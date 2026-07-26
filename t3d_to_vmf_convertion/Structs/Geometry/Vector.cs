using System.Globalization;
using System.Runtime.CompilerServices;

// A lot of very old and a little old comments. There should be an "UnrealVector" and a "SourceVector"...

/*
    Old Unreal Engine Epsilons. Holy shit
    https://github.com/FaultyRAM/Ut99PubSrc/blob/master/Core/Inc/UnMath.h#L1288

    Lengths of normalized vectors (These are half their maximum values
    to assure that dot products with normalized vectors don't overflow).
    #define FLOAT_NORMAL_THRESH				(0.0001f)

    Magic numbers for numerical precision.

    #define THRESH_POINT_ON_PLANE			(0.10f)		Thickness of plane for front/back/inside test
    #define THRESH_POINT_ON_SIDE			(0.20f)		Thickness of polygon side's side-plane for point-inside/outside/on side test
    #define THRESH_POINTS_ARE_SAME			(0.002f)	Two points are same if within this distance
    #define THRESH_POINTS_ARE_NEAR			(0.015f)	Two points are near if within this distance and can be combined if imprecise math is ok
    
    #define THRESH_NORMALS_ARE_SAME			(0.00002f)	Two normal points are same if within this distance
                                                        Making this too large results in incorrect CSG classification and disaster
    
    #define THRESH_VECTORS_ARE_NEAR			(0.0004f)	Two vectors are near if within this distance and can be combined if imprecise math is ok
                                                        Making this too large results in lighting problems due to inaccurate texture coordinates
    
    #define THRESH_SPLIT_POLY_WITH_PLANE	(0.25f)		A plane splits a polygon in half
    #define THRESH_SPLIT_POLY_PRECISELY		(0.01f)		A plane exactly splits a polygon
    #define THRESH_ZERO_NORM_SQUARED		(0.0001f)	Size of a unit normal that is considered "zero", squared
    #define THRESH_VECTORS_ARE_PARALLEL		(0.02f)		Vectors are parallel if dot product varies less than this
*/

