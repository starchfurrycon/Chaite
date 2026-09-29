# Round 183 handoff

## HEAD / state
**No source change this round.** `src/` is identical to the round-180 commit. Build clean.
Tests **749 pass / 9 fail**. Docs: `docs/fishron-native-truth.md` §184.1–§184.6.

---

## 1. All seven five-hit failures share the SAME two body contacts

`tmp/body183.py` — dps 475/600/725/750/850/900/1100:

| tick | source | dx | dy | phase | dmg |
|---|---|---|---|---|---|
| **422** | **npc 370** | **148.4** | **69.1** | `refill` | 161 |
| **2186** | **npc 370** | **11.0** | **67.1** | `charge-horizontal` | 134 |

The pre-422 trajectory is **dps-independent** (identical coordinates), so a single fix helps all of them.
Note t=2186 is a **near-head-on** contact (`dx = 11.0`) — a different geometry from t=422.

## 2. ★ The dash-hijack hypothesis was REFUTED — the ceiling is worth net +5

`tmp/diverge183.py` showed the ceiling rule diverting a failing run (`kG-w-850`) into
`weak-altitude-ceiling` at t=3010 where the successful `kG-w-800` dashes. Testing that directly:

`tmp/tab183.py`, obsidian, ceiling 4400 **ON** vs `=0` **OFF**, 14 common points:

| | ON | OFF |
|---|---|---|
| kills | **7** | 2 |
| **net** | **+5** | |

Flips: 900 **DIED→KILL** (the only gain); 575/625/650/800/950/1050 all **KILL→DIED**.
With the ceiling off nearly every point dies **faster** (≈6300–8700 t → ≈3800–5400 t) with **more** hits
(5–7 → 6–8), because the player sinks into the Boss band. **This independently reproduces §179's +5/−3 on a
second dataset.** The dash hijack is a coincidence inside winning trajectories, not a cause.

**Single counterexample: dps 900.** Since §179 measured 4000/4200/4400/4600 **byte-identical** (cliff at
4800), plain band tuning cannot exploit this; the ceiling would need to adapt to fight state.

## 3. ★★★ Root cause of the deterministic t=422 contact (new, and it is repairable)

`tmp/hit422.py`, kG-w-800. **Correction first:** the `dx`/`dy` in the damage record are positions when the
record was *written*, not at contact. Applying the known **10-tick deferral** puts the real contact at
**ticks 411–412**:

| tick | player vx | dx | dy |
|---|---|---|---|
| 406 | +2.67 | −20.7 | −124.7 | ← **safe** |
| **407** | **+14.50** | +3.1 | −111.2 | ← **shield dash fires** |
| 408 | +14.18 | +26.5 | −97.3 |
| 409 | +13.87 | +49.7 | −82.9 |
| 410 | +13.57 | +72.5 | −68.2 |
| **411** | +13.26 | **+95.1** | **−53.1** | ← **contact window** |
| **412** | +12.97 | **+117.3** | **−37.6** | ← **contact window** |
| **413** | **−9.00** | +117.6 | −26.9 | ← **★ post-dash reversal** |

The Boss charge is a constant `(−9.28, −14.24)` from t=402 to t=431.

**At t=406 the player was safe.** The dash at t=407 pushed vx to +14.50 and carried them onto the Boss's
crossing line. But **removing the dash does not fix it** (the player would still be hit), so the dash is not
the cause.

**The cause is the post-dash horizontal reversal at t=413**: vx flips to **−9.00** even though
`plan.horizontal` is still `1` (right). The player was opening the gap at +13 and could have cleared
(`dx` 120+, `dy` past zero by t=415), but the reversal stops it.

## 4. The next lever (concrete, with the screening rule respected)

> In the `charge-horizontal` phase, when a shield dash has pushed the player away from the Boss's crossing
> line, **keep the horizontal direction consistent with the dash direction through the crossing window**
> — i.e. do not reverse immediately after the dash ends. This is literally the owner's stated principle:
> **只要横向移动速度一直保持**.

