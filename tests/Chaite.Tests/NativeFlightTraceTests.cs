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
        // Read-only x86 oracle, never a game launcher. Checks native boundaries
        // and the complete JumpMovement -> wing/resources -> gravity -> flat
        // floor collision vertical tick. Horizontal/other gear are not claimed.
        private static int VerifyNativeFlightTrace(string path)
        {
            try
            {
                var parser = new JavaScriptSerializer();
                var rows = new List<Dictionary<string, object>>();
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Empty flight row.");
                    rows.Add(parser.Deserialize<Dictionary<string, object>>(line));
                }
                if (rows.Count != 600) throw new InvalidDataException("Expected exactly 600 flight frames.");
                var floor = float.MinValue;
                for (var index = 0; index < 20; index++)
                    floor = Math.Max(floor, Number(Object(rows[index], "postPlayer"), "bottomY"));
                var maximumVelocityError = 0f; var maximumPositionError = 0f;
                var poweredFrames = 0; var glideFrames = 0; var featherFrames = 0;
                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    if ((string)row["schema"] != "chaite-native-flight-frame/v1" || Integer(row, "tick") != index + 1)
                        throw new InvalidDataException("Flight schema/sequence differs.");
                    CheckTraceValue(Integer(row, "playerUpdateCalls") == 1 && Integer(row, "jumpMovementCalls") == 1,
                        index, "one native player/jump update");
                    var before = Object(row, "preJump"); var after = Object(row, "postJump");
                    var final = Object(row, "postPlayer");
                    var jump = new JumpSnapshot { Known = true, RemainingTicks = Integer(before, "jump"),
                        ReleaseReady = Boolean(before, "releaseJump"), CloudAvailable = Boolean(before, "canJumpAgain_Cloud"),
                        CloudEnabled = Boolean(before, "hasJumpOption_Cloud"), AutoJump = Boolean(before, "autoJump"),
                        SlowFall = Boolean(before, "slowFall"), Speed = Number(before, "jumpSpeed"), Height = Integer(before, "jumpHeight") };
                    var flight = new FlightSnapshot { Known = true, WingsLogic = Integer(before, "wingsLogic"),
                        RocketBoots = Integer(before, "rocketBoots"), WingTime = Number(before, "wingTime"),
                        WingTimeMax = Integer(before, "wingTimeMax"), RocketTime = Integer(before, "rocketTime"),
                        RocketTimeMax = Integer(before, "rocketTimeMax"), RocketDelay = Integer(before, "rocketDelay"),
                        CanRocket = Boolean(before, "canRocket"), RocketRelease = Boolean(before, "rocketRelease"),
                        JustJumped = Boolean(before, "justJumped") };
                    CheckTraceValue(flight.WingsLogic == 1 && (flight.RocketBoots == 0 || flight.RocketBoots == 2) &&
                        flight.WingTimeMax == 100 && flight.RocketTimeMax == 7 && flight.RocketDelay == 0,
                        index, "declared Demon profile");
                    CheckTraceValue(Number(before, "gravDir") == 1f && jump.Speed == 5.01f && jump.Height == 15,
                        index, "normal base jump parameters");
                    CheckTraceValue(!flight.JustJumped, index, "ResetEffects cleared prior JustJumped");
                    var control = Boolean(Object(before, "controls"), "jump");
                    var controlUp = Boolean(Object(before, "controls"), "up");
                    var controlDown = Boolean(Object(before, "controls"), "down");
                    CheckTraceValue(control == Boolean(row, "requestedJump"), index, "requested/native jump");
                    CheckTraceValue(controlUp == Boolean(row, "requestedUp"), index, "requested/native Up");
                    CheckTraceValue(controlDown == Boolean(row, "requestedDown"), index, "requested/native Down");
                    var vy = Number(Object(before, "velocity"), "y");
                    FlightMotion.ApplyJump(ref flight, ref jump, ref vy, control);
                    CheckFlightJump(after, in jump, in flight, index);
                    CheckFlightNear(vy, Number(Object(after, "velocity"), "y"), index, "postJump vy", ref maximumVelocityError, .00001f);

                    var wingExpected = control && flight.WingTime > 0f && jump.RemainingTicks == 0 && vy != 0f;
                    var preWingFuel = vy == 0f && jump.ReleaseReady || jump.AutoJump && flight.JustJumped ?
                        flight.WingTimeMax : flight.WingTime;
                    if (preWingFuel != flight.WingTime)
                        wingExpected = control && preWingFuel > 0f && jump.RemainingTicks == 0 && vy != 0f;
                    CheckTraceValue(Integer(row, "wingMovementCalls") == (wingExpected ? 1 : 0), index, "native wing decision/call count");
                    if (wingExpected)
                    {
                        var preWing = Object(row, "preWing"); var postWing = Object(row, "postWing");
                        CheckTraceValue(Number(preWing, "wingTime") == preWingFuel, index, "preWing refill order");
                        CheckTraceValue(Integer(preWing, "rocketTime") == (flight.RocketBoots == 0 ? 0 : flight.RocketTime),
                            index, "rocket conversion has not occurred before WingMovement");
                        CheckFlightNear(vy, Number(Object(preWing, "velocity"), "y"), index, "preWing vy", ref maximumVelocityError, .00001f);
                        CheckFlightNear(FlightMotion.DemonThrust(vy, jump.Speed), Number(Object(postWing, "velocity"), "y"),
                            index, "postWing vy", ref maximumVelocityError, .00001f);
                        CheckTraceValue(Number(postWing, "wingTime") == preWingFuel - 1f, index, "native wing decrement");
                    }
                    else CheckTraceValue(row["preWing"] == null && row["postWing"] == null, index, "absent wing call snapshots");

                    var phase = FlightMotion.ApplyAfterJump(ref flight, in jump, ref vy, control,
                        controlUp, controlDown, Number(before, "gravity"), Number(before, "maxFallSpeed"));
                    if (phase == FlightPhase.WingPowered) poweredFrames++;
                    if (phase == FlightPhase.Glide) glideFrames++;
                    if (phase == FlightPhase.FeatherFall) featherFrames++;
                    CheckFlightJump(final, in jump, in flight, index);
                    CheckTraceValue(Number(final, "wingTime") == flight.WingTime && Integer(final, "rocketTime") == flight.RocketTime &&
                        Integer(final, "rocketDelay") == flight.RocketDelay, index, "complete tick flight resources");
                    var beforeY = Number(Object(before, "position"), "y"); var y = beforeY + vy;
                    var height = Integer(before, "height");
                    if (y + height >= floor && vy >= 0f) { y = floor - height; vy = y - beforeY; }
                    CheckFlightNear(y, Number(Object(final, "position"), "y"), index, "postPlayer Y", ref maximumPositionError, .002f);
                    CheckFlightNear(vy, Number(Object(final, "velocity"), "y"), index, "postPlayer vy", ref maximumVelocityError, .00001f);
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "NATIVE FLIGHT MODEL PASS frames={0} powered={1} glide={2} feather={3} maxVelocityError={4:R} maxPositionError={5:R}",
                    rows.Count, poweredFrames, glideFrames, featherFrames, maximumVelocityError, maximumPositionError));
                VerifyFreeFlight(rows, floor);
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("NATIVE FLIGHT MODEL FAIL " + error.Message);
                return 1;
            }
        }

        private static void VerifyFreeFlight(List<Dictionary<string, object>> rows, float floor)
        {
            // A second oracle carries its OWN Y, velocity, jump counter and
            // flight resources through all 600 ticks. It never re-seeds from
            // intermediate native results. Only prescribed controls and fixed
            // fixture physics constants are inputs after the first snapshot.
            var first = Object(rows[0], "preJump");
            var jump = new JumpSnapshot { Known = true, RemainingTicks = Integer(first, "jump"),
                ReleaseReady = Boolean(first, "releaseJump"), CloudAvailable = Boolean(first, "canJumpAgain_Cloud"),
                CloudEnabled = Boolean(first, "hasJumpOption_Cloud"), AutoJump = Boolean(first, "autoJump"),
                SlowFall = Boolean(first, "slowFall"), Speed = Number(first, "jumpSpeed"), Height = Integer(first, "jumpHeight") };
            var flight = new FlightSnapshot { Known = true, WingsLogic = Integer(first, "wingsLogic"),
                RocketBoots = Integer(first, "rocketBoots"), WingTime = Number(first, "wingTime"),
                WingTimeMax = Integer(first, "wingTimeMax"), RocketTime = Integer(first, "rocketTime"),
                RocketTimeMax = Integer(first, "rocketTimeMax"), RocketDelay = Integer(first, "rocketDelay"),
                CanRocket = Boolean(first, "canRocket"), RocketRelease = Boolean(first, "rocketRelease") };
            var y = Number(Object(first, "position"), "y");
            var vy = Number(Object(first, "velocity"), "y");
            var gravity = Number(first, "gravity"); var cap = Number(first, "maxFallSpeed"); var height = Integer(first, "height");
            var maxY = 0f; var maxV = 0f;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index]; var before = Object(row, "preJump"); var after = Object(row, "postPlayer");
                CheckTraceValue(Number(before, "gravity") == gravity && Number(before, "maxFallSpeed") == cap &&
                    Integer(before, "height") == height && Boolean(before, "autoJump") == jump.AutoJump &&
                    Boolean(before, "hasJumpOption_Cloud") == jump.CloudEnabled &&
                    Boolean(before, "slowFall") == jump.SlowFall, index, "fixed free-run fixture parameters");
                var held = Boolean(row, "requestedJump");
                var up = Boolean(row, "requestedUp");
                var down = Boolean(row, "requestedDown");
                var previousY = y;
                FlightMotion.RefreshBeforeMovement(ref flight, ref jump, vy);
                FlightMotion.ApplyJump(ref flight, ref jump, ref vy, held);
                FlightMotion.ApplyAfterJump(ref flight, in jump, ref vy, held, up, down, gravity, cap);
                y += vy;
                if (y + height >= floor && vy >= 0f) { y = floor - height; vy = y - previousY; }
                CheckFlightNear(y, Number(Object(after, "position"), "y"), index, "free-run Y", ref maxY, .002f);
                CheckFlightNear(vy, Number(Object(after, "velocity"), "y"), index, "free-run vy", ref maxV, .00001f);
                CheckFlightJump(after, in jump, in flight, index);
                CheckTraceValue(Number(after, "wingTime") == flight.WingTime && Integer(after, "rocketTime") == flight.RocketTime &&
                    Integer(after, "rocketDelay") == flight.RocketDelay, index, "free-run resource counters");
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "NATIVE FLIGHT FREE-RUN PASS frames={0} maxVelocityError={1:R} maxPositionError={2:R}", rows.Count, maxV, maxY));
        }

        private static void CheckFlightJump(Dictionary<string, object> native, in JumpSnapshot jump,
            in FlightSnapshot flight, int index)
        {
            CheckTraceValue(Integer(native, "jump") == jump.RemainingTicks, index, "jump counter");
            CheckTraceValue(Boolean(native, "releaseJump") == jump.ReleaseReady, index, "jump release");
            CheckTraceValue(Boolean(native, "canJumpAgain_Cloud") == jump.CloudAvailable, index, "cloud charge");
            CheckTraceValue(Boolean(native, "canRocket") == flight.CanRocket && Boolean(native, "rocketRelease") == flight.RocketRelease,
                index, "rocket can/release");
            CheckTraceValue(Boolean(native, "justJumped") == flight.JustJumped, index, "JustJumped");
        }

        private static void CheckFlightNear(float expected, float actual, int index, string field, ref float maximumError, float tolerance)
        {
            var error = Math.Abs(expected - actual); maximumError = Math.Max(maximumError, error);
            CheckTraceValue(error <= tolerance, index, field + " predicted=" + expected.ToString("R", CultureInfo.InvariantCulture) +
                " native=" + actual.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
