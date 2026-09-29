# Round 181 handoff

## HEAD
**Source identical to the round-180 commit** — the round-181 lever was implemented, measured, and reverted.
Build clean. Tests **749 pass / 9 fail**. Weak 800 still reproduces `6384/2/0` with the shipped default intact.

---

## What was tested and why

Round 180's diagnostic showed the weak wing's two remaining hits (ticks 422 and 2186, byte-identical at
dps 800 and 950) happen with `wingTime == 0` on every frame of the lookback, and that the player only
regains budget from y≈6492 and y≈4586. The implied lever: **begin the refill descent earlier so the apex
lands higher.** Implemented as `CHAITE_REFILL_APEX_Y` (default off).

## Result: **three configurations, all byte-identical to the no-rule baseline**

| config | w800 | w850 | w900 | w950 |
|---|---|---|---|---|
| no rule (§180 default) | KILL 6384/2 | DIED 4913/5 | DIED 4736/5 | KILL 5462/2 |
| apex 3600, called **before** the ceiling rule | KILL 6384/2 | DIED 4913/5 | DIED 4736/5 | KILL 5462/2 |
| apex 3600, called **after** the ceiling rule | **KILL 6384/2** | **DIED 4913/5** | **DIED 4736/5** | **KILL 5462/2** |

Along the way I found and fixed **two of my own defects** — the ceiling rule (called later) was overwriting
the new rule, and the position test was inverted (it pushed an already-deep player deeper). **After both
fixes the runs were still byte-identical**, so the problem was not the implementation.

## ★★ Root cause: the weak wing physically cannot reach a high apex

Enumerated the guards directly (`tmp/apexwhy181.py`) over a 6385-frame run:

| classification | frames |
|---|---|
| climbing **and** `wingTime <= 60` **and** `y < 3600` — **the only firing case** | **0** |
| climbing and `wingTime <= 60` but `y >= 3600` | 1098 |
| descending and `wingTime <= 60` (rule requires a climb) | 3061 |

**Across all 2894 climbing frames the player's y is min 4705, p1 4725, p25 5250, p50 5715, p75 5999.**
**Zero of them are above y 3600.** A fairy-wing player in this arena is never above y≈4705 *while climbing*,
so there is no position from which to start a higher descent — the rule is **unreachable, not mistuned**.

## ★ Structural conclusion (the durable finding)

The weak wing's flight cycle (measured, `tmp/cycle181.py`):

| phase | rate | note |
|---|---|---|
| climb (`plan.jump=True`) | **−9.91 px/tick** | limited by `wingTimeMax = 130` |
| descend (refill phase, `plan.drop=True`) | **+10.01 px/tick** | long descent to refill |
| cycle apex | ≈ **y 6625** (climbing up from ≈7960) | |

**The weak wing cannot hold a narrow altitude band the way the no-hit video does.** Its cycle apex
(≈6625) sits *inside* the Boss's own band (256–500 tiles = 4096–8000 px). That is exactly why the altitude
ceiling takes the **strong** wing to 28/28 but the **weak** wing only to +5/−3: the strong wing can end the
fight early at dps ≥1175 — where **all** of its zero-hit runs sit — while the weak wing must grind on and
re-expose an apex inside the Boss band every cycle.

**Therefore the entire "vertical timing / apex height" family is physically closed for the weak wing.**
Further weak-wing progress needs a change to the **cycle itself** (climb efficiency, budget, or the
horizontal strategy), or acceptance of the weak wing's weakness.

## ★ Shroomite (high defence) is **worse** than obsidian on the weak wing

