namespace Islet.Core;

using System.Text.Json.Serialization;

/// <summary>
/// All user-tunable values. Numbers are device-independent pixels (dp @ 96 DPI) or ms.
/// Values marked "(estimate)" are read off reference video frame-by-frame, not official.
/// Serialized to %APPDATA%/Islet/config.json by the app layer.
/// </summary>
public sealed class IsletConfig
{
    public LayoutConfig Layout { get; set; } = new();
    public SpringConfig Springs { get; set; } = new();
    public TimingConfig Timings { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
    public ColorConfig Colors { get; set; } = new();

    /// <summary>Base (content-independent) target metrics for a mode.</summary>
    public IslandMetrics BaseMetrics(IslandMode mode) => mode switch
    {
        IslandMode.Hidden   => new(Layout.IdleW, Layout.IdleH, Layout.IdleH / 2),
        IslandMode.Idle     => new(Layout.IdleW, Layout.IdleH, Layout.IdleH / 2),
        IslandMode.Compact  => new(Layout.CompactW, Layout.CompactH, Layout.CompactH / 2),
        IslandMode.Minimal  => new(Layout.IdleW, Layout.IdleH, Layout.IdleH / 2),
        IslandMode.Expanded => new(Layout.ExpandedW, Layout.ExpandedH, Layout.ExpandedR),
        IslandMode.Alert    => new(Layout.AlertW, Layout.AlertH, Layout.AlertR),
        IslandMode.Ask      => new(Layout.AskW, Layout.AskH, Layout.AskH / 2),
        _                   => new(Layout.IdleW, Layout.IdleH, Layout.IdleH / 2),
    };
}

public sealed class LayoutConfig
{
    public double TopMarginDp { get; set; } = 11.0;   // iPhone 14 Pro reference
    public double CornerExponent { get; set; } = 4.0; // continuous squircle
    public double GlowRadiusDp { get; set; } = 20.0;
    public double GlowStrength { get; set; } = 0.9;

    public double IdleW { get; set; } = 126.0; public double IdleH { get; set; } = 37.0;
    public double CompactW { get; set; } = 188.0; public double CompactH { get; set; } = 37.0; // (estimate)
    public double ExpandedW { get; set; } = 372.0; public double ExpandedH { get; set; } = 160.0; public double ExpandedR { get; set; } = 44.0; // (estimate)
    public double AlertW { get; set; } = 320.0; public double AlertH { get; set; } = 84.0; public double AlertR { get; set; } = 30.0; // (estimate)
    public double AskW { get; set; } = 460.0; public double AskH { get; set; } = 48.0; // (estimate)

    public double MinimalGapDp { get; set; } = 10.0;      // gap between pill and detached blob
    public double MinimalBlobDp { get; set; } = 30.0;     // detached circle diameter
}

public sealed class SpringConfig
{
    public double GeometryResponse { get; set; } = 0.45; public double GeometryDamping { get; set; } = 0.76;
    public double ContentResponse { get; set; } = 0.40; public double ContentDamping { get; set; } = 0.82;
    public double SnappyResponse { get; set; } = 0.32; public double SnappyDamping { get; set; } = 0.86;

    [JsonIgnore] public SpringParams Geometry => new(GeometryResponse, GeometryDamping);
    [JsonIgnore] public SpringParams Content => new(ContentResponse, ContentDamping);
    [JsonIgnore] public SpringParams Snappy => new(SnappyResponse, SnappyDamping);
}

public sealed class TimingConfig
{
    public double ContentOutMs { get; set; } = 130;
    public double ContentInMs { get; set; } = 150;
    public double ContentInDelayMs { get; set; } = 60;
    public double AlertDwellMs { get; set; } = 3000;
    public double CursorBlinkMs { get; set; } = 560;
    public double BorderSweepMs { get; set; } = 2600;
}

public sealed class BehaviorConfig
{
    public string HotkeyModifiers { get; set; } = "Win+Alt"; // Win, Alt, Ctrl, Shift combos
    public string HotkeyKey { get; set; } = "Space";
    public bool HideOnFullscreen { get; set; } = true;
    public bool HideOnLock { get; set; } = true;
    public bool ExpandOnHover { get; set; } = true;
    public string PreferredMonitor { get; set; } = "Primary"; // Primary | WithCursor
    public string AskHandler { get; set; } = "WindowsSearch";  // WindowsSearch | Command | Http
    public string AskCommand { get; set; } = "";
    public string AskEndpoint { get; set; } = "";
}

public sealed class ColorConfig
{
    public string GlowDefault { get; set; } = "#5AC8FA";  // soft activity glow
    public string AskGlow { get; set; } = "#8E8CFF";      // Ask-mode border/caret glow
    public string Accent { get; set; } = "#FFFFFF";
}
