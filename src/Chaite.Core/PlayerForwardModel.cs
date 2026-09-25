using System;

namespace Chaite.Core
{
    /// <summary>Why the forward model declined to predict a tick.
    ///
    /// Prediction is fail-closed on purpose. A search that prunes on a
    /// trajectory must never be handed a plausible-looking wrong one, so a
    /// regime that is not modeled is named instead of approximated. The replay
    /// harness counts refusals by reason, which is how the unmodeled surface
    /// stays visible instead of quietly biasing the error statistics.
    /// </summary>
    public enum ForwardModelRefusal
    {
        None = 0,
        /// <summary>A mount is active. No measured model replays mount
        /// locomotion.</summary>
        Mounted,
        /// <summary>A grapple is attached or was this tick.</summary>
        Grappling,
        /// <summary>The dash is engaged, or one is requested on a frame that
        /// cannot say which reviewed dash it would start or in which direction.
        /// <see cref="HorizontalMotion"/> states outright that it does not replay
        /// dash physics, and a dash also overrides gravity for its duration.</summary>
        Dashing,
        /// <summary>Rocket boots are firing.</summary>
        RocketBoots,
        /// <summary>A wing is holding the player up. The measured wing
        /// deceleration is not the same quantity as gravity, so entering the
        /// ballistic branch under a live wing would silently predict a fall.</summary>
        Wings,
        /// <summary>The jump control is down. The recorded observation does not
        /// carry the multi-jump charge state, and inventing it would be a
        /// derived constant.</summary>
        Jumping,
        /// <summary>Inverted gravity. The measured vertical fixture declares
        /// normal gravity only, so the clamp direction is unverified here.</summary>
        InvertedGravity,
        /// <summary>Frozen, webbed, stoned or otherwise held in place.</summary>
        Immobilized,
        /// <summary>In water, honey or lava, all of which change the drag.</summary>
        Liquid,
        /// <summary>A required native scalar was missing or not finite.</summary>
        InvalidInput,
        /// <summary>The state is outside the loop being searched. A loop is a
        /// slice of the fight and the Boss's script is only recorded inside it,
        /// so past either end there is no threat field to step into.</summary>
        PastLoopEnd,
        /// <summary>The threat field is incomplete: the recording had to drop
        /// hostile projectiles. Refused rather than scored, because a hit that
        /// cannot be seen would be reported as a clean tick.</summary>
        ThreatIncomplete,
        /// <summary>The predicted position leaves the playable span. The
        /// forward model has no tile collision, so a wall contact is exactly the
        /// case it would predict wrongly; refusing is what keeps that gap from
        /// becoming a wrong answer.</summary>
        OutsideArena,
    }

    /// <summary>One tick of engine-observed player state together with the
    /// control bits the engine actually applied on that tick.
    ///
    /// Deliberately a plain struct with no reference to the probe: the offline
    /// replay harness fills it from a recorded trace and production can fill it
    /// from the live snapshot, so both sides provably run the same model rather
    /// than two similar ones.</summary>
    /// <summary>The Boss's own position and velocity, carried on the frame.
    ///
    /// It cannot live in the world, because the world is shared across the whole
    /// search tree: two branches that took different routes put the player in
    /// different places, so the Boss that chases them is in different places too,
    /// and a single copy in the world would mix them. It cannot be read from the
    /// recording either, which is what the first attempt did and why it failed:
    /// the recorded Boss describes the recorded route.
    ///
    /// Measured, on one candidate against its source trace: the player ended up
    /// two thousand pixels away from where the recording had it, and the Boss
    /// five thousand six hundred pixels from its recorded place.</summary>
    public struct BossChaseState
    {
        /// <summary>False until the first tick has taken the Boss's starting
        /// position from the recording. A default frame has no Boss, and the
        /// search starts from a default frame.</summary>
        public bool Active;
        public Vec2 Position;
        public Vec2 Velocity;
    }

