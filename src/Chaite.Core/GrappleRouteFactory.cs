using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Read-only world view used by the cold grapple proof builder. Implementations
    /// must report the exact tile state from one synchronous game snapshot.
    /// </summary>
    public interface IBasicHookWorldEvidenceSource
    {
        int MaxTilesX { get; }
        int MaxTilesY { get; }
        bool TryReadTile(int tileX, int tileY, out BasicHookAnchorObservation tile);
        bool TrySweepPlayer(in RectF before, in RectF after,
            out bool pathClear, out bool platformFree);
    }

    public struct BasicHookRouteBuffers
    {
        public BasicHookTrajectoryTick[] SuccessTicks;
        public BasicHookTileSample[] SuccessTileSamples;
        public BasicHookTrajectoryTick[] MissTicks;
        public BasicHookTileSample[] MissTileSamples;
    }

    /// <summary>
    /// Inputs for the deliberately narrow ordinary-hook route. All candidate
    /// anchors have already been read from the same world snapshot as the player.
    /// The generated route holds neutral left/right/up/down/jump inputs except for
    /// the single native jump edge which detaches the hook.
    /// </summary>
    public struct BasicHookRouteBuildRequest
    {
        public bool Known;
        public BasicHookIdentity Identity;
        public BasicHookUseContext Context;
        public Vec2 FireCenter;
        public Vec2 PlayerVelocity;
        public bool PlayerGrounded;
        public int PlayerWidth;
        public int PlayerHeight;
        public float Gravity;
        public float MaximumFallSpeed;
        public float HorizontalSlowdown;
        public float JumpSpeed;
        public int JumpHeight;
        public bool OldStyleParkour;
        public Vec2 LowConfigLoopCenter;
        public float LoopHalfWidth;
        public float LoopHalfHeight;
        public float LoopMaximumAbsVelocityX;
        public float LoopMaximumAbsVelocityY;
        public int LowConfigLoopEpoch;
        public float MaximumAcceptedRiskScore;
        public float PlayerSafetyMargin;
        public IList<ThreatSnapshot> Threats;
        public BasicHookAnchorObservation[] CandidateAnchors;
        public int CandidateAnchorCount;
    }

    /// <summary>
    /// Bounded, allocation-free (given caller buffers) construction of both the
    /// intended latch path and the no-latch return contingency. The factory never
    /// reads or writes Terraria state; the plugin adapter owns the snapshot.
    /// </summary>
    public static class BasicHookRouteFactory
    {
        public const int RequiredSuccessTickCapacity = 256;
        public const int RequiredMissTickCapacity = 256;
        public const int RequiredTileSampleCapacity = 4096;
        private const int PullTicksBeforeDetach = 4;
        private const int MinimumReturnTicks = 4;

        public static bool TryBuild(in BasicHookRouteBuildRequest request,
            IBasicHookWorldEvidenceSource world, in BasicHookRouteBuffers buffers,
            out BasicHookRouteCertificate certificate, out BasicHookVerifiedRoute proof,
            out BasicHookFailure failure)
        {
            certificate = default(BasicHookRouteCertificate);
            proof = default(BasicHookVerifiedRoute);
            failure = BasicHookFailure.UnsafeRoute;
            if (!ValidateRequest(in request, world, in buffers)) return false;

            for (var anchorIndex = 0; anchorIndex < request.CandidateAnchorCount; anchorIndex++)
            {
                var anchor = request.CandidateAnchors[anchorIndex];
                if (!BasicHookMotion.IsSafetyAnchor(in anchor)) continue;
                var dx = anchor.HookCenter.X - request.FireCenter.X;
                var dy = anchor.HookCenter.Y - request.FireCenter.Y;
                var distanceSquared = (double)dx * dx + (double)dy * dy;
                // The first production profile only pulls upward and keeps ample
                // distance for four exact 11 px attached ticks plus detachment.
                if (dy >= -32f || distanceSquared < 96d * 96d ||
                    distanceSquared > 260d * 260d) continue;

                if (!TryBuildForAnchor(in request, world, in buffers, in anchor,
                        out certificate)) continue;
                if (BasicHookMotion.TryVerifyRoute(in certificate, request.Context.SlowFall,
                        out proof, out failure)) return true;
            }
            certificate = default(BasicHookRouteCertificate);
            proof = default(BasicHookVerifiedRoute);
            failure = BasicHookFailure.UnsafeRoute;
            return false;
        }

        /// <summary>
        /// Re-scores the still-relevant absolute player corridor against a fresh
        /// threat snapshot. Threat positions are at elapsedTicks; offsets passed
        /// to BoundsAt/BeamGeometry are therefore relative to the current frame.
        /// The caller chooses both branches until native projectile state proves
        /// latch or miss, then may validate only that observed branch.
        /// </summary>
        public static bool TryScoreRemainingThreats(in BasicHookRouteCertificate route,
            int elapsedTicks, IList<ThreatSnapshot> threats, int playerWidth,
            int playerHeight, float playerSafetyMargin, bool successBranch,
            bool missBranch, out float worstRisk)
        {
            worstRisk = 0f;
            if (!route.Known || elapsedTicks < 0 || threats == null ||
                playerWidth <= 0 || playerWidth > 128 || playerHeight <= 0 ||
                playerHeight > 192 || !IsFiniteNonNegative(playerSafetyMargin) ||
                !successBranch && !missBranch || route.Evidence.SuccessTicks == null ||
                route.Evidence.MissTicks == null || route.Evidence.SuccessTickCount < 1 ||
                route.Evidence.SuccessTickCount > route.Evidence.SuccessTicks.Length ||
                route.Evidence.MissTickCount < 1 ||
                route.Evidence.MissTickCount > route.Evidence.MissTicks.Length)
                return false;
            if (successBranch && !ScoreRemainingBranch(route.Evidence.SuccessTicks,
                    route.Evidence.SuccessTickCount, elapsedTicks, threats, playerWidth,
                    playerHeight, playerSafetyMargin, ref worstRisk)) return false;
            if (missBranch && !ScoreRemainingBranch(route.Evidence.MissTicks,
                    route.Evidence.MissTickCount, elapsedTicks, threats, playerWidth,
                    playerHeight, playerSafetyMargin, ref worstRisk)) return false;
            return IsFiniteNonNegative(worstRisk);
        }

        private static bool ScoreRemainingBranch(BasicHookTrajectoryTick[] ticks,
            int count, int elapsedTicks, IList<ThreatSnapshot> threats, int playerWidth,
            int playerHeight, float margin, ref float worstRisk)
        {
            for (var index = 0; index < count; index++)
            {
                var tick = ticks[index];
                var absoluteAfterTick = tick.Tick + 1;
                if (!tick.Known || !IsFinite(tick.PlayerCenterBefore) ||
                    !IsFinite(tick.PlayerCenterAfter)) return false;
                if (absoluteAfterTick < elapsedTicks) continue;
                var future = absoluteAfterTick - elapsedTicks;
                var before = new RectF(tick.PlayerCenterBefore.X - playerWidth * .5f,
                    tick.PlayerCenterBefore.Y - playerHeight * .5f,
                    playerWidth, playerHeight).Inflated(margin);
                var after = new RectF(tick.PlayerCenterAfter.X - playerWidth * .5f,
                    tick.PlayerCenterAfter.Y - playerHeight * .5f,
                    playerWidth, playerHeight).Inflated(margin);
                var risk = 0f;
                for (var threatIndex = 0; threatIndex < threats.Count; threatIndex++)
                {
                    var threat = threats[threatIndex];
                    if (!FiniteThreat(threat)) return false;
                    if (EmpressLanceGeometry.RequiresSafetyModel(threat) &&
                        !EmpressLanceGeometry.ValidSnapshot(threat)) return false;
                    var penalty = 1000f + Math.Max(1, threat.Damage) * 90f;
                    if (threat.Geometry == ThreatGeometry.Body)
                    {
                        if (threat.TimeLeft > 0 && threat.TimeLeft < future) continue;
                        RectF threatBefore;
                        RectF threatAfter;
                        if (threat.Trajectory != ThreatTrajectory.Linear)
                        {
                            ProjectileMotionSweep motion;
                            if (!HostileProjectileMotion.TrySweep(threat,
                                    Math.Max(0, future - 1), future,
                                    out motion)) return false;
                            if (!motion.Active) continue;
                            threatBefore = threatAfter = motion.Bounds;
                        }
                        else
                        {
                            threatBefore = threat.BoundsAt(
                                Math.Max(0, future - 1));
                            threatAfter = threat.BoundsAt(future);
                        }
                        if (SweptIntersects(in before, in after,
                                in threatBefore, in threatAfter)) risk += penalty;
                    }
                    else
                    {
                        var beam = BeamGeometry.Sweep(threat,
                            Math.Max(0, future - 1), future);
                        if (BeamGeometry.Intersects(Union(in before, in after),
                                beam, margin)) risk += penalty;
                    }
                    if (!IsFinite(risk)) return false;
                }
                if (risk > worstRisk) worstRisk = risk;
            }
            return true;
        }

        private static bool TryBuildForAnchor(in BasicHookRouteBuildRequest request,
            IBasicHookWorldEvidenceSource world, in BasicHookRouteBuffers buffers,
            in BasicHookAnchorObservation anchor, out BasicHookRouteCertificate route)
        {
            route = new BasicHookRouteCertificate
            {
                Known = true,
                FireCenter = request.FireCenter,
                IntendedAnchorCenter = anchor.HookCenter,
                IntendedAnchor = anchor,
                LowConfigLoopCenter = request.LowConfigLoopCenter,
                ReturnProfileSlowFall = request.Context.SlowFall,
                MaximumAcceptedRiskScore = request.MaximumAcceptedRiskScore,
                Gravity = request.Gravity,
                MaximumFallSpeed = request.MaximumFallSpeed,
                HorizontalSlowdown = request.HorizontalSlowdown,
                JumpSpeed = request.JumpSpeed,
                JumpHeight = request.JumpHeight,
                OldStyleParkour = request.OldStyleParkour,
                AnchorTolerance = .1f,
                DetachRadius = .25f,
                LoopHalfWidth = request.LoopHalfWidth,
                LoopHalfHeight = request.LoopHalfHeight,
                LoopMaximumAbsVelocityX = request.LoopMaximumAbsVelocityX,
                LoopMaximumAbsVelocityY = request.LoopMaximumAbsVelocityY,
                LowConfigLoopEpoch = request.LowConfigLoopEpoch,
                LatchDeadlineTicks = BasicHookMotion.MaximumLatchObservationTicks,
                DetachDeadlineTicks = 120,
                ReentryDeadlineTicks = 240
            };

            Vec2 launch;
            if (!BasicHookMotion.TryGetLaunchVelocity(in request.Identity, request.FireCenter,
                    anchor.HookCenter, out launch)) return false;

            var successCount = 0;
            var successTileCount = 0;
            var player = request.FireCenter;
            var velocity = request.PlayerVelocity;
            var grounded = request.PlayerGrounded;
            var projectile = request.FireCenter;
            var releaseJump = true;
            var latched = false;
            for (var tickIndex = 0; tickIndex < BasicHookMotion.MaximumLatchObservationTicks;
                 tickIndex++)
            {
                Vec2 afterPlayerVelocity;
                Vec2 afterPlayer;
                if (!TryAdvanceNeutralPlayer(in request, velocity, grounded,
                        out afterPlayerVelocity) || !TryAdd(player, afterPlayerVelocity,
                        out afterPlayer)) return false;
                var tick = NewTick(tickIndex, BasicHookEvidencePhase.Outbound,
                    player, velocity, afterPlayer, afterPlayerVelocity, grounded,
                    projectile, projectile, true, true, 0f, 0f, releaseJump, true);
                BasicHookAnchorObservation firstLatch;
                if (!AppendNativeScan(world, in route, ref tick,
                        buffers.SuccessTileSamples, ref successTileCount,
                        false, out firstLatch)) return false;
                var foundLatch = firstLatch.Known;
                if (foundLatch)
                {
                    if (firstLatch.TileX != anchor.TileX || firstLatch.TileY != anchor.TileY ||
                        firstLatch.TileType != anchor.TileType ||
                        !NearlySame(firstLatch.HookCenter, anchor.HookCenter, .1f)) return false;
                    tick.Phase = BasicHookEvidencePhase.Latch;
                    tick.ProjectileAiAfter = 2f;
                    tick.ProjectileCenterAfter = anchor.HookCenter;
                    latched = true;
                }
                else
                {
                    double playerDistance;
                    if (!TryDistance(projectile, player, out playerDistance) ||
                        playerDistance > BasicHookMotion.OutboundRange ||
                        !TryAdd(projectile, launch, out tick.ProjectileCenterAfter)) return false;
                }
                if (!FinishCommonTick(in request, world, ref tick)) return false;
                if (successCount >= buffers.SuccessTicks.Length) return false;
                buffers.SuccessTicks[successCount++] = tick;
                player = afterPlayer;
                velocity = afterPlayerVelocity;
                projectile = tick.ProjectileCenterAfter;
                releaseJump = true;
                if (latched) break;
            }
            if (!latched) return false;

            for (var pullIndex = 0; pullIndex < PullTicksBeforeDetach; pullIndex++)
            {
                var tickIndex = successCount;
                var projectileObservation = ProjectileAt(anchor.HookCenter, 2f);
                var link = AttachedLink();
                BasicHookAttachedTick attached;
                if (!BasicHookMotion.TryApplyAttachedTick(in projectileObservation, in link,
                        in anchor, 0, player, velocity, false, request.JumpSpeed,
                        request.JumpHeight, request.OldStyleParkour, false, false,
                        false, releaseJump, out attached) || attached.Detached ||
                    Math.Abs(Length(attached.PullVelocity) - BasicHookMotion.PullSpeed) > .01f)
                    return false;
                Vec2 afterPlayer;
                if (!TryAdd(player, attached.VelocityAfterGrapple, out afterPlayer)) return false;
                var tick = NewTick(tickIndex, BasicHookEvidencePhase.Pull,
                    player, velocity, afterPlayer, attached.VelocityAfterGrapple, false,
                    anchor.HookCenter, anchor.HookCenter, true, true, 2f, 2f,
                    releaseJump, attached.ReleaseJumpAfter);
                if (!FinishCommonTick(in request, world, ref tick) ||
                    successCount >= buffers.SuccessTicks.Length) return false;
                buffers.SuccessTicks[successCount++] = tick;
                player = afterPlayer;
                velocity = attached.VelocityAfterGrapple;
                releaseJump = attached.ReleaseJumpAfter;
            }

            route.DetachCenter = player;
            {
                var projectileObservation = ProjectileAt(anchor.HookCenter, 2f);
                var link = AttachedLink();
                BasicHookAttachedTick attached;
                if (!releaseJump || !BasicHookMotion.TryApplyAttachedTick(
                        in projectileObservation, in link, in anchor, 0, player, velocity,
                        false, request.JumpSpeed, request.JumpHeight,
                        request.OldStyleParkour, true, false, false, releaseJump,
                        out attached) || !attached.Detached) return false;
                Vec2 afterPlayer;
                if (!TryAdd(player, attached.VelocityAfterGrapple, out afterPlayer)) return false;
                var tick = NewTick(successCount, BasicHookEvidencePhase.DetachPulse,
                    player, velocity, afterPlayer, attached.VelocityAfterGrapple, false,
                    anchor.HookCenter, anchor.HookCenter, true, false, 2f, 2f,
                    releaseJump, attached.ReleaseJumpAfter);
                tick.ControlJump = true;
                if (!FinishCommonTick(in request, world, ref tick) ||
                    successCount >= buffers.SuccessTicks.Length) return false;
                buffers.SuccessTicks[successCount++] = tick;
                player = afterPlayer;
                velocity = attached.VelocityAfterGrapple;
                releaseJump = attached.ReleaseJumpAfter;
            }

            var returned = false;
            for (var returnIndex = 0; returnIndex < 180; returnIndex++)
            {
                Vec2 afterVelocity;
                Vec2 afterPlayer;
                if (!TryAdvanceNeutralPlayer(in request, velocity, false,
                        out afterVelocity) || !TryAdd(player, afterVelocity,
                        out afterPlayer)) return false;
                var tick = NewTick(successCount,
                    request.Context.SlowFall ? BasicHookEvidencePhase.ReturnFeatherFall :
                        BasicHookEvidencePhase.ReturnBallistic,
                    player, velocity, afterPlayer, afterVelocity, false,
                    anchor.HookCenter, anchor.HookCenter, false, false, 2f, 2f,
                    releaseJump, true);
                if (!FinishCommonTick(in request, world, ref tick) ||
                    successCount >= buffers.SuccessTicks.Length) return false;
                buffers.SuccessTicks[successCount++] = tick;
                player = afterPlayer;
                velocity = afterVelocity;
                releaseJump = true;
                if (returnIndex + 1 >= MinimumReturnTicks && InLoop(in request, player, velocity))
                {
                    returned = true;
                    break;
                }
            }
            if (!returned) return false;

            var missCount = 0;
            var missTileCount = 0;
            player = request.FireCenter;
            velocity = request.PlayerVelocity;
            grounded = request.PlayerGrounded;
            projectile = request.FireCenter;
            releaseJump = true;
            var transitioned = false;
            for (var tickIndex = 0; tickIndex < 80; tickIndex++)
            {
                Vec2 afterVelocity;
                Vec2 afterPlayer;
                if (!TryAdvanceNeutralPlayer(in request, velocity, grounded,
                        out afterVelocity) || !TryAdd(player, afterVelocity,
                        out afterPlayer)) return false;
                double playerDistance;
                if (!TryDistance(projectile, player, out playerDistance)) return false;
                var phase = playerDistance > BasicHookMotion.OutboundRange
                    ? BasicHookEvidencePhase.OutboundRangeTransition
                    : BasicHookEvidencePhase.Outbound;
                var tick = NewTick(tickIndex, phase, player, velocity,
                    afterPlayer, afterVelocity, grounded, projectile, projectile,
                    true, true, 0f, phase == BasicHookEvidencePhase.Outbound ? 0f : 1f,
                    releaseJump, true);
                BasicHookAnchorObservation unexpected;
                if (!AppendNativeScan(world, in route, ref tick, buffers.MissTileSamples,
                        ref missTileCount, true, out unexpected) || unexpected.Known ||
                    !TryAdd(projectile, launch, out tick.ProjectileCenterAfter) ||
                    !FinishCommonTick(in request, world, ref tick) ||
                    missCount >= buffers.MissTicks.Length) return false;
                buffers.MissTicks[missCount++] = tick;
                player = afterPlayer;
                velocity = afterVelocity;
                projectile = tick.ProjectileCenterAfter;
                releaseJump = true;
                if (phase == BasicHookEvidencePhase.OutboundRangeTransition)
                {
                    transitioned = true;
                    break;
                }
            }
            if (!transitioned) return false;

            var missKilled = false;
            for (var returnIndex = 0; returnIndex < 120; returnIndex++)
            {
                Vec2 afterVelocity;
                Vec2 afterPlayer;
                if (!TryAdvanceNeutralPlayer(in request, velocity, grounded,
                        out afterVelocity) || !TryAdd(player, afterVelocity,
                        out afterPlayer)) return false;
                double distance;
                if (!TryDistance(projectile, player, out distance)) return false;
                var killed = distance < BasicHookMotion.ReturnKillDistance;
                var afterProjectile = projectile;
                if (!killed)
                {
                    Vec2 returnVelocity;
                    if (!TryVector(player, projectile, BasicHookMotion.ReturnSpeed,
                            out returnVelocity) || !TryAdd(projectile, returnVelocity,
                            out afterProjectile)) return false;
                }
                var tick = NewTick(missCount, BasicHookEvidencePhase.MissReturn,
                    player, velocity, afterPlayer, afterVelocity, grounded,
                    projectile, afterProjectile, true, !killed, 1f, 1f,
                    releaseJump, true);
                if (!FinishCommonTick(in request, world, ref tick) ||
                    missCount >= buffers.MissTicks.Length) return false;
                buffers.MissTicks[missCount++] = tick;
                player = afterPlayer;
                velocity = afterVelocity;
                projectile = afterProjectile;
                releaseJump = true;
                if (killed)
                {
                    missKilled = true;
                    break;
                }
            }
            if (!missKilled || !InLoop(in request, player, velocity)) return false;

            route.Evidence = new BasicHookTrajectoryEvidence
            {
                Known = true,
                WorldMaxTilesX = world.MaxTilesX,
                WorldMaxTilesY = world.MaxTilesY,
                SuccessTicks = buffers.SuccessTicks,
                SuccessTickCount = successCount,
                SuccessTileSamples = buffers.SuccessTileSamples,
                SuccessTileSampleCount = successTileCount,
                MissTicks = buffers.MissTicks,
                MissTickCount = missCount,
                MissTileSamples = buffers.MissTileSamples,
                MissTileSampleCount = missTileCount
            };
            return true;
        }

        private static bool AppendNativeScan(IBasicHookWorldEvidenceSource world,
            in BasicHookRouteCertificate route, ref BasicHookTrajectoryTick tick,
            BasicHookTileSample[] samples, ref int sampleCount, bool hideIntendedAnchor,
            out BasicHookAnchorObservation firstLatch)
        {
            firstLatch = default(BasicHookAnchorObservation);
            tick.TileSampleOffset = sampleCount;
            var startX = (int)((tick.ProjectileCenterBefore.X - 21f) / 16f);
            var endX = (int)((tick.ProjectileCenterBefore.X + 37f) / 16f);
            var startY = (int)((tick.ProjectileCenterBefore.Y - 21f) / 16f);
            var endY = (int)((tick.ProjectileCenterBefore.Y + 37f) / 16f);
            if (startX < 0) startX = 0;
            if (startY < 0) startY = 0;
            if (endX > world.MaxTilesX) endX = world.MaxTilesX;
            if (endY > world.MaxTilesY) endY = world.MaxTilesY;
            for (var x = startX; x < endX; x++)
            for (var y = startY; y < endY; y++)
            {
                BasicHookAnchorObservation tile;
                if (!world.TryReadTile(x, y, out tile) || sampleCount >= samples.Length)
                    return false;
                if (hideIntendedAnchor && x == route.IntendedAnchor.TileX &&
                    y == route.IntendedAnchor.TileY)
                {
                    tile.NativeActive = false;
                    tile.NativeSolid = false;
                }
                samples[sampleCount++].Tile = tile;
                var left = x * 16d;
                var top = y * 16d;
                var overlaps = tick.ProjectileCenterBefore.X + 5d > left &&
                    tick.ProjectileCenterBefore.X - 5d < left + 16d &&
                    tick.ProjectileCenterBefore.Y + 5d > top &&
                    tick.ProjectileCenterBefore.Y - 5d < top + 16d;
                if (!overlaps || !BasicHookMotion.NativeCanLatch(in tile)) continue;
                firstLatch = tile;
                tick.TileSampleCount = sampleCount - tick.TileSampleOffset;
                return true;
            }
            tick.TileSampleCount = sampleCount - tick.TileSampleOffset;
            return true;
        }

        private static bool FinishCommonTick(in BasicHookRouteBuildRequest request,
            IBasicHookWorldEvidenceSource world, ref BasicHookTrajectoryTick tick)
        {
            var before = PlayerBounds(in request, tick.PlayerCenterBefore);
            var after = PlayerBounds(in request, tick.PlayerCenterAfter);
            bool clear;
            bool platforms;
            if (!world.TrySweepPlayer(in before, in after, out clear, out platforms))
                return false;
            tick.PlayerPathKnown = true;
            tick.PlayerPathClear = clear;
            tick.PlatformFree = platforms;
            tick.SupportStateKnown = true;
            float risk;
            if (!TryScoreThreats(in request, in before, in after, tick.Tick + 1,
                    out risk)) return false;
            tick.ThreatScoreKnown = true;
            tick.RiskScore = risk;
            return clear && platforms && risk <= request.MaximumAcceptedRiskScore;
        }

        private static bool TryScoreThreats(in BasicHookRouteBuildRequest request,
            in RectF before, in RectF after, int tick, out float risk)
        {
            risk = 0f;
            var threats = request.Threats;
            if (threats == null) return false;
            for (var index = 0; index < threats.Count; index++)
            {
                var threat = threats[index];
                if (!FiniteThreat(threat)) return false;
                if (EmpressLanceGeometry.RequiresSafetyModel(threat) &&
                    !EmpressLanceGeometry.ValidSnapshot(threat)) return false;
                var penalty = 1000f + Math.Max(1, threat.Damage) * 90f;
                var playerBefore = before.Inflated(request.PlayerSafetyMargin);
                var playerAfter = after.Inflated(request.PlayerSafetyMargin);
                if (threat.Geometry == ThreatGeometry.Body)
                {
                    if (threat.TimeLeft > 0 && threat.TimeLeft < tick) continue;
                    RectF threatBefore;
                    RectF threatAfter;
                    if (threat.Trajectory != ThreatTrajectory.Linear)
                    {
                        ProjectileMotionSweep motion;
                        if (!HostileProjectileMotion.TrySweep(threat,
                                Math.Max(0, tick - 1), tick, out motion))
                            return false;
                        if (!motion.Active) continue;
                        threatBefore = threatAfter = motion.Bounds;
                    }
                    else
                    {
                        threatBefore = threat.BoundsAt(Math.Max(0, tick - 1));
                        threatAfter = threat.BoundsAt(tick);
                    }
                    if (SweptIntersects(in playerBefore, in playerAfter,
                            in threatBefore, in threatAfter)) risk += penalty;
                }
                else
                {
                    var beam = BeamGeometry.Sweep(threat, Math.Max(0, tick - 1), tick);
                    if (BeamGeometry.Intersects(Union(in playerBefore, in playerAfter),
                            beam, request.PlayerSafetyMargin)) risk += penalty;
                }
                if (!IsFinite(risk)) return false;
            }
            return true;
        }

        private static bool TryAdvanceNeutralPlayer(in BasicHookRouteBuildRequest request,
            Vec2 before, bool grounded, out Vec2 after)
        {
            after = before;
            var drag = request.HorizontalSlowdown * (grounded ? 1f : .5f);
            if (!IsFinite(before) || !IsFiniteNonNegative(drag)) return false;
            if (Math.Abs(after.X) <= drag) after.X = 0f;
            else after.X -= Math.Sign(after.X) * drag;
            if (grounded)
            {
                after.Y = 0f;
                return IsFinite(after);
            }
            var divisor = request.Context.SlowFall ? 3f : 1f;
            after.Y = Math.Min(request.MaximumFallSpeed,
                after.Y + request.Gravity / divisor);
            if (request.Context.SlowFall && after.Y > request.MaximumFallSpeed / 3f)
                after.Y = request.MaximumFallSpeed / 3f;
            return IsFinite(after);
        }

        private static BasicHookTrajectoryTick NewTick(int index,
            BasicHookEvidencePhase phase, Vec2 playerBefore, Vec2 velocityBefore,
            Vec2 playerAfter, Vec2 velocityAfter, bool grounded,
            Vec2 projectileBefore, Vec2 projectileAfter, bool projectileActiveBefore,
            bool projectileActiveAfter, float aiBefore, float aiAfter,
            bool releaseBefore, bool releaseAfter)
        {
            return new BasicHookTrajectoryTick
            {
                Known = true,
                Tick = index,
                Phase = phase,
                PlayerCenterBefore = playerBefore,
                PlayerVelocityBefore = velocityBefore,
                PlayerCenterAfter = playerAfter,
                PlayerVelocityAfter = velocityAfter,
                Grounded = grounded,
                ProjectileCenterBefore = projectileBefore,
                ProjectileCenterAfter = projectileAfter,
                ProjectileActiveBefore = projectileActiveBefore,
                ProjectileActiveAfter = projectileActiveAfter,
                ProjectileAiBefore = aiBefore,
                ProjectileAiAfter = aiAfter,
                ReleaseJumpBefore = releaseBefore,
                ReleaseJumpAfter = releaseAfter
            };
        }

        private static BasicHookProjectileObservation ProjectileAt(Vec2 center, float ai)
            => new BasicHookProjectileObservation
            {
                Known = true,
                Active = true,
                Index = 1,
                Owner = 0,
                Type = BasicHookMotion.ProjectileType,
                AiStyle = BasicHookMotion.ProjectileAiStyle,
                AiState = ai,
                Center = center
            };

        private static BasicHookLinkObservation AttachedLink()
            => new BasicHookLinkObservation
            {
                Known = true,
                AtGrappleMovementEntry = true,
                GrappleCount = 1,
                FirstProjectileIndex = 1
            };

        private static bool ValidateRequest(in BasicHookRouteBuildRequest request,
            IBasicHookWorldEvidenceSource world, in BasicHookRouteBuffers buffers)
        {
            return request.Known && world != null &&
                BasicHookMotion.MatchesExactIdentity(in request.Identity) &&
                request.Context.Known && request.Context.LocalPlayerKnown &&
                request.Context.LocalPlayerIndex >= 0 && request.Context.NormalGravity &&
                !request.Context.GravityControlActive && !request.Context.MountActive &&
                !request.Context.Pulley && !request.Context.Wet &&
                IsFinite(request.FireCenter) && IsFinite(request.PlayerVelocity) &&
                request.PlayerWidth > 0 && request.PlayerWidth <= 128 &&
                request.PlayerHeight > 0 && request.PlayerHeight <= 192 &&
                IsFiniteNonNegative(request.Gravity) &&
                IsFinitePositive(request.MaximumFallSpeed) &&
                IsFiniteNonNegative(request.HorizontalSlowdown) &&
                IsFinitePositive(request.JumpSpeed) && request.JumpHeight >= 0 &&
                IsFinite(request.LowConfigLoopCenter) &&
                IsFinitePositive(request.LoopHalfWidth) &&
                IsFinitePositive(request.LoopHalfHeight) &&
                IsFiniteNonNegative(request.LoopMaximumAbsVelocityX) &&
                IsFiniteNonNegative(request.LoopMaximumAbsVelocityY) &&
                request.LowConfigLoopEpoch >= 0 &&
                IsFiniteNonNegative(request.MaximumAcceptedRiskScore) &&
                IsFiniteNonNegative(request.PlayerSafetyMargin) &&
                request.Threats != null && request.CandidateAnchors != null &&
                request.CandidateAnchorCount > 0 &&
                request.CandidateAnchorCount <= request.CandidateAnchors.Length &&
                world.MaxTilesX > 0 && world.MaxTilesY > 0 &&
                buffers.SuccessTicks != null &&
                buffers.SuccessTicks.Length >= RequiredSuccessTickCapacity &&
                buffers.MissTicks != null &&
                buffers.MissTicks.Length >= RequiredMissTickCapacity &&
                buffers.SuccessTileSamples != null &&
                buffers.SuccessTileSamples.Length >= RequiredTileSampleCapacity &&
                buffers.MissTileSamples != null &&
                buffers.MissTileSamples.Length >= RequiredTileSampleCapacity;
        }

        private static RectF PlayerBounds(in BasicHookRouteBuildRequest request,
            Vec2 center) => new RectF(center.X - request.PlayerWidth * .5f,
                center.Y - request.PlayerHeight * .5f,
                request.PlayerWidth, request.PlayerHeight);

        private static bool InLoop(in BasicHookRouteBuildRequest request,
            Vec2 position, Vec2 velocity)
        {
            return Math.Abs((double)position.X - request.LowConfigLoopCenter.X) <= request.LoopHalfWidth &&
                Math.Abs((double)position.Y - request.LowConfigLoopCenter.Y) <= request.LoopHalfHeight &&
                Math.Abs((double)velocity.X) <= request.LoopMaximumAbsVelocityX &&
                Math.Abs((double)velocity.Y) <= request.LoopMaximumAbsVelocityY;
        }

        private static bool FiniteThreat(ThreatSnapshot threat)
        {
            return IsFinite(threat.Position) && IsFinite(threat.Velocity) &&
                threat.Width >= 0 && threat.Height >= 0 && IsFinite(threat.BeamOrigin) &&
                IsFinite(threat.BeamDirection) && IsFinite(threat.BeamSourceVelocity) &&
                IsFinite(threat.BeamAngularVelocity) && IsFinite(threat.BeamBaseAngle) &&
                IsFinite(threat.BeamAngle) && IsFinite(threat.BeamAge) &&
                IsFinite(threat.BeamLength) && IsFinite(threat.BeamScale) &&
                IsFinite(threat.BeamScaleLimit);
        }

        private static bool SweptIntersects(in RectF playerBefore, in RectF playerAfter,
            in RectF threatBefore, in RectF threatAfter)
        {
            if (playerBefore.Intersects(threatBefore) || playerAfter.Intersects(threatAfter)) return true;
            var x = playerBefore.Center.X - threatBefore.Center.X;
            var y = playerBefore.Center.Y - threatBefore.Center.Y;
            var dx = playerAfter.X - playerBefore.X - (threatAfter.X - threatBefore.X);
            var dy = playerAfter.Y - playerBefore.Y - (threatAfter.Y - threatBefore.Y);
            var entry = 0f;
            var exit = 1f;
            return Clip(x, dx, (playerBefore.Width + threatBefore.Width) * .5f,
                       ref entry, ref exit) &&
                   Clip(y, dy, (playerBefore.Height + threatBefore.Height) * .5f,
                       ref entry, ref exit);
        }

        private static bool Clip(float origin, float delta, float radius,
            ref float entry, ref float exit)
        {
            if (Math.Abs(delta) < .00001f) return Math.Abs(origin) <= radius;
            var first = (-radius - origin) / delta;
            var second = (radius - origin) / delta;
            if (first > second) { var swap = first; first = second; second = swap; }
            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return entry <= exit;
        }

        private static RectF Union(in RectF first, in RectF second)
        {
            var left = Math.Min(first.Left, second.Left);
            var top = Math.Min(first.Top, second.Top);
            var right = Math.Max(first.Right, second.Right);
            var bottom = Math.Max(first.Bottom, second.Bottom);
            return new RectF(left, top, right - left, bottom - top);
        }

        private static bool TryVector(Vec2 target, Vec2 origin, float length,
            out Vec2 result)
        {
            result = default(Vec2);
            double distance;
            if (!TryDistance(target, origin, out distance) || distance <= 0d ||
                !IsFinitePositive(length)) return false;
            var scale = length / distance;
            result = new Vec2((float)((target.X - origin.X) * scale),
                (float)((target.Y - origin.Y) * scale));
            return IsFinite(result);
        }

        private static bool TryAdd(Vec2 left, Vec2 right, out Vec2 result)
        {
            result = new Vec2(left.X + right.X, left.Y + right.Y);
            return IsFinite(left) && IsFinite(right) && IsFinite(result);
        }

        private static bool TryDistance(Vec2 left, Vec2 right, out double distance)
        {
            distance = double.NaN;
            if (!IsFinite(left) || !IsFinite(right)) return false;
            var x = (double)left.X - right.X;
            var y = (double)left.Y - right.Y;
            distance = Math.Sqrt(x * x + y * y);
            return IsFinite(distance);
        }

        private static float Length(Vec2 value)
            => (float)Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);

        private static bool NearlySame(Vec2 left, Vec2 right, float tolerance)
        {
            double distance;
            return TryDistance(left, right, out distance) && distance <= tolerance;
        }

        private static bool IsFinite(Vec2 value) => IsFinite(value.X) && IsFinite(value.Y);
        private static bool IsFinite(RectF value) => IsFinite(value.X) && IsFinite(value.Y) &&
            IsFinite(value.Width) && IsFinite(value.Height);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinitePositive(float value) => IsFinite(value) && value > 0f;
        private static bool IsFiniteNonNegative(float value) => IsFinite(value) && value >= 0f;
    }
}
