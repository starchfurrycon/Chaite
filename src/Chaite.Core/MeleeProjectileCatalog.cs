using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Reviewed native projectile families whose position is not a free-flying
    /// straight line.  The catalog intentionally contains only profiles with
    /// an explicit Item.SetDefaults and Projectile AI contract.
    /// </summary>
    public static class MeleeProjectileCatalog
    {
        public const int EnchantedBoomerangWeaponId = 55;
        public const int FlamarangWeaponId = 119;
        public const int ThornChakramWeaponId = 191;
        public const int WoodenBoomerangWeaponId = 284;
        public const int TridentWeaponId = 277;
        public const int SpearWeaponId = 280;
        public const int IceBoomerangWeaponId = 670;
        public const int LightDiscWeaponId = 561;
        public const int BananarangWeaponId = 1324;
        public const int FruitcakeChakramWeaponId = 1918;
        public const int MushroomSpearWeaponId = 756;
        public const int TitaniumTridentWeaponId = 1200;
        public const int ThunderSpearWeaponId = 4061;
        public const int SlimeSpearWeaponId = 5687;
        public const int WoodYoyoWeaponId = 3278;

        public const int EnchantedBoomerangProjectileId = 6;
        public const int FlamarangProjectileId = 19;
        public const int ThornChakramProjectileId = 33;
        public const int WoodenBoomerangProjectileId = 52;
        public const int TridentProjectileId = 47;
        public const int SpearProjectileId = 49;
        public const int IceBoomerangProjectileId = 113;
        public const int LightDiscProjectileId = 106;
        public const int BananarangProjectileId = 272;
        public const int FruitcakeChakramProjectileId = 333;
        public const int MushroomSpearProjectileId = 130;
        public const int TitaniumTridentProjectileId = 218;
        public const int ThunderSpearProjectileId = 730;
        public const int SlimeSpearProjectileId = 1103;
        public const int WoodYoyoProjectileId = 541;

        private enum Model
        {
            Boomerang,
            Spear,
            Yoyo
        }

        private struct Definition
        {
            public int WeaponId;
            public int ProjectileId;
            public string Slug;
            public string Name;
            public int Damage;
            public int UseTime;
            public int UseAnimation;
            public float ShootSpeed;
            public bool AutoReuse;
            public float ReachPixels;
            public float SolverSpeed;
            public int SolverLifetimeTicks;
            public int ContactWindowTicks;
            public int LifetimeSubupdates;
            public Model Kind;
        }

        private static readonly Definition[] Definitions =
        {
            // AI_003_Boomerang increments ai[1] until 30 native updates,
            // then enters its owner-seeking return phase.  The solver credits
            // only this deterministic outbound window; return hits are never
            // used to justify a shot or a DPS threshold.
            DefinitionFor(EnchantedBoomerangWeaponId,
                EnchantedBoomerangProjectileId, "enchanted-boomerang",
                "Enchanted Boomerang", 17, 20, 20, 10f, false, 300f,
                10f, 30, 30, Model.Boomerang),
            DefinitionFor(FlamarangWeaponId, FlamarangProjectileId,
                "flamarang", "Flamarang", 49, 20, 20, 14f, false, 420f,
                14f, 30, 30, Model.Boomerang),
            // Thorn Chakram, Wooden Boomerang, and Ice Boomerang all use the
            // native AI_003 outbound/return FSM. Their conservative contact
            // envelope is the deterministic 30-update outbound prefix.
            DefinitionFor(ThornChakramWeaponId, ThornChakramProjectileId,
                "thorn-chakram", "Thorn Chakram", 25, 15, 15, 14f, false,
                420f, 14f, 30, 30, Model.Boomerang),
            DefinitionFor(WoodenBoomerangWeaponId,
                WoodenBoomerangProjectileId, "wooden-boomerang",
                "Wooden Boomerang", 10, 20, 20, 6.5f, false, 195f, 6.5f,
                30, 30, Model.Boomerang),
            DefinitionFor(IceBoomerangWeaponId, IceBoomerangProjectileId,
                "ice-boomerang", "Ice Boomerang", 21, 20, 20, 11.5f, false,
                345f, 11.5f, 30, 30, Model.Boomerang),
            // Light Disc is the one reviewed AI_003 variant whose outbound
            // phase lasts 45 updates. It is auto-reuse, so the native held
            // input may remain asserted between animation cycles.
            DefinitionFor(LightDiscWeaponId, LightDiscProjectileId,
                "light-disc", "Light Disc", 60, 14, 14, 16f, true, 720f,
                16f, 45, 45, Model.Boomerang),
            // Bananarang is another auto-reuse AI_003 projectile. Its normal
            // return threshold remains the standard 30 updates.
            DefinitionFor(BananarangWeaponId, BananarangProjectileId,
                "bananarang", "Bananarang", 45, 11, 11, 16f, true, 480f,
                16f, 30, 30, Model.Boomerang),
            // Fruitcake Chakram is another ordinary AI_003 projectile. Its
            // primary path has the same 30-update outbound boundary; visual
            // effects are intentionally outside the damage contract.
            DefinitionFor(FruitcakeChakramWeaponId,
                FruitcakeChakramProjectileId, "fruitcake-chakram",
                "Fruitcake Chakram", 19, 15, 15, 11f, false, 330f, 11f,
                30, 30, Model.Boomerang),
            // AI_019_Spears rebinds the projectile center to MountedCenter on
            // every update.  The range is a conservative body-contact radius,
            // not a fabricated projectile flight distance.
            DefinitionFor(TridentWeaponId, TridentProjectileId, "trident",
                "Trident", 14, 31, 31, 4f, false, 40f, 4f, 31, 31,
                Model.Spear),
            DefinitionFor(SpearWeaponId, SpearProjectileId, "spear", "Spear",
                8, 31, 31, 3.7f, false, 40f, 3.7f, 31, 31, Model.Spear),
            // Mushroom Spear emits deterministic-timed spore projectiles from
            // its primary spear. Only the owner-anchored spear is admitted;
            // spores are recorded as an uncredited secondary effect.
            DefinitionFor(MushroomSpearWeaponId, MushroomSpearProjectileId,
                "mushroom-spear", "Mushroom Spear", 60, 40, 40, 5.5f, false,
                40f, 5.5f, 40, 40, Model.Spear),
            DefinitionFor(TitaniumTridentWeaponId,
                TitaniumTridentProjectileId, "titanium-trident",
                "Titanium Trident", 48, 23, 23, 5f, false, 40f, 5f,
                23, 23, Model.Spear),
            // Thunder Spear creates one independent lightning projectile at
            // launch. The primary spear remains an exact AI_019 path; the
            // lightning damage is deliberately not credited.
            DefinitionFor(ThunderSpearWeaponId, ThunderSpearProjectileId,
                "thunder-spear", "Thunder Spear", 14, 28, 28, 3.5f, false,
                40f, 3.5f, 28, 28, Model.Spear),
            DefinitionFor(SlimeSpearWeaponId, SlimeSpearProjectileId,
                "slime-spear", "Slime Spear", 12, 24, 24, 5.5f, false,
                40f, 5.5f, 24, 24, Model.Spear),
            // Projectile 541 is the Wood Yoyo.  Vanilla's set values are a
            // three-second lifetime multiplier, 130 px maximum range and
            // 9 px/tick top speed.  While channelled, AI_099 keeps timeLeft at
            // six; the profile lifetime below is the reviewed base multiplier.
            DefinitionFor(WoodYoyoWeaponId, WoodYoyoProjectileId,
                "wood-yoyo", "Wood Yoyo", 9, 25, 25, 16f, false, 130f,
                9f, 180, 180, Model.Yoyo)
        };

        private static readonly Dictionary<WeaponProfileKey, WeaponProfile>
            Profiles = BuildProfiles();

        public static int Count => Profiles.Count;

        public static bool IsWeapon(int weaponId)
        {
            for (var i = 0; i < Definitions.Length; i++)
                if (Definitions[i].WeaponId == weaponId) return true;
            return false;
        }

        public static bool TryGet(int weaponId, int ammoId,
            out WeaponProfile profile)
        {
            return Profiles.TryGetValue(new WeaponProfileKey(weaponId, ammoId),
                out profile);
        }

        /// <summary>
        /// Read-only fallback for adapters which cannot access
        /// ContentSamples.ProjectilesByType.  No projectile is instantiated.
        /// </summary>
        public static bool TryGetProjectileDefaults(int projectileId,
            out int extraUpdates, out int lifetimeSubupdates)
        {
            for (var i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].ProjectileId != projectileId) continue;
                extraUpdates = 0;
                lifetimeSubupdates = Definitions[i].LifetimeSubupdates;
                return true;
            }
            extraUpdates = 0;
            lifetimeSubupdates = 0;
            return false;
        }

        internal static bool IsMeleeProfile(WeaponProfile profile)
        {
            return profile != null &&
                (profile.Ballistics == WeaponBallisticKind.BoomerangReturn ||
                 profile.Ballistics == WeaponBallisticKind.SpearOwnerAnchored ||
                 profile.Ballistics == WeaponBallisticKind.YoyoCursorAnchored);
        }

        internal static WeaponProfileEvaluation Evaluate(
            WeaponProfileInput input, WeaponProfile profile)
        {
            if (profile == null || !IsMeleeProfile(profile))
                return Rejected(WeaponProfileStatus.UnsupportedCombination,
                    profile);
            if (input.WeaponId != profile.Key.WeaponId || input.AmmoId != 0)
                return Rejected(WeaponProfileStatus.UnsupportedCombination,
                    profile);
            if (!input.HasAmmo)
                return Rejected(WeaponProfileStatus.MissingAmmo, profile);
            if (input.ProjectileId != profile.ProjectileId)
                return Rejected(WeaponProfileStatus.ProjectileMismatch,
                    profile);

            Definition definition;
            if (!TryGetDefinition(profile.Key.WeaponId, out definition))
                return Rejected(WeaponProfileStatus.UnsupportedCombination,
                    profile);
            if (input.ProjectileExtraUpdates != 0 ||
                input.ProjectileLifetimeSubupdates !=
                    definition.LifetimeSubupdates ||
                !FinitePositive(input.WeaponShootSpeed) ||
                Math.Abs(input.WeaponShootSpeed - definition.ShootSpeed) >
                    0.0001f ||
                !FiniteZeroOrPositive(input.AmmoShootSpeed) ||
                input.AmmoShootSpeed != 0f || input.AmmoBaseDamage != 0 ||
                !FiniteZeroOrPositive(input.AmmoDamageMultiplier))
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            if (input.WeaponDamageAfterModifiers < 1)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (input.UseTime != definition.UseTime ||
                input.UseAnimation != definition.UseAnimation ||
                input.ReuseDelay != 0 ||
                input.AutoReuse != definition.AutoReuse ||
                input.AnimationRemainingAtShot < 0 ||
                input.AnimationRemainingAtShot > definition.UseAnimation)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);

            var damage = input.WeaponDamageAfterModifiers;
            var cycle = definition.Kind == Model.Yoyo ? definition.UseTime :
                definition.UseAnimation + (definition.AutoReuse ? 0 : 1);
            if (cycle < 1) return Rejected(WeaponProfileStatus.InvalidTiming,
                profile);
            var dps = damage * (60f / cycle);
            if (!FinitePositive(dps) || !FinitePositive(definition.ReachPixels))
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);

            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported,
                Profile = profile,
                SpeedPixelsPerTick = definition.SolverSpeed,
                MaxFlightTicks = definition.SolverLifetimeTicks,
                ConservativeRangePixels = definition.ReachPixels,
                DirectDamage = damage,
                ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = 0f,
                BurstShotIndex = 0,
                ManaCostPerUse = 0,
                InitialSpeedPixelsPerSubupdate = definition.SolverSpeed,
                SpreadSpeedPixelsPerSubupdate = 0f,
                FirstTickProjectileUpdates = 1,
                SustainedProjectileUpdatesPerTick = 1
            };
        }

        /// <summary>
        /// O(1) conservative aim for the reviewed native AI families.  No
        /// straight-line intercept is attempted: boomerangs have a distinct
        /// return phase, spears are owner-anchored, and yoyos follow the mouse
        /// anchor through AI_099.  A current target point is therefore used,
        /// and a target outside the reviewed contact/range envelope is refused.
        /// </summary>
        internal static WeaponAimSolution SolveAim(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported || !IsMeleeProfile(weapon.Profile))
                return result;
            if (!Finite(origin) || !Finite(target) || !Finite(targetVelocity) ||
                !FinitePositive(maxLeadTicks) ||
                !FinitePositive(weapon.ConservativeRangePixels))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            var delta = target - origin;
            var distanceSquared = delta.LengthSquared;
            var range = weapon.ConservativeRangePixels;
            if (float.IsNaN(distanceSquared) ||
                float.IsInfinity(distanceSquared) ||
                distanceSquared > range * range)
            {
                result.Status = WeaponAimStatus.BeyondLifetime;
                return result;
            }
            // Target velocity is deliberately validated above but not fed into
            // a projectile intercept equation. The native routes use the cursor,
            // owner animation, or a return FSM rather than a free flight vector.
            result.Status = WeaponAimStatus.Ready;
            result.LeadTicks = 0f;
            result.SpreadRadiusPixels = 0f;
            result.AimWorld = target;
            return result;
        }

        private static Dictionary<WeaponProfileKey, WeaponProfile>
            BuildProfiles()
        {
            var result = new Dictionary<WeaponProfileKey, WeaponProfile>(
                Definitions.Length);
            for (var i = 0; i < Definitions.Length; i++)
            {
                var d = Definitions[i];
                var ballistic = d.Kind == Model.Boomerang ?
                    WeaponBallisticKind.BoomerangReturn : d.Kind == Model.Spear ?
                    WeaponBallisticKind.SpearOwnerAnchored :
                    WeaponBallisticKind.YoyoCursorAnchored;
                var profile = new WeaponProfile(
                    d.WeaponId, 0, d.Name, d.Slug, d.ProjectileId,
                    d.ProjectileId, d.AutoReuse ? WeaponFireMode.Automatic :
                    WeaponFireMode.NativeAnimationBurst,
                    WeaponSpreadModel.None, 0f, ballistic, 1f, 0, 0f,
                    OutputRouteKind.MeleeProjectile,
                    OutputResourceKind.Melee, false, d.ShootSpeed, 0f,
                    d.Damage, 0, d.UseTime, d.UseAnimation, 0,
                    d.AutoReuse, 0, 0, d.LifetimeSubupdates);
                result.Add(profile.Key, profile);
            }
            return result;
        }

        private static bool TryGetDefinition(int weaponId,
            out Definition definition)
        {
            for (var i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].WeaponId != weaponId) continue;
                definition = Definitions[i];
                return true;
            }
            definition = default(Definition);
            return false;
        }

        private static Definition DefinitionFor(int weaponId, int projectileId,
            string slug, string name, int damage, int useTime,
            int useAnimation, float shootSpeed, bool autoReuse,
            float reachPixels, float solverSpeed, int solverLifetimeTicks,
            int contactWindowTicks, Model kind)
        {
            return new Definition
            {
                WeaponId = weaponId,
                ProjectileId = projectileId,
                Slug = slug,
                Name = name,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ShootSpeed = shootSpeed,
                AutoReuse = autoReuse,
                ReachPixels = reachPixels,
                SolverSpeed = solverSpeed,
                SolverLifetimeTicks = solverLifetimeTicks,
                ContactWindowTicks = contactWindowTicks,
                LifetimeSubupdates = 3600,
                Kind = kind
            };
        }

        private static WeaponProfileEvaluation Rejected(
            WeaponProfileStatus status, WeaponProfile profile)
        {
            return new WeaponProfileEvaluation { Status = status,
                Profile = profile };
        }

        private static bool FinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static bool FiniteZeroOrPositive(float value)
        {
            return value >= 0f && !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static bool Finite(Vec2 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
                !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
        }
    }
}
