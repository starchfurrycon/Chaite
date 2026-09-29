"""Round 196d: compare the exported plan object at the fork (ticks 4960-4980)."""
import io, json

def rows(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

A, B = rows('kB4-s450'), rows('kB4-s500')
print('plan keys:', sorted((A[5000].get('plan') or {}).keys()))
print()
print('%-6s | %-46s | %-46s' % ('tick', 'A dps450', 'B dps500'))
for i in range(4964, 4978):
    pa = A[i].get('plan') or {}
    pb = B[i].get('plan') or {}
    sa = 'h=%-3s j=%-3s d=%-3s dr=%-3s ph=%s' % (
        pa.get('horizontal'), pa.get('jump'), pa.get('dash'), pa.get('drop'), pa.get('phase'))
    sb = 'h=%-3s j=%-3s d=%-3s dr=%-3s ph=%s' % (
        pb.get('horizontal'), pb.get('jump'), pb.get('dash'), pb.get('drop'), pb.get('phase'))
    mark = '  <<<' if sa != sb else ''
    print('%-6s | %-46s | %-46s%s' % (A[i].get('tick'), sa, sb, mark))
