# Learned-policy files

> **Historical record (scope change 2026-09-23).** Every candidate in this file
> was measured on the Empress of Light strong-wing route
> (`FormulaRoute.EmpressStrongWingsDash`). That route — and the Empress itself —
> has been removed from the program, so the enum member named below no longer
> exists and these `.policy.txt` files can no longer be loaded. The measurements
> are kept as the record of what was tried, and the four files themselves have
> been **moved out of this directory** to
> `artifacts/retired-policies-empress-20260923/`, because this directory is
> published with the release and a shipped file that names a route the program
> does not have is a leftover, not a record. `zero.policy.txt` is route-agnostic
> and stays.

These are the residual-network files read by `LearnedPolicy` through the
`CHAITE_POLICY_FILE` environment variable. They are the only way a policy can be
changed without recompiling, which is what the project's "no rebuild during a
training run" constraint requires: a probe wave that spans two builds is void,
so training has to move parameters, not code.

## Running with a policy

There are two on-disk formats and `CHAITE_POLICY_FORMAT` decides which loader
owns `CHAITE_POLICY_FILE`. They are not interchangeable.

### The exported (bridge-trained) format — `fishron-strong-wing.policy.bin`

```
CHAITE_POLICY_FILE=<absolute path to fishron-strong-wing.policy.bin>
CHAITE_POLICY_FORMAT=exported
CHAITE_PROJ_SLOTS=12
CHAITE_PROJ_SORT=threat
CHAITE_PROJ_COLLAPSE=1
CHAITE_OBS_WORLD_BOUND=18
```

`CHAITE_POLICY_FORMAT=exported` is mandatory: `CHAITE_POLICY_FILE` alone selects
the residual loader below, which would then try to read float32 weights as
whitespace-separated tokens. With the format set, `LearnedPolicy.EnsureConfigured`
stands down (`LearnedPolicy.cs:216`) and **`CHAITE_POLICY_ROUTES` is not needed**.
The observation builder variables must match the ones the checkpoint was trained
with; a mismatch is refused at startup by `ChaitePolicyDriver` rather than
silently feeding the network the wrong columns.

Leave `CHAITE_BRIDGE_FILE`, `CHAITE_ROUTE_FILE`, `CHAITE_OBS_AGG`,
`CHAITE_PROBE_OUT`, `CHAITE_EPISODES` and `CHAITE_RUN_MAX_TICKS` unset: they
belong to the trainer and to the rehearsal harness, and `CHAITE_BRIDGE_FILE`
in particular makes `RouteReplay` drive the player, which would replace the
policy.

The file format is detected from its own `CHAITEPOLICY` magic, so the `.bin`
extension is documentation rather than a loader contract.

### The residual text format — `zero.policy.txt`

```
CHAITE_POLICY_FILE=<absolute path to a .policy.txt>
CHAITE_POLICY_ROUTES=FishronStrongWingsDash
```

`CHAITE_POLICY_ROUTES` is required whenever a file is set; the loader refuses to
guess which routes a policy owns and fails closed if it is missing. The name is
the `FormulaRoute` enum member, not the CLI route name (`fishron-strong-wing`).
The current members are `None`, `FishronFairyWingsDash`, `FishronStrongWingsDash`,
`FishronTrustyChillet`, `FishronTrustyChilletIgnis` and `FishronLilithWolf`.

## Format

Whitespace-separated tokens:

```
chaite-policy 1 <inputs> <hidden>
<hidden * inputs>   hidden weights, row major, index h * inputs + i
<hidden>            hidden biases
<10 * hidden>       head weights, index k * hidden + h
<10>                head biases
```

The header order is **inputs before hidden**. Writing it the other way round
produces `declares 1 inputs and 38 hidden units` and the plugin disables
automation at takeover rather than quietly running the fixed machine.

The ten heads are, in order, horizontal (leave, -1, +1), vertical (leave, -1,
+1), jump (leave, toggle), dash (leave, toggle). Class zero means "leave the
scripted decision alone" and receives `CHAITE_DEFAULT_ACTION_MARGIN`, so an
all-zero file reproduces the fixed circuit bit for bit. That identity is the
control every candidate is measured against.

The 38 inputs are, in order: dx/900, dy/900, |dx|/900, |dy|/900, distance/900,
player velocity x and y over 20, boss velocity x and y over 20, the two relative
velocities over 20, a one-hot of native state 0..13, native timer over 600,
native sequence over 600, a one-hot of form 0..3, four arena clearances over
640, life fraction, and whether the player is below and right of the boss. The
state one-hot and the timer are what let a policy specialise per phase and per
part of the clock, which is the only kind of reaction available at this layer:
`FormulaScriptInput` carries no threat information, so a script route cannot see
an incoming projectile at all.

Write files with `tools/new-policy.ps1` (`zero`, `random`, `head-bias`,
`perturb`); search with `tools/train-policy.ps1`.

## Files here

