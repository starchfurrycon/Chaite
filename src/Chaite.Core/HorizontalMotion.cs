using System;

namespace Chaite.Core
{
    /// <summary>
    /// Allocation-free ordinary 1.4.5.8 horizontal movement approximation. Native
    /// base/sprint thresholds and acceleration are separate; tile, cart, portal,
    /// special mount, dash and grappling physics are not replayed here.
    /// </summary>
    public static class HorizontalMotion
    {
        public static float Advance(PlayerSnapshot player, float velocity, int direction,
            bool grounded, int ticks, out float displacement, float mountedSpeed = 0f)
        {
            displacement = 0f;
            if (ticks <= 0) return velocity;
            if (player.BaseRunSpeed <= 0f)
            {
                // Keep old adapter snapshots bit-compatible, including their
                // endpoint-step integration; production supplies the native profile.
                var target = direction * Math.Max(2f, Math.Max(player.MaxRunSpeed, mountedSpeed));
                velocity = MoveTowards(velocity, target, Math.Max(.08f, player.RunAcceleration) * ticks);
                displacement = velocity * ticks;
                return velocity;
            }
            var baseSpeed = player.BaseRunSpeed;
            // The acceleration-run speed is the boundary the trace draws. Every
            // opposing frame at or above it was bled and then scaled; every one
            // below it stepped by the bleed alone, with no scaling and no floor.
            var accRunSpeed = player.AccRunSpeed;
            // Native fields already include the currently active mount and its
            // modifiers. Raw mount stats must not undo a live slowing effect.
            var topSpeed = Math.Max(baseSpeed, player.MaxRunSpeed);
            var acceleration = Math.Max(0f, player.RunAcceleration);
            var sprintAcceleration = Math.Max(0f, player.SprintAcceleration);
            var slowdown = Math.Max(0f, player.RunSlowdown);
            var drag = grounded ? slowdown : slowdown * .5f;
            for (var tick = 0; tick < ticks; tick++)
            {
                CountCalls++;
                var incoming = Math.Abs(velocity);
                if (incoming > MaxIncomingSpeed) MaxIncomingSpeed = incoming;
                if (topSpeed > MaxTopSpeed) MaxTopSpeed = topSpeed;
                LastIncoming = incoming;
                LastAccRunSpeed = accRunSpeed;
                LastTopSpeed = topSpeed;
                LastBaseSpeed = baseSpeed;
                LastDirection = direction;
                if (direction != 0)
                {
                    var forward = velocity * direction;
                    if (forward < baseSpeed)
                    {
                        CountInputBrake++;
                        if (forward < 0f && accRunSpeed > 0f)
                        {
                            LastBranch = "brake-oppose";
                            // The input opposes the motion, so the engine never
                            // accelerates: it bleeds, and above the acceleration-run
                            // speed it scales and floors. The open-loop comparison
                            // shows the model exact for nine frames and first wrong
                            // here, with the engine at eight and the model at the
                            // bled and scaled seven point three seven seven six.
                            CountInputOppose++;
                            velocity = EyeShieldDashMotion.ApplyOpposingBrake(
                                velocity, EyeShieldDashMotion.OpposingSpeedBleed,
                                accRunSpeed);
                        }
                        else
                        {
                            LastBranch = forward < 0f ? "brake-oppose-nospeed"
                                : "brake-reverse";
                            // Native reversal brakes first, then accelerates. Do not
                            // replace a measured debuffed acceleration with an .08
                            // floor.
                            if (forward < -slowdown) forward += slowdown;
                            forward += acceleration;
                            velocity = forward * direction;
                        }
                    }
                    else if (forward < topSpeed && sprintAcceleration > 0f)
                    {
                        CountInputSprint++;
                        LastBranch = "sprint";
                        // Boots use .2*runAcceleration (wings add another .2).
                        // Without wings/flying mount, airborne input maintains the
                        // already-earned speed but cannot build sprint speed.
                        if (grounded || player.CanSprintInAir)
                            velocity = (forward + sprintAcceleration) * direction;
                    }
                    else if (forward > topSpeed)
                    {
                        CountInputOver++;
                        LastBranch = "over";
                        velocity = AboveTopSpeed(velocity, true,
                            accRunSpeed > 0f ? accRunSpeed : topSpeed);
                    }
                    else
                    {
                        CountInputHold++;
                        LastBranch = "hold";
                        velocity = MoveTowards(velocity, 0f, drag);
                    }
                }
                else if (Math.Abs(velocity) > topSpeed)
                {
                    CountCoastOver++;
                    LastBranch = "coast-over";
                    velocity = AboveTopSpeed(velocity, false,
                        accRunSpeed > 0f ? accRunSpeed : topSpeed);
                }
                else
                {
                    CountCoastDecay++;
                    LastBranch = "coast-decay";
                    velocity = MoveTowards(velocity, 0f, drag);
                }
                // Integrate each bounded sub-tick. Applying the final velocity to
                // the entire 3-tick sample overstates travel while accelerating.
                displacement += velocity;
            }
            return velocity;
        }

