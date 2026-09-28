# Round 160 handoff

## HEAD
`506a843` — pushed. **Source byte-identical to the round start** (`git diff 6afb8f1 HEAD -- src/`
is empty): the round's one code change was measured, refuted, and removed.
Tests **749 pass / 9 fail**, the long-standing accepted set. Build clean.

## HEADLINE: the pre-emptive landing was implemented, it WORKS mechanically, and it is still a net loss — which converts round 159's correlation into causation

### The unmeasured quantity that motivated it

The weak wing's descent is broken:

| arm | descending frames | **median vy** | max vy |
|---|---|---|---|
| weak 600 | 54 | **2.7** | 6.4 |
| strong 600 | 99 | **9.5** | 10.0 |

Weak drifts down at 2.7 px/tick against a terminal ~10, so it never builds the momentum to reach a
surface — which is *why* it logged 0 landings in round 159. Note `cruise-descend` (L2843) already
issues `vertical = 1`; the failure is that the descent never becomes a fall.

### What was implemented

`CHAITE_WINGTIME_FLOOR`: below the floor, with the player airborne, the vertical axis is **committed
to a descent** instead of being re-decided from the charge geometry each tick. Placed deliberately
**after** `ApplyArena`, because ApplyArena's floor rule (L3518, "stop the descent command 90 px above
the ground") is precisely what makes the ground unreachable.

**Inertness verified first**: with the floor unset, weak 300/600/800 reproduce their recorded
results exactly (5839/8/51366, 5320/6/30138, 3875/7/33502).

### The mechanism works (dense, weak 600, floor 40)

| metric | floor 0 | floor 40 |
|---|---|---|
| landings (refills) | **0** | **2** (t=3690, 3967 — both a full 130) |
| median descent vy | 2.7 | **6.5** |
| descending frames | 54 | 175 |

### But the outcome is uniformly worse, and monotonic in the floor

| weak DPS | floor 0 | floor 40 |
|---|---|---|
| 300 | 5839/8/51366 | 4402/8/58584 (dies far sooner) |
| 600 | 5320/6/30138 | 4402/8/39279 |
| 800 | 3875/7/33502 | 5393/7/13177 (more damage, still dies) |
| 1100 | 4554/5/4344 fail | **4788/5/0 KILL** |
| 1400 | 3359/4/12176 | 3395/5/11375 |

| strong DPS | floor 0 | floor 40 |
|---|---|---|
| 600 | 7830/4/5096 | 7861/6/4766 (2 more hits) |
| 1100 | 4409/4/7087 fail | 4066/3/13376 (fewer hits, far more HP) |

Floor sweep at weak 600 — higher floor is strictly worse, surviving ticks collapsing 5320 → 1379:

| floor | 0 | 40 | 60 | 80 | 100 | 120 |
|---|---|---|---|---|---|---|
| hits | 6 | 8 | 9 | 6 | 5 | 5 |
| boss HP left | 30138 | 39279 | 28339 | 45645 | 55163 | 69557 |

### Why this matters more than another failed knob

Round 159 could only show that the stretches approaching a platform plane *ended at* contacts —
correlation. This round **made the landing happen** and the fight immediately got worse, so the
relationship is now **causation**: refilling requires standing in the boss's altitude band, and that
cost exceeds the benefit of the refill. That closes the "just land more often" family for good.

The knob was therefore **removed** rather than shipped. The finding is kept in prose (§159 of
`docs/fishron-native-truth.md`) with a note that it may be re-implemented only if the altitude-band
strategy itself changes, never on its own.

## The concrete next lead (§159.7) — distinct from the already-refuted counter-dash

Every one of the six weak contacts happens **10–48 ticks after the last dash**, with **no dash
requested in the final 6 ticks**:

| contact | last dash | gap |
|---|---|---|
| 1367 | 1353 | 14 |
| 3317 | 3307 | 10 |
| 3463 | 3423 | 40 |
| 3860 | 3843 | 17 |
| 4592 | 4556 | 36 |
| 4958 | 4910 | 48 |

And dash spacing is a **hard 58 ticks** (34 of 52 gaps are exactly 58) — the *same* as the charge
period. So each charge gets exactly one dash, and a dash that fires early (1367 by 14, 3317 by 10)
leaves the cooldown active when contact arrives.

**Next test**: a **time-to-contact gate for the trigger instant only**, leaving the escape direction
unchanged. This is explicitly *not* `CHAITE_COUNTER_DASH`, which steers **at** the boss and has been
refuted repeatedly. Sweep `CHAITE_DASH_TTC = 2,3,4,5,6,8` on the weak arm; require that weak
300/600/800 lose contacts **and** that all 15 strong points hold. If strong regresses, ttc is
insufficient and the lead is refuted too.

## Method notes

* **Verify inertness before interpreting a sweep.** Running the default first (floor unset) is what
  proved the new default path was byte-identical, so the floor sweep's numbers were attributable.
* **A working mechanism is not a working fix.** Landings 0 → 2 and vy 2.7 → 6.5 were both real and
  both irrelevant to the outcome. Measure the objective, not the mechanism.
* Saturation can look like a broken invariant: at floor 40, weak 300 and weak 600 return
  byte-identical results (4402 ticks, 8 hits, 7 npc contacts). That is **deterministic saturation**
  (once the vertical axis is locked, output stops depending on DPS), not environment contamination —
  the broken-invariant signature does **not** apply here.
* Analysis scripts added this round, reusable: `tmp/contactprofile.py`, `tmp/refillwindow.py`,
  `tmp/wingdrain.py`, `tmp/windowclimb.py`, `tmp/emptyclimb.py`, `tmp/ballistic.py`.
