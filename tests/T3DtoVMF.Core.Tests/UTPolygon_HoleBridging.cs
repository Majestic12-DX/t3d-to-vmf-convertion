using UTModels;
using Xunit;

namespace t3d_to_vmf_convertion.Tests;

public class UTPolygon_HoleBridging
{
    private static UTPolygon MakePolygon(string name, params (double x, double y, double z)[] vertices)
    {
        var poly = new UTPolygon(name);
        foreach (var (x, y, z) in vertices)
            poly.PushVertex(new Vector(x, y, z));
        return poly;
    }

    private static readonly string _objOutputFolder = "hole_bridging_test_objs";

    // Direct vertex-list dump - one OBJ vertex per polygon vertex, no dedup or normal recompute.
    // Preserves bridge duplicates (M and P each appearing twice) so the in-memory topology is visible.
    // OBJWriter would route through BrushActor which dedupes via VectorComparer and may apply
    // other transformations - that's fine for production geometry but hides the splice structure.
    private static void DumpAsObj(UTPolygon polygon, string fileName)
    {
        Directory.CreateDirectory(_objOutputFolder);
        var path = Path.Combine(_objOutputFolder, $"{fileName}.obj");

        using var writer = new StreamWriter(path);
        writer.WriteLine($"# Raw polygon dump - {polygon.Vertices.Count} vertices (bridge duplicates preserved)");

        foreach (var v in polygon.Vertices)
            writer.WriteLine($"v {v.ToOBJString()}");

        writer.Write("f");
        for (int i = 1; i <= polygon.Vertices.Count; i++)
            writer.Write($" {i}");
        writer.WriteLine();
    }

    // Each triangle gets its own three OBJ vertices and one face. Easier to inspect than
    // the self-touching single face that the raw dump produces.
    private static void DumpTriangulatedAsObj(UTPolygon polygon, string fileName)
    {
        Directory.CreateDirectory(_objOutputFolder);
        var path = Path.Combine(_objOutputFolder, $"{fileName}_triangulated.obj");

        using var writer = new StreamWriter(path);

        if (!polygon.GetTriangulated(out var triangles))
        {
            writer.WriteLine($"# Triangulation FAILED for polygon with {polygon.Vertices.Count} vertices");
            return;
        }

        writer.WriteLine($"# Triangulated polygon - {triangles.Count} triangles");

        int vertexCounter = 1;
        foreach (var tri in triangles)
        {
            foreach (var v in tri.Vertices)
                writer.WriteLine($"v {v.ToOBJString()}");
            writer.WriteLine($"f {vertexCounter} {vertexCounter + 1} {vertexCounter + 2}");
            vertexCounter += 3;
        }
    }

    private static int CountOccurrencesNearlyEqual(UTPolygon polygon, Vector target)
    {
        int count = 0;
        foreach (var v in polygon.Vertices)
            if (v.NearlyEquals(target))
                count++;
        return count;
    }

    // =====================================================
    // Test 1: Basic square outer with a square hole in the middle
    // =====================================================
    [Fact]
    public void Square_WithCenteredSquareHole_BridgesSuccessfully()
    {
        // 10x10 outer, CCW around +Z
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        // 2x2 hole at (4..6, 4..6), CW around +Z (opposite winding to outer)
        var hole = MakePolygon("hole",
            (4, 4, 0), (4, 6, 0), (6, 6, 0), (6, 4, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal), "Hole must wind opposite to outer");
        Assert.True(outer.TryBridgeHole(hole), "TryBridgeHole should succeed for a hole fully inside the outer");

        // 4 outer + 4 hole + 1 hole-M duplicate + 1 intersection point (mid-edge) + 1 intersection duplicate = 11
        Assert.Equal(11, outer.Vertices.Count);

        Assert.False(outer.IsInvalid(), "Bridged polygon must still have a non-zero normal");

        // The hole's chosen bridge vertex M should appear exactly twice (start and end of hole loop walk)
        // Rightmost vertex of the hole in polygon-U coords is (4, 4, 0)
        Assert.Equal(2, CountOccurrencesNearlyEqual(outer, new Vector(4, 4, 0)));

        DumpAsObj(outer, "Square_WithCenteredSquareHole");
        DumpTriangulatedAsObj(outer, "Square_WithCenteredSquareHole");
    }

    // =====================================================
    // Test 2: Square outer with a triangular hole
    // =====================================================
    [Fact]
    public void Square_WithTriangleHole_BridgesSuccessfully()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        // Triangle hole, CW around +Z
        var hole = MakePolygon("hole",
            (3, 3, 0), (3, 6, 0), (6, 4, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));

        // 4 outer + 3 hole + 1 M-duplicate + 1 intersection + 1 intersection-duplicate = 10
        Assert.Equal(10, outer.Vertices.Count);
        Assert.False(outer.IsInvalid());

