namespace Chaite.Core
{
    public static class FormulaMobilityContract
    {
        public const int DemonWingsItem = 492;
        public const int LightningBootsItem = 898;
        public const int ShieldOfCthulhuItem = 3097;
        public const int MasterNinjaGearItem = 984;
        public const int FrogLegItem = 2423;
        public const int AmphibianBootsItem = 3990;
        /// <summary>The reviewed "frog boots" source: Amphibian Boots are the
        /// vanilla upgrade of a Frog Leg, so both satisfy the same route
        /// identity. Neither is treated as a second, unrelated mobility item.</summary>
        public static bool IsFrogSourceItem(int type) =>
            type == FrogLegItem || type == AmphibianBootsItem;
        public static bool IsMobilityAccessory(int type) =>
            type == DemonWingsItem || type == LightningBootsItem ||
            type == ShieldOfCthulhuItem || type == 492 || type == 761 ||
            type == 2609 || type == 984 || type == 4981 || type == 3367 ||
            IsFrogSourceItem(type);
        public static bool TryValidate(CombatSnapshot s, int boss, out string reason)
        {
            FormulaRoute ignored;
            return TrySelectRoute(s, boss, out ignored, out reason);
        }

        public static bool TrySelectRoute(CombatSnapshot s, int boss,
            out FormulaRoute route, out string reason) =>
            TrySelectRoute(s, boss, true, out route, out reason);

        /// <summary>admission is false when the route identity is merely being
        /// re-derived for an already locked route: the stock gate has already
        /// been satisfied once, and the circuit drinks from that stock, so
        /// applying it again would cancel the run it provisioned.</summary>
        private static bool TrySelectRoute(CombatSnapshot s, int boss,
            bool admission, out FormulaRoute route, out string reason)
        {
            route = FormulaRoute.None;
            if (!TryValidateCommon(s, boss, out reason)) return false;
            if (admission && !TryValidateBubbleClearance(s, boss, out reason))
                return false;
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
            // Readability, unlike the count, is re-asserted every tick: a
            // facade that stops reporting the stock cannot be trusted to keep
            // the ring up, so the route fails closed even though its stock was
            // sufficient at admission.
            if (boss == SupportedBossPolicy.DukeFishronType &&
                !s.Mobility.InfernoPotionStockKnown)
            { reason = "无法读取地狱药水库存"; return false; }
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
            if (!TrySelectRoute(s, boss, false, out selected, out reason) ||
                selected != route)
            { reason = "战前锁定的公式机动配置发生变化"; return false; }
            reason = null;
            return true;
        }

        /// <summary>Reviewed bubble clearance. The Detonating Bubbles home hard
        /// enough that movement cannot clear them, so without the ring the hit
        /// count is decided by luck rather than by the circuit and no wing
        /// route can reach zero hits. This is checked when a route is chosen,
        /// which is the only point at which the carried stock is still whole.
        /// </summary>
        private static bool TryValidateBubbleClearance(CombatSnapshot s,
            int boss, out string reason)
        {
            reason = null;
            if (boss != SupportedBossPolicy.DukeFishronType) return true;
            if (!s.Mobility.InfernoPotionStockKnown)
            { reason = "无法读取地狱药水库存"; return false; }
            if (!FishronThreatCatalog.HasSufficientInfernoStock(
                    s.Mobility.InfernoPotionStock))
            {
                reason = "猪鲨公式需要携带至少 " +
                    FishronThreatCatalog.RequiredInfernoPotionStock +
                    " 瓶地狱药水用于清泡";
                return false;
            }
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
