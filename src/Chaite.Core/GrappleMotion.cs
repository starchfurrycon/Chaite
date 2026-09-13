using System;

namespace Chaite.Core
{
    /// <summary>The observed native state of one vanilla Grappling Hook projectile.</summary>
    public enum BasicHookProjectilePhase
    {
        Unsupported,
        Outbound,
        ReturningAfterMiss,
        Latched
    }

    /// <summary>
    /// Advice from the pure rescue contract. This type never writes Terraria input.
    /// A caller must separately validate and apply any suggested pulse.
    /// </summary>
    public enum BasicHookRescueStep
    {
        Ineligible,
        ReadyToFire,
        AwaitProjectile,
        AwaitLatch,
        Pulling,
        ArmJumpRelease,
        PulseJumpToDetach,
        FollowCertifiedMissRecovery,
        FollowCertifiedReturn,
        ReenteredLowConfigLoop,
        Abort
    }

    public enum BasicHookFailure
    {
        None,
        UnknownObservation,
        IdentityMismatch,
        InvalidNumber,
        UnsafeNativeContext,
        UnverifiedAnchor,
        UnsafeRoute,
        ShotNotObserved,
        HookMissed,
        LatchTimedOut,
        AnchorChanged,
        PullTimedOut,
        LowConfigLoopChanged,
        ReentryTimedOut
    }

    public enum BasicHookContingency
    {
        None,
        ShotNotObserved,
        HookMissed
    }

    /// <summary>
    /// Exact item/projectile fields read from the local 1.4.5.8 build. Known is
    /// evidence that every field was observed; it must not be set from item name alone.
    /// </summary>
    public struct BasicHookIdentity
    {
        public bool Known;
        public bool ResolvedByQuickGrapple;
        public bool ProjectileMarkedAsHook;
        public int ItemType;
        public int ProjectileType;
        public int ProjectileAiStyle;
        public float ShootSpeed;
        public int UseStyle;
        public int UseAnimation;
        public int UseTime;
        public bool NoUseGraphic;
        public bool NoMelee;
        public int ProjectileWidth;
        public int ProjectileHeight;
        public bool ProjectileNetImportant;
        public bool ProjectileTileCollide;
        public int MaximumSimultaneousHooks;
    }

    public struct BasicHookProjectileObservation
    {
        public bool Known;
        public bool Active;
        public int Index;
        public int Owner;
        public int Type;
        public int AiStyle;
        public float AiState;
        public Vec2 Center;
    }

    /// <summary>
    /// The tile and snapped projectile center associated with a candidate or a
    /// real latch. NativeActive means Tile.nactive(), not merely Tile.active().
    /// </summary>
    public struct BasicHookAnchorObservation
    {
        public bool Known;
        public int TileX;
        public int TileY;
        public int TileType;
        public bool NativeActive;
        public bool NativeSolid;
        public bool SolidTop;
        public bool Platform;
        public bool MinecartTrack;
        /// <summary>A sloped or half-block tile; native may latch, but this rescue profile will not.</summary>
        public bool Shaped;
        public bool Blacklisted;
        public Vec2 HookCenter;
    }

    /// <summary>Native Player.grapCount/grappling[0] identity for the observed tick.</summary>
    public struct BasicHookLinkObservation
    {
        public bool Known;
        public bool AtGrappleMovementEntry;
        public int GrappleCount;
        public int FirstProjectileIndex;
    }

    public enum BasicHookEvidencePhase
    {
        Outbound,
        Latch,
        Pull,
        DetachArm,
        DetachPulse,
        ReturnBallistic,
        ReturnFeatherFall,
        OutboundRangeTransition,
        MissReturn
    }

    public struct BasicHookTileSample
    {
        public BasicHookAnchorObservation Tile;
    }

    /// <summary>
    /// One finite pre/post tick supplied in preallocated evidence arrays. Positions
    /// are centers. For outbound phases projectile fields bracket native hook AI;
    /// for pull/detach they bracket Player.GrappleMovement.
    /// </summary>
    public struct BasicHookTrajectoryTick
    {
        public bool Known;
        public int Tick;
        public BasicHookEvidencePhase Phase;
        public Vec2 PlayerCenterBefore;
        public Vec2 PlayerVelocityBefore;
        public Vec2 PlayerCenterAfter;
        public Vec2 PlayerVelocityAfter;
        public bool PlayerPathKnown;
        public bool PlayerPathClear;
        public bool PlatformFree;
        public bool SupportStateKnown;
        public bool Grounded;
        public bool ThreatScoreKnown;
        public float RiskScore;
        public bool ProjectileActiveBefore;
        public bool ProjectileActiveAfter;
        public float ProjectileAiBefore;
        public float ProjectileAiAfter;
        public Vec2 ProjectileCenterBefore;
        public Vec2 ProjectileCenterAfter;
        public int TileSampleOffset;
        public int TileSampleCount;
        public bool ControlJump;
        public bool ControlUp;
        public bool ControlDown;
        public bool ReleaseJumpBefore;
        public bool ReleaseJumpAfter;
    }

    /// <summary>
    /// The successful latch branch and the counterfactual miss/return branch.
    /// Arrays are caller-owned and may be pooled; only Count entries are read.
    /// </summary>
    public struct BasicHookTrajectoryEvidence
    {
        public bool Known;
        public int WorldMaxTilesX;
        public int WorldMaxTilesY;
        public BasicHookTrajectoryTick[] SuccessTicks;
        public int SuccessTickCount;
        public BasicHookTileSample[] SuccessTileSamples;
        public int SuccessTileSampleCount;
        public BasicHookTrajectoryTick[] MissTicks;
        public int MissTickCount;
        public BasicHookTileSample[] MissTileSamples;
        public int MissTileSampleCount;
    }

    /// <summary>
    /// Static conditions for this deliberately narrow rescue profile. Mounts,
    /// inverted/gravity-control movement, pulley and liquid need separate models.
    /// SlowFall may be true only when the route certificate was built for it.
    /// </summary>
    public struct BasicHookUseContext
    {
        public bool Known;
        public bool LocalPlayerKnown;
        public int LocalPlayerIndex;
        public bool Dead;
        public bool CrowdControlled;
        public bool Tongued;
        public bool NoItems;
        public bool GrappleAndInteractShared;
        public bool MountActive;
        public bool NormalGravity;
        public bool GravityControlActive;
        public bool Pulley;
        public bool Wet;
        public bool SlowFall;
        public bool ReleaseHook;
        public bool ItemStartGateKnown;
        public bool ItemStartGateOpen;
    }

    /// <summary>
    /// A caller-supplied geometry certificate. The first-latch sweep and return
    /// trajectory must be produced from known tiles and the original low-config
    /// Boss loop; booleans default false so missing work fails closed.
    /// </summary>
    public struct BasicHookRouteCertificate
    {
        public bool Known;
        public Vec2 FireCenter;
        public Vec2 IntendedAnchorCenter;
        public Vec2 DetachCenter;
        public Vec2 LowConfigLoopCenter;
        public BasicHookAnchorObservation IntendedAnchor;
        public BasicHookTrajectoryEvidence Evidence;
        public bool ReturnProfileSlowFall;
        public float MaximumAcceptedRiskScore;
        public float Gravity;
        public float MaximumFallSpeed;
        /// <summary>Observed native runSlowdown used while every neutral route tick holds no direction.</summary>
        public float HorizontalSlowdown;
        public float JumpSpeed;
        public int JumpHeight;
        public bool OldStyleParkour;
        public float AnchorTolerance;
        public float DetachRadius;
        public float LoopHalfWidth;
        public float LoopHalfHeight;
        public float LoopMaximumAbsVelocityX;
        public float LoopMaximumAbsVelocityY;
        public int LowConfigLoopEpoch;
        public int LatchDeadlineTicks;
        public int DetachDeadlineTicks;
        public int ReentryDeadlineTicks;
    }

