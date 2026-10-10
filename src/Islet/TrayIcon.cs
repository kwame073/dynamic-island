using System;
using System.Drawing;
using System.Windows.Forms;
using Islet.Activities;

namespace Islet;

/// <summary>System-tray icon with a context menu (WinForms NotifyIcon under WPF).</summary>
internal sealed class TrayIcon
{
    private readonly IsletHost _host;
    private readonly ActivityManager _mgr;
    private NotifyIcon? _ni;

    public TrayIcon(IsletHost host, ActivityManager mgr) { _host = host; _mgr = mgr; }

    public void Start()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Ask  (Win+Alt+Space)", null, (_, _) => _host.ToggleAsk());
        menu.Items.Add("Start 25:00 timer", null, (_, _) => _host.StartTimer(25 * 60));
        menu.Items.Add("Start 10s test timer", null, (_, _) => _host.StartTimer(10));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open config folder", null, (_, _) => _host.OpenConfigFolder());
        menu.Items.Add("Quit Islet", null, (_, _) => _host.Quit());

        _ni = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Islet",
            ContextMenuStrip = menu,
        };
        _ni.DoubleClick += (_, _) => _host.ToggleAsk();
    }

    public void Stop()
    {
        if (_ni != null) { _ni.Visible = false; _ni.Dispose(); _ni = null; }
    }
}
