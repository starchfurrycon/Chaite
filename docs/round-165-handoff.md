# Round 165 handoff

## HEAD
`e84382b` — pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.

Objective **still NOT met**: strong 10/14, weak 4/14 on the obsidian full-range sweep.

## HEADLINE: the weak wing's handicap is a **shallow climb ramp at charge start**, not positioning, not meter, not refill cadence

### 1. Per-charge exposure, measured

| run | charges | reached `\|dy\|>150` | band entries/charge | **in-band frames/charge** |
|---|---|---|---|---|
| weak 600 | 47 | 39 (**83%**) | 0.62 | **9.0** |
| strong 1200 | 24 | **24 (100%)** | 0.29 | **3.8** |
| strong 500 | 56 | **56 (100%)** | 0.23 | **2.4** |

### 2. ★ Two of my own earlier explanations are refuted by this table

**(a) "The weak wing starts charges closer to the boss" — WRONG.**
Pre-lock positions are nearly identical: share already clear of the 92 px band *before* the lock is
**weak 89% (42/47), strong 88% (21/24), strong 93% (52/56)**. The weak wing's 9.0 in-band frames are
generated **during** the charge, not inherited from the lock.

**(b) "The weak wing runs out of wing meter" — NOT the operative cause.**
Lock-time `wingTime` medians: weak **32**, strong 1200 **26**, strong 500 **4**. The weak wing is not
the lowest. And it *does* reach `vy = −9.9` on several charges at wingTime **110, 62, 14 and 30** —
including 62 and 14. So it is physically capable of the fast climb; the route just doesn't ask for it
at the right time.

### 3. The actual cause: climb starts on a ramp

Measured `vy` sequences within charges:

* **weak**: first frame **−3.7**, ramping slowly to −6.4 (or −4.9 → −7.6); reaches −9.9 in only a few
  charges.
* **strong**: first frame **−9.1 → −12.5**, or holds **−16.3** for an entire charge.

Crossing the 184 px contact band at −4 to −6 consumes the entire 28-tick charge — which is exactly the
32% charge-frame exposure measured last round. The strong wing crosses it in ~18 ticks and keeps
margin.

**The zero-hit strong 1200 run shows the trick**: it is often **descending at +10** immediately before
the lock (9 of 24 charges), then converts that into an immediate high-speed climb. It enters the charge
already in motion rather than starting from rest.

### 4. Why the earlier refill frame was mis-aimed

Last round concluded the fix was "refill the meter before every charge (apex cadence 252 → 58 ticks)".
The lock-time meter medians above (32 vs 26 vs 4) show that quantity does not separate good from bad
runs, so the refill cadence is **not** the operative variable. It was a plausible mechanism that this
round's data does not support, and it is recorded as superseded.

## Next step — precise, two-part, quantified

**The weak wing must have a high climb speed on the FIRST frame of each charge, not a ramp.**

Either climb before the lock, or be descending at the lock so it converts immediately (as strong 1200
does). This is a **phase-alignment** problem: the ascent/descent cycle must be in phase with the
58-tick charge period so that the lock lands on "descent end" or "full-speed climb".

**Acceptance criteria:**

1. weak first-frame charge `vy` median: **−4.40 → ≤−9**
2. share of charges reaching `|dy| > 150`: **83% → 100%**
3. in-band frames per charge: **9.0 → ≤3.8**
4. charge-frame LEVEL share: **32% → ≤13%**
5. weak low-DPS (300–800) total contacts: **≤3**
6. weak obsidian acceptance: **4/14 → 14/14**

**Do not** pursue: refill-cadence changes (superseded above), forced-descent refill
(`_refillGuardBudget` > 0 — it lowers `mean|vy|` during charges, directly against this round's
finding), dash direction/timing/ttc, perpendicular dash, i-frames, height-band escape, standoff
ceiling, `CHAITE_CASCADE_*`, tornado knobs, bubble-break, "reconverge after the fork", or defense tier
as a survival lever.

## Standing facts (unchanged, still load-bearing)

* **Survival = total contacts ≤3** (20/20 kill below, 0/8 above). 20 of 28 strong points already pass.
* Contact needs `|dx| < 95` **and** `|dy| < 92`.
* **The fork is the phase transition** at a fixed tick (2238 for the s1200-family), observed directly
  as an `ai[0]` difference with identical position, and it **never reconverges**.
* Strong 1200 and strong 2000 are **native-measured zero-hit kills**.
* `wingTime` **does** refill airborne at the apex (`vy == 0 && releaseJump`, or `autoJump &&
  justJumped`) — round 159's "never increases airborne" was wrong.

## Code state

No source changes this round (analysis only). `CHAITE_PERP_DASH` still default -1 and inert.
Reusable scripts: `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py`.
