using SkiaSharp;

namespace Islet.Rendering;

/// <summary>
/// Builds the "gooey" liquid connector for the Minimal state: a detached blob plus a pinched
/// neck toward the main pill. As <see cref="RenderModel.MinimalSeparation"/> grows 0→1 the neck
/// thins and finally breaks, approximating a metaball split/merge. Unioned with the pill by the renderer.
/// </summary>
internal static class MinimalShape
{
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static SKPath BuildGooey(RenderModel m, float cx, float cy, float w, float h)
    {
        float sc = m.Scale;
        float R = h / 2f;                              // pill cap radius
        float px = cx + w / 2f - R;                    // pill right-cap center
        float rb = m.MinimalBlobDp * 0.5f * sc;
        float gap = m.MinimalGapDp * sc;
        float sep = IslandRenderer.Clamp01(m.MinimalSeparation);

        float bx = px + R + rb * 0.15f + sep * (gap + R + rb * 0.35f);

        var path = new SKPath();
        path.AddCircle(bx, cy, rb);

        float dxx = bx - px;
        float reach = R + rb + gap;
        if (dxx < reach)
        {
            float t = IslandRenderer.Clamp01(dxx / reach);
            float kp = Lerp(0.95f, 0.16f, t);
            float kb = Lerp(0.95f, 0.16f, t);
            float neckHalf = Lerp(R * 0.85f, R * 0.14f, t);
            float midx = (px + bx) / 2f;

            float pTop = cy - R * kp, pBot = cy + R * kp;
            float bTop = cy - rb * kb, bBot = cy + rb * kb;

            using var neck = new SKPath();
            neck.MoveTo(px, pTop);
            neck.QuadTo(midx, cy - neckHalf, bx, bTop);
            neck.LineTo(bx, bBot);
            neck.QuadTo(midx, cy + neckHalf, px, pBot);
            neck.Close();

            var merged = path.Op(neck, SKPathOp.Union);
            path.Dispose();
            return merged;
        }
        return path;
    }
}
