# Round 172 handoff

## HEAD
Pushed, tree clean. Tests **749 pass / 9 fail**. Build clean.
Source delta vs the round-168 commit: **only the round-171 `tools/GameProbe.cs` probe-report fix** (9 lines,
zero behavioral change).

## HEADLINE: the weak-wing horizontal escape is refuted — **the vertical component IS the escape**

Acting on round 171's own suggestion, I replaced the weak wing's diagonal locked-charge escape on the
`charge-ascend` beat with a purely horizontal one (rationale: Fairy wings climb at ~60% of Fishron wings
while **horizontal speed is identical** between them). Gated behind `CHAITE_WEAK_HORIZONTAL_ESCAPE`.
**Inertness verified**: unset ⇒ weak 300 reproduces `5839/8/51366` byte-for-byte.

### It collapses, and does so **independently of DPS**

| dps | baseline | horizontal escape | |
|---|---|---|---|
| 300 | 5839/8/51366 | **3504/6/63131** | −2335 ticks |
| 450 | 7487/9/25719 | **3504/6/55724** | −3983 ticks |
| 600 | 5320/6/30138 | **3504/6/48316** | |
| 800 | 3875/7/33502 | **3504/6/38440** | |
| **900** | **KILL 5726/5** | **DIED 3501/7/33555** | kill destroyed |
| **1000** | **KILL 5210/5** | **DIED 3411/5/30140** | kill destroyed |
| **1075** | **KILL 4885/2** | **DIED 3814/5/19292** | kill destroyed |

**Every point 300–800 lands on exactly 3504 ticks with exactly 6 hits.** A DPS-independent constant is
the signature of a **structural collapse**, not a tradeoff.

### Mechanism (from the hit sequences)

| | baseline | horizontal escape |
|---|---|---|
| first hit | **tick 1367** | **tick 846** (−521) |
| projectile types | body / 384 / body (5 of 8 = tornado 384) | **almost all body** (4 of 6 = `proj=None`) |

Deleting the ascend beat's vertical component means the player **never gains altitude**, so the charge
line stays at the player's height and **body contact replaces tornado contact**. The tornadoes largely
stop hitting because the player never occupies the tornado layer at all.

**Conclusion: the vertical component is not an optional refinement a weaker climber can drop — it is the
escape.** This confirms *by wing, for the first time*, what the code already recorded for the strong wing
(perpendicular escape at 13.87 → zero contacts; horizontal-only → contact at tick 3).

## ★ Also corrected: a misreading of the owner's mechanism

The owner's rule — dodge along the normal **unless the distance is already far enough to pull away
horizontally** — is **already implemented**, gated on **distance**:
`_chargeNormalVertical = runAway ? 0 : …` (`FishronWingScript.cs:3343`), where
`runAway = runAwayDistance > 0f && lockDistance >= runAwayDistance` (`:3162-3163`) and the threshold comes
from `CHAITE_LOCK_RUN_AWAY`, which **defaults off**.

So "pull away horizontally" is a **distance-triggered existing rule**, not a per-wing replacement.
Any future attempt should **tune that distance threshold**, not add another per-wing override.

## ★ Loadout completeness verified (owner requirement)

Four distinct loadouts appear in the evidence, exactly matching the objective — **all four share** Shield
of Cthulhu (3097), Amphibian Boots (3990) and the featherfall slot, differing **only in the wing**:

| armor | wing | count |
|---|---|---|
| Obsidian 3266/3267/3268 | **Fishron 2609** | 786 |
| Obsidian 3266/3267/3268 | **Fairy 761** | 433 |
| Shroomite 1547/1549/1550 | **Fairy 761** | 326 |
| Shroomite 1547/1549/1550 | **Fishron 2609** | 197 |

## Acceptance status (obsidian = honest yardstick)

| | obsidian | shroomite |
|---|---|---|
| strong wing | **19/28** | 6/7 (sample) |
| weak wing | **6/28** | **8/28** |
| strict (`-maxticks 6000`, `hits == 0`) | strong 1200 & 2000; weak 2000 | — |

**Not met.** Neither wing kills across 300–2000. Goal stays **active**.

## Next round — the remaining lever is **acceleration**, not direction

For weak wing, all of these are now excluded: delay knobs (7 of them), gating knobs, single-axis escape,
altitude strategies (void — the hover follows the player), and per-wing horizontal replacement.

The one axis not yet differentiated by wing, and directly matching the measured deficit:

* −9.91 is a **peak speed**, but the deficit that matters may be **time-to-peak** (acceleration).
* Earlier leads are already refuted (weak lead ≥30 collapses to 2176).
* **So the remaining lever is the shield dash**, which at 14.50 is *identical* for both wings and is an
  **instantaneous** speed, unlike the wing's ramped cruise.
* Every existing dash experiment (timing / direction / ttc / perpendicular) was tuned **on the strong
  wing and then transferred**. **The untried form is an independent weak-wing scan of dash timing relative
  to the lock** (`ChargeTicksSinceLock` offset), not a borrowed strong-wing optimum.

## Do NOT re-attempt (new this round)

* Weak-wing `charge-ascend` pure-horizontal escape — collapses to 3504 ticks, DPS-decoupled, destroys all
  three weak kills.
* Any per-wing substitution of the diagonal escape for a single axis.
* Previously: tornado escape under **any** trigger; DPS-constant gates; armor tier as a survival lever;
  weak delay knobs; pre-lock lift band; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_APEX_REFILL` default;
  `_refillGuardBudget`; `CHAITE_PRELOCK_LIFT`; standoff ceiling; `CHAITE_CASCADE_*`; bubble-break;
  "reconverge after the fork"; altitude raising.

## Standing constants

* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95,|dy|<92`; tornado `|dx|<122,|dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box **225×63**, **~150×900 px wall**, `vx == 0`.
* Damage census: ≈½ contacts = 386, a few = 384, **≈⅓ = boss body**.
* Player pool **480** (400 + 80) — read from `hurt-observations.player.lifeBefore`, **not**
  `equipment.lifeMax`. Boss despawn/report lag is a fixed **302 ticks**.
* Weak climb peak **−9.91** vs strong **−16.52**; `wingTimeMax` **130 vs 180**; cruise 7–8 both; dash peak **14.50** both.
* Scripts: `tmp/weakhoriz172.ps1`, `tmp/weakshroom171.ps1`, `tmp/fullgrid-te.ps1`, `tmp/lifegate170.ps1`.
