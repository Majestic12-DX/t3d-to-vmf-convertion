public sealed class VMFSettings
{
    public static int LIGHTMAP_SCALE_MAX = 1;
    public static int LIGHTMAP_SCALE_MIN = 32;

    private int _lightmapScale = 16;

    public int LightmapScale
    {
        get => _lightmapScale;
        set => Math.Clamp(value, LIGHTMAP_SCALE_MAX, LIGHTMAP_SCALE_MIN);
    }
}