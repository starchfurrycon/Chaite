using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>One tick of the fight as the loop decomposition sees it: where
    /// the player and the Boss were, what the Boss was doing, and whether the
    /// player lost life. Deliberately smaller than a full frame so the
    /// decomposition can be tested on hand-built sequences.</summary>
    public struct LoopObservation
    {
        public Vec2 PlayerPosition;
        public Vec2 BossPosition;
        /// <summary>The Boss's native state. State 1 is the repositioning that
        /// follows an attack; every other state is an attack.</summary>
        public int BossState;
        public bool Hit;
    }

    /// <summary>One loop: an attack and the repositioning that follows it.</summary>
    public struct LoopSegment
    {
        public int StartTick;
        /// <summary>Exclusive.</summary>
        public int EndTick;
        /// <summary>The (band, sector) cell the loop started in. Closing the
        /// loop means returning to this cell.</summary>
        public int StartBucket;
        /// <summary>The player was back in the start bucket when the loop
        /// ended, having actually run its cycle. Geometric closure on its own is
        /// not enough: a loop that never completed has no cycle to return
        /// from.</summary>
        public bool Closed;
        /// <summary>The loop ended because its repositioning ran its course,
        /// rather than because the next attack interrupted it. A loop has to be
        /// both complete and closed to be the unit the method treats as a
        /// cycle, and keeping the two apart is not pedantry: this project has
        /// twice reported a closure rate that mixed them.</summary>
        public bool Complete;
        public int Hits;
        /// <summary>Loops sharing this value face the same threat pattern and
        /// can therefore reuse the same route. This is the whole reason the
        /// enumeration is affordable.</summary>
        public int ThreatSignature;
        /// <summary>Where the player actually was when the loop started, and at
        /// its last tick. A route found for one loop of a group has to end where
        /// the *next* loop starts, and two loops in the same bucket are not the
        /// same point; the bucket is coarse on purpose and cannot answer this.</summary>
        public Vec2 StartPosition;
        public Vec2 EndPosition;
        public int Length { get { return EndTick - StartTick; } }
    }

    public sealed class LoopSegmentationOptions
    {
        /// <summary>Width of a distance band, in pixels. This is the resolution
        /// of the loop state space, not the closure tolerance.</summary>
        public float BandWidth = 200f;
        /// <summary>Number of bearing sectors the circle is cut into.</summary>
        public int BearingSectors = 4;
        /// <summary>A loop shorter than this is a flicker, not a loop.</summary>
        public int MinimumTicks = 30;
        /// <summary>What fraction of the compared runs must carry the same Boss
        /// state for a period to be accepted. It is below one on purpose: the
        /// Boss alternates a fixed inter-attack state with a cycle of attacks,
        /// and some of those slots take one of two different attacks from cycle
        /// to cycle. An exact match therefore finds no period at all on a real
        /// trace, while the cycle boundary is plainly there. The floor is set
        /// well above a half because every other run is the inter-attack state
        /// and matches for free.</summary>
        public double MatchFraction = 0.75;
        /// <summary>Side of a cell of the player's offset from the Boss, in
        /// pixels, used to decide whether two loops face the same threat. The
        /// distance band is far too coarse for this: it is 200 px wide, so it
        /// groups loops whose relative geometry differs by most of a band. A
        /// cell of zero or less disables the term and restores the old, coarser
        /// grouping.</summary>
        public float RelativeCellSize = 32f;
    }

    /// <summary>
    /// Cuts a fight into loops, groups the loops that face the same threat, and
    /// reports what a per-group route would have to achieve.
    ///
    /// This is the step that makes enumeration affordable. A fight is thousands
    /// of ticks and the action alphabet is twelve, so enumerating the fight is
    /// twelve to the power of thousands. A loop is tens of ticks, and because
    /// the Boss's attack sequence is a fixed repeating script, many loops are
    /// the same loop: one route per distinct threat pattern covers all of them.
    /// The enumeration cost is therefore set by the number of distinct groups,
    /// which is what <see cref="LoopDecompositionReport.DistinctGroups"/>
    /// reports, and not by the length of the fight.
    /// </summary>
    public static class LoopDecomposition
    {
        public static List<LoopSegment> Segment(
            IReadOnlyList<LoopObservation> observations,
            LoopSegmentationOptions options)
        {
            if (observations == null) throw new ArgumentNullException("observations");
            if (options == null) throw new ArgumentNullException("options");
            var segments = new List<LoopSegment>();
            if (observations.Count == 0) return segments;

            var minimum = Math.Max(1, options.MinimumTicks);

            // The script is matched on the sequence of Boss states, not on the
            // tick at which each state recurs. Measured behaviour is that the
            // state segments are the same every cycle while their durations
            // drift by a handful of ticks, so an exact tick period matches
            // nothing at all on a real trace. Run-length encoding first
            // also removes the constant-run problem outright: a hover is one
            // run, so it cannot be mistaken for a short period.
            var runState = new List<int>();
            var runStart = new List<int>();
            for (var tick = 0; tick < observations.Count; tick++)
            {
                if (runState.Count == 0 ||
                    observations[tick].BossState != runState[runState.Count - 1])
                {
                    runState.Add(observations[tick].BossState);
                    runStart.Add(tick);
                }
            }
            if (runState.Count < 2) return segments;
            runStart.Add(observations.Count);

            var period = FindRunPeriod(runState, runStart, minimum,
                options.MatchFraction);
            if (period <= 0)
            {
                // The script does not repeat, so the fight is one segment. It is
                // reported as incomplete rather than silently dropped: a fight
                // whose script does not repeat is exactly the one worth knowing
                // about, and it is the case the method cannot help with.
                segments.Add(Finish(observations, options, 0,
                    observations.Count, Bucket(observations[0], options),
                    CountHits(observations, 0, observations.Count), false));
                return segments;
            }

            for (var run = 0; run + period < runState.Count; run += period)
            {
                var start = runStart[run];
                var end = runStart[run + period];
                var more = run + 2 * period < runState.Count;
                segments.Add(Finish(observations, options, start, end,
                    Bucket(observations[start], options),
                    CountHits(observations, start, end), more));
            }
            var tailStart = runStart[runStart.Count - 2];
            // Recompute the last emitted boundary so the remainder is not lost.
            if (segments.Count > 0)
                tailStart = segments[segments.Count - 1].EndTick;
            if (tailStart < observations.Count)
                segments.Add(Finish(observations, options, tailStart,
                    observations.Count, Bucket(observations[tailStart], options),
                    CountHits(observations, tailStart, observations.Count),
                    false));

            AssignSignatures(segments, observations, options);
            return segments;
        }

        /// <summary>
        /// The smallest number of state runs after which the run's state values
        /// begin repeating, or zero if they never do.
        ///
        /// The comparison spans two cycles so that a pattern which happens to
        /// match once is not accepted, and the compared window must contain more
        /// than one distinct state, so that a plain alternation is not mistaken
        /// for a script. A period is accepted when enough of the compared runs
        /// agree, not when all of them do, because the Boss re-rolls some of its
        /// attacks between cycles while keeping the cycle's shape; see
        /// <see cref="LoopSegmentationOptions.MatchFraction"/>. Acceptance also
        /// requires one cycle to be at least the minimum length in ticks, which
        /// keeps a two-state flicker from being reported as the Boss's script.
        /// </summary>
        private static int FindRunPeriod(List<int> runState, List<int> runStart,
            int minimum, double matchFraction)
        {
            var runs = runState.Count;
            for (var period = 1; period * 2 <= runs; period++)
            {
                if (runStart[period] - runStart[0] < minimum) continue;
                // Two cycles of evidence, not three. Comparing a third cycle
                // reaches further into the trace, and a stretch that stops
                // repeating near the end then votes against a period that the
                // first two cycles establish plainly.
                var span = Math.Min(2 * period, runs - period);
                var agreed = 0;
                var distinct = false;
                for (var offset = 0; offset < span; offset++)
                {
                    if (runState[offset] == runState[offset + period]) agreed++;
                    if (runState[offset] != runState[0]) distinct = true;
                }
                if (distinct && span > 0 &&
                    (double)agreed / span >= matchFraction)
                    return period;
            }
            return 0;
        }

        private static int CountHits(IReadOnlyList<LoopObservation> observations,
            int start, int end)
        {
            var hits = 0;
            for (var tick = start; tick < end; tick++)
                if (observations[tick].Hit) hits++;
            return hits;
        }

        private static LoopSegment Finish(
            IReadOnlyList<LoopObservation> observations,
            LoopSegmentationOptions options, int start, int end, int startBucket,
            int hits, bool complete)
        {
            var segment = new LoopSegment
            {
                StartTick = start,
                EndTick = end,
                StartBucket = startBucket,
                Hits = hits,
                Complete = complete,
                StartPosition = observations[start].PlayerPosition,
                // The exclusive end tick, not end-1. A segment covers ticks
                // [start, end), and the state it leaves behind is the one sampled
                // at the start of tick end, which is also exactly where the next
                // segment begins. Using end-1 puts the two a single tick apart,
                // which showed up as a 12.7 px boundary gap on a real fight and
                // would make every boundary look disconnected.
                EndPosition = observations[Math.Min(end, observations.Count - 1)]
                    .PlayerPosition,
            };
            segment.Closed = complete &&
                end - start >= options.MinimumTicks &&
                end > start &&
                Bucket(observations[end - 1], options) ==
                    startBucket;
            return segment;
        }

        /// <summary>Groups loops by the threat they face: the Boss state
        /// sequence over the loop plus the cell the loop started in, plus a
        /// finer cell of the player's offset from the Boss.
        ///
        /// The coarse bucket alone is not enough to make two loops the same
        /// problem. Its band is 200 px wide, so two loops whose relative
        /// geometry differs by nearly a band land in the same cell and were
        /// reported as one group, which promises a single route that cannot
        /// actually serve both. Measured on a real fight, groups held loops
        /// whose Boss-relative offsets differed by up to 410 px. The relative
        /// cell is what makes the grouping mean what it claims.</summary>
        private static void AssignSignatures(List<LoopSegment> segments,
            IReadOnlyList<LoopObservation> observations,
            LoopSegmentationOptions options)
        {
            var signatures = new Dictionary<string, int>();
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var key = segment.StartBucket + "|" +
                    RelativeCell(observations[segment.StartTick], options) + "|";
                for (var tick = segment.StartTick; tick < segment.EndTick; tick++)
                    key += observations[tick].BossState + ",";
                int assigned;
                if (!signatures.TryGetValue(key, out assigned))
                {
                    assigned = signatures.Count;
                    signatures[key] = assigned;
                }
                segment.ThreatSignature = assigned;
                segments[index] = segment;
            }
        }

        /// <summary>The player's offset from the Boss, quantised into a cell of
        /// <see cref="LoopSegmentationOptions.RelativeCellSize"/> pixels. The
        /// physics is translation invariant on the arena's flat plain, so two
        /// loops that differ only by a translation really are the same problem;
        /// the offset is what has to match, not the absolute position.</summary>
        public static int RelativeCell(in LoopObservation observation,
            LoopSegmentationOptions options)
        {
            // A non-positive size disables the term, which is how the previous
            // grouping is reproduced for comparison rather than argued about.
            if (options.RelativeCellSize <= 0f) return 0;
            var size = options.RelativeCellSize;
            var dx = observation.PlayerPosition.X - observation.BossPosition.X;
            var dy = observation.PlayerPosition.Y - observation.BossPosition.Y;
            var cellX = (int)Math.Floor(dx / size);
            var cellY = (int)Math.Floor(dy / size);
            // Negative cells must not collide with positive ones, so the pair is
            // folded into a single key through a bias wide enough for the arena.
            const int bias = 4096;
            return (cellX + bias) * 8192 + (cellY + bias);
        }

        /// <summary>The loop state cell: distance band crossed with bearing
        /// sector, both measured from the Boss.
        ///
        /// The Boss is taken from the same tick as the player. Using one fixed
        /// reference instead looks equivalent and is not: the Boss moves, so a
        /// player who has genuinely returned to the same place relative to the
        /// Boss would compare unequal against a reference taken minutes
        /// earlier. That mistake produced a closure rate of 4 out of 25 on a
        /// real trace where the player was in fact returning.</summary>
        public static int Bucket(in LoopObservation observation,
            LoopSegmentationOptions options)
        {
            var dx = observation.PlayerPosition.X - observation.BossPosition.X;
            var dy = observation.PlayerPosition.Y - observation.BossPosition.Y;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);
            var band = (int)Math.Floor(distance / Math.Max(1f, options.BandWidth));
            var sectors = Math.Max(1, options.BearingSectors);
            // atan2 in [-pi, pi] mapped to a stable sector index. The sector is
            // what makes the cell a position rather than only a range, which is
            // what the closure test needs.
            var angle = (float)Math.Atan2(dy, dx);
            var sector = (int)Math.Floor((angle + Math.PI) / (2 * Math.PI) * sectors);
            if (sector < 0) sector = 0;
            if (sector >= sectors) sector = sectors - 1;
            return band * sectors + sector;
        }
    }

    public sealed class LoopDecompositionReport
    {
        public int Loops;
        public int ClosedLoops;
        public int CompleteLoops;
        /// <summary>Loops that are complete, closed and hit-free: the unit the
        /// method calls perfect.</summary>
        public int PerfectLoops;
        public int CleanLoops;
        public int DistinctGroups;
        public int Hits;
        /// <summary>The sum of the lengths of one representative loop per
        /// group. This is what a route-reusing enumeration pays, and it is worth
        /// keeping apart from <see cref="SearchHorizonRatio"/>: on both real
        /// traces measured so far every loop landed in its own group, so this
        /// saving was nil while the decomposition was still worth a great deal.
        /// Reporting the two as one number is how a method ends up claiming a
        /// reduction it does not have.</summary>
        public int EnumerationTicks;
        public int FightTicks;
        /// <summary>How much shorter the route-reusing enumeration is than the
        /// fight, by representative ticks. Nil when no two loops share a
        /// group.</summary>
        public double ReductionFactor;
        /// <summary>The longest single loop. This is the horizon one search
        /// actually faces, and it is the decomposition's real contribution:
        /// searching the whole fight means a horizon of
        /// <see cref="FightTicks"/>, searching loop by loop means a horizon of
        /// this, repeated <see cref="Loops"/> times independently.</summary>
        public int LongestLoop;
        /// <summary>The fight's length divided by the longest loop: how much
        /// shorter each independent search is than the fight. This is the number
        /// that says whether the decomposition made enumeration affordable, and
        /// unlike <see cref="ReductionFactor"/> it does not depend on any two
        /// loops being alike.</summary>
        public double SearchHorizonRatio;
        /// <summary>Per group: loops in it, and how many are clean.</summary>
        public readonly List<int> GroupLoops = new List<int>();
        public readonly List<int> GroupCleanLoops = new List<int>();
        public readonly List<int> GroupRepresentativeTicks = new List<int>();
    }

    public static class LoopDecompositionSummary
    {
        /// <summary>Summarises a decomposition, including the cost reduction it
        /// buys. The reduction is the honest measure of whether enumeration is
        /// affordable, so it is computed here rather than asserted in prose.
        /// </summary>
        public static LoopDecompositionReport Summarize(
            IReadOnlyList<LoopSegment> segments)
        {
            var report = new LoopDecompositionReport();
            if (segments == null || segments.Count == 0) return report;
            report.Loops = segments.Count;

            var groupLoops = new Dictionary<int, int>();
            var groupClean = new Dictionary<int, int>();
            var groupLength = new Dictionary<int, int>();
            var groupSeen = new HashSet<int>();
            var lastEnd = 0;

            foreach (var segment in segments)
            {
                if (segment.Closed) report.ClosedLoops++;
                if (segment.Complete) report.CompleteLoops++;
                if (segment.Hits == 0) report.CleanLoops++;
                if (segment.Complete && segment.Closed && segment.Hits == 0)
                    report.PerfectLoops++;
                report.Hits += segment.Hits;
                if (segment.EndTick > lastEnd) lastEnd = segment.EndTick;
                if (segment.Length > report.LongestLoop)
                    report.LongestLoop = segment.Length;

                int count;
                groupLoops.TryGetValue(segment.ThreatSignature, out count);
                groupLoops[segment.ThreatSignature] = count + 1;
                if (segment.Hits == 0)
                {
                    groupClean.TryGetValue(segment.ThreatSignature, out count);
                    groupClean[segment.ThreatSignature] = count + 1;
                }
                if (groupSeen.Add(segment.ThreatSignature))
                    groupLength[segment.ThreatSignature] = segment.Length;
            }

            report.DistinctGroups = groupLoops.Count;
            report.FightTicks = lastEnd;
            foreach (var group in groupLoops.Keys)
            {
                report.GroupLoops.Add(groupLoops[group]);
                int clean;
                groupClean.TryGetValue(group, out clean);
                report.GroupCleanLoops.Add(clean);
                var length = groupLength[group];
                report.GroupRepresentativeTicks.Add(length);
                report.EnumerationTicks += length;
            }
            report.ReductionFactor = report.EnumerationTicks > 0
                ? (double)report.FightTicks / report.EnumerationTicks : 0.0;
            report.SearchHorizonRatio = report.LongestLoop > 0
                ? (double)report.FightTicks / report.LongestLoop : 0.0;
            return report;
        }
    }
}
