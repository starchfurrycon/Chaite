using System;
using System.Reflection;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private delegate FlightSnapshot FlightRead(object player, bool mountActive, out JumpSnapshot jump);
        private static FlightRead _nativeFlightRead;

        private static void RunNativeFlightReaderRegressions()
        {
            Run(nameof(NativeFlightReaderSharesObservedJumpAndInputState), NativeFlightReaderSharesObservedJumpAndInputState);
            Run(nameof(NativeFlightReaderCapturesFeatherFallWithoutChangingResources), NativeFlightReaderCapturesFeatherFallWithoutChangingResources);
            Run(nameof(NativeFlightReaderAllowsConvertedResourcesWithoutRewritingThem), NativeFlightReaderAllowsConvertedResourcesWithoutRewritingThem);
            Run(nameof(NativeFlightReaderRejectsOtherWingsBootsAndRocketBatches), NativeFlightReaderRejectsOtherWingsBootsAndRocketBatches);
            Run(nameof(NativeFlightReaderRejectsAllUnmodeledBooleanBranches), NativeFlightReaderRejectsAllUnmodeledBooleanBranches);
            Run(nameof(NativeFlightReaderRejectsInvalidResourceCapacitiesAndNumbers), NativeFlightReaderRejectsInvalidResourceCapacitiesAndNumbers);
            Run(nameof(NativeFlightReaderPreservesOrdinaryJumpFallback), NativeFlightReaderPreservesOrdinaryJumpFallback);
            Run(nameof(NativeFlightReaderDoesNotLeakProfilesAcrossEquipmentChanges), NativeFlightReaderDoesNotLeakProfilesAcrossEquipmentChanges);
            Run(nameof(NativeFlightReaderReportsEffectiveResourceFraction), NativeFlightReaderReportsEffectiveResourceFraction);
            Run(nameof(NativeFlightReaderHotPathDoesNotAllocate), NativeFlightReaderHotPathDoesNotAllocate);
        }

        private static FlightRead NativeFlightRead()
        {
            if (_nativeFlightRead != null) return _nativeFlightRead;
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.NativeFlightReader", true);
            var reader = Activator.CreateInstance(type, new object[] { typeof(NativeFlightPlayer) });
            _nativeFlightRead = (FlightRead)Delegate.CreateDelegate(typeof(FlightRead), reader, type.GetMethod("Read"));
            return _nativeFlightRead;
        }

        private static void NativeFlightReaderSharesObservedJumpAndInputState()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer
            {
                jump = 9, releaseJump = false, autoJump = true,
                hasJumpOption_Cloud = true, canJumpAgain_Cloud = true,
                canRocket = true, rocketRelease = false, justJumped = true
            };
            NativeFlightPlayer.jumpSpeed = 99f;
            NativeFlightPlayer.jumpHeight = 99;
            JumpSnapshot jump;
            var flight = read(player, false, out jump);
            True(flight.Known && jump.Known);
            Equal(1, flight.WingsLogic);
            Equal(0, flight.RocketBoots);
            Equal(9, jump.RemainingTicks);
            Equal(5.01f, jump.Speed);
            Equal(15, jump.Height);
            False(jump.ReleaseReady);
            True(jump.AutoJump && jump.CloudAvailable && jump.CloudEnabled);
            True(flight.CanRocket && flight.JustJumped);
            False(flight.RocketRelease);
            // The reader does not pre-run ResetEffects or consume a jump. Core's
            // pre-movement transition owns resetting this sampled prior-frame flag.
            True(player.justJumped && player.canJumpAgain_Cloud);
            Equal(9, player.jump);
            Equal(99f, NativeFlightPlayer.jumpSpeed);
            Equal(99, NativeFlightPlayer.jumpHeight);
        }

        private static void NativeFlightReaderCapturesFeatherFallWithoutChangingResources()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer
            {
                slowFall = true, wingTime = 71f, rocketBoots = 2, rocketTime = 4,
                canRocket = true, rocketRelease = true
            };
            JumpSnapshot jump;
            var flight = read(player, false, out jump);
            True(flight.Known && jump.Known && jump.SlowFall);
            Equal(71f, flight.WingTime);
            Equal(4, flight.RocketTime);
            True(flight.CanRocket && flight.RocketRelease);
            True(player.slowFall);
            Equal(71f, player.wingTime);
            Equal(4, player.rocketTime);
        }

        private static void NativeFlightReaderAllowsConvertedResourcesWithoutRewritingThem()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer { rocketBoots = 2, wingTime = 142f, rocketTime = 0 };
            JumpSnapshot jump;
            for (var index = 0; index < 20; index++)
            {
                var flight = read(player, false, out jump);
                True(flight.Known && jump.Known);
                Equal(142f, flight.WingTime);
                Equal(100, flight.WingTimeMax);
                Equal(0, flight.RocketTime);
                Equal(7, flight.RocketTimeMax);
            }
            Equal(142f, player.wingTime);
            Equal(0, player.rocketTime);
            player.wingTime = 0f;
            player.rocketTime = 7;
            var beforeConversion = read(player, false, out jump);
            True(beforeConversion.Known);
            Equal(0f, beforeConversion.WingTime);
            Equal(7, beforeConversion.RocketTime); // Read must not perform the future conversion.
            player.rocketBoots = 0;
            True(read(player, false, out jump).Known); // Native ground update restores 7 even without boots.
        }

        private static void NativeFlightReaderRejectsOtherWingsBootsAndRocketBatches()
        {
            var read = NativeFlightRead();
            JumpSnapshot jump;
            foreach (var wings in new[] { -1, 2, 22, 44, 999 })
            {
                var unknown = read(new NativeFlightPlayer
                    { wingsLogic = wings }, false, out jump);
                False(unknown.Known);
                Equal(wings, unknown.WingsLogic);
                False(jump.Known);
            }
            foreach (var boots in new[] { -1, 1, 3, 4, 5, 999 })
            {
                var unknown = read(new NativeFlightPlayer
                    { rocketBoots = boots }, false, out jump);
                False(unknown.Known);
                Equal(boots, unknown.RocketBoots);
                False(jump.Known);
            }
            foreach (var delay in new[] { -1, 1, 10, 999 })
            {
                False(read(new NativeFlightPlayer { rocketBoots = 2, rocketDelay = delay }, false, out jump).Known);
                False(jump.Known);
            }
            False(read(new NativeFlightPlayer(), true, out jump).Known);
            False(jump.Known);
            False(read(new NativeFlightPlayer { gravDir = -1f }, false, out jump).Known);
            False(jump.Known); // Inverted wing glide is not the ordinary inverted-jump model.
        }

        private static void NativeFlightReaderRejectsAllUnmodeledBooleanBranches()
        {
            var read = NativeFlightRead();
            foreach (var field in typeof(NativeFlightPlayer).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType != typeof(bool) || field.Name == "releaseJump" || field.Name == "autoJump" ||
                    field.Name == "hasJumpOption_Cloud" || field.Name == "canJumpAgain_Cloud" || field.Name == "isPerformingJump_Cloud" ||
                    field.Name == "canRocket" || field.Name == "rocketRelease" || field.Name == "justJumped" ||
                    field.Name == "slowFall") continue;
                var player = new NativeFlightPlayer();
                field.SetValue(player, true);
                JumpSnapshot jump;
                False(read(player, false, out jump).Known, "must reject flight branch " + field.Name);
                False(jump.Known, "must not fall back to ordinary physics with equipped wings: " + field.Name);
                True((bool)field.GetValue(player), "reader changed " + field.Name);
            }
            var stool = new NativeFlightPlayer();
            stool.portableStoolInfo.IsInUse = true;
            JumpSnapshot unsupported;
            False(read(stool, false, out unsupported).Known);
            True(stool.portableStoolInfo.IsInUse);
        }

        private static void NativeFlightReaderRejectsInvalidResourceCapacitiesAndNumbers()
        {
            var read = NativeFlightRead();
            JumpSnapshot jump;
            foreach (var value in new[] { -1f, .5f, 100.5f, 143f, float.NaN, float.PositiveInfinity })
            {
                False(read(new NativeFlightPlayer { wingTime = value }, false, out jump).Known);
                False(jump.Known);
            }
            foreach (var capacity in new[] { -1, 0, 99, 101, int.MaxValue })
                False(read(new NativeFlightPlayer { wingTimeMax = capacity }, false, out jump).Known);
            foreach (var capacity in new[] { -1, 0, 6, 8, int.MaxValue })
                False(read(new NativeFlightPlayer { rocketTimeMax = capacity }, false, out jump).Known);
            foreach (var resource in new[] { -1, 8, int.MaxValue })
                False(read(new NativeFlightPlayer { rocketTime = resource }, false, out jump).Known);
            foreach (var boost in new[] { .01f, -1f, float.NaN, float.PositiveInfinity })
                False(read(new NativeFlightPlayer { jumpSpeedBoost = boost }, false, out jump).Known);
            foreach (var counter in new[] { -1, 16, int.MaxValue })
                False(read(new NativeFlightPlayer { jump = counter }, false, out jump).Known);
            foreach (var name in new[] { "grapCount", "cartRampTime" })
            {
                var player = new NativeFlightPlayer();
                typeof(NativeFlightPlayer).GetField(name).SetValue(player, 1);
                False(read(player, false, out jump).Known);
            }
            foreach (var gravity in new[] { -.1f, float.NaN, float.PositiveInfinity })
                False(read(new NativeFlightPlayer { gravity = gravity }, false, out jump).Known);
            foreach (var fall in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                False(read(new NativeFlightPlayer { maxFallSpeed = fall }, false, out jump).Known);
            True(read(new NativeFlightPlayer { gravity = .04f }, false, out jump).Known);
        }

        private static void NativeFlightReaderPreservesOrdinaryJumpFallback()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer
            {
                wingsLogic = 0, wingTimeMax = 0, wingTime = 0, rocketBoots = 0,
                hasJumpOption_Cloud = true, canJumpAgain_Cloud = true, gravDir = -1f
            };
            JumpSnapshot jump;
            False(read(player, false, out jump).Known);
            True(jump.Known && jump.CloudAvailable && jump.CloudEnabled);
            Equal(5.01f, jump.Speed);
            Equal(15, jump.Height);
            player.jumpBoost = true;
            False(read(player, false, out jump).Known);
            False(jump.Known);
            False(read(null, false, out jump).Known);
            False(jump.Known);
            Equal(0, jump.RemainingTicks);
        }

        private static void NativeFlightReaderDoesNotLeakProfilesAcrossEquipmentChanges()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer();
            JumpSnapshot jump;
            True(read(player, false, out jump).Known);
            player.wingsLogic = 2;
            False(read(player, false, out jump).Known);
            False(jump.Known);
            player.wingsLogic = 1;
            player.rocketBoots = 2;
            True(read(player, false, out jump).Known);
            player.merman = true;
            False(read(player, false, out jump).Known);
            False(jump.Known);
            player.merman = false;
            player.canJumpAgain_Cloud = true;
            player.hasJumpOption_Cloud = false;
            False(read(player, false, out jump).Known);
            player.hasJumpOption_Cloud = true;
            True(read(player, false, out jump).Known);
        }

        private static void NativeFlightReaderReportsEffectiveResourceFraction()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer { rocketBoots = 2, wingTime = 142, rocketTime = 0 };
            JumpSnapshot jump;
            var flight = read(player, false, out jump);
            Equal(142f, FlightMotion.RemainingWingTicks(in flight));
            Equal(1f, FlightMotion.ResourceFraction(in flight));
            player.wingTime = 71;
            flight = read(player, false, out jump);
            Equal(.5f, FlightMotion.ResourceFraction(in flight));
            player.wingTime = 100;
            player.rocketTime = 7;
            flight = read(player, false, out jump);
            Equal(142f, FlightMotion.RemainingWingTicks(in flight));
            Equal(1f, FlightMotion.ResourceFraction(in flight));
            player.rocketBoots = 0;
            player.wingTime = 50;
            flight = read(player, false, out jump);
            Equal(50f, FlightMotion.RemainingWingTicks(in flight));
            Equal(.5f, FlightMotion.ResourceFraction(in flight)); // Unusable rocket counter is not free flight.
        }

        private static void NativeFlightReaderHotPathDoesNotAllocate()
        {
            var read = NativeFlightRead();
            var player = new NativeFlightPlayer { rocketBoots = 2, wingTime = 142, rocketTime = 0 };
            string scope;
            var allocated = CreateAllocationCounter(out scope);
            if (allocated == null) throw new InvalidOperationException("Cannot verify flight reader allocations: " + scope);
            var checksum = 0f;
            JumpSnapshot jump;
            for (var index = 0; index < 1000; index++) checksum += read(player, false, out jump).WingTime;
            allocated();
            var before = allocated();
            for (var index = 0; index < 10000; index++) checksum += read(player, false, out jump).WingTime;
            var delta = allocated() - before;
            True(checksum > 0f);
            Equal(0L, delta);
        }

