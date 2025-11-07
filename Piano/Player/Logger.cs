using System;
using System.IO;
using System.Threading;

namespace Piano.Player
{
    /// <summary>
    /// 日志记录类，用于将调试信息写入文件
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = "Log.txt";
        private static readonly object LockObject = new object();

        /// <summary>
        /// 记录调试信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        /// <summary>
        /// 记录普通信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 记录警告信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public static void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public static void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息内容</param>
        private static void WriteLog(string level, string message)
        {
            try
            {
                // 使用锁确保线程安全
                lock (LockObject)
                {
                    // 获取当前时间戳
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    
                    // 构造日志行
                    string logLine = $"[{timestamp}] [{level}] {message}";
                    
                    // 写入文件
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，输出到控制台
                Console.WriteLine($"[Logger Error] Failed to write log: {ex.Message}");
            }
        }
    }
}