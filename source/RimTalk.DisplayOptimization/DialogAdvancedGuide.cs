using UnityEngine;
using Verse;

namespace RimTalk.DisplayOptimization
{
    public class DialogAdvancedGuide : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        
        // 修改点：不再硬编码，而是通过 .Translate() 从 XML 获取
        // 如果 XML 里找不到这个 Key，它会默认返回 Key 的名字
        private string contentText => "RimTalk.DisplayOptimization.AdvancedGuide".Translate();

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public DialogAdvancedGuide()
        {
            this.forcePause = false;
            this.absorbInputAroundWindow = false;
            this.closeOnClickedOutside = false;
            this.doCloseButton = true;
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), IsChinese() ? "高级模式适配说明" : "Advanced Guide");
            
            Text.Font = GameFont.Small;
            Rect outRect = new Rect(0f, 40f, inRect.width, inRect.height - 110f);
            
            // 动态计算高度
            float textHeight = Text.CalcHeight(contentText, outRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, textHeight + 20f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            // 使用获取到的翻译内容
            Widgets.TextArea(viewRect, contentText, true);

            Widgets.EndScrollView();

            // 底部提示
            Rect hintRect = new Rect(0f, inRect.height - 65f, inRect.width, 25f);
            GUI.color = Color.gray;
            string hint = IsChinese() ? "提示：选中上方文本后按 Ctrl+C 即可复制内容。" : "Tip: Select text above and press Ctrl+C to copy.";
            Widgets.Label(hintRect, hint);
            GUI.color = Color.white;
        }

        private bool IsChinese()
        {
            return LanguageDatabase.activeLanguage.folderName.Contains("Chinese") || 
                   LanguageDatabase.activeLanguage.info.friendlyNameNative.Contains("中文");
        }
    }
}