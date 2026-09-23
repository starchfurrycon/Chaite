namespace Chaite.Core
{
    public enum FormulaRoute
    {
        None, FishronFairyWingsDash, FishronStrongWingsDash,
        FishronTrustyChillet, FishronTrustyChilletIgnis,
        FishronLilithWolf
    }

    /// <summary>Route selection only. Matching equipment is not a no-hit proof.</summary>
    public static class FormulaRouteCatalog
    {
        public const string Refusal = "\u8FD9\u4E2A\u6CE2\u65AF\uFF0C\u7528\u8FD9\u4E2A\u6B66\u5668\u6765\u6253\uFF0C\u4ECE\u6765\u6CA1\u8BD5\u8FC7\u54E6";
        public static bool IsSupportedBoss(int bossType) => bossType == 370;

        public static bool IsTrustyChillet(FormulaRoute route) =>
            route == FormulaRoute.FishronTrustyChillet ||
            route == FormulaRoute.FishronTrustyChilletIgnis;

        public static int TrustyChilletMountType(FormulaRoute route) =>
            route == FormulaRoute.FishronTrustyChillet ? 64 :
            route == FormulaRoute.FishronTrustyChilletIgnis ? 65 : -1;

        /// <summary>Whether an observed mount satisfies a route's identity.
        ///
        /// Mount 64 and mount 65 are the same Palworld ground mount in two
        /// skins: identical dash, identical jump reach, no flight and no
        /// hover on either. They were admitted as two routes and would have
        /// been trained as two models, which buys nothing and costs a full
        /// training run, so they now select one route. The equality check that
        /// used to guard the locked route could not see that: it compared the
        /// observed mount type against a single expected id, so simply
        /// returning the merged route from Select would have rejected every
        /// mount-65 player at lock time. Membership is therefore decided here,
        /// in one place, instead of by == at the call site.</summary>
        public static bool IsAcceptableMount(FormulaRoute route, int mountType)
        {
            switch (route)
            {
                case FormulaRoute.FishronLilithWolf: return mountType == 52;
                case FormulaRoute.FishronTrustyChillet:
                case FormulaRoute.FishronTrustyChilletIgnis:
                    return mountType == 64 || mountType == 65;
                default: return false;
            }
        }

        public static bool BelongsToBoss(FormulaRoute route, int bossType)
        {
            switch (route)
            {
                case FormulaRoute.FishronFairyWingsDash:
                case FormulaRoute.FishronStrongWingsDash:
                case FormulaRoute.FishronTrustyChillet:
                case FormulaRoute.FishronTrustyChilletIgnis:
                case FormulaRoute.FishronLilithWolf:
                    return bossType == 370;
                default: return false;
            }
        }

        public static FormulaRoute Select(int bossType, int wingItem,
            int dashItem, bool crystalAssassinSet, bool frogLeg,
            int mountType, bool raining)
        {
            // raining is retained in the signature because callers and the probe
            // pass native weather through, but no reviewed route depends on it
            // any more: the rain-gated Shrimpy Truffle route was withdrawn as
            // out of scope. Duke Fishron is the only admitted Boss, so every
            // other Boss type falls through to None and is refused rather than
            // driven.
            bool dash = dashItem == 3097 || dashItem == 984 ||
                crystalAssassinSet;
            if (bossType == 370)
            {
                // The Queen Slime saddle (mount 50) was withdrawn from the
                // Fishron admission set on 2026-09-21 by the owner's call: it is
                // a pure mount with no flight, no hover and no dash, and its
                // measured training record was the worst of every admitted
                // loadout (19 wins in 12174 episodes, 0.16%, best win 3 hits).
                // It is removed here rather than merely untrained, so that the
                // admission rule and the training set cannot drift apart again.
                // The saddle therefore falls through to `mountType >= 0` below
                // and is refused, exactly like any other unreviewed mount.
                // Lilith's necklace is item 5130 and summons MountID.Wolf, which
                // is mount 52. Both ids were always resolvable from the assembly;
                // what was missing is that Select returned None for every mount
                // other than 50, 64 and 65, so mount 52 was never a reviewed
                // route rather than being an item that could not be found.
                if (mountType == 52) return FormulaRoute.FishronLilithWolf;
                // Mount 65 is the reskin of mount 64 and deliberately collapses
                // onto the same route; see IsAcceptableMount.
                if (mountType == 64 || mountType == 65)
                    return FormulaRoute.FishronTrustyChillet;
                if (mountType >= 0) return FormulaRoute.None;
                if (dash && frogLeg && wingItem == 761)
                    return FormulaRoute.FishronFairyWingsDash;
                if (dash && frogLeg && IsStrongWingItem(wingItem))
                    return FormulaRoute.FishronStrongWingsDash;
            }
            return FormulaRoute.None;
        }

        public static bool IsStrongWingItem(int type)
        {
            return type == 2609 || type == 492 || type == 493 ||
                type == 2280 || type == 3468 || type == 3469 ||
                type == 3470 || type == 3471 || type == 3883 ||
                type == 4823;
        }
    }
}
