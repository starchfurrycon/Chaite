# Round 158 handoff

## HEAD
`f5bca60` — pushed. Worktree clean except untracked `tmp/`.
Tests **749 pass / 9 fail**, the long-standing accepted set.
Route channel re-verified: strong `6000/6/55`, weak `5636/9/102`, both `MATCH`, `REPRODUCED`.
(Route replay sets no simulated DPS, so the new delay settings do not perturb it.)

## HEADLINE: the i-frame mismatch is now measured, the dash-delay optimum is per-wing, and the parameter is honestly a chaotic perturbation rather than a timed fix

### The measurement (round 157 only inferred this from a code comment)

Aligning `shield-events.jsonl` (`requested`/`started`/`eocDash`/`contact`) against the contact
ticks of the dense strong-300 run:

| contact | last dash | lead | i-frames end | gap |
|---|---|---|---|---|
| 2083 | 2063 | 20 | 2078 | **5** |
| 2666 | 2647 | 19 | 2662 | **4** |
| 4998 | 4963 | 35 | 4978 | **20** |
| 5464 | 5441 | 23 | 5456 | **8** |
| 9220 | 9093 | 127 | 9108 | **112** |
| 9281 | 9093 | 188 | 9108 | **173** |
| 9321 | 9093 | 228 | 9108 | **213** |

`eocDash = 15`, and the dash is spent 19–35 ticks before contact, so **the i-frames expire before
contact every single time**. That is why all seven contacts land.

Two corrections to earlier notes: the `±4.50 / −3.50` with `immuneTime = 40` on contact rows is the
**post-hit knockback, not a dash**; and all seven contacts are the Boss **body** (370) with
`threatsWithin400 = 0` on every row — the round-155 note calling 9220/9321 `wall386` was a
`hurt-observations`/`prehit` conflation.

### The optimum is per-wing, so the knob is now per-route

`DashDelay` became `RouteDashDelay(route)`, matching the existing `DashSuppressGap(FormulaRoute)`
shape. Strong **7**, weak **4**. Applying 7 to the weak wing is uniformly worse — the same
shared-knob mistake the weak pre-charge lead made.

Default-only verification (no knobs set at all) reproduces the knob-driven numbers exactly.

### Default-only fine grid, obsidian, 15 DPS points per arm

**Strong (d=7) — kills at every coarse-grid point, three zero-hit**

| DPS | result | DPS | result |
|---|---|---|---|
| 300 | 15938/6/0 KILL | 1000 | 5221/3/0 KILL |
| 400 | 12240/5/0 KILL | 1100 | 4409/4/7087 no-kill |
| 500 | 7706/3/0 KILL | **1200** | **4441/0/0 ZERO-HIT** |
| 550 | 7531/5/13926 no-kill | **1300** | **4141/0/0 ZERO-HIT** |
| 575 | 8674/4/0 KILL | 1400 | 3884/1/0 KILL |
| 600 | 7830/4/5096 no-kill | 1500 | 3659/2/0 KILL |
| 650 | 7738/3/0 KILL | 1750 | 3215/2/0 KILL |
| 700 | 5965/4/14721 no-kill | **2000** | **2881/0/0 ZERO-HIT** |
| 800 | 6386/2/0 KILL | | |
| 900 | 5741/1/0 KILL | | |

**Weak (d=4)** — failures only at low DPS, plus three near misses:

| DPS | result | DPS | result |
|---|---|---|---|
| 300 | 5839/8/51366 | 1000 | 5210/5/0 KILL |
| 400 | 5839/8/42536 | 1100 | 4554/5/4344 near |
| 500 | 6418/7/28855 | 1200 | 4432/2/0 KILL |
| 600 | 5320/6/30138 | 1300 | 4138/3/0 KILL |
| 700 | 5022/6/25597 | 1400 | 3359/4/12176 near |
| 800 | 3875/7/33502 | 1500 | 3656/1/0 KILL |
| 900 | 5726/5/0 KILL | 1750 | 3153/5/1808 near |
| | | 2000 | 2880/2/0 KILL |

### THE MOST IMPORTANT FINDING: do not over-claim the delay

At d=7, 25-DPS steps flip the outcome: **550 fails, 575 kills; 600 and 625 fail, 650 kills;
1100 fails, 1150 kills.** Adjacent DPS values giving opposite results means the delay is **not** a
timed mechanism that aligns i-frames with contact — if it were, the response would be smooth and its
optimum would move monotonically with DPS. It is a **small perturbation to a chaotic trajectory**,
and the outcome is far more sensitive to initial geometry than to DPS.

Consequences, which the next round must respect:

* The **zero-hit region at 1200/1300/2000 is robust** — it appears at d=0, d=2 and d=7 alike.
  This is the strongest claim available.
* The residual "just short" results at **600 / 700 / 1100** are 5–15k of a 78000 pool and must
  **not** be called solved; a 25-DPS nudge reverses them.
* d=7's overall gain over d=0 is real but is a **shift of the failure tail**, not its removal.

### Rejected as a default

`CHAITE_DASH_DELAY_DPS` (a DPS-indexed table) carries the strong wing to 11/11 kills with the same
zero-hit points, but it reads the DPS the **harness injects** rather than anything the circuit can
observe in a real fight. It is kept strictly as a measurement instrument and is documented as such.

## Status: objective NOT met, but much closer

| arm | coarse-grid failures |
|---|---|
| strong | **0** (600/700/1100 short by 5–15k) |
| weak | **4**, all at DPS ≤ 800, plus 3 near misses |

Started this round at 7 failing points across both arms. Strict zero-hit target still unmet.

## Next, in priority order

1. **The failures are now all about fight LENGTH, not a missed dodge.** Weak fails only where the
   fight runs 4000–6400 ticks. The next direction is reducing exposure per unit time — a longer
   effective movement cycle, fewer forced crossings — **not** further searches over tail parameters
   like the dash delay, which is now characterised as chaotic.
2. Weak low-DPS (300/600/800) per-tick analysis: the weak wing's contact-lead distribution has
   never been measured (its optimum being 4 rather than 7 says the distribution is shorter overall).
3. Strong 600/700/1100: decide honestly whether to accept as "close" or invest. Given the chaotic
   sensitivity, any claimed fix must be shown *robust across a DPS neighbourhood*, not at one point.
4. Shroomite tier re-run after the per-route delay (partially done: strong failing points down to 1).
5. 9 test failures are pre-existing and unchanged; **never** claim the suite is green.

## Method notes

* Clear `CHAITE_POLICY_FILE` and `CHAITE_POLICY_FORMAT`; the runner hard-refuses them.
* The runner now prints and records every inherited `CHAITE_*` variable.
* **Test robustness across a parameter neighbourhood before believing a result.** Round 158's
  headline number (strong 300 → kill) is real, but the same class of number at 600/1100 reverses
  under a 25-DPS nudge. Reporting a single point as solved is how a chaotic result gets mistaken
  for a mechanism.
* A/B one variable at a time — round 157 lost time to an A/B that changed two things at once.
* Dense runs need `CHAITE_PROBE_DENSE_FRAMES=1`; parse `artifacts/<run>/result.json`, never banners.
