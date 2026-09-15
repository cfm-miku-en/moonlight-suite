using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight follow")]
public class MoonlightFollow : MoonlightFilter
{
    private const float Curve = 2f;

    private Vector2 _position;

    [SliderProperty("max lag (mm)", 0.05f, 5f, 1f), DefaultPropertyValue(1f)]
    [ToolTip("The furthest the cursor is ever allowed to fall behind the pen. The closer it gets to this, the harder it catches up, so this is a hard ceiling on lag rather than a target.")]
    public float MaxLag { get; set; } = 1f;

    [SliderProperty("deadzone (mm)", 0f, 1f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("Movement smaller than this produces no cursor motion at all. 0 leaves it off and lets the catch-up curve do the work, which is usually what you want.")]
    public float Deadzone { get; set; }

    [SliderProperty("smoothing (ms)", 0f, 30f, 10f), DefaultPropertyValue(10f)]
    [ToolTip("How heavily the cursor is smoothed while it is sitting close to the pen. This fades away as it falls behind.")]
    public float Smoothing { get; set; } = 10f;

    [SliderProperty("leak", 0f, 1f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("How much smoothing survives past max lag instead of the cursor snapping back to the ceiling. 0 gives a hard ceiling, higher trades that guarantee for a softer feel at speed.")]
    public float Leak { get; set; }

    protected override void Reset(Vector2 position) => _position = position;

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var outer = ToUnits(MathF.Max(MaxLag, 0.001f));
        var inner = MathF.Min(ToUnits(Deadzone), outer);

        var offset = input - _position;
        var distance = offset.Length();

        if (distance <= inner)
            return _position;

        var reach = Math.Clamp((distance - inner) / MathF.Max(outer - inner, MoonMath.Epsilon), 0f, 1f);
        var falloff = MathF.Max(MathF.Pow(1f - reach, Curve), Math.Clamp(Leak, 0f, 1f));

        _position = MoonMath.Damp(_position, input, deltaTime, Smoothing * falloff / 1000f);
        return _position;
    }
}
