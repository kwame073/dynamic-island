using System.Threading;
using Islet.Core;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Islet;

/// <summary>Hides the island during fullscreen apps/games and on the lock screen.</summary>
internal sealed class SystemWatcher
{
    private readonly IsletConfig _cfg;
    private readonly IslandWindow _win;
    private Timer? _poll;
    private bool _locked;
    private bool _fullscreen;

    public SystemWatcher(IsletConfig cfg, IslandWindow win) { _cfg = cfg; _win = win; }

    public void Start()
    {
        if (_cfg.Behavior.HideOnLock)
            SystemEvents.SessionSwitch += OnSession;
        if (_cfg.Behavior.HideOnFullscreen)
            _poll = new Timer(_ => PollFullscreen(), null, 600, 1000);
        Apply();
    }

    public void Stop()
    {
        _poll?.Dispose();
        SystemEvents.SessionSwitch -= OnSession;
    }

    private void OnSession(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect)
            _locked = true;
        else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect)
            _locked = false;
        Apply();
    }

    private void PollFullscreen()
    {
        try
        {
            PInvoke.SHQueryUserNotificationState(out var state);
            bool fs = state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
                            or QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                            or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
            if (fs != _fullscreen) { _fullscreen = fs; Apply(); }
        }
        catch { }
    }

    private void Apply() => _win.SetVisible(!(_locked || _fullscreen));
}
