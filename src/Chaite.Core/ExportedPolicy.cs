using System;
using System.Globalization;
using System.IO;

namespace Chaite.Core
{
    /// <summary>Which on-disk format <c>CHAITE_POLICY_FILE</c> names.
    ///
    /// The residual format predates this enum and is the default, so an
    /// environment that has never heard of <c>CHAITE_POLICY_FORMAT</c> keeps the
    /// exact behaviour it had before: <see cref="LearnedPolicy"/> owns the
    /// variable. The selector exists because the two formats are both binary-
    /// incompatible and semantically different -- a 40-input residual that
    /// corrects a script versus a 98-input greedy controller that replaces it --
    /// and guessing from the file contents would silently pick a controller.</summary>
    public enum PolicyFileFormat
    {
        /// <summary>40 inputs / 11 heads, plain text, consumed by
        /// <see cref="LearnedPolicy"/> as a residual over a reviewed route.</summary>
        Residual = 0,
        /// <summary>98 inputs / 12 action logits, little-endian float32 MLP,
        /// written by <c>training/export_policy.py</c> and consumed by
        /// <see cref="ExportedPolicy"/>.</summary>
        Exported = 1
    }

    /// <summary>
    /// Loader for the trained policy that <c>training/export_policy.py</c>
    /// exports: a plain feed-forward MLP whose weights live in a data file so a
    /// training generation never recompiles the plugin.
    ///
    /// The file is little-endian throughout:
    ///
    /// <code>
    /// bytes 0..12   the 13 bytes "CHAITEPOLICY\0"
    /// uint32        format version, must be 1
    /// uint32        layer count
    /// per layer:
    ///   uint32      rows
    ///   uint32      cols
    ///   float32     rows*cols weights, row-major (row r, column c at r*cols+c)
    ///   float32     rows biases
    /// </code>
    ///
    /// Forward pass, exactly as the exporter's numpy reference implements it:
    ///
    /// <code>
    /// h = obs                                  // float32[98]
    /// for every layer except the last:
    ///     h = tanh(W @ h + b)                  // float32[rows]
    /// logits = W_last @ h + b_last             // float32[12]
    /// action = argmax(logits)                  // ties: lowest index wins
    /// </code>
    ///
    /// There is deliberately no observation normalisation. The exporter refuses
    /// to write a file for a policy that normalises, because the plugin would
    /// have to reproduce the same running statistics to be correct; the manifest
    /// carries no normalisation and this loader applies none.
    ///
    /// Every failure path throws. A configured-but-broken policy must never
    /// silently fall back to the script, because that would blend two different
    /// controllers into one run, and it must never index past the arrays a
    /// corrupt header describes.
    /// </summary>
    /// <remarks>
    /// <see cref="ChooseAction"/> and <see cref="Forward"/> reuse instance
    /// scratch buffers, so one instance is not thread-safe. Load once per
    /// process (see <see cref="ForConfiguredFile"/>) and call it from one thread,
    /// which is how the plugin's tick drives it.
    /// </remarks>
    public sealed class ExportedPolicy
    {
        /// <summary>The variable that names the policy file. Shared with
        /// <see cref="LearnedPolicy"/>; <see cref="FormatVariable"/> decides
        /// which loader owns it.</summary>
        public const string FileVariable = "CHAITE_POLICY_FILE";

        /// <summary>Selects the format of the file named by
        /// <see cref="FileVariable"/>: <c>residual</c> (the default, and the
        /// only value any earlier configuration used) or <c>exported</c>.
        /// Any other value is refused rather than guessed.</summary>
        public const string FormatVariable = "CHAITE_POLICY_FORMAT";

        public const string ExportedFormatName = "exported";
        public const string ResidualFormatName = "residual";

        /// <summary>Format version this build reads.</summary>
        public const int FormatVersion = 1;

        /// <summary>Observation width of the ORIGINAL exported format, kept as
        /// the legacy fixture's width rather than as a build constant.
        ///
        /// MEASURED 2026-09-21: this used to be enforced as "the width this build
        /// produces", which made every bridge-trained checkpoint unloadable --
        /// those are 121 wide and emit 24 logits, and the plugin refused the file
        /// before it ever reached the game. The plugin's observation width is
        /// configuration now (<c>ChaiteObservation.FromEnvironment</c>), so the
        /// width is taken from the file and checked against the configured
        /// builder by <see cref="ChaitePolicyDriver"/>, which is the only place
        /// that knows what this build actually emits.</summary>
        public const int ObservationCount = 98;

