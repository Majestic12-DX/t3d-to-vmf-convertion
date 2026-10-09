using UTModels;
using Xunit;

namespace t3d_to_vmf_convertion.Tests;

public class UTPolygon_IsPointInside
{
    private static UTPolygon MakePolygon(string name, params (double x, double y, double z)[] vertices)
    {
        var poly = new UTPolygon(name);
        foreach (var (x, y, z) in vertices)
            poly.PushVertex(new Vector(x, y, z));

        return poly;
    }

    private static UTPolygon SmallSquare() =>
        MakePolygon("SmallSquare", (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0));

    private static UTPolygon LargeSquare() =>
        MakePolygon("LargeSquare", (0, 0, 0), (10, 0, 0), (10, 10, 0), (0, 10, 0));

    private static UTPolygon LShape() =>
        MakePolygon("LShape", (0, 0, 0), (10, 0, 0), (10, 5, 0), (5, 5, 0), (5, 10, 0), (0, 10, 0));

    [Fact]
    public void SmallSquare_CenterPoint_IsInside()
    {
        Assert.True(SmallSquare().IsPointInside(new Vector(0.5, 0.5, 0)));
    }

    [Fact]
    public void SmallSquare_PointOutsideToRight_IsNotInside()
    {
        Assert.False(SmallSquare().IsPointInside(new Vector(2, 0.5, 0)));
    }

    [Fact]
    public void SmallSquare_PointAbovePlane_IsNotInside()
    {
        Assert.False(SmallSquare().IsPointInside(new Vector(0.5, 0.5, 5)));
    }

    [Fact]
    public void LargeSquare_CenterPoint_IsInside()
    {
        Assert.True(LargeSquare().IsPointInside(new Vector(5, 5, 0)));
    }

    [Fact]
    public void LargeSquare_PointFarOutside_IsNotInside()
    {
        Assert.False(LargeSquare().IsPointInside(new Vector(20, 5, 0)));
    }

    [Fact]
    public void LargeSquare_PointNearEdge_IsInside()
    {
        Assert.True(LargeSquare().IsPointInside(new Vector(9.5, 5, 0)));
    }

    [Fact]
    public void LShape_PointInLowerLeftSolid_IsInside()
    {
        Assert.True(LShape().IsPointInside(new Vector(2, 2, 0)));
    }

    [Fact]
    public void LShape_PointInConcavityCutout_IsNotInside()
    {
        Assert.False(LShape().IsPointInside(new Vector(7, 7, 0)));
    }

    [Fact]
    public void LShape_PointInLowerRightSolid_IsInside()
    {
        Assert.True(LShape().IsPointInside(new Vector(7, 2, 0)));
    }

    [Fact]
    public void TiltedSquare_CenterPoint_IsInside()
    {
        var poly = MakePolygon("TiltedSquare", (0, 0, 0), (1, 0, 1), (1, 1, 1), (0, 1, 0));
        Assert.True(poly.IsPointInside(new Vector(0.5, 0.5, 0.5)));
    }
}
