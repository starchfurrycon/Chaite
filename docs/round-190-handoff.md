# Round 190 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline. Build clean. Tests **749 pass / 9 fail**.
Verified binary confirmed: `960A03BFF6050CF7…` ✅

New datasets: `kV-s*` (strong+shroomite, strict 6000 cap, 18 points), `kW2-s*` (strong+shroomite low band,
relaxed horizon), `kX-s*` (strong+obsidian strict verify). Script: `tmp/strict190.ps1`.

---

## 1. ★★★ The strict goal is now ACHIEVED and NATIVELY VERIFIED — on the strong wing

Criterion: `-maxticks` cap **6000**, `death == False`, **`hits == 0`**. The probe prints its own line:
**`ACCEPTED: zero hits in the native engine.`**

**Independently reproduced on the current build** (`kX-s*`), not read from stale artifacts:

| dps | ticks | hits | probe verdict |
|---|---|---|---|
| 1100 | 4409 | 4 | DIED (boss 7087 left) |
| **1175** | **4524** | **0** | **zero hits** |
| **1200** | **4441** | **0** | **zero hits** |
| **1300** | **4141** | **0** | **zero hits** |
| 1500 | 3659 | 2 | kill |
| **2000** | **2881** | **0** | **zero hits** |

| loadout | strict zero-hit points within the 6000 cap |
|---|---|
| strong + **obsidian** | **1175, 1200, 1300, 2000** |
| strong + **shroomite** | **1200, 1600** |
| **weak (both tiers)** | **none** |

**This is the first native confirmation of a no-hit kill in the project, and it is reproducible.**

## 2. Strong + shroomite: complete grid

**At the strict 6000 cap (18 points):**

| dps | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1500 | 1600 | 1700 | 1800 | 1900 | 2000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| verdict | T | T | T | T | T | T | K | K | K | K | K | K | K | K | K | K | K | K |
| hits | 5 | 5 | 5 | 3 | 3 | 5 | 1 | 2 | 1 | **0** | 1 | 1 | 1 | **0** | 1 | 2 | 3 | 1 |

(T = `test-time-limit`, player alive, boss unfinished)

* **900–2000: 12/12 kills inside the cap**, nine of them at **≤ 1 hit**.
* **300–800: cannot be killed inside 6000 ticks — a DPS problem, not a movement problem.**
  At dps 300 only **28804 of 78000** life is removed in the whole window (**boss on 49191, 63 %**).

**At the relaxed horizon (20000):**

| dps | ticks | hits | boss left | verdict |
|---|---|---|---|---|
| 300 | 10344 | 11 | 28976 | DIED |
| 400 | 8745 | 9 | 23284 | DIED |
| 500 | 7852 | 8 | 17066 | DIED |
| **600** | **8341** | 5 | 0 | **KILL** |
| 700 | 7064 | 6 | **1890** | DIED (**missed by 1890**) |
| **800** | **6391** | 5 | 0 | **KILL** |

**300/400/500/700 are real deaths, not merely timeouts.**

## 3. ★ The two tiers differ in kind (and §190's weak result repeats here)

| dps band | obsidian | shroomite |
|---|---|---|
| low (≤ ~800) | **better** — obsidian ends the fight sooner (kill at 300) | dies |
| high (900–2000) | mixed, 4 zero-hit points | **better** — 12/12 kills, 2 zero-hit points |

**Shroomite's defence makes long fights survivable and yields near-clean kills; obsidian's faster clear
matters more at low DPS.** Neither dominates. **Obsidian remains the owner-specified honest yardstick.**

## 4. Current honest acceptance panorama

| loadout | kills | strict `hits==0` |
|---|---|---|
| weak + obsidian | 11 / 27 (41 %) | **none** |
| weak + shroomite | 17 / 33 (52 %) | none (best **1 hit** at dps 1200) |
| strong + obsidian | 28 / 28 | **4 points** (1175/1200/1300/2000) |
| strong + shroomite | 900–2000: 12/12 in-cap | **2 points** (1200/1600) |

**Strict goal status: ACHIEVED on the strong wing at specific DPS points, on BOTH tiers; NOT met on the weak
wing at any point; NOT met at dps ≤ 800 on either wing/tier.**

**Nothing above is described as no-hit unless the probe printed `ACCEPTED: zero hits in the native engine.`**

## 5. What is left

1. **Weak wing zero-hit.** No weak configuration has ever reached 0. Best is **1 hit** (shroomite dps 1200).
   The weak wing remains the true remaining problem.
2. **Low band (dps ≤ 800) on both wings.** DPS 300 needs ~16000 ticks to finish; the player dies first.
   This is the second remaining problem, and it is a **survivability-over-time** problem, not a DPS problem.
3. **The dps-700 near-miss (1890 life) at the relaxed horizon** is a cheap candidate to convert.

## Do NOT re-attempt

The **entire vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5),
**descent floor (byte-identical and physically impossible: you cannot arrest a fall with the wings the fall
spent)**; dash timing 0/1/2/3/4/7/10/14 (closed at both ends); dash timing × ceiling (anti-composes);
start side right (−1); holding horizontal speed after a dash; reversing the dash direction; global horizontal
strategy; phase-3 teleport for weak. Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite is strictly worse" is **FALSE** (weak net **+1**, and it is the *only*
tier with an in-cap strong grid); "the weak refill point is y 6032 and is an apex" is **FALSE** (apex y 6507,
budget 0 there); "damage records are deferred" is **FALSE**.

## Method notes

* **`-WallSeconds` is capped at 900** — a larger value silently aborts every run.
* Stale run dirs cause `Run already exists; use a fresh RunName` — delete the `artifacts/game-probe-<tag>`
  directory before rerunning.
* Older artifacts do **not** record which armor was used; re-run before asserting a tier attribution.
* `phase` strings are not exported to the probe trace — A/B against the rule's disabled state instead.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
