using System;
using System.Collections.Generic;

namespace Chaite.Manager
{
    internal enum SpriteKind
    {
        /// <summary>Id is an ItemID; the asset is Images/Item_&lt;id&gt;.</summary>
        Item,
        /// <summary>
        /// Id is a BossHeadTextures index, not an NPC id; the asset is
        /// Images/NPC_Head_Boss_&lt;id&gt;. Terraria ships these as purpose-made
        /// square icons, so they need no cropping and read better at 40 px than
        /// a frame lifted out of a tall animation sheet.
        /// </summary>
        BossHead
    }

    /// <summary>
    /// One vanilla sprite the console shows.
    ///
    /// The art is never bundled: it is read out of the owner's own Terraria
    /// installation at run time (see SpriteStore), so what the console draws is
    /// the real 1.4.5.8 texture for the owner's own copy and language. Nothing
    /// Re-Logic made is stored in this repository or in the installer payload.
    /// </summary>
    internal sealed class SpriteDefinition
    {
        internal SpriteDefinition(string key, SpriteKind kind, int id,
            string chineseName)
            : this(key, kind, id, chineseName, 0, 0)
        {
        }

        internal SpriteDefinition(string key, SpriteKind kind, int id,
            string chineseName, int frameWidth, int frameHeight)
        {
            Key = key;
            Kind = kind;
            Id = id;
            ChineseName = chineseName;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
        }

        /// <summary>Cache file stem; stable, so a renamed asset is a new file.</summary>
        internal string Key { get; private set; }
        internal SpriteKind Kind { get; private set; }
        internal int Id { get; private set; }
        /// <summary>The name Terraria itself uses in zh-Hans, for the tooltip.</summary>
        internal string ChineseName { get; private set; }
        /// <summary>Width of one frame; 0 means the whole image is the icon.</summary>
        internal int FrameWidth { get; private set; }
        /// <summary>Height of one frame; 0 means the whole image is the icon.</summary>
        internal int FrameHeight { get; private set; }

        /// <summary>
        /// The content path inside the game's Content directory. Terraria's own
        /// layout, so a wrong id fails loudly instead of drawing the wrong art.
        /// </summary>
        internal string AssetPath
        {
            get
            {
                return Kind == SpriteKind.Item
                    ? "Images/Item_" + Id
                    : "Images/NPC_Head_Boss_" + Id;
            }
        }
    }

    /// <summary>
    /// Every sprite the console may draw, with the official zh-Hans names read
    /// from the game's own localization rather than invented here.
    ///
    /// Ids are the ones the production route catalog already uses, so a loadout
    /// card cannot show an accessory the planner would not accept.
    /// </summary>
    internal static class SpriteCatalog
    {
        // Boss head indices come from the game's own
        // NPCID.Sets.BossHeadTextures initializer (the (npcType, headIndex)
        // pair table in Terraria.exe), read rather than guessed: Fishron 370->4.
        //
        // The console's name-plate. It is the real Duke Fishron head rather than
        // a drawing of one, because the owner asked for the game's own art. The
        // Skeletron head that used to sit here went with the Fishron-only scope:
        // the build drives exactly one Boss, so its emblem is that Boss, and an
        // emblem for a Boss it never touches would misstate what the program
        // does. The star-cannon/Skeletron joke survives in MemeVoice, where it
        // is text and costs nothing.
        internal static readonly SpriteDefinition DukeFishron =
            new SpriteDefinition("bosshead-4", SpriteKind.BossHead, 4,
                "猪龙鱼公爵");

        internal static readonly SpriteDefinition TruffleWorm =
            new SpriteDefinition("item-2673", SpriteKind.Item, 2673, "松露虫");

        internal static readonly SpriteDefinition LilithNecklace =
            new SpriteDefinition("item-5130", SpriteKind.Item, 5130,
                "Lilith的项链");
        internal static readonly SpriteDefinition BundleOfBalloons =
            new SpriteDefinition("item-1164", SpriteKind.Item, 1164, "气球束");
        internal static readonly SpriteDefinition FeatherfallPotion =
            new SpriteDefinition("item-295", SpriteKind.Item, 295, "羽落药水");
        internal static readonly SpriteDefinition FairyWings =
            new SpriteDefinition("item-761", SpriteKind.Item, 761, "仙灵之翼");
        internal static readonly SpriteDefinition FishronWings =
            new SpriteDefinition("item-2609", SpriteKind.Item, 2609,
                "猪龙鱼之翼");
        internal static readonly SpriteDefinition FrogLeg =
            new SpriteDefinition("item-2423", SpriteKind.Item, 2423, "蛙腿");
        internal static readonly SpriteDefinition ShieldOfCthulhu =
            new SpriteDefinition("item-3097", SpriteKind.Item, 3097,
                "克苏鲁护盾");
        internal static readonly SpriteDefinition Chillet =
            new SpriteDefinition("item-6150", SpriteKind.Item, 6150,
                "可靠的疾旋鼬");
        internal static readonly SpriteDefinition ChilletIgnis =
            new SpriteDefinition("item-6151", SpriteKind.Item, 6151,
                "可靠的疾旋火鼬");

        private static readonly SpriteDefinition[] Entries =
        {
            DukeFishron,
            TruffleWorm,
            LilithNecklace, BundleOfBalloons, FeatherfallPotion,
            FairyWings, FishronWings,
            FrogLeg, ShieldOfCthulhu,
            Chillet, ChilletIgnis
        };

        internal static SpriteDefinition[] All { get { return Entries; } }

        internal static SpriteDefinition Find(string key)
        {
            for (var i = 0; i < Entries.Length; i++)
                if (string.Equals(Entries[i].Key, key, StringComparison.Ordinal))
                    return Entries[i];
            return null;
        }

        /// <summary>
        /// Keys in a stable order, so the extraction manifest is byte-identical
        /// across runs and a re-extraction is a no-op rather than a rewrite.
        /// </summary>
        internal static List<string> Keys()
        {
            var keys = new List<string>(Entries.Length);
            for (var i = 0; i < Entries.Length; i++) keys.Add(Entries[i].Key);
            return keys;
        }
    }
}
