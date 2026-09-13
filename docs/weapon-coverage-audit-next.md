# Weapon coverage audit (Terraria 1.4.5.8)

This began as a read-only coverage audit. It compares `WeaponProfileCatalog.cs` with
the locally decompiled 1.4.5.8 `Item`, `Player`, `Projectile`, and `ItemID`
sources under `.analysis-tools`. The Diamond Staff, Pew-matic Horn, bounded
Snowball Cannon, Flower of Fire, and Razorpine routes were subsequently added
from the evidence below. The pinned executable is the existing SHA-256
`960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`.

## Current coverage

The primary catalog has 885 exact pairs: 22 gun items × 15 bullet items (330), one
Snowball Cannon × Snowball pair, two star cannons, 35 bow/repeater items × the
reviewed arrow matrix (512; Hellwing is restricted to two wooden-arrow
  identities), 4 dart weapons × 5 darts (20), and 20 mana weapons. A separate
  experimental non-linear melee catalog records 15 exact no-ammo profiles (900
  source-reviewed profile records when both catalogs are counted), but all 15
  are deliberately rejected by production Boss admission. Rocket Launcher + Rocket I is a
separate production route outside both catalog counts; no other rocket pair is
admitted by that route. Ordinary summon output is another separate contract
and currently has 28 staff items plus 18 whips.

These counts describe source-reviewed identities and offline catalog/solver
contracts. Only the 885 primary profiles are production candidates; the 15
melee records are not production output routes. None of these counts is
evidence that every profile has been fired in a real client, and they do not
establish per-Boss DPS, hit rate, or win rate.

The `AmmoID.Bullet` cases in `Item.SetDefaults` are:

```
95, 96, 98, 164, 219, 434, 533, 534, 679, 800, 964, 1254, 1255,
1265, 1319, 1553, 1870, 1929, 2269, 2270, 2797, 3475, 3788, 5117
```

All entries other than `1319`, `2797`, and `3475` are represented by the
ordinary bullet matrix. Pew-matic Horn (`5117`) is represented by an explicit
projectile-override profile below; the remaining three omitted entries are not
safe to add as a straight bullet profile.

Pew-matic Horn's `ItemCheck_Shoot` branch replaces the selected bullet with
projectile `968` and adds independent `[-1.125,+1.125]` velocity components.
Projectile 968's AI2 branch does not change velocity (the remaining work is
wind physics and visual frame selection), so the catalog locks its output to
968, zero extra updates, and the native 3600-subupdate lifetime. Ammo still
supplies the native damage/speed contribution. The profile credits only the
primary path and marks secondary projectile effects uncredited.

The arrow cases add these omitted weapons to the covered bow family:

```
3029, 3540, 3854, 3859, 4953
```

Their `useAmmo=Arrow` field does not imply an ordinary one-arrow launch. The
current ordinary bow matrix should not be widened to include them.

## Conditionally safe magic addition

`Diamond Staff` (`item 744`, `ammo 0`) is the only member of the seven vanilla
gem staffs which fits the current straight-magic profile after an explicit
armor-state gate, and it is now integrated.

| Field | Native value | Evidence/limit |
|---|---:|---|
| Item damage | 23 | `Item.SetDefaults` case 744 |
| `useTime/useAnimation` | 26 / 26 | same case |
| `shootSpeed` | 9.5 | same case |
| mana | 9 | same case |
| raw/final projectile | 126 / 126 | `Item.shoot`; no ammo |
| projectile dimensions | 10×10 | `Projectile.SetDefaults` type 126 |
| `aiStyle`, `extraUpdates`, lifetime | 29, 0, 300 | type 126; `ApplyGemStaffStats` |
| motion | straight, unchanged velocity | `AI_029_GemStaffs`; only visuals and feature flags |
| collision/penetration | hitbox inflated by 30 px; `penetrate=2` | `BiggerHitbox` in `CanHit`; `FinalizeProjectile_GemStaves` |

The item is not unconditionally safe because `Player.PackGemStaffFeatures`
reads the effective body armor (`GetEffectiveArmor(1)`). Gem robes `1282..1287` (Amethyst through
Diamond Robe) and `4256` (Amber Robe) can add homing, bounce, acceleration,
swirl, spread, extra damage, or other feature bits to gem-staff projectiles.
The production route must either read and validate this feature state, or
reject this profile while any of those robe IDs is equipped. For the default
no-gem-robe state, use `StraightMagic`, `ProjectileSafetyRadiusPixels=30`,
and do not credit secondary visual effects.

The other six staff items remain blocked even without a robe: Amethyst `739`
uses homing projectile 121, Topaz `740` uses AOE projectile 122, Sapphire `741`
uses twin-swirl projectile 123, Emerald `742` uses fast-then-slow projectile
124, Ruby `743` uses armor-penetrating spread projectile 125, and Amber `3377`
uses bouncing projectile 597. They are not color variants of the Diamond
Staff's straight route and cannot inherit its profile.

Admission tests:

1. Pin `TryGet(744, 0)` to projectile 126, damage 23, mana 9, 26/26 timing,
   speed 9.5, `extraUpdates=0`, and lifetime 300.
