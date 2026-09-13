namespace Chaite.Core
{
    public static class FormulaMobilityContract
    {
        public const int DemonWingsItem = 492;
        public const int LightningBootsItem = 898;
        public static bool TryValidate(CombatSnapshot s, int boss, out string reason)
        {
            if (s == null || s.Player == null || s.Mobility == null ||
                !s.Player.FunctionalEquipmentIdentityKnown ||
                !s.Mobility.FormulaAccessoryScanKnown)
            { reason = "无法读取公式机动饰品"; return false; }
            if (boss != 370 && boss != 636)
            { reason = FormulaRouteCatalog.Refusal; return false; }
            if (s.Player.WingAccessoryItemType != DemonWingsItem ||
                s.Player.RocketBootAccessoryItemType != LightningBootsItem)
            { reason = "公式机动套装必须是恶魔之翼和闪电靴"; return false; }
            if (s.Mobility.UnexpectedFormulaMobilityItemType != 0 ||
                s.Mobility.MountActive || s.Mobility.Grappling ||
                s.Mobility.GravityInverted)
            { reason = "检测到公式外机动状态"; return false; }
            reason = null; return true;
        }
    }
}
