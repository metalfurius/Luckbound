"""Compile reviewed staging sources without starting Unity or modifying Assets."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--stage', action='append', default=[])
parser.add_argument('--assembly', action='append')
parser.add_argument('--override', action='append', default=[])
parser.add_argument('--reference-dir')
args = parser.parse_args()
root = Path.cwd().resolve()
out = root / args.output
out.mkdir(exist_ok=False, parents=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
source_root = root / 'Assets/Luckbound'
live = {p.relative_to(root).as_posix(): p for p in source_root.rglob('*.cs')}
guard = {p: sha(actual) for p, actual in live.items()}
files = dict(live)
for stage_arg in args.stage:
    candidate = (root / stage_arg / 'candidate').resolve()
    assert candidate.is_relative_to(root / 'Logs'), candidate
    assert candidate.is_dir(), 'Missing staging directory: ' + str(candidate)
    staged_sources = list(candidate.rglob('*.cs'))
    assert staged_sources, 'Empty staging: ' + str(candidate)
    for actual in staged_sources:
        logical = actual.relative_to(candidate).as_posix()
        assert logical.startswith('Assets/Luckbound/'), logical
        assert logical not in live, 'Only additive F09 staging is permitted: ' + logical
        assert logical not in files, 'Duplicate staging owner: ' + logical
        files[logical] = actual
test_overrides = []
for override_arg in args.override:
    logical, relative_actual = override_arg.split('=', 1)
    actual = (root / relative_actual).resolve()
    assert logical in {'Assets/Luckbound/Scripts/App/HubPreparationController.cs', 'Assets/Luckbound/Tests/PlayMode/HubPreparationLocalizationTests.cs', 'Assets/Luckbound/Tests/PlayMode/SaveLegacyReadOnlyApplicationTests.cs'} and logical in live, logical
    assert logical not in test_overrides and actual.is_relative_to(root / 'Logs'), logical
    assert actual.is_file() and actual.suffix == '.cs', actual
    files[logical] = actual
    test_overrides.append(logical)

assembly_dirs = {}
for definition in source_root.rglob('*.asmdef'):
    name = json.loads(definition.read_text(encoding='utf-8-sig'))['name']
    assembly_dirs[definition.parent.relative_to(root).as_posix()] = name

def owner(logical):
    directory = Path(logical).parent
    while directory != Path('.'):
        key = directory.as_posix()
        if key in assembly_dirs:
            return assembly_dirs[key]
        directory = directory.parent
    raise AssertionError('No assembly for ' + logical)

order = ['Luckbound.Core', 'Luckbound.Content', 'Luckbound.Core.Tests', 'Luckbound.Content.Route.Tests',
         'Luckbound.Gameplay', 'Luckbound.App', 'Luckbound.PlayMode.Tests']
if args.assembly:
    assert set(args.assembly).issubset(order)
    order = [name for name in order if name in args.assembly]
core_baseline = root / 'Logs/FullIntegrationFix-20261030-r5/gate-c-graph-offline-r1'
app_baseline = root / 'Logs/UnityCombinedCandidate-20261030-r1/combined/offline-r1'
compiled = {}
if args.reference_dir:
    reference_dir = (root / args.reference_dir).resolve()
    assert reference_dir.is_relative_to(root / 'Logs') and reference_dir.is_dir()
    for ready_ref in reference_dir.glob('Luckbound.*.ref.dll'):
        compiled[ready_ref.name.removesuffix('.ref.dll')] = ready_ref
results = []
for name in order:
    selected = sorted((logical, actual) for logical, actual in files.items() if owner(logical) == name)
    rows = [dict(path=logical, actual=actual.as_posix(), sha256=sha(actual))
            for logical, actual in selected]
    baseline = app_baseline if name in ['Luckbound.App', 'Luckbound.PlayMode.Tests'] else core_baseline
    original = (baseline / (name + '.rsp')).read_text(encoding='utf-8')
    lines = []
    out_counts = {'out': 0, 'refout': 0}
    for line in original.splitlines():
        if re.fullmatch(r'"[^"\r\n]*\.cs"', line):
            continue
        match = re.match(r'^[-/](out|refout):', line)
        if match:
            switch = match.group(1)
            out_counts[switch] += 1
            suffix = '.dll' if switch == 'out' else '.ref.dll'
            line = '-' + switch + ':"' + (out / (name + suffix)).as_posix() + '"'
        match = re.fullmatch(r'[-/]r:"([^"]+)"', line)
        if match:
            ref_name = Path(match.group(1).replace('\\', '/')).name
            for ready_name, ready_ref in compiled.items():
                if ref_name in [ready_name + '.dll', ready_name + '.ref.dll']:
                    line = '-r:"' + ready_ref.as_posix() + '"'
                    break
        lines.append(line)
    assert out_counts == {'out': 1, 'refout': 1}, (name, out_counts)
    lines.extend('"' + row['actual'] + '"' for row in rows)
    rsp = out / (name + '.rsp')
    rsp.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    process = subprocess.run(
        ['C:/Program Files/dotnet/dotnet.exe',
         'C:/Program Files/dotnet/sdk/9.0.306/Roslyn/bincore/csc.dll', '@' + str(rsp)],
        capture_output=True, text=True,
        creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    log = out / (name + '.log')
    log.write_text(process.stdout + process.stderr, encoding='utf-8')
    for row in rows:
        assert sha(Path(row['actual'])) == row['sha256'], 'Input changed: ' + row['path']
    results.append(dict(assembly=name, sourceCount=len(rows), exitCode=process.returncode,
                        responseSha256=sha(rsp), logSha256=sha(log), inputs=rows))
    if process.returncode:
        print(process.stdout + process.stderr)
        break
    compiled[name] = out / (name + '.ref.dll')

assert guard == {p.relative_to(root).as_posix(): sha(p) for p in source_root.rglob('*.cs')}
report = dict(unityExecuted=False, nunitExecuted=False,
              referenceEditor='6000.6.6f1', targetCiEditor='6000.6.7f1',
              guardedLiveSources=len(guard), stageRoots=args.stage,
              testOverrides=test_overrides, referenceDirectory=args.reference_dir, results=results)
(out / 'RESULTS.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps([{k: v for k, v in row.items() if k != 'inputs'} for row in results]))
raise SystemExit(0 if len(results) == len(order) and all(row['exitCode'] == 0 for row in results) else 1)
