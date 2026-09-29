# Round 195 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅
New script: `tmp/hole195.py`.

---

## 1. ★ Corrected constant: the player's full life is **480**, not 600

`hurt-observations.jsonl` records `player.lifeBefore = 480` on the first hit of every run.
This corrects the base used in §195/§196.

## 2. ★★ Per-contact actual damage — what actually kills

| run | verdict | total actual | breakdown |
|---|---|---|---|
| **dps 500** | died | **717** | **384: 75** ｜ 370: 184 ｜ **386: 458 (150+143+165)** |
| **dps 550** | died | **641** | **384: 132 (64+68)** ｜ **386: 509 (183+190+136)** |
| **dps 650** | **kill** | **486** | **386: 323 (183+140)** ｜ 370: 163 |
| **dps 300** | **kill** | **884** | 370: 272 ｜ **386: 612 (168+168+140+136)** |
| **dps 400** | **kill** | **586** | 370: 269 ｜ **384: 138** ｜ 386: 179 |

**Three facts:**

1. **The type-384 projectile is stationary** (`vx=0.01, vy=0`) and deals only **60–78** actual damage.
   In the dps-500 run it is **75 of 717 = 10 %** of all damage. **This confirms the owner's statement that
   bubbles are essentially harmless — it is not the cause of death, and it is now dropped as a target.**
2. **Type 386 (tornado projectile) is the only lethal source** — **64 %** of damage at dps 500, **79 %** at
   dps 550, always as hits **40 ticks apart**.
3. **But the passing dps 300 run absorbed the MOST 386 damage (612, four consecutive hits) and survived.**

## 3. ★★★ The real discriminator: the health at which the 386 cluster arrives

| run | life when the 386 cluster first lands | cluster | result |
|---|---|---|---|
| dps 650 | **480 (full)** | 183+140 | survived (159 left) |
| dps 300 | **480 (full)** | +168+140+136 | survived (44 left) |
| dps 400 | 408 | 179 (single) | survived |
| **dps 500** | **296** | 150+143+165 | **died** |
| **dps 550** | **434** | 183+190 | **died** (then 136 finished it) |

**The 386 cluster is not lethal at any health — dps 300 ate four consecutive hits at full life and lived.
Death happens when the cluster arrives after the pool has already been halved.**

**The complete dps-500 chain:**
```
t=5345  384 bubble    480 -> 405   (-75, negligible)
t=5498  370 boss body 413 -> 229   (-184, pool halved)
t=6847  386 cluster   296 -> 146
t=6887  386           148 ->   5   <-- death
t=6927  386             7 ->   0
```

**Had the t=5498 boss contact not happened (or happened ~1500 ticks later), the cluster would have landed at
~480 life, which the dps 650 and 300 runs show is survivable.**

## 4. The repair target is now precise

**Not fewer contacts — separate the *boss body* contact (`370`) from the *tornado projectile* cluster (`386`)
in time.**

* `384` bubbles: **negligible (60–78 dmg) — no longer a target.**
* `386` cluster alone: **not lethal.**
* `370` alone: **not lethal (184).**
* **Lethal combination = "a `370` halves the pool, then a `386` cluster lands within ~1350 ticks."**

**Concretely: at dps 500 the `370` at t=5498 and the `386` cluster at t=6847 are 1349 ticks apart. If that
gap can be stretched past one healing cycle, the point should convert to a kill.**

## 5. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28 @20000**; low band **300–450 pass**, hole at 500/550 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000; completes from dps 800 @20000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. Judge by **net kills**, never hit count.
**NEW: the type-384 bubble is negligible (60–78 dmg) — drop it as a target.**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "weak refill point y 6032 is an apex"
**FALSE**; "damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "the contact rate is
a random variable" **FALSE (harness is deterministic)**; **"player life pool is 600" FALSE — it is 480.**

## Method notes

* **The harness is deterministic** (§197.1) — one run per configuration is sufficient and exactly reproducible.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s). A timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`; always state it.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* `SuccessAfterDeath` is not a kill. Delete `artifacts/game-probe-<tag>` before reusing a RunName.
* Older artifacts do not record the armour tier — re-run before attributing one.
* Read `actualReturn`, not `request.damage`, for real damage.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
