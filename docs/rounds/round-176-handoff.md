# Round 176 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail**. Build clean.
**Source byte-identical to the round-168 tree** — sole delta remains the round-171 `tools/GameProbe.cs` fix.

---

## ★ Correction 1: my round-175 claim "`plan` is null in every observation" was **wrong**

I read only the **first line** of the file, which happens to be `plan: null`. Measured on `jY-w300-inert`:

| | |
|---|---|
| rows | 5840 |
| **rows with a populated `plan`** | **5601 (95.9%)** |
| usable fields | `phase, horizontal, jump, drop, dash, fire, riskScore, tacticalMode, replayFrame, …` |

So the script's **per-tick phase and commands have always been readable**. The "cannot read the phase"
caveats in rounds 173–175 are **void**. All of round 176's analysis uses `plan`.

## ★ Correction 2: round-175's causality was **wrong** — the risk is never *computed*

Round 175 claimed the planner "computes the projectile risk and discards it" (because `RiskScore` is
written but never read). Measurement refutes this:

| | weak dps300 |
|---|---|
| `plan.riskScore == 0` | **5600 / 5600 (100%)** |
| `hostileProjectileCount` mean | **16.5** (max 34; 4726/5840 frames nonzero) |
| `riskScore` in the 40 ticks before any hit | **all 0** |

Projectiles are present, yet the score is always 0 — because of **code structure**:

```
CombatPlanner.Plan()            L531
  └─ if (_formulaRoute != None) return PlanFormula(snapshot);   L537
CombatPlanner.PlanFormula()     L757  ← SEPARATE method, returns at L855
     LastCandidateCount = 0;    L759   ... never calls PrepareThreats/ScoreCandidate
CombatPlanner (generic body)    L635  PrepareThreats(...);
                                L706  plan.RiskScore = best.Score;
```

The fishron route **dispatches away** at L537 and never reaches L635/L706. **`riskScore` is 0 because the
risk was never computed, not because it was discarded.**

**Why this matters**: "computed but ignored" = *missing pre-condition* (the shape of both genuine wins).
"Never computed" = **adding a whole capability from nothing** — which explains why both round-175 veto
implementations failed: I was adding to a blank, not filling a gap.

## ★★ New finding: the two wings fly in **different altitude bands**, and the tornado occupies only one

| weak dps300 | p1 | p5 | p25 | p50 | p75 | p95 | full range |
|---|---|---|---|---|---|---|---|
| **tornado y** | 6364 | 6652 | 7227 | 7568 | 7735 | 7938 | **6285..7973** (span 1688) |
| **player y** | 5067 | 5067 | 5931 | **6718** | 7288 | 7939 | 5037..7958 |

| strong dps300 | p1 | p5 | p25 | p50 | p75 | p95 | full range |
|---|---|---|---|---|---|---|---|
| **tornado y** | 2916 | 3501 | 5067 | 5609 | 5928 | 6736 | **2740..6920** (span 4180) |
| **player y** | 3100 | 3529 | 4266 | **5101** | 5754 | 6086 | 2823..7958 |

**★ The weak wing's tornado never rises above `y = 6285`**, yet the player's median is 6718 and p75 is 7288 —
**more than half the fight is spent inside or immediately below the band**. The strong wing's tornado spans
**4180 px vs the weak wing's 1688**, in a completely different altitude range, because **the tornado follows
the Boss, which follows the player**: the weak wing drags them below it, the strong wing carries them above.

**So the tornado band is a function of the player's own altitude — not a fixed feature.**

## ★ The weak wing's altitude ceiling decays, and the last 500 ticks are wing-dead

| block | plY_min (highest) | plY_mean | meanWT | %WT==0 |
|---|---|---|---|---|
| 0–499 | **7074.9** | 7777.1 | 103.5 | 5.2% |
| 1500–1999 | 5326.1 | 6085.8 | 53.4 | 42.0% |
| 4500–4999 | 5326.5 | 5875.9 | 67.0 | 17.6% |
| 5000–5499 | **5036.7** | 5569.0 | 50.8 | 38.0% |
| 5500–death | 5066.8 | 5066.8 | **0.0** | **100%** |

Highest point falls **7075 → 5037** and mean **7777 → 5569** over 5000 ticks. The final 500 ticks before
death have `meanWT = 0.0` and `%WT==0 = 100%` — **no wing budget at all**, pure hover, caught at t=5481
with `dy` converging from +20 to 0 as the boss matched its descent (confirmed at hit 1367 too: no walls
present, `dy` 12.1 → −40.5 with player and boss descending in lockstep).

