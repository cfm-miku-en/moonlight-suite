using System;
using System.Collections.Generic;
using System.Numerics;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;

namespace MoonlightSuite;

[PluginName("moonlight resample")]
public class MoonlightResample : MoonlightAsyncFilter
{
    public const string LinearMode = "linear";
    public const string CurvedMode = "curved";

    private const float MinInterval = 0.0005f;
    private const float MaxInterval = 0.05f;
    private const float IntervalBlend = 0.1f;

    private Vector2 _older;
    private Vector2 _previous;
    private Vector2 _latest;
    private float _interval = 0.0075f;
    private float _since;

    [SliderProperty("latency (ms)", 0f, 20f, 8f), DefaultPropertyValue(8f)]
    [ToolTip("How far behind live the cursor is drawn. At or above one report interval the cursor only ever moves between two readings you have actually had, which is the smooth and safe end. Below that it starts guessing ahead, which is sharper but overshoots direction changes. A 133 hz tablet has a 7.5 ms interval.")]
    public float Latency { get; set; } = 8f;

    [SliderProperty("max lead (ms)", 0f, 15f, 5f), DefaultPropertyValue(5f)]
    [ToolTip("Hard cap on how far ahead of the newest reading the cursor may be placed when latency is low enough to make it guess. Also what stops it running away if reports stall.")]
    public float MaxLead { get; set; } = 5f;

    [Property("mode"), PropertyValidated(nameof(Modes))]
    [ToolTip("linear draws straight lines between readings. curved fits an arc through the last three, which tracks sweeping motion better but reacts harder to noise.")]
    public string Mode { get; set; } = LinearMode;

    public static IEnumerable<string> Modes => new[] { LinearMode, CurvedMode };

    protected override void Reset(Vector2 position)
    {
        _older = position;
        _previous = position;
        _latest = position;
        _since = 0f;
    }

    protected override void SetTarget(Vector2 position, float reportInterval)
    {
        if (reportInterval > MinInterval && reportInterval < MaxInterval)
            _interval += (reportInterval - _interval) * IntervalBlend;

        _older = _previous;
        _previous = _latest;
        _latest = position;
        _since = 0f;
    }

    protected override Vector2 Advance(float deltaTime)
    {
        _since += deltaTime;

        var interval = MathF.Max(_interval, MinInterval);
        var ceiling = 1f + MathF.Max(MaxLead, 0f) / 1000f / interval;
        var t = Math.Clamp(1f + (_since - Latency / 1000f) / interval, 0f, ceiling);

        return Mode == CurvedMode ? Curved(t) : Vector2.Lerp(_previous, _latest, t);
    }

    private Vector2 Curved(float t)
    {
        var a = (t * t - t) * 0.5f;
        var b = t * t - 1f;
        var c = (t * t + t) * 0.5f;

        return _older * a - _previous * b + _latest * c;
    }
}
