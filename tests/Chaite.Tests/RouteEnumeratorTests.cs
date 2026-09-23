using System;
using System.Collections.Generic;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>
        /// A world small enough to brute force, so the enumerator's claims can be
        /// checked against the answer instead of against itself.
        ///
        /// Position.X is the axis the route moves along and Position.Y counts
        /// ticks. Both are part of the dominance bucket, and that is the point:
        /// the goal is a region of (position, time), so a bucket that collapsed
        /// time would mark the goal as already reached one tick earlier and
        /// prune the goal itself. That mistake was made here first and caught by
        /// these tests.
        /// </summary>
        private sealed class OneDimensionalRouteWorld : IRouteWorld
        {
            public readonly HashSet<int> HazardTicks = new HashSet<int>();
            public readonly HashSet<int> HazardPositions = new HashSet<int>();
            public int RefuseAtTick = -1;
            public bool RefuseEveryStep;
            public int MinimumTicks = 2;
            public int Ticks;

            public bool TryStep(in PlayerMotionFrame state,
                in PlayerControlFrame controls, out PlayerMotionFrame next,
                out bool hit, out ForwardModelRefusal refusal)
            {
                next = state;
                hit = false;
                refusal = ForwardModelRefusal.None;
                Ticks++;
                if (RefuseEveryStep || Ticks == RefuseAtTick)
                {
                    refusal = ForwardModelRefusal.Dashing;
                    return false;
                }
                var step = controls.Direction * (controls.Dash ? 2 : 1);
                next.Position = new Vec2(state.Position.X + step,
                    state.Position.Y + 1f);
                hit = HazardTicks.Contains(Ticks) ||
                    HazardPositions.Contains((int)next.Position.X);
                return true;
            }

            public bool IsGoal(in PlayerMotionFrame state)
                => Math.Abs(state.Position.X) < .5f &&
                    state.Position.Y >= MinimumTicks;

            public int Bucket(in PlayerMotionFrame state)
                => (int)state.Position.Y * 1000 + (int)Math.Round(state.Position.X);

            public float DistanceToGoal(in PlayerMotionFrame state)
                => Math.Abs(state.Position.X) +
                    Math.Max(0f, MinimumTicks - state.Position.Y);
        }

        /// <summary>Every step is a hit, so no clean route can exist at all.
        /// Used to check that a non-clean result is reported as such rather than
        /// dressed up as a solution.</summary>
        private sealed class AllHazardWorld : IRouteWorld
        {
            public bool TryStep(in PlayerMotionFrame state,
                in PlayerControlFrame controls, out PlayerMotionFrame next,
                out bool hit, out ForwardModelRefusal refusal)
            {
                next = state;
                refusal = ForwardModelRefusal.None;
                next.Position = new Vec2(0f, state.Position.Y + 1f);
                hit = true;
                return true;
            }
            public bool IsGoal(in PlayerMotionFrame state)
                => state.Position.Y >= 3f;
            public int Bucket(in PlayerMotionFrame state)
                => (int)state.Position.Y;
            public float DistanceToGoal(in PlayerMotionFrame state)
                => Math.Max(0f, 3f - state.Position.Y);
        }

        private static PlayerMotionFrame RouteStart()
        {
            return new PlayerMotionFrame
            {
                Position = new Vec2(0f, 0f),
                Velocity = new Vec2(0f, 0f),
                Width = 20,
                Height = 40,
                Gravity = .4f,
                MaxFallSpeed = 10f,
                GravityDirection = 1,
                FloorY = 40f,
            };
        }

        /// <summary>The enumerator must not be trusted until it has been shown to
        /// agree with an unbounded search on a space small enough to exhaust,
        /// and until the route it reports has been replayed and shown to be
        /// walkable, clean and actually closed.</summary>
        private static void RouteEnumeratorAgreesWithBruteForce()
        {
            var world = new OneDimensionalRouteWorld { HazardPositions = { 3 } };
            var start = RouteStart();

            var exhaustive = RouteEnumerator.Search(world, in start,
                new RouteSearchRequest
                {
                    StepBudget = 8,
                    BeamWidth = 100000,
                    MaxExpansions = 1000000,
                });
            True(exhaustive.Found, "no route in a world that has one");
            True(exhaustive.Clean, "the reference route is not clean");
            Equal(0, exhaustive.Hits);
            True(exhaustive.PrunedByBeam == 0,
                "the unbounded reference search dropped frontier entries");
            True(exhaustive.Ticks >= 2 && exhaustive.Ticks <= 8,
                "the reference route is outside the step budget: " +
                exhaustive.Ticks);

            // Replay it, so the report is checked against the world rather than
            // against the search's own bookkeeping.
            var replayWorld = new OneDimensionalRouteWorld
            {
                HazardPositions = { 3 },
                MinimumTicks = 2,
            };
            var state = start;
            var replayedHits = 0;
            foreach (var action in exhaustive.Route)
            {
                PlayerMotionFrame next;
                bool hit;
                ForwardModelRefusal refusal;
                True(replayWorld.TryStep(in state, action.ToControls(), out next,
                    out hit, out refusal), "the reported route is not walkable");
                if (hit) replayedHits++;
                state = next;
            }
            Equal(0, replayedHits);
            True(replayWorld.IsGoal(in state),
                "the reported route does not close the loop");

            // Bounded the way a real search runs: it must still find a clean
            // route, because the bounds are meant to cost time, not answers, on
            // a space this small.
            var bounded = RouteEnumerator.Search(world, in start,
                new RouteSearchRequest { StepBudget = 8, BeamWidth = 16 });
            True(bounded.Found, "the bounded search lost a route the reference found");
            True(bounded.Clean, "the bounded search lost the clean route");
            Equal(0, bounded.Hits);
        }

        /// <summary>Two properties the acceptance criterion depends on: a goal
        /// that carries a hit is never reported as clean, and because the open
        /// set is ordered by hit count first, a non-clean goal is evidence that
        /// no clean route exists within the bounds rather than that the search
        /// stopped early.</summary>
        private static void RouteEnumeratorNeverCallsADirtyRouteClean()
        {
            var clean = new OneDimensionalRouteWorld();
            var start = RouteStart();
            var reachable = RouteEnumerator.Search(clean, in start,
                new RouteSearchRequest { StepBudget = 6, BeamWidth = 100000 });
            True(reachable.Found, "a clean route was not found in an empty world");
            True(reachable.Clean, "an empty world produced a dirty route");

            var dirty = new AllHazardWorld();
            var report = RouteEnumerator.Search(dirty, in start,
                new RouteSearchRequest { StepBudget = 6, BeamWidth = 100000 });
            True(report.Found, "the only route to the goal was not found at all");
            False(report.Clean,
                "a route that takes a hit was reported as clean");
            True(report.Hits > 0,
                "a non-clean report has no hits to explain it");
        }

        /// <summary>Every bound that gives up completeness has to show up in the
        /// report. A search that silently truncates reads as "no route exists",
        /// which is the failure this counter exists to prevent.</summary>
        private static void RouteEnumeratorReportsEveryBound()
        {
            // The goal is placed far enough away that the cap has to fire, so
            // this measures the cap rather than the world's size.
            var world = new OneDimensionalRouteWorld { MinimumTicks = 40 };
            var start = RouteStart();
            var report = RouteEnumerator.Search(world, in start,
                new RouteSearchRequest { StepBudget = 40, MaxExpansions = 25 });
            Equal(1, report.Truncated);
            True(report.Expanded <= 26,
                "the search expanded past its own cap: " + report.Expanded);

            // Refusals are reported by reason, so an unmodeled regime is visible
            // as a dead end rather than as an empty space.
            var refusing = new OneDimensionalRouteWorld { RefuseEveryStep = true };
            var refused = RouteEnumerator.Search(refusing, in start,
                new RouteSearchRequest { StepBudget = 4, MaxExpansions = 200 });
            False(refused.Found, "a world that refuses every step found a route");
            True(refused.RefusalsByReason.ContainsKey("Dashing"),
                "a refusal reason was not recorded");

            // The beam is the other bound that costs completeness, and it is
            // counted too.
            var beamed = RouteEnumerator.Search(world, in start,
                new RouteSearchRequest { StepBudget = 40, BeamWidth = 1,
                    MaxExpansions = 400 });
            True(beamed.PrunedByBeam > 0,
                "a beam of one pruned nothing, so the counter is not wired");
        }

        /// <summary>The alphabet is the branching factor the whole method is
        /// built to beat, so it is pinned: three directions, jump, dash.</summary>
        private static void RouteEnumeratorAlphabetIsTwelve()
        {
            Equal(12, RouteEnumerator.Alphabet.Length);
            var seen = new HashSet<string>();
            foreach (var action in RouteEnumerator.Alphabet)
                True(seen.Add(action.Direction + "/" + action.Jump + "/" + action.Dash),
                    "the alphabet repeats an action");
            True(RouteEnumerator.Alphabet[0].Direction == -1,
                "the alphabet order is not the documented one");
        }
    }
}
