using System.Runtime.CompilerServices;

public readonly struct Plane
{
    public Vector Origin { get; init; }
    public Vector Normal { get; init; }

    public Plane(Vector origin, Vector normal)
    {
        Origin = origin;
        Normal = normal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Distance() => Vector.Dot(Normal, Origin);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double SignedDistance(Vector point) => Vector.Dot(Normal, point - Origin);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPointBehindPlane(Vector point) => SignedDistance(point) < -Vector.PlaneEpsilon;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPointInFrontOfPlane(Vector point) => SignedDistance(point) > Vector.PlaneEpsilon;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPointOnPlane(Vector point)
    {
        var signedDistance = SignedDistance(point);
        return signedDistance >= -Vector.PlaneEpsilon && signedDistance <= Vector.PlaneEpsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPointOnPlane(Vector point, double epsilon)
    {
        var signedDistance = SignedDistance(point);
        return signedDistance >= -epsilon && signedDistance <= epsilon;
    }

    // Closest point on the plane
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector ProjectPoint(Vector point) => point - Normal * SignedDistance(point);

    public static Plane operator -(Plane a) => new Plane(a.Origin, -a.Normal);

    public override string ToString()
    {
        return $"Normal {Normal}, Origin {Origin}";
    }
}