#pragma warning disable CS0649
        private sealed class NativeFlightPlayer
        {
            public static float jumpSpeed;
            public static int jumpHeight;
            public int jump, wingsLogic = 1, rocketBoots, rocketDelay, grapCount, cartRampTime;
            public int wingTimeMax = 100, rocketTime = 7, rocketTimeMax = 7;
            public float wingTime = 100f, jumpSpeedBoost, gravity = .4f, maxFallSpeed = 10.01f, gravDir = 1f;
            public bool canRocket, rocketRelease, justJumped, merman, tryKeepingHoveringDown, tryKeepingHoveringUp;
            public bool releaseJump = true, canJumpAgain_Cloud, hasJumpOption_Cloud, isPerformingJump_Cloud, autoJump;
            public bool dead, ghost, wet, shimmerWet, shimmering, jumpBoost, wereWolf, moonLordLegs, empressBrooch, frogLegJumpBoost;
            public bool sticky, dazed, frozen, webbed, stoned, carpet, sliding, pulley, slowFall, vortexDebuff, tongued, onTrack;
            public bool hasDeadCellsDownDash, isPerformingJump_DownDash;
            public bool hasJumpOption_Sandstorm, canJumpAgain_Sandstorm, isPerformingJump_Sandstorm;
            public bool hasJumpOption_Blizzard, canJumpAgain_Blizzard, isPerformingJump_Blizzard;
            public bool hasJumpOption_Fart, canJumpAgain_Fart, isPerformingJump_Fart;
            public bool hasJumpOption_Sail, canJumpAgain_Sail, isPerformingJump_Sail;
            public bool hasJumpOption_Unicorn, canJumpAgain_Unicorn, isPerformingJump_Unicorn;
            public bool hasJumpOption_Santank, canJumpAgain_Santank, isPerformingJump_Santank;
            public bool hasJumpOption_WallOfFleshGoat, canJumpAgain_WallOfFleshGoat, isPerformingJump_WallOfFleshGoat;
            public bool hasJumpOption_Basilisk, canJumpAgain_Basilisk, isPerformingJump_Basilisk;
            public NativeFlightStool portableStoolInfo;
        }

        private struct NativeFlightStool { public bool IsInUse; }
#pragma warning restore CS0649
    }
}
