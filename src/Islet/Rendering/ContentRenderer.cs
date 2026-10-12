using Islet.Core;
using SkiaSharp;

namespace Islet.Rendering;

/// <summary>Draws the per-mode content (already clipped to the island shape by the caller).</summary>
internal static class ContentRenderer
{
    public static void Draw(SKCanvas canvas, RenderModel m, SKRect bounds)
    {
        if (m.AskActive) { DrawAsk(canvas, m, bounds); return; }

        var layer = m.Current;
        // Fade/scale the outgoing content out first (blur + scale-down + fade).
        if (m.Outgoing is { } outg && outg.Alpha > 0.01f)
            DrawLayer(canvas, m, bounds, outg, interactive: false);
        if (layer is { } cur && cur.Alpha > 0.01f)
            DrawLayer(canvas, m, bounds, cur, interactive: true);
    }

    private static void DrawLayer(SKCanvas canvas, RenderModel m, SKRect bounds, ContentLayer layer, bool interactive)
    {
        int save = canvas.Save();
        // Scale transforms content about the island center for the blur/scale transition.
        canvas.Scale(layer.Scale, layer.Scale, bounds.MidX, bounds.MidY);

        byte a = (byte)(255 * IslandRenderer.Clamp01(layer.Alpha));
        var snap = layer.Snapshot;
        switch (m.Mode)
        {
            case IslandMode.Compact: DrawCompact(canvas, m, bounds, snap, a); break;
            case IslandMode.Minimal: DrawMinimal(canvas, m, bounds, snap, a); break;
            case IslandMode.Expanded: DrawExpanded(canvas, m, bounds, snap, a, interactive); break;
            case IslandMode.Alert: DrawExpanded(canvas, m, bounds, snap, a, false); break;
            default: break; // Idle: empty
        }
        canvas.RestoreToCount(save);
    }

    private static SKColor Accent(ActivitySnapshot s, byte a)
    {
        if (!string.IsNullOrEmpty(s.AccentHex) && SKColor.TryParse(s.AccentHex, out var c))
            return c.WithAlpha(a);
        return new SKColor(255, 255, 255, a);
    }

    private static void DrawGlyph(SKCanvas c, string glyph, float cx, float cy, float size, SKColor color)
    {
        using var p = new SKPaint { IsAntialias = true, Color = color, Typeface = Typefaces.Icons, TextSize = size, TextAlign = SKTextAlign.Center };
        var fm = p.FontMetrics;
        float baseline = cy - (fm.Ascent + fm.Descent) / 2f;
        c.DrawText(glyph, cx, baseline, p);
    }

    private static void DrawText(SKCanvas c, string text, float x, float y, float size, SKColor color, SKTextAlign align = SKTextAlign.Left, bool semibold = false)
    {
        using var p = new SKPaint { IsAntialias = true, Color = color, Typeface = semibold ? Typefaces.UiSemibold : Typefaces.Ui, TextSize = size, TextAlign = align };
        c.DrawText(text ?? "", x, y, p);
    }

    private static bool IsGlyph(string? s) => !string.IsNullOrEmpty(s) && s.Length <= 2 && s[0] >= ''; //''; //'';

    private static void DrawSlot(SKCanvas c, string? content, float cx, float cy, float size, SKColor color)
    {
        if (string.IsNullOrEmpty(content)) return;
        if (IsGlyph(content)) DrawGlyph(c, content, cx, cy, size, color);
        else DrawText(c, content, cx, cy - size * -0.35f, size * 0.9f, color, SKTextAlign.Center, semibold: true);
    }

    private static void DrawCompact(SKCanvas c, RenderModel m, SKRect b, ActivitySnapshot s, byte a)
    {
        float h = b.Height;
        float cy = b.MidY;
        var color = Accent(s, a);
        float lx = b.Left + h * 0.5f;
        float rx = b.Right - h * 0.5f;

        DrawSlot(c, s.CompactLeading ?? s.Glyph, lx, cy, h * 0.46f, color);

        if (s.Visual == VisualKind.Ring && s.Progress is double pr)
            DrawRing(c, rx, cy, h * 0.32f, (float)pr, color, h * 0.12f);
        else
            DrawSlot(c, s.CompactTrailing, rx, cy, h * 0.42f, color);
    }

    private static void DrawMinimal(SKCanvas c, RenderModel m, SKRect b, ActivitySnapshot s, byte a)
    {
        // Content shown inside the main pill; the detached blob is drawn by MetaballLayer.
        var color = Accent(s, a);
        DrawSlot(c, s.Glyph ?? s.CompactLeading, b.MidX, b.MidY, b.Height * 0.46f, color);
    }

