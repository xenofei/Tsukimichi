# Share-code test vectors (feature plan v7 T12; docs/design/v7/ui/spec-1.17.md section C).
#
# Runs the approved reference encoder and decoder (docs/design/v7/ui/1.17/sharecode.py) over a fixed set of looks and
# pasted texts and writes what it produced to Tsukimichi.Tests/Fixtures/sharecode-vectors.json. ShareCodeTests reads
# that file and asserts that the plugin's ShareCode writes the same codes, character for character, and reads every text
# the same way. The vectors are deterministic (a seeded generator), so rerunning this rewrites the same file.
#
# Covered: every preset look (6 themes x 6 palettes x 6 frames x high contrast), seeded random mixes, the spec's
# examples, ids from a newer build (themes 0 and 7..15, read as 0 so the receiver keeps theirs; palettes, frames and
# picks 7..15), tolerant reading (case, dashes, spaces, a missing "TM", O for 0 and I/L for 1, typographic dashes and the
# minus sign, zero-width characters and no-break spaces, full-width ASCII), a newer format version, version 0, truncated
# and padded codes, characters outside the alphabet, and every single-character typo of the spec's examples.
#
# Usage:
#   py -3 -X utf8 tools/themes/sharecode_vectors.py [path/to/sharecode.py]
import importlib.util
import json
import os
import random
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REFERENCE = os.path.join(ROOT, "docs", "design", "v7", "ui", "1.17", "sharecode.py")
OUT = os.path.join(ROOT, "Tsukimichi.Tests", "Fixtures", "sharecode-vectors.json")


