# Round 161 handoff

## HEAD
`5852bbd` — pushed. Tests **749 pass / 9 fail**, the long-standing accepted set. Build clean.
Source change: `CHAITE_DASH_TTC` added (default `-1`, verified byte-identical to the reviewed circuit).

## HEADLINE: ttc-keyed dash timing is refuted — it is a LOWER bound, so it only ever postpones the dash, and the strong wing's committed delay is already correct

### What was implemented

`CHAITE_DASH_TTC`: the charge dash additionally waits until closing ticks-to-contact is ≤ the value.
**Only the trigger instant changes** — the dash keeps the escape branch's direction, which is what
distinguishes it from the repeatedly-refuted `CHAITE_COUNTER_DASH` (steers **at** the boss).
`ChargeTicksToContact` was factored out and shared with the counter-dash gate. Default `-1` →
condition always true → reviewed circuit reproduced byte-for-byte (verified: weak 300 = 5839/8/51366).

### A self-correction worth recording

The first sweep (ttc 2,3,4,5,6,8) returned **byte-identical** results, which looked like an inert
gate. It is not inert — **ttc at the lock is already ~28** (boss ~476 px away at ~16 px/tick), so every
value ≤ 8 is non-binding, and `ttc=20` does change the run. **The binding range is ttc ≥ ~15**, and my
first sweep sat entirely on the non-binding side.
**Lesson: compute a gate's binding range before sweeping it**, or the whole sweep looks like "the
parameter does nothing".

### Results: another zero-sum trade, and the strong wing suffers most

**Weak (the failing band)**

| weak DPS | baseline | ttc=12 | ttc=14 | **ttc=20** |
|---|---|---|---|---|
| 300 | 5839/8/51366 | — | — | 6966/10/45776 |
| 600 | 5320/6/30138 | — | — | 6941/7/**13847** |
| 800 | 3875/7/33502 | 4342/5/27223 | 3981/5/32003 | 4602/6/**23799** |

Weak 800's **damage rises 44485 → 54188 (+22%)** and residual falls 33502 → 23799 — the best low-DPS
output yet — but it still does not kill and takes no fewer hits.

**Strong (the control, which should not regress)**

| strong DPS | baseline | ttc=12 | ttc=14 | ttc=20 |
|---|---|---|---|---|
| 300 | **15938/6/0 KILL** | 14325/6/8027 | 14090/6/9154 | 12271/8/19296 DEAD |
| 600 | 7830/4/5096 | **8340/3/0 KILL** | **8340/1/0 KILL** | 5546/5/27914 (damage halved) |
| 800 | 6386/2/0 KILL | — | — | 6386/1/0 KILL |

Strong 300 goes from **kill → no kill**, and strong 600 loses half its damage at ttc=20.

### Why it cannot work as implemented

The gate is a **lower bound** — it only ever *postpones* the dash. The strong wing's committed delay
of 7 is already tuned (round 158), so any postponement damages it. The real requirement is **later
for weak, unchanged for strong**, which needs the ttc as an **UPPER bound** and enabled **per route**.

**This completes a pattern**: dash *timing* now has three refuted parameterisations —
fixed delay shared across wings (round 158), fixed delay constant across DPS (round 158), and
time-to-contact (round 161). Timing alone does not separate the arms.

## Next step

Two options, in priority order:

1. **Height-band strategy** (the higher-value structural direction). Round 159/160 established that
   wing refills require standing in the boss's altitude band and that this is fatal. Every remaining
   weak failure is a fight-length problem in that band. Changing the band relationship — rather than
   the landing timing or the dash timing — is the only untried structural axis. A concrete first
   query: measure the boss's altitude distribution during the *hover* states (0 and 5) versus the
   charge states, to see whether a band exists that hovers cannot reach.
2. **Per-route upper-bound ttc** — cheap (the knob exists), but round 161 suggests the payoff is
   small since weak's fixed delay 4 is already optimal and the ttc is essentially affine in it.

Do **not** re-attempt: shared dash delay, DPS-constant dash delay, ttc lower bound, counter-dash
(steers at boss), pre-emptive landing, apex refill, refill guard > 0, standoff ceiling raise,
weak lead 40 + delay 4, or any single-DPS-point tuning.

## Status

| arm | coarse-grid failures |
|---|---|
| strong | **0** (600/700/1100 short by 5–15k) |
| weak | **4**, all at DPS ≤ 800, plus near misses at 1100/1400/1750 |

Strict zero-hit target: strong achieves it at **1200, 1300, 2000**. Objective still **not met**.

## Method notes

* **Compute a gate's binding range before sweeping it.** Round 161's first sweep was entirely
  non-binding and produced six identical results that could easily have been read as "inert".
* An identical-result sweep is *not* automatically the broken-invariant signature — check whether the
  parameter's precondition is satisfiable in the run before suspecting the environment.
* `git diff --stat <round-start> -- src/` is the fastest proof that a change really compiled in.
