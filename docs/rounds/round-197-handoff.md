# Round 197 handoff — the fork converges to ONE tick and ONE input

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅
New dataset `kD3-s*` (gate forced off, 6 points); new script `tmp/diverge197b.py`.

---

## 1. Refuted: the DPS-conditional standoff gate

`FishronWingScript.cs` L104 `StandoffDistanceArmed` branches on `CHAITE_SIM_DPS`:

```csharp
return dps <= StandoffLowDpsMax;      // L132, default 450 (L150)
```

So `dps450 → armed` (radius 1600 px, true separation) and `dps500 → off` (720 px horizontal gap).
A second gate of the same class exists: `TornadoAxisGuardMaxDps` (L1155, also reads `CHAITE_SIM_DPS`).

**Both are genuine instances of "simulated DPS steering movement".** But an A/B with
`CHAITE_STANDOFF_DPS_MAX=0` (gate forced off; falls back to 450 = the default) is **byte-identical at
every probed point**:

| dps | gate on (baseline) | gate forced off | change |
|---|---|---|---|
| 300 | 15938 / 6 / KILL | **15938 / 6 / KILL** | none |
| 400 | 12240 / 5 / KILL | **12240 / 5 / KILL** | none |
| 450 | 10937 / 4 / KILL | **10937 / 4 / KILL** | none |
| 500 | 7304 / 5 / DIED | **7304 / 5 / DIED** | none |
| 550 | 7531 / 5 / DIED | **7531 / 5 / DIED** | none |
| 650 | 7738 / 3 / KILL | **7738 / 3 / KILL** | none |

**⇒ The gate is inert in this band and is NOT the cause.** (It remains a hazard worth removing — §4.)

## 2. ★★★ The fork is ONE tick and ONE input difference

Tick-by-tick alignment of the passing dps 450 run and the failing dps 500 run:

| tick | A (dps450) | B (dps500) | verdict |
|---|---|---|---|
| 4967 | x=3096.84 **vx=-3.934** | x=3096.84 **vx=-3.934** | **identical** |
| 4968 | x=3092.67 **vx=-4.166** | x=3092.67 **vx=-4.166** | **identical** |
| **4969** | x=3088.27 **vx=-4.398** | x=3088.27 **vx=-4.398** | **★ position AND velocity identical** |
| **4970** | x=3073.77 **vx=-14.500** | x=3083.78 **vx=-4.491** | **★★ A shield-dashed, B did not** |

**At tick 4970 A's `vx` becomes exactly `-14.500`** — the literal `velocity.X = 14.5f * dir2` of the
Shield of Cthulhu dash — while B's `-4.491` shows it merely kept cruising.

**At the decision frame (tick 4969) every input is identical — player position, player velocity, player
life, boss position, boss velocity, projectile counts — except `boss life: 42525 vs 38584`.**

## 3. ★★★ That life difference straddles the phase-2 threshold

`lifeMax = 78000`, phase-2 threshold = **39000**:

* dps 450: **42525 > 39000 → still PHASE ONE**
* dps 500: **38584 ≤ 39000 → already PHASE TWO**

**So at the very same tick, one run faces a phase-1 Fishron and the other a phase-2 Fishron — and those
differ in `num3` (hover duration) and `num28` (attack group) via `flag` (§200.2).**

**This resolves the paradox of "identical inputs, different decision": the state machine's *internal memory*
(`FishronPreviousNativeState/Sequence`, the trackers that feed `ValidNativePhase`) had already diverged
earlier, because the boss's cadence diverged. Memory is not observable, so a purely positional diff cannot
see it.** The §200.4 mechanism is thereby confirmed at the finest possible resolution.

## 4. Two actionable conclusions

1. **Remove the DPS-steering gates.** `StandoffDistanceArmed` (L104–136) and `TornadoAxisGuardMaxDps` (L1155)
   must not read `CHAITE_SIM_DPS`. Even though this A/B shows them inert at 300–650, their presence means any
   DPS sweep can be contaminated by a gate flip, and it violates the premise that movement is more than a
   function of the simulated damage setting.

2. **The real repair is de-memorising (or phase-aligning) the decision.** The machine must not depend on
   states obtainable only by *accumulated observation* ("which charge number this is"), because accumulated
   observation shifts wholesale with the tick at which the boss crosses a phase. It should decide each frame
   from the currently observable `(ai[0], ai[2], ai[3], life band)`, demoting
   `FishronPreviousNativeState/Sequence` from a *criterion* to a *hint*.

## 5. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28 @20000**; low band 300–450 pass, hole at 500/550 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000; completes from dps 800 @20000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. The type-384 bubble is negligible (60–78 dmg).
Judge by **net kills**, never hit count. **NEW: the DPS-conditional standoff gate (A/B byte-identical).**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "the contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — it is 480.**

## Method notes

* **The harness is deterministic** — one run per configuration suffices and is exactly reproducible.
* **Boss AI reads its own `life` every frame** and re-phases its attack cadence from it (§200) — so any
  change to the player's damage history re-shuffles the fight.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s). A timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`; always state it.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is now ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
