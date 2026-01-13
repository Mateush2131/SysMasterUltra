using System;
using System.IO;

namespace SysMasterUltra.Services
{
    public static class Logger
    {
        private static readonly object lockObject = new object();
        private static string logFilePath = "SysMasterLog.txt";

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            lock (lockObject)
            {
                try
                {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    Console.WriteLine(logMessage);
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
                catch { }
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            string errorMsg = $"ERROR: {message}";
            if (ex != null)
                errorMsg += $"\nException: {ex.Message}";

            Log(errorMsg, LogLevel.Error);
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}