using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimTalk.API;
using RimTalk.Client;
using RimTalk.Service;
using RimTalk.Data;
using RimTalk.Source.Data;
using RimTalk.UI;
using RimTalk.Prompt;
using RimTalk.Util;
using Verse;
using RimWorld;
using UnityEngine;
using Cache = RimTalk.Data.Cache;

namespace RimTalk.DisplayOptimization
{
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        public static FieldInfo CachedStringField;


        // 语言判定
        private static bool IsChinese => LanguageDatabase.activeLanguage?.folderName.Contains("Chinese") ??
                                         LanguageDatabase.activeLanguage?.info.friendlyNameNative.Contains("中文") ??
                                         false;

        static ModInit()
        {

            var harmony = new Harmony("RimTalk.DisplayOptimization.Master.V33");

            // 基础反射缓存
            CachedStringField =
                typeof(PlayLogEntry_RimTalkInteraction).GetField("_cachedString",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            // 1. 拦截条目构建 (解决简单模式注入 & 仅涂色模式逻辑)
            var buildFromPreset = AccessTools.Method(typeof(PromptManager), "BuildMessagesFromPreset");
            if (buildFromPreset != null)
                harmony.Patch(buildFromPreset,
                    prefix: new HarmonyMethod(typeof(ModInit), nameof(Prefix_EntryNameInjector)));
            
            // 3. 实时提供变量内容 (Scriban 渲染时调用)
            var render = AccessTools.Method(typeof(ScribanParser), "Render");
            if (render != null)
                harmony.Patch(render, prefix: new HarmonyMethod(typeof(ModInit), nameof(Prefix_ResolveVariables)));
            
            // 4. 其他常规补丁加载
            new PatchClassProcessor(harmony, typeof(PromptPatch)).Patch();
            new PatchClassProcessor(harmony, typeof(HistoryPatch)).Patch();
            new PatchClassProcessor(harmony, typeof(ExportPatch)).Patch();
            new PatchClassProcessor(harmony, typeof(TalkHistorySanitizePatch)).Patch();
            new PatchClassProcessor(harmony, typeof(TalkServicePatch)).Patch();
            new PatchClassProcessor(harmony, typeof(DeletionInterceptPatch)).Patch();
            new PatchClassProcessor(harmony, typeof(ForceTalkUpdateOnPause)).Patch();
            new PatchClassProcessor(harmony, typeof(PlayLogOrderFix)).Patch();
            new PatchClassProcessor(harmony, typeof(PlayerDialogueOrderPatch)).Patch();
            new PatchClassProcessor(harmony, typeof(IgnoreAllInterceptPatch)).Patch();
           
            // 构造函数与重置钩子
            var ctor = AccessTools.Constructor(typeof(PlayLogEntry_RimTalkInteraction),
                new[] { typeof(InteractionDef), typeof(Pawn), typeof(Pawn), typeof(List<RulePackDef>) });
            if (ctor != null)
                harmony.Patch(ctor,
                    postfix: new HarmonyMethod(typeof(PlayLogCtorPatch), nameof(PlayLogCtorPatch.Postfix)));

            var finalizeInit = AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit));
            if (finalizeInit != null)
                harmony.Patch(finalizeInit, postfix: new HarmonyMethod(typeof(ModInit), nameof(ResetOnLoad)));

            // 在 ModInit 的 static ModInit() 内部
            var getVars = AccessTools.Method(typeof(VariableDefinitions), "GetScribanVariables");
            if (getVars != null)
            {
                // 注意：这里确保 Postfix_RegisterVariables 的签名与上面 Patch 类一致
                harmony.Patch(getVars, postfix: new HarmonyMethod(typeof(RegisterVariablesPatch), nameof(RegisterVariablesPatch.Postfix)));
            }
            
            // 1. 注册动态变量到引擎（确保 {{ rt_total }} 能用）
            // 注意：这里的 ModId 必须统一，方便后续 UI 过滤
            const string myModId = "RimTalk.DisplayOptimization";
            RegisterDynamicVariables(myModId);
            
            // 2. 彻底清理旧存档/配置文件里的“病毒”变量
            KillVariableVirus();
            
