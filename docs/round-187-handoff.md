# Round 187 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.
Shipped config: `WeakDashDelayDefault = 4`, `WeakAltitudeCeilingDefault = 4400f`, `-StartSide left`.

**Verified binary restored and re-checked at end of round: `Terraria.exe` = `960A03BFF6050CF7…` ✅**

---

## 0. ★ READ FIRST — the environment was broken and is now fixed

Mid-round the probe refused to launch: **`Only verified vanilla 1.4.5.8 supported`**. The executable hash had
changed to `275D1088082AFB…` while the version still read 1.4.5.8, and a **`ZhaDai.Runtime.dll`** appeared in
the install alongside `%TEMP%\zhadai-*` staging directories containing `Terraria.patched.exe`. **An external
tool patched the game.** A later check showed the executable **restored to the reviewed hash**, all four
dependencies matching, `Content/` present, and no Terraria process holding the file.

* The inert `ZhaDai.Runtime.dll` **remains** in the install — it is harmless to a vanilla process and the
  probe does not check it.
* A second verified fixture exists at `%TEMP%\zhaodai-sandbox\Terraria.exe` but **lacks `Content/` and the
  four dependencies**, so it cannot serve as `-GameDirectory` alone.
* **⚠ Verify the hash before every long sweep:**
  `(Get-FileHash "D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe" -Algorithm SHA256).Hash`
  must equal `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`.
* **If it happens again: do NOT modify source and do NOT relax the hash.** The probe's refusal is correct
  behaviour. Wait for the executable to return to the reviewed value.
* Round-187 results taken before 11:09:32 are **unaffected**; the right-side dps-625 point was correctly
  stopped by the probe's `Preparation input changed concurrently` check.

---

## 1. Both weak-wing contact classes are now geometrically explained

This is the consolidated result of rounds 180–186, and it is the thing to build on.

