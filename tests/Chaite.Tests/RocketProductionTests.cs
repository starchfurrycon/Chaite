using Chaite.Core;
using System;

namespace Chaite.Tests
{
    public static class RocketProductionTests
    {
        public static int RunAll()
        {
            var count = 0;
            Run(ExactRocketRouteIsAdmitted, ref count);
            Run(OutputRouteAdmitsExactRocketPair, ref count);
            Run(OtherRocketPairsAndDriftFailClosed, ref count);
            Run(RocketAimUsesAcceleratingTrace, ref count);
            return count;
        }

        private static void ExactRocketRouteIsAdmitted()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(759, 771, out profile));
            Equal(134, profile.ProjectileId);
            Equal(WeaponBallisticKind.RocketAcceleration, profile.Ballistics);
            var evaluation = WeaponProfileCatalog.Evaluate(Input());
            True(evaluation.IsSupported, evaluation.Reason);
            Equal(95, evaluation.DirectDamage);
            Equal(134, WeaponProfileCatalog.ResolveProjectileForPair(759, 771, 0));
        }

        private static void OtherRocketPairsAndDriftFailClosed()
        {
            WeaponProfile profile;
            False(WeaponProfileCatalog.TryGet(759, 772, out profile));
            var wrongProjectile = Input();
            wrongProjectile.ProjectileId = 137;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(wrongProjectile).Status);
            var wrongLifetime = Input();
            wrongLifetime.ProjectileLifetimeSubupdates = 181;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(wrongLifetime).Status);
        }

        private static void OutputRouteAdmitsExactRocketPair()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(Input());
            var snapshot = new CombatSnapshot
            {
                Weapon = new WeaponSnapshot
                {
                    Slot = 0, WeaponId = 759, AmmoId = 771,
                    ProjectileId = 134, IsProjectile = true,
                    IsUsable = true, HasAmmo = true,
                    NativeProfileRequired = true, Profile = evaluation
                }
            };
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason), reason);
            Equal(OutputRouteKind.StraightRanged, route.Kind);
            Equal(759, route.WeaponId);
            Equal(771, route.AmmoId);
        }

        private static void RocketAimUsesAcceleratingTrace()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(Input());
            var solution = WeaponAimSolver.Solve(evaluation,
                new Vec2(0f, 0f), new Vec2(300f, 0f),
                new Vec2(0f, 0f), 90f, 20, 20);
            True(solution.CanFire);
            True(solution.LeadTicks > 20f && solution.LeadTicks < 40f,
                "unexpected rocket flight time " + solution.LeadTicks);
        }

        private static WeaponProfileInput Input()
        {
            return new WeaponProfileInput
            {
                WeaponId = 759, AmmoId = 771, ProjectileId = 134,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 180,
                WeaponShootSpeed = 5f, AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = 55, AmmoBaseDamage = 40,
                AmmoDamageMultiplier = 1f, UseTime = 30, UseAnimation = 30,
                ReuseDelay = 0, AnimationRemainingAtShot = 0,
                AutoReuse = true, HasAmmo = true
            };
        }

        private static void Run(Action action, ref int count)
        {
            action();
            count++;
        }

        private static void True(bool value, string message = null)
        {
            if (!value) throw new InvalidOperationException(message ?? "expected true");
        }

        private static void False(bool value)
        {
            if (value) throw new InvalidOperationException("expected false");
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }
    }
}
