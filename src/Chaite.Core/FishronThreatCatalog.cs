namespace Chaite.Core
{
    /// <summary>
    /// Verified identities for the Duke Fishron threats that a wing-route
    /// circuit cannot answer by moving. Values are read from the pinned
    /// 1.4.5.8 assembly; see docs/fishron-threat-identities.md.
    ///
    /// Only the Detonating Bubble needs weapon fire. The two Sharknado
    /// bubbles are emitted by the tornado itself, and the reviewed play puts
    /// that tornado at an arena edge, so they are answered by position rather
    /// than by damage. That is what keeps the weapon requirement broad: the
    /// target has one life and no defence, so coverage and rate decide it and
    /// damage does not.
    /// </summary>
    public static class FishronThreatCatalog
    {
        /// <summary>Detonating Bubble: 36x36, 1 life, 0 defence, no tile
        /// collision, strongly homing, explodes. Twenty are emitted per
        /// phase-one attack and a ring of them in phase two.</summary>
        public const int DetonatingBubbleType = 371;
        /// <summary>Sharknado-generating bubble, large: 120x24, 100 life,
        /// 100 defence.</summary>
        public const int LargeSharknadoBubbleType = 372;
        /// <summary>Sharknado-generating bubble, small: 100x24, 100 life,
        /// 100 defence.</summary>
        public const int SmallSharknadoBubbleType = 373;
        /// <summary>Sharknado column: 150x42, 540-tick lifetime, stationary
        /// once the bolt has landed.</summary>
        public const int SharknadoType = 384;
        /// <summary>Sharknado bolt: 30x30, 300-tick lifetime, travels down and
        /// outwards from the Boss centre before it lands.</summary>
        public const int SharknadoBoltType = 385;

        public const int DetonatingBubbleLife = 1;
        public const int SharknadoBubbleLife = 100;
        public const int SharknadoBubbleDefense = 100;
        public const int SharknadoLifetimeTicks = 540;

        /// <summary>Every NPC here homes hard enough that movement alone cannot
        /// clear it; each has to be destroyed by weapon fire.</summary>
        public static bool IsHomingBubble(int npcType) =>
            npcType == DetonatingBubbleType ||
            npcType == LargeSharknadoBubbleType ||
            npcType == SmallSharknadoBubbleType;

        /// <summary>The two bubbles the Sharknado emits at the arena edge.
        /// They survive a single hit, but the reviewed circuit never has to
        /// remove them: it chooses where the tornado lands instead.</summary>
        public static bool IsSharknadoSpawnedBubble(int npcType) =>
            npcType == LargeSharknadoBubbleType ||
            npcType == SmallSharknadoBubbleType;

        /// <summary>The only Fishron threat that must be shot down, and the
        /// reason the route requires a wide, fast weapon in the hotbar.</summary>
        public static bool RequiresWeaponClearance(int npcType) =>
            npcType == DetonatingBubbleType;

        /// <summary>Reviewed bubble-clearing weapons. All three are chosen for
        /// coverage and rate rather than damage, because the target has one
        /// life and no defence. Item ids read from the pinned assembly.</summary>
        public const int GoldenShowerItem = 1336;
        public const int RazorbladeTyphoonItem = 2622;
        public const int RazorpineItem = 1930;
        /// <summary>Inferno Potion destroys Detonating Bubbles without any
        /// weapon input at all.</summary>
        public const int InfernoPotionItem = 2348;

        public static bool IsReviewedBubbleClearer(int itemType) =>
            itemType == GoldenShowerItem ||
            itemType == RazorbladeTyphoonItem ||
            itemType == RazorpineItem;
    }
}
