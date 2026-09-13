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
- Expert daytime-Empress, same deterministic seed, no longer dies in the early
  pre-dash window: the 600-tick run reported 0 deaths / 0 hits. A longer
  24000-tick run still dies once at about native tick 960 and is classified as
  a loss, so the daytime-Empress route is not yet a high-reliability win.
- Product direction: support multiple equivalent lower-bound routes rather
  than one fixed loadout. Candidate routes include certified dash, Witch's
  Broom / other flight mounts, wings + mobility accessories, and multi-jump +
  feather-fall for the daytime Empress; dash, wings, or high-speed mounts for
  Duke Fishron. The Witch's Broom model is currently only kinematic and is
  still marked `ProductionClosureCertified=false`, so it cannot yet authorize
  production input; this is the next route to complete.
- The daytime Empress inter-attack move now always steps away from the body,
  not only when the next table entry is the 8/9 dash. The same expert seed
  then avoided the earlier NPC contact death, but now dies to projectile 873
  around tick 323, showing the body-vs-homing-streak conflict remains. A
  working shield-dash edge is the next concrete prerequisite; the headless
  dash refresh path still needs to be corrected.
- Forcing `Player.dashType/dashDelay/dashTime` once per headless frame was
  still not enough to make the planner request a shield dash, so the remaining
  gap is in the full `EyeShieldDashState` / `CanScoreEyeShieldDash` admission,
  not only the raw native field. The next step is to trace that admission path
  in the isolated probe rather than add more one-line state overrides.
- The daytime pre-dash route now locks horizontal escape away from the Empress
  but leaves the vertical lane free, so the scorer can step around a homing
  873 streak instead of being forced downward into it. The same expert seed
  still dies once in the isolated smoke, so this is a necessary but not
  sufficient part of the daytime-Empress lower-bound route.
- Three fresh expert seeds each for Duke Fishron and night Empress survived the
  first 900 native ticks with zero deaths (`valid=true`, all classified as the
  intended tick-bound timeout). Night Empress still took 1-4 hits, so this is
  not a no-hit claim, but it is reproducible survival evidence for the current
  finite-flight route. Daytime Empress remains the open cell.
- Night-Empress hit sources in that batch were hostile projectile 873 (rainbow
  streak) and 923 (sun dance), not body contact. The next no-hit pass should
  tighten those projectile envelopes rather than change the body-dodge route.
- Added a +24px safety cushion for `EmpressSunDance` beams. The same three
  expert night seeds reran with hits 2/4/1 (previously 3/4/1), still zero
  deaths. The sun-dance grazing is reduced but not eliminated; the 873 streak
  remains the other hit source to tighten.
- Added a +18px candidate-step cushion for the homing 873 rainbow streak. The
  same three expert night seeds now report hits 2/2/1 (previously 2/4/1),
  still zero deaths. The remaining night hits are still projectile grazes, not
  body contact.
- Raised the sun-dance cushion to +40px and the 873 streak cushion to +28px.
  The three expert night seeds then reported 3/1/1 hits (still zero deaths),
  so the remaining grazes are near the sampling/noise boundary rather than a
  single missing margin. Further no-hit work should target the native 873/923
  age/scale capture rather than blindly increasing the safety cushion.
- Decompiled `Projectile.AI_180_FairyQueenSunDance` (type 923): the existing
  `BeamGeometry` scale and rotation formulas match native, but native also moves
  the beam origin to the live Empress NPC center every tick. The current
  snapshot uses the projectile position captured at one frame, so the next
  correction is to couple the 923 beam origin to the Empress body in the
  candidate rollout rather than enlarge the margin further.
- Captured the 923 beam origin and source velocity from the live Empress NPC.
  The three expert night seeds still report 3/1/1 hits (zero deaths), so the
  remaining grazes are dominated by the 873 streak path rather than the 923
  origin alone. The next step is a native-equivalent 873 homing envelope under
  the player's coarse rollout step.
- Advanced the 873 targeted state once per native tick inside the coarse
  planner step. The three expert night seeds still report 3/1/1 hits, so the
  remaining grazes are not a simple sub-step interpolation gap; they need the
  actual native homing weight/timing checked against the captured 873 state.
- A temporary probe confirmed the 873 targeted state is created tens of
  thousands of times by native tick 378, so the night hits are not a fallback
  to the broad envelope. The remaining grazes come from the planner choosing a
  path that the correct homing model still intersects; the next work is scoring
  / candidate diversity for the 873 homing cone, not the motion model itself.
- Raised the near-miss penalty for the 873 homing streak by 1.5x. The three
  expert night seeds then reported 1/1/1 hits (previously 3/1/1), still zero
  deaths. This is the first scoring change to reproducibly reduce the night
  Empress grazes across all three seeds.

## Git

- GitHub remote: `https://github.com/starchfurrycon/Chaite.git`
- Pushed commit `5cb6895` (`Narrow production takeover to Fishron and Empress`).
