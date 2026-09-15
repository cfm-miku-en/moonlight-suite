using OpenTabletDriver.Plugin.Timing;

namespace MoonlightSuite.Core;

internal interface ITimeSource
{
    float Restart();
}

internal sealed class HpetTimeSource : ITimeSource
{
    private readonly HPETDeltaStopwatch _stopwatch = new(true);

    public float Restart() => (float)_stopwatch.Restart().TotalSeconds;
}

internal sealed class ManualTimeSource : ITimeSource
{
    public ManualTimeSource(float delta = 0.001f) => Delta = delta;

    public float Delta { get; set; }

    public float Restart() => Delta;
}
