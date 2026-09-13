using System;

namespace Chaite.Core
{
    /// <summary>
    /// Shared fail-closed primitives for the five secondary vanilla Boss
    /// controllers reviewed against the pinned 1.4.5.8 x86 executable.  This
    /// deliberately accepts only single-player Classic on an ordinary seed:
    /// random or difficulty-specific native branches are not silently treated
    /// as their Classic counterparts.
    /// </summary>
    internal static class ClassicSecondaryBossContract
    {
        public static bool TryValidateFixture(CombatSnapshot snapshot,
            out string reason)
        {
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Difficulty == null)
                return Invalid("missing combat snapshot", out reason);
            var difficulty = snapshot.Difficulty;
            if (!snapshot.NativeContextKnown || snapshot.NetMode != 0 ||
                snapshot.LocalPlayerIndex < 0 ||
                snapshot.LocalPlayerIndex >= 255)
                return Invalid("requires an observed single-player native frame",
                    out reason);
            if (!difficulty.GameModeKnown || difficulty.GameMode != 0 ||
                difficulty.Journey || difficulty.Expert || difficulty.Master)
                return Invalid("only native Classic difficulty is reviewed",
                    out reason);
            if (difficulty.Drunk || difficulty.NotTheBees ||
                difficulty.ForTheWorthy || difficulty.Remix ||
                difficulty.Zenith || difficulty.Celebration ||
                difficulty.Constant || difficulty.NoTraps ||
                difficulty.Skyblock)
                return Invalid("secret-seed AI is not part of the Classic contract",
                    out reason);
            if (snapshot.Player.Dead)
                return Invalid("the targeted player is dead", out reason);
            reason = null;
            return true;
        }

        /// <summary>
        /// Plantera and Golem have separately reviewed ordinary-world branches
        /// for all three fixed difficulties. Keep this entry point separate
        /// from TryValidateFixture so the other secondary controllers remain
        /// on their narrower Classic-only contract.
        /// </summary>
        public static bool TryValidateOrdinaryDifficultyFixture(
            CombatSnapshot snapshot, out string reason)
        {
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Difficulty == null)
                return Invalid("missing combat snapshot", out reason);
            var difficulty = snapshot.Difficulty;
            if (!snapshot.NativeContextKnown || snapshot.NetMode != 0 ||
                snapshot.LocalPlayerIndex < 0 ||
                snapshot.LocalPlayerIndex >= 255)
                return Invalid("requires an observed single-player native frame",
                    out reason);

