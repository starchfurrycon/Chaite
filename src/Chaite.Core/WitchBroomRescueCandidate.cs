namespace Chaite.Core
{
    public enum WitchBroomCandidateStage
    {
        Unavailable,
        ActivationPreview,
        NativeConfirmation
    }

    /// <summary>
    /// Reused adapter-to-planner transport. Arrays are allocated once with the
    /// CombatSnapshot and are never replaced on the per-frame path.
    /// </summary>
    public sealed class WitchBroomRescueCandidateSnapshot
    {
        public const int MountedCapacity = 96;
        public const int ReturnCapacity = 4;

        public WitchBroomCandidateStage Stage;
        public long RouteIdentity;
        public int EscapeDirection;
        public WitchBroomRescueRequest Request;
        public readonly WitchBroomRescueTick[] MountedTicks =
            new WitchBroomRescueTick[MountedCapacity];
        public readonly WitchBroomReturnTick[] ReturnTicks =
            new WitchBroomReturnTick[ReturnCapacity];
        public int MountedCount;
        public int ReturnCount;

        public bool Available => Stage != WitchBroomCandidateStage.Unavailable;

        public void Reset()
        {
            Stage = WitchBroomCandidateStage.Unavailable;
            RouteIdentity = 0;
            EscapeDirection = 0;
            Request = default(WitchBroomRescueRequest);
            MountedCount = 0;
            ReturnCount = 0;
        }

        public bool TryEvaluate(out WitchBroomRescueResult result)
        {
            if (Stage == WitchBroomCandidateStage.ActivationPreview)
                return WitchBroomRescueTrajectory.TryEvaluateActivationPreview(in Request,
                    MountedTicks, 0, MountedCount, ReturnTicks, 0, ReturnCount, out result);
            if (Stage == WitchBroomCandidateStage.NativeConfirmation)
                return WitchBroomRescueTrajectory.TryEvaluate(in Request,
                    MountedTicks, 0, MountedCount, ReturnTicks, 0, ReturnCount, out result);
            result = new WitchBroomRescueResult
            {
                Failure = WitchBroomRescueFailure.InvalidRequest,
                FailedIndex = -1
            };
            return false;
        }
    }

    public sealed class WitchBroomRescueCandidateSet
    {
        public readonly WitchBroomRescueCandidateSnapshot Left =
            new WitchBroomRescueCandidateSnapshot();
        public readonly WitchBroomRescueCandidateSnapshot Right =
            new WitchBroomRescueCandidateSnapshot();

        public WitchBroomRescueCandidateSnapshot Get(int index) => index == 0 ? Left : Right;

        public void Reset()
        {
            Left.Reset();
            Right.Reset();
        }
    }
}
