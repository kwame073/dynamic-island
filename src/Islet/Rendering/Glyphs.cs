using System;

namespace Islet.Rendering;

/// <summary>
/// Icon glyphs from "Segoe Fluent Icons" (Win11) / "Segoe MDL2 Assets" (Win10), referenced by
/// code point so no literal PUA characters live in source. These are our own iconography — not SF Symbols.
/// </summary>
internal static class Glyphs
{
    public static readonly string Play = char.ConvertFromUtf32(0xE768);
    public static readonly string Pause = char.ConvertFromUtf32(0xE769);
    public static readonly string Previous = char.ConvertFromUtf32(0xE892);
    public static readonly string Next = char.ConvertFromUtf32(0xE893);
    public static readonly string Music = char.ConvertFromUtf32(0xEC4F);
    public static readonly string Search = char.ConvertFromUtf32(0xE721);
    public static readonly string Microphone = char.ConvertFromUtf32(0xE720);
    public static readonly string Camera = char.ConvertFromUtf32(0xE722);
    public static readonly string Volume = char.ConvertFromUtf32(0xE767);
    public static readonly string VolumeMute = char.ConvertFromUtf32(0xE74F);
    public static readonly string Timer = char.ConvertFromUtf32(0xE916);
    public static readonly string Battery = char.ConvertFromUtf32(0xE83F);
    public static readonly string BatteryCharging = char.ConvertFromUtf32(0xE83E);
    public static readonly string Bolt = char.ConvertFromUtf32(0xE945);
    public static readonly string Bell = char.ConvertFromUtf32(0xEA8F);
    public static readonly string Phone = char.ConvertFromUtf32(0xE717);
    public static readonly string Download = char.ConvertFromUtf32(0xE896);
    public static readonly string Bluetooth = char.ConvertFromUtf32(0xE702);
    public static readonly string Wifi = char.ConvertFromUtf32(0xE701);
    public static readonly string Headphones = char.ConvertFromUtf32(0xE7F6);
    public static readonly string Brightness = char.ConvertFromUtf32(0xE706);
    public static readonly string CapsLock = char.ConvertFromUtf32(0xE84A);
}
