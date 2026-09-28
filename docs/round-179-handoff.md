# Round 179 handoff — ★★★ BREAKTHROUGH

## HEAD
`src/Chaite.Core/FishronWingScript.cs` carries the **new winning rule** (this is the one change that has
ever produced a large net gain). Build clean. Tests **749 pass / 9 fail**.

---

## The causal chain that produced it

1. **Round 177** (previous round) refuted a weak-wing *altitude floor* at **net −3** and recorded the lesson:
   *judge levers by net change in kills, never by a fall in hits.*
2. Three consecutive rounds of lever-refutations (174: +1/−5, 175: 0/−1, 177: −3) showed the whole
   "retime the vertical/budget" family was exhausted. So instead of a fourth, round 179 acted on the owner's
   standing guidance: *"如果你始终无法实现验收就看视频里的打法…我的描述是定性而非绝对准确的"*.
3. **Recovered the real trajectory from the video.** `tmp/ocr-series.txt` (a pre-existing 1 Hz OCR pass of
   `tmp/bili/D-nohit.mp4`) ends each line with a HUD position readout, e.g. `3686浠ヨタ | 218鐨勫湴琛?`
   (mojibake for `以西` / `的地表`). `tmp/hud177.py` parses it; 218/232 samples give a usable y.
4. **The video's player holds y 218..275 tiles — a band only 57 tiles tall.**
   Our engine's weak-wing player oscillates over **315..497 tiles**, which is almost exactly the
   **Boss's own band (256..500 tiles)**.
   Frame confirmed: `GameProbe.cs:2521-2522` sets `Game.worldSurface = 500`, `rockLayer = 750`, and
   `arena.groundTop = 500`; the depth readout is `position.Y / 16f`, so **both y values are directly comparable.**
5. **Therefore the circuit and the Boss were chasing each other inside the same altitude band** — which is
   exactly why the tornadoes (spawned at the Boss centre by AI_069) kept landing on the player. Round 176 had
   already measured this symptom without knowing the cause: the weak wing's tornado band is y 6285..7973 and
   the player's median was **6718, i.e. inside it**.
6. **Round 177's floor lifted the player only to 383 tiles — still inside the Boss band — and lost 3 kills.**
   The video says to go **above** the band. That is a structural difference, not a parameter tweak.

## The rule

`CHAITE_WEAK_ALTITUDE_CEILING=<y>` (default **0 = off**, byte-identical when unset) →
`ApplyWeakAltitudeCeiling` in `FishronWingScript.cs`, called just before the output is written.
While the player is **deeper than the ceiling** and **has wingTime** and is **not on the ground** and is
**not already climbing** and is **not in the locked-charge escape**, it turns that frame's `vertical` into
a climb (`-1`), phase `fishron-wing-weak-altitude-ceiling`.

It deliberately **never touches the locked-charge escape** (§175 proved rewriting the escape forfeits the
dash and climb that make the escape work), and it never fires with an empty bar (that state belongs to the
refill rule).

**Inertness verified**: with the variable unset, weak 300 reproduces `5839/8/51366` exactly.

## ★★★ Results at `ceiling = 4400` (275 tiles), obsidian

### STRONG WING — perfect sweep

| | baseline | ceiling 4400 |
|---|---|---|
| kills | **3 / 28** | **28 / 28** |
| net | — | **+25 gained / −0 lost** |

Every single baseline failure now kills. Highlights: s300 KILL 6 hits (baseline DIED) over **15938 ticks**;
s900 **1 hit**; s1150 **1 hit**; s1400 1 hit; s1600 1 hit.

**★ ZERO-HIT KILLS (strict goal, hits == 0): s1175, s1200, s1300, s2000.**
Baseline had zero-hit kills only at strong 1200 and 2000 — this more than doubles that set.

### WEAK WING — net positive

| | baseline | ceiling 4400 |
|---|---|---|
| kills | **10 / 28** | **12 / 28** |
| net | — | **+5 gained / −3 lost** |

Gained: 625, 650, 800, 950, 1050. Lost: 900, 1300, and 1200 (boss died but so did the player).
Near-kills: 550 at 8548 life, 600 at 19928, 700 at 18774, 750 at 23568, 850 at 16032.

**Weak hits collapse**: w800 **7 → 2**, w950 → **2**, w1050 → **4**, w1600 → **1**.

## ★ Why this is the first genuine large win since §129/§132

Every previous lever **retimed** the vertical/budget conversion and was zero-sum-to-negative.
This one **changes which altitude band the player occupies**, and the change is derived from measurement
of an actual no-hit run rather than from a knock on the script. It matches §170.1's screening rule —
it *permits* an existing command (a climb that was already available) by supplying a **missing
pre-condition** (the altitide reference), which is the only shape that has ever netted a gain.

## Not yet done (next round)

