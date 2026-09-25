# Tutorial-derived pattern notes (ASCII only)

These notes transcribe public guides into explicit rules for Chaite. They are
meant to replace blind scoring tuning with per-attack deterministic responses.

## Duke Fishron (post-Plantera, expert)

- Phase 1 (100%-50%): 5 charges -> 21 detonating bubbles -> 5 charges ->
  Sharknado (29 tiles, 9s, 6 Sharkrons). Move vertically the instant a charge
  starts. Inferno Potion auto-pops bubbles.
  - CORRECTION (user, 2026-09-15): the bubbles are meant to be *shot down*.
    They are one-life NPCs, so a high fire rate or a wide/piercing weapon clears
    them; weaving left and right to dodge them was the mistake, and the
    `bubble-line` branch's 39% per-frame direction flip is a defect, not intent.
    The Inferno ring only covers what reaches the player.
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
- State 5 (Everlasting Rainbow) keeps a `CircleOrbit`, but `FreshRainbowLoopDirection`
  flips the orbit when the nearest live trail lies ahead of the current loop,
  matching the guide's "never retrace the path, fly to fresh space" rule.
- State 6 (Sun Dance) no longer uses a tangent orbit. It moves vertically
  toward the Empress body line with `-PerpendicularY`, which matches the guide
  "stay near the pivot where the beam is slowest": player below -> up,
  player above -> down. `PerpendicularY` itself is world-space signed, so the
  negation converts it into the player-gravity `VerticalIntent` convention.
  - SCOPE CORRECTION (2026-09-15): everything in this section describes
    `BossStrategyCatalog.cs`, the legacy scorer. It does NOT describe the
    formula scripts that actually fly the reviewed Empress routes.
    `EmpressFlightScript` still calls `Tangent()` for state 6, a fixed
    per-quadrant diagonal that is not an orbit: measured on rt-eb1 it drives
    the player through the body (centre distance 325 -> 41 px) and then in a
    straight line out to 3010 px, still taking beams. Do not read this file as
    a description of the live Empress circuit. See
    `docs/continuation-20260915.md` sections 8-9.
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
  generic orbit.
- FALSIFIED (2026-09-15): this file used to end with "and by latching the charge
  escape direction for the group". Latching it was measured on ten seeds and is
  clearly worse -- 1 win / 78 hits against 3 / 73 for the baseline and 4 / 75
  with only the body latch. A charge is aimed continuously at the player's
  current position, so its side is feedback and must be re-derived every frame;
  the *beat* is scheduling and is the thing that must be latched. See
  `docs/continuation-20260915.md`.
