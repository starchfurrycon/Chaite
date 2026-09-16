using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Chaite.Core
{
    /// <summary>
    /// Small learned movement policy for one reviewed route.
    ///
    /// The point of this type is that training never recompiles anything: the
    /// weights live in a plain text file that is read once per process, so a
    /// training generation only rewrites a data file and the engine binary --
    /// and therefore <c>InputCoreSha256</c> -- stays constant for the whole
    /// run. That makes every generation comparable by construction and removes
    /// the stale-build trap that once produced a completely wrong conclusion in
    /// this project.
    ///
    /// Every failure path throws. A configured-but-broken policy must never
    /// fall back to the fixed state machine, because that would silently blend
    /// two different controllers into one training curve.
    /// </summary>
    public sealed class LearnedPolicy
    {
        public const int InputCount = 38;
        // Halved from 32. The policy reads a 38-dimensional input and emits
        // ten logits, and a derivative-free search pays for every parameter,
        // so the previous width was habit rather than measurement.
        public const int HiddenCount = 16;
        public const int HeadCount = 10;

        public const string FileVariable = "CHAITE_POLICY_FILE";
        public const string RoutesVariable = "CHAITE_POLICY_ROUTES";

        private const string Magic = "chaite-policy";
        private const string Version = "1";

        private static readonly object Gate = new object();
        private static bool _configured;
        private static string _file;
        private static HashSet<string> _routes;
        private static readonly Dictionary<FormulaRoute, LearnedPolicy> Cache =
            new Dictionary<FormulaRoute, LearnedPolicy>();

        private readonly float[] _hiddenWeights = new float[HiddenCount * InputCount];
        private readonly float[] _hiddenBias = new float[HiddenCount];
        private readonly float[] _headWeights = new float[HeadCount * HiddenCount];
        private readonly float[] _headBias = new float[HeadCount];
        private readonly float[] _features = new float[InputCount];
        private readonly float[] _hidden = new float[HiddenCount];
        private readonly float[] _logits = new float[HeadCount];

        /// <summary>True when a policy file has been configured at all.</summary>
        public static bool IsConfigured
        {
            get { return EnsureConfigured() && _file != null; }
        }

        /// <summary>Returns the policy that owns this route, or null when the
        /// fixed state machine still owns it. Throws when a policy was
        /// configured for the route but cannot be loaded.</summary>
        public static LearnedPolicy ForRoute(FormulaRoute route)
        {
            if (!EnsureConfigured() || _file == null) return null;
            lock (Gate)
            {
                if (!_routes.Contains(route.ToString())) return null;
                LearnedPolicy policy;
                if (Cache.TryGetValue(route, out policy)) return policy;
                policy = Load(_file);
                Cache[route] = policy;
                return policy;
            }
        }

        /// <summary>Drops the process-wide cache. Only used by tests.</summary>
        public static void ResetCache()
        {
            lock (Gate)
            {
                _configured = false;
                _file = null;
                _routes = null;
                Cache.Clear();
            }
        }

        private static bool EnsureConfigured()
        {
            lock (Gate)
            {
                if (_configured) return true;
                var path = Environment.GetEnvironmentVariable(FileVariable);
                if (string.IsNullOrEmpty(path))
                {
                    _configured = true;
                    _file = null;
                    return true;
                }
                var names = Environment.GetEnvironmentVariable(RoutesVariable);
                if (string.IsNullOrEmpty(names))
                    throw new InvalidOperationException(FileVariable +
                        " is set but " + RoutesVariable + " is not; refusing to" +
                        " guess which routes the policy owns.");
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in names.Split(','))
                {
                    var name = entry.Trim();
                    if (name.Length > 0) set.Add(name);
                }
                if (set.Count == 0)
                    throw new InvalidOperationException(RoutesVariable +
                        " lists no route names.");
                _configured = true;
                _file = path;
                _routes = set;
                return true;
            }
        }

        public static LearnedPolicy Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("policy path is empty", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "learned policy file was configured but does not exist: " +
                    path, path);
            var tokens = File.ReadAllText(path).Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 4 || tokens[0] != Magic || tokens[1] != Version)
                throw new InvalidDataException(
                    "learned policy file is not a " + Magic + " " + Version +
                    " file: " + path);
            int declaredInputs, declaredHidden;
            if (!int.TryParse(tokens[2], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out declaredInputs) ||
                !int.TryParse(tokens[3], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out declaredHidden) ||
                declaredInputs != InputCount || declaredHidden != HiddenCount)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "learned policy file declares {0} inputs and {1} hidden" +
                    " units but this build expects {2} and {3}: {4}",
                    tokens[2], tokens[3], InputCount, HiddenCount, path));
            var expected = 4 + HiddenCount * InputCount + HiddenCount +
                HeadCount * HiddenCount + HeadCount;
            if (tokens.Length != expected)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "learned policy file has {0} tokens but {1} are required: {2}",
                    tokens.Length, expected, path));

            var policy = new LearnedPolicy();
            var index = 4;
            for (var i = 0; i < policy._hiddenWeights.Length; i++)
                policy._hiddenWeights[i] = Parse(tokens[index++], path);
            for (var i = 0; i < policy._hiddenBias.Length; i++)
                policy._hiddenBias[i] = Parse(tokens[index++], path);
            for (var i = 0; i < policy._headWeights.Length; i++)
                policy._headWeights[i] = Parse(tokens[index++], path);
            for (var i = 0; i < policy._headBias.Length; i++)
                policy._headBias[i] = Parse(tokens[index++], path);
            return policy;
        }

        private static float Parse(string token, string path)
        {
            double value;
            if (!double.TryParse(token, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) ||
                double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException(
                    "learned policy file has a malformed weight '" + token +
                    "': " + path);
            return (float)value;
        }

        /// <summary>
        /// Feature vector, in order. Kept in one place so the documented layout
        /// and the evaluated layout cannot drift apart.
        /// </summary>
        public static void FillFeatures(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            float[] destination)
        {
            if (destination == null || destination.Length < InputCount)
                throw new ArgumentException("feature buffer is too small",
                    "destination");
            var playerX = player.Center.X;
            var playerY = player.Center.Y;
            var bossX = boss.Center.X;
            var bossY = boss.Center.Y;
            var dx = playerX - bossX;
            var dy = playerY - bossY;
            var at = 0;
            destination[at++] = Scale(dx, 900f);
            destination[at++] = Scale(dy, 900f);
            destination[at++] = Scale(Math.Abs(dx), 900f);
            destination[at++] = Scale(Math.Abs(dy), 900f);
            destination[at++] = Scale((float)Math.Sqrt(dx * dx + dy * dy), 900f);
            destination[at++] = Scale(player.Velocity.X, 20f);
            destination[at++] = Scale(player.Velocity.Y, 20f);
            destination[at++] = Scale(boss.Velocity.X, 20f);
            destination[at++] = Scale(boss.Velocity.Y, 20f);
            destination[at++] = Scale(player.Velocity.X - boss.Velocity.X, 20f);
            destination[at++] = Scale(player.Velocity.Y - boss.Velocity.Y, 20f);
            var state = input.NativeState;
            for (var i = 0; i <= 13; i++) destination[at++] = state == i ? 1f : 0f;
            destination[at++] = Scale(input.NativeTimer, 600f);
            destination[at++] = Scale(input.NativeSequence, 600f);
            var form = input.NativeForm;
            for (var i = 0; i < 4; i++)
                destination[at++] = input.NativeFormKnown && form == i ? 1f : 0f;
            if (arena != null)
            {
                destination[at++] = Scale(arena.ClearanceLeft, 640f);
                destination[at++] = Scale(arena.ClearanceRight, 640f);
                destination[at++] = Scale(arena.ClearanceUp, 640f);
                destination[at++] = Scale(arena.ClearanceDown, 640f);
            }
            else
            {
                destination[at++] = 0f;
                destination[at++] = 0f;
                destination[at++] = 0f;
                destination[at++] = 0f;
            }
            destination[at++] = player.MaxLife > 0
                ? (float)player.Life / player.MaxLife : 0f;
            destination[at++] = input.PlayerBelowBoss ? 1f : 0f;
            destination[at] = input.PlayerRightOfBoss ? 1f : 0f;
        }

        private static float Scale(float value, float divisor)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            var scaled = value / divisor;
            if (scaled > 4f) return 4f;
            if (scaled < -4f) return -4f;
            return scaled;
        }

        /// <summary>Residual form of the same network, and the only form any
        /// caller uses. The scripted decision is the base and the network may only
        /// correct it, with class zero of each head meaning "leave it alone".
        /// Zero weights therefore reproduce the fixed state machine exactly,
        /// so the search starts from competence instead of from noise. The ten
        /// head offsets are unchanged; only their meaning is.</summary>
        public bool Adjust(in FormulaScriptInput input, PlayerSnapshot player,
            in TargetSnapshot boss, ArenaSnapshot arena, int scriptedHorizontal,
            int scriptedVertical, bool scriptedJump, bool scriptedDash,
            out int horizontal, out int vertical, out bool jump, out bool dash)
        {
            horizontal = scriptedHorizontal;
            vertical = scriptedVertical;
            jump = scriptedJump;
            dash = scriptedDash;
            if (!Evaluate(in input, player, in boss, arena)) return false;
            var hx = ArgMax(0, 3);
            if (hx == 1) horizontal = ClampStep(scriptedHorizontal - 1);
            else if (hx == 2) horizontal = ClampStep(scriptedHorizontal + 1);
            var vy = ArgMax(3, 3);
            if (vy == 1) vertical = ClampStep(scriptedVertical - 1);
            else if (vy == 2) vertical = ClampStep(scriptedVertical + 1);
            if (ArgMax(6, 2) == 1) jump = !scriptedJump;
            if (ArgMax(8, 2) == 1) dash = !scriptedDash;
            return true;
        }

        private static int ClampStep(int value)
        {
            return value < -1 ? -1 : (value > 1 ? 1 : value);
        }

        /// <summary>Forward pass shared by both entry points.</summary>
        private bool Evaluate(in FormulaScriptInput input, PlayerSnapshot player,
            in TargetSnapshot boss, ArenaSnapshot arena)
        {
            if (player == null) return false;
            FillFeatures(in input, player, in boss, arena, _features);
            for (var h = 0; h < HiddenCount; h++)
            {
                var sum = _hiddenBias[h];
                var offset = h * InputCount;
                for (var i = 0; i < InputCount; i++)
                    sum += _hiddenWeights[offset + i] * _features[i];
                _hidden[h] = (float)Math.Tanh(sum);
            }
            for (var k = 0; k < HeadCount; k++)
            {
                var sum = _headBias[k];
                var offset = k * HiddenCount;
                for (var h = 0; h < HiddenCount; h++)
                    sum += _headWeights[offset + h] * _hidden[h];
                _logits[k] = sum;
            }
            return true;
        }

        private int ArgMax(int start, int count)
        {
            var best = 0;
            var bestValue = _logits[start];
            for (var i = 1; i < count; i++)
            {
                if (_logits[start + i] > bestValue)
                {
                    bestValue = _logits[start + i];
                    best = i;
                }
            }
            return best;
        }
    }
}