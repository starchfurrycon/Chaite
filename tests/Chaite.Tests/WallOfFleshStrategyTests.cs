using Chaite.Core;
using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunWallOfFleshRegressions()
        {
            Run(nameof(WallNativeSpeedMatchesEvery1458Boundary),
                WallNativeSpeedMatchesEvery1458Boundary);
            Run(nameof(WallAdmissionUsesRemainingFightSpeedUpperBound),
                WallAdmissionUsesRemainingFightSpeedUpperBound);
            Run(nameof(WallRejectsMalformedMouthTunnelAndEyeState),
                WallRejectsMalformedMouthTunnelAndEyeState);
            Run(nameof(WallPublishesEveryNativeHealthSpeedBand),
                WallPublishesEveryNativeHealthSpeedBand);
            Run(nameof(WallLowLifeTakeoverDoesNotCloseWhileBeingCaught),
                WallLowLifeTakeoverDoesNotCloseWhileBeingCaught);
            Run(nameof(WallRunwayOwnsItsForwardDirectionBeyondGenericGap),
                WallRunwayOwnsItsForwardDirectionBeyondGenericGap);
            Run(nameof(WallActiveTakeoverRejectsMouthAlreadyPastPlayer),
                WallActiveTakeoverRejectsMouthAlreadyPastPlayer);
            Run(nameof(WallActiveTakeoverDoesNotCloseOnContactOnlyLead),
                WallActiveTakeoverDoesNotCloseOnContactOnlyLead);
        }

        private static void WallNativeSpeedMatchesEvery1458Boundary()
        {
            const int lifeMax = 1000000;
            var classicThresholds = new[] { 750000, 500000, 250000, 100000 };
            var expertThresholds = new[]
            {
                750000, 660000, 500000, 330000, 250000,
                100000, 50000, 35000, 25000
            };
            foreach (var mode in WallDifficultyCases())
            {
                var thresholds = mode.Expert || mode.Master ?
                    expertThresholds : classicThresholds;
                for (var i = 0; i < thresholds.Length; i++)
                {
                    var boundary = thresholds[i];
                    var at = InvokeWallNativeSpeed(boundary, lifeMax, mode);
                    var below = InvokeWallNativeSpeed(boundary - 1, lifeMax,
                        mode);
                    Equal(ReferenceWallSpeed(boundary, lifeMax, mode), at);
                    Equal(ReferenceWallSpeed(boundary - 1, lifeMax, mode),
                        below);
                    True(below > at, "native speed did not step below " +
                        boundary + "/" + lifeMax + " in " +
                        WallDifficultyName(mode));
                }
            }
        }

        private static void WallAdmissionUsesRemainingFightSpeedUpperBound()
        {
            foreach (var mode in WallDifficultyCases())
            {
                var scene = WallScene(mode, 800000, 1000000);
                var requirements = new BossStrategyEngine().Evaluate(scene).
                    Requirements;
                var required = ReferenceWallSpeed(0, 1, mode);
                Equal(required, requirements.RequiredHorizontalSpeedFor(mode));

                scene.Player.MaxRunSpeed = required;
                string reason;
                True(requirements.IsMet(scene, out reason),
                    WallDifficultyName(mode) + " exact boundary: " + reason);

                scene.Player.MaxRunSpeed = PreviousPositiveSingle(required);
                False(requirements.IsMet(scene, out reason),
                    WallDifficultyName(mode) +
                    " admitted the next float below native maximum");
            }
        }

        private static void WallRejectsMalformedMouthTunnelAndEyeState()
        {
            AssertWallRejected(s => s.Targets[0] = WithMouthAi(s.Targets[0],
                0f, 1f), "unobservable mouth ai2=1");
            AssertWallRejected(s => s.Targets[0] = WithMouthAi(s.Targets[0],
                2701f, 0f), "mouth charge clock above 2700");
            AssertWallRejected(s => s.Targets[0] = WithMouthAi(s.Targets[0],
                61f, 2f), "mouth burst clock above 60");
            AssertWallRejected(s =>
            {
                var mouth = WithMouthAi(s.Targets[0], 0f, 4f);
                mouth.Life = 800000;
                mouth.LifeMax = 1000000;
                s.Targets[0] = mouth;
            }, "high-life mouth stage four");
            AssertWallRejected(s =>
            {
                var tunnel = s.PriorityBoss.WallOfFleshTunnels[0];
                tunnel.NativeDirection = 0;
                s.PriorityBoss.WallOfFleshTunnels[0] = tunnel;
            }, "zero native tunnel direction");
            AssertWallRejected(s =>
            {
                var tunnel = s.PriorityBoss.WallOfFleshTunnels[0];
                tunnel.DrawAreaBottomPixels = tunnel.DrawAreaTopPixels + 159;
                s.PriorityBoss.WallOfFleshTunnels[0] = tunnel;
            }, "tunnel below vanilla minimum height");

            var eyeScene = WallScene(new DifficultySnapshot(), 800000,
                1000000, true);
            var eye = eyeScene.Targets[1];
            eye.LineOfSightKnown = false;
            eyeScene.Targets[1] = eye;
            RefreshPriorityNativeContext(eyeScene);
            True(new BossStrategyEngine().Evaluate(eyeScene).Directive.
                RequestControlReturn, "unknown eye LOS was accepted");

            eyeScene = WallScene(new DifficultySnapshot(), 800000,
                1000000, true);
            var nativeEye = eyeScene.PriorityBoss.WallOfFleshEyes[0];
            nativeEye.LocalAi1ChargeTimer += 1f;
            eyeScene.PriorityBoss.WallOfFleshEyes[0] = nativeEye;
            True(new BossStrategyEngine().Evaluate(eyeScene).Directive.
                RequestControlReturn, "inconsistent eye clock was accepted");

            var leechScene = WallScene(new DifficultySnapshot(), 1999, 10000);
            leechScene.Targets[0] = WithMouthAi(leechScene.Targets[0], 44f,
                2f);
            var leechPhase = new BossStrategyEngine().Evaluate(leechScene).
                Directive.PhaseId;
            True(leechPhase.Contains("leech-burst-2") &&
                !leechPhase.Contains("imminent"), leechPhase);
            leechScene.Targets[0] = WithMouthAi(leechScene.Targets[0], 45f,
                2f);
            leechPhase = new BossStrategyEngine().Evaluate(leechScene).
                Directive.PhaseId;
            True(leechPhase.Contains("leech-burst-imminent-16"),
                leechPhase);
        }

        private static void WallPublishesEveryNativeHealthSpeedBand()
        {
            AssertWallPhase(new DifficultySnapshot(), 800000,
                "speed-1000-to-750");
            AssertWallPhase(new DifficultySnapshot(), 700000,
                "speed-750-to-500");
            AssertWallPhase(new DifficultySnapshot(), 400000,
                "speed-500-to-250");
            AssertWallPhase(new DifficultySnapshot(), 200000,
                "speed-250-to-100");
            AssertWallPhase(new DifficultySnapshot(), 50000,
                "speed-under-100");

            var expert = new DifficultySnapshot { Expert = true };
            var lives = new[]
            {
                800000, 700000, 600000, 400000, 300000,
                200000, 70000, 40000, 30000, 20000
            };
            var phases = new[]
            {
                "speed-1000-to-750", "speed-750-to-660",
                "speed-660-to-500", "speed-500-to-330",
                "speed-330-to-250", "speed-250-to-100",
                "speed-100-to-050", "speed-050-to-035",
                "speed-035-to-025", "speed-under-025"
            };
            for (var i = 0; i < lives.Length; i++)
                AssertWallPhase(expert, lives[i], phases[i]);
        }

        private static void WallLowLifeTakeoverDoesNotCloseWhileBeingCaught()
        {
            var difficulty = new DifficultySnapshot
            {
                Expert = true,
                Master = true,
                ForTheWorthy = true
            };
            var scene = WallScene(difficulty, 1, 1000000);
            var wallSpeed = ReferenceWallSpeed(1, 1000000, difficulty);
            scene.Arena.LocalOpenBounds = new RectF(-10000f, 0f,
                40000f, 2400f);
            scene.Player.Position = new Vec2(10000f, 800f);
            var mouth = scene.Targets[0];
            mouth.Position = new Vec2(scene.Player.Position.X + 650f,
                mouth.Position.Y);
            mouth.Velocity = new Vec2(-wallSpeed, 0f);
            scene.Targets[0] = mouth;
            scene.Player.MaxRunSpeed = ReferenceWallSpeed(0, 1,
                scene.Difficulty) + 1f;
            scene.Player.Velocity = new Vec2(-8f, 0f);
            RefreshWallRunway(scene);
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);
            var initialSeparation = mouth.Center.X - scene.Player.Center.X;
            var minimumSeparation = initialSeparation;
            var reachedWallSpeed = false;
            var reachedStableLoop = false;
            for (var frame = 0; frame < 360; frame++)
            {
                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn,
                    "recoverable low-life takeover returned control on frame " +
                    (frame + 1) + ": " + plan.ControlReturnReason);
                var forwardSpeed = -scene.Player.Velocity.X;
                if (forwardSpeed + .001f < wallSpeed)
                {
                    False(plan.TacticalMode == TacticalMode.StablePattern,
                        "a slower trajectory falsely entered StablePattern on frame " +
                        (frame + 1));
                    True(plan.PhaseId != null && plan.PhaseId.Contains(
                        "acquire-native-speed"),
                        "missing native-speed acquisition phase on frame " +
                        (frame + 1));
                }

                float travel;
                scene.Player.Velocity.X = HorizontalMotion.Advance(
                    scene.Player, scene.Player.Velocity.X,
                    plan.Horizontal, true, 1, out travel);
                scene.Player.Position.X += travel;
                mouth = scene.Targets[0];
                mouth.Position.X -= wallSpeed;
                mouth.Velocity.X = -wallSpeed;
                scene.Targets[0] = mouth;
                RefreshWallRunway(scene);
                RefreshPriorityNativeContext(scene);

                var separation = mouth.Center.X - scene.Player.Center.X;
                minimumSeparation = Math.Min(minimumSeparation, separation);
                True(separation > (scene.Player.Width + mouth.Width) * .5f,
                    "Wall caught the player during deterministic recovery on frame " +
                    (frame + 1));
                reachedWallSpeed |= -scene.Player.Velocity.X + .001f >=
                    wallSpeed;
                reachedStableLoop |= plan.TacticalMode ==
                    TacticalMode.StablePattern;
                if (reachedStableLoop && separation > initialSeparation + 96f)
                    break;
            }
            True(minimumSeparation < initialSeparation,
                "fixture never exercised the initially losing trajectory");
            True(reachedWallSpeed,
                "admitted route never accelerated to the current Wall speed");
            True(reachedStableLoop,
                "admitted route never converged into StablePattern");
            True(scene.Targets[0].Center.X - scene.Player.Center.X >
                initialSeparation + 96f,
                "recovery did not rebuild a useful separation margin");
        }

        private static void WallRunwayOwnsItsForwardDirectionBeyondGenericGap()
        {
            var scene = WallScene(new DifficultySnapshot(), 800000, 1000000);
            var wallSpeed = ReferenceWallSpeed(800000, 1000000,
                scene.Difficulty);
            scene.Arena.LocalOpenBounds = new RectF(-10000f, 0f,
                40000f, 2400f);
            scene.Player.Position = new Vec2(900f, 800f);
            scene.Player.MaxRunSpeed = ReferenceWallSpeed(0, 1,
                scene.Difficulty) + 1f;
            scene.Player.Velocity = new Vec2(-wallSpeed, 0f);
            var mouth = scene.Targets[0];
            mouth.Position = new Vec2(2600f, mouth.Position.Y);
            mouth.Velocity = new Vec2(-wallSpeed, 0f);
            mouth.NativeDirectionKnown = true;
            mouth.NativeDirection = -1;
            scene.Targets[0] = mouth;
            RefreshWallRunway(scene);
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            Equal(-1, plan.Horizontal);
            True(plan.TacticalMode == TacticalMode.RecoverToPattern,
                "the initial active-Wall frame must still join the runway");
        }

        private static void WallActiveTakeoverRejectsMouthAlreadyPastPlayer()
        {
            var scene = WallScene(new DifficultySnapshot(), 800000, 1000000);
            var wallSpeed = ReferenceWallSpeed(800000, 1000000,
                scene.Difficulty);
            scene.Arena.LocalOpenBounds = new RectF(-10000f, 0f,
                40000f, 2400f);
            scene.Player.Position = new Vec2(3000f, 800f);
            scene.Player.MaxRunSpeed = ReferenceWallSpeed(0, 1,
                scene.Difficulty) + 1f;
            scene.Player.Velocity = new Vec2(-wallSpeed, 0f);
            var mouth = scene.Targets[0];
            mouth.Position = new Vec2(2000f, mouth.Position.Y);
            mouth.Velocity = new Vec2(-wallSpeed, 0f);
            mouth.NativeDirectionKnown = true;
            mouth.NativeDirection = -1;
            scene.Targets[0] = mouth;
            RefreshWallRunway(scene);
            RefreshPriorityNativeContext(scene);

            var decision = new BossStrategyEngine().Evaluate(scene);
            True(decision.Directive.RequestControlReturn,
                "a Wall that already passed the player was admitted");
            True(decision.Directive.ControlReturnReason.Contains(
                "already at or ahead"), decision.Directive.ControlReturnReason);

            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            Equal(ActiveEncounterPreparationResult.Rejected,
                planner.PrepareForActiveEncounterDetailed(scene, out reason));
            True(reason.Contains("already at or ahead"), reason);
            Equal(-1, planner.LatchedOutputSlot);
            Equal(0, planner.LatchedOutputWeaponId);
        }

        private static void WallActiveTakeoverDoesNotCloseOnContactOnlyLead()
        {
            var scene = WallScene(new DifficultySnapshot(), 800000, 1000000);
            var wallSpeed = ReferenceWallSpeed(800000, 1000000,
                scene.Difficulty);
            scene.Arena.LocalOpenBounds = new RectF(-10000f, 0f,
                40000f, 2400f);
            scene.Player.Position = new Vec2(1200f, 800f);
            scene.Player.MaxRunSpeed = ReferenceWallSpeed(0, 1,
                scene.Difficulty) + 1f;
            scene.Player.Velocity = new Vec2(-wallSpeed, 0f);
            var mouth = scene.Targets[0];
            var contact = (scene.Player.Width + mouth.Width) * .5f;
            // Direction -1 means a valid runner is left of the mouth. Leave
            // only four pixels above body contact: enough not to be already
            // passed, but far below the reviewed reaction/braking lead.
            mouth.Position = new Vec2(scene.Player.Center.X + contact + 4f -
                mouth.Width * .5f, mouth.Position.Y);
            mouth.Velocity = new Vec2(-wallSpeed, 0f);
            mouth.NativeDirectionKnown = true;
            mouth.NativeDirection = -1;
            scene.Targets[0] = mouth;
            RefreshWallRunway(scene);
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);
            for (var frame = 0; frame < 3; frame++)
            {
                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn, plan.ControlReturnReason);
                Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            }
        }

        private static void RefreshWallRunway(CombatSnapshot scene)
        {
            scene.Arena.ClearanceLeft = scene.Player.Position.X -
                scene.Arena.LocalOpenBounds.Left;
            scene.Arena.ClearanceRight = scene.Arena.LocalOpenBounds.Right -
                scene.Player.Position.X - scene.Player.Width;
            scene.Arena.ClearanceUp = scene.Player.Position.Y -
                scene.Arena.LocalOpenBounds.Top;
            scene.Arena.ClearanceDown = scene.Arena.LocalOpenBounds.Bottom -
                scene.Player.Position.Y - scene.Player.Height;
        }

        private static CombatSnapshot WallScene(DifficultySnapshot difficulty,
            int life, int lifeMax, bool withEye = false)
        {
            var scene = withEye ? CombatScenario(113, 114) :
                CombatScenario(113);
            scene.Difficulty.Expert = difficulty.Expert;
            scene.Difficulty.Master = difficulty.Master;
            scene.Difficulty.ForTheWorthy = difficulty.ForTheWorthy;
            var mouth = scene.Targets[0];
            mouth.Life = life;
            mouth.LifeMax = lifeMax;
            mouth.Ai1 = 0f;
            mouth.Ai2 = 0f;
            scene.Targets[0] = mouth;
            if (withEye)
            {
                var eye = scene.Targets[1];
                eye.Ai0 = 1f;
                eye.Ai0Known = true;
                eye.LocalAi1 = 0f;
                eye.LocalAi2 = 0f;
                eye.LocalAi1Known = true;
                eye.LocalAi2Known = true;
                eye.LineOfSightKnown = true;
                eye.HasLineOfSight = true;
                scene.Targets[1] = eye;
            }
            RefreshPriorityNativeContext(scene);
            return scene;
        }

        private static TargetSnapshot WithMouthAi(TargetSnapshot mouth,
            float ai1, float ai2)
        {
            mouth.Ai1 = ai1;
            mouth.Ai2 = ai2;
            return mouth;
        }

        private static void AssertWallRejected(Action<CombatSnapshot> mutate,
            string label)
        {
            var scene = WallScene(new DifficultySnapshot(), 800000, 1000000);
            mutate(scene);
            True(new BossStrategyEngine().Evaluate(scene).Directive.
                RequestControlReturn, label + " was accepted");
        }

        private static void AssertWallPhase(DifficultySnapshot difficulty,
            int life, string expected)
        {
            var scene = WallScene(difficulty, life, 1000000);
            scene.Player.Velocity = new Vec2(-20f, 0f);
            var decision = new BossStrategyEngine().Evaluate(scene);
            False(decision.Directive.RequestControlReturn,
                "valid Wall phase returned control: " + expected);
            True(decision.Directive.PhaseId.Contains(expected),
                "expected " + expected + ", got " +
                decision.Directive.PhaseId);
        }

        private static DifficultySnapshot[] WallDifficultyCases() => new[]
        {
            new DifficultySnapshot(),
            new DifficultySnapshot { Expert = true },
            new DifficultySnapshot { Expert = true, Master = true },
            new DifficultySnapshot { ForTheWorthy = true },
            new DifficultySnapshot { Expert = true, ForTheWorthy = true },
            new DifficultySnapshot
            {
                Expert = true, Master = true, ForTheWorthy = true
            }
        };

        private static string WallDifficultyName(DifficultySnapshot value) =>
            (value.Master ? "master" : value.Expert ? "expert" : "classic") +
            (value.ForTheWorthy ? "+ftw" : string.Empty);

        private static float InvokeWallNativeSpeed(int life, int lifeMax,
            DifficultySnapshot difficulty)
        {
            var type = typeof(BossRequirements).Assembly.GetType(
                "Chaite.Core.WallStrategy", true);
            var method = type.GetMethod("NativeHorizontalSpeed",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null, "missing Wall native speed contract");
            return (float)method.Invoke(null,
                new object[] { life, lifeMax, difficulty });
        }

        private static float ReferenceWallSpeed(int life, int lifeMax,
            DifficultySnapshot difficulty)
        {
            var speed = 1.5f;
            if ((double)life < (double)lifeMax * .75) speed += .25f;
            if ((double)life < (double)lifeMax * .5) speed += .4f;
            if ((double)life < (double)lifeMax * .25) speed += .5f;
            if ((double)life < (double)lifeMax * .1) speed += .6f;
            if (difficulty.Expert || difficulty.Master)
            {
                if ((double)life < (double)lifeMax * .66) speed += .3f;
                if ((double)life < (double)lifeMax * .33) speed += .3f;
                if ((double)life < (double)lifeMax * .05) speed += .6f;
                if ((double)life < (double)lifeMax * .035) speed += .6f;
                if ((double)life < (double)lifeMax * .025) speed += .6f;
                speed *= 1.35f;
                speed += .35f;
            }
            if (difficulty.ForTheWorthy)
            {
                speed *= 1.1f;
                speed += .2f;
            }
            return speed;
        }

        private static float PreviousPositiveSingle(float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits - 1), 0);
        }
    }
}
