using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight noise")]
public class MoonlightNoise : MoonlightFilter
{
    private const float RelaxReference = 200f;

    private Vector2 _position;

    [SliderProperty("deadzone (mm)", 0f, 1f, 0.05f), DefaultPropertyValue(0.05f)]
    [ToolTip("Movement smaller than this is treated as sensor noise and thrown away. Pen chatter and hand tremor usually sit under 0.1 mm. Past the edge of the deadzone the cursor tracks one to one, so this costs no latency.")]
    public float Deadzone { get; set; } = 0.05f;

    [SliderProperty("softness", 0f, 4f, 1f), DefaultPropertyValue(1f)]
    [ToolTip("Eases the cursor out of the deadzone rather than releasing it all at once, which stops slow movement looking like stairs. 0 is a hard edge.")]
    public float Softness { get; set; } = 1f;

    [SliderProperty("speed relax", 0f, 1f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("Shrinks the deadzone as the pen speeds up so it leaves fast aim completely alone.")]
    public float SpeedRelax { get; set; }

    protected override void Reset(Vector2 position) => _position = position;

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var radius = ToUnits(Deadzone);

        if (SpeedRelax > 0f)
            radius /= 1f + SpeedRelax * Speed / RelaxReference;

        var offset = input - _position;
        var excess = offset.Length() - radius;

        if (excess <= 0f)
            return _position;

        if (Softness > 0f && radius > 0f)
            excess *= Math.Clamp(excess / (radius * Softness), 0f, 1f);

        _position += MoonMath.SafeNormalize(offset) * excess;
        return _position;
    }
}
