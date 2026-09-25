namespace Chaite.Core
{
    /// <summary>Reachable AI_069 tuples for vanilla 1.4.5.8.</summary>
    public static class FishronFormulaStateContract
    {
        public static bool IsValid(in FormulaScriptInput input,
            DifficultySnapshot difficulty, bool nativeEnraged)
        {
            if (input.BossType != 370 || difficulty == null ||
                input.NativeState < -1 || input.NativeState > 12 ||
                input.NativeTimer < 0 || input.NativeSequence < 0)
                return false;
            var expert = difficulty.Expert || difficulty.Master;
            if (input.NativeState >= 9 && !expert) return false;
            var limit = TimerLimit(input.NativeState,
                input.NativeSequence, expert, nativeEnraged);
            if (limit < 0 || input.NativeTimer > limit &&
                !(nativeEnraged && IsDash(input.NativeState) &&
                  input.NativeTimer <= limit + 2)) return false;
            switch (input.NativeState)
            {
                case -1: return input.NativeSequence == 0;
                case 0: return input.NativeSequence <= 11;
                case 1: return input.NativeSequence <= 9;
                case 2: return input.NativeSequence == 1;
                case 3: return input.NativeSequence == 0 ||
                        nativeEnraged && input.NativeSequence == 1;
                case 4: return input.NativeSequence <= 11;
                case 5: return input.NativeSequence <= 7;
                case 6: return input.NativeSequence <= 5;
                case 7: return input.NativeSequence == 1;
                // Sequence 1 is reachable: state 8 is the phase-2 sixth attack,
                // and the native AI's attack counter is already 1 by the time it
                // enters that state. Audited tuple by tuple against the engine's
                // own recorded (ai[0], ai[3]) pairs -- this was the only case the
                // engine contradicted, and TimerLimit needed no change (every
                // observed pair's max(ai[2]) is exactly its limit minus one).
                // See artifacts/formula-contract-case8-restore-20260925.md.
                case 8: return input.NativeSequence <= 1;
                case 9: return input.NativeSequence <= 7;
                case 10: return input.NativeSequence <= 8;
                case 11:
                    return input.NativeSequence == 0 ||
                        input.NativeSequence == 2 ||
                        input.NativeSequence == 3 ||
                        input.NativeSequence == 5 ||
                        input.NativeSequence == 6 ||
                        input.NativeSequence == 7;
                case 12:
                    return input.NativeSequence == 1 ||
                        input.NativeSequence == 4 ||
                        input.NativeSequence == 8;
                default: return false;
            }
        }

        private static int TimerLimit(int state, int sequence, bool expert,
            bool enraged)
        {
            if (state == -1) return 75;
            if (state == 0) return enraged ? 10 : sequence < 10 ? 30 :
                expert ? 40 : 60;
            if (state == 1) return enraged ? 25 : expert ? 28 : 30;
            if (state == 2) return 80;
            if (state == 3) return enraged ? 180 : 90;
            if (state == 4) return 180;
            if (state == 5) return enraged ? 10 : sequence < 6 ?
                expert ? 40 : 20 : expert ? 40 : 60;
            if (state == 6) return enraged ? 25 : expert ? 27 : 30;
            if (state == 7) return 120;
            if (state == 8) return 90;
            if (state == 9) return 180;
            if (state == 10) return enraged ? 10 : 30;
            if (state == 11) return enraged ? 25 : expert ? 27 : 30;
            if (state == 12) return 30;
            return -1;
        }

        private static bool IsDash(int state) =>
            state == 1 || state == 6 || state == 11;
    }
}
