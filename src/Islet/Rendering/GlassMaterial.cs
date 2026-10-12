using SkiaSharp;

namespace Islet.Rendering;

/// <summary>
/// "Liquid Glass" island body: a translucent smoked panel (lets the desktop/content show through),
/// a specular sheen along the top, a faint reflection at the bottom, and an edge rim-light that is
/// bright at the top and faint below. Grounded in Apple's regular-glass look while staying legible.
/// </summary>
internal static class GlassMaterial
{
    public static void Draw(SKCanvas canvas, SKPath body, SKRect bounds, RenderModel m)
    {
        float scale = m.Scale;
        float top = bounds.Top, bot = bounds.Bottom;
        var tintTop = new SKColor(m.GlassTintTop);
        var tintBottom = new SKColor(m.GlassTintBottom);

        // 1) Translucent tinted fill (vertical gradient top -> bottom).
        using (var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, bot),
                new[] { tintTop, tintBottom }, null, SKShaderTileMode.Clamp),
        })
            canvas.DrawPath(body, fill);

        // Inner sheen + reflection, clipped to the shape. Rect is widened so the Minimal blob is covered.
        int save = canvas.Save();
        canvas.ClipPath(body, antialias: true);
        var wide = new SKRect(bounds.Left - 180, top, bounds.Right + 180, bot);

        byte sheenA = (byte)(96 * m.GlassHighlight);
        float sheenBottom = top + bounds.Height * 0.52f;
        using (var sheen = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, sheenBottom),
                new[] { SKColors.White.WithAlpha(sheenA), SKColors.White.WithAlpha(0) }, null, SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(wide, sheen);

        byte reflA = (byte)(30 * m.GlassHighlight);
        using (var refl = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, bot - bounds.Height * 0.32f), new SKPoint(0, bot),
                new[] { SKColors.White.WithAlpha(0), SKColors.White.WithAlpha(reflA) }, null, SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(wide, refl);
        canvas.RestoreToCount(save);

        // 2) Edge rim light: bright at the top, invisible mid, faint reflected at the bottom.
        byte rimTop = (byte)(150 * m.GlassRim), rimBot = (byte)(28 * m.GlassRim);
        using (var rim = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.3f * scale,
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, bot),
                new[] { SKColors.White.WithAlpha(rimTop), SKColors.White.WithAlpha(0), SKColors.White.WithAlpha(rimBot) },
                new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp),
        })
            canvas.DrawPath(body, rim);
    }
}
