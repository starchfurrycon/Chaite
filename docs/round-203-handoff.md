# Round 203 handoff — cornering and clustering refuted; the failure is exactly three hits

## HEAD / state
**No source change.** `src/` identical to the round-180 baseline and clean. Build clean.
Tests **749 pass / 9 fail**. Binary verified `960A03BFF6050CF7…` ✅

New scripts `tmp/wall203.py`, `tmp/cluster203.py`, `tmp/sep203.py`.
New data: `kL4-w700`, `kL4-w800`, `kL4-s600` (all at the 24 000-tick ceiling).

---

## 1. Hypothesis 1 — "cornered against the arena wall" — **REFUTED**

Player x at every hit, in tiles from the nearer wall (320-tile arena):

| hit type | n | min | p25 | median | p75 | max |
|---|---|---|---|---|---|---|
| 370 (boss body) | 39 | **50.1** | 58.9 | 67.2 | 92.2 | 157.5 |
| 384 (still bubble) | 41 | **48.8** | 124.0 | 136.0 | 136.0 | 147.6 |
| 386 (tornado) | 66 | **51.7** | 58.6 | 77.2 | 99.7 | 151.2 |

**Hits within 40 tiles of a wall: 370 = 0/39, 384 = 0/41, 386 = 0/66 — all zero.**

**⇒ Hits never happen at the arena edge (medians 67–77 tiles from a wall). Turning earlier to escape a wall
has no value.**

## 2. Hypothesis 2 — "hits too closely spaced (combo)" — **REFUTED**

Minimum hit-to-hit interval, both classes: **minimum 40 in each**, and the survivors include runs at 40
(kA3-s300, kA3-s650, kF3-s650, kI4-s850) while the deaths include runs at **870** and **927**.
**Whether hits are clustered does not decide the outcome.**

## 3. ★★★ The structure that *is* universal: the last hit falls at 93–97 % of the fight

| run | last hit tick | fight length | % in | boss left |
|---|---|---|---|---|
| kJ4-s1100 | 4098 | 4409 | **93 %** | 7087 |
| kI4-s700 | 5608 | 5965 | **94 %** | 14721 |
| kF3-s550 | 6795 | 7185 | **95 %** | 17079 |
| kE3-s500 | 6927 | 7304 | **95 %** | 21642 |
| kA3-s550 | 7161 | 7531 | **95 %** | 13926 |
| kF3-s500 | 7765 | 8116 | **96 %** | 14857 |
| kF3-s400 | 9231 | 9648 | **96 %** | 17260 |
| kF3-s350 | 9558 | 9907 | **96 %** | 23339 |
| kF3-s300 | 11218 | 11589 | **97 %** | 22705 |

**All ten deaths land in a 4-point relative band, whether the fight is 4409 or 11589 ticks long.**

**⇒ Death is not about the fight lasting too long (that ratio would drift with length) — it is about crossing
the 480-HP line.**

## 4. ★★ The quantitative gap, in hits

| run | total damage taken | margin vs 480 | outcome |
|---|---|---|---|
| kJ4-s1600 | **150** | +330 | KILL |
| kI4-s800 | 313 | +167 | KILL |
| kA3-s400 | 399 | +81 | KILL |
| kJ4-s1100 | **596** | **−116** | DIED |
| kA3-s550 | 641 | −161 | DIED |
| kI4-s700 | **682** | **−202** | DIED |

**⇒ The best survivor took 150; the closest death took 596. The gap is "two or three hits fewer."**

**★★★ This is the project's most precise failure statement: at the obsidian tier (480 HP) a failing run is
only 2–3 hits over the line, and each hit is a *single attack event* worth 140–190 damage that can only be
removed by **avoiding it entirely** — it cannot be amortised away.**

## 5. ★ Harness ceiling: `-maxticks` ∈ [600, **24000**]

`-MaxTicks 30000` is rejected (`-maxticks must be an integer in 600..24000.`).

**Pure arithmetic consequence:** the weak wing at dps 500/600/700 needs 78000/dps =
**156 / 130 / 111 s = 9360 / 7800 / 6686 ticks** — all well inside 24 000, so **time was never the weak
wing's problem.** Measured directly: at the 24 000 ceiling, weak dps 700 dies at **5616** ticks with the boss
at 18774.

**⇒ Extending the horizon cannot help the weak wing's low band: the cause is the damage *rate*, not the
remaining time.**

## 6. Bonus measurements from this round

* `kL4-w800` — weak + obsidian at the 24 000 ceiling: **6384 ticks, 2 hits, KILL** (one of the best weak-wing
  results recorded).
