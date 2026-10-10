using System;
using Islet.Core;
using Islet.Rendering;
using Windows.System.Power;

namespace Islet.Activities;

/// <summary>Charging / low-battery live activity backed by WinRT <see cref="PowerManager"/>.</summary>
internal sealed class BatteryActivity : ILiveActivity
{
    public string Id => "battery";
    public string DisplayName => "Battery";
    public ActivityPriority Priority => ActivityPriority.High;

    public event Action? Changed;

    private IActivityHost? _host;
    private bool _wasCharging;

    public void Start(IActivityHost host)
    {
        _host = host;
        PowerManager.BatteryStatusChanged += OnStatus;
        PowerManager.RemainingChargePercentChanged += OnPercent;
        PowerManager.PowerSupplyStatusChanged += OnStatus;
        _wasCharging = PowerManager.BatteryStatus == BatteryStatus.Charging;
    }

    public void Stop()
    {
        PowerManager.BatteryStatusChanged -= OnStatus;
        PowerManager.RemainingChargePercentChanged -= OnPercent;
        PowerManager.PowerSupplyStatusChanged -= OnStatus;
    }

    private void OnPercent(object? s, object e) => Changed?.Invoke();

    private void OnStatus(object? s, object e)
    {
        bool charging = PowerManager.BatteryStatus == BatteryStatus.Charging;
        if (charging && !_wasCharging && _host != null)
            _host.RequestAlert(this, 3.0); // plugging in briefly auto-expands
        _wasCharging = charging;
        Changed?.Invoke();
    }

    private static bool HasBattery =>
        PowerManager.BatteryStatus != BatteryStatus.NotPresent &&
        PowerManager.RemainingChargePercent is >= 0 and <= 100;

    public bool IsActive
    {
        get
        {
            if (!HasBattery) return false;
            bool charging = PowerManager.BatteryStatus == BatteryStatus.Charging;
            bool low = PowerManager.RemainingChargePercent <= 20 &&
                       PowerManager.BatteryStatus == BatteryStatus.Discharging;
            return charging || low;
        }
    }

    public ActivitySnapshot Snapshot()
    {
        int pct = Math.Clamp(PowerManager.RemainingChargePercent, 0, 100);
        bool charging = PowerManager.BatteryStatus == BatteryStatus.Charging;
        return new ActivitySnapshot
        {
            Title = charging ? "Charging" : "Low Battery",
            Subtitle = $"{pct}%",
            Progress = pct / 100.0,
            Visual = VisualKind.Ring,
            Glyph = charging ? Glyphs.BatteryCharging : Glyphs.Battery,
            CompactLeading = charging ? Glyphs.Bolt : Glyphs.Battery,
            CompactTrailing = $"{pct}%",
            AccentHex = charging ? "#30D158" : "#FF375F",
        };
    }
}
