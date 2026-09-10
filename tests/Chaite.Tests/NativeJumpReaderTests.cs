using System;
using System.Reflection;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static Func<object, bool, JumpSnapshot> _nativeJumpRead;

        private static void RunNativeJumpReaderRegressions()
        {
            Run(nameof(NativeJumpReaderUsesInstanceStateNotSharedStaticParameters), NativeJumpReaderUsesInstanceStateNotSharedStaticParameters);
            Run(nameof(NativeJumpReaderPreservesCloudStateWithoutConsumingIt), NativeJumpReaderPreservesCloudStateWithoutConsumingIt);
            Run(nameof(NativeJumpReaderRejectsEveryUnsupportedBooleanBranch), NativeJumpReaderRejectsEveryUnsupportedBooleanBranch);
            Run(nameof(NativeJumpReaderRejectsFlightBoostAndInvalidData), NativeJumpReaderRejectsFlightBoostAndInvalidData);
            Run(nameof(NativeJumpReaderNestedStoolIsLiveAndReadOnly), NativeJumpReaderNestedStoolIsLiveAndReadOnly);
            Run(nameof(NativeJumpReaderHotPathDoesNotAllocate), NativeJumpReaderHotPathDoesNotAllocate);
        }

        private static Func<object, bool, JumpSnapshot> NativeJumpRead()
        {
            if (_nativeJumpRead != null) return _nativeJumpRead;
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.NativeJumpReader", true);
            var reader = Activator.CreateInstance(type, new object[] { typeof(NativeJumpPlayer) });
            _nativeJumpRead = (Func<object, bool, JumpSnapshot>)Delegate.CreateDelegate(
                typeof(Func<object, bool, JumpSnapshot>), reader, type.GetMethod("Read"));
            return _nativeJumpRead;
        }

        private static void NativeJumpReaderUsesInstanceStateNotSharedStaticParameters()
        {
            var read = NativeJumpRead();
            var player = new NativeJumpPlayer { jump = 6, releaseJump = false, autoJump = true };
            NativeJumpPlayer.jumpSpeed = 99f;
            NativeJumpPlayer.jumpHeight = 99;
            var state = read(player, false);
            True(state.Known);
            Equal(5.01f, state.Speed);
            Equal(15, state.Height);
            Equal(6, state.RemainingTicks);
            False(state.ReleaseReady);
            True(state.AutoJump);
            Equal(99f, NativeJumpPlayer.jumpSpeed);
            Equal(99, NativeJumpPlayer.jumpHeight);
            Equal(6, player.jump);
            False(read(null, false).Known);
        }

        private static void NativeJumpReaderPreservesCloudStateWithoutConsumingIt()
        {
            var read = NativeJumpRead();
            var player = new NativeJumpPlayer { hasJumpOption_Cloud = true, canJumpAgain_Cloud = true };
            for (var index = 0; index < 20; index++)
            {
                var state = read(player, false);
                True(state.Known && state.CloudAvailable && state.CloudEnabled && state.ReleaseReady);
            }
            True(player.canJumpAgain_Cloud);
            player.canJumpAgain_Cloud = false;
            player.isPerformingJump_Cloud = true;
            player.jump = 11;
            var active = read(player, false);
            True(active.Known && active.CloudEnabled && !active.CloudAvailable);
            Equal(11, active.RemainingTicks);
            player.hasJumpOption_Cloud = false;
            player.canJumpAgain_Cloud = true;
            False(read(player, false).Known);
        }

        private static void NativeJumpReaderRejectsEveryUnsupportedBooleanBranch()
        {
            var read = NativeJumpRead();
            foreach (var field in typeof(NativeJumpPlayer).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType != typeof(bool) || field.Name == "releaseJump" || field.Name == "autoJump" ||
                    field.Name == "hasJumpOption_Cloud" || field.Name == "canJumpAgain_Cloud" || field.Name == "isPerformingJump_Cloud")
                    continue;
                var player = new NativeJumpPlayer();
                field.SetValue(player, true);
                False(read(player, false).Known, "must reject unsupported branch " + field.Name);
                True((bool)field.GetValue(player), "reader mutated " + field.Name);
            }
        }

        private static void NativeJumpReaderRejectsFlightBoostAndInvalidData()
        {
            var read = NativeJumpRead();
            False(read(new NativeJumpPlayer(), true).Known);
            foreach (var name in new[] { "wingsLogic", "rocketBoots", "rocketDelay", "grapCount", "cartRampTime" })
            {
                var player = new NativeJumpPlayer();
                typeof(NativeJumpPlayer).GetField(name).SetValue(player, 1);
                False(read(player, false).Known, name);
            }
            foreach (var boost in new[] { .01f, -1f, float.NaN, float.PositiveInfinity })
                False(read(new NativeJumpPlayer { jumpSpeedBoost = boost }, false).Known);
            foreach (var ticks in new[] { -1, 16, int.MaxValue })
                False(read(new NativeJumpPlayer { jump = ticks }, false).Known);
            foreach (var gravity in new[] { -.01f, float.NaN, float.PositiveInfinity })
                False(read(new NativeJumpPlayer { gravity = gravity }, false).Known);
            foreach (var fall in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                False(read(new NativeJumpPlayer { maxFallSpeed = fall }, false).Known);
            foreach (var direction in new[] { 0f, .5f, float.NaN, float.PositiveInfinity })
                False(read(new NativeJumpPlayer { gravDir = direction }, false).Known);
            True(read(new NativeJumpPlayer { gravity = .04f, gravDir = -1f }, false).Known);
        }

        private static void NativeJumpReaderNestedStoolIsLiveAndReadOnly()
        {
            var read = NativeJumpRead();
            var player = new NativeJumpPlayer();
            True(read(player, false).Known);
            player.portableStoolInfo.IsInUse = true;
            player.portableStoolInfo.HeightBoost = 16;
            False(read(player, false).Known);
            True(player.portableStoolInfo.IsInUse);
            Equal(16, player.portableStoolInfo.HeightBoost);
            player.portableStoolInfo.IsInUse = false;
            True(read(player, false).Known);
        }

        private static void NativeJumpReaderHotPathDoesNotAllocate()
        {
            var read = NativeJumpRead();
            var player = new NativeJumpPlayer { hasJumpOption_Cloud = true, canJumpAgain_Cloud = true };
            string scope;
            var allocated = CreateAllocationCounter(out scope);
            if (allocated == null) throw new InvalidOperationException("Cannot verify native jump reader allocation: " + scope);
            var checksum = 0;
            for (var index = 0; index < 1000; index++) checksum += read(player, false).Height;
            allocated();
            var before = allocated();
            for (var index = 0; index < 10000; index++) checksum += read(player, false).Height;
            var delta = allocated() - before;
            True(checksum > 0);
            Equal(0L, delta);
        }

#pragma warning disable CS0649
        private sealed class NativeJumpPlayer
        {
            public static float jumpSpeed;
            public static int jumpHeight;
            public int jump, wingsLogic, rocketBoots, rocketDelay, grapCount, cartRampTime;
            public float jumpSpeedBoost, gravity = .4f, maxFallSpeed = 10f, gravDir = 1f;
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
            public NativeJumpStool portableStoolInfo;
        }

        private struct NativeJumpStool { public bool IsInUse; public int HeightBoost; }
#pragma warning restore CS0649
    }
}
