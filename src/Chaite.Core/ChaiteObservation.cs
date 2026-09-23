using System;
using System.Globalization;

namespace Chaite.Core
{
    /// <summary>
    /// One hostile projectile of the observation's slot window, as the native
    /// probe writes it into a bridge row's <c>pr</c> entry. Positions are the
    /// projectile's top-left corner (<c>Projectile.position</c>), not its
    /// centre: the trainer subtracts the player's top-left position from this
    /// value, so a caller that passes centres would shift every projectile
    /// feature by half of both boxes.
    /// </summary>
    public struct ChaiteProjectileObservation
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float Width;
        public float Height;
        public float Type;
    }

    /// <summary>
    /// The fields <c>training/chaite_env.py::obs_vector</c> reads from a bridge
    /// observation row, as plain values.
    ///
    /// The probe emits a JSON object per native tick and the trainer converts it
    /// with <c>obs_vector</c>. The plugin has no JSON row to convert: it reads
    /// the same quantities out of the live game. This structure is the meeting
    /// point -- it names exactly the probe's keys, so the mapping from the game
    /// to the feature vector is written down once, in
    /// <see cref="ChaiteObservation"/>.
    ///
    /// Every field here is a value the probe always writes, so a missing value
    /// is a programming error rather than a state to represent. The two places
    /// the trainer itself tolerates a missing or zero value -- <c>plm</c> and
    /// <c>blm</c>, which fall back to 1.0 so a zero maximum cannot divide by
    /// zero -- are reproduced inside the builder, not here.
    /// </summary>
    public sealed class ChaiteObservationRow
    {
        /// <summary>Player top-left position (<c>px</c>, <c>py</c>).</summary>
        public float PlayerX;
        public float PlayerY;
        /// <summary>Player velocity (<c>vx</c>, <c>vy</c>).</summary>
        public float PlayerVelocityX;
        public float PlayerVelocityY;
        /// <summary>Player life and its maximum (<c>pl</c>, <c>plm</c>).</summary>
        public float PlayerLife;
        public float PlayerLifeMax;
        /// <summary>Player wing time and its maximum (<c>wt</c>, <c>wm</c>).</summary>
        public float WingTime;
        public float WingTimeMax;
        /// <summary>Player dash cooldown and eye-of-cthulhu dash (<c>dd</c>, <c>eo</c>).</summary>
        public float DashDelay;
        public float EocDash;
        /// <summary>Player jump counter (<c>jt</c>).</summary>
        public float JumpTicks;
        /// <summary>Player death flag (<c>dead</c>).</summary>
        public bool Dead;

        /// <summary>Boss top-left position (<c>bx</c>, <c>by</c>).</summary>
        public float BossX;
        public float BossY;
        /// <summary>Boss velocity (<c>bvx</c>, <c>bvy</c>).</summary>
        public float BossVelocityX;
        public float BossVelocityY;
        /// <summary>Boss life and its maximum (<c>bl</c>, <c>blm</c>).</summary>
        public float BossLife;
        public float BossLifeMax;
        /// <summary>Boss native ai[0] and ai[1] (<c>bs</c>, <c>bi</c>). The probe
        /// writes -1 for <c>bs</c> when no expected Boss root is alive.</summary>
        public float BossAi0;
        public float BossAi1;

        /// <summary>Ticks elapsed in the episode (<c>et</c>).</summary>
        public float ElapsedTicks;
        /// <summary>Hits taken this episode (<c>hits</c>).</summary>
        public float Hits;

        /// <summary>The slot window (<c>pr</c>), nearest hostile projectile
        /// first by Manhattan distance from the player's centre. May be null or
        /// shorter than the window; the builder pads the remainder with zeroes
        /// exactly as the trainer does.</summary>
        public ChaiteProjectileObservation[] Projectiles;

        /// <summary>True hostile projectile count over the whole array
        /// (<c>pc</c>), read only when the aggregates are enabled.</summary>
        public float ProjectileCount;
        /// <summary>Cumulative hostile counts within 200/400/800 px
        /// (<c>p2</c>, <c>p4</c>, <c>p8</c>), read only when the aggregates are
        /// enabled.</summary>
        public float ProjectilesWithin200;
        public float ProjectilesWithin400;
        public float ProjectilesWithin800;

        /// <summary>NPC-class threat counts within 200/400/800 px
        /// (<c>nt2</c>, <c>nt4</c>, <c>nt8</c>): the Fishron bubbles and the
        /// Sharknado column are NPCs, so they are counted here rather than in
        /// the projectile window.</summary>
        public float NpcThreatsWithin200;
        public float NpcThreatsWithin400;
        public float NpcThreatsWithin800;

        /// <summary>The nearest NPC-class threat, relative to the player's
        /// centre (<c>nrx</c>, <c>nry</c>) with its velocity (<c>nrvx</c>,
        /// <c>nrvy</c>), native type id (<c>nrt</c>), life (<c>nrl</c>) and box
        /// (<c>nrw</c>, <c>nrh</c>).
        ///
        /// The probe writes zeroes for the geometry and <c>-1</c> for the type
        /// when no such threat is alive, and the trainer divides the type by 500
        /// without special-casing it, so "nothing there" is a small negative
        /// feature rather than a flag of its own. A caller that leaves
        /// <see cref="NpcThreatType"/> at its default of zero therefore reports
        /// NPC 0, which is a real type: it has to set -1 explicitly, exactly as
        /// it does for <see cref="BossAi0"/>.</summary>
        public float NpcThreatRelativeX;
        public float NpcThreatRelativeY;
        public float NpcThreatVelocityX;
        public float NpcThreatVelocityY;
        public float NpcThreatType;
        public float NpcThreatLife;
        public float NpcThreatWidth;
        public float NpcThreatHeight;

        /// <summary>The Boss's native attack timer (<c>bs2</c>). For Duke
        /// Fishron this is <c>ai[2]</c>, the tick counter inside the current
        /// attack state. Read together with
        /// <see cref="BossAi0"/> it says how far through the attack the Boss is.
        /// The probe writes <c>-1</c> when no Boss is alive, so a caller that
        /// leaves it at the default of zero reports "attack just started".</summary>
        public float BossAi2;

        /// <summary>The Boss's native attack sequence (<c>bs3</c>). For Duke
        /// Fishron this is <c>ai[3]</c>, the field that selects which attack
        /// comes next. The probe writes
        /// <c>-1</c> when no Boss is alive.</summary>
        public float BossAi3;

        /// <summary>The multi-jump charges, one flag per balloon type, in the
        /// order of <see cref="ChaiteObservation.JumpChargeNames"/>. Read from
        /// the probe's <c>jc0</c>..<c>jc8</c>.</summary>
        public float[] JumpCharges;
    }

    /// <summary>
    /// Builds the policy's observation vector from live game state, matching
    /// <c>training/chaite_env.py::obs_vector</c> feature for feature, in order,
    /// with the same scaling and the same clipping.
    ///
    /// This is the missing half of the exported-policy path. A trained MLP is
    /// only as good as the vector it is handed: a silently different feature
    /// order, a different divisor, or the wrong clip bound makes every shipped
    /// policy act on noise, and nothing about the resulting fight would say so.
    /// The trainer's own file is therefore the single source of truth and this
    /// type transcribes it. <c>ChaiteObservationConformanceTests</c> pins the
    /// transcription against real recorded rows rather than against a
    /// restatement of this code.
    ///
    /// Two clipping rules are deliberately distinct and must not be collapsed:
    ///
    /// <list type="bullet">
    /// <item><c>_clip(value)</c> uses a fixed 4.0 for velocities, clocks,
    /// life fractions, projectile geometry and NPC-threat geometry. The
    /// NPC-threat block is scaled in world units but is <c>_clip</c>ed rather
    /// than <c>_clip_world</c>ed, because that is what the trainer does: the
    /// relative position is already centred on the player, so it is a
    /// displacement of a few hundred pixels rather than an arena coordinate,
    /// and 4.0 covers 4000 px of it.</item>
    /// <item><c>_clip_world(value)</c> uses <c>OBS_WORLD_BOUND</c> for scaled
    /// world coordinates, because the arena is roughly 12k px wide and the
    /// original 4.0 saturated the player's own altitude on 99.9% of measured
    /// ticks. The trainer defaults it to 4.0 and a session may raise it to 12
    /// with <c>CHAITE_OBS_WORLD_BOUND</c>.</item>
    /// </list>
    ///
    /// A builder is constructed for one window/aggregate configuration, because
    /// both change <see cref="ObservationCount"/>, and a policy's first layer
    /// only reads the width it was trained with. The configuration has to be
    /// the session's own: <c>CHAITE_PROJ_SLOTS</c>, <c>CHAITE_OBS_AGG</c> and
    /// <c>CHAITE_OBS_WORLD_BOUND</c> are read from the environment by
    /// <see cref="FromEnvironment"/>, and passed in explicitly by tests.
    ///
    /// The builder allocates nothing per call: one instance holds one output
    /// buffer and one projectile buffer, so the tick path can call
    /// <see cref="Fill"/> every frame.
    /// </summary>
    public sealed class ChaiteObservation
    {
        /// <summary>Default slot window, matching the trainer's own default.
        /// The probe writes this many <c>pr</c> entries and the trainer sizes
        /// its projectile block from the same number.</summary>
        public const int DefaultProjectileSlots = 12;

        /// <summary>Upper bound on the slot window. Not a tuning knob: it keeps
        /// a corrupt environment variable from asking for an arbitrary
        /// allocation on every tick.</summary>
        public const int MaximumProjectileSlots = 4096;

        /// <summary>Fixed <c>_clip</c> bound, in the trainer's own units.</summary>
        public const float DefaultClipBound = 4f;

        /// <summary>Default world-coordinate bound (<c>_clip_world</c>). The
        /// trainer's default, kept as the default here so an environment that
        /// has never heard of the variable reproduces the same vector.</summary>
        public const float DefaultWorldBound = 4f;

        /// <summary>Features before the projectile window: the player, the
        /// boss, and the boss-relative geometry.</summary>
        public const int BaseFeatureCount = 22;

        /// <summary>Features after the projectile window: the two fractions,
        /// the ready flag and the hit flag.</summary>
        public const int TailFeatureCount = 4;

        /// <summary>Features for NPC-class threats: the counts within
        /// 200/400/800 px and the nearest such threat's relative position,
        /// velocity, distance, type, life and extent.
        ///
        /// They exist because Fishron's Detonating Bubbles (NPC 371), the
        /// Sharknado-generating bubbles (372/373) and the Sharknado column (384)
        /// are NPCs, not projectiles, so the slot window above never contained
        /// them and the policy was blind to Fishron's primary threat. They are
        /// appended after the tail features and before the optional aggregates,
        /// which keeps the 98 values the earlier checkpoints were trained on as
        /// an exact prefix of the wider vector.</summary>
        public const int NpcThreatFeatureCount = 12;

        /// <summary>Features appended when the aggregates are enabled.</summary>
        public const int AggregateFeatureCount = 4;

        /// <summary>Features for the Boss's own attack clock: the native timer
        /// (<c>bs2</c>) and the native sequence (<c>bs3</c>).
        ///
        /// These are not a refinement of <see cref="ChaiteObservationRow.BossAi0"/>
        /// and <see cref="ChaiteObservationRow.BossAi1"/>; they are the two
        /// fields the reviewed formula scripts branch on. For Duke Fishron
        /// (NPC 370) <c>FormulaScriptController.TryReadInput</c> reads
        /// <c>ai[0]</c> as the state, <b><c>ai[2]</c> as the timer</b> and
        /// <b><c>ai[3]</c> as the sequence</b>, and
        /// <c>FishronFormulaStateContract.TimerLimit</c> turns (state, sequence,
        /// difficulty, enraged) into the exact tick on which the state ends.
        /// The sequence is what selects the next attack: state 2 emits 20
        /// Detonating Bubbles, state 3 drops Sharknado projectiles, state 7
        /// circles, state 8 drops the Cthulhunado.
        ///
        /// A policy that cannot read the timer and the sequence can only guess
        /// how far into an attack the Boss is, which is exactly the information a
        /// dodge needs: the bubbles are spawned on a 4-tick cadence and the
        /// Sharknado drop is announced by the state change alone. Appended after
        /// the NPC-threat block so the earlier 110 values keep their indices.</summary>
        public const int BossClockFeatureCount = 2;

        /// <summary>Features for the multi-jump charges: one per balloon type,
        /// in the assembly's own field order.
        ///
        /// The audit flagged this as the last unobservable mobility resource.
        /// The Lilith's Wolf loadout is the only one that carries the Bundle of
        /// Balloons (1164), and its whole mobility is the multi-jump, so a
        /// policy that cannot read how many charges are left cannot decide
        /// whether to spend one. <see cref="ChaiteObservationRow.JumpTicks"/>
        /// (<c>jt</c>) is the jump hold counter and does not carry the count:
        /// measured in <c>current-focus.md</c> C122, a held jump reaches
        /// <c>jt</c> 0-5 with <c>wingTime</c> untouched while a tapped one
        /// reaches 11-18 with <c>wingTime</c> spent, so the two are
        /// distinguishable by their consequences but the remaining charges are
        /// not readable at all.
        ///
        /// Nine features rather than one sum: measured on a recorded
        /// lilith-wolf stream (254,957 rows, balloons equipped throughout), the
        /// Bundle of Balloons (1164) sets the cloud, sandstorm and blizzard
        /// flags on every row, so a sum would report 3 and leave the policy
        /// unable to tell which jump it is about to spend. Appended after the
        /// Boss clock so every earlier value keeps its index.</summary>
        public const int JumpChargeFeatureCount = 9;

        /// <summary>The balloon types the jump-charge block reports, in the same
        /// order as the probe's <c>jc0</c>..<c>jc8</c> keys and the trainer's
        /// own list. Named here so a reordering cannot be silent.</summary>
        public static readonly string[] JumpChargeNames =
        {
            "cloud", "sandstorm", "blizzard", "fart", "sail", "basilisk",
            "santank", "unicorn", "wall_of_flesh_goat"
        };

        /// <summary>Features one projectile slot contributes.</summary>
        public const int ProjectileFeatureCount = 6;

        public const string SlotsVariable = "CHAITE_PROJ_SLOTS";
        public const string AggregatesVariable = "CHAITE_OBS_AGG";
        public const string WorldBoundVariable = "CHAITE_OBS_WORLD_BOUND";

        private readonly float[] _values;
        private readonly ChaiteProjectileObservation[] _projectiles;
        private readonly float _worldBound;
        // Scratch for SelectProjectiles, sized up on demand and reused, so the
        // tick path does not allocate a closure or a list per frame.
        private int[] _sortOrder;
        private float[] _sortDistances;

        public ChaiteObservation(int projectileSlots, bool aggregates,
            float worldBound)
        {
            if (projectileSlots < 1 || projectileSlots > MaximumProjectileSlots)
                throw new ArgumentOutOfRangeException("projectileSlots",
                    projectileSlots, "the projectile window must be in 1.." +
                    MaximumProjectileSlots);
            if (float.IsNaN(worldBound) || float.IsInfinity(worldBound) ||
                worldBound <= 0f)
                throw new ArgumentOutOfRangeException("worldBound", worldBound,
                    "the world clip bound must be a finite positive number");
            ProjectileSlots = projectileSlots;
            Aggregates = aggregates;
            _worldBound = worldBound;
            _values = new float[ObservationCount];
            _projectiles =
                new ChaiteProjectileObservation[projectileSlots];
        }

        /// <summary>Slots in the projectile window.</summary>
        public int ProjectileSlots { get; private set; }

        /// <summary>Whether the four threat-density features are appended.</summary>
        public bool Aggregates { get; private set; }

        /// <summary>The bound <c>_clip_world</c> uses.</summary>
        public float WorldBound { get { return _worldBound; } }

        /// <summary>
        /// Vector width, computed the way the trainer computes <c>OBS_DIM</c>:
        /// 10 + 12 base, the window, 4 tail, 12 NPC-threat features, the 4
        /// Boss-clock features, and the optional 4 aggregates.
        /// </summary>
        public int ObservationCount
        {
            get
            {
                return BaseFeatureCount + ProjectileSlots * ProjectileFeatureCount +
                    TailFeatureCount + NpcThreatFeatureCount +
                    BossClockFeatureCount + JumpChargeFeatureCount +
                    (Aggregates ? AggregateFeatureCount : 0);
            }
        }

        /// <summary>
        /// Refuses a checkpoint whose first layer is not the width this builder
        /// produces.
        ///
        /// A loaded policy already checks its own file for internal
        /// consistency; this is the other half, and the one the environment can
        /// get wrong: a checkpoint trained with <c>CHAITE_PROJ_SLOTS=48</c>
        /// driven by a plugin still configured for 12. Handing a 110-wide vector
        /// to a first layer that reads 326 either reads out of bounds or --
        /// worse, in a runtime that pads -- silently scores whatever follows,
        /// so this fails loudly instead. It lives here rather than in the
        /// plugin because the two numbers it compares are both this type's, and
        /// because that makes the check reachable from the offline suite.
        /// </summary>
        public void RequireObservationCount(int expected, string source)
        {
            if (ObservationCount == expected) return;
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "{0} reads {1} observation values but the configured builder" +
                " produces {2} (slots {3}, aggregates {4}, world bound {5}):" +
                " set {6}/{7}/{8} to match the training session",
                string.IsNullOrEmpty(source) ? "the policy" : source, expected,
                ObservationCount, ProjectileSlots,
                Aggregates ? "on" : "off",
                _worldBound.ToString(CultureInfo.InvariantCulture),
                SlotsVariable, AggregatesVariable, WorldBoundVariable));
        }

        /// <summary>
        /// The configuration the session's environment names, so the plugin
        /// builds the vector the running checkpoint was trained on rather than
        /// the one this build happens to default to.
        ///
        /// The defaults are the trainer's own: 12 slots, no aggregates, world
        /// bound 4.0. <c>CHAITE_PROJ_SLOTS</c> is clamped to at least one, as
        /// the trainer clamps it; an unparsable or out-of-range value is
        /// refused rather than guessed, because guessing here silently feeds the
        /// policy a vector of the wrong width.
        /// </summary>
        public static ChaiteObservation FromEnvironment()
        {
            var slots = DefaultProjectileSlots;
            var rawSlots = Environment.GetEnvironmentVariable(SlotsVariable);
            if (!string.IsNullOrEmpty(rawSlots))
            {
                int parsed;
                if (!int.TryParse(rawSlots.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out parsed))
                    throw new InvalidOperationException(SlotsVariable +
                        " must be an integer, not '" + rawSlots + "'.");
                slots = parsed < 1 ? 1 : parsed;
            }
            var aggregates = false;
            var rawAggregates = Environment.GetEnvironmentVariable(
                AggregatesVariable);
            if (!string.IsNullOrEmpty(rawAggregates))
            {
                var value = rawAggregates.Trim().ToLowerInvariant();
                aggregates = value == "1" || value == "true" || value == "yes" ||
                    value == "on";
            }
            var worldBound = DefaultWorldBound;
            var rawBound = Environment.GetEnvironmentVariable(WorldBoundVariable);
            if (!string.IsNullOrEmpty(rawBound))
            {
                float parsed;
                if (!float.TryParse(rawBound.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out parsed) ||
                    float.IsNaN(parsed) || float.IsInfinity(parsed) ||
                    parsed <= 0f)
                    throw new InvalidOperationException(WorldBoundVariable +
                        " must be a finite positive number, not '" + rawBound +
                        "'.");
                worldBound = parsed;
            }
            return new ChaiteObservation(slots, aggregates, worldBound);
        }

        /// <summary>
        /// Fills <paramref name="destination"/> with the observation for
        /// <paramref name="row"/>. The destination may be longer than
        /// <see cref="ObservationCount"/>; the extra entries are left alone, so
        /// a caller with one large buffer can serve several configurations.
        /// </summary>
        public void Fill(ChaiteObservationRow row, float[] destination)
        {
            if (row == null) throw new ArgumentNullException("row");
            if (destination == null)
                throw new ArgumentNullException("destination");
            var count = ObservationCount;
            if (destination.Length < count)
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture,
                    "the observation buffer must hold at least {0} values but" +
                    " holds {1}", count, destination.Length), "destination");

            var px = row.PlayerX;
            var py = row.PlayerY;
            var playerLife = row.PlayerLife;
            var playerLifeMax = NonZeroOrOne(row.PlayerLifeMax);
            var wingTime = row.WingTime;
            var wingTimeMax = NonZeroOrOne(row.WingTimeMax);
            var bossLife = row.BossLife;
            var bossLifeMax = NonZeroOrOne(row.BossLifeMax);
            var bossX = row.BossX;
            var bossY = row.BossY;

            var at = 0;
            destination[at++] = ClipWorld(px / 1000f);
            destination[at++] = ClipWorld(py / 1000f);
            destination[at++] = Clip(row.PlayerVelocityX / 16f);
            destination[at++] = Clip(row.PlayerVelocityY / 16f);
            destination[at++] = Clip(wingTime / 60f);
            destination[at++] = Clip(row.DashDelay / 30f);
            destination[at++] = Clip(row.EocDash / 15f);
            destination[at++] = Clip(playerLife / playerLifeMax);
            destination[at++] = row.Dead ? 1f : 0f;
            destination[at++] = Clip(row.JumpTicks / 20f);
            destination[at++] = ClipWorld(bossX / 1000f);
            destination[at++] = ClipWorld(bossY / 1000f);
            destination[at++] = Clip(row.BossVelocityX / 16f);
            destination[at++] = Clip(row.BossVelocityY / 16f);
            var separationX = px - bossX;
            var separationY = py - bossY;
            destination[at++] = ClipWorld(separationX / 1000f);
            destination[at++] = ClipWorld(separationY / 1000f);
            destination[at++] = ClipWorld(
                (float)Math.Sqrt(separationX * separationX +
                    separationY * separationY) / 1500f);
            destination[at++] = Clip(bossLife / bossLifeMax);
            destination[at++] = Clip(row.BossAi0 / 15f);
            destination[at++] = Clip(row.BossAi1 / 8f);
            destination[at++] = bossLife > 0f ? 1f : 0f;
            destination[at++] = Clip(row.ElapsedTicks / 600f);

            var window = _projectiles;
            var supplied = row.Projectiles;
            var available = supplied == null ? 0 : supplied.Length;
            if (available > window.Length) available = window.Length;
            for (var index = 0; index < available; index++) window[index] = supplied[index];
            for (var index = 0; index < window.Length; index++)
            {
                if (index >= available)
                {
                    // An empty slot is six literal zeroes, not a zero-valued
                    // projectile. The difference is not cosmetic: a padding
                    // built by subtracting the player's position from a zero
                    // projectile would emit -px/1000 and -py/1000, i.e. a
                    // phantom threat sitting at the world origin, on every
                    // tick with fewer than PROJECTILE_SLOTS hostiles -- which
                    // is most of them.
                    destination[at++] = 0f;
                    destination[at++] = 0f;
                    destination[at++] = 0f;
                    destination[at++] = 0f;
                    destination[at++] = 0f;
                    destination[at++] = 0f;
                    continue;
                }
                var shot = window[index];
                destination[at++] = Clip((shot.X - px) / 1000f);
                destination[at++] = Clip((shot.Y - py) / 1000f);
                destination[at++] = Clip(shot.VelocityX / 16f);
                destination[at++] = Clip(shot.VelocityY / 16f);
                destination[at++] = Clip(Math.Max(shot.Width, shot.Height) / 64f);
                destination[at++] = Clip(shot.Type / 1000f);
            }

            destination[at++] = Clip(playerLife / playerLifeMax);
            destination[at++] = Clip(wingTime / wingTimeMax);
            destination[at++] = row.DashDelay == 0f && row.EocDash == 0f ? 1f : 0f;
            destination[at++] = row.Hits != 0f ? 1f : 0f;

            // NPC-class threats: Fishron's Detonating Bubbles (NPC 371), the
            // Sharknado-generating bubbles (372/373) and the Sharknado column
            // (384) are NPCs, not projectiles, so the slot window above never
            // contained them and the policy was blind to Fishron's primary
            // threat. The counts come from the probe's nt2/nt4/nt8 and the
            // nearest threat supplies the geometry. These sit after the tail and
            // before the aggregates, so the 98 values an earlier checkpoint was
            // trained on stay at the same indices.
            var npcX = row.NpcThreatRelativeX;
            var npcY = row.NpcThreatRelativeY;
            destination[at++] = Clip(row.NpcThreatsWithin200 / 8f);
            destination[at++] = Clip(row.NpcThreatsWithin400 / 8f);
            destination[at++] = Clip(row.NpcThreatsWithin800 / 16f);
            destination[at++] = Clip(npcX / 1000f);
            destination[at++] = Clip(npcY / 1000f);
            destination[at++] = Clip(row.NpcThreatVelocityX / 16f);
            destination[at++] = Clip(row.NpcThreatVelocityY / 16f);
            destination[at++] = Clip((float)Math.Sqrt(
                npcX * npcX + npcY * npcY) / 1500f);
            destination[at++] = Clip(row.NpcThreatType / 500f);
            destination[at++] = Clip(row.NpcThreatLife / 1000f);
            destination[at++] = Clip(Math.Max(row.NpcThreatWidth,
                row.NpcThreatHeight) / 64f);
            // The trainer tests the truncated count, not the value: int(0.5) is
            // zero, so a fractional nt4 is "no threat" there and has to be here
            // too. The probe writes an integer, so the cast only matters to a
            // caller that does not.
            destination[at++] = (int)row.NpcThreatsWithin400 > 0 ? 1f : 0f;

            // The Boss's own attack clock. See BossClockFeatureCount: these are
            // the fields the reviewed formula scripts branch on, and they are
            // what says how far into the current attack the Boss is and which
            // attack the next state change will start.
            //
            // Scaled as (value + 1) / divisor, not value / divisor. The probe
            // writes -1 for "no Boss alive", so dividing the raw value maps that
            // sentinel onto 0, which is also the value a Boss whose timer is
            // genuinely zero produces: the policy could not tell "no Boss" from
            // "the attack just started", and those call for opposite behaviour.
            // Shifting by one puts the sentinel at 0 and every real value in
            // 1/divisor..1, and _clip is a no-op on all of it.
            //
            // The two divisors differ because the ranges differ: Fishron's timer
            // runs to 180 ticks inside one state and the sequence only reaches
            // 11. Both are _clip()ed rather than _clip_world()ed, since neither
            // is a world coordinate.
            destination[at++] = Clip((row.BossAi2 + 1f) / 181f);
            destination[at++] = Clip((row.BossAi3 + 1f) / 16f);

            // The multi-jump charges. A boolean per type, so the trainer's own
            // divisor is 4 (a flag has to stay well inside the clip and the
            // trainer uses the same 4.0 it uses for every other flag).
            for (var index = 0; index < JumpChargeFeatureCount; index++)
            {
                var charges = row.JumpCharges;
                destination[at++] = charges != null && index < charges.Length &&
                    charges[index] != 0f ? .25f : 0f;
            }

            if (Aggregates)
            {
                // Threat density over the full hostile set, independent of the
                // slot cap: the window truncates, so a policy reading only
                // slots goes blind exactly when the screen is fullest.
                destination[at++] = Clip(row.ProjectileCount / 64f);
                destination[at++] = Clip(row.ProjectilesWithin200 / 8f);
                destination[at++] = Clip(row.ProjectilesWithin400 / 16f);
                destination[at++] = Clip(row.ProjectilesWithin800 / 32f);
            }
        }

        /// <summary>
        /// Selects the projectile window from a hostile list, the way the
        /// native probe selects it before writing a bridge row.
        ///
        /// The key is the Manhattan distance between the player's centre and
        /// the projectile's centre, and it is not a detail: the recorded
        /// streams confirm it. Ordering the fixture's own recorded windows by
        /// top-left-to-top-left Manhattan distance leaves 48 of 200 rows out of
        /// order; ordering them by centre-to-centre distance leaves none. A
        /// builder that sorted differently would feed the policy a different
        /// window from the one it trained on whenever the screen is busy, which
        /// is exactly when the window matters.
        ///
        /// The window is a prefix of the sorted list: fewer candidates than
        /// slots is padded with zeroes by <see cref="Fill"/>, and more is
        /// truncated. The aggregates are counted over the whole list, never
        /// over the truncated window, because their purpose is to stay
        /// informative when the window is full.
        /// </summary>
        public void SelectProjectiles(
            System.Collections.Generic.IList<ChaiteProjectileObservation> candidates,
            ChaiteObservationRow row, float playerWidth, float playerHeight,
            ChaiteProjectileObservation[] window)
        {
            if (row == null) throw new ArgumentNullException("row");
            if (window == null) throw new ArgumentNullException("window");
            var count = candidates == null ? 0 : candidates.Count;
            row.ProjectileCount = count;
            var centerX = row.PlayerX + playerWidth * .5f;
            var centerY = row.PlayerY + playerHeight * .5f;
            var order = _sortOrder;
            if (order == null || order.Length < count)
            {
                order = new int[count];
                _sortOrder = order;
            }
            for (var index = 0; index < count; index++) order[index] = index;
            var distances = _sortDistances;
            if (distances == null || distances.Length < count)
            {
                distances = new float[count];
                _sortDistances = distances;
            }
            for (var index = 0; index < count; index++)
            {
                var shot = candidates[index];
                distances[index] = Math.Abs(shot.X + shot.Width * .5f -
                        centerX) +
                    Math.Abs(shot.Y + shot.Height * .5f - centerY);
            }
            // Insertion sort: the window is a dozen entries in the common case,
            // it is stable, and it allocates nothing. A comparison sort over a
            // delegate would allocate a closure per tick.
            for (var index = 1; index < count; index++)
            {
                var key = order[index];
                var distance = distances[key];
                var scan = index - 1;
                while (scan >= 0 && distances[order[scan]] > distance)
                {
                    order[scan + 1] = order[scan];
                    scan--;
                }
                order[scan + 1] = key;
            }
            var nearest200 = 0;
            var nearest400 = 0;
            var nearest800 = 0;
            for (var index = 0; index < count; index++)
            {
                var distance = distances[index];
                if (distance < 200f) nearest200++;
                if (distance < 400f) nearest400++;
                if (distance < 800f) nearest800++;
            }
            row.ProjectilesWithin200 = nearest200;
            row.ProjectilesWithin400 = nearest400;
            row.ProjectilesWithin800 = nearest800;
            var take = count < window.Length ? count : window.Length;
            for (var index = 0; index < take; index++)
                window[index] = candidates[order[index]];
            for (var index = take; index < window.Length; index++)
                window[index] = default(ChaiteProjectileObservation);
            row.Projectiles = window;
        }

        /// <summary>
        /// The observation for <paramref name="row"/> in a fresh buffer. The
        /// tick path uses <see cref="Fill"/> instead, so that this allocation
        /// exists only for one-shot callers and tests.
        /// </summary>
        public float[] ObsVector(ChaiteObservationRow row)
        {
            var values = new float[ObservationCount];
            Fill(row, values);
            return values;
        }

        /// <summary>Reusable buffer for the projectile window, so
        /// <see cref="Fill"/> does not allocate.</summary>
        public ChaiteProjectileObservation[] ProjectileBuffer
        {
            get { return _projectiles; }
        }

        /// <summary>
        /// The trainer's <c>_clip</c>: a fixed 4.0, used for everything that is
        /// not a scaled world coordinate.
        /// </summary>
        public float Clip(float value)
        {
            return Clip(value, DefaultClipBound);
        }

        /// <summary>
        /// The trainer's <c>_clip_world</c>: the arena-sized bound, used for
        /// world coordinates scaled by 1000 (and the 1500-scaled separation).
        /// </summary>
        public float ClipWorld(float value)
        {
            return Clip(value, _worldBound);
        }

        private static float Clip(float value, float bound)
        {
            if (value > bound) return bound;
            if (value < -bound) return -bound;
            return value;
        }

        /// <summary>
        /// The trainer's <c>float(row.get(key, 0.0)) or 1.0</c>: a missing or
        /// zero maximum becomes one, so a division cannot produce a NaN.
        /// </summary>
        private static float NonZeroOrOne(float value)
        {
            return value == 0f ? 1f : value;
        }

        /// <summary>
        /// The names of the features, in order, for a log or a failure report.
        /// Kept beside <see cref="Fill"/> so a reader can check the two against
        /// each other, and used by the conformance test to name the first
        /// differing index instead of only numbering it.
        /// </summary>
        public string[] FeatureNames()
        {
            var names = new string[ObservationCount];
            var at = 0;
            names[at++] = "player_x";
            names[at++] = "player_y";
            names[at++] = "player_velocity_x";
            names[at++] = "player_velocity_y";
            names[at++] = "wing_time";
            names[at++] = "dash_delay";
            names[at++] = "eoc_dash";
            names[at++] = "life_fraction";
            names[at++] = "dead";
            names[at++] = "jump_ticks";
            names[at++] = "boss_x";
            names[at++] = "boss_y";
            names[at++] = "boss_velocity_x";
            names[at++] = "boss_velocity_y";
            names[at++] = "separation_x";
            names[at++] = "separation_y";
            names[at++] = "separation_distance";
            names[at++] = "boss_life_fraction";
            names[at++] = "boss_ai0";
            names[at++] = "boss_ai1";
            names[at++] = "boss_alive";
            names[at++] = "elapsed_ticks";
            for (var index = 0; index < ProjectileSlots; index++)
            {
                var slot = "projectile" + index.ToString(
                    CultureInfo.InvariantCulture) + "_";
                names[at++] = slot + "x";
                names[at++] = slot + "y";
                names[at++] = slot + "velocity_x";
                names[at++] = slot + "velocity_y";
                names[at++] = slot + "extent";
                names[at++] = slot + "type";
            }
            names[at++] = "life_fraction_tail";
            names[at++] = "wing_time_fraction";
            names[at++] = "dash_ready";
            names[at++] = "hit_this_episode";
            names[at++] = "npc_threats_within_200";
            names[at++] = "npc_threats_within_400";
            names[at++] = "npc_threats_within_800";
            names[at++] = "npc_threat_relative_x";
            names[at++] = "npc_threat_relative_y";
            names[at++] = "npc_threat_velocity_x";
            names[at++] = "npc_threat_velocity_y";
            names[at++] = "npc_threat_distance";
            names[at++] = "npc_threat_type";
            names[at++] = "npc_threat_life";
            names[at++] = "npc_threat_extent";
            names[at++] = "npc_threat_present";
            names[at++] = "boss_attack_timer";
            names[at++] = "boss_attack_sequence";
            for (var index = 0; index < JumpChargeFeatureCount; index++)
                names[at++] = "jump_charge_" + JumpChargeNames[index];
            if (Aggregates)
            {
                names[at++] = "threat_count";
                names[at++] = "threats_within_200";
                names[at++] = "threats_within_400";
                names[at++] = "threats_within_800";
            }
            return names;
        }
    }
}