**This is NOT "delete the dash"** — that is zero-sum and forbidden by §170.1. It **adds a missing
pre-condition** (post-dash horizontal continuity), which is the only shape that has ever produced a genuine
win in this project. **Judge by net kills, never by hit count (§177).**

Also unresolved and worth a cheap check: why does frictional deceleration produce −9.00 when
`plan.horizontal == 1`? Find the code that sets the post-dash horizontal velocity — it should be the direct
target of the change.

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | 7 kills / 14 re-measured points; failures all 5–7 contacts | worse (§182.6) |

**Strict `hits == 0`: strong 1175/1200/1300/2000; weak none. Not met — goal stays active.**

## Closed lever families (do not re-attempt)

* **Vertical timing / apex height** — §174 (+1/−5), §175 (0/−1), §177 (−3), §181 (0, and the rule is
  *unreachable*: 0 of 2894 climbing frames above y 3600; weak climb ceiling is y 4705).
* **Horizontal strategy as a global change** — §183.2, the weak wing's horizontal execution is already
  identical to the strong wing's (mean |vx| 5.90 vs 6.15).
* **Armour tier** — §182.6, shroomite is strictly worse.
* **Cycle restructuring** — §183.1, apex = climb rate × `wingTimeMax`, and a shallower descent never
  triggers the native apex refill, so the ~50% empty-budget fraction is unavoidable.
* **Disabling the ceiling** — §184.2/184.5, net −5.
* **Phase-3 teleport as a weak-wing explanation** — every weak failure is at dps ≤ 1100, where the Boss is
  nowhere near 15% life.
* Judge by hit count instead of net kills; reading `hostileProjectiles` with a nested `position` key (flat
  `x/y/vx/vy`); reading `plan` from the first row only (95.9% populated).
* Apex refill on weak; dash veto; `CHAITE_WEAK_ALTITUDE_FLOOR`; "weak under-dashes"; "weak lacks wingTime";
  1–2 tick timing knobs; tornado escape under any trigger; `CHAITE_TORNADO_*`; `CHAITE_WIDTH_HOLD`;
  `CHAITE_PRELOCK_LIFT`; `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_LOCK_RUN_AWAY`; `CHAITE_COUNTER_DASH`;
  `CHAITE_DASH_SUPPRESS`; `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`; `CHAITE_CHARGE_ESCAPE_SIM`;
  `_refillGuardBudget > 0`; DPS-constant gates.

## Standing constants

* **Shipped rule**: `WeakAltitudeCeilingDefault = 4400f`; 4000/4200/4400/4600 byte-identical, cliff 4800;
  `CHAITE_WEAK_ALTITUDE_CEILING=0` disables (**net −5**, verified §184.2).
* **Weak cycle**: climb −9.91 (130 t), descend +10.01 (198 t), refill every **328 t** at y≈6032, apex ≈6670,
  climbing y never above **4705**.
* **Shield dash**: peak vx **14.50**, then reverses to −9.00 (this is the t=413 problem). Bearings 13.87–14.
* **Boss charge**: constant `(−9.28, −14.24)` in the t=402–431 window; 28 ticks/charge; hover 30/40;
  period 58; p2 at `life≤39000`, p3 at `life≤11700`.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* **Damage-record deferral**: **exactly 10 ticks** — always convert record coords to contact coords.
* Acceptance env: `CHAITE_ARMOR_TIER`, `CHAITE_SIM_DPS`, `CHAITE_SIM_DPS_FULL_TILES=400`,
  `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`,
  `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-fairy-wing|fishron-strong-wing`.
* Round-183 scripts: `tmp/body183.py`, `tmp/diverge183.py`, `tmp/tab183.py`, `tmp/hit422.py`.
  (Note artifact tag format: `game-probe-kG-w-<dps>` hyphenated, `game-probe-kL-w<dps>` not.)
