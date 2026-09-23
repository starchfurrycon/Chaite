using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Production admission scope for the native controller. The strategy
    /// catalog intentionally keeps the other vanilla strategies for offline
    /// research and regression fixtures, but the shipped runtime must fail
    /// closed unless the active encounter is exactly Duke Fishron.
    /// </summary>
    public static class SupportedBossPolicy
    {
        public const int DukeFishronType = 370;

        // The user supplied meme clip is mapped by AudioCuePlayer. Keeping the
        // text here avoids coupling Core to System.Media or the plugin.
        public const string UnsupportedBossMessage =
            "\u8FD9\u4E2A\u6CE2\u65AF\u53EF\u662F\u8D85\u56CA\u7684\u5BF9\u6211\u6765\u8BF4";

        // Vanilla boss-summon item identities currently recognized by the
        // generic start planner. This is diagnostic only; no item is consumed.
        public static bool IsKnownBossSummonItem(int itemType)
        {
            switch (itemType)
            {
                case 43:   // Suspicious Looking Eye
                case 70:   // Worm Food
                case 267:  // Guide Voodoo Doll
                case 544:  // Mechanical Eye
                case 556:  // Mechanical Worm
                case 557:  // Mechanical Skull
                case 560:  // Slime Crown
                case 1133: // Abeemination
                case 1293: // Lihzahrd Power Cell
                case 1331: // Bloody Spine
                case 2673: // Truffle Worm
                case 3601: // Celestial Sigil
                case 4988: // Gelatin Crystal
                case 5120: // Deer Thing
                case 5334: // Ocram's Razor
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSupportedBossType(int type)
        {
            return type == DukeFishronType;
        }

        public static bool IsSupportedExpectedBoss(int type)
        {
            return IsSupportedBossType(type);
        }

        /// <summary>
        /// Mirrors the production strategy engine's definition of an active
        /// Boss root. Eater of Worlds heads do not normally set NPC.boss, so
        /// type 13 must remain visible to the allowlist on that native edge.
        /// The one supported root is also retained if its native boss flag
        /// is stale for one frame.
        /// </summary>
        public static bool IsEncounterBossRoot(int type, bool nativeBossFlag)
        {
            return nativeBossFlag || type == 13 || IsSupportedBossType(type);
        }

        /// <summary>
        /// Legacy/permissive set check. It accepts repeated observations of
        /// one root, but rejects an unknown or mixed root. Production should
        /// use TryValidateActiveBossTypes when it has the authoritative list.
        /// </summary>
        public static bool IsSupportedBossSet(IList<int> types)
        {
            if (types == null || types.Count == 0)
                return false;
            var selected = 0;
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                if (!IsSupportedBossType(type))
                    return false;
                if (selected == 0) selected = type;
                else if (selected != type) return false;
            }
            return selected != 0;
        }

        /// <summary>
        /// Strict production validation for the authoritative active root list
        /// emitted by the native facade. A duplicate root is rejected: the
        /// caller has not proven that only one Boss instance is present.
        /// </summary>
        public static bool TryValidateActiveBossTypes(IList<int> types,
            out int supportedType, out string reason)
        {
            supportedType = 0;
            reason = null;
            if (types == null || types.Count == 0)
            {
                reason = "active Boss identities are unavailable";
                return false;
            }

            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                if (!IsSupportedBossType(type))
                {
                    reason = UnsupportedReason(type);
                    return false;
                }
                if (supportedType == 0)
                {
                    supportedType = type;
                    continue;
                }
                reason = supportedType == type
                    ? UnsupportedReason(type) + " (duplicate Boss root)"
                    : UnsupportedBossMessage + " (multiple Boss roots)";
                return false;
            }
            return supportedType != 0;
        }

        /// <summary>
        /// Validates the facade's parallel root-key/type observations. There
        /// must be exactly one distinct native root and exactly one matching
        /// type entry; this prevents two instances of the same supported Boss
        /// from being collapsed into one type-only observation.
        /// </summary>
        public static bool TryValidateActiveBossIdentities(IList<int> keys,
            IList<int> generations, IList<int> types,
            out int supportedType, out string reason)
        {
            supportedType = 0;
            reason = null;
            if (keys == null || generations == null || types == null ||
                keys.Count != generations.Count || keys.Count != types.Count)
            {
                reason = "active Boss root identities are inconsistent";
                return false;
            }
            if (keys.Count != 1)
            {
                reason = keys.Count == 0
                    ? "active Boss identities are unavailable"
                    : UnsupportedBossMessage + " (multiple Boss roots)";
                return false;
            }
            if (keys[0] < 0 || generations[0] <= byte.MinValue ||
                generations[0] > byte.MaxValue)
            {
                reason = "active Boss root identity is invalid";
                return false;
            }
            return TryValidateActiveBossTypes(types, out supportedType,
                out reason);
        }

        /// <summary>
        /// Strict target-list equivalent for Core-only callers. Production
        /// should prefer the type-list overload because a broadphase can omit
        /// an otherwise active Boss root.
        /// </summary>
        public static bool TryValidateActiveTargets(
            IList<TargetSnapshot> targets, out int supportedType, out string reason)
        {
            supportedType = 0;
            reason = null;
            if (targets == null)
            {
                reason = "active Boss target list is unavailable";
                return false;
            }

            var roots = 0;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Life <= 0 || !IsEncounterBossRoot(target.Type,
                        target.Boss))
                    continue;
                roots++;
                if (!IsSupportedBossType(target.Type))
                {
                    reason = UnsupportedReason(target.Type);
                    return false;
                }
                if (supportedType == 0)
                    supportedType = target.Type;
                else if (supportedType != target.Type)
                {
                    reason = UnsupportedBossMessage + " (multiple Boss roots)";
                    return false;
                }
            }

            if (roots == 0)
            {
                reason = "no verifiable active Boss root";
                return false;
            }
            if (roots != 1)
            {
                reason = UnsupportedBossMessage + " (multiple Boss roots)";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks that a pre-summon plan uses the one reviewed native summon
        /// flow. Matching only ExpectedBossType is not enough: a forged
        /// DirectItem plan must not bypass special summon handling.
        /// </summary>
        public static bool TryValidateStartPlan(BossStartPlan plan,
            out string reason)
        {
            reason = null;
            if (plan == null)
            {
                reason = "no supported Boss summon plan";
                return false;
            }
            if (!plan.MatchesPlannerSelection())
            {
                reason = UnsupportedBossMessage +
                    " (summon route was not produced unchanged by the planner)";
                return false;
            }

            if (plan.ExpectedBossType == DukeFishronType &&
                plan.Kind == BossSummonKind.TruffleWormFishing &&
                plan.ItemType == 2673 &&
                string.Equals(plan.Id, "truffle-worm-fishing",
                    StringComparison.Ordinal))
            {
                if (IsHotbarSlot(plan.SummonSlot) &&
                    IsHotbarSlot(plan.ActionSlot) &&
                    plan.SummonSlot != plan.ActionSlot &&
                    plan.TimeoutTicks == 900 &&
                    IsFinitePositiveWorld(plan.InteractionWorld) &&
                    IsOptionalHotbarSlot(plan.CombatWeaponSlot))
                    return true;
                reason = UnsupportedBossMessage +
                    " (Fishron summon route fields are not canonical)";
                return false;
            }

            reason = UnsupportedBossMessage + " (summon route is not reviewed)";
            return false;
        }

        private static bool IsHotbarSlot(int slot)
        {
            return slot >= 0 && slot < 10;
        }

        private static bool IsOptionalHotbarSlot(int slot)
        {
            return slot == -1 || IsHotbarSlot(slot);
        }

        private static bool IsFinitePositiveWorld(Vec2 world)
        {
            return world.X > 0f && world.Y > 0f &&
                !float.IsNaN(world.X) && !float.IsInfinity(world.X) &&
                !float.IsNaN(world.Y) && !float.IsInfinity(world.Y);
        }

        public static bool TryValidateExpectedType(int type, out string reason)
        {
            if (IsSupportedExpectedBoss(type))
            {
                reason = null;
                return true;
            }
            reason = UnsupportedReason(type);
            return false;
        }

        /// <summary>
        /// Production snapshots mark themselves with NativeContextKnown. The
        /// helper ignores ordinary hostile NPCs and examines active Boss roots
        /// (plus the exact Fishron ID to protect a stale Boss flag edge).
        /// This method remains permissive for repeated observations; strict
        /// callers should use TryValidateActiveTargets.
        /// </summary>
        public static bool IsSupportedNativeSnapshot(CombatSnapshot snapshot,
            out string reason)
        {
            reason = null;
            if (snapshot == null || !snapshot.NativeContextKnown ||
                snapshot.Targets == null)
            {
                reason = "production native Boss context is unavailable";
                return false;
            }
            int ignored;
            return TryValidateActiveTargets(snapshot.Targets, out ignored,
                out reason);
        }

        public static string UnsupportedReason(int type)
        {
            return UnsupportedBossMessage + " (Boss type " + type +
                " is outside the production scope)";
        }
    }
}
