# Round 204 handoff — a real controller/engine divergence, concentrated in the last tenth of the fight

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅

New scripts: `tmp/lock204.py` (lock-to-hit latency + dash usage), `tmp/raw204.py` (raw tick dumps),
`tmp/stall204.py` (divergence quantification).

---

## 1. The trigger: a hit landing **0 ticks after the lock**, on the dash frame

```
kI4-s900  hit t=3481 type=384 dmg=82 | lock t=3481 (0 before, state=6) | shield dash t=3481 (0 before)
kA3-s550  hit t=5146 type=384 dmg=68 | lock t=5146 (0 before, state=6) | shield dash t=5141 (5 before)
```

## 2. ★★★ Raw ticks: the velocity increments by *exactly* the acceleration step

`kI4-s900`, t=3473–3482:

| tick | ai0 | ai2 | player vx | player vy | px | py | |
|---|---|---|---|---|---|---|---|
| 3473 | 6 | 0 | **−7.944** | +10.01 | 2435 | 3994 | |
| 3474 | 6 | 1 | −7.512 | +10.01 | 2427 | 4004 | |
| 3475 | 6 | 2 | −7.080 | +10.01 | 2420 | 4014 | |
| 3476 | 6 | 3 | −6.648 | +10.01 | 2414 | 4024 | |
| 3477 | 6 | 4 | −6.216 | +10.01 | 2407 | 4034 | |
| 3478 | 6 | 5 | −5.784 | +10.01 | 2402 | 4044 | |
| 3479 | 6 | 6 | −5.877 | +10.01 | 2396 | 4054 | |
| 3480 | 6 | 7 | −5.970 | +10.01 | 2390 | 4064 | |
| **3481** | 6 | 8 | **+4.500** | −3.500 | 2375 | 4074 | **HIT(384, 82) + DASH-START** |
| 3482 | 6 | 9 | +8.000 | −3.100 | 2383 | 4070 | |

**The vx at t=3474–3478 rises by exactly +0.432/tick** — that is *active left-input acceleration*
(0.92 inertia decay + 0.9 accel ≈ +0.072 × 6), **not a lack of input.**

**And at t=3481 vx jumps −5.97 → +4.5** and vy +10.01 → −3.5: the shield dash fires **on the lock frame**,
**opposite to the direction the player had been accelerating for 8 ticks.**

## 3. ★★★ The global divergence: plan asks for full input, engine shows vx = 0

Plan-carrying rows only (i.e. after takeover):

| run | plan rows | stall ticks (\|vx\|<0.5) | rate | stalls with `horizontal == 0` |
|---|---|---|---|---|
| kI4-s700 | 5727 | 259 | **4.5 %** | **0** |
| kI4-s750 | 6543 | 581 | **8.9 %** | **0** |
| kI4-s800 | 6148 | 681 | **11.1 %** | **0** |
| kI4-s900 | 5503 | 401 | **7.3 %** | **0** |
| kJ4-s1100 | 4171 | 381 | **9.1 %** | **0** |
| kA3-s300 | 15700 | 923 | **5.9 %** | **0** |
| kA3-s550 | 7293 | 268 | **3.7 %** | **0** |
| kJ4-s1600 | 3228 | 349 | **10.8 %** | **0** |
| kJ4-s2000 | 2643 | 339 | **12.8 %** | **0** |

**Across 9 runs and 4182 stall ticks, `plan.horizontal == 0` occurs 0 times.**
**The controller never asks to stop, yet the realized horizontal speed is zero.**

**This reproduces the owner's early complaint verbatim — "how can the player suddenly stand still?"**

## 4. The floor/platform explanation is **excluded**
An initial count said 38 % of stalls were at the floor y=7958 — **that was wrong: it included pre-takeover
rows with no `plan`.** Re-scoring plan-carrying rows only leaves **exactly 1 ground-aligned tick per run**.
**The stalls happen airborne.**

## 5. ★★ Stalls concentrate in the **last tenth** of the fight

`kI4-s900` by decile of fight length:

| 0-10 | 10-20 | 20-30 | 30-40 | 40-50 | 50-60 | 60-70 | **70-80** | 80-90 | **90-100** |
|---|---|---|---|---|---|---|---|---|---|
| 2.4 % | 0.0 % | 1.6 % | 1.0 % | 2.8 % | 1.6 % | 2.1 % | **14.1 %** | 1.2 % | **43.7 %** |

`kA3-s300`: last decile **19.5 %** vs **5.9 %** average.

**The final decile's stall rate is 4–6× the run average** — and §207.3 already showed **every death occurs at
93–97 % of the fight.** Two independent observations point at the same window.

## 6. Honest status of this finding
**Established:** (a) the controller never commands a stop, yet airborne vx is 0 in 3.7–12.8 % of ticks;
(b) this amplifies to 19.5–43.7 % in the last decile; (c) lethal hits can land 0 ticks after a lock, on a
dash frame whose direction opposes the preceding 8 ticks of movement.

**NOT established:** that the stalls *cause* the deaths. Both are enriched late, which is **correlation**.

**But it is the first finding with a property no previous lever had: it is a genuine controller/engine
*inconsistency* (the model asks to move, the engine doesn't), not a badly chosen parameter.** §206.5 explained
why parameter levers must be zero-sum; **an inconsistency of this kind has a determinate fix.**

## 7. Next step (highest priority)
Replay `kI4-s900`'s last 10 % (t≈5160–5741) and `kA3-s300`'s last 10 % (t≈14340–15938) tick by tick, find the
exact ticks where `plan.horizontal != 0` and `vx == 0`, compare the controller's internal predicted vx with the
observed vx, and locate the frame where the divergence begins.

**If it is a model/engine mismatch, fixing it could improve every loadout at every DPS simultaneously, because
it is global rather than per-DPS.**

## 8. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill @20000 |
|---|---|---|
| **strong + obsidian** | **4** (1175, 1200, 1300, 2000) | **15/20 = 75 %**; holes `{500,550,600,700,1100}` |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | **9/18 = 50 %**; 300–700 all die; best 2 hits |
| weak + shroomite | **0** | **9/18 = 50 %**; 300–700 all die; best 1 hit |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **Relaxed goal: not yet achieved;** strong wing 15/20, weak wing 9/18.
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## Do NOT re-attempt

**Sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill, altitude floor, ceiling off, **descent floor (byte-identical and
physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling; start side right (−1); holding
horizontal speed after a dash; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–82 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4. Contact-rate threshold as a criterion (ranges overlap).
Altitude as the explanation for low contact rates (§206.4). Cornering/arena walls (§207.1). Hit clustering
(§207.2). Extending the horizon (§207.5).

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — 480**; "the fork is invisible in
observables" **FALSE**; "`ai[3]` ≡ accumulated dash count" **FALSE**; "the strong wing has only two holes"
**FALSE — five**; "the contact-rate floor is 0.313" **FALSE — 0.174**; "hits cluster near the arena walls"
**FALSE**; "`-maxticks` can be raised past 24000" **FALSE**; **"stalls happen on the ground/platforms"
FALSE — plan-carrying stalls are airborne (§208.4).**

## Method notes

* **Filter to plan-carrying rows before measuring divergence** — pre-takeover rows have no `plan` and skew
  every rate (this produced the wrong 38 % floor figure).
* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **`-maxticks` must be in 600..24000.**
* **Env-gate any behaviour change and verify the default path is byte-identical before reading an A/B.**
* Sorting a tuple with a bool first mislabels groups — sort by an explicit key.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000/100 s) — a timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
