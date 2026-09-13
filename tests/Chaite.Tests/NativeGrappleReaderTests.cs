using System;
using System.Collections.Generic;
using System.Reflection;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class NativeGrappleReaderTests
    {
        private delegate BasicHookFrameSnapshot ReadHook(object player, long sequence,
            bool grounded);
        private delegate bool ReadTile(int x, int y,
            out BasicHookAnchorObservation tile);
        private delegate bool Sweep(in RectF before, in RectF after,
            out bool pathClear, out bool platformFree);

        public static int RunAll()
        {
            ExactQuickGrappleItemAndProjectileDefaultsAreRequired();
            LiveProjectileLinkAndAnchorComeFromNativeSlots();
            OtherHooksAndAmbiguousProjectilesFailClosed();
            ConservativeStartGateRejectsConflictingMovementStates();
            WorldEvidenceUsesNactiveShapePlatformTrackAndBlacklist();
            ReaderDoesNotMutateStateOrAllocateOnTheHotPath();
            return 6;
        }

        private static void ExactQuickGrappleItemAndProjectileDefaultsAreRequired()
        {
            var adapter = Adapter.Create();
            var player = ExactPlayer();
            var frame = adapter.Read(player, 10, true);
            True(frame.Known);
            True(BasicHookMotion.MatchesExactIdentity(in frame.Identity));
            True(frame.Context.Known && frame.Context.LocalPlayerKnown);
            True(frame.Context.ItemStartGateKnown && frame.Context.ItemStartGateOpen);
            Equal(0, frame.Context.LocalPlayerIndex);
            Equal(0, frame.Link.GrappleCount);
            True(frame.Link.Known && frame.Link.AtGrappleMovementEntry);
            False(frame.ProjectileObserved);
            Near(110f, frame.PlayerCenter.X);
            Near(221f, frame.PlayerCenter.Y);

            player.hook.shootSpeed = 12f;
            frame = adapter.Read(player, 11, true);
            False(BasicHookMotion.MatchesExactIdentity(in frame.Identity));
            False(frame.Context.ItemStartGateOpen);
            player.hook.shootSpeed = 11.5f;
            FakeContentSamples.ProjectilesByType[13].tileCollide = true;
            frame = adapter.Read(player, 12, true);
            False(BasicHookMotion.MatchesExactIdentity(in frame.Identity));
            False(frame.Context.ItemStartGateOpen);
        }

        private static void LiveProjectileLinkAndAnchorComeFromNativeSlots()
        {
            var adapter = Adapter.Create();
            var player = ExactPlayer();
            var projectile = ExactProjectile();
            projectile.active = true;
            projectile.owner = 0;
            projectile.position = new FakeVector(10 * 16f + 8f - 9f,
                8 * 16f + 8f - 9f);
            FakeMain.projectile[7] = projectile;

            var outbound = adapter.Read(player, 20, false);
            True(outbound.ProjectileObserved);
            Equal(7, outbound.Projectile.Index);
            Equal(BasicHookProjectilePhase.Outbound,
                Classify(in outbound.Projectile, 0));
            False(outbound.Anchor.Known);

            projectile.ai[0] = 2f;
            player.grapCount = 1;
            player.grappling[0] = 7;
            FakeMain.tile[10, 8].activeValue = true;
            FakeMain.tile[10, 8].type = 1;
            FakeMain.tileSolid[1] = true;
            var latched = adapter.Read(player, 21, false);
            True(latched.ProjectileObserved);
            True(latched.Anchor.Known);
            True(BasicHookMotion.IsSafetyAnchor(in latched.Anchor));
            Equal(10, latched.Anchor.TileX);
            Equal(8, latched.Anchor.TileY);
            Equal(7, latched.Link.FirstProjectileIndex);
            True(latched.Link.AtGrappleMovementEntry);
        }

        private static void OtherHooksAndAmbiguousProjectilesFailClosed()
        {
            var adapter = Adapter.Create();
            var player = ExactPlayer();
            player.hook.type = 1234;
            player.hook.shoot = 14;
            FakeMain.projHook[14] = true;
            FakeContentSamples.ProjectilesByType[14] = ExactProjectile();
            FakeContentSamples.ProjectilesByType[14].type = 14;
            var other = adapter.Read(player, 30, true);
            False(other.Identity.Known);
            False(other.Context.ItemStartGateOpen);

            player = ExactPlayer();
            for (var slot = 3; slot <= 4; slot++)
            {
                var projectile = ExactProjectile();
                projectile.active = true;
                projectile.owner = 0;
                projectile.position = new FakeVector(160f + slot, 120f);
                FakeMain.projectile[slot] = projectile;
            }
            var ambiguous = adapter.Read(player, 31, true);
            False(ambiguous.Known);
        }

        private static void ConservativeStartGateRejectsConflictingMovementStates()
        {
            var adapter = Adapter.Create();
            foreach (var mutation in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                var player = ExactPlayer();
                if (mutation == 0) FakePlayerInput.GrappleAndInteractAreShared = true;
                else if (mutation == 1) FakeMain.netMode = 1;
                else if (mutation == 2) player.releaseJump = false;
                else if (mutation == 3) player.releaseHook = false;
                else if (mutation == 4) player.itemAnimation = 1;
                else if (mutation == 5) player.itemTime = 1;
                else if (mutation == 6) player.mount.Active = true;
                else if (mutation == 7) player.noItems = true;
                else player._quickGrappleCooldown = 1;
                var frame = adapter.Read(player, 40 + mutation, true);
                False(frame.Context.ItemStartGateOpen,
                    "unsafe start mutation accepted: " + mutation);
                FakePlayerInput.GrappleAndInteractAreShared = false;
                FakeMain.netMode = 0;
            }

            var gravity = ExactPlayer();
            gravity.gravDir = -1f;
            var inverted = adapter.Read(gravity, 60, false);
            False(inverted.Context.NormalGravity);
            var wet = ExactPlayer();
            wet.wet = true;
            var submerged = adapter.Read(wet, 61, false);
            True(submerged.Context.Wet);
        }

        private static void WorldEvidenceUsesNactiveShapePlatformTrackAndBlacklist()
        {
            var adapter = Adapter.Create();
            var player = ExactPlayer();
            adapter.Prepare(player);
            var native = FakeMain.tile[4, 5];
            native.activeValue = true;
            native.type = 1;
            FakeMain.tileSolid[1] = true;
            BasicHookAnchorObservation tile;
            True(adapter.Tile(4, 5, out tile));
            True(tile.NativeActive && tile.NativeSolid);
            True(BasicHookMotion.IsSafetyAnchor(in tile));

            native.inactiveValue = true;
            True(adapter.Tile(4, 5, out tile));
            False(tile.NativeActive);
            native.inactiveValue = false;
            native.slopeValue = 1;
            True(adapter.Tile(4, 5, out tile));
            True(tile.Shaped);
            True(BasicHookMotion.NativeCanLatch(in tile));
            False(BasicHookMotion.IsSafetyAnchor(in tile));
            native.slopeValue = 0;

            player.blacklistX = 4;
            player.blacklistY = 5;
            True(adapter.Tile(4, 5, out tile));
            True(tile.Blacklisted);
            False(BasicHookMotion.NativeCanLatch(in tile));
            player.blacklistX = player.blacklistY = -1;

            FakeMain.tile[6, 5].activeValue = true;
            FakeMain.tile[6, 5].type = 2;
            FakeMain.tileSolid[2] = true;
            FakeMain.tileSolidTop[2] = true;
            True(adapter.Tile(6, 5, out tile));
            True(tile.Platform && tile.SolidTop);

            FakeMain.tile[7, 5].activeValue = true;
            FakeMain.tile[7, 5].type = 314;
            True(adapter.Tile(7, 5, out tile));
            True(tile.MinecartTrack);

            bool clear;
            bool platforms;
            var before = new RectF(3 * 16f, 4 * 16f, 10f, 10f);
            var after = new RectF(4 * 16f, 5 * 16f, 10f, 10f);
            True(adapter.Sweep(in before, in after, out clear, out platforms));
            False(clear);
            True(platforms);
            before = new RectF(5 * 16f, 5 * 16f, 10f, 10f);
            after = new RectF(6 * 16f, 5 * 16f, 10f, 10f);
            True(adapter.Sweep(in before, in after, out clear, out platforms));
            False(clear);
            False(platforms);
        }

        private static void ReaderDoesNotMutateStateOrAllocateOnTheHotPath()
        {
            var adapter = Adapter.Create();
            var player = ExactPlayer();
            var hook = player.hook;
            var centerBefore = player.position;
            var checksum = 0f;
            for (var index = 0; index < 1000; index++)
                checksum += adapter.Read(player, 100 + index, true).PlayerCenter.X;
            GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 10000; index++)
                checksum += adapter.Read(player, 2000 + index, true).PlayerCenter.X;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            True(checksum > 0f);
            Equal(0L, allocated);
            True(ReferenceEquals(hook, player.hook));
            Near(centerBefore.X, player.position.X);
            Near(centerBefore.Y, player.position.Y);
            Equal(0, player.itemAnimation);
            Equal(0, player.itemTime);
        }

        private static BasicHookProjectilePhase Classify(
            in BasicHookProjectileObservation projectile, int player)
        {
            BasicHookFailure failure;
            return BasicHookMotion.ClassifyProjectile(in projectile, player, out failure);
        }

        private static FakePlayer ExactPlayer()
        {
            return new FakePlayer
            {
                active = true,
                whoAmI = 0,
                width = 20,
                height = 42,
                position = new FakeVector(100f, 200f),
                velocity = new FakeVector(1f, -2f),
                releaseHook = true,
                releaseJump = true,
                gravDir = 1f,
                grappling = new[] { -1 },
                hook = new FakeItem
                {
                    type = 84,
                    stack = 1,
                    shoot = 13,
                    shootSpeed = 11.5f,
                    useStyle = 5,
                    useAnimation = 20,
                    useTime = 20,
                    noUseGraphic = true,
                    noMelee = true
                }
            };
        }

        private static FakeProjectile ExactProjectile()
        {
            return new FakeProjectile
            {
                type = 13,
                owner = -1,
                width = 18,
                height = 18,
                aiStyle = 7,
                ai = new float[2],
                netImportant = true,
                tileCollide = false
            };
        }

        private static void ResetNative()
        {
            FakeMain.myPlayer = 0;
            FakeMain.netMode = 0;
            FakeMain.maxTilesX = 64;
            FakeMain.maxTilesY = 64;
            FakeMain.projectile = new FakeProjectile[32];
            FakeMain.tile = new FakeTile[64, 64];
            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 64; y++) FakeMain.tile[x, y] = new FakeTile();
            FakeMain.projHook = new bool[400];
            FakeMain.projHook[13] = true;
            FakeMain.tileSolid = new bool[400];
            FakeMain.tileSolidTop = new bool[400];
            FakePlayerInput.GrappleAndInteractAreShared = false;
            FakeContentSamples.ProjectilesByType = new Dictionary<int, FakeProjectile>
            {
                [13] = ExactProjectile()
            };
        }

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private static void True(bool value, string message = null)
        {
            if (!value) throw new InvalidOperationException(message ?? "expected true");
        }

        private static void False(bool value, string message = null) => True(!value, message);

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private sealed class Adapter
        {
            private readonly object _reader;
            public readonly ReadHook Read;
            public readonly Action<object> Prepare;
            public readonly ReadTile Tile;
            public readonly Sweep Sweep;

            private Adapter(object reader, Type type)
            {
                _reader = reader;
                Read = (ReadHook)Delegate.CreateDelegate(typeof(ReadHook), reader,
                    type.GetMethod("ReadAtPlayerUpdateEntry"));
                Prepare = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>),
                    reader, type.GetMethod("PrepareWorldEvidence"));
                Tile = (ReadTile)Delegate.CreateDelegate(typeof(ReadTile), reader,
                    type.GetMethod("TryReadTile"));
                Sweep = (Sweep)Delegate.CreateDelegate(typeof(Sweep), reader,
                    type.GetMethod("TrySweepPlayer"));
            }

            public static Adapter Create()
            {
                ResetNative();
                var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                    "Chaite.Plugin.NativeGrappleReader", true);
                var arguments = new object[]
                {
                    typeof(FakeMain), typeof(FakePlayer), typeof(FakeEntity),
                    typeof(FakeProjectile), typeof(FakeItem), typeof(FakeTile),
                    typeof(FakeMount), typeof(FakePlayerInput), typeof(FakeContentSamples)
                };
                var reader = Activator.CreateInstance(type,
                    BindingFlags.Instance | BindingFlags.NonPublic, null, arguments, null);
                return new Adapter(reader, type);
            }
        }

