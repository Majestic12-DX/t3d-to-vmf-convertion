namespace T3DtoVMF.Core.Public
{
    public sealed record ConversionResult
    {
        public TimeSpan T3DReadTime { get; init; }
        public TimeSpan VMFWriteTime { get; init; }
        public TimeSpan TotalTime => T3DReadTime + VMFWriteTime;
        public int BrushCount { get; init; }
        public int BrushSidesCount { get; init; }
        public int BadBrushCount { get; init; }
        public int BadBrushSidesCount { get; init; }
        public int UniqueVerticesCount { get; init; }
    }
}
