using System;

namespace Chaite.Core
{
    /// <summary>
    /// Adapter boundary for structured world/threat evidence. Implementations
    /// fill counters and numeric analysis; the evaluator remains the authority
    /// which decides whether those records form a valid certificate.
    /// </summary>
    public interface IWitchBroomRescueEvidenceSource
    {
        bool TryReadSweep(int tick, float beforeX, float beforeY, float afterX,
            float afterY, out WitchBroomSweepEvidence evidence);
        bool TryPopulateThreats(WitchBroomRescueCandidateSnapshot candidate,
            int closureTick);
    }

    public struct WitchBroomRouteBuildContext
    {
        public bool Known;
        public long RouteIdentity;
        public int Tick;
        public long ObservationSequence;
        public long WorldIdentity;
        public int WorldRevision;
        public long EncounterIdentity;
        public int EscapeDirection;
        public int EscapeTicks;
        public int ReturnTicks;
        public float MinimumArenaMargin;
        public float MaximumThreatRisk;
        public int MinimumThreatLeadTicks;
        public float MaximumDismountVelocityX;
        public float MaximumDismountVelocityY;
        public float MaximumReturnVelocityX;
        public float MaximumReturnVelocityY;
        public RectF ArenaBounds;
        public WitchBroomToggleSnapshot ActivationToggle;
        public WitchBroomMotionSnapshot EntryMotion;
        public WitchBroomReturnProfile ReturnProfile;
        public bool ExitSlowFallKnown;
        public bool ExitSlowFall;
    }

    /// <summary>
    /// Builds a bounded horizontal escape followed by native type-23 braking,
    /// a proved dismount and a short ordinary-motion return. It neither scans
    /// Terraria nor scores its own evidence.
    /// </summary>
    public static class WitchBroomRescueRouteBuilder
    {
        public const int DefaultEscapeTicks = 24;
        public const int MinimumEscapeTicks = 4;
        public const int MaximumEscapeTicks = 48;

