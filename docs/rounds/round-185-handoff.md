# Round 185 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.
Shipped config unchanged: `WeakDashDelayDefault = 4`, `WeakAltitudeCeilingDefault = 4400f`.

New datasets: `kZ-w*` (dash delay 0, 33 points), `kW-w*` (combination, 8 points), `kY-w*` (delay 1, 3 points).
Docs: `docs/fishron-native-truth.md` §186.1–§186.9.

---

## 1. The lever tested, and its mechanism

Round 184 derived from the vanilla recoil code (`velocity.X = -num4*9; velocity.Y = -4f;`, `Player.cs:21298`)
that an **earlier dash** lets the wing's powered speed (+7…+8) cancel the −9 recoil inside the 10-tick
immunity window. The existing knob is `CHAITE_WEAK_DASH_DELAY` (**smaller = earlier**, shipped **4**).

## 2. It looked strongly positive on the first five points

| dps | delay 4 (shipped) | **delay 0** |
|---|---|---|
| 850 | DIED 4913 / 5h | **KILL** 6036 / **4h** |
| 900 | DIED 4736 / 5h | **KILL** 5739 / 5h |
| 1100 | DIED 4793 / 5h | **KILL** 4788 / **4h** |
| 950 | KILL 5462 / 2h | KILL 5459 / 3h |
| 800 | KILL 6384 / **2h** | **DIED** 5328 / 5h |

## 3. ★ Full grid reverses it: **net −2**

| | points | kills |
|---|---|---|
| **delay 4 (shipped)** | 27 | **11** |
| delay 0 | 33 | 13 |
| delay 1 | 3 (decisive) | **0** |

On the **27 common points: net = −2.**

* **gained**: 675, 825, 850, 900, 1100 (5)
* **lost**: 575, 625, 650, 800, 1000, 1050, 1150 (7)

**Delay 1 is worse than delay 0** (dps 300: 10 hits, 400: 9, 450: 8 — delay 0 gives 8/6/6).
So **0 is the optimum of that axis and the shipped 4 was already it.**

## 4. ★★ The two levers flip *different* points — but **anti-compose**

| dps | shipped | delay 0 only | ceiling 0 only | **both** |
|---|---|---|---|---|
| 300 | DIED 9472/10h | DIED 10731/8h | — | DIED 8777/8h |
| **400** | DIED 8577/6h | DIED 8030/6h | DIED 8989/8h | **DIED 7244/9h** |
| **450** | DIED 7098/6h | DIED 7830/6h | DIED 7628/6h | **DIED 6072/9h** |
| **525** | DIED 7351/6h | DIED 6900/6h | DIED 7564/6h | **DIED 6552/8h** |
| **550** | DIED 8112/6h (boss 8548) | DIED 7026/6h | — | **DIED 5947/6h (boss 28381)** |

**The combination is worse than either lever alone at every measured point.** They both consume the *same*
resource — the player's ability to move off the Boss's charge line — so stacking them cancels and inverts.

This matches **§170.1**: both are **rewrites of the same quantity**, not additions of a missing pre-condition,
so each can be optimal only alone. (Every genuine win in this project has added a *missing pre-condition*.)

## 5. The weak wing's hard core (unfixable by either lever or their combination)

**300, 400, 450, 475, 500, 525, 550, 600, 700, 725, 750, 825, 1200, 1400** — all **dps ≤ 1400**, fights
**5000–11000 ticks**.

**Why:** §182 measured that damage per hit is nearly equal between the wings (137.9 vs 184.7) and life lost at
dps 300 is identical (885 vs 884), so **only the contact count separates survival from death**. A low-DPS
fight runs 5–10× longer, 5–7 contacts are lethal, and **one geometric repair removes only 1–2 contacts**.

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | **11 kills / 27 points** (shipped optimum) | worse (§182.6) |

**Strict `hits == 0`: strong 1175/1200/1300/2000; weak none. Not met — goal active.**

## Next round — what is actually left

Two degrees of freedom are now exhausted and provably non-composable. What has **not** been tried:

1. **Reduce the contact count at the source rather than by geometry.** Every lever so far moves the player.
   The other side of the equation is **how many charge cycles the fight contains**. §185's census splits
   contacts roughly evenly between tornado 386 and body 370 across `charge-*`/`precharge-*`/`refill` phases.
   The **386 class** has not been attacked with a *spawn-position* argument: tornadoes spawn at the **Boss
   centre**, so the player's position at the hover preceding each charge decides where the wall lands.
2. **Re-examine the 2-hit kills (950 at delays 0/4; 800 at delay 4)** — these show the circuit *can* produce
   near-clean fights. Find what makes those specific trajectories different and whether it is controllable.
3. **Report the both-tier grid honestly** (`tmp/grid180.ps1`, ~28/132 done) and finish the shroomite arm with
   the ceiling, since the objective explicitly requires both tiers.

## Do NOT re-attempt (updated)

* **Any dash-timing value** — 0 is optimal, 1 is worse, and shipped 4 matches 0's net (§186.3).
* **Any combination of dash timing with the ceiling** — proven anti-composing (§186.7).
* **Holding horizontal speed after a dash** — engine-forced recoil (§185.6).
* **Reversing the dash direction** — forfeits the immunity contact grants (§185.10).
* Ceiling off (net −5); vertical timing (§174/175/177/181); horizontal strategy globally (§183.2);
  armour tier (§182.6); cycle restructuring (§183.1); phase-3 teleport for weak (never reached).
* Judge by hit count instead of net kills; nested `position` in `hostileProjectiles` (flat `x/y/vx/vy`);
  reading `plan` from the first row only; assuming a damage-record deferral (**there is none** — verified).

## Standing constants

* **Shield dash**: `vx = 14.5`, `eocDash = 15`; on hit `eocDash = 10`, `dashDelay = 30`,
  **recoil `(−9, −4)` = −(dash direction)×9**, **10 ticks immunity** starting at the hit.
* **Wing powered horizontal**: 7–8 px/tick toward the command; recoil decays ≈ **+0.7/tick**.
* **Boss charge** in the t=402–431 window: constant `(−9.28, −14.24)`.
* **Weak cycle**: climb −9.91 (130 t), descend +10.01 (198 t), refill every **328 t** at y≈6032, apex ≈6670,
  climbing y never above **4705**.
* **Ceiling**: 4000/4200/4400/4600 byte-identical, cliff 4800.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* `p2` at `life ≤ 39000`, `p3` at `life ≤ 11700`; 28 ticks/charge, hover 30/40, period 58.
* Round-185 scripts: `tmp/base185.py`, `tmp/tab185.py`, `tmp/early185.py`.
  Artifact tags: `game-probe-kG-w-<dps>` (hyphenated) for the shipped grid, `game-probe-kZ-w<dps>` (not).
