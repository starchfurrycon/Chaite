# Round 175 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail**. Build clean.
**Source is byte-identical to the round-168 tree** — the only delta is the round-171 `tools/GameProbe.cs`
probe-report fix (9 lines, zero behavioral change). Weak 300 reproduces `5839/8/51366` exactly.

---

## HEADLINE 1: ★ I found a real, previously unknown **script-level defect**

`FishronWingScript.cs` (3730 lines) contains **zero references to threats or projectiles**:

```
grep "Threat|Projectile|_relevant" src/Chaite.Core/FishronWingScript.cs  →  0 matches
```

The escape direction is chosen from the **Boss alone** — never from the geometry of the 384/386 tornadoes.

Yet the projectile data is **already available and already analysed**:
* `snapshot.Threats` carries every hostile projectile (geometry, trajectory, damage);
* `CombatPlanner.PrepareThreats` (:2862) filters them against the player's swept body with
  `_settings.ProjectileSafetyMargin` (:2914);
* `ImmediateRisk` (:3049) computes the exact player-future × projectile-future intersection.

**The generic path feeds all of that into `ScoreCandidate`. The Fishron formula path overwrites the
result** — `plan.Horizontal = script.Horizontal` (:845), `plan.Dash = script.Dash` (:849).
**And `RiskScore` is only ever *written* (`:706`, `:969`) and *never read*** (the plugin references it
nowhere). So: *the planner computes the projectile risk, discards it, then hands every mobility command to
a script that cannot see projectiles.*

This **directly explains** the earlier damage census — "≈half of all hits from 386, a few from 384".

## HEADLINE 2: per-tick evidence of the hit mechanism

At weak DPS 300, hit at **tick 3258**: a **stationary** 384 wall (`vx=0.01`) at `x=1470, y=6806.3`,
box **150×42**; player at `(1469.4, 6803.1)` → **`|dx|=1.0, |dy|=3.2`**.

| tick | player y | vy | controlLeft | controlJump | nearest 384 |
|---|---|---|---|---|---|
| 3250 | 6845.1 | −4.80 | 1 | 1 | d=89 |
| 3253 | 6830.1 | −5.10 | 1 | 1 | d=52 |
| 3257 | 6808.7 | −5.50 | 1 | 1 | **d=14** |
| **3258** | **6803.1** | −3.50 | 1 | 1 | **d=3.4 ← hit** |

At 3250 the player was **below the wall's bottom edge (6827.3) and safe**; it then **climbed at
vy≈−5 while dashing left at −14.5→−12.1** and flew from below the wall straight into it. Same shape
recurs: weak 3218, weak dps800 3460, strong 14022/14062.

## HEADLINE 3: the fix I built **failed twice** and has been fully reverted

I implemented the missing pre-condition — withdraw the dash when the escape sweep meets a hostile
projectile — and measured it in two forms:

| dps | baseline | current-velocity extrapolation | **commanded-velocity extrapolation** |
|---|---|---|---|
| 300 | DIED 5839/8/51366 | DIED 5599/9/52596 | **DIED 3931/8/60951** |
| 575 | **KILL** 5426/6 | **DIED** 5426/6/31072 | **DIED** 5426/6/31072 |
| 800 | DIED 3875/7/33502 | DIED 4014/8/31649 | (stopped) |

