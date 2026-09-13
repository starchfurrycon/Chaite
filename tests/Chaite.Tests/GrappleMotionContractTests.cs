using System;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// Standalone suite so the grapple work can be compiled in isolation while the
    /// shared test project/Program files are being edited by other workstreams.
    /// </summary>
    internal static class GrappleMotionContractTests
    {
        public static int RunAll()
        {
            ExactIdentityIsRequired();
            LaunchVelocityUsesObservedItemSpeed();
            AnchorHasNativeAndNarrowSafetyPredicates();
            ProjectileStateRequiresNativeIdentityAndFiniteCenter();
            PullUsesRealLatchAndElevenPixelCap();
            AttachedTickUsesNativeReleaseEdge();
            RouteCertificateRejectsUnmodeledCombinations();
            ImmediateEvadeWithoutReturnIsRejected();
            FiniteTrajectoryEvidenceRejectsMalformedOrDivergentFrames();
            RescueStateMachineCoversFlightMissLatchDetachAndReturn();
            NonFiniteInputsAlwaysFailClosed();
            return 11;
        }

        private static BasicHookIdentity Identity()
        {
            return new BasicHookIdentity
            {
                Known = true,
                ResolvedByQuickGrapple = true,
                ProjectileMarkedAsHook = true,
                ItemType = 84,
                ProjectileType = 13,
                ProjectileAiStyle = 7,
                ShootSpeed = 11.5f,
                UseStyle = 5,
                UseAnimation = 20,
                UseTime = 20,
                NoUseGraphic = true,
                NoMelee = true,
                ProjectileWidth = 18,
                ProjectileHeight = 18,
                ProjectileNetImportant = true,
                ProjectileTileCollide = false,
                MaximumSimultaneousHooks = 1
            };
        }

        private static BasicHookAnchorObservation Anchor(Vec2 center)
        {
            return new BasicHookAnchorObservation
            {
                Known = true,
                TileX = 12,
                TileY = 34,
                TileType = 1,
                NativeActive = true,
                NativeSolid = true,
                HookCenter = center
            };
        }

        private static BasicHookProjectileObservation Projectile(float ai, Vec2 center)
        {
            return new BasicHookProjectileObservation
            {
                Known = true,
                Active = true,
                Index = 41,
                Owner = 0,
                Type = 13,
                AiStyle = 7,
                AiState = ai,
                Center = center
            };
        }

        private static BasicHookLinkObservation Link()
            => new BasicHookLinkObservation
            {
                Known = true,
                AtGrappleMovementEntry = true,
                GrappleCount = 1,
                FirstProjectileIndex = 41
            };

        private static BasicHookUseContext Context(bool slowFall = false)
        {
            return new BasicHookUseContext
            {
                Known = true,
                LocalPlayerKnown = true,
                LocalPlayerIndex = 0,
                NormalGravity = true,
                SlowFall = slowFall,
                ReleaseHook = true,
                ItemStartGateKnown = true,
                ItemStartGateOpen = true
            };
        }

        private static BasicHookRouteCertificate Route(bool slowFall = false)
        {
            var anchor = Anchor(new Vec2(200f, 200f));
            anchor.TileX = 12;
            anchor.TileY = 12;
            var route = new BasicHookRouteCertificate
            {
                Known = true,
                FireCenter = new Vec2(100f, 200f),
                IntendedAnchorCenter = anchor.HookCenter,
                DetachCenter = new Vec2(155f, 200f),
                LowConfigLoopCenter = new Vec2(155f, 202f),
                IntendedAnchor = anchor,
                ReturnProfileSlowFall = slowFall,
                MaximumAcceptedRiskScore = 25f,
                Gravity = .4f,
                MaximumFallSpeed = 10f,
                JumpSpeed = 5.01f,
                JumpHeight = 15,
                AnchorTolerance = 1f,
                DetachRadius = 1f,
                LoopHalfWidth = 60f,
                LoopHalfHeight = 10f,
                LoopMaximumAbsVelocityX = 12f,
                LoopMaximumAbsVelocityY = 3f,
                LowConfigLoopEpoch = 7,
                LatchDeadlineTicks = 32,
                DetachDeadlineTicks = 90,
                ReentryDeadlineTicks = 180
            };
            route.Evidence = BuildEvidence(in route);
            return route;
        }

        private static BasicHookRescueObservation Observation()
        {
            return new BasicHookRescueObservation
            {
                Known = true,
                OriginalLowConfigLoopStillValid = true,
                LiveThreatFieldKnown = true,
                CertifiedTrajectoryStillSafe = true,
                LiveWorstCaseRiskScore = 20f,
                LowConfigLoopEpoch = 7,
                PlayerCenter = new Vec2(100f, 200f),
                PlayerVelocity = new Vec2(0f, 0f)
            };
        }

        private static BasicHookTrajectoryEvidence BuildEvidence(in BasicHookRouteCertificate route)
        {
            var success = new BasicHookTrajectoryTick[64];
            var successTiles = new BasicHookTileSample[1024];
            var successCount = 0;
            var successTileCount = 0;
            var player = route.FireCenter;
            var playerVelocity = new Vec2(0f, 0f);
            var projectile = route.FireCenter;
            var releaseJump = true;

            for (var tickIndex = 0; tickIndex < 8; tickIndex++)
            {
                var afterProjectile = new Vec2(projectile.X + 11.5f, projectile.Y);
                var tick = Tick(tickIndex, BasicHookEvidencePhase.Outbound,
                    player, playerVelocity, player, playerVelocity, true,
                    projectile, afterProjectile, true, true, 0f, 0f,
                    releaseJump, releaseJump);
                AppendScan(ref tick, successTiles, ref successTileCount,
                    route.IntendedAnchor, targetActive: true, stopAtTarget: false);
                success[successCount++] = tick;
                projectile = afterProjectile;
            }
            {
                var tick = Tick(8, BasicHookEvidencePhase.Latch,
                    player, playerVelocity, player, playerVelocity, true,
                    projectile, route.IntendedAnchorCenter, true, true, 0f, 2f,
                    releaseJump, releaseJump);
                AppendScan(ref tick, successTiles, ref successTileCount,
                    route.IntendedAnchor, targetActive: true, stopAtTarget: true);
                success[successCount++] = tick;
                projectile = route.IntendedAnchorCenter;
            }
            for (var tickIndex = 9; tickIndex <= 13; tickIndex++)
            {
                var delta = route.IntendedAnchorCenter.X - player.X;
                var pullX = Math.Min(11f, delta);
                var afterVelocity = new Vec2(pullX, 0f);
                var afterPlayer = new Vec2(player.X + pullX, player.Y);
                success[successCount++] = Tick(tickIndex, BasicHookEvidencePhase.Pull,
                    player, playerVelocity, afterPlayer, afterVelocity, true,
                    projectile, projectile, true, true, 2f, 2f,
                    releaseJump, true);
                player = afterPlayer;
                playerVelocity = afterVelocity;
                releaseJump = true;
            }
            {
                var afterVelocity = new Vec2(11f, .01f);
                var afterPlayer = new Vec2(player.X + afterVelocity.X,
                    player.Y + afterVelocity.Y);
                var tick = Tick(14, BasicHookEvidencePhase.DetachPulse,
                    player, playerVelocity, afterPlayer, afterVelocity, false,
                    projectile, projectile, true, false, 2f, 2f,
                    true, false);
                tick.ControlJump = true;
                success[successCount++] = tick;
                player = afterPlayer;
                playerVelocity = afterVelocity;
                releaseJump = false;
            }
            for (var tickIndex = 15; tickIndex <= 18; tickIndex++)
            {
                var divisor = route.ReturnProfileSlowFall ? 3f : 1f;
                var afterVelocity = new Vec2(playerVelocity.X,
                    playerVelocity.Y + route.Gravity / divisor);
                var afterPlayer = new Vec2(player.X + afterVelocity.X,
                    player.Y + afterVelocity.Y);
                success[successCount++] = Tick(tickIndex,
                    route.ReturnProfileSlowFall ? BasicHookEvidencePhase.ReturnFeatherFall :
                        BasicHookEvidencePhase.ReturnBallistic,
                    player, playerVelocity, afterPlayer, afterVelocity, false,
                    projectile, projectile, false, false, 2f, 2f,
                    releaseJump, true);
                player = afterPlayer;
                playerVelocity = afterVelocity;
                releaseJump = true;
            }

            var miss = new BasicHookTrajectoryTick[96];
            var missTiles = new BasicHookTileSample[2048];
            var missCount = 0;
            var missTileCount = 0;
            player = route.FireCenter;
            playerVelocity = new Vec2(0f, 0f);
            projectile = route.FireCenter;
            releaseJump = true;
            for (var tickIndex = 0; tickIndex <= 26; tickIndex++)
            {
                var afterProjectile = new Vec2(projectile.X + 11.5f, projectile.Y);
                var tick = Tick(tickIndex, BasicHookEvidencePhase.Outbound,
                    player, playerVelocity, player, playerVelocity, true,
                    projectile, afterProjectile, true, true, 0f, 0f,
                    releaseJump, releaseJump);
                AppendScan(ref tick, missTiles, ref missTileCount,
                    route.IntendedAnchor, targetActive: false, stopAtTarget: false);
                miss[missCount++] = tick;
                projectile = afterProjectile;
            }
            {
                var afterProjectile = new Vec2(projectile.X + 11.5f, projectile.Y);
                var tick = Tick(27, BasicHookEvidencePhase.OutboundRangeTransition,
                    player, playerVelocity, player, playerVelocity, true,
                    projectile, afterProjectile, true, true, 0f, 1f,
                    releaseJump, releaseJump);
                AppendScan(ref tick, missTiles, ref missTileCount,
                    route.IntendedAnchor, targetActive: false, stopAtTarget: false);
                miss[missCount++] = tick;
                projectile = afterProjectile;
            }
            for (var tickIndex = 28; tickIndex < 80; tickIndex++)
            {
                var distance = projectile.X - player.X;
                var killed = distance < 24f;
                var afterProjectile = killed ? projectile :
                    new Vec2(projectile.X - 11f, projectile.Y);
                miss[missCount++] = Tick(tickIndex, BasicHookEvidencePhase.MissReturn,
                    player, playerVelocity, player, playerVelocity, true,
                    projectile, afterProjectile, true, !killed, 1f, 1f,
                    releaseJump, true);
                releaseJump = true;
                projectile = afterProjectile;
                if (killed) break;
            }

            return new BasicHookTrajectoryEvidence
            {
                Known = true,
                WorldMaxTilesX = 1000,
                WorldMaxTilesY = 1000,
                SuccessTicks = success,
                SuccessTickCount = successCount,
                SuccessTileSamples = successTiles,
                SuccessTileSampleCount = successTileCount,
                MissTicks = miss,
                MissTickCount = missCount,
                MissTileSamples = missTiles,
                MissTileSampleCount = missTileCount
            };
        }

        private static BasicHookTrajectoryTick Tick(int index, BasicHookEvidencePhase phase,
            Vec2 playerBefore, Vec2 velocityBefore, Vec2 playerAfter, Vec2 velocityAfter,
            bool grounded, Vec2 projectileBefore, Vec2 projectileAfter,
            bool projectileActiveBefore, bool projectileActiveAfter,
            float aiBefore, float aiAfter, bool releaseBefore, bool releaseAfter)
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
                PlayerPathKnown = true,
                PlayerPathClear = true,
                PlatformFree = true,
                SupportStateKnown = true,
                Grounded = grounded,
                ThreatScoreKnown = true,
                RiskScore = 20f,
                ProjectileActiveBefore = projectileActiveBefore,
                ProjectileActiveAfter = projectileActiveAfter,
                ProjectileAiBefore = aiBefore,
                ProjectileAiAfter = aiAfter,
                ProjectileCenterBefore = projectileBefore,
                ProjectileCenterAfter = projectileAfter,
                ReleaseJumpBefore = releaseBefore,
                ReleaseJumpAfter = releaseAfter
            };
        }

        private static void AppendScan(ref BasicHookTrajectoryTick tick,
            BasicHookTileSample[] samples, ref int sampleCount,
            BasicHookAnchorObservation target, bool targetActive, bool stopAtTarget)
        {
            tick.TileSampleOffset = sampleCount;
            var startX = (int)((tick.ProjectileCenterBefore.X - 21f) / 16f);
            var endX = (int)((tick.ProjectileCenterBefore.X + 37f) / 16f);
            var startY = (int)((tick.ProjectileCenterBefore.Y - 21f) / 16f);
            var endY = (int)((tick.ProjectileCenterBefore.Y + 37f) / 16f);
            if (startX < 0) startX = 0;
            if (startY < 0) startY = 0;
            if (endX > 1000) endX = 1000;
            if (endY > 1000) endY = 1000;
            for (var x = startX; x < endX; x++)
            for (var y = startY; y < endY; y++)
            {
                var isTarget = x == target.TileX && y == target.TileY;
                var tile = new BasicHookAnchorObservation
                {
                    Known = true,
                    TileX = x,
                    TileY = y,
                    TileType = isTarget ? target.TileType : 0,
                    NativeActive = isTarget && targetActive,
                    NativeSolid = isTarget && targetActive,
                    HookCenter = isTarget ? target.HookCenter :
                        new Vec2(x * 16f + 8f, y * 16f + 8f)
                };
                samples[sampleCount++].Tile = tile;
                if (stopAtTarget && isTarget)
                {
                    tick.TileSampleCount = sampleCount - tick.TileSampleOffset;
                    return;
                }
            }
            tick.TileSampleCount = sampleCount - tick.TileSampleOffset;
        }

        private static BasicHookVerifiedRoute Verified(in BasicHookRouteCertificate route,
            bool slowFall = false)
        {
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            if (!BasicHookMotion.TryVerifyRoute(in route, slowFall, out proof, out failure))
                throw new InvalidOperationException("route proof failed: " + failure);
            return proof;
        }

        private static void ExactIdentityIsRequired()
        {
            var identity = Identity();
            True(BasicHookMotion.MatchesExactIdentity(in identity));
            identity.Known = false;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
            identity = Identity(); identity.ResolvedByQuickGrapple = false;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
            identity = Identity(); identity.ItemType = 1234;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
            identity = Identity(); identity.ProjectileType = 230;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
            identity = Identity(); identity.ShootSpeed = 11f;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
            identity = Identity(); identity.MaximumSimultaneousHooks = 3;
            False(BasicHookMotion.MatchesExactIdentity(in identity));
        }

        private static void LaunchVelocityUsesObservedItemSpeed()
        {
            var identity = Identity();
            Vec2 velocity;
            True(BasicHookMotion.TryGetLaunchVelocity(in identity, new Vec2(10f, 20f),
                new Vec2(13f, 24f), out velocity));
            Near(6.9f, velocity.X);
            Near(9.2f, velocity.Y);
            False(BasicHookMotion.TryGetLaunchVelocity(in identity, new Vec2(10f, 20f),
                new Vec2(10f, 20f), out velocity));
        }

        private static void AnchorHasNativeAndNarrowSafetyPredicates()
        {
            var solid = Anchor(new Vec2(200f, 0f));
            True(BasicHookMotion.NativeCanLatch(in solid));
            True(BasicHookMotion.IsSafetyAnchor(in solid));

            var platform = solid; platform.Platform = true; platform.SolidTop = true;
            True(BasicHookMotion.NativeCanLatch(in platform));
            False(BasicHookMotion.IsSafetyAnchor(in platform));
            var track = solid; track.NativeSolid = false; track.MinecartTrack = true; track.TileType = 314;
            True(BasicHookMotion.NativeCanLatch(in track));
            False(BasicHookMotion.IsSafetyAnchor(in track));
            var blacklisted = solid; blacklisted.Blacklisted = true;
            False(BasicHookMotion.NativeCanLatch(in blacklisted));
            var unknown = solid; unknown.Known = false;
            False(BasicHookMotion.NativeCanLatch(in unknown));
        }

        private static void ProjectileStateRequiresNativeIdentityAndFiniteCenter()
        {
            BasicHookFailure failure;
            var projectile = Projectile(0f, new Vec2(10f, 0f));
            Equal(BasicHookProjectilePhase.Outbound,
                BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
            Equal(BasicHookFailure.None, failure);
            projectile.AiState = 1f;
            Equal(BasicHookProjectilePhase.ReturningAfterMiss,
                BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
            projectile.AiState = 2f;
            Equal(BasicHookProjectilePhase.Latched,
                BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
            projectile.Owner = 1;
            Equal(BasicHookProjectilePhase.Unsupported,
                BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
            Equal(BasicHookFailure.IdentityMismatch, failure);
            projectile = Projectile(3f, new Vec2(10f, 0f));
            Equal(BasicHookProjectilePhase.Unsupported,
                BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
        }

        private static void PullUsesRealLatchAndElevenPixelCap()
        {
            var anchor = Anchor(new Vec2(100f, 0f));
            var projectile = Projectile(2f, anchor.HookCenter);
            var link = Link();
            Vec2 velocity;
            True(BasicHookMotion.TryGetPullVelocity(in projectile, in link, in anchor,
                0, new Vec2(0f, 0f), out velocity));
            Near(11f, velocity.X); Near(0f, velocity.Y);
            True(BasicHookMotion.TryGetPullVelocity(in projectile, in link, in anchor,
                0, new Vec2(96f, 3f), out velocity));
            Near(4f, velocity.X); Near(-3f, velocity.Y);

            var outbound = projectile; outbound.AiState = 0f;
            False(BasicHookMotion.TryGetPullVelocity(in outbound, in link, in anchor,
                0, new Vec2(0f, 0f), out velocity));
            var wrongLink = link; wrongLink.FirstProjectileIndex = 42;
            False(BasicHookMotion.TryGetPullVelocity(in projectile, in wrongLink, in anchor,
                0, new Vec2(0f, 0f), out velocity));
            var wrongBoundary = link; wrongBoundary.AtGrappleMovementEntry = false;
            False(BasicHookMotion.TryGetPullVelocity(in projectile, in wrongBoundary, in anchor,
                0, new Vec2(0f, 0f), out velocity));
        }

        private static void AttachedTickUsesNativeReleaseEdge()
        {
            var anchor = Anchor(new Vec2(100f, 0f));
            var projectile = Projectile(2f, anchor.HookCenter);
            var link = Link();
            BasicHookAttachedTick result;

            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(3f, 0f), false, 5.01f, 15, false,
                false, false, false, false, out result));
            False(result.Detached); True(result.ReleaseJumpAfter); Near(11f, result.VelocityAfterGrapple.X);

            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(3f, 0f), false, 5.01f, 15, false,
                true, false, false, true, out result));
            True(result.Detached); False(result.ReleaseJumpAfter); Near(0.01f, result.VelocityAfterGrapple.Y);
            Equal(0, result.JumpTicksAfter); True(result.RefreshesDoubleJumps);

            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(0f, 0f), false, 5.01f, 15, false,
                true, false, false, true, out result));
            Near(-5.01f, result.VelocityAfterGrapple.Y); Equal(15, result.JumpTicksAfter);

            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(0f, 0f), false, 5.01f, 15, false,
                true, false, true, true, out result));
            Near(0.01f, result.VelocityAfterGrapple.Y); Equal(0, result.JumpTicksAfter);

            anchor = Anchor(new Vec2(0f, 100f)); projectile = Projectile(2f, anchor.HookCenter);
            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(0f, 0f), false, 5.01f, 15, false,
                true, false, false, true, out result));
            Near(11.01f, result.VelocityAfterGrapple.Y);
            True(result.GoingDownWithGrapple);
            True(BasicHookMotion.TryApplyAttachedTick(in projectile, in link, in anchor, 0,
                new Vec2(0f, 0f), new Vec2(0f, 0f), false, 5.01f, 15, false,
                true, true, false, true, out result));
            Near(-5.01f, result.VelocityAfterGrapple.Y);
        }

        private static void RouteCertificateRejectsUnmodeledCombinations()
        {
            var identity = Identity(); var context = Context(); var route = Route();
            var proof = Verified(in route);
            BasicHookFailure failure;
            True(BasicHookMotion.CanBeginRescue(in identity, in context, in proof, out failure));
            Equal(BasicHookFailure.None, failure);

            var mounted = context; mounted.MountActive = true;
            False(BasicHookMotion.CanBeginRescue(in identity, in mounted, in proof, out failure));
            Equal(BasicHookFailure.UnsafeNativeContext, failure);
            var inverted = context; inverted.NormalGravity = false; inverted.GravityControlActive = true;
            False(BasicHookMotion.CanBeginRescue(in identity, in inverted, in proof, out failure));
            var wet = context; wet.Wet = true;
            False(BasicHookMotion.CanBeginRescue(in identity, in wet, in proof, out failure));

            var platform = Route(); platform.Evidence.SuccessTicks[9].PlatformFree = false;
            False(BasicHookMotion.ValidateRoute(in platform, false, out failure));
            var noReturn = Route(); noReturn.LowConfigLoopCenter = new Vec2(900f, 900f);
            False(BasicHookMotion.ValidateRoute(in noReturn, false, out failure));
            var noMissRecovery = Route(); noMissRecovery.Evidence.MissTickCount--;
            False(BasicHookMotion.ValidateRoute(in noMissRecovery, false, out failure));
            var incompleteScore = Route(); incompleteScore.Evidence.SuccessTicks[0].ThreatScoreKnown = false;
            False(BasicHookMotion.ValidateRoute(in incompleteScore, false, out failure));
            var excessiveRisk = Route(); excessiveRisk.Evidence.SuccessTicks[0].RiskScore = 26f;
            False(BasicHookMotion.ValidateRoute(in excessiveRisk, false, out failure));
            var tooFar = route; tooFar.IntendedAnchorCenter = new Vec2(301f, 0f);
            tooFar.IntendedAnchor.HookCenter = tooFar.IntendedAnchorCenter;
            tooFar.DetachCenter = new Vec2(120f, 0f);
            False(BasicHookMotion.ValidateRoute(in tooFar, false, out failure));

            var feather = Context(true);
            False(BasicHookMotion.CanBeginRescue(in identity, in feather, in proof, out failure));
            var featherRoute = Route(true);
            var featherProof = Verified(in featherRoute, true);
            True(BasicHookMotion.CanBeginRescue(in identity, in feather, in featherProof, out failure));
        }

        private static void ImmediateEvadeWithoutReturnIsRejected()
        {
            BasicHookFailure failure;
            var route = Route();
            route.LowConfigLoopCenter = new Vec2(900f, 900f);
            False(BasicHookMotion.ValidateRoute(in route, false, out failure));
            Equal(BasicHookFailure.UnsafeRoute, failure);
        }

        private static void FiniteTrajectoryEvidenceRejectsMalformedOrDivergentFrames()
        {
            BasicHookFailure failure;

            var wrongFirstLatch = Route();
            var latch = FindSuccessTick(in wrongFirstLatch, BasicHookEvidencePhase.Latch);
            var latchTick = wrongFirstLatch.Evidence.SuccessTicks[latch];
            var inserted = false;
            for (var index = latchTick.TileSampleOffset;
                 index < latchTick.TileSampleOffset + latchTick.TileSampleCount; index++)
            {
                var tile = wrongFirstLatch.Evidence.SuccessTileSamples[index].Tile;
                if (tile.TileX != 11 || tile.TileY != 12) continue;
                tile.NativeActive = true;
                tile.NativeSolid = true;
                tile.TileType = 1;
                wrongFirstLatch.Evidence.SuccessTileSamples[index].Tile = tile;
                inserted = true;
                break;
            }
            True(inserted);
            False(BasicHookMotion.ValidateRoute(in wrongFirstLatch, false, out failure));

            var brokenOutbound = Route();
            brokenOutbound.Evidence.SuccessTicks[2].ProjectileCenterAfter.X += 1f;
            False(BasicHookMotion.ValidateRoute(in brokenOutbound, false, out failure));

            var wrongPull = Route();
            var pull = FindSuccessTick(in wrongPull, BasicHookEvidencePhase.Pull);
            var pullTick = wrongPull.Evidence.SuccessTicks[pull];
            pullTick.PlayerVelocityAfter.X = 10f;
            pullTick.PlayerCenterAfter = new Vec2(
                pullTick.PlayerCenterBefore.X + pullTick.PlayerVelocityAfter.X,
                pullTick.PlayerCenterBefore.Y + pullTick.PlayerVelocityAfter.Y);
            wrongPull.Evidence.SuccessTicks[pull] = pullTick;
            False(BasicHookMotion.ValidateRoute(in wrongPull, false, out failure));

            var wrongMissSpeed = Route();
            var missReturn = FindMissTick(in wrongMissSpeed, BasicHookEvidencePhase.MissReturn);
            wrongMissSpeed.Evidence.MissTicks[missReturn].ProjectileCenterAfter.X += 1f;
            False(BasicHookMotion.ValidateRoute(in wrongMissSpeed, false, out failure));

            var wrongReleaseEdge = Route();
            var detach = FindSuccessTick(in wrongReleaseEdge, BasicHookEvidencePhase.DetachPulse);
            wrongReleaseEdge.Evidence.SuccessTicks[detach].ReleaseJumpBefore = false;
            False(BasicHookMotion.ValidateRoute(in wrongReleaseEdge, false, out failure));

            var wrongFeatherGravity = Route(true);
            var featherReturn = FindSuccessTick(in wrongFeatherGravity,
                BasicHookEvidencePhase.ReturnFeatherFall);
            var featherTick = wrongFeatherGravity.Evidence.SuccessTicks[featherReturn];
            featherTick.PlayerVelocityAfter.Y = featherTick.PlayerVelocityBefore.Y +
                wrongFeatherGravity.Gravity;
            featherTick.PlayerCenterAfter.Y = featherTick.PlayerCenterBefore.Y +
                featherTick.PlayerVelocityAfter.Y;
            wrongFeatherGravity.Evidence.SuccessTicks[featherReturn] = featherTick;
            False(BasicHookMotion.ValidateRoute(in wrongFeatherGravity, true, out failure));

            var wrongFinalEnvelope = Route();
            wrongFinalEnvelope.LowConfigLoopCenter = new Vec2(900f, 900f);
            False(BasicHookMotion.ValidateRoute(in wrongFinalEnvelope, false, out failure));

            var nullTicks = Route(); nullTicks.Evidence.SuccessTicks = null;
            False(BasicHookMotion.ValidateRoute(in nullTicks, false, out failure));
            var excessiveCount = Route();
            excessiveCount.Evidence.MissTickCount = excessiveCount.Evidence.MissTicks.Length + 1;
            False(BasicHookMotion.ValidateRoute(in excessiveCount, false, out failure));
            var notANumber = Route(); notANumber.Evidence.MissTicks[0].RiskScore = float.NaN;
            False(BasicHookMotion.ValidateRoute(in notANumber, false, out failure));
        }

        private static int FindSuccessTick(in BasicHookRouteCertificate route,
            BasicHookEvidencePhase phase)
        {
            for (var index = 0; index < route.Evidence.SuccessTickCount; index++)
                if (route.Evidence.SuccessTicks[index].Phase == phase) return index;
            throw new InvalidOperationException("success evidence phase not found: " + phase);
        }

        private static int FindMissTick(in BasicHookRouteCertificate route,
            BasicHookEvidencePhase phase)
        {
            for (var index = 0; index < route.Evidence.MissTickCount; index++)
                if (route.Evidence.MissTicks[index].Phase == phase) return index;
            throw new InvalidOperationException("miss evidence phase not found: " + phase);
        }

        private static void RescueStateMachineCoversFlightMissLatchDetachAndReturn()
        {
            var identity = Identity(); var context = Context(); var route = Route();
            var proof = Verified(in route);
            var observation = Observation();
            Decision(BasicHookRescueStep.ReadyToFire, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));

            observation.ShotIssued = true; observation.TicksSinceShot = 1;
            Decision(BasicHookRescueStep.AwaitProjectile, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.TicksSinceShot = 3;
            observation.PlayerCenter = new Vec2(80f, 80f);
            Decision(BasicHookRescueStep.FollowCertifiedMissRecovery, BasicHookFailure.None,
                BasicHookContingency.ShotNotObserved,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));

            observation.ProjectileObserved = true;
            observation.Projectile = Projectile(0f, new Vec2(23f, 0f));
            observation.PlayerCenter = new Vec2(0f, 0f);
            Decision(BasicHookRescueStep.AwaitLatch, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.Projectile.AiState = 1f;
            observation.PlayerCenter = new Vec2(80f, 80f);
            Decision(BasicHookRescueStep.FollowCertifiedMissRecovery, BasicHookFailure.None,
                BasicHookContingency.HookMissed,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));

            observation.Projectile = Projectile(2f, route.IntendedAnchorCenter);
            observation.Link = Link(); observation.Anchor = route.IntendedAnchor;
            observation.TicksSinceShot = 9;
            observation.FirstLatchTickKnown = true; observation.FirstLatchTick = 9;
            observation.PlayerCenter = new Vec2(120f, 200f);
            Decision(BasicHookRescueStep.Pulling, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.PlayerCenter = route.DetachCenter;
            Decision(BasicHookRescueStep.ArmJumpRelease, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.ReleaseJump = true;
            Decision(BasicHookRescueStep.PulseJumpToDetach, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));

            observation.DetachObserved = true; observation.ProjectileObserved = false;
            observation.PlayerCenter = new Vec2(80f, 80f); observation.PlayerVelocity = new Vec2(-5f, 2f);
            Decision(BasicHookRescueStep.FollowCertifiedReturn, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.PlayerCenter = route.LowConfigLoopCenter; observation.PlayerVelocity = new Vec2(2f, -1f);
            Decision(BasicHookRescueStep.FollowCertifiedReturn, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.TicksSinceShot = route.Evidence.SuccessTicks[
                route.Evidence.SuccessTickCount - 1].Tick + 1;
            Decision(BasicHookRescueStep.ReenteredLowConfigLoop, BasicHookFailure.None,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
            observation.LowConfigLoopEpoch++;
            Decision(BasicHookRescueStep.Abort, BasicHookFailure.LowConfigLoopChanged,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
        }

        private static void NonFiniteInputsAlwaysFailClosed()
        {
            var identity = Identity(); var context = Context(); var route = Route();
            var proof = Verified(in route);
            BasicHookFailure failure;
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var malformed = identity; malformed.ShootSpeed = bad;
                False(BasicHookMotion.MatchesExactIdentity(in malformed));
                Vec2 velocity;
                False(BasicHookMotion.TryGetLaunchVelocity(in identity, new Vec2(bad, 0f),
                    new Vec2(1f, 1f), out velocity));
                var badRoute = route; badRoute.DetachCenter.X = bad;
                False(BasicHookMotion.ValidateRoute(in badRoute, false, out failure));
                var projectile = Projectile(2f, new Vec2(bad, 0f));
                Equal(BasicHookProjectilePhase.Unsupported,
                    BasicHookMotion.ClassifyProjectile(in projectile, 0, out failure));
                Equal(BasicHookFailure.InvalidNumber, failure);
            }
            var observation = Observation(); observation.PlayerVelocity.X = float.NaN;
            Decision(BasicHookRescueStep.Abort, BasicHookFailure.UnknownObservation,
                BasicHookMotion.Decide(in identity, in context, in proof, in observation));
        }

        private static void Decision(BasicHookRescueStep step, BasicHookFailure failure,
            BasicHookRescueDecision actual)
        {
            Decision(step, failure, BasicHookContingency.None, actual);
        }

        private static void Decision(BasicHookRescueStep step, BasicHookFailure failure,
            BasicHookContingency contingency, BasicHookRescueDecision actual)
        {
            Equal(step, actual.Step); Equal(failure, actual.Failure);
            Equal(contingency, actual.Contingency);
        }

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > 0.001f)
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("expected true");
        }

        private static void False(bool value) => True(!value);

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }
    }
}