        /// <summary>Action logits of the ORIGINAL exported format. The widths
        /// this build can decode are <see cref="SupportedActionCounts"/>.</summary>
        public const int ActionCount = 12;

        /// <summary>The action alphabets the format writes down, and therefore
        /// the only output widths this build can decode. 12 is horizontal only;
        /// 24 adds the independent DOWN bit and renames jump to up, which drives
        /// the same controlJump. They are not interchangeable.</summary>
        public static readonly int[] SupportedActionCounts = { 12, 24 };

        /// <summary>Bounds so a corrupt header cannot ask for an arbitrary
        /// allocation. Not tuning knobs: a 98-input MLP is nowhere near
        /// either limit, and every allocation is additionally bounded by the
        /// length of the file that was actually read.</summary>
        public const int MaxLayerCount = 64;
        public const int MaxLayerDimension = 4096;

        /// <summary>The 13 bytes <c>"CHAITEPOLICY\0"</c>.</summary>
        private static readonly byte[] Magic =
        {
            0x43, 0x48, 0x41, 0x49, 0x54, 0x45, 0x50,
            0x4F, 0x4C, 0x49, 0x43, 0x59, 0x00
        };

        private static readonly object Gate = new object();
        /// <summary>The one loaded policy. Also the "configured" marker, so a
        /// load that throws is retried -- and therefore throws again -- on the
        /// next call instead of degrading into a silent null.</summary>
        private static ExportedPolicy _cache;

        private readonly string _sourcePath;
        private readonly int[] _rows;
        private readonly int[] _columns;
        private readonly float[][] _weights;
        private readonly float[][] _biases;
        private readonly float[] _scratchA;
        private readonly float[] _scratchB;
        // Sized from the file's own output count rather than from ActionCount:
        // the plugin drives both the legacy 98/12 MLP and the 121/24 one the
        // bridge-trained policies use, and the two differ in width.
        private readonly float[] _logits;

        private ExportedPolicy(string sourcePath, int[] rows, int[] columns,
            float[][] weights, float[][] biases)
        {
            _sourcePath = sourcePath ?? string.Empty;
            _rows = rows;
            _columns = columns;
            _weights = weights;
            _biases = biases;
            _logits = new float[rows[rows.Length - 1]];
            var widest = _logits.Length;
            for (var layer = 0; layer < rows.Length; layer++)
            {
                if (rows[layer] > widest) widest = rows[layer];
                if (columns[layer] > widest) widest = columns[layer];
            }
            _scratchA = new float[widest];
            _scratchB = new float[widest];
        }

        /// <summary>Path the instance was loaded from, for logs. Empty when the
        /// instance came from <see cref="Parse"/> with no source name.</summary>
        public string SourcePath { get { return _sourcePath; } }

        /// <summary>Number of weight layers in the file, including the final
        /// linear layer.</summary>
        public int LayerCount { get { return _rows.Length; } }

        /// <summary>Observation width read from the first layer. Always
        /// <see cref="ObservationCount"/>: anything else is refused.</summary>
        public int ObservationDimension { get { return _columns[0]; } }

        /// <summary>Action count read from the last layer. Always
        /// <see cref="ActionCount"/>: anything else is refused.</summary>
        public int OutputCount { get { return _rows[_rows.Length - 1]; } }

        /// <summary>Row counts, in application order. A copy, so a caller
        /// cannot corrupt the loaded shapes.</summary>
        public int[] LayerRows { get { return (int[])_rows.Clone(); } }

        /// <summary>Column counts, in application order. A copy.</summary>
        public int[] LayerColumns { get { return (int[])_columns.Clone(); } }

