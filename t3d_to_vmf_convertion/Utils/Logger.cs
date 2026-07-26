using System.Diagnostics;

public enum LogLevel
{
    INFO,
    WARNING,
    ERROR,
    DEBUG
}

public static class Logger
{
    private const string _logFolder = "t3d_to_vmf_logs";
    private static StreamWriter? _writer;
    private static Dictionary<LogLevel, ConsoleColor> _logColors = new() { 
        { LogLevel.INFO, ConsoleColor.Cyan },
        { LogLevel.WARNING, ConsoleColor.Yellow },
        { LogLevel.ERROR, ConsoleColor.Red },
        { LogLevel.DEBUG, ConsoleColor.DarkGreen },
    };

    public static bool SetLogFileName(string logFileName)
    {
        // Ensure we get the filename, with extension, maybe I'll make this converter two-way someday
        logFileName = $"{Path.GetFileName(logFileName)} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.log";
        string logFilePath = Path.Combine(_logFolder, logFileName);

        try
        {
            Directory.CreateDirectory(_logFolder);

            _writer = new StreamWriter(logFilePath, true);
            _writer.AutoFlush = true;
        } catch (Exception ex) {
            Console.WriteLine($"Failed to initialize logger: {ex.Message}");
            return false;
        }

        return true;
    }

    public static void Write(LogLevel logType, string logMessage)
    {
        if (_writer == null) return;

        DateTime dateTime = DateTime.Now;
        string logLine = $"[{dateTime:yyyy-MM-dd HH:mm:ss}] [{logType}] {logMessage}";
        
        _writer.WriteLine(logLine);

#if !DEBUG
        if (logType == LogLevel.DEBUG) return;
#endif

        Console.ForegroundColor = _logColors[logType];

        Console.WriteLine(logLine);

        if (logType == LogLevel.ERROR)
            Console.WriteLine();

        Console.ResetColor();
    }

    public static void Close()
    {
        if (_writer == null) return;

        _writer.Dispose();
        _writer = null;
    }
}