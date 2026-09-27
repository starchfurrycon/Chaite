import io
import json
import urllib.request

# The danmaku say "看不懂规律的去看置顶评论" (if you cannot see the pattern, read the
# pinned comment). Fetch the comment section and pull the top/pinned replies, which
# is where the author most likely wrote the timing rules.
UA = ('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 '
      '(KHTML, like Gecko) Chrome/124.0 Safari/537.36')
HDR = {'User-Agent': UA, 'Referer': 'https://www.bilibili.com/'}

VIDEOS = [
    ('BV1Lj411Q76R', 'A-detail'),
    ('BV1XVwnerECt', 'C-simple'),
    ('BV1hXth6LEoL', 'B-formula'),
    ('BV1Rf3o6EEx7', 'D-nohit'),
]


def get_json(url):
    req = urllib.request.Request(url, headers=HDR)
    return json.loads(urllib.request.urlopen(req, timeout=30).read().decode('utf-8'))


out = io.open('tmp/bili/comments.txt', 'w', encoding='utf-8')
for bv, tag in VIDEOS:
    out.write('=' * 72 + '\n=== %s (%s)\n' % (bv, tag))
    try:
        aid = get_json('https://api.bilibili.com/x/web-interface/view?bvid=%s' % bv)
        if aid.get('code') != 0:
            out.write('  view api failed\n')
            continue
        oid = aid['data']['aid']
        # The reply API puts the pinned/top comment first when sorted by likes.
        data = get_json('https://api.bilibili.com/x/v2/reply?type=1&oid=%s'
                        '&sort=2&ps=20&pn=1' % oid)
        if data.get('code') != 0:
            out.write('  reply api code %s %s\n' % (data.get('code'), data.get('message')))
            continue
        top = (data.get('data') or {}).get('top') or {}
        upper = top.get('upper')
        if upper:
            out.write('--- PINNED by uploader ---\n')
            out.write('%s\n\n' % upper.get('content', {}).get('message', ''))
        replies = (data.get('data') or {}).get('replies') or []
        out.write('--- top %d comments by likes ---\n' % min(12, len(replies)))
        for r in replies[:12]:
            msg = (r.get('content') or {}).get('message', '')
            like = r.get('like', 0)
            msg = msg.replace('\n', ' | ')
            out.write('[%5d likes] %s\n' % (like, msg[:600]))
    except Exception as exc:
        out.write('  error %s\n' % exc)
    out.write('\n')
out.close()
print('wrote tmp/bili/comments.txt')
