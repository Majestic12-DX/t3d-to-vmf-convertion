public enum LogLevel
{
    INFO,
    WARNING,
    ERROR,
    DEBUG
}

static class Logger
{
    public static void Write(LogLevel level, string message) => Settings.Data.LogCallback?.Invoke(level, message);
}