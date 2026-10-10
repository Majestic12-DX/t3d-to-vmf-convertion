public enum LogLevel
{
    NONE, // For custom loggers to ignore messages
    INFO,
    WARNING,
    ERROR,
    DEBUG
}

static class Logger
{
    public static void Write(LogLevel level, string message) => Settings.Data.LogCallback?.Invoke(level, message);
}