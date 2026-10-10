using SkiaSharp;

namespace Islet.Rendering;

/// <summary>
/// Ask-mode contour decoration: a faint always-on glowing rim plus a bright "comet" highlight
/// that runs around the island outline (driven by <see cref="RenderModel.BorderSweepPhase"/>).
/// </summary>
internal static class AskDecoration
{
    public static void DrawBorder(SKCanvas canvas, SKPath body, RenderModel m)
    {
        var glow = new SKColor(m.AskGlowArgb);
        float sc = m.Scale;

        // Faint always-on rim.
        using (var rim = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f * sc,
            Color = glow.WithAlpha(70),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.5f * sc),
        })
            canvas.DrawPath(body, rim);

        // Running comet highlight.
        using var pm = new SKPathMeasure(body, true, 1);
        float len = pm.Length;
        if (len <= 1f) return;

        float segLen = len * 0.16f;
        float start = (m.BorderSweepPhase % 1f + 1f) % 1f * len;
        using var seg = new SKPath();
        if (start + segLen <= len)
        {
            pm.GetSegment(start, start + segLen, seg, true);
        }
        else
        {
            pm.GetSegment(start, len, seg, true);
            pm.GetSegment(0, start + segLen - len, seg, true);
        }

        using var halo = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.0f * sc,
            StrokeCap = SKStrokeCap.Round,
            Color = glow.WithAlpha(230),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f * sc),
        };
        canvas.DrawPath(seg, halo);

        using var core = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f * sc,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(240, 241, 255, 255),
        };
        canvas.DrawPath(seg, core);
    }
}
