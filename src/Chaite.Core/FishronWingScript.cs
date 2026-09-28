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
    /// player leaves that line 閳?which is why every reviewed source says to
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
        /// <summary>AI_069 treats the first and last 400 tiles as the ocean band.
        ///
        /// MEASURED (round 157, when the arena was shortened to 320 tiles):
        ///
        /// The band and the arena have to agree. With the arena at the owner's 320
        /// tiles the real ground ends at x = 5120, but this constant is a band
        /// WIDTH measured from the world edge, and `worldLeft` is a hardcoded 16,
        /// so 6400 put the band's right edge at 6156 -- 1036 px of band past the
        /// end of the ground. The weak wing chased that band and died at tick 2916
        /// with the Boss at 18605, where on the 399-tile arena the very same run
        /// was a clean 3-hit kill at 3659. The strong wing was unaffected, which
        /// is why the fault was easy to miss. 5120 is the arena's own width, so
        /// the band now ends where the ground does.</summary>
        private const float OceanBandPixels = 5120f;
        /// <summary>AI_069 enrages below this native player Y.</summary>
        private const float SkyEnrageCeiling = 800f;
        private const float CeilingMargin = 480f;
        private const float FloorMargin = 90f;
        private const float BandEdgeMargin = 260f;
        private const float CoLocationBand = 24f;
        /// <summary>How far inside the band edge the turnaround is placed. The
        /// band edge is NOT the obstruction: `_bandLeft` is
        /// worldLeft + BandEdgeMargin = 276, while the player is stopped at
        /// x = 640 and charged there with vx 0.00 for six or more ticks, taking
        /// 73-83 damage per contact.
        ///
        /// The obstruction cannot be found from `arena.ClearanceLeft` either:
        /// `ScanHorizontal` returns a tile count capped at 150, so
        /// ClearanceLeft is 2400.0 -- saturated -- everywhere in the arena
        /// interior, and it reads as open at the very position the player is
        /// actually pinned at. The world's own left edge is tile 1 (x = 16), so
        /// 640 is not a boundary the tile scan reports at all.
        ///
        /// MEASURED (game-probe-gated-strong-6k): min player x over the fight is
        /// exactly 640.0 against a max of 6085.3. Turning 640 px inside the band
        /// edge places the turnaround at x = 916, comfortably clear of 640 while
        /// still leaving a 4920 px corridor (§78 requires the W-cycle timing
        /// elsewhere to be preserved, so the margin is kept well inside the
        /// arena rather than at its centre).</summary>
        private const float PinnedWallMargin = 640f;
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
        /// <summary>Standoff tested against TRUE separation rather than the
        /// horizontal gap, with a larger radius. See <c>StandoffViolated</c>.
        /// Paired with <c>StandoffDistanceVariable</c>; default OFF.</summary>
        internal static bool StandoffDistanceArmed
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(StandoffDistanceVariable);
                if (!string.IsNullOrEmpty(raw) && raw.Trim() == "1") return true;
                // DPS-CONDITIONAL form. MEASURED: a distance-based standoff of
                // 1200 px is a clear gain at 300 DPS (8812 -> 10004 ticks, boss
                // 36546 -> 30628) and NEUTRAL at 1200/1500/2000, but it REGRESSES
                // the middle (strong 600 loses its kill, strong 1000 3 hits -> 4),
                // so it is not a global default. The one place it helps is the one
                // place that is blocked, so this form enables it only below a DPS
                // ceiling. Paired with StandoffLowDpsVariable / StandoffLowDpsMax.
                // DEFAULT (no variable set) is the `dps` form, which is inert
                // unless simulated output is configured. That matters: route
                // replay never sets CHAITE_SIM_DPS (see verify-fishron-routes.ps1,
                // which clears every knob and injects no damage), so committing
                // this default cannot perturb the committed control path. It only
                // applies in the simulated accept/reject channel, which is where
                // the measurement that justifies it was taken.
                if (string.IsNullOrEmpty(raw) || raw.Trim() == "dps")
                {
                    var dpsRaw = Environment.GetEnvironmentVariable("CHAITE_SIM_DPS");
                    float dps;
                    if (string.IsNullOrEmpty(dpsRaw) ||
                        !float.TryParse(dpsRaw.Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out dps))
                        return false;   // no simulated output: fight as reviewed
                    return dps <= StandoffLowDpsMax;
                }
                return false;
            }
        }

        /// <summary>Highest simulated DPS at which the distance-based standoff is
        /// enabled by the <c>dps</c> form. Measured window: 1200 px helps at 300 and
        /// is a regression at 600, so the ceiling sits between them.</summary>
        internal static float StandoffLowDpsMax
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(StandoffLowDpsMaxVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return 450f;
                return value;
            }
        }

        private const string StandoffLowDpsMaxVariable = "CHAITE_STANDOFF_DPS_MAX";

        private const string StandoffDistanceVariable = "CHAITE_STANDOFF_DISTANCE";
        /// <summary>Radius in px for the distance-based standoff.</summary>
        internal static float StandoffDistancePixels
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(StandoffRadiusVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return StandoffDistanceDefaultPixels;
                return value;
            }
        }

        private const string StandoffRadiusVariable = "CHAITE_STANDOFF_PX";
        /// <summary>Radius in px for the distance-based standoff.
        ///
        /// RAISED 1200 -> 1600 (round 151, measured A/B over the whole band). The
        /// earlier note justified 1200 by saying the term "saturates above 1200", but
        /// that reading came from a sweep of the MIDDLE of the band, where the standoff
        /// is in fact inert. Swept properly at the low end it does not saturate:
        ///
        ///   strong 300   1200 -> 11528/8/... (no-kill)   1600 -> **16090/8/0 KILL**
        ///   strong 1800  12889 no-kill   2000 -> 11903   2400+ -> 9794 (all collapse)
        ///   weak   300   800/1000/1200/1400/1800/2200 -> 8778/9456/9456/8777/8777/8777,
        ///                none of which kill: the weak wing is radius-INDEPENDENT here
        ///
        /// A full A/B of reviewed-1200 against 1600 over ten DPS points returned
        /// IDENTICAL results at 600, 700, 800, 900, 1000, 1100, 1200, 1500 and 2000, and
        /// a +5341-tick gain at 300 (10749/8/26835 no-kill -> 16090/8/0 KILL). So 1600 is
        /// a strict improvement: it changes exactly one point in the band, and at that
        /// point it converts the failure into a kill.
        ///
        /// That single point is the one the objective needs. At 300 DPS the sim deals
        /// 5 damage per tick against lifeMax 78000, so a kill needs 15600 ticks of
        /// survival; the old behaviour died at 10749 (section 140). 16090 clears it.
        ///
        /// The radius is DPS-gated (see <see cref="StandoffDistanceArmed"/>), so it only
        /// takes effect below <see cref="StandoffLowDpsMax"/>. Above that the reviewed
        /// horizontal-gap standoff runs unchanged, which is why the other nine points
        /// are byte-identical.</summary>
        private const float StandoffDistanceDefaultPixels = 1600f;
        /// <summary>Whether the pre-charge standoff is violated.
        ///
        /// MEASURED (dense strong-300 trace, 98 charge locks): a hit is strongly
        /// predicted by how CLOSE the Boss was when it committed. Hit locks have a
        /// mean separation of 311 px against 572 for clean locks, and 18 ticks is
        /// not enough for the escape's crossing to develop where 34 is. The
        /// reviewed standoff gates on the HORIZONTAL GAP alone, so a Boss hovering
        /// above the player at a small horizontal offset reads as "far" and the
        /// standoff never fires. The option here gates on true separation instead,
        /// which is the quantity the measurement implicates. Default OFF so every
        /// earlier measurement is unchanged.</summary>
        private static bool StandoffViolated(float gap, PlayerSnapshot player,
            in TargetSnapshot boss)
        {
            if (!StandoffDistanceArmed) return Math.Abs(gap) < StandoffPixels;
            var dx = boss.Center.X - player.Center.X;
            var dy = boss.Center.Y - player.Center.Y;
            return Math.Sqrt(dx * dx + dy * dy) < StandoffDistancePixels;
        }
        /// <summary>Horizontal half-width kept clear of the remembered Sharknado
        /// column. A Cthulhunado is 23 tiles wide, so this is column plus body
        /// plus a full escape.</summary>
        private const float TornadoClearance = 760f;
        /// <summary>Half-extents of the Cthulhunado column, in px. MEASURED from the
        /// decompiled setup and confirmed by the live trace histogram: `width = 150 *
        /// scale`, `height = 42 * scale`, scale capped at 1.5 for type 386, so at full
        /// growth 225 x 63 -- half-extents 112 x 31, plus the player's own 10 x 21
        /// half-box, rounded to 125 x 55 for margin.
        ///
        /// NOTE: an earlier draft used 190 x 110 from a histogram read that mistook the
        /// growing mid-life values for the maximum. The 190 x 110 box was so generous
        /// that it fired on frames where the player was nowhere near the column, which
        /// is why its result was byte-identical at strong 300 and harmful elsewhere. The
        /// corrected box is deliberately tight.</summary>
        private const float TornadoHalfWidth = 125f;
        private const float TornadoHalfHeight = 55f;
        /// <summary>Arm the vertical half of the tornado box. Default OFF so every
        /// measurement recorded before this round still reproduces.</summary>
        internal static bool TornadoVerticalArmed
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoVerticalVariable);
                return !string.IsNullOrEmpty(raw) && raw.Trim() == "1";
            }
        }

        private const string TornadoVerticalVariable = "CHAITE_TORNADO_BOX";
        /// <summary>Horizontal radius of the tornado gate, sweepable.
        ///
        /// MEASURED DEFECT (round 148, dense strong-300). The column is produced by
        /// the boss's projectile attack and is present in ONE continuous episode from
        /// tick 9125 to 10004 -- 881 ticks, longer than the 540-tick memory -- and
        /// FOUR of that run's eight hits fall inside it. Reconstructing the geometry
        /// tick by tick over those 881 ticks:
        ///
        ///   |dx| < 760 (the gate)   : 634 ticks (72.0%)
        ///   |dx| < 190 (column box) : 491 ticks (55.7%)
        ///
        /// A larger radius pushes the player across the arena, which is what the
        /// owner's technique notes describe ("get the Sharknado released at the two
        /// ends", "keep the whole width available to run back into"). Sweepable
        /// because the right value is an empirical question.
        ///
        /// MEASURED RESULT: 760 is OPTIMAL, and every other value is worse (135.5):
        /// 1200 -> 7867, 1600 -> 4679, 2200 and 3000 both -> 3658. The escape radius
        /// is a knife edge and the reviewed value sits on it.</summary>
        internal static float TornadoClearanceRadius
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoClearanceVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return TornadoClearance;
                return value;
            }
        }

        private const string TornadoClearanceVariable = "CHAITE_TORNADO_CLEARANCE";
        /// <summary>How long a landed Sharknado keeps its column.
        ///
        /// MEASURED (round 148/149): the Cthulhunado (projectile 386) is present in one
        /// continuous episode of **881 ticks** (9125..10004) with a recorded `timeLeft`
        /// reaching 840, against a memory of 540 -- so a ~341-tick window exists in
        /// which the circuit has forgotten a column that is still there.
        ///
        /// MEASURED RESULT: extending the memory is REFUTED, and by a very narrow
        /// margin. 540 -> 10004/8/30628, but **560 is already past the cliff**:
        ///
        ///   560, 600, 620, 640, 660, 700, 900, 1200, 1600  ->  ALL 6969 / 8 / 45782
        ///
        /// Every value above 540 collapses to one identical failure tick. The cause is
        /// a coupling, not the column: this branch RETURNS, so while it is active the
        /// pre-charge jump below it never runs, and `PreJumpTicks = 20` is exactly the
        /// wind-up the jump needs. Persisting the gate a few ticks longer therefore
        /// suppresses the jump often enough to lose the run -- a 20-tick change flips
        /// it. The 540 value is another knife edge that the reviewed circuit has right.
        /// Sweepable via <c>CHAITE_TORNADO_MEMORY</c> for future measurement; default
        /// is the reviewed 540.</summary>
        internal static int TornadoMemoryHorizon
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoMemoryVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 || value > 3000)
                    return TornadoMemoryTicks;
                return value;
            }
        }

        private const string TornadoMemoryVariable = "CHAITE_TORNADO_MEMORY";
        /// <summary>How long the ESCAPE branch runs and, through its `return`, keeps
        /// suppressing the pre-charge jump.
        ///
        /// WHY THIS IS SEPARATE FROM THE MEMORY (round 150). Round 149 established both
        /// halves of a puzzle that the single counter could not satisfy at once:
        ///
        ///  - The column is present for **881 ticks**, and the memory is **540**, so 341
        ///    ticks of live column are unmodelled.
        ///  - Extending the memory is REFUTED: 560 onward all collapse to one identical
        ///    failure (6969/8/45782). The cause is a coupling, because this branch
        ///    `return`s and so suppresses the pre-charge jump beneath it, and
        ///    `PreJumpTicks = 20` is exactly that jump's wind-up.
        ///
        /// One counter was doing three jobs: how long the column is REMEMBERED, how long
        /// the player ESCAPES it, and how long the jump is SUPPRESSED. The last two
        /// belong together (an escape without a jump is the review's own design), but
        /// neither should scale with the first. Splitting them lets the memory cover the
        /// column's real 881-tick life while the response stays at the reviewed 540.
        ///
        /// Sweepable via <c>CHAITE_TORNADO_RESPONSE</c>.
        ///
        /// DEFAULT RAISED FROM 540 TO 120 (round 150, measured). The reviewed 540 was
        /// inherited from the single-counter circuit, where it also had to cover the
        /// column's memory. Once the memory is decoupled, 540 turns out to be far too
        /// long a COMMITMENT: the branch `return`s for the whole window, so the controller
        /// spends 9 seconds running away and never re-engages. Measured at 300 DPS,
        /// strong wing, obsidian tier:
        ///
        ///   180 -> 6583/6/47682    240 -> 7168/7/44790    360 -> 4889/5/56197
        ///   420 -> 6970/8/45795    540 -> 10004/8/30628   **120 -> 10749/8/26835**
        ///
        /// The optimum is sharp and NOT monotone, so it was swept, not reasoned. The
        /// decisive evidence is the same run at other DPS: at 600 the reviewed window
        /// cannot kill (6744/6, boss at 15937) while 120 kills with 5 hits (8328), and at
        /// 1200 the window 120 reaches a ZERO-HIT kill (4437/0/0, where 540 took 1 hit).
        /// A window this short is defensible against the native numbers rather than against
        /// taste: the column lives 840 ticks, and 120 ticks is 2 seconds -- enough to clear
        /// the immediate cascade, after which continuing to flee costs the fight.
        ///
        /// The width knob was ALSO wrong, and inertly so: see <see cref="TornadoRecallRadius"/>.
        /// Sweeping the width at this window gave the identical result at 760, 2000 and
        /// 4000, so the width is not the load-bearing variable; the window is.</summary>
        internal static int TornadoResponseHorizon
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoResponseVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 || value > 3000)
                    return TornadoResponseTicks;
                return value;
            }
        }

        /// <summary>Reviewed escape/response window, in ticks. See
        /// <see cref="TornadoResponseHorizon"/> for the sweep that chose 120.</summary>
        internal const int TornadoResponseTicks = 120;

        private const string TornadoResponseVariable = "CHAITE_TORNADO_RESPONSE";
        /// <summary>Radius of the escape/recall gate for the response window. Defaults to
        /// the reviewed <see cref="TornadoClearance"/> so the circuit is unchanged.</summary>
        internal static float TornadoRecallRadius
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoRecallVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return TornadoClearance;
                return value;
            }
        }

        private const string TornadoRecallVariable = "CHAITE_TORNADO_RECALL";
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
        /// <summary>Pre-charge jump lead, made wing-aware.
        ///
        /// MEASURED (dense 300-DPS native traces, one per wing):
        ///
        ///            peak climb   median climb   altitude span   wingTimeMax
        ///   strong     16.31         7.50           4663            180
        ///   weak        9.91         5.08           3367            130
        ///
        /// The weak wing climbs at roughly two thirds the strong wing's rate (median
        /// 5.08 against 7.50, a 1.48x deficit) while FALLING at exactly the same
        /// 10.01. So a fixed 20-tick wind-up gives the weak wing far less altitude by
        /// contact than it gives the strong wing, and 71 px of |dy| is what the body
        /// box needs. This is the concrete content of the owner's note that the two
        /// wings need two sets because their vertical mobility differs -- the shared
        /// constant is the thing that ignores it.
        ///
        /// The weak wing is NOT more often wing-exhausted (wingTime == 0 on 25.6% of
        /// ticks against the strong wing's 33.8%), so the deficit is rate rather than
        /// budget, and the naive fix is to start the wind-up earlier in proportion
        /// (20 * 7.50 / 5.08 = 29.5, so 30).
        ///
        /// THAT PROPORTIONAL SCALING IS REFUTED, AND THE TIMING IS A KNIFE EDGE.
        ///
        ///   weak 300, lead 20 (reviewed):  5879 / 8 / 51124
        ///   weak 300, lead 26:             4812 / 7 / 56546
        ///   weak 300, lead 30:             2176 / 5 / 69767   <- collapse
        ///   weak 300, lead 35:             2176 / 5 / 69767   identical
        ///   weak 300, lead 45:             2176 / 5 / 69767   identical
        ///
        /// Leads of 30, 35 and 45 all produce the SAME tick, dead at 2176, which is
        /// the broken-invariant signature again. Extending the wind-up does not buy
        /// altitude; it makes the player commit to a climb earlier and then fly into
        /// the Boss's hover point, which is exactly the failure the personal-space
        /// comment in this file already records ("a climb from a few tens of pixels
        /// below the hover point flies straight into it"). The 20-tick lead is the
        /// reviewed value and it stays; the wing-aware threshold exists so the
        /// behaviour is at least expressed per wing and swimmable, but it is left at
        /// 20 for both. See WeakPreJumpTicks.</summary>
        private const int WeakPreJumpTicks = 20;

        /// <summary>Horizontal speed below which the charge branch stops
        /// honouring a neutral pattern charge and simply runs away. A charge
        /// closes at 14.7 px/tick and wing cruise is a measured 13.87, so a
        /// player this far below cruise cannot leave the charge line in time and
        /// has to spend the beat rebuilding speed instead of varying the
        /// pattern.</summary>
        private const float EscapeSpeedFloor = 6f;

        /// <summary>Sign of the horizontal component of the latched charge
        /// normal, and the sign of its vertical component.
        ///
        /// Native lock, read from AI_069: the charge velocity is computed and
        /// frozen on the single tick the Boss enters charge state 1 (or 6, or
        /// 11) -- `velocity = Vector2.Normalize(player.Center - center) * num7`,
        /// with num7 = 17 in expert and 23 when enraged. States 1/6/11 never
        /// rewrite velocity, and the state-1 wind-up lasts num6 = 28 ticks
        /// during which the Boss is already travelling at that speed, so the
        /// whole approach is downhill from one decision made on one tick.
        ///
        /// That single tick is what makes the dodge a perpendicular one. The
        /// locked speed is 17 against a measured maxRunSpeed of 4.71, so the
        /// player can never open the gap along the charge line; the only axis
        /// that works is normal to it. The owner's rule states the same thing
        /// operationally: when the Boss begins a locked charge, dodge diagonally
        /// up if it is above the player and diagonally down if it is below, and
        /// only run straight away when the lock distance is already large.
        ///
        /// Both signs are latched once per charge rather than recomputed each
        /// tick. Recomputing the horizontal from the Boss's current side is what
        /// the flee branch used to do, and AI_069 crosses the player during a
        /// charge, so the sign flipped exactly while the body was on top of the
        /// player. Measured on the native stream: of 40 locked charges, 27 held
        /// the correct normal for less than half the episode and the diagonal
        /// ones (aim_y about -0.5) spent 67 to 100 percent of the charge moving
        /// *opposite* to the normal, i.e. back across the locked line.</summary>
        private int _chargeNormalHorizontal;
        private int _chargeNormalVertical;
        /// <summary>Separation at the instant the charge locked, in px. Kept because the
        /// owner's dodge rule is distance-dependent and the lock is the ONLY moment it
        /// can be measured: the Boss's aim is frozen then, so this number never changes
        /// for the rest of the episode.</summary>
        private float _chargeLockDistance;
        /// <summary>Take the lock distance above this and the diagonal dodge is replaced
        /// by a straight horizontal pull-away.
        ///
        /// THE OWNER'S RULE, VERBATIM: "猪鲨的冲刺是锁定后再进行的，理应向法线躲避（猪鲨开始
        /// 锁定冲刺时比玩家高则需要斜上移动，比玩家低则斜向下移动），除非距离已经够远才能直接
        /// 水平拉远！" The first two clauses are already what the locked normals do -- see
        /// <see cref="LatchChargeNormal"/>. The third clause, the DISTANCE EXCEPTION, was
        /// documented there as intent but never implemented: nothing read the lock
        /// distance after computing it.
        ///
        /// IMPLEMENTED, SWEPT, AND REFUTED AS A BAND RULE (round 151). Faithfully built
        /// (drop the diagonal to `vertical = 0`, i.e. hold altitude and pull straight
        /// away, whenever the lock separation exceeded the threshold) and swept:
        ///
        ///   strong 300   off 10749/8/26835   ->  400 9132/8/34986   550 **11061/7/25251**
        ///   strong 600   off  8328/5/KILL    ->  400 5879/6/24602   550  6993/6/13401
        ///   strong 800   off  6011/5/5015    ->  400 6389/4/KILL    550  5480/4/12091
        ///   strong 1000  off  4536/4/11408   ->                        550  4647/3/9489
        ///   strong 1200  off  4437/0/0       ->                        550  4440/1/0
        ///   strong 2000  off  2881/0/0       ->                        550  2880/0/0
        ///   weak   300   off  9456/9/33297   ->  400 2086/7/70251   550  4071/7/60305
        ///   weak   600   off  4924/6/34027   ->                        550  4071/7/42655
        ///   weak   800   off  5287/5/14595   ->  400 2086/7/57376   550  3739/7/35333
        ///   weak  2000   off  2881/1/0       ->                        550  2881/0/0
        ///
        /// Three readings decide it. (1) The `550` optimum is a KNIFE EDGE, not a
        /// plateau: on the strong wing 600 -> 8876 and 700 -> 5929, and 400 collapses two
        /// weak points to tick 2086. (2) Even at its own optimum it is a NARROW win: it
        /// buys one hit at strong 300 and the weak-2000 no-hit, but STRICTLY LOSES the
        /// strong-600 kill (6993/6 with the boss at 13401, against a clean kill) and
        /// drives weak 300/600/800 from 9456/4924/5287 down to 4071/4071/3739. (3) At
        /// 400, two configurations from opposite wings die on the SAME tick 2086 -- the
        /// broken-invariant signature (section 126), which says the rule replaced
        /// fight-specific evasion with a trajectory that no longer depends on the fight.
        ///
        /// This is the fourth consecutive command-space redirection to fail (exact
        /// perpendicular, along-the-charge, pre-lock facing, now the distance exception),
        /// and it matches the two recorded wins: 129 and 132 both ADDED a missing
        /// pre-condition, while every one of these REDIRECTED a command that was already
        /// right. Left default-OFF so the measurement is preserved and repeatable.
        ///
        /// Note the owner's rule is not WRONG in its own terms -- for the one measured
        /// point it helps, it helps exactly as described. It is simply not true of the
        /// whole band, which is what the objective needs.</summary>
        internal static float LockRunAwayDistance
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(LockRunAwayVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return 0f;
                return value;
            }
        }

        private const string LockRunAwayVariable = "CHAITE_LOCK_RUN_AWAY";
        /// <summary>The charge sequence the normal signs belong to, so a new
        /// lock re-latches and the rest of the episode reuses them. Starts at -1
        /// and is only read as "a lock is in force" once a lock has happened, so
        /// the very first charge of a fight latches like every later one.</summary>
        private int _chargeNormalSequence = -1;

        /// <summary>Latched by `ChargeEscape` on the frames of a locked charge, at
        /// the last point it runs before Tick. True once the counter-dash has been
        /// issued for the current armed window; it is cleared wherever the script
        /// resets the locked-charge state.</summary>
        internal bool _counterDashSpent;

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

        /// <summary>Wing budget at or below which the circuit stops climbing while
        /// airborne so that it can land and refill, in wingTime units.
        ///
        /// The guard exists because an airborne player with no budget has no
        /// vertical authority at all, which makes every charge that arrives in
        /// that state unavoidable. MEASURED (dense native trace, script only, hit
        /// at tick 533): wingTime was 0 on every row from tick 508 to 533 while
        /// the player was airborne throughout, so it never landed and never
        /// refilled, and the latched normal could not be executed.
        ///
        /// A threshold of 0 releases the climb only once the bar is already
        /// empty. A larger threshold reserves enough budget to actually complete
        /// a dodge, at the cost of landing earlier. Read from the environment so
        /// it can be swept in-engine on whole native fights without a rebuild; a
        /// missing, malformed or negative value falls back to 0, which reproduces
        /// the reviewed circuit exactly.</summary>
        private const string RefillGuardVariable = "CHAITE_REFILL_GUARD";
        private const string CoLocationRoutesVariable = "CHAITE_COLOCATION_ROUTES";
        private const string ApexRefillVariable = "CHAITE_APEX_REFILL";
        private const string DashSuppressVariable = "CHAITE_DASH_SUPPRESS";
        private const string DashDelayVariable = "CHAITE_DASH_DELAY";

        private const string NoChargeDashVariable = "CHAITE_NO_CHARGE_DASH";

        /// <summary>True when the charge's dash is withheld entirely. Kept only
        /// as a documented refutation: it kills both arms. See the call site.</summary>
        private static bool NoChargeDashArmed
        {
            get { return Environment.GetEnvironmentVariable(NoChargeDashVariable) == "1"; }
        }

        private const string ChargeClimbAwayVariable = "CHAITE_CHARGE_CLIMB_AWAY";

        private const string ChargeEscapeSimVariable = "CHAITE_CHARGE_ESCAPE_SIM";

        /// <summary>True when the locked-charge escape direction is chosen by
        /// forward-simulating the frozen charge against candidate 2-D directions
        /// instead of by projecting the player's own past velocity. Default OFF.
        /// The frozen normal is still latched either way, so the reviewed circuit
        /// is reproduced exactly when this is unset.</summary>
        private static bool ChargeEscapeSimArmed
        {
            get { return Environment.GetEnvironmentVariable(ChargeEscapeSimVariable) == "1"; }
        }

        private const string ChargeDashSideVariable = "CHAITE_CHARGE_DASH_SIDE";

        /// <summary>True when the locked-charge escape refuses any direction that
        /// moves the player toward the Boss. Default OFF.
        ///
        /// Native Player.cs:31602 gives the Shield's dash immunity only to the NPC
        /// the dash first touched (`eocHit`), so a dash that lands on the Boss
        /// spends that immunity on the Boss and leaves only the 4 tick collision
        /// immunity while eocDash still has ~11 ticks to run.</summary>
        private static bool ChargeDashSideArmed
        {
            get { return Environment.GetEnvironmentVariable(ChargeDashSideVariable) == "1"; }
        }

        private const string CounterDashVariable = "CHAITE_COUNTER_DASH";

        /// <summary>True when the locked-charge dash is timed to meet the arriving
        /// Boss instead of being fired at the lock. Default OFF. See the call site
        /// for the native basis: the dash grants contact immunity to the NPC it
        /// touches, deals the shield's damage, and recoils the player clear.</summary>
        private static bool CounterDashArmed
        {
            get { return Environment.GetEnvironmentVariable(CounterDashVariable) == "1"; }
        }

        /// <summary>Ticks-to-contact at which the counter-dash is issued. Paired
        /// with CounterDashVariable. The dash's immunity runs 15 ticks (eocDash)
        /// and is cut to 10 on the touch, so the useful window is a few ticks
        /// either side of arrival.</summary>
        private static float CounterDashGap
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(CounterDashGapVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value < 0f)
                    return 4f;
                return value;
            }
        }

        private const string CounterDashGapVariable = "CHAITE_COUNTER_DASH_GAP";

        /// <summary>True when the ascend beat refuses to lift while the Boss is
        /// below the player. Default OFF, so the reviewed circuit is reproduced
        /// exactly and this can be swept on whole native fights without a rebuild.
        /// The evidence for it is in the beat-1 comment: at the measured t=2251 hit
        /// the beat lifted with the Boss below, which held the player inside the
        /// 71 px body box along the frozen charge line instead of leaving it.</summary>
        private static bool ChargeClimbAwayArmed
        {
            get { return Environment.GetEnvironmentVariable(ChargeClimbAwayVariable) == "1"; }
        }

        // REFUTED and deliberately deleted, so nobody re-adds it: driving the
        // vertical half of the locked-charge escape straight from the lock
        // geometry (an experiment called CHAITE_CHARGE_NORMAL_OWNER, with a
        // 320 px straight-pull-away clause and a 40 px level band). Every
        // strong-wing run at every DPS died at one identical tick -- 2151 without
        // the clauses, 1811 with them -- against a baseline where 600 survives to
        // the cap and 1500 is a zero-hit kill. The full measurements and the
        // reason a good description is not a good controller are recorded at the
        // discriminator in LatchChargeNormal, which is where the rule is rejected.





        /// <summary>Ticks to wait after the charge lock before spending the
        /// charge's single dash.
        ///
        /// MEASURED AND REFUTED -- THE DEFAULT IS 0 AND SHOULD STAY 0. The hits
        /// really are timed as this was built to fix: on four hits across BOTH
        /// loadouts the charge dash is spent 2-5 ticks after the lock, so its 15
        /// i-frames expire about 5 ticks before contact (`lock 1406 -> dash 1408
        /// -> i-frames die 1420 -> contact 1424`, and likewise 1584/1586/1600/1604,
        /// 1758/1760/~1774/~1778 and 4250/4250/4265/4271). Delaying the dash DOES
        /// cover the contacts -- `npc contact` falls from 3-4 to 0 at delays
        /// 12/16/24, so the collisions land inside the i-frames exactly as
        /// predicted. It nevertheless makes the FIGHT worse at every nonzero
        /// value, because holding the dash perturbs the whole cruise cycle and
        /// the dash is a once-per-charge budget:
        ///
        ///   delay  0 -> 2 hits / 3-4 contacts   (best)
        ///          2 -> 4        4 -> 4        6 -> 8        8 -> 10
        ///         12 -> 5 (0 contacts)        16 -> 9 (0)   20 -> 8        24 -> 8 (0)
        ///
        /// The covers-the-contact benefit is real but smaller than the cost of
        /// losing the dash elsewhere, so this knob exists only to demonstrate that
        /// the timing is measurable. It is not a fix.</summary>
        private static int DashDelay
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(DashDelayVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 ||
                    value > 120)
                    return 0;
                return value;
            }
        }


        /// <summary>Vertical gap (px) below which a ready charge dash is
        /// suppressed, so those frames go to the vertical escape instead of a
        /// horizontal dash that cannot outrun the charge. Route-dependent, from
        /// the controlled sweep at the call site. CHAITE_DASH_SUPPRESS forces one
        /// value for both routes when set to a positive number, and 0 disables
        /// the rule entirely (reproducing the pre-§97 circuit).</summary>
        private static float DashSuppressGap(FormulaRoute route)
        {
            var raw = Environment.GetEnvironmentVariable(DashSuppressVariable);
            if (!string.IsNullOrEmpty(raw))
            {
                var text = raw.Trim();
                float forced;
                if (text == "0") return 0f;
                if (float.TryParse(text, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out forced) && forced > 0f)
                    return forced;
            }
            return route == FormulaRoute.FishronFairyWingsDash ? 50f : 90f;
        }

        /// <summary>The route of the episode being driven, latched in Tick.
        /// ChargeEscape is an instance method and sees no FormulaScriptInput, so
        /// the loadout-dependent gate above reads it from here.</summary>
        private FormulaRoute _dashSuppressRoute = FormulaRoute.FishronStrongWingsDash;

        /// <summary>The current tick's NativeSequence, latched in Tick before
        /// ChargeEscape runs. `_previousSequence` is one tick behind at that
        /// point, so it cannot be used for a ticks-since-lock countdown.</summary>
        private int _currentTimer;

        /// <summary>Ticks since the charge lock, or -1 when no lock has been
        /// taken.
        ///
        /// NativeTimer, NOT NativeSequence: MEASURED on game-probe-fin-strong-dflt,
        /// the Boss's ai[2] -- which is what NativeSequence carries -- counts up
        /// through the WIND-UP and RESETS TO ZERO exactly at the lock
        /// (`ai2: 29 -> 0` as `ai0` goes 0 -> 1). Differencing it across the lock
        /// therefore yields garbage, and using it made every nonzero delay hold
        /// the dash for the whole charge (measured: 7 hits and a death at tick
        /// 2406 for delays 10, 14, 18, 22 and 28 alike). NativeTimer restarts
        /// from zero at the state entry, which is the lock, so its value IS the
        /// ticks-since-lock count.</summary>
        private int ChargeTicksSinceLock
        {
            get { return _chargeNormalSequence >= 0 ? _currentTimer : -1; }
        }


        /// <summary>Whether the drained-bar refill also drops the jump command at
        /// the apex, so the native `velocity.Y == 0 && releaseJump` clause
        /// (Player.cs:26992) can restore the flight bar in mid-air. Off unless
        /// CHAITE_APEX_REFILL=1.</summary>
        private static bool ApexRefillArmed
        {
            get { return Environment.GetEnvironmentVariable(ApexRefillVariable) == "1"; }
        }

        /// <summary>Whether the co-location lift applies to this route. Read from
        /// CHAITE_COLOCATION_ROUTES (a comma-separated list of `strong` / `weak`)
        /// so the loadout gate can be swept without a rebuild. The default is
        /// `strong` alone, which is the measured configuration.</summary>
        private static bool CoLocationRoute(FormulaRoute route)
        {
            var raw = Environment.GetEnvironmentVariable(CoLocationRoutesVariable);
            if (string.IsNullOrEmpty(raw)) return route == FormulaRoute.FishronStrongWingsDash;
            var weak = raw.IndexOf("weak", StringComparison.OrdinalIgnoreCase) >= 0;
            var strong = raw.IndexOf("strong", StringComparison.OrdinalIgnoreCase) >= 0;
            if (route == FormulaRoute.FishronStrongWingsDash) return strong;
            if (route == FormulaRoute.FishronFairyWingsDash) return weak;
            return false;
        }

        private static float ReadRefillGuard()
        {
            var raw = Environment.GetEnvironmentVariable(RefillGuardVariable);
            float value;
            if (string.IsNullOrEmpty(raw) ||
                !float.TryParse(raw.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) || value < 0f)
                return 0f;
            return value;
        }

        private static bool ReadTornadoAxisGuard()
        {
            var raw = Environment.GetEnvironmentVariable(TornadoAxisGuardVariable);
            if (string.IsNullOrEmpty(raw)) return TornadoAxisGuardDefault;
            return raw.Trim() != "0";
        }

        /// <summary>Simulated DPS read for the axis-guard ceiling, or NaN when no
        /// simulated output is configured. Duplicated from the standoff reader rather
        /// than shared because the two gates must stay independently switchable.</summary>
        private static float ReadSimulatedDps()
        {
            var raw = Environment.GetEnvironmentVariable("CHAITE_SIM_DPS");
            float dps;
            if (string.IsNullOrEmpty(raw) ||
                !float.TryParse(raw.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out dps))
                return float.NaN;
            return dps;
        }

        /// <summary>Highest simulated DPS at which the axis guard runs. See
        /// <see cref="TornadoAxisGuardDefaultMaxDps"/> for the measured band. With no
        /// simulated output the fight is the reviewed one that produced every earlier
        /// measurement except the low-DPS band, so the guard stays on.</summary>
        private static float TornadoAxisGuardMaxDps
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(TornadoAxisGuardMaxDpsVariable);
                float value;
                if (string.IsNullOrEmpty(raw) ||
                    !float.TryParse(raw.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) || value <= 0f)
                    return TornadoAxisGuardDefaultMaxDps;
                return value;
            }
        }

        /// <summary>Step the observed type-386 wall forward from what the probe can see
        /// this tick, and forget it when nothing is live.
        ///
        /// The extent is published per wall as a COLLISION footprint -- each member
        /// expanded by its own half-width and half-height -- so the stored interval is
        /// the true footprint and not just the centres. Walls are never merged.</summary>
        private void ObserveCascade(PlayerSnapshot player, in SharknadoBubbleSnapshot bubble)
        {
            _cascadeLeft = float.NaN;
            _cascadeRight = float.NaN;
            _cascadeAtHazardDepth = false;
            if (bubble.CascadeCount <= 0 || bubble.CascadeWallCount <= 0) return;
            // Pick the wall the player is standing in. Walls are already separated by
            // at least 256 px, so at most one can contain the player, and a union must
            // never be taken -- the union of two walls 3380 px apart is the whole arena.
            var x = player.Center.X;
            var y = player.Center.Y;
            for (var i = 0; i < bubble.CascadeWallCount; i++)
            {
                if (x < bubble.CascadeWallLeft(i) || x > bubble.CascadeWallRight(i))
                    continue;
                _cascadeLeft = bubble.CascadeWallLeft(i);
                _cascadeRight = bubble.CascadeWallRight(i);
                // Is the player also inside the wall's VERTICAL band? This is the
                // difference between a wall that can hurt it and a wall it is merely
                // flying past at altitude. MEASURED (round 155): at strong 1000 the
                // player sits inside a wall's x-footprint for 510 ticks, 497 of them
                // unbroken to death, and it dies at hazard depth. At the low-DPS points
                // the same x-overlap happens at altitude, ~700 px above the band, where
                // yielding the horizontal axis buys nothing and costs the fight.
                _cascadeAtHazardDepth =
                    y >= bubble.CascadeWallTop(i) && y <= bubble.CascadeWallBottom(i);
                return;
            }
        }

        /// <summary>Command the horizontal axis OUT of the observed wall, returning true
        /// when it did.
        ///
        /// Why this shape and not the axis guard: the axis guard tried to veto the descent
        /// once the nearest sub-tornado was close, and was refuted three times (rounds
        /// 152-153) because by then the player is already inside the wall's vertical span.
        /// A wall, however, is only a few hundred px wide and essentially static, so the
        /// cheap correction is HORIZONTAL -- no reaction window is needed, because leaving
        /// the footprint is monotone progress that the wall cannot undo.
        ///
        /// GATED ON HAZARD DEPTH, which is the round-155 measurement. Being inside a wall's
        /// x-footprint only matters if the player is also inside its VERTICAL band; at the
        /// low-DPS points the same x-overlap occurs hundreds of px above the band, where
        /// yielding the horizontal axis buys nothing and costs the fight. Requiring both
        /// halves keeps the wins at strong 800/1000/1500 and weak 600/900/1500 without
        /// touching the extended low-DPS fight.</summary>
        private bool TryEscapeCascade(PlayerSnapshot player, out int horizontal)
        {
            horizontal = 0;
            if (_cascadeDepthGate && !_cascadeAtHazardDepth) return false;
            if (float.IsNaN(_cascadeLeft) || float.IsNaN(_cascadeRight)) return false;
            var x = player.Center.X;
            if (x < _cascadeLeft || x > _cascadeRight) return false;
            var leftTarget = _cascadeLeft - CascadeEscapeMargin;
            var rightTarget = _cascadeRight + CascadeEscapeMargin;
            var leftRoom = leftTarget - _bandLeft;
            var rightRoom = _bandRight - rightTarget;
            // Pick the reachable exit; when both are reachable take the wider one.
            if (leftRoom > 0f && rightRoom > 0f)
            {
                horizontal = leftRoom >= rightRoom ? -1 : 1;
                return true;
            }
            if (leftRoom > 0f) { horizontal = -1; return true; }
            if (rightRoom > 0f) { horizontal = 1; return true; }
            // Neither exit fits inside the band. Fall back to the wider side of the
            // band, which is still strictly better than sitting in the wall.
            horizontal = x - _bandLeft <= _bandRight - x ? -1 : 1;
            return true;
        }

        /// <summary>Suppress a command that would drive the player deeper into the
        /// vertical band of a remembered Sharknado column.
        ///
        /// WHY THIS IS A MISSING PRE-CONDITION AND NOT A REDIRECT. A type-386 column is
        /// 225 px wide but only 63 px tall (width 150 x scale, height 42 x scale, scale
        /// capped at 1.5), and it never moves vertically. The circuit already knows the
        /// column's horizontal half and routes around it, but nothing in the sprint,
        /// descend or bait branches knew its VERTICAL half, so a descent onto the
        /// column's own y met it head-on. Measured strong 1000: the player descends from
        /// y 5912 to y 5589 while the column sits at 5592, and the four hits arrive 40
        /// ticks apart. The guard does not change which way the player flees; it only
        /// refuses the one axis that is about to intersect.
        ///
        /// It is deliberately NOT an "inside the box" rule -- the existing box branch at
        /// line 2073 already handles that case and must keep priority, because once the
        /// player is inside, leaving by the cheaper axis is correct. This guard only
        /// fires while the player is still clear on the vertical axis, which is exactly
        /// the window where the descent is still free to be cancelled.
        ///
        /// The horizontal denominator is capped at the column's own half-width so that a
        /// distant column cannot veto a descent: the guard is about columns the player is
        /// horizontally inside-or-near, not about columns in general.</summary>
        private void ApplyTornadoAxisGuard(PlayerSnapshot player, ref int vertical)
        {
            if (!_tornadoAxisGuard || _tornadoTicksLeft <= 0 || vertical == 0) return;
            // MEASURED (round 152): the guard is DESTRUCTIVE under the extended
            // distance standoff and only under it. With the low-DPS standoff armed
            // (1600 px) the circuit deliberately holds a wide separation from the
            // Boss, which also parks it nearer the columns, so the guard fires often
            // and strong 300 falls from 16090 (a kill) to 10626. With the reviewed
            // standoff the guard is neutral at 700/800/1000 and helpful elsewhere
            // (weak 600 4924 -> 6130, weak 800 -> a new kill). The two rules are
            // solving the same problem -- keep away from the columns -- so running
            // both double-counts it. The standoff wins where it is armed.
            if (StandoffDistanceArmed) return;
            var dps = ReadSimulatedDps();
            if (!float.IsNaN(dps) && dps > TornadoAxisGuardMaxDps) return;
            var dx = player.Center.X - _tornadoX;
            if (Math.Abs(dx) >= TornadoAxisGuardHorizontalReach) return;
            // Only the APPROACH matters. Vertical-plus-closing is what the measured
            // deaths had; vertical alone is how the circuit gains altitude anywhere
            // else in the fight, and vetoing that everywhere cost strong 300 7218
            // ticks. If the player is opening the horizontal gap, a descent is not
            // "into" the column even while it is near.
            if (dx * player.Velocity.X >= 0f) return;
            // Positive Y is downward. Descending moves toward a column below the player.
            var movingToward = vertical > 0
                ? player.Center.Y < _tornadoY
                : player.Center.Y > _tornadoY;
            if (!movingToward) return;
            if (Math.Abs(player.Center.Y - _tornadoY) <= TornadoAxisGuardVerticalMargin) return;
            vertical = 0;
        }

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
        private int _coLocationLiftCount;
        public int DiagnosticCoLocationLiftCount { get { return _coLocationLiftCount; } }
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
        private float _tornadoY;
        private int _tornadoTicksLeft;
        /// <summary>Whether the tornado-axis guard runs. See <see cref="ApplyTornadoAxisGuard"/>.
        ///
        /// ENABLED BY DEFAULT (round 152). The evidence is the strong-1000 death, whose
        /// four hits arrive at ticks 4048/4088/4128/4168 -- exactly 40 apart, four in a
        /// row -- and every one is the same column. The window shows the player
        /// descending at vy -7.5 to -10.0 from y 5912 onto the column's own y, 5592, while
        /// closing horizontally from 528 px to 87 px, until the player enters the column
        /// box at tick 4046. The column is 225 px WIDE but only 63 px TALL, so the descent
        /// is what kills, and the controller was commanding it from three different
        /// branches at once: <c>charge-descend</c> (default beat -> vertical +1),
        /// <c>tornado-bait</c> (line 2218) and <c>precharge-jump</c>. None of them knew
        /// about the column's vertical half.
        ///
        /// Strong 800 died the same way (hits at 5606 and 5646, 40 apart, column 4-141 px
        /// away), and strong 600 -- which KILLS -- never had a column closer than 71 px
        /// but was never descending onto it. So the discriminator is not the distance
        /// alone, it is "descending into the column's vertical extent".</summary>
        private const string TornadoAxisGuardVariable = "CHAITE_TORNADO_AXIS_GUARD";
        /// <summary>Default for <see cref="TornadoAxisGuardVariable"/>: on.</summary>
        internal const bool TornadoAxisGuardDefault = true;
        /// <summary>Vertical margin at which the guard treats the player as about to
        /// enter a column's band. Half-height of a fully grown sub-tornado (31) plus the
        /// player's own half-height (42) is 73, which is the true contact margin.
        ///
        /// MEASURED (round 153): widening this to 400 changed NOTHING -- byte-identical
        /// at strong 300/600/700/800/900/1000/1100 and weak 600/800/1100. That proves the
        /// binding constraint is not the margin but the guard's "closing horizontally"
        /// requirement, which the guard's own predicate rarely satisfies in the wreck
        /// window. Kept at the true contact margin.</summary>
        private const float TornadoAxisGuardVerticalMargin = 73f;
        /// <summary>How close, horizontally, a column must be for the guard to look at it.
        ///
        /// MEASURED, and the reason this is 125 and not more. The guard was written for
        /// the strong-1000 death, where the player descends onto a column's own y. It
        /// turns out 125 px is too SHORT to catch that descent: the player is always
        /// already inside the 73 px vertical margin by the time it is within 125 px
        /// (at tick 4044 dx is 124.9 and dy only -33.5), so the "still clear vertically"
        /// clause never holds there and the guard is a no-op in its own target window.
        ///
        /// Widening the reach to 600 does make it fire in that window -- the vertical
        /// separation passes 73 px at tick ~4014, when the horizontal gap is still
        /// ~360 px -- but it is far MORE destructive than doing nothing:
        ///
        ///   strong 600   8328/5/0 KILL        -> 6606/4/17309 no-kill
        ///   strong 700   6205/7/11888         -> 6321/4/10540
        ///   strong 900   5739/3/0 KILL        -> 4634/5/16568 no-kill
        ///   weak   600   4924/6               -> 3542/7/47957
        ///   weak   800   5287/5 no-kill       -> 3542/7/37954
        ///   weak   900   4439/5               -> 3665/8/31102
        ///
        /// The apex of that trade is exactly the "one axis vetoed globally" failure
        /// already recorded twice this round: at 600 px the guard touches a large
        /// fraction of the fight and vetoes descents the circuit needs.
        ///
        /// So the reach stays at the column's own half-width. The guard is then a
        /// conservative rule that helps the weak low band (weak 800 gains a kill) and
        /// does not fix 700/800/1000 -- which is stated plainly rather than papered
        /// over. Fixing those three needs the descent cut BEFORE the lock, not a veto.
        ///
        /// WHAT ROUND 153 ADDED, and why this rule shape is now abandoned. Type 386 is
        /// NOT a single column: the Boss lays a CASCADE of 25 sub-tornadoes along its
        /// charge path. Measured at tick 4048 of strong 1000 they spanned x 1330..1485
        /// and y 5140..6049 with scales 0.375..1.5 and sizes 56x15 .. 225x63 -- a WALL
        /// roughly perpendicular to the charge, straddling the whole band, not a point
        /// hazard. The player is killed while DESCENDING through that wall: from y 5967
        /// down to 5568, ending level with a sub-tornado whose top edge is 10 px below
        /// them. No per-frame axis veto can fix that, because by the time any single
        /// sub-tornado is the nearest one the player is already inside the wall's
        /// vertical span. Refuted three times this round: unconditional suppression
        /// (strong 300 -7218 ticks), reach 600 (two kills lost, targets still
        /// byte-identical), and margin 400 (byte-identical everywhere). The rule shape is
        /// exhausted; the next attempt must be geometric -- keep the player clear of the
        /// cascade's BAND, not of its nearest member.</summary>
        private const float TornadoAxisGuardHorizontalReach = 125f;
        /// <summary>Upper end of the simulated-DPS band where the axis guard earns its
        /// place, and the reason it is gated at all.
        ///
        /// MEASURED (round 152, obsidian, 320 tiles, 2 rows, both arms):
        ///   weak   600   4924/6 no-kill -> 6130/6 (longer life, same hits)
        ///   weak   800   5287/5 no-kill -> **6381/3 KILL (new)**
        ///   weak   900   no-kill -> no-kill (unchanged)
        ///   weak  1100   **4788/3 KILL -> 3875/6 DEATH**
        ///   weak  1200   3082/8 -> 3065/8
        ///   strong 300/600/700/800/1000/1100/1200/1500/2000: unchanged
        ///
        /// So the guard is a genuine gain in the weak low band and a genuine loss
        /// above ~1000 (at 1100 the reviewed circuit already has the kill). The
        /// ceiling sits at the top of the measured-gain band. As with the standoff,
        /// this is DPS-conditional, not a claim that the rule is universally correct.</summary>
        private const float TornadoAxisGuardDefaultMaxDps = 900f;
        private const string TornadoAxisGuardMaxDpsVariable = "CHAITE_TORNADO_AXIS_GUARD_DPS_MAX";
        private readonly bool _tornadoAxisGuard = ReadTornadoAxisGuard();
        /// <summary>Separate from the recall horizon. See <see cref="TornadoResponseHorizon"/>.
        /// The ESCAPE branch and the jump suppression it implies are gated on this, while
        /// mere RECALL of where the column landed is gated on <c>_tornadoTicksLeft</c>.</summary>
        private int _tornadoResponseLeft;
        /// <summary>Observed x-extent of the live type-386 CASCADE.
        ///
        /// MEASURED (round 154): type 386 is not one projector. A single Sharknado lays a
        /// chain of up to 25 sub-tornadoes, and the chain is a narrow vertical WALL --
        /// at strong 1000 tick 4128 the whole thing occupied x 1315..1533, i.e. 218 px of
        /// an arena that is 16..5120 wide, while spanning y 5140..6049 (909 px) with
        /// per-instance sizes from 56x15 to 225x63. So there were 1300 px of clearance on
        /// one side and 3646 px on the other.
        ///
        /// The measured failure is then embarrassingly simple: the player sits INSIDE that
        /// 218 px x-range for 390 ticks (strong 1000) and descends through the wall,
        /// whereas strong 900 -- which kills -- is never inside the band at all (its two
        /// chains sit at x 968..988 and 4287..4290 and the player runs between them).
        /// Nothing was stepping sideways out of a narrow, essentially static column.
        ///
        /// These track the observed extent so the escape has a direction. They are reset
        /// whenever no 386 is live.</summary>
        private float _cascadeLeft = float.NaN;
        private float _cascadeRight = float.NaN;
        /// <summary>True when the player is inside the chosen wall's x-footprint AND
        /// inside its vertical collision band -- i.e. this wall can actually hurt it.
        /// See <see cref="ObserveCascade"/> for why the vertical half of this test is
        /// the whole point.</summary>
        private bool _cascadeAtHazardDepth;
        /// <summary>Pixels of margin added to the observed cascade extent, so the exit
        /// target clears the outermost sub-tornado's own half-width rather than its
        /// centre. The largest member is 225 px wide (scale 1.5 of a 150-px base), so
        /// its centre-to-edge is 112.5.</summary>
        private const float CascadeEscapeMargin = 120f;
        private const string CascadeEscapeVariable = "CHAITE_CASCADE_ESCAPE";
        private const string CascadeDepthGateVariable = "CHAITE_CASCADE_DEPTH_GATE";
        /// <summary>Whether the sideways escape from a type-386 wall is armed. Default
        /// ON as of round 155, but ONLY together with the hazard-depth gate below --
        /// armed alone it is a net regression, because it destroys the extended low-DPS
        /// fight. Set to 0 to disable.
        ///
        /// MEASURED (round 154), escape armed with NO depth gate:
        ///   gains  strong 800 6011/5 -> 6388/1/0 KILL, strong 1000 4536/4 -> 5217/3/0
        ///          KILL, strong 1500 -> a zero-hit, weak 600/900/1500 -> kills
        ///   loses  strong 300 16090/8/0 KILL -> a death at 10202, strong 600 a kill ->
        ///          a death, strong 1200 a zero-hit -> a death
        /// The harm scaled with fight length, which is what pointed at the vertical
        /// dimension rather than at DPS: at low DPS the same x-overlap happens at
        /// altitude, where yielding the horizontal axis buys nothing.</summary>
        private readonly bool _cascadeEscape = ReadCascadeEscape();
        /// <summary>Whether the escape additionally requires the player to be inside the
        /// wall's VERTICAL band. Default ON; <c>CHAITE_CASCADE_DEPTH_GATE=0</c> reverts to
        /// the ungated round-154 behaviour for A/B measurement.</summary>
        private readonly bool _cascadeDepthGate = ReadCascadeDepthGate();

        private static bool ReadCascadeEscape()
        {
            var raw = Environment.GetEnvironmentVariable(CascadeEscapeVariable);
            return raw != "0";
        }

        private static bool ReadCascadeDepthGate()
        {
            var raw = Environment.GetEnvironmentVariable(CascadeDepthGateVariable);
            return raw != "0";
        }
        /// <summary>Half-width of the largest possible type-386 member. The scale is
        /// (32 - ai[1]) * 1.5 / 32, so it reaches exactly 1.5 at ai[1] = 0, which is a
        /// 225x63 member and therefore a 112.5-px centre-to-edge.</summary>
        private const float CascadeMemberHalfWidth = 112.5f;

        private float _bandLeft;
        private float _bandRight;
        private float _floorY;
        private float _ceilingY;
        /// <summary>Empty-bar guard threshold, read once per instance so a whole
        /// fight is planned against one value. See RefillGuardVariable.</summary>
        private readonly float _refillGuardBudget = ReadRefillGuard();
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
            _chargeNormalHorizontal = 0;
            _chargeNormalVertical = 0;
            _chargeNormalSequence = -1;
            _counterDashSpent = false;
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
            // The lock. Native computes and freezes the charge velocity on this
            // one tick, so this is the last moment the player's position can
            // still influence where the charge goes, and the first moment the
            // real dodge may begin. Moving before it only re-aims the charge.
            if (dash && _previousState != state)
                LatchChargeNormal(player, in boss, input.NativeSequence);
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
                    _tornadoY = boss.Center.Y;
                    _tornadoTicksLeft = TornadoMemoryHorizon;
                    _tornadoResponseLeft = TornadoResponseHorizon;
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
            _dashSuppressRoute = input.Route;
            _currentTimer = input.NativeTimer;
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

            // Keep the observed type-386 cascade extent current, then use it to step
            // sideways out of the wall before anything else looks at the horizontal.
            //
            // MEASURED (round 154): the wall is ~218 px wide in an arena 5104 px wide,
            // so unlike the axis guard -- which had to win a reaction race against the
            // descent -- this correction is a monotone horizontal walk the wall cannot
            // undo. That is why it is applied unconditionally rather than as a veto.
            ObserveCascade(player, in bubble);
            int cascadeHorizontal;
            if (_cascadeEscape && TryEscapeCascade(player, out cascadeHorizontal))
            {
                horizontal = cascadeHorizontal;
                phase = phase + "-cascade-exit";
            }

            // Budget guard: with an empty bar and both feet off the ground the
            // player cannot move on the vertical axis at all, so every charge
            // that arrives in that state is unavoidable.
            //
            // MEASURED (dense native trace, script only, hit at tick 533): from
            // tick 508 to 533 the player was airborne with wingTime 0 on every
            // single row, vy between -0.98 and +1.02, i.e. hovering, and
            // sliding false throughout -- it never landed and never refilled.
            // The charge that killed it locked at tick 518 and the player
            // drifted from dy -74 to dy +5 under gravity alone while the normal
            // wanted to climb.
            //
            // Releasing jump is what lets the fall happen (holding it keeps the
            // wings deployed and holds vy near zero), and the native refill at
            // Player.cs:26992 restores wingTime on the landing tick. Climbing is
            // therefore suppressed only while the bar is empty and the player is
            // airborne: the descend branches are untouched, and a grounded player
            // is untouched so the takeoff that refills the bar still happens.
            // REFILL ONLY BETWEEN CHARGES.
            //
            // MEASURED (game-probe-rg-strong, the committed default): the bar
            // empties in stretches of 153-324 ticks, and the strong wing's first
            // hit at t=3259 sits 28 ticks into the t=3231..3514 stretch -- a
            // 284-tick window that is almost exactly the descent-to-landing time,
            // since a full bar is 180 and drains at about 1 per tick. So the
            // circuit flies the bar to empty and only then starts down.
            //
            // Scheduling the refill earlier WORKS on the target hits. Same
            // circuit, only CHAITE_REFILL_GUARD changed:
            //
            //   guard  0 -> hits [3259, 4271]        the stamina hit and the rear-end
            //   guard 60 -> hits [2256, 2484, 4564]  BOTH original hits GONE
            //   guard 90 -> hits [896, 2949, 3314, 3514, 3554]
            //
            // But every nonzero guard also made the total worse, because the
            // guard acts by converting a CLIMB into a DESCEND and the charges are
            // where the climb is the escape. guard 60 introduced its own hits at
            // 2256/2484, which are not near either original one.
            //
            // The two effects are NOT separable, which was the next hypothesis and
            // is also refuted: withholding the refill descend from the charge
            // states (CHAITE_NO_CHARGE_REFILL) made every threshold far worse,
            //
            //   guard 60, ungated -> 3 hits     guard 60, gated -> 9 hits
            //   guard 90, ungated -> 5 hits     guard 90, gated -> 8 hits
            //
            // so the descend-to-land DURING a charge is itself load-bearing -- it
            // is what actually gets the bar back. There is no window where the
            // refill is free, and the committed default of 0 already sits at the
            // optimum (2 hits, measured in §104.2).
            if (player.WingTime <= _refillGuardBudget &&
                !player.OnGround && vertical < 0)
            {
                vertical = 1;
                phase = "fishron-wing-refill";
                // The bar also refills at an APEX, not only on landing. Native:
                //
                //   Player.cs:26992
                //   if (((velocity.Y == 0f || sliding) && releaseJump) ||
                //       (autoJump && justJumped)) wingTime = wingTimeMax;
                //
                // so a frame with velocity.Y == 0 and the jump key released that
                // frame restores the whole bar in mid-air. `releaseJump` is the
                // release TRANSITION, so the key must be dropped and pressed
                // again -- which is what the forced descend below already does to
                // the command, but only if the release actually lands on an apex.
                //
                // MEASURED (game-probe-pin640-strong-6k): the player is airborne
                // for 100% of 6000 ticks and wingTime is exactly 0 for 33.5% of
                // them, so whatever refills it is not reliable. Arming the apex
                // release while the bar is empty is the direct fix; whether it
                // helps is what the run decides.
                if (ApexRefillArmed && vertical > 0 && player.Velocity.Y > -0.6f)
                    vertical = 0;
            }
            ApplyArena(player, ref horizontal, ref vertical);
            // REFUTED: holding `away` across the uncovered tail of the
            // dash-body-hit deferral (CHAITE_BODY_WINDOW).
            //
            // The deferral itself is the durable finding and it is EXACTLY 10
            // ticks, measured on every dash-body-hit that was followed by damage
            // in both 6000-tick runs: 4262->4272 (strong) and 1416->1426,
            // 1596->1606, 1773->1783 (weak). It is a constant -- independent of
            // loadout, of the closing geometry, and of the launch direction --
            // so the collision's 4 i-frames (Player.cs:21284) can never cover it
            // and the last 6 ticks are always exposed. The circuit spends exactly
            // those ticks pre-positioning for the NEXT charge, letting dx
            // collapse 67.9 -> 13.8 before the hit lands.
            //
            // Holding `away` over those 6 ticks was the narrowest possible form
            // of the repair and it still fails on the strong wing:
            //
            //   off: strong 6000/2 hits/42 dmg   weak 6000/3 hits/48 dmg
            //   on:  strong 6000/6 hits/27 dmg   weak 6000/3 hits/48 dmg
            //
            // Note the tell: damage FALLS 42 -> 27 while hits RISE 2 -> 6. The
            // rule is not being ignored -- it is converting two hard hits into
            // six soft ones, i.e. the override changes which contact lands rather
            // than preventing one. That is the same signature as every other
            // `horizontal` override, and it closes the last variant of the width
            // axis. Reverted.
            // REFUTED: holding the width open through the pre-charge window
            // (CHAITE_WIDTH_HOLD).
            //
            // §110's threshold is real -- AI_069 commits the charge as
            // `Normalize(player - boss) * 16f` (NPC.cs:35341-35343), so bvy =
            // 16*sin(theta) and the vertical race is winnable only while
            // dy/dx < 0.42. Measured over the whole fight that race is lost at
            // 49 of 70 locks, and dx is the cheap axis: at the t=4249 lock dx is
            // 410.2 against the 468 needed, with pvx only +4.40 while the wing
            // cruises at ~13.9.
            //
            // Forcing `away` in the pre-charge window is nevertheless FATAL on
            // both loadouts:
            //
            //   off: strong 6000/2 hits/no death   weak 6000/3/no death
            //   on:  strong 3265/7 hits/DEATH      weak 3413/7 hits/DEATH
            //
            // Pinning the horizontal direction destroys the horizontal BEAT
            // PATTERN, and the pattern is what desynchronises the circuit from
            // AI_069's attack clock. This is the same lesson as §78 (the
            // turnaround schedule must not be desynchronised to buy a local
            // separation). A rule that overrides `horizontal` for a whole window
            // is not affordable no matter what it buys at the lock, so the width
            // axis is closed. Reverted.
            // Steer the VERTICAL GAP AT THE LOCK, not the escape.
            //
            // AI_069 commits the charge at the lock (NPC.cs:35341-35343):
            //
            //   Vector2 vector124 = Main.player[target].Center - base.Center;
            //   vector124.Normalize();
            //   velocity = vector124 * 16f;
            //
            // so the charge is exactly 16 px/tick along the lock geometry and
            // there is no randomness to exploit. That turns the escape from the
            // lock into a two-axis race with known closing rates.
            //
            // MEASURED (game-probe-vf-strong, t=4249):
            //
            //   dx +410.2  dy +196.5  pvx +4.40  pvy +1.11  bvx +15.33  bvy +7.34
            //   the locked vector is 16 px/tick, so bvy/bvx = dy/dx = 0.479
            //   vertical escape needs |bvy| < the wing ceiling ~6.2
            //     -> 7.34 > 6.2, and the gap closes ~1.1/tick until contact
            //   the wing is simply slower than the charge on the vertical axis.
            //
            // The load-bearing relation is dy/dx at the lock. bvy = 16*sin(theta),
            // and |bvy| < 6.2 requires sin(theta) < 0.388, i.e.
            //
            //   dy/dx < 0.42
            //
            // The actual lock is dy/dx = 0.479 -- just ABOVE the threshold, so the
            // Boss gets 7.34 of vertical against a 6.2 ceiling and wins the race
            // by ~1.1/tick. Every earlier attempt steered the escape AFTER the
            // lock; by then dy/dx is fixed and the race is already lost. This is
            // the first thing in the session to act BEFORE the commit, when the
            // ratio is still controllable.
            //
            // THIS MUST RUN IN Tick, NOT ChargeEscape. ChargeEscape is called only
            // when the Boss is already in a charge state (1/6/11); at t=4230 the
            // Boss is still in its pre-charge state, so `dash` is false and Cruise
            // runs instead. A first version of this was placed in ChargeEscape and
            // never fired once in 6000 ticks -- the phase string appeared 0 times.
            // REFUTED: steering the vertical gap AT the lock (CHAITE_BAND_TARGET).
            //
            // AI_069 commits the charge at the lock (NPC.cs:35341-35343) with
            // `velocity = Normalize(player.Center - Center) * 16f`, so the charge
            // direction IS the lock geometry and bvy = 16*sin(theta). The measured
            // lock is dx +410.2 / dy +196.5, i.e. dy/dx 0.479, giving bvy 7.34
            // against a wing ceiling of about -6.2 -- the Boss wins the vertical
            // race by ~1.1/tick and the boxes meet at t=4268-4272.
            //
            // The load-bearing relation is therefore dy/dx at the lock, since
            // vertical escape needs 16*sin(theta) < 6.2, i.e. dy/dx < 0.42, and
            // 0.479 is just above it. Keeping the dive so the lock lands at a
            // smaller ratio is a genuinely different axis from every earlier
            // attempt, all of which steered the escape AFTER the ratio was fixed.
            //
            // It was implemented, confirmed to FIRE (31 times in the strong run,
            // phase string present), and it is WORSE on both loadouts:
            //
            //   off: strong 6000/2 hits/no death   weak 6000/3/no death
            //   on:  strong 6000/3 hits            weak 3543/10 hits/DEATH
            //
            // Note it must live in Tick, not here: ChargeEscape runs only for
            // states 1/6/11, but the rule has to act while the Boss is still in
            // its pre-charge state, where Cruise is the branch that runs. Placed
            // here it never fired once in 6000 ticks. Reverted.
            // Break the co-location, but ONLY for the strong wing.
            //
            // THE MECHANISM IS VALIDATED. §81.2 showed every strong-wing body
            // contact happens as the Boss's centre crosses the player's, and this
            // rule removes those contacts: measured at the cap, the four body
            // hits at ticks 2963, 3439, 4745 and 5396 are ALL ELIMINATED, the
            // phase fires 63 times, and the strong wing still survives the cap.
            //
            // It is loadout-gated because the same rule REGRESSES the weak wing:
            //
            //   strong wing   hits 11 -> 8,  npc contact 8 -> 5,  survives the cap
            //   weak wing     ticks 4598 -> 2224, hits 8 -> 7, death TRUE
            //
            // The weak wing fires the rule 76 times in a 1986-tick life and dies
            // half as far in. That is the §79 pattern again: spending wing time
            // on vertical commitment costs the Fairy wings more than the
            // separation buys, because their budget is 130 ticks against the
            // Fishron wing's 180. The latched-normal clause that was tried first
            // (`_chargeNormalVertical <= 0`) suppressed the rule COMPLETELY -- the
            // in-script counter read 0 while a post-hoc count said 67 -- so it
            // was removed; the gate belongs on the loadout, not on the latch.
            if (CoLocationRoute(input.Route) &&
                dash && vertical >= 0 && !player.OnGround)
            {
                var gapY = Math.Abs(player.Center.Y - boss.Center.Y);
                // MEASURED (dense native trace, strong 300, 8812 ticks): this rule
                // fired on 59 frames and 52 of them had `wingTime == 0`, where the
                // commanded lift cannot fly -- `output.Jump = vertical < 0` only
                // asks for a jump the wing gate (Player.cs:27001) refuses. The
                // failure sequence is visible in the trace:
                //
                //   t=3482  wingTime 0, plvy +3.34 (terminal fall), |dy| 17
                //   t=3483..3486  plvy +3.34 every tick -- the lift is inert
                //   t=3487  CONTACT at |dx| 7.4, |dy| 41.2; plvy snaps to -3.50,
                //           which is the SHIELD's fixed recoil, not wing flight
                //
                // GATING ON WING BUDGET WAS TRIED AND IS A NET NEGATIVE, so the
                // rule is left exactly as it was. With a `WingTime > 6` gate the
                // band regressed: strong 300 went 8812 ticks -> 7340, strong 600
                // lost its KILL and died at 6301, strong 1000 went 3 hits -> 4, and
                // nothing improved. The inert lift turns out to have been doing
                // positional work even without flight -- those five ticks hold the
                // player inside the band instead of letting the fall carry them
                // along the Boss's line -- so suppressing it changed the geometry
                // for the worse. Recorded because the reasoning was sound and the
                // measurement disagreed, which is the same lesson as 122 and 129.5.
                if (gapY < CoLocationBand)
                {
                    vertical = -1;
                    phase = "fishron-wing-colocation-lift";
                    _coLocationLiftCount++;
                }
            }
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
                Math.Abs(player.Center.X - _tornadoX) < TornadoClearanceRadius;
            if (tornadoOverrides)
            {
                horizontal = away;
            }
            else if (player.OnGround ||
                Math.Abs(player.Velocity.X) < EscapeSpeedFloor)
            {
                // A player who is not already moving on the horizontal axis
                // always flees on it, whatever the pattern asks for.
                //
                // MEASURED (dense native trace, current build, hit at tick
                // 3019): the circuit was in this branch with a neutral pattern
                // charge, so horizontal was 0 -- plan horizontal 0, controls
                // L 0, R 0 -- for the entire approach while the boss descended
                // from x 597 to x 508 at bovy 15.8 and the player sat at
                // exactly plX 640 with vx 0.00.
                //
                // Keying this on being grounded alone was not enough, and the
                // trace says why: at tick 3005 the player had wingTime 0 and
                // was falling at vy 10.01 with slowFall set, so it was airborne
                // rather than standing, and the grounded test did not fire. An
                // airborne player with no horizontal speed is in the same trap
                // as a grounded one -- the wings cannot build horizontal speed
                // from nothing any faster than the ground can, and a charge
                // closes at 14.7 -- so the condition is "has no horizontal
                // escape speed", not "is touching the floor".
                //
                // A neutral charge is a legitimate way to vary the dodge, but
                // only once the player is already leaving the line at cruise
                // speed. From a standstill it also mis-aims the dash, which
                // writes velocity.X in the facing direction, so a neutral
                // horizontal points the dash at the boss as well. Fleeing costs
                // the pattern nothing it needs: the pattern exists to
                // desynchronise from AI_069's group clock, and that still
                // happens while a stalled beat simply runs away.
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
            else if (_chargeNormalSequence >= 0)
            {
                // The locked charge has a latched perpendicular. It outranks the
                // pattern and the leg schedule because those exist to move the
                // player around the arena, while this exists to leave the line
                // the Boss has already committed to. Recomputing `away` from the
                // Boss's current side instead -- which is what every branch below
                // does -- flips the sign as the Boss crosses the player, and the
                // measured result was that diagonal charges spent most of their
                // length travelling back across the locked line.
                horizontal = _chargeNormalHorizontal;
                // MEASURED (dense native trace, strong 300): the escape the circuit
                // executes splits diagonally against the locked line rather than
                // crossing it. Over the 3060 charge frames of that run the velocity
                // relative to the Boss's charge direction is |along| 5.64 and
                // |across| 10.15; inside the 7-tick arrival window of the 8 hits it
                // becomes |along| 5.62 and |across| 5.43. The ALONG component is
                // unchanged and the crossing rate collapses, and crossing needs
                // 85 px of perpendicular clearance.
                //
                // STEERING THE HORIZONTAL COMMAND TO THE EXACT PERPENDICULAR OF THE
                // CHARGE WAS TRIED AND REFUTED. Choosing the perpendicular of the
                // frozen charge velocity, on the side the latch already leaned to,
                // collapsed the band:
                //
                //   strong 300   8812 / 8 / 36546   ->  3722 / 6 / 62035
                //   strong 600   6744 / 6 / KILL    ->  3722 / 6 / 46130
                //   strong 800   5057 / 4 / 17755   ->  3631 / 6 / 36740
                //   strong 1200  4437 / 1 / KILL    ->  4439 / 4 / KILL
                //
                // Strong 300 and 600 die on the SAME tick, 3722, which is the
                // broken-invariant signature already recorded in section 126: the
                // change made the trajectory independent of the fight it was in.
                // The knob was removed rather than left default-OFF, so it cannot be
                // half-restored later.
                //
                // The instructive part is that the horizontal COMMAND was already
                // the latched normal, so the diagonal must arise downstream -- the
                // executed velocity is the sum of the command, the wing's own
                // horizontal rate (measured 7-8 px/tick, not the 13.87 of
                // wingAccRunSpeed), and the vertical beat. A command-space fix cannot
                // reach that, which is why this failed where the tornado-clear
                // wind-up fix (129) succeeded: that one restored a needed
                // pre-condition instead of redirecting an already-correct command.
                // REFUTED: running and dashing ALONG a locked horizontal charge.
                //
                // All four hits that share this anatomy do dash INTO the charge --
                // at strong t=4271 the player is at x=4673 and the Boss charges
                // right at +15.33, yet the dash sends the player LEFT at -14.50,
                // closing at 29.83 px/tick so the Boss arrives in 14 ticks, where
                // dashing along would close at 6.83 and take 63. The geometry is
                // real and the dash direction is genuinely perpendicular to the
                // charge. Forcing it along nevertheless made BOTH arms worse:
                //
                //   off: strong 6000/2 hits/4 contacts   weak 6000/3/3
                //   on:  strong 6000/7 hits/3 contacts   weak 6000/4/3
                //
                // The 14-tick head-on arrival is therefore not the binding
                // constraint -- opposing the charge and crossing its path is what
                // the circuit wants, and the "closes faster" reading is a
                // superficial one. Left in the tree as a documented refutation.
            }
            else
            {
                horizontal = away;
            }
            // REFUTED: facing away from the charge before the lock
            // (CHAITE_CHARGE_FACING).
            //
            // The rear-end diagnosis is right -- all five hits have the dash-body
            // hit 8 ticks before contact at strike depth 1, with the recoil
            // reversing vx (+9.00) against the Boss's +15.33, so the Boss
            // overtakes -- but steering the FACING along the charge ahead of the
            // lock made both arms much worse:
            //
            //   off: strong 6000/2 hits/4 contacts   weak 6000/3/3
            //   on:  strong 6000/6 hits/1 contact    weak 5317/DEATH/10
            //
            // and strong boss damage collapsed to 9, i.e. the circuit mostly
            // stopped engaging. The recoil direction is not the thing to steer.
            // Reverted.
            // The owner's distance exception applies to the WHOLE escape, not just beat
            // 0: taken from far enough out, the diagonal beats 1 and 2 buy clearance the
            // player does not need while spending altitude it does, so hold the altitude
            // and let the horizontal pull-away do the work.
            if (LockRunAwayDistance > 0f && _chargeLockDistance >= LockRunAwayDistance &&
                _chargeNormalSequence >= 0)
            {
                vertical = 0;
                phase = "fishron-wing-charge-run-away";
            }
            else switch (_chargeBeat)
            {
                case 0:
                    // Beat 0 is the horizontal beat, which used to ask for no
                    // vertical input at all. A grounded player then stays
                    // grounded for the whole beat, and that is fatal in a way
                    // that has nothing to do with which way it runs: measured
                    // ground acceleration under the move-speed debuff is about
                    // 0.08 px/tick^2, so a player caught at a standstill
                    // accelerates to roughly 0.5 px/tick over six ticks while
                    // the charge arrives at 14.7. The wings are the only
                    // mobility that matters, at a measured 13.87 cruise, and
                    // they do nothing from the ground.
                    //
                    // MEASURED (dense native trace): at ticks 3003-3013 the
                    // player is in exactly this phase with wingTime 0, plvx
                    // 0.0, and it takes until tick 3019 to reach plvx 4.5 --
                    // by which point the boss body is already overlapping.
                    //
                    // So a grounded player on the horizontal beat takes off
                    // instead. Contact with support refills the flight budget
                    // (Player.WingMovement restores wingTime on landing), so
                    // this is not spent twice, and being airborne is what makes
                    // the horizontal beat mean anything.
                    //
                    // The latched normal decides the vertical beat as well: a
                    // mostly horizontal locked charge has a mostly vertical
                    // normal, and that diagonal is the whole escape. The float
                    // parts of the closed-loop pattern never applied here -- apex
                    // taps and beat schedules cannot move the player off a line
                    // the Boss has already frozen.
                    vertical = _chargeNormalSequence >= 0
                        ? _chargeNormalVertical
                        : (player.OnGround ? -1 : 0);
                    phase = "fishron-wing-charge-horizontal";
                    break;
                case 1:
                    // The ascend beat always asks for lift, but a normal that
                    // wants to descend keeps that instead: climbing back onto the
                    // locked line is the one direction that must not happen.
                    vertical = _chargeNormalSequence >= 0 && _chargeNormalVertical > 0
                        ? 1
                        : -1;
                    // MEASURED REFINEMENT (dense native trace, strong 600, the
                    // t=2251 hit). The lift above is the beat's default, and it is
                    // wrong whenever the Boss is BELOW the player: the charge line
                    // then runs upward through the player, so lifting moves the
                    // player ALONG that line instead of off it. In the measured
                    // hit the Boss was below (boss y 4430.7 vs player y 4329.5,
                    // i.e. dy -101.2) and the beat climbed, which held |dy| inside
                    // the 71 px body box (62.3, 51.2, 40.3, 32.6, 25.2, 18.1,
                    // 11.3, 4.8, 1.5 ... 27.1) for the whole arrival while |dx|
                    // only just cleared 85. Both boxes were satisfied for ticks
                    // 2239-2245, which is the contact.
                    //
                    // The escape that works is perpendicular to the frozen line:
                    // simulated against the real lock geometry, a perpendicular
                    // cruise of the wing's own 13.87 takes |perp| clear with NO
                    // contact at all, while the horizontal-only escape contacts at
                    // tick 3. The missing component is vertical, and it is ~10
                    // px/tick against the measured ~0.
                    //
                    // So the rule is one-sided: never lift while the Boss is
                    // below, never dive while it is above. That is the exact
                    // content of "do not climb back onto the locked line", applied
                    // to the beat that was free to violate it.
                    // MEASURED (round 161): REMOVING this gate is a net negative,
                    // so it stays. The rule itself is a genuine correctness fix --
                    // the guard fires only when the charge is LOCKED and the Boss is
                    // genuinely below, which is exactly when lifting moves the
                    // player along the frozen line. But ungating it regressed the
                    // measured points: strong 800 went 4 hits -> 6, strong 1000
                    // 3 -> 5, and weak 1500 went from a 4-hit KILL to a death at
                    // 3131 ticks, while weak 1000 gained a kill. A net loss at the
                    // top of the useful band, so the switch stays and stays OFF.
                    //
                    // The lesson is the one already recorded in 122 and 128: a rule
                    // that is CORRECT in isolation can still be a worse controller
                    // than the heuristic it replaces, because the heuristic is
                    // entangled with the beat schedule.
                    if (ChargeClimbAwayArmed && _chargeNormalSequence >= 0 &&
                        !float.IsNaN(_chargeNormalVertical) &&
                        player.Center.Y >= boss.Center.Y)
                    {
                        vertical = 1;
                    }
                    phase = "fishron-wing-charge-ascend";
                    break;
                default:
                    vertical = _chargeNormalSequence >= 0 && _chargeNormalVertical < 0
                        ? -1
                        : 1;
                    phase = "fishron-wing-charge-descend";
                    break;
            }
            // NOTE: the band-target rule that used to live here was UNREACHABLE.
            // ChargeEscape is entered only when the Boss is already charging
            // (`dash`, i.e. state 1/6/11), but the rule has to act BEFORE the
            // lock, while the Boss is still in its pre-charge state and Cruise
            // is the branch that runs. It now lives in Tick, after ApplyArena.
            // Measured: in this position the phase string appeared 0 times in
            // 6000 ticks.
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
            {
                // CONTROLLED EXPERIMENT, in the ACTIVE path this time.
                //
                // The dash is horizontal (dashType 2, speed ~14.5). MEASURED
                // (game-probe-pin640-strong-6k, the t=2486 hit): at the lock the
                // escape normal is 97% vertical, but the dash fires along the
                // charge axis at vx -14.50 against a +16.97 charge -- it cannot
                // win on that axis, and it replaces the climb for nine frames,
                // leaving |dy| 40.2 against the 71 needed.
                //
                // §95 tried this inside DecideMovement, which is the
                // policy-only hook and returns immediately when no policy is
                // configured, so it never ran. This copy is in ChargeEscape,
                // which is the method that actually produces the proposal.
                // Dash TIMING: the shield's i-frames must still be running when
                // the Boss actually arrives, and the charge takes ~29 ticks to
                // cross while dashType 2 grants 15.
                //
                // MEASURED (game-probe-fin-weak-dflt, all three weak-wing hits,
                // and game-probe-fin-strong-dflt t=4271 -- four hits on BOTH
                // loadouts): the script burns the charge's dash 2-5 ticks after
                // the lock, so the i-frames expire about 5 ticks BEFORE contact.
                //
                //   lock 1406 -> dash 1408 -> i-frames die 1420 -> contact 1424
                //   lock 1584 -> dash 1586 -> i-frames die 1600 -> contact 1604
                //   lock 1758 -> dash 1760 -> i-frames die ~1774 -> contact ~1778
                //   lock 4250 -> dash 4250 -> i-frames die 4265 -> contact 4271
                //
                // This is the same error on both loadouts, which is why the two
                // arms fail at the same kind of tick. Holding the dash until
                // DashDelay ticks after the lock puts the i-frames over the
                // arrival instead. 0 = the fixed circuit.
                // COUNTER-DASH (round 161). An external guide for this fight names
                // "Shield of Cthulhu counter-dash for i-frames" as THE core
                // survival mechanic, and the native code says why:
                //
                //   Player.cs:31602  (dash == 2 && i == eocHit && eocDash > 0)
                //       -> while the dash is live the player takes NO contact
                //          damage from the NPC the dash touched
                //   Player.cs:21284-21292
                //       eocDash = 10; dashDelay = 30;
                //       velocity.X = -sign * 9; velocity.Y = -4f;   // recoil, its own
                //       GiveImmuneTimeForCollisionAttack(4);
                //       eocHit = i;
                //
                // So dashing INTO the charge makes the player immune to that body,
                // deals the shield's contact damage, and recoils the player clear.
                // Dashing AWAY -- which is what the escape branch does -- buys
                // nothing against the body, because the body arrives faster than
                // the player can leave.
                //
                // The circuit already produces dash-body-hits, and section 97
                // measured that removing them entirely kills both arms and starves
                // the fight of damage. What it has never done is TIME the dash to
                // be live when the body arrives. This arm spends the dash once the
                // closing Boss is inside CounterDashPixels, then holds it. Default
                // OFF so the reviewed circuit is reproduced exactly.
                var counterDash = false;
                if (CounterDashArmed && _chargeNormalSequence >= 0 && !_counterDashSpent)
                {
                    var cbx = boss.Center.X - player.Center.X;
                    var cby = boss.Center.Y - player.Center.Y;
                    var cd = (float)Math.Sqrt(cbx * cbx + cby * cby);
                    // The gate is TICKS TO CONTACT, not distance. The dash's
                    // immunity is a 15-tick resource (eocDash) that ALSO gets cut to
                    // 10 the moment it touches, so what matters is issuing it
                    // CounterDashTicks before the body arrives. A distance gate was
                    // measured first and does nothing (128.3) because the closing
                    // speed varies with the lock geometry.
                    var closing = (float)Math.Sqrt(
                        boss.Velocity.X * boss.Velocity.X +
                        boss.Velocity.Y * boss.Velocity.Y);
                    var ticksToContact = closing > 0.01f ? cd / closing : 9999f;
                    if (ticksToContact <= CounterDashGap)
                    {
                        counterDash = true;
                        _counterDashSpent = true;
                        // Steer AT the Boss, not away from it. The whole point of
                        // the counter-dash is to make contact while `eocDash` is
                        // live: Player.cs:31602 grants contact immunity to the NPC
                        // the dash touched, and Player.cs:21288 recoils the player
                        // clear. MEASURED: of the 20 baseline dash frames, 9
                        // already move toward the Boss, but the escape branch's
                        // `AwayFromBossAxis` direction means the dash is spent
                        // running away and never touches.
                        horizontal = cbx > 0f ? 1 : (cbx < 0f ? -1 : 0);
                    }
                }
                if (counterDash)
                {
                    dash = true;
                    phase = "fishron-wing-charge-counter-dash";
                }
                else if (_chargeNormalSequence >= 0 &&
                    ChargeTicksSinceLock < DashDelay)
                {
                    // still too early: hold the proposal, spend nothing
                }
                else if (Math.Abs(player.Center.Y - boss.Center.Y) <
                    DashSuppressGap(_dashSuppressRoute))
                {
                    // escape is mostly vertical; a horizontal dash cannot win
                    // along the charge axis (see the controlled sweep in §97)
                }
                else if (NoChargeDashArmed)
                {
                    // REFUTED, AND THE MOST DECISIVE NEGATIVE RESULT OF THE SESSION.
                    //
                    // The dash-body-hit is LOAD-BEARING, not a liability. Removing
                    // the charge dash kills BOTH arms outright:
                    //
                    //   off: strong 6000/2 hits/no death   weak 6000/3/3
                    //   on:  strong 2406/7 hits/DEATH      weak 1636/7/DEATH
                    //        and boss damage 0 on both -- the circuit did not
                    //        damage the Boss at all before dying
                    //
                    // So the dash-body-hit is not merely trading 4 i-frames for a
                    // recoil that reverses vx. It is structurally required, and the
                    // rear-end reading in the comment above is incomplete: the same
                    // impact that costs the hit at t=4271 is what keeps the other
                    // 5990 ticks alive. The "boss damage 0" also shows the
                    // circuit's damage comes through this contact path, so removing
                    // it starves the fight as well. Reverted.
                }
                else
                    dash = true;
            }
            // LAST, so it overrides every branch above including the personal-space
            // latch and the counter-dash. Neither of those is a reason to fly into a
            // column: the column's contact damage lands on its own and does not care
            // why the player was descending.
            ApplyTornadoAxisGuard(player, ref vertical);
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
            if (_tornadoResponseLeft > 0) _tornadoResponseLeft--;
            ApplyTornadoAxisGuard(player, ref vertical);
            var gap = boss.Center.X - player.Center.X;
            // DECOUPLED (round 150). The escape branch runs for the RESPONSE window
            // while the column is merely RECALLED for the longer memory horizon, so
            // that a longer memory does not also prolong the jump suppression that
            // this branch's `return` implies (see TornadoResponseHorizon). When both
            // horizons are equal and the radii are equal this is the reviewed circuit
            // exactly: `_tornadoResponseLeft > 0 && |dx| < 760` reduces to the old
            // `_tornadoTicksLeft > 0 && |dx| < 760`.
            var responseLive = _tornadoResponseLeft > 0;
            // The escape radius is a SEPARATE knob from the reviewed TornadoClearance.
            // MEASURED BUG (round 150): the first version of this decoupling evaluated
            // the reviewed radius on the gate, which made CHAITE_TORNADO_RECALL a dead
            // variable -- setting it to 2000 changed nothing at all, twice, while the
            // response knob alone produced a different run. A knob that is read but not
            // used is worse than no knob, because it makes an inert result look like a
            // tested hypothesis. The gate now honours TornadoRecallRadius, whose default
            // IS TornadoClearance, so the reviewed circuit is still exact.
            var gate = Math.Abs(player.Center.X - _tornadoX) < TornadoRecallRadius;
            if (gate && responseLive)
            {
                // Still inside the column the last Sharknado left behind.
                //
                // MEASURED CORRECTION (round 148). The reviewed gate tests the
                // HORIZONTAL distance only, and the comment here used to assert that
                // "horizontal distance is the whole defence" because Sharkrons from a
                // fixed tornado have limited range. The dense traces say both halves
                // of that are wrong, on both wings:
                //
                //   Cthulhunado (projectile 386) contact events, with |dx| / |dy|:
                //     strong  5952: |dx| 401  |dy| 172
                //     strong  9563: |dx| 753  |dy|   9
                //     strong  9603: |dx| 804  |dy| 216
                //     strong  9643: |dx| 621  |dy| 254
                //     weak    6213: |dx| 423  |dy| 202
                //     weak    9526: |dx| 325  |dy| 121
                //     weak    9567: |dx| 251  |dy| 342
                //
                // Those cannot be body contact (the body box is 85 x 71) and they are
                // not Sharkron either -- the projectile list at those ticks is
                // dominated by type 386. So the column damages at 250-800 px
                // horizontally AND at up to 342 px vertically, i.e. outside the 760 px
                // gate entirely and at a vertical distance nothing models. The column
                // grows as it lives: measured width 56 -> 225 and height 15 -> 63 with
                // scale 0.375 -> 1.5, so the late-phase column is a large box, not a
                // thin spike.
                //
                // The fix is to model the vertical half. If the player is horizontally
                // inside the column, escape on whichever axis needs the smaller move,
                // and treat the column's own vertical extent as live.
                var dxToColumn = player.Center.X - _tornadoX;
                var dyToColumn = player.Center.Y - _tornadoY;
                var outX = Math.Abs(dxToColumn) - TornadoHalfWidth;
                var outY = Math.Abs(dyToColumn) - TornadoHalfHeight;
                if (TornadoVerticalArmed && outX < 0f && outY < 0f)
                {
                    // Inside the column box: leave by the cheaper axis.
                    if (-outX <= -outY)
                    {
                        horizontal = dxToColumn >= 0f ? 1 : -1;
                        vertical = player.OnGround ? -1 : 1;
                    }
                    else
                    {
                        horizontal = 0;
                        vertical = dyToColumn >= 0f ? -1 : 1;
                    }
                    phase = "fishron-wing-tornado-box";
                    return;
                }
                // MEASURED NOTE (round 149): the horizontal escape is NOT blocked by
                // the arena edge, so do not add a wall fallback here. An earlier draft
                // of this round claimed a "wall pin" from a relative-coordinate
                // misreading -- the trace was read as world x = 15 against a column at
                // x = 61, i.e. 48 px apart. The raw record says otherwise: the player
                // is at x 2236 and the column at x 2476, i.e. **240 px** apart, and the
                // printed 15/61 were local-to-column offsets. A wall fallback written on
                // that reading measured byte-identical on both arms, which is what a
                // never-true condition looks like.
                //
                // The decompiled aiStyle 64 is still worth having, because it bounds
                // what this branch can ever achieve: `width = 150 * scale` and
                // `height = 42 * scale` with scale capped at 1.5 for type 386, so at
                // full growth the column is 225 x 63 -- about 112 px wide but only 31 px
                // per side vertically -- and its only motion is a `cos` sway of
                // amplitude `width/5 * 2 = 90` px on X. It is wide and flat, and it
                // never moves vertically.
                horizontal = _tornadoX >= player.Center.X ? -1 : 1;
                vertical = player.OnGround ? -1 : 1;
                // MEASURED DEFECT (round 161, the t=2236 lock of strong 600).
                //
                // This branch RETURNS, so the pre-charge jump below never runs
                // while it is active -- and it commands vertical = +1, which is
                // a DESCENT. The lock audit over all 29 charges of that run shows
                // why that is fatal: the vertical speed at the lock was climbing
                // (-5 to -12.8 px/tick) on 19 charges and falling (+10, the
                // TerminalVelocity) on 10, and NONE was flat. All 10 falls were
                // frames whose phase was `refill` with wingTime 0, EXCEPT the one
                // at t=2236, which is the hit: it was in `tornado-clear` with
                // wingTime 31 and plvy +2.76 -- already descending -- and the
                // charge committed from only 122 px away, giving about 7 ticks
                // before the body arrived. Every other charge in the run locked
                // from 300-1634 px with the player already climbing, because the
                // pre-charge jump had 20 ticks to load the vertical speed.
                //
                // The fix is to give this branch the same wind-up as every other
                // pre-charge frame instead of a descent. Holding the jump is what
                // loads the climb; it is not paid for twice because landing
                // refills the flight budget.
                if (PredictChargImminent(state, timer) && !player.OnGround)
                {
                    vertical = -1;
                    phase = "fishron-wing-tornado-clear-prejump";
                    return;
                }
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
                    // Vertical escape increases the gap on the vertical axis.
                    //
                    // This asked for "climb" whenever the Boss was above, which
                    // is the one direction that closes the gap: AI_069 hovers
                    // above the player and begins its charge from there, so a
                    // climb meets the incoming body. The comment three branches
                    // below already warns about exactly that for the pre-charge
                    // jump ("a climb from a few tens of pixels below the hover
                    // point flies straight into it") but the personal-space
                    // latch never got the same correction.
                    //
                    // MEASURED (dense native trace, hit at tick 3019): the Boss
                    // charged straight down at bovy 15.8 from x 609 while the
                    // player held x 640 and climbed with vy -6.2, so the body
                    // was moving down onto a player moving up. Contact boxes
                    // overlapped 14 px horizontally and 29 px vertically.
                    // Native Y grows downward, so running away from a Boss above
                    // means descending: vy positive.
                    //
                    // The sign was then inverted. `boss.Center.Y >= player.Center.Y
                    // ? 1 : -1` returns 1 when the Boss is BELOW the player (larger
                    // Y, since native Y grows downward), and 1 means descend -- so
                    // the latch drove the player down into a Boss that was already
                    // underneath it, and up into one that was above. Both branches
                    // closed the gap. Increasing the vertical gap requires moving
                    // toward +Y when the Boss is at smaller Y, which is this:
                    _personalSpaceVertical =
                        boss.Center.Y <= player.Center.Y ? 1 : -1;
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
                vertical = player.OnGround ? -1 : 1;
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
                // Grounded escapees jump. The escape branches used to ask for
                // "no vertical input" on the ground, which reads as neutral, but
                // the wing circuit has no ground mobility at all: measured in
                // the dense native trace, a landed player under a live charge
                // sits at |vx| 3.5 falling to 0.2, while the charge closes at
                // 14.7 px/tick horizontally. A grounded player that does not
                // jump is simply stationary, and a charge that locks its line
                // once and then travels 476 px cannot be left by standing on it.
                // Vertical -1 is what raises controlJump (see output.Jump =
                // vertical < 0), and wing ascent is slow, so the jump has to be
                // spent early rather than at contact.
                vertical = player.OnGround ? -1 : 1;
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
                vertical = player.OnGround ? -1 : 1;
                phase = "fishron-wing-tornado-bait";
                return;
            }
            if (StandoffViolated(gap, player, in boss))
            {
                // REFUTED, AND IT REFUTES THE "SMOOTHER STANDOFF" IDEA THAT THE
                // TEMPO ANALYSIS (132.5) SUGGESTED.
                //
                // The reasoning looked sound: the standoff's hard clamp produces
                // MORE charges (98 -> 109) while improving the hit rate per charge
                // (8.2% -> 7.3%), so almost all of its gain is cancelled by the
                // fight getting slower, and a form that keeps the cruise behaviour
                // and merely flips the patrol away from the Boss should keep the
                // dodge quality and give back the tempo.
                //
                // It does not. Keeping the patrol horizontal and only biasing its
                // direction was worse EVERYWHERE, and it broke the band invariant:
                //
                //                  hard clamp (kept)        patrol-flip
                //   strong  300    10004 / 8 / 30628         8094 / 10 / 40100
                //   strong  600     5734 / 5 / 26023         5743 /  6 / 25903
                //   strong  800     5398 / 4 / 13180         4887 /  5 / 20018
                //   strong 1200     4441 / 3 / KILL          3994 /  4 / 8922
                //   weak    300     5879 / 8 / 51124         2576 /  6 / 67798
                //   weak    800     6380 / 5 / KILL          2576 /  6 / 50840
                //
                // Weak 300 and weak 800 die on the SAME tick, 2576 -- the
                // broken-invariant signature of section 126 again, for the third
                // time this session.
                //
                // The lesson is that the standoff's tempo cost is LOAD-BEARING, not
                // waste. Fleeing the Boss axis outright and diving is what actually
                // buys the separation; merely reversing a patrol that was going to
                // turn around anyway does not. This is the same shape as 130.2 (the
                // inert wing-budget lift was doing positional work) and 131.2 (the
                // already-correct command was not the thing to steer): the working
                // rule's incidental-looking side effects are the mechanism.
                //
                // REFUTED: holding altitude through the wind-up instead of diving
                // (CHAITE_ALTITUDE_HOLD).
                //
                // The dive is real and it is expensive. MEASURED
                // (game-probe-rv-strong, the t=4271 hit): during the standoff at
                // ticks 4219-4229 the circuit drove the player DOWN at vy +6.0
                // rising to +10.00, cutting dy from 257.5 to 246.2 while the Boss
                // hovered with bovy ~0 at a fixed y 4077.8. The precharge jump then
                // had to reverse that dive, and the entire climb before contact
                // came to about 22 px -- dy was only -28.8 at the lock and -39.0
                // at contact, against the 71 the body box needs.
                //
                // Suppressing the dive while the Boss hovers above nevertheless
                // made BOTH arms worse:
                //
                //   off: strong 6000/2 hits/4 contacts   weak 6000/3/3
                //   on:  strong 6000/4 hits/5 contacts   weak 2946/DEATH/8
                //
                // Interfering with the wind-up dive costs the circuit more
                // elsewhere than the extra separation buys at the lock.
                horizontal = AwayFromBossAxis(gap);
                vertical = player.OnGround ? -1 : 1;
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
            // Flee along the centre-to-centre vector, not perpendicular to it.
            //
            // The perpendicular form is the reviewed counter-play for a charge
            // that has already LOCKED its line: once the Boss is committed, the
            // shortest way off that line is sideways. Applied as the default
            // escape for every close state it does the opposite of what the
            // fight needs, because the perpendicular to a vertical Boss-to-player
            // vector is purely HORIZONTAL and the perpendicular to a horizontal
            // one is purely VERTICAL. A Boss hovering directly above therefore
            // produced a purely vertical escape, which widened nothing on the
            // axis the charge was about to travel.
            //
            // MEASURED (dense native trace, hit at tick 3019): the Boss charged
            // straight down (bovx -6.2, bovy 15.8) from x 609 while the player
            // stood at x 640 with vx 0.0 for sixteen consecutive ticks, and the
            // boxes overlapped by 14 px horizontally and 29 px vertically at
            // contact. The player never moved on the axis that mattered.
            //
            // Running away from the centre instead always increases both gaps
            // at once, which is the property that matters when the charge's line
            // is not yet known. Transverse evasion is still wanted once a charge
            // is live and its line is fixed, and that is handled by the charge
            // branch rather than by this helper.
            var dx = boss.Center.X - player.Center.X;
            var dy = boss.Center.Y - player.Center.Y;
            Split(-dx, -dy, out horizontal, out vertical);
        }

        /// <summary>True once the current hover is close enough to its end that
        /// the next charge is imminent.</summary>
        /// <summary>Decides the charge dodge on the frame the Boss locks, and
        /// keeps that decision for the rest of the episode.
        ///
        /// aim is the locked charge direction, normalised, read straight off the
        /// geometry native used: the charge is aimed from the Boss centre to the
        /// player centre. The dodge axis is the perpendicular, because the locked
        /// speed (17, or 23 enraged) is far above maxRunSpeed (4.71) and the
        /// charge line cannot be outrun. Of the two perpendiculars the one that
        /// increases the player's clearance from that line is chosen.
        ///
        /// The owner's rule is the practical form of the same geometry: a locked
        /// charge leaves the player a diagonal dodge, upward when the Boss is
        /// above and downward when it is below, and straight-away running only
        /// when the lock was taken from far enough out. A mostly horizontal
        /// charge has a mostly vertical normal, which is that diagonal; a shallow
        /// one has a large horizontal component, which is the straight run.</summary>
        private void LatchChargeNormal(PlayerSnapshot player, in TargetSnapshot boss,
            int sequence)
        {
            _chargeNormalSequence = sequence;
            var dx = player.Center.X - boss.Center.X;
            var dy = player.Center.Y - boss.Center.Y;
            var lockDistance = (float)Math.Sqrt(dx * dx + dy * dy);
            _chargeLockDistance = lockDistance;
            // The owner's distance exception. Taken from far enough out, the diagonal
            // costs altitude the player needs later and buys clearance it does not, so
            // the straight horizontal pull-away is the better dodge. Default OFF.
            var runAwayDistance = LockRunAwayDistance;
            var runAway = runAwayDistance > 0f && lockDistance >= runAwayDistance;
            if (lockDistance <= 0.01f)
            {
                _chargeNormalHorizontal = 0;
                _chargeNormalVertical = 0;
                return;
            }
            var aimX = dx / lockDistance;
            var aimY = dy / lockDistance;
            // The two unit perpendiculars to the aim.
            var normalAX = -aimY;
            var normalAY = aimX;
            var normalBX = aimY;
            var normalBY = -aimX;
            // Prefer the perpendicular the player is ALREADY travelling along.
            //
            // This used to compare `dotA` and `dotB` of the offset onto the two
            // normals, but the offset is parallel to the aim by construction, so
            // both dot products are identically zero (measured: dx=339 dy=-179
            // gives dotA=-0.0, dotB=0.0) and the sign was decided by
            // floating-point noise on an exact tie. It therefore always picked
            // normal A. Flipping the tie-break was measured to be a total no-op
            // (identical hit ticks), which is the signature of an arbitrary
            // choice rather than a decision.
            //
            // MEASURED (dense live weak-wing run, the tick-950 contact): with
            // normal A latched the script commanded vertical=+1 (plan.Drop=true,
            // controlDown=true, controlJump=false) for the whole charge, so the
            // wing was gated off and vy decayed ballistically at exactly
            // gravity*3 = +0.40 per tick (-2.46 ... +0.34). The player needed to
            // leave the line UPWARD; the arbitrary normal sent it DOWN.
            //
            // Choose the perpendicular that carries the player FURTHER to the
            // side it is already on: project the offset (dx,dy) onto each normal
            // and keep the larger. The offset is the only quantity here that
            // discriminates, because the escape is about widening the clearance
            // between the player and the Boss, and the Boss is on the opposite
            // side of the aim from the player by construction.
            //
            // MEASURED (dense live weak-wing run at the cap, the three near-miss
            // charges of section 75): the velocity projection above was the wrong
            // discriminator. At the charge locked at tick 3781 the plan commanded
            // h=-1 for its whole length while the Boss closed from the LEFT, so
            // the script ran the player straight down the approach axis -- |dx|
            // fell 280 -> 36 and the centre distance bottomed at 86 px against
            // the 112 px every miss in that run achieved. At tick 3245 the input
            // produced vx = 0.00 for the entire charge.
            //
            // The velocity projection fails because velocity carries the history
            // of the previous decision: a bad earlier choice becomes the reason
            // to keep making it. The offset has no such feedback, and it is what
            // decides whether the nearest approach clears the body.
            var rateA = normalAX * player.Velocity.X + normalAY * player.Velocity.Y;
            var rateB = normalBX * player.Velocity.X + normalBY * player.Velocity.Y;
            // REFUTED: taking the vertical half of the escape straight from the
            // lock geometry (CHAITE_CHARGE_NORMAL_OWNER).
            //
            // The owner's rule is that a locked charge leaves a diagonal dodge --
            // up when the Boss is above, down when it is below -- with a straight
            // horizontal pull-away when the lock was taken from far enough out.
            // That rule has real support in the data: a lock-frame audit of 16
            // hits found the circuit already obeying it on 13 of them (81%), so
            // the rule describes what a working escape usually looks like.
            //
            // Implementing it as the discriminator, however, kills both arms, and
            // it does so DETERMINISTICALLY. Every strong-wing run at every DPS
            // collapsed to an identical death at a single tick:
            //
            //   clause 2 only (flip the sign on every lock):
            //     strong 600/1000/1500 -> death at tick 2151, 5 hits
            //     (baseline: 600 survives to 6000 on 6 hits; 1500 is a ZERO-HIT kill)
            //   both clauses (skip the flip when level, pull away when far):
            //     strong 600/1000/1200/1500 -> death at tick 1811, 5 hits
            //     weak 1500 -> kill, 4 hits (baseline 3); weak 1600 -> kill, 3 hits
            //
            // The identical tick across every DPS is the signature of a broken
            // invariant rather than of a bad heuristic: the outcome stops
            // depending on the fight at all. The cause is that the rule reads a
            // quantity the escape must not read. `dy` at lock is measured against
            // a Boss that is usually within a body length of the player, so its
            // sign is near-arbitrary and flips from charge to charge; obeying it
            // replaces a coherent escape with a coin flip, and the strong wing --
            // which has the flight budget to hold a clean line -- is the one that
            // loses most, because it was the arm actually using that line.
            //
            // So the 81% agreement is evidence that the rule is a good
            // DESCRIPTION of the escape, not that it is a good CONTROLLER for it.
            // The velocity projection below is left in place. Reverted.
            var normalX = rateA >= rateB ? normalAX : normalBX;
            var normalY = rateA >= rateB ? normalAY : normalBY;
            if (ChargeEscapeSimArmed)
            {
                // MEASURED REPLACEMENT (round 160). Everything above decides the
                // escape from the player's OWN PAST VELOCITY, and the comment at
                // 1742 already names the consequence: "a bad earlier choice
                // becomes the reason to keep making it". The dense native trace of
                // the t=2251 hit shows that consequence directly -- the latched
                // normal was a CLIMB (-1) while the frozen charge line ran upward
                // through the player (Boss below at lock: boss y 4430.7, player y
                // 4329.5), so the escape moved the player ALONG that line and held
                // |dy| at 62.3, 51.2, 40.3 ... 4.8, inside the 71 px body box, for
                // the whole arrival. The measured peak perpendicular clearance was
                // 72.6 px against the 85 the body box needs.
                //
                // The lock is fully determined: AI_069_DukeFishron commits
                // `velocity = normalize(player - boss) * 16f` and holds that exact
                // vector to the end of the charge (dense rows: boss velocity
                // (-12.12,-11.92), |v| 17.00, constant for every one of the 22
                // ticks). So the escape can be chosen by asking which command
                // actually clears the body, with no dependence on what the player
                // was doing before.
                //
                // Verified against the real geometry before writing it: a
                // perpendicular escape at the wing's own measured 13.87 cruise
                // gives NO body contact at all, and still gives none with the dash
                // removed; a horizontal-only escape contacts at tick 3 and the
                // measured flat escape contacts at tick 3 as well. The choice
                // therefore has to be made over 2-D directions, not over the
                // vertical sign alone -- which is exactly what killed the
                // CHAITE_CHARGE_NORMAL_OWNER attempt recorded above.
                var bx = dx;   // boss centre -> player centre, the same vector native aims along
                var by = dy;
                var bovx = boss.Velocity.X;
                var bovy = boss.Velocity.Y;
                var selfX = player.Velocity.X;
                var selfY = player.Velocity.Y;
                // WHY THE SIDE MATTERS AND NOT JUST THE CLEARANCE.
                //
                // Native Player.cs:31602:
                //   if (... || (dash == 2 && i == eocHit && eocDash > 0) || ...)
                //       continue;
                // The Shield's damage immunity during a dash applies ONLY to the
                // NPC the dash first touched (`eocHit`). A dash that lands on the
                // Boss therefore SPENDS the shield's immunity on the Boss and
                // leaves only the 4 tick collision immunity, while `eocDash` still
                // has ~11 ticks to run. MEASURED in the fatal charge: the dash
                // fired at t=2237, the body contact at t=2239 made the Boss
                // `eocHit`, the 4 i-frames lapsed around t=2243, and the hit landed
                // at t=2251 with the dash window still open.
                //
                // Both perpendiculars give the SAME clearance from the frozen line,
                // so clearance alone cannot choose between them. The tie has to be
                // broken by never dashing into the Boss, which is what the
                // candidate filter below does: a candidate whose motion has a
                // negative projection onto (player -> Boss) drives the player at
                // the Boss and is dropped.
                if (ChargeDashSideArmed && (normalX * bx + normalY * by) < 0f)
                {
                    // The fallback normal also has to point away from the Boss.
                    normalX = -normalX;
                    normalY = -normalY;
                }
                var bestX = normalX;
                var bestY = normalY;
                var bestScore = float.NegativeInfinity;
                for (var i = 0; i < DirectionCount; i++)
                {
                    float cx = ChargeEscapeSimDirections[i, 0];
                    float cy = ChargeEscapeSimDirections[i, 1];
                    if (ChargeDashSideArmed && (cx * bx + cy * by) < 0f)
                    {
                        // This candidate drives the player at the Boss, which is
                        // the one thing that must not happen while the dash is
                        // live: it would make the Boss `eocHit` and spend the
                        // shield immunity. Skip it entirely.
                        continue;
                    }
                    var score = ScoreEscapeDirection(
                        cx, cy, bx, by, bovx, bovy, selfX, selfY);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = cx;
                        bestY = cy;
                    }
                }
                normalX = bestX;
                normalY = bestY;
            }
            _chargeNormalHorizontal = Math.Abs(normalX) < 0.2f ? 0 : (normalX > 0f ? 1 : -1);
            _chargeNormalVertical = runAway
                ? 0
                : (Math.Abs(normalY) < 0.2f ? 0 : (normalY > 0f ? 1 : -1));
        }

        /// <summary>Candidate escape directions for the locked-charge dodge, in
        /// screen coordinates (y grows downward). The axes are 22.5 degrees apart
        /// so the chosen direction can put most of its speed on whichever axis the
        /// frozen charge line leaves open, instead of the four cardinal moves
        /// alone. `CHAITE_CHARGE_ESCAPE_SIM=1` arms the search.</summary>
        private static readonly float[,] ChargeEscapeSimDirections =
        {
            { 1f, 0f }, { -1f, 0f }, { 0f, -1f }, { 0f, 1f },
            { 0.92388f, -0.38268f }, { 0.92388f, 0.38268f },
            { -0.92388f, -0.38268f }, { -0.92388f, 0.38268f },
            { 0.38268f, -0.92388f }, { 0.38268f, 0.92388f },
            { -0.38268f, -0.92388f }, { -0.38268f, 0.92388f },
            { 0.70711f, -0.70711f }, { 0.70711f, 0.70711f },
            { -0.70711f, -0.70711f }, { -0.70711f, 0.70711f }
        };

        /// <summary>Number of candidate directions. NOTE: a 2-D array's `Length`
        /// is the product of both dimensions (32 here), so indexing a loop with it
        /// runs off the first dimension and throws IndexOutOfRangeException on the
        /// live tick -- which is how this was found.</summary>
        private const int DirectionCount = 16;

        /// <summary>Scores one escape direction by replaying the frozen charge
        /// against it and returning the least clearance it ever achieves.
        ///
        /// The Boss's path is already decided at the lock, so this is a
        /// deterministic forward simulation, not a prediction. The player's speed
        /// is the measured wing cruise (13.87) along the candidate direction, with
        /// a short ramp from its current speed so a direction it is already
        /// travelling is not unfairly rewarded or punished.
        ///
        /// The score is the minimum of the two body-box margins
        /// (`|dx| - 85`, `|dy| - 71`), maximised over the horizon. Using the box
        /// margins rather than the centre distance matters because the native
        /// contact test is a box test, and because a direction can keep the
        /// centres far apart while still sitting inside the box on one axis.</summary>
        private static float ScoreEscapeDirection(float cx, float cy,
            float bx, float by, float bovx, float bovy, float selfX, float selfY)
        {
            // MEASURED (dense trace, 1193 charge frames of strong 600): the
            // horizontal speed actually held during charges is 7-8 px/tick -- the
            // histogram peaks at 366 frames at 7 and 312 at 8 -- with a maximum of
            // 14.50 reached only on the 28 dash frames. A first version of this
            // used 13.87 for the cruise and therefore chose directions against a
            // speed 1.7x too high, which systematically under-bought the vertical
            // component. 8f is the measured mode.
            const float Cruise = 8f;
            const float Ramp = 0.25f;      // ticks needed to reach the commanded speed
            const int Horizon = 24;        // the charge reaches the player well inside this
            var px = -bx;                  // player position relative to the Boss at lock
            var py = -by;
            var vx = selfX;
            var vy = selfY;
            var margin = float.MaxValue;
            for (var t = 0; t < Horizon; t++)
            {
                vx += (cx * Cruise - vx) * Ramp;
                vy += (cy * Cruise - vy) * Ramp;
                px += vx;
                py += vy;
                var dx = px - bovx * t;
                var dy = py - bovy * t;
                var m = Math.Min(Math.Abs(dx) - 85f, Math.Abs(dy) - 71f);
                if (m < margin)
                    margin = m;
            }
            return margin;
        }

        private bool PredictChargImminent(int state, int timer)        {
            if (state != 0 && state != 5 && state != 10) return false;
            var limit = _hoverLimit[state];
            if (limit <= 0) return false;
            // Wing-aware lead: the weak wing climbs at about two thirds the strong
            // wing's rate, so it needs a longer wind-up to reach the same altitude
            // by contact. See WeakPreJumpTicks.
            var lead = PreJumpTicks;
            if (_dashSuppressRoute == FormulaRoute.FishronFairyWingsDash)
            {
                lead = WeakPreJumpLead;
            }
            else
            {
                // STRONG-WING PRE-CHARGE LEAD = 40, REVIEWED (round 156).
                //
                // The reviewed constant was 20. Raising it to 40 is the largest single
                // improvement measured this session on the strong arm, and it is the first
                // change that answers the arrival mechanism of section 150: the precharge
                // window is the only part of the circuit that can move the player off the
                // charge line BEFORE the line is locked, and 20 ticks of it did not.
                //
                // MEASURED, obsidian, full band, lead 40 (24k cap):
                //   300  9697/7/32136 DEATH   <- the ONE remaining failure
                //   600  8332/3/0 KILL          700  7221/3/0 KILL (was a death)
                //   800  6382/5/0 KILL (was not killed)
                //   900  5736/3/0 KILL          1000 5220/2/0 KILL
                //   1100 4795/1/0 KILL          1200 4437/5/0 KILL
                //   1300 4140/3/0 KILL          1500 3661/0/0 ZERO HIT
                //   2000 2881/0/0 ZERO HIT
                // Against the reviewed 20 the strong arm went from THREE failing points
                // (700, 800, 1000) to ONE, and gained a new zero-hit at 1500.
                //
                // It is NOT free: strong 300 dies at 9697 with 32136 Boss health left, where
                // the reviewed lead survived and killed at 16103/8/0. The two are on opposite
                // sides of a bistable cliff -- every lead >= 38 loses strong 300 and wins
                // 700/800, every lead <= 37 does the reverse -- so this is a deliberate trade
                // of the lowest point for the three above it, not a free win. See
                // PredictChargImminent for the failed attempt to split them on hover length.
                lead = StrongPreJumpLead > 0 ? StrongPreJumpLead : PreJumpTicks;
                if (StrongPreJumpLead <= 0) lead = StrongPreJumpDefault;
            }
            // PROPORTIONAL LEAD (round 156), MEASUREMENT ONLY AND DEFAULT OFF.
            //
            // The lead is BISTABLE. Everything that puts it at 38 or above -- the constants
            // 38/40/44/48/56 and every ratio from 1200 up -- produces the SAME run: it kills
            // strong 700 (7221/3) and strong 800 (6382/5) and always loses strong 300 (9697
            // death). Everything at 37 or below does the reverse: strong 300 is killed with 5
            // hits at 15915, and strong 700 is lost. No constant serves both.
            //
            // The learned hover duration (limit) is the obvious structural discriminator,
            // because the two fights are on opposite sides of a cliff that the lead alone
            // cannot straddle. IT WAS TRIED AND IT DOES NOT SEPARATE THEM. Gating on
            // `limit > floor` for floor 30/40/50/60/80 at ratio 1400 gives, verbatim:
            //   strong 300   f<=30 loses, f>=40 reverts to the reviewed 16103/8/0 KILL
            //   strong 700   5248/6/23053 at f=30, then 6205/7/11888 for EVERY f from 40 up
            // i.e. the floor either reverts strong 700 along with strong 300 (harmless but
            // useless) or loses strong 700 anyway. The two states on the countdown are not
            // distinguishable by their hover length in the runs that matter.
            //
            // So the proportional form is left in the tree as a documented negative result and
            // reverts to the reviewed constant unless a ratio is asked for explicitly.
            var ratio = PreJumpRatio;
            if (ratio > 0 && limit > DefaultPreJumpRatioFloor)
                lead = limit * ratio / 1000;
            return limit - timer <= lead;
        }

        /// <summary>Weak-wing pre-charge jump lead in ticks. Default 30, derived
        /// from the measured climb ratio (20 * 7.50 / 5.08 = 29.5). Set
        /// <c>CHAITE_WEAK_PREJUMP</c> to sweep it; 20 reproduces the old shared
        /// behaviour exactly.</summary>
        private static int WeakPreJumpLead
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(WeakPreJumpVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 || value > 120)
                    return WeakPreJumpTicks;
                return value;
            }
        }

        private const string WeakPreJumpVariable = "CHAITE_WEAK_PREJUMP";

        /// <summary>Strong-wing pre-charge jump lead in ticks. Unset or 0 means the
        /// reviewed constant <see cref="PreJumpTicks"/> (20). Measurement only.
        ///
        /// WHY THIS IS WORTH A KNOB (round 156). The precharge-jump is the only part of the
        /// circuit that can put the player off the charge line BEFORE the line exists, and
        /// section 150 measured that it is far too weak to matter: in the t=3083 body hit the
        /// window opens at t=3047 and the lock lands at t=3066, so the player gets 19 ticks
        /// of climb and reaches -3.06 px/tick horizontally while the charge travels at 14.12.
        /// At the lock the player is still only 208 px above a Boss whose own charge climbs
        /// at 9.46 px/tick. A longer wind-up is the one lever section 150 did not test.</summary>
        private static int StrongPreJumpLead
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(StrongPreJumpVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value <= 0 || value > 120)
                    return 0;
                return value;
            }
        }

        private const string StrongPreJumpVariable = "CHAITE_STRONG_PREJUMP";

        /// <summary>Reviewed strong-wing pre-charge jump lead. See
        /// <see cref="PredictChargImminent"/> for the full band that chose it and for the
        /// strong-300 cost it knowingly pays.</summary>
        private const int StrongPreJumpDefault = 40;

        /// <summary>Pre-charge jump lead as a per-mille fraction of the hover duration the
        /// circuit has learned for the state being counted down. 0 (default) keeps the
        /// constant leads above. Measurement only. See <see cref="PredictChargImminent"/>.
        ///
        /// The point of a RATIO rather than a count: the strong wing needs a lead of 38+ to
        /// survive its 700/800 points and 37 or less to survive its 300 point, and no constant
        /// can be both. The two differ in how long the Boss hovers before it commits, so the
        /// wind-up that works has to be a fraction of the hover, not a fixed number of ticks.
        /// 1200 is a 20% longer wind-up than the hover it precedes.</summary>
        private static int PreJumpRatio
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(PreJumpRatioVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 || value > 2000)
                    return 0;
                return value;
            }
        }

        private const string PreJumpRatioVariable = "CHAITE_PREJUMP_RATIO";

        /// <summary>Hover duration at or below which the pre-charge jump keeps the reviewed
        /// constant lead instead of the proportional one. 0 (default) disables the gate.
        /// Measurement only; see <see cref="PredictChargImminent"/>.
        ///
        /// This is the structural discriminator the constant sweep could not supply. Strong 300
        /// and strong 700 are on opposite sides of a bistable cliff in the lead, so the only way
        /// to serve both is to give them different leads -- and the honest basis for doing that
        /// is the one thing they genuinely differ in, which is how long the Boss hovers before
        /// it commits.</summary>
        private static int PreJumpRatioFloor
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable(PreJumpRatioFloorVariable);
                int value;
                if (string.IsNullOrEmpty(raw) ||
                    !int.TryParse(raw.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value) || value < 0 || value > 400)
                    return 0;
                return value;
            }
        }

        private const string PreJumpRatioFloorVariable = "CHAITE_PREJUMP_RATIO_FLOOR";

        /// <summary>Hover length above which the proportional lead applies. Fixed rather than
        /// configurable because the sweep is complete and negative: see
        /// <see cref="PredictChargImminent"/>. 60 is one full reviewed hover, so only genuinely
        /// longer hovers are affected.</summary>
        private const int DefaultPreJumpRatioFloor = 60;

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
            //
            // The corner case is what a wall must never do: if the escape is
            // pressed against an edge AND the boss is charging down the other
            // axis, then simply reversing the horizontal input is not enough,
            // because the reversed value is recomputed away on the next tick and
            // the player oscillates on the wall. A grounded player there has no
            // mobility at all, so the wall decides the fight.
            //
            // MEASURED (dense native trace, hit at tick 3019): the player held
            // plX 640 with plvx 0.0 for sixteen consecutive ticks -- _bandLeft is
            // worldLeft + BandEdgeMargin = 640 -- while the boss descended
            // vertically at bovy 15.8 and the player climbed at plvy -6.2 into
            // it. The horizontal axis was the only escape and the wall had
            // cancelled it.
            //
            // The fix is to spend the wall on the tangential axis: when a wall
            // cancels the horizontal escape, climb or dive along the wall rather
            // than standing on it, so the boss's line is left vertically even
            // though it cannot be left horizontally.
            // Reversal engages exactly at the edge.
            //
            // An earlier revision reversed 120 px early (WallApproachMargin),
            // on the theory that reaching the edge at zero speed is what loses
            // the fight, and that the player needs to already be travelling
            // inward when a charge arrives. That theory was wrong: it assumed
            // the boss attacked from the far side of the arena, but the probe
            // spawns the boss at player.Center.X + 640, i.e. the SAME side as
            // the player (tools/GameProbe.cs:3255), and a charge travels a fixed
            // 476 px, so it cannot reach across a 399-tile arena at all. The
            // measured player position range over a fight is 640..6723, so the
            // player is not trapped at the edge either. Reversing early was
            // therefore covering a case that does not arise, and it measured no
            // differently from reversing at the edge. Do not reinstate it
            // without a native trace that shows the player pinned at the edge
            // while a charge arrives.
            var atLeftWall = x <= _bandLeft + PinnedWallMargin;
            var atRightWall = x >= _bandRight - PinnedWallMargin;
            if (atLeftWall && horizontal <= 0) horizontal = 1;
            else if (atRightWall && horizontal >= 0) horizontal = -1;
            if (y - player.Height * 0.5f <= _ceilingY) vertical = 1;
            else if (y >= _floorY - FloorMargin && vertical > 0) vertical = 0;
        }

        /// <summary>Decomposes a separation vector into native input.
        ///
        /// Both axes are engaged whenever they carry any component at all. The
        /// earlier form left an axis neutral when it fell below
        /// <see cref="DominantAxisFraction"/> of the escape vector, on the
        /// theory that only the dominant axis was worth spending. Native
        /// measurement refutes that for this fight: a charge locks its velocity
        /// once, at state entry, and then travels its fixed distance in a
        /// straight line, so what decides contact is whether the player has
        /// moved off that line by the time it arrives -- not how elegant the
        /// escape vector looked. Zeroing the small axis threw away the only
        /// component that arrives at full speed on the first tick, and left the
        /// player coasting with no reason to be anywhere.
        ///
        /// MEASURED (dense native trace, tick 4182 hit): for the 24 ticks
        /// before a body hit the circuit held the player at |vx| 5.1 falling to
        /// 0.2 with wingTime 0, i.e. standing on the ground, and the horizontal
        /// gap at contact was about 25 px against a 75 px boss half-width. The
        /// circuit did not dodge into the boss; it stopped inside the boss's
        /// footprint and waited. Entry distances of the ten body hits (197 to
        /// 1020 px) overlap the clean charges at 197 and 233 px, so distance
        /// does not separate hits from misses and speed does.
        ///
        /// A degenerate vector still climbs, because a separation of zero has no
        /// direction to run in and altitude is the only escape left.</summary>
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
            if (nx != 0f) horizontal = nx > 0f ? 1 : -1;
            if (ny != 0f) vertical = ny > 0f ? 1 : -1;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

