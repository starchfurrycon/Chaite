using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Chaite.Core
{
    /// <summary>
    /// A per-tick control sequence read from a file, so a route the enumerator
    /// composed in the forward model can be replayed through the real game.
    ///
    /// This exists because the model saying zero hits is not the acceptance
    /// criterion; the isolated probe is. Replaying a route requires issuing
    /// exactly the controls the search issued, and re-deriving them from the
    /// planner would be a different route, so the controls travel as data.
    ///
    /// The file is a header line and then one line per tick, each
    /// <c>direction,jump,dash</c> with direction in -1..1 and the other two 0 or
    /// 1. Every other control the planner would set is forced off for the
    /// duration of the replay, because a route is a claim about a specific set
    /// of inputs and leaving the rest of the planner live would make it a
    /// different claim.
    ///
    /// A file may instead carry four columns, <c>tick,direction,jump,dash</c>,
    /// and then it is read as tick-keyed with hold-last semantics. That mode
    /// exists because the positional index is not the game tick: the counter
    /// only advances on a tick the plugin actually applied a plan for, and it
    /// was measured drifting 240 -> 96965 against the absolute tick within a
    /// single session, so a positional route cannot reproduce a long fight.
    ///
    /// Five columns, <c>tick,direction,up,down,dash</c>, are the current live
    /// bridge format: the owner ruled that holding the DOWN key is a distinct
    /// input whose behaviour differs for the broom, wings, the Featherfall
    /// potion and mounts, so the action has to carry <c>up</c> and <c>down</c>
    /// as bits of their own rather than as the single horizontal
    /// <c>direction</c>. The <c>up</c> bit also drives the jump channel: the
    /// trainer's file no longer carries a separate jump field because in
    /// Terraria the up key and the jump key both drive <c>controlJump</c>, so
    /// without that mapping wings and mounts would lose their only lift input.
    /// A four-column line stays readable as the legacy
    /// <c>tick,direction,jump,dash</c>, with the two absent bits false.
    /// </summary>
    public sealed class RouteReplay
    {
        public const string FileVariable = "CHAITE_ROUTE_FILE";

        /// <summary>Applied frames to pass over before the first route tick is
        /// issued. The composed route starts at the tick its seed state was
        /// measured at, which is not the tick the probe took over on, so the gap
        /// between them has to be declared rather than guessed. Ignored by a
        /// tick-keyed file, which carries its own absolute ticks.</summary>
        public const string SkipVariable = "CHAITE_ROUTE_SKIP";

        /// <summary>When set, the route is not a file of pre-computed controls
        /// but a live one-line action file that a trainer rewrites every tick.
        /// This is the t-agent port: the policy trains inside the real engine,
        /// and its actions reach the player through exactly the same override
        /// channel a composed route uses, so what is trained is what is
        /// flown.</summary>
        public const string BridgeVariable = "CHAITE_BRIDGE_FILE";

        private readonly int[] _direction;
        private readonly bool[] _jump;
        private readonly bool[] _up;
        private readonly bool[] _down;
        private readonly bool[] _dash;
        private readonly int[] _ticks;
        private int _tickCursor;
        private readonly string _bridgeActionPath;
        private int _bridgeTick = -1;
        private int _bridgeDirection;
        private bool _bridgeJump;
        private bool _bridgeUp;
        private bool _bridgeDown;
        private bool _bridgeDash;

        public int Count { get { return _bridgeActionPath != null
            ? int.MaxValue : _direction.Length; } }
        public int Skip { get; private set; }

        /// <summary>True when the file carried absolute ticks, so lookups must
        /// go through <see cref="TryReadTick"/> rather than the frame index.
        /// </summary>
        public bool TickKeyed { get { return _ticks != null; } }

        private RouteReplay(int[] direction, bool[] jump, bool[] up, bool[] down,
            bool[] dash)
        {
            _direction = direction;
            _jump = jump;
            _up = up;
            _down = down;
            _dash = dash;
            Skip = ResolveSkip();
        }

        private RouteReplay(int[] ticks, int[] direction, bool[] jump, bool[] up,
            bool[] down, bool[] dash)
        {
            _ticks = ticks;
            _direction = direction;
            _jump = jump;
            _up = up;
            _down = down;
            _dash = dash;
            Skip = 0;
        }

        private RouteReplay(string bridgeActionPath)
        {
            _direction = null;
            _jump = null;
            _up = null;
            _down = null;
            _dash = null;
            _bridgeActionPath = bridgeActionPath;
            Skip = 0;
        }

        private static int ResolveSkip()
        {
            var raw = Environment.GetEnvironmentVariable(SkipVariable);
            int parsed;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw,
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out parsed) && parsed > 0)
                return parsed;
            return 0;
        }

        public static RouteReplay LoadFromEnvironment()
        {
            var bridge = Environment.GetEnvironmentVariable(BridgeVariable);
            if (!string.IsNullOrEmpty(bridge))
                return new RouteReplay(bridge + ".action");
            var path = Environment.GetEnvironmentVariable(FileVariable);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            return Load(path);
        }

        public static RouteReplay Load(string path)
        {
            var direction = new List<int>();
            var jump = new List<bool>();
            var up = new List<bool>();
            var down = new List<bool>();
            var dash = new List<bool>();
            var ticks = new List<int>();
            bool tickKeyed = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("direction", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("tick", StringComparison.OrdinalIgnoreCase))
                {
                    tickKeyed = true;
                    continue;
                }
                var parts = line.Split(',');
                int value;
                if (parts.Length >= 5)
                {
                    // tick,direction,up,down,dash
                    int tick;
                    if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out tick)) continue;
                    if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out value)) continue;
                    tickKeyed = true;
                    ticks.Add(tick);
                    direction.Add(Math.Max(-1, Math.Min(1, value)));
                    var upBit = parts[2].Trim() == "1";
                    up.Add(upBit);
                    down.Add(parts[3].Trim() == "1");
                    dash.Add(parts[4].Trim() == "1");
                    // The up bit is the ascend input, and the file carries no
                    // separate jump column any more: drive the jump channel
                    // from it so wings and mounts keep their lift.
                    jump.Add(upBit);
                    continue;
                }
                if (parts.Length >= 4)
                {
                    // Legacy tick,direction,jump,dash. The two columns the
                    // down-key change added are absent, so they stay false and
                    // this line means exactly what it meant before.
                    int tick;
                    if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out tick)) continue;
                    if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out value)) continue;
                    tickKeyed = true;
                    ticks.Add(tick);
                    direction.Add(Math.Max(-1, Math.Min(1, value)));
                    jump.Add(parts[2].Trim() == "1");
                    up.Add(false);
                    down.Add(false);
                    dash.Add(parts[3].Trim() == "1");
                    continue;
                }
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value)) continue;
                direction.Add(value);
                jump.Add(parts[1].Trim() == "1");
                up.Add(false);
                down.Add(false);
                dash.Add(parts[2].Trim() == "1");
            }
            if (tickKeyed)
                return new RouteReplay(ticks.ToArray(), direction.ToArray(),
                    jump.ToArray(), up.ToArray(), down.ToArray(), dash.ToArray());
            return new RouteReplay(direction.ToArray(), jump.ToArray(),
                up.ToArray(), down.ToArray(), dash.ToArray());
        }

        /// <summary>Reads the control for a tick of a tick-keyed file. The
        /// lookup holds the last recorded action, exactly like the live bridge
        /// channel, and reports false only before the file's first tick, so the
        /// caller hands those frames back to the ordinary planner.
        /// </summary>
        public bool TryReadTick(long currentTick, out int direction,
            out bool jump, out bool up, out bool down, out bool dash)
        {
            direction = 0;
            jump = false;
            up = false;
            down = false;
            dash = false;
            if (_ticks == null || _ticks.Length == 0) return false;
            while (_tickCursor + 1 < _ticks.Length &&
                   _ticks[_tickCursor + 1] <= currentTick)
                _tickCursor++;
            if (_ticks[_tickCursor] > currentTick) return false;
            direction = _direction[_tickCursor];
            jump = _jump[_tickCursor];
            up = _up[_tickCursor];
            down = _down[_tickCursor];
            dash = _dash[_tickCursor];
            return true;
        }

        /// <summary>Reads the control for an applied frame. False means the route
        /// does not cover this frame, which is how the caller knows to hand the
        /// frame back to the ordinary planner.</summary>
        public bool TryRead(int appliedFrame, out int direction, out bool jump,
            out bool up, out bool down, out bool dash)
        {
            return TryRead(appliedFrame, -1L, out direction, out jump, out up,
                out down, out dash);
        }

        /// <summary>Reads the control for a frame, using the absolute game tick
        /// when the file is tick-keyed and the applied-frame index otherwise.
        /// </summary>
        public bool TryRead(int appliedFrame, long currentTick, out int direction,
            out bool jump, out bool up, out bool down, out bool dash)
        {
            if (_bridgeActionPath != null)
                return TryReadBridge(out direction, out jump, out up, out down,
                    out dash);
            if (_ticks != null)
                return TryReadTick(currentTick, out direction, out jump, out up,
                    out down, out dash);
            direction = 0;
            jump = false;
            up = false;
            down = false;
            dash = false;
            var index = appliedFrame - Skip;
            if (index < 0 || index >= _direction.Length) return false;
            direction = _direction[index];
            jump = _jump[index];
            up = _up[index];
            down = _down[index];
            dash = _dash[index];
            return true;
        }

        /// <summary>
        /// Reads the trainer's live action file. The file holds a single line
        /// <c>tick,dir,up,down,dash</c>; the trainer rewrites it through an atomic
        /// replace, so a torn read can only mean the previous generation, which
        /// is held until a well-formed newer line arrives. A missing or
        /// malformed file yields the neutral action, never ownership of the
        /// frame: during training the policy owns every tick, including the
        /// ones before the trainer has said anything.
        /// </summary>
        private bool TryReadBridge(out int direction, out bool jump, out bool up,
            out bool down, out bool dash)
        {
            direction = 0;
            jump = false;
            up = false;
            down = false;
            dash = false;
            try
            {
                string line;
                // Share everything, including delete: the trainer replaces the
                // action file with an atomic rename every tick, and a reader
                // that holds the file with only FileShare.Read makes that
                // rename fail with access-denied. Measured: the trainer died
                // with PermissionError after 398451 ticks of a session.
                using (var stream = new FileStream(_bridgeActionPath,
                    FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                    line = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(line))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 5)
                    {
                        // tick,direction,up,down,dash. The down bit is the one
                        // this format exists for: holding DOWN is its own
                        // input in Terraria (broom descent, folded wings,
                        // Featherfall, mount descent), so it can no longer be
                        // implied by direction.
                        int tick, parsedDirection;
                        if (int.TryParse(parts[0], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out tick) &&
                            int.TryParse(parts[1], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out parsedDirection))
                        {
                            // The latest well-formed line always wins. An
                            // earlier revision also demanded tick >= the last
                            // accepted tick, and that guard is what silently
                            // broke the whole training channel: once any line
                            // carried a larger tick, every later line from a
                            // live trainer was rejected and the plan stayed
                            // neutral forever. Measured with a frozen
                            // "999999999,1,0,0": before the guard was removed
                            // the player never left a 260 px window; after it,
                            // the same file carried the player 3708 px across
                            // the runway. A replace is atomic, so a read can
                            // only return a whole generation, and acting on the
                            // previous generation for one tick is the same
                            // hold-the-last-action behaviour as before.
                            _bridgeTick = tick;
                            _bridgeDirection = Math.Max(-1, Math.Min(1,
                                parsedDirection));
                            _bridgeUp = parts[2].Trim() == "1";
                            _bridgeDown = parts[3].Trim() == "1";
                            _bridgeDash = parts[4].Trim() == "1";
                            // The file carries no jump column: the up bit is
                            // the ascend input and the trainer's 24-action
                            // space replaced the old jump bit with it, so the
                            // jump channel has to follow it or wings and
                            // mounts would have no lift at all.
                            _bridgeJump = _bridgeUp;
                            TraceBridge("accepted tick=" + tick + " dir=" +
                                _bridgeDirection + " jump=" + _bridgeJump +
                                " up=" + _bridgeUp + " down=" + _bridgeDown +
                                " dash=" + _bridgeDash);
                        }
                        else
                        {
                            TraceBridge("unparsable tick='" + parts[0].Trim() +
                                "' dir='" + parts[1].Trim() + "'");
                        }
                    }
                    else if (parts.Length == 4)
                    {
                        // Legacy tick,direction,jump,dash, which is what every
                        // route file written before the down key existed still
                        // carries. The two columns the new format added are
                        // absent, so they stay false and the line keeps its old
                        // meaning; the jump column still drives the jump
                        // channel directly.
                        int tick, parsedDirection;
                        if (int.TryParse(parts[0], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out tick) &&
                            int.TryParse(parts[1], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out parsedDirection))
                        {
                            _bridgeTick = tick;
                            _bridgeDirection = Math.Max(-1, Math.Min(1,
                                parsedDirection));
                            _bridgeJump = parts[2].Trim() == "1";
                            _bridgeUp = false;
                            _bridgeDown = false;
                            _bridgeDash = parts[3].Trim() == "1";
                            TraceBridge("accepted-legacy tick=" + tick + " dir=" +
                                _bridgeDirection + " jump=" + _bridgeJump +
                                " up=False down=False dash=" + _bridgeDash);
                        }
                        else
                        {
                            TraceBridge("unparsable tick='" + parts[0].Trim() +
                                "' dir='" + parts[1].Trim() + "'");
                        }
                    }
                    else
                    {
                        TraceBridge("malformed parts=" + parts.Length);
                    }
                }
            }
            catch (IOException e)
            {
                // The trainer replaces the file atomically, but a read racing
                // the replace is still possible on some filesystems. Holding
                // the previous action for one tick is the documented behaviour.
                TraceBridge("read-failed " + e.GetType().Name + " " + e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                TraceBridge("read-denied " + e.Message);
            }
            direction = _bridgeDirection;
            jump = _bridgeJump;
            up = _bridgeUp;
            down = _bridgeDown;
            dash = _bridgeDash;
            return true;
        }

        /// <summary>
        /// Bounded diagnostic next to the action file. A swallowed read failure
        /// is indistinguishable from a trainer that never wrote anything -- the
        /// plan simply stays neutral forever -- so the first few reads and then
        /// an occasional sample are recorded with their outcome.
        /// </summary>
        private void TraceBridge(string message)
        {
            try
            {
                if (_bridgeReads < 12 || _bridgeReads % 2000 == 0)
                {
                    File.AppendAllText(_bridgeActionPath + ".trace",
                        _bridgeReads + " " + message + Environment.NewLine);
                }
                _bridgeReads++;
            }
            catch
            {
                // Diagnostics must never affect the frame.
            }
        }

        private int _bridgeReads;
    }
}
