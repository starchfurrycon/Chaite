"""Dump the raw chat-completion response for one image so an empty content field
can be explained rather than guessed at."""
import base64
import json
import sys
import urllib.request

BASE = "https://llmapi.paratera.com"
KEY = "sk-zGNJZhaLpFqpSyCQG56tng"


def raw(path, model, prompt):
    with open(path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    payload = {
        "model": model,
        "messages": [{
            "role": "user",
            "content": [
                {"type": "text", "text": prompt},
                {"type": "image_url",
                 "image_url": {"url": "data:image/png;base64," + b64}},
            ],
        }],
        "max_tokens": 64,
        "temperature": 0.1,
    }
    req = urllib.request.Request(
        BASE + "/v1/chat/completions",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Authorization": "Bearer " + KEY,
                 "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            return r.status, r.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")


if __name__ == "__main__":
    path = sys.argv[1]
    prompt = sys.argv[2]
    for model in sys.argv[3:]:
        status, body = raw(path, model, prompt)
        print("=" * 70)
        print("MODEL:", model, "HTTP", status)
        try:
            j = json.loads(body)
            print("keys:", list(j.keys()))
            ch = j.get("choices", [{}])[0]
            print("finish_reason:", ch.get("finish_reason"))
            msg = ch.get("message", {})
            print("message keys:", list(msg.keys()))
            print("content:", repr(msg.get("content"))[:500])
            if msg.get("reasoning_content"):
                print("reasoning:", repr(msg["reasoning_content"])[:300])
        except Exception:                                     # noqa: BLE001
            print(body[:600])
