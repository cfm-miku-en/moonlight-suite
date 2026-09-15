# moonlight suite: filter reference

Every filter, every setting. See the [README](README.md) for install and a quick start.

- [before you tune](#before-you-tune)
- [aim](#aim): aim, follow, resample, smooth, noise, predict
- [drawing](#drawing): curve
- [fun](#fun): spring, snap, angle, interpolate, wobble
- [starting points](#starting-points)
- [upgrading from 1.0](#upgrading-from-10)

---

## before you tune

Filters run before the transformation matrix, so they see the raw tablet area rather than your screen.

**Order matters.** Filters run top to bottom. If you stack several: noise, then smooth, then predict, then resample last.

**Every filter has `pen lift reset (ms)`**, default 25. If reports stop for longer than this, the pen counts as lifted and the filter restarts wherever it comes back down instead of sweeping across from where it left. Leave it alone unless a filter drops out mid-stroke on a flaky connection.

**Anything that reacts to pen speed shares one estimate**, and that estimate is deliberately lopsided. It rises fast and falls slowly, so smoothing gets out of the way the instant you launch into a movement, and doesn't slam back on the moment you decelerate.

**Your report rate changes what the small numbers mean.** Every millisecond setting is a time constant, and a time constant shorter than your report interval does almost nothing. How much of each report survives is `1 - e^(-interval / latency)`:

| latency | at 1000 Hz (1 ms apart) | at 133 Hz (7.5 ms apart) |
| --- | --- | --- |
| 2 ms | 39% | 98%, a no-op |
| 4 ms | 22% | 85% |
| 8 ms | 12% | 61% |

Defaults assume a fast tablet. **On a 133 Hz tablet, roughly double any millisecond setting** before deciding it does nothing.

---

## aim

### moonlight aim

Everything that helps aim, on three dials. Holds a small follow circle while the pen is near-still and smooths the low-speed band, then switches off completely once you're actually moving.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| strength | 0-1 | 0.5 | How much help while the pen is slow. 0 is untouched input. |
| release speed (mm/s) | 10-400 | 60 | Speed at which all help switches off. Above this the filter is a guaranteed no-op. |
| character | 0-1 | 0.5 | Which kind of help. Towards 0 is follow circle: no latency, but can feel sticky on tiny nudges. Towards 1 is smoothing: never sticky, but lags. |

Set **release speed** just under the speed you actually move between targets. Turn **strength** up until tremor stops, then stop. Only touch **character** when it's wrong in a specific way. Sticky means up, floaty means down.

Under the hood it's the follow circle and adaptive smoother below, with a fixed release curve of 0.6, sharing one budget. Deadzone is `0.12 mm x strength x (1 - character)`, latency is `24 ms x strength x character`.

### moonlight follow

The distance-driven counterpart. Instead of asking how fast the pen is moving, it asks how far the cursor has fallen behind, and catches up harder the further back it is.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| max lag (mm) | 0.05-5 | 1 | The furthest the cursor can ever fall behind. A hard ceiling, not a target. |
| deadzone (mm) | 0-1 | 0 | Movement smaller than this produces nothing. 0 leaves it off and lets the curve do the work. |
| smoothing (ms) | 0-30 | 10 | How heavily it smooths while sitting close to the pen. Fades as it falls behind. |
| leak | 0-1 | 0 | How much smoothing survives past max lag. 0 keeps the hard ceiling. |

**aim** promises it does nothing above a speed. **follow** promises a lag ceiling at any speed. Pick the guarantee you care about.

### moonlight resample

Your tablet reports at a fixed rate, 7.5 ms apart at 133 Hz. Between readings the cursor has nowhere to go, so it sits still and then jumps. This redraws it continuously in between. Different axis to everything else here, and it stacks with them.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| latency (ms) | 0-20 | 8 | How far behind live the cursor is drawn. |
| max lead (ms) | 0-15 | 5 | Cap on how far ahead it may guess, and what stops it running off if reports stall. |
| mode | linear / curved | linear | Straight lines between readings, or an arc fitted through the last three. |

**Latency is the whole filter, and it is not free.** Without resampling the cursor shows the newest reading, so its staleness averages half a report interval. With resampling it is exactly `latency`. On a 133 Hz tablet that makes **3.75 ms break-even**. Stock 8 ms costs you about 4 ms, and anything under 3.75 ms is a net win bought with guessing.

At or above one full interval it only ever draws between two readings it has actually received. Below that it extrapolates past the newest one, which is sharper but overshoots direction changes.

**curved** fits an arc through the last three readings instead of a straight line. Better on sweeping motion, worse on noise.

Inherits **frequency (hz)** from OTD's async pipeline. That's the redraw rate, not a feel setting. Leave it at 1000.

### moonlight smooth

Smoothing that gets out of the way when you move. Fights tremor while the pen is slow and releases as you speed up.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| latency (ms) | 0-20 | 2 | How far behind your hand the cursor sits when still. This number *is* the latency. |
| adaptive | on/off | on | Release the smoothing as the pen speeds up. |
| full speed (mm/s) | 10-600 | 250 | Speed at which smoothing is fully gone. |
| release curve | 0.25-4 | 1 | Over 1 drops it off quickly as you speed up, under 1 holds on closer to full speed. |

Turn **adaptive** off and it's a plain exponential smoother at the latency you asked for.

### moonlight noise

A follow circle. Chatter inside it produces no cursor movement at all. Past the edge the cursor tracks one to one, so unlike smoothing this costs no latency once you're actually moving.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| deadzone (mm) | 0-1 | 0.05 | Radius of the circle. Pen chatter is usually under 0.1 mm. |
| softness | 0-4 | 1 | Eases out of the circle instead of releasing all at once, so slow movement doesn't look like stairs. 0 is a hard edge. |
| speed relax | 0-1 | 0 | Shrinks the circle as you speed up, so it leaves fast aim completely alone. |

Start at 0.03 to 0.08 mm. If the cursor sits still while you're clearly drawing, it's too big.

### moonlight predict

Puts the cursor slightly ahead of the pen based on how you're moving. Buys back latency from elsewhere in your chain, and overshoots sharp direction changes. That's the trade.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| lead (ms) | 0-15 | 2 | How far ahead to sit. |
| velocity smoothing (ms) | 0-20 | 4 | Steadies the speed estimate. Too low and it jitters ahead of you, too high and it predicts through corners. |
| acceleration | 0-1 | 0 | Also lead on how fast your speed is changing. Helps on curves, worsens corners. |
| max offset (mm) | 0-15 | 4 | Hard cap on how far ahead it can ever get. 0 turns prediction off. |

Small numbers. Past about 5 ms of lead it starts feeling like the cursor is guessing, because it is.

---

## drawing

### moonlight curve

Reshapes pen pressure. Nothing to do with aim. This one is for people actually drawing with the thing.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| mode | soft / hard / linear | soft | Which way the curve bends. |
| amount | 0-1 | 0.5 | How far it bends. 0 is a straight line whatever the mode. |
| deadzone (%) | 0-50 | 0 | Pressure below this reads as nothing. |
| ceiling (%) | 50-100 | 100 | Pressure above this reads as full. |

**soft** needs more force for the same output, spreading out the light end of the range for finer control over thin lines. **hard** is the opposite and reaches full pressure sooner. **linear** leaves the shape alone, so only deadzone and ceiling apply.

Raise **deadzone** if the pen starts a line before you mean to press. Lower **ceiling** if you can't comfortably reach full pressure.

---

## fun

### moonlight spring

The cursor is on a spring attached to your pen.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| stiffness (hz) | 0.5-40 | 10 | How eagerly it chases. Low is floaty, high is tight. |
| damping ratio | 0.1-4 | 1 | 1 catches up as fast as possible without overshooting. Below 1 bounces past and wobbles back, above 1 is sluggish. |
| settle (mm) | 0-1 | 0.05 | Parks the cursor once it's this close and barely moving. |

Also inherits **frequency (hz)**, which is redraw rate rather than bounciness. Leave it at 1000.

### moonlight snap

Overshoots your movement past a speed threshold, then drifts back.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| threshold (mm/s) | 0-500 | 120 | Speed the boost starts at. |
| boost | 1-4 | 1.5 | How much further than your hand the cursor travels. |
| ramp (mm/s) | 0-500 | 150 | Extra speed needed to reach full boost. 0 switches it on all at once. |
| max offset (mm) | 0-20 | 5 | How far the cursor may run from the pen. |
| recenter (ms) | 0-500 | 120 | How quickly it settles back once you slow down. |

### moonlight angle

Pulls strokes onto the nearest of a set of directions. Works across all 360 degrees, snapping direction of travel, so left and right are separate targets.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| strength | 0-1 | 0.5 | How hard strokes get pulled. 1 locks them. |
| divisions | 2-36 | 12 | How many directions. 4 is horizontal and vertical, 8 adds the diagonals, 12 is every 30 degrees, 36 is every 10. |
| tolerance (deg) | 0-45 | 15 | How far off a direction a stroke can be and still get corrected. |
| rotation (deg) | -45-45 | 0 | Turns the whole set, for a tablet that sits at an angle. |
| soft edge | on/off | on | Holds full correction through most of the tolerance, then fades near the edge so strokes don't pop. |
| min speed (mm/s) | 0-200 | 10 | Leaves slow movement alone. |
| max offset (mm) | 0-20 | 3 | How far a corrected stroke may drift from the real pen position. On a long stroke this is what makes straightening give up, so raise it if correction stops partway. |

How much of the circle it touches is `divisions x 2 x tolerance / 360`. Every direction is covered once `tolerance` reaches `180 / divisions`, which is what the stock 12 and 15 gives you. At the old stock of 4 and 15, only a third of directions fell inside tolerance and the rest passed straight through.

### moonlight interpolate

A moving average over the last few reports. The bluntest smoother here, and **moonlight smooth** does the same job better. Kept because it's simple and predictable.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| window | 1-32 | 5 | How many reports get averaged. |
| falloff | 0.1-1 | 1 | 1 weighs every report the same. Lower leans on the newest ones. |

Counts reports rather than time, so unlike the rest of the suite its feel *does* change with your report rate.

### moonlight wobble

A sine wave bolted onto your cursor path, sideways to the direction you're moving. Was "sine mode" inside moonlight smooth in 1.0.

| setting | range | default | what it does |
| --- | --- | --- | --- |
| amplitude (mm) | 0-20 | 2 | How far sideways it swings. |
| frequency (hz) | 0-50 | 8 | Swings per second. |
| elastic amplitude | on/off | off | Scale the swing with pen speed. |
| elastic frequency | on/off | off | Speed the swing up with pen speed. |

---

## starting points

Found on a 133 Hz tablet with a small area. Scale the millisecond values down on a faster tablet.

**Speed and precision together.** `aim` at strength 0.58, release speed 60, character 0.57. Help concentrated on settling onto a target, gone by the time you're travelling.

**Precision only.** `aim` at strength 0.75, release speed 45, character 0.30. More help, shifted toward the follow circle so it costs less latency.

**Precision, zero latency.** `aim` at strength 0.55, release speed 45, character 0. Pure follow circle. Use this if the others feel late rather than shaky.

**Lowest latency that still does something.** `noise` alone at deadzone 0.03, softness 1, speed relax 0.3.

Two failure modes, two different fixes. If small deliberate movements stop registering, the deadzone is too big, so lower **strength**. If you keep arriving behind, there's too much smoothing, so lower **character**.

---

## upgrading from 1.0

**1.0 filters will not carry over.** Everything moved into a `MoonlightSuite` namespace, so OTD sees them as new plugins. Remove the old ones and add the new ones. The settings are in different units anyway, so the old numbers wouldn't have meant the same thing.

What changed:

- Everything is in mm and ms, scaled to your tablet, and independent of report rate
- Filters no longer start at the tablet origin, so there's no jump on the first report
- Filters reset when the pen leaves, so tapping at opposite corners no longer sweeps the cursor between them
- **smooth** is speed-adaptive, and its sine mode split out into **wobble**
- **noise** is a follow circle rather than a threshold with a soft knee
- **predict** leads on your current velocity. 1.0 used the velocity from two reports earlier
- **snap** and **angle** accumulate their correction instead of reapplying it fresh each report, so they shape a stroke instead of jittering
- **spring** takes stiffness in hz and a damping ratio. The old friction slider could multiply velocity every step and blow up
- `snap to diagonals` on **angle** did nothing in 1.0, because both branches listed the same angles. It's `divisions` now
- **aim**, **follow**, **resample**, **curve** and **wobble** are new
