namespace T3DtoVMF.Core.Public
{
    public struct ConversionData
    {
        public Action<LogLevel, string>? LogCallback { get; init; }
        public Func<string, TextWriter>? OBJWriterCreator { get; init; }
    }
}
