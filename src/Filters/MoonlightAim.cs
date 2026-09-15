using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight aim")]
public class MoonlightAim : MoonlightFilter
{
    private const float MaxDeadzone = 0.12f;
    private const float MaxLatency = 24f;
    private const float ReleaseCurve = 0.6f;
    private const float Softness = 1f;

    private Vector2 _held;
    private Vector2 _position;

    [SliderProperty("strength", 0f, 1f, 0.5f), DefaultPropertyValue(0.5f)]
    [ToolTip("How much help you get while the pen is slow. 0 is untouched input. Turn it up until tremor stops, then stop.")]
    public float Strength { get; set; } = 0.5f;

    [SliderProperty("release speed (mm/s)", 10f, 400f, 60f), DefaultPropertyValue(60f)]
    [ToolTip("Pen speed at which every bit of help switches off. Above this the filter does nothing at all, so set it just under the speed you actually move between notes.")]
    public float ReleaseSpeed { get; set; } = 60f;

    [SliderProperty("character", 0f, 1f, 0.5f), DefaultPropertyValue(0.5f)]
    [ToolTip("Which kind of help. Towards 0 is a follow circle, which costs no latency but can feel sticky on tiny deliberate nudges. Towards 1 is smoothing, which never sticks but lags. Middle is a bit of both.")]
    public float Character { get; set; } = 0.5f;

    protected override void Reset(Vector2 position)
    {
        _held = position;
        _position = position;
    }

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var strength = Math.Clamp(Strength, 0f, 1f);
        var character = Math.Clamp(Character, 0f, 1f);
        var release = MathF.Pow(1f - Math.Clamp(Speed / MathF.Max(ReleaseSpeed, 1f), 0f, 1f), ReleaseCurve);

        var radius = ToUnits(MaxDeadzone * strength * (1f - character)) * release;
        var latency = MaxLatency * strength * character * release;

        _held = Settle(_held, input, radius);
        _position = MoonMath.Damp(_position, _held, deltaTime, latency / 1000f);

        return _position;
    }

    private static Vector2 Settle(Vector2 held, Vector2 input, float radius)
    {
        var offset = input - held;
        var excess = offset.Length() - radius;

        if (excess <= 0f)
            return held;

        if (radius > 0f)
            excess *= Math.Clamp(excess / (radius * Softness), 0f, 1f);

        return held + MoonMath.SafeNormalize(offset) * excess;
    }
}
