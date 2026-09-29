"""Round 197b: exactly where does the boss's OWN ai[0] state sequence differ? shield-events records it."""
import io, json

def rows(tag):
    p = 'artifacts/game-probe-%s/shield-events.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

A, B = rows('kB4-s450'), rows('kB4-s500')
print('rows A=%d B=%d' % (len(A), len(B)))
print('%-6s | %-40s | %-40s' % ('row', 'A dps450', 'B dps500'))
for i in range(0, min(len(A), len(B))):
    a, b = A[i], B[i]
    sa = 't=%-6s ai0=%-4s cont=%-5s dash=%-5s eoc=%-3s' % (
        a.get('tick'), a.get('bossAi0'), a.get('contact'), a.get('dashDelay'), a.get('eocDash'))
    sb = 't=%-6s ai0=%-4s cont=%-5s dash=%-5s eoc=%-3s' % (
        b.get('tick'), b.get('bossAi0'), b.get('contact'), b.get('dashDelay'), b.get('eocDash'))
    mark = '  <<<' if (a.get('tick') != b.get('tick') or a.get('bossAi0') != b.get('bossAi0')) else ''
    print('%-6d | %-40s | %-40s%s' % (i, sa, sb, mark))
    if i > 62:
        break
