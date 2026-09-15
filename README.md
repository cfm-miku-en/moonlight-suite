# moonlight suite

Twelve filters for OpenTabletDriver. Some are for aim, one is for drawing, the rest are for fun.

- **Settings are in millimetres and milliseconds**, converted using your tablet's own specs. A number means the same thing on a Wacom CTL-480 as on a Huion 1060.
- **Report rate doesn't change the feel.** A filter behaves identically at 133 Hz and at 1000 Hz.
- **The filters make promises, and the promises are tested.** `moonlight aim` does *nothing at all* above a speed you set. `moonlight follow` never lets the cursor fall further behind than a distance you set. Those are assertions in a test suite, not claims in a readme.

Built against OpenTabletDriver 0.6.6.2.

## install

Grab `MoonlightSuite.zip` from [releases](https://github.com/cfm-miku-en/moonlight-suite/releases) and drop it into OTD's plugin manager, or unzip `MoonlightSuite.dll` into:

```
%LOCALAPPDATA%\OpenTabletDriver\Plugins\moonlight suite\
```

Restart the daemon afterwards.

## which one do I want

| you want | use | it costs you |
| --- | --- | --- |
| steadier aim, one dial | **aim** | a little latency, or a little stickiness |
| steadier aim, guaranteed lag ceiling | **follow** | the same, shaped differently |
| smoother motion on a slow tablet | **resample** | latency, unless you tune it down |
| just smoothing, nothing clever | **smooth** | latency |
| just chatter removal | **noise** | nothing, until it feels sticky |
| to cancel latency from elsewhere | **predict** | overshoot on direction changes |
| better pressure response for drawing | **curve** | nothing |
| to mess about | **spring**, **snap**, **angle**, **wobble**, **interpolate** | your accuracy |

Most of the rest are **aim** taken apart, or novelties.

## quick start

Add **moonlight aim** and leave it on stock. Three dials:

| dial | what to do with it |
| --- | --- |
| strength | How much help while the pen is slow. Turn it up until tremor stops, then stop. |
| release speed (mm/s) | Where all help switches off. Set it just under the speed you actually move between targets. |
| character | Which kind of help. Towards 0 is a follow circle, which costs no latency but can feel sticky. Towards 1 is smoothing, which never sticks but lags. |

Two failure modes, two fixes. If small deliberate movements stop registering, the deadzone is too big, so lower **strength**. If you keep arriving behind the target, there's too much smoothing, so lower **character**.

**On a slow tablet, double the millisecond values.** A time constant shorter than your report interval does almost nothing, and 133 Hz tablets report only every 7.5 ms. Stock values assume a fast tablet.

## everything else

**[FILTERS.md](FILTERS.md)** covers every filter, every setting, what each one costs, and the configs worth starting from. It also has the stacking order, the report-rate maths, and what changed since 1.0.

## building

```bash
dotnet test
dotnet build MoonlightSuite.csproj -c Release
```

The plugin lands at `bin/Release/net8.0/MoonlightSuite.dll`.

Filter maths lives in `src/Filters/`. The shared plumbing lives in `src/Core/`: pen lift detection, timing, mm conversion, the speed estimate, and NaN guards. A filter only has to implement `Reset` and `Filter`.

Tests drive synthetic report streams through every filter at a controlled rate. They check that nothing goes non-finite at any slider extreme, that the pen lift reset holds, that each filter keeps the promise it makes, and that the plugin surface resolves the way the driver resolves it.

## credits

cfm-miku-en, developer and owner.

**AbstractQBit** ([AbstractOTDPlugins](https://github.com/AbstractQbit/AbstractOTDPlugins), GPL-3.0). Inspiration for moonlight spring and the sine mode that became moonlight wobble. Radial Follow's inner/outer radius model and its smoothing leak are the idea behind moonlight follow.

**Kuuube** ([Slimy Scylla](https://github.com/Kuuuube/Slimy_Scylla), GPL-3.0). The asymmetric inertia idea behind the shared speed estimate, and dynamic speed smoothing as prior art for the adaptive release.

Both are GPL-3.0, same as this. Nothing here is copied from either. The filters were written from the concepts, and the credit is for the ideas.

GPL 3.0
