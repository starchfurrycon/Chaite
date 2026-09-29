# Round 153 handoff

## HEAD
`2860b8c` — all pushed. Worktree clean except untracked `tmp/`.

## HEADLINE: type 386 is a WALL of 25 sub-tornadoes, not a column

This corrects a substantive error carried since §141. The `225 × 63` figure was described as
"the column". Dumping `hostileProjectiles` in full shows **type 386 exists as 25 instances at
once**. Measured at tick 4048 of strong 1000:

```
x 1330..1485      y 5140..6049
scale 0.375..1.5  sizes 56x15 .. 225x63
ai1 = 0..24,  ai0 = -144, -152, ... -329  (step -8)
```

`225 × 63` is only the **largest member**. The group is a **diagonal wall roughly
perpendicular to the Boss's charge**: only ~155 px wide in x but ~909 px tall in y. That is
the **opposite** of the "very wide, very flat" reading I had been working from.

## The player is killed by DESCENDING THROUGH the wall

Per-tick `gapX`/`gapY` (negative = overlapping), player vs nearest 386:

| tick | player (x,y) | nearest 386 (x,y,w×h) | gapX | gapY |
|------|--------------|----------------------|------|------|
| 3990 | (2153, 5967) | (1470, 5569, 168×47) | 589 | 353 |
| 4032 | (1758, 5626) | (1464, 5657, 154×43) | 207 | **−12** |
| 4042 | (1678, 5587) | (1468, 5614, 161×45) | 120 | −17 |
| 4048 | (1630, 5568) | (1469, 5569, 168×47) | 67 | −44 |

The player descends from y 5967 to y 5568 over 58 ticks, crossing the wall's entire vertical
span, and at the hit the nearest sub-tornado's **top edge is 10 px below them**.

**No per-frame "veto the nearest member" rule can fix this**, because by the time a single
sub-tornado is the nearest one the player is already inside the wall's vertical span.

## THREE refutations this round (all measured) — the rule shape is exhausted

| variant | result |
|---|---|
| unconditional vertical suppression | strong 300 **16090 → 8872 (−7218)** |
| reach 125 → 600 | loses strong 600 and strong 900 kills; **targets still byte-identical** |
| vertical margin 73 → 400 | **byte-identical at all seven points tested** |

The third is the most informative: widening the margin changed *nothing*, which pins the
binding constraint on the guard's **"closing horizontally" requirement**, not on any margin
value. Margins returned to the true contact values (125 / 73).

**Tree state verified byte-identical to the committed circuit**: strong 600 still
**8329/4/0 KILL**. Tests **749 pass / 9 fail**; routes `MATCH`.

## THE NEXT DIRECTION MUST BE GEOMETRIC (not another per-frame veto)

Facts the next attempt can use directly:

1. **The wall is ~900 px tall and only ~155 px wide** ⇒ going *around* it horizontally is far
   cheaper than going *through* it vertically.
2. **The wall's position is fixed at the moment the Boss commits its charge** — it is laid
   along the charge path — so the side to pass on can be decided after the lock but *before*
   any descent.
3. **Strong 600 and strong 900 already kill**, and *neither has a single 386 hit*
   (s900: 1 bubble + 2 body; s600: 4 body + 1 tornado). So the wall **is** passable — the
   circuit simply keeps descending through it when it is present.

⇒ Keep the player clear of the cascade's **band**, not of its nearest member.

## Committed-default results (obsidian = honest tier)

**Strong**: 300 **16090/8/0 KILL** · 600 **8329/4/0 KILL** · 700 6205/7/11888 · 800 6011/5/5015 ·
900 5739/3/0 KILL · 1000 4536/4/11408 · 1100 4795/**0/0 zero-hit** · 1200 4437/**0/0 zero-hit** ·
1500 3661/1/0 KILL · 2000 2881/**0/0 zero-hit**

**Weak**: 300 8777/8/36712 · 600 6130/6 · 800 **6381/3/0 KILL** · 900 4439/5 · 1000 4395/5 ·
1100 4788/3/0 KILL · 1200 3082/8 · 1300 3489/4 · 1500 3362/4/7466 · 2000 2881/1/0 KILL

## Still unmet (honest)
- **Owner's relaxed criterion (survival-kill across 300–2000) is NOT met.** Strong fails
  700/800/1000; weak passes only 800/1100/1500/2000.
- **Weak wing: still ZERO zero-hit results ever.** Zero-hit only for the strong wing at
  1100/1200/2000; no zero-hit claim below 1200 DPS.
- Tests 749/9 (pre-existing baseline). Never claim the suite is green.

## Run method
Unset `CHAITE_POLICY_FILE` AND `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian` (or
`shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`;
`CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1`. Route + `-FormulaRoute`
always together. `-WallSeconds` 15..900. **Verify by parsing `artifacts/<run>/result.json`**
(`ticks`/`hits`/`bossLifeRemaining`).

**Useful probe fields for the next round**: `boss-observations.jsonl` rows carry the FULL
`hostileProjectiles` array *with per-instance* `width`/`height`/`scale`/`ai0`/`ai1`, and the
`plan` object with `phase`/`horizontal`/`jump`. That is how the cascade was found — it is the
cheapest way to see the real hazard geometry.
