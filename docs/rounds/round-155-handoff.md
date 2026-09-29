# Round 155 handoff

## HEAD
`1f72e9e` — pushed. Worktree clean except untracked `tmp/`.

## HEADLINE: the wall escape now carries a hazard-depth gate and is ON by default

Round 154 left the escape **opt-in** because it destroyed the extended low-DPS fight while
winning at high DPS, and the damage scaled with **fight length**. That scaling pointed at the
**vertical** dimension, not at DPS: being inside a wall's x-footprint only matters if the
player is also inside its vertical collision band. At low DPS the same x-overlap happens
hundreds of px above the band (`§147.5`: at strong 1000 t=3705 the player is at y 4432 while
the wall spans y 5140..6049), where yielding the horizontal axis buys nothing.

### Implementation
- Each published wall now carries `CascadeWallTop(i)` / `CascadeWallBottom(i)`.
- `PublishCascadeWalls` takes both x and y centre buffers and sorts them **together** with
  `Array.Sort(keys, items, ...)`, so each contiguous run also reports its vertical span,
  padded by the largest member's half-height (31.5 px = scale 1.5 of a 42-px base).
- `ObserveCascade` sets `_cascadeAtHazardDepth`; `TryEscapeCascade` requires it unless
  `CHAITE_CASCADE_DEPTH_GATE=0`.
- Defaults: escape **ON**, depth gate **ON**. `CHAITE_CASCADE_ESCAPE=0` disables the escape.

### Measured: the gate restores exactly what the ungated escape destroyed

| point | baseline (no escape) | ungated (r154) | **gated (r155, default)** |
|---|---|---|---|
| strong 300 | 16090/8/**0 K** | 10202 **death** | **16103/8/0 K** |
| strong 600 | 8329/4/**0 K** | 6908 **death** | **8329/4/0 K** |
| strong 700 | 6205/7/11888 | 5228/6/23286 | 6205/7/11888 |
| strong 800 | 6011/5/5015 | 6388/1/**0 K** | 5654/5/9795 |
| strong 900 | 5739/3/**0 K** | 5738/2/**0 K** | 5739/3/0 K |
| **strong 1000** | 4536/4/**11408 death** | 5217/3/**0 K** | **5217/3/0 K** |
| strong 1100 | 4795/**0/0** | same | 4795/**0/0** |
| **strong 1200** | 4437/**0/0** | 3940 **death** | **4437/0/0** |
| strong 1500 | 3661/1/0 K | 3661/**0/0** | 3661/1/0 K |
| strong 2000 | 2881/**0/0** | same | 2881/**0/0** |

Weak: 800/900/1100/2000 keep their kills; 300/600/1000/1200/1300/1500 do not kill.

**Honest: this is an EVEN TRADE, not a clean win.** Strong 1000 gains a kill (it was a death),
but weak 1500 loses its kill (3362/4/0 → 3642/4/330) and weak 900 loses a hit (3 → 4).

## Shroomite tier re-run (second tier, not the honest one)
Shroomite (63 def) fails **more** at low DPS than obsidian (27 def): strong 300 is
**10153/13/29836 death** on shroomite vs **16103/8/0 KILL** on obsidian. Same mechanism as
`§143` — higher defence lowers damage per hit, so the player survives longer, takes *more*
hits, and still dies with more Boss health left. Hit counts are **not** comparable across
defence tiers. Obsidian remains the honest yardstick.

Shroomite kills: strong 1100/1200/1500/2000 (2000 = zero-hit), weak 1100/2000.

## Why single-axis rules keep moving only one or two points
Damage-source decomposition of the remaining obsidian failures:

| point | hits | sources |
|---|---|---|
| strong 800 | 5 | **wall ×3**, body ×2 |
| strong 700 | 7 | Sharknado384 ×3, body ×2, wall ×2 |
| weak 300 | 8 | **body ×7**, Sharknado384 ×1 |
| weak 600 | 6 | body ×4, wall ×2 |
| weak 1000 | 5 | body ×3, wall ×2 |
| weak 1200 | 8 | **Sharknado384 ×7**, body ×1 |
| weak 1500 | 4 | body ×2, wall ×2 |

**No failing point has a single dominant source.** Everything built so far (the tornado axis
guard, the wall escape) targets only type 386. The rest of the damage comes from **body
contact** and **Sharknado (384)**. A single-axis rule therefore cannot close the gap.

## Determinism confirmed
Two new dense runs (`CHAITE_PROBE_DENSE_FRAMES=1`) reproduced the ordinary runs byte-for-byte:
`weak 600 = 6130/6/22031`, `weak 1000 = 4489/5/12159`. Native replay is fully deterministic —
if two runs differ, the configuration genuinely changed.

## Still unmet (honest)
- **Owner's relaxed criterion (survival-kill across 300–2000) NOT met.** Default fails
  strong 700/800 and weak 300/600/1000/1200/1300/1500 (obsidian).
- **Strict zero-hit NOT met.** Zero hits only at strong 1100/1200/2000 (obsidian).
  **The weak wing has NEVER produced a zero-hit result.**
- Tests **749 pass / 9 fail** (pre-existing). Never claim the suite is green.

## Next direction
The two families that have been ignored are **body contact (NPC 370)** and
**Sharknado (384)**. Weak 1200 is 7/8 Sharknado; weak 300 is 7/8 body. Either:
1. attack those two families directly (a 384 avoidance rule, or a body-contact spacing rule
   that is genuinely new rather than a re-tune of the existing standoff), or
2. reduce hits **per unit time** rather than improving single-dodge accuracy — every failing
   point is a long fight where the player is eventually ground down, and the kill margins are
   large (strong 800 still has 9795 Boss health at death).

## Run method
Unset `CHAITE_POLICY_FILE` **and** `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian`
(or `shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`;
`CHAITE_CASCADE_DEPTH_GATE=0` for the ungated A/B; `CHAITE_CASCADE_ESCAPE=0` to disable the
escape; `CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1` whenever per-tick
data will be analysed. Route + `-FormulaRoute` always together.
`-MaxTicks` ∈ **600..24000**, `-WallSeconds` ∈ 15..900. Verify by parsing
`artifacts/<run>/result.json` (`ticks`/`hits`/`bossLifeRemaining`).
