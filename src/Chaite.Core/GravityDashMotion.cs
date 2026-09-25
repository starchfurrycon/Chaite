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
        ShieldOfCthulhuItem3097 = 1,
        MasterNinjaGearItem984 = 2,
        CrystalAssassinArmorSet = 3
    }

    public static class ReviewedDashIdentity
    {
        public static bool IsSupported(DashEquipmentIdentity identity) =>
            identity == DashEquipmentIdentity.ShieldOfCthulhuItem3097 ||
            identity == DashEquipmentIdentity.MasterNinjaGearItem984 ||
            identity == DashEquipmentIdentity.CrystalAssassinArmorSet;
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
        /// <summary>The two fields the engine's own horizontal step decelerates
        /// by. The dash reuses them, so they are carried here rather than being
        /// baked in as constants.</summary>
        public float RunSlowdown;
        public float RunAcceleration;
        public bool Grounded;
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
        // Re-measured per tick from the replay dense trace, and the form of the
        // rule matters as much as the number. The engine does not scale the
        // speed: it removes a tenth of a pixel per tick and then scales what is
        // left, so the apparent ratio drifts upward with speed and reading a
        // ratio off one speed is what made the earlier constants approximate.
        // High speed is exactly (14.5 - .1) * .985 = 14.184, and low speed is
        // exactly (11.8208 - .1) * .94 = 11.0176, both matching the trace to
        // four decimals across their whole runs. The 0.9782 and 0.9304 recorded
        // before were the apparent ratios of these two forms at fourteen and a
        // half and at nine, which is why they held the wrong speed six ticks in.
        private const float HighSpeedDecay = .985f;
        private const float LowSpeedDecay = .94f;
        /// <summary>The fixed amount the engine removes before scaling.</summary>
        internal const float DashSpeedBleed = .1f;
        /// <summary>The same quantity when the held direction fights the dash.
        /// See the rule below for how this was pinned.</summary>
        internal const float OpposingSpeedBleed = .4512f;
        private const int CooldownTicks = 30;
        private const int ContactWindowTicks = 15;
        /// <summary>The dash identity the reviewed start belongs to: the
        /// decompile's `dash == 2`, the branch that writes fourteen and a half
        /// and opens the fifteen-tick contact window. <see cref="IsValid"/>
        /// already required this value of a state handed in from a sample; it is
        /// named because the frame-level ready state below has no sample to read
        /// it from.</summary>
        private const int ReviewedDashType = 2;

        /// <summary>The measured decay, in one rule:
        ///
        ///     bleed  = 0.1 when the input runs with the dash, 0.4512 when it
        ///              opposes it
        ///     speed  = |velocityX| - bleed
        ///     decay  = 0.985 when speed is above 12, else 0.94
        ///     result = sign * speed * decay
        ///
        /// The shape was the hard part and it took several wrong answers to
        /// reach. Two things had to be seen at once.
        ///
        /// First, the bleed and the scale are separate: the engine removes a
        /// fixed amount and then scales what is left, rather than scaling the
        /// speed. Reading a ratio off one speed gave 0.978 and 0.930, which are
        /// the apparent ratios of this form at fourteen and a half and at nine,
        /// and a ratio read at one speed holds the wrong speed a few ticks in.
        ///
        /// Second, the bleed depends on the input, and the rate test is applied
        /// to the speed after the bleed rather than before it. That is why the
        /// rate seemed to switch at a different speed in every dash. Measured
        /// raw switch speeds were 11.82 for dashes with no opposing input and
        /// 12.24, 12.39 and 12.58 for dashes with opposing input, which no
        /// single threshold can produce. Subtracting the bleed first puts every
        /// one of them on the same side of 12: 11.72 against 11.93, 12.00 and
        /// 12.13.
        ///
        /// The value 0.4512 is pinned rather than fitted. Solving consecutive
        /// pairs of an opposing dash for the bleed in each rate separately gives
        /// 0.451199 in the fast rate and 0.451199 in the slow rate, from the
        /// same dash and from a second one, to six digits. Sign is preserved and
        /// the magnitude cannot cross zero, so a decay can never reverse a dash.
        /// </summary>
        internal static float ApplyDashBleed(float velocityX, float bleed)
        {
            var magnitude = Math.Abs(velocityX) - bleed;
            if (magnitude < 0f) magnitude = 0f;
            var decay = magnitude > HighSpeedThreshold
                ? HighSpeedDecay
                : LowSpeedDecay;
            return velocityX < 0f ? -magnitude * decay : magnitude * decay;
        }

        /// <summary>
        /// The bleed alone, with no scaling and no floor. This is what the engine
        /// does below the acceleration-run speed when the input opposes the motion:
        /// over eight consecutive airborne frames it steps by exactly minus four
        /// five one two ten-thousandths each time, from seven point five four eight
        /// eight down through six point five four six four to four point two nine
        /// zero four, with no multiplicative decay and nothing holding it at eight.
        /// Scaling those frames by ninety-four hundredths is what made the model
        /// undershoot, and clamping them to eight is what pinned it earlier.
        /// </summary>
        internal static float ApplyBleedOnly(float velocityX, float bleed)
        {
            var magnitude = Math.Abs(velocityX) - bleed;
            if (magnitude < 0f) magnitude = 0f;
            return velocityX < 0f ? -magnitude : magnitude;
        }

        /// <summary>
        /// The brake the engine applies when the input opposes the motion, which is
        /// the one place the over-speed rule and the ordinary bleed meet. The
        /// open-loop comparison pins both halves exactly: from eight point two nine
        /// nine seven the engine reports eight, which is the bled and scaled seven
        /// point three seven seven six raised back to the acceleration-run speed, and
        /// from exactly eight it reports seven point five four eight eight, which is
        /// the bleed alone with no scaling and no floor. So the test is strict -- at
        /// or below the acceleration-run speed the engine only bleeds -- and the
        /// floor belongs to the branch above it.
        ///
        /// This is the same rule three earlier attempts got wrong, and the reason
        /// they could not be checked is recorded at the call site: walking the trace
        /// compares a diverged model against a trace that no longer describes it.
        /// The open loop compares the two trajectories from a common origin instead,
        /// and it shows the model exact for nine frames and first wrong on this one.
        /// </summary>
        internal static float ApplyOpposingBrake(float velocityX, float bleed,
            float accRunSpeed)
        {
            // A profile that does not carry the acceleration-run speed cannot use
            // this rule: with zero, the strict test below would be true at every
            // speed and the decay would be applied where the engine only bleeds.
            // The caller guards on it, and this returns the plain bleed so that a
            // synthetic profile behaves as it did before the rule existed.
            if (accRunSpeed <= 0f) return ApplyBleedOnly(velocityX, bleed);
            var magnitude = Math.Abs(velocityX);
            var bled = magnitude - bleed;
            if (bled < 0f) bled = 0f;
            if (magnitude > accRunSpeed)
            {
                bled *= bled > HighSpeedThreshold ? HighSpeedDecay : LowSpeedDecay;
                if (bled < accRunSpeed) bled = accRunSpeed;
            }
            return velocityX < 0f ? -bled : bled;
        }

        /// <summary>The amount the dash removes before scaling, which is the
        /// engine's ordinary horizontal deceleration rather than a constant of
        /// the dash. Both values are read from the trace's own player fields:
        /// run slowdown is nought point two and run acceleration is nought point
        /// two five one two there. Opposing input decelerates by their sum, four
        /// five one two ten-thousandths, and anything else decelerates by run
        /// slowdown, halved while airborne, which is the tenth of a pixel the
        /// trace shows. Deriving it this way is what makes the rule hold across
        /// loadouts instead of only for the one that was measured.</summary>
        private static float DashBleed(in EyeShieldDashState state, bool opposing)
        {
            // The derived form -- run slowdown plus run acceleration when
            // opposing, run slowdown halved when airborne otherwise -- is exact
            // on the trace that produced it, but wiring it to the state fields
            // moved the first divergence from tick 251 back to 245, so the
            // forward model is not feeding those fields the values the engine
            // used at that frame. Until that is found, the measured pair stays
            // as constants. Reverting a correct derivation because its inputs are
            // wrong is the honest move; the derivation is recorded above.
            return opposing ? OpposingSpeedBleed : DashSpeedBleed;
        }

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
        /// Materialises the ready state a Shield dash starts from when the frame
        /// carries the reviewed identity and the readiness flag instead of a
        /// native sample.
        ///
        /// A frame a rollout built has no engine behind it, so dashType,
        /// dashDelay, dashTime, eocDash and the release edge have to come from
        /// the review's own contract rather than from a reading, and none of
        /// these values is a new measurement: they are what the engine's ready
        /// path holds, which <see cref="IsValid"/> already requires. The velocity
        /// and the two run speeds are the frame's, because the decay below is
        /// read against them.
        ///
        /// The collision-free contract is the same one the candidate enforces:
        /// this model has no tile probe and no hostile-contact prediction, so the
        /// only start it can replay is the one into clear space with no contact.
        /// A caller that needs the halved blocked-probe start has to hand in the
        /// native state instead.
        ///
        /// The direction is the caller's, and the resolver below returns the held
        /// direction whenever one is held, so passing it as the facing gives the
        /// direction the engine would take. A request with no direction has no
        /// facing to fall back on, and is rejected rather than guessed.
        /// </summary>
        public static bool TryCreateFrameReadyState(DashEquipmentIdentity identity,
            int direction, float velocityX, float velocityY, float accRunSpeed,
            float maxRunSpeed, out EyeShieldDashState state)
        {
            state = default(EyeShieldDashState);
            if (!ReviewedDashIdentity.IsSupported(identity)) return false;
            if (direction != 1 && direction != -1) return false;

            state = new EyeShieldDashState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                EquipmentIdentity = identity,
                DashType = ReviewedDashType,
                Dash = ReviewedDashType,
                // Zero delay with nothing open is the ready edge; releaseDash
                // armed is what makes the request carrying it a start.
                DashDelay = 0,
                DashTime = 0,
                TimeSinceLastDashStarted = 0,
                EocDash = 0,
                EocHit = -1,
                ReleaseDash = true,
                FacingDirection = direction,
                VelocityX = velocityX,
                VelocityY = velocityY,
                AccRunSpeed = accRunSpeed,
                MaxRunSpeed = maxRunSpeed,
                ForwardSolidProbeKnown = true,
                HostileContactKnown = true
            };
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
            next.Dash = ReviewedDashType;
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
            var opposing = next.ControlLeft && next.VelocityX > 0f ||
                next.ControlRight && next.VelocityX < 0f;
            var runSpeed = Math.Max(next.AccRunSpeed, next.MaxRunSpeed);
            var bleed = DashBleed(in next, opposing);
            var bled = Math.Abs(next.VelocityX) - bleed;
            if (bled < 0f) bled = 0f;
            // The rate test reads the speed after the bleed, which is what makes
            // every measured switch speed agree on one threshold. Reading it
            // before the bleed is why the switch looked like a different speed in
            // every dash.
            if (bled > HighSpeedThreshold)
            {
                next.VelocityX = ApplyDashBleed(next.VelocityX, bleed);
                if (!IsFinite(next.VelocityX)) return EyeShieldDashPhase.Unsupported;
                state = next;
                return EyeShieldDashPhase.ActiveHighSpeed;
            }
            if (bled > runSpeed)
            {
                next.VelocityX = ApplyDashBleed(next.VelocityX, bleed);
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
            // Route admission is a liveness check, not a collision-free dash
            // proof. After a legitimate Shield dash vanilla keeps the same
            // equipment identity while DashDelay/EocDash are in cooldown and
            // EocHit may contain the NPC just contacted. IsValid deliberately
            // rejects those states for trajectory rollout, so using it here
            // incorrectly ended an otherwise valid Fishron takeover.
            state.Known && state.NormalPlayerUpdatePath &&
            ReviewedDashIdentity.IsSupported(state.EquipmentIdentity) &&
            state.DashType == ReviewedDashType && state.Dash == ReviewedDashType &&
            !state.MountActive &&
            !state.Pulley && !state.Grappling && !state.Tongued &&
            !state.OldStyleParkour && state.DashDelay >= -1 &&
            state.DashDelay <= CooldownTicks && state.DashTime >= -15 &&
            state.DashTime <= 15 && state.TimeSinceLastDashStarted >= 0 &&
            state.TimeSinceLastDashStarted <= 300 && state.EocDash >= 0 &&
            state.EocDash <= ContactWindowTicks && state.EocHit >= -1 &&
            IsFinite(state.VelocityX) && IsFinite(state.VelocityY) &&
            IsFinite(state.AccRunSpeed) && IsFinite(state.MaxRunSpeed) &&
            state.AccRunSpeed > 0f && state.MaxRunSpeed > 0f;

        private static int ResolveDedicatedDirection(int facing, bool left, bool right)
        {
            var horizontal = (right ? 1 : 0) - (left ? 1 : 0);
            return horizontal == -facing ? horizontal : facing;
        }

        private static bool IsValid(in EyeShieldDashState state)
        {
            if (!state.Known || !state.NormalPlayerUpdatePath ||
                !ReviewedDashIdentity.IsSupported(state.EquipmentIdentity) ||
                state.DashType != ReviewedDashType || state.Dash != ReviewedDashType ||
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
