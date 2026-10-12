using System;
using System.Collections.Generic;
using System.IO;
using Islet.Core;
using Islet.Rendering;
using SkiaSharp;

namespace Islet;

/// <summary>
/// Renders a scripted motion-design showcase to PNG frames using the REAL island engine
/// (IslandController springs + IslandRenderer), over an animated background with captions.
/// Encode the frames to video with ffmpeg. Invoked via: --showcase &lt;dir&gt; [seconds] [fps].
/// </summary>
internal static class Showcase
{
    public static void Run(string outDir, double seconds = 15.0, int fps = 60, int W = 1280, int H = 720)
    {
        Directory.CreateDirectory(outDir);
        var cfg = new IsletConfig();
        var ctl = new IslandController(cfg);
        var renderer = new IslandRenderer();
        const float scale = 2.0f;
        double dt = 1.0 / fps;
        int total = (int)Math.Round(seconds * fps);
        float centerY = H * 0.36f;

        byte[] art = GradientArt(128, 128, new SKColor(0xFF, 0x5E, 0x3A), new SKColor(0x8E, 0x2D, 0xE0));

        ActivitySnapshot Media(double p) => new()
        {
            Title = "Midnight City", Subtitle = "M83 · SoundCloud",
            Glyph = Glyphs.Music, CompactLeading = Glyphs.Music, CompactTrailing = Glyphs.Pause,
            Visual = VisualKind.Bar, Progress = p, AccentHex = "#FF5E3A",
            ImageBgra = art, ImageW = 128, ImageH = 128,
            ShowTransport = true, IsPlaying = true, CanSeek = true, CanNext = true, CanPrevious = true,
        };
        var battery = new ActivitySnapshot
        {
            Title = "Charging", Subtitle = "80%", Visual = VisualKind.Ring, Progress = 0.8,
            Glyph = Glyphs.BatteryCharging, CompactLeading = Glyphs.Bolt, CompactTrailing = "80%", AccentHex = "#30D158",
        };

        // live progress feed while media is on screen
        bool mediaLive = false; double mediaT0 = 0;
        string askTarget = "weather in Tokyo";

        var actions = new List<(double t, Action act)>
        {
            (0.8,  () => { ctl.ShowContent(Media(0.28), IslandMode.Compact); mediaLive = true; mediaT0 = 0.8; }),
            (2.4,  () => ctl.ShowContent(Media(0.33), IslandMode.Expanded)),
            (4.6,  () => ctl.ShowContent(Media(0.5), IslandMode.Minimal)),
            (6.4,  () => { mediaLive = false; ctl.ShowContent(battery, IslandMode.Alert); }),
            (8.0,  () => { ctl.SetAsk(true); }),
            (11.7, () => { ctl.SetAsk(false); ctl.ShowContent(Media(0.74), IslandMode.Compact); mediaLive = true; mediaT0 = 11.7; }),
            (12.9, () => ctl.Clear()),
        };
        int ai = 0;

        var info = new SKImageInfo(W, H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var island = SKSurface.Create(info);
        using var outp = SKSurface.Create(info);
        var model = new RenderModel { CornerExponent = cfg.Layout.CornerExponent };

        for (int i = 0; i < total; i++)
        {
            double t = i * dt;
            while (ai < actions.Count && t >= actions[ai].t) { actions[ai].act(); ai++; }

            // Type the Ask query letter-by-letter.
            if (ctl.AskActive)
            {
                double at = t - 8.2;
                int n = (int)Math.Clamp(at / 0.11, 0, askTarget.Length);
                ctl.SetAskText(askTarget[..n]);
            }
            if (mediaLive)
            {
                double p = 0.28 + (t - mediaT0) * 0.03;
                ctl.UpdateCurrent(Media(Math.Clamp(p, 0, 1)));
            }

            ctl.Update(dt);

            model.SurfaceW = W; model.SurfaceH = H; model.Scale = scale;
            ctl.BuildModel(model);
            float hpx = (float)model.Dp.Height * scale;
            model.AnchorTopPx = centerY - hpx / 2f;
            renderer.Draw(island.Canvas, model);

            var c = outp.Canvas;
            DrawBackground(c, t, W, H);
            using (var img = island.Snapshot()) c.DrawImage(img, 0, 0);
            DrawCaptions(c, t, W, H, centerY, hpx);
            DrawEndCard(c, t, seconds, W, H);

            using var snap = outp.Snapshot();
            using var data = snap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(outDir, $"f{i:D4}.png"), data.ToArray());
        }
        Console.WriteLine($"showcase: wrote {total} frames to {outDir} ({W}x{H}@{fps})");
    }

    // __APPEND_SHOW__

    private static float Clamp01(double v) => (float)(v < 0 ? 0 : v > 1 ? 1 : v);
    private static float Smooth(float x) => x * x * (3 - 2 * x);

