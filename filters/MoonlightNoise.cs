using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

[PluginName("moonlight noise")]
public class MoonlightNoise : IPositionedPipelineElement<IDeviceReport> 
{
    private Vector2 _last;

#pragma warning disable CS8618
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("threshold", 0f, 20f), DefaultPropertyValue(2f)]
    public float Threshold { get; set; } = 2f;

    [SliderProperty("release multiplier", 1f, 5f), DefaultPropertyValue(1.5f)]
    public float ReleaseMultiplier { get; set; } = 1.5f;

    [BooleanProperty("scale with speed", "reduces threshold when moving fast")]
    public bool ScaleWithSpeed { get; set; } = false;

    [SliderProperty("speed scale factor", 0.1f, 1f), DefaultPropertyValue(0.5f)]
    public float SpeedScaleFactor { get; set; } = 0.5f;

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
        float dist = Vector2.Distance(input, _last);
        float threshold = Threshold;

        if (ScaleWithSpeed)
            threshold *= Math.Max(1f - dist * SpeedScaleFactor * 0.1f, 0.1f);

        if (dist < threshold)
            return _last;

        if (dist > threshold * ReleaseMultiplier)
        {
            _last = input;
            return input;
        }

        _last = Vector2.Lerp(_last, input, (dist - threshold) / (threshold * (ReleaseMultiplier - 1f)));
        return _last;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}