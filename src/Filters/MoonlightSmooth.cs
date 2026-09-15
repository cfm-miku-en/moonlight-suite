using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight smooth")]
public class MoonlightSmooth : MoonlightFilter
{
    private Vector2 _position;

    [SliderProperty("latency (ms)", 0f, 20f, 2f), DefaultPropertyValue(2f)]
    [ToolTip("How much delay the smoothing adds while the pen is still. This number is the latency itself, so 2 means about 2 ms behind your hand. On a 133 hz tablet anything under 4 barely does anything.")]
    public float Latency { get; set; } = 2f;

    [BooleanProperty("adaptive", "Let go of the smoothing as the pen speeds up, so it only fights tremor and never eats into aim.")]
    [DefaultPropertyValue(true)]
    public bool Adaptive { get; set; } = true;

    [SliderProperty("full speed (mm/s)", 10f, 600f, 250f), DefaultPropertyValue(250f)]
    [ToolTip("Pen speed at which the smoothing is fully released. Ignored when adaptive is off.")]
    public float FullSpeed { get; set; } = 250f;

    [SliderProperty("release curve", 0.25f, 4f, 1f), DefaultPropertyValue(1f)]
    [ToolTip("Over 1 drops the smoothing off quickly as you speed up, under 1 holds onto it closer to full speed.")]
    public float ReleaseCurve { get; set; } = 1f;

    protected override void Reset(Vector2 position) => _position = position;

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var tau = Latency / 1000f;

        if (Adaptive && FullSpeed > 0f)
        {
            var release = 1f - Math.Clamp(Speed / FullSpeed, 0f, 1f);
            tau *= MathF.Pow(release, MathF.Max(ReleaseCurve, 0.01f));
        }

        _position = MoonMath.Damp(_position, input, deltaTime, tau);
        return _position;
    }
}
