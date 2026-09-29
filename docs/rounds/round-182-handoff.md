# Round 182 handoff

## HEAD / state
**No source change this round** — the round-182 work was measurement that closed lever families and
narrowed the target. `src/` is identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.

Docs added: `docs/fishron-native-truth.md` §183.1–§183.7.

---

## 1. The weak wing's flight cycle is measured as physically optimal

`tmp/budget182.py`, weak dps 800, 6385 frames:

| quantity | value |
|---|---|
| full refills (`wingTime` 0 → 130) | **18** |
| refill y | **p25 = p50 = p75 = 6032** (essentially constant) |
| ticks between refills | min 232 / **p50 328** / max 445 |
| frames with `wingTime > 0` | **51.4%** |
| frames with `wingTime == 0` | **48.6%** |
| y range while budgeted | 4715 .. 7958 = **3371** |

Cycle: 130 frames of climb (−9.91 px/tick from 7958 to ≈6670) then **198 frames with no budget at all**,
refilling at y≈6032.

**Why the cycle shape cannot be compressed:** the apex is the product of climb rate × `wingTimeMax`
(≈6670 px), and a shallower descent never brings `velocity.Y` to zero, so the native apex refill
(`Player.cs:26992`) never fires and the budget stays zero forever. **Descending to an apex is the only way
to refill.** So the ~50% empty-budget fraction is a property of the fairy wing, not of the route.

## 2. Horizontal capacity is identical between the wings — that lever is closed too

`tmp/horiz182.py`:

| | weak 800 | strong 800 | strong 1150 |
|---|---|---|---|
| mean \|vx\| | **5.90** | 6.15 | 6.30 |
| direction reversals | 18 (**1 per 355 t**) | 15 (per 426) | 9 (per 512) |

The weak wing's horizontal execution is **not worse** than the strong wing's. The only structural
difference remains the vertical range: **3371 (weak) vs 5135 (strong)**.

## 3. ★ The real mechanism: equal damage per hit, 2.8× the contact rate

`tmp/dmg182.py`:

| run | ticks | hits | dmg/hit | life lost | outcome |
|---|---|---|---|---|---|
| weak obs 300 | 9472 | **10** | 137.9 | 885 | DIED |
| **strong obs 300** | **15938** | **6** | 184.7 | **884** | **KILL** |
| weak obs 800 | 6384 | 2 | 147.5 | 228 | KILL |
| strong obs 1150 | 4611 | 1 | 173.0 | 137 | KILL |

**Damage per hit is nearly the same (137.9 vs 184.7) and total life lost is the same (885 vs 884).**
The difference is the **contact rate**: strong **0.38** per 1000 ticks vs weak **1.06** — **2.8×**.

Source census after the ceiling (`tmp/src182.py`):

| source | weak (16 runs / 94 hits) | strong (11 runs / 46 hits) |
|---|---|---|
| **386 Sharknado** | **44.7%** | 54.3% |
| **370 Boss body** | **46.8%** | 34.8% |
| 384 | 8.5% | 10.9% |

## 4. ★★ Every weak failure is 5–7 contacts; every weak kill is 2–4

`tmp/perrun182.py` — all 16 failing points:

| dps | 300 | 400 | 450 | 475 | 500 | 525 | 550 | 600 | 675 | 700 | 725 | 750 | 825 | 850 | 900 | 1100 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hits | **10** | 6 | 6 | 5 | 6 | 6 | 6 | 5 | **7** | 6 | 5 | 5 | 6 | 5 | 5 | 5 |

**No failure is below 5 hits. The survival ceiling is 4 hits.** A failing point is therefore only
**1–3 contacts away from a kill** — a far more precise target than "reduce the contact rate".

Contact rate alone does **not** decide it: failing points span 0.62–1.34 per 1000 ticks and overlap the
rates of kills (dps 675 at 1.34 fails while dps 800 at 0.31 survives). Fight length matters too.

## 5. The DPS sweep is non-monotone, but the scenario really is identical

Verified from `result.json` (`tmp/scen182.py`): `seed = 20260910` and
`battleRandom.independentTwinFingerprint` are **identical across every dps point**; only
`CHAITE_SIM_DPS` differs. Dash-commanded frames are a constant **0.9%** and the player is **never grounded**
in any run (`tmp/fp182.py`). So the divergences are genuine trajectory divergence caused by the differing
boss death tick, not a hidden scenario difference.

Useful: `hits` is defined in `result.json` as *"native update frames with decreased player life, including
environmental damage; not a damage-event hook"* — i.e. it is exactly the owner's `hits == 0` criterion.

## 6. Phase-3 teleport is **excluded** as an explanation for the weak wing

