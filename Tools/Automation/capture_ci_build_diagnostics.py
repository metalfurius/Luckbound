#!/usr/bin/env python3
"""Bounded, read-only CI build diagnostics for CI9.

The public interface is deliberately limited to ``start`` and ``stop``. The
private ``__watch`` entry point is used only by the detached observer started
by ``start``. Records contain allowlisted fields; command output and exceptions
are never copied into the diagnostic stream.
"""

from __future__ import annotations

import argparse
import ctypes
import datetime as _datetime
import hashlib
import json
import os
import posixpath
import queue
import re
import shutil
import subprocess
import sys
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Iterable, Mapping, Sequence


SCHEMA = "luckbound.ci9.build-diagnostics"
SCHEMA_VERSION = 1
MAX_DURATION_SECONDS = 6 * 60 * 60 + 5 * 60
MAX_OUTPUT_BYTES = 32 * 1024 * 1024
MAX_RECORDS = 25_000
MAX_RECORD_BYTES = 64 * 1024
MAX_CGROUP_FILE_BYTES = 64 * 1024
MAX_COMMAND_OUTPUT_BYTES = 1024 * 1024
MAX_DMESG_MATCHES = 256
MAX_EVENT_LINE_BYTES = 8 * 1024
MAX_QUEUED_EVENTS = 512
MAX_EVENTS = 1024
MAX_INSPECTS = 256
MAX_TRACKED_CONTAINERS = 64
MAX_CONTAINER_SAMPLES_PER_TICK = 8
SAMPLE_INTERVAL_SECONDS = 1.0
START_READY_TIMEOUT_SECONDS = 10.0
STOP_WAIT_TIMEOUT_SECONDS = 15.0
COMMAND_TIMEOUT_SECONDS = 3.0
DOCKER_INSPECT_TIMEOUT_SECONDS = 2.0

_RUN_ID_RE = re.compile(r"^[A-Za-z0-9._-]{1,80}$")
_COMMIT_RE = re.compile(r"^[0-9a-fA-F]{7,64}$")
_CONTAINER_ID_RE = re.compile(r"^[0-9a-f]{12,64}$")
_IMAGE_ID_RE = re.compile(r"^(?:sha256:)?[0-9a-f]{64}$")
_TIME_RE = re.compile(r"^[0-9TZ:+.\-]{1,40}$")
_DMESG_LINE_RE = re.compile(r"^\[\s*(\d+(?:\.\d+)?)\]\s*(.*)$")
_DMESG_PATTERNS: tuple[tuple[str, re.Pattern[str]], ...] = (
    ("oom_kill", re.compile(r"oom[-_ ]kill|invoked oom-killer|out of memory", re.I)),
    ("killed_process", re.compile(r"killed process|kill process", re.I)),
    ("oom_reaper", re.compile(r"oom_reaper", re.I)),
)


def _utc_now() -> str:
    return _datetime.datetime.now(_datetime.timezone.utc).isoformat(timespec="milliseconds")


def _utc_from_unix(value: float) -> str:
    try:
        return _datetime.datetime.fromtimestamp(value, _datetime.timezone.utc).isoformat(timespec="milliseconds")
    except (OverflowError, OSError, ValueError):
        return _utc_now()


def _error_fields(exc: BaseException) -> dict[str, Any]:
    """Return exception metadata only; never include exception text or paths."""
    fields: dict[str, Any] = {"error_type": type(exc).__name__}
    error_number = getattr(exc, "errno", None)
    if isinstance(error_number, int):
        fields["errno"] = error_number
    return fields


@dataclass(frozen=True)
class CommandResult:
    returncode: int | None
    stdout: bytes = b""
    failure: str | None = None
    errno: int | None = None
    truncated: bool = False
    error_type: str | None = None


CommandRunner = Callable[[Sequence[str], float, int], CommandResult]
ProcReader = Callable[[str], bytes | str]


def _windows_creationflags() -> int:
    if os.name != "nt":
        return 0
    flag = getattr(subprocess, "CREATE_NO_WINDOW", None)
    if not isinstance(flag, int) or flag == 0:
        raise RuntimeError("CREATE_NO_WINDOW unavailable")
    return flag


def _detached_session_options() -> dict[str, Any]:
    return {"start_new_session": True} if os.name != "nt" else {}


def _bounded_command_runner(
    argv: Sequence[str], timeout_seconds: float, max_output_bytes: int
) -> CommandResult:
    """Run a fixed, read-only helper command with bounded output and time."""
    process: subprocess.Popen[bytes] | None = None
    result: dict[str, Any] = {}
    completed = threading.Event()
    try:
        process = subprocess.Popen(
            list(argv),
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            shell=False,
            close_fds=True,
            creationflags=_windows_creationflags(),
            **_detached_session_options(),
        )
    except OSError as exc:
        return CommandResult(None, failure="launch_error", errno=getattr(exc, "errno", None))
    except Exception as exc:
        return CommandResult(None, failure="launch_error", errno=getattr(exc, "errno", None))

    assert process.stdout is not None

    def read_bounded() -> None:
        try:
            result["stdout"] = process.stdout.read(max_output_bytes + 1)
        except Exception as exc:  # metadata only, never exposed
            result["read_error"] = _error_fields(exc)
        finally:
            completed.set()

    reader = threading.Thread(target=read_bounded, name="ci9-bounded-reader", daemon=True)
    reader.start()
    deadline = time.monotonic() + max(0.1, timeout_seconds)
    while time.monotonic() < deadline:
        if completed.is_set() and len(result.get("stdout", b"")) > max_output_bytes:
            try:
                process.terminate()
            except Exception:
                pass
            break
        if process.poll() is not None and completed.is_set():
            break
        time.sleep(0.02)

    timed_out = process.poll() is None and not completed.is_set()
    if timed_out or (completed.is_set() and len(result.get("stdout", b"")) > max_output_bytes):
        try:
            process.terminate()
        except Exception:
            pass
    try:
        returncode = process.wait(timeout=1.0)
    except subprocess.TimeoutExpired:
        # This is an owned helper process, never a container or a discovered PID.
        try:
            process.kill()
        except Exception:
            pass
        try:
            returncode = process.wait(timeout=1.0)
        except Exception:
            returncode = None
    reader.join(timeout=1.0)
    try:
        process.stdout.close()
    except Exception:
        pass

    raw = result.get("stdout", b"")
    read_error = result.get("read_error")
    if isinstance(read_error, Mapping):
        error_number = read_error.get("errno")
        error_type = read_error.get("error_type")
        return CommandResult(
            returncode,
            b"",
            "read_error",
            error_number if isinstance(error_number, int) else None,
            False,
            error_type if isinstance(error_type, str) else "OSError",
        )
    if not isinstance(raw, bytes):
        raw = b""
    truncated = len(raw) > max_output_bytes
    return CommandResult(
        returncode,
        raw[:max_output_bytes],
        "timeout" if timed_out else None,
        None,
        truncated,
    )


def _read_proc_file(path: str) -> bytes:
    with open(path, "rb") as handle:
        return handle.read(MAX_CGROUP_FILE_BYTES + 1)


def _decode_bounded(value: bytes | str) -> tuple[str | None, bool]:
    if isinstance(value, str):
        raw = value.encode("utf-8", errors="replace")
    elif isinstance(value, bytes):
        raw = value
    else:
        return None, False
    if len(raw) > MAX_CGROUP_FILE_BYTES:
        return None, True
    return raw.decode("utf-8", errors="replace"), False


def _read_text(path: str, reader: ProcReader) -> tuple[str | None, dict[str, Any] | None]:
    try:
        contents, oversized = _decode_bounded(reader(path))
    except FileNotFoundError as exc:
        return None, {"state": "Unavailable", "reason": "missing", **_error_fields(exc)}
    except PermissionError as exc:
        return None, {"state": "Unavailable", "reason": "denied", **_error_fields(exc)}
    except OSError as exc:
        return None, {"state": "Unavailable", "reason": "read_error", **_error_fields(exc)}
    except Exception as exc:
        return None, {"state": "Unavailable", "reason": "read_error", **_error_fields(exc)}
    if oversized or contents is None:
        return None, {"state": "Unavailable", "reason": "size_limit"}
    return contents, None


