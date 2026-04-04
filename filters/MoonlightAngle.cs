using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

[PluginName("moonlight angle")] // obtuse or acute
public class MoonlightAngle : IPositionedPipelineElement<IDeviceReport>
{
    private Vector2 _last;

#pragma warning disable CS8618 // STOP HANDICAPPING THE ERROR CODES
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("correction strength", 0f, 1f), DefaultPropertyValue(0.2f)] // ERM 1+1 IS NOT 11, IT IS 2!!
    public float CorrectionStrength { get; set; } = 0.2f;

    [SliderProperty("angle snap threshold (degrees)", 0f, 45f), DefaultPropertyValue(15f)] // ahh its 15 degrees outside it should be SNOWING!!
    public float AngleThreshold { get; set; } = 15f;

    [BooleanProperty("snap to diagonals", "includes 45 degree angles in snap targets")] // shoot the target 45 degrees with a 45mm
    public bool SnapToDiagonals { get; set; } = true;

    [BooleanProperty("snap to cardinals only", "only snaps to horizontal and vertical")]
    public bool CardinalOnly { get; set; } = false;

    [SliderProperty("min movement", 0f, 10f), DefaultPropertyValue(0.5f)]
    public float MinMovement { get; set; } = 0.5f;

    public void Consume(IDeviceReport report)
    {
        if (report is IAbsolutePositionReport pos)
        {
            pos.Position = Filter(pos.Position);
        }
        Emit.Invoke(report);
    }

    private Vector2 Filter(Vector2 input)
    {
        var delta = input - _last;
        if (delta.Length() < MinMovement)
        {
            _last = input;
            return input;
        }

        var angle = Math.Atan2(delta.Y, delta.X) * (180.0 / Math.PI);

        double[] snapAngles;
        if (CardinalOnly)
            snapAngles = new double[] { 0, 90, 180, -90, -180 };
        else if (SnapToDiagonals)
            snapAngles = new double[] { 0, 45, 90, 135, 180, -45, -90, -135, -180 };
        else
            snapAngles = new double[] { 0, 90, 180, -90, -180, 45, -45, 135, -135 };

        double nearest = snapAngles[0];
        double minDiff = Math.Abs(angle - snapAngles[0]);
        foreach (var snap in snapAngles)
        {
            var diff = Math.Abs(angle - snap);
            if (diff < minDiff) { minDiff = diff; nearest = snap; }
        }

        Vector2 result = input;
        if (minDiff < AngleThreshold)
        {
            var rad = nearest * (Math.PI / 180.0);
            var len = delta.Length();
            var snapped = new Vector2(
                (float)(Math.Cos(rad) * len),
                (float)(Math.Sin(rad) * len)
            );
            result = _last + Vector2.Lerp(delta, snapped, CorrectionStrength);
        }

        _last = input;
        return result;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}