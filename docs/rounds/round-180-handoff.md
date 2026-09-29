# Round 180 handoff

## HEAD
The altitude ceiling is now the **shipped default** (`WeakAltitudeCeilingDefault = 4400f`).
Build clean. Tests **749 pass / 9 fail** (unchanged).

---

## What changed this round

### 1. The winning rule is now the default, not a knob

Round 179 proved the rule at `CHAITE_WEAK_ALTITUDE_CEILING=4400` but left it **default-off**, which meant the
deliverable did not actually ship it. Round 180 changes the semantics:

* **unset → 4400** (the acceptance path now gets the rule with no environment setup);
* **explicit `0` → off** (byte-reproduces the pre-round-179 circuit);
* a positive value overrides, for experiments.

**Verified** at weak 800 obsidian:
* unset → `ticks=6384 hits=2 life=0` **KILL** (= the round-179 explicit-4400 result);
* explicit `0` → `ticks=3875 hits=7 life=33502` **DIED** (= the round-179 baseline).

The rule is inert on every route but the fairy-wing one, so the strong wing's reviewed circuit is reached
through its own entry rather than through this default — confirmed by the strong grid reproducing round 179's
numbers exactly.

### 2. ★ Attribution of the hits that remain (using the now-readable `plan.phase`)

| run | hit tick | source | dmg | script phase sequence before the hit | boss ai0 |
|---|---|---|---|---|---|
| kD-s1150 (strong, 1 hit) | 2537 | **npc 370** | 173 | `precharge-jump` → `charge-descend` | 5 → 6 |
| kD-s900 (strong, 1 hit) | 3481 | projectile **384** | 112 | `refill` → `charge-horizontal` → `charge-horizontal-dash` | 5 → 6 |
| kD-s1600 (strong, 1 hit) | 1990 | **npc 370** | 187 | `precharge-jump` → `charge-horizontal` | 5 → 6 |
| kC-w800 (weak, 2 hits) | **422** | **npc 370** | 161 | `refill` → `refill-dash` → `refill` | 0 → 1 |
| kC-w800 | **2186** | **npc 370** | 134 | `refill` → `charge-horizontal` | 0 → 1 |
| kC-w950 (weak, 2 hits) | **422 / 2186** | npc 370 | 161 / 134 | **identical to w800** | 0 → 1 |

**Two facts that change the picture:**

1. **The strong wing's residual hits are almost all Boss-BODY contacts, not tornadoes.** The source is
   `npc 370` (the Boss), all inside `precharge-jump` / `charge-*`. Only one 384 tornado hit remains (kD-s900).
   **This supersedes the round-175 damage census** (≈half 386, ≈third body): after the ceiling rule, body
   contact is now the dominant source. The originally-diagnosed "tornado/projectile blindness" is largely fixed.
2. **The weak wing's two hits happen at exactly the same ticks (422 and 2186) at both dps 800 and dps 950**,
   with identical phase sequences. They are **DPS-independent, fully deterministic** early/mid-fight events,
   not luck. That makes them directly targetable — far cheaper than a whole-grid search.

### 3. Partial obsidian/weak acceptance grid (denser than round 179's)

26 points complete before the round ended (the full 4-grid sweep is still running — see below):

**13 kills** (575, 625, 650, **775**, 800, **875**, 950, 1000, 1050, 1075, …) vs the pre-rule baseline's
10/28 on the original grid. Newly gained over the original 28-point grid: **625, 650, 775, 800, 875, 950,
1050** (775 and 875 were not in the original grid).

Still failing on the weak wing: 300–550 (low DPS, long fight), 600, 675, 700, 725, 750, 825, 850, 900, 1100.
Note **900 still fails** while 875 and 950 both kill — non-monotone in DPS, consistent with earlier findings.

## Not done / next round

1. **Finish the 4-grid sweep** (obsidian + shroomite × weak + strong, 33 points each). Started this round as
   `tmp/grid180.ps1`; ~110 runs remain. Artefacts: `game-probe-kG-{w,s}-<dps>` and `…-s` for shroomite.
2. **Target the weak wing's two deterministic hits (t=422, t=2186).** Because they are DPS-independent and
   phase-identical, the judgement can be "are these two ticks still hits", which is a very cheap search.
3. **Strong wing's residual body contacts** — distinguish escape failure from `precharge-jump` approach;
   phases are available.
4. **Strict zero-hit target**: strong still has 4 zero-hit points (1175/1200/1300/2000); **weak has none**.
5. **Phase-3 teleport modelling** — still not modelled; the sibling video's top comment gives the pattern
   (teleport to the opposite side from the previous teleport to keep the player centred, then dash 1-2-3).
   The owner's own comment on the no-hit video says *"进三阶段要控血，这个公式不太行"*.

## Do NOT re-attempt

* **`CHAITE_WEAK_ALTITUDE_FLOOR`** — round 177, net **−3**.
* **Judging any lever by hit count rather than net kills** — round 177's lesson.
* Apex refill on weak (+1/−5); dash veto (0/−1); "weak under-dashes"; "weak misses apex refills";
  any 1–2 tick timing knob; tornado escape under any trigger; `CHAITE_TORNADO_*`; `CHAITE_WIDTH_HOLD`;
  `CHAITE_PRELOCK_LIFT`; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_LOCK_RUN_AWAY`; `CHAITE_COUNTER_DASH`;
  `CHAITE_DASH_SUPPRESS`; `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`; `CHAITE_CHARGE_ESCAPE_SIM`;
  `_refillGuardBudget > 0`; DPS-constant gates; armor tier as a survival lever.
* **Parsing `hostileProjectiles` with a nested `position` key** (flat `x/y/vx/vy` only).
* **Reading `plan` from the first row only** (95.9% of rows are populated).

## Standing constants & interfaces

* **`WeakAltitudeCeilingDefault = 4400f`** (275 tiles). **Plateau 4000–4600 byte-identical; cliff at 4800.**
* `CHAITE_WEAK_ALTITUDE_CEILING=0` disables; positive overrides.
* **Video trajectory**: `tmp/hud177.py` → video player y **218..275 tiles**. `worldSurface = 500`,
  `rockLayer = 750`, `arena.groundTop = 500`; depth = `position.Y/16`.
* **Engine bands**: weak player 315..497 tiles; **Boss 256..500 tiles**.
  Tornado bands: weak y ≥ 6285 (p1 6364); strong y 2740..6920.
* `_ceilingY = 800 + 480 = 1280 tiles (20480 px)`.
* **`plan` readable per tick**: `phase, horizontal, jump, drop, dash, riskScore`.
* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤50 live, box 225×63, `vx == 0`, spawned at the **Boss centre**.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, not `equipment.lifeMax`.
* Weak climb peak −9.91 vs strong −16.52; `wingTimeMax` 130 vs 180; dash peak 14.50 both.
* Acceptance env: `CHAITE_ARMOR_TIER`, `CHAITE_SIM_DPS`, `CHAITE_SIM_DPS_FULL_TILES=400`,
  `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`,
  `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-strong-wing|fishron-fairy-wing`.
* Scripts: `tmp/grid180.ps1` (running), `tmp/hits180.py`, `tmp/tab180.py`, `tmp/hud177.py`,
  `tmp/strong179.ps1`, `tmp/ceilgrid178.ps1`, `tmp/floor177.ps1`.
