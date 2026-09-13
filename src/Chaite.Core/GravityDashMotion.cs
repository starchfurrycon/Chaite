using System;

namespace Chaite.Core
{
    /// <summary>
    /// Exact identities which can establish vanilla gravity-control semantics.
    /// Values outside this closed set are deliberately unsupported.
    /// </summary>
    public enum GravityControlIdentity
    {
        Unknown = 0,
        GravitationBuff18 = 1,
        GravityGlobeItem1131 = 2,
        GravitationBuff18AndGravityGlobe1131 = 3
    }

    public enum GravityFlipPhase
    {
        Unsupported = 0,
        NoEdge = 1,
        Flipped = 2,
        ForcedInverted = 3,
        MountedForcedNormal = 4
    }

    /// <summary>
    /// Observation immediately before Player.Update's gravity-control branch.
    /// The contract is intentionally independent of reflection and input writes.
    /// </summary>
    public struct GravityFlipState
    {
        public bool Known;
        public bool NormalPlayerUpdatePath;
        public GravityControlIdentity Identity;
        public bool GravControl;
        public bool GravControl2;
        public int ForcedGravity;
        public bool MountActive;
        public bool ControlUp;
        public bool ReleaseUp;
        public float GravityDirection;
        public float PositionY;
        public float VelocityY;
        public int JumpTicks;
        public int FallStart;
    }

    /// <summary>
    /// A prediction-only input candidate. This type never writes Terraria input.
    /// A caller must trajectory-score the resulting state before authorizing it.
    /// </summary>
    public struct GravityFlipCandidate
    {
        public bool Known;
        public bool ControlUp;
        public float ExpectedGravityDirection;
    }

    public static class GravityFlipMotion
    {
        /// <summary>
        /// Builds the single-tick Up pulse used by the Gravitation buff. The same
        /// pulse changes +1 to -1 and -1 to +1; Down is never a reversal input.
        /// </summary>
        public static bool TryCreatePotionCandidate(in GravityFlipState state,
            float desiredGravityDirection, out GravityFlipCandidate candidate)
        {
            candidate = default(GravityFlipCandidate);
            if (!IsValid(state) || !HasPotion(state.Identity) || state.ForcedGravity != 0 ||
                state.MountActive || !state.ReleaseUp || !IsDirection(desiredGravityDirection) ||
                desiredGravityDirection != -state.GravityDirection)
                return false;

            candidate = new GravityFlipCandidate
            {
                Known = true,
                ControlUp = true,
                ExpectedGravityDirection = desiredGravityDirection
            };
            return true;
        }

        /// <summary>
        /// Advances only the reviewed gravity-control branch plus the immediately
        /// following UpdateControlHolds release-Up transition. Invalid observations
        /// are returned unchanged.
        /// </summary>
        public static GravityFlipPhase TryAdvanceNativeBranch(ref GravityFlipState state,
            bool controlUp)
        {
            if (!IsValid(state)) return GravityFlipPhase.Unsupported;

            var next = state;
            next.ControlUp = controlUp;
            GravityFlipPhase phase;
            if (next.ForcedGravity > 0)
            {
                next.GravityDirection = -1f;
                phase = GravityFlipPhase.ForcedInverted;
            }
            else if (next.MountActive)
            {
                // Both gravControl branches are gated by !mount.Active. Their
                // shared fallback restores ordinary gravity on the reviewed path.
                next.GravityDirection = 1f;
                phase = GravityFlipPhase.MountedForcedNormal;
            }
            else if (controlUp && next.ReleaseUp)
            {
                next.GravityDirection = -next.GravityDirection;
                next.FallStart = (int)(next.PositionY / 16f);
                next.JumpTicks = 0;
                phase = GravityFlipPhase.Flipped;
            }
            else
            {
                phase = GravityFlipPhase.NoEdge;
            }

            // Player.Update calls UpdateControlHolds immediately after this
            // branch: holding Up consumes the edge; one released update rearms it.
            next.ReleaseUp = !controlUp;
            state = next;
            return phase;
        }

        private static bool IsValid(in GravityFlipState state)
        {
            if (!state.Known || !state.NormalPlayerUpdatePath || state.ForcedGravity < 0 ||
                !IsDirection(state.GravityDirection) || !IsFinite(state.PositionY) ||
                !IsFinite(state.VelocityY))
                return false;

            var tileY = state.PositionY / 16f;
            if (!IsFinite(tileY) || tileY < int.MinValue || tileY > int.MaxValue)
                return false;

            switch (state.Identity)
            {
                case GravityControlIdentity.GravitationBuff18:
                    return state.GravControl && !state.GravControl2;
                case GravityControlIdentity.GravityGlobeItem1131:
                    return !state.GravControl && state.GravControl2;
                case GravityControlIdentity.GravitationBuff18AndGravityGlobe1131:
                    return state.GravControl && state.GravControl2;
                default:
                    return false;
            }
        }

