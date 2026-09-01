# RimTalk 对话显示拓展 · RimTalk.DisplayOptimization

增强 [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752) 对话显示效果的 RimWorld 模组。
Enhances [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752) dialogue display in RimWorld.

## 功能 / Features

- 对话内容涂色 / Dialogue coloring
- 内容转换（配对符号、正则替换）/ Content conversion (paired symbols, regex replacement)
- 对话速度调节 / Adjustable dialogue speed
- 一键暂停 / 一键忽略对话 / One-key pause & skip
- 防止对话被忽略 / Prevent dialogue from being skipped
- 智能暂停（跟随游戏暂停、窗口避让等）/ Smart pause (follows game pause, menu avoidance)
- 心声（Inner Voice）染色、名字高亮、重点标注、进度符号 ○/● / Inner-voice coloring, name highlighting, emphasis, progress symbols
- 自定义染色规则 + 文本调试器 / Custom coloring rules + text debugger

## 依赖 / Dependencies

- [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752)（`cj.rimtalk`，必需 / required）

## 安装 / Installation

Steam 创意工坊订阅，或将本仓库的 `About/`、`Assemblies/`、`Defs/`、`Languages/` 目录放入 RimWorld 的 `Mods/RimTalk.DisplayOptimization/` 目录。
Subscribe via Steam Workshop, or copy the `About/`, `Assemblies/`, `Defs/`, `Languages/` folders into `Mods/RimTalk.DisplayOptimization/` under RimWorld.

## 编译 / Building

1. 复制 `source/Directory.Build.props.example` → `source/Directory.Build.props`
   Copy `source/Directory.Build.props.example` to `source/Directory.Build.props`.
2. 把文件里的 3 个路径改成你自己电脑上的实际路径：
   Set the 3 paths in that file to your own install locations:
   - `RimWorldDir`：RimWorld 游戏安装目录 / RimWorld game folder
   - `RimTalkDir`：RimTalk 模组目录（创意工坊 `3551203752`）/ RimTalk mod folder (Workshop `3551203752`)
   - `HarmonyDir`：Harmony 模组目录（创意工坊 `2009463077`）/ Harmony mod folder (Workshop `2009463077`)
3. 用 Visual Studio / Rider 打开 `source/RimTalk.DisplayOptimization.sln`，或命令行编译：
   Open `source/RimTalk.DisplayOptimization.sln` in Visual Studio / Rider, or build from the command line:
   ```
   msbuild source/RimTalk.DisplayOptimization.sln /p:Configuration=Release
   ```
   编译产物在 `source/RimTalk.DisplayOptimization/bin/Release/`。
   The output goes to `source/RimTalk.DisplayOptimization/bin/Release/`.

## 作者 / Author

OCEAN · 打赏支持见 [打赏喵](./打赏喵)
