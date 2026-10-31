"""Audit this exact Native artifact against the accepted Gate D bytes and XML."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

REPO = Path.cwd()
ROOT = Path(__file__).resolve().parent
EXTRACTED = ROOT / 'tests-attempt1/extracted'
COMMIT = '9ad667f5362111bbf0f2e40d8ac23a337ce2d3d1'
RUN = 41211023582
REFERENCE = REPO / ('Logs/CI-41095991782-root/download-attempt1/extracted/Logs/Scenarios/'
                    'V6Capture-93c6ea46c6472d734e1d93fea801861ab2a5902b-41095991782-1')
CAPTURE = EXTRACTED / ('Logs/Scenarios/V6Capture-' + COMMIT + '-' + str(RUN) + '-1')
PORTABLE_REFERENCE = REPO / 'docs/ci-integration-20261029/gate-d-integrated-ci3-evidence'
sha = lambda data: hashlib.sha256(data).hexdigest()
blob = lambda data: hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
write = lambda name, value: (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

manifest_data = (CAPTURE / 'manifest.json').read_bytes()
manifest = json.loads(manifest_data)
assert manifest['commit'] == COMMIT and manifest['unityVersion'] == '6000.6.7f1'
assert manifest['projectVersionDeclared'] == '6000.6.7f1'
assert all(manifest[key] is True for key in ('sourcesStable', 'sessionDocumentStable', 'dataComplete', 'roleRequirementsPassed'))
assert len(manifest['attempts']) == len(manifest['expectedCaseIds']) == 11
assert all(row['complete'] and row['semanticAcceptance'] for row in manifest['attempts'])

accepted_data = json.loads((REPO/'docs/ci-integration-20261030/ci9-build-recovery-runtime-evidence/DATA-COMPARISON.json').read_text(encoding='utf-8'))
comparisons = []
for item in accepted_data['files']:
    path = item['path']
    current, reference = (CAPTURE/path).read_bytes(), (REFERENCE/path).read_bytes()
    assert current == reference, path
    comparisons.append(dict(path=path, bytes=len(current), sha256=sha(current), referenceSha256=sha(reference), identical=True))
assert len(comparisons) == 97
write('DATA-COMPARISON.json', dict(referenceRun=41095991782, currentRun=RUN, count=97, allIdentical=True,
                                  comparison='Direct bytes from current and extracted Gate D files', files=comparisons))

phases = {'codec', 'file-store', 'fallback', 'repository', 'settings-independent'}
def observations(path):
    values = {}
    for case in ET.parse(path).getroot().iter('test-case'):
        for line in (case.findtext('output') or '').splitlines():
            try:
                value = json.loads(line)
            except ValueError:
                continue
            if isinstance(value, dict) and value.get('phase') in phases:
                assert case.get('result') == 'Passed'
                values[json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':'))] = value
    return values
current_xml = EXTRACTED/'test-results/playmode-results.xml'
reference_xml = PORTABLE_REFERENCE/'playmode-results.xml'
current_obs, reference_obs = observations(current_xml), observations(reference_xml)
assert len(current_obs) == len(reference_obs) == 77
assert current_obs.keys() == reference_obs.keys()
write('OBSERVABILITY-VERIFICATION.json', dict(referenceRun=41095991782, currentRun=RUN,
    referenceXmlSha256=sha(reference_xml.read_bytes()), currentXmlSha256=sha(current_xml.read_bytes()),
    count=77, allIdentical=True, phaseGroups=dict(Counter(value['phase'] for value in current_obs.values())),
    observations=list(current_obs.values())))

before = {row['path']: row for row in manifest['sourceHashesBefore']}
after = {row['path']: row for row in manifest['sourceHashesAfter']}
assert before == after and len(before) == 14
git_sources = json.loads((ROOT/'GIT-SOURCES.json').read_text(encoding='utf-8'))
binding = []
for item in git_sources:
    path = item['path']
    native = before[path]
    assert not native['error']
    raw = (REPO/path).read_bytes()
    candidates = [('raw', raw), ('CRLF-to-LF', raw.replace(b'\r\n', b'\n'))]
    matching = [(kind, data) for kind, data in candidates if len(data) == native['bytes']
                and sha(data) == native['sha256'] and blob(data) == item['gitBlobSha1']]
    assert matching, path
    kind, data = matching[0]
    binding.append(dict(path=path, bytes=len(data), sha256=sha(data), gitBlobSha1=blob(data),
                        localRepresentation=kind, nativeBeforeAfterIdentical=True))
assert len(binding) == 14
write('SOURCE-BINDING.json', dict(commit=COMMIT, count=14, allIdentical=True, sources=binding))

raw_log = (ROOT/'test-job.private.txt').read_bytes()
log = raw_log.decode('utf-8', errors='replace')
clean_log = re.sub(r'(?m)^\d{4}-\d{2}-\d{2}T\S+\s?', '', log)
verifiers = []
for marker in re.finditer(r'"outputFileCount"\s*:', clean_log):
    start = clean_log.rfind('{', max(0, marker.start()-3000), marker.start())
    assert start >= 0
    value, _ = json.JSONDecoder().raw_decode(clean_log[start:])
    if isinstance(value, dict) and value.get('commit') == COMMIT:
        verifiers.append(value)
assert len(verifiers) == 1
verifier = verifiers[0]
assert verifier['caseCount'] == verifier['attemptCount'] == 11
assert verifier['outputFileCount'] == 97 and verifier['sourceCount'] == 14
assert verifier['manifestSha256'] == sha(manifest_data)
assert verifier['observedUnityVersion'] == verifier['expectedUnityVersion'] == '6000.6.7f1'
assert verifier['sessionId'] == manifest['sessionId']
assert 'python3 Tools/Automation/verify_v6_capture.py' in log
assert 'UNITY_TEST_OUTCOME: success' in log and 'V6_GATE_OUTCOME: success' in log
write('REAL-GATES.json', dict(testJob=124565845944, unityOutcome='success', v6GateOutcome='success',
    verifier=verifier, rawLogBytes=len(raw_log), rawLogSha256=sha(raw_log), rawLogPublished=False,
    evidence=['python3 Tools/Automation/verify_v6_capture.py', 'UNITY_TEST_OUTCOME: success', 'V6_GATE_OUTCOME: success']))
write('NATIVE-MANIFEST.json', manifest)
print(json.dumps(dict(dataDirectIdentical=97, observationsDirectIdentical=77, sourcesGitAndNative=14,
                       verifierCases=11, verifierAttempts=11, verifierOutputs=97,
                       UnityAndV6Sentinel='success', rawLogPublished=False)))
