using System;
using System.IO;
using System.Text;

public static class Logger
{
    private static readonly string LogDirectory = "logs";
    private static readonly string LogFileName = "app.log";
    private static readonly int MaxLogFileSize = 5 * 1024 * 1024; // 5MB
    private static readonly object LockObject = new object();
    private static string CurrentLogFile;
    private static LogLevel CurrentLogLevel = LogLevel.Info;  // デフォルトはInfo

    static Logger()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            CurrentLogFile = Path.Combine(LogDirectory, LogFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ログディレクトリの作成に失敗しました: " + ex.Message);
        }
    }

    public static void SetLogLevel(LogLevel level)
    {
        CurrentLogLevel = level;
    }

    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        // 現在のログレベルより低いレベルのログは出力しない
        if (level < CurrentLogLevel)
        {
            return;
        }

        try
        {
            lock (LockObject)
            {
                CheckLogFileSize();
                string logMessage = FormatLogMessage(message, level);
                File.AppendAllText(CurrentLogFile, logMessage, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ログの書き込みに失敗しました: " + ex.Message);
        }
    }

    private static void CheckLogFileSize()
    {
        if (File.Exists(CurrentLogFile))
        {
            var fileInfo = new FileInfo(CurrentLogFile);
            if (fileInfo.Length >= MaxLogFileSize)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string newLogFile = Path.Combine(LogDirectory, string.Format("app_{0}.log", timestamp));
                File.Move(CurrentLogFile, newLogFile);
                CurrentLogFile = Path.Combine(LogDirectory, LogFileName);
            }
        }
    }

    private static string FormatLogMessage(string message, LogLevel level)
    {
        return string.Format("[{0}] [{1}] {2}{3}", 
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            level,
            message,
            Environment.NewLine);
    }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
} 