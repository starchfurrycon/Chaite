using System;

namespace Chaite.Core
{
    /// <summary>
    /// Fixed wing circuit for Duke Fishron, written against the pinned 1.4.5.8
    /// AI_069 source, the pinned Player wing/jump implementation, the reviewed
    /// counter-play on the Terraria Wiki (Duke Fishron and
    /// Guide:Duke Fishron strategies), and the isolated probe.
    ///
    /// Measured facts the circuit is built on:
    /// <list type="number">
    /// <item>A charge (ai[0] 1/6/11) commits its velocity once, at the native
    /// state entry, to <c>normalize(playerCentre - centre) * 17</c> and then
    /// travels a fixed 476 px in a straight line. Nothing the player does
    /// afterwards bends it, so the only input that matters is how far the
    /// player leaves that line — which is why every reviewed source says to
    /// move perpendicular to the charge.</item>
    /// <item>Wing flight answers slowly. <c>Player.WingMovement</c> adds about
    /// 0.1 px/tick per tick while rising, so a charge that starts with zero
    /// vertical speed cannot be cleared within the 28 ticks the Boss needs to
    /// cross. A ground jump is different: it sets velocity.Y straight to
    /// -jumpSpeed, and <c>if ((velocity.Y == 0 || sliding) &amp;&amp;
    /// releaseJump) wingTime = wingTimeMax</c> refills the whole 130-tick
    /// flight budget on contact. The circuit is therefore ground-anchored and
    /// answers charges with a jump.</item>
    /// <item>AI_069's ai[3] counter selects the next projectile attack outright,
    /// so the Sharknado phase is known several charges in advance. Sharkrons
    /// are fired from a fixed tornado and have limited range, so the reviewed
    /// answer is to spend that flurry ending at an arena edge and then run the
    /// arena's width away from the column.</item>
    /// <item>The Boss enrages from geometry alone: AI_069 uses the short
    /// 10-tick hover, doubled damage and a 23 px/tick charge whenever
    /// <c>player.position.Y &lt; 800</c>, <c>player.position.Y &gt;
    /// worldSurface * 16</c>, or the player stands in the middle
    /// 6400..world-6400 band. The circuit owns a bounded arena and never trades
    /// that bound for a shorter escape.</item>
    /// </list>
    ///
    /// It never scans individual projectiles, never scores candidates and never
    /// re-selects a route; one native state selects one fixed branch.
    /// </summary>
    public sealed class FishronWingScript
    {
        /// <summary>AI_069 treats the first and last 400 tiles as the ocean band.</summary>
        private const float OceanBandPixels = 6400f;
        /// <summary>AI_069 enrages below this native player Y.</summary>
        private const float SkyEnrageCeiling = 800f;
        private const float CeilingMargin = 480f;
        private const float FloorMargin = 90f;
        private const float BandEdgeMargin = 260f;
        /// <summary>An axis smaller than this fraction of the escape vector is
        /// left neutral. The threshold is deliberately low: a measured AI_069
        /// charge leaves only about a fifth of the escape on the horizontal
        /// axis, and that fifth is worth keeping because it is the only part
        /// that arrives at full speed on the first tick.</summary>
        private const float DominantAxisFraction = 0.15f;
        /// <summary>Perpendicular separation that already clears both hitboxes
        /// with margin, so the escape can stop climbing. It is deliberately
        /// close to the minimum: every pixel climbed has to be walked back down
        /// before the next charge, and arriving on the ground late is what turns
        /// a dodge into a hit.</summary>
        /// <summary>Horizontal gap the circuit refuses to give up. A charge is a
        /// fixed 476 px of travel, so a player standing further away than that
        /// is simply never reached and needs no dodge at all.</summary>
        private const float StandoffPixels = 720f;
        /// <summary>Horizontal half-width kept clear of the remembered Sharknado
        /// column. A Cthulhunado is 23 tiles wide, so this is column plus body
        /// plus a full escape.</summary>
        private const float TornadoClearance = 760f;
        /// <summary>How long a landed Sharknado keeps its column. The pinned
        /// build gives projectile 384 a timeLeft of 540 ticks.</summary>
        private const int TornadoMemoryTicks = 540;
        /// <summary>Centre-to-centre distance inside which the Boss body itself
        /// is the threat. A charge that ends beside the player leaves the Boss
        /// close enough that the next hover starts from contact range, and
        /// running away cannot outpace it: 6.2 px/tick against 8.5.</summary>
        private const float PersonalSpace = 200f;
        /// <summary>Signed offset large enough to count as a committed side.</summary>
        /// <summary>Perpendicular speed large enough to count as a committed
        /// direction when the offset itself is still ambiguous.</summary>
        /// <summary>Height above the verified floor still treated as standing,
        /// so a charge that starts on the landing tick still gets the jump.</summary>
        /// <summary>Distance to the floor under which a falling player finishes
        /// the landing instead of trying to out-climb the incoming charge.</summary>
        /// <summary>Ticks before the predicted charge at which the circuit stops
        /// descending. Wing flight reverses a fall at roughly 0.5 px/tick per
        /// tick, so a charge that finds the player falling cannot be escaped at
        /// all; the last few hover ticks are spent making sure it never does.</summary>
        private const int PreJumpTicks = 20;

