using Chaite.Core;
using System;

namespace Chaite.Tests
{
    /// <summary>
    /// Standalone regressions for the full Witch's Broom rescue certificate.
    /// This file remains outside the legacy project include list until the
    /// concurrent planner integration is frozen.
    /// </summary>
    public static class WitchBroomRescueTrajectoryTests
    {
        public static int RunAll()
        {
            Action[] tests =
            {
                CompleteClosureUsesObservedFeatherFall,
                PreviewCannotImpersonateNativeConfirmation,
                SameFrameOrWrongMountConfirmationIsRejected,
                StaleOrWrongGeometrySweepIsRejected,
                WorldRevisionCannotBeMixedAcrossSweeps,
                OmittedThreatCannotHideBehindZeroRisk,
                ThreatHorizonMustCoverTheWholeClosure,
                ThreatBodyMustMatchTheSimulatedEndpoint,
                RiskAndTimeToImpactAreBothChecked,
                LocalSafetyWithoutDismountSpaceIsRejected,
                SpeedRequiresARealBrakingHorizon,
                OutwardVelocityRequiresRealBrakingDistance,
                DismountRequiresARearmedNativeEdge,
                ExitFeatherFallStateCannotBeSubstituted,
                ReturnMustReachTheNumericLowConfigClosure,
                FuturePlatformOrCollisionCannotBeGuessed,
                APostDismountTrajectoryIsMandatory,
                MixedOptionalMobilityActionsFailClosed,
                CallerOwnedSlicesAreHonored,
                HotEvaluationDoesNotAllocate
            };
            foreach (var test in tests) test();
            return tests.Length;
        }

        private static void CompleteClosureUsesObservedFeatherFall()
        {
            var fixture = Valid();
            True(Evaluate(fixture, out var result));
            True(result.Accepted);
            Equal(WitchBroomRescueFailure.None, result.Failure);
            Equal(WitchBroomRescuePhase.Closure, result.Phase);
            True(result.UsedSlowFall);
            Equal(fixture.Request.Dismount.Tick, result.DismountTick);
            Equal(fixture.Request.Dismount.Tick + fixture.ReturnCount, result.ClosureTick);
            True(IsFinite(result.Final.PositionX) && IsFinite(result.Final.PositionY));
        }

        private static void PreviewCannotImpersonateNativeConfirmation()
        {
            var fixture = Valid();
            False(WitchBroomRescueTrajectory.TryEvaluateActivationPreview(in fixture.Request,
                fixture.Mounted, 0, fixture.MountedCount, fixture.Return, 0,
                fixture.ReturnCount, out var nativeAsPreview));
            Equal(WitchBroomRescueFailure.MountConfirmationMissing, nativeAsPreview.Failure);

            var confirmation = fixture.Request.Confirmation;
            confirmation.Provenance = WitchBroomObservationProvenance.PredictedActivationPreview;
            fixture.Request.Confirmation = confirmation;
            True(WitchBroomRescueTrajectory.TryEvaluateActivationPreview(in fixture.Request,
                fixture.Mounted, 0, fixture.MountedCount, fixture.Return, 0,
                fixture.ReturnCount, out var preview));
            True(preview.Accepted);
            False(Evaluate(fixture, out var predictedAsNative));
            Equal(WitchBroomRescueFailure.MountConfirmationMissing, predictedAsNative.Failure);
        }

        private static void SameFrameOrWrongMountConfirmationIsRejected()
        {
            var sameFrame = Valid();
            sameFrame.Request.Confirmation.Tick = sameFrame.Request.Activation.Tick;
            Rejected(sameFrame, WitchBroomRescueFailure.MountConfirmationTooEarly);

            var wrongType = Valid();
            wrongType.Request.Confirmation.Motion.MountType = 22;
            Rejected(wrongType, WitchBroomRescueFailure.MountIdentityMismatch);

            var selectedWrongItem = Valid();
            var activation = selectedWrongItem.Request.Activation;
            activation.Toggle.QuickMountItemType = 4443;
            selectedWrongItem.Request.Activation = activation;
            Rejected(selectedWrongItem, WitchBroomRescueFailure.ActivationRejected);
        }

