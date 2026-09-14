namespace Chaite.Core
{
    public static class FormulaMobilityContract
    {
        public const int DemonWingsItem = 492;
        public const int LightningBootsItem = 898;
        public const int ShieldOfCthulhuItem = 3097;
        public const int MasterNinjaGearItem = 984;
        public const int FrogLegItem = 2423;
        public static bool IsMobilityAccessory(int type) =>
            type == DemonWingsItem || type == LightningBootsItem ||
            type == ShieldOfCthulhuItem || type == 492 || type == 761 ||
            type == 2609 || type == 984 || type == 4981 || type == 3367 ||
            type == FrogLegItem;
        public static bool TryValidate(CombatSnapshot s, int boss, out string reason)
        {
            FormulaRoute ignored;
            return TrySelectRoute(s, boss, out ignored, out reason);
        }

        public static bool TrySelectRoute(CombatSnapshot s, int boss,
            out FormulaRoute route, out string reason)
        {
            route = FormulaRoute.None;
            if (!TryValidateCommon(s, boss, out reason)) return false;
            var mount = -1;
            if (s.Mobility.MountActive)
            {
                if (!s.Mobility.ActiveMountIdentityKnown)
                { reason = "无法读取活动公式坐骑身份"; return false; }
                mount = s.Mobility.ActiveMountType;
            }
            else if (s.Mobility.SelectedMountIdentityKnown)
                mount = s.Mobility.SelectedMountType;
            var dashIdentity = s.Mobility.EyeShieldDash.EquipmentIdentity;
            var dashItem = dashIdentity ==
                DashEquipmentIdentity.ShieldOfCthulhuItem3097 ? 3097 :
                dashIdentity == DashEquipmentIdentity.MasterNinjaGearItem984
                    ? MasterNinjaGearItem : 0;
            var crystalAssassinSet = dashIdentity ==
                DashEquipmentIdentity.CrystalAssassinArmorSet;
            var frogLeg = s.Mobility.FrogLegAccessoryKnown &&
                s.Mobility.FrogLegAccessoryPresent;
            var raining = s.Difficulty != null && s.Difficulty.RainKnown &&
                s.Difficulty.Rain;
            route = FormulaRouteCatalog.Select(boss,
                s.Player.WingAccessoryItemType, dashItem, crystalAssassinSet,
                frogLeg, mount, raining);
            if (route == FormulaRoute.None)
            {
                reason = "未匹配当前 Boss 的公式翅膀／冲刺或坐骑路线";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool TryValidateLockedRoute(CombatSnapshot s, int boss,
            FormulaRoute route, out string reason)
        {
            if (!TryValidateCommon(s, boss, out reason) ||
                !FormulaRouteCatalog.BelongsToBoss(route, boss))
                return false;
            var expectedMount = ExpectedMount(route);
            if (expectedMount >= 0)
            {
                if (s.Mobility.MountActive)
                {
                    if (!s.Mobility.ActiveMountIdentityKnown ||
                        s.Mobility.ActiveMountType != expectedMount)
                    { reason = "活动坐骑不是战前锁定的公式坐骑"; return false; }
                }
                else if (!s.Mobility.SelectedMountIdentityKnown ||
                         s.Mobility.SelectedMountType != expectedMount)
                { reason = "战前锁定的公式坐骑已改变"; return false; }
                if (route == FormulaRoute.EmpressRainFishron &&
                    (s.Difficulty == null || !s.Difficulty.RainKnown ||
                     !s.Difficulty.Rain))
                { reason = "雨天虾松露公式已失去原生雨天身份"; return false; }
                reason = null;
                return true;
            }
            if (s.Mobility.MountActive)
            { reason = "翼类公式运行时出现坐骑状态"; return false; }
            FormulaRoute selected;
            if (!TrySelectRoute(s, boss, out selected, out reason) ||
                selected != route)
            { reason = "战前锁定的公式机动配置发生变化"; return false; }
            reason = null;
            return true;
        }

        private static bool TryValidateCommon(CombatSnapshot s, int boss,
            out string reason)
        {
            if (s == null || s.Player == null || s.Mobility == null ||
                s.Difficulty == null ||
                !s.Player.FunctionalEquipmentIdentityKnown ||
                !s.Mobility.FormulaAccessoryScanKnown)
            { reason = "无法读取公式机动饰品"; return false; }
            if (boss != 370 && boss != 636)
            { reason = FormulaRouteCatalog.Refusal; return false; }
            if (s.Mobility.UnexpectedFormulaMobilityItemType != 0 ||
                s.Mobility.Grappling || s.Mobility.GravityInverted)
            { reason = "检测到公式外机动状态"; return false; }
            reason = null;
            return true;
        }

        private static int ExpectedMount(FormulaRoute route)
        {
            switch (route)
            {
                case FormulaRoute.FishronQueenSlime: return 50;
                case FormulaRoute.FishronTrustyChillet: return 64;
                case FormulaRoute.FishronTrustyChilletIgnis: return 65;
                case FormulaRoute.EmpressBroom: return 23;
                case FormulaRoute.EmpressRainFishron: return 12;
                default: return -1;
            }
        }

    }
}
