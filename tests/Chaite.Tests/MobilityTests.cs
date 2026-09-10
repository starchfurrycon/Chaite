using Chaite.Core;
using System;

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
            Run(nameof(GrappledEmergencyReleasesThenPulsesNativeJump), GrappledEmergencyReleasesThenPulsesNativeJump);
            Run(nameof(GrapplePullHasBoundedWaitAndNoThreatWaitingCanDetach), GrapplePullHasBoundedWaitAndNoThreatWaitingCanDetach);
            Run(nameof(GrappleReleaseResetsAfterDetachAndSessionReset), GrappleReleaseResetsAfterDetachAndSessionReset);
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

        private static void GrappledEmergencyReleasesThenPulsesNativeJump()
        {
            var scene = CombatScenario(4);
            scene.Mobility.Grappling = true;
            scene.Player.OnGround = true;
            scene.Player.Velocity = new Vec2(0, 0);
            scene.Mobility.FlightResourceFraction = 0;
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 0 });
            for (var frame = 0; frame < 6; frame++)
            {
                var plan = planner.Plan(scene);
                Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
                Equal(frame % 2 == 1, plan.Jump);
                False(plan.Hook);
                False(plan.Drop);
                False(plan.ToggleMount);
                Equal(0, plan.GravityControl);
            }
        }

        private static void GrapplePullHasBoundedWaitAndNoThreatWaitingCanDetach()
        {
            var scene = CombatScenario(4);
            scene.Targets.Clear();
            scene.Threats.Clear();
            scene.Mobility.Grappling = true;
            scene.Mobility.FlightResourceFraction = 0;
            scene.Player.OnGround = false;
            scene.Player.Velocity = new Vec2(10, 0);
            var planner = new CombatPlanner(new PlannerSettings());
            for (var frame = 0; frame < 8; frame++) False(planner.PlanSurvival(scene).Jump);
            True(planner.PlanSurvival(scene).Jump);
            False(planner.PlanSurvival(scene).Jump);
            True(planner.PlanSurvival(scene).Jump);
        }

        private static void GrappleReleaseResetsAfterDetachAndSessionReset()
        {
            var scene = CombatScenario(4);
            scene.Mobility.Grappling = true;
            scene.Player.OnGround = true;
            var planner = new CombatPlanner(new PlannerSettings());
            False(planner.Plan(scene).Jump);
            True(planner.Plan(scene).Jump);
            scene.Mobility.Grappling = false;
            False(planner.Plan(scene).Hook);
            scene.Mobility.Grappling = true;
            False(planner.Plan(scene).Jump);
            planner.Reset();
            False(planner.Plan(scene).Jump);
            True(planner.Plan(scene).Jump);
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

        private static void MotionNear(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("motion expected " + expected + ", actual " + actual);
        }
    }
}
