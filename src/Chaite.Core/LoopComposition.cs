using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>What the enumeration found for one loop.
    ///
    /// A route is only usable if it is clean and if it actually ends where the
    /// next loop begins. Those are separate facts and the report keeps them
    /// apart, because a route that is clean but lands 40 px away does not
    /// compose into a fight that stays clean, and reporting it as success is
    /// exactly the mistake this project keeps making.</summary>
    public struct LoopRoute
    {
        /// <summary>Whether the enumeration produced any route at all for this
        /// loop's group.</summary>
        public bool Found;
        /// <summary>Hits the route takes inside the loop. A clean loop route has
        /// zero, and a non-zero one is evidence that no clean route exists
        /// rather than that the search was stopped too early.</summary>
        public int Hits;
        /// <summary>Enumeration ticks the route cost.</summary>
        public int Ticks;
        /// <summary>Where the route leaves the player, in world pixels.</summary>
        public Vec2 EndPosition;
    }

    public sealed class LoopCompositionRequest
    {
        /// <summary>How far a route's end may be from the next loop's start and
        /// still count as connected. The forward model is accurate to well under
        /// a pixel on the modelled subset, so a loose tolerance here would hide a
        /// real discontinuity rather than absorb numerical noise.</summary>
        public float PositionTolerance = 4f;
        /// <summary>Reject routes that take a hit. Leaving this false composes a
        /// fight that is not no-hit, which is never what is wanted; it exists so
        /// a diagnostic run can measure how far short the enumeration falls.</summary>
        public bool RequireClean = true;
    }

    /// <summary>Why a composition failed, named precisely enough to act on.</summary>
    public struct LoopCompositionBreak
    {
        public int LoopIndex;
        /// <summary>Index of the loop the route failed to reach, or -1 when the
        /// break is about this loop alone.</summary>
        public int NextLoopIndex;
        public LoopCompositionBreakReason Reason;
        /// <summary>Distance from the route's end to the next loop's start, for
        /// the position reason.</summary>
        public float PositionError;
        public int Hits;
    }

    public enum LoopCompositionBreakReason
    {
        None = 0,
        /// <summary>The enumeration found nothing for this loop's group.</summary>
        MissingRoute,
        /// <summary>The route takes a hit inside the loop.</summary>
        DirtyRoute,
        /// <summary>The route ends too far from where the next loop starts, so
        /// the loops do not join and the fight would be stitched from states that
        /// never occur next to each other.</summary>
        Disconnected,
        /// <summary>The loop runs past the end of the fight.</summary>
        PastEnd,
    }

    public sealed class LoopCompositionReport
    {
        public int Loops;
        public int Groups;
        public int RoutesFound;
        public int RoutesMissing;
        public int RoutesDirty;
        /// <summary>Adjacent pairs whose boundary actually joins.</summary>
        public int Connected;
        public bool Complete;
        /// <summary>Enumeration cost of the composition: the sum of the routes'
        /// ticks, one route per distinct group rather than one per loop.</summary>
        public int EnumerationTicks;
        /// <summary>Ticks the fight actually lasts.</summary>
        public int FightTicks;
        public int TotalHits;
        public float ReductionFactor
        {
            get
            {
                return EnumerationTicks > 0
                    ? (float)FightTicks / EnumerationTicks
                    : 0f;
            }
        }
        public List<LoopCompositionBreak> Breaks =
            new List<LoopCompositionBreak>();
    }

    /// <summary>
    /// Chains the per-loop routes into one fight.
    ///
    /// The decomposition says which loops face the same threat, so one route per
    /// distinct group is enough; this is what makes the enumeration affordable.
    /// What this file adds is the check that the chain is real. Two loops in the
    /// same group start in the same bucket, and a bucket is deliberately coarse,
    /// so a route that ends in the right bucket can still end tens of pixels from
    /// where the next loop actually begins. Composing those produces a fight that
    /// never happens, and it would look clean the whole way.
    ///
    /// A break is recorded with its loop index, the boundary it failed, and the
    /// distance, so the failure names the loop to fix instead of only reporting
    /// that the fight does not compose.
    /// </summary>
    public static class LoopComposition
    {
        public static LoopCompositionReport Compose(
            IReadOnlyList<LoopSegment> segments,
            IReadOnlyList<LoopRoute> routes,
            in LoopCompositionRequest request)
        {
            var report = new LoopCompositionReport();
            if (segments == null || segments.Count == 0) return report;

            report.Loops = segments.Count;
            report.Groups = CountGroups(segments);
            var routeTicks = new Dictionary<int, int>();

            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                report.FightTicks += segment.Length;

                var route = routes != null && index < routes.Count
                    ? routes[index]
                    : default(LoopRoute);
                if (!route.Found)
                {
                    report.RoutesMissing++;
                    report.Breaks.Add(new LoopCompositionBreak
                    {
                        LoopIndex = index,
                        NextLoopIndex = -1,
                        Reason = LoopCompositionBreakReason.MissingRoute,
                    });
                    continue;
                }
                report.RoutesFound++;
                if (request.RequireClean && route.Hits > 0)
                {
                    report.RoutesDirty++;
                    report.TotalHits += route.Hits;
                    report.Breaks.Add(new LoopCompositionBreak
                    {
                        LoopIndex = index,
                        NextLoopIndex = -1,
                        Reason = LoopCompositionBreakReason.DirtyRoute,
                        Hits = route.Hits,
                    });
                }
                // One route per group is what the enumeration pays for, so the
                // same group's cost is counted once however many loops use it.
                if (!routeTicks.ContainsKey(segment.ThreatSignature))
                    routeTicks[segment.ThreatSignature] = route.Ticks;

                if (index + 1 >= segments.Count)
                {
                    // The last loop has no successor to join to. Its own end is
                    // the end of the fight, so there is no boundary to check.
                    continue;
                }

                var next = segments[index + 1];
                if (route.EndPosition.X == 0f && route.EndPosition.Y == 0f &&
                    (next.StartPosition.X != 0f || next.StartPosition.Y != 0f))
                {
                    // A route that reports no end position cannot be joined; the
                    // caller has not filled the field in.
                    report.Breaks.Add(new LoopCompositionBreak
                    {
                        LoopIndex = index,
                        NextLoopIndex = index + 1,
                        Reason = LoopCompositionBreakReason.Disconnected,
                        PositionError = float.PositiveInfinity,
                    });
                    continue;
                }

                var error = Math.Max(
                    Math.Abs(route.EndPosition.X - next.StartPosition.X),
                    Math.Abs(route.EndPosition.Y - next.StartPosition.Y));
                if (error > request.PositionTolerance)
                {
                    report.Breaks.Add(new LoopCompositionBreak
                    {
                        LoopIndex = index,
                        NextLoopIndex = index + 1,
                        Reason = LoopCompositionBreakReason.Disconnected,
                        PositionError = error,
                    });
                    continue;
                }
                report.Connected++;
            }

            foreach (var pair in routeTicks) report.EnumerationTicks += pair.Value;
            report.Complete = report.RoutesMissing == 0 && report.RoutesDirty == 0 &&
                report.Connected == Math.Max(0, segments.Count - 1);
            return report;
        }

        private static int CountGroups(IReadOnlyList<LoopSegment> segments)
        {
            var seen = new HashSet<int>();
            for (var index = 0; index < segments.Count; index++)
                seen.Add(segments[index].ThreatSignature);
            return seen.Count;
        }
    }
}