        /// <summary>Shapes as <c>"128x98, 128x128, 12x128"</c>, for a training
        /// or plugin log that has to record which layout produced a row.</summary>
        public string LayerShapes
        {
            get
            {
                var parts = new string[_rows.Length];
                for (var layer = 0; layer < _rows.Length; layer++)
                    parts[layer] = _rows[layer].ToString(
                        CultureInfo.InvariantCulture) + "x" +
                        _columns[layer].ToString(CultureInfo.InvariantCulture);
                return string.Join(", ", parts);
            }
        }

        /// <summary>Which format <see cref="FileVariable"/> currently names.
        /// Unset or empty means <see cref="PolicyFileFormat.Residual"/>, which is
        /// what every configuration used before this format existed. An
        /// unrecognised value throws rather than defaulting, because defaulting
        /// would evaluate the wrong controller against the wrong file.</summary>
        public static PolicyFileFormat ResolveFormat()
        {
            var raw = Environment.GetEnvironmentVariable(FormatVariable);
            if (string.IsNullOrEmpty(raw)) return PolicyFileFormat.Residual;
            var value = raw.Trim();
            if (string.Equals(value, ExportedFormatName,
                    StringComparison.OrdinalIgnoreCase))
                return PolicyFileFormat.Exported;
            if (string.Equals(value, ResidualFormatName,
                    StringComparison.OrdinalIgnoreCase))
                return PolicyFileFormat.Residual;
            throw new InvalidOperationException(FormatVariable + " must be '" +
                ResidualFormatName + "' or '" + ExportedFormatName +
                "', not '" + raw + "'.");
        }

        /// <summary>True when the exported format is selected. <see cref="LearnedPolicy"/>
        /// consults this so the residual loader stands down instead of trying to
        /// parse a binary as text.</summary>
        public static bool IsExportedFormatSelected
        {
            get { return ResolveFormat() == PolicyFileFormat.Exported; }
        }

        /// <summary>
        /// The exported policy named by the environment, or null when the
        /// residual format owns <see cref="FileVariable"/>.
        ///
        /// This is the plugin's entry point, and it mirrors
        /// <see cref="LearnedPolicy.ForRoute"/>: it is inert unless configured,
        /// it loads at most once per process, and it throws when it was
        /// configured but cannot be honoured. The loaded instance is cached
        /// because the weights are read-only and the file is a data file a
        /// training generation rewrites only between runs.
        /// </summary>
        public static ExportedPolicy ForConfiguredFile()
        {
            if (ResolveFormat() != PolicyFileFormat.Exported) return null;
            lock (Gate)
            {
                if (_cache != null) return _cache;
                var path = Environment.GetEnvironmentVariable(FileVariable);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException(FormatVariable + " is '" +
                        ExportedFormatName + "' but " + FileVariable + " is not" +
                        " set; there is no policy file to load.");
                _cache = Load(path);
                return _cache;
            }
        }

        /// <summary>Drops the process-wide cache. Only used by tests, which is
        /// also why it exists here rather than in a private field.</summary>
        public static void ResetCache()
        {
            lock (Gate)
            {
                _cache = null;
            }
        }

        /// <summary>Loads the file at <paramref name="path"/>. Throws
        /// <see cref="FileNotFoundException"/> when it is absent and
        /// <see cref="InvalidDataException"/> when it is not a valid version-1
        /// export.</summary>
        public static ExportedPolicy Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("policy path is empty", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "exported policy file was configured but does not exist: " +
                    path, path);
            return Parse(File.ReadAllBytes(path), path);
        }

        /// <summary>
        /// Parses an already-read export. Separated from <see cref="Load"/> so
        /// the format's failure paths can be exercised without touching disk,
        /// and so a caller that already holds the bytes does not re-read them.
        /// </summary>
        public static ExportedPolicy Parse(byte[] data, string sourcePath)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException(
                    "the exported policy format is little-endian, and this " +
                    "platform is not; the weights would be misread");
            var origin = string.IsNullOrEmpty(sourcePath)
                ? "(in-memory)" : sourcePath;
            var offset = 0;

            Require(data, offset, Magic.Length, origin);
            for (var i = 0; i < Magic.Length; i++)
            {
                if (data[offset + i] != Magic[i])
                    throw new InvalidDataException(
                        "exported policy file does not begin with the 13-byte " +
                        "CHAITEPOLICY\\0 magic: " + origin);
            }
            offset += Magic.Length;

