using Chaite.Core;
using System;

namespace Chaite.Tests
{
    public static class WitchBroomRouteBuilderTests
    {
        public static int RunAll()
        {
            PreviewBuildsAndScoresACompleteRoute();
            NativePromotionRequiresTheExactNextObservation();
            StructuredObstructionSurvivesIntoTheEvaluator();
            return 3;
        }

        private static void PreviewBuildsAndScoresACompleteRoute()
        {
            var source = new EvidenceSource(false);
            var candidate = new WitchBroomRescueCandidateSnapshot();
            var context = Context();
            True(WitchBroomRescueRouteBuilder.TryBuildActivationPreview(in context,
                source, candidate));
            Equal(WitchBroomCandidateStage.ActivationPreview, candidate.Stage);
            True(candidate.MountedCount >= context.EscapeTicks);
            Equal(context.ReturnTicks, candidate.ReturnCount);
            True(candidate.TryEvaluate(out var result));
            True(result.Accepted);
            True(result.MaximumRequiredBrakeTicks > 0);
            True(result.MaximumBrakeTravelX > 0f);
        }

        private static void NativePromotionRequiresTheExactNextObservation()
        {
            var source = new EvidenceSource(false);
            var candidate = new WitchBroomRescueCandidateSnapshot();
            var context = Context();
            True(WitchBroomRescueRouteBuilder.TryBuildActivationPreview(in context,
                source, candidate));
            var observation = candidate.Request.Confirmation;
            observation.Provenance = WitchBroomObservationProvenance.NativePostActivation;
            var wrong = observation;
            wrong.Tick++;
            False(WitchBroomRescueRouteBuilder.TryPromoteNativeConfirmation(candidate, in wrong));
            True(WitchBroomRescueRouteBuilder.TryPromoteNativeConfirmation(candidate,
                in observation));
            Equal(WitchBroomCandidateStage.NativeConfirmation, candidate.Stage);
            if (!candidate.TryEvaluate(out var accepted))
                throw new InvalidOperationException("native promotion rejected: " + accepted.Failure);
            True(accepted.Accepted);
        }

        private static void StructuredObstructionSurvivesIntoTheEvaluator()
        {
            var source = new EvidenceSource(true);
            var candidate = new WitchBroomRescueCandidateSnapshot();
            var context = Context();
            True(WitchBroomRescueRouteBuilder.TryBuildActivationPreview(in context,
                source, candidate));
            False(candidate.TryEvaluate(out var result));
            Equal(WitchBroomRescueFailure.SweepObstructed, result.Failure);
        }

        private static WitchBroomRouteBuildContext Context() => new WitchBroomRouteBuildContext
        {
            Known = true,
            RouteIdentity = 1234,
            Tick = 50,
            ObservationSequence = 200,
            WorldIdentity = 111,
            WorldRevision = 7,
            EncounterIdentity = 222,
            EscapeDirection = 1,
            EscapeTicks = 12,
            ReturnTicks = 2,
            MinimumArenaMargin = 4f,
            MaximumThreatRisk = 0f,
            MinimumThreatLeadTicks = 1,
            MaximumDismountVelocityX = .5f,
            MaximumDismountVelocityY = .5f,
            MaximumReturnVelocityX = 1f,
            MaximumReturnVelocityY = 1f,
            ArenaBounds = new RectF(0f, 0f, 1200f, 800f),
            ActivationToggle = new WitchBroomToggleSnapshot
            {
                Known = true,
                ControlMount = true,
                ReleaseMount = true,
                ActiveMountType = -1,
                QuickMountItemType = 4444,
                QuickMountType = 23,
                ItemUseStartEdgeReady = true,
                CanFitMount = true,
                CanFitDismount = true
            },
            EntryMotion = new WitchBroomMotionSnapshot
            {
                Known = true,
                MountActive = true,
                MountType = 23,
                FrameState = 0,
                PositionX = 300f,
                PositionY = 300f,
                VelocityY = .5f,
                Gravity = .4f,
                NormalGravity = true,
                Dry = true,
                PortalPhysicsDisabled = true,
                ReleaseUp = true
            },
            ReturnProfile = new WitchBroomReturnProfile
            {
                Known = true,
                NormalGravity = true,
                Dry = true,
                PortalPhysicsDisabled = true,
                Gravity = .4f,
                MaxFallSpeed = 10f,
                BaseRunSpeed = 3f,
                MaxRunSpeed = 6f,
                RunAcceleration = .08f,
                SprintAcceleration = .016f,
                RunSlowdown = .4f
            },
            ExitSlowFallKnown = true,
            ExitSlowFall = true
        };

        private sealed class EvidenceSource : IWitchBroomRescueEvidenceSource
        {
            private long _sweepSequence;
            private long _threatSequence;
            private readonly bool _blockEntry;

