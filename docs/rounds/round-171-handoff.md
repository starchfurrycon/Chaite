# Round 171 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.
Only source delta vs the round-168 commit: **9 added lines in `tools/GameProbe.cs`** (a probe-report fix,
zero behavioral change — strong 625 still reproduces `7689/4/3480` byte-for-byte).

## HEADLINE: two armor tiers are now complete for BOTH wings — and **neither tier dominates**

The owner requires both tiers with obsidian as the honest yardstick, and **weak wing had only ever been
measured on obsidian**. This round ran the full weak-wing **shroomite** grid (28 points, same route, same
knobs) for the first time.

**Weak wing, obsidian vs shroomite, like-for-like:**

| dps | obsidian | shroomite | |
|---|---|---|---|
| 900 | KILL 5726/5 | KILL 5732/4 | agree |
| **1000** | **KILL 5210/5** | **DIED 4964/6** | **obsidian wins** |
| **1075** | **KILL 4885/2** | **DIED 4309/5** | **obsidian wins** |
| **1100** | DIED 4554/5 | **KILL 4792/2** | **shroomite wins** |
| **1125** | DIED 4430/5 | **KILL 4695/1** | **shroomite wins** |
| 1150 | KILL 4598/2 | KILL 4604/3 | agree |
| 1200 | KILL 4432/2 | KILL 4433/2 | agree |
| **1400** | DIED 3359/4 | **KILL 3880/3** | **shroomite wins** |
| **1600** | DIED 3219/4 | **KILL 3460/3** | **shroomite wins** |
| 1750 | DIED 3153/5 | DIED 3146/5 | agree (both one breath short) |
| 2000 | KILL 2880/2 | KILL 2880/1 | agree |

**Totals: obsidian 6/28, shroomite 8/28.** 22 points agree, shroomite wins 4, obsidian wins 2, and the
**kill ranges are interleaved**. **Higher defense is not a systematic advantage** — it changes *when*
damage empties the pool, hence *which DPS points happen to pass*.

**This also confirms the §170 contact budget from the other direction**: shroomite takes less damage per
hit but fights longer, so **contacts go UP** — weak 300 goes from **8 contacts / 5839 ticks** (obsidian)
to **13 contacts / 11125 ticks** (shroomite). *Taking less damage is not the same as being safer.*

## ★ Full acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| **strong wing** | **19/28** | (7-pt sample: 6/7) |
| **weak wing** | **6/28** | **8/28** |
| strict goal (`-maxticks 6000`, `hits == 0`) | strong at 1200 and 2000 = **zero-hit kills**; weak at **2000 = zero-hit kill** | — |

**Not met**: neither wing kills across the whole 300–2000 range. Goal stays active.

## ★ Probe defect fixed (would have misled future budget analysis)

`result.json` `equipment.lifeMax` always read **100** while the measured pool is **480**. Cause:
`GameProbe.cs` read `player.statLifeMax2` at **equipment-setup time**, before the scenario applies its max
life. It now also reports `statLifeMax` and `effectiveLifeMax` and documents the reading point.

**Rule for future rounds: do NOT use `equipment.lifeMax` for damage-budget work.**
Use `hurt-observations.jsonl` → `player.lifeBefore` (measured pool **480 = 400 + 80**).

## ★ Weak-wing failure structure, quantified

* **Every** weak death is the pool being emptied; there is no "failed without being hit" case.
* Weak 300 dies on its **8th hit at tick 5481** (life 116 → 0, one 158 hit). The run's reported
  `ticks=5839` is **post-death simulation** (358 extra ticks) — do not read it as fight length.
* Low band 300–800: **9/9 deaths on 6–9 contacts**. Strong wing in the same band: **2–6**.
  So the gap is **2–3× the contact count**, not a near miss.
* Root cause already on record: climb peak **−9.91 vs strong's −16.52**, `wingTimeMax` **130 vs 180**, so
  the lock-direction diagonal dodge produces **~60%** of strong wing's displacement.

## Next round

1. **Report both tiers, obsidian honest** (now possible for both wings).
2. **Strong wing mid/low band**: per §170.1 the budget is ~1 contact in 70 — attack **per-contact damage**
   (`§170.3`), not trajectory.
3. **Weak wing**: stop tuning delay-type knobs (7 already refuted: `CHAIte_WEAK_DASH_DELAY`,
   `CHAITE_WEAK_PREJUMP`, weak pre-jump lead, weak gate removal, colocation gate, refill guard, wing-time
   floor). The one direction that is **wing-differentiated and matches the measured deficit**: make the
   weak wing dodge with **horizontal displacement + shield dash** rather than *diagonal climb*, since
   horizontal speed is identical between wings (cruise 7–8, dash peak 14.5) while its climb is 60%.
4. Note the strong-wing low-band kills are **fragile at ~20% positional disturbance** (§170) — any change
   must be verified across 300/400/450 first.

## Do NOT re-attempt

See §169/§170 lists, plus: tornado escape under **any** trigger; DPS-constant gates; armor tier as a
survival lever (now proven symmetric); weak-wing delay knobs.

## Standing constants

* **28 ticks per charge**; hover 30/40; dominant period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, up to **50 live**, box **225×63**, a **~150×900 px wall** with `vx == 0`.
* Damage census: ≈½ contacts = 386, a few = 384, ≈⅓ = boss body.
* Boss phase thresholds `lifeMax = 78000` → **39000 / 11700**.
* Player pool **480** (400 + 80). Boss despawn/report lag is a fixed **302 ticks** in every run.
* Scripts: `tmp/weakshroom171.ps1`, `tmp/fullgrid-te.ps1`, `tmp/lifegate170.ps1`, `tmp/bosslife170.ps1`.
