# Round 191 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅

New datasets: `kY2-obsidian0-w*` and `kY2-shroomite0-w*` (weak wing, **strict 6000 cap**, 15 points each).

---

## 1. ★★★ The weak wing is squeezed between running out of time and running out of health

All previous weak-wing grids used `-MaxTicks 20000`, which **does not match the strict criterion's 6000-tick
cap**. Re-measured at 6000, the failure *shape* is the finding.

**Weak + obsidian (6000 cap) — 15 points, 5 kills, `hits==0` = 0:**

| dps | ticks | hits | boss left | outcome |
|---|---|---|---|---|
| 300 | 6000 | 3 | 49164 | timeout (player alive) |
| 400 | 6000 | 3 | 39564 | timeout |
| 500 | 6000 | 3 | 29982 | timeout |
| 600 | 6000 | 5 | 20368 | timeout (player already dead) |
| 700 | 5616 | 6 | 18774 | DIED |
| 800 | 6000 | 2 | **1100** | timeout (**missed by 1100**) |
| 900 | 4736 | 5 | 15043 | DIED |
| 1000 | 5216 | 3 | 0 | kill |
| **1100** | 4793 | 5 | 0 | **`SuccessAfterDeath` — boss died, player died — NOT a kill** |
| **1200** | 4439 | 5 | 0 | **`SuccessAfterDeath`** |
| 1300 | 3731 | 5 | 8852 | DIED |
| 1400 | 3884 | 2 | 0 | kill |
| 1600 | 3465 | 1 | 0 | kill |
| 1800 | 3141 | 3 | 0 | kill |
| 2000 | 2881 | 2 | 0 | kill |

**Weak + shroomite (6000 cap) — 15 points, 7 kills, `hits==0` = 0:**

| dps | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1600 | 1800 | 2000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| verdict | T | T | T | T | D | T | **K** | D | **K** | **K** | **K** | D | **K** | **K** | **K** |
| hits | 3 | 3 | 3 | 5 | 7 | 5 | 3 | 5 | 3 | **1** | 5 | 7 | 4 | 3 | 2 |

### The squeeze
* **dps 300–800: the player survives but time runs out.** At dps 300 only **28831 of 78000** life (37 %) is
  removed in the whole 6000 ticks.
* **dps 1100–1200 (obsidian): the boss dies but so does the player** — the probe prints
  *"the Boss died but so did the player"* and **refuses it**.
* **Only the middle (1000, 1400–2000) manages both — always with 1–5 hits, never 0.**

**So there is no DPS value at which the weak wing is both alive and finished and untouched inside the cap.**
This is the concrete form of the vertical-mobility gap the owner described, and it contrasts with the strong
wing, which kills **12/12** across 900–2000 within the cap and produces zero-hit runs.

## 2. Tier comparison at the strict cap
**Shroomite 7/15 beats obsidian 5/15**, consistent with §190's 20000-cap result (17 vs 11). Both have
`hits==0` = **0** for the weak wing; best is **1 hit** (obsidian 1600, shroomite 1200).

## 3. Acceptance master table (native tick-replay is the only channel)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | 28/28 @20000 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 DPS points across both tiers; ZERO on the weak wing.**
* **Relaxed goal (300–2000 survive-and-kill): NOT achieved on either wing.**
* `SuccessAfterDeath` is **never** counted as a kill.
* **Nothing is called no-hit unless the probe printed `ACCEPTED: zero hits in the native engine.`**

## 4. What is left, ranked by value

1. **Weak wing, low band (300–800): finish the fight before 6000 ticks.** The player is *alive* the whole
   window at 300/400/500 — this is purely a **time** problem, and 800 misses by **1100 life**. Any small
   offence or positioning gain that shortens the fight converts these. **This is the most tractable gap.**
2. **Weak wing, dps 1100–1200: survive the finish.** The boss dies; the player dies too at 5 hits.
   **One or two fewer contacts flips these**, which is why the weak contact count is the key quantity.
3. **Weak wing zero-hit**: zero of 30 measured points. The true remaining problem.

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible: you cannot arrest a fall with the wings the fall has spent)**;
dash timing 0/1/2/3/4/7/10/14 (closed at both ends); dash timing × ceiling (anti-composes); start side right
(−1); holding horizontal speed after a dash; reversing the dash direction; global horizontal strategy;
phase-3 teleport for weak. Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite strictly worse" **FALSE** (better at the strict cap, 7 vs 5);
"weak refill point y 6032 is an apex" **FALSE** (apex y 6507, budget 0); "damage records deferred" **FALSE**.

## Method notes

* **`-MaxTicks` and `-WallSeconds` are separate**: WallSeconds caps at **900** and a larger value silently
  aborts every run. Tick-horizon results are **not** comparable across different `-MaxTicks`.
* `SuccessAfterDeath` is **not** a kill.
* Delete `artifacts/game-probe-<tag>` before reusing a RunName.
* Older artifacts do not record the armour tier — re-run before attributing a tier.
* `phase` strings are not exported; A/B against the rule's disabled state instead.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