    /// <summary>
    /// Immutable-by-construction scalar result of the cold evidence pass. It does
    /// not retain the caller's mutable arrays, so the per-tick decision path is O(1)
    /// and allocation-free after verification.
    /// </summary>
    public struct BasicHookVerifiedRoute
    {
        internal int Stamp;
        internal Vec2 FireCenter;
        internal Vec2 LaunchVelocity;
        internal Vec2 IntendedAnchorCenter;
        internal Vec2 DetachCenter;
        internal Vec2 LowConfigLoopCenter;
        internal BasicHookAnchorObservation IntendedAnchor;
        internal bool ReturnProfileSlowFall;
        internal float MaximumAcceptedRiskScore;
        internal float Gravity;
        internal float MaximumFallSpeed;
        internal float HorizontalSlowdown;
        internal float JumpSpeed;
        internal int JumpHeight;
        internal bool OldStyleParkour;
        internal float AnchorTolerance;
        internal float DetachRadius;
        internal float LoopHalfWidth;
        internal float LoopHalfHeight;
        internal float LoopMaximumAbsVelocityX;
        internal float LoopMaximumAbsVelocityY;
        internal int LowConfigLoopEpoch;
        internal int ExpectedLatchTick;
        internal int ExpectedDetachTick;
        internal int ExpectedReentryTick;
        internal int LatchDeadlineTicks;
        internal int DetachDeadlineTicks;
        internal int ReentryDeadlineTicks;

        public bool Known => Stamp == BasicHookMotion.VerifiedRouteStamp;
    }

    /// <summary>
    /// One read-only Player.Update-entry snapshot from the version-locked adapter.
    /// CandidateRoute is a cold-path proof copied without its mutable evidence
    /// buffers. Projectile/link/anchor are live observations, not predictions.
    /// </summary>
    public struct BasicHookFrameSnapshot
    {
        public bool Known;
        public long Sequence;
        public BasicHookIdentity Identity;
        public BasicHookUseContext Context;
        public bool ReleaseJump;
        public Vec2 PlayerCenter;
        public Vec2 PlayerVelocity;
        public bool PlayerGrounded;
        public bool CandidateKnown;
        public BasicHookVerifiedRoute CandidateRoute;
        public Vec2 CandidateAimWorld;
        public bool ProjectileObserved;
        public BasicHookProjectileObservation Projectile;
        public BasicHookLinkObservation Link;
        public BasicHookAnchorObservation Anchor;
        public bool LiveThreatFieldKnown;
        public bool CertifiedTrajectoryStillSafe;
        public float LiveWorstCaseRiskScore;
        public int LowConfigLoopEpoch;
    }

    public struct BasicHookRescueObservation
    {
        public bool Known;
        public bool ShotIssued;
        public int TicksSinceShot;
        public bool ProjectileObserved;
        public BasicHookProjectileObservation Projectile;
        public BasicHookLinkObservation Link;
        public BasicHookAnchorObservation Anchor;
        public bool DetachObserved;
        public bool FirstLatchTickKnown;
        public int FirstLatchTick;
        public bool ReleaseJump;
        public bool OriginalLowConfigLoopStillValid;
        public bool LiveThreatFieldKnown;
        public bool CertifiedTrajectoryStillSafe;
        public float LiveWorstCaseRiskScore;
        public int LowConfigLoopEpoch;
        public Vec2 PlayerCenter;
        public Vec2 PlayerVelocity;
    }

    public struct BasicHookRescueDecision
    {
        public BasicHookRescueStep Step;
        public BasicHookFailure Failure;
        public BasicHookContingency Contingency;

        public bool Accepted => Failure == BasicHookFailure.None &&
            Step != BasicHookRescueStep.Ineligible && Step != BasicHookRescueStep.Abort;
    }

    /// <summary>The relevant result of one native GrappleMovement tick.</summary>
    public struct BasicHookAttachedTick
    {
        public bool Supported;
        public Vec2 PullVelocity;
        public Vec2 VelocityAfterGrapple;
        public bool GoingDownWithGrapple;
        public bool ReleaseJumpAfter;
        public bool Detached;
        public int JumpTicksAfter;
        public bool ClearsRocketState;
        public bool RefreshesDoubleJumps;
    }

    /// <summary>
    /// Allocation-free, side-effect-free minimum contract for item 84 / projectile
    /// 13 in Terraria 1.4.5.8. This is a motion primitive, not a Boss success claim.
    /// </summary>
    public static class BasicHookMotion
    {
        public const int ItemType = 84;
        public const int ProjectileType = 13;
        public const int ProjectileAiStyle = 7;
        public const float ShootSpeed = 11.5f;
        public const float OutboundRange = 300f;
        public const float ReturnSpeed = 11f;
        public const float PullSpeed = 11f;
        public const float ReturnKillDistance = 24f;
        public const int MaximumSimultaneousHooks = 1;
        public const int ProjectileObservationGraceTicks = 2;
        public const int MaximumLatchObservationTicks = 32;
        public const int MaximumCertifiedPullTicks = 240;
        public const int MaximumCertifiedRescueTicks = 600;
        public const int MaximumEvidenceTicksPerBranch = 600;
        public const int MaximumEvidenceTileSamplesPerBranch = 4096;
        internal const int VerifiedRouteStamp = 0x42724831;

        public static bool MatchesExactIdentity(in BasicHookIdentity identity)
        {
            return identity.Known && identity.ResolvedByQuickGrapple && identity.ProjectileMarkedAsHook &&
                identity.ItemType == ItemType && identity.ProjectileType == ProjectileType &&
                identity.ProjectileAiStyle == ProjectileAiStyle && IsFinite(identity.ShootSpeed) &&
                identity.ShootSpeed == ShootSpeed && identity.UseStyle == 5 &&
                identity.UseAnimation == 20 && identity.UseTime == 20 &&
                identity.NoUseGraphic && identity.NoMelee && identity.ProjectileWidth == 18 &&
                identity.ProjectileHeight == 18 && identity.ProjectileNetImportant &&
                !identity.ProjectileTileCollide &&
                identity.MaximumSimultaneousHooks == MaximumSimultaneousHooks;
        }

        /// <summary>
        /// Mirrors the non-inverted, non-degenerate QuickGrapple launch vector.
        /// Native has a fallback for a zero cursor vector; rescue planning rejects it.
        /// </summary>
        public static bool TryGetLaunchVelocity(in BasicHookIdentity identity, Vec2 fireCenter,
            Vec2 cursorWorld, out Vec2 velocity)
        {
            velocity = default(Vec2);
            if (!MatchesExactIdentity(in identity) || !IsFinite(fireCenter) || !IsFinite(cursorWorld))
                return false;
            return TryVectorWithMaximum(cursorWorld, fireCenter, ShootSpeed, alwaysNormalize: true, out velocity);
        }

