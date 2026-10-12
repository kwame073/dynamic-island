using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Islet.Core;
using Islet.Rendering;
using Windows.Win32;
using static Islet.Interop.Native;

namespace Islet;

/// <summary>
/// Owns the layered Win32 island window on a dedicated STA thread: creates it, runs the
/// message-pump + render loop (paced to the compositor via DwmFlush), performs shape hit-testing,
/// and blits each Skia frame with UpdateLayeredWindow.
/// </summary>
internal sealed class IslandWindow
{
    private readonly IsletConfig _cfg;
    private readonly IslandController _ctl;
    private readonly IslandRenderer _renderer = new();
    private readonly LayeredSurface _surface = new();
    private readonly ConcurrentQueue<Action> _pending = new();

    private Thread? _thread;
    private IntPtr _hwnd;
    private WndProc? _wndProc; // kept alive against GC
    private volatile bool _running;
    private bool _forceRender = true;

    private double _scale = 1.0;
    private int _surfW, _surfH;
    private int _winX, _winY;
    private RECT _monitor;

    // Last rendered island geometry in surface pixels (for hit-testing).
    private float _hitCx, _hitCy, _hitW, _hitH, _hitR;
    private bool _hovering;
    private RenderModel? _model;

    private const int HOTKEY_ID = 0xA11;

    public event Action? Clicked;
    public event Action? HotkeyPressed;
    public event Action<bool>? HoverChanged;
    public event Action<char>? CharTyped;
    public event Action<int>? KeyPressed; // virtual-key code
    public event Action<string, float>? MediaCommand; // (zone id, seek fraction)

    public IslandWindow(IsletConfig cfg, IslandController ctl)
    {
        _cfg = cfg;
        _ctl = ctl;
    }

    public IslandController Controller => _ctl;