        public static bool TryBuildActivationPreview(in WitchBroomRouteBuildContext context,
            IWitchBroomRescueEvidenceSource source,
            WitchBroomRescueCandidateSnapshot candidate)
        {
            if (candidate == null) return false;
            candidate.Reset();
            if (source == null || !Validate(in context)) return false;

            candidate.RouteIdentity = context.RouteIdentity;
            candidate.EscapeDirection = context.EscapeDirection;
            candidate.Stage = WitchBroomCandidateStage.ActivationPreview;
            var request = new WitchBroomRescueRequest
            {
                Known = true,
                WorldIdentity = context.WorldIdentity,
                WorldRevision = context.WorldRevision,
                EncounterIdentity = context.EncounterIdentity,
                ArenaBounds = context.ArenaBounds,
                MinimumArenaMargin = context.MinimumArenaMargin,
                MaximumThreatRisk = context.MaximumThreatRisk,
                MinimumThreatLeadTicks = context.MinimumThreatLeadTicks,
                DismountTarget = new WitchBroomClosureTarget
                {
                    BodyRegion = context.ArenaBounds,
                    MaxAbsVelocityX = context.MaximumDismountVelocityX,
                    MaxAbsVelocityY = context.MaximumDismountVelocityY
                },
                ReturnTarget = new WitchBroomClosureTarget
                {
                    BodyRegion = context.ArenaBounds,
                    MaxAbsVelocityX = context.MaximumReturnVelocityX,
                    MaxAbsVelocityY = context.MaximumReturnVelocityY
                },
                ReturnProfile = context.ReturnProfile
            };

            var activation = new WitchBroomActivationEvidence
            {
                Known = true,
                Tick = context.Tick,
                CaptureSequence = context.ObservationSequence,
                WorldIdentity = context.WorldIdentity,
                WorldRevision = context.WorldRevision,
                EncounterIdentity = context.EncounterIdentity,
                Toggle = context.ActivationToggle,
                EntryMotion = context.EntryMotion,
                EntryInput = EscapeInput(context.EscapeDirection,
                    context.EntryMotion.VelocityY, context.MaximumDismountVelocityY)
            };
            var entry = context.EntryMotion;
            entry.OpenDryPath = true;
            if (!WitchBroomMotion.TryAdvanceOpenDryTick(in entry, in activation.EntryInput,
                out var entryStep) || !entryStep.Supported ||
                !source.TryReadSweep(context.Tick, context.EntryMotion.PositionX,
                    context.EntryMotion.PositionY, entryStep.Next.PositionX,
                    entryStep.Next.PositionY, out activation.EntrySweep))
            {
                candidate.Reset();
                return false;
            }
            request.Activation = activation;
            request.Confirmation = new WitchBroomMountObservation
            {
                Known = true,
                Tick = context.Tick + 1,
                CaptureSequence = context.ObservationSequence + 1,
                WorldIdentity = context.WorldIdentity,
                WorldRevision = context.WorldRevision,
                EncounterIdentity = context.EncounterIdentity,
                Provenance = WitchBroomObservationProvenance.PredictedActivationPreview,
                ReleaseMount = false,
                Motion = entryStep.Next
            };

            var state = entryStep.Next;
            var mountedCount = 0;
            for (var travel = 0; travel < context.EscapeTicks; travel++)
            {
                var input = EscapeInput(context.EscapeDirection, state.VelocityY,
                    context.MaximumDismountVelocityY);
                if (!AppendMounted(source, candidate, context.Tick + 1 + mountedCount,
                    ref state, in input))
                {
                    candidate.Reset();
                    return false;
                }
                mountedCount++;
            }
            while (Math.Abs(state.VelocityX) > context.MaximumDismountVelocityX ||
                Math.Abs(state.VelocityY) > context.MaximumDismountVelocityY)
            {
                if (mountedCount >= WitchBroomRescueCandidateSnapshot.MountedCapacity)
                {
                    candidate.Reset();
                    return false;
                }
                var input = BrakeInput(in state, context.MaximumDismountVelocityX,
                    context.MaximumDismountVelocityY);
                if (!AppendMounted(source, candidate, context.Tick + 1 + mountedCount,
                    ref state, in input))
                {
                    candidate.Reset();
                    return false;
                }
                mountedCount++;
            }
            candidate.MountedCount = mountedCount;

            var dismountTick = context.Tick + 1 + mountedCount;
            if (!source.TryReadSweep(dismountTick, state.PositionX, state.PositionY,
                state.PositionX, state.PositionY, out var clearance))
            {
                candidate.Reset();
                return false;
            }
            request.Dismount = new WitchBroomDismountProbe
            {
                Known = true,
                Tick = dismountTick,
                CaptureSequence = context.ObservationSequence + 2,
                WorldIdentity = context.WorldIdentity,
                WorldRevision = context.WorldRevision,
                EncounterIdentity = context.EncounterIdentity,
                MountActive = true,
                MountType = WitchBroomMotion.WitchBroomMountType,
                ReleaseMount = true,
                SlowFallKnown = context.ExitSlowFallKnown,
                SlowFall = context.ExitSlowFall,
                Clearance = clearance
            };

            var ordinary = new WitchBroomReturnState
            {
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                VelocityX = state.VelocityX,
                VelocityY = state.VelocityY
            };
            for (var i = 0; i < context.ReturnTicks; i++)
            {
                var before = ordinary;
                var input = default(WitchBroomReturnInput);
                if (!WitchBroomRescueTrajectory.TryAdvanceReturnBallisticTick(in ordinary,
                    in context.ReturnProfile, in input, context.ExitSlowFall, out var step) ||
                    !source.TryReadSweep(dismountTick + i, before.PositionX, before.PositionY,
                        step.Next.PositionX, step.Next.PositionY, out var sweep))
                {
                    candidate.Reset();
                    return false;
                }
                ordinary = step.Next;
                candidate.ReturnTicks[i] = new WitchBroomReturnTick
                {
                    Known = true,
                    Tick = dismountTick + i,
                    SlowFallKnown = context.ExitSlowFallKnown,
                    SlowFall = context.ExitSlowFall,
                    Input = input,
                    Sweep = sweep
                };
            }
            candidate.ReturnCount = context.ReturnTicks;
            candidate.Request = request;
            var closureTick = dismountTick + context.ReturnTicks;
            if (!source.TryPopulateThreats(candidate, closureTick))
            {
                candidate.Reset();
                return false;
            }
            // The source fills the activation threat after Request was copied.
            request = candidate.Request;
            request.Activation = candidate.Request.Activation;
            candidate.Request = request;
            return true;
        }

