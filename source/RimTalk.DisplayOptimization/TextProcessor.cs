using System.Linq;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;

namespace RimTalk.DisplayOptimization
{
    
   
    
    public static class TextProcessor
    {
        private static readonly Regex NewlineRegex = new Regex(@"[\r\n\t\v\f]+", RegexOptions.Compiled);
        private static readonly Regex SymbolCleaner = new Regex(@"^[○●]|[○●]$", RegexOptions.Compiled);
        private static readonly Regex RichTextStripper = new Regex(@"<[^>]*>", RegexOptions.Compiled);

        public static string StripCircles(string input) {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("●", "").Replace("○", "").Trim();
        }
        
        public static string ProcessBase(string text)
        {
            
            if (string.IsNullOrEmpty(text)) return text;
            var s = DisplayOptimizationMod.Settings;

            string result = text.Replace("●", "").Replace("○", "").Trim(' ', '\u00A0');
            
            result = NewlineRegex.Replace(result, " ").Trim();
            if (s.EnableNoWrap) result = result.Replace(" ", "\u00A0");
            
            
                //重点标注并清洗
            if (s.EnableEmphasis)
            {
                result = InjectColorTagsExempt(result, "**", "**", "", "", s.ColorEmphasis, false, s.EnableEmphasisBolding);
            }
            
            //名字高亮：染色并直接删掉
            if (s.EnableHighlighting)
            {
                result = InjectColorTagsExempt(result, "|", "|", "", "", s.ColorColonist, false, s.EnableBolding);
                /*
                 if (Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null)
                 {
                     // 性能优化：可以考虑只获取人类 Pawn 或当前对话者
                     foreach (var pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
                     {
                         string name = pawn.LabelShort;
                         // 增加长度过滤和已着色检查
                         if (name.Length < 2 || result.Contains($">{name}<") || result.Contains($">{name}</b>")) continue;

                         // 使用正则：\b 确保是独立单词，(?<!<[^>]*) 确保不在标签内
                         string pattern = @"(?<!<[^>]*)\b" + Regex.Escape(name) + @"\b(?![^>]*>)";
                         string replacement = s.EnableBolding ? $"<b><color={col}>{name}</color></b>" : $"<color={col}>{name}</color>";
                         result = Regex.Replace(result, pattern, replacement);
                     }
                 }
                 */
            }
                
            //动作染色
            if (s.EnableActionColor) result = InjectColorTagsExempt(result, s.SymActionL, s.SymActionR, s.SymActionL, s.SymActionR, s.ColorAction, s.EnableActionItalic, false);
                
            //心声染色
            if (s.EnableThoughtColor)
            {
                // result = result.Replace("【", $"〖").Replace("】", $"〗");
                result = InjectColorTagsExempt(result, s.SymThoughtL, s.SymThoughtR, s.SymThoughtL, s.SymThoughtR, s.ColorThought, s.EnableThoughtItalic, false);
                // result = result.Replace($"{s.SymThoughtL}Inner Monologue:", $"{s.SymThoughtL}").Replace($"{s.SymThoughtL}Inner Monologue", $"{s.SymThoughtL}");
            }

            if (s.CustomRules != null)
            {
                foreach (var rule in s.CustomRules)
                {
                    // 1. 判定装饰标签 (开启染色且有颜色码才加标签)
                    string pre = ""; string post = "";
                    if (rule.EnableColor && !string.IsNullOrEmpty(rule.ColorHex)) { pre += $"<color={rule.ColorHex}>"; post = "</color>" + post; }
                    if (rule.EnableBold) { pre += "<b>"; post = "</b>" + post; }
                    if (rule.EnableItalic) { pre += "<i>"; post = "</i>" + post; }

                    if (rule.Type == RuleType.Paired)
                    {
                        if (string.IsNullOrEmpty(rule.InputL)) continue;
                        // 颜色判定逻辑已经在内部处理，直接传 hex 即可
                        string finalCol = rule.EnableColor ? rule.ColorHex : null;
                        result = InjectColorTagsExempt(result, rule.InputL, rule.InputR, rule.OutputL, rule.OutputR, 
                            finalCol, rule.EnableItalic, rule.EnableBold);
                    }
                    else if (rule.Type == RuleType.Simple)
                    {
                        if (string.IsNullOrEmpty(rule.TargetText)) continue;

                        // 2. 支持 \n 识别转换
                        string target = rule.TargetText.Replace("\\n", "\n");
                        string replacement = pre + (rule.ReplacementText ?? "").Replace("\\n", "\n") + post;

                        // 3. 极简替换逻辑 (正则为可选开关，节省性能)
                        if (rule.UseRegex)
                        {
                            try { result = System.Text.RegularExpressions.Regex.Replace(result, target, replacement); }
                            catch { /* 正则语法错则跳过 */ }
                        }
                        else
                        {
                            // 只有不使用正则时，才跑原生 Replace，性能最高
                            result = result.Replace(target, replacement);
                        }
                    }
                }
            }
            // 【解决遮挡】：在末尾强行补一个带空格的换行，诱导 RimTalk 分配更多高度
            return result + "\u00A0"; 
        }
        
        
        // 【标签豁免算法】：扫描时完全无视任何 < > 标签内容
        private static string InjectColorTagsExempt(string input, string open, string close, string displayOpen, string displayClose, string hexColor, bool italic, bool bold)
        {
            if (string.IsNullOrEmpty(input) || !input.Contains(open)) return input;

            StringBuilder sb = new StringBuilder();
            int lastPos = 0;
            
            
            string startTag = "";
            string endTag = "";
            if (!string.IsNullOrEmpty(hexColor)) 
            { 
                startTag += $"<color={hexColor}>"; 
                endTag = "</color>" + endTag; 
            }
            if (italic) 
            { 
                startTag += "<i>"; 
                endTag = "</i>" + endTag; 
            }
            if (bold) 
            { 
                startTag += "<b>"; 
                endTag = "</b>" + endTag; 
            }

            for (int i = 0; i < input.Length; i++)
            {
                // 1. 标签豁免逻辑 (保持不变)
                if (input[i] == '<')
                {
                    int tagEnd = input.IndexOf('>', i);
                    if (tagEnd != -1)
                    {
                        sb.Append(input.Substring(lastPos, i - lastPos));
                        sb.Append(input.Substring(i, tagEnd - i + 1));
                        lastPos = tagEnd + 1;
                        i = tagEnd;
                        continue;
                    }
                }

                // 2. 核心改进：当发现左括号时
                if (IsAt(input, i, open))
                {
                    // --- 尝试向后寻找匹配的右括号 (考虑嵌套) ---
                    int matchIdx = FindMatchingClosing(input, i, open, close);

                    if (matchIdx != -1) // 找到了匹配的结尾
                    {
                        // 先把括号前面的文字存了
                        sb.Append(input.Substring(lastPos, i - lastPos));

                        // 提取括号内部内容
                        string content = input.Substring(i + open.Length, matchIdx - (i + open.Length));
                        
                        // === 【新增处】：处理内部嵌套的同类符号替换
                        if (open != displayOpen && !string.IsNullOrEmpty(open)) content = content.Replace(open, displayOpen);
                        if (close != displayClose && !string.IsNullOrEmpty(close)) content = content.Replace(close, displayClose);
                                
                        // 执行去句号逻辑
                        content = content.TrimEnd();
                        if (content.EndsWith("。") || content.EndsWith("."))
                        {
                            content = content.Substring(0, content.Length - 1).TrimEnd();
                        }

                        // 注入带有颜色的内容
                        sb.Append(startTag + displayOpen + content + displayClose + endTag);

                        // 更新指针位置到匹配的右括号之后
                        i = matchIdx + close.Length - 1;
                        lastPos = i + 1;
                    }
                    else
                    {
                        // 没找到匹配的右括号，这部分我们什么都不做，让它作为普通文字在最后补齐
                        // 指针正常向后走，寻找下一个可能的左括号
                    }
                }
            }

            // 3. 补齐末尾剩余文本
            if (lastPos < input.Length) sb.Append(input.Substring(lastPos));

            return sb.ToString();
        }

