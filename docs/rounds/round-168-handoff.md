# Round 168 handoff

## HEAD
Pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.
**No source changes** — the round was diagnostic, and it found the reason the last 13 interventions failed.

## HEADLINE: ★ the damage is NOT the boss — it is **projectile 386 (the Cthulhunado)**, and the script cannot see it

### 1. Damage-source census (from `hurt-observations.jsonl` → `reason.declaredProjectileType`)

Across all 28 strong points:

| source | meaning | share |
|---|---|---|
| **`declaredProjectileType: 386`** | **Cthulhunado tornado** (150×42, `timeLeft 840`) | **≈ half** |
| `declaredProjectileType: 384` | Sharknado tornado (150×42, `timeLeft 540`) | a few |
| no source | boss body contact | ≈ one third |

**Decompiled evidence** (`Projectile.cs:4671-4704`, `29902-30016`): both are `aiStyle 64`, which **spawns a
copy of itself every tick while decrementing `ai[1]`**, so **15 generations coexist** with `scale` 0.4→1.5.
Measured: **up to 50 hostile projectiles in a single tick**, and type 386's **actual box reaches
`225×63`**.

**★ The tornado box (225×63) is 2.6× wider than the boss body (85×71).** With the player's half-width 10
the horizontal threshold is **122 px**, against 95 px for the boss. **The tornado is the largest single
contact threat in this fight.**

### 2. ★ At the hit frames the player is **surrounded by a vertical wall**

* tick 6055 (a hit): **7 tornadoes within 300×200** — 3 left, 4 right, 3 above, 4 below.
* tick 6399 (a hit): **10 tornadoes, all 10 to the WEST**.

**The cluster is a wall perpendicular to travel**: at tick 6380 the 25 live 386s span only **148 px in x
but 909 px in y** (25 × 63 ≈ 1575, i.e. they overlap ⇒ a **continuous, gap-free vertical wall**), and the
whole wall's **`vx` is exactly 0** — it does not move horizontally, only drifts slowly upward.

Consequences:
* Horizontally it is a **900+ px continuous wall** you cannot run through at one point.
* But it is only **~150 px thick**, so **the far side is safe**.
* The player's measured x oscillates **2586 ↔ 2679** across consecutive hit frames — **the signature of
  being shoved by the wall.**

### 3. ★ This explains why 13+ `vertical` rewrites were all zero-sum

Every existing knob (charge normal, escape direction, perpendicular dash, pre-lock lift,
`_refillGuardBudget`, apex refill) tunes **the escape from the boss body's charge line**. **None of them
addresses the tornado, which is the dominant damage source.**

Round 166.1's finding ("climbing is void because the boss's hover follows the player") **remains true —
but it is about the boss body**. The tornado is an **independent** threat and is not bound by it.

### 4. The script **cannot see projectiles at all**

* `FishronWingScript.cs` never references `Projectile` — only comments about 386.
* `FormulaScriptInput` (`FormulaScriptController.cs:3-19`) carries only `BossType, Route, NativeState,
  NativeTimer, NativeSequence, NativeForm, PlayerBelowBoss, PlayerRightOfBoss` — **no projectile data**.
* `TargetSnapshot` has **no** projectile field.
* The probe already **observes** `hostileProjectiles` per tick (type, x, y, width, height, vx, vy, ai…),
  so the data exists in telemetry but **never reaches the decision**.

## ★ The failure criterion, finalised

| group | total damage taken | % of 480 pool |
|---|---|---|
| kills | 0 – 864 | 0% – **180%** |
| deaths | 179 – 496 | 37% – **103%** |

**Total damage does not separate at all.** A kill survives **180%** of the pool (s300: 6 hits over
15938 ticks, carried by regeneration); a death happens at **37%** (s475: 1 hit / 179).

**The only separator remains the total contact count: every one of the 9 deaths has 4–5 hits, and every
point with ≤3 hits kills.** So acceptance is exactly "**bring every point to ≤3 contacts**" — and since
contacts are mostly tornados, the lever is **tornado avoidance**, not further vertical tuning.

## Next step (concrete, and genuinely new)

**Give the script tornado awareness and prefer horizontal escape from the wall.**

1. Add a compact hostile-projectile view (type, x, y, w, h, vx, vy) to the snapshot, plumb it through
   `FormulaScriptInput`, and have `FishronWingScript` widen/move horizontally when a 384/386 box
   intersects the player's predicted position.
2. This is **not** a retry of `CHARGE_NORMAL_OWNER` (that dodges the **boss body's** normal and is
   refuted). It targets **projectiles**, which has **no existing implementation**.
3. It is consistent with the owner's two remarks: "**as long as horizontal speed is maintained
   throughout**" and that bubbles are "**basically no threat**".

**Acceptance criteria:**

1. strong failing points (475/500/525/550/600/625/700/1100/1125) → **total contacts ≤3**
2. strong → **28/28 kills**
3. weak low-DPS band (0/16) → first kills appear
4. weak → improve on **6/28**

## Do NOT re-attempt (unchanged)

`CHAITE_CHARGE_NORMAL_OWNER` / any `dy`-sign dodge flip; pre-lock lift band; `CHAITE_APEX_REFILL` as a
default; `_refillGuardBudget` > 0; dash direction/timing/ttc; perpendicular dash; i-frames; height-band
escape; standoff ceiling; `CHAITE_CASCADE_*`; tornado *spawn* knobs (`CHAITE_TORNADO_*` — these gate when
tornados appear, **not** how to dodge them); bubble-break; "reconverge after the fork"; defense tier as a
survival lever; any "raise your own altitude" strategy against the boss body.

## Standing facts

* **Survival = total contacts ≤3** (every death 4–5; every ≤3 kills).
* Contact with the **boss** needs `|dx| < 95` **and** `|dy| < 92`; contact with a **tornado** needs
  `|dx| < 122` **and** `|dy| < 52`.
* **The fork is the phase transition** at a fixed tick (`ai[0]` differs, position identical); it never
  reconverges. Strong 300/400 weak arms are byte-identical but for boss life.
* Strong **1200** and **2000** are native-measured **zero-hit kills**; weak has none.
* Charge cadence: **28 ticks** per charge, hover **30** or **40**, dominant period **58 ticks**.
* `wingTime` refills airborne at the apex (`vy == 0 && releaseJump`).
* First tornado damage lands from **tick ~5000** onward in most runs.

Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`.
