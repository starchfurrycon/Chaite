using Chaite.Core;
using System;
using System.Collections.Generic;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private sealed class EmpressNativeCase
        {
            public string Name;
            public int State;
            public int Tick;
            public int Index;
            public int Form;
            public bool Day;
            public bool Expert;
            public bool Master;
            public bool ForTheWorthy;
        }

        private sealed class MoonSourceCase
        {
            public string Name;
            public int Type;
            public int State;
            public int Clock;
            public int Side;
        }

        private static void RunPriorityEmpressMoonStrategyRegressions()
        {
            Run(nameof(EmpressAcceptsEveryNaturalAttackFamily),
                EmpressAcceptsEveryNaturalAttackFamily);
            Run(nameof(EmpressRejectsImpossibleStateClockAndTablePairs),
                EmpressRejectsImpossibleStateClockAndTablePairs);
            Run(nameof(EmpressHandlesClassicDayNightScheduleTransitions),
                EmpressHandlesClassicDayNightScheduleTransitions);
            Run(nameof(EmpressMidFightTakeoverKeepsOneExplicitLoop),
                EmpressMidFightTakeoverKeepsOneExplicitLoop);
            Run(nameof(EmpressSunDanceMovesTowardPivotLine),
                EmpressSunDanceMovesTowardPivotLine);
            Run(nameof(MoonLordAcceptsEveryHeadHandAndTrueEyeClockSegment),
                MoonLordAcceptsEveryHeadHandAndTrueEyeClockSegment);
            Run(nameof(MoonLordRejectsImpossibleSourceIdentityAndClocks),
                MoonLordRejectsImpossibleSourceIdentityAndClocks);
            Run(nameof(MoonLordConcurrentHazardsUseTheUrgentSafeContract),
                MoonLordConcurrentHazardsUseTheUrgentSafeContract);
            Run(nameof(MoonLordExpiredBoltCadenceDoesNotInventAProjectile),
                MoonLordExpiredBoltCadenceDoesNotInventAProjectile);
            Run(nameof(MoonLordMidFightTakeoverIsImmediateAndDeterministic),
                MoonLordMidFightTakeoverIsImmediateAndDeterministic);
        }

        private static void EmpressAcceptsEveryNaturalAttackFamily()
        {
            var cases = new[]
            {
                E("p1-intro", 0, 0, 0, 0),
                E("p1-reposition", 1, 12, 3, 0),
                E("p1-bolts", 2, 70, 1, 0),
                E("p1-lances", 4, 70, 8, 0),
                E("p1-rainbow", 5, 40, 5, 0),
                E("p1-sun", 6, 100, 3, 0),
                E("p1-left-dash", 8, 50, 2, 0),
                E("p1-right-dash", 9, 50, 2, 0),
                E("p1-transition-before-latch", 10, 89, 1, 0),
                E("p1-transition-boundary", 10, 90, 1, 0),
                E("p2-transition-after-latch", 10, 90, 1, 1),
                E("p2-reposition", 1, 10, 0, 1),
                E("p2-lance-wall", 7, 80, 1, 1),
                E("p2-bolts", 2, 60, 2, 1),
                E("p2-left-dash", 8, 55, 3, 1),
                E("p2-right-dash", 9, 55, 3, 1),
                E("p2-rainbow", 5, 35, 4, 1),
                E("p2-sun", 6, 90, 6, 1),
                E("p2-lances", 4, 60, 7, 1),
                E("p2-spiral", 12, 70, 9, 1),
                E("expert-predictive", 11, 60, 4, 1,
                    expert: true),
                E("master-predictive", 11, 60, 4, 1,
                    master: true),
                E("day-lethal-predictive", 11, 60, 4, 1,
                    day: true),
                E("ftw-short-reposition", 1, 9, 0, 1,
                    forTheWorthy: true)
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var scene = EmpressScene(cases[i]);
                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                False(directive.RequestControlReturn,
                    cases[i].Name + ": " + directive.ControlReturnReason);
                True(directive.UseExplicitMovement ||
                    cases[i].State != 8 && cases[i].State != 9 &&
                    !cases[i].Day,
                    cases[i].Name + " lost its strict movement contract");
            }

            var departure = EmpressScene(E("departure", 13, 0, 1, 0));
            var leaving = new BossStrategyEngine().Evaluate(departure).
                Directive;
            True(leaving.RequestControlReturn);
            True(leaving.PhaseId.Contains("native-departure"),
                leaving.PhaseId);
        }

        private static void EmpressRejectsImpossibleStateClockAndTablePairs()
        {
            AssertEmpressRejected(E("unused-state-3", 3, 0, 1, 0));
            AssertEmpressRejected(E("p1-cannot-use-wall", 7, 30, 1, 0));
            AssertEmpressRejected(E("p2-cannot-use-intro", 0, 0, 0, 1));
            AssertEmpressRejected(E("transition-latch-too-early", 10, 89,
                1, 1));
            AssertEmpressRejected(E("transition-latch-too-late", 10, 91,
                1, 0));
            AssertEmpressRejected(E("departure-clock-never-advances", 13,
                1, 1, 0));
            AssertEmpressRejected(E("active-state-needs-selected-index", 2,
                20, 0, 0));
            AssertEmpressRejected(E("wrong-p1-table-entry", 4, 20, 1, 0));
            AssertEmpressRejected(E("wrong-expert-table-entry", 5, 20, 4,
                1, expert: true));

            var fractional = EmpressScene(E("fractional", 2, 20, 1, 0));
            var target = fractional.Targets[0];
            target.Ai0 = 2.5f;
            fractional.Targets[0] = target;
            RefreshPriorityNativeContext(fractional);
            AssertControlReturn(fractional, "fractional Empress state");

            var stale = EmpressScene(E("stale-copy", 2, 20, 1, 0));
            var observation = stale.PriorityBoss.Empresses[0];
            observation.Ai2AttackIndex = 2f;
            stale.PriorityBoss.Empresses[0] = observation;
            AssertControlReturn(stale, "stale Empress ai2 copy");
        }

        private static void EmpressHandlesClassicDayNightScheduleTransitions()
        {
            // State 11 can have been selected by the lethal daytime table and
            // remain active for a few frames after night begins.
            var afterSunset = EmpressScene(E("day-to-night", 11, 50, 4,
                3));
            var night = new BossStrategyEngine().Evaluate(afterSunset).
                Directive;
            False(night.RequestControlReturn, night.ControlReturnReason);
            True(night.PhaseId.Contains("day-rage-p2-"), night.PhaseId);

            // Conversely, a classic-night attack remains valid if dawn makes
            // ShouldEmpressBeEnraged true before it returns to state 1.
            var afterDawn = EmpressScene(E("night-to-day", 5, 30, 4, 1,
                day: true));
            var day = new BossStrategyEngine().Evaluate(afterDawn).Directive;
            False(day.RequestControlReturn, day.ControlReturnReason);
            True(day.PhaseId.Contains("day-lethal-p2-"), day.PhaseId);

            // Reposition uses the current predicate because the next attack is
            // not selected until this native state completes.
            var select = EmpressScene(E("select-after-dawn", 1, 4, 3, 1,
                day: true));
            var next = new BossStrategyEngine().Evaluate(select).Directive;
            True(next.PhaseId.Contains("reposition-before-predictive-lances"),
                next.PhaseId);
        }

        private static void EmpressMidFightTakeoverKeepsOneExplicitLoop()
        {
            foreach (var day in new[] { false, true })
            foreach (var playerOnRight in new[] { false, true })
            {
                var scene = EmpressScene(E("takeover", 6, 121, 3, 0,
                    day: day));
                scene.Player.Position = new Vec2(playerOnRight ? 2600f :
                    420f, 760f);
                scene.Arena.ClearanceLeft = playerOnRight ? 2500f : 400f;
                scene.Arena.ClearanceRight = playerOnRight ? 400f : 2500f;
                var engine = new BossStrategyEngine();
                var first = engine.Evaluate(scene).Directive;
                False(first.RequestControlReturn, first.ControlReturnReason);
                if (day) True(first.UseExplicitMovement);
                for (var frame = 0; frame < 12; frame++)
                {
                    var current = engine.Evaluate(scene).Directive;
                    Equal(first.HorizontalIntent, current.HorizontalIntent);
                    Equal(first.VerticalIntent, current.VerticalIntent);
                    False(current.RequestControlReturn,
                        "Empress takeover oscillated or abandoned on frame " +
                        frame);
                }
            }
        }

        private static void EmpressSunDanceMovesTowardPivotLine()
        {
            // Sun Dance's slowest region is the rotating pivot near the
            // Empress body, so the tutorial response is vertical motion
            // toward that line: below the body means up (-1), above it means
            // down (+1). This assertion pins the sign convention rather than
            // accepting any non-zero input.
            var below = EmpressScene(E("sun-below", 6, 100, 3, 0));
            below.Player.Position = new Vec2(1500f, 900f);
            var belowPlan = new BossStrategyEngine().Evaluate(below).
                Directive;
            False(belowPlan.RequestControlReturn, belowPlan.ControlReturnReason);
            Equal(0, belowPlan.HorizontalIntent);
            Equal(1, belowPlan.VerticalIntent);

            var above = EmpressScene(E("sun-above", 6, 100, 3, 0));
            above.Player.Position = new Vec2(1500f, 300f);
            var abovePlan = new BossStrategyEngine().Evaluate(above).
                Directive;
            False(abovePlan.RequestControlReturn, abovePlan.ControlReturnReason);
            Equal(0, abovePlan.HorizontalIntent);
            Equal(-1, abovePlan.VerticalIntent);
        }

        private static void MoonLordAcceptsEveryHeadHandAndTrueEyeClockSegment()
        {
            var cases = new List<MoonSourceCase>();
            AddMoonBoundaries(cases, "head", 396, 0,
                new[] { 3, 0, 2, 3, 1 },
                new[] { 180, 30, 435, 180, 375 });
            AddMoonBoundaries(cases, "left", 397, 0,
                new[] { 0, 1, 2, 0, 3 },
                new[] { 50, 70, 330, 60, 90 });
            AddMoonBoundaries(cases, "right", 397, 1,
                new[] { 1, 0, 3, 0, 2 },
                new[] { 70, 50, 90, 60, 330 });
            AddMoonBoundaries(cases, "true-eye", 400, 0,
                new[] { 0, 1, 0, 2, 0, 3, 0, 4, 0, 2 },
                new[] { 53, 90, 53, 135, 53, 200, 53, 375, 53, 135 });

            for (var i = 0; i < cases.Count; i++)
            {
                var scene = MoonScene(cases[i]);
                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                False(directive.RequestControlReturn,
                    cases[i].Name + ": " + directive.ControlReturnReason);
                True(directive.UseExplicitMovement,
                    cases[i].Name + " did not enter an immediate controller");
            }

            AssertMoonAccepted(new MoonSourceCase
                { Name = "closed-head", Type = 396, State = -2,
                    Clock = 1199 });
            AssertMoonAccepted(new MoonSourceCase
                { Name = "dying-head", Type = 396, State = -3,
                    Clock = 1199 });
            AssertMoonAccepted(new MoonSourceCase
                { Name = "closed-hand-last-tick", Type = 397, State = -2,
                    Clock = 31, Side = 0 });
            AssertMoonAccepted(new MoonSourceCase
                { Name = "new-eye-offset", Type = 400, State = -2,
                    Clock = 196 });
        }

        private static void MoonLordRejectsImpossibleSourceIdentityAndClocks()
        {
            AssertMoonRejected(new MoonSourceCase
                { Name = "head-state-clock-mismatch", Type = 396,
                    State = 1, Clock = 300 });
            AssertMoonRejected(new MoonSourceCase
                { Name = "left-state-clock-mismatch", Type = 397,
                    State = 0, Clock = 100, Side = 0 });
            AssertMoonRejected(new MoonSourceCase
                { Name = "right-state-clock-mismatch", Type = 397,
                    State = 0, Clock = 400, Side = 1 });
            AssertMoonRejected(new MoonSourceCase
                { Name = "closed-hand-wraps-at-32", Type = 397,
                    State = -2, Clock = 32, Side = 0 });
            AssertMoonRejected(new MoonSourceCase
                { Name = "true-eye-cannot-stay-closed-in-idle", Type = 400,
                    State = -2, Clock = 0 });

            var wrongParent = MoonScene(new MoonSourceCase
                { Name = "wrong-parent", Type = 396, State = 3,
                    Clock = 0 });
            var source = wrongParent.Targets[1];
            source.Ai3 += 1f;
            wrongParent.Targets[1] = source;
            AssertControlReturn(wrongParent, "wrong Moon Lord parent key");

            var wrongTarget = MoonScene(new MoonSourceCase
                { Name = "wrong-target", Type = 400, State = 2,
                    Clock = 196 });
            source = wrongTarget.Targets[1];
            source.NativeTargetPlayerIndex = 1;
            wrongTarget.Targets[1] = source;
            AssertControlReturn(wrongTarget, "non-local Moon Lord target");

            var missingCore = MoonScene(new MoonSourceCase
                { Name = "missing-core", Type = 396, State = 3,
                    Clock = 0 });
            missingCore.Targets.RemoveAt(0);
            AssertControlReturn(missingCore, "missing Moon Lord core");

            var fractional = MoonScene(new MoonSourceCase
                { Name = "fractional", Type = 400, State = 2,
                    Clock = 196 });
            source = fractional.Targets[1];
            source.Ai1 = 196.5f;
            fractional.Targets[1] = source;
            AssertControlReturn(fractional, "fractional Moon Lord clock");
        }

        private static void MoonLordConcurrentHazardsUseTheUrgentSafeContract()
        {
            var scene = MoonScene(null);
            AddDetachedSphere(scene, 70);
            AddMoonRay(scene, new Vec2(1f, 0f), .02f, 20f);
            var active = new BossStrategyEngine().Evaluate(scene).Directive;
            True(active.PhaseId.Contains("active-deathray-"),
                active.PhaseId);
            True(active.PreferDash && active.UseExplicitMovement);

            // Two opposite rotations can cancel their aggregate vector. The
            // strongest-ray fallback must still produce a deterministic exit.
            scene = MoonScene(null);
            AddMoonRay(scene, new Vec2(1f, 0f), .02f, 20f);
            AddMoonRay(scene, new Vec2(1f, 0f), -.02f, 20f);
            var engine = new BossStrategyEngine();
            var opposed = engine.Evaluate(scene).Directive;
            True(opposed.HorizontalIntent != 0 || opposed.VerticalIntent != 0,
                "opposed Moon Lord rays cancelled every escape input");
            for (var i = 0; i < 8; i++)
            {
                var repeat = engine.Evaluate(scene).Directive;
                Equal(opposed.HorizontalIntent, repeat.HorizontalIntent);
                Equal(opposed.VerticalIntent, repeat.VerticalIntent);
            }

            // A source-clock ray inside 45 ticks outranks a live sphere; the
            // controller enters the swept-side lane before projectile 455 is
            // damaging rather than reacting only after spawn.
            scene = MoonScene(new MoonSourceCase
                { Name = "head-ray", Type = 396, State = 1,
                    Clock = 980 }); // local 155, 45 ticks until damage
            AddDetachedSphere(scene, 71);
            var predicted = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(predicted.PhaseId.Contains("deathray-telegraph-head-45"),
                predicted.PhaseId);
            True(predicted.HorizontalIntent != 0 ||
                predicted.VerticalIntent != 0);
        }

        private static void MoonLordExpiredBoltCadenceDoesNotInventAProjectile()
        {
            var head = MoonScene(new MoonSourceCase
                { Name = "head-expired", Type = 396, State = 3,
                    Clock = 179 });
            var headPlan = new BossStrategyEngine().Evaluate(head).Directive;
            False(headPlan.PhaseId.Contains("bolt-clock"), headPlan.PhaseId);

            var hand = MoonScene(new MoonSourceCase
                { Name = "hand-expired", Type = 397, State = 1,
                    Clock = 119, Side = 0 });
            var handPlan = new BossStrategyEngine().Evaluate(hand).Directive;
            False(handPlan.PhaseId.Contains("bolt-clock"), handPlan.PhaseId);

            var eye = MoonScene(new MoonSourceCase
                { Name = "eye-expired", Type = 400, State = 1,
                    Clock = 142 });
            var eyePlan = new BossStrategyEngine().Evaluate(eye).Directive;
            False(eyePlan.PhaseId.Contains("bolt-clock"), eyePlan.PhaseId);
        }

        private static void MoonLordMidFightTakeoverIsImmediateAndDeterministic()
        {
            var starts = new[]
            {
                new MoonSourceCase { Name = "hand-sphere-midstream",
                    Type = 397, State = 2, Clock = 280, Side = 0 },
                new MoonSourceCase { Name = "first-eye-sphere-window",
                    Type = 400, State = 2, Clock = 270 },
                new MoonSourceCase { Name = "eye-spin-midstream",
                    Type = 400, State = 3, Clock = 500 },
                new MoonSourceCase { Name = "eye-ray-midstream",
                    Type = 400, State = 4, Clock = 817 },
                new MoonSourceCase { Name = "second-eye-sphere-window",
                    Type = 400, State = 2, Clock = 1130 }
            };
            for (var c = 0; c < starts.Length; c++)
            {
                var scene = MoonScene(starts[c]);
                var engine = new BossStrategyEngine();
                var first = engine.Evaluate(scene).Directive;
                False(first.RequestControlReturn,
                    starts[c].Name + ": " + first.ControlReturnReason);
                True(first.UseExplicitMovement &&
                    first.ForceContinuousMovement,
                    starts[c].Name + " did not take over immediately");
                for (var frame = 0; frame < 12; frame++)
                {
                    var next = engine.Evaluate(scene).Directive;
                    Equal(first.HorizontalIntent, next.HorizontalIntent);
                    Equal(first.VerticalIntent, next.VerticalIntent);
                    False(next.RequestControlReturn);
                }
            }
        }

        private static EmpressNativeCase E(string name, int state, int tick,
            int index, int form, bool day = false, bool expert = false,
            bool master = false, bool forTheWorthy = false) =>
            new EmpressNativeCase
            {
                Name = name,
                State = state,
                Tick = tick,
                Index = index,
                Form = form,
                Day = day,
                Expert = expert,
                Master = master,
                ForTheWorthy = forTheWorthy
            };

        private static CombatSnapshot EmpressScene(EmpressNativeCase item)
        {
            var scene = CombatScenario(636);
            scene.Difficulty.DayTime = item.Day;
            scene.Difficulty.Expert = item.Expert;
            scene.Difficulty.Master = item.Master;
            scene.Difficulty.ForTheWorthy = item.ForTheWorthy;
            var target = scene.Targets[0];
            target.Ai0 = item.State;
            target.Ai1 = item.Tick;
            target.Ai2 = item.Index;
            target.Ai3 = item.Form;
            target.Velocity = item.State == 8 ? new Vec2(-20f, 0f) :
                item.State == 9 ? new Vec2(20f, 0f) :
                new Vec2(0f, 0f);
            scene.Targets[0] = target;
            RefreshPriorityNativeContext(scene);
            return scene;
        }

        private static void AssertEmpressRejected(EmpressNativeCase item)
        {
            AssertControlReturn(EmpressScene(item), item.Name);
        }

        private static void AddMoonBoundaries(List<MoonSourceCase> result,
            string name, int type, int side, int[] states, int[] durations)
        {
            var start = 0;
            for (var i = 0; i < states.Length; i++)
            {
                result.Add(new MoonSourceCase { Name = name + "-" + i +
                    "-first", Type = type, State = states[i], Clock = start,
                    Side = side });
                result.Add(new MoonSourceCase { Name = name + "-" + i +
                    "-last", Type = type, State = states[i],
                    Clock = start + durations[i] - 1, Side = side });
                start += durations[i];
            }
            Equal(type == 397 ? 600 : 1200, start);
        }

        private static CombatSnapshot MoonScene(MoonSourceCase sourceCase)
        {
            var scene = CombatScenario(398);
            var core = scene.Targets[0];
            core.Ai0 = 0f;
            core.Ai1 = 0f;
            core.Ai2 = 0f;
            core.Ai3 = 0f;
            core.Invulnerable = true;
            scene.Targets[0] = core;
            if (sourceCase != null)
            {
                scene.Targets.Add(new TargetSnapshot
                {
                    Key = 40,
                    Type = sourceCase.Type,
                    Position = new Vec2(1880f, 620f),
                    Velocity = new Vec2(0f, 0f),
                    Width = 80,
                    Height = 80,
                    Life = 9000,
                    LifeMax = 10000,
                    Damage = 60,
                    Boss = true,
                    Chaseable = true,
                    Ai0Known = true,
                    Ai1Known = true,
                    Ai2Known = true,
                    Ai3Known = true,
                    Ai0 = sourceCase.State,
                    Ai1 = sourceCase.Clock,
                    Ai2 = sourceCase.Side,
                    Ai3 = core.Key,
                    NativeTargetKnown = true,
                    NativeTargetPlayerIndex = scene.LocalPlayerIndex
                });
            }
            return scene;
        }

        private static void AssertMoonAccepted(MoonSourceCase item)
        {
            var directive = new BossStrategyEngine().Evaluate(MoonScene(item)).
                Directive;
            False(directive.RequestControlReturn,
                item.Name + ": " + directive.ControlReturnReason);
        }

        private static void AssertMoonRejected(MoonSourceCase item)
        {
            AssertControlReturn(MoonScene(item), item.Name);
        }

        private static void AssertControlReturn(CombatSnapshot scene,
            string name)
        {
            var directive = new BossStrategyEngine().Evaluate(scene).Directive;
            True(directive.RequestControlReturn,
                name + " did not fail closed: " + directive.PhaseId);
            False(directive.ForceContinuousMovement,
                name + " emitted forced movement while returning control");
        }

        private static void AddDetachedSphere(CombatSnapshot scene, int key)
        {
            scene.PriorityBoss.MoonLordProjectiles454.Add(
                new MoonLordProjectile454Observation
                {
                    Known = true,
                    ProjectileKey = key,
                    Ai0AgeOrMode = -1f,
                    SourceNpcAi1 = -1f,
                    LocalAi0 = 0f,
                    LocalAi1 = 0f,
                    TimeLeft = 120,
                    Alpha = 0,
                    ExtraUpdates = 1,
                    SourceNpcIdentityKnown = true
                });
        }

        private static void AddMoonRay(CombatSnapshot scene, Vec2 direction,
            float angularVelocity, float age)
        {
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.MoonLordDeathray,
                Type = 455,
                Position = new Vec2(1800f, 600f),
                BeamOrigin = new Vec2(1800f, 600f),
                BeamDirection = direction,
                BeamAngularVelocity = angularVelocity,
                BeamAge = age,
                BeamLength = 2400f,
                BeamScale = 1f,
                BeamScaleLimit = 1f,
                TimeLeft = 180
            });
        }
    }
}
