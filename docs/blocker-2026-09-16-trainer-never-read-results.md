# Blocker found 2026-09-16: the trainer never read a single result

## What was wrong

`runwave.ps1` names each run by **concatenating** the tag and the seed:

```powershell
$seedTag = "$Tag$seed"          # tag ...-g1-p + seed 1  ->  ...-g1-p1
```

`runprobe.ps1` then names the directory `game-probe-rt-<seedTag>-<stamp>`, so
seed 1 of the parent of generation 1 produced:

```
game-probe-rt-empress-broom-nohit-v4-g1-p1-0916-151433
```

The trainer selected a tag's runs with a **trailing dash**:

```powershell
Where-Object { $_.Name -like "game-probe-rt-$RunTag-*" }
```

That pattern requires `-g1-p-`, which never occurs. Measured directly:

| pattern | matches |
| --- | --- |
| `game-probe-rt-<tag>-*` (as written) | **0** |
| `game-probe-rt-<tag>*` | 18 |

So `Get-ResultFor` always returned zero rows. `Invoke-Evaluation` treated that as
a probe timeout, retried three times, then threw and killed the run — before the
parent line was ever written to `log.csv`. The same pattern was used by
`Remove-Item -Filter`, so stale run directories were never cleaned either.

## Where it came from

The trailing dash was **mine**, added in `e47fb9d` to stop a tag of `g1` from
matching `g11`. The intent was right and the comment even predicted the collision
it was guarding. What it missed is that the tag and the seed are joined with no
separator, so the dash cannot appear where the pattern expects it. Git history
shows both halves:

```
3a00ce9  +  Where-Object { $_.Name -like "game-probe-rt-$RunTag*" }     <- v1 worked
e47fb9d  -  Where-Object { $_.Name -like "game-probe-rt-$RunTag*" }
         +  Where-Object { $_.Name -like "game-probe-rt-$RunTag-*" }     <- broke it
```

This is why `nohit-v1` has real numbers in its log while `v3` completed zero
generations. v3 was not slow or unlucky: it was dying in generation 1 every time,
and I had attributed that to the by-value policy bug instead.

## Why it was hard to see

The two layers disagreed and the disagreement was invisible:

* the wave log printed `status=loss ticks=1876 ... wall=18.4s` — **success**;
* `result.json` existed in every directory — **success**;
* the trainer said `produced no results` — **failure**.

Zero rows is a legitimate outcome for a probe that hits its wall-clock cap, so
the retry logic swallowed the difference. Nothing anywhere printed the name it
looked for next to the names that existed.

## The fix

1. `runwave.ps1`: `$seedTag = "$Tag-$seed"`, giving
   `game-probe-rt-<tag>-<seed>-<stamp>`. Now the trainer's trailing dash matches
   exactly, and it *also* still prevents `c1` matching `c11` and `g1` matching
   `g11` — which is what the dash was for in the first place. Fixing the naming
   rather than the pattern keeps both properties.
2. `train-policy.ps1`: added `Get-NearMissReport`, which on a zero-result
   evaluation lists the directories whose name merely *contains* the tag. If
   near misses exist but nothing matched, the pattern is wrong, not the probe.
   This is reported at both the evaluation and the per-candidate site.

## Verification

`log.csv` after the fix (parent of generation 1, six seeds):

```
1,parent,1,2,3,4,5,6,0.08286,wins=0,noHit=0
```

A separate one-generation end-to-end check (`mini-check`) scored four candidates
**on the same seed** and produced genuinely different fitness —
`-0.03453 / -0.00337 / +0.09027 / -0.00381`. Differences that come from the
policy rather than from the seed are only possible once results are read at all.

The six parent results also still match the fixed machine seed by seed
(1876 ticks, 1 hit, boss life 75115/74037/75569/73628/74995/75214), so zero
weights continue to reproduce the baseline exactly.

## Standing lesson

Same shape as the by-value bug: an intermediate layer reported success while the
consumer silently received nothing. A "no results" path must distinguish *the
producer emitted nothing* from *the consumer looked in the wrong place*, and it
must print the names it looked for. Silent zero is the failure mode to design
against.