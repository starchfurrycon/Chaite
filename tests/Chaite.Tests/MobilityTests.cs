using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunMobilityRegressions()
        {
            Run(nameof(BootSprintUsesNativeReducedAcceleration), BootSprintUsesNativeReducedAcceleration);
            Run(nameof(BaseToSprintThresholdIntegratesEachTick), BaseToSprintThresholdIntegratesEachTick);
            Run(nameof(AirborneBootsKeepMomentumWithoutInventingAcceleration), AirborneBootsKeepMomentumWithoutInventingAcceleration);
            Run(nameof(NativeMotionHonorsDebuffsAndReversalBraking), NativeMotionHonorsDebuffsAndReversalBraking);
            Run(nameof(LegacyHorizontalSnapshotsRemainCompatible), LegacyHorizontalSnapshotsRemainCompatible);
            Run(nameof(CalibratedMotionPlannerMatchesReference), CalibratedMotionPlannerMatchesReference);
            Run(nameof(UnknownAttachedGrappleReturnsControlWithoutInputs), UnknownAttachedGrappleReturnsControlWithoutInputs);
            Run(nameof(InactiveUnknownGrappleNeverEmitsHookInput), InactiveUnknownGrappleNeverEmitsHookInput);
            Run(nameof(UnknownOptionalProfilesLeaveControlPlanUnchanged), UnknownOptionalProfilesLeaveControlPlanUnchanged);
            Run(nameof(VanillaMountCatalogContainsEveryIdentity), VanillaMountCatalogContainsEveryIdentity);
            Run(nameof(FacadePublishesKnownActiveMountIdentity), FacadePublishesKnownActiveMountIdentity);
            Run(nameof(FacadePublishesQuickMountSelectionIdentity), FacadePublishesQuickMountSelectionIdentity);
            Run(nameof(FacadeRejectsMismatchedQuickMountPair), FacadeRejectsMismatchedQuickMountPair);
            Run(nameof(UnwiredMountsReturnControlWithoutInputs), UnwiredMountsReturnControlWithoutInputs);
            Run(nameof(DefaultMobilityContractsFailClosed), DefaultMobilityContractsFailClosed);
            Run(nameof(MeasuredCapabilityRejectsNonFiniteValues), MeasuredCapabilityRejectsNonFiniteValues);
            Run(nameof(ModeledSlowNormalizesOnlyExactVanillaImpairment), ModeledSlowNormalizesOnlyExactVanillaImpairment);
            Run(nameof(EffectiveMotionThresholdDoesNotUseInventoryCapabilityUnions), EffectiveMotionThresholdDoesNotUseInventoryCapabilityUnions);
            Run(nameof(CapabilityThresholdSelectsAnyCompleteCertifiedRoute), CapabilityThresholdSelectsAnyCompleteCertifiedRoute);
            Run(nameof(CapabilityThresholdUsesMeasuredDynamicsNotEquipmentNames), CapabilityThresholdUsesMeasuredDynamicsNotEquipmentNames);
            Run(nameof(PlayerOwnedRoutePrecedesOptionalMountAndBurst), PlayerOwnedRoutePrecedesOptionalMountAndBurst);
            Run(nameof(ProductionBossesDeclareCapabilityThresholdsSeparately), ProductionBossesDeclareCapabilityThresholdsSeparately);
            Run(nameof(PriorityFlightSpeedThresholdMatchesNativeLightningDemon), PriorityFlightSpeedThresholdMatchesNativeLightningDemon);
            Run(nameof(ExplicitReviewedRoutesChooseOneWholeProfile), ExplicitReviewedRoutesChooseOneWholeProfile);
            Run(nameof(PlannerLatchesExplicitRouteUntilReset), PlannerLatchesExplicitRouteUntilReset);
            Run(nameof(PlannerLatchesAndRevalidatesSingleRoute), PlannerLatchesAndRevalidatesSingleRoute);
            Run(nameof(PlannerRejectsDifficultyDriftWithoutRouteSwap), PlannerRejectsDifficultyDriftWithoutRouteSwap);
            Run(nameof(OnFootRouteIgnoresDormantFiniteFlight), OnFootRouteIgnoresDormantFiniteFlight);
            Run(nameof(OnFootRouteRejectsUnmodelledFlightEquipment), OnFootRouteRejectsUnmodelledFlightEquipment);
            Run(nameof(PreflightRouteReservationSurvivesBossArrivalReset), PreflightRouteReservationSurvivesBossArrivalReset);
            Run(nameof(RequiredDashBaselineDrivesReviewedBossPhase), RequiredDashBaselineDrivesReviewedBossPhase);
            Run(nameof(WitchBroomCapabilityRemainsNotProductionCertified),
                WitchBroomCapabilityRemainsNotProductionCertified);
            Run(nameof(LateOptionalEdgeUsesOnlyCertifiedFallback), LateOptionalEdgeUsesOnlyCertifiedFallback);
            Run(nameof(TrustyChilletDashIsRevalidatedAfterNativeInputCopy),
                TrustyChilletDashIsRevalidatedAfterNativeInputCopy);
            Run(nameof(LateFeatherFallExpiryNeutralizesEveryInput), LateFeatherFallExpiryNeutralizesEveryInput);
            Run(nameof(DashCandidateRequiresBrakingRoomAndSafeReturn), DashCandidateRequiresBrakingRoomAndSafeReturn);
            Run(nameof(GravityReturnUsesUpForBothDirections), GravityReturnUsesUpForBothDirections);
            Run(nameof(GravityReturnWaitsForNativeReleaseFrame), GravityReturnWaitsForNativeReleaseFrame);
            Run(nameof(CalibratedMotionWorkloadBenchmark), CalibratedMotionWorkloadBenchmark);
        }

        private static PlayerSnapshot RunningBoots()
        {
            return new PlayerSnapshot { BaseRunSpeed = 3f, MaxRunSpeed = 6f,
                RunAcceleration = .08f, SprintAcceleration = .016f, RunSlowdown = .2f };
        }

        private static void BootSprintUsesNativeReducedAcceleration()
        {
            var player = RunningBoots();
            float distance;
            var speed = HorizontalMotion.Advance(player, 3f, 1, true, 30, out distance);
            MotionNear(3.48f, speed);
            MotionNear(97.44f, distance);
            player.SprintAcceleration = .032f;
            player.CanSprintInAir = true;
            speed = HorizontalMotion.Advance(player, 3f, 1, false, 30, out distance);
            MotionNear(3.96f, speed);
            MotionNear(104.88f, distance);
        }

        private static void BaseToSprintThresholdIntegratesEachTick()
        {
            var player = RunningBoots();
            float distance;
            MotionNear(3.072f, HorizontalMotion.Advance(player, 2.96f, 1, true, 3, out distance));
            MotionNear(9.168f, distance);
            MotionNear(-3.072f, HorizontalMotion.Advance(player, -2.96f, -1, true, 3, out distance));
            MotionNear(-9.168f, distance);
        }

        private static void AirborneBootsKeepMomentumWithoutInventingAcceleration()
        {
            var player = RunningBoots();
            float distance;
            MotionNear(4.5f, HorizontalMotion.Advance(player, 4.5f, 1, false, 30, out distance));
            MotionNear(135f, distance);
            MotionNear(3.04f, HorizontalMotion.Advance(player, 2.96f, 1, false, 3, out distance));
            MotionNear(9.12f, distance);
            // Releasing direction still uses native airborne half-drag; merely
            // maintaining direction above the base threshold does not add speed.
            MotionNear(4.2f, HorizontalMotion.Advance(player, 4.5f, 0, false, 3, out distance));
            MotionNear(12.9f, distance);
        }

        private static void NativeMotionHonorsDebuffsAndReversalBraking()
        {
            var player = RunningBoots();
            float distance;
            MotionNear(2.16f, HorizontalMotion.Advance(player, 3f, -1, true, 3, out distance));
            MotionNear(7.32f, distance);
            player.BaseRunSpeed = .6f;
            player.MaxRunSpeed = 1.2f;
            player.RunAcceleration = .01f;
            player.SprintAcceleration = .002f;
            player.RunSlowdown = .05f;
            MotionNear(.03f, HorizontalMotion.Advance(player, 0f, 1, true, 3, out distance));
            MotionNear(.06f, distance);
            MotionNear(.606f, HorizontalMotion.Advance(player, .6f, 1, true, 3, out distance));
            MotionNear(1.812f, distance);
            MotionNear(1.15f, HorizontalMotion.Advance(player, 1.2f, 1, true, 1, out distance, 12f));
        }

        private static void LegacyHorizontalSnapshotsRemainCompatible()
        {
            var random = new Random(308016);
            for (var scene = 0; scene < 128; scene++)
            {
                var player = new PlayerSnapshot { MaxRunSpeed = (float)random.NextDouble() * 9f,
                    RunAcceleration = (float)random.NextDouble() * .3f };
                var current = (float)random.NextDouble() * 20f - 10f;
                var direction = scene % 3 - 1;
                var ticks = 1 + scene % 6;
                var target = direction * Math.Max(2f, player.MaxRunSpeed);
                var acceleration = Math.Max(.08f, player.RunAcceleration) * ticks;
                var expected = Math.Abs(target - current) <= acceleration ? target :
                    current + Math.Sign(target - current) * acceleration;
                float distance;
                Equal(expected, HorizontalMotion.Advance(player, current, direction, scene % 2 == 0, ticks, out distance));
                Equal(expected * ticks, distance);
            }
        }

        private static void CalibratedMotionPlannerMatchesReference()
        {
            for (var sceneIndex = 0; sceneIndex < 24; sceneIndex++)
            {
                var scene = CombatScenario(sceneIndex % 2 == 0 ? 4 : 370);
                scene.Player.BaseRunSpeed = 3f;
                scene.Player.MaxRunSpeed = 6f;
                scene.Player.RunAcceleration = .08f;
                scene.Player.SprintAcceleration = sceneIndex % 2 == 0 ? .016f : .032f;
                scene.Player.RunSlowdown = .2f;
                scene.Player.CanSprintInAir = sceneIndex % 2 != 0;
                scene.Player.OnGround = sceneIndex % 3 == 0;
                scene.Player.Velocity.X = sceneIndex * .4f - 4f;
                for (var i = 0; i < 30; i++)
                    scene.Threats.Add(new ThreatSnapshot { Kind = ThreatKind.Projectile,
                        Position = scene.Player.Position + new Vec2(i * 18 - 240, (i % 4) * 50 - 100),
                        Velocity = new Vec2(7 - i % 15, i % 3 - 1), Width = 16, Height = 16, Damage = 50, TimeLeft = 300 });
                var optimized = new CombatPlanner(new PlannerSettings());
                var reference = new CombatPlanner(new PlannerSettings { CacheThreatPrediction = false, EnableScorePruning = false });
                for (var frame = 0; frame < 4; frame++)
                {
                    scene.Mobility.Grappling = frame == 1 || frame == 2;
                    AssertPlansIdentical(reference.Plan(scene), optimized.Plan(scene), sceneIndex, frame);
                    scene.Player.Position.X += 2;
                }
            }
        }

        private static void UnknownAttachedGrappleReturnsControlWithoutInputs()
        {
            var scene = CombatScenario(4);
            scene.Mobility.Grappling = true;
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 0 });
            var plan = planner.Plan(scene);
            True(plan.RequestControlReturn);
            Equal("unsupported-active-grapple", plan.StrategyId);
            False(plan.Jump || plan.Hook || plan.Drop || plan.ToggleMount ||
                plan.Dash || plan.Fire || plan.FeatherFallUp);
            Equal(0, plan.Horizontal);
            Equal(0, plan.GravityControl);

            plan = planner.PlanSurvival(scene);
            True(plan.RequestControlReturn);
            False(plan.Jump || plan.Hook || plan.Drop || plan.ToggleMount ||
                plan.Dash || plan.Fire || plan.FeatherFallUp);
        }

        private static void InactiveUnknownGrappleNeverEmitsHookInput()
        {
            var scene = CombatScenario(4);
            scene.Mobility.HasGrapple = true;
            scene.Mobility.GrappleRangePixels = 9999f;
            scene.Mobility.FlightResourceFraction = 0f;
            scene.Arena.GrappleAnchors.Add(new Vec2(1680f, 680f));
            var planner = new CombatPlanner(new PlannerSettings
                { EmergencyRiskThreshold = 0f });
            for (var frame = 0; frame < 64; frame++)
            {
                var plan = planner.Plan(scene);
                False(plan.Hook);
                False(plan.RequestControlReturn);
            }
        }

        private static void UnknownOptionalProfilesLeaveControlPlanUnchanged()
        {
            var aggregateOnly = OptionalMotionScene(false);
            aggregateOnly.Mobility.CanDash = true;
            aggregateOnly.Mobility.DashReady = true;
            aggregateOnly.Mobility.CanFlipGravity = true;
            aggregateOnly.Mobility.HasUsableMount = true;
            aggregateOnly.Mobility.MountCanFly = true;
            aggregateOnly.Mobility.MountRunSpeed = 99f;
            // Exact native states/probes deliberately remain unknown.
            var absent = OptionalMotionScene(false);
            var settingsA = new PlannerSettings { PatternSafeRiskThreshold = -1f };
            var settingsB = new PlannerSettings { PatternSafeRiskThreshold = -1f };
            var expected = new CombatPlanner(settingsA).Plan(aggregateOnly);
            var actual = new CombatPlanner(settingsB).Plan(absent);
            AssertPlansIdentical(expected, actual, 1458, 0);
            Equal(expected.JumpAction, actual.JumpAction);
            Equal(expected.WeaponIssue, actual.WeaponIssue);
            Equal(expected.HookWorld.X, actual.HookWorld.X);
            Equal(expected.HookWorld.Y, actual.HookWorld.Y);
        }

        private static void EffectiveMotionThresholdDoesNotUseInventoryCapabilityUnions()
        {
            var requirements = new BossRequirements
            {
                MinimumHorizontalClearance = 800f,
                MinimumVerticalClearance = 200f,
                MinimumEffectiveHorizontalSpeed = 6f,
                MinimumWeaponDps = 1f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.OnFoot,
                    Dash = BossDashBaseline.None
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.None
                }
            };
            string reason;

            var baseline = CombatScenario(4);
            baseline.Player.MaxRunSpeed = 6f;
            baseline.Mobility = new MobilitySnapshot();
            True(requirements.IsMet(baseline, out reason), reason);

            // Better equipment in inventory is optional state. It must neither
            // replace nor invalidate the single on-foot lower-bound route.
            var dormantHighGear = CombatScenario(4);
            dormantHighGear.Player.MaxRunSpeed = 6f;
            dormantHighGear.Mobility = new MobilitySnapshot
            {
                HasUsableMount = true,
                MountCanFly = true,
                MountRunSpeed = 99f,
                HasGrapple = true,
                CanDash = true,
                DashReady = true,
                CanFlipGravity = true
            };
            True(requirements.IsMet(dormantHighGear, out reason), reason);

            // No aggregate capability can disguise an actually sub-baseline
            // player movement profile.
            dormantHighGear.Player.MaxRunSpeed = 5.99f;
            False(requirements.IsMet(dormantHighGear, out reason));

            dormantHighGear.Player.MaxRunSpeed = 6f;
            dormantHighGear.Mobility.MountActive = true;
            False(requirements.IsMet(dormantHighGear, out reason));
            dormantHighGear.Mobility.MountActive = false;
            dormantHighGear.Mobility.Grappling = true;
            False(requirements.IsMet(dormantHighGear, out reason));

            var flight = new BossRequirements
            {
                MinimumHorizontalClearance = 800f,
                MinimumVerticalClearance = 200f,
                MinimumEffectiveHorizontalSpeed = 6f,
                MinimumWeaponDps = 1f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                    Dash = BossDashBaseline.None
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.ControlledAirRoute,
                    Burst = BossBurstMobilityThreshold.None
                }
            };
            var noPlayerFlight = CombatScenario(4);
            noPlayerFlight.Player.MaxRunSpeed = 6f;
            noPlayerFlight.Mobility = new MobilitySnapshot
            {
                HasUsableMount = true,
                MountCanFly = true,
                MountRunSpeed = 99f,
                HasGrapple = true,
                CanDash = true,
                CanFlipGravity = true
            };
            False(flight.IsMet(noPlayerFlight, out reason));
            noPlayerFlight.Mobility.HasFiniteFlightResource = true;
            noPlayerFlight.Mobility.FlightResourceFraction = .5f;
            // Aggregate values alone remain insufficient.
            False(flight.IsMet(noPlayerFlight, out reason));
            ConfigureReviewedFlight(noPlayerFlight, .5f);
            True(flight.IsMet(noPlayerFlight, out reason), reason);

            // Exact dry mount motion is still not a Boss production route. A
            // catalog/model identity cannot pass admission until one strategy
            // owns its collision, threat and return closure end-to-end.
            var broom = new BossRequirements
            {
                MinimumHorizontalClearance = 800f,
                MinimumVerticalClearance = 200f,
                MinimumEffectiveHorizontalSpeed = 7f,
                MinimumWeaponDps = 1f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.ActiveWitchBroom,
                    Dash = BossDashBaseline.None
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.ControlledAirRoute,
                    Burst = BossBurstMobilityThreshold.None
                }
            };
            var exactBroom = CombatScenario(4);
            exactBroom.Mobility = new MobilitySnapshot
            {
                MountActive = true,
                MountCanFly = true,
                MountRunSpeed = WitchBroomMotion.RunSpeed,
                WitchBroomMotion = new WitchBroomMotionSnapshot
                {
                    Known = true,
                    MountActive = true,
                    MountType = WitchBroomMotion.WitchBroomMountType,
                    FrameState = 2,
                    PositionX = 1500f,
                    PositionY = 800f,
                    VelocityX = 0f,
                    VelocityY = WitchBroomMotion.NeutralTarget,
                    Gravity = .4f,
                    NormalGravity = true,
                    Dry = true,
                    PortalPhysicsDisabled = true
                }
            };
            // A mount is measured from its own indivisible controller route;
            // malformed or weak on-foot parameters must not be preconditions.
            exactBroom.Player.MaxRunSpeed = float.NaN;
            var broomImplementation = new BossMobilityBaseline
            {
                Locomotion = BossLocomotionBaseline.ActiveWitchBroom,
                Dash = BossDashBaseline.None
            };
            BossMobilityCapabilityEnvelope broomCapability;
            True(BossMobilityCapabilityEvaluator.TryMeasure(exactBroom,
                in broomImplementation, true, out broomCapability, out reason),
                reason);
            False(broomCapability.ProductionClosureCertified);
            False(broom.IsMet(exactBroom, out reason));
            True(reason.IndexOf("生产控制闭环", StringComparison.Ordinal) >= 0,
                reason);
            var wrongBroom = exactBroom.Mobility.WitchBroomMotion;
            wrongBroom.MountType = 22;
            exactBroom.Mobility.WitchBroomMotion = wrongBroom;
            False(broom.IsMet(exactBroom, out reason));

            var dash = new BossRequirements
            {
                MinimumHorizontalClearance = 800f,
                MinimumVerticalClearance = 200f,
                MinimumEffectiveHorizontalSpeed = 6f,
                MinimumWeaponDps = 1f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.OnFoot,
                    Dash = BossDashBaseline.ShieldOfCthulhu
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn
                }
            };
            var aggregateDash = CombatScenario(4);
            aggregateDash.Player.MaxRunSpeed = 6f;
            aggregateDash.Mobility = new MobilitySnapshot
                { CanDash = true, DashReady = true, DashType = 2 };
            False(dash.IsMet(aggregateDash, out reason));
            AddExactDash(aggregateDash);
            True(dash.IsMet(aggregateDash, out reason), reason);

            var fishron = CombatScenario(370);
            ConfigureReviewedFlight(fishron, 1f);
            var planner = new CombatPlanner(new PlannerSettings());
            True(planner.RequirementsMetForExpected(fishron,
                "truffle-worm-fishing", 370, out reason), reason);
            fishron.Difficulty.Expert = true;
            fishron.Mobility.EyeShieldDash = default(EyeShieldDashState);
            fishron.Mobility.CanDash = true;
            fishron.Mobility.DashReady = true;
            False(planner.RequirementsMetForExpected(fishron,
                "truffle-worm-fishing", 370, out reason));
            AddExactDash(fishron);
            True(planner.RequirementsMetForExpected(fishron,
                "truffle-worm-fishing", 370, out reason), reason);
        }

        private static void DefaultMobilityContractsFailClosed()
        {
            string reason;
            var scene = CombatScenario(4);
            False(new BossRequirements().IsMet(scene, out reason));
            True(!string.IsNullOrEmpty(reason));

            var idOnly = new BossRequirements
            {
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.None
                },
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile { Id = "id-only" }
                }
            };
            BossMobilityRouteProfile selected;
            False(idOnly.TrySelectReadyMobilityRoute(scene, out selected,
                out reason));
            True(reason.IndexOf("未知", StringComparison.Ordinal) >= 0,
                reason);

            var missingThreshold = new BossRequirements
            {
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile
                    {
                        Id = "missing-threshold",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.None
                        }
                    }
                }
            };
            False(missingThreshold.TrySelectReadyMobilityRoute(scene,
                out selected, out reason));
            True(reason.IndexOf("未知", StringComparison.Ordinal) >= 0,
                reason);
        }

        private static void MeasuredCapabilityRejectsNonFiniteValues()
        {
            var threshold = new BossMobilityCapabilityThreshold
            {
                Vertical = BossVerticalMobilityThreshold.GroundRoute,
                Burst = BossBurstMobilityThreshold.None
            };
            var capability = new BossMobilityCapabilityEnvelope
            {
                Known = true,
                ProductionClosureCertified = true,
                Locomotion = BossLocomotionBaseline.OnFoot,
                Dash = BossDashBaseline.None,
                Vertical = BossVerticalMobilityThreshold.GroundRoute,
                Burst = BossBurstMobilityThreshold.None,
                HorizontalTopSpeed = 6f,
                HorizontalAcceleration = .08f,
                HorizontalBraking = .2f
            };
            string reason;
            capability.HorizontalTopSpeed = float.NaN;
            False(BossMobilityCapabilityEvaluator.Meets(in capability,
                in threshold, 5f, out reason));
            capability.HorizontalTopSpeed = 6f;
            capability.HorizontalAcceleration = float.PositiveInfinity;
            False(BossMobilityCapabilityEvaluator.Meets(in capability,
                in threshold, 5f, out reason));
            capability.HorizontalAcceleration = .08f;
            capability.HorizontalBraking = float.NegativeInfinity;
            False(BossMobilityCapabilityEvaluator.Meets(in capability,
                in threshold, 5f, out reason));
            capability.HorizontalBraking = .2f;
            capability.ControlledAscentSpeed = float.NaN;
            False(BossMobilityCapabilityEvaluator.Meets(in capability,
                in threshold, 5f, out reason));
            capability.ControlledAscentSpeed = 0f;
            capability.BurstStartSpeed = float.NaN;
            False(BossMobilityCapabilityEvaluator.Meets(in capability,
                in threshold, 5f, out reason));
        }

        private static void ModeledSlowNormalizesOnlyExactVanillaImpairment()
        {
            True(BossMobilityCapabilityEvaluator.IsModeledSlowEffectPresent(
                true, true, false));
            True(BossMobilityCapabilityEvaluator.IsModeledSlowEffectPresent(
                true, false, true));
            False(BossMobilityCapabilityEvaluator.IsModeledSlowEffectPresent(
                true, false, false));
            False(BossMobilityCapabilityEvaluator.IsModeledSlowEffectPresent(
                false, false, true));

            var threshold = new BossMobilityCapabilityThreshold
            {
                Vertical = BossVerticalMobilityThreshold.GroundRoute,
                Burst = BossBurstMobilityThreshold.None,
                MinimumHorizontalAcceleration = .08f
            };
            var slowed = new BossMobilityCapabilityEnvelope
            {
                Known = true,
                ProductionClosureCertified = true,
                Locomotion = BossLocomotionBaseline.OnFoot,
                Dash = BossDashBaseline.None,
                Vertical = BossVerticalMobilityThreshold.GroundRoute,
                Burst = BossBurstMobilityThreshold.None,
                HorizontalTopSpeed = 3f,
                HorizontalAcceleration = .04f,
                HorizontalBraking = .2f
            };
            var player = new PlayerSnapshot
            {
                SlowDebuffKnown = true,
                SlowDebuffActive = true,
                MoveSpeedDebuffFactorKnown = true,
                MoveSpeedDebuffFactor = .5f
            };
            string reason;
            False(BossMobilityCapabilityEvaluator.Meets(in slowed,
                in threshold, 6f, out reason));
            True(BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                in slowed, player, in threshold, 6f, out reason), reason);

            player.SlowDebuffActive = false;
            False(BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                in slowed, player, in threshold, 6f, out reason));
            player.SlowDebuffActive = true;
            player.MoveSpeedDebuffFactor = 1f / 3f;
            False(BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                in slowed, player, in threshold, 6f, out reason));
            player.MoveSpeedDebuffFactor = .5f;
            slowed.HorizontalTopSpeed = 1.5f;
            False(BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                in slowed, player, in threshold, 6f, out reason));

            slowed.HorizontalTopSpeed = 3f;
            slowed.Dash = BossDashBaseline.ShieldOfCthulhu;
            False(BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                in slowed, player, in threshold, 6f, out reason));
        }

        private static void ExplicitReviewedRoutesChooseOneWholeProfile()
        {
            var requirements = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.None
                },
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile
                    {
                        Id = "shield-runway",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.ShieldOfCthulhu
                        }
                    },
                    new BossMobilityRouteProfile
                    {
                        Id = "finite-wing-loop",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                            Dash = BossDashBaseline.None
                        }
                    }
                }
            };
            BossMobilityRouteProfile selected;
            string reason;

            var shield = CombatScenario(4);
            shield.Player.MaxRunSpeed = 6f;
            shield.Mobility = new MobilitySnapshot();
            AddExactDash(shield);
            True(requirements.TrySelectReadyMobilityRoute(shield, out selected,
                out reason), reason);
            Equal("shield-runway", selected.Id);
            Equal(BossDashBaseline.ShieldOfCthulhu,
                selected.Baseline.Dash);

            var wings = CombatScenario(4);
            wings.Player.MaxRunSpeed = 6f;
            wings.Mobility = new MobilitySnapshot
            {
                HasFiniteFlightResource = true,
                FlightResourceFraction = .5f,
                // Better but dormant gear must not alter the selected route.
                HasUsableMount = true,
                SelectedMountIdentityKnown = true,
                SelectedMountItemType = 4444,
                SelectedMountType = WitchBroomMotion.WitchBroomMountType
            };
            ConfigureReviewedFlight(wings, .5f);
            True(requirements.TrySelectReadyMobilityRoute(wings, out selected,
                out reason), reason);
            Equal("finite-wing-loop", selected.Id);

            // Aggregate dash flags cannot contribute the dash half of the
            // first route, while absent flight cannot contribute the locomotion
            // half of the second route. No cross-profile union is admitted.
            var aggregatePieces = CombatScenario(4);
            aggregatePieces.Player.MaxRunSpeed = 6f;
            aggregatePieces.Mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true,
                DashType = 2
            };
            False(requirements.TrySelectReadyMobilityRoute(aggregatePieces,
                out selected, out reason));

            // Once selected, the exact shield route remains identifiable during
            // its native cooldown; that does not mean another dash is ready.
            var dashState = shield.Mobility.EyeShieldDash;
            dashState.DashDelay = 30;
            dashState.EocDash = 15;
            dashState.ReleaseDash = false;
            shield.Mobility.EyeShieldDash = dashState;
            False(requirements.TrySelectReadyMobilityRoute(shield, out selected,
                out reason));
            True(requirements.TrySelectLiveMobilityRoute(shield, out selected,
                out reason), reason);
            Equal("shield-runway", selected.Id);
            False(EyeShieldDashMotion.IsReady(
                in shield.Mobility.EyeShieldDash));

            // Route IDs are stable identities, so malformed sets fail closed
            // instead of silently selecting an ambiguous alternative.
            requirements.ClassicMobilityRoutes[1].Id = "shield-runway";
            False(requirements.TrySelectReadyMobilityRoute(wings, out selected,
                out reason));
            True(reason.IndexOf("重复", StringComparison.Ordinal) >= 0,
                reason);
        }

        private static void CapabilityThresholdSelectsAnyCompleteCertifiedRoute()
        {
            var air = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.ControlledAirRoute,
                    Burst = BossBurstMobilityThreshold.None
                },
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile
                    {
                        Id = "effective-on-foot",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.None
                        }
                    },
                    new BossMobilityRouteProfile
                    {
                        Id = "certified-finite-air",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                            Dash = BossDashBaseline.None
                        }
                    }
                }
            };
            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 20f;
            ConfigureReviewedFlight(scene, .75f);
            BossMobilityRouteProfile selected;
            string reason;
            True(air.TrySelectReadyMobilityRoute(scene, out selected,
                out reason), reason);
            // Raw run speed is above the numeric minimum, but it cannot supply
            // the independently required controlled-air dimension. The second
            // complete controller route is selected without prescribing gear.
            Equal("certified-finite-air", selected.Id);

            var burst = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn
                },
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile
                    {
                        Id = "ordinary-run",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.None
                        }
                    },
                    new BossMobilityRouteProfile
                    {
                        Id = "certified-shield-burst",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.ShieldOfCthulhu
                        }
                    }
                }
            };
            scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            AddExactDash(scene);
            True(burst.TrySelectReadyMobilityRoute(scene, out selected,
                out reason), reason);
            Equal("certified-shield-burst", selected.Id);

            burst.ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
            {
                Vertical = (BossVerticalMobilityThreshold)99
            };
            False(burst.TrySelectReadyMobilityRoute(scene, out selected,
                out reason));
            True(reason.IndexOf("未知", StringComparison.Ordinal) >= 0, reason);
        }

        private static void CapabilityThresholdUsesMeasuredDynamicsNotEquipmentNames()
        {
            var requirements = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                    Dash = BossDashBaseline.None
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.ControlledAirRoute,
                    Burst = BossBurstMobilityThreshold.None,
                    MinimumHorizontalAcceleration = .016f,
                    MinimumHorizontalBraking = .2f,
                    MinimumControlledAscentSpeed = 7.5f,
                    MinimumControlledAirTicks = 100
                }
            };
            var scene = CombatScenario(4);
            scene.Player.BaseRunSpeed = 3f;
            scene.Player.MaxRunSpeed = 6f;
            scene.Player.RunAcceleration = .08f;
            scene.Player.SprintAcceleration = .016f;
            scene.Player.RunSlowdown = .2f;
            ConfigureReviewedFlight(scene, .75f);

            string reason;
            True(requirements.IsMet(scene, out reason), reason);

            var threshold = requirements.ClassicMobilityThreshold;
            threshold.MinimumHorizontalAcceleration = .0161f;
            requirements.ClassicMobilityThreshold = threshold;
            False(requirements.IsMet(scene, out reason));
            True(reason.IndexOf("加速度", StringComparison.Ordinal) >= 0,
                reason);

            threshold.MinimumHorizontalAcceleration = .016f;
            threshold.MinimumHorizontalBraking = .201f;
            requirements.ClassicMobilityThreshold = threshold;
            False(requirements.IsMet(scene, out reason));
            True(reason.IndexOf("制动", StringComparison.Ordinal) >= 0,
                reason);

            threshold.MinimumHorizontalBraking = .2f;
            threshold.MinimumControlledAscentSpeed = 7.516f;
            requirements.ClassicMobilityThreshold = threshold;
            False(requirements.IsMet(scene, out reason));
            True(reason.IndexOf("上升", StringComparison.Ordinal) >= 0,
                reason);

            threshold.MinimumControlledAscentSpeed = 7.5f;
            threshold.MinimumControlledAirTicks = 101;
            requirements.ClassicMobilityThreshold = threshold;
            False(requirements.IsMet(scene, out reason));
            True(reason.IndexOf("空中控制时间", StringComparison.Ordinal) >= 0,
                reason);

            // Item labels and dormant alternatives are irrelevant once the
            // same live implementation has yielded the measured envelope.
            threshold.MinimumControlledAirTicks = 100;
            requirements.ClassicMobilityThreshold = threshold;
            scene.Player.WingAccessoryItemType = 999999;
            scene.Mobility.HasUsableMount = true;
            scene.Mobility.SelectedMountIdentityKnown = true;
            scene.Mobility.SelectedMountItemType = 4444;
            scene.Mobility.SelectedMountType =
                WitchBroomMotion.WitchBroomMountType;
            True(requirements.IsMet(scene, out reason), reason);

            var burst = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobility = new BossMobilityBaseline
                {
                    Locomotion = BossLocomotionBaseline.OnFoot,
                    Dash = BossDashBaseline.ShieldOfCthulhu
                },
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn,
                    MinimumBurstStartSpeed =
                        EyeShieldDashMotion.CertifiedStartSpeed
                }
            };
            scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            AddExactDash(scene);
            True(burst.IsMet(scene, out reason), reason);
            threshold = burst.ClassicMobilityThreshold;
            threshold.MinimumBurstStartSpeed =
                EyeShieldDashMotion.CertifiedStartSpeed + .001f;
            burst.ClassicMobilityThreshold = threshold;
            False(burst.IsMet(scene, out reason));
            True(reason.IndexOf("爆发速度", StringComparison.Ordinal) >= 0,
                reason);
        }

        private static void PlayerOwnedRoutePrecedesOptionalMountAndBurst()
        {
            var requirements = new BossRequirements
            {
                MinimumEffectiveHorizontalSpeed = 6f,
                ClassicMobilityThreshold = new BossMobilityCapabilityThreshold
                {
                    Vertical = BossVerticalMobilityThreshold.GroundRoute,
                    Burst = BossBurstMobilityThreshold.None
                },
                // Deliberately put more intrusive implementations first. The
                // selector must use measured-route preference, not array order.
                ClassicMobilityRoutes = new[]
                {
                    new BossMobilityRouteProfile
                    {
                        Id = "player-flight",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion =
                                BossLocomotionBaseline.FinitePlayerFlight,
                            Dash = BossDashBaseline.None
                        }
                    },
                    new BossMobilityRouteProfile
                    {
                        Id = "player-with-optional-burst",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.ShieldOfCthulhu
                        }
                    },
                    new BossMobilityRouteProfile
                    {
                        Id = "player-ground",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.OnFoot,
                            Dash = BossDashBaseline.None
                        }
                    }
                }
            };
            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            ConfigureReviewedFlight(scene, .75f);
            AddExactDash(scene);
            BossMobilityRouteProfile selected;
            string reason;
            True(requirements.TrySelectReadyMobilityRoute(scene, out selected,
                out reason), reason);
            Equal("player-ground", selected.Id);

            var air = new BossMobilityCapabilityThreshold
            {
                Vertical = BossVerticalMobilityThreshold.ControlledAirRoute,
                Burst = BossBurstMobilityThreshold.None
            };
            var playerAir = new BossMobilityBaseline
            {
                Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                Dash = BossDashBaseline.None
            };
            var mountAir = new BossMobilityBaseline
            {
                Locomotion = BossLocomotionBaseline.ActiveWitchBroom,
                Dash = BossDashBaseline.None
            };
            True(BossMobilityCapabilityEvaluator.Preference(in playerAir,
                in air) < BossMobilityCapabilityEvaluator.Preference(
                    in mountAir, in air));
        }

        private static void PlannerLatchesExplicitRouteUntilReset()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var requirements = PlannerRequirements(planner, "eye-of-cthulhu");
            requirements.ClassicMobilityRoutes = new[]
            {
                new BossMobilityRouteProfile
                {
                    Id = "shield-first",
                    Baseline = new BossMobilityBaseline
                    {
                        Locomotion = BossLocomotionBaseline.OnFoot,
                        Dash = BossDashBaseline.ShieldOfCthulhu
                    }
                },
                new BossMobilityRouteProfile
                {
                    Id = "flight-second",
                    Baseline = new BossMobilityBaseline
                    {
                        Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                        Dash = BossDashBaseline.None
                    }
                }
            };

            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            scene.Mobility = new MobilitySnapshot();
            AddExactDash(scene);

            // Both complete routes match. Reviewed order selects and latches
            // the exact shield route on the first live Boss frame.
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn);
            Equal("shield-first", PlannerMobilityRouteId(planner));

            // Removing the exact shield source leaves the second route wholly
            // valid, but a live session may not silently switch strategies.
            scene.Mobility.EyeShieldDash = default(EyeShieldDashState);
            ConfigureReviewedFlight(scene, .75f);
            plan = planner.Plan(scene);
            True(plan.RequestControlReturn);
            Equal("unsupported-mobility-route", plan.StrategyId);
            Equal(0, plan.Horizontal);
            False(plan.Jump || plan.Drop || plan.Fire || plan.Dash || plan.Hook ||
                plan.ToggleMount || plan.FeatherFallUp);
            Equal("shield-first", PlannerMobilityRouteId(planner));

            // Reset begins a new takeover session. Only then may the now-valid
            // second complete profile be selected.
            planner.Reset();
            Equal(null, PlannerMobilityRouteId(planner));
            plan = planner.Plan(scene);
            False(plan.RequestControlReturn);
            Equal("flight-second", PlannerMobilityRouteId(planner));
        }

        private static void PlannerLatchesAndRevalidatesSingleRoute()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn);
            Equal("eye-of-cthulhu-classic-primary",
                PlannerMobilityRouteId(planner));

            // A single-route strategy is still a concrete implementation. It
            // must be checked on every frame and may not silently continue once
            // its measured ability falls below the encounter threshold.
            scene.Player.MaxRunSpeed = 5.49f;
            plan = planner.Plan(scene);
            True(plan.RequestControlReturn);
            Equal("unsupported-mobility-route", plan.StrategyId);
            Equal("eye-of-cthulhu-classic-primary",
                PlannerMobilityRouteId(planner));

            planner.Reset();
            Equal(null, PlannerMobilityRouteId(planner));
        }

        private static void PlannerRejectsDifficultyDriftWithoutRouteSwap()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var requirements = PlannerRequirements(planner, "eye-of-cthulhu");
            requirements.ClassicMobilityRoutes = new[]
            {
                new BossMobilityRouteProfile
                {
                    Id = "same-id",
                    Baseline = new BossMobilityBaseline
                    {
                        Locomotion = BossLocomotionBaseline.OnFoot,
                        Dash = BossDashBaseline.None
                    }
                }
            };
            requirements.ExpertMobilityRoutes = new[]
            {
                new BossMobilityRouteProfile
                {
                    Id = "same-id",
                    Baseline = new BossMobilityBaseline
                    {
                        Locomotion =
                            BossLocomotionBaseline.FinitePlayerFlight,
                        Dash = BossDashBaseline.None
                    }
                }
            };

            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn);
            Equal("same-id", PlannerMobilityRouteId(planner));

            // A bad adapter or changed world cannot use the same string ID to
            // replace the admitted controller with another difficulty's route.
            scene.Difficulty.Expert = true;
            ConfigureReviewedFlight(scene, .75f);
            plan = planner.Plan(scene);
            True(plan.RequestControlReturn);
            Equal("unsupported-mobility-route", plan.StrategyId);
            Equal("same-id", PlannerMobilityRouteId(planner));
        }

        private static void OnFootRouteIgnoresDormantFiniteFlight()
        {
            var ordinary = CombatScenario(4);
            ordinary.Player.MaxRunSpeed = 6f;
            var dormantFlight = CombatScenario(4);
            dormantFlight.Player.MaxRunSpeed = 6f;
            ConfigureReviewedFlight(dormantFlight, .75f);
            ordinary.Player.Jump = dormantFlight.Player.Jump;
            ordinary.Player.Flight = default(FlightSnapshot);
            ordinary.Player.WingTime = ordinary.Player.RocketTime = 0f;
            ordinary.Mobility.HasFiniteFlightResource = false;
            ordinary.Mobility.FlightResourceFraction = 0f;
            ordinary.Player.OnGround = dormantFlight.Player.OnGround = false;
            ordinary.Player.Position = dormantFlight.Player.Position =
                new Vec2(1500f, 800f);
            for (var i = 0; i < 24; i++)
            {
                var threat = new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Position = ordinary.Player.Position + new Vec2(
                        i * 21f - 220f, (i % 5) * 34f - 70f),
                    Velocity = new Vec2(5f - i % 9, i % 3 - 1f),
                    Width = 14,
                    Height = 14,
                    Damage = 45,
                    TimeLeft = 240
                };
                ordinary.Threats.Add(threat);
                dormantFlight.Threats.Add(threat);
            }

            var settingsA = new PlannerSettings
                { PatternSafeRiskThreshold = -1f };
            var settingsB = new PlannerSettings
                { PatternSafeRiskThreshold = -1f };
            var ordinaryPlanner = new CombatPlanner(settingsA);
            var flightPlanner = new CombatPlanner(settingsB);
            for (var frame = 0; frame < 64; frame++)
            {
                var expected = ordinaryPlanner.Plan(ordinary);
                var actual = flightPlanner.Plan(dormantFlight);
                AssertPlansIdentical(expected, actual, 14004, frame);
                Equal(expected.JumpAction, actual.JumpAction);
                Equal(expected.WeaponIssue, actual.WeaponIssue);
                Equal(ordinaryPlanner.LastCandidateCount,
                    flightPlanner.LastCandidateCount);
                True(ordinaryPlanner.LastCandidateCount > 1,
                    "dormant-flight isolation must exercise expanded candidates");
                Equal("eye-of-cthulhu-classic-primary",
                    PlannerMobilityRouteId(ordinaryPlanner));
                Equal(PlannerMobilityRouteId(ordinaryPlanner),
                    PlannerMobilityRouteId(flightPlanner));
                ordinary.Player.Position.X += expected.Horizontal * 2f;
                dormantFlight.Player.Position.X += actual.Horizontal * 2f;
            }
        }

        private static void OnFootRouteRejectsUnmodelledFlightEquipment()
        {
            var scene = OptionalMotionScene(false);
            var route = new BossMobilityBaseline
            {
                Locomotion = BossLocomotionBaseline.OnFoot,
                Dash = BossDashBaseline.None
            };
            BossMobilityCapabilityEnvelope measured;
            string reason;

            scene.Player.Flight = new FlightSnapshot
            {
                Known = false,
                WingsLogic = 44,
                RocketBoots = 0
            };
            False(BossMobilityCapabilityEvaluator.TryMeasure(scene, in route,
                true, out measured, out reason));
            True(reason != null && reason.Contains("unmodelled equipped wings"),
                reason);

            scene.Player.Flight = new FlightSnapshot
            {
                Known = false,
                WingsLogic = 0,
                RocketBoots = 3
            };
            False(BossMobilityCapabilityEvaluator.TryMeasure(scene, in route,
                true, out measured, out reason));

            // The reviewed Demon-Wings snapshot remains eligible to stay
            // dormant on an on-foot route; only unknown flight semantics are
            // barred from silently reinterpreting Jump.
            ConfigureReviewedFlight(scene, .75f);
            True(BossMobilityCapabilityEvaluator.TryMeasure(scene, in route,
                true, out measured, out reason), reason);
        }

        private static void ProductionBossesDeclareCapabilityThresholdsSeparately()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var fishron = PlannerRequirements(planner, "duke-fishron");
            var classic = new DifficultySnapshot();
            var expert = new DifficultySnapshot { Expert = true };
            var master = new DifficultySnapshot { Expert = true, Master = true };
            Equal(BossVerticalMobilityThreshold.ControlledAirRoute,
                fishron.MobilityThresholdFor(classic).Vertical);
            Equal(BossBurstMobilityThreshold.None,
                fishron.MobilityThresholdFor(classic).Burst);
            Equal(BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn,
                fishron.MobilityThresholdFor(expert).Burst);
            Equal(BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn,
                fishron.MobilityThresholdFor(master).Burst);

            var king = PlannerRequirements(planner, "king-slime");
            Equal(BossVerticalMobilityThreshold.GroundRoute,
                king.MobilityThresholdFor(classic).Vertical);
            Equal(BossBurstMobilityThreshold.None,
                king.MobilityThresholdFor(classic).Burst);

            var prime = PlannerRequirements(planner, "skeletron-prime");
            Equal(BossVerticalMobilityThreshold.ControlledAirRoute,
                prime.MobilityThresholdFor(classic).Vertical);
            // Controller identity is a separate implementation choice, not the
            // definition of the Boss's required movement capability.
            Equal(BossLocomotionBaseline.FinitePlayerFlight,
                prime.MobilityFor(classic).Locomotion);

            var engineField = typeof(CombatPlanner).GetField("_strategies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var engine = engineField.GetValue(planner);
            var listField = typeof(BossStrategyEngine).GetField("_strategies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var strategies = (System.Collections.IEnumerable)listField.GetValue(
                engine);
            foreach (var entry in strategies)
            {
                var strategy = (IBossStrategy)entry;
                var classicRoutes = strategy.Requirements.MobilityRoutesFor(
                    classic);
                var expertRoutes = strategy.Requirements.MobilityRoutesFor(
                    expert);
                var masterRoutes = strategy.Requirements.MobilityRoutesFor(
                    master);
                True(classicRoutes != null && classicRoutes.Length > 0,
                    strategy.Id + " missing classic mobility route");
                True(expertRoutes != null && expertRoutes.Length > 0,
                    strategy.Id + " missing expert mobility route");
                True(masterRoutes != null && masterRoutes.Length > 0,
                    strategy.Id + " missing master mobility route");
                True(!classicRoutes[0].Id.StartsWith("legacy-",
                    StringComparison.Ordinal));
                True(!expertRoutes[0].Id.StartsWith("legacy-",
                    StringComparison.Ordinal));
                True(!masterRoutes[0].Id.StartsWith("legacy-",
                    StringComparison.Ordinal));
                True(!string.Equals(classicRoutes[0].Id,
                    expertRoutes[0].Id, StringComparison.Ordinal));
                True(!string.Equals(expertRoutes[0].Id,
                    masterRoutes[0].Id, StringComparison.Ordinal));
            }
        }

        private static void PriorityFlightSpeedThresholdMatchesNativeLightningDemon()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var cases = new[]
            {
                new { Strategy = "duke-fishron", BossType = 370 }
            };
            for (var i = 0; i < cases.Length; i++)
            {
                var requirements = PlannerRequirements(planner,
                    cases[i].Strategy);
                Equal(6.75f, requirements.RequiredHorizontalSpeedFor(
                    new DifficultySnapshot()));

                var scene = CombatScenario(cases[i].BossType);
                var flight = DemonFlight();
                scene.Player.Flight = flight;
                scene.Player.Jump = OrdinaryJump();
                scene.Player.WingTime = flight.WingTime;
                scene.Player.RocketTime = flight.RocketTime;
                scene.Mobility.HasFiniteFlightResource = true;
                scene.Mobility.FlightResourceFraction =
                    FlightMotion.ResourceFraction(in flight);
                scene.Player.MaxRunSpeed = 6.75f;

                string reason;
                True(requirements.IsMet(scene, out reason),
                    cases[i].Strategy + " rejected native 6.75 speed: " +
                    reason);
                scene.Player.MaxRunSpeed = 6.74f;
                False(requirements.IsMet(scene, out reason),
                    cases[i].Strategy + " accepted speed below 6.75");
            }
        }

        private static void PreflightRouteReservationSurvivesBossArrivalReset()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            var requirements = PlannerRequirements(planner,
                "eye-of-cthulhu");
            requirements.ClassicMobilityRoutes = new[]
            {
                new BossMobilityRouteProfile
                {
                    Id = "preflight-shield",
                    Baseline = new BossMobilityBaseline
                    {
                        Locomotion = BossLocomotionBaseline.OnFoot,
                        Dash = BossDashBaseline.ShieldOfCthulhu
                    }
                },
                new BossMobilityRouteProfile
                {
                    Id = "later-flight",
                        Baseline = new BossMobilityBaseline
                        {
                            Locomotion = BossLocomotionBaseline.FinitePlayerFlight,
                            Dash = BossDashBaseline.None
                        }
                }
            };
            var scene = CombatScenario(4);
            scene.Player.MaxRunSpeed = 6f;
            AddExactDash(scene);
            string reason;
            True(planner.PrepareForExpectedEncounter(scene, "direct-item",
                4, out reason), reason);
            Equal("preflight-shield", PlannerMobilityRouteId(planner));

            // The waiting interval may change observable inventory/equipment.
            // Boss arrival resets motion memory, but must not replace the route
            // which authorized consumption of the summon item.
            scene.Mobility.EyeShieldDash = default(EyeShieldDashState);
            ConfigureReviewedFlight(scene, .75f);
            planner.ResetForBossArrival();
            Equal("preflight-shield", PlannerMobilityRouteId(planner));
            var plan = planner.Plan(scene);
            True(plan.RequestControlReturn);
            Equal("unsupported-mobility-route", plan.StrategyId);
            Equal("preflight-shield", PlannerMobilityRouteId(planner));

            planner.Reset();
            Equal(null, PlannerMobilityRouteId(planner));
        }

        private static void ConfigureReviewedFlight(CombatSnapshot scene,
            float fraction)
        {
            var flight = DemonFlight(false);
            flight.WingTime = (int)(flight.WingTimeMax * fraction);
            flight.RocketTime = 0;
            scene.Player.Flight = flight;
            scene.Player.Jump = OrdinaryJump();
            scene.Player.WingTime = flight.WingTime;
            scene.Player.RocketTime = flight.RocketTime;
            scene.Mobility.HasFiniteFlightResource = true;
            scene.Mobility.FlightResourceFraction =
                FlightMotion.ResourceFraction(in flight);
        }

        private static void DepleteReviewedFlight(CombatSnapshot scene)
        {
            var flight = scene.Player.Flight;
            flight.WingTime = 0f;
            flight.RocketTime = 0;
            scene.Player.Flight = flight;
            scene.Player.WingTime = 0f;
            scene.Player.RocketTime = 0f;
            scene.Mobility.HasFiniteFlightResource = true;
            scene.Mobility.FlightResourceFraction =
                FlightMotion.ResourceFraction(in flight);
        }

        private static BossRequirements PlannerRequirements(CombatPlanner planner,
            string strategyId)
        {
            var engineField = typeof(CombatPlanner).GetField("_strategies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(engineField != null, "missing planner strategy engine");
            var engine = engineField.GetValue(planner);
            var listField = typeof(BossStrategyEngine).GetField("_strategies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(listField != null, "missing strategy catalog");
            var strategies = (System.Collections.IEnumerable)listField.GetValue(
                engine);
            foreach (var entry in strategies)
            {
                var strategy = (IBossStrategy)entry;
                if (strategy.Id == strategyId) return strategy.Requirements;
            }
            throw new InvalidOperationException("missing strategy " + strategyId);
        }

        private static string PlannerMobilityRouteId(CombatPlanner planner)
        {
            var field = typeof(CombatPlanner).GetField("_latchedMobilityRouteId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(field != null, "missing mobility route latch");
            return (string)field.GetValue(planner);
        }

        private static void VanillaMountCatalogContainsEveryIdentity()
        {
            Equal(66, VanillaMountCatalog.Count);
            Equal(WitchBroomMotion.VerifiedTerrariaSha256,
                VanillaMountCatalog.VerifiedTerrariaSha256);
            var seenItems = new bool[7000];
            var carts = 0;
            var exactMotion = 0;
            for (var mountType = 0; mountType < VanillaMountCatalog.Count;
                mountType++)
            {
                VanillaMountDescriptor entry;
                True(VanillaMountCatalog.TryGetByMountType(mountType,
                    out entry));
                Equal(mountType, entry.MountType);
                True(!string.IsNullOrWhiteSpace(entry.Key));
                if (entry.Minecart) carts++;
                if (entry.Evidence == VanillaMountModelEvidence.ExactDryMotion)
                {
                    exactMotion++;
                    Equal(WitchBroomMotion.WitchBroomMountType,
                        entry.MountType);
                }
                if (!entry.HasSummonItem) continue;
                True(entry.SummonItemType < seenItems.Length);
                False(seenItems[entry.SummonItemType],
                    "duplicate mount summon item identity");
                seenItems[entry.SummonItemType] = true;
                VanillaMountDescriptor byItem;
                True(VanillaMountCatalog.TryGetBySummonItem(
                    entry.SummonItemType, out byItem));
                Equal(entry.MountType, byItem.MountType);
            }
            Equal(27, carts);
            Equal(1, exactMotion);

            AssertMountIdentity(7, 2769, 141);   // UFO
            AssertMountIdentity(23, 4444, 230); // Witch's Broom
            AssertMountIdentity(56, 5597, 377); // Bat
            AssertMountIdentity(61, 5662, 384); // Pixie
            AssertMountIdentity(62, 5665, 387); // Chillet
            AssertMountIdentity(65, 6151, 392); // Trusty Chillet Ignis
            VanillaMountDescriptor missing;
            False(VanillaMountCatalog.TryGetByMountType(-1, out missing));
            False(VanillaMountCatalog.TryGetByMountType(66, out missing));
            False(VanillaMountCatalog.TryGetBySummonItem(0, out missing));
        }

        private static void AssertMountIdentity(int mountType, int itemType,
            int buffType)
        {
            VanillaMountDescriptor entry;
            True(VanillaMountCatalog.TryGetByMountType(mountType, out entry));
            Equal(itemType, entry.SummonItemType);
            Equal(buffType, entry.BuffType);
        }

        private static void FacadePublishesKnownActiveMountIdentity()
        {
            var mobility = PublishMountIdentities(true,
                WitchBroomMotion.WitchBroomMountType, false, 0, -1);
            True(mobility.MountActive);
            True(mobility.ActiveMountIdentityKnown);
            Equal(WitchBroomMotion.WitchBroomMountType,
                mobility.ActiveMountType);
            False(mobility.SelectedMountIdentityKnown);

            mobility = PublishMountIdentities(false,
                WitchBroomMotion.WitchBroomMountType, false, 0, -1);
            False(mobility.ActiveMountIdentityKnown);
            Equal(-1, mobility.ActiveMountType);

            mobility = PublishMountIdentities(true,
                VanillaMountCatalog.Count, false, 0, -1);
            False(mobility.ActiveMountIdentityKnown);
            Equal(VanillaMountCatalog.Count, mobility.ActiveMountType);
        }

        private static void FacadePublishesQuickMountSelectionIdentity()
        {
            var fixture = new MountFacadeFixture(2769, 7);
            float speed;
            bool canFly;
            int itemType;
            int mountType;
            True(fixture.Read(out speed, out canFly, out itemType,
                out mountType));
            Equal(1, fixture.QuickMountReads);
            Equal(2769, itemType);
            Equal(7, mountType);
            Equal(9.5f, speed);
            True(canFly);

            var mobility = PublishMountIdentities(false, -1, true,
                itemType, mountType);
            True(mobility.SelectedMountIdentityKnown);
            Equal(2769, mobility.SelectedMountItemType);
            Equal(7, mobility.SelectedMountType);
        }

        private static void FacadeRejectsMismatchedQuickMountPair()
        {
            // The native selector may only provide an item object. A corrupt,
            // custom, or future-version item/mount pair must not be upgraded
            // into a known vanilla identity from either number in isolation.
            var fixture = new MountFacadeFixture(
                WitchBroomMotion.WitchBroomItemType, 7);
            float speed;
            bool canFly;
            int itemType;
            int mountType;
            True(fixture.Read(out speed, out canFly, out itemType,
                out mountType));
            var mobility = PublishMountIdentities(false, -1, true,
                itemType, mountType);
            False(mobility.SelectedMountIdentityKnown);
            Equal(WitchBroomMotion.WitchBroomItemType,
                mobility.SelectedMountItemType);
            Equal(7, mobility.SelectedMountType);
        }

        private static void UnwiredMountsReturnControlWithoutInputs()
        {
            var identityOnly = OptionalMotionScene(false);
            identityOnly.Mobility.MountActive = true;
            identityOnly.Mobility.ActiveMountIdentityKnown = true;
            identityOnly.Mobility.ActiveMountType = 7;
            identityOnly.Mobility.MountCanFly = true;
            identityOnly.Mobility.MountRunSpeed = 99f;
            AssertUnsupportedMountPlan(new CombatPlanner(
                new PlannerSettings()).Plan(identityOnly),
                TacticalMode.EmergencyEvade);
            AssertUnsupportedMountPlan(new CombatPlanner(
                new PlannerSettings()).PlanSurvival(identityOnly),
                TacticalMode.AwaitingBoss);

            // ExactDryMotion is deliberately narrower than a production Boss
            // controller: the broom still lacks live arena/threat integration.
            var broom = OptionalMotionScene(false);
            broom.Mobility.MountActive = true;
            broom.Mobility.ActiveMountIdentityKnown = true;
            broom.Mobility.ActiveMountType =
                WitchBroomMotion.WitchBroomMountType;
            broom.Mobility.MountCanFly = true;
            broom.Mobility.MountRunSpeed = WitchBroomMotion.RunSpeed;
            broom.Mobility.WitchBroomMotion = new WitchBroomMotionSnapshot
            {
                Known = true,
                MountActive = true,
                MountType = WitchBroomMotion.WitchBroomMountType,
                FrameState = 2,
                PositionX = broom.Player.Position.X,
                PositionY = broom.Player.Position.Y,
                VelocityY = WitchBroomMotion.NeutralTarget,
                Gravity = broom.Player.Gravity,
                NormalGravity = true,
                Dry = true,
                PortalPhysicsDisabled = true
            };
            AssertUnsupportedMountPlan(new CombatPlanner(
                new PlannerSettings()).Plan(broom),
                TacticalMode.EmergencyEvade);

            // Merely selecting an inactive IdentityOnly mount is inventory
            // knowledge, not permission to toggle it or alter the plan.
            var selected = OptionalMotionScene(false);
            selected.Mobility.HasUsableMount = true;
            selected.Mobility.SelectedMountIdentityKnown = true;
            selected.Mobility.SelectedMountItemType = 2769;
            selected.Mobility.SelectedMountType = 7;
            selected.Mobility.MountCanFly = true;
            selected.Mobility.MountRunSpeed = 99f;
            var absent = OptionalMotionScene(false);
            var selectedPlan = new CombatPlanner(new PlannerSettings()).Plan(
                selected);
            var absentPlan = new CombatPlanner(new PlannerSettings()).Plan(
                absent);
            AssertPlansIdentical(absentPlan, selectedPlan, 2769, 7);
            False(selectedPlan.ToggleMount);
        }

        private static MobilitySnapshot PublishMountIdentities(bool active,
            int activeType, bool selectedAvailable, int selectedItemType,
            int selectedType)
        {
            var facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var publish = facadeType.GetMethod("PublishMountIdentities",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(publish != null, "missing facade mount identity publisher");
            var mobility = new MobilitySnapshot { MountActive = active };
            publish.Invoke(null, new object[] { mobility, active, activeType,
                selectedAvailable, selectedItemType, selectedType });
            return mobility;
        }

        private static void AssertUnsupportedMountPlan(ControlPlan plan,
            TacticalMode mode)
        {
            True(plan.RequestControlReturn);
            Equal(mode, plan.TacticalMode);
            Equal("unsupported-active-mount", plan.StrategyId);
            Equal(0, plan.Horizontal);
            Equal(0, plan.GravityControl);
            False(plan.Jump || plan.Drop || plan.Fire || plan.Dash ||
                plan.Hook || plan.ToggleMount || plan.FeatherFallUp ||
                plan.QuickHeal || plan.QuickMana);
            Equal(JumpAction.Release, plan.JumpAction);
        }

        private sealed class MountFacadeFixture
        {
            private readonly object _facade;
            private readonly Type _facadeType;
            private readonly MountFacadePlayer _player;
            public int QuickMountReads;

            public MountFacadeFixture(int itemType, int mountType)
            {
                _facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                    "Chaite.Plugin.TerrariaFacade", true);
                _facade = System.Runtime.Serialization.FormatterServices.
                    GetUninitializedObject(_facadeType);
                _player = new MountFacadePlayer
                {
                    QuickMount = new MountFacadeItem
                    {
                        Type = itemType,
                        Stack = 1,
                        MountType = mountType
                    }
                };
                var mounts = new object[VanillaMountCatalog.Count];
                mounts[mountType] = new MountFacadeData
                {
                    RunSpeed = 9.5f,
                    FlightTime = 120,
                    UsesHover = false
                };
                Set("_quickMountItem", new Func<object, object>(player =>
                {
                    QuickMountReads++;
                    return ((MountFacadePlayer)player).QuickMount;
                }));
                Set("_itemTypeId", new Func<object, int>(item =>
                    ((MountFacadeItem)item).Type));
                Set("_itemStack", new Func<object, int>(item =>
                    ((MountFacadeItem)item).Stack));
                Set("_itemMountType", new Func<object, int>(item =>
                    ((MountFacadeItem)item).MountType));
                Set("_mounts", new Func<object[]>(() => mounts));
                Set("_mountDataRunSpeed", new Func<object, float>(mount =>
                    ((MountFacadeData)mount).RunSpeed));
                Set("_mountDataFlightTime", new Func<object, int>(mount =>
                    ((MountFacadeData)mount).FlightTime));
                Set("_mountDataUsesHover", new Func<object, bool>(mount =>
                    ((MountFacadeData)mount).UsesHover));
            }

            private void Set(string name, object value)
            {
                var field = _facadeType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing mount fixture dependency: " +
                    name);
                field.SetValue(_facade, value);
            }

            public bool Read(out float speed, out bool canFly,
                out int itemType, out int mountType)
            {
                var read = _facadeType.GetMethod("ReadAvailableMount",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(read != null, "missing facade QuickMount reader");
                var arguments = new object[] { _player, 0f, false, 0, -1 };
                var available = (bool)read.Invoke(_facade, arguments);
                speed = (float)arguments[1];
                canFly = (bool)arguments[2];
                itemType = (int)arguments[3];
                mountType = (int)arguments[4];
                return available;
            }
        }

        private sealed class MountFacadePlayer
        {
            public MountFacadeItem QuickMount;
        }

        private sealed class MountFacadeItem
        {
            public int Type;
            public int Stack;
            public int MountType;
        }

        private sealed class MountFacadeData
        {
            public float RunSpeed;
            public int FlightTime;
            public bool UsesHover;
        }

        private static void DashCandidateRequiresBrakingRoomAndSafeReturn()
        {
            var directive = OptionalDirective();
            var wide = OptionalMotionScene(false);
            AddExactDash(wide);
            var wideScore = InvokeMobilityCandidateScore(new CombatPlanner(
                new PlannerSettings { HorizonTicks = 6, SimulationStepTicks = 1 }),
                wide, directive, 1);
            True(wideScore < float.MaxValue,
                "a wide observed runway should admit the proved dash round trip");

            var narrow = OptionalMotionScene(false);
            AddExactDash(narrow);
            narrow.Arena.LocalOpenBounds = new RectF(460f, 0f, 190f, 1200f);
            narrow.Arena.FloorSupport = new SupportSpan { Valid = true,
                Left = 460f, Right = 650f, SurfaceY = 842f };
            var narrowScore = InvokeMobilityCandidateScore(new CombatPlanner(
                new PlannerSettings { HorizonTicks = 6, SimulationStepTicks = 1 }),
                narrow, directive, 1);
            Equal(float.MaxValue, narrowScore);

            var occupiedReturn = OptionalMotionScene(false);
            AddExactDash(occupiedReturn);
            occupiedReturn.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Position = new Vec2(0f, 0f),
                Velocity = new Vec2(0f, 0f),
                Width = 2000,
                Height = 1200,
                Damage = 1,
                TimeLeft = 300
            });
            var occupiedScore = InvokeMobilityCandidateScore(new CombatPlanner(
                new PlannerSettings { HorizonTicks = 6, SimulationStepTicks = 1 }),
                occupiedReturn, directive, 1);
            Equal(float.MaxValue, occupiedScore);
        }

        private static void RequiredDashBaselineDrivesReviewedBossPhase()
        {
            var scene = OptionalMotionScene(false);
            var fishron = scene.Targets[0];
            fishron.Type = 370;
            fishron.Ai0 = 1f;
            fishron.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = fishron;
            RefreshPriorityNativeContext(scene);
            scene.Difficulty.Expert = true;
            scene.Player.MaxRunSpeed = 7f;
            ConfigureReviewedFlight(scene, .75f);
            AddExactDash(scene);

            // The ordinary candidate is already below the normal hazard gate.
            // Expert Fishron nevertheless names the exact shield dash as part
            // of its lower-bound charge response, so the planner must evaluate
            // and take its completely proved dash/brake/return trajectory.
            var plan = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                PatternSafeRiskThreshold = float.MaxValue
            }).Plan(scene);
            True(plan.Dash,
                "the reviewed expert Fishron charge must consume its declared shield-dash baseline");
            True(plan.LateMobilityFallback.Known,
                "a late-revalidated dash must carry the same-pass safe ordinary candidate");
            False(plan.LateMobilityFallback.Jump &&
                plan.LateMobilityFallback.Drop);
            True(plan.LateMobilityFallback.Horizontal >= -1 &&
                plan.LateMobilityFallback.Horizontal <= 1);

            // Aggregate flags and the right numeric dash type are not an
            // identity. Without the exact item-3097 native state, the same
            // phase remains on ordinary controls and never invents an edge.
            scene.Mobility.EyeShieldDash = default(EyeShieldDashState);
            plan = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                PatternSafeRiskThreshold = float.MaxValue
            }).Plan(scene);
            False(plan.Dash);

            // The same shield is optional in the reviewed classic profile. A
            // clean charge frame must therefore keep the ordinary baseline
            // instead of treating every possessed dash as mandatory.
            scene = OptionalMotionScene(false);
            fishron = scene.Targets[0];
            fishron.Type = 370;
            fishron.Ai0 = 1f;
            fishron.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = fishron;
            RefreshPriorityNativeContext(scene);
            scene.Player.MaxRunSpeed = 7f;
            ConfigureReviewedFlight(scene, .75f);
            AddExactDash(scene);
            plan = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                PatternSafeRiskThreshold = float.MaxValue
            }).Plan(scene);
            False(plan.Dash);

            // Even a required exact dash remains forbidden when its full
            // braking/return closure does not fit the observed runway.
            scene.Difficulty.Expert = true;
            scene.Arena.LocalOpenBounds = new RectF(460f, 0f, 190f, 1200f);
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true,
                Left = 460f,
                Right = 650f,
                SurfaceY = 842f
            };
            plan = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                PatternSafeRiskThreshold = float.MaxValue
            }).Plan(scene);
            False(plan.Dash);
        }

        private static void WitchBroomCapabilityRemainsNotProductionCertified()
        {
            var scene = OptionalMotionScene(false);
            scene.Mobility.MountActive = true;
            scene.Mobility.WitchBroomMotion = new WitchBroomMotionSnapshot
            {
                Known = true,
                MountActive = true,
                MountType = WitchBroomMotion.WitchBroomMountType,
                FrameState = 0,
                PositionX = 100f,
                PositionY = 200f,
                VelocityX = 0f,
                VelocityY = 0f,
                Gravity = .3f,
                ReleaseUp = true,
                SlowFall = false,
                NormalGravity = true,
                Dry = true,
                OpenDryPath = true,
                PortalPhysicsDisabled = true,
                Grappling = false,
                HookInFlight = false,
                DashInProgress = false,
                CrowdControlled = false,
                Tongued = false,
                Dead = false,
                Pulley = false,
                Sliding = false,
                WindPushed = false,
                ForcedMotion = false
            };
            BossMobilityCapabilityEnvelope capability;
            string reason;
            True(BossMobilityCapabilityEvaluator.TryMeasure(scene,
                    new BossMobilityBaseline
                    {
                        Locomotion = BossLocomotionBaseline.ActiveWitchBroom,
                        Dash = BossDashBaseline.None
                    }, true, out capability, out reason),
                "Witch's Broom should be measurable but not yet production-certified: " + reason);
            False(capability.ProductionClosureCertified,
                "Witch's Broom must not authorize production input before its live closure exists");
        }

        private static void LateOptionalEdgeUsesOnlyCertifiedFallback()
        {
            var player = new PendingMobilityPlayer();
            SetEveryPendingControl(player, true);
            var facade = PendingMobilityFacade();
            SetFacadeField(facade, "_pendingFallbackKnown", true);
            SetFacadeField(facade, "_pendingFallbackLeft", true);
            SetFacadeField(facade, "_pendingFallbackRight", false);
            SetFacadeField(facade, "_pendingFallbackJump", true);
            SetFacadeField(facade, "_pendingFallbackDrop", false);
            var resolve = facade.GetType().GetMethod(
                "ResolveRejectedPendingMobility",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(resolve != null);

            True((bool)resolve.Invoke(facade,
                new object[] { player, true, false }));
            True(player.controlLeft && !player.controlRight &&
                player.controlJump && !player.controlDown);
            False(player.controlUp || player.controlDash ||
                player.controlHook || player.controlMount);
            False(player.controlUseItem || player.controlUseTile ||
                player.controlThrow || player.controlQuickHeal ||
                player.controlQuickMana,
                "uncertified item and consumable inputs must not survive a mobility fallback");

            // No certificate means no subset of the rejected plan survives.
            SetEveryPendingControl(player, true);
            SetFacadeField(facade, "_pendingFallbackKnown", false);
            False((bool)resolve.Invoke(facade,
                new object[] { player, true, false }));
            AssertEveryPendingControl(player, false);

            // A fallback scored with the old passive gravity cannot be reused
            // after feather-fall physics itself changes.
            SetEveryPendingControl(player, true);
            SetFacadeField(facade, "_pendingFallbackKnown", true);
            False((bool)resolve.Invoke(facade,
                new object[] { player, true, true }));
            AssertEveryPendingControl(player, false);
        }

        private static void TrustyChilletDashIsRevalidatedAfterNativeInputCopy()
        {
            var player = new PendingMobilityPlayer
            {
                mount = new PendingMobilityMount { Active = true, Type = 64 },
                releaseDash = true,
                dashType = 6,
                dashDelay = 0,
                controlDash = true
            };
            var facade = PendingMobilityFacade();
            SetFacadeField(facade, "_validatePendingFormulaMountDash", true);
            SetFacadeField(facade, "_pendingFormulaMountType", 64);
            var validate = facade.GetType().GetMethod(
                "ValidatePendingMobility", BindingFlags.Instance |
                BindingFlags.Public);
            True(validate != null);
            Equal(null, validate.Invoke(facade, new object[] { player }));
            True(player.controlDash);

            SetEveryPendingControl(player, true);
            player.mount.Type = 65;
            SetFacadeField(facade, "_validatePendingFormulaMountDash", true);
            SetFacadeField(facade, "_pendingFormulaMountType", 64);
            var rejection = validate.Invoke(facade,
                new object[] { player }) as string;
            True(rejection != null && rejection.Contains(
                "trusty-chillet-native-dash-state-changed"));
            AssertEveryPendingControl(player, false);
        }

        private static void LateFeatherFallExpiryNeutralizesEveryInput()
        {
            // buffTime 0 can leave the prior frame's aggregate slowFall=true at
            // the early hook. ResetEffects removes it before movement; the late
            // gate must reject even though no Up edge was requested.
            var expired = new PendingMobilityPlayer { slowFall = false };
            expired.buffType[0] = 8;
            expired.buffTime[0] = 0;
            SetEveryPendingControl(expired, true);
            var facade = PendingMobilityFacade();
            SetFacadeField(facade, "_validatePendingFeatherFall", true);
            SetFacadeField(facade,
                "_pendingFeatherFallRequiresPotionUp", false);
            var validate = facade.GetType().GetMethod("ValidatePendingMobility");
            var rejection = (string)validate.Invoke(facade,
                new object[] { expired });
            True(rejection != null && rejection.Contains(
                "slow-fall-effect-expired-before-movement"), rejection);
            True(rejection.Contains("resolution=all-controls-neutral"),
                rejection);
            AssertEveryPendingControl(expired, false);

            // Native UpdateBuffs decrements a final time=1 slot to zero and
            // still applies slowFall in that update. The exact type-8 lineage
            // plus the live aggregate therefore remains valid for this frame.
            var finalTick = new PendingMobilityPlayer { slowFall = true };
            finalTick.buffType[0] = 8;
            finalTick.buffTime[0] = 0;
            SetEveryPendingControl(finalTick, true);
            facade = PendingMobilityFacade();
            SetFacadeField(facade, "_validatePendingFeatherFall", true);
            SetFacadeField(facade,
                "_pendingFeatherFallRequiresPotionUp", true);
            Equal(null, (string)validate.Invoke(facade,
                new object[] { finalTick }));
            AssertEveryPendingControl(finalTick, true);
        }

        private static void GravityReturnUsesUpForBothDirections()
        {
            foreach (var rememberedInverted in new[] { false, true })
            {
                var scene = OptionalMotionScene(!rememberedInverted);
                AddExactGravity(scene, !rememberedInverted, true);
                var planner = new CombatPlanner(new PlannerSettings
                {
                    HorizonTicks = 6,
                    SimulationStepTicks = 1
                });
                ArmGravityReturn(planner, scene, rememberedInverted, false);
                var plan = planner.Plan(scene);
                True(plan.GravityControl == 1,
                    "gravity return must use Up; rememberedInverted=" +
                    rememberedInverted);
                False(plan.FeatherFallUp);
            }
        }

        private static void GravityReturnWaitsForNativeReleaseFrame()
        {
            var scene = OptionalMotionScene(true);
            AddExactGravity(scene, true, false);
            var planner = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1
            });
            ArmGravityReturn(planner, scene, false, true);
            Equal(0, planner.Plan(scene).GravityControl);

            var gravity = scene.Mobility.GravityFlip;
            gravity.ReleaseUp = true;
            scene.Mobility.GravityFlip = gravity;
            var returned = planner.Plan(scene);
            Equal(1, returned.GravityControl);
        }

        private static CombatSnapshot OptionalMotionScene(bool inverted)
        {
            // Optional motion tests exercise the generic candidate layer. Use
            // a ground-route Boss so an unrelated finite-flight requirement
            // cannot hide the behavior under test behind route admission.
            var scene = CombatScenario(4);
            scene.Player.Position = inverted ? new Vec2(500f, 500f) :
                new Vec2(500f, 800f);
            scene.Player.Velocity = new Vec2(0f, 0f);
            scene.Player.OnGround = true;
            scene.Player.OnOneWaySupport = false;
            scene.Player.WorldLeft = 0f;
            scene.Player.WorldRight = 2000f;
            scene.Player.WorldTop = 0f;
            scene.Player.WorldBottom = 1200f;
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10f;
            scene.Player.BaseRunSpeed = 3f;
            scene.Player.MaxRunSpeed = 6f;
            scene.Player.RunAcceleration = .08f;
            scene.Player.SprintAcceleration = .016f;
            scene.Player.RunSlowdown = .2f;
            scene.Player.Jump = default(JumpSnapshot);
            scene.Player.Flight = default(FlightSnapshot);
            scene.Player.WingTime = 0f;
            scene.Player.RocketTime = 0f;
            scene.Mobility = new MobilitySnapshot
            {
                GravityInverted = inverted,
                HasFiniteFlightResource = false
            };
            scene.Arena = new ArenaSnapshot
            {
                LocalOpenBounds = new RectF(0f, 0f, 2000f, 1200f),
                SafeCenter = new Vec2(1000f, 700f),
                ClearanceLeft = 500f,
                ClearanceRight = 1480f,
                ClearanceUp = inverted ? 0f : 500f,
                ClearanceDown = inverted ? 658f : 358f,
                HasFloor = true,
                HasCeiling = true,
                FloorSupport = new SupportSpan { Valid = true, Left = 0f,
                    Right = 2000f, SurfaceY = 842f },
                CeilingSupport = new SupportSpan { Valid = true, Inverted = true,
                    Left = 0f, Right = 2000f, SurfaceY = 500f }
            };
            var target = scene.Targets[0];
            target.Position = new Vec2(1100f, 650f);
            target.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = target;
            return scene;
        }

        private static BossDirective OptionalDirective()
        {
            return new BossDirective
            {
                Pattern = BossPattern.HorizontalKite,
                StrategyId = "optional-motion-test",
                PhaseId = "recovery",
                IdealDistance = 500f,
                AllowGravityFlip = true,
                ForceContinuousMovement = false
            };
        }

        private static void AddExactDash(CombatSnapshot scene)
        {
            scene.Mobility.CanDash = true;
            scene.Mobility.DashReady = true;
            scene.Mobility.DashRightProbeKnown = true;
            scene.Mobility.DashLeftProbeKnown = true;
            scene.Mobility.EyeShieldDash = new EyeShieldDashState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                EquipmentIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097,
                DashType = 2,
                Dash = 2,
                DashDelay = 0,
                TimeSinceLastDashStarted = 300,
                EocHit = -1,
                ReleaseDash = true,
                FacingDirection = 1,
                VelocityX = scene.Player.Velocity.X,
                VelocityY = scene.Player.Velocity.Y,
                AccRunSpeed = 6f,
                MaxRunSpeed = 6f,
                HostileContactKnown = true
            };
        }

        private static void AddExactGravity(CombatSnapshot scene, bool inverted,
            bool releaseUp)
        {
            scene.Mobility.CanFlipGravity = true;
            scene.Mobility.GravityInverted = inverted;
            scene.Mobility.GravityFlip = new GravityFlipState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                Identity = GravityControlIdentity.GravitationBuff18,
                GravControl = true,
                ReleaseUp = releaseUp,
                GravityDirection = inverted ? -1f : 1f,
                PositionY = scene.Player.Position.Y,
                VelocityY = scene.Player.Velocity.Y
            };
        }

        private static void ArmGravityReturn(CombatPlanner planner,
            CombatSnapshot scene, bool rememberedInverted, bool releasePending)
        {
            SetPlannerField(planner, "_gravityReturnPending", true);
            SetPlannerField(planner, "_gravityReturnInverted", rememberedInverted);
            SetPlannerField(planner, "_gravityReturnWasGrounded", true);
            SetPlannerField(planner, "_gravityReturnAnchor", rememberedInverted
                ? new Vec2(500f, 500f) : new Vec2(500f, 800f));
            SetPlannerField(planner, "_gravityReturnSupport", rememberedInverted
                ? scene.Arena.CeilingSupport : scene.Arena.FloorSupport);
            SetPlannerField(planner, "_gravityReleasePending", releasePending);
        }

        private static void SetPlannerField(CombatPlanner planner, string name,
            object value)
        {
            var field = typeof(CombatPlanner).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(field != null, "missing planner field " + name);
            field.SetValue(planner, value);
        }

        private static float InvokeMobilityCandidateScore(CombatPlanner planner,
            CombatSnapshot scene, BossDirective directive, int actionValue)
        {
            return InvokeMobilityCandidateField(planner, scene, directive,
                actionValue, "Score");
        }

        private static float InvokeMobilityCandidateField(CombatPlanner planner,
            CombatSnapshot scene, BossDirective directive, int actionValue,
            string fieldName)
        {
            var method = typeof(CombatPlanner).GetMethod("ScoreCandidateWithMobility",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null, "missing mobility candidate scorer");
            var parameters = method.GetParameters();
            var action = Enum.ToObject(parameters[8].ParameterType, actionValue);
            var candidate = method.Invoke(planner, new object[]
            {
                scene, scene.Targets[0], directive, 1, 0, 1, 0,
                float.MaxValue, action
            });
            var score = candidate.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            True(score != null, "missing candidate field " + fieldName);
            return (float)score.GetValue(candidate);
        }

        private static void CalibratedMotionWorkloadBenchmark()
        {
            var scene = RichScenario(4);
            scene.Player.BaseRunSpeed = 3f;
            scene.Player.MaxRunSpeed = 6f;
            scene.Player.RunAcceleration = .08f;
            scene.Player.SprintAcceleration = .016f;
            scene.Player.RunSlowdown = .2f;
            var allocation = CreateAllocationCounter(out var scope);
            MeasureRichScenario("native boot acceleration + 200 projectiles", scene, allocation, scope);
        }

        private sealed class PendingMobilityPlayer
        {
            public bool controlLeft, controlRight, controlJump, controlDown;
            public bool controlUp, controlDash, controlHook, controlMount;
            public bool controlUseItem, controlUseTile, controlThrow;
            public bool controlQuickHeal, controlQuickMana;
            public bool slowFall;
            public bool releaseDash;
            public bool cced, pulley, tongued;
            public int dashType, dashDelay, grapCount;
            public float gravDir = 1f;
            public PendingMobilityMount mount;
            public int[] buffType = new int[22];
            public int[] buffTime = new int[22];
        }

        private sealed class PendingMobilityMount
        {
            public bool Active;
            public int Type;
        }

        private static object PendingMobilityFacade()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var facade = System.Runtime.Serialization.FormatterServices.
                GetUninitializedObject(type);
            var names = new[]
            {
                "controlLeft", "controlRight", "controlJump", "controlDown",
                "controlUp", "controlDash", "controlHook", "controlMount",
                "controlUseItem", "controlUseTile", "controlThrow",
                "controlQuickHeal", "controlQuickMana"
            };
            var controls = new Dictionary<string, Action<object, bool>>();
            var readers = new Dictionary<string, Func<object, bool>>();
            var captured = new Dictionary<string, bool>();
            foreach (var name in names)
            {
                var field = typeof(PendingMobilityPlayer).GetField(name,
                    BindingFlags.Instance | BindingFlags.Public);
                True(field != null, "missing pending-player field " + name);
                controls[name] = (player, value) => field.SetValue(player,
                    value);
                readers[name] = player => (bool)field.GetValue(player);
                captured[name] = false;
            }
            SetFacadeField(facade, "_controls", controls);
            SetFacadeField(facade, "_controlReaders", readers);
            SetFacadeField(facade, "_capturedControls", captured);
            SetFacadeField(facade, "_playerMount",
                new Func<object, object>(player =>
                    ((PendingMobilityPlayer)player).mount));
            SetFacadeField(facade, "_mountActive",
                new Func<object, bool>(mount =>
                    mount != null && ((PendingMobilityMount)mount).Active));
            SetFacadeField(facade, "_mountTypeId",
                new Func<object, int>(mount =>
                    ((PendingMobilityMount)mount).Type));
            SetFacadeField(facade, "_playerBuffType",
                new Func<object, int[]>(player =>
                    ((PendingMobilityPlayer)player).buffType));
            SetFacadeField(facade, "_playerBuffTime",
                new Func<object, int[]>(player =>
                    ((PendingMobilityPlayer)player).buffTime));
            SetFacadeField(facade, "_playerSlowFall",
                new Func<object, bool>(player =>
                    ((PendingMobilityPlayer)player).slowFall));
            SetFacadeField(facade, "_playerGrapCount",
                new Func<object, int>(player =>
                    ((PendingMobilityPlayer)player).grapCount));
            SetFacadeField(facade, "_playerGravDir",
                new Func<object, float>(player =>
                    ((PendingMobilityPlayer)player).gravDir));
            SetFacadeField(facade, "_playerGravControl",
                new Func<object, bool>(player => false));
            SetFacadeField(facade, "_playerGravControl2",
                new Func<object, bool>(player => false));
            SetFacadeField(facade, "_playerForcedGravity",
                new Func<object, int>(player => 0));
            SetFacadeField(facade, "_playerDashType",
                new Func<object, int>(player =>
                    ((PendingMobilityPlayer)player).dashType));
            SetFacadeField(facade, "_playerDashDelay",
                new Func<object, int>(player =>
                    ((PendingMobilityPlayer)player).dashDelay));
            SetFacadeField(facade, "_releaseDash",
                new Func<object, bool>(player =>
                    ((PendingMobilityPlayer)player).releaseDash));
            SetFacadeField(facade, "_playerCCed",
                new Func<object, bool>(player =>
                    ((PendingMobilityPlayer)player).cced));
            SetFacadeField(facade, "_playerPulley",
                new Func<object, bool>(player =>
                    ((PendingMobilityPlayer)player).pulley));
            SetFacadeField(facade, "_playerTongued",
                new Func<object, bool>(player =>
                    ((PendingMobilityPlayer)player).tongued));
            return facade;
        }

        private static void SetFacadeField(object facade, string name,
            object value)
        {
            var field = facade.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(field != null, "missing pending-facade field " + name);
            field.SetValue(facade, value);
        }

        private static void SetEveryPendingControl(PendingMobilityPlayer player,
            bool value)
        {
            player.controlLeft = player.controlRight = player.controlJump =
                player.controlDown = player.controlUp = player.controlDash =
                player.controlHook = player.controlMount =
                player.controlUseItem = player.controlUseTile =
                player.controlThrow = player.controlQuickHeal =
                player.controlQuickMana = value;
        }

        private static void AssertEveryPendingControl(
            PendingMobilityPlayer player, bool expected)
        {
            Equal(expected, player.controlLeft);
            Equal(expected, player.controlRight);
            Equal(expected, player.controlJump);
            Equal(expected, player.controlDown);
            Equal(expected, player.controlUp);
            Equal(expected, player.controlDash);
            Equal(expected, player.controlHook);
            Equal(expected, player.controlMount);
            Equal(expected, player.controlUseItem);
            Equal(expected, player.controlUseTile);
            Equal(expected, player.controlThrow);
            Equal(expected, player.controlQuickHeal);
            Equal(expected, player.controlQuickMana);
        }

        private static void MotionNear(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("motion expected " + expected + ", actual " + actual);
        }
    }
}
