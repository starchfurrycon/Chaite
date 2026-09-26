#!/usr/bin/env python3
"""Turn a native probe run's applied controls into a replayable route file.

The acceptance channel is `CHAITE_ROUTE_FILE`: a CSV of per-tick directional
input that `RouteReplay` feeds to the player, with `hits` then read back from
`result.json`. Nothing produces such a file yet, so this is the bridge between
"the engine decided these inputs" and "replay these inputs in the engine".

`boss-observations.jsonl` records, for every tick it samples, both the plan the
controller produced and the controls actually observed at the facade's
`ApplyPlan` return. The latter is what reached the player, so that is what is
harvested: a route built from the plan would be a claim about intent, while a
route built from the applied controls is a record of what happened.

Two shapes are emitted, because `RouteReplay` accepts both:

  * the tick-keyed form, `tick,direction,jump,up,down,dash`, which carries
    absolute ticks and is what `-startside`-independent replay wants;
  * nothing else -- the frame-indexed form is deliberately not offered, since a
    frame-indexed route silently depends on how many ticks were applied before
    the first read, which `CHAITE_ROUTE_SKIP` then has to be told by hand.

Rows are only emitted where an actual applied control was observed. A tick with
no observation is left as an explicit neutral row rather than being skipped,
because a skipped tick shifts every later frame index and would replay a
different fight than the one that was measured.
"""

from __future__ import annotations

import argparse
import json
import os
import sys

NEUTRAL = (0, 0, 0, 0, 0)


def _direction(left: bool, right: bool) -> int:
    """Terraria's horizontal intent as RouteReplay reads it: -1, 0 or 1."""
    if left and not right:
        return -1
    if right and not left:
        return 1
    return 0


def _pick_controls(row: dict) -> tuple[int, int, int, int, int] | None:
    """Read the per-tick input this row should replay.

    `plan` is preferred, and the reason is measured. `CHAITE_ROUTE_FILE` is read
    by `Runtime` and written into the plan's own fields -- `plan.Horizontal`,
    `plan.Jump`, `plan.Dash`, `plan.Drop`, `plan.FeatherFallUp` -- which are then
    passed through the facade. So the route carries a control *request*, and the
    matching record of that request is `plan`, not the facade's output.

    Harvesting the facade's output instead (`actualAtApplyReturn`) does not
    round-trip, because the facade's `MovementActionGate.ResolveJump` is not
    idempotent: replaying a harvest built from `controlJump` puts the gate's own
    output back through the gate, which suppresses the jump. MEASURED: a 1200
    tick route harvested from the control snapshot recorded 1 hit, and replaying
    it produced 6 hits and a death, with the first divergence at tick 240 where
    the original applied `jump=True` and the replay produced `jump=False`. Over
    the 50 commonly sampled ticks, 28 disagreed.

    The control snapshot stays as the fallback for streams that predate the plan
    payload. Its key names carry a `control` prefix -- `controlLeft`,
    `controlRight`, `controlJump`, `controlUp`, `controlDown`, `controlDash` --
    because those are the native `Player` fields the probe reads back, whereas
    what the run ACTUALLY applied, not what the planner asked for.

    The choice is PER CHANNEL, because the two sources fail differently:

    * `jump` must come from the PLAN. `MovementActionGate.ResolveJump` is not
      idempotent, so feeding the applied `controlJump` back through the gate
      suppresses the jump. MEASURED (see the note above): a route harvested
      entirely from the control snapshot recorded 1 hit live but replayed to 6
      hits and a death, first diverging at tick 240 where the original applied
      jump=True and the replay produced jump=False.
    * `up`, `down` and `dash` must come from what was APPLIED. Here the plan is
      the unsafe source: at tick 254 the plan carried drop=1 while the live run's
      applied `controlDown` was false, so a plan-based route replayed a descent
      the recorded fight never performed. MEASURED: 947 of 1199 ticks differed,
      first divergence exactly tick 254, with the replay's `planDash=True` while
      live had `planDash=False` and `vy` -4.74 against -4.48.
    """
    source = row.get("actualAtApplyReturn")
    applied = source if isinstance(source, dict) and source else {}

    plan = row.get("plan")
    if isinstance(plan, dict) and plan:
        horizontal = plan.get("horizontal")
        direction = 0 if not isinstance(horizontal, (int, float)) else (
            -1 if horizontal < 0 else (1 if horizontal > 0 else 0))
        jump = int(bool(plan.get("jump")))
    elif applied:
        left = bool(applied.get("controlLeft"))
        right = bool(applied.get("controlRight"))
        direction = _direction(left, right)
        jump = int(bool(applied.get("controlJump")))
    else:
        return None

    def aflag(*names: str) -> int:
        for name in names:
            if name in applied:
                return int(bool(applied.get(name)))
        return 0

    return (direction, jump, aflag("controlUp", "up", "Up"),
            aflag("controlDown", "down", "Drop", "Down"),
            aflag("controlDash", "dash", "Dash"))


