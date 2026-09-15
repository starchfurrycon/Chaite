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

        public void Reset()
        {
            _initialized = false;
            _loopTicks = 0;
            _previousState = int.MinValue;
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
            _previousState = input.NativeState;
            _loopTicks++;
            if (input.NativeState == 8 || input.NativeState == 9)
            {
                // The Empress body charge is horizontal. Leave the committed
                // lane with a perpendicular vertical input and keep the
                // horizontal axis neutral so homing lances do not reverse us.
                output.Horizontal = 0;
                output.Vertical = input.PlayerBelowBoss ? -1 : 1;
                output.Jump = output.Vertical < 0;
                output.Phase = "empress-flight-dash-perpendicular";
            }
            else if (input.NativeState == 6)
            {
                output.Horizontal = 0;
                output.Vertical = input.PlayerBelowBoss ? -1 : 1;
                output.Jump = output.Vertical < 0;
                output.Phase = "empress-flight-sun-dance-pivot";
            }
            else
            {
                OrbitQuadrant(_loopTicks, _loopDirection, out var horizontal,
                    out var vertical);
                output.Horizontal = horizontal;
                output.Vertical = vertical;
                output.Jump = vertical < 0;
                output.Phase = "empress-flight-" + Phase(in input);
            }
            return output;
        }

        private static void OrbitQuadrant(int ticks, int loop,
            out int horizontal, out int vertical)
        {
            switch ((ticks / 24) & 3)
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
