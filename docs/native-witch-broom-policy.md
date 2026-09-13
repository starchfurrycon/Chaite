# Native Witch's Broom motion contract

This document freezes a deliberately narrow, fail-closed motion contract for the
Witch's Broom as an optional rescue layer. It is not a generic flying-mount
approximation and it is not evidence of any boss win rate.

## Locked native evidence

- Game: vanilla Terraria `1.4.5.8`
- `Terraria.exe` SHA-256:
  `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`
- Managed assembly identity: `Terraria, Version=1.4.5.8`
- Module MVID: `2c29f6c3-4bd9-4add-9c58-da159804e083`
- Read-only inspection: Mono.Cecil `0.11.6`; decompilation was used only as a
  readable rendering of the same locked IL.

Relevant definitions in that exact module:

| Native member | Metadata token | RVA / useful IL evidence |
| --- | ---: | --- |
| `Item.SetDefaults5(int)` | `0x0600076C` | case `4444` assigns `mountType = 23` |
| `Mount.Initialize()` | `0x06000243` | RVA `0x0002F63C`; type-23 block starts at `IL_259A` |
| `Mount.CanHover()` | `0x0600026A` | RVA `0x000345B5` |
| `Mount.Hover(Player)` | `0x0600027C` | RVA `0x000354AC` |
| `Mount.DoesHoverIgnoresFatigue()` | `0x0600027E` | type 23 branches true at `IL_0022..004F` |
| `Mount.SetMount(int, Player)` | `0x0600029C` | RVA `0x0003BE40` |
| `Mount.Dismount(Player, bool)` | `0x0600029B` | RVA `0x0003BC90` |
| `Player.QuickMount()` | `0x0600089B` | RVA `0x002E597C` |
| `Player.HorizontalMovement()` | `0x0600094E` | RVA `0x002FFBF4` |
| `Player.DashMovement()` | `0x0600095E` | RVA `0x00304050` |
| `Player.GrappleMovement()` | `0x06000969` | RVA `0x0030939C` |
| `Player.DryCollision(bool, bool)` | `0x0600097E` | RVA `0x0030B598` |
| `Player.Update(int)` | `0x06000990` | horizontal `IL_4B5E`, mount reset `IL_4EB9`, hover `IL_550B`, flight fallback `IL_5543`, dry collision from `IL_8B25` |

Together, the item-default case and type-23 initializer establish:

- item `4444`, mount type `23`, buff `230`;
- `flightTimeMax = 320`, `fatigueMax = 320`, `usesHover = true`;
- `runSpeed = 9`, `dashSpeed = 9`, `acceleration = 0.16`;
- `jumpHeight = 10`, `jumpSpeed = 4`, `blockExtraJumps = true`;
- `heightBoost = 0`, `fallDamage = 0`.

The unset `MountData` booleans also matter: type 23 does not enable
`CanUseWings`, `MovementStatsAreAdditive`, or `dismountsOnItemUse`.

## Item presence is not an activation contract

Inventory recognition and active-mount recognition must remain separate:

1. `QuickMount_GetItemToUse()` chooses the non-cart mount in `miscEquips[3]`
   first, otherwise the first non-cart mount in inventory slots `0..57`.
   Merely finding item 4444 somewhere in the backpack is insufficient.
2. Before requesting an activation edge, the adapter must prove that the item
   actually returned by that native selector has `type == 4444` and
   `mountType == 23`.
3. After activation, the next observation must independently prove
   `mount.Active && mount.Type == 23`. The planner must not infer active type 23
   from the earlier inventory result.

`controlMount && releaseMount` is the native press edge. A held key does not
repeat; a key-up frame rearms `releaseMount`. The separate
`ItemUseStartEdgeReady` observation represents the successful
`ItemCheck_TryStartUse` gate; it must not be inferred from the mount-key edge.
Unmounted activation is rejected by native code for CC, tongue, inverse
gravity, death, `noItems`, insufficient
fit, a live grapple, or a failed item-use start. `SetMount(23, ...)` does not
zero player velocity and does not change the normal 20x42 hitbox because the
height boost is zero.

While mounted, another rearmed mount-key edge attempts to dismount. A normal
dismount requires `Collision.TryChangingSizeFromBottomCenter` to fit the 20x42
player. Repeating a no-space edge three times enters vanilla's forced-dismount
and no-space teleport path. That is not a combat escape primitive: the pure
contract returns `UnsafeDismountBlocked`, and production must not issue or
repeat that edge.

