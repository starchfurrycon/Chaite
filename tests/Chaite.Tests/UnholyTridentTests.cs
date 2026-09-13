using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunUnholyTridentRegressions()
        {
            Run(nameof(UnholyTridentPinsNativeIdentityAndCadence),
                UnholyTridentPinsNativeIdentityAndCadence);
            Run(nameof(UnholyTridentModelsDelayedNativeDecayAndEarlyKill),
                UnholyTridentModelsDelayedNativeDecayAndEarlyKill);
            Run(nameof(UnholyTridentAimUsesThreeSubupdatesPerTick),
                UnholyTridentAimUsesThreeSubupdatesPerTick);
            Run(nameof(UnholyTridentRejectsLiveRouteDrift),
                UnholyTridentRejectsLiveRouteDrift);
        }

        private static WeaponProfileInput UnholyTridentInput(
            int rawMana = 19, int damage = 150, float manaMultiplier = 1f)
        {
            // Independent Terraria 1.4.5.8 Item.SetDefaults / Projectile
            // defaults, not values borrowed from the production catalog.
            return new WeaponProfileInput
            {
                WeaponId = 683,
                AmmoId = 0,
                ProjectileId = 114,
                ProjectileExtraUpdates = 2,
                ProjectileLifetimeSubupdates = 180,
                WeaponShootSpeed = 13f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = damage,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 0f,
                UseTime = 27,
                UseAnimation = 27,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = (int)(rawMana * manaMultiplier),
                ManaBaseCostKnown = true,
                ManaBaseCost = rawMana,
                ManaCostMultiplierKnown = true,
                ManaCostMultiplier = manaMultiplier
            };
        }

        private static void UnholyTridentPinsNativeIdentityAndCadence()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(683, 0, out profile));
            Equal(new WeaponProfileKey(683, 0), profile.Key);
            Equal(114, profile.ProjectileId);
            Equal("unholy-trident", profile.OutputRouteId);
            Equal(OutputRouteKind.StraightMagic, profile.OutputKind);
            Equal(OutputResourceKind.Mana, profile.ResourceKind);
            Equal(WeaponBallisticKind.UnholyTridentDecay,
                profile.Ballistics);
            Equal(150, profile.DefaultWeaponDamage);
            NearWeapon(13f, profile.DefaultWeaponShootSpeed);
            Equal(27, profile.DefaultUseTime);
            Equal(27, profile.DefaultUseAnimation);
            Equal(0, profile.DefaultReuseDelay);
            Equal(true, profile.DefaultAutoReuse);
            Equal(19, profile.DefaultManaCost);
            Equal(2, profile.DefaultExtraUpdates);
            Equal(180, profile.DefaultLifetimeSubupdates);
            Equal(WeaponSecondaryEffect.PenetrationNotCredited,
                profile.SecondaryEffect);

            var evaluation = WeaponProfileCatalog.Evaluate(
                UnholyTridentInput());
            True(evaluation.IsSupported, evaluation.Reason);
            Equal(150, evaluation.DirectDamage);
            NearWeapon(39f, evaluation.SpeedPixelsPerTick);
            NearWeapon(13f, evaluation.InitialSpeedPixelsPerSubupdate);
            Equal(3, evaluation.FirstTickProjectileUpdates);
            Equal(3, evaluation.SustainedProjectileUpdatesPerTick);
            NearWeapon(145f / 3f, evaluation.MaxFlightTicks);
            NearWeapon((float)(13d * UnholyTridentCatalog.MovementFactor(
                145d)), evaluation.ConservativeRangePixels, .01f);
            Equal(114, WeaponProfileCatalog.ResolveProjectileForPair(683,
                0, 114));
            // The facade's liquid proof is intentionally route-specific and
            // bounded: it is not a generic per-frame or whole-world scan.
            True(UnholyTridentCatalog.RequiresDryTrajectoryGate(683, 114));
            False(UnholyTridentCatalog.RequiresDryTrajectoryGate(683, 115));
            False(UnholyTridentCatalog.RequiresDryTrajectoryGate(682, 114));
            NearWeapon(900f, UnholyTridentCatalog.
                DryTrajectoryGateMaximumPathPixels);
            Equal(225, UnholyTridentCatalog.
                DryTrajectoryGateMaximumSamples);
            Equal(2034, UnholyTridentCatalog.
                DryTrajectoryGateMaximumTileReads);

            // The weaker world variant has a different raw Item.mana and
            // damage.  It remains valid only because both live values are
            // read, rather than silently hard-coding normal-world defaults.
            var weaker = WeaponProfileCatalog.Evaluate(UnholyTridentInput(9,
                77));
            True(weaker.IsSupported, weaker.Reason);
            Equal(77, weaker.DirectDamage);
            Equal(9, weaker.ManaCostPerUse);
        }

        private static void UnholyTridentModelsDelayedNativeDecayAndEarlyKill()
        {
            // ai[0] is incremented before the velocity branch.  Therefore
            // update 20, not update 19, is the first 0.98 movement segment.
            NearTridentDouble(0d, UnholyTridentCatalog.MovementFactor(0d));
            NearTridentDouble(19d, UnholyTridentCatalog.MovementFactor(19d));
            NearTridentDouble(19.980000019073486d,
                UnholyTridentCatalog.MovementFactor(20d));
            NearTridentDouble(20.940400056457521d,
                UnholyTridentCatalog.MovementFactor(21d));
            NearTridentDouble(64.156935120783694d,
                UnholyTridentCatalog.MovementFactor(145d), .000001d);
            NearTridentDouble(19.490000009536743d,
                UnholyTridentCatalog.MovementFactor(19.5d));

            // localAI[0] is a Single recurrence.  The 126th decayed segment
            // is still live; the 127th is the native Kill threshold, even
            // though projectile.timeLeft would otherwise allow 180 updates.
            True(UnholyTridentCatalog.StoredSpeedAfterDecays(126) >= 1f);
            True(UnholyTridentCatalog.StoredSpeedAfterDecays(127) < 1f);
            Equal(145, UnholyTridentCatalog.LastDamagingSubupdate);
            True(double.IsNaN(UnholyTridentCatalog.MovementFactor(146d)));
            True(double.IsNaN(UnholyTridentCatalog.MovementFactor(
                double.NaN)));
        }

        private static void UnholyTridentAimUsesThreeSubupdatesPerTick()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                UnholyTridentInput());
            var stationary = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(500f, 0f), new Vec2(), 90f);
            True(stationary.CanFire, stationary.Status.ToString());
            // The solution lies 0.0529353 through native subupdate 45: the
            // fractional segment is linear movement at 0.98^26, not a
            // continuous exponential interpolation.
            NearWeapon(14.684312f, stationary.LeadTicks, .0001f);
            NearWeapon(500f, (float)(13d *
                UnholyTridentCatalog.MovementFactor(
                    stationary.LeadTicks * 3d)), .0002f);

            // Place a target exactly on the horizontal native path at 60
            // subupdates (20 ticks).  A nominal 39px/tick straight solver
            // cannot recover this delayed-decay arrival.
            var arrival = (float)(13d * UnholyTridentCatalog.MovementFactor(
                60d));
            var target = new Vec2(arrival - 20f, 5f);
            var velocity = new Vec2(1f, -.25f);
            var moving = WeaponAimSolver.Solve(evaluation, new Vec2(),
                target, velocity, 90f);
            True(moving.CanFire, moving.Status.ToString());
            NearWeapon(20f, moving.LeadTicks, .0001f);
            NearWeapon(arrival, moving.AimWorld.X, .0002f);
            NearWeapon(0f, moving.AimWorld.Y, .0002f);

            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(500f, 0f), new Vec2(), 14f).Status);
            Equal(WeaponAimStatus.BeyondLifetime,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(900f, 0f), new Vec2(), 90f).Status);
        }

        private static void UnholyTridentRejectsLiveRouteDrift()
        {
            var input = UnholyTridentInput();
            input.ProjectileId = 115;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = UnholyTridentInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.ProjectileLifetimeSubupdates = 179;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.WeaponShootSpeed = 13.1f;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.UseAnimation = 28;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = UnholyTridentInput();
            input.ManaBaseCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.ManaBaseCost = 18;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.ManaCostMultiplierKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.ManaCostMultiplier = .8f;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.ManaCostPerUse = 18;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);

            // A legitimately read mana-cost modifier is valid only when the
            // resulting effective cost matches native Single multiplication.
            input = UnholyTridentInput(19, 150, .8f);
            True(WeaponProfileCatalog.Evaluate(input).IsSupported);

            input = UnholyTridentInput();
            input.AmmoBaseDamage = 1;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.HasAmmo = false;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = UnholyTridentInput();
            input.WeaponDamageAfterModifiers = 0;
            Equal(WeaponProfileStatus.InvalidDamage,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void NearTridentDouble(double expected, double actual,
            double tolerance = .0000001d)
        {
            True(Math.Abs(expected - actual) <= tolerance,
                "Unholy Trident numeric mismatch: expected " + expected +
                ", actual " + actual);
        }
    }
}