        private static void StaleOrWrongGeometrySweepIsRejected()
        {
            var stale = Valid();
            stale.Mounted[1].Sweep.CaptureSequence = stale.Mounted[0].Sweep.CaptureSequence;
            Rejected(stale, WitchBroomRescueFailure.SweepEvidenceStale);

            var geometry = Valid();
            geometry.Mounted[0].Sweep.MaxTileX++;
            Rejected(geometry, WitchBroomRescueFailure.SweepGeometryMismatch);

            var partial = Valid();
            partial.Mounted[0].Sweep.ScannedTileCount--;
            Rejected(partial, WitchBroomRescueFailure.SweepIncomplete);
        }

        private static void WorldRevisionCannotBeMixedAcrossSweeps()
        {
            var fixture = Valid();
            fixture.Mounted[2].Sweep.WorldRevision++;
            Rejected(fixture, WitchBroomRescueFailure.SweepWorldMismatch);
        }

        private static void OmittedThreatCannotHideBehindZeroRisk()
        {
            var fixture = Valid();
            var threat = fixture.Mounted[0].Threat;
            threat.ObservedThreatCount = 2;
            threat.AnalyzedThreatCount = 1;
            threat.OmittedThreatCount = 1;
            threat.Risk = 0f;
            fixture.Mounted[0].Threat = threat;
            Rejected(fixture, WitchBroomRescueFailure.ThreatIncomplete);
        }

        private static void ThreatHorizonMustCoverTheWholeClosure()
        {
            var fixture = Valid();
            fixture.Mounted[0].Threat.PredictionHorizonTicks--;
            Rejected(fixture, WitchBroomRescueFailure.ThreatHorizonTooShort);
        }

        private static void ThreatBodyMustMatchTheSimulatedEndpoint()
        {
            var fixture = Valid();
            fixture.Return[0].Threat.BodyX += 16f;
            Rejected(fixture, WitchBroomRescueFailure.ThreatBodyMismatch);
        }

        private static void RiskAndTimeToImpactAreBothChecked()
        {
            var risky = Valid();
            risky.Mounted[0].Threat.Risk = risky.Request.MaximumThreatRisk + .01f;
            Rejected(risky, WitchBroomRescueFailure.ThreatRiskTooHigh);

            var impact = Valid();
            var evidence = impact.Mounted[0].Threat;
            evidence.ObservedThreatCount = 1;
            evidence.AnalyzedThreatCount = 1;
            evidence.EarliestTimeToImpactTicks = evidence.PredictionHorizonTicks;
            impact.Mounted[0].Threat = evidence;
            Rejected(impact, WitchBroomRescueFailure.ThreatImpactWithinClosure);
        }

        private static void LocalSafetyWithoutDismountSpaceIsRejected()
        {
            var fixture = Valid();
            fixture.Request.Dismount.Clearance.SolidBlockingCount = 1;
            Rejected(fixture, WitchBroomRescueFailure.DismountSpaceUnverified);
        }

        private static void SpeedRequiresARealBrakingHorizon()
        {
            var fixture = Valid(8f, 1);
            Rejected(fixture, WitchBroomRescueFailure.InsufficientBrakingHorizon);
        }

        private static void OutwardVelocityRequiresRealBrakingDistance()
        {
            var fixture = Valid(8f, 100);
            fixture.Request.ArenaBounds = new RectF(0f, 0f, 250f, 1000f);
            Rejected(fixture, WitchBroomRescueFailure.InsufficientBrakingDistance);
        }

        private static void DismountRequiresARearmedNativeEdge()
        {
            var fixture = Valid();
            var probe = fixture.Request.Dismount;
            probe.ReleaseMount = false;
            fixture.Request.Dismount = probe;
            Rejected(fixture, WitchBroomRescueFailure.DismountNotRearmed);
        }

