using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight wobble")]
public class MoonlightWobble : MoonlightFilter
{
    private const float SpeedReference = 200f;

    private Vector2 _heading;
    private float _phase;

    [SliderProperty("amplitude (mm)", 0f, 20f, 2f), DefaultPropertyValue(2f)]
    [ToolTip("How far sideways the cursor swings off your actual path.")]
    public float Amplitude { get; set; } = 2f;

    [SliderProperty("frequency (hz)", 0f, 50f, 8f), DefaultPropertyValue(8f)]
    [ToolTip("How many full swings per second.")]
    public float Frequency { get; set; } = 8f;

    [BooleanProperty("elastic amplitude", "Scale the swing with how fast the pen is moving, so it settles down when you stop.")]
    public bool ElasticAmplitude { get; set; }

    [BooleanProperty("elastic frequency", "Speed the swing up as the pen moves faster.")]
    public bool ElasticFrequency { get; set; }

    protected override void Reset(Vector2 position)
    {
        _heading = Vector2.Zero;
        _phase = 0f;
    }

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var heading = MoonMath.SafeNormalize(Movement);

        if (heading != Vector2.Zero)
            _heading = heading;

        if (_heading == Vector2.Zero)
            return input;

        var reached = Math.Clamp(Speed / SpeedReference, 0f, 1f);
        var frequency = ElasticFrequency ? Frequency * reached : Frequency;
        var amplitude = ToUnits(Amplitude) * (ElasticAmplitude ? reached : 1f);

        _phase = (_phase + deltaTime * frequency) % 1f;

        return input + MoonMath.Perpendicular(_heading) * (amplitude * MathF.Sin(_phase * MathF.Tau));
    }
}
