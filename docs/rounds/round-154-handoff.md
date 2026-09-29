# Round 154 handoff

## HEAD
`edbd036` — pushed. Worktree clean except untracked `tmp/`.

## What round 154 established

### 1. The controller had NO visibility of the 386 wall — now it does
`FishronWingScript.Tick` receives `PlayerSnapshot / TargetSnapshot / ArenaSnapshot /
MobilitySnapshot / SharknadoBubbleSnapshot`. The repo-wide threat list lives on
`CombatSnapshot`, which the script never sees. So "go around the wall" (§146's conclusion)
was **impossible to implement** before this round.

`TerrariaFacade` now collects every live type-386 centre **before the threat gate**, splits
them into contiguous walls (adjacent gap > 256 px ⇒ different wall), pads each by the largest
member's half-width (112.5 px = scale 1.5 of a 150-px base), and publishes up to 4 collision
footprints on `SharknadoBubbleSnapshot` (`CascadeWallCount`, `CascadeWallLeft(i)`,
`CascadeWallRight(i)`).

**Walls must never be merged.** Strong 600 holds two walls 3380 px apart and strong 900 two
3492 px apart; the union spans most of the 5104-px arena, so the player can never be outside
it and the escape degenerates to drifting. The union version cost the strong 600 and strong
900 kills (8329/4/0 → 7117/5/12137, 5739/3/0 → 5336/4/5958).

### 2. A `ref` bug made the escape inert for a whole round
`PublishCascadeWalls(..., SharknadoBubbleSnapshot bubble)` took the **struct by value** and
threw away every wall it computed. The function returned "successfully" while writing nothing.
**Passing by `ref` makes the rule live.** Do not remove that `ref`.

### 3. The escape works — and is incompatible with the low band
With `CHAITE_CASCADE_ESCAPE=1`:

| point | baseline | escape ON |
|---|---|---|
| strong 800 | 6011/**5**/5015 | **6388/1/0 KILL** |
| strong 1000 | 4536/4/11408 | **5217/3/0 KILL** |
| strong 1500 | 3661/1/0 | **3661/0/0 zero-hit** |
| weak 600 | 6130/6 | **8310/6/0 KILL** |
| weak 900 | 4439/5 | **5729/3/0 KILL** |
| weak 1500 | 3362/4/7466 | **3658/4/0 KILL** |
| **strong 300** | **16090/8/0 KILL** | **10202/7/29583 DEATH** |
| strong 600 | 8329/4/0 KILL | 6908/5/14241 DEATH |
| strong 1200 | 4437/**0/0 zero-hit** | 3940/3/9997 |

Re-confirmed at the 24000-tick cap: 300 DPS is 16090/8/0 OFF vs 10202 DEATH ON.

**The harm scales with fight length.** At low DPS the fight is long (16090 ticks) and the
accumulated cost of yielding the horizontal axis outweighs the gain; at high DPS the fight is
short (2881–6388) and it ends before the cost accrues.

⇒ **Default is OFF.** Committed circuit is byte-identical: strong 300 = 16090/8/0,
strong 600 = 8329/4/0, strong 1000 = 4536/4/11408, weak 800 = 6381/3/0.

## Methodology trap that cost real time this round

I first censused "how many ticks is the player inside a wall" on the **g9-*** runs and got
12–19, then wrote an explanation for the inertness around it. That census was **invalid**: the
g9 runs did not set `CHAITE_PROBE_DENSE_FRAMES=1`, so `boss-observations.jsonl` had only
**227 rows** — transition frames, not ticks.

On **dense** runs (`mid2-*`, 4537 rows):

| run | ticks inside a wall | longest unbroken stretch |
|---|---|---|
| mid2-s1000 | **510** | **t 4040..4536 = 497 ticks, straight to death** |
| mid2-s800 | 602 | t 5589..6011 = 423 |
| mid2-s600 | 335 | max 120 |
| mid2-s900 | 243 | t 3798..3968 = 171 |

**Always check that row count ≈ tick span before trusting any per-tick statistic.**

## The concrete trap for the next attempt
At **strong 1000, t 4040–4536**, the player is inside a wall **continuously for 497 ticks**
while the plan holds `horizontal = -1` and the wall's right edge is ~20–60 px away. The escape
fixes exactly this (and strong 800/1000/1500 all improve). What it breaks is the low band.
The open question is a **structural** condition separating those two regimes. It is **not** a
DPS constant — DPS-constant gating has been refuted four times (rounds 152–153).

## Committed results (obsidian = honest tier)
**Strong**: 300 **16090/8/0 KILL** · 600 **8329/4/0 KILL** · 700 6205/7/11888 ·
800 6011/5/5015 · 900 **5739/3/0 KILL** · 1000 4536/4/11408 · 1100 **0 hits** ·
1200 **0 hits** · 1500 3661/1/0 KILL · 2000 **0 hits**

**Weak**: 300 8777/8/36712 · 600 6130/6 · 800 **6381/3/0 KILL** · 900 4439/5 ·
1000 4395/5/13670 · 1100 **4788/3/0 KILL** · 1200 3082/8 · 1300 3489/4 ·
1500 3362/4/7466 · 2000 2881/1/0 KILL

## Still unmet (honest)
- **Owner's relaxed criterion NOT met.** Default: strong fails 700/1000; weak passes only
  800/1100/2000.
- **Strict zero-hit NOT met.** Zero hits only at strong 1100/1200/2000. **Weak wing has never
  recorded a zero-hit result.**
- Tests **749 pass / 9 fail** (pre-existing). Never claim the suite is green.

## Run method
Unset `CHAITE_POLICY_FILE` **and** `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian`
(or `shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`; `CHAITE_CASCADE_ESCAPE=1`
to arm the wall escape; `CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1`
whenever per-tick data will be analysed. Route + `-FormulaRoute` always together.
`-MaxTicks` ∈ **600..24000**, `-WallSeconds` ∈ 15..900. Verify by parsing
`artifacts/<run>/result.json` (`ticks`/`hits`/`bossLifeRemaining`).