        private static void ExitFeatherFallStateCannotBeSubstituted()
        {
            var fixture = Valid();
            fixture.Return[0].SlowFall = false;
            Rejected(fixture, WitchBroomRescueFailure.ReturnSlowFallMismatch);

            fixture = Valid();
            var probe = fixture.Request.Dismount;
            probe.SlowFallKnown = false;
            fixture.Request.Dismount = probe;
            Rejected(fixture, WitchBroomRescueFailure.ExitSlowFallUnknown);
        }

        private static void ReturnMustReachTheNumericLowConfigClosure()
        {
            var fixture = Valid();
            fixture.Request.ReturnTarget = new WitchBroomClosureTarget
            {
                BodyRegion = new RectF(700f, 700f, 40f, 60f),
                MaxAbsVelocityX = 1f,
                MaxAbsVelocityY = 1f
            };
            Rejected(fixture, WitchBroomRescueFailure.ClosureNotReached);
        }

        private static void FuturePlatformOrCollisionCannotBeGuessed()
        {
            var fixture = Valid();
            fixture.Return[1].Sweep.PlatformCount = 1;
            Rejected(fixture, WitchBroomRescueFailure.SweepObstructed);

            fixture = Valid();
            fixture.Mounted[3].Sweep.SlopeCount = 1;
            Rejected(fixture, WitchBroomRescueFailure.SweepObstructed);
        }

        private static void APostDismountTrajectoryIsMandatory()
        {
            var fixture = Valid();
            False(WitchBroomRescueTrajectory.TryEvaluate(in fixture.Request,
                fixture.Mounted, 0, fixture.MountedCount, fixture.Return, 0, 0, out var result));
            Equal(WitchBroomRescueFailure.ReturnTrajectoryMissing, result.Failure);
        }

        private static void MixedOptionalMobilityActionsFailClosed()
        {
            var hook = Valid();
            hook.Mounted[0].Input.Hook = true;
            Rejected(hook, WitchBroomRescueFailure.MotionUnsupported);

            var dash = Valid();
            dash.Return[0].Input.Dash = true;
            Rejected(dash, WitchBroomRescueFailure.ReturnMotionUnsupported);

            var gravity = Valid();
            gravity.Return[0].Input.FlipGravity = true;
            Rejected(gravity, WitchBroomRescueFailure.ReturnMotionUnsupported);
        }

        private static void CallerOwnedSlicesAreHonored()
        {
            var source = Valid();
            var mounted = new WitchBroomRescueTick[source.MountedCount + 2];
            var returns = new WitchBroomReturnTick[source.ReturnCount + 2];
            Array.Copy(source.Mounted, 0, mounted, 1, source.MountedCount);
            Array.Copy(source.Return, 0, returns, 1, source.ReturnCount);
            True(WitchBroomRescueTrajectory.TryEvaluate(in source.Request,
                mounted, 1, source.MountedCount, returns, 1, source.ReturnCount, out _));

            False(WitchBroomRescueTrajectory.TryEvaluate(in source.Request,
                mounted, mounted.Length, 1, returns, 1, source.ReturnCount, out var result));
            Equal(WitchBroomRescueFailure.BufferRangeInvalid, result.Failure);
        }

        private static void HotEvaluationDoesNotAllocate()
        {
            var fixture = Valid();
            True(Evaluate(fixture, out _));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
                True(Evaluate(fixture, out _));
            var after = GC.GetAllocatedBytesForCurrentThread();
            Equal(0L, after - before);
        }

