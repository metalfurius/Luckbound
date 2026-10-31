"""Produce a line diff; raw before/after bytes remain bound by FREEZE.json."""
import difflib
import json
from pathlib import Path

root = Path(__file__).resolve().parent / 'composed-r1'
freeze = json.loads((root / 'FREEZE.json').read_text(encoding='utf-8'))
parts = []
for row in freeze['files']:
    path = row['path']
    before = (root / 'before' / path).read_text(encoding='utf-8').splitlines(keepends=True)
    after = (root / 'candidate' / path).read_text(encoding='utf-8').splitlines(keepends=True)
    parts.extend(difflib.unified_diff(before, after, fromfile='a/'+path, tofile='b/'+path))
(root / 'DIFF.patch').write_text(''.join(parts), encoding='utf-8', newline='\n')
print(json.dumps({'files': len(freeze['files']), 'diffBytes': (root/'DIFF.patch').stat().st_size}))
