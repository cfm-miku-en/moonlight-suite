using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace MoonlightSuite;

public abstract class MoonlightFilter : IPositionedPipelineElement<IDeviceReport>
{
    private const float MinDelta = 1e-5f;
    private const float SpeedAttack = 0.008f;
    private const float SpeedRelease = 0.045f;

    private TabletReference? _tablet;
    private Vector2 _previous;
    private bool _tracking;

    public event Action<IDeviceReport>? Emit;

    public PipelinePosition Position => PipelinePosition.PreTransform;

    [SliderProperty("pen lift reset (ms)", 1f, 200f, 25f), DefaultPropertyValue(25f)]
    [ToolTip("A gap between reports longer than this counts as the pen leaving the tablet. The filter then restarts from wherever the pen comes back down instead of sweeping across from the old position.")]
    public float PenLiftReset { get; set; } = 25f;

    [TabletReference]
    public TabletReference? Tablet
    {
        get => _tablet;
        set
        {
            _tablet = value;
            UnitsPerMm = TabletScale.UnitsPerMm(value);
        }
    }

    protected float UnitsPerMm { get; private set; } = TabletScale.Fallback;

    protected float Speed { get; private set; }

    protected Vector2 Movement { get; private set; }

    internal ITimeSource Clock { get; set; } = new HpetTimeSource();

    public void Consume(IDeviceReport report)
    {
        if (report is OutOfRangeReport or IProximityReport { NearProximity: false })
        {
            _tracking = false;
            Emit?.Invoke(report);
            return;
        }

        if (report is IAbsolutePositionReport positioned)
            Process(positioned);

        Emit?.Invoke(report);
    }

    protected abstract void Reset(Vector2 position);

    protected abstract Vector2 Filter(Vector2 input, float deltaTime);

    protected float ToUnits(float millimetres) => millimetres * UnitsPerMm;

    protected float ToMillimetres(float units) => units / UnitsPerMm;

    private void Process(IAbsolutePositionReport positioned)
    {
        var input = positioned.Position;
        var elapsed = Clock.Restart();

        if (!MoonMath.IsFinite(input))
        {
            _tracking = false;
            return;
        }

        if (!_tracking || elapsed > PenLiftReset / 1000f)
        {
            _tracking = true;
            Restart(input);
            return;
        }

        var deltaTime = MathF.Max(elapsed, MinDelta);

        Movement = input - _previous;
        _previous = input;

        var measured = ToMillimetres(Movement.Length()) / deltaTime;
        Speed = MoonMath.Damp(Speed, measured, deltaTime, measured > Speed ? SpeedAttack : SpeedRelease);

        var output = Filter(input, deltaTime);

        if (MoonMath.IsFinite(output))
            positioned.Position = output;
        else
            Restart(input);
    }

    private void Restart(Vector2 input)
    {
        _previous = input;
        Movement = Vector2.Zero;
        Speed = 0f;
        Reset(input);
    }
}
