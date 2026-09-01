using UnityEngine;
using Verse;
using System;

namespace RimTalk.DisplayOptimization
{
    public class DialogTextDebug : Window
    {
        private string inputBuffer = $"这里输入测试文本，例如：|泰南|！快过来，（笑了笑）这个**好吃** 〖才怪，估计会食物中毒。〗";
        private string outputResult = "";
        private Vector2 scrollPosInput = Vector2.zero;
        private Vector2 scrollPosOutput = Vector2.zero;

        // 设置窗口大小
        public override Vector2 InitialSize => new Vector2(750f, 650f);

        public DialogTextDebug()
        {
            this.forcePause = false;            // 不暂停游戏
            this.absorbInputAroundWindow = false; // 允许点击外部
            this.closeOnClickedOutside = false;  // 点击外部不关闭
            this.doCloseButton = true;
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = true;
        }

        private bool IsChinese() => LanguageDatabase.activeLanguage.folderName.Contains("Chinese") || 
                                    LanguageDatabase.activeLanguage.info.friendlyNameNative.Contains("中文");

        public override void DoWindowContents(Rect inRect)
        {
            bool isZh = IsChinese();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), isZh ? "文本渲染实时调试" : "Text Rendering Debugger");

            // 计算各部分高度
            float panelHeight = (inRect.height - 180f) / 2f;

            // --- 1. 上半部分：手动输入栏 ---
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40f, inRect.width, 25f), isZh ? "输入原始文本 (AI返回格式):" : "Input Raw Text (AI format):");
            Rect inputRect = new Rect(0f, 65f, inRect.width, panelHeight);
            
            // 手动实现输入框滚动视图（解决符号无法解析问题）
            float inputContentHeight = Text.CalcHeight(inputBuffer, inputRect.width - 25f);
            Rect inputViewRect = new Rect(0f, 0f, inputRect.width - 25f, Math.Max(panelHeight, inputContentHeight + 40f));
            
            Widgets.BeginScrollView(inputRect, ref scrollPosInput, inputViewRect);
            string newIn = Widgets.TextArea(inputViewRect, inputBuffer);
            Widgets.EndScrollView();

            // --- 2. 中间控制区：HistoryProcessing 开关 ---
            Rect toggleRect = new Rect(0f, 75f + panelHeight, inRect.width, 30f);
            bool prevStatus = DisplayOptimizationMod.Settings.EnableHistoryProcessing;
            
            Widgets.CheckboxLabeled(toggleRect, 
                isZh ? " (请保持开启)" : "(Please keep enabled)", 
                ref DisplayOptimizationMod.Settings.EnableHistoryProcessing);

            // --- 实时更新逻辑 ---
            // 如果输入内容变了，或者开关状态变了，立即刷新预览
            if (newIn != inputBuffer || prevStatus != DisplayOptimizationMod.Settings.EnableHistoryProcessing)
            {
                inputBuffer = newIn;
                if (DisplayOptimizationMod.Settings.EnableHistoryProcessing)
                {
                    // 模拟 ProcessBase 逻辑
                    outputResult = TextProcessor.ProcessForDisplay(inputBuffer, "●");
                }
                else
                {
                    // 关闭优化时，显示最原始的内容
                    outputResult = inputBuffer;
                }
            }

            // --- 3. 下半部分：渲染预览栏 ---
            Widgets.Label(new Rect(0, 110f + panelHeight, inRect.width, 25f), isZh ? "实时渲染效果 (游戏内视觉):" : "Real-time Rendering:");
            Rect outputBoxRect = new Rect(0f, 135f + panelHeight, inRect.width, panelHeight);
            
            // 绘制深色背景
            Widgets.DrawBoxSolid(outputBoxRect, new Color(0f, 0f, 0f, 0.3f));

            // 计算输出内容高度
            float outputHeight = Text.CalcHeight(outputResult, outputBoxRect.width - 25f);
            Rect outputViewRect = new Rect(0f, 0f, outputBoxRect.width - 25f, Math.Max(panelHeight, outputHeight + 40f));

            Widgets.BeginScrollView(outputBoxRect, ref scrollPosOutput, outputViewRect);
            // 关键：Label 原生支持 RichText 渲染
            Widgets.Label(outputViewRect, outputResult);
            Widgets.EndScrollView();

            // 4. 底部清空按钮
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 35f, 100f, 30f), isZh ? "清空" : "Clear"))
            {
                inputBuffer = "";
                outputResult = "";
            }
        }
    }
}