### Class A — tornado 386 (~45 % of weak contacts)
**Spawn height is FIXED at y ≈ 5953–6049** (first six spawns identical across three unrelated runs, step
−9.28/tick = the Boss's own vertical speed). That band is **exactly the weak wing's refill apex, y ≈ 6032**
(one refill per 328 ticks, p25 = p50 = p75 = 6032).

**So the weak wing restores flight budget at precisely the height from which tornadoes are generated.**
The strong wing escapes only by climbing to **y 2823**; the weak wing's ceiling is **y 4705**.

### Class B — Boss body 370 (~47 % of weak contacts)
The deterministic contacts at **t=422 / t=2186** are the engine's own post-hit recoil
(`velocity.X = -num4*9; velocity.Y = -4f;`, `Player.cs:21298`), which throws the player **into** the Boss's
charge vector `(−9.28, −14.24)` at nearly the same speed, so relative horizontal speed ≈ 0 and the 10-tick
immunity expires while still overlapping.

### ★ Why neither can be fixed by moving the player
The weak wing's vertical cycle spans **y 4700 ↔ 7958** (climb ceiling 4705, floor 7958) — and the tornado band
(5953–6049) lies **squarely inside** that span. **The player must cross the band on every descent, and the
band cannot be moved out of range in either direction.** Moving the refill apex is triple-locked (§183.1,
§181, §185.6–185.10).

## 2. Levers tested and refuted (rounds 178–187)

| lever | net |
|---|---|
| ceiling default 4400 (shipped) | **+5** ✅ |
| ceiling off | −5 |
| `CHAITE_WEAK_ALTITUDE_FLOOR` | −3 |
| apex refill (weak) | +1/−5 |
| dash veto | 0/−1 |
| "descend earlier to raise apex" | 0 (unreachable) |
| dash timing 0 | −2 |
| dash timing 1 | worse |
| **dash timing 7 / 10 / 14** | **worse — axis now closed at both ends** |
| dash timing × ceiling | anti-compose |
| start side right | −1 |
| armour tier shroomite | strictly worse |

**★ The dash-timing axis is now COMPLETELY swept (0/1/2/3/4/7/10/14) and shipped 4 is its only optimum.**
Above 4 (this round, §188.5): dps 400 goes 6h → **7h** at both 7 and 10; dps 500 → 7h at 10; dps 550 → 7h at
10; dps 700 → 6h; dps 725 → 6h. **No point improved from death to kill.**

**The only genuine win in the whole span is the altitude ceiling (+5)**, and per §170.1 its shape was the
only kind that ever works: it **added a missing pre-condition** rather than rewriting an existing command.
Every lever that rewrites a quantity (dash timing, start side, altitude floor, apex refill) has been
zero-sum or negative — that is now a strong, repeatedly confirmed pattern.

## 3. The quantified gap

* Every weak **failure: 5–7 contacts. Every weak kill: 2–4.** The survival ceiling is **4 contacts**.
* Damage per hit is nearly equal between wings (**weak 137.9 vs strong 184.7**), and life lost at dps 300 is
  **identical (885 vs 884)** — so **only the contact count separates survival from death**.
* Weak contact rate **1.06 per 1000 ticks** vs strong **0.38** — **2.8×**, and that ratio follows directly
  from the vertical range (**weak 3371 vs strong 5135**).
* Weak's hard core: **300, 400, 450, 475, 500, 525, 550, 600, 700, 725, 750, 825, 1200, 1400** — all
  **dps ≤ 1400**, fights **5000–11000 ticks**.

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | **11 kills / 27 points** | worse |

**Strict `hits == 0`: strong 1175/1200/1300/2000; weak none. NOT MET — goal active.**

**Honest statement of what is and is not achieved:** the strong wing satisfies the owner's relaxed criterion
across the whole band. The weak wing does not — it survives-and-kills at 11 of 27 measured points, and its
failures are a direct consequence of the fairy wing's vertical parameters, which the owner fixed as the only
difference between the two loadouts.

## 4. What is genuinely left

1. **Characterise the 2-hit kills** (dps 950 and 800 at delay 4; 950 at delay 0). These are existence proofs
   that the circuit *can* run a near-clean fight with the weak wing. Finding whether their enabling condition
   is controllable is the highest-value remaining work.
2. **The 386 spawn is player-position-dependent in x** (p50 |dx| 189 in the failing dps300 run vs 345–639 in
   others). The band's **height** is fixed, but its **horizontal position** at spawn is not, and the player is
   near it every cycle. A horizontal-shaping rule is the only untried avenue with a mechanism.
3. **Finish the shroomite arm with the ceiling** for the both-tier report the objective explicitly requires.
4. **Longer horizon**: the owner relaxed the tick cap to allow longer fights; a 20000-tick weak run at very
   low dps is the honest way to test whether the fight is winnable at all below ~400 dps.

## Do NOT re-attempt

Ceiling off; altitude floor; apex refill; dash veto; dash timing (0/1/above-4); dash timing × ceiling;
start side right; holding horizontal speed after a dash; reversing dash direction; armour tier; cycle
restructuring; phase-3 teleport for weak (never reached); global horizontal strategy (§183.2).
Judge by **net kills**, never hit count. `hostileProjectiles` uses **flat `x/y/vx/vy`**. There is **no**
damage-record deferral. `plan` is populated on 95.9 % of rows.
⚠ `Terraria.exe` was modified once during this session — verify binary stability before long sweeps.

## Standing constants

* **386 spawn y ≈ 5953–6049 FIXED**; strong's tornado band 2740–6920.
* **Weak refill apex y ≈ 6032**, every **328 t**; climb −9.91 (130 t); descend +10.01 (198 t);
  climbing y **never above 4705**; vertical range **3371** (strong **5135**, reaching y 2823).
* **Shield**: `vx 14.5`, `eocDash 15`; on hit `eocDash 10`, `dashDelay 30`, recoil `(−9, −4)`, **10 t immunity**.
* **Boss charge** t=402–431: `(−9.28, −14.24)`; 28 t/charge; hover 30/40; period 58.
* `p2` at `life ≤ 39000`, `p3` at `life ≤ 11700`.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Acceptance env: `CHAITE_ARMOR_TIER`, `CHAITE_SIM_DPS`, `CHAITE_SIM_DPS_FULL_TILES=400`,
  `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`, `CHAITE_PROBE_DENSE_FRAMES=1`,
  `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-fairy-wing|fishron-strong-wing`.
* Round-187 scripts: `tmp/latedash187.ps1` (inline), `tmp/tab186.py`, `tmp/tornado186.py`.