        /// <summary>The exact ordinary-hook tile predicate before the safety exclusions.</summary>
        public static bool NativeCanLatch(in BasicHookAnchorObservation anchor)
        {
            return anchor.Known && anchor.TileX >= 0 && anchor.TileY >= 0 &&
                anchor.TileType >= 0 && anchor.MinecartTrack == (anchor.TileType == 314) &&
                IsFinite(anchor.HookCenter) && anchor.NativeActive &&
                !anchor.Blacklisted && (anchor.NativeSolid || anchor.MinecartTrack);
        }

        /// <summary>
        /// The first reviewed rescue profile excludes tracks and all platform/solid-top
        /// anchors. It also requires the swept pull corridor to be platform-free.
        /// </summary>
        public static bool IsSafetyAnchor(in BasicHookAnchorObservation anchor)
        {
            return NativeCanLatch(in anchor) && anchor.NativeSolid && !anchor.MinecartTrack &&
                !anchor.Platform && !anchor.SolidTop && !anchor.Shaped;
        }

        public static BasicHookProjectilePhase ClassifyProjectile(
            in BasicHookProjectileObservation projectile, int localPlayerIndex,
            out BasicHookFailure failure)
        {
            failure = BasicHookFailure.UnknownObservation;
            if (!projectile.Known || !projectile.Active || localPlayerIndex < 0)
                return BasicHookProjectilePhase.Unsupported;
            if (projectile.Index < 0 || projectile.Owner != localPlayerIndex ||
                projectile.Type != ProjectileType || projectile.AiStyle != ProjectileAiStyle)
            {
                failure = BasicHookFailure.IdentityMismatch;
                return BasicHookProjectilePhase.Unsupported;
            }
            if (!IsFinite(projectile.AiState) || !IsFinite(projectile.Center))
            {
                failure = BasicHookFailure.InvalidNumber;
                return BasicHookProjectilePhase.Unsupported;
            }
            failure = BasicHookFailure.None;
            if (projectile.AiState == 0f) return BasicHookProjectilePhase.Outbound;
            if (projectile.AiState == 1f) return BasicHookProjectilePhase.ReturningAfterMiss;
            if (projectile.AiState == 2f) return BasicHookProjectilePhase.Latched;
            failure = BasicHookFailure.IdentityMismatch;
            return BasicHookProjectilePhase.Unsupported;
        }

        /// <summary>
        /// Exact one-hook branch of Player.GetGrapplingForces: delta to the real,
        /// snapped projectile center, capped at 11 px/tick. A candidate tile coordinate
        /// is not accepted as a substitute for an observed ai[0] == 2 projectile.
        /// </summary>
        public static bool TryGetPullVelocity(in BasicHookProjectileObservation projectile,
            in BasicHookLinkObservation link, in BasicHookAnchorObservation anchor,
            int localPlayerIndex, Vec2 playerCenter, out Vec2 velocity)
        {
            velocity = default(Vec2);
            BasicHookFailure ignored;
            if (ClassifyProjectile(in projectile, localPlayerIndex, out ignored) != BasicHookProjectilePhase.Latched ||
                !link.Known || link.GrappleCount != MaximumSimultaneousHooks ||
                !link.AtGrappleMovementEntry ||
                link.FirstProjectileIndex != projectile.Index || !IsSafetyAnchor(in anchor) ||
                !IsFinite(playerCenter) || !NearlySame(projectile.Center, anchor.HookCenter, 0.01f))
                return false;
            return TryVectorWithMaximum(anchor.HookCenter, playerCenter, PullSpeed,
                alwaysNormalize: false, out velocity);
        }

        /// <summary>
        /// One side-effect-free GrappleMovement step for the reviewed single-hook
        /// profile. The preGrappleVelocity is the velocity native code samples before
        /// overwriting it with the pull force; that distinction controls jump-off.
        /// </summary>
        public static bool TryApplyAttachedTick(in BasicHookProjectileObservation projectile,
            in BasicHookLinkObservation link, in BasicHookAnchorObservation anchor,
            int localPlayerIndex, Vec2 playerCenter, Vec2 preGrappleVelocity,
            bool wet, float jumpSpeed, int jumpHeight, bool oldStyleParkour,
            bool controlJump, bool controlUp, bool controlDown, bool releaseJump,
            out BasicHookAttachedTick result)
        {
            result = default(BasicHookAttachedTick);
            Vec2 pull;
            if (!TryGetPullVelocity(in projectile, in link, in anchor, localPlayerIndex,
                    playerCenter, out pull) || !IsFinite(preGrappleVelocity) ||
                !IsFinite(jumpSpeed) || jumpSpeed <= 0f || jumpHeight < 0)
                return false;

            result.Supported = true;
            result.PullVelocity = pull;
            result.VelocityAfterGrapple = pull;
            result.GoingDownWithGrapple = pull.Y > 0f;
            result.ReleaseJumpAfter = releaseJump;
            result.ClearsRocketState = true;

            if (!controlJump)
            {
                result.ReleaseJumpAfter = true;
                return true;
            }
            if (!releaseJump) return true;

            var slowBeforePull = LengthSquared(preGrappleVelocity) < 4d;
            var wetAtRest = wet && preGrappleVelocity.Y > -0.02f && preGrappleVelocity.Y < 0.02f;
            var fullJump = slowBeforePull || wetAtRest;
            if (controlDown || pull.Y > 0f && preGrappleVelocity.Y == 0f && !controlUp)
                fullJump = false;

            result.Detached = true;
            result.ReleaseJumpAfter = false;
            result.RefreshesDoubleJumps = true;
            if (fullJump)
            {
                result.VelocityAfterGrapple.Y = -jumpSpeed;
                result.JumpTicksAfter = oldStyleParkour ? jumpHeight / 2 : jumpHeight;
            }
            else
            {
                result.VelocityAfterGrapple.Y = pull.Y + 0.01f;
            }
            return IsFinite(result.VelocityAfterGrapple);
        }

        public static bool ValidateRoute(in BasicHookRouteCertificate route,
            bool observedSlowFall, out BasicHookFailure failure)
        {
            BasicHookVerifiedRoute ignored;
            return TryVerifyRoute(in route, observedSlowFall, out ignored, out failure);
        }

