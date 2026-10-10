using Islet.Core;
using SkiaSharp;

namespace Islet.Rendering;

/// <summary>Draws the island (glow + continuous-squircle body + clipped content) into an SKCanvas.</summary>
internal sealed class IslandRenderer
{
    public void Draw(SKCanvas canvas, RenderModel m)
    {
        canvas.Clear(SKColors.Transparent);

        float w = (float)m.Dp.Width * m.Scale;
        float h = (float)m.Dp.Height * m.Scale;
        float r = (float)m.Dp.CornerRadius * m.Scale;
        float cx = m.SurfaceW / 2f;
        float cy = m.AnchorTopPx + h / 2f;

        using var body = BuildBody(m, cx, cy, w, h, r);

        // --- Soft activity glow ---
        if (m.GlowStrength > 0.001f)
        {
            uint argb = m.AskActive ? m.AskGlowArgb : m.GlowArgb;
            var glow = new SKColor(argb);
            float sigma = MathF.Max(1f, m.GlowRadiusPx * m.Scale * 0.6f);
            using var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = glow.WithAlpha((byte)(glow.Alpha * Clamp01(m.GlowStrength))),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma),
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawPath(body, glowPaint);
        }

        // --- Black body ---
        using (var bodyPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 255), Style = SKPaintStyle.Fill })
            canvas.DrawPath(body, bodyPaint);

        // --- Content, clipped to the island shape ---
        int save = canvas.Save();
        canvas.ClipPath(body, SKClipOperation.Intersect, antialias: true);
        var bounds = new SKRect(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
        ContentRenderer.Draw(canvas, m, bounds);
        canvas.RestoreToCount(save);

        // --- Ask-mode running border (drawn on top, follows the contour) ---
        if (m.AskActive)
            AskDecoration.DrawBorder(canvas, body, m);
    }

    private static SKPath BuildBody(RenderModel m, float cx, float cy, float w, float h, float r)
    {
        var body = SquirclePath.Build(cx, cy, w, h, r, m.CornerExponent);
        if (m.Mode == IslandMode.Minimal && m.MinimalSeparation > 0.001f)
        {
            using var gooey = MinimalShape.BuildGooey(m, cx, cy, w, h);
            var combined = body.Op(gooey, SKPathOp.Union);
            body.Dispose();
            return combined;
        }
        return body;
    }

    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
