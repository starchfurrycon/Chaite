using System.Collections.Generic;

namespace Chaite.Manager
{
    /// <summary>
    /// The Boss families the console presents. The Empress of Light is
    /// permanently out of scope for this program, so Duke Fishron is the only
    /// family left.
    /// </summary>
    internal enum BossFamily
    {
        Fishron
    }

    /// <summary>
    /// One reviewed loadout, as the console presents it to the owner.
    ///
    /// These are presentation rows only. Selecting one changes nothing about
    /// what the mod will accept: the route is still decided in game from the
    /// equipment actually worn, so a card here can never widen admission.
    /// </summary>
    internal sealed class LoadoutDefinition
    {
        internal LoadoutDefinition(string key, BossFamily boss, string name,
            SpriteDefinition[] icons, string[] requirements, string summon)
        {
            Key = key;
            Boss = boss;
            Name = name;
            Icons = icons;
            Requirements = requirements;
            Summon = summon;
        }

        internal string Key { get; private set; }
        internal BossFamily Boss { get; private set; }
        internal string Name { get; private set; }
        /// <summary>The vanilla sprites drawn on the card, left to right.</summary>
        internal SpriteDefinition[] Icons { get; private set; }
        /// <summary>What the owner has to be wearing, in official names.</summary>
        internal string[] Requirements { get; private set; }
        /// <summary>How this Boss is brought out.</summary>
        internal string Summon { get; private set; }
    }

    internal static class LoadoutCatalog
    {
        private static readonly LoadoutDefinition[] Entries =
        {
            new LoadoutDefinition("fishron-lilith", BossFamily.Fishron,
                "Lilith 的项链",
                new[]
                {
                    SpriteCatalog.LilithNecklace,
                    SpriteCatalog.BundleOfBalloons,
                    SpriteCatalog.FeatherfallPotion
                },
                new[]
                {
                    "Lilith的项链", "气球束（马掌气球束同族）", "羽落药水"
                },
                "在海洋用松露虫钓鱼"),

            new LoadoutDefinition("fishron-fairy", BossFamily.Fishron,
                "仙灵之翼",
                new[]
                {
                    SpriteCatalog.FairyWings, SpriteCatalog.FrogLeg,
                    SpriteCatalog.ShieldOfCthulhu, SpriteCatalog.FeatherfallPotion
                },
                new[] { "仙灵之翼", "蛙腿", "克苏鲁护盾", "羽落药水" },
                "在海洋用松露虫钓鱼"),

            new LoadoutDefinition("fishron-strong", BossFamily.Fishron,
                "高级翅膀",
                new[]
                {
                    SpriteCatalog.FishronWings, SpriteCatalog.FrogLeg,
                    SpriteCatalog.ShieldOfCthulhu, SpriteCatalog.FeatherfallPotion
                },
                new[]
                {
                    "猪龙鱼之翼（或同级强化翼）", "蛙腿", "克苏鲁护盾", "羽落药水"
                },
                "在海洋用松露虫钓鱼"),

            new LoadoutDefinition("fishron-chillet", BossFamily.Fishron,
                "疾旋鼬",
                new[]
                {
                    SpriteCatalog.Chillet, SpriteCatalog.ChilletIgnis,
                    SpriteCatalog.BundleOfBalloons, SpriteCatalog.FeatherfallPotion
                },
                new[] { "可靠的疾旋鼬 或 可靠的疾旋火鼬", "气球束（马掌气球束同族）", "羽落药水" },
                "在海洋用松露虫钓鱼")
        };

        internal static LoadoutDefinition[] All { get { return Entries; } }

        internal static List<LoadoutDefinition> For(BossFamily boss)
        {
            var result = new List<LoadoutDefinition>();
            for (var i = 0; i < Entries.Length; i++)
                if (Entries[i].Boss == boss) result.Add(Entries[i]);
            return result;
        }

        internal static string BossName(BossFamily boss)
        {
            return "猪龙鱼公爵";
        }

        internal static SpriteDefinition BossSprite(BossFamily boss)
        {
            return SpriteCatalog.DukeFishron;
        }

        internal static SpriteDefinition SummonSprite(BossFamily boss)
        {
            return SpriteCatalog.TruffleWorm;
        }

        internal static string SummonItem(BossFamily boss)
        {
            return "松露虫";
        }
    }
}
