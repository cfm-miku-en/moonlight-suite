using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight interpolate")]
public class MoonlightInterpolate : MoonlightFilter
{
    private const int MaxWindow = 32;

    private readonly RingBuffer<Vector2> _samples = new(MaxWindow);

    [SliderProperty("window", 1f, MaxWindow, 5f), DefaultPropertyValue(5f)]
    [ToolTip("How many recent reports get averaged together. Bigger is smoother and laggier, and because it counts reports rather than time, the feel changes if you change your tablet's report rate.")]
    public float Window { get; set; } = 5f;

    [SliderProperty("falloff", 0.1f, 1f, 1f), DefaultPropertyValue(1f)]
    [ToolTip("1 weighs every report in the window the same. Lower leans on the newest reports, which cuts the lag but smooths less.")]
    public float Falloff { get; set; } = 1f;

    protected override void Reset(Vector2 position) => _samples.Fill(position);

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        _samples.Add(input);

        var window = Math.Clamp((int)MathF.Round(Window), 1, MaxWindow);
        var count = Math.Min(window, _samples.Count);
        var falloff = Math.Clamp(Falloff, 0.1f, 1f);
        var sum = Vector2.Zero;
        var total = 0f;
        var weight = 1f;

        for (var i = 0; i < count; i++)
        {
            sum += _samples[i] * weight;
            total += weight;
            weight *= falloff;
        }

        return total > 0f ? sum / total : input;
    }
}
