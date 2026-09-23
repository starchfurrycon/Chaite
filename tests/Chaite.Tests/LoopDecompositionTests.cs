using System;
using System.Collections.Generic;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static LoopObservation At(float playerX, float playerY,
            float bossX, int bossState, bool hit)
        {
            return new LoopObservation
            {
                PlayerPosition = new Vec2(playerX, playerY),
                BossPosition = new Vec2(bossX, 0f),
                BossState = bossState,
                Hit = hit,
            };
        }

        /// <summary>Appends one cycle: an attack of the given length then a
        /// recovery of the given length.</summary>
        private static void AppendCycle(List<LoopObservation> rows, int attackTicks,
            int recoveryTicks, bool hitInAttack, float playerX)
        {
            for (var tick = 0; tick < attackTicks; tick++)
                rows.Add(At(playerX, 0f, 900f, 0, hitInAttack && tick == 0));
            for (var tick = 0; tick < recoveryTicks; tick++)
                rows.Add(At(playerX, 0f, 900f, 1, false));
        }

        /// <summary>
        /// The invariant every decomposition has to satisfy: the segments tile
        /// the trace exactly, and the hits they attribute sum to the hits that
        /// happened. A segmentation that loses ticks still looks plausible in
        /// aggregate, and a search built on it would be optimising a fight that
        /// is not the one being played.
        /// </summary>
        private static void AssertTilesTheTrace(
            IReadOnlyList<LoopSegment> segments, int ticks, int hits)
        {
            var at = 0;
            var attributed = 0;
            foreach (var segment in segments)
            {
                Equal(at, segment.StartTick);
                True(segment.EndTick > segment.StartTick,
                    "a segment has no ticks: " + segment.StartTick);
                attributed += segment.Hits;
                at = segment.EndTick;
            }
            Equal(ticks, at);
            Equal(hits, attributed);
        }

        /// <summary>The boundary comes from the Boss's own repeating script and
        /// not from a hand-written list of attack and recovery state numbers.
        /// That matters because a hand-written list stops cutting the fight
        /// exactly where the Boss changes to a state set nobody enumerated.</summary>
        private static void LoopDecompositionFindsTheScriptPeriod()
        {
            var rows = new List<LoopObservation>();
            for (var cycle = 0; cycle < 4; cycle++)
                AppendCycle(rows, 20, 20, false, 400f);

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            Equal(4, segments.Count);
            Equal(0, segments[0].StartTick);
            Equal(40, segments[0].EndTick);
            Equal(40, segments[1].StartTick);
            Equal(80, segments[1].EndTick);
            AssertTilesTheTrace(segments, 160, 0);
        }

        /// <summary>
        /// The measured behaviour the segmentation exists to survive: the Boss
        /// keeps the shape of its cycle but re-rolls some attacks between
        /// cycles, so an exact match finds no period at all on a real trace.
        /// Both halves are asserted, because a tolerance that is never shown to
        /// be necessary is indistinguishable from a bug that accepts anything.
        /// </summary>
        private static void LoopDecompositionToleratesVariableAttacks()
        {
            var rows = new List<LoopObservation>();
            var attacks = new[] { 2, 2, 3, 3 };
            foreach (var attack in attacks)
            {
                for (var tick = 0; tick < 20; tick++) rows.Add(At(400f, 0f, 900f, 0, false));
                for (var tick = 0; tick < 20; tick++) rows.Add(At(400f, 0f, 900f, 1, false));
                for (var tick = 0; tick < 20; tick++) rows.Add(At(400f, 0f, 900f, 0, false));
                for (var tick = 0; tick < 20; tick++) rows.Add(At(400f, 0f, 900f, attack, false));
            }

            var tolerant = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            True(tolerant.Count > 1,
                "the cycle was not found even though only two of its eight runs " +
                "differ between cycles");

            var exact = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10, MatchFraction = 1.0 });
            Equal(1, exact.Count);
            False(exact[0].Complete,
                "a trace with no exact period was reported as a cycle");
        }

        /// <summary>A hit anywhere in the cycle has to land on that cycle, not
        /// on a neighbour: the whole method keys off which loop is dirty.</summary>
        private static void LoopDecompositionAttributesHitsToTheRightLoop()
        {
            var rows = new List<LoopObservation>();
            AppendCycle(rows, 20, 20, false, 400f);
            AppendCycle(rows, 20, 20, true, 400f);
            AppendCycle(rows, 20, 20, false, 400f);
            AppendCycle(rows, 20, 20, false, 400f);

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            Equal(4, segments.Count);
            Equal(0, segments[0].Hits);
            Equal(1, segments[1].Hits);
            Equal(0, segments[2].Hits);
            Equal(0, segments[3].Hits);
            AssertTilesTheTrace(segments, 160, 1);

            var report = LoopDecompositionSummary.Summarize(segments);
            Equal(4, report.Loops);
            Equal(3, report.CleanLoops);
            Equal(2, report.CompleteLoops);
            Equal(2, report.ClosedLoops);
            // Only the first cycle is both complete and hit-free; the cycle
            // carrying the hit is not perfect, and the last cannot be confirmed.
            Equal(1, report.PerfectLoops);
            Equal(1, report.Hits);
        }

        /// <summary>Closure is a property of the player, not of the boundary:
        /// the script says where the loop ends, and whether the player is back
        /// where it started is what the search has to achieve. Conflating the
        /// two would make the decomposition discard the very loops the search
        /// needs to fix.</summary>
        private static void LoopDecompositionSeparatesBoundaryFromClosure()
        {
            var rows = new List<LoopObservation>();
            // The player moves during each recovery and ends in a different cell
            // from the one it started in. It has to actually move: with a
            // stationary player every cycle trivially closes and the test would
            // pass while measuring nothing.
            for (var cycle = 0; cycle < 4; cycle++)
            {
                var from = 400f + 300f * cycle;
                for (var tick = 0; tick < 20; tick++)
                    rows.Add(At(from, 0f, 900f, 0, false));
                for (var tick = 0; tick < 20; tick++)
                    rows.Add(At(from + 15f * (tick + 1), 0f, 900f, 1, false));
            }

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            Equal(4, segments.Count);
            AssertTilesTheTrace(segments, 160, 0);
            // Not one of them closes, because the player never comes back. The
            // boundary detection must not have been affected by that.
            var closed = 0;
            foreach (var segment in segments) if (segment.Closed) closed++;
            Equal(0, closed);
        }

        /// <summary>Cycles facing the same threat must group together, because
        /// that grouping is what sets the enumeration cost. This is the property
        /// the whole decomposition exists for, so it is asserted directly.</summary>
        private static void LoopDecompositionGroupsIdenticalThreats()
        {
            var rows = new List<LoopObservation>();
            for (var cycle = 0; cycle < 5; cycle++)
                AppendCycle(rows, 20, 20, false, 400f);

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            AssertTilesTheTrace(segments, 200, 0);
            for (var index = 1; index < segments.Count; index++)
                Equal(segments[0].ThreatSignature,
                    segments[index].ThreatSignature);

            var report = LoopDecompositionSummary.Summarize(segments);
            Equal(1, report.DistinctGroups);
            True(report.ReductionFactor > 1.5,
                "identical cycles did not reduce the enumeration cost: " +
                report.ReductionFactor);
        }

        /// <summary>Two cycles that start in different cells are different
        /// problems even with the same script, because closing them means
        /// returning to different places.</summary>
        private static void LoopDecompositionSeparatesStartCells()
        {
            var rows = new List<LoopObservation>();
            AppendCycle(rows, 20, 20, false, 400f);
            AppendCycle(rows, 20, 20, false, 1600f);
            AppendCycle(rows, 20, 20, false, 400f);
            AppendCycle(rows, 20, 20, false, 1600f);

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            True(segments.Count >= 4,
                "too few cycles were found: " + segments.Count);
            True(segments[0].StartBucket != segments[1].StartBucket,
                "two different start cells produced the same bucket");
            True(segments[0].ThreatSignature != segments[1].ThreatSignature,
                "cycles from different cells were grouped together");
        }

        /// <summary>
        /// Two cycles can share the script and the coarse start cell and still be
        /// different problems, because the cell is two hundred pixels wide and
        /// the physics is translation invariant. Grouping them promises one route
        /// that cannot serve both, and on a real fight that mistake showed up as
        /// groups whose Boss-relative geometry differed by up to 410 px while the
        /// report called the saving 1.23.
        /// </summary>
        private static void LoopDecompositionSeparatesDifferentRelativeGeometry()
        {
            var rows = new List<LoopObservation>();
            // Both cycles put the player 400 px out with the Boss at 900 and at
            // 950: same distance band, same bearing sector, so the same bucket,
            // but 50 px apart relative to the Boss.
            AppendCycleAt(rows, 900f);
            AppendCycleAt(rows, 950f);

            var fine = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions
                {
                    MinimumTicks = 10,
                    RelativeCellSize = 32f,
                });
            True(fine.Count >= 2, "too few cycles: " + fine.Count);
            Equal(fine[0].StartBucket, fine[1].StartBucket);
            True(fine[0].ThreatSignature != fine[1].ThreatSignature,
                "cycles fifty pixels apart relative to the Boss were grouped " +
                "as one problem");

            // The option is what does it, so a cell wide enough to span the
            // difference has to reproduce the older, coarser grouping. Without
            // this half, the test would pass for a signature that had simply
            // stopped grouping at all.
            var coarse = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions
                {
                    MinimumTicks = 10,
                    RelativeCellSize = 200f,
                });
            True(coarse.Count >= 2, "too few cycles: " + coarse.Count);
            Equal(coarse[0].ThreatSignature, coarse[1].ThreatSignature);
        }

        private static void AppendCycleAt(List<LoopObservation> rows, float bossX)
        {
            for (var tick = 0; tick < 20; tick++)
                rows.Add(At(400f, 0f, bossX, 0, false));
            for (var tick = 0; tick < 20; tick++)
                rows.Add(At(400f, 0f, bossX, 1, false));
        }

        /// <summary>A trace that stops repeating still has to be tiled end to
        /// end. Dropping the remainder would hide the stretch the player died
        /// in, which is the only stretch worth looking at.</summary>
        private static void LoopDecompositionKeepsTheFinalStretch()
        {
            var rows = new List<LoopObservation>();
            for (var cycle = 0; cycle < 6; cycle++)
                AppendCycle(rows, 20, 20, false, 400f);
            // A tail whose state sequence never repeats.
            for (var tick = 0; tick < 12; tick++)
                rows.Add(At(400f, 0f, 900f, 20 + tick, tick == 11));

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            Equal(252, rows.Count);
            AssertTilesTheTrace(segments, 252, 1);
            Equal(252, segments[segments.Count - 1].EndTick);
            Equal(1, segments[segments.Count - 1].Hits);
        }

        /// <summary>A long constant run is one run after encoding, so it cannot
        /// be mistaken for a short period and cut into pieces. This is the check
        /// that a single long hover does not shatter the decomposition.</summary>
        private static void LoopDecompositionDoesNotCutConstantRuns()
        {
            var rows = new List<LoopObservation>();
            for (var tick = 0; tick < 90; tick++) rows.Add(At(400f, 0f, 900f, 0, false));
            for (var cycle = 0; cycle < 4; cycle++)
                AppendCycle(rows, 20, 20, false, 400f);
            AppendCycle(rows, 20, 20, false, 400f);

            var segments = LoopDecomposition.Segment(rows,
                new LoopSegmentationOptions { MinimumTicks = 10 });
            AssertTilesTheTrace(segments, rows.Count, 0);
            // A false period would have shattered the hover into segments of
            // whatever the minimum length is. Five cycles produce five
            // segments, so anything near that is fine and anything that trips
            // the minimum repeatedly is not.
            True(segments.Count <= 6,
                "the constant run was cut into false periods: " +
                segments.Count + " segments");
            foreach (var segment in segments)
                True(segment.Length >= 30,
                    "a segment shorter than a cycle was produced: " +
                    segment.Length);
        }
    }
}
