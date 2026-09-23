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
        // Separate, read-only x86 oracle. This does not launch the game or count
        // motion frames as Boss victories. The guarded launcher audits identity,
        // manifest, isolation and exits independently before this comparison.
        private static int VerifyNativeMotionTrace(string path)
        {
            try
            {
                var parser = new JavaScriptSerializer();
                var rows = new List<Dictionary<string, object>>();
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Empty trace row.");
                    rows.Add(parser.Deserialize<Dictionary<string, object>>(line));
                }
                if (rows.Count != 180) throw new InvalidDataException("Expected exactly 180 native motion frames.");
                var floor = float.MinValue;
                for (var index = 0; index < 20; index++)
                    floor = Math.Max(floor, Number(Object(rows[index], "postPlayer"), "bottomY"));
                var maximumJumpError = 0f;
                var maximumPositionError = 0f;
                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    if ((string)row["schema"] != "chaite-native-motion-frame/v1" || Integer(row, "tick") != index + 1)
                        throw new InvalidDataException("Trace schema or tick sequence differs.");
                    if (Integer(row, "playerUpdateCalls") != 1 || Integer(row, "jumpMovementCalls") != 1)
                        throw new InvalidDataException("Native update count differs at tick " + (index + 1));
                    var before = Object(row, "preJump");
                    var after = Object(row, "postJump");
                    var final = Object(row, "postPlayer");
                    var state = new JumpSnapshot { Known = true, RemainingTicks = Integer(before, "jump"),
                        ReleaseReady = Boolean(before, "releaseJump"), CloudAvailable = Boolean(before, "canJumpAgain_Cloud"),
                        CloudEnabled = Boolean(before, "hasJumpOption_Cloud"),
                        AutoJump = Boolean(before, "autoJump"),
                        Speed = Number(before, "jumpSpeed"), Height = Integer(before, "jumpHeight") };
                    var vy = Number(Object(before, "velocity"), "y");
                    var control = Boolean(Object(before, "controls"), "jump");
                    if (control != Boolean(row, "requestedJump"))
                        throw new InvalidDataException("Requested/native jump controls differ at tick " + (index + 1));
                    var inverted = Number(before, "gravDir") < 0f;
                    if (inverted) throw new InvalidDataException("This fixture only declares normal gravity.");
                    JumpMotion.ApplyJump(ref state, ref vy, control, inverted);
                    CheckTraceValue(Integer(after, "jump") == state.RemainingTicks, index, "jump counter");
                    CheckTraceValue(Boolean(after, "releaseJump") == state.ReleaseReady, index, "release gate");
                    CheckTraceValue(Boolean(after, "canJumpAgain_Cloud") == state.CloudAvailable, index, "cloud charge");
                    var jumpError = Math.Abs(vy - Number(Object(after, "velocity"), "y"));
                    maximumJumpError = Math.Max(maximumJumpError, jumpError);
                    CheckTraceValue(jumpError <= .00001f, index, "post-JumpMovement velocity");
                    vy = JumpMotion.ApplyGravity(vy, Number(before, "gravity"), Number(before, "maxFallSpeed"), inverted);
                    var beforeY = Number(Object(before, "position"), "y");
                    var y = beforeY + vy;
                    var height = Integer(before, "height");
                    if (y + height >= floor && vy >= 0f) { y = floor - height; vy = y - beforeY; }
                    var positionError = Math.Abs(y - Number(Object(final, "position"), "y"));
                    maximumPositionError = Math.Max(maximumPositionError, positionError);
                    // Float world-coordinate addition rounds at the native x86
                    // store. 0.002 px is below one texture pixel by 500x.
                    CheckTraceValue(positionError <= .002f, index, "post-Player.Update position");
                    CheckTraceValue(Math.Abs(vy - Number(Object(final, "velocity"), "y")) <= .00001f,
                        index, "post-Player.Update velocity");
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "NATIVE MOTION MODEL PASS frames={0} maxJumpVelocityError={1:R} maxPositionError={2:R}",
                    rows.Count, maximumJumpError, maximumPositionError));
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("NATIVE MOTION MODEL FAIL " + error.Message);
                return 1;
            }
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> value, string name)
            => value[name] as Dictionary<string, object> ?? throw new InvalidDataException("Missing object " + name);

        private static bool Boolean(Dictionary<string, object> value, string name)
            => value[name] is bool result ? result : throw new InvalidDataException("Non-boolean " + name);

        private static int Integer(Dictionary<string, object> value, string name)
            => value[name] is int result ? result : throw new InvalidDataException("Non-integer " + name);

        private static float Number(Dictionary<string, object> value, string name)
        {
            var raw = value[name];
            if (!(raw is int) && !(raw is long) && !(raw is decimal) && !(raw is double) && !(raw is float))
                throw new InvalidDataException("Non-numeric " + name);
            var result = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidDataException("Non-finite " + name);
            return result;
        }

        /// <summary>Reads a JSON array field. The serialiser hands back either a
        /// typed array or an ArrayList depending on the shape, so both are
        /// accepted rather than one being assumed.</summary>
        private static object[] ArrayField(Dictionary<string, object> value, string name)
        {
            if (!value.ContainsKey(name)) return null;
            var raw = value[name];
            var typed = raw as object[];
            if (typed != null) return typed;
            var list = raw as System.Collections.ArrayList;
            if (list != null) return list.ToArray();
            return null;
        }

        /// <summary>Reads one element of a numeric array field, which is how the
        /// Boss's attack state reaches the trace: it lives in the NPC's own ai
        /// array rather than in a named field.</summary>
        private static float Number(Dictionary<string, object> value, string name, int index)
        {
            var array = ArrayField(value, name);
            if (array == null || index < 0 || index >= array.Length)
                throw new InvalidDataException("Missing array element " + name + "[" + index + "]");
            var raw = array[index];
            if (!(raw is int) && !(raw is long) && !(raw is decimal) && !(raw is double) && !(raw is float))
                throw new InvalidDataException("Non-numeric " + name + "[" + index + "]");
            var result = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidDataException("Non-finite " + name + "[" + index + "]");
            return result;
        }

        private static void CheckTraceValue(bool condition, int index, string field)        {
            if (!condition) throw new InvalidDataException("Native mismatch at tick " + (index + 1) + ": " + field);
        }
    }
}
