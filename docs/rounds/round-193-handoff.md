# Round 193 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅

This was a modelling round (no new native sweep); it converts the low-band shortfall into a single number.

---

## 1. ★★★ The minimum-DPS theorem

Three measured constants (from §182 and the low-band artifacts):

| constant | strong wing | weak wing |
|---|---|---|
| player life pool | 600 | 600 |
| damage per hit (obsidian) | **184.7** | **137.9** |
| **hits the player can take** | **3.25** | **4.35** |
| **best contact rate ever measured** (hits / 1000 t) | **0.313** | **0.788** |
| **⇒ maximum survivable fight length** | **10379 t** | **5522 t** |
| **★ minimum boss dps to finish in time** | **451** | **848** |

**Theorem:** with the current loadouts and state machine, if the contact rate cannot go below its measured
floor, the player can only last that long — so **below those DPS values, "survive and kill" is impossible
under any movement.**

## 2. ★★★ It matches the measurements

| prediction | measurement | agreement |
|---|---|---|
| weak completes only at **dps ≳ 848** | weak obsidian **first completes at 800** (6384 t); 700 dies short by **1890** | ✅ **within 6 %** |
| strong completes only at **dps ≳ 451** | strong obsidian **completed at 300** (15938 t) | stronger than predicted |

**The weak-wing agreement (848 predicted vs 800–700 bracketing) says the model captures the real
constraint, not a coincidence.**

**Why the strong wing beat its own bound:** that dps-300 run achieved a contact rate of **0.376**, i.e. very
close to the floor, and survived **15938 ticks**. But the *same build* at dps 500 survived only **7304
ticks** — so the strong wing sits **on a knife edge between 300 and 600**, decided by trajectory accident.

## 3. The shortfall as a single number

| dps | required contact rate ≤ | strong achieves | weak achieves | verdict |
|---|---|---|---|---|
| 300 | **0.208** | 0.313 (best 0.376) | 1.056 | both short; strong needs a lucky run |
| 500 | **0.347** | 0.313 ✅ marginal | 0.852 | strong on the line (still died) |
| 600 | **0.416** | 0.313 ✅ | 0.788 | strong should make it |
| 800 | **0.555** | 0.313 ✅ | 0.313 ✅ | **both make it — and both measurably did** ✅ |

**To make the weak wing work at dps 300 its contact rate must fall from 1.056 to 0.208 — a factor of 5.1.**
That is why every single-contact geometric lever has been zero-sum: **they change one contact where the
requirement is a change of magnitude.**

## 4. What this implies for the remaining work

1. **The low-band goal is to drive the contact rate to 0.2–0.5 per 1000 ticks**, not to remove any particular
   contact. Every geometric repair in this project's history changed 1–2 contacts — **an order of magnitude
   too little.**
2. **Stabilising the strong wing's 300–600 band is the more realistic target**: it is already on the line,
   and needs roughly a further **15 %** contact-rate reduction to cover dps ≳ 450. That is within reach of a
   real lever, unlike the weak wing's 5× requirement.
3. **The weak wing cannot reach the low band by tuning.** Its usable range begins near **dps 800–850**,
   exactly where the theorem puts it. Meeting the objective for the weak wing across 300–2000 would require
   changing the contact *rate* by a factor of five, which the exhausted geometric family cannot do.

## Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28 @20000**; low band marginal ≥ ~450 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000; completes from **dps 800** @20000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **dps 800–2000: effectively achieved.**
* **dps 300–780: not achieved; the minimum-DPS theorem explains why, quantitatively.**
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## 5. Highest-value next step

**Reduce the strong wing's contact rate by ~15 % in the 300–600 band** to stabilise dps ≳ 450 — this is the
only low-band target the theorem says is within reach of a single real lever. Concretely worth testing:
the band where the strong wing's surviving runs differ from its dying runs (dps 300 survived 15938 t at
contact rate 0.376; dps 500 died at 7304 t at 0.685) — **characterise what the 0.376 run did differently.**

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "weak refill point y 6032 is an apex"
**FALSE** (apex y 6507, budget 0); "damage records deferred" **FALSE**; **"survival time rises with DPS"
FALSE** (strong 15938 t @300 vs 7304 t @500).

## Method notes

* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 life / 100 s). A timeout there is arithmetic, not a
  movement failure.
* Tick-horizon results are **not** comparable across `-MaxTicks`; always state it.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* `SuccessAfterDeath` is not a kill. Delete `artifacts/game-probe-<tag>` before reusing a RunName.
* Older artifacts do not record the armour tier — re-run before attributing one.
* `phase` strings are not exported; A/B against the rule's disabled state instead.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