2. Exercise horizontal and diagonal trajectories and verify velocity is
   unchanged through the profile lifetime; verify the 30 px safety padding
   and two-hit penetration boundary.
3. For each robe ID `1282..1287,4256`, verify the route rejects the profile (or
   uses a separately captured feature-aware route). A test that only checks
   the item fields is insufficient.
4. Keep a static source check that type 126 remains `aiStyle=29` and that the
   relevant Player branch still packs gem-staff features from effective armor.

## Recently integrated bounded magic routes

Flower of Fire (`112`) and Razorpine (`1930`) now use the explicit
`ConservativeStraightPrefix` contract. Their native post-prefix acceleration
is deliberately excluded; admission stops at the independently reviewed
straight prefix and fails closed if projectile identity, timing, or resource
state drifts. See `SimpleMagicPrefixContracts.cs` and its regression tests for
the exact limits.

## Experimental non-linear melee coverage

The separate `MeleeProjectileCatalog` records these 15 no-ammo identities from
the same 1.4.5.8 source review:

| Item | Projectile | Native route and timing | Conservative contract |
|---:|---:|---|---|
| 55 Enchanted Boomerang | 6 | `AI_003`, 10 px/tick, 20/20, outbound 30 updates | 300 px outbound envelope |
| 119 Flamarang | 19 | `AI_003`, 14 px/tick, 20/20, outbound 30 updates | 420 px; On Fire debuff uncredited |
| 191 Thorn Chakram | 33 | `AI_003`, 14 px/tick, 15/15, outbound 30 updates | 420 px; Poison debuff uncredited |
| 284 Wooden Boomerang | 52 | `AI_003`, 6.5 px/tick, 20/20, outbound 30 updates | 195 px outbound envelope |
| 277 Trident | 47 | `AI_019`, 4 px/tick, 31/31 | owner-anchored 40 px contact band |
| 280 Spear | 49 | `AI_019`, 3.7 px/tick, 31/31 | owner-anchored 40 px contact band |
| 670 Ice Boomerang | 113 | `AI_003`, 11.5 px/tick, 20/20, outbound 30 updates | 345 px; Frostburn debuff uncredited |
| 561 Light Disc | 106 | `AI_003`, 16 px/tick, 14/14, auto-reuse, outbound 45 updates | 720 px outbound envelope |
| 1324 Bananarang | 272 | `AI_003`, 16 px/tick, 11/11, auto-reuse, outbound 30 updates | 480 px outbound envelope |
| 1918 Fruitcake Chakram | 333 | `AI_003`, 11 px/tick, 15/15, outbound 30 updates | 330 px outbound envelope |
| 756 Mushroom Spear | 130 | `AI_019`, 5.5 px/tick, 40/40 | 40 px contact band; spore projectiles uncredited |
| 1200 Titanium Trident | 218 | `AI_019`, 5 px/tick, 23/23 | owner-anchored 40 px contact band |
| 4061 Thunder Spear | 730 | `AI_019`, 3.5 px/tick, 28/28 | 40 px contact band; lightning projectile uncredited |
| 5687 Slime Spear | 1103 | `AI_019`, 5.5 px/tick, 24/24 | 40 px contact band; debuff uncredited |
| 3278 Wood Yoyo | 541 | `AI_099`, 9 px/tick cap, 25/25, 180-tick reviewed base | 130 px cursor-anchored band |

All listed projectiles have `extraUpdates=0` and the constructor lifetime of
3600 subupdates in the pinned build. `AI_003` return thresholds are 30 native
updates except Light Disc type 106, which is 45. `AI_019` keeps the projectile
anchored to the player's `MountedCenter`; it is never passed to the straight
intercept solver. Mushroom Spear type 130 creates spores, Thunder Spear type
730 creates one lightning projectile, and the Flamarang/Thorn/Ice/Slime
projectiles apply debuffs. These effects are exposed as metadata but excluded
from experimental `ApproximateDirectDps`.

This is not production melee support. `OutputRouteContract` can construct each
reviewed experimental identity so its catalog and solver contract can be
tested, but `CombatPlanner.TryCreateOutputAdmission` rejects every
`MeleeProjectile` before either a summoned or already-active Boss can be
controlled: no Boss-pattern hit-range and native-cadence certificate exists.
The hotbar ranker also gives these profiles a zero score, so they cannot outrank
a positive-score production weapon. If all slots score zero it may retain the
current slot, after which Boss admission still fails closed. A separately valid
summon/whip route may be selected instead; that does not admit the melee item.

## Magic candidates that remain blocked

These are useful coverage targets, but are not safe additions to the current
profile schema without a route/model extension:

