using System;
using OpenTabletDriver.Plugin.Tablet;

namespace MoonlightSuite.Core;

internal static class TabletScale
{
    public const float Fallback = 100f;

    public static float UnitsPerMm(TabletReference? tablet)
    {
        var digitizer = tablet?.Properties?.Specifications?.Digitizer;

        if (digitizer == null)
            return Fallback;

        var x = digitizer.Width > 0f ? digitizer.MaxX / digitizer.Width : 0f;
        var y = digitizer.Height > 0f ? digitizer.MaxY / digitizer.Height : 0f;
        var scale = x > 0f && y > 0f ? (x + y) * 0.5f : MathF.Max(x, y);

        return float.IsFinite(scale) && scale > 0f ? scale : Fallback;
    }
}
