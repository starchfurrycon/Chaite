using System;

namespace Chaite.Core
{
    /// <summary>Pure, allocation-bounded selection of the leftmost currently usable summon.</summary>
    public static class BossStartPlanner
    {
        // Verified vanilla Main.UpdateTime IL_0af6-0b08 ends the night after 32400.
        // The 7200 reserve is CHAITE'S conservative admission policy, not a native
        // summon restriction or a proof that the fight can be won in two minutes.
        // These are world-time units: accelerated time/remix require richer context
        // before they can be treated as a verified wall-clock combat budget.
        private const double NightLength = 32400d;
        private const double NightStartReserve = 7200d;

        public static BossStartPlan Select(BossStartContext context)
        {
            if (context == null)
                return null;

            for (var slot = 0; slot < 10; slot++)
            {
                var item = FindAt(context, slot);
                if (item == null || item.Stack <= 0)
                    continue;
                var plan = FromItem(context, item);
                if (plan != null && !AlreadyActive(context, plan.ExpectedBossType, plan.Id))
                    return plan;
            }

            if (context.SpawnEyeScheduled && HasNightStartWindow(context) && !AlreadyActive(context, 4, null))
                return Natural(BossSummonKind.NaturalEye, 4, "natural-eye", 7200);

            // Main.UpdateTime IL_086b-08a3 blocks a scheduled mechanical spawn while
            // ANY Boss is alive; IL_0930-09c3 only maps values 1, 2 and 3.
            if (context.SpawnHardBoss >= 1 && context.SpawnHardBoss <= 3 &&
                HasNightStartWindow(context) && context.ActiveBossTypes.Count == 0)
            {
                if (context.ZenithWorld && !AlreadyActive(context, 127, "natural-mechdusa"))
                    return Natural(BossSummonKind.NaturalMechanicalBoss, 127, "natural-mechdusa", 7200);
                var type = context.SpawnHardBoss == 1 ? 134 : context.SpawnHardBoss == 2 ? 125 : 127;
                if (!context.ZenithWorld && !AlreadyActive(context, type, null))
                    return Natural(BossSummonKind.NaturalMechanicalBoss, type, "natural-mechanical", 7200);
            }

            if (context.MoonLordCountdown > 0 && !AlreadyActive(context, 398, null))
                return Natural(BossSummonKind.NaturalMoonLord, 398, "natural-moon-lord", context.MoonLordCountdown + 900);

            return null;
        }

        /// <summary>
        /// Production start selector for the current deliberately narrow
        /// release. The generic selector above remains available to offline
        /// strategy fixtures, but a live game must skip every earlier hotbar
        /// summon belonging to an unreviewed Boss rather than accidentally
        /// selecting it before a valid Fishron/Empress item.
        /// </summary>
        public static BossStartPlan SelectProduction(BossStartContext context)
        {
            if (context == null || context.ActiveBossTypes == null ||
                context.ActiveBossTypes.Count != 0)
                return null;

            for (var slot = 0; slot < 10; slot++)
            {
                var item = FindAt(context, slot);
                if (item == null || item.Stack <= 0)
                    continue;
                // Keep the exact biome/time/critters checks in FromItem, but
                // never even construct a plan for another Boss family.
                if (item.Type != 2673 && item.Type != 4961)
                    continue;
                var plan = FromItem(context, item);
                string ignored;
                if (SupportedBossPolicy.TryValidateStartPlan(plan,
                        out ignored))
                    return plan;
            }
            return null;
        }

