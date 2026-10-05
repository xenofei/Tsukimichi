"""Writes tools/portrait-pack/portrait_pack_overrides.json, the per-NPC boxes the portrait pack builder uses instead of
its head finder (the portrait audit's change C9), from the audit's files under docs/research/portrait-audit/.

Every pack row of reconciled/reconciled.json is one Garland photo shared by the givers in its npcIds. Its box, in order:

1. The owner's answer (desk/owner-answers.json, key "pack:<photo id>"):
   - "adjust": the owner's own box, normalised over the photo (cx, cy fractions of its width and height, s the side
     over the width): side = s * W, x = cx * W - side / 2, y = cy * H - side / 2, clamped to the photo.
   - "accept": the reconciler's final box (final.curated.photoBox).
2. No answer: the reconciler's final box, when it changed the box (final.action "change") and the face-centring
   supervisor passed it (reconciled/supervisor/verdicts.json). Otherwise the row gets no override and the builder's
   head finder frames the photo.

A box under 72 px (the builder's MinBox: the 72 px plate never upscales) is held at 72 px with its eye line (0.44 of
the side) and centre where they were, as the reconciler held its own, then clamped to the photo.

Usage, from the repository root: py -3 tools/portrait-pack/make_overrides.py [--check]
--check exits 1 when the file on disk differs from what the audit files give.
"""
import json
import os
import sys
from collections import Counter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
AUDIT = os.path.join(ROOT, 'docs', 'research', 'portrait-audit')
OUT = os.path.join(ROOT, 'tools', 'portrait-pack', 'portrait_pack_overrides.json')
MIN_BOX = 72
EYE_LINE = 0.44


def load(*parts):
    with open(os.path.join(AUDIT, *parts), encoding='utf-8') as f:
        return json.load(f)


def held(x, y, side, w, h):
    """The box at MIN_BOX px at least (eye line and centre kept), then inside the photo, to 0.1 px."""
    if side < MIN_BOX:
        eyes, centre = y + EYE_LINE * side, x + side / 2
        side = MIN_BOX
        x, y = centre - side / 2, eyes - EYE_LINE * side
    side = round(min(side, w, h), 1)
    x = min(max(round(x, 1), 0.0), round(w - side, 1))
    y = min(max(round(y, 1), 0.0), round(h - side, 1))
    return [x, y, side]


def build():
    rows = [r for r in load('reconciled', 'reconciled.json')['rows'] if r['kind'] == 'pack']
    verdicts = load('reconciled', 'supervisor', 'verdicts.json')['verdicts']
    owner = {c['key']: c for c in load('desk', 'owner-answers.json')['corrections'] if c['key'].startswith('pack:')}
    stats = Counter()
    skipped = []
    out = {}
    for row in rows:
        key, photo = row['key'], row['id']
        w, h = row['sourceSize']
        answer = owner.get(key)
        final = row['final']
        if answer is not None and answer['action'] == 'adjust':
            if list(answer['source']) != [w, h]:
                raise SystemExit(f'{key}: the owner saw a {answer["source"]} photo, the audit a {[w, h]} one')
            b = answer['box']
            side = b['s'] * w
            x, y = b['cx'] * w - side / 2, b['cy'] * h - side / 2
            box = held(x, y, side, w, h)
            stats['owner adjust'] += 1
            stats['owner adjust held at 72 px'] += side < MIN_BOX
            stats['owner adjust clamped to the photo'] += (
                side >= MIN_BOX and (x < 0 or y < 0 or x + side > w or y + side > h))
            note = f'owner adjust (Portrait Desk), photo {photo}'
        elif answer is not None and answer['action'] == 'accept':
            box = held(*final['curated']['photoBox'], w, h)
            stats['owner accept'] += 1
            note = f'owner accept: reconciler box, photo {photo}'
        elif final['action'] == 'change' and verdicts.get(key, {}).get('verdict') == 'pass':
            box = held(*final['curated']['photoBox'], w, h)
            stats['reconciler, supervisor pass'] += 1
            note = f'reconciler box, supervisor pass, photo {photo}'
        else:
            why = 'box unchanged by the reconciler' if final['action'] != 'change' else \
                f'supervisor verdict {verdicts.get(key, {}).get("verdict", "none")}'
            skipped.append((photo, row['character'], why))
            continue
        if box != [round(v, 1) for v in final['curated']['photoBox']] and answer is None:
            stats['reconciler box moved into the photo'] += 1
        for npc in row['npcIds']:
            if str(npc) in out:
                raise SystemExit(f'ENpc {npc} is in two pack rows')
            out[str(npc)] = {'photoBox': box, 'note': f'{note}, {row["character"]}'}
    data = {
        'schema': 1,
        'note': 'Per-NPC head boxes for Tsukimichi.DataGen --portrait-pack (portrait audit change C9): ENpc id to '
                'photoBox [x, y, side] in the giver\'s Garland photo, in its own pixels. Written by '
                'tools/portrait-pack/make_overrides.py from docs/research/portrait-audit; edit the audit files or the '
                'script, not this file.',
        'overrides': dict(sorted(out.items(), key=lambda kv: int(kv[0]))),
    }
    return data, stats, skipped


def render(data):
    """The file: one override a line, numbers to 0.1 px without a trailing .0."""
    def num(v):
        return f'{round(float(v), 1):g}'
    lines = ['{', f'  "schema": {data["schema"]},', f'  "note": {json.dumps(data["note"], ensure_ascii=False)},',
             '  "overrides": {']
    items = list(data['overrides'].items())
    for i, (npc, entry) in enumerate(items):
        box = ', '.join(num(v) for v in entry['photoBox'])
        comma = ',' if i < len(items) - 1 else ''
        lines.append(f'    "{npc}": {{ "photoBox": [{box}], "note": {json.dumps(entry["note"], ensure_ascii=False)} }}{comma}')
    lines += ['  }', '}']
    return '\n'.join(lines) + '\n'


def main():
    data, stats, skipped = build()
    text = render(data)
    if '--check' in sys.argv:
        with open(OUT, encoding='utf-8') as f:
            same = f.read() == text
        print('up to date' if same else f'{OUT} is stale: run tools/portrait-pack/make_overrides.py')
        return 0 if same else 1
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    photos = stats['owner adjust'] + stats['owner accept'] + stats['reconciler, supervisor pass']
    print(f'{OUT}: {len(data["overrides"])} NPCs on {photos} photos')
    for k, v in stats.items():
        print(f'  {k}: {v}')
    for photo, who, why in skipped:
        print(f'  no override: photo {photo} ({who}): {why}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