        private static bool HasPotion(GravityControlIdentity identity) =>
            identity == GravityControlIdentity.GravitationBuff18 ||
            identity == GravityControlIdentity.GravitationBuff18AndGravityGlobe1131;

        private static bool IsDirection(float value) => value == 1f || value == -1f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum DashEquipmentIdentity
    {
        Unknown = 0,
        ShieldOfCthulhuItem3097 = 1
    }

    public enum EyeShieldDashPhase
    {
        Unsupported = 0,
        Ready = 1,
        StartedClear = 2,
        StartedIntoSolidProbe = 3,
        ActiveHighSpeed = 4,
        ActiveLowSpeed = 5,
        EnteredCooldown = 6,
        Cooldown = 7,
        ReadyNextUpdate = 8
    }

    /// <summary>
    /// Closed vanilla 1.4.5.8 Shield of Cthulhu dash profile. It excludes
    /// mounts, pulley/grapple/tongue paths, NPC contact and old-style parkour.
    /// Those states require a different reviewed trajectory contract.
    /// </summary>
    public struct EyeShieldDashState
    {
        public bool Known;
        public bool NormalPlayerUpdatePath;
        public DashEquipmentIdentity EquipmentIdentity;
        public int DashType;
        public int Dash;
        public int DashDelay;
        public int DashTime;
        public int TimeSinceLastDashStarted;
        public int EocDash;
        public int EocHit;
        public bool CrowdControlled;
        public bool MountActive;
        public bool Pulley;
        public bool Grappling;
        public bool Tongued;
        public bool OldStyleParkour;
        public bool ControlDash;
        public bool ReleaseDash;
        public bool ControlLeft;
        public bool ControlRight;
        public int FacingDirection;
        public float VelocityX;
        public float VelocityY;
        public float AccRunSpeed;
        public float MaxRunSpeed;
        public bool ForwardSolidProbeKnown;
        public bool ForwardSolidProbeBlocked;
        public bool HostileContactKnown;
        public bool HostileContact;
    }

    /// <summary>Prediction result only; it has no delegate or production input hook.</summary>
    public struct EyeShieldDashCandidate
    {
        public bool Known;
        public int Direction;
        public EyeShieldDashPhase Phase;
        public EyeShieldDashState AfterDashMovement;
    }

    public static class EyeShieldDashMotion
    {
        public const float CertifiedStartSpeed = 14.5f;
        private const float HighSpeedThreshold = 12f;
        private const float HighSpeedDecay = .985f;
        private const float LowSpeedDecay = .94f;
        private const int CooldownTicks = 30;
        private const int ContactWindowTicks = 15;

        /// <summary>
        /// Models TriggersSet.CopyInto: releaseDash is sticky and is rearmed by
        /// a copied false Dash control. This must happen before pending replay.
        /// </summary>
        public static bool TryApplyNativeInputCopy(ref EyeShieldDashState state, bool rawControlDash)
        {
            if (!IsValid(state)) return false;
            var next = state;
            next.ControlDash = rawControlDash;
            next.ReleaseDash = next.ReleaseDash || !rawControlDash;
            state = next;
            return true;
        }

        /// <summary>
        /// Models only Chaite's post-CopyInto replay assignment. Unlike native
        /// input copy, replaying false does not itself rearm releaseDash.
        /// </summary>
        public static bool TryReplayPlannedControl(ref EyeShieldDashState state, bool controlDash)
        {
            if (!IsValid(state)) return false;
            state.ControlDash = controlDash;
            return true;
        }

        /// <summary>
        /// Predicts a dedicated-key Shield dash. The candidate must still be
        /// checked by the arena/target trajectory scorer before it is used.
        /// </summary>
        public static bool TryCreateDedicatedCandidate(in EyeShieldDashState state,
            out EyeShieldDashCandidate candidate)
        {
            candidate = default(EyeShieldDashCandidate);
            if (!IsValid(state) || state.DashDelay != 0 || state.EocDash != 0 ||
                state.EocHit != -1 || !state.ControlDash || !state.ReleaseDash ||
                state.CrowdControlled || state.MountActive || !state.ForwardSolidProbeKnown)
                return false;

            var direction = ResolveDedicatedDirection(state.FacingDirection,
                state.ControlLeft, state.ControlRight);
            var next = state;
            next.Dash = 2;
            next.DashTime = 0;
            next.TimeSinceLastDashStarted = 0;
            next.ReleaseDash = false;
            next.VelocityX = CertifiedStartSpeed * direction;
            if (next.ForwardSolidProbeBlocked) next.VelocityX /= 2f;
            next.DashDelay = -1;
            next.EocDash = ContactWindowTicks;

            candidate = new EyeShieldDashCandidate
            {
                Known = true,
                Direction = direction,
                Phase = state.ForwardSolidProbeBlocked
                    ? EyeShieldDashPhase.StartedIntoSolidProbe
                    : EyeShieldDashPhase.StartedClear,
                AfterDashMovement = next
            };
            return true;
        }

