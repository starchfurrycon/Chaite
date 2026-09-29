# Round 205 handoff — §208 RETRACTED; the real defect is high-frequency horizontal oscillation

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅

New scripts: `tmp/onset205.py`, `tmp/stop205.py`, `tmp/decile205.py`, `tmp/fight205.py`,
`tmp/sustain205.py`.

---

## 1. ★★★ RETRACTION of §208 — "the controller asks to move, the engine doesn't" was **wrong**

§208 rested on "`plan.horizontal != 0` while `|vx| < 0.5`". I re-tested for a **genuine** sudden stop:
`|vx|` falling from **≥ 3.0 to < 0.5 between consecutive ticks** while the input stays non-zero.

| run | plan rows | **genuine sudden stops** |
|---|---|---|
| kI4-s700 / s750 / s800 / s900 | 5727 / 6543 / 6148 / 5503 | **0 / 0 / 0 / 0** |
| kJ4-s1100 / s1600 / s2000 | 4171 / 3228 / 2643 | **0 / 0 / 0** |
| kA3-s300 / s400 / s550 | 15700 / 12002 / 7293 | **0 / 0 / 0** |

**Nine runs, ~79 000 rows, ZERO sudden stops.**

**The §208 "4182 stall ticks" was a measurement error:** `|vx| < 0.5` also catches the **natural zero
crossing of a direction reversal** — and the onset analysis confirms it, since the tick before each "stall"
onset has signature `(horiz=±1, vx=∓0.6…0.9, wingTime=0)`, i.e. **the frame where the player is accelerating
the other way.**

**⇒ There is NO controller/engine horizontal divergence. The "determinate fix" direction of §208 is VOID.**

## 2. ★★★ What IS real: mean |vx| ≈ 7, but the direction reverses about every 30 ticks

Fighting-only (ticks after the boss dies are excluded):

| run | ticks | \|vx\| mean | sign runs | median run | p90 run | **net** | **gross** | **net/gross** |
|---|---|---|---|---|---|---|---|---|
| kI4-s700 | 5727 | 6.66 | 135 | **2** | 131 | 1169 | 36546 | **0.032** |
| kI4-s800 | 5844 | 6.71 | 268 | **1** | 39 | 3104 | 39226 | **0.079** |
| kI4-s850 | 5503 | 7.04 | 57 | 44 | 331 | 3051 | 38764 | **0.079** |
| kI4-s900 | 5199 | 6.87 | 79 | 33 | 251 | 2784 | 35715 | **0.078** |
| kJ4-s1100 | 4171 | 6.09 | 194 | **2** | 38 | 3523 | 24009 | **0.147** |
| kJ4-s1200 | 3899 | 7.13 | 31 | 58 | 362 | 2673 | 27782 | **0.096** |
| kJ4-s1400 | 3342 | 7.17 | **22** | **72** | **462** | 1399 | 23973 | **0.058** |
| kJ4-s1600 | 2924 | 7.03 | 59 | **2** | 236 | 3370 | 20546 | **0.164** |
| kJ4-s2000 | 2339 | 6.92 | 75 | **1** | 58 | 566 | 16181 | **0.035** |
| kA3-s300 | 15396 | 6.72 | 311 | 12 | 120 | 1136 | 103526 | **0.011** |
| kA3-s400 | 11698 | 6.89 | 192 | 28 | 178 | 992 | 80667 | **0.012** |
| kA3-s450 | 10395 | 6.83 | 267 | **2** | 98 | 1574 | 71046 | **0.022** |
| kA3-s550 | 7293 | 6.61 | 135 | 16 | 200 | 282 | 46512 | **0.006** |
| kA3-s650 | 7196 | 6.83 | 140 | 10 | 213 | 2794 | 49148 | **0.057** |

**Three key numbers:**
1. **mean `|vx|` = 6.1–7.2** — the player **is** moving, so §208's "stationary" was wrong;
2. **median constant-direction run = 1–72 ticks**, mostly **1–33** — the player reverses every **0.02–0.55 s**;
3. **net/gross = 0.006–0.164**, mostly **0.03–0.08** — **92–99 % of all horizontal travel cancels itself out.**

