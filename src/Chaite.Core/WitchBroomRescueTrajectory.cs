using System;

namespace Chaite.Core
{
    public enum WitchBroomRescuePhase
    {
        None,
        Activation,
        Confirmation,
        Mounted,
        Braking,
        Dismount,
        Return,
        Closure
    }

    public enum WitchBroomRescueFailure
    {
        None,
        InvalidRequest,
        BufferRangeInvalid,
        PlanTooLong,
        WorldIdentityMismatch,
        WorldRevisionMismatch,
        EncounterIdentityMismatch,
        ActivationEvidenceMissing,
        ActivationRejected,
        MountConfirmationMissing,
        MountConfirmationTooEarly,
        MountIdentityMismatch,
        ObservationSequenceStale,
        InvalidNumber,
        TickSequenceMismatch,
        MotionUnsupported,
        SweepEvidenceMissing,
        SweepEvidenceStale,
        SweepWorldMismatch,
        SweepGeometryMismatch,
        SweepIncomplete,
        SweepObstructed,
        ThreatEvidenceMissing,
        ThreatEvidenceStale,
        ThreatBodyMismatch,
        ThreatIncomplete,
        ThreatHorizonTooShort,
        ThreatRiskTooHigh,
        ThreatImpactWithinClosure,
        OutsideArena,
        InsufficientBrakingHorizon,
        InsufficientBrakingDistance,
        DismountTargetNotReached,
        DismountProbeMissing,
        DismountIdentityMismatch,
        DismountNotRearmed,
        DismountSpaceUnverified,
        UnsafeDismount,
        ExitSlowFallUnknown,
        ReturnTrajectoryMissing,
        ReturnSlowFallMismatch,
        ReturnMotionUnsupported,
        ClosureNotReached
    }

    public struct WitchBroomActivationEvidence
    {
        public bool Known;
        public int Tick;
        public long CaptureSequence;
        public long WorldIdentity;
        public int WorldRevision;
        public long EncounterIdentity;
        public WitchBroomToggleSnapshot Toggle;
        public WitchBroomMotionSnapshot EntryMotion;
        public WitchBroomMotionInput EntryInput;
        public WitchBroomSweepEvidence EntrySweep;
        public WitchBroomThreatEvidence EntryThreat;
    }

    public enum WitchBroomObservationProvenance
    {
        Unknown,
        PredictedActivationPreview,
        NativePostActivation
    }

    /// <summary>
    /// A real observation made after the activation update. It is deliberately
    /// separate from the selected QuickMount item: an inventory match cannot
    /// certify that native code actually installed mount type 23.
    /// </summary>
    public struct WitchBroomMountObservation
    {
        public bool Known;
        public int Tick;
        public long CaptureSequence;
        public long WorldIdentity;
        public int WorldRevision;
        public long EncounterIdentity;
        public WitchBroomObservationProvenance Provenance;
        public bool ReleaseMount;
        public WitchBroomMotionSnapshot Motion;
    }

    /// <summary>
    /// Exact tile rectangle and scan counters for one 20x42 swept body. There
    /// is intentionally no Clear boolean: the evaluator derives clearance from
    /// geometry, coverage and the individual native interaction counters.
    /// </summary>
    public struct WitchBroomSweepEvidence
    {
        public bool Known;
        public int Tick;
        public long CaptureSequence;
        public long WorldIdentity;
        public int WorldRevision;
        public int MinTileX;
        public int MaxTileX;
        public int MinTileY;
        public int MaxTileY;
        public int ScannedTileCount;
        public int SolidBlockingCount;
        public int HalfBlockCount;
        public int SlopeCount;
        public int PlatformCount;
        public int ConveyorCount;
        public int LiquidTileCount;
        public int UnloadedTileCount;
        public int OutOfWorldTileCount;
    }

    /// <summary>
    /// Numeric all-threat evidence for the resulting player body. Horizon and
    /// time-to-impact are relative to Tick. Omitted threats can never be hidden
    /// behind a low aggregate risk score.
    /// </summary>
    public struct WitchBroomThreatEvidence
    {
        public bool Known;
        public int Tick;
        public long CaptureSequence;
        public long EncounterIdentity;
        public float BodyX;
        public float BodyY;
        public int BodyWidth;
        public int BodyHeight;
        public int ObservedThreatCount;
        public int AnalyzedThreatCount;
        public int OmittedThreatCount;
        public int PredictionHorizonTicks;
        public int EarliestTimeToImpactTicks;
        public float Risk;
    }

    public struct WitchBroomRescueTick
    {
        public bool Known;
        public int Tick;
        public WitchBroomMotionInput Input;
        public WitchBroomSweepEvidence Sweep;
        public WitchBroomThreatEvidence Threat;
    }

    public struct WitchBroomDismountProbe
    {
        public bool Known;
        public int Tick;
        public long CaptureSequence;
        public long WorldIdentity;
        public int WorldRevision;
        public long EncounterIdentity;
        public bool MountActive;
        public int MountType;
        public bool ReleaseMount;
        public bool Dead;
        public bool SlowFallKnown;
        public bool SlowFall;
        public WitchBroomSweepEvidence Clearance;
    }

    /// <summary>A restricted dry, airborne ordinary-movement profile after dismount.</summary>
    public struct WitchBroomReturnProfile
    {
        public bool Known;
        public bool NormalGravity;
        public bool Dry;
        public bool PortalPhysicsDisabled;
        public bool MountActive;
        public bool Grappling;
        public bool HookInFlight;
        public bool DashInProgress;
        public bool Pulley;
        public bool Sliding;
        public bool WindPushed;
        public bool ForcedMotion;
        public float Gravity;
        public float MaxFallSpeed;
        public float BaseRunSpeed;
        public float MaxRunSpeed;
        public float RunAcceleration;
        public float SprintAcceleration;
        public float RunSlowdown;
        public bool CanSprintInAir;
    }

