# Round 157 handoff

## HEAD
`cc52dbb` — pushed. Worktree clean except untracked `tmp/`.
Tests **749 pass / 9 fail**, the long-standing accepted set (unchanged since round 151).

## HEADLINE: the measurement environment had been silently wrong, and it is now guarded

The ambient shell that hosts this repository carries **six** `CHAITE_*` variables:

```
CHAITE_POLICY_FILE   = ...\policies\fishron-strong-wing.policy.bin
CHAITE_POLICY_FORMAT = exported
CHAITE_OBS_WORLD_BOUND = 18
CHAITE_PROJ_COLLAPSE   = 1
CHAITE_PROJ_SLOTS      = 12
CHAITE_PROJ_SORT       = threat
```

Hand-written clear lists had only ever covered the first two. A sweep that looked clean therefore
measured a **different circuit**: weak 300 returned `6230/7/49420` when the committed default is
`8777/8/36712` — and three different parameter values reproduced each other **byte for byte**,
which is the broken-invariant signature. Any weak-lead conclusion drawn from that sweep was
unreliable.

`tools/run-native-acceptance.ps1` now refuses on the two variables that replace the controller
outright (with a policy loaded, every `Chaite.Core` edit is inert) and **records** the rest to
`<run>\acceptance-environment.json`, written **after** launch because the isolated launcher aborts
on any file its manifest does not list.

Re-verified under a clean environment: strong-wing lead 40 reproduces **every** point exactly
(9697/7/32136, 8332/3/0, 7221/3/0, 6382/5/0, 5220/2/0, 3661/0/0, 2881/0/0), so the round-156
change stands on measurement rather than on a polluted run.

## The hover duration can never be a discriminator

Reading `ai[0]`/`ai[1]` straight out of the dense runs: every phase-1 hover is **exactly 30 ticks**
and every charge **exactly 28**, identical across s600/s800/s900/s1000. So `_hoverLimit` is a
constant 31 and the pre-charge window always opens at `timer = 10`. This is the mechanistic reason
round-156's hover-length-proportional lead could never have worked — that route is now closed in
principle, not merely by measurement.

## Weak-wing lead: a real fight gain that is correctly NOT the default

Lead 40 vs the reviewed 30, measured clean:

| DPS | lead 30 | lead 40 |
|---|---|---|
| **900** | **5729/4/0 KILL** | 4915/5/12318 death |
| 1200 | 3082/8/27162 | 3800/4/12715 |
| **1300** | 3489/4/14076 | **4138/1/0 KILL** |
| **1500** | 3642/4/330 | **3660/1/0 KILL** |

It trades one kill for two, takes failures 6 → 5, and improves the worst margin from 36712 to 49420
Boss health left. It is still **not** the default, because raising it breaks two real branch-
precedence assertions (`FishronWingKeepsStandoffGap`, `FishronWingLatchesTheBodyEscapeSide`), which
drive the route at `NativeTimer` 1–2 and expect standoff/personal-space rather than precharge-jump.
Available as `CHAITE_WEAK_PREJUMP=40`.

## Strong-300's late cluster, dissected per tick

All **seven** contacts are the Boss **body** (370), not the wall — the earlier `wall386` reading came
from conflating `hurt-observations` with `prehit`. `threatsWithin400` is 0 on every row.

Four are wing-exhausted charges (`wingTime = 0`). The three that form the late cluster are in Boss
states 5/6/7, where the player still has `wingTime 50` but descends at **+10.0 (terminal)** with
**vx ≈ 0** while the Boss climbs. Making that descent conditional on the Boss being above the player
— the obvious reading of the owner's "keep horizontal speed" rule — is **worse on both arms**
(strong 9697→11341, weak 8777→4121). Reverted and recorded; this is the **fourth** vertical-
suppression refutation, so the descending trajectory is load-bearing.

Reverted state reproduces the committed baseline byte for byte.

## Status: objective NOT met

| arm | failing points |
|---|---|
| strong | **300** (9697/7/32136, late-cluster) |
| weak | **300, 600, 900, 1000, 1200, 1500** |

Strict zero-hit target unmet: route replay gives strong 6000/6/55 and weak 5636/9/102
(both `MATCH`, `REPRODUCED`).

## Next, in priority order

1. **The four wing-exhausted charge contacts** (t=2083/2666/4998/5464, `wingTime = 0`). These are the
   majority of strong-300's hits and have never been analysed frame by frame. §150 shows a dash
   fired on lock cannot reach the contact window; the unexplored question is whether the *wing
   budget* can be spent so the player is not at `wingTime = 0` when a charge arrives.
2. Weak 300/600 remain front-loaded and are the largest remaining block. Weak 1200 also improved
   markedly under lead 40, so re-test it with the knob set deliberately.
3. Re-run the Shroomite tier: the last full Shroomite sweep predates the lead-40 default.
4. Clean up `CHAITE_AUTO_FIRE` in `src/Chaite.Plugin/TerrariaFacade.cs`.
5. 9 test failures are pre-existing and unchanged; **never** claim the suite is green.

## Method notes for the next round

* Always clear `CHAITE_POLICY_FILE` **and** `CHAITE_POLICY_FORMAT`; the runner now enforces it.
* Run sweeps from a shell where every `CHAITE_*` is cleared, and set only what you intend to vary.
* A/B one variable at a time. Round 157 lost time to an A/B that changed two things at once and
  produced a confident, wrong explanation ("the rename breaks the tests") that had to be retracted.
* Parse `artifacts/<run>/result.json`; never read the banner.
* Dense runs need `CHAITE_PROBE_DENSE_FRAMES=1`; sparse censuses produced wrong numbers twice.
