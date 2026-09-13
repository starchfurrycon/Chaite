using Chaite.Core;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSummonWhipProductionRegressions()
        {
            Run(nameof(OrdinaryOutputRemainsPreferredOverSummonWhip),
                OrdinaryOutputRemainsPreferredOverSummonWhip);
            Run(nameof(ExpectedEncounterAdmitsSummonWhipFallback),
                ExpectedEncounterAdmitsSummonWhipFallback);
            Run(nameof(ActiveEncounterAdmitsSummonWhipFallback),
                ActiveEncounterAdmitsSummonWhipFallback);
            Run(nameof(ActiveEncounterAdoptsExistingMinionWithoutStaffUse),
                ActiveEncounterAdoptsExistingMinionWithoutStaffUse);
            Run(nameof(PlannerEmitsCompleteSummonWhipNativeSequence),
                PlannerEmitsCompleteSummonWhipNativeSequence);
            Run(nameof(BossArrivalResetPreservesSummonDeploymentState),
                BossArrivalResetPreservesSummonDeploymentState);
            Run(nameof(SummonWhipLiveProofLossReturnsNeutralControl),
                SummonWhipLiveProofLossReturnsNeutralControl);
            Run(nameof(LacewingStartRejectsDualSlotOnlyOutput),
                LacewingStartRejectsDualSlotOnlyOutput);
            Run(nameof(FacadeSumsSpiderFamilyWithoutWorldProjectileScan),
                FacadeSumsSpiderFamilyWithoutWorldProjectileScan);
            Run(nameof(FacadeRejectsAmbiguousOrMalformedSummonPairs),
                FacadeRejectsAmbiguousOrMalformedSummonPairs);
            Run(nameof(FacadeSummonManaGateFailsClosed),
                FacadeSummonManaGateFailsClosed);
            Run(nameof(FacadeAuthorizesOnlyFreshCertifiedSummonWhipEdges),
                FacadeAuthorizesOnlyFreshCertifiedSummonWhipEdges);
        }

        private static void OrdinaryOutputRemainsPreferredOverSummonWhip()
        {
            var snapshot = NativeGunScenario();
            snapshot.SummonWhipOutput = ReadyProductionSummonWhip();
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(snapshot,
                "suspicious-eye", 4, out reason), reason);
            False(planner.UsesSummonWhipOutput);
            Equal(2, planner.LatchedOutputSlot);
            Equal(98, planner.LatchedOutputWeaponId);
        }

        private static void ExpectedEncounterAdmitsSummonWhipFallback()
        {
            var snapshot = SummonWhipFallbackScenario();
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(snapshot,
                "suspicious-eye", 4, out reason), reason);
            True(planner.UsesSummonWhipOutput);
            Equal(-1, planner.LatchedOutputSlot);
            Equal(0, planner.LatchedOutputWeaponId);
        }

        private static void ActiveEncounterAdmitsSummonWhipFallback()
        {
            var snapshot = SummonWhipFallbackScenario();
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForActiveEncounter(snapshot, out reason),
                reason);
            True(planner.UsesSummonWhipOutput);
            var plan = planner.Plan(snapshot);
            False(plan.RequestControlReturn);
            Equal(OutputRouteKind.MinionAndWhip, plan.OutputRouteKind);
            Equal(SummonWhipOutputAction.SelectStaff,
                plan.SummonWhipOutputAction);
        }

        private static void ActiveEncounterAdoptsExistingMinionWithoutStaffUse()
        {
            var snapshot = SummonWhipFallbackScenario();
            ConfirmProductionMinion(ref snapshot.SummonWhipOutput);
            snapshot.SummonWhipOutput.SelectedSlot = 2;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForActiveEncounter(snapshot, out reason),
                reason);
            True(planner.UsesSummonWhipOutput);

            var selectWhip = planner.Plan(snapshot);
            AssertSummonPlan(selectWhip,
                SummonWhipOutputPhase.SelectWhip,
                SummonWhipOutputAction.SelectWhip, 5, 4672, 0, 841,
                false);
            snapshot.SummonWhipOutput.SelectedSlot = 5;
            var release = planner.Plan(snapshot);
            AssertSummonPlan(release,
                SummonWhipOutputPhase.WhipLoop,
                SummonWhipOutputAction.ReleaseUseItem, 5, 4672, 0, 841,
                false);
        }

        private static void PlannerEmitsCompleteSummonWhipNativeSequence()
        {
            var snapshot = SummonWhipFallbackScenario();
            snapshot.SummonWhipOutput.SelectedSlot = 0;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForActiveEncounter(snapshot, out reason),
                reason);

            var selectStaff = planner.Plan(snapshot);
            AssertSummonPlan(selectStaff, SummonWhipOutputPhase.SelectStaff,
                SummonWhipOutputAction.SelectStaff, 2, 1309, 64, 266,
                false);

            snapshot.SummonWhipOutput.SelectedSlot = 2;
            var pulseStaff = planner.Plan(snapshot);
            AssertSummonPlan(pulseStaff,
                SummonWhipOutputPhase.ConfirmDeployment,
                SummonWhipOutputAction.PulseStaffUse, 2, 1309, 64, 266,
                true);

            var releaseStaff = planner.Plan(snapshot);
            AssertSummonPlan(releaseStaff,
                SummonWhipOutputPhase.ConfirmDeployment,
                SummonWhipOutputAction.ReleaseUseItem, 2, 1309, 64, 266,
                false);

            ConfirmProductionMinion(ref snapshot.SummonWhipOutput);
            var selectWhip = planner.Plan(snapshot);
            AssertSummonPlan(selectWhip, SummonWhipOutputPhase.SelectWhip,
                SummonWhipOutputAction.SelectWhip, 5, 4672, 0, 841,
                false);

            snapshot.SummonWhipOutput.SelectedSlot = 5;
            var releaseWhip = planner.Plan(snapshot);
            AssertSummonPlan(releaseWhip, SummonWhipOutputPhase.WhipLoop,
                SummonWhipOutputAction.ReleaseUseItem, 5, 4672, 0, 841,
                false);
            var pulseWhip = planner.Plan(snapshot);
            AssertSummonPlan(pulseWhip, SummonWhipOutputPhase.WhipLoop,
                SummonWhipOutputAction.PulseWhipUse, 5, 4672, 0, 841,
                true);
            var freshRelease = planner.Plan(snapshot);
            AssertSummonPlan(freshRelease, SummonWhipOutputPhase.WhipLoop,
                SummonWhipOutputAction.ReleaseUseItem, 5, 4672, 0, 841,
                false);
        }

        private static void BossArrivalResetPreservesSummonDeploymentState()
        {
            var snapshot = SummonWhipFallbackScenario();
            snapshot.SummonWhipOutput.SelectedSlot = 2;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.PrepareForExpectedEncounter(snapshot,
                "suspicious-eye", 4, out reason), reason);
            Equal(SummonWhipOutputAction.PulseStaffUse,
                planner.PlanSurvival(snapshot).SummonWhipOutputAction);

            planner.ResetForBossArrival();
            ConfirmProductionMinion(ref snapshot.SummonWhipOutput);
            var afterArrival = planner.Plan(snapshot);
            False(afterArrival.RequestControlReturn);
            Equal(SummonWhipOutputAction.SelectWhip,
                afterArrival.SummonWhipOutputAction);
            False(afterArrival.Fire);
        }

        private static void SummonWhipLiveProofLossReturnsNeutralControl()
        {
            for (var mutation = 0; mutation < 4; mutation++)
            {
                var snapshot = SummonWhipFallbackScenario();
                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                True(planner.PrepareForActiveEncounter(snapshot, out reason),
                    reason);
                if (mutation == 0)
                    snapshot.SummonWhipOutput.Staff.ItemId = 2365;
                else if (mutation == 1)
                    snapshot.SummonWhipOutput.MaximumMinionSlots = 0;
                else if (mutation == 2)
                    snapshot.SummonWhipOutput.OwnerProjectileCount = 2;
                else
                    snapshot.SummonWhipOutput.WhipEffectiveDamage = 1;
                AssertNeutralReturn(planner.Plan(snapshot));
            }

            var confirmed = SummonWhipFallbackScenario();
            confirmed.SummonWhipOutput.SelectedSlot = 2;
            var confirmedPlanner = new CombatPlanner(new PlannerSettings());
            string confirmedReason;
            True(confirmedPlanner.PrepareForActiveEncounter(confirmed,
                out confirmedReason), confirmedReason);
            confirmedPlanner.Plan(confirmed);
            ConfirmProductionMinion(ref confirmed.SummonWhipOutput);
            confirmedPlanner.Plan(confirmed);
            confirmed.SummonWhipOutput.SelectedSlot = 5;
            confirmedPlanner.Plan(confirmed);
            confirmed.SummonWhipOutput.ExpectedStaffBuffActive = false;
            AssertNeutralReturn(confirmedPlanner.Plan(confirmed));
        }

        private static void LacewingStartRejectsDualSlotOnlyOutput()
        {
            var snapshot = SummonWhipFallbackScenario(636);
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            False(planner.PrepareForExpectedEncounter(snapshot,
                "prismatic-lacewing", 636, out reason));
            True(reason != null && reason.Contains("single-slot"), reason);
            False(planner.UsesSummonWhipOutput);
        }

        private static void FacadeSumsSpiderFamilyWithoutWorldProjectileScan()
        {
            var fixture = new SummonWhipFacadeFixture(2551, 390, 133,
                4672, 841);
            fixture.Player.Owned[390] = 1;
            fixture.Player.Owned[391] = 2;
            fixture.Player.Owned[392] = 3;
            fixture.Player.BuffTypes[0] = 133;
            fixture.Player.BuffTimes[0] = 600;
            var observation = fixture.Read();
            True(observation.Known);
            True(observation.OwnerProjectileCountKnown);
            Equal(6, observation.OwnerProjectileCount);
            True(observation.BuffStateKnown);
            True(observation.ExpectedStaffBuffActive);
            Equal(120, observation.WhipEffectiveDamage);
            True(Math.Abs(240f - observation.ConservativeWhipDps) < .001f);
            // The fixture deliberately never initializes TerrariaFacade's
            // _projectiles delegate. A successful read proves this production
            // path used only Player.ownedProjectileCounts O(1) lookups.
        }

        private static void FacadeRejectsAmbiguousOrMalformedSummonPairs()
        {
            var duplicateStaff = new SummonWhipFacadeFixture();
            duplicateStaff.Items[1] = SummonWhipFixtureItem.Staff(
                2365, 375, 126);
            False(duplicateStaff.Read().Known);

            var duplicateWhip = new SummonWhipFacadeFixture();
            duplicateWhip.Items[7] = SummonWhipFixtureItem.Whip(
                4913, 914);
            False(duplicateWhip.Read().Known);

            var malformedBuff = new SummonWhipFacadeFixture();
            malformedBuff.Player.BuffTimes = new int[1];
            malformedBuff.Player.BuffTypes = new int[2];
            var buffObservation = malformedBuff.Read();
            True(buffObservation.Known);
            False(buffObservation.BuffStateKnown);
            SummonWhipOutputRoute ignored;
            string reason;
            False(SummonWhipOutputRouteContract.TryCreateReady(
                in buffObservation, out ignored, out reason));

            var shortOwned = new SummonWhipFacadeFixture(2551, 390, 133,
                4672, 841);
            shortOwned.Player.Owned = new int[392];
            var ownerObservation = shortOwned.Read();
            True(ownerObservation.Known);
            False(ownerObservation.OwnerProjectileCountKnown);
            False(SummonWhipOutputRouteContract.TryCreateReady(
                in ownerObservation, out ignored, out reason));
        }

        private static void FacadeSummonManaGateFailsClosed()
        {
            var fixture = new SummonWhipFacadeFixture();
            fixture.Player.Selected = 2;
            fixture.Items[2].Mana = 10;
            fixture.Player.Mana = 0;
            False(fixture.Read().CanUseItem);
            fixture.Player.Mana = 10;
            True(fixture.Read().CanUseItem);
            fixture.Player.Silence = true;
            False(fixture.Read().CanUseItem);
        }

        private static void FacadeAuthorizesOnlyFreshCertifiedSummonWhipEdges()
        {
            var observation = ReadyProductionSummonWhip();
            observation.SelectedSlot = 0;
            SummonWhipOutputRoute route;
            string reason;
            True(SummonWhipOutputRouteContract.TryCreateReady(in observation,
                out route, out reason), reason);

            var select = CertifiedSummonPlan(in route,
                SummonWhipOutputPhase.SelectStaff,
                SummonWhipOutputAction.SelectStaff, false);
            int requested;
            bool pulse;
            True(AuthorizeSummonWhip(in select, in observation,
                out requested, out pulse, out reason), reason);
            Equal(2, requested);
            False(pulse);

            observation.SelectedSlot = 2;
            var staffPulse = CertifiedSummonPlan(in route,
                SummonWhipOutputPhase.ConfirmDeployment,
                SummonWhipOutputAction.PulseStaffUse, true);
            True(AuthorizeSummonWhip(in staffPulse, in observation,
                out requested, out pulse, out reason), reason);
            Equal(-1, requested);
            True(pulse);

            var stale = observation;
            stale.OwnerProjectileCount = 2;
            False(AuthorizeSummonWhip(in staffPulse, in stale,
                out requested, out pulse, out reason));
            False(pulse);
            True(!string.IsNullOrEmpty(reason));

            ConfirmProductionMinion(ref observation);
            observation.SelectedSlot = 5;
            var whipPulse = CertifiedSummonPlan(in route,
                SummonWhipOutputPhase.WhipLoop,
                SummonWhipOutputAction.PulseWhipUse, true);
            True(AuthorizeSummonWhip(in whipPulse, in observation,
                out requested, out pulse, out reason), reason);
            True(pulse);
            observation.ReleaseUseItem = false;
            False(AuthorizeSummonWhip(in whipPulse, in observation,
                out requested, out pulse, out reason));
            False(pulse);
        }

        private static CombatSnapshot SummonWhipFallbackScenario(
            int bossType = 4)
        {
            var snapshot = bossType == 4 ? NativeGunScenario() :
                CombatScenario(bossType);
            snapshot.Weapon.NativeProfileRequired = true;
            snapshot.Weapon.WeaponId = 999;
            snapshot.Weapon.AmmoId = 0;
            snapshot.Weapon.ProjectileId = 999;
            snapshot.SummonWhipOutput = ReadyProductionSummonWhip();
            return snapshot;
        }

        private static SummonWhipOutputObservation ReadyProductionSummonWhip()
        {
            return new SummonWhipOutputObservation
            {
                Known = true,
                Staff = new SummonWhipItemObservation
                {
                    Known = true, Slot = 2, Stack = 1, ItemId = 1309,
                    ProjectileId = 266, BuffId = 64
                },
                Whip = new SummonWhipItemObservation
                {
                    Known = true, Slot = 5, Stack = 1, ItemId = 4672,
                    ProjectileId = 841, BuffId = 0
                },
                SelectedSlot = 0,
                CanUseItem = true,
                ReleaseUseItem = true,
                ItemAnimation = 0,
                ItemTime = 0,
                MinionCapacityKnown = true,
                UsedMinionSlots = 0f,
                MaximumMinionSlots = 1,
                BuffStateKnown = true,
                ExpectedStaffBuffActive = false,
                OwnerProjectileCountKnown = true,
                OwnerProjectileCount = 0,
                WhipOutputKnown = true,
                WhipEffectiveDamage = 120,
                WhipUseAnimation = 30,
                WhipUseTime = 30
            };
        }

        private static void ConfirmProductionMinion(
            ref SummonWhipOutputObservation observation)
        {
            observation.ExpectedStaffBuffActive = true;
            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
        }

        private static void AssertSummonPlan(ControlPlan plan,
            SummonWhipOutputPhase phase, SummonWhipOutputAction action,
            int slot, int item, int auxiliary, int projectile, bool fire)
        {
            False(plan.RequestControlReturn);
            Equal(OutputRouteKind.MinionAndWhip, plan.OutputRouteKind);
            Equal(phase, plan.SummonWhipOutputPhase);
            Equal(action, plan.SummonWhipOutputAction);
            Equal(slot, plan.PreferredWeaponSlot);
            Equal(item, plan.ExpectedWeaponId);
            Equal(auxiliary, plan.ExpectedAmmoId);
            Equal(projectile, plan.ExpectedProjectileId);
            Equal(fire, plan.Fire);
            False(plan.QuickMana);
            True(plan.SummonWhipOutputRoute.IsSpecified);
        }

        private static void AssertNeutralReturn(ControlPlan plan)
        {
            True(plan.RequestControlReturn);
            False(plan.Fire || plan.QuickMana || plan.Jump || plan.Dash ||
                plan.Hook || plan.ToggleMount || plan.Drop ||
                plan.FeatherFallUp || plan.GravityControl != 0);
            Equal(0, plan.Horizontal);
            True(!string.IsNullOrEmpty(plan.ControlReturnReason));
        }

        private static ControlPlan CertifiedSummonPlan(
            in SummonWhipOutputRoute route, SummonWhipOutputPhase phase,
            SummonWhipOutputAction action, bool fire)
        {
            var staffPhase = phase == SummonWhipOutputPhase.SelectStaff ||
                phase == SummonWhipOutputPhase.DeployOnce ||
                phase == SummonWhipOutputPhase.ConfirmDeployment;
            return new ControlPlan
            {
                OutputRouteKind = OutputRouteKind.MinionAndWhip,
                SummonWhipOutputRoute = route,
                SummonWhipOutputPhase = phase,
                SummonWhipOutputAction = action,
                Fire = fire,
                PreferredWeaponSlot = staffPhase ? route.StaffSlot :
                    route.WhipSlot,
                ExpectedWeaponId = staffPhase ? route.StaffProfile.ItemId :
                    route.WhipProfile.ItemId,
                ExpectedAmmoId = staffPhase ? route.StaffProfile.BuffId : 0,
                ExpectedProjectileId = staffPhase ?
                    route.StaffProfile.ProjectileId :
                    route.WhipProfile.ProjectileId
            };
        }

        private static bool AuthorizeSummonWhip(in ControlPlan plan,
            in SummonWhipOutputObservation observation, out int requested,
            out bool pulse, out string reason)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var method = type.GetMethod("TryAuthorizeSummonWhipAction",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[] { plan, observation, -1, false, null };
            var accepted = (bool)method.Invoke(null, arguments);
            requested = (int)arguments[2];
            pulse = (bool)arguments[3];
            reason = arguments[4] as string;
            return accepted;
        }

        private sealed class SummonWhipFacadeFixture
        {
            private readonly object _facade;
            private readonly Type _type;
            public readonly SummonWhipFixturePlayer Player =
                new SummonWhipFixturePlayer();
            public readonly SummonWhipFixtureItem[] Items =
                new SummonWhipFixtureItem[10];

            public SummonWhipFacadeFixture(int staffId = 1309,
                int staffProjectile = 266, int staffBuff = 64,
                int whipId = 4672, int whipProjectile = 841)
            {
                Items[2] = SummonWhipFixtureItem.Staff(staffId,
                    staffProjectile, staffBuff);
                Items[5] = SummonWhipFixtureItem.Whip(whipId,
                    whipProjectile);
                _type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                    "Chaite.Plugin.TerrariaFacade", true);
                _facade = FormatterServices.GetUninitializedObject(_type);
                Set("_itemStack", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).Stack));
                Set("_itemTypeId", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).Type));
                Set("_itemShoot", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).Shoot));
                Set("_itemBuffType", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).Buff));
                Set("_itemMana", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).Mana));
                Set("_itemUseAnimation", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).UseAnimation));
                Set("_itemUseTime", new Func<object, int>(value =>
                    ((SummonWhipFixtureItem)value).UseTime));
                Set("_selectedItem", new Func<object, int>(value =>
                    ((SummonWhipFixturePlayer)value).Selected));
                Set("_releaseUseItem", new Func<object, bool>(value =>
                    ((SummonWhipFixturePlayer)value).Release));
                Set("_playerItemAnimation", new Func<object, int>(value => 0));
                Set("_playerItemTime", new Func<object, int>(value => 0));
                Set("_playerMaxMinions", new Func<object, int>(value =>
                    ((SummonWhipFixturePlayer)value).MaxMinions));
                Set("_playerSlotsMinions", new Func<object, float>(value => 0f));
                Set("_playerOwnedProjectileCounts",
                    new Func<object, int[]>(value =>
                        ((SummonWhipFixturePlayer)value).Owned));
                Set("_playerBuffType", new Func<object, int[]>(value =>
                    ((SummonWhipFixturePlayer)value).BuffTypes));
                Set("_playerBuffTime", new Func<object, int[]>(value =>
                    ((SummonWhipFixturePlayer)value).BuffTimes));
                Set("_weaponDamage", new Func<object, object, int>((player,
                    item) => ((SummonWhipFixtureItem)item).Damage));
                Set("_playerActive", new Func<object, bool>(value => true));
                Set("_playerDead", new Func<object, bool>(value => false));
                Set("_playerCCed", new Func<object, bool>(value => false));
                Set("_playerNoItems", new Func<object, bool>(value => false));
                Set("_playerCursed", new Func<object, bool>(value => false));
                Set("_drawingPlayerChat", new Func<bool>(() => false));
                Set("_gamePaused", new Func<bool>(() => false));
                Set("_inventoryChestStack", new Func<object, bool[]>(value =>
                    ((SummonWhipFixturePlayer)value).ChestStack));
                Set("_mouseItem", new Func<object>(() => null));
                Set("_playerManaCost", new Func<object, float>(value =>
                    ((SummonWhipFixturePlayer)value).ManaCost));
                Set("_playerMana", new Func<object, int>(value =>
                    ((SummonWhipFixturePlayer)value).Mana));
                Set("_playerSilence", new Func<object, bool>(value =>
                    ((SummonWhipFixturePlayer)value).Silence));
                // Deliberately do not set _projectiles: the read under test
                // must rely only on Player.ownedProjectileCounts.
            }

            private void Set(string field, object value)
            {
                var info = _type.GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(info != null, "missing summon fixture field " + field);
                info.SetValue(_facade, value);
            }

            public SummonWhipOutputObservation Read()
            {
                var method = _type.GetMethod("ReadSummonWhipOutput",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(method != null);
                return (SummonWhipOutputObservation)method.Invoke(_facade,
                    new object[] { Player, Array.ConvertAll(Items,
                        item => (object)item) });
            }
        }

        private sealed class SummonWhipFixturePlayer
        {
            public int Selected;
            public bool Release = true;
            public int MaxMinions = 1;
            public int[] Owned = new int[1000];
            public int[] BuffTypes = new int[22];
            public int[] BuffTimes = new int[22];
            public bool[] ChestStack = new bool[10];
            public float ManaCost = 1f;
            public int Mana = 200;
            public bool Silence;
        }

        private sealed class SummonWhipFixtureItem
        {
            public int Type;
            public int Stack = 1;
            public int Shoot;
            public int Buff;
            public int Mana;
            public int UseAnimation;
            public int UseTime;
            public int Damage;

            public static SummonWhipFixtureItem Staff(int type, int shoot,
                int buff)
            {
                return new SummonWhipFixtureItem
                {
                    Type = type, Shoot = shoot, Buff = buff, Mana = 10,
                    UseAnimation = 28, UseTime = 28, Damage = 20
                };
            }

            public static SummonWhipFixtureItem Whip(int type, int shoot)
            {
                return new SummonWhipFixtureItem
                {
                    Type = type, Shoot = shoot, Buff = 0,
                    UseAnimation = 30, UseTime = 30, Damage = 120
                };
            }
        }
    }
}
