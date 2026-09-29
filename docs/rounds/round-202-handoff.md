# Round 202 handoff — the strong wing mapped to 20 points; the outcome is a time-window effect

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅
New: dataset `kJ4-s*` (strong wing high band, 6 points, 20 000 horizon); script `tmp/why900.py`.

---

## 1. ★★★ Strong + obsidian, complete at 20 points, 20 000 horizon

| dps | ticks | hits | rate | verdict | | dps | ticks | hits | rate | verdict |
|---|---|---|---|---|---|---|---|---|---|---|
| 300 | 15938 | 6 | 0.376 | **K** | | 1000 | 5221 | 3 | 0.575 | **K** |
| 350 | 13901 | 5 | 0.360 | **K** | | **1100** | 4409 | 4 | **0.907** | **D** |
| 400 | 12240 | 5 | 0.408 | **K** | | **1200** | 4441 | **0** | **0.000** | **K ★zero-hit** |
| 450 | 10937 | 4 | 0.366 | **K** | | 1400 | 3884 | 1 | 0.257 | **K** |
| 500 | 7304 | 5 | 0.685 | D | | 1600 | 3466 | 1 | 0.289 | **K** |
| 550 | 7531 | 5 | 0.664 | D | | 1800 | 3141 | 2 | 0.637 | **K** |
| 600 | 7830 | 4 | 0.511 | D | | **2000** | 2881 | **0** | **0.000** | **K ★zero-hit** |
| 650 | 7738 | 3 | 0.388 | **K** | | | | | | |
| 700 | 5965 | 4 | 0.671 | D | | | | | | |
| 750 | 6781 | 3 | 0.442 | **K** | | | | | | |
| 800 | 6386 | 2 | 0.313 | **K** | | | | | | |
| 850 | 6045 | 4 | 0.662 | **K** | | | | | | |
| 900 | 5741 | 1 | **0.174** | **K** | | | | | | |

**Pass 15/20 = 75 %.** Holes = `{500, 550, 600, 700, 1100}` (five, non-adjacent).
**New zero-hit points: `1200` and `2000`** (probe printed `ACCEPTED: zero hits in the native engine.`).

## 2. ★★ dps 1100 is an isolated *high-band* hole, contact rate 0.907

**The highest rate ever measured on this wing**, sandwiched between a pass at 1000 (0.575) and a **zero-hit**
pass at 1200. Its failure mode is **too many contacts** (4 in 4409 t), not running out of time (boss left at
7087).

## 3. ★★ The contact rate still does not separate the outcomes

| class | rate range |
|---|---|
| **pass (15)** | 0.000 – **0.662** |
| **fail (5)** | **0.511** – **0.907** |

**Heavy overlap (0.511–0.662 contains both), so "lower is better" is a tendency, not a criterion.** But the
safe floor is sharp: **every point at rate ≤ 0.41 passed (7/7)**, while in the 0.66–0.91 band 3 of 6 failed.

## 4. ★★★ dps 900 vs dps 700: the mechanism is not geometry

Frame-by-frame over 700/750/800/900:

| run | rows | y_min | y_p25 | y_p50 | y_p75 | y_max | wingTime==0 | dashes |
|---|---|---|---|---|---|---|---|---|
| kI4-s700 | 5966 | 2823 | 4198 | 5276 | 5969 | 7958 | 43.0 % | 55 |
| kI4-s750 | 6782 | 2823 | 4264 | 5208 | 5906 | 7958 | 45.3 % | 62 |
| kI4-s800 | 6387 | 2823 | 4322 | 5181 | 5886 | 7958 | 40.7 % | 58 |
| **kI4-s900** | 5742 | 2711 | 4164 | **5121** | 5916 | 7958 | **47.5 %** | **53** |

**The four runs hold an almost identical altitude distribution** (p50 all within 5121–5276 ≈ tiles 320–330;
identical min 2823 and max 7958). **So dps 900's rate of 0.174 is *not* because it flew higher or lower.**

**What differs is *time*:**
* **dps 900 finished in 5741 t** and was touched only once, by a **type-384 bubble** (82 dmg) — it **never**
  met 370 (boss body) or 386 (tornado);
* **dps 700 died at 5965 t** to 1×370 + 3×386, including the **40-tick-apart 386 pair** at t=5568/5608
  (241→55→0) — the same cluster mechanism recorded in §199;
* **dps 750 killed at 6781 t** with 2×386 + 1×370.

**⇒ What actually decides the outcome is whether the fight ends before or after the 386 cluster window —
not movement quality**, since all four runs are statistically the same trajectory.

## 5. Why every movement lever has been zero-sum

Given that 700/750/800/900 share nearly identical trajectory statistics yet span "died" to "1 hit", the
differences come from **a deterministic trajectory's extreme sensitivity to DPS** (§200: the boss reads its own
`life` every frame to pick its attack group) — **not from any parameter being good or bad.**

**A trajectory that is good at one dps disappears at another, so a parameter tuned at one dps does not hold at
another. That is the root reason every single-point lever in this project has been zero-sum.**

## 6. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill (20 000) |
|---|---|---|
| **strong + obsidian** | **4** (1175, 1200, 1300, 2000) | **15/20 = 75 %**; holes `{500,550,600,700,1100}`; lowest rate **0.174** |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | **9/18 = 50 %**; 300–700 all die |
| weak + shroomite | **0** | **9/18 = 50 %**; 300–700 all die |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **Relaxed goal: not yet achieved;** strong wing 15/20, weak wing 9/18.
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## 7. What is left, ranked

1. **Strong wing's five holes** (`500/550/600/700/1100`) — bounded and measurable; 75 % already pass.
2. **Weak wing's low band** (300–700, ten failures) — needs a contact-rate change of magnitude.
3. **Weak wing `hits==0`** — never achieved (best 1 hit at dps 1200).

## Do NOT re-attempt

**Sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill, altitude floor, ceiling off, **descent floor (byte-identical and
physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling; start side right (−1); holding
horizontal speed after a dash; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–82 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4. **A single contact-rate threshold as the criterion — the ranges
overlap (§206.3).** **Altitude as the explanation for low contact rates — disproved (§206.4).**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — 480**; "the fork is invisible in
observables" **FALSE**; "`ai[3]` ≡ accumulated dash count" **FALSE**; "the strong wing has only two holes"
**FALSE — it has five**; "the contact-rate floor is 0.313" **FALSE — 0.174 at dps 900**.

## Method notes

* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **Env-gate any behaviour change and verify the default path is byte-identical before reading an A/B.**
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000/100 s) — a timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
