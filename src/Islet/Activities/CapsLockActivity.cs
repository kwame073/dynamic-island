using System;
using System.Threading;
using Islet.Core;
using Islet.Interop;
using Islet.Rendering;

namespace Islet.Activities;

/// <summary>Caps Lock HUD: flashes briefly when the key toggles (like a system HUD).</summary>
internal sealed class CapsLockActivity : ILiveActivity
{
    private const int VK_CAPITAL = 0x14;

    public string Id => "capslock";
    public string DisplayName => "Caps Lock";
    public ActivityPriority Priority => ActivityPriority.Alert;
    public event Action? Changed;

    private IActivityHost? _host;
    private Timer? _poll;
    private bool _on;
    private DateTime _showUntil = DateTime.MinValue;

    public void Start(IActivityHost host)
    {
        _host = host;
        _on = (Native.GetKeyState(VK_CAPITAL) & 1) != 0;
        _poll = new Timer(_ => Poll(), null, 150, 150);
    }

    public void Stop() => _poll?.Dispose();

    private void Poll()
    {
        bool on = (Native.GetKeyState(VK_CAPITAL) & 1) != 0;
        if (on != _on)
        {
            _on = on;
            _showUntil = DateTime.UtcNow.AddMilliseconds(1400);
            _host?.RequestAlert(this, 1.4);
            Changed?.Invoke();
        }
    }

    public bool IsActive => DateTime.UtcNow < _showUntil;

    public ActivitySnapshot Snapshot() => new()
    {
        Title = _on ? "Caps Lock On" : "Caps Lock Off",
        Glyph = Glyphs.CapsLock,
        CompactLeading = Glyphs.CapsLock,
        CompactTrailing = _on ? "ON" : "OFF",
        AccentHex = _on ? "#64D2FF" : "#8E8E93",
    };
}
