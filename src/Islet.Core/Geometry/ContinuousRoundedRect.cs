using System;
using System.Collections.Generic;

namespace Islet.Core;

/// <summary>
/// Builds a "continuous" (superelliptical) rounded-rectangle outline — the squircle look used by
/// iOS, as opposed to a plain circular <c>RoundedRect</c>. Corners follow a Lamé curve
/// |x|^n + |y|^n = 1 so curvature ramps smoothly (G2-ish) into the straight edges.
/// When radius == height/2 and width &gt; height this yields a continuous "stadium" pill.
/// Output is a closed polyline (clockwise), consumed by the renderer to build an SKPath.
/// </summary>
public static class ContinuousRoundedRect
{
    /// <param name="w">Width in pixels.</param>
    /// <param name="h">Height in pixels.</param>
    /// <param name="radius">Corner radius (clamped to min(w,h)/2).</param>
    /// <param name="exponent">Superellipse exponent; ~2 = circular, ~5 = iOS-continuous.</param>
    /// <param name="perCorner">Samples per corner.</param>
    public static List<Vec2> Build(double w, double h, double radius, double exponent = 5.0, int perCorner = 22)
    {
        double r = Math.Max(0.0, Math.Min(radius, Math.Min(w, h) / 2.0));
        var pts = new List<Vec2>(perCorner * 4 + 4);

        if (r <= 0.01)
        {
            pts.Add(new Vec2(0, 0));
            pts.Add(new Vec2(w, 0));
            pts.Add(new Vec2(w, h));
            pts.Add(new Vec2(0, h));
            return pts;
        }

        // Corner centers.
        Vec2 tr = new(w - r, r), br = new(w - r, h - r), bl = new(r, h - r), tl = new(r, r);

        // Clockwise from the top edge. Each corner sweeps a quarter superellipse.
        AddCorner(pts, tr, r, -Math.PI / 2, 0, exponent, perCorner);        // top-right
        AddCorner(pts, br, r, 0, Math.PI / 2, exponent, perCorner);         // bottom-right
        AddCorner(pts, bl, r, Math.PI / 2, Math.PI, exponent, perCorner);   // bottom-left
        AddCorner(pts, tl, r, Math.PI, 3 * Math.PI / 2, exponent, perCorner); // top-left
        return pts;
    }

    private static void AddCorner(List<Vec2> pts, Vec2 c, double r, double a0, double a1, double n, int steps)
    {
        double inv = 2.0 / n;
        for (int i = 0; i <= steps; i++)
        {
            double t = a0 + (a1 - a0) * (i / (double)steps);
            double cos = Math.Cos(t), sin = Math.Sin(t);
            double sx = Math.Sign(cos) * Math.Pow(Math.Abs(cos), inv);
            double sy = Math.Sign(sin) * Math.Pow(Math.Abs(sin), inv);
            pts.Add(new Vec2(c.X + r * sx, c.Y + r * sy));
        }
    }
}
