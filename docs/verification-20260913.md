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

## Follow-up (same date)

- Confirmed from Terraria 1.4.5.8 `Projectile.AI_171_HallowBossRainbowStreak`
  that hostile type 873 stores the target player index in `ai[0]`, so the
  existing targeted capture and `TryCreateTargetedState` contract are correct.
  The native smoothing constants also match the Core model.
- The pre-dash route was locking a pure vertical dodge into the 873 homing
  lane. The state-1 route now releases movement closure when a live 873 streak
  is present, allowing the scorer to step around the streak.
- After that change the same deterministic daytime fixture no longer dies to
  873, but still dies to NPC 636 contact during the later reposition/dash
  window. A post-Plantera fixture probe was also given the reviewed
  Shield-of-Cthulhu dash accessory, but the daytime Empress contact death
  remains in this single seed.
- Daytime Empress is therefore still not verified as a high-reliability win.
  This remains an active follow-up, not a completed acceptance claim.

## Further daytime-Empress tuning

- The state-1 pre-dash escape now keeps horizontal movement directed away from
  the approaching Empress and locks that horizontal direction, while leaving
  the vertical axis free to step around the homing 873 streaks.
- The daytime Empress horizontal-dash phase now prefers the reviewed
  Shield-of-Cthulhu dash even in Classic, without making the dash a hard
  requirement for the ordinary night/Classic profile.
- Same deterministic smoke seed still ends in one NPC-636 contact death around
  tick 340; the day-Empress lower-bound route is not yet closed. The next
  follow-up should verify the native dash angle and exact contact envelope
  rather than claim a no-hit result.
- The headless probe does not run the full client accessory-refresh pass, so
  `Player.dashType` stayed zero even with the Shield-of-Cthulhu item equipped.
  The probe fixture now publishes the reviewed dash state explicitly for the
  post-Plantera loadout. The daytime pre-dash route also permits the reviewed
  shield dash when a live 873 streak is present, instead of locking both axes
  and forcing the player into the homing path.
- The single deterministic daytime seed still ends in one death. The remaining
  work is still the exact daytime-Empress dodge timing/envelope, not a proven
  win-rate claim.
- Even after the headless fixture explicitly publishes the Shield-of-Cthulhu
  dash state, the native `Player.dashType` reads zero during the battle. The
  headless probe therefore still does not produce a real dash edge, so the
  daytime-Empress dash-dependent formula cannot be validated there yet. This
  is the next concrete blocker for the daytime-Empress lower-bound route.
- Confirmed with a same-frame probe log that `extraAccessory=True` and
  `armor[8].type=3097` while `dashType` remains zero. The dedicated-server
  headless path does not run the client accessory functional pass that derives
  `dashType`, so the reviewed shield-dash route has no native edge in this
  fixture. Fixing that refresh path is required before the daytime-Empress
  formula can be exercised end to end.

## Git

- GitHub remote: `https://github.com/starchfurrycon/Chaite.git`
- Pushed commit `5cb6895` (`Narrow production takeover to Fishron and Empress`).
