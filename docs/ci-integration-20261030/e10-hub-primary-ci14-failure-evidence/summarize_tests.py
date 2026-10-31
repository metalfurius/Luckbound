"""Read exact NUnit XML; record failures and diagnostics without a Unity log."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
XML = ROOT / 'tests-attempt1/extracted/test-results'
BASELINE = ROOT.parent / 'CI-41203388580-root/tests-attempt1/extracted/test-results'
RUN = 41207289640
COMMIT = '5c4bed77ced5e99c444657e61988dc6819d331ae'

def write(name, value):
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

assemblies, current, previous = [], {}, {}
for filename in ('editmode-results.xml', 'playmode-results.xml'):
    path = XML / filename
    data = path.read_bytes()
    tree = ET.fromstring(data)
    cases = list(tree.iter('test-case'))
    counts = Counter(case.get('result') for case in cases)
    assert len(cases) == int(tree.get('total')), filename
    assemblies.append(dict(file=filename, sha256=hashlib.sha256(data).hexdigest(),
                           total=len(cases), passed=counts['Passed'], failed=counts['Failed'],
                           skipped=counts['Skipped'], inconclusive=counts['Inconclusive'],
                           results=dict(counts)))
    for case in cases:
        name = case.get('fullname') or case.get('name')
        assert name not in current, name
        current[name] = case
    for case in ET.parse(BASELINE / filename).getroot().iter('test-case'):
        name = case.get('fullname') or case.get('name')
        assert name not in previous, name
        previous[name] = case
assert len(previous) == 1171

failures, primary, metrics = [], [], []
for name, case in current.items():
    row = dict(name=name, result=case.get('result'),
               message=case.findtext('failure/message'), stack=case.findtext('failure/stack-trace'))
    if case.get('result') != 'Passed':
        failures.append(dict(row))
    if '.HubPreparationLocalizationTests.' in name:
        row['output'] = case.findtext('output') or ''
        row['diagnostics'] = []
        for line in row['output'].splitlines():
            marker = re.search(r'\[E10\.HUB\.PRIMARY\.[A-Z.\-]+\]', line)
            if not marker:
                continue
            tail = line[marker.end():].lstrip()
            value, _ = json.JSONDecoder().raw_decode(tail)
            diagnostic = dict(test=name, kind=marker.group(), value=value)
            row['diagnostics'].append(diagnostic)
            metrics.append(diagnostic)
        primary.append(row)
write('PRIMARY-METRICS.json', dict(run=RUN, commit=COMMIT, count=len(metrics), records=metrics))
write('SUMMARY.json', dict(run=RUN, commit=COMMIT, assemblies=assemblies, primary=primary,
    failures=failures, previousCases=len(previous), previousNotPassed=[
        dict(name=name, result=current[name].get('result') if name in current else 'Missing')
        for name in previous if name not in current or current[name].get('result') != 'Passed']))
print(json.dumps(dict(assemblies=assemblies, primary=[{k:v for k,v in row.items()
    if k not in ('output','diagnostics','stack')} for row in primary],
    failures=len(failures), metrics=len(metrics), previousCases=len(previous),
    previousNotPassed=sum(name not in current or current[name].get('result') != 'Passed' for name in previous))))