        /// <summary>
        /// Cold-path proof construction. It verifies both finite trajectory branches
        /// and copies only immutable scalars into proof; caller-owned arrays are not
        /// retained or re-walked by the hot decision path.
        /// </summary>
        public static bool TryVerifyRoute(in BasicHookRouteCertificate route,
            bool observedSlowFall, out BasicHookVerifiedRoute proof,
            out BasicHookFailure failure)
        {
            proof = default(BasicHookVerifiedRoute);
            failure = BasicHookFailure.UnsafeRoute;
            if (!route.Known || route.ReturnProfileSlowFall != observedSlowFall ||
                !IsSafetyAnchor(in route.IntendedAnchor))
                return false;
            if (!IsFinite(route.FireCenter) || !IsFinite(route.IntendedAnchorCenter) ||
                !IsFinite(route.DetachCenter) || !IsFinite(route.LowConfigLoopCenter) ||
                !IsFinitePositive(route.AnchorTolerance) || route.AnchorTolerance > 16f ||
                !IsFinitePositive(route.DetachRadius) || route.DetachRadius > 32f ||
                !IsFinitePositive(route.LoopHalfWidth) || !IsFinitePositive(route.LoopHalfHeight) ||
                !IsFiniteNonNegative(route.LoopMaximumAbsVelocityX) ||
                !IsFiniteNonNegative(route.LoopMaximumAbsVelocityY) ||
                !IsFiniteNonNegative(route.MaximumAcceptedRiskScore) ||
                !IsFiniteNonNegative(route.Gravity) ||
                !IsFinitePositive(route.MaximumFallSpeed) ||
                !IsFiniteNonNegative(route.HorizontalSlowdown) ||
                !IsFinitePositive(route.JumpSpeed) || route.JumpHeight < 0)
            {
                failure = BasicHookFailure.InvalidNumber;
                return false;
            }
            if (!NearlySame(route.IntendedAnchor.HookCenter, route.IntendedAnchorCenter,
                    route.AnchorTolerance) || !IsPointOnPullSegment(route.FireCenter,
                    route.IntendedAnchorCenter, route.DetachCenter))
                return false;
            double anchorDistance;
            if (!TryDistance(route.FireCenter, route.IntendedAnchorCenter, out anchorDistance) ||
                anchorDistance <= 0d || anchorDistance > OutboundRange)
                return false;
            if (route.LowConfigLoopEpoch < 0 || route.LatchDeadlineTicks < 1 ||
                route.LatchDeadlineTicks > MaximumLatchObservationTicks ||
                route.DetachDeadlineTicks < route.LatchDeadlineTicks ||
                route.DetachDeadlineTicks > MaximumCertifiedPullTicks ||
                route.ReentryDeadlineTicks < route.DetachDeadlineTicks ||
                route.ReentryDeadlineTicks > MaximumCertifiedRescueTicks)
                return false;

            float worstRisk;
            if (!ValidateTrajectoryEvidence(in route, out worstRisk) ||
                worstRisk > route.MaximumAcceptedRiskScore)
                return false;

            var expectedLatchTick = -1;
            var expectedDetachTick = -1;
            for (var index = 0; index < route.Evidence.SuccessTickCount; index++)
            {
                if (route.Evidence.SuccessTicks[index].Phase == BasicHookEvidencePhase.Latch &&
                    expectedLatchTick < 0)
                {
                    // Evidence tick zero is the first native update after the
                    // controlHook edge. Runtime observes its post-state one entry
                    // later, so elapsed native updates are index + 1.
                    expectedLatchTick = route.Evidence.SuccessTicks[index].Tick + 1;
                }
                if (route.Evidence.SuccessTicks[index].Phase == BasicHookEvidencePhase.DetachPulse)
                    expectedDetachTick = route.Evidence.SuccessTicks[index].Tick + 1;
            }
            var expectedReentryTick = route.Evidence.SuccessTicks[
                route.Evidence.SuccessTickCount - 1].Tick + 1;
            if (expectedLatchTick < 0 || expectedDetachTick <= expectedLatchTick ||
                expectedReentryTick <= expectedDetachTick) return false;
            Vec2 verifiedLaunch;
            if (!TryVectorWithMaximum(route.IntendedAnchorCenter, route.FireCenter,
                    ShootSpeed, alwaysNormalize: true, out verifiedLaunch)) return false;

            proof = new BasicHookVerifiedRoute
            {
                Stamp = VerifiedRouteStamp,
                FireCenter = route.FireCenter,
                LaunchVelocity = verifiedLaunch,
                IntendedAnchorCenter = route.IntendedAnchorCenter,
                DetachCenter = route.DetachCenter,
                LowConfigLoopCenter = route.LowConfigLoopCenter,
                IntendedAnchor = route.IntendedAnchor,
                ReturnProfileSlowFall = route.ReturnProfileSlowFall,
                MaximumAcceptedRiskScore = route.MaximumAcceptedRiskScore,
                Gravity = route.Gravity,
                MaximumFallSpeed = route.MaximumFallSpeed,
                HorizontalSlowdown = route.HorizontalSlowdown,
                JumpSpeed = route.JumpSpeed,
                JumpHeight = route.JumpHeight,
                OldStyleParkour = route.OldStyleParkour,
                AnchorTolerance = route.AnchorTolerance,
                DetachRadius = route.DetachRadius,
                LoopHalfWidth = route.LoopHalfWidth,
                LoopHalfHeight = route.LoopHalfHeight,
                LoopMaximumAbsVelocityX = route.LoopMaximumAbsVelocityX,
                LoopMaximumAbsVelocityY = route.LoopMaximumAbsVelocityY,
                LowConfigLoopEpoch = route.LowConfigLoopEpoch,
                ExpectedLatchTick = expectedLatchTick,
                ExpectedDetachTick = expectedDetachTick,
                ExpectedReentryTick = expectedReentryTick,
                LatchDeadlineTicks = route.LatchDeadlineTicks,
                DetachDeadlineTicks = route.DetachDeadlineTicks,
                ReentryDeadlineTicks = route.ReentryDeadlineTicks
            };
            failure = BasicHookFailure.None;
            return true;
        }

        public static bool CanBeginRescue(in BasicHookIdentity identity,
            in BasicHookUseContext context, in BasicHookVerifiedRoute route,
            out BasicHookFailure failure)
        {
            if (!ValidateEnvironment(in identity, in context, out failure)) return false;
            if (!IsVerifiedRoute(in route, context.SlowFall))
            {
                failure = BasicHookFailure.UnsafeRoute;
                return false;
            }
            if (!context.ReleaseHook || !context.ItemStartGateKnown || !context.ItemStartGateOpen)
            {
                failure = BasicHookFailure.UnsafeNativeContext;
                return false;
            }
            failure = BasicHookFailure.None;
            return true;
        }

