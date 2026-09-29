# Round 199 handoff — the fix was implemented, measured, and reverted (net −3)

## HEAD / state
**No source change.** `src/` reverted via `git checkout` and verified **clean**; `BeatFromSequence` absent;
rebuild **exit 0**. Build clean. Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅

New datasets: `kE3-s*` (switch off — regression check, 4 points), `kF3-s*` (switch on, 8 points).

---

## 1. What was implemented

Per §202.4, the W-cycle beat index was moved from the accumulated dash count to the native attack sequence,
behind a **default-off** switch `CHAITE_BEAT_FROM_SEQUENCE`:

```csharp
var beatIndex = BeatFromSequenceArmed ? input.NativeSequence : _chargeIndex;
_chargeBeat   = beatIndex % 3;
var slot      = (beatIndex - 1) % pattern.Length;
_chargeIndex++;
```

With the switch off, `beatIndex == _chargeIndex`, so the expression is **textually equivalent to the old
formula**.

## 2. ★ The default path is safe — byte-identical regression check

| dps | recorded baseline | rebuilt, switch off | |
|---|---|---|---|
| 300 | 15938 / 6 / KILL | **15938 / 6 / KILL** | ✅ |
| 450 | 10937 / 4 / KILL | **10937 / 4 / KILL** | ✅ |
| 500 | 7304 / 5 / DIED | **7304 / 5 / DIED** | ✅ |
| 650 | 7738 / 3 / KILL | **7738 / 3 / KILL** | ✅ |

**The refactor itself changed nothing on the default path.**

## 3. ★★★ With the switch ON: net **−3**

| dps | old (accumulated) | **new (native sequence)** | change |
|---|---|---|---|
| 300 | 15938 / 6 / **KILL** | 11589 / 9 / DIED | **LOST KILL** ❌ |
| 350 | 13901 / 5 / **KILL** | 9907 / 8 / DIED | **LOST KILL** ❌ |
| 400 | 12240 / 5 / **KILL** | 9648 / 8 / DIED | **LOST KILL** ❌ |
| 450 | 10937 / 4 / KILL | 10928 / 6 / KILL | kept (hits 4→6) |
| 500 | 7304 / 5 / DIED | 8116 / 7 / DIED | still fails |
| 550 | 7531 / 5 / DIED | 7185 / 7 / DIED | still fails |
| 650 | 7738 / 3 / KILL | 7737 / 7 / KILL | kept (hits 3→7) |
| 600 | (not measured) | 8336 / 6 / KILL | new (no control) |

**Common-point wins: old 5/7 vs new 2/7 ⇒ net −3 kills.** Not a shift — a **general degradation**: the three
passing low points all died, and the 500/550 hole **did not close**.

## 4. ★★ Why it failed — the two indices are not equivalent

`ai[3]` and "accumulated dash count" both look like "which attack within the group this is", but:

1. **Phase-1 and phase-2 attack groups are numbered differently** (different `num28` ranges), so `ai[3]`
   **renumbers** when `flag` flips and the modulo lands the beat on the **wrong slot**;
2. **`ai[3]` also advances *within* a state** (it marks group changes too), while the accumulated count adds
   once per dash — so they are already out of step *inside a single charge*;
3. hence at the fork (tick 4963, both `ai[3] = 3`) the **instantaneous values agree but the histories do
   not**, so the post-modulo phase still differs.

**`ai[3]` is not an equivalent observable for the accumulated dash count; swapping the index does not remove
the desynchronisation, it replaces it with a different one.**

## 5. ★★★ The conclusion this round actually produces

**"Keep two runs on the same beat" is unattainable, because the two runs face genuinely different boss
behaviour** (a phase-1 dash vs the phase-2-only group, §202.2).

**Any strategy keyed to "which charge this is" must desynchronise at a phase crossing. That is a necessity of
the information available, not a matter of picking a better index.**

**The only viable direction is therefore to remove the dependence on an accumulated beat entirely: decide
every frame from the threat observable *in that frame* — `(ai[0], ai[2], ai[3], player/boss geometry)` —
rather than from "which beat of the W-cycle it is my turn to walk".**

That is a substantially larger change than swapping an index, but it is the only direction this round's
measurements leave open.

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

**NEW: sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill (+1/−5), altitude floor (−3), ceiling off (−5), **descent floor
(byte-identical and physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling
(anti-composes); start side right (−1); holding horizontal speed after a dash; reversing the dash direction;
global horizontal strategy; phase-3 teleport for weak. Type-384 bubble = negligible (60–78 dmg).
DPS-conditional standoff gate = byte-identical A/B. `IsReachableState` — legal for state 4.

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — it is 480**; "the fork is invisible
in observables" **FALSE** (native state differs from tick 4963); **"`ai[3]` is an equivalent proxy for the
accumulated dash count" FALSE (§203.4).**

## Method notes

* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **Env-gate any behaviour change and verify the default path is byte-identical before interpreting an A/B.**
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000 / 100 s).
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