| dps | obsidian | shroomite |
|---|---|---|
| 300 | DIED 9472/10/**33286** | DIED **11250**/10/**24359** |
| 400 | DIED 8577/6/24377 | DIED **8989**/**8**/**21611** |
| 500 | DIED 7042/6/23793 | DIED **7802**/**7**/17440 |
| 525 | DIED 7351/6/18381 | DIED **7564**/6/**16471** |

**Shroomite converts none of these failures into kills and generally raises hits and fight length.** Higher
defence lowers damage per hit (visible as lower remaining Boss life) but lengthens the fight, so more
contacts land and the player still dies.

**Conclusion: the survival bottleneck is how many times the player is hit, not how much each hit costs.**
This matches §170 (total damage does not separate survival from death; only contact count does) and explains
why the round-179 altitude ceiling works while the armour tier does not. **Obsidian remains the honest tier.**

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** (original 28-point grid) | not re-run with the ceiling |
| weak wing | ~13 kills on the denser grid | **worse** — no failures converted |

**Strict `hits == 0`: strong 1175 / 1200 / 1300 / 2000; weak none.**

**Not met.** Goal stays **active**.

## Next round — the weak wing needs a different *kind* of lever

Vertical timing is closed (§181). The candidates left:

1. **Change the climb budget or efficiency.** The weak wing climbs at −9.91 for only `wingTimeMax = 130`
   ticks. `Player.cs:26992` refills only at an apex or on landing. Anything that raises the *usable climb
   per cycle* changes the cycle itself rather than its timing. Note **§172–174 already refuted** several
   `wingTime`-timing knobs — any retry must target climb **amount**, not schedule.
2. **Change the horizontal strategy so the Boss's hover phase puts its band above the player's apex** rather
   than through it. The tornado spawns at the Boss centre, so the horizontal cycle decides where the band
   lands.
3. **Phase-3 teleport modelling** — still unmodelled; the sibling video's top comment gives the pattern
   (teleport to the opposite side from the previous teleport to keep the player centred, then dash 1-2-3),
   and the owner's own comment says *"进三阶段要控血，这个公式不太行"*.
4. **Finish the four-grid sweep** (`tmp/grid180.ps1`, ~28/132 done) for the reported both-tier numbers.

## Do NOT re-attempt (added this round)

* **`CHAITE_REFILL_APEX_Y` / any "descend earlier to raise the apex"** — the weak wing's climbing ceiling is
  y≈4705, measured on 2894 climbing frames with 0 above 3600.
* **Judge by hit count instead of net kills** (round 177's rule).
* **Armour tier as a survival lever** — shroomite is strictly worse here (§182.6).
* Apex refill on weak (+1/−5); dash veto (0/−1); `CHAITE_WEAK_ALTITUDE_FLOOR` (−3); "weak under-dashes";
  "weak misses apex refills"; any 1–2 tick timing knob; tornado escape under any trigger; `CHAITE_TORNADO_*`;
  `CHAITE_WIDTH_HOLD`; `CHAITE_PRELOCK_LIFT`; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_LOCK_RUN_AWAY`;
  `CHAITE_COUNTER_DASH`; `CHAITE_DASH_SUPPRESS`; `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`;
  `CHAITE_CHARGE_ESCAPE_SIM`; `_refillGuardBudget > 0`; DPS-constant gates.
* **Parsing `hostileProjectiles` with a nested `position` key** (flat `x/y/vx/vy` only).
* **Reading `plan` from the first row only** (95.9% of rows are populated).

## Standing constants & interfaces

* **Shipped rule**: `WeakAltitudeCeilingDefault = 4400f` (275 tiles). Plateau **4000–4600 byte-identical**;
  cliff at 4800. `CHAITE_WEAK_ALTITUDE_CEILING=0` disables, positive overrides. Fairy-wing route only.
* **Weak wing cycle**: climb −9.91, descend +10.01, apex ≈6625, climbing y **never above 4705**.
* **Video trajectory**: video player y **218..275 tiles**; `worldSurface=500`, `rockLayer=750`,
  `arena.groundTop=500`; depth = `position.Y/16`.
* **Engine bands**: weak player 315..497 tiles; **Boss 256..500 tiles**. Tornado bands: weak y ≥ 6285;
  strong y 2740..6920. `_ceilingY = 800+480 = 1280 tiles (20480 px)`.
* **`plan` readable per tick**: `phase, horizontal, jump, drop, dash, riskScore`.
* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤50 live, box 225×63, `vx == 0`, spawned at the **Boss centre**.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, not `equipment.lifeMax`.
* Acceptance env: `CHAITE_ARMOR_TIER`, `CHAITE_SIM_DPS`, `CHAITE_SIM_DPS_FULL_TILES=400`,
  `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`,
  `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-strong-wing|fishron-fairy-wing`.
* Scripts: `tmp/apexreach181.py`, `tmp/apexwhy181.py`, `tmp/cycle181.py`, `tmp/revert181.py`,
  `tmp/grid180.ps1`, `tmp/hits180.py`, `tmp/tab180.py`.
