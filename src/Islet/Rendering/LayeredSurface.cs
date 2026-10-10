using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using static Islet.Interop.Native;

namespace Islet.Rendering;

/// <summary>
/// A GDI DIB section (top-down, 32bpp premultiplied BGRA) wrapped by an <see cref="SKSurface"/>.
/// We render with Skia straight into the DIB memory, then blit it to a layered window with
/// <c>UpdateLayeredWindow</c>, which gives true per-pixel alpha (soft glow + antialiased squircle)
/// composited over whatever is on the desktop.
/// </summary>
internal sealed class LayeredSurface : IDisposable
{
    private IntPtr _memDc;
    private IntPtr _dib;
    private IntPtr _oldObj;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public SKSurface? Surface { get; private set; }

    public void EnsureSize(int w, int h)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        if (w == Width && h == Height && Surface != null) return;

        DisposeGdi();

        _memDc = CreateCompatibleDC(IntPtr.Zero);
        var bmi = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,          // negative => top-down, matching Skia's layout
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BI_RGB,
        };
        _dib = CreateDIBSection(_memDc, ref bmi, DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed");
        _oldObj = SelectObject(_memDc, _dib);

        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        Surface = SKSurface.Create(info, bits, w * 4);
        Width = w;
        Height = h;
    }

    /// <summary>Blit current pixels to the layered window positioned at screen (x,y).</summary>
    public void Present(IntPtr hwnd, int x, int y)
    {
        if (Surface == null) return;
        Surface.Canvas.Flush();

        var ptDst = new POINT(x, y);
        var size = new SIZE(Width, Height);
        var ptSrc = new POINT(0, 0);
        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA,
        };
        UpdateLayeredWindow(hwnd, IntPtr.Zero, ref ptDst, ref size, _memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
    }

    private void DisposeGdi()
    {
        Surface?.Dispose();
        Surface = null;
        if (_memDc != IntPtr.Zero)
        {
            if (_oldObj != IntPtr.Zero) SelectObject(_memDc, _oldObj);
            if (_dib != IntPtr.Zero) DeleteObject(_dib);
            DeleteDC(_memDc);
        }
        _memDc = _dib = _oldObj = IntPtr.Zero;
        Width = Height = 0;
    }

    public void Dispose() => DisposeGdi();
}