    public struct PlayerMotionFrame
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>The engine tick this frame was sampled at. A search walks a
        /// loop tick by tick and the Boss's script is indexed by tick, so a
        /// state without one cannot say which tick of the loop it is on.</summary>
        public int Tick;
        public int Width;
        public int Height;
        public float Gravity;
        public float MaxFallSpeed;
        /// <summary>+1 normal gravity, -1 inverted.</summary>
        public int GravityDirection;
        public float BaseRunSpeed;
        public float MaxRunSpeed;
        /// <summary>The acceleration-run speed. Below it an opposing input only
        /// bleeds; at or above it the engine applies the bleed and then the
        /// multiplicative decay. The trace puts the boundary exactly here: every
        /// opposing frame at or above eight was decayed and every one below eight
        /// stepped by the bleed alone.</summary>
        public float AccRunSpeed;
        public float RunAcceleration;
        public float SprintAcceleration;
        public float RunSlowdown;
        public bool CanSprintInAir;
        public bool Grounded;
        /// <summary>The engine applied a jump impulse on this tick. This is the
        /// engine's own record, not the control bit, and the two differ: an
        /// auto-jump, a wing flap and a rocket all launch the player with the
        /// jump control down. Trusting the control bit alone let the model treat
        /// a launch as standing still, which is how a grounded tick came out
        /// 6.2 px wrong.</summary>
        public bool JustJumped;
        /// <summary>World Y of the surface the player rests on. Measured once
        /// per trace rather than per row, the same way the native vertical
        /// fixture derives it.</summary>
        public float FloorY;
        /// <summary>Wing and rocket-boot resource, and the dash state. Carried on
        /// the frame rather than derived so the replay harness and production
        /// provably feed the same values to the same models.</summary>
        public float WingTime;
        public EyeShieldDashState Dash;
        /// <summary>The reviewed dash the loadout carries, and whether its
        /// cooldown is at zero.
        ///
        /// A frame filled from a trace or a live sample reads both facts from
        /// <see cref="Dash"/>, and a known dash state is authoritative: these two
        /// are not consulted at all then. A frame a rollout built has no sample to
        /// read them from -- every field of a default <see cref="EyeShieldDashState"/>
        /// is zero, which cannot be told apart from a dash that has just come off
        /// cooldown, and the identity of the dash is nowhere on the frame.
        /// Without them a rollout cannot start a dash at all: the control bit is
        /// read and then dropped, which is why a dash sweep over a rollout
        /// returned identical figures for every dash setting. The pair is the
        /// frame-level form of MobilitySnapshot's CanDash (a reviewed identity)
        /// and DashReady (dashDelay is zero).</summary>
        public DashEquipmentIdentity DashIdentity;
        public bool DashReady;
        /// <summary>The reviewed jump and flight inputs. The wing path needs the
        /// jump charge, the wing and rocket resources, and the engine's own
        /// justJumped flag, and none of them can be derived from the others.</summary>
        public JumpSnapshot Jump;
        public FlightSnapshot Flight;
        /// <summary>Threat state that travels with the branch. See
        /// <see cref="BossChaseState"/>.</summary>
        public BossChaseState Boss;

        // Refusal flags. All false is the plain regime the model does replay.
        public bool Mounted;
        public bool Grappling;
        public bool Dashing;
        public bool RocketBoots;
        public bool Immobilized;
        public bool Liquid;

        public bool IsFinite
        {
            get
            {
                return Finite(Position.X) && Finite(Position.Y) &&
                    Finite(Velocity.X) && Finite(Velocity.Y) &&
                    Finite(Gravity) && Gravity >= 0f &&
                    Finite(MaxFallSpeed) && MaxFallSpeed > 0f &&
                    Finite(BaseRunSpeed) && Finite(MaxRunSpeed) &&
                    Finite(RunAcceleration) && Finite(SprintAcceleration) &&
                    Finite(RunSlowdown) && Finite(FloorY) && Height > 0 &&
                    Finite(Dash.VelocityX) && Finite(Dash.VelocityY) &&
                    Finite(Dash.AccRunSpeed) && Finite(Dash.MaxRunSpeed) &&
                    Finite(Jump.Speed) && Finite(Jump.Height) &&
                    Finite(Flight.WingTime) && Finite(Flight.RocketTime);
            }
        }

        private static bool Finite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>The control bits the engine applied on one tick.</summary>
    public struct PlayerControlFrame
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Jump;
        public bool Dash;
        public bool Mount;
        public bool Hook;

