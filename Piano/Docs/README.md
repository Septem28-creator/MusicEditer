# AutoPiano

AutoPiano 是一个基于 C#/.NET 9.0 的音乐脚本解析与播放工具，能够将文本格式的音乐脚本转换为实际的音频输出。该工具支持丰富的音乐符号表示法，包括音符、和弦、休止符、连音线、颤音等多种音乐表现形式。

## 功能特性

- **音乐脚本解析**：解析自定义格式的音乐脚本文件（.music）
- **音频播放**：使用 NAudio 库实现音乐播放
- **交互控制**：支持播放、暂停、停止等交互控制
- **可配置参数**：支持多种命令行选项进行个性化配置
- **多声部支持**：支持多声部音乐的解析和播放
- **丰富的音乐符号**：支持多种音符、和弦、休止符、调号、节拍速度等音乐元素

## 系统要求

- .NET 9.0 SDK 或更高版本
- Windows 操作系统（由于使用 NAudio 进行音频播放）

## 安装与构建

1. 确保已安装 .NET 9.0 SDK
2. 克隆或下载项目到本地
3. 在项目根目录执行以下命令：

```bash
dotnet restore
dotnet build
```

## 使用方法

### 基本使用

```bash
dotnet run --project Piano/Piano.csproj <音乐脚本文件路径>
```

### 命令行选项

- `-h`, `--help`：显示帮助信息
- `-v`, `--version`：显示版本信息
- `-d`, `--debug`：启用调试输出模式
- `-i`, `--interactive`：启用交互模式（支持播放控制）[默认启用]
- `-n`, `--no-interactive`：禁用交互模式

### 示例

```bash
# 播放音乐脚本文件
dotnet run --project Piano/Piano.csproj ./Docs/test.music

# 以调试模式播放
dotnet run --project Piano/Piano.csproj -d ./Docs/test.music

# 禁用交互模式播放
dotnet run --project Piano/Piano.csproj -n ./Docs/test.music
```

## 音乐脚本语法

AutoPiano 支持丰富的音乐脚本语法，详细说明如下：

### 音名表示

- 使用大写字母表示基本音级：C, D, E, F, G, A, B
- 升号：#（如 C#）
- 降号：b（如 Db）
- 八度：采用科学音高记号法，中央C为C4，如 C4, D5

### 时值表示

- 全音符：1
- 二分音符：1/2
- 四分音符：1/4
- 八分音符：1/8
- 十六分音符：1/16
- 三十二分音符：1/32
- 附点音符：在时值后加点(.)，如 1/4. 表示3/8拍

### 特殊标记

- 休止符：R(时值)，如 R(1/4)
- 连音线：用 - 连接两个相同音高的音符
- 颤音：音符后加 ~，如 C4(1/4)~
- 倚音：音符前加 \，如 \G4 G4(1/2)
- 滑音：音符前加 \\，如 \\C4 G4(1/2)
- 和弦：用大括号包围多个音符，如 {C4,E4,G4}(1/4)

### 全局设置

- 节拍速度：[BPM=数值]，如 [BPM=120]（默认120）
- 调号：[KEY=调名]，如 [KEY=C] 或 [KEY=Am]

### 示例脚本

```
[BPM=120]
[KEY=C]
V0
C4(1/4) D4(1/4) E4(1/4) F4(1/4) G4(1/4) A4(1/4) B4(1/4) C5(1/4) |
R(1/4) C5(1/4) B4(1/4) A4(1/4) G4(1/4) F4(1/4) E4(1/4) D4(1/4) |
C4(1/2) G4(1/2) C5(1/2) G4(1/2) |
```

## 项目结构

```
Piano/
├── Parser/          # 词法分析和语法分析器
│   ├── AstNode.cs   # 抽象语法树节点
│   ├── Lexer.cs     # 词法分析器
│   ├── Parser.cs    # 语法分析器
│   └── Token.cs     # 词法单元定义
├── PianoConsole/    # 控制台交互模块
│   ├── DebugOutputManager.cs  # 调试输出管理
│   ├── IPlaybackController.cs # 播放控制器接口
│   └── PlaybackConsole.cs     # 播放控制台
├── Player/          # 音频播放器模块
│   ├── AudioPlayer.cs   # 音频播放器
│   ├── Logger.cs        # 日志记录器
│   └── PianoPlayer.cs   # 钢琴播放器主类
├── Docs/            # 文档和示例
│   ├── GrammarDesign.md  # 语法设计文档
│   └── test.music        # 示例音乐文件
└── Program.cs       # 程序入口点
```

## 开发

该项目使用 .NET 9.0 开发，主要依赖 NAudio 库进行音频播放。代码结构清晰，分为解析器、播放器和控制台交互三个主要模块。

## 许可证

此项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

## 贡献

欢迎提交 Issue 和 Pull Request 来改进项目！