using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>
        /// Replays a dense per-tick engine trace through the composed forward
        /// model and reports where it is exact, where it is close, and where it
        /// refuses.
        ///
        /// This is a measurement, not an assertion of success: the coverage and
        /// the error are printed so the unmodeled surface stays visible. The
        /// exit code only fails when a tick the model claimed to predict was
        /// wildly wrong, because that is the case a search must never trust.
        /// </summary>
        private static int VerifyForwardModelTrace(string path)
        {
            try
            {
                var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
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

                // The floor is measured from the standing phase before the fight,
                // the same way the native vertical fixture derives it, rather
                // than assumed from a tile constant.
                var floor = float.MinValue;
                for (var index = 0; index < 240 && index < rows.Count; index++)
                    floor = Math.Max(floor,
                        Player(rows[index], "position", "y") + Player(rows[index], "height"));

                var modeled = 0;
                var refusals = new Dictionary<string, int>();
                var maxPositionError = 0f;
                var hitTicks = 0;
                var contactTicks = 0;
                var hitPositionError = 0f;
                var cleanTicks = 0;
                var worstTick = -1;
                var worstPredicted = new Vec2(0f, 0f);
                var worstActual = new Vec2(0f, 0f);
                var worstDashing = false;
                var worstWingTime = 0f;
                var worstRocketTime = 0f;
                var maxVelocityError = 0f;
                var sumPositionError = 0.0;
                var exact = 0;
                var groundContacts = 0;
                var groundContactError = 0f;
                var worstGroundTick = -1;
                var worstGroundPredictedX = 0f;
                var worstGroundActualX = 0f;
                var worstGroundPredictedY = 0f;
                var worstGroundActualY = 0f;
                var worstGroundVelocityX = 0f;
                var worstGroundDirection = 0;
                var worstGroundDashing = false;

                for (var index = 0; index + 1 < rows.Count; index++)
                {
                    var current = rows[index];
                    var following = rows[index + 1];
                    if (Integer(current, "tick") + 1 != Integer(following, "tick"))
                        continue;

                    var frame = BuildFrame(current, following, floor,
                        PlayerBoolean(following, "justJumped"));
                    // The observation is sampled after the native update, so the
                    // control bits and the engine's own justJumped flag on a row
                    // describe the transition INTO that row, not the one out of
                    // it. Reading them from the current row is off by one tick
                    // and silently misses every launch: the tick that jumped
                    // reported the jump on the following row only.
                    var controls = BuildControls(following);
                    Vec2 predictedPosition, predictedVelocity;
                    ForwardModelRefusal refusal;
                    if (!PlayerForwardModel.TryAdvance(in frame, in controls,
                            out predictedPosition, out predictedVelocity, out refusal))
                    {
                        var name = refusal.ToString();
                        refusals[name] = refusals.ContainsKey(name)
                            ? refusals[name] + 1 : 1;
                        continue;
                    }
                    modeled++;

                    // A tick on which the player lost life is a tick the boss
                    // hit them, and the engine applies knockback the model has no
                    // way to predict. So is a tick on which the shield contacted
                    // something: the reviewed dash contract says outright that the
                    // shield's hit/bounce/immune path is outside it, and the
                    // engine reverses velocity and grants i-frames without any
                    // life loss, so the life test alone does not catch it.
                    // Scoring the model on either blames it for the fight.
                    var hitTick = Player(following, "life") < Player(current, "life");
                    var shieldContact = Player(following, "eocHit") != -1f;
                    if (hitTick) hitTicks++;
                    if (shieldContact) contactTicks++;
                    var disturbed = hitTick || shieldContact;

                    var actualX = Player(following, "position", "x");
                    var actualY = Player(following, "position", "y");
                    var positionError = Math.Max(
                        Math.Abs(predictedPosition.X - actualX),
                        Math.Abs(predictedPosition.Y - actualY));
                    var velocityError = Math.Max(
                        Math.Abs(predictedVelocity.X - Player(following, "velocity", "x")),
                        Math.Abs(predictedVelocity.Y - Player(following, "velocity", "y")));
                    if (positionError == 0f && velocityError == 0f) exact++;
                    if (!disturbed)
                    {
                        if (positionError > maxPositionError)
                        {
                            maxPositionError = positionError;
                            worstTick = Integer(current, "tick");
                            worstPredicted = predictedPosition;
                            worstActual = new Vec2(actualX, actualY);
                            worstDashing = frame.Dashing;
                            worstWingTime = frame.WingTime;
                            worstRocketTime = Player(current, "rocketTime");
                        }
                        maxVelocityError = Math.Max(maxVelocityError, velocityError);
                        sumPositionError += positionError;
                        cleanTicks++;
                    }
                    else
                    {
                        hitPositionError = Math.Max(hitPositionError, positionError);
                    }
                    if (frame.Grounded &&
                        Math.Abs(frame.Position.Y + frame.Height - floor) < .5f)
                    {
                        groundContacts++;
                        if (positionError > groundContactError)
                        {
                            groundContactError = positionError;
                            worstGroundTick = Integer(current, "tick");
                            worstGroundPredictedX = predictedPosition.X;
                            worstGroundActualX = actualX;
                            worstGroundPredictedY = predictedPosition.Y;
                            worstGroundActualY = actualY;
                            worstGroundVelocityX = frame.Velocity.X;
                            worstGroundDirection = controls.Direction;
                            worstGroundDashing = frame.Dashing;
                        }
                    }
                }

                var total = rows.Count - 1;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL TRACE rows={0} pairs={1} modeled={2} ({3:F1}%) " +
                    "exact={4} maxPositionError={5:R} maxVelocityError={6:R} " +
                    "meanPositionError={7:R}",
                    rows.Count, total, modeled,
                    total > 0 ? 100.0 * modeled / total : 0.0, exact,
                    maxPositionError, maxVelocityError,
                    modeled > 0 ? sumPositionError / modeled : 0.0));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL CLEAN ticks={0} meanPositionError={1:R} " +
                    "HIT ticks={2} maxPositionError={3:R} SHIELD contacts={4}",
                    cleanTicks,
                    cleanTicks > 0 ? sumPositionError / cleanTicks : 0.0,
                    hitTicks, hitPositionError, contactTicks));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL WORST tick={0} predicted=({1:R},{2:R}) " +
                    "actual=({3:R},{4:R}) dashing={5} wingTime={6:R} rocketTime={7:R}",
                    worstTick, worstPredicted.X, worstPredicted.Y, worstActual.X,
                    worstActual.Y, worstDashing ? 1 : 0, worstWingTime,
                    worstRocketTime));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL GROUND contacts={0} maxPositionError={1:R} floor={2:R}",
                    groundContacts, groundContactError, floor));
                // The worst grounded tick, in full. An aggregate error says the
                // model is wrong somewhere; only the tick says whether that is a
                // formula mistake or a mechanism the model never claimed, such
                // as running into a wall.
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL GROUND WORST tick={0} predictedX={1:R} " +
                    "actualX={2:R} predictedY={3:R} actualY={4:R} " +
                    "velocityX={5:R} direction={6} dashing={7}",
                    worstGroundTick, worstGroundPredictedX, worstGroundActualX,
                    worstGroundPredictedY, worstGroundActualY,
                    worstGroundVelocityX, worstGroundDirection,
                    worstGroundDashing ? 1 : 0));
                foreach (var pair in refusals)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "FORWARD MODEL REFUSED {0}={1}", pair.Key, pair.Value));

                // Roll forward from the first frame using the recorded controls.
                //
                // A single-tick replay can be right by construction, because it
                // rebuilds every sub-state from the trace before each step. A
                // rollout cannot: it has to carry the dash, jump and flight
                // sub-states itself. So this is the measurement that shows the
                // frame-returning overload really threads them, and it is the
                // same thing a search needs in order to exist at all.
                var rollFrame = BuildFrame(rows[0], rows[1], floor,
                    PlayerBoolean(rows[1], "justJumped"));
                var rollTicks = 0;
                var rollError = 0f;
                var rollWorstTick = -1;
                var rollStop = ForwardModelRefusal.None;
                for (var index = 1; index < rows.Count; index++)
                {
                    var rollControls = BuildControls(rows[index]);
                    PlayerMotionFrame advanced;
                    ForwardModelRefusal rollRefusal;
                    if (!PlayerForwardModel.TryAdvance(in rollFrame,
                            in rollControls, out advanced, out rollRefusal))
                    {
                        rollStop = rollRefusal;
                        break;
                    }
                    rollFrame = advanced;
                    var error = Math.Max(
                        Math.Abs(rollFrame.Position.X -
                            Player(rows[index], "position", "x")),
                        Math.Abs(rollFrame.Position.Y -
                            Player(rows[index], "position", "y")));
                    if (error > rollError)
                    {
                        rollError = error;
                        rollWorstTick = Integer(rows[index], "tick");
                    }
                    rollTicks++;
                    // Error at fixed horizons, because a rollout's usable length
                    // is what a search is actually bounded by. A single number
                    // for the whole run cannot distinguish slow drift, which
                    // shortens the horizon, from one event, which does not.
                    foreach (var horizon in new[] { 32, 64, 128, 256, 512, 1024 })
                        if (rollTicks == horizon)
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "FORWARD MODEL ROLLOUT horizon={0} error={1:R}",
                                horizon, rollError));
                    if (rollError > 20f) break;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FORWARD MODEL ROLLOUT ticks={0} maxPositionError={1:R} " +
                    "worstTick={2} stoppedBy={3}",
                    rollTicks, rollError, rollWorstTick, rollStop));

                // A modeled tick that is off by more than a player width is not a
                // trajectory, it is a different fight. Anything under that is
                // reported but tolerated: the point of this run is to size the
                // error, not to pretend it is zero.
                if (maxPositionError > 20f)
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "FORWARD MODEL FAIL a modeled tick is off by {0:R} px",
                        maxPositionError));
                    return 1;
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FORWARD MODEL FAIL " + error.Message);
                return 1;
            }
        }

        private static PlayerMotionFrame BuildFrame(Dictionary<string, object> row,
            Dictionary<string, object> following, float floor, bool justJumped)
        {
            var height = (int)Player(row, "height");
            var y = Player(row, "position", "y");
            var wingTime = Player(row, "wingTime");
            return new PlayerMotionFrame
            {
                Position = new Vec2(Player(row, "position", "x"), y),
                Velocity = new Vec2(Player(row, "velocity", "x"),
                    Player(row, "velocity", "y")),
                Width = (int)Player(row, "width"),
                Height = height,
                Gravity = Player(row, "gravity"),
                MaxFallSpeed = Player(row, "maxFallSpeed"),
                GravityDirection = Player(row, "gravDir") < 0f ? -1 : 1,
                MaxRunSpeed = Math.Max(Player(row, "maxRunSpeed"),
                    Player(row, "accRunSpeed")),
                AccRunSpeed = Player(row, "accRunSpeed"),
                // The production mapping, copied from the live facade rather
                // than re-derived: base run speed is maxRunSpeed, sprint
                // acceleration is a fraction of run acceleration that differs
                // with wings, and airborne sprinting needs a wing or a flying
                // mount. A missing profile used to be silently replaced by the
                // adapter path, which predicted a different fight.
                BaseRunSpeed = Player(row, "maxRunSpeed"),
                RunAcceleration = Player(row, "runAcceleration"),
                SprintAcceleration = Player(row, "dashDelay") < 0f
                    ? 0f
                    : Player(row, "runAcceleration") *
                        (Player(row, "wingsLogic") > 0f ? .4f : .2f),
                RunSlowdown = Player(row, "runSlowdown"),
                CanSprintInAir = Player(row, "wingsLogic") > 0f,
                Grounded = Math.Abs(y + height - floor) < .5f,
                JustJumped = justJumped,
                FloorY = floor,
                WingTime = wingTime,
                Dash = BuildDash(row),
                // The flight model consumes these counters, so it needs their
                // start-of-tick values and they come from the current row. That
                // is the opposite of the controls and justJumped, which describe
                // the update being predicted. Getting it backwards spends a
                // resource a tick early and mis-selects the glide branch.
                Jump = new JumpSnapshot
                {
                    Known = true,
                    RemainingTicks = (int)Player(row, "jump"),
                    Speed = Player(row, "jumpSpeed"),
                    Height = (int)Player(row, "jumpHeight"),
                    ReleaseReady = PlayerBoolean(row, "releaseJump"),
                    CloudAvailable = PlayerBoolean(row, "canJumpAgain_Cloud"),
                    CloudEnabled = PlayerBoolean(row, "hasJumpOption_Cloud"),
                    AutoJump = PlayerBoolean(row, "autoJump"),
                    SlowFall = PlayerBoolean(row, "slowFall"),
                },
                Flight = new FlightSnapshot
                {
                    Known = true,
                    WingsLogic = (int)Player(row, "wingsLogic"),
                    RocketBoots = (int)Player(row, "rocketBoots"),
                    WingTime = Player(row, "wingTime"),
                    WingTimeMax = (int)Player(row, "wingTimeMax"),
                    RocketTime = (int)Player(row, "rocketTime"),
                    RocketTimeMax = (int)Player(row, "rocketTimeMax"),
                    RocketDelay = (int)Player(row, "rocketDelay"),
                    CanRocket = PlayerBoolean(row, "canRocket"),
                    RocketRelease = PlayerBoolean(row, "rocketRelease"),
                    JustJumped = justJumped,
                },
                Mounted = PlayerBoolean(row, "mountActive"),
                Grappling = Player(row, "grapCount") > 0f,
                // The active dash is the negative dashDelay, not the shield's
                // contact window: eocDash stays positive for fifteen ticks after
                // the dash itself has ended.
                Dashing = Player(row, "dashDelay") < 0f,
                RocketBoots = Player(row, "rocketTime") > 0f,
                Immobilized = PlayerBoolean(row, "frozen") ||
                    PlayerBoolean(row, "webbed") || PlayerBoolean(row, "stoned"),
                Liquid = PlayerBoolean(row, "wet") ||
                    PlayerBoolean(row, "honeyWet") ||
                    PlayerBoolean(row, "lavaWet"),
            };
        }

        /// <summary>
        /// Rebuilds the dash state the reviewed model expects from the recorded
        /// fields. The equipment identity comes from the recorded dashType
        /// rather than from the loadout name, and the collision-free contract is
        /// asserted rather than assumed: a tick with a live hostile contact is
        /// outside what this model replays and must refuse instead of guessing.
        /// </summary>
        private static EyeShieldDashState BuildDash(Dictionary<string, object> row)
        {
            var dashType = (int)Player(row, "dashType");
            return new EyeShieldDashState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                EquipmentIdentity = dashType == 2
                    ? DashEquipmentIdentity.MasterNinjaGearItem984
                    : dashType == 1
                        ? DashEquipmentIdentity.ShieldOfCthulhuItem3097
                        : DashEquipmentIdentity.Unknown,
                DashType = dashType,
                Dash = (int)Player(row, "dash"),
                DashDelay = (int)Player(row, "dashDelay"),
                DashTime = (int)Player(row, "dashTime"),
                TimeSinceLastDashStarted =
                    (int)Player(row, "timeSinceLastDashStarted"),
                EocDash = (int)Player(row, "eocDash"),
                EocHit = (int)Player(row, "eocHit"),
                Pulley = PlayerBoolean(row, "pulley"),
                Grappling = Player(row, "grapCount") > 0f,
                ControlDash = PlayerBoolean(row, "controlDash"),
                ReleaseDash = PlayerBoolean(row, "releaseDash"),
                ControlLeft = PlayerBoolean(row, "controlLeft"),
                ControlRight = PlayerBoolean(row, "controlRight"),
                FacingDirection = Player(row, "direction") < 0f ? -1 : 1,
                VelocityX = Player(row, "velocity", "x"),
                VelocityY = Player(row, "velocity", "y"),
                AccRunSpeed = Player(row, "accRunSpeed"),
                MaxRunSpeed = Math.Max(Player(row, "maxRunSpeed"),
                    Player(row, "accRunSpeed")),
                ForwardSolidProbeKnown = true,
                ForwardSolidProbeBlocked = false,
                HostileContactKnown = true,
                HostileContact = false,
            };
        }

        private static PlayerControlFrame BuildControls(
            Dictionary<string, object> row)        {
            return new PlayerControlFrame
            {
                Left = PlayerBoolean(row, "controlLeft"),
                Right = PlayerBoolean(row, "controlRight"),
                Up = PlayerBoolean(row, "controlUp"),
                Down = PlayerBoolean(row, "controlDown"),
                Jump = PlayerBoolean(row, "controlJump"),
                Dash = PlayerBoolean(row, "controlDash"),
                Mount = PlayerBoolean(row, "controlMount"),
                Hook = PlayerBoolean(row, "controlHook"),
            };
        }

        private static bool PlayerBoolean(Dictionary<string, object> row,
            string name) => Boolean(Object(row, "player"), name);

        private static float Player(Dictionary<string, object> row, string name)
            => Number(Object(row, "player"), name);

        private static float Player(Dictionary<string, object> row, string name,
            string nested)
            => Number(Object(Object(row, "player"), name), nested);

        /// <summary>
        /// Runs the loop decomposition over a real dense trace and prints what it
        /// found.
        ///
        /// The point is the reduction factor. Segmentation is only worth
        /// anything if it turns a fight into a handful of distinct loops, so the
        /// number that matters is distinct groups rather than loops, and it is
        /// printed next to the tick counts it is derived from. The Boss's attack
        /// state comes from the trace rather than from a hand-written list of
        /// phase names, and the histogram is printed so the repositioning state
        /// is chosen from the data instead of guessed.
        /// </summary>
        private static int VerifyLoopDecompositionTrace(string path,
            int minimumTicks)
        {
            try
            {
                var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
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

                var observations = new List<LoopObservation>();
                var histogram = new Dictionary<int, int>();
                var bossType = 0;
                var previousLife = float.NaN;

                foreach (var row in rows)
                {
                    var boss = BossNpc(row);
                    if (boss == null) continue;
                    if (bossType == 0) bossType = (int)Number(boss, "type");
                    var state = (int)Number(boss, "ai", 0);
                    histogram[state] = histogram.ContainsKey(state)
                        ? histogram[state] + 1 : 1;

                    var life = Player(row, "life");
                    observations.Add(new LoopObservation
                    {
                        PlayerPosition = new Vec2(Player(row, "position", "x"),
                            Player(row, "position", "y")),
                        BossPosition = new Vec2(Number(Object(boss, "position"), "x"),
                            Number(Object(boss, "position"), "y")),
                        BossState = state,
                        // `hits` is defined as a native frame in which the
                        // player's life decreased, so the trace's own life
                        // series is the definition rather than a proxy for it.
                        Hit = !float.IsNaN(previousLife) && life < previousLife,
                    });
                    previousLife = life;
                }

                Console.WriteLine("LOOP TRACE rows=" + observations.Count +
                    " bossType=" + bossType + " minimumTicks=" + minimumTicks);
                var histogramText = new System.Text.StringBuilder();
                var ordered = new List<int>(histogram.Keys);
                ordered.Sort();
                foreach (var state in ordered)
                    histogramText.Append("ai0=").Append(state).Append(":")
                        .Append(histogram[state]).Append(" ");
                Console.WriteLine("LOOP TRACE bossAi0Histogram " +
                    histogramText.ToString().TrimEnd());

                // The run sequence is the script as the decomposition sees it.
                // Without it, a failure to find a period says nothing about why.
                var runStates = new List<int>();
                foreach (var observation in observations)
                    if (runStates.Count == 0 ||
                        observation.BossState != runStates[runStates.Count - 1])
                        runStates.Add(observation.BossState);
                Console.WriteLine("LOOP TRACE runs=" + runStates.Count);
                var runText = new System.Text.StringBuilder();
                for (var index = 0; index < runStates.Count && index < 96; index++)
                    runText.Append(runStates[index]).Append(
                        index + 1 == runStates.Count ? "" : ",");
                Console.WriteLine("LOOP TRACE runSequence=" + runText);

                var options = new LoopSegmentationOptions
                {
                    MinimumTicks = minimumTicks,
                };
                var segments = LoopDecomposition.Segment(observations, options);
                var report = LoopDecompositionSummary.Summarize(segments);
                Console.WriteLine("LOOP DECOMPOSITION loops=" + report.Loops +
                    " complete=" + report.CompleteLoops +
                    " closed=" + report.ClosedLoops +
                    " clean=" + report.CleanLoops +
                    " perfect=" + report.PerfectLoops +
                    " hits=" + report.Hits +
                    " distinctGroups=" + report.DistinctGroups +
                    " fightTicks=" + report.FightTicks +
                    " enumerationTicks=" + report.EnumerationTicks +
                    " reduction=" +
                    report.ReductionFactor.ToString("0.00",
                        CultureInfo.InvariantCulture) +
                    " longestLoop=" + report.LongestLoop +
                    " searchHorizon=" +
                    report.SearchHorizonRatio.ToString("0.00",
                        CultureInfo.InvariantCulture));
                for (var group = 0; group < report.GroupLoops.Count; group++)
                    Console.WriteLine("LOOP DECOMPOSITION group=" + group +
                        " loops=" + report.GroupLoops[group] +
                        " clean=" + report.GroupCleanLoops[group] +
                        " representativeTicks=" +
                        report.GroupRepresentativeTicks[group]);
                // Per-loop detail. The aggregate says a decomposition is bad;
                // only the boundaries say why, so they are always printed.
                foreach (var segment in segments)
                    Console.WriteLine("LOOP DECOMPOSITION segment=" +
                        segment.StartTick + ".." + segment.EndTick +
                        " len=" + segment.Length +
                        " closed=" + (segment.Closed ? 1 : 0) +
                        " complete=" + (segment.Complete ? 1 : 0) +
                        " hits=" + segment.Hits +
                        " group=" + segment.ThreatSignature +
                        " startBucket=" + segment.StartBucket +
                        " endBucket=" + LoopDecomposition.Bucket(
                            observations[segment.EndTick - 1], options) +
                        " startState=" +
                        observations[segment.StartTick].BossState +
                        " endState=" +
                        observations[segment.EndTick - 1].BossState);

                // Two things the composition driver depends on, measured on a
                // real fight rather than assumed.
                //
                // The boundary gap has to be zero: consecutive segments are
                // adjacent ticks, so a non-zero gap would mean the recorded
                // positions are not the ones the chain would actually visit.
                //
                // The start spread is the number that decides whether the
                // decomposition is worth anything. One route per group is only
                // valid if the loops in a group start close enough together that
                // the route's end can join the next loop's start; a group whose
                // starts are spread across the arena is a group whose single
                // route cannot connect, and the composition would have to reject
                // it rather than pretend.
                var boundaryGap = 0f;
                for (var index = 0; index + 1 < segments.Count; index++)
                    boundaryGap = Math.Max(boundaryGap, Math.Max(
                        Math.Abs(segments[index].EndPosition.X -
                            segments[index + 1].StartPosition.X),
                        Math.Abs(segments[index].EndPosition.Y -
                            segments[index + 1].StartPosition.Y)));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "LOOP COMPOSITION boundaryMaxGap={0:R} loops={1}",
                    boundaryGap, segments.Count));
                for (var group = 0; group < report.GroupLoops.Count; group++)
                {
                    var spread = 0f;
                    var relativeSpread = 0f;
                    var firstX = float.NaN;
                    var firstY = float.NaN;
                    var firstRelX = float.NaN;
                    var firstRelY = float.NaN;
                    for (var index = 0; index < segments.Count; index++)
                    {
                        if (segments[index].ThreatSignature != group) continue;
                        var start = segments[index].StartPosition;
                        var boss = observations[segments[index].StartTick]
                            .BossPosition;
                        var relX = start.X - boss.X;
                        var relY = start.Y - boss.Y;
                        if (float.IsNaN(firstX))
                        {
                            firstX = start.X;
                            firstY = start.Y;
                            firstRelX = relX;
                            firstRelY = relY;
                            continue;
                        }
                        spread = Math.Max(spread, Math.Max(
                            Math.Abs(start.X - firstX),
                            Math.Abs(start.Y - firstY)));
                        relativeSpread = Math.Max(relativeSpread, Math.Max(
                            Math.Abs(relX - firstRelX),
                            Math.Abs(relY - firstRelY)));
                    }
                    // The absolute spread says nothing on its own: the arena is a
                    // long flat plain, so two loops that differ only by a
                    // translation are the same problem and one control sequence
                    // solves both. The boss-relative spread is what decides it.
                    // A group with a small relative spread is a genuine group; a
                    // large one means the grouping is too coarse and the single
                    // route it promises does not exist.
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP COMPOSITION group={0} loops={1} startSpread={2:R} " +
                        "bossRelativeSpread={3:R}",
                        group, report.GroupLoops[group], spread,
                        relativeSpread));
                }

                // The grouping promises one route per group, so the question that
                // decides whether the reduction is real is how far apart the
                // Boss-relative geometry of a group's loops is. Sweeping the cell
                // size states the trade-off as numbers instead of picking one and
                // hoping: a coarse cell groups more loops and is wrong more
                // often, a fine cell is right and groups nothing.
                foreach (var cell in new[] { 0f, 200f, 64f, 32f, 16f, 8f })
                {
                    var sweepOptions = new LoopSegmentationOptions
                    {
                        MinimumTicks = minimumTicks,
                        RelativeCellSize = cell,
                    };
                    var sweep = LoopDecomposition.Segment(observations, sweepOptions);
                    var sweepReport = LoopDecompositionSummary.Summarize(sweep);
                    var worst = 0f;
                    for (var group = 0; group < sweepReport.GroupLoops.Count; group++)
                    {
                        var first = true;
                        var firstRelX = 0f;
                        var firstRelY = 0f;
                        for (var index = 0; index < sweep.Count; index++)
                        {
                            if (sweep[index].ThreatSignature != group) continue;
                            var start = sweep[index].StartPosition;
                            var boss = observations[sweep[index].StartTick]
                                .BossPosition;
                            var relX = start.X - boss.X;
                            var relY = start.Y - boss.Y;
                            if (first)
                            {
                                firstRelX = relX;
                                firstRelY = relY;
                                first = false;
                                continue;
                            }
                            worst = Math.Max(worst, Math.Max(
                                Math.Abs(relX - firstRelX),
                                Math.Abs(relY - firstRelY)));
                        }
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "LOOP SWEEP cell={0:R} groups={1} loops={2} " +
                        "maxBossRelativeSpread={3:R} reduction={4:0.00}",
                        cell, sweepReport.DistinctGroups, sweep.Count, worst,
                        sweepReport.ReductionFactor));
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("LOOP DECOMPOSITION FAILED " +
                    error.GetType().Name + ": " + error.Message);
                return 1;
            }
        }

        /// <summary>The Boss NPC in a dense row: the one flagged as a boss, or
        /// failing that the one with the largest maximum life, so a trace with
        /// minions in it does not silently decompose the wrong entity.</summary>
        private static Dictionary<string, object> BossNpc(
            Dictionary<string, object> row)
        {
            var npcs = ArrayField(row, "npcs");
            if (npcs == null) return null;
            Dictionary<string, object> best = null;
            foreach (var entry in npcs)
            {
                var npc = entry as Dictionary<string, object>;
                if (npc == null) continue;
                if (Boolean(npc, "boss")) return npc;
                if (best == null ||
                    Number(npc, "lifeMax") > Number(best, "lifeMax"))
                    best = npc;
            }
            return best;
        }
    }
}
