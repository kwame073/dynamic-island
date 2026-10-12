using Islet.Activities;
using Islet.Core;

namespace Islet;

/// <summary>Top-level orchestrator: owns config, controller, window and the activity manager, and wires input.</summary>
internal sealed class IsletHost
{
    private IsletConfig _cfg = new();
    private IslandController? _ctl;
    private IslandWindow? _win;
    private ActivityManager? _mgr;
    private SystemWatcher? _watcher;
    private AskInput? _ask;
    private TrayIcon? _tray;

    public void Start()
    {
        _cfg = ConfigStore.Load();
        ConfigStore.Save(_cfg); // materialize defaults for the user to tweak

        _ctl = new IslandController(_cfg);
        _win = new IslandWindow(_cfg, _ctl);
        _mgr = new ActivityManager(_cfg, _ctl, _win);

        _win.Clicked += () => _mgr!.OnClick();
        _win.HotkeyPressed += () => _mgr!.ToggleAsk();
        _win.HoverChanged += h => { if (_cfg.Behavior.ExpandOnHover) _mgr!.OnHover(h); };
        _win.MediaCommand += (id, f) => _mgr!.OnMediaCommand(id, f);

        _ask = new AskInput(_cfg, _ctl, _win, _mgr);
        _win.CharTyped += c => _ask!.OnChar(c);
        _win.KeyPressed += vk => _ask!.OnKey(vk);

        _win.Start();
        _mgr.Start();

        _watcher = new SystemWatcher(_cfg, _win);
        _watcher.Start();

        _tray = new TrayIcon(this, _mgr);
        _tray.Start();
    }

    public void ToggleAsk() => _ask?.Toggle();

    public void StartTimer(int seconds) => _mgr?.Timer.StartCountdown(System.TimeSpan.FromSeconds(seconds));

    public void OpenConfigFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigStore.Dir) { UseShellExecute = true }); }
        catch { }
    }

    public void Quit() => System.Windows.Application.Current?.Shutdown();

    public void Stop()
    {
        _tray?.Stop();
        _watcher?.Stop();
        _mgr?.Stop();
        _win?.Stop();
    }
}
