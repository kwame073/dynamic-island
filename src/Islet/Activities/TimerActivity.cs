using System;
using System.Threading;
using Islet.Core;
using Islet.Rendering;

namespace Islet.Activities;

/// <summary>A countdown timer / stopwatch live activity. Start it from the tray.</summary>
internal sealed class TimerActivity : ILiveActivity
{
    public string Id => "timer";
    public string DisplayName => "Timer";
    public ActivityPriority Priority => ActivityPriority.High;
    public event Action? Changed;

    private IActivityHost? _host;
    private Timer? _tick;
    private bool _running;
    private bool _finished;
    private DateTime _endUtc;
    private TimeSpan _duration;

    public void Start(IActivityHost host) => _host = host;

    public void Stop() { _tick?.Dispose(); _running = false; }

    public void StartCountdown(TimeSpan duration)
    {
        _duration = duration;
        _endUtc = DateTime.UtcNow + duration;
        _running = true;
        _finished = false;
        _tick?.Dispose();
        _tick = new Timer(_ => OnTick(), null, 0, 500);
        _host?.RequestAlert(this, 2.0);
        Changed?.Invoke();
    }

    public void Cancel()
    {
        _running = false;
        _finished = false;
        _tick?.Dispose();
        Changed?.Invoke();
    }

    private void OnTick()
    {
        if (!_running) return;
        if (!_finished && DateTime.UtcNow >= _endUtc)
        {
            _finished = true;
            _host?.RequestAlert(this, 4.0); // ring/flash on completion
        }
        Changed?.Invoke();
    }

    public bool IsActive => _running;

    public ActivitySnapshot Snapshot()
    {
        var remaining = _endUtc - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        double frac = _duration.TotalSeconds > 0.5 ? remaining.TotalSeconds / _duration.TotalSeconds : 0;

        string mmss = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";

        return new ActivitySnapshot
        {
            Title = _finished ? "Time's Up" : "Timer",
            Subtitle = mmss,
            Progress = Math.Clamp(frac, 0, 1),
            Visual = VisualKind.Ring,
            Glyph = Glyphs.Timer,
            CompactLeading = Glyphs.Timer,
            CompactTrailing = mmss,
            AccentHex = _finished ? "#FF9F0A" : "#FFD60A",
        };
    }
}