    private static void DrawRing(SKCanvas c, float cx, float cy, float radius, float progress, SKColor color, float stroke)
    {
        using var track = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = color.WithAlpha((byte)(color.Alpha * 0.28f)) };
        c.DrawCircle(cx, cy, radius, track);
        using var arc = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = color, StrokeCap = SKStrokeCap.Round };
        using var path = new SKPath();
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        path.AddArc(rect, -90, 360f * IslandRenderer.Clamp01(progress));
        c.DrawPath(path, arc);
    }

    private static void DrawExpanded(SKCanvas c, RenderModel m, SKRect b, ActivitySnapshot s, byte a, bool interactive)
    {
        float sc = m.Scale;
        float pad = 18 * sc;
        var white = new SKColor(255, 255, 255, a);
        var dim = new SKColor(255, 255, 255, (byte)(a * 0.62f));
        var accent = Accent(s, a);

        float box = MathF.Min(b.Height - 2 * pad, 64 * sc);
        var boxRect = new SKRect(b.Left + pad, b.Top + pad, b.Left + pad + box, b.Top + pad + box);

        if (s.ImageBgra != null && s.ImageW > 0 && s.ImageH > 0)
            DrawImage(c, s.ImageBgra, s.ImageW, s.ImageH, boxRect, 12 * sc, a);
        else
        {
            using var chip = new SKPaint { IsAntialias = true, Color = accent.WithAlpha((byte)(a * 0.18f)) };
            c.DrawRoundRect(boxRect, 12 * sc, 12 * sc, chip);
            DrawGlyph(c, s.Glyph ?? "", boxRect.MidX, boxRect.MidY, box * 0.5f, accent);
        }

        float tx = boxRect.Right + 14 * sc;
        float titleSize = 17 * sc, subSize = 13 * sc;
        DrawText(c, s.Title, tx, b.Top + pad + titleSize, titleSize, white, SKTextAlign.Left, semibold: true);
        if (!string.IsNullOrEmpty(s.Subtitle))
            DrawText(c, s.Subtitle, tx, b.Top + pad + titleSize + subSize + 6 * sc, subSize, dim);

        if (s.ShowTransport)
        {
            DrawTransport(c, m, b, s, a, sc, accent, white, interactive);
            return;
        }

        if (s.Visual == VisualKind.Bar && s.Progress is double p)
        {
            float barY = b.Bottom - pad - 6 * sc;
            DrawBar(c, tx, b.Right - pad, barY, (float)p, accent, 5 * sc);
        }
        else if (s.Visual == VisualKind.Equalizer && s.Levels is { Length: > 0 })
        {
            DrawEqualizer(c, s.Levels, new SKRect(tx, b.Bottom - pad - 22 * sc, b.Right - pad, b.Bottom - pad), accent);
        }
    }

    private static void DrawTransport(SKCanvas c, RenderModel m, SKRect b, ActivitySnapshot s, byte a, float sc, SKColor accent, SKColor white, bool interactive)
    {
        float pad = 18 * sc;
        float cx = b.MidX;
        float by = b.Bottom - pad - 10 * sc;   // seek bar
        float ty = by - 34 * sc;               // transport row center
        float gap = 48 * sc;

        var prevCol = s.CanPrevious ? accent : accent.WithAlpha((byte)(a * 0.28f));
        var nextCol = s.CanNext ? accent : accent.WithAlpha((byte)(a * 0.28f));
        DrawGlyph(c, Glyphs.Previous, cx - gap, ty, 22 * sc, prevCol);
        DrawGlyph(c, s.IsPlaying ? Glyphs.Pause : Glyphs.Play, cx, ty, 30 * sc, accent);
        DrawGlyph(c, Glyphs.Next, cx + gap, ty, 22 * sc, nextCol);

        float x0 = b.Left + pad, x1 = b.Right - pad;
        float frac = IslandRenderer.Clamp01((float)(s.Progress ?? 0));
        float xk = x0 + (x1 - x0) * frac;
        using (var track = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * sc, StrokeCap = SKStrokeCap.Round, Color = white.WithAlpha((byte)(a * 0.22f)) })
            c.DrawLine(x0, by, x1, by, track);
        if (xk > x0 + 0.5f)
            using (var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * sc, StrokeCap = SKStrokeCap.Round, Color = accent })
                c.DrawLine(x0, by, xk, by, fill);
        using (var knob = new SKPaint { IsAntialias = true, Color = white })
            c.DrawCircle(xk, by, 6 * sc, knob);

        if (interactive)
        {
            float r = 22 * sc;
            m.HitZones.Add(new HitZone(cx - gap - r, ty - r, cx - gap + r, ty + r, "media.prev"));
            m.HitZones.Add(new HitZone(cx - r, ty - r, cx + r, ty + r, "media.playpause"));
            m.HitZones.Add(new HitZone(cx + gap - r, ty - r, cx + gap + r, ty + r, "media.next"));
            m.HitZones.Add(new HitZone(x0, by - 14 * sc, x1, by + 14 * sc, "media.seek"));
        }
    }

    private static void DrawBar(SKCanvas c, float x0, float x1, float y, float progress, SKColor color, float thickness)
    {
        using var track = new SKPaint { IsAntialias = true, Color = color.WithAlpha((byte)(color.Alpha * 0.26f)), StrokeCap = SKStrokeCap.Round, StrokeWidth = thickness, Style = SKPaintStyle.Stroke };
        c.DrawLine(x0, y, x1, y, track);
        using var fill = new SKPaint { IsAntialias = true, Color = color, StrokeCap = SKStrokeCap.Round, StrokeWidth = thickness, Style = SKPaintStyle.Stroke };
        float xp = x0 + (x1 - x0) * IslandRenderer.Clamp01(progress);
        if (xp > x0 + 0.5f) c.DrawLine(x0, y, xp, y, fill);
    }

    private static void DrawEqualizer(SKCanvas c, float[] levels, SKRect r, SKColor color)
    {
        int n = levels.Length;
        float gap = 3f, bw = (r.Width - gap * (n - 1)) / n;
        using var p = new SKPaint { IsAntialias = true, Color = color };
        for (int i = 0; i < n; i++)
        {
            float hgt = MathF.Max(2f, r.Height * IslandRenderer.Clamp01(levels[i]));
            float x = r.Left + i * (bw + gap);
            var bar = new SKRect(x, r.Bottom - hgt, x + bw, r.Bottom);
            c.DrawRoundRect(bar, bw / 2, bw / 2, p);
        }
    }

    private static void DrawAsk(SKCanvas c, RenderModel m, SKRect b)
    {
        float sc = m.Scale;
        float pad = 20 * sc;
        var glow = new SKColor(m.AskGlowArgb);
        var white = new SKColor(255, 255, 255, 255);
        var dim = new SKColor(220, 222, 235, 150);

        float searchSize = 19 * sc;
        float gx = b.Left + pad + searchSize * 0.5f;
        DrawGlyph(c, "", gx, b.MidY, searchSize, dim);  // Segoe Fluent "Search"

        float tx = gx + searchSize * 0.5f + 12 * sc;
        float textSize = 18 * sc;
        bool empty = string.IsNullOrEmpty(m.AskText);
        string shown = empty ? m.AskPlaceholder : m.AskText;
        float baseline = b.MidY + textSize * 0.34f;
        DrawText(c, shown, tx, baseline, textSize, empty ? dim : white, SKTextAlign.Left);

        // Blinking, glowing caret positioned after the typed text.
        using var meas = new SKPaint { Typeface = Typefaces.Ui, TextSize = textSize };
        float textW = empty ? 0 : meas.MeasureText(m.AskText);
        float caretX = tx + textW + 2 * sc;
        float caretH = textSize * 1.15f;
        var caret = new SKRect(caretX, b.MidY - caretH / 2, caretX + MathF.Max(1.5f, 2f * sc), b.MidY + caretH / 2);
        byte ca = (byte)(255 * IslandRenderer.Clamp01(m.CaretOpacity));
        using (var g = new SKPaint { IsAntialias = true, Color = glow.WithAlpha((byte)(ca * 0.9f)), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f * sc) })
            c.DrawRoundRect(caret, 2 * sc, 2 * sc, g);
        using (var s = new SKPaint { IsAntialias = true, Color = new SKColor(235, 236, 255, ca) })
            c.DrawRoundRect(caret, 2 * sc, 2 * sc, s);
    }

    private static void DrawImage(SKCanvas c, byte[] bgra, int w, int h, SKRect dest, float radius, byte alpha)
    {
        try
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var img = SKImage.FromPixelCopy(info, bgra);
            int save = c.Save();
            using (var clip = new SKPath())
            {
                clip.AddRoundRect(dest, radius, radius);
                c.ClipPath(clip, antialias: true);
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High, Color = SKColors.White.WithAlpha(alpha) };
                c.DrawImage(img, dest, paint);
            }
            c.RestoreToCount(save);
        }
        catch { /* malformed artwork: skip */ }
    }
}
