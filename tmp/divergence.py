"""Round 163: where do the zero-hit runs diverge from the high-hit runs?

Trajectories are deterministic, so any early divergence must amplify into the
final difference. If divergence appears early, an on-line precursor exists; if
only mid-game, the phase transition is implicated.

Usage: python tmp/divergence.py
"""
import io, json, os, math


def load(run):
    """Per-tick player/boss sample, keyed by tick."""
    out = {}
    for line in io.open(os.path.join(run, "boss-observations.jsonl"), encoding="utf-8"):
        r = json.loads(line)
        p = r["player"]
        npcs = [n for n in (r.get("npcs") or ()) if n.get("type") == 370]
        if not npcs:
            continue
        b = npcs[0]
        ai = b.get("ai") or []
        out[r["tick"]] = {
            "px": p["position"]["x"], "py": p["position"]["y"],
            "vx": p["velocity"]["x"], "vy": p["velocity"]["y"],
            "wing": p.get("wingTime"), "life": p.get("life"),
            "bx": b["position"]["x"], "by": b["position"]["y"],
            "bvx": b["velocity"]["x"], "bvy": b["velocity"]["y"],
            "state": ai[0] if ai else None,
            "timer": ai[1] if len(ai) > 1 else None,
        }
    return out


def first_divergence(a, b):
    """First tick where the two runs differ materially."""
    common = sorted(set(a) & set(b))
    for t in common:
        x, y = a[t], b[t]
        if abs(x["px"] - y["px"]) > 0.5 or abs(x["py"] - y["py"]) > 0.5:
            return t, x, y
    return None, None, None


def main():
    base = "artifacts"
    zero = ["game-probe-jD-s1200", "game-probe-jD-s2000"]
    high = ["game-probe-jD-s500", "game-probe-jD-s600", "game-probe-jD-s700"]

    runs = {}
    for r in zero + high:
        p = os.path.join(base, r)
        if os.path.exists(os.path.join(p, "boss-observations.jsonl")):
            runs[r] = load(p)

    print("loaded %d runs" % len(runs))
    print()
    print("=== pairwise first divergence (player position > 0.5 px) ===")
    names = sorted(runs)
    for i, x in enumerate(names):
        for y in names[i + 1:]:
            t, sx, sy = first_divergence(runs[x], runs[y])
            if t is None:
                print("  %-22s vs %-22s : NO divergence found (identical)" % (x, y))
            else:
                print("  %-22s vs %-22s : diverge at tick %5d  "
                      "(px %.1f vs %.1f, boss state %s vs %s)" % (
                          x, y, t, sx["px"], sy["px"], sx["state"], sy["state"]))

    print()
    print("=== boss state timeline, first 1400 ticks: when does each run charge? ===")
    for name in names:
        ser = runs[name]
        ts = sorted(ser)
        charges = [t for a, t in zip(ts, ts[1:])
                   if ser[t]["state"] == 1 and ser[a]["state"] != 1]
        # the tick of the boss's own phase transition is reflected in life; find
        # the first tick where the boss is below half of 78000
        half = [t for t in ts if (ser[t].get("life") or 1) and False]
        print("  %-22s first charges: %s" % (name, charges[:14]))

    print()
    print("=== player x at each run's first 12 charge locks ===")
    for name in names:
        ser = runs[name]
        ts = sorted(ser)
        locks = [t for a, t in zip(ts, ts[1:])
                 if ser[t]["state"] == 1 and ser[a]["state"] != 1][:12]
        xs = ["%.0f" % ser[t]["px"] for t in locks]
        print("  %-22s %s" % (name, xs))


if __name__ == "__main__":
    main()
