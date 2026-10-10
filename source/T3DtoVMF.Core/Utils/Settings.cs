using T3DtoVMF.Core.Public;

static class Settings
{
    public static ConversionData Data { get; set; }
    public static readonly int LIGHTMAP_SCALE_MAX = 32;
    public static readonly int LIGHTMAP_SCALE_MIN = 1;

    private static int _lightmapScale = 16;
    public static int LightmapScale
    {
        get => _lightmapScale;
        set => _lightmapScale = Math.Clamp(value, LIGHTMAP_SCALE_MIN, LIGHTMAP_SCALE_MAX);
    }
}