        private static Fixture Valid(float initialVelocityX = .4f, int mountedCount = 6)
        {
            const long world = 27182818;
            const int revision = 17;
            const long encounter = 31415926;
            const int activationTick = 100;
            var confirmationTick = activationTick + 1;
            var closureTick = confirmationTick + mountedCount + 2;
            long sweepSequence = 10;
            long threatSequence = 100;
            var entry = new WitchBroomMotionSnapshot
            {
                Known = true,
                MountActive = true,
                MountType = WitchBroomMotion.WitchBroomMountType,
                FrameState = 0,
                PositionX = 200f,
                PositionY = 200f,
                VelocityX = initialVelocityX,
                VelocityY = .5f,
                Gravity = .4f,
                NormalGravity = true,
                Dry = true,
                PortalPhysicsDisabled = true,
                ReleaseUp = true
            };
            var entryCandidate = entry;
            entryCandidate.OpenDryPath = true;
            True(WitchBroomMotion.TryAdvanceOpenDryTick(in entryCandidate,
                default(WitchBroomMotionInput), out var entryStep));
            var state = entryStep.Next;
            var request = new WitchBroomRescueRequest
            {
                Known = true,
                WorldIdentity = world,
                WorldRevision = revision,
                EncounterIdentity = encounter,
                ArenaBounds = new RectF(0f, 0f, 1000f, 1000f),
                MinimumArenaMargin = 4f,
                MaximumThreatRisk = .1f,
                MinimumThreatLeadTicks = 1,
                DismountTarget = new WitchBroomClosureTarget
                {
                    BodyRegion = new RectF(100f, 100f, 400f, 400f),
                    MaxAbsVelocityX = 1f,
                    MaxAbsVelocityY = 1f
                },
                ReturnTarget = new WitchBroomClosureTarget
                {
                    BodyRegion = new RectF(100f, 100f, 400f, 400f),
                    MaxAbsVelocityX = 1f,
                    MaxAbsVelocityY = 1f
                },
                Activation = new WitchBroomActivationEvidence
                {
                    Known = true,
                    Tick = activationTick,
                    CaptureSequence = 1,
                    WorldIdentity = world,
                    WorldRevision = revision,
                    EncounterIdentity = encounter,
                    Toggle = MountEdge(),
                    EntryMotion = entry,
                    EntrySweep = Sweep(entry.PositionX, entry.PositionY, state.PositionX,
                        state.PositionY, activationTick, sweepSequence++, world, revision),
                    EntryThreat = Threat(state.PositionX, state.PositionY, activationTick,
                        threatSequence++, encounter, closureTick)
                },
                Confirmation = new WitchBroomMountObservation
                {
                    Known = true,
                    Tick = confirmationTick,
                    CaptureSequence = 2,
                    WorldIdentity = world,
                    WorldRevision = revision,
                    EncounterIdentity = encounter,
                    Provenance = WitchBroomObservationProvenance.NativePostActivation,
                    ReleaseMount = false,
                    Motion = state
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
                    RunSlowdown = .4f,
                    CanSprintInAir = false
                }
            };

            var mounted = new WitchBroomRescueTick[mountedCount];
            for (var i = 0; i < mountedCount; i++)
            {
                var before = state;
                state.OpenDryPath = true;
                True(WitchBroomMotion.TryAdvanceOpenDryTick(in state,
                    default(WitchBroomMotionInput), out var step));
                state = step.Next;
                var tick = confirmationTick + i;
                mounted[i] = new WitchBroomRescueTick
                {
                    Known = true,
                    Tick = tick,
                    Sweep = Sweep(before.PositionX, before.PositionY, state.PositionX,
                        state.PositionY, tick, sweepSequence++, world, revision),
                    Threat = Threat(state.PositionX, state.PositionY, tick, threatSequence++,
                        encounter, closureTick)
                };
            }

            var dismountTick = confirmationTick + mountedCount;
            request.Dismount = new WitchBroomDismountProbe
            {
                Known = true,
                Tick = dismountTick,
                CaptureSequence = 3,
                WorldIdentity = world,
                WorldRevision = revision,
                EncounterIdentity = encounter,
                MountActive = true,
                MountType = WitchBroomMotion.WitchBroomMountType,
                ReleaseMount = true,
                SlowFallKnown = true,
                SlowFall = true,
                Clearance = Sweep(state.PositionX, state.PositionY, state.PositionX,
                    state.PositionY, dismountTick, sweepSequence++, world, revision)
            };

            var returns = new WitchBroomReturnTick[2];
            var ordinary = new WitchBroomReturnState
            {
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                VelocityX = state.VelocityX,
                VelocityY = state.VelocityY
            };
            for (var i = 0; i < returns.Length; i++)
            {
                var before = ordinary;
                True(WitchBroomRescueTrajectory.TryAdvanceReturnBallisticTick(in ordinary,
                    in request.ReturnProfile, default(WitchBroomReturnInput), true, out var step));
                ordinary = step.Next;
                var tick = dismountTick + i;
                returns[i] = new WitchBroomReturnTick
                {
                    Known = true,
                    Tick = tick,
                    SlowFallKnown = true,
                    SlowFall = true,
                    Sweep = Sweep(before.PositionX, before.PositionY, ordinary.PositionX,
                        ordinary.PositionY, tick, sweepSequence++, world, revision),
                    Threat = Threat(ordinary.PositionX, ordinary.PositionY, tick,
                        threatSequence++, encounter, closureTick)
                };
            }
            return new Fixture
            {
                Request = request,
                Mounted = mounted,
                MountedCount = mountedCount,
                Return = returns,
                ReturnCount = returns.Length
            };
        }