| file | route | what it is | measured status |
|---|---|---|---|
| `fishron-strong-wing.policy.bin` | `FishronStrongWingsDash` (exported) | bridge-trained MLP, 121 observations, 24 actions | **accepted**: mounted rehearsal 97.4% (38/39) against the 90% win bar; sha256 `40C41D75…` |
| `zero.policy.txt` | any | all weights zero, the identity | reproduces the script bit for bit |
| ~~`empress-strong-wing-nudge-left.policy.txt`~~ | ~~EmpressStrongWingsDash~~ | horizontal decrement in every state (head 1 bias 4.0) | **refuted**; file moved to `artifacts/retired-policies-empress-20260923/` |
| ~~`empress-strong-wing-gate46.policy.txt`~~ | ~~EmpressStrongWingsDash~~ | horizontal decrement only in native states 4 and 6 | **refuted**; file moved to `artifacts/retired-policies-empress-20260923/` |
| ~~`empress-strong-wing-gate89.policy.txt`~~ | ~~EmpressStrongWingsDash~~ | horizontal decrement only in native states 8 and 9 | **refuted**; file moved to `artifacts/retired-policies-empress-20260923/` |
| ~~`empress-strong-wing-gate-not-4689.policy.txt`~~ | ~~EmpressStrongWingsDash~~ | horizontal decrement in every state except 3, 4, 6, 8 and 9 | **refuted**; file moved to `artifacts/retired-policies-empress-20260923/` |

Nothing here beats the hand-written script. Every candidate was measured against
the zero policy on paired seeds and scored on damage taken by the player per
thousand ticks, which is the rate at which the zero-hit budget is spent:

| arm | seeds | dmgPerK | clean% | hits | player damage | boss damage |
|---|---|---|---|---|---|---|
| script | 1-7 | **162.0** | 79.9 | 93 | 6,767 | 535,375 |
| nudge left, every state | 1-7 | 172.1 | 81.6 | 96 | 7,701 | 606,969 |
| script | 8-13 | **177.7** | 79.5 | 84 | 5,909 | 415,655 |
| gate 8 and 9 | 8-13 | 179.9 | 79.3 | 80 | 5,421 | 375,909 |
| gate 4 and 6 | 8-13 | 203.6 | 76.2 | 79 | 5,576 | 339,981 |
| gate everything but 3, 4, 6, 8, 9 | 8-13 | 214.6 | 77.4 | 63 | 5,527 | 322,688 |

A change that helps when applied in every state is harmful in each of its parts,
and the whole-family result is harmful too, so the gain is not a matter of *when*
to nudge. Two facts explain the ranking. Player damage taken is nearly constant
across the arms, 5,421 to 5,909, while survival time varies by 22 percent, so
these policies change how fast a roughly fixed damage budget is spent, not how
much is taken. And the nudge's apparent win came from the old metric: hits per
thousand ticks improved from 2.226 to 2.146 while damage taken rose 13.8 percent,
because it traded many light hits for fewer heavy ones. Scoring on hits alone
accepts that trade, which is why the scorer now ranks on damage taken instead.


Head one is `ClampStep(scripted - 1)`, not "always left": scripted +1 becomes 0.
The observation rows confirm it, showing applied horizontal values of -1 and 0
and never +1 for the nudge file.

### The dash attack is one loop unit, not two

Native state 9 is the side-selected form of table entry 8, as
`EmpressFormulaStateContract` documents and normalises. Measured on the first
sampled row of each attack, ai8 commits with the player left in 18 of 20 cases
and ai9 with the player right in 26 of 26. Counting 8 and 9 separately therefore
compares left-commits against right-commits and reports it as a per-state
effect; pass `-MergeSideStates` to `report-loops.ps1` to score them as one.

That correction reverses what the nudge appears to do:

| loop unit | script clean% | nudge clean% | entry-position sd, script to nudge |
|---|---|---|---|
| 4 | 50.0 | 100.0 | 198/299 to 94/108 |
| 6 | 45.5 | 90.5 | 369/638 to 55/63 |
| 8 and 9 merged | 77.6 | 58.6 | 250/86 to 164/248 |
| all | 79.9 | 81.6 | — |

So the clean-loop gain comes from states 4 and 6, the dash attack gets worse,
total hits stay flat, and closure improves in every one of those units. The two
gated files exist to separate within-state effect from earlier-state effect,
because the nudge applied in every state throughout and a per-state table of a
global change cannot attribute cause.


`empress-strong-wing-nudge-left` is the first candidate a search produced. It is
preserved — in `artifacts/retired-policies-empress-20260923/`, not here, because
its route no longer exists — as the first policy artifact of this project and
because it reproduces, not because it is an improvement worth adopting. It is now
refuted:
on its own paired seeds it takes 13.8 percent more damage than the script, 7,701
against 6,767, and the metric that once made it look better is the one described
above. The hypothesis it suggested, that the script's deliberate
`Horizontal = 0` in states 8 and 9 might be wrong for this loadout, was tested
directly by `gate89` and is also refuted: acting in those states scores 179.9
against the script's 177.7 on the same seeds.