    public struct WitchBroomReturnInput
    {
        public int Horizontal;
        public bool Up;
        public bool Down;
        public bool Jump;
        public bool Hook;
        public bool Dash;
        public bool ToggleMount;
        public bool FlipGravity;
    }

    public struct WitchBroomReturnTick
    {
        public bool Known;
        public int Tick;
        public bool SlowFallKnown;
        public bool SlowFall;
        public WitchBroomReturnInput Input;
        public WitchBroomSweepEvidence Sweep;
        public WitchBroomThreatEvidence Threat;
    }

    public struct WitchBroomReturnState
    {
        public float PositionX;
        public float PositionY;
        public float VelocityX;
        public float VelocityY;
    }

    public struct WitchBroomReturnMotionResult
    {
        public bool Supported;
        public GravityPhase GravityPhase;
        public WitchBroomReturnState Next;
    }

    /// <summary>The whole-body region and speed envelope accepted at an edge.</summary>
    public struct WitchBroomClosureTarget
    {
        public RectF BodyRegion;
        public float MaxAbsVelocityX;
        public float MaxAbsVelocityY;
    }

    public struct WitchBroomRescueRequest
    {
        public bool Known;
        public long WorldIdentity;
        public int WorldRevision;
        public long EncounterIdentity;
        public RectF ArenaBounds;
        public float MinimumArenaMargin;
        public float MaximumThreatRisk;
        public int MinimumThreatLeadTicks;
        public WitchBroomClosureTarget DismountTarget;
        public WitchBroomClosureTarget ReturnTarget;
        public WitchBroomActivationEvidence Activation;
        public WitchBroomMountObservation Confirmation;
        public WitchBroomDismountProbe Dismount;
        public WitchBroomReturnProfile ReturnProfile;
    }

    public struct WitchBroomRescueResult
    {
        public bool Accepted;
        public WitchBroomRescueFailure Failure;
        public WitchBroomRescuePhase Phase;
        public int FailedIndex;
        public int DismountTick;
        public int ClosureTick;
        public int MaximumRequiredBrakeTicks;
        public float MaximumBrakeTravelX;
        public float MaximumBrakeTravelY;
        public bool UsedSlowFall;
        public WitchBroomReturnState Final;
    }

    /// <summary>
    /// Allocation-free proof checker for an optional Witch's Broom rescue. It
    /// accepts only a complete activation -> observed mount -> safe per-tick
    /// route -> braked dismount -> ordinary-motion return closure.
    /// </summary>
    public static class WitchBroomRescueTrajectory
    {
        public const int MaxMountedTicks = 256;
        public const int MaxReturnTicks = 256;
        public const int MaxBrakeSimulationTicks = 128;
        private const float TileSize = 16f;
        private const float FloatTolerance = .0001f;

        public static bool TryEvaluate(in WitchBroomRescueRequest request,
            WitchBroomRescueTick[] mountedTicks, int mountedOffset, int mountedCount,
            WitchBroomReturnTick[] returnTicks, int returnOffset, int returnCount,
            out WitchBroomRescueResult result)
        {
            return TryEvaluateCore(in request, mountedTicks, mountedOffset, mountedCount,
                returnTicks, returnOffset, returnCount, true, out result);
        }

        /// <summary>
        /// Scores the same complete route before emitting the activation edge.
        /// It accepts only an explicitly predicted confirmation. Such a result
        /// may authorize the mount pulse but never mounted movement; the next
        /// update must rebuild the route with NativePostActivation and pass
        /// TryEvaluate before any broom control is emitted.
        /// </summary>
        public static bool TryEvaluateActivationPreview(in WitchBroomRescueRequest request,
            WitchBroomRescueTick[] mountedTicks, int mountedOffset, int mountedCount,
            WitchBroomReturnTick[] returnTicks, int returnOffset, int returnCount,
            out WitchBroomRescueResult result)
        {
            return TryEvaluateCore(in request, mountedTicks, mountedOffset, mountedCount,
                returnTicks, returnOffset, returnCount, false, out result);
        }

