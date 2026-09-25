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
    """Read the controls that reached the player on this tick.

    `actualAtApplyReturn` is preferred because it is sampled at the point the
    plan has been applied, so it describes the frame that will actually run.
    Older streams only carry `plan`, which is the request rather than the
    result, so it is the fallback rather than the default.
    """
    source = row.get("actualAtApplyReturn") or row.get("plan")
    if not isinstance(source, dict):
        return None

    # The keys differ between the plan shape and the control snapshot shape.
    left = bool(source.get("left", source.get("Left", False)))
    right = bool(source.get("right", source.get("Right", False)))
    jump = bool(source.get("jump", source.get("Jump", False)))
    up = bool(source.get("up", source.get("Up", False)))
    down = bool(source.get("down", source.get("Drop", source.get("Down", False))))
    dash = bool(source.get("dash", source.get("Dash", False)))

    return (_direction(left, right), int(jump), int(up), int(down), int(dash))


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
        lines.append("{},{},{},{},{}".format(tick, *controls))

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
