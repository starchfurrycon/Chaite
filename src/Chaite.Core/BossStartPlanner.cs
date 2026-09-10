using System;

namespace Chaite.Core
{
    /// <summary>Pure, allocation-bounded selection of the leftmost currently usable summon.</summary>
    public static class BossStartPlanner
    {
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

            if (context.SpawnEyeScheduled && !context.DayTime && !AlreadyActive(context, 4, null))
                return Natural(BossSummonKind.NaturalEye, 4, "natural-eye", 7200);

            if (context.SpawnHardBoss > 0 && !context.DayTime)
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

        private static BossStartPlan FromItem(BossStartContext c, HotbarItemSnapshot item)
        {
            switch (item.Type)
            {
                case 43:
                    return !c.DayTime ? Direct(item, 4, "suspicious-eye") : null;
                case 70:
                    return c.ZoneCorrupt ? Direct(item, 13, "worm-food") : null;
                case 544:
                    return !c.DayTime ? Direct(item, 125, "mechanical-eye") : null;
                case 556:
                    return !c.DayTime ? Direct(item, 134, "mechanical-worm") : null;
                case 557:
                    return !c.DayTime ? Direct(item, 127, "mechanical-skull") : null;
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
                    return c.ZoneHallow && c.ZoneOverworld && (!c.DayTime || c.ZenithWorld) && !c.CritterProtection
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
            return new BossStartPlan
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
        }

        private static BossStartPlan Natural(BossSummonKind kind, int bossType, string id, int timeout)
        {
            return new BossStartPlan
            {
                Kind = kind,
                ExpectedBossType = bossType,
                TimeoutTicks = timeout,
                Id = id
            };
        }
    }
}