def _decode_mount_path(value: str) -> str:
    return re.sub(r"\\([0-7]{3})", lambda match: chr(int(match.group(1), 8)), value)


def _parse_mountinfo(mountinfo: str) -> list[dict[str, Any]]:
    mounts: list[dict[str, Any]] = []
    for line in mountinfo.splitlines():
        left, separator, right = line.partition(" - ")
        if not separator:
            continue
        left_parts = left.split()
        right_parts = right.split()
        if len(left_parts) < 6 or len(right_parts) < 3:
            continue
        fs_type = right_parts[0]
        if fs_type not in ("cgroup", "cgroup2"):
            continue
        root = _decode_mount_path(left_parts[3])
        mountpoint = _decode_mount_path(left_parts[4])
        options = set(right_parts[2].split(",")) | set(left_parts[5].split(","))
        mounts.append({"fs_type": fs_type, "root": root, "mountpoint": mountpoint, "options": options})
    return mounts


def _membership_path(cgroup_text: str, version: int) -> str | None:
    for line in cgroup_text.splitlines():
        fields = line.split(":", 2)
        if len(fields) != 3:
            continue
        controllers = set(fields[1].split(",")) if fields[1] else set()
        if version == 2 and fields[0] == "0" and not fields[1]:
            return fields[2]
        if version == 1 and "memory" in controllers:
            return fields[2]
    return None


def _mount_for_cgroup(mounts: list[dict[str, Any]], version: int) -> dict[str, Any] | None:
    for mount in mounts:
        if version == 2 and mount["fs_type"] == "cgroup2":
            return mount
        if version == 1 and mount["fs_type"] == "cgroup" and "memory" in mount["options"]:
            return mount
    return None


def _safe_cgroup_dir(mountpoint: str, root: str, membership: str) -> str | None:
    root = posixpath.normpath(root)
    membership = posixpath.normpath(membership)
    mountpoint = posixpath.normpath(mountpoint)
    if not root.startswith("/") or not membership.startswith("/") or not mountpoint.startswith("/"):
        return None
    if membership != root and not membership.startswith(root.rstrip("/") + "/"):
        return None
    relative = membership[len(root):].lstrip("/")
    candidate = posixpath.normpath(posixpath.join(mountpoint, relative))
    try:
        if posixpath.commonpath((mountpoint, candidate)) != mountpoint:
            return None
    except ValueError:
        return None
    return candidate


def _metric(state: str = "Unavailable", **fields: Any) -> dict[str, Any]:
    return {"state": state, **fields}


def _read_integer_metric(path: str, reader: ProcReader, allow_max: bool = False) -> dict[str, Any]:
    contents, failure = _read_text(path, reader)
    if failure:
        return failure
    assert contents is not None
    value = contents.strip()
    if allow_max and value == "max":
        return _metric("available", value=None, unlimited=True)
    try:
        number = int(value, 10)
    except (ValueError, OverflowError):
        return _metric("Unavailable", reason="invalid_value")
    if number < 0:
        return _metric("Unavailable", reason="invalid_value")
    return _metric("available", value=number)


def _read_key_value_metrics(path: str, reader: ProcReader) -> tuple[dict[str, Any] | None, dict[str, Any] | None]:
    contents, failure = _read_text(path, reader)
    if failure:
        return None, failure
    assert contents is not None
    values: dict[str, Any] = {}
    for line in contents.splitlines():
        fields = line.split()
        if len(fields) != 2 or not re.fullmatch(r"[A-Za-z0-9_.-]{1,64}", fields[0]):
            continue
        try:
            number = int(fields[1], 10)
        except (ValueError, OverflowError):
            continue
        if number >= 0:
            values[fields[0]] = number
    if not values:
        return None, _metric("Unavailable", reason="invalid_value")
    return values, None


def _cgroup_snapshot(reader: ProcReader = _read_proc_file, membership_file: str = "/proc/self/cgroup") -> dict[str, Any]:
    if not sys.platform.startswith("linux"):
        return {"state": "Unavailable", "reason": "not_linux_host"}

    cgroup_text, failure = _read_text(membership_file, reader)
    if failure:
        return {"state": "Unavailable", "reason": "membership_unavailable", "membership": failure}
    mountinfo, mount_failure = _read_text("/proc/self/mountinfo", reader)
    if mount_failure:
        return {"state": "Unavailable", "reason": "mountinfo_unavailable", "mountinfo": mount_failure}
    assert cgroup_text is not None and mountinfo is not None

    version = 2 if _membership_path(cgroup_text, 2) is not None else 1
    membership = _membership_path(cgroup_text, version)
    if membership is None:
        return {"state": "Unavailable", "reason": "membership_missing"}
    mount = _mount_for_cgroup(_parse_mountinfo(mountinfo), version)
    if mount is None:
        return {"state": "Unavailable", "reason": "mount_missing", "version": version}
    directory = _safe_cgroup_dir(mount["mountpoint"], mount["root"], membership)
    if directory is None:
        return {"state": "Unavailable", "reason": "unsafe_membership", "version": version}

    scope_hash = hashlib.sha256((version.__str__() + ":" + membership).encode("utf-8")).hexdigest()[:16]
    values: dict[str, Any] = {}
    if version == 2:
        for key, filename, allow_max in (
            ("memory.current", "memory.current", False),
            ("memory.peak", "memory.peak", False),
            ("memory.max", "memory.max", True),
        ):
            values[key] = _read_integer_metric(posixpath.join(directory, filename), reader, allow_max)
        for key, filename in (("memory.events", "memory.events"), ("memory.events.local", "memory.events.local")):
            parsed, read_failure = _read_key_value_metrics(posixpath.join(directory, filename), reader)
            if read_failure:
                values[key] = read_failure
            else:
                values[key] = _metric("available", counters=parsed)
    else:
        for key, filename in (
            ("memory.limit_in_bytes", "memory.limit_in_bytes"),
            ("memory.usage_in_bytes", "memory.usage_in_bytes"),
            ("memory.max_usage_in_bytes", "memory.max_usage_in_bytes"),
            ("memory.failcnt", "memory.failcnt"),
        ):
            values[key] = _read_integer_metric(posixpath.join(directory, filename), reader)
        parsed, read_failure = _read_key_value_metrics(posixpath.join(directory, "memory.oom_control"), reader)
        values["memory.oom_control"] = read_failure or _metric("available", counters=parsed)

    available_fields = sum(1 for value in values.values() if value.get("state") == "available")
    return {
        "state": "available" if available_fields else "Unavailable",
        "reason": None if available_fields else "all_metrics_unavailable",
        "version": version,
        "scope_id": f"cgroup-v{version}:{scope_hash}",
        "values": values,
        "notes": ["memory.failcnt is a cgroup counter and does not prove that this process or container was killed."] if version == 1 else [],
    }


def _metric_numbers(sample: Mapping[str, Any]) -> dict[str, int]:
    result: dict[str, int] = {}
    values = sample.get("values")
    if not isinstance(values, Mapping):
        return result
    for name, metric in values.items():
        if not isinstance(metric, Mapping) or metric.get("state") != "available":
            continue
        value = metric.get("value")
        if isinstance(value, int) and not isinstance(value, bool):
            result[str(name)] = value
        counters = metric.get("counters")
        if isinstance(counters, Mapping):
            for counter_name, counter_value in counters.items():
                if isinstance(counter_value, int) and not isinstance(counter_value, bool):
                    result[f"{name}.{counter_name}"] = counter_value
    return result


