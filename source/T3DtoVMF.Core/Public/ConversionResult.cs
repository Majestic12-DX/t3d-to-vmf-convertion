namespace T3DtoVMF.Core.Public
{
    public struct ConversionResult
    {
        public TimeSpan T3DReadTime { get; internal set; } = TimeSpan.Zero;
        public TimeSpan VMFWriteTime { get; internal set; } = TimeSpan.Zero;
        public TimeSpan TotalTime => T3DReadTime + VMFWriteTime;
        public int BrushCount { get; internal set; } = 0;
        public int BrushSidesCount { get; internal set; } = 0;
        public int BadBrushCount { get; internal set; } = 0;
        public int BadBrushSidesCount { get; internal set; } = 0;
        public int UniqueVerticesCount { get; internal set; } = 0;
        public ConversionResult() { }
    }
}
