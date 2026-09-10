using Chaite.Core;
using System;
using System.Reflection.Emit;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunEyeRegressions()
        {
            Run("Eye native first charge is independent of speed", EyeFirstNativeStages);
            Run("Eye expert transform uses native r4 65 percent", EyeTransformThreshold);
            Run("Eye actual spin stages outrank health heuristic", EyeTransformStages);
            Run("Eye second normal brake starts at native timer", EyeSecondBrakeTimer);
            Run("Eye expert fast timer follows r8 four percent boundary", EyeFastTimer);
            Run("Eye low health reposition is not immediate dash", EyeRepositionThreshold);
            Run("Eye committed exit does not flip when body crosses", EyeCommittedExit);
            Run("Eye recovery turns on a verified safe runway", EyeRecoveryTurn);
            Run("Eye charge edge brakes without reversing into body", EyeChargeEdge);
            Run("Eye recovery refuses an occupied return corridor", EyeUnsafeReturn);
            Run("Eye low friction preserves actual stopping distance", EyeLowFriction);
            Run("Eye observed low dash can use native held jump", EyeGroundHop);
            Run("Eye hop launch tick keeps native grounded acceleration", EyeFirstJumpTickGroundAcceleration);
            Run("Eye unknown jump or airborne cloud is not invented", EyeNoUnobservedJump);
            Run("Eye reset clears committed direction", EyeResetDirection);
            Run("Eye fire honors real visibility and invulnerability", EyeFireGuards);
        }

        private static CombatSnapshot EyeSnapshot()
        {
            var s = CombatScenario(4);
            s.Player.Position = new Vec2(1800f, 958f);
            s.Player.Velocity = new Vec2(6f, 0f);
            s.Player.Width = 20;
            s.Player.Height = 42;
            s.Player.OnGround = true;
            s.Player.BaseRunSpeed = 3f;
            s.Player.MaxRunSpeed = 6f;
            s.Player.RunAcceleration = .08f;
            s.Player.SprintAcceleration = .016f;
            s.Player.RunSlowdown = .2f;
            s.Player.Gravity = .4f;
            s.Player.MaxFallSpeed = 10f;
            s.Player.Jump = new JumpSnapshot { Known = true, Speed = 5.01f, Height = 15, ReleaseReady = true, CloudAvailable = true, CloudEnabled = true };
            s.Mobility = new MobilitySnapshot();
            s.Arena = new ArenaSnapshot
            {
                ClearanceLeft = 600f, ClearanceRight = 1800f, ClearanceUp = 600f,
                HasFloor = true,
                FloorSupport = new SupportSpan { Valid = true, Left = 1200f, Right = 3620f, SurfaceY = 1000f }
            };
            var eye = s.Targets[0];
            eye.Key = 45;
            eye.Position = new Vec2(1500f, 740f);
            eye.Width = eye.Height = 110;
            eye.Velocity = default(Vec2);
            eye.Life = eye.LifeMax = 4000;
            eye.Ai0 = eye.Ai1 = eye.Ai2 = eye.Ai3 = 0f;
            eye.LineOfSightKnown = eye.HasLineOfSight = true;
            s.Targets[0] = eye;
            return s;
        }

        private static void EyeFirstNativeStages()
        {
            var s = EyeSnapshot();
            var engine = new BossStrategyEngine();
            var eye = s.Targets[0];
            eye.Ai1 = 1;
            eye.Velocity = new Vec2(20, 0);
            s.Targets[0] = eye;
            Equal("classic-first-launch-prepare-exit", engine.Evaluate(s).Directive.PhaseId);
            eye.Ai1 = 2;
            eye.Ai2 = 39;
            eye.Velocity = new Vec2(1, 0);
            s.Targets[0] = eye;
            Equal("classic-first-charge-committed-exit", engine.Evaluate(s).Directive.PhaseId);
            eye.Ai2 = 40;
            s.Targets[0] = eye;
            Equal("classic-first-brake-recover-runway", engine.Evaluate(s).Directive.PhaseId);
        }

        private static void EyeTransformThreshold()
        {
            var s = EyeSnapshot();
            s.Difficulty.Expert = true;
            var eye = s.Targets[0];
            var native = EyeThresholdOracle(.65f, true);
            for (var life = 2599; life <= 2601; life++)
            {
                eye.Life = life;
                s.Targets[0] = eye;
                var phase = new BossStrategyEngine().Evaluate(s).Directive.PhaseId;
                Equal(native(life, 4000) ? "expert-transform-pending-recover-runway" : "expert-first-hover-bait", phase);
                if (life == 2599) True(phase.Contains("transform"));
                if (life == 2601) False(phase.Contains("transform"));
            }
            s.Difficulty.Expert = false;
            eye.Life = 2400;
            s.Targets[0] = eye;
            Equal("classic-first-hover-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void EyeTransformStages()
        {
            var s = EyeSnapshot();
            var eye = s.Targets[0];
            eye.Ai0 = 1;
            s.Targets[0] = eye;
            Equal("classic-transform-spin-up-recover", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            eye.Ai0 = 2;
            s.Targets[0] = eye;
            Equal("classic-transform-spin-down-recover", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            eye.Ai0 = 3;
            s.Targets[0] = eye;
            Equal("classic-second-track-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void EyeSecondBrakeTimer()
        {
            var s = EyeSnapshot();
            var eye = s.Targets[0];
            eye.Ai0 = 3;
            eye.Ai1 = 2;
            eye.Ai2 = 40;
            s.Targets[0] = eye;
            Equal("classic-second-brake-recover-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            s.Difficulty.Expert = true;
            Equal("expert-second-charge-committed-exit", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            eye.Ai2 = 50;
            s.Targets[0] = eye;
            Equal("expert-second-brake-recover-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void EyeFastTimer()
        {
            var s = EyeSnapshot();
            s.Difficulty.Master = true; // Master uses native expert AI as well.
            var eye = s.Targets[0];
            eye.Ai0 = 3;
            eye.Ai1 = 4;
            eye.Ai2 = 10;
            var native = EyeThresholdOracle(.04, false);
            for (var life = 159; life <= 161; life++)
            {
                eye.Life = life;
                s.Targets[0] = eye;
                Equal(native(life, 4000) ? "master-fast-brake-recover-runway" : "master-fast-charge-committed-exit",
                    new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            }
            eye.Life = 400;
            eye.Ai2 = 19; // Native may hold this timer while still within 200px.
            s.Targets[0] = eye;
            var engine = new BossStrategyEngine();
            for (var tick = 0; tick < 40; tick++)
                Equal("master-fast-charge-committed-exit", engine.Evaluate(s).Directive.PhaseId);
            eye.Ai2 = 20;
            s.Targets[0] = eye;
            Equal("master-fast-brake-recover-runway", engine.Evaluate(s).Directive.PhaseId);
        }

        private static void EyeRepositionThreshold()
        {
            var s = EyeSnapshot();
            s.Difficulty.Expert = true;
            var eye = s.Targets[0];
            eye.Ai0 = 3;
            var native = EyeThresholdOracle(.12, false);
            for (var life = 479; life <= 481; life++)
            {
                eye.Life = life;
                s.Targets[0] = eye;
                Equal(native(life, 4000) ? "expert-fast-reposition-bait" : "expert-second-track-bait",
                    new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            }
            eye.Ai1 = 5;
            eye.Life = 3000;
            s.Targets[0] = eye;
            Equal("expert-fast-reposition-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void EyeCommittedExit()
        {
            var s = EyeSnapshot();
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            var eye = s.Targets[0];
            eye.Ai1 = 2;
            eye.Velocity = new Vec2(7, 0);
            s.Targets[0] = eye;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            eye.Position.X = 1960;
            eye.Ai2 = 20;
            s.Targets[0] = eye;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            True(engine.Evaluate(s).Directive.UseExplicitMovement);
        }

        private static void EyeRecoveryTurn()
        {
            var s = EyeSnapshot();
            s.Arena.ClearanceRight = 220;
            s.Arena.FloorSupport.Right = 2040;
            var engine = new BossStrategyEngine();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void EyeChargeEdge()
        {
            var s = EyeSnapshot();
            s.Arena.ClearanceRight = 80;
            var eye = s.Targets[0];
            eye.Ai1 = 2;
            eye.Velocity = new Vec2(7, 0);
            s.Targets[0] = eye;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void EyeUnsafeReturn()
        {
            var s = EyeSnapshot();
            s.Arena.ClearanceRight = 220;
            s.Arena.FloorSupport.Right = 2040;
            var eye = s.Targets[0];
            eye.Position = new Vec2(1670, 940);
            eye.Velocity = new Vec2(4, 0);
            s.Targets[0] = eye;
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent >= 0);
        }

        private static void EyeLowFriction()
        {
            var s = EyeSnapshot();
            s.Player.RunSlowdown = .01f;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void EyeGroundHop()
        {
            var s = EyeSnapshot();
            var engine = new BossStrategyEngine();
            engine.Evaluate(s); // Establish rightward runway before this charge.
            var eye = s.Targets[0];
            eye.Position = new Vec2(2100, 985);
            eye.Velocity = new Vec2(-6.8f, 0);
            eye.Ai0 = 3;
            eye.Ai1 = 2;
            s.Targets[0] = eye;
            var d = engine.Evaluate(s).Directive;
            Equal(JumpAction.Hold, d.JumpAction);
            Equal(1, d.VerticalIntent);
            s.Player.OnGround = false;
            s.Player.Jump.RemainingTicks = 8;
            s.Player.Jump.ReleaseReady = false;
            s.Player.Position.Y = 920;
            Equal(JumpAction.Hold, engine.Evaluate(s).Directive.JumpAction);
            s.Player.Jump.RemainingTicks = 0;
            Equal(JumpAction.Release, engine.Evaluate(s).Directive.JumpAction);
        }

        private static void EyeNoUnobservedJump()
        {
            var s = EyeSnapshot();
            s.Player.Jump.Known = false;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal(JumpAction.Release, d.JumpAction);
            s.Player.Jump.Known = true;
            s.Player.OnGround = false;
            s.Player.Jump.CloudAvailable = true;
            Equal(JumpAction.Release, new BossStrategyEngine().Evaluate(s).Directive.JumpAction);
        }

        private static void EyeFirstJumpTickGroundAcceleration()
        {
            var s = EyeSnapshot();
            s.Player.Velocity = new Vec2(5f, 0f);
            var engine = new BossStrategyEngine();
            engine.Evaluate(s);
            const int crossingTick = 12;
            var oldX = s.Player.Position.X;
            var nativeX = oldX;
            var oldVx = s.Player.Velocity.X;
            var nativeVx = oldVx;
            var hopY = s.Player.Position.Y;
            var hopVy = 0f;
            var jump = s.Player.Jump;
            for (var tick = 1; tick <= crossingTick; tick++)
            {
                float travel;
                oldVx = HorizontalMotion.Advance(s.Player, oldVx, 1, false, 1, out travel);
                oldX += travel;
                nativeVx = HorizontalMotion.Advance(s.Player, nativeVx, 1, tick == 1, 1, out travel);
                nativeX += travel;
                JumpMotion.ApplyJump(ref jump, ref hopVy, true, false);
                hopVy = JumpMotion.ApplyGravity(hopVy, s.Player.Gravity, s.Player.MaxFallSpeed, false);
                hopY += hopVy;
            }
            True(nativeX > oldX, "the first grounded tick must earn sprint acceleration");
            var eye = s.Targets[0];
            eye.Ai0 = 3;
            eye.Ai1 = 2;
            eye.Velocity = new Vec2(-6.8f, 0f);
            // Put a low horizontal dash between the two predicted horizontal
            // envelopes at tick 12. The native envelope still overlaps by .1px
            // vertically; one tick later the held jump clears vertically. The
            // old all-airborne predictor therefore accepted this unsafe hop.
            eye.Position = new Vec2((oldX + nativeX) * .5f + s.Player.Width + 24f - eye.Velocity.X * crossingTick,
                hopY + s.Player.Height + 24f - .1f);
            s.Targets[0] = eye;
            Equal(JumpAction.Release, engine.Evaluate(s).Directive.JumpAction);
        }

        private static void EyeResetDirection()
        {
            var s = EyeSnapshot();
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            engine.Reset();
            var eye = s.Targets[0];
            eye.Position.X = 2300;
            eye.Ai1 = 2;
            s.Targets[0] = eye;
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void EyeFireGuards()
        {
            var s = EyeSnapshot();
            var eye = s.Targets[0];
            eye.HasLineOfSight = false;
            s.Targets[0] = eye;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
            eye.HasLineOfSight = true;
            eye.Invulnerable = true;
            s.Targets[0] = eye;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
        }

        private static Func<int, int, bool> EyeThresholdOracle(double threshold, bool single)
        {
            var method = new DynamicMethod("EyeNativeThreshold", typeof(bool), new[] { typeof(int), typeof(int) });
            var il = method.GetILGenerator();
            var no = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(single ? OpCodes.Conv_R4 : OpCodes.Conv_R8);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(single ? OpCodes.Conv_R4 : OpCodes.Conv_R8);
            if (single) il.Emit(OpCodes.Ldc_R4, (float)threshold); else il.Emit(OpCodes.Ldc_R8, threshold);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Bge_Un_S, no);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(no);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            return (Func<int, int, bool>)method.CreateDelegate(typeof(Func<int, int, bool>));
        }
    }
}
