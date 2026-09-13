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
        public static FormulaScriptOutput Tick(in FormulaScriptInput input)
        {
            var output = new FormulaScriptOutput { Accepted = false };
            if (!FormulaRouteCatalog.IsSupportedBoss(input.BossType) ||
                input.Route == FormulaRoute.None || input.NativeTimer < 0)
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
