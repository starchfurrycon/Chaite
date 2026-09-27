# Round 149-150 handoff

## HEAD
`77eb22d` — all pushed. Worktree clean except untracked `tmp/`.

## Round 150 additions

### The column is a self-cloning CASCADE, not one box
At each late hit there are **~12 type-386 projectiles within 250 px**, nearest 36 px, widths cascading
175, 168, 161, ... 63. `aiStyle 64` re-spawns itself at a decremented `ai[1]`, which drives `scale`. All
hostile, all 50 damage, each alive the full 840-tick `timeLeft`.

### Corrected diagnostic
Round 149's geometry script measured `dx = player.X - col[0].x`; `col[0]` was an arbitrary cascade
fragment, not the column. Real geometry: player x **2474**, column spawn x **2476** — the player stands at
the spawn point, and the gate reads **759, 771, 731 px** against its 760 boundary. So the gate is sized
~right; the player missed clearance by 1–11 px.

### The decoupling is implemented and the gate PASSED
Three separate knobs now: `CHAITE_TORNADO_MEMORY` (recall, 540), `CHAITE_TORNADO_RESPONSE` (escape +
jump suppression, 540), `CHAITE_TORNADO_RECALL` (gate radius, 760).
```
strong 300  memory 540 / response 540   10004 / 8 / 30628   baseline
strong 300  memory 881 / response 540   10004 / 8 / 30628   BYTE-IDENTICAL  <- gate passed
strong 300  memory 881 / response 700    6969 / 8 / 45782   collapse
```
**The RESPONSE window is load-bearing; the memory horizon is inert.** This re-attributes round 149's
"memory sweep": it was a *response* sweep, because one counter carried both meanings. Conclusion unchanged
(540 optimal), mechanism now correct.

### Also confirmed
- `CHAITE_SIM_BUBBLE_BREAK` at 1.0 / 0.5 / 0.0 → **byte-identical**, so bubble breaking is not simulated.
  Quantitatively confirms the owner's "bubbles are nearly no threat".
- The colocation-lift **wing-budget gate was already tried and refuted** (§ in-file: strong 300 8812→7340,
  strong 600 lost its kill). Do not re-attempt.
- `CHAITE_REFILL_GUARD` default 0 is already at its optimum (§104.2).

### Damage split, strong 300 (892 total)
- 524 before tick 9125 — 5 hits, **no tornado present**
- 368 inside the tornado window — 3 hits
So the tornado owns **3 of 8** hits, not 4 as round 135.5 estimated from correlation.

## THE TORNADO HAZARD IS CLOSED
`penetrate = -1` (indestructible), self-cloning cascade, outlives memory by 341 ticks, and both 760 and 540
sit at sharp optima. Every lever measured: box (loose+tight) inert, wall-pin inert (removed), radius
1200/1600/2200/3000 → 7867/4679/3658/3658, response 560..1600 → all 6969/8/45782, memory 881 inert.

## Verified invariants (re-check every round)
```
strong 300 10004/8/30628   strong 600 6744/6/15937   strong 800 5057/4/17755
strong 1000 4573/3/10777   strong 1200 4437/1/KILL   strong 1500 3661/1/KILL
strong 2000 2881/0/ZERO-HIT KILL
weak 300 5879/8/51124      weak 600 4741/6/35818     weak 800 6380/5/KILL
weak 1500 3656/4/KILL      weak 2000 2881/1/KILL
```
Routes both MATCH (strong 6000/6/False, weak 5636/9/True). Default and `MEMORY=881 RESPONSE=540`
both reproduce byte-identically.

## Run method
Unset `CHAITE_POLICY_FILE` and `CHAITE_POLICY_FORMAT`. Then `CHAITE_ARMOR_TIER=obsidian`,
`CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`, `CHAITE_SIM_DPS_ZERO_TILES=401`,
`CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`. `-WallSeconds` must be 15..900.
Route + `-FormulaRoute` always together.

## Traps
- `verify-fishron-routes.ps1` now clears the full env (fixed r149) — keep it that way.
- Parse `result.json` for `bossLifeRemaining`; the printed header is capital-B `Boss life left`, so a
  lowercase PowerShell `-match` parse silently yields empty and makes every row look like `DIFF`.
- An identical death tick across different DPS/params = broken invariant.
- Tests: 749 pass / 9 fail, PRE-EXISTING. Never claim green.
- **Do not re-try**: colocation-lift wing gate, refill guard > 0, soft standoff, pre-charge-only standoff,
  platform rows 0/1, exact-perpendicular escape, `CHAITE_DASH_DELAY` 2-8, global `CHAITE_DASH_SUPPRESS`,
  weak prejump lead 30/35/45, any tornado knob.


