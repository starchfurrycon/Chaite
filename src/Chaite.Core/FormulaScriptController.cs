namespace Chaite.Core
{
    public struct FormulaScriptInput
    {
        public int BossType;
        public FormulaRoute Route;
        public int NativeState;
        public int NativeTimer;
        public int NativeSequence;
        public bool PlayerBelowBoss;
        public bool PlayerRightOfBoss;
    }

    public struct FormulaScriptOutput
    {
        public bool Accepted;
        public int Horizontal;
        public int Vertical;
        public bool Jump;
        public bool Dash;
        public bool Fire;
        public string Phase;
    }

    /// <summary>
    /// Deterministic formula-script layer. It never scores candidates, scans
    /// threats, or changes route; the native state selects one fixed branch.
    /// </summary>
    public static class FormulaScriptController
    {
        public static bool TryReadInput(in TargetSnapshot target,
            FormulaRoute route, PlayerSnapshot player,
            out FormulaScriptInput input)
        {
            input = default(FormulaScriptInput);
            if (player == null || !FormulaRouteCatalog.BelongsToBoss(route, target.Type) ||
                !target.Ai0Known || !target.Ai2Known ||
                (target.Type == 370 ? !target.Ai3Known : !target.Ai1Known))
                return false;
            var timer = target.Type == 370 ? target.Ai2 : target.Ai1;
            var sequence = target.Type == 370 ? target.Ai3 : target.Ai2;
            if (!Integer(target.Ai0, -1, 13) || !Integer(timer, 0, int.MaxValue) ||
                !Integer(sequence, 0, int.MaxValue)) return false;
            input = new FormulaScriptInput
            {
                BossType = target.Type, Route = route,
                NativeState = (int)target.Ai0, NativeTimer = (int)timer,
                NativeSequence = (int)sequence,
                PlayerBelowBoss = player.Center.Y >= target.Center.Y,
                PlayerRightOfBoss = player.Center.X >= target.Center.X
            };
            return true;
        }

        private static bool Integer(float value, int min, int max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) &&
            (double)value >= min && (double)value <= max &&
            value == System.Math.Floor(value);

        public static FormulaScriptOutput Tick(in FormulaScriptInput input)
        {
            var output = new FormulaScriptOutput { Accepted = false };
            if (!FormulaRouteCatalog.BelongsToBoss(input.Route, input.BossType) ||
                input.NativeTimer < 0 || input.NativeSequence < 0 ||
                (input.BossType == 370 ? input.NativeState < -1 || input.NativeState > 12 :
                    input.NativeState < 0 || input.NativeState > 12 || input.NativeState == 3))
                return output;
            output.Accepted = true;
            output.Fire = true;
            if (input.BossType == 370)
                Fishron(in input, ref output);
            else
                Empress(in input, ref output);
            return output;
        }

        private static void Fishron(in FormulaScriptInput input,
            ref FormulaScriptOutput output)
        {
            output.Phase = "fishron-state-" + input.NativeState;
            switch (input.NativeState)
            {
                case 1: case 6: case 11:
                    // Leave the charge line with the perpendicular axis. The
                    // fixed route keeps this side until the native state ends.
                    output.Horizontal = input.PlayerBelowBoss ? 0 :
                        (input.PlayerRightOfBoss ? 1 : -1);
                    output.Vertical = input.PlayerBelowBoss ? 1 : -1;
                    output.Dash = input.Route == FormulaRoute.FishronFairyWingsDash ||
                        input.Route == FormulaRoute.FishronStrongWingsDash;
                    output.Jump = true;
                    break;
                case 2: case 3: case 7: case 8:
                    output.Horizontal = input.PlayerRightOfBoss ? -1 : 1;
                    output.Vertical = 1;
                    output.Jump = true;
                    break;
                default:
                    output.Horizontal = input.PlayerRightOfBoss ? -1 : 1;
                    output.Vertical = input.PlayerBelowBoss ? -1 : 1;
                    output.Jump = true;
                    break;
            }
        }

        private static void Empress(in FormulaScriptInput input,
            ref FormulaScriptOutput output)
        {
            output.Phase = "empress-state-" + input.NativeState;
            switch (input.NativeState)
            {
                case 8: case 9:
                    output.Horizontal = input.PlayerRightOfBoss ? -1 : 1;
                    output.Vertical = input.PlayerBelowBoss ? 1 : -1;
                    output.Dash = input.Route == FormulaRoute.EmpressStrongWingsDash;
                    output.Jump = true;
                    break;
                case 6:
                    output.Horizontal = 0;
                    output.Vertical = input.PlayerBelowBoss ? 1 : -1;
                    output.Jump = true;
                    break;
                default:
                    output.Horizontal = input.PlayerRightOfBoss ? 1 : -1;
                    output.Vertical = input.PlayerBelowBoss ? -1 : 1;
                    output.Jump = true;
                    break;
            }
        }
    }
}
