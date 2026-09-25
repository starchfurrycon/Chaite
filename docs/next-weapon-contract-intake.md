# Next weapon-contract intake (Terraria 1.4.5.8)

This is a source-only intake note for the next common-weapon additions.  It
does not claim runtime firing evidence, Boss hit rate, DPS, or win rate.  The
review was made against the locally decompiled 1.4.5.8 `Item`, `Player`, and
`Projectile` sources under `.analysis-tools`; it did not start Terraria or
read/write a world or player save.

## Recommended order

| Order | Item -> projectile | Why it is a good next route | Required model boundary |
|---:|---|---|---|
| 1 | Unholy Trident `683 -> 114` | A common strong magic option with one free projectile, no `ItemCheck_Shoot` item-specific branch, no target search, no random gameplay motion, and no wind-physics AI style. | Dedicated delayed exponential-decay trajectory; credit the first hit only. |
| 2 | Aqua Scepter `157 -> 22` | Early common magic weapon with one free projectile and deterministic delayed gravity on an ordinary non-lava path. | Exact delayed-gravity solver plus an explicit no-lava-path gate. |
| 3 | Betsy's Wrath `3852 -> 712` | Strong late-game magic option and a free, straight projectile rather than a held projectile. | Native animation-burst phase plus stable first-shot aim state; wind gate or wind model. |
| research | Flower of Frost `1264 -> 253` | One deterministic projectile and delayed gravity. | Must read/gate wind because `aiStyle=8` participates in wind physics. |
| research | Staff of Earth `1296 -> 261` | One deterministic projectile and delayed gravity. | Must read/gate wind because `aiStyle=14` participates in wind physics. |
| research | Wand of Sparking `3069 -> 954`; Wand of Frosting `5147 -> 979` | Simple early progression candidates. | Prefix/variant-aware delayed-gravity model plus wind gate (`aiStyle=2`). |

## Exact source facts

### Unholy Trident

- `Item.SetDefaults`, item `683`: damage `150`, mana `19`,
  `useTime/useAnimation=27/27`, `shoot=114`, `shootSpeed=13`, and
  `autoReuse=true`.  Its weaker world variant changes damage and mana, so a
  production contract must use live damage/mana rather than hard-code those
  two values.
- Projectile `114` is 16x16, `aiStyle=27`, magic, `extraUpdates=2`,
  `timeLeft=180`, and has armor penetration 25.
- In the `aiStyle=27` branch, native code increments `ai[0]`; updates 1--19
  retain launch velocity, while update 20 onward multiplies both velocity and
  its stored initial-speed value by `0.98` until expiration.  There is no
  RNG-driven flight change in that branch.
- `Projectile.ShouldUseWindPhysics()` does not include `aiStyle=27`, so the
  generic surface wind adjustment does not apply.
- Native post-hit handling reduces later `114` hit damage.  Admission/DPS
  must credit only the first impact unless live hit evidence establishes more.

### Aqua Scepter

- `Item.SetDefaults`, item `157`: damage `27`, mana `7`,
  `useTime/useAnimation=8/16`, `shoot=22`, `shootSpeed=12.5`, and
  `autoReuse=true`.
- Projectile `22` is 18x18, `aiStyle=12`, magic, `penetrate=5`,
  `extraUpdates=2`, and ignores water.
- Its native AI leaves the first four projectile updates without gravity;
  after that it adds `0.15` to Y per update.  This is a deterministic
  piecewise path and can use the existing discrete-vertical-acceleration
  solver shape once its parameters are made item-specific.
- The same native branch checks for lava after downward motion.  On lava it
  changes state, alters `extraUpdates`, and uses random rebound behaviour.
  A production route must prove the predicted prefix does not enter lava, or
  stop certification before the first possible lava state.  It must never
  treat the lava branch as ordinary ballistic motion.
- `aiStyle=12` is not in the generic wind list.

### Betsy's Wrath

- `Item.SetDefaults`, item `3852`: damage `36`, mana `20`,
  `useTime/useAnimation=3/25`, `shoot=712`, `shootSpeed=11`, and
  `autoReuse=true`.
- Projectile `712` is a 10x10 magic projectile with `aiStyle=1`,
  `extraUpdates=1`, and `timeLeft=600`.  Its flight path itself has no
  reviewed gameplay velocity mutation, target acquisition, or sub-projectile
  generation.
- `Player.ItemCheck_Shoot` treats later shots in an animation specially:
  when `sItem.type == 3852 && !ItemAnimationJustStarted`, it reuses the
  current `itemRotation` direction rather than taking a fresh mouse vector.
  Therefore a controller must either capture/reuse the current animation's
  first-shot aim state or wait for a new animation boundary; it must not
  pretend each burst shot independently follows the current mouse position.
- `aiStyle=1` is included in `Projectile.ShouldUseWindPhysics()`.  Without a
  verified wind-state read and a matching model, production must fail closed
  whenever the route can receive wind physics.

## Explicit exclusions

Do not widen a generic magic or projectile profile merely because a candidate
looks similar.  In particular, held/cursor-anchored routes, target-searching
shots, random multi-projectile emission, and post-impact child-projectile
routes require their own native state and emission contracts.  Examples
already excluded by source review include Laser Machinegun, Charged Blaster
Cannon, Magical Harp, Bubble Gun, Nightglow, Nettle Burst, Leaf Blower, and
unbounded Razorblade Typhoon behaviour.

## Production checklist

Before a row is enabled for automatic output, add all of the following:

1. An exact item/ammo/projectile/timing/resource validation path, including
   live variant values where vanilla changes them.
2. A dedicated aim solver for any non-straight trajectory; do not substitute
   a nominal constant-speed lead.
3. A native environmental gate for each unmodelled branch (notably lava and
   wind), which fails closed instead of guessing.
4. Offline source-shape, profile rejection, aim, and mid-animation/mid-fight
   regression tests.  The tests should prove the contract, not claim a Boss
   win rate.
