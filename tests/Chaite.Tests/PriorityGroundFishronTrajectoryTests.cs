using Chaite.Core;
using System;
using System.Collections.Generic;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPriorityGroundFishronTrajectoryRegressions()
        {
            Run(nameof(QueenBeeReachableTuplesSupportMidCycleTakeover),
                QueenBeeReachableTuplesSupportMidCycleTakeover);
            Run(nameof(SkeletronIndependentHandLinesSupportMidCycleTakeover),
                SkeletronIndependentHandLinesSupportMidCycleTakeover);
            Run(nameof(DeerclopsNativeTimelineAndSlowSpikeJumpAreClosed),
                DeerclopsNativeTimelineAndSlowSpikeJumpAreClosed);
            Run(nameof(FishronAllReachableSequencesSupportMidCycleTakeover),
                FishronAllReachableSequencesSupportMidCycleTakeover);
            Run(nameof(FishronNativeTimerBoundariesFollowCurrentVersion),
                FishronNativeTimerBoundariesFollowCurrentVersion);
            Run(nameof(PriorityBossPhaseVocabularyIsFiniteAcrossLongClocks),
                PriorityBossPhaseVocabularyIsFiniteAcrossLongClocks);
        }

        private static void QueenBeeReachableTuplesSupportMidCycleTakeover()
        {
            foreach (var mode in new[] { 0, 1, 2, 3 })
            {
                foreach (var leftStart in new[] { true, false })
                {
                    var scene = CombatScenario(222);
                    ConfigureDifficulty(scene, mode);
                    scene.Player.Position = new Vec2(leftStart ? 600f :
                        3900f, 800f);
                    RefreshArenaFromPlayer(scene);
                    var bee = scene.Targets[0];
                    bee.LifeMax = 3000;
                    bee.Life = mode == 0 ? 2400 : 180;
                    bee.Position = new Vec2(2200f, 620f);
                    scene.Targets[0] = bee;

                    // AI_043 exposes one final even value after the last
                    // charge finishes braking, before state -1 consumes it.
                    var maxSequence = mode == 1 || mode == 2 ? 12 : 6;
                    for (var sequence = 0; sequence <= maxSequence;
                        sequence++)
                    {
                        SetBossAi(scene, 0f, sequence, sequence % 2, 0f,
                            new Vec2(sequence % 2 == 0 ? 0f :
                                (leftStart ? -16f : 16f), 0f));
                        AssertJoinable(scene, "Queen charge sequence " +
                            sequence);
                    }
                    for (var wave = 0; wave <= 5; wave++)
                    {
                        SetBossAi(scene, 1f, 10.25f + wave, wave, 0f,
                            new Vec2(2f, -1f));
                        AssertJoinable(scene, "Queen bee-wave " + wave);
                    }
                    SetBossAi(scene, -1f, 6f, 5f, 0f, new Vec2());
                    AssertJoinable(scene, "Queen choose state");
                    SetBossAi(scene, 2f, 0f, 0f, 0f, new Vec2());
                    AssertJoinable(scene, "Queen reposition state");
                    SetBossAi(scene, 3f, 20f, 0f, 0f, new Vec2());
                    AssertJoinable(scene, "Queen stinger state");
                    SetBossAi(scene, 4f, 7f, 1f, 0f, new Vec2());
                    AssertJoinable(scene, "Queen reacquire state");
                }
            }

            var invalid = CombatScenario(222);
            SetBossAi(invalid, 0f, 999f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Queen impossible charge count");
            SetBossAi(invalid, 1f, 2f, 6f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Queen impossible bee count");
            SetBossAi(invalid, 1f, 41f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Queen impossible bee clock");
            SetBossAi(invalid, 2f, 1f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Queen state-2 retained clock");
            SetBossAi(invalid, 2f, -300f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid,
                "Queen state-2 synthetic negative horizontal offset");
            SetBossAi(invalid, 2f, 300f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid,
                "Queen state-2 synthetic positive horizontal offset");

            SetBossAi(invalid, 5f, 0f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Queen native departure");
        }

        private static void SkeletronIndependentHandLinesSupportMidCycleTakeover()
        {
            foreach (var mode in new[] { 0, 1, 2, 3 })
            {
                foreach (var leftStart in new[] { true, false })
                {
                    var scene = CombatScenario(35);
                    ConfigureDifficulty(scene, mode);
                    scene.Player.Position = new Vec2(leftStart ? 700f :
                        3900f, 800f);
                    RefreshArenaFromPlayer(scene);
                    SetBossAi(scene, 0f, 0f, 790f, 0f, new Vec2());
                    AssertJoinable(scene, "Skeletron pre-spin");
                    SetBossAi(scene, 0f, 1f, 200f, 0f,
                        new Vec2(leftStart ? -5f : 5f, 2f));
                    AssertJoinable(scene, "Skeletron spin");

                    foreach (var committedState in new[] { 2, 5 })
                    {
                        var spinAndHand = CombatScenario(35);
                        ConfigureDifficulty(spinAndHand, mode);
                        spinAndHand.Player.Position = scene.Player.Position;
                        RefreshArenaFromPlayer(spinAndHand);
                        SetBossAi(spinAndHand, 0f, 1f, 200f, 0f,
                            new Vec2(leftStart ? -5f : 5f, 2f));
                        spinAndHand.Targets.Add(SkeletronHand(
                            spinAndHand.Targets[0].Key, 76 + committedState,
                            -1, committedState, 0f,
                            committedState == 2 ? new Vec2(3f, 18f) :
                            new Vec2(20f, 2f)));
                        var overlap = AssertJoinable(spinAndHand,
                            "Skeletron spin plus committed hand " +
                            committedState);
                        True(overlap.PhaseId.Contains(committedState == 2 ?
                            "hand-vertical-dive-committed" :
                            "hand-horizontal-dive-committed"));
                        True(overlap.OwnsMovementClosure);
                        True(overlap.HorizontalIntent != 0 ||
                            overlap.VerticalIntent != 0,
                            "committed hand overlap produced no escape input");
                    }

                    for (var state = 0; state <= 5; state++)
                    {
                        var handScene = CombatScenario(35);
                        ConfigureDifficulty(handScene, mode);
                        handScene.Player.Position = scene.Player.Position;
                        RefreshArenaFromPlayer(handScene);
                        handScene.Targets.Add(SkeletronHand(
                            handScene.Targets[0].Key, 71, -1, state,
                            state == 0 || state == 3 ? 280f : 0f,
                            state == 2 ? new Vec2(3f, 18f) :
                            state == 5 ? new Vec2(20f, 2f) : new Vec2()));
                        AssertJoinable(handScene, "Skeletron hand state " +
                            state);
                    }
                }
            }

            var simultaneous = CombatScenario(35);
            simultaneous.Targets.Add(SkeletronHand(
                simultaneous.Targets[0].Key, 72, -1, 2, 0f,
                new Vec2(3f, 18f)));
            simultaneous.Targets.Add(SkeletronHand(
                simultaneous.Targets[0].Key, 73, 1, 5, 0f,
                new Vec2(-20f, 2f)));
            var decision = new BossStrategyEngine().Evaluate(simultaneous);
            False(decision.Directive.RequestControlReturn);
            True(decision.Directive.PhaseId.Contains(
                "hand-vertical-dive-committed"));
            True(decision.Directive.OwnsMovementClosure);
            True(decision.Directive.HorizontalIntent != 0,
                "vertical hand line did not reserve a perpendicular exit");

            var invalid = CombatScenario(35);
            invalid.Targets.Add(SkeletronHand(999, 74, 1, 0, 0f,
                new Vec2()));
            AssertReturnsControl(invalid, "Skeletron foreign-parent hand");
            invalid = CombatScenario(35);
            invalid.Targets.Add(SkeletronHand(invalid.Targets[0].Key, 75,
                0, 0, 0f, new Vec2()));
            AssertReturnsControl(invalid, "Skeletron zero-side hand");
        }

        private static void DeerclopsNativeTimelineAndSlowSpikeJumpAreClosed()
        {
            var states = new[]
            {
                new[] { -1, 0 }, new[] { 0, 0 }, new[] { 0, 240 },
                new[] { 1, 0 }, new[] { 1, 79 }, new[] { 2, 59 },
                new[] { 3, 59 }, new[] { 4, 89 }, new[] { 5, 59 },
                new[] { 6, 1500 }, new[] { 7, 59 }
            };
            foreach (var mode in new[] { 0, 1, 2, 3 })
            {
                for (var i = 0; i < states.Length; i++)
                {
                    var scene = CombatScenario(668);
                    ConfigureDifficulty(scene, mode);
                    SetBossAi(scene, states[i][0], states[i][1], 100f,
                        100f, new Vec2());
                    AssertJoinable(scene, "Deer state " + states[i][0] +
                        " clock " + states[i][1]);
                }
            }

            var passive = CombatScenario(668);
            passive.Difficulty.Expert = true;
            var deer = passive.Targets[0];
            deer.LocalAi2 = 0f;
            passive.Targets[0] = deer;
            RefreshPriorityNativeContext(passive);
            var justReset = new BossStrategyEngine().Evaluate(passive).
                Directive;
            False(justReset.PhaseId.Contains("passive-shadow-hand-imminent"),
                "localAI[2]==0 was incorrectly treated as the next-frame spawn");
            deer.LocalAi2 = 66f; // (int)(40 + 40 * 2/3) => 66
            passive.Targets[0] = deer;
            RefreshPriorityNativeContext(passive);
            var fullPeriod = new BossStrategyEngine().Evaluate(passive).
                Directive;
            False(fullPeriod.PhaseId.Contains("passive-shadow-hand-imminent"),
                "an exact native period is a just-fired, not future, edge");
            deer.LocalAi2 = 64f;
            passive.Targets[0] = deer;
            RefreshPriorityNativeContext(passive);
            True(new BossStrategyEngine().Evaluate(passive).Directive.PhaseId.
                Contains("passive-shadow-hand-imminent"));

            SimulateDeerclopsSlowSpikeJump(-1);
            SimulateDeerclopsSlowSpikeJump(1);

            var invalid = CombatScenario(668);
            SetBossAi(invalid, 1f, 81f, 100f, 100f, new Vec2());
            AssertReturnsControl(invalid, "Deer state-1 timer overflow");
            SetBossAi(invalid, 8f, 0f, 100f, 100f, new Vec2());
            AssertReturnsControl(invalid, "Deer native disappearance");
        }

        private static void SimulateDeerclopsSlowSpikeJump(int direction)
        {
            var scene = CombatScenario(668);
            scene.Player.Position = new Vec2(direction < 0 ? 1880f :
                2080f, 958f);
            scene.Player.Velocity = new Vec2(0f, 0f);
            scene.Player.OnGround = true;
            scene.Player.MaxRunSpeed = 1.575f;
            scene.Player.RunAcceleration = .12f;
            scene.Player.RunSlowdown = .2f;
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10f;
            scene.Player.Jump = new JumpSnapshot
            {
                Known = true,
                Height = 15,
                Speed = 5.01f,
                ReleaseReady = true,
                CloudEnabled = true,
                CloudAvailable = true
            };
            var boss = scene.Targets[0];
            boss.Position = new Vec2(1950f, 846f);
            boss.Width = 60;
            boss.Height = 154;
            boss.NativeDirection = direction;
            scene.Targets[0] = boss;
            var engine = new BossStrategyEngine();
            var sawBaseJump = false;
            var sawRelease = false;
            var sawCloud = false;
            var maximumHeight = 0f;
            const float floorY = 1000f;
            for (var tick = 0; tick < 80; tick++)
            {
                SetBossAi(scene, 1f, tick, 100f, 100f, new Vec2());
                var observed = scene.Targets[0];
                observed.NativeDirection = direction;
                scene.Targets[0] = observed;
                RefreshPriorityNativeContext(scene);
                RefreshArenaFromPlayer(scene);
                var directive = engine.Evaluate(scene).Directive;
                False(directive.RequestControlReturn,
                    "Deer spike takeover returned at native tick " + tick);
                True(directive.OwnsMovementClosure,
                    "Deer spike window lost its reviewed closure");
                Equal(direction, directive.HorizontalIntent);

                if (directive.JumpAction == JumpAction.Hold &&
                    scene.Player.OnGround) sawBaseJump = true;
                if (directive.JumpAction == JumpAction.Release &&
                    !scene.Player.OnGround &&
                    scene.Player.Jump.CloudAvailable) sawRelease = true;
                if (directive.JumpAction == JumpAction.Cloud) sawCloud = true;

                var velocity = scene.Player.Velocity;
                velocity.X += directive.HorizontalIntent *
                    scene.Player.RunAcceleration;
                velocity.X = Math.Max(-scene.Player.MaxRunSpeed,
                    Math.Min(scene.Player.MaxRunSpeed, velocity.X));
                var jump = scene.Player.Jump;
                JumpMotion.RefreshBeforeMovement(ref jump, velocity.Y);
                var jumpRequested = directive.VerticalIntent > 0;
                var controlJump = JumpMotion.ResolveControl(jumpRequested,
                    directive.JumpAction, in jump, scene.Player.OnGround,
                    false);
                JumpMotion.ApplyJump(ref jump, ref velocity.Y, controlJump,
                    false);
                JumpMotion.ApplyGravityChecked(ref velocity.Y,
                    scene.Player.Gravity, scene.Player.MaxFallSpeed, false,
                    false, false, false);
                var position = scene.Player.Position + velocity;
                if (position.Y + scene.Player.Height >= floorY)
                {
                    position.Y = floorY - scene.Player.Height;
                    velocity.Y = 0f;
                    scene.Player.OnGround = true;
                }
                else scene.Player.OnGround = false;
                scene.Player.Position = position;
                scene.Player.Velocity = velocity;
                scene.Player.Jump = jump;
                maximumHeight = Math.Max(maximumHeight,
                    floorY - (position.Y + scene.Player.Height));
            }
            True(sawBaseJump, "Deer spike cadence never began base jump");
            True(sawRelease, "Deer spike cadence never rearmed Cloud jump");
            True(sawCloud, "Deer spike cadence never consumed Cloud jump");
            True(maximumHeight >= 120f,
                "reviewed base/cloud cadence did not clear a useful spike arc: " +
                maximumHeight);
        }

        private static void FishronAllReachableSequencesSupportMidCycleTakeover()
        {
            var classic = new[]
            {
                NativeTuple(-1, 20, 0), NativeTuple(0, 10, 0),
                NativeTuple(1, 10, 0), NativeTuple(1, 10, 9),
                NativeTuple(2, 40, 1), NativeTuple(3, 70, 0),
                NativeTuple(4, 100, 5), NativeTuple(5, 10, 0),
                NativeTuple(6, 10, 5), NativeTuple(7, 40, 1),
                NativeTuple(8, 70, 0)
            };
            var expertOnly = new[]
            {
                NativeTuple(9, 100, 3), NativeTuple(10, 10, 8),
                NativeTuple(11, 10, 0), NativeTuple(11, 10, 2),
                NativeTuple(11, 10, 3), NativeTuple(11, 10, 5),
                NativeTuple(11, 10, 6), NativeTuple(11, 10, 7),
                NativeTuple(12, 0, 1), NativeTuple(12, 15, 4),
                NativeTuple(12, 29, 8)
            };
            foreach (var mode in new[] { 0, 1, 2, 3 })
            {
                foreach (var leftStart in new[] { true, false })
                {
                    ValidateFishronTuples(classic, mode, leftStart);
                    if (mode == 1 || mode == 2)
                        ValidateFishronTuples(expertOnly, mode, leftStart);
                }
            }

            var tornado = CombatScenario(370);
            SetBossAi(tornado, 8f, 0f, 60f, 0f, new Vec2());
            tornado.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 386,
                Position = new Vec2(tornado.Player.Center.X - 80f, 600f),
                Width = 150,
                Height = 500,
                Damage = 50,
                TimeLeft = 600
            });
            var tornadoDecision = new BossStrategyEngine().Evaluate(tornado).
                Directive;
            Equal(1, tornadoDecision.HorizontalIntent);
            True(tornadoDecision.UseExplicitMovement);

            var invalid = CombatScenario(370);
            SetBossAi(invalid, 13f, 0f, 0f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Fishron state 13 is unreachable");
            SetBossAi(invalid, 2f, 0f, 20f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Fishron bubble wrong sequence");
            invalid.Difficulty.Expert = true;
            SetBossAi(invalid, 11f, 0f, 10f, 1f, new Vec2(20f, 0f));
            AssertReturnsControl(invalid, "Fishron dash on teleport sequence");
            SetBossAi(invalid, 12f, 0f, 10f, 0f, new Vec2());
            AssertReturnsControl(invalid, "Fishron teleport wrong sequence");
        }

        private static void FishronNativeTimerBoundariesFollowCurrentVersion()
        {
            // AI_069 checks ai[2] after incrementing it.  The adapter keeps the
            // final observed boundary tick for network/interpolation tolerance,
            // while the next tick must fail closed.  State 5 has a phase-local
            // flag5 bound of six (not the phase-one bound of ten).
            AssertFishronClock(0, 0, 0, 30, true,
                "classic state 0 sequence 0 boundary");
            AssertFishronClock(0, 0, 0, 31, false,
                "classic state 0 sequence 0 overflow");
            AssertFishronClock(1, 0, 0, 30, true,
                "expert state 0 sequence 0 boundary");
            AssertFishronClock(1, 0, 0, 31, false,
                "expert state 0 sequence 0 overflow");

            AssertFishronClock(0, 5, 6, 60, true,
                "classic state 5 late-sequence boundary");
            AssertFishronClock(0, 5, 6, 61, false,
                "classic state 5 late-sequence overflow");
            AssertFishronClock(1, 5, 6, 40, true,
                "expert state 5 late-sequence boundary");
            AssertFishronClock(1, 5, 6, 41, false,
                "expert state 5 late-sequence overflow");

            // The three native enrage inputs force num3=10 in every hover /
            // reposition branch, but do not change the charge-state num6 clock.
            AssertFishronClock(0, 0, 0, 10, true,
                "enraged state 0 boundary", true);
            AssertFishronClock(0, 0, 0, 11, false,
                "enraged state 0 overflow", true);
            AssertFishronClock(0, 5, 0, 10, true,
                "enraged state 5 boundary", true);
            AssertFishronClock(0, 5, 0, 11, false,
                "enraged state 5 overflow", true);
            AssertFishronClock(1, 10, 0, 10, true,
                "enraged state 10 boundary", true);
            AssertFishronClock(1, 10, 0, 11, false,
                "enraged state 10 overflow", true);
            AssertFishronClock(0, 10, 0, 10, false,
                "classic enraged state 10 is unreachable", true);
            AssertFishronClock(3, 10, 0, 10, false,
                "classic FTW enraged state 10 is unreachable", true);
            // flag6 can redirect phase-one sequence 10 into state 3 and seed
            // its clock at 140 (num12 - 40).  A late enrage can likewise leave
            // a normal state-3 clock below that value, so only the upper bound
            // changes with the independently observed predicate.
            AssertFishronClock(0, 3, 1, 140, true,
                "enraged state 3 accelerated entry", true);
            AssertFishronClock(0, 3, 1, 180, true,
                "enraged state 3 accelerated boundary", true);
            AssertFishronClock(0, 3, 1, 181, false,
                "enraged state 3 accelerated overflow", true);
            AssertFishronClock(0, 3, 0, 90, true,
                "calm state 3 boundary");
            AssertFishronClock(0, 3, 0, 91, false,
                "calm state 3 overflow");
            AssertFishronClock(0, 3, 1, 0, false,
                "calm state 3 redirected sequence");

            AssertFishronClock(1, 1, 0, 28, true,
                "expert state 1 boundary");
            AssertFishronClock(1, 1, 0, 29, false,
                "expert state 1 overflow");
            AssertFishronClock(1, 6, 0, 27, true,
                "expert state 6 boundary");
            AssertFishronClock(1, 6, 0, 28, false,
                "expert state 6 overflow");

            // AI_069's exact enrage branch replaces num6 for every dash,
            // including phase-one, phase-two, and phase-three entries.
            AssertFishronClock(0, 1, 0, 27, true,
                "enraged state 1 boundary", true);
            AssertFishronClock(0, 1, 0, 28, false,
                "enraged state 1 overflow", true);
            AssertFishronClock(0, 6, 0, 27, true,
                "enraged state 6 boundary", true);
            AssertFishronClock(0, 6, 0, 28, false,
                "enraged state 6 overflow", true);
            AssertFishronClock(1, 11, 0, 27, true,
                "enraged state 11 boundary", true);
            AssertFishronClock(1, 11, 0, 28, false,
                "enraged state 11 overflow", true);

            AssertFishronClock(0, -1, 0, 75, true,
                "spawn fade boundary");
            AssertFishronClock(0, -1, 0, 76, false,
                "spawn fade overflow");
            AssertFishronClock(0, -1, 1, 0, false,
                "spawn state foreign sequence");

            foreach (var mode in new[] { 1, 2 })
            {
                foreach (var sequence in new[] { 0, 2, 3, 5, 6, 7 })
                    AssertFishronClock(mode, 11, sequence, 25, true,
                        "state 11 legal sequence " + sequence);
                foreach (var sequence in new[] { 1, 4, 8 })
                    AssertFishronClock(mode, 12, sequence, 30, true,
                        "state 12 legal sequence " + sequence);
            }
            foreach (var mode in new[] { 0, 3 })
            {
                AssertFishronClock(mode, 9, 0, 60, false,
                    "classic third-form transition");
                AssertFishronClock(mode, 11, 0, 10, false,
                    "classic third-form dash");
                AssertFishronClock(mode, 12, 1, 10, false,
                    "classic third-form teleport");
            }
            foreach (var sequence in new[] { 1, 4, 8 })
                AssertFishronClock(1, 11, sequence, 0, false,
                    "state 11 illegal sequence " + sequence);
            foreach (var sequence in new[] { 0, 2, 3, 5, 6, 7 })
                AssertFishronClock(1, 12, sequence, 0, false,
                    "state 12 illegal sequence " + sequence);
        }

        private static void AssertFishronClock(int mode, int state,
            int sequence, int tick, bool joinable, string label,
            bool enraged = false)
        {
            var scene = CombatScenario(370);
            ConfigureDifficulty(scene, mode);
            var target = scene.Targets[0];
            target.Ai0 = state;
            target.Ai1 = 0f;
            target.Ai2 = tick;
            target.Ai3 = sequence;
            target.Velocity = state == 1 || state == 6 || state == 11 ?
                new Vec2(16f, 0f) : new Vec2();
            scene.Targets[0] = target;
            RefreshPriorityNativeContext(scene);
            if (enraged)
            {
                // The shared test refresh helper intentionally uses a calm
                // Fishron fixture. Override the independent native observation
                // after refresh so the contract still validates all terms.
                var native = scene.PriorityBoss.DukeFishrons[0];
                native.PlayerAboveY800Band = true;
                native.NativeEnraged = true;
                scene.PriorityBoss.DukeFishrons[0] = native;
            }
            var directive = new BossStrategyEngine().Evaluate(scene).Directive;
            if (joinable)
            {
                False(directive.RequestControlReturn,
                    label + " unexpectedly returned control: " +
                    directive.ControlReturnReason);
            }
            else
            {
                True(directive.RequestControlReturn,
                    label + " did not fail closed: " + directive.PhaseId);
                False(directive.Fire, label + " fired after invalid native state");
            }
        }

        private static void ValidateFishronTuples(NativeBossTuple[] tuples,
            int mode, bool leftStart)
        {
            for (var i = 0; i < tuples.Length; i++)
            {
                var scene = CombatScenario(370);
                ConfigureDifficulty(scene, mode);
                scene.Player.Position = new Vec2(leftStart ? 600f : 3900f,
                    800f);
                RefreshArenaFromPlayer(scene);
                var tuple = tuples[i];
                var velocity = tuple.State == 1 || tuple.State == 6 ||
                    tuple.State == 11 ? new Vec2(leftStart ? -20f : 20f,
                    tuple.State == 11 ? 6f : 0f) : new Vec2();
                SetBossAi(scene, tuple.State, 0f, tuple.Tick,
                    tuple.Sequence, velocity);
                var directive = AssertJoinable(scene, "Fishron state " +
                    tuple.State + " sequence " + tuple.Sequence);
                if (tuple.State == 1 || tuple.State == 6 ||
                    tuple.State == 11)
                {
                    True(directive.UseExplicitMovement);
                    True(directive.PreferDash);
                }
                if (tuple.State == 12)
                {
                    False(directive.PreferDash);
                    True(directive.UseExplicitMovement);
                }
            }
        }

        private static void PriorityBossPhaseVocabularyIsFiniteAcrossLongClocks()
        {
            var phases = new HashSet<string>(StringComparer.Ordinal);
            var queen = CombatScenario(222);
            var queenEngine = new BossStrategyEngine();
            var deer = CombatScenario(668);
            var deerEngine = new BossStrategyEngine();
            var skeletron = CombatScenario(35);
            var skeletronEngine = new BossStrategyEngine();
            var fishron = CombatScenario(370);
            var fishronEngine = new BossStrategyEngine();
            for (var tick = 0; tick < 2400; tick++)
            {
                SetBossAi(queen, 3f, tick % 200, 0f, 0f, new Vec2());
                phases.Add(queenEngine.Evaluate(queen).Directive.PhaseId);

                SetBossAi(deer, 0f, tick, 100f, 100f, new Vec2());
                var deerTarget = deer.Targets[0];
                deerTarget.LocalAi2 = tick % 67;
                deer.Targets[0] = deerTarget;
                RefreshPriorityNativeContext(deer);
                phases.Add(deerEngine.Evaluate(deer).Directive.PhaseId);

                SetBossAi(skeletron, 0f, 0f, tick % 800, 0f,
                    new Vec2());
                phases.Add(skeletronEngine.Evaluate(skeletron).Directive.
                    PhaseId);

                SetBossAi(fishron, 0f, 0f, tick % 30, tick % 12,
                    new Vec2());
                phases.Add(fishronEngine.Evaluate(fishron).Directive.PhaseId);
            }
            True(phases.Count <= 32,
                "native diagnostic clocks grew an unbounded phase vocabulary: " +
                phases.Count);
        }

        private static BossDirective AssertJoinable(CombatSnapshot scene,
            string label)
        {
            RefreshPriorityNativeContext(scene);
            var directive = new BossStrategyEngine().Evaluate(scene).
                Directive;
            False(directive.RequestControlReturn, label + ": " +
                directive.ControlReturnReason + " / " + directive.PhaseId);
            return directive;
        }

        private static void AssertReturnsControl(CombatSnapshot scene,
            string label)
        {
            RefreshPriorityNativeContext(scene);
            var directive = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(directive.RequestControlReturn, label +
                " did not fail closed (" + directive.PhaseId + ")");
            False(directive.Fire);
        }

        private static void SetBossAi(CombatSnapshot scene, float ai0,
            float ai1, float ai2, float ai3, Vec2 velocity)
        {
            var target = scene.Targets[0];
            target.Ai0 = ai0;
            target.Ai1 = ai1;
            target.Ai2 = ai2;
            target.Ai3 = ai3;
            target.Velocity = velocity;
            scene.Targets[0] = target;
            RefreshPriorityNativeContext(scene);
        }

        private static void ConfigureDifficulty(CombatSnapshot scene,
            int mode)
        {
            scene.Difficulty.Expert = mode == 1;
            scene.Difficulty.Master = mode == 2;
            scene.Difficulty.ForTheWorthy = mode == 3;
            RefreshPriorityNativeContext(scene);
        }

        private static void RefreshArenaFromPlayer(CombatSnapshot scene)
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

        private static TargetSnapshot SkeletronHand(int parent, int key,
            int side, int state, float timer, Vec2 velocity)
        {
            return new TargetSnapshot
            {
                Key = key,
                Type = 36,
                Position = new Vec2(1900f + side * 200f, 750f),
                Velocity = velocity,
                Width = 40,
                Height = 40,
                Life = 800,
                LifeMax = 1000,
                Damage = 25,
                Chaseable = true,
                Ai0Known = true,
                Ai0 = side,
                Ai1Known = true,
                Ai1 = parent,
                Ai2Known = true,
                Ai2 = state,
                Ai3Known = true,
                Ai3 = timer
            };
        }

        private static NativeBossTuple NativeTuple(int state, int tick,
            int sequence) => new NativeBossTuple
            {
                State = state,
                Tick = tick,
                Sequence = sequence
            };

        private struct NativeBossTuple
        {
            public int State;
            public int Tick;
            public int Sequence;
        }
    }
}
