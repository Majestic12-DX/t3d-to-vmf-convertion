namespace T3DtoVMF.Cli.Utils
{
    sealed class CLILogger : IDisposable
    {
        private const string _logFolder = "t3d_to_vmf_logs";
        private readonly StreamWriter? _file;
        private static readonly Dictionary<LogLevel, ConsoleColor> _colors = new()
    {
        { LogLevel.INFO, ConsoleColor.Cyan },
        { LogLevel.WARNING, ConsoleColor.Yellow },
        { LogLevel.ERROR, ConsoleColor.Red },
        { LogLevel.DEBUG, ConsoleColor.DarkGreen },
    };

        public CLILogger(string t3dFileName)
        {
            try
            {
                Directory.CreateDirectory(_logFolder);
                string logName = $"{Path.GetFileName(t3dFileName)} {Time.Now()}.log";
                _file = new StreamWriter(Path.Combine(_logFolder, logName)) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't create log file, logging to console only: {ex.Message}");
            }
        }

        public void Write(LogLevel level, string message)
        {
            string line = $"[{Time.Now()}] [{level}] {message}";
            _file?.WriteLine(line);

#if !DEBUG
        if (level == LogLevel.DEBUG) return;
#endif

            Console.ForegroundColor = _colors[level];
            Console.WriteLine(line);

            if (level == LogLevel.ERROR) Console.WriteLine();

            Console.ResetColor();
        }

        public void Dispose() => _file?.Dispose();
    }
}
