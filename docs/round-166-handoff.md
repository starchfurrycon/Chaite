# Round 166 handoff

## HEAD
`8d3de46` — pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.
**No source changes shipped**: the one implementation attempted was measured, refuted, and fully
reverted (`git diff --stat e26f7ed -- src/ tools/` is empty).

Objective **still NOT met**: strong 10/14, weak 4/14 on the obsidian full-range sweep.

## HEADLINE: three refutations/measurements, and a screening rule that should govern future rounds

### 1. "Pre-lock lift band" — implemented, refuted, reverted

**A real gap exists**: the co-location lift requests an ascend only when `|dy| < 24`
(`CoLocationBand`), but contact needs `|dy| < 92` — so **68 px is a dead zone with no lift commanded**.
The weak wing's level-flight climb caps near 5.08 px/tick, needing ~36 ticks to cross the 184 px band
against a 28-tick charge. Inertness passed (unset ⇒ weak 600 reproduces `5320/6/30138` exactly).

**It is refuted.** Widening the band made everything worse:

| band | ticks | hits |
|---|---|---|
| baseline | 5320 | 6 |
| 60 | 4894 | 5 |
| **92** | **2866** | 6 |
| 140 | 5348 | **7** |
| 200 | 3844 | 6 |

Nothing killed; hits moved both ways while ticks fell everywhere. Deleted from source per the standing
rule.

**Mechanism — this is the valuable part**: the boss's hover **follows the player**, so raising the
player also raises the boss and the relative geometry barely changes. **"Climb to raise your own
altitude" is void against this boss in principle, not merely unprofitable.**

### 2. ★ The charge cadence, measured exactly

* Every charge is **exactly 28 ticks** (weak 47/47, strong 24/24).
* Hover lengths take two values: **30** (dominant) and **40**.
* Charge-to-charge period is **bimodal**: **30 ticks** for **37/47** weak and **19/24** strong, with
  the remainder at 150–160 (repeated hovers before re-charging).

**The dominant cycle is 28 + 30 = 58 ticks and it is constant across DPS.** The player therefore has a
**30-tick window** after each charge, in which the weak wing's 5.08 px/tick yields **~152 px** — just
over the 92 px half-width of the death band.

### 3. `CHAITE_APEX_REFILL=1` is a zero-sum trade

| point | baseline | apex_refill on |
|---|---|---|
| weak 300 | 5839 / **8** / died | 7804 / **8** / died |
| weak 600 | 5320 / **6** / died | 6219 / **6** / died |
| weak 800 | 3875 / **7** / died | 6366 / **5** / **KILL** |

One gain, one regression, one no-change. **Informative detail**: all three runs get *longer* (+33%,
+17%, +64%), so the refill genuinely works — the player survives longer and deals more damage — but
**contact counts do not fall** (8 stays 8, 6 stays 6). Surviving longer is not being hit less; it
spreads the same contacts over a longer fight.

This is exactly consistent with round 164's criterion (survival = total contacts ≤3) and confirms
round 165's correction that the meter is not the operative variable.

## ★ Screening rule for future rounds

**Thirteen** separate attempts to improve movement by **rewriting the vertical output** under some
condition have now all been zero-sum. The only **two genuine wins** in this project (§129, §132) both
**added a missing pre-condition** rather than redirecting an existing command.

> **If a change rewrites `vertical`/`horizontal` under some condition, expect zero-sum.
> Only a change that permits or forbids an existing command has a chance of net gain.**

## Next step

**Put the weak wing into a descent (`vy ≈ +10`) at the lock, so the descent converts into a fast climb
on the first charge frame** — because descending is free (gravity, no meter cost) while climbing to
raise your own altitude is refuted (§1).

Strong 1200 measurably does this: it descends at +10 before the lock on 9 of 24 charges, then reaches
−9 to −16 immediately after.

**Acceptance criteria:**

1. weak pre-lock `vy` median: **−4.40 → ≥ +5** (descending)
2. weak first-frame charge `vy` median: **−4.40 → ≤ −9**
3. share of charges reaching `|dy| > 150`: **83% → 100%**
4. in-band frames per charge: **9.0 → ≤3.8**
5. weak low-DPS (300–800) contacts: **≤3**
6. weak obsidian: **4/14 → 14/14**

Given the screening rule, prefer an implementation that **permits/forbids** the existing climb command
at the right moment rather than rewriting the vertical value.

## Do NOT re-attempt

Pre-lock lift band (this round), `CHAITE_APEX_REFILL` as a default, `_refillGuardBudget` > 0, refill
cadence changes, dash direction/timing/ttc, perpendicular dash, i-frames, height-band escape, standoff
ceiling, `CHAITE_CASCADE_*`, tornado knobs, bubble-break, "reconverge after the fork", defense tier as
a survival lever, or **any** "raise your own altitude" strategy (refuted in principle in §1).

## Standing facts (unchanged)

* **Survival = total contacts ≤3** (20/20 kill below, 0/8 above); 20 of 28 strong points already pass.
* Contact needs `|dx| < 95` **and** `|dy| < 92`.
* **The fork is the phase transition** at a fixed tick, observed as an `ai[0]` difference with
  identical position, and it **never reconverges**.
* Strong 1200 and strong 2000 are **native-measured zero-hit kills**.
* `wingTime` **does** refill airborne at the apex (`vy == 0 && releaseJump`, or `autoJump &&
  justJumped`).

Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`.
