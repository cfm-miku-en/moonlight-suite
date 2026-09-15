using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight snap")]
public class MoonlightSnap : MoonlightFilter
{
    private Vector2 _position;

    [SliderProperty("threshold (mm/s)", 0f, 500f, 120f), DefaultPropertyValue(120f)]
    [ToolTip("Pen speed the boost starts at. Below this the cursor tracks normally.")]
    public float Threshold { get; set; } = 120f;

    [SliderProperty("boost", 1f, 4f, 1.5f), DefaultPropertyValue(1.5f)]
    [ToolTip("How much further than your hand the cursor travels once the boost is in.")]
    public float Boost { get; set; } = 1.5f;

    [SliderProperty("ramp (mm/s)", 0f, 500f, 150f), DefaultPropertyValue(150f)]
    [ToolTip("How much extra speed past the threshold it takes to reach the full boost. 0 switches it on all at once.")]
    public float Ramp { get; set; } = 150f;

    [SliderProperty("max offset (mm)", 0f, 20f, 5f), DefaultPropertyValue(5f)]
    [ToolTip("How far the boosted cursor may run from where the pen really is.")]
    public float MaxOffset { get; set; } = 5f;

    [SliderProperty("recenter (ms)", 0f, 500f, 120f), DefaultPropertyValue(120f)]
    [ToolTip("How quickly the cursor settles back onto the real pen position once you slow down.")]
    public float Recenter { get; set; } = 120f;

    protected override void Reset(Vector2 position) => _position = position;

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        _position += Movement * Gain();
        _position = MoonMath.Damp(_position, input, deltaTime, Recenter / 1000f);
        _position = input + MoonMath.ClampLength(_position - input, ToUnits(MaxOffset));

        return _position;
    }

    private float Gain()
    {
        if (Speed <= Threshold)
            return 1f;

        var reached = Ramp > 0f ? Math.Clamp((Speed - Threshold) / Ramp, 0f, 1f) : 1f;
        return 1f + (Boost - 1f) * reached;
    }
}
