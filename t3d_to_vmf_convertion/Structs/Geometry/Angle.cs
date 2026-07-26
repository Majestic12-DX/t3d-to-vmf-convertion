using System.Runtime.CompilerServices;

// There should be "EulerAngle" and "UnrealAngle"...
public readonly struct Angle
{
    public const double FullUnrealCircle = 65536;
    public const double FullEulerCircle = 360;

    public double Pitch { get; }
    public double Yaw { get; }
    public double Roll { get; }

    public Angle(double pitch = 0, double yaw = 0, double roll = 0)
    {
        Pitch = pitch;
        Yaw = yaw;
        Roll = roll;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsZero() => Pitch == 0 && Yaw == 0 && Roll == 0;

    private const double UnrealToEulerFactor = FullEulerCircle / FullUnrealCircle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double UnrealAngleComponentToEulerAngleComponent(double component)
    {
        return component * UnrealToEulerFactor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Angle GetEulerAngleFromUnrealAngle(Angle ang)
    {
        double pitch = UnrealAngleComponentToEulerAngleComponent(ang.Pitch);
        double yaw = -UnrealAngleComponentToEulerAngleComponent(ang.Yaw);
        double roll = UnrealAngleComponentToEulerAngleComponent(ang.Roll);
        
        return new Angle(pitch, yaw, roll);
    }

    public override string ToString() => $"(Pitch: {Pitch}, Yaw: {Yaw}, Roll: {Roll})";
    public string ToVMFString() => FormattableString.Invariant($"{Pitch} {Yaw} {Roll}");
}