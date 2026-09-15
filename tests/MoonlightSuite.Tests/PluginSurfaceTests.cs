using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenTabletDriver.Plugin.Attributes;
using Xunit;

namespace MoonlightSuite.Tests;

public class PluginSurfaceTests
{
    private static IEnumerable<Type> PluginTypes => typeof(MoonlightFilter).Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && type.GetCustomAttribute<PluginNameAttribute>() != null);

    public static IEnumerable<object[]> Plugins => PluginTypes.Select(type => new object[] { type });

    [Theory, MemberData(nameof(Plugins))]
    public void ValidatedPropertiesResolveTheWayTheDriverResolvesThem(Type type)
    {
        foreach (var property in type.GetProperties())
        {
            var validated = property.GetCustomAttribute<PropertyValidatedAttribute>();

            if (validated == null)
                continue;

            var member = type.GetMember(validated.MemberName).FirstOrDefault();

            Assert.True(member != null,
                $"{type.Name}.{property.Name} points at '{validated.MemberName}', which does not exist");

            var source = type.GetProperty(validated.MemberName);

            Assert.True(source != null, $"{type.Name}.{validated.MemberName} must be a property");
            Assert.True(source!.GetMethod!.IsStatic,
                $"{type.Name}.{validated.MemberName} must be static, or the driver logs 'Failed to get valid binding values'");

            var values = (IEnumerable<string>)source.GetValue(null)!;

            Assert.NotEmpty(values);

            var stock = (string)property.GetValue(Activator.CreateInstance(type))!;

            Assert.Contains(stock, values);
        }
    }

    [Theory, MemberData(nameof(Plugins))]
    public void EveryPluginIsConstructibleAndNamed(Type type)
    {
        var name = type.GetCustomAttribute<PluginNameAttribute>()!.Name;

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.NotNull(Activator.CreateInstance(type));
    }

    [Theory, MemberData(nameof(Plugins))]
    public void SliderDefaultsSitInsideTheirOwnRange(Type type)
    {
        var instance = Activator.CreateInstance(type)!;

        foreach (var property in type.GetProperties())
        {
            var slider = property.GetCustomAttribute<SliderPropertyAttribute>();

            if (slider == null || property.PropertyType != typeof(float))
                continue;

            var stock = (float)property.GetValue(instance)!;

            Assert.True(stock >= slider.Min && stock <= slider.Max,
                $"{type.Name}.{property.Name} starts at {stock}, outside its {slider.Min} to {slider.Max} slider");

            var declared = property.GetCustomAttribute<DefaultPropertyValueAttribute>();

            if (declared?.Value is float announced)
                Assert.True(Math.Abs(announced - stock) < 0.0001f,
                    $"{type.Name}.{property.Name} starts at {stock} but advertises {announced}");
        }
    }
}
