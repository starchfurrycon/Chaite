# Round 189 handoff

## HEAD / state
**No source change — the experiment was implemented, measured, refuted, and reverted.**
`src/` verified identical to the round-180 baseline **by `git diff` and by grepping the removed identifiers.**
Build clean; tests **749 pass / 9 fail**. Verified binary re-confirmed: `960A03BFF6050CF7…` ✅
Shipped config unchanged: ceiling 4400, dash delay 4, left start.

---

## 1. What was tried, and the methodology that caught it

Round 189 had identified the **descent floor** as the only remaining lever of the §170.1-correct shape
(supplying a missing pre-condition rather than rewriting a quantity). I implemented it as
`CHAITE_WEAK_DESCENT_FLOOR` (**default 0 = disabled**, so the shipped circuit stays byte-identical) plus
`ApplyWeakDescentFloor`, gated on: fairy-wing route, not a charge escape, not on ground, **`vertical > 0`**,
`WingTime > 0`, `Center.Y > floor`.

**★ First measurement: three floor values × three DPS, all byte-identical to the rule being OFF.**

| dps | floor 0 | floor 6800 | floor 5600 |
|---|---|---|---|
| 900 | 4736 / 5h / 15043 | 4736 / 5h / 15043 | 4736 / 5h / 15043 |
| 700 | 5616 / 6h / 18774 | 5616 / 6h / 18774 | 5616 / 6h / 18774 |
| 400 | 8577 / 6h / 24377 | 8577 / 6h / 24377 | 8577 / 6h / 24377 |

**Prove the rule is inert BEFORE tuning parameters** — otherwise "the parameter doesn't matter" is misread
as "the direction is wrong". That is the reusable lesson of this round.

## 2. ★★ The dense trace gives the real reason

`dps 700`, per-tick:

```
t=381  y=6655  vy=-9.91  wing=3
t=382  y=6645  vy=-9.91  wing=2
t=383  y=6635  vy=-9.91  wing=1
t=384  y=6625  vy=-9.91  wing=0     <-- budget exhausted
t=385  y=6615  vy=-9.52  wing=0
...
t=408  y=6507  vy=-0.32  wing=0     <-- APEX (vy crosses zero)
t=409  y=6507  vy=+0.08  wing=0
```

Two facts:

1. **The entire descent happens with `wingTime == 0`.** The budget dies at t=384; the apex is not reached
   until t=409. **The fall is performed by gravity with no budget at all.**
2. **The apex is at y ≈ 6507 and the budget is already zero there**, so the native apex refill cannot fire at
   the true apex.

## 3. ★★★ Why the lever is physically impossible, not merely mis-tuned

**Raising the wings to arrest a fall requires budget — and it is precisely while falling that there is none.
The wings that would stop the fall are the wings the fall has already spent.**

Widening the gate to `vertical >= 0` (to cover the *uncommanded* gravity descent) changed **nothing**, for
the same reason: budget is 0 throughout.

**So §189's "only candidate of the correct shape" is refuted, and the refutation is not a tuning question
but a conservation relation.** The **vertical family — apex, floor, ceiling, refill — is now completely
exhausted with no net gain.**

## 4. Number correction

**The weak wing's climbing apex is y ≈ 6507 (≈407 tiles), NOT y ≈ 6032.**
§183.1's "refill every 328 ticks at y ≈ 6032" is a **different, lower event**. §189.2's wording is corrected:
y 6032 is *where the measured budget restoration occurs*; the dense trace shows the apex is ~475 px higher
and has zero budget, so the restoration point is genuinely below the apex — but **its trigger has still not
been directly observed.**

## 5. What is left (both non-vertical)

1. **Horizontal shaping** (§183, §187.4). The tornado spawn *height* is fixed, but its **horizontal** position
   follows the Boss's hover, which follows the player. §187.2 measured spawn \|dx\| p50 = **189** in a failing
   run vs **345–639** in others — the horizontal coordinate is not pinned.
2. **Characterise the 1-hit trajectory** (shroomite, dps 1200) — the closest known approach to the strict
   `hits == 0` goal (§190.4). Determine whether its enabling condition is controllable.

## Acceptance status (unchanged this round)

| tier | weak (fairy) | strong (fishron) |
|---|---|---|
| **obsidian** (honest) | **11 / 27 (41 %)** | **28 / 28 (100 %)** |
| **shroomite** | **17 / 33 (52 %)** | 6-point sample, all killed |

**Strict `hits == 0`: NOT met — neither tier, neither wing.** Closest: shroomite dps 1200 at **1 hit**.
**No run is described as no-hit.**

## Do NOT re-attempt

**The whole vertical family:** apex refill (+1/−5), altitude floor (−3), ceiling off (−5),
**descent floor (byte-identical, and impossible — §191.5)**, "descend earlier to raise the apex" (unreachable),
dash timing 0/1/2/3/4/7/10/14 (closed at both ends), dash timing × ceiling (anti-composes),
start side right (−1), holding horizontal speed after a dash, reversing the dash direction,
armour tier as a *survival* lever (it is +1 for kills but −0.5 mean hits), global horizontal strategy,
phase-3 teleport for weak (never reached).

**Corrected claims now on record:** "shroomite is strictly worse" is **FALSE** (net **+1**);
"the weak refill point is y 6032 and is an apex" is **FALSE** (apex y 6507, budget 0 there);
"damage records are deferred 10 ticks" is **FALSE** (none).

## Method notes

* **Always A/B a new rule against its own disabled state and check for byte-identical output before tuning.**
* Long sweeps: **hash-check `Terraria.exe` first** (§187.0) — it was externally patched once this session.
* Judge by **net kills**, never by hit count. `hostileProjectiles` uses **flat `x/y/vx/vy`**.
* `phase` strings are **not exported** to the probe trace — a rule's firing cannot be confirmed from
  artifacts alone; use output differences or the dense per-tick fields (`wingTime`, `velocity.y`, `position`).
* Round-189 scripts: `tmp/tab188.py`, `tmp/tornado186.py`; datasets `kU-w*-f*`, `kT-w*`.
