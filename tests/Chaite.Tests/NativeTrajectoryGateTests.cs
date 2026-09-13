using Chaite.Core;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunNativeTrajectoryGateRegressions()
        {
            Run(nameof(NativeTrajectoryGatePinsReviewedLiquidPrefixes),
                NativeTrajectoryGatePinsReviewedLiquidPrefixes);
            Run(nameof(NativeTrajectoryGatePinsAquaLavaOnlyPath),
                NativeTrajectoryGatePinsAquaLavaOnlyPath);
            Run(nameof(NativeTrajectoryGateExcludesRazorpineNativeSpread),
                NativeTrajectoryGateExcludesRazorpineNativeSpread);
            Run(nameof(AquaLavaGateUsesExactDescendingCenterTile),
                AquaLavaGateUsesExactDescendingCenterTile);
            Run(nameof(ReviewedTrajectoryGateIsBoundedAndFailsClosed),
                ReviewedTrajectoryGateIsBoundedAndFailsClosed);
            Run(nameof(NativeTrajectoryGateRefusesUnlistedOrMalformedPaths),
                NativeTrajectoryGateRefusesUnlistedOrMalformedPaths);
        }

        private static void NativeTrajectoryGatePinsReviewedLiquidPrefixes()
        {
            var pairs = new[]
            {
                new[] { 112, 15 },
                new[] { 519, 95 },
                new[] { 1264, 253 }
            };
            var speeds = new[] { 7.5f, 10f, 9f };
            for (var index = 0; index < pairs.Length; index++)
            {
                NativeTrajectoryGateProfile profile;
                True(NativeTrajectoryGateCatalog.TryGet(pairs[index][0],
                    pairs[index][1], out profile));
                True(profile.IsSpecified);
                Equal(NativeTrajectoryGatePathKind.StraightPrefix,
                    profile.PathKind);
                Equal(NativeTrajectoryGateLiquidPolicy.AllLiquids,
                    profile.LiquidPolicy);
                Equal(19, profile.MaximumSubupdates);
                Equal(16, profile.ProjectileWidthPixels);
                Equal(16, profile.ProjectileHeightPixels);
                NearWeapon(speeds[index],
                    profile.InitialSpeedPixelsPerSubupdate);
                Equal(19, profile.MaximumSamples);
                Equal(171, profile.MaximumTileReads);

                var position = new Vec2();
                var velocity = new Vec2(profile.
                    InitialSpeedPixelsPerSubupdate, 0f);
                for (var update = 1; update <= profile.MaximumSubupdates;
                    update++)
                    True(NativeTrajectoryGateCatalog.TryAdvance(ref position,
                        ref velocity, update, profile));
                NearWeapon(speeds[index] * 19f, position.X);
                NearWeapon(0f, position.Y);
                NearWeapon(speeds[index], velocity.X);
                NearWeapon(0f, velocity.Y);
            }
        }

        private static void NativeTrajectoryGatePinsAquaLavaOnlyPath()
        {
            NativeTrajectoryGateProfile profile;
            True(NativeTrajectoryGateCatalog.TryGet(157, 22, out profile));
            Equal(NativeTrajectoryGatePathKind.DiscreteVerticalAcceleration,
                profile.PathKind);
            Equal(NativeTrajectoryGateLiquidPolicy.LavaOnly,
                profile.LiquidPolicy);
            Equal(100, profile.MaximumSubupdates);
            Equal(18, profile.ProjectileWidthPixels);
            Equal(18, profile.ProjectileHeightPixels);
            Equal(4, profile.VerticalAccelerationDelaySubupdates);
            NearWeapon(12.5f, profile.InitialSpeedPixelsPerSubupdate);
            NearWeapon(.15f, profile.VerticalAccelerationPerSubupdate);
            Equal(100, profile.MaximumSamples);
            Equal(100, profile.MaximumTileReads);
            True(NativeTrajectoryGateCatalog.RequiresGate(157, 22));
        }

        private static void NativeTrajectoryGateExcludesRazorpineNativeSpread()
        {
            NativeTrajectoryGateProfile profile;
            False(NativeTrajectoryGateCatalog.TryGet(1930, 336,
                out profile));
            False(NativeTrajectoryGateCatalog.RequiresGate(1930, 336));
            False(profile.IsSpecified);
        }

        private static void AquaLavaGateUsesExactDescendingCenterTile()
        {
            var fixture = new NativeTrajectoryFacadeFixture();
            NativeTrajectoryGateProfile profile;
            True(NativeTrajectoryGateCatalog.TryGet(157, 22, out profile));

            fixture.Tiles[7, 9].Lava = true;
            True(fixture.CellSafe(new Vec2(127.75f, 159.75f),
                new Vec2(0f, -0.01f), profile));
            Equal(0, fixture.TileReads);

            False(fixture.CellSafe(new Vec2(127.75f, 159.75f),
                new Vec2(0f, 0.01f), profile));
            Equal(1, fixture.TileReads);
            Equal(7, fixture.LastTileX);
            Equal(9, fixture.LastTileY);

            // The production entry point must use ItemCheck_Shoot's exact
            // pointPosition as projectile Center, with no half-size offset.
            fixture = new NativeTrajectoryFacadeFixture();
            fixture.FailAtRead = 1;
            False(fixture.TrajectorySafe(new Vec2(127.75f, 159.75f),
                new Vec2(127.75f, 300f), 157, 22));
            Equal(1, fixture.TileReads);
            Equal(7, fixture.LastTileX);
            Equal(9, fixture.LastTileY);
        }

        private static void ReviewedTrajectoryGateIsBoundedAndFailsClosed()
        {
            NativeTrajectoryGateProfile profile;
            True(NativeTrajectoryGateCatalog.TryGet(112, 15, out profile));
            var fixture = new NativeTrajectoryFacadeFixture();
            True(fixture.TrajectorySafe(new Vec2(160f, 160f),
                new Vec2(300f, 160f), 112, 15));
            True(fixture.TileReads <= profile.MaximumTileReads,
                "tile reads exceeded declared bound: " + fixture.TileReads);

            fixture = new NativeTrajectoryFacadeFixture();
            fixture.Tiles[9, 9].liquid = 1;
            False(fixture.TrajectorySafe(new Vec2(160f, 160f),
                new Vec2(300f, 160f), 112, 15));

            fixture = new NativeTrajectoryFacadeFixture();
            fixture.FailAtRead = 1;
            False(fixture.TrajectorySafe(new Vec2(160f, 160f),
                new Vec2(300f, 160f), 112, 15));
            Equal(1, fixture.TileReads);
        }

        private static void NativeTrajectoryGateRefusesUnlistedOrMalformedPaths()
        {
            NativeTrajectoryGateProfile profile;
            False(NativeTrajectoryGateCatalog.TryGet(112, 16, out profile));
            False(NativeTrajectoryGateCatalog.TryGet(157, 23, out profile));
            False(NativeTrajectoryGateCatalog.RequiresGate(759, 134));
            False(profile.IsSpecified);

            profile = new NativeTrajectoryGateProfile();
            var position = new Vec2();
            var velocity = new Vec2(1f, 0f);
            False(NativeTrajectoryGateCatalog.TryAdvance(ref position,
                ref velocity, 1, profile));
        }

        private sealed class NativeTrajectoryTile
        {
            public byte liquid;
            public bool Lava;
        }

        private sealed class NativeTrajectoryFacadeFixture
        {
            private readonly object _facade;
            private readonly MethodInfo _cellSafe;
            private readonly MethodInfo _trajectorySafe;
            public readonly NativeTrajectoryTile[,] Tiles =
                new NativeTrajectoryTile[80, 80];
            public int TileReads;
            public int LastTileX = -1;
            public int LastTileY = -1;
            public int FailAtRead = -1;

            public NativeTrajectoryFacadeFixture()
            {
                for (var x = 0; x < Tiles.GetLength(0); x++)
                for (var y = 0; y < Tiles.GetLength(1); y++)
                    Tiles[x, y] = new NativeTrajectoryTile();

                var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                    "Chaite.Plugin.TerrariaFacade", true);
                _facade = FormatterServices.GetUninitializedObject(type);
                Bind(type, "_tiles", new Func<Array>(() => Tiles));
                Bind(type, "_maxTilesX", new Func<int>(() =>
                    Tiles.GetLength(0)));
                Bind(type, "_maxTilesY", new Func<int>(() =>
                    Tiles.GetLength(1)));
                Bind(type, "_razorbladeSpawnCenter",
                    new Func<object, Vec2>(player => (Vec2)player));
                Bind(type, "_tileAt",
                    new Func<Array, int, int, object>((tiles, x, y) =>
                    {
                        TileReads++;
                        LastTileX = x;
                        LastTileY = y;
                        if (TileReads == FailAtRead) return null;
                        return ((NativeTrajectoryTile[,])tiles)[x, y];
                    }));
                Bind(type, "_tileLava", new Func<object, bool>(tile =>
                    ((NativeTrajectoryTile)tile).Lava));
                Bind(type, "_liquidField", typeof(NativeTrajectoryTile).
                    GetField("liquid"));
                _cellSafe = type.GetMethod(
                    "IsReviewedTrajectoryCellSafe",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _trajectorySafe = type.GetMethod("IsReviewedDryTrajectory",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(_cellSafe != null && _trajectorySafe != null);
            }

            public bool CellSafe(Vec2 center, Vec2 velocity,
                NativeTrajectoryGateProfile profile) =>
                (bool)_cellSafe.Invoke(_facade, new object[] {
                    Tiles, center, velocity, profile });

            public bool TrajectorySafe(Vec2 origin, Vec2 aim, int weaponId,
                int projectileId) => (bool)_trajectorySafe.Invoke(_facade,
                    new object[] { origin, aim, weaponId, projectileId });

            private void Bind(Type type, string name, object value)
            {
                var field = type.GetField(name, BindingFlags.Instance |
                    BindingFlags.NonPublic);
                True(field != null, "missing trajectory fixture field " +
                    name);
                field.SetValue(_facade, value);
            }
        }
    }
}