    private static float Seg(double t, double a, double b)
    {
        if (t < a || t > b) return 0;
        double half = (b - a) / 2;
        double inL = Math.Min(0.28, half), outL = Math.Min(0.34, half);
        double f = 1;
        if (t < a + inL) f = (t - a) / inL;
        else if (t > b - outL) f = (b - t) / outL;
        return Smooth(Clamp01(f));
    }

    private static void Blob(SKCanvas c, float x, float y, float r, SKColor col, byte a)
    {
        using var sh = SKShader.CreateRadialGradient(new SKPoint(x, y), r,
            new[] { col.WithAlpha(a), col.WithAlpha(0) }, new[] { 0f, 1f }, SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = sh, BlendMode = SKBlendMode.Plus, IsAntialias = true };
        c.DrawCircle(x, y, r, p);
    }

    private static void DrawBackground(SKCanvas c, double t, int W, int H)
    {
        using (var p = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, H),
                new[] { new SKColor(0x12, 0x14, 0x1C), new SKColor(0x05, 0x06, 0x0A) }, null, SKShaderTileMode.Clamp)
        })
            c.DrawRect(0, 0, W, H, p);

        float ft = (float)t;
        Blob(c, W * 0.30f + 70 * MathF.Sin(ft * 0.5f), H * 0.30f + 46 * MathF.Cos(ft * 0.40f), W * 0.34f, new SKColor(0x8E, 0x2D, 0xE0), 60);
        Blob(c, W * 0.72f + 60 * MathF.Sin(ft * 0.35f + 1), H * 0.33f + 54 * MathF.Cos(ft * 0.5f + 2), W * 0.30f, new SKColor(0x2B, 0xD4, 0xFF), 52);
        Blob(c, W * 0.52f + 80 * MathF.Sin(ft * 0.27f + 3), H * 0.66f + 44 * MathF.Cos(ft * 0.33f), W * 0.38f, new SKColor(0xFF, 0x5E, 0x8A), 46);

        using (var v = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(W / 2f, H / 2f), W * 0.72f,
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, 170) }, new[] { 0.5f, 1f }, SKShaderTileMode.Clamp)
        })
            c.DrawRect(0, 0, W, H, v);
    }

    // __APPEND_SHOW2__

    private static readonly (double a, double b, string title, string sub)[] Caps =
    {
        (0.8, 2.4, "Compact", "one glance, one tap"),
        (2.4, 4.6, "Expanded", "a rich live activity"),
        (4.6, 6.4, "Minimal · gooey split", "two activities, liquid metaball"),
        (6.4, 8.0, "Alert", "events surface themselves"),
        (8.0, 11.6, "Ask", "“Search or Ask”, glowing"),
    };

    private static void DrawCaptions(SKCanvas c, double t, int W, int H, float centerY, float hpx)
    {
        float baseY = centerY + hpx / 2f + 64;
        foreach (var (a, b, title, sub) in Caps)
        {
            float f = Seg(t, a, b);
            if (f <= 0.001f) continue;
            byte ta = (byte)(255 * f), sa = (byte)(170 * f);
            float rise = (1 - f) * 10;
            CenterText(c, title, W / 2f, baseY + rise, 34, new SKColor(245, 246, 252, ta), semibold: true);
            CenterText(c, sub, W / 2f, baseY + 30 + rise, 17, new SKColor(170, 176, 196, sa), semibold: false);
        }
    }

    private static void DrawEndCard(SKCanvas c, double t, double seconds, int W, int H)
    {
        double start = seconds - 2.4;
        if (t < start) return;
        float f = Smooth(Clamp01((t - start) / 0.9));
        byte wa = (byte)(255 * f), sa = (byte)(165 * f);
        float cy = H * 0.60f + (1 - f) * 12;
        CenterText(c, "Islet", W / 2f, cy, 92, new SKColor(255, 255, 255, wa), semibold: true);
        CenterText(c, "Live activities for Windows", W / 2f, cy + 50, 22, new SKColor(150, 196, 250, sa), semibold: false);
        using var dot = new SKPaint { IsAntialias = true, Color = new SKColor(0x5A, 0xC8, 0xFA, (byte)(220 * f)) };
        c.DrawCircle(W / 2f, cy - 70, 5f, dot);
    }

    private static void CenterText(SKCanvas c, string text, float cx, float baselineY, float size, SKColor color, bool semibold)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var p = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Typeface = semibold ? Typefaces.UiSemibold : Typefaces.Ui,
            TextSize = size,
            TextAlign = SKTextAlign.Center,
            SubpixelText = true,
        };
        c.DrawText(text, cx, baselineY, p);
    }

    private static byte[] GradientArt(int w, int h, SKColor c1, SKColor c2)
    {
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var cv = new SKCanvas(bmp))
        using (var p = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w, h), new[] { c1, c2 }, null, SKShaderTileMode.Clamp)
        })
            cv.DrawRect(0, 0, w, h, p);
        return bmp.Bytes;
    }
}
