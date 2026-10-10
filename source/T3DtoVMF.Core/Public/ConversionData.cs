namespace T3DtoVMF.Core.Public
{
    public sealed class ConversionData
    {
        public Action<LogLevel, string>? LogCallback { get; init; }
        public Func<string, TextWriter>? OBJWriterCreator { get; init; }
        public ConversionOptions Options { get; init; } = new();
    }
}
