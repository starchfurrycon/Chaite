# Round 167 handoff

## HEAD
Pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.
**No source changes** — the one candidate was found to be already-refuted in source and was not retried.

## HEADLINE: the strong wing is at 19/28, not 10/14 — and the weak wing's gap is structural

### 1. ★ Round 166's target was ALREADY implemented and ALREADY refuted in source

I was about to implement "descend at the lock, then convert the descent into a climb". Reading
`FishronWingScript.cs:3217-3250` first showed this **is** `CHAITE_CHARGE_NORMAL_OWNER` — the owner's own
rule, `vertical = -sign(player.y − boss.y)`. Its recorded measurements:

| variant | result |
|---|---|
| clause 2 only (**flip the sign on every lock**) | strong 600/1000/1500 → **all die at tick 2151, 5 hits** |
| both clauses (skip flip when level + pull away when far) | strong 600/1000/1200/1500 → **all die at tick 1811, 5 hits** |

Baseline: strong 600 survives to 6000 (6 hits); **strong 1500 is a zero-hit kill**.

**Two inherited conclusions:**
1. **`dy` at the lock is near-random** — "the sign is near-arbitrary and flips from charge to charge;
   obeying it replaces a coherent escape with a coin flip". This matches round 165's finding that
   pre-lock positions are nearly identical (89% / 88% / 93% already clear).
2. **The owner's rule is a good *description* of a working escape but a bad *controller*** — the
   circuit already obeys it on **13 of 16** audited hits.

Round 166's "next step" is therefore **withdrawn as already-refuted**, not untested, and must not be
retried.

### 2. ★ Strong obsidian: **19/28 kills**, including two exact zero-hit kills

Round 164's "10/14" was a **sample**, not the range. Full native re-measurement of all 28 points:

| dps | ticks | hits | life left | | dps | ticks | hits | life left |
|---|---|---|---|---|---|---|---|---|
| 300 | 15938 | 6 | 0 ✅ | | 750 | 6781 | 3 | 0 ✅ |
| 400 | 12240 | 5 | 0 ✅ | | 800 | 6386 | 2 | 0 ✅ |
| 450 | 10937 | 4 | 0 ✅ | | 900 | 5741 | 1 | 0 ✅ |
| 475 | 7838 | 4 | 20233 ✗ | | 1000 | 5221 | 3 | 0 ✅ |
| 500 | 7304 | 5 | 21642 ✗ | | 1050 | 4997 | 3 | 0 ✅ |
| 525 | 8247 | 4 | 10545 ✗ | | 1075 | 4893 | 2 | 0 ✅ |
| 550 | 7531 | 5 | 13926 ✗ | | 1100 | 4409 | 4 | 7087 ✗ |
| 575 | 8674 | 4 | 0 ✅ | | 1125 | 4461 | 4 | **4500** ✗ |
| 600 | 7830 | 4 | **5096** ✗ | | 1150 | 4611 | 1 | 0 ✅ |
| 625 | 7689 | 4 | **3480** ✗ | | **1200** | 4441 | **0** | 0 ✅ |
| 650 | 7738 | 3 | 0 ✅ | | 1400 | 3884 | 1 | 0 ✅ |
| 675 | 7472 | 2 | 0 ✅ | | 1600 | 3466 | 1 | 0 ✅ |
| 700 | 5965 | 4 | 14721 ✗ | | 1750 | 3215 | 2 | 0 ✅ |
| 725 | 6997 | 2 | 0 ✅ | | **2000** | 2881 | **0** | 0 ✅ |

**19/28 = 68%.** All 9 failures are in the **3–28% band**, three inside **7%** (625→3480, 1125→4500,
600→5096). **No failure is a rout.** `hits` is not monotonic in DPS (500→5, 1150→1, 1200→0, 1400→1,
1600→1, 1750→2, 2000→0) — the signature of the chaotic trajectory and the phase-transition fork,
meaning the remaining points are **reachable**, not systematically out of range.

### 3. ★ Weak wing: **5/26**, and the gap is *not* a matter of one or two contacts

| wing | kills | failing life-left |
|---|---|---|
| **strong** | **19/28 = 68%** | **3480 – 21642 (4.5% – 27.7%)** |
| **weak** | **5/26 = 19%** | **4344 – 51366 (5.6% – 65.9%)** |

**At identical dps 300:**

| wing | ticks | hits | outcome |
|---|---|---|---|
| strong | **15938** | 6 | **KILL** |
| weak | **5839** | 8 | death, 51366 (66%) of boss HP left |

**The strong wing survives 2.7× as long at the same DPS and kills; the weak wing dies at 1/2.7 of the
way through.** The weak wing's contacts are **4–9** against the strong's **1–6**, and it fails at
**every** low-DPS point (300–800).

### 4. ★ The weak wing's bottleneck is ENDURANCE, not dodge quality

The **only** difference between the arms is the wings: strong `wingTimeMax 180`, climb peak **16.52**;
weak `wingTimeMax 130`, climb peak **9.91**. Low DPS ⇒ long fight ⇒ more exposure, and the weak wing
cannot endure it. So it needs contacts cut from **6–9 down to ≤3** — nearly a halving. **Structural.**

## Recommended next steps

1. **Converge the strong wing first.** 19/28 with all failures in the "one breath short" band and
   non-monotonic hits. Strong-wing acceptance alone would satisfy the objective for that loadout.
2. **Treat the weak wing as a separate, harder problem.** 13+ "rewrite `vertical`" interventions have
   all been zero-sum, so look for **permit/forbid** interventions instead — or reconsider the weak
   wing's whole stance.
3. **★ The most promising untried idea** (recorded for the next round): since the two arms differ
   **only in vertical capability** while the weak wing's **horizontal** capability is *identical*
   (cruise 7–8, dash 14.5), the weak wing's correct strategy may be to **abandon vertical dodging and
   maximise horizontal distance** rather than imitating the strong wing's vertical play. This aligns
   with the owner's remark that "as long as horizontal speed is maintained throughout".

## Do NOT re-attempt

`CHAITE_CHARGE_NORMAL_OWNER` or any "flip the dodge sign by `dy` at lock" rule (refuted in source, §1);
pre-lock lift band; `CHAITE_APEX_REFILL` as a default; `_refillGuardBudget` > 0; dash
direction/timing/ttc; perpendicular dash; i-frames; height-band escape; standoff ceiling;
`CHAITE_CASCADE_*`; tornado knobs; bubble-break; "reconverge after the fork"; defense tier as a survival
lever; any "raise your own altitude" strategy (void in principle — the boss's hover follows the player).

## Standing facts

* **Survival = total contacts ≤3** (20/20 kill below, 0/8 above).
* Contact needs `|dx| < 95` **and** `|dy| < 92`.
* **The fork is the phase transition** at a fixed tick, observed as an `ai[0]` difference with identical
  position, and it **never reconverges**. Strong 300/400 weak arms are byte-identical but for boss life.
* Strong 1200 and strong 2000 are **native-measured zero-hit kills**; weak has **no** zero-hit run.
* `wingTime` **does** refill airborne at the apex (`vy == 0 && releaseJump`).
* Charge cadence: **28 ticks** per charge, hovers of **30** or **40**, dominant period **58 ticks**,
  constant across DPS.

Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`.
