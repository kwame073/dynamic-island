using System;

namespace Islet.Core;

/// <summary>Relative importance; higher priority activities win the compact slot and can self-expand.</summary>
public enum ActivityPriority { Low, Normal, High, Alert }

/// <summary>Hint for how the renderer should visualize <see cref="ActivitySnapshot.Progress"/>.</summary>
public enum VisualKind { None, Bar, Ring, Equalizer, Pulse }

/// <summary>
/// Immutable, renderer-agnostic description of what an activity wants to show right now.
/// The app's renderer interprets these fields for the compact / minimal / expanded templates,
/// so plugins stay free of any UI-framework dependency and remain unit-testable.
/// </summary>
public sealed record ActivitySnapshot
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public double? Progress { get; init; }           // 0..1
    public VisualKind Visual { get; init; } = VisualKind.None;
    public string? Glyph { get; init; }              // Segoe Fluent Icons glyph (own iconography, no SF Symbols)
    public string? CompactLeading { get; init; }     // glyph/short text for the left slot
    public string? CompactTrailing { get; init; }    // glyph/short text or number for the right slot
    public string? AccentHex { get; init; }          // activity glow color, e.g. "#5AC8FA"
    public float[]? Levels { get; init; }             // equalizer bars 0..1 (optional)

    // Optional raster artwork (album art etc.), premultiplied BGRA, top-down.
    public byte[]? ImageBgra { get; init; }
    public int ImageW { get; init; }
    public int ImageH { get; init; }

    // Media transport: when true, the expanded view shows prev / play-pause / next and a seekable bar.
    public bool ShowTransport { get; init; }
    public bool IsPlaying { get; init; }
    public bool CanSeek { get; init; }
    public bool CanNext { get; init; }
    public bool CanPrevious { get; init; }
}

/// <summary>Services the host exposes to activities.</summary>
public interface IActivityHost
{
    /// <summary>Ask the host to re-render (content changed).</summary>
    void Invalidate(ILiveActivity activity);
    /// <summary>Briefly auto-expand the island to surface an event, then collapse.</summary>
    void RequestAlert(ILiveActivity activity, double seconds);
}

/// <summary>
/// A content plugin. Implementations observe some Windows source (media session, battery, timer,
/// notifications…) and publish <see cref="ActivitySnapshot"/>s. Templates are produced by the
/// renderer from the snapshot; see <see cref="VisualKind"/> and the compact/trailing hints.
/// </summary>
public interface ILiveActivity
{
    string Id { get; }
    string DisplayName { get; }
    ActivityPriority Priority { get; }
    bool IsActive { get; }
    event Action? Changed;
    ActivitySnapshot Snapshot();
    void Start(IActivityHost host);
    void Stop();
}