* **Current-velocity form**: `+0/−1`, and **8 of 12 points byte-identical** → it almost never fired
  (the dash's 14.5 only exists *after* the tick is applied).
* **Commanded-velocity form** (rebuilds next-tick speed from `Horizontal`/`Vertical`/`Dash` at 14.5 dash /
  −9.9 weak climb / terminal fall): fires properly and is **much worse** — weak 300 loses **33%** of its fight.

**Root cause — and the owner predicted it**: *withdrawing the dash removes the player's only acceleration.*
The owner's own words: *"克盾冲刺也是能临时提供极大加速度的手段呀"*. In the 28-tick locked-charge window
the dash (14.5) is the only input that materially changes position; cruise is 7–8 and wing climb is capped.
So declining to dash because a tornado lies ahead makes the player **die to the Boss instead of the
tornado** — exactly the 300-point degradation observed.

**This matches the round-170 screening rule**: withdrawing a command also withdraws its benefit, so a
prohibition must **substitute**, not **delete**. My implementation only deleted.

**All of it is removed.** `CombatPlanner.cs` is byte-identical to round-168.

## HEADLINE 4: a probe error of mine is corrected (invalidates earlier conclusions)

`boss-observations.jsonl`'s `hostileProjectiles[]` uses **flat keys** `x / y / vx / vy / width / height`,
**not** a nested `position` object. My analysis scripts in rounds 173–174 read the nested form, so every
value collapsed to `0` and produced the **false** conclusion "no projectile is near the player at any hit".
That conclusion is **void**. (Also confirmed: `plan` is **null** in every observation, so the script's phase
cannot be read from that file; use `player.controlUp/Down/Left/Right/Jump` instead.)

## ★ Bonus structural fact: vertical input model

Measured across every run: **`controlUp` is 0.0% on every single frame**, and the player is **airborne
100% of the time**. This is **correct by design**, not a bug: `Runtime.cs:436-449` routes jump-based lift
through `plan.Jump` → `controlJump` (38–62% on), while `controlUp` is fed only by `plan.FeatherFallUp`,
which this route never sets. So the player's vertical is **wing-glide/fall**, which is why ascent is capped
by `wingTime` and `plan.Drop` (`controlDown`, 41–62%) is the only deliberate descent.

## Acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| strong wing | **19/28** | 6/7 (sample) |
| weak wing | **6/28** | **8/28** |
| strict (`-maxticks 6000`, `hits == 0`) | strong 1200 & 2000; weak 2000 | — |

**Not met.** Goal stays **active**.

## Next round — redirect, do not cancel

The defect is real and the direction is still open **only in this form**:

> **Redirect the dash** rather than withdraw it: from the candidate escape directions, **drop those whose
> sweep meets a hostile projectile**, and choose one that neither meets a wall **nor** points back toward
> the Boss — i.e. substitute a *different* escape, keeping the acceleration.

Why this is not the refuted form: the refuted implementations **deleted** the dash (losing its acceleration).
This one keeps a dash and only changes its direction, which is the substitution the screening rule requires.

**Also worth doing**: the escape decision currently has no notion of "the tornado layer I am already inside".
Adding a **vertical** preference (climb out / drop out) rather than a horizontal one may matter, since the
walls are **~150 px wide but ~900 px tall** — escaping *around* them is cheaper than *through* them.

## Do NOT re-attempt (cumulative, new this round)

* **Withdrawing the dash because a projectile is ahead** — refuted in both extrapolation forms
  (current-velocity `+0/−1` with 8/12 points inert; commanded-velocity **−33% fight length** at weak 300).
* **Any "delete the dash" form of projectile avoidance** — the dash is the only acceleration in the lock window.
* Parsing `hostileProjectiles` with a nested `position` key (must use flat `x/y/vx/vy`).
* Reading the script phase from `plan` in `boss-observations.jsonl` (it is **null**).
* Round 174: moving the strong wing's fall→refill→climb cycle earlier (its cycling is incidental);
  `CHAITE_APEX_REFILL` on the weak wing (`+1/−5`); "weak misses apex refills"; "weak lacks wingTime".
* Round 173: weak `CHAITE_WEAK_DASH_DELAY` 0–3; **any 1–2 tick timing knob**; "weak under-dashes".
* Round 172: per-wing single-axis escape.
* Earlier: tornado escape under any trigger; `CHAITE_TORNADO_*` knobs; DPS-constant gates; armor tier as a
  survival lever; pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `_refillGuardBudget`; altitude raising.

## Standing constants

* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box **225×63** (measured 150×42 at scale 0.4),
  **~150 px wide × ~900 px tall wall**, `vx == 0` (stationary).
* Damage census: ≈½ contacts = 386, a few = 384, **≈⅓ = boss body**.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* **Weak climb peak −9.91 vs strong −16.52**; `wingTimeMax` **130 vs 180**; terminal fall **+10.01 both**;
  dash peak **14.50 both**; cruise 7–8 both.
* **Weak-wing first hit is ALWAYS tick 1367**; strong-wing first hit is tick 5409–5868.
* **Both wings refill only at apex** (20 weak / 31 strong before tick 1360); **zero landing refills**.
* `controlUp` = 0.0% always (by design); `controlDown` 41–62%; airborne 100%.
* Scripts: `tmp/climb175.py`, `tmp/proj175b.py`, `tmp/hurtsrc175.py`, `tmp/idx175.py`, `tmp/traj175.py`,
  `tmp/cmd175.py`, `tmp/vertcmd175.py`, `tmp/tab175.py`, `tmp/veto175.ps1`.
