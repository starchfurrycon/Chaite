# Round 150-151 handoff

## HEAD
`109bf9c` — all pushed. Worktree clean except untracked `tmp/`.

## THE TWO RESULTS OF ROUND 150

### 1. The escape WINDOW was the load-bearing tornado knob, not its width
`CHAITE_TORNADO_RESPONSE` default **raised 540 -> 120** (`TornadoResponseTicks`).

The decoupling left a defect: the gate evaluated `TornadoClearanceRadius` (760) while
`TornadoRecallRadius` was computed but never used, so `CHAITE_TORNADO_RECALL` was a **dead variable** —
`=2000` changed nothing, twice, while `RESPONSE` alone moved the run. Located by putting base /
recall-only / response-only / both in ONE sweep. Fixed the gate to honour `TornadoRecallRadius`
(default is still `TornadoClearance`, so the reviewed circuit is byte-identical).

Then the sweep showed the width is irrelevant (760 / 2000 / 4000 all **identical**; natural `|dx|`
already exceeds every candidate), and the WINDOW is what matters. Decoupled from the memory, 540 is far
too long a commitment — the branch `return`s for its whole length, so the controller flees for 9 s and
never re-engages:

```
strong, obsidian, 300 DPS
  180 -> 6583/6/47682      240 -> 7168/7/44790     360 -> 4889/5/56197
  420 -> 6970/8/45795      540 -> 10004/8/30628    **120 -> 10749/8/26835**
```

Sharp and NON-MONOTONE, so it was swept, not reasoned. Decisive evidence is the same window elsewhere:

| DPS | 540 (old default) | 120 |
|---|---|---|
| 600 | 6744/6/15937 (cannot kill) | **8328/5/KILL** |
| 1200 | 4437/1/KILL | **4437/0/ZERO-HIT KILL** |

**Regression-checked**: route channel still `REPRODUCED ... MATCH` (strong 6000/6/55, weak 5636/9/102),
because it is gated on `CHAITE_SIM_DPS`, which route replay never sets.

### 2. The demo video reconstructed by OCR of the game's own HUD text
Owner's round-150 direction. **Do not track sprites — read the Depth Meter.** It prints absolute world
coordinates as text, giving an exact trajectory with no vision model:

```
数字条 x 1061..1148, y 442..459 (1280x720)  ->  "3880以西" = world x 3880 tiles
```

1 Hz over 232 frames -> **218 points**. Measured "W走位": **major turns every ~11 s**
(11,10,9,11,14,11,12,9,11,11,10,12,11,11,11,11), east end **x ≈ 3990..4093**, west end **x ≈ 3553..3648**
— a **full ~530-tile arena crossing per leg**, at 50–55 tiles/s, hugging the surface (depth 218–305).
Current circuit escapes only **760 px (47 tiles)**: an order of magnitude smaller than the real
technique, which is why widening it did not help — width was never the variable.

**Abandoned**: 10 Hz digit template matching. The HUD font's strokes fragment under thresholding and
connected components split one digit into several boxes; validation never exceeded 0.9%. 1 Hz is enough
for turn structure.

## Objective status (honest)
- **300 DPS floor: STILL UNMET.** strong `10749/8/26835` (dies), weak `9456/9/33297` (dies).
- Owner's relaxed bar ("stable survival-kill 300–2000") **not met**: strong kills at 600 but **dies at
  800/1000** (non-monotone — a parameter-sensitive regime, not a robust policy), zero-hit from 1200.
  weak dies at 300/600/800, kills at 2000 only.
- **No no-hit claim below 1200 (strong); none at all for weak.**

## UI
Not regressed. `UiTheme` (`a5f45b8`, 9/25) landed **after** `805243e` (9/23); frameless
`FormBorderStyle.None`, no rectangles, gradient hairlines; `Chaite.Manager.exe` (9/27 18:42) post-dates
`UiTheme.cs` and `MainForm.cs`. Visible text is only short functional labels.

## Next lever (not yet tried and NOT refuted)
The video says the real dodge is a **full-arena run**, while the circuit's locked-charge beats
(`_chargeBeat` 0/1/2) already implement *something* like the owner's perpendicular rule. What is missing
is the owner's third clause: **only run straight away when the lock distance is already large**. Every
attempt to re-steer the *command* failed (exact-perpendicular, along-the-charge, pre-lock facing — all
refuted with numbers in-file); the file's own recorded lesson is that both genuine wins **added a missing
pre-condition** rather than redirecting an existing command. So the next attempt should add the
distance-gated horizontal pull-out as a new precondition, not re-aim the existing normal.

## Run method
Unset `CHAITE_POLICY_FILE` AND `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian`,
`CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`, `CHAITE_SIM_DPS_ZERO_TILES=401`,
`CHAITE_SIM_BUBBLE_BREAK=0.95`; `CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1`.
Route + `-FormulaRoute` always together. `-WallSeconds` 15..900.
**Verify by parsing `artifacts/<run>/result.json`** (`ticks`/`hits`/`bossLifeRemaining`) — PowerShell
`Select-String` on the printed verdict is case-sensitive in pwsh 7 and has silently produced false DIFFs.
