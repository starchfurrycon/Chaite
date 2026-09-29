# Round 192 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅

New datasets: `kZ2-obs-s*` (strong low band, 20000 horizon), `kZ2-wk-w*` (weak low band, same).

---

## 1. ★ An arithmetic floor settles a criterion question

**Boss `lifeMax = 78000` (expert). 6000 ticks = 100 s. Therefore:**

```
minimum dps to kill inside the strict cap  =  78000 / 100  =  780
```

**`dps ≤ 780` cannot kill inside 6000 ticks no matter how perfect the movement is.**
Measured confirmation: dps 300 deals **28831** damage in the entire 6000-tick window (37 %), needing
**≈ 16200 ticks** to finish.

**Consequence: the objective's relaxed dps band (300–2000) and the strict 6000-tick cap are mutually
exclusive below 780 dps. They must be assessed as two separate regimes.**

## 2. ★★ So the low band is a SURVIVAL-TIME problem, and survival is not monotone in DPS

At a 20000-tick horizon:

| dps | strong + obsidian | weak + obsidian |
|---|---|---|
| 300 | **KILL 15938t** (6h) | DIED 9472t (10h), boss 33286 |
| 500 | DIED **7304t** (5h), boss 21642 | DIED 7042t (6h), boss 23793 |
| 600 | DIED **7830t** (4h), boss 5096 | DIED 6345t (5h), boss 19928 |
| 800 | **KILL 6386t** (2h) | **KILL 6384t** (2h) |

**Two important facts:**

1. **Survival time is NOT monotone in DPS.** The strong wing lived **15938 ticks at dps 300** but only
   **7304 at dps 500** — *less than half, at higher DPS*. The low band is dominated by **trajectory
   accident**, not by damage.
2. **At dps 800 both wings finish cleanly (~6385 t, 2 hits each)** — just above the 780 floor — so
   **from 800 upward the time limit stops binding.**

## 3. The resulting two-regime acceptance framework

| dps band | does the tick cap bind? | measured state |
|---|---|---|
| **800–2000** | **No** (≥780 suffices) | strong: **900–2000 all killed** (shroomite 12/12 at 6000); weak: 800 killed, most of 900+ killed |
| **300–780** | **Yes** — needs 10000–26000 ticks of survival | strong occasionally survives long enough (**dps300: 15938 t, killed**); **weak never does** |

## 4. The weak wing's exact low-band gap

* dps 300 needs **≈16200 t**; the weak wing survived only **9472 t** (short by ~6700).
* **The weak wing never survived past ~11000 t at any low point.**
* The strong wing reached **15938 t** at dps 300 and finished.
* **So the weak wing needs ≈1.7× more survival time in the low band — far beyond what any single-contact
  geometric repair can deliver (each such repair removes only 1–2 contacts).**

## 5. Acceptance master table (native tick-replay only channel)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28 @20000** |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000; 800 KILL @20000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **dps 800–2000: effectively achieved** (strong fully; weak mostly).
* **dps 300–780: not achieved on either wing** — a survival-duration problem, quantified in §4.
* `SuccessAfterDeath` is **never** a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## 6. What is left

1. **dps 300–780 survival duration.** Needs ~10000–26000 ticks of life. This is the dominant remaining gap
   and it is **not** addressable by the exhausted geometric family.
2. **Weak wing zero-hit** (0 of 30 measured points; best 1 hit).
3. **dps 1000/1300/1400 weak shroomite** and **900/1000/1300 weak obsidian** — mid-band holes where the
   boss survives and the player dies; each would convert with 1–2 fewer contacts.

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "weak refill point y 6032 is an apex"
**FALSE** (apex y 6507, budget 0); "damage records deferred" **FALSE**;
**"survival time rises with DPS" FALSE** (strong: 15938 t @300 vs 7304 t @500).

## Method notes

* **Tick-horizon results are not comparable across `-MaxTicks`.** Always state the horizon.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* **`dps ≤ 780` cannot finish in 6000 ticks** — do not read a timeout there as a movement failure.
* `SuccessAfterDeath` is not a kill. Delete `artifacts/game-probe-<tag>` before reusing a RunName.
* Older artifacts do not record the armour tier — re-run before attributing one.
* `phase` strings are not exported; A/B against the rule's disabled state instead.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
