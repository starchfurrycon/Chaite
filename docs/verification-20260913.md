# Chaite verification notes - 2026-09-13

This file is ASCII-only to avoid mixed-encoding damage in the legacy Chinese
verification log.

## Production scope

- Production takeover is restricted to Duke Fishron (NPC 370) and Empress of
  Light (NPC 636, day/night). Other Bosses are rejected before summon/item
  consumption and play the user-supplied `boss_too_hard_for_me.wav` slot with
  the fixed message "这个波斯可是超囊的对我来说".
- Truffle Worm fishing and Prismatic Lacewing release are the only supported
  automatic summon routes.

## Unit regression

- `tools/build.ps1 -Configuration Release` succeeds for all projects.
- `tests/Chaite.Tests/bin/Release/net48/Chaite.Tests.exe` reports
  `通过 721，失败 0` (721 passed, 0 failed) with the current build.
- Fixes in this pass:
  - `HostileRainbowThreat` fixture now marks `TrajectoryAi0Known = true`,
    matching the production fail-closed contract for projectile 873.
  - Empress pre-dash state 1 now steps off the horizontal 8/9 charge lane
    vertically and widens the daytime contact margin; the pre-dash route owns
    movement closure so the generic scorer cannot steer back along the lane.

## Isolated native smoke (headless, private desktop, no user save/input)

Three 600-tick staged-native-phase regression cases were re-run after the
873-target fix:

- `duke-fishron classic p1-hover seed 2026091301`: timeout (intended 600-tick
  bound), 0 deaths, 3 hits.
- `empress-night classic p1-reposition seed 2026091302`: timeout, 0 deaths,
  1 hit.
- `empress-day classic p1-reposition seed 2026091303`: timeout, 1 death.

After the pre-dash change, the single daytime Empress case no longer dies to
NPC 636 contact. The remaining death is projectile type 873 (rainbow streak)
at native tick 346 in the same deterministic seed:

`{"source":{"kind":"projectile","type":873,"damage":9999},
  "request":{"damage":17798}}`

This is a single-seed staged fixture, not a general win rate. The daytime
Empress one-shot path still needs work before any "near-certain" claim can be
made.

## Git

- GitHub remote: `https://github.com/starchfurrycon/Chaite.git`
- Pushed commit `5cb6895` (`Narrow production takeover to Fishron and Empress`).