* `kL4-s600` — strong + obsidian at 24 000: identical to the 20 000 result (**7830 ticks, died**), confirming
  the horizon is not the constraint there either.

## 7. Acceptance master table (native tick-replay only)

| loadout | `hits==0` @6000 cap | survive-and-kill @20000 |
|---|---|---|
| **strong + obsidian** | **4** (1175, 1200, 1300, 2000) | **15/20 = 75 %**; holes `{500,550,600,700,1100}` |
| strong + shroomite | **2** (1200, 1600) | **12/12 @6000 (900–2000)** |
| weak + obsidian | **0** | **9/18 = 50 %**; 300–700 all die; best 2 hits |
| weak + shroomite | **0** | **9/18 = 50 %**; 300–700 all die; best 1 hit |

* **Strict goal: ACHIEVED on the strong wing at 6 points across both tiers; ZERO on the weak wing.**
* **Relaxed goal: not yet achieved;** strong wing 15/20, weak wing 9/18.
* `SuccessAfterDeath` is never a kill. Nothing is called no-hit without the probe's
  `ACCEPTED: zero hits in the native engine.` line.

## 8. What is left, ranked

1. **Strong wing's five holes** (`500/550/600/700/1100`) — each is a 2–3-hit overshoot; bounded and measured.
2. **Weak wing's low band** (300–700, ten failures) — cannot be fixed by time (§207.5); needs either a
   genuinely lower contact rate or the recognition that 480 HP is insufficient there.
3. **Weak wing `hits==0`** — never achieved (best 1 hit; 2 hits at the 24 000 ceiling).

## Do NOT re-attempt

**Sourcing the W-cycle beat from the native sequence `ai[3]` — net −3, reverted (§203).**
**The whole vertical family** — apex refill, altitude floor, ceiling off, **descent floor (byte-identical and
physically impossible)**; dash timing 0/1/2/3/4/7/10/14; dash timing × ceiling; start side right (−1); holding
horizontal speed after a dash; reversing the dash direction; global horizontal strategy; phase-3 teleport for
weak. Type-384 bubble = negligible (60–82 dmg). DPS-conditional standoff gate = byte-identical A/B.
`IsReachableState` — legal for state 4. Contact-rate threshold as a criterion (ranges overlap).
**Altitude as the explanation for low contact rates (disproved, §206.4).**
**NEW: cornering/arena walls as the cause of hits — 0 % of hits are within 40 tiles of a wall (§207.1).**
**NEW: hit clustering / combo spacing — survivors and deaths both span 40 to 900+ ticks (§207.2).**
**NEW: extending the horizon (`-maxticks` caps at 24 000; the failures are damage-rate limited, §207.5).**

**Corrections on record:** "shroomite strictly worse" **FALSE**; "refill point y 6032 is an apex" **FALSE**;
"damage records deferred" **FALSE**; "survival time rises with DPS" **FALSE**; "contact rate is a random
variable" **FALSE (deterministic)**; "player life pool is 600" **FALSE — 480**; "the fork is invisible in
observables" **FALSE**; "`ai[3]` ≡ accumulated dash count" **FALSE**; "the strong wing has only two holes"
**FALSE — five**; "the contact-rate floor is 0.313" **FALSE — 0.174**; "hits cluster near the arena walls"
**FALSE**; "`-maxticks` can be raised past 24000" **FALSE**.

## Method notes

* **`npcs[i].ai` is an array in the trace** — parse it (`ai[0]` state, `ai[2]` timer, `ai[3]` sequence).
* **Boss AI reads its own `life` every frame** (§200) and re-phases its cadence from it.
* **Harness is deterministic** — one run per configuration suffices.
* **`-maxticks` must be in 600..24000.**
* **Env-gate any behaviour change and verify the default path is byte-identical before reading an A/B.**
* **Sorting a tuple with a bool first mislabels groups** — `(dps, tag, ..., kill)` sorts `True` *before*
  `False`; sort by the explicit key instead.
* **`dps ≤ 780` cannot kill inside 6000 ticks** (78000/100 s) — a timeout there is arithmetic.
* Tick-horizon results are **not** comparable across `-MaxTicks`. `-WallSeconds` caps at **900**.
* `SuccessAfterDeath` is not a kill. Read `actualReturn`, not `request.damage`.
* **Do not `git add -A tmp`** — `tmp/video/*.mp4` (up to 175 MB) exceeds GitHub's limit; it is ignored.
* **Hash-check `Terraria.exe` before long sweeps** (§187.0).
