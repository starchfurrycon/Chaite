# Round 186 handoff

## HEAD / state
**No source change.** `src/` identical to the round-180 commit. Build clean. Tests **749 pass / 9 fail**.
Shipped config stands and is the measured optimum: `WeakDashDelayDefault = 4`,
`WeakAltitudeCeilingDefault = 4400f`, `-StartSide left`.

Docs: `docs/fishron-native-truth.md` §187.1–§187.8. New dataset `kR-w*` (right start side, 10 points).

---

## 1. ★★★ The precise cause of the 386 contact class (new, and it is geometric)

`tmp/tornado186.py` tracks type-386 spawns through `hostileProjectiles[]` (**flat keys `x/y/vx/vy`**) by
`slot` first-appearance.

| run | 386 spawns | spawn y vs player | spawn \|dx\| | spawn \|dy\|<52 |
|---|---|---|---|---|
| weak **kill** dps800 | 50 | p50 **+253** | p50 **345** | 2 (4%) |
| weak **fail** dps850 | 25 | p50 **+807** | p50 **639** | 1 (4%) |
| weak **fail** dps300 | 25 | p50 **−226** | p50 **189** | 1 (4%) |

**The first six spawns are IDENTICAL across all three runs:**

```
(…, 6049) (…, 6021) (…, 6017) (…, 5997) (…, 5976) (…, 5953)     step ≈ −9.28/tick
```

**Spawn height is fixed at y ≈ 5953–6049 regardless of player position or DPS.**

### ★ And that is exactly the weak wing's refill apex
§183.1 measured the weak wing refilling at **y ≈ 6032** (p25 = p50 = p75 = 6032), **one refill every 328
ticks**.

**Spawn band 5953–6049 ↔ refill apex 6032.** So the weak wing restores its flight budget at precisely the
one height from which tornadoes are generated — which is *why* **44.7 %** of its contacts are 386 (§183.3).
That is now a geometric necessity, not a statistic.

**The strong wing escapes it only because it climbs to y 2823** (3000+ px above the spawn band). The weak
wing's climb ceiling is **y 4705** (§181), so it cannot.

## 2. Three candidate degrees of freedom refuted (this round + last)

| lever | result |
|---|---|
| dash timing (§186) | **net −2**; 0 is that axis's optimum, shipped 4 equals it |
| dash timing × ceiling (§186.7) | **anti-compose** — worse than either alone at every point |
| **start side right (§187.7)** | **net −1**; 1 kill vs 2 on the 10 common points |

Right-side detail: hit count is **never better** than left at any point, and the single kill worsens from
**4 hits to 6**.

*(The right-side sweep stopped at dps 625 because `Terraria.exe` was modified concurrently — the probe's
integrity check working correctly, not a failure.)*

## 3. Why the refill apex cannot be moved (triple-locked)

1. The apex is **climb rate (−9.91) × budget (130)** → not compressible (§183.1).
2. Descending *earlier* to raise it is **unreachable** — the weak wing is never above y 4705 while climbing
   (§181).
3. A **shallower descent never zeroes `velocity.Y`**, so the native apex refill (`Player.cs:26992`) never
   fires and the budget stays 0 forever (§183.1).

## 4. The only mechanistically grounded direction left

**Move the spawn band itself.** It follows the Boss's hover, and the Boss's hover follows the player. So the
remaining work is **horizontal shaping** — deliberately choosing the player's horizontal position to move
the Boss's hover phase, and therefore where the tornado wall lands (§183, §187.4). This is the one avenue
that has a mechanism and that the 386 finding now specifically motivates.

Secondary, cheap, and still open:
* **The 2-hit kills are existence proofs** — dps 950 (2h) and 800 (2h) at delay 4. Characterise those
  trajectories and ask whether the enabling condition is controllable.
* **Finish the shroomite arm with the ceiling** for the both-tier report the objective requires
  (`tmp/grid180.ps1`, ~28/132 done).

## Acceptance status

| | obsidian | shroomite |
|---|---|---|
| strong wing | **28/28 kills** | not re-run with the ceiling |
| weak wing | **11 kills / 27 points** (shipped optimum) | worse (§182.6) |

**Strict `hits == 0`: strong 1175/1200/1300/2000; weak none. Not met — goal active.**

## Do NOT re-attempt

* **Dash timing** (any value), **its combination with the ceiling**, **start side right** — all refuted.
* Holding horizontal speed after a dash (engine recoil, §185.6); reversing the dash direction (forfeits the
  immunity, §185.10); ceiling off (−5); vertical timing (§174/175/177/181); global horizontal strategy
  (§183.2); armour tier (§182.6); cycle restructuring (§183.1); phase-3 teleport for weak (never reached).
* Judge by hit count instead of net kills; nested `position` in `hostileProjectiles` (**flat `x/y/vx/vy`**);
  reading `plan` from the first row only (95.9 % populated); assuming a damage-record deferral (**none**).
* ⚠ `Terraria.exe` was modified during this session — reruns may trip the probe's integrity check; verify
  the binary is stable before a long sweep.

## Standing constants

* **386 spawn height: y ≈ 5953–6049, FIXED** (step −9.28/tick), independent of player and DPS.
* **Weak refill apex: y ≈ 6032**, one refill per **328 ticks**; climb −9.91 (130 t), descend +10.01 (198 t);
  climbing y **never above 4705**.
* **Strong climbs to y 2823**; vertical range 5135 vs weak 3371.
* **Shield dash**: `vx = 14.5`, `eocDash = 15`; on hit `eocDash = 10`, `dashDelay = 30`,
  **recoil `(−9, −4)`**, **10 ticks immunity**.
* **Boss charge** in the t=402–431 window: `(−9.28, −14.24)`.
* **Ceiling**: 4000/4200/4400/4600 byte-identical, cliff 4800.
* **Contact boxes**: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* `p2` at `life ≤ 39000`, `p3` at `life ≤ 11700`; 28 t/charge, hover 30/40, period 58.
* Round-186 scripts: `tmp/tornado186.py`, `tmp/tab186.py`.
  Artifact tags: `game-probe-kG-w-<dps>` (hyphenated), `game-probe-kR-w<dps>` (not).
