# Tutorial-derived pattern notes (ASCII only)

These notes transcribe public guides into explicit rules for Chaite. They are
meant to replace blind scoring tuning with per-attack deterministic responses.

## Duke Fishron (post-Plantera, expert)

- Phase 1 (100%-50%): 5 charges -> 21 detonating bubbles -> 5 charges ->
  Sharknado (29 tiles, 9s, 6 Sharkrons). Move vertically the instant a charge
  starts. Inferno Potion auto-pops bubbles.
- Phase 2 (50%-15%): 3 faster charges per set (~1s apart), 31 circling bubbles,
  tracking Cthulhunado 58x23 tiles for 14s with 12 Sharkrons. Drag tornadoes
  to arena edges; keep center clear.
- Phase 3 (<15%, Expert/Master): fixed teleport/dash rhythm:
  teleport -> 1 dash -> teleport -> 2 dashes -> teleport -> 3 dashes -> reset.
  Counter-dash with Shield of Cthulhu into his charge for i-frames; full
  evasion during the triple dash.

## Empress of Light

- Night Phase 1: Prismatic Bolts (wide circles, never stop; bolts overshoot
  curves), horizontal Dash (perpendicular on body glow), Sun Dance (fly
  straight up/down, stay near pivot where beam speed is lowest), Ethereal
  Lance (vertical dodge after side telegraph lines).
- Night Phase 2 (<50%): Everlasting Rainbow trail lingers ~4s; never retrace
  path or chase her directly. Lances come from more angles; keep moving.
- Daytime: all attacks one-shot. Soaring Insignia / good wings / high-speed
  mount or Master Ninja Gear dash are the lower-bound mobility. Wide circles
  and constant motion; perpendicular dash on charge telegraph; vertical for
  Sun Dance; fresh space for rainbow trail.

## Mapping intent

- Replace score-only avoidance with native-state branch rules: state 6 =
  Sun Dance -> vertical escape; state 5 = rainbow -> fresh-space path; state
  1 with next=8/9 -> perpendicular dash; Fishron phase 3 -> explicit 1-2-3
  dash rhythm + Shield counter-dash.

## Implemented in `BossStrategyCatalog.cs`

- `EmpressStrategy` always seeds `OrbitIntents` before the native-state
  switch. Night and day both keep a continuous large loop rather than
  coasting through Prismatic Bolts.
- State 2 (Prismatic Bolts) is now always `CircleOrbit` with the strict
  horizontal/vertical loop intent.
- State 6 (Sun Dance) no longer uses a tangent orbit. It moves vertically
  toward the Empress body line with `PerpendicularY`, which matches the guide
  "stay near the pivot where the beam is slowest": player below -> up,
  player above -> down.
- State 1 with the next fixed table entry 8/9 keeps the perpendicular charge
  lane reservation. States 4/7/11 retain vertical lane escape for lance walls.
- `EmpressSunDanceMovesTowardPivotLine` regression pins the up/down sign
  convention for both above and below the body.

## Fishron native rhythm already encoded

- Native AI_069 states map directly to spawn/hover/dash/bubble/tornado/phase
  transition/teleport groups.
- Phase 3 `ai[3]` sequence is decoded as teleport -> 1 dash -> teleport ->
  2 dashes -> teleport -> 3 dashes -> reset, and `FishronThirdPhaseHonorsCurrentVersionBoundary`
  plus the sequence-boundary tests lock that rhythm against the current
  1.4.5.8 decompile.
- Expert/Master `FishronStrategy` certifies Shield of Cthulhu as the burst
  baseline, so the planner can counter-dash during the fixed dash windows;
  full triple-dash evasion is achieved by never treating state 11 as a
  generic orbit and by latching the charge escape direction for the group.
