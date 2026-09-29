# Round 194 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅

New datasets: `kA3-s*`, `kB3-s*` (determinism), `kB4-s*`, `kC3-s*` (all strong obsidian, 20000 horizon).

---

## 1. ★★★ The acceptance harness is fully deterministic

Identical configuration, different RunName:

| run | dps | ticks | hits | verdict |
|---|---|---|---|---|
| `kB3-s300-r996d` | 300 | **15938** | **6** | kill |
| `kB3-s300-rdbc4` | 300 | **15938** | **6** | kill |
| `kB3-s550-rb4f9` | 550 | **7531** | **5** | died |

**Byte-identical repeats**, and they match the earlier `kA3-s300` / `kZ2-obs-s300` runs (15938/6) too.

**⇒ There is no randomness and no run-time jitter. Every measurement is exactly reproducible; repeated
sampling is unnecessary.**

**This refutes §195's explanation** that the contact rate is a noisy random variable. It is a
**deterministic function of the whole trajectory — and a highly discontinuous one.**

## 2. ★★ The strong wing's low band, mapped exactly (obsidian, 20000 horizon)

| dps | ticks needed | actual ticks | hits | contact rate | boss left | verdict |
|---|---|---|---|---|---|---|
| 150 | 31200 | 20000 (timeout) | 9 | 0.450 | 28712 | died |
| 200 | 23400 | 15424 | 8 | 0.519 | 28381 | died |
| 250 | 18720 | 12368 | 6 | 0.485 | 28670 | died |
| 280 | 16714 | 11064 | 6 | 0.542 | 28856 | died |
| **300** | 15600 | **15938** | 6 | 0.376 | **0** | **KILL** ✅ |
| 350 | 13371 | 13901 | 5 | 0.360 | 0 | **KILL** ✅ |
| 400 | 11700 | 12240 | 5 | 0.408 | 0 | **KILL** ✅ |
| 450 | 10400 | 10937 | 4 | 0.366 | 0 | **KILL** ✅ |
| **500** | 9360 | **7304** | 5 | **0.685** | 21642 | **died** ❌ |
| **550** | 8510 | **7531** | 5 | **0.664** | 13926 | **died** ❌ |
| 650 | 7200 | 7738 | 3 | 0.388 | 0 | **KILL** ✅ |

**Boundary one — the lower bound is exactly `dps 300`**, the owner's interval floor. Below it (150/200/250/280)
**every point dies**; from 300 through 450 it **kills continuously**.

**Boundary two — a non-monotone hole at 500/550.** Both need **less** time than the passing dps 450 run
(9360/8510 vs 10400) yet **fail because the contact rate jumps to 0.66–0.69**.

**⇒ The usable set is NOT a continuous band.** It looks like
`[300, 450] ∪ {650} ∪ [900, 2000]`.

## 3. ★★ Low-band failures share an almost constant cumulative damage

Boss life remaining at death: **28712 / 28381 / 28670 / 28856** at dps 150/200/250/280 —
i.e. the player consistently deals **~49 000–50 000** before dying (`78000 − 49000 ≈ 29000`).

**So the low band fails by attrition, independent of DPS: the player can support roughly 49 000 damage
dealt, and the fight needs 78 000.** That is §195's theorem in a simpler form.

## 4. Consolidated position

* **Strong wing (honest obsidian, 20000 horizon): usable from `dps 300` = the interval floor**, with a
  non-monotone hole at 500/550 and full coverage from 650 up apart from that hole.
* **Strong wing zero-hit (`hits==0`): 4 points** (1175/1200/1300/2000, verified current build) plus
  1200/1600 on shroomite — **the strict goal is met on this wing.**
* **Weak wing: usable only from ~`dps 800`**; contact rate 0.788–1.056, i.e. the same attrition problem at
  roughly 2× the rate.
* **Strict goal: achieved on the strong wing; zero on the weak wing.**

## 5. What is left

1. **Weak wing low band** — needs the contact rate to fall from ~1.0 to ≤0.5. This is the dominant gap, and
   §195/§196 show a single-contact geometric repair is an order of magnitude too small.
2. **The strong wing's 500/550 hole** — the only *narrow*, clearly-bounded defect left on an otherwise
   passing wing. Two DPS points; both fail purely by contact rate (0.685/0.664 vs ≤0.41 elsewhere).
   **A lever that shaves the contact rate by ~40 % at those points closes the strong wing completely.**
3. **Weak wing zero-hit** (0 of 30 measured points; best 1 hit).

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "weak refill point y 6032 is an apex"
**FALSE** (apex y 6507, budget 0); "damage records deferred" **FALSE**; "survival time rises with DPS"
**FALSE**; **"the contact rate is a random variable / results need repeated sampling" FALSE — the harness is
deterministic (§197.1).**

## Method notes

* **The harness is deterministic — one run per configuration is sufficient and exactly reproducible.**
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s). A timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`; always state it.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* `SuccessAfterDeath` is not a kill. Delete `artifacts/game-probe-<tag>` before reusing a RunName.
* Older artifacts do not record the armour tier — re-run before attributing one.
* `phase` strings are not exported; A/B against the rule's disabled state instead.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
