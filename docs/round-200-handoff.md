# Round 200 handoff — the weak wing mapped completely; the wing gap quantified

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅

New datasets: `kG4-w*` (weak obsidian, 18 points) and `kH4-w*` (weak shroomite, 18 points), both 20 000 horizon.

---

## 1. Why this round

The weak wing had only sparse samples (§193) and **had never been searched for internal holes**, unlike the
strong wing. Both tiers were therefore swept at **every 100 dps from 300 to 2000** (18 points each).

## 2. ★★ Weak + obsidian (honest yardstick), 20 000 horizon

| dps | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1500 | 1600 | 1700 | 1800 | 1900 | 2000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| verdict | D | D | D | D | D | **K** | D | **K** | D | D | D | **K** | **K** | **K** | **K** | **K** | **K** | **K** |
| hits | 10 | 6 | 6 | 5 | 6 | 2 | 5 | 3 | 5 | 5 | 5 | 2 | 1 | 1 | 2 | 3 | 3 | 2 |

**Passing set: `{800, 1000, 1400–2000}` = 9/18 (50 %)**

## 3. ★★ Weak + shroomite, 20 000 horizon

| dps | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1500 | 1600 | 1700 | 1800 | 1900 | 2000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| verdict | D | D | D | D | D | **K** | **K** | D | **K** | **K** | **K** | D | D | **K** | **K** | **K** | D* | **K** |
| hits | 10 | 8 | 7 | 7 | 7 | 5 | 3 | 5 | 3 | **1** | 5 | 7 | 5 | 4 | 2 | 3 | 4 | 2 |

`D*` = `SuccessAfterDeath` (boss died, player died — **not a kill**).

**Passing set: `{800, 900, 1100–1300, 1600–1800, 2000}` = 9/18 (50 %)**

**Both tiers pass 9/18 and both first pass at dps 800** — matching §195's minimum-DPS theorem for the weak
wing (848).

## 4. ★★★ The weak wing's real structure: a dead low end and a sparse high end

* **dps 300–700: every point dies on BOTH tiers (10 measurements, 0 passes).**
* **dps 800 is the first common pass.**
* Above 800 each tier still has holes: obsidian fails at **900/1100/1200/1300**; shroomite at
  **1000/1400/1500/1900**.
* **The two passing sets barely overlap.**

**⇒ The weak wing's usable set is not a band but a scatter:
`{800, 1000, 1400–2000}` (obsidian) and `{800, 900, 1100–1300, 1600–1800, 2000}` (shroomite).**

## 5. ★★ Versus the strong wing: half the availability

| loadout | points | kills | rate | dps 300–700 band |
|---|---|---|---|---|
| strong + obsidian | 7 (300/350/400/450/500/550/650) | **5** | **71 %** | **4/4 pass (300–450); only 500 and 550 are holes** |
| **weak + obsidian** | **18 (every 100)** | **9** | **50 %** | **0/5 pass (300–700 all die)** |

**This is the final quantitative form of the gap between the two loadouts:** the strong wing passes the entire
low band 300–450 with just two isolated holes, while the weak wing fails the **entire** low band 300–700 and
does not begin until 800, with many holes after that.

**It is the quantitative evidence for the owner's statement that the vertical-mobility difference requires two
separate scripts: the two wings' usable DPS sets have *different shapes*** (strong = "continuous low + continuous
high"; weak = "dead low + scattered high"), so **no single set of cadence parameters can cover both.**

## 6. Effect on the acceptance table

* The weak wing's results are corrected from sparse sampling (§193's 5/15, 7/15) to **complete grids
  (9/18 per tier)**.
* **Confirmed: the weak wing cannot meet "survive and kill across 300–2000"** — the low band 300–700 fails at
  **all ten measurements across both tiers**, and it is not a single-point movement defect (§195's theorem
  requires ≳1.7× better survival).
* **The weak wing's best result remains 1 hit** (shroomite dps 1200); it has **never** produced `hits==0`.

## 7. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill (20000 horizon) |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28**; 300–450 pass, holes 500/550 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | **9/18 (50 %)**; 300–700 all die |
| weak + shroomite | **0** | **9/18 (50 %)**; 300–700 all die |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **Relaxed goal (300–2000 survive-and-kill): NOT achieved on either wing;** the strong wing is closest
  (2 isolated holes at 500/550 against 4 holes at 300–700 + many more on the weak wing).
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## 8. What is left, ranked

1. **The strong wing's two-point hole (500/550)** — the narrowest, best-bounded defect anywhere in the
   project. Closing it makes the strong wing continuous from 300 to 2000 except for nothing.
2. **The weak wing's low band (300–700)** — ten failures; §195's theorem says this needs a contact-rate
   change of magnitude, not a point fix.
3. **Weak wing `hits==0`** — never achieved (best 1 hit).

## Do NOT re-attempt

**Sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill, altitude floor, ceiling off, **descent floor (byte-identical and
physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling; start side right (−1); holding
horizontal speed after a dash; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–78 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — it is 480**; "the fork is invisible
in observables" **FALSE**; "`ai[3]` is an equivalent proxy for the accumulated dash count" **FALSE**.

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
