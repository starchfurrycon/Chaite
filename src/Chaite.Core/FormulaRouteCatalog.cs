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
        public const string Refusal = "\u8FD9\u4E2A\u6CE2\u65AF\uFF0C\u7528\u8FD9\u4E2A\u6B66\u5668\u6765\u6253\uFF0C\u4ECE\u6765\u6CA1\u8BD5\u8FC7\u54E6";
        public static bool IsSupportedBoss(int bossType) =>
            bossType == 370 || bossType == 636;

        public static bool BelongsToBoss(FormulaRoute route, int bossType)
        {
            switch (route)
            {
                case FormulaRoute.FishronFairyWingsDash:
                case FormulaRoute.FishronStrongWingsDash:
                case FormulaRoute.FishronQueenSlime:
                case FormulaRoute.FishronTrustyChillet:
                case FormulaRoute.FishronTrustyChilletIgnis:
                    return bossType == 370;
                case FormulaRoute.EmpressStrongWingsDash:
                case FormulaRoute.EmpressBroom:
                case FormulaRoute.EmpressRainFishron:
                    return bossType == 636;
                default: return false;
            }
        }

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
                if (dash && (wingItem == 2609 || wingItem == 492)) return FormulaRoute.FishronStrongWingsDash;
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
