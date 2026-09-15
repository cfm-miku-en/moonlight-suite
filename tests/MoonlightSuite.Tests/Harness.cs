using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using MoonlightSuite.Core;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace MoonlightSuite.Tests;

internal sealed class PenReport : ITabletReport
{
    public byte[] Raw { get; set; } = Array.Empty<byte>();
    public Vector2 Position { get; set; }
    public uint Pressure { get; set; } = 1000;
    public bool[] PenButtons { get; set; } = new bool[2];
}

internal sealed class HoverReport : ITabletReport, IProximityReport
{
    public byte[] Raw { get; set; } = Array.Empty<byte>();
    public Vector2 Position { get; set; }
    public uint Pressure { get; set; }
    public bool[] PenButtons { get; set; } = new bool[2];
    public bool NearProximity { get; set; }
    public uint HoverDistance { get; set; }
}

internal sealed class Harness
{
    private readonly MoonlightFilter _filter;
    private readonly ManualTimeSource _clock;

    public Harness(MoonlightFilter filter, float reportRate = 1000f)
    {
        _filter = filter;
        _clock = new ManualTimeSource(1f / reportRate);
        _filter.Clock = _clock;
        _filter.Emit += _ => Emitted++;
    }

    public int Emitted { get; private set; }

    public float Interval
    {
        get => _clock.Delta;
        set => _clock.Delta = value;
    }

    public Vector2 Send(Vector2 position)
    {
        var report = new PenReport { Position = position };
        _filter.Consume(report);
        return report.Position;
    }

    public Vector2 SendAfterPause(Vector2 position, float seconds)
    {
        var interval = _clock.Delta;
        _clock.Delta = seconds;
        var output = Send(position);
        _clock.Delta = interval;
        return output;
    }

    public Vector2 Run(IEnumerable<Vector2> path)
    {
        var output = Vector2.Zero;

        foreach (var point in path)
            output = Send(point);

        return output;
    }

    public List<Vector2> Capture(IEnumerable<Vector2> path) => path.Select(Send).ToList();

    public void Lift() => _filter.Consume(new OutOfRangeReport());

    public void LeaveProximity() => _filter.Consume(new HoverReport { NearProximity = false });
}

internal static class Catalog
{
    public static IEnumerable<Type> SyncFilterTypes => typeof(MoonlightFilter).Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && typeof(MoonlightFilter).IsAssignableFrom(type))
        .OrderBy(type => type.Name);

    public static MoonlightFilter Create(Type type) => (MoonlightFilter)Activator.CreateInstance(type)!;

    public static IEnumerable<PropertyInfo> Sliders(Type type) => type
        .GetProperties()
        .Where(property => property.PropertyType == typeof(float)
            && property.GetCustomAttribute<SliderPropertyAttribute>() != null);

    public static IEnumerable<PropertyInfo> Switches(Type type) => type
        .GetProperties()
        .Where(property => property.PropertyType == typeof(bool)
            && property.GetCustomAttribute<BooleanPropertyAttribute>() != null);

    public static SliderPropertyAttribute Range(PropertyInfo property)
        => property.GetCustomAttribute<SliderPropertyAttribute>()!;
}

internal static class Paths
{
    public static readonly Vector2 Origin = new(8000f, 6000f);

    public static IEnumerable<Vector2> Hostile()
    {
        for (var i = 0; i < 40; i++)
            yield return Origin;

        for (var i = 0; i < 200; i++)
            yield return Origin + new Vector2(i * 4f, 0f);

        for (var i = 0; i < 200; i++)
            yield return Origin + new Vector2(800f - i * 4f, i * 4f);

        for (var i = 0; i < 120; i++)
            yield return Origin + new Vector2(i % 2 * 2f, 800f);

        yield return Origin + new Vector2(14000f, 11000f);

        for (var i = 0; i < 40; i++)
            yield return Origin;
    }

    public static IEnumerable<Vector2> Line(Vector2 start, float degrees, float spacing, int count)
    {
        var radians = degrees * (MathF.PI / 180f);
        var step = new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * spacing;

        for (var i = 0; i <= count; i++)
            yield return start + step * i;
    }
}