## Exact supported tick

`WitchBroomMotion.TryAdvanceOpenDryTick` mirrors the useful native order for a
restricted profile:

1. type 23 overrides horizontal speed/acceleration with `9 / 9 / 0.16` and,
   because it cannot use wings, sets horizontal slowdown to `0.2`;
2. `HorizontalMovement` updates X;
3. a grounded `Up && releaseUp` edge bootstraps Y to
   `-(0.16 + gravity + 0.001)`;
4. the model deliberately forbids `Jump`, so the separate ordinary-jump state
   machine cannot be confused with hover ascent;
5. the type-23 `Hover` branch updates Y;
6. an already-proven open, dry 20x42 sweep applies the resulting displacement.

A mid-air activation starts from Mount's reset standing frame while preserving
the incoming non-zero velocity. On that one entry tick, fatigue-ignoring
`Hover` leaves Y unchanged (except that an exact `-0.001` sentinel becomes
zero); the subsequent open-air `PlayerFrame` selects state 2. This entry phase
is part of the pure trajectory, so an activation candidate does not need to
hide an unmodeled tick between the key edge and normal hover.

For horizontal intent `h` and incoming `(vx, vy)`, the supported native subset
is:

```text
h < 0 and vx > -9:  if vx >  0.2, vx -= 0.2; vx -= 0.16
h > 0 and vx <  9:  if vx < -0.2, vx += 0.2; vx += 0.16
otherwise:          approach zero by 0.2 when vy == 0, else by 0.1
```

The last line is intentionally used even if the player is holding toward an
already-reached speed cap. It is an observable quirk of the native branch
order, not a conventional target-speed approximation.

In airborne frame state 2, type 23 ignores fatigue, so its target interval is:

```text
Up:      lower = -8,     upper = -0.001, pre-accelerate vy by -0.16
Down:    lower = -0.001, upper =  8,     pre-accelerate vy by +0.16
Neutral: lower = -0.001, upper = -0.001
```

After that pre-acceleration, native code approaches an exceeded bound by
`0.16`, snapping only when the remaining gap is strictly less than `0.16`.
When neutral convergence lands exactly on `-0.001`, `Hover` first adds `0.001`
to position Y; the subsequent open collision displacement of `-0.001` makes
the net Y displacement zero.

`OpenDryPath` is a one-tick proof, not a sticky mode. The result clears it, so a
trajectory caller must validate the complete swept 20x42 hitbox against the
current map again before every next step. The input and output velocity length
must also remain at or below 16 pixels/tick, which keeps Terraria's
`DryCollision` on its single-step path; faster external knockback/substep
motion is rejected. Wet/honey/shimmer movement, slopes,
platform decisions, conveyors, portals, wind push, track boost, forced motion,
CC, tongue, pulley, sliding, hooks and an in-progress dash all fail closed.
This is conservative by design; a future collision oracle can extend the
contract without weakening this profile.

If a change of vertical intent produces exactly `velocity.Y == 0`, native
`PlayerFrame` selects standing/running state 0/1 rather than assuming airborne
state 2. The pure result preserves that quirk and refuses to advance another
ordinary tick from it. A fresh native observation or a separately proved
ground-Up edge is required. Likewise, a tile collision or landing probe is not
guessed from velocity: that candidate branch fails closed.

## Optional equipment interactions

- **Fatigue:** `Player.Update` selects `Mount.Hover` before the finite-flight
  fallback. `DoesHoverIgnoresFatigue()` explicitly includes type 23, so hover
  neither consumes `_flyTime` nor accumulates fatigue. The broom's useful
  hover is therefore not limited to 320 ticks.
- **Featherfall potion:** `slowFall` only changes the fatigue ratio calculation
  inside `Hover`; that calculation has no motion effect when fatigue is
  ignored. Tests require identical broom trajectories with the flag on and
  off. After dismount, the ordinary featherfall contract applies again, so the
  return-to-baseline solver must use the player's real potion state.
- **Gravity potion:** native gravity flipping is gated by `!mount.Active`, and
  unmounted `QuickMount` rejects inverse gravity. Type 23 must never be combined
  with a gravity-flip candidate. If a return route needs a flip, safely
  dismount and model the flip as a later, separate edge.
- **Hooks:** `MountID.Sets.CanUseHooks[23]` is false. Mounting is blocked while
  grappling; `QuickGrapple` tries to dismount an active broom before firing.
  A hook in flight or attached hook is outside this contract.