        private static bool TryEvaluateCore(in WitchBroomRescueRequest request,
            WitchBroomRescueTick[] mountedTicks, int mountedOffset, int mountedCount,
            WitchBroomReturnTick[] returnTicks, int returnOffset, int returnCount,
            bool requireNativeConfirmation, out WitchBroomRescueResult result)
        {
            result = new WitchBroomRescueResult
            {
                Failure = WitchBroomRescueFailure.InvalidRequest,
                Phase = WitchBroomRescuePhase.None,
                FailedIndex = -1
            };

            if (!ValidateRequest(in request)) return false;
            if (!ValidRange(mountedTicks, mountedOffset, mountedCount) ||
                !ValidRange(returnTicks, returnOffset, returnCount))
                return Fail(ref result, WitchBroomRescueFailure.BufferRangeInvalid,
                    WitchBroomRescuePhase.None, -1);
            if (mountedCount > MaxMountedTicks || returnCount > MaxReturnTicks)
                return Fail(ref result, WitchBroomRescueFailure.PlanTooLong,
                    WitchBroomRescuePhase.None, -1);
            if (mountedCount <= 0)
                return Fail(ref result, WitchBroomRescueFailure.MountConfirmationMissing,
                    WitchBroomRescuePhase.Confirmation, -1);
            if (returnCount <= 0)
                return Fail(ref result, WitchBroomRescueFailure.ReturnTrajectoryMissing,
                    WitchBroomRescuePhase.Return, -1);

            var activation = request.Activation;
            if (!activation.Known)
                return Fail(ref result, WitchBroomRescueFailure.ActivationEvidenceMissing,
                    WitchBroomRescuePhase.Activation, -1);
            var identityFailure = ValidateIdentity(activation.WorldIdentity, activation.WorldRevision,
                activation.EncounterIdentity, in request);
            if (identityFailure != WitchBroomRescueFailure.None)
                return Fail(ref result, identityFailure, WitchBroomRescuePhase.Activation, -1);
            var activated = WitchBroomMotion.ResolveToggle(in activation.Toggle);
            if (!activated.Supported || activated.Transition != WitchBroomMountTransition.Mount ||
                activated.NextMountType != WitchBroomMotion.WitchBroomMountType)
                return Fail(ref result, WitchBroomRescueFailure.ActivationRejected,
                    WitchBroomRescuePhase.Activation, -1);

            var confirmation = request.Confirmation;
            if (!confirmation.Known)
                return Fail(ref result, WitchBroomRescueFailure.MountConfirmationMissing,
                    WitchBroomRescuePhase.Confirmation, -1);
            identityFailure = ValidateIdentity(confirmation.WorldIdentity, confirmation.WorldRevision,
                confirmation.EncounterIdentity, in request);
            if (identityFailure != WitchBroomRescueFailure.None)
                return Fail(ref result, identityFailure, WitchBroomRescuePhase.Confirmation, -1);
            if (activation.Tick == int.MaxValue || confirmation.Tick != activation.Tick + 1)
                return Fail(ref result, WitchBroomRescueFailure.MountConfirmationTooEarly,
                    WitchBroomRescuePhase.Confirmation, -1);
            if (confirmation.CaptureSequence <= activation.CaptureSequence)
                return Fail(ref result, WitchBroomRescueFailure.ObservationSequenceStale,
                    WitchBroomRescuePhase.Confirmation, -1);
            var expectedProvenance = requireNativeConfirmation
                ? WitchBroomObservationProvenance.NativePostActivation
                : WitchBroomObservationProvenance.PredictedActivationPreview;
            if (confirmation.Provenance != expectedProvenance)
                return Fail(ref result, WitchBroomRescueFailure.MountConfirmationMissing,
                    WitchBroomRescuePhase.Confirmation, -1);
            if (!confirmation.Motion.MountActive ||
                confirmation.Motion.MountType != WitchBroomMotion.WitchBroomMountType ||
                confirmation.ReleaseMount != activated.NextReleaseMount)
                return Fail(ref result, WitchBroomRescueFailure.MountIdentityMismatch,
                    WitchBroomRescuePhase.Confirmation, -1);

            var dismountTickLong = (long)confirmation.Tick + mountedCount;
            var closureTickLong = dismountTickLong + returnCount;
            if (dismountTickLong > int.MaxValue || closureTickLong > int.MaxValue)
                return Fail(ref result, WitchBroomRescueFailure.InvalidRequest,
                    WitchBroomRescuePhase.None, -1);
            var dismountTick = (int)dismountTickLong;
            var closureTick = (int)closureTickLong;
            result.DismountTick = dismountTick;
            result.ClosureTick = closureTick;

            long lastSweepSequence = long.MinValue;
            long lastThreatSequence = long.MinValue;
            var entry = activation.EntryMotion;
            entry.OpenDryPath = true;
            if (!entry.MountActive || entry.MountType != WitchBroomMotion.WitchBroomMountType ||
                !WitchBroomMotion.TryAdvanceOpenDryTick(in entry, in activation.EntryInput,
                    out var entryStep) || !entryStep.Supported)
                return Fail(ref result, WitchBroomRescueFailure.MotionUnsupported,
                    WitchBroomRescuePhase.Activation, -1);
            var entrySweepFailure = ValidateSweep(in activation.EntrySweep, activation.Tick,
                request.WorldIdentity, request.WorldRevision, activation.EntryMotion.PositionX,
                activation.EntryMotion.PositionY, entryStep.Next.PositionX,
                entryStep.Next.PositionY, ref lastSweepSequence);
            if (entrySweepFailure != WitchBroomRescueFailure.None)
                return Fail(ref result, entrySweepFailure, WitchBroomRescuePhase.Activation, -1);
            if (!BodyInside(in request.ArenaBounds, request.MinimumArenaMargin,
                entryStep.Next.PositionX, entryStep.Next.PositionY))
                return Fail(ref result, WitchBroomRescueFailure.OutsideArena,
                    WitchBroomRescuePhase.Activation, -1);
            var entryThreatFailure = ValidateThreat(in activation.EntryThreat, activation.Tick,
                request.EncounterIdentity, entryStep.Next.PositionX, entryStep.Next.PositionY,
                closureTick, in request, ref lastThreatSequence);
            if (entryThreatFailure != WitchBroomRescueFailure.None)
                return Fail(ref result, entryThreatFailure, WitchBroomRescuePhase.Activation, -1);
            if (!EquivalentMotion(in entryStep.Next, in confirmation.Motion))
                return Fail(ref result, WitchBroomRescueFailure.MountIdentityMismatch,
                    WitchBroomRescuePhase.Confirmation, -1);

            var state = confirmation.Motion;
            if (!FiniteMotion(in state) ||
                !BodyInside(in request.ArenaBounds, request.MinimumArenaMargin,
                    state.PositionX, state.PositionY))
                return Fail(ref result, WitchBroomRescueFailure.OutsideArena,
                    WitchBroomRescuePhase.Confirmation, -1);

            var releaseMount = activated.NextReleaseMount;

            if (!CheckBraking(in state, mountedCount, in request, ref result, -1)) return false;

            for (var i = 0; i < mountedCount; i++)
            {
                var plan = mountedTicks[mountedOffset + i];
                var expectedTick = confirmation.Tick + i;
                if (!plan.Known || plan.Tick != expectedTick)
                    return Fail(ref result, WitchBroomRescueFailure.TickSequenceMismatch,
                        WitchBroomRescuePhase.Mounted, i);

                var before = state;
                var candidate = state;
                candidate.OpenDryPath = true;
                if (!WitchBroomMotion.TryAdvanceOpenDryTick(in candidate, in plan.Input, out var step) ||
                    !step.Supported)
                    return Fail(ref result, WitchBroomRescueFailure.MotionUnsupported,
                        WitchBroomRescuePhase.Mounted, i);

                var sweepFailure = ValidateSweep(in plan.Sweep, expectedTick, request.WorldIdentity,
                    request.WorldRevision, before.PositionX, before.PositionY,
                    step.Next.PositionX, step.Next.PositionY, ref lastSweepSequence);
                if (sweepFailure != WitchBroomRescueFailure.None)
                    return Fail(ref result, sweepFailure, WitchBroomRescuePhase.Mounted, i);
                if (!BodyInside(in request.ArenaBounds, request.MinimumArenaMargin,
                    step.Next.PositionX, step.Next.PositionY))
                    return Fail(ref result, WitchBroomRescueFailure.OutsideArena,
                        WitchBroomRescuePhase.Mounted, i);
                var threatFailure = ValidateThreat(in plan.Threat, expectedTick,
                    request.EncounterIdentity, step.Next.PositionX, step.Next.PositionY,
                    closureTick, in request, ref lastThreatSequence);
                if (threatFailure != WitchBroomRescueFailure.None)
                    return Fail(ref result, threatFailure, WitchBroomRescuePhase.Mounted, i);

                state = step.Next;
                var rearmSnapshot = new WitchBroomToggleSnapshot
                {
                    Known = true,
                    ControlMount = false,
                    ReleaseMount = releaseMount,
                    MountActive = true,
                    ActiveMountType = WitchBroomMotion.WitchBroomMountType
                };
                var rearmed = WitchBroomMotion.ResolveToggle(in rearmSnapshot);
                if (!rearmed.Supported || rearmed.Transition != WitchBroomMountTransition.None)
                    return Fail(ref result, WitchBroomRescueFailure.DismountNotRearmed,
                        WitchBroomRescuePhase.Mounted, i);
                releaseMount = rearmed.NextReleaseMount;

                if (!CheckBraking(in state, mountedCount - i - 1, in request, ref result, i))
                    return false;
            }

            if (!InsideTarget(in request.DismountTarget, state.PositionX, state.PositionY,
                state.VelocityX, state.VelocityY))
                return Fail(ref result, WitchBroomRescueFailure.DismountTargetNotReached,
                    WitchBroomRescuePhase.Braking, mountedCount - 1);

            var probe = request.Dismount;
            if (!probe.Known)
                return Fail(ref result, WitchBroomRescueFailure.DismountProbeMissing,
                    WitchBroomRescuePhase.Dismount, -1);
            identityFailure = ValidateIdentity(probe.WorldIdentity, probe.WorldRevision,
                probe.EncounterIdentity, in request);
            if (identityFailure != WitchBroomRescueFailure.None)
                return Fail(ref result, identityFailure, WitchBroomRescuePhase.Dismount, -1);
            if (probe.Tick != dismountTick)
                return Fail(ref result, WitchBroomRescueFailure.TickSequenceMismatch,
                    WitchBroomRescuePhase.Dismount, -1);
            if (probe.CaptureSequence <= confirmation.CaptureSequence)
                return Fail(ref result, WitchBroomRescueFailure.ObservationSequenceStale,
                    WitchBroomRescuePhase.Dismount, -1);
            if (!probe.MountActive || probe.MountType != WitchBroomMotion.WitchBroomMountType)
                return Fail(ref result, WitchBroomRescueFailure.DismountIdentityMismatch,
                    WitchBroomRescuePhase.Dismount, -1);
            if (!releaseMount || !probe.ReleaseMount)
                return Fail(ref result, WitchBroomRescueFailure.DismountNotRearmed,
                    WitchBroomRescuePhase.Dismount, -1);
            if (!probe.SlowFallKnown)
                return Fail(ref result, WitchBroomRescueFailure.ExitSlowFallUnknown,
                    WitchBroomRescuePhase.Dismount, -1);

            var clearanceFailure = ValidateSweep(in probe.Clearance, dismountTick,
                request.WorldIdentity, request.WorldRevision, state.PositionX, state.PositionY,
                state.PositionX, state.PositionY, ref lastSweepSequence);
            if (clearanceFailure != WitchBroomRescueFailure.None)
            {
                var mapped = clearanceFailure == WitchBroomRescueFailure.SweepObstructed ||
                    clearanceFailure == WitchBroomRescueFailure.SweepIncomplete ||
                    clearanceFailure == WitchBroomRescueFailure.SweepGeometryMismatch
                    ? WitchBroomRescueFailure.DismountSpaceUnverified : clearanceFailure;
                return Fail(ref result, mapped, WitchBroomRescuePhase.Dismount, -1);
            }

            var dismountSnapshot = new WitchBroomToggleSnapshot
            {
                Known = true,
                ControlMount = true,
                ReleaseMount = probe.ReleaseMount,
                MountActive = probe.MountActive,
                ActiveMountType = probe.MountType,
                Dead = probe.Dead,
                CanFitDismount = true
            };
            var dismounted = WitchBroomMotion.ResolveToggle(in dismountSnapshot);
            if (!dismounted.Supported || dismounted.Transition != WitchBroomMountTransition.Dismount ||
                dismounted.NextMountActive)
                return Fail(ref result, WitchBroomRescueFailure.UnsafeDismount,
                    WitchBroomRescuePhase.Dismount, -1);

            var ordinary = new WitchBroomReturnState
            {
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                VelocityX = state.VelocityX,
                VelocityY = state.VelocityY
            };
            for (var i = 0; i < returnCount; i++)
            {
                var plan = returnTicks[returnOffset + i];
                var expectedTick = dismountTick + i;
                if (!plan.Known || plan.Tick != expectedTick)
                    return Fail(ref result, WitchBroomRescueFailure.TickSequenceMismatch,
                        WitchBroomRescuePhase.Return, i);
                if (!plan.SlowFallKnown || plan.SlowFall != probe.SlowFall)
                    return Fail(ref result, WitchBroomRescueFailure.ReturnSlowFallMismatch,
                        WitchBroomRescuePhase.Return, i);

                var before = ordinary;
                if (!TryAdvanceReturnBallisticTick(in ordinary, in request.ReturnProfile,
                    in plan.Input, probe.SlowFall, out var step))
                    return Fail(ref result, WitchBroomRescueFailure.ReturnMotionUnsupported,
                        WitchBroomRescuePhase.Return, i);
                ordinary = step.Next;
                var sweepFailure = ValidateSweep(in plan.Sweep, expectedTick, request.WorldIdentity,
                    request.WorldRevision, before.PositionX, before.PositionY,
                    ordinary.PositionX, ordinary.PositionY, ref lastSweepSequence);
                if (sweepFailure != WitchBroomRescueFailure.None)
                    return Fail(ref result, sweepFailure, WitchBroomRescuePhase.Return, i);
                if (!BodyInside(in request.ArenaBounds, request.MinimumArenaMargin,
                    ordinary.PositionX, ordinary.PositionY))
                    return Fail(ref result, WitchBroomRescueFailure.OutsideArena,
                        WitchBroomRescuePhase.Return, i);
                var threatFailure = ValidateThreat(in plan.Threat, expectedTick,
                    request.EncounterIdentity, ordinary.PositionX, ordinary.PositionY,
                    closureTick, in request, ref lastThreatSequence);
                if (threatFailure != WitchBroomRescueFailure.None)
                    return Fail(ref result, threatFailure, WitchBroomRescuePhase.Return, i);
            }

            result.Final = ordinary;
            result.UsedSlowFall = probe.SlowFall;
            if (!InsideTarget(in request.ReturnTarget, ordinary.PositionX, ordinary.PositionY,
                ordinary.VelocityX, ordinary.VelocityY))
                return Fail(ref result, WitchBroomRescueFailure.ClosureNotReached,
                    WitchBroomRescuePhase.Closure, returnCount - 1);

            result.Accepted = true;
            result.Failure = WitchBroomRescueFailure.None;
            result.Phase = WitchBroomRescuePhase.Closure;
            result.FailedIndex = -1;
            return true;
        }

