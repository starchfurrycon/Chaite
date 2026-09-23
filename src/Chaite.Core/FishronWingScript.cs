using System;
using System.Globalization;

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

        /// <summary>Closed-loop leg schedule, in charges per one-direction leg.
        ///
        /// Why this knob exists, measured rather than reasoned: the reviewed
        /// circuit's horizontal decision is "run away from the Boss", so the
        /// player advances a fixed 483 px on every 60-tick charge and turns around
        /// only when the arena stops it or when the Boss crosses it. A full band
        /// traverse is therefore 11.80 charges, and the legs measured from the
        /// plan's own horizontal are 6, 9, 11-12 and 7, while AI_069's attack group
        /// is 5 charges in phase one and 3 in phase two. No leg length is a
        /// multiple of either group, so the pattern's phase relative to the Boss
        /// precesses by a different amount every group and a cycle can
        /// never return to its own start. The related band-edge experiment
        /// recorded in <see cref="Initialize"/> failed for exactly this reason --
        /// its own note says it "desynchronises the W cycle from the Boss's
        /// attack clock" -- so the fix is not to move the edge again but to make
        /// the turnaround happen on a charge count instead of on a wall.
        ///
        /// Unset, non-numeric, or below 1 keeps the reviewed circuit exactly:
        /// the leg ends where the arena ends. That default path is what makes the
        /// branch measurable -- an isolated probe run with the variable unset has
        /// to reproduce the previous build's ticks/hits/damage unchanged, and a
        /// run with it set has to differ, or the branch was not executed.
        ///
        /// MEASURED OUTCOME: the naive form of this schedule is refuted and must
        /// not be switched on expecting it to help. Isolated probe waves on one
        /// build, three seeds each (duke-fishron / fishron-fairy-wing / expert):
        /// the unset run reproduced the previous build digit for digit
        /// (win 4932 ticks 3 hits 78000 damage, 4030/9/59781, 3640/9/57322), so
        /// the default path is untouched; leg=5 and leg=3 changed the plan's
        /// horizontal at exactly the (k+1)-th charge in every seed (tick 755 for
        /// k=5, tick 525 for k=3, same phase name, same jump), so the branch did
        /// execute; and the schedule itself works -- measured by the plan's own
        /// horizontal, leg=3 produced seven to eight exact 3-charge legs in every
        /// seed, a genuinely periodic W. It still did not win: hits were capped at
        /// 8 (8/8/8 against a 3/9/9 baseline), boss damage fell in seven of nine
        /// cells, leg=5 lost the seed the reviewed circuit won
        /// (4932/3/78000 -> 3337/8/45926), and the hit mix was traded rather than
        /// reduced -- Boss-body hits rose with reversal frequency
        /// (1/3/5 baseline, 3/4/2 at k=5, 5/5/5 at k=3) while bubble hits fell.
        /// So periodicity alone is not sufficient, because the turnaround itself
        /// creates body contact. The mechanism is that this circuit's horizontal
        /// rule is reactive -- it flees every charge and so is almost never on the
        /// charge line -- whereas a leg count makes the player run the other way
        /// on the turnaround charge. Scheduling the turnaround is also not
        /// sufficient on its own: leg=5 broke into 3 1 1 5 1 1 1 1 after two exact
        /// legs and the advance per charge fell from 483 px to 306-454, because
        /// only the charge branch obeys the leg while personal-space, standoff and
        /// tornado-clear still override it and hitstun erases the displacement.
        /// Three conditions have
        /// to hold before this is retried: the pattern period must divide the
        /// attack group (5 charges in phase one, 3 in phase two, so phase one
        /// needs a period of 5 and a combined lcm(2k,3,5) of 30 charges at k=5),
        /// a turnaround must be gated on a measured safe distance rather than on
        /// a derived one, and the pattern must own the horizontal in every
        /// branch.</summary>
        private const string LegChargesVariable = "CHAITE_LOOP_LEG_CHARGES";

        /// <summary>Per-charge horizontal pattern for the closed loop, one entry
        /// per charge within an attack group.
        ///
        /// Why this shape and not a leg count: the leg experiment established two
        /// things. A periodic horizontal pattern is achievable (leg=3 produced
        /// seven to eight exact 3-charge legs per seed), and periodicity alone
        /// does not help because the turnaround charge runs the player into the
        /// Boss body. So the pattern has to be periodic AND every charge that
        /// points at the Boss has to be one the circuit already answers with a
        /// jump.
        ///
        /// Measured beat assignment inside a group (seed 1, phase one, five
        /// charges): charges 0..4 run beats 0,1,2,0,1, so charges 0 and 3 are the
        /// horizontal beat and are not jumped while charges 1,2 and 4 are the
        /// ascend/descend beats and are. A pattern of +1/-1/0 therefore has a
        /// structural safety rule available: leave every non-jumped charge on +1
        /// (the reviewed flee) and put the -1 strokes only on jumped charges.
        ///
        /// Measured timer ranges the pattern rides on: a charge state's ai[2]
        /// reaches 26 and a hover state's reaches 30 (37 once the circuit has
        /// learned an extended hover), which is the measured 60-tick cadence.
        ///
        /// Format: comma-separated entries from {+1, 0, -1}, optionally two
        /// groups separated by '|' where the first is used for charge family 1
        /// (ai[0]==1) and the second for families 2 and 3 (ai[0]==6 or 11).
        /// A single group applies to every family. Unset, malformed, or an empty
        /// group disables the whole branch and leaves the reviewed circuit, so a
        /// probe run without the variable has to reproduce the previous build
        /// exactly and a run with it has to differ.
        ///
        /// MEASURED OUTCOME: refuted, and more decisively than the leg count was.
        /// Isolated probe waves on one build, three seeds each (duke-fishron /
        /// fishron-fairy-wing / expert). With the variable unset the run
        /// reproduced the previous build digit for digit (win 4932 ticks 3 hits
        /// 78000 damage, 4030/9/59781, 3640/9/57322), so the branch really is off
        /// by default. With "1,-1,0,1,-1|1,-1,0" the plan first differs at tick
        /// 405 in every seed -- the second charge, the first -1 entry, same phase
        /// name and same jump, only the horizontal flipped -- and all three runs
        /// collapsed to nearly the same fight (1672/8/19464, 1672/8/20839,
        /// 1636/7/22413) against 4932/3/78000, 4030/9/59781, 3640/9/57322, with
        /// the advance per charge falling from 483 px to 257.7/257.7/264.2.
        /// Every single hit became Boss-body contact: type 370 counts went
        /// 1/3/5 to 8/8/7 while bubbles and Sharkrons went to zero.
        ///
        /// The schedule itself executed exactly (pattern legs 1 1 1 2 1 1 2 2
        /// against the reviewed circuit's 6 9 11 7), so this is not a case of the
        /// branch failing to run -- it is a precisely executed schedule losing
        /// outright.
        ///
        /// What that establishes: the horizontal direction during a charge is not
        /// a free parameter. Pointing it at the Boss produces body contact even on
        /// a beat that jumps, because the wing ascent only adds about 0.1 px/tick
        /// per tick of climb while the charge is a fixed 476 px of travel aimed at
        /// where the player entered it, and fleeing along the line clears that by
        /// only 7 px. So the width and frequency of the W cannot be scheduled as a
        /// horizontal direction; the only horizontally schedulable freedom left is
        /// in the hover, and even there it is bounded, because the hover's
        /// direction sets the gap the next charge starts from. Safe use of this
        /// knob would have to reverse only while no charge is in flight and only
        /// past a measured distance gate.</summary>
        private const string BeatPatternVariable = "CHAITE_LOOP_BEAT_PATTERN";

        /// <summary>The one branch name that carries a latched direction.</summary>
        private const string PersonalSpacePhase = "fishron-wing-personal-space";

        private bool _initialized;
        private int _patrol = 1;
        private bool _personalSpaceLatched;
        private int _personalSpaceHorizontal;
        private int _personalSpaceVertical;
        private int _previousState = int.MinValue;
        private int _previousSequence = int.MinValue;
        private int _previousTimer;
        private int _chargeIndex;
        private int _chargeBeat;
        private bool _dashIssued;
        // Closed-loop leg schedule. _legCharges is the configured leg length in
        // charges (0 disables the whole branch); _legDirection is the committed
        // direction of the current leg; _legProgress counts the charges spent in
        // it; _legFamily remembers which charge family (ai[0] 1/6/11) the leg
        // belongs to, so a phase change starts a fresh leg instead of continuing
        // the previous phase's count.
        private int _legCharges;
        private int _legDirection;
        private int _legProgress;
        private int _legFamily;
        // Closed-loop horizontal pattern, indexed by the charge's position inside
        // its attack group. _patternFirst covers charge family 1 and _patternRest
        // covers families 2 and 3; either may be empty, which disables the branch
        // for that family. _patternDirection is the entry chosen for the charge
        // currently being planned.
        private int[] _patternFirst = new int[0];
        private int[] _patternRest = new int[0];
        private int _patternDirection;
        private bool _patternActive;
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
            _chargeBeat = 0;
            _dashIssued = false;
            _legDirection = 0;
            _legProgress = 0;
            _legFamily = 0;
            _patternDirection = 0;
            _patternActive = false;
            _tornadoTicksLeft = 0;
            _previousState = int.MinValue;
            _previousSequence = int.MinValue;
            _previousTimer = 0;
            _patrol = 1;
            _personalSpaceLatched = false;
            _personalSpaceHorizontal = 0;
            _personalSpaceVertical = 0;
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
                // The beat is chosen once per charge, not once per tick. The
                // distance between the Boss and the player is tiny compared to
                // 14.5 px/tick of dash, so a beat that changes mid-charge walks
                // the player straight through the Boss body.
                _chargeBeat = _chargeIndex % 3;
                _chargeIndex++;
                // The pattern is indexed by the charge's position in its group,
                // which is the same index the beat came from. Both are reset at
                // the group boundary above, so the jump lands on the same charge
                // of every group and a -1 stroke can be placed only on a jumped
                // one.
                var family = state == 1 ? 1 : state == 6 ? 2 : 3;
                var pattern = PatternFor(family);
                _patternActive = pattern.Length > 0;
                if (_patternActive)
                {
                    var slot = (_chargeIndex - 1) % pattern.Length;
                    _patternDirection = pattern[slot];
                }
                else
                {
                    _patternDirection = 0;
                }
                // The hover that just ended tells the circuit its real length,
                // including the shortened enraged clock.
                if (_previousState >= 0 && _previousState < _hoverLimit.Length &&
                    _previousTimer > 0 && _hoverLimit[_previousState] != 0)
                    _hoverLimit[_previousState] = _previousTimer + 1;
                if (_legCharges > 0 && pattern.Length == 0)
                {
                    // One leg lasts exactly _legCharges charges, so the pattern's
                    // horizontal phase repeats on a count instead of on wherever
                    // the arena happens to stop the player. The first leg of each
                    // charge family still starts by fleeing, which is the reviewed
                    // choice; only the turnaround is scheduled. The pattern above
                    // takes precedence when both are configured.
                    if (family != _legFamily)
                    {
                        _legFamily = family;
                        _legProgress = 0;
                    }
                    if (_legProgress == 0)
                    {
                        _legDirection = AwayFromBossAxis(boss.Center.X - player.Center.X);
                        _legProgress = 1;
                    }
                    else
                    {
                        _legProgress++;
                        if (_legProgress > _legCharges)
                        {
                            _legDirection = -_legDirection;
                            _legProgress = 1;
                        }
                    }
                }
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
            // A latched escape lives only as long as the episode that set it,
            // so the next close pass chooses its side from its own geometry.
            if (phase != PersonalSpacePhase) _personalSpaceLatched = false;

            // A trained policy for this route replaces only the movement
            // decision. Route, form and mount admission above are untouched,
            // because that part is already verified.
            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = vertical < 0;
            output.Dash = dashInput;
            output.Phase = phase;
            // The script's own proposal, kept so the latch below can tell a dash
            // it may actually spend from one the residual forced on a tick where
            // nothing was ready. Without this the residual could latch the
            // charge's single dash on a tick the engine ignores, and the
            // legitimate dash would then be lost for the rest of the charge.
            var dashProposed = dashInput;
            DecideMovement(in input, player, in boss, arena, mobility, ref output);
            // Issuing is what spends the charge's one dash; a held proposal
            // stays available on the next tick, which is what makes the timing
            // searchable. With no policy configured this latches on the first
            // ready tick exactly as it always did, so the fixed circuit is
            // unchanged and only the reachable set grows.
            if (output.Dash && dashProposed)
            {
                _dashIssued = true;
                if (output.Phase != null &&
                    !output.Phase.EndsWith("-dash", StringComparison.Ordinal))
                    output.Phase += "-dash";
            }
            _previousState = state;
            _previousSequence = input.NativeSequence;
            _previousTimer = input.NativeTimer;
            return output;
        }

        /// <summary>
        /// Applies the trained residual to the scripted decision computed just
        /// above, and only to that decision. The script is the base and the
        /// network may only correct it, so zero weights reproduce the fixed
        /// circuit exactly and the search starts from that circuit's measured
        /// behaviour rather than from a random walk. Route, form and mount
        /// admission, the charge-beat bookkeeping and the hover-limit learning
        /// above are not reachable from here. A policy exception is
        /// deliberately not caught: a configured-but-broken policy must fail
        /// loudly rather than silently revert to the fixed machine.
        /// </summary>
        private static void DecideMovement(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility, ref FormulaScriptOutput output)
        {
            var learned = LearnedPolicy.ForRoute(input.Route);
            if (learned == null) return;
            var scriptedPhase = output.Phase;
            int horizontal, vertical;
            bool jump, dash;
            if (!learned.Adjust(in input, player, in boss, arena, mobility,
                    output.Horizontal, output.Vertical, output.Jump,
                    output.Dash, out horizontal, out vertical, out jump,
                    out dash))
                return;
            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = jump;
            output.Dash = dash;
            output.Phase = LearnedPolicy.ComposeLearnedPhase(
                scriptedPhase, "fishron-wing-learned");
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
            _legCharges = ReadLegCharges();
            ReadBeatPattern();
            var worldLeft = player.WorldLeft;
            var worldRight = player.WorldRight;
            // Pick the ocean band the player actually occupies; AI_069 only
            // protects the band the fight started in.
            //
            // Both edges are deliberately *inside* the native world-border
            // keep-out: Player.BordersMovement clamps the position to
            // leftWorld + 640 and to rightWorld - 640 - width, so the left
            // edge here can never be reached. The engine does NOT turn the
            // player around at that clamp: it pins the position and zeroes
            // that axis of the velocity, while the circuit goes on holding
            // the outward input until its own phase changes. The pinned
            // frames are measured in docs/continuation-20260915.md section
            // 17, together with the two refutations of moving this edge
            // inward. Moving the edge out to the border was
            // measured (see docs/formula-routes.md): it removes the cheap
            // bubble hits that cluster there, but every seed then dies to a
            // Cthulhunado at the end of the runway. The unreachable edge is
            // load-bearing and must not be "fixed" without answering that.
            //
            // That answer has since been measured twice, and the dwell turns
            // out to be a timing buffer, not just a hazard. The first attempt
            // moved the edge to the clamp itself; the second (docs
            // continuation-20260915.md section 17) moved it to clamp + 260 so
            // the player never touches the wall at all, on the theory that the
            // pinned frames are 2.4x more likely to precede a hit. Both scored
            // far worse: the second went 0 wins in 20 against a 10-in-20
            // baseline, and it *raised* the hit rate, from about one per 470
            // ticks to one per 380, shortening runs from 4000 ticks to 3100.
            // The 2.4x figure is a correlation measured at frames where the
            // cycle happens to be holding station; removing the station
            // desynchronises the W cycle from the Boss's attack clock and costs
            // more than the pinned frames ever did. Do not retry this family
            // without preserving the cycle timing.
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
            // Priority: the remembered Sharknado column still outranks everything,
            // because it does not move and a stroke that carries the player into
            // it is worse than one that carries them towards the Boss. Then the
            // closed-loop pattern if one is configured for this family, then the
            // reviewed leg schedule if that is configured instead, and otherwise
            // the reviewed flee.
            var tornadoOverrides = _tornadoTicksLeft > 0 &&
                Math.Abs(player.Center.X - _tornadoX) < TornadoClearance;
            if (tornadoOverrides)
            {
                horizontal = away;
            }
            else if (_patternActive)
            {
                // +1 is the reviewed flee, -1 is its opposite, and 0 is a neutral
                // charge. Active is tracked separately from the value because 0 is
                // a legitimate entry: reading the value alone would turn a
                // configured neutral charge back into a flee.
                horizontal = _patternDirection == 0 ? 0 : (_patternDirection > 0 ? away : -away);
            }
            else if (_legCharges > 0 && _legDirection != 0)
            {
                horizontal = _legDirection;
            }
            else
            {
                horizontal = away;
            }
            switch (_chargeBeat)
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
            // A ready dash is *proposed* here, never issued. Issuing happens
            // after the trained residual has had its say (see Tick), because
            // latching here makes a hold indistinguishable from a burn: the
            // reachable set would be "dash on the first ready tick" or "not at
            // all this charge", and the timing the remaining body contacts need
            // is not in that set. Measured on the corrected arena, the single
            // remaining phase-two body contact happens because the dash fires
            // 270 px out and its i-frames are spent before the closest approach,
            // which is exactly a timing the old latch could not express.
            if (!_dashIssued && mobility != null && mobility.CanDash &&
                mobility.DashReady)
                dash = true;
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
                //
                // The escape side is latched once per episode, not re-derived
                // from the Boss's current side. AI_069 crosses the player on
                // its way through a charge, so a per-frame sign flips exactly
                // while the body is on top of the player -- and that is the
                // frame where the raw difference is pure noise: one death
                // frame decided the whole escape on a 0.7 px gap. The latch
                // keeps the circuit committed to leaving on one side instead
                // of oscillating underneath the Boss.
                if (!_personalSpaceLatched)
                {
                    _personalSpaceLatched = true;
                    _personalSpaceHorizontal = AwayFromBossAxis(gap);
                    _personalSpaceVertical =
                        boss.Center.Y >= player.Center.Y ? -1 : 1;
                }
                horizontal = _personalSpaceHorizontal;
                vertical = _personalSpaceVertical;
                phase = PersonalSpacePhase;
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

        /// <summary>Reads the leg schedule once per fight. Anything that is not a
        /// positive integer disables the branch, so a missing or malformed
        /// variable can only reproduce the reviewed circuit and can never leave
        /// the circuit in a half-configured state.</summary>
        private static int ReadLegCharges()
        {
            var raw = Environment.GetEnvironmentVariable(LegChargesVariable);
            int value;
            if (string.IsNullOrEmpty(raw) ||
                !int.TryParse(raw.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value) || value < 1)
                return 0;
            return value;
        }

        /// <summary>Parses the per-charge pattern once per fight. Any malformed
        /// entry rejects the whole pattern rather than skipping it: a partially
        /// applied schedule would be a different schedule, and silently planning a
        /// partly-valid pattern is exactly the kind of half-configured state that
        /// makes a negative result unreadable.</summary>
        private void ReadBeatPattern()
        {
            _patternFirst = new int[0];
            _patternRest = new int[0];
            var raw = Environment.GetEnvironmentVariable(BeatPatternVariable);
            if (string.IsNullOrEmpty(raw)) return;
            var halves = raw.Split('|');
            if (halves.Length > 2) return;
            var first = ParsePattern(halves[0]);
            if (first == null) return;
            _patternFirst = first;
            if (halves.Length == 2)
            {
                var rest = ParsePattern(halves[1]);
                if (rest == null)
                {
                    _patternFirst = new int[0];
                    return;
                }
                _patternRest = rest;
            }
            else
            {
                _patternRest = first;
            }
        }

        private static int[] ParsePattern(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split(',');
            var values = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                int value;
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) ||
                    value < -1 || value > 1)
                    return null;
                values[i] = value;
            }
            return values.Length == 0 ? null : values;
        }

        /// <summary>The pattern entry for a charge family, or an empty array when
        /// the branch is disabled for it.</summary>
        private int[] PatternFor(int family) =>
            family == 1 ? _patternFirst : _patternRest;

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