"""1.17 T12: the share code (spec-1.17 §C). Reference encoder and decoder, with the examples the spec and mock show.

Code:  "TM" + payload in Crockford base32, shown in groups of 4 after a dash:  TM1-…
Payload bits (big-endian, then zero-padded to a whole base32 character):
  version 5   (the first character, so v1 reads "TM1")
  theme   4   GlyphSetId of the preset (1 medallion, 2 classic, 3 aether-crystal, 4 ishgard-glass, 5 astrologian-orrery, 6 sumi-to-kinpaku)
  palette 4   0 from theme, 1 night, 2 ishgard-snow, 3 dawn, 4 kugane-lacquer, 5 follow-dalamud
  frames  4   0 from theme, 1 brass, 2 silver, 3 came, 4 astrolabe, 5 kirikane
  hc      1
  mix     1   1 when the 8 per-state picks follow
  [8 x 4]     per state, in QuestState order (Ready, Ready on another job, In journal, Blocked, Done this cycle,
              Completed, Locked out, Not checked): 0 from theme, else a GlyphSetId
  crc     8   CRC-8 (poly 0x07, init 0) over every bit before it, packed MSB first; padding bits must be zero
Lengths: a preset-only look is 27 bits = 6 characters (TM1-XXXXX); a mix is 59 bits = 12 characters (TM1-XXXX-XXXX-XXX).
Reading is tolerant: case-insensitive; spaces, dashes and a missing "TM" are ignored; O reads 0 and I/L read 1; U is
never written. Unknown ids (a newer build's sets, palettes or frames) are kept out of the applied look and named.
Run: py -3 -X utf8 sharecode.py
"""
ALPHA = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
SETS = {1: "medallion", 2: "classic", 3: "aether-crystal", 4: "ishgard-glass", 5: "astrologian-orrery", 6: "sumi-to-kinpaku"}
PALETTES = {1: "night", 2: "ishgard-snow", 3: "dawn", 4: "kugane-lacquer", 5: "follow-dalamud"}
FRAMES = {1: "brass", 2: "silver", 3: "came", 4: "astrolabe", 5: "kirikane"}
STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"]

def crc8(bits):
    c = 0
    for b in bits:
        top = (c >> 7) & 1
        c = ((c << 1) & 0xFF) ^ (0x07 if top ^ b else 0)
    return c

def encode(theme, palette=0, frames=0, hc=False, picks=None):
    bits = []
    put = lambda v, n: bits.extend((v >> (n - 1 - i)) & 1 for i in range(n))
    put(1, 5); put(theme, 4); put(palette, 4); put(frames, 4); put(int(hc), 1); put(1 if picks else 0, 1)
    if picks:
        for st in STATES:
            put(picks.get(st, 0), 4)
    put(crc8(bits), 8)
    while len(bits) % 5: bits.append(0)
    chars = "".join(ALPHA[int("".join(map(str, bits[i:i + 5])), 2)] for i in range(0, len(bits), 5))
    body = chars[1:]
    return "TM" + chars[0] + "-" + "-".join(body[i:i + 4] for i in range(0, len(body), 4))

def decode(text):
    t = text.upper().replace("-", "").replace(" ", "").replace("O", "0").replace("I", "1").replace("L", "1")
    if t.startswith("TM"): t = t[2:]
    if not t or any(ch not in ALPHA for ch in t): return {"error": "unreadable"}
    bits = [int(b) for ch in t for b in format(ALPHA.index(ch), "05b")]
    get = lambda i, n: int("".join(map(str, bits[i:i + n])), 2)
    ver = get(0, 5)
    if ver != 1: return {"error": "newer" if ver > 1 else "unreadable", "version": ver}
    n = 19 + (32 if bits[18] else 0)
    if len(bits) < n + 8 or crc8(bits[:n]) != get(n, 8) or any(bits[n + 8:]): return {"error": "checksum"}
    look = {"theme": get(5, 4), "palette": get(9, 4), "frames": get(13, 4), "hc": bool(bits[17]), "picks": {}}
    unknown = []
    if look["theme"] not in SETS: unknown.append(("theme", look["theme"])); look["theme"] = 1
    if look["palette"] and look["palette"] not in PALETTES: unknown.append(("palette", look["palette"])); look["palette"] = 0
    if look["frames"] and look["frames"] not in FRAMES: unknown.append(("frames", look["frames"])); look["frames"] = 0
    if bits[18]:
        for i, st in enumerate(STATES):
            v = get(19 + 4 * i, 4)
            if v and v not in SETS: unknown.append((st, v)); v = 0
            if v: look["picks"][st] = v
    look["unknown"] = unknown
    return look

if __name__ == "__main__":
    ex = {
        "preset Ishgard Glass": encode(4),
        "Medallion, Ready from Aether Crystal, Completed from Aether Crystal": encode(1, picks={"ready": 3, "completed": 3}),
        "Orrery on Dawn, Astrolabe, Ready from Medallion": encode(5, 3, 4, picks={"ready": 1}),
        "Medallion on Kugane Lacquer, high contrast": encode(1, 4, 0, True),
        "from a newer build: Ready from set 9": encode(1, picks={"ready": 9, "completed": 3}),
    }
    for k, v in ex.items():
        print(f"{v:22s} {k}  ->  {decode(v)}")
    print("lowercase, no dashes:", decode(ex["preset Ishgard Glass"].lower().replace("-", "")))
    for code in (ex["preset Ishgard Glass"], ex["Medallion, Ready from Aether Crystal, Completed from Aether Crystal"]):
        flat = code.replace("-", "")
        for i in range(3, len(flat)):
            for ch in "0123456789ABCDEFGHJKMNPQRSTVWXYZ":
                if ch != flat[i]:
                    r = decode(flat[:i] + ch + flat[i + 1:])
                    assert "error" in r, (code, i, ch, r)
    print("every single-character typo in both examples is caught")
