public readonly struct RGBColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }
 
    // Default arg values are default source values
    public RGBColor(byte r = 255, byte g = 255, byte b = 255, byte a = 200)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public override string ToString() => $"{R} {G} {B} {A}";
}
