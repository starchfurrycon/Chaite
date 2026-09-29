"""Round 196e: find the FIRST tick where plan.phase differs between dps450 and dps500."""
import io, json
from collections import Counter

def rows(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

A, B = rows('kB4-s450'), rows('kB4-s500')
first = None
for i in range(min(len(A), len(B))):
    pa = (A[i].get('plan') or {}).get('phase')
    pb = (B[i].get('plan') or {}).get('phase')
    if pa != pb:
        first = i
        break
print('first plan.phase divergence at row %d (tick %s)' % (first, A[first].get('tick')))
lo = max(0, first - 8)
for i in range(lo, min(len(A), first + 6)):
    pa = (A[i].get('plan') or {}).get('phase')
    pb = (B[i].get('plan') or {}).get('phase')
    print('  row %-6d tick=%-6s A=%-42s B=%s' % (i, A[i].get('tick'), pa, pb))
print()
print('=== phase histogram over the whole common prefix (rows 0..%d) ===' % first)
ca = Counter((A[i].get('plan') or {}).get('phase') for i in range(first))
cb = Counter((B[i].get('plan') or {}).get('phase') for i in range(first))
for k in sorted(set(ca) | set(cb)):
    print('  %-44s A=%-6d B=%d' % (k, ca.get(k, 0), cb.get(k, 0)))