        /// <summary>
        /// Exposed for deterministic adapters/tests, but the full evaluator
        /// always repeats this calculation and never trusts a supplied endpoint.
        /// It covers only open, dry, airborne ordinary movement without jump,
        /// flight, hook, dash, gravity flip or landing.
        /// </summary>
        public static bool TryAdvanceReturnBallisticTick(in WitchBroomReturnState state,
            in WitchBroomReturnProfile profile, in WitchBroomReturnInput input, bool slowFall,
            out WitchBroomReturnMotionResult result)
        {
            result = new WitchBroomReturnMotionResult
            {
                Supported = false,
                GravityPhase = GravityPhase.Unsupported,
                Next = state
            };
            if (!SupportsReturn(in state, in profile, in input)) return false;

            var next = state;
            next.VelocityX = AdvanceReturnHorizontal(state.VelocityX, input.Horizontal, in profile);
            var vertical = state.VelocityY;
            var phase = JumpMotion.ApplyGravityChecked(ref vertical, profile.Gravity,
                profile.MaxFallSpeed, false, slowFall, input.Up, input.Down);
            if (phase == GravityPhase.Unsupported || !Finite(next.VelocityX) || !Finite(vertical) ||
                !WithinSingleCollisionStep(next.VelocityX, vertical))
                return false;
            next.VelocityY = vertical;
            next.PositionX += next.VelocityX;
            next.PositionY += next.VelocityY;
            if (!Finite(next.PositionX) || !Finite(next.PositionY)) return false;

            result = new WitchBroomReturnMotionResult
            {
                Supported = true,
                GravityPhase = phase,
                Next = next
            };
            return true;
        }

