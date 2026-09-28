# Round 170 handoff

## HEAD
Pushed, working tree clean. Tests **749 pass / 9 fail** (the long-standing accepted set).
**No source changes shipped** — measured, refuted, and reverted. `git diff <round-168 commit> HEAD -- src/ tools/`
is empty, and strong 625 still reproduces `7689/4/3480` byte-for-byte.

## HEADLINE: ★ the real constraint, derived — the contact budget is per charge cycle, and it is **one contact in seventy**

This round produced the explanation for why **17 consecutive mechanism changes have all measured zero-sum**.

Dividing contacts by charge cycles (each charge is **28 ticks**, §167) over the strong grid:

| dps | ticks | hits | cycles | **hits / 100 cycles** | result |
|---|---|---|---|---|---|
| 300 | 15938 | 6 | 569 | 1.1 | KILL |
| 450 | 10937 | 4 | 391 | **1.0** | KILL |
| 675 | 7472 | 2 | 267 | 0.7 | KILL |
| **725** | 6997 | **2** | 250 | **0.8** | **KILL** |
| **1050** | 4997 | 3 | 178 | **1.7** | **KILL** |
| **1100** | 4409 | 4 | 157 | **2.5** | **DIED** |
| **1125** | 4461 | 4 | 159 | **2.5** | **DIED** |
| **1400** | 3884 | 1 | 139 | **0.7** | **KILL** |
| 2000 | 2881 | 0 | 103 | 0.0 | KILL |

**A fight contains only 150–570 charge cycles and the budget is 3 contacts.** Surviving means holding the
density near 1 per 100 cycles. The low-DPS kills do exactly that (1.0–1.1); the mid/high failures
(700/1100/1125) are **the dirtiest points in the whole table** (1.9–2.5).

**The threshold is not even monotone** — 1050 survives at 1.7 while 725 survives at 0.8. So the margin is
as much luck as design. Turning a 4-hit death into a 3-hit kill means removing **exactly one contact out
of ~275** — a tolerance of **one charge in seventy**. Any trajectory rewrite at that scale is noise that
can as easily *add* a contact as remove one. **This is why every nudge fails.**

## The round's measurements: three triggers, all zero-sum or negative

**★ The law, holding across every trigger**: the escape **only ever saves runs that were already dying,
and only ever breaks runs that were already surviving.**

| dps | baseline | unbounded | life 20% | life 35% | boss 40% | boss 25% | boss 15% |
|---|---|---|---|---|---|---|---|
| 300 | KILL 15938/6 | died 11281/5 | **KILL 15938/6** | **KILL 15938/6** | died 13223/6 | KILL 16130/**4** | died 14730/7 |
| 400 | KILL 12240/5 | died 11167/7 | **KILL 12240/5** | **KILL 12240/5** | KILL 12208/6 | KILL 12240/5 | KILL 12240/5 |
| 450 | KILL 10937/4 | died 10538/6 | **KILL 10937/4** | **KILL 10937/4** | died 10382/5 | died 10382/5 | died 10382/5 |
| 475 | died 20233 | **KILL 10390/1** | died 7930/4 | died 7930/4 | died | died | died |
| 600 | died 5096 | **KILL 8341/2** | died 7830/4 | died 6537/4 | **KILL 8341/2** | died 7830/4 | — |
| 625 | died 3480 | **KILL 8026/2** | died 7689/4 | died 7689/4 | **KILL 8026/2** | **KILL 8026/2** | — |
| 700 | died 14721 | **KILL 7224/4** | died 5965/4 | died 5965/4 | **KILL 7224/4** | **KILL 7224/4** | — |

**Net**: unbounded **+4/−3**; life 20% **+0/−0**; life 35% **+0/−0**; boss 40% **+3/−2**;
boss 25% **+2/−1**; boss 15% **−2**.

Best configs are net **+1**, but they *trade one low-DPS kill for two or three mid-range kills*, and are
**chaotically sensitive to their own value** (40 vs 25 vs 15) — the same defect that already refuted
`CHAITE_DASH_DELAY_DPS`.

## ★ The reusable foundation (do not throw this away)

**The player-life gate at 20–35% leaves every baseline kill byte-identical** — strong 300 at *exactly*
15938/6, 400 at *exactly* 12240/5, 450 at *exactly* 10937/4. It correctly **confines the disturbance to
the already-lost stretch of the fight**. It is simply **too weak to convert a death into a kill**, for the
one-in-seventy reason above.

## Next round — stronger intervention, not a smarter gate

Given one contact decides the fight, and horizontal nudges are noise-scale:

1. **Keep the player-life gate (20–35%)** as a proven side-effect-free isolation mechanism.
2. **Attack the contact budget itself, not the trajectory:**
   * **Lower the damage of each contact** — note the standing refutation is about *swapping armor*, which
     is **not** the same as *reducing damage on the frame you are hit* (shield / i-frame / damage-reduction
     timing). This is the untried form.
   * **Spread 3 contacts' damage over 4 contacts** (same total, lower per hit, avoiding a single lethal hit).
3. **Weak wing (6/28) needs a separate diagnosis, not more tornado work.** At DPS 300 weak wing dies at
   **5839 ticks on 8 contacts** where strong wing **kills at 15938 on 6**. Its deficit is **endurance (2.7×)**,
   not a near miss. Look first at `wingTimeMax` **130 vs 180** with climb peak **−9.91 vs −16.52**.

Acceptance criteria unchanged: strong failing points → contacts ≤3; strong → 28/28; weak 6/28 → up.

## Do NOT re-attempt

Tornado escape with **any** trigger — tornado presence (169), player life (170), or boss life (170), and
not with a latch either. Plus: pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_APEX_REFILL`
default; `_refillGuardBudget` > 0; charge-normal perpendicular dash; i-frames as a coverage lever;
standoff ceiling; `CHAITE_CASCADE_*`; bubble-break; "reconverge after the fork"; defense tier as a
survival lever; any "raise your own altitude" strategy; DPS-constant gates.

## Standing constants

* **28 ticks per charge**; hover 30 or 40; dominant period 58 ticks (constant across DPS).
* Contact box: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Tornadoes: 384 Sharknado / 386 Cthulhunado, `aiStyle 64`, **up to 50 live**, box **225×63**, forms a
  **~150 px thick × ~900 px tall wall** with `vx == 0`.
* **Strong obsidian 19/28**; **weak 6/28**. Strong 1200 and 2000 are **zero-hit kills**; weak has none.
* Damage census: **≈half of contacts = 386**, a few = 384, **≈1/3 = boss body**.
* Boss phase thresholds `lifeMax = 78000` → **39000 / 11700**; strong phase-3 entry lands at fatal ticks.
* Scripts: `tmp/fullgrid-te.ps1`, `tmp/lifegate170.ps1`, `tmp/bosslife170.ps1`, `tmp/lockscan.py`,
  `tmp/divergence.py`, `tmp/reconverge.py`.
