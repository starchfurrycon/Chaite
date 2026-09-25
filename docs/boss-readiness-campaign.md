# Boss readiness campaign (v1)

`boss-readiness-coverage-v1.json` is the fixed product acceptance scope for
Terraria 1.4.5.8. It is intentionally a coverage declaration, not a report of
completed testing or a promise that every row is currently implemented.

The catalog has 63 independent strata. The six Empress of Light rows (night and
day, at each of the three difficulties) are outside the program scope now that
the Empress has been removed from the program, leaving 57 in scope:

| Category | Boss / encounter variants | Difficulties | Goal | Cells |
|---|---:|---:|---|---:|
| Priority | Deerclops, Skeletron, Queen Bee, Wall of Flesh, Duke Fishron, Moon Lord | Classic, Expert, Master | `priority-near-certain` | 18 |
| Secondary | King Slime, Eye, Eater, Brain, Queen Slime, Destroyer, Twins, Prime, Plantera, Golem, Lunatic Cultist, mechanical trio, Mechdusa | Classic, Expert, Master | `secondary-majority` | 39 |

Eater of Worlds and Brain of Cthulhu are separate world-evil alternatives. The
mechanical trio is the normal simultaneous three-mech encounter, while Mechdusa is the
distinct `getfixedboi` encounter. Invasions and wave events remain outside this
Boss-only product scope. Other secret-world rule mutations are not allowed to
masquerade as `standard`; they need their own future, versioned coverage
catalog before they can contribute to a readiness claim.

## Acceptance rules

Every cell uses at least 40 distinct, preregistered seeds and one common build.
Every planned seed must have exactly one audited completed record. A death,
timeout, rejection, duplicate retry, missing evidence, or profile/build mismatch
is never converted into a win.

- `priority-near-certain`: observed win fraction >= 95% and the lower endpoint
  of the two-sided 95% Wilson interval >= 90%.
- `secondary-majority`: both the observed fraction and the Wilson lower endpoint
  must be strictly greater than 50%.

The evaluator applies these rules per cell and ANDs the cells. A strong result
for one Boss, difficulty, day/night form, weapon profile, or seed set cannot
compensate for a weak or absent one elsewhere. At the 40-sample minimum, a
priority cell in practice needs all 40 wins to clear the 90% Wilson lower bound.

## Staged-fixture boundary

The current priority-Boss phase suite is deliberately a **regression** suite,
not a campaign. It uses native direct spawning and disclosed AI/life/position
phase staging so that F8 recovery edges can be checked. Those results are
written as `evidenceKind: staged-native-phase-regression` and
`readinessEligible: false`.

The readiness evaluator does not trust those two labels alone. For every
candidate campaign record using result schema v1, it independently requires
the probe's origin fields to describe the non-staged path:

- `directSpawn: false`, `directSpawnTick: -1`, and all direct-spawn/staging
  Boolean flags `false`;
- `phaseStage` and `takeoverNativeSnapshot` explicitly `null`;
- `encounterFixtureReady: true` and `summonConsumed: true`.

Consequently, copying a staged result into a summary and changing only its
labels cannot make it a readiness win. Missing origin fields also fail closed.
The regression suite remains useful for phase coverage, but even a 100% staged
suite must never be quoted as a priority-Boss success rate.

Deerclops and Queen Bee now also expose a `summon` phase under their existing
readiness identities. Those two phases place exactly one Deer Thing (item 5120)
or Abeemination (item 1133) in hotbar slot 1, synthesize the normal production
F8 edge, and rely on `BossStartPlanner` / `ExecuteSummonPulse` plus vanilla item
use and consumption to create the encounter. They never enter the probe's
direct-spawn or native-field phase-staging path. The runner independently
requires the exact item identity/count, observed consumption, null staging
reports, false staging flags, and the expected native Boss identity before it
accepts `evidenceKind: isolated-native-encounter` with
`readinessEligible: true`.

`run-boss-validation.ps1 -Suite priority-organic6` plans the two organic
encounters at all three supported difficulties. It remains a bounded fixture
suite, not a readiness result: every catalog cell still needs its complete
preregistered campaign. The other six priority scenarios currently remain
staged-only. The existing launcher/manifest/desktop-lock audit must validate
every raw run before its `summary.json` is supplied to this evaluator; the
evaluator is intentionally a summary decision tool, not a replacement for raw
launch-evidence review.

## Creating a campaign plan

First collect profile discovery from explicitly bounded, isolated native test
batches. The probe must report an explicit `result.variant`; older evidence
that inferred `legacy-standard-fixture` is deliberately ineligible for this
catalog-bound campaign.

```powershell
./tools/evaluate-boss-readiness.ps1 `
  -Summary "$PWD\artifacts\summary-a.json","$PWD\artifacts\summary-b.json" `
  -DescribeProfiles > artifacts\readiness-profiles.json
```

Choose all 40 or more independent integer seeds before starting the campaign,
then generate the immutable V3 target plan. The generator fails closed if a
catalog cell lacks a discovered profile, if a profile has an unknown Boss or
variant, or if profiles were collected from different builds.

```powershell
./tools/new-boss-readiness-targets.ps1 `
  -Catalog "$PWD\docs\boss-readiness-coverage-v1.json" `
  -Profiles "$PWD\artifacts\readiness-profiles.json" `
  -PlannedSeeds 101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125,126,127,128,129,130,131,132,133,134,135,136,137,138,139,140 `
  -SamplingPlanId "2026-09-fixed-campaign" `
  -SamplingPlanDescription "Independent seeds fixed before native execution" `
  -Output "$PWD\artifacts\readiness-targets-v3.json"
```

The seed values above are only command syntax examples, not a recommended or
already-preregistered campaign. Do not overwrite a generated plan; preserve it
with the resulting evidence.

Finally evaluate only audited isolated summaries against both the generated
plan and the checked-in catalog:

```powershell
./tools/evaluate-boss-readiness.ps1 `
  -Summary "$PWD\artifacts\batch-summary-a.json","$PWD\artifacts\batch-summary-b.json" `
  -Targets "$PWD\artifacts\readiness-targets-v3.json" `
  -Catalog "$PWD\docs\boss-readiness-coverage-v1.json" `
  -Output "$PWD\artifacts\readiness-report-v3.json"
```

This command is read-only except for the explicitly new `-Output` report. It
never starts Terraria, touches a user save, changes input focus, or opens a
desktop. The native campaign runner remains a separate, explicit `-Run` step.
