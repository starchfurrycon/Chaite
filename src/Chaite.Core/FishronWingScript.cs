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
        private const float EscapeSufficient = 96f;
        /// <summary>Horizontal gap the circuit refuses to give up. A charge is a
        /// fixed 476 px of travel, so a player standing further away than that
        /// is simply never reached and needs no dodge at all.</summary>
        private const float StandoffPixels = 720f;
        /// <summary>Centre-to-centre distance inside which the Boss body itself
        /// is the threat. A charge that ends beside the player leaves the Boss
        /// close enough that the next hover starts from contact range, and
        /// running away cannot outpace it: 6.2 px/tick against 8.5.</summary>
        private const float PersonalSpace = 150f;
        /// <summary>Signed offset large enough to count as a committed side.</summary>
        private const float CommittedOffset = 24f;
        /// <summary>Perpendicular speed large enough to count as a committed
        /// direction when the offset itself is still ambiguous.</summary>
        private const float CommittedVelocity = 0.25f;
        /// <summary>Height above the verified floor still treated as standing,
        /// so a charge that starts on the landing tick still gets the jump.</summary>
        private const float GroundedBand = 70f;
        /// <summary>Distance to the floor under which a falling player finishes
        /// the landing instead of trying to out-climb the incoming charge.</summary>
        private const float LandingBand = 170f;
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
        private bool _escapeLatched;
        private float _escapeX;
        private float _escapeY;
        private float _bandLeft;
        private float _bandRight;
        private float _floorY;
        private float _ceilingY;
        private float _lineX;
        private float _lineY;
        private float _lineDirX;
        private float _lineDirY;
        // Indexed by native state; only the hover states are used.
        private readonly int[] _hoverLimit = { 30, 30, 80, 90, 180, 30, 30, 120, 90, 180, 30, 30, 30 };

        public void Reset()
        {
            _initialized = false;
            _escapeLatched = false;
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
            MobilitySnapshot mobility)
        {
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
            if (stateEdge) _escapeLatched = false;
            if (dash && stateEdge)
            {
                // The hover that just ended tells the circuit its real length,
                // including the shortened enraged clock.
                if (_previousState >= 0 && _previousState < _hoverLimit.Length &&
                    _previousTimer > 0 && _hoverLimit[_previousState] != 0)
                    _hoverLimit[_previousState] = _previousTimer + 1;
                LatchEscape(player, in boss);
            }

            output.Accepted = true;
            output.Fire = true;
            int horizontal, vertical;
            string phase;
            if (dash && _escapeLatched)
            {
                ChargeEscape(player, in boss, out horizontal, out vertical,
                    out phase);
            }
            else
            {
                Cruise(player, in boss, state, input.NativeSequence,
                    input.NativeTimer, out horizontal, out vertical,
                    out phase);
            }
            ApplyArena(player, dash && _escapeLatched, ref horizontal,
                ref vertical);
            _patrol = horizontal == 0 ? _patrol : horizontal;

            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = vertical < 0;
            // The Shield of Cthulhu dash writes velocity.X = +-14.5 and nothing
            // else. AI_069 hovers 300 px to the side and 200 above, so its
            // charges are mostly horizontal, which makes the shield edge nearly
            // parallel to the very charge it would have to dodge. It stays a
            // reviewed route identity, not a per-charge input.
            output.Dash = false;
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

        /// <summary>The charge velocity is committed before the first dash tick,
        /// so the whole problem is the perpendicular axis. The observed offset
        /// is <em>parallel</em> to that velocity by construction: AI_069 aims
        /// the charge straight at the player, so the perpendicular component is
        /// near zero and one of the two perpendicular directions has to be
        /// chosen on its merits.
        ///
        /// The side is taken from the signed offset, or from the offset's
        /// current rate when the offset itself is still ambiguous, and only
        /// falls back to "upward" when neither is committed. Choosing a fixed
        /// side instead walks the player back across the charge line whenever
        /// they were already separating on the other one.</summary>
        private void LatchEscape(PlayerSnapshot player, in TargetSnapshot boss)
        {
            var vx = boss.Velocity.X;
            var vy = boss.Velocity.Y;
            var speed = (float)Math.Sqrt(vx * vx + vy * vy);
            if (!IsFinite(speed) || speed < 0.01f)
            {
                _escapeX = 0f;
                _escapeY = -1f;
                _escapeLatched = true;
                return;
            }
            vx /= speed;
            vy /= speed;
            _lineX = boss.Center.X;
            _lineY = boss.Center.Y;
            _lineDirX = vx;
            _lineDirY = vy;
            var cross = (player.Center.X - _lineX) * vy -
                (player.Center.Y - _lineY) * vx;
            var crossVelocity = player.Velocity.X * vy -
                player.Velocity.Y * vx;
            // (dirY, -dirX) adds +1 to the signed offset and (-dirY, dirX)
            // subtracts one, so the side is the sign of the offset, or of the
            // offset's rate. Getting this backwards walks the player across the
            // charge line, which is exactly what the dodge must never do.
            int side;
            if (Math.Abs(cross) > CommittedOffset) side = cross > 0f ? 1 : -1;
            else if (Math.Abs(crossVelocity) > CommittedVelocity)
                side = crossVelocity > 0f ? 1 : -1;
            else side = 0;
            var px = side == 0 ? 0f : side * vy;
            var py = side == 0 ? 0f : -side * vx;
            if (side == 0)
            {
                // Nothing committed either way: take the upward perpendicular.
                px = -vy;
                py = vx;
                if (py > 0f)
                {
                    px = -px;
                    py = -py;
                }
            }
            // A band edge only ever removes the horizontal component; reversing
            // it would break the perpendicularity that the dodge depends on.
            if (px < 0f && player.Position.X - _bandLeft < 320f ||
                px > 0f && _bandRight - player.Position.X < 320f)
                px = 0f;
            _escapeX = px;
            _escapeY = py;
            _escapeLatched = true;
        }

        private void ChargeEscape(PlayerSnapshot player, in TargetSnapshot boss,
            out int horizontal, out int vertical, out string phase)
        {
            Split(_escapeX, _escapeY, out horizontal, out vertical);
            var separation = PerpendicularSeparation(player);
            if (player.OnGround ||
                player.Center.Y >= _floorY - GroundedBand - player.Height * 0.5f)
            {
                // A grounded jump is the only input that produces perpendicular
                // speed on the very first tick of a charge.
                vertical = -1;
                phase = "fishron-wing-charge-ground-jump";
                return;
            }
            if (player.Velocity.Y > 3f &&
                player.Center.Y >= _floorY - LandingBand)
            {
                // Already falling the last few pixels to the floor. Reversing
                // that with wings costs about twenty ticks and the charge does
                // not have twenty ticks left; finishing the landing refills the
                // flight budget and makes the very next tick a ground jump.
                vertical = 1;
                phase = "fishron-wing-charge-landing";
                return;
            }
            if (separation >= EscapeSufficient)
            {
                // Far enough from the line. The rest of the charge is spent
                // widening the gap, because the distance at the *next* charge
                // edge is what decides whether that charge is escapable at all.
                vertical = 0;
                horizontal = AwayFromBossAxis(boss.Center.X - player.Center.X);
                phase = "fishron-wing-charge-glide";
                return;
            }
            if (vertical > 0) vertical = -1;
            phase = "fishron-wing-charge-ascent";
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
            var gap = boss.Center.X - player.Center.X;
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

        /// <summary>Perpendicular distance from the committed charge line.</summary>
        private float PerpendicularSeparation(PlayerSnapshot player) =>
            Math.Abs((player.Center.X - _lineX) * _lineDirY -
                (player.Center.Y - _lineY) * _lineDirX);

        /// <summary>Keeps the circuit inside the geometry AI_069 reads for its
        /// own enrage test. During a charge an edge only cancels the offending
        /// axis: reversing it would turn the perpendicular escape back into the
        /// charge line.</summary>
        private void ApplyArena(PlayerSnapshot player, bool chargeEscape,
            ref int horizontal, ref int vertical)
        {
            var x = player.Position.X;
            var y = player.Center.Y;
            if (x <= _bandLeft)
            {
                if (!chargeEscape || horizontal < 0) horizontal = 1;
            }
            else if (x >= _bandRight)
            {
                if (!chargeEscape || horizontal > 0) horizontal = -1;
            }
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