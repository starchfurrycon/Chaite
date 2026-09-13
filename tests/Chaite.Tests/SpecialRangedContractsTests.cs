using System;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// Pure contracts for the audited special-ranged family.  These tests never
    /// launch Terraria or touch input; they pin the native facts at the planner
    /// boundary so a later adapter can be tested independently.
    /// </summary>
    public static class SpecialRangedContractsTests
    {
        public static int RunAll()
        {
            var count = 0;
            Run(LauncherCatalogIsExplicit, ref count);
            Run(AmmoCatalogAndMappingAreExplicit, ref count);
            Run(WorldMutatingAmmoFailsClosed, ref count);
            Run(ElectrosphereQuantizesAndFixesTarget, ref count);
            Run(ElectrosphereDetonatesWithoutDirectDamage, ref count);
            Run(ElectrosphereAreaPulsesAndExpires, ref count);
            Run(ElectrosphereOverlapUsesEnvelope, ref count);
            Run(StraightRocketAcceleratesOnlyBelowThreshold, ref count);
            Run(StraightRocketRequiresCollisionEvidence, ref count);
            Run(GrenadeGravityStartsOnUpdateSixteen, ref count);
            Run(SnowmanSearchAndRetentionBoundaries, ref count);
            Run(CelebrationSequenceHasTenRocketsAndOneAmmoPick, ref count);
            Run(SteadyElectrosphereAdvanceAllocatesNothing, ref count);
            return count;
        }

        private static void LauncherCatalogIsExplicit()
        {
            var expected = new[] { 758, 759, 760, 1946, 2796, 2797, 3475, 3930 };
            for (var i = 0; i < expected.Length; i++)
            {
                SpecialRangedLauncherProfile profile;
                True(SpecialRangedNativeCatalog.TryGetLauncher(expected[i], out profile));
                Equal(expected[i], profile.WeaponId);
                True(profile.IsKnown);
            }
            SpecialRangedLauncherProfile unknown;
            False(SpecialRangedNativeCatalog.TryGetLauncher(999999, out unknown));
        }

        private static void AmmoCatalogAndMappingAreExplicit()
        {
            var expected = new[] { 771, 772, 773, 774, 4445, 4446, 4447, 4448,
                4449, 4457, 4458, 4459 };
            for (var i = 0; i < expected.Length; i++)
            {
                RocketAmmoProfile ammo;
                True(SpecialRangedNativeCatalog.TryGetAmmo(expected[i], out ammo));
                True(ammo.IsKnown);
                SpecialRangedProjectileContract contract;
                True(SpecialRangedNativeCatalog.TryResolveProjectile(2796,
                    expected[i], out contract));
                Equal(442, contract.ProjectileId);
                Equal(2796, contract.WeaponId);
                Equal(expected[i], contract.AmmoId);
                False((contract.Safety & SpecialRangedSafetyFlags.WorldMutation) != 0);
            }

            // The mapped projectiles are launcher-specific bomb parents.  Pin
            // each native explosion family so a planner does not overestimate
            // the hitbox (or treat a small liquid projectile as a nuke).
            foreach (var launcherId in new[] { 758, 759, 760 })
            {
                AssertExplosionSize(launcherId, 771, 128);
                AssertExplosionSize(launcherId, 773, 200);
                AssertExplosionSize(launcherId, 4445, 128); // cluster parent
                AssertExplosionSize(launcherId, 4447, 48);  // wet/liquid family
                AssertExplosionSize(launcherId, 4457, 250); // Mini Nuke I
                AssertExplosionSize(launcherId, 4458, 250); // Mini Nuke II
                AssertExplosionSize(launcherId, 4459, 48);  // Dry Rocket
            }
            AssertExplosionSize(1946, 771, 128);
            AssertExplosionSize(1946, 773, 200);
            AssertExplosionSize(1946, 4445, 128);
            AssertExplosionSize(1946, 4447, 48);
            AssertExplosionSize(1946, 4457, 250);
            // Native PrepareBombToBlow omits projectile 809 in this exact
            // white-listed build, while tile destruction still sees radius 7.
            AssertExplosionSize(1946, 4458, 14, 7);
            AssertExplosionSize(1946, 4459, 48);

            // The two audited held-projectile weapons intentionally require their
            // own live state contracts and are not guessed here.
            SpecialRangedProjectileContract unsupported;
            False(SpecialRangedNativeCatalog.TryResolveProjectile(2797, 771,
                out unsupported));
            False(SpecialRangedNativeCatalog.TryResolveProjectile(3475, 771,
                out unsupported));
        }

        private static void WorldMutatingAmmoFailsClosed()
        {
            foreach (var ammoId in new[] { 772, 774, 4446, 4447, 4448, 4449,
                4458, 4459 })
            {
                SpecialRangedProjectileContract contract;
                True(SpecialRangedNativeCatalog.TryResolveProjectile(759,
                    ammoId, out contract));
                True((contract.Safety & SpecialRangedSafetyFlags.WorldMutation) != 0);
                False(contract.CanUseForBossByDefault);
            }
            SpecialRangedProjectileContract safe;
            True(SpecialRangedNativeCatalog.TryResolveProjectile(759, 771,
                out safe));
            False((safe.Safety & SpecialRangedSafetyFlags.WorldMutation) != 0);
            True(safe.CanUseForBossByDefault);

            // Celebration folds liquid/Dry ammo into ordinary 717 and does not
            // inherit the four-launcher liquid world side effect.
            True(SpecialRangedNativeCatalog.TryResolveProjectile(3930, 4448,
                out safe));
            Equal(717, safe.ProjectileId);
            Equal(240, safe.ExplosionWidth);
            Equal(240, safe.ExplosionHeight);
            False((safe.Safety & SpecialRangedSafetyFlags.WorldMutation) != 0);

            AssertExplosionSize(3930, 771, 128, 0);
            AssertExplosionSize(3930, 772, 128, 3);
            AssertExplosionSize(3930, 773, 240, 0);
            AssertExplosionSize(3930, 774, 240, 5);
        }

        private static void ElectrosphereQuantizesAndFixesTarget()
        {
            int tileX;
            int tileY;
            True(ElectrospherePlacement.TryQuantizeTarget(
                new Vec2(31.99f, -0.01f), out tileX, out tileY));
            Equal(1, tileX);
            Equal(-1, tileY);
            var center = ElectrospherePlacement.TileCenter(tileX, tileY);
            Near(24f, center.X);
            Near(-8f, center.Y);

            var request = new ElectrospherePlacementRequest
            {
                Known = true,
                Origin = new Vec2(0f, 8f),
                Velocity = new Vec2(12f, 0f),
                TargetTileX = 4,
                TargetTileY = 0,
                Damage = 80,
                TimeLeft = 600,
                Owner = 0
            };
            ElectrospherePlacementState state;
            string reason;
            True(ElectrospherePlacement.TryCreate(in request, out state,
                out reason), reason);
            var collision = new ElectrosphereCollision { Known = true };
            ElectrospherePlacementStep step;
            for (var i = 0; i < 6; i++)
            {
                True(ElectrospherePlacement.TryAdvance(ref state,
                    in collision, out step));
                if (i == 0) Near(12f, state.Position.X);
            }
            // The target is the recorded tile, not a later mouse position. On
            // the sixth update the native pre-move radius check sees the tile
            // centre and enters 443 before applying another velocity step.
            Equal(ElectrospherePlacementPhase.Area443, state.Phase);
            Near(60f, state.AreaCenter.X);
        }

        private static void ElectrosphereDetonatesWithoutDirectDamage()
        {
            var request = new ElectrospherePlacementRequest
            {
                Known = true, Origin = new Vec2(0f, 0f), Velocity = new Vec2(4f, 0f),
                TargetTileX = 0, TargetTileY = 0, Damage = 120, TimeLeft = 600, Owner = 2
            };
            ElectrospherePlacementState state;
            string reason;
            True(ElectrospherePlacement.TryCreate(in request, out state,
                out reason), reason);
            var collision = new ElectrosphereCollision
            {
                Known = true,
                NpcHit = true,
                ExistingAreaIntersects = true
            };
            ElectrospherePlacementStep step;
            True(ElectrospherePlacement.TryAdvance(ref state, in collision,
                out step));
            Equal(ElectrospherePlacementEvent.Detonated, step.Event);
            Equal(0, step.DirectDamage);
            Equal(120, step.AreaDamage);
            Near(0f, step.Knockback);
            True(step.RetireOldArea);
        }

        private static void ElectrosphereAreaPulsesAndExpires()
        {
            var request = new ElectrospherePlacementRequest
            {
                Known = true, Origin = new Vec2(0f, 0f), Velocity = new Vec2(1f, 0f),
                TargetTileX = 0, TargetTileY = 0, Damage = 80, TimeLeft = 600, Owner = 0
            };
            ElectrospherePlacementState state;
            string reason;
            True(ElectrospherePlacement.TryCreate(in request, out state,
                out reason), reason);
            var hit = new ElectrosphereCollision { Known = true, TargetReached = true };
            ElectrospherePlacementStep step;
            True(ElectrospherePlacement.TryAdvance(ref state, in hit, out step));
            Equal(ElectrospherePlacementPhase.Area443, state.Phase);
            var minimum = float.MaxValue;
            var maximum = float.MinValue;
            var areaTicks = 0;
            var noCollision = new ElectrosphereCollision { Known = true };
            while (state.Phase == ElectrospherePlacementPhase.Area443)
            {
                True(ElectrospherePlacement.TryAdvance(ref state, in noCollision,
                    out step));
                if (step.AreaWidth > 0f)
                {
                    minimum = Math.Min(minimum, step.AreaWidth);
                    maximum = Math.Max(maximum, step.AreaWidth);
                }
                areaTicks++;
                True(areaTicks <= 301);
            }
            Equal(300, areaTicks);
            True(minimum >= ElectrospherePlacement.AreaMinimumSize);
            True(maximum <= ElectrospherePlacement.AreaMaximumSize);
            Equal(ElectrospherePlacementPhase.Retired, state.Phase);
        }

        private static void ElectrosphereOverlapUsesEnvelope()
        {
            True(ElectrospherePlacement.AreasOverlap(new Vec2(0, 0),
                new Vec2(79.9f, 0f)));
            False(ElectrospherePlacement.AreasOverlap(new Vec2(0, 0),
                new Vec2(80.1f, 0f)));
            var bounds = ElectrospherePlacement.AreaBounds(new Vec2(10, 20), 62);
            Near(-21f, bounds.Left);
            Near(-11f, bounds.Top);
            Near(62f, bounds.Width);
        }

        private static void StraightRocketAcceleratesOnlyBelowThreshold()
        {
            StraightRocketTrajectoryState state;
            string reason;
            True(StraightRocketTrajectory.TryCreate(new Vec2(0, 0),
                new Vec2(10, 0), 134, 180, true, out state, out reason), reason);
            var collision = new StraightRocketCollision { Known = true };
            StraightRocketStep step;
            True(StraightRocketTrajectory.TryAdvance(ref state, in collision,
                out step));
            Near(11f, state.Velocity.X);
            Near(11f, state.Position.X);

            state.Velocity = new Vec2(15f, 0f);
            True(StraightRocketTrajectory.TryAdvance(ref state, in collision,
                out step));
            Near(15f, state.Velocity.X);
        }

        private static void StraightRocketRequiresCollisionEvidence()
        {
            StraightRocketTrajectoryState state;
            string reason;
            True(StraightRocketTrajectory.TryCreate(new Vec2(0, 0),
                new Vec2(5, 0), 134, 180, true, out state, out reason));
            var before = state.Position;
            StraightRocketStep step;
            False(StraightRocketTrajectory.TryAdvance(ref state,
                new StraightRocketCollision { Known = false }, out step));
            Near(before.X, state.Position.X);
            Equal(SpecialRangedTrajectoryPhase.Unsupported, step.Phase);
            True(StraightRocketTrajectory.TryAdvance(ref state,
                new StraightRocketCollision { Known = true, TileHit = true }, out step));
            True(step.Exploded);
            Equal(SpecialRangedTrajectoryPhase.Impacted, step.Phase);
        }

        private static void GrenadeGravityStartsOnUpdateSixteen()
        {
            GrenadeTrajectoryState state;
            string reason;
            True(GrenadeTrajectory.TryCreate(new Vec2(0, 0),
                new Vec2(0, 0), 133, 180, out state, out reason), reason);
            var noCollision = new GrenadeCollision { Known = true };
            GrenadeTrajectoryStep step;
            for (var i = 0; i < 15; i++)
            {
                True(GrenadeTrajectory.TryAdvance(ref state, in noCollision,
                    out step));
                Near(0f, state.Velocity.Y);
            }
            True(GrenadeTrajectory.TryAdvance(ref state, in noCollision,
                out step));
            Near(.2f, state.Velocity.Y);
            Near(.2f, state.Position.Y);
            var landing = new GrenadeCollision
            {
                Known = true, TileHit = true, Landed = true,
                HasNormal = true, Normal = new Vec2(0, -1)
            };
            True(GrenadeTrajectory.TryAdvance(ref state, in landing, out step));
            Near(0f, state.Velocity.X);
            True(state.Velocity.Y < 0f);
        }

        private static void SnowmanSearchAndRetentionBoundaries()
        {
            var candidates = new[]
            {
                new SnowmanTargetObservation
                {
                    Known = true, Key = 3, Center = new Vec2(500, 0),
                    Velocity = new Vec2(1, 0), Chaseable = true,
                    LineOfSightKnown = true, HasLineOfSight = true
                },
                new SnowmanTargetObservation
                {
                    Known = true, Key = 4, Center = new Vec2(100, 0),
                    Velocity = new Vec2(0, 1), Chaseable = true,
                    LineOfSightKnown = true, HasLineOfSight = true
                }
            };
            SnowmanTargetSelection selection;
            True(SnowmanTrajectory.TrySelectTarget(new Vec2(0, 0), candidates,
                candidates.Length, 600, out selection), "selection call");
            True(selection.Found, "selection found");
            Equal(4, selection.Key);

            SnowmanTrajectoryState state;
            string reason;
            True(SnowmanTrajectory.TryCreate(new Vec2(0, 0), new Vec2(0, 0),
                338, 180, out state, out reason), reason ?? "snowman create");
            var noCollision = new SnowmanCollision { Known = true };
            SnowmanTrajectoryStep step;
            for (var i = 0; i < SnowmanTrajectory.SearchStartsAfterUpdate; i++)
                True(SnowmanTrajectory.TryAdvance(ref state, candidates,
                    candidates.Length, in noCollision, out step), "pre-search advance " + i);
            False(state.HasTarget, "target acquired too early");
            True(SnowmanTrajectory.TryAdvance(ref state, candidates,
                candidates.Length, in noCollision, out step), "search advance");
            True(state.HasTarget, "target not acquired");
            Equal(4, state.TargetKey);

            var far = candidates;
            // The projectile has already moved toward the old target; use a
            // clearly out-of-range point so the post-move position cannot make
            // an intended retention-boundary test ambiguous.
            far[1].Center = new Vec2(2000, 0);
            True(SnowmanTrajectory.TryAdvance(ref state, far, far.Length,
                in noCollision, out step), "retention advance");
            False(state.HasTarget, "target retained beyond range");
        }

        private static void CelebrationSequenceHasTenRocketsAndOneAmmoPick()
        {
            CelebrationHeldState state;
            string reason;
            True(CelebrationMk2Trajectory.TryCreate(0, 0, out state,
                out reason), reason);
            var emitted = 0;
            var picks = 0;
            var counts = 0;
            for (var i = 0; i < CelebrationMk2Trajectory.StateCount; i++)
            {
                CelebrationEventPlan plan;
                True(CelebrationMk2Trajectory.TryAdvance(ref state, true, 771,
                    out plan));
                True(plan.Emitted);
                emitted++;
                picks += plan.AmmoPickPerformed ? 1 : 0;
                counts += plan.RocketCount;
                True(plan.MaxAmmoConsumed <= 1);
                True(plan.AimSpreadDegrees <= CelebrationMk2Trajectory.WorstCaseAimSpreadDegrees);
                for (var tick = 0; tick < CelebrationMk2Trajectory.EventPeriod; tick++)
                    True(CelebrationMk2Trajectory.TryAdvance(ref state, true, 771,
                        out plan));
            }
            Equal(7, emitted);
            Equal(7, picks);
            Equal(10, counts);
            Equal(10, CelebrationMk2Trajectory.RocketsInFullCycle());
        }

        private static void SteadyElectrosphereAdvanceAllocatesNothing()
        {
            var request = new ElectrospherePlacementRequest
            {
                Known = true, Origin = new Vec2(0, 0), Velocity = new Vec2(1, 0),
                TargetTileX = 1000, TargetTileY = 0, Damage = 80, TimeLeft = 600, Owner = 0
            };
            ElectrospherePlacementState state;
            string reason;
            True(ElectrospherePlacement.TryCreate(in request, out state,
                out reason), reason);
            var collision = new ElectrosphereCollision { Known = true };
            ElectrospherePlacementStep step;
            // Warm JIT and static initialization before sampling the hot loop.
            ElectrospherePlacement.TryAdvance(ref state, in collision, out step);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0;
            for (var i = 0; i < 64; i++)
            {
                if (!ElectrospherePlacement.TryAdvance(ref state, in collision,
                    out step)) break;
                checksum += (int)step.Event;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated);
            True(checksum > 0);
        }

        private static void Run(Action test, ref int count)
        {
            try
            {
                test();
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("special-ranged subtest " +
                    test.Method.Name + ": " + error.Message, error);
            }
            count++;
        }

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private static void True(bool value, string reason = null)
        {
            if (!value) throw new InvalidOperationException(reason ?? "expected true");
        }

        private static void False(bool value, string reason = null) => True(!value, reason);

        private static void AssertExplosionSize(int launcherId, int ammoId,
            int expectedSize, int expectedRadius = -1)
        {
            SpecialRangedProjectileContract contract;
            True(SpecialRangedNativeCatalog.TryResolveProjectile(launcherId,
                ammoId, out contract), "missing projectile mapping");
            Equal(expectedSize, contract.ExplosionWidth);
            Equal(expectedSize, contract.ExplosionHeight);
            if (expectedRadius >= 0)
                Equal(expectedRadius, contract.TileDestructionRadius);
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }
    }
}