def _delta_from(baseline: Mapping[str, Any] | None, current: Mapping[str, Any]) -> dict[str, Any]:
    if not isinstance(baseline, Mapping) or baseline.get("state") != "available":
        return {"state": "Unavailable", "reason": "baseline_unavailable"}
    if current.get("state") != "available":
        return {"state": "Unavailable", "reason": "current_unavailable"}
    if baseline.get("scope_id") != current.get("scope_id"):
        return {"state": "Unavailable", "reason": "scope_changed"}
    before = _metric_numbers(baseline)
    after = _metric_numbers(current)
    return {
        "state": "available",
        "scope_id": current.get("scope_id"),
        "values": {name: after[name] - before[name] for name in sorted(before.keys() & after.keys())},
    }


def _linux_memory() -> dict[str, Any]:
    try:
        with open("/proc/meminfo", "rb") as handle:
            raw = handle.read(64 * 1024 + 1)
        if len(raw) > 64 * 1024:
            return {"state": "Unavailable", "reason": "size_limit"}
        rows: dict[str, int] = {}
        for line in raw.decode("ascii", errors="ignore").splitlines():
            fields = line.split()
            if len(fields) >= 2 and fields[0].endswith(":"):
                try:
                    rows[fields[0][:-1]] = int(fields[1])
                except (ValueError, OverflowError):
                    continue
        required = ("MemTotal", "MemAvailable", "SwapTotal", "SwapFree")
        if not all(name in rows for name in required):
            return {"state": "Unavailable", "reason": "fields_missing"}
        return {
            "state": "available",
            "available_bytes": rows["MemAvailable"] * 1024,
            "total_bytes": rows["MemTotal"] * 1024,
            "swap_available_bytes": rows["SwapFree"] * 1024,
            "swap_total_bytes": rows["SwapTotal"] * 1024,
            "source": "proc_meminfo",
        }
    except OSError as exc:
        return {"state": "Unavailable", "reason": "read_error", **_error_fields(exc)}


def _windows_memory() -> dict[str, Any]:
    class _MemoryStatusEx(ctypes.Structure):
        _fields_ = [
            ("dwLength", ctypes.c_ulong),
            ("dwMemoryLoad", ctypes.c_ulong),
            ("ullTotalPhys", ctypes.c_ulonglong),
            ("ullAvailPhys", ctypes.c_ulonglong),
            ("ullTotalPageFile", ctypes.c_ulonglong),
            ("ullAvailPageFile", ctypes.c_ulonglong),
            ("ullTotalVirtual", ctypes.c_ulonglong),
            ("ullAvailVirtual", ctypes.c_ulonglong),
            ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
        ]

    try:
        status = _MemoryStatusEx()
        status.dwLength = ctypes.sizeof(status)
        if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
            return {"state": "Unavailable", "reason": "api_error"}
        return {
            "state": "available",
            "available_bytes": int(status.ullAvailPhys),
            "total_bytes": int(status.ullTotalPhys),
            "swap_available_bytes": max(0, int(status.ullAvailPageFile) - int(status.ullAvailPhys)),
            "swap_total_bytes": max(0, int(status.ullTotalPageFile) - int(status.ullTotalPhys)),
            "source": "global_memory_status_ex",
        }
    except Exception as exc:
        return {"state": "Unavailable", "reason": "api_error", **_error_fields(exc)}


def _memory_snapshot() -> dict[str, Any]:
    if sys.platform.startswith("linux"):
        return _linux_memory()
    if os.name == "nt":
        return _windows_memory()
    return {"state": "Unavailable", "reason": "unsupported_host"}


def _disk_snapshot() -> dict[str, Any]:
    try:
        usage = shutil.disk_usage(os.getcwd())
        return {"state": "available", "free_bytes": int(usage.free), "total_bytes": int(usage.total)}
    except OSError as exc:
        return {"state": "Unavailable", "reason": "read_error", **_error_fields(exc)}
    except Exception as exc:
        return {"state": "Unavailable", "reason": "read_error", **_error_fields(exc)}


def _host_snapshot(reader: ProcReader = _read_proc_file) -> dict[str, Any]:
    return {
        "memory": _memory_snapshot(),
        "workspace_disk": _disk_snapshot(),
        "cgroup": _cgroup_snapshot(reader),
    }


def _filtered_dmesg(
    phase: str,
    started_monotonic: float,
    runner: CommandRunner = _bounded_command_runner,
) -> dict[str, Any]:
    if not sys.platform.startswith("linux"):
        return {"state": "Unavailable", "reason": "not_linux_host", "phase": phase}
    try:
        result = runner(("sudo", "-n", "dmesg", "--time-format", "raw"), COMMAND_TIMEOUT_SECONDS, 1024 * 1024)
    except Exception as exc:
        return {"state": "Unavailable", "reason": "runner_error", "phase": phase, **_error_fields(exc)}
    base: dict[str, Any] = {"phase": phase, "source": "sudo_n_dmesg", "return_code": result.returncode}
    if result.failure:
        base.update({"state": "Unavailable", "reason": result.failure})
        if result.errno is not None:
            base["errno"] = result.errno
        if result.error_type is not None:
            base["error_type"] = result.error_type
        return base
    if result.returncode != 0:
        base.update({"state": "Unavailable", "reason": "command_exit_nonzero"})
        return base

    matches: list[dict[str, Any]] = []
    lines_seen = 0
    malformed_timestamp = 0
    for raw_line in result.stdout.splitlines()[:8192]:
        lines_seen += 1
        line = raw_line.decode("utf-8", errors="replace")
        match = _DMESG_LINE_RE.match(line)
        if not match:
            malformed_timestamp += 1
            continue
        try:
            timestamp = float(match.group(1))
        except (ValueError, OverflowError):
            malformed_timestamp += 1
            continue
        if timestamp < max(0.0, started_monotonic - 0.25):
            continue
        message = match.group(2)
        categories = [name for name, pattern in _DMESG_PATTERNS if pattern.search(message)]
        if categories and len(matches) < MAX_DMESG_MATCHES:
            matches.append({"timestamp_seconds_since_boot": round(timestamp, 6), "categories": categories})
    return {
        **base,
        "state": "available",
        "match_count": len(matches),
        "matches": matches,
        "lines_scanned": lines_seen,
        "malformed_timestamp_lines": malformed_timestamp,
        "output_truncated": result.truncated or lines_seen >= 8192,
        "no_matching_message": not matches,
    }