        public static bool TryPromoteNativeConfirmation(
            WitchBroomRescueCandidateSnapshot candidate,
            in WitchBroomMountObservation observation)
        {
            if (candidate == null ||
                candidate.Stage != WitchBroomCandidateStage.ActivationPreview ||
                observation.Provenance != WitchBroomObservationProvenance.NativePostActivation ||
                observation.Tick != candidate.Request.Confirmation.Tick ||
                observation.WorldIdentity != candidate.Request.WorldIdentity ||
                observation.WorldRevision != candidate.Request.WorldRevision ||
                observation.EncounterIdentity != candidate.Request.EncounterIdentity)
                return false;
            var request = candidate.Request;
            request.Confirmation = observation;
            candidate.Request = request;
            candidate.Stage = WitchBroomCandidateStage.NativeConfirmation;
            return true;
        }

        private static bool AppendMounted(IWitchBroomRescueEvidenceSource source,
            WitchBroomRescueCandidateSnapshot candidate, int tick,
            ref WitchBroomMotionSnapshot state, in WitchBroomMotionInput input)
        {
            if (candidate.MountedCount >= WitchBroomRescueCandidateSnapshot.MountedCapacity)
                return false;
            var before = state;
            state.OpenDryPath = true;
            if (!WitchBroomMotion.TryAdvanceOpenDryTick(in state, in input, out var step) ||
                !step.Supported || !source.TryReadSweep(tick, before.PositionX, before.PositionY,
                    step.Next.PositionX, step.Next.PositionY, out var sweep))
                return false;
            state = step.Next;
            candidate.MountedTicks[candidate.MountedCount] = new WitchBroomRescueTick
            {
                Known = true,
                Tick = tick,
                Input = input,
                Sweep = sweep
            };
            candidate.MountedCount++;
            return true;
        }

        private static WitchBroomMotionInput EscapeInput(int direction, float velocityY,
            float maximumVertical)
        {
            return new WitchBroomMotionInput
            {
                Horizontal = direction,
                Up = velocityY > maximumVertical,
                Down = velocityY < -maximumVertical
            };
        }

        private static WitchBroomMotionInput BrakeInput(in WitchBroomMotionSnapshot state,
            float maximumHorizontal, float maximumVertical)
        {
            var horizontal = state.VelocityX > maximumHorizontal ? -1 :
                state.VelocityX < -maximumHorizontal ? 1 : 0;
            // Avoid a small opposite-input oscillation near zero. Air slowdown
            // reaches zero exactly for the final <=0.1 px/tick remainder.
            if (Math.Abs(state.VelocityX) <= WitchBroomMotion.RunSlowdown * .5f)
                horizontal = 0;
            return new WitchBroomMotionInput
            {
                Horizontal = horizontal,
                Up = state.VelocityY > maximumVertical,
                Down = state.VelocityY < -maximumVertical
            };
        }

        private static bool Validate(in WitchBroomRouteBuildContext context)
        {
            return context.Known && context.RouteIdentity != 0 && context.Tick < int.MaxValue - 256 &&
                context.ObservationSequence < long.MaxValue - 2 &&
                (context.EscapeDirection == -1 || context.EscapeDirection == 1) &&
                context.EscapeTicks >= MinimumEscapeTicks &&
                context.EscapeTicks <= MaximumEscapeTicks && context.ReturnTicks > 0 &&
                context.ReturnTicks <= WitchBroomRescueCandidateSnapshot.ReturnCapacity &&
                context.ExitSlowFallKnown && Finite(context.MinimumArenaMargin) &&
                context.MinimumArenaMargin >= 0f && Finite(context.MaximumThreatRisk) &&
                context.MaximumThreatRisk >= 0f && context.MinimumThreatLeadTicks >= 0 &&
                Finite(context.MaximumDismountVelocityX) &&
                context.MaximumDismountVelocityX >= WitchBroomMotion.RunSlowdown * .5f &&
                Finite(context.MaximumDismountVelocityY) &&
                context.MaximumDismountVelocityY >= Math.Abs(WitchBroomMotion.NeutralTarget) &&
                Finite(context.MaximumReturnVelocityX) && context.MaximumReturnVelocityX >= 0f &&
                Finite(context.MaximumReturnVelocityY) &&
                context.MaximumReturnVelocityY >= Math.Abs(WitchBroomMotion.NeutralTarget);
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
