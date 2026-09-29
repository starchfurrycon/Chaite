# Round 198 handoff — ★★★ COMPLETE ROOT CAUSE OF THE DPS SENSITIVITY

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅

---

## 1. Correction to §201: the difference IS observable

The round-197 claim that tick 4969's only difference was boss life was **wrong** — that diff script never
parsed `npcs[0].ai`. Reading it shows the fork is observable from **tick 4963**:

| tick | A (dps450) | B (dps500) | |
|---|---|---|---|
| **4963** | **state=1** seq=3 timer=1 | **state=4** seq=3 timer=1 | **★ native state differs** |
| 4969 | state=1 seq=3 timer=7 | state=4 seq=3 timer=7 | |
| 4970 | state=1 seq=3 timer=8 | state=4 seq=3 timer=8 | |

**`ai[3]` (sequence) and `ai[2]` (timer) are identical throughout.**

## 2. ★★ What states 1 and 4 are natively

`tmp/NPC.cs` L49652–49692:

```csharp
if (flag) { num28 = 4; }        // L49652  phase 2 FORCES the group
switch (num28)
{
case 1: ai[0] = 1f; ...         // L49659  the dash
case 4: ai[0] = 4f; ...         // L49689  ★ phase-2-ONLY extra group
}
```

**`ai[0]=4` is an attack group that only phase two can select** (via `flag`); `ai[0]=1` is a dash.

So dps450 (boss 42525 > 39000, **phase 1**) sees a **dash**, while dps500 (boss 38584 ≤ 39000, **phase 2**)
sees the **phase-2-only group**. **That difference is legitimate and unavoidable.**

## 3. ★★★ The actual defect: the beat index only advances on dash states

`FishronWingScript.cs`:

```csharp
var dash = state == 1 || state == 6 || state == 11;    // L1633
...
if (dash && stateEdge)                                  // L1662
{
    _chargeBeat = _chargeIndex % 3;                     // L1668  ← the W-cycle beat
    _chargeIndex++;                                     // L1669  ← ★ only on dash
    ...
    var slot = (_chargeIndex - 1) % pattern.Length;      // L1680
    _patternDirection = pattern[slot];
```

**`_chargeIndex` is an *accumulated* counter, advanced only on the state edge of `{1, 6, 11}`.**

* dps450: boss enters `state=1` → **`_chargeIndex++`** → the beat advances
* dps500: boss enters `state=4` → `dash == false` → **`_chargeIndex` frozen** → the beat stalls

**⇒ From tick 4963 the two runs' W-cycle beat indices differ by one.** At tick 4970 dps450's beat is the one
that spends the Shield dash (`vx` set to exactly `-14.500` = `velocity.X = 14.5f * dir2`), while dps500 is
still in `Cruise` (`vx=-4.491`). This matches the exported `plan.phase` exactly (dps500 stayed in
`fishron-wing-standoff` because `dash == false` routes to `Cruise`).

## 4. ★★★ The concrete fix

**The W-cycle beat is indexed to the *accumulated dash count*, which desynchronises whenever the boss's phase
transition selects a non-dash group.**

**Change the index from an accumulated count to the native observable sequence:**

```csharp
// now — accumulated, desynchronises
_chargeBeat = _chargeIndex % 3;
_chargeIndex++;

// proposed — native-sequence driven, cannot desynchronise
_chargeBeat = ((int)input.NativeSequence) % 3;
```

`ai[3]` is **identical (3) in both runs at the fork**, and natively already encodes "which attack within the
group this is".

**This is the precise location of the "entanglement with the beat schedule" that this project's own comments
warn about** (L2436–2439: *"a rule that is CORRECT in isolation can still be a worse controller than the
heuristic it replaces, because the heuristic is entangled with the beat schedule"*), and it is the mechanism
behind §122/§128. The `_legCharges` / `_legProgress` scheduler (L1692–1718) is the same design.

**Verification for the next round:** derive `_chargeBeat` / `_patternDirection` from `input.NativeSequence`,
then re-run 450 and 500; if they take the same branch after tick 4963, the desynchronisation is gone.

## 5. Confirmed NOT the cause: `IsReachableState`

`IsReachableState` (L2100) accepts `-1..12` with `timer >= 0 && sequence >= 0`. `state=4, timer=7, seq=3` is
**fully legal**, so dps500 was not rejected early — it entered the machine and took the `Cruise` branch
because `dash == false`.

## 6. Acceptance master table (native tick-replay only)

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
global horizontal strategy; phase-3 teleport for weak. Type-384 bubble = negligible (60–78 dmg).
DPS-conditional standoff gate = byte-identical A/B. **NEW: `IsReachableState` — legal for state 4.**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — it is 480**; **"the fork is invisible
in observables" FALSE — the native state differs from tick 4963.**

## Method notes

* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
  A diff that skips it will miss the decisive field.
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices and is exactly reproducible.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s).
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