## ★ Feasibility verified for the new hypothesis

If the weak wing held `y ≤ 6200` (above the band top at 6285):

| | |
|---|---|
| frames already above it | **2022 (34.6%)** |
| mean `wingTime` while below | **67.5** |
| below with `wingTime > 0` | **3054/3818 (80.0%)** |
| below and already rising (`vy<0`) | **2205/3818 (57.8%)** |
| longest continuous stretch below | **1840 ticks** (from t=2735) |

**It is a route choice, not a budget limit** — the weak wing can climb, it simply isn't asked to climb that high.

**Risk recorded honestly**: 65.4% of frames are below the line with a single 1840-tick stretch, so this is a
**large route rewrite** and is expected to carry net-negative risk under §173.4's chaotic sensitivity.

## Acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| strong wing | **19/28** | 6/7 (sample) |
| weak wing | **6/28** | **8/28** |
| strict (`-maxticks 6000`, `hits == 0`) | strong 1200 & 2000; weak 2000 | — |

**Not met.** Goal stays **active**.

## Next round — the experiment design is fixed

1. Implement `CHAITE_WEAK_ALTITUDE_FLOOR` (**default off**, byte-identical baseline when unset):
   on the weak wing, when `player.Center.Y > floor` **and** `wingTime > 0`, lift that frame's `vertical`
   from `0 or +1` to `-1`. **It must NOT override the locked-charge escape direction** — §175.7 proved
   rewriting the escape forfeits the dash/climb. Non-charge frames only.
2. Test weak **300 / 575 / 800** first (the "all-dead / existing kill / new kill" representatives).
3. **Check the failure mode**: compare the tornado's `y` p50 before and after — if raising the player
   raises the band by more than ~500 px, the form fails (§176.3).
4. Judge on **obsidian**.

## Do NOT re-attempt (cumulative, new this round)

* Reading `plan` from the **first line only** of `boss-observations.jsonl` (95.9% of rows are populated).
* Believing the formula path "computes and discards" projectile risk — it **never computes it**
  (`PlanFormula` L757-855 bypasses `PrepareThreats` L635 / `ScoreCandidate` L706).
* **Withdrawing the dash because a projectile is ahead** — refuted in both extrapolation forms
  (round 175: current-velocity `+0/−1` with 8/12 points inert; commanded-velocity **−33%** fight length).
* Parsing `hostileProjectiles` with a nested `position` key (flat `x/y/vx/vy` only).
* Round 174: moving the strong wing's fall→refill→climb cycle earlier (its cycling is incidental);
  `CHAITE_APEX_REFILL` on the weak wing (`+1/−5`); "weak misses apex refills"; "weak lacks wingTime".
* Round 173: weak `CHAITE_WEAK_DASH_DELAY` 0–3; **any 1–2 tick timing knob**; "weak under-dashes".
* Round 172: per-wing single-axis escape.
* Earlier: tornado escape under any trigger; `CHAITE_TORNADO_*` knobs; DPS-constant gates; armor tier as a
  survival lever; pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `_refillGuardBudget`;
  **raising altitude relative to the Boss** (it follows — but see the band hypothesis, which is different).

## Standing constants & usable interfaces

* **`plan` IS readable** per tick: `phase, horizontal, jump, drop, dash, riskScore, replayFrame`.
* Altitude machinery: `_ceilingY = SkyEnrageCeiling + CeilingMargin` (≈4280, sky-enrage ceiling);
  `_floorY` = arena floor (≈**7958**); enforcement at `FishronWingScript.cs:3680-3681`.
* **Weak tornado band: y ≥ 6285** (p1 6364). **Strong tornado band: y 2740..6920.**
* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box 225×63 (measured 150×42 at scale 0.4), `vx == 0`.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* Weak climb peak **−9.91** vs strong **−16.52**; `wingTimeMax` **130 vs 180**; terminal fall **+10.01 both**;
  dash peak **14.50 both**; cruise 7–8 both.
* **Weak-wing first hit is ALWAYS tick 1367**; strong's is tick 5409–5868.
* **Both wings refill only at apex** (20 weak / 31 strong before tick 1360); **zero landing refills**.
* `controlUp` = 0.0% always (by design); `controlDown` 41–62%; airborne 100%.
* Scripts: `tmp/risk176.py`, `tmp/feasible176.py`, `tmp/nowall176.py`, `tmp/ceiling176.py`,
  `tmp/band176.py`, `tmp/bandfeasible176.py`.
