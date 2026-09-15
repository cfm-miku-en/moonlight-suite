using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace MoonlightSuite.Tests;

public class SpringTests
{
    private sealed class Probe : MoonlightSpring
    {
        public Vector2 Current => Advance(0f);

        public void Seed(Vector2 position) => Reset(position);

        public void Aim(Vector2 position) => SetTarget(position, 1f / 133f);

        public Vector2 Step(float deltaTime) => Advance(deltaTime);
    }

    [Fact]
    public void SpringNeverDivergesAcrossItsSliderRange()
    {
        foreach (var stiffness in new[] { 0.5f, 5f, 12f, 25f, 40f })
        foreach (var damping in new[] { 0.1f, 0.5f, 1f, 2f, 4f })
        {
            var probe = new Probe { Stiffness = stiffness, Damping = damping, Settle = 0f };
            var target = Paths.Origin + new Vector2(1000f, 0f);

            probe.Seed(Paths.Origin);
            probe.Aim(target);

            for (var i = 0; i < 4000; i++)
            {
                var position = probe.Step(0.001f);

                Assert.True(float.IsFinite(position.X) && float.IsFinite(position.Y),
                    $"{stiffness} hz at damping {damping} went non-finite");
                Assert.True(Vector2.Distance(position, Paths.Origin) < 3000f,
                    $"{stiffness} hz at damping {damping} ran away to {position}");
            }
        }
    }

    [Fact]
    public void SpringSurvivesAStalledReportStream()
    {
        var probe = new Probe { Stiffness = 40f, Damping = 4f, Settle = 0f };

        probe.Seed(Paths.Origin);
        probe.Aim(Paths.Origin + new Vector2(5000f, 5000f));

        for (var i = 0; i < 200; i++)
        {
            var position = probe.Step(0.25f);

            Assert.True(float.IsFinite(position.X) && float.IsFinite(position.Y),
                "a stalled report stream blew the integrator up");
        }
    }

    [Fact]
    public void CriticallyDampedSpringDoesNotOvershoot()
    {
        var probe = new Probe { Stiffness = 12f, Damping = 1f, Settle = 0f };
        var target = Paths.Origin + new Vector2(1000f, 0f);

        probe.Seed(Paths.Origin);
        probe.Aim(target);

        for (var i = 0; i < 2000; i++)
            Assert.True(probe.Step(0.001f).X <= target.X + 0.5f, "a critically damped spring should never overshoot");
    }

    [Fact]
    public void SpringSettlesExactlyOnTheTarget()
    {
        var probe = new Probe { Stiffness = 15f, Damping = 1f, Settle = 0.05f };
        var target = Paths.Origin + new Vector2(500f, 0f);

        probe.Seed(Paths.Origin);
        probe.Aim(target);

        for (var i = 0; i < 4000; i++)
            probe.Step(0.001f);

        Assert.Equal(target, probe.Current);
    }

    [Fact]
    public void SpringRestartsWhereThePenComesBackDown()
    {
        var probe = new Probe { Stiffness = 5f, Damping = 1f, Settle = 0f };
        probe.IntegrationClock = new ManualTimeSource(0.001f);
        probe.ReportClock = new ManualTimeSource(0.001f);

        for (var i = 0; i < 50; i++)
            probe.Consume(new PenReport { Position = Paths.Origin + new Vector2(i * 5f, 0f) });

        probe.Consume(new OutOfRangeReport());

        var landing = new Vector2(600f, 15000f);
        probe.Consume(new PenReport { Position = landing });

        Assert.Equal(landing, probe.Current);
    }
}