#pragma warning disable CS0649
        private struct FakeVector
        {
            public float X;
            public float Y;
            public FakeVector(float x, float y) { X = x; Y = y; }
        }

        private struct FakePoint
        {
            public int X;
            public int Y;
            public FakePoint(int x, int y) { X = x; Y = y; }
        }

        private class FakeEntity
        {
            public int whoAmI, width, height;
            public bool wet;
            public FakeVector position, velocity;
        }

        private sealed class FakeProjectile : FakeEntity
        {
            public bool active, netImportant, tileCollide;
            public int owner, type, aiStyle;
            public float[] ai;
        }

        private sealed class FakeItem
        {
            public int type, stack, shoot, useStyle, useAnimation, useTime;
            public float shootSpeed;
            public bool noUseGraphic, noMelee;
        }

        private sealed class FakeTile
        {
            public ushort type;
            public bool activeValue, inactiveValue, halfBrickValue;
            public byte slopeValue;
            public bool nactive() => activeValue && !inactiveValue;
            public byte slope() => slopeValue;
            public bool halfBrick() => halfBrickValue;
        }

        private sealed class FakeMount
        {
            public bool Active { get; set; }
        }

        private sealed class FakePlayer : FakeEntity
        {
            public bool active, dead, tongued, noItems, cursed, pulley;
            public bool releaseHook, releaseJump, gravControl, gravControl2, slowFall;
            public bool crowdControlled;
            public int grapCount, itemAnimation, itemTime, _quickGrappleCooldown;
            public float gravDir;
            public int[] grappling;
            public FakeMount mount = new FakeMount();
            public FakeItem hook;
            public int blacklistX = -1, blacklistY = -1;
            public bool CCed => crowdControlled;
            public FakeItem QuickGrapple_GetItemToUse() => hook;
            public bool IsBlacklistedForGrappling(FakePoint point)
                => point.X == blacklistX && point.Y == blacklistY;
        }

        private static class FakePlayerInput
        {
            public static bool GrappleAndInteractAreShared;
        }

        private static class FakeContentSamples
        {
            public static Dictionary<int, FakeProjectile> ProjectilesByType;
        }

        private static class FakeMain
        {
            public static int myPlayer, netMode, maxTilesX, maxTilesY;
            public static FakeProjectile[] projectile;
            public static FakeTile[,] tile;
            public static bool[] projHook, tileSolid, tileSolidTop;
        }
#pragma warning restore CS0649
    }
}
