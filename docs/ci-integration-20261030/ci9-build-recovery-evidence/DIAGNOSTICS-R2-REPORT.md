# CI9 build diagnostics candidate R2 — 2026-10-31

## Base and scope

R2 is a new candidate based on the frozen R1 files, addressing only the four P2 findings. R1 remains unchanged. The public `start`/`stop` CLI, workflow glue, output identity, and dependency footprint are unchanged. The full R1-to-R2 source and test delta is in `DIFF-R1.patch`.

R2 contains the two assigned logical files under this candidate directory, plus this report, the manifest, and the R1 comparison patch. No live `Tools/Automation` path, workflow, Unity source, project setting, build destination, or other file was changed. No Git add or commit was performed.

## P2 corrections

1. **Create/start cgroup baseline:** a create event with PID 0 no longer freezes an unavailable baseline. Tracking keeps no baseline until the first available cgroup sample, so a later start event can establish the first usable scope. Regression `test_create_unavailable_cgroup_baseline_is_replaced_by_start` verifies a subsequent sample produces a same-scope delta.
2. **Die event cleanup and PID scope:** tracking is removed for every die event, including inspect failure and inspect-limit cases. A final cgroup sample is accepted only when its scope matches the tracked baseline; a changed scope is `Unavailable` with `pid_scope_changed` and no metrics are attributed to the prior container ID. Regressions cover disappeared inspect and changed PID scope.
3. **Bounded shutdown drain:** the loop consumes one event at a time, so a stop cannot strand a locally drained batch. Shutdown closes and joins the owned Docker event reader, then drains the entire bounded queue (at most 512 queued items). It records all OOM/kill events and the latest lifecycle state per container first, without issuing an inspect per queued event. Coalesced, malformed, queue-dropped, over-budget, and unwritten events are counted in the S1 summary and final record. Shutdown event facts explicitly mark inspect as unavailable because the per-event inspect was skipped.
4. **Child stdout read error:** `_bounded_command_runner` now returns `failure=read_error`, the allowlisted error type and errno, and empty stdout if its reader raises. The collector reports `Unavailable` even if the child exit code is zero. Regression `test_bounded_command_reader_error_is_unavailable_even_when_child_exits_zero` reproduces this with a mocked `Popen` and `OSError(EIO)`.

## Verification

Final command on 2026-10-31, Python 3.14.7 on Windows:

```text
python -B -m unittest discover -s Logs/CI9BuildDiagnostics-20261031-r2/candidate/Tools/Automation/tests -p test_capture_ci_build_diagnostics.py -v
Ran 21 tests in 0.132s
OK
```

All 21 tests passed. The four added regressions use fixtures and injected runners. No Docker daemon, `sudo`, Unity, builder, network, or real helper process was invoked. The bounded command-reader failure uses a mocked `Popen` that exits zero after its stdout read raises; no subprocess is created.

## Limits and review status

- R2 has not been bound to the workflow or exercised in CI. Root retains workflow ownership and should review the CLI binding during integration.
- Docker access remains read-only and local-only. A missing/denied daemon, disappeared container, inaccessible cgroup, or close/join timeout remains explicitly `Unavailable`; unknown queue loss is flagged as `events_not_captured_unknown`.
- Synthetic tests do not prove real runner permissions, Docker event availability, Windows helper lifetime, build outcomes, artifact upload, or any OOM/GLX cause.
- Sol re-review and Root integration review are pending. This candidate is not an acceptance claim.
