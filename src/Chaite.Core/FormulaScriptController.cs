namespace Chaite.Core
{
    public struct FormulaScriptInput
    {
        public int BossType;
        public FormulaRoute Route;
        public int NativeState;
        public int NativeTimer;
        public int NativeSequence;
        // Duke Fishron has no native form, so this is always the known zero
        // form. The field is retained because the exported policy's 40-entry
        // feature vector carries four one-hot form slots in the middle of its
        // layout (see LearnedPolicy.FillFeatures); dropping them here would
        // misalign every trained Fishron weight.
        public int NativeForm;
        public bool NativeFormKnown;
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
        public bool ToggleMount;
        public bool Fire;
        public string Phase;
    }

    /// <summary>
    /// Deterministic formula-script layer. It never scores candidates, scans
    /// threats, or changes route; the native state selects one fixed branch.
    /// Duke Fishron is the only admitted Boss.
    /// </summary>
    public static class FormulaScriptController
    {
        public static bool TryReadInput(in TargetSnapshot target,
            FormulaRoute route, PlayerSnapshot player,
            out FormulaScriptInput input)
        {
            input = default(FormulaScriptInput);
            if (player == null ||
                !FormulaRouteCatalog.BelongsToBoss(route, target.Type) ||
                !target.Ai0Known || !target.Ai2Known || !target.Ai3Known)
                return false;
            // Fishron AI_069 exposes the state in ai[0], the state clock in
            // ai[2] and the sequence index in ai[3].
            var timer = target.Ai2;
            var sequence = target.Ai3;
            if (!Integer(target.Ai0, -1, 13) || !Integer(timer, 0, int.MaxValue) ||
                !Integer(sequence, 0, int.MaxValue)) return false;
            input = new FormulaScriptInput
            {
                BossType = target.Type,
                Route = route,
                NativeState = (int)target.Ai0,
                NativeTimer = (int)timer,
                NativeSequence = (int)sequence,
                NativeForm = 0,
                NativeFormKnown = true,
                PlayerBelowBoss = player.Center.Y >= target.Center.Y,
                PlayerRightOfBoss = player.Center.X >= target.Center.X
            };
            return true;
        }

        private static bool Integer(float value, int minimum, int maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= minimum && value <= maximum &&
                value == System.Math.Floor(value);
        }

        /// <summary>
        /// The pure scripted plan, with no learned residual. Kept because the
        /// probe and the offline fixtures call it directly, and because the
        /// residual network is defined as a correction to exactly this output.
        /// </summary>
        public static FormulaScriptOutput Tick(in FormulaScriptInput input)
        {
            var output = new FormulaScriptOutput { Accepted = false };
            if (!FormulaRouteCatalog.BelongsToBoss(input.Route, input.BossType) ||
                input.NativeTimer < 0 || input.NativeSequence < 0 ||
                input.NativeState < -1 || input.NativeState > 12)
                return output;
            output.Accepted = true;
            output.Fire = true;
            Fishron(in input, ref output);
            return output;
        }

        /// <summary>
        /// The learned-residual entry point. The scripted decision is the base
        /// and the network may only correct it.
        /// </summary>
        public static FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility)
        {
            var result = Tick(in input);
            if (!result.Accepted) return result;
            var learned = LearnedPolicy.ForRoute(input.Route);
            if (learned == null) return result;
            var phase = result.Phase;
            int horizontal;
            int vertical;
            bool jump;
            bool dash;
            if (!learned.Adjust(in input, player, in boss, arena, mobility,
                    result.Horizontal, result.Vertical, result.Jump,
                    result.Dash, out horizontal, out vertical, out jump,
                    out dash))
                return result;
            result.Horizontal = horizontal;
            result.Vertical = vertical;
            result.Jump = jump;
            result.Dash = dash;
            result.Phase = LearnedPolicy.ComposeLearnedPhase(phase,
                "formula-learned");
            return result;
        }

        private static void Fishron(in FormulaScriptInput input,
            ref FormulaScriptOutput output)
        {
            output.Phase = "fishron-state-" + input.NativeState;
            switch (input.NativeState)
            {
                case 1:
                case 6:
                case 11:
                    // A charge that arrives from above must be left on the
                    // horizontal axis, not met head-on.
                    //
                    // This used to zero the horizontal input whenever the
                    // player was below the boss and jump instead, which reads
                    // as "dodge vertically" but is the one direction that
                    // cannot work. AI_069 hovers above the player and charges
                    // down at 14.7 to 17.0 px/tick, so a player below the boss
                    // that climbs is moving into the body, and with horizontal
                    // zeroed the dash is mis-aimed too, because the dash writes
                    // velocity.X in the facing direction.
                    //
                    // MEASURED (dense native trace, tick 3005 of the live
                    // formula path): plan horizontal 0, controls L 0, R 0, with
                    // the player at plX 640 and vx 0.00 while the boss descended
                    // from x 597 to x 508 at bovy 15.8. Contact at tick 3019
                    // had the boxes overlapping 14 px horizontally and 29 px
                    // vertically. The player never moved on the axis that
                    // decided the outcome.
                    //
                    // The vertical escape still climbs, because a charge from
                    // above also has to be out-climbed eventually, but it no
                    // longer costs the horizontal axis. Native Y grows downward,
                    // so below the boss means descending: vy positive.
                    output.Horizontal = input.PlayerRightOfBoss ? -1 : 1;
                    output.Vertical = 1;
                    output.Dash = input.Route ==
                            FormulaRoute.FishronFairyWingsDash ||
                        input.Route == FormulaRoute.FishronStrongWingsDash;
                    output.Jump = true;
                    break;
                case 2:
                case 3:
                case 7:
                case 8:
                    output.Horizontal = !input.PlayerRightOfBoss ? 1 : -1;
                    output.Vertical = 1;
                    output.Jump = true;
                    break;
                default:
                    output.Horizontal = !input.PlayerRightOfBoss ? 1 : -1;
                    output.Vertical = !input.PlayerBelowBoss ? 1 : -1;
                    output.Jump = true;
                    break;
            }
        }
    }
}
