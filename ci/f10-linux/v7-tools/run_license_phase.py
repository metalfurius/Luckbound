#!/usr/bin/env python3
"""Bound a pinned GameCI licensing phase in the same ephemeral Linux container.

Raw output and the non-secret fallback-method marker remain in private /tmp.
This helper never prints credentials or licensing output.
"""
from pathlib import Path
import os
import signal
import subprocess
import sys
import time

SHELL = r'''
task_phase=$1
task_state=$2
export STEPS_DIR=$3
if [[ "$task_phase" == activate ]]; then
  task_save_method() {
    task_exit=$?
    trap - EXIT
    case "${GAME_CI_ACTIVATED_VIA:-}" in
      ''|personal) (umask 077; set -o noclobber; printf '%s\n' "${GAME_CI_ACTIVATED_VIA:-}" > "$task_state") || task_exit=92 ;;
      *) task_exit=92 ;;
    esac
    exit "$task_exit"
  }
  trap task_save_method EXIT
  source "$STEPS_DIR/activate.sh"
  [[ "${UNITY_EXIT_CODE:-1}" == 0 ]]
else
  if [[ -f "$task_state" ]]; then
    GAME_CI_ACTIVATED_VIA=$(cat -- "$task_state")
    case "$GAME_CI_ACTIVATED_VIA" in ''|personal) export GAME_CI_ACTIVATED_VIA ;; *) exit 92 ;; esac
  fi
  source "$STEPS_DIR/return_license.sh"
  [[ -z "${RETURN_STRATEGY:-}" || "${RETURN_EXIT_CODE:-1}" == 0 ]]
fi
'''


def main():
    if sys.platform != 'linux' or len(sys.argv) != 5:
        print('Licensing phases require the dedicated Linux container.', file=sys.stderr)
        return 90
    expected_context = {'GITHUB_ACTIONS': 'true', 'RUNNER_ENVIRONMENT': 'github-hosted',
                        'GITHUB_WORKFLOW': 'F10 V6 Linux Diagnostic',
                        'GITHUB_JOB': 'f10-v6-linux-acceptance'}
    if any(os.environ.get(key) != value for key, value in expected_context.items()):
        return 90
    phase, private_arg, steps_arg, seconds_arg = sys.argv[1:]
    if phase not in ('activate', 'return'):
        return 90
    private = Path(private_arg)
    steps = Path(steps_arg)
    seconds = int(seconds_arg)
    if not 1 <= seconds <= 900 or not private.is_absolute() or not steps.is_absolute():
        return 90
    if private.is_symlink() or not private.is_dir() or private.resolve() != private or not str(private).startswith('/tmp/luckbound-license.'):
        return 90
    if private.stat().st_mode & 0o077:
        return 90
    if steps.resolve() != steps or not (steps / 'activate.sh').is_file() or not (steps / 'return_license.sh').is_file():
        return 90
    state = private / 'activated-via.txt'
    if state.is_symlink() or (phase == 'activate' and state.exists()):
        return 90
    had_state = state.is_file()
    child = None
    spawning = False
    pending_signal = None

    def unconfirmed_group():
        marker = private / 'process-group-unconfirmed'
        try:
            if not marker.exists():
                with marker.open('x', encoding='utf-8') as stream:
                    stream.write('Previous licensing process group did not confirm termination.\n')
        except OSError:
            print('Process cleanup remains unconfirmed; its private marker could not be written.', file=sys.stderr)
        return False

    def stop_group():
        if child is None:
            return True
        try:
            os.killpg(child.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        except PermissionError:
            return unconfirmed_group()
        try:
            child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            pass
        # A shell can exit before a child which ignored TERM. Kill the group
        # even when the shell has exited, before allowing license return.
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        except PermissionError:
            return unconfirmed_group()
        try:
            child.wait(timeout=5)
        except subprocess.TimeoutExpired:
            return unconfirmed_group()
        deadline = time.monotonic() + 5
        while True:
            try:
                os.killpg(child.pid, 0)
            except ProcessLookupError:
                return True
            except PermissionError:
                return unconfirmed_group()
            if time.monotonic() >= deadline:
                return unconfirmed_group()
            time.sleep(0.05)

    def interrupted(signum, _frame):
        nonlocal pending_signal
        if spawning:
            pending_signal = pending_signal or signum
            return
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        stopped = stop_group()
        raise SystemExit(128 + signum if stopped else 93)

    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    log = private / (phase + '.log')
    try:
        with log.open('xb') as stream:
            spawning = True
            try:
                child = subprocess.Popen(['/bin/bash', '-c', SHELL, '--', phase, str(state), str(steps)],
                                         stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
            finally:
                spawning = False
                if pending_signal is not None:
                    interrupted(pending_signal, None)
            try:
                code = child.wait(timeout=seconds)
            except subprocess.TimeoutExpired:
                stopped = stop_group()
                print('Unity licensing phase timed out; its process group was stopped.', file=sys.stderr)
                return 124 if stopped else 93
        if code != 0:
            if not stop_group():
                return 93
        if phase == 'return' and not had_state:
            # An interrupted activation might have acquired a seat without
            # recording its strategy. Never claim cleanup was confirmed.
            print('Activation method was not recorded; license cleanup remains unconfirmed.', file=sys.stderr)
            return 92
        return code
    except OSError:
        try:
            stopped = stop_group()
        except OSError:
            stopped = unconfirmed_group()
        if not stopped:
            return 93
        print('Unity licensing phase failed; private output was not published.', file=sys.stderr)
        return 92


if __name__ == '__main__':
    raise SystemExit(main())
