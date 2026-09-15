using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight predict")]
public class MoonlightPredict : MoonlightFilter
{
    private Vector2 _velocity;
    private Vector2 _acceleration;

    [SliderProperty("lead (ms)", 0f, 15f, 2f), DefaultPropertyValue(2f)]
    [ToolTip("How far ahead of the pen to place the cursor. Roughly cancels this many milliseconds of latency from elsewhere in your chain, at the cost of overshooting direction changes.")]
    public float Lead { get; set; } = 2f;

    [SliderProperty("velocity smoothing (ms)", 0f, 20f, 4f), DefaultPropertyValue(4f)]
    [ToolTip("Steadies the speed estimate the prediction runs on. Too low and the cursor jitters ahead of you, too high and it keeps predicting through corners. On a 133 hz tablet anything under 8 does nothing.")]
    public float VelocitySmoothing { get; set; } = 4f;

    [SliderProperty("acceleration", 0f, 1f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("Also lead on how quickly your speed is changing. Helps on flowing curves, makes sharp corners overshoot more.")]
    public float Acceleration { get; set; }

    [SliderProperty("max offset (mm)", 0f, 15f, 4f), DefaultPropertyValue(4f)]
    [ToolTip("Hard limit on how far ahead the cursor is ever allowed to sit. 0 disables the prediction entirely.")]
    public float MaxOffset { get; set; } = 4f;

    protected override void Reset(Vector2 position)
    {
        _velocity = Vector2.Zero;
        _acceleration = Vector2.Zero;
    }

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        var tau = VelocitySmoothing / 1000f;
        var measured = Movement / deltaTime;
        var velocity = MoonMath.Damp(_velocity, measured, deltaTime, tau);

        _acceleration = MoonMath.Damp(_acceleration, (velocity - _velocity) / deltaTime, deltaTime, tau);
        _velocity = velocity;

        var lead = Lead / 1000f;
        var offset = _velocity * lead;

        if (Acceleration > 0f)
            offset += _acceleration * (0.5f * lead * lead * Acceleration);

        return input + MoonMath.ClampLength(offset, ToUnits(MaxOffset));
    }
}
