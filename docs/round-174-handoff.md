# Round 174 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail**. Build clean.
**No source change this round** — source delta vs round-168 is still only the round-171 `tools/GameProbe.cs`
probe fix.

## HEADLINE: the weak wing's deficit is its refill **cycle shape**, not budget or missed refills

Rounds 173–174 together answer the question round 173 posed ("which branch suppresses vertical input while
`wingTime` is high?"). The answer is **none** — the weak wing is not being suppressed; it is running a
**structurally different vertical cycle**.

### 1. The weak wing does **not** miss apex refills

Counting `wingTime` increases before the first hit (tick ≤ 1360):

| | weak | strong |
|---|---|---|
| `wingTime` increases | **20** | **31** |
| …at **apex** | **20 (100%)** | **31 (100%)** |
| …on **landing** | **0** | **0** |

Both wings refill **exclusively at apex**. This is not a defect — it is the mechanism:
`Player.cs:26992` requires `velocity.Y == 0f && releaseJump`, and with wings equipped an apex is the only
reliable place that occurs. **The "missed refill" hypothesis is refuted.**

But the weak wing gets only **20** refills to the strong wing's **31** — a **55% shortfall**, worse than the
`wingTimeMax` ratio of 130/180 = 72%. So the problem is **cycle frequency**, not budget size.

### 2. ★★ The two wings run **two differently-shaped cycles** (the key finding)

Same window (ticks 1150–1400), frame by frame:

**WEAK** — `wingTime` walks 130 → 115 → 100 → 95 → 80 → 65 → 50 → 45 → 30 → 15 → 2 → **0**,
with vertical speed pinned between −6 and −9.91.
**One monotonic burn of the whole bar**, dry at **tick 1335** — **32 ticks before the hit at tick 1367**
(which is the weak wing's first hit in *every* run).

**STRONG** — falls to terminal velocity **+10.01** → takes a **full 180 refill at its apex (tick 1260)** →
converts the whole 180 into a peak climb marching **+3.60 → −2.92 → −6.75 → −8.63 → −10.50 → −12.38 →
−14.25 → −16.12 → −16.52**.

**The strong wing trades a large altitude oscillation (~1446 px, 6056 → 4610) for frequent full refills and
peak climb. The weak wing climbs continuously and gently, so its budget only ever decreases — it never
converts budget into peak climb because it never stops to refill.**

This is why §173.6 measured weak at 5.4% of frames near peak climb vs strong's 23.1%, despite the weak wing
holding *more* average `wingTime` (77.5 vs 86.1 — and it is at zero far *less* often, 16.0% vs 32.5%).

## 3. But adding a refill is refuted — including on the weak wing (first time measured)

`CHAITE_APEX_REFILL` (`FishronWingScript.cs:1764`) was already refuted as a *default*, but had **never been
measured on the weak wing**. Round 174 measured it:

| dps | baseline | `CHAITE_APEX_REFILL=1` | |
|---|---|---|---|
| 300 | DIED 5839/8/51366 | DIED 7804/8/41553 | survives longer |
| 400 | DIED 5839/8/42536 | DIED 8185/7/26885 | survives longer |
| 450 | DIED 7487/**9**/25719 | DIED 6901/**6**/30073 | **3 fewer hits** |
| **575** | **KILL** 5426/6 | **DIED** 7076/6/15232 | **LOST** |
| **800** | DIED 3875/7/33502 | **KILL** 6366/5 | **GAINED** |
| **900** | **KILL** 5726/5 | **DIED** 4364/5/20574 | **LOST** |
| **1000** | **KILL** 5210/5 | **DIED** 5008/5/3436 | **LOST** |
| **1075** | **KILL** 4885/2 | **DIED** 4227/5/11880 | **LOST** |
| **1150** | **KILL** 4598/2 | **DIED** 3763/4/16101 | **LOST** |
| 1200 | KILL 4432/2 | KILL 4434/5 | |
| 2000 | KILL 2880/2 | KILL 2879/2 | |

**+1 / −5 = net −4.** Same direction as the strong-wing result. It genuinely raises low-band survivability
(and cuts 450's hits from 9 to 6) but **destroys the mid/high-band kills**.

This is **round 173's chaotic-timing finding seen from the other side**: any change to *when* the budget is
converted into climb saves some points and destroys others, for a negative net.

## Acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| strong wing | **19/28** | 6/7 (sample) |
| weak wing | **6/28** | **8/28** |
| strict (`-maxticks 6000`, `hits == 0`) | strong 1200 & 2000; weak 2000 | — |

**Not met.** Goal stays **active**.

## ★ The "move the strong wing's cycle earlier" idea was **refuted by its own caveat check**

I proposed moving the strong wing's fall→refill→peak-climb cycle earlier for the weak wing, and flagged one
caveat to verify first: *are the strong wing's terminal-velocity stretches deliberate, or incidental?*

**They are incidental — the premise fails.** Frames at terminal fall speed (+10.01) at dps 300:

| | terminal frames / total | share |
|---|---|---|
| strong | **6211 / 15939** | **39.0%** |
| weak | **940 / 5840** | **16.1%** |

Strong's terminal frames **by phase**:

| frames | phase |
|---|---|
| **2770** | **`fishron-wing-refill`** |
| 810 | `fishron-wing-bubble-line` |
| 570 | `fishron-wing-charge-horizontal` |
| 535 | `fishron-wing-charge-ascend` |
| 459 | `fishron-wing-tornado-clear` |
| 409 | `fishron-wing-sharknado-exit` |

And the stretches start at `wingTime` **0**: `t=1015 len=235 wt 0→0`, `t=1507 len=237 wt 0→0`.

**So the strong wing's cycling is simply the existing "budget exhausted → forced descent → refill at apex"
mechanism firing repeatedly in a long fight — not a strategy it chooses.**

### ★ The decisive consequence

**The weak wing's cycle shape is an *effect* of dying early, not a *cause* of it.** Strong gets 31 refills
to weak's 20 because its run lasts **2.7× longer** (15938 vs 5839 ticks), giving it more chances to drain
the bar and fall. The weak wing therefore **cannot** adopt the strong wing's shape — that would demand
**more cycles in less time** while climbing slower (−9.91 vs −16.52), which is **not physically available**.

## What is actually left

Cumulatively across rounds 173–174:

1. The weak wing does **not** lack dashes (rates equal).
2. It does **not** lack `wingTime` budget (mean 77.5, and `WT==0` **less** often than strong).
3. It does **not** miss apex refills (both refill 100% at apex).
4. Its cycle shape is a **result, not a cause**.
5. Adding a refill is **net negative** (+1/−5).

**The one position with measurement support that nothing has refuted**: the weak wing's binding constraint
is its **climb rate (−9.91 vs −16.52)** — an intrinsic wing property the script cannot change.

**This is not a proof of impossibility.** It means the **script-level levers are essentially exhausted**.

## Next round — gather evidence, do not sweep more parameters

Confirm whether **every** low-band (300–800) weak hit occurs in a state where climbing was **genuinely
unavailable** (`wingTime ≈ 0` and/or the escape direction's vertical component infeasible). Then either:

* a **real script error** is found (worth fixing), or
* a **quantitative boundary** is established and reported honestly to the owner — rather than spending
  further rounds on exhausted levers.

§171.3 already established that every weak death is the pool emptying with **no "failed without being hit"
case**; this round would close the loop on *whether the hits were avoidable*.

## Do NOT re-attempt (cumulative, new this round)

* **Moving the strong wing's fall→refill→peak-climb cycle earlier for the weak wing** — refuted this round:
  the strong wing's cycling is incidental (2770 of its 39% terminal-fall frames are ordinary `refill`
  phase), and the weak wing cannot fit more cycles into a shorter fight.
* `CHAITE_APEX_REFILL` on the weak wing (measured +1/−5).
* "The weak wing misses apex refills" as a hypothesis — **refuted**: both wings refill 100% at apex.
* "The weak wing lacks `wingTime` budget" — refuted: it holds *more* mean budget and is at zero *less* often.
* "The weak wing's cycle shape is the cause" — refuted: it is an effect of the shorter run.
* Round 173: weak `CHAITE_WEAK_DASH_DELAY` 0–3; **any timing knob moving only 1–2 ticks after the lock**;
  "the weak wing under-dashes".
* Round 172: per-wing single-axis escape.
* Earlier: tornado escape under any trigger; DPS-constant gates; armor tier as a survival lever;
  pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `_refillGuardBudget`; altitude raising.

## Standing constants

* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box **225×63**, **~150×900 px wall**, `vx == 0`.
* Damage census: ≈½ contacts = 386, a few = 384, **≈⅓ = boss body**.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* **Weak climb peak −9.91 vs strong −16.52**; `wingTimeMax` **130 vs 180**; terminal fall **+10.01 both**;
  dash peak **14.50 both**; horizontal cruise 7–8 both.
* **Weak-wing first hit is ALWAYS tick 1367**; strong-wing first hit is tick 5409–5868.
* **Both wings refill only at apex** (20 weak / 31 strong before tick 1360); **zero landing refills**.
* Scripts: `tmp/trace174.py`, `tmp/refills174.py`, `tmp/apexmiss174.py`, `tmp/tab174.py`,
  `tmp/weakdash173.ps1`, `tmp/wingbudget173.py`.
