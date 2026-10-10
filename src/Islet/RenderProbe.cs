using System;
using System.IO;
using Islet.Core;
using Islet.Rendering;
using SkiaSharp;

namespace Islet;

/// <summary>
/// Headless renderer self-check: draws each island state to a PNG over a neutral backdrop so the
/// visuals can be inspected without a live window. Invoked via <c>--render-probe &lt;dir&gt;</c>.
/// </summary>
internal static class RenderProbe
{
    public static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var cfg = new IsletConfig();
        var renderer = new IslandRenderer();

        Render(outDir, "01-idle", cfg, renderer, m => { m.Mode = IslandMode.Idle; m.Dp = cfg.BaseMetrics(IslandMode.Idle); m.GlowStrength = 0.22f; });

        Render(outDir, "02-compact", cfg, renderer, m =>
        {
            m.Mode = IslandMode.Compact; m.Dp = cfg.BaseMetrics(IslandMode.Compact);
            m.GlowArgb = 0xFFFF2D55; m.Current = Layer(new ActivitySnapshot
            { CompactLeading = Glyphs.Play, Visual = VisualKind.Ring, Progress = 0.62, AccentHex = "#FF2D55" });
        });

        Render(outDir, "03-minimal-mid", cfg, renderer, m =>
        {
            m.Mode = IslandMode.Minimal; m.Dp = cfg.BaseMetrics(IslandMode.Minimal);
            m.MinimalSeparation = 0.55f; m.MinimalBlobDp = (float)cfg.Layout.MinimalBlobDp; m.MinimalGapDp = (float)cfg.Layout.MinimalGapDp;
            m.Current = Layer(new ActivitySnapshot { Glyph = Glyphs.Music, AccentHex = "#30D158" });
        });

        Render(outDir, "04-minimal-split", cfg, renderer, m =>
        {
            m.Mode = IslandMode.Minimal; m.Dp = cfg.BaseMetrics(IslandMode.Minimal);
            m.MinimalSeparation = 1.0f; m.MinimalBlobDp = (float)cfg.Layout.MinimalBlobDp; m.MinimalGapDp = (float)cfg.Layout.MinimalGapDp;
            m.Current = Layer(new ActivitySnapshot { Glyph = Glyphs.Music, AccentHex = "#30D158" });
        });

        Render(outDir, "05-expanded", cfg, renderer, m =>
        {
            m.Mode = IslandMode.Expanded; m.Dp = cfg.BaseMetrics(IslandMode.Expanded);
            m.GlowArgb = 0xFF5AC8FA;
            m.Current = Layer(new ActivitySnapshot
            { Title = "Islet Session", Subtitle = "SkiaSharp · 120 Hz", Glyph = Glyphs.Music, Visual = VisualKind.Bar, Progress = 0.42, AccentHex = "#5AC8FA" });
        });

        Render(outDir, "06-ask", cfg, renderer, m =>
        {
            m.Mode = IslandMode.Ask; m.Dp = cfg.BaseMetrics(IslandMode.Ask);
            m.AskActive = true; m.AskText = "weather tod"; m.CaretOpacity = 1f; m.BorderSweepPhase = 0.32f;
        });

        Console.WriteLine($"Render probe written to {outDir}");
    }

    // __APPEND_PROBE__

    private static ContentLayer Layer(ActivitySnapshot s) => new() { Snapshot = s, Alpha = 1f, Scale = 1f };

    private static void Render(string outDir, string name, IsletConfig cfg, IslandRenderer renderer, Action<RenderModel> configure)
    {
        const int W = 1000, H = 380;
        const float scale = 2.0f;

        var model = new RenderModel
        {
            SurfaceW = W,
            SurfaceH = H,
            Scale = scale,
            CornerExponent = cfg.Layout.CornerExponent,
            GlowRadiusPx = (float)cfg.Layout.GlowRadiusDp,
            GlowStrength = (float)cfg.Layout.GlowStrength,
        };
        configure(model);

        float h = (float)model.Dp.Height * scale;
        model.AnchorTopPx = H / 2f - h / 2f;

        var info = new SKImageInfo(W, H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var island = SKSurface.Create(info);
        renderer.Draw(island.Canvas, model);
        using var img = island.Snapshot();

        using var outSurf = SKSurface.Create(info);
        var c = outSurf.Canvas;
        using (var bg = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, H),
                new[] { new SKColor(0x2A, 0x2C, 0x34), new SKColor(0x12, 0x12, 0x16) }, null, SKShaderTileMode.Clamp)
        })
            c.DrawRect(0, 0, W, H, bg);
        c.DrawImage(img, 0, 0);

        using var snap = outSurf.Snapshot();
        using var data = snap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), data.ToArray());
    }
}
