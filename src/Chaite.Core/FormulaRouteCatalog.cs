namespace Chaite.Core
{
    public enum FormulaRoute
    {
        None, FishronFairyWingsDash, FishronStrongWingsDash,
        FishronQueenSlime, FishronTrustyChillet, FishronTrustyChilletIgnis,
        EmpressStrongWingsDash, EmpressBroom, EmpressRainFishron
    }

    /// <summary>Route selection only. Matching equipment is not a no-hit proof.</summary>
    public static class FormulaRouteCatalog
    {
        public const string Refusal = "这个波斯，用这个武器来打，从来没试过哦";
        public static bool IsSupportedBoss(int bossType) =>
            bossType == 370 || bossType == 636;

        public static FormulaRoute Select(int bossType, int wingItem,
            int dashItem, bool crystalAssassinSet, int mountType,
            bool raining)
        {
            bool dash = dashItem == 3097 || dashItem == 984 ||
                crystalAssassinSet;
            if (bossType == 370)
            {
                if (mountType == 50) return FormulaRoute.FishronQueenSlime;
                if (mountType == 64) return FormulaRoute.FishronTrustyChillet;
                if (mountType == 65) return FormulaRoute.FishronTrustyChilletIgnis;
                if (mountType >= 0) return FormulaRoute.None;
                if (dash && wingItem == 761) return FormulaRoute.FishronFairyWingsDash;
                if (dash && wingItem == 2609) return FormulaRoute.FishronStrongWingsDash;
            }
            else if (bossType == 636)
            {
                if (mountType == 23) return FormulaRoute.EmpressBroom;
                if (mountType == 12 && raining) return FormulaRoute.EmpressRainFishron;
                if (mountType >= 0) return FormulaRoute.None;
                if (dash && wingItem == 2609) return FormulaRoute.EmpressStrongWingsDash;
            }
            return FormulaRoute.None;
        }
    }
}
