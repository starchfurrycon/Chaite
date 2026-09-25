"""List the models the Paratera endpoint advertises, so a vision model can be
picked by name rather than guessed."""
import json
import urllib.request

BASE = "https://llmapi.paratera.com"
KEY = "sk-zGNJZhaLpFqpSyCQG56tng"

req = urllib.request.Request(
    BASE + "/v1/models",
    headers={"Authorization": "Bearer " + KEY,
             "Content-Type": "application/json"})
with urllib.request.urlopen(req, timeout=60) as r:
    body = json.loads(r.read().decode("utf-8"))

ids = sorted(m.get("id", "") for m in body.get("data", []))
print(f"{len(ids)} models")
for i in ids:
    print(" ", i)
