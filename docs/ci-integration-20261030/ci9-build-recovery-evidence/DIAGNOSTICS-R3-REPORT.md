# CI9 build diagnostics candidate R3 — 2026-10-31

## Base and scope

R3 copies the frozen R2 candidate and addresses only its two remaining review findings. R1 and R2 remain unchanged. The public CLI, workflow glue, output identity, dependency footprint, and R2 fixes are unchanged. The complete R2-to-R3 delta is in `DIFF-R2.patch`.

R3 contains the same two logical source paths plus `REPORT.md`, `MANIFEST.json`, and `DIFF-R2.patch`, all under this candidate directory. No live `Tools/Automation` path, workflow, Unity source, settings, or build destination was changed. No Git add or commit was performed.

## Remaining findings addressed

1. **Periodic container PID scope change:** `_sample_record` compares the current cgroup scope with the tracked first available scope. If the same PID now maps to another scope, it emits `cgroup: Unavailable` with `pid_scope_changed`, keeps the delta unavailable with `scope_changed`, marks tracking closed, and removes that container ID from tracking. Regression `test_periodic_sample_marks_pid_scope_change_unavailable_and_closes_tracking` changes the cgroup path for the same PID without sending a die event and verifies the new scope is not attributed to the old container ID.
2. **Stop race after one dequeue:** the main loop checks stop before dequeue. Once it removes its single event, it processes that event before the next stop/deadline check. The final bounded drain then handles events still queued without per-event inspect. Regression `test_stop_racing_after_single_drain_keeps_oom_and_final_die` models stop false at loop entry, dequeues and records OOM, observes stop true after that event, then records the queued DIE. It verifies correct stop reason and zero uncaptured events.

## Verification

Final command on 2026-10-31, Python 3.14.7 on Windows:

```text
python -B -m unittest discover -s Logs/CI9BuildDiagnostics-20261031-r3/candidate/Tools/Automation/tests -p test_capture_ci_build_diagnostics.py -v
Ran 23 tests in 0.204s
OK
```

All 23 tests passed. The two new regressions use cgroup fixtures and a fake event source. Docker, `sudo`, Unity, builders, network, and a real helper process were not invoked.

The first R3 run reported 22/23 because the race fixture expected two calls to the stop predicate. The loop validly checks it a third time to label the stop reason. I changed only that test expectation to match the observed control flow; the source did not change between the failed and final runs. The final regression still asserts both OOM and DIE are recorded, the stop reason is `stop`, and `events_not_captured` is zero.

## Limits and review status

- R3 has not been bound to the workflow or exercised in CI. Root retains workflow ownership and should review integration.
- Tests use synthetic proc/cgroup and Docker event data; they do not establish real runner permissions or Docker availability.
- The candidate is pending Sol re-review and Root integration review; it is not an acceptance claim.
