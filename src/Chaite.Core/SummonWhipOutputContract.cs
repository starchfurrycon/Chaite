using System;

namespace Chaite.Core
{
    /// <summary>
    /// Hash-locked Terraria 1.4.5.8 identity for one ordinary minion staff.
    /// The profile describes only deployment.  Minion combat remains native.
    /// </summary>
    public sealed class VanillaSummonStaffOutputProfile
    {
        public int ItemId { get; }
        public int ProjectileId { get; }
        // Production sums only this bounded hash-reviewed owner family. This
        // covers random variants and linked multi-projectile deployments
        // without scanning Main.projectile.
        public int OwnerProjectileId1 { get; }
        public int OwnerProjectileId2 { get; }
        public int OwnerProjectileId3 { get; }
        public int OwnerProjectileTypeCount { get; }
        // Most staffs create one owner projectile. Optic creates its two
        // half-slot twins, while the first Stardust Dragon use creates its
        // four linked identities. Confirmation must prove that exact delta.
        public int ExpectedOwnerProjectileDelta { get; }
        // Some native ItemCheck paths replace an existing related summon even
        // when capacity is available. Those profiles are admitted only from a
        // completely empty owner family so automation never performs a swap.
        public bool RequiresEmptyOwnerFamily { get; }
        // Tiger and Abigail use the existing owner-family count as a native
        // strengthening input. A live buff can be false while those roots
        // remain present; only these reviewed families may treat that count
        // as a new, capacity-checked reinforcement.
        public bool AllowsOwnerBaselineReinforcement { get; }
        public int BuffId { get; }
        public float MinionSlotCost { get; }
        public int DefaultUseAnimation { get; }
        public int DefaultUseTime { get; }
        public int DefaultReuseDelay { get; }

        internal VanillaSummonStaffOutputProfile(int itemId, int projectileId,
            int buffId, float minionSlotCost, int useAnimation, int useTime,
            int reuseDelay, int ownerProjectileId1 = 0,
            int ownerProjectileId2 = 0, int ownerProjectileId3 = 0,
            int expectedOwnerProjectileDelta = 1,
            bool requiresEmptyOwnerFamily = false,
            bool allowsOwnerBaselineReinforcement = false)
        {
            ItemId = itemId;
            ProjectileId = projectileId;
            OwnerProjectileId1 = ownerProjectileId1;
            OwnerProjectileId2 = ownerProjectileId2;
            OwnerProjectileId3 = ownerProjectileId3;
            OwnerProjectileTypeCount = ownerProjectileId3 > 0 ? 4 :
                ownerProjectileId2 > 0 ? 3 :
                ownerProjectileId1 > 0 ? 2 : 1;
            ExpectedOwnerProjectileDelta = expectedOwnerProjectileDelta;
            RequiresEmptyOwnerFamily = requiresEmptyOwnerFamily;
            AllowsOwnerBaselineReinforcement = allowsOwnerBaselineReinforcement;
            BuffId = buffId;
            MinionSlotCost = minionSlotCost;
            DefaultUseAnimation = useAnimation;
            DefaultUseTime = useTime;
            DefaultReuseDelay = reuseDelay;
        }

        public int OwnerProjectileIdAt(int index)
        {
            if (index == 0) return ProjectileId;
            if (index == 1 && OwnerProjectileTypeCount >= 2)
                return OwnerProjectileId1;
            if (index == 2 && OwnerProjectileTypeCount >= 3)
                return OwnerProjectileId2;
            if (index == 3 && OwnerProjectileTypeCount >= 4)
                return OwnerProjectileId3;
            return 0;
        }
    }

    /// <summary>
    /// Hash-locked Terraria 1.4.5.8 identity for one ordinary whip.
    /// Range and hit geometry are deliberately outside this deployment FSM.
    /// </summary>
    public sealed class VanillaWhipOutputProfile
    {
        public int ItemId { get; }
        public int ProjectileId { get; }
        public int DefaultUseAnimation { get; }
        public int DefaultUseTime { get; }
        public float DefaultShootSpeed { get; }

        internal VanillaWhipOutputProfile(int itemId, int projectileId,
            int useAnimation, int useTime, float shootSpeed)
        {
            ItemId = itemId;
            ProjectileId = projectileId;
            DefaultUseAnimation = useAnimation;
            DefaultUseTime = useTime;
            DefaultShootSpeed = shootSpeed;
        }
    }

