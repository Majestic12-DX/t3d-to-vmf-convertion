namespace T3DtoVMF.Cli.Utils
{
    sealed class CliOptions
    {
        public bool NoOBJ { get; set; }
        public bool NoLogFile { get; set; }
        public LogLevel LoggingLevel { get; set; } =
#if DEBUG
    LogLevel.DEBUG;
#else
    LogLevel.ERROR;
#endif
        public string? OutputFolder { get; set; }
        public List<string> T3ds { get; } = new();
    }
}
