using System;
using System.Linq;
using System.Numerics;
using MoonlightSuite.Core;
using Xunit;

namespace MoonlightSuite.Tests;

public class FilterTests
{
    [Theory]
    [InlineData(typeof(MoonlightPredict))]
    [InlineData(typeof(MoonlightAngle))]
    [InlineData(typeof(MoonlightSnap))]
    public void MaxOffsetIsNeverExceeded(Type type)
    {
        var filter = Catalog.Create(type);
        type.GetProperty("MaxOffset")!.SetValue(filter, 2f);

        var harness = new Harness(filter);
        var inputs = Paths.Hostile().ToList();
        var outputs = harness.Capture(inputs);
        var limit = 2f * TabletScale.Fallback;

        for (var i = 0; i < inputs.Count; i++)
        {
            var drift = Vector2.Distance(inputs[i], outputs[i]);

            Assert.True(drift <= limit + 0.01f, $"{type.Name} drifted {drift} units past a {limit} unit leash");
        }
    }

    [Fact]
    public void SmoothHoldsItsTimeConstantAcrossReportRates()
    {
        foreach (var rate in new[] { 133f, 266f, 500f, 1000f })
        {
            var measured = StepResponse(rate);

            Assert.True(MathF.Abs(measured - 0.005f) <= 1f / rate,
                $"at {rate} hz a 5 ms smoother settled in {measured * 1000f} ms");
        }
    }

    [Fact]
    public void AdaptiveSmoothingLetsGoAtSpeed()
    {
        var filter = new MoonlightSmooth { Latency = 20f, Adaptive = true, FullSpeed = 100f, ReleaseCurve = 1f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 50f, 400).ToList();
        var outputs = harness.Capture(inputs);

        Assert.Equal(inputs[^1], outputs[^1]);
    }

    [Fact]
    public void AdaptiveSmoothingStillFightsTremor()
    {
        var filter = new MoonlightSmooth { Latency = 20f, Adaptive = true, FullSpeed = 250f, ReleaseCurve = 1f };
        var harness = new Harness(filter);

        harness.Send(Paths.Origin);

        var outputs = harness.Capture(Enumerable.Range(0, 600)
            .Select(i => Paths.Origin + new Vector2(i % 2 == 0 ? 2f : -2f, 0f)));

        foreach (var output in outputs.Skip(200))
            Assert.True(MathF.Abs(output.X - Paths.Origin.X) < 0.5f,
                $"tremor of 2 units came through as {output.X - Paths.Origin.X}");
    }

    [Fact]
    public void HigherReleaseCurveLetsGoSooner()
    {
        var clingy = TrailingDistance(0.25f);
        var eager = TrailingDistance(4f);

        Assert.True(eager < clingy * 0.5f,
            $"a release curve of 4 trailed {eager} units where 0.25 trailed {clingy}");
    }

    [Fact]
    public void AimAtZeroStrengthIsPassThrough()
    {
        var filter = new MoonlightAim { Strength = 0f, Character = 0.5f, ReleaseSpeed = 60f };
        var harness = new Harness(filter);

        var inputs = Paths.Hostile().ToList();
        var outputs = harness.Capture(inputs);

        for (var i = 0; i < inputs.Count; i++)
            Assert.Equal(inputs[i], outputs[i]);
    }

    [Fact]
    public void AimDoesNothingAboveReleaseSpeed()
    {
        var filter = new MoonlightAim { Strength = 1f, Character = 0.5f, ReleaseSpeed = 50f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 50f, 400).ToList();
        var outputs = harness.Capture(inputs);

        Assert.Equal(inputs[^1], outputs[^1]);
    }

    [Fact]
    public void AimCharacterPicksBetweenHoldingAndSmoothing()
    {
        var circle = JitterSpread(0f);
        var smoothing = JitterSpread(1f);

        Assert.Equal(0f, circle);
        Assert.True(smoothing > 0f, "character 1 should smooth rather than hold, so the output has to move");
    }

    [Fact]
    public void NoiseHoldsInsideTheDeadzone()
    {
        var filter = new MoonlightNoise { Deadzone = 0.1f, Softness = 0f, SpeedRelax = 0f };
        var harness = new Harness(filter);

        harness.Send(Paths.Origin);

        for (var i = 0; i < 500; i++)
        {
            var jitter = new Vector2(i % 2 == 0 ? 3f : -3f, i % 3 == 0 ? 2f : -2f);

            Assert.Equal(Paths.Origin, harness.Send(Paths.Origin + jitter));
        }
    }

