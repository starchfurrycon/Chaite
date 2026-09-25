using System;
using System.Collections.Generic;
using System.Globalization;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// The offline forward model's Shield of Cthulhu dash.
    ///
    /// The model could replay a dash a trace had sampled, but it could not start
    /// one from a frame a rollout built itself: such a frame carries a default
    /// dash state, and there was nowhere on it to say that a reviewed dash is
    /// equipped and off cooldown. A dash request was therefore read and dropped,
    /// which is why a sweep over dash timing in the offline lab returned
    /// byte-identical figures for every dash setting. This regression pins the
    /// capability on the two frame-level facts that close that gap, and pins what
    /// a frame that does not carry them still does.
    /// </summary>
    internal static partial class Program
    {
        private static void FrameDashRequestReachesCertifiedSpeedThenCoolsDown()
        {
            var start = DashFloorFrame();
            start.DashIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            start.DashReady = true;

            // The schedule a controller would use, and the one that keeps the
            // cooldown measurable: a start edge on the first tick, no request
            // while the impulse is running, a request on every cooldown frame,
            // and a release on the last one so the second start has the release
            // edge the engine's sticky releaseDash requires.
            var velocityX = new List<float>();
            var dashing = new List<bool>();
            var dashDelay = new List<int>();
            var eocDash = new List<int>();
            var frame = start;
            for (var tick = 1; tick <= 70; tick++)
            {
                bool request;
                if (tick == 1) request = true;
                else if (frame.Dashing) request = false;
                else if (frame.Dash.DashDelay > 1) request = true;
                else if (frame.Dash.DashDelay == 1) request = false;
                else request = true;

                PlayerMotionFrame next;
                ForwardModelRefusal refusal;
                True(PlayerForwardModel.TryAdvance(in frame,
                    new PlayerControlFrame { Right = true, Dash = request },
                    out next, out refusal),
                    "the dash rollout was refused at tick " + tick + " with " + refusal);
                frame = next;
                velocityX.Add(frame.Velocity.X);
                dashing.Add(frame.Dashing);
                dashDelay.Add(frame.Dash.DashDelay);
                eocDash.Add(frame.Dash.EocDash);
            }

            var firstDashTicks = 0;
            while (dashing[firstDashTicks]) firstDashTicks++;
            var cooldownFrames = 0;
            for (var tick = firstDashTicks; dashDelay[tick] > 0; tick++) cooldownFrames++;
            var secondStart = firstDashTicks + cooldownFrames;
            var secondDashTicks = 0;
            while (dashing[secondStart + 1 + secondDashTicks]) secondDashTicks++;

            var shape = new System.Text.StringBuilder();
            for (var index = 0; index <= secondStart + 1; index++)
            {
                if (index > 0) shape.Append(',');
                shape.Append(velocityX[index].ToString("F4", CultureInfo.InvariantCulture));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "DASH FRAME startVX={0:F4} firstDashTicks={1} lastActiveVX={2:F4} " +
                "cooldownEntryVX={3:F4} cooldownFrames={4} secondStartVX={5:F4} " +
                "secondDashTicks={6}",
                velocityX[0], firstDashTicks, velocityX[firstDashTicks - 1],
                velocityX[firstDashTicks], cooldownFrames,
                velocityX[secondStart + 1], secondDashTicks));
            Console.WriteLine("DASH FRAME velocities=" + shape);

            // The impulse: the certified start speed on the request, then the
            // reviewed decay. The form is the engine measurement the dash model
            // already carries -- remove a tenth of a pixel, then scale by nine
            // eight five thousandths above twelve -- so the second tick is
            // (14.5 - .1) * .985 and not another 14.5.
            Equal(14.5f, velocityX[0]);
            Equal((14.5f - .1f) * .985f, velocityX[1]);

            // The fifteen-tick contact window the decompile opens on a start,
            // which is a fact about eocDash and not about the impulse: eocDash
            // stays fifteen for as long as the dash is running.
            for (var tick = 0; tick < 15; tick++)
            {
                True(dashing[tick], "the dash left its active phase at tick " + (tick + 1));
                Equal(15, eocDash[tick]);
            }

            // The impulse ends when the speed has bled to the run speed, which at
            // this loadout's six and three quarters is eighteen ticks from
            // fourteen and a half down through six point five nine. That last
            // frame is already under the run speed -- the test that ends the dash
            // reads the speed after the bleed -- and the frame that ends it is the
            // one that clamps.
            Equal(18, firstDashTicks);
            True(velocityX[firstDashTicks - 1] > 6.5f &&
                velocityX[firstDashTicks - 1] < 6.75f,
                "the dash must run its speed down to the run speed, not stop above it");

            // The cooldown: entered by clamping the speed to the run speed, then
            // thirty frames that are not the dash at all. The frame after it is
            // ready again, and the same request that did nothing on those thirty
            // frames starts a second dash at the certified speed for exactly as
            // long.
            Equal(30, dashDelay[firstDashTicks]);
            False(dashing[firstDashTicks]);
            Equal(6.75f, velocityX[firstDashTicks]);
            Equal(30, cooldownFrames);
            Equal(0, dashDelay[secondStart]);
            Equal(14.5f, velocityX[secondStart + 1]);
            Equal(firstDashTicks, secondDashTicks);

            // A request during the cooldown does nothing, and "nothing" is exact:
            // the control run is the same start with no request after it, and the
            // two agree tick for tick through the whole cooldown. Comparing
            // against the run rather than against a speed bound is what makes the
            // claim checkable, because the ordinary horizontal model has a sprint
            // branch of its own and overshoots the run speed by a few hundredths
            // on some of these frames.
            var control = start;
            var controlVelocityX = new List<float>();
            var controlDashDelay = new List<int>();
            for (var tick = 1; tick <= firstDashTicks + cooldownFrames; tick++)
            {
                PlayerMotionFrame next;
                ForwardModelRefusal refusal;
                True(PlayerForwardModel.TryAdvance(in control,
                    new PlayerControlFrame { Right = true, Dash = tick == 1 },
                    out next, out refusal));
                control = next;
                controlVelocityX.Add(control.Velocity.X);
                controlDashDelay.Add(control.Dash.DashDelay);
            }
            for (var tick = 0; tick < controlVelocityX.Count; tick++)
            {
                Equal(controlVelocityX[tick], velocityX[tick]);
                Equal(controlDashDelay[tick], dashDelay[tick]);
            }

            // What the model did before the frame-level facts existed, and what it
            // must still do for a frame that carries neither: an undeclared frame
            // is not dash-capable, so the request falls through to the ordinary
            // step. Measured, this is the figure every dash sweep over the offline
            // lab produced -- the first acceleration step of nought point nought
            // eight from rest.
            var legacy = DashFloorFrame();
            var legacyControls = new PlayerControlFrame { Right = true, Dash = true };
            PlayerMotionFrame legacyNext;
            ForwardModelRefusal legacyRefusal;
            True(PlayerForwardModel.TryAdvance(in legacy, in legacyControls,
                out legacyNext, out legacyRefusal));
            Equal(ForwardModelRefusal.None, legacyRefusal);
            Equal(.08f, legacyNext.Velocity.X);
            False(legacyNext.Dashing);

            // A reviewed identity on cooldown is the same answer for the same
            // reason: the engine applies no impulse, so the ordinary step owns the
            // tick rather than the request being refused.
            var cooling = DashFloorFrame();
            cooling.DashIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            PlayerMotionFrame coolingNext;
            ForwardModelRefusal coolingRefusal;
            True(PlayerForwardModel.TryAdvance(in cooling, in legacyControls,
                out coolingNext, out coolingRefusal));
            Equal(ForwardModelRefusal.None, coolingRefusal);
            Equal(.08f, coolingNext.Velocity.X);

            // Ready without a reviewed identity, and ready with no direction to
            // dash in, are both requests the frame cannot answer. They are refused
            // instead of being predicted as ordinary movement.
            var unnamed = DashFloorFrame();
            unnamed.DashReady = true;
            var unnamedRefusal = DashRefusal(in unnamed, true);
            Equal(ForwardModelRefusal.Dashing, unnamedRefusal);

            var undirected = DashFloorFrame();
            undirected.DashIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            undirected.DashReady = true;
            var undirectedRefusal = DashRefusal(in undirected, false);
            Equal(ForwardModelRefusal.Dashing, undirectedRefusal);

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "DASH FRAME undeclaredRequestVX={0:F4} unnamedIdentityRefusal={1} " +
                "undirectedRefusal={2}",
                legacyNext.Velocity.X, unnamedRefusal, undirectedRefusal));
        }

        /// <summary>Advances one tick holding the dash key and returns why the
        /// model refused it. A request the frame cannot answer must not advance:
        /// predicting it as ordinary movement is the silent drop this regression
        /// exists to catch.</summary>
        private static ForwardModelRefusal DashRefusal(in PlayerMotionFrame frame,
            bool rightHeld)
        {
            var controls = new PlayerControlFrame { Right = rightHeld, Dash = true };
            PlayerMotionFrame next;
            ForwardModelRefusal refusal;
            True(!PlayerForwardModel.TryAdvance(in frame, in controls, out next,
                out refusal), "expected a refusal, got a tick");
            return refusal;
        }

        /// <summary>The offline lab's loadout profile as a frame: the strong-wing
        /// route's run speeds and slowdown, standing on a floor. The lab builds
        /// its frame without a trace, so its dash state is default and the dash it
        /// wants to sweep has to be declared on the frame; this is that frame.</summary>
        private static PlayerMotionFrame DashFloorFrame()
        {
            var jump = new JumpSnapshot
            {
                Known = true,
                RemainingTicks = 15,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = true,
                CloudAvailable = false,
                CloudEnabled = false,
                AutoJump = false,
            };
            var flight = new FlightSnapshot
            {
                Known = true,
                WingsLogic = 45,
                RocketBoots = 0,
                WingTime = 150f,
                WingTimeMax = 150,
                RocketTime = 0,
                RocketTimeMax = 0,
                RocketDelay = 0,
                CanRocket = false,
                RocketRelease = true,
            };
            return new PlayerMotionFrame
            {
                Position = new Vec2(3000f, 6000f - 42f),
                Velocity = new Vec2(0f, 0f),
                Width = 20,
                Height = 42,
                Gravity = .4f,
                MaxFallSpeed = 10f,
                GravityDirection = 1,
                BaseRunSpeed = 3f,
                MaxRunSpeed = 6.75f,
                AccRunSpeed = 6f,
                RunAcceleration = .08f,
                SprintAcceleration = .08f,
                RunSlowdown = .2f,
                CanSprintInAir = true,
                Grounded = true,
                FloorY = 6000f,
                WingTime = 150f,
                Jump = jump,
                Flight = flight,
            };
        }
    }
}
