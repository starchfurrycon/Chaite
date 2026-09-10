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

        private static int Main()
        {
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
            Run(nameof(StuckPlannerUsesRecoveryTools), StuckPlannerUsesRecoveryTools);
            Run(nameof(PlannerProducesBoundedPlan), PlannerProducesBoundedPlan);
            RunSafetyRegressions();
            RunBeamRegressions();
            RunMobilityRegressions();
            RunSupportRegressions();
            RunReflectionRegressions();
            RunTransactionRegressions();
            RunPatcherRegressions();

            Console.WriteLine($"通过 {_passed}，失败 {_failed}");
            return _failed == 0 ? 0 : 1;
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
            context.ZenithWorld = true;
            True(BossStartPlanner.Select(context) == null);
            context.DayTime = true;
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
                var plan = new CombatPlanner(new PlannerSettings()).Plan(scenario);
                Equal(pair.Value, plan.StrategyId);
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
                var scenario = CombatScenario(boss);
                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                True(planner.RequirementsMetForExpected(scenario, "test-direct-summon", boss, out reason));
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

        private static void StuckPlannerUsesRecoveryTools()
        {
            var scenario = CombatScenario(262);
            scenario.Mobility.FlightResourceFraction = 0f;
            scenario.Arena.GrappleAnchors.Add(new Vec2(1680, 680));
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 1000000f });
            ControlPlan plan = default(ControlPlan);
            var hookPulses = 0;
            for (var i = 0; i < 24; i++)
            {
                plan = planner.Plan(scenario);
                if (plan.Hook)
                {
                    Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
                    hookPulses++;
                }
            }
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            Equal(1, hookPulses); // One recovery pulse; the new cooldown must not spam Hook every frame.
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
                    HasFiniteFlightResource = true, FlightResourceFraction = 1f, MountRunSpeed = 8f
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
                LineOfSightToPrimary = true
            };
            for (var i = 0; i < bossTypes.Length; i++)
            {
                snapshot.Targets.Add(new TargetSnapshot
                {
                    Key = i + 3, Type = bossTypes[i], Position = new Vec2(2150 + i * 80, 650),
                    Velocity = new Vec2(-1, 0), Width = 90, Height = 90,
                    Life = 2000, LifeMax = 3000, Damage = 60, Boss = true, Chaseable = true
                });
            }
            return snapshot;
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