class RecordWriter:
    """Append-only JSONL writer with hard record and byte bounds."""

    def __init__(self, output_dir: Path, identity: Mapping[str, Any], monotonic: Callable[[], float] = time.monotonic):
        self.output_dir = output_dir
        self.identity = dict(identity)
        self.monotonic = monotonic
        self.path = output_dir / "records.jsonl"
        self.count = 0
        self.byte_count = 0
        try:
            stat = self.path.stat()
            self.byte_count = min(int(stat.st_size), MAX_OUTPUT_BYTES)
            if stat.st_size <= MAX_OUTPUT_BYTES:
                with self.path.open("rb") as existing:
                    for _ in existing:
                        self.count += 1
                        if self.count >= MAX_RECORDS:
                            break
        except FileNotFoundError:
            pass
        self.handle = self.path.open("ab", buffering=0)
        try:
            if os.name != "nt":
                os.chmod(self.path, 0o600)
        except OSError:
            pass
        self.closed = False
        self.limited = self.byte_count >= MAX_OUTPUT_BYTES or self.count >= MAX_RECORDS
        self.finalizing = False

    def _serialize(self, record: Mapping[str, Any]) -> bytes:
        return json.dumps(record, ensure_ascii=True, separators=(",", ":"), sort_keys=True).encode("ascii") + b"\n"

    def _append(self, line: bytes) -> bool:
        if self.byte_count + len(line) > MAX_OUTPUT_BYTES or self.count + 1 > MAX_RECORDS:
            return False
        try:
            self.handle.write(line)
        except OSError as exc:
            self.limited = True
            _write_collector_error(self.output_dir, self.identity, "observer", "record_write_failed", exc)
            return False
        self.byte_count += len(line)
        self.count += 1
        return True

    def _limit_marker(self, phase: str, reason: str) -> None:
        marker = {
            "schema": SCHEMA,
            "version": SCHEMA_VERSION,
            "record_type": "collection_limit",
            "identity": self.identity,
            "timestamp_utc": _utc_now(),
            "monotonic_seconds": round(self.monotonic(), 6),
            "phase": phase,
            "state": "Unavailable",
            "reason": reason,
        }
        line = self._serialize(marker)
        self._append(line)
        self.limited = True

    def begin_finalization(self) -> None:
        self.finalizing = True

    def write(self, record_type: str, phase: str, fields: Mapping[str, Any] | None = None) -> bool:
        final_record = self.finalizing and phase == "S1" and record_type in {
            "docker_observer", "snapshot", "dmesg", "observer_finished",
        }
        if self.closed or (self.limited and not final_record):
            return False
        record: dict[str, Any] = {
            "schema": SCHEMA,
            "version": SCHEMA_VERSION,
            "record_type": record_type,
            "identity": self.identity,
            "timestamp_utc": _utc_now(),
            "monotonic_seconds": round(self.monotonic(), 6),
            "phase": phase,
        }
        if fields:
            record.update(fields)
        try:
            line = self._serialize(record)
        except (TypeError, ValueError, OverflowError):
            record = {
                "schema": SCHEMA,
                "version": SCHEMA_VERSION,
                "record_type": "serialization_error",
                "identity": self.identity,
                "timestamp_utc": _utc_now(),
                "monotonic_seconds": round(self.monotonic(), 6),
                "phase": phase,
                "state": "Unavailable",
                "reason": "serialization_error",
            }
            line = self._serialize(record)
        if len(line) > MAX_RECORD_BYTES:
            record = {
                "schema": SCHEMA,
                "version": SCHEMA_VERSION,
                "record_type": "record_size_limit",
                "identity": self.identity,
                "timestamp_utc": _utc_now(),
                "monotonic_seconds": round(self.monotonic(), 6),
                "phase": phase,
                "state": "Unavailable",
                "reason": "record_size_limit",
            }
            line = self._serialize(record)
            if not final_record:
                self._limit_marker(phase, "record_size_limit")
                return False
        if not final_record and (
            self.count + 1 + 5 > MAX_RECORDS
            or self.byte_count + len(line) + 5 * MAX_RECORD_BYTES > MAX_OUTPUT_BYTES
        ):
            self._limit_marker(phase, "output_size_or_record_limit")
            return False
        if not self._append(line):
            self.limited = True
            return False
        return True

    def close(self) -> None:
        if not self.closed:
            try:
                self.handle.close()
            except OSError:
                pass
            self.closed = True


def _atomic_json(path: Path, payload: Mapping[str, Any]) -> None:
    data = json.dumps(payload, ensure_ascii=True, separators=(",", ":"), sort_keys=True).encode("ascii")
    if len(data) > 128 * 1024:
        raise ValueError("metadata size limit")
    temp_path = path.with_name(path.name + ".tmp")
    with temp_path.open("wb") as handle:
        handle.write(data)
        handle.flush()
        os.fsync(handle.fileno())
    if os.name != "nt":
        try:
            os.chmod(temp_path, 0o600)
        except OSError:
            pass
    os.replace(temp_path, path)


def _read_json_bounded(path: Path, limit: int = 128 * 1024) -> dict[str, Any] | None:
    try:
        raw = path.read_bytes()
    except OSError:
        return None
    if len(raw) > limit:
        return None
    try:
        value = json.loads(raw.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError):
        return None
    return value if isinstance(value, dict) else None


def _write_collector_error(output_dir: Path, identity: Mapping[str, Any], phase: str, reason: str, exc: BaseException | None = None) -> None:
    payload: dict[str, Any] = {
        "schema": SCHEMA,
        "version": SCHEMA_VERSION,
        "identity": dict(identity),
        "phase": phase,
        "state": "Unavailable",
        "reason": reason,
        "timestamp_utc": _utc_now(),
    }
    if exc is not None:
        payload.update(_error_fields(exc))
    try:
        _atomic_json(output_dir / "collector-error.json", payload)
    except Exception:
        pass


def _validate_output_dir(output_dir: Path, run_id: str, attempt: int, platform: str) -> bool:
    runner_temp = os.environ.get("RUNNER_TEMP")
    if not runner_temp:
        return False
    try:
        root = Path(runner_temp).resolve(strict=True)
        target = output_dir.resolve(strict=False)
        if target == root or os.path.commonpath((os.path.normcase(str(root)), os.path.normcase(str(target)))) != os.path.normcase(str(root)):
            return False
        expected_suffix = f"-{run_id}-{attempt}-{platform}".casefold()
        return target.name.casefold().endswith(expected_suffix)
    except (OSError, ValueError, RuntimeError):
        return False


def _valid_identity(identity: Mapping[str, Any]) -> bool:
    return (
        isinstance(identity.get("run_id"), str)
        and bool(_RUN_ID_RE.fullmatch(identity["run_id"]))
        and isinstance(identity.get("attempt"), int)
        and not isinstance(identity.get("attempt"), bool)
        and 1 <= identity["attempt"] <= 100_000
        and identity.get("platform") in ("windows", "webgl")
        and isinstance(identity.get("commit"), str)
        and bool(_COMMIT_RE.fullmatch(identity["commit"]))
    )


def _normalize_options(options: argparse.Namespace) -> tuple[dict[str, Any] | None, str | None]:
    if options.platform not in ("windows", "webgl"):
        return None, "invalid_platform"
    if not isinstance(options.run_id, str) or not _RUN_ID_RE.fullmatch(options.run_id):
        return None, "invalid_run_id"
    if not isinstance(options.attempt, int) or options.attempt < 1 or options.attempt > 100_000:
        return None, "invalid_attempt"
    if not isinstance(options.commit, str) or not _COMMIT_RE.fullmatch(options.commit):
        return None, "invalid_commit"
    if not isinstance(options.duration_seconds, int) or not 1 <= options.duration_seconds <= MAX_DURATION_SECONDS:
        return None, "invalid_duration"
    try:
        directory = Path(options.output_dir)
    except (TypeError, ValueError):
        return None, "invalid_output_dir"
    if not _validate_output_dir(directory, options.run_id, options.attempt, options.platform):
        return None, "output_dir_outside_runner_temp"
    return {
        "identity": {
            "run_id": options.run_id,
            "attempt": options.attempt,
            "platform": options.platform,
            "commit": options.commit.lower(),
        },
        "output_dir": directory,
        "duration_seconds": options.duration_seconds,
    }, None


def _launch_watcher(output_dir: Path) -> bool:
    try:
        command = [sys.executable, "-B", str(Path(__file__).resolve()), "__watch", str(output_dir)]
        subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            shell=False,
            close_fds=True,
            creationflags=_windows_creationflags(),
            **_detached_session_options(),
        )
        return True
    except Exception:
        return False


def _wait_for_file(
    path: Path,
    timeout: float,
    monotonic: Callable[[], float] = time.monotonic,
    sleeper: Callable[[float], None] = time.sleep,
) -> bool:
    deadline = monotonic() + timeout
    while monotonic() < deadline:
        if path.is_file():
            return True
        sleeper(min(0.1, max(0.0, deadline - monotonic())))
    return path.is_file()


