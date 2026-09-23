using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>
        /// Calibrates the threat model against a real fight and then runs the
        /// enumerator over a real loop.
        ///
        /// The calibration is the part that has to come first. A hit test that
        /// misses a tick reports a false clean, and a false clean is the one
        /// result this whole method exists to avoid; a hit test that flags a
        /// tick where life did not fall is merely wasteful. So the misses are
        /// counted separately from the false alarms and printed as such, and
        /// only then is anything searched.
        /// </summary>
        private static int VerifyLoopSearchTrace(string path)
        {
            try
            {
                var parser = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue,
                };
                var rows = new List<Dictionary<string, object>>();
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    rows.Add(parser.Deserialize<Dictionary<string, object>>(line));
                }
                if (rows.Count < 300)
                    throw new InvalidDataException(
                        "Expected a dense per-tick trace, got " + rows.Count +
                        " rows.");

                var floor = float.MinValue;
                for (var index = 0; index < 240 && index < rows.Count; index++)
                    floor = Math.Max(floor,
                        Player(rows[index], "position", "y") +
                        Player(rows[index], "height"));

                // Threat and observation rows are paired by tick: a row is
                // sampled after its native update, so row i holds the player and
                // the threat as they stand at the end of tick i, and a hit during
                // tick i is what the overlap at row i has to show.
                var threats = new List<TraceThreat>();
                // The rows that produced the threats, kept in step with them.
                // Rows before the takeover have no Boss and are skipped, so the
                // two lists are shorter than the trace and index i in one is not
                // row i in the other. Pairing them by position anyway put the
                // player of one tick next to the projectiles of another, which
                // reported a minimum separation of 2.88 px where the trace
                // really has overlapping boxes.
                var threatRows = new List<Dictionary<string, object>>();
                var observations = new List<LoopObservation>();
                var bossType = 0;
                for (var index = 0; index < rows.Count; index++)
                {
                    var boss = BossNpc(rows[index]);
                    if (boss == null) continue;
                    if (bossType == 0) bossType = (int)Number(boss, "type");
                    threats.Add(BuildThreat(rows[index], boss));
                    threatRows.Add(rows[index]);
                    observations.Add(new LoopObservation
                    {
                        PlayerPosition = new Vec2(
                            Player(rows[index], "position", "x"),
                            Player(rows[index], "position", "y")),
                        BossPosition = new Vec2(
                            Number(Object(boss, "position"), "x"),
                            Number(Object(boss, "position"), "y")),
                        BossState = (int)Number(boss, "ai", 0),
                        Hit = index > 0 && Player(rows[index], "life") <
                            Player(rows[index - 1], "life"),
                    });
                }

                var withProjectiles = 0;
                for (var index = 0; index < threats.Count; index++)
                    if (threats[index].Projectiles != null &&
                        threats[index].Projectiles.Length > 0) withProjectiles++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH rawRows={0} threats={1} firstThreatTick={2} " +
                    "lastThreatTick={3} bossType={4} ticksWithProjectiles={5}",
                    rows.Count, threats.Count, threats[0].Tick,
                    threats[threats.Count - 1].Tick, bossType,
                    withProjectiles));

                // Calibration: replay the recorded trajectory through the world's
                // own hit test and compare against the ticks where life fell.
                var flagged = 0;
                var actual = 0;
                var both = 0;
                var missed = 0;
                var falseAlarm = 0;
                var minimumGap = float.MaxValue;
                var minimumGapTick = -1;
                var minimumGapBox = string.Empty;
                for (var index = 1; index < threats.Count; index++)
                {
                    var frame = BuildFrame(threatRows[index], threatRows[index], floor,
                        false);
                    var threat = threats[index];
                    var overlap = TraceLoopWorld.Overlaps(in frame,
                        in threat);
                    // The smallest separation seen, so a zero flag count can be
                    // told apart from a hit test that never had anything near it.
                    if (threat.Projectiles != null)
                        for (var p = 0; p < threat.Projectiles.Length; p++)
                        {
                            var projectile = threat.Projectiles[p];
                            if (projectile.Width <= 0 ||
                                projectile.Height <= 0) continue;
                            var gapX = Math.Max(Math.Max(
                                frame.Position.X - (projectile.Position.X +
                                    projectile.Width),
                                projectile.Position.X - (frame.Position.X +
                                    frame.Width)), 0f);
                            var gapY = Math.Max(Math.Max(
                                frame.Position.Y - (projectile.Position.Y +
                                    projectile.Height),
                                projectile.Position.Y - (frame.Position.Y +
                                    frame.Height)), 0f);
                            var gap = Math.Max(gapX, gapY);
                            if (gap < minimumGap)
                            {
                                minimumGap = gap;
                                minimumGapTick = threat.Tick;
                                minimumGapBox = "type=" + projectile.Type +
                                    " pw=" + projectile.Width + "x" +
                                    projectile.Height + " fw=" + frame.Width +
                                    "x" + frame.Height;
                            }
                        }
                    var life = Player(rows[index], "life") <
                        Player(rows[index - 1], "life");
                    if (overlap) flagged++;
                    if (life) actual++;
                    if (overlap && life) both++;
                    else if (life) missed++;
                    else if (overlap) falseAlarm++;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH CALIBRATION flagged={0} actualHits={1} " +
                    "matched={2} MISSED={3} falseAlarm={4} " +
                    "minGap={5:R} minGapTick={6} {7}",
                    flagged, actual, both, missed, falseAlarm, minimumGap,
                    minimumGapTick, minimumGapBox));

                // The same comparison against the pre-update field with swept
                // boxes. This is the pairing a hit test needs: a hit is what
                // removes the projectile, so the post-update list cannot contain
                // it. Reported next to the post-update numbers rather than
                // replacing them, because the difference is the whole point.
                var sweptFlagged = 0;
                var sweptMatched = 0;
                var sweptMissed = 0;
                var sweptFalseAlarm = 0;
                var beforeUpdateTicks = 0;
                for (var index = 1; index < threats.Count; index++)
                {
                    var frame = BuildFrame(threatRows[index], threatRows[index],
                        floor, false);
                    var threat = threats[index];
                    if (threat.ProjectilesBeforeUpdate != null)
                        beforeUpdateTicks++;
                    var overlap = TraceLoopWorld.Hit(in frame, in threat, true);
                    var life = Player(threatRows[index], "life") <
                        Player(threatRows[index - 1], "life");
                    if (overlap) sweptFlagged++;
                    if (overlap && life) sweptMatched++;
                    else if (life) sweptMissed++;
                    else if (overlap) sweptFalseAlarm++;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH SWEPT flagged={0} matched={1} MISSED={2} " +
                    "falseAlarm={3} ticksWithBeforeUpdateField={4}",
                    sweptFlagged, sweptMatched, sweptMissed, sweptFalseAlarm,
                    beforeUpdateTicks));

                // The reviewed threat model in place of the axis-aligned box.
                // This is the test that is supposed to be right: type 872
                // collides with its fifty-point oldPos trail and type 919 with
                // an eighty-by-eight oriented segment, so a box at the current
                // position was wrong in both directions at once.
                var modeledFlagged = 0;
                var modeledMatched = 0;
                var modeledMissed = 0;
                var modeledFalseAlarm = 0;
                var modeledTicks = 0;
                for (var index = 1; index < threats.Count; index++)
                {
                    var frame = BuildFrame(threatRows[index], threatRows[index],
                        floor, false);
                    // The collision happens during the update, while the player
                    // is still travelling, but the row records where the player
                    // ended up. Testing the end position alone moves the player
                    // by up to a tick of travel and drops grazing hits: the
                    // type-923 lance that took life at a perpendicular offset of
                    // twenty and a half pixels was missed for exactly that
                    // reason. The box therefore covers both ends of the tick.
                    var previous = BuildFrame(threatRows[index - 1],
                        threatRows[index - 1], floor, false);
                    var swept = frame;
                    var left = Math.Min(previous.Position.X, frame.Position.X);
                    var top = Math.Min(previous.Position.Y, frame.Position.Y);
                    var right = Math.Max(
                        previous.Position.X + previous.Width,
                        frame.Position.X + frame.Width);
                    var bottom = Math.Max(
                        previous.Position.Y + previous.Height,
                        frame.Position.Y + frame.Height);
                    swept.Position = new Vec2(left, top);
                    swept.Width = (int)Math.Ceiling(right - left);
                    swept.Height = (int)Math.Ceiling(bottom - top);
                    var threat = threats[index];
                    var overlap = TraceLoopWorld.Hit(in swept, in threat, true,
                        true);
                    var life = Player(threatRows[index], "life") <
                        Player(threatRows[index - 1], "life");
                    if (overlap) modeledFlagged++;
                    if (overlap && life) modeledMatched++;
                    else if (life) modeledMissed++;
                    else if (overlap) modeledFalseAlarm++;
                    modeledTicks++;
                    if (life && !overlap)
                    {
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "LOOP SEARCH MODELED MISS tick={0} life={1} " +
                            "nearestBefore={2} nearestAfter={3} bossGap={4}",
                            threat.Tick, Player(threatRows[index], "life"),
                            NearestGap(in frame, threat.ProjectilesBeforeUpdate),
                            NearestGap(in frame, threat.Projectiles),
                            BossGap(in frame, in threat)));
                    }
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH MODELED flagged={0} matched={1} MISSED={2} " +
                    "falseAlarm={3} ticks={4}",
                    modeledFlagged, modeledMatched, modeledMissed,
                    modeledFalseAlarm, modeledTicks));

                // What the misses actually are. A hit test that misses reports a
                // clean tick, so the misses are the only rows worth explaining:
                // for each one, how close the nearest projectile was in either
                // field, and how close the Boss was. A projectile a few pixels
                // away means the box is the wrong shape; nothing near at all
                // means the damaging thing was never in the trace to be seen.
                for (var index = 1; index < threats.Count; index++)
                {
                    var row = threatRows[index];
                    var life = Player(row, "life") <
                        Player(threatRows[index - 1], "life");
                    if (!life) continue;
                    var frame = BuildFrame(row, row, floor, false);
                    var threat = threats[index];
                    var overlap = TraceLoopWorld.Hit(in frame, in threat, true);
                    if (overlap) continue;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP SEARCH MISS tick={0} life={1} immuneTime={2} " +
                        "nearestBefore={3:R} nearestAfter={4:R} bossGap={5:R}",
                        threat.Tick, Player(row, "life"),
                        Player(row, "immuneTime"),
                        NearestGap(in frame, threat.ProjectilesBeforeUpdate),
                        NearestGap(in frame, threat.Projectiles),
                        BossGap(in frame, in threat)));
                }

                var segments = LoopDecomposition.Segment(observations,
                    new LoopSegmentationOptions { MinimumTicks = 30 });
                var summary = LoopDecompositionSummary.Summarize(segments);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH loops={0} longestLoop={1} clean={2}",
                    segments.Count, summary.LongestLoop, summary.CleanLoops));

                if (segments.Count == 0) return 0;

                // Search the first loop that is long enough to be a real one.
                var target = -1;
                for (var index = 0; index < segments.Count; index++)
                    if (segments[index].Length >= 30) { target = index; break; }
                if (target < 0) return 0;

                var segment = segments[target];
                var slice = new List<TraceThreat>();
                for (var tick = segment.StartTick;
                    tick <= segment.EndTick && tick < threats.Count; tick++)
                    slice.Add(threats[tick]);
                var startRow = threatRows[segment.StartTick];
                var start = BuildFrame(startRow, startRow, floor, false);
                // The decomposition indexes observations by position in the
                // list, and the world indexes its threats the same way, so the
                // start frame has to carry that same index. Setting it from the
                // engine tick instead puts the search off the front of the loop
                // by however many ticks the trace starts at, and every one of
                // the twelve actions is then refused as past the end.
                start.Tick = slice[0].Tick;
                var startRelativeX = segment.StartPosition.X -
                    threats[segment.StartTick].BossPosition.X;
                var startRelativeY = segment.StartPosition.Y -
                    threats[segment.StartTick].BossPosition.Y;

                var world = new TraceLoopWorld(slice, startRelativeX,
                    startRelativeY, new TraceLoopWorldOptions
                    {
                        GoalTolerance = BoundaryTolerance,
                        PositionCellSize = 8f,
                        VelocityCellSize = 2f,
                    });
                var report = RouteEnumerator.Search(world, in start,
                    new RouteSearchRequest
                    {
                        StepBudget = slice.Count + 8,
                        BeamWidth = 256,
                        MaxExpansions = 200000,
                    });
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP SEARCH loop={0} ticks={1} found={2} clean={3} " +
                    "hits={4} expanded={5} generated={6} dominance={7} " +
                    "beam={8} refused={9} truncated={10}",
                    target, segment.Length, report.Found, report.Clean,
                    report.Hits, report.Expanded, report.Generated,
                    report.PrunedByDominance, report.PrunedByBeam,
                    report.Refused, report.Truncated));
                if (report.RefusalsByReason != null)
                    foreach (var pair in report.RefusalsByReason)
                        Console.WriteLine("LOOP SEARCH REFUSAL " + pair.Key +
                            "=" + pair.Value);

                ComposeLoops(segments, threats, threatRows, floor, path);
                AuditObservedRoutes(segments, threats, threatRows, floor);
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine("LOOP SEARCH FAILED " + error.Message);
                Console.WriteLine("LOOP SEARCH FAILED DETAIL " +
                    error.GetType().Name + " :: " + error.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// The driver the method was missing: walk the decomposition's segments
        /// in order and search each one from the state the previous route ended
        /// in, so the fight is covered end to end rather than one loop at a time.
        ///
        /// The state is threaded rather than reset, because a per-loop search
        /// that always restarts from the observed state proves only that each
        /// loop is individually survivable and says nothing about whether the
        /// routes join. Threading is what makes the composition claim real, and
        /// it is also what makes it fail visibly: the first segment whose route
        /// is not found or not clean stops the walk and is reported by index,
        /// so the coverage is a count of segments actually crossed and not an
        /// extrapolation from one success.
        ///
        /// Every segment keeps its own threat field, taken from the trace at
        /// that segment's own ticks, because the drift between cycles is a
        /// handful of ticks and a field borrowed from another cycle would be
        /// describing a different fight.
        /// </summary>
        private static void ComposeLoops(List<LoopSegment> segments,
            List<TraceThreat> threats,
            List<Dictionary<string, object>> threatRows, float floor,
            string tracePath)
        {
            if (segments.Count == 0)
            {
                Console.WriteLine("LOOP COMPOSE segments=0 crossed=0");
                return;
            }
            var options = new TraceLoopWorldOptions
            {
                GoalTolerance = BoundaryTolerance,
                PositionCellSize = 8f,
                VelocityCellSize = 2f,
                // Measured from the trace, not guessed: the player sits at
                // exactly x 640 while pressing left with zero velocity, and the
                // furthest right the player ever gets is x 6315.8 with a body
                // twenty wide. Without these the model walks through the wall
                // and diverges by hundreds of pixels inside one loop.
                ArenaLeft = 640f,
                ArenaRight = 6336f,
                ClampToArena = true,
                SurviveToEnd = true,
                // BossFollowsPlayer stays off. Two generative Boss models have
                // been tried and both make the first loop unpassable.
                //
                // The first held the recorded offset between Boss and player, on
                // the reasoning that a chasing Boss keeps its relative geometry.
                // It does not: the offset is an outcome of the chase, and in the
                // recording the Boss is often close, so pinning it glues the Boss
                // to the player. Three and a half million refusals, no route
                // through loop one.
                //
                // The second advanced the Boss from its own state toward the
                // player at the speed the recording shows for that tick, with the
                // state carried on the frame so it travels with the branch. The
                // recorded speed is the result of the geometry that actually
                // occurred; applying it while the geometry differs over-drives the
                // Boss past its real attack and hover phases. One and a half
                // million expansions this time, still no route through loop one.
                //
                // A kinematic approximation is not enough. A faithful generative
                // threat needs the Boss's own state machine, which is a project
                // rather than a patch. Until then the trace-anchored field stays,
                // and the honest way to correct it is to iterate: run a candidate,
                // take the trace it produces, and re-cut against that, so the
                // field converges on the fight the candidate is actually in.
            };
            // Loops whose observed route took a hit are kept, not dropped.
            //
            // The reason they were dropped was that a hit knocks the player back,
            // so the loop's endpoint is somewhere no clean route can reach. That
            // reason does not apply here: this composition's goal is the end of
            // the loop in any legal state, so the search is never asked for the
            // knocked-back position. Dropping them also makes the hit loops
            // unfixable by construction -- the chain simply skips them, and the
            // skip moves the player, so the threat field expires at the seam.
            // Measured: re-cutting against a candidate's own trace, which is the
            // self-consistent step, still gave seven or eight hits, because the
            // seven bad loops were excluded from the chain both times.
            //
            // The counting stays, because which loops the recording was hit in is
            // worth reporting even when they are kept.
            var chain = new List<LoopSegment>();
            var hitLoops = 0;
            var hitIndices = new List<int>();
            var loopIndex = -1;
            foreach (var candidate in segments)
            {
                loopIndex++;
                var contaminated = false;
                for (var row = candidate.StartTick;
                    row <= candidate.EndTick && row < threatRows.Count; row++)
                {
                    if (Player(threatRows[row], "immuneTime") > 0f)
                    {
                        contaminated = true;
                        break;
                    }
                }
                if (contaminated) { hitLoops++; hitIndices.Add(loopIndex); }
                chain.Add(candidate);
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "LOOP COMPOSE HITLOOPS {0}",
                string.Join(",", hitIndices.ConvertAll(
                    value => value.ToString(CultureInfo.InvariantCulture))
                    .ToArray())));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "LOOP COMPOSE CHAIN loops={0} clean={1} hitLoops={2}",
                segments.Count, chain.Count, hitLoops));
            var first = segments[0].StartTick;
            // How many loops to compose, when a driver wants one at a time.
            //
            // The threat field a trace provides is only valid while the route
            // follows that trace, and a composed route leaves it at the first
            // takeover tick. Composing one loop, running it, and composing the
            // next against the trace that run produced keeps the field valid at
            // every loop's start, because the route prefix that led there is
            // exactly the one the trace recorded. This is the switch that lets a
            // driver take those steps; unset means compose the whole fight.
            var loopLimitText = Environment.GetEnvironmentVariable(
                "CHAITE_COMPOSE_LOOPS");
            int loopLimit;
            if (int.TryParse(loopLimitText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out loopLimit) &&
                loopLimit > 0 && loopLimit < chain.Count)
            {
                chain.RemoveRange(loopLimit, chain.Count - loopLimit);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE LIMITED loops={0}", chain.Count));
            }
            // The route is indexed from the first tick the plugin drives, and
            // the engine drives from the chain's first segment. Published as a
            // manifest so each loop can be probed on its own: the model's verdict
            // for a loop is only meaningful while the route follows the
            // recording, and the recording is left at the first tick, so the
            // engine has to be the one that decides.
            {
                var manifest = new StringBuilder();
                manifest.AppendLine(
                    "index,startTick,endTick,ticks,actionStart,actionEnd,observedHits");
                var observed = 0;
                for (var i = 0; i < chain.Count; i++)
                {
                    var segment = chain[i];
                    var hits = 0;
                    for (var row = segment.StartTick;
                        row <= segment.EndTick && row < threatRows.Count; row++)
                    {
                        if (Player(threatRows[row], "immuneTime") > 0f) hits++;
                    }
                    observed += hits;
                    manifest.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6}",
                        i, segment.StartTick, segment.EndTick,
                        segment.EndTick - segment.StartTick + 1,
                        segment.StartTick - first,
                        segment.EndTick - first, hits));
                }
                var manifestPath = Path.Combine(Path.GetDirectoryName(tracePath),
                    "loop-segments.csv");
                File.WriteAllText(manifestPath, manifest.ToString(),
                    new UTF8Encoding(false));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE SEGMENTS {0} loops={1} observedHits={2}",
                    manifestPath, chain.Count, observed));
            }
            var seed = BuildFrame(threatRows[first], threatRows[first], floor,
                false);
            // A boundary carries a set of states, not one. The state a loop
            // leaves behind is what the next loop starts from, and a single one
            // can be a dead end even when the loop had many clean routes through
            // it: carrying one chained twenty-three loops and then stopped at a
            // loop whose entire space it exhausted in six hundred fifty-five
            // expansions. The width is the only approximation here, and it is
            // reported.
            var branches = new List<ComposeBranch>();
            branches.Add(new ComposeBranch(seed, new List<RouteAction>()));
            var crossed = 0;
            var failedAt = -1;
            var composedTicks = 0;
            var composedHits = 0;
            var replayRefusals = 0;
            var replayedHits = 0;
            var resynced = 0;
            var widest = 1;
            // The chain, not the raw segmentation. It was computed and reported
            // and then never used: this loop walked every segment regardless, so
            // the loop limit a driver sets had no effect on the route at all and
            // the exported route came back byte-identical. Measured directly --
            // with the limit at two the manifest said two loops while the route
            // still covered all fifty-one and the engine returned the unchanged
            // seven hits.
            for (var index = 0; index < chain.Count; index++)
            {
                var segment = chain[index];
                var slice = new List<TraceThreat>();
                for (var tick = segment.StartTick;
                    tick <= segment.EndTick && tick < threats.Count; tick++)
                    slice.Add(threats[tick]);
                if (slice.Count < 2) continue;
                // The goal is the end of this loop in any legal state, so no
                // relative offsets are needed; SurviveToEnd drops the place and
                // keeps the tick bound, which is what the decomposition is for.
                var world = new TraceLoopWorld(slice, 0f, 0f, options);
                var next = new List<ComposeBranch>();
                var expanded = 0;
                var truncated = 0;
                var refusals = new Dictionary<string, int>();
                var seen = new HashSet<string>();
                ComposeBranch fallback = null;
                var fallbackHits = 0;
                // The recording's own route enters the search as a candidate.
                //
                // The composition used to throw it away and substitute whatever
                // the enumeration found first, and the enumeration's per-loop
                // routes measured worse than the recording's own: one loop gave
                // seven engine hits against the script's four, two loops thirteen,
                // three nine. Meanwhile the recording's route is engine-clean in
                // thirty-three of forty loops. So the observed continuation is
                // carried as a branch, which is a statement about the search space
                // and not a preference about which branch to take: the frontier
                // still hands goals out in turn and the engine still decides.
                //
                // The inverse mapping is lossy in the one place the alphabet is:
                // Up and Down are not in RouteAction, so a recorded tick that held
                // them cannot be reproduced here. That gap is already recorded as
                // a known hole in the enumeration, and this branch inherits it
                // rather than working around it.
                {
                    var observedBranches = new List<ComposeBranch>();
                    // The recorded continuation also serves as the fallback when
                    // the enumeration finds nothing clean. A loop where the model
                    // has no clean route used to stop the whole composition, and
                    // it stopped at loop twenty-six of twenty-seven on the trace
                    // that had just been measured, exporting nothing. Falling back
                    // to the recorded controls keeps the fight going and charges
                    // the hits it takes to the composition's own count, so the
                    // report says five hits rather than claiming to have failed.
                    ComposeBranch observedFallback = null;
                    var observedFallbackHits = int.MaxValue;
                    foreach (var branch in branches)
                    {
                        var from = branch.State;
                        from.Tick = slice[0].Tick;
                        var route = new List<RouteAction>(branch.Route);
                        var reached = true;
                        var taken = 0;
                        for (var tick = segment.StartTick;
                            tick <= segment.EndTick; tick++)
                        {
                            // The decomposition's last segment can end past the
                            // rows the trace actually holds, which is why every
                            // other walk over these rows carries the same bound.
                            // Without it the observed branch indexed off the end
                            // on the final loop and the whole composition died
                            // there with an index error, after eleven minutes of
                            // work and without exporting anything.
                            if (tick >= threatRows.Count) { reached = false; break; }
                            var row = threatRows[tick];
                            var recorded = ObservedControls(row,
                                tick + 1 < threatRows.Count
                                    ? threatRows[tick + 1]
                                    : null);
                            var action = new RouteAction
                            {
                                Direction = recorded.Right ? 1
                                    : (recorded.Left ? -1 : 0),
                                Jump = recorded.Jump,
                                Dash = recorded.Dash,
                            };
                            PlayerMotionFrame stepped;
                            bool hit;
                            ForwardModelRefusal refusal;
                            if (!world.TryStep(in from, action.ToControls(),
                                    out stepped, out hit, out refusal))
                            {
                                reached = false;
                                break;
                            }
                            if (hit) taken++;
                            route.Add(action);
                            from = stepped;
                        }
                        if (!reached) continue;
                        if (taken == 0)
                            observedBranches.Add(new ComposeBranch(from, route));
                        if (taken < observedFallbackHits)
                        {
                            observedFallbackHits = taken;
                            observedFallback = new ComposeBranch(from, route);
                        }
                    }
                    if (observedBranches.Count > 0)
                    {
                        branches.AddRange(observedBranches);
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "LOOP COMPOSE OBSERVEDBRANCH loop={0} added={1}",
                            index, observedBranches.Count));
                    }
                    if (observedFallback != null)
                    {
                        fallback = observedFallback;
                        fallbackHits = observedFallbackHits;
                    }
                }
                // Goals are collected per branch and then handed out in turn,
                // rather than filled from whichever branch is searched first.
                // The old shape broke out of this loop as soon as the frontier
                // was full, and one branch can fill it on its own, so every
                // frontier after the first was built from branch zero alone. The
                // symptom was forty-eight routes that differed only in their last
                // loop, and an engine that gave all of them the same verdict
                // because the loops that matter had no alternatives in them.
                var perBranch = new List<List<ComposeBranch>>();
                foreach (var branch in branches)
                {
                    var from = branch.State;
                    from.Tick = slice[0].Tick;
                    // The composition enumerates; it does not strategise. The
                    // beam's width and its dominance pruning were a second
                    // policy hiding inside the splicer, so the sweep that
                    // already enumerates loop routes is the one that runs
                    // here: every action from every state at a tick, exact
                    // dedup, hit states dropped as the objective, and whole
                    // states cut per depth by the weighted score rather than
                    // merged. The ceiling, the per-depth keep and the score
                    // weights are bounds on the enumeration, not a route
                    // preference, and they arrive from the environment so
                    // choosing what to prefer is not this file's job.
                    var report = RouteEnumerator.Exhaustive(world, in from,
                        slice.Count + 8, ComposeExhaustiveCeiling(),
                        ComposeExhaustiveKeep(), ComposeExhaustiveScore(world, slice));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP COMPOSE SEARCH loop={0} found={1} clean={2} " +
                        "expanded={3} truncated={4} goals={5} dominance={6} " +
                        "beam={7} refusals={8}",
                        index, report.GoalStates.Count > 0,
                        report.GoalStates.Count > 0, report.Expansions,
                        report.Truncated, report.GoalStates.Count,
                        0L, report.Dropped, report.RefusalsByReason.Count));
                    expanded += report.Expansions > int.MaxValue
                        ? int.MaxValue : (int)report.Expansions;
                    truncated += report.Truncated ? 1 : 0;
                    foreach (var pair in report.RefusalsByReason)
                    {
                        int known;
                        refusals.TryGetValue(pair.Key, out known);
                        refusals[pair.Key] = known + (int)pair.Value;
                    }
                    var mine = new List<ComposeBranch>();
                    perBranch.Add(mine);
                    if (report.GoalStates.Count == 0) continue;
                    // The sweep can land on more goal arrivals than the
                    // boundary can carry, and replaying all of them spends the
                    // budget on near-duplicates of the same arrival. The list is
                    // in discovery order, which is the per-depth score order,
                    // so the first ones are the ones the weights prefer; the
                    // rest are still enumerated, only not all of them chained.
                    var goalLimit = Math.Min(report.GoalStates.Count,
                        BoundaryWidth);
                    for (var goal = 0; goal < goalLimit; goal++)
                    {
                        var frame = from;
                        var replayFailed = false;
                        foreach (var action in report.GoalRoutes[goal])
                        {
                            PlayerMotionFrame stepped;
                            bool hit;
                            ForwardModelRefusal refusal;
                            // The replay goes through the same world the search
                            // used, so the arena clamp and the threat test both
                            // apply. Calling the forward model directly skipped
                            // both and walked the state through the wall.
                            var controls = action.ToControls();
                            if (!world.TryStep(in frame, in controls,
                                out stepped, out hit, out refusal))
                            {
                                replayRefusals++;
                                replayFailed = true;
                                break;
                            }
                            // Counted here rather than taken from the search
                            // report, so the hit count in the summary is an
                            // independent replay of the controls the route
                            // actually issues, not the search's bookkeeping.
                            if (hit) replayedHits++;
                            frame = stepped;
                        }
                        if (replayFailed) continue;
                        // Re-synchronise at the loop boundary. The model is exact
                        // at the origin and holds to about one and a third pixels
                        // over the first ten ticks, but the error grows with the
                        // tick count -- one point three five, then ten, then sixty
                        // by the third bucket -- and chaining fifty-one loops of
                        // extrapolation is what carries the route out of safety and
                        // gives the engine its hits. The method's second step is
                        // that the boundaries are where the engine's own trace
                        // supplies the state. The resync is applied only when the
                        // replay's length matches the slice it was searched over,
                        // because otherwise the model's frame and the trace's frame
                        // are not the same moment and the substitution would be
                        // unsound rather than corrective.
                        // The gate is the model's tick against the trace tick at
                        // the end of the slice. It was written against the loop's
                        // end index, which is a threat index, while the frame
                        // carries a trace tick -- two different numberings that can
                        // never be equal, so the resync never fired once and the
                        // route came out byte-identical to the run without it. That
                        // is this project's definition of a change that did not
                        // execute. Both numbers are printed so the relationship is
                        // read off the run rather than assumed again.
                        var endTraceTick = slice[slice.Count - 1].Tick;
                        if (resynced + 1 <= 3)
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "LOOP COMPOSE TICK index={0} frameTick={1} " +
                                "sliceFirst={2} sliceEndTick={3} endIndex={4}",
                                index, frame.Tick, slice[0].Tick, endTraceTick,
                                segment.EndTick));
                        if (frame.Tick == endTraceTick &&
                            segment.EndTick >= 0 && segment.EndTick < threatRows.Count)
                        {
                            frame = BuildFrame(threatRows[segment.EndTick],
                                threatRows[segment.EndTick], floor, false);
                            frame.Tick = endTraceTick;
                            resynced++;
                        }
                        // Bucketing the boundary keeps the frontier from filling
                        // with states that differ by less than the world's own
                        // resolution.
                        var key = string.Format(CultureInfo.InvariantCulture,
                            "{0:F0}|{1:F0}|{2:F0}|{3:F0}",
                            frame.Position.X / 8f, frame.Position.Y / 8f,
                            frame.Velocity.X / 2f, frame.Velocity.Y / 2f);
                        if (!seen.Add(key)) continue;
                        var route = new List<RouteAction>(branch.Route);
                        route.AddRange(report.GoalRoutes[goal]);
                        mine.Add(new ComposeBranch(frame, route));
                    }
                }
                // Hand the boundary out in turns so every surviving branch is
                // represented before any branch gets a second state.
                for (var round = 0; next.Count < BoundaryWidth; round++)
                {
                    var offered = false;
                    foreach (var mine in perBranch)
                    {
                        if (round >= mine.Count) continue;
                        offered = true;
                        next.Add(mine[round]);
                        if (next.Count >= BoundaryWidth) break;
                    }
                    if (!offered) break;
                }
                if (next.Count == 0 && fallback != null)
                {
                    // No clean route through this loop in the model. Carry the
                    // recorded controls and charge what they cost, rather than
                    // stopping the composition here.
                    next.Add(fallback);
                    composedHits += fallbackHits;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP COMPOSE FALLBACK index={0} hits={1}",
                        index, fallbackHits));
                }
                if (next.Count == 0)
                {
                    failedAt = index;
                    // What the engine actually pressed during the loop that
                    // stopped the composition. The enumerator's alphabet has no
                    // Up and no Down, so if the observed route leaned on either
                    // the wall may be the alphabet rather than the model.
                    var down = 0;
                    var up = 0;
                    var jump = 0;
                    var horizontal = 0;
                    var observedHits = 0;
                    for (var row = segment.StartTick;
                        row <= segment.EndTick && row < threatRows.Count; row++)
                    {
                        var observed = threatRows[row];
                        if (PlayerBoolean(observed, "controlDown")) down++;
                        if (PlayerBoolean(observed, "controlUp")) up++;
                        if (PlayerBoolean(observed, "controlJump")) jump++;
                        if (PlayerBoolean(observed, "controlLeft") ||
                            PlayerBoolean(observed, "controlRight")) horizontal++;
                        if (Player(observed, "immuneTime") > 0f) observedHits++;
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP COMPOSE FAIL OBSERVED down={0} up={1} jump={2} " +
                        "horizontal={3} hitRows={4} rows={5}",
                        down, up, jump, horizontal, observedHits,
                        segment.Length + 1));
                    foreach (var pair in refusals)
                        Console.WriteLine("LOOP COMPOSE FAIL REFUSAL " +
                            pair.Key + "=" + pair.Value);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP COMPOSE FAIL index={0} ticks={1} branches={2} " +
                        "expanded={3} truncated={4} replayRefusals={5}",
                        index, segment.Length, branches.Count, expanded,
                        truncated, replayRefusals));
                    break;
                }
                branches = next;
                if (branches.Count > widest) widest = branches.Count;
                crossed++;
                composedTicks += segment.Length;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE THREAD index={0} branches={1} x={2} y={3} " +
                    "vx={4} vy={5}",
                    index, branches.Count, branches[0].State.Position.X,
                    branches[0].State.Position.Y, branches[0].State.Velocity.X,
                    branches[0].State.Velocity.Y));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "LOOP COMPOSE segments={0} crossed={1} failedAt={2} " +
                "composedTicks={3} composedHits={4} replayRefusals={5} " +
                "complete={6} widest={7} routeTicks={8} replayedHits={9} " +
                "resynced={10}",
                segments.Count, crossed, failedAt, composedTicks, composedHits,
                replayRefusals, crossed == segments.Count, widest,
                branches.Count > 0 ? branches[0].Route.Count : 0,
                replayedHits, resynced));
            // The route is written out because the model saying zero hits is not
            // the acceptance criterion; the isolated probe is. Exporting the
            // controls is what lets the probe replay exactly this route rather
            // than a re-derived approximation of it.
            // Gated on the chain, not on every segment. The full fight is the
            // case this was written for, but a driver composing one loop at a
            // time crosses one loop and would export nothing -- and since the
            // route file is only overwritten on export, the probe then replayed
            // whatever route happened to be left there from the previous run.
            // Measured: a one-loop composition reported crossing one loop and the
            // file still held all four thousand one hundred fifty-three actions.
            if (crossed == chain.Count && branches.Count > 0)
            {
                var routeLines = new List<string>();
                routeLines.Add("direction,jump,dash");
                foreach (var action in branches[0].Route)
                    routeLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2}", action.Direction, action.Jump ? 1 : 0,
                        action.Dash ? 1 : 0));
                var routePath = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(tracePath)),
                    "composed-route.csv");
                File.WriteAllLines(routePath, routeLines.ToArray());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE ROUTE {0} ticks={1}", routePath,
                    branches[0].Route.Count));
                // The frontier after the last loop holds one complete route per
                // surviving branch, and they are distinct routes, not variations
                // of one. Exporting only the first threw that away and left the
                // engine with a single candidate to judge, which is not an
                // enumeration. Every branch is written out and a candidate list
                // is produced for the isolated route verifier, so the engine
                // decides which of them are real rather than the model.
                var exportDirectory = Path.GetDirectoryName(Path.GetFullPath(tracePath));
                var exportCount = Math.Min(branches.Count, BoundaryWidth);
                var candidates = new List<string>();
                candidates.Add("label,scenario,route,seed,routeFile,skip,maxTicks,takeoverTick");
                var identity = ReadTraceIdentity(exportDirectory);
                for (var branchIndex = 0; branchIndex < exportCount; branchIndex++)
                {
                    var branchLines = new List<string>();
                    branchLines.Add("direction,jump,dash");
                    foreach (var action in branches[branchIndex].Route)
                        branchLines.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2}", action.Direction, action.Jump ? 1 : 0,
                            action.Dash ? 1 : 0));
                    var branchPath = Path.Combine(exportDirectory, string.Format(
                        CultureInfo.InvariantCulture, "composed-route-{0:D3}.csv",
                        branchIndex));
                    File.WriteAllLines(branchPath, branchLines.ToArray());
                    candidates.Add(string.Format(CultureInfo.InvariantCulture,
                        "composed-{0:D3},{1},{2},{3},{4},0,12000,120",
                        branchIndex, identity.Scenario, identity.Route,
                        identity.Seed, branchPath));
                }
                var candidatePath = Path.Combine(exportDirectory, "candidates.csv");
                File.WriteAllLines(candidatePath, candidates.ToArray());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE CANDIDATES {0} count={1}", candidatePath,
                    exportCount));
                // The route is replayed once more over the whole fight and
                // compared against the engine's own rows. The model calling it
                // hit-free is only worth something if the model's trajectory is
                // the engine's trajectory, and the first tick where they part is
                // where the residual lives.
                var whole = new TraceLoopWorld(threats, 0f, 0f, options);
                // The rows to compare against are the engine's rows from the run
                // that actually issued this route, not the rows the threats came
                // from. Those two traces are different routes -- the seed trace
                // is the script's play and this route is not -- so comparing
                // against the seed trace would report a divergence at the first
                // tick that is nothing but the two routes differing. The raw rows
                // carry every tick, so the engine tick indexes them directly.
                var residualRaw = threatRows;
                var residualPath =
                    Environment.GetEnvironmentVariable("CHAITE_RESIDUAL_TRACE");
                if (!string.IsNullOrEmpty(residualPath) &&
                    File.Exists(residualPath))
                {
                    residualRaw = new List<Dictionary<string, object>>();
                    var residualParser = new JavaScriptSerializer
                    {
                        MaxJsonLength = int.MaxValue,
                    };
                    foreach (var line in File.ReadLines(residualPath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        residualRaw.Add(residualParser.Deserialize<
                            Dictionary<string, object>>(line));
                    }
                }
                var walker = seed;
                walker.Tick = threats[0].Tick;
                var divergenceTick = -1;
                var worst = 0f;
                for (var step = 0; step < branches[0].Route.Count; step++)
                {
                    PlayerMotionFrame stepped;
                    bool steppedHit;
                    ForwardModelRefusal steppedRefusal;
                    var stepControls = branches[0].Route[step].ToControls();
                    if (!whole.TryStep(in walker, in stepControls, out stepped,
                            out steppedHit, out steppedRefusal))
                        break;
                    walker = stepped;
                    // The engine's row for the state the model has reached. The
                    // seed is the state entering the first Boss row's tick, and
                    // one model step leaves the state entering the next tick, so
                    // the step count indexes the engine's ticks from there. Off
                    // by one here reads as a fifteen pixel divergence at the very
                    // first tick, which is not a model error at all.
                    var engineTick = threats[0].Tick + step;
                    var engineRow = engineTick - 1;
                    if (engineRow < 0 || engineRow >= residualRaw.Count) break;
                    var enginePlayer = Object(residualRaw[engineRow], "player");
                    var enginePosition = Object(enginePlayer, "position");
                    var dx = walker.Position.X - Number(enginePosition, "x");
                    var dy = walker.Position.Y - Number(enginePosition, "y");
                    var gap = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (gap > worst) worst = gap;
                    if (gap > 1f && divergenceTick < 0)
                    {
                        divergenceTick = engineTick;
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "LOOP COMPOSE DIVERGE tick={0} modelX={1:F2} " +
                            "modelY={2:F2} engineX={3:F2} engineY={4:F2} " +
                            "gap={5:F2} modelVX={6:F3} modelVY={7:F3} " +
                            "engineVX={8:F3} engineVY={9:F3}",
                            engineTick, walker.Position.X, walker.Position.Y,
                            Number(enginePosition, "x"),
                            Number(enginePosition, "y"), gap,
                            walker.Velocity.X, walker.Velocity.Y,
                            Number(Object(enginePlayer, "velocity"), "x"),
                            Number(Object(enginePlayer, "velocity"), "y")));
                    }
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE RESIDUAL firstDivergenceTick={0} " +
                    "worstGap={1:F2} walked={2}",
                    divergenceTick, worst, walker.Tick - threats[0].Tick));
            }
        }

        /// <summary>How many states a loop boundary carries forward. One is a
        /// greedy chain and can walk into a dead end; the frontier is what makes
        /// the composition a search over boundaries rather than a single line
        /// through them.</summary>
        // Completeness check: raised to 192 and the exported route came back
        // byte-identical, so this cap is not currently deleting the answer. The
        // frontier does reach the cap (widest=192), so the space is wider than
        // this, but the beam's ordering picks the same path either way. Keep the
        // smaller cap for runtime; re-test it whenever the ranking changes.
        private const int BoundaryWidth = 48;

        /// <summary>How close to the loop's start the player must return, in
        /// pixels, measured relative to the Boss.
        ///
        /// This was suspected of pinning the enumeration: the goal point is where
        /// the observed route happened to be, so a four pixel radius would force
        /// every branch to reproduce that trace's trajectory. Raised to
        /// thirty-two and the exported routes came back byte-identical, all
        /// forty-eight of them, so the tolerance is not binding -- the states the
        /// search already returns satisfy the tighter value. The refuted
        /// hypothesis is the useful part: whatever collapses forty-eight distinct
        /// routes into two engine runs is not the width of the goal.</summary>
        private const float BoundaryTolerance = 4f;

        /// <summary>What the probe run that produced this trace was launched as,
        /// so an exported candidate can be replayed by the isolated verifier
        /// without anyone retyping it. The identity lives in the result.json
        /// beside the trace.</summary>
        private struct TraceIdentity
        {
            public string Scenario;
            public string Route;
            public int Seed;
        }

        private static TraceIdentity ReadTraceIdentity(string directory)
        {
            var identity = new TraceIdentity
            {
                Scenario = "duke-fishron",
                Route = "fishron-strong-wing",
                Seed = 2,
            };
            var resultPath = Path.Combine(directory, "result.json");
            if (!File.Exists(resultPath)) return identity;
            try
            {
                var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = parser.DeserializeObject(
                    File.ReadAllText(resultPath)) as Dictionary<string, object>;
                if (root == null) return identity;
                identity.Scenario = Text(root, "scenario") ?? identity.Scenario;
                identity.Route = Text(root, "route") ?? identity.Route;
                var seed = Text(root, "seed");
                int parsed;
                if (int.TryParse(seed, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out parsed))
                    identity.Seed = parsed;
            }
            catch (Exception)
            {
                // A trace without a readable result.json still exports; the
                // defaults are the trace this driver was built around.
            }
            return identity;
        }

        private static string Text(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) ||
                value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>One surviving line through the loops so far: the state it has
        /// reached and the controls that got it there.</summary>
        private sealed class ComposeBranch
        {
            public readonly PlayerMotionFrame State;
            public readonly List<RouteAction> Route;

            public ComposeBranch(PlayerMotionFrame state, List<RouteAction> route)
            {
                State = state;
                Route = route;
            }
        }

        /// <summary>
        /// Sweeps one loop exhaustively and reports how wide the sweep gets.
        ///
        /// The question this answers is not whether a route exists -- the
        /// best-first search already answers that -- but whether the loop's
        /// reachable state set is small enough to enumerate without pruning
        /// anything. The user's position is that under the character's mobility it
        /// is, and that the bucketed dominance rule is what deletes the answer
        /// rather than what makes the search affordable. Both claims are
        /// measurable, and this is the measurement: the frontier width per tick,
        /// the total distinct states, and how many of them close the loop.
        /// </summary>
        /// <summary>
        /// The exhaustive sweep's bounds, read from the environment so the
        /// splicer itself stays a splicer. The weights default to uniform --
        /// no hand-written preference for one safe state over another -- and
        /// every cut the sweep makes is reported through Dropped and Truncated
        /// rather than hidden.
        /// </summary>
        private static int ComposeExhaustiveCeiling()
        {
            int ceiling;
            return int.TryParse(Environment.GetEnvironmentVariable(
                "CHAITE_COMPOSE_CEILING"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out ceiling) && ceiling > 0
                ? ceiling : 120000;
        }

        private static int ComposeExhaustiveKeep()
        {
            int keep;
            return int.TryParse(Environment.GetEnvironmentVariable(
                "CHAITE_COMPOSE_KEEP"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out keep) && keep >= 0
                ? keep : 32;
        }

        private static Func<PlayerMotionFrame, float> ComposeExhaustiveScore(
            TraceLoopWorld world, List<TraceThreat> slice)
        {
            var text = Environment.GetEnvironmentVariable(
                "CHAITE_COMPOSE_WEIGHTS");
            if (string.IsNullOrEmpty(text)) text = "1,1,1,1";
            var parts = text.Split(',');
            if (parts.Length < 4) return null;
            var weights = new float[parts.Length];
            for (var index = 0; index < parts.Length; index++)
                if (!float.TryParse(parts[index], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out weights[index]))
                    return null;
            return delegate(PlayerMotionFrame state)
            {
                // The same features the sweep's CLI ranks by, because they are
                // the ones the loop can actually see: how far the state still
                // is from closing the loop, how much room there is to the
                // nearest hostile projectile and to the boss, and how much
                // flight resource is left. The last is the "and what follows"
                // part -- a state that is safe now but has spent its wings is
                // worth less than one that is equally safe and can still move.
                var index = state.Tick - slice[0].Tick;
                if (index < 0) index = 0;
                if (index >= slice.Count) index = slice.Count - 1;
                var threat = slice[index];
                var gap = NearestGap(in state, threat.Projectiles);
                if (float.IsNaN(gap)) gap = 400f;
                if (gap > 400f) gap = 400f;
                var bossGap = 400f;
                if (threat.BossWidth > 0 && threat.BossHeight > 0)
                {
                    var gapX = Math.Max(Math.Max(
                        state.Position.X - (threat.BossPosition.X +
                            threat.BossWidth),
                        threat.BossPosition.X - (state.Position.X +
                            state.Width)), 0f);
                    var gapY = Math.Max(Math.Max(
                        state.Position.Y - (threat.BossPosition.Y +
                            threat.BossHeight),
                        threat.BossPosition.Y - (state.Position.Y +
                            state.Height)), 0f);
                    bossGap = (float)Math.Sqrt(gapX * gapX + gapY * gapY);
                    if (bossGap > 400f) bossGap = 400f;
                }
                var wing = state.Flight.WingTimeMax > 0f
                    ? state.Flight.WingTime / state.Flight.WingTimeMax : 0f;
                var goal = world.DistanceToGoal(in state);
                if (float.IsNaN(goal) || float.IsInfinity(goal)) goal = 400f;
                if (goal > 400f) goal = 400f;
                return weights[0] * (-goal) + weights[1] * gap +
                    weights[2] * bossGap + weights[3] * wing;
            };
        }

        private static int ExhaustiveLoop(string tracePath, string startTickText,
            string budgetText, string ceilingText, string keepText,
            string weightText)
        {
            if (!File.Exists(tracePath)) return 2;
            var rows = new List<Dictionary<string, object>>();
            var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (var line in File.ReadLines(tracePath))
            {
                if (line.Trim().Length == 0) continue;
                rows.Add(parser.DeserializeObject(line)
                    as Dictionary<string, object>);
            }
            if (rows.Count == 0) return 2;
            var floor = float.MinValue;
            for (var index = 0; index < rows.Count; index++)
                floor = Math.Max(floor,
                    Player(rows[index], "position", "y") +
                    Player(rows[index], "height"));

            // The route origin is measured the same way the audit measures it:
            // the row whose replay counter reads zero.
            var origin = 0;
            for (var index = 0; index < rows.Count; index++)
            {
                object rawPlan;
                if (!rows[index].TryGetValue("plan", out rawPlan)) continue;
                var plan = rawPlan as Dictionary<string, object>;
                object rawFrame;
                if (plan == null || !plan.TryGetValue("replayFrame", out rawFrame))
                    continue;
                if (Convert.ToInt32(rawFrame, CultureInfo.InvariantCulture) == 0)
                {
                    origin = index;
                    break;
                }
            }

            var threats = new List<TraceThreat>();
            var threatRows = new List<Dictionary<string, object>>();
            for (var index = 0; index < rows.Count; index++)
            {
                var boss = BossNpc(rows[index]);
                if (boss == null) continue;
                threats.Add(BuildThreat(rows[index], boss));
                threatRows.Add(rows[index]);
            }

            int startTick;
            if (!int.TryParse(startTickText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out startTick)) startTick = origin;
            int budget;
            if (!int.TryParse(budgetText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out budget)) budget = 60;
            int ceiling;
            if (!int.TryParse(ceilingText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out ceiling)) ceiling = 2000000;
            int keep;
            if (!int.TryParse(keepText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out keep)) keep = 0;
            float[] weights = null;
            if (!string.IsNullOrEmpty(weightText))
            {
                var parts = weightText.Split(',');
                weights = new float[parts.Length];
                for (var index = 0; index < parts.Length; index++)
                    float.TryParse(parts[index], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out weights[index]);
            }

            var slice = new List<TraceThreat>();
            for (var tick = startTick; tick <= startTick + budget &&
                tick < threats.Count; tick++)
                slice.Add(threats[tick]);
            if (slice.Count < 2) return 2;

            var options = new TraceLoopWorldOptions
            {
                GoalTolerance = BoundaryTolerance,
                PositionCellSize = 8f,
                VelocityCellSize = 2f,
                ArenaLeft = 640f,
                ArenaRight = 6336f,
                ClampToArena = true,
                // The loops are cut on the boss's attack script, not on the player
                // returning to a place, so the goal is surviving to the end of the
                // cycle. Leaving this off makes the goal a position, and the
                // recorded route -- which the engine itself calls a clean loop --
                // ends three hundred seventy pixels from where it began, so nothing
                // ever satisfies it and the sweep reports no goals at all. That is
                // the method's own definition of a perfect loop: no hits inside a
                // cycle.
                SurviveToEnd = true,
            };
            var world = new TraceLoopWorld(slice, 0f, 0f, options);
            var state = BuildFrame(threatRows[startTick], threatRows[startTick],
                floor, false);
            // The goal is a boss-relative position, and the relative offset has to
            // be measured from the trace rather than passed as zero. With zero the
            // goal becomes "stand where the boss ends up", which no swept state
            // ever satisfies, and the sweep reported no goals at all on loops the
            // recording itself closes cleanly.
            world = new TraceLoopWorld(slice,
                state.Position.X - slice[slice.Count - 1].BossPosition.X,
                state.Position.Y - slice[slice.Count - 1].BossPosition.Y,
                options);
            // The world indexes its slice by subtracting the slice's own first
            // tick, so the frame's tick has to be the trace's tick and not a
            // relative index. Setting it to zero made every root action refuse
            // with PastLoopEnd, and a sweep that refuses everything measures the
            // harness rather than the character's mobility.
            state.Tick = slice[0].Tick;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "EXHAUSTIVE ORIGIN stateTick={0} sliceFirst={1} sliceCount={2} " +
                "threatRowsTick={3}",
                state.Tick, slice[0].Tick, slice.Count,
                Integer(threatRows[startTick], "tick")));
            // The score the sweep ranks by. Its weights arrive as arguments rather
            // than being written here, because choosing what a good route looks
            // like is not this file's job -- an optimiser supplies them and this
            // only enumerates. The features are the ones the loop can actually
            // see: how far the state still is from closing the loop, how much
            // room there is to the nearest hostile projectile and to the boss, and
            // how much flight resource is left. The last two are the "and what
            // follows" part: a state that is safe now but has spent its wings is
            // worth less than one that is equally safe and can still move.
            Func<PlayerMotionFrame, float> score = null;
            if (weights != null && weights.Length >= 4)
            {
                score = delegate(PlayerMotionFrame state)
                {
                    var index = state.Tick - slice[0].Tick;
                    if (index < 0) index = 0;
                    if (index >= slice.Count) index = slice.Count - 1;
                    var threat = slice[index];
                    var gap = NearestGap(in state, threat.Projectiles);
                    if (float.IsNaN(gap)) gap = 400f;
                    if (gap > 400f) gap = 400f;
                    var bossGap = 400f;
                    if (threat.BossWidth > 0 && threat.BossHeight > 0)
                    {
                        var gapX = Math.Max(Math.Max(
                            state.Position.X - (threat.BossPosition.X +
                                threat.BossWidth),
                            threat.BossPosition.X - (state.Position.X +
                                state.Width)), 0f);
                        var gapY = Math.Max(Math.Max(
                            state.Position.Y - (threat.BossPosition.Y +
                                threat.BossHeight),
                            threat.BossPosition.Y - (state.Position.Y +
                                state.Height)), 0f);
                        bossGap = (float)Math.Sqrt(gapX * gapX + gapY * gapY);
                        if (bossGap > 400f) bossGap = 400f;
                    }
                    var wing = state.Flight.WingTimeMax > 0f
                        ? state.Flight.WingTime / state.Flight.WingTimeMax : 0f;
                    var goal = world.DistanceToGoal(in state);
                    if (float.IsNaN(goal) || float.IsInfinity(goal)) goal = 400f;
                    if (goal > 400f) goal = 400f;
                    return weights[0] * (-goal) + weights[1] * gap +
                        weights[2] * bossGap + weights[3] * wing;
                };
            }

            RouteEnumerator.ExhaustiveReport report;
            try
            {
                report = RouteEnumerator.Exhaustive(world, in state, budget,
                    ceiling, keep, score);
            }
            catch (Exception error)
            {
                // The runtime cannot render this exception's own ToString, so the
                // type and the first frames of the stack are printed directly.
                Console.WriteLine("EXHAUSTIVE THREW type=" +
                    error.GetType().FullName);
                Console.WriteLine("EXHAUSTIVE THREW message=" + error.Message);
                Console.WriteLine("EXHAUSTIVE THREW stack=" + error.StackTrace);
                return 1;
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "EXHAUSTIVE start={0} budget={1} states={2} expansions={3} " +
                "refused={4} widest={5} goals={6} truncated={7} keep={8} " +
                "dropped={9}",
                startTick, budget, report.States, report.Expansions,
                report.Refused, report.Widest, report.GoalStates.Count,
                report.Truncated, keep, report.Dropped));
            var frontier = new System.Text.StringBuilder();
            for (var index = 0; index < report.Frontier.Count; index++)
            {
                if (index > 0) frontier.Append(',');
                frontier.Append(report.Frontier[index]);
            }
            Console.WriteLine("EXHAUSTIVE FRONTIER " + frontier);
            // Whether the loop is closable in the model at all, measured by
            // replaying the controls the engine actually used. If the recorded
            // route closes the loop in the game and this says the world never
            // accepts it, the wall is the model or the alphabet rather than the
            // search, and those have different fixes.
            var replay = BuildFrame(threatRows[startTick],
                threatRows[startTick], floor, false);
            // Same correction the sweep needed: the frame's tick is what indexes
            // the world's slice, and BuildFrame leaves it at zero, so the replay
            // refused its first step and reported a position that meant nothing.
            replay.Tick = slice[0].Tick;
            var replayHits = 0;
            var replayRefusals = 0;
            var reached = false;
            for (var step = 0; step < budget; step++)
            {
                var here = startTick + step;
                if (here + 1 >= threatRows.Count) break;
                var controls = ObservedControls(threatRows[here],
                    threatRows[here + 1]);
                PlayerMotionFrame stepped;
                bool hit;
                ForwardModelRefusal refusal;
                if (!world.TryStep(in replay, in controls, out stepped, out hit,
                    out refusal))
                {
                    replayRefusals++;
                    break;
                }
                if (hit) replayHits++;
                replay = stepped;
                if (world.IsGoal(in replay)) { reached = true; break; }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "EXHAUSTIVE OBSERVED ticks={0} hits={1} refusals={2} reached={3} " +
                "endTick={4} goalTick={5} endX={6:F1} endY={7:F1} relX={8:F1} " +
                "relY={9:F1}",
                budget, replayHits, replayRefusals, reached, replay.Tick,
                world.GoalTick, replay.Position.X, replay.Position.Y,
                replay.Position.X - slice[slice.Count - 1].BossPosition.X,
                replay.Position.Y - slice[slice.Count - 1].BossPosition.Y));
            foreach (var pair in report.RefusalsByReason)
                Console.WriteLine("EXHAUSTIVE REFUSAL " + pair.Key + "=" +
                    pair.Value);
            var shown = 0;
            foreach (var route in report.GoalRoutes)
            {
                if (shown >= 5) break;
                shown++;
                var text = new System.Text.StringBuilder();
                foreach (var action in route)
                {
                    text.Append('(').Append(action.Direction).Append(',')
                        .Append(action.Jump ? 1 : 0).Append(',')
                        .Append(action.Dash ? 1 : 0).Append(')');
                }
                Console.WriteLine("EXHAUSTIVE GOAL ticks=" + route.Count +
                    " " + text);
            }
            return 0;
        }

        /// <summary>
        /// Replays the controls the engine actually saw through the same world
        /// the search uses, and compares what the world scores against what the
        /// engine did. This is the experiment that separates the two remaining
        /// explanations for a boundary the search cannot cross.
        ///
        /// If the observed route reaches the goal and the world calls it clean
        /// while the search found nothing, then a solution was in the space and
        /// the pruning removed it, which is the failure the notes warn about. If
        /// the world calls the observed route a hit where the engine took no
        /// life, the threat field is over-flagging and is pruning real routes.
        /// The two have opposite fixes, so guessing between them wastes a round.
        /// </summary>
        private static void AuditObservedRoutes(List<LoopSegment> segments,
            List<TraceThreat> threats,
            List<Dictionary<string, object>> threatRows, float floor)
        {
            var options = new TraceLoopWorldOptions
            {
                GoalTolerance = BoundaryTolerance,
                PositionCellSize = 8f,
                VelocityCellSize = 2f,
                // Measured from the trace, not guessed: the player sits at
                // exactly x 640 while pressing left with zero velocity, and the
                // furthest right the player ever gets is x 6315.8 with a body
                // twenty wide. Without these the model walks through the wall
                // and diverges by hundreds of pixels inside one loop.
                ArenaLeft = 640f,
                ArenaRight = 6336f,
                ClampToArena = true,
            };
            var shown = 0;
            for (var index = 0; index < segments.Count && shown < 10; index++)
            {
                var segment = segments[index];
                var slice = new List<TraceThreat>();
                for (var tick = segment.StartTick;
                    tick <= segment.EndTick && tick < threats.Count; tick++)
                    slice.Add(threats[tick]);
                if (slice.Count < 2) continue;
                var goalSegment = index + 1 < segments.Count
                    ? segments[index + 1] : segment;
                float relativeX;
                float relativeY;
                // The audit compares the model against the observed route, so
                // its goal is where that route actually finished: the segment's
                // last row. Passing the first row here instead, which the
                // shared helper's new signature made easy to do by accident,
                // moved the goal a whole loop and reported four segments as
                // six and a quarter pixels out when they were exact.
                GoalOffsets(slice, threatRows,
                    segment.StartTick + slice.Count - 1,
                    out relativeX, out relativeY);
                var world = new TraceLoopWorld(slice, relativeX, relativeY,
                    options);
                var state = BuildFrame(threatRows[segment.StartTick],
                    threatRows[segment.StartTick], floor, false);
                state.Tick = slice[0].Tick;
                var worldHits = 0;
                var refused = 0;
                var reached = false;
                var refusalName = string.Empty;
                var maxError = 0f;
                var errorTick = 0;
                var hitAt = 0;
                var driftShown = 0;
                var diverged = false;
                // The control that produced a row lives in that row, not in the
                // one before it: the recorded control bits describe the
                // transition into the row they are stored on. Stepping with the
                // source row's controls is therefore a tick late, and it silently
                // swallows every one-tick event. That is exactly what happened
                // here: the model stayed grounded on a tick the engine had just
                // jumped on, and it never entered a dash that the engine held for
                // thirty-one ticks, because the press was read from the wrong
                // row. Iterating over destination rows also removes the tick
                // arithmetic, since the destination row is the engine row.
                for (var engineRow = segment.StartTick + 1;
                    engineRow <= segment.EndTick + 1 &&
                    engineRow < threatRows.Count; engineRow++)
                {
                    var controls = ObservedControls(threatRows[engineRow],
                        engineRow + 1 < threatRows.Count
                            ? threatRows[engineRow + 1]
                            : null);
                    PlayerMotionFrame next;
                    bool hit;
                    ForwardModelRefusal refusal;
                    if (!world.TryStep(in state, in controls, out next, out hit,
                        out refusal))
                    {
                        refused++;
                        refusalName = refusal.ToString();
                        break;
                    }
                    if (hit) worldHits++;
                    state = next;
                    // The model is only worth searching with if it reproduces
                    // the engine on the very trajectory the engine produced, so
                    // the divergence is measured rather than assumed. A goal the
                    // observed route cannot reach is either a wrong goal or a
                    // model that walked away from the engine, and this number
                    // says which.
                    if (engineRow >= 0 && engineRow < threatRows.Count)
                    {
                        var engine = BuildFrame(threatRows[engineRow],
                            threatRows[engineRow], floor, false);
                        // A hit knocks the player back, and the knockback is not
                        // something the forward model reproduces or should. The
                        // engine's invulnerability counter appearing is the
                        // signature of a hit, and the trace shows the shape of
                        // it: a vertical velocity reversing from plus eight to
                        // minus three and a half with the horizontal halved, at
                        // exactly the four ticks where the whole fight reverses
                        // downward motion, two of which were being reported as
                        // model divergence. Measuring through a hit therefore
                        // measures the knockback, not the model, so the error is
                        // only accumulated while the engine has taken no hit,
                        // and the hit tick is reported alongside.
                        if (hitAt == 0 && Player(threatRows[engineRow], "immuneTime") > 0f)
                            hitAt = state.Tick;
                        var errorX = state.Position.X - engine.Position.X;
                        var errorY = state.Position.Y - engine.Position.Y;
                        var error = (float)Math.Sqrt(errorX * errorX +
                            errorY * errorY);
                        if (hitAt == 0 && error > maxError)
                        {
                            maxError = error;
                            errorTick = state.Tick;
                        }
                        // Report the first tick at which the model has clearly
                        // left the engine, with the regime both sides were in.
                        // A divergence is only actionable if it can be attached
                        // to a regime: a few pixels is rounding, hundreds means
                        // the model is flying where the engine was standing, or
                        // the reverse.
                        // Gated on the hit counter, like the maximum error above.
                        //
                        // Without the gate this printed the knockback and called
                        // it model divergence. Measured on the trace this was
                        // written against: at tick four hundred seven the model
                        // predicted minus seven and a half horizontally and the
                        // engine showed plus four and a half, which reads as a
                        // model failure until the row is opened and the life drop
                        // of eighty-two and the invulnerability counter of forty
                        // show it was the hit. The same at tick five hundred
                        // twenty-five, where the invulnerability counter of four
                        // marks it as still inside an earlier hit's knockback. Both
                        // were reported as divergence and neither was.
                        if (error > 4f && !diverged && hitAt == 0)
                        {
                            diverged = true;
                            // The engine's own fields are read through the
                            // tolerant accessor rather than by digging out a
                            // player object, because a trace row may hold the
                            // player block directly or nest it.
                            var engineRowValues = threatRows[engineRow];
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "LOOP COMPOSE DIVERGE index={0} tick={1} " +
                                "error={2} modelGrounded={3} modelWing={4} " +
                                "modelDash={5} engineJump={6} " +
                                "engineWing={7} engineRocket={8} " +
                                "engineVX={9} engineVY={10} " +
                                "modelVX={11} modelVY={12} " +
                                "dashDelay={13} dashTime={14} " +
                                "eocDash={15} sinceStart={16} " +
                                "engineDashDelay={17} engineDashTime={18} " +
                                "engineEoc={19} engineSinceStart={20} " +
                                "acc={21} max={22} " +
                                "engineGravity={23}",
                                index, state.Tick, error, state.Grounded,
                                state.WingTime, state.Dashing,
                                Player(engineRowValues, "jump"),
                                Player(engineRowValues, "wingTime"),
                                Player(engineRowValues, "rocketTime"),
                                engine.Velocity.X, engine.Velocity.Y,
                                state.Velocity.X, state.Velocity.Y,
                                state.Dash.DashDelay,
                                state.Dash.DashTime, state.Dash.EocDash,
                                state.Dash.TimeSinceLastDashStarted,
                                Player(engineRowValues, "dashDelay"),
                                Player(engineRowValues, "dashTime"),
                                Player(engineRowValues, "eocDash"),
                                Player(engineRowValues, "timeSinceLastDashStarted"),
                                state.Dash.AccRunSpeed, state.Dash.MaxRunSpeed,
                                Player(engineRowValues, "gravity")));
                        }
                        if (false)
                        {
                            driftShown++;
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "LOOP COMPOSE DRIFT tick={0} error={1} " +
                                "modelX={2} engineX={3} modelY={4} engineY={5} " +
                                "modelVX={6} engineVX={7} modelVY={8} " +
                                "engineVY={9} grounded={10}",
                                state.Tick, error, state.Position.X,
                                engine.Position.X, state.Position.Y,
                                engine.Position.Y, state.Velocity.X,
                                engine.Velocity.X, state.Velocity.Y,
                                engine.Velocity.Y, state.Grounded));
                        }
                    }
                    if (world.IsGoal(in state)) { reached = true; break; }
                }
                var engineHits = 0;
                for (var tick = segment.StartTick + 1;
                    tick <= segment.EndTick && tick < threatRows.Count; tick++)
                    if (Player(threatRows[tick], "life") <
                        Player(threatRows[tick - 1], "life")) engineHits++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSE OBSERVED index={0} ticks={1} worldHits={2} " +
                    "engineHits={3} reachedGoal={4} refused={5} refusal={6} " +
                    "endTick={7} maxError={8} errorTick={9} hitAt={10}",
                    index, segment.Length, worldHits, engineHits, reached,
                    refused, refusalName, state.Tick, maxError, errorTick,
                    hitAt));
                shown++;
            }
        }

        /// <summary>
        /// The recorded controls, with the dash rebuilt from the engine's own dash
        /// state rather than from the control field.
        ///
        /// The control field is not enough. In this version the shield dash fires
        /// on a double tap, and the trace shows the dash delay going to minus one
        /// with the contact counter at fifteen while the recorded dash control is
        /// zero on that frame and on every frame around it. Reading the control
        /// field therefore produced routes with no dashes in them at all, and a
        /// route with no dashes against a trajectory full of them is a comparison
        /// that measures nothing: the model walked while the engine dashed from
        /// frame seven hundred and thirty-four onward, and every error figure it
        /// reported came from that.
        ///
        /// The dash starts on the frame where the delay becomes negative, so the
        /// control belongs on the frame before it, which is also what the model
        /// needs: its dedicated candidate is created from a ready state and leaves
        /// the delay at minus one.
        /// </summary>
        private static PlayerControlFrame ObservedControls(
            Dictionary<string, object> row, Dictionary<string, object> nextRow)
        {
            var controls = ObservedControls(row);
            if (nextRow == null) return controls;
            var delay = Field(Object(row, "player"), "dashDelay");
            var nextDelay = Field(Object(nextRow, "player"), "dashDelay");
            if (delay >= 0f && nextDelay < 0f)
            {
                controls.Dash = true;
                // The direction is missing for the same reason the dash itself
                // was. A double-tap dash leaves both direction controls clear in
                // the trace, so the model fell back to its facing direction and
                // dashed left on a frame where the engine dashed right at fourteen
                // and a half. The engine's own velocity on the dash frame carries
                // the sign, so that is where the direction comes from.
                var velocityX = Field(Object(nextRow, "player"), "velocity", "x");
                if (velocityX < 0f) { controls.Left = true; controls.Right = false; }
                else if (velocityX > 0f)
                {
                    controls.Right = true;
                    controls.Left = false;
                }
            }
            return controls;
        }

        private static PlayerControlFrame ObservedControls(
            Dictionary<string, object> row)
        {
            return new PlayerControlFrame
            {
                Left = Boolean(Object(row, "player"), "controlLeft"),
                Right = Boolean(Object(row, "player"), "controlRight"),
                // Up and Down are part of the control frame and the vertical
                // model reads them, but the replay left both at their default.
                // The trace shows what that costs: the engine holds a terminal
                // fall of ten and one hundredth while Down is pressed and drops
                // to three and a third the moment it is released, so a replay
                // that never reports Down keeps the model in the wrong descent
                // regime for the whole fall.
                Up = Boolean(Object(row, "player"), "controlUp"),
                Down = Boolean(Object(row, "player"), "controlDown"),
                Jump = Boolean(Object(row, "player"), "controlJump"),
                Dash = Boolean(Object(row, "player"), "controlDash"),
            };
        }

        /// <summary>
        /// The goal the world actually tests, expressed the way the world tests
        /// it. IsGoal compares the player against the boss position at the last
        /// tick of the slice, so the relative offset has to be taken against
        /// that same boss position. Taking it against the boss at the loop's
        /// first tick instead is off by a whole loop of boss travel, which on
        /// the Fishron trace is around three hundred pixels: the goal then sits
        /// where the boss used to be, no route can reach it, and the search
        /// reports a full exhaustive failure that looks like a threat problem
        /// and is not one. The observed route failing to reach the goal is what
        /// exposed it, since the observed route reaches the observed state by
        /// construction.
        /// </summary>
        /// <summary>The goal is the player's position at the row the next loop
        /// begins on, expressed relative to the boss at the last tick of the
        /// slice being searched. Aiming at this loop's own end instead makes the
        /// target the observed route's endpoint, and when that route was hit the
        /// endpoint is where knockback left the player rather than anywhere a
        /// clean route would go, so the search is asked for something the next
        /// loop's threat field has no reason to accept.</summary>
        private static void GoalOffsets(List<TraceThreat> slice,
            List<Dictionary<string, object>> threatRows, int playerRowIndex,
            out float relativeX, out float relativeY)
        {
            var last = slice[slice.Count - 1];
            var player = Object(threatRows[playerRowIndex], "player");
            var position = Object(player, "position");
            relativeX = Number(position, "x") - last.BossPosition.X;
            relativeY = Number(position, "y") - last.BossPosition.Y;
        }

        private static TraceThreat BuildThreat(Dictionary<string, object> row,
            Dictionary<string, object> boss)
        {
            var player = Object(row, "player");
            var threat = new TraceThreat
            {
                Tick = Integer(row, "tick"),
                BossPresent = true,
                BossType = (int)Number(boss, "type"),
                BossPosition = new Vec2(
                    Number(Object(boss, "position"), "x"),
                    Number(Object(boss, "position"), "y")),
                BossWidth = (int)Number(boss, "width"),
                BossHeight = (int)Number(boss, "height"),
                PlayerPosition = new Vec2(
                    Number(Object(player, "position"), "x"),
                    Number(Object(player, "position"), "y")),
                Omitted = (int)NumberOr(row, "omittedHostileProjectiles"),
            };
            threat.Projectiles = BuildProjectiles(row, "hostileProjectiles");
            threat.ProjectilesBeforeUpdate = BuildProjectiles(row,
                "hostileProjectilesBeforeUpdate");
            return threat;
        }

        private static float NearestGap(in PlayerMotionFrame player,
            TraceProjectile[] projectiles)
        {
            if (projectiles == null) return float.NaN;
            var nearest = float.MaxValue;
            for (var index = 0; index < projectiles.Length; index++)
            {
                var projectile = projectiles[index];
                if (projectile.Width <= 0 || projectile.Height <= 0) continue;
                var gapX = Math.Max(Math.Max(
                    player.Position.X - (projectile.Position.X +
                        projectile.Width),
                    projectile.Position.X - (player.Position.X +
                        player.Width)), 0f);
                var gapY = Math.Max(Math.Max(
                    player.Position.Y - (projectile.Position.Y +
                        projectile.Height),
                    projectile.Position.Y - (player.Position.Y +
                        player.Height)), 0f);
                var gap = Math.Max(gapX, gapY);
                if (gap < nearest) nearest = gap;
            }
            return nearest;
        }

        private static float BossGap(in PlayerMotionFrame player,
            in TraceThreat threat)
        {
            if (!threat.BossPresent) return float.NaN;
            var gapX = Math.Max(Math.Max(
                player.Position.X - (threat.BossPosition.X + threat.BossWidth),
                threat.BossPosition.X - (player.Position.X + player.Width)), 0f);
            var gapY = Math.Max(Math.Max(
                player.Position.Y - (threat.BossPosition.Y + threat.BossHeight),
                threat.BossPosition.Y - (player.Position.Y + player.Height)), 0f);
            return Math.Max(gapX, gapY);
        }

        private static TraceProjectile[] BuildProjectiles(
            Dictionary<string, object> row, string field)
        {
            var recorded = ArrayField(row, field);
            if (recorded == null) return null;
            var projectiles = new TraceProjectile[recorded.Length];
            for (var index = 0; index < recorded.Length; index++)
            {
                var entry = recorded[index] as Dictionary<string, object>;
                if (entry == null) continue;
                projectiles[index] = new TraceProjectile
                {
                    Slot = (int)Number(entry, "slot"),
                    Type = (int)Number(entry, "type"),
                    Position = new Vec2(Number(entry, "x"), Number(entry, "y")),
                    Velocity = new Vec2(Number(entry, "vx"),
                        Number(entry, "vy")),
                    Width = (int)Number(entry, "width"),
                    Height = (int)Number(entry, "height"),
                    Damage = (int)Number(entry, "damage"),
                    TimeLeft = (int)Number(entry, "timeLeft"),
                    ExtraUpdates = (int)Number(entry, "extraUpdates"),
                    Ai0 = Number(entry, "ai0"),
                    Ai1 = Number(entry, "ai1"),
                    // Tolerant on purpose: traces recorded before these fields
                    // existed are still worth calibrating against, and a missing
                    // key means the field was never captured rather than that it
                    // was zero. Reading it as zero keeps the older trace usable
                    // while the modelled path simply declines to model what it
                    // was not given.
                    Ai2 = NumberOr(entry, "ai2"),
                    LocalAi0 = NumberOr(entry, "localAI0"),
                    LocalAi1 = NumberOr(entry, "localAI1"),
                    Direction = (int)NumberOr(entry, "direction"),
                    Scale = NumberOr(entry, "scale"),
                };
            }
            return projectiles;
        }

        private static float NumberOr(Dictionary<string, object> entry,            string name)
        {
            return entry.ContainsKey(name) ? Number(entry, name) : 0f;
        }

        /// <summary>A numeric field that may simply be absent.
        ///
        /// The strict accessor throws on a missing key, and a trace row only
        /// carries the fields the engine had that tick, so a regime classifier
        /// built on it dies on the first row where a resource is not being used.
        /// </summary>
        private static float Field(Dictionary<string, object> entry, string name)
        {
            return Field(entry, name, null);
        }

        /// <summary>The same, one level down, for the nested blocks a row holds.
        /// </summary>
        private static float Field(Dictionary<string, object> entry, string name,
            string nested)
        {
            object raw;
            if (entry == null || !entry.TryGetValue(name, out raw)) return 0f;
            if (nested != null)
            {
                var inner = raw as Dictionary<string, object>;
                if (inner == null) return 0f;
                return Field(inner, nested);
            }
            if (raw == null) return 0f;
            if (!(raw is int) && !(raw is long) && !(raw is decimal) &&
                !(raw is double) && !(raw is float) && !(raw is bool)) return 0f;
            return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
        }

        private static float[] Floats(object[] values)
        {
            var result = new float[values.Length];
            for (var index = 0; index < values.Length; index++)
                result[index] = values[index] == null ? 0f
                    : Convert.ToSingle(values[index],
                        CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>Measures the forward model on a route the search produced,
        /// rather than on the route the recording took.
        ///
        /// Every fidelity number this project has is measured on the observed
        /// route, and the last three rounds showed what that is worth: the model
        /// is exact there, and every divergence that looked like a model failure
        /// turned out to be the engine's knockback. That says the model is right
        /// about the easy case. The search walks routes nobody recorded, and the
        /// evaluator gap that makes enumeration expensive is about those.
        ///
        /// The motion does not depend on the threat field, only on the controls
        /// and the player's own state, so the model can be walked along a route
        /// with no threat field at all and compared against the engine run of
        /// that same route. Frames are classified by the regime the engine was in,
        /// which is what turns a single error number into a list of places to
        /// work.
        /// </summary>
        // Zero, and it was tried at twenty-four first. Resyncing for a while after
        // the invulnerability window ends made the measurement worse, not better:
        // the air regime's mean went from nineteen hundred to eighty-eight hundred
        // and its maximum to fifteen thousand, which is wider than the arena. The
        // reason is that the knockback velocity is still decaying when the window
        // ends, so resyncing there hands the model a velocity it cannot reproduce
        // and the drift after that is counted as model error. Leaving this at zero
        // keeps the one number that has been checked against an independent path:
        // the wing regime's mean of a hundred and eighty-seven against the other
        // comparison's maximum of a hundred and eighty-seven.
        private const int KnockbackGrace = 0;

        private static int AuditRouteModel(string routePath, string tracePath,
            string startTickText)
        {
            if (!File.Exists(routePath) || !File.Exists(tracePath)) return 2;
            var actions = new List<RouteAction>();
            foreach (var line in File.ReadAllLines(routePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("direction",
                    StringComparison.Ordinal)) continue;
                var parts = trimmed.Split(',');
                if (parts.Length < 3) continue;
                actions.Add(new RouteAction
                {
                    Direction = int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Jump = parts[1] == "1",
                    Dash = parts[2] == "1",
                });
            }
            // The rebuilt route puts a dash bit on the frame before the engine's
            // dash delay turns negative, and the file demonstrably contains one on
            // frame seven hundred and thirty-three. The audit still read that
            // frame's dash component as clear, so the row it indexes is not the row
            // for that tick. Printing the parsed count, the opening rows and the
            // row at the index the window uses says where the two diverge instead
            // of leaving it to be reasoned about.
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL PARSE count={0} first={1},{2},{3} second={4},{5},{6} " +
                "at492={7},{8},{9}",
                actions.Count,
                actions.Count > 0 ? actions[0].Direction : 0,
                actions.Count > 0 && actions[0].Jump ? 1 : 0,
                actions.Count > 0 && actions[0].Dash ? 1 : 0,
                actions.Count > 1 ? actions[1].Direction : 0,
                actions.Count > 1 && actions[1].Jump ? 1 : 0,
                actions.Count > 1 && actions[1].Dash ? 1 : 0,
                actions.Count > 492 ? actions[492].Direction : 0,
                actions.Count > 492 && actions[492].Jump ? 1 : 0,
                actions.Count > 492 && actions[492].Dash ? 1 : 0));
            var rows = new List<Dictionary<string, object>>();
            var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (var line in File.ReadLines(tracePath))
            {
                if (line.Trim().Length == 0) continue;
                rows.Add(parser.DeserializeObject(line)
                    as Dictionary<string, object>);
            }
            if (rows.Count == 0) return 2;
            // Where the route starts is where the plugin first drove, and the
            // trace records that directly rather than leaving it to be inferred
            // from a tick offset.
            var startTick = -1;
            for (var index = 0; index < rows.Count; index++)
            {
                // Only the rows the plugin drove carry a replay frame, so the
                // lookup has to tolerate the key being absent. Reading it through
                // the strict accessor threw on the very first row and the whole
                // audit died before printing anything, with an exception whose
                // own ToString failed, which is a poor way to learn this.
                object rawPlan;
                if (!rows[index].TryGetValue("plan", out rawPlan)) continue;
                var plan = rawPlan as Dictionary<string, object>;
                object rawFrame;
                if (plan == null || !plan.TryGetValue("replayFrame", out rawFrame))
                    continue;
                if (Convert.ToInt32(rawFrame, CultureInfo.InvariantCulture) == 0)
                {
                    // One less than the row, because the walk computes its tick as
                    // this plus the step plus one. Setting it to the row itself put
                    // the whole route one frame late, and the symptom was a first
                    // frame where the engine stood still while the model dashed:
                    // that was the route's second action being compared against the
                    // engine's first. Two rounds of conclusions were drawn from it
                    // before the trace was scanned directly for the row where the
                    // replay counter reads zero, which is the measurement this is
                    // supposed to be.
                    startTick = index - 1;
                    break;
                }
            }
            if (startTick < 0) startTick = 0;
            // An explicit start overrides the trace's replay frame, which is what
            // lets the same audit run on a route built from the recording's own
            // controls. That comparison is the control: the per-tick audit already
            // reports a maximum error of zero on the observed route, so if this
            // tool reports thousands of pixels there, the tool is what is wrong
            // and the regime breakdown means nothing.
            int explicitStart;
            if (!string.IsNullOrEmpty(startTickText) &&
                int.TryParse(startTickText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out explicitStart) &&
                explicitStart >= 0 && explicitStart < rows.Count)
                startTick = explicitStart;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL AUDIT actions={0} rows={1} startTick={2}",
                actions.Count, rows.Count, startTick + 1));

            // The same floor the rest of this file uses: the highest surface the
            // player stands on during the opening rows, as position plus height.
            // Taking the first row's position instead put the floor far above the
            // ground, so the model never believed it was standing, and the audit
            // reported thousands of pixels of error on a route the other per-tick
            // comparison scores at a hundred and eighty-seven.
            // The floor has to be the actual ground, which is the lowest point the
            // player's bottom reaches anywhere in the trace. Taking it from the
            // opening rows instead gives the ground the fight starts on, and on a
            // frame where the player has descended even slightly below that, the
            // ground clamp fires while the player is airborne: it snaps the
            // position up to the floor and reports that displacement as the
            // velocity, which on the traced frame is minus thirty-nine point seven
            // five and is the entire vertical error in this audit.
            var floor = float.MinValue;
            for (var index = 0; index < rows.Count; index++)
                floor = Math.Max(floor,
                    Player(rows[index], "position", "y") +
                    Player(rows[index], "height"));
            var state = BuildFrame(rows[startTick], rows[startTick], floor, false);
            var names = new[] { "ground", "air", "wing", "rocket", "dash" };
            var frames = new int[names.Length];
            var over4 = new int[names.Length];
            var maxError = new float[names.Length];
            var sumError = new double[names.Length];
            var refused = 0;
            var knockbackFrames = 0;
            var graceUntil = -1;
            // Walking the trace can never validate a rule, because the model
            // diverges and the frames after that describe states the trace does not
            // contain. Open loop disables the resync so the two trajectories can be
            // compared tick by tick from a common origin.
            var openLoop = Environment.GetEnvironmentVariable(
                "CHAITE_AUDIT_OPENLOOP") == "1";
            var openLoopShown = 0;
            var sinceResync = 0;
            // The error as a function of how long the model has been running
            // since it was last put back on the engine's state. A uniform error
            // across regimes has two possible shapes and they mean different
            // things: a straight line says some term is biased every tick and can
            // be traced to it, a staircase says the fault is confined to
            // particular frames. Ten-tick buckets separate them.
            var bucketSum = new double[9];
            var bucketCount = new int[9];
            var bucketX = new double[9];
            var bucketY = new double[9];
            // Velocity error by regime, which is what localizes a per-tick bias:
            // a position error grows with time and hides where it came from, while
            // a velocity error points straight at the branch that produced it.
            var velX = new double[names.Length];
            var velY = new double[names.Length];
            var velAbsX = new double[names.Length];
            var velAbsY = new double[names.Length];
            var printed = new bool[names.Length];
            var previousNeutral = false;
            var previousEngineVx = 0f;
            var previousModelVx = 0f;
            var neutralCount = new int[2];
            var neutralEngine = new double[2];
            var neutralModel = new double[2];
            var overCount = 0;
            var overDelta = 0d;
            var overRatio = 0d;
            var airAccelCount = 0;
            var airAccelDelta = 0d;
            var previousRegime = -1;
            var previousEngineVy = 0f;
            var previousModelVy = 0f;
            var airVertCount = 0;
            var airVertEngine = 0d;
            var airVertModel = 0d;
            var airVertAbs = 0d;
            var airVertBig = 0;
            var rocketVertCount = 0;
            var rocketVertAbs = 0d;
            var adjacent = new int[names.Length];
            var walked = 0;
            var firstOver4 = -1;
            var firstOver4Regime = "";
            // The previous tick's velocities on both sides, so the per-tick step can
            // be compared instead of the levels.
            var previousEngineVX = 0f;
            var previousModelVX = 0f;
            var opposesBleed = new int[16];
            var opposesAccel = new int[16];
            var opposesMaxSpeed = 0f;
            var opposesShown = 0;
            for (var step = 0; step < actions.Count; step++)
            {
                var tick = startTick + step + 1;
                if (tick >= rows.Count) break;
                PlayerMotionFrame next;
                ForwardModelRefusal refusal;
                if (!PlayerForwardModel.TryAdvance(in state,
                        actions[step].ToControls(), out next, out refusal))
                {
                    refused++;
                    break;
                }
                state = next;
                walked++;
                var engine = BuildFrame(rows[tick], rows[tick], floor, false);
                var dx = state.Position.X - engine.Position.X;
                var dy = state.Position.Y - engine.Position.Y;
                var error = (float)Math.Sqrt(dx * dx + dy * dy);
                var player = Object(rows[tick], "player");
                // The engine's knockback is not something the forward model
                // reproduces or should, so a frame inside an invulnerability
                // window measures the knockback rather than the model. The
                // per-tick comparison elsewhere in this file learned that already
                // and gates on it; this one did not, and reported three thousand
                // pixels of error on a route that same comparison scores at a
                // hundred and eighty-seven.
                if (Field(player, "immuneTime") > 0f) graceUntil = tick + KnockbackGrace;
                if (tick <= graceUntil && !openLoop)
                {
                    // Resync rather than skip, and keep resyncing past the
                    // invulnerability window. A hit displaces the player and the
                    // model never recovers from it, so after the first hit the two
                    // are permanently apart and every later frame measures that
                    // one displacement. That is what produced a mean error of
                    // three thousand two hundred pixels with a maximum only eight
                    // per cent higher -- the signature of a constant offset, not
                    // of physics. Restarting the model from the engine's own state
                    // at each hit measures the segments between hits, which is
                    // what the model is actually for.
                    //
                    // The invulnerability counter alone is not the whole window:
                    // it expires while the knockback velocity is still decaying,
                    // so resyncing only on those rows left the drift to be counted
                    // as model error afterwards. That is the reading that still
                    // had the air regime at nineteen hundred pixels with a mean
                    // and maximum within a few per cent of each other.
                    knockbackFrames++;
                    state = BuildFrame(rows[tick], rows[tick], floor, false);
                    sinceResync = 0;
                    continue;
                }
                var regime = 0;
                // Open loop: print the two trajectories side by side for the first
                // stretch, so the rule is judged on the ticks before the model
                // diverges rather than on an aggregate that the divergence has
                // already swamped. The resync above is what hid this -- it restarts
                // the model from the engine's own state at every hit, so the
                // comparison never runs longer than one segment and the walk always
                // looks locally accurate.
                if (openLoop && openLoopShown < 120)
                {
                    openLoopShown++;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL OPENLOOP tick={0} engX={1:F4} modX={2:F4} " +
                        "engVX={3:F4} modVX={4:F4} engY={5:F4} modY={6:F4} " +
                        "engVY={7:F4} modVY={8:F4} err={9:F4} branch={10} " +
                        "in={11:F4} accRun={12:F4} top={13:F4} base={14:F4} " +
                        "dir={15} engDashDelay={16} engEoc={17} modDashDelay={18} " +
                        "modEoc={19} modDashing={20} engDashing={21}",
                        tick, engine.Position.X, state.Position.X,
                        engine.Velocity.X, state.Velocity.X,
                        engine.Position.Y, state.Position.Y,
                        engine.Velocity.Y, state.Velocity.Y, error,
                        HorizontalMotion.LastBranch,
                        HorizontalMotion.LastIncoming,
                        HorizontalMotion.LastAccRunSpeed,
                        HorizontalMotion.LastTopSpeed,
                        HorizontalMotion.LastBaseSpeed,
                        HorizontalMotion.LastDirection,
                        engine.Dash.DashDelay, engine.Dash.EocDash,
                        state.Dash.DashDelay, state.Dash.EocDash,
                        state.Dashing, engine.Dashing));
                }
                // The engine's own invariants, taken from the dash model rather
                // than guessed from the field names. DashDelay of minus one pairs
                // with EocDash at the contact window and means the dash is
                // running; zero with zero means ready; a positive delay is the
                // cooldown and EocDash tracks it as delay minus fifteen. So a
                // dash is running exactly when the delay is negative, which is
                // the opposite of what the first two versions of this assumed:
                // one read the dash type field, which is a constant, and the next
                // read the cooldown as the dash.
                if (Field(player, "dashDelay") < 0f) regime = 4;
                else if (Field(player, "rocketTime") > 0f) regime = 3;
                else if (Field(player, "wingTime") > 0f) regime = 2;
                else if (Field(player, "jump") > 0f ||
                    Field(player, "velocity", "y") != 0f) regime = 1;
                // Comparing the two velocities as levels conflates the per-tick
                // rule with everything already accumulated, and three repairs to
                // the per-tick rule were built on that reading and all moved the
                // aggregate by a fraction of a pixel. The delta is what identifies
                // the term: what each side changed by on this one tick.
                if (regime == 2 && tick >= 240 && tick <= 262 && tick > 240)
                {
                    var engineStep = engine.Velocity.X - previousEngineVX;
                    var modelStep = state.Velocity.X - previousModelVX;
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "ROUTE MODEL HDELTA tick={0} engineVX={1:F4} " +
                        "engineStep={2:F4} modelVX={3:F4} modelStep={4:F4} " +
                        "stepDelta={5:F4} left={6} right={7} jump={8} dir={9} " +
                        "grounded={10} wingTime={11:F1} accRun={12:F3} " +
                        "maxRun={13:F3} runAccel={14:F4} sprintAccel={15:F4} " +
                        "runSlowdown={16:F4} rowMaxRun={17:F3} " +
                        "rowBaseRun={18:F3} stateBaseRun={19:F3}",
                        tick, engine.Velocity.X, engineStep, state.Velocity.X,
                        modelStep, modelStep - engineStep,
                        Field(player, "controlLeft") > 0.5f,
                        Field(player, "controlRight") > 0.5f,
                        Field(player, "controlJump") > 0.5f,
                        (int)Field(player, "direction"), engine.Grounded,
                        engine.Flight.WingTime, Field(player, "accRunSpeed"),
                        state.MaxRunSpeed, state.RunAcceleration,
                        state.SprintAcceleration, state.RunSlowdown,
                        Field(player, "maxRunSpeed"),
                        Field(player, "baseRunSpeed"), state.BaseRunSpeed));
                }
                // When the input opposes the motion, does the engine bleed or does it
                // accelerate? Six consecutive frames all showed bleed, and a rule
                // built on that alone made everything worse because the model could
                // then never reverse -- so the engine does accelerate while opposing
                // under some condition those six frames do not straddle. This counts
                // both outcomes against the speed, so the boundary is read off the
                // trace instead of reasoned from whichever frames happen to be
                // printed.
                if (tick > 0)
                {
                    var vx = engine.Velocity.X;
                    var opposes = (vx > 0f && Field(player, "controlLeft") > 0.5f) ||
                        (vx < 0f && Field(player, "controlRight") > 0.5f);
                    if (opposes && Math.Abs(vx) > 0.01f)
                    {
                        var opposeStep = vx - previousEngineVX;
                        var bleedOnly = Math.Abs(opposeStep + 0.4512f) < 0.02f;
                        var slot = (int)Math.Min(15f, Math.Abs(vx));
                        opposesBleed[slot]++;
                        if (!bleedOnly) opposesAccel[slot]++;
                        if (Math.Abs(vx) > opposesMaxSpeed) opposesMaxSpeed =
                            Math.Abs(vx);
                        // The speed and the sign of the input do not separate the two
                        // outcomes -- two rules built on them were measured and both
                        // made the aggregate worse. So the fields that could separate
                        // them are printed next to the outcome, for the first few
                        // opposing frames, to be read rather than guessed at.
                        if (opposesShown < 40)
                        {
                            opposesShown++;
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "ROUTE MODEL OPPOSEFRAME tick={0} vx={1:F4} " +
                                "step={2:F4} bleedOnly={3} grounded={4} " +
                                "wingAccRun={5:F3} accRun={6:F3} maxRun={7:F3} " +
                                "runAccel={8:F4} sprintAccel={9:F4} " +
                                "runSlowdown={10:F4} jump={11} sliding={12} " +
                                "wingTime={13:F1} rocketTime={14:F1}",
                                tick, vx, opposeStep, bleedOnly, engine.Grounded,
                                Field(player, "wingAccRunSpeed"),
                                Field(player, "accRunSpeed"),
                                Field(player, "maxRunSpeed"),
                                Field(player, "runAcceleration"),
                                Field(player, "sprintAcceleration"),
                                Field(player, "runSlowdown"),
                                Field(player, "controlJump") > 0.5f,
                                Field(player, "sliding") > 0.5f,
                                engine.Flight.WingTime,
                                engine.Flight.RocketTime));
                        }
                    }
                }
                previousEngineVX = engine.Velocity.X;
                previousModelVX = state.Velocity.X;
                frames[regime]++;
                // The wing branch is where the horizontal error lives: three
                // hundred fifty-five frames with a mean of twenty-eight pixels
                // against a vertical component of zero. The first frame over four
                // pixels is tick two hundred fifty-one, so the window around the
                // origin is dumped with the engine's own velocity, the model's, and
                // the controls and constants each of them saw.
                if (regime == 2 && tick >= 240 && tick <= 262)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL HORIZ tick={0} engineVX={1:F4} modelVX={2:F4} " +
                        "delta={3:F4} left={4} right={5} jump={6} dir={7} " +
                        "grounded={8} wingTime={9:F1} wingMax={10:F1} " +
                        "accRun={11:F3} maxRun={12:F3} runAccel={13:F4} " +
                        "sprintAccel={14:F4} runSlowdown={15:F4} stateVX={16:F4} " +
                        "engineVY={17:F4} modelVY={18:F4}",
                        tick, engine.Velocity.X, state.Velocity.X,
                        state.Velocity.X - engine.Velocity.X,
                        Field(player, "controlLeft") > 0.5f,
                        Field(player, "controlRight") > 0.5f,
                        Field(player, "controlJump") > 0.5f,
                        (int)Field(player, "direction"), engine.Grounded,
                        engine.Flight.WingTime, engine.Flight.WingTimeMax,
                        Field(player, "accRunSpeed"), state.MaxRunSpeed,
                        state.RunAcceleration, state.SprintAcceleration,
                        state.RunSlowdown, state.Velocity.X,
                        engine.Velocity.Y, state.Velocity.Y));
                sumError[regime] += error;
                var bucket = sinceResync / 10;
                if (bucket > 8) bucket = 8;
                bucketSum[bucket] += error;
                bucketCount[bucket]++;
                bucketX[bucket] += Math.Abs(dx);
                bucketY[bucket] += Math.Abs(dy);
                var dvx = state.Velocity.X - engine.Velocity.X;
                var dvy = state.Velocity.Y - engine.Velocity.Y;
                // The dash that the engine starts on frame seven hundred and
                // thirty-four takes it to fourteen and a half in one tick, and the
                // model never gets there. Everything else about the horizontal
                // model has now been measured as correct -- the deceleration
                // constants, the ceiling, the airborne acceleration rate -- so the
                // loss is in this window, and this window separates the two ways it
                // could happen: the dash not starting in the model at all, or
                // starting without its speed being set.
                if (tick >= 728 && tick <= 744)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL DASHWIN tick={0} action=({1},{2},{3}) " +
                        "modelDash={4} modelVX={5:F4} engineDash={6} " +
                        "engineVX={7:F4} engineEocDash={8:F0} " +
                        "engineControlDash={9:F0}",
                        tick, actions[step].Direction,
                        actions[step].Jump ? 1 : 0, actions[step].Dash ? 1 : 0,
                        state.Dash.DashDelay, state.Velocity.X,
                        engine.Dash.DashDelay, engine.Velocity.X,
                        Field(player, "eocDash"), Field(player, "controlDash")));
                // The horizontal fault has the clearer signature: with no input at
                // all the model sheds two and nine tenths of speed per tick more
                // than the engine does. A velocity difference says that much; the
                // per-tick change across consecutive neutral frames says which
                // deceleration each side is actually using, which is what has to
                // be compared against the measured constants. Split by grounded,
                // because ground and air decelerate by different amounts.
                var neutral = actions[step].Direction == 0 && !actions[step].Jump &&
                    !actions[step].Dash;
                if (neutral && previousNeutral)
                {
                    var side = state.Grounded ? 0 : 1;
                    neutralCount[side]++;
                    neutralEngine[side] += engine.Velocity.X - previousEngineVx;
                    neutralModel[side] += state.Velocity.X - previousModelVx;
                    if (neutralCount[side] <= 8)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL NEUTRAL tick={0} grounded={1} " +
                            "engineVX={2:F4}->{3:F4} d={4:F4} " +
                            "modelVX={5:F4}->{6:F4} d={7:F4}",
                            tick, state.Grounded, previousEngineVx,
                            engine.Velocity.X,
                            engine.Velocity.X - previousEngineVx,
                            previousModelVx, state.Velocity.X,
                            state.Velocity.X - previousModelVx));
                }
                // The air branch's vertical error is now the only fault left: every
                // other branch is under one point one pixels per tick horizontally
                // and vertically, and air is at seven point seven seven. The error
                // is zero on the branch's first frame and accumulates inside it, so
                // it is an acceleration term. Comparing the per-tick change in
                // vertical speed on both sides says which term, against the
                // measured gravity and wing thrust constants.
                // Regime one is air. Zero is ground, which holds a single frame in
                // this trace, and testing zero here is what produced the previous
                // round's wrong conclusion: the counter reported one frame, that
                // was the ground frame, and the reading "air is almost never
                // adjacent" was drawn from it. The adjacency counter, which indexes
                // by the same table, reports three hundred adjacent pairs for air
                // and none for ground, which is what exposed the mistake.
                // The frame where the vertical offset appears, with the fields that
                // decide which vertical branch runs. Gravity is four tenths and a
                // jump impulse is a few pixels, so forty-three and a half is
                // neither: it is the shape of a thrust term, or of a value that was
                // never initialised.
                // The step that produced the offset, taken apart. The thrust
                // functions cannot reach minus forty-three, so the fault is in the
                // gravity term, the collision resolution, or an uninitialised
                // field. Rebuilding the state from the previous row and running the
                // sub-steps separately says which.
                if (tick == 1509)
                {
                    var before = BuildFrame(rows[tick - 1], rows[tick - 1], floor,
                        false);
                    var probeControls = ObservedControls(rows[tick - 1], rows[tick]);
                    PlayerMotionFrame stepped;
                    ForwardModelRefusal stepRefusal;
                    PlayerForwardModel.TryAdvance(in before, in probeControls,
                        out stepped, out stepRefusal);
                    var gravityOnly = before.Velocity.Y;
                    JumpMotion.ApplyGravityChecked(ref gravityOnly,
                        before.Gravity, before.MaxFallSpeed, false, false,
                        probeControls.Up, false);
                    var flightOnly = before.Velocity.Y;
                    var flightState = before.Flight;
                    var flightPhase = FlightMotion.ApplyAfterJump(ref flightState,
                        in before.Jump, ref flightOnly, probeControls.Jump,
                        probeControls.Up, probeControls.Down, before.Gravity,
                        before.MaxFallSpeed);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL VERTFLOOR tick={0} floorY={1:F4} " +
                        "positionY={2:F4} height={3:F2} floorMinusHeight={4:F4} " +
                        "advancedY={5:F4} advancedVY={6:F4} " +
                        "engineY={7:F4} engineHeight={8:F2} " +
                        "engineBottom={9:F4} modelBottom={10:F4} " +
                        "engineVY={11:F4} grounded={12}",
                        tick, before.FloorY, before.Position.Y, before.Height,
                        before.FloorY - before.Height, stepped.Position.Y,
                        stepped.Velocity.Y, engine.Position.Y, engine.Height,
                        engine.Position.Y + engine.Height,
                        stepped.Position.Y + before.Height, engine.Velocity.Y,
                        before.Grounded));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL VERTSTEP tick={0} beforeVY={1:F4} " +
                        "gravityOnly={2:F4} flightOnly={3:F4} flightPhase={4} " +
                        "advancedVY={5:F4} refusal={6} engineVY={7:F4} " +
                        "beforeWingTime={8:F1} beforeWingMax={9:F1} " +
                        "beforeRocket={10} beforeJumpKnown={11} " +
                        "beforeJumpSpeed={12:F3} beforeJumpRemaining={13:F0} " +
                        "beforeGrounded={14} beforeMaxFall={15:F2}",
                        tick, before.Velocity.Y, gravityOnly, flightOnly,
                        flightPhase, stepped.Velocity.Y, stepRefusal,
                        engine.Velocity.Y, before.Flight.WingTime,
                        before.Flight.WingTimeMax, before.RocketBoots,
                        before.Jump.Known, before.Jump.Speed,
                        before.Jump.RemainingTicks, before.Grounded,
                        before.MaxFallSpeed));
                }
                if (tick >= 1505 && tick <= 1513)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL VERTWIN tick={0} engineVY={1:F4} modelVY={2:F4} " +
                        "grounded={3} wingTimeMax={4:F1} wingTime={5:F1} " +
                        "rocketBoots={6} jumpKnown={7} jumpSpeed={8:F3} " +
                        "maxFall={9:F2} gravity={10:F3} action=({11},{12},{13})",
                        tick, engine.Velocity.Y, state.Velocity.Y,
                        engine.Grounded, engine.Flight.WingTimeMax,
                        engine.Flight.WingTime, engine.RocketBoots,
                        engine.Jump.Known, engine.Jump.Speed, state.MaxFallSpeed,
                        state.Gravity, actions[step].Direction,
                        actions[step].Jump ? 1 : 0, actions[step].Dash ? 1 : 0));
                // Rocket is index three in this table, not one and not two. The
                // branch is the last known vertical defect and the composed route
                // leans on it hard enough to put it on the critical path: the
                // source trace holds twenty-eight rocket frames and the probe of
                // the composed route holds a hundred and twenty-five.
                if (regime == 3)
                {
                    rocketVertCount++;
                    rocketVertAbs += Math.Abs(state.Velocity.Y - engine.Velocity.Y);
                    if (rocketVertCount <= 10)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL ROCKETVERT tick={0} " +
                            "engineVY={1:F4} modelVY={2:F4} abs={3:F4} " +
                            "prevEngineVY={4:F4} prevModelVY={5:F4} " +
                            "jump={6} wingTime={7:F0} rocketTime={8:F0} " +
                            "rocketDelay={9:F0} canRocket={10} " +
                            "rocketRelease={11} rocketBoots={12} " +
                            "modelWing={13:F1} modelRocketTime={14:F0} " +
                            "modelRocketDelay={15:F0} modelCanRocket={16}",
                            tick, engine.Velocity.Y, state.Velocity.Y,
                            Math.Abs(state.Velocity.Y - engine.Velocity.Y),
                            previousEngineVy, previousModelVy,
                            actions[step].Jump ? 1 : 0,
                            Field(player, "wingTime"), Field(player, "rocketTime"),
                            Field(player, "rocketDelay"),
                            Field(player, "canRocket") > 0.5f,
                            Field(player, "rocketRelease") > 0.5f,
                            Field(player, "rocketBoots") > 0.5f,
                            state.WingTime, state.Flight.RocketTime,
                            state.Flight.RocketDelay, state.Flight.CanRocket));
                }
                if (regime == 1 && previousRegime >= 0)
                {
                    airVertCount++;
                    airVertEngine += engine.Velocity.Y - previousEngineVy;
                    airVertModel += state.Velocity.Y - previousModelVy;
                    // The absolute difference as well as the change. The change
                    // locates an acceleration term inside the branch; the absolute
                    // figure says whether the error is a constant offset or
                    // something that grows with the frame count, which decides
                    // whether this branch is wrong or whether the offset is made
                    // upstream and merely shows up here.
                    airVertAbs += Math.Abs(state.Velocity.Y - engine.Velocity.Y);
                    // The branch's per-tick dynamics are exact, so the seven point
                    // seven seven is an offset introduced somewhere and carried.
                    // Printing the first frames where it is present, with the frame
                    // before them, says where it enters and which branch made it.
                    if (Math.Abs(state.Velocity.Y - engine.Velocity.Y) > 1f &&
                        airVertBig < 8)
                    {
                        airVertBig++;
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL AIRBIG tick={0} prevTick={1} " +
                            "prevRegime={2} regime={3} " +
                            "engineVY={4:F4} modelVY={5:F4} abs={6:F4} " +
                            "prevAbs={7:F4} jump={8} wingTime={9:F0} " +
                            "dashDelay={10:F0} rocketTime={11:F0}",
                            tick, tick - 1, names[previousRegime < 0 ? 0
                                : previousRegime], names[regime],
                            engine.Velocity.Y, state.Velocity.Y,
                            Math.Abs(state.Velocity.Y - engine.Velocity.Y),
                            Math.Abs(previousModelVy - previousEngineVy),
                            actions[step].Jump ? 1 : 0,
                            Field(player, "wingTime"), Field(player, "dashDelay"),
                            Field(player, "rocketTime")));
                    }
                    if (airVertCount <= 10)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL AIRVERT tick={0} " +
                            "engineVY={1:F4} modelVY={2:F4} abs={3:F4} " +
                            "jump={4} wingTime={5:F0} maxFall={6:F2} " +
                            "gravity={7:F3} modelWing={8:F0} modelGrav={9:F3}",
                            tick, engine.Velocity.Y, state.Velocity.Y,
                            Math.Abs(state.Velocity.Y - engine.Velocity.Y),
                            actions[step].Jump ? 1 : 0,
                            Field(player, "wingTime"), Field(player, "maxFallSpeed"),
                            Field(player, "gravity"), state.WingTime,
                            state.Gravity));
                }
                if (previousRegime == regime) adjacent[regime]++;
                previousRegime = regime;
                previousEngineVy = engine.Velocity.Y;
                previousModelVy = state.Velocity.Y;
                // What the engine does above the acceleration-run speed has to be
                // measured, not assumed, because the model treats that speed as a
                // hard ceiling and decays past it while the engine holds speeds
                // well above it. The dash decay already measured is multiplicative
                // and switches at twelve, so the ratio here says whether the same
                // rule governs ordinary running.
                var accRun = Field(player, "accRunSpeed");
                // Whether the engine builds speed in the air without wings. The
                // model's acceleration branch requires grounded or a sprint-in-air
                // flag, and the frame builder sets that flag from the wing logic
                // alone, so with no wings the model cannot build speed aloft and
                // sits near the base run speed while the engine runs at seven and a
                // half. Reading the trace's own airborne aligned-input frames
                // settles whether the flag is wrong rather than assuming it.
                if (!engine.Grounded && Field(player, "wingTime") <= 0f &&
                    actions[step].Direction != 0 && previousEngineVx != 0f &&
                    Math.Sign(actions[step].Direction) ==
                        Math.Sign(previousEngineVx) &&
                    Math.Abs(previousEngineVx) < accRun)
                {
                    airAccelCount++;
                    airAccelDelta += engine.Velocity.X - previousEngineVx;
                    if (airAccelCount <= 8)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL AIRACCEL tick={0} engineVX={1:F4}->{2:F4} " +
                            "d={3:F4} accRun={4:F2} runAccel={5:F4} " +
                            "wingTime={6:F0} wingsLogic={7:F0} input={8}",
                            tick, previousEngineVx, engine.Velocity.X,
                            engine.Velocity.X - previousEngineVx, accRun,
                            Field(player, "runAcceleration"),
                            Field(player, "wingTime"), Field(player, "wingsLogic"),
                            actions[step].Direction));
                }
                if (accRun > 0f && Math.Abs(previousEngineVx) > accRun)
                {
                    var ratio = previousEngineVx == 0f ? 0d
                        : engine.Velocity.X / previousEngineVx;
                    overCount++;
                    overDelta += engine.Velocity.X - previousEngineVx;
                    overRatio += ratio;
                    if (overCount <= 10)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "ROUTE MODEL OVERCAP tick={0} grounded={1} " +
                            "accRunSpeed={2:F2} engineVX={3:F4}->{4:F4} " +
                            "d={5:F4} ratio={6:F6} input={7}",
                            tick, state.Grounded, accRun,
                            previousEngineVx, engine.Velocity.X,
                            engine.Velocity.X - previousEngineVx, ratio,
                            actions[step].Direction));
                }
                previousEngineVx = engine.Velocity.X;
                previousModelVx = state.Velocity.X;
                // The first frame of each regime, with both sides' fields. The
                // per-regime average says which branch is wrong; this says which
                // term inside it, without inference. The air branch's vertical
                // error is seven and three quarter pixels a tick and the engine's
                // two terminal fall speeds differ by six and two thirds, so the
                // down key and the fall cap are the first things to read.
                if (!printed[regime])
                {
                    printed[regime] = true;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "ROUTE MODEL FIRST {0} tick={1} action=({2},{3},{4}) " +
                        "dVX={5:F4} dVY={6:F4} modelVX={7:F4} modelVY={8:F4} " +
                        "engineVX={9:F4} engineVY={10:F4} " +
                        "down={11} up={12} jump={13} maxFall={14:F2} " +
                        "gravity={15:F3} wingTime={16} dashDelay={17} " +
                        "eocDash={18} dashTime={19} rocketTime={20} " +
                        "engineMaxFall={21:F2} engineGravity={22:F3} " +
                        "frameMaxRun={23:F4} frameBaseRun={24:F4} " +
                        "frameSprint={25:F4} engineAccRun={26:F2} " +
                        "engineMaxRun={27:F2}",
                        names[regime], tick, actions[step].Direction,
                        actions[step].Jump ? 1 : 0, actions[step].Dash ? 1 : 0,
                        dvx, dvy, state.Velocity.X, state.Velocity.Y,
                        engine.Velocity.X, engine.Velocity.Y,
                        Field(player, "controlDown"), Field(player, "controlUp"),
                        Field(player, "controlJump"),
                        state.MaxFallSpeed, state.Gravity, state.WingTime,
                        state.Dash.DashDelay, state.Dash.EocDash,
                        state.Dash.DashTime, Field(player, "rocketTime"),
                        Field(player, "maxFallSpeed"), Field(player, "gravity"),
                        engine.MaxRunSpeed, engine.BaseRunSpeed,
                        engine.SprintAcceleration, Field(player, "accRunSpeed"),
                        Field(player, "maxRunSpeed")));
                }
                velX[regime] += dvx;
                velY[regime] += dvy;
                velAbsX[regime] += Math.Abs(dvx);
                velAbsY[regime] += Math.Abs(dvy);
                sinceResync++;
                if (error > maxError[regime]) maxError[regime] = error;
                if (error > 4f)
                {
                    over4[regime]++;
                    if (firstOver4 < 0)
                    {
                        firstOver4 = tick;
                        firstOver4Regime = names[regime];
                    }
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL AUDIT walked={0} refused={1} firstOver4Tick={2} " +
                "firstOver4Regime={3} knockbackFrames={4}", walked, refused,
                firstOver4, firstOver4Regime, knockbackFrames));
            // Which horizontal case ran, and how fast the model ever got. Two
            // repairs to the ordering of those cases were built and both left the
            // error unchanged, so the counts are what says whether the case they
            // touched is even reachable from this route.
            Console.WriteLine("ROUTE MODEL HBRANCH " +
                HorizontalMotion.BranchSummary());
            Console.WriteLine("ROUTE MODEL HSPEED " +
                PlayerForwardModel.SpeedSummary());
            for (var slot = 0; slot < opposesBleed.Length; slot++)
            {
                if (opposesBleed[slot] == 0) continue;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL OPPOSE speed={0}-{1} frames={2} bleedOnly={3} " +
                    "other={4}", slot, slot + 1, opposesBleed[slot],
                    opposesBleed[slot] - opposesAccel[slot], opposesAccel[slot]));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL OPPOSE maxSpeed={0:F4}", opposesMaxSpeed));
            for (var index = 0; index < names.Length; index++)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL REGIME {0} frames={1} over4={2} mean={3:F3} " +
                    "max={4:F3}", names[index], frames[index], over4[index],
                    frames[index] == 0 ? 0d : sumError[index] / frames[index],
                    maxError[index]));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL VELOCITY {0} frames={1} dVX={2:F4} dVY={3:F4} " +
                    "absVX={4:F4} absVY={5:F4}", names[index], frames[index],
                    frames[index] == 0 ? 0d : velX[index] / frames[index],
                    frames[index] == 0 ? 0d : velY[index] / frames[index],
                    frames[index] == 0 ? 0d : velAbsX[index] / frames[index],
                    frames[index] == 0 ? 0d : velAbsY[index] / frames[index]));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL AIRVERT_SUMMARY frames={0} engineDVY={1:F4} " +
                "modelDVY={2:F4} absVY={3:F4}", airVertCount,
                airVertCount == 0 ? 0d : airVertEngine / airVertCount,
                airVertCount == 0 ? 0d : airVertModel / airVertCount,
                airVertCount == 0 ? 0d : airVertAbs / airVertCount));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL ROCKETVERT_SUMMARY frames={0} absVY={1:F4}",
                rocketVertCount,
                rocketVertCount == 0 ? 0d : rocketVertAbs / rocketVertCount));
            for (var index = 0; index < names.Length; index++)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL ADJACENT {0} frames={1} adjacentPairs={2}",
                    names[index], frames[index], adjacent[index]));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL AIRACCEL_SUMMARY frames={0} meanDelta={1:F4}",
                airAccelCount,
                airAccelCount == 0 ? 0d : airAccelDelta / airAccelCount));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROUTE MODEL OVERCAP_SUMMARY frames={0} meanDelta={1:F4} " +
                "meanRatio={2:F6}", overCount,
                overCount == 0 ? 0d : overDelta / overCount,
                overCount == 0 ? 0d : overRatio / overCount));
            for (var side = 0; side < 2; side++)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL NEUTRAL_SUMMARY {0} frames={1} " +
                    "engineDVX={2:F4} modelDVX={3:F4}",
                    side == 0 ? "ground" : "air", neutralCount[side],
                    neutralCount[side] == 0 ? 0d
                        : neutralEngine[side] / neutralCount[side],
                    neutralCount[side] == 0 ? 0d
                        : neutralModel[side] / neutralCount[side]));
            }
            for (var index = 0; index < bucketSum.Length; index++)            {
                if (bucketCount[index] == 0) continue;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "ROUTE MODEL SINCE_RESYNC {0}-{1} frames={2} mean={3:F3} " +
                    "meanX={4:F3} meanY={5:F3}",
                    index * 10, index == 8 ? "more" : (index * 10 + 9).ToString(
                        CultureInfo.InvariantCulture), bucketCount[index],
                    bucketSum[index] / bucketCount[index],
                    bucketX[index] / bucketCount[index],
                    bucketY[index] / bucketCount[index]));
            }
            return 0;
        }
    }
}
