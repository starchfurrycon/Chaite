using System;

namespace Chaite.Core
{
    /// <summary>
    /// AI_120 attack-table and clock contract for vanilla 1.4.5.8. It accepts
    /// only tuples reachable from the fixed P1/P2 tables; state 9 is the
    /// side-selected form of table entry 8.
    /// </summary>
    public static class EmpressFormulaStateContract
    {
        private static readonly int[] PhaseOne =
            { 2, 8, 6, 8, 5, 2, 8, 4, 8, 5 };
        private static readonly int[] PhaseTwoClassic =
            { 7, 2, 8, 5, 2, 6, 4, 8, 12 };
        private static readonly int[] PhaseTwoExpert =
            { 7, 2, 8, 11, 5, 2, 6, 4, 8, 12 };

        public static bool IsValid(in FormulaScriptInput input,
            DifficultySnapshot difficulty)
        {
            if (input.BossType != 636 || difficulty == null ||
                !input.NativeFormKnown || input.NativeForm < 0 ||
                input.NativeForm > 3 || input.NativeState < 0 ||
                input.NativeState > 12 || input.NativeState == 3 ||
                input.NativeTimer < 0 || input.NativeSequence < 0)
                return false;

            var second = input.NativeForm == 1 || input.NativeForm == 3;
            var expertSchedule = difficulty.DayTime || difficulty.Expert ||
                difficulty.Master;
            var state = input.NativeState;
            var index = input.NativeSequence;
            if (!second && (state == 7 || state == 11 || state == 12) ||
                second && state == 0 || state == 10 && index <= 0)
                return false;
            if (state == 0 && index != 0) return false;
            if (state != 0 && state != 1 && state != 10 &&
                !MatchesPrevious(state, second, expertSchedule, index))
                return false;

            var modifier = (second ? 15 : 0) +
                (expertSchedule ? 5 : 0);
            int duration;
            switch (state)
            {
                case 0: duration = 180; break;
                case 1:
                    duration = (int)Math.Ceiling((second ? 20f : 45f) *
                        (difficulty.ForTheWorthy ? .5f : 1f));
                    break;
                case 2: duration = 150 - modifier; break;
                case 4: duration = 120 - modifier; break;
                case 5: duration = 72 - modifier; break;
                case 6: duration = 300 - modifier; break;
                case 7: duration = expertSchedule ? 280 - modifier :
                        260 - modifier; break;
                case 8:
                case 9: duration = 110 - modifier; break;
                case 10: duration = 200 - modifier; break;
                case 11: duration = 120 - modifier; break;
                case 12: duration = 150 - modifier; break;
                default: return false;
            }
            return input.NativeTimer <= duration;
        }

        private static bool MatchesPrevious(int state, bool second,
            bool expert, int currentIndex)
        {
            if (currentIndex <= 0) return false;
            var table = !second ? PhaseOne : expert ? PhaseTwoExpert :
                PhaseTwoClassic;
            var scheduled = table[(currentIndex - 1) % table.Length];
            return scheduled == 8 ? state == 8 || state == 9 :
                scheduled == state;
        }
    }
}
