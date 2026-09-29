"""Round 204b: raw tick-by-tick dump around a hit that lands 0 ticks after the lock.
kI4-s900 t=3481 and kA3-s550 t=5146 are the sharpest cases.
"""
import io, json, os

def jl(p):
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()] if os.path.exists(p) else []

def dump(tag, centre, lo=-8, hi=14):
    base = 'artifacts/game-probe-%s/' % tag
    obs = {o['tick']: o for o in jl(base + 'boss-observations.jsonl') if o.get('tick') is not None}
    sh = {s['tick']: s for s in jl(base + 'shield-events.jsonl') if s.get('tick') is not None}
    hu = {h['tickBefore']: h for h in jl(base + 'hurt-observations.jsonl') if h.get('tickBefore') is not None}
    print('=== %s around t=%d ===' % (tag, centre))
    print('%-7s %-5s %-4s %-7s %-8s %-8s %-8s %-8s %-6s %-6s %-5s %s' % (
        'tick', 'ai0', 'ai2', 'ai3', 'boss_vx', 'boss_vy', 'play_vx', 'play_vy', 'px', 'py', 'eoc', 'note'))
    for t in range(centre + lo, centre + hi + 1):
        o = obs.get(t)
        if o is None:
            continue
        npcs = o.get('npcs') or [{}]
        ai = npcs[0].get('ai') or [None, None, None, None]
        bv = npcs[0].get('velocity') or {}
        pl = o.get('player') or {}
        pv = pl.get('velocity') or {}
        pp = pl.get('position') or {}
        s = sh.get(t)
        note = ''
        if t in hu:
            note += 'HIT(type=%s,dmg=%s) ' % (hu[t].get('source', {}).get('type'), hu[t].get('actualReturn'))
        if s:
            if s.get('started'):
                note += 'DASH-START '
            if s.get('requested'):
                note += 'req '
        print('%-7d %-5s %-4s %-7s %-8s %-8s %-8s %-8s %-6.0f %-6.0f %-5s %s' % (
            t, ai[0], ai[2], ai[3], bv.get('x'), bv.get('y'),
            pv.get('x'), pv.get('y'), pp.get('x', 0), pp.get('y', 0),
            s.get('eocDash') if s else '', note))
    print()

dump('kI4-s900', 3481)
dump('kA3-s550', 5146)
