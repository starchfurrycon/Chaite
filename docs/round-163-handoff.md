# Round 163 handoff

## HEAD
`af24c98` — pushed. Tests **749 pass / 9 fail** (long-standing accepted set). Build clean.

Objective **still NOT met**: neither wing satisfies the owner's full-range criterion.

## HEADLINE: the movement is DPS-independent; the outcome forks at the phase transition, and the fork never reconverges

### 1. Full-range acceptance sweep (obsidian — the honest yardstick)

| DPS | strong (fishron wings) | weak (fairy wings) |
|---|---|---|
| 300 | 15938 / 6 / **kill** | 5839 / 8 / died |
| 400 | 12240 / 5 / **kill** | 5839 / 8 / died |
| 500 | 7304 / 5 / died | 6418 / 7 / died |
| 600 | 7830 / 4 / died | 5320 / 6 / died |
| 700 | 5965 / 4 / died | 5022 / 6 / died |
| 800 | 6386 / 2 / **kill** | 3875 / 7 / died |
| 900 | 5741 / 1 / **kill** | 5726 / 5 / **kill** |
| 1000 | 5221 / 3 / **kill** | 5210 / 5 / **kill** |
| 1100 | 4409 / 4 / died | 4554 / 5 / died |
| 1200 | 4441 / **0** / **kill** ★ | 4432 / 2 / **kill** |
| 1400 | 3884 / 1 / **kill** | 3359 / 4 / died |
| 1600 | 3466 / 1 / **kill** | 3219 / 4 / died |
| 1750 | 3215 / 2 / **kill** | 3153 / 5 / died |
| 2000 | 2881 / **0** / **kill** ★ | 2880 / 2 / **kill** |
| | **10 / 14** | **4 / 14** |

★ = **native-measured `hits == 0` with a kill** — the round's only achievement of the
higher-priority strict objective.

Fine sweep (25–50 DPS steps) confirms the boundary is **non-monotone**: 450 kills, 475 dies,
525/550 die, 575 kills, 625 dies, 650 kills, 1125 dies, 1150 kills.

### 2. Hits are bimodal, and regeneration is the discriminator

20 of 28 strong points take **≤3 contacts**; 8 take 4–6. Movement is generally clean; failures
cluster at DPS values that fall into a bad phase.

| run | hits | total damage | end life |
|---|---|---|---|
| strong 300 | 6 | **884** | 44 — **survived** |
| strong 500 | 5 | 559 | 0 — died |
| weak 300 | 8 | 711 | 0 — died |

Strong 300 absorbed **884 damage against a 480 HP pool** thanks to regeneration over 15938 ticks.
So *total damage does not decide survival*; contact clustering and fight length do. This is why the
long low-DPS fights at 300/400 survive while mid-DPS 500–700 die.

### 3. ★ Trajectories are frame-identical across DPS until the phase threshold

Raw per-tick comparison of the zero-hit 1200 run against the dying 500 run:

```
tick   s1200 (px, py, bossLife, state)     s500 (px, py, bossLife, state)
1590     2691.3  4501.3   50980  1          2691.3  4501.3   66742  1
1592     2713.7  4521.3   50940  1          2713.7  4521.3   66725  1
1596     2768.6  4561.4   50860  1          2768.6  4561.4   66692  1
```

Identical to one decimal place; only `bossLife` differs. The first 14 charge times and the player's
x at the first 12 locks are also identical across all DPS. **DPS enters only through the health
number and has no direct effect on movement.**

### 4. ★ The fork is the phase transition, and it is permanent

Measuring every run's positional gap against the zero-hit 1200 baseline:

| | fork tick | gap@fork | gap@+200 | gap@+500 | gap@end |
|---|---|---|---|---|---|
| s450, s500, s575, s600, s650, s675, s700, s725, s750, s1075 | **2238** | **1.2 px** | 317.1 | 541.6 | 246 … 3009 |
| s1150 | 2238 | 1.2 | 151.2 | 432.5 | 1453.3 |

**All fork at exactly tick 2238, and the gap grows monotonically — it never reconverges.**
Against a 5120 px arena, end gaps of 1500–3000 px mean the runs are in opposite halves.

Read directly at the fork tick:

```
s1200  t=2238  boss life=38020  ai=[4, 0, 2, 8]     <- below 39000, transitioned
s500   t=2238  boss life=61342  ai=[1, 0, 2, 8]     <- still phase one
```

Same player position, same `ai[1..3]`, **only `ai[0]` differs (4 vs 1)**. The player stands in the
same spot and is funnelled into a different trajectory solely because the boss selected a
different attack after crossing half health.

*(Caveat recorded honestly: `ai[0]` is confirmed to be the state machine, but I have not verified
that 4 specifically denotes "phase two" rather than one of several attacks. The load-bearing fact is
only that `ai[0]` differs while everything else matches.)*

### 5. ★ A testable pattern

Every **accepting** run crossed half health **early** (2000→1409, 1200→2189, 1150→2274,
1075→2416); every **dying** run crossed **late** (700→3582, 600→4139, 500→4919).

Mechanism: the later the crossing, the more charges are flown inside the new, divergent trajectory.

### 6. Degree-of-freedom analysis (why there is no tidy fix)

The script is deterministic and has **no phase awareness at all** (`grep` confirms zero matches for
any phase/life concept). The harness applies DPS **unconditionally**, so the transition instant is
fully determined by DPS and the script cannot move it. Therefore, for a fixed route, the accepting
DPS set is **discrete and alternating**. Turning it continuous requires changing where the fork
*leads*, not when it happens — and the script has exactly one free variable (movement) against a
one-parameter family of transition phases.

**Two candidate directions were evaluated:**

* **(A) "Reconverge after the fork" — REFUTED** by §4: diverged runs never reconverge.
* **(B) Recognise the transition and take a dual-safe stance — the only remaining route.**
  The script does not know the boss's health, but it **does see `ai[0]` and the timer**, so it can
  detect the transition as it happens and occupy a stance safe against both attack selections across
  a window either side of the crossing.

### 7. Defense does not fix anything — it reallocates failure

Shroomite (high defense) on the obsidian failures: strong 600 and 1100 convert to kills, but strong
500 goes 5→8 hits, strong 700 becomes a 6-hit death at 1890 remaining, and **weak 300 gets much
worse (8→13 hits, 5839→11125 ticks)**. Lower damage per hit means a longer fight and more contacts.

## Next step

Test whether the crossing instant is **causal** rather than correlated — the sharpest open question.
Then, if causal, implement a transition-aware stance in the script keyed on `ai[0]` changing.

## Do NOT re-attempt (independently refuted)

Dash delay (shared / DPS-constant / ttc lower bound), dash direction (`CHAITE_PERP_DASH` and the
perpendicular idea), dash i-frames, height-band escape, pre-emptive landing, apex refill, standoff
ceiling, `CHAITE_CASCADE_*`, tornado knobs, bubble-break, "reconverge after the fork", and using
defense tier as a survival lever.

## Code state

| item | default | note |
|---|---|---|
| `CHAITE_PERP_DASH` | -1 | inert, verified byte-identical twice |
| shield-event boss/player pose | always on | observation only; baseline unchanged |
| `tmp/lockscan.py`, `tmp/divergence.py`, `tmp/reconverge.py` | — | reusable analysis |