**Extremes:** `kA3-s550` travelled **46512 px** and netted **282 px (0.6 %)**;
`kA3-s300` travelled **103526 px (6.5 arena lengths)** and netted **1136 px (1.1 %)**.

## 3. ★★ This hits the owner's original instruction exactly
Owner: *"as long as horizontal speed is kept the whole time"* and *"how can the player suddenly stand
still?"*

**Accurate statement: the player is never stationary, but the horizontal direction keeps reversing, so "keep
horizontal speed" is entirely unsatisfied in the sense of net displacement — the player oscillates in place
and never outruns the charge path.**

**This gives a second, more fundamental reason why every horizontal-strategy lever has been zero-sum** (§206.5
gave the DPS-sensitivity reason): **when net travel is 1–16 % of gross, "left or right" cannot matter — either
choice is cancelled by the next reversal.**

## 4. ★ Incidental: every run has a longest same-direction run of ~473 ticks
Across 14 runs the max run is **473–493** almost exactly. Such a uniform value suggests a **fixed-length
scripted segment** (e.g. the pre-takeover opening) rather than adaptive behaviour. **Not investigated this
round; logged as a low-priority lead** — it is the only long same-direction behaviour observed and may be
reusable mid-fight.

## 5. ★★ Capability vs. usage — the most actionable conclusion
p90 run length reaches **98–462 ticks** and max reaches **443–493 ticks**, i.e. the controller **is able** to
hold a direction for 8–30 seconds. **It simply chooses not to: the median is 1–72 ticks.**

**⇒ This is not a capability limit but a strategy choice — and unlike a per-DPS parameter it is a
DPS-independent behavioural constraint** (§206.5 showed per-DPS parameters must be zero-sum because a good
trajectory at one DPS vanishes at another; "hold a direction" does not have that property).

**That makes it the highest-priority and best-behaved candidate now that §208 is void.**

## 6. Next step
Find where the controller decides to reverse (the Fishron wing's horizontal emission for the charge/standoff
phases), determine whether the reversal is driven by the boss axis, the arena bounds, or a standoff band, and
test a **directional commitment** (hysteresis on the horizontal sign) behind a default-off env gate with the
usual byte-identical regression check on the default path.

## 7. Acceptance master table (native tick-replay only)

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
horizontal speed *after a dash*; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–82 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4. Contact-rate threshold as a criterion (ranges overlap).
Altitude as the explanation for low contact rates (§206.4). Cornering/arena walls (§207.1). Hit clustering
(§207.2). Extending the horizon (§207.5). **A sudden-stop / controller-engine horizontal divergence — it does
not exist (§209.1).**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — 480**; "the fork is invisible in
observables" **FALSE**; "`ai[3]` ≡ accumulated dash count" **FALSE**; "the strong wing has only two holes"
**FALSE — five**; "the contact-rate floor is 0.313" **FALSE — 0.174**; "hits cluster near the arena walls"
**FALSE**; "`-maxticks` can be raised past 24000" **FALSE**; "stalls happen on the ground/platforms"
**FALSE**; **"there is a controller/engine horizontal divergence" FALSE — retracted (§209.1)**.

## Method notes

* **A threshold metric like `|vx| < 0.5` is contaminated by legitimate zero crossings.** Test *transitions*
  (`|vx_prev| ≥ 3 → |vx| < 0.5`) before claiming a control failure. This error cost a full round.
* **Exclude post-kill rows before computing fight statistics** — `kJ4-s2000`'s "final decile" was 288 rows of
  the boss already being dead, which made a zero-hit winner look like it stopped moving.
* **Filter to plan-carrying rows** — pre-takeover rows have no `plan` and skew every rate.
* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **`-maxticks` must be in 600..24000.** `-WallSeconds` caps at **900**.
* **Env-gate any behaviour change and verify the default path is byte-identical before reading an A/B.**
* Sorting a tuple with a bool first mislabels groups — sort by an explicit key.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000/100 s) — a timeout there is arithmetic.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