    /// <summary>Run an action on the render thread (safe from any thread).</summary>
    public void Post(Action a) { _pending.Enqueue(a); _forceRender = true; }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Islet.Island" };
        _thread.SetApartmentState(ApartmentState.STA);
        _running = true;
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero) PostMessageW(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    // __APPEND_WIN__

    private IntPtr _clsNamePtr;

    private void Run()
    {
        var hInstance = GetModuleHandleW(null);
        _wndProc = WndProcImpl;
        const string cls = "IsletIslandWindow";
        _clsNamePtr = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(cls);

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            hCursor = LoadCursorW(IntPtr.Zero, IDC_ARROW),
            lpszClassName = _clsNamePtr,
        };
        RegisterClassExW(ref wc);

        ChooseMonitorRect();

        uint ex = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        _hwnd = CreateWindowExW(ex, cls, "Islet", WS_POPUP,
            _monitor.Left, _monitor.Top, 16, 16, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return;

        _scale = Math.Max(1.0, PInvoke.GetDpiForWindow(new Windows.Win32.Foundation.HWND(_hwnd)) / 96.0);
        ComputeSurfaceSize();
        RegisterConfiguredHotkey();
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);

        Loop();
        Cleanup();
    }

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        double last = clock.Elapsed.TotalSeconds;
        var model = new RenderModel();

        while (_running)
        {
            while (_pending.TryDequeue(out var a)) { try { a(); } catch { } _forceRender = true; }

            while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
            if (!_running) break;

            double now = clock.Elapsed.TotalSeconds;
            double dt = now - last;
            last = now;

            bool active = _ctl.Update(dt);
            PollHover();

            if (active || _forceRender)
            {
                RenderFrame(model);
                _forceRender = false;
            }

            if (active)
            {
                if (PInvoke.DwmFlush().Failed) Thread.Sleep(8);
            }
            else Thread.Sleep(12);
        }
    }

    // __APPEND_WIN2__

    private void RenderFrame(RenderModel model)
    {
        model.SurfaceW = _surfW;
        model.SurfaceH = _surfH;
        model.Scale = (float)_scale;
        model.AnchorTopPx = (float)(_cfg.Layout.GlowRadiusDp * _scale);
        _ctl.BuildModel(model);

        _surface.EnsureSize(_surfW, _surfH);
        _renderer.Draw(_surface.Surface!.Canvas, model);

        int monCenterX = (_monitor.Left + _monitor.Right) / 2;
        _winX = monCenterX - _surfW / 2;
        _winY = _monitor.Top + (int)Math.Round(_cfg.Layout.TopMarginDp * _scale) - (int)model.AnchorTopPx;
        _surface.Present(_hwnd, _winX, _winY);

        float s = (float)_scale;
        _hitW = (float)model.Dp.Width * s;
        _hitH = (float)model.Dp.Height * s;
        _hitR = (float)model.Dp.CornerRadius * s;
        _hitCx = _surfW / 2f;
        _hitCy = model.AnchorTopPx + _hitH / 2f;
        _model = model;
    }

    private void PollHover()
    {
        if (!GetCursorPos(out var p)) return;
        float lx = p.X - _winX, ly = p.Y - _winY;
        bool inside = RoundedContains(lx, ly, _hitCx, _hitCy, _hitW, _hitH, _hitR, pad: 2f * (float)_scale);
        if (inside != _hovering)
        {
            _hovering = inside;
            HoverChanged?.Invoke(inside);
        }
    }

    private static bool RoundedContains(float px, float py, float cx, float cy, float w, float h, float r, float pad = 0)
    {
        float hw = w / 2f + pad, hh = h / 2f + pad;
        float dx = MathF.Abs(px - cx), dy = MathF.Abs(py - cy);
        if (dx > hw || dy > hh) return false;
        float rx = hw - r, ry = hh - r;
        if (dx <= rx || dy <= ry) return true;
        float qx = dx - rx, qy = dy - ry;
        return qx * qx + qy * qy <= r * r;
    }

    private void ChooseMonitorRect()
    {
        POINT p;
        IntPtr mon;
        if (string.Equals(_cfg.Behavior.PreferredMonitor, "WithCursor", StringComparison.OrdinalIgnoreCase)
            && GetCursorPos(out p))
            mon = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        else
            mon = MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY);

        var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfoW(mon, ref mi)) _monitor = mi.rcMonitor;
        else _monitor = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
    }

    private void ComputeSurfaceSize()
    {
        var L = _cfg.Layout;
        double maxW = Math.Max(Math.Max(L.IdleW, L.CompactW), Math.Max(L.ExpandedW, Math.Max(L.AlertW, L.AskW)));
        double maxH = Math.Max(Math.Max(L.IdleH, L.CompactH), Math.Max(L.ExpandedH, Math.Max(L.AlertH, L.AskH)));
        double marginDp = L.GlowRadiusDp * 2 + 40;
        double wdp = maxW + marginDp * 2 + 110; // +110 for the Minimal detached blob reach
        double hdp = L.TopMarginDp + maxH + marginDp;
        _surfW = (int)Math.Ceiling(wdp * _scale);
        _surfH = (int)Math.Ceiling(hdp * _scale);
        _surface.EnsureSize(_surfW, _surfH);
        _forceRender = true;
    }

    // __APPEND_WIN3__

    private IntPtr WndProcImpl(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
            {
                int sx = LoWord(lParam), sy = HiWord(lParam);
                float lx = sx - _winX, ly = sy - _winY;
                bool inside = RoundedContains(lx, ly, _hitCx, _hitCy, _hitW, _hitH, _hitR);
                return (IntPtr)(inside ? HTCLIENT : HTTRANSPARENT);
            }
            case WM_LBUTTONUP:
            {
                float lx = LoWord(lParam), ly = HiWord(lParam); // client coords == surface pixels
                var zones = _model?.HitZones;
                if (zones != null)
                {
                    for (int i = zones.Count - 1; i >= 0; i--) // last drawn = topmost
                    {
                        var z = zones[i];
                        if (z.Contains(lx, ly))
                        {
                            float frac = z.Id == "media.seek" && z.R > z.L ? (lx - z.L) / (z.R - z.L) : 0f;
                            MediaCommand?.Invoke(z.Id, frac);
                            return IntPtr.Zero;
                        }
                    }
                }
                Clicked?.Invoke();
                return IntPtr.Zero;
            }
            case WM_HOTKEY:
                HotkeyPressed?.Invoke();
                return IntPtr.Zero;
            case WM_CHAR:
                CharTyped?.Invoke((char)(long)wParam);
                return IntPtr.Zero;
            case WM_KEYDOWN:
                KeyPressed?.Invoke((int)(long)wParam);
                return IntPtr.Zero;
            case WM_DPICHANGED:
                _scale = Math.Max(1.0, LoWord(wParam) / 96.0);
                ComputeSurfaceSize();
                return IntPtr.Zero;
            case WM_DISPLAYCHANGE:
                ChooseMonitorRect();
                ComputeSurfaceSize();
                return IntPtr.Zero;
            case WM_CLOSE:
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void SetVisible(bool visible) => Post(() =>
    {
        if (_hwnd != IntPtr.Zero) ShowWindow(_hwnd, visible ? SW_SHOWNOACTIVATE : SW_HIDE);
    });

    /// <summary>Allow/deny the window to receive keyboard focus (used only during Ask mode).</summary>
    public void EnableInput(bool on) => Post(() =>
    {
        if (_hwnd == IntPtr.Zero) return;
        uint ex = (uint)(long)GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
        if (on) ex &= ~WS_EX_NOACTIVATE; else ex |= WS_EX_NOACTIVATE;
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, (IntPtr)(long)ex);
        if (on) SetForegroundWindow(_hwnd);
    });

    private void RegisterConfiguredHotkey()
    {
        uint mods = MOD_NOREPEAT;
        foreach (var token in _cfg.Behavior.HotkeyModifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "win": mods |= MOD_WIN; break;
                case "alt": mods |= MOD_ALT; break;
                case "ctrl": case "control": mods |= MOD_CONTROL; break;
                case "shift": mods |= MOD_SHIFT; break;
            }
        }
        uint vk = ParseVk(_cfg.Behavior.HotkeyKey);
        if (vk != 0) RegisterHotKey(_hwnd, HOTKEY_ID, mods, vk);
    }

    private static uint ParseVk(string key)
    {
        key = key.Trim();
        if (key.Equals("Space", StringComparison.OrdinalIgnoreCase)) return 0x20;
        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
        }
        if (key.StartsWith('F') && int.TryParse(key.AsSpan(1), out int f) && f is >= 1 and <= 24)
            return (uint)(0x70 + (f - 1)); // VK_F1..
        return 0x20;
    }

    private void Cleanup()
    {
        if (_hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, HOTKEY_ID);
        _surface.Dispose();
        if (_clsNamePtr != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeHGlobal(_clsNamePtr);
        _clsNamePtr = IntPtr.Zero;
    }
}
