namespace Chaite.Core
{
    /// <summary>
    /// Verified identities for the Duke Fishron threats that a wing-route
    /// circuit cannot answer by moving. Values are read from the pinned
    /// 1.4.5.8 assembly; see docs/fishron-threat-identities.md.
    ///
    /// The important split is that the two bubble families behave the same way
    /// — strong homing, no way to outrun them — but do not cost the same to
    /// remove. The Detonating Bubble dies to any single hit; the two
    /// Sharknado-generating bubbles carry 100 life behind 100 defence and need
    /// a weapon that actually deals damage through that.
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

        /// <summary>The two bubbles that survive a single hit, so their removal
        /// is what the reviewed bubble-clearing weapon has to be judged on.</summary>
        public static bool IsArmouredBubble(int npcType) =>
            npcType == LargeSharknadoBubbleType ||
            npcType == SmallSharknadoBubbleType;
    }
}