        private static bool CheckBraking(in WitchBroomMotionSnapshot state, int remainingTicks,
            in WitchBroomRescueRequest request, ref WitchBroomRescueResult result, int index)
        {
            if (!TryComputeMinimumBrake(in state, in request.DismountTarget,
                in request.ArenaBounds, request.MinimumArenaMargin, out var requiredTicks,
                out var travelX, out var travelY, out var insideArena))
                return Fail(ref result, WitchBroomRescueFailure.MotionUnsupported,
                    WitchBroomRescuePhase.Braking, index);
            if (requiredTicks > result.MaximumRequiredBrakeTicks)
                result.MaximumRequiredBrakeTicks = requiredTicks;
            if (travelX > result.MaximumBrakeTravelX) result.MaximumBrakeTravelX = travelX;
            if (travelY > result.MaximumBrakeTravelY) result.MaximumBrakeTravelY = travelY;
            if (!insideArena)
                return Fail(ref result, WitchBroomRescueFailure.InsufficientBrakingDistance,
                    WitchBroomRescuePhase.Braking, index);
            if (requiredTicks > remainingTicks)
                return Fail(ref result, WitchBroomRescueFailure.InsufficientBrakingHorizon,
                    WitchBroomRescuePhase.Braking, index);
            return true;
        }

