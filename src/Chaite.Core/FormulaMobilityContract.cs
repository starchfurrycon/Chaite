namespace Chaite.Core
{
    public static class FormulaMobilityContract
    {
        public const int DemonWingsItem = 492;
        public const int LightningBootsItem = 898;
        public const int ShieldOfCthulhuItem = 3097;
        public static bool IsMobilityAccessory(int type) =>
            type == DemonWingsItem || type == LightningBootsItem ||
            type == ShieldOfCthulhuItem || type == 492 || type == 761 ||
            type == 2609 || type == 984 || type == 4981 || type == 3367;
        public static bool TryValidate(CombatSnapshot s, int boss, out string reason)
        {
            if (s == null || s.Player == null || s.Mobility == null ||
                !s.Player.FunctionalEquipmentIdentityKnown ||
                !s.Mobility.FormulaAccessoryScanKnown)
            { reason = "无法读取公式机动饰品"; return false; }
            if (boss != 370 && boss != 636)
            { reason = FormulaRouteCatalog.Refusal; return false; }
            var mount = s.Mobility.SelectedMountIdentityKnown
                ? s.Mobility.SelectedMountType : -1;
            var fishronMount = boss == 370 &&
                (mount == 50 || mount == 64 || mount == 65);
            var empressMount = boss == 636 && mount == 23;
            var wingRoute = IsStrongWing(s.Player.WingAccessoryItemType) &&
                s.Player.RocketBootAccessoryItemType == LightningBootsItem &&
                s.Mobility.EyeShieldDash.EquipmentIdentity ==
                    DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            if (!fishronMount && !empressMount && !wingRoute)
            { reason = "公式机动套装必须是恶魔之翼和闪电靴"; return false; }
            if (s.Mobility.UnexpectedFormulaMobilityItemType != 0 ||
                s.Mobility.MountActive || s.Mobility.Grappling ||
                s.Mobility.GravityInverted)
            { reason = "检测到公式外机动状态"; return false; }
            reason = null; return true;
        }

        private static bool IsStrongWing(int type) =>
            type == 761 || type == 2280 || type == 2609 || type == 3468 ||
            type == 3469 || type == 3470 || type == 3471 || type == 3883 ||
            type == 4823 || type == 492 || type == 493;
    }
}
