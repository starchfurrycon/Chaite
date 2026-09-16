using System;

namespace Chaite.Core
{
    /// <summary>
    /// Fixed continuous-flight mount circuit for Empress of Light. It is used
    /// for both reviewed mount routes (Witch's Broom 23 and rain-only Shrimpy
    /// Truffle 12); the caller owns a separate instance per route so each
    /// route has independent state. The input loop is selected by AI_120's
    /// attack state, not by threat scoring.
    /// </summary>
    public sealed class EmpressFlightScript
    {
        public const int WitchBroomMountType = 23;
        public const int ShrimpyTruffleMountType = 12;

        private bool _initialized;
        private int _loopDirection;
        private int _loopTicks;
        private int _previousState = int.MinValue;
        private int _dashVertical;

        public void Reset()
        {
            _initialized = false;
            _loopTicks = 0;
            _previousState = int.MinValue;
            _dashVertical = 0;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena, MobilitySnapshot mobility,
            DifficultySnapshot difficulty)
        {
            var output = new FormulaScriptOutput();
            if (input.BossType != 636 || player == null || arena == null ||
                mobility == null || difficulty == null ||
                input.Route != FormulaRoute.EmpressBroom &&
                input.Route != FormulaRoute.EmpressRainFishron)
                return output;
            if (!EmpressFormulaStateContract.IsValid(in input, difficulty))
                return output;

            var expectedMount = input.Route == FormulaRoute.EmpressBroom
                ? WitchBroomMountType : ShrimpyTruffleMountType;
            if (input.Route == FormulaRoute.EmpressRainFishron &&
                (!difficulty.RainKnown || !difficulty.Rain))
                return Rejected();

            if (!mobility.MountActive)
            {
                if (!mobility.SelectedMountIdentityKnown ||
                    mobility.SelectedMountType != expectedMount)
                    return Rejected();
                output.Accepted = true;
                output.Fire = false;
                output.ToggleMount = mobility.ActiveMountReleaseReady;
                output.Phase = output.ToggleMount
                    ? "empress-flight-mount-edge"
                    : "empress-flight-await-mount-release";
                return output;
            }

            if (!mobility.ActiveMountIdentityKnown ||
                mobility.ActiveMountType != expectedMount ||
                mobility.Grappling || mobility.GravityInverted)
                return Rejected();

            if (!_initialized)
            {
                _initialized = true;
                _loopDirection = arena.ClearanceRight >= arena.ClearanceLeft
                    ? 1 : -1;
                if (_loopDirection == 0) _loopDirection = 1;
            }

            output.Accepted = true;
            output.Fire = true;
            if (input.NativeState == 1 &&
                (_previousState == 8 || _previousState == 9))
                _loopDirection *= -1;
            if (input.NativeState != _previousState &&
                (input.NativeState == 8 || input.NativeState == 9))
                _dashVertical = input.PlayerBelowBoss ? -1 : 1;
            _previousState = input.NativeState;
            _loopTicks++;
            if (input.NativeState == 8 || input.NativeState == 9)
            {
                // The Empress body charge is horizontal. Leave the committed
                // lane with a perpendicular vertical input and keep the
                // horizontal axis neutral so homing lances do not reverse us.
                output.Horizontal = 0;
                output.Vertical = _dashVertical;
                output.Jump = output.Vertical < 0;
                output.Phase = "empress-flight-dash-perpendicular";
            }
            else if (input.NativeState == 6)
            {
                // Sun Dance is 58% of the damage still being taken. It lands at a
                // median of 481 px, in the 26% band, and the reason is the
                // player's bearing rather than the range: bucketing every Sun
                // Dance frame by position relative to her gives a hazard of
                // 97-100% within 150 px of her vertical axis (69 frames) against
                // 4.6-18.8% once laterally offset (429 frames). Retreating
                // straight down that axis keeps the player inside the beam
                // column, and the radial retreat below commands exactly that
                // whenever the player is directly beneath her.
                Standoff(player, in boss, _loopTicks, _loopDirection,
                    out output.Horizontal, out output.Vertical);
                var sunDanceDx = player.Center.X - boss.Center.X;
                if (Math.Abs(sunDanceDx) < SunDanceLateralClearance)
                    output.Horizontal = sunDanceDx > 0f ? 1 :
                        sunDanceDx < 0f ? -1 : _loopDirection;
                output.Jump = output.Vertical < 0;
                output.Phase = "empress-flight-sun-dance-pivot";
            }
            else if (input.NativeState == 1)
            {
                // Inter-attack reposition is 15.6% of the damage still being
                // taken, all of it from 873, and the geometry says the unsafe
                // region is below her rather than close to her:
                //
                //   below/centred <400px   257 frames   20.6%
                //   wellBelow/right 400-900 133 frames   32.3%
                //   below/right     400-900  35 frames   25.7%
                //   level/right     <400     88 frames    0.0%
                //   level/left      <400     85 frames    1.2%
                //   above/...       >900    133 frames    0.0%
                //
                // A radial retreat from below drives the player deeper into
                // that region, so the vertical component is inverted here:
                // when the player is below her the circuit climbs toward her
                // level instead. No 636 body damage was recorded in this state
                // at all, so levelling out carries no contact risk.
                Standoff(player, in boss, _loopTicks, _loopDirection,
                    out var horizontal, out var vertical);
                if (player.Center.Y - boss.Center.Y > LevelBandPixels) vertical = -1;
                output.Horizontal = horizontal;
                output.Vertical = vertical;
                output.Jump = vertical < 0;
                output.Phase = "empress-flight-inter-attack-reposition";
            }
            else
            {
                Standoff(player, in boss, _loopTicks, _loopDirection,
                    out var horizontal, out var vertical);
                output.Horizontal = horizontal;
                output.Vertical = vertical;
                output.Jump = vertical < 0;
                output.Phase = "empress-flight-" + Phase(in input);
            }
            DecideMovement(in input, player, in boss, arena, output);
            return output;
        }

