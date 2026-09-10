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
}
