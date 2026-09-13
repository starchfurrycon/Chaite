using System;

namespace Chaite.Core
{
    /// <summary>
    /// The semantic controller selected for one complete damage route.  These
    /// values are deliberately not inferred from Item.shoot or damage class:
    /// every production route must be backed by an explicit native profile.
    /// </summary>
    public enum OutputRouteKind
    {
        Unspecified,
        StraightRanged,
        StraightMagic,
        MinionAndWhip,
        // Owner-anchored/cursor-anchored native melee projectiles (boomerang,
        // spear and yoyo) use a dedicated controller and never the straight
        // intercept route.
        MeleeProjectile,
        // Terra Blade's reviewed ranged wave. The simultaneous owner-anchored
        // swing is declared by CommonWeaponOutputCatalog but not credited.
        DirectedMeleeWave,
        // Eventide's complete five-event fan/convergence animation.
        ConvergingRangedBurst,
        // A source-reviewed magic projectile whose native AI owns target
        // acquisition and curvature; this is deliberately distinct from a
        // straight magic intercept.
        HomingMagicProjectile
    }

    public enum OutputResourceKind
    {
        Unspecified,
        Ammunition,
        Mana,
        MinionSlots,
        // The selected melee projectile consumes no ammunition or mana.
        Melee
    }

    /// <summary>
    /// Immutable identity of the output controller admitted before summoning.
    /// A slot is part of the identity: replacing the item or changing ammunition
    /// must never silently install a different aim/resource model mid-fight.
    /// </summary>
    public struct OutputRouteProfile
    {
        public string Id;
        public OutputRouteKind Kind;
        public OutputResourceKind Resource;
        public int WeaponSlot;
        public int WeaponId;
        public int AmmoId;
        public int ProjectileId;
        public WeaponProfile Profile;

        public bool IsSpecified => !string.IsNullOrEmpty(Id) &&
            Kind != OutputRouteKind.Unspecified &&
            Resource != OutputResourceKind.Unspecified &&
            WeaponSlot >= 0 && WeaponSlot < 10 && WeaponId > 0 &&
            ProjectileId > 0 && Profile != null;
    }

