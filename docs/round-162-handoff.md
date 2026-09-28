# Round 162 handoff

## HEAD
`17c3365` — pushed. Tests **749 pass / 9 fail**, the long-standing accepted set. Build clean.
Route channel re-verified after the probe change: strong `6000/6/55`, weak `5636/9/102`,
both `MATCH`, `REPRODUCED`.

The objective is **still NOT met** (strict zero-hit at the 6000-tick cap is 6/9 hits on the routes).

## HEADLINE: the height-band axis is dead, but the round's real value is that the dash direction is now understood and measurable

### 1. Height-band escape is refuted — and the mechanism is known

The round's opening question was whether an altitude band exists that the boss's **hover** states
cannot reach. It does not:

| player position | weak 600 | strong 600 |
|---|---|---|
| above the hover band | 11.8% | 18.2% |
| below the hover band | 0.0% | 24.5% |
| **inside the hover band** | **88.2%** | **57.3%** |

Hover band widths are **192 tiles** (weak) and **176 tiles** (strong) — essentially the whole arena.
The reason is decisive: **the boss's hover tracks the player.** At t=1320 boss.y 5956 / player.y 5694;
at t=3272 boss.y 7671 / player.y 7630. `|boss.y − player.y|` median = **230 px**.

So no altitude-shelter strategy can work: wherever the player goes, the hover follows. This is a
mechanism, not a "tried it, did nothing" result.

### 2. The dash direction is fully reverse-engineered (the decisive finding)

`Player.DoCommonDashHandle`:

```csharp
int num2 = direction;                    // facing
int num3 = controlRight - controlLeft;
int num4 = num2;
if (num3 == -num2) num4 = num3;          // REVERSAL only
dir = num4;
...
velocity.X = 14.5f * (float)dir2;        // dash speed is horizontal only
```

and facing is assigned **after** the movement step:

```csharp
position += velocity;
if (velocity.X < 0f) direction = -1;
else if (velocity.X > 0f) direction = 1;
```

Two consequences, both proven by measurement:

* The override **only ever reverses** relative to facing, so an input *matching* the intended
  direction is **ignored**. That is exactly why the first `CHAITE_PERP_DASH` produced **0**
  perpendicular dashes.
* **Facing lags one frame**, so the dash direction cannot be set on the dash frame at all.

This explains, at last, why **every** dash-timing parameterisation was zero-sum (rounds 158/161):
the circuit never controlled the dash direction, and direction is set by a lagged facing value.

### 3. The implementation, and a measurement gap found and then closed

Implemented the one-frame-early aim: on the dash tick arm `horizontal = −desired`, applied at the
top of the next tick before anything reads the horizontal.

The first self-check appeared to show the dash count collapsing 70 → 6. **That diagnostic was
wrong, and I corrected it twice:**

1. First hypothesis (`eocDash` threshold excludes dashes that collide on frame 1) — **refuted**: all
   53/53 and 55/55 dashes start at `eocDash = 15`.
2. Real cause — **sampling bias**: `prehit-observations.jsonl` contains only a **47-tick window
   before each contact**, so dashes outside those windows are simply absent. The 70/60/6 "take-off"
   counts were never dash totals. The true totals are in `shield-events.jsonl` (53/55/32).
3. That exposed the deeper gap: `shield-events` had the complete dash record with `vx/vy` but **no
   boss pose**, while `prehit` had boss poses only inside contact windows — only **7 of 53** dashes
   were classifiable.

**Fixed** by publishing `bossX/bossY/bossVx/bossVy/bossAi0` and `playerX/playerY` into shield events
(`tools/GameProbe.cs`). Observation only; baseline reproduces byte-for-byte. Coverage rose from
~10% to **85–87%**.

### 4. The first trustworthy direction distribution

| run | dashes | classified | AWAY | PERP | TOWARD |
|---|---|---|---|---|---|
| baseline | 61 | 53 | **38 (72%)** | 6 (11%) | 9 (17%) |
| perp aim | 65 | 55 | **26 (47%)** | **10 (18%)** | 19 (35%) |

The aim **demonstrably works** — AWAY drops 25 points, PERP rises 7 — but it is **partial**. The
reason is geometric: **the Shield dash is purely horizontal**, so the normal is only horizontal when
the boss approaches from directly above/below. From the side the normal is near-vertical and the dash
**cannot express it**. Only the horizontal component of the normal is reachable.

**Survival is unchanged**: 5320/6/30138 baseline vs 5590/6/27344 with the aim — still 6 hits, still
dies. **No claim is made that normal-dodging improves survival.**

> Correction to earlier notes: the previously reported "58 of 70 dashes away (83%)" was inflated by
> the sampling bias. The trustworthy figure is **72%**.

## Next step

Group the residual AWAY/TOWARD dashes **by `bossAi0`** using the newly available field, to decide
whether they come from the facing time window or from the horizontal-normal geometric limit:

* If AWAY concentrates in specific states → a facing-timing problem, worth another iteration.
* If it is spread across states → the geometric limit dominates, and dash **direction** should be
  abandoned as a lever (the dash is horizontal-only by construction).

Only after that should any survival question be asked. Do **not** re-attempt: pre-emptive landing,
apex refill, standoff ceiling, ttc lower bound, shared/DPS-constant dash delay, height-band escape.

## Code state

| item | default | inert? |
|---|---|---|
| `CHAITE_PERP_DASH` | -1 | yes, verified byte-identical twice |
| shield-event boss/player pose | always on | observation only, baseline unchanged |

Analysis scripts: `tmp/altitudeband.py`, `tmp/iframecoverage.py`, plus the round-159/160 set.