            Log.Message("<color=#FFA500>[RimTalk-显示优化]</color> 加载成功");
        }
        
        // 修正：重置逻辑
        public static void ResetOnLoad()
        {
            DisplayOptimizationMod.ResetSessionState();
            // 【完善】：调用 Patch 类的清理方法
            TalkServicePatch.ClearData();
            
            // 3. 【核心修复】：强行清洗 RimTalk 本体的 Pawn 缓存
            // 很多跨存档报错都是因为 RimTalk 缓存了旧存档的 Pawn 对象
            // 注意：RimTalk 1.2.0 (1.6) 中 Cache 内部字段已改为 PawnCache / NameCache，
            // 旧字段 "items" 已不存在 → 旧反射清理会静默失效，导致跨存档残留 PawnState
            // （其中的 TalkResponses / IsGeneratingTalk 可能残留 true），使该 Pawn 永久无法生成对话。
            // 因此这里改用公开方法 Cache.Clear()（清空所有 Pawn/名称缓存）后立即 Cache.Refresh()
            // 让 RimTalk 用当前地图的可说话 Pawn 重新填充缓存。
            try
            {
                Cache.Clear();
                Cache.Refresh();
            }
            catch (Exception ex)
            {
                Log.Warning("[RimTalk-清理] 清理本体缓存失败: " + ex.Message);
            }
            
        }


        // 【核心优化】：通过条目名称精准控制逻辑，支持“仅涂色模式”
        public static void Prefix_EntryNameInjector(PromptPreset preset)
        {
            if (preset == null || preset.Entries == null) return;
            var s = DisplayOptimizationMod.Settings;

            foreach (var entry in preset.Entries)
            {
                string entryName = entry.Name?.Trim() ?? "";

                // 处理 JSON Format 条目
                if (entryName.Equals("JSON Format", StringComparison.OrdinalIgnoreCase))
                {
                    if (!entry.Content.Contains("{{ text_rules }}"))
                        entry.Content += "\n{{ text_rules }}";
                }

                // 处理 Dialogue Prompt 条目
                if (entryName.Equals("Dialogue Prompt", StringComparison.OrdinalIgnoreCase))
                {
                    // 只有在【未开启】仅涂色模式时，才注入心声规则
                    if (s.EnableDirectorMode)
                    {
                        if (!entry.Content.Contains("{{ soul_rules }}"))
                            entry.Content += "\n{{ soul_rules }}";
                    }
                    else
                    {
                        // 如果开启了“仅涂色”，则移除可能存在的注入标签（防止简单/高级切换残留）
                        if (entry.Content.Contains("{{ soul_rules }}"))
                            entry.Content = entry.Content.Replace("{{ soul_rules }}", "");
                    }
                }
            }
        }

        // 让我们的变量出现在高级模式的参考表 UI 里（支持英文）
        private static void RegisterDynamicVariables(string modId)
        {
            // 注册到 ContextHookRegistry，这解决了“变量逻辑”问题
            // 这样 Scriban 引擎就能通过 TryGetContextVariable 找到这些变量
            ContextHookRegistry.RegisterContextVariable("rt_total", modId, 
                (Func<object, string>)(ctx => (DisplayOptimizationMod.SessionDialogueCount - 1).ToString()), 
                IsChinese ? "当前总轮次 (数字)" : "Current turn count");

            ContextHookRegistry.RegisterContextVariable("soul_rules", modId, 
                (Func<object, string>)(ctx => {
                    var s = DisplayOptimizationMod.Settings;
                    if (!s.EnableDirectorMode) return "";
                    bool advanced = DisplayOptimizationMod.SoulReaderCountdown > 0;
                    return SyncService.GetSoulSystemEntry(advanced, DisplayOptimizationMod.CurrentReaders);
                }), 
                IsChinese ? "心声系统" : "Inner monologue");

            ContextHookRegistry.RegisterContextVariable("text_rules", modId, 
                (Func<object, string>)(ctx => {
                    var s = DisplayOptimizationMod.Settings;
                    int currentIdx = DisplayOptimizationMod.SessionDialogueCount - 1;
                    bool force = (currentIdx < 3);
                    return SyncService.GetJsonFormattingRules(force || (currentIdx % s.IntervalHighlight == 0), force || (currentIdx % s.IntervalEmphasis == 0));
                }), 
                IsChinese ? "文本规则" : "formatting rules");
        }
        
        private static void KillVariableVirus()
        {
            try 
            {
                var store = PromptManager.Instance?.VariableStore;
                if (store != null) 
                {
                    var dictField = AccessTools.Field(typeof(VariableStore), "_variables");
                    if (dictField?.GetValue(store) is System.Collections.IDictionary dict) 
                    {
                        // 所有的清理目标名单
                        string[] virusKeys = { 
                            "rt_total", "soul_rules", "text_rules", 
                            "text_rule", "text rules", "soul rules", 
                            "soul_persistence", "player_mute" 
                        };

                        foreach (var key in virusKeys)
                        {
                            // 尝试清理各种可能的变体（带空格、大小写等）
                            if (dict.Contains(key)) dict.Remove(key);
                            if (dict.Contains(key.ToLowerInvariant())) dict.Remove(key.ToLowerInvariant());
                            if (dict.Contains(key.Replace("_", " "))) dict.Remove(key.Replace("_", " "));
                        }
                    }
                }
            } 
            catch (Exception ex) { Log.Warning("[RimTalk-显示优化] 清理测试变量时出错: " + ex.Message); }
        }
        
        // 在 ModInit 类中或独立类中
        [HarmonyPatch(typeof(VariableDefinitions), "GetScribanVariables")]
        public static class RegisterVariablesPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Dictionary<string, List<(string name, string description)>> __result)
            {
                if (__result == null) return;

                // 1. 确定语言和分类名
                bool isCn = LanguageDatabase.activeLanguage?.folderName.Contains("Chinese") ?? false;
                string myCat = isCn ? "RimTalk-显示优化" : "RimTalk-Display Optimization";

                // 2. 准备我们的正式变量信息
                var myVars = new List<(string, string)>
                {
                    ("rt_total", isCn ? "当前总轮次 (数字)" : "Current turn count"),
                    ("soul_rules", isCn ? "心声系统规则" : "Inner monologue rules"),
                    ("text_rules", isCn ? "文本染色规则" : "Formatting rules")
                };

                // 3. 【核心逻辑】：从全字典中清理掉这三个变量，防止它们出现在其他栏位
                foreach (var category in __result.Values)
                {
                    category.RemoveAll(v => 
                            v.name.Equals("rt_total") || 
                            v.name.Equals("soul_rules") || 
                            v.name.Equals("text_rules") ||
                            v.name.Contains("RimTalk.DisplayOptimization") // 清理残留的自动描述项
                    );
                }

                // 4. 强行插入我们的分类到字典第一位（或者指定位置）
                // 如果字典里已经有这个分类了，先删掉确保数据最新
                if (__result.ContainsKey(myCat)) __result.Remove(myCat);
        
                // 重新赋值
                __result[myCat] = myVars;

                // 5. 调试：如果还是没看到，解除下面这行的注释，进游戏看控制台日志
                // Log.Message($"[RT-Debug] 变量列表已注入分类: {myCat}, 包含变量数: {myVars.Count}");
            }
        }

        // --- 2. 修改变量内容解析：只保留 rt_total 的物理替换 ---
        public static void Prefix_ResolveVariables(ref string templateText, PromptContext context)
        {
            // 【关键修复】：去掉了 context.IsPreview 的检查，允许预览时也进行替换
            if (context == null || string.IsNullOrEmpty(templateText)) return;

            // 获取当前序号，确保预览时至少为 0
            int currentIdx = Math.Max(0, DisplayOptimizationMod.SessionDialogueCount - 1);

            // 物理替换：将变量名直接换成数字字面量，以支持 {{ rt_total % 2 }} 数学运算
            templateText = Regex.Replace(templateText, @"\brt_total\b", currentIdx.ToString());
        
            // 严禁在此处调用 store.SetVar，防止产生新“病毒”
        }
        
        
    }

    public static class PromptPatch
    {
        [HarmonyPatch(typeof(PromptService), "DecoratePrompt",
            new[] { typeof(TalkRequest), typeof(List<Pawn>), typeof(string) })]
        [HarmonyPostfix]
        public static void Postfix(TalkRequest talkRequest, List<Pawn> pawns)
        {
            if (talkRequest == null) return;
            var s = DisplayOptimizationMod.Settings;

            // 1. 读心者状态判定 (完全不动)
            var player = Cache.GetPlayer();
            bool readerInvolved = (pawns != null && (pawns.Contains(player) || pawns.Any(p => p.story?.traits?.HasTrait(TraitDef.Named("RimTalk_MindReader")) == true)));
            if (talkRequest.TalkType == TalkType.User || readerInvolved)
            {
                DisplayOptimizationMod.SoulReaderCountdown = s.SoulReaderPersistence;
            }

            // 2. 构造动态名单 (完全不动)
            DisplayOptimizationMod.CurrentReaders.Clear();
            if (DisplayOptimizationMod.SoulReaderCountdown > 0)
            {
                var currentReaders = (pawns ?? new List<Pawn>())
                    .Where(p => p == player || p.story?.traits?.HasTrait(TraitDef.Named("RimTalk_MindReader")) == true)
                    .Select(p => p.LabelShort).Distinct().ToList();
                if (currentReaders.Count > 0) DisplayOptimizationMod.CurrentReaders = currentReaders;
            }

            // --- 计数器自增 ---
            DisplayOptimizationMod.SessionDialogueCount++;

            if (!readerInvolved && talkRequest.TalkType != TalkType.User &&
                DisplayOptimizationMod.SoulReaderCountdown > 0)
            {
                DisplayOptimizationMod.SoulReaderCountdown--;
            }
        }
    }
    
    //##用于拦截并修改历史记录文本，包含染色以及添加进度圆圈##
    public static class HistoryPatch
    {
        private static readonly object _lock = new object();
        private static Guid lastRequestId = Guid.Empty;
        private static readonly Dictionary<string, ApiLog> lastLogsBySpeaker = new Dictionary<string, ApiLog>();
        
        private static long globalSequenceCounter = 0;
        private static readonly Dictionary<Guid, long> MessageSequenceMap = new Dictionary<Guid, long>();
        
        public static long GetSequence(Guid id)
        {
            lock (_lock)
            {
                return MessageSequenceMap.TryGetValue(id, out long seq) ? seq : 0;
            }
        }
        
        // 3. 封装清理逻辑，内部调用
        private static void InternalClean()
        {
            // 只有超过 200 条时才清理，保留最近的 100 条
            if (MessageSequenceMap.Count > 200)
            {
                long thresholdSeq = globalSequenceCounter - 100;
                var keysToRemove = MessageSequenceMap
                    .Where(kvp => kvp.Value < thresholdSeq)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var k in keysToRemove) MessageSequenceMap.Remove(k);
            }
        }
        
        public static ApiLog GetLatestUnspokenLogBySpeaker(string name)
        {
            lock (_lock)
            {
                return ApiHistory.GetAll()
                    .Where(l => l != null
                                && l.SpokenTick == 0
                                && l.Name == name
                                && MessageSequenceMap.ContainsKey(l.Id))
                    .OrderByDescending(l => MessageSequenceMap[l.Id])
                    .FirstOrDefault();
            }
        }
        
        

        // ===== 处理两种 AddResponse 重载 =====
        // RimTalk 1.6 中 ApiHistory.AddResponse 存在两个重载：
        //   6 参：AddResponse(Guid, string, string, string, Payload, int)          —— Player2 / 带图 OpenAI 路径
        //   7 参：AddResponse(Guid, string, string, string, Payload, int, string) —— 官方 OpenAI 主路径
        // 旧版只截获 6 参，导致 OpenAI 用户生成的对话不进入序号/圆圈逻辑，
        // TalkResponses 一直无法被调度器消费，最终表现为“对话无法生成”。
        // 因此这里对两个重载都做 Prefix/Postfix，逻辑复用共享实现。

        [HarmonyPatch(typeof(ApiHistory), "AddResponse", new Type[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload), typeof(int) })]
        [HarmonyPrefix]
        public static void Prefix(Guid id, string response, string name)
        {
            PrefixImpl(id, name);
        }

        [HarmonyPatch(typeof(ApiHistory), "AddResponse", new Type[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload), typeof(int), typeof(string) })]
        [HarmonyPrefix]
        public static void Prefix7(Guid id, string response, string name, string interactionType, Payload payload, int elapsedMs, string targetName)
        {
            PrefixImpl(id, name);
        }

        private static void PrefixImpl(Guid id, string name)
        {
            if (string.IsNullOrEmpty(name)) return; 
            lock (_lock)
            {
                if (id != lastRequestId) { lastRequestId = id; lastLogsBySpeaker.Clear(); }

                var s = DisplayOptimizationMod.Settings;
                if (s.EnableHistoryProcessing && s.EnableHistoryCircles)
                {
                    string hollowSym = s.OnlyShowLastSymbol ? "" : "○";
                    if (lastLogsBySpeaker.TryGetValue(name, out ApiLog previousLog))
                    {
                        if (previousLog != null && !string.IsNullOrEmpty(previousLog.Response))
                            previousLog.Response = TextProcessor.ReplaceCircle(previousLog.Response, hollowSym);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ApiHistory), "AddResponse", new Type[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload), typeof(int) })]
        public static void Postfix(ApiLog __result, string name)
        {
            PostfixImpl(__result, name);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ApiHistory), "AddResponse", new Type[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(Payload), typeof(int), typeof(string) })]
        public static void Postfix7(ApiLog __result, string name, string targetName)
        {
            PostfixImpl(__result, name);
        }

        private static void PostfixImpl(ApiLog __result, string name)
        {
            if (__result == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(__result.Response)) return;
            lock (_lock)
            {
                // 1. 发号：给这条消息一个唯一的递增序号
                globalSequenceCounter++;
                MessageSequenceMap[__result.Id] = globalSequenceCounter;
                
                InternalClean();
                
                lastLogsBySpeaker[name] = __result;
                var s = DisplayOptimizationMod.Settings;
                if (s.EnableHistoryProcessing)
                {
                    // 关键：先 StripCircles。这样无论 Response 之前是什么状态，这里拿到的都是纯文本
                    
                    string formatted = TextProcessor.ProcessBase(__result.Response);
                    string symbol = s.EnableHistoryCircles ? "●" : "";
                    // 这里的修改仅影响 ApiHistory 列表显示
                    __result.Response = TextProcessor.ApplySymbol(formatted, symbol);
                }
            }
        }

        public static void ClearData()
        {
            lock (_lock)
            {
                lastRequestId = Guid.Empty;
                lastLogsBySpeaker.Clear();
                // 重置发号器
                globalSequenceCounter = 0;
                MessageSequenceMap.Clear();
            }
        }
        
        public static void CleanOldRecords()
        {
            lock (_lock)
            {
                if (MessageSequenceMap.Count > 150)
                {
                    long thresholdSeq = globalSequenceCounter - 80;
                    
                    var keysToRemove = MessageSequenceMap.Where(kvp => kvp.Value < thresholdSeq).Select(kvp => kvp.Key).ToList();
                    foreach (var k in keysToRemove) MessageSequenceMap.Remove(k);
                }
            }
        }
    }
     
    //##专门针对对话气泡部分的修改，当对话说出时，直接检测之前的文本并添加对话进度##