| Item | Native output | Why the current model is insufficient |
|---|---|---|
| Nebula Blaze `3542` | projectile 634, `extraUpdates=2`; item damage 130, mana 12, 12/12, speed 6 | `ItemCheck_Shoot` rotates by a random ±0.2749 rad envelope and scales speed by 0.95..1.25. A nominal straight profile would understate spread/range. |
| Demon Scythe `272` | projectile 45, `aiStyle=18` | native acceleration/decay changes velocity after age 30; not a straight primary. |
| Magic Missile `113`, Flamelash `218`, Rainbow Rod `495` | projectiles 16, 34, 79 | controllable, homing, or bouncing paths; require their own route and target state. |
| Blizzard Staff `1931`, Lunar Flare `3570`, Razorblade Typhoon `2622`, Nebula Arcanum `3476` | multi-spawn/targeted projectile families | spawn location, count, or target selection is part of the output contract. |

## Gun contracts requiring dedicated routes

Rocket Launcher (`759`) + Rocket I (`771`) is now the first production
special-ranged route. It pins final projectile 134, a 180-subupdate lifetime,
and the reviewed accelerating trajectory. This one route is intentionally not
part of the 885 primary or 900 source-reviewed-record counts; Rocket II-IV, liquid
rockets, cluster rockets, mini nukes, grenade launchers, and mines remain
blocked unless separately modeled.

| ID | Native fields | Required route/restriction |
|---:|---|---|
| Snowball Cannon `1319` | final `useAmmo=Snowball`, projectile 166; normal damage 10, 19/19, speed 11, auto; stronger variant damage 22, 6/6; type 166 is 14×14 `aiStyle=2` | Implemented as a separate Snowball ammo family. The route admits only the first 19 straight projectile updates before AI2 begins applying `velocity.Y += 0.3` and `velocity.X *= 0.98`; ItemCheck's independent ±0.8 velocity components are included in the conservative spread envelope. It never inherits the ordinary bullet matrix. |
| Xenopopper `2797` | `useAmmo=Bullet`, raw shoot 444, damage 45, 21/21, speed 12, auto | One use consumes one picked bullet and emits 4 bubbles (75%) or 5 (25%); bubbles are projectile 444 with `extraUpdates=1`, `timeLeft=60`, target/launch state in `ai/localAI`. Requires a dedicated live-bubble route; it is not currently admitted. |
| Vortex Beater `3475` | held projectile 615, damage 50, 20/20, speed 20, channel | Live held-projectile phase; firing event every 5 game updates; each seventh event creates a projectile 616 rocket in addition to the bullet. Read 615 state before firing. |

## Omitted bow contracts

| ID | Native fields | Required route/restriction |
|---:|---|---|
| Daedalus Stormbow `3029` | projectile starts from `shoot=1`; damage 38, 19/19, speed 12.5 | ItemCheck emits 3 arrows normally or 4 randomly, with random high spawn positions, vertical offsets, and velocity perturbations. Needs a spawn/count-aware route; never inherit the one-primary bow route. |
| Phantasm `3540` | held projectile 630, damage 50, 12/12, speed 20, channel, arrow ammo | The held projectile owns the firing phase and later creates arrows; capture 630 state and arrow event timing. |
| DD2 Phoenix Bow `3854` | held projectile 705, damage 32, 18/18, speed 20, channel, arrow ammo | Dedicated held-projectile path; do not treat 705 as the selected arrow projectile. |
| DD2 Betsy Bow `3859` | emits five projectile-710 arrows; damage 38, 30/30, speed 11, auto | Each arrow is `extraUpdates=1`, `timeLeft=300`, and receives a deterministic spread/rotation sequence. The five-arrow count and spread must be modeled together. |
| Fairy Queen Ranged Item/Eventide `4953` | `useAmmo=Arrow`, `shoot=1`, damage 50, 30 animation / 2 use time, speed 10 | Wooden arrow is converted to projectile 932; ammo consumption is suppressed for the first 8 animation frames. Needs the Eventide sequence and exact ammo-state handling. |

## Summon coverage result

The 28 ordinary minion staff IDs in `VanillaSummonWhipOutputCatalog` exactly
match the reviewed ordinary `summon=true` staff set:

```
1157, 1309, 1802, 2364, 2365, 2535, 2551, 2584, 2621, 2749, 3249,
3474, 3531, 4269, 4273, 4281, 4607, 4758, 5005, 5069, 5114, 5456,
5663, 5664, 6148, 6149, 6161, 6164
```

The intentionally excluded summon-like IDs are sentries, pets, or deprecated
entries: `1169, 1180, 1242, 1572, 2366, 3569, 3571, 3834, 5119, 5463,
6143`. They need separate sentry lifetime/placement or pet contracts rather
than a minion-staff output profile. No summon gap is safe to add to the
ordinary minion route from this audit.

## Regression checklist for future additions

- Pin item ID, ammo ID, raw selected projectile, final projectile, resource,
  timing, and extra updates from `SetDefaults`/`PickAmmo`.
- Check every `Player.ItemCheck_Shoot` branch for the item ID before admitting
  a generic route; random count, spawn position, held projectiles, and target
  selection are route boundaries.
- Read projectile `SetDefaults` and its AI branch for dimensions, lifetime,
  gravity/drag, penetration, local immunity, and world-writing side effects.
- Add an offline profile test, a projectile identity mismatch test, a resource
  and timing mismatch test, and a source/hash-locked audit test before
  changing production coverage.
