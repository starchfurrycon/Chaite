# Round 159 handoff

## HEAD
`4cb2e08` — pushed. Source **untouched** this round (findings and diagnosis only).
Tests **749 pass / 9 fail**, the long-standing accepted set. Build clean.

## HEADLINE: the weak wing's problem is not tuning — the route has a hard wing budget it refills ~1/9 as often as it needs to

### The two wings fail for different reasons

Dense comparison at the same DPS (obsidian, defaults):

| | weak 600 (fails) | strong 600 (nearly kills) |
|---|---|---|
| contacts | 6 | 4 |
| boss state at contact | **0/1 charge: 4 of 6** | **5/6/7 bubble: 4 of 4** |
| wingTime = 0 at contact | **5 of 6** | 1 of 4 |
| `refill` label share | 39% | 19% |
| `|vx| < 3` share | 10% | 49% |

Everything before this round treated both arms as one problem. They are not.

### The wing budget is a magnitude mismatch

* `wingTime` drains at **exactly −1 per deployed tick or 0** — the histogram has only those two
  values on both arms. So the budget is literally *airborne ticks with wings out*, and **no
  manoeuvre reduces the rate**; only folding the wings conserves it.
* `wingTime` **never increases while airborne** — 0 such frames in all four dense runs. The apex
  refill documented in §8 is unreachable in the real trajectory.
* Actual whole-fight landings (adjacent frames where `wingTime` jumps ≥ 20):

| run | fight length | **landings** |
|---|---|---|
| weak 600 | 4958 | **0** |
| weak 300 | 5481 | 2 |
| strong 600 | 7476 | **0** |
| weak 800 | 3519 | **0** |

* Extrapolating the in-window drain rate across the full span: weak 600 needs **~1122** ticks of
  wing time against a **130** bar; strong 600 needs **~539** against **180**.

So the bar must be refilled roughly **9×** (weak) / **3×** (strong) per fight, and it is refilled
**0–2×**. The defect is **refill frequency, low by two orders of magnitude** — not an inability to
refill. (The extrapolation is rough: the sampled windows cluster before contacts and may
overestimate the rate. Next round must validate with real whole-fight landing counts.)

### Why landing was avoided — and why that reasoning is a trap

Landing means descending to a platform plane, and **the plane is the boss's altitude band**:

* Both arms' player-y span is ~3100 px and coincides with the boss's.
* 36.5% (weak) / 27.1% (strong) of sampled frames sit within 100 px of the boss's y.
* The two stretches where the player gets within 40 px of a plane (weak 1332..1367 and
  3819..3860) **end exactly at contacts 1367 and 3860**. Strong's single such stretch
  (5404..5464) is followed by contact 5474 *with wingTime 53 — the bar was not the issue*.

But not landing is equally fatal: an empty bar leaves **92.5% of frames in pure ballistic
descent** (vy decaying from −6.12 by a steady +0.40/tick, i.e. gravity on upward momentum, with
only 6.9% showing any active lift), and a falling player cannot dodge at all. That is precisely why
5 of 6 weak contacts happen on an empty bar.

This also explains why **every knob this round was zero-sum** — a knob reroutes the circuit but
cannot raise a 130-tick ceiling:

| experiment | gain | loss |
|---|---|---|
| standoff DPS ceiling 450 → 1300 | weak 1100 → **4784/3/0 KILL** | weak 800 → 3727/8/35421 |
| weak lead 40 + delay 4 | weak 1400 → **3884/0/0 zero-hit** | low-DPS band still fails |

## Self-corrections made this round (do not revert these)

1. **§158.7's "wasted lift on an empty bar" is retracted.** The `vy < -1` frames are ballistic
   coasting, not climb requests. Measured: 92.5% / 80.9% of empty-bar frames are gravity-dominated.
2. **"The refill phase climbs" is a measurement artifact.** `phase` is assigned on the tick its
   branch fires and never re-derived, so a stale `refill` label sits on frames whose motion is a
   ballistic arc. Evidence: `refill`-labelled frames are 19% descending on weak but 69% on strong —
   the same code cannot have two semantics, so it is the label that is stale.
   **Lesson: never measure physics from a phase label.**
3. **"One-time allocation" is too strong** — weak 300 does land twice. Corrected to a magnitude
   mismatch (§158.15).

## THE NEXT STEP (single, concrete)

**Implement a deliberate, pre-emptive landing to refill before the bar empties** (at roughly 40%
remaining: weak ~52, strong ~72 — not at 0), and require as the acceptance test that **landing
frames stop coinciding with contact frames**. §158.6 shows they currently coincide by necessity.

Two-branch falsification:

* If a landing instant **can** be found that does not coincide with a contact, landing frequency is
  the right lever and should be driven from ~0 to ~9 per fight.
* If **every** landing instant coincides with a contact, then no altitude-band landing is ever safe
  and the fix must instead forbid staying in the boss's altitude band for more than N ticks —
  changing the altitude relationship rather than the landing timing.

Do **not** re-attempt: raising the standoff ceiling, weak lead 40 + delay 4, apex refill, refill
guard > 0, tornado knobs, or any single DPS point tuning — all measured zero-sum or refuted.

## Method notes

* Dense runs need `CHAITE_PROBE_DENSE_FRAMES=1`. Read `artifacts/<run>/result.json`, never banners.
* **Sampled frames are sparse and biased** — `prehit` rows are a 47-tick window before each contact,
  so any share computed from them is conditioned on being near a contact. Cross-check absolute
  quantities (like landing counts) by walking *adjacent* sampled ticks and scaling deliberately.
* Analysis scripts written this round, all reusable: `tmp/contactprofile.py`,
  `tmp/refillwindow.py`, `tmp/wingdrain.py`, `tmp/windowclimb.py`, `tmp/emptyclimb.py`,
  `tmp/ballistic.py`.
