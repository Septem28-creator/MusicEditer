using System;
using System.Threading.Tasks;
using Piano.Player;

namespace Piano.PianoConsole
{
    /// <summary>
    /// 播放控制台类，用于处理用户输入并控制播放
    /// </summary>
    public static class PlaybackConsole
    {
        private static PianoPlayer _player;
        private static bool _isPlaying = false;

        /// <summary>
        /// 设置播放器实例
        /// </summary>
        /// <param name="player">播放器实例</param>
        public static void SetPlayer(PianoPlayer player)
        {
            _player = player;
        }

        /// <summary>
        /// 开始监听用户输入
        /// </summary>
        public static async Task StartListeningAsync()
        {
            _isPlaying = true;
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    AutoPiano 播放控制台                      ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  播放控制:                                                   ║");
            Console.WriteLine("║    p/P   - 暂停播放                                          ║");
            Console.WriteLine("║    r/R   - 继续播放                                          ║");
            Console.WriteLine("║    s/S   - 停止播放                                          ║");
            Console.WriteLine("║  音量控制:                                                   ║");
            Console.WriteLine("║    v/V + 数值 - 设置音量 (0.0-1.0)                           ║");
            Console.WriteLine("║  速度控制:                                                   ║");
            Console.WriteLine("║    sp/SP + 数值 - 设置速度 (BPM), 如: sp 120                 ║");
            Console.WriteLine("║  其他:                                                       ║");
            Console.WriteLine("║    h/H   - 显示帮助信息                                      ║");
            Console.WriteLine("║    q/Q   - 退出控制台                                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            while (_isPlaying && _player != null)
            {
                Console.Write("输入命令 > ");
                var key = await ReadLineAsync();
                if (string.IsNullOrEmpty(key)) continue;

                var parts = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                switch (command)
                {
                    case "p":
                        _player.Pause();
                        Console.WriteLine("播放已暂停。");
                        break;
                    case "r":
                        _player.Resume();
                        Console.WriteLine("播放已恢复。");
                        break;
                    case "s":
                        _player.Stop();
                        Console.WriteLine("播放已停止。");
                        _isPlaying = false;
                        break;
                    case "v":
                        if (parts.Length > 1 && double.TryParse(parts[1], out double volume))
                        {
                            if (volume >= 0.0 && volume <= 1.0)
                            {
                                _player.SetVolume(volume);
                                Console.WriteLine($"音量已设置为 {volume:F2}。");
                            }
                            else
                            {
                                Console.WriteLine("错误: 音量必须在0.0到1.0之间。");
                            }
                        }
                        else
                        {
                            Console.WriteLine("错误: 请提供有效的音量值 (0.0-1.0)。");
                        }
                        break;
                    case "sp":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int bpm))
                        {
                            if (bpm > 0)
                            {
                                _player.SetSpeed(bpm);
                                Console.WriteLine($"速度已设置为 {bpm} BPM。");
                            }
                            else
                            {
                                Console.WriteLine("错误: BPM 必须大于0。");
                            }
                        }
                        else
                        {
                            Console.WriteLine("错误: 请提供有效的BPM值。");
                        }
                        break;
                    case "h":
                        ShowHelp();
                        break;
                    case "q":
                        _player.Stop();
                        Console.WriteLine("已退出控制台。");
                        _isPlaying = false;
                        break;
                    default:
                        Console.WriteLine($"未知命令: {command}。输入 'h' 查看帮助。");
                        break;
                }
                
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                          帮助信息                            ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  播放控制:                                                   ║");
            Console.WriteLine("║    p/P   - 暂停播放                                          ║");
            Console.WriteLine("║    r/R   - 继续播放                                          ║");
            Console.WriteLine("║    s/S   - 停止播放                                          ║");
            Console.WriteLine("║  音量控制:                                                   ║");
            Console.WriteLine("║    v/V + 数值 - 设置音量 (0.0-1.0), 如: v 0.8                ║");
            Console.WriteLine("║  速度控制:                                                   ║");
            Console.WriteLine("║    sp/SP + 数值 - 设置速度 (BPM), 如: sp 120                 ║");
            Console.WriteLine("║  其他:                                                       ║");
            Console.WriteLine("║    h/H   - 显示帮助信息                                      ║");
            Console.WriteLine("║    q/Q   - 退出控制台                                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        }

        /// <summary>
        /// 异步读取用户输入
        /// </summary>
        /// <returns>用户输入的字符串</returns>
        private static Task<string> ReadLineAsync()
        {
            return Task.Run(() => Console.ReadLine());
        }
    }
}