        /// <summary>
        /// Applies the trained residual to the scripted decision computed just
        /// above. The script is the base and the network may only correct it,
        /// so zero weights reproduce the fixed circuit exactly and the search
        /// starts from that circuit's measured behaviour rather than from a
        /// random walk. Admission, mount handling and the state clocks are not
        /// reachable from here. A policy exception is deliberately not caught:
        /// a configured-but-broken policy must fail loudly rather than silently
        /// revert to the fixed machine.
        /// </summary>
        private static void DecideMovement(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            FormulaScriptOutput output)
        {
            var learned = LearnedPolicy.ForRoute(input.Route);
            if (learned == null) return;
            int horizontal, vertical;
            bool jump, dash;
            if (!learned.Adjust(in input, player, in boss, arena,
                    output.Horizontal, output.Vertical, output.Jump,
                    output.Dash, out horizontal, out vertical, out jump,
                    out dash))
                return;
            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = jump;
            output.Dash = dash;
            output.Phase = "empress-flight-learned";
        }

        /// <summary>Centre-to-centre distance the flight circuit opens to before it
        /// resumes the reviewed quadrant loop.
        ///
        /// Measured over twenty baseline runs, the chance that damage lands
        /// within the next half second falls monotonically with distance:
        /// 74.4% inside 150 px, 37.9% at 150-300, 26.2% at 300-500, 6.0% at
        /// 500-800 and 0.5% beyond 800 -- a factor of 150 between the closest
        /// and farthest bands. The quadrant loop as written orbits at 150-500
        /// px, which is the 26-38% band, so it is exactly the wrong shape for
        /// this fight; the same curve is what made the 360 px pivot experiment
        /// fail. The loop is therefore held back until the gap is open, and
        /// resumes once the player is outside this radius, which keeps the
        /// player from simply running off the arena.</summary>
        public const float StandoffRadius = 900f;

        /// <summary>Lateral offset from the Empress's vertical axis that the
        /// Sun Dance circuit insists on. Measured hazard during that state is
        /// 97-100% inside 150 px of the axis against 4.6-18.8% outside it, so
        /// the clearance is set above the measured boundary rather than at
        /// it.</summary>
        public const float SunDanceLateralClearance = 200f;

        /// <summary>Vertical offset below the Empress that state 1 treats as the
        /// unsafe side of the fight. Measured hazard there is 20.6-32.3% against
        /// 0.0-1.2% for the same distances taken level with her.</summary>
        public const float LevelBandPixels = 100f;

        /// <summary>Radius component small enough to be treated as zero, so an
        /// exactly axis-aligned separation still produces a usable input.</summary>
        public const float StandoffDeadZone = 24f;

        private static void Standoff(PlayerSnapshot player, in TargetSnapshot boss,
            int ticks, int loop, out int horizontal, out int vertical)
        {
            horizontal = 0;
            vertical = 0;
            var dx = player.Center.X - boss.Center.X;
            var dy = player.Center.Y - boss.Center.Y;
            if (!IsFinite(dx) || !IsFinite(dy))
            {
                vertical = -1;
                return;
            }
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance >= StandoffRadius)
            {
                OrbitQuadrant(ticks, loop, out horizontal, out vertical);
                return;
            }
            if (Math.Abs(dx) >= StandoffDeadZone) horizontal = dx > 0f ? 1 : -1;
            if (Math.Abs(dy) >= StandoffDeadZone) vertical = dy > 0f ? 1 : -1;
            if (horizontal == 0 && vertical == 0) vertical = -1;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void OrbitQuadrant(int ticks, int loop,
            out int horizontal, out int vertical)
        {
            switch ((ticks / 45) & 3)
            {
                case 1:
                    horizontal = -loop;
                    vertical = loop;
                    break;
                case 2:
                    horizontal = -loop;
                    vertical = -loop;
                    break;
                case 3:
                    horizontal = loop;
                    vertical = -loop;
                    break;
                default:
                    horizontal = loop;
                    vertical = loop;
                    break;
            }
        }

        private static string Phase(in FormulaScriptInput input)
        {
            switch (input.NativeState)
            {
                case 0: return "initial-reposition";
                case 1: return "inter-attack-reposition";
                case 2: return "prismatic-bolts";
                case 4: return "ethereal-lances";
                case 5: return "everlasting-rainbow";
                case 7: return "lance-wall";
                case 10: return "phase-transition";
                case 11: return "predictive-lances";
                case 12: return "spiral-bolts";
                case 13: return "departure";
                default: return "state-" + input.NativeState;
            }
        }

        private static FormulaScriptOutput Rejected() =>
            new FormulaScriptOutput { Accepted = false };
    }
}
