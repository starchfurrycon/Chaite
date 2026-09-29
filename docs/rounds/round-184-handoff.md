# Round 184 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.
Docs: `docs/fishron-native-truth.md` §185.1–§185.11. **`tmp/Player.cs`** is a decompiled `Terraria.Player`
(**UTF-16**) kept for reference — reading it requires decoding UTF-16, and the `read` tool rejects it as binary.

---

## The t=422 deterministic contact: full causal chain (every link verified)

This contact is identical on **all seven** five-hit weak failures, so one fix helps all of them.

### 1. The damage record has no deferral (corrects §184.4)
Raw record: `tickBefore 422`, `type 370`, `position (672.4, 6419.0)`, `velocity (−9.28, −14.24)`,
`request.damage 161`. The boss position in that record equals the **t=422** row's boss position exactly, so
the record is contemporaneous. **But the contact is still not at t=422**: there `dx = 157.7` (threshold 95).

### 2. The native shield trace gives the real sequence (`tmp/shield184.py`)

| tick | requested | started | eocDash | dashDelay | contact | vx | vy |
|---|---|---|---|---|---|---|---|
| **407** | True | True | **15** | **−1** | False | **+14.50** | −1.12 |
| **413** | False | False | **9** | **29** | **True** | **−9.00** | −4.00 |

### 3. The engine code (`tmp/Player.cs`, UTF-16)

```
L21628  dashType 2 (Shield of Cthulhu)
L21633    velocity.X = 14.5f * dir2;
L21640    dashDelay = -1;
L21641    eocDash   = 15;

L21279  int num3 = (velocity.X != 0f) ? Math.Sign(velocity.X) : direction;
L21284    eocDash = 10;          // shield hit the NPC
L21285    dashDelay = 30;
L21286    if (oldStyleParkour) { L21288 velocity.X = -num3 * 9; velocity.Y = -4f; }
L21291    GiveImmuneTimeForCollisionAttack(4);
L21292    eocHit = i;
L21295  if (eocHit >= 0 && !oldStyleParkour)
L21298    velocity.X = -num4 * 9;  velocity.Y = -4f;

L21426  if (dashDelay > 0) { if (eocDash > 0) eocDash--; if (eocDash == 0) eocHit = -1; dashDelay--; }
```

**The observed `vx = −9.00, vy = −4.00` matches `velocity.X = −num4*9; velocity.Y = −4f` bit for bit.**

### 4. The chain
1. **t=407** dash: `vx = +14.50`, `eocDash = 15`, `dashDelay = −1` (trace matches exactly).
2. `eocDash` decrements each tick → **10 at t=412**.
3. **t=412**: shield hits the Boss body → `eocDash = 10`, `dashDelay = 30`, **and 10 ticks of immunity**
   (hence t=413 shows 9 and 29 — fully self-consistent).
4. **t=413**: dash terminated; engine writes **`vx = −9.00`, `vy = −4.00`**.
5. Immunity expires **t=421** with the Boss still overlapping → **hit at t=422**.

### 5. ★ Why it kills: the recoil is anti-aligned with nothing useful
The player dashed **toward** the Boss (`dir2 = +1`), so the recoil is **−9.00 (left)** — and the Boss's charge
velocity is **(−9.28, −14.24)**. **The recoil is almost exactly the Boss's charge direction**, with a
magnitude (9) nearly equal to the Boss's horizontal speed (9.28), so relative horizontal speed ≈ 0.

---

## ★ The fix plan from §185.4 was WRONG — superseded

"Hold horizontal speed after the dash" **is impossible**: the −9 is written once by the engine
(L21288/L21298) and `dashDelay > 0` then only decrements counters. **The script cannot prevent it.**

## ★ And reversing the dash direction is also not viable

Reversing would point the dash **away** from the Boss — but **contact is what grants the immunity**
(`dash == 2 && i == eocHit && eocDash > 0`). No contact ⇒ no immunity ⇒ the player eats full damage.
So the dash direction is effectively **pinned** (it must aim at the Boss).

## ★★ The remaining degree of freedom: **DASH TIMING**

The model, confirmed by the trace:

| quantity | value |
|---|---|
| recoil horizontal velocity | **−9.00** (engine-fixed) |
| wing powered horizontal speed | **+7 … +8** (toward the commanded direction) |
| net | ≈ **−1 … −2** |

Observed t=413→421: `−9.00, −8.25, −7.54, −6.86, −6.22, −5.61, −5.03, −4.47, −3.95` — **exactly −9.00
converging toward 0 at ≈ +0.7/tick.** At the current dash tick (407) it is still **−3.95** when immunity
expires at t=421, i.e. still moving with the Boss's charge.

**Dashing earlier lets the wing cancel the recoil inside the immunity window:**

| dash tick | vx at immunity expiry (extrapolated) | net separation |
|---|---|---|
| **407 (current)** | **≈ −3.95** | ≈ 5.3 px/tick, caught |
| 400 | ≈ **+1.0** | ≈ 10.3 px/tick |
| 395 | ≈ **+4.5** | ≈ **13.8 px/tick** |

**The gates already exist**: `ChargeTicksSinceLock < RouteDashDelay(...)` (L2567) and
`ChargeTicksToContact(...) > DashTtcTicks` (L2568). The `CHAITE_DASH_DELAY` family was already tried on the
**strong** wing; this is the **weak** wing, and the target is now mechanistically motivated.

### Next round's experiment
Move the weak wing's dash early enough that `−9 + 0.7·Δt > 0` by immunity expiry (**Δt ≥ ≈ 13 ticks**).
Judge by **net kills** (§177), with the five-hit failures (475/600/725/750/850/900/1100) as the primary
observables. This adds a **missing pre-condition** on an existing command (satisfies §170.1) rather than
deleting anything.

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | 7 kills / 14 re-measured; failures all 5–7 contacts | worse |

**Strict `hits == 0`: strong 1175/1200/1300/2000; weak none. Not met — goal active.**

## Do NOT re-attempt

* **Holding horizontal speed after a dash** — engine-forced recoil (§185.6).
* **Reversing the dash direction** — forfeits the immunity that contact grants (§185.10).
* Ceiling off (net −5, §184.2); vertical timing (§174/175/177/181); horizontal strategy as a global change
  (§183.2); armour tier (§182.6); cycle restructuring (§183.1); phase-3 teleport for weak (never reached).
* Judge by hit count instead of net kills; nested `position` key in `hostileProjectiles` (flat `x/y/vx/vy`);
  reading `plan` from the first row only (95.9% populated); assuming a damage-record deferral (there is none).

## Standing constants

* **Shield dash**: `vx = 14.5`, `eocDash = 15`; on hit `eocDash = 10`, `dashDelay = 30`, **recoil
  `(−9, −4)` = −(dash direction)×9**, **10 ticks immunity**.
* **Wing powered horizontal**: 7–8 px/tick toward the command; recoil decay ≈ **+0.7/tick**.
* **Boss charge** in the t=402–431 window: constant `(−9.28, −14.24)`.
* **Weak cycle**: climb −9.91 (130 t), descend +10.01 (198 t), refill every 328 t at y≈6032.
* **Ceiling**: `WeakAltitudeCeilingDefault = 4400f`; 4000–4600 byte-identical, cliff 4800.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* `p2` at `life ≤ 39000`, `p3` at `life ≤ 11700`; 28 ticks/charge, hover 30/40, period 58.
* Round-184 scripts: `tmp/shield184.py`, `tmp/recon184.py`, `tmp/Player.cs` (UTF-16).
