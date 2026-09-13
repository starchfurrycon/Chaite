using System;

namespace Chaite.Core
{
    // Explicit native profile: dry, normal gravity, base ordinary/cloud jump,
    // Demon Wings (1), optionally Lightning Boots (rocketBoots 2). Unknown
    // profiles must not enter this transition function as generic wing models.
    public struct FlightSnapshot
    {
        public bool Known;
        public int WingsLogic;
        public int RocketBoots;
        public float WingTime;
        public int WingTimeMax;
        public int RocketTime;
        public int RocketTimeMax;
        public int RocketDelay;
        public bool CanRocket;
        public bool RocketRelease;
        public bool JustJumped;
    }

    public enum FlightPhase { Unsupported, JumpHold, WingPowered, RocketBatch, FeatherFall, Glide, Ballistic }

    public static class FlightMotion
    {
        /// <summary>
        /// Authorizes only the optional Up branch of Featherfall Potion.  The
        /// aggregate slowFall flag still participates in ordinary prediction
        /// for every vanilla source, but it cannot prove Buff 8 identity and it
        /// cannot prove that Up is free from gravity, mount, or grapple input
        /// semantics.
        /// </summary>
        public static bool CanRequestFeatherFallPotionUp(PlayerSnapshot player,
            MobilitySnapshot mobility)
        {
            if (player == null || mobility == null || !player.Jump.Known ||
                !player.Jump.SlowFall || !mobility.FeatherFall ||
                !mobility.FeatherFallPotionKnown ||
                !mobility.FeatherFallPotionActive || mobility.GravityInverted ||
                mobility.MountActive || mobility.Grappling ||
                !mobility.GravityFlip.Known) return false;
            var gravity = mobility.GravityFlip;
            return !gravity.GravControl && !gravity.GravControl2 &&
                gravity.ForcedGravity == 0;
        }

        public static float RemainingWingTicks(in FlightSnapshot flight)
        {
            if (!flight.Known) return 0f;
            var bonus = flight.RocketBoots > 0 ? flight.RocketTime * 6 : 0;
            return Math.Max(0f, bonus > 0 ? Math.Min(flight.WingTime + bonus, flight.WingTimeMax + bonus) : flight.WingTime);
        }

        public static float ResourceFraction(in FlightSnapshot flight)
        {
            if (!flight.Known) return 0f;
            var capacity = CapacityTicks(in flight);
            return capacity > 0 ? Math.Min(1f, RemainingWingTicks(in flight) / capacity) : 0f;
        }

        public static int CapacityTicks(in FlightSnapshot flight)
        {
            if (!flight.Known) return 0;
            return Math.Max(0, flight.WingTimeMax +
                (flight.RocketBoots > 0 ? flight.RocketTimeMax * 6 : 0));
        }

        // ResetEffects and the grounded cloud refresh occur before movement.
        public static void RefreshBeforeMovement(ref FlightSnapshot flight, ref JumpSnapshot jump, float velocityY)
        {
            if (!flight.Known || !jump.Known) return;
            flight.JustJumped = false;
            JumpMotion.RefreshBeforeMovement(ref jump, velocityY);
        }

        // Split from ApplyAfterJump so native pre/post-JumpMovement evidence
        // can verify both stages without calling game methods or mutating them.
        public static void ApplyJump(ref FlightSnapshot flight, ref JumpSnapshot jump,
            ref float velocityY, bool controlJump)
        {
            if (!flight.Known || !jump.Known) return;
            var beforeY = velocityY;
            var beforeTicks = jump.RemainingTicks;
            JumpMotion.ApplyJump(ref jump, ref velocityY, controlJump, false);
            if (!controlJump) flight.RocketRelease = true;
            else if (beforeTicks == 0 && jump.RemainingTicks > 0)
            {
                flight.JustJumped = beforeY == 0f;
                flight.CanRocket = false;
                flight.RocketRelease = false;
            }
        }

        // One native tick AFTER JumpMovement, BEFORE collision/position update.
        // There are no allocations, searches, engine calls or resource writes.
        public static FlightPhase ApplyAfterJump(ref FlightSnapshot flight, in JumpSnapshot jump,
            ref float velocityY, bool controlJump, bool controlDown, float gravity, float maxFallSpeed)
            => ApplyAfterJump(ref flight, in jump, ref velocityY, controlJump, false, controlDown,
                gravity, maxFallSpeed);

