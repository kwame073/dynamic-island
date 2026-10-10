using Islet.Core;

namespace Islet.Tests;

public class GeometryTests
{
    [Fact]
    public void Pill_Points_Stay_Within_Bounds()
    {
        double w = 126, h = 37;
        var pts = ContinuousRoundedRect.Build(w, h, h / 2);
        Assert.True(pts.Count > 40);
        foreach (var p in pts)
        {
            Assert.InRange(p.X, -0.001, w + 0.001);
            Assert.InRange(p.Y, -0.001, h + 0.001);
        }
    }

    [Fact]
    public void Radius_Is_Clamped_To_Half_Min_Side()
    {
        // Asking for a huge radius on a wide short rect must not blow past height.
        var pts = ContinuousRoundedRect.Build(300, 40, 999);
        foreach (var p in pts)
            Assert.InRange(p.Y, -0.001, 40.001);
    }

    [Fact]
    public void Zero_Radius_Is_A_Rectangle()
    {
        var pts = ContinuousRoundedRect.Build(100, 50, 0);
        Assert.Equal(4, pts.Count);
    }

    [Fact]
    public void Corners_Touch_All_Four_Edges()
    {
        double w = 200, h = 80;
        var pts = ContinuousRoundedRect.Build(w, h, 20);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in pts)
        {
            minX = System.Math.Min(minX, p.X); maxX = System.Math.Max(maxX, p.X);
            minY = System.Math.Min(minY, p.Y); maxY = System.Math.Max(maxY, p.Y);
        }
        Assert.Equal(0, minX, 0.5); Assert.Equal(w, maxX, 0.5);
        Assert.Equal(0, minY, 0.5); Assert.Equal(h, maxY, 0.5);
    }
}
