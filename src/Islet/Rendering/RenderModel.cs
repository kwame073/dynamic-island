using Islet.Core;

namespace Islet.Rendering;

/// <summary>Everything the renderer needs for one frame. Produced by the controller, consumed by the renderer.</summary>
internal sealed class RenderModel
{
    public float SurfaceW;
    public float SurfaceH;
    public float Scale = 1f;        // device pixels per dp
    public float AnchorTopPx;       // y of the island's top edge within the surface

    public IslandMetrics Dp;        // animated metrics in dp
    public IslandMode Mode = IslandMode.Idle;
    public double CornerExponent = 5.0;

    // Glow
    public uint GlowArgb = 0xFF5AC8FA;
    public float GlowRadiusPx = 20f;
    public float GlowStrength = 0.9f;

    // Minimal-state detached blob (gooey). 0 = merged, 1 = fully separated.
    public float MinimalSeparation;
    public float MinimalBlobDp;
    public float MinimalGapDp;

    // Ask-mode
    public bool AskActive;
    public string AskText = "";
    public string AskPlaceholder = "Search or Ask";
    public float CaretOpacity;       // 0..1 blinking caret
    public float BorderSweepPhase;   // 0..1 running border highlight position
    public uint AskGlowArgb = 0xFF8E8CFF;

    // Content transition (old -> new) 0..1
    public ContentLayer? Current;
    public ContentLayer? Outgoing;
}

/// <summary>A content snapshot plus its animated transition transform (blur/scale/fade).</summary>
internal sealed class ContentLayer
{
    public ActivitySnapshot Snapshot = new();
    public float Alpha = 1f;
    public float Scale = 1f;
    public float BlurPx = 0f;
}
