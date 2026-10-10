namespace Islet.Core;

/// <summary>High-level visual state of the island. Mirrors the iOS-style state set.</summary>
public enum IslandMode
{
    /// <summary>Not shown at all (fullscreen app, game, or lock screen).</summary>
    Hidden,
    /// <summary>Empty black pill, nothing active.</summary>
    Idle,
    /// <summary>One activity: leading icon + trailing indicator, pill slightly wider.</summary>
    Compact,
    /// <summary>Two activities: one in the pill, a second split off as a circle (gooey).</summary>
    Minimal,
    /// <summary>Fully expanded card with leading/trailing/center/bottom regions.</summary>
    Expanded,
    /// <summary>An event briefly auto-expanded the island (call, timer, charging).</summary>
    Alert,
    /// <summary>Ask field: "Search or Ask" with glowing caret and running border.</summary>
    Ask,
}

/// <summary>Animatable island geometry in device-independent pixels (dp @ 96 DPI).</summary>
public readonly record struct IslandMetrics(double Width, double Height, double CornerRadius)
{
    public double HalfWidth => Width / 2.0;
}