def harvest(path: str) -> tuple[list[tuple[int, tuple[int, int, int, int, int]]], dict]:
    """Collect observed tick -> controls pairs from a boss observation stream."""
    by_tick: dict[int, tuple[int, int, int, int, int]] = {}
    stats = {"rows": 0, "with_controls": 0, "unparsable": 0, "min_tick": -1, "max_tick": -1}

    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            stats["rows"] += 1
            try:
                row = json.loads(line)
            except ValueError:
                stats["unparsable"] += 1
                continue
            tick = row.get("gameUpdateCount")
            if not isinstance(tick, int):
                # Older streams only carry the probe's own counter. It is not the
                # axis the route reader uses, so a route keyed on it is applied at
                # a constant offset; see the note at GameProbe's gameUpdateCount.
                tick = row.get("tick")
            if not isinstance(tick, int):
                continue
            controls = _pick_controls(row)
            if controls is None:
                continue
            by_tick[tick] = controls
            stats["with_controls"] += 1

    if not by_tick:
        return [], stats

    stats["min_tick"] = min(by_tick)
    stats["max_tick"] = max(by_tick)
    ordered = sorted(by_tick.items())
    return ordered, stats


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", help="artifacts run directory name or path")
    parser.add_argument("-o", "--output", help="route file to write")
    parser.add_argument(
        "--start-tick",
        type=int,
        default=None,
        help="first tick to emit; defaults to the first observed tick",
    )
    parser.add_argument(
        "--end-tick",
        type=int,
        default=None,
        help="last tick to emit; defaults to the last observed tick",
    )
    parser.add_argument(
        "--stream",
        default="boss-observations.jsonl",
        help="observation stream to harvest",
    )
    args = parser.parse_args(argv)

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    run_dir = args.run
    if not os.path.isdir(run_dir):
        run_dir = os.path.join(root, "artifacts", args.run)
    if not os.path.isdir(run_dir):
        print(f"no such run directory: {args.run}", file=sys.stderr)
        return 2

    stream = os.path.join(run_dir, args.stream)
    if not os.path.isfile(stream):
        print(f"no observation stream: {stream}", file=sys.stderr)
        return 2

    pairs, stats = harvest(stream)
    if not pairs:
        print(
            f"no applied controls in {stream} "
            f"({stats['rows']} rows, {stats['unparsable']} unparsable)",
            file=sys.stderr,
        )
        return 3

    start = stats["min_tick"] if args.start_tick is None else args.start_tick
    end = stats["max_tick"] if args.end_tick is None else args.end_tick
    if end < start:
        print("end tick precedes start tick", file=sys.stderr)
        return 2

    observed = dict(pairs)
    lines: list[str] = []
    gaps = 0
    for tick in range(start, end + 1):
        controls = observed.get(tick)
        if controls is None:
            # A neutral filler preserves the absolute tick numbering, which is
            # the whole point of the tick-keyed form.
            controls = NEUTRAL
            gaps += 1
        lines.append("{},{},{},{},{},{}".format(tick, *controls))

    output = args.output
    if not output:
        output = os.path.join(root, "tmp", f"route-{os.path.basename(run_dir)}.txt")
    os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)
    with open(output, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines))
        handle.write("\n")

    dashes = sum(1 for _, c in pairs if c[4])
    lefts = sum(1 for _, c in pairs if c[0] < 0)
    rights = sum(1 for _, c in pairs if c[0] > 0)
    jumps = sum(1 for _, c in pairs if c[1])

    print(f"stream          : {os.path.relpath(stream, root)}")
    print(f"rows read       : {stats['rows']} ({stats['with_controls']} carried controls)")
    print(f"tick range      : {start}..{end} ({end - start + 1} emitted, {gaps} neutral fillers)")
    print(f"route file      : {os.path.relpath(output, root)}")
    print(f"controls        : left={lefts} right={rights} jump={jumps} dash={dashes}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
