import sys, io
from rapidocr_onnxruntime import RapidOCR

engine = RapidOCR()
path = sys.argv[1]
out = sys.argv[2]
result, _ = engine(path)
lines = []
if not result:
    lines.append("NO TEXT DETECTED")
else:
    for box, text, score in result:
        xs = [float(p[0]) for p in box]
        ys = [float(p[1]) for p in box]
        lines.append("%6.0f %6.0f  %s" % (min(xs), min(ys), text))
with io.open(out, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines))
print("wrote %d lines to %s" % (len(lines), out))
