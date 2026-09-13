using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPriorityBossNativeContextRegressions()
        {
            Run(nameof(PriorityNativeAvailabilityNeverUsesZeroAsASentinel),
                PriorityNativeAvailabilityNeverUsesZeroAsASentinel);
            Run(nameof(QueenBeeEnragePreservesAllThreeVanillaSources),
                QueenBeeEnragePreservesAllThreeVanillaSources);
            Run(nameof(WallTunnelAndEyeLaserKeepIndependentAuthority),
                WallTunnelAndEyeLaserKeepIndependentAuthority);
            Run(nameof(FishronEnrageMatchesTheExactThreeTermPredicate),
                FishronEnrageMatchesTheExactThreeTermPredicate);
            Run(nameof(EmpressRageHandlesNormalAndRemixWorlds),
                EmpressRageHandlesNormalAndRemixWorlds);
            Run(nameof(EmpressAiThreeRetainsPhaseAndGenuineRageBits),
                EmpressAiThreeRetainsPhaseAndGenuineRageBits);
            Run(nameof(DeerclopsAcceptsEveryNativeStateAndZeroTimers),
                DeerclopsAcceptsEveryNativeStateAndZeroTimers);
            Run(nameof(MoonLord454MirrorsTheNativeDamageWindow),
                MoonLord454MirrorsTheNativeDamageWindow);
            Run(nameof(MoonLord456DecodesSignedOneBasedNpcKeys),
                MoonLord456DecodesSignedOneBasedNpcKeys);
            Run(nameof(MoonLord456RejectsInvalidSourceEvenWithValidTarget),
                MoonLord456RejectsInvalidSourceEvenWithValidTarget);
            Run(nameof(MoonLord456SeparatesMetadataFromSourceLiveness),
                MoonLord456SeparatesMetadataFromSourceLiveness);
        }

        private static void PriorityNativeAvailabilityNeverUsesZeroAsASentinel()
        {
            string reason;
            False(PriorityBossNativeContextContract.TryValidate(
                default(QueenBeeNativeEnrageObservation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(WallOfFleshTunnelObservation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(DukeFishronNativeEnrageObservation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(EmpressNativeCombatObservation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(DeerclopsNativeTimerObservation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(MoonLordProjectile454Observation), out reason));
            False(PriorityBossNativeContextContract.TryValidate(
                default(MoonLordProjectile456Observation), out reason));

            var queen = new QueenBeeNativeEnrageObservation { Known = true };
            True(PriorityBossNativeContextContract.TryValidate(
                in queen, out reason), reason);
            var fishron = new DukeFishronNativeEnrageObservation { Known = true };
            True(PriorityBossNativeContextContract.TryValidate(
                in fishron, out reason), reason);
            var empress = new EmpressNativeCombatObservation { Known = true };
            True(PriorityBossNativeContextContract.TryValidate(
                in empress, out reason), reason);
        }

        private static void QueenBeeEnragePreservesAllThreeVanillaSources()
        {
            string reason;
            for (var mask = 0; mask < 8; mask++)
            {
                var value = new QueenBeeNativeEnrageObservation
                {
                    Known = true,
                    BossAboveWorldSurface = (mask & 1) != 0,
                    TargetOutsideJungle = (mask & 2) != 0,
                    GetGoodWorld = (mask & 4) != 0
                };
                value.NativeEnrageFactor = value.ExpectedEnrageFactor;
                True(PriorityBossNativeContextContract.TryValidate(
                    in value, out reason), reason);
                Equal(mask != 0, value.Enraged);
            }

            var inconsistent = new QueenBeeNativeEnrageObservation
            {
                Known = true,
                BossAboveWorldSurface = true,
                NativeEnrageFactor = 0f
            };
            False(PriorityBossNativeContextContract.TryValidate(
                in inconsistent, out reason));
        }

        private static void WallTunnelAndEyeLaserKeepIndependentAuthority()
        {
            string reason;
            var tunnel = new WallOfFleshTunnelObservation
            {
                Known = true,
                NativeDirectionKnown = true,
                DrawAreaTopPixels = 0,
                DrawAreaBottomPixels = 160
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in tunnel, out reason), reason);
            Equal(160, tunnel.HeightPixels);
            True(tunnel.ContainsCenterY(0f));
            True(tunnel.ContainsCenterY(160f));
            False(tunnel.ContainsCenterY(160.01f));
            tunnel.DrawAreaBottomPixels = 159;
            False(PriorityBossNativeContextContract.TryValidate(
                in tunnel, out reason));

            var eye = new WallOfFleshEyeLaserObservation
            {
                Known = true,
                NpcKey = 0,
                Ai0EyeSide = -1f,
                LocalAi1ChargeTimer = 0f,
                LocalAi2BurstStage = 0f
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in eye, out reason), reason);
            True(eye.UpperEye);
            False(eye.ChargeThresholdExceeded || eye.BurstCadenceReady ||
                eye.CanFireLaserNow);
            eye.LocalAi1ChargeTimer = 601f;
            True(eye.ChargeThresholdExceeded);
            False(eye.BurstCadenceReady);
            eye.LocalAi1ChargeTimer = 46f;
            eye.LocalAi2BurstStage = 1f;
            True(eye.BurstActive && eye.BurstCadenceReady);
            False(eye.CanFireLaserNow);
            eye.LineOfSightKnown = true;
            eye.LineOfSight = true;
            True(eye.CanFireLaserNow);
        }

        private static void FishronEnrageMatchesTheExactThreeTermPredicate()
        {
            string reason;
            for (var mask = 0; mask < 8; mask++)
            {
                var value = new DukeFishronNativeEnrageObservation
                {
                    Known = true,
                    PlayerAboveY800Band = (mask & 1) != 0,
                    PlayerBelowWorldSurface = (mask & 2) != 0,
                    PlayerInsideCentralHorizontalBand = (mask & 4) != 0,
                    NativeEnraged = mask != 0
                };
                True(PriorityBossNativeContextContract.TryValidate(
                    in value, out reason), reason);
                value.NativeEnraged = !value.NativeEnraged;
                False(PriorityBossNativeContextContract.TryValidate(
                    in value, out reason));
            }
        }

        private static void EmpressRageHandlesNormalAndRemixWorlds()
        {
            string reason;
            var normalNight = new EmpressNativeCombatObservation
            {
                Known = true,
                DayTime = false,
                RemixWorld = false,
                RemixRageMode = true,
                BossAboveWorldSurface = true,
                NativeShouldBeEnraged = false
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in normalNight, out reason), reason);

            normalNight.DayTime = true;
            normalNight.NativeShouldBeEnraged = true;
            True(PriorityBossNativeContextContract.TryValidate(
                in normalNight, out reason), reason);

            var remix = new EmpressNativeCombatObservation
            {
                Known = true,
                RemixWorld = true,
                BossAboveWorldSurface = true,
                NativeShouldBeEnraged = true
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in remix, out reason), reason);
            remix.BossAboveWorldSurface = false;
            remix.NativeShouldBeEnraged = false;
            True(PriorityBossNativeContextContract.TryValidate(
                in remix, out reason), reason);
            remix.RemixRageMode = true;
            remix.NativeShouldBeEnraged = true;
            True(PriorityBossNativeContextContract.TryValidate(
                in remix, out reason), reason);
        }

        private static void EmpressAiThreeRetainsPhaseAndGenuineRageBits()
        {
            string reason;
            for (var ai3 = 0; ai3 <= 3; ai3++)
            {
                var value = new EmpressNativeCombatObservation
                {
                    Known = true,
                    Ai3PhaseAndRage = ai3
                };
                True(PriorityBossNativeContextContract.TryValidate(
                    in value, out reason), reason);
                Equal(ai3 == 1 || ai3 == 3, value.PhaseTwo);
                Equal(ai3 == 2 || ai3 == 3, value.GenuinelyEnraged);
            }
            var invalid = new EmpressNativeCombatObservation
            {
                Known = true,
                Ai3PhaseAndRage = 4f
            };
            False(PriorityBossNativeContextContract.TryValidate(
                in invalid, out reason));
        }

        private static void DeerclopsAcceptsEveryNativeStateAndZeroTimers()
        {
            string reason;
            for (var state = -1; state <= 8; state++)
            {
                var value = new DeerclopsNativeTimerObservation
                {
                    Known = true,
                    NpcKey = 0,
                    Ai0State = state,
                    Ai1StateTimer = 0f,
                    LocalAi1MeleeCounter = 0f,
                    LocalAi2ShadowHandTimer = 0f,
                    LocalAi3DistanceInvulnerabilityTimer = state == -1 ? -10f : 0f,
                    TimeLeft = 0,
                    HomeTileX = 0f,
                    HomeTileY = 0f,
                    NativeDirectionKnown = true
                };
                True(PriorityBossNativeContextContract.TryValidate(
                    in value, out reason), reason);
                Equal(state, value.State);
            }

            var unknownState = new DeerclopsNativeTimerObservation
            {
                Known = true,
                Ai0State = 8.5f
            };
            False(PriorityBossNativeContextContract.TryValidate(
                in unknownState, out reason));
        }

        private static void MoonLord454MirrorsTheNativeDamageWindow()
        {
            string reason;
            var value = new MoonLordProjectile454Observation
            {
                Known = true,
                ProjectileKey = 0,
                Ai0AgeOrMode = 0f,
                SourceNpcAi1 = 0f,
                LocalAi0 = 0f,
                LocalAi1 = 0f,
                TimeLeft = 0,
                Alpha = 0,
                ExtraUpdates = 0
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in value, out reason), reason);
            True(value.AttachedToSource);
            False(value.DamageEnabled);

            value.Ai0AgeOrMode = 59f;
            False(value.DamageEnabled);
            value.Ai0AgeOrMode = 60f;
            True(value.DamageEnabled);
            value.Ai0AgeOrMode = -1f;
            True(value.Detached);
            True(value.DamageEnabled);
            value.Ai0AgeOrMode = 0f;
            value.SourceNpcAi1 = -1f;
            True(value.DamageEnabled);
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));
        }

        private static void MoonLord456DecodesSignedOneBasedNpcKeys()
        {
            string reason;
            var value = new MoonLordProjectile456Observation
            {
                Known = true,
                ProjectileKey = 0,
                EncodedSourceAi0 = 1f,
                TargetPlayerAi1 = 0f,
                AgeTicks = 0f,
                ContactLatchAi = 0f,
                TimeLeft = 0
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in value, out reason), reason);
            Equal(0, value.SourceNpcKey);
            False(value.Returning || value.ContactLatched ||
                value.NativeReturnDeadlineReached);

            value.EncodedSourceAi0 = -1f;
            value.AgeTicks = 330f;
            value.ContactLatchAi = 1f;
            True(PriorityBossNativeContextContract.TryValidate(
                in value, out reason), reason);
            Equal(0, value.SourceNpcKey);
            True(value.Returning && value.ContactLatched &&
                value.NativeReturnDeadlineReached);

            value.EncodedSourceAi0 = 0f;
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));
            value.EncodedSourceAi0 = 1.5f;
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));
            value.EncodedSourceAi0 = 2147483648f;
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));
        }

        private static void MoonLord456SeparatesMetadataFromSourceLiveness()
        {
            string reason;
            var value = new MoonLordProjectile456Observation
            {
                Known = true,
                ProjectileKey = 4,
                EncodedSourceAi0 = 1f,
                TargetPlayerAi1 = 0f,
                AgeTicks = 0f,
                ContactLatchAi = 0f,
                TimeLeft = 120
            };
            True(PriorityBossNativeContextContract.TryValidate(
                in value, out reason), reason);
            False(value.HasLiveMoonLordSource);

            value.SourceNpcIdentityKnown = true;
            value.SourceNpcActive = true;
            value.SourceNpcType = MoonLordProjectile456Observation.RequiredSourceNpcType;
            True(value.HasLiveMoonLordSource);
            value.SourceNpcType = 397;
            False(value.HasLiveMoonLordSource);
            value.SourceNpcType = 396;
            value.SourceNpcActive = false;
            False(value.HasLiveMoonLordSource);
        }

        private static void MoonLord456RejectsInvalidSourceEvenWithValidTarget()
        {
            string reason;
            var value = new MoonLordProjectile456Observation
            {
                Known = true,
                ProjectileKey = 1,
                // Zero is not a valid signed one-based NPC key.  The target
                // player key is valid and must not mask this malformed source.
                EncodedSourceAi0 = 0f,
                TargetPlayerAi1 = 0f,
                AgeTicks = 1f,
                ContactLatchAi = 0f,
                TimeLeft = 10
            };
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));

            value.EncodedSourceAi0 = 1.5f;
            False(PriorityBossNativeContextContract.TryValidate(
                in value, out reason));
        }
    }
}
