using System;
using System.Threading;
using Islet.Core;
using Islet.Interop;
using Islet.Rendering;

namespace Islet.Activities;

/// <summary>Volume HUD: flashes briefly when the system volume or mute changes (polled Core Audio).</summary>
internal sealed class VolumeActivity : ILiveActivity
{
    public string Id => "volume";
    public string DisplayName => "Volume";
    public ActivityPriority Priority => ActivityPriority.Alert;
    public event Action? Changed;

    private IActivityHost? _host;
    private Timer? _poll;
    private int _pct = -1;
    private bool _mute;
    private DateTime _showUntil = DateTime.MinValue;

    public void Start(IActivityHost host)
    {
        _host = host;
        if (CoreAudio.TryGetMasterVolume(out float s, out bool m)) { _pct = (int)Math.Round(s * 100); _mute = m; }
        _poll = new Timer(_ => Poll(), null, 150, 150);
    }

    public void Stop() => _poll?.Dispose();

    private void Poll()
    {
        if (!CoreAudio.TryGetMasterVolume(out float s, out bool m)) return;
        int pct = (int)Math.Round(s * 100);
        if (pct == _pct && m == _mute) return;
        _pct = pct; _mute = m;
        _showUntil = DateTime.UtcNow.AddMilliseconds(1300);
        _host?.RequestAlert(this, 1.3);
        Changed?.Invoke();
    }

    public bool IsActive => DateTime.UtcNow < _showUntil;

    public ActivitySnapshot Snapshot() => new()
    {
        Title = _mute ? "Muted" : "Volume",
        Subtitle = $"{_pct}%",
        Progress = _mute ? 0 : _pct / 100.0,
        Visual = VisualKind.Bar,
        Glyph = _mute ? Glyphs.VolumeMute : Glyphs.Volume,
        CompactLeading = _mute ? Glyphs.VolumeMute : Glyphs.Volume,
        CompactTrailing = $"{_pct}%",
        AccentHex = "#64D2FF",
    };
}
