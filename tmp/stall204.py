"""Round 204: quantify the controller/engine horizontal divergence.

Finding: the plan commands a full horizontal input (never 0) in every row that has a plan,
yet the realised player vx is 0.0 in a measurable minority of ticks. Break the stalls down
by altitude, by floor contact, and by fight phase.
"""
import io, json, sys

TILE = 16.0
FLOOR_Y = 7958.0


def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]


def analyse(tag):
    obs = load(tag)
    rows = []
    for o in obs:
        p = o.get('player') or {}
        v = (p.get('velocity') or {}).get('x')
        y = (p.get('position') or {}).get('y')
        if v is None or y is None:
            continue
        rows.append((o.get('tick'), v, y, o.get('plan') or {}, p.get('wingTime')))
    # only rows that carry a plan (i.e. after takeover)
    planned = [r for r in rows if r[3]]
    stalls = [r for r in planned if abs(r[1]) < 0.5]
    floor = [r for r in stalls if abs(r[2] - FLOOR_Y) < 8]
    noinput = [r for r in stalls if r[3].get('horizontal') == 0]
    print('%-12s planned=%-6d stalls=%-5d (%5.1f%%)  stalls_on_floor=%-4d  stalls_with_horiz0=%d'
          % (tag, len(planned), len(stalls),
             100.0 * len(stalls) / max(len(planned), 1), len(floor), len(noinput)))
    return rows, planned, stalls


def phase_profile(tag):
    obs = load(tag)
    rows = [(o.get('tick'), ((o.get('player') or {}).get('velocity') or {}).get('x'),
             o.get('plan') or {}) for o in obs]
    rows = [r for r in rows if r[2] and r[1] is not None]
    if not rows:
        return
    span = rows[-1][0] or 1
    print('  %s stalls by decile of fight length:' % tag)
    for d in range(10):
        lo, hi = span * d / 10.0, span * (d + 1) / 10.0
        seg = [r for r in rows if lo <= r[0] < hi]
        st = [r for r in seg if abs(r[1]) < 0.5]
        if seg:
            print('    %2d0-%2d0%%: %4d / %4d = %5.1f%%' % (
                d, d + 1, len(st), len(seg), 100.0 * len(st) / len(seg)))


if __name__ == '__main__':
    print('=== controller/engine horizontal divergence, plan-carrying rows only ===')
    for tag in ('kI4-s700', 'kI4-s750', 'kI4-s800', 'kI4-s900',
                'kJ4-s1100', 'kA3-s300', 'kA3-s550', 'kJ4-s1600', 'kJ4-s2000'):
        try:
            analyse(tag)
        except IOError:
            print('%-12s no data' % tag)
    print()
    for tag in ('kI4-s900', 'kA3-s300'):
        phase_profile(tag)
