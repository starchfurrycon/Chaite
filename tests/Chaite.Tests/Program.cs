using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--native-motion-trace") return VerifyNativeMotionTrace(args[1]);
            if (args.Length == 2 && args[0] == "--native-flight-trace") return VerifyNativeFlightTrace(args[1]);
            if (args.Length != 0) return 2;
            Run(nameof(BossMonitoringDoesNotOwnControls), BossMonitoringDoesNotOwnControls);
            Run(nameof(FishronThreatIdentitiesKeepTheWeaponBarWide), FishronThreatIdentitiesKeepTheWeaponBarWide);
            Run(nameof(FishronWingFollowsReviewedChargeCycle), FishronWingFollowsReviewedChargeCycle);
            Run(nameof(FishronWingKeepsStandoffGap), FishronWingKeepsStandoffGap);
            Run(nameof(FishronWingRestartsCycleAfterProjectileAttack), FishronWingRestartsCycleAfterProjectileAttack);
            Run(nameof(FishronWingScriptIgnoresThreatListContents), FishronWingScriptIgnoresThreatListContents);
            Run(nameof(FishronFormulaRejectsImpossibleNativeTuples), FishronFormulaRejectsImpossibleNativeTuples);
            Run(nameof(FishronChilletUsesReviewedNativeDashCadence), FishronChilletUsesReviewedNativeDashCadence);
            Run(nameof(FishronChilletRejectsWrongMountAndShortRunway), FishronChilletRejectsWrongMountAndShortRunway);
            Run(nameof(FishronAdmissionRequiresInfernoStock), FishronAdmissionRequiresInfernoStock);
            Run(nameof(FishronChilletLockedRouteAcceptsOnlyItsActiveMount), FishronChilletLockedRouteAcceptsOnlyItsActiveMount);
            Run(nameof(FormulaAdmissionSeparatesMobilityAndOutputRefusal), FormulaAdmissionSeparatesMobilityAndOutputRefusal);
            Run(nameof(FishronQueenSlimeUsesMountAndFixedRunway), FishronQueenSlimeUsesMountAndFixedRunway);
            Run(nameof(EmpressFlightUsesReviewedMountAndRainGate), EmpressFlightUsesReviewedMountAndRainGate);
            Run(nameof(EmpressWingUsesOneRepositionDashEdge), EmpressWingUsesOneRepositionDashEdge);
            Run(nameof(EmpressFormulaKeepsLoopAcrossAttackBoundaries), EmpressFormulaKeepsLoopAcrossAttackBoundaries);
            Run(nameof(BossMonitoringRejectsMidFightAndDeadArming), BossMonitoringRejectsMidFightAndDeadArming);
            Run(nameof(BossMonitoringTransitionsOnceAndResetsLifeAccounting), BossMonitoringTransitionsOnceAndResetsLifeAccounting);
            Run(nameof(BossMonitoringProductionHasNoSummonOrSurvivalPath), BossMonitoringProductionHasNoSummonOrSurvivalPath);
            Run(nameof(GravityDashMotionContracts), GravityDashMotionContracts);
            Run(nameof(GrappleMotionContracts), GrappleMotionContracts);
            Run(nameof(GrappleRouteFactoryContracts), GrappleRouteFactoryContracts);
            Run(nameof(BasicHookRescueControllerContracts), BasicHookRescueControllerContracts);
            Run(nameof(NativeGrappleReaderContracts), NativeGrappleReaderContracts);
            Run(nameof(WitchBroomMotionContracts), WitchBroomMotionContracts);
            Run(nameof(WitchBroomRouteBuilderContracts), WitchBroomRouteBuilderContracts);
            Run(nameof(WitchBroomRescueTrajectoryContracts), WitchBroomRescueTrajectoryContracts);
            Run(nameof(ActiveNativeMobilityHandoffContracts), ActiveNativeMobilityHandoffContracts);
            Run(nameof(CombatWeaponSelectionHandoffContracts), CombatWeaponSelectionHandoffContracts);
            Run(nameof(NativeWitchBroomReaderContracts), NativeWitchBroomReaderContracts);
            Run(nameof(PlannerRichWorkloadBenchmark), PlannerRichWorkloadBenchmark);
            Run(nameof(RejectsActivationWithoutBossStart), RejectsActivationWithoutBossStart);
            Run(nameof(EventsAreNotAccepted), EventsAreNotAccepted);
            Run(nameof(AuthorizedSummonEntersWaitingThenCombat), AuthorizedSummonEntersWaitingThenCombat);
            Run(nameof(BossSuccessWithoutDeath), BossSuccessWithoutDeath);
            Run(nameof(MultipleBossesRequireEveryKill), MultipleBossesRequireEveryKill);
            Run(nameof(InterruptedBossIsNotSuccess), InterruptedBossIsNotSuccess);
            Run(nameof(DeathWaitsForRespawnWhileBossContinues), DeathWaitsForRespawnWhileBossContinues);
            Run(nameof(DeathThenBossDespawnIsFailure), DeathThenBossDespawnIsFailure);
            Run(nameof(HitCueDoesNotTerminate), HitCueDoesNotTerminate);
            Run(nameof(LeftmostCurrentlyUsableSummonWins), LeftmostCurrentlyUsableSummonWins);
            Run(nameof(FishronRequiresOceanRodAndTruffleWorm), FishronRequiresOceanRodAndTruffleWorm);
            Run(nameof(NaturalBossScheduleIsAccepted), NaturalBossScheduleIsAccepted);
            Run(nameof(NightSummonsRequireConservativeWorldTimeReserve), NightSummonsRequireConservativeWorldTimeReserve);
            Run(nameof(LateNightKeepsOtherSummonsAndLeftmostUsablePriority), LateNightKeepsOtherSummonsAndLeftmostUsablePriority);
            Run(nameof(NaturalSchedulesHonorNightWindowBoundary), NaturalSchedulesHonorNightWindowBoundary);
            Run(nameof(NaturalMechanicalScheduleMapsOnlyKnownCodes), NaturalMechanicalScheduleMapsOnlyKnownCodes);
            Run(nameof(NaturalMechanicalScheduleWaitsForEveryOtherBoss), NaturalMechanicalScheduleWaitsForEveryOtherBoss);
            Run(nameof(ZenithLacewingIsConservativelyRejectedByDayAndNight), ZenithLacewingIsConservativelyRejectedByDayAndNight);
            Run(nameof(OcramsRazorWorksByDayButSigilHonorsBlockers), OcramsRazorWorksByDayButSigilHonorsBlockers);
            Run(nameof(InterceptLeadsMovingTarget), InterceptLeadsMovingTarget);
            Run(nameof(EveryVanillaBossHasDedicatedStrategy), EveryVanillaBossHasDedicatedStrategy);
            Run(nameof(MechanicalMayhemAndMechdusaAreDistinct), MechanicalMayhemAndMechdusaAreDistinct);
            Run(nameof(UnrelatedMultiBossUsesComposite), UnrelatedMultiBossUsesComposite);
            Run(nameof(RequirementsRejectUndersizedArena), RequirementsRejectUndersizedArena);
            Run(nameof(RequirementsRejectWeakWeapon), RequirementsRejectWeakWeapon);
            Run(nameof(RequirementsRejectWeaponWithoutAmmoBeforeSummoning), RequirementsRejectWeaponWithoutAmmoBeforeSummoning);
            Run(nameof(RequirementsRejectPureMeleeDespiteHighDps), RequirementsRejectPureMeleeDespiteHighDps);
            Run(nameof(VariantPhaseIsTagged), VariantPhaseIsTagged);
            Run(nameof(StuckPlannerDoesNotInventUnknownRecoveryTool), StuckPlannerDoesNotInventUnknownRecoveryTool);
            Run(nameof(PlannerProducesBoundedPlan), PlannerProducesBoundedPlan);
            RunSafetyRegressions();
            RunBeamRegressions();
            RunHostileProjectileMotionRegressions();
            RunMobilityRegressions();
            RunJumpMotionRegressions();
            RunFlightMotionRegressions();
            RunNativeJumpReaderRegressions();
            RunNativeFlightReaderRegressions();
            RunNativeBuffIdentityRegressions();
            RunSupportRegressions();
            RunReflectionRegressions();
            RunWeaponProfileRegressions();
            RunSnowballCannonRegressions();
            RunDiamondStaffRegressions();
            RunSimpleMagicPrefixRegressions();
            RunDemonScytheRegressions();
            RunUnholyTridentRegressions();
            RunAquaScepterRegressions();
            RunNativeTrajectoryGateRegressions();
            RunOutputRouteRegressions();
            RunNativeWindEmissionGateRegressions();
            RunCommonWeaponOutputRegressions();
            Run(nameof(SummonWhipOutputContracts), SummonWhipOutputContracts);
            RunSummonWhipProductionRegressions();
            RunWeaponActionGateRegressions();
            RunKingSlimeRegressions();
            RunEyeRegressions();
            RunPriorityBossActiveRecoveryRegressions();
            RunPriorityBossNativeStrategyRegressions();
            RunQueenBeeContactRegressions();
            RunPriorityEmpressMoonStrategyRegressions();
            RunPriorityGroundFishronTrajectoryRegressions();
            RunWallOfFleshRegressions();
            RunPriorityBossNativeContextRegressions();
            RunPriorityBossNativeCaptureRegressions();
            RunTwinsRegressions();
            RunDestroyerStrategyRegressions();
            RunDestroyerObservationRegressions();
            RunQueenSlimeRegressions();
            RunPrimeRegressions();
            RunTransactionRegressions();
            RunPatcherRegressions();
            Run(nameof(SpecialRangedContracts), SpecialRangedContracts);
            Run(nameof(RocketProductionContracts), RocketProductionContracts);
            RunAudioCueRegressions();
            RunSupportedBossPolicyRegressions();
            RunMeleeProjectileRegressions();

            Console.WriteLine($"通过 {_passed}，失败 {_failed}");
            return _failed == 0 ? 0 : 1;
        }

        private static void GravityDashMotionContracts()
        {
            Equal(12, GravityDashMotionTests.RunAll());
        }

        private static void SpecialRangedContracts()
        {
            Equal(13, SpecialRangedContractsTests.RunAll());
        }

        private static void RocketProductionContracts()
        {
            Equal(4, RocketProductionTests.RunAll());
        }


        private static void GrappleMotionContracts()
        {
            Equal(11, GrappleMotionContractTests.RunAll());
        }

        private static void GrappleRouteFactoryContracts()
        {
            Equal(8, GrappleRouteFactoryTests.RunAll());
        }

        private static void BasicHookRescueControllerContracts()
        {
            Equal(7, BasicHookRescueControllerTests.RunAll());
        }

        private static void NativeGrappleReaderContracts()
        {
            Equal(6, NativeGrappleReaderTests.RunAll());
        }

        private static void WitchBroomMotionContracts()
        {
            Equal(22, WitchBroomMotionTests.RunAll());
        }

        private static void WitchBroomRouteBuilderContracts()
        {
            Equal(3, WitchBroomRouteBuilderTests.RunAll());
        }

        private static void WitchBroomRescueTrajectoryContracts()
        {
            Equal(20, WitchBroomRescueTrajectoryTests.RunAll());
        }

        private static void ActiveNativeMobilityHandoffContracts()
        {
            Equal(6, ActiveNativeMobilityHandoffTests.RunAll());
        }

        private static void CombatWeaponSelectionHandoffContracts()
        {
            Equal(5, CombatWeaponSelectionHandoffTests.RunAll());
        }

        private static void NativeWitchBroomReaderContracts()
        {
            Equal(4, NativeWitchBroomReaderTests.RunAll());
        }

        private static void SummonWhipOutputContracts()
        {
            Equal(22, SummonWhipOutputContractTests.RunAll());
        }

        private static void RejectsActivationWithoutBossStart()
        {
            var controller = new EncounterController(2);
            var result = controller.Activate(Observe());
            Equal(SessionState.RejectedNoEncounter, result.Current);
            Equal(AudioCue.NoSlimeAng, result.Cue);
            False(controller.IsControlling);
        }

        private static void EventsAreNotAccepted()
        {
            var controller = new EncounterController(2);
            var result = controller.Activate(Observe(events: EncounterFlags.PumpkinMoon));
            Equal(SessionState.RejectedNoEncounter, result.Current);
        }

        private static void AuthorizedSummonEntersWaitingThenCombat()
        {
            var controller = new EncounterController(2);
            var start = controller.Activate(Observe(startAuthorized: true));
            Equal(SessionState.PreparingBoss, start.Current);
            controller.MarkSummonIssued();
            Equal(SessionState.AwaitingBossSpawn, controller.State);
            var waiting = controller.Update(Observe(startAuthorized: true));
            Equal(SessionState.AwaitingBossSpawn, waiting.Current);
            var engaged = controller.Update(Observe(bosses: new[] { 7 }));
            Equal(SessionState.EngagedAlive, engaged.Current);
        }

        private static void BossSuccessWithoutDeath()
        {
            var controller = new EncounterController(2);
            controller.Activate(Observe(bosses: new[] { 7 }, startAuthorized: true));
            controller.Update(Observe(killed: new[] { 7 }));
            controller.Update(Observe());
            var result = controller.Update(Observe());
            Equal(SessionState.SuccessNoDeath, result.Current);
            Equal(AudioCue.MambaOut, result.Cue);
        }

        private static void MultipleBossesRequireEveryKill()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 11, 12, 13 }, startAuthorized: true));
            controller.Update(Observe(killed: new[] { 11, 12 }));
            var result = controller.Update(Observe());
            Equal(SessionState.EncounterInterrupted, result.Current);
            controller.ReturnToIdle();
            controller.Activate(Observe(bosses: new[] { 11, 12, 13 }, startAuthorized: true));
            controller.Update(Observe(killed: new[] { 11, 12, 13 }));
            result = controller.Update(Observe());
            Equal(SessionState.SuccessNoDeath, result.Current);
        }

        private static void InterruptedBossIsNotSuccess()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 2 }, startAuthorized: true));
            controller.Update(Observe());
            var result = controller.Update(Observe());
            Equal(SessionState.EncounterInterrupted, result.Current);
        }

        private static void DeathWaitsForRespawnWhileBossContinues()
        {
            var controller = new EncounterController(2);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            var dead = controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0));
            Equal(SessionState.EngagedDeadWaitingRespawn, dead.Current);
            Equal(AudioCue.Dead, dead.Cue);
            var alive = controller.Update(Observe(bosses: new[] { 4 }));
            Equal(SessionState.EngagedAlive, alive.Current);
            False(alive.ApplyControls);
            var settled = controller.Update(Observe(bosses: new[] { 4 }));
            Equal(SessionState.EngagedAlive, settled.Current);
            True(settled.ApplyControls);
        }

        private static void DeathThenBossDespawnIsFailure()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0));
            controller.Update(Observe(dead: true, life: 0));
            var result = controller.Update(Observe(dead: true, life: 0));
            Equal(SessionState.FailedAfterDeath, result.Current);
            Equal(AudioCue.LowLevelChaite, result.Cue);
        }

        private static void HitCueDoesNotTerminate()
        {
            var controller = new EncounterController(2);
            controller.Activate(Observe(bosses: new[] { 9 }, startAuthorized: true));
            var hurt = controller.Update(Observe(bosses: new[] { 9 }, life: 360));
            Equal(AudioCue.Man, hurt.Cue);
            True(hurt.ApplyControls);
        }

        private static void LeftmostCurrentlyUsableSummonWins()
        {
            var context = new BossStartContext { HardMode = true, DayTime = false };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 2673, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 2, Type = 560, Stack = 1 });
            var plan = BossStartPlanner.Select(context);
            Equal(2, plan.SummonSlot);
            Equal("slime-crown", plan.Id);
        }

        private static void FishronRequiresOceanRodAndTruffleWorm()
        {
            var context = new BossStartContext
            {
                HardMode = true,
                ZoneBeach = true,
                OceanWater = true,
                FishingRodHotbarSlot = 4,
                OceanWaterWorld = new Vec2(100, 200)
            };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 1, Type = 2673, Stack = 2 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 4, Type = 2291, Stack = 1 });
            var plan = BossStartPlanner.Select(context);
            Equal(BossSummonKind.TruffleWormFishing, plan.Kind);
            Equal(1, plan.SummonSlot);
            Equal(4, plan.ActionSlot);
            Equal(370, plan.ExpectedBossType);
            context.OceanWater = false;
            context.ZoneBeach = false;
            Equal(null, BossStartPlanner.Select(context));
        }

        private static void NaturalBossScheduleIsAccepted()
        {
            var context = new BossStartContext { HardMode = true, DayTime = false, SpawnHardBoss = 2 };
            var plan = BossStartPlanner.Select(context);
            Equal(BossSummonKind.NaturalMechanicalBoss, plan.Kind);
            Equal(125, plan.ExpectedBossType);
        }

        private static void NightSummonsRequireConservativeWorldTimeReserve()
        {
            // These are Chaite's conservative world-time admission limits, not
            // vanilla's item usability rules or a guaranteed wall-clock budget.
            foreach (var item in new[] { 43, 544, 556, 557, 4961 })
            {
                var context = new BossStartContext { ZoneHallow = true, ZoneOverworld = true };
                context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = item, Stack = 1 });
                foreach (var time in new[] { 0d, 25199d, 32400d - 7200d })
                {
                    context.Time = time;
                    True(BossStartPlanner.Select(context) != null, "night boundary rejected item " + item + " at " + time);
                }
                foreach (var time in new[] { 25200.01d, 25201d, 32400d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    context.Time = time;
                    True(BossStartPlanner.Select(context) == null, "unsafe night window accepted item " + item + " at " + time);
                }
                context.Time = 0d;
                context.DayTime = true;
                if (item == 4961)
                    Equal(BossSummonKind.PrismaticLacewing,
                        BossStartPlanner.Select(context).Kind);
                else
                    True(BossStartPlanner.Select(context) == null);
            }
        }

        private static void LateNightKeepsOtherSummonsAndLeftmostUsablePriority()
        {
            var context = new BossStartContext { Time = 32400d, ZoneHallow = true };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 43, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 2, Type = 560, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 4, Type = 4988, Stack = 1 });
            Equal("slime-crown", BossStartPlanner.Select(context).Id);
            Equal(2, BossStartPlanner.Select(context).SummonSlot);
            context.Hotbar.RemoveAt(1);
            context.DayTime = true;
            Equal("queen-slime-crystal", BossStartPlanner.Select(context).Id);
            Equal(4, BossStartPlanner.Select(context).SummonSlot);
        }

        private static void NaturalSchedulesHonorNightWindowBoundary()
        {
            foreach (var eye in new[] { false, true })
            {
                var context = new BossStartContext
                {
                    SpawnEyeScheduled = eye, SpawnHardBoss = eye ? 0 : 1, Time = 25200d
                };
                True(BossStartPlanner.Select(context) != null);
                foreach (var time in new[] { 25200.01d, 32400d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    context.Time = time;
                    True(BossStartPlanner.Select(context) == null);
                }
                context.Time = 0d;
                context.DayTime = true;
                True(BossStartPlanner.Select(context) == null);
            }
        }

        private static void NaturalMechanicalScheduleMapsOnlyKnownCodes()
        {
            var expected = new[] { 134, 125, 127 };
            foreach (var zenith in new[] { false, true })
            {
                var context = new BossStartContext { ZenithWorld = zenith, Time = 1000d };
                for (var code = 1; code <= 3; code++)
                {
                    context.SpawnHardBoss = code;
                    var plan = BossStartPlanner.Select(context);
                    True(plan != null);
                    Equal(BossSummonKind.NaturalMechanicalBoss, plan.Kind);
                    Equal(zenith ? 127 : expected[code - 1], plan.ExpectedBossType);
                    Equal(zenith ? "natural-mechdusa" : "natural-mechanical", plan.Id);
                }
                foreach (var code in new[] { -1, 0, 4, int.MaxValue })
                {
                    context.SpawnHardBoss = code;
                    True(BossStartPlanner.Select(context) == null);
                }
            }
        }

        private static void NaturalMechanicalScheduleWaitsForEveryOtherBoss()
        {
            foreach (var zenith in new[] { false, true })
            foreach (var schedule in new[] { 1, 2, 3 })
            {
                var context = new BossStartContext { SpawnHardBoss = schedule, ZenithWorld = zenith };
                foreach (var boss in new[] { 4, 50, 125, 126, 127, 134, 657 })
                {
                    context.ActiveBossTypes.Add(boss);
                    True(BossStartPlanner.Select(context) == null, "natural mechanical spawn must wait while Boss " + boss + " is alive");
                    context.ActiveBossTypes.Clear();
                    True(BossStartPlanner.Select(context) != null);
                }
                // This native scheduling restriction is not a blanket ban on
                // direct item summons or on an independently scheduled Eye.
                context.ActiveBossTypes.Add(4);
                context.Hotbar.Add(new HotbarItemSnapshot { Slot = 3, Type = 560, Stack = 1 });
                Equal("slime-crown", BossStartPlanner.Select(context).Id);
            }
            var eye = new BossStartContext { SpawnEyeScheduled = true };
            eye.ActiveBossTypes.Add(50);
            Equal(BossSummonKind.NaturalEye, BossStartPlanner.Select(eye).Kind);
            eye.ActiveBossTypes.Add(4);
            True(BossStartPlanner.Select(eye) == null);
        }

        private static void ZenithLacewingIsConservativelyRejectedByDayAndNight()
        {
            var context = new BossStartContext { ZoneHallow = true, ZoneOverworld = true, Time = 1000d };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 4961, Stack = 1 });
            Equal(BossSummonKind.PrismaticLacewing, BossStartPlanner.Select(context).Kind);
            context.DayTime = true;
            Equal(BossSummonKind.PrismaticLacewing,
                BossStartPlanner.Select(context).Kind);
            context.ZenithWorld = true;
            True(BossStartPlanner.Select(context) == null);
            context.DayTime = false;
            True(BossStartPlanner.Select(context) == null);
            // Keep the separate, valid Zenith daytime Mechdusa workflow intact.
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 4, Type = 5334, Stack = 1 });
            Equal("mechdusa-summon", BossStartPlanner.Select(context).Id);
        }

        private static void OcramsRazorWorksByDayButSigilHonorsBlockers()
        {
            var context = new BossStartContext { ZenithWorld = true, DayTime = true };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 1, Type = 5334, Stack = 1 });
            Equal("mechdusa-summon", BossStartPlanner.Select(context).Id);

            context = new BossStartContext { HardMode = true, DownedGolemBoss = true, BlockingInvasion = true };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 3601, Stack = 1 });
            Equal(null, BossStartPlanner.Select(context));
        }

        private static void InterceptLeadsMovingTarget()
        {
            var aim = InterceptSolver.PredictAim(new Vec2(0, 0), new Vec2(100, 0), new Vec2(0, 2), 10);
            True(aim.Y > 0f);
            True(aim.X >= 99.9f);
        }

        private static void EveryVanillaBossHasDedicatedStrategy()
        {
            var cases = new Dictionary<int, string>
            {
                [50] = "king-slime", [4] = "eye-of-cthulhu", [13] = "eater-of-worlds",
                [266] = "brain-of-cthulhu", [222] = "queen-bee", [35] = "skeletron",
                [668] = "deerclops", [113] = "wall-of-flesh", [657] = "queen-slime",
                [125] = "twins", [134] = "destroyer", [127] = "skeletron-prime",
                [262] = "plantera", [245] = "golem", [370] = "duke-fishron",
                [636] = "empress-of-light", [439] = "lunatic-cultist", [398] = "moon-lord"
            };
            foreach (var pair in cases)
            {
                var scenario = CombatScenario(pair.Key);
                var decision = new BossStrategyEngine().Evaluate(scenario);
                True(decision != null, "missing strategy for boss " + pair.Key);
                Equal(pair.Value, decision.Directive.StrategyId);
            }
        }

        private static void MechanicalMayhemAndMechdusaAreDistinct()
        {
            var mayhem = CombatScenario(125, 134, 127);
            Equal("mechanical-mayhem", new CombatPlanner(new PlannerSettings()).Plan(mayhem).StrategyId);
            var mechdusa = CombatScenario(125, 134, 127);
            mechdusa.Difficulty.Zenith = true;
            Equal("mechdusa", new CombatPlanner(new PlannerSettings()).Plan(mechdusa).StrategyId);
        }

        private static void UnrelatedMultiBossUsesComposite()
        {
            var scenario = CombatScenario(50, 4);
            Equal("multi-boss-composite", new CombatPlanner(new PlannerSettings()).Plan(scenario).StrategyId);
        }

        private static void RequirementsRejectUndersizedArena()
        {
            var scenario = CombatScenario(370);
            scenario.Arena.ClearanceLeft = 100;
            scenario.Arena.ClearanceRight = 100;
            string reason;
            False(new CombatPlanner(new PlannerSettings()).RequirementsMet(scenario, out reason));
            True(reason.Contains("横向"));
        }

        private static void RequirementsRejectWeakWeapon()
        {
            var scenario = CombatScenario(398);
            ConfigureReviewedFlight(scenario, 1f);
            scenario.Weapon.Damage = 10;
            scenario.Weapon.UseTime = 60;
            string reason;
            False(new CombatPlanner(new PlannerSettings()).RequirementsMet(scenario, out reason));
            True(reason.Contains("输出"));
        }

        private static void RequirementsRejectWeaponWithoutAmmoBeforeSummoning()
        {
            var scenario = CombatScenario(4);
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            True(planner.RequirementsMetForExpected(scenario, "suspicious-eye", 4, out reason));
            scenario.Weapon.HasAmmo = false;
            False(planner.RequirementsMetForExpected(scenario, "suspicious-eye", 4, out reason));
            True(!string.IsNullOrEmpty(reason));
            False(planner.RequirementsMet(scenario, out reason));
            scenario.Weapon.HasAmmo = true;
            True(planner.RequirementsMetForExpected(scenario, "suspicious-eye", 4, out reason));
        }

        private static void RequirementsRejectPureMeleeDespiteHighDps()
        {
            foreach (var boss in new[] { 4, 50, 657, 134, 125, 127 })
            {
                // Destroyer P1 deliberately requires its exact reviewed native
                // mobility/branch fixture before any summon can be consumed.
                var scenario = boss == 134 ? DestroyerP1Snapshot() :
                    boss == 125 ? TwinsSnapshot() : CombatScenario(boss);
                if (boss == 127) ConfigureReviewedFlight(scenario, 1f);
                if (boss == 657) ConfigureReviewedFlight(scenario, 1f);
                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                True(planner.RequirementsMetForExpected(scenario, "test-direct-summon", boss, out reason),
                    "boss " + boss + ": " + reason);
                scenario.Weapon.Damage = 999;
                scenario.Weapon.UseTime = 1;
                scenario.Weapon.IsProjectile = false;
                scenario.Weapon.IsMelee = true;
                scenario.Weapon.ShootSpeed = 0f;
                False(planner.RequirementsMetForExpected(scenario, "test-direct-summon", boss, out reason));
                True(reason.Contains("挥砍"));
                False(planner.RequirementsMet(scenario, out reason));
                scenario.Weapon.IsProjectile = true;
                scenario.Weapon.IsMelee = false;
                True(planner.RequirementsMetForExpected(scenario, "test-direct-summon", boss, out reason));
            }
        }

        private static void VariantPhaseIsTagged()
        {
            var scenario = CombatScenario(4);
            scenario.Difficulty.Master = true;
            var plan = new CombatPlanner(new PlannerSettings()).Plan(scenario);
            True(plan.PhaseId.StartsWith("master-"));
        }

        private static void StuckPlannerDoesNotInventUnknownRecoveryTool()
        {
            // Use a fully reviewed Eye fixture. Plantera now correctly
            // fails closed when its native phase contract is absent, so it
            // cannot exercise the generic stuck-recovery path at all.
            var scenario = CombatScenario(4);
            scenario.Arena.GrappleAnchors.Add(new Vec2(1680, 680));
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 1000000f });
            string reason;
            True(planner.PrepareForExpectedEncounter(scenario, "test-eye",
                4, out reason), reason);
            planner.ResetForBossArrival();
            DepleteReviewedFlight(scenario);
            ControlPlan plan = default(ControlPlan);
            var recoveryTrace = new List<string>();
            for (var i = 0; i < 24; i++)
            {
                plan = planner.Plan(scenario);
                False(plan.Hook);
                recoveryTrace.Add(i + ":" + plan.TacticalMode + "/" +
                    plan.StrategyId + "/" + plan.PhaseId + "/h=" +
                    plan.Horizontal + "/j=" + plan.Jump);
            }
            True(plan.TacticalMode == TacticalMode.RecoverToPattern,
                "expected recovery without an invented hook; trace=" +
                string.Join(",", recoveryTrace));
        }

        private static void PlannerProducesBoundedPlan()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(CombatScenario(4));
            True(plan.Horizontal >= -1 && plan.Horizontal <= 1);
            True(plan.Fire);
            False(float.IsNaN(plan.RiskScore));
        }

        private static CombatSnapshot CombatScenario(params int[] bossTypes)
        {
            var snapshot = new CombatSnapshot
            {
                Player = new PlayerSnapshot
                {
                    Position = new Vec2(1500, 800), Velocity = new Vec2(0, 0), Width = 20, Height = 42,
                    Life = 400, MaxLife = 500, Mana = 100, MaxMana = 200, Gravity = .4f,
                    MaxFallSpeed = 10f, MaxRunSpeed = 8f, RunAcceleration = .2f,
                    JumpSpeedBoost = 2f, WingTime = 100f, WorldLeft = 16, WorldRight = 10000,
                    WorldTop = 16, WorldBottom = 5000, OnGround = true
                },
                Mobility = new MobilitySnapshot
                {
                    CanDash = true, DashReady = true, HasGrapple = true,
                    HasFiniteFlightResource = true, FlightResourceFraction = 1f, MountRunSpeed = 8f,
                    // Fishron admission includes the reviewed bubble clearance,
                    // so the shared fixture carries the potion stock a real run
                    // is required to bring.
                    InfernoPotionStockKnown = true, InfernoPotionStock = 3
                },
                Arena = new ArenaSnapshot
                {
                    LocalOpenBounds = new RectF(0, 0, 5000, 2400),
                    SafeCenter = new Vec2(2500, 1200),
                    ClearanceLeft = 1500, ClearanceRight = 2000,
                    ClearanceUp = 800, ClearanceDown = 1000, HasFloor = true
                },
                Weapon = new WeaponSnapshot
                {
                    Slot = 1, Damage = 80, UseTime = 10, ShootSpeed = 14f,
                    IsProjectile = true, HasAmmo = true, IsUsable = true
                },
                LineOfSightToPrimary = true,
                NativeContextKnown = true,
                NetMode = 0,
                LocalPlayerIndex = 0
            };
            for (var i = 0; i < bossTypes.Length; i++)
            {
                snapshot.Targets.Add(new TargetSnapshot
                {
                    Key = i + 3, Type = bossTypes[i], Position = new Vec2(2150 + i * 80, 650),
                    Velocity = new Vec2(-1, 0), Width = 90, Height = 90,
                    Life = 2000, LifeMax = 3000, Damage = 60, Boss = true, Chaseable = true,
                    Ai0Known = true, Ai1Known = true, Ai2Known = true,
                    Ai3Known = true,
                    NativeDirectionKnown = true, NativeDirection = -1,
                    NativeTimeLeftKnown = true, NativeTimeLeft = 750,
                    NativeTargetKnown = true, NativeTargetPlayerIndex = 0
                });
            }
            if (bossTypes.Length > 1 || Array.Exists(bossTypes, type =>
                type == 657 || type == 125 || type == 127 || type == 134 || type == 262 ||
                type == 370 || type == 636 || type == 439 || type == 398))
                ConfigureReviewedFlight(snapshot, 1f);
            RefreshPriorityNativeContext(snapshot);
            return snapshot;
        }

        private static void RefreshPriorityNativeContext(
            CombatSnapshot snapshot)
        {
            var context = snapshot.PriorityBoss;
            context.Clear();
            context.WorldGeometryKnown = true;
            context.WorldSurfaceTiles = 250d;
            context.WorldWidthTiles = 8400;
            context.WallOfFleshDrawAreaKnown = true;
            context.WallOfFleshDrawAreaTopPixels = 400;
            context.WallOfFleshDrawAreaBottomPixels = 1600;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                switch (target.Type)
                {
                    case 222:
                        var queenFactor = snapshot.Difficulty.ForTheWorthy ?
                            .5f : 0f;
                        context.QueenBees.Add(
                            new QueenBeeNativeEnrageObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                GetGoodWorld = snapshot.Difficulty.ForTheWorthy,
                                NativeEnrageFactor = queenFactor
                            });
                        break;
                    case 113:
                        context.WallOfFleshTunnels.Add(
                            new WallOfFleshTunnelObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                NativeDirectionKnown = true,
                                NativeDirection = target.NativeDirectionKnown ?
                                    target.NativeDirection : 1,
                                DrawAreaTopPixels =
                                    (int)target.Center.Y - 600,
                                DrawAreaBottomPixels =
                                    (int)target.Center.Y + 600
                            });
                        break;
                    case 114:
                        context.WallOfFleshEyes.Add(
                            new WallOfFleshEyeLaserObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                Ai0EyeSide = target.Ai0 == -1f ? -1f : 1f,
                                LocalAi1ChargeTimer = target.LocalAi1,
                                LocalAi2BurstStage = target.LocalAi2,
                                LineOfSightKnown = target.LineOfSightKnown,
                                LineOfSight = target.HasLineOfSight
                            });
                        break;
                    case 370:
                        context.DukeFishrons.Add(
                            new DukeFishronNativeEnrageObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                NativeEnraged = false
                            });
                        break;
                    case 636:
                        var empressEnraged = snapshot.Difficulty.Remix ?
                            false : snapshot.Difficulty.DayTime;
                        context.Empresses.Add(
                            new EmpressNativeCombatObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                DayTime = snapshot.Difficulty.DayTime,
                                RemixWorld = snapshot.Difficulty.Remix,
                                NativeShouldBeEnraged = empressEnraged,
                                Ai0AttackState = target.Ai0,
                                Ai1AttackTimer = target.Ai1,
                                Ai2AttackIndex = target.Ai2,
                                Ai3PhaseAndRage = target.Ai3
                            });
                        break;
                    case 668:
                        context.Deerclopses.Add(
                            new DeerclopsNativeTimerObservation
                            {
                                Known = true,
                                NpcKey = target.Key,
                                Ai0State = target.Ai0,
                                Ai1StateTimer = target.Ai1,
                                LocalAi1MeleeCounter = target.LocalAi1,
                                LocalAi2ShadowHandTimer = target.LocalAi2,
                                LocalAi3DistanceInvulnerabilityTimer =
                                    target.LocalAi3,
                                TimeLeft = target.NativeTimeLeftKnown ?
                                    target.NativeTimeLeft : 750,
                                HomeTileX = 100f,
                                HomeTileY = 100f,
                                NativeDirectionKnown = true,
                                NativeDirection = target.NativeDirectionKnown ?
                                    target.NativeDirection : 1
                            });
                        break;
                }
            }
        }

        private static EncounterObservation Observe(EncounterFlags events = EncounterFlags.None, int[] bosses = null,
            int[] killed = null, bool dead = false, int life = 400, bool startAuthorized = false)
        {
            return new EncounterObservation
            {
                Flags = events | ((bosses?.Length ?? 0) > 0 ? EncounterFlags.Boss : EncounterFlags.None),
                ActiveBossKeys = bosses ?? Array.Empty<int>(),
                KilledBossKeys = killed ?? Array.Empty<int>(),
                PlayerDead = dead,
                PlayerLife = life,
                StartAuthorized = startAuthorized
            };
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        private static void True(bool value, string message = "expected true")
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void False(bool value, string message = "expected false") => True(!value, message);

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"expected {expected}, got {actual}");
        }
    }
}