        // controlUp is separate from controlJump: feather fall uses the former
        // to select its one-tenth branch. Keep the legacy overload above so the
        // already audited no-feather traces retain their exact call contract.
        public static FlightPhase ApplyAfterJump(ref FlightSnapshot flight, in JumpSnapshot jump,
            ref float velocityY, bool controlJump, bool controlUp, bool controlDown,
            float gravity, float maxFallSpeed)
        {
            if (!flight.Known || !jump.Known) return FlightPhase.Unsupported;

            // Validate before touching any counter. A corrupt scalar must not
            // consume wing/rocket resources and then masquerade as a usable
            // prediction on the following tick.
            var validationVelocity = velocityY;
            if (JumpMotion.ApplyGravityChecked(ref validationVelocity, gravity, maxFallSpeed,
                false, false, false, false) == GravityPhase.Unsupported)
                return FlightPhase.Unsupported;

            if (flight.RocketBoots == 0) flight.RocketTime = 0;
            if (velocityY > -jump.Speed && velocityY != 0f) flight.CanRocket = true;
            if (velocityY == 0f && jump.ReleaseReady || jump.AutoJump && flight.JustJumped)
                flight.WingTime = flight.WingTimeMax;

            var powered = controlJump && flight.WingTime > 0f && jump.RemainingTicks == 0 && velocityY != 0f;
            if (powered)
            {
                velocityY = DemonThrust(velocityY, jump.Speed);
                flight.WingTime -= 1f;
            }

            // Conversion occurs after this tick's wing-powered decision. An
            // empty wing gains future fuel, not retroactive thrust this tick.
            if (flight.RocketBoots > 0 && velocityY != 0f && flight.RocketTime != 0)
            {
                var bonus = flight.RocketTime * 6;
                flight.WingTime = Math.Min(flight.WingTime + bonus, flight.WingTimeMax + bonus);
                flight.RocketTime = 0;
            }
            // Native restores this counter even without rocket equipment;
            // the next tick clears it again. Never treat that as usable flight.
            if (velocityY == 0f || jump.AutoJump && flight.JustJumped)
                flight.RocketTime = flight.RocketTimeMax;

            if (flight.WingTime == 0f && flight.RocketBoots > 0 && controlJump &&
                flight.RocketDelay == 0 && flight.CanRocket && flight.RocketRelease)
            {
                if (flight.RocketTime > 0) { flight.RocketTime--; flight.RocketDelay = 10; }
                else flight.CanRocket = false;
            }
            if (flight.RocketDelay > 0)
            {
                flight.RocketDelay--;
                velocityY = RocketThrust(velocityY, jump.Speed);
                velocityY = Math.Min(maxFallSpeed, velocityY);
                return FlightPhase.RocketBatch;
            }
            if (powered)
            {
                velocityY = Math.Min(maxFallSpeed, velocityY);
                return FlightPhase.WingPowered;
            }
            // Powered WingMovement and an active rocket batch have already
            // skipped gravity above. Otherwise 1.4.5.8 selects feather fall
            // before the unpowered wing-glide branch. Feather fall itself does
            // not consume either resource; Down bypasses it.
            if (jump.SlowFall && !controlDown)
            {
                var phase = JumpMotion.ApplyGravityChecked(ref velocityY, gravity, maxFallSpeed,
                    false, true, controlUp, false);
                return phase == GravityPhase.Unsupported ? FlightPhase.Unsupported : FlightPhase.FeatherFall;
            }
            if (controlJump && velocityY > 0f)
            {
                velocityY += gravity / 3f;
                if (!controlDown) velocityY = Math.Min(maxFallSpeed / 3f, velocityY);
                velocityY = Math.Min(maxFallSpeed, velocityY);
                return FlightPhase.Glide;
            }
            JumpMotion.ApplyGravityChecked(ref velocityY, gravity, maxFallSpeed,
                false, false, false, false);
            return jump.RemainingTicks > 0 ? FlightPhase.JumpHold : FlightPhase.Ballistic;
        }

        public static float DemonThrust(float velocityY, float jumpSpeed)
        {
            velocityY -= .1f;
            if (velocityY > 0f) velocityY -= .5f;
            else if (velocityY > -jumpSpeed * .5f) velocityY -= .1f;
            return Math.Max(velocityY, -jumpSpeed * 1.5f);
        }

        private static float RocketThrust(float velocityY, float jumpSpeed)
        {
            velocityY -= .1f;
            if (velocityY > 0f) velocityY -= .5f;
            // The native rocket comparison uses r8; WingMovement uses r4.
            else if ((double)velocityY > -(double)jumpSpeed * .5) velocityY -= .1f;
            return Math.Max(velocityY, -jumpSpeed * 1.5f);
        }
    }
}
