using System;
using System.Collections.Generic;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// The composition driver is what turns per-loop routes into a fight. Its
    /// job is to refuse a chain that does not join, so every test here is about a
    /// way the chain can look right and not be.
    /// </summary>
    internal static partial class Program
    {
        private static void VerifyLoopComposition()
        {
            CompositionAcceptsAChainOfCleanRoutes();
            CompositionReusesOneRoutePerGroup();
            CompositionRejectsAMissingRoute();
            CompositionRejectsADirtyRoute();
            CompositionDetectsABrokenBoundary();
            CompositionUsesTheNextLoopsRealStartNotTheBucket();
            CompositionRequiresAnEndPosition();
            CompositionReportsTheReduction();
            CompositionAcceptsASingleLoop();
        }

        private static LoopSegment Segment(int startTick, int endTick, int bucket,
            int signature, float startX, float endX)
        {
            return new LoopSegment
            {
                StartTick = startTick,
                EndTick = endTick,
                StartBucket = bucket,
                ThreatSignature = signature,
                Complete = true,
                Closed = true,
                StartPosition = new Vec2(startX, 0f),
                EndPosition = new Vec2(endX, 0f),
            };
        }

        private static LoopRoute Route(bool found, int hits, int ticks, float endX)
        {
            return new LoopRoute
            {
                Found = found,
                Hits = hits,
                Ticks = ticks,
                EndPosition = new Vec2(endX, 0f),
            };
        }

        /// <summary>Three loops, each starting where the previous one ended, each
        /// with a clean route that ends on the next loop's start.</summary>
        private static void CompositionAcceptsAChainOfCleanRoutes()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 100f, 100f),
                Segment(100, 200, 1, 7, 100f, 100f),
                Segment(200, 300, 1, 7, 100f, 100f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 100f),
                Route(true, 0, 100, 100f),
                Route(true, 0, 100, 100f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            True(report.Complete, "a chain of clean routes should compose");
            Equal(3, report.Loops);
            Equal(1, report.Groups);
            Equal(2, report.Connected);
            Equal(0, report.TotalHits);
            Equal(0, report.Breaks.Count);
        }

        /// <summary>The point of the decomposition: many loops, one route. The
        /// enumeration cost is the distinct groups, not the loops.</summary>
        private static void CompositionReusesOneRoutePerGroup()
        {
            var segments = new List<LoopSegment>();
            var routes = new List<LoopRoute>();
            for (var index = 0; index < 40; index++)
            {
                segments.Add(Segment(index * 100, index * 100 + 100, 1, 7,
                    100f, 100f));
                routes.Add(Route(true, 0, 100, 100f));
            }
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            True(report.Complete, "forty identical loops should compose");
            Equal(40, report.Loops);
            Equal(1, report.Groups);
            Equal(4000, report.FightTicks);
            // One route of 100 ticks pays for all forty loops.
            Equal(100, report.EnumerationTicks);
            True(report.ReductionFactor > 39f && report.ReductionFactor < 41f,
                "reduction should be the loop count: " + report.ReductionFactor);
        }

        private static void CompositionRejectsAMissingRoute()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 100f, 100f),
                Segment(100, 200, 1, 8, 100f, 100f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 100f),
                Route(false, 0, 0, 0f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            False(report.Complete, "a missing route cannot compose");
            Equal(1, report.RoutesMissing);
            Equal(1, report.Breaks.Count);
            Equal(1, report.Breaks[0].LoopIndex);
            True(report.Breaks[0].Reason ==
                LoopCompositionBreakReason.MissingRoute,
                "the break should name the missing route");
        }

        private static void CompositionRejectsADirtyRoute()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 100f, 100f),
                Segment(100, 200, 1, 7, 100f, 100f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 100f),
                Route(true, 2, 100, 100f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            False(report.Complete, "a route that takes hits cannot compose");
            Equal(1, report.RoutesDirty);
            Equal(2, report.TotalHits);
            True(report.Breaks[0].Reason ==
                LoopCompositionBreakReason.DirtyRoute,
                "the break should name the dirty route");
            Equal(2, report.Breaks[0].Hits);
        }

        /// <summary>A clean route that stops short of where the next loop begins
        /// stitches together two states that never occur next to each other. It
        /// would look clean the whole way, which is why it is checked.</summary>
        private static void CompositionDetectsABrokenBoundary()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 100f, 100f),
                Segment(100, 200, 1, 7, 140f, 140f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 100f),
                Route(true, 0, 100, 140f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            False(report.Complete, "a disconnected boundary cannot compose");
            Equal(0, report.Connected);
            Equal(1, report.Breaks.Count);
            True(report.Breaks[0].Reason ==
                LoopCompositionBreakReason.Disconnected,
                "the break should name the boundary");
            Equal(0, report.Breaks[0].LoopIndex);
            Equal(1, report.Breaks[0].NextLoopIndex);
            True(report.Breaks[0].PositionError == 40f,
                "the break should carry the distance: " +
                report.Breaks[0].PositionError);
        }

        /// <summary>
        /// Two loops in the same group start in the same bucket, and a bucket is
        /// deliberately coarse. A route that ends in the right bucket can still
        /// land tens of pixels from where the next loop actually starts, so the
        /// check has to use the next loop's recorded start rather than the
        /// bucket. This test would pass under a bucket comparison and must fail.
        /// </summary>
        private static void CompositionUsesTheNextLoopsRealStartNotTheBucket()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 4, 7, 100f, 100f),
                Segment(100, 200, 4, 7, 112f, 112f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 100f),
                Route(true, 0, 100, 112f),
            };
            var loose = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest { PositionTolerance = 16f });
            True(loose.Complete,
                "a twelve pixel gap is inside a sixteen pixel tolerance");

            var tight = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest { PositionTolerance = 4f });
            False(tight.Complete,
                "the same chain must fail a four pixel tolerance, which a " +
                "bucket comparison could never see");
            True(tight.Breaks[0].PositionError == 12f,
                "the reported gap should be the real one: " +
                tight.Breaks[0].PositionError);
        }

        /// <summary>A route that never recorded where it ends cannot be joined,
        /// and treating a default end as the origin would silently accept it
        /// whenever the next loop happens to start near the origin.</summary>
        private static void CompositionRequiresAnEndPosition()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 900f, 900f),
                Segment(100, 200, 1, 7, 900f, 900f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 100, 0f),
                Route(true, 0, 100, 900f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            False(report.Complete,
                "a route with no end position must not be joined");
            True(float.IsPositiveInfinity(report.Breaks[0].PositionError),
                "an unrecorded end should be reported as unreachable, not " +
                "as a small distance");
        }

        private static void CompositionReportsTheReduction()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 400, 1, 7, 100f, 100f),
                Segment(400, 800, 1, 9, 100f, 100f),
            };
            var routes = new List<LoopRoute>
            {
                Route(true, 0, 200, 100f),
                Route(true, 0, 300, 100f),
            };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            Equal(800, report.FightTicks);
            Equal(500, report.EnumerationTicks);
            // A tolerance rather than equality: the factor is a division and its
            // bit pattern need not match the literal 1.6f.
            True(Math.Abs(report.ReductionFactor - 1.6f) < .0001f,
                "reduction should be 800/500: " + report.ReductionFactor);
        }

        private static void CompositionAcceptsASingleLoop()
        {
            var segments = new List<LoopSegment>
            {
                Segment(0, 100, 1, 7, 100f, 100f),
            };
            var routes = new List<LoopRoute> { Route(true, 0, 100, 100f) };
            var report = LoopComposition.Compose(segments, routes,
                new LoopCompositionRequest());
            True(report.Complete, "a single clean loop composes trivially");
            Equal(0, report.Connected);
            Equal(0, report.Breaks.Count);
        }
    }
}
