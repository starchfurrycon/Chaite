"""Round 205: locate the FIRST frame of each airborne stall and what precedes it.

A stall = plan carries a non-zero horizontal but the realised vx is ~0.
Look at the ticks before each stall onset for a common signature.
"""
import io, json
import collections

def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def onsets(tag):
    obs = load(tag)
    byt = {}
    for o in obs:
        if o.get('tick') is not None:
            byt[o['tick']] = o
    ticks = sorted(byt)
    out = []
    prev_stall = False
    for t in ticks:
        o = byt[t]
        p = o.get('plan') or {}
        pl = o.get('player') or {}
        v = (pl.get('velocity') or {}).get('x')
        w = pl.get('wingTime')
        if not p or v is None:
            prev_stall = False
            continue
        stall = abs(v) < 0.5
        if stall and not prev_stall:
            out.append(t)
        prev_stall = stall
    return byt, out

for tag in ('kI4-s900', 'kA3-s300'):
    byt, ons = onsets(tag)
    print('=== %s: %d stall onsets (airborne, plan-carrying) ===' % (tag, len(ons)))
    print('    first 12 onsets: %s' % ons[:12])
    # signature of the preceding tick
    sig = collections.Counter()
    for t in ons:
        prev = byt.get(t - 1)
        if prev is None:
            continue
        pp = prev.get('plan') or {}
        pv = ((prev.get('player') or {}).get('velocity') or {})
        sig[(pp.get('horizontal'), pp.get('jump'), pp.get('dash'),
             round(pv.get('x') or 0, 1), round(pv.get('y') or 0, 1),
             (prev.get('player') or {}).get('wingTime'))] += 1
    print('    preceding-tick signature (horiz, jump, dash, vx, vy, wingTime) -> count')
    for k, n in sig.most_common(10):
        print('      %s -> %d' % (k, n))
    print()

# Detail on the very first onset of kI4-s900
byt, ons = onsets('kI4-s900')
if ons:
    t0 = ons[0]
    print('=== kI4-s900 first onset t=%d, context ===' % t0)
    print('%-7s %-6s %-8s %-8s %-8s %-7s %-7s %-6s %s' % (
        'tick', 'horiz', 'dash', 'jump', 'vx', 'vy', 'px', 'wingT', 'phase'))
    for t in range(t0 - 6, t0 + 4):
        o = byt.get(t)
        if o is None:
            continue
        p = o.get('plan') or {}
        pl = o.get('player') or {}
        v = pl.get('velocity') or {}
        pos = pl.get('position') or {}
        print('%-7d %-6s %-8s %-8s %-8s %-7s %-7.0f %-6s %s' % (
            t, p.get('horizontal'), p.get('dash'), p.get('jump'),
            round(v.get('x', 0), 2), round(v.get('y', 0), 2),
            pos.get('x', 0), pl.get('wingTime'), p.get('phase')))