            // Main.expertMode remains true in Master. Requiring the exact
            // GameMode/flag tuple prevents a synthetic default or contradictory
            // adapter frame from entering a difficulty-specific native branch.
            var ordinaryMode = difficulty.GameModeKnown &&
                (difficulty.GameMode == 0 && !difficulty.Expert &&
                    !difficulty.Master ||
                 difficulty.GameMode == 1 && difficulty.Expert &&
                    !difficulty.Master ||
                 difficulty.GameMode == 2 && difficulty.Expert &&
                    difficulty.Master);
            if (!ordinaryMode || difficulty.Journey)
                return Invalid("requires a consistent Classic, Expert, or Master native game mode",
                    out reason);
            if (difficulty.Drunk || difficulty.NotTheBees ||
                difficulty.ForTheWorthy || difficulty.Remix ||
                difficulty.Zenith || difficulty.Celebration ||
                difficulty.Constant || difficulty.NoTraps ||
                difficulty.Skyblock)
                return Invalid("secret-seed AI is outside the ordinary-world contract",
                    out reason);
            if (snapshot.Player.Dead)
                return Invalid("the targeted player is dead", out reason);
            reason = null;
            return true;
        }

        public static bool HasAllAi(TargetSnapshot target)
        {
            return target.Ai0Known && target.Ai1Known && target.Ai2Known &&
                target.Ai3Known && Finite(target.Ai0) && Finite(target.Ai1) &&
                Finite(target.Ai2) && Finite(target.Ai3);
        }

        public static bool HasAllLocalAi(TargetSnapshot target)
        {
            return target.LocalAi0Known && target.LocalAi1Known &&
                target.LocalAi2Known && target.LocalAi3Known &&
                Finite(target.LocalAi0) && Finite(target.LocalAi1) &&
                Finite(target.LocalAi2) && Finite(target.LocalAi3);
        }

        public static bool TargetsLocalPlayer(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            return target.NativeTargetKnown &&
                target.NativeTargetPlayerIndex == snapshot.LocalPlayerIndex;
        }

        public static bool IsExactInt(float value)
        {
            if (!Finite(value) || value < int.MinValue || value > int.MaxValue)
                return false;
            return Math.Abs(value - Math.Round(value)) <= .001f;
        }

        public static bool IsExactInt(float value, int minimum, int maximum)
        {
            return IsExactInt(value) && value >= minimum && value <= maximum;
        }

        public static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool Finite(Vec2 value)
        {
            return Finite(value.X) && Finite(value.Y);
        }

        public static int CountType(CombatSnapshot snapshot, int type)
        {
            var count = 0;
            for (var i = 0; i < snapshot.Targets.Count; i++)
                if (snapshot.Targets[i].Life > 0 &&
                    snapshot.Targets[i].Type == type) count++;
            return count;
        }

        public static bool TryFindByKey(CombatSnapshot snapshot, int key,
            out TargetSnapshot target)
        {
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                if (snapshot.Targets[i].Life <= 0 ||
                    snapshot.Targets[i].Key != key) continue;
                target = snapshot.Targets[i];
                return true;
            }
            target = default(TargetSnapshot);
            return false;
        }

        public static bool IsVisibleForNativeFinalRay(TargetSnapshot target)
        {
            return !target.LineOfSightKnown || target.HasLineOfSight;
        }

        public static bool IsWithinReviewedWeaponRange(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            if (snapshot.Weapon == null ||
                !snapshot.Weapon.NativeProfileRequired ||
                !snapshot.Weapon.Profile.IsSupported) return true;
            var range = snapshot.Weapon.Profile.ConservativeRangePixels;
            return range > 0f && Vec2.DistanceSquared(target.Center,
                snapshot.Player.Center) <= range * range;
        }

        public static int StableHorizontal(CombatSnapshot snapshot,
            TargetSnapshot anchor, BossMemory memory, float edgeMargin = 180f)
        {
            if (memory.PreviousTargetKey < 0 ||
                memory.OrbitDirection != -1 && memory.OrbitDirection != 1)
                memory.OrbitDirection = snapshot.Player.Center.X >=
                    anchor.Center.X ? 1 : -1;
            var direction = memory.OrbitDirection;
            var forward = direction > 0 ? snapshot.Arena.ClearanceRight :
                snapshot.Arena.ClearanceLeft;
            var reverse = direction > 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            if (forward < edgeMargin && reverse > forward + edgeMargin)
            {
                direction = -direction;
                memory.OrbitDirection = direction;
            }
            return direction;
        }

        /// <summary>
        /// Produces an explicit, immediately joinable orbit command.  World Y
        /// points down while VerticalIntent is expressed as Up in player-
        /// gravity coordinates, so the conversion is intentionally explicit.
        /// </summary>
        public static void OrbitIntent(CombatSnapshot snapshot,
            TargetSnapshot anchor, BossMemory memory, float innerRadius,
            float outerRadius, out int horizontal, out int vertical)
        {
            var relative = snapshot.Player.Center - anchor.Center;
            var distanceSquared = relative.X * relative.X +
                relative.Y * relative.Y;
            var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
            float desiredX;
            float desiredWorldY;
            if (distanceSquared < innerRadius * innerRadius)
            {
                desiredX = relative.X;
                desiredWorldY = relative.Y;
            }
            else if (distanceSquared > outerRadius * outerRadius)
            {
                desiredX = -relative.X;
                desiredWorldY = -relative.Y;
            }
            else
            {
                var orbit = memory.OrbitDirection == -1 ? -1 : 1;
                desiredX = orbit * relative.Y;
                desiredWorldY = -orbit * relative.X;
            }
            horizontal = Math.Abs(desiredX) < 8f ? 0 : Math.Sign(desiredX);
            vertical = Math.Abs(desiredWorldY) < 8f ? 0 :
                -Math.Sign(desiredWorldY) * gravitySign;
            if (horizontal > 0 && snapshot.Arena.ClearanceRight < 96f ||
                horizontal < 0 && snapshot.Arena.ClearanceLeft < 96f)
            {
                horizontal = -horizontal;
                if (horizontal != 0) memory.OrbitDirection = horizontal;
            }
            if (vertical > 0 && snapshot.Arena.ClearanceUp < 80f ||
                vertical < 0 && snapshot.Arena.ClearanceDown < 80f)
                vertical = -vertical;
        }

        private static bool Invalid(string value, out string reason)
        {
            reason = value;
            return false;
        }
    }
}