    [Fact]
    public void NoiseTracksOneToOneOutsideTheDeadzone()
    {
        var filter = new MoonlightNoise { Deadzone = 0.1f, Softness = 0f, SpeedRelax = 0f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 20f, 200).ToList();
        var outputs = harness.Capture(inputs);
        var trail = Vector2.Distance(inputs[^1], outputs[^1]);

        Assert.True(MathF.Abs(trail - 10f) < 0.01f, $"expected a 10 unit trail, got {trail}");
    }

    [Fact]
    public void NoiseSurvivesTheSettingsThatUsedToDivideByZero()
    {
        var filter = new MoonlightNoise { Deadzone = 0f, Softness = 0f, SpeedRelax = 0f };
        var harness = new Harness(filter);

        var inputs = Paths.Hostile().ToList();
        var outputs = harness.Capture(inputs);

        for (var i = 0; i < inputs.Count; i++)
            Assert.Equal(inputs[i], outputs[i]);
    }

    [Fact]
    public void PredictLeadsOnTheCurrentReportNotTheLastOne()
    {
        var filter = new MoonlightPredict { Lead = 5f, VelocitySmoothing = 0f, Acceleration = 0f, MaxOffset = 15f };
        var harness = new Harness(filter);

        harness.Send(Paths.Origin);

        var moved = Paths.Origin + new Vector2(10f, 0f);
        var lead = (harness.Send(moved) - moved).X;

        Assert.True(MathF.Abs(lead - 50f) < 0.5f, $"expected a 50 unit lead, got {lead}");
    }

    [Fact]
    public void AngleStraightensAnOffAxisStroke()
    {
        var result = StrokeAngle(4f, 3f);

        Assert.True(MathF.Abs(result) < 0.25f, $"stroke came out at {result} degrees");
    }

    [Fact]
    public void DivisionsActuallyChangeTheSnapTargets()
    {
        var loose = StrokeAngle(4f, 44f);
        var tight = StrokeAngle(8f, 44f);

        Assert.True(MathF.Abs(loose - 44f) < 0.25f, $"four divisions should have left a 44 degree stroke alone, got {loose}");
        Assert.True(MathF.Abs(tight - 45f) < 0.25f, $"eight divisions should have pulled it onto the diagonal, got {tight}");
    }

    [Fact]
    public void AngleDefaultsCorrectEnoughToNotice()
    {
        foreach (var offAxis in new[] { 2f, 5f, 10f })
        {
            var filter = new MoonlightAngle();
            var harness = new Harness(filter, 133f);
            var outputs = harness.Capture(Paths.Line(Paths.Origin, offAxis, 8f, 60));
            var travel = outputs[^1] - outputs[0];
            var residual = MathF.Abs(MathF.Atan2(travel.Y, travel.X) * (180f / MathF.PI));
            var corrected = 1f - residual / offAxis;

            Assert.True(corrected > 0.3f,
                $"a {offAxis} degree stroke was only {corrected * 100f} percent corrected at stock settings");
        }
    }

    [Fact]
    public void FollowNeverLagsPastMaxLag()
    {
        var filter = new MoonlightFollow { MaxLag = 1f, Deadzone = 0f, Smoothing = 30f, Leak = 0f };
        var harness = new Harness(filter, 133f);

        var inputs = Paths.Hostile().ToList();
        var outputs = harness.Capture(inputs);
        var ceiling = 1f * TabletScale.Fallback;

        for (var i = 0; i < inputs.Count; i++)
        {
            var lag = Vector2.Distance(inputs[i], outputs[i]);

            Assert.True(lag <= ceiling + 0.01f, $"cursor fell {lag} units behind a {ceiling} unit ceiling");
        }
    }

    [Fact]
    public void FollowLeakGivesUpTheCeilingForSmoothness()
    {
        static float PeakLag(float leak)
        {
            var filter = new MoonlightFollow { MaxLag = 0.2f, Deadzone = 0f, Smoothing = 30f, Leak = leak };
            var harness = new Harness(filter, 133f);

            var inputs = Paths.Line(Paths.Origin, 0f, 40f, 200).ToList();
            var outputs = harness.Capture(inputs);

            return inputs.Select((input, i) => Vector2.Distance(input, outputs[i])).Max();
        }

        Assert.True(PeakLag(0f) <= 20f + 0.01f, "a zero leak should hold the ceiling exactly");
        Assert.True(PeakLag(1f) > 20f, "a full leak should trade the ceiling for continued smoothing");
    }

    [Fact]
    public void SnapCarriesTheBoostForwardInsteadOfSpiking()
    {
        var filter = new MoonlightSnap { Threshold = 0f, Boost = 2f, Ramp = 0f, MaxOffset = 20f, Recenter = 500f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 10f, 100).ToList();
        var outputs = harness.Capture(inputs);
        var lead = outputs[^1].X - inputs[^1].X;

        Assert.True(lead > 50f, $"the boost only reached {lead} units");
        Assert.True(lead <= 2000f + 0.01f, $"the boost ran {lead} units past a 2000 unit leash");
    }