        public int Direction
        {
            get
            {
                if (Left == Right) return 0;
                return Right ? 1 : -1;
            }
        }
    }

    /// <summary>
    /// Composes the per-mechanic, engine-measured motion models into one tick of
    /// player movement, so a closed-loop search can predict motion without
    /// running the engine.
    ///
    /// Every constant used here was measured in the engine and lives in the model
    /// it came from; nothing in this file introduces a new one. What this file
    /// adds is the ordering and the state threading between those models, which
    /// is exactly where composition bugs live and therefore what the replay
    /// harness checks against recorded engine ticks.
    /// </summary>
    public static class PlayerForwardModel
    {
        /// <summary>
        /// Advances exactly one tick.
        ///
        /// Native order is why this is not two independent calls: the horizontal
        /// step and the gravity step both read the velocity sampled at the start
        /// of the tick, and the position is then integrated with the post-step
        /// velocities. A ground contact rewrites velocityY to whatever the clamp
        /// actually moved, so the clamp happens before the result is reported
        /// rather than as a separate correction afterwards.
        /// </summary>
        public static bool TryAdvance(in PlayerMotionFrame frame,
            in PlayerControlFrame controls, out Vec2 position, out Vec2 velocity,
            out ForwardModelRefusal refusal)
        {
            PlayerMotionFrame next;
            var advanced = TryAdvance(in frame, in controls, out next,
                out refusal);
            position = next.Position;
            velocity = next.Velocity;
            return advanced;
        }

        /// <summary>
        /// Advances one tick and returns the whole next frame, not only its
        /// position and velocity.
        ///
        /// A search rolls the fight forward for hundreds of ticks, and the dash,
        /// jump and flight sub-states evolve as it does. Returning only the
        /// position and velocity leaves a caller no way to continue: it would
        /// have to rebuild the frame, and the only place those values exist is
        /// the trace, which a search does not have. That is why the replay
        /// harness could be built first and a rollout could not.
        /// </summary>
        public static bool TryAdvance(in PlayerMotionFrame frame,
            in PlayerControlFrame controls, out PlayerMotionFrame next,
            out ForwardModelRefusal refusal)
        {
            next = frame;
            refusal = Classify(frame);
            if (refusal != ForwardModelRefusal.None) return false;
            if (frame.GravityDirection < 0)
                return Refuse(ForwardModelRefusal.InvertedGravity, ref refusal);

            // Airborne with a live flight resource replays the reviewed flight
            // model instead of plain gravity. That is a wing holding the player
            // up or a rocket batch firing; both are branches of the same model,
            // and a rocket batch skips gravity exactly as a powered wing does.
            // The jump control is not refused there because that model owns the
            // impulse too: refusing it would starve the wing-powered branch,
            // which is most of a strong-wing fight, and would predict a fall
            // where the engine applied thrust.
            // Having wings is not the same as having wing fuel. The engine
            // still glides when the wing charge is spent, capping the descent
            // at a third of the maximum fall speed, and only the powered part
            // of the flight needs charge. Gating this on the remaining charge
            // therefore sent every exhausted-wing tick down the ordinary
            // branch, where the descent caps at the full maximum fall speed.
            // The trace measures both caps side by side: with the jump held and
            // down released the engine sits at three and a third, which is ten
            // and one hundredth over three, while the model sat at ten and one
            // hundredth. The charge gate belongs inside the flight step, which
            // already tests it before applying thrust.
            var winged = !frame.Grounded &&
                (frame.Flight.WingTimeMax > 0f || frame.RocketBoots);
            if (!winged && (controls.Jump || frame.JustJumped) &&
                !frame.Jump.Known)
                return Refuse(ForwardModelRefusal.Jumping, ref refusal);

            // Horizontal first: native acceleration reads the start-of-tick
            // velocity, which is still intact here.
            //
            // The dash is a branch and not a modifier. Native order is: copy the
            // input into the trigger set, decide whether a dash starts, and only
            // then run dash movement. A start replaces velocityX with the
            // certified speed outright, so running ordinary acceleration as well
            // would double-count the same velocity; and a continuation owns the
            // horizontal step for its whole decay, which is why neither path
            // falls through to the ordinary model.
            float displacement;
            // Horizontal speed on the way in and on the way out, across every path.
            // The advance records the fastest speed that enters it, but the dash
            // assigns its velocity directly and never calls the advance, so that
            // measurement cannot see whether the model ever carries the fourteen and
            // a half the engine's dash produces. These do, and the path counts say
            // which route the fast frames take.
            CountAdvanceCalls++;
            var entrySpeed = Math.Abs(frame.Velocity.X);
            if (entrySpeed > MaxEntrySpeed) MaxEntrySpeed = entrySpeed;

            float nextVelocityX;
            var dashStarted = false;
            var dash = frame.Dash;
            // A frame a rollout built carries no native dash state, only the
            // reviewed identity and the readiness flag above. Declared readiness
            // is what turns a request into the impulse: the reviewed start speed,
            // then the reviewed decay, then the cooldown. Ready false means the
            // loadout's dash is not available on this tick -- a cooldown, or no
            // reviewed dash at all -- and the engine applies no impulse for a
            // request then, so the ordinary step owns the tick. Ready true with
            // no reviewed identity, or with no direction to dash in, is a request
            // the frame cannot answer, and it is refused here rather than
            // dropped: a dropped request is not a prediction, it is the ordinary
            // tick with a dash bit set, which is what made every dash sweep over
            // a rollout return one set of figures.
            if (!dash.Known && !frame.Dashing && controls.Dash && frame.DashReady)
            {
                EyeShieldDashState ready;
                if (!EyeShieldDashMotion.TryCreateFrameReadyState(frame.DashIdentity,
                        controls.Direction, frame.Velocity.X, frame.Velocity.Y,
                        frame.AccRunSpeed, frame.MaxRunSpeed, out ready))
                    return Refuse(ForwardModelRefusal.Dashing, ref refusal);
                dash = ready;
            }
            dash.ControlLeft = controls.Left;
            dash.ControlRight = controls.Right;
            // The dash decelerates by the engine's ordinary horizontal amounts,
            // so the two fields that produce them travel with the state.
            dash.RunSlowdown = frame.RunSlowdown;
            dash.RunAcceleration = frame.RunAcceleration;
            dash.Grounded = frame.Grounded;
            EyeShieldDashMotion.TryApplyNativeInputCopy(ref dash, controls.Dash);
            EyeShieldDashCandidate started;
            if (EyeShieldDashMotion.TryCreateDedicatedCandidate(in dash,
                out started))
            {
                dashStarted = true;
                // The whole candidate state is adopted, not just its speed. The
                // candidate is what carries DashDelay of minus one, EocDash of
                // fifteen and the reset counter, and taking only the velocity
                // left the frame holding its pre-start dash state. Dashing is
                // then false on the very tick the dash began, so the following
                // ticks take the ordinary branch and the dash never continues:
                // the model held thirteen and nine tenths for the whole dash
                // while the engine decayed from fourteen and a half through the
                // measured curve.
                dash = started.AfterDashMovement;
                nextVelocityX = dash.VelocityX;
            }
            else if (frame.Dashing)
            {
                var dashPhase = EyeShieldDashMotion.TryAdvanceCollisionFreeTick(
                    ref dash);
                if (dashPhase == EyeShieldDashPhase.Unsupported)
                    return Refuse(ForwardModelRefusal.Dashing, ref refusal);
                nextVelocityX = dash.VelocityX;
            }
            else
            {
                // The cooldown has to tick down here as well. Only the dashing
                // branch advanced the dash state, and that branch is taken when
                // the delay is negative -- which is the dash itself, not the
                // cooldown after it. So once the model entered a cooldown it stayed
                // in it forever: the trace shows the model holding a delay of
                // thirty through frames where the engine was ready at zero, and
                // that is why the dash on frame seven hundred and thirty-three
                // never started. The candidate is created only from a ready state,
                // so a model stuck in cooldown cannot dash at all.
                //
                // The cooldown path leaves the horizontal velocity alone, so the
                // ordinary step still owns the speed on these frames.
                if (dash.DashDelay > 0)
                    EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref dash);
                nextVelocityX = HorizontalMotion.Advance(ToSnapshot(frame),
                    frame.Velocity.X, controls.Direction, frame.Grounded, 1,
                    out displacement);
            }
            next.Dash = dash;
            var exitSpeed = Math.Abs(nextVelocityX);
            if (exitSpeed > MaxExitSpeed) MaxExitSpeed = exitSpeed;
            if (dashStarted) CountDashStart++;
            else if (frame.Dashing) CountDashContinue++;
            else CountOrdinary++;

            var nextVelocityY = frame.Velocity.Y;
            if (winged)
            {
                // Native order: the jump impulse first, then the wing and rocket
                // resources. Calling the second without the first would leave the
                // jump charge untouched and mis-select the glide branch.
                var jump = frame.Jump;
                var flight = frame.Flight;
                flight.JustJumped = frame.JustJumped;
                if (!jump.Known || !flight.Known)
                    return Refuse(ForwardModelRefusal.Wings, ref refusal);
                FlightMotion.ApplyJump(ref flight, ref jump, ref nextVelocityY,
                    controls.Jump);
                var flightPhase = FlightMotion.ApplyAfterJump(ref flight,
                    in jump, ref nextVelocityY, controls.Jump, controls.Up,
                    controls.Down, frame.Gravity, frame.MaxFallSpeed);
                if (flightPhase == FlightPhase.Unsupported)
                    return Refuse(ForwardModelRefusal.Wings, ref refusal);
                next.Jump = jump;
                next.Flight = flight;
                next.WingTime = flight.WingTime;
            }
            else
            {
                // The ordinary and cloud jump, in native order: refresh the
                // charge, resolve the input, then the impulse, then gravity.
                //
                // This branch used to refuse the jump control outright, on the
                // grounds that the observation carried no multi-jump charge
                // state. It carries one now, and refusing meant the enumerator
                // could not jump from the ground at all: four of its twelve
                // actions were dead on every grounded tick, and ninety-seven per
                // cent of its refusals were this. A search space that cannot
                // express a jump is the answer deleted from the space, which is
                // the failure this project has already recorded twice.
                var jump = frame.Jump;
                if (!jump.Known)
                    return Refuse(ForwardModelRefusal.Jumping, ref refusal);
                JumpMotion.RefreshBeforeMovement(ref jump, frame.Velocity.Y);
                var resolved = JumpMotion.ResolveControl(controls.Jump,
                    controls.Jump ? JumpAction.Hold : JumpAction.Release,
                    in jump, frame.Grounded, false);
                JumpMotion.ApplyJump(ref jump, ref nextVelocityY, resolved,
                    frame.GravityDirection < 0);
                next.Jump = jump;

                var phase = JumpMotion.ApplyGravityChecked(ref nextVelocityY,
                    frame.Gravity, frame.MaxFallSpeed, false, false, controls.Up,
                    controls.Down);
                if (phase == GravityPhase.Unsupported)
                    return Refuse(ForwardModelRefusal.InvalidInput, ref refusal);
                // Native restores the wing and rocket charges on the ground, in
                // the same branch that would otherwise spend them. A rollout that
                // walks off an edge and then tries to fly would start with an
                // empty wing without this; a replay never noticed because it
                // re-reads the charge from the trace every tick.
                if (frame.Grounded && frame.Flight.Known)
                {
                    var flight = frame.Flight;
                    // The test is on the start-of-tick velocity, not the one
                    // this tick is about to have. Native restores the charges
                    // before gravity is applied, so testing the post-gravity
                    // value never fires: a standing player has just been given
                    // 0.4 downward, which is not zero, and the wing is then
                    // never refuelled. The search found this by refusing on
                    // ninety-seven per cent of its expansions.
                    if (frame.Velocity.Y == 0f && frame.Jump.ReleaseReady ||
                        frame.Jump.AutoJump && frame.JustJumped)
                        flight.WingTime = flight.WingTimeMax;
                    if (frame.Velocity.Y == 0f ||
                        frame.Jump.AutoJump && frame.JustJumped)
                        flight.RocketTime = flight.RocketTimeMax;
                    next.Flight = flight;
                    next.WingTime = flight.WingTime;
                }
            }

            var nextX = frame.Position.X + nextVelocityX;
            var nextY = frame.Position.Y + nextVelocityY;

            // Ground contact, in the same form the verified native vertical
            // fixture uses: the clamp applies only while falling, and the
            // reported velocity becomes the correction the clamp produced.
            if (nextY + frame.Height >= frame.FloorY && nextVelocityY >= 0f)
            {
                nextY = frame.FloorY - frame.Height;
                nextVelocityY = nextY - frame.Position.Y;
            }

            next.Position = new Vec2(nextX, nextY);
            next.Velocity = new Vec2(nextVelocityX, nextVelocityY);
            next.Tick = frame.Tick + 1;
            // The engine advances this counter every local-player update and
            // caps it at three hundred. The forward model never did, so the
            // value stayed frozen at whatever the starting row held for the
            // whole rollout, which measurement shows directly: at one engine
            // tick the engine reads twenty-four while the model still reads the
            // cap of three hundred. A dash start resets it to zero in
            // TryCreateDedicatedCandidate, and that reset is the only change the
            // model ever produced. The order matches the planner, which resets
            // on a start and advances otherwise.
            if (!dashStarted)
            {
                next.Dash.TimeSinceLastDashStarted = Math.Min(300,
                    next.Dash.TimeSinceLastDashStarted + 1);
            }
            next.Grounded = Math.Abs(nextY + frame.Height - frame.FloorY) < .5f;
            next.Dashing = next.Dash.DashDelay < 0;
            next.JustJumped = next.Flight.JustJumped;
            return true;
        }

        private static bool Refuse(ForwardModelRefusal reason,
            ref ForwardModelRefusal refusal)
        {
            refusal = reason;
            return false;
        }

        /// <summary>Names the first regime this tick cannot be replayed in.
        /// The order is fixed so the harness buckets are stable across runs.</summary>
        public static ForwardModelRefusal Classify(in PlayerMotionFrame frame)
        {
            if (!frame.IsFinite) return ForwardModelRefusal.InvalidInput;
            if (frame.Immobilized) return ForwardModelRefusal.Immobilized;
            if (frame.Mounted) return ForwardModelRefusal.Mounted;
            if (frame.Grappling) return ForwardModelRefusal.Grappling;
            if (frame.Liquid) return ForwardModelRefusal.Liquid;
            return ForwardModelRefusal.None;
        }

        // Horizontal speed on entry and exit, and which path produced it. Kept in
        // the shipped build for the same reason as the advance's counters: the
        // question of whether the model ever carries the engine's dash speed is
        // answered by a number, not by reading the branch.
        public static long CountAdvanceCalls;
        public static long CountDashStart;
        public static long CountDashContinue;
        public static long CountOrdinary;
        public static float MaxEntrySpeed;
        public static float MaxExitSpeed;

        public static void ResetSpeedCounters()
        {
            CountAdvanceCalls = 0;
            CountDashStart = 0;
            CountDashContinue = 0;
            CountOrdinary = 0;
            MaxEntrySpeed = 0f;
            MaxExitSpeed = 0f;
        }

        public static string SpeedSummary()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "calls={0} dashStart={1} dashContinue={2} ordinary={3} " +
                "maxEntry={4:F4} maxExit={5:F4}",
                CountAdvanceCalls, CountDashStart, CountDashContinue,
                CountOrdinary, MaxEntrySpeed, MaxExitSpeed);
        }

        private static PlayerSnapshot ToSnapshot(in PlayerMotionFrame frame)
        {
            return new PlayerSnapshot
            {
                Position = frame.Position,
                Velocity = frame.Velocity,
                Width = frame.Width,
                Height = frame.Height,
                Gravity = frame.Gravity,
                MaxFallSpeed = frame.MaxFallSpeed,
                MaxRunSpeed = frame.MaxRunSpeed,
                AccRunSpeed = frame.AccRunSpeed,
                RunAcceleration = frame.RunAcceleration,
                BaseRunSpeed = frame.BaseRunSpeed,
                SprintAcceleration = frame.SprintAcceleration,
                RunSlowdown = frame.RunSlowdown,
                CanSprintInAir = frame.CanSprintInAir,
            };
        }
    }
}
