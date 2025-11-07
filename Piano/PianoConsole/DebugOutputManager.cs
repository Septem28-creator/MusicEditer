using System;
using Piano.Player;

namespace Piano.PianoConsole
{
    /// <summary>
    /// 调试输出管理器
    /// </summary>
    public static class DebugOutputManager
    {
        /// <summary>
        /// 是否启用调试输出
        /// </summary> 666
        public static bool IsDebugEnabled { get; set; } = false;

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="message">调试信息</param>
        public static void DebugWriteLine(string message)
        {
            if (IsDebugEnabled == true)
            {
                Console.WriteLine($"[DEBUG] {message}");
                Logger.Debug(message);
            }
        }

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">信息</param>
        public static void InfoWriteLine(string message)
        {
            Console.WriteLine(message);
            Logger.Info(message);
        }
    }
}