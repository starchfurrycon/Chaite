# Round 151 handoff

## HEAD
`83945d0` — all pushed. Worktree clean except untracked `tmp/`.

## HEADLINE: the strong wing now MEETS the 300 DPS floor

`StandoffDistanceDefaultPixels` **1200 -> 1600**. Native, obsidian, 320 tiles, 2 rows:

```
strong 300   16090 / 8 hits / boss life 0   ACCEPTED (kill)   <- was 10749/8/26835 no-kill
```

Verified with the committed default. At 300 DPS the sim deals 5 damage/tick against
lifeMax 78000, so a kill needs **15600 ticks** of survival (§140). 16090 clears it with 490
ticks to spare — the measured value lands almost exactly on the prediction.

The old 1200 was justified by a note claiming the term "saturates above 1200". That came
from a sweep of the MIDDLE band, where the standoff is inert. Swept at the low end it does
not saturate: 1600 kills, 1800/2000/2400 collapse.

**Ten-point A/B, reviewed-1200 vs 1600**: byte-identical at 600/700/800/900/1000/1100/
1200/1500/2000, and **+5341 ticks at 300**. So it changes exactly one point in the band,
and at that point converts a failure into a kill. The radius stays DPS-gated
(`CHAITE_STANDOFF_DPS_MAX`, ceiling 450), which is why the other nine are untouched.

Route channel re-verified: both routes still `MATCH` (strong 6000/6/55, weak 5636/9/102).

## Also landed this round
- `CHAITE_TORNADO_RESPONSE` **540 -> 120** (`TornadoResponseTicks`). Also fixed a defect I
  had introduced: the escape gate evaluated `TornadoClearanceRadius` while
  `TornadoRecallRadius` was computed but never used, making `CHAITE_TORNADO_RECALL` a
  **dead variable**. Width then proved irrelevant (760/2000/4000 identical); the WINDOW is
  what matters.
- `CHAITE_LOCK_RUN_AWAY` (the owner's distance exception) implemented and **refuted as a
  band rule**: knife-edge optimum at 550, strictly loses the strong-600 kill, and at 400
  two configs from opposite wings die on the identical tick 2086.

## Committed-default results (obsidian = honest tier)

**Strong**: 300 **16090/8/0 KILL** · 600 8328/5/0 KILL · 700 6205/7/11888 · 800 6011/5/5015 ·
900 5739/3/0 KILL · 1000 4536/4/11408 · 1100 4795/0/0 **zero-hit** · 1200 4437/0/0
**zero-hit** · 1500 3661/1/0 KILL · 2000 2881/0/0 **zero-hit**

**Weak**: 300 8777/8/36712 · 600 4924/6 · 800 5287/5 · 1100 4788/3/0 KILL · 1200 3082/8 ·
1300 3489/4 · 1500 3362/4/7466 · 2000 2881/1/0 KILL

## Shroomite (high-defence) tier — now measured too
Counter-intuitively takes **MORE** hits than Obsidian: strong 300 **13 vs 8**, strong 600 7
vs 5, weak 300 10 vs 8, weak 1200 10 vs 8. Cause: 63 defence vs 27 lowers damage per hit, so
the player survives longer and the fight runs longer. Strong 300 took 13 hits without dying.
=> **hit count is not comparable across defence tiers**; the defence tier does not move the
structural gap (the same DPS points fail on both).

## Still unmet (honest)
- **Owner's relaxed criterion (survival-kill across 300–2000) is NOT met.** Strong fails
  700/800/1000; weak passes only 1100/1500/2000.
- **Weak wing: no kill below 1100, and still ZERO zero-hit results ever.**
- Zero-hit has only been observed for the strong wing at 1100/1200/2000. No zero-hit claim
  is made below 1200 DPS.
- Tests: **749 pass / 9 fail** — same pre-existing baseline (2 missing
  `observation-conformance.jsonl` fixtures + 7 others). Never claim the suite is green.

## THE ONE STRUCTURAL FINDING TO BUILD ON (§141)
All four boss-body hits in strong-300 share an anatomy: `eocDash=0`, `immune=True`, and the
shield recoil velocity `(+-4.50, -3.50)` — i.e. **the dash has already struck the body**
(spending `eocHit`), and by the time contact is judged `eocDash` is back to 0, so dash
immunity no longer covers it. A shield dash buys only ~4 ticks and ~58 px laterally, which
**cannot clear the 85 px body half-width**.

Two different failure shapes at impact:
- seq 1 & 4: player already clear of the 85 px horizontal box, short **vertically** by only
  **3.2 px** and **18.9 px**.
- seq 2 & 3: vertical miss by 52.8 and 63.8 px — the vertical component never engaged.

Temporal budget: the lock lands **19–28 ticks** before the hit (about 20 = `PreJumpTicks`),
so a later reaction is impossible. Inside it, **vertical is affordable** (descent
10.01 px/tick -> 71 px in 7–9 ticks) and **horizontal is not** (cruise 7–8 -> 85 px in ~11
ticks vs a 4-tick dash). **Vertical is the reachable avoidance axis; horizontal is not** —
which is also why the "exact perpendicular" attempt failed, since it moved the horizontal
command.

## Also newly established: 700/800/1000 are structural, not tuning
Strong 800 gives **byte-identical `6011/5/5015` at px 1300, 1400, 1600 and 1800**; strong
1000 likewise at 1400/1600/1800. These points are ALSO no-kill at the reviewed 1200. So the
mid-band failures are **independent of the standoff radius** and of the owner's
lock-distance rule (§139). Next work should target them directly — most likely the phase-1
charge sequence, possibly per §141's precondition (hold the vertical dodge until the 71 px
box is actually cleared) rather than another command redirect.

## Run method
Unset `CHAITE_POLICY_FILE` AND `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian` (or
`shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`;
`CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1`. Route + `-FormulaRoute`
always together. `-WallSeconds` 15..900.
**Verify by parsing `artifacts/<run>/result.json`** (`ticks`/`hits`/`bossLifeRemaining`) —
`Select-String` on the printed verdict is case-sensitive in pwsh 7 and has silently produced
false DIFFs.
