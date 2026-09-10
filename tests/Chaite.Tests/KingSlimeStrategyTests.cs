using Chaite.Core;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunKingSlimeRegressions()
        {
            Run("King resting velocity is not teleport", KingRestIsNotTeleport);
            Run("King native jump index distinguishes normal short high", KingNativeJumpCycle);
            Run("King strict life thresholds accelerate countdown", KingLifeCountdown);
            Run("King teleport reads observed destination only", KingTeleportDestination);
            Run("King explicit coast never requests generic jump", KingExplicitCoast);
            Run("King planner preserves explicit coast despite Boss height", KingPlannerExplicitCoast);
            Run("King verified high jump commits and clears opposite side", KingRunUnderLoop);
            Run("King native sprint profile verifies and rejects run-under", KingNativeRunUnderModel);
            Run("King low friction does not invent stronger braking", KingLowFrictionBraking);
            Run("King stuck recovery preserves scored committed exit", KingStuckPreservesScoredExit);
            Run("King unsafe floor gravity and low hop reject run-under", KingRejectUnsafeCrossing);
            Run("King engine reset clears crossing memory", KingResetClearsMemory);
            Run("King expert spiked pressure preserves pattern target", KingSpikedPressure);
            Run("King fire respects invulnerability and line of sight", KingFireGuards);
        }

        private static CombatSnapshot KingSnapshot()
        {
            var s = CombatScenario(50);
            s.Player.Position = new Vec2(1800f, 958f);
            s.Player.Width = 20;
            s.Player.Height = 42;
            s.Player.Velocity = default(Vec2);
            s.Player.MaxRunSpeed = 6f;
            s.Player.RunAcceleration = .2f;
            s.Player.RunSlowdown = .2f;
            s.Player.OnGround = true;
            s.Mobility = new MobilitySnapshot();
            s.Arena = new ArenaSnapshot
            {
                ClearanceLeft = 700f, ClearanceRight = 1000f, ClearanceUp = 600f,
                HasFloor = true,
                FloorSupport = new SupportSpan { Valid = true, Left = 1100f, Right = 2800f, SurfaceY = 1000f }
            };
            var king = s.Targets[0];
            king.Key = 30;
            king.Position = new Vec2(1430f, 900f);
            king.Width = 120;
            king.Height = 100;
            king.Velocity = default(Vec2);
            king.Ai0 = -120f;
            king.Ai1 = 1f;
            king.Life = king.LifeMax = 2000;
            king.LineOfSightKnown = true;
            king.HasLineOfSight = true;
            s.Targets[0] = king;
            return s;
        }

        private static void KingRestIsNotTeleport()
        {
            var s = KingSnapshot();
            var engine = new BossStrategyEngine();
            BossDecision d = null;
            for (var tick = 0; tick < 100; tick++) d = engine.Evaluate(s);
            Equal("classic-ground-normal-wait", d.Directive.PhaseId);
            Equal(0, d.Directive.VerticalIntent);
        }

        private static void KingNativeJumpCycle()
        {
            var s = KingSnapshot();
            var engine = new BossStrategyEngine();
            var king = s.Targets[0];
            king.Velocity = new Vec2(4f, -8f);
            king.Ai1 = 1;
            s.Targets[0] = king;
            Equal("classic-normal-hop-retreat", engine.Evaluate(s).Directive.PhaseId);
            king.Ai1 = 2;
            s.Targets[0] = king;
            Equal("classic-normal-hop-retreat", engine.Evaluate(s).Directive.PhaseId);
            king.Ai1 = 3;
            king.Velocity = new Vec2(4.5f, -6f);
            s.Targets[0] = king;
            Equal("classic-short-hop-retreat", engine.Evaluate(s).Directive.PhaseId);
            king.Ai1 = 0;
            king.Ai0 = -200;
            king.Velocity = new Vec2(3.5f, -13f);
            s.Targets[0] = king;
            Equal("classic-high-jump-wait-for-clearance", engine.Evaluate(s).Directive.PhaseId);
            king.Ai0 = 0; // Initial falling spawn must not be a high-jump opening.
            king.Velocity = new Vec2(0f, 3f);
            s.Targets[0] = king;
            Equal("classic-normal-hop-retreat", engine.Evaluate(s).Directive.PhaseId);
        }

        private static void KingLifeCountdown()
        {
            var s = KingSnapshot();
            var engine = new BossStrategyEngine();
            var king = s.Targets[0];
            king.Ai1 = 3;
            var thresholds = new[] { .8f, .6f, .4f, .2f, .1f };
            var nominalLives = new[] { 1600, 1200, 800, 400, 200 };
            var increments = new[] { 1, 1, 2, 3, 4 };
            var comparisons = new Func<int, int, bool>[thresholds.Length];
            for (var i = 0; i < thresholds.Length; i++) comparisons[i] = KingNativeThresholdOracle(thresholds[i]);
            var previousRate = 2;
            for (var boundary = 0; boundary < thresholds.Length; boundary++)
            {
                king.Ai0 = -(Math.Max(30, previousRate * 12) + 1);
                for (var offset = -1; offset <= 1; offset++)
                {
                    king.Life = nominalLives[boundary] + offset;
                    var rate = 2;
                    for (var i = 0; i < comparisons.Length; i++)
                        if (comparisons[i](king.Life, king.LifeMax)) rate += increments[i];
                    s.Targets[0] = king;
                    var expected = -king.Ai0 <= rate * 12 ? "classic-ground-high-windup" : "classic-ground-high-wait";
                    Equal(expected, engine.Evaluate(s).Directive.PhaseId);
                    // Independently prove strict neighboring integer behavior;
                    // the exact mathematical percentage follows native IL below,
                    // not an assumed x87/SSE intermediate rounding convention.
                    if (offset < 0) Equal("classic-ground-high-windup", expected);
                    if (offset > 0) Equal("classic-ground-high-wait", expected);
                }
                previousRate += increments[boundary];
            }
        }

        private static Func<int, int, bool> KingNativeThresholdOracle(float threshold)
        {
            // AI_015_KingSlime IL_07fd..0811 (and subsequent four boundaries):
            // compare converted life against converted lifeMax * ldc.r4. Keep
            // the product on the evaluation stack as native code does. This
            // test-only oracle neither loads Terraria nor executes its methods.
            var method = new DynamicMethod("KingNativeThreshold", typeof(bool), new[] { typeof(int), typeof(int) });
            var il = method.GetILGenerator();
            var noIncrement = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Conv_R4);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Conv_R4);
            il.Emit(OpCodes.Ldc_R4, threshold);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Bge_Un_S, noIncrement);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(noIncrement);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            return (Func<int, int, bool>)method.CreateDelegate(typeof(Func<int, int, bool>));
        }

        private static void KingTeleportDestination()
        {
            var s = KingSnapshot();
            var king = s.Targets[0];
            king.Ai1 = 5;
            king.LocalAiKnown = true;
            king.LocalAi1 = 2000f;
            king.LocalAi2 = 1000f;
            s.Targets[0] = king;
            var d = new BossStrategyEngine().Evaluate(s);
            Equal("classic-teleport-leave-known-destination", d.Directive.PhaseId);
            Equal(-1, d.Directive.HorizontalIntent);
            True(d.Directive.Fire); // Shrink is not blanket invulnerability.
            king.LocalAiKnown = false;
            s.Targets[0] = king;
            d = new BossStrategyEngine().Evaluate(s);
            Equal("classic-teleport-destination-unobserved", d.Directive.PhaseId);
            Equal(1, d.Directive.HorizontalIntent); // Real current Boss remains left.
            king.Ai1 = 6;
            king.Position = new Vec2(1940f, 900f);
            s.Targets[0] = king;
            d = new BossStrategyEngine().Evaluate(s);
            Equal("classic-teleport-reform-regain-gap", d.Directive.PhaseId);
            Equal(-1, d.Directive.HorizontalIntent);
        }

        private static void KingExplicitCoast()
        {
            var s = KingSnapshot();
            var king = s.Targets[0];
            king.Position = new Vec2(1300f, 900f);
            s.Targets[0] = king;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            True(d.UseExplicitMovement);
            Equal(0, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            False(d.ForceContinuousMovement);
        }

        private static CombatSnapshot KingCrossingSnapshot()
        {
            var s = KingSnapshot();
            var king = s.Targets[0];
            king.Position = new Vec2(1640f, 650f);
            king.Velocity = new Vec2(3.5f, -8f);
            king.Ai0 = -200;
            king.Ai1 = 0;
            s.Targets[0] = king;
            return s;
        }

        private static void KingPlannerExplicitCoast()
        {
            var s = KingSnapshot();
            var king = s.Targets[0];
            king.Position = new Vec2(1300f, 650f);
            s.Targets[0] = king;
            var planner = new CombatPlanner(new PlannerSettings());
            for (var tick = 0; tick < 20; tick++)
            {
                var plan = planner.Plan(s);
                Equal(0, plan.Horizontal);
                False(plan.Jump);
                False(plan.Drop);
            }
        }

        private static void KingRunUnderLoop()
        {
            var s = KingCrossingSnapshot();
            var engine = new BossStrategyEngine();
            var d = engine.Evaluate(s).Directive;
            Equal("classic-high-jump-committed-run-under", d.PhaseId);
            Equal(-1, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            s.Player.Position = new Vec2(1670f, 958f); // Already under the body.
            s.Player.Velocity = new Vec2(-5f, 0f);
            d = engine.Evaluate(s).Directive;
            Equal("classic-high-jump-committed-run-under", d.PhaseId);
            Equal(-1, d.HorizontalIntent);
            s.Player.Position = new Vec2(1400f, 958f);
            d = engine.Evaluate(s).Directive;
            False(d.PhaseId.Contains("committed"));
            Equal(-1, d.HorizontalIntent); // No second reversal in the same jump.
        }

        private static void KingNativeRunUnderModel()
        {
            var s = KingCrossingSnapshot();
            s.Player.BaseRunSpeed = 3f;
            s.Player.RunAcceleration = .08f;
            s.Player.SprintAcceleration = .016f;
            s.Player.RunSlowdown = .2f;
            s.Player.Velocity = new Vec2(-3f, 0f);
            True(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
            // Same actual sprint fields, but a late descending body and a fast
            // away-directed player cannot clear it safely after braking.
            s.Player.Velocity = new Vec2(6f, 0f);
            var king = s.Targets[0];
            king.Position = new Vec2(1640f, 700f);
            king.Velocity = new Vec2(3.5f, -1.1f);
            s.Targets[0] = king;
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
        }

        private static void KingLowFrictionBraking()
        {
            var s = KingSnapshot();
            s.Player.Velocity = new Vec2(6f, 0f);
            s.Player.RunSlowdown = .01f;
            s.Player.BaseRunSpeed = 3f;
            s.Player.SprintAcceleration = .04f;
            // 1000px remains; true coast braking needs 1800px. The former .08
            // floor pretended 225px was sufficient and continued toward the end.
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            s.Player.RunSlowdown = .2f;
            Equal(1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void KingStuckPreservesScoredExit()
        {
            var s = KingCrossingSnapshot();
            var planner = new CombatPlanner(new PlannerSettings
            {
                PatternDeviationPenalty = 1000000f,
                VerticalPatternDeviationPenalty = 1000000f,
                EmergencyRiskThreshold = 10000000f
            });
            Equal(-1, planner.Plan(s).Horizontal);
            s.Player.Position = new Vec2(1670f, 958f);
            s.Player.Velocity = new Vec2(-5f, 0f);
            typeof(CombatPlanner).GetField("_stuckTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(planner, 1000);
            var plan = planner.Plan(s);
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            Equal(9, planner.LastCandidateCount);
            True(plan.PhaseId.Contains("committed"));
            Equal(-1, plan.Horizontal);
            False(plan.Jump);
            False(plan.Drop);
        }

        private static void KingRejectUnsafeCrossing()
        {
            var s = KingCrossingSnapshot();
            s.Arena.FloorSupport = default(SupportSpan);
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
            s = KingCrossingSnapshot();
            s.Arena.FloorSupport.Left = 1780f; // Route crosses an unobserved gap.
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
            s = KingCrossingSnapshot();
            s.Mobility.GravityInverted = true;
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
            s = KingCrossingSnapshot();
            var king = s.Targets[0];
            king.Ai1 = 3;
            king.Ai0 = -120;
            s.Targets[0] = king;
            Equal("classic-short-hop-retreat", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            s = KingCrossingSnapshot();
            king = s.Targets[0];
            king.Position = new Vec2(1640f, 840f); // Bottom only 18px above player.
            s.Targets[0] = king;
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
            s = KingCrossingSnapshot();
            s.Arena.HasCeiling = true;
            s.Arena.ClearanceUp = 100;
            False(new BossStrategyEngine().Evaluate(s).Directive.PhaseId.Contains("committed"));
        }

        private static void KingResetClearsMemory()
        {
            var s = KingCrossingSnapshot();
            var engine = new BossStrategyEngine();
            True(engine.Evaluate(s).Directive.PhaseId.Contains("committed"));
            engine.Reset();
            var king = s.Targets[0];
            king.Ai1 = 1;
            king.Ai0 = -120;
            king.Velocity = default(Vec2);
            king.Position = new Vec2(1430f, 900f);
            s.Targets[0] = king;
            Equal("classic-ground-normal-wait", engine.Evaluate(s).Directive.PhaseId);
        }

        private static void KingSpikedPressure()
        {
            var s = KingSnapshot();
            s.Targets.Add(new TargetSnapshot
            {
                Key = 91, Type = 535, Position = new Vec2(1900, 970), Width = 32, Height = 30,
                Life = 50, LifeMax = 50, Chaseable = true, LineOfSightKnown = true, HasLineOfSight = true
            });
            Equal(50, new BossStrategyEngine().Evaluate(s).Target.Type);
            s.Difficulty.Expert = true;
            var d = new BossStrategyEngine().Evaluate(s);
            Equal(535, d.Target.Type);
            Equal(50, d.PatternTarget.Type);
            Equal("expert-ground-normal-wait", d.Directive.PhaseId);
            s.Difficulty.Expert = false;
            s.Difficulty.Master = true;
            d = new BossStrategyEngine().Evaluate(s);
            Equal(535, d.Target.Type);
            Equal("master-ground-normal-wait", d.Directive.PhaseId);
            var spike = s.Targets[1];
            spike.HasLineOfSight = false;
            s.Targets[1] = spike;
            Equal(50, new BossStrategyEngine().Evaluate(s).Target.Type);
        }

        private static void KingFireGuards()
        {
            var s = KingSnapshot();
            var king = s.Targets[0];
            king.Invulnerable = true;
            s.Targets[0] = king;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
            king.Invulnerable = false;
            king.HasLineOfSight = false;
            s.Targets[0] = king;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
        }
    }
}
