# Round 156 handoff

## HEAD
`1dbc62b` — pushed. Worktree clean except untracked `tmp/`.

## HEADLINE: strong-wing pre-charge lead 20 -> 40. Strong arm down from 3 failing points to 1.

`§150` reconstructed a body hit tick by tick and showed that the lock-to-contact window is
16 ticks while the precharge window is only 19, and that the player had built just **-3.06
px/tick** of horizontal speed against a 14.12 charge by the time the line froze. The
precharge window is the only part of the circuit that can move the player off the charge line
**before it is locked**, so lengthening it was the one direction §150 left open.

### Measured, obsidian, lead 40 vs the reviewed 20

| DPS | reviewed 20 | **lead 40 (new default)** |
|---|---|---|
| 300 | 16103/8/**0 KILL** | **9697/7/32136 DEATH** |
| 600 | 8329/4/0 KILL | 8332/**3**/0 KILL |
| **700** | 6205/7/11888 death | **7221/3/0 KILL** |
| **800** | 6011/5/5015 not killed | **6382/5/0 KILL** |
| 900 | 5739/3/0 KILL | 5736/3/0 KILL |
| **1000** | 4536/4/11408 death | **5220/2/0 KILL** |
| 1100 | 4795/0/0 zero-hit | 4795/1/0 KILL |
| 1200 | 4437/0/0 zero-hit | 4437/5/0 KILL |
| 1500 | 3661/1/0 KILL | **3661/0/0 ZERO-HIT** |
| 2000 | 2881/0/0 zero-hit | 2881/0/0 zero-hit |

Strong failures went **3 → 1**, and the arm gained a zero-hit at 1500. It is a **deliberate
trade**: strong 300 goes from a kill to a death.

### Why a constant cannot serve both: the lead is BISTABLE
Every value **≥ 38** produces the *identical* run (7221/3 and 6382/5, strong 300 lost).
Every value **≤ 37** does the reverse (strong 300 killed at 15915/5, strong 700 lost).
Constants 40/44/48/56 and ratios 1200/1500/1800 all converge to the same trajectory.

### The structural discriminator was tried and REFUTED
Scaling the lead by the learned hover duration (`_hoverLimit`) is the intuitive fix. With the
gate `limit > floor` at ratio 1400:

| floor | strong 300 | strong 700 |
|---|---|---|
| 30 | 14668/8/7253 no-kill | 5248/6/23053 no-kill |
| 40/50/60/80 | 16103/8/**0 KILL** (reverted) | **6205/7/11888 — fails at every floor** |

Any floor high enough to rescue strong 300 also reverts strong 700 to the losing branch.
Kept in the tree documented, default OFF behind `CHAITE_PREJUMP_RATIO`.

## Weak arm unaffected — verified byte-identical
The weak wing has its own lead (`WeakPreJumpLead`, default 30). Its whole band reproduces the
committed default exactly (w800 = 6381/3/0, w2000 = 2881/1/0).

## Current status (obsidian, both arms, defaults)

**Strong (lead 40)**: 300 **DEATH** · 600 3 · 700 3 · 800 5 · 900 3 · 1000 2 · 1100 1 ·
1200 5 · 1300 3 · 1500 **0** · 2000 **0** — *all kills except 300*

**Weak (lead 30, unchanged)**: 300 8777/8/36712 death · 600 6130/6/22031 death ·
800 6381/3/0 KILL · 900 5729/4/0 KILL · 1000 4489/5/12159 death · 1100 4788/3/0 KILL ·
1200 3082/8/27162 death · 1300 3489/4/14076 death · 1500 3642/4/330 death ·
2000 2881/1/0 KILL

### Still unmet (honest)
- **Owner's relaxed criterion NOT met.** Failing: strong 300; weak 300/600/1000/1200/1300/1500.
- **Strict zero-hit NOT met.** Zero hits at strong 1500/2000 only (obsidian). **The weak wing
  has never produced a zero-hit.**
- Tests **749 pass / 9 fail** (pre-existing). Never claim green.

## Next direction
**Weak 300/600 are now the most stubborn pair.** Almost everything that helps the strong arm
leaves the weak arm untouched, because the weak wing climbs at ~2/3 the strong rate
(-5.08 vs -7.50 px/tick), so "take off earlier" buys proportionally less. The weak arm needs
its own arrival geometry rather than the strong arm's solution.

Concrete levers not yet tested on the weak arm:
- `CHAITE_WEAK_PREJUMP` was refuted at 30/35/45, but the strong finding shows the response is
  **bistable**, so fine values between and beyond (e.g. 52-60, and 36/37/38) deserve a sweep
  with the same discipline: the strong arm needed 38+, and weak's optimum may be far from 30.
- The weak arm's recorded 1200 death is **Sharknado(384)-dominated (7/8 hits)** and its 300
  death is **body-dominated (7/8)** — those are different mechanisms and may need different
  fixes; see `§149.2`.

## Run method
Unset `CHAITE_POLICY_FILE` **and** `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian`
(or `shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`.
Overrides: `CHAITE_STRONG_PREJUMP=<n>` (default 40), `CHAITE_WEAK_PREJUMP=<n>` (default 30),
`CHAITE_PREJUMP_RATIO=<per-mille>` (default 0 = off), `CHAITE_CASCADE_ESCAPE=0` to disable the
wall escape, `CHAITE_CASCADE_DEPTH_GATE=0` for its ungated A/B, `CHAITE_PROBE_DENSE_FRAMES=1`
before `run-native-acceptance.ps1` whenever per-tick data will be analysed.
Route + `-FormulaRoute` always together. `-MaxTicks` ∈ **600..24000**, `-WallSeconds` ∈ 15..900.
Verify by parsing `artifacts/<run>/result.json` (`ticks`/`hits`/`bossLifeRemaining`) — do not
grep stdout (PowerShell `Select-String` is case-insensitive and the probe banner is wide).
