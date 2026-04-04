using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

[PluginName("moonlight interpolate")] // interpolar bear
public class MoonlightInterpolate : IPositionedPipelineElement<IDeviceReport>
{
    private Queue<Vector2> _samples = new();

#pragma warning disable CS8618
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("samples", 1f, 30f), DefaultPropertyValue(5f)]
    public float Samples { get; set; } = 5f;

    [BooleanProperty("weighted average", "recent samples count more")]
    public bool WeightedAverage { get; set; } = false;

    [SliderProperty("weight falloff", 0.1f, 1f), DefaultPropertyValue(0.7f)]
    public float WeightFalloff { get; set; } = 0.7f;

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
        _samples.Enqueue(input);
        while (_samples.Count > (int)Samples)
            _samples.Dequeue();

        if (!WeightedAverage)
        {
            var avg = _samples.Aggregate(Vector2.Zero, (a, b) => a + b);
            return avg / _samples.Count;
        }

        var arr = _samples.ToArray();
        Vector2 weightedSum = Vector2.Zero;
        float totalWeight = 0f;
        float weight = 1f;

        for (int i = arr.Length - 1; i >= 0; i--)
        {
            weightedSum += arr[i] * weight;
            totalWeight += weight;
            weight *= WeightFalloff;
        }

        return weightedSum / totalWeight;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}