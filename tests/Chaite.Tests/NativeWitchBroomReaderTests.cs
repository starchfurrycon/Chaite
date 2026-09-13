using Chaite.Core;
using Chaite.Plugin;
using System;

namespace Chaite.Tests
{
    public static class NativeWitchBroomReaderTests
    {
        public static int RunAll()
        {
            IdentityAndSelectorRemainSeparate();
            EntryAndNativeMountFramesRemainSeparate();
            EveryObservedMotionGateFailsClosed();
            ItemUseAndMountReleaseEdgesAreObserved();
            return 4;
        }

        private static void IdentityAndSelectorRemainSeparate()
        {
            var player = ValidPlayer();
            var reader = Reader();
            var toggle = reader.ReadToggle(player, true, true, false);
            Equal(4444, toggle.QuickMountItemType);
            Equal(23, toggle.QuickMountType);
            False(toggle.MountActive);

            player.quickMountItem = new FakeItem { type = 4443, mountType = 23 };
            var wrongItem = reader.ReadToggle(player, true, true, false);
            False(WitchBroomMotion.ResolveToggle(in wrongItem).Supported);
            player.quickMountItem = new FakeItem { type = 4444, mountType = 22 };
            var wrongMount = reader.ReadToggle(player, true, true, false);
            False(WitchBroomMotion.ResolveToggle(in wrongMount).Supported);
        }

        private static void EntryAndNativeMountFramesRemainSeparate()
        {
            var player = ValidPlayer();
            var reader = Reader();
            var entry = reader.ReadMotion(player, true, false, false);
            True(entry.MountActive);
            Equal(23, entry.MountType);
            Equal(0, entry.FrameState);

            player.mount._active = true;
            player.mount._type = 23;
            player.mount._frameState = 2;
            var observed = reader.ReadMotion(player, false, false, false);
            True(observed.MountActive);
            Equal(23, observed.MountType);
            Equal(2, observed.FrameState);
        }

        private static void EveryObservedMotionGateFailsClosed()
        {
            var player = ValidPlayer();
            var reader = Reader();
            Action<FakePlayer> unsupported = value =>
            {
                var state = reader.ReadMotion(value, true, true, false);
                False(WitchBroomMotion.TryAdvanceOpenDryTick(in state,
                    default(WitchBroomMotionInput), out _));
            };
            player.wet = true; unsupported(player); player.wet = false;
            player.honeyWet = true; unsupported(player); player.honeyWet = false;
            player.lavaWet = true; unsupported(player); player.lavaWet = false;
            player.shimmerWet = true; unsupported(player); player.shimmerWet = false;
            player.portalPhysicsFlag = true; unsupported(player); player.portalPhysicsFlag = false;
            player._portalPhysicsTime = 1; unsupported(player); player._portalPhysicsTime = 0;
            player.grapCount = 1; unsupported(player); player.grapCount = 0;
            unsupportedWithHook(reader, player);
            player.dashDelay = -1; unsupported(player); player.dashDelay = 0;
            player.CCed = true; unsupported(player); player.CCed = false;
            player.pulley = true; unsupported(player); player.pulley = false;
            player.sliding = true; unsupported(player); player.sliding = false;
            player.windPushed = true; unsupported(player); player.windPushed = false;
            player.frozen = true; unsupported(player); player.frozen = false;
            player.onTrack = true; unsupported(player);
        }

        private static void unsupportedWithHook(NativeWitchBroomReader reader, FakePlayer player)
        {
            var state = reader.ReadMotion(player, true, true, true);
            False(WitchBroomMotion.TryAdvanceOpenDryTick(in state,
                default(WitchBroomMotionInput), out _));
        }

        private static void ItemUseAndMountReleaseEdgesAreObserved()
        {
            var player = ValidPlayer();
            var reader = Reader();
            var ready = reader.ReadToggle(player, true, true, false);
            True(ready.ItemUseStartEdgeReady);
            True(ready.ReleaseMount);
            player.itemAnimation = 1;
            False(reader.ReadToggle(player, true, true, false).ItemUseStartEdgeReady);
            player.itemAnimation = 0;
            player.releaseUseItem = false;
            False(reader.ReadToggle(player, true, true, false).ItemUseStartEdgeReady);
            player.releaseMount = false;
            False(reader.ReadToggle(player, true, true, false).ReleaseMount);
        }

        private static NativeWitchBroomReader Reader() => new NativeWitchBroomReader(
            typeof(FakePlayer), typeof(FakeMount), typeof(FakeItem));

        private static FakePlayer ValidPlayer() => new FakePlayer
        {
            mount = new FakeMount(),
            quickMountItem = new FakeItem { type = 4444, mountType = 23 },
            position = new FakeVector { X = 100f, Y = 100f },
            velocity = new FakeVector { X = 0f, Y = .5f },
            gravity = .4f,
            trackBoost = 0f,
            gravDir = 1f,
            controlMount = true,
            releaseMount = true,
            releaseUp = true,
            releaseUseItem = true,
            slowFall = false,
            shimmering = false,
            eocDash = 0,
            tongued = false,
            dead = false,
            noItems = false,
            itemTime = 0,
            ghost = false,
            webbed = false,
            stoned = false,
            vortexDebuff = false,
            sticky = false,
            dazed = false
        };

        private struct FakeVector { public float X, Y; }
        private sealed class FakeItem { public int type, mountType; }
        private sealed class FakeMount
        {
            public bool _active;
            public int _type = -1;
            public int _frameState;
            public bool Active => _active;
            public int Type => _type;
        }

        private sealed class FakePlayer
        {
            public FakeMount mount;
            public FakeItem quickMountItem;
            public FakeVector position, velocity;
            public float gravity, trackBoost, gravDir;
            public bool controlMount, releaseMount, releaseUp, releaseUseItem, slowFall;
            public bool wet, honeyWet, lavaWet, shimmerWet, shimmering;
            public bool portalPhysicsFlag;
            public int _portalPhysicsTime, grapCount, dashDelay, eocDash;
            public bool CCed { get; set; }
            public bool tongued, dead, noItems, pulley, sliding, windPushed;
            public int itemAnimation, itemTime;
            public bool ghost, frozen, webbed, stoned, onTrack, vortexDebuff, sticky, dazed;
            private FakeItem QuickMount_GetItemToUse() => quickMountItem;
        }

        private static void True(bool value) { if (!value) throw new InvalidOperationException("expected true"); }
        private static void False(bool value) { if (value) throw new InvalidOperationException("expected false"); }
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"expected {expected}, actual {actual}");
        }
    }
}