def _start_command(
    options: argparse.Namespace,
    runner: CommandRunner = _bounded_command_runner,
    proc_reader: ProcReader = _read_proc_file,
    watcher_launcher: Callable[[Path], bool] = _launch_watcher,
    monotonic: Callable[[], float] = time.monotonic,
    sleeper: Callable[[float], None] = time.sleep,
    host_snapshot: Callable[[ProcReader], Mapping[str, Any]] = _host_snapshot,
) -> int:
    normalized, error = _normalize_options(options)
    if error or normalized is None:
        _print_json({"phase": "start", "state": "Unavailable", "reason": error or "invalid_options"}, stream=sys.stderr)
        return 2
    output_dir: Path = normalized["output_dir"]
    identity: dict[str, Any] = normalized["identity"]
    try:
        output_dir.mkdir(parents=True, exist_ok=False)
    except OSError as exc:
        _print_json({"phase": "start", "state": "Unavailable", "reason": "output_dir_create_failed", **_error_fields(exc)}, stream=sys.stderr)
        return 2

    started_monotonic = monotonic()
    started_unix = time.time()
    started_utc = _utc_from_unix(started_unix)
    deadline_monotonic = started_monotonic + normalized["duration_seconds"]
    try:
        baseline = dict(host_snapshot(proc_reader))
    except Exception as exc:
        _write_collector_error(output_dir, identity, "S0", "snapshot_read_failed", exc)
        _print_json({"phase": "start", "state": "Unavailable", "reason": "snapshot_read_failed", **_error_fields(exc)}, stream=sys.stderr)
        return 2
    writer: RecordWriter | None = None
    try:
        writer = RecordWriter(output_dir, identity, monotonic)
        writer.write("snapshot", "S0", {"host": baseline})
        writer.write("dmesg", "S0", _filtered_dmesg("S0", started_monotonic, runner))
        writer.close()
        metadata = {
            "schema": SCHEMA,
            "version": SCHEMA_VERSION,
            "identity": identity,
            "started_utc": started_utc,
            "started_unix": started_unix,
            "started_monotonic": started_monotonic,
            "deadline_monotonic": deadline_monotonic,
            "duration_seconds": normalized["duration_seconds"],
            "baseline_cgroup": baseline.get("cgroup", {}),
        }
        _atomic_json(output_dir / "metadata.json", metadata)
    except Exception as exc:
        if writer is not None:
            writer.close()
        _write_collector_error(output_dir, identity, "S0", "snapshot_write_failed", exc)
        _print_json({"phase": "start", "state": "Unavailable", "reason": "snapshot_write_failed", **_error_fields(exc)}, stream=sys.stderr)
        return 2

    try:
        launched = watcher_launcher(output_dir)
    except Exception:
        launched = False
    if not launched:
        _write_collector_error(output_dir, identity, "start", "observer_launch_failed")
        _print_json({"phase": "start", "state": "Unavailable", "reason": "observer_launch_failed"}, stream=sys.stderr)
        return 2
    if not _wait_for_file(output_dir / "observer-ready.json", START_READY_TIMEOUT_SECONDS, monotonic, sleeper):
        _write_collector_error(output_dir, identity, "start", "observer_ready_timeout")
        _print_json({"phase": "start", "state": "Unavailable", "reason": "observer_ready_timeout"}, stream=sys.stderr)
        return 2
    ready = _read_json_bounded(output_dir / "observer-ready.json") or {}
    if ready.get("state") != "ready":
        _write_collector_error(output_dir, identity, "start", "observer_not_ready")
        _print_json({"phase": "start", "state": "Unavailable", "reason": "observer_not_ready"}, stream=sys.stderr)
        return 2
    _print_json({"phase": "start", "state": "ready"})
    return 0


def _inspect_template() -> str:
    # This is the complete inspect allowlist: container/image IDs, the specified
    # State fields, and Memory/MemorySwap limits only.
    return (
        '{"id":{{json .Id}},"image_id":{{json .Image}},"pid":{{.State.Pid}},'
        '"started_at":{{json .State.StartedAt}},"finished_at":{{json .State.FinishedAt}},'
        '"exit_code":{{.State.ExitCode}},"oom_killed":{{.State.OOMKilled}},'
        '"memory_limit":{{.HostConfig.Memory}},"memory_swap_limit":{{.HostConfig.MemorySwap}}}'
    )


def _safe_inspect_fields(result: CommandResult) -> dict[str, Any]:
    if result.failure:
        fields: dict[str, Any] = {"state": "Unavailable", "reason": result.failure}
        if result.errno is not None:
            fields["errno"] = result.errno
        if result.error_type is not None:
            fields["error_type"] = result.error_type
        return fields
    if result.returncode != 0:
        return {"state": "Unavailable", "reason": "command_exit_nonzero", "return_code": result.returncode}
    try:
        parsed = json.loads(result.stdout.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError):
        return {"state": "Unavailable", "reason": "invalid_allowlisted_response"}
    if not isinstance(parsed, dict):
        return {"state": "Unavailable", "reason": "invalid_allowlisted_response"}

    cleaned: dict[str, Any] = {"state": "available"}
    identifier = parsed.get("id")
    if isinstance(identifier, str) and _CONTAINER_ID_RE.fullmatch(identifier):
        cleaned["container_id"] = identifier
    else:
        return {"state": "Unavailable", "reason": "invalid_container_id"}
    image_id = parsed.get("image_id")
    if isinstance(image_id, str) and _IMAGE_ID_RE.fullmatch(image_id):
        cleaned["image_id"] = image_id if image_id.startswith("sha256:") else "sha256:" + image_id
    else:
        cleaned["image_id"] = {"state": "Unavailable", "reason": "invalid_image_id"}
    pid = parsed.get("pid")
    cleaned["pid"] = int(pid) if isinstance(pid, int) and not isinstance(pid, bool) and 0 <= pid <= 2**31 - 1 else None
    for key in ("started_at", "finished_at"):
        value = parsed.get(key)
        cleaned[key] = value if isinstance(value, str) and (value == "" or _TIME_RE.fullmatch(value)) else {"state": "Unavailable", "reason": "invalid_value"}
    exit_code = parsed.get("exit_code")
    cleaned["exit_code"] = int(exit_code) if isinstance(exit_code, int) and not isinstance(exit_code, bool) and -(2**31) <= exit_code < 2**31 else None
    oom_killed = parsed.get("oom_killed")
    cleaned["oom_killed"] = oom_killed if isinstance(oom_killed, bool) else {"state": "Unavailable", "reason": "missing"}
    for key in ("memory_limit", "memory_swap_limit"):
        value = parsed.get(key)
        cleaned[key] = int(value) if isinstance(value, int) and not isinstance(value, bool) and value >= 0 else {"state": "Unavailable", "reason": "invalid_value"}
    return cleaned


def _inspect_container(container_id: str, runner: CommandRunner) -> dict[str, Any]:
    if not _CONTAINER_ID_RE.fullmatch(container_id):
        return {"state": "Unavailable", "reason": "invalid_container_id"}
    command = ("docker", "--host", _local_docker_endpoint(), "inspect", "--format", _inspect_template(), container_id)
    try:
        result = runner(command, DOCKER_INSPECT_TIMEOUT_SECONDS, 4096)
    except Exception as exc:
        return {"state": "Unavailable", "reason": "runner_error", **_error_fields(exc)}
    return _safe_inspect_fields(result)


def _local_docker_endpoint() -> str:
    # Never inherit a remote Docker context/DOCKER_HOST from the job environment.
    if os.name == "nt":
        return "npipe:////./pipe/docker_engine"
    return "unix:///var/run/docker.sock"


def _container_cgroup(pid: int | None, reader: ProcReader = _read_proc_file) -> dict[str, Any]:
    if not isinstance(pid, int) or isinstance(pid, bool) or pid <= 0:
        return {"state": "Unavailable", "reason": "container_pid_unavailable"}
    return _cgroup_snapshot(reader, f"/proc/{pid}/cgroup")


def _parse_event_line(line: bytes) -> dict[str, Any] | None:
    if len(line) > MAX_EVENT_LINE_BYTES:
        return None
    try:
        fields = line.decode("ascii", errors="strict").strip().split("|", 3)
    except UnicodeError:
        return None
    if len(fields) != 4:
        return None
    event_type, action, container_id, timestamp_text = fields
    if event_type != "container" or action not in {"oom", "kill", "die", "create", "start"}:
        return None
    if not _CONTAINER_ID_RE.fullmatch(container_id):
        return None
    if not timestamp_text.isdecimal() or len(timestamp_text) > 20:
        return None
    timestamp_ns = int(timestamp_text)
    if not 0 < timestamp_ns < 2**64:
        return None
    return {
        "event_type": event_type,
        "action": action,
        "container_id": container_id,
        "timestamp_ns": timestamp_ns,
        "timestamp_utc": _utc_from_unix(timestamp_ns / 1_000_000_000),
    }


