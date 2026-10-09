using UTModels;
using Xunit;

namespace t3d_to_vmf_convertion.Tests;

public class UTPolygon_HolesBridging
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

    // =====================================================
    // Test 1: Tall rectangle with three column-aligned holes at different Y ranges.
    // Each hole's ray cast hits a unique horizontal slice of the outer, so no
    // interference between bridges. Safe baseline for multi-hole.
    // =====================================================
    [Fact]
    public void TallRectangle_WithThreeVerticallyStackedHoles_BridgesAll()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 30, 0), (0, 30, 0));

        // Three holes at different Y bands, slightly staggered X to give each a unique rightmost-U
        var hole1 = MakePolygon("hole1",(3, 3, 0), (3, 7, 0), (5, 7, 0), (5, 3, 0));   // bottom band
        var hole2 = MakePolygon("hole2",(4, 13, 0), (4, 17, 0), (6, 17, 0), (6, 13, 0)); // middle band
        var hole3 = MakePolygon("hole3",(5, 23, 0), (5, 27, 0), (7, 27, 0), (7, 23, 0)); // top band

        Assert.True(outer.TryBridgeHoles(new[] { hole1, hole2, hole3 }));
        Assert.False(outer.IsInvalid());

        // 4 outer + 3 × (4 hole + 1 M-duplicate + 1 intersection + 1 intersection-duplicate) = 4 + 21 = 25
        Assert.Equal(25, outer.Vertices.Count);

        DumpAsObj(outer, "TallRectangle_ThreeStackedHoles");
        DumpTriangulatedAsObj(outer, "TallRectangle_ThreeStackedHoles");
    }

    // =====================================================
    // Test 2: Window-frame with multiple slots in a row (Mover2-shaped).
    // All slots share the same Y range, so all rays cast at Y=5 (the slots' bottom).
    // This is the harder case - exercises how the bridged outer behaves when
    // subsequent rays cross the same horizontal slice.
    // =====================================================
    [Fact]
    public void WindowFrame_WithFourSlotHoles_BridgesAll()
    {
        // 40x20 frame, CCW around +Z
        var outer = MakePolygon("outer",
            (0, 0, 0), (40, 0, 0), (40, 20, 0), (0, 20, 0));

        // 4 slots, each 5x10, spaced with 3-unit gaps. All CW around +Z.
        var slot1 = MakePolygon("slot1",(3, 5, 0), (3, 15, 0), (8, 15, 0), (8, 5, 0));
        var slot2 = MakePolygon("slot2",(11, 5, 0), (11, 15, 0), (16, 15, 0), (16, 5, 0));
        var slot3 = MakePolygon("slot3",(19, 5, 0), (19, 15, 0), (24, 15, 0), (24, 5, 0));
        var slot4 = MakePolygon("slot4",(27, 5, 0), (27, 15, 0), (32, 15, 0), (32, 5, 0));

        bool result = outer.TryBridgeHoles(new[] { slot1, slot2, slot3, slot4 });

        DumpAsObj(outer, "WindowFrame_FourSlots");
        DumpTriangulatedAsObj(outer, "WindowFrame_FourSlots");

        Assert.True(result, "Window-frame multi-hole bridging should succeed");
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles), "Bridged window-frame should triangulate after vertex-hit raycast chains the bridges");
        Assert.True(triangles.Count > 0);
    }

    // =====================================================
    // Test 3: Empty list of holes should succeed and leave the outer untouched
    // =====================================================
    [Fact]
    public void TryBridgeHoles_EmptyList_SucceedsAndLeavesOuterUnchanged()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));
        int originalCount = outer.Vertices.Count;
        var originalVertices = outer.Vertices.ToList();

        Assert.True(outer.TryBridgeHoles(Array.Empty<UTPolygon>()));
        Assert.Equal(originalCount, outer.Vertices.Count);

        for (int i = 0; i < originalCount; i++)
            Assert.True(originalVertices[i].NearlyEquals(outer.Vertices[i]));
    }

    // =====================================================
    // Test 4: Single hole should behave identically to TryBridgeHole
    // =====================================================
    [Fact]
    public void TryBridgeHoles_SingleHole_MatchesTryBridgeHoleResult()
    {
        var outerA = MakePolygon("outerA",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));
        var outerB = MakePolygon("outerB",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        var holeA = MakePolygon("holeA",(4, 4, 0), (4, 6, 0), (6, 6, 0), (6, 4, 0));
        var holeB = MakePolygon("holeB",(4, 4, 0), (4, 6, 0), (6, 6, 0), (6, 4, 0));

        Assert.True(outerA.TryBridgeHole(holeA));
        Assert.True(outerB.TryBridgeHoles(new[] { holeB }));

        Assert.Equal(outerA.Vertices.Count, outerB.Vertices.Count);
        for (int i = 0; i < outerA.Vertices.Count; i++)
            Assert.True(outerA.Vertices[i].NearlyEquals(outerB.Vertices[i]),
                $"Vertex {i} differs: {outerA.Vertices[i]} vs {outerB.Vertices[i]}");
    }

    // =====================================================
    // Test 5: Rollback on partial failure. Pair a valid hole with an out-of-bounds
    // one; the valid hole bridges first (higher rightmost-U), then the invalid one
    // fails the IsPointInside guard, and the outer should be reverted.
    // =====================================================
    [Fact]
    public void TryBridgeHoles_RollsBackWhenSecondHoleFails()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));
        int originalCount = outer.Vertices.Count;
        var originalVertices = outer.Vertices.ToList();

        // Valid hole near the right of outer - will get sorted first (highest U)
        var validHole = MakePolygon("validHole",(5, 4, 0), (5, 6, 0), (8, 6, 0), (8, 4, 0));

        // Invalid hole: probe vertex is outside the outer entirely
        var invalidHole = MakePolygon("invalidHole",(100, 100, 0), (100, 102, 0), (102, 102, 0), (102, 100, 0));

        Assert.False(outer.TryBridgeHoles(new[] { validHole, invalidHole }),
            "TryBridgeHoles should return false when any hole fails");

        Assert.Equal(originalCount, outer.Vertices.Count);
        for (int i = 0; i < originalCount; i++)
            Assert.True(originalVertices[i].NearlyEquals(outer.Vertices[i]),
                $"Vertex {i} should be restored after rollback");
    }

    // =====================================================
    // Test 6: Two distinct holes at different Y, processed in sort order.
    // After bridging, the polygon should contain both holes' vertices and remain valid.
    // =====================================================
    [Fact]
    public void Square_WithTwoSeparatedHoles_BridgesBoth()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (20, 0, 0), (20, 20, 0), (0, 20, 0));

        var hole1 = MakePolygon("hole1",(3, 3, 0), (3, 7, 0), (7, 7, 0), (7, 3, 0));
        var hole2 = MakePolygon("hole2",(3, 13, 0), (3, 17, 0), (7, 17, 0), (7, 13, 0));

        Assert.True(outer.TryBridgeHoles(new[] { hole1, hole2 }));
        Assert.False(outer.IsInvalid());

        // Both holes' rightmost vertices should appear twice each in the bridged outer
        // hole1's rightmost: (3, 3, 0) (max U=-3, tiebreak by V picks V=-3)
        // hole2's rightmost: (3, 13, 0)
        int hole1MCount = 0, hole2MCount = 0;
        foreach (var v in outer.Vertices)
        {
            if (v.NearlyEquals(new Vector(3, 3, 0))) hole1MCount++;
            if (v.NearlyEquals(new Vector(3, 13, 0))) hole2MCount++;
        }
        Assert.Equal(2, hole1MCount);
        Assert.Equal(2, hole2MCount);

        DumpAsObj(outer, "Square_WithTwoSeparatedHoles");
        DumpTriangulatedAsObj(outer, "Square_WithTwoSeparatedHoles");
    }

    // =====================================================
    // Test 7: Concave hole through TryBridgeHoles
    // Single L-shape hole, but routed through the multi-hole API.
    // Verifies the multi-hole code path doesn't choke on a concave hole.
    // =====================================================
    [Fact]
    public void Square_WithLShapedConcaveHole_ViaHoles_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

        var hole = MakePolygon("hole",
            (3, 3, 0), (3, 7, 0), (7, 7, 0), (7, 5, 0), (5, 5, 0), (5, 3, 0));

        Assert.True(hole.Normal.NearlyEquals(-outer.Normal));
        Assert.True(outer.TryBridgeHoles(new[] { hole }));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "Square_WithLShapedConcaveHole_ViaHoles");
        DumpTriangulatedAsObj(outer, "Square_WithLShapedConcaveHole_ViaHoles");
    }

    // =====================================================
    // Test 8: 3x2 grid of hexagonal holes
    // Six hexagons (3 columns, 2 rows) inside a 30x20 frame. The bottom-row hexagons
    // share Y range and must chain via vertex-hit raycasting; same for the top row.
    // The two rows don't interfere with each other (different Y).
    // =====================================================
    [Fact]
    public void WideRectangle_With3x2HexagonHoles_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (30, 0, 0), (30, 20, 0), (0, 20, 0));

        var s = Math.Sqrt(3);
        UTPolygon Hexagon(double cx, double cy) => MakePolygon($"Hexagon_{cx}_{cy}",
            (cx + 2, cy, 0),
            (cx + 1, cy - s, 0),
            (cx - 1, cy - s, 0),
            (cx - 2, cy, 0),
            (cx - 1, cy + s, 0),
            (cx + 1, cy + s, 0));

        var holes = new[]
        {
            Hexagon(5, 5),  Hexagon(15, 5),  Hexagon(25, 5),   // bottom row
            Hexagon(5, 15), Hexagon(15, 15), Hexagon(25, 15),  // top row
        };

        foreach (var h in holes)
            Assert.True(h.Normal.NearlyEquals(-outer.Normal));

        Assert.True(outer.TryBridgeHoles(holes));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "WideRectangle_3x2_HexagonHoles");
        DumpTriangulatedAsObj(outer, "WideRectangle_3x2_HexagonHoles");
    }

    // =====================================================
    // Test 9: 3x2 grid of L-shaped concave holes
    // Six concave holes in a 3-column, 2-row arrangement. Bottom row shares M's
    // V (Y range), top row shares M's V - same chaining requirement as the
    // hexagon grid, but every hole carries internal reflex corners.
    // =====================================================
    [Fact]
    public void WideRectangle_With3x2LShapedHoles_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (30, 0, 0), (30, 20, 0), (0, 20, 0));

        // L-shape (CW around +Z) with center (cx, cy), spanning [cx-2, cx+2] x [cy-2, cy+2].
        // Reflex corner at (cx+2, cy).
        UTPolygon LShape(double cx, double cy) => MakePolygon($"LShape_{cx}_{cy}",
            (cx - 2, cy - 2, 0),
            (cx - 2, cy + 2, 0),
            (cx + 2, cy + 2, 0),
            (cx + 2, cy, 0),
            (cx, cy, 0),
            (cx, cy - 2, 0));

        var holes = new[]
        {
            LShape(5, 5),  LShape(15, 5),  LShape(25, 5),    // bottom row
            LShape(5, 15), LShape(15, 15), LShape(25, 15),   // top row
        };

        foreach (var h in holes)
            Assert.True(h.Normal.NearlyEquals(-outer.Normal));

        Assert.True(outer.TryBridgeHoles(holes));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "WideRectangle_3x2_LShapedHoles");
        DumpTriangulatedAsObj(outer, "WideRectangle_3x2_LShapedHoles");
    }

    // =====================================================
    // Test 10: Mixed shapes - six hexagons plus one L-shape in the same outer
    // The L-shape sits in its own Y band (between the two hexagon rows), so it
    // bridges independently to the outer's left wall while the two hexagon rows
    // chain among themselves.
    // =====================================================
    [Fact]
    public void WideRectangle_WithSixHexagonsAndOneLShape_BridgesAndTriangulates()
    {
        var outer = MakePolygon("outer",
            (0, 0, 0), (40, 0, 0), (40, 20, 0), (0, 20, 0));

        var s = Math.Sqrt(3);
        UTPolygon Hexagon(double cx, double cy) => MakePolygon($"Hexagon_{cx}_{cy}",
            (cx + 2, cy, 0),
            (cx + 1, cy - s, 0),
            (cx - 1, cy - s, 0),
            (cx - 2, cy, 0),
            (cx - 1, cy + s, 0),
            (cx + 1, cy + s, 0));

        UTPolygon LShape(double cx, double cy) => MakePolygon($"LShape_{cx}_{cy}",
            (cx - 2, cy - 2, 0),
            (cx - 2, cy + 2, 0),
            (cx + 2, cy + 2, 0),
            (cx + 2, cy, 0),
            (cx, cy, 0),
            (cx, cy - 2, 0));

        var holes = new UTPolygon[]
        {
            Hexagon(5, 5),  Hexagon(15, 5),  Hexagon(25, 5),    // bottom row at Y=5
            Hexagon(5, 15), Hexagon(15, 15), Hexagon(25, 15),   // top row at Y=15
            LShape(35, 10),                                      // L-shape between rows
        };

        foreach (var h in holes)
            Assert.True(h.Normal.NearlyEquals(-outer.Normal));

        Assert.True(outer.TryBridgeHoles(holes));
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles));
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "WideRectangle_6Hexagons_1LShape");
        DumpTriangulatedAsObj(outer, "WideRectangle_6Hexagons_1LShape");
    }

    // =====================================================
    // Test 11: 5-pointed star outer with a mini-star hole in each leg
    // The outer is concave (5 reflex inner corners), each hole is also concave
    // (mini-star with 5 reflex inner corners), and Vertices[0]→Vertices[1] isn't
    // axis-aligned so the UV frame is rotated. Bridging needs to thread a ray
    // through these non-axis polygons and end up with a triangulable composite.
    // =====================================================
    [Fact]
    public void FivePointedStar_WithFiveMiniStarHoles_BridgesAndTriangulates()
    {
        // Five-pointed star, CCW around +Z when ccw=true.
        // 5 outer points at angles 18° + 72°k, 5 inner points at 54° + 72°k.
        UTPolygon Star(double cx, double cy, double outerR, double innerR, bool ccw)
        {
            var ar = new (double angle, double r)[10];
            for (int i = 0; i < 5; i++)
            {
                ar[2 * i]     = (18 + 72 * i, outerR);
                ar[2 * i + 1] = (54 + 72 * i, innerR);
            }
            if (!ccw) Array.Reverse(ar);

            var poly = new UTPolygon($"Star_{cx}_{cy}_{(ccw ? "ccw" : "cw")}");
            foreach (var (angle, r) in ar)
            {
                var rad = angle * Math.PI / 180.0;
                poly.PushVertex(new Vector(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad), 0));
            }
            return poly;
        }

        // Centroid of leg k (the triangle formed by outer point k and its two
        // adjacent inner points).
        Vector LegCentroid(int k, double outerR, double innerR)
        {
            double oRad  = (18 + 72 * k) * Math.PI / 180.0;
            double iLeft = (54 + 72 * k) * Math.PI / 180.0;     // CCW-next inner
            double iRight = (-18 + 72 * k) * Math.PI / 180.0;   // CCW-previous inner

            double cx = (outerR * Math.Cos(oRad) + innerR * Math.Cos(iLeft) + innerR * Math.Cos(iRight)) / 3.0;
            double cy = (outerR * Math.Sin(oRad) + innerR * Math.Sin(iLeft) + innerR * Math.Sin(iRight)) / 3.0;
            return new Vector(cx, cy, 0);
        }

        var outer = Star(0, 0, outerR: 20, innerR: 8, ccw: true);

        var miniStars = new List<UTPolygon>();
        for (int k = 0; k < 5; k++)
        {
            var c = LegCentroid(k, outerR: 20, innerR: 8);
            miniStars.Add(Star(c.X, c.Y, outerR: 2.0, innerR: 0.8, ccw: false));
        }

        foreach (var ms in miniStars)
            Assert.True(ms.Normal.NearlyEquals(-outer.Normal),
                "Each mini-star must wind opposite to the outer star");

        Assert.True(outer.TryBridgeHoles(miniStars), "Multi-hole bridging into a concave star outer should succeed");
        Assert.False(outer.IsInvalid());
        Assert.True(outer.GetTriangulated(out var triangles), "Bridged star polygon should triangulate");
        Assert.True(triangles.Count > 0);

        DumpAsObj(outer, "FivePointedStar_FiveMiniStarHoles");
        DumpTriangulatedAsObj(outer, "FivePointedStar_FiveMiniStarHoles");
    }
}
