# Round 169 handoff

## HEAD
Pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.
**No source changes shipped** — the feature was implemented, measured, found net-negative, and fully
reverted (`git diff` vs the round-168 commit is empty).

## HEADLINE: ★ the tornado-escape layer converts **6 of 9** strong failures to kills — but destroys the low-DPS band, so it is net-negative

This is the **largest single-lever effect found in this project**, and it is also the clearest
demonstration of why the acceptance criterion is the hard part.

### 1. Implementation

New `src/Chaite.Core/TornadoEscape.cs` (+ a hook in `CombatPlanner.PlanFormula`): reads the 384/386
entries out of `snapshot.Threats`, predicts against the 225×63 box, and moves **horizontally** toward the
side with fewer tornadoes. Inert by default; inertness verified twice (unset ⇒ strong 625 reproduces
`7689/4/3480` byte-for-byte; strong 300 reproduces `15938/6/0`).

**★ Plumbing trap worth recording**: the first hook went into the generic formula block at
`CombatPlanner.cs:743` and did **nothing at all**. There are **two** formula paths — the generic block and
a **Fishron-specific `PlanFormula`** (`:757-877`) that assigns `plan.Horizontal` from the script at `:866`
and returns. **The latter is the one a Fishron formula route takes.** The hook had to move there.

### 2. The gate was the first defect, and fixing it unlocked the effect

Instrumenting the phase label with the live tornado count showed **5195 of 7690 frames (68%)** have a
tornado in the threat list, yet the escape fired on only **35 frames (0.67%)** — the gate required a box
to *already overlap*, and the engine resolves that same tick's damage before input matters. **Deleting the
hard gate** made the weighted prediction the trigger.

### 3. ★ The result: middle of the range improves dramatically, low DPS is destroyed

| dps | baseline (obsidian) | gate removed | |
|---|---|---|---|
| 475 | death, 20233 left | **KILL 10390, 1 hit** | ✅ |
| 600 | death, **5096** left | **KILL 8341, 2 hits** | ✅ |
| 625 | death, **3480** left | **KILL 8026, 2 hits** | ✅ |
| 700 | death, 14721 left | **KILL, 4 hits** | ✅ |
| 1100 | death, 7087 left | **KILL, 2 hits** | ✅ |
| 1125 | death, 4500 left | **KILL, 1 hit** | ✅ |
| 500 / 525 / 550 | death | still death | ✗ |
| **300** | **KILL 15938/6** | **death 11281/5, 24291 left** | ❌ **flip** |
| **400** | **KILL 12240/5** | **death 11167/7** | ❌ **flip** |
| **450** | **KILL 10937/4** | **death 10538/6** | ❌ **flip** |

Bounding the escape with a **40-tick latch** (instead of overriding every frame) does **not** help:
300 dies at 10813, 400 at 10215, 450 at 9675, 475 at 7983, 600 at 5849.

**Net: +6 middle kills, −3 low-DPS kills. Reverted.**

### 4. Mechanism — why the asymmetry

* A tornado exists in **68%** of frames, so **any** override conditioned on "a tornado exists" rewrites
  most of the fight.
* The **low-DPS band survives by long fight + regeneration** (§168.4: s300 lives 15938 ticks on 6
  contacts / 864 damage = **180% of its HP pool**). It is the **least able to absorb positional
  disturbance**: contact count doesn't fall but **survival time drops ~30%**, which breaks the
  regeneration mechanism that was carrying it.
* The **middle band** was "one breath short", so the same disturbance pushes it over the line.

**This is the 15th zero-sum result — and the first on the horizontal axis.** Combined with the 14 vertical
ones, the lesson sharpens: **both axes redistribute contacts rather than remove them.**

### 5. ★ Revised view of the gap (important and encouraging)

The gap is **smaller than previously stated**: strong is **19/28**, and **6 of the 9 failures are provably
reachable** — this round *did* reach them. The problem is purely that the mechanism that reaches them also
breaks the low-DPS band.

## Next step

**Find a form that acts only on the middle failing points.**

* **Do NOT** retry any override conditioned merely on "a 384/386 is present" — measured net-negative.
* **Do NOT** gate by DPS. DPS-constant gates have repeatedly proven fragile, and
  `CHAITE_DASH_DELAY_DPS` was already refuted as chaotic.
* **The untried form is gating on the BOSS's state**: e.g. apply the horizontal escape only while
  `bossLife` is inside a band, or only during phase 3, or only while a specific `ai[0]` state is active.
  This is the only gating axis not yet used.

Acceptance criteria (unchanged): strong failing points → total contacts ≤3; strong → 28/28; weak 6/28 → up.

## Do NOT re-attempt

Tornado escape conditioned on tornado presence (this round); pre-lock lift band;
`CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_APEX_REFILL` as a default; `_refillGuardBudget` > 0; charge-normal
perpendicular dash; i-frames; standoff ceiling; `CHAITE_CASCADE_*`; bubble-break; "reconverge after the
fork"; defense tier as a survival lever; any "raise your own altitude" strategy.

## Standing facts

* **Survival = total contacts ≤3** (every death 4–5; every ≤3 kills). Total damage does **not** separate
  (kills span 0–864 = 0–180% of pool; deaths 179–496 = 37–103%).
* Contact with the **boss** needs `|dx| < 95` and `|dy| < 92`; with a **tornado** `|dx| < 122`,
  `|dy| < 52`.
* Tornados: type 384 Sharknado, 386 Cthulhunado, both `aiStyle 64` spawning 15 generations; **up to 50
  live in one tick**; box reaches **225×63**; the cluster is a **~150 px thick, ~900 px tall wall with
  `vx == 0`**.
* **Strong obsidian 19/28**, weak **6/28**. Strong 1200 and 2000 are **zero-hit kills**; weak has none.
* Charge cadence: **28 ticks**/charge, hover 30 or 40, dominant period **58 ticks**.
* Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`, `tmp/fullgrid-te.ps1`.