    [Fact]
    public void SnapReturnsToThePenWhenYouStop()
    {
        var filter = new MoonlightSnap { Threshold = 0f, Boost = 2f, Ramp = 0f, MaxOffset = 20f, Recenter = 120f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 10f, 100).ToList();
        harness.Run(inputs);

        var resting = inputs[^1];
        var output = harness.Run(Enumerable.Repeat(resting, 2000));
        var drift = Vector2.Distance(output, resting);

        Assert.True(drift < 1f, $"settled {drift} units off the pen");
    }

    [Fact]
    public void InterpolateAveragesItsWindow()
    {
        var filter = new MoonlightInterpolate { Window = 4f, Falloff = 1f };
        var harness = new Harness(filter);

        harness.Send(Vector2.Zero);

        var output = harness.Run(new[] { 100f, 200f, 300f, 400f }.Select(x => new Vector2(x, 0f)));

        Assert.True(MathF.Abs(output.X - 250f) < 0.01f, $"expected 250, got {output.X}");
    }

    [Fact]
    public void InterpolateSurvivesAZeroWindow()
    {
        var filter = new MoonlightInterpolate { Window = 0f, Falloff = 0f };
        var harness = new Harness(filter);

        var inputs = Paths.Hostile().ToList();
        var outputs = harness.Capture(inputs);

        for (var i = 0; i < inputs.Count; i++)
            Assert.Equal(inputs[i], outputs[i]);
    }

    [Fact]
    public void WobbleSwingsAcrossTheDirectionOfTravel()
    {
        var filter = new MoonlightWobble { Amplitude = 1f, Frequency = 10f };
        var harness = new Harness(filter);

        var inputs = Paths.Line(Paths.Origin, 0f, 10f, 400).ToList();
        var outputs = harness.Capture(inputs);
        var sideways = outputs.Select((output, i) => output.Y - inputs[i].Y).ToList();

        Assert.True(sideways.Max() > 90f, $"peak swing was only {sideways.Max()}");
        Assert.True(sideways.Min() < -90f, $"peak swing was only {sideways.Min()}");

        for (var i = 0; i < inputs.Count; i++)
            Assert.True(MathF.Abs(outputs[i].X - inputs[i].X) < 0.01f, "the swing leaked into the direction of travel");
    }

    [Fact]
    public void WobbleIgnoresAStationaryPen()
    {
        var filter = new MoonlightWobble { Amplitude = 5f, Frequency = 20f };
        var harness = new Harness(filter);

        foreach (var output in harness.Capture(Enumerable.Repeat(Paths.Origin, 500)))
            Assert.Equal(Paths.Origin, output);
    }

    private static float JitterSpread(float character)
    {
        var filter = new MoonlightAim { Strength = 1f, Character = character, ReleaseSpeed = 400f };
        var harness = new Harness(filter);

        harness.Send(Paths.Origin);

        var outputs = harness.Capture(Enumerable.Range(0, 400)
            .Select(i => Paths.Origin + new Vector2(i % 2 == 0 ? 3f : -3f, 0f)));

        var tail = outputs.Skip(200).Select(output => output.X).ToList();

        return tail.Max() - tail.Min();
    }

    private static float TrailingDistance(float releaseCurve)
    {
        var filter = new MoonlightSmooth
        {
            Latency = 20f,
            Adaptive = true,
            FullSpeed = 200f,
            ReleaseCurve = releaseCurve
        };

        var harness = new Harness(filter);
        var inputs = Paths.Line(Paths.Origin, 0f, 10f, 500).ToList();
        var outputs = harness.Capture(inputs);

        return Vector2.Distance(inputs[^1], outputs[^1]);
    }

    private static float StepResponse(float reportRate)
    {
        var filter = new MoonlightSmooth { Latency = 5f, Adaptive = false };
        var harness = new Harness(filter, reportRate);
        var target = new Vector2(1000f, 0f);

        harness.Send(Vector2.Zero);

        for (var i = 1; i <= (int)reportRate; i++)
            if (harness.Send(target).X >= 632f)
                return i / reportRate;

        return float.NaN;
    }

    private static float StrokeAngle(float divisions, float degrees)
    {
        var filter = new MoonlightAngle
        {
            Strength = 1f,
            Divisions = divisions,
            Tolerance = 20f,
            Rotation = 0f,
            SoftEdge = false,
            MinSpeed = 0f,
            MaxOffset = 20f
        };

        var harness = new Harness(filter);
        var outputs = harness.Capture(Paths.Line(Paths.Origin, degrees, 8f, 300));
        var travel = outputs[^1] - outputs[0];

        return MathF.Atan2(travel.Y, travel.X) * (180f / MathF.PI);
    }
}
