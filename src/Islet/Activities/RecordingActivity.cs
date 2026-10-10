using System;
using System.Threading;
using Islet.Core;
using Islet.Rendering;
using Microsoft.Win32;

namespace Islet.Activities;

/// <summary>
/// Privacy indicator: shows while any app is using the microphone or camera, read from the
/// CapabilityAccessManager consent store (a leaf with LastUsedTimeStop == 0 is currently in use).
/// </summary>
internal sealed class RecordingActivity : ILiveActivity
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public string Id => "recording";
    public string DisplayName => "Mic / Camera";
    public ActivityPriority Priority => ActivityPriority.High;
    public event Action? Changed;

    private IActivityHost? _host;
    private Timer? _poll;
    private bool _mic, _cam;

    public void Start(IActivityHost host)
    {
        _host = host;
        _poll = new Timer(_ => Poll(), null, 500, 1000);
    }

    public void Stop() => _poll?.Dispose();

    private void Poll()
    {
        bool mic = InUse("microphone");
        bool cam = InUse("webcam");
        if (mic == _mic && cam == _cam) return;
        bool becameActive = (mic && !_mic) || (cam && !_cam);
        _mic = mic; _cam = cam;
        if (becameActive) _host?.RequestAlert(this, 2.0);
        Changed?.Invoke();
    }

    private static bool InUse(string capability)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey($@"{Root}\{capability}");
            if (root == null) return false;
            if (AnyLeafActive(root)) return true;
            using var nonPackaged = root.OpenSubKey("NonPackaged");
            return nonPackaged != null && AnyLeafActive(nonPackaged);
        }
        catch { return false; }
    }

    private static bool AnyLeafActive(RegistryKey parent)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            try
            {
                using var sub = parent.OpenSubKey(name);
                if (sub?.GetValue("LastUsedTimeStop") is long stop && stop == 0) return true;
            }
            catch { }
        }
        return false;
    }

    public bool IsActive => _mic || _cam;

    public ActivitySnapshot Snapshot()
    {
        bool cam = _cam;
        return new ActivitySnapshot
        {
            Title = cam ? (_mic ? "Camera & Mic" : "Camera On") : "Microphone On",
            Subtitle = "In use",
            Glyph = cam ? Glyphs.Camera : Glyphs.Microphone,
            CompactLeading = cam ? Glyphs.Camera : Glyphs.Microphone,
            CompactTrailing = cam && _mic ? "2" : "",
            AccentHex = cam ? "#FF3B30" : "#FF9F0A",
        };
    }
}
