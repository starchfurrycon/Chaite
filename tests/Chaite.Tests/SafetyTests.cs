using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSafetyRegressions()
        {
            Run(nameof(ExistingBossAloneDoesNotAuthorizeActivation), ExistingBossAloneDoesNotAuthorizeActivation);
            Run(nameof(EveryEventAloneIsRejected), EveryEventAloneIsRejected);
            Run(nameof(CancelImmediatelyReleasesAndStaysTerminal), CancelImmediatelyReleasesAndStaysTerminal);
            Run(nameof(WaitingTimeoutReleasesWithoutVictory), WaitingTimeoutReleasesWithoutVictory);
            Run(nameof(WaitingDeathThenTimeoutPlaysFailure), WaitingDeathThenTimeoutPlaysFailure);
            Run(nameof(DeathThenConfirmedVictoryUsesCorrectCue), DeathThenConfirmedVictoryUsesCorrectCue);
            Run(nameof(LiveAdditionalBossPreventsVictory), LiveAdditionalBossPreventsVictory);
            Run(nameof(BossReturningDuringGraceResetsClearTimer), BossReturningDuringGraceResetsClearTimer);
            Run(nameof(SessionResetDoesNotCarryOldKillsOrDeath), SessionResetDoesNotCarryOldKillsOrDeath);
            Run(nameof(DeathCueOverridesDamageCue), DeathCueOverridesDamageCue);
            Run(nameof(HotbarOrderingIgnoresEnumerationOrder), HotbarOrderingIgnoresEnumerationOrder);
            Run(nameof(HotbarSummonPrecedesNaturalSchedule), HotbarSummonPrecedesNaturalSchedule);
            Run(nameof(SpecialSummonsRequireTheirInteractions), SpecialSummonsRequireTheirInteractions);
            Run(nameof(LacewingHonorsBiomeTimeAndCritterProtection), LacewingHonorsBiomeTimeAndCritterProtection);
            Run(nameof(EmptyStacksAndNonHotbarSummonsAreIgnored), EmptyStacksAndNonHotbarSummonsAreIgnored);
            Run(nameof(ProjectileOnlyWaitingDoesNotThrowOrFire), ProjectileOnlyWaitingDoesNotThrowOrFire);
            Run(nameof(DeadPlannerNeverAppliesControls), DeadPlannerNeverAppliesControls);
            Run(nameof(InvulnerableBossIsNotFiredUpon), InvulnerableBossIsNotFiredUpon);
            Run(nameof(UnusableWeaponDoesNotFire), UnusableWeaponDoesNotFire);
            Run(nameof(PlannerResetMatchesFreshSession), PlannerResetMatchesFreshSession);
            Run(nameof(EveryBossDifficultyPhaseProducesFiniteControls), EveryBossDifficultyPhaseProducesFiniteControls);
            Run(nameof(FlightExhaustionStopsSustainedJump), FlightExhaustionStopsSustainedJump);
            Run(nameof(SelectedTargetVisibilityOverridesPrimarySnapshotFlag), SelectedTargetVisibilityOverridesPrimarySnapshotFlag);
            Run(nameof(ExistingBossFamilyBlocksDuplicateSummon), ExistingBossFamilyBlocksDuplicateSummon);
            Run(nameof(UnrelatedBossDoesNotSkipSummonPreparation), UnrelatedBossDoesNotSkipSummonPreparation);
            Run(nameof(FastCrossingProjectileTriggersEmergency), FastCrossingProjectileTriggersEmergency);
            Run(nameof(ArenaReversalDoesNotImmediatelyOscillateBack), ArenaReversalDoesNotImmediatelyOscillateBack);
            Run(nameof(RespawnDoesNotPlayHitCue), RespawnDoesNotPlayHitCue);
            Run(nameof(SafePatternEvaluatesOnlyOneCandidate), SafePatternEvaluatesOnlyOneCandidate);
            Run(nameof(BossPhaseTransitionPreservesDashMemory), BossPhaseTransitionPreservesDashMemory);
            Run(nameof(FishronThirdPhaseHonorsCurrentVersionBoundary), FishronThirdPhaseHonorsCurrentVersionBoundary);
            Run(nameof(RecoveryDoesNotFireHookAtUnreachableAnchor), RecoveryDoesNotFireHookAtUnreachableAnchor);
            Run(nameof(InstantExpectedBossKillStillRequiresGraceAndConfirmation), InstantExpectedBossKillStillRequiresGraceAndConfirmation);
            Run(nameof(OptimizedPlannerMatchesReferenceAcrossDeterministicScenes), OptimizedPlannerMatchesReferenceAcrossDeterministicScenes);
        }

        private static void ExistingBossAloneDoesNotAuthorizeActivation()
        {
            var controller = new EncounterController(2);
            var result = controller.Activate(Observe(bosses: new[] { 4 }));
            Equal(SessionState.RejectedNoEncounter, result.Current);
            Equal(AudioCue.NoSlimeAng, result.Cue);
            False(result.ApplyControls);
            False(controller.IsControlling);
        }

        private static void EveryEventAloneIsRejected()
        {
            foreach (EncounterFlags flag in Enum.GetValues(typeof(EncounterFlags)))
            {
                if (flag == EncounterFlags.None || flag == EncounterFlags.Boss) continue;
                var result = new EncounterController(1).Activate(Observe(events: flag));
                Equal(SessionState.RejectedNoEncounter, result.Current);
                False(result.ApplyControls);
            }
        }

        private static void CancelImmediatelyReleasesAndStaysTerminal()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(startAuthorized: true));
            controller.MarkSummonIssued();
            var result = controller.Cancel();
            Equal(SessionState.Cancelled, result.Current);
            True(result.BecameTerminal);
            False(result.ApplyControls);
            False(controller.IsControlling);
            result = controller.Update(Observe(bosses: new[] { 4 }));
            Equal(SessionState.Cancelled, result.Current);
            False(result.ApplyControls);
            Equal(AudioCue.None, result.Cue);
        }

        private static void WaitingTimeoutReleasesWithoutVictory()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(startAuthorized: true));
            controller.MarkSummonIssued();
            var expired = Observe();
            expired.WaitingStillValid = false;
            var result = controller.Update(expired);
            Equal(SessionState.EncounterInterrupted, result.Current);
            False(result.ApplyControls);
            True(result.BecameTerminal);
            Equal(AudioCue.None, result.Cue);
        }

        private static void WaitingDeathThenTimeoutPlaysFailure()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(startAuthorized: true));
            var dead = controller.Update(Observe(dead: true, life: 0));
            Equal(AudioCue.Dead, dead.Cue);
            False(dead.ApplyControls);
            var expired = Observe(dead: true, life: 0);
            expired.WaitingStillValid = false;
            var result = controller.Update(expired);
            Equal(SessionState.FailedAfterDeath, result.Current);
            Equal(AudioCue.LowLevelChaite, result.Cue);
        }

        private static void DeathThenConfirmedVictoryUsesCorrectCue()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0));
            controller.Update(Observe(bosses: new[] { 4 }));
            controller.Update(Observe(killed: new[] { 4 }));
            var result = controller.Update(Observe());
            Equal(SessionState.SuccessAfterDeath, result.Current);
            Equal(AudioCue.FailedBossDesign, result.Cue);
            False(result.ApplyControls);
        }

        private static void LiveAdditionalBossPreventsVictory()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 125 }, startAuthorized: true));
            controller.Update(Observe(bosses: new[] { 125, 126, 134, 127 }));
            controller.Update(Observe(bosses: new[] { 127 }, killed: new[] { 125, 126, 134 }));
            for (var tick = 0; tick < 8; tick++)
            {
                var result = controller.Update(Observe(bosses: new[] { 127 }));
                Equal(SessionState.EngagedAlive, result.Current);
                False(result.BecameTerminal);
                True(result.ApplyControls);
            }
        }

        private static void BossReturningDuringGraceResetsClearTimer()
        {
            var controller = new EncounterController(2);
            controller.Activate(Observe(bosses: new[] { 7 }, startAuthorized: true));
            controller.Update(Observe());
            controller.Update(Observe());
            Equal(SessionState.EngagedAlive, controller.Update(Observe(bosses: new[] { 7 })).Current);
            False(controller.Update(Observe()).BecameTerminal);
            False(controller.Update(Observe()).BecameTerminal);
            Equal(SessionState.EncounterInterrupted, controller.Update(Observe()).Current);
        }

        private static void SessionResetDoesNotCarryOldKillsOrDeath()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            controller.Update(Observe(killed: new[] { 4 }, dead: true, life: 0));
            controller.Cancel();
            controller.ReturnToIdle();
            False(controller.DiedDuringSession);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            controller.Update(Observe());
            Equal(SessionState.EncounterInterrupted, controller.Update(Observe()).Current);
        }

        private static void DeathCueOverridesDamageCue()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            Equal(AudioCue.Dead, controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0)).Cue);
            Equal(AudioCue.None, controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0)).Cue);
        }

        private static void HotbarOrderingIgnoresEnumerationOrder()
        {
            var context = new BossStartContext { DayTime = false };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 8, Type = 560, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 2, Type = 43, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 5, Type = 560, Stack = 1 });
            Equal(2, BossStartPlanner.Select(context).SummonSlot);
        }

        private static void HotbarSummonPrecedesNaturalSchedule()
        {
            var context = new BossStartContext { DayTime = false, SpawnEyeScheduled = true };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 9, Type = 560, Stack = 1 });
            var result = BossStartPlanner.Select(context);
            Equal(BossSummonKind.DirectItem, result.Kind);
            Equal(9, result.ActionSlot);
        }

        private static void SpecialSummonsRequireTheirInteractions()
        {
            var altar = new BossStartContext();
            altar.Hotbar.Add(new HotbarItemSnapshot { Slot = 1, Type = 1293, Stack = 1 });
            Equal(null, BossStartPlanner.Select(altar));
            altar.NearLihzahrdAltar = true;
            altar.AltarWorld = new Vec2(123f, 456f);
            Equal(BossSummonKind.LihzahrdAltar, BossStartPlanner.Select(altar).Kind);
            Equal(123f, BossStartPlanner.Select(altar).InteractionWorld.X);

            var doll = new BossStartContext { GuideAlive = true, ZoneUnderworld = true };
            doll.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 267, Stack = 1 });
            Equal(null, BossStartPlanner.Select(doll));
            doll.NearbyLava = true;
            Equal(BossSummonKind.GuideVoodooDoll, BossStartPlanner.Select(doll).Kind);
            doll.GuideAlive = false;
            Equal(null, BossStartPlanner.Select(doll));
        }

        private static void LacewingHonorsBiomeTimeAndCritterProtection()
        {
            var context = new BossStartContext { ZoneHallow = true, ZoneOverworld = true, DayTime = false };
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 1, Type = 4961, Stack = 1 });
            Equal(BossSummonKind.PrismaticLacewing, BossStartPlanner.Select(context).Kind);
            context.CritterProtection = true;
            Equal(null, BossStartPlanner.Select(context));
            context.CritterProtection = false;
            context.DayTime = true;
            Equal(null, BossStartPlanner.Select(context));
            context.DayTime = false;
            context.ZoneHallow = false;
            Equal(null, BossStartPlanner.Select(context));
        }

        private static void EmptyStacksAndNonHotbarSummonsAreIgnored()
        {
            var context = new BossStartContext();
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 560, Stack = 0 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = 10, Type = 560, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot { Slot = -1, Type = 560, Stack = 1 });
            Equal(null, BossStartPlanner.Select(context));
        }

        private static void ProjectileOnlyWaitingDoesNotThrowOrFire()
        {
            var scenario = CombatScenario();
            scenario.Threats.Add(new ThreatSnapshot { Kind = ThreatKind.Projectile,
                Position = scenario.Player.Position + new Vec2(120, 0), Velocity = new Vec2(-12, 0),
                Width = 20, Height = 20, Damage = 80, TimeLeft = 120 });
            var planner = new CombatPlanner(new PlannerSettings());
            for (var tick = 0; tick < 5; tick++)
            {
                var plan = planner.PlanSurvival(scenario);
                AssertFinitePlan(plan);
                False(plan.Fire);
                Equal(-1, plan.TargetKey);
                Equal(TacticalMode.AwaitingBoss, plan.TacticalMode);
            }
        }

        private static void DeadPlannerNeverAppliesControls()
        {
            var scenario = CombatScenario(4);
            scenario.Player.Dead = true;
            scenario.Player.Life = 0;
            var planner = new CombatPlanner(new PlannerSettings());
            AssertNeutralPlan(planner.Plan(scenario));
            AssertNeutralPlan(planner.PlanSurvival(scenario));
            AssertNeutralPlan(planner.Plan(null));
        }

        private static void InvulnerableBossIsNotFiredUpon()
        {
            var scenario = CombatScenario(439);
            var target = scenario.Targets[0];
            target.Invulnerable = true;
            scenario.Targets[0] = target;
            var plan = new CombatPlanner(new PlannerSettings()).Plan(scenario);
            False(plan.Fire);
            Equal(target.Key, plan.TargetKey);
        }

        private static void UnusableWeaponDoesNotFire()
        {
            var scenario = CombatScenario(4);
            scenario.Weapon.IsUsable = false;
            False(new CombatPlanner(new PlannerSettings()).Plan(scenario).Fire);
            scenario.Weapon.IsUsable = true;
            scenario.Weapon.HasAmmo = false;
            False(new CombatPlanner(new PlannerSettings()).Plan(scenario).Fire);
        }

        private static void PlannerResetMatchesFreshSession()
        {
            var scenario = CombatScenario(262);
            var planner = new CombatPlanner(new PlannerSettings());
            for (var i = 0; i < 35; i++) planner.Plan(scenario);
            planner.Reset();
            var reset = planner.Plan(scenario);
            var fresh = new CombatPlanner(new PlannerSettings()).Plan(scenario);
            Equal(fresh.Horizontal, reset.Horizontal);
            Equal(fresh.Jump, reset.Jump);
            Equal(fresh.Drop, reset.Drop);
            Equal(fresh.TacticalMode, reset.TacticalMode);
            Equal(fresh.PhaseId, reset.PhaseId);
            Equal(fresh.RiskScore, reset.RiskScore);
        }

        private static void EveryBossDifficultyPhaseProducesFiniteControls()
        {
            var bossTypes = new[] { 50, 4, 13, 266, 222, 35, 668, 113, 657, 125, 134, 127, 262, 245, 370, 636, 439, 398 };
            var checkedCases = 0;
            foreach (var boss in bossTypes)
            for (var difficulty = 0; difficulty < 6; difficulty++)
            for (var phase = 0; phase < 3; phase++)
            {
                var scenario = CombatScenario(boss);
                scenario.Difficulty.Expert = difficulty >= 1;
                scenario.Difficulty.Master = difficulty >= 2;
                scenario.Difficulty.ForTheWorthy = difficulty == 3;
                scenario.Difficulty.Zenith = difficulty == 4;
                scenario.Difficulty.DayTime = difficulty == 5;
                var target = scenario.Targets[0];
                target.Life = phase == 0 ? 2900 : phase == 1 ? 1200 : 200;
                target.Ai0 = phase;
                target.Ai1 = phase;
                target.Velocity = phase == 2 ? new Vec2(15, -4) : new Vec2(-1, 0);
                scenario.Targets[0] = target;
                scenario.Threats.Add(new ThreatSnapshot { Position = new Vec2(1800, 800), Velocity = new Vec2(-5, 2),
                    Width = 20, Height = 20, Damage = 100, TimeLeft = 120, Kind = ThreatKind.Projectile });
                var plan = new CombatPlanner(new PlannerSettings()).Plan(scenario);
                AssertFinitePlan(plan);
                True(!string.IsNullOrEmpty(plan.StrategyId), "missing strategy for boss " + boss);
                Equal(target.Key, plan.TargetKey);
                checkedCases++;
            }
            Equal(324, checkedCases);
        }

        private static void FlightExhaustionStopsSustainedJump()
        {
            var scenario = CombatScenario(262);
            scenario.Player.OnGround = false;
            scenario.Player.WingTime = 0f;
            scenario.Player.RocketTime = 0f;
            scenario.Mobility.FlightResourceFraction = 0f;
            scenario.Mobility.MountCanFly = false;
            scenario.Mobility.CanFlipGravity = false;
            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scenario);
            False(plan.Jump, "flight-empty player should establish a landing/recovery path, not hold jump");
        }

        private static void SelectedTargetVisibilityOverridesPrimarySnapshotFlag()
        {
            var scenario = CombatScenario(4);
            var target = scenario.Targets[0];
            target.LineOfSightKnown = true;
            target.HasLineOfSight = false;
            scenario.Targets[0] = target;
            scenario.LineOfSightToPrimary = true;
            False(new CombatPlanner(new PlannerSettings()).Plan(scenario).Fire);
            target.HasLineOfSight = true;
            scenario.Targets[0] = target;
            scenario.LineOfSightToPrimary = false;
            True(new CombatPlanner(new PlannerSettings()).Plan(scenario).Fire);
        }

        private static void ExistingBossFamilyBlocksDuplicateSummon()
        {
            var twins = new BossStartContext { DayTime = false };
            twins.ActiveBossTypes.Add(126);
            twins.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 544, Stack = 1 });
            Equal(null, BossStartPlanner.Select(twins));
            twins.Hotbar.Add(new HotbarItemSnapshot { Slot = 4, Type = 560, Stack = 1 });
            Equal("slime-crown", BossStartPlanner.Select(twins).Id);

            var worm = new BossStartContext { ZoneCorrupt = true };
            worm.ActiveBossTypes.Add(14);
            worm.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 70, Stack = 1 });
            Equal(null, BossStartPlanner.Select(worm));

            var sigil = new BossStartContext { HardMode = true, DownedGolemBoss = true };
            sigil.ActiveBossTypes.Add(4);
            sigil.Hotbar.Add(new HotbarItemSnapshot { Slot = 0, Type = 3601, Stack = 1 });
            Equal(null, BossStartPlanner.Select(sigil));
        }

        private static void UnrelatedBossDoesNotSkipSummonPreparation()
        {
            var controller = new EncounterController(1);
            var preparing = Observe(bosses: new[] { 4 }, startAuthorized: true);
            preparing.RequirePreparation = true;
            preparing.ExpectedBossArrived = false;
            Equal(SessionState.PreparingBoss, controller.Activate(preparing).Current);
            controller.MarkSummonIssued();
            var waiting = Observe(bosses: new[] { 4 });
            waiting.ExpectedBossArrived = false;
            Equal(SessionState.AwaitingBossSpawn, controller.Update(waiting).Current);
            waiting.ActiveBossKeys = new[] { 4, 50 };
            waiting.ExpectedBossArrived = true;
            Equal(SessionState.EngagedAlive, controller.Update(waiting).Current);
        }

        private static void FastCrossingProjectileTriggersEmergency()
        {
            var scenario = CombatScenario(4);
            scenario.Player.OnGround = false;
            scenario.Player.WingTime = 0;
            scenario.Mobility.FlightResourceFraction = 0;
            scenario.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Position = new Vec2(1200, 810), Velocity = new Vec2(200, 0),
                Width = 16, Height = 16, Damage = 100, TimeLeft = 60
            });
            var plan = new CombatPlanner(new PlannerSettings()).Plan(scenario);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
            True(plan.RiskScore >= 3500f, "fast threat crossing between 3-tick samples must not tunnel through prediction");
        }

        private static void ArenaReversalDoesNotImmediatelyOscillateBack()
        {
            var scenario = CombatScenario(50);
            scenario.Arena.ClearanceLeft = 60;
            var planner = new CombatPlanner(new PlannerSettings());
            Equal(1, planner.Plan(scenario).Horizontal);
            scenario.Arena.ClearanceLeft = 140;
            scenario.Player.Position.X += 5;
            scenario.Player.Velocity.X = 5;
            Equal(1, planner.Plan(scenario).Horizontal);
        }

        private static void RespawnDoesNotPlayHitCue()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(bosses: new[] { 4 }, startAuthorized: true));
            controller.Update(Observe(bosses: new[] { 4 }, dead: true, life: 0));
            var respawned = controller.Update(Observe(bosses: new[] { 4 }, life: 100));
            Equal(SessionState.EngagedAlive, respawned.Current);
            Equal(AudioCue.None, respawned.Cue);

            controller.ReturnToIdle();
            controller.Activate(Observe(startAuthorized: true));
            controller.Update(Observe(dead: true, life: 0));
            Equal(AudioCue.None, controller.Update(Observe(life: 100)).Cue);
        }

        private static void SafePatternEvaluatesOnlyOneCandidate()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var scenario = CombatScenario(50);
            planner.Plan(scenario);
            Equal(1, planner.LastCandidateCount);
        }

        private static void BossPhaseTransitionPreservesDashMemory()
        {
            var memory = new BossMemory();
            memory.Enter("duke-fishron", "phase-1-five-dashes");
            memory.DashCounter = 2;
            memory.Enter("duke-fishron", "cthulunado-cycle");
            Equal(2, memory.DashCounter);
            Equal(0, memory.PhaseTicks);
            memory.Enter("duke-fishron", "cthulunado-cycle");
            Equal(1, memory.PhaseTicks);
            memory.Enter("eye-of-cthulhu", "hover");
            Equal(0, memory.DashCounter);
        }

        private static void FishronThirdPhaseHonorsCurrentVersionBoundary()
        {
            var scenario = CombatScenario(370);
            scenario.Difficulty.Expert = true;
            var target = scenario.Targets[0];
            target.Life = 450;
            target.Velocity = new Vec2(0, 0);
            scenario.Targets[0] = target;
            True(new CombatPlanner(new PlannerSettings()).Plan(scenario).PhaseId.Contains("expert-teleport"));
            target.Life = 451;
            scenario.Targets[0] = target;
            False(new CombatPlanner(new PlannerSettings()).Plan(scenario).PhaseId.Contains("expert-teleport"));
            target.Life = 2800;
            target.Ai0 = 10;
            scenario.Targets[0] = target;
            True(new CombatPlanner(new PlannerSettings()).Plan(scenario).PhaseId.Contains("expert-teleport"));
        }

        private static void RecoveryDoesNotFireHookAtUnreachableAnchor()
        {
            var scenario = CombatScenario(262);
            scenario.Mobility.FlightResourceFraction = 0;
            scenario.Arena.GrappleAnchors.Add(new Vec2(600, 300));
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 1000000f });
            for (var tick = 0; tick < 24; tick++)
                False(planner.Plan(scenario).Hook, "a recovery hook must remain within the conservative equipped-hook range");
        }

        private static void InstantExpectedBossKillStillRequiresGraceAndConfirmation()
        {
            var controller = new EncounterController(1);
            controller.Activate(Observe(startAuthorized: true));
            controller.MarkSummonIssued();
            var kill = Observe(killed: new[] { 50 });
            kill.ExpectedBossArrived = true;
            False(controller.Update(kill).BecameTerminal);
            False(controller.Update(Observe()).BecameTerminal);
            var result = controller.Update(Observe());
            Equal(SessionState.SuccessNoDeath, result.Current);
            Equal(AudioCue.MambaOut, result.Cue);
        }

        private static void OptimizedPlannerMatchesReferenceAcrossDeterministicScenes()
        {
            var random = new Random(731938);
            var bossSets = new[] { new[] { 4 }, new[] { 370 }, new[] { 125, 126, 134, 127 },
                new[] { 50, 4, 370 }, new[] { 636 }, new[] { 262 }, new[] { 398 }, new[] { 668 } };
            var checkedPlans = 0;
            for (var scene = 0; scene < 24; scene++)
            {
                var snapshot = CombatScenario(bossSets[scene % bossSets.Length]);
                snapshot.Difficulty.Expert = scene % 2 == 0;
                snapshot.Difficulty.Master = scene % 3 == 0;
                snapshot.Difficulty.Zenith = scene % 7 == 0;
                snapshot.Mobility.GravityInverted = scene % 4 == 0;
                snapshot.Player.OnGround = scene % 3 == 0;
                snapshot.Arena.GrappleAnchors.Add(new Vec2(1650, 650));
                var horizon = 18 + scene % 4 * 12;
                var step = 1 + scene % 4;
                var optimized = new CombatPlanner(new PlannerSettings { HorizonTicks = horizon, SimulationStepTicks = step });
                var reference = new CombatPlanner(new PlannerSettings { HorizonTicks = horizon, SimulationStepTicks = step,
                    EnableScorePruning = false, CacheThreatPrediction = false });
                for (var frame = 0; frame < 12; frame++)
                {
                    snapshot.Player.Position.X = 1450 + random.Next(150);
                    snapshot.Player.Position.Y = 760 + random.Next(80);
                    snapshot.Player.Velocity = new Vec2(random.Next(-10, 11), random.Next(-6, 7));
                    snapshot.Player.WingTime = random.Next(0, 101);
                    snapshot.Mobility.FlightResourceFraction = snapshot.Player.WingTime / 100f;
                    snapshot.Threats.Clear();
                    var threats = frame == 0 ? 0 : 10 + random.Next(100);
                    for (var i = 0; i < threats; i++)
                    {
                        snapshot.Threats.Add(new ThreatSnapshot
                        {
                            Kind = i % 5 == 0 ? ThreatKind.NpcContact : ThreatKind.Projectile,
                            Position = new Vec2(1150 + random.Next(800), 550 + random.Next(550)),
                            Velocity = new Vec2(random.Next(-60, 61), random.Next(-20, 21)),
                            Width = random.Next(8, 70), Height = random.Next(8, 80), Damage = random.Next(1, 160),
                            TimeLeft = i % 7 == 0 ? 1 : i % 5 == 0 ? 0 : random.Next(2, 100),
                            Type = i % 8 == 0 ? 455 : 1
                        });
                    }
                    for (var targetIndex = 0; targetIndex < snapshot.Targets.Count; targetIndex++)
                    {
                        var target = snapshot.Targets[targetIndex];
                        target.Life = random.Next(1, 3001);
                        target.Ai0 = random.Next(0, 13);
                        target.Ai1 = random.Next(0, 4);
                        target.Velocity = new Vec2(random.Next(-16, 17), random.Next(-6, 7));
                        target.LineOfSightKnown = true;
                        target.HasLineOfSight = frame % 3 != 0;
                        snapshot.Targets[targetIndex] = target;
                    }
                    AssertPlansIdentical(reference.Plan(snapshot), optimized.Plan(snapshot), scene, frame);
                    checkedPlans++;
                }
            }
            Equal(288, checkedPlans);
        }

        private static void AssertPlansIdentical(ControlPlan expected, ControlPlan actual, int scene, int frame)
        {
            var mismatch = expected.Horizontal != actual.Horizontal || expected.Jump != actual.Jump || expected.Drop != actual.Drop ||
                expected.Fire != actual.Fire || expected.QuickHeal != actual.QuickHeal || expected.QuickMana != actual.QuickMana ||
                expected.Dash != actual.Dash || expected.Hook != actual.Hook || expected.ToggleMount != actual.ToggleMount ||
                expected.GravityControl != actual.GravityControl || expected.AimWorld.X != actual.AimWorld.X ||
                expected.AimWorld.Y != actual.AimWorld.Y || expected.HookWorld.X != actual.HookWorld.X ||
                expected.HookWorld.Y != actual.HookWorld.Y || expected.TargetKey != actual.TargetKey ||
                expected.PreferredWeaponSlot != actual.PreferredWeaponSlot || expected.TacticalMode != actual.TacticalMode ||
                expected.StrategyId != actual.StrategyId || expected.PhaseId != actual.PhaseId || expected.RiskScore != actual.RiskScore;
            False(mismatch, "optimized/reference divergence at scene " + scene + ", frame " + frame +
                "; scores " + expected.RiskScore + " / " + actual.RiskScore);
        }

        private static void AssertFinitePlan(ControlPlan plan)
        {
            True(plan.Horizontal >= -1 && plan.Horizontal <= 1);
            True(plan.GravityControl >= -1 && plan.GravityControl <= 1);
            False(plan.Jump && plan.Drop, "contradictory jump/drop controls");
            False(float.IsNaN(plan.RiskScore) || float.IsInfinity(plan.RiskScore));
            False(float.IsNaN(plan.AimWorld.X) || float.IsInfinity(plan.AimWorld.X));
            False(float.IsNaN(plan.AimWorld.Y) || float.IsInfinity(plan.AimWorld.Y));
        }

        private static void AssertNeutralPlan(ControlPlan plan)
        {
            Equal(0, plan.Horizontal);
            False(plan.Jump || plan.Drop || plan.Fire || plan.QuickHeal || plan.QuickMana || plan.Dash ||
                plan.Hook || plan.ToggleMount || plan.GravityControl != 0);
        }
    }
}