        private static bool TryComputeMinimumBrake(in WitchBroomMotionSnapshot initial,
            in WitchBroomClosureTarget target, in RectF arena, float margin,
            out int ticks, out float travelX, out float travelY, out bool insideArena)
        {
            ticks = 0;
            travelX = 0f;
            travelY = 0f;
            insideArena = BodyInside(in arena, margin, initial.PositionX, initial.PositionY);
            if (!insideArena || !FiniteMotion(in initial)) return false;

            var state = initial;
            var originX = state.PositionX;
            var originY = state.PositionY;
            while (Math.Abs(state.VelocityX) > target.MaxAbsVelocityX ||
                Math.Abs(state.VelocityY) > target.MaxAbsVelocityY)
            {
                if (ticks >= MaxBrakeSimulationTicks) return false;
                var input = new WitchBroomMotionInput
                {
                    Horizontal = state.VelocityX > target.MaxAbsVelocityX ? -1 :
                        state.VelocityX < -target.MaxAbsVelocityX ? 1 : 0,
                    Up = state.VelocityY > target.MaxAbsVelocityY,
                    Down = state.VelocityY < -target.MaxAbsVelocityY
                };
                var candidate = state;
                candidate.OpenDryPath = true;
                if (!WitchBroomMotion.TryAdvanceOpenDryTick(in candidate, in input, out var step) ||
                    !step.Supported)
                    return false;
                state = step.Next;
                ticks++;
                var dx = Math.Abs(state.PositionX - originX);
                var dy = Math.Abs(state.PositionY - originY);
                if (dx > travelX) travelX = dx;
                if (dy > travelY) travelY = dy;
                if (!BodyInside(in arena, margin, state.PositionX, state.PositionY))
                    insideArena = false;
            }
            return true;
        }

        private static WitchBroomRescueFailure ValidateSweep(in WitchBroomSweepEvidence evidence,
            int tick, long worldIdentity, int worldRevision, float beforeX, float beforeY,
            float afterX, float afterY, ref long lastSequence)
        {
            if (!evidence.Known) return WitchBroomRescueFailure.SweepEvidenceMissing;
            if (evidence.Tick != tick || evidence.CaptureSequence <= lastSequence)
                return WitchBroomRescueFailure.SweepEvidenceStale;
            if (evidence.WorldIdentity != worldIdentity || evidence.WorldRevision != worldRevision)
                return WitchBroomRescueFailure.SweepWorldMismatch;
            if (!TryGetSweptTileBounds(beforeX, beforeY, afterX, afterY,
                out var minX, out var maxX, out var minY, out var maxY, out var count))
                return WitchBroomRescueFailure.InvalidNumber;
            if (evidence.MinTileX != minX || evidence.MaxTileX != maxX ||
                evidence.MinTileY != minY || evidence.MaxTileY != maxY)
                return WitchBroomRescueFailure.SweepGeometryMismatch;
            if (evidence.ScannedTileCount != count || evidence.SolidBlockingCount < 0 ||
                evidence.HalfBlockCount < 0 || evidence.SlopeCount < 0 ||
                evidence.PlatformCount < 0 || evidence.ConveyorCount < 0 ||
                evidence.LiquidTileCount < 0 || evidence.UnloadedTileCount < 0 ||
                evidence.OutOfWorldTileCount < 0)
                return WitchBroomRescueFailure.SweepIncomplete;
            if (evidence.SolidBlockingCount != 0 || evidence.HalfBlockCount != 0 ||
                evidence.SlopeCount != 0 || evidence.PlatformCount != 0 ||
                evidence.ConveyorCount != 0 || evidence.LiquidTileCount != 0 ||
                evidence.UnloadedTileCount != 0 || evidence.OutOfWorldTileCount != 0)
                return WitchBroomRescueFailure.SweepObstructed;
            lastSequence = evidence.CaptureSequence;
            return WitchBroomRescueFailure.None;
        }

