# Round 188 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.
Shipped config: `WeakDashDelayDefault = 4`, `WeakAltitudeCeilingDefault = 4400f`, `-StartSide left`.
**Verified binary confirmed before and after the sweeps: `960A03BFF6050CF7…` ✅**

---

## 1. ★★ The shroomite arm is complete, and the earlier verdict on it was WRONG

§182.6 had declared shroomite **strictly worse** on the basis of **two points**. The full 33-point grid
refutes that.

| | measured | kills | kill rate |
|---|---|---|---|
| **obsidian** (low tolerance) | 27 | **11** | 41 % |
| **shroomite** (high defence) | 33 | **17** | **52 %** |

**Common 27 points: obsidian 11 vs shroomite 12 ⇒ net +1.**

* **shroomite gains** 825, 850, 900, 1100 — **exactly the hard middle points every rewrite-shaped lever
  failed to repair** (§186).
* **shroomite loses** 575, 950, 1000.

### They are different in kind, not ordered
* **Mean hits on common points: obsidian 4.89, shroomite 5.41** — shroomite is rougher *on average*.
* **But shroomite produces the cleanest weak-wing fight measured so far: 1 hit.**

| | points with hits ≤ 2 |
|---|---|
| obsidian | 800 (2h), 950 (2h), 1075 (2h) |
| **shroomite** | 650 (2h), **1200 (1h)**, 1700 (2h), 2000 (2h) |

Also notable: **shroomite dps 550 ends with the boss on 285 life** and **dps 750 on 3056** — near-misses.

**Obsidian stays the primary/honest yardstick because the owner specified it, but shroomite is not a
degraded tier.** `CHAITE_ARMOR_TIER=shroomite` is the better arm for the *strict* goal; obsidian is the
steadier one for pure survival.

## 2. ★ The releaseJump pulse — a premise correction that licenses one new lever

Decompiled condition (`Player.cs:26992`):

```
if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump && justJumped))
    wingTime = wingTimeMax;
```

**`releaseJump` is a ONE-FRAME PULSE**, not "is the key currently up":
`L21213 releaseJump = false;` / `L21218 releaseJump = true;`

**So the mid-air refill fires only on a frame that is both an apex AND the release transition.**

### Consequence
The weak wing's measured refill point **y ≈ 6032 is 1327 px BELOW its climbing ceiling of y 4705** ⇒
**y 6032 is the bottom of the descent, not the top of the climb.** §181/§183/§186 all treated it as an apex
and concluded "fixed by climb rate × budget, not compressible". **That premise is false** — it is set by how
deep/long the descent runs, which is a **behaviour parameter**.

Per §170.1 this is the **only remaining candidate with the right shape** (it adds a missing pre-condition
rather than rewriting a quantity).

### But it is NOT yet confirmed as the explanation
The newly measured **1–2 hit trajectories at shroomite 1200/2000/1700** show the contact count is **not**
locked by y ≈ 6032 alone. **§189.3's claim is a hypothesis, not an established cause.**

### Where it would be implemented
```
L63    private const float FloorMargin = 90f;
L1564  private float _floorY;   L1565 _ceilingY;
L2165  _ceilingY = SkyEnrageCeiling + CeilingMargin;
L2167  _floorY = support.Valid && !support.Inverted && ...   // degenerates to a constant on flat ocean
L3763  if (y - player.Height * 0.5f <= _ceilingY) vertical = 1;
L3764  else if (y >= _floorY - FloorMargin && vertical > 0) vertical = 0;
```
`CHAITE_WEAK_ALTITUDE_CEILING` acts on the **ceiling** side only. **The floor/descent side has never been
parameterised.**

## 3. Complete acceptance report (owner's format)

| armour tier | weak wing (fairy) | strong wing (fishron) |
|---|---|---|
| **obsidian** (honest) | **11 / 27 (41 %)** | **28 / 28 (100 %)** |
| **shroomite** | **17 / 33 (52 %)** | 6-point sample, all killed |

* Criterion applied: **survived AND boss killed** (`SuccessNoDeath`, `bossLifeRemaining == 0`).
* **Strict goal (`hits == 0`): NOT met — neither tier, neither wing.**
  Closest: **shroomite weak wing dps 1200 at 1 hit**.
* **No run is described as no-hit.**

## 4. What is left

1. **Parameterise the descent floor** (`_floorY` side) with a weak-wing clamp and measure it as a *new
   pre-condition* — the one candidate of the correct §170.1 shape. Must be judged by **net kills**.
2. **Characterise the 1-hit trajectory at shroomite dps 1200** — the existence proof closest to the strict
   goal. Ask whether its enabling condition is controllable.
3. **Run the strong wing on the shroomite grid properly** (only 6 sample points done).
4. **Try `CHAITE_ARMOR_TIER=shroomite` together with the round-185-validated knobs** — since shroomite now
   changes the outcome at 825/850/900/1100, the interaction with the ceiling may differ from obsidian.

## Do NOT re-attempt

Dash timing (0/1/2/3/4/7/10/14 — closed at both ends); dash timing × ceiling (anti-composes); start side
right (−1); holding horizontal speed after a dash; reversing the dash direction; ceiling off (−5); altitude
floor (−3); apex refill (+1/−5); dash veto (0/−1); global horizontal strategy (§183.2); phase-3 teleport for
weak (never reached). Judge by **net kills**, never hit count.
**Corrected this round: "shroomite is strictly worse" is FALSE — it is net +1.**

## Standing constants

* **386 tornado spawn y ≈ 5953–6049 FIXED**; weak refill point y ≈ 6032 (descent floor, **not** an apex).
* **Weak**: climb ceiling y **4705**; descend floor y **7958**; vertical range **3371**; refill every 328 t.
  **Strong**: reaches y **2823**; range **5135**.
* **Shield**: `vx 14.5`, `eocDash 15`; on hit `eocDash 10`, `dashDelay 30`, recoil `(−9, −4)`, **10 t immunity**.
* **`releaseJump` = single-frame pulse** ⇒ apex refill needs apex-frame ∧ release-frame.
* **Boss charge** t=402–431: `(−9.28, −14.24)`; 28 t/charge; hover 30/40; period 58.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Armour item IDs: obsidian 3266/3267/3268; shroomite 1547/1549/1550.
* **⚠ Hash-check `Terraria.exe` before every long sweep** (§187.0).
* Round-188 scripts: `tmp/tab188.py`; datasets `kT-w*`, `kT-s*`, `kS-w*-d*`.
