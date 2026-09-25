"""Classify sampled video frames as live gameplay or an explanatory diagram, so
the movement pattern can be read from the frames that actually contain it.

The video alternates between fight footage and annotated slides, and the slides
carry the overlay text that was OCR'd earlier. Asking a vision model for a
classification per frame is cheap and is the only way to tell them apart without
being able to look at the frames directly.
"""
import sys
import concurrent.futures as cf
from llm_describe import ask

PROMPT = (
    "One frame from a Terraria guide video. Reply with EXACTLY one word from this "
    "list, nothing else: GAMEPLAY (a live in-game scene with the player's "
    "character visible) or DIAGRAM (an annotated slide, diagram, title card, or "
    "mostly text/graphics)."
)


def classify(path):
    try:
        return path, ask(path, PROMPT, "GLM-4.6V", max_tokens=400).strip().upper()
    except Exception as e:                                    # noqa: BLE001
        return path, "ERROR:" + str(e)[:60]


if __name__ == "__main__":
    paths = sys.argv[1:]
    with cf.ThreadPoolExecutor(max_workers=6) as ex:
        for path, label in ex.map(classify, paths):
            print(f"{path}\t{label}")
