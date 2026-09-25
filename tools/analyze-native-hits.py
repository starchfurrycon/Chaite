#!/usr/bin/env python3
"""Per-tick native hit analysis for the Duke Fishron probe.

`boss-observations.jsonl` at dense resolution is one row per native tick and
carries the whole state that decides an outcome: the player's position,
velocity and mobility resources, the boss's position, velocity, hunt timer and
attack sequence, and the controls that actually reached the player. This reads
that stream back and prints the window around each hit, so a hit can be
explained by the native numbers rather than by a model.

The boss's own attack clock is in `npcs[0].ai`: ai[0] is the state, ai[1] the
hunt countdown, ai[2] the state timer and ai[3] the sequence that selects the
next projectile attack. A charge writes its velocity once at state entry and
then travels a fixed distance, so the approach is visible as a constant
velocity with a shrinking gap.
"""

from __future__ import annotations

import argparse
import json
import os
import sys


def load(path: str) -> tuple[dict[int, dict], list[dict]]:
    rows: list[dict] = []
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return {row["tick"]: row for row in rows}, rows


def boss_of(row: dict) -> dict | None:
    for npc in row.get("npcs") or []:
        if npc.get("boss") and npc.get("active"):
            return npc
    return None


def fmt_window(rows_by_tick: dict[int, dict], center: int, before: int, after: int) -> None:
    print(
        " tick |    plX    plY   plvx  plvy  wing  dd   eo |    boX    boY   bovx  bovy  bs  bs2 bs3 | "
        "L R U D J dash | plan phase"
    )
    print("-" * 150)
    for tick in range(center - before, center + after + 1):
        row = rows_by_tick.get(tick)
        if row is None:
            print(f"{tick:5d}  (no row)")
            continue
        player = row.get("player") or {}
        pos = player.get("position") or {}
        vel = player.get("velocity") or {}
        boss = boss_of(row)
        bp = (boss or {}).get("position") or {}
        bv = (boss or {}).get("velocity") or {}
        ai = (boss or {}).get("ai") or [None, None, None, None]
        ctl = row.get("actualAtApplyReturn") or {}
        plan = row.get("plan") or {}
        marker = "  <== HIT" if tick == center else ""
        print(
            "%5d | %6.0f %6.0f %6.1f %5.1f %5s %4s %4s | %6.0f %6.0f %6.1f %5.1f %3s %4s %3s | "
            "%d %d %d %d %d %4d | %s%s"
            % (
                tick,
                pos.get("x", 0), pos.get("y", 0), vel.get("x", 0), vel.get("y", 0),
                player.get("wingTime"), player.get("dashDelay"), player.get("eocDash"),
                bp.get("x", 0), bp.get("y", 0), bv.get("x", 0), bv.get("y", 0),
                ai[0], ai[2], ai[3],
                bool(ctl.get("controlLeft")), bool(ctl.get("controlRight")),
                bool(ctl.get("controlUp")), bool(ctl.get("controlDown")),
                bool(ctl.get("controlJump")), bool(ctl.get("controlDash")),
                plan.get("phase"), marker,
            )
        )


def hits_from(hurt_path: str) -> list[dict]:
    hits: list[dict] = []
    if not os.path.isfile(hurt_path):
        return hits
    with open(hurt_path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                hits.append(json.loads(line))
    return hits


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", help="artifacts run directory name or path")
    parser.add_argument("--before", type=int, default=26)
    parser.add_argument("--after", type=int, default=6)
    parser.add_argument("--hit-index", type=int, default=None, help="only this hit, 1-based")
    args = parser.parse_args(argv)

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    run_dir = args.run if os.path.isdir(args.run) else os.path.join(root, "artifacts", args.run)
    if not os.path.isdir(run_dir):
        print(f"no such run directory: {args.run}", file=sys.stderr)
        return 2

    rows_by_tick, rows = load(os.path.join(run_dir, "boss-observations.jsonl"))
    hits = hits_from(os.path.join(run_dir, "hurt-observations.jsonl"))
    print(f"run   : {os.path.relpath(run_dir, root)}")
    print(f"ticks : {len(rows)} rows, {min(rows_by_tick)}..{max(rows_by_tick)}")
    print(f"hits  : {len(hits)}")
    print()

    for index, hit in enumerate(hits, start=1):
        if args.hit_index is not None and index != args.hit_index:
            continue
        tick = hit.get("tickBefore")
        source = hit.get("source") or {}
        print("=" * 150)
        print(
            f"HIT {index}/{len(hits)} at tick {tick}: {source.get('kind')} type {source.get('type')}, "
            f"damage {(hit.get('request') or {}).get('damage')} -> {hit.get('actualReturn')}, "
            f"life {(hit.get('player') or {}).get('lifeBefore')} -> {(hit.get('player') or {}).get('lifeAfter')}"
        )
        print("=" * 150)
        if isinstance(tick, int) and tick in rows_by_tick:
            fmt_window(rows_by_tick, tick, args.before, args.after)
        else:
            print(f"  no dense row at tick {tick}")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
