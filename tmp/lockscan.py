"""Round 163: is the player's stance at charge lock separable between
charges that hit and charges that miss?

The dash line is closed (a horizontal-only 15-frame dash has essentially zero
displacement budget against a 95 px contact box), so the remaining structural
question is the fixed 58-tick charge cadence: at the moment the boss commits a
charge (the lock), is the player's position/facing in a state that decides the
outcome? If yes, stance preparation is a lever; if no, individual charges are
geometrically unavoidable.

Usage: python tmp/lockscan.py <run-dir> [<run-dir> ...]
"""
import io, json, math, os, sys
from collections import Counter, defaultdict

# Boss AI states: 0 = hover, 1 = charge. Commit sets velocity toward the
# player's centre at 16 (expert 17.00) on the transition into state 1.
CHARGE_STATE = 1


def rows(path):
    with io.open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                yield json.loads(line)


def find_boss(npcs, boss_types):
    for n in npcs or ():
        if n.get("type") in boss_types:
            return n
    return None


def main():
    boss_types = {370}
    for run in sys.argv[1:]:
        path = os.path.join(run, "boss-observations.jsonl")
        if not os.path.exists(path):
            print("%s: no boss-observations.jsonl" % run)
            continue

        series = []
        for r in rows(path):
            p = r["player"]
            b = find_boss(r.get("npcs"), boss_types)
            if b is None:
                continue
            # The state machine is ai[0] (0 = hover, 1 = charge). localAI is a
            # constant (1,0,0,0) for this NPC and carries no state.
            ai = b.get("ai") or b.get("AI") or []
            series.append({
                "tick": r["tick"],
                "px": p["position"]["x"] + p["width"] / 2.0,
                "py": p["position"]["y"] + p["height"] / 2.0,
                "pvx": p["velocity"]["x"],
                "pvy": p["velocity"]["y"],
                "dir": p.get("direction"),
                "eocDash": p.get("eocDash", 0),
                "wing": p.get("wingTime"),
                "bx": b["position"]["x"] + b.get("width", 0) / 2.0,
                "by": b["position"]["y"] + b.get("height", 0) / 2.0,
                "bvx": b["velocity"]["x"],
                "bvy": b["velocity"]["y"],
                "ai0": ai[0] if len(ai) > 0 else 0,
                "ai1": ai[1] if len(ai) > 1 else 0,
            })

        if not series:
            print("%s: boss never observed" % run)
            continue

        # Charge locks = transitions into the charge state.
        locks = []
        for prev, cur in zip(series, series[1:]):
            if cur["ai0"] == CHARGE_STATE and prev["ai0"] != CHARGE_STATE:
                locks.append(cur)

        # Hits, from the hurt rows.
        hurt_path = os.path.join(run, "hurt-observations.jsonl")
        hits = sorted({r.get("tickAfter", r.get("tickBefore")) for r in rows(hurt_path)}) if os.path.exists(hurt_path) else []
        hits = [h for h in hits if h is not None]

        print("=" * 78)
        print("%s" % run)
        print("  ticks=%d  charge locks=%d  hits=%d" % (len(series), len(locks), len(hits)))

        if not locks:
            print("  no locks detected")
            continue

        # Attribute each hit to the most recent lock before it, within one cadence.
        by_lock = defaultdict(list)
        for h in hits:
            prior = [l for l in locks if l["tick"] <= h]
            if prior:
                by_lock[prior[-1]["tick"]].append(h)

        def features(l):
            dx = l["px"] - l["bx"]
            dy = l["py"] - l["by"]
            return dx, dy, math.hypot(dx, dy), l["dir"], l["pvx"], l["pvy"], l["wing"]

        locked = []
        for l in locks:
            dx, dy, dist, d, pvx, pvy, wing = features(l)
            # Facing relative to the boss: does the player face away from it?
            facing_away = (dx * (d or 0)) > 0
            locked.append({
                "tick": l["tick"], "dx": dx, "dy": dy, "dist": dist,
                "dir": d, "pvx": pvx, "pvy": pvy, "wing": wing,
                "faceaway": facing_away, "hits": by_lock.get(l["tick"], []),
            })

        hit_locks = [l for l in locked if l["hits"]]
        clean = [l for l in locked if not l["hits"]]
        print("  locks that produced a hit : %d" % len(hit_locks))
        print("  locks that produced none  : %d" % len(clean))

        def summarize(name, group):
            if not group:
                print("    %-14s (empty)" % name)
                return
            def med(key):
                v = sorted(g[key] for g in group)
                return v[len(v) // 2]
            fa = sum(1 for g in group if g["faceaway"])
            print("    %-14s n=%3d  |dx|med=%7.1f  dy med=%8.1f  dist med=%7.1f  "
                  "wing med=%6.1f  face-away %3d%%" % (
                      name, len(group), med("dx") if False else
                      sorted(abs(g["dx"]) for g in group)[len(group) // 2],
                      med("dy"), med("dist"), med("wing"),
                      round(100.0 * fa / len(group))))

        print("  separability (medians):")
        summarize("HIT locks", hit_locks)
        summarize("CLEAN locks", clean)

        # Distance and vertical offset histograms, to see whether one variable splits.
        for key, label, fmt in (("dist", "distance", "%7.0f"),
                                ("dy", "dy (player-boss)", "%8.0f"),
                                ("dx", "dx (player-boss)", "%8.0f")):
            hv = sorted(g[key] for g in hit_locks)
            cv = sorted(g[key] for g in clean)
            if not hv or not cv:
                continue
            print("    %-18s HIT range [%s .. %s]  CLEAN range [%s .. %s]" % (
                label,
                fmt % hv[0], fmt % hv[-1], fmt % cv[0], fmt % cv[-1]))

        # Facing split, the round's specific hypothesis.
        print("  facing-away at lock: HIT %d/%d   CLEAN %d/%d" % (
            sum(1 for g in hit_locks if g["faceaway"]), len(hit_locks),
            sum(1 for g in clean if g["faceaway"]), len(clean)))
        print("  dir value at lock:   HIT %s   CLEAN %s" % (
            dict(Counter(g["dir"] for g in hit_locks)),
            dict(Counter(g["dir"] for g in clean))))


if __name__ == "__main__":
    main()
