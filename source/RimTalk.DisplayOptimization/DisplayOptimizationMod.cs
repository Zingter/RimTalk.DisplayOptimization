using System;
using UnityEngine;
using Verse;
using System.Collections.Generic;
//3.11  22.33开始修改ui！！！
namespace RimTalk.DisplayOptimization
{
    public enum IntervalMode { Ticks, Seconds }
    
    public enum RuleType
    {
        Paired, // 配对染色模式 (例如 [[ ]] -> <color>【 】</color>)
        Simple  // 简单替换模式 (例如 "User:" -> "")
    }

    public class CustomRule : IExposable
    {
        public string Name = "新规则";
        public RuleType Type = RuleType.Paired;

        // --- 通用装饰字段 ---
        public bool EnableColor = false;
        public string ColorHex = "#FFFFFF";
        public bool EnableBold = false;
        public bool EnableItalic = false;
        public bool UseRegex = false; // 是否启用正则表达式

        // --- 类型 A: 配对模式专用 ---
        public string InputL = "[";
        public string InputR = "]";
        public string OutputL = "【";
        public string OutputR = "】";

        // --- 类型 B: 简单/正则模式专用 ---
        public string TargetText = "";      
        public string ReplacementText = ""; 

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "Name", "规则");
            Scribe_Values.Look(ref Type, "Type", RuleType.Paired);
            Scribe_Values.Look(ref EnableColor, "EnableColor", false);
            Scribe_Values.Look(ref ColorHex, "ColorHex", "#FFFFFF");
            Scribe_Values.Look(ref EnableBold, "EnableBold", false);
            Scribe_Values.Look(ref EnableItalic, "EnableItalic", false);
            Scribe_Values.Look(ref UseRegex, "UseRegex", false);

