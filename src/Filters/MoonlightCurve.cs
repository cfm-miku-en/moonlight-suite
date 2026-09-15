using System;
using System.Collections.Generic;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace MoonlightSuite;

[PluginName("moonlight curve")]
public class MoonlightCurve : IPositionedPipelineElement<IDeviceReport>
{
    public const string SoftMode = "soft";
    public const string HardMode = "hard";
    public const string LinearMode = "linear";

    private const float FallbackMaxPressure = 8192f;
    private const float MaxGamma = 3f;

    private TabletReference? _tablet;

    public event Action<IDeviceReport>? Emit;

    public PipelinePosition Position => PipelinePosition.PreTransform;

    [Property("mode"), PropertyValidated(nameof(Modes))]
    [ToolTip("soft needs more force for the same output, which spreads the light end of the range out and gives finer control over thin lines. hard is the opposite and reaches full pressure sooner. linear leaves the shape alone so only deadzone and ceiling apply.")]
    public string Mode { get; set; } = SoftMode;

    public static IEnumerable<string> Modes => new[] { SoftMode, HardMode, LinearMode };

    [SliderProperty("amount", 0f, 1f, 0.5f), DefaultPropertyValue(0.5f)]
    [ToolTip("How far the curve bends. 0 is a straight line whatever the mode.")]
    public float Amount { get; set; } = 0.5f;

    [SliderProperty("deadzone (%)", 0f, 50f, 0f), DefaultPropertyValue(0f)]
    [ToolTip("Pressure below this much of the range reads as nothing. Raise it if your pen registers a line before you mean to press.")]
    public float Deadzone { get; set; }

    [SliderProperty("ceiling (%)", 50f, 100f, 100f), DefaultPropertyValue(100f)]
    [ToolTip("Pressure above this much of the range reads as full. Lower it if you cannot comfortably reach maximum pressure.")]
    public float Ceiling { get; set; } = 100f;

    [TabletReference]
    public TabletReference? Tablet
    {
        get => _tablet;
        set
        {
            _tablet = value;
            MaxPressure = Resolve(value);
        }
    }

    private float MaxPressure { get; set; } = FallbackMaxPressure;

    public void Consume(IDeviceReport report)
    {
        if (report is ITabletReport tablet)
            tablet.Pressure = Map(tablet.Pressure);

        Emit?.Invoke(report);
    }

    private uint Map(uint pressure)
    {
        var ceiling = MathF.Max(Ceiling, 0f) / 100f;
        var deadzone = MathF.Min(Deadzone / 100f, ceiling - 0.01f);
        var span = MathF.Max(ceiling - deadzone, 0.01f);

        var level = Math.Clamp((pressure / MaxPressure - deadzone) / span, 0f, 1f);
        var amount = Math.Clamp(Amount, 0f, 1f);

        if (amount > 0f && Mode != LinearMode)
        {
            var gamma = 1f + amount * (MaxGamma - 1f);
            level = MathF.Pow(level, Mode == HardMode ? 1f / gamma : gamma);
        }

        return (uint)MathF.Round(Math.Clamp(level, 0f, 1f) * MaxPressure);
    }

    private static float Resolve(TabletReference? tablet)
    {
        var max = tablet?.Properties?.Specifications?.Pen?.MaxPressure ?? 0u;

        return max > 0u ? max : FallbackMaxPressure;
    }
}
