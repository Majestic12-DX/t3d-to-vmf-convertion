readonly struct UnrealHSVColor
{
    // Default Unreal values
    public byte Hue { get; } = 0;
    public byte Saturation { get; } = 255;
    public byte Brightness { get; } = 64;

    // Struct properties set their default values only when constructor runs, and I only figured that out now..
    // = new() in LightActor.cs skips the one with 3 bytes, so empty one is needed
    public UnrealHSVColor() { }

    public UnrealHSVColor(byte hue, byte saturation, byte brightness)
    {
        Hue = hue;
        Saturation = saturation;
        Brightness = brightness;
    }

    public UnrealHSVColor WithHue(byte newHue) => new UnrealHSVColor(newHue, Saturation, Brightness);
    public UnrealHSVColor WithSaturation(byte newSaturation) => new UnrealHSVColor(Hue, newSaturation, Brightness);
    public UnrealHSVColor WithBrightness(byte newBrightness) => new UnrealHSVColor(Hue, Saturation, newBrightness);

    // Converting light.. HSV to RGB
    // Im no color expert

    // Some data from UnrealEd:
    // Hue (0 = Red, 45 = Yellow, 80 = Green, 135 = Some Oceanish Color, 170 = Blue, 180 = Something Purple, 225 = Pink and slightly red, 255 = Red) (0-255)
    // Saturation means how "white" is our light.. kind of impacts non-primary colors of the light, increasing them (0-255)
    // Brightness is not part of the color, it's a separate linear multiplier (LightBrightness / 255), so it goes to 4th _light value
    // This works more like a spectrum, rather than a combination of basic colors

    // Port of UT99 Engine.dll FGetHSV, the way Render.dll calls it for lights: FGetHSV(LightHue, LightSaturation, 255)
    public RGBColor ToRGB()
    {
        float redFraction, greenFraction, blueFraction;
        if (Hue < 86)
        {
            redFraction = (85 - Hue) / 85f;
            greenFraction = Hue / 85f;
            blueFraction = 0;
        }
        else if (Hue < 171)
        {
            redFraction = 0;
            greenFraction = (170 - Hue) / 85f;
            blueFraction = (Hue - 85) / 85f;
        }
        else
        {
            redFraction = (Hue - 170) / 85f;
            greenFraction = 0;
            blueFraction = (255 - Hue) / 84f;
        }

        // Saturation is inverted
        float colorBrightnessMul = Saturation / 255f;
        redFraction += colorBrightnessMul * (1 - redFraction);
        greenFraction += colorBrightnessMul * (1 - greenFraction);
        blueFraction += colorBrightnessMul * (1 - blueFraction);

        byte red = (byte)Math.Round(redFraction * 255);
        byte green = (byte)Math.Round(greenFraction * 255);
        byte blue = (byte)Math.Round(blueFraction * 255);

        Logger.Write(LogLevel.DEBUG, $"Converted Unreal HSV Light ({Hue} {Saturation} {Brightness}) -> RGB ({red} {green} {blue} {Brightness})");
        return new RGBColor(red, green, blue, Brightness);
    }
}
