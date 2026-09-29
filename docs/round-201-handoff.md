# Round 201 handoff — the strong wing's holes are wider than recorded; a new lowest contact rate

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅
New dataset `kI4-s*` (strong obsidian, 6 points, 20 000 horizon).

---

## 1. ★★ Strong + obsidian, complete, 20 000 horizon

| dps | ticks | hits | **contact rate** (per 1000 t) | boss left | verdict |
|---|---|---|---|---|---|
| 300 | 15938 | 6 | 0.376 | 0 | **K** |
| 350 | 13901 | 5 | 0.360 | 0 | **K** |
| 400 | 12240 | 5 | 0.408 | 0 | **K** |
| 450 | 10937 | 4 | 0.366 | 0 | **K** |
| 500 | 7304 | 5 | 0.685 | 21642 | D |
| 550 | 7531 | 5 | 0.664 | 13926 | D |
| **600** | 7830 | 4 | 0.511 | 5096 | **D** |
| 650 | 7738 | 3 | 0.388 | 0 | **K** |
| **700** | 5965 | 4 | 0.671 | 14721 | **D** |
| 750 | 6781 | 3 | 0.442 | 0 | **K** |
| 800 | 6386 | 2 | 0.313 | 0 | **K** |
| 850 | 6045 | 4 | 0.662 | 0 | **K** |
| 900 | 5741 | 1 | **0.174** | 0 | **K** |
| 1000 | 5221 | 3 | 0.575 | 0 | **K** |

**Pass 10/14 (71 %).** Holes at **500 / 550 / 600 / 700** — **four, and non-adjacent**: `650` and `750` both
pass between them.

## 2. ★ Correction to my earlier description

I had repeatedly recorded the strong wing as "300–450 pass, only 500/550 are holes."
**The holes are four points (500/550/600/700)**, though still a minority of the 14 measurements, with
continuous passing above 800.

**Corrected: strong-obsidian usable set = `{300, 350, 400, 450} ∪ {650} ∪ {750, 800, 850, 900, 1000, …}`
(10 of 14; holes = `{500, 550, 600, 700}`).**

## 3. ★★ The contact rate is a *partial* predictor, not a criterion

* **largest rate among passes = 0.662 (dps 850)**
* **smallest rate among failures = 0.511 (dps 600)**

**The ranges overlap (0.511 < 0.662), so no single contact-rate threshold decides the outcome.**

**But** the three high-rate failures (500: 0.685, 700: 0.671, 550: 0.664) cluster in **0.66–0.69**, and the one
high-rate pass (850: 0.662) sits exactly at its lower edge:

**⇒ a danger band: `rate < 0.51` never failed (8 points); `rate ∈ [0.66, 0.69]` failed 3 of 4.**

## 4. ★★★ The most valuable single result: dps 900 at contact rate **0.174**

**dps 900 took 1 hit in 5741 ticks — rate 0.174, the lowest ever measured on this wing**
(previous best 0.313 at dps 800).

**This proves the state machine is *able* to hold the contact rate below 0.2 and still kill. So the "0.313
floor" that §195's theorem uses as an input is a sampling artefact, not a capability limit.**

**And it changes the low-band outlook directly:** a rate of 0.2 supports roughly 16 000 ticks of survival,
which is **exactly what dps 300–450 need** (§195: dps 300 needs ≤ 0.208, dps 500 needs ≤ 0.347).
So the low band is not closed by arithmetic after all — it is closed by *whether that trajectory can be
reproduced at lower dps*.

## 5. Next step

**The goal is no longer "fewer contacts" but "identify and reproduce the trajectory features of dps 900".**

Concretely: diff `kI4-s900` (rate 0.174, KILL) against `kI4-s800` (0.313, KILL) and `kI4-s700` (0.671, DIED)
tick by tick over the shared fight window and locate **which ticks 900 avoided what**. Because the harness is
deterministic (§197.1) these differences are exactly reproducible and can be attributed frame by frame.

## 6. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill (20000) |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **10/14**; holes {500, 550, 600, 700}; best rate **0.174** |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | **9/18 (50 %)**; 300–700 all die |
| weak + shroomite | **0** | **9/18 (50 %)**; 300–700 all die |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **Relaxed goal: not yet achieved on either wing**; the strong wing is 10/14 with four narrow holes.
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## Do NOT re-attempt

**Sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill, altitude floor, ceiling off, **descent floor (byte-identical and
physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling; start side right (−1); holding
horizontal speed after a dash; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–78 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4. **A single contact-rate threshold as the success criterion — the
ranges overlap (§205.4).**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — 480**; "the fork is invisible in
observables" **FALSE**; "`ai[3]` ≡ accumulated dash count" **FALSE**; **"the strong wing has only two holes" 
FALSE — it has four (500/550/600/700)**; **"the contact-rate floor is 0.313" FALSE — dps 900 reaches 0.174**.

## Method notes

* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **Env-gate any behaviour change and verify the default path is byte-identical before reading an A/B.**
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000/100 s) — a timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