public readonly struct Vector : IEquatable<Vector>
{
    // Old Comment
    // Vertex Epsilon, should be 10 times tighter than plane epsilon as I know...
    // Is 1e-6 as of writing this, 6 digits of precision like .t3d vectors. May need to be bigger, because UT Compiler works with polygons, not volumes like VBSP
    // You can have a tiny sliver in what seems to be a closed volume which is actually a non-watertight, and UT gladly accepts...
    // UT does not care about manifoldness when it comes to maps at least, too
    // It also has a lot of ways to produce floating point errors. As I know - the author scaling brushes at the very least
    // Maybe should be 0.01? BRUSH_CLIP_EPSILON. Or 0.1? THRESH_POINT_ON_PLANE
    // Should be merged with PlaneEpsilon into 1 epsilon?
    // https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/utils/vbsp/map.cpp#L30
    
    // UPD: Should stay 0.01, probably. However, may use 0.05 epsilon for planes
    // Because it removes "edge is too small" error on that one cone concave brush split and on largest Brush195 split (AS-Guardia)
    // And manages to decompose Brush101, too

    // UPD UPD: Should be slightly bigger than PolygonPlanarityEpsilon (0.002?)
    // Or probably stay like this, can be used for welding tiny cracks in the brush geometry
    public static readonly double CoordinateEpsilon = 0.01; // 0.000001;
    public static readonly double CoordinateEpsilonSquared = CoordinateEpsilon * CoordinateEpsilon;

    // THRESH_SPLIT_POLY_PRECISELY from Unreal public code
    // Used for splitting concaves with Sutherland Hodgman 3D clipper
    // The cap polygons should probably be planarized as a sanity and safety measure
    // By multi-plane intersections (SHOULD BE DONE BEFORE TRIANGULATING IN CASE CAP IS CONCAVE!!!)
    public const double SplitPolygonByPlaneEpsilon = 0.01;

    // For planarization of polygons by reconstructing corners using plane intersections
    public const double PolygonPlanarityEpsilon = 0.0001;

    // Divide by zero guard
    public static readonly double ShortEdgeEpsilon = 1e-9;

    // "Exactly collinear" threshold, far tighter than CoordinateEpsilon. A vertex whose
    // perpendicular distance to its neighbor line is below this is collinear only to
    // floating-point noise (cut-line intersection math), so dropping it cannot move the
    // outline. Used by CleanupCollinearPoints to de-tessellate cap polygons WITHOUT the
    // invasive geometry shifting that the 0.01 threshold caused. .t3d coords carry ~6
    // digits, so 1e-6 sits above FP noise yet below any real geometric deviation.

    // For cleaning up collinear vertices on polygons
    public static readonly double CollinearExactEpsilon = 1e-6;
    public static readonly double CollinearExactEpsilonSquared = CollinearExactEpsilon * CollinearExactEpsilon;

    // Perpendicular-distance tolerance for IsOnLine (point-on-segment / T-junction tests).
    // Deliberately tighter than CoordinateEpsilon (0.01): the 0.01 perp band was fat enough
    // to "absorb" sub-0.01 cracks in UT brushes (e.g. a vertex 0.0036 off an edge), papering
    // them over as fake collinearity so decomposition tried and failed. 0.001 surfaces those
    // cracks so such brushes are honestly classified non-manifold, while staying loose enough
    // to tolerate UT's normal FP noise (1e-6 was too tight and exploded tessellation).
    // Validated across the test-map corpus (global 0.001 clean; T-junction-only desynced
    // predicates and regressed Guardia).

    // For checking if vertex is on ray/edge
    public static readonly double PerpDistanceEpsilon = 0.001;
    public static readonly double PerpDistanceEpsilonSquared = PerpDistanceEpsilon * PerpDistanceEpsilon;

    // Source seems to eat this tolerance. Tested on Brush101 AS-Guardia, which failed on 0.01 epsilon
    // Might need to get this back? Should solve Brush195 self intersecting cap poly first
    // Maybe something as simple as proper CCW heuristic on the walker would do the trick
    // I think our current setup allows that

    // THRESH_POINT_ON_PLANE from Unreal public code
    public static readonly double PlaneEpsilon = 0.1; // 0.05, 0.00001;

    // Tolerance for the convex/concave CLASSIFICATION only (CalculateBrushType), kept
    // SEPARATE from PlaneEpsilon so concavity tolerance can be tuned without loosening
    // clipping / plane-membership (which want the tight 0.1). A vertex must sit MORE than
    // this far in front of a face plane to make the brush concave. Set to Unreal's
    // THRESH_POINT_ON_SIDE (0.20) - looser than THRESH_POINT_ON_PLANE/PlaneEpsilon (0.10) -
    // because face-plane normals from cap-triangulation slivers are imprecise at deep
    // recursion, and a too-tight test flags marginal (~0.13) false concavities that then
    // fail to split and silently drop geometry.

    // THRESH_POINT_ON_SIDE from Unreal public code
    // Probably should be set to 0.1 later
    // If any vertex is in front of any polygon by this many units the brush is concave
    public static readonly double ConvexEpsilon = 0.2;

    // If a polygon is non-planar by more than MaximumPlanarizationEpsilon units it gets triangulated instead of straightened
    // Polygons in Unreal are considered planar if they are less than 0.2 units non-planar
    // This will be a deciding epsilon of what to do with a non-planar polygon (planarize/triangulate)
    public const double MaximumPlanarizationEpsilon = 0.2;

    public static readonly Vector Zero = new Vector(0, 0, 0);
    public static readonly Vector Right = new Vector(1, 0, 0);
    public static readonly Vector Forward = new Vector(0, 1, 0);
    public static readonly Vector Up = new Vector(0, 0, 1);

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public Vector(double x = 0, double y = 0, double z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }
    public Vector((double X, double Y, double Z) coords)
    {
        X = coords.X;
        Y = coords.Y;
        Z = coords.Z;
    }

    #region Vector Mathematics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsZero() => X == 0 && Y == 0 && Z == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector Cross(Vector a, Vector b) => new Vector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double LengthSquared() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Length() => Math.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DistanceSquared(Vector otherVec) => (this - otherVec).LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Distance(Vector otherVec) => (this - otherVec).Length();

    // Old method was a simple triangle inequality test
    // Which was error prone...
    public bool IsOnLine(Vector lineStart, Vector lineEnd, bool allowOutOfBounds = false)
    {
        if (this.NearlyEquals(lineStart) || this.NearlyEquals(lineEnd)) return false;

        Vector lineVec = lineEnd - lineStart;
        double lineLengthSquared = lineVec.LengthSquared();

        // Degenerate-line / divide-by-zero guard for the fraction below (ShortEdgeEpsilon),
        // decoupled from CoordinateEpsilon. The perp test below, on its own PerpDistanceEpsilon,
        // is what actually decides on-line-ness.
        if (lineLengthSquared <= ShortEdgeEpsilon) return false;

        Vector pointVec = this - lineStart;

        double fraction = Dot(pointVec, lineVec) / lineLengthSquared;

        // Check if within line bounds
        if (!allowOutOfBounds && (fraction <= 0 || fraction >= 1)) return false;

        // Calculate line fraction point that is nearest to our point
        Vector closestPoint = lineStart + lineVec * fraction;

        // Check how far the actual point is from closest line position
        return this.DistanceSquared(closestPoint) <= PerpDistanceEpsilonSquared;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector GetNormal(Vector vec1, Vector vec2, Vector vec3)
    {
        if (vec1.NearlyEquals(vec2) || vec1.NearlyEquals(vec3) || vec2.NearlyEquals(vec3))
            return Zero;

        if (vec2.IsOnLine(vec1, vec3, allowOutOfBounds: true))
            return Zero;

        Vector edge1 = vec2 - vec1;
        Vector edge2 = vec3 - vec1;
        Vector crossResult = Cross(edge1, edge2);

        double len = crossResult.Length();
        return new Vector(crossResult.X / len, crossResult.Y / len, crossResult.Z / len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector GetNormalized()
    {
        double len = Length();
        if (len <= CoordinateEpsilon)
            return Zero;

        return new Vector(X / len, Y / len, Z / len);
    }

    public Vector GetTangent()
    {
        Vector tangent = Math.Abs(X) > Math.Abs(Z)
                                ? new Vector(-Y, X, 0)
                                : new Vector(0, -Z, Y);

        return tangent.GetNormalized();
    }

    #endregion

    // This but vertex level instead of matrices
    // https://github.com/FaultyRAM/Ut99PubSrc/blob/db43a2e05935cb1b36a7a654322d0609a43cb152/Core/Inc/UnMath.h#L1934
    private static readonly double tau = Math.PI * 2;
    public Vector GetRotatedFromUnrealAngle(Angle unrealAngle)
    {
        double roll  = (unrealAngle.Roll / Angle.FullUnrealCircle) * tau;
        double pitch = (unrealAngle.Pitch / Angle.FullUnrealCircle) * tau;
        double yaw   = (unrealAngle.Yaw / Angle.FullUnrealCircle) * tau;

        double cosRoll = Math.Cos(roll), sinRoll = Math.Sin(roll);
        double cosPitch = Math.Cos(pitch), sinPitch = Math.Sin(pitch);
        double cosYaw = Math.Cos(yaw), sinYaw = Math.Sin(yaw);

        double x = X, y = Y, z = Z;

        // Roll (around X)
        double yAfterRoll =  y * cosRoll + z * sinRoll;
        double zAfterRoll = -y * sinRoll + z * cosRoll;
        y = yAfterRoll; z = zAfterRoll;

        // Pitch (around Y)
        double xAfterPitch = x * cosPitch - z * sinPitch;
        double zAfterPitch = x * sinPitch + z * cosPitch;
        x = xAfterPitch; z = zAfterPitch;

        // Yaw (around Z)
        double xAfterYaw = x * cosYaw - y * sinYaw;
        double yAfterYaw = x * sinYaw + y * cosYaw;

        return new Vector(xAfterYaw, yAfterYaw, z);
    }

    // Set to 1.33x or 0.75? Unreal Unit = 1.905~ cm, Source Unit = 1~ inch (2.54~ cm)
    private const double SourceScale = 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector GetSourcePositionFromUnrealPosition(Vector vec) => new Vector(vec.X, -vec.Y, vec.Z) * SourceScale;

    #region Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator +(Vector a, Vector b) => new Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator -(Vector a, Vector b) => new Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator -(Vector a) => new Vector(a.X * -1, a.Y * -1, a.Z * -1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator *(Vector a, double doubleValue) => new Vector(a.X * doubleValue, a.Y * doubleValue, a.Z * doubleValue);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator *(double doubleValue, Vector a) => new Vector(a.X * doubleValue, a.Y * doubleValue, a.Z * doubleValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator *(Vector a, Vector b) => new Vector(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator /(Vector a, Vector b) => new Vector(a.X / b.X, a.Y / b.Y, a.Z / b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator /(Vector a, double doubleValue) => new Vector(a.X / doubleValue, a.Y / doubleValue, a.Z / doubleValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector operator /(Vector a, int intValue) => new Vector(a.X / intValue, a.Y / intValue, a.Z / intValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector a, Vector b) => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector a, Vector b) => !a.Equals(b);
    #endregion
    #region Comparison Stuff
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector otherVec) => X == otherVec.X && Y == otherVec.Y && Z == otherVec.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is Vector otherVec && Equals(otherVec);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetSnappedValue(double value) => Math.Round(value / CoordinateEpsilon) * CoordinateEpsilon;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector GetSnapped()
    {
        double snappedX = GetSnappedValue(X);
        double snappedY = GetSnappedValue(Y);
        double snappedZ = GetSnappedValue(Z);
        return new Vector(snappedX, snappedY, snappedZ);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        var snappedToGrid = GetSnapped();
        return HashCode.Combine(snappedToGrid.X, snappedToGrid.Y, snappedToGrid.Z);
        //return HashCode.Combine(X, Y, Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NearlyEquals(Vector otherVec) => Math.Abs(X - otherVec.X) <= CoordinateEpsilon && Math.Abs(Y - otherVec.Y) <= CoordinateEpsilon && Math.Abs(Z - otherVec.Z) <= CoordinateEpsilon;
    #endregion

    public override string ToString() => $"(X: {X} Y: {Y} Z: {Z})";

    private static readonly double HAMMER_COORD_MIN = -16383f;
    private static readonly double HAMMER_COORD_MAX = 16383f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double X, double Y, double Z) GetVMFCoords(double margin = 0f)
    {
        return (
            GetSnappedValue(Math.Clamp(X, HAMMER_COORD_MIN + margin, HAMMER_COORD_MAX - margin)),
            GetSnappedValue(Math.Clamp(Y, HAMMER_COORD_MIN + margin, HAMMER_COORD_MAX - margin)),
            GetSnappedValue(Math.Clamp(Z, HAMMER_COORD_MIN + margin, HAMMER_COORD_MAX - margin))
        );
    }

    private static readonly int decimals = (int)Math.Round(-Math.Log10(CoordinateEpsilon));
    private static readonly string decimalsFormat = $"F{decimals}";
    public string ToVMFString()
    {
        var vmfCoords = GetVMFCoords();
        return string.Format(
            CultureInfo.InvariantCulture,
            $"{{0:{decimalsFormat}}} {{1:{decimalsFormat}}} {{2:{decimalsFormat}}}",
            vmfCoords.X,
            vmfCoords.Y,
            vmfCoords.Z
        );
    }

    public string ToOBJString()
        => FormattableString.Invariant($"{X} {Z} {-Y}");
}