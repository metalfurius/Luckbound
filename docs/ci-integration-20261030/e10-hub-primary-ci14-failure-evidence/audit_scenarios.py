"""Verify the observed C08 reports and exact source/resource bindings."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO = Path.cwd()
COMMIT = '5c4bed77ced5e99c444657e61988dc6819d331ae'
RUN = 41207289640
sha = lambda data: hashlib.sha256(data).hexdigest()
blob = lambda data: hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()

def write(name, value):
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

reports = sorted((ROOT/'tests-attempt1/extracted/Logs/Scenarios/C08').rglob('scenario-report.json'))
assert len(reports) == 3
expected = {'C08_EarlyJumpLanding': 3, 'C08_ObservedSpawnLanding': 3, 'C08_RealKeyboardTwoWaveCombat': 10}
scenarios, manifests = [], []
for path in reports:
    data = path.read_bytes()
    row = json.loads(data)
    scenario = row['scenarioId']
    assert scenario in expected and scenario not in {s['scenario'] for s in scenarios}
    assert row['sourceCommit'] == COMMIT and row['unityVersion'] == '6000.6.7f1'
    assert row['passed'] is True and row['exitCode'] == 0 and not row['errors']
    assert len(row['assertions']) == expected[scenario]
    assert all(item['passed'] is True for item in row['assertions'])
    manifest = path.with_name('source-manifest.sha256').read_bytes()
    assert sha(manifest) == row['sourceHash']
    assert len(manifest.decode('utf-8').splitlines()) == row['sourceFileCount']
    manifests.append(manifest)
    scenarios.append(dict(scenario=scenario, passed=True, assertions=len(row['assertions']),
        allAssertionsPassed=True, sourceCommit=COMMIT, sourceFileCount=row['sourceFileCount'],
        sourceManifestSha256=sha(manifest), reportSha256=sha(data),
        keyEvents=[event for event in row['events'] if event.startswith('C08 input ordering:') or event.startswith('ticks=')]))
assert len(set(manifests)) == 1
native = dict(line.rsplit(':', 1) for line in manifests[0].decode('utf-8').splitlines())
relevant = json.loads((ROOT/'RELEVANT-GIT-SOURCES.json').read_text(encoding='utf-8'))
assert len(relevant) == 20
binding = []
for row in relevant:
    raw = (REPO/row['path']).read_bytes()
    candidates = [('raw',raw),('CRLF-to-LF',raw.replace(b'\r\n',b'\n'))]
    native_matches = [(mode,data) for mode,data in candidates if sha(data) == native.get(row['path'])]
    git_matches = [(mode,data) for mode,data in candidates if blob(data) == row['gitBlobSha1']]
    assert native_matches and git_matches, row['path']
    binding.append(dict(path=row['path'],nativeSha256=native[row['path']],nativeMode=native_matches[0][0],
        localRawSha256=sha(raw),gitBlobSha=row['gitBlobSha1'],gitMode=git_matches[0][0]))
write('C08-SUMMARY.json',dict(run=RUN,scenarios=scenarios,perceptualReview=False))
write('NATIVE-RELEVANT-SOURCE-BINDING.json',dict(commit=COMMIT,nativeManifestSha256=sha(manifests[0]),
    sourceFiles=len(native),count=len(binding),allMatched=True,files=binding))
print(json.dumps(dict(scenarios=[{'id':r['scenario'],'passed':r['passed'],'assertions':r['assertions']} for r in scenarios],
                     boundSourcesAndResources=len(binding),nativeSourceFileCount=len(native))))
