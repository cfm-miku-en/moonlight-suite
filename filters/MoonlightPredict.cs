using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

[PluginName("moonlight predict")]
public class MoonlightPredict : IPositionedPipelineElement<IDeviceReport>
{
    private Vector2 _prev;
    private Vector2 _last;
    private bool _hasTwo;

#pragma warning disable CS8618
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("prediction strength", 0f, 1f), DefaultPropertyValue(0.3f)] // strength under 0.3 = you are retarbed   strength over 0.3 = smart prediction simpsons
    public float PredictionStrength { get; set; } = 0.3f;

    [SliderProperty("max prediction distance", 1f, 100f), DefaultPropertyValue(30f)]
    public float MaxPredictionDistance { get; set; } = 30f;

    [BooleanProperty("clamp prediction", "prevents prediction from overshooting too far")]
    public bool ClampPrediction { get; set; } = true;

    [SliderProperty("acceleration scale", 0f, 2f), DefaultPropertyValue(1f)]
    public float AccelerationScale { get; set; } = 1f;

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
        if (_hasTwo)
        {
            var delta = (_last - _prev) * AccelerationScale;
            var predicted = input + delta * PredictionStrength;

            if (ClampPrediction)
            {
                var predDelta = predicted - input;
                if (predDelta.Length() > MaxPredictionDistance)
                    predicted = input + Vector2.Normalize(predDelta) * MaxPredictionDistance;
            }

            _prev = _last;
            _last = input;
            return predicted;
        }

        if (_last != Vector2.Zero)
        {
            _prev = _last;
            _hasTwo = true;
        }

        _last = input;
        return input;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}