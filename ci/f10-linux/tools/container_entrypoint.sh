#!/usr/bin/env bash
# Candidate for one dedicated, ephemeral GitHub-hosted Ubuntu container.
# Activation, the Python supervisor and all Unity processes share this namespace.
# Do not run this on Fran's desktop. No local activation is authorized by this file.
set -o pipefail

task_bundle="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)" || exit 90
if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ||
      "${GITHUB_ACTIONS:-}" != true || "${RUNNER_ENVIRONMENT:-}" != github-hosted ||
      "${GITHUB_WORKFLOW:-}" != 'F10 V6 Linux Diagnostic' ||
      "${GITHUB_JOB:-}" != f10-v6-linux-acceptance ]]; then
  echo 'Dedicated Linux diagnostic runner context is required.' >&2
  exit 90
fi
# Context variables are guardrails; isolation is established by the reviewed
# hosted-runner workflow and its container mounts, not by these strings alone.
task_python="$task_bundle/acceptance/run_ci_linux_diagnostic.py"
[[ -f "$task_python" && -f "$task_bundle/run_license_phase.py" && -x /opt/unity/Editor/Unity ]] || {
  echo 'The frozen acceptance wrapper or pinned Editor is missing.' >&2
  exit 90
}
command -v python3 >/dev/null || exit 90
export STEPS_DIR="$task_bundle/vendor/game-ci-v0.1.72"
(cd "$STEPS_DIR" && sha256sum --check --status SHA256SUMS) || {
  echo 'Pinned GameCI licensing sources failed checksum verification.' >&2
  exit 90
}
task_private="$(mktemp -d /tmp/luckbound-license.XXXXXXXX)" || exit 90
chmod 700 "$task_private" || exit 90
export ACTIVATE_LICENSE_PATH="$task_private"
export UNITY_EDITOR_PATH=/opt/unity/Editor/Unity
# One identity for this disposable container, shared by activation, both Unity
# processes and return. Never copy a runner/desktop identity into the container.
python3 - "$task_private" <<'PY'
from pathlib import Path
import hashlib
import os
import sys
identity = (os.urandom(16).hex() + '\n').encode('ascii')
primary = Path('/etc/machine-id')
secondary = Path('/var/lib/dbus/machine-id')
secondary.parent.mkdir(parents=True, exist_ok=True)
if primary.is_symlink():
    primary.unlink()
if secondary.is_symlink():
    secondary.unlink()
primary.write_bytes(identity)
secondary.write_bytes(identity)
if primary.read_bytes() != identity or secondary.read_bytes() != identity:
    raise SystemExit('Container machine identity could not be established.')
Path(sys.argv[1], 'machine-id.sha256').write_text(hashlib.sha256(identity).hexdigest(), encoding='ascii')
PY
[[ $? == 0 ]] || exit 90

task_verify_machine_identity() {
  python3 - "$task_private" <<'PY'
from pathlib import Path
import hashlib
import re
import sys
identity = Path('/etc/machine-id').read_bytes()
expected = Path(sys.argv[1], 'machine-id.sha256').read_text(encoding='ascii')
if (not re.fullmatch(rb'[0-9a-f]{32}\n', identity)
        or Path('/var/lib/dbus/machine-id').read_bytes() != identity
        or hashlib.sha256(identity).hexdigest() != expected):
    raise SystemExit('Container machine identity changed; licensing cannot continue.')
PY
}
task_activation_started=0
task_supervisor_pid=
task_launching=0
task_pending_signal=
task_unsafe_process_group=0

task_wait_supervisor() {
  local task_wait_result
  wait "$task_supervisor_pid"
  task_wait_result=$?
  [[ "$task_wait_result" != 93 ]] || task_unsafe_process_group=1
  task_supervisor_pid=
  return "$task_wait_result"
}

task_stop_supervisor() {
  if [[ -n "$task_supervisor_pid" ]]; then
    if kill -0 "$task_supervisor_pid" 2>/dev/null; then
      # Both supervisors handle TERM by stopping and waiting for their Unity
      # process group before exiting. License return must wait for that exit.
      kill -TERM "$task_supervisor_pid" 2>/dev/null || true
    fi
    task_wait_supervisor 2>/dev/null || true
  fi
}

task_signal() {
  local task_interrupted_status=$?
  [[ "$task_interrupted_status" != 93 ]] || task_unsafe_process_group=1
  if [[ "$task_launching" == 1 ]]; then
    [[ -n "$task_pending_signal" ]] || task_pending_signal="$1"
    return
  fi
  task_signal_exit="$1"
  trap '' TERM INT
  task_stop_supervisor
  [[ "$task_unsafe_process_group" == 0 ]] || exit 93
  exit "$task_signal_exit"
}
trap 'task_signal 143' TERM
trap 'task_signal 130' INT

task_start_supervisor() {
  # Bash may run a pending trap after '&' and before '$!' is assigned. Defer
  # termination through that interval so cleanup always owns the created child.
  task_launching=1
  "$@" &
  task_supervisor_pid=$!
  task_launching=0
  if [[ -n "$task_pending_signal" ]]; then
    local task_replay="$task_pending_signal"
    task_pending_signal=
    task_signal "$task_replay"
  fi
}

task_finish() {
  task_result=$?
  trap - EXIT
  task_stop_supervisor
  if [[ "$task_activation_started" == 1 ]]; then
    if [[ "$task_unsafe_process_group" == 1 || -e "$task_private/process-group-unconfirmed" ]]; then
      echo 'A licensing process group did not confirm termination; return was not started concurrently.' >&2
      exit 93
    fi
    task_verify_machine_identity || exit 93
    # The helper carries only the canonical fallback-method marker through a
    # private file; all licensing processes remain in this same container.
    task_start_supervisor python3 "$task_bundle/run_license_phase.py" return "$task_private" "$STEPS_DIR" 180
    task_wait_supervisor
    task_return_result=$?
    [[ "$task_unsafe_process_group" == 0 ]] || exit 93
    if [[ "$task_return_result" != 0 ]]; then
      echo 'Unity license return did not confirm success; diagnostic is failed.' >&2
      [[ "$task_result" != 0 ]] || task_result=91
    fi
  fi
  exit "$task_result"
}
trap task_finish EXIT
task_verify_machine_identity || exit 90
task_activation_started=1
# Bound the upstream activation and its process group. EXIT still attempts
# return for a seat potentially acquired before a later activation error.
task_start_supervisor python3 "$task_bundle/run_license_phase.py" activate "$task_private" "$STEPS_DIR" 600
task_wait_supervisor
task_activation_result=$?
[[ "$task_activation_result" == 0 ]] || exit 91
task_verify_machine_identity || exit 93
echo 'Unity activation succeeded; starting the diagnostic supervisor.'

# The supervisor and game receive no account credentials. The parent retains
# them only for the matching return step after the supervisor has terminated.
task_start_supervisor env -u UNITY_LICENSE -u UNITY_LICENSE_FILE -u UNITY_EMAIL -u UNITY_PASSWORD \
    -u UNITY_SERIAL -u UNITY_LICENSING_SERVER \
    python3 "$task_python" "$@"
task_wait_supervisor
task_supervisor_result=$?
task_verify_machine_identity || exit 93
exit "$task_supervisor_result"