## The big measured result this round
Tornado ablation via `CHAITE_SIM_RETIRE_PROJECTILE` (with a byte-identical null control):

| config | default | retire 386 (tornado) |
|---|---|---|
| strong 300 | 10004 / 8 / 30628 dies | **16077 / 9 / 0 ACCEPTED KILL** |
| weak 300 | 5879 / 8 / 51124 dies | **5879 / 8 / 51124 byte-identical** |
| strong 600 | 6744 / 6 / 15937 dies | 8333 / 4 / 0 KILL |
| weak 800 | 6380 / 5 / KILL | 6367 / 4 / KILL |

Two conclusions:
1. The column is the **dominant** hazard for the strong wing: removing it flips strong 300 from death to
   accepted kill. Retiring the tornado is diagnostic only (not a legitimate change) but proves the damage
   is real and sufficient.
2. The weak wing is **completely tornado-independent** — byte-identical. The two wings fail through
   different hazards. This is the measured basis for the owner's "two state machines" requirement.

## Every tornado-family lever is refuted
```
lever                          strong 300
default (radius 760, mem 540)  10004 / 8 / 30628
CHAITE_TORNADO_BOX=1 (loose)   10004 / 8 / 30628  byte-identical, never fires
CHAITE_TORNADO_BOX=1 (tight)   10004 / 8 / 30628  byte-identical, never fires
radius 1200 / 1600 / 2200 / 3000   7867 / 4679 / 3658 / 3658
CHAITE_TORNADO_WALLPIN=1       10004 / 8 / 30628  byte-identical (REMOVED, was a coord misread)
memory 560..1600               ALL 6969 / 8 / 45782
```
Memory 560 (just +20) is already past the cliff. Cause is a **coupling**: the `tornado-clear` branch
`return`s, so it suppresses the pre-charge jump below it, and `PreJumpTicks = 20` is exactly that jump's
wind-up.

## NEXT ROUND: the memory/response decoupling test
`tornado-clear` couples three things that should be separate:
- **memory** — how long the column is remembered (`_tornadoTicksLeft`)
- **response** — how long the escape branch runs (currently the same)
- **jump gate** — whether the branch suppresses `PreJumpTicks`

The column lives **881** ticks but the memory is **540**, so 341 ticks of the episode are unmodelled. The
nesting odds are:
- chance the jump suppression (not the escape) causes the collapse: high
- chance recalling the column after its escape window closes is free: high
- chance it wins: moderate

**Exact test:** split the two windows, then sweep only the recall horizon with the response window pinned
at the reviewed 540:
- add `CHAITE_TORNADO_RESPONSE` (default = 540) gating the **escape + jump-suppression** branch
- let `CHAITE_TORNADO_MEMORY` (default 540, sweep to 881) control only the **recall**
- first confirm `MEMORY=881 RESPONSE=540` is **byte-identical** to `540/540`; if it is not, the split is
  wrong and must be reconsidered rather than tuned
- then sweep response 540..881 with memory 881

## Verified invariants (re-check every round)
Full band on obsidian, 320 tiles, 2 rows, standoff default ON:
```
strong 300 10004/8/30628   strong 600 6744/6/15937   strong 800 5057/4/17755
strong 1000 4573/3/10777   strong 1200 4437/1/KILL   strong 1500 3661/1/KILL
strong 2000 2881/0/ZERO-HIT KILL
weak 300 5879/8/51124      weak 600 4741/6/35818     weak 800 6380/5/KILL
weak 1500 3656/4/KILL      weak 2000 2881/1/KILL
```
Routes: both MATCH (strong 6000/6/False, weak 5636/9/True).

## Run method
Unset `CHAITE_POLICY_FILE` and `CHAITE_POLICY_FORMAT`. Then `CHAITE_ARMOR_TIER=obsidian`,
`CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`, `CHAITE_SIM_DPS_ZERO_TILES=401`,
`CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`. `-WallSeconds` must be 15..900.
Route + `-FormulaRoute` always together.

## Traps
- `verify-fishron-routes.ps1` now clears the full env (fixed this session) — keep it that way.
- Parse `result.json` for `bossLifeRemaining`; the printed header is capital-B `Boss life left`, so a
  lowercase PowerShell `-match` parse silently yields empty and makes every row look like `DIFF`.
- An identical death tick across different DPS = broken invariant.
- Tests: 749 pass / 9 fail, PRE-EXISTING. Never claim green.
