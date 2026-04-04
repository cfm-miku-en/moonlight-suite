using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

[PluginName("moonlight snap")]
public class MoonlightSnap : IPositionedPipelineElement<IDeviceReport>
{
    private Vector2 _last;

#pragma warning disable CS8618
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("speed threshold", 1f, 200f), DefaultPropertyValue(20f)] // OH MY GOD ISHOWSPEED W SPEED ISHOWSPEED HUIHASIUS
    public float SpeedThreshold { get; set; } = 20f;

    [SliderProperty("boost multiplier", 1f, 5f), DefaultPropertyValue(1.5f)]
    public float BoostMultiplier { get; set; } = 1.5f;

    [SliderProperty("boost decay", 0f, 1f), DefaultPropertyValue(0.8f)]
    public float BoostDecay { get; set; } = 0.8f;

    [BooleanProperty("soft boost", "smoothly ramps up instead of hard snap")]
    public bool SoftBoost { get; set; } = false;

    [SliderProperty("soft boost range", 1f, 100f), DefaultPropertyValue(30f)]
    public float SoftBoostRange { get; set; } = 30f;

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
        var speed = delta.Length();
        Vector2 result;

        if (SoftBoost)
        {
            float t = Math.Clamp((speed - SpeedThreshold) / SoftBoostRange, 0f, 1f);
            float multiplier = 1f + (BoostMultiplier - 1f) * t;
            result = speed > SpeedThreshold ? _last + delta * multiplier : input;
        }
        else
        {
            result = speed > SpeedThreshold ? _last + delta * BoostMultiplier : input;
        }

        _last = input;
        return result;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}