        /// <summary>
        /// Native movement above the acceleration-run speed, which the engine does
        /// not treat as a ceiling. The model used to decay linearly past it and so
        /// sat near five while the engine held eight to fourteen.
        ///
        /// The rule above the speed is the one already measured for the dash, and
        /// it was confirmed against nine consecutive over-speed frames of the dense
        /// trace rather than assumed: remove four five one two ten-thousandths when
        /// the input opposes the motion and one tenth otherwise, scale by nine
        /// eight five thousandths above twelve and by ninety-four hundredths at or
        /// below, and floor the result at the acceleration-run speed. Every one of
        /// those frames reproduced to four decimals.
        ///
        /// This is currently unreachable, and saying so matters more than shipping
        /// it quietly. The audit before and after adding it reported the same
        /// per-regime horizontal errors to two decimals, which by the standing rule
        /// means the branch does not execute. The reason is upstream: the
        /// acceleration branch above only runs while the speed is below the top
        /// speed, so the model never carries speed above it and the condition here
        /// never holds. What is missing is not how to decay above the speed but the
        /// dash's residual velocity being carried out of the dash into ordinary
        /// movement, where the engine holds fourteen and the model holds five.
        /// </summary>
        private static float AboveTopSpeed(float velocity, bool opposing,
            float accRunSpeed)
        {
            var bleed = opposing
                ? EyeShieldDashMotion.OpposingSpeedBleed
                : EyeShieldDashMotion.DashSpeedBleed;
            var next = EyeShieldDashMotion.ApplyDashBleed(velocity, bleed);
            // The floor is real and this is where it belongs, but the speed it
            // clamps to was wrong. It clamped to the top speed, six, when the trace
            // floors at the acceleration-run speed, eight. The open-loop comparison
            // shows it exactly: the model is correct for nine frames and first wrong
            // on the frame the engine steps from eight point two nine nine seven to
            // eight, where the model steps to seven point three seven seven six --
            // the bled and scaled value with nothing raising it back. The frame after
            // that, from exactly eight, the engine steps to seven point five four
            // eight eight, which is the bleed alone, so the floor applies strictly
            // above the acceleration-run speed and not at it.
            var magnitude = Math.Abs(next);
            if (magnitude < accRunSpeed) magnitude = accRunSpeed;
            return next < 0f ? -magnitude : magnitude;
        }

        private static float MoveTowards(float current, float target, float delta)
        {
            if (Math.Abs(target - current) <= delta) return target;
            return current + Math.Sign(target - current) * delta;
        }

        // Which case actually runs, counted rather than argued about. Two repairs to
        // the ordering of these cases were reasoned from the engine's own frames,
        // built, and measured as no-ops, which says the case they touched is not the
        // one carrying the error. Counting is the way to find out which one is: the
        // branch totals say where the work happens, and the incoming speed says
        // whether the model ever reaches the speeds the engine's frames show. Left in
        // the shipped build because it is a handful of increments and the alternative
        // is another round of reasoning about which branch is taken.
        public static long CountInputBrake;
        public static long CountInputSprint;
        public static long CountInputOver;
        public static long CountInputHold;
        /// <summary>Brake frames where the input actually opposes the motion. These
        /// are the ones that bleed instead of accelerating.</summary>
        /// <summary>Brake frames where the input actually opposes the motion. These
        /// are the ones that bleed instead of accelerating.</summary>
        public static long CountInputOppose;
        /// <summary>Which case ran on the most recent call, so a wrong value can be
        /// traced to its branch in one run instead of by inference.</summary>
        public static string LastBranch = "";
        public static float LastIncoming;
        public static float LastAccRunSpeed;
        public static float LastTopSpeed;
        public static float LastBaseSpeed;
        public static int LastDirection;
        public static long CountCoastOver;
        public static long CountCoastDecay;
        public static long CountCalls;
        public static float MaxIncomingSpeed;
        public static float MaxTopSpeed;

        public static void ResetBranchCounters()
        {
            CountInputBrake = 0;
            CountInputSprint = 0;
            CountInputOver = 0;
            CountInputHold = 0;
            CountInputOppose = 0;
            CountCoastOver = 0;
            CountCoastDecay = 0;
            CountCalls = 0;
            MaxIncomingSpeed = 0f;
            MaxTopSpeed = 0f;
        }

        public static string BranchSummary()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "calls={0} brake={1} oppose={2} sprint={3} over={4} hold={5} " +
                "coastOver={6} coastDecay={7} maxIncoming={8:F4} maxTop={9:F4}",
                CountCalls, CountInputBrake, CountInputOppose, CountInputSprint,
                CountInputOver, CountInputHold, CountCoastOver, CountCoastDecay,
                MaxIncomingSpeed, MaxTopSpeed);
        }
    }
}