            Scribe_Values.Look(ref InputL, "InputL", "[");
            Scribe_Values.Look(ref InputR, "InputR", "]");
            Scribe_Values.Look(ref OutputL, "OutputL", "【");
            Scribe_Values.Look(ref OutputR, "OutputR", "】");
            Scribe_Values.Look(ref TargetText, "TargetText", "");
            Scribe_Values.Look(ref ReplacementText, "ReplacementText", "");
        }
    }
    public class DisplayOptimizationSettings : ModSettings
    {
        public bool EnableDirectorMode = false, EnableThoughtColor = true, EnableThoughtItalic = false, EnableHighlighting = true, EnableBolding = true, EnableEmphasis = true, EnableNoWrap = true, EnableEmphasisBolding = false;
        public string ColorAction = "#A9A9A9", ColorThought = "#D28DFF", ColorEmphasis = "#FF6B6B", ColorColonist = "#CC7A00";
        public string SymActionL = "（", SymActionR = "）";
        public string SymThoughtL = "〖", SymThoughtR = "〗";
        public int SoulReaderPersistence = 2, IntervalHighlight = 4, IntervalEmphasis = 3;
        public bool EnableHistoryCircles = true, CircleAtEnd = false, OnlyShowLastSymbol = false;
        public bool EnableActionColor = true;
        public bool EnableActionItalic = false;
        
        public bool EnableFailsafePusher = true;
        public bool ForceSpeakIgnored = false;
        public float SpeakIntervalSeconds = 4.0f;
        public IntervalMode IntervalMode = IntervalMode.Ticks;
        public bool SpeakWhilePaused = false;
        public KeyCode IgnoreAllKey = KeyCode.Home; // 默认删除键
        
        public KeyCode StopAllTalkKey = KeyCode.T; // 暂停对话按键
        public bool StopAllTalk = false;
        public bool FollowGamePause = true;
        
        
        public bool StopSpeakingInMenus = false; // 窗口开启时停止说话（默认关闭：避免开着主标签页时对话被吞）
        public bool AdvancedMenuAvoidance = true; // 高级避让
        
        // 【新增】：调试开关，控制是否对历史记录进行视觉加工
        public bool EnableHistoryProcessing = true; 
        
        //public bool ForceSpeakIgnoringState = true; 
        
        public List<CustomRule> CustomRules = new List<CustomRule>();
        
        private Dictionary<string, int> _savedTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableDirectorMode, "EnableDirectorMode", false);
            Scribe_Values.Look(ref EnableThoughtColor, "EnableThoughtColor", true);
            Scribe_Values.Look(ref EnableThoughtItalic, "EnableThoughtItalic", false);
            Scribe_Values.Look(ref EnableNoWrap, "EnableNoWrap", true);
            Scribe_Values.Look(ref EnableActionColor, "EnableActionColor", true);
            Scribe_Values.Look(ref EnableActionItalic, "EnableActionItalic", false);
            Scribe_Values.Look(ref EnableHighlighting, "EnableHighlighting", true);
            Scribe_Values.Look(ref EnableBolding, "EnableBolding", true);
            Scribe_Values.Look(ref EnableEmphasis, "EnableEmphasis", true);
            Scribe_Values.Look(ref EnableEmphasisBolding, "EnableEmphasisBolding", false);
            Scribe_Values.Look(ref ColorAction, "ColorAction", "#A9A9A9");
            Scribe_Values.Look(ref ColorThought, "ColorThought", "#D28DFF");
            Scribe_Values.Look(ref ColorEmphasis, "ColorEmphasis", "#FF6B6B");
            Scribe_Values.Look(ref ColorColonist, "ColorColonist", "#CC7A00");
            Scribe_Values.Look(ref SymActionL, "SymActionL", "（"); Scribe_Values.Look(ref SymActionR, "SymActionR", "）");
            Scribe_Values.Look(ref SymThoughtL, "SymThoughtL", "〖"); Scribe_Values.Look(ref SymThoughtR, "SymThoughtR", "〗");
            Scribe_Values.Look(ref SoulReaderPersistence, "SoulReaderPersistence", 2);
            Scribe_Values.Look(ref IntervalHighlight, "IntervalHighlight", 4);
            Scribe_Values.Look(ref IntervalEmphasis, "IntervalEmphasis", 3);
            
            Scribe_Values.Look(ref EnableHistoryCircles, "EnableHistoryCircles", true);
            Scribe_Values.Look(ref CircleAtEnd, "CircleAtEnd", false);
            Scribe_Values.Look(ref OnlyShowLastSymbol, "OnlyShowLastSymbol", false);
            
            Scribe_Collections.Look(ref CustomRules, "CustomRules", LookMode.Deep);
            if (CustomRules == null) CustomRules = new List<CustomRule>();
            
            Scribe_Values.Look(ref EnableHistoryProcessing, "EnableHistoryProcessing", true);
            
            Scribe_Values.Look(ref EnableFailsafePusher, "EnableFailsafePusher", true);
            Scribe_Values.Look(ref ForceSpeakIgnored, "ForceSpeakIgnored", false);
            Scribe_Values.Look(ref SpeakIntervalSeconds, "SpeakIntervalSeconds", 4.0f);
            Scribe_Values.Look(ref IntervalMode, "IntervalMode", IntervalMode.Ticks);
            Scribe_Values.Look(ref SpeakWhilePaused, "SpeakWhilePaused", false);
            Scribe_Values.Look(ref FollowGamePause, "FollowGamePause", true);
            Scribe_Values.Look(ref StopSpeakingInMenus, "StopSpeakingInMenus", false);
            Scribe_Values.Look(ref IgnoreAllKey, "IgnoreAllKey", KeyCode.Home);
            Scribe_Values.Look(ref StopAllTalkKey, "StopAllTalkKey", KeyCode.T);
           
            Scribe_Values.Look(ref AdvancedMenuAvoidance, "AdvancedMenuAvoidance", true);
            
            //Scribe_Values.Look(ref ForceSpeakIgnoringState, "ForceSpeakIgnoringState", true);
            
            try
            {
                Scribe_Values.Look(ref IntervalMode, "IntervalMode", IntervalMode.Ticks);
            }
            catch (Exception) 
            {
                // 如果存档里的值（如 RealTime）在新版本中不存在，强制重置为默认值
                IntervalMode = IntervalMode.Ticks; 
            }
            
        }
    }

    public class DisplayOptimizationMod : Mod
    {
        public static DisplayOptimizationSettings Settings;
        private Vector2 _scrollPosition = Vector2.zero;
        
        public static List<string> CurrentReaders = new List<string>(); 
        
        public static int SoulReaderCountdown = 0, SessionDialogueCount = 0;

        public DisplayOptimizationMod(ModContentPack content) : base(content) => Settings = GetSettings<DisplayOptimizationSettings>();
        public static void ResetSessionState() 
        { 
            SoulReaderCountdown = SessionDialogueCount = 0; 
            CurrentReaders.Clear(); 
            Settings.StopAllTalk = false;
        }
        private bool IsChinese => LanguageDatabase.activeLanguage?.folderName.Contains("Chinese") ?? LanguageDatabase.activeLanguage?.info.friendlyNameNative.Contains("中文") ?? false;

        public override string SettingsCategory() => IsChinese ? "Rimtalk-对话显示拓展" : "RimTalk-Display Optimization";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float scrollHeight = 1600f; // 基础高度，用于容纳所有固定开关和滑块
            
            // 遍历规则列表，累加高度
            if (Settings.CustomRules != null)
            {
                foreach (var rule in Settings.CustomRules)
                {
                    scrollHeight += (rule.Type == RuleType.Paired ? 140f : 130f) + 6f;
                }
            }
          
            // 使用计算出来的高度
            Rect viewRect = new Rect(0f, 0f, inRect.width - 30f, scrollHeight);
            // --- 【修改结束】 ---

            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ==========================================
            // 1. 渲染引擎开关
            // ==========================================
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "渲染引擎开关" : "Rendering Engine Controls");
            Text.Font = GameFont.Small;
            listing.Gap(5f);
            listing.CheckboxLabeled(IsChinese ? "启用〖心声〗模式" : "〖Inner Voice〗 Mode", ref Settings.EnableDirectorMode,
                IsChinese ? "预计额外增加150-300token，无需额外提示词，开启即用。可于设置中自由修改符号与颜色" : "Estimated increase of 150-300 tokens. No additional prompts are needed—ready to use immediately. you can freely modify symbols and colors in the settings");
            listing.Gap(4f);
            listing.CheckboxLabeled(IsChinese ? "启用心声染色" : "Enable Inner Voice Coloring", ref Settings.EnableThoughtColor, 
                    IsChinese ? "对设置中的心声符号进行涂色" : "Color the inner voice symbols in settings");
            if (Settings.EnableThoughtColor)
            {
                // 二级缩进
                listing.CheckboxLabeled((IsChinese ? "  └─ 心声斜体" : "  └─ Italicize Inner Voice"), ref Settings.EnableThoughtItalic,
                    IsChinese ? "不建议开启，偶尔会造成显示问题，应对修复了一下，但可能会失效" : "Not recommended to enable. display issues may occur Occasionally. We attempted to fix it, but it may still fail.");
                
            }
            listing.Gap(8f);
            listing.CheckboxLabeled(IsChinese ? "启用动作染色" : "Enable action Coloring", ref Settings.EnableActionColor);
            if (Settings.EnableActionColor)
            {
                 // 二级缩进
                listing.CheckboxLabeled((IsChinese ? "  └─ 动作斜体" : "  └─ Italicize Actions"), ref Settings.EnableActionItalic,
                    IsChinese ? "同上，不建议开启" : "As above, it is not recommended to enable.\n\n");
                
            }
            listing.Gap(8f);
            listing.CheckboxLabeled(IsChinese ? "启用名字高亮" : "Enable Name Highlighting", ref Settings.EnableHighlighting);
            if (Settings.EnableHighlighting) 
            {
                
                listing.CheckboxLabeled(IsChinese ? "  └─ 名字加粗" : "  └─ Bold Names", ref Settings.EnableBolding);
                
            }
            listing.Gap(8f);
            listing.CheckboxLabeled(IsChinese ? "启用重点标注" : "Enable Emphasis", ref Settings.EnableEmphasis);
            if (Settings.EnableEmphasis) 
            {
                
                listing.CheckboxLabeled(IsChinese ? "  └─ 重点加粗" : "  └─ Bold Emphasis", ref Settings.EnableEmphasisBolding);
                
            }
            listing.Gap(8f);
            listing.CheckboxLabeled(IsChinese ? "文本强制单行补丁" : "Anti-Linebreak Patch", ref Settings.EnableNoWrap);
            
            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(10f);

            // ==========================================
            // 2. 视觉效果自定义
            // ==========================================
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "视觉效果自定义" : "Visual Customization");
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            listing.Label(IsChinese ? "两个不要用同一种，且不能为空！建议对称，如【】《》（）「」等，不建议使用<>\"\"以及英文括号()" : "Do not use the same type of symbol for both elements! Cannot be empty！Use paired symbols like【】《》（）「」,but avoid <>\"\" . In English contexts, it is recommended to change the default parentheses （Chinese parentheses） to (English parentheses).");
            Text.Font = GameFont.Small;
            listing.Gap(5f);
            
            GUI.color = (Settings.EnableThoughtColor || Settings.EnableDirectorMode) ? Color.white : Color.gray;
            DrawSymbolRow(listing, IsChinese ? "心声" : "Thought", ref Settings.SymThoughtL, ref Settings.SymThoughtR, ref Settings.ColorThought, true);
            GUI.color = Settings.EnableActionColor ? Color.white : Color.gray;
            DrawSymbolRow(listing, IsChinese ? "动作" : "Action", ref Settings.SymActionL, ref Settings.SymActionR, ref Settings.ColorAction, true);
            GUI.color = Settings.EnableHighlighting ? Color.white : Color.gray;
            DrawColorRow(listing, IsChinese ? "名字" : "Name Color", ref Settings.ColorColonist);
            GUI.color = Settings.EnableEmphasis ? Color.white : Color.gray;
            DrawColorRow(listing, IsChinese ? "重点" : "Emphasis Color", ref Settings.ColorEmphasis);
            
            // 重置按钮居右或美观排版，这里留点间距
            listing.Gap(5f);
            GUI.color = Color.white;
            Rect resetRect = listing.GetRect(24f);
            if (Widgets.ButtonText(new Rect(resetRect.x + 12f, resetRect.y, 200f, 24f), IsChinese ? "恢复默认值" : "Reset Defaults"))
            {
                Settings.ColorAction = "#A9A9A9"; Settings.ColorThought = "#D28DFF";
                Settings.ColorEmphasis = "#FF6B6B"; Settings.ColorColonist = "#CC7A00";
                Settings.SymActionL = "（"; Settings.SymActionR = "）";
                Settings.SymThoughtL = "〖"; Settings.SymThoughtR = "〗";
            }

            listing.Gap(15f);
            listing.GapLine();
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "自定义额外染色规则 (高级):" : "Custom Coloring Rules (Advanced):");
            
            
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            listing.Label(IsChinese ? "两种规则都可随意，但配对染色规则前后符号一样时，不能处理嵌套包裹。且替换前的内容可以为文字，替换后的内容可随意自定，包括为空，可在下面的“<color=#CCCCFF>文本调试</color>”中自行查看" : "Both rules are optional, but when the Paired Rule uses the same symbol before and after, it cannot handle nested wrapping.The content before replacement can be text, The replaced content can be freely customized, including leaving it empty. You can test it out in the \"<color=#CCCCFF>Text Debugger</color>\" below.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(5f);

            // 绘制按钮区域 (规则添加)
            Rect btnRect = listing.GetRect(30); 
            float btnWidth = (btnRect.width - 10) / 2;
            
            if (Widgets.ButtonText(new Rect(btnRect.x, btnRect.y, btnWidth, 30), IsChinese ? "添加：配对染色规则" : "Add: Paired Rule"))
            {
                if (Settings.CustomRules == null) Settings.CustomRules = new List<CustomRule>();
                Settings.CustomRules.Add(new CustomRule { Type = RuleType.Paired, Name = IsChinese ? "新染色规则" : "New Color Rule" });
            }
            if (Widgets.ButtonText(new Rect(btnRect.x + btnWidth + 10, btnRect.y, btnWidth, 30), IsChinese ? "添加：简单替换规则" : "Add: Simple Replace"))
            {
                if (Settings.CustomRules == null) Settings.CustomRules = new List<CustomRule>();
                Settings.CustomRules.Add(new CustomRule { Type = RuleType.Simple, Name = IsChinese ? "新替换规则" : "New Replace Rule", TargetText = "any words", ReplacementText = "" });
            }
            
            listing.Gap(10f);

            // 遍历绘制规则列表
            var rules = Settings.CustomRules;
            if (rules != null)
            {
               for (int i = 0; i < rules.Count; i++)
               {
                   CustomRule rule = rules[i];
                   float ruleHeight = (rule.Type == RuleType.Paired) ? 140f : 130f;
                   Rect ruleRect = listing.GetRect(ruleHeight);
                    
                   // 背景框区分
                   if (rule.Type == RuleType.Paired) Widgets.DrawMenuSection(ruleRect);
                   else Widgets.DrawWindowBackground(ruleRect);

                   GUI.BeginGroup(ruleRect.ContractedBy(8));
                   float width = ruleRect.width - 16;
                   float lineH = 24f;

                   // --- 第一行布局 ---
                   float typeLabelW = 50f;
                   float nameLabelW = 35f;
                   float sortBtnW = 24f;
                   float delBtnW = 50f;
                   // 计算中间名字输入框的宽度
                   float nameFieldW = width - typeLabelW - nameLabelW - (sortBtnW * 2) - delBtnW - 25;

                   Text.Anchor = TextAnchor.MiddleLeft;
                   GUI.color = rule.Type == RuleType.Paired ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.8f, 0.7f);
                   Widgets.Label(new Rect(0, 0, typeLabelW, lineH), (rule.Type == RuleType.Paired ? (IsChinese ? "[染色]" : "[Color]") : (IsChinese ? "[替换]" : "[Rep]")));
                   GUI.color = Color.white;
                    
                   Widgets.Label(new Rect(typeLabelW, 0, nameLabelW, lineH), IsChinese ? "名称:" : "Name:");
                   rule.Name = Widgets.TextField(new Rect(typeLabelW + nameLabelW, 0, nameFieldW, lineH), rule.Name);

                   // --- 调序按钮 ---
                   if (i > 0) {
                       if (Widgets.ButtonText(new Rect(width - delBtnW - sortBtnW * 2 - 10, 0, sortBtnW, lineH), "↑")) {
                           var temp = rules[i]; rules[i] = rules[i - 1]; rules[i - 1] = temp;
                       }
                   }
                   if (i < rules.Count - 1) {
                       if (Widgets.ButtonText(new Rect(width - delBtnW - sortBtnW - 5, 0, sortBtnW, lineH), "↓")) {
                           var temp = rules[i]; rules[i] = rules[i + 1]; rules[i + 1] = temp;
                       }
                   }

                   // --- 删除按钮 ---
                   GUI.color = new Color(1f, 0.5f, 0.5f);
                   if (Widgets.ButtonText(new Rect(width - delBtnW, 0, delBtnW, lineH), IsChinese ? "删除" : "Del")) {
                       rules.RemoveAt(i); i--;
                       GUI.color = Color.white; GUI.EndGroup(); continue;
                   }
                   GUI.color = Color.white;
                   Text.Anchor = TextAnchor.UpperLeft;

                   // --- 内容行渲染 ---
                   float y = lineH + 6;
                   if (rule.Type == RuleType.Paired)
                   {
                       float labelW = 70;
                       float inputW = (width - labelW - 30) / 2; 

                       Widgets.Label(new Rect(0, y, labelW, lineH), IsChinese ? "检测符号:" : "Detect:");
                       rule.InputL = Widgets.TextField(new Rect(labelW, y, inputW, lineH), rule.InputL);
                       Widgets.Label(new Rect(labelW + inputW + 5, y, 20, lineH), "...");
                       rule.InputR = Widgets.TextField(new Rect(labelW + inputW + 25, y, inputW, lineH), rule.InputR);

                       y += lineH + 6;
                       Widgets.Label(new Rect(0, y, labelW, lineH), IsChinese ? "替换符号:" : "Replace:");
                       rule.OutputL = Widgets.TextField(new Rect(labelW, y, inputW, lineH), rule.OutputL);
                       Widgets.Label(new Rect(labelW + inputW + 5, y, 20, lineH), "...");
                       rule.OutputR = Widgets.TextField(new Rect(labelW + inputW + 25, y, inputW, lineH), rule.OutputR);

                       y += lineH + 6;
                       DrawDecorationLine(new Rect(0, y, width, lineH), rule);
                   }
                   else
                   {
                       // --- 简单模式内容渲染 ---
                       float labelW = 70;
    
                       // 正则开关
                       Widgets.CheckboxLabeled(new Rect(width - 80, y, 80, lineH), IsChinese ? "正则替换" :"Regex", ref rule.UseRegex);

                       Widgets.Label(new Rect(0, y, labelW, lineH), IsChinese ? "目标文本:" : "Target:");
                       rule.TargetText = Widgets.TextField(new Rect(labelW, y, width - labelW - 85, lineH), rule.TargetText);
    
                       y += lineH + 6;
                       Widgets.Label(new Rect(0, y, labelW, lineH), IsChinese ? "替换为:" : "To:");
                       rule.ReplacementText = Widgets.TextField(new Rect(labelW, y, width - labelW, lineH), rule.ReplacementText);

                       // 【重要：补上这一行，简单替换才有颜色和加粗选项】
                       y += lineH + 6;
                       DrawDecorationLine(new Rect(0, y, width, lineH), rule);
                   }
                   GUI.EndGroup();
                   listing.Gap(6f);
               }
                                
            }
            
            listing.GapLine();
            listing.Gap(10f);

            // ==========================================
            // 3. 指令频率
            // ==========================================
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "3. AI 指令策略 (Token 节省)" : "3. AI Instruction Strategy");
            Text.Font = GameFont.Small;
            listing.Gap(5f);

            
            Settings.SoulReaderPersistence = (int)listing.SliderLabeled((IsChinese ? "读心规则持续轮次: " : "Advanced Persistence: ") + Settings.SoulReaderPersistence, Settings.SoulReaderPersistence, 1, 10);
            Settings.IntervalHighlight = (int)listing.SliderLabeled((IsChinese ? "名字高亮发送间隔: " : "Name Interval: ") + Settings.IntervalHighlight, Settings.IntervalHighlight, 1, 10);
            Settings.IntervalEmphasis = (int)listing.SliderLabeled((IsChinese ? "重点标注发送间隔: " : "Emphasis Interval: ") + Settings.IntervalEmphasis, Settings.IntervalEmphasis, 1, 10);
            
            
            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(10f);

            // ==========================================
            // 4. 进度提示
            // ==========================================
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "4. 进度提示 (○/●)" : "4. Progress Symbols (○/●)");
            Text.Font = GameFont.Small;
            listing.Gap(5f);
            
            
            listing.CheckboxLabeled(IsChinese ? "启用进度提示功能" : "Enable Progress Symbols", ref Settings.EnableHistoryCircles,
                IsChinese ? "开启后，所有台词将添加 ○/●,注意：现对话气泡的 ○/● 必须依托对话历史才可以正常显示" : "Add ○/● to dialogues. Note: The ○/● in bubbles can only display properly if they rely on the dialog history.");
            
            if(Settings.EnableHistoryCircles)
            {
                listing.Gap(8f);
                listing.CheckboxLabeled(IsChinese ? "仅末尾句才显示标记 (隐藏 ○)" : "Only show last symbol (Hidden ○)", ref Settings.OnlyShowLastSymbol);
                listing.Gap(8f);
                if (listing.RadioButton(IsChinese ? "符号置于句首" : "Symbol at start", !Settings.CircleAtEnd)) Settings.CircleAtEnd = false;
                if (listing.RadioButton(IsChinese ? "符号置于句末" : "Symbol at end", Settings.CircleAtEnd)) Settings.CircleAtEnd = true;
                
                
            }
            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(10f);

            // ==========================================
            // 5. 对话调度器
            // ==========================================
            Text.Font = GameFont.Medium;
            listing.Label(IsChinese ? "对话调度系统" : "5. Conversation Dispatcher"); 
            Text.Font = GameFont.Small;
            listing.Gap(5f);
            listing.CheckboxLabeled(IsChinese ? "启用对话调度功能" : "Enable Smart Dispatcher", ref Settings.EnableFailsafePusher,
                IsChinese ? "该功能并未完全测试到位，可能存在bug，或影响游戏性能，如果发现问题，欢迎于创意工坊详细说明情况" : "This feature has not been fully tested and may contain bugs or affect game performance. If you encounter any issues, please feel free to describe them in detail on the Steam Workshop");
            
            if (Settings.EnableFailsafePusher)
            {
                listing.Gap(8f);
                // 强制对话
                listing.CheckboxLabeled(IsChinese ? "对话强制说出" : "Force Dialogue Output", ref Settings.ForceSpeakIgnored,
                    IsChinese ? "建议开启，此时对话将会被强制推送（无论任何情况，如小人离开地图等）；若关闭，则如果出现上述情况时，对话将会停滞，待超时检测补丁生效后才会将此次对话清楚" : "Recommended to enable. When enabled, the dialogue will be forcibly pushed through (regardless of any circumstances, such as the pawn leaving the map, etc.). If disabled, and any of the above situations occur, the dialogue will stall and will only be cleared after the timeout detection patch takes effect."  );
                
                listing.Gap(8f);
                // 说话间隔
                string sliderLabel = Settings.SpeakIntervalSeconds.ToString("F1") + "s";
                listing.Label((IsChinese ? "对话说出间隔:  " : "Speak Interval:  ") + sliderLabel);
                float val = listing.Slider(Settings.SpeakIntervalSeconds, 0.5f, 8.0f);
                Settings.SpeakIntervalSeconds = (float)Math.Round(val * 10f) / 10f;
                
                listing.Gap(8f);
        
                Rect ignoreKeyRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(ignoreKeyRect.LeftPart(0.6f), IsChinese ? "一键忽略当前对话:" : "Keybind to ignore all talk:");
                if (Widgets.ButtonText(ignoreKeyRect.RightPart(0.4f), Settings.IgnoreAllKey.ToString()))
                {
                    // 弹出自定义的按键监听窗口
                    Find.WindowStack.Add(new DialogAssignKey(
                        IsChinese ? "请按下你要绑定的按键...\n(按 ESC 取消，按 Backspace 清除)" : "Press any key to bind...\n(ESC to cancel, Backspace to clear)",
                        (key) => Settings.IgnoreAllKey = key
                    ));
                }
                listing.Gap(4f); // 两个按键之间留点空隙

                // --- 2. 一键停止对话按键 ---
                Rect stopKeyRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(stopKeyRect.LeftPart(0.6f), IsChinese ? "一键停止对话按键:" : "Keybind to stop all talk:");
                if (Widgets.ButtonText(stopKeyRect.RightPart(0.4f), Settings.StopAllTalkKey.ToString()))
                {
                    // 弹出自定义的按键监听窗口
                    Find.WindowStack.Add(new DialogAssignKey(
                        IsChinese ? "请按下你要绑定的按键...\n(按 ESC 取消，按 Backspace 清除)" : "Press any key to bind...\n(ESC to cancel, Backspace to clear)",
                        (key) => Settings.StopAllTalkKey = key
                    ));
                }
                listing.Gap(8f);
                // 2. 暂停说话开关 (现在全模式支持，直接显示)
                listing.CheckboxLabeled(IsChinese ? "允许在游戏暂停时说话" : "Allow speaking while paused", ref Settings.SpeakWhilePaused);
                if (Settings.SpeakWhilePaused)
                {
                    listing.CheckboxLabeled(
                        IsChinese ? "  └─ 对话暂停跟随游戏状态" : "  └─ Sync Dialogue Pause with Game",
                        ref Settings.FollowGamePause,
                        IsChinese
                            ? "开启后，暂停游戏会自动暂停对话（即RimTalk原版逻辑）。\n优点：虽然它会自动跟随暂停，但你仍可以在游戏暂停期间按下“一键暂停键”手动恢复对话。"
                            : "When enabled, pausing the game automatically pauses dialogue (mimics vanilla RimTalk).\nBonus: You can still manually resume dialogue using the keybind while the game remains paused.");
                }
                
                listing.Gap(8f);
                listing.CheckboxLabeled(IsChinese ? "打开窗口时暂停说话 (如调试/设置)" : "Stop speaking when Logs are open", ref Settings.StopSpeakingInMenus,
                    IsChinese
                        ? "仅调试窗口、详情窗口等"
                        : "Only debug windows, detail windows, etc.");
                if (Settings.StopSpeakingInMenus)
                {
                    listing.CheckboxLabeled(
                        IsChinese
                            ? "高级即时暂停 (包含各种菜单)"
                            : "Advanced menu avoidance (Includes building/research menus, etc.)",
                        ref Settings.AdvancedMenuAvoidance,
                        IsChinese
                            ? "包括大部分窗口，如建筑规划、研究等，以及与小人对话时的输入窗口"
                            : "Covers most windows, such as building planning, research, etc., and input windows when chatting with characters.");
                }

                listing.Gap(8f);

                // --- 3. 时间模式选择 (单选框模式) ---
                listing.Label(IsChinese ? "对话间隔计算模式:" : "Speak Interval Mode:");
                
                if (listing.RadioButton(
                        "  " + (IsChinese ? "游戏刻 (受倍速影响)" : "Ticks (Affected by speed)"), 
                        Settings.IntervalMode == IntervalMode.Ticks))
                {
                    Settings.IntervalMode = IntervalMode.Ticks;
                }
                
                if (listing.RadioButton(
                        "  " + (IsChinese ? "现实秒 (恒定速度)" : "Seconds (Constant speed)"), 
                        Settings.IntervalMode == IntervalMode.Seconds))
                {
                    Settings.IntervalMode = IntervalMode.Seconds;
                }
                
            }
            
            listing.GapLine();
            listing.Gap(10f);

            // 绘制按钮
            if (listing.ButtonText(IsChinese ? "高级模式适配说明" : "Advanced Compatibility Guide"))
            {
                // 检查是否已经打开了，防止重复弹出
                if (Find.WindowStack.WindowOfType<DialogAdvancedGuide>() == null)
                {
                    Find.WindowStack.Add(new DialogAdvancedGuide());
                }
            }
            
            listing.Gap(5f);

            // --- 新增：调试按钮 ---
            GUI.color = new Color(0.8f, 0.8f, 1f); // 给按钮加点浅蓝色暗示这是调试工具
            if (listing.ButtonText(IsChinese ? "文本调试" : "Text Debugger"))
            {
                if (Find.WindowStack.WindowOfType<DialogTextDebug>() == null)
                {
                    Find.WindowStack.Add(new DialogTextDebug());
                }
            }
            GUI.color = Color.white;
            
            listing.End();
            Widgets.EndScrollView();
        }

        
        // 这是一个辅助绘图方法，放在类内部
        private void DrawDecorationLine(Rect rect, CustomRule rule)
        {
            float x = 0;
            float checkW = 60f;
            float hexW = 80f;

            // 染色开关
            Widgets.CheckboxLabeled(new Rect(x, rect.y, checkW, rect.height), IsChinese ? "染色" : "Color", ref rule.EnableColor);
            x += checkW + 5;

            // 颜色输入框 (仅在开启时显示，否则变灰)
            if (rule.EnableColor) {
                rule.ColorHex = Widgets.TextField(new Rect(x, rect.y, hexW, rect.height), rule.ColorHex);
            } else {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(x, rect.y, hexW, rect.height), " #------");
                GUI.color = Color.white;
            }
            x += hexW + 15;

            // 粗体开关
            Widgets.CheckboxLabeled(new Rect(x, rect.y, checkW, rect.height), IsChinese ? "粗体" : "Bold", ref rule.EnableBold);
            x += checkW + 10;

            // 斜体开关
            Widgets.CheckboxLabeled(new Rect(x, rect.y, checkW, rect.height), IsChinese ? "斜体" : "Italic", ref rule.EnableItalic);
        }
        
        
        private void DrawSymbolRow(Listing_Standard listing, string label, ref string left, ref string right, ref string color, bool showSymbols)
        {
            Rect rect = listing.GetRect(28f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, 28f), label);
            color = Widgets.TextField(new Rect(rect.x + 130f, rect.y, 90f, 24f), color);
            if (showSymbols)
            {
                left = Widgets.TextField(new Rect(rect.x + 235f, rect.y, 30f, 24f), left);
                Widgets.Label(new Rect(rect.x + 270f, rect.y, 15f, 24f), "..");
                right = Widgets.TextField(new Rect(rect.x + 290f, rect.y, 30f, 24f), right);
            }
        }

        private void DrawColorRow(Listing_Standard listing, string label, ref string color)
        {
            Rect rect = listing.GetRect(28f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, 28f), label);
            color = Widgets.TextField(new Rect(rect.x + 130f, rect.y, 90f, 24f), color);
        }
    }
    
    public class DialogAssignKey : Window
    {
        private Action<KeyCode> onKeyAssigned;
        private string promptMsg;

        public DialogAssignKey(string promptMsg, Action<KeyCode> onKeyAssigned)
        {
            this.promptMsg = promptMsg;
            this.onKeyAssigned = onKeyAssigned;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true; // 拦截窗口外的输入，防止误触游戏
            this.doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(350f, 150f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(inRect, promptMsg);
            Text.Anchor = TextAnchor.UpperLeft;

            // 核心逻辑：捕获当前的键盘事件
            if (Event.current.isKey && Event.current.type == EventType.KeyDown)
            {
                KeyCode pressedKey = Event.current.keyCode;

                // 过滤掉 None，防止无效触发
                if (pressedKey != KeyCode.None)
                {
                    if (pressedKey == KeyCode.Escape)
                    {
                        // 按 ESC 直接退出，不改变按键
                        this.Close();
                    }
                    else if (pressedKey == KeyCode.Backspace)
                    {
                        // 可选：按退格键把快捷键置空 (如果你的逻辑允许设为 None 的话)
                        onKeyAssigned?.Invoke(KeyCode.None);
                        this.Close();
                    }
                    else
                    {
                        // 记录按下的按键并关闭窗口
                        onKeyAssigned?.Invoke(pressedKey);
                        this.Close();
                    }
                    Event.current.Use(); // 消耗掉这个按键事件，防止传给游戏底层
                }
            }
        }
    }
    
    
    
}