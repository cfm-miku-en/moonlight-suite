using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timing;

[PluginName("moonlight spring")]
public class MoonlightSpring : AsyncPositionedPipelineElement<IDeviceReport>
{
    private Vector2 _pos;
    private Vector2 _vel;
    private Vector2 _target;
    private uint _pressure;
    private HPETDeltaStopwatch _stopwatch = new HPETDeltaStopwatch();

    [SliderProperty("tension", 0.001f, 10f), DefaultPropertyValue(2f)]
    public float Tension
    {
        get => _tension;
        set => _tension = Math.Max(value, 0.001f);
    }
    private float _tension = 2f;

    [SliderProperty("friction", 0.001f, 10f), DefaultPropertyValue(3f)]
    public float Friction
    {
        get => _friction;
        set => _friction = Math.Max(value, 0.001f);
    }
    private float _friction = 3f;

    [SliderProperty("time scale", 1f, 5000f), DefaultPropertyValue(40f)]
    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Max(value, 1f);
    }
    private float _timeScale = 40f;

    [BooleanProperty("snap to target", "instantly snaps when close enough to target")]
    public bool SnapToTarget { get; set; } = true;

    [SliderProperty("snap threshold", 0f, 5f), DefaultPropertyValue(0.5f)]
    public float SnapThreshold { get; set; } = 0.5f;

    [BooleanProperty("preserve pressure", "passes through pen pressure unchanged")]
    public bool PreservePressure { get; set; } = true;

    protected override void ConsumeState()
    {
        if (State is not ITabletReport report) return;
        _target = report.Position;
        _pressure = report.Pressure;
        Tick((float)_stopwatch.Restart().TotalMilliseconds);
    }

    protected override void UpdateState()
    {
        if (State is ITabletReport report)
        {
            Tick((float)_stopwatch.Restart().TotalMilliseconds);
            report.Position = _pos;
            if (PreservePressure)
                report.Pressure = _pressure;
            State = report;
        }

        if (PenIsInRange())
            OnEmit();
    }

    private void Tick(float ms)
    {
        if (ms <= 0f) return;

        float dt = ms / _timeScale;

        if (SnapToTarget &&
            Vector2.Distance(_pos, _target) < SnapThreshold &&
            _vel.LengthSquared() < SnapThreshold * SnapThreshold)
        {
            _pos = _target;
            _vel = Vector2.Zero;
            return;
        }

        Vector2 force = (_target - _pos) * _tension;
        _vel += force * dt;
        _vel *= MathF.Pow(1f / _friction, dt);
        _pos += _vel * dt;
    }

    public override PipelinePosition Position => PipelinePosition.PreTransform;
}