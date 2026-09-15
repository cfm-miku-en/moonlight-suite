using System;
using System.Linq;
using Xunit;

namespace MoonlightSuite.Tests;

public class CurveTests
{
    private const uint Max = 8192u;

    private static uint Through(MoonlightCurve curve, uint pressure)
    {
        var report = new PenReport { Pressure = pressure };
        curve.Consume(report);

        return report.Pressure;
    }

    [Fact]
    public void LinearModeLeavesPressureAlone()
    {
        var curve = new MoonlightCurve { Mode = MoonlightCurve.LinearMode, Amount = 1f };

        foreach (var pressure in new uint[] { 0, 1000, 4096, 8000, Max })
            Assert.True(Math.Abs((int)Through(curve, pressure) - (int)pressure) <= 1,
                $"linear mode changed {pressure}");
    }

    [Fact]
    public void ZeroAmountLeavesPressureAlone()
    {
        var curve = new MoonlightCurve { Mode = MoonlightCurve.SoftMode, Amount = 0f };

        foreach (var pressure in new uint[] { 0, 1000, 4096, 8000, Max })
            Assert.True(Math.Abs((int)Through(curve, pressure) - (int)pressure) <= 1,
                $"a zero amount changed {pressure}");
    }

    [Fact]
    public void SoftNeedsMoreForceAndHardNeedsLess()
    {
        var soft = Through(new MoonlightCurve { Mode = MoonlightCurve.SoftMode, Amount = 1f }, Max / 2);
        var hard = Through(new MoonlightCurve { Mode = MoonlightCurve.HardMode, Amount = 1f }, Max / 2);

        Assert.True(soft < Max / 2, $"soft mode should read lighter at half force, got {soft}");
        Assert.True(hard > Max / 2, $"hard mode should read heavier at half force, got {hard}");
    }

    [Fact]
    public void DeadzoneAndCeilingClampTheEnds()
    {
        var curve = new MoonlightCurve
        {
            Mode = MoonlightCurve.LinearMode,
            Amount = 0f,
            Deadzone = 20f,
            Ceiling = 80f
        };

        Assert.Equal(0u, Through(curve, (uint)(Max * 0.15f)));
        Assert.Equal(0u, Through(curve, 0u));
        Assert.Equal(Max, Through(curve, (uint)(Max * 0.85f)));
        Assert.Equal(Max, Through(curve, Max));

        var middle = Through(curve, (uint)(Max * 0.5f));

        Assert.True(middle > 0u && middle < Max, $"halfway between deadzone and ceiling should be partial, got {middle}");
    }

    [Fact]
    public void OutputNeverLeavesTheValidRange()
    {
        foreach (var mode in new[] { MoonlightCurve.SoftMode, MoonlightCurve.HardMode, MoonlightCurve.LinearMode })
        foreach (var amount in new[] { 0f, 0.5f, 1f })
        foreach (var deadzone in new[] { 0f, 25f, 50f })
        foreach (var ceiling in new[] { 50f, 75f, 100f })
        {
            var curve = new MoonlightCurve { Mode = mode, Amount = amount, Deadzone = deadzone, Ceiling = ceiling };

            foreach (var pressure in Enumerable.Range(0, 33).Select(i => (uint)(i * Max / 32)))
                Assert.True(Through(curve, pressure) <= Max,
                    $"{mode} at amount {amount}, deadzone {deadzone}, ceiling {ceiling} blew past max pressure");
        }
    }

    [Fact]
    public void ReportsWithoutPressureAreForwardedUntouched()
    {
        var curve = new MoonlightCurve { Mode = MoonlightCurve.SoftMode, Amount = 1f };
        var seen = 0;
        curve.Emit += _ => seen++;

        curve.Consume(new OpenTabletDriver.Plugin.Tablet.OutOfRangeReport());

        Assert.Equal(1, seen);
    }
}
