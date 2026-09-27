import io
import re
import struct
import urllib.request

# The segmented danmaku endpoint returns protobuf. The `content` field is a
# length-delimited UTF-8 string, and `progress` is a varint in milliseconds. Parse
# the wire format directly: walk length-delimited fields and keep the ones that
# decode as CJK-or-ASCII text. This avoids pulling in a protobuf dependency and
# does not depend on the schema staying stable.
UA = ('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 '
      '(KHTML, like Gecko) Chrome/124.0 Safari/537.36')
HDR = {'User-Agent': UA, 'Referer': 'https://www.bilibili.com/'}

VIDEOS = [
    ('BV1Lj411Q76R', '1172360923', 'A-detail'),
    ('BV1hXth6LEoL', '41471773878', 'B-formula'),
    ('BV1XVwnerECt', '27915520365', 'C-simple'),
    ('BV1Rf3o6EEx7', '40542341382', 'D-nohit'),
]


def readable(b):
    try:
        s = b.decode('utf-8')
    except UnicodeDecodeError:
        return None
    if not s or len(s) < 2:
        return None
    cjk = sum(1 for c in s if '\u4e00' <= c <= '\u9fff')
    ok = sum(1 for c in s if c.isprintable())
    if ok != len(s):
        return None
    if cjk >= 2 or (cjk == 0 and 3 <= len(s) <= 60 and re.search(r'[A-Za-z0-9]', s)):
        return s
    return None


def walk(buf, depth=0):
    """Yield every readable length-delimited string in the protobuf buffer."""
    i = 0
    n = len(buf)
    while i < n:
        key = 0
        shift = 0
        while i < n:
            b = buf[i]
            i += 1
            key |= (b & 0x7F) << shift
            shift += 7
            if not (b & 0x80):
                break
        if shift > 28:
            return
        wire = key & 7
        if wire == 0:
            while i < n and (buf[i] & 0x80):
                i += 1
            i += 1
        elif wire == 2:
            ln = 0
            shift = 0
            while i < n:
                b = buf[i]
                i += 1
                ln |= (b & 0x7F) << shift
                shift += 7
                if not (b & 0x80):
                    break
            if ln < 0 or i + ln > n:
                return
            chunk = buf[i:i + ln]
            i += ln
            s = readable(chunk)
            if s:
                yield s
            elif depth < 2 and ln > 4:
                for sub in walk(chunk, depth + 1):
                    yield sub
        elif wire == 5:
            i += 4
        elif wire == 1:
            i += 8
        else:
            return


out = io.open('tmp/bili/danmaku.txt', 'w', encoding='utf-8')
for bv, cid, tag in VIDEOS:
    lines = []
    for seg in range(1, 12):
        url = ('https://api.bilibili.com/x/v2/dm/web/seg.so?type=1&oid=%s'
               '&segment_index=%d' % (cid, seg))
        try:
            req = urllib.request.Request(url, headers=HDR)
            raw = urllib.request.urlopen(req, timeout=30).read()
        except Exception as exc:
            if '304' not in str(exc):
                lines.append('  seg %d error %s' % (seg, exc))
            continue
        if not raw:
            break
        for s in walk(raw):
            lines.append(s)
    # Deduplicate while preserving order; drop single characters and noise.
    seen = set()
    clean = []
    for s in lines:
        s = s.strip()
        if len(s) < 3 or s in seen:
            continue
        if not any('\u4e00' <= c <= '\u9fff' for c in s):
            continue
        seen.add(s)
        clean.append(s)
    out.write('=' * 72 + '\n=== %s (%s)   distinct: %d\n' % (bv, tag, len(clean)))
    out.write('=' * 72 + '\n')
    for s in clean:
        out.write('  %s\n' % s)
    out.write('\n')
out.close()
print('wrote tmp/bili/danmaku.txt')
