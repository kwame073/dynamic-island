using Islet.Core;
using SkiaSharp;

namespace Islet.Rendering;

/// <summary>Converts the Core continuous-rounded-rect outline into an <see cref="SKPath"/>.</summary>
internal static class SquirclePath
{
    public static SKPath Build(float cx, float cy, float width, float height, float radius, double exponent)
    {
        float left = cx - width / 2f;
        float top = cy - height / 2f;

        var pts = ContinuousRoundedRect.Build(width, height, radius, exponent);
        var path = new SKPath();
        bool first = true;
        foreach (var p in pts)
        {
            float x = left + (float)p.X;
            float y = top + (float)p.Y;
            if (first) { path.MoveTo(x, y); first = false; }
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }
}
