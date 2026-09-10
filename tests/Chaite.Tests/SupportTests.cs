using Chaite.Core;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSupportRegressions()
        {
            Run(nameof(SupportRequiresObservedPositiveInterval), SupportRequiresObservedPositiveInterval);
            Run(nameof(SupportFootingEndsAtActualPlatformEdge), SupportFootingEndsAtActualPlatformEdge);
            Run(nameof(SupportLandingRequiresVerifiedFootprint), SupportLandingRequiresVerifiedFootprint);
            Run(nameof(SupportLandingUsesExactSurfaceInsteadOfAirBounds), SupportLandingUsesExactSurfaceInsteadOfAirBounds);
            Run(nameof(SupportCannotCatchPlayerFromWrongSide), SupportCannotCatchPlayerFromWrongSide);
            Run(nameof(SupportDropsThroughPlatformsButNotSolids), SupportDropsThroughPlatformsButNotSolids);
            Run(nameof(SupportInvertedGravityUsesSolidUnderside), SupportInvertedGravityUsesSolidUnderside);
            Run(nameof(SupportLandingCrossingMatrix), SupportLandingCrossingMatrix);
            Run(nameof(SupportDiagonalCrossingCannotTeleportOntoLedge), SupportDiagonalCrossingCannotTeleportOntoLedge);
            Run(nameof(FacadeSupportSpanStopsAtPlatformEdge), FacadeSupportSpanStopsAtPlatformEdge);
            Run(nameof(FacadeSupportSpanDoesNotBridgeAirGaps), FacadeSupportSpanDoesNotBridgeAirGaps);
            Run(nameof(FacadeSupportExcludesSlopesHalfBricksAndActuatedTiles), FacadeSupportExcludesSlopesHalfBricksAndActuatedTiles);
            Run(nameof(FacadeSupportRefreshUsesBoundedCachedWork), FacadeSupportRefreshUsesBoundedCachedWork);
            Run(nameof(FacadeSupportRefreshesOnNewGroundContact), FacadeSupportRefreshesOnNewGroundContact);
            Run(nameof(FacadeInvertedSupportRejectsOneWayCeilings), FacadeInvertedSupportRejectsOneWayCeilings);
            Run(nameof(SupportRecoverySteersBackBeforeDropping), SupportRecoverySteersBackBeforeDropping);
            Run(nameof(SupportRecoveryPreservesFullFlightPattern), SupportRecoveryPreservesFullFlightPattern);
            Run(nameof(SupportRecoveryEndsAfterObservedGroundContact), SupportRecoveryEndsAfterObservedGroundContact);
            Run(nameof(SupportRecoveryCannotInventMissingTerrain), SupportRecoveryCannotInventMissingTerrain);
            Run(nameof(OrdinaryJumpNeverTriggersFlightRecovery), OrdinaryJumpNeverTriggersFlightRecovery);
            Run(nameof(RemovingFlightEquipmentClearsRecoveryIntent), RemovingFlightEquipmentClearsRecoveryIntent);
            Run(nameof(FacadeDistinguishesFiniteFlightFromDefaultRocketTimer), FacadeDistinguishesFiniteFlightFromDefaultRocketTimer);
        }

        private static SupportSpan FlatSupport(bool inverted = false, bool oneWay = false)
        {
            return new SupportSpan { Valid = true, Left = 100, Right = 200, SurfaceY = 300,
                Inverted = inverted, OneWay = oneWay };
        }

        private static void SupportRequiresObservedPositiveInterval()
        {
            var support = FlatSupport();
            True(support.ContainsBody(100, 20));
            True(support.ContainsBody(180, 20));
            False(support.ContainsBody(181, 20));
            support.Valid = false;
            False(support.ContainsBody(120, 20));
            False(support.OverlapsBody(120, 20));
            support.Valid = true;
            support.Right = support.Left;
            False(support.ContainsBody(100, 20));
            False(support.OverlapsBody(99, 20));
            support.Right = support.Left - 1;
            False(support.OverlapsBody(99, 20));
        }

        private static void SupportFootingEndsAtActualPlatformEdge()
        {
            var support = FlatSupport();
            True(SupportGeometry.RetainsFooting(support, 199, 20, false, false));
            False(SupportGeometry.RetainsFooting(support, 200, 20, false, false));
            True(SupportGeometry.RetainsFooting(support, 81, 20, false, false));
            False(SupportGeometry.RetainsFooting(support, 80, 20, false, false));
            False(SupportGeometry.RetainsFooting(support, 2400, 20, false, false));
        }

        private static void SupportLandingRequiresVerifiedFootprint()
        {
            foreach (var x in new[] { 80f, 81f, 99f, 181f, 199f, 200f, 2400f })
            {
                var position = new Vec2(x, 290);
                var velocity = new Vec2(3, 10);
                False(SupportGeometry.TryLand(FlatSupport(), new Vec2(x, 270), ref position, ref velocity,
                    20, 20, false, false));
                Equal(290f, position.Y);
                Equal(10f, velocity.Y);
            }
            var narrow = FlatSupport();
            narrow.Right = 116; // A one-tile ledge is not a promised full-width landing.
            var point = new Vec2(100, 290);
            var speed = new Vec2(0, 10);
            False(SupportGeometry.TryLand(narrow, new Vec2(100, 270), ref point, ref speed, 20, 20, false, false));
        }

        private static void SupportLandingUsesExactSurfaceInsteadOfAirBounds()
        {
            var position = new Vec2(140, 295);
            var velocity = new Vec2(7, 10);
            True(SupportGeometry.TryLand(FlatSupport(), new Vec2(140, 250), ref position, ref velocity,
                20, 42, false, false));
            Equal(258f, position.Y);
            Equal(140f, position.X);
            Equal(7f, velocity.X);
            Equal(0f, velocity.Y);
        }

        private static void SupportCannotCatchPlayerFromWrongSide()
        {
            foreach (var beforeY in new[] { 281f, 300f, 500f })
            {
                var position = new Vec2(120, beforeY + 10);
                var velocity = new Vec2(0, 10);
                False(SupportGeometry.TryLand(FlatSupport(), new Vec2(120, beforeY), ref position, ref velocity,
                    20, 20, false, false));
            }
            foreach (var speed in new[] { -10f, 0f })
            {
                var position = new Vec2(120, 290);
                var velocity = new Vec2(0, speed);
                False(SupportGeometry.TryLand(FlatSupport(), new Vec2(120, 270), ref position, ref velocity,
                    20, 20, false, false));
            }
        }

        private static void SupportDropsThroughPlatformsButNotSolids()
        {
            foreach (var oneWay in new[] { false, true })
            foreach (var drop in new[] { false, true })
            {
                var support = FlatSupport(oneWay: oneWay);
                var position = new Vec2(120, 290);
                var velocity = new Vec2(0, 10);
                var expected = !oneWay || !drop;
                Equal(expected, SupportGeometry.RetainsFooting(support, 120, 20, false, drop));
                Equal(expected, SupportGeometry.TryLand(support, new Vec2(120, 270), ref position, ref velocity,
                    20, 20, false, drop));
            }
        }

        private static void SupportInvertedGravityUsesSolidUnderside()
        {
            foreach (var oneWay in new[] { false, true })
            foreach (var drop in new[] { false, true })
            {
                var support = FlatSupport(inverted: true, oneWay: oneWay);
                var position = new Vec2(120, 290);
                var velocity = new Vec2(4, -10);
                Equal(!oneWay, SupportGeometry.TryLand(support, new Vec2(120, 310), ref position, ref velocity,
                    20, 42, true, drop));
                Equal(!oneWay, SupportGeometry.RetainsFooting(support, 120, 20, true, drop));
                if (!oneWay) { Equal(300f, position.Y); Equal(0f, velocity.Y); }
                False(SupportGeometry.RetainsFooting(support, 120, 20, false, drop));
            }
        }

        private static void SupportLandingCrossingMatrix()
        {
            // Ordinary/one-way, both gravity directions, drop control and all
            // footprint boundaries exercise the production helper, not a clone.
            foreach (var inverted in new[] { false, true })
            foreach (var oneWay in new[] { false, true })
            foreach (var drop in new[] { false, true })
            foreach (var x in new[] { 99f, 100f, 120f, 180f, 181f, 200f })
            {
                var support = FlatSupport(inverted, oneWay);
                var before = new Vec2(x, inverted ? 310 : 270);
                var position = new Vec2(x, 290);
                var velocity = new Vec2(0, inverted ? -10 : 10);
                var expected = x >= 100 && x <= 180 && (!oneWay || !inverted && !drop);
                Equal(expected, SupportGeometry.TryLand(support, before, ref position, ref velocity,
                    20, 20, inverted, drop));
            }
        }

        private static void FacadeSupportSpanStopsAtPlatformEdge()
        {
            var fixture = new SupportFacadeFixture();
            fixture.Row(240, 260, 150, true);
            var arena = fixture.Read();
            True(arena.FloorSupport.Valid);
            True(arena.FloorSupport.OneWay);
            Equal(240 * 16f, arena.FloorSupport.Left);
            Equal(261 * 16f, arena.FloorSupport.Right);
            Equal(150 * 16f, arena.FloorSupport.SurfaceY);
            False(arena.CeilingSupport.Valid);
            True(arena.LocalOpenBounds.Width > arena.FloorSupport.Right - arena.FloorSupport.Left,
                "fixture must expose the old open-air-as-floor regression");
        }

        private static void SupportDiagonalCrossingCannotTeleportOntoLedge()
        {
            foreach (var inverted in new[] { false, true })
            {
                var support = FlatSupport(inverted);
                var before = new Vec2(40, inverted ? 310 : 270);
                var position = new Vec2(140, inverted ? 270 : 310);
                var velocity = new Vec2(100, inverted ? -40 : 40);
                // At the actual vertical crossing X is still 65: the player
                // only moves under/over the platform after passing its plane.
                False(SupportGeometry.TryLand(support, before, ref position, ref velocity,
                    20, 20, inverted, false));
                before.X = 140;
                position.X = 160;
                True(SupportGeometry.TryLand(support, before, ref position, ref velocity,
                    20, 20, inverted, false));
            }
        }

        private static void FacadeSupportSpanDoesNotBridgeAirGaps()
        {
            var fixture = new SupportFacadeFixture();
            fixture.Row(240, 260, 150, true);
            fixture.Tiles[251, 150] = null;
            var arena = fixture.Read();
            True(arena.FloorSupport.Valid);
            Equal(240 * 16f, arena.FloorSupport.Left);
            Equal(251 * 16f, arena.FloorSupport.Right);
            False(arena.FloorSupport.ContainsBody(253 * 16f, 20));
        }

        private static void FacadeSupportExcludesSlopesHalfBricksAndActuatedTiles()
        {
            for (var kind = 0; kind < 4; kind++)
            {
                var fixture = new SupportFacadeFixture();
                fixture.Row(240, 260, 150, false);
                // Keep the center ordinary; a malformed adjacent support must
                // terminate the verified span instead of joining two surfaces.
                var tile = fixture.Tiles[252, 150];
                if (kind == 0) tile.Slope = 1;
                if (kind == 1) tile.Half = true;
                if (kind == 2) tile.Inactive = true;
                if (kind == 3) tile.Active = false;
                var arena = fixture.Read();
                True(arena.FloorSupport.Valid);
                Equal(252 * 16f, arena.FloorSupport.Right);
                False(arena.FloorSupport.OneWay);
            }
        }

        private static void FacadeSupportRefreshUsesBoundedCachedWork()
        {
            var fixture = new SupportFacadeFixture();
            fixture.Row(1, 498, 150, false);
            var first = fixture.Read();
            True(first.FloorSupport.Valid);
            True(first.FloorSupport.Right - first.FloorSupport.Left <= 301 * 16f,
                "support scan must be local, never the whole continuous world floor");
            True(fixture.TileReads < 5000, "bounded initial tile probes exceeded budget");
            var reads = fixture.TileReads;
            for (var index = 0; index < 10; index++) True(ReferenceEquals(first, fixture.Read()));
            Equal(reads, fixture.TileReads);
            fixture.Player.Position.X += 80f;
            False(ReferenceEquals(first, fixture.Read()));
            True(fixture.TileReads > reads);
            True(fixture.TileReads - reads < 5000, "bounded refresh tile probes exceeded budget");
        }

        private static void FacadeSupportRefreshesOnNewGroundContact()
        {
            var fixture = new SupportFacadeFixture();
            fixture.Row(240, 260, 150, true);
            fixture.Player.Position.Y = 150 * 16f - fixture.Player.Height - 8;
            var airborne = fixture.Read();
            fixture.Player.Position.Y += 8; // Below the ordinary 64-pixel invalidation distance.
            fixture.Player.OnGround = true;
            var landed = fixture.Read();
            False(ReferenceEquals(airborne, landed));
            True(landed.FloorSupport.OneWay);
        }

        private static void FacadeInvertedSupportRejectsOneWayCeilings()
        {
            foreach (var platform in new[] { false, true })
            {
                var fixture = new SupportFacadeFixture();
                fixture.Row(240, 260, 130, platform);
                var arena = fixture.Read(true);
                Equal(!platform, arena.CeilingSupport.Valid);
                if (!platform)
                {
                    True(arena.CeilingSupport.Inverted);
                    False(arena.CeilingSupport.OneWay);
                    Equal(131 * 16f, arena.CeilingSupport.SurfaceY);
                }
            }
        }

        private sealed class SupportTile
        {
            public ushort Type;
            public bool Active = true;
            public bool Inactive;
            public byte Slope;
            public bool Half;
        }

        private static CombatSnapshot RecoveryScene()
        {
            var snapshot = CombatScenario(4);
            snapshot.Player.Position = new Vec2(230, 220);
            snapshot.Player.Velocity = new Vec2(6, 2);
            snapshot.Player.OnGround = false;
            snapshot.Player.WingTime = 5;
            snapshot.Player.RocketTime = 0;
            snapshot.Mobility.FlightResourceFraction = .05f;
            snapshot.Arena.FloorSupport = FlatSupport(oneWay: true);
            snapshot.Arena.RecoverySupport = snapshot.Arena.FloorSupport;
            return snapshot;
        }

        private static void BudgetSupport(CombatPlanner planner, CombatSnapshot snapshot, ref int horizontal, ref int vertical)
        {
            var method = typeof(CombatPlanner).GetMethod("BudgetFlight", BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[] { snapshot, horizontal, vertical };
            method.Invoke(planner, arguments);
            horizontal = (int)arguments[1];
            vertical = (int)arguments[2];
        }

        private static void SupportRecoverySteersBackBeforeDropping()
        {
            var snapshot = RecoveryScene();
            var horizontal = 1;
            var vertical = -1;
            BudgetSupport(new CombatPlanner(new PlannerSettings()), snapshot, ref horizontal, ref vertical);
            Equal(-1, horizontal);
            Equal(0, vertical); // Do not hold Down through the intended one-way landing.
        }

        private static void SupportRecoveryPreservesFullFlightPattern()
        {
            var snapshot = RecoveryScene();
            snapshot.Player.WingTime = 1000;
            snapshot.Mobility.FlightResourceFraction = 1;
            var horizontal = 1;
            var vertical = 1;
            BudgetSupport(new CombatPlanner(new PlannerSettings()), snapshot, ref horizontal, ref vertical);
            Equal(1, horizontal);
            Equal(1, vertical);
        }

        private static void SupportRecoveryEndsAfterObservedGroundContact()
        {
            var snapshot = RecoveryScene();
            var planner = new CombatPlanner(new PlannerSettings());
            var horizontal = 1;
            var vertical = 1;
            BudgetSupport(planner, snapshot, ref horizontal, ref vertical);
            Equal(-1, horizontal);
            Equal(0, vertical);
            snapshot.Player.OnGround = true;
            snapshot.Player.Position = new Vec2(140, 258);
            horizontal = vertical = 1;
            BudgetSupport(planner, snapshot, ref horizontal, ref vertical);
            Equal(1, horizontal);
            Equal(1, vertical);
        }

        private static void SupportRecoveryCannotInventMissingTerrain()
        {
            var snapshot = RecoveryScene();
            snapshot.Arena.FloorSupport = default(SupportSpan);
            snapshot.Arena.RecoverySupport = default(SupportSpan);
            snapshot.Arena.HasFloor = true; // Legacy open-air metadata is NOT a support proof.
            var horizontal = 1;
            var vertical = 1;
            BudgetSupport(new CombatPlanner(new PlannerSettings()), snapshot, ref horizontal, ref vertical);
            Equal(1, horizontal);
            Equal(0, vertical);
        }

        private static void OrdinaryJumpNeverTriggersFlightRecovery()
        {
            foreach (var horizontalIntent in new[] { -1, 0, 1 })
            foreach (var verticalIntent in new[] { -1, 0, 1 })
            foreach (var x in new[] { 120f, 230f })
            {
                var snapshot = RecoveryScene();
                snapshot.Player.Position.X = x;
                snapshot.Player.WingTime = snapshot.Player.RocketTime = 0;
                snapshot.Mobility.HasFiniteFlightResource = false;
                snapshot.Mobility.FlightResourceFraction = 0;
                var horizontal = horizontalIntent;
                var vertical = verticalIntent;
                BudgetSupport(new CombatPlanner(new PlannerSettings()), snapshot, ref horizontal, ref vertical);
                Equal(horizontalIntent, horizontal);
                Equal(verticalIntent, vertical);
            }
        }

        private static void RemovingFlightEquipmentClearsRecoveryIntent()
        {
            var snapshot = RecoveryScene();
            var planner = new CombatPlanner(new PlannerSettings());
            var horizontal = 1;
            var vertical = 1;
            BudgetSupport(planner, snapshot, ref horizontal, ref vertical);
            Equal(-1, horizontal);
            Equal(0, vertical);
            snapshot.Mobility.HasFiniteFlightResource = false;
            horizontal = vertical = 1;
            BudgetSupport(planner, snapshot, ref horizontal, ref vertical);
            Equal(1, horizontal);
            Equal(1, vertical);
            var field = typeof(CombatPlanner).GetField("_restoringFlight", BindingFlags.Instance | BindingFlags.NonPublic);
            False((bool)field.GetValue(planner));
        }

        private static void FacadeDistinguishesFiniteFlightFromDefaultRocketTimer()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(type);
            var wings = 0;
            var wingMax = 0;
            var boots = 0;
            var rocketMax = 7; // Vanilla constructor initializes this even without boots.
            Action<string, Func<object, int>> bind = (name, getter) =>
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "finite flight fixture binding missing: " + name);
                field.SetValue(facade, getter);
            };
            bind("_playerWingsLogic", player => wings);
            bind("_playerWingTimeMax", player => wingMax);
            bind("_playerRocketBoots", player => boots);
            bind("_playerRocketTimeMax", player => rocketMax);
            var method = type.GetMethod("HasFiniteFlightResource", BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null);
            Func<bool> hasFlight = () => (bool)method.Invoke(facade, new object[] { new object() });
            False(hasFlight());
            wingMax = 120;
            False(hasFlight());
            wings = 1;
            True(hasFlight());
            wingMax = 0;
            False(hasFlight());
            boots = 1;
            True(hasFlight());
            rocketMax = 0;
            False(hasFlight());
            // Capability is independent of the remaining counters; a genuinely
            // exhausted equipped wing must still enter its restoration loop.
            wingMax = 120;
            True(hasFlight());
        }

        private sealed class SupportFacadeFixture
        {
            private readonly object _facade;
            private readonly Type _type;
            public readonly SupportTile[,] Tiles = new SupportTile[500, 300];
            public readonly PlayerSnapshot Player = new PlayerSnapshot
            {
                Position = new Vec2(250 * 16f - 2, 140 * 16f - 13), Width = 20, Height = 42,
                Gravity = .4f, MaxFallSpeed = 10f, WorldLeft = 16f, WorldRight = 7984f,
                WorldTop = 16f, WorldBottom = 4784f
            };
            public int TileReads;

            public SupportFacadeFixture()
            {
                _type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
                _facade = FormatterServices.GetUninitializedObject(_type);
                Bind("_tiles", new Func<Array>(() => Tiles));
                Bind("_maxTilesX", new Func<int>(() => Tiles.GetLength(0)));
                Bind("_maxTilesY", new Func<int>(() => Tiles.GetLength(1)));
                Bind("_tileAt", new Func<Array, int, int, object>((array, x, y) =>
                { TileReads++; return array.GetValue(x, y); }));
                Bind("_tileType", new Func<object, ushort>(tile => ((SupportTile)tile).Type));
                Bind("_tileActive", new Func<object, bool>(tile => ((SupportTile)tile).Active));
                Bind("_tileInactive", new Func<object, bool>(tile => ((SupportTile)tile).Inactive));
                Bind("_tileSlope", new Func<object, byte>(tile => ((SupportTile)tile).Slope));
                Bind("_tileHalfBrick", new Func<object, bool>(tile => ((SupportTile)tile).Half));
                var solid = new bool[40];
                var oneWay = new bool[40];
                solid[19] = solid[38] = oneWay[19] = true;
                Bind("_tileSolid", solid);
                Bind("_tileSolidTop", oneWay);
            }

            public void Row(int left, int right, int y, bool platform)
            {
                for (var x = left; x <= right; x++) Tiles[x, y] = new SupportTile { Type = (ushort)(platform ? 19 : 38) };
            }

            public ArenaSnapshot Read(bool inverted = false)
            {
                var method = _type.GetMethod("ReadArena", BindingFlags.Instance | BindingFlags.NonPublic);
                True(method != null);
                return (ArenaSnapshot)method.Invoke(_facade, new object[] { Player, inverted });
            }

            private void Bind(string name, object value)
            {
                var field = _type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "support fixture binding missing: " + name);
                field.SetValue(_facade, value);
            }
        }
    }
}