public static class PlayLogCtorPatch
    {
        public static void Postfix(object __instance, Pawn initiator)
        {
            if (ModInit.CachedStringField == null || __instance == null || initiator == null) return;

            // 获取构造函数传入的原始文本 (带有 | , * , = 等符号)
            string rawText = (string)ModInit.CachedStringField.GetValue(__instance);
            if (string.IsNullOrEmpty(rawText)) return;

            try
            {
                string processedRaw = TextProcessor.ProcessBase(rawText);
                ModInit.CachedStringField.SetValue(__instance, processedRaw);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-显示优化] Error: {ex}");
            }
        }
    }
    
    //##用于游戏暂停时依旧触发对话##
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootUpdate")]
    public static class ForceTalkUpdateOnPause
    {
        private static float lastCheckTime = 0f;
        [HarmonyPostfix]
        public static void Postfix()
        {
            var s = DisplayOptimizationMod.Settings;
            if (!s.EnableFailsafePusher) return;
            
            bool isGamePaused = Find.TickManager.Paused;

            // 1. 一键忽略对话按键检测
            if (UnityEngine.Input.GetKeyDown(s.IgnoreAllKey))
            {
                TalkServicePatch.ClearAllDialoguesForce();
                Messages.Message(LanguageDatabase.activeLanguage.folderName.Contains("Chinese") ? "已跳过所有积压对话" : "All dialogues skipped", MessageTypeDefOf.CautionInput, false);
            }
            
            
            // --- 核心：自动跟随逻辑 ---
            if (s.FollowGamePause)
            {
                // 检测暂停状态切换瞬间
                if (isGamePaused != TalkServicePatch.lastPausedState)
                {
                    if (isGamePaused) // 游戏刚暂停
                    {
                        // 如果当前没被手动暂停，则自动切断对话
                        if (!s.StopAllTalk)
                        {
                            s.StopAllTalk = true;
                            TalkServicePatch.wasAutoPaused = true;
                        }
                    }
                    else // 游戏刚恢复运行
                    {
                        // 如果之前的暂停是“跟随暂停”导致的自动停，则现在自动恢复
                        if (s.StopAllTalk && TalkServicePatch.wasAutoPaused)
                        {
                            s.StopAllTalk = false;
                            TalkServicePatch.wasAutoPaused = false;
                        }
                    }
                    TalkServicePatch.lastPausedState = isGamePaused;
                }
            }
            
            // --- 【新增】：一键暂停/恢复按键检测 (假设你的按键变量叫 StopAllTalkKey) ---
            if (UnityEngine.Input.GetKeyDown(s.StopAllTalkKey))
            {
                s.StopAllTalk = !s.StopAllTalk;

                // 关键：如果玩家在暂停期间手动“恢复”了对话，我们要清除自动暂停标记
                // 否则等游戏恢复运行时，逻辑会产生混乱。
                if (!s.StopAllTalk && TalkServicePatch.wasAutoPaused)
                {
                    TalkServicePatch.wasAutoPaused = false;
                }

                
                // 只有手动按键才播报消息
                string msg = s.StopAllTalk ? 
                    (LanguageDatabase.activeLanguage.folderName.Contains("Chinese") ?  "当前对话已暂停" : "Dialogue globally paused") : 
                    (LanguageDatabase.activeLanguage.folderName.Contains("Chinese") ? "当前对话已恢复" : "Dialogue resumed");
                Messages.Message(msg, s.StopAllTalk ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
            }
                 
            // 2. 暂停时依旧说话功能部分
            if (isGamePaused && s.SpeakWhilePaused)
            {
                // 只有当 ShouldBlockTalk 返回 false (即没被拦截) 时才执行
                if (!TalkServicePatch.ShouldBlockTalk())
                {
                    if (Time.realtimeSinceStartup - lastCheckTime > 0.15f)
                    {
                        lastCheckTime = Time.realtimeSinceStartup;
                        TalkService.DisplayTalk();
                    }
                }
            }
        }
    }
     
    //##核心功能部分，拦截rimtalk本体的对话说出功能，然后以此处的逻辑来接管##
    [HarmonyPatch(typeof(TalkService), "DisplayTalk")]
    public static class TalkServicePatch
{
    private static readonly List<PendingSpeech> allPendingData = new List<PendingSpeech>(128);
    private static readonly HashSet<Guid> activeTalkIds = new HashSet<Guid>();
    
    private static readonly Dictionary<string, float> lastSpeakerSpeakTime = new Dictionary<string, float>();
    private static readonly Dictionary<int, float> lastRoundSpeakTime = new Dictionary<int, float>();
        
    // 核心：所有计时回归现实秒
    private static float lastGlobalProcessTime = 0f;
    private static float lastMaintenanceTime = 0f;
    private static float lastGlobalSpeakTime = -9999f;
        
    // 保持你原有的字段
    public static int forcedTickCounter = 0;
    public static readonly FieldInfo ticksAbsField = AccessTools.Field(typeof(LogEntry), "ticksAbs");
    internal static bool lastPausedState = false;
    internal static bool wasAutoPaused = false;
    private static IntervalMode lastUsedMode = IntervalMode.Ticks;
    
    private static bool globalBlocked = false;
    private static PendingSpeech blockingItem = null;
    
    // 记录每个对话轮次（ConversationId）开始卡住的现实时间戳
    private static readonly Dictionary<int, float> roundStuckTimer = new Dictionary<int, float>();
    
    // 【掉帧优化】：CreateInteraction 的 MethodInfo 只在静态构造时查找缓存一次。
    // 旧代码在 ExecuteTalk 里每次弹气泡都执行 AccessTools.Method(...) 反射查找，
    // 高频对话时每帧都在反射+分配对象，是掉帧/GC 的主要热点之一。
    private static readonly MethodInfo createInteractionMethod =
        AccessTools.Method(typeof(TalkService), "CreateInteraction");
    
    public static int GetNextDisplayTick()
    {
        if (forcedTickCounter < GenTicks.TicksAbs) forcedTickCounter = GenTicks.TicksAbs;
        forcedTickCounter++;
        return forcedTickCounter;
    }
    
    // 抽取 ShouldBlockTalk，确保暂停逻辑正确
    public static bool ShouldBlockTalk()
    {
        var s = DisplayOptimizationMod.Settings;
        if (s.StopAllTalk) return true;
        if (s.StopSpeakingInMenus)
        {
            var windowStack = Find.WindowStack;
            if (windowStack != null && windowStack.Count > 1)
            {
                foreach (var w in windowStack.Windows)
                {
                    if (w.layer == WindowLayer.Dialog)
                    {
                        var type = w.GetType();
                        if (type.Namespace != null && type.Namespace.StartsWith("RimTalk"))
                        {
                            if (s.AdvancedMenuAvoidance && type.Name == "CustomDialogueWindow") return true;
                            continue;
                        }
                        return true;
                    }
                    if (s.AdvancedMenuAvoidance && (w is MainTabWindow && !(w is MainTabWindow_Inspect))) return true;
                }
            }
        }
        // 关键：这里决定了暂停时是否允许执行
        if (Find.TickManager.Paused && !s.SpeakWhilePaused) return true;
        return false;
    }
    
    [HarmonyPrefix]
public static bool Prefix()
{
    
    var s = DisplayOptimizationMod.Settings;
    if (!s.EnableFailsafePusher) return true;

    float now = Time.realtimeSinceStartup;
    // 【根因修复】：0.15s 节流只让“调度器自己”放慢，绝不能吞掉 RimTalk 原版 DisplayTalk，
    // 否则对话生成后永远无人消费 → 调试窗口大量 Pending/Ignored、气泡不显示。
    // 因此这里改回放行原版（return true），调度器想说话时在下方主动 return false 抢占。
    if (now - lastGlobalProcessTime < 0.15f) return true;
    lastGlobalProcessTime = now;

    // 【根因修复】：ShouldBlockTalk（窗口避让/暂停避让）只表示“调度器不主动接管”，
    // 必须放行原版 DisplayTalk 由 RimTalk 自己正常显示。旧代码 return false 会把原版
    // 整个吞掉 —— 开着建筑/研究/需求主标签页（AdvancedMenuAvoidance 默认开）时就
    // 表现为“RimTalk 不再显示任何对话”。
    if (ShouldBlockTalk()) return true;
    
    // --- 清理缓存容器 ---
    allPendingData.Clear();
    activeTalkIds.Clear();

    // --- 1. 时间算法：倍速跟随 ---
    float multiplier = Find.TickManager.Paused ? 1.0f : Find.TickManager.TickRateMultiplier;
    if (multiplier < 0.1f) multiplier = 1.0f;
    float threshold = s.IntervalMode == IntervalMode.Ticks ? (s.SpeakIntervalSeconds / multiplier) : s.SpeakIntervalSeconds;
    if (s.SpeakIntervalSeconds > 0f && now - lastGlobalSpeakTime < 0.15f) return false;

    var pawnsInCache = Cache.Keys.ToList(); 
    
    // 直接从小人状态里拿对话
    foreach (var pawn in pawnsInCache)
    {

        if (pawn == null)
        {
            // Log.Message($"角色 {pawn.LabelShort} 为空！直接跳过");
            continue;
        }
            
        var state = Cache.Get(pawn);
       
        if (state == null)
            continue;

        // ★ 这一步原版 DisplayTalk 每 Tick 都会做
        DrainIncomingTalkResponsesMethod?.Invoke(state, null);

        if (state.TalkResponses == null || state.TalkResponses.Count == 0) continue;
        
        // 使用 for 循环遍历 List 是最安全的
        for (int i = 0; i < state.TalkResponses.Count; i++)
        {
            var t = state.TalkResponses[i];
            
            if (t == null) continue;
        
            activeTalkIds.Add(t.Id);
        
            var log = ApiHistory.GetApiLog(t.Id);
            
            if (log == null)
            {
                continue;
            }

            allPendingData.Add(new PendingSpeech {
                Pawn = pawn,
                SpeakerName = pawn.LabelShort,
                State = state,
                Talk = t,
                Log = log,
                SequenceId = HistoryPatch.GetSequence(log.Id) // 确保你已经添加了上一步给你的 GetSequence 方法
            });
        }
    }

    // 抓取虚拟发言（玩家或无实体的发言）
    var recentLogs = ApiHistory.GetAll().Reverse().Take(50);
    foreach (var log in recentLogs)
    {
        // 基础防御检查
        if (log == null) continue;

        // 核心逻辑：
        // 1. SpokenTick == 0 表示该消息尚未被调度系统“说出”
        //    （兼容修复：RimTalk 1.6 中 -1=已忽略，0=待说出，>0=已说出）
        // 2. !activeTalkIds.Contains(log.Id) 确保这条消息没有在小人的待播队列里重复出现
        if (log.SpokenTick == 0 && !activeTalkIds.Contains(log.Id))
        {
            allPendingData.Add(new PendingSpeech 
            {
                Pawn = null, // 历史记录中的消息通常视为无实体消息（或由玩家发送）
                SpeakerName = log.Name,
                State = null,
                // 构造一个 Talk 对象用于后续逻辑一致性
                Talk = new TalkResponse(TalkType.User, log.Name, log.Response) { Id = log.Id },
                Log = log,
                SequenceId = HistoryPatch.GetSequence(log.Id)
            });
        }
    }

    if (allPendingData.Count == 0) return true;

    // --- 3. 排序与轮次锁 ---
    allPendingData.Sort((a, b) => a.SequenceId.CompareTo(b.SequenceId));
    
    var absoluteRoundMinSeq = allPendingData.GroupBy(l => l.Log.ConversationId).ToDictionary(g => g.Key, g => g.Min(l => l.SequenceId));
    HashSet<int> blockedRounds = new HashSet<int>();

    // --- 4. 扫描分发 ---
    foreach (var item in allPendingData)
    {
       
        int rid = item.Log.ConversationId;
        if (blockedRounds.Contains(rid)) continue;

        // 【序号锁】：本轮对话只要前面有序号更小的没说，后面绝不敢动
        if (absoluteRoundMinSeq.TryGetValue(rid, out long realMin) && item.SequenceId > realMin)
        {
            blockedRounds.Add(rid);
            continue;
        }

        // 【状态判定】：是否强制忽略小人状态
        
        bool isPlayer = item.Pawn == null || item.Pawn.IsPlayer();
        bool spawned = item.Pawn != null && item.Pawn.Spawned;
        bool dead = item.Pawn != null && item.Pawn.Dead;
        bool canDisplay = item.State != null && item.State.CanDisplayTalk();

        bool ready = isPlayer
                     || s.ForceSpeakIgnored
                     || (spawned && !dead && item.State != null && canDisplay);
        
        if (!ready)
        {
            /*
            Log.Message(
                $"[BLOCK] " +
                $"{item.SpeakerName} " +
                $"Seq={item.SequenceId}"
            );
            */
            // --- 【新增：超时丢弃逻辑】 ---
            // 只有当禁止忽略开关关闭，且游戏正在运行时，才开启秒表
            // ————————————————此处ForceSpeakIgnored为暂时替代ForceSpeakIgnoringState——————————————
            if (!s.ForceSpeakIgnored && !Find.TickManager.Paused && !s.StopAllTalk)
            {
                if (!roundStuckTimer.TryGetValue(rid, out float startTime))
                {
                    roundStuckTimer[rid] = now;
                }
                else if (now - startTime > threshold + 5.0f)
                {
                    // 超过了 (间隔 + 5秒) 还没准备好，整轮丢弃！
                    SkipRoundForce(rid);
                    roundStuckTimer.Remove(rid);
                    return false; // 这一帧直接跳出，让历史记录刷新
                }
            }
            blockedRounds.Add(rid); // 不就绪，卡住本轮进度，绝不跳过
            continue;
        }

        // 【冷却判定】
        lastSpeakerSpeakTime.TryGetValue(item.SpeakerName, out float lpTime);
        lastRoundSpeakTime.TryGetValue(rid, out float lrTime);

        if (now - lpTime >= threshold && now - lrTime >= threshold)
        {
            ExecuteTalk(item, rid, now);
            return false;
        }
        else
        {
            blockedRounds.Add(rid);
        }
    }
    return false;
}

    // 修复旧版不兼容问题（8.21）
    private static readonly MethodInfo DrainIncomingTalkResponsesMethod =
        typeof(PawnState).GetMethod(
            "DrainIncomingTalkResponses",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            Type.EmptyTypes,
            null
        );
    
    private static void SkipRoundForce(int conversationId)
    {
        // 1. 获取该轮次所有还没说的日志
        // 【兼容修正】：RimTalk 1.6 中 SpokenTick 语义为 0=待说出、-1=已忽略、>0=已说出。
        // 旧代码用 -1 判断“没说”，在新版里会漏掉所有待说的日志（它们现在是 0），
        // 导致超时跳轮永远找不到目标日志 → 对话依旧卡死。改为 == 0。
        var history = ApiHistory.GetAll();
        var logsToSkip = history.Where(l => l.ConversationId == conversationId && l.SpokenTick == 0).ToList();
        if (logsToSkip.Count == 0) return;

        var idsToSkip = logsToSkip.Select(l => l.Id).ToHashSet();

        // 2. 逻辑层：在历史记录中“盖章”，并通知 RimTalk 内部系统这些话已结束
        foreach (var log in logsToSkip)
        {
            log.SpokenTick = GenTicks.TicksGame;
            TalkHistory.AddSpoken(log.Id); // 必须执行，否则 RimTalk 内部逻辑会认为对话没完
        }

        // 3. 物理层：从小人“肚子”里彻底删掉这些对话
        // 遍历所有正在被 RimTalk 缓存的小人状态
        var cachedPawns = Cache.Keys.ToList(); 
        foreach (var pawn in cachedPawns)
        {
            if (pawn == null) continue;
            var state = Cache.Get(pawn);
            if (state != null && state.TalkResponses != null)
            {
                // 既然拦截了 Ignore 功能，我们就必须在这里手动 RemoveAll
                state.TalkResponses.RemoveAll(x => idsToSkip.Contains(x.Id));
            }
        }
        
        // 5. 刷新 UI
        roundStuckTimer.Remove(conversationId);
        
        Overlay.NotifyLogUpdated();
        Log.Message($"[RimTalk-显示优化] 检测到对话轮次 {conversationId} 物理卡死，已强制跳过整轮。");
    }
    
      private static void ExecuteTalk(PendingSpeech item, int roundId, float timeNow)
{
    try
    {
        
        // 1. 净化文本，确保存入 Talk 对象的是纯净文本（不带圆圈）
        if (item.Log != null && !string.IsNullOrEmpty(item.Log.Response)) 
            item.Talk.Text = TextProcessor.StripCircles(item.Log.Response);
        
        bool isPlayer = item.Pawn == null || item.Pawn.IsPlayer();
        
        // 2. 物理表现判定
        // 只要小人在场且活着，就尝试让他产生 RimTalk 原生的社交互动（头顶气泡）
        if (item.Pawn != null && item.Pawn.Spawned && !item.Pawn.Dead)
        {
if (!isPlayer)
            {
                // 如果这是 NPC，调用 RimTalk 原生方法弹气泡
                // 【掉帧优化】：使用静态缓存的 MethodInfo，不再每次气泡反射查找 CreateInteraction
                createInteractionMethod?.Invoke(null, new object[] { item.Pawn, item.Talk });
            }

            // --- 关键：清理物理队列 ---
            // 既然我们拦截了 RimTalk 的 Ignore 函数，小人的 TalkResponses 就是唯一的仓库
            // 话既然说出来了，就必须从小人的“肚子（队列）”里彻底删掉，否则会无限循环播放
            if (item.State != null)
            {
                /*
                Log.Message(
                    $"[REMOVE_BEFORE] " +
                    $"{item.SpeakerName} " +
                    $"Queue="
                    + string.Join(",",
                        item.State.TalkResponses.Select(x=>HistoryPatch.GetSequence(x.Id)))
                    );
                */
                item.State.TalkResponses.RemoveAll(x => x.Id == item.Talk.Id);
                /*
                Log.Message(
                    $"[REMOVE_AFTER] " +
                    $"{item.SpeakerName} " +
                    $"Queue="
                    + string.Join(",",
                        item.State.TalkResponses.Select(x=>HistoryPatch.GetSequence(x.Id)))
                );
                */
            }
        }
        else
        {
            // 如果小人不在场（失踪或强制推进模式），我们只需确保把这句话从状态缓存里清理掉
            if (item.State != null)
            {
                item.State.TalkResponses.RemoveAll(x => x.Id == item.Talk.Id);
            }
        }

        // 4. 逻辑推进（真理盖章）
        // 无论有没有弹气泡，这一步是解锁“序号锁”的唯一钥匙
        TalkHistory.AddSpoken(item.Talk.Id);
        
        if (item.Log != null)
        {
            item.Log.SpokenTick = GenTicks.TicksGame;
        }

        // 5. 更新调度时间戳
        lastSpeakerSpeakTime[item.SpeakerName] = timeNow;
        lastRoundSpeakTime[roundId] = timeNow;
        lastGlobalSpeakTime = timeNow; 
        
        // 通知历史记录 UI 刷新（圆圈变黑）
        Overlay.NotifyLogUpdated();

        // 6. 重置阻塞状态（如果你决定保留这个全局变量的话，不建议保留，Prefix 里的 blockedRounds 更稳）
        // globalBlocked = false;
        // blockingItem = null;
    }
    catch (Exception ex) 
    { 
        Log.Error($"[RimTalk-显示优化] 异常: {ex}"); 
    }
}
        

    public static void ClearAllDialoguesForce()
    {
        ClearData();
        foreach (var pawn in Cache.Keys.ToList()) Cache.Get(pawn)?.TalkResponses.Clear();
        Overlay.NotifyLogUpdated();
    }

    public static void ClearData()
    {
        lastSpeakerSpeakTime.Clear();
        lastRoundSpeakTime.Clear();
        HistoryPatch.ClearData();
        lastGlobalProcessTime = 0f;
        lastGlobalSpeakTime = -9999f;
        forcedTickCounter = GenTicks.TicksAbs;
    }

    private class PendingSpeech { public Pawn Pawn; public string SpeakerName; public PawnState State; public TalkResponse Talk; public ApiLog Log; public bool IsRescued; public long SequenceId; }
}

    //##针对于rimtalk忽略对话函数的拦截，一旦检测到有对话被忽略，立刻启动，然后执行保留对话##
    [HarmonyPatch(typeof(PawnState), "IgnoreTalkResponse")]
    public static class DeletionInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PawnState __instance)
        {
            var s = DisplayOptimizationMod.Settings;
            // 如果开启了调度器且开启了“禁止忽略”，直接拦截 RimTalk 的删除行为
            if (s.EnableFailsafePusher && s.ForceSpeakIgnored)
            {
                return false; // 返回 false 意味着 RimTalk 本体的删除逻辑被跳过，对话保留在队列中
            }
            return true;
        }
    }
    
    [HarmonyPatch(typeof(PawnState), "IgnoreAllTalkResponses")]
    public static class IgnoreAllInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PawnState __instance)
        {
            var s = DisplayOptimizationMod.Settings;
            if (s.EnableFailsafePusher && s.ForceSpeakIgnored)
            {
                // 同样禁止清空。只有我们自己的调度器说出一句，才能删掉一句
                return false; 
            }
            return true;
        }
    }
        
    //##下面两个都是用于检测是否为玩家说话，是针对玩家说话位置错误的补丁##
    [HarmonyPatch(typeof(PlayLogEntry_RimTalkInteraction), MethodType.Constructor, new[] { typeof(InteractionDef), typeof(Pawn), typeof(Pawn), typeof(List<RulePackDef>) })]
    public static class PlayLogOrderFix
    {
        [HarmonyPostfix]
        public static void Postfix(PlayLogEntry_RimTalkInteraction __instance)
        { 
            if (!DisplayOptimizationMod.Settings.EnableFailsafePusher) return;

            int uniqueDisplayTick = TalkServicePatch.GetNextDisplayTick();
            if (TalkServicePatch.ticksAbsField != null)
            {
                TalkServicePatch.ticksAbsField.SetValue(__instance, uniqueDisplayTick);
            }
        }
    }