        DumpAsObj(outer, "Square_WithTriangleHole");
        DumpTriangulatedAsObj(outer, "Square_WithTriangleHole");
    }

    // =====================================================
    // Test 3: Concave L-shape outer with a square hole
    // =====================================================
    [Fact]
    public void LShape_WithSquareHole_BridgesSuccessfully()
    {
        // L-shape, CCW around +Z. Solid is bottom-left, cutout at upper-right.
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 5, 0), (5, 5, 0), (5, 10, 0), (0, 10, 0));

        // Small hole entirely in the lower-left solid quadrant
        var hole = MakePolygon("hole",
            (2, 2, 0), (2, 3, 0), (3, 3, 0), (3, 2, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));

        // 6 outer + 4 hole + 1 M-duplicate + 1 intersection + 1 intersection-duplicate = 13
        Assert.Equal(13, outer.Vertices.Count);
        Assert.False(outer.IsInvalid());

        DumpAsObj(outer, "LShape_WithSquareHole");
        DumpTriangulatedAsObj(outer, "LShape_WithSquareHole");
    }

    // =====================================================
    // Test 4: Rectangle with a hole close to the right edge (short bridge)
    // =====================================================
    [Fact]
    public void Rectangle_WithHoleNearRightEdge_BridgesSuccessfully()
    {
        // Wide rectangle (20x6), CCW around +Z
        var outer = MakePolygon("outer",
            (0, 0, 0), (20, 0, 0), (20, 6, 0), (0, 6, 0));

        var hole = MakePolygon("hole",
            (15, 2, 0), (15, 4, 0), (17, 4, 0), (17, 2, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));

        // 4 + 4 + 1 + 1 + 1 = 11
        Assert.Equal(11, outer.Vertices.Count);
        Assert.False(outer.IsInvalid());

        DumpAsObj(outer, "Rectangle_WithHoleNearRightEdge");
        DumpTriangulatedAsObj(outer, "Rectangle_WithHoleNearRightEdge");
    }

    // =====================================================
    // Test 5: Window-frame style outer with one hole (Mover2-shaped)
    // =====================================================
    [Fact]
    public void WindowFrame_WithSlotHole_BridgesSuccessfully()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (40, 0, 0), (40, 20, 0), (0, 20, 0));

        var hole = MakePolygon("hole",
            (10, 5, 0), (10, 15, 0), (15, 15, 0), (15, 5, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));

        Assert.False(outer.IsInvalid());

        DumpAsObj(outer, "WindowFrame_WithSlotHole");
        DumpTriangulatedAsObj(outer, "WindowFrame_WithSlotHole");
    }

    // =====================================================
    // Test 6: Failure case - hole with same winding as outer should be rejected
    // =====================================================
    [Fact]
    public void TryBridgeHole_RejectsHoleWithSameWindingAsOuter()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        // Same winding as outer (CCW around +Z) - this is what an outer loop would look like,
        // not a hole. TryBridgeHole should refuse it because the normal check fails.
        var notAHole = MakePolygon("notAHole",
            (4, 4, 0), (6, 4, 0), (6, 6, 0), (4, 6, 0));

        Assert.True(notAHole.Normal.NearlyEquals(outer.Normal), "Test setup: both should wind the same way");
        Assert.False(outer.TryBridgeHole(notAHole), "Should refuse a hole with non-opposing winding");
    }

    // =====================================================
    // Test 7: Failure case - hole entirely outside the outer
    // =====================================================
    [Fact]
    public void TryBridgeHole_RejectsHoleOutsideOuter()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        // Hole positioned outside the outer, correct opposite winding
        var hole = MakePolygon("hole",
            (100, 100, 0), (100, 102, 0), (102, 102, 0), (102, 100, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.False(outer.TryBridgeHole(hole), "Should refuse a hole whose probe vertex is outside the outer");
    }

    // =====================================================
    // Test 8: L-shaped concave hole
    // The hole itself is non-convex - one of its vertices (7,5) is a reflex corner.
    // Verifies bridging + triangulation work when the hole carries concavity.
    // =====================================================
    [Fact]
    public void Square_WithLShapedConcaveHole_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        // L-shape (CW around +Z) - outline:
        //   (3,7)──(7,7)
        //     │      │
        //     │   (7,5)──(5,5) reflex inner corner at (7,5)
        //     │            │
        //   (3,3)────────(5,3)
        var hole = MakePolygon("hole",
            (3, 3, 0), (3, 7, 0), (7, 7, 0), (7, 5, 0), (5, 5, 0), (5, 3, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "Square_WithLShapedConcaveHole");
        DumpTriangulatedAsObj(outer, "Square_WithLShapedConcaveHole");
    }

    // =====================================================
    // Test 9: Hexagonal hole (6 vertices)
    // Regular hexagon centered at (5,5), radius 2, CW around +Z.
    // =====================================================
    [Fact]
    public void Square_WithHexagonHole_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        var s = Math.Sqrt(3);
        var hole = MakePolygon("hole",
            (7, 5, 0),
            (6, 5 - s, 0),
            (4, 5 - s, 0),
            (3, 5, 0),
            (4, 5 + s, 0),
            (6, 5 + s, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHole(hole));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "Square_WithHexagonHole");
        DumpTriangulatedAsObj(outer, "Square_WithHexagonHole");
    }
}