        private static BossStartPlan FromItem(BossStartContext c, HotbarItemSnapshot item)
        {
            switch (item.Type)
            {
                case 43:
                    return HasNightStartWindow(c) ? Direct(item, 4, "suspicious-eye") : null;
                case 70:
                    return c.ZoneCorrupt ? Direct(item, 13, "worm-food") : null;
                case 544:
                    return HasNightStartWindow(c) ? Direct(item, 125, "mechanical-eye") : null;
                case 556:
                    // The only reviewed Destroyer controller is a single-Boss
                    // profile.  Do not consume the worm into the generic or
                    // mechanical multi-Boss controllers while another Boss is
                    // already alive.
                    return HasNightStartWindow(c) && c.ActiveBossTypes.Count == 0
                        ? Direct(item, 134, "mechanical-worm") : null;
                case 557:
                    return HasNightStartWindow(c) ? Direct(item, 127, "mechanical-skull") : null;
                case 560:
                    return Direct(item, 50, "slime-crown");
                case 1133:
                    return c.ZoneJungle ? Direct(item, 222, "abeemination") : null;
                case 1331:
                    return c.ZoneCrimson ? Direct(item, 266, "bloody-spine") : null;
                case 4988:
                    return c.ZoneHallow ? Direct(item, 657, "queen-slime-crystal") : null;
                case 5120:
                    return c.ZoneSnow ? Direct(item, 668, "deer-thing") : null;
                case 5334:
                    return c.ZenithWorld ? Direct(item, 127, "mechdusa-summon", 480) : null;
                case 3601:
                    return c.HardMode && c.DownedGolemBoss && !c.CultistActive && !c.MysteriousTabletActive &&
                           !c.BlockingInvasion && !c.LunarPillarsActive && c.MoonLordCountdown <= 0
                           && c.ActiveBossTypes.Count == 0
                        ? Direct(item, 398, "celestial-sigil", 1200) : null;
                case 4961:
                    // ShouldEmpressBeEnraged IL_0000-0066: remix/Zenith surface
                    // summons latch rage by HEIGHT, even at night. This surface-only
                    // workflow has no validated enraged/underground start profile;
                    // decline it rather than treating Zenith as a safe day bypass.
                    // Vanilla NPC 661 does not impose a daytime-use ban. Once
                    // released beside the player in surface Hallow it remains
                    // damageable while nearby; ExecuteLacewingStart immediately
                    // uses the already admitted single-slot projectile route.
                    // Daytime therefore selects the same transaction and lets
                    // the stricter lethal-day Empress mobility gate decide
                    // whether taking control is safe.
                    return !c.ZenithWorld && c.ZoneHallow && c.ZoneOverworld &&
                           (c.DayTime || HasNightStartWindow(c)) &&
                           !c.CritterProtection
                        ? Special(BossSummonKind.PrismaticLacewing, item, 636, "prismatic-lacewing", item.Slot, 480, new Vec2()) : null;
                case 1293:
                    return c.NearLihzahrdAltar
                        ? Special(BossSummonKind.LihzahrdAltar, item, 245, "lihzahrd-altar", item.Slot, 360, c.AltarWorld) : null;
                case 2673:
                    return c.ZoneBeach && c.OceanWater && c.FishingRodHotbarSlot >= 0
                        ? Special(BossSummonKind.TruffleWormFishing, item, 370, "truffle-worm-fishing", c.FishingRodHotbarSlot, 900, c.OceanWaterWorld) : null;
                case 267:
                    return c.ZoneUnderworld && c.GuideAlive && c.NearbyLava
                        ? Special(BossSummonKind.GuideVoodooDoll, item, 113, "guide-voodoo-doll", item.Slot, 600, c.LavaWorld) : null;
                default:
                    return null;
            }
        }

        private static bool HasNightStartWindow(BossStartContext context)
        {
            return !context.DayTime && !double.IsNaN(context.Time) && !double.IsInfinity(context.Time) &&
                context.Time >= 0d && context.Time <= NightLength - NightStartReserve;
        }

        private static HotbarItemSnapshot FindAt(BossStartContext context, int slot)
        {
            for (var i = 0; i < context.Hotbar.Count; i++)
                if (context.Hotbar[i].Slot == slot)
                    return context.Hotbar[i];
            return null;
        }

        private static bool AlreadyActive(BossStartContext context, int type, string id)
        {
            foreach (var active in context.ActiveBossTypes)
            {
                if (active == type) return true;
                if (id != null && id.IndexOf("mechdusa", StringComparison.Ordinal) >= 0 &&
                    (MechanicalFamilies.IsTwin(active) || MechanicalFamilies.IsPrime(active) || MechanicalFamilies.IsDestroyer(active))) return true;
                if (type == 125 && MechanicalFamilies.IsTwin(active)) return true;
                if (type == 134 && MechanicalFamilies.IsDestroyer(active)) return true;
                if (type == 127 && MechanicalFamilies.IsPrime(active)) return true;
                if (type == 13 && active >= 13 && active <= 15) return true;
                if (type == 113 && (active == 113 || active == 114)) return true;
                if (type == 245 && active >= 245 && active <= 249) return true;
                if (type == 398 && active >= 396 && active <= 398) return true;
            }
            return false;
        }

        private static BossStartPlan Direct(HotbarItemSnapshot item, int bossType, string id, int timeout = 600)
        {
            return Special(BossSummonKind.DirectItem, item, bossType, id, item.Slot, timeout, new Vec2());
        }

        private static BossStartPlan Special(BossSummonKind kind, HotbarItemSnapshot item, int bossType,
            string id, int actionSlot, int timeout, Vec2 world)
        {
            var plan = new BossStartPlan
            {
                Kind = kind,
                SummonSlot = item.Slot,
                ActionSlot = actionSlot,
                ItemType = item.Type,
                ExpectedBossType = bossType,
                TimeoutTicks = timeout,
                InteractionWorld = world,
                Id = id
            };
            plan.SealPlannerSelection();
            return plan;
        }

        private static BossStartPlan Natural(BossSummonKind kind, int bossType, string id, int timeout)
        {
            var plan = new BossStartPlan
            {
                Kind = kind,
                ExpectedBossType = bossType,
                TimeoutTicks = timeout,
                Id = id
            };
            plan.SealPlannerSelection();
            return plan;
        }
    }
}
