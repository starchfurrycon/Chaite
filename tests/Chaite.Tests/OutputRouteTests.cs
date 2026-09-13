using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunOutputRouteRegressions()
        {
            Run(nameof(OutputRouteRequiresExactReviewedIdentity),
                OutputRouteRequiresExactReviewedIdentity);
            Run(nameof(OutputRouteIsLatchedAcrossBossArrival),
                OutputRouteIsLatchedAcrossBossArrival);
            Run(nameof(OutputRouteRejectsMidFightWeaponOrAmmoSwap),
                OutputRouteRejectsMidFightWeaponOrAmmoSwap);
            Run(nameof(ActiveBossAdmissionStartsWithLatchedRecoveryRoute),
                ActiveBossAdmissionStartsWithLatchedRecoveryRoute);
            Run(nameof(RangedAndLegacyRoutesNeverRequestQuickMana),
                RangedAndLegacyRoutesNeverRequestQuickMana);
            Run(nameof(ManaControllerChoosesExactlyOneResourceAction),
                ManaControllerChoosesExactlyOneResourceAction);
            Run(nameof(ManaControllerFailsClosedOnUnknownOrInvalidState),
                ManaControllerFailsClosedOnUnknownOrInvalidState);
            Run(nameof(MagicOutputRouteUsesManaControllerDuringCombat),
                MagicOutputRouteUsesManaControllerDuringCombat);
            Run(nameof(MagicOutputRouteRejectsUnknownManaState),
                MagicOutputRouteRejectsUnknownManaState);
            Run(nameof(ManaControllerRequiresFreshQuickManaEdgeAndPermission),
                ManaControllerRequiresFreshQuickManaEdgeAndPermission);
            Run(nameof(ManaSicknessDoesNotInvalidateLatchedMagicRoute),
                ManaSicknessDoesNotInvalidateLatchedMagicRoute);
        }

        private static CombatSnapshot NativeGunScenario(int weaponId = 98,
            int ammoId = 97)
        {
            var snapshot = CombatScenario(4);
            var evaluation = WeaponProfileCatalog.Evaluate(
                ProfileInput(weaponId, ammoId));
            snapshot.Weapon = new WeaponSnapshot
            {
                Slot = 2,
                Damage = evaluation.DirectDamage,
                UseTime = weaponId == 98 ? 8 : 4,
                ShootSpeed = evaluation.SpeedPixelsPerTick,
                IsProjectile = true,
                HasAmmo = true,
                IsUsable = true,
                NativeProfileRequired = true,
                WeaponId = weaponId,
                AmmoId = ammoId,
                ProjectileId = evaluation.Profile.ProjectileId,
                Profile = evaluation
            };
            return snapshot;
        }

        private static void OutputRouteRequiresExactReviewedIdentity()
        {
            var snapshot = NativeGunScenario();
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason));
            True(reason == null && route.IsSpecified);
            Equal(OutputRouteKind.StraightRanged, route.Kind);
            Equal(OutputResourceKind.Ammunition, route.Resource);
            Equal(2, route.WeaponSlot);
            Equal(98, route.WeaponId);
            Equal(97, route.AmmoId);
            Equal(14, route.ProjectileId);

            snapshot.Weapon.ProjectileId = 89;
            False(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason));
            True(!string.IsNullOrEmpty(reason));
            snapshot = NativeGunScenario();
            snapshot.Weapon.Profile.ApproximateDirectDps = float.NaN;
            False(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason));
        }

        private static void OutputRouteIsLatchedAcrossBossArrival()
        {
            var snapshot = NativeGunScenario();
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(snapshot, "eye", 4,
                out reason), reason);
            Equal(2, planner.LatchedOutputSlot);
            Equal(98, planner.LatchedOutputWeaponId);
            planner.ResetForBossArrival();
            Equal(2, planner.LatchedOutputSlot);
            var plan = planner.Plan(snapshot);
            False(plan.RequestControlReturn);
            Equal(OutputRouteKind.StraightRanged, plan.OutputRouteKind);
            Equal(98, plan.ExpectedWeaponId);
            Equal(97, plan.ExpectedAmmoId);
            Equal(14, plan.ExpectedProjectileId);
            Equal(2, plan.PreferredWeaponSlot);
        }

        private static void OutputRouteRejectsMidFightWeaponOrAmmoSwap()
        {
            foreach (var change in new[] { 0, 1, 2, 3 })
            {
                var snapshot = NativeGunScenario();
                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                True(planner.PrepareForExpectedEncounter(snapshot, "eye", 4,
                    out reason), reason);
                if (change == 0) snapshot.Weapon.Slot = 3;
                if (change == 1) snapshot.Weapon.WeaponId = 434;
                if (change == 2) snapshot.Weapon.AmmoId = 515;
                if (change == 3) snapshot.Weapon.ProjectileId = 89;
                var plan = planner.Plan(snapshot);
                True(plan.RequestControlReturn);
                False(plan.Fire || plan.QuickMana || plan.Jump || plan.Dash ||
                    plan.Hook || plan.ToggleMount);
                True(!string.IsNullOrEmpty(plan.ControlReturnReason));
            }
        }

        private static void ActiveBossAdmissionStartsWithLatchedRecoveryRoute()
        {
            var snapshot = NativeGunScenario();
            var planner = new CombatPlanner(new PlannerSettings
            {
                RecoveryTicks = 20
            });
            string reason;
            True(planner.PrepareForActiveEncounter(snapshot, out reason), reason);
            Equal(2, planner.LatchedOutputSlot);
            var plan = planner.Plan(snapshot);
            False(plan.RequestControlReturn);
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            Equal(2, plan.PreferredWeaponSlot);
        }

        private static void RangedAndLegacyRoutesNeverRequestQuickMana()
        {
            var native = NativeGunScenario();
            native.Player.Mana = 0;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(native, "eye", 4,
                out reason), reason);
            False(planner.Plan(native).QuickMana);

            var legacy = CombatScenario(4);
            legacy.Player.Mana = 0;
            False(new CombatPlanner(new PlannerSettings()).Plan(legacy).QuickMana);
        }

        private static ManaOutputState ManaState() => new ManaOutputState
        {
            Known = true,
            CurrentMana = 40,
            MaximumMana = 200,
            ManaCostPerUse = 12,
            RegenerationDelay = 10f,
            RegenerationCount = 0,
            RegenerationRate = 4,
            PotionDelay = 0,
            QuickManaAutomationAllowed = true,
            QuickManaReleaseReady = true,
            QuickManaUsableNow = true,
            QuickManaItemAvailable = true,
            QuickManaHeal = 100,
            ManaSicknessKnown = true,
            ManaSicknessReduction = 0f
        };

        private static CombatSnapshot MagicScenario()
        {
            var snapshot = CombatScenario(4);
            var input = new WeaponProfileInput
            {
                WeaponId = 127,
                AmmoId = 0,
                ProjectileId = 20,
                ProjectileExtraUpdates = 2,
                ProjectileLifetimeSubupdates = 600,
                WeaponShootSpeed = 10f,
                WeaponDamageAfterModifiers = 20,
                UseTime = 17,
                UseAnimation = 17,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = 6
            };
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            var mana = ManaState();
            mana.ManaCostPerUse = 6;
            snapshot.Player.Mana = mana.CurrentMana;
            snapshot.Player.MaxMana = mana.MaximumMana;
            snapshot.Weapon = new WeaponSnapshot
            {
                Slot = 1,
                Damage = evaluation.DirectDamage,
                UseTime = 17,
                ShootSpeed = evaluation.SpeedPixelsPerTick,
                IsProjectile = true,
                IsMelee = false,
                HasAmmo = true,
                IsUsable = true,
                NativeProfileRequired = true,
                WeaponId = 127,
                AmmoId = 0,
                ProjectileId = 20,
                Profile = evaluation,
                Mana = mana
            };
            return snapshot;
        }

        private static void MagicOutputRouteUsesManaControllerDuringCombat()
        {
            var snapshot = MagicScenario();
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(snapshot, "eye", 4,
                out reason), reason);

            var enough = planner.Plan(snapshot);
            Equal(OutputRouteKind.StraightMagic, enough.OutputRouteKind);
            True(enough.Fire);
            False(enough.QuickMana);

            var mana = snapshot.Weapon.Mana;
            mana.CurrentMana = 0;
            snapshot.Weapon.Mana = mana;
            snapshot.Player.Mana = 0;
            var refill = planner.Plan(snapshot);
            False(refill.Fire);
            True(refill.QuickMana);

            mana.PotionDelay = 60;
            snapshot.Weapon.Mana = mana;
            var wait = planner.Plan(snapshot);
            False(wait.Fire || wait.QuickMana);
        }

        private static void MagicOutputRouteRejectsUnknownManaState()
        {
            var snapshot = MagicScenario();
            var mana = snapshot.Weapon.Mana;
            mana.Known = false;
            snapshot.Weapon.Mana = mana;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            False(planner.PrepareForExpectedEncounter(snapshot, "eye", 4,
                out reason));
            True(reason.Contains("mana"));
        }

        private static void ManaControllerChoosesExactlyOneResourceAction()
        {
            var state = ManaState();
            var decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.Ready, decision.Status);
            True(decision.Fire);
            False(decision.QuickMana);

            state.CurrentMana = 4;
            decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.UseQuickMana, decision.Status);
            False(decision.Fire);
            True(decision.QuickMana);

            state.PotionDelay = 30;
            decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.WaitForRegeneration, decision.Status);
            False(decision.Fire || decision.QuickMana);

            state.ManaCostPerUse = 0;
            decision = ManaOutputController.Decide(in state, true);
            True(decision.Fire);
            False(decision.QuickMana);
            decision = ManaOutputController.Decide(in state, false);
            False(decision.Fire || decision.QuickMana);
        }

        private static void ManaControllerFailsClosedOnUnknownOrInvalidState()
        {
            var state = ManaState();
            state.Known = false;
            var decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.Unknown, decision.Status);
            False(decision.Fire || decision.QuickMana);

            state = ManaState();
            state.CurrentMana = -1;
            decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.InvalidState, decision.Status);
            False(decision.Fire || decision.QuickMana);

            state = ManaState();
            state.RegenerationDelay = float.NaN;
            decision = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.InvalidState, decision.Status);
        }

        private static void ManaControllerRequiresFreshQuickManaEdgeAndPermission()
        {
            var state = ManaState();
            state.CurrentMana = 0;

            state.QuickManaReleaseReady = false;
            var held = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.WaitForRegeneration, held.Status);
            False(held.Fire || held.QuickMana);

            state.QuickManaReleaseReady = true;
            state.QuickManaAutomationAllowed = false;
            var disabled = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.WaitForRegeneration, disabled.Status);
            False(disabled.Fire || disabled.QuickMana);

            state.QuickManaAutomationAllowed = true;
            state.QuickManaUsableNow = false;
            var blocked = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.WaitForRegeneration, blocked.Status);
            False(blocked.Fire || blocked.QuickMana);

            state.QuickManaUsableNow = true;
            var fresh = ManaOutputController.Decide(in state, true);
            Equal(ManaOutputStatus.UseQuickMana, fresh.Status);
            True(fresh.QuickMana);
            False(fresh.Fire);
        }

        private static void ManaSicknessDoesNotInvalidateLatchedMagicRoute()
        {
            var snapshot = MagicScenario();
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason), reason);
            var admitted = snapshot.Weapon.ApproximateDps;

            var profile = snapshot.Weapon.Profile;
            profile.ApproximateDirectDps = admitted * .75f;
            snapshot.Weapon.Profile = profile;
            var mana = snapshot.Weapon.Mana;
            mana.ManaSicknessReduction = .25f;
            snapshot.Weapon.Mana = mana;
            True(OutputRouteContract.ValidateLive(snapshot, in route,
                admitted * .99f, out reason), reason);

            mana.ManaSicknessReduction = 0f;
            snapshot.Weapon.Mana = mana;
            False(OutputRouteContract.ValidateLive(snapshot, in route,
                admitted * .99f, out reason));
            True(reason.Contains("threshold"));
        }
    }
}
