using System.Runtime.CompilerServices;

public static class Geometry
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNonCollinear(Vector vec1, Vector vec2, Vector vec3)
    {
        if (vec1.NearlyEquals(vec2) || vec1.NearlyEquals(vec3) || vec2.NearlyEquals(vec3)) return false;
        return !vec2.IsOnLine(vec1, vec3, allowOutOfBounds: true);
    }
}