        private static WitchBroomRescueFailure ValidateThreat(in WitchBroomThreatEvidence evidence,
            int tick, long encounterIdentity, float bodyX, float bodyY, int closureTick,
            in WitchBroomRescueRequest request, ref long lastSequence)
        {
            if (!evidence.Known) return WitchBroomRescueFailure.ThreatEvidenceMissing;
            if (evidence.Tick != tick || evidence.CaptureSequence <= lastSequence)
                return WitchBroomRescueFailure.ThreatEvidenceStale;
            if (evidence.EncounterIdentity != encounterIdentity)
                return WitchBroomRescueFailure.EncounterIdentityMismatch;
            if (!Near(evidence.BodyX, bodyX) || !Near(evidence.BodyY, bodyY) ||
                evidence.BodyWidth != WitchBroomMotion.HitboxWidth ||
                evidence.BodyHeight != WitchBroomMotion.HitboxHeight)
                return WitchBroomRescueFailure.ThreatBodyMismatch;
            if (evidence.ObservedThreatCount < 0 || evidence.AnalyzedThreatCount < 0 ||
                evidence.OmittedThreatCount != 0 ||
                evidence.AnalyzedThreatCount != evidence.ObservedThreatCount ||
                evidence.PredictionHorizonTicks < 0 ||
                evidence.EarliestTimeToImpactTicks < 0 || !Finite(evidence.Risk) ||
                evidence.Risk < 0f)
                return WitchBroomRescueFailure.ThreatIncomplete;
            var remaining = closureTick - tick;
            if (evidence.PredictionHorizonTicks < remaining)
                return WitchBroomRescueFailure.ThreatHorizonTooShort;
            if (evidence.Risk > request.MaximumThreatRisk)
                return WitchBroomRescueFailure.ThreatRiskTooHigh;
            var requiredLead = Math.Max(remaining, request.MinimumThreatLeadTicks);
            if (evidence.ObservedThreatCount > 0 &&
                evidence.EarliestTimeToImpactTicks <= requiredLead)
                return WitchBroomRescueFailure.ThreatImpactWithinClosure;
            lastSequence = evidence.CaptureSequence;
            return WitchBroomRescueFailure.None;
        }

        private static bool TryGetSweptTileBounds(float beforeX, float beforeY,
            float afterX, float afterY, out int minTileX, out int maxTileX,
            out int minTileY, out int maxTileY, out int tileCount)
        {
            minTileX = maxTileX = minTileY = maxTileY = tileCount = 0;
            if (!Finite(beforeX) || !Finite(beforeY) || !Finite(afterX) || !Finite(afterY))
                return false;
            var left = Math.Min(beforeX, afterX);
            var top = Math.Min(beforeY, afterY);
            var right = Math.Max(beforeX, afterX) + WitchBroomMotion.HitboxWidth;
            var bottom = Math.Max(beforeY, afterY) + WitchBroomMotion.HitboxHeight;
            var minX = Math.Floor(left / TileSize);
            var maxX = Math.Ceiling(right / TileSize) - 1d;
            var minY = Math.Floor(top / TileSize);
            var maxY = Math.Ceiling(bottom / TileSize) - 1d;
            if (minX < int.MinValue || minX > int.MaxValue || maxX < int.MinValue ||
                maxX > int.MaxValue || minY < int.MinValue || minY > int.MaxValue ||
                maxY < int.MinValue || maxY > int.MaxValue)
                return false;
            var width = maxX - minX + 1d;
            var height = maxY - minY + 1d;
            var count = width * height;
            if (width <= 0d || height <= 0d || count > int.MaxValue) return false;
            minTileX = (int)minX;
            maxTileX = (int)maxX;
            minTileY = (int)minY;
            maxTileY = (int)maxY;
            tileCount = (int)count;
            return true;
        }

        private static bool SupportsReturn(in WitchBroomReturnState state,
            in WitchBroomReturnProfile profile, in WitchBroomReturnInput input)
        {
            if (!profile.Known || !profile.NormalGravity || !profile.Dry ||
                !profile.PortalPhysicsDisabled || profile.MountActive || profile.Grappling ||
                profile.HookInFlight || profile.DashInProgress || profile.Pulley ||
                profile.Sliding || profile.WindPushed || profile.ForcedMotion)
                return false;
            if (input.Horizontal < -1 || input.Horizontal > 1 || input.Up && input.Down ||
                input.Jump || input.Hook || input.Dash || input.ToggleMount || input.FlipGravity)
                return false;
            if (!Finite(state.PositionX) || !Finite(state.PositionY) ||
                !Finite(state.VelocityX) || !Finite(state.VelocityY) ||
                !Finite(profile.Gravity) || profile.Gravity < 0f ||
                !Finite(profile.MaxFallSpeed) || profile.MaxFallSpeed <= 0f ||
                !Finite(profile.BaseRunSpeed) || profile.BaseRunSpeed <= 0f ||
                !Finite(profile.MaxRunSpeed) || profile.MaxRunSpeed < profile.BaseRunSpeed ||
                !Finite(profile.RunAcceleration) || profile.RunAcceleration < 0f ||
                !Finite(profile.SprintAcceleration) || profile.SprintAcceleration < 0f ||
                !Finite(profile.RunSlowdown) || profile.RunSlowdown < 0f ||
                !WithinSingleCollisionStep(state.VelocityX, state.VelocityY))
                return false;
            return true;
        }

        private static float AdvanceReturnHorizontal(float velocity, int direction,
            in WitchBroomReturnProfile profile)
        {
            var drag = profile.RunSlowdown * .5f;
            if (direction != 0)
            {
                var forward = velocity * direction;
                if (forward < profile.BaseRunSpeed)
                {
                    if (forward < -profile.RunSlowdown) forward += profile.RunSlowdown;
                    forward += profile.RunAcceleration;
                    velocity = forward * direction;
                }
                else if (forward < profile.MaxRunSpeed && profile.SprintAcceleration > 0f)
                {
                    if (profile.CanSprintInAir)
                        velocity = (forward + profile.SprintAcceleration) * direction;
                }
                else velocity = MoveTowards(velocity, 0f, drag);
            }
            else velocity = MoveTowards(velocity, 0f, drag);
            return velocity;
        }

        private static float MoveTowards(float current, float target, float delta)
        {
            if (Math.Abs(target - current) <= delta) return target;
            return current + Math.Sign(target - current) * delta;
        }