        private static WitchBroomToggleSnapshot MountEdge() => new WitchBroomToggleSnapshot
        {
            Known = true,
            ControlMount = true,
            ReleaseMount = true,
            ActiveMountType = -1,
            QuickMountItemType = WitchBroomMotion.WitchBroomItemType,
            QuickMountType = WitchBroomMotion.WitchBroomMountType,
            ItemUseStartEdgeReady = true,
            CanFitMount = true,
            CanFitDismount = true
        };

        private static WitchBroomThreatEvidence Threat(float x, float y, int tick,
            long sequence, long encounter, int closureTick) => new WitchBroomThreatEvidence
        {
            Known = true,
            Tick = tick,
            CaptureSequence = sequence,
            EncounterIdentity = encounter,
            BodyX = x,
            BodyY = y,
            BodyWidth = WitchBroomMotion.HitboxWidth,
            BodyHeight = WitchBroomMotion.HitboxHeight,
            PredictionHorizonTicks = closureTick - tick,
            EarliestTimeToImpactTicks = int.MaxValue,
            Risk = 0f
        };

        private static WitchBroomSweepEvidence Sweep(float beforeX, float beforeY,
            float afterX, float afterY, int tick, long sequence, long world, int revision)
        {
            var left = Math.Min(beforeX, afterX);
            var top = Math.Min(beforeY, afterY);
            var right = Math.Max(beforeX, afterX) + WitchBroomMotion.HitboxWidth;
            var bottom = Math.Max(beforeY, afterY) + WitchBroomMotion.HitboxHeight;
            var minX = (int)Math.Floor(left / 16f);
            var maxX = (int)(Math.Ceiling(right / 16f) - 1d);
            var minY = (int)Math.Floor(top / 16f);
            var maxY = (int)(Math.Ceiling(bottom / 16f) - 1d);
            return new WitchBroomSweepEvidence
            {
                Known = true,
                Tick = tick,
                CaptureSequence = sequence,
                WorldIdentity = world,
                WorldRevision = revision,
                MinTileX = minX,
                MaxTileX = maxX,
                MinTileY = minY,
                MaxTileY = maxY,
                ScannedTileCount = (maxX - minX + 1) * (maxY - minY + 1)
            };
        }

        private static bool Evaluate(Fixture fixture, out WitchBroomRescueResult result) =>
            WitchBroomRescueTrajectory.TryEvaluate(in fixture.Request,
                fixture.Mounted, 0, fixture.MountedCount,
                fixture.Return, 0, fixture.ReturnCount, out result);

        private static void Rejected(Fixture fixture, WitchBroomRescueFailure expected)
        {
            False(Evaluate(fixture, out var result));
            Equal(expected, result.Failure);
            False(result.Accepted);
        }

        private sealed class Fixture
        {
            public WitchBroomRescueRequest Request;
            public WitchBroomRescueTick[] Mounted;
            public int MountedCount;
            public WitchBroomReturnTick[] Return;
            public int ReturnCount;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("expected true");
        }
        private static void False(bool value)
        {
            if (value) throw new InvalidOperationException("expected false");
        }
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"expected {expected}, actual {actual}");
        }
    }
}