    /// <summary>
    /// Hash-reviewed vanilla summon/whip allow-list.
    ///
    /// Evidence is Terraria.Item.SetDefaults and Projectile.SetDefaults from
    /// 1.4.5.8 SHA256
    /// 960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3.
    /// Non-sentry, non-pet staff tuples are (item, shoot, buff,
    /// animation/time/reuse):
    /// 1157=(191,49,28/28/2), 1309=(266,64,28/28/2),
    /// 1802=(317,83,28/28/2), 2364=(373,125,22/22/2),
    /// 2365=(375,126,36/36/2), 2535=(387,134,36/36/2),
    /// 2551=(390,133,36/36/2), 2584=(393,135,36/36/2),
    /// 2621=(407,139,36/36/2), 2749=(423,140,36/36/2),
    /// 3249=(533,161,36/36/2), 3474=(613,182,36/36/2),
    /// 3531=(625,188,36/36/2), 4269=(755,213,36/36/2),
    /// 4273=(758,214,36/36/2), 4281=(759,216,36/36/2),
    /// 4607=(831,263,36/36/2), 4758=(864,271,36/36/2),
    /// 5005=(946,322,36/36/2), 5069=(951,325,36/36/2),
    /// 5114=(970,335,36/36/2), 5456=(1022,355,36/36/2),
    /// 5663=(1093,385,15/15/2), 5664=(1094,386,15/15/2),
    /// 6148=(1112,389,15/15/2), 6149=(1113,390,15/15/2),
    /// 6161=(1118,393,36/36/2), and 6164=(1119,394,36/36/2).
    /// Their root projectiles all declare minion=true. Roots cost one slot,
    /// except Optic's 387+388 pair (0.5 each) and Stardust Dragon's initial
    /// 625+626+627+628 chain (only 626/627 cost 0.5 each).
    /// Player.ItemCheck_Shoot chooses Pygmy 191..194 and Pirate 393..395,
    /// cycles Spider 390..392, creates both Optic twins, and creates four
    /// linked Dragon identities on the first use. Those exact owner families
    /// and deltas are certified below. Dragon is restricted to an empty family
    /// so the variable two-segment reinforcement branch is never automated.
    /// Native FreeUpPetsAndMinions replaces related Palworld roots 1093/1112
    /// or 1094/1113; those four items are likewise admitted only when their
    /// complete related owner family is empty. The controller itself never
    /// calls that method and its exact free-slot guard prevents capacity-based
    /// eviction. Tiger 831 and Abigail 970 retain one owner root per added
    /// slot, so their native strengthening-by-count remains exactly provable.
    ///
    /// Every real Item.SetDefaults switch case which calls Item.DefaultToWhip
    /// gives the following (item, shoot, animation/time, shootSpeed) tuples:
    /// 4672=(841,30/30,4), 4678=(847,28/28,4),
    /// 4679=(848,35/35,4), 4680=(849,27/27,4),
    /// 4911=(912,30/30,4), 4912=(913,30/30,4),
    /// 4913=(914,30/30,4), 4914=(915,30/30,4),
    /// 5074=(952,30/30,5), 5473=(1028,35/35,3),
    /// 5474=(1029,30/30,4), 5475=(1030,30/30,4),
    /// 5476=(1031,30/30,4), 5477=(1032,30/30,4),
    /// 5478=(1033,30/30,4), 5479=(1034,30/30,4),
    /// 5480=(1035,30/30,4), and 5688=(1104,30/30,4).
    /// The method declaration itself is not a catalog entry. Item 5480 then
    /// overrides useStyle to 5; that identity exception is recorded here but
    /// is not treated as evidence that its geometry matches another whip.
    /// DefaultToWhip sets autoReuse=false for all eighteen, so the controller
    /// retains one real release frame after every native use pulse. No range,
    /// hit geometry, tag damage, or minion damage is inferred by this table.
    /// </summary>
    public static class VanillaSummonWhipOutputCatalog
    {
        private static readonly VanillaSummonStaffOutputProfile Staff1157 =
            Staff(1157, 191, 49, 28, 28, 2, 192, 193, 194);
        private static readonly VanillaSummonStaffOutputProfile Staff1309 =
            Staff(1309, 266, 64, 28, 28, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff1802 =
            Staff(1802, 317, 83, 28, 28, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff2364 =
            Staff(2364, 373, 125, 22, 22, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff2365 =
            Staff(2365, 375, 126, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff2535 =
            Staff(2535, 387, 134, 36, 36, 2, 388,
                expectedOwnerProjectileDelta: 2);
        private static readonly VanillaSummonStaffOutputProfile Staff2551 =
            Staff(2551, 390, 133, 36, 36, 2, 391, 392);
        private static readonly VanillaSummonStaffOutputProfile Staff2584 =
            Staff(2584, 393, 135, 36, 36, 2, 394, 395);
        private static readonly VanillaSummonStaffOutputProfile Staff2621 =
            Staff(2621, 407, 139, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff2749 =
            Staff(2749, 423, 140, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff3249 =
            Staff(3249, 533, 161, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff3474 =
            Staff(3474, 613, 182, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff3531 =
            Staff(3531, 625, 188, 36, 36, 2, 626, 627, 628,
                expectedOwnerProjectileDelta: 4,
                requiresEmptyOwnerFamily: true);
        private static readonly VanillaSummonStaffOutputProfile Staff4269 =
            Staff(4269, 755, 213, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff4273 =
            Staff(4273, 758, 214, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff4281 =
            Staff(4281, 759, 216, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff4607 =
            Staff(4607, 831, 263, 36, 36, 2,
                allowsOwnerBaselineReinforcement: true);
        private static readonly VanillaSummonStaffOutputProfile Staff4758 =
            Staff(4758, 864, 271, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff5005 =
            Staff(5005, 946, 322, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff5069 =
            Staff(5069, 951, 325, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff5114 =
            Staff(5114, 970, 335, 36, 36, 2,
                allowsOwnerBaselineReinforcement: true);
        private static readonly VanillaSummonStaffOutputProfile Staff5456 =
            Staff(5456, 1022, 355, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff5663 =
            Staff(5663, 1093, 385, 15, 15, 2, 1112,
                requiresEmptyOwnerFamily: true);
        private static readonly VanillaSummonStaffOutputProfile Staff5664 =
            Staff(5664, 1094, 386, 15, 15, 2, 1113,
                requiresEmptyOwnerFamily: true);
        private static readonly VanillaSummonStaffOutputProfile Staff6148 =
            Staff(6148, 1112, 389, 15, 15, 2, 1093,
                requiresEmptyOwnerFamily: true);
        private static readonly VanillaSummonStaffOutputProfile Staff6149 =
            Staff(6149, 1113, 390, 15, 15, 2, 1094,
                requiresEmptyOwnerFamily: true);
        private static readonly VanillaSummonStaffOutputProfile Staff6161 =
            Staff(6161, 1118, 393, 36, 36, 2);
        private static readonly VanillaSummonStaffOutputProfile Staff6164 =
            Staff(6164, 1119, 394, 36, 36, 2);

        private static readonly VanillaWhipOutputProfile Whip4672 =
            Whip(4672, 841, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip4678 =
            Whip(4678, 847, 28, 28, 4f);
        private static readonly VanillaWhipOutputProfile Whip4679 =
            Whip(4679, 848, 35, 35, 4f);
        private static readonly VanillaWhipOutputProfile Whip4680 =
            Whip(4680, 849, 27, 27, 4f);
        private static readonly VanillaWhipOutputProfile Whip4911 =
            Whip(4911, 912, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip4912 =
            Whip(4912, 913, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip4913 =
            Whip(4913, 914, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip4914 =
            Whip(4914, 915, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5074 =
            Whip(5074, 952, 30, 30, 5f);
        private static readonly VanillaWhipOutputProfile Whip5473 =
            Whip(5473, 1028, 35, 35, 3f);
        private static readonly VanillaWhipOutputProfile Whip5474 =
            Whip(5474, 1029, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5475 =
            Whip(5475, 1030, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5476 =
            Whip(5476, 1031, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5477 =
            Whip(5477, 1032, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5478 =
            Whip(5478, 1033, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5479 =
            Whip(5479, 1034, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5480 =
            Whip(5480, 1035, 30, 30, 4f);
        private static readonly VanillaWhipOutputProfile Whip5688 =
            Whip(5688, 1104, 30, 30, 4f);

        public static bool TryGetStaff(int itemId,
            out VanillaSummonStaffOutputProfile profile)
        {
            switch (itemId)
            {
            case 1157: profile = Staff1157; return true;
            case 1309: profile = Staff1309; return true;
            case 1802: profile = Staff1802; return true;
            case 2364: profile = Staff2364; return true;
            case 2365: profile = Staff2365; return true;
            case 2535: profile = Staff2535; return true;
            case 2551: profile = Staff2551; return true;
            case 2584: profile = Staff2584; return true;
            case 2621: profile = Staff2621; return true;
            case 2749: profile = Staff2749; return true;
            case 3249: profile = Staff3249; return true;
            case 3474: profile = Staff3474; return true;
            case 3531: profile = Staff3531; return true;
            case 4269: profile = Staff4269; return true;
            case 4273: profile = Staff4273; return true;
            case 4281: profile = Staff4281; return true;
            case 4607: profile = Staff4607; return true;
            case 4758: profile = Staff4758; return true;
            case 5005: profile = Staff5005; return true;
            case 5069: profile = Staff5069; return true;
            case 5114: profile = Staff5114; return true;
            case 5456: profile = Staff5456; return true;
            case 5663: profile = Staff5663; return true;
            case 5664: profile = Staff5664; return true;
            case 6148: profile = Staff6148; return true;
            case 6149: profile = Staff6149; return true;
            case 6161: profile = Staff6161; return true;
            case 6164: profile = Staff6164; return true;
            default: profile = null; return false;
            }
        }

        public static bool TryGetWhip(int itemId,
            out VanillaWhipOutputProfile profile)
        {
            switch (itemId)
            {
            case 4672: profile = Whip4672; return true;
            case 4678: profile = Whip4678; return true;
            case 4679: profile = Whip4679; return true;
            case 4680: profile = Whip4680; return true;
            case 4911: profile = Whip4911; return true;
            case 4912: profile = Whip4912; return true;
            case 4913: profile = Whip4913; return true;
            case 4914: profile = Whip4914; return true;
            case 5074: profile = Whip5074; return true;
            case 5473: profile = Whip5473; return true;
            case 5474: profile = Whip5474; return true;
            case 5475: profile = Whip5475; return true;
            case 5476: profile = Whip5476; return true;
            case 5477: profile = Whip5477; return true;
            case 5478: profile = Whip5478; return true;
            case 5479: profile = Whip5479; return true;
            case 5480: profile = Whip5480; return true;
            case 5688: profile = Whip5688; return true;
            default: profile = null; return false;
            }
        }

        private static VanillaSummonStaffOutputProfile Staff(int itemId,
            int projectileId, int buffId, int useAnimation, int useTime,
            int reuseDelay, int ownerProjectileId1 = 0,
            int ownerProjectileId2 = 0, int ownerProjectileId3 = 0,
            int expectedOwnerProjectileDelta = 1,
            bool requiresEmptyOwnerFamily = false,
            bool allowsOwnerBaselineReinforcement = false) =>
            new VanillaSummonStaffOutputProfile(itemId, projectileId, buffId,
                1f, useAnimation, useTime, reuseDelay, ownerProjectileId1,
                ownerProjectileId2, ownerProjectileId3,
                expectedOwnerProjectileDelta, requiresEmptyOwnerFamily,
                allowsOwnerBaselineReinforcement);

        private static VanillaWhipOutputProfile Whip(int itemId,
            int projectileId, int useAnimation, int useTime, float shootSpeed) =>
            new VanillaWhipOutputProfile(itemId, projectileId, useAnimation,
                useTime, shootSpeed);
    }

    /// <summary>Read-only identity observed in one hotbar slot.</summary>
    public struct SummonWhipItemObservation
    {
        public bool Known;
        public int Slot;
        public int Stack;
        public int ItemId;
        public int ProjectileId;
        // Zero for a whip.  A staff must expose its exact Item.buffType.
        public int BuffId;
    }

    /// <summary>
    /// All live facts consumed by the pure dual-slot FSM.  OwnerProjectileCount
    /// is the sum of Player.ownedProjectileCounts for every type declared by
    /// StaffProfile.OwnerProjectileIdAt.  This is one to four bounded O(1)
    /// lookups, depending on the reviewed owner family; never scan world
    /// projectiles.
    /// </summary>
    public struct SummonWhipOutputObservation
    {
        public bool Known;
        public SummonWhipItemObservation Staff;
        public SummonWhipItemObservation Whip;
        public int SelectedSlot;
        public bool CanUseItem;
        public bool ReleaseUseItem;
        public int ItemAnimation;
        public int ItemTime;

        public bool MinionCapacityKnown;
        public float UsedMinionSlots;
        public int MaximumMinionSlots;
        public bool BuffStateKnown;
        public bool ExpectedStaffBuffActive;
        public bool OwnerProjectileCountKnown;
        public int OwnerProjectileCount;

        // Player.GetWeaponDamage already includes the live prefix/equipment
        // modifiers.  A whip produces at most one direct strike per complete
        // use animation, so this deliberately ignores all minion damage and is
        // a conservative lower-bound used only for Boss output admission.
        public bool WhipOutputKnown;
        public int WhipEffectiveDamage;
        public int WhipUseAnimation;
        public int WhipUseTime;

        public float ConservativeWhipDps
        {
            get
            {
                var cadence = Math.Max(WhipUseAnimation, WhipUseTime);
                if (!WhipOutputKnown || WhipEffectiveDamage <= 0 ||
                    cadence <= 0)
                    return 0f;
                return WhipEffectiveDamage * 60f / cadence;
            }
        }
    }

    /// <summary>Immutable two-slot identity admitted before a boss begins.</summary>
    public struct SummonWhipOutputRoute
    {
        public VanillaSummonStaffOutputProfile StaffProfile;
        public VanillaWhipOutputProfile WhipProfile;
        public int StaffSlot;
        public int WhipSlot;
        public int BaselineOwnerProjectileCount;
        public float BaselineUsedMinionSlots;
        // A matching live buff plus the complete reviewed owner-projectile
        // family may be adopted without issuing another staff use. This is
        // especially important for mid-fight takeover and full minion slots.
        public bool UsesExistingDeployment;

        public bool IsSpecified => StaffProfile != null && WhipProfile != null &&
            StaffSlot >= 0 && StaffSlot < 10 && WhipSlot >= 0 && WhipSlot < 10 &&
            StaffSlot != WhipSlot && BaselineOwnerProjectileCount >= 0 &&
            StaffProfile.ExpectedOwnerProjectileDelta > 0 &&
            StaffProfile.ExpectedOwnerProjectileDelta <=
                StaffProfile.OwnerProjectileTypeCount &&
            FiniteNonnegative(BaselineUsedMinionSlots) &&
            (!UsesExistingDeployment ||
             BaselineOwnerProjectileCount >=
                StaffProfile.ExpectedOwnerProjectileDelta &&
             BaselineUsedMinionSlots + .001f >=
                StaffProfile.MinionSlotCost);

        private static bool FiniteNonnegative(float value) => value >= 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class SummonWhipOutputRouteContract
    {
        private const float SlotEpsilon = .001f;

        public static bool TryCreateReady(
            in SummonWhipOutputObservation observation,
            out SummonWhipOutputRoute route, out string reason)
        {
            route = default(SummonWhipOutputRoute);
            VanillaSummonStaffOutputProfile staff;
            VanillaWhipOutputProfile whip;
            if (!TryReadProfiles(in observation, out staff, out whip,
                out reason))
                return false;
            if (!ValidCapacity(in observation))
            {
                reason = "native minion-slot capacity is unavailable or invalid";
                return false;
            }
            var hasExistingBuff = observation.ExpectedStaffBuffActive;
            var hasExistingOwner = observation.OwnerProjectileCount > 0;
            // Tiger and Abigail retain one native root per additional use.
            // When that root family is present without the staff buff, the
            // next use is a provable reinforcement (not an adoption of an
            // existing deployment). Every other family must either be empty
            // or present with both the complete buff and owner proof.
            var baselineReinforcement = !hasExistingBuff && hasExistingOwner &&
                staff.AllowsOwnerBaselineReinforcement;
            var usesExisting = hasExistingBuff;
            // These native branches (Dragon and the related Palworld roots)
            // can replace/reinforce a related family rather than append one.
            // The adapter publishes a family count, not per-type identities,
            // so any non-empty baseline must be rejected before the generic
            // "partial proof" check below. This keeps the route fail-closed
            // while preserving an actionable reason for the caller.
            if (staff.RequiresEmptyOwnerFamily && hasExistingOwner)
            {
                reason = "native staff would reinforce or replace an existing related minion";
                return false;
            }
            if (hasExistingOwner && !hasExistingBuff &&
                !staff.AllowsOwnerBaselineReinforcement)
            {
                reason = "matching minion buff and complete owner family are inconsistent; refusing replacement or reinforcement";
                return false;
            }
            if (baselineReinforcement &&
                (observation.OwnerProjectileCount <
                    staff.ExpectedOwnerProjectileDelta ||
                 observation.UsedMinionSlots + SlotEpsilon <
                    staff.MinionSlotCost))
            {
                reason = "existing owner family cannot prove a capacity-safe reinforcement";
                return false;
            }
            if (usesExisting &&
                (!hasExistingBuff ||
                 observation.OwnerProjectileCount <
                    staff.ExpectedOwnerProjectileDelta ||
                 observation.UsedMinionSlots + SlotEpsilon <
                    staff.MinionSlotCost))
            {
                reason = "matching minion buff and complete owner family are inconsistent; refusing replacement or reinforcement";
                return false;
            }
            if (!usesExisting &&
                observation.MaximumMinionSlots - observation.UsedMinionSlots +
                    SlotEpsilon < staff.MinionSlotCost)
            {
                reason = "one deployment would replace or evict an existing minion";
                return false;
            }
            route = new SummonWhipOutputRoute
            {
                StaffProfile = staff,
                WhipProfile = whip,
                StaffSlot = observation.Staff.Slot,
                WhipSlot = observation.Whip.Slot,
                BaselineOwnerProjectileCount = observation.OwnerProjectileCount,
                BaselineUsedMinionSlots = observation.UsedMinionSlots,
                UsesExistingDeployment = usesExisting
            };
            if (!route.IsSpecified)
            {
                route = default(SummonWhipOutputRoute);
                reason = "dual-slot summon route is incomplete";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool ValidateLive(
            in SummonWhipOutputObservation observation,
            in SummonWhipOutputRoute expected, out string reason)
        {
            if (!expected.IsSpecified)
            {
                reason = "latched summon/whip route is incomplete";
                return false;
            }
            VanillaSummonStaffOutputProfile staff;
            VanillaWhipOutputProfile whip;
            if (!TryReadProfiles(in observation, out staff, out whip,
                out reason))
                return false;
            if (!ReferenceEquals(staff, expected.StaffProfile) ||
                !ReferenceEquals(whip, expected.WhipProfile) ||
                observation.Staff.Slot != expected.StaffSlot ||
                observation.Whip.Slot != expected.WhipSlot)
            {
                reason = "staff or whip identity changed after route admission";
                return false;
            }
            if (!ValidCapacity(in observation) ||
                observation.MaximumMinionSlots + SlotEpsilon <
                    expected.StaffProfile.MinionSlotCost)
            {
                reason = "live minion-slot capacity is unavailable or invalid";
                return false;
            }
            if (expected.UsesExistingDeployment &&
                !ExistingDeploymentConfirmed(in observation, in expected))
            {
                reason = "adopted minion buff, owner family, or slot baseline changed";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool HasFreeSlotForOneDeployment(
            in SummonWhipOutputObservation observation,
            in SummonWhipOutputRoute expected)
        {
            return expected.IsSpecified && !expected.UsesExistingDeployment &&
                ValidCapacity(in observation) &&
                observation.MaximumMinionSlots - observation.UsedMinionSlots +
                    SlotEpsilon >= expected.StaffProfile.MinionSlotCost;
        }

        public static bool DeploymentConfirmed(
            in SummonWhipOutputObservation observation,
            in SummonWhipOutputRoute expected)
        {
            if (!expected.IsSpecified || !observation.BuffStateKnown ||
                !observation.OwnerProjectileCountKnown)
                return false;
            if (expected.UsesExistingDeployment)
                return ExistingDeploymentConfirmed(in observation,
                    in expected);
            if (!observation.ExpectedStaffBuffActive ||
                observation.OwnerProjectileCount !=
                    expected.BaselineOwnerProjectileCount +
                    expected.StaffProfile.ExpectedOwnerProjectileDelta)
                return false;
            // Every admitted staff use costs exactly one total slot. Most add
            // one root, Optic adds two half-slot roots, and an initial Dragon
            // adds four linked identities whose two body segments cost 0.5.
            // Exact owner-family and slot deltas reject unrelated changes.
            return ValidCapacity(in observation) &&
                Math.Abs(observation.UsedMinionSlots -
                    (expected.BaselineUsedMinionSlots +
                     expected.StaffProfile.MinionSlotCost)) <= SlotEpsilon;
        }

        public static bool DeploymentBaselineUnchanged(
            in SummonWhipOutputObservation observation,
            in SummonWhipOutputRoute expected)
        {
            return expected.IsSpecified && !expected.UsesExistingDeployment &&
                observation.OwnerProjectileCountKnown &&
                observation.OwnerProjectileCount ==
                    expected.BaselineOwnerProjectileCount &&
                ValidCapacity(in observation) &&
                Math.Abs(observation.UsedMinionSlots -
                    expected.BaselineUsedMinionSlots) <= SlotEpsilon;
        }

        private static bool ExistingDeploymentConfirmed(
            in SummonWhipOutputObservation observation,
            in SummonWhipOutputRoute expected)
        {
            return expected.IsSpecified && expected.UsesExistingDeployment &&
                observation.BuffStateKnown &&
                observation.ExpectedStaffBuffActive &&
                observation.OwnerProjectileCountKnown &&
                observation.OwnerProjectileCount ==
                    expected.BaselineOwnerProjectileCount &&
                ValidCapacity(in observation) &&
                Math.Abs(observation.UsedMinionSlots -
                    expected.BaselineUsedMinionSlots) <= SlotEpsilon;
        }

        private static bool TryReadProfiles(
            in SummonWhipOutputObservation observation,
            out VanillaSummonStaffOutputProfile staff,
            out VanillaWhipOutputProfile whip, out string reason)
        {
            staff = null;
            whip = null;
            if (!observation.Known || !observation.Staff.Known ||
                !observation.Whip.Known || !observation.BuffStateKnown ||
                !observation.OwnerProjectileCountKnown ||
                !observation.WhipOutputKnown)
            {
                reason = "native summon/whip observation is incomplete";
                return false;
            }
            if (!ValidHotbarItem(in observation.Staff) ||
                !ValidHotbarItem(in observation.Whip) ||
                observation.Staff.Slot == observation.Whip.Slot ||
                observation.SelectedSlot < 0 || observation.SelectedSlot >= 10 ||
                observation.ItemAnimation < 0 || observation.ItemTime < 0 ||
                observation.OwnerProjectileCount < 0 ||
                observation.WhipEffectiveDamage <= 0 ||
                observation.WhipUseAnimation <= 0 ||
                observation.WhipUseTime <= 0 ||
                !FinitePositive(observation.ConservativeWhipDps))
            {
                reason = "native summon/whip observation contains invalid values";
                return false;
            }
            if (!VanillaSummonWhipOutputCatalog.TryGetStaff(
                    observation.Staff.ItemId, out staff) ||
                observation.Staff.ProjectileId != staff.ProjectileId ||
                observation.Staff.BuffId != staff.BuffId)
            {
                reason = "staff is not an exact reviewed vanilla identity";
                return false;
            }
            if (!VanillaSummonWhipOutputCatalog.TryGetWhip(
                    observation.Whip.ItemId, out whip) ||
                observation.Whip.ProjectileId != whip.ProjectileId ||
                observation.Whip.BuffId != 0)
            {
                reason = "whip is not an exact reviewed vanilla identity";
                return false;
            }
            reason = null;
            return true;
        }

        private static bool ValidHotbarItem(
            in SummonWhipItemObservation item) => item.Slot >= 0 &&
            item.Slot < 10 && item.Stack > 0 && item.ItemId > 0 &&
            item.ProjectileId > 0;

        private static bool ValidCapacity(
            in SummonWhipOutputObservation observation) =>
            observation.MinionCapacityKnown &&
            observation.MaximumMinionSlots > 0 &&
            observation.UsedMinionSlots >= 0f &&
            !float.IsNaN(observation.UsedMinionSlots) &&
            !float.IsInfinity(observation.UsedMinionSlots) &&
                observation.UsedMinionSlots <=
                observation.MaximumMinionSlots + SlotEpsilon;

        private static bool FinitePositive(float value) => value > 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum SummonWhipOutputPhase
    {
        SelectStaff,
        DeployOnce,
        ConfirmDeployment,
        SelectWhip,
        WhipLoop,
        FailedClosed
    }

    public enum SummonWhipOutputAction
    {
        None,
        SelectStaff,
        PulseStaffUse,
        ReleaseUseItem,
        SelectWhip,
        PulseWhipUse,
        FailedClosed
    }

    public struct SummonWhipOutputDecision
    {
        public SummonWhipOutputPhase Phase;
        public SummonWhipOutputAction Action;
        // -1 means no selection request.  UseItem is true for exactly one
        // native frame only on PulseStaffUse or PulseWhipUse.
        public int SelectSlot;
        public bool UseItem;
        public bool Failed;
        public string FailureReason;
    }

    /// <summary>
    /// Allocation-free-after-construction dual-slot output state machine.
    /// It deploys exactly one new minion, proves both its buff and its
    /// owner-scoped projectile, and only then enters a release-edge-gated whip
    /// loop.  It never calls FreeUpPetsAndMinions and never retries deployment.
    /// </summary>
    public sealed class SummonWhipOutputController
    {
        private const int SelectionTimeoutTicks = 90;
        private const int DeployReadyTimeoutTicks = 90;
        private readonly SummonWhipOutputRoute _route;
        private SummonWhipOutputPhase _phase;
        private int _phaseTicks;
        private bool _staffPulseIssued;
        private bool _releaseWhipPulse;
        private string _failureReason;

        public SummonWhipOutputPhase Phase => _phase;
        public bool Failed => _phase == SummonWhipOutputPhase.FailedClosed;
        public SummonWhipOutputRoute Route => _route;

        private SummonWhipOutputController(in SummonWhipOutputRoute route)
        {
            _route = route;
            _phase = route.UsesExistingDeployment ?
                SummonWhipOutputPhase.SelectWhip :
                SummonWhipOutputPhase.SelectStaff;
        }

        public static bool TryCreate(
            in SummonWhipOutputObservation observation,
            out SummonWhipOutputController controller, out string reason)
        {
            SummonWhipOutputRoute route;
            if (!SummonWhipOutputRouteContract.TryCreateReady(in observation,
                out route, out reason))
            {
                controller = null;
                return false;
            }
            controller = new SummonWhipOutputController(in route);
            return true;
        }

        public SummonWhipOutputDecision Tick(
            in SummonWhipOutputObservation observation,
            bool attackRequested, bool allowItemUse = true)
        {
            if (Failed)
                return FailedDecision();

            string reason;
            if (!SummonWhipOutputRouteContract.ValidateLive(in observation,
                in _route, out reason))
                return Fail(reason);

            switch (_phase)
            {
            case SummonWhipOutputPhase.SelectStaff:
                if (_staffPulseIssued)
                    return Fail("staff deployment pulse cannot be repeated");
                if (!SummonWhipOutputRouteContract.
                    DeploymentBaselineUnchanged(in observation, in _route))
                    return Fail("minion identity or slot baseline changed before deployment");
                if (!SummonWhipOutputRouteContract.HasFreeSlotForOneDeployment(
                    in observation, in _route))
                    return Fail("one deployment no longer fits without eviction");
                if (observation.SelectedSlot != _route.StaffSlot)
                {
                    if (++_phaseTicks > SelectionTimeoutTicks)
                        return Fail("staff selection did not become observable");
                    return Decision(SummonWhipOutputAction.SelectStaff,
                        _route.StaffSlot, false);
                }
                _phase = SummonWhipOutputPhase.DeployOnce;
                _phaseTicks = 0;
                goto case SummonWhipOutputPhase.DeployOnce;

            case SummonWhipOutputPhase.DeployOnce:
                if (_staffPulseIssued)
                    return Fail("staff deployment pulse cannot be repeated");
                if (!SummonWhipOutputRouteContract.
                    DeploymentBaselineUnchanged(in observation, in _route))
                    return Fail("minion identity or slot baseline changed before deployment");
                if (!SummonWhipOutputRouteContract.HasFreeSlotForOneDeployment(
                    in observation, in _route))
                    return Fail("one deployment no longer fits without eviction");
                if (observation.SelectedSlot != _route.StaffSlot)
                {
                    _phase = SummonWhipOutputPhase.SelectStaff;
                    _phaseTicks = 0;
                    return Decision(SummonWhipOutputAction.SelectStaff,
                        _route.StaffSlot, false);
                }
                if (!allowItemUse)
                    return Decision(SummonWhipOutputAction.None, -1, false);
                if (!observation.CanUseItem || !observation.ReleaseUseItem ||
                    observation.ItemAnimation != 0 || observation.ItemTime != 0)
                {
                    if (++_phaseTicks > DeployReadyTimeoutTicks)
                        return Fail("staff never reached a fresh native use edge");
                    return Decision(SummonWhipOutputAction.None, -1, false);
                }
                _staffPulseIssued = true;
                _phase = SummonWhipOutputPhase.ConfirmDeployment;
                _phaseTicks = 0;
                return Decision(SummonWhipOutputAction.PulseStaffUse, -1,
                    true);

            case SummonWhipOutputPhase.ConfirmDeployment:
                if (SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in _route))
                {
                    _phase = SummonWhipOutputPhase.SelectWhip;
                    _phaseTicks = 0;
                    if (observation.SelectedSlot != _route.WhipSlot)
                        return Decision(SummonWhipOutputAction.SelectWhip,
                            _route.WhipSlot, false);
                    _phase = SummonWhipOutputPhase.WhipLoop;
                    return Decision(SummonWhipOutputAction.ReleaseUseItem,
                        -1, false);
                }
                // Item.SetDefaults supplies the 28/36 tick staff animation and
                // 2 tick reuse delay above.  Thirty further ticks are a bounded
                // observation margin, not permission to issue a second summon.
                if (++_phaseTicks > _route.StaffProfile.DefaultUseAnimation +
                    _route.StaffProfile.DefaultReuseDelay + 30)
                    return Fail("staff buff and new owner projectile were not both confirmed");
                return Decision(SummonWhipOutputAction.ReleaseUseItem, -1,
                    false);

            case SummonWhipOutputPhase.SelectWhip:
                if (!SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in _route))
                    return Fail("confirmed minion disappeared before whip control");
                if (observation.SelectedSlot != _route.WhipSlot)
                {
                    if (++_phaseTicks > SelectionTimeoutTicks)
                        return Fail("whip selection did not become observable");
                    return Decision(SummonWhipOutputAction.SelectWhip,
                        _route.WhipSlot, false);
                }
                _phase = SummonWhipOutputPhase.WhipLoop;
                _phaseTicks = 0;
                return Decision(SummonWhipOutputAction.ReleaseUseItem, -1,
                    false);

            case SummonWhipOutputPhase.WhipLoop:
                if (!SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in _route))
                    return Fail("latched minion buff or owner projectile disappeared");
                if (observation.SelectedSlot != _route.WhipSlot)
                    return Decision(SummonWhipOutputAction.SelectWhip,
                        _route.WhipSlot, false);
                if (_releaseWhipPulse)
                {
                    _releaseWhipPulse = false;
                    return Decision(SummonWhipOutputAction.ReleaseUseItem, -1,
                        false);
                }
                if (!attackRequested || !observation.CanUseItem ||
                    !allowItemUse ||
                    !observation.ReleaseUseItem || observation.ItemAnimation != 0 ||
                    observation.ItemTime != 0)
                    return Decision(SummonWhipOutputAction.None, -1, false);
                _releaseWhipPulse = true;
                return Decision(SummonWhipOutputAction.PulseWhipUse, -1,
                    true);

            default:
                return Fail("unrecognized summon/whip output phase");
            }
        }

        private SummonWhipOutputDecision Decision(
            SummonWhipOutputAction action, int slot, bool useItem) =>
            new SummonWhipOutputDecision
            {
                Phase = _phase,
                Action = action,
                SelectSlot = slot,
                UseItem = useItem
            };

        private SummonWhipOutputDecision Fail(string reason)
        {
            _phase = SummonWhipOutputPhase.FailedClosed;
            _failureReason = string.IsNullOrEmpty(reason) ?
                "summon/whip output route failed closed" : reason;
            return FailedDecision();
        }

        private SummonWhipOutputDecision FailedDecision() =>
            new SummonWhipOutputDecision
            {
                Phase = SummonWhipOutputPhase.FailedClosed,
                Action = SummonWhipOutputAction.FailedClosed,
                SelectSlot = -1,
                UseItem = false,
                Failed = true,
                FailureReason = _failureReason
            };
    }
}
