using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chaite.Core
{
    /// <summary>
    /// Horizontal escape from the Fishron tornado PROJECTILES (384 Sharknado,
    /// 386 Cthulhunado). Gated behind <see cref="Variable"/> and inert by
    /// default, so the reviewed circuit is reproduced byte-for-byte unless the
    /// knob is set.
    ///
    /// WHY (round 168, native damage-source census over all 28 strong points).
    /// Reading `reason.declaredProjectileType` from the hurt observations shows
    /// that about HALF of every contact is projectile 386, with a few 384, and
    /// only about a third is the Boss body. The decompiled AI says why they are
    /// so dangerous: both are `aiStyle 64`, which spawns a copy of itself every
    /// tick while decrementing `ai[1]`, so fifteen generations coexist at scales
    /// from 0.4 to 1.5 and up to 50 are live in a single tick. Type 386's real
    /// box reaches 225x63, which is 2.6x the Boss body's 85x71, so with the
    /// player's 10 px half-width the horizontal contact threshold is 122 px
    /// against the body's 95.
    ///
    /// The geometry at the hit frames is a WALL, not a bullet. At tick 6055 the
    /// player has 7 tornadoes within 300x200 -- 3 left, 4 right, 3 above, 4
    /// below. At tick 6399 ten are all to the west. At tick 6380 the 25 live
    /// 386s span only 148 px in x but 909 px in y, i.e. they overlap into a
    /// continuous gap-free vertical wall, and the whole wall's `vx` is exactly
    /// zero: it does not travel sideways, it only drifts upward. The player's
    /// measured x oscillates 2586..2679 across consecutive hit frames, which is
    /// the signature of being shoved by it.
    ///
    /// Consequences, and why this layer moves HORIZONTALLY:
    /// <list type="bullet">
    /// <item>The wall cannot be run through at any one point, so a purely
    /// local "run away from the nearest tornado" rule is not enough.</item>
    /// <item>But the wall is only ~150 px thick, so its far side is safe, and
    /// the cheapest escape is to be on the side with the FEWEST tornadoes
    /// rather than to climb. Climbing is separately refuted against the Boss
    /// body (the hover follows the player) and the tornado column is 900 px
    /// tall anyway.</item>
    /// </list>
    ///
    /// This is NOT a retry of the refuted `CHAITE_CHARGE_NORMAL_OWNER`, which
    /// dodges the Boss BODY's charge normal. Every knob in FishronWingScript
    /// tunes the body's charge line; none of them reads a projectile at all,
    /// which is why thirteen or more vertical rewrites measured zero-sum.
    /// </summary>
    public static class TornadoEscape
    {
        public const string Variable = "CHAITE_TORNADO_ESCAPE";

        /// <summary>Diagnostic phase label the planner appends when this layer
        /// overrides the horizontal axis.</summary>
        public const string Phase = "fishron-wing-tornado-escape";

        /// <summary>Projectile ids of the two Fishron tornadoes: 384 Sharknado
        /// (timeLeft 540) and 386 Cthulhunado (timeLeft 840). Both are aiStyle
        /// 64 and share the 150x42 base box.</summary>
        private const int Sharknado = 384;
        private const int Cthulhunado = 386;

        /// <summary>Horizontal reach of a tornado box plus the player's own
        /// half-width. 225/2 + 10 rounds up to 123; a little margin is added
        /// because the box is integer-truncated by the engine.</summary>
        private const float HorizontalReach = 128f;

        /// <summary>Vertical reach of a tornado box plus the player's
        /// half-height: 63/2 + 21 = 52.5.</summary>
        private const float VerticalReach = 56f;

        /// <summary>How far ahead the escape looks. The plan is applied for one
        /// tick, so the horizon only needs to cover the time until the next
        /// decision, but a short lookahead stops the rule from flipping sides
        /// on a tornado that is already past.</summary>
        private const int Horizon = 10;

        /// <summary>Horizontal speed used to project the two candidate escapes.
        /// The measured cruise is 7-8 px/tick and the shield dash peaks at
        /// 14.5; the conservative cruise value is used so the choice does not
        /// depend on a dash being available.</summary>
        private const float Cruise = 7.5f;

        /// <summary>True when the escape layer is armed.</summary>
        public static bool Armed
        {
            get { return Environment.GetEnvironmentVariable(Variable) == "1"; }
        }

        /// <summary>Counts the 384/386 entries in a threat list.</summary>
        public static int CountTornadoes(IList<ThreatSnapshot> threats)
        {
            if (threats == null) return 0;
            var n = 0;
            for (var i = 0; i < threats.Count; i++)
                if (IsTornado(threats[i])) n++;
            return n;
        }

        /// <summary>
        /// Returns +1 or -1 when the player should move horizontally to leave
        /// the tornado wall, or 0 when no tornado is a threat within the
        /// horizon. `threats` may be null or empty; nothing is allocated.
        /// </summary>
        public static int HorizontalEscape(PlayerSnapshot player,
            IList<ThreatSnapshot> threats)
        {
            if (!Armed || threats == null || threats.Count == 0) return 0;

            var px = player.Center.X;
            var py = player.Center.Y;

            // Only a tornado that can still reach the player vertically is
            // relevant. Anything outside the band is ignored entirely, which is
            // what keeps this rule from firing during the whole fight.
            var relevant = 0;
            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                if (threat.Kind != ThreatKind.Projectile) continue;
                if (threat.Type != Sharknado && threat.Type != Cthulhunado) continue;
                var dy = Math.Abs(threat.Position.Y - py);
                if (dy > VerticalReach + Math.Abs(threat.Velocity.Y) * Horizon)
                    continue;
                relevant++;
            }
            if (relevant == 0) return 0;

            // The wall is only dangerous if it is actually about to overlap this
            // tick. If the player is already clear, leave the decision alone --
            // an escape that fires every frame is just a patrol flip.
            if (!Overlaps(player, px, py, threats)) return 0;

            var left = 0f;
            var right = 0f;
            for (var step = 1; step <= Horizon; step++)
            {
                var cxp = px - Cruise * step;
                var cxr = px + Cruise * step;
                left += Danger(threats, cxp, py, step);
                right += Danger(threats, cxr, py, step);
            }
            if (left == right) return 0;
            return left < right ? -1 : 1;
        }

        /// <summary>True when any tornado already overlaps the player's box.</summary>
        private static bool Overlaps(PlayerSnapshot player, float px, float py,
            IList<ThreatSnapshot> threats)
        {
            for (var i = 0; i < threats.Count; i++)
            {
                if (!IsTornado(threats[i])) continue;
                var threat = threats[i];
                if (Math.Abs(threat.Position.X - px) <
                        HalfWidth(threat) + player.Width * 0.5f &&
                    Math.Abs(threat.Position.Y - py) <
                        HalfHeight(threat) + player.Height * 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>Weighted count of tornadoes overlapping a predicted
        /// position, higher nearer in time.</summary>
        private static float Danger(IList<ThreatSnapshot> threats, float x,
            float y, int step)        {
            var weight = 1f / step;
            var total = 0f;
            for (var i = 0; i < threats.Count; i++)
            {
                if (!IsTornado(threats[i])) continue;
                var threat = threats[i];
                var tx = threat.Position.X + threat.Velocity.X * step;
                var ty = threat.Position.Y + threat.Velocity.Y * step;
                if (Math.Abs(tx - x) < HorizontalReach &&
                    Math.Abs(ty - y) < VerticalReach)
                    total += weight;
            }
            return total;
        }

        private static bool IsTornado(ThreatSnapshot threat) =>
            threat.Kind == ThreatKind.Projectile &&
            (threat.Type == Sharknado || threat.Type == Cthulhunado);

        // The engine truncates the box as it scales; use the live value when the
        // snapshot carries one and fall back to the base box otherwise.
        private static float HalfWidth(ThreatSnapshot threat) =>
            threat.Width > 0f ? threat.Width * 0.5f : 75f;

        private static float HalfHeight(ThreatSnapshot threat) =>
            threat.Height > 0f ? threat.Height * 0.5f : 21f;
    }
}