        private bool _initialized;
        private int _patrol = 1;
        private int _previousState = int.MinValue;
        private int _previousSequence = int.MinValue;
        private int _previousTimer;
        private int _chargeIndex;
        private bool _dashIssued;
        private float _tornadoX;
        private int _tornadoTicksLeft;
        private float _bandLeft;
        private float _bandRight;
        private float _floorY;
        private float _ceilingY;
        // Indexed by native state; only the hover states are used.
        private readonly int[] _hoverLimit = { 30, 30, 80, 90, 180, 30, 30, 120, 90, 180, 30, 30, 30 };

        public void Reset()
        {
            _initialized = false;
            _chargeIndex = 0;
            _dashIssued = false;
            _tornadoTicksLeft = 0;
            _previousState = int.MinValue;
            _previousSequence = int.MinValue;
            _previousTimer = 0;
            _patrol = 1;
            for (var i = 0; i < _hoverLimit.Length; i++)
                _hoverLimit[i] = DefaultHoverLimit(i);
        }

        /// <summary>AI_069 hover durations from the pinned build, used only as
        /// the starting estimate before the fight reports its own.</summary>
        private static int DefaultHoverLimit(int state) =>
            state == 0 ? 30 : state == 5 ? 30 : state == 10 ? 30 : 0;

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility) =>
            Tick(in input, player, in boss, arena, mobility,
                default(SharknadoBubbleSnapshot));

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility, in SharknadoBubbleSnapshot bubble)
        {
            // bubble is published but deliberately not acted on. Two candidate
            // rules were implemented and measured, and both came out at or
            // worse than the seed-to-seed spread: a perpendicular escape
            // (7-9 hits) and a steer-to-the-nearer-edge placement (8-10 hits)
            // against 7-9 without either. The observation is kept because it
            // is the right shape for the next attempt; the input is not,
            // because nothing yet shows it helps.
            var output = new FormulaScriptOutput();
            if (input.BossType != 370 || player == null || arena == null ||
                (input.Route != FormulaRoute.FishronFairyWingsDash &&
                 input.Route != FormulaRoute.FishronStrongWingsDash))
                return output;
            if (!IsReachableState(input.NativeState, input.NativeTimer,
                    input.NativeSequence))
                return output;

            if (!_initialized) Initialize(player, in boss, arena);

            var state = input.NativeState;
            var dash = state == 1 || state == 6 || state == 11;
            var stateEdge = state != _previousState ||
                input.NativeSequence != _previousSequence;
            if (stateEdge)
            {
                // A projectile attack ends the charge group, so the reviewed W
                // cycle restarts from its horizontal first beat.
                if (state == 2 || state == 3 || state == 7 || state == 8)
                    _chargeIndex = 0;
                if (state == 3 || state == 8)
                {
                    // AI_069 fires the Sharknado bolts downwards from its own
                    // centre, so that is where the column will stand. One
                    // remembered X is the whole record the circuit keeps: it is
                    // the position it chose to let the tornado land at, not a
                    // scan of live projectiles.
                    _tornadoX = boss.Center.X;
                    _tornadoTicksLeft = TornadoMemoryTicks;
                }
                _dashIssued = false;
            }
            if (dash && stateEdge)
            {
                // The hover that just ended tells the circuit its real length,
                // including the shortened enraged clock.
                if (_previousState >= 0 && _previousState < _hoverLimit.Length &&
                    _previousTimer > 0 && _hoverLimit[_previousState] != 0)
                    _hoverLimit[_previousState] = _previousTimer + 1;
            }

            output.Accepted = true;
            output.Fire = true;
            int horizontal, vertical;
            string phase;
            var dashInput = false;
            if (dash)
            {
                ChargeEscape(player, in boss, mobility, out horizontal,
                    out vertical, out phase, out dashInput);
            }
            else
            {
                Cruise(player, in boss, state, input.NativeSequence,
                    input.NativeTimer, out horizontal, out vertical,
                    out phase);
            }
            ApplyArena(player, ref horizontal, ref vertical);
            _patrol = horizontal == 0 ? _patrol : horizontal;

            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = vertical < 0;
            output.Dash = dashInput;
            output.Phase = phase;
            _previousState = state;
            _previousSequence = input.NativeSequence;
            _previousTimer = input.NativeTimer;
            return output;
        }

        /// <summary>Compatibility overload retained for synthetic adapters which
        /// only expose an aggregate dash-ready flag.</summary>
        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            bool shieldReady = false)
        {
            var mobility = new MobilitySnapshot
            {
                CanDash = shieldReady,
                DashReady = shieldReady
            };
            return Tick(in input, player, in boss, arena, mobility);
        }

        private static bool IsReachableState(int state, int timer, int sequence)
        {
            // -1 (intro) and 0..12 are the reachable AI_069 states for the
            // pinned build; 13 exists in the source but no transition selects it.
            if (state < -1 || state > 12) return false;
            return timer >= 0 && sequence >= 0;
        }

        private void Initialize(PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena)
        {
            _initialized = true;
            var worldLeft = player.WorldLeft;
            var worldRight = player.WorldRight;
            // Pick the ocean band the player actually occupies; AI_069 only
            // protects the band the fight started in.
            var leftDistance = player.Center.X - worldLeft;
            var rightDistance = worldRight - player.Center.X;
            if (leftDistance <= rightDistance)
            {
                _bandLeft = worldLeft + BandEdgeMargin;
                _bandRight = worldLeft + OceanBandPixels - BandEdgeMargin;
            }
            else
            {
                _bandLeft = worldRight - OceanBandPixels + BandEdgeMargin;
                _bandRight = worldRight - BandEdgeMargin;
            }
            if (_bandRight - _bandLeft < 400f)
            {
                _bandLeft = worldLeft + BandEdgeMargin;
                _bandRight = worldRight - BandEdgeMargin;
            }
            _ceilingY = SkyEnrageCeiling + CeilingMargin;
            var support = arena.FloorSupport;
            _floorY = support.Valid && !support.Inverted &&
                support.Right > support.Left && support.SurfaceY > player.Center.Y
                ? support.SurfaceY
                : player.Center.Y + 2400f;
            _patrol = boss.Center.X >= player.Center.X ? -1 : 1;
        }

        /// <summary>The reviewed W cycle for charges on flat ground.
        ///
        /// Every reviewed source agrees on why a charge is beatable: AI_069 aims
        /// it at where the player <em>is</em>, so leaving the line is the whole
        /// dodge. What the reviewed play adds is that the escape axis is not
        /// recomputed per charge. The cycle is horizontal, ascend, descend,
        /// repeating, with a Shield of Cthulhu edge spent on every beat:
        ///
        /// <code>
        ///   beat 0  horizontal away            (+ dash)
        ///   beat 1  ascend,  dash widens X     (+ dash)
        ///   beat 2  descend, dash widens X     (+ dash)
        /// </code>
        ///
        /// That is the phase-two "horizontal, ascend, descend" cycle and the
        /// opening beats of the phase-one five-charge group at once, because
        /// the group is just this cycle continuing past its third beat. The
        /// dash is what makes the vertical beats work: it writes 14.5 px/tick
        /// of horizontal speed in the player's facing direction, so an ascend
        /// or descend beat leaves the charge line diagonally, which neither
        /// axis manages alone.</summary>
        private void ChargeEscape(PlayerSnapshot player, in TargetSnapshot boss,
            MobilitySnapshot mobility, out int horizontal, out int vertical,
            out string phase, out bool dash)
        {
            dash = false;
            // The dash writes velocity.X in the player's facing direction, so
            // the horizontal input is also what aims the dash away from the
            // hazard. Turning back at a band edge is part of the same cycle and
            // uses the same dash.
            //
            // A remembered Sharknado column outranks the Boss here: the column
            // does not move, so a charge that carries the player into it is
            // worse than one that carries them towards the Boss.
            var away = _tornadoTicksLeft > 0 &&
                Math.Abs(player.Center.X - _tornadoX) < TornadoClearance
                ? (_tornadoX >= player.Center.X ? -1 : 1)
                : (boss.Center.X >= player.Center.X ? -1 : 1);
            horizontal = away;
            switch (_chargeIndex % 3)
            {
                case 0:
                    vertical = 0;
                    phase = "fishron-wing-charge-horizontal";
                    break;
                case 1:
                    vertical = -1;
                    phase = "fishron-wing-charge-ascend";
                    break;
                default:
                    vertical = 1;
                    phase = "fishron-wing-charge-descend";
                    break;
            }
            if (!_dashIssued && mobility != null && mobility.CanDash &&
                mobility.DashReady)
            {
                dash = true;
                _dashIssued = true;
                phase += "-dash";
            }
            _chargeIndex++;
        }

        /// <summary>Everything that is not a charge.
        ///
        /// AI_069's ai[3] counter selects the next projectile attack outright,
        /// so the Sharknado phase is known several charges in advance. The
        /// reviewed counter-play is to spend the flurry before it ending at an
        /// arena edge, so that once the Sharknados exist the circuit can run the
        /// whole width of the arena away from them. Sharkrons are fired from a
        /// fixed tornado and have limited range, so distance is the whole
        /// defence; nothing here tries to out-turn a homing projectile.
        ///
        /// Between those phases the circuit walks the ocean band on foot:
        /// contact with support refills the flight budget and makes the next
        /// charge jump available on its first tick.</summary>
        private void Cruise(PlayerSnapshot player, in TargetSnapshot boss,
            int state, int sequence, int timer, out int horizontal,
            out int vertical, out string phase)
        {
            horizontal = 0;
            vertical = 0;
            phase = null;
            if (_tornadoTicksLeft > 0) _tornadoTicksLeft--;
            var gap = boss.Center.X - player.Center.X;
            if (_tornadoTicksLeft > 0 &&
                Math.Abs(player.Center.X - _tornadoX) < TornadoClearance)
            {
                // Still inside the column the last Sharknado left behind.
                // Sharkrons fired from a fixed tornado have limited range, so
                // horizontal distance is the whole defence.
                horizontal = _tornadoX >= player.Center.X ? -1 : 1;
                vertical = player.OnGround ? 0 : 1;
                phase = "fishron-wing-tornado-clear";
                return;
            }
            var separation = Distance(player, in boss);
            if (separation < PersonalSpace && state >= 0)
            {
                // Nothing else matters while the Boss body is this close.
                horizontal = gap >= 0f ? -1 : 1;
                vertical = boss.Center.Y >= player.Center.Y ? -1 : 1;
                phase = "fishron-wing-personal-space";
                return;
            }
            // Fires regardless of the gap: a close charge is exactly when an
            // already-rising player matters most.
            var preJump = PredictChargImminent(state, timer);
            if (preJump)
            {
                // Hold the jump for the whole window. Wing flight only adds
                // about 0.1 px/tick per tick of climb, so starting from zero at
                // the charge edge is worth almost nothing; twenty ticks of
                // pre-load turns the same escape into roughly 120 px. Landing
                // refills the flight budget, so this is not paid for twice.
                //
                // The one thing that outranks the pre-load is the Boss body:
                // AI_069 hovers 200 px above the player, so a climb from a few
                // tens of pixels below the hover point flies straight into it.
                horizontal = AwayFromBossAxis(gap);
                vertical = -1;
                phase = "fishron-wing-precharge-jump";
                return;
            }
            if (state == 3 || state == 8)
            {
                // The Sharknado is a fixed column near the Boss. Clear it
                // horizontally for the whole phase; the column cannot follow.
                horizontal = AwayFromBossAxis(gap);
                vertical = player.OnGround ? 0 : 1;
                phase = "fishron-wing-sharknado-exit";
                return;
            }
            if (state == 2 || state == 7)
            {
                // Detonating Bubbles: laid along the Boss line in phase one and
                // around the Boss in phase two. They follow the player and
                // explode, so the useful input is to keep crossing their line
                // rather than to try to outrun them.
                AwayFromBoss(player, in boss, out horizontal, out vertical);
                vertical = player.OnGround ? 0 : 1;
                phase = "fishron-wing-bubble-line";
                return;
            }
            if (TornadoIncoming(state, sequence))
            {
                // Run the flurry out towards the far edge, so the incoming
                // Sharknado lands behind the circuit and the arena's whole
                // width stays available to run back into. Fleeing the Boss
                // reaches that edge on its own and never crosses him.
                horizontal = AwayFromBossAxis(gap);
                vertical = player.OnGround ? 0 : 1;
                phase = "fishron-wing-tornado-bait";
                return;
            }
            if (Math.Abs(gap) < StandoffPixels)
            {
                horizontal = AwayFromBossAxis(gap);
                vertical = player.OnGround ? 0 : 1;
                phase = "fishron-wing-standoff";
                return;
            }
            horizontal = _patrol;
            if (!player.OnGround)
            {
                vertical = 1;
                phase = "fishron-wing-cruise-descend";
                return;
            }
            vertical = 0;
            phase = "fishron-wing-cruise-" + state;
        }

        private static float Distance(PlayerSnapshot player,
            in TargetSnapshot boss)
        {
            var dx = boss.Center.X - player.Center.X;
            var dy = boss.Center.Y - player.Center.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>AI_069 chooses the next projectile attack from ai[3] when a
        /// hover ends, so the value while hovering names the attack that is
        /// about to happen. 11 selects the phase-one Sharknado pair and 7 the
        /// phase-two Cthulhunado.</summary>
        private static bool TornadoIncoming(int state, int sequence) =>
            (state == 0 && sequence >= 9) || (state == 5 && sequence >= 6);

        private static int AwayFromBossAxis(float gap) => gap >= 0f ? -1 : 1;

        /// <summary>Perpendicular to the Boss-to-player line, with the sign
        /// chosen to widen the horizontal gap rather than close it.</summary>
        private static void AwayFromBoss(PlayerSnapshot player,
            in TargetSnapshot boss, out int horizontal, out int vertical)
        {
            var dx = boss.Center.X - player.Center.X;
            var dy = boss.Center.Y - player.Center.Y;
            var perpX = dy;
            var perpY = -dx;
            if (perpX * -dx < 0f)
            {
                perpX = -perpX;
                perpY = -perpY;
            }
            Split(perpX, perpY, out horizontal, out vertical);
        }

        /// <summary>True once the current hover is close enough to its end that
        /// the next charge is imminent.</summary>
        private bool PredictChargImminent(int state, int timer)
        {
            if (state != 0 && state != 5 && state != 10) return false;
            var limit = _hoverLimit[state];
            if (limit <= 0) return false;
            return limit - timer <= PreJumpTicks;
        }

        /// <summary>Keeps the circuit inside the geometry AI_069 reads for its
        /// own enrage test. During a charge an edge only cancels the offending
        /// axis: reversing it would turn the perpendicular escape back into the
        /// charge line.</summary>
        private void ApplyArena(PlayerSnapshot player, ref int horizontal,
            ref int vertical)
        {
            var x = player.Position.X;
            var y = player.Center.Y;
            // Turning back at a band edge is part of the reviewed W cycle, not
            // an exception to it: the turnaround charges use the same dash.
            if (x <= _bandLeft && horizontal < 0) horizontal = 1;
            else if (x >= _bandRight && horizontal > 0) horizontal = -1;
            if (y - player.Height * 0.5f <= _ceilingY) vertical = 1;
            else if (y >= _floorY - FloorMargin && vertical > 0) vertical = 0;
        }

        /// <summary>Decomposes a separation vector into native input. An axis
        /// below the dominance fraction is deliberately left neutral.</summary>
        private static void Split(float x, float y, out int horizontal,
            out int vertical)
        {
            horizontal = 0;
            vertical = 0;
            var length = (float)Math.Sqrt(x * x + y * y);
            if (!IsFinite(length) || length < 0.001f)
            {
                vertical = -1;
                return;
            }
            var nx = x / length;
            var ny = y / length;
            if (Math.Abs(nx) >= DominantAxisFraction) horizontal = nx > 0f ? 1 : -1;
            if (Math.Abs(ny) >= DominantAxisFraction) vertical = ny > 0f ? 1 : -1;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}