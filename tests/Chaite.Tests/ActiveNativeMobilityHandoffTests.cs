using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class ActiveNativeMobilityHandoffTests
    {
        public static int RunAll()
        {
            GrappleReleaseIsRearmedThenPulsedOnce();
            MountReleaseIsSpaceGatedAndPulsedOnce();
            HookIsAlwaysReleasedBeforeMount();
            IncompleteAndUnsafeNativeStateFailsClosed();
            DeadPlayerWaitsWithoutLeakingInput();
            BoundedHandoffCannotKeepControlForever();
            return 6;
        }

        private static void GrappleReleaseIsRearmedThenPulsedOnce()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var state = Ready();
            state.Grappling = true;
            state.ReleaseJump = false;
            var rearm = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.RearmJump, rearm.Action);
            False(rearm.HoldJump);
            False(rearm.ToggleMount);

            state.ReleaseJump = true;
            var detach = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.DetachGrapple, detach.Action);
            True(detach.HoldJump);
            False(detach.ToggleMount);

            var repeated = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, repeated.Action);
            True(repeated.Rejected);

            handoff.Begin();
            state = Ready();
            var ready = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.Ready, ready.Action);
            True(ready.Complete);
            False(handoff.Active);
        }

        private static void MountReleaseIsSpaceGatedAndPulsedOnce()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var state = Ready();
            state.MountActive = true;
            state.MountDismountProbeKnown = true;
            state.MountCanDismount = true;
            state.ReleaseMount = false;
            var rearm = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.RearmMount, rearm.Action);
            False(rearm.HoldJump);
            False(rearm.ToggleMount);

            state.ReleaseMount = true;
            var dismount = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.DismountMount, dismount.Action);
            True(dismount.ToggleMount);
            False(dismount.HoldJump);

            var repeated = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, repeated.Action);
            True(repeated.Rejected);
        }

        private static void HookIsAlwaysReleasedBeforeMount()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var state = Ready();
            state.Grappling = true;
            state.ReleaseJump = true;
            state.MountActive = true;
            state.ReleaseMount = true;
            state.MountDismountProbeKnown = true;
            state.MountCanDismount = true;
            var hook = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.DetachGrapple, hook.Action);
            True(hook.HoldJump);
            False(hook.ToggleMount);

            state.Grappling = false;
            var mount = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.DismountMount, mount.Action);
            False(mount.HoldJump);
            True(mount.ToggleMount);
        }

        private static void IncompleteAndUnsafeNativeStateFailsClosed()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var incomplete = Ready();
            incomplete.Known = false;
            var bad = handoff.Advance(in incomplete);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, bad.Action);
            True(bad.Rejected);
            False(bad.HoldJump || bad.ToggleMount);

            handoff.Begin();
            var unknownProbe = Ready();
            unknownProbe.MountActive = true;
            var probe = handoff.Advance(in unknownProbe);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, probe.Action);
            True(probe.Rejected);

            handoff.Begin();
            var noSpace = Ready();
            noSpace.MountActive = true;
            noSpace.MountDismountProbeKnown = true;
            noSpace.MountCanDismount = false;
            var blocked = handoff.Advance(in noSpace);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, blocked.Action);
            True(blocked.Rejected);
        }

        private static void DeadPlayerWaitsWithoutLeakingInput()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var dead = Ready();
            dead.PlayerDead = true;
            dead.Grappling = true;
            dead.ReleaseJump = true;
            var wait = handoff.Advance(in dead);
            Equal(ActiveNativeMobilityHandoffAction.None, wait.Action);
            False(wait.Complete || wait.Rejected || wait.HoldJump || wait.ToggleMount);
            True(handoff.Active);
        }

        private static void BoundedHandoffCannotKeepControlForever()
        {
            var handoff = new ActiveNativeMobilityHandoff();
            handoff.Begin();
            var state = Ready();
            state.Grappling = true;
            state.ReleaseJump = false;
            for (var i = 0; i < ActiveNativeMobilityHandoff.MaximumFrames; i++)
            {
                var decision = handoff.Advance(in state);
                Equal(ActiveNativeMobilityHandoffAction.RearmJump, decision.Action);
            }
            var timeout = handoff.Advance(in state);
            Equal(ActiveNativeMobilityHandoffAction.Rejected, timeout.Action);
            True(timeout.Rejected);
            False(handoff.Active);
        }

        private static ActiveNativeMobilityHandoffSnapshot Ready()
        {
            return new ActiveNativeMobilityHandoffSnapshot
            {
                Known = true,
                EncounterActive = true
            };
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
                throw new InvalidOperationException("expected " + expected + ", actual " + actual);
        }
    }
}
