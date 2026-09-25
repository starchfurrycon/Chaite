import io, os, sys, glob
from rapidocr_onnxruntime import RapidOCR

engine = RapidOCR()
frames = sorted(glob.glob(r"D:\personal tasks\modding\Chaite\tmp\frames\f*.jpg"))
out = io.open(r"D:\personal tasks\modding\Chaite\tmp\ocr_all.txt", "w", encoding="utf-8")
for path in frames:
    name = os.path.basename(path)
    try:
        idx = int(name[1:5])
    except Exception:
        continue
    ts = (idx - 1) * 5
    result, _ = engine(path)
    out.write("=== %s  t=%ds ===\n" % (name, ts))
    if result:
        for box, text, score in result:
            ys = [float(p[1]) for p in box]
            xs = [float(p[0]) for p in box]
            out.write("%5.0f %5.0f  %s\n" % (min(xs), min(ys), text))
    else:
        out.write("(none)\n")
    out.flush()
out.close()
print("done")
