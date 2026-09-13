using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class SummonWhipOutputContractTests
    {
        public static int RunAll()
        {
            CatalogPinsReviewedVanillaIdsAndTimings();
            StaffCatalogContainsExactlyEveryReviewedOrdinaryMinionItem();
            SpecialStaffFamiliesMatchNativeShootBranches();
            MultiRootDeploymentsRequireExactCombinedProof();
            ReplacementFamiliesRequireAnEmptyOwnerBaseline();
            TigerAndAbigailReinforcementRemainExactlyProvable();
            WhipCatalogContainsExactlyEveryDefaultToWhipItem();
            AdmissionRequiresDistinctExactHotbarIdentities();
            AdmissionRejectsUnknownOrInsufficientCapacity();
            AdmissionRejectsUnknownOrInvalidWhipOutput();
            ExistingDeploymentDoesNotRequireAnotherFreeSlot();
            ExistingDeploymentRequiresConsistentNativeProof();
            ExistingDeploymentStartsAtTheWhip();
            DeploymentProofRequiresExactOwnerAndSlotDeltas();
            ControllerDeploysExactlyOnceAndRequiresBothProofs();
            CapacityIsRecheckedImmediatelyBeforeDeployment();
            BlockedItemUseDoesNotConsumeDeploymentOrWhipEdge();
            WhipLoopHonorsDemandAndFreshReleaseEdges();
            IdentityMutationFailsPermanently();
            ConfirmationTimeoutNeverRetriesDeployment();
            ConfirmedMinionDisappearanceFailsClosed();
            SteadyWhipLoopAllocatesNothing();
            return 22;
        }

        private static void CatalogPinsReviewedVanillaIdsAndTimings()
        {
            var staffCases = new[,]
            {
                // item, shoot, buff, animation, time, reuse, family count,
                // extra owner IDs 1..3, exact owner delta, requires empty.
                { 1157, 191, 49, 28, 28, 2, 4, 192, 193, 194, 1, 0 },
                { 1309, 266, 64, 28, 28, 2, 1, 0, 0, 0, 1, 0 },
                { 1802, 317, 83, 28, 28, 2, 1, 0, 0, 0, 1, 0 },
                { 2364, 373, 125, 22, 22, 2, 1, 0, 0, 0, 1, 0 },
                { 2365, 375, 126, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 2535, 387, 134, 36, 36, 2, 2, 388, 0, 0, 2, 0 },
                { 2551, 390, 133, 36, 36, 2, 3, 391, 392, 0, 1, 0 },
                { 2584, 393, 135, 36, 36, 2, 3, 394, 395, 0, 1, 0 },
                { 2621, 407, 139, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 2749, 423, 140, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 3249, 533, 161, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 3474, 613, 182, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 3531, 625, 188, 36, 36, 2, 4, 626, 627, 628, 4, 1 },
                { 4269, 755, 213, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 4273, 758, 214, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 4281, 759, 216, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 4607, 831, 263, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 4758, 864, 271, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 5005, 946, 322, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 5069, 951, 325, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 5114, 970, 335, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 5456, 1022, 355, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 5663, 1093, 385, 15, 15, 2, 2, 1112, 0, 0, 1, 1 },
                { 5664, 1094, 386, 15, 15, 2, 2, 1113, 0, 0, 1, 1 },
                { 6148, 1112, 389, 15, 15, 2, 2, 1093, 0, 0, 1, 1 },
                { 6149, 1113, 390, 15, 15, 2, 2, 1094, 0, 0, 1, 1 },
                { 6161, 1118, 393, 36, 36, 2, 1, 0, 0, 0, 1, 0 },
                { 6164, 1119, 394, 36, 36, 2, 1, 0, 0, 0, 1, 0 }
            };
            for (var index = 0; index < staffCases.GetLength(0); index++)
            {
                VanillaSummonStaffOutputProfile profile;
                True(VanillaSummonWhipOutputCatalog.TryGetStaff(
                    staffCases[index, 0], out profile));
                Equal(staffCases[index, 0], profile.ItemId);
                Equal(staffCases[index, 1], profile.ProjectileId);
                Equal(staffCases[index, 2], profile.BuffId);
                Equal(staffCases[index, 3], profile.DefaultUseAnimation);
                Equal(staffCases[index, 4], profile.DefaultUseTime);
                Equal(staffCases[index, 5], profile.DefaultReuseDelay);
                Near(1f, profile.MinionSlotCost);
                Equal(staffCases[index, 6],
                    profile.OwnerProjectileTypeCount);
                Equal(profile.ProjectileId, profile.OwnerProjectileIdAt(0));
                Equal(staffCases[index, 7], profile.OwnerProjectileIdAt(1));
                Equal(staffCases[index, 8], profile.OwnerProjectileIdAt(2));
                Equal(staffCases[index, 9], profile.OwnerProjectileIdAt(3));
                Equal(staffCases[index, 10],
                    profile.ExpectedOwnerProjectileDelta);
                Equal(staffCases[index, 11] != 0,
                    profile.RequiresEmptyOwnerFamily);
                Equal(0, profile.OwnerProjectileIdAt(4));
            }
            VanillaSummonStaffOutputProfile unsupportedStaff;
            False(VanillaSummonWhipOutputCatalog.TryGetStaff(999,
                out unsupportedStaff));
            True(unsupportedStaff == null);

            var whipCases = new[,]
            {
                { 4672, 841, 30, 30, 4 },
                { 4678, 847, 28, 28, 4 },
                { 4679, 848, 35, 35, 4 },
                { 4680, 849, 27, 27, 4 },
                { 4911, 912, 30, 30, 4 },
                { 4912, 913, 30, 30, 4 },
                { 4913, 914, 30, 30, 4 },
                { 4914, 915, 30, 30, 4 },
                { 5074, 952, 30, 30, 5 },
                { 5473, 1028, 35, 35, 3 },
                { 5474, 1029, 30, 30, 4 },
                { 5475, 1030, 30, 30, 4 },
                { 5476, 1031, 30, 30, 4 },
                { 5477, 1032, 30, 30, 4 },
                { 5478, 1033, 30, 30, 4 },
                { 5479, 1034, 30, 30, 4 },
                { 5480, 1035, 30, 30, 4 },
                { 5688, 1104, 30, 30, 4 }
            };
            for (var index = 0; index < whipCases.GetLength(0); index++)
            {
                VanillaWhipOutputProfile profile;
                True(VanillaSummonWhipOutputCatalog.TryGetWhip(
                    whipCases[index, 0], out profile));
                Equal(whipCases[index, 0], profile.ItemId);
                Equal(whipCases[index, 1], profile.ProjectileId);
                Equal(whipCases[index, 2], profile.DefaultUseAnimation);
                Equal(whipCases[index, 3], profile.DefaultUseTime);
                Near(whipCases[index, 4], profile.DefaultShootSpeed);
            }
            VanillaWhipOutputProfile unsupportedWhip;
            False(VanillaSummonWhipOutputCatalog.TryGetWhip(999,
                out unsupportedWhip));
            True(unsupportedWhip == null);
        }

        private static void StaffCatalogContainsExactlyEveryReviewedOrdinaryMinionItem()
        {
            var expected = new[]
            {
                1157, 1309, 1802, 2364, 2365, 2535, 2551, 2584, 2621,
                2749, 3249, 3474, 3531, 4269, 4273, 4281, 4607, 4758,
                5005, 5069, 5114, 5456, 5663, 5664, 6148, 6149, 6161,
                6164
            };
            var expectedIndex = 0;
            for (var itemId = 1; itemId <= 6200; itemId++)
            {
                VanillaSummonStaffOutputProfile profile;
                if (!VanillaSummonWhipOutputCatalog.TryGetStaff(itemId,
                        out profile))
                    continue;
                True(expectedIndex < expected.Length,
                    "staff catalog contains an extra item " + itemId);
                Equal(expected[expectedIndex++], itemId);
                Equal(itemId, profile.ItemId);
            }
            Equal(28, expectedIndex);

            // Explicitly pin representative sentries and non-combat pets out
            // of this controller even though they also expose shoot/buff data.
            foreach (var excluded in new[]
                { 1169, 1180, 1242, 1572, 2366, 3569, 3571, 3834, 5119,
                    5463 })
            {
                VanillaSummonStaffOutputProfile profile;
                False(VanillaSummonWhipOutputCatalog.TryGetStaff(excluded,
                    out profile));
                True(profile == null);
            }
        }

        private static void SpecialStaffFamiliesMatchNativeShootBranches()
        {
            AssertOwnerFamily(1157, 1, false, 191, 192, 193, 194);
            AssertOwnerFamily(2535, 2, false, 387, 388);
            AssertOwnerFamily(2551, 1, false, 390, 391, 392);
            AssertOwnerFamily(2584, 1, false, 393, 394, 395);
            AssertOwnerFamily(3531, 4, true, 625, 626, 627, 628);
            AssertOwnerFamily(5663, 1, true, 1093, 1112);
            AssertOwnerFamily(6148, 1, true, 1112, 1093);
            AssertOwnerFamily(5664, 1, true, 1094, 1113);
            AssertOwnerFamily(6149, 1, true, 1113, 1094);
        }

        private static void MultiRootDeploymentsRequireExactCombinedProof()
        {
            foreach (var test in new[]
            {
                new[] { 2535, 2 }, // Optic: two half-slot twins.
                new[] { 3531, 4 }  // Dragon: initial four-part chain.
            })
            {
                var observation = ObservationForStaff(test[0]);
                SummonWhipOutputRoute route;
                string reason;
                True(SummonWhipOutputRouteContract.TryCreateReady(
                    in observation, out route, out reason), reason);
                observation.ExpectedStaffBuffActive = true;
                observation.OwnerProjectileCount = test[1] - 1;
                observation.UsedMinionSlots = 1f;
                False(SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in route));
                observation.OwnerProjectileCount = test[1];
                observation.UsedMinionSlots = .5f;
                False(SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in route));
                observation.UsedMinionSlots = 1f;
                True(SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in route));
            }
        }

        private static void ReplacementFamiliesRequireAnEmptyOwnerBaseline()
        {
            foreach (var itemId in new[] { 3531, 5663, 5664, 6148, 6149 })
            {
                var occupied = ObservationForStaff(itemId);
                occupied.MaximumMinionSlots = 2;
                occupied.UsedMinionSlots = 1f;
                occupied.OwnerProjectileCount = 1;
                SummonWhipOutputRoute route;
                string reason;
                False(SummonWhipOutputRouteContract.TryCreateReady(
                    in occupied, out route, out reason));
                True(reason.Contains("replace") ||
                    reason.Contains("reinforce"));

                var empty = ObservationForStaff(itemId);
                True(SummonWhipOutputRouteContract.TryCreateReady(in empty,
                    out route, out reason), reason);
            }
        }

        private static void TigerAndAbigailReinforcementRemainExactlyProvable()
        {
            foreach (var itemId in new[] { 4607, 5114 })
            {
                var observation = ObservationForStaff(itemId);
                observation.MaximumMinionSlots = 3;
                observation.UsedMinionSlots = 2f;
                observation.OwnerProjectileCount = 2;
                SummonWhipOutputRoute route;
                string reason;
                True(SummonWhipOutputRouteContract.TryCreateReady(
                    in observation, out route, out reason), reason);
                observation.ExpectedStaffBuffActive = true;
                observation.UsedMinionSlots = 3f;
                observation.OwnerProjectileCount = 3;
                True(SummonWhipOutputRouteContract.DeploymentConfirmed(
                    in observation, in route));
            }
        }

        private static void WhipCatalogContainsExactlyEveryDefaultToWhipItem()
        {
            var expected = new[]
            {
                4672, 4678, 4679, 4680, 4911, 4912, 4913, 4914, 5074,
                5473, 5474, 5475, 5476, 5477, 5478, 5479, 5480, 5688
            };
            var found = 0;
            var expectedIndex = 0;
            for (var itemId = 1; itemId <= 6000; itemId++)
            {
                VanillaWhipOutputProfile profile;
                if (!VanillaSummonWhipOutputCatalog.TryGetWhip(itemId,
                        out profile))
                    continue;
                True(expectedIndex < expected.Length,
                    "whip catalog contains an extra item " + itemId);
                Equal(expected[expectedIndex++], itemId);
                Equal(itemId, profile.ItemId);
                found++;
            }
            Equal(18, found);
            Equal(expected.Length, expectedIndex);
        }

        private static void AdmissionRequiresDistinctExactHotbarIdentities()
        {
            var observation = ReadyObservation();
            SummonWhipOutputRoute route;
            string reason;
            True(SummonWhipOutputRouteContract.TryCreateReady(in observation,
                out route, out reason), reason);
            True(route.IsSpecified);
            Equal(2, route.StaffSlot);
            Equal(5, route.WhipSlot);

            foreach (var mutation in new[] { 0, 1, 2, 3, 4, 5 })
            {
                var invalid = ReadyObservation();
                if (mutation == 0) invalid.Staff.ProjectileId++;
                else if (mutation == 1) invalid.Staff.BuffId++;
                else if (mutation == 2) invalid.Whip.ProjectileId++;
                else if (mutation == 3) invalid.Whip.BuffId = 64;
                else if (mutation == 4) invalid.Whip.Slot = invalid.Staff.Slot;
                else invalid.Staff.Stack = 0;
                False(SummonWhipOutputRouteContract.TryCreateReady(in invalid,
                    out route, out reason));
                True(!string.IsNullOrEmpty(reason));
            }
        }

        private static void AdmissionRejectsUnknownOrInsufficientCapacity()
        {
            foreach (var mutation in new[] { 0, 1, 2, 3, 4 })
            {
                var observation = ReadyObservation();
                if (mutation == 0) observation.MinionCapacityKnown = false;
                else if (mutation == 1) observation.UsedMinionSlots = 1f;
                else if (mutation == 2) observation.UsedMinionSlots = float.NaN;
                else if (mutation == 3)
                    observation.OwnerProjectileCountKnown = false;
                else observation.BuffStateKnown = false;
                SummonWhipOutputRoute route;
                string reason;
                False(SummonWhipOutputRouteContract.TryCreateReady(
                    in observation, out route, out reason));
                True(!string.IsNullOrEmpty(reason));
            }
        }

        private static void AdmissionRejectsUnknownOrInvalidWhipOutput()
        {
            for (var mutation = 0; mutation < 4; mutation++)
            {
                var observation = ReadyObservation();
                if (mutation == 0) observation.WhipOutputKnown = false;
                else if (mutation == 1) observation.WhipEffectiveDamage = 0;
                else if (mutation == 2) observation.WhipUseAnimation = 0;
                else observation.WhipUseTime = -1;
                SummonWhipOutputRoute route;
                string reason;
                False(SummonWhipOutputRouteContract.TryCreateReady(
                    in observation, out route, out reason));
                True(!string.IsNullOrEmpty(reason));
            }
        }

        private static void ExistingDeploymentDoesNotRequireAnotherFreeSlot()
        {
            var observation = ReadyObservation();
            observation.ExpectedStaffBuffActive = true;
            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
            SummonWhipOutputRoute route;
            string reason;
            True(SummonWhipOutputRouteContract.TryCreateReady(in observation,
                out route, out reason), reason);
            True(route.UsesExistingDeployment);
            Equal(1, route.BaselineOwnerProjectileCount);
            Near(1f, route.BaselineUsedMinionSlots);
            False(SummonWhipOutputRouteContract.
                HasFreeSlotForOneDeployment(in observation, in route));
            True(SummonWhipOutputRouteContract.DeploymentConfirmed(
                in observation, in route));
        }

        private static void ExistingDeploymentRequiresConsistentNativeProof()
        {
            for (var mutation = 0; mutation < 3; mutation++)
            {
                var observation = ReadyObservation();
                if (mutation == 0)
                    observation.ExpectedStaffBuffActive = true;
                else if (mutation == 1)
                    observation.OwnerProjectileCount = 1;
                else
                {
                    observation.ExpectedStaffBuffActive = true;
                    observation.OwnerProjectileCount = 1;
                    observation.UsedMinionSlots = .5f;
                }
                SummonWhipOutputRoute route;
                string reason;
                False(SummonWhipOutputRouteContract.TryCreateReady(
                    in observation, out route, out reason));
                True(!string.IsNullOrEmpty(reason));
            }

            var optic = ObservationForStaff(2535);
            optic.ExpectedStaffBuffActive = true;
            optic.OwnerProjectileCount = 1;
            optic.UsedMinionSlots = 1f;
            SummonWhipOutputRoute ignored;
            string opticReason;
            False(SummonWhipOutputRouteContract.TryCreateReady(in optic,
                out ignored, out opticReason));
        }

        private static void ExistingDeploymentStartsAtTheWhip()
        {
            var observation = ReadyObservation();
            observation.SelectedSlot = observation.Staff.Slot;
            observation.ExpectedStaffBuffActive = true;
            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);
            Equal(SummonWhipOutputPhase.SelectWhip, controller.Phase);

            var select = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.SelectWhip, select.Action);
            Equal(observation.Whip.Slot, select.SelectSlot);
            False(select.UseItem);

            observation.SelectedSlot = observation.Whip.Slot;
            var release = controller.Tick(in observation, true);
            Equal(SummonWhipOutputPhase.WhipLoop, release.Phase);
            Equal(SummonWhipOutputAction.ReleaseUseItem, release.Action);

            observation.OwnerProjectileCount++;
            var failed = controller.Tick(in observation, true);
            True(failed.Failed);
            False(failed.UseItem);
        }

        private static void DeploymentProofRequiresExactOwnerAndSlotDeltas()
        {
            var observation = ReadyObservation();
            SummonWhipOutputRoute route;
            string reason;
            True(SummonWhipOutputRouteContract.TryCreateReady(in observation,
                out route, out reason), reason);
            observation.ExpectedStaffBuffActive = true;

            observation.OwnerProjectileCount = 2;
            observation.UsedMinionSlots = 1f;
            False(SummonWhipOutputRouteContract.DeploymentConfirmed(
                in observation, in route));

            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = .5f;
            False(SummonWhipOutputRouteContract.DeploymentConfirmed(
                in observation, in route));

            observation.UsedMinionSlots = 1f;
            True(SummonWhipOutputRouteContract.DeploymentConfirmed(
                in observation, in route));
        }

        private static void ControllerDeploysExactlyOnceAndRequiresBothProofs()
        {
            var observation = ReadyObservation();
            observation.SelectedSlot = 0;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);

            var decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.SelectStaff, decision.Action);
            Equal(2, decision.SelectSlot);
            False(decision.UseItem);

            observation.SelectedSlot = 2;
            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.PulseStaffUse, decision.Action);
            True(decision.UseItem);
            Equal(SummonWhipOutputPhase.ConfirmDeployment, decision.Phase);

            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.ReleaseUseItem, decision.Action);
            False(decision.UseItem);

            observation.ExpectedStaffBuffActive = true;
            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputPhase.ConfirmDeployment, decision.Phase);
            observation.ExpectedStaffBuffActive = false;
            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputPhase.ConfirmDeployment, decision.Phase);

            observation.ExpectedStaffBuffActive = true;
            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.SelectWhip, decision.Action);
            Equal(5, decision.SelectSlot);
            Equal(SummonWhipOutputPhase.SelectWhip, decision.Phase);
            False(decision.UseItem);

            observation.SelectedSlot = 5;
            decision = controller.Tick(in observation, true);
            Equal(SummonWhipOutputPhase.WhipLoop, decision.Phase);
            Equal(SummonWhipOutputAction.ReleaseUseItem, decision.Action);
        }

        private static void CapacityIsRecheckedImmediatelyBeforeDeployment()
        {
            var observation = ReadyObservation();
            observation.SelectedSlot = 0;
            // Admit with one existing slot still occupied and one genuinely
            // free slot.  Then model a live maxMinions loss while the selected
            // staff transition is pending.  The deployment baseline itself
            // stays unchanged, so this specifically exercises the immediate
            // no-eviction capacity guard rather than the stricter baseline
            // mutation guard.
            observation.UsedMinionSlots = 1f;
            observation.MaximumMinionSlots = 2;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);
            Equal(SummonWhipOutputAction.SelectStaff,
                controller.Tick(in observation, true).Action);
            observation.SelectedSlot = 2;
            observation.MaximumMinionSlots = 1;
            var decision = controller.Tick(in observation, true);
            True(decision.Failed);
            False(decision.UseItem);
            True(decision.FailureReason.Contains("eviction"));
        }

        private static void BlockedItemUseDoesNotConsumeDeploymentOrWhipEdge()
        {
            var observation = ReadyObservation();
            observation.SelectedSlot = observation.Staff.Slot;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);

            var blockedStaff = controller.Tick(in observation, true, false);
            Equal(SummonWhipOutputPhase.DeployOnce, blockedStaff.Phase);
            Equal(SummonWhipOutputAction.None, blockedStaff.Action);
            False(blockedStaff.UseItem);
            Equal(SummonWhipOutputAction.PulseStaffUse,
                controller.Tick(in observation, true, true).Action);

            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
            observation.ExpectedStaffBuffActive = true;
            Equal(SummonWhipOutputAction.SelectWhip,
                controller.Tick(in observation, true).Action);
            observation.SelectedSlot = observation.Whip.Slot;
            Equal(SummonWhipOutputAction.ReleaseUseItem,
                controller.Tick(in observation, true).Action);

            var blockedWhip = controller.Tick(in observation, true, false);
            Equal(SummonWhipOutputAction.None, blockedWhip.Action);
            False(blockedWhip.UseItem);
            Equal(SummonWhipOutputAction.PulseWhipUse,
                controller.Tick(in observation, true, true).Action);
        }

        private static void WhipLoopHonorsDemandAndFreshReleaseEdges()
        {
            var observation = ReadyObservation();
            var controller = EnterWhipLoop(ref observation);

            var idle = controller.Tick(in observation, false);
            Equal(SummonWhipOutputAction.None, idle.Action);
            False(idle.UseItem);

            var pulse = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.PulseWhipUse, pulse.Action);
            True(pulse.UseItem);

            var release = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.ReleaseUseItem, release.Action);
            False(release.UseItem);

            observation.ReleaseUseItem = false;
            var held = controller.Tick(in observation, true);
            Equal(SummonWhipOutputAction.None, held.Action);
            False(held.UseItem);
            observation.ReleaseUseItem = true;
            Equal(SummonWhipOutputAction.PulseWhipUse,
                controller.Tick(in observation, true).Action);
        }

        private static void IdentityMutationFailsPermanently()
        {
            var observation = ReadyObservation();
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);
            observation.Whip.ProjectileId++;
            var failed = controller.Tick(in observation, true);
            True(failed.Failed);
            False(failed.UseItem);
            True(!string.IsNullOrEmpty(failed.FailureReason));

            observation.Whip.ProjectileId--;
            var stillFailed = controller.Tick(in observation, true);
            True(stillFailed.Failed);
            Equal(SummonWhipOutputAction.FailedClosed,
                stillFailed.Action);
            Equal(failed.FailureReason, stillFailed.FailureReason);
        }

        private static void ConfirmationTimeoutNeverRetriesDeployment()
        {
            var observation = ReadyObservation();
            observation.SelectedSlot = 2;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);
            Equal(SummonWhipOutputAction.PulseStaffUse,
                controller.Tick(in observation, true).Action);

            var staffPulseCount = 1;
            SummonWhipOutputDecision decision = default(
                SummonWhipOutputDecision);
            for (var tick = 0; tick < 80 && !controller.Failed; tick++)
            {
                decision = controller.Tick(in observation, true);
                if (decision.Action == SummonWhipOutputAction.PulseStaffUse)
                    staffPulseCount++;
            }
            True(controller.Failed);
            Equal(1, staffPulseCount);
            False(decision.UseItem);
            True(decision.FailureReason.Contains("not both confirmed"));
        }

        private static void ConfirmedMinionDisappearanceFailsClosed()
        {
            var observation = ReadyObservation();
            var controller = EnterWhipLoop(ref observation);
            observation.ExpectedStaffBuffActive = false;
            var decision = controller.Tick(in observation, true);
            True(decision.Failed);
            False(decision.UseItem);
            True(decision.FailureReason.Contains("disappeared"));
        }

        private static void SteadyWhipLoopAllocatesNothing()
        {
            var observation = ReadyObservation();
            var controller = EnterWhipLoop(ref observation);
            // Warm static/JIT paths before taking the allocation sample.
            controller.Tick(in observation, true);
            controller.Tick(in observation, true);
            GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0;
            for (var tick = 0; tick < 512; tick++)
                checksum += (int)controller.Tick(in observation, true).Action;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated);
            True(checksum > 0);
        }

        private static SummonWhipOutputController EnterWhipLoop(
            ref SummonWhipOutputObservation observation)
        {
            observation.SelectedSlot = observation.Staff.Slot;
            SummonWhipOutputController controller;
            string reason;
            True(SummonWhipOutputController.TryCreate(in observation,
                out controller, out reason), reason);
            Equal(SummonWhipOutputAction.PulseStaffUse,
                controller.Tick(in observation, true).Action);
            observation.OwnerProjectileCount = 1;
            observation.UsedMinionSlots = 1f;
            observation.ExpectedStaffBuffActive = true;
            Equal(SummonWhipOutputAction.SelectWhip,
                controller.Tick(in observation, true).Action);
            observation.SelectedSlot = observation.Whip.Slot;
            var transition = controller.Tick(in observation, true);
            Equal(SummonWhipOutputPhase.WhipLoop, transition.Phase);
            Equal(SummonWhipOutputAction.ReleaseUseItem,
                transition.Action);
            return controller;
        }

        private static void AssertOwnerFamily(int itemId, int expectedDelta,
            bool requiresEmpty, params int[] projectileIds)
        {
            VanillaSummonStaffOutputProfile profile;
            True(VanillaSummonWhipOutputCatalog.TryGetStaff(itemId,
                out profile));
            Equal(projectileIds.Length, profile.OwnerProjectileTypeCount);
            Equal(expectedDelta, profile.ExpectedOwnerProjectileDelta);
            Equal(requiresEmpty, profile.RequiresEmptyOwnerFamily);
            for (var index = 0; index < projectileIds.Length; index++)
                Equal(projectileIds[index],
                    profile.OwnerProjectileIdAt(index));
            Equal(0, profile.OwnerProjectileIdAt(projectileIds.Length));
        }

        private static SummonWhipOutputObservation ObservationForStaff(
            int itemId)
        {
            VanillaSummonStaffOutputProfile profile;
            True(VanillaSummonWhipOutputCatalog.TryGetStaff(itemId,
                out profile));
            var observation = ReadyObservation();
            observation.Staff.ItemId = profile.ItemId;
            observation.Staff.ProjectileId = profile.ProjectileId;
            observation.Staff.BuffId = profile.BuffId;
            return observation;
        }

        private static SummonWhipOutputObservation ReadyObservation() =>
            new SummonWhipOutputObservation
            {
                Known = true,
                Staff = new SummonWhipItemObservation
                {
                    Known = true,
                    Slot = 2,
                    Stack = 1,
                    ItemId = 1309,
                    ProjectileId = 266,
                    BuffId = 64
                },
                Whip = new SummonWhipItemObservation
                {
                    Known = true,
                    Slot = 5,
                    Stack = 1,
                    ItemId = 4672,
                    ProjectileId = 841,
                    BuffId = 0
                },
                SelectedSlot = 2,
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
                WhipEffectiveDamage = 100,
                WhipUseAnimation = 30,
                WhipUseTime = 30
            };

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected +
                    ", got " + actual);
        }

        private static void True(bool value, string reason = null)
        {
            if (!value)
                throw new InvalidOperationException(reason ?? "expected true");
        }

        private static void False(bool value) => True(!value);

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected +
                    ", got " + actual);
        }
    }
}
