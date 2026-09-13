namespace Chaite.Core
{
    /// <summary>
    /// Evidence available to the control layer for one vanilla mount family.
    /// IdentityOnly entries are deliberately not authorized to emit inputs.
    /// </summary>
    public enum VanillaMountModelEvidence
    {
        IdentityOnly,
        ExactDryMotion
    }

    public struct VanillaMountDescriptor
    {
        public int MountType;
        public int SummonItemType;
        public int BuffType;
        public string Key;
        public bool Minecart;
        public VanillaMountModelEvidence Evidence;

        public bool HasSummonItem => SummonItemType > 0;
    }

    /// <summary>
    /// Complete vanilla 1.4.5.8 MountID identity directory (0..65). Keeping
    /// every identity here prevents a generic "can fly" or "is a mount" flag
    /// from merging mechanically different mounts. A directory entry is not a
    /// movement contract: only entries whose Evidence says so may be selected
    /// by a Boss baseline/controller.
    /// </summary>
    public static class VanillaMountCatalog
    {
        public const string VerifiedTerrariaSha256 =
            "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";

        private static readonly VanillaMountDescriptor[] Entries =
        {
            E(0, 1914, 90, "rudolph"),
            E(1, 2428, 128, "bunny"),
            E(2, 2429, 129, "pigron"),
            E(3, 2430, 130, "slime"),
            E(4, 2491, 131, "turtle"),
            E(5, 2502, 132, "bee"),
            E(6, 0, 118, "minecart", true),
            E(7, 2769, 141, "ufo"),
            E(8, 2768, 142, "drill"),
            E(9, 2771, 143, "scutlix"),
            E(10, 3260, 162, "unicorn"),
            E(11, 3353, 166, "mechanical-cart", true),
            E(12, 3367, 168, "cute-fishron"),
            E(13, 0, 184, "wooden-minecart", true),
            E(14, 3771, 193, "basilisk"),
            E(15, 4066, 208, "desert-minecart", true),
            E(16, 4067, 210, "minecarp", true),
            E(17, 4264, 212, "golf-cart"),
            E(18, 4426, 220, "bee-minecart", true),
            E(19, 4427, 222, "ladybug-minecart", true),
            E(20, 4428, 224, "pigron-minecart", true),
            E(21, 4429, 226, "sunflower-minecart", true),
            E(22, 4443, 228, "demonic-hellcart", true),
            E(23, 4444, 230, "witch-broom", false,
                VanillaMountModelEvidence.ExactDryMotion),
            E(24, 4450, 231, "shroom-minecart", true),
            E(25, 4451, 233, "amethyst-minecart", true),
            E(26, 4452, 235, "topaz-minecart", true),
            E(27, 4453, 237, "sapphire-minecart", true),
            E(28, 4454, 239, "emerald-minecart", true),
            E(29, 4455, 241, "ruby-minecart", true),
            E(30, 4456, 243, "diamond-minecart", true),
            E(31, 4467, 245, "amber-minecart", true),
            E(32, 4468, 247, "beetle-minecart", true),
            E(33, 4469, 249, "meowmere-minecart", true),
            E(34, 4470, 251, "party-wagon", true),
            E(35, 4471, 253, "dutchman-minecart", true),
            E(36, 4472, 255, "steampunk-minecart", true),
            E(37, 4716, 265, "flamingo"),
            E(38, 4745, 269, "coffin-minecart", true),
            E(39, 4763, 272, "digging-molecart", true),
            E(40, 4785, 275, "painted-horse"),
            E(41, 4786, 276, "majestic-horse"),
            E(42, 4787, 277, "dark-horse"),
            E(43, 4791, 278, "pogo-stick"),
            E(44, 4792, 279, "pirate-ship"),
            E(45, 4793, 280, "tree"),
            E(46, 4794, 281, "santank"),
            E(47, 4795, 282, "goat"),
            E(48, 4796, 283, "dark-mage-book"),
            E(49, 4828, 305, "lava-shark"),
            E(50, 4981, 318, "winged-slime"),
            E(51, 5125, 338, "fart-kart", true),
            E(52, 5130, 342, "wolf"),
            E(53, 5288, 346, "terra-fart-kart", true),
            E(54, 5510, 370, "velociraptor"),
            E(55, 5525, 374, "rat"),
            E(56, 5597, 377, "bat"),
            E(57, 5600, 378, "blue-roller-skates"),
            E(58, 5640, 379, "green-roller-skates"),
            E(59, 5641, 380, "classic-roller-skates"),
            E(60, 5642, 381, "party-roller-skates"),
            E(61, 5662, 384, "pixie"),
            E(62, 5665, 387, "chillet"),
            E(63, 5666, 388, "chillet-ignis"),
            E(64, 6150, 391, "trusty-chillet"),
            E(65, 6151, 392, "trusty-chillet-ignis")
        };

        public static int Count => Entries.Length;

        public static bool TryGetByMountType(int mountType,
            out VanillaMountDescriptor descriptor)
        {
            if (mountType >= 0 && mountType < Entries.Length &&
                Entries[mountType].MountType == mountType)
            {
                descriptor = Entries[mountType];
                return true;
            }
            descriptor = default(VanillaMountDescriptor);
            return false;
        }

        public static bool TryGetBySummonItem(int itemType,
            out VanillaMountDescriptor descriptor)
        {
            if (itemType > 0)
                for (var i = 0; i < Entries.Length; i++)
                    if (Entries[i].SummonItemType == itemType)
                    {
                        descriptor = Entries[i];
                        return true;
                    }
            descriptor = default(VanillaMountDescriptor);
            return false;
        }

        private static VanillaMountDescriptor E(int mountType, int itemType,
            int buffType, string key, bool minecart = false,
            VanillaMountModelEvidence evidence = VanillaMountModelEvidence.IdentityOnly)
        {
            return new VanillaMountDescriptor
            {
                MountType = mountType,
                SummonItemType = itemType,
                BuffType = buffType,
                Key = key,
                Minecart = minecart,
                Evidence = evidence
            };
        }
    }
}
