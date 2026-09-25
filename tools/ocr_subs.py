import io, re

src = io.open(r"D:\personal tasks\modding\Chaite\tmp\ocr_all.txt", encoding="utf-8").read()
out = io.open(r"D:\personal tasks\modding\Chaite\tmp\ocr_subs.txt", "w", encoding="utf-8")
ts = None
for line in src.splitlines():
    m = re.match(r"=== f(\d+)\.jpg\s+t=(\d+)s ===", line)
    if m:
        ts = int(m.group(2))
        continue
    m2 = re.match(r"\s*(\d+)\s+(\d+)\s+(.*)$", line)
    if not m2 or ts is None:
        continue
    x, y, text = float(m2.group(1)), float(m2.group(2)), m2.group(3)
    # Subtitle band: below the hotbar, and not the bottom-right HP text.
    if y >= 780 and not (x > 850 and y > 980):
        out.write("t=%4ds  y=%4.0f  %s\n" % (ts, y, text))
out.close()
print("done")
