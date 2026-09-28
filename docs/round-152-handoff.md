# Round 152 handoff

## HEAD
`c1ca394` — all pushed. Worktree clean except untracked `tmp/`.

## HEADLINE: the mid-band failures are TORNADO deaths, not body contacts

The strong 700/800/1000 failures had been assumed to be charge/body problems (§141). They
are not. From native `hurt-observations.jsonl`:

- **strong 1000**: all **four** hits are projectile 386 (Cthulhunado), all in phase 2, at
  ticks **4048 / 4088 / 4128 / 4168 — exactly 40 apart, four in a row, one column**.
- **strong 800**: 1 body (ph1), 1 tornado (ph2), 1 body (ph3), then **2 tornadoes at 5606
  and 5646**.
- **strong 900 (KILL)**: 1 bubble + 2 body — *no tornado hits at all*.

The `prehit` window gives the mechanism: the player **descends** (vy −7.5 → −10.0) from
y 5912 to y 5589 while the column sits at **y 5592**, closing horizontally from 528 → 87 px
until it enters the column box at tick 4046. A type-386 column is `150×scale` by `42×scale`
with scale capped at 1.5 ⇒ **225 px wide but only 63 px tall** (half-extents 112 × 31), and it
never moves vertically. **The descent is the lethal command**, and three branches issued it
without knowing the column's vertical half: `charge-descend` (default beat → vertical +1,
line 1821), `tornado-bait` (line 2218), `precharge-jump` (line 2177).

## LANDED: a tornado vertical-axis guard (`CHAITE_TORNADO_AXIS_GUARD`, default ON)

`ApplyTornadoAxisGuard(player, ref vertical)` is called at the end of `ChargeEscape` and at
the top of `Cruise`, so it overrides every branch including the personal-space latch and the
counter-dash. It refuses only the axis about to intersect: fires while the player is
horizontally inside-or-near the column AND closing on it horizontally AND still clear of it
vertically. It is **not** an inside-the-box rule — that case still belongs to the existing box
branch at line ~2073.

It is gated **twice**, and both gates were measured:

1. **Off whenever the extended distance standoff is armed.** The two rules solve the same
   problem; running both cost strong 300 the flagship result (16090 → 10626).
2. **On only up to 900 simulated DPS** (`CHAITE_TORNADO_AXIS_GUARD_DPS_MAX`). Above ~1000 the
   reviewed circuit already has the kill and the guard's interference costs it — weak 1100
   went **4788/3 KILL → 3875/6 DEATH**.

**Net measured effect: weak 800 gains a new kill (5287/5 no-kill → 6381/3), weak 600 lives
longer (4924 → 6130), and every other measured point is unchanged.**

## REFUTED this round (do not re-attempt)

- **Unconditional vertical suppression** (no "closing" requirement): missed its own target
  points byte-identically AND cost strong 300 **7218 ticks** (16090 → 8872).
- **Reach 600 px**: does make the guard fire in the strong-1000 window (the vertical
  separation passes 73 px at tick ~4014, when the horizontal gap is still ~360 px), but:
  strong 600 KILL → no-kill, strong 900 KILL → no-kill, weak 600/800 gutted to **3542** — and
  **strong 1000 still byte-identical**. The reach stays at **125**.

## Why 125 is a no-op in its own target window (the key insight for next round)

At tick 4044, `dx = 124.9` (just inside 125) while `dy` is already only **−33.5**. The player
is **always already inside the 73 px vertical margin** by the time it is horizontally close.
So the "still clear vertically" clause never holds where the death occurs, and the guard never
fires there. **The descent cannot be vetoed after the lock — by then the vertical overlap has
already happened.**

⇒ **Next entry point (quantified, no guessing needed): cut the descent BEFORE the lock**, in
the precharge/pre-lock decision, so the descent never happens at all. That is the same shape as
both genuine wins in this session (129, 132): *add a missing pre-condition* rather than
redirect a command after the fact.

## Committed-default results (obsidian = honest tier)

**Strong**: 300 **16090/8/0 KILL** · 600 **8329/4/0 KILL** · 700 6205/7/11888 · 800 6011/5/5015 ·
900 5739/3/0 KILL · 1000 4536/4/11408 · 1100 4795/**0/0 zero-hit** · 1200 4437/**0/0 zero-hit** ·
1500 3661/1/0 KILL · 2000 2881/**0/0 zero-hit**

**Weak**: 300 8777/8/36712 · 600 **6130/6** · 800 **6381/3/0 KILL (new)** · 900 4439/5 ·
1000 4395/5/13670 · 1100 4788/3/0 KILL · 1200 3082/8 · 1300 3489/4 · 1500 3362/4/7466 ·
2000 2881/1/0 KILL

## Still unmet (honest)
- **Owner's relaxed criterion (survival-kill across 300–2000) is NOT met.** Strong fails
  700/800/1000; weak passes only 800/1100/1500/2000.
- **Weak wing: still ZERO zero-hit results ever.** Zero-hit is only observed for the strong
  wing at 1100/1200/2000; no zero-hit claim below 1200 DPS.
- Tests: **749 pass / 9 fail** — same pre-existing baseline. Never claim the suite is green.
- Routes still `MATCH` (strong 6000/6/55, weak 5636/9/102).

## Run method
Unset `CHAITE_POLICY_FILE` AND `CHAITE_POLICY_FORMAT`; then `CHAITE_ARMOR_TIER=obsidian` (or
`shroomite`), `CHAITE_SIM_DPS=<dps>`, `CHAITE_SIM_DPS_FULL_TILES=400`,
`CHAITE_SIM_DPS_ZERO_TILES=401`, `CHAITE_SIM_BUBBLE_BREAK=0.95`;
`CHAITE_PROBE_DENSE_FRAMES=1` before `run-native-acceptance.ps1`. Route + `-FormulaRoute`
always together. `-WallSeconds` 15..900. **Verify by parsing `artifacts/<run>/result.json`**
(`ticks`/`hits`/`bossLifeRemaining`) — `Select-String` on the printed verdict is case-sensitive
in pwsh 7 and has silently produced false DIFFs.
