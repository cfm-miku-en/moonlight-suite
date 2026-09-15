using System;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace MoonlightSuite;

public abstract class MoonlightAsyncFilter : AsyncPositionedPipelineElement<IDeviceReport>
{
    private const float MinDelta = 1e-5f;

    private TabletReference? _tablet;
    private bool _tracking;

    public override PipelinePosition Position => PipelinePosition.PreTransform;

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

    internal ITimeSource IntegrationClock { get; set; } = new HpetTimeSource();

    internal ITimeSource ReportClock { get; set; } = new HpetTimeSource();

    protected abstract void Reset(Vector2 position);

    protected abstract void SetTarget(Vector2 position, float reportInterval);

    protected abstract Vector2 Advance(float deltaTime);

    protected float ToUnits(float millimetres) => millimetres * UnitsPerMm;

    protected float ToMillimetres(float units) => units / UnitsPerMm;

    protected override void ConsumeState()
    {
        if (State is OutOfRangeReport or IProximityReport { NearProximity: false })
        {
            _tracking = false;
            return;
        }

        if (State is not IAbsolutePositionReport positioned)
            return;

        var input = positioned.Position;
        var gap = ReportClock.Restart();

        if (!MoonMath.IsFinite(input))
        {
            _tracking = false;
            return;
        }

        if (!_tracking || gap > PenLiftReset / 1000f)
        {
            _tracking = true;
            IntegrationClock.Restart();
            Reset(input);
            return;
        }

        SetTarget(input, gap);
    }

    protected override void UpdateState()
    {
        if (_tracking && State is IAbsolutePositionReport positioned)
        {
            var output = Advance(MathF.Max(IntegrationClock.Restart(), MinDelta));

            if (MoonMath.IsFinite(output))
                positioned.Position = output;
            else
                Reset(positioned.Position);
        }

        if (PenIsInRange())
            OnEmit();
    }
}
