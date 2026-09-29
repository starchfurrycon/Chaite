# Round 173 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail**. Build clean.
Source delta vs the round-168 commit: **only the round-171 `tools/GameProbe.cs` probe-report fix**.
**No source change this round.**

## HEADLINE 1: the weak wing does **not** under-dash — premise refuted

Acting on round 172's one open lever (scan dash timing independently for the weak wing), the first result
kills the premise. **Dash initiations per 1000 ticks** (`shield-events.jsonl` → `started`):

| dps | weak | strong |
|---|---|---|
| 300 | 10.8 | 10.3 |
| 600 | 10.0 | 9.3 |
| 800 | 9.3 | 9.1 |
| 900 | 9.3 | 9.2 |
| 2000 | **7.6** | **7.6** (both: 22 dashes / 2880 ticks) |

The two wings dash at **essentially the same rate** — identical at dps 2000. The weak wing's lower
*absolute* count is only because **it dies sooner**. Dash availability is **not** the bottleneck.

## HEADLINE 2: the weak dash-delay parameter is **chaotic to a single tick**

The knob accepts 0–120 but had only ever been swept at 4/7/10/12/14/16/20 — **0–3 was untested**.

| dps | baseline (4) | **delay 0** | **delay 1** |
|---|---|---|---|
| 300 | DIED 5839/8/51366 | DIED 8777/8/36712 | **DIED 4547/7/57924** |
| 400 | DIED 5839/8/42536 | DIED 7244/9/33228 | **DIED 4547/7/51248** |
| 450 | DIED 7487/9/25719 | DIED 6072/9/36432 | **DIED 4547/7/47909** |
| 550 | DIED 6948/7/19110 | DIED 5947/6/28381 | DIED 6169/8/26351 |
| 575 | KILL 5426/6 | KILL 8657/6 | **DIED 4752/8/37599** |
| **800** | DIED 3875/7/33502 | **KILL 6381/3** | — |
| **1000** | **KILL** 5210/5 | **DIED** 4489/5/12159 | — |
| **1075** | **KILL** 4885/2 | **DIED** 3875/6/18151 | — |
| **1150** | **KILL** 4598/2 | **DIED** 3966/4/12303 | — |
| **1200** | **KILL** 4432/2 | **DIED** 3082/8/27162 | — |

**Delay 0: +1 / −4 = net −3. Delay 1: +0 / −1.**

### ★★ And the structural finding, which matters more than the numbers

Moving the delay by **one tick** (0 → 1) collapses dps 300/400/450 onto **exactly identical results**
(4547 ticks, 7 hits each) where at delay 0 they were 8777/8, 7244/9, 6072/9.

**The parameter's response is not smooth — it is chaotic.**

**Generalised rule (new, and more valuable than the knob):** any knob that only moves timing by one or two
ticks after the lock **cannot be a solution**, because its response is chaotic and no optimum generalises.
This is the round-170 contact budget (1 contact in ~70) restated as a design constraint.

## ★ Side diagnostic: the weak wing's real early-fight deficit

Before its first hit (frames ≤ tick 1300):

| metric | weak | strong |
|---|---|---|
| mean `wingTime` | **77.5** | 86.1 |
| `wingTime == 0` frames | 16.0% | **32.5%** |
| min vertical speed | **−9.91** | −16.52 |
| frames at ≤ −9 | **5.4%** | **23.1%** |

**The strong wing exhausts its wings MORE often (32.5% vs 16.0%) yet reaches peak climb four times as
often (23.1% vs 5.4%).** The weak wing is **not out of budget — it rarely converts budget into climb.**

At **tick 1367 — the weak wing's first hit in *every single run*** — the weak wing has `wingTime = 0` and
vertical velocity of only ±2, while the strong wing has `wingTime 70–100` and climbs steadily at −16.52,
gaining ~944 px in 60 ticks. The strong wing's first hit is at tick **5409–5868**; the weak wing's is
always **1367**.

## Acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| strong wing | **19/28** | 6/7 (sample) |
| weak wing | **6/28** | **8/28** |
| strict (`-maxticks 6000`, `hits == 0`) | strong 1200 & 2000; weak 2000 | — |

**Not met.** Goal stays **active**.

## Next round

The weak wing's actionable deficit is now precise and single: **it holds ~77.5 wingTime before the first
hit but reaches peak climb on only 5.4% of frames.** That is a *conversion* problem, not a budget problem.

**Do NOT** simply "climb more": §166 refuted raising altitude as a strategy (the hover follows the player).
The open question is therefore **when and why the weak wing declines to convert budget into climb**, i.e.
which branch suppresses vertical input while `wingTime` is high. That is a code-reading question first,
and it is the next concrete step.

## Do NOT re-attempt (new this round)

* Weak `CHAITE_WEAK_DASH_DELAY` in 0–3 (measured: 0 → net −3, 1 → net −1).
* **Any timing knob that moves only 1–2 ticks after the lock** — chaotic response, no generalisable optimum.
* "The weak wing under-dashes" as a hypothesis — refuted (rates are equal).
* Previously: per-wing single-axis escape (172); tornado escape under any trigger (169/170); DPS-constant
  gates; armor tier as a survival lever (171); pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`;
  `CHAITE_APEX_REFILL` default; `_refillGuardBudget`; altitude raising.

## Standing constants

* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box **225×63**, **~150×900 px wall**, `vx == 0`.
* Damage census: ≈½ contacts = 386, a few = 384, **≈⅓ = boss body**.
* Player pool **480** (400 + 80) — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* Weak climb peak **−9.91** vs strong **−16.52**; `wingTimeMax` **130 vs 180**; dash peak **14.50** both.
* Weak-wing first hit is **always tick 1367**; strong-wing first hit is tick 5409–5868.
* Scripts: `tmp/weakdash173.ps1`, `tmp/wingbudget173.py`, `tmp/weakhoriz172.ps1`, `tmp/weakshroom171.ps1`.