        private static bool ValidateRequest(in WitchBroomRescueRequest request)
        {
            return request.Known && ValidRect(in request.ArenaBounds) &&
                request.ArenaBounds.Width >= WitchBroomMotion.HitboxWidth &&
                request.ArenaBounds.Height >= WitchBroomMotion.HitboxHeight &&
                Finite(request.MinimumArenaMargin) && request.MinimumArenaMargin >= 0f &&
                Finite(request.MaximumThreatRisk) && request.MaximumThreatRisk >= 0f &&
                request.MinimumThreatLeadTicks >= 0 && ValidTarget(in request.DismountTarget) &&
                ValidTarget(in request.ReturnTarget);
        }

        private static bool ValidTarget(in WitchBroomClosureTarget target)
        {
            return ValidRect(in target.BodyRegion) &&
                target.BodyRegion.Width >= WitchBroomMotion.HitboxWidth &&
                target.BodyRegion.Height >= WitchBroomMotion.HitboxHeight &&
                Finite(target.MaxAbsVelocityX) && target.MaxAbsVelocityX >= 0f &&
                Finite(target.MaxAbsVelocityY) &&
                target.MaxAbsVelocityY >= Math.Abs(WitchBroomMotion.NeutralTarget);
        }

        private static WitchBroomRescueFailure ValidateIdentity(long worldIdentity,
            int worldRevision, long encounterIdentity, in WitchBroomRescueRequest request)
        {
            if (worldIdentity != request.WorldIdentity)
                return WitchBroomRescueFailure.WorldIdentityMismatch;
            if (worldRevision != request.WorldRevision)
                return WitchBroomRescueFailure.WorldRevisionMismatch;
            if (encounterIdentity != request.EncounterIdentity)
                return WitchBroomRescueFailure.EncounterIdentityMismatch;
            return WitchBroomRescueFailure.None;
        }

        private static bool ValidRange<T>(T[] buffer, int offset, int count)
        {
            return buffer != null && offset >= 0 && count >= 0 && offset <= buffer.Length &&
                count <= buffer.Length - offset;
        }

        private static bool InsideTarget(in WitchBroomClosureTarget target, float x, float y,
            float velocityX, float velocityY)
        {
            return BodyInside(in target.BodyRegion, 0f, x, y) &&
                Math.Abs(velocityX) <= target.MaxAbsVelocityX &&
                Math.Abs(velocityY) <= target.MaxAbsVelocityY;
        }

        private static bool BodyInside(in RectF bounds, float margin, float x, float y)
        {
            return Finite(x) && Finite(y) &&
                x >= bounds.Left + margin &&
                y >= bounds.Top + margin &&
                x + WitchBroomMotion.HitboxWidth <= bounds.Right - margin &&
                y + WitchBroomMotion.HitboxHeight <= bounds.Bottom - margin;
        }

        private static bool ValidRect(in RectF value)
        {
            return Finite(value.X) && Finite(value.Y) && Finite(value.Width) &&
                Finite(value.Height) && value.Width > 0f && value.Height > 0f &&
                Finite(value.Right) && Finite(value.Bottom);
        }

        private static bool FiniteMotion(in WitchBroomMotionSnapshot state)
        {
            return Finite(state.PositionX) && Finite(state.PositionY) &&
                Finite(state.VelocityX) && Finite(state.VelocityY);
        }

        private static bool EquivalentMotion(in WitchBroomMotionSnapshot expected,
            in WitchBroomMotionSnapshot observed)
        {
            return observed.Known == expected.Known &&
                observed.MountActive == expected.MountActive &&
                observed.MountType == expected.MountType &&
                observed.FrameState == expected.FrameState &&
                Near(observed.PositionX, expected.PositionX) &&
                Near(observed.PositionY, expected.PositionY) &&
                Near(observed.VelocityX, expected.VelocityX) &&
                Near(observed.VelocityY, expected.VelocityY) &&
                Near(observed.Gravity, expected.Gravity) &&
                Near(observed.TrackBoost, expected.TrackBoost) &&
                observed.ReleaseUp == expected.ReleaseUp &&
                observed.SlowFall == expected.SlowFall &&
                observed.NormalGravity == expected.NormalGravity &&
                observed.Dry == expected.Dry &&
                observed.OpenDryPath == expected.OpenDryPath &&
                observed.PortalPhysicsDisabled == expected.PortalPhysicsDisabled &&
                observed.Grappling == expected.Grappling &&
                observed.HookInFlight == expected.HookInFlight &&
                observed.DashInProgress == expected.DashInProgress &&
                observed.CrowdControlled == expected.CrowdControlled &&
                observed.Tongued == expected.Tongued &&
                observed.Dead == expected.Dead &&
                observed.Pulley == expected.Pulley &&
                observed.Sliding == expected.Sliding &&
                observed.WindPushed == expected.WindPushed &&
                observed.ForcedMotion == expected.ForcedMotion;
        }

        private static bool WithinSingleCollisionStep(float x, float y) =>
            (double)x * x + (double)y * y <=
            (double)WitchBroomMotion.MaxSingleCollisionStep *
            WitchBroomMotion.MaxSingleCollisionStep;

        private static bool Near(float left, float right) =>
            Finite(left) && Finite(right) && Math.Abs(left - right) <= FloatTolerance;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Fail(ref WitchBroomRescueResult result,
            WitchBroomRescueFailure failure, WitchBroomRescuePhase phase, int index)
        {
            result.Accepted = false;
            result.Failure = failure;
            result.Phase = phase;
            result.FailedIndex = index;
            return false;
        }
    }
}
