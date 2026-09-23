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
        public const int InputCount = 40;
        // Halved from 32. The policy reads a 40-dimensional input and emits
        // eleven logits, and a derivative-free search pays for every parameter,
        // so the previous width was habit rather than measurement.
        //
        // This is now only the DEFAULT written by the trainer, not a property
        // of the build: the width is read from the file header and the arrays
        // are sized from it. The 843-weight layout is dominated by the hidden
        // layer -- 640 input weights plus 16 biases out of 843 parameters --
        // so no search-side restriction can improve the evaluations-per-
        // parameter ratio without shrinking the width itself. Reproduction of
        // the fixed state machine does not depend on the width: zero weights
        // still make every logit zero, and the action margin then gives class
        // zero the win for every head at any width.
        public const int HiddenCount = 16;
        /// <summary>Upper bound on the width a file may declare. Not a tuning
        /// knob: it exists so a corrupt header cannot ask the loader to
        /// allocate an arbitrary amount of memory.</summary>
        public const int MaxHiddenCount = 64;
        /// <summary>Logits, and therefore the head layout, in order:
        /// <c>0..2</c> horizontal, <c>3..5</c> vertical, <c>6..7</c> jump and
        /// <c>8..10</c> dash. The dash head is three-way rather than two-way on
        /// purpose. A two-way head could only flip the scripted bit, so "hold
        /// the dash" and "fire a dash the script never proposed" were the same
        /// action, and a policy that wanted to delay a dash spent it during
        /// cruise instead. Class one now suppresses only, and class two forces
        /// only, so a hold cannot cost the charge its dash.</summary>
        public const int HeadCount = 11;

        public const string FileVariable = "CHAITE_POLICY_FILE";
        public const string RoutesVariable = "CHAITE_POLICY_ROUTES";

        private const string Magic = "chaite-policy";
        private const string Version = "1";

        /// <summary>Margin added to class zero of every head before the argmax,
        /// and the only scale-sensitive term in the residual decision.
        ///
        /// The argmax over a head's logits is invariant to the overall weight
        /// scale, so without this term a perturbation of any size -- sigma 0.1
        /// or sigma 0.001 -- replaces the scripted decision on most ticks
        /// instead of nudging it: every logit starts at exactly zero, so any
        /// non-zero value wins the tie that class zero was winning, and a
        /// three-way head then leaves class zero winning only about a third of
        /// the time. Measured on the first generation that produced real rows,
        /// that made 15 of 16 candidates take their first hit at 338..785 ticks
        /// against the fixed machine's 1876, and it made the sigma schedule
        /// inert, because shrinking sigma cannot change which logit is largest.
        ///
        /// With the margin, class zero wins unless a correction beats it by a
        /// fixed amount, so the policy stays a sparse correction to the script
        /// and sigma becomes a real knob again. Zero weights still reproduce the
        /// fixed state machine exactly, because every logit is zero and class
        /// zero then wins by the margin alone.
        ///
        /// Read from the environment so that the margin can be swept in-engine,
        /// on whole fights, without a rebuild -- the same reason the weights are
        /// a data file rather than a compiled constant.</summary>
        public const float DefaultActionMargin = 1.0f;
        public const string ActionMarginVariable = "CHAITE_DEFAULT_ACTION_MARGIN";
        private static readonly float ActionMargin = ResolveActionMargin();

        private static float ResolveActionMargin()
        {
            var raw = Environment.GetEnvironmentVariable(ActionMarginVariable);
            float parsed;
            if (!string.IsNullOrEmpty(raw) && float.TryParse(raw,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return DefaultActionMargin;
        }

        private static readonly object Gate = new object();
        private static bool _configured;
        private static string _file;
        private static HashSet<string> _routes;
        private static readonly Dictionary<FormulaRoute, LearnedPolicy> Cache =
            new Dictionary<FormulaRoute, LearnedPolicy>();

        private readonly int _hiddenCount;
        private readonly float[] _hiddenWeights;
        private readonly float[] _hiddenBias;
        private readonly float[] _headWeights;
        private readonly float[] _headBias;
        private readonly float[] _features = new float[InputCount];
        private readonly float[] _hidden;
        private readonly float[] _logits = new float[HeadCount];

        /// <summary>Width this instance was loaded with. Exposed so a training
        /// log can record which layout produced a row.</summary>
        public int HiddenWidth { get { return _hiddenCount; } }

        private LearnedPolicy(int hiddenCount)
        {
            _hiddenCount = hiddenCount;
            _hiddenWeights = new float[hiddenCount * InputCount];
            _hiddenBias = new float[hiddenCount];
            _headWeights = new float[HeadCount * hiddenCount];
            _headBias = new float[HeadCount];
            _hidden = new float[hiddenCount];
        }

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
        /// <summary>Marker appended to the branch label the script already chose
        /// when a policy adjusts the output.</summary>
        public const string LearnedSuffix = "+learned";

        /// <summary>
        /// Composes the label of the branch that ran with the marker that a
        /// policy adjusted it, instead of replacing the branch label outright.
        /// Replacing it costs two things. The probe marks a battle observation
        /// edge when the phase string changes, so a label pinned to a constant
        /// for the whole run stops that reason from ever firing. And per-branch
        /// attribution in the observation channel is lost: section 25 needed to
        /// know which of the three rules inside state 6 pressed the horizontal
        /// key, and every state-6 row in every run carried the same composed
        /// label, which made the question unanswerable from the artifacts.
        /// </summary>
        public static string ComposeLearnedPhase(string scriptedPhase, string fallback)
        {
            return string.IsNullOrEmpty(scriptedPhase)
                ? fallback
                : scriptedPhase + LearnedSuffix;
        }

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

        /// <summary>
        /// Opt-in diagnostic. Every call is written, so call counts are exact.
        /// It is inert unless CHAITE_TRACE_FILE is set: the normal path does no
        /// I/O at all. It exists because a loaded, accepted, correctly-evaluated
        /// policy still produced a fight identical to the fixed machine on every
        /// seed, and the branch that actually ran could not be determined by
        /// reading the code.
        /// </summary>
        public static void TraceDiag(string message)
        {
            var path = Environment.GetEnvironmentVariable("CHAITE_TRACE_FILE");
            if (string.IsNullOrEmpty(path)) return;
            try { File.AppendAllText(path, message + Environment.NewLine); }
            catch { }
        }

        private static bool EnsureConfigured()
        {
            lock (Gate)
            {
                if (_configured) return true;
                // The exported 98-input MLP shares CHAITE_POLICY_FILE with this
                // loader, and CHAITE_POLICY_FORMAT decides which one owns it.
                // When the exported format is selected this loader must stand
                // down rather than try to read a float32 binary as weight
                // tokens: the scripts call ForRoute unconditionally on every
                // tick, so a hard failure here would take the whole run down,
                // and falling back to the fixed machine would silently blend
                // two controllers. With the format variable unset -- the
                // default, and the only configuration any existing caller or
                // test uses -- none of this is reached and the behaviour below
                // is unchanged.
                if (ExportedPolicy.IsExportedFormatSelected)
                {
                    _configured = true;
                    _file = null;
                    return true;
                }
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
                declaredInputs != InputCount || declaredHidden < 1 ||
                declaredHidden > MaxHiddenCount)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "learned policy file declares {0} inputs and {1} hidden" +
                    " units but this build expects {2} inputs and a width in" +
                    " 1..{3}: {4}",
                    tokens[2], tokens[3], InputCount, MaxHiddenCount, path));
            var expected = 4 + declaredHidden * InputCount + declaredHidden +
                HeadCount * declaredHidden + HeadCount;
            if (tokens.Length != expected)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "learned policy file has {0} tokens but {1} are required" +
                    " for the declared width {2}: {3}",
                    tokens.Length, expected, declaredHidden, path));

            var policy = new LearnedPolicy(declaredHidden);
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
        ///
        /// The last two entries are the dash's own state, and they were added
        /// because the policy could not otherwise express dash <em>timing</em>
        /// at all: without knowing whether a dash would even be accepted, a
        /// policy that wanted to delay one had no way to tell a held dash from a
        /// spent one. Measured on the corrected arena, the remaining phase-two
        /// body contact is exactly that — the dash fires while the Boss is still
        /// 270 px out and its i-frames are gone before the closest approach.
        /// </summary>
        public static void FillFeatures(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility, float[] destination)
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
            destination[at++] = input.PlayerRightOfBoss ? 1f : 0f;
            // The dash's own state. Unknown mobility reads as "no dash
            // available", which is the fail-closed reading: a policy must never
            // be told a dash is ready when the snapshot did not say so.
            destination[at++] = mobility != null && mobility.DashReady ? 1f : 0f;
            destination[at] = mobility != null && mobility.CanDash ? 1f : 0f;
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
        /// so the search starts from competence instead of from noise. The head
        /// offsets are unchanged; only their meaning is.</summary>
        public bool Adjust(in FormulaScriptInput input, PlayerSnapshot player,
            in TargetSnapshot boss, ArenaSnapshot arena, MobilitySnapshot mobility,
            int scriptedHorizontal,
            int scriptedVertical, bool scriptedJump, bool scriptedDash,
            out int horizontal, out int vertical, out bool jump, out bool dash)
        {
            horizontal = scriptedHorizontal;
            vertical = scriptedVertical;
            jump = scriptedJump;
            dash = scriptedDash;
            if (!Evaluate(in input, player, in boss, arena, mobility)) return false;
            ApplyActionMargin();

            var hx = ArgMax(0, 3);
            if (hx == 1) horizontal = ClampStep(scriptedHorizontal - 1);
            else if (hx == 2) horizontal = ClampStep(scriptedHorizontal + 1);
            var vy = ArgMax(3, 3);
            if (vy == 1) vertical = ClampStep(scriptedVertical - 1);
            else if (vy == 2) vertical = ClampStep(scriptedVertical + 1);
            if (ArgMax(6, 2) == 1) jump = !scriptedJump;
            // Three-way on purpose. Class one suppresses only, so a held dash
            // stays armed for a later tick; class two forces only, so the policy
            // can ask for a dash the script did not propose without that also
            // being how it holds one. With a two-way flip the two were the same
            // action, and the probe showed the cost: the policy spent the dash
            // during cruise and left the charge itself on cooldown.
            var dashHead = ArgMax(8, 3);
            if (dashHead == 1) dash = false;
            else if (dashHead == 2) dash = true;

            return true;
        }

        private static int ClampStep(int value)
        {
            return value < -1 ? -1 : (value > 1 ? 1 : value);
        }

        /// <summary>Adds the default-action margin to the class-zero slot of each
        /// of the four heads. Deliberately applied here rather than inside
        /// <see cref="Evaluate"/>, because Evaluate is shared with the older
        /// non-residual entry point, whose class zero means a concrete action
        /// rather than "leave the script alone" and must not be biased.</summary>
        private void ApplyActionMargin()
        {
            _logits[0] += ActionMargin;
            _logits[3] += ActionMargin;
            _logits[6] += ActionMargin;
            _logits[8] += ActionMargin;
        }

        /// <summary>Forward pass shared by both entry points.</summary>
        private bool Evaluate(in FormulaScriptInput input, PlayerSnapshot player,
            in TargetSnapshot boss, ArenaSnapshot arena, MobilitySnapshot mobility)
        {
            if (player == null) return false;
            FillFeatures(in input, player, in boss, arena, mobility, _features);
            for (var h = 0; h < _hiddenCount; h++)
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
                var offset = k * _hiddenCount;
                for (var h = 0; h < _hiddenCount; h++)
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