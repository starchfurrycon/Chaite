"""Round 163, direction (A): do diverged runs ever reconverge?

If the fork is unavoidable but the movement re-converges to a DPS-independent
safe trajectory within N ticks, then only a short window after each fork needs
defending. If positions never reconverge, the fork permanently changes the fight
and per-phase safety is all that matters.

Metric: for each run and a zero-hit reference, |px_run - px_ref| over ticks after
the fork. Report whether the gap decays or persists.

Usage: python tmp/reconverge.py <ref-run-dir> <run-dir> [...]
"""
import io, json, os, sys


def load(run):
    out = {}
    for line in io.open(os.path.join(run, "boss-observations.jsonl"), encoding="utf-8"):
        r = json.loads(line)
        p = r["player"]
        out[r["tick"]] = (p["position"]["x"], p["position"]["y"])
    return out


def main():
    ref_dir = sys.argv[1]
    ref = load(ref_dir)
    print("reference: %s (%d ticks)" % (ref_dir, len(ref)))
    print()
    print("%-26s %8s %10s %10s %10s %10s" % (
        "run", "fork", "gap@fork", "gap@+200", "gap@+500", "gap@end"))
    for run in sys.argv[2:]:
        cur = load(run)
        common = sorted(set(ref) & set(cur))
        if not common:
            print("%-26s no overlap" % run)
            continue
        fork = None
        for t in common:
            if abs(cur[t][0] - ref[t][0]) > 0.5 or abs(cur[t][1] - ref[t][1]) > 0.5:
                fork = t
                break

        def gap(t):
            if t not in ref or t not in cur:
                return None
            return max(abs(cur[t][0] - ref[t][0]), abs(cur[t][1] - ref[t][1]))

        last = common[-1]
        vals = []
        for off in (0, 200, 500):
            vals.append(gap(fork + off) if fork is not None else None)
        vals.append(gap(last))
        fmt = lambda v: ("%10.1f" % v) if v is not None else "%10s" % "-"
        print("%-26s %8s %10s %10s %10s %10s" % (
            os.path.basename(run),
            fork if fork else "-",
            *[fmt(v) for v in vals]))

    # Does the separation grow, and is it bounded by the arena width?
    print()
    print("arena span is 16..5120 px, so a gap approaching ~5100 means the runs are")
    print("in opposite halves of the arena (fully decorrelated).")


if __name__ == "__main__":
    main()
