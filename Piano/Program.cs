using System;
using System.IO;
using System.Threading.Tasks;
using Piano.Parser;
using Piano.PianoConsole;
using Piano.Player;

namespace Piano
{
    /// <summary>
    /// AutoPiano - 自动钢琴演奏程序
    /// 支持音乐脚本解析与播放、二进制文件导入导出功能
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var app = new PianoApplication();
            await app.RunAsync(args);
        }
    }

    /// <summary>
    /// 钢琴应用程序主类
    /// </summary>
    public class PianoApplication
    {
        private const string AppName = "AutoPiano";
        private const string Version = "1.0.0";
        private PianoPlayer _player;

        /// <summary>
        /// 运行应用程序
        /// </summary>
        /// <param name="args">命令行参数</param>
        public async Task RunAsync(string[] args)
        {
            try
            {
                var options = ParseCommandLineArgs(args);
                
                // 设置调试模式
                DebugOutputManager.IsDebugEnabled = options.DebugMode;
                
                if (options.ShowHelp)
                {
                    ShowHelp();
                    return;
                }
                
                if (options.ShowVersion)
                {
                    ShowVersion();
                    return;
                }
                if (string.IsNullOrEmpty(options.InputFilePath))
                {
                    DebugOutputManager.InfoWriteLine("错误: 未指定输入文件路径。");
                    ShowHelp();
                    return;
                }
                
                if (!File.Exists(options.InputFilePath))
                {
                    DebugOutputManager.InfoWriteLine($"错误: 输入文件 '{options.InputFilePath}' 不存在。");
                    return;
                }

                ScoreNode score = null;
                
                
                DebugOutputManager.InfoWriteLine($"从文件读取音乐脚本: {options.InputFilePath}");
                var script = await File.ReadAllTextAsync(options.InputFilePath);
                
                // 词法分析
                DebugOutputManager.InfoWriteLine("开始词法分析...");
                var lexer = new Lexer(script);
                var tokens = lexer.Tokenize();
                
                DebugOutputManager.InfoWriteLine($"词法分析完成，共识别 {tokens.Count} 个词法单元");
                
                // 语法分析
                DebugOutputManager.InfoWriteLine("开始语法分析...");
                var parser = new Piano.Parser.Parser(tokens);
                score = parser.Parse();
                DebugOutputManager.InfoWriteLine("语法分析完成");

                // 播放音乐（如果未指定仅导出二进制文件）
                if (!options.BinaryExportOnly)
                {
                    DebugOutputManager.InfoWriteLine("开始播放音乐...");
                    _player = new PianoPlayer();
                    
                    // 默认启动交互模式
                    // 设置播放器实例
                    PlaybackConsole.SetPlayer(_player);
                    
                    // 在单独的线程中播放音乐，以便可以接收用户输入
                    var playTask = Task.Run(() => _player.PlayScore(score));
                    
                    // 启动播放控制台
                    await PlaybackConsole.StartListeningAsync();
                    
                    // 等待播放完成
                    await playTask;
                    
                    DebugOutputManager.InfoWriteLine("音乐播放完成");
                }
            }
            catch (Exception ex)
            {
                DebugOutputManager.InfoWriteLine($"发生错误: {ex.Message}");
                DebugOutputManager.DebugWriteLine($"详细信息: {ex.StackTrace}");
            }
            finally
            {
                _player?.Dispose();
                
                if (Environment.UserInteractive)
                {
                    DebugOutputManager.InfoWriteLine("\n程序执行完毕，按任意键退出...");
                    try
                    {
                        Console.ReadKey();
                    }
                    catch (InvalidOperationException)
                    {
                        // 在非交互环境中忽略此错误
                    }
                }
            }
        }

        /// <summary>
        /// 解析命令行参数
        /// </summary>
        /// <param name="args">命令行参数数组</param>
        /// <returns>解析后的选项</returns>
        private CommandLineOptions ParseCommandLineArgs(string[] args)
        {
            var options = new CommandLineOptions
            {
                // 默认启用交互模式
                InteractiveMode = true
            };

            if (args.Length == 0)
            {
                options.ShowHelp = true;
                return options;
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    case "-v":
                    case "--version":
                        options.ShowVersion = true;
                        break;
                    case "-d":
                    case "--debug":
                        options.DebugMode = true;
                        break;
                    case "-i":
                    case "--interactive":
                        options.InteractiveMode = true;
                        break;
                    case "-n":
                    case "--no-interactive":
                        options.InteractiveMode = false;
                        break;
                    default:
                        // 如果不是选项，则视为输入文件路径
                        if (args[i].StartsWith("-"))
                        {
                            DebugOutputManager.InfoWriteLine($"未知选项: {args[i]}");
                            options.ShowHelp = true;
                        }
                        else
                        {
                            options.InputFilePath = args[i];
                        }
                        break;
                }
            }

            return options;
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            DebugOutputManager.InfoWriteLine($"{AppName} - 音乐脚本播放与二进制文件处理工具");
            DebugOutputManager.InfoWriteLine("");
            DebugOutputManager.InfoWriteLine("用法:");
            DebugOutputManager.InfoWriteLine("  Piano <文件路径>                              - 播放音乐脚本文件");
            DebugOutputManager.InfoWriteLine("  Piano --help                                  - 显示帮助信息");
            DebugOutputManager.InfoWriteLine("  Piano --version                               - 显示版本信息");
            DebugOutputManager.InfoWriteLine("  Piano --debug                                 - 启用调试输出模式");
            DebugOutputManager.InfoWriteLine("  Piano --interactive                           - 启用交互模式（支持播放控制）[默认启用]");
            DebugOutputManager.InfoWriteLine("  Piano --no-interactive                        - 禁用交互模式");
        }

        /// <summary>
        /// 显示版本信息
        /// </summary>
        private void ShowVersion()
        {
            DebugOutputManager.InfoWriteLine($"{AppName} 版本 {Version}");
        }
    }

    /// <summary>
    /// 命令行选项类
    /// </summary>
    public class CommandLineOptions
    {
        public bool ShowHelp { get; set; } = false;
        public bool ShowVersion { get; set; } = false;
        public string? InputFilePath { get; set; }
        public bool BinaryExportOnly { get; set; } = false;
        public bool DebugMode { get; set; } = false;
        public bool InteractiveMode { get; set; } = false;
    }
}