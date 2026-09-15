using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace MoonlightSuite.Tests;

public class ContractTests
{
    public static IEnumerable<object[]> AllFilters => Catalog.SyncFilterTypes.Select(type => new object[] { type });

    [Theory, MemberData(nameof(AllFilters))]
    public void FirstReportIsUntouched(Type type)
    {
        var harness = new Harness(Catalog.Create(type));

        Assert.Equal(Paths.Origin, harness.Send(Paths.Origin));
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void FirstReportAfterOutOfRangeIsUntouched(Type type)
    {
        var harness = new Harness(Catalog.Create(type));
        harness.Run(Paths.Line(Paths.Origin, 0f, 6f, 120));
        harness.Lift();

        var landing = new Vector2(1200f, 14000f);

        Assert.Equal(landing, harness.Send(landing));
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void FirstReportAfterProximityLossIsUntouched(Type type)
    {
        var harness = new Harness(Catalog.Create(type));
        harness.Run(Paths.Line(Paths.Origin, 0f, 6f, 120));
        harness.LeaveProximity();

        var landing = new Vector2(1200f, 14000f);

        Assert.Equal(landing, harness.Send(landing));
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void FirstReportAfterSilenceIsUntouched(Type type)
    {
        var harness = new Harness(Catalog.Create(type));
        harness.Run(Paths.Line(Paths.Origin, 0f, 6f, 120));

        var landing = new Vector2(1200f, 14000f);

        Assert.Equal(landing, harness.SendAfterPause(landing, 0.2f));
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void EveryReportIsForwarded(Type type)
    {
        var harness = new Harness(Catalog.Create(type));
        var path = Paths.Hostile().ToList();
        harness.Run(path);
        harness.Lift();

        Assert.Equal(path.Count + 1, harness.Emitted);
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void StillPenNeverProducesGarbage(Type type)
    {
        var harness = new Harness(Catalog.Create(type));

        foreach (var output in harness.Capture(Enumerable.Repeat(Paths.Origin, 2000)))
        {
            Assert.True(float.IsFinite(output.X) && float.IsFinite(output.Y));
            Assert.True(Vector2.Distance(output, Paths.Origin) < 1f);
        }
    }

    [Theory, MemberData(nameof(AllFilters))]
    public void SlidersAtTheirLimitsStayFinite(Type type)
    {
        foreach (var setup in Extremes(type))
        {
            var filter = Catalog.Create(type);
            setup(filter);

            var harness = new Harness(filter);

            foreach (var output in harness.Capture(Paths.Hostile()))
                Assert.True(float.IsFinite(output.X) && float.IsFinite(output.Y),
                    $"{type.Name} produced {output} at a slider limit");
        }
    }

    private static IEnumerable<Action<MoonlightFilter>> Extremes(Type type)
    {
        var sliders = Catalog.Sliders(type).ToList();
        var switches = Catalog.Switches(type).ToList();

        foreach (var flag in new[] { false, true })
        {
            var state = flag;

            void ApplySwitches(MoonlightFilter filter)
            {
                foreach (var toggle in switches)
                    toggle.SetValue(filter, state);
            }

            yield return ApplySwitches;

            yield return filter =>
            {
                ApplySwitches(filter);

                foreach (var slider in sliders)
                    slider.SetValue(filter, Catalog.Range(slider).Min);
            };

            yield return filter =>
            {
                ApplySwitches(filter);

                foreach (var slider in sliders)
                    slider.SetValue(filter, Catalog.Range(slider).Max);
            };

            foreach (var slider in sliders)
            {
                var property = slider;
                var range = Catalog.Range(slider);

                yield return filter =>
                {
                    ApplySwitches(filter);
                    property.SetValue(filter, range.Min);
                };

                yield return filter =>
                {
                    ApplySwitches(filter);
                    property.SetValue(filter, range.Max);
                };
            }
        }
    }
}
