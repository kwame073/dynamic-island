using System;
using System.Windows;

namespace Islet;

public partial class App : Application
{
    private IsletHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless renderer self-check: `Islet.exe --render-probe <dir>`
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--render-probe")
            {
                string dir = i + 1 < e.Args.Length ? e.Args[i + 1] : Environment.CurrentDirectory;
                RenderProbe.Run(dir);
                Shutdown(0);
                return;
            }
            if (e.Args[i] == "--showcase")
            {
                string dir = i + 1 < e.Args.Length ? e.Args[i + 1] : "showcase_frames";
                double secs = i + 2 < e.Args.Length && double.TryParse(e.Args[i + 2], out var s) ? s : 15.0;
                int fps = i + 3 < e.Args.Length && int.TryParse(e.Args[i + 3], out var f) ? f : 60;
                Showcase.Run(dir, secs, fps);
                Shutdown(0);
                return;
            }
        }

        if (Array.IndexOf(e.Args, "--media-test") >= 0)
        {
            try
            {
                var mgr = global::Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
                var sessions = mgr.GetSessions();
                Console.WriteLine($"media-test: {sessions.Count} session(s)");
                foreach (var s in sessions)
                {
                    var pb = s.GetPlaybackInfo();
                    var p = s.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"  [{pb?.PlaybackStatus}] {s.SourceAppUserModelId} :: {p?.Title} — {p?.Artist}");
                }
            }
            catch (Exception ex) { Console.WriteLine("media-test error: " + ex.Message); }
            Shutdown(0);
            return;
        }

        if (Array.IndexOf(e.Args, "--audio-test") >= 0)
        {
            bool ok = Islet.Interop.CoreAudio.TryGetMasterVolume(out float s, out bool m);
            Console.WriteLine($"audio-test: ok={ok} volume={(int)(s * 100)}% mute={m}");
            Shutdown(0);
            return;
        }

        _host = new IsletHost();
        _host.Start();

        if (Array.IndexOf(e.Args, "--demo") >= 0)
            _host.StartTimer(120); // populate a Compact activity for live verification
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Stop();
        base.OnExit(e);
    }
}