            var version = ReadUInt32(data, ref offset, origin);
            if (version != FormatVersion)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "exported policy file declares format version {0} but this" +
                    " build reads version {1}: {2}",
                    version, FormatVersion, origin));

            var declaredLayers = ReadUInt32(data, ref offset, origin);
            if (declaredLayers < 1 || declaredLayers > MaxLayerCount)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "exported policy file declares {0} layers but 1..{1} are" +
                    " possible: {2}", declaredLayers, MaxLayerCount, origin));
            var layerCount = (int)declaredLayers;

            var rows = new int[layerCount];
            var columns = new int[layerCount];
            var weights = new float[layerCount][];
            var biases = new float[layerCount][];
            for (var layer = 0; layer < layerCount; layer++)
            {
                var declaredRows = ReadUInt32(data, ref offset, origin);
                var declaredColumns = ReadUInt32(data, ref offset, origin);
                if (declaredRows < 1 || declaredRows > MaxLayerDimension ||
                    declaredColumns < 1 || declaredColumns > MaxLayerDimension)
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "exported policy layer {0} declares {1} rows and {2}" +
                        " columns but 1..{3} are possible: {4}",
                        layer, declaredRows, declaredColumns,
                        MaxLayerDimension, origin));
                var layerRows = (int)declaredRows;
                var layerColumns = (int)declaredColumns;

                // The chain has to be checked before the bytes are copied,
                // because a layer whose input width does not match the previous
                // layer's output width would be read out of bounds by the
                // forward pass.
                if (layer > 0 && layerColumns != rows[layer - 1])
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "exported policy layer {0} reads {1} values but layer" +
                        " {2} emits {3}: {4}",
                        layer, layerColumns, layer - 1, rows[layer - 1],
                        origin));

                var weightBytes = layerRows * layerColumns * 4;
                var biasBytes = layerRows * 4;
                Require(data, offset, weightBytes + biasBytes, origin);

                var layerWeights = new float[layerRows * layerColumns];
                Buffer.BlockCopy(data, offset, layerWeights, 0, weightBytes);
                offset += weightBytes;
                var layerBiases = new float[layerRows];
                Buffer.BlockCopy(data, offset, layerBiases, 0, biasBytes);
                offset += biasBytes;

                rows[layer] = layerRows;
                columns[layer] = layerColumns;
                weights[layer] = layerWeights;
                biases[layer] = layerBiases;
            }

            // The observation width is NOT checked here any more: this build
            // emits whatever ChaiteObservation is configured for, so the file's
            // first layer is checked against that configuration by
            // ChaitePolicyDriver.RequireObservationCount, which fails at startup
            // naming the variables instead of leaving the game to run on noise.
            // What Parse can still decide alone is whether it is able to DECODE
            // the outputs, and that is the one thing a caller cannot recover
            // from later.
            var outputs = rows[layerCount - 1];
            var decodable = false;
            foreach (var width in SupportedActionCounts)
                if (outputs == width) decodable = true;
            if (!decodable)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "exported policy emits {0} action logits but this build" +
                    " writes down an encoding for {1} only: {2}",
                    outputs, string.Join("/", Array.ConvertAll(
                        SupportedActionCounts,
                        value => value.ToString(CultureInfo.InvariantCulture))),
                    origin));
            if (offset != data.Length)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "exported policy file has {0} trailing bytes after its last" +
                    " layer: {1}", data.Length - offset, origin));

            return new ExportedPolicy(sourcePath, rows, columns, weights,
                biases);
        }

        /// <summary>
        /// Greedy action for one observation: the argmax of the last layer's
        /// output, with ties going to the lowest index because the comparison is
        /// strict. <paramref name="observation"/> is not modified.
        /// </summary>
        public int ChooseAction(float[] observation)
        {
            Forward(observation, _logits);
            var best = 0;
            var bestValue = _logits[0];
            for (var i = 1; i < _logits.Length; i++)
            {
                if (_logits[i] > bestValue)
                {
                    bestValue = _logits[i];
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// The forward pass itself: tanh between layers, no activation on the
        /// last, accumulating in float32 in row-major order exactly as the
        /// format's reference implementation does. Fills the first
        /// <see cref="OutputCount"/> entries of <paramref name="logits"/>.
        /// </summary>
        public void Forward(float[] observation, float[] logits)
        {
            if (observation == null)
                throw new ArgumentNullException("observation");
            if (observation.Length != ObservationDimension)
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture,
                    "the policy expects {0} observation values but {1} were" +
                    " given", ObservationDimension, observation.Length),
                    "observation");
            if (logits == null)
                throw new ArgumentNullException("logits");
            if (logits.Length < OutputCount)
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture,
                    "the logit buffer must hold at least {0} values but holds" +
                    " {1}", OutputCount, logits.Length), "logits");

            var current = observation;
            var next = _scratchA;
            for (var layer = 0; layer < _rows.Length; layer++)
            {
                var rows = _rows[layer];
                var columns = _columns[layer];
                var weight = _weights[layer];
                var bias = _biases[layer];
                var last = layer == _rows.Length - 1;
                for (var row = 0; row < rows; row++)
                {
                    var sum = bias[row];
                    var offset = row * columns;
                    for (var column = 0; column < columns; column++)
                        sum += weight[offset + column] * current[column];
                    next[row] = last ? sum : (float)Math.Tanh(sum);
                }
                if (last)
                {
                    Array.Copy(next, logits, OutputCount);
                    return;
                }
                current = next;
                next = ReferenceEquals(next, _scratchA) ? _scratchB : _scratchA;
            }
        }

        /// <summary>
        /// Splits an action index into the controls the trainer's action space
        /// encodes, which is the only place the encoding is written down on the
        /// plugin side:
        ///
        /// <code>
        /// 12 actions (horizontal only)
        /// direction = (-1, 0, 1)[action / 4]
        /// jump      = ((action % 4) / 2) != 0
        /// dash      = (action % 2) != 0
        ///
        /// 24 actions (adds the independent DOWN bit; jump is renamed up and
        /// drives the same controlJump)
        /// direction = (-1, 0, 1)[action / 8]
        /// up        = ((action % 8) / 4) != 0
        /// down      = ((action % 4) / 2) != 0
        /// dash      = (action % 2) != 0
        /// </code>
        ///
        /// The two alphabets are not interchangeable, which is why the width is
        /// an argument rather than a build constant: the same integer means
        /// different controls under each.
        /// </summary>
        public static void DecodeAction(int actionCount, int action,
            out int direction, out bool up, out bool down, out bool dash)
        {
            var known = false;
            foreach (var width in SupportedActionCounts)
                if (actionCount == width) known = true;
            if (!known)
                throw new ArgumentOutOfRangeException("actionCount",
                    actionCount, "this build writes down an encoding for " +
                    "12 and 24 actions only");
            if (action < 0 || action >= actionCount)
                throw new ArgumentOutOfRangeException("action", action,
                    "action index must be in 0.." + (actionCount - 1));
            if (actionCount == 12)
            {
                direction = action / 4 - 1;
                up = (action % 4) / 2 != 0;
                down = false;
                dash = action % 2 != 0;
                return;
            }
            direction = action / 8 - 1;
            up = (action % 8) / 4 != 0;
            down = (action % 4) / 2 != 0;
            dash = action % 2 != 0;
        }

        /// <summary>The legacy twelve-action decode, kept so the 98/12 fixture
        /// and its tests keep one entry point. <c>jump</c> is the 12-action name
        /// for the control the 24-action alphabet calls <c>up</c>.</summary>
        public static void DecodeAction(int action, out int direction,
            out bool jump, out bool dash)
        {
            bool up;
            bool down;
            DecodeAction(12, action, out direction, out up, out down, out dash);
            jump = up;
        }

        private static uint ReadUInt32(byte[] data, ref int offset, string origin)
        {
            Require(data, offset, 4, origin);
            var value = (uint)(data[offset] | (data[offset + 1] << 8) |
                (data[offset + 2] << 16) | (data[offset + 3] << 24));
            offset += 4;
            return value;
        }

        private static void Require(byte[] data, int offset, int length,
            string origin)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "exported policy file is truncated: {0} bytes are needed at" +
                    " offset {1} but the file is {2} bytes: {3}",
                    length, offset, data.Length, origin));
        }
    }
}
