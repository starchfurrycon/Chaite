"""Round 196c: diff every observable column of the two runs up to the fork at tick 4970."""
import io, json, os

def rows(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    out = []
    for l in io.open(p, encoding='utf-8'):
        if l.strip():
            out.append(json.loads(l))
    return out

A, B = rows('kB4-s450'), rows('kB4-s500')
print('rows A=%d B=%d' % (len(A), len(B)))

def flat(d, pre=''):
    out = {}
    if isinstance(d, dict):
        for k, v in d.items():
            out.update(flat(v, pre + k + '.'))
    elif isinstance(d, list):
        out[pre + 'n'] = len(d)
        for i, v in enumerate(d[:40]):
            out.update(flat(v, pre + str(i) + '.'))
    else:
        out[pre.rstrip('.')] = d
    return out

# walk forward, report the first tick where any flattened key differs
prev = None
for i in range(min(len(A), len(B))):
    fa, fb = flat(A[i]), flat(B[i])
    keys = set(fa) | set(fb)
    diffs = [k for k in keys if fa.get(k) != fb.get(k)]
    if diffs:
        print('FIRST DIFF at row %d (tick=%s)' % (i, A[i].get('tick')))
        print('  %d differing keys' % len(diffs))
        for k in sorted(diffs)[:30]:
            print('    %-52s A=%-24s B=%s' % (k, repr(fa.get(k))[:24], repr(fb.get(k))[:24]))
        break
else:
    print('no difference found in the overlap')
