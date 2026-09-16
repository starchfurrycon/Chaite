namespace Chaite.Core
{
    /// <summary>
    /// Fixed Fishron-wing circuit for Empress of Light. AI_120 selects the
    /// authored branch; the script keeps one loop direction and emits at most
    /// one horizontal dash edge in an inter-attack reposition window.
    /// </summary>
    public sealed class EmpressWingScript
    {
        private bool _initialized;
        private bool _dashIssued;
        private int _loopDirection;
        private int _loopTicks;
        private int _dashVertical;
        private int _previousState = int.MinValue;
        private int _previousSequence = int.MinValue;

        public void Reset()
        {
            _initialized = false;
            _dashIssued = false;
            _previousState = int.MinValue;
            _previousSequence = int.MinValue;
            _loopTicks = 0;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena, MobilitySnapshot mobility,
            DifficultySnapshot difficulty)
        {
            var output = new FormulaScriptOutput();
            if (input.BossType != 636 ||
                input.Route != FormulaRoute.EmpressStrongWingsDash ||
                player == null || arena == null || mobility == null ||
                difficulty == null ||
                mobility.MountActive || mobility.Grappling ||
                mobility.GravityInverted || input.NativeState < 0 ||
                input.NativeState > 13 || input.NativeState == 3)
                return output;
            if (!EmpressFormulaStateContract.IsValid(in input, difficulty))
                return output;

            if (!_initialized)
            {
                _initialized = true;
                _loopDirection = arena.ClearanceRight >= arena.ClearanceLeft
                    ? 1 : -1;
            }

            var attackEdge = input.NativeState != _previousState ||
                input.NativeSequence != _previousSequence;
            if (input.NativeState == 1 &&
                (_previousState == 8 || _previousState == 9))
                _loopDirection *= -1;
            if (attackEdge && (input.NativeState == 8 ||
                    input.NativeState == 9))
                _dashVertical = input.PlayerBelowBoss ? -1 : 1;
            if (attackEdge) _dashIssued = false;
            _previousState = input.NativeState;
            _previousSequence = input.NativeSequence;
            _loopTicks++;

            output.Accepted = true;
            output.Fire = true;
            switch (input.NativeState)
            {
                case 8:
                case 9:
                    output.Horizontal = 0;
                    output.Vertical = _dashVertical;
                    output.Phase = "empress-wing-dash-perpendicular";
                    break;
                case 6:
                    Tangent(in input, _loopDirection, out output.Horizontal,
                        out output.Vertical);
                    output.Phase = "empress-wing-sun-dance-pivot";
                    break;
                default:
                    OrbitQuadrant(_loopTicks, _loopDirection,
                        out output.Horizontal, out output.Vertical);
                    output.Phase = "empress-wing-state-" +
                        input.NativeState;
                    break;
            }

            // Horizontal burst is useful before the next authored attack, not
            // during the horizontal body charge where vertical separation is
            // the fixed response. The edge is single-shot for this state.
            if (input.NativeState == 1 && input.NativeTimer <= 8 &&
                !_dashIssued && mobility.CanDash && mobility.DashReady)
            {
                output.Horizontal = boss.Center.X >= player.Center.X ? -1 : 1;
                output.Dash = true;
                _dashIssued = true;
                output.Phase = "empress-wing-reposition-dash-edge";
            }
            output.Jump = output.Vertical < 0;
            DecideMovement(in input, player, in boss, arena, output);
            return output;
        }

        /// <summary>
        /// Applies the trained residual to the scripted decision computed just
        /// above, including the single-shot reposition dash edge. The script is
        /// the base and the network may only correct it, so zero weights
        /// reproduce the fixed circuit exactly and the search starts from that
        /// circuit's measured behaviour rather than from a random walk.
        /// Admission and the state bookkeeping are not reachable from here. A
        /// policy exception is deliberately not caught: a configured-but-broken
        /// policy must fail loudly rather than silently revert to the fixed
        /// machine.
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
            output.Phase = "empress-wing-learned";
        }

        private static void OrbitQuadrant(int timer, int loop,
            out int horizontal, out int vertical)
        {
            switch ((timer / 60) & 3)
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

        private static void Tangent(in FormulaScriptInput input, int loop,
            out int horizontal, out int vertical)
        {
            horizontal = input.PlayerBelowBoss ? -loop : loop;
            vertical = input.PlayerRightOfBoss ? -loop : loop;
        }
    }
}
