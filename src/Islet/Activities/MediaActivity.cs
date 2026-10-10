using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Islet.Core;
using Islet.Rendering;
using SkiaSharp;
using Windows.Media.Control;
using Windows.Storage.Streams;
using SmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using SmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Islet.Activities;

/// <summary>Now-playing live activity backed by the System Media Transport Controls (SMTC).</summary>
internal sealed class MediaActivity : ILiveActivity
{
    public string Id => "media";
    public string DisplayName => "Media";
    public ActivityPriority Priority => ActivityPriority.Normal;
    public event Action? Changed;

    private IActivityHost? _host;
    private SmtcManager? _mgr;
    private SmtcSession? _session;
    private Timer? _tick;

    private string _title = "", _artist = "", _source = "";
    private string _attachedId = "";
    private bool _playing;
    private double _posSec, _endSec;
    private long _updateTicks;
    private byte[]? _artBgra; private int _artW, _artH;
    private string _accent = "#5AC8FA";

    public void Start(IActivityHost host)
    {
        _host = host;
        _ = InitAsync();
        _tick = new Timer(_ => { if (_playing) Changed?.Invoke(); }, null, 1000, 1000);
    }

    public void Stop()
    {
        _tick?.Dispose();
        Detach();
    }

    private async Task InitAsync()
    {
        try
        {
            _mgr = await SmtcManager.RequestAsync();
            _mgr.CurrentSessionChanged += (_, _) => PickBest();
            _mgr.SessionsChanged += (_, _) => PickBest();
            PickBest();
        }
        catch { /* SMTC unavailable */ }
    }

    // Choose the most relevant session across all services (SoundCloud, Spotify, browsers, …):
    // prefer one that is actively Playing, else the system "current" session, else the first.
    private void PickBest()
    {
        if (_mgr == null) return;
        SmtcSession? best = null;
        try
        {
            var sessions = _mgr.GetSessions();
            foreach (var s in sessions)
            {
                try
                {
                    if (s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    { best = s; break; }
                }
                catch { }
            }
            best ??= _mgr.GetCurrentSession() ?? (sessions.Count > 0 ? sessions[0] : null);
        }
        catch { try { best = _mgr.GetCurrentSession(); } catch { } }

        string id = SafeAumid(best);
        if (best != null && id == _attachedId && _session != null) { _ = LoadPropsAsync(_session); return; }
        _attachedId = id;
        _source = FriendlyService(id);
        Attach(best);
    }

    private static string SafeAumid(SmtcSession? s) { try { return s?.SourceAppUserModelId ?? ""; } catch { return ""; } }

    private static string FriendlyService(string aumid)
    {
        string a = aumid.ToLowerInvariant();
        if (a.Contains("soundcloud")) return "SoundCloud";
        if (a.Contains("spotify")) return "Spotify";
        if (a.Contains("youtube")) return "YouTube";
        if (a.Contains("chrome")) return "Chrome";
        if (a.Contains("msedge") || a.Contains("edge")) return "Edge";
        if (a.Contains("firefox")) return "Firefox";
        if (a.Contains("vlc")) return "VLC";
        if (a.Contains("tidal")) return "TIDAL";
        if (a.Contains("deezer")) return "Deezer";
        if (a.Contains("apple") && a.Contains("music")) return "Apple Music";
        if (a.Contains("zune") || a.Contains("music")) return "Media Player";
        int dot = a.LastIndexOf(".exe", StringComparison.Ordinal);
        if (dot > 0) { int slash = a.LastIndexOfAny(new[] { '\\', '/', '!' }, dot); string nm = a.Substring(slash + 1, dot - slash - 1); if (nm.Length > 0) return char.ToUpper(nm[0]) + nm[1..]; }
        return "";
    }

    private void Attach(SmtcSession? s)
    {
        Detach();
        _session = s;
        if (s == null) { Changed?.Invoke(); return; }
        s.MediaPropertiesChanged += OnProps;
        s.PlaybackInfoChanged += OnPlayback;
        s.TimelinePropertiesChanged += OnTimeline;
        OnPlayback(s, null!); OnTimeline(s, null!); _ = LoadPropsAsync(s);
    }

    private void Detach()
    {
        if (_session == null) return;
        try
        {
            _session.MediaPropertiesChanged -= OnProps;
            _session.PlaybackInfoChanged -= OnPlayback;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        catch { }
        _session = null;
    }

    private void OnProps(SmtcSession s, MediaPropertiesChangedEventArgs e) => _ = LoadPropsAsync(s);

    private void OnPlayback(SmtcSession s, PlaybackInfoChangedEventArgs e)
    {
        try { _playing = s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { }
        Changed?.Invoke();
    }

    private void OnTimeline(SmtcSession s, TimelinePropertiesChangedEventArgs e)
    {
        try
        {
            var t = s.GetTimelineProperties();
            _posSec = t.Position.TotalSeconds;
            _endSec = t.EndTime.TotalSeconds;
            _updateTicks = Stopwatch.GetTimestamp();
        }
        catch { }
        Changed?.Invoke();
    }

    // __APPEND_MEDIA__

    private async Task LoadPropsAsync(SmtcSession s)
    {
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            _title = p.Title ?? "";
            _artist = p.Artist ?? "";
            if (p.Thumbnail != null) await LoadArtAsync(p.Thumbnail);
            Changed?.Invoke();
        }
        catch { }
    }

    private async Task LoadArtAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            uint size = (uint)stream.Size;
            if (size == 0) return;
            using var dr = new DataReader(stream);
            await dr.LoadAsync(size);
            var bytes = new byte[size];
            dr.ReadBytes(bytes);

            using var decoded = SKBitmap.Decode(bytes);
            if (decoded == null) return;
            const int tw = 128, th = 128;
            using var small = decoded.Resize(new SKImageInfo(tw, th, SKColorType.Bgra8888, SKAlphaType.Premul), SKFilterQuality.Medium);
            if (small == null) return;

            _artBgra = small.Bytes;
            _artW = tw; _artH = th;
            _accent = Dominant(small);
        }
        catch { }
    }

    private static string Dominant(SKBitmap bmp)
    {
        try
        {
            using var one = bmp.Resize(new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul), SKFilterQuality.Medium);
            if (one == null) return "#5AC8FA";
            var c = one.GetPixel(0, 0);
            // Lift toward a vivid glow so dark art still produces visible light.
            byte r = (byte)Math.Min(255, c.Red + 30), g = (byte)Math.Min(255, c.Green + 30), b = (byte)Math.Min(255, c.Blue + 30);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch { return "#5AC8FA"; }
    }

    public bool IsActive => _session != null && !string.IsNullOrEmpty(_title);

    public ActivitySnapshot Snapshot()
    {
        double cur = _posSec;
        if (_playing) cur += (Stopwatch.GetTimestamp() - _updateTicks) / (double)Stopwatch.Frequency;
        double frac = _endSec > 0.5 ? Math.Clamp(cur / _endSec, 0, 1) : 0;

        string subtitle = _artist.Length > 0
            ? (_source.Length > 0 ? $"{_artist} · {_source}" : _artist)
            : _source;

        return new ActivitySnapshot
        {
            Title = _title.Length > 0 ? _title : "Playing",
            Subtitle = subtitle,
            Progress = frac,
            Visual = VisualKind.Bar,
            Glyph = Glyphs.Music,
            CompactLeading = Glyphs.Music,
            CompactTrailing = _playing ? Glyphs.Pause : Glyphs.Play,
            AccentHex = _accent,
            ImageBgra = _artBgra,
            ImageW = _artW,
            ImageH = _artH,
        };
    }
}
