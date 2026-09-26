"""Ask a vision model to read a contact sheet and describe it.

The harness cannot hand images to a subagent (the child route has no image
input), so this calls the vision endpoint directly with an OpenAI-compatible
chat/completions payload carrying a base64 data URL. The reply is written to a
text file so the calling agent can read it with the ordinary file tools.

Usage:
  python tools/vision-read-sheet.py <image.png> <prompt.txt> <out.txt> [model]
"""
import base64
import json
import os
import sys
import urllib.error
import urllib.request

BASE = os.environ.get("CHAITE_VISION_BASE", "https://llmapi.paratera.com")
KEY = os.environ.get("CHAITE_VISION_KEY", "")
DEFAULT_MODEL = os.environ.get("CHAITE_VISION_MODEL", "GLM-4.6V")

if len(sys.argv) < 4:
    print(__doc__)
    raise SystemExit(2)

image_path, prompt_path, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
model = sys.argv[4] if len(sys.argv) > 4 else DEFAULT_MODEL

if not KEY:
    print("CHAITE_VISION_KEY is not set")
    raise SystemExit(2)

with open(image_path, "rb") as handle:
    encoded = base64.b64encode(handle.read()).decode("ascii")
with open(prompt_path, encoding="utf-8") as handle:
    prompt = handle.read()

payload = {
    "model": model,
    "messages": [{
        "role": "user",
        "content": [
            {"type": "text", "text": prompt},
            {"type": "image_url",
             "image_url": {"url": "data:image/png;base64," + encoded}},
        ],
    }],
    "max_tokens": 8000,
    "temperature": 0.1,
}

request = urllib.request.Request(
    BASE.rstrip("/") + "/v1/chat/completions",
    data=json.dumps(payload).encode("utf-8"),
    headers={"Content-Type": "application/json",
             "Authorization": "Bearer " + KEY},
    method="POST",
)
print("POST %s/v1/chat/completions  model=%s  image=%s (%.1f KB)"
      % (BASE.rstrip("/"), model, os.path.basename(image_path),
         os.path.getsize(image_path) / 1024.0))

try:
    with urllib.request.urlopen(request, timeout=600) as response:
        body = json.loads(response.read().decode("utf-8"))
except urllib.error.HTTPError as error:
    detail = error.read().decode("utf-8", "replace")
    print("HTTP %s: %s" % (error.code, detail[:2000]))
    raise SystemExit(1)
except Exception as error:  # noqa: BLE001 - report anything the endpoint does
    print("request failed: %r" % (error,))
    raise SystemExit(1)

try:
    text = body["choices"][0]["message"]["content"]
except (KeyError, IndexError):
    print("unexpected response shape: %s" % json.dumps(body)[:2000])
    raise SystemExit(1)

if isinstance(text, list):
    text = "\n".join(part.get("text", "") for part in text
                     if isinstance(part, dict))

with open(out_path, "w", encoding="utf-8") as handle:
    handle.write(text)
print("wrote %s (%d chars)" % (out_path, len(text)))