def load(path):
    spec = importlib.util.spec_from_file_location("sharecode", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    ref = load(sys.argv[1] if len(sys.argv) > 1 else REFERENCE)
    states = ref.STATES
    rng = random.Random(1170)

    def picks_dict(picks):
        return {st: v for st, v in zip(states, picks) if v} if picks else None

    looks = []
    for theme in range(1, 7):
        for palette in range(0, 6):
            for frames in range(0, 6):
                for hc in (False, True):
                    looks.append((theme, palette, frames, hc, None))

    # The spec's examples, then seeded mixes over the registered sets (a mix with every pick 0 encodes as no mix).
    looks.append((1, 0, 0, False, [3, 0, 0, 0, 0, 3, 0, 0]))
    looks.append((5, 3, 4, False, [1, 0, 0, 0, 0, 0, 0, 0]))
    looks.append((4, 0, 0, False, [6, 5, 4, 3, 2, 1, 6, 5]))
    for _ in range(300):
        picks = [rng.choice([0, 0, 0, 1, 2, 3, 4, 5, 6]) for _ in states]
        if not any(picks):
            picks[rng.randrange(len(picks))] = rng.randint(1, 6)
        looks.append((rng.randint(1, 6), rng.randint(0, 5), rng.randint(0, 5), rng.random() < 0.5, picks))

    # Ids a newer build might write: they encode, and reading names them.
    newer = [
        (0, 0, 0, False, None),
        (0, 3, 2, False, [3, 0, 0, 0, 0, 0, 0, 0]),
        (9, 0, 0, False, None),
        (15, 2, 1, False, None),
        (1, 6, 0, False, None),
        (1, 15, 0, True, None),
        (1, 0, 6, False, None),
        (4, 0, 15, False, None),
        (1, 0, 0, False, [9, 0, 0, 0, 0, 3, 0, 0]),
        (12, 9, 10, True, [7, 8, 9, 10, 11, 12, 13, 15]),
    ]
    for _ in range(40):
        picks = [rng.choice([0, 1, 3, 4, 7, 8, 11, 15]) for _ in states]
        if not any(picks):
            picks[0] = 9
        newer.append((rng.randint(1, 15), rng.randint(0, 15), rng.randint(0, 15), rng.random() < 0.5, picks))
    looks.extend(newer)

    encode = []
    for theme, palette, frames, hc, picks in looks:
        encode.append({"theme": theme, "palette": palette, "frames": frames, "hc": hc, "picks": picks, "code": ref.encode(theme, palette, frames, hc, picks_dict(picks))})

    texts = []
    codes = [e["code"] for e in encode]
    texts.extend(codes)
    examples = [ref.encode(4), ref.encode(1, 4, 0, True), ref.encode(1, picks={"ready": 3, "completed": 3}), ref.encode(5, 3, 4, picks={"ready": 1})]

    # Tolerant reading.
    for code in codes[::7] + examples:
        texts.append(code.lower())
        texts.append(code.replace("-", ""))
        texts.append(code.replace("-", " "))
        texts.append("  " + code.replace("-", " - ") + "  ")
        texts.append(code[2:])
        texts.append(code.replace("-", "")[2:].lower())
        texts.append(code.replace("0", "O").replace("1", "I"))
        texts.append(code.replace("1", "l").replace("0", "o"))
        texts.append(code + "0")
        texts.append(code + "00")
        texts.append(code + "1")
        texts.append(code[:-1])
        texts.append(code[:-2])

    # Typography a chat client or an input method adds (the coordinator's 1.17 ruling): typographic dashes and the minus
    # sign, zero-width characters, word joiners, byte-order marks and no-break spaces, and full-width ASCII.
    def full_width(text):
        return "".join(chr(ord(c) + 0xFEE0) if "!" <= c <= "~" else c for c in text)

    for code in codes[::41] + examples:
        for dash in "‐‑‒–—−":
            texts.append(code.replace("-", dash))
        texts.append("​" + code.replace("-", "​-‌") + "‍")
        texts.append("﻿" + code.replace("-", "⁠"))
        texts.append(code.replace("-", " ") + " ")
        texts.append(full_width(code))
        texts.append(full_width(code.lower()).replace("－", "　"))
        texts.append(full_width(code[:2]) + code[2:].replace("-", "–"))
    # Look-alikes that stay unreadable: a horizontal bar and a full-width U are not in the code's alphabet.
    texts.extend(["TM1―8003―0", "TM1-8003-Ｕ", "−", "​", " – ", "ＴＭ"])

    # A newer format, version 0, and text that is not a code at all.
    for code in examples:
        flat = code.replace("-", "")
        for ch in "2345Z":
            texts.append("TM" + ch + flat[3:])
        texts.append("TM0" + flat[3:])
    texts.extend(["", " ", "-", "TM", "tm", "TM1", "TM1-", "TMU-8003-0", "TM1-8003-U", "TM1-80!3-0", "TM1-8003-0?", "hello", "TM1-8003-0 TM1-8003-0", "U", "1"])

    # Every single-character typo of the spec's examples (each must fail; the decoder says how).
    for code in examples:
        flat = code.replace("-", "")
        for i in range(2, len(flat)):
            for ch in ref.ALPHA:
                if ch != flat[i]:
                    texts.append(flat[:i] + ch + flat[i + 1:])

    decode = []
    seen = set()
    for text in texts:
        if text in seen:
            continue
        seen.add(text)
        try:
            result = ref.decode(text)
        except IndexError:
            # The reference indexes past the end of a version-1 text shorter than its fixed fields (under 19 bits: one
            # to three characters). Too short to hold its checksum, so the length check's own answer.
            result = {"error": "checksum", "short": True}
        if "error" in result:
            entry = {"text": text, "error": result["error"]}
            if "version" in result:
                entry["version"] = result["version"]
            if result.get("short"):
                entry["referenceRaised"] = True
        else:
            entry = {
                "text": text,
                "theme": result["theme"],
                "palette": result["palette"],
                "frames": result["frames"],
                "hc": result["hc"],
                "picks": [result["picks"].get(st, 0) for st in states],
                "unknown": [[field, value] for field, value in result["unknown"]],
            }
        decode.append(entry)

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"states": states, "encode": encode, "decode": decode}, f, indent=1)
        f.write("\n")
    print(f"{len(encode)} encode and {len(decode)} decode vectors -> {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    main()
