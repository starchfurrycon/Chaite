"""Send one video frame to a vision model and print the description.

This is the step that was impossible before: the agent's own model declares no
image input, so a frame can only be seen by handing it to a separate
vision-capable endpoint. The Paratera gateway is OpenAI-compatible, so the image
goes in as a data URL.
"""
import base64
import json
import sys
import urllib.request

BASE = "https://llmapi.paratera.com"
KEY = "sk-zGNJZhaLpFqpSyCQG56tng"


def ask(image_path, prompt, model="GLM-4.6V", max_tokens=2000):
    with open(image_path, "rb") as f:
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
        "max_tokens": max_tokens,
        "temperature": 0.1,
        "stream": False,
    }
    req = urllib.request.Request(
        BASE + "/v1/chat/completions",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Authorization": "Bearer " + KEY,
                 "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        body = json.loads(r.read().decode("utf-8"))
    msg = body["choices"][0]["message"]
    content = (msg.get("content") or "").strip()
    if content:
        return content
    # Several models here are reasoning models: they spend the token budget on
    # reasoning_content and return an EMPTY content when max_tokens is tight, and
    # the finish reason is then "length". Fall back to the reasoning text so a
    # truncated answer is still usable rather than silently blank.
    return (msg.get("reasoning_content") or "").strip()


if __name__ == "__main__":
    path = sys.argv[1]
    prompt = sys.argv[2] if len(sys.argv) > 2 else (
        "This is a Terraria screenshot. Describe precisely: (1) where the player "
        "character is on screen (describe as a fraction of width/height from the "
        "top-left, e.g. 'x 40%, y 60%'), (2) where Duke Fishron is on screen, "
        "(3) the player's pose - wings out flying, standing, or dashing, (4) any "
        "orange dash trail. Be concrete and brief.")
    model = sys.argv[3] if len(sys.argv) > 3 else "GLM-4.6V"
    print(ask(path, prompt, model))
