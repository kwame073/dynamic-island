// Both WPF and WinForms are enabled (WinForms only for the tray NotifyIcon), which makes a few
// type names ambiguous. Pin the ones we use to the intended type for the whole assembly.
global using Timer = System.Threading.Timer;
global using Application = System.Windows.Application;