The dash formula is applied only for `state == 1 || 6 || 11` (`FishronWingScript.cs:1633`), where 11 is the
phase-3 charge. Phase 3 requires `life <= lifeMax*0.15 = 11700`. **Every weak failure is at dps ≤ 1100,
where the fight ends well before the boss reaches 15%** (at dps 300 the player dies with the boss still on
33252 life). So teleport modelling cannot improve any weak failure.

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | ~13 kills; **failures are all 5–7 contacts** | worse (§182.6) |

**Strict `hits == 0`: strong 1175 / 1200 / 1300 / 2000; weak none. Not met — goal stays active.**

## Next round — the only remaining avenue with a mechanism

Three families are now closed: **vertical timing** (§174 +1/−5, §175 0/−1, §177 −3, §181 0 and unreachable),
**horizontal strategy** (§183.2, identical to strong), **armour tier** (§182.6, shroomite worse).
Cycle efficiency is closed by §183.1.

What is left is **removing 1–3 specific contacts from specific failing trajectories**, not reshaping the
cycle. Concretely:

1. **Take the 5-hit failures first** (dps 475/600/725/750/850/900/1100 — that is 7 of the 16, and they are
   1 contact from the ceiling). A contact-removal rule that works on a 5-hit run converts it to a kill.
2. **Separate the two contact classes.** 386 and body are ~45% each. Body contacts (`npc 370`) need a
   horizontal/vertical separation fix at the charge; 386 contacts need altitude separation from the tornado
   band at the moment of spawn. These are different geometric problems and must be attacked separately —
   note §180 already showed the weak wing's two deterministic hits are **body** contacts at ticks 422/2186.
3. **Screening rule that must be honoured** (§170.1): a change is zero-sum unless it **permits or forbids an
   existing command**; every genuine win so far added a **missing pre-condition**. A prohibition must
   **substitute**, not delete. Judge by **net kills**, never by hit count (§177).
4. Finish the four-grid sweep (`tmp/grid180.ps1`, ~28/132 done) for the reported both-tier numbers.

## Do NOT re-attempt

* **`CHAITE_REFILL_APEX_Y` / "descend earlier to raise the apex"** — weak climb ceiling is y 4705; 0 of 2894
  climbing frames above 3600 (§181).
* **Any vertical-timing knob**; **any horizontal-strategy change** (identical to strong); **armour tier**
  (shroomite strictly worse); **cycle restructuring** (apex = rate × budget).
* **Phase-3 teleport as an explanation for weak failures** (they never reach phase 3).
* Judge by hit count instead of net kills; reading `hostileProjectiles` with a nested `position` key (flat
  `x/y/vx/vy`); reading `plan` from the first row only (95.9% populated).
* Apex refill on weak (+1/−5); dash veto (0/−1); `CHAITE_WEAK_ALTITUDE_FLOOR` (−3); "weak under-dashes";
  "weak lacks wingTime"; 1–2 tick timing knobs; tornado escape under any trigger; `CHAITE_TORNADO_*`;
  `CHAITE_WIDTH_HOLD`; `CHAITE_PRELOCK_LIFT`; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_LOCK_RUN_AWAY`;
  `CHAITE_COUNTER_DASH`; `CHAITE_DASH_SUPPRESS`; `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`;
  `CHAITE_CHARGE_ESCAPE_SIM`; `_refillGuardBudget > 0`; DPS-constant gates.

## Standing constants

* **Shipped rule**: `WeakAltitudeCeilingDefault = 4400f`; plateau **4000–4600 byte-identical**, cliff 4800;
  `CHAITE_WEAK_ALTITUDE_CEILING=0` disables.
* **Weak cycle**: climb −9.91 (130 t), descend +10.01 (198 t), refill every **328 t** at y≈6032, apex ≈6670,
  climbing y **never above 4705**.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`. Tornadoes 384/386 spawn at the
  **boss centre**; 386 `timeLeft 840`.
* **Boss**: 28 ticks/charge, hover 30/40, period 58. p2 at `life<=39000`, p3 at `life<=11700`.
* **Video**: player y **218..275 tiles**; `worldSurface=500`, `rockLayer=750`; depth = `position.Y/16`.
* Acceptance env: `CHAITE_ARMOR_TIER`, `CHAITE_SIM_DPS`, `CHAITE_SIM_DPS_FULL_TILES=400`,
  `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`,
  `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-fairy-wing|fishron-strong-wing`.
* Round-182 scripts: `tmp/budget182.py`, `tmp/horiz182.py`, `tmp/dmg182.py`, `tmp/src182.py`,
  `tmp/perrun182.py`, `tmp/fp182.py`, `tmp/scen182.py`.
