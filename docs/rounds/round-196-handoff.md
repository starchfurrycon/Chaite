# Round 196 handoff — ★★★ ROOT CAUSE OF THE DPS SENSITIVITY

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅
New scripts: `tmp/diverge196{,b,c,d,e}.py`; new decompile `tmp/NPC.cs`.

---

## 1. ★★★ THE ROOT CAUSE

**`AI_069_DukeFishron` reads its own `life` every frame and uses it to choose its attack group.**

`tmp/NPC.cs` L49296 onward:

```csharp
private void AI_069_DukeFishron()
{
    bool expertMode = Main.expertMode;
    float num = (expertMode ? 1.2f : 1f);
    bool flag  = (double)life <= (double)lifeMax * 0.5;                  // L49300  ← phase 2
    bool flag2 = expertMode && (double)life <= (double)lifeMax * 0.15;   // L49301  ← phase 3
    ...
    int num3 = (expertMode ? 40 : 60);            // hover duration
    if (flag4) { num4 = 0.7f; num5 = 12f; num3 = 30; }
    ...
    if (ai[2] >= (float)num3)                     // L49622  hover timer elapsed
    {
        ...
        if (flag) { num28 = 4; }                  // L49652  ← PHASE 2 FORCES THE ATTACK GROUP
        switch (num28)
        {
        case 1: ai[0] = 1f; ...                   // L49659  charge
        ...
```

**`flag` rewrites `num3` (hover duration) and `num28` (which attack the boss performs next).**

**The DPS simulation subtracts health. So different DPS ⇒ the boss crosses `life ≤ lifeMax*0.5` at a
different tick ⇒ `flag` flips at a different tick ⇒ the whole attack cadence re-phases.**

## 2. The complete, fully deterministic causal chain

```
dps 450 vs 500
 -> different per-tick health subtraction
 -> boss life already differs by 1 at tick 240
 -> boss crosses life <= 39000 (phase 2) ~83 ticks apart
 -> flag flips at a different tick
 -> num3 (hover duration) and num28 (attack group) differ from then on
 -> the 58/68-tick hover cadence shifts phase
 -> at tick 4962 the refill exit branches: charge-ascend vs standoff
 -> at tick 4970 a 10-pixel horizontal dash in the OPPOSITE direction
 -> trajectories diverge permanently
 -> dps 500 takes an extra boss-body hit at t=5498 (413 -> 229)
 -> the 386 cluster at t=6847 lands at 296 life instead of 480
 -> death
```

## 3. The evidence, tick by tick

| check | result |
|---|---|
| first differing field anywhere up to the fork | **`npcs.0.life` only**, ±1 at **tick 240** |
| player position / velocity, boss position / velocity, projectile counts | **identical every tick** |
| first player divergence | **tick 4970**, x differs by **10.01 px** (one dash) |
| first `plan.phase` divergence | **tick 4963**: A → `charge-ascend`, B → `standoff` |
| boss `ai[0]` divergence (shield-events) | A `t=4970 ai0=1` vs B `t=5190 ai0=6` |

## 4. ★★★ Why this explains almost every historical dead end

1. **Why every "rewriting" geometric lever has been zero-sum:**
   any change to movement changes the player's damage history → changes the boss's cumulative health →
   **changes the tick at which the boss crosses phase 2** → **re-shuffles the entire fight cadence.**
   The gain from the change and the disruption it causes are the same order of magnitude, so the net is
   noise.

2. **Why the contact rate is discontinuous across DPS** (0.313 / 0.360 / 0.408 / 0.664 / 0.685):
   because the boss's cadence is extremely sensitive to the phase-crossing tick, and the phase-crossing tick
   is set by DPS.

3. **Why "reduce the contact rate" cannot be reached by single-point repair:**
   what is actually required is **invariance to a shift in the boss's cadence phase**, not a "better
   trajectory".

## 5. ★★ The new objective — different in kind from everything tried before

**From "find a better trajectory" to "find a trajectory insensitive to the boss's cadence phase".**

Concretely: derive the state machine in real time from the boss's **observable** state (`ai[0]`, `ai[2]`,
`ai[3]`, life band) rather than from counters like "which charge number this is", so that when `flag` flips
and the cadence re-phases, the machine follows automatically.

**Test for the next round (already localised):** the 450-vs-500 fork is exactly the **refill exit at tick
4963**. If that exit's decision is made to depend only on the boss's currently observable state, 450 and 500
should leave through the same branch and **both pass**.

## 6. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill |
|---|---|---|
| strong + obsidian | **4** (1175, 1200, 1300, 2000) | **28/28 @20000**; low band 300–450 pass, hole at 500/550 |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | 5/15 @6000; completes from dps 800 @20000 |
| weak + shroomite | **0** | 7/15 @6000 |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `Accepted: zero hits in the native engine.` line.

## Do NOT re-attempt

**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. The type-384 bubble is negligible (60–78 dmg).
Judge by **net kills**, never hit count.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "the contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — it is 480.**

## Method notes

* **The harness is deterministic** — one run per configuration suffices and is exactly reproducible.
* **`Chaite.Core` movement must be robust to the boss's phase-crossing tick**, because the boss's attack
  cadence depends on its own life (§200).
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s). A timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`; always state it.
* `-WallSeconds` caps at **900**; larger values abort every run silently.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is now ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
