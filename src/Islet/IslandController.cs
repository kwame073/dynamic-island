using Islet.Core;
using Islet.Rendering;

namespace Islet;

/// <summary>
/// Owns the island's animated state: geometry springs, content cross-fade transitions, the
/// Minimal-split spring, and Ask-mode timers. Touched only on the render thread (the window
/// marshals external events in via a queue), so it needs no locking.
/// </summary>
internal sealed class IslandController
{
    private readonly IsletConfig _cfg;
    private readonly SpringValue _w, _h, _r, _sep;

    private IslandMode _mode = IslandMode.Idle;
    private IslandMode _contentMode = IslandMode.Idle; // mode the content wants (before Ask/alert overrides)

    private readonly ContentLayer _cur = new();
    private readonly ContentLayer _out = new();
    private bool _hasCur, _hasOut;
    private double _nowMs, _inStartMs, _outStartMs;

    private bool _ask;
    private string _askText = "";
    private double _caretMs, _sweepPhase;

    public IslandController(IsletConfig cfg)
    {
        _cfg = cfg;
        var m = cfg.BaseMetrics(IslandMode.Idle);
        _w = new SpringValue(m.Width, cfg.Springs.Geometry);
        _h = new SpringValue(m.Height, cfg.Springs.Geometry);
        _r = new SpringValue(m.CornerRadius, cfg.Springs.Geometry);
        _sep = new SpringValue(0, cfg.Springs.Snappy);
        // Rest thresholds suited to pixel-ish magnitudes.
        _w.RestDisplacement = _h.RestDisplacement = _r.RestDisplacement = 0.15;
        _sep.RestDisplacement = 0.002; _sep.RestVelocity = 0.002;
    }

    public IslandMode Mode => _mode;
    public bool AskActive => _ask;

    private void ApplyGeometry(IslandMode mode, IslandMetrics? custom = null)
    {
        var t = custom ?? _cfg.BaseMetrics(mode);
        _w.SetTarget(t.Width);
        _h.SetTarget(t.Height);
        _r.SetTarget(t.CornerRadius);
        _sep.SetTarget(mode == IslandMode.Minimal ? 1.0 : 0.0);
    }

    /// <summary>Show (or update) an activity's content and move to the given mode with a cross-fade.</summary>
    public void ShowContent(ActivitySnapshot snap, IslandMode mode)
    {
        _contentMode = mode;
        if (_ask) return; // Ask overrides the visible mode; content resumes when it closes.

        if (_hasCur && !SnapshotEquivalent(_cur.Snapshot, snap))
        {
            _out.Snapshot = _cur.Snapshot;
            _hasOut = true;
            _outStartMs = _nowMs;
        }
        _cur.Snapshot = snap;
        _hasCur = true;
        _inStartMs = _nowMs;
        _mode = mode;
        ApplyGeometry(mode);
    }

    /// <summary>Collapse back to the idle pill, fading current content out.</summary>
    public void Clear()
    {
        if (_hasCur) { _out.Snapshot = _cur.Snapshot; _hasOut = true; _outStartMs = _nowMs; }
        _hasCur = false;
        _contentMode = IslandMode.Idle;
        if (_ask) return;
        _mode = IslandMode.Idle;
        ApplyGeometry(IslandMode.Idle);
    }

    public void SetMode(IslandMode mode) { _mode = mode; ApplyGeometry(mode); }

    /// <summary>Update the current content in place (progress ticks) without a cross-fade.</summary>
    public void UpdateCurrent(ActivitySnapshot snap)
    {
        if (_hasCur && !_ask) _cur.Snapshot = snap;
    }

    public void SetAsk(bool on, string? text = null)
    {
        _ask = on;
        if (text != null) _askText = text;
        if (on)
        {
            _mode = IslandMode.Ask;
            _sweepPhase = 0; _caretMs = 0;
            ApplyGeometry(IslandMode.Ask);
        }
        else
        {
            _askText = "";
            _mode = _contentMode;
            ApplyGeometry(_contentMode);
        }
    }

    public void SetAskText(string text) => _askText = text;
    public string AskText => _askText;

