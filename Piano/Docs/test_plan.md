# AutoPiano 测试计划

## 1. 测试目标
验证重新编写的 Program.cs 文件功能完整性和稳定性

## 2. 测试环境
- 操作系统: Windows 10/11
- .NET 版本: .NET 9.0
- 开发环境: Visual Studio 或 JetBrains Rider

## 3. 测试用例

### 3.1 命令行参数测试
| 测试项 | 命令 | 预期结果 |
|--------|------|----------|
| 显示帮助 | `Piano --help` 或 `Piano -h` | 显示帮助信息 |
| 显示版本 | `Piano --version` 或 `Piano -v` | 显示版本信息 |
| 基本播放 | `Piano test.music` | 播放音乐脚本文件 |
| MIDI导入 | `Piano --import-midi test.mid` | 从MIDI文件导入并播放 |
| 播放并导出MIDI | `Piano test.music --export-midi output.mid` | 播放并导出MIDI文件 |
| 仅导出MIDI | `Piano test.music --export-midi output.mid --midi-only` | 仅导出MIDI文件，不播放 |
| 无效参数 | `Piano --invalid` | 显示错误信息并显示帮助 |

### 3.2 功能测试
1. 音乐脚本解析和播放
2. MIDI文件导入和播放
3. MIDI文件导出
4. 错误处理（文件不存在、格式错误等）

### 3.3 边界条件测试
1. 空命令行参数
2. 不存在的文件路径
3. 损坏的音乐脚本文件
4. 损坏的MIDI文件