// 拦截 RimTalk 对话条目的构造函数，确保 100% 捕获玩家手动发送的消息
    // 【兼容修复】：RimTalk 1.6 中 CustomDialogueService.ExecuteDialogue 有两个重载
    //   ExecuteDialogue(Pawn, Pawn, string, bool)               ← 4 参（转发到5参）
    //   ExecuteDialogue(Pawn, Pawn, string, bool, string)       ← 5 参（真正的入口）
    // 实测跟踪：玩家手动对话 / 对话窗口发送 / 定时器分发 全部走 5 参重载：
    //   CustomDialogueWindow.DispatchDialogue → CustomDialogueService.DispatchDialogue
    //   → ExecuteDialogue(initiator, recipient, message, isAnnouncement, imageBase64)
    // 旧版只按名字 patch 存在二义性，若命中 4 参重载则永远不会捕获玩家消息。
    // 这里显式指定 5 参类型数组，确保命中真实入口。
    [HarmonyPatch(typeof(CustomDialogueService), "ExecuteDialogue",
        new Type[] { typeof(Pawn), typeof(Pawn), typeof(string), typeof(bool), typeof(string) })]
    public static class PlayerDialogueOrderPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn initiator)
        {
           if (initiator == null || !initiator.IsPlayer()) return;

           var lastLog = HistoryPatch.GetLatestUnspokenLogBySpeaker(initiator.LabelShort);
           if (lastLog != null)
           {
               lastLog.SpokenTick = GenTicks.TicksGame;
               Overlay.NotifyLogUpdated();
           }
       }
   }

    // 【无损导出补丁】：解决导出脱色导致游戏内变白的问题
    public static class ExportPatch
    {
        // 临时存储区，用于备份原始带颜色的文本
        private static readonly Dictionary<ApiLog, string> _backupCache = new Dictionary<ApiLog, string>();

        [HarmonyPatch(typeof(UIUtil), "ExportLogs")]
        [HarmonyPrefix]
        public static void Prefix(List<ApiLog> apiLogs)
        {
            if (apiLogs == null) return;
            _backupCache.Clear();

            foreach (var log in apiLogs)
            {
                if (log.Response != null)
                {
                    // 1. 备份原始带颜色和标签的文本
                    _backupCache[log] = log.Response;
                    
                    // 2. 将原始文本“脱色”，仅保留符号用于导出
                    log.Response = TextProcessor.StripTagsOnly(log.Response);
                }
            }
        }

        [HarmonyPatch(typeof(UIUtil), "ExportLogs")]
        [HarmonyPostfix]
        public static void Postfix(List<ApiLog> apiLogs)
        {
            if (apiLogs == null) return;

            // 3. 导出结束后，从备份中恢复所有文本，游戏界面瞬间变回彩色
            foreach (var log in apiLogs)
            {
                if (_backupCache.TryGetValue(log, out string originalText))
                {
                    log.Response = originalText;
                }
            }
            _backupCache.Clear();
        }
    }
    
    // 【核心新增】：AI 记忆清洗补丁
    public static class TalkHistorySanitizePatch
    {
        // 拦截 RimTalk 存储对话记忆的方法
        [HarmonyPatch(typeof(TalkHistory), "AddMessageHistory")]
        [HarmonyPrefix]
        public static void Prefix(ref string request, ref string response)
        {
            // 在对话存入 AI 记忆前，彻底剥离所有颜色标签、加粗标签和圆圈符号
            // 这样 AI 看到的上下文将永远是纯净的文本
            if (!string.IsNullOrEmpty(request))
            {
                request = TextProcessor.StripEverything(request);
            }

            if (!string.IsNullOrEmpty(response))
            {
                // 这里建议也调用一下 StripCircles，以防万一
                response = TextProcessor.StripCircles(TextProcessor.StripTagsOnly(response));
            }
        }
    }
    
}