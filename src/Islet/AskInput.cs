using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Islet.Activities;
using Islet.Core;

namespace Islet;

/// <summary>
/// Drives the Ask field: toggles the mode, collects typed text (while the window briefly accepts focus),
/// and dispatches the query to the configured handler (Windows search / command / local HTTP endpoint).
/// </summary>
internal sealed class AskInput
{
    private static readonly HttpClient Http = new();

    private readonly IsletConfig _cfg;
    private readonly IslandController _ctl;
    private readonly IslandWindow _win;
    private readonly ActivityManager _mgr;
    private string _text = "";

    public AskInput(IsletConfig cfg, IslandController ctl, IslandWindow win, ActivityManager mgr)
    {
        _cfg = cfg; _ctl = ctl; _win = win; _mgr = mgr;
    }

    public void Toggle()
    {
        bool on = !_ctl.AskActive;
        _win.Post(() => _ctl.SetAsk(on));
        if (on) { _text = ""; _win.EnableInput(true); }
        else { _win.EnableInput(false); _mgr.Refresh(); }
    }

    public void OnChar(char c)
    {
        if (!_ctl.AskActive) return;
        if (c < 32 || c == 127) return; // control chars handled via OnKey
        _text += c;
        Update();
    }

    public void OnKey(int vk)
    {
        if (!_ctl.AskActive) return;
        switch (vk)
        {
            case 8: if (_text.Length > 0) { _text = _text[..^1]; Update(); } break; // Backspace
            case 13: Submit(); break;                                               // Enter
            case 27: Close(); break;                                                // Esc
        }
    }

    private void Update() => _win.Post(() => _ctl.SetAskText(_text));

    private void Close()
    {
        _win.Post(() => _ctl.SetAsk(false));
        _win.EnableInput(false);
        _mgr.Refresh();
    }

    private void Submit()
    {
        string q = _text.Trim();
        Close();
        if (q.Length == 0) return;
        try { Dispatch(q); } catch { }
    }

    private void Dispatch(string q)
    {
        switch (_cfg.Behavior.AskHandler.ToLowerInvariant())
        {
            case "command" when _cfg.Behavior.AskCommand.Length > 0:
                Process.Start(new ProcessStartInfo(_cfg.Behavior.AskCommand, q) { UseShellExecute = true });
                break;
            case "http" when _cfg.Behavior.AskEndpoint.Length > 0:
                _ = Http.PostAsync(_cfg.Behavior.AskEndpoint,
                        new StringContent($"{{\"query\":{System.Text.Json.JsonSerializer.Serialize(q)}}}", Encoding.UTF8, "application/json"));
                break;
            default: // WindowsSearch
                try { Process.Start(new ProcessStartInfo($"search-ms:query={Uri.EscapeDataString(q)}") { UseShellExecute = true }); }
                catch { Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={Uri.EscapeDataString(q)}") { UseShellExecute = true }); }
                break;
        }
    }
}