class DockerEventSource:
    """Read-only Docker event stream; captures no names, labels, or attributes."""

    def __init__(self, since_utc: str):
        self.since_utc = since_utc
        self.process: subprocess.Popen[bytes] | None = None
        self.events: queue.Queue[bytes | None] = queue.Queue(maxsize=MAX_QUEUED_EVENTS)
        self.reader: threading.Thread | None = None
        self.dropped = 0
        self.drop_lock = threading.Lock()
        self.start_failure: dict[str, Any] | None = None
        self.finished_recorded = False

    def start(self) -> dict[str, Any]:
        command = [
            "docker", "--host", _local_docker_endpoint(), "events", "--since", self.since_utc,
            "--filter", "type=container",
            "--filter", "event=oom", "--filter", "event=kill", "--filter", "event=die",
            "--filter", "event=create", "--filter", "event=start",
            "--format", "{{.Type}}|{{.Action}}|{{.Actor.ID}}|{{.TimeNano}}",
        ]
        try:
            self.process = subprocess.Popen(
                command,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                shell=False,
                close_fds=True,
                creationflags=_windows_creationflags(),
                **_detached_session_options(),
            )
        except OSError as exc:
            self.start_failure = {"state": "Unavailable", "reason": "launch_error", **_error_fields(exc)}
            return self.start_failure
        except Exception as exc:
            self.start_failure = {"state": "Unavailable", "reason": "launch_error", **_error_fields(exc)}
            return self.start_failure
        assert self.process.stdout is not None

        def read_events() -> None:
            try:
                while True:
                    line = self.process.stdout.readline(MAX_EVENT_LINE_BYTES + 1)
                    if not line:
                        break
                    if len(line) > MAX_EVENT_LINE_BYTES:
                        self._enqueue(b"__oversized__")
                        continue
                    self._enqueue(line)
            except Exception:
                self._enqueue(b"__reader_error__")
            finally:
                self._enqueue(None)

        self.reader = threading.Thread(target=read_events, name="ci9-docker-events-reader", daemon=True)
        self.reader.start()
        return {"state": "observing", "source": "docker_events"}

    def _enqueue(self, value: bytes | None) -> None:
        try:
            self.events.put_nowait(value)
        except queue.Full:
            with self.drop_lock:
                self.dropped += 1

    def drain(self, limit: int = MAX_QUEUED_EVENTS) -> list[bytes | None]:
        result: list[bytes | None] = []
        for _ in range(limit):
            try:
                result.append(self.events.get_nowait())
            except queue.Empty:
                break
        return result

    def status(self) -> dict[str, Any] | None:
        if self.start_failure:
            return self.start_failure
        if self.process is None:
            return {"state": "Unavailable", "reason": "not_started"}
        returncode = self.process.poll()
        if returncode is None:
            return None
        return {"state": "Unavailable", "reason": "command_exit_nonzero" if returncode else "stream_ended", "return_code": returncode}

    def take_dropped(self) -> int:
        with self.drop_lock:
            value = self.dropped
            self.dropped = 0
        return value

    def close(self) -> dict[str, Any]:
        if self.process is None:
            return {"state": "Unavailable", "reason": "not_started"}
        close_status: dict[str, Any] = {"state": "closed"}
        if self.process.poll() is None:
            try:
                self.process.terminate()
            except Exception as exc:
                close_status = {"state": "Unavailable", "reason": "owned_client_close_error", **_error_fields(exc)}
            else:
                try:
                    self.process.wait(timeout=2.0)
                except subprocess.TimeoutExpired:
                    close_status = {"state": "Unavailable", "reason": "owned_client_close_timeout"}
        if self.reader is not None:
            self.reader.join(timeout=1.0)
            if self.reader.is_alive() and close_status.get("state") == "closed":
                close_status = {"state": "Unavailable", "reason": "event_reader_join_timeout"}
        return close_status


class _NullEventSource:
    def __init__(self, status: Mapping[str, Any] | None = None):
        self._status = dict(status or {"state": "Unavailable", "reason": "docker_unavailable"})

    def start(self) -> dict[str, Any]:
        return dict(self._status)

    def drain(self, limit: int = MAX_QUEUED_EVENTS) -> list[bytes | None]:
        return []

    def status(self) -> dict[str, Any] | None:
        return dict(self._status)

    def take_dropped(self) -> int:
        return 0

    def close(self) -> dict[str, Any]:
        return dict(self._status)


def _event_inspect_fields(container_id: str, runner: CommandRunner) -> dict[str, Any]:
    fields = _inspect_container(container_id, runner)
    if fields.get("state") != "available":
        return fields
    return fields


