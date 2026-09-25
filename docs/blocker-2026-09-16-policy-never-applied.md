# Blocker found 2026-09-16: the learned policy never reached the game

## What was wrong

`FormulaScriptOutput` is a **struct** (`FormulaScriptController.cs:16`). All five
route scripts passed it to their residual hook **by value**:

```csharp
private static void DecideMovement(in FormulaScriptInput input,
    PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
    FormulaScriptOutput output)          // <-- struct, passed by value
{
    ...
    output.Horizontal = horizontal;      // written to a discarded copy
    output.Vertical   = vertical;
    output.Jump       = jump;
    output.Dash       = dash;
    output.Phase      = "empress-flight-learned";
}
```

Every field assignment, including the phase marker, mutated a temporary copy
that was discarded when the method returned. Consequence: **the trained policy
had exactly zero effect on every fight.**

## How it was proven

`CHAITE_TRACE_FILE` was set to enable an opt-in trace (the helper is still in
`LearnedPolicy.TraceDiag`, inert unless the variable is set). One run of the
fixed machine with a deliberately extreme policy (head biases ±1000, so
horizontal is forced backwards and vertical forwards at every tick) produced:

```
APPLIED phase=empress-flight-initial-reposition h=-1 v=1
EVAL ok
RET -1,1,False,False
MUTATED copy phase=[empress-flight-learned]
TICK tick=1 phase=[empress-flight-initial-reposition]      <-- the copy is gone
```

`RET` proving the network produced `-1,1` and the very next line reading the
*scripted* phase is the whole bug in two adjacent lines.

The earlier checks could not catch this:

* "zero policy ≡ fixed machine" is **vacuously** true if the policy is ignored —
  it was never evidence that the wiring worked.
* The offline harness tested `Adjust` in isolation and with value copies. It
  proved `Adjust` computes the right numbers; it never proved those numbers
  reached the plan.
* The one control that did look conclusive (`policy-force-class` changing a
  decision) was an *offline* observation, not an in-engine one.

## The fix

`ref FormulaScriptOutput output` in all five hooks, plus `ref` at all five call
sites: `EmpressFlightScript`, `EmpressWingScript`, `FishronChilletScript`,
`FishronQueenSlimeScript`, `FishronWingScript`. `FormulaScriptController` (the
code path that always worked) already used `ref`, which is why its scripts were
never affected.

## Verification after the fix

| run | ticks | hits | bossLife | damage | shots |
|---|---|---|---|---|---|
| fixed machine (no policy) | 1876 | 1 | 75115 | 22885 | 23 |
| zero-weight policy | 1876 | 1 | 75115 | 22885 | 23 |
| force-move policy | **727** | 1 | **92828** | **5172** | **9** |

Zero weights still reproduce the fixed machine bit-for-bit, so the fixed state
machine remains the base of the search. A non-zero policy now visibly changes
the fight (`APPLIED-learned` = 487 of 487 ticks).

## What this invalidates

* `artifacts/training/empress-broom-nohit-v1/log.csv` — the differences between
  candidates in that run were **seed noise**, not policy effect. It is not
  evidence about the search.
* The "random policy is a no-op" anomaly, previously explained away as the
  network happening to return class 0 at the probed state. The real explanation
  is that *every* policy was a no-op, always.

## Standing lesson

A residual hook needs an in-engine control that **cannot** be a no-op, and the
check must observe the value **after** it crosses the call boundary — not the
value inside the callee. Comparing `Adjust`'s return against a locally-built
snapshot is not sufficient, because it never exercises parameter passing.
