using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace MoonlightSuite.Tests;

public class ResampleTests
{
    private const float Interval = 1f / 133f;
    private const float Tick = Interval / 8f;
    private const float Step = 40f;

    private sealed class Probe : MoonlightResample
    {
        public void Seed(Vector2 position) => Reset(position);

        public void Feed(Vector2 position) => SetTarget(position, Interval);

        public new Vector2 Advance(float deltaTime) => base.Advance(deltaTime);
    }

    private static Probe Running(float latency, float maxLead, string mode, out Vector2 newest)
    {
        var probe = new Probe { Latency = latency, MaxLead = maxLead, Mode = mode };
        probe.Seed(Paths.Origin);

        newest = Paths.Origin;

        for (var report = 0; report < 20; report++)
        {
            newest += new Vector2(Step, 0f);
            probe.Feed(newest);

            for (var tick = 0; tick < 8; tick++)
                probe.Advance(Tick);
        }

        return probe;
    }

    [Fact]
    public void FullLatencyOnlyEverDrawsBetweenReadingsItHas()
    {
        var probe = new Probe { Latency = 8f, MaxLead = 5f, Mode = MoonlightResample.LinearMode };
        probe.Seed(Paths.Origin);

        var newest = Paths.Origin;

        for (var report = 0; report < 20; report++)
        {
            newest += new Vector2(Step, 0f);
            probe.Feed(newest);

            for (var tick = 0; tick < 8; tick++)
            {
                var output = probe.Advance(Tick);

                Assert.True(output.X <= newest.X + 0.01f,
                    $"cursor reached {output.X} past a newest reading of {newest.X} while reports were still arriving");
            }
        }
    }

    [Fact]
    public void LowLatencyGuessesAheadButNeverPastMaxLead()
    {
        var probe = Running(0f, 5f, MoonlightResample.LinearMode, out var newest);
        var ceiling = newest.X + 0.005f / Interval * Step + 0.01f;
        var reached = newest.X;

        for (var tick = 0; tick < 40; tick++)
            reached = MathF.Max(reached, probe.Advance(Tick).X);

        Assert.True(reached > newest.X, "a zero latency resampler should be placing the cursor ahead of the newest reading");
        Assert.True(reached <= ceiling, $"cursor ran to {reached} past a {ceiling} ceiling");
    }

    [Fact]
    public void StalledReportsDoNotRunAway()
    {
        foreach (var mode in new[] { MoonlightResample.LinearMode, MoonlightResample.CurvedMode })
        {
            var probe = Running(0f, 5f, mode, out var newest);
            var ceiling = newest.X + 0.005f / Interval * Step + 0.01f;

            for (var tick = 0; tick < 4000; tick++)
            {
                var output = probe.Advance(Tick);

                Assert.True(MoonMath.IsFinite(output), $"{mode} went non-finite after reports stopped");
                Assert.True(output.X <= ceiling, $"{mode} drifted to {output.X} after reports stopped");
            }
        }
    }

    [Fact]
    public void CurvedModeStillLandsOnTheReadingsItIsGiven()
    {
        var probe = new Probe { Latency = 0f, MaxLead = 0f, Mode = MoonlightResample.CurvedMode };
        probe.Seed(Paths.Origin);

        var newest = Paths.Origin;

        for (var report = 0; report < 6; report++)
        {
            newest += new Vector2(Step, 0f);
            probe.Feed(newest);

            var output = probe.Advance(1e-5f);

            Assert.True(Vector2.Distance(output, newest) < 0.5f,
                $"curved mode put the cursor at {output} when the newest reading was {newest}");
        }
    }

    [Fact]
    public void RestartsWhereThePenComesBackDown()
    {
        var probe = new Probe { Latency = 8f, MaxLead = 5f, Mode = MoonlightResample.LinearMode };
        probe.IntegrationClock = new ManualTimeSource(Tick);
        probe.ReportClock = new ManualTimeSource(Interval);

        for (var i = 0; i < 30; i++)
            probe.Consume(new PenReport { Position = Paths.Origin + new Vector2(i * Step, 0f) });

        probe.Consume(new OutOfRangeReport());

        var landing = new Vector2(900f, 15000f);
        probe.Consume(new PenReport { Position = landing });

        Assert.Equal(landing, probe.Advance(1e-5f));
    }
}
