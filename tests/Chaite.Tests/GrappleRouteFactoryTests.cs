using System;
using System.Collections.Generic;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class GrappleRouteFactoryTests
    {
        public static int RunAll()
        {
            CompleteWorldSnapshotBuildsBothFiniteBranches();
            EarlierNativeLatchRejectsTheIntendedAnchor();
            UnknownOrPlatformCorridorFailsClosed();
            NumericallyScoredThreatRejectsTheRoute();
            InvalidBuffersAndNumbersFailClosed();
            FreshThreatsRescoreOnlyTheStillPossibleBranches();
            MalformedLiveThreatsFailClosed();
            NativeProjectileMotionProtectsBothThreatScoringPasses();
            return 8;
        }

        private static void CompleteWorldSnapshotBuildsBothFiniteBranches()
        {
            var world = World();
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            True(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            True(proof.Known);
            Equal(BasicHookFailure.None, failure);
            True(route.Evidence.SuccessTickCount > 10);
            True(route.Evidence.MissTickCount > route.Evidence.SuccessTickCount);
            Equal(BasicHookEvidencePhase.Latch,
                route.Evidence.SuccessTicks[Find(route.Evidence.SuccessTicks,
                    route.Evidence.SuccessTickCount, BasicHookEvidencePhase.Latch)].Phase);
            Equal(BasicHookEvidencePhase.DetachPulse,
                route.Evidence.SuccessTicks[Find(route.Evidence.SuccessTicks,
                    route.Evidence.SuccessTickCount, BasicHookEvidencePhase.DetachPulse)].Phase);
            False(route.Evidence.MissTicks[route.Evidence.MissTickCount - 1]
                .ProjectileActiveAfter);
            True(BasicHookMotion.ValidateRoute(in route, false, out failure));
        }

        private static void EarlierNativeLatchRejectsTheIntendedAnchor()
        {
            var world = World();
            world.HasBlockingTile = true;
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            False(proof.Known);
        }

        private static void UnknownOrPlatformCorridorFailsClosed()
        {
            var world = World();
            world.PlatformFree = false;
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));

            world = World();
            world.WorldKnown = false;
            request = Request(world.Anchor);
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
        }

        private static void NumericallyScoredThreatRejectsTheRoute()
        {
            var world = World();
            var request = Request(world.Anchor);
            request.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Position = new Vec2(1590f, 1579f),
                Velocity = new Vec2(0f, 0f),
                Width = 20,
                Height = 42,
                Damage = 1,
                TimeLeft = 600
            });
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
        }

        private static void InvalidBuffersAndNumbersFailClosed()
        {
            var world = World();
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            buffers.SuccessTicks = new BasicHookTrajectoryTick[1];
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            buffers = Buffers();
            request.Gravity = float.NaN;
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            request = Request(world.Anchor);
            request.CandidateAnchorCount = 2;
            False(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
        }

        private static void FreshThreatsRescoreOnlyTheStillPossibleBranches()
        {
            var world = World();
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            True(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            var lateMiss = route.Evidence.MissTicks[route.Evidence.MissTickCount - 2];
            var elapsed = route.Evidence.SuccessTicks[
                route.Evidence.SuccessTickCount - 1].Tick + 2;
            var threats = new List<ThreatSnapshot>
            {
                new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Geometry = ThreatGeometry.Body,
                    Position = new Vec2(lateMiss.PlayerCenterAfter.X - 10f,
                        lateMiss.PlayerCenterAfter.Y - 21f),
                    Width = 20,
                    Height = 42,
                    Damage = 1,
                    TimeLeft = 600
                }
            };
            float risk;
            True(BasicHookRouteFactory.TryScoreRemainingThreats(in route, elapsed,
                threats, 20, 42, 0f, false, true, out risk));
            True(risk > 0f);
            True(BasicHookRouteFactory.TryScoreRemainingThreats(in route, elapsed,
                threats, 20, 42, 0f, true, false, out risk));
            Equal(0f, risk);
        }

        private static void MalformedLiveThreatsFailClosed()
        {
            var world = World();
            var request = Request(world.Anchor);
            var buffers = Buffers();
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            True(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                out route, out proof, out failure));
            var threats = new List<ThreatSnapshot>
            {
                new ThreatSnapshot { Position = new Vec2(float.NaN, 0f) }
            };
            float risk;
            False(BasicHookRouteFactory.TryScoreRemainingThreats(in route, 0,
                threats, 20, 42, 4f, true, true, out risk));
            False(BasicHookRouteFactory.TryScoreRemainingThreats(in route, -1,
                new List<ThreatSnapshot>(), 20, 42, 4f, true, true, out risk));
            False(BasicHookRouteFactory.TryScoreRemainingThreats(in route, 0,
                new List<ThreatSnapshot>(), 20, 42, 4f, false, false, out risk));
        }

        private static void NativeProjectileMotionProtectsBothThreatScoringPasses()
        {
            var world = World();
            var buffers = Buffers();
            var linearRequest = Request(world.Anchor);
            var threat = BouncingThreat(ThreatTrajectory.Linear);
            linearRequest.Threats.Add(threat);
            BasicHookRouteCertificate route;
            BasicHookVerifiedRoute proof;
            BasicHookFailure failure;
            True(BasicHookRouteFactory.TryBuild(in linearRequest, world,
                in buffers, out route, out proof, out failure));

            var nativeRequest = Request(world.Anchor);
            threat.Trajectory = ThreatTrajectory.BouncingFallingHostileBolt;
            nativeRequest.Threats.Add(threat);
            False(BasicHookRouteFactory.TryBuild(in nativeRequest, world,
                in buffers, out route, out proof, out failure));

            var cleanRequest = Request(world.Anchor);
            True(BasicHookRouteFactory.TryBuild(in cleanRequest, world,
                in buffers, out route, out proof, out failure));
            float risk;
            True(BasicHookRouteFactory.TryScoreRemainingThreats(in route, 0,
                nativeRequest.Threats, 20, 42, 4f, true, true, out risk));
            True(risk > 0f);
        }

        private static ThreatSnapshot BouncingThreat(
            ThreatTrajectory trajectory)
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = trajectory,
                Type = 921,
                Position = new Vec2(1685f, 1597f),
                Velocity = new Vec2(60f, 0f),
                Width = 6,
                Height = 6,
                Damage = 40,
                TimeLeft = 600,
                TrajectoryAi0 = 4f
            };
        }

        private static BasicHookRouteBuildRequest Request(BasicHookAnchorObservation anchor)
        {
            return new BasicHookRouteBuildRequest
            {
                Known = true,
                Identity = Identity(),
                Context = Context(),
                FireCenter = new Vec2(1600f, 1600f),
                PlayerVelocity = new Vec2(0f, 0f),
                PlayerGrounded = true,
                PlayerWidth = 20,
                PlayerHeight = 42,
                Gravity = .4f,
                MaximumFallSpeed = 10f,
                HorizontalSlowdown = .3f,
                JumpSpeed = 5.01f,
                JumpHeight = 15,
                LowConfigLoopCenter = new Vec2(1600f, 1600f),
                LoopHalfWidth = 200f,
                LoopHalfHeight = 200f,
                LoopMaximumAbsVelocityX = 20f,
                LoopMaximumAbsVelocityY = 20f,
                LowConfigLoopEpoch = 4,
                MaximumAcceptedRiskScore = 0f,
                PlayerSafetyMargin = 4f,
                Threats = new List<ThreatSnapshot>(),
                CandidateAnchors = new[] { anchor },
                CandidateAnchorCount = 1
            };
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

        private static BasicHookUseContext Context()
        {
            return new BasicHookUseContext
            {
                Known = true,
                LocalPlayerKnown = true,
                LocalPlayerIndex = 0,
                NormalGravity = true,
                ReleaseHook = true,
                ItemStartGateKnown = true,
                ItemStartGateOpen = true
            };
        }

        private static BasicHookRouteBuffers Buffers()
        {
            return new BasicHookRouteBuffers
            {
                SuccessTicks = new BasicHookTrajectoryTick[BasicHookRouteFactory.RequiredSuccessTickCapacity],
                MissTicks = new BasicHookTrajectoryTick[BasicHookRouteFactory.RequiredMissTickCapacity],
                SuccessTileSamples = new BasicHookTileSample[BasicHookRouteFactory.RequiredTileSampleCapacity],
                MissTileSamples = new BasicHookTileSample[BasicHookRouteFactory.RequiredTileSampleCapacity]
            };
        }

        private static FakeWorld World()
        {
            var anchor = new BasicHookAnchorObservation
            {
                Known = true,
                TileX = 108,
                TileY = 92,
                TileType = 1,
                NativeActive = true,
                NativeSolid = true,
                HookCenter = new Vec2(1736f, 1480f)
            };
            return new FakeWorld { Anchor = anchor, WorldKnown = true, PlatformFree = true };
        }

        private static int Find(BasicHookTrajectoryTick[] ticks, int count,
            BasicHookEvidencePhase phase)
        {
            for (var index = 0; index < count; index++)
                if (ticks[index].Phase == phase) return index;
            throw new InvalidOperationException("phase not found: " + phase);
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

        private sealed class FakeWorld : IBasicHookWorldEvidenceSource
        {
            public BasicHookAnchorObservation Anchor;
            public bool HasBlockingTile;
            public bool WorldKnown;
            public bool PlatformFree;
            public int MaxTilesX => 1000;
            public int MaxTilesY => 1000;

            public bool TryReadTile(int x, int y, out BasicHookAnchorObservation tile)
            {
                tile = new BasicHookAnchorObservation
                {
                    Known = WorldKnown,
                    TileX = x,
                    TileY = y,
                    TileType = 0,
                    HookCenter = new Vec2(x * 16f + 8f, y * 16f + 8f)
                };
                if (!WorldKnown) return false;
                if (x == Anchor.TileX && y == Anchor.TileY)
                {
                    tile = Anchor;
                    return true;
                }
                // This tile intersects the launch scan one step before the
                // intended block along the same diagonal.
                if (HasBlockingTile && x == 107 && y == 93)
                {
                    tile.TileType = 1;
                    tile.NativeActive = true;
                    tile.NativeSolid = true;
                }
                return true;
            }

            public bool TrySweepPlayer(in RectF before, in RectF after,
                out bool pathClear, out bool platformFree)
            {
                pathClear = WorldKnown;
                platformFree = WorldKnown && PlatformFree;
                return WorldKnown;
            }
        }
    }
}