    private static bool SnapshotEquivalent(ActivitySnapshot a, ActivitySnapshot b) =>
        a.Title == b.Title && a.Subtitle == b.Subtitle && a.Glyph == b.Glyph &&
        a.CompactLeading == b.CompactLeading && a.CompactTrailing == b.CompactTrailing &&
        Nullable.Equals(a.Progress, b.Progress) && a.Visual == b.Visual &&
        ReferenceEquals(a.ImageBgra, b.ImageBgra);

    // __APPEND_CTL2__

    /// <summary>Advance springs and timers. Returns true while something is still animating.</summary>
    public bool Update(double dt)
    {
        _nowMs += dt * 1000.0;
        bool anim = false;
        anim |= _w.Update(dt);
        anim |= _h.Update(dt);
        anim |= _r.Update(dt);
        anim |= _sep.Update(dt);

        if (_ask)
        {
            _caretMs += dt * 1000.0;
            _sweepPhase += dt * 1000.0 / System.Math.Max(1.0, _cfg.Timings.BorderSweepMs);
            _sweepPhase -= System.Math.Floor(_sweepPhase);
            anim = true;
        }

        // Content transitions keep us animating until they settle.
        double inDone = _inStartMs + _cfg.Timings.ContentInDelayMs + _cfg.Timings.ContentInMs;
        if (_hasCur && _nowMs < inDone) anim = true;
        if (_hasOut)
        {
            if (_nowMs - _outStartMs >= _cfg.Timings.ContentOutMs) _hasOut = false;
            else anim = true;
        }
        return anim;
    }

    private static double EaseOut(double t) { t = t < 0 ? 0 : t > 1 ? 1 : t; double u = 1 - t; return 1 - u * u * u; }
    private static float Lerp(float a, float b, double t) => (float)(a + (b - a) * t);

    public void BuildModel(RenderModel m)
    {
        m.Mode = _ask ? IslandMode.Ask : _mode;
        m.Dp = new IslandMetrics(_w.Value, _h.Value, _r.Value);
        m.CornerExponent = _cfg.Layout.CornerExponent;

        m.MinimalSeparation = (float)_sep.Value;
        m.MinimalBlobDp = (float)_cfg.Layout.MinimalBlobDp;
        m.MinimalGapDp = (float)_cfg.Layout.MinimalGapDp;

        string accent = (_hasCur ? _cur.Snapshot.AccentHex : null) ?? _cfg.Colors.GlowDefault;
        m.GlowArgb = ParseArgb(accent);
        m.GlowRadiusPx = (float)_cfg.Layout.GlowRadiusDp;
        bool quiet = m.Mode is IslandMode.Idle && !_hasCur && !_hasOut;
        m.GlowStrength = quiet ? 0.22f : (float)_cfg.Layout.GlowStrength;

        m.AskActive = _ask;
        m.AskText = _askText;
        m.AskGlowArgb = ParseArgb(_cfg.Colors.AskGlow);
        m.CaretOpacity = (float)(0.5 * (1 + System.Math.Cos(System.Math.PI * _caretMs / System.Math.Max(1.0, _cfg.Timings.CursorBlinkMs))));
        m.BorderSweepPhase = (float)_sweepPhase;

        // Incoming content transform (scale-up + fade; approximates "arrives from blur").
        double te = _nowMs - _inStartMs - _cfg.Timings.ContentInDelayMs;
        double ein = EaseOut(te / System.Math.Max(1.0, _cfg.Timings.ContentInMs));
        _cur.Alpha = (float)ein;
        _cur.Scale = Lerp(1.06f, 1.0f, ein);

        // Outgoing content transform (scale-down + fade; approximates "leaves to blur").
        double to = _nowMs - _outStartMs;
        double eo = EaseOut(to / System.Math.Max(1.0, _cfg.Timings.ContentOutMs));
        _out.Alpha = (float)(1 - eo);
        _out.Scale = Lerp(1.0f, 0.92f, eo);

        m.Current = _hasCur ? _cur : null;
        m.Outgoing = _hasOut ? _out : null;
    }

    private static uint ParseArgb(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6) return 0xFF000000u | System.Convert.ToUInt32(hex, 16);
            if (hex.Length == 8) return System.Convert.ToUInt32(hex, 16);
        }
        catch { }
        return 0xFF5AC8FAu;
    }
}
