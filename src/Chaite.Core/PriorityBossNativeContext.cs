using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Independent, source-specific observations used by the deterministic
    /// controllers for the priority Bosses.  Each source owns its own Known
    /// bit: a legitimate native zero must never be used as an unavailable
    /// sentinel, and one missing source must not taint unrelated observations.
    /// </summary>
    public sealed class PriorityBossNativeContext
    {
        public bool WorldGeometryKnown;
        public double WorldSurfaceTiles;
        public int WorldWidthTiles;
        // Main.UnderworldLayer is an independent static property in the pinned
        // executable. Keep its availability separate so an older adapter can
        // still supply horizontal/surface geometry without impersonating the
        // complete Plantera vertical-enrage predicate.
        public bool UnderworldLayerKnown;
        public int UnderworldLayerTiles;
        public bool WallOfFleshDrawAreaKnown;
        public int WallOfFleshDrawAreaTopPixels;
        public int WallOfFleshDrawAreaBottomPixels;

        public readonly List<QueenBeeNativeEnrageObservation> QueenBees =
            new List<QueenBeeNativeEnrageObservation>(2);
        public readonly List<WallOfFleshTunnelObservation> WallOfFleshTunnels =
            new List<WallOfFleshTunnelObservation>(1);
        public readonly List<WallOfFleshEyeLaserObservation> WallOfFleshEyes =
            new List<WallOfFleshEyeLaserObservation>(2);
        public readonly List<DukeFishronNativeEnrageObservation> DukeFishrons =
            new List<DukeFishronNativeEnrageObservation>(2);
        public readonly List<DeerclopsNativeTimerObservation> Deerclopses =
            new List<DeerclopsNativeTimerObservation>(2);
        public readonly List<MoonLordProjectile454Observation> MoonLordProjectiles454 =
            new List<MoonLordProjectile454Observation>(32);
        public readonly List<MoonLordProjectile456Observation> MoonLordProjectiles456 =
            new List<MoonLordProjectile456Observation>(16);

        /// <summary>Clears the borrowed per-tick view without replacing list instances.</summary>
        public void Clear()
        {
            WorldGeometryKnown = false;
            WorldSurfaceTiles = 0d;
            WorldWidthTiles = 0;
            UnderworldLayerKnown = false;
            UnderworldLayerTiles = 0;
            WallOfFleshDrawAreaKnown = false;
            WallOfFleshDrawAreaTopPixels = 0;
            WallOfFleshDrawAreaBottomPixels = 0;
            QueenBees.Clear();
            WallOfFleshTunnels.Clear();
            WallOfFleshEyes.Clear();
            DukeFishrons.Clear();
            Deerclopses.Clear();
            MoonLordProjectiles454.Clear();
            MoonLordProjectiles456.Clear();
        }
    }

    /// <summary>Exact vanilla AI-style-43 inputs and its resulting enrage factor.</summary>
    public struct QueenBeeNativeEnrageObservation
    {
        public bool Known;
        public int NpcKey;
        public bool BossAboveWorldSurface;
        public bool TargetOutsideJungle;
        public bool GetGoodWorld;
        public float NativeEnrageFactor;

        public float ExpectedEnrageFactor =>
            (BossAboveWorldSurface ? 1f : 0f) +
            (TargetOutsideJungle ? 1f : 0f) +
            (GetGoodWorld ? .5f : 0f);

        public bool Enraged => Known && NativeEnrageFactor > 0f;
    }

    /// <summary>Main.wofDrawAreaTop/Bottom captured from the same game tick.</summary>
    public struct WallOfFleshTunnelObservation
    {
        public bool Known;
        public int NpcKey;
        public bool NativeDirectionKnown;
        public int NativeDirection;
        public int DrawAreaTopPixels;
        public int DrawAreaBottomPixels;

        public int HeightPixels => Known && DrawAreaBottomPixels >=
            DrawAreaTopPixels ? DrawAreaBottomPixels - DrawAreaTopPixels : 0;

        public bool ContainsCenterY(float centerY) => Known &&
            PriorityBossNativeContextContract.Finite(centerY) &&
            centerY >= DrawAreaTopPixels && centerY <= DrawAreaBottomPixels;
    }

    /// <summary>
    /// One Wall-of-Flesh eye's native AI-style-28 laser state.  Ai0 is the
    /// vanilla +1/-1 eye side, localAI[1] is the current timer, and localAI[2]
    /// is zero while charging or the nonzero burst-shot index. LOS is a
    /// separately fallible source.
    /// </summary>
    public struct WallOfFleshEyeLaserObservation
    {
        public bool Known;
        public int NpcKey;
        public float Ai0EyeSide;
        public float LocalAi1ChargeTimer;
        public float LocalAi2BurstStage;
        public bool LineOfSightKnown;
        public bool LineOfSight;

        public bool UpperEye => Known && Ai0EyeSide < 0f;
        public bool BurstActive => Known && LocalAi2BurstStage != 0f;
        public bool ChargeThresholdExceeded => Known && !BurstActive &&
            LocalAi1ChargeTimer > 600f;
        public bool BurstCadenceReady => BurstActive &&
            LocalAi1ChargeTimer > 45f;
        public bool CanFireLaserNow => BurstCadenceReady &&
            LineOfSightKnown && LineOfSight;
    }

    /// <summary>Exact three-term vanilla AI_069 Fishron enrage predicate.</summary>
    public struct DukeFishronNativeEnrageObservation
    {
        public bool Known;
        public int NpcKey;
        public bool PlayerAboveY800Band;
        public bool PlayerBelowWorldSurface;
        public bool PlayerInsideCentralHorizontalBand;
        public bool NativeEnraged;

        public bool ExpectedEnraged => PlayerAboveY800Band ||
            PlayerBelowWorldSurface || PlayerInsideCentralHorizontalBand;
    }

    /// <summary>Vanilla AI_123 state and local timers for one Deerclops NPC.</summary>
    public struct DeerclopsNativeTimerObservation
    {
        public bool Known;
        public int NpcKey;
        public float Ai0State;
        public float Ai1StateTimer;
        public float LocalAi1MeleeCounter;
        public float LocalAi2ShadowHandTimer;
        public float LocalAi3DistanceInvulnerabilityTimer;
        public int TimeLeft;
        public float HomeTileX;
        public float HomeTileY;
        public bool NativeDirectionKnown;
        public int NativeDirection;

        public int State => Known && PriorityBossNativeContextContract.IsExactInt(
            Ai0State) ? (int)Ai0State : int.MinValue;
    }

    /// <summary>
    /// Native projectile-454 metadata. Ai0 is age/mode and SourceNpcAi1 keeps
    /// the raw NPC key so fractional or non-finite corruption cannot be hidden
    /// by an integer cast. Projectile/NPC key zero is valid; Known is the only
    /// availability marker.
    /// </summary>
    public struct MoonLordProjectile454Observation
    {
        public const int ProjectileType = 454;

        public bool Known;
        public int ProjectileKey;
        public float Ai0AgeOrMode;
        public float SourceNpcAi1;
        public float LocalAi0;
        public float LocalAi1;
        public int TimeLeft;
        public int Alpha;
        public int ExtraUpdates;
        public bool SourceNpcIdentityKnown;
        public bool SourceNpcActive;
        public int SourceNpcType;

        public int SourceNpcKey
        {
            get
            {
                int key;
                return PriorityBossNativeContextContract.TryDecodeNpcKeyOrMinusOne(
                    SourceNpcAi1, out key) ? key : -2;
            }
        }

        public bool Detached => Known && Ai0AgeOrMode < 0f;
        public bool AttachedToSource => Known && Ai0AgeOrMode >= 0f &&
            Ai0AgeOrMode < 30f && SourceNpcKey != -1;

        // Mirrors Projectile AI style 83 exactly: only this native interval
        // suppresses contact damage.  Unknown observations fail closed.
        public bool DamageEnabled => Known && !(Ai0AgeOrMode >= 0f &&
            Ai0AgeOrMode < 60f && SourceNpcKey != -1);
    }

    /// <summary>
    /// Native projectile-456 (Moon Leech) metadata.  EncodedSourceAi0 is the
    /// signed, one-based source NPC key.  Its sign is direction; localAI[0]
    /// is age and localAI[1] is the one-shot contact latch.
    /// </summary>
    public struct MoonLordProjectile456Observation
    {
        public const int ProjectileType = 456;
        public const int RequiredSourceNpcType = 396;

        public bool Known;
        public int ProjectileKey;
        public float EncodedSourceAi0;
        public float TargetPlayerAi1;
        public float AgeTicks;
        public float ContactLatchAi;
        public int TimeLeft;
        public bool SourceNpcIdentityKnown;
        public bool SourceNpcActive;
        public int SourceNpcType;

        public int SourceNpcKey
        {
            get
            {
                int key;
                return PriorityBossNativeContextContract.TryDecodeSignedOneBasedKey(
                    EncodedSourceAi0, out key) ? key : -1;
            }
        }

        public int TargetPlayerKey
        {
            get
            {
                int key;
                return PriorityBossNativeContextContract.TryDecodeNonnegativeKey(
                    TargetPlayerAi1, out key) ? key : -1;
            }
        }

        public bool Returning => Known && EncodedSourceAi0 < 0f;
        public bool ContactLatched => Known && ContactLatchAi == 1f;
        public bool NativeReturnDeadlineReached => Known && AgeTicks >= 330f;
        public bool HasLiveMoonLordSource => Known &&
            SourceNpcIdentityKnown && SourceNpcActive &&
            SourceNpcType == RequiredSourceNpcType;
    }

    /// <summary>
    /// Structural validation for the source-specific observations.  These
    /// checks intentionally distinguish unavailable data from legitimate
    /// zero-valued native state.
    /// </summary>
    public static class PriorityBossNativeContextContract
    {
        public static bool TryValidate(
            in QueenBeeNativeEnrageObservation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Queen Bee native enrage inputs are unavailable", out reason);
            if (value.NpcKey < 0 || !Finite(value.NativeEnrageFactor) ||
                value.NativeEnrageFactor != value.ExpectedEnrageFactor)
                return Invalid("Queen Bee native enrage factor does not match its three vanilla sources", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in WallOfFleshTunnelObservation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Wall of Flesh tunnel bounds are unavailable", out reason);
            if (value.NpcKey < 0 || !value.NativeDirectionKnown ||
                (value.NativeDirection < -1 || value.NativeDirection > 1) ||
                value.DrawAreaBottomPixels < value.DrawAreaTopPixels ||
                (long)value.DrawAreaBottomPixels - value.DrawAreaTopPixels < 160L)
                return Invalid("Wall of Flesh tunnel bounds violate the vanilla 160-pixel minimum", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in WallOfFleshEyeLaserObservation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Wall of Flesh eye laser state is unavailable", out reason);
            if (value.NpcKey < 0 ||
                (value.Ai0EyeSide != -1f && value.Ai0EyeSide != 1f) ||
                !FiniteNonnegative(value.LocalAi1ChargeTimer) ||
                !FiniteNonnegative(value.LocalAi2BurstStage) ||
                !IsExactInt(value.LocalAi2BurstStage))
                return Invalid("Wall of Flesh eye laser state is malformed", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in DukeFishronNativeEnrageObservation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Duke Fishron native enrage inputs are unavailable", out reason);
            if (value.NpcKey < 0 ||
                value.NativeEnraged != value.ExpectedEnraged)
                return Invalid("Duke Fishron native enrage result does not match AI_069", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in DeerclopsNativeTimerObservation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Deerclops native timers are unavailable", out reason);
            if (value.NpcKey < 0 || !KnownDeerclopsState(value.Ai0State) ||
                !Finite(value.Ai1StateTimer) ||
                !Finite(value.LocalAi1MeleeCounter) ||
                !Finite(value.LocalAi2ShadowHandTimer) ||
                !Finite(value.LocalAi3DistanceInvulnerabilityTimer) ||
                value.TimeLeft < 0 || !Finite(value.HomeTileX) ||
                !Finite(value.HomeTileY) || !value.NativeDirectionKnown ||
                value.NativeDirection < -1 || value.NativeDirection > 1)
                return Invalid("Deerclops native timer state is malformed", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in MoonLordProjectile454Observation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Moon Lord projectile 454 metadata is unavailable", out reason);
            int sourceKey;
            if (value.ProjectileKey < 0 || !IsExactInt(value.Ai0AgeOrMode) ||
                !TryDecodeNpcKeyOrMinusOne(value.SourceNpcAi1,
                    out sourceKey) || !Finite(value.LocalAi0) ||
                !Finite(value.LocalAi1) || value.TimeLeft < 0 ||
                value.Alpha < 0 || value.Alpha > 255 ||
                value.ExtraUpdates < 0 ||
                (value.Ai0AgeOrMode >= 0f &&
                 value.Ai0AgeOrMode < 30f && sourceKey < 0))
                return Invalid("Moon Lord projectile 454 metadata is malformed", out reason);
            reason = null;
            return true;
        }

        public static bool TryValidate(
            in MoonLordProjectile456Observation value, out string reason)
        {
            if (!value.Known)
                return Invalid("Moon Lord projectile 456 metadata is unavailable", out reason);
            int sourceKey;
            int targetKey;
            if (value.ProjectileKey < 0 ||
                !TryDecodeSignedOneBasedKey(value.EncodedSourceAi0,
                    out sourceKey) || sourceKey < 0 ||
                !TryDecodeNonnegativeKey(value.TargetPlayerAi1,
                    out targetKey) || targetKey < 0 ||
                !FiniteNonnegative(value.AgeTicks) ||
                (value.ContactLatchAi != 0f && value.ContactLatchAi != 1f) ||
                value.TimeLeft < 0 ||
                (value.SourceNpcIdentityKnown && value.SourceNpcType < 0))
                return Invalid("Moon Lord projectile 456 metadata is malformed", out reason);
            reason = null;
            return true;
        }

        internal static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool IsExactInt(float value) => Finite(value) &&
            (double)value >= int.MinValue && (double)value <= int.MaxValue &&
            value == (float)Math.Truncate(value);

        internal static bool TryDecodeSignedOneBasedKey(float encoded,
            out int key)
        {
            key = -1;
            if (!IsExactInt(encoded) || encoded == 0f ||
                encoded == int.MinValue)
                return false;
            var signed = (int)encoded;
            var magnitude = Math.Abs(signed);
            key = magnitude - 1;
            return key >= 0;
        }

        internal static bool TryDecodeNonnegativeKey(float encoded,
            out int key)
        {
            key = -1;
            if (!IsExactInt(encoded) || encoded < 0f) return false;
            key = (int)encoded;
            return true;
        }

        internal static bool TryDecodeNpcKeyOrMinusOne(float encoded,
            out int key)
        {
            key = -2;
            if (!IsExactInt(encoded) || encoded < -1f) return false;
            key = (int)encoded;
            return true;
        }

        private static bool KnownDeerclopsState(float state) =>
            IsExactInt(state) && state >= -1f && state <= 8f;

        private static bool FiniteNonnegative(float value) =>
            Finite(value) && value >= 0f;

        private static bool Invalid(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
