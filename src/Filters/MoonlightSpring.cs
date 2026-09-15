using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight spring")]
public class MoonlightSpring : MoonlightAsyncFilter
{
    private const float MaxStep = 1f / 2000f;
    private const int MaxSteps = 64;

    private Vector2 _position;
    private Vector2 _velocity;
    private Vector2 _target;

    [SliderProperty("stiffness (hz)", 0.5f, 40f, 10f), DefaultPropertyValue(10f)]
    [ToolTip("How eagerly the cursor chases the pen. Low is loose and floaty, high is tight.")]
    public float Stiffness { get; set; } = 10f;

    [SliderProperty("damping ratio", 0.1f, 4f, 1f), DefaultPropertyValue(1f)]
    [ToolTip("1 catches up as fast as it can without overshooting. Below 1 bounces past and wobbles back, above 1 is sluggish.")]
    public float Damping { get; set; } = 1f;

    [SliderProperty("settle (mm)", 0f, 1f, 0.05f), DefaultPropertyValue(0.05f)]
    [ToolTip("Once the cursor is this close to the pen and barely moving it just parks there, instead of creeping the last fraction of a unit forever.")]
    public float Settle { get; set; } = 0.05f;

    protected override void Reset(Vector2 position)
    {
        _position = position;
        _target = position;
        _velocity = Vector2.Zero;
    }

    protected override void SetTarget(Vector2 position, float reportInterval) => _target = position;

    protected override Vector2 Advance(float deltaTime)
    {
        deltaTime = MathF.Min(deltaTime, MaxSteps * MaxStep);

        var frequency = MathF.Max(Stiffness, 0.01f) * MathF.Tau;
        var damping = MathF.Max(Damping, 0f);
        var steps = Math.Clamp((int)MathF.Ceiling(deltaTime / MaxStep), 1, MaxSteps);
        var step = deltaTime / steps;

        for (var i = 0; i < steps; i++)
        {
            var acceleration = (_target - _position) * (frequency * frequency) - _velocity * (2f * damping * frequency);

            _velocity += acceleration * step;
            _position += _velocity * step;
        }

        var settle = ToUnits(Settle);

        if (settle > 0f && Vector2.Distance(_position, _target) < settle && _velocity.Length() < settle * frequency)
        {
            _position = _target;
            _velocity = Vector2.Zero;
        }

        return _position;
    }
}