- **Dash accessories:** `MountID.Sets.CanDash[23]` is false, so new accessory
  dashes are blocked. Unlike mount type 44, `SetMount(23)` does not clear an
  already-running dash. Activation therefore requires a proved idle dash
  state; no dash input may be mixed into a broom trajectory.
- **Wings/rocket boots:** type 23 cannot use wings, and the mounted update zeros
  rocket-boot and wing logic. They are not additive broom resources.
- **Weapons:** type 23 is not in `DontHoldItems` and does not dismount on item
  use. Weapon control can remain independent, but its recoil and use movement
  still have to be included in a later full candidate contract.

## Candidate trajectory and return closure

Possessing item 4444 must not perturb the low-mobility boss loop. The broom is
eligible only as a scored optional rescue candidate with the whole sequence
declared in advance:

```text
exact QuickMount item edge
  -> observe active type 23
  -> release mount key to rearm it
  -> N per-tick broom states, each with fresh map/threat clearance
  -> reach a proved 20x42 dismount volume
  -> issue one rearmed dismount edge
  -> re-enter a safe state in the existing low-mobility boss closure
```

Every intermediate state, not just the immediate dodge, must remain inside the
reviewed arena and threat horizon. The candidate is rejected if the predicted
route cannot reach a safe dismount volume with enough braking distance, if the
post-dismount featherfall/gravity/hook state is not modeled, or if any native
observation diverges. This prevents a locally attractive mount dodge from
stranding the player outside the stable boss loop.

A user cancellation still releases all synthetic movement controls
immediately. It must not be translated into an unconditional mount toggle:
only a fresh, known-clear dismount probe may authorize that extra edge. If the
probe is unknown or blocked, leaving the player mounted but uncontrolled is
safer than entering vanilla's repeated no-space/teleport recovery. A landing
or any collision likewise ends the predicted branch and requires a fresh
native observation before another broom action is considered.

`WitchBroomRescueTrajectory` now implements the allocation-free proof checker
for that complete sequence. Its caller supplies fixed-size, preallocated tick
arrays (at most 256 mounted and 256 ordinary-return ticks), not a collection of
"safe" booleans. In particular, the checker itself:

- resolves the exact item-4444/type-23 activation edge and then requires an
  independent native observation on the following tick;
- derives every required tile rectangle from the simulated 20x42 swept body,
  checks full tile-count coverage, rejects solids, half blocks, slopes,
  platforms, conveyors, liquids, unloaded cells and out-of-world cells, and
  rejects reused capture sequences or a changed world revision;
- binds numeric all-threat evidence to each simulated body. The analyzed count
  must equal the observed count, omissions must be zero, and both its horizon
  and time-to-impact must cover the remainder of the complete return closure;
- recomputes the fastest type-23 braking trajectory at the initial state and
  after every mounted tick. A route fails if the remaining tick budget cannot
  stop it or even the minimum-distance brake leaves the reviewed arena;
- derives `CanFitDismount` only after validating a stationary 20x42 clearance
  scan, replays a no-key update to prove `releaseMount` is rearmed, and resolves
  exactly one native dismount edge;
- carries the observed exit `slowFall` state through a separately simulated
  open-air ordinary return. Featherfall therefore uses the native Up/Down
  gravity branches; substituting the low-config no-potion fall speed fails;
- accepts only when both the dismount edge and final low-config re-entry lie in
  caller-declared numeric body/velocity envelopes. Local safety without an
  exit, braking room, or a return state is not a valid rescue.

The hot evaluator contains no allocation, search, reflection, engine call or
input write. Hooks, accessory dashes, gravity flips, jumps, wings, landings and
all other mixed motion remain separate trajectory candidates and fail closed
inside this broom certificate. That separation is important when optional
equipment is present: a potion or accessory can improve an emergency route,
but cannot silently change the baseline boss loop or invalidate its timing.

No production Boss input path uses these models yet. The motion, route-builder,
full-rescue and native-reader regression groups are compiled into the ordinary
test executable and currently contain 49 deterministic checks in total. They
cover stale/misaligned scans, omitted threats, insufficient braking, blocked
dismount, wrong featherfall return, missing low-config closure, selector/active
identity separation and a measured zero-allocation hot loop. Passing those
checks proves the bounded pure contracts, not that an actual Boss strategy is
authorized to press the mount key or drive type 23.
