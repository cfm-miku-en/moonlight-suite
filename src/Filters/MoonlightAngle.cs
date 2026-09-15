using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight angle")]
public class MoonlightAngle : MoonlightFilter
{
    private const float MinDivisions = 2f;
    private const float MaxDivisions = 36f;
    private const float SoftEdgeKnee = 0.6f;

    private Vector2 _position;

    [SliderProperty("strength", 0f, 1f, 0.5f), DefaultPropertyValue(0.5f)]
    [ToolTip("How hard a stroke gets pulled onto the nearest direction. 1 locks it completely.")]
    public float Strength { get; set; } = 0.5f;

    [SliderProperty("divisions", MinDivisions, MaxDivisions, 12f), DefaultPropertyValue(12f)]
    [ToolTip("How many directions to snap to. 4 is horizontal and vertical, 8 adds the diagonals, 12 is every 30 degrees, 36 is every 10. Every stroke gets corrected once tolerance reaches 180 divided by this, which is why the stock pairing is 12 and 15.")]
    public float Divisions { get; set; } = 12f;

    [SliderProperty("tolerance (deg)", 0f, 45f, 15f), DefaultPropertyValue(15f)]
    [ToolTip("How far off a direction a stroke can be and still get corrected.")]
    public float Tolerance { get; set; } = 15f;

    [SliderProperty("rotation (deg)", -45f, 45f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("Turns the whole set of snap directions, for when your tablet sits at an angle.")]
    public float Rotation { get; set; }

    [BooleanProperty("soft edge", "Hold full correction through most of the tolerance, then fade it out near the edge so strokes do not pop as they cross the boundary.")]
    [DefaultPropertyValue(true)]
    public bool SoftEdge { get; set; } = true;

    [SliderProperty("min speed (mm/s)", 0f, 200f, 10f), DefaultPropertyValue(10f)]
    [ToolTip("Leaves slow movement alone, so resting the pen does not get dragged onto an axis.")]
    public float MinSpeed { get; set; } = 10f;

    [SliderProperty("max offset (mm)", 0f, 20f, 3f), DefaultPropertyValue(3f)]
    [ToolTip("How far the corrected cursor may drift from where the pen really is before it gets reeled back in. On a long stroke this is what stops the correction, so raise it if straightening gives up partway.")]
    public float MaxOffset { get; set; } = 3f;

    protected override void Reset(Vector2 position) => _position = position;

    protected override Vector2 Filter(Vector2 input, float deltaTime)
    {
        _position += Correct(Movement);
        _position = input + MoonMath.ClampLength(_position - input, ToUnits(MaxOffset));

        return _position;
    }

    private Vector2 Correct(Vector2 delta)
    {
        if (Strength <= 0f || Speed < MinSpeed || delta.LengthSquared() <= 0f)
            return delta;

        var step = 360f / Math.Clamp(MathF.Round(Divisions), MinDivisions, MaxDivisions);
        var angle = MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI);
        var nearest = MathF.Round((angle - Rotation) / step) * step + Rotation;
        var error = MoonMath.WrapDegrees(angle - nearest);

        if (MathF.Abs(error) >= Tolerance)
            return delta;

        var amount = Strength;

        if (SoftEdge && Tolerance > 0f)
        {
            var reach = MathF.Abs(error) / Tolerance;
            amount *= 1f - MoonMath.SmoothStep((reach - SoftEdgeKnee) / (1f - SoftEdgeKnee));
        }

        return MoonMath.Rotate(delta, -error * amount * (MathF.PI / 180f));
    }
}
