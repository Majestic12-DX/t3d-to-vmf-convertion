using T3DtoVMF.Core.Public;

static class Settings
{
    public static ConversionData Data { get; set; } = new();
    public static readonly int LIGHTMAP_SCALE_MAX = 32;
    public static readonly int LIGHTMAP_SCALE_MIN = 1;
}