public readonly struct UnrealHSVColor
{
    public byte Hue { get; }
    public byte Saturation { get; }
    public byte Brightness { get; }

    // Default UT Values
    public UnrealHSVColor(byte hue = 0, byte saturation = 255, byte brightness = 64)
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
    // Brightness seems to be maximum color value (0-255)
    // This works more like a spectrum, rather than a combination of basic colors

    // TODO: This still fails sometimes on some light colors???
    public RGBColor ToRGB()
    {
        // How much "far" we are in the current sector
        double hueNormalized = (Hue * 6) / 255f;

        // Saturation is inversed in UT
        double saturationPercent = 1f - Saturation / 255f;
        
        // This just means maximum RGB color value
        byte maxBrightness = Brightness;

        // Current color sector
        int hueSector = (int)Math.Floor(hueNormalized);
        double sectorFragment = hueNormalized - hueSector;

        // Lowest color value achievable
        byte lowValue = (byte)(maxBrightness * (1f - saturationPercent));

        // Color that is fading out in the current hue sector
        byte fallingValue = (byte)(maxBrightness * (1f - saturationPercent * sectorFragment));

        // Color that is fading in in the current hue sector
        byte risingValue = (byte)(maxBrightness * (1f - saturationPercent * (1f - sectorFragment)));

        byte red, green, blue;
        switch (hueSector)
        {
            case 0: red = maxBrightness; green = risingValue; blue = lowValue; break; // Red -> Yellow (0-45 range)
            case 1: red = fallingValue; green = maxBrightness; blue = lowValue; break; // Yellow -> Green (45-80 range)
            case 2: red = lowValue; green = maxBrightness; blue = risingValue; break; // Green -> Oceanish (80-135 range)
            case 3: red = lowValue; green = fallingValue; blue = maxBrightness; break; // Oceanish -> Blue (135-170 range)
            case 4: red = risingValue; green = lowValue; blue = maxBrightness; break; // Blue -> Pink (170-225 range)
            default: red = maxBrightness; green = lowValue; blue = fallingValue; break; // Pink -> Red (225-255 range)
        }

        Logger.Write(LogLevel.DEBUG, $"Converted Unreal HSV Light ({Hue} {Saturation} {Brightness}) -> RGB ({red} {green} {blue})");
        return new RGBColor(red, green, blue);
    }
}
