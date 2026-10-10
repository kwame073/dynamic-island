using System;
using System.Runtime.InteropServices;

namespace Islet.Interop;

// Minimal Core Audio COM interop to read the default render endpoint's master volume + mute.
// Only the vtable slots up to the methods we call are declared (order matters).

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr p);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr p);
    [PreserveSig] int GetChannelCount(out int count);
    [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr ctx);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr ctx);
    [PreserveSig] int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint n, float level, IntPtr ctx);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint n, float level, IntPtr ctx);
    [PreserveSig] int GetChannelVolumeLevel(uint n, out float level);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint n, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr ctx);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

internal static class CoreAudio
{
    private const int ERender = 0, EMultimedia = 1, ClsCtxAll = 23;

    /// <summary>Reads the default playback device's volume (0..1) and mute. False if unavailable.</summary>
    public static bool TryGetMasterVolume(out float scalar, out bool mute)
    {
        scalar = 0; mute = false;
        IMMDeviceEnumerator? en = null;
        IMMDevice? dev = null;
        IAudioEndpointVolume? vol = null;
        try
        {
            en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(ERender, EMultimedia, out dev) != 0 || dev == null) return false;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var obj) != 0 || obj == null) return false;
            vol = (IAudioEndpointVolume)obj;
            vol.GetMasterVolumeLevelScalar(out scalar);
            vol.GetMute(out mute);
            return true;
        }
        catch { return false; }
        finally
        {
            if (vol != null) Marshal.ReleaseComObject(vol);
            if (dev != null) Marshal.ReleaseComObject(dev);
            if (en != null) Marshal.ReleaseComObject(en);
        }
    }
}
