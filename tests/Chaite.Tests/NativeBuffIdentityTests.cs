using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private delegate bool ActiveBuffRead(int[] types, int[] times, int buffType,
            out bool active);
        private static ActiveBuffRead _activeBuffRead;

        private static void RunNativeBuffIdentityRegressions()
        {
            Run(nameof(ExactBuffReaderDistinguishesFeatherFallFromOtherSlowFallSources),
                ExactBuffReaderDistinguishesFeatherFallFromOtherSlowFallSources);
            Run(nameof(ExactBuffReaderRequiresLiveAlignedNativeArrays),
                ExactBuffReaderRequiresLiveAlignedNativeArrays);
            Run(nameof(ExactBuffReaderDoesNotMutateNativeArrays),
                ExactBuffReaderDoesNotMutateNativeArrays);
            Run(nameof(ExactBuffReaderRecognizesVanillaSlowIdentity),
                ExactBuffReaderRecognizesVanillaSlowIdentity);
        }

        private static ActiveBuffRead ActiveBuffReader()
        {
            if (_activeBuffRead != null) return _activeBuffRead;
            var facade = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var method = facade.GetMethod("TryReadActiveBuff",
                BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(int[]), typeof(int[]), typeof(int), typeof(bool).MakeByRefType() },
                null);
            True(method != null);
            _activeBuffRead = (ActiveBuffRead)Delegate.CreateDelegate(
                typeof(ActiveBuffRead), method);
            return _activeBuffRead;
        }

        private static void ExactBuffReaderDistinguishesFeatherFallFromOtherSlowFallSources()
        {
            var read = ActiveBuffReader();
            bool active;
            True(read(new[] { 18, 8, 0 }, new[] { 600, 1200, 0 }, 8, out active));
            True(active);
            // Djinn's Curse and CarpetMovement can set slowFall, but neither is
            // active Buff 8 and therefore cannot authorize the dedicated Up edge.
            True(read(new[] { 18, 0, 0 }, new[] { 600, 0, 0 }, 8, out active));
            False(active);
            True(read(new[] { 8, 0 }, new[] { 0, 0 }, 8, out active));
            False(active);
        }

        private static void ExactBuffReaderRequiresLiveAlignedNativeArrays()
        {
            var read = ActiveBuffReader();
            bool active;
            False(read(null, new int[1], 8, out active)); False(active);
            False(read(new int[1], null, 8, out active)); False(active);
            False(read(Array.Empty<int>(), Array.Empty<int>(), 8, out active)); False(active);
            False(read(new int[2], new int[1], 8, out active)); False(active);
            False(read(new[] { 8 }, new[] { 10 }, 0, out active)); False(active);
        }

        private static void ExactBuffReaderDoesNotMutateNativeArrays()
        {
            var read = ActiveBuffReader();
            var types = new[] { 8, 18, 0 };
            var times = new[] { 10, 20, 0 };
            var typesBefore = (int[])types.Clone();
            var timesBefore = (int[])times.Clone();
            bool active;
            True(read(types, times, 8, out active)); True(active);
            for (var index = 0; index < types.Length; index++)
            {
                Equal(typesBefore[index], types[index]);
                Equal(timesBefore[index], times[index]);
            }
        }

        private static void ExactBuffReaderRecognizesVanillaSlowIdentity()
        {
            var read = ActiveBuffReader();
            bool active;
            True(read(new[] { 32, 0, 0 }, new[] { 720, 0, 0 }, 32,
                out active));
            True(active);
            True(read(new[] { 46, 0, 0 }, new[] { 720, 0, 0 }, 32,
                out active));
            False(active);
            // Duplicate identities are not a valid vanilla observation and
            // must not authorize movement-capability normalization.
            False(read(new[] { 32, 32 }, new[] { 720, 719 }, 32,
                out active));
            False(active);
        }
    }
}
