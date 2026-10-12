using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Islet.Core;

namespace Islet.Activities;

/// <summary>
/// Chooses which activity (or pair) is shown and in what mode, then drives the controller.
/// Acts as the <see cref="IActivityHost"/> for all providers. Recompute runs on the render thread
/// (posted through the window) so controller access stays single-threaded.
/// </summary>
internal sealed class ActivityManager : IActivityHost
{
    private readonly IsletConfig _cfg;
    private readonly IslandController _ctl;
    private readonly IslandWindow _win;
    private readonly List<ILiveActivity> _activities = new();

    private string? _lastKey;
    private bool _hovering, _clickExpanded;
    private ILiveActivity? _alert;
    private Timer? _alertRevert;

    public TimerActivity Timer { get; } = new();
    public MediaActivity Media { get; } = new();

    public ActivityManager(IsletConfig cfg, IslandController ctl, IslandWindow win)
    {
        _cfg = cfg; _ctl = ctl; _win = win;
    }

    public void Start()
    {
        _activities.Add(Media);
        _activities.Add(new BatteryActivity());
        _activities.Add(new VolumeActivity());
        _activities.Add(new RecordingActivity());
        _activities.Add(new CapsLockActivity());
        _activities.Add(Timer);

        foreach (var a in _activities)
        {
            var captured = a;
            a.Changed += () => _win.Post(Recompute);
            try { a.Start(this); } catch { }
        }
        _win.Post(Recompute);
    }

    public void Stop() { foreach (var a in _activities) { try { a.Stop(); } catch { } } }

    private List<ILiveActivity> ActiveSorted() =>
        _activities.Where(a => { try { return a.IsActive; } catch { return false; } })
                   .OrderByDescending(a => (int)a.Priority).ToList();

    private void Recompute()
    {
        if (_ctl.AskActive) return; // Ask owns the island until dismissed

        // An in-flight alert takes over as a banner.
        if (_alert != null && SafeActive(_alert))
        {
            Push(_alert, IslandMode.Alert);
            return;
        }

        var active = ActiveSorted();
        if (active.Count == 0) { _ctl.Clear(); _lastKey = null; return; }

        var primary = active[0];
        IslandMode mode = (_hovering || _clickExpanded) ? IslandMode.Expanded
                        : active.Count >= 2 ? IslandMode.Minimal
                        : IslandMode.Compact;
        Push(primary, mode);
    }

    private void Push(ILiveActivity a, IslandMode mode)
    {
        var snap = a.Snapshot();
        string key = $"{a.Id}|{mode}";
        if (key == _lastKey) _ctl.UpdateCurrent(snap);
        else { _ctl.ShowContent(snap, mode); _lastKey = key; }
    }

    private static bool SafeActive(ILiveActivity a) { try { return a.IsActive; } catch { return false; } }

    // __APPEND_MGR__

    // ---- User interaction (wired from the window; posted onto the render thread) ----

    public void OnHover(bool hovering) => _win.Post(() => { _hovering = hovering; Recompute(); });

    public void OnClick() => _win.Post(() =>
    {
        if (_ctl.AskActive) return;
        _clickExpanded = !_clickExpanded;
        Recompute();
    });

    public void ToggleAsk() => _win.Post(() =>
    {
        bool turningOn = !_ctl.AskActive;
        _ctl.SetAsk(turningOn);
        if (!turningOn) Recompute();
    });

    /// <summary>Route a media transport/seek click from the expanded view to the media activity.</summary>
    public void OnMediaCommand(string id, float fraction) => _win.Post(() =>
    {
        switch (id)
        {
            case "media.playpause": Media.TogglePlayPause(); break;
            case "media.next": Media.Next(); break;
            case "media.prev": Media.Previous(); break;
            case "media.seek": Media.SeekFraction(fraction); break;
        }
    });

    // ---- IActivityHost ----

    public void Invalidate(ILiveActivity activity) => _win.Post(Recompute);

    public void Refresh() => _win.Post(Recompute);

    public void RequestAlert(ILiveActivity activity, double seconds) => _win.Post(() =>
    {
        _alert = activity;
        Recompute();
        _alertRevert?.Dispose();
        _alertRevert = new Timer(_ => _win.Post(() =>
        {
            if (ReferenceEquals(_alert, activity)) { _alert = null; Recompute(); }
        }), null, (int)(seconds * 1000), System.Threading.Timeout.Infinite);
    });
}