            public EvidenceSource(bool blockEntry)
            {
                _blockEntry = blockEntry;
                _sweepSequence = 1000;
                _threatSequence = 2000;
            }

            public bool TryReadSweep(int tick, float beforeX, float beforeY,
                float afterX, float afterY, out WitchBroomSweepEvidence evidence)
            {
                var left = Math.Min(beforeX, afterX);
                var right = Math.Max(beforeX, afterX) + WitchBroomMotion.HitboxWidth;
                var top = Math.Min(beforeY, afterY);
                var bottom = Math.Max(beforeY, afterY) + WitchBroomMotion.HitboxHeight;
                var minX = (int)Math.Floor(left / 16f);
                var maxX = (int)(Math.Ceiling(right / 16f) - 1d);
                var minY = (int)Math.Floor(top / 16f);
                var maxY = (int)(Math.Ceiling(bottom / 16f) - 1d);
                evidence = new WitchBroomSweepEvidence
                {
                    Known = true,
                    Tick = tick,
                    CaptureSequence = _sweepSequence++,
                    WorldIdentity = 111,
                    WorldRevision = 7,
                    MinTileX = minX,
                    MaxTileX = maxX,
                    MinTileY = minY,
                    MaxTileY = maxY,
                    ScannedTileCount = (maxX - minX + 1) * (maxY - minY + 1),
                    SolidBlockingCount = _blockEntry && _sweepSequence == 1002 ? 1 : 0
                };
                return true;
            }

            public bool TryPopulateThreats(WitchBroomRescueCandidateSnapshot candidate,
                int closureTick)
            {
                var request = candidate.Request;
                var activation = request.Activation;
                activation.EntryThreat = Threat(activation.Tick,
                    activation.EntrySweep, request.Confirmation.Motion.PositionX,
                    request.Confirmation.Motion.PositionY, closureTick);
                request.Activation = activation;
                candidate.Request = request;
                for (var i = 0; i < candidate.MountedCount; i++)
                {
                    var tick = candidate.MountedTicks[i];
                    tick.Threat = Threat(tick.Tick, tick.Sweep,
                        BodyX(tick.Sweep), BodyY(tick.Sweep), closureTick);
                    // The sweep only gives a conservative tile AABB. Recover the
                    // exact endpoint by replaying the route below.
                    candidate.MountedTicks[i] = tick;
                }

                var state = candidate.Request.Confirmation.Motion;
                for (var i = 0; i < candidate.MountedCount; i++)
                {
                    state.OpenDryPath = true;
                    var tick = candidate.MountedTicks[i];
                    if (!WitchBroomMotion.TryAdvanceOpenDryTick(in state, in tick.Input,
                        out var step)) return false;
                    state = step.Next;
                    tick.Threat.BodyX = state.PositionX;
                    tick.Threat.BodyY = state.PositionY;
                    candidate.MountedTicks[i] = tick;
                }
                var ordinary = new WitchBroomReturnState
                {
                    PositionX = state.PositionX,
                    PositionY = state.PositionY,
                    VelocityX = state.VelocityX,
                    VelocityY = state.VelocityY
                };
                for (var i = 0; i < candidate.ReturnCount; i++)
                {
                    var tick = candidate.ReturnTicks[i];
                    if (!WitchBroomRescueTrajectory.TryAdvanceReturnBallisticTick(in ordinary,
                        in request.ReturnProfile, in tick.Input, tick.SlowFall, out var step))
                        return false;
                    ordinary = step.Next;
                    tick.Threat = Threat(tick.Tick, tick.Sweep, ordinary.PositionX,
                        ordinary.PositionY, closureTick);
                    candidate.ReturnTicks[i] = tick;
                }
                return true;
            }

            private WitchBroomThreatEvidence Threat(int tick,
                WitchBroomSweepEvidence unused, float x, float y, int closureTick) =>
                new WitchBroomThreatEvidence
                {
                    Known = true,
                    Tick = tick,
                    CaptureSequence = _threatSequence++,
                    EncounterIdentity = 222,
                    BodyX = x,
                    BodyY = y,
                    BodyWidth = WitchBroomMotion.HitboxWidth,
                    BodyHeight = WitchBroomMotion.HitboxHeight,
                    PredictionHorizonTicks = closureTick - tick,
                    EarliestTimeToImpactTicks = int.MaxValue
                };

            private static float BodyX(WitchBroomSweepEvidence sweep) => sweep.MinTileX * 16f;
            private static float BodyY(WitchBroomSweepEvidence sweep) => sweep.MinTileY * 16f;
        }

        private static void True(bool value) { if (!value) throw new InvalidOperationException("expected true"); }
        private static void False(bool value) { if (value) throw new InvalidOperationException("expected false"); }
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"expected {expected}, actual {actual}");
        }
    }
}
