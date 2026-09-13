using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class CombatWeaponSelectionHandoffTests
    {
        public static int RunAll()
        {
            AlreadySelectedCompletesImmediately();
            FortyFiveFrameSummonAnimationIsAllowedToFinish();
            ReleasedSelectionMustCommitWithinBound();
            NativeUseCannotHoldControlForever();
            ANewNativeBlockResetsTheReleasedCommitWindow();
            return 5;
        }

        private static void AlreadySelectedCompletesImmediately()
        {
            var handoff = new CombatWeaponSelectionHandoff();
            var decision = handoff.Advance(true, false);
            Equal(CombatWeaponSelectionAction.Ready, decision.Action);
            True(decision.Complete);
            False(decision.RequestSelection || decision.Rejected);
        }

        private static void FortyFiveFrameSummonAnimationIsAllowedToFinish()
        {
            var handoff = new CombatWeaponSelectionHandoff();
            for (var frame = 0; frame < 45; frame++)
            {
                var waiting = handoff.Advance(false, false);
                Equal(CombatWeaponSelectionAction.RequestWhileNativeUseFinishes,
                    waiting.Action);
                True(waiting.RequestSelection);
                False(waiting.Complete || waiting.Rejected);
            }
            var released = handoff.Advance(false, true);
            Equal(CombatWeaponSelectionAction.RequestAfterNativeRelease,
                released.Action);
            True(released.RequestSelection);
            var committed = handoff.Advance(true, true);
            True(committed.Complete);
        }

        private static void ReleasedSelectionMustCommitWithinBound()
        {
            var handoff = new CombatWeaponSelectionHandoff();
            for (var frame = 0;
                frame < CombatWeaponSelectionHandoff.MaximumReleasedFrames;
                frame++)
            {
                var request = handoff.Advance(false, true);
                True(request.RequestSelection);
                False(request.Rejected);
            }
            var rejected = handoff.Advance(false, true);
            True(rejected.Rejected);
            False(rejected.RequestSelection);
        }

        private static void NativeUseCannotHoldControlForever()
        {
            var handoff = new CombatWeaponSelectionHandoff();
            for (var frame = 0;
                frame < CombatWeaponSelectionHandoff.MaximumFrames;
                frame++)
            {
                var request = handoff.Advance(false, false);
                True(request.RequestSelection);
                False(request.Rejected);
            }
            var rejected = handoff.Advance(false, false);
            True(rejected.Rejected);
            False(rejected.RequestSelection);
        }

        private static void ANewNativeBlockResetsTheReleasedCommitWindow()
        {
            var handoff = new CombatWeaponSelectionHandoff();
            for (var frame = 0;
                frame < CombatWeaponSelectionHandoff.MaximumReleasedFrames;
                frame++)
                False(handoff.Advance(false, true).Rejected);
            False(handoff.Advance(false, false).Rejected);
            False(handoff.Advance(false, true).Rejected);
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("expected true");
        }

        private static void False(bool value)
        {
            if (value) throw new InvalidOperationException("expected false");
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected +
                    ", actual " + actual);
        }
    }
}
