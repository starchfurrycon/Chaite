# Round 164 handoff

## HEAD
`15638e3` — pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.

Objective **still NOT met**: strong 10/14, weak 4/14 on the obsidian full-range sweep.

## HEADLINE: the survival criterion is now exact, and the weak wing's root cause is fully traced

### 1. ★ Survival is decided *exactly* by total contact count

Across all 28 strong-wing DPS points:

| criterion | kill group | death group | separable? |
|---|---|---|---|
| **total contacts** | 0–6 (median **2**) | **4–5** (median 4) | **YES: ≤3 ⇒ 20/20 kill; ≥4 ⇒ 0/8 kill** |
| max-in-300-tick window | 0–3 | 2–3 | no (overlaps) |
| max-in-500 | 0–3 | 2–3 | no |
| max-in-800 | 0–3 | 2–4 | no |

**Contact *density* doesn't separate at all — only the total matters.** This reduces the objective to
a single quantified target: **bring all 28 points to ≤3 contacts**. The strong wing already achieves
this at **20/28**; only 8 points need 1–2 contacts removed.

### 2. ★ The weak wing never holds altitude separation (the root cause)

`dy = player.y − boss.y` over **boss-charge frames only** (negative = player above boss). Contact
requires `|dy| < 92`, so that band is called LEVEL.

| run | <−500 | −300:−92 | **LEVEL** | 92:300 | >500 |
|---|---|---|---|---|---|
| strong 1200 (zero-hit) | **27.8%** | 5.7% | 13.4% | 6.8% | **17.0%** |
| strong 2000 (zero-hit) | **44.0%** | 6.4% | 16.9% | 7.4% | 13.8% |
| strong 500 (died) | 24.6% | 7.3% | 8.4% | 7.5% | **21.1%** |
| **weak 300 (died)** | **0.0%** | 30.8% | **32.3%** | 25.6% | **2.2%** |
| **weak 600 (died)** | **0.0%** | 27.1% | **32.1%** | 29.9% | **1.4%** |
| **weak 900 (kill)** | **0.0%** | 27.1% | 28.6% | 33.3% | 2.2% |

The strong wing is >500 px from the boss for 22–44% of charge frames and only 8–17% level. The weak
wing is **0.0% beyond either boundary**, 57% within ±300, and **32% inside the death band**. Its whole
`dy` range is **[−393, +1334]** vs the strong's **[−938, +1258]** — and its max is *below* the boss,
so it is not "cannot climb" but "spends much of the fight descending".

Peak climb: weak **−9.91** vs strong **−16.52** (factor 1.67). Both climb in similar frame shares
(51% vs 44%), but the weak accumulates only **474** frames beyond 500 px separation vs the strong's
**1879**.

### 3. ★ The complete causal chain — every link measured

1. **Geometry**: contact needs `|dx| < 95` **and** `|dy| < 92`.
2. **Weak wing sits mid-band** during charges (§2).
3. **Lock position is NOT decisive**: only **5 of 47** weak charges are in the band at lock, needing
   at most **157 px = 16 ticks** to clear — affordable inside a 28-tick charge at 9.91 px/tick.
4. **But the meter is empty exactly then.** Weak 600's median `wingTime` at lock is **32 of 130**, and
   **21/47 (45%) have <20**. Those 5 charges that needed to climb had lock wing values of
   **14, 0, 0, 14, 0 — every one empty.**
5. **Refills are too rare.** Refills occur only at an apex: **18 times, median interval 252 ticks**,
   against a 58-tick charge period — so ~1 charge in 4 starts full. Meter empty **31%** of frames;
   **39 climbing stretches but only 18 reach an apex**, i.e. **over half of all climbs are abandoned
   mid-way with an empty meter**.

**The route does command the climb; the meter is empty on precisely the charges that require it.**

### 4. ★ Corrected a standing measurement error

Round 159 recorded that `wingTime` "never increases while airborne (0 frames in 4 runs)". **That is
wrong and is herewith refuted.** The weak 600 run refills **18 times**, every one with
`vy: 0.00 → −6.48`, `controlJump=True`, `releaseJump=False`, `jump=15`, **`justJumped=True`**, always
restoring exactly **130**.

This matches the decompiled apex branch (`Player.cs:26992`, already quoted in the source). It matters
because it moves refilling from a *landing* requirement — refuted in round 160, since landing means
entering the boss's altitude band — to a purely **airborne** action.

## Next step

Raise the weak wing's apex cadence from **one per 252 ticks to one per ~58**, so every charge is
preceded by a climb → apex → refill.

**Six measurable acceptance criteria:**

1. `wingTime` median at lock: **32 → ≥100**
2. share with `wingTime < 20` at lock: **45% → <10%**
3. LEVEL share of charge frames: **32% → ~10–13%** (strong wing's level)
4. frames with `|dy| > 500`: **474 → ≥1500**
5. weak low-DPS (300–800) total contacts: **8/8/7/6/6 → ≤3**
6. weak obsidian acceptance: **4/14 → 14/14**

**Implementation caveat discovered while starting it — important for the next round.**
The refilling apex is the **jump** apex (`vy == 0 && releaseJump`, or `autoJump && justJumped`), *not*
any turnaround. In the traced cycle at t=3046 the player gets a full meter at the jump apex and then
climbs 38 ticks until the meter empties mid-climb (t=3084); by then it is **past** the apex, so simply
"holding the climb to the apex" **cannot** refill — there is no refillable apex ahead, only freefall.
The fix must therefore create a *new* refillable apex while the meter still has charge: release jump
and let gravity bring `velocity.Y` through zero **before** the meter empties, then resume.
`ApexRefillArmed` already exists for exactly this (`vertical > 0 && player.Velocity.Y > -0.6f`), so the
next round should test arming it earlier (at a higher meter floor) rather than adding a new mechanism.

Do **not** re-attempt: dash direction/timing/ttc, perpendicular dash, i-frames, height-band escape,
pre-emptive landing as a *landing*, standoff ceiling, `CHAITE_CASCADE_*`, tornado knobs, bubble-break,
"reconverge after the fork", defense tier as a survival lever, or the old landing-based
`_refillGuardBudget` (round 160 refuted it; note the guard's *direction* is what changed, not its use).

## Code state

| item | default | note |
|---|---|---|
| `CHAITE_PERP_DASH` | -1 | inert, verified byte-identical twice |
| shield-event boss/player pose | always on | observation only; baseline unchanged |
| `ApexRefillArmed` | existing knob | the intended vehicle for the next round |

Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`.
