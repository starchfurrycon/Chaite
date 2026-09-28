# Round 177 handoff

## HEAD
**Source reverted and verified byte-identical to round-168** (`git diff 6ab9096 -- src/` empty).
Build clean. Tests **749 pass / 9 fail**. Weak 300 reproduces `5839/8/51366` exactly after the revert.

---

## What was tested

Implemented and ran the round-176 hypothesis: `CHAITE_WEAK_ALTITUDE_FLOOR=<y>` (default **0 = off**),
which on the weak wing turns a neutral-or-descending `vertical` into a climb while
`player.Center.Y > floor` and `WingTime > 0`, **only outside the locked-charge escape**.

**Inertness verified exactly**: with the variable unset, weak 300 reproduced `5839/8/51366`.

## Result: the mechanism works, but the net is **−3** — reverted

Full weak grid, obsidian, `floor = 6600`:

| dps | baseline | floor 6600 | effect |
|---|---|---|---|
| 300 | dead 5839/**8** | dead 4655/**6** | hits −2 |
| 400 | dead 5839/**8** | dead 4655/**6** | −2 |
| 450 | dead 7487/**9** | dead 4655/**6** | **−3** |
| 550 | dead 6948/7 | dead 5961/7 | |
| **575** | **KILL** 5426/6 | **dead** 4907/7/36097 | **LOST** |
| 650 | dead — | dead 4719/**6** | |
| **750** | dead — | dead 6203/6/**7123** | **7123 life from a kill** |
| **800** | dead 3875/7 | **KILL** 6373/**5** | **GAINED** |
| **900** | **KILL** 5726/5 | **dead** 4029/5/25648 | **LOST** |
| 950 | dead — | dead 5096/6/**5794** | 5794 from a kill |
| **1000** | **KILL** 5210/5 | **dead** 3376/5/30698 | **LOST** |
| 1050 | dead — | dead 4545/5/**7864** | 7864 from a kill |
| 1075 | KILL 4885/2 | KILL 4886/3 | |
| **1100** | dead — | **KILL** 4787/3 | **GAINED** |
| **1150** | **KILL** 4598/2 | **dead** 4599/5/**82** | **LOST — 82 life!** |
| **1200** | **KILL** 4432/2 | **dead** 3691/5/14926 | **LOST** |
| 1300 / 1400 / 1600 / 2000 | KILL | KILL | |

**NET: +2 gained / −5 lost = −3. Baseline weak kills 10 → floor 6600 weak kills 7.**

## ★ The failure-mode check came out **opposite to my prediction** — and that is informative

Round 176 predicted that raising the player would raise the tornado band (since the tornado follows the
Boss which follows the player). Measured at weak dps 300:

| config | tornado p25 | p50 | p75 | player p25 | p50 | p75 |
|---|---|---|---|---|---|---|
| baseline (off) | 7227 | **7568** | 7735 | 5931 | **6718** | 7288 |
| floor 6200 | 6221 | **6684** | 6956 | 5467 | **5820** | 6037 |
| floor 6600 | 6521 | **6778** | 7526 | 5749 | **6133** | 6885 |
| floor 7000 | 6769 | **7054** | 7770 | 6040 | **6648** | 6933 |

**The player moved up and the tornado moved DOWN.** The reason is that the tornado spawns at the **Boss
centre** (AI_069), and the Boss's hover *lags* the player, so changing the player's altitude phase shifts
where the Boss happens to be when it fires — in the opposite direction.

**This is favourable, not harmful**: at floor 6200 the player's p50 rises 6718→5820 while the tornado's
falls 7568→6684, so relative clearance *widens* 850→864, and the player's y distribution compresses
(p25..p75 span **1357 → 570**). The mechanism genuinely works — which is why **hits fall almost everywhere**
(450: 9→6; 300/400: 8→6; 800: 7→5).

## ★★ The real lesson of this round

**Reducing hits is not the same as gaining kills.** This project's acceptance is *kill and survive*, and the
altitude floor buys "shorter fights with fewer hits" — it lowers hits while **shortening the run** at low DPS
(5839→4655 at dps 300), so less total damage is dealt before death.

This is the **same signature** as §174.3 (apex refill on weak: **+1/−5**) and §175 (the veto: **0/−1**):
**any lever that changes when flight budget is converted into vertical mobility saves some points and ruins
others, for a negative net.** Three independent levers, three negative nets.

**Therefore: judge every future lever by the net change in KILLS, never by a fall in hits.**

## Next round

The weak wing's remaining structure is now well mapped, and three whole families of "change the vertical /
change the budget timing" lever are refuted. What has **not** been touched is the strong wing's own deficit
(19/28 obsidian, not 28/28) — its failures are a different set of points, and the round-176 band data shows
it flies in a completely different altitude regime (tornado 2740..6920) that no weak-wing lever can inform.

**Proposed next direction**: characterise the strong wing's failing points the way round 176 characterised
the weak wing's — per-tick `plan.phase` + boss state at each hit (now that `plan` is known readable), and
look for a *missing pre-condition* (the only shape that has ever produced a genuine win) rather than another
timing knob. Given the −3/−5/−1 record of timing knobs, prefer diagnosis over new levers.

## Do NOT re-attempt (cumulative, new this round)

* **`CHAITE_WEAK_ALTITUDE_FLOOR` / any constant weak-wing altitude floor** — measured **net −3** at 6600
  (and the 6200/7000 probe points also lost the 575 kill).
* **Any "reduce hits" lever judged by hit count.** Three separate levers have now reduced hits and lost kills.
* Round 176: nothing to retry — its feasibility check was correct; only its *prediction* about the band's
  direction was wrong.
* Round 175: withdrawing the dash for a projectile ahead (both extrapolation forms).
* Round 174: moving the strong wing's fall→refill→climb cycle earlier; `CHAITE_APEX_REFILL` on the weak wing
  (**+1/−5**); "weak misses apex refills"; "weak lacks wingTime".
* Round 173: weak `CHAITE_WEAK_DASH_DELAY` 0–3; **any 1–2 tick timing knob**; "weak under-dashes".
* Earlier: tornado escape under any trigger; `CHAITE_TORNADO_*`; DPS-constant gates; armor tier as a survival
  lever; pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `_refillGuardBudget`; raising altitude relative to
  the Boss; `CHAITE_WIDTH_HOLD`; `CHAITE_PRELOCK_LIFT`; `CHAITE_LOCK_RUN_AWAY`; `CHAITE_COUNTER_DASH`;
  `CHAITE_DASH_SUPPRESS`; `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`; `CHAITE_CHARGE_ESCAPE_SIM`.
* **Parsing `hostileProjectiles` with a nested `position` key** (flat `x/y/vx/vy` only).
* **Reading `plan` from the first row only** (95.9% of rows are populated).

## 🚫 Trap that cost this round real time

My revert script located the helper method with the anchor `/// <summary>True when the locked-charge escape
direction is chosen by`, but an **earlier step in the same script had already deleted that text**, so
`s.index` threw and **the file had already been written with a partial revert**. Always:
(a) verify the revert with `git diff <baseline> -- src/` (**must be empty**), and
(b) grep for the removed identifiers afterwards. Both checks caught it; neither was assumed.

## Standing constants & usable interfaces

* **`plan` IS readable per tick**: `phase, horizontal, jump, drop, dash, riskScore, replayFrame`.
* Altitude machinery: `_ceilingY = SkyEnrageCeiling + CeilingMargin` (≈4280); `_floorY` = arena floor
  (≈**7958**); existing guard at `FishronWingScript.cs:3680-3681`.
* **Tornadoes spawn at the BOSS CENTRE (AI_069)**; where they land is decided by the Boss's hover phase,
  **not directly by the player** (§177.3). The Boss's hover lags the player.
* **Weak tornado band: y ≥ 6285** (p1 6364). **Strong tornado band: y 2740..6920** (span 4180 vs 1688).
* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box 225×63 (measured 150×42 at scale 0.4), `vx == 0`.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* Weak climb peak **−9.91** vs strong **−16.52**; `wingTimeMax` **130 vs 180**; terminal fall **+10.01 both**;
  dash peak **14.50 both**; cruise 7–8 both. **Both refill only at apex; zero landing refills.**
* `controlUp` = 0.0% always (by design); `controlDown` 41–62%; airborne 100%.
* **Weak-wing first hit is ALWAYS tick 1367**; strong's is tick 5409–5868.
* Acceptance: strong **19/28** obsidian, weak **6/28** obsidian / **8/28** shroomite. Strict zero-hit
  (`-maxticks 6000`): strong 1200 & 2000, weak 2000. **Not met.**
* Scripts this round: `tmp/floor177.ps1`, `tmp/tab177.py`, `tmp/bandshift177.py`, `tmp/revert177.py`.
* Run artefacts: `game-probe-k7-w300-inert` (inertness), `k8-w{300,575,800}-f{6200,6600,7000}`,
  `k9-w<dps>` (full grid), `kA-w300` (post-revert).