        private static bool IsAt(string input, int idx, string target)
        {
            if (idx + target.Length > input.Length) return false;
            for (int i = 0; i < target.Length; i++)
                if (input[idx + i] != target[i]) return false;
            return true;
        }
        
                // 专门用于寻找匹配的右括号，支持嵌套判定且无视 RichText 标签
        private static int FindMatchingClosing(string input, int startIdx, string open, string close)
        {
            if (open == close)
            {
                // 直接从起始位置之后找下一个 close 符号
                // 注意：要跳过 RichText 标签，防止匹配到 <color=...|...> 里的字符
                int searchStart = startIdx + open.Length;
                for (int i = searchStart; i < input.Length; i++)
                {
                    if (input[i] == '<')
                    {
                        int tagEnd = input.IndexOf('>', i);
                        if (tagEnd != -1) { i = tagEnd; continue; }
                    }
                    if (IsAt(input, i, close)) return i;
                }
                return -1;
            }
                    
            int depth = 0;
            for (int i = startIdx; i < input.Length; i++)
            {
                // 寻找时也要跳过已经存在的标签，防止 <color> 干扰
                if (input[i] == '<')
                {
                    int tagEnd = input.IndexOf('>', i);
                    if (tagEnd != -1) { i = tagEnd; continue; }
                }

                if (IsAt(input, i, open))
                {
                    depth++;
                }
                else if (IsAt(input, i, close))
                {
                    depth--;
                    if (depth == 0) return i; // 找到了最外层匹配的右括号
                }
            }
            return -1; // 没找到匹配
        }
        
        public static string ApplySymbol(string text, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return text;
            return DisplayOptimizationMod.Settings.CircleAtEnd ? text + symbol : symbol + text;
        }

        public static string StripSymbols(string text) => SymbolCleaner.Replace(text, "").Trim();
        public static string StripTagsOnly(string text) => RichTextStripper.Replace(text, "");
                // 关键：用于比对的“纯净指纹”
                // 【核心修正点】：StripEverything 不再删除 [] () 【】 
                // 这样在重绘历史记录时，ProcessBase 依然能找到这些“锚点”来重新上色
        public static string StripEverything(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string t = StripTagsOnly(text);
            return StripSymbols(t);
        }
        
        public static string ProcessForDisplay(string text, string symbol) => ApplySymbol(ProcessBase(text), symbol);
        
                // --- 【新增逻辑】：纠偏专用，同时支持头尾圆圈替换 ---
        public static string ReplaceCircle(string text, string newSymbol)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
                    // 剥离现有的开头和结尾圆圈
            string cleanContent = text.TrimStart('○', '●', ' ', '\u00A0').TrimEnd('○', '●', ' ', '\u00A0');
            
                    // 重新按设置应用新圆圈
            return ApplySymbol(cleanContent, newSymbol);
        }
        
        
    }
}