        /// <summary>
        /// Evaluates the observed rescue phase. ReadyToFire/PulseJumpToDetach are
        /// recommendations only; no input, projectile, tile or player state is changed.
        /// </summary>
        public static BasicHookRescueDecision Decide(in BasicHookIdentity identity,
            in BasicHookUseContext context, in BasicHookVerifiedRoute route,
            in BasicHookRescueObservation observation)
        {
            BasicHookFailure failure;
            if (!ValidateEnvironment(in identity, in context, out failure))
                return Decision(BasicHookRescueStep.Ineligible, failure);
            if (!IsVerifiedRoute(in route, context.SlowFall))
                return Decision(BasicHookRescueStep.Ineligible, BasicHookFailure.UnsafeRoute);
            if (!observation.Known || observation.TicksSinceShot < 0 ||
                !IsFinite(observation.PlayerCenter) || !IsFinite(observation.PlayerVelocity))
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.UnknownObservation);
            if (!observation.LiveThreatFieldKnown || !observation.CertifiedTrajectoryStillSafe ||
                !IsFiniteNonNegative(observation.LiveWorstCaseRiskScore) ||
                observation.LiveWorstCaseRiskScore > route.MaximumAcceptedRiskScore)
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.UnsafeRoute);
            if (!observation.OriginalLowConfigLoopStillValid ||
                observation.LowConfigLoopEpoch != route.LowConfigLoopEpoch)
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.LowConfigLoopChanged);
            if (!observation.ShotIssued)
            {
                if (observation.TicksSinceShot != 0 || observation.ProjectileObserved ||
                    observation.DetachObserved || observation.FirstLatchTickKnown)
                    return Decision(BasicHookRescueStep.Abort, BasicHookFailure.UnknownObservation);
                if (!context.ReleaseHook || !context.ItemStartGateKnown || !context.ItemStartGateOpen)
                    return Decision(BasicHookRescueStep.Ineligible, BasicHookFailure.UnsafeNativeContext);
                return Decision(BasicHookRescueStep.ReadyToFire, BasicHookFailure.None);
            }
            if (observation.TicksSinceShot > route.ReentryDeadlineTicks)
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.ReentryTimedOut);

            if (observation.DetachObserved)
            {
                if (observation.ProjectileObserved)
                    return Decision(BasicHookRescueStep.Abort, BasicHookFailure.AnchorChanged);
                if (observation.TicksSinceShot >= route.ExpectedReentryTick &&
                    InLoopEnvelope(in route, observation.PlayerCenter, observation.PlayerVelocity))
                    return Decision(BasicHookRescueStep.ReenteredLowConfigLoop, BasicHookFailure.None);
                return Decision(BasicHookRescueStep.FollowCertifiedReturn, BasicHookFailure.None);
            }

            if (!observation.ProjectileObserved)
            {
                if (observation.TicksSinceShot <= ProjectileObservationGraceTicks)
                    return Decision(BasicHookRescueStep.AwaitProjectile, BasicHookFailure.None);
                if (InLoopEnvelope(in route, observation.PlayerCenter, observation.PlayerVelocity))
                    return Decision(BasicHookRescueStep.ReenteredLowConfigLoop,
                        BasicHookFailure.None, BasicHookContingency.ShotNotObserved);
                return Decision(BasicHookRescueStep.FollowCertifiedMissRecovery,
                    BasicHookFailure.None, BasicHookContingency.ShotNotObserved);
            }

            var phase = ClassifyProjectile(in observation.Projectile,
                context.LocalPlayerIndex, out failure);
            if (phase == BasicHookProjectilePhase.Unsupported)
                return Decision(BasicHookRescueStep.Abort, failure);
            if (phase == BasicHookProjectilePhase.ReturningAfterMiss)
            {
                return Decision(BasicHookRescueStep.FollowCertifiedMissRecovery,
                    BasicHookFailure.None, BasicHookContingency.HookMissed);
            }
            if (phase == BasicHookProjectilePhase.Outbound)
            {
                if (observation.TicksSinceShot > route.LatchDeadlineTicks)
                    return Decision(BasicHookRescueStep.Abort, BasicHookFailure.LatchTimedOut);
                return Decision(BasicHookRescueStep.AwaitLatch, BasicHookFailure.None);
            }

            Vec2 pull;
            if (!observation.FirstLatchTickKnown || observation.FirstLatchTick < 0 ||
                observation.FirstLatchTick > route.LatchDeadlineTicks ||
                observation.FirstLatchTick != route.ExpectedLatchTick ||
                observation.FirstLatchTick > observation.TicksSinceShot)
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.LatchTimedOut);
            if (!TryGetPullVelocity(in observation.Projectile, in observation.Link,
                    in observation.Anchor, context.LocalPlayerIndex,
                    observation.PlayerCenter, out pull))
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.UnverifiedAnchor);
            if (!NearlySame(observation.Anchor.HookCenter, route.IntendedAnchorCenter,
                    route.AnchorTolerance))
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.AnchorChanged);
            if (observation.TicksSinceShot > route.DetachDeadlineTicks)
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.PullTimedOut);

            double detachDistance;
            if (!TryDistance(observation.PlayerCenter, route.DetachCenter, out detachDistance))
                return Decision(BasicHookRescueStep.Abort, BasicHookFailure.InvalidNumber);
            if (detachDistance > route.DetachRadius)
                return Decision(BasicHookRescueStep.Pulling, BasicHookFailure.None);
            return observation.ReleaseJump
                ? Decision(BasicHookRescueStep.PulseJumpToDetach, BasicHookFailure.None)
                : Decision(BasicHookRescueStep.ArmJumpRelease, BasicHookFailure.None);
        }

        private static bool ValidateTrajectoryEvidence(in BasicHookRouteCertificate route,
            out float worstRisk)
        {
            worstRisk = 0f;
            var evidence = route.Evidence;
            if (!evidence.Known || evidence.WorldMaxTilesX <= 0 || evidence.WorldMaxTilesY <= 0 ||
                evidence.SuccessTicks == null || evidence.MissTicks == null ||
                evidence.SuccessTileSamples == null || evidence.MissTileSamples == null ||
                evidence.SuccessTickCount < 1 || evidence.MissTickCount < 1 ||
                evidence.SuccessTickCount > evidence.SuccessTicks.Length ||
                evidence.MissTickCount > evidence.MissTicks.Length ||
                evidence.SuccessTickCount > MaximumEvidenceTicksPerBranch ||
                evidence.MissTickCount > MaximumEvidenceTicksPerBranch ||
                evidence.SuccessTileSampleCount < 0 || evidence.MissTileSampleCount < 0 ||
                evidence.SuccessTileSampleCount > evidence.SuccessTileSamples.Length ||
                evidence.MissTileSampleCount > evidence.MissTileSamples.Length ||
                evidence.SuccessTileSampleCount > MaximumEvidenceTileSamplesPerBranch ||
                evidence.MissTileSampleCount > MaximumEvidenceTileSamplesPerBranch)
                return false;

            Vec2 launchVelocity;
            if (!TryVectorWithMaximum(route.IntendedAnchorCenter, route.FireCenter,
                    ShootSpeed, alwaysNormalize: true, out launchVelocity))
                return false;
            if (!ValidateSuccessBranch(in route, launchVelocity, ref worstRisk) ||
                !ValidateMissBranch(in route, launchVelocity, ref worstRisk))
                return false;
            return IsFiniteNonNegative(worstRisk);
        }

        private static bool ValidateSuccessBranch(in BasicHookRouteCertificate route,
            Vec2 launchVelocity, ref float worstRisk)
        {
            var evidence = route.Evidence;
            var sampleCursor = 0;
            var stage = 0;
            var sawPull = false;
            var sawPulse = false;
            var sawReturn = false;
            for (var index = 0; index < evidence.SuccessTickCount; index++)
            {
                var tick = evidence.SuccessTicks[index];
                if (!ValidateCommonTick(evidence.SuccessTicks, index, in tick, ref worstRisk))
                    return false;
                if (index == 0 && (!tick.ProjectileActiveBefore ||
                        !NearlySame(tick.ProjectileCenterBefore, route.FireCenter, 0.01f)))
                    return false;
                switch (tick.Phase)
                {
                case BasicHookEvidencePhase.Outbound:
                    if (stage != 0 || !ValidateOutboundFrame(in route, in tick,
                            launchVelocity, expectLatch: false, rangeTransition: false,
                            evidence.SuccessTileSamples, evidence.SuccessTileSampleCount,
                            ref sampleCursor)) return false;
                    break;
                case BasicHookEvidencePhase.Latch:
                    if (stage != 0 || tick.Tick > route.LatchDeadlineTicks ||
                        !ValidateOutboundFrame(in route, in tick, launchVelocity,
                            expectLatch: true, rangeTransition: false,
                            evidence.SuccessTileSamples, evidence.SuccessTileSampleCount,
                            ref sampleCursor)) return false;
                    stage = 1;
                    break;
                case BasicHookEvidencePhase.Pull:
                    if ((stage != 1 && stage != 2) ||
                        !ValidateAttachedFrame(in route, in tick, mustDetach: false,
                            mustArm: false)) return false;
                    stage = 2;
                    sawPull = true;
                    break;
                case BasicHookEvidencePhase.DetachArm:
                    if ((stage != 1 && stage != 2) ||
                        !ValidateAttachedFrame(in route, in tick, mustDetach: false,
                            mustArm: true)) return false;
                    stage = 3;
                    break;
                case BasicHookEvidencePhase.DetachPulse:
                    if ((stage != 2 && stage != 3) || tick.Tick > route.DetachDeadlineTicks ||
                        !ValidateAttachedFrame(in route, in tick, mustDetach: true,
                            mustArm: false)) return false;
                    stage = 4;
                    sawPulse = true;
                    break;
                case BasicHookEvidencePhase.ReturnBallistic:
                case BasicHookEvidencePhase.ReturnFeatherFall:
                    if ((stage != 4 && stage != 5) || tick.Tick > route.ReentryDeadlineTicks ||
                        !ValidateReturnFrame(in route, in tick)) return false;
                    stage = 5;
                    sawReturn = true;
                    break;
                default:
                    return false;
                }
            }
            var last = evidence.SuccessTicks[evidence.SuccessTickCount - 1];
            return stage == 5 && sawPull && sawPulse && sawReturn &&
                sampleCursor == evidence.SuccessTileSampleCount &&
                InLoopEnvelope(in route, last.PlayerCenterAfter, last.PlayerVelocityAfter);
        }

        private static bool ValidateMissBranch(in BasicHookRouteCertificate route,
            Vec2 launchVelocity, ref float worstRisk)
        {
            var evidence = route.Evidence;
            var sampleCursor = 0;
            var stage = 0;
            var sawTransition = false;
            var sawReturn = false;
            for (var index = 0; index < evidence.MissTickCount; index++)
            {
                var tick = evidence.MissTicks[index];
                if (!ValidateCommonTick(evidence.MissTicks, index, in tick, ref worstRisk))
                    return false;
                if (index == 0 && (!tick.ProjectileActiveBefore ||
                        !NearlySame(tick.ProjectileCenterBefore, route.FireCenter, 0.01f)))
                    return false;
                switch (tick.Phase)
                {
                case BasicHookEvidencePhase.Outbound:
                    if (stage != 0 || !ValidateOutboundFrame(in route, in tick,
                            launchVelocity, expectLatch: false, rangeTransition: false,
                            evidence.MissTileSamples, evidence.MissTileSampleCount,
                            ref sampleCursor)) return false;
                    break;
                case BasicHookEvidencePhase.OutboundRangeTransition:
                    if (stage != 0 || !ValidateOutboundFrame(in route, in tick,
                            launchVelocity, expectLatch: false, rangeTransition: true,
                            evidence.MissTileSamples, evidence.MissTileSampleCount,
                            ref sampleCursor)) return false;
                    stage = 1;
                    sawTransition = true;
                    break;
                case BasicHookEvidencePhase.MissReturn:
                    if (stage != 1 || tick.Tick > route.ReentryDeadlineTicks ||
                        !ValidateMissReturnFrame(in route, in tick)) return false;
                    sawReturn = true;
                    break;
                default:
                    return false;
                }
            }
            var last = evidence.MissTicks[evidence.MissTickCount - 1];
            return sawTransition && sawReturn && !last.ProjectileActiveAfter &&
                sampleCursor == evidence.MissTileSampleCount &&
                InLoopEnvelope(in route, last.PlayerCenterAfter, last.PlayerVelocityAfter);
        }

        private static bool ValidateCommonTick(BasicHookTrajectoryTick[] ticks, int index,
            in BasicHookTrajectoryTick tick, ref float worstRisk)
        {
            if (!tick.Known || tick.Tick != index || !tick.PlayerPathKnown ||
                !tick.PlayerPathClear || !tick.PlatformFree || !tick.SupportStateKnown ||
                !tick.ThreatScoreKnown || !IsFiniteNonNegative(tick.RiskScore) ||
                !IsFinite(tick.PlayerCenterBefore) || !IsFinite(tick.PlayerVelocityBefore) ||
                !IsFinite(tick.PlayerCenterAfter) || !IsFinite(tick.PlayerVelocityAfter) ||
                !IsFinite(tick.ProjectileAiBefore) || !IsFinite(tick.ProjectileAiAfter) ||
                !IsFinite(tick.ProjectileCenterBefore) || !IsFinite(tick.ProjectileCenterAfter) ||
                tick.TileSampleOffset < 0 || tick.TileSampleCount < 0)
                return false;
            worstRisk = Math.Max(worstRisk, tick.RiskScore);
            if (index > 0)
            {
                var previous = ticks[index - 1];
                if (!NearlySame(tick.PlayerCenterBefore, previous.PlayerCenterAfter, 0.01f) ||
                    !NearlySame(tick.PlayerVelocityBefore, previous.PlayerVelocityAfter, 0.01f) ||
                    tick.ReleaseJumpBefore != previous.ReleaseJumpAfter ||
                    tick.ProjectileActiveBefore != previous.ProjectileActiveAfter)
                    return false;
                if (tick.ProjectileActiveBefore &&
                    (!NearlySame(tick.ProjectileCenterBefore, previous.ProjectileCenterAfter, 0.01f) ||
                     tick.ProjectileAiBefore != previous.ProjectileAiAfter))
                    return false;
            }
            Vec2 expectedCenter;
            if (!TryAdd(tick.PlayerCenterBefore, tick.PlayerVelocityAfter, out expectedCenter) ||
                !NearlySame(expectedCenter, tick.PlayerCenterAfter, 0.02f))
                return false;
            if (tick.Grounded && (tick.PlayerVelocityBefore.Y != 0f ||
                    tick.PlayerVelocityAfter.Y != 0f ||
                    Math.Abs((double)tick.PlayerCenterAfter.Y - tick.PlayerCenterBefore.Y) > 0.01d))
                return false;
            return true;
        }

        private static bool ValidateOutboundFrame(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick, Vec2 launchVelocity, bool expectLatch,
            bool rangeTransition, BasicHookTileSample[] samples, int sampleCount,
            ref int sampleCursor)
        {
            if (!tick.ProjectileActiveBefore || tick.ProjectileAiBefore != 0f ||
                !tick.ProjectileActiveAfter || tick.ControlJump || tick.ControlUp ||
                tick.ControlDown || !tick.ReleaseJumpAfter ||
                !ValidateNeutralPlayerMotion(in route, in tick)) return false;
            double playerDistance;
            if (!TryDistance(tick.ProjectileCenterBefore, tick.PlayerCenterBefore,
                    out playerDistance)) return false;
            if (rangeTransition ? playerDistance <= OutboundRange : playerDistance > OutboundRange)
                return false;
            if (!ValidateHookScan(in route, in tick, samples, sampleCount,
                    expectLatch, ref sampleCursor)) return false;
            if (expectLatch)
            {
                return !rangeTransition && tick.ProjectileAiAfter == 2f &&
                    NearlySame(tick.ProjectileCenterAfter, route.IntendedAnchorCenter,
                        route.AnchorTolerance);
            }
            Vec2 expectedProjectile;
            if (!TryAdd(tick.ProjectileCenterBefore, launchVelocity, out expectedProjectile) ||
                !NearlySame(expectedProjectile, tick.ProjectileCenterAfter, 0.02f))
                return false;
            return tick.ProjectileAiAfter == (rangeTransition ? 1f : 0f);
        }

        private static bool ValidateHookScan(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick, BasicHookTileSample[] samples, int sampleCount,
            bool expectLatch, ref int sampleCursor)
        {
            if (tick.TileSampleOffset != sampleCursor ||
                tick.TileSampleOffset > sampleCount - tick.TileSampleCount)
                return false;
            var worldX = route.Evidence.WorldMaxTilesX;
            var worldY = route.Evidence.WorldMaxTilesY;
            var startX = (int)((tick.ProjectileCenterBefore.X - 21f) / 16f);
            var endX = (int)((tick.ProjectileCenterBefore.X + 37f) / 16f);
            var startY = (int)((tick.ProjectileCenterBefore.Y - 21f) / 16f);
            var endY = (int)((tick.ProjectileCenterBefore.Y + 37f) / 16f);
            if (startX < 0) startX = 0;
            if (startY < 0) startY = 0;
            if (endX > worldX) endX = worldX;
            if (endY > worldY) endY = worldY;
            var used = 0;
            for (var x = startX; x < endX; x++)
            for (var y = startY; y < endY; y++)
            {
                if (used >= tick.TileSampleCount) return false;
                var tile = samples[tick.TileSampleOffset + used].Tile;
                if (!tile.Known || tile.TileX != x || tile.TileY != y ||
                    !IsFinite(tile.HookCenter) ||
                    tile.MinecartTrack != (tile.TileType == 314)) return false;
                used++;
                var tileLeft = x * 16d;
                var tileTop = y * 16d;
                var overlaps = tick.ProjectileCenterBefore.X + 5d > tileLeft &&
                    tick.ProjectileCenterBefore.X - 5d < tileLeft + 16d &&
                    tick.ProjectileCenterBefore.Y + 5d > tileTop &&
                    tick.ProjectileCenterBefore.Y - 5d < tileTop + 16d;
                if (!overlaps || !NativeCanLatch(in tile)) continue;
                if (!expectLatch || used != tick.TileSampleCount ||
                    tile.TileX != route.IntendedAnchor.TileX ||
                    tile.TileY != route.IntendedAnchor.TileY ||
                    tile.TileType != route.IntendedAnchor.TileType ||
                    !NearlySame(tile.HookCenter, route.IntendedAnchorCenter,
                        route.AnchorTolerance)) return false;
                sampleCursor += used;
                return true;
            }
            if (expectLatch || used != tick.TileSampleCount) return false;
            sampleCursor += used;
            return true;
        }

        private static bool ValidateAttachedFrame(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick, bool mustDetach, bool mustArm)
        {
            if (!tick.ProjectileActiveBefore || tick.ProjectileAiBefore != 2f ||
                !NearlySame(tick.ProjectileCenterBefore, route.IntendedAnchorCenter,
                    route.AnchorTolerance) || tick.TileSampleCount != 0)
                return false;
            var projectile = new BasicHookProjectileObservation
            {
                Known = true, Active = true, Index = 1, Owner = 0,
                Type = ProjectileType, AiStyle = ProjectileAiStyle, AiState = 2f,
                Center = tick.ProjectileCenterBefore
            };
            var link = new BasicHookLinkObservation
            {
                Known = true, AtGrappleMovementEntry = true,
                GrappleCount = 1, FirstProjectileIndex = 1
            };
            BasicHookAttachedTick actual;
            if (!TryApplyAttachedTick(in projectile, in link, in route.IntendedAnchor, 0,
                    tick.PlayerCenterBefore, tick.PlayerVelocityBefore, false,
                    route.JumpSpeed, route.JumpHeight, route.OldStyleParkour,
                    tick.ControlJump, tick.ControlUp, tick.ControlDown,
                    tick.ReleaseJumpBefore, out actual) ||
                actual.Detached != mustDetach ||
                !NearlySame(actual.VelocityAfterGrapple, tick.PlayerVelocityAfter, 0.01f) ||
                actual.ReleaseJumpAfter != tick.ReleaseJumpAfter)
                return false;
            double detachDistance;
            if (!TryDistance(tick.PlayerCenterBefore, route.DetachCenter, out detachDistance))
                return false;
            if (mustDetach)
            {
                return !mustArm && tick.ControlJump && tick.ReleaseJumpBefore &&
                    detachDistance <= route.DetachRadius && !tick.ProjectileActiveAfter &&
                    tick.ProjectileAiAfter == 2f &&
                    NearlySame(tick.ProjectileCenterAfter,
                        tick.ProjectileCenterBefore, 0.01f);
            }
            if (!tick.ProjectileActiveAfter || tick.ProjectileAiAfter != 2f ||
                !NearlySame(tick.ProjectileCenterAfter,
                    tick.ProjectileCenterBefore, 0.01f)) return false;
            if (mustArm)
                return !tick.ControlJump && !tick.ReleaseJumpBefore &&
                    tick.ReleaseJumpAfter && detachDistance <= route.DetachRadius;
            return !actual.Detached && detachDistance > route.DetachRadius;
        }

        private static bool ValidateReturnFrame(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick)
        {
            if (tick.Phase != (route.ReturnProfileSlowFall
                    ? BasicHookEvidencePhase.ReturnFeatherFall
                    : BasicHookEvidencePhase.ReturnBallistic) ||
                tick.ProjectileActiveBefore || tick.ProjectileActiveAfter ||
                tick.TileSampleCount != 0 || tick.ControlJump || tick.ControlUp ||
                tick.ControlDown || !tick.ReleaseJumpAfter)
                return false;
            return ValidateNeutralPlayerMotion(in route, in tick);
        }

        private static bool ValidateMissReturnFrame(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick)
        {
            if (!tick.ProjectileActiveBefore || tick.ProjectileAiBefore != 1f ||
                tick.TileSampleCount != 0 || tick.ControlJump || tick.ControlUp ||
                tick.ControlDown || !tick.ReleaseJumpAfter ||
                !ValidateNeutralPlayerMotion(in route, in tick)) return false;
            double distance;
            if (!TryDistance(tick.ProjectileCenterBefore, tick.PlayerCenterBefore,
                    out distance)) return false;
            if (distance < ReturnKillDistance)
            {
                return !tick.ProjectileActiveAfter && tick.ProjectileAiAfter == 1f &&
                    NearlySame(tick.ProjectileCenterAfter,
                        tick.ProjectileCenterBefore, 0.01f);
            }
            Vec2 returnVelocity;
            Vec2 expectedCenter;
            return tick.ProjectileActiveAfter && tick.ProjectileAiAfter == 1f &&
                TryVectorWithMaximum(tick.PlayerCenterBefore,
                    tick.ProjectileCenterBefore, ReturnSpeed, alwaysNormalize: true,
                    out returnVelocity) &&
                TryAdd(tick.ProjectileCenterBefore, returnVelocity, out expectedCenter) &&
                NearlySame(expectedCenter, tick.ProjectileCenterAfter, 0.02f);
        }

        private static bool ValidateNeutralPlayerMotion(in BasicHookRouteCertificate route,
            in BasicHookTrajectoryTick tick)
        {
            var drag = route.HorizontalSlowdown * (tick.Grounded ? 1f : .5f);
            var expectedX = MoveTowardsZero(tick.PlayerVelocityBefore.X, drag);
            if (!IsFinite(expectedX) || Math.Abs(expectedX - tick.PlayerVelocityAfter.X) > .01f)
                return false;
            if (tick.Grounded)
            {
                return tick.PlayerVelocityAfter.Y == 0f;
            }
            Vec2 expectedVelocity;
            if (!TryAdvanceReturnVelocity(tick.PlayerVelocityBefore, route.Gravity,
                    route.MaximumFallSpeed, route.ReturnProfileSlowFall,
                    tick.ControlUp, tick.ControlDown, out expectedVelocity)) return false;
            expectedVelocity.X = expectedX;
            return NearlySame(expectedVelocity, tick.PlayerVelocityAfter, 0.01f);
        }

        private static float MoveTowardsZero(float value, float delta)
        {
            if (!IsFinite(value) || !IsFiniteNonNegative(delta)) return float.NaN;
            if (Math.Abs(value) <= delta) return 0f;
            return value - Math.Sign(value) * delta;
        }

        private static bool TryAdvanceReturnVelocity(Vec2 before, float gravity,
            float maximumFallSpeed, bool slowFall, bool controlUp, bool controlDown,
            out Vec2 after)
        {
            after = before;
            if (!IsFinite(before) || !IsFiniteNonNegative(gravity) ||
                !IsFinitePositive(maximumFallSpeed)) return false;
            var feather = slowFall && !controlDown;
            var divisor = feather ? (controlUp ? 10f : 3f) : 1f;
            var y = before.Y + gravity / divisor;
            y = Math.Min(maximumFallSpeed, y);
            if (feather && y > maximumFallSpeed / 3f) y = maximumFallSpeed / 3f;
            if (feather && controlUp && y > maximumFallSpeed / 5f)
                y = maximumFallSpeed / 10f;
            after.Y = y;
            return IsFinite(after);
        }

        private static bool ValidateEnvironment(in BasicHookIdentity identity,
            in BasicHookUseContext context, out BasicHookFailure failure)
        {
            failure = BasicHookFailure.UnknownObservation;
            if (!MatchesExactIdentity(in identity))
            {
                failure = BasicHookFailure.IdentityMismatch;
                return false;
            }
            if (!context.Known || !context.LocalPlayerKnown || context.LocalPlayerIndex < 0)
                return false;
            if (context.Dead || context.CrowdControlled || context.Tongued || context.NoItems ||
                context.GrappleAndInteractShared || context.MountActive || !context.NormalGravity ||
                context.GravityControlActive || context.Pulley || context.Wet)
            {
                failure = BasicHookFailure.UnsafeNativeContext;
                return false;
            }
            failure = BasicHookFailure.None;
            return true;
        }

        private static BasicHookRescueDecision Decision(BasicHookRescueStep step,
            BasicHookFailure failure, BasicHookContingency contingency = BasicHookContingency.None)
            => new BasicHookRescueDecision { Step = step, Failure = failure, Contingency = contingency };

        private static bool InLoopEnvelope(in BasicHookVerifiedRoute route,
            Vec2 position, Vec2 velocity)
        {
            return Math.Abs((double)position.X - route.LowConfigLoopCenter.X) <= route.LoopHalfWidth &&
                Math.Abs((double)position.Y - route.LowConfigLoopCenter.Y) <= route.LoopHalfHeight &&
                Math.Abs((double)velocity.X) <= route.LoopMaximumAbsVelocityX &&
                Math.Abs((double)velocity.Y) <= route.LoopMaximumAbsVelocityY;
        }

        private static bool InLoopEnvelope(in BasicHookRouteCertificate route,
            Vec2 position, Vec2 velocity)
        {
            return Math.Abs((double)position.X - route.LowConfigLoopCenter.X) <= route.LoopHalfWidth &&
                Math.Abs((double)position.Y - route.LowConfigLoopCenter.Y) <= route.LoopHalfHeight &&
                Math.Abs((double)velocity.X) <= route.LoopMaximumAbsVelocityX &&
                Math.Abs((double)velocity.Y) <= route.LoopMaximumAbsVelocityY;
        }

        private static bool IsVerifiedRoute(in BasicHookVerifiedRoute route, bool slowFall)
        {
            return route.Stamp == VerifiedRouteStamp && route.ReturnProfileSlowFall == slowFall &&
                IsSafetyAnchor(in route.IntendedAnchor) &&
                IsFiniteNonNegative(route.MaximumAcceptedRiskScore);
        }

        private static bool IsPointOnPullSegment(Vec2 start, Vec2 anchor, Vec2 point)
        {
            var dx = (double)anchor.X - start.X;
            var dy = (double)anchor.Y - start.Y;
            var px = (double)point.X - start.X;
            var py = (double)point.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (!IsFinite(lengthSquared) || lengthSquared <= 0d) return false;
            var projection = (px * dx + py * dy) / lengthSquared;
            if (!IsFinite(projection) || projection < 0d || projection > 1d) return false;
            var nearestX = dx * projection;
            var nearestY = dy * projection;
            var errorX = px - nearestX;
            var errorY = py - nearestY;
            var errorSquared = errorX * errorX + errorY * errorY;
            return IsFinite(errorSquared) && errorSquared <= 1d;
        }

        private static bool TryVectorWithMaximum(Vec2 target, Vec2 origin, float maximum,
            bool alwaysNormalize, out Vec2 result)
        {
            result = default(Vec2);
            if (!IsFinite(target) || !IsFinite(origin) || !IsFinitePositive(maximum)) return false;
            var dx = (double)target.X - origin.X;
            var dy = (double)target.Y - origin.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (!IsFinite(lengthSquared) || lengthSquared <= 0d) return !alwaysNormalize;
            var length = Math.Sqrt(lengthSquared);
            var scale = alwaysNormalize || length > maximum ? maximum / length : 1d;
            var x = dx * scale;
            var y = dy * scale;
            if (!IsFinite(x) || !IsFinite(y) || Math.Abs(x) > float.MaxValue ||
                Math.Abs(y) > float.MaxValue) return false;
            result = new Vec2((float)x, (float)y);
            return IsFinite(result);
        }

        private static bool TryAdd(Vec2 left, Vec2 right, out Vec2 result)
        {
            result = default(Vec2);
            if (!IsFinite(left) || !IsFinite(right)) return false;
            var x = (double)left.X + right.X;
            var y = (double)left.Y + right.Y;
            if (!IsFinite(x) || !IsFinite(y) || Math.Abs(x) > float.MaxValue ||
                Math.Abs(y) > float.MaxValue) return false;
            result = new Vec2((float)x, (float)y);
            return IsFinite(result);
        }

        private static bool NearlySame(Vec2 left, Vec2 right, float tolerance)
        {
            double distance;
            return IsFiniteNonNegative(tolerance) && TryDistance(left, right, out distance) &&
                distance <= tolerance;
        }

        private static bool TryDistance(Vec2 left, Vec2 right, out double distance)
        {
            distance = double.NaN;
            if (!IsFinite(left) || !IsFinite(right)) return false;
            var dx = (double)left.X - right.X;
            var dy = (double)left.Y - right.Y;
            var squared = dx * dx + dy * dy;
            if (!IsFinite(squared) || squared < 0d) return false;
            distance = Math.Sqrt(squared);
            return IsFinite(distance);
        }

        private static double LengthSquared(Vec2 value)
        {
            var x = (double)value.X;
            var y = (double)value.Y;
            return x * x + y * y;
        }

        private static bool IsFinite(Vec2 value) => IsFinite(value.X) && IsFinite(value.Y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinitePositive(float value) => IsFinite(value) && value > 0f;
        private static bool IsFiniteNonNegative(float value) => IsFinite(value) && value >= 0f;
    }
}
