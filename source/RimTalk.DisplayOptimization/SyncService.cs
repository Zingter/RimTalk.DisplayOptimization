using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Source.Data;
using Verse;
using RimWorld;

namespace RimTalk.DisplayOptimization
{
    public static class SyncService
    {
        // 1. 内部辅助：仅负责生成那段读心者描述文本 (逻辑保持原样)
        private static string GenerateReaderHeaderText(List<string> names, bool plural)
        {
            if (names == null || names.Count == 0) return "";
            var s = DisplayOptimizationMod.Settings;
            string namesStr = string.Join(" and ", names.Select(n => $"**{n}**").ToArray());
            string verb = plural ? "are" : "is";
            return $"\n{namesStr} {verb} the Soul-Reader. They can perceive everything inside {s.SymThoughtL} {s.SymThoughtR} as clear as spoken words.\n";
        }

        // 2. 独立条目层：现在包含了【名单 + 基础规则 + 进阶规则】
        // 修改：增加参数 readers
        public static string GetSoulSystemEntry(bool showAdvanced, List<string> readers)
        {
            var s = DisplayOptimizationMod.Settings;
            string text = "";

            // --- A. 首先注入读心者名单 (原本在 Prompt) ---
            if (readers != null && readers.Count > 0)
            {
                text += GenerateReaderHeaderText(readers, readers.Count > 1);
            }

            // --- B. 基础规则 (始终发送) ---
            text += $"\n[Cognitive Wall]: Pawns cannot perceive Inner Monologue in {s.SymThoughtL}{s.SymThoughtR}. Their actions, dialogue, and attitudes must be based solely on actions and spoken dialogue, and they must accept these surface interactions as truth.\n";
            
            // --- C. 进阶部分 (条件触发) ---
            if (showAdvanced)
            {
                text += $"[State Separation]: Pawns must maintain \"State Separation\" —even if talking within {s.SymThoughtL} {s.SymThoughtR} to the Soul-Reader, their public persona and actions towards other pawns must remain perfectly in-character. If a Soul-Reader blurs a secret publicly, pawns must act shocked, confused, or defensive.\n";
            }
            
            text += $"\n## The User watches from shadows. You MUST show pawns' Inner Monologue in {s.SymThoughtL}{s.SymThoughtR} in dialogue for him. ##\n";
            return text;
        }

        // 3. JSON 格式层：格式指令 (用于 json.format 或 {{text_rule}})
        public static string GetJsonFormattingRules(bool showName, bool showEmp)
        {
            var s = DisplayOptimizationMod.Settings;
            string rules = $"\n\"text\":\n";
            if (showName && s.EnableHighlighting) rules += "- Wrap every mention of a person (name, nickname or title) in single Pipe. like |name|\n";
            
            if (s.EnableActionColor) rules += $"- Wrap pawns' Action in {s.SymActionL} {s.SymActionR}.\n";
            
            if (s.EnableDirectorMode) rules += $"- Wrap pawns' Inner Monologue in {s.SymThoughtL} {s.SymThoughtR}\n";
            
            if (showEmp && s.EnableEmphasis) rules += "- Wrap core keywords and emotional emphasis in double asterisks. like **hate** ,but each turn must not exceed three pairs asterisks\n";
            
            return rules;
        }
        
    }
}