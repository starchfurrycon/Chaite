"""Round 205c: if there are no sudden stops, what IS the last-decile behaviour?

Hypothesis: the controller reverses direction near every tick, so |vx| hovers around zero
and the player never builds sustained horizontal speed -- precisely the "keep horizontal
speed" violation the owner named, but caused by the CONTROLLER, not by the engine.
"""
import io, json, collections

FLOOR = 7958.0

def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def profile(tag):
    obs = load(tag)
    rows = []
    for o in obs:
        pl = o.get('player') or {}
        v = (pl.get('velocity') or {}).get('x')
        p = o.get('plan') or {}
        if not p or v is None:
            continue
        rows.append((o.get('tick'), v, p.get('horizontal'),
                     (pl.get('position') or {}).get('y'), pl.get('wingTime')))
    if not rows:
        return
    span = rows[-1][0] or 1
    print('=== %s (span %d) ===' % (tag, span))
    print('%-12s %-7s %-9s %-9s %-9s %-9s %s' % (
        'decile', 'rows', '|vx|mean', 'sign flips', 'flips/100t', '|vx|>=8', 'input churn/100t'))
    for d in range(10):
        lo, hi = span * d / 10.0, span * (d + 1) / 10.0
        seg = [r for r in rows if lo <= r[0] < hi]
        if len(seg) < 10:
            continue
        vs = [r[1] for r in seg]
        mean = sum(abs(v) for v in vs) / len(vs)
        flips = sum(1 for i in range(1, len(seg)) if vs[i] * vs[i - 1] < 0)
        hs = [r[2] for r in seg]
        churn = sum(1 for i in range(1, len(hs)) if hs[i] is not None and hs[i - 1] is not None and hs[i] != hs[i - 1])
        fast = sum(1 for v in vs if abs(v) >= 8.0)
        print('%-12s %-7d %-9.2f %-9d %-9.1f %-9d %s' % (
            '%d-%d%%' % (d * 10, (d + 1) * 10), len(seg), mean, flips,
            100.0 * flips / len(seg), fast, '%.1f' % (100.0 * churn / len(seg))))
    print()

for t in ('kI4-s900', 'kA3-s300', 'kJ4-s2000'):
    profile(t)
