using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timing;

[PluginName("moonlight smooth")]
public class MoonlightSmooth : IPositionedPipelineElement<IDeviceReport>
{
#pragma warning disable CS8618
    public event Action<IDeviceReport> Emit;
#pragma warning restore CS8618

    [SliderProperty("strength", 0f, 0.98f), DefaultPropertyValue(0.5f)] // this is my property you may not buy it i own it and if you wanna buy it it is $150,000 on zillow
    public float Strength { get; set; } = 0.5f;

    [BooleanProperty("damp low movement", "extra damping when moving slowly")] // damp move slow, slow move damp, damp move you, you move damp, damp move damp, you move you
    public bool DampLowMovement { get; set; } = false;

    [SliderProperty("damp threshold", 0f, 20f), DefaultPropertyValue(5f)]
    public float DampThreshold { get; set; } = 5f;

    [BooleanProperty("sine mode", "adds a sine wave wobble to your cursor path")]
    public bool SineMode { get; set; } = false;

    [SliderProperty("sine amplitude", 0f, 200f), DefaultPropertyValue(10f)]
    public float SineAmplitude
    {
        get => _sineAmplitude;
        set => _sineAmplitude = Math.Clamp(value, 0f, 200f);
    }
    private float _sineAmplitude = 10f;

    [SliderProperty("sine frequency", 0f, 50f), DefaultPropertyValue(5f)]
    public float SineFrequency
    {
        get => _sineFrequency;
        set => _sineFrequency = Math.Clamp(value, 0f, 50f);
    }
    private float _sineFrequency = 5f;

    [BooleanProperty("elastic amplitude", "scales wobble with movement speed")]
    public bool ElasticAmplitude { get; set; } = false;

    [BooleanProperty("elastic frequency", "scales frequency with movement speed")]
    public bool ElasticFrequency { get; set; } = false;

    private Vector2 _last;
    private Vector2 _prevRadial;
    private Vector2 _prevPos;
    private float _phase = 0f;
    private HPETDeltaStopwatch _stopwatch = new HPETDeltaStopwatch();

    private bool IsFinite(Vector2 v) => float.IsFinite(v.X) & float.IsFinite(v.Y);

    public void Consume(IDeviceReport report)
    {
        if (report is IAbsolutePositionReport pos)
        {
            pos.Position = ApplySmooth(pos.Position);
        }

        if (SineMode && report is ITabletReport tab)
        {
            if (!IsFinite(_prevRadial)) _prevRadial = tab.Position;
            if (!IsFinite(_prevPos)) _prevPos = tab.Position;

            var deltaRadial = tab.Position - _prevRadial;
            var delta = tab.Position - _prevPos;

            _phase = (_phase + (float)_stopwatch.Restart().TotalSeconds * _sineFrequency * (ElasticFrequency ? delta.Length() / Math.Max(_sineAmplitude, 1f) : 1f)) % 1f;

            _prevRadial += Vector2.Normalize(deltaRadial) * Math.Max(deltaRadial.Length() - _sineAmplitude, 0f);
            deltaRadial = tab.Position - _prevRadial;
            _prevPos = tab.Position;

            var perpSource = ElasticAmplitude ? delta : deltaRadial;
            if (perpSource.Length() > 0.001f)
            {
                tab.Position += MathF.Sin(_phase * MathF.PI * 2f)
                    * Vector2.Transform(perpSource, Matrix3x2.CreateRotation(MathF.PI / 2f));
            }
        }

        Emit.Invoke(report);
    }

    private Vector2 ApplySmooth(Vector2 input)
    {
        float str = Math.Clamp(Strength, 0f, 0.98f);

        if (DampLowMovement)
        {
            float dist = Vector2.Distance(input, _last);
            if (dist < DampThreshold)
                str = Math.Clamp(str * (dist / Math.Max(DampThreshold, 0.001f)), 0f, 0.98f);
        }

        _last = Vector2.Lerp(input, _last, str);
        return _last;
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
}
