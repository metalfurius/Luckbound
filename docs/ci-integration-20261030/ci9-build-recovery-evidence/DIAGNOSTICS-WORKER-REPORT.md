# CI9 build diagnostics candidate — 2026-10-31

## Scope and contract

Prepared against `docs/ci-integration-20261030/SOL-CI9-BUILD-DIAGNOSTIC-CONTRACT.md`, SHA256 `8b78d0cbc73ce7f5c2a3861f86b36526557260a5974e47bd7a2a645f71451853` (verified before implementation).

The candidate contains only the helper and its unit tests at the two logical paths assigned to this worker. Both files are new and remain under this candidate directory. No live `Tools/Automation` path, workflow, Unity source, project setting, package, or build destination was edited. No commit was created.

## Candidate diff

| Logical destination for Root review | Candidate file | SHA256 |
| --- | --- | --- |
| `Tools/Automation/capture_ci_build_diagnostics.py` | `Tools/Automation/capture_ci_build_diagnostics.py` | `fe719c46bc2f408acdfaF205acb7658ed4266f76d0385b9ce74b8ed661baf3db` |
| `Tools/Automation/tests/test_capture_ci_build_diagnostics.py` | `Tools/Automation/tests/test_capture_ci_build_diagnostics.py` | `2d4c1066c6bdb3a21ed72b8f86ac2b564c0a152a0b7130f09f98427bd8329cc5` |

The full source diff is readable in those two candidate files. `Logs/` is ignored by the repository's `.gitignore`, so Git does not show these untracked candidate files in ordinary `git diff`/`git status`; they were not force-staged. Root can copy the reviewed logical files into the live paths during integration.

Implementation points:

- Public CLI is `start` and `stop`; `start` validates the new output directory under `RUNNER_TEMP`, the run/attempt/platform suffix, identity, and bounded duration. It writes S0 and waits for the detached observer's ready marker before returning.
- The private watcher observes until a stop marker or deadline. `stop` waits at most 15 seconds and records a bounded collector error if shutdown does not complete. No PID is stored or targeted. The only process shut down is the Docker events client this watcher started, through its own process handle.
- Host memory, swap, workspace disk, and cgroup v1/v2 values include explicit `Unavailable` states. Counter deltas include a scope ID; v1 `memory.failcnt` is labeled as a counter, not proof of a kill.
- Docker access is read-only and pinned to the local Unix socket or local Windows named pipe. Event format and `inspect` output use allowlisted fields only. Container IDs and event time correlate records; image names, labels, process arguments, and raw inspect output are not recorded.
- `sudo -n dmesg --time-format raw` is bounded, then Python stores only timestamps and fixed OOM/kill categories. A successful no-match result is distinct from an unavailable/error result.
- Records are versioned JSONL with UTC and monotonic times. Sampling is at most 1 Hz. Duration, command time, event/inspect counts, record count, record size, and total output bytes are bounded. A collection-limit record is emitted when possible, with slots reserved for S1.
- Every helper subprocess sets `CREATE_NO_WINDOW` on Windows. The test verifies the guard; tests do not launch helper processes.

## Verification

Final command on 2026-10-31, Python 3.14.7 on Windows:

```text
python -B -m unittest discover -s Logs/CI9BuildDiagnostics-20261031-r1/candidate/Tools/Automation/tests -p test_capture_ci_build_diagnostics.py -v
Ran 17 tests in 0.093s
OK
```

All 17 tests passed. The fixtures cover cgroup v1/v2 metrics and deltas, missing and denied reads, scope changes, Docker inspect allowlisting and disappeared containers, filtered dmesg/no-match/error handling, output and record bounds, start readiness, stop marker and timeout, deadline exit, and secret contamination in environment/argv/output.

Docker, `sudo`, Unity, builders, network, and the actual detached helper process were not run. Docker, proc/cgroup, and `sudo dmesg` are simulated by fixtures and injected runners. One earlier test pass exposed that the host checkout is Windows while Linux cgroup/dmesg parsers require Linux semantics; tests now set the module platform to Linux only around those fixture cases. The final pass is 17/17.

## Limits and integration handoff

- This candidate has not been bound to the workflow or exercised in CI. Root owns those changes and should review CLI paths, Windows shell quoting, and `always()` wiring before integration.
- A Windows host has no Linux cgroup or dmesg source; those fields report `Unavailable`, while Windows host memory uses `GlobalMemoryStatusEx`.
- Docker daemon absence/denial, an exited/disappeared container, and inaccessible cgroup files remain explicit `Unavailable` observations. No OOM/GLX cause is inferred from exit code 137 or adjacency.
- The unit run verifies local behavior with synthetic data only. It does not verify real CI permissions, daemon availability, Windows process lifetime, build outcomes, or artifact upload.
- The code has not received Root/Sol review and is not an integration or acceptance claim.
