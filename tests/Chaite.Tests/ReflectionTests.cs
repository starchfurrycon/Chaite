using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunReflectionRegressions()
        {
            Run(nameof(CompiledFieldAccessReadsAndWritesInheritedFields), CompiledFieldAccessReadsAndWritesInheritedFields);
            Run(nameof(CompiledStaticAndPropertyWritesHaveCorrectTargets), CompiledStaticAndPropertyWritesHaveCorrectTargets);
            Run(nameof(CompiledVectorAccessReadsLiveComponents), CompiledVectorAccessReadsLiveComponents);
            Run(nameof(CompiledTileArrayReaderHonorsBothCoordinates), CompiledTileArrayReaderHonorsBothCoordinates);
            Run(nameof(CompiledPrivateMethodGetterPassesArgumentWithoutMutation), CompiledPrivateMethodGetterPassesArgumentWithoutMutation);
            Run(nameof(CompiledStaticDictionaryReaderHandlesHitsMissingAndNull), CompiledStaticDictionaryReaderHandlesHitsMissingAndNull);
            Run(nameof(MissingReflectionMembersFailExplicitly), MissingReflectionMembersFailExplicitly);
            Run(nameof(HotkeyEdgesOnlyFireOncePerPress), HotkeyEdgesOnlyFireOncePerPress);
            Run(nameof(HotkeysOutsideForegroundDoNotActivate), HotkeysOutsideForegroundDoNotActivate);
            Run(nameof(HeldActivationDoesNotRearmOnFocusReturn), HeldActivationDoesNotRearmOnFocusReturn);
            Run(nameof(CancelWinsOverSimultaneousActivation), CancelWinsOverSimultaneousActivation);
            Run(nameof(StructSelectionMethodMutatesOriginalOwnerWithoutBoxingCopy), StructSelectionMethodMutatesOriginalOwnerWithoutBoxingCopy);
            Run(nameof(FacadeCaptureReplayPreservesDelayedSlotRequest), FacadeCaptureReplayPreservesDelayedSlotRequest);
            Run(nameof(FacadeSelectionRequestDoesNotLeakAcrossFrames), FacadeSelectionRequestDoesNotLeakAcrossFrames);
            Run(nameof(FacadeWithoutRequestFollowsActualSelectedSlot), FacadeWithoutRequestFollowsActualSelectedSlot);
            Run(nameof(FacadeAimPreservesWorldTargetWithNormalGravity), FacadeAimPreservesWorldTargetWithNormalGravity);
            Run(nameof(FacadeAimPreservesWorldTargetWithInvertedGravity), FacadeAimPreservesWorldTargetWithInvertedGravity);
            Run(nameof(FacadeMinisharkUsesAmmoSpeedAndProjectileSubupdates), FacadeMinisharkUsesAmmoSpeedAndProjectileSubupdates);
            Run(nameof(FacadeClockworkUsesNativeSelectedAmmoWithoutConsumption), FacadeClockworkUsesNativeSelectedAmmoWithoutConsumption);
            Run(nameof(FacadeWeaponReadUsesLiveProjectileSample), FacadeWeaponReadUsesLiveProjectileSample);
            Run(nameof(FacadeWeaponReadRejectsMissingNativeAmmo), FacadeWeaponReadRejectsMissingNativeAmmo);
            Run(nameof(FacadeWeaponSelectionDoesNotPreferHighDpsPureMelee), FacadeWeaponSelectionDoesNotPreferHighDpsPureMelee);
            Run(nameof(FacadeWeaponProfilesUseDamageModifiersAndBurstPhase), FacadeWeaponProfilesUseDamageModifiersAndBurstPhase);
            Run(nameof(FacadeWeaponProfilesResetOnAmmoAndWeaponChanges), FacadeWeaponProfilesResetOnAmmoAndWeaponChanges);
            Run(nameof(PlannerRefusesUnsupportedNativeCombinationBeforeSummoning), PlannerRefusesUnsupportedNativeCombinationBeforeSummoning);
            Run(nameof(PlannerWeaponAimUsesExactProfileAndRejectsImpossibleIntercept), PlannerWeaponAimUsesExactProfileAndRejectsImpossibleIntercept);
            Run(nameof(JumpGateReleasesGroundedHoldBeforeFreshPress), JumpGateReleasesGroundedHoldBeforeFreshPress);
            Run(nameof(JumpGatePreservesAirborneAndGrappleHolds), JumpGatePreservesAirborneAndGrappleHolds);
            Run(nameof(FacadePlatformsRemainOneWayWithBothSolidFlags), FacadePlatformsRemainOneWayWithBothSolidFlags);
            Run(nameof(TargetSightCacheExpiresAfterTwelveFrames), TargetSightCacheExpiresAfterTwelveFrames);
            Run(nameof(TargetSightCacheInvalidatesWhenEitherEndpointMoves), TargetSightCacheInvalidatesWhenEitherEndpointMoves);
            Run(nameof(TargetSightCacheSeparatesKeysTypesAndClears), TargetSightCacheSeparatesKeysTypesAndClears);
            Run(nameof(FacadeInputFrameRenewsSightBudgetWithoutClearingCache), FacadeInputFrameRenewsSightBudgetWithoutClearingCache);
        }

        private static Type ReflectionApi => typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.ReflectionAccess", true);

        private static TDelegate GenericAccessor<TDelegate>(string name, Type valueType, params object[] arguments)
        {
            var method = ReflectionApi.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null, "missing reflection accessor: " + name);
            return (TDelegate)method.MakeGenericMethod(valueType).Invoke(null, arguments);
        }

        private static TDelegate Accessor<TDelegate>(string name, params object[] arguments)
        {
            var method = ReflectionApi.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null, "missing reflection accessor: " + name);
            return (TDelegate)method.Invoke(null, arguments);
        }

        private static void CompiledFieldAccessReadsAndWritesInheritedFields()
        {
            var target = new TestFields();
            var get = GenericAccessor<Func<object, int>>("Getter", typeof(int), typeof(TestFields), "HiddenNumber");
            var set = GenericAccessor<Action<object, int>>("Setter", typeof(int), typeof(TestFields), "HiddenNumber");
            Equal(7, get(target));
            set(target, 93);
            Equal(93, get(target));
            var setBool = GenericAccessor<Action<object, bool>>("Setter", typeof(bool), typeof(TestFields), "ControlLeft");
            setBool(target, true);
            True(target.ControlLeft);
            setBool(target, false);
            False(target.ControlLeft);
            var other = new TestFields();
            Equal(7, get(other));
        }

        private static void CompiledStaticAndPropertyWritesHaveCorrectTargets()
        {
            var first = new TestFields();
            var second = new TestFields();
            var setStatic = GenericAccessor<Action<int>>("StaticSetter", typeof(int), typeof(TestFields), "SharedNumber");
            var getStatic = GenericAccessor<Func<int>>("StaticGetter", typeof(int), typeof(TestFields), "SharedNumber");
            setStatic(81);
            Equal(81, getStatic());
            var setProperty = GenericAccessor<Action<object, int>>("PropertySetter", typeof(int), typeof(TestFields), "PropertyNumber");
            var getProperty = GenericAccessor<Func<object, int>>("PropertyGetter", typeof(int), typeof(TestFields), "PropertyNumber");
            setProperty(first, 41);
            setProperty(second, 42);
            Equal(41, getProperty(first));
            Equal(42, getProperty(second));
            setStatic(0);
        }

        private static void CompiledVectorAccessReadsLiveComponents()
        {
            var target = new TestFields { Position = new TestVector { X = 12.5f, Y = -18.25f } };
            var x = Accessor<Func<object, float>>("VectorComponentGetter", typeof(TestFields), "Position", "X");
            var y = Accessor<Func<object, float>>("VectorComponentGetter", typeof(TestFields), "Position", "Y");
            Equal(12.5f, x(target));
            Equal(-18.25f, y(target));
            target.Position.X = 39f;
            Equal(39f, x(target));
            TestFields.SharedPosition = new TestVector { X = -22f, Y = 70f };
            var staticX = Accessor<Func<float>>("StaticVectorComponentGetter", typeof(TestFields), "SharedPosition", "X");
            Equal(-22f, staticX());
            TestFields.SharedPosition.X = 50f;
            Equal(50f, staticX());
        }

        private static void CompiledTileArrayReaderHonorsBothCoordinates()
        {
            var tiles = new TestTile[2, 3];
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 3; y++)
                tiles[x, y] = new TestTile { Id = x * 100 + y };
            var get = Accessor<Func<Array, int, int, object>>("ArrayElementGetter2D", tiles.GetType());
            Equal(102, ((TestTile)get(tiles, 1, 2)).Id);
            Equal(1, ((TestTile)get(tiles, 0, 1)).Id);
            var replacement = new TestTile { Id = 301 };
            tiles[1, 2] = replacement;
            True(ReferenceEquals(replacement, get(tiles, 1, 2)));
            Throws<IndexOutOfRangeException>(() => get(tiles, 2, 0));
            var values = new int[2, 3];
            values[1, 2] = 17;
            var getValue = Accessor<Func<Array, int, int, object>>("ArrayElementGetter2D", values.GetType());
            Equal(17, (int)getValue(values, 1, 2));
        }

        private static void MissingReflectionMembersFailExplicitly()
        {
            Throws<MissingFieldException>(() => GenericAccessor<Func<object, int>>("Getter", typeof(int), typeof(TestFields), "DefinitelyMissing"));
            Throws<MissingMemberException>(() => GenericAccessor<Func<object, int>>("PropertyGetter", typeof(int), typeof(TestFields), "DefinitelyMissing"));
        }

        private static void CompiledPrivateMethodGetterPassesArgumentWithoutMutation()
        {
            var weapon = TestAmmoItem.Minishark();
            var ammo = TestAmmoItem.MusketBall();
            var player = new TestAmmoPlayer { ExpectedWeapon = weapon, SelectedAmmo = ammo, AmmoCyclingOffset = 7 };
            var pick = GenericAccessor<Func<object, object, object>>("MethodGetterWithArgument", typeof(object),
                typeof(TestAmmoPlayer), "PickAmmo_PickAmmoItem", typeof(TestAmmoItem));
            for (var i = 0; i < 5; i++) True(ReferenceEquals(ammo, pick(player, weapon)));
            True(pick(player, TestAmmoItem.Clockwork()) == null, "the exact method argument must be passed");
            True(pick(player, null) == null);
            Equal(999, ammo.stack);
            Equal(7, player.AmmoCyclingOffset);
            Equal(0, player.ConsumptionCalls);
            Throws<MissingMethodException>(() => GenericAccessor<Func<object, object, object>>(
                "MethodGetterWithArgument", typeof(object), typeof(TestAmmoPlayer), "MissingAmmoMethod", typeof(TestAmmoItem)));
        }

        private static void CompiledStaticDictionaryReaderHandlesHitsMissingAndNull()
        {
            var previous = TestContentSamples.ProjectilesByType;
            try
            {
                var sample = new TestProjectileSample { extraUpdates = 1 };
                TestContentSamples.ProjectilesByType = new Dictionary<int, TestProjectileSample> { { 14, sample } };
                var get = Accessor<Func<int, object>>("StaticIntDictionaryValueGetter", typeof(TestContentSamples), "ProjectilesByType");
                True(ReferenceEquals(sample, get(14)));
                True(get(89) == null);
                Equal(1, TestContentSamples.ProjectilesByType.Count);
                var replacement = new TestProjectileSample { extraUpdates = 7 };
                TestContentSamples.ProjectilesByType = new Dictionary<int, TestProjectileSample> { { 242, replacement } };
                True(ReferenceEquals(replacement, get(242)), "read the live static dictionary, not a captured instance");
                True(get(14) == null);
                TestContentSamples.ProjectilesByType = null;
                True(get(242) == null);
            }
            finally { TestContentSamples.ProjectilesByType = previous; }
        }

        private static void HotkeyEdgesOnlyFireOncePerPress()
        {
            var poller = NewHotkeyPoller();
            var first = SampleKeys(poller, true, true, false);
            True(Edge(first, "ActivatePressed"));
            False(Edge(first, "CancelPressed"));
            False(Edge(SampleKeys(poller, true, true, false), "ActivatePressed"));
            var cancel = SampleKeys(poller, true, true, true);
            True(Edge(cancel, "CancelPressed"));
            False(Edge(cancel, "ActivatePressed"));
            False(Edge(SampleKeys(poller, true, true, true), "CancelPressed"));
            SampleKeys(poller, true, false, false);
            True(Edge(SampleKeys(poller, true, true, false), "ActivatePressed"));
        }

        private static void HotkeysOutsideForegroundDoNotActivate()
        {
            var poller = NewHotkeyPoller();
            False(Edge(SampleKeys(poller, false, true, false), "ActivatePressed"));
            False(Edge(SampleKeys(poller, false, true, true), "ActivatePressed"));
            SampleKeys(poller, false, false, false);
            SampleKeys(poller, true, false, false);
            True(Edge(SampleKeys(poller, true, true, false), "ActivatePressed"));
        }

        private static void HeldActivationDoesNotRearmOnFocusReturn()
        {
            var poller = NewHotkeyPoller();
            SampleKeys(poller, false, true, false);
            False(Edge(SampleKeys(poller, true, true, false), "ActivatePressed"));
            SampleKeys(poller, true, false, false);
            True(Edge(SampleKeys(poller, true, true, false), "ActivatePressed"));
        }

        private static void CancelWinsOverSimultaneousActivation()
        {
            var poller = NewHotkeyPoller();
            var edges = SampleKeys(poller, true, true, true);
            False(Edge(edges, "ActivatePressed"));
            True(Edge(edges, "CancelPressed"));
            SampleKeys(poller, false, false, false);
            True(Edge(SampleKeys(poller, false, false, true), "CancelPressed"));
        }

        private static void StructSelectionMethodMutatesOriginalOwnerWithoutBoxingCopy()
        {
            var first = new TestSelectionOwner();
            var second = new TestSelectionOwner();
            True(typeof(TestSelectionOwner).GetProperty("selectedItem").SetMethod == null);
            var select = GenericAccessor<Action<object, int>>("StructMethodSetter", typeof(int),
                typeof(TestSelectionOwner), "selectedItemState", "Select");
            select(first, 7);
            Equal(7, first.selectedItem);
            Equal(7, first.selectedItemState.Hotbar);
            Equal(1, first.selectedItemState.Calls);
            Equal(0, second.selectedItem);
            select(second, 3);
            Equal(3, second.selectedItem);
            Equal(3, second.selectedItemState.Hotbar);
            Equal(7, first.selectedItem);
            select(first, 2);
            Equal(2, first.selectedItem);
            Equal(2, first.selectedItemState.Hotbar);
            Equal(2, first.selectedItemState.Calls);
            Equal(1, second.selectedItemState.Calls);
        }

        private static object NewHotkeyPoller()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.HotkeyPoller", true);
            return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { "F8", "F9" }, null);
        }

        private static void FacadeCaptureReplayPreservesDelayedSlotRequest()
        {
            var fixture = new FacadeInputFixture();
            fixture.Player.Selected = 0;
            fixture.Call("BeginInputFrame");
            fixture.Call("SetSelectedItem", fixture.Player, 3);
            Equal(0, (int)fixture.Call("GetSelectedItem", fixture.Player));
            Equal(3, fixture.Player.Pending);
            fixture.Player.Left = true;
            fixture.Player.Fire = true;
            fixture.MouseX = 143;
            fixture.MouseY = 287;
            fixture.Call("CapturePendingInput", fixture.Player);
            // Simulate vanilla's intervening input copy. Selection itself has not
            // been committed by SelectedItemState.Update yet, exactly as in-game.
            fixture.Player.Pending = 8;
            fixture.Player.Left = false;
            fixture.Player.Fire = false;
            fixture.MouseX = 1;
            fixture.MouseY = 2;
            fixture.Call("ApplyPendingInput", fixture.Player);
            Equal(3, fixture.Player.Pending);
            Equal(0, fixture.Player.Selected);
            True(fixture.Player.Left);
            True(fixture.Player.Fire);
            Equal(143, fixture.MouseX);
            Equal(287, fixture.MouseY);
            // The second production replay point must preserve the same request.
            fixture.Player.Pending = 9;
            fixture.Call("ApplyPendingSelection", fixture.Player);
            Equal(3, fixture.Player.Pending);
            fixture.Player.Commit();
            Equal(3, (int)fixture.Call("GetSelectedItem", fixture.Player));
        }

        private static void FacadeSelectionRequestDoesNotLeakAcrossFrames()
        {
            var fixture = new FacadeInputFixture();
            fixture.Call("BeginInputFrame");
            fixture.Call("SetSelectedItem", fixture.Player, 6);
            fixture.Call("SetSelectedItem", fixture.Player, 7);
            fixture.Call("CapturePendingInput", fixture.Player);
            fixture.Call("ApplyPendingInput", fixture.Player);
            Equal(7, fixture.Player.Pending); // Latest request in one frame wins.
            fixture.Player.Commit();
            Equal(7, fixture.Player.Selected);
            fixture.Player.Selected = 2;
            fixture.Player.Pending = 2;
            fixture.Call("BeginInputFrame");
            fixture.Call("CapturePendingInput", fixture.Player);
            fixture.Call("ApplyPendingInput", fixture.Player);
            Equal(2, fixture.Player.Pending);
            fixture.Player.Commit();
            Equal(2, fixture.Player.Selected);
        }

        private static void FacadeWithoutRequestFollowsActualSelectedSlot()
        {
            var fixture = new FacadeInputFixture();
            foreach (var slot in new[] { 5, 0, 9 })
            {
                fixture.Player.Selected = slot;
                fixture.Call("BeginInputFrame");
                fixture.Call("CapturePendingInput", fixture.Player);
                fixture.Player.Pending = (slot + 1) % 10;
                fixture.Call("ApplyPendingSelection", fixture.Player);
                Equal(slot, fixture.Player.Pending);
                fixture.Player.Commit();
                Equal(slot, fixture.Player.Selected);
            }
        }

        private static void FacadeAimPreservesWorldTargetWithNormalGravity()
        {
            var fixture = new FacadeInputFixture { ScreenX = 32750.25f, ScreenY = 7210.5f, ScreenHeight = 900 };
            fixture.Call("AimAt", fixture.Player, new Chaite.Core.Vec2(33110.25f, 7400.5f));
            Equal(360, fixture.MouseX);
            Equal(190, fixture.MouseY);
            Equal(33110.25f, fixture.ScreenX + fixture.MouseX);
            Equal(7400.5f, fixture.ScreenY + fixture.MouseY);

            // Offscreen targets remain valid world coordinates; do not clamp
            // the cursor to the viewport and alter the firing direction.
            fixture.Call("AimAt", fixture.Player, new Chaite.Core.Vec2(32150.25f, 8510.5f));
            Equal(-600, fixture.MouseX);
            Equal(1300, fixture.MouseY);
        }

        private static void FacadeAimPreservesWorldTargetWithInvertedGravity()
        {
            var fixture = new FacadeInputFixture
            {
                ScreenX = 32750.25f, ScreenY = 7210.5f, ScreenHeight = 900, GravityDirection = -1f
            };
            fixture.Call("AimAt", fixture.Player, new Chaite.Core.Vec2(33110.25f, 7400.5f));
            Equal(360, fixture.MouseX);
            Equal(710, fixture.MouseY);
            // This is vanilla Main.MouseWorld's inverted-gravity decoding.
            Equal(7400.5f, fixture.ScreenY + fixture.ScreenHeight - fixture.MouseY);
            fixture.Call("CapturePendingInput", fixture.Player);
            fixture.MouseX = fixture.MouseY = 0;
            fixture.Call("ApplyPendingSelection", fixture.Player);
            Equal(360, fixture.MouseX);
            Equal(710, fixture.MouseY);

            // Read the live camera and viewport on every aim, including after
            // resizing and while the desired target is beyond the viewport.
            fixture.ScreenY = 7310.5f;
            fixture.ScreenHeight = 720;
            fixture.Call("AimAt", fixture.Player, new Chaite.Core.Vec2(32150.25f, 8510.5f));
            Equal(-600, fixture.MouseX);
            Equal(-480, fixture.MouseY);
            Equal(8510.5f, fixture.ScreenY + fixture.ScreenHeight - fixture.MouseY);
        }

        private static void FacadeMinisharkUsesAmmoSpeedAndProjectileSubupdates()
        {
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Minishark(), TestAmmoItem.MusketBall());
            var result = fixture.Read();
            Equal(22f, result.ShootSpeed);
            Equal(13, result.Damage); // Native modified weapon plus ammo, not the weapon's tooltip alone.
            True(result.NativeProfileRequired && result.Profile.IsSupported);
            Equal(98, result.WeaponId);
            Equal(97, result.AmmoId);
            Equal(8, result.UseTime);
            True(result.IsProjectile && !result.IsMelee && result.IsUsable && result.HasAmmo);
            Equal(10, fixture.Player.ExpectedWeapon.shoot);
            Equal(1, fixture.SelectionCalls);
            Equal(999, fixture.Player.SelectedAmmo.stack);
            Equal(0, fixture.Player.ConsumptionCalls);
        }

        private static void FacadeClockworkUsesNativeSelectedAmmoWithoutConsumption()
        {
            var crystal = TestAmmoItem.CrystalBullet();
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Clockwork(), crystal);
            // Put a different matching ammo stack FIRST. The native selector's
            // result, not a hand-written inventory search, owns ammo priority.
            var musket = TestAmmoItem.MusketBall();
            fixture.Items = new object[] { fixture.Player.ExpectedWeapon, musket, crystal };
            for (var i = 0; i < 7; i++)
            {
                var result = fixture.Read();
                Equal(25.5f, result.ShootSpeed);
                True(result.HasAmmo);
            }
            Equal(7, fixture.SelectionCalls);
            Equal(999, crystal.stack);
            Equal(999, musket.stack);
            Equal(1, fixture.Player.ExpectedWeapon.stack);
            Equal(11, fixture.Player.AmmoCyclingOffset);
            Equal(0, fixture.Player.ConsumptionCalls);
            True(ReferenceEquals(crystal, fixture.Player.SelectedAmmo));
        }

        private static void FacadeWeaponReadUsesLiveProjectileSample()
        {
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Minishark(), TestAmmoItem.MusketBall());
            // This fixture verifies the sample getter wins over fallback values;
            // it is not claiming this artificial extraUpdates is vanilla's value.
            fixture.Samples[14] = new TestProjectileSample { extraUpdates = 2 };
            Equal(33f, fixture.Read().ShootSpeed);
            fixture.Samples[14].extraUpdates = 3;
            Equal(44f, fixture.Read().ShootSpeed);
            fixture.Samples.Clear();
            Equal(22f, fixture.Read().ShootSpeed);
            fixture.Player.ExpectedWeapon.shootSpeed = 8f;
            Equal(24f, fixture.Read().ShootSpeed);
        }

        private static void FacadeWeaponReadRejectsMissingNativeAmmo()
        {
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Minishark(), TestAmmoItem.MusketBall());
            // A matching inventory entry must not override native selection failure.
            fixture.Player.SelectedAmmo = null;
            False(fixture.Read().HasAmmo);
            fixture.Player.SelectedAmmo = TestAmmoItem.MusketBall();
            fixture.Player.SelectedAmmo.stack = 0;
            False(fixture.Read().HasAmmo);
            fixture.Player.SelectedAmmo = null;
            fixture.Items = new object[] { fixture.Player.ExpectedWeapon };
            False(fixture.Read().HasAmmo);
            Equal(0, fixture.Player.ConsumptionCalls);
        }

        private static void FacadeWeaponSelectionDoesNotPreferHighDpsPureMelee()
        {
            var gun = TestAmmoItem.Minishark();
            var ammo = TestAmmoItem.MusketBall();
            var fixture = new FacadeWeaponFixture(gun, ammo);
            var sword = new TestAmmoItem { type = 4, damage = 999, useTime = 1, useStyle = 1, shoot = 0 };
            fixture.Items = new object[] { sword, gun, ammo };
            Equal(0f, fixture.Score(0));
            True(fixture.Score(1) > 0f);
            Equal(1, fixture.FindBestSlot());
            fixture.Items = new object[] { gun, sword, ammo };
            Equal(0, fixture.FindBestSlot());
            Equal(0, fixture.Player.ConsumptionCalls);
            Equal(999, ammo.stack);
        }

        private static void FacadeWeaponProfilesUseDamageModifiersAndBurstPhase()
        {
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Clockwork(), TestAmmoItem.CrystalBullet());
            fixture.Player.DamageMultiplier = 1.5f;
            fixture.Player.ItemAnimation = 10; // Native decrements to 9 before gun shooting.
            var result = fixture.Read();
            Equal(38, result.Damage); // floor(17*1.5+epsilon) + floor(9*1.5).
            Equal(1, result.Profile.BurstShotIndex);
            True(Math.Abs(result.ShootSpeed - 26.775f) < .0001f);
            True(Math.Abs(result.ApproximateDps - 38f * 180f / 26f) < .001f);
            fixture.Player.ItemAnimation = 5;
            result = fixture.Read();
            Equal(2, result.Profile.BurstShotIndex);
            True(Math.Abs(result.ShootSpeed - 28.05f) < .0001f);
            fixture.Player.ItemAnimation = 0;
            Equal(0, fixture.Read().Profile.BurstShotIndex);
            Equal(0, fixture.Player.ConsumptionCalls);
        }

        private static void FacadeWeaponProfilesResetOnAmmoAndWeaponChanges()
        {
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Minishark(), TestAmmoItem.MusketBall());
            True(fixture.Read().Profile.IsSupported);
            fixture.Player.SelectedAmmo = new TestAmmoItem { type = 9999, stack = 50, damage = 900, shoot = 14, shootSpeed = 4, ammo = 97 };
            var unsupported = fixture.Read();
            Equal(9999, unsupported.AmmoId);
            False(unsupported.Profile.IsSupported);
            Equal(0f, fixture.Score(0));
            fixture.Player.SelectedAmmo = TestAmmoItem.CrystalBullet();
            True(fixture.Read().Profile.IsSupported);
            fixture.Items = new object[0];
            var empty = fixture.Read();
            True(empty.NativeProfileRequired);
            False(empty.Profile.IsSupported);
            Equal(0, empty.WeaponId);
            Equal(0, empty.AmmoId);
        }

        private static void PlannerRefusesUnsupportedNativeCombinationBeforeSummoning()
        {
            var snapshot = CombatScenario(4);
            snapshot.Weapon.NativeProfileRequired = true;
            string reason;
            var planner = new Chaite.Core.CombatPlanner(new Chaite.Core.PlannerSettings());
            False(planner.RequirementsMetForExpected(snapshot, "eye", 4, out reason));
            True(!string.IsNullOrEmpty(reason));
            var plan = planner.Plan(snapshot);
            False(plan.Fire);
            True(!string.IsNullOrEmpty(plan.WeaponIssue));
            False(planner.PlanSurvival(snapshot).Fire);
        }

        private static void PlannerWeaponAimUsesExactProfileAndRejectsImpossibleIntercept()
        {
            var snapshot = CombatScenario(4);
            var fixture = new FacadeWeaponFixture(TestAmmoItem.Clockwork(), TestAmmoItem.CrystalBullet());
            fixture.Player.ItemAnimation = 5;
            snapshot.Weapon = fixture.Read();
            snapshot.Weapon.ShootSpeed = 1f; // Poison obsolete generic input.
            var planner = new Chaite.Core.CombatPlanner(new Chaite.Core.PlannerSettings());
            var target = snapshot.Targets[0];
            var exact = Chaite.Core.WeaponAimSolver.Solve(snapshot.Weapon.Profile,
                snapshot.Player.Center, target.Center, target.Velocity);
            var plan = planner.Plan(snapshot);
            True(plan.Fire);
            Equal(exact.AimWorld.X, plan.AimWorld.X);
            Equal(exact.AimWorld.Y, plan.AimWorld.Y);
            target.Velocity = new Chaite.Core.Vec2(1000f, 0f);
            snapshot.Targets[0] = target;
            False(planner.Plan(snapshot).Fire);
            False(planner.PlanSurvival(snapshot).Fire);
        }

        private static void JumpGateReleasesGroundedHoldBeforeFreshPress()
        {
            False(HoldNativeJump(true, true, false, false));
            True(HoldNativeJump(true, true, true, false));
            False(HoldNativeJump(false, true, true, false));
        }

        private static void JumpGatePreservesAirborneAndGrappleHolds()
        {
            True(HoldNativeJump(true, false, false, false));
            True(HoldNativeJump(true, true, false, true));
            True(HoldNativeJump(true, false, false, true));
            foreach (var grounded in new[] { false, true })
            foreach (var released in new[] { false, true })
            foreach (var grappling in new[] { false, true })
                False(HoldNativeJump(false, grounded, released, grappling));
        }

        private static bool HoldNativeJump(bool requested, bool grounded, bool releaseReady, bool grappling)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.MovementActionGate", true);
            var method = type.GetMethod("ShouldHoldJump", BindingFlags.Public | BindingFlags.Static);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { requested, grounded, releaseReady, grappling });
        }

        private static void FacadePlatformsRemainOneWayWithBothSolidFlags()
        {
            var facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            Action<string, object> bind = (name, value) =>
            {
                var field = facadeType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing tile fixture dependency: " + name);
                field.SetValue(facade, value);
            };
            var tiles = new TestCombatTile[2, 2];
            tiles[0, 0] = new TestCombatTile { type = 19, Active = true };
            tiles[1, 0] = new TestCombatTile { type = 38, Active = true };
            var solidFlags = new bool[40];
            var topFlags = new bool[40];
            // Native platforms may have BOTH flags. SolidTop must take priority
            // over Solid when scanning upward or excluding one-way platforms.
            solidFlags[19] = topFlags[19] = true;
            solidFlags[38] = true;
            bind("_tileAt", Accessor<Func<Array, int, int, object>>("ArrayElementGetter2D", tiles.GetType()));
            bind("_tileType", GenericAccessor<Func<object, ushort>>("Getter", typeof(ushort), typeof(TestCombatTile), "type"));
            bind("_tileActive", GenericAccessor<Func<object, bool>>("MethodGetter", typeof(bool), typeof(TestCombatTile), "active"));
            bind("_tileInactive", GenericAccessor<Func<object, bool>>("MethodGetter", typeof(bool), typeof(TestCombatTile), "inActive"));
            bind("_tileSolid", solidFlags);
            bind("_tileSolidTop", topFlags);
            var method = facadeType.GetMethod("IsSolid", BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null);
            Func<int, int, bool, bool> solid = (x, y, includePlatforms) =>
                (bool)method.Invoke(facade, new object[] { tiles, x, y, includePlatforms });
            False(solid(0, 0, false));
            True(solid(0, 0, true));
            True(solid(1, 0, false));
            True(solid(1, 0, true));
            False(solid(0, 1, false));
            False(solid(0, 1, true));
            foreach (var x in new[] { 0, 1 })
            {
                tiles[x, 0].Inactive = true;
                False(solid(x, 0, false));
                False(solid(x, 0, true));
                tiles[x, 0].Inactive = false;
                tiles[x, 0].Active = false;
                False(solid(x, 0, false));
                False(solid(x, 0, true));
            }
        }

        private static void TargetSightCacheExpiresAfterTwelveFrames()
        {
            var cache = new SightCacheFixture();
            var player = new Chaite.Core.Vec2(100, 200);
            var target = new Chaite.Core.Vec2(500, 600);
            bool visible;
            False(cache.TryGet(3, 134, player, target, 100, out visible));
            False(visible);
            cache.Record(3, 134, player, target, 100, true);
            True(cache.TryGet(3, 134, player, target, 100, out visible));
            True(visible);
            True(cache.TryGet(3, 134, player, target, 112, out visible));
            True(visible);
            False(cache.TryGet(3, 134, player, target, 113, out visible));
            False(visible);
            False(cache.TryGet(3, 134, player, target, 99, out visible));
            False(cache.TryGet(3, 134, player, target, -1, out visible));
            foreach (var frame in new[] { 0, -1 })
            {
                cache.Record(3, 134, player, target, frame, true);
                False(cache.TryGet(3, 134, player, target, 1, out visible));
            }
        }

        private static void TargetSightCacheInvalidatesWhenEitherEndpointMoves()
        {
            var cache = new SightCacheFixture();
            var player = new Chaite.Core.Vec2(100, 200);
            var target = new Chaite.Core.Vec2(500, 600);
            cache.Record(3, 134, player, target, 100, true);
            bool visible;
            True(cache.TryGet(3, 134, new Chaite.Core.Vec2(164, 200), target, 101, out visible));
            True(visible);
            False(cache.TryGet(3, 134, new Chaite.Core.Vec2(165, 200), target, 101, out visible));
            False(visible);
            True(cache.TryGet(3, 134, player, new Chaite.Core.Vec2(500, 664), 101, out visible));
            False(cache.TryGet(3, 134, player, new Chaite.Core.Vec2(500, 665), 101, out visible));
            // Distance is measured from the recorded endpoints, not each prior query.
            True(cache.TryGet(3, 134, new Chaite.Core.Vec2(132, 200), target, 102, out visible));
            False(cache.TryGet(3, 134, new Chaite.Core.Vec2(165, 200), target, 103, out visible));
        }

        private static void TargetSightCacheSeparatesKeysTypesAndClears()
        {
            var cache = new SightCacheFixture();
            var player = new Chaite.Core.Vec2(100, 200);
            var target = new Chaite.Core.Vec2(500, 600);
            bool visible;
            cache.Record(0, 134, player, target, 100, false);
            True(cache.TryGet(0, 134, player, target, 100, out visible), "cached blocked sight is a hit, not an unknown result");
            False(visible);
            False(cache.TryGet(1, 134, player, target, 100, out visible));
            False(cache.TryGet(0, 135, player, target, 100, out visible));
            False(cache.TryGet(0, -1, player, target, 100, out visible));
            foreach (var key in new[] { -1, 200, int.MinValue, int.MaxValue })
            {
                cache.Record(key, 134, player, target, 100, true);
                False(cache.TryGet(key, 134, player, target, 100, out visible));
            }
            cache.Record(0, 135, player, target, 101, true);
            False(cache.TryGet(0, 134, player, target, 101, out visible));
            True(cache.TryGet(0, 135, player, target, 101, out visible));
            True(visible);
            cache.Record(199, 136, player, target, 101, true);
            True(cache.TryGet(199, 136, player, target, 101, out visible));
            cache.Clear();
            False(cache.TryGet(0, 135, player, target, 101, out visible));
            False(cache.TryGet(199, 136, player, target, 101, out visible));
        }

        private static void FacadeInputFrameRenewsSightBudgetWithoutClearingCache()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(type);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var budget = type.GetField("_sightQueryBudget", flags);
            var cacheField = type.GetField("_sightCache", flags);
            var frame = type.GetField("_sightFrame", flags);
            var begin = type.GetMethod("BeginInputFrame");
            True(budget != null && cacheField != null && frame != null && begin != null);
            var cache = new SightCacheFixture();
            var player = new Chaite.Core.Vec2(100f, 200f);
            var target = new Chaite.Core.Vec2(500f, 600f);
            cache.Record(3, 134, player, target, 100, true);
            cacheField.SetValue(facade, cache.Instance);
            frame.SetValue(facade, 100);
            budget.SetValue(facade, 1);
            begin.Invoke(facade, null);
            Equal(3, (int)budget.GetValue(facade));
            // Simulate the first snapshot exhausting this input frame's shared budget.
            budget.SetValue(facade, 0);
            Equal(0, (int)budget.GetValue(facade));
            begin.Invoke(facade, null);
            Equal(3, (int)budget.GetValue(facade));
            Equal(100, (int)frame.GetValue(facade));
            True(ReferenceEquals(cache.Instance, cacheField.GetValue(facade)));
            bool visible;
            True(cache.TryGet(3, 134, player, target, 100, out visible));
            True(visible, "a new per-frame ray budget must not clear valid target visibility");
        }

        private sealed class SightCacheFixture
        {
            private readonly object _cache;
            private readonly Type _type;
            public object Instance => _cache;

            public SightCacheFixture()
            {
                _type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TargetSightCache", true);
                _cache = Activator.CreateInstance(_type, true);
            }

            public void Record(int key, int type, Chaite.Core.Vec2 player, Chaite.Core.Vec2 target, int frame, bool visible) =>
                _type.GetMethod("Record").Invoke(_cache, new object[] { key, type, player, target, frame, visible });

            public bool TryGet(int key, int type, Chaite.Core.Vec2 player, Chaite.Core.Vec2 target, int frame, out bool visible)
            {
                var arguments = new object[] { key, type, player, target, frame, false };
                var found = (bool)_type.GetMethod("TryGet").Invoke(_cache, arguments);
                visible = (bool)arguments[5];
                return found;
            }

            public void Clear() => _type.GetMethod("Clear").Invoke(_cache, null);
        }

        private sealed class FacadeWeaponFixture
        {
            private readonly object _facade;
            private readonly Type _facadeType;
            public readonly TestAmmoPlayer Player;
            public readonly Dictionary<int, TestProjectileSample> Samples = new Dictionary<int, TestProjectileSample>();
            public object[] Items;
            public int SelectionCalls;

            public FacadeWeaponFixture(TestAmmoItem weapon, TestAmmoItem ammo)
            {
                Player = new TestAmmoPlayer { ExpectedWeapon = weapon, SelectedAmmo = ammo, AmmoCyclingOffset = 11 };
                Items = new object[] { weapon, ammo };
                _facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
                _facade = FormatterServices.GetUninitializedObject(_facadeType);
                Set("_inventory", new Func<object, object[]>(player => Items));
                Set("_selectedItem", new Func<object, int>(player => 0));
                Set("_weaponSelectionSnapshot", new Chaite.Core.WeaponSnapshot());
                Set("_playerItemAnimation", new Func<object, int>(player => ((TestAmmoPlayer)player).ItemAnimation));
                Set("_weaponDamage", new Func<object, object, int>((player, item) =>
                    (int)(((TestAmmoItem)item).damage * ((TestAmmoPlayer)player).DamageMultiplier + .000005f)));
                Set("_weaponDamageMultiplier", new Func<object, object, float>((player, item) => ((TestAmmoPlayer)player).DamageMultiplier));
                BindItem<int>("_itemTypeId", "type");
                BindItem<int>("_itemStack", "stack");
                BindItem<int>("_itemDamage", "damage");
                BindItem<int>("_itemUseTime", "useTime");
                BindItem<int>("_itemUseAnimation", "useAnimation");
                BindItem<int>("_itemReuseDelay", "reuseDelay");
                BindItem<int>("_itemUseStyle", "useStyle");
                BindItem<int>("_itemPick", "pick");
                BindItem<int>("_itemAxe", "axe");
                BindItem<int>("_itemHammer", "hammer");
                BindItem<int>("_itemCreateTile", "createTile");
                BindItem<int>("_itemFishingPole", "fishingPole");
                BindItem<int>("_itemShoot", "shoot");
                BindItem<float>("_itemShootSpeed", "shootSpeed");
                BindItem<int>("_itemAmmo", "ammo");
                BindItem<int>("_itemUseAmmo", "useAmmo");
                var readAmmo = GenericAccessor<Func<object, object, object>>("MethodGetterWithArgument", typeof(object),
                    typeof(TestAmmoPlayer), "PickAmmo_PickAmmoItem", typeof(TestAmmoItem));
                Set("_pickAmmoItem", new Func<object, object, object>((player, item) =>
                {
                    SelectionCalls++;
                    return readAmmo(player, item);
                }));
                Set("_projectileSample", new Func<int, object>(type =>
                {
                    TestProjectileSample sample;
                    return Samples.TryGetValue(type, out sample) ? sample : null;
                }));
                Set("_projectileExtraUpdates", GenericAccessor<Func<object, int>>("Getter", typeof(int),
                    typeof(TestProjectileSample), "extraUpdates"));
                Set("_projectileTimeLeft", GenericAccessor<Func<object, int>>("Getter", typeof(int),
                    typeof(TestProjectileSample), "timeLeft"));
            }

            private void BindItem<T>(string dependency, string fieldName) =>
                Set(dependency, GenericAccessor<Func<object, T>>("Getter", typeof(T), typeof(TestAmmoItem), fieldName));

            private void Set(string name, object value)
            {
                var field = _facadeType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing weapon fixture dependency: " + name);
                field.SetValue(_facade, value);
            }

            public Chaite.Core.WeaponSnapshot Read()
            {
                var result = new Chaite.Core.WeaponSnapshot();
                var method = _facadeType.GetMethod("ReadWeaponInto", BindingFlags.Instance | BindingFlags.NonPublic);
                True(method != null);
                method.Invoke(_facade, new object[] { Player, Items, 0, result });
                return result;
            }

            public int FindBestSlot()
            {
                var method = _facadeType.GetMethod("FindBestWeaponSlot", BindingFlags.Instance | BindingFlags.Public);
                True(method != null);
                return (int)method.Invoke(_facade, new object[] { Player });
            }

            public float Score(int slot)
            {
                var method = _facadeType.GetMethod("WeaponScore", BindingFlags.Instance | BindingFlags.NonPublic);
                True(method != null);
                return (float)method.Invoke(_facade, new object[] { Player, Items, slot });
            }
        }

        private sealed class FacadeInputFixture
        {
            private readonly object _facade;
            private readonly Type _facadeType;
            public readonly DelayedSelectionPlayer Player = new DelayedSelectionPlayer();
            public int MouseX;
            public int MouseY;
            public float ScreenX;
            public float ScreenY;
            public int ScreenHeight = 900;
            public float GravityDirection = 1f;

            public FacadeInputFixture()
            {
                _facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
                // Exercise the REAL production facade's public input frame/capture/
                // replay methods. Only its external game field delegates are mocked;
                // bypassing the constructor avoids loading Terraria or input APIs.
                _facade = FormatterServices.GetUninitializedObject(_facadeType);
                Set("_selectedItem", new Func<object, int>(p => ((DelayedSelectionPlayer)p).Selected));
                Set("_setSelectedItem", new Action<object, int>((p, slot) => ((DelayedSelectionPlayer)p).Pending = slot));
                Set("_controls", new Dictionary<string, Action<object, bool>>
                {
                    { "controlLeft", (p, value) => ((DelayedSelectionPlayer)p).Left = value },
                    { "controlUseItem", (p, value) => ((DelayedSelectionPlayer)p).Fire = value }
                });
                Set("_controlReaders", new Dictionary<string, Func<object, bool>>
                {
                    { "controlLeft", p => ((DelayedSelectionPlayer)p).Left },
                    { "controlUseItem", p => ((DelayedSelectionPlayer)p).Fire }
                });
                Set("_capturedControls", new Dictionary<string, bool>());
                Set("_readMouseX", new Func<int>(() => MouseX));
                Set("_readMouseY", new Func<int>(() => MouseY));
                Set("_mouseX", new Action<int>(value => MouseX = value));
                Set("_mouseY", new Action<int>(value => MouseY = value));
                Set("_screenX", new Func<float>(() => ScreenX));
                Set("_screenY", new Func<float>(() => ScreenY));
                Set("_screenHeight", new Func<int>(() => ScreenHeight));
                Set("_playerGravDir", new Func<object, float>(p => GravityDirection));
            }

            private void Set(string name, object value)
            {
                var field = _facadeType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing facade test dependency: " + name);
                field.SetValue(_facade, value);
            }

            public object Call(string name, params object[] arguments)
            {
                var method = _facadeType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                True(method != null, "missing production facade method: " + name);
                return method.Invoke(_facade, arguments);
            }
        }

        private sealed class DelayedSelectionPlayer
        {
            public int Selected;
            public int Pending;
            public bool Left;
            public bool Fire;
            public void Commit() => Selected = Pending;
        }

        private static object SampleKeys(object poller, bool foreground, bool activate, bool cancel)
        {
            var method = poller.GetType().GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            True(method != null, "HotkeyPoller.Sample should provide deterministic headless edge tests");
            return method.Invoke(poller, new object[] { foreground, activate, cancel });
        }

        private static bool Edge(object edges, string name) => (bool)edges.GetType().GetField(name).GetValue(edges);

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                if (ex is TException) return;
                throw new InvalidOperationException("expected " + typeof(TException).Name + ", got " + ex.GetType().Name + ": " + ex.Message);
            }
            throw new InvalidOperationException("expected " + typeof(TException).Name + ", but nothing was thrown");
        }
    }

    // Synthetic fixtures only: no installed Terraria assembly, live process or input APIs are used.
    public class TestFieldsBase
    {
        private int HiddenNumber = 7;
        public int ReadHiddenNumber() => HiddenNumber;
    }

    public sealed class TestFields : TestFieldsBase
    {
        public bool ControlLeft;
        public static int SharedNumber;
        public TestVector Position;
        public static TestVector SharedPosition;
        public int PropertyNumber { get; set; }
    }

    public struct TestVector
    {
        public float X;
        public float Y;
    }

    public sealed class TestTile
    {
        public int Id;
    }

    public sealed class TestSelectionOwner
    {
        public TestSelectionState selectedItemState;
        public int selectedItem => selectedItemState.Selected;
    }

    public struct TestSelectionState
    {
        private int _selected;
        private int _hotbar;
        public int Selected => _selected;
        public int Hotbar => _hotbar;
        public int Calls;

        public void Select(int item)
        {
            _selected = item;
            _hotbar = item;
            Calls++;
        }
    }

    public sealed class TestProjectileSample
    {
        public int extraUpdates;
        public int timeLeft = 600;
    }

    public sealed class TestCombatTile
    {
        public ushort type;
        public bool Active;
        public bool Inactive;
        private bool active() => Active;
        private bool inActive() => Inactive;
    }

    public static class TestContentSamples
    {
        public static Dictionary<int, TestProjectileSample> ProjectilesByType;
    }

    public sealed class TestAmmoPlayer
    {
        public TestAmmoItem ExpectedWeapon;
        public TestAmmoItem SelectedAmmo;
        public int AmmoCyclingOffset;
        public int ConsumptionCalls;
        public int ItemAnimation;
        public float DamageMultiplier = 1f;

        private TestAmmoItem PickAmmo_PickAmmoItem(TestAmmoItem weapon) =>
            ReferenceEquals(weapon, ExpectedWeapon) ? SelectedAmmo : null;

        // Poison pill: consuming PickAmmo is deliberately not a fixture binding.
        // Only the read-only selector can satisfy the production adapter tests.
        private void PickAmmo(TestAmmoItem weapon)
        {
            ConsumptionCalls++;
            throw new InvalidOperationException("Ammo inspection must not call consuming PickAmmo.");
        }
    }

    public sealed class TestAmmoItem
    {
        public int type;
        public int stack = 1;
        public int damage;
        public int useTime;
        public int useAnimation;
        public int reuseDelay;
        public int useStyle;
        public int pick;
        public int axe;
        public int hammer;
        public int createTile = -1;
        public int fishingPole;
        public int shoot;
        public float shootSpeed;
        public int ammo;
        public int useAmmo;

        // Exact unprefixed 1.4.5.8 fields audited from Item.SetDefaults1.
        public static TestAmmoItem Minishark() => new TestAmmoItem
        { type = 98, damage = 6, useTime = 8, useAnimation = 8, useStyle = 5, shoot = 10, shootSpeed = 7f, useAmmo = 97 };
        public static TestAmmoItem Clockwork() => new TestAmmoItem
        { type = 434, damage = 17, useTime = 4, useAnimation = 12, reuseDelay = 14, useStyle = 5, shoot = 10, shootSpeed = 7.75f, useAmmo = 97 };
        public static TestAmmoItem MusketBall() => new TestAmmoItem
        { type = 97, stack = 999, damage = 7, shoot = 14, shootSpeed = 4f, ammo = 97 };
        public static TestAmmoItem CrystalBullet() => new TestAmmoItem
        { type = 515, stack = 999, damage = 9, shoot = 89, shootSpeed = 5f, ammo = 97 };
    }
}
