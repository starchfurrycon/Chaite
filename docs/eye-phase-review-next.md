# Eye phase review — next iteration only

Status: read-only native IL review, recorded while the current Boss batch is running. This document does not change production code, tests, fixtures, or build outputs, and is not added to a publishing allowlist. It records proposed work, not a verified fix or a new battle-success claim.

## Evidence and snapshot timing

- Vanilla Terraria 1.4.5.8 x86, original executable SHA256: `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`.
- Eye behavior is inline in `Terraria.NPC.AI()`, `aiStyle == 4`, not a separate `AI_004` method.
- `InstallationService.PatchAssembly` places `Runtime.Tick` at the entry to `Player.Update(int)`. `Runtime.Tick` builds the combat snapshot before native player movement.
- Native `Main.DoUpdateInWorld_Inner`: `IL_004c` calls `UpdateWorld_Players`, `IL_00ad` calls `UpdateWorld_NPCs`, and `IL_00ed` calls `UpdateWorld_Projectiles`.
- Thus the observed NPC `Ai2` and velocity ordinarily describe the previous NPC update. The player's selected controls and movement happen before the next NPC AI update. Native damage or state changes between these stages are additional reasons not to treat a projected label as an observed fact.

Keep two concepts separate: the observed stage/velocity, and the next NPC movement branch or guaranteed future straight-motion interval. Do not globally add one to every observed timer or relabel every phase as a next-tick prediction.

## First-form transformation applies to every ai1 substage

The first-form branches join `NPC.AI IL_11a4..1209`, with no `ai1 == 0` restriction. Examples of predecessors are `0e99`, `0fcf`, `0fe1`, and `1135`. The threshold uses native r4 operations:

```text
(float)life < (float)lifeMax * (expert ? 0.65f : 0.5f)
```

On success, native sets `ai0 = 1`, clears `ai1`, `ai2`, and `ai3`, then returns. Preserve native operation order and its existing x86 threshold regression; do not replace this with a normalized life ratio.

Current `EyeStrategy.ObservePhase` only returns `TransformPending` for `ai0 == 0 && ai1 == 0`. It misses impending transformation during launch, charge, and brake.

### Safety consequence

Native executes the old substage's motion logic before checking the transformation threshold. In particular, an `ai1 == 1` snapshot still produces a new aimed launch velocity in the upcoming native update, even if that update subsequently sets `ai0 = 1`. Transformation does not retroactively cancel that movement or immediately apply spin-stage damping.

Do **not** merely remove `ai1 == 0` and let the current `TransformPending` recovery behavior apply to all substages. That would prematurely grant coasting or a return turn based on a new label while the old launch/charge remains relevant. The current missed charge label generally preserves the committed exit for one additional observation, which is conservative.

Suggested bounded correction after the frozen batch:

1. Compute impending transformation as an auxiliary flag across `ai0 == 0`, independent of the actual observed substage.
2. Preserve launch/charge movement commitment and charge-vector handling while those substages are observed. A pending flag must not itself authorize reversal or shorten the body-collision check.
3. Begin actual spin-stage recovery from observed `ai0 == 1/2`, still respecting actual velocity and the verified return corridor.
4. If pending information is displayed, use an additional reason/status field or suffix; do not overload the movement-authority phase without explicit tests.

Regression proposals: low life during each `ai1 == 0/1/2`, including early charge and brake; pending launch with stale observed velocity; pending must not reverse a committed charge. Preserve all existing native r4 threshold boundary tests.

## Ordinary dash timers: strict forward straight interval

Native increments the timer before deciding whether to damp velocity:

- First form: `0fe6..100d` increments and compares against 40; `1012..1040` applies `0.98`, and expert additionally `0.985`.
- Second-form ordinary dash: `1e10..1e42` increments and compares against 40 classic / 50 expert; `1e47..1e75` applies `0.97`, and expert additionally `0.98`.
- Master shares the native expert check. Secret-seed adjustments must not be inferred into an unverified movement template.

For an observed ordinary-charge timer `Ai2`, a future update `k >= 1` remains undamped only while `Ai2 + k < T`. For normal integral native timers, the guaranteed future undamped count is:

```text
max(0, T - 1 - Ai2)
```

The current `CanStartGroundHop` horizon uses `T - Ai2` and can therefore extrapolate one first-damped movement step at the full observed velocity. Keep its existing 24-tick cap; the correction is the strict timer boundary, not a changed safety constant, equipment level, or arena size.

| Ordinary branch | T | Snapshot whose next update first damps |
| --- | ---: | ---: |
| First form | 40 | 39 |
| Second form, classic | 40 | 39 |
| Second form, expert/master | 50 | 49 |

Current observed labels (`Ai2 < T`) can truthfully describe the previously executed NPC update. They should not be silently treated as a proof that the upcoming movement remains undamped. Correct the forward horizon independently unless a separately tested next-motion API is introduced.

For malformed/nonintegral externally supplied timers, define behavior explicitly rather than assuming that a cast implements the strict inequality generally. Ordinary native snapshots use integral timer steps.

Regression proposals: `T-2` leaves one guaranteed straight update, `T-1` leaves none; preserve the behavior of earlier timers and the 24-tick cap. Native-step evidence should observe the first damping update, not only assert a synthetic label.

## Fast dash near-player extension is an exact conditional

Native `ai0 == 3 && ai1 == 4`, `2677..274a`:

```text
T = expert && life < lifeMax * 0.04 (r8) ? 10 : 20
next = Ai2 + 1
if next == T && Vector2.Distance(npc.position, targetPlayer.position) < 200:
    next -= 1
if next >= T:
    velocity *= 0.95
```

Evidence: `26c7..26e0` increment; `26e1..26eb` exact equality to T; `26ed..270e` position-distance comparison; `2710..2729` decrement; `272a..274a` damping. Low-health threshold and T are set at `0450..0484` using r8 for the 4% life check.

Important details:

- Distance is between entity **top-left positions**, not centers or hitbox-edge distance.
- It is strictly `< 200`; exactly 200 does not extend the dash.
- The condition is `next == T`, not `next >= T`.
- The target player has already moved during the upcoming world update. Testing snapshot distance without accounting for that movement is not an exact next-step oracle.
- The native target player may differ from the local player in multiplayer. The current single-player batch does not establish correctness for that case.
- If the life threshold changes T from 20 to 10 while the timer is already past the new threshold, proximity does not hold the timer; the equality test fails and damping begins.

An observed `Ai2 == T-1` can persist for many ticks while this condition holds. Current `FastCharge` labeling correctly avoids prematurely declaring recovery in that case. If the player moves out of range, the next update damps; keeping the observed charge label for one further observation is conservative. Blindly replacing the check with `Ai2 + 1 < T` would incorrectly permit recovery during an actual near-player extension.

For the hop preview, do not assume an unobserved future extension. A strict guaranteed window can stop before the conditional boundary. If a later implementation wishes to include the conditional tick, it needs a separately verified prediction using the native target's position after player movement; it must not infer repeated future extensions from the present held timer.

Regression proposals: T-1 at distances below/equal/above 200 using deliberately different top-left and center distances; player movement crossing the boundary; repeated held T-1; T already exceeded; and life crossing the 4% threshold while a dash is in progress.

## Next-iteration scope

Finish the current frozen batch first. Prefer the minimal strict ordinary-hop-window fix and its tests. Treat pending-transform diagnostics and any next-motion helper as separate changes with their own regressions. No evidence here justifies changing arena dimensions, equipment, damage, Boss AI, player stats, or difficulty to obtain a different outcome.