        /// <summary>
        /// Advances one collision-free DashMovement tick after a reviewed start.
        /// Potential or unknown hostile contact is rejected because the shield's
        /// hit/bounce/immune path is outside this movement-only contract.
        /// </summary>
        public static EyeShieldDashPhase TryAdvanceCollisionFreeTick(ref EyeShieldDashState state)
        {
            if (!IsValid(state) || state.CrowdControlled || state.MountActive)
                return EyeShieldDashPhase.Unsupported;

            if (state.DashDelay == 0)
                return EyeShieldDashPhase.Ready;
            if (!state.HostileContactKnown || state.HostileContact)
                return EyeShieldDashPhase.Unsupported;

            var next = state;
            if (next.DashDelay > 0)
            {
                if (next.EocDash > 0)
                {
                    next.EocDash--;
                    if (next.EocDash == 0) next.EocHit = -1;
                }
                next.DashDelay--;
                var cooldownPhase = next.DashDelay == 0
                    ? EyeShieldDashPhase.ReadyNextUpdate
                    : EyeShieldDashPhase.Cooldown;
                state = next;
                return cooldownPhase;
            }

            // For this identity the sole reviewed negative value is -1.
            var speed = Math.Abs(next.VelocityX);
            if (speed > HighSpeedThreshold)
            {
                next.VelocityX *= HighSpeedDecay;
                if (!IsFinite(next.VelocityX)) return EyeShieldDashPhase.Unsupported;
                state = next;
                return EyeShieldDashPhase.ActiveHighSpeed;
            }

            var runSpeed = Math.Max(next.AccRunSpeed, next.MaxRunSpeed);
            if (speed > runSpeed)
            {
                next.VelocityX *= LowSpeedDecay;
                if (!IsFinite(next.VelocityX)) return EyeShieldDashPhase.Unsupported;
                state = next;
                return EyeShieldDashPhase.ActiveLowSpeed;
            }

            next.DashDelay = CooldownTicks;
            if (next.VelocityX < 0f || next.VelocityX == 0f && next.ControlLeft)
                next.VelocityX = -runSpeed;
            else if (next.VelocityX > 0f || next.VelocityX == 0f && next.ControlRight)
                next.VelocityX = runSpeed;
            state = next;
            return EyeShieldDashPhase.EnteredCooldown;
        }

        public static bool IsReady(in EyeShieldDashState state) =>
            IsValid(state) && !state.CrowdControlled && !state.MountActive &&
            state.DashDelay == 0 && state.EocDash == 0 && state.EocHit == -1 &&
            state.ReleaseDash;

        /// <summary>
        /// True while the exact reviewed shield identity and native state
        /// contract remain valid, including its active/cooldown frames. This
        /// identifies a live route; unlike IsReady it does not authorize a new
        /// dash edge.
        /// </summary>
        public static bool IsSupportedState(in EyeShieldDashState state) =>
            IsValid(state) && !state.MountActive;

        private static int ResolveDedicatedDirection(int facing, bool left, bool right)
        {
            var horizontal = (right ? 1 : 0) - (left ? 1 : 0);
            return horizontal == -facing ? horizontal : facing;
        }

        private static bool IsValid(in EyeShieldDashState state)
        {
            if (!state.Known || !state.NormalPlayerUpdatePath ||
                state.EquipmentIdentity != DashEquipmentIdentity.ShieldOfCthulhuItem3097 ||
                state.DashType != 2 || state.Dash != 2 ||
                state.DashDelay < -1 || state.DashDelay > CooldownTicks ||
                state.DashTime < -15 || state.DashTime > 15 ||
                state.TimeSinceLastDashStarted < 0 || state.TimeSinceLastDashStarted > 300 ||
                state.EocDash < 0 || state.EocDash > ContactWindowTicks || state.EocHit < -1 ||
                state.FacingDirection != -1 && state.FacingDirection != 1 ||
                state.Pulley || state.Grappling || state.Tongued || state.OldStyleParkour ||
                !IsFinite(state.VelocityX) || !IsFinite(state.VelocityY) ||
                !IsFinite(state.AccRunSpeed) || !IsFinite(state.MaxRunSpeed))
                return false;

            var runSpeed = Math.Max(state.AccRunSpeed, state.MaxRunSpeed);
            if (!(runSpeed > 0f) || runSpeed > HighSpeedThreshold)
                return false;

            // Collision-free predictions never retain a contacted NPC identity.
            if (state.EocHit != -1) return false;
            if (state.DashDelay == -1 && state.EocDash != ContactWindowTicks) return false;
            if (state.DashDelay == 0 && state.EocDash != 0) return false;
            if (state.DashDelay > 0 && state.EocDash != Math.Max(0, state.DashDelay - 15))
                return false;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
