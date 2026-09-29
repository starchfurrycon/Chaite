"""Round 196b: what is the boss doing at the divergence (tick 4734 -> 5190) in dps450 vs dps500?"""
import io, json, os

def rows(tag, name):
    p = 'artifacts/game-probe-%s/%s.jsonl' % (tag, name)
    out = []
    if not os.path.exists(p):
        return out
    for l in io.open(p, encoding='utf-8'):
        if l.strip():
            out.append(json.loads(l))
    return out

A, B = 'kB4-s450', 'kB4-s500'
for name in ('shield-events', 'boss-observations'):
    ra, rb = rows(A, name), rows(B, name)
    print('=== %s ===' % name)
    print('  %-6s | %-34s | %-34s' % ('row', A, B))
    n = min(len(ra), len(rb))
    lo, hi = (50, 60) if name == 'shield-events' else (4960, 4980)
    for i in range(max(0, lo), min(n, hi)):
        a, b = ra[i], rb[i]
        if name == 'shield-events':
            sa = 't=%-6s ai0=%-4s vx=%-8s vy=%-8s npc=%s' % (
                a.get('tick'), a.get('bossAi0'), round(a.get('bossVx') or 0, 2),
                round(a.get('bossVy') or 0, 2), a.get('bossY') is not None)
            sb = 't=%-6s ai0=%-4s vx=%-8s vy=%-8s npc=%s' % (
                b.get('tick'), b.get('bossAi0'), round(b.get('bossVx') or 0, 2),
                round(b.get('bossVy') or 0, 2), b.get('bossY') is not None)
        else:
            pa = (a.get('player') or {})
            pb = (b.get('player') or {})
            sa = 't=%-6s x=%-9s y=%-9s wt=%-4s nps=%s' % (
                a.get('tick'), round((pa.get('position') or {}).get('x') or 0, 1),
                round((pa.get('position') or {}).get('y') or 0, 1),
                pa.get('wingTime'), a.get('hostileProjectileCount'))
            sb = 't=%-6s x=%-9s y=%-9s wt=%-4s nps=%s' % (
                b.get('tick'), round((pb.get('position') or {}).get('x') or 0, 1),
                round((pb.get('position') or {}).get('y') or 0, 1),
                pb.get('wingTime'), b.get('hostileProjectileCount'))
        print('  %-6d | %-34s | %-34s' % (i, sa, sb))
    print()