    public static class OutputRouteContract
    {
        public static bool TryCreateReady(CombatSnapshot snapshot,
            out OutputRouteProfile route, out string reason)
        {
            route = default(OutputRouteProfile);
            var weapon = snapshot?.Weapon;
            if (weapon == null || !weapon.NativeProfileRequired)
            {
                reason = "native output profile is unavailable";
                return false;
            }
            if (!weapon.IsUsable || !weapon.IsProjectile)
            {
                reason = "selected item is not an admitted projectile weapon";
                return false;
            }
            if (weapon.Profile.Status != WeaponProfileStatus.Supported ||
                weapon.Profile.Profile == null)
            {
                reason = weapon.Profile.Reason;
                return false;
            }
            var profile = weapon.Profile.Profile;
            if (profile.ResourceKind == OutputResourceKind.Ammunition &&
                !weapon.HasAmmo)
            {
                reason = "the selected output route has no usable ammunition";
                return false;
            }
            if (profile.ResourceKind == OutputResourceKind.Mana &&
                (ManaOutputController.Decide(in weapon.Mana, false).Status !=
                    ManaOutputStatus.Ready ||
                 weapon.Mana.ManaCostPerUse !=
                    weapon.Profile.ManaCostPerUse))
            {
                reason = "live mana state or effective spell cost is unavailable";
                return false;
            }
            if (weapon.Slot < 0 || weapon.Slot >= 10 ||
                weapon.WeaponId != profile.Key.WeaponId ||
                weapon.AmmoId != profile.Key.AmmoId ||
                weapon.ProjectileId != profile.ProjectileId ||
                !FinitePositive(weapon.ApproximateDps))
            {
                reason = "live weapon identity or output measurements do not match the reviewed profile";
                return false;
            }

            route = new OutputRouteProfile
            {
                Id = profile.OutputRouteId,
                Kind = profile.OutputKind,
                Resource = profile.ResourceKind,
                WeaponSlot = weapon.Slot,
                WeaponId = weapon.WeaponId,
                AmmoId = weapon.AmmoId,
                ProjectileId = weapon.ProjectileId,
                Profile = profile
            };
            if (!route.IsSpecified)
            {
                route = default(OutputRouteProfile);
                reason = "reviewed output profile has an incomplete route contract";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool ValidateLive(CombatSnapshot snapshot,
            in OutputRouteProfile expected, float minimumEffectiveDps,
            out string reason)
        {
            if (!expected.IsSpecified || !FiniteNonnegative(minimumEffectiveDps))
            {
                reason = "latched output route is incomplete";
                return false;
            }
            OutputRouteProfile current;
            if (!TryCreateReady(snapshot, out current, out reason))
                return false;
            if (current.WeaponSlot != expected.WeaponSlot ||
                current.WeaponId != expected.WeaponId ||
                current.AmmoId != expected.AmmoId ||
                current.ProjectileId != expected.ProjectileId ||
                current.Kind != expected.Kind || current.Resource != expected.Resource ||
                !ReferenceEquals(current.Profile, expected.Profile) ||
                !string.Equals(current.Id, expected.Id, StringComparison.Ordinal))
            {
                reason = "weapon, ammunition, projectile, or controller identity changed after output-route admission";
                return false;
            }
            var thresholdDps = snapshot.Weapon.ApproximateDps;
            // Quick-mana applies vanilla Mana Sickness after the route has
            // already been admitted. That temporary, deterministic penalty
            // must not look like an equipment downgrade and tear down the
            // controller. Undo only the exact live native multiplier; all
            // other damage changes still participate in the threshold check.
            if (expected.Resource == OutputResourceKind.Mana &&
                snapshot.Weapon.Mana.ManaSicknessKnown &&
                snapshot.Weapon.Mana.ManaSicknessReduction > 0f)
            {
                var multiplier = 1f -
                    snapshot.Weapon.Mana.ManaSicknessReduction;
                if (multiplier > 0f)
                    thresholdDps /= multiplier;
            }
            if (thresholdDps < minimumEffectiveDps)
            {
                reason = "live conservative output fell below the admitted Boss threshold";
                return false;
            }
            reason = null;
            return true;
        }

        private static bool FinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteNonnegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum ManaOutputStatus
    {
        Unknown,
        Ready,
        UseQuickMana,
        WaitForRegeneration,
        InvalidState
    }

    /// <summary>Read-only native mana facts used by a magic-weapon controller.</summary>
    public struct ManaOutputState
    {
        public bool Known;
        public int CurrentMana;
        public int MaximumMana;
        public int ManaCostPerUse;
        public float RegenerationDelay;
        public int RegenerationCount;
        public int RegenerationRate;
        public int PotionDelay;
        // QuickMana is release-edge gated by vanilla. A held input after a
        // failed attempt cannot retrigger it, so the planner must emit a real
        // release frame before another pulse.
        public bool QuickManaAutomationAllowed;
        public bool QuickManaReleaseReady;
        public bool QuickManaUsableNow;
        public bool QuickManaItemAvailable;
        public int QuickManaHeal;
        // Player.GetWeaponDamage already contains this multiplier. Preserve
        // the exact native value separately so live route validation can
        // distinguish a potion's temporary penalty from removed equipment.
        public bool ManaSicknessKnown;
        public float ManaSicknessReduction;
    }

    public struct ManaOutputDecision
    {
        public ManaOutputStatus Status;
        public bool Fire;
        public bool QuickMana;
    }

    /// <summary>
    /// Constant-time resource FSM for an already reviewed magic route.  It
    /// never invokes native mana checks or consumes an item itself.  A caller
    /// may emit exactly one input: cast, quick-mana, or neither.
    /// </summary>
    public static class ManaOutputController
    {
        public static ManaOutputDecision Decide(in ManaOutputState state,
            bool attackRequested)
        {
            var result = new ManaOutputDecision();
            if (!Valid(in state))
            {
                result.Status = state.Known ? ManaOutputStatus.InvalidState :
                    ManaOutputStatus.Unknown;
                return result;
            }
            if (!attackRequested)
            {
                result.Status = ManaOutputStatus.Ready;
                return result;
            }
            if (state.ManaCostPerUse == 0 ||
                state.CurrentMana >= state.ManaCostPerUse)
            {
                result.Status = ManaOutputStatus.Ready;
                result.Fire = true;
                return result;
            }
            if (state.QuickManaAutomationAllowed &&
                state.QuickManaReleaseReady && state.QuickManaUsableNow &&
                state.PotionDelay == 0 && state.QuickManaItemAvailable &&
                state.QuickManaHeal > 0 &&
                Math.Min(state.MaximumMana,
                    state.CurrentMana + state.QuickManaHeal) >=
                    state.ManaCostPerUse)
            {
                result.Status = ManaOutputStatus.UseQuickMana;
                result.QuickMana = true;
                return result;
            }
            result.Status = ManaOutputStatus.WaitForRegeneration;
            return result;
        }

        private static bool Valid(in ManaOutputState value)
        {
            return value.Known && value.MaximumMana > 0 &&
                value.CurrentMana >= 0 && value.CurrentMana <= value.MaximumMana &&
                value.ManaCostPerUse >= 0 && value.ManaCostPerUse <= value.MaximumMana &&
                !float.IsNaN(value.RegenerationDelay) &&
                !float.IsInfinity(value.RegenerationDelay) &&
                value.RegenerationDelay >= 0f && value.RegenerationCount >= 0 &&
                value.RegenerationRate >= 0 && value.PotionDelay >= 0 &&
                value.QuickManaHeal >= 0 && value.ManaSicknessKnown &&
                !float.IsNaN(value.ManaSicknessReduction) &&
                !float.IsInfinity(value.ManaSicknessReduction) &&
                value.ManaSicknessReduction >= 0f &&
                value.ManaSicknessReduction < 1f;
        }
    }
}
