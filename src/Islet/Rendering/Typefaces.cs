using SkiaSharp;

namespace Islet.Rendering;

/// <summary>Resolves text and icon typefaces, honoring the "no SF Pro / SF Symbols" rule.</summary>
internal static class Typefaces
{
    // Text: Segoe UI Variable (Win11) -> Segoe UI -> Inter -> default.
    public static readonly SKTypeface Ui =
        Pick("Segoe UI Variable Display", "Segoe UI Variable Text", "Segoe UI", "Inter");

    public static readonly SKTypeface UiSemibold =
        PickStyled(new SKFontStyle(SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            "Segoe UI Variable Display Semib", "Segoe UI Variable Display", "Segoe UI", "Inter");

    // Icons: Segoe Fluent Icons (Win11) -> Segoe MDL2 Assets (Win10) -> default.
    public static readonly SKTypeface Icons =
        Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");

    private static SKTypeface Pick(params string[] families) => PickStyled(SKFontStyle.Normal, families);

    private static SKTypeface PickStyled(SKFontStyle style, params string[] families)
    {
        foreach (var f in families)
        {
            var t = SKTypeface.FromFamilyName(f, style);
            if (t != null && t.FamilyName != null &&
                t.FamilyName.Contains(f.Split(' ')[0], System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return SKTypeface.Default;
    }
}