1. **Parameter sweep** — 4400 is untuned. Sweep **4000 / 4200 / 4400 / 4600 / 4800** on both wings.
2. **Shroomite tier** (high defence) not yet re-run with the ceiling; the honest yardstick is obsidian, but
   the goal asks for both.
3. **Strict target**: weak is still not zero-hit anywhere; strong has 4 zero-hit points. Push the weak wing
   and the mid-band points.
4. **Intermediate DPS points** not yet covered (350/425/675/725/1000-1150 fine points on weak; 350/425 on strong).
5. **Phase-3 teleport** — the top comment (618 likes) on the sibling video states the phase-3 pattern:
   the Duke teleports to the **opposite side from its previous teleport** to keep the player centred, then
   dashes 1, then 2, then 3 times. The owner's own comment on video D says *"进三阶段要控血，这个公式不太行"*.
   **The script models no teleport at all.** This is the next structural gap, and it may be what limits the
   mid-band weak points.
6. **Weak wing may want a different ceiling than the strong wing** — their tornado bands differ
   (weak 6285..7973, strong 2740..6920), so a per-route ceiling value is likely.

## 🚫 Do NOT re-attempt

* **`CHAITE_WEAK_ALTITUDE_FLOOR`** — round 177, net **−3** (lifts only to 383 tiles, still inside the Boss band).
* **Any "reduce hits" lever judged by hit count** — round 177's lesson.
* Round 174 apex refill on weak (**+1/−5**); round 175 dash veto (**0/−1**); "weak under-dashes";
  "weak misses apex refills"; any 1–2 tick timing knob.
* Tornado escape under any trigger; `CHAITE_TORNADO_*`; `CHAITE_WIDTH_HOLD`; `CHAITE_PRELOCK_LIFT`;
  `CHAITE_CHARGE_NORMAL_OWNER`; `CHAITE_LOCK_RUN_AWAY`; `CHAITE_COUNTER_DASH`; `CHAITE_DASH_SUPPRESS`;
  `CHAITE_NO_CHARGE_DASH`; `CHAITE_CHARGE_CLIMB_AWAY`; `CHAITE_CHARGE_ESCAPE_SIM`; `CHAITE_APEX_REFILL`;
  `_refillGuardBudget > 0`; DPS-constant gates; armor tier as a survival lever.
* **Parsing `hostileProjectiles` with a nested `position` key** (flat `x/y/vx/vy` only).
* **Reading `plan` from the first row only** (95.9% of rows are populated).

## Standing constants & interfaces

* **The new lever**: `CHAITE_WEAK_ALTITUDE_CEILING=<px>` (0 = off). **4400 px = 275 tiles.**
* **Video trajectory recovered**: `tmp/hud177.py` → video player y **218..275 tiles**, x 3585..4102 tiles.
* **Reference frame**: `worldSurface = 500`, `rockLayer = 750`, `arena.groundTop = 500`
  (`GameProbe.cs:2521-2522`). Depth readout = `position.Y / 16f`.
* **Engine bands**: weak player 315..497 tiles; **Boss 256..500 tiles**; strong player 2823..7958 px.
* **Tornado bands**: weak y ≥ 6285 (p1 6364); strong y 2740..6920.
* `_ceilingY = SkyEnrageCeiling + CeilingMargin = 800 + 480 = 1280 tiles (20480 px)`.
* **`plan` IS readable per tick**: `phase, horizontal, jump, drop, dash, riskScore, replayFrame`.
* **28 ticks/charge**; hover 30/40; period 58. Contact: boss `|dx|<95, |dy|<92`; tornado `|dx|<122, |dy|<52`.
* Tornadoes 384/386, `aiStyle 64`, ≤**50 live**, box 225×63, `vx == 0`. Tornadoes spawn at the **Boss centre**.
* Player pool **480** — from `hurt-observations.player.lifeBefore`, **not** `equipment.lifeMax`.
* Weak climb peak −9.91 vs strong −16.52; `wingTimeMax` 130 vs 180; terminal fall +10.01 both;
  dash peak 14.50 both; cruise 7–8 both. **Both refill only at apex.**
* Acceptance run env: `CHAITE_ARMOR_TIER=obsidian|shroomite`, `CHAITE_SIM_DPS`,
  `CHAITE_SIM_DPS_FULL_TILES=400`, `CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`,
  `CHAITE_PROBE_DENSE_FRAMES=1`, `-Phase monitor -MaxTicks 20000 -FormulaRoute fishron-strong-wing|fishron-fairy-wing`.
* Artefacts: `game-probe-kC-w<dps>` (weak ceiling 4400), `game-probe-kD-s<dps>` (strong ceiling 4400),
  `k7-w300-inert` (inertness), `kA-w300` (post-revert baseline).
* Scripts: `tmp/hud177.py`, `tmp/ceil178.ps1`, `tmp/ceilgrid178.ps1`, `tmp/strong179.ps1`,
  `tmp/tab178.py`, `tmp/floor177.ps1`, `tmp/revert177.py`.