def _sample_record(
    baseline: Mapping[str, Any],
    proc_reader: ProcReader,
    tracked: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    current = _cgroup_snapshot(proc_reader)
    record: dict[str, Any] = {"cgroup": current, "cgroup_delta_from_S0": _delta_from(baseline, current)}
    containers: list[dict[str, Any]] = []
    close_tracking: list[str] = []
    for container_id, state in list(tracked.items())[:MAX_CONTAINER_SAMPLES_PER_TICK]:
        cgroup = _container_cgroup(state.get("pid"), proc_reader)
        first = state.get("baseline_cgroup")
        scope_changed = (
            isinstance(first, Mapping)
            and first.get("state") == "available"
            and cgroup.get("state") == "available"
            and first.get("scope_id") != cgroup.get("scope_id")
        )
        entry: dict[str, Any] = {
            "container_id": container_id,
            "cgroup": {"state": "Unavailable", "reason": "pid_scope_changed"} if scope_changed else cgroup,
            "cgroup_delta_from_first": _delta_from(first, cgroup),
        }
        if scope_changed:
            entry["tracking_state"] = "closed"
            entry["tracking_close_reason"] = "pid_scope_changed"
            close_tracking.append(container_id)
        containers.append(entry)
    for container_id in close_tracking:
        tracked.pop(container_id, None)
    record["containers"] = containers
    if close_tracking:
        record["containers_tracking_closed"] = close_tracking
    if len(tracked) > MAX_CONTAINER_SAMPLES_PER_TICK:
        record["containers_omitted"] = len(tracked) - MAX_CONTAINER_SAMPLES_PER_TICK
    return record


def _process_event(
    event: Mapping[str, Any],
    writer: RecordWriter,
    runner: CommandRunner,
    proc_reader: ProcReader,
    tracked: dict[str, dict[str, Any]],
    inspect_count: list[int],
) -> bool:
    container_id = str(event["container_id"])
    action = str(event["action"])
    fields: dict[str, Any] = {
        "event": {
            "type": "container",
            "action": action,
            "container_id": container_id,
            "timestamp_ns": int(event["timestamp_ns"]),
            "timestamp_utc": str(event["timestamp_utc"]),
        }
    }
    if action in {"create", "start", "oom", "kill", "die"} and inspect_count[0] < MAX_INSPECTS:
        inspect_count[0] += 1
        state = _event_inspect_fields(container_id, runner)
        fields["inspect"] = state
        if state.get("state") == "available":
            pid = state.get("pid")
            if action == "die":
                previous = tracked.get(container_id)
                baseline = previous.get("baseline_cgroup") if previous else None
                if not isinstance(pid, int) or pid <= 0:
                    cgroup = _container_cgroup(pid, proc_reader)
                elif not isinstance(baseline, Mapping) or baseline.get("state") != "available":
                    cgroup = {"state": "Unavailable", "reason": "baseline_scope_unavailable"}
                else:
                    cgroup = _container_cgroup(pid, proc_reader)
                    if cgroup.get("state") == "available" and cgroup.get("scope_id") != baseline.get("scope_id"):
                        cgroup = {"state": "Unavailable", "reason": "pid_scope_changed"}
                fields["container_cgroup"] = cgroup
            else:
                cgroup = _container_cgroup(pid, proc_reader)
                fields["container_cgroup"] = cgroup
                if action in {"create", "start"}:
                    previous = tracked.get(container_id, {})
                    previous_baseline = previous.get("baseline_cgroup")
                    baseline = previous_baseline if isinstance(previous_baseline, Mapping) and previous_baseline.get("state") == "available" else None
                    if baseline is None and cgroup.get("state") == "available":
                        baseline = cgroup
                    if container_id in tracked or len(tracked) < MAX_TRACKED_CONTAINERS:
                        tracked[container_id] = {"pid": pid, "baseline_cgroup": baseline}
    elif action in {"create", "start", "oom", "kill", "die"}:
        fields["inspect"] = {"state": "Unavailable", "reason": "inspect_limit"}
    if action == "die":
        tracked.pop(container_id, None)
    return writer.write("docker_event", "observer", fields)


def _prioritize_shutdown_events(events: Sequence[Mapping[str, Any]]) -> list[Mapping[str, Any]]:
    """Keep every failure event and the latest lifecycle state per container."""
    selected: dict[tuple[str, str, int], Mapping[str, Any]] = {}
    latest_state: dict[str, Mapping[str, Any]] = {}
    state_rank = {"create": 0, "start": 1, "die": 2}
    for event in events:
        action = str(event["action"])
        container_id = str(event["container_id"])
        timestamp_ns = int(event["timestamp_ns"])
        if action in {"oom", "kill"}:
            selected[(container_id, action, timestamp_ns)] = event
        elif action in state_rank:
            previous = latest_state.get(container_id)
            if previous is None or (
                timestamp_ns,
                state_rank[action],
            ) > (
                int(previous["timestamp_ns"]),
                state_rank[str(previous["action"])],
            ):
                latest_state[container_id] = event
    for container_id, event in latest_state.items():
        selected[(container_id, str(event["action"]), int(event["timestamp_ns"]))] = event
    priority = {"oom": 0, "kill": 1, "die": 2, "start": 3, "create": 4}
    return sorted(
        selected.values(),
        key=lambda event: (priority[str(event["action"])], int(event["timestamp_ns"]), str(event["container_id"])),
    )


def _write_shutdown_event(event: Mapping[str, Any], writer: RecordWriter, tracked: dict[str, dict[str, Any]]) -> bool:
    action = str(event["action"])
    fields: dict[str, Any] = {
        "event": {
            "type": "container",
            "action": action,
            "container_id": str(event["container_id"]),
            "timestamp_ns": int(event["timestamp_ns"]),
            "timestamp_utc": str(event["timestamp_utc"]),
        },
        "final_drain": True,
    }
    if action in {"create", "start", "oom", "kill", "die"}:
        fields["inspect"] = {"state": "Unavailable", "reason": "shutdown_drain_inspect_skipped"}
    if action == "die":
        tracked.pop(str(event["container_id"]), None)
    return writer.write("docker_event", "S1", fields)


def _observer_loop(
    output_dir: Path,
    metadata: Mapping[str, Any],
    writer: RecordWriter,
    event_source: Any,
    runner: CommandRunner,
    proc_reader: ProcReader,
    stop_requested: Callable[[], bool],
    monotonic: Callable[[], float] = time.monotonic,
    sleeper: Callable[[float], None] = time.sleep,
    dmesg_capture: Callable[[str, float, CommandRunner], Mapping[str, Any]] = _filtered_dmesg,
    host_snapshot: Callable[[ProcReader], Mapping[str, Any]] = _host_snapshot,
) -> str:
    identity = metadata["identity"]
    start_status = event_source.start()
    writer.write("docker_observer", "S0", start_status)
    _atomic_json(output_dir / "observer-ready.json", {"schema": SCHEMA, "version": SCHEMA_VERSION, "state": "ready"})

    baseline = metadata.get("baseline_cgroup", {})
    deadline = float(metadata["deadline_monotonic"])
    next_sample = monotonic()
    tracked: dict[str, dict[str, Any]] = {}
    inspect_count = [0]
    event_count = 0
    event_records_written = 0
    events_not_captured = 0
    events_inspect_skipped = 0
    dropped_total = 0
    rejected_events = 0
    event_stream_unavailable_written = False
    stop_reason = "deadline"

    while True:
        now = monotonic()
        if writer.limited:
            stop_reason = "output_limit"
            break
        if stop_requested():
            stop_reason = "stop"
            break
        if now >= deadline:
            stop_reason = "deadline"
            break

        # Pull one item at a time so a stop/deadline cannot strand a locally
        # drained batch before the final queue drain.
        for raw_event in event_source.drain(limit=1):
            # Once removed from the queue, retain this single bounded item by
            # processing it before the post-event stop/deadline check below.
            if raw_event is None:
                continue
            if raw_event == b"__oversized__":
                events_not_captured += 1
                writer.write("docker_event", "observer", {"state": "Unavailable", "reason": "event_line_size_limit"})
                continue
            if raw_event == b"__reader_error__":
                events_not_captured += 1
                writer.write("docker_observer", "observer", {"state": "Unavailable", "reason": "event_reader_error"})
                continue
            event = _parse_event_line(raw_event)
            if event is None:
                rejected_events += 1
                events_not_captured += 1
                continue
            event_count += 1
            if event_count > MAX_EVENTS:
                events_not_captured += 1
                writer.write("docker_observer", "observer", {"state": "Unavailable", "reason": "event_count_limit"})
                stop_reason = "event_limit"
                break
            if _process_event(event, writer, runner, proc_reader, tracked, inspect_count):
                event_records_written += 1
            else:
                events_not_captured += 1
            if writer.limited:
                stop_reason = "output_limit"
                break
        if stop_reason == "event_limit":
            break

        if writer.limited:
            stop_reason = "output_limit"
            break

        if stop_requested() or monotonic() >= deadline:
            stop_reason = "stop" if stop_requested() else "deadline"
            break

        dropped = event_source.take_dropped()
        if dropped:
            dropped_total += dropped
            events_not_captured += dropped
            writer.write("docker_observer", "observer", {"state": "Unavailable", "reason": "events_dropped", "dropped_count": min(dropped, MAX_QUEUED_EVENTS * 100)})
        if rejected_events:
            writer.write("docker_observer", "observer", {"state": "Unavailable", "reason": "event_shape_rejected", "rejected_count": min(rejected_events, MAX_EVENTS)})
            rejected_events = 0

        source_status = event_source.status()
        if source_status and not event_stream_unavailable_written and source_status.get("state") == "Unavailable":
            writer.write("docker_observer", "observer", source_status)
            event_stream_unavailable_written = True

        now = monotonic()
        if now >= next_sample:
            writer.write("sample", "observer", _sample_record(baseline, proc_reader, tracked))
            next_sample = now + SAMPLE_INTERVAL_SECONDS
        remaining = min(deadline - monotonic(), max(0.01, next_sample - monotonic()), 0.25)
        if remaining > 0:
            sleeper(remaining)

    close_status = event_source.close()
    writer.begin_finalization()

    # Close/join the owned stream first, then drain the entire bounded queue.
    # Shutdown records carry event facts only; they do not issue one inspect per
    # queued event. Failure events and the latest lifecycle state take priority.
    queued = event_source.drain(limit=MAX_QUEUED_EVENTS)
    dropped = event_source.take_dropped()
    dropped_total += dropped
    events_not_captured += dropped
    final_events: list[Mapping[str, Any]] = []
    final_malformed = 0
    for raw_event in queued:
        if raw_event is None:
            continue
        if raw_event in (b"__oversized__", b"__reader_error__"):
            final_malformed += 1
            continue
        event = _parse_event_line(raw_event)
        if event is None:
            final_malformed += 1
            continue
        final_events.append(event)
    events_not_captured += final_malformed
    event_count += len(final_events)

    prioritized = _prioritize_shutdown_events(final_events)
    coalesced = max(0, len(final_events) - len(prioritized))
    events_not_captured += coalesced
    final_budget = max(0, MAX_EVENTS - event_records_written)
    selected = prioritized[:final_budget]
    events_not_captured += len(prioritized) - len(selected)
    final_events_recorded = 0
    for index, event in enumerate(selected):
        if writer.limited:
            events_not_captured += len(selected) - index
            break
        if _write_shutdown_event(event, writer, tracked):
            event_records_written += 1
            final_events_recorded += 1
            if event.get("action") in {"create", "start", "oom", "kill", "die"}:
                events_inspect_skipped += 1
        else:
            events_not_captured += 1 + len(selected) - index - 1
            break

    final_drain_complete = events_not_captured == 0 and close_status.get("state") == "closed"
    final_drain_state = "available" if final_drain_complete else "Unavailable"
    final_drain_reason = "complete" if final_drain_complete else "final_drain_incomplete"
    writer.write("docker_observer", "S1", {
        **dict(close_status),
        "final_drain_state": final_drain_state,
        "final_drain_reason": final_drain_reason,
        "final_queue_items": len(queued),
        "final_valid_events": len(final_events),
        "final_events_recorded": final_events_recorded,
        "final_events_coalesced": coalesced,
        "final_malformed_items": final_malformed,
        "events_dropped": dropped_total,
        "events_not_captured": events_not_captured,
        "events_not_captured_unknown": close_status.get("state") != "closed",
    })

    final_host = dict(host_snapshot(proc_reader))
    final_cgroup = final_host.get("cgroup", {})
    writer.write("snapshot", "S1", {
        "host": final_host,
        "cgroup_delta_from_S0": _delta_from(baseline, final_cgroup),
        "observer_end_reason": stop_reason,
    })
    dmesg = dmesg_capture("S1", float(metadata["started_monotonic"]), runner)
    writer.write("dmesg", "S1", dmesg)
    writer.write("observer_finished", "S1", {
        "state": "complete",
        "reason": stop_reason,
        "events_seen": event_count,
        "events_recorded": event_records_written,
        "events_not_captured": events_not_captured,
        "events_inspect_skipped": events_inspect_skipped,
        "events_dropped": dropped_total,
        "inspects": inspect_count[0],
    })
    writer.close()
    _atomic_json(output_dir / "observer-done.json", {
        "schema": SCHEMA,
        "version": SCHEMA_VERSION,
        "state": "complete",
        "reason": stop_reason,
        "identity": identity,
        "timestamp_utc": _utc_now(),
    })
    return stop_reason


def _observer_main(
    output_dir: Path,
    runner: CommandRunner = _bounded_command_runner,
    proc_reader: ProcReader = _read_proc_file,
    event_source_factory: Callable[[str], Any] = DockerEventSource,
    monotonic: Callable[[], float] = time.monotonic,
    sleeper: Callable[[float], None] = time.sleep,
) -> int:
    metadata = _read_json_bounded(output_dir / "metadata.json")
    if not metadata or metadata.get("schema") != SCHEMA or metadata.get("version") != SCHEMA_VERSION:
        return 2
    identity = metadata.get("identity")
    if not isinstance(identity, dict) or not _valid_identity(identity):
        return 2
    writer: RecordWriter | None = None
    try:
        writer = RecordWriter(output_dir, identity, monotonic)
        event_source = event_source_factory(str(metadata["started_utc"]))
        _observer_loop(
            output_dir,
            metadata,
            writer,
            event_source,
            runner,
            proc_reader,
            lambda: (output_dir / "stop-requested").exists(),
            monotonic,
            sleeper,
        )
        return 0
    except Exception as exc:
        if writer is not None:
            writer.close()
        _write_collector_error(output_dir, identity, "observer", "observer_failed", exc)
        return 2


def _stop_command(
    output_dir_text: str,
    monotonic: Callable[[], float] = time.monotonic,
    sleeper: Callable[[float], None] = time.sleep,
    timeout_seconds: float = STOP_WAIT_TIMEOUT_SECONDS,
) -> int:
    try:
        output_dir = Path(output_dir_text)
    except (TypeError, ValueError):
        _print_json({"phase": "stop", "state": "Unavailable", "reason": "invalid_output_dir"}, stream=sys.stderr)
        return 2
    metadata = _read_json_bounded(output_dir / "metadata.json")
    if not metadata or not isinstance(metadata.get("identity"), dict):
        _print_json({"phase": "stop", "state": "Unavailable", "reason": "metadata_unavailable"}, stream=sys.stderr)
        return 2
    identity = metadata["identity"]
    if not _valid_identity(identity) or not _validate_output_dir(
        output_dir,
        identity["run_id"],
        identity["attempt"],
        identity["platform"],
    ):
        _print_json({"phase": "stop", "state": "Unavailable", "reason": "output_dir_outside_runner_temp"}, stream=sys.stderr)
        return 2
    done_path = output_dir / "observer-done.json"
    if done_path.is_file():
        _print_json({"phase": "stop", "state": "complete"})
        return 0
    try:
        marker = output_dir / "stop-requested"
        with marker.open("xb"):
            pass
    except FileExistsError:
        pass
    except OSError as exc:
        _write_collector_error(output_dir, identity, "stop", "stop_marker_failed", exc)
        _print_json({"phase": "stop", "state": "Unavailable", "reason": "stop_marker_failed", **_error_fields(exc)}, stream=sys.stderr)
        return 2

    deadline = monotonic() + max(0.1, min(timeout_seconds, STOP_WAIT_TIMEOUT_SECONDS))
    while monotonic() < deadline:
        if done_path.is_file():
            _print_json({"phase": "stop", "state": "complete"})
            return 0
        sleeper(min(0.1, max(0.0, deadline - monotonic())))
    if done_path.is_file():
        _print_json({"phase": "stop", "state": "complete"})
        return 0
    _write_collector_error(output_dir, identity, "stop", "observer_shutdown_timeout")
    _print_json({"phase": "stop", "state": "Unavailable", "reason": "observer_shutdown_timeout"}, stream=sys.stderr)
    return 2


def _print_json(value: Mapping[str, Any], stream: Any = None) -> None:
    target = stream or sys.stdout
    try:
        target.write(json.dumps(value, ensure_ascii=True, separators=(",", ":"), sort_keys=True) + "\n")
        target.flush()
    except Exception:
        pass


class _QuietParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise ValueError("invalid arguments")


def _build_parser() -> argparse.ArgumentParser:
    parser = _QuietParser(add_help=False)
    subparsers = parser.add_subparsers(dest="command", required=True, parser_class=_QuietParser)
    start = subparsers.add_parser("start", add_help=False)
    start.add_argument("--platform", required=True, choices=("windows", "webgl"))
    start.add_argument("--output-dir", required=True)
    start.add_argument("--run-id", required=True)
    start.add_argument("--attempt", required=True, type=int)
    start.add_argument("--commit", required=True)
    start.add_argument("--duration-seconds", required=True, type=int)
    stop = subparsers.add_parser("stop", add_help=False)
    stop.add_argument("--output-dir", required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    raw_argv = list(sys.argv[1:] if argv is None else argv)
    if raw_argv and raw_argv[0] == "__watch":
        if len(raw_argv) != 2:
            _print_json({"phase": "observer", "state": "Unavailable", "reason": "invalid_internal_invocation"}, stream=sys.stderr)
            return 2
        return _observer_main(Path(raw_argv[1]))
    try:
        options = _build_parser().parse_args(raw_argv)
    except (ValueError, SystemExit):
        _print_json({"phase": "cli", "state": "Unavailable", "reason": "invalid_arguments"}, stream=sys.stderr)
        return 2
    if options.command == "start":
        return _start_command(options)
    if options.command == "stop":
        return _stop_command(options.output_dir)
    _print_json({"phase": "cli", "state": "Unavailable", "reason": "invalid_command"}, stream=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
