#!/usr/bin/env python3
"""Run the frozen F10 V6 route or the separately pinned V7 export in one Linux Unity namespace.

This wrapper expects a dedicated ephemeral Linux container which already has
Unity 6000.6.10f1 licensed by the canonical GameCI activation step. It does
not download, install, activate, or return Unity. Every Unity child is a new
process in this same mount namespace; A and B share one measured project copy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath


UNITY_VERSION = "6000.6.10f1"
BASE_OVERLAY_SHA256 = "d918873b7206f8091a4d286cb8e5be94be2d0c45d2df60de85e46cf9e390139c"
SOURCE_ZIP_SHA256 = "0d710dc65065a1435897db61a9bee2124fe8ed1989e97c738b5ee15ea9b47f0e"
SOURCE_INVENTORY_SHA256 = "6aca3aa53a27dcba77893b24495041e16a47fb188d9cf727dbcaa2b16a4d99be"
BASE_SNAPSHOT_PATH = "Logs/F10LinuxSourceSnapshot-20261127/source.zip"
SOURCE_INVENTORY_PATH = "Logs/F10LinuxSourceSnapshot-20261127/source-inventory.json"
OVERLAY_MANIFEST_COPY_PATH = "Logs/F10V6LinuxAcceptance-20261121/linux-overlay-manifest.json"
RUNTIME_RECEIPT_PATH = "Logs/F10V6Resume-20261120/ci-linux-diagnostic.json"
MATERIALIZATION_RECEIPT_PATH = "Logs/F10V6Resume-20261120/materialization-linux-diagnostic.json"
V7_OPENING_ROOT = "Logs/F10V7DiagnosticOpening-20261120"
V7_OVERLAY_MANIFEST_COPY_PATH = V7_OPENING_ROOT + "/linux-v7-overlay-manifest.json"
V7_PREFLIGHT_RECEIPT_PATH = V7_OPENING_ROOT + "/linux-v7-preflight.json"
V7_EXPORT_RECEIPT_PATH = V7_OPENING_ROOT + "/linux-v7-export-receipt.json"
V7_BUNDLE_ROOT = "Logs/F10V7PhysicalExport-20261116/candidate/revisions"
V7_EXPORT_METHOD = "Luckbound.Editor.F10TechnicalPhysicalDescriptorExporter.ExecuteV7LinuxDiagnosticExport"
V7_READONLY_MOUNT_ROOT = Path("/luckbound")
V7_MANIFEST_SHA_ENV = "LUCKBOUND_F10_V7_LINUX_MANIFEST_SHA256"
V7_PREFLIGHT_SCHEMA = "luckbound.f10.v7.linux-preflight.v1"
V7_EXPORT_RECEIPT_SCHEMA = "luckbound.f10.v7.linux-export-receipt.v1"
V7_OVERLAY_SCHEMA = "luckbound.f10.v7.linux-overlay.v1"
V7_BUNDLE_SCHEMA = "luckbound.f10.v7.physical-bundle.v1"
V7_ROUTE = "v7-export"
V7_MATERIALIZED_ASSETS = (
    "Assets/Luckbound/Editor/Diagnostic/F10/F10TutorialDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioCombatC1Diagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioExplorationE1Diagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioGuardianDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10SafeExtractionDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset",
    "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab",
)
V7_GENERATED_DIRECTORY_METAS = (
    "Assets/Luckbound/Editor/Diagnostic/F10.meta",
    "Assets/Luckbound/Editor/Diagnostic/Shared.meta",
    "Assets/Luckbound/Editor/Diagnostic/F10GoldCache.meta",
)
V7_EXPECTED_DELETION_DESTINATIONS = frozenset((
    "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs",
    "Assets/Luckbound/Editor/Diagnostic/F10DisposableArtReferenceImporter.cs.meta",
    "Assets/Luckbound/Editor/F10DiagnosticMaterializationReceiptExporter.cs",
    "Assets/Luckbound/Editor/F10DiagnosticMaterializationReceiptExporter.cs.meta",
    "Assets/Luckbound/Scripts/App/F10DiagnosticMaterializationReceiptValidator.cs",
    "Assets/Luckbound/Scripts/App/F10DiagnosticMaterializationReceiptValidator.cs.meta",
    "Assets/Luckbound/Scripts/App/F10DiagnosticPackageDependencyEvidence.cs",
    "Assets/Luckbound/Scripts/App/F10DiagnosticPackageDependencyEvidence.cs.meta",
    "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs",
    "Assets/Luckbound/Scripts/App/SaveRepository.F10DiagnosticResume.cs.meta",
    "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticMaterializationPackageDependencyTests.cs",
    "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticMaterializationPackageDependencyTests.cs.meta",
    "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticPackageDependencyEvidenceTests.cs",
    "Assets/Luckbound/Tests/EditMode/Editor/F10DiagnosticPackageDependencyEvidenceTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/F10V6ResumeAcceptanceTests.cs",
    "Assets/Luckbound/Tests/PlayMode/F10V6ResumeAcceptanceTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/SaveRepositoryF10DiagnosticResumeConflictTests.cs",
    "Assets/Luckbound/Tests/PlayMode/SaveRepositoryF10DiagnosticResumeConflictTests.cs.meta",
))
UNITY_MATERIALIZER = "Luckbound.Editor.F10DiagnosticLinuxMaterializationReceiptExporter.MaterializeAndMeasure"
WORKFLOW_NAME = "F10 V6 Linux Diagnostic"
JOB_NAME = "f10-v6-linux-acceptance"
INVENTORY_ROOTS = ("Assets", "Packages", "ProjectSettings")
EXTERNAL_INVENTORY_PATHS = (
    "docs/art/F01_bellator_32x48.png",
    "docs/art/F01_master_reference.md",
    "docs/art/records/ART-F01-BELLATOR-32X48.v1.json",
)
MATERIALIZED_ASSETS = (
    "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10TutorialDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioCombatC1Diagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioExplorationE1Diagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10PatioGuardianDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10/F10SafeExtractionDiagnostic.prefab",
    "Assets/Luckbound/Editor/Diagnostic/Shared/StonePrototypeDiagnosticTile.asset",
    "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab",
    "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab",
)
GENERATED_DIRECTORY_METAS = (
    "Assets/Luckbound/Editor/Diagnostic/F10.meta",
    "Assets/Luckbound/Editor/Diagnostic/Shared.meta",
    "Assets/Luckbound/Editor/Diagnostic/F10GoldCache.meta",
)
GENERATOR_FILES = (
    "Assets/Luckbound/Editor/F10DiagnosticSceneGenerator.cs",
    "Assets/Luckbound/Editor/F10TechnicalDiagnosticPrefabGenerator.cs",
    "Assets/Luckbound/Editor/F10RoomTemplatePrefabGenerator.cs",
    "Assets/Luckbound/Editor/F10TechnicalDiagnosticPlayerPrefabGenerator.cs",
    "Assets/Luckbound/Editor/F10PhysicalGoldCacheDiagnosticPrefabGenerator.cs",
    "Assets/Luckbound/Editor/F10DiagnosticLinuxMaterializationReceiptExporter.cs",
)
EXPECTED_LINUX_DELTA_DESTINATIONS = {
    "Assets/Luckbound/Editor/F10DiagnosticLinuxMaterializationReceiptExporter.cs",
    "Assets/Luckbound/Editor/F10DiagnosticLinuxMaterializationReceiptExporter.cs.meta",
    "Assets/Luckbound/Editor/F10DiagnosticSceneGenerator.cs",
    "Assets/Luckbound/Editor/F10RoomTemplatePrefabGenerator.cs",
    "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10Diagnostic.cs",
    "Assets/Luckbound/Scripts/App/ApplicationBootstrapper.F10V7CombatPayment.cs.meta",
    "Assets/Luckbound/Scripts/App/F10DiagnosticLinuxRuntimeEvidence.cs",
    "Assets/Luckbound/Scripts/App/F10DiagnosticLinuxRuntimeEvidence.cs.meta",
    "Assets/Luckbound/Scripts/App/F10ObjectiveDriver2D.cs.meta",
    "Assets/Luckbound/Scripts/App/F10ObjectiveObservation.cs.meta",
    "Assets/Luckbound/Scripts/App/RoomObjectiveTriggerRelay2D.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/F10ObjectiveDriver2DTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/F10V6ResumeAcceptanceTests.cs",
    "Assets/Luckbound/Tests/PlayMode/F10V7CombatPaymentCandidateTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/SaveRepositoryV7AdmissionPendingTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/SaveRepositoryV7AdmissionRaceTests.cs.meta",
    "Assets/Luckbound/Tests/PlayMode/SaveRepositoryV7SessionHandleTests.cs.meta",
    "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
    "Packages/packages-lock.json",
}
PROCESS_A = "Luckbound.Tests.PlayMode.F10V6ResumeAcceptanceTests.ProcessA_BeginsNewRunClearsC1AndWritesReadOnlyReceipt"
PROCESS_B = "Luckbound.Tests.PlayMode.F10V6ResumeAcceptanceTests.ProcessB_ResumesSameC1CheckpointWithoutRepayAndExtracts"
PROCESS_A_NAME = "ProcessA_BeginsNewRunClearsC1AndWritesReadOnlyReceipt"
PROCESS_B_NAME = "ProcessB_ResumesSameC1CheckpointWithoutRepayAndExtracts"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")

_active_child: subprocess.Popen[str] | None = None
_active_pgid: int | None = None
_launching_child = False
_pending_signal: int | None = None
_license_private_directory: Path | None = None
_cleanup_unconfirmed = False
_preservation_context: dict[str, object] | None = None
_run_outcome = "failed"
_failure_summary = ""
UNITY_TERM_GRACE_SECONDS = 10.0
UNITY_KILL_GRACE_SECONDS = 5.0
UNITY_GROUP_POLL_SECONDS = 0.05


class AcceptanceError(RuntimeError):
    pass


class ProcessGroupUnconfirmed(AcceptanceError):
    """Reserved exit 93 tells the entrypoint not to return the license."""
    pass


def fail(message: str) -> None:
    raise AcceptanceError(message)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical(path: Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def is_within(path: Path, root: Path) -> bool:
    try:
        canonical(path).relative_to(canonical(root))
        return True
    except ValueError:
        return False


def reject_symlink_chain(path: Path, *, allow_missing_leaf: bool = False) -> None:
    full = canonical(path)
    parts = [full]
    parts.extend(full.parents)
    for item in reversed(parts):
        try:
            info = item.lstat()
        except FileNotFoundError:
            if allow_missing_leaf:
                continue
            fail(f"Required path is missing: {item}")
        if stat.S_ISLNK(info.st_mode):
            fail(f"Symbolic links are not allowed in diagnostic paths: {item}")


def require_plain_file(path: Path) -> os.stat_result:
    reject_symlink_chain(path)
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode):
        fail(f"Expected a plain regular file: {path}")
    return info


def require_plain_directory(path: Path) -> os.stat_result:
    reject_symlink_chain(path)
    info = path.lstat()
    if not stat.S_ISDIR(info.st_mode):
        fail(f"Expected a plain directory: {path}")
    return info


def safe_relative(raw: str) -> str:
    if not isinstance(raw, str) or not raw or "\\" in raw or ":" in raw or raw.startswith("/"):
        fail(f"Unsafe relative path: {raw!r}")
    if any(part in ("", ".", "..") for part in raw.split("/")):
        fail(f"Unsafe relative path segment: {raw!r}")
    path = PurePosixPath(raw)
    return path.as_posix()


def path_below(root: Path, relative: str) -> Path:
    rel = safe_relative(relative)
    result = canonical(root / Path(*PurePosixPath(rel).parts))
    if not is_within(result, root) or result == canonical(root):
        fail(f"Relative path escaped its root: {relative}")
    return result


def optional_plain_file(path: Path) -> bool:
    """Return false for an absent output, while still rejecting a symlink chain."""
    reject_symlink_chain(path, allow_missing_leaf=True)
    try:
        info = path.lstat()
    except FileNotFoundError:
        return False
    if not stat.S_ISREG(info.st_mode):
        fail(f"Expected a plain regular file: {path}")
    return True


def hash_inventory_content(rows: list[dict[str, object]]) -> str:
    """Canonical content-only snapshot hash: path, sha256, sizeBytes, newline each."""
    payload = "".join(
        f"{row['path']}\n{row['sha256']}\n{row['length']}\n"
        for row in sorted(rows, key=lambda row: str(row["path"]))
    ).encode("utf-8", errors="strict")
    return sha256_bytes(payload)


def hash_copy_inventory(rows: list[dict[str, object]]) -> str:
    """Canonical physical inventory hash shared with the Unity statx validator."""
    payload = "".join(
        f"{row['path']}\n{row['sha256']}\n{row['length']}\n"
        f"{row['deviceMajor']}\n{row['deviceMinor']}\n{row['inode']}\n{row['linkCount']}\n"
        for row in sorted(rows, key=lambda row: str(row["path"]))
    ).encode("utf-8", errors="strict")
    return sha256_bytes(payload)


def inventory_one_file(project_root: Path, relative: str) -> dict[str, object]:
    full = path_below(project_root, relative)
    info = require_plain_file(full)
    if info.st_nlink != 1:
        fail(f"Hard-linked files are not allowed in the measured project: {relative}")
    return {
        "path": safe_relative(relative),
        "sha256": sha256_file(full),
        "length": info.st_size,
        "deviceMajor": os.major(info.st_dev),
        "deviceMinor": os.minor(info.st_dev),
        "inode": info.st_ino,
        "linkCount": info.st_nlink,
    }


def artifact_identity(path: Path) -> dict[str, object]:
    info = require_plain_file(path)
    if info.st_nlink != 1:
        fail(f"Frozen source artifact must not be hard-linked: {path}")
    return {
        "deviceMajor": os.major(info.st_dev),
        "deviceMinor": os.minor(info.st_dev),
        "inode": info.st_ino,
        "length": info.st_size,
        "linkCount": info.st_nlink,
        "sha256": sha256_file(path),
    }


def verify_artifact_unchanged(path: Path, expected: dict[str, object]) -> None:
    if artifact_identity(path) != expected:
        fail(f"A frozen input artifact changed during the diagnostic: {path}")


def verify_no_artifact_inode_reuse(rows: list[dict[str, object]], artifacts: list[dict[str, object]]) -> None:
    artifact_ids = {
        (int(row["deviceMajor"]), int(row["deviceMinor"]), int(row["inode"]))
        for row in artifacts
    }
    if any((int(row["deviceMajor"]), int(row["deviceMinor"]), int(row["inode"])) in artifact_ids for row in rows):
        fail("The project copy shares a physical inode with a frozen ZIP/inventory artifact.")


def capture_inventory(project_root: Path) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    identities: set[tuple[int, int, int]] = set()
    seen: set[str] = set()
    for root_name in INVENTORY_ROOTS:
        root = path_below(project_root, root_name)
        require_plain_directory(root)
        for current, directories, files in os.walk(root, topdown=True, followlinks=False):
            current_path = Path(current)
            require_plain_directory(current_path)
            for name in directories:
                require_plain_directory(current_path / name)
            for name in files:
                full = current_path / name
                relative = full.relative_to(project_root).as_posix()
                row = inventory_one_file(project_root, relative)
                identity = (int(row["deviceMajor"]), int(row["deviceMinor"]), int(row["inode"]))
                if identity in identities:
                    fail(f"Two project files share one Linux inode identity: {relative}")
                identities.add(identity)
                if relative in seen:
                    fail(f"Duplicate inventory path: {relative}")
                seen.add(relative)
                rows.append(row)
    for relative in EXTERNAL_INVENTORY_PATHS:
        row = inventory_one_file(project_root, relative)
        identity = (int(row["deviceMajor"]), int(row["deviceMinor"]), int(row["inode"]))
        if identity in identities:
            fail(f"Two project files share one Linux inode identity: {relative}")
        identities.add(identity)
        if relative in seen:
            fail(f"Duplicate inventory path: {relative}")
        seen.add(relative)
        rows.append(row)
    return sorted(rows, key=lambda row: str(row["path"]))


def content_rows_from_physical(rows: list[dict[str, object]]) -> list[dict[str, object]]:
    return [{"path": row["path"], "sha256": row["sha256"], "length": row["length"]} for row in rows]


def source_rows_from_manifest(inventory_path: Path) -> tuple[list[dict[str, object]], str]:
    require_plain_file(inventory_path)
    if sha256_file(inventory_path) != SOURCE_INVENTORY_SHA256:
        fail("The source-inventory.json bytes differ from the frozen 2026-11-27 artifact SHA-256.")
    try:
        document = json.loads(inventory_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        fail(f"Could not parse the frozen source inventory: {exc}")
    if not isinstance(document, dict) or document.get("schema") != "luckbound.f10.logical-source.v1" or not isinstance(document.get("files"), list):
        fail("The source inventory schema is not the frozen Luckbound F10 logical-source format.")
    rows: list[dict[str, object]] = []
    previous = ""
    for item in document["files"]:
        if not isinstance(item, dict):
            fail("The source inventory contains a malformed file row.")
        path = safe_relative(item.get("path"))
        digest = item.get("sha256")
        size = item.get("sizeBytes")
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest) or not isinstance(size, int) or size < 0:
            fail(f"The source inventory row is malformed: {path}")
        if path <= previous:
            fail("Source inventory paths must be unique and sorted with ordinal path order.")
        previous = path
        rows.append({"path": path, "sha256": digest, "length": size})
    if len(rows) != 4953:
        fail(f"The frozen logical source must contain 4,953 files; found {len(rows)}.")
    return rows, hash_inventory_content(rows)


def read_linux_manifest(path: Path, overlay_root: Path) -> tuple[dict[str, object], str]:
    require_plain_file(path)
    raw = path.read_bytes()
    try:
        manifest = json.loads(raw.decode("utf-8", errors="strict"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"The frozen Linux overlay manifest is invalid JSON: {exc}")
    if not isinstance(manifest, dict) or manifest.get("schema") != "luckbound.f10.linux-overlay.v1" or manifest.get("schemaVersion") != 1:
        fail("The new Linux overlay manifest schema/version is unsupported.")
    if manifest.get("baseOverlayManifestSha256") != BASE_OVERLAY_SHA256:
        fail("The new Linux overlay manifest lost the accepted d918 provenance marker.")
    base = manifest.get("baseSnapshot")
    if not isinstance(base, dict) or base.get("path") != BASE_SNAPSHOT_PATH or \
            base.get("sourceZipSha256") != SOURCE_ZIP_SHA256 or \
            base.get("sourceInventoryPath") != SOURCE_INVENTORY_PATH or \
            base.get("sourceInventorySha256") != SOURCE_INVENTORY_SHA256:
        fail("The Linux manifest does not bind the fixed final source ZIP and inventory.")
    overlays = manifest.get("overlays")
    if not isinstance(overlays, list) or not overlays:
        fail("The frozen Linux overlay manifest has no project delta rows.")
    destinations: set[str] = set()
    for row in overlays:
        if not isinstance(row, dict):
            fail("A Linux overlay row is malformed.")
        destination = safe_relative(row.get("destination"))
        source = safe_relative(row.get("source"))
        digest = row.get("sha256")
        preimage = row.get("preimageSha256")
        if not (destination.startswith("Assets/") or destination == "Packages/packages-lock.json"):
            fail(f"Only fixed project asset destinations are allowed in the Linux overlay: {destination}")
        if destination in destinations:
            fail(f"The Linux overlay repeats a destination: {destination}")
        destinations.add(destination)
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest):
            fail(f"The Linux overlay source hash is invalid: {source}")
        if preimage is not None and (not isinstance(preimage, str) or not SHA256_RE.fullmatch(preimage)):
            fail(f"The Linux overlay preimage is invalid: {destination}")
        source_path = path_below(overlay_root, source)
        require_plain_file(source_path)
        if sha256_file(source_path) != digest:
            fail(f"Overlay source bytes differ from their frozen hash: {source}")
    if destinations != EXPECTED_LINUX_DELTA_DESTINATIONS:
        fail("The Linux manifest must contain exactly the fixed exporter/runtime/helper/test and metadata-repair delta.")
    return manifest, sha256_bytes(raw)


def read_v7_linux_manifest(path: Path, overlay_root: Path, expected_sha256: str) -> tuple[dict[str, object], str]:
    """Read only the separately pinned V7 union manifest; never treat the V6 manifest as its authority."""
    if not isinstance(expected_sha256, str) or not SHA256_RE.fullmatch(expected_sha256):
        fail("The V7 route requires Root's exact lowercase overlay-manifest SHA-256 frozen before CI.")
    require_plain_file(path)
    raw = path.read_bytes()
    actual_manifest_sha = sha256_bytes(raw)
    if actual_manifest_sha != expected_sha256:
        fail("The V7 overlay manifest differs from the exact SHA-256 frozen before CI.")
    try:
        manifest = json.loads(raw.decode("utf-8", errors="strict"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"The frozen V7 Linux overlay manifest is invalid JSON: {exc}")
    if not isinstance(manifest, dict) or manifest.get("schema") != V7_OVERLAY_SCHEMA or manifest.get("schemaVersion") != 1:
        fail("The explicitly selected V7 route requires the distinct V7 Linux overlay schema/version.")
    if manifest.get("baseOverlayManifestSha256") != BASE_OVERLAY_SHA256:
        fail("The V7 union manifest lost the accepted d918 provenance marker.")
    base = manifest.get("baseSnapshot")
    if not isinstance(base, dict) or base.get("path") != BASE_SNAPSHOT_PATH or \
            base.get("sourceZipSha256") != SOURCE_ZIP_SHA256 or \
            base.get("sourceInventoryPath") != SOURCE_INVENTORY_PATH or \
            base.get("sourceInventorySha256") != SOURCE_INVENTORY_SHA256:
        fail("The V7 union manifest is not based on the exact frozen V6 source ZIP and inventory.")
    overlays = manifest.get("overlays")
    if not isinstance(overlays, list) or not overlays:
        fail("The frozen V7 overlay manifest has no source deltas.")
    destinations: set[str] = set()
    output_paths = expected_v7_output_paths()
    for row in overlays:
        if not isinstance(row, dict):
            fail("A V7 overlay row is malformed.")
        destination = safe_relative(row.get("destination"))
        source = safe_relative(row.get("source"))
        digest = row.get("sha256")
        preimage = row.get("preimageSha256")
        if not (destination.startswith("Assets/") or destination == "Packages/packages-lock.json"):
            fail(f"Only project Assets or the exact Packages lockfile may be overlaid for V7: {destination}")
        if destination in destinations or destination in output_paths:
            fail(f"The V7 overlay repeats a destination or claims a generated output: {destination}")
        destinations.add(destination)
        if not isinstance(digest, str) or not SHA256_RE.fullmatch(digest):
            fail(f"The V7 overlay source hash is invalid: {source}")
        if preimage is not None and (not isinstance(preimage, str) or not SHA256_RE.fullmatch(preimage)):
            fail(f"The V7 overlay preimage is invalid: {destination}")
        source_path = path_below(overlay_root, source)
        if not is_within(source_path, V7_READONLY_MOUNT_ROOT):
            fail(f"V7 overlay bytes must come from the fixed read-only mount: {source}")
        require_plain_file(source_path)
        if source_path.stat().st_nlink != 1 or sha256_file(source_path) != digest:
            fail(f"V7 overlay source bytes or physical identity differ from their frozen hash: {source}")
    deletions = manifest.get("deletions")
    if not isinstance(deletions, list) or len(deletions) != len(V7_EXPECTED_DELETION_DESTINATIONS):
        fail("The V7 manifest must contain the exact fixed deletion set for this source union.")
    deletion_destinations: set[str] = set()
    for row in deletions:
        if not isinstance(row, dict) or set(row) != {"destination", "preimageSha256"}:
            fail("A V7 deletion row must contain only destination and mandatory preimageSha256.")
        destination = safe_relative(row["destination"])
        preimage = row["preimageSha256"]
        if destination not in V7_EXPECTED_DELETION_DESTINATIONS or destination in deletion_destinations:
            fail(f"The V7 deletion row is outside the exact fixed set or is repeated: {destination}")
        if not isinstance(preimage, str) or not SHA256_RE.fullmatch(preimage):
            fail(f"The V7 deletion preimage is missing or invalid: {destination}")
        deletion_destinations.add(destination)
    if deletion_destinations != V7_EXPECTED_DELETION_DESTINATIONS:
        fail("The V7 deletion array is not the exact fixed set of 18 frozen ZIP paths.")
    if destinations.intersection(deletion_destinations):
        fail("V7 overlay destinations must not overlap explicit ZIP deletions.")
    return manifest, actual_manifest_sha


def expected_v7_output_paths() -> set[str]:
    return {path for asset in V7_MATERIALIZED_ASSETS for path in (asset, asset + ".meta")} | \
        set(V7_GENERATED_DIRECTORY_METAS)


def verify_v7_overlay_snapshot(source_rows: list[dict[str, object]], manifest: dict[str, object],
                               overlay_root: Path, project_root: Path,
                               actual_rows: list[dict[str, object]]) -> None:
    expected = {str(row["path"]): {"path": row["path"], "sha256": row["sha256"], "length": row["length"]}
                for row in source_rows}
    deletion_destinations: set[str] = set()
    for row in manifest["deletions"]:
        destination = safe_relative(row["destination"])
        previous = expected.get(destination)
        if destination not in V7_EXPECTED_DELETION_DESTINATIONS or destination in deletion_destinations or \
                previous is None or row.get("preimageSha256") != previous["sha256"]:
            fail(f"V7 deletion preimage differs from the exact frozen source inventory: {destination}")
        deletion_destinations.add(destination)
        del expected[destination]
    for row in manifest["overlays"]:
        destination = safe_relative(row["destination"])
        if destination in deletion_destinations:
            fail(f"A V7 overlay cannot restore an explicitly deleted source path: {destination}")
        source_path = path_below(overlay_root, safe_relative(row["source"]))
        overlay_bytes = source_path.read_bytes()
        if sha256_bytes(overlay_bytes) != row["sha256"]:
            fail(f"V7 overlay source bytes changed after the manifest read: {row['source']}")
        previous = expected.get(destination)
        preimage = row.get("preimageSha256")
        if previous is None:
            if preimage is not None:
                fail(f"V7 manifest declared a preimage for absent frozen source path: {destination}")
        elif preimage != previous["sha256"]:
            fail(f"V7 overlay preimage differs from the frozen source inventory: {destination}")
        expected[destination] = {"path": destination, "sha256": row["sha256"], "length": len(overlay_bytes)}
    actual_content = content_rows_from_physical(actual_rows)
    if any(row["path"] in deletion_destinations for row in actual_content):
        fail("A V7 deleted ZIP path reappeared in the measured temporary project.")
    expected_content = [expected[path] for path in sorted(expected)]
    if actual_content != expected_content:
        fail("The V7 temporary project is not exactly the frozen V6 ZIP minus deletions plus the SHA-pinned V7 overlay union.")
    if hash_inventory_content(actual_content) == hash_inventory_content(source_rows):
        fail("The V7 overlay input snapshot is indistinguishable from its frozen V6 base.")


def prepare_v7_bundle_candidate(project_root: Path) -> None:
    """Create the exporter-required empty candidate folder with create-only path semantics."""
    logs_root = path_below(project_root, "Logs")
    require_plain_directory(logs_root)
    for relative in ("Logs/F10V7PhysicalExport-20261116", "Logs/F10V7PhysicalExport-20261116/candidate"):
        target = path_below(project_root, relative)
        if target.exists() or target.is_symlink():
            fail(f"The V7 physical-export candidate destination is occupied; preserving it: {relative}")
        target.mkdir(mode=0o700)
        require_plain_directory(target)


def verify_v7_outputs_absent(project_root: Path) -> None:
    for relative in expected_v7_output_paths():
        target = path_below(project_root, relative)
        if target.exists() or target.is_symlink():
            fail(f"A V7 generated asset/meta destination was occupied before Unity; preserving it: {relative}")


def verify_environment(args: argparse.Namespace) -> tuple[Path, Path, Path, Path, Path]:
    global _license_private_directory
    if not sys.platform.startswith("linux") or os.name != "posix":
        fail("The F10 diagnostic wrapper is Linux-only.")
    if os.environ.get("GITHUB_ACTIONS", "").lower() != "true" or os.environ.get("RUNNER_OS") != "Linux":
        fail("The fixed GitHub-hosted Linux diagnostic context is required.")
    if os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted":
        fail("The dedicated ephemeral GitHub-hosted runner is required.")
    if os.environ.get("GITHUB_WORKFLOW") != WORKFLOW_NAME or os.environ.get("GITHUB_JOB") != JOB_NAME:
        fail("The exact F10 V6 Linux Diagnostic workflow/job is required.")
    _license_private_directory = validate_license_private_directory()
    workspace = canonical(args.workspace)
    temp_root = canonical(args.temp_root)
    project_root = canonical(args.project_root)
    state_path = canonical(args.state)
    results_root = canonical(args.results_root)
    source_zip = canonical(args.source_zip)
    source_inventory = canonical(args.source_inventory)
    if args.route == "v6":
        if args.v7_linux_manifest is not None or args.v7_linux_manifest_sha256 is not None:
            fail("V7 manifest arguments require the explicit --route=v7-export selection.")
        if args.linux_manifest is None:
            fail("The preserved V6 route requires --linux-manifest.")
        manifest = canonical(args.linux_manifest)
    elif args.route == V7_ROUTE:
        if args.linux_manifest is not None:
            fail("The explicit V7 route does not accept the V6 --linux-manifest input.")
        if args.v7_linux_manifest is None:
            fail("The explicit V7 route requires --v7-linux-manifest.")
        if not isinstance(args.v7_linux_manifest_sha256, str) or not SHA256_RE.fullmatch(args.v7_linux_manifest_sha256):
            fail("The explicit V7 route requires Root's exact lowercase --v7-linux-manifest-sha256.")
        manifest = canonical(args.v7_linux_manifest)
    else:
        fail("The diagnostic route must be explicitly selected as v6 or v7-export.")
    overlay_root = canonical(args.overlay_root)
    unity = canonical(args.unity_editor)
    if workspace != canonical(Path(os.environ.get("GITHUB_WORKSPACE", ""))):
        fail("--workspace must name the same GITHUB_WORKSPACE visible inside this container.")
    if temp_root != canonical(Path(os.environ.get("RUNNER_TEMP", ""))):
        fail("--temp-root must name the same RUNNER_TEMP visible inside this container.")
    if project_root != canonical(Path(os.environ.get("LUCKBOUND_F10_LINUX_PROJECT_ROOT", ""))):
        fail("--project-root must equal LUCKBOUND_F10_LINUX_PROJECT_ROOT.")
    require_plain_directory(workspace)
    require_plain_directory(temp_root)
    if not is_within(project_root, temp_root) or project_root == temp_root or is_within(project_root, workspace) or is_within(workspace, project_root):
        fail("The Unity project must be a separate new directory below RUNNER_TEMP and outside GITHUB_WORKSPACE.")
    if is_within(state_path, project_root) or is_within(results_root, project_root) or \
            is_within(state_path, workspace) or is_within(results_root, workspace):
        fail("State and XML/log outputs must stay outside the project copy and source checkout.")
    if not is_within(state_path, temp_root) or not is_within(results_root, temp_root):
        fail("State and output directories must remain below RUNNER_TEMP.")
    require_plain_file(source_zip)
    require_plain_file(source_inventory)
    require_plain_file(manifest)
    require_plain_directory(overlay_root)
    if args.route == V7_ROUTE:
        if any(not is_within(path, V7_READONLY_MOUNT_ROOT) for path in
               (source_zip, source_inventory, manifest, overlay_root)):
            fail("V7 source ZIP, inventory, manifest, and overlay bytes must stay under the fixed read-only mount.")
        require_plain_directory(V7_READONLY_MOUNT_ROOT)
    unity_info = require_plain_file(unity)
    if not (unity_info.st_mode & 0o111):
        fail("The explicitly supplied Unity Editor is not executable in this namespace.")
    if os.environ.get("UNITY_EDITOR_PATH") and canonical(Path(os.environ["UNITY_EDITOR_PATH"])) != unity:
        fail("--unity-editor differs from the explicitly activated UNITY_EDITOR_PATH.")
    return workspace, temp_root, project_root, state_path, results_root


def validate_license_private_directory() -> Path:
    """Validate the private directory used only for the cleanup-failure marker."""
    raw = os.environ.get("ACTIVATE_LICENSE_PATH", "")
    private = Path(raw)
    if not raw or not private.is_absolute() or private.parent != Path("/tmp") or \
            not private.name.startswith("luckbound-license."):
        fail("The private activation directory required for process-group cleanup evidence is missing.")
    try:
        info = private.lstat()
    except OSError as exc:
        fail(f"The private activation directory cannot be verified: {exc}")
    if not stat.S_ISDIR(info.st_mode) or stat.S_ISLNK(info.st_mode) or \
            private.resolve(strict=True) != private or info.st_uid != os.geteuid() or \
            stat.S_IMODE(info.st_mode) != 0o700:
        fail("ACTIVATE_LICENSE_PATH must be the canonical owned mode-0700 directory under /tmp.")
    marker = private / "process-group-unconfirmed"
    try:
        marker.lstat()
    except FileNotFoundError:
        pass
    else:
        fail("A prior Unity process-group cleanup is unconfirmed; no Unity phase may start.")
    return private


def verify_source_zip(source_zip: Path, source_rows: list[dict[str, object]]) -> None:
    if sha256_file(source_zip) != SOURCE_ZIP_SHA256:
        fail("The source ZIP bytes differ from the frozen final 4,953-file project snapshot.")
    expected = {str(row["path"]): row for row in source_rows}
    observed: set[str] = set()
    try:
        with zipfile.ZipFile(source_zip, "r") as archive:
            for entry in archive.infolist():
                name = entry.filename.rstrip("/")
                if not name:
                    continue
                rel = safe_relative(name)
                mode = (entry.external_attr >> 16) & 0xFFFF
                kind = stat.S_IFMT(mode)
                if entry.is_dir():
                    if kind not in (0, stat.S_IFDIR):
                        fail(f"The source ZIP contains a non-directory directory entry: {rel}")
                    continue
                if kind not in (0, stat.S_IFREG):
                    fail(f"The source ZIP contains a symbolic link or special file: {rel}")
                row = expected.get(rel)
                if row is None or rel in observed:
                    fail(f"The source ZIP contains an unlisted or duplicate file: {rel}")
                observed.add(rel)
                entry_digest = hashlib.sha256()
                length = 0
                with archive.open(entry, "r") as stream:
                    for block in iter(lambda: stream.read(1024 * 1024), b""):
                        entry_digest.update(block)
                        length += len(block)
                if length != row["length"] or entry_digest.hexdigest() != row["sha256"]:
                    fail(f"The source ZIP bytes differ from the frozen inventory row: {rel}")
    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        fail(f"Could not verify the source ZIP: {exc}")
    if observed != set(expected):
        missing = sorted(set(expected) - observed)[:3]
        fail(f"The source ZIP does not contain all 4,953 frozen inventory files; missing={missing}.")


def extract_source_zip(source_zip: Path, project_root: Path, source_rows: list[dict[str, object]]) -> None:
    project_root.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(source_zip, "r") as archive:
        for entry in archive.infolist():
            name = entry.filename.rstrip("/")
            if not name:
                continue
            rel = safe_relative(name)
            target = path_below(project_root, rel)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                require_plain_directory(target)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            require_plain_directory(target.parent)
            if target.exists() or target.is_symlink():
                fail(f"The source ZIP repeats a path during extraction: {rel}")
            flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
            if hasattr(os, "O_NOFOLLOW"):
                flags |= os.O_NOFOLLOW
            descriptor = os.open(target, flags, 0o644)
            try:
                with archive.open(entry, "r") as source_stream, os.fdopen(descriptor, "wb") as stream:
                    shutil.copyfileobj(source_stream, stream)
                    stream.flush()
                    os.fsync(stream.fileno())
            except BaseException:
                try:
                    os.close(descriptor)
                except OSError:
                    pass
                raise
    actual_rows = capture_inventory(project_root)
    expected_rows = [dict(row) for row in source_rows]
    actual_content = content_rows_from_physical(actual_rows)
    if actual_content != expected_rows:
        fail("Extracted source tree differs from the frozen inventory after write/readback.")
    if hash_inventory_content(actual_content) != hash_inventory_content(expected_rows):
        fail("Extracted source tree canonical snapshot hash differs from the frozen source inventory.")


def apply_v7_deletions(manifest: dict[str, object], source_rows: list[dict[str, object]], project_root: Path) -> None:
    """Remove only the fixed V7 deletion leaves after validating every frozen preimage and inode."""
    deletions = manifest.get("deletions")
    source_by_path = {str(row["path"]): row for row in source_rows}
    if not isinstance(deletions, list) or len(deletions) != len(V7_EXPECTED_DELETION_DESTINATIONS):
        fail("The V7 deletion list changed before application.")

    validated: list[tuple[Path, tuple[int, int, int, int], str]] = []
    seen: set[str] = set()
    for row in deletions:
        if not isinstance(row, dict) or set(row) != {"destination", "preimageSha256"}:
            fail("A V7 deletion row changed before application.")
        destination = safe_relative(row["destination"])
        preimage = row["preimageSha256"]
        source_row = source_by_path.get(destination)
        if destination not in V7_EXPECTED_DELETION_DESTINATIONS or destination in seen or \
                not isinstance(preimage, str) or not SHA256_RE.fullmatch(preimage) or \
                source_row is None or source_row.get("sha256") != preimage:
            fail(f"A V7 deletion is not bound to the exact frozen ZIP preimage: {destination}")
        target = path_below(project_root, destination)
        info = require_v7_deletion_target(target, project_root)
        if info.st_nlink != 1 or sha256_file(target) != preimage:
            fail(f"V7 deletion preimage or physical identity differs; file preserved: {destination}")
        validated.append((target, (info.st_dev, info.st_ino, info.st_size, info.st_nlink), preimage))
        seen.add(destination)

    if seen != V7_EXPECTED_DELETION_DESTINATIONS:
        fail("The V7 deletion list is not the complete fixed set; no files were removed.")

    # Finish the complete preflight before the first unlink.
    for target, expected_identity, preimage in validated:
        info = require_v7_deletion_target(target, project_root)
        current_identity = (info.st_dev, info.st_ino, info.st_size, info.st_nlink)
        if current_identity != expected_identity or info.st_nlink != 1 or sha256_file(target) != preimage:
            fail(f"V7 deletion target changed during preflight; no files were removed: {target}")

    for target, expected_identity, preimage in validated:
        info = require_v7_deletion_target(target, project_root)
        current_identity = (info.st_dev, info.st_ino, info.st_size, info.st_nlink)
        if current_identity != expected_identity or info.st_nlink != 1 or sha256_file(target) != preimage:
            fail(f"V7 deletion target changed before unlink: {target}")
        os.unlink(target)
        if target.exists() or target.is_symlink():
            fail(f"A V7 deletion leaf remains or reappeared after unlink: {target}")


def require_v7_deletion_target(target: Path, project_root: Path) -> os.stat_result:
    """Require the target leaf and every project ancestor to remain plain and contained."""
    root = canonical(project_root)
    if not is_within(target, root) or target == root:
        fail(f"V7 deletion escaped the disposable project copy: {target}")
    current = target.parent
    while True:
        if not is_within(current, root):
            fail(f"V7 deletion ancestor escaped the disposable project copy: {current}")
        require_plain_directory(current)
        if current == root:
            break
        current = current.parent
    return require_plain_file(target)


def apply_overlay(manifest: dict[str, object], overlay_root: Path, project_root: Path) -> None:
    for row in manifest["overlays"]:
        destination = safe_relative(row["destination"])
        source = path_below(overlay_root, row["source"])
        target = path_below(project_root, destination)
        require_plain_file(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        require_plain_directory(target.parent)
        preimage = row.get("preimageSha256")
        if target.exists() or target.is_symlink():
            require_plain_file(target)
            if preimage is None or sha256_file(target) != preimage:
                fail(f"Overlay preimage mismatch; existing bytes preserved: {destination}")
        elif preimage is not None:
            fail(f"Overlay preimage was declared but destination is absent: {destination}")
        source_bytes = source.read_bytes()
        if sha256_bytes(source_bytes) != row["sha256"]:
            fail(f"Overlay bytes changed after manifest validation: {row['source']}")
        temp = target.with_name(target.name + ".f10-linux-write-tmp")
        if temp.exists() or temp.is_symlink():
            fail(f"A previous overlay write temp exists; it was preserved: {temp}")
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(temp, flags, 0o644)
        try:
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(source_bytes)
                stream.flush()
                os.fsync(stream.fileno())
        except BaseException:
            try:
                os.close(descriptor)
            except OSError:
                pass
            raise
        os.replace(temp, target)
        require_plain_file(target)
        if sha256_file(target) != row["sha256"]:
            fail(f"Overlay destination failed post-write hash verification: {destination}")


def write_create_only(path: Path, data: bytes, mode: int = 0o444) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    require_plain_directory(path.parent)
    if path.exists() or path.is_symlink():
        fail(f"Create-only evidence destination is occupied; prior bytes preserved: {path}")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.chmod(path, mode)
    require_plain_file(path)
    if path.stat().st_nlink != 1 or sha256_file(path) != sha256_bytes(data):
        fail(f"Create-only evidence failed physical readback: {path}")
    return sha256_file(path)


def save_state(state_path: Path, state: dict[str, object]) -> None:
    serialized = (json.dumps(state, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    write_create_only(state_path, serialized, 0o600)


def load_state(state_path: Path) -> dict[str, object]:
    require_plain_file(state_path)
    try:
        state = json.loads(state_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        fail(f"The temporary diagnostic phase state is unreadable: {exc}")
    if not isinstance(state, dict) or state.get("schema") != "ci-linux-diagnostic-wrapper-state" or state.get("schemaVersion") != 1:
        fail("The temporary diagnostic phase state schema is invalid.")
    return state


def rows_without_materialized_outputs(rows: list[dict[str, object]]) -> list[dict[str, object]]:
    removed = {path for asset in MATERIALIZED_ASSETS for path in (asset, asset + ".meta")}
    removed.update(GENERATED_DIRECTORY_METAS)
    return [
        {"path": row["path"], "sha256": row["sha256"], "length": row["length"]}
        for row in rows if row["path"] not in removed
    ]


def expected_output_paths() -> set[str]:
    return {path for asset in MATERIALIZED_ASSETS for path in (asset, asset + ".meta")} | set(GENERATED_DIRECTORY_METAS)


def verify_materialization_receipt(project_root: Path, before_rows: list[dict[str, object]], after_rows: list[dict[str, object]]) -> tuple[Path, str, dict[str, object]]:
    receipt_path = path_below(project_root, MATERIALIZATION_RECEIPT_PATH)
    require_plain_file(receipt_path)
    receipt_sha = sha256_file(receipt_path)
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        fail(f"Unity's Linux materialization receipt is invalid: {exc}")
    if receipt.get("schema") != "ci-linux-materialization-diagnostic" or receipt.get("schemaVersion") != 1 or receipt.get("status") != "verified":
        fail("Unity's Linux materialization receipt has the wrong schema/status.")
    if canonical(Path(receipt.get("projectRoot", ""))) != project_root or receipt.get("unityVersion") != UNITY_VERSION:
        fail("Unity's Linux materialization receipt is bound to another project/version.")
    output_rows = receipt.get("outputs")
    directory_meta_rows = receipt.get("generatedDirectoryMetas")
    if not isinstance(output_rows, list) or not isinstance(directory_meta_rows, list):
        fail("Unity's Linux materialization output inventory is missing.")
    output_paths = {safe_relative(row.get("path")) for row in output_rows if isinstance(row, dict)}
    if output_paths != {path for asset in MATERIALIZED_ASSETS for path in (asset, asset + ".meta")}:
        fail("Unity's receipt does not list exactly the nine generated assets and their metadata.")
    directory_paths = {safe_relative(row.get("path")) for row in directory_meta_rows if isinstance(row, dict)}
    if directory_paths != set(GENERATED_DIRECTORY_METAS):
        fail("Unity's receipt does not list exactly the three generated directory metadata files.")
    before_map = {str(row["path"]): row for row in before_rows}
    after_map = {str(row["path"]): row for row in after_rows}
    if set(after_map) - set(before_map) != expected_output_paths():
        fail("The post-materialization tree delta differs from the exact Unity output allowlist.")
    if set(before_map) - set(after_map):
        fail("Unity materialization removed project files.")
    for path, before in before_map.items():
        after = after_map.get(path)
        if after is None or any(before.get(field) != after.get(field) for field in
                                ("sha256", "length", "deviceMajor", "deviceMinor", "inode", "linkCount")):
            fail(f"Unity materialization changed immutable source bytes or physical identity: {path}")
    for row in output_rows + directory_meta_rows:
        if not isinstance(row, dict):
            fail("Unity's Linux materialization receipt contains a malformed output row.")
        path = safe_relative(row.get("path"))
        if path not in after_map or any(row.get(field) != after_map[path].get(field) for field in
                                        ("sha256", "length", "deviceMajor", "deviceMinor", "inode", "linkCount")):
            fail(f"Unity's output physical identity/hash row differs from Python readback: {path}")
    generators = receipt.get("generatorFiles")
    if not isinstance(generators, list):
        fail("Unity's measured generator source list is missing.")
    generator_map = {row.get("path"): row.get("sha256") for row in generators if isinstance(row, dict)}
    for path in GENERATOR_FILES:
        if generator_map.get(path) != after_map.get(path, {}).get("sha256"):
            fail(f"Unity's receipt did not bind the expected generator bytes: {path}")
    closures = receipt.get("dependencyClosures")
    dependency_paths = receipt.get("dependencyPaths")
    dependency_files = receipt.get("dependencyFiles")
    package_dependencies = receipt.get("packageDependencies")
    if not isinstance(closures, list) or len(closures) != len(MATERIALIZED_ASSETS) or \
            not isinstance(dependency_paths, list) or not isinstance(dependency_files, list) or \
            not isinstance(package_dependencies, list):
        fail("Unity's real AssetDatabase dependency closure evidence is incomplete.")
    return receipt_path, receipt_sha, receipt


def write_runtime_receipt(args: argparse.Namespace, manifest_sha: str, source_snapshot_sha: str,
                          snapshot_sha: str, project_root: Path, temp_root: Path,
                          workspace: Path, source_zip: Path, source_inventory: Path,
                          materialization_sha: str, inventory: list[dict[str, object]]) -> None:
    env_names = (
        "GITHUB_REPOSITORY", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT", "GITHUB_WORKFLOW_REF",
    )
    context = {name: os.environ.get(name, "") for name in env_names}
    if any(not value for value in context.values()):
        fail("GitHub repository/run/attempt/workflow-ref metadata is incomplete.")
    image_version = os.environ.get("ImageVersion")
    if not image_version:
        release = Path("/etc/os-release")
        if not release.is_file():
            fail("Neither ImageVersion nor /etc/os-release is available to identify the runner image.")
        values = {}
        for line in release.read_text(encoding="utf-8").splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                values[key] = value.strip().strip('"')
        image_version = f"{values.get('ID', 'linux')}:{values.get('VERSION_ID', '')}"
    materialization_relative = MATERIALIZATION_RECEIPT_PATH
    source_zip_identity = require_plain_file(source_zip)
    source_inventory_identity = require_plain_file(source_inventory)
    if source_zip_identity.st_nlink != 1 or source_inventory_identity.st_nlink != 1:
        fail("Frozen source artifacts must be independent plain files with link count one.")
    receipt = {
        "schema": "ci-linux-diagnostic",
        "schemaVersion": 1,
        "status": "verified",
        "unityVersion": UNITY_VERSION,
        "baseOverlayManifestSha256": BASE_OVERLAY_SHA256,
        "linuxOverlayManifestPath": OVERLAY_MANIFEST_COPY_PATH,
        "linuxOverlayManifestSha256": manifest_sha,
        "baseSnapshotPath": BASE_SNAPSHOT_PATH,
        "sourceInventoryPath": SOURCE_INVENTORY_PATH,
        "sourceZipRuntimePath": str(source_zip),
        "sourceInventoryRuntimePath": str(source_inventory),
        "sourceZipSha256": SOURCE_ZIP_SHA256,
        "sourceInventorySha256": SOURCE_INVENTORY_SHA256,
        "snapshotSha256": snapshot_sha,
        "sourceSnapshotSha256": source_snapshot_sha,
        "projectRoot": str(project_root),
        "runnerTempRoot": str(temp_root),
        "sourceCheckoutRoot": str(workspace),
        "workflowName": WORKFLOW_NAME,
        "jobName": JOB_NAME,
        "repository": context["GITHUB_REPOSITORY"],
        "runId": context["GITHUB_RUN_ID"],
        "runAttempt": context["GITHUB_RUN_ATTEMPT"],
        "workflowRef": context["GITHUB_WORKFLOW_REF"],
        "runnerImageOS": "Linux",
        "runnerImageVersion": image_version,
        "copyInventorySha256": hash_copy_inventory(inventory),
        "copyInventory": inventory,
        "materializationReceiptPath": materialization_relative,
        "materializationReceiptSha256": materialization_sha,
    }
    path = path_below(project_root, RUNTIME_RECEIPT_PATH)
    write_create_only(path, (json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8"), 0o444)


def mount_namespace_id(pid: int | None = None) -> tuple[int, int]:
    path = Path(f"/proc/{pid}/ns/mnt") if pid is not None else Path("/proc/self/ns/mnt")
    info = path.stat()
    return info.st_dev, info.st_ino


def _process_group_exists(pgid: int) -> bool | None:
    try:
        os.killpg(pgid, 0)
        return True
    except ProcessLookupError:
        return False
    except (PermissionError, OSError):
        return None


def _wait_for_process_group_exit(pgid: int, child: subprocess.Popen[str] | None,
                                 timeout_seconds: float) -> bool:
    deadline = time.monotonic() + max(0.0, timeout_seconds)
    while True:
        if child is not None:
            try:
                child.poll()  # Reap the Unity leader while checking its descendants.
            except OSError:
                return False
        exists = _process_group_exists(pgid)
        if exists is False:
            return True
        if exists is None:
            return False
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return False
        time.sleep(min(UNITY_GROUP_POLL_SECONDS, remaining))


def _write_unconfirmed_process_group_marker() -> bool:
    global _cleanup_unconfirmed
    _cleanup_unconfirmed = True
    private = _license_private_directory
    if private is None:
        print("Unity process-group cleanup is unconfirmed and no trusted marker directory is available.",
              file=sys.stderr)
        return False
    marker = private / "process-group-unconfirmed"
    try:
        info = private.lstat()
        if not stat.S_ISDIR(info.st_mode) or stat.S_ISLNK(info.st_mode) or \
                private.resolve(strict=True) != private or info.st_uid != os.geteuid() or \
                stat.S_IMODE(info.st_mode) != 0o700:
            raise OSError("private marker directory changed after validation")
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(marker, flags, 0o600)
        try:
            payload = b"Unity process group did not confirm termination.\n"
            offset = 0
            while offset < len(payload):
                offset += os.write(descriptor, payload[offset:])
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        return True
    except FileExistsError:
        try:
            marker_info = marker.lstat()
            return stat.S_ISREG(marker_info.st_mode) and not stat.S_ISLNK(marker_info.st_mode)
        except OSError:
            pass
    except OSError as exc:
        print(f"Could not write the process-group cleanup marker: {exc}", file=sys.stderr)
    return False


def _record_unconfirmed_process_group() -> bool:
    global _cleanup_unconfirmed
    _cleanup_unconfirmed = True
    try:
        return _write_unconfirmed_process_group_marker()
    except Exception as exc:
        print(f"Could not record unconfirmed Unity cleanup: {exc}", file=sys.stderr)
        return False


def _clear_active_child(child: subprocess.Popen[str] | None) -> None:
    global _active_child, _active_pgid
    if child is None or _active_child is child:
        _active_child = None
        _active_pgid = None


def stop_child() -> bool:
    """Stop Unity and prove its entire new-session process group is gone."""
    global _active_child, _active_pgid
    if _cleanup_unconfirmed:
        return False
    child = _active_child
    pgid = _active_pgid
    if child is None and pgid is None:
        return True
    if pgid is None or pgid <= 0:
        _record_unconfirmed_process_group()
        return False

    try:
        os.killpg(pgid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    except OSError:
        # Still attempt the bounded KILL phase and record failure if the PGID remains.
        pass
    if _wait_for_process_group_exit(pgid, child, UNITY_TERM_GRACE_SECONDS):
        _clear_active_child(child)
        return True

    try:
        os.killpg(pgid, signal.SIGKILL)
    except ProcessLookupError:
        pass
    except OSError:
        pass
    if _wait_for_process_group_exit(pgid, child, UNITY_KILL_GRACE_SECONDS):
        _clear_active_child(child)
        return True

    _record_unconfirmed_process_group()
    return False


def on_signal(signum: int, _frame: object) -> None:
    global _pending_signal, _failure_summary
    if _launching_child:
        _pending_signal = _pending_signal or signum
        return
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    signal.signal(signal.SIGINT, signal.SIG_IGN)
    stopped = stop_child()
    _failure_summary = ("interrupted; Unity process group was stopped and verified" if stopped else
                        "interrupted; Unity process group termination remains unconfirmed")
    raise SystemExit(128 + signum if stopped else 93)


def run_unity(unity: Path, project_root: Path, command: list[str], stdout_path: Path,
              unity_log_path: Path, timeout_seconds: int, *, route: str = "v6",
              v7_manifest_sha256: str | None = None) -> int:
    global _active_child, _active_pgid, _launching_child, _pending_signal
    if _active_child is not None:
        fail("Unity processes must run sequentially; one child is already active.")
    if stdout_path.exists() or stdout_path.is_symlink():
        fail(f"Unity stdout output already exists; prior evidence preserved: {stdout_path}")
    if unity_log_path.exists() or unity_log_path.is_symlink():
        fail(f"Unity log output already exists; prior evidence preserved: {unity_log_path}")
    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    require_plain_directory(stdout_path.parent)
    env = os.environ.copy()
    env["LUCKBOUND_F10_LINUX_PROJECT_ROOT"] = str(project_root)
    env["LUCKBOUND_F10_LINUX_ROUTE"] = route
    if route == V7_ROUTE:
        if not isinstance(v7_manifest_sha256, str) or not SHA256_RE.fullmatch(v7_manifest_sha256):
            fail("The Unity V7 export process requires the exact frozen overlay-manifest SHA-256.")
        env[V7_MANIFEST_SHA_ENV] = v7_manifest_sha256
    if os.environ.get("UNITY_EDITOR_PATH"):
        env["UNITY_EDITOR_PATH"] = str(unity)
    launch_namespace = mount_namespace_id()
    with stdout_path.open("xb") as log_stream:
        child: subprocess.Popen[str] | None = None
        launch_error: BaseException | None = None
        _launching_child = True
        try:
            child = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=log_stream,
                                     stderr=subprocess.STDOUT, env=env, start_new_session=True,
                                     close_fds=True)
        except BaseException as exc:
            launch_error = exc
        finally:
            if child is not None:
                _active_child = child
                _active_pgid = int(child.pid)
            _launching_child = False
        pending = _pending_signal
        _pending_signal = None
        if pending is not None:
            on_signal(pending, None)
        if launch_error is not None:
            raise launch_error
        if child is None:
            fail("Unity process creation returned without a child handle.")
        try:
            time.sleep(0.05)
            if child.poll() is None:
                if mount_namespace_id(child.pid) != launch_namespace:
                    fail("Unity child entered a different Linux mount namespace than Python.")
            result = child.wait(timeout=timeout_seconds)
            return result
        except subprocess.TimeoutExpired:
            fail(f"Unity exceeded the {timeout_seconds}s phase timeout.")
        finally:
            if _active_child is child or _active_pgid is not None:
                if not stop_child():
                    raise ProcessGroupUnconfirmed(
                        "Unity process-group termination could not be confirmed; the license return is blocked.")


def parse_exact_test_xml(xml_path: Path, selector: str, method_name: str) -> None:
    require_plain_file(xml_path)
    try:
        root = ET.parse(xml_path).getroot()
    except (OSError, ET.ParseError) as exc:
        fail(f"Unity did not produce a parseable XML result for {selector}: {exc}")
    if root.tag != "test-run":
        fail(f"Unity result root is not NUnit test-run for {selector}.")
    if root.get("result") != "Passed" or any(root.get(key) != expected for key, expected in (
            ("total", "1"), ("passed", "1"), ("failed", "0"), ("skipped", "0"))):
        fail(f"Unity XML must contain exactly one Passed test and no failures/skips: {selector}")
    cases = [node for node in root.iter() if node.tag == "test-case"]
    expected_class = selector.rsplit(".", 1)[0]
    expected_method = selector.rsplit(".", 1)[-1]
    if len(cases) != 1 or any(cases[0].get(key) != expected for key, expected in (
            ("result", "Passed"), ("fullname", selector), ("name", method_name),
            ("methodname", expected_method), ("classname", expected_class))):
        fail(f"Unity XML does not contain the exact Passed selector: {selector}")


def run_test(unity: Path, project_root: Path, results_root: Path, selector: str,
             method_name: str, label: str, timeout_seconds: int) -> Path:
    xml_path = results_root / f"{label}.xml"
    log_path = results_root / f"{label}.unity.log"
    stdout_path = results_root / f"{label}.stdout.log"
    if xml_path.exists() or xml_path.is_symlink():
        fail(f"Unity XML output already exists; prior evidence preserved: {xml_path}")
    command = [
        str(unity), "-batchmode", "-nographics", "-projectPath", str(project_root),
        "-runTests", "-testPlatform", "PlayMode", "-testFilter", selector,
        "-testResults", str(xml_path), "-logFile", str(log_path),
    ]
    # Deliberately no -quit in PlayMode tests: Unity Test Runner owns completion.
    result = run_unity(unity, project_root, command, stdout_path, log_path, timeout_seconds)
    parse_exact_test_xml(xml_path, selector, method_name)
    if result != 0:
        fail(f"Unity exited {result} after writing a test XML; selector is not accepted: {selector}")
    return xml_path


def verify_inventory_unchanged(project_root: Path, expected_rows: list[dict[str, object]], stage: str) -> None:
    current = capture_inventory(project_root)
    if current != expected_rows:
        before = {str(row["path"]): row for row in expected_rows}
        after = {str(row["path"]): row for row in current}
        changed = sorted(path for path in before.keys() & after.keys() if before[path] != after[path])[:3]
        delta = sorted(before.keys() ^ after.keys())[:3]
        fail(f"Protected project inventory changed {stage}; changed={changed}, pathDelta={delta}.")


def preserve_available_evidence() -> list[str]:
    context = _preservation_context
    if context is None:
        return []
    project_root = Path(str(context["projectRoot"]))
    source_inventory = Path(str(context["sourceInventory"]))
    manifest_path = Path(str(context["linuxManifest"]))
    state_path = Path(str(context["statePath"]))
    results_root = Path(str(context["resultsRoot"]))
    unity_logs_root = Path(str(context["unityLogsRoot"]))
    evidence_root = results_root / "evidence"
    errors: list[str] = []
    copied: list[dict[str, object]] = []
    try:
        evidence_root.mkdir(parents=True, exist_ok=True)
        require_plain_directory(evidence_root)
    except Exception as exc:  # preserve every other available artifact if this mount is unavailable
        return [f"Could not open the mounted evidence destination: {exc}"]

    def copy_bytes(source: Path, destination_relative: str) -> None:
        try:
            if not optional_plain_file(source):
                return
            destination = path_below(evidence_root, destination_relative)
            digest = write_create_only(destination, source.read_bytes(), 0o444)
            copied.append({"path": destination_relative, "sha256": digest, "length": destination.stat().st_size})
        except FileNotFoundError:
            return
        except Exception as exc:
            errors.append(f"{destination_relative}: {exc}")

    route = str(context.get("route", "v6"))
    copy_bytes(manifest_path, "inputs/v7-linux-overlay-manifest.json" if route == V7_ROUTE else
               "inputs/linux-overlay-manifest.json")
    copy_bytes(source_inventory, "inputs/source-inventory.json")
    copy_bytes(state_path, "inputs/wrapper-state.json")
    if route == V7_ROUTE:
        for relative in (V7_OVERLAY_MANIFEST_COPY_PATH, V7_PREFLIGHT_RECEIPT_PATH, V7_EXPORT_RECEIPT_PATH):
            copy_bytes(path_below(project_root, relative), f"project/{relative}")
        for relative in expected_v7_output_paths():
            copy_bytes(path_below(project_root, relative), f"project/{relative}")
        bundle_root = path_below(project_root, V7_BUNDLE_ROOT)
        try:
            if bundle_root.exists():
                require_plain_directory(bundle_root)
                for current, directories, files in os.walk(bundle_root, topdown=True, followlinks=False):
                    current_path = Path(current)
                    require_plain_directory(current_path)
                    for name in directories:
                        require_plain_directory(current_path / name)
                    for name in files:
                        full = current_path / name
                        require_plain_file(full)
                        relative = full.relative_to(project_root).as_posix()
                        copy_bytes(full, f"project/{relative}")
        except Exception as exc:
            errors.append(f"V7 bundle preservation: {exc}")
    else:
        for relative in (
            OVERLAY_MANIFEST_COPY_PATH,
            MATERIALIZATION_RECEIPT_PATH,
            RUNTIME_RECEIPT_PATH,
            "Logs/F10V6Resume-20261120/acceptance/process-a-receipt.json",
            "Logs/F10V6Resume-20261120/acceptance/bootstrap-process-a.json",
            "Logs/F10V6Resume-20261120/acceptance/bootstrap-process-b.json",
            "Logs/F10V6Resume-20261120/session-manifest.json",
        ):
            copy_bytes(path_below(project_root, relative), f"project/{relative}")

    process_a_receipt_path = path_below(project_root,
        "Logs/F10V6Resume-20261120/acceptance/process-a-receipt.json")
    if process_a_receipt_path.exists():
        try:
            receipt = json.loads(process_a_receipt_path.read_text(encoding="utf-8"))
            run_id = receipt.get("runId")
            profile_relative = safe_relative(receipt.get("profileRelativePath"))
            expected_prefix = f"Logs/F10V6Resume-20261120/sessions/{run_id}/"
            if isinstance(run_id, str) and re.fullmatch(r"[A-Fa-f0-9-]{1,64}", run_id) and profile_relative.startswith(expected_prefix):
                copy_bytes(path_below(project_root, profile_relative), f"project/{profile_relative}")
        except Exception as exc:
            errors.append(f"session profile path: {exc}")

    if project_root.exists() and project_root.is_dir():
        try:
            rows = capture_inventory(project_root)
            inventory_document = {
                "schema": "ci-linux-diagnostic-preserved-inventory",
                "schemaVersion": 1,
                "observedAtExit": True,
                "copyInventorySha256": hash_copy_inventory(rows),
                "copyInventory": rows,
            }
            destination = evidence_root / "project/observed-inventory-at-exit.json"
            write_create_only(destination, (json.dumps(inventory_document, ensure_ascii=False, indent=2) + "\n").encode("utf-8"), 0o444)
            copied.append({"path": "project/observed-inventory-at-exit.json",
                           "sha256": sha256_file(destination), "length": destination.stat().st_size})
            # Preserve rejected migration bytes only after Unity's full process group is gone.
            # These files remain rejected inputs; this capture never changes the expected inventory.
            if _cleanup_unconfirmed or _launching_child or _active_child is not None or _active_pgid is not None:
                errors.append("Rejected-input bytes were not captured: Unity shutdown is unconfirmed.")
            else:
                for relative in (
                    "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                    "Packages/packages-lock.json",
                ):
                    try:
                        source = path_below(project_root, relative)
                        before = artifact_identity(source)
                        data = source.read_bytes()
                        after = artifact_identity(source)
                        observed = next((row for row in rows if row["path"] == relative), None)
                        if (before != after or len(data) != before["length"] or
                                sha256_bytes(data) != before["sha256"] or observed != dict(before, path=relative)):
                            fail(f"Rejected-input capture is unstable or differs from the exit inventory: {relative}")
                        relative_output = f"rejected-inputs/{relative}"
                        output = path_below(evidence_root, relative_output)
                        digest = write_create_only(output, data, 0o444)
                        copied.append({"path": relative_output, "sha256": digest, "length": len(data),
                                       "sourcePhysicalIdentity": before, "approvedAsInput": False})
                    except Exception as exc:
                        errors.append(f"rejected input {relative}: {exc}")
        except Exception as exc:
            errors.append(f"observed inventory: {exc}")

    log_groups = (("v7-export", (".unity.log", ".stdout.log")),) if route == V7_ROUTE else (
        ("materialization", (".unity.log", ".stdout.log")),
        ("process-a", (".xml", ".unity.log", ".stdout.log")),
        ("process-b", (".xml", ".unity.log", ".stdout.log")),
    )
    for name, suffixes in log_groups:
        for suffix in suffixes:
            path = unity_logs_root / (name + suffix)
            try:
                if not optional_plain_file(path):
                    continue
                copied.append({"path": f"unity/{path.name}", "sha256": sha256_file(path), "length": path.stat().st_size})
            except Exception as exc:
                errors.append(f"unity/{path.name}: {exc}")

    try:
        index = {
            "schema": "ci-linux-diagnostic-preserved-evidence",
            "schemaVersion": 1,
            "status": _run_outcome,
            "failure": _failure_summary,
            "files": sorted(copied, key=lambda row: str(row["path"])),
            "preservationErrors": errors,
        }
        write_create_only(evidence_root / "preservation-index.json",
                          (json.dumps(index, ensure_ascii=False, indent=2) + "\n").encode("utf-8"), 0o444)
    except Exception as exc:
        errors.append(f"preservation index: {exc}")
    return errors


def run_v7_export(args: argparse.Namespace, workspace: Path, temp_root: Path, project_root: Path,
                  state_path: Path, results_root: Path) -> None:
    global _preservation_context, _run_outcome
    source_zip = canonical(args.source_zip)
    source_inventory = canonical(args.source_inventory)
    manifest_path = canonical(args.v7_linux_manifest)
    overlay_root = canonical(args.overlay_root)
    unity = canonical(args.unity_editor)
    manifest_sha_expected = args.v7_linux_manifest_sha256
    if project_root != Path("/tmp/luckbound-run/project"):
        fail("The V7 exporter requires the exact fixed /tmp/luckbound-run/project path.")
    if project_root.exists() or project_root.is_symlink():
        fail(f"The temporary V7 Unity project destination already exists; it will not be reused: {project_root}")
    reject_symlink_chain(project_root, allow_missing_leaf=True)
    if state_path.exists() or state_path.is_symlink():
        fail(f"The wrapper state file already exists; prior evidence preserved: {state_path}")
    if results_root.exists() or results_root.is_symlink():
        fail(f"The Unity result directory already exists; prior evidence preserved: {results_root}")

    results_root.mkdir(parents=True, exist_ok=False)
    require_plain_directory(results_root)
    unity_logs_root = results_root / "unity"
    unity_logs_root.mkdir()
    require_plain_directory(unity_logs_root)
    _preservation_context = {
        "route": V7_ROUTE,
        "projectRoot": str(project_root),
        "sourceZip": str(source_zip),
        "sourceInventory": str(source_inventory),
        "linuxManifest": str(manifest_path),
        "overlayRoot": str(overlay_root),
        "manifestSha256": manifest_sha_expected,
        "statePath": str(state_path),
        "resultsRoot": str(results_root),
        "unityLogsRoot": str(unity_logs_root),
    }

    source_zip_identity = artifact_identity(source_zip)
    source_inventory_identity = artifact_identity(source_inventory)
    source_artifacts = [source_zip_identity, source_inventory_identity]
    source_rows, source_snapshot_sha = source_rows_from_manifest(source_inventory)
    verify_source_zip(source_zip, source_rows)
    manifest, manifest_sha = read_v7_linux_manifest(manifest_path, overlay_root, manifest_sha_expected)
    extract_source_zip(source_zip, project_root, source_rows)
    base_rows = capture_inventory(project_root)
    verify_no_artifact_inode_reuse(base_rows, source_artifacts)
    if content_rows_from_physical(base_rows) != source_rows:
        fail("The newly extracted V7 source tree did not match the frozen logical inventory.")
    apply_v7_deletions(manifest, source_rows, project_root)
    apply_overlay(manifest, overlay_root, project_root)

    # The V7 receipt paths live under Logs, outside the measured inventory roots.
    opening_root = path_below(project_root, V7_OPENING_ROOT)
    if opening_root.exists() or opening_root.is_symlink():
        fail("A prior V7 opening-evidence directory exists in the new project; preserving it.")
    opening_root.mkdir(parents=True, exist_ok=False)
    require_plain_directory(opening_root)
    manifest_copy = path_below(project_root, V7_OVERLAY_MANIFEST_COPY_PATH)
    manifest_copy_sha = write_create_only(manifest_copy, manifest_path.read_bytes(), 0o444)
    if manifest_copy_sha != manifest_sha:
        fail("The in-project V7 overlay-manifest copy differs from the root-frozen exact bytes.")

    input_inventory = capture_inventory(project_root)
    verify_no_artifact_inode_reuse(input_inventory, source_artifacts)
    verify_v7_overlay_snapshot(source_rows, manifest, overlay_root, project_root, input_inventory)
    snapshot_sha = hash_inventory_content(content_rows_from_physical(input_inventory))
    if snapshot_sha == source_snapshot_sha:
        fail("The V7 overlay snapshot is indistinguishable from its frozen V6 source snapshot.")
    verify_v7_outputs_absent(project_root)
    prepare_v7_bundle_candidate(project_root)

    context_names = ("GITHUB_REPOSITORY", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT", "GITHUB_WORKFLOW_REF")
    context = {name: os.environ.get(name, "") for name in context_names}
    if any(not value for value in context.values()):
        fail("GitHub repository/run/attempt/workflow-ref metadata is incomplete for V7 correlation.")
    workflow_name = os.environ.get("GITHUB_WORKFLOW", "")
    job_name = os.environ.get("GITHUB_JOB", "")
    if workflow_name != WORKFLOW_NAME or job_name != JOB_NAME:
        fail("V7 context must correlate with the fixed Linux workflow/job used by the Unity helper.")
    preflight = {
        "schema": V7_PREFLIGHT_SCHEMA,
        "schemaVersion": 1,
        "route": V7_ROUTE,
        "status": "measured",
        "projectRoot": str(project_root),
        "unityVersion": UNITY_VERSION,
        "sourceZipPath": BASE_SNAPSHOT_PATH,
        "sourceZipSha256": SOURCE_ZIP_SHA256,
        "sourceInventoryPath": SOURCE_INVENTORY_PATH,
        "sourceInventorySha256": SOURCE_INVENTORY_SHA256,
        "sourceZipRuntimePath": str(source_zip),
        "sourceInventoryRuntimePath": str(source_inventory),
        "sourceSnapshotSha256": source_snapshot_sha,
        "overlayRootPath": str(overlay_root),
        "overlayManifestPath": V7_OVERLAY_MANIFEST_COPY_PATH,
        "overlayManifestSha256": manifest_sha,
        "snapshotSha256": snapshot_sha,
        "copyInventorySha256": hash_copy_inventory(input_inventory),
        "runnerTempRoot": str(temp_root),
        "sourceCheckoutRoot": str(workspace),
        "workflowName": workflow_name,
        "jobName": job_name,
        "repository": context["GITHUB_REPOSITORY"],
        "runId": context["GITHUB_RUN_ID"],
        "runAttempt": context["GITHUB_RUN_ATTEMPT"],
        "workflowRef": context["GITHUB_WORKFLOW_REF"],
        "inputInventory": input_inventory,
    }
    preflight_path = path_below(project_root, V7_PREFLIGHT_RECEIPT_PATH)
    preflight_bytes = (json.dumps(preflight, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    preflight_sha = write_create_only(preflight_path, preflight_bytes, 0o444)
    save_state(state_path, {
        "schema": "ci-linux-diagnostic-wrapper-state",
        "schemaVersion": 1,
        "route": V7_ROUTE,
        "projectRoot": str(project_root),
        "sourceZipPath": str(source_zip),
        "sourceInventoryPath": str(source_inventory),
        "linuxManifestPath": str(manifest_path),
        "linuxManifestSha256": manifest_sha,
        "sourceZipSha256": SOURCE_ZIP_SHA256,
        "sourceInventorySha256": SOURCE_INVENTORY_SHA256,
        "sourceZipPhysicalIdentity": source_zip_identity,
        "sourceInventoryPhysicalIdentity": source_inventory_identity,
        "sourceSnapshotSha256": source_snapshot_sha,
        "snapshotSha256": snapshot_sha,
        "preflightReceiptSha256": preflight_sha,
        "inputInventory": input_inventory,
    })

    project_version = path_below(project_root, "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    if f"m_EditorVersion: {UNITY_VERSION}" not in project_version:
        fail("The frozen V7 project ProjectVersion.txt does not select Unity 6000.6.10f1.")
    export_log = unity_logs_root / "v7-export.unity.log"
    export_stdout = unity_logs_root / "v7-export.stdout.log"
    command = [str(unity), "-batchmode", "-nographics", "-projectPath", str(project_root),
               "-executeMethod", V7_EXPORT_METHOD, "-logFile", str(export_log), "-quit"]
    export_exit = run_unity(unity, project_root, command, export_stdout, export_log,
                            args.export_timeout, route=V7_ROUTE, v7_manifest_sha256=manifest_sha)
    if export_exit != 0:
        fail(f"Unity V7 physical export exited {export_exit}; no later opening was authorized.")

    verify_artifact_unchanged(source_zip, source_zip_identity)
    verify_artifact_unchanged(source_inventory, source_inventory_identity)
    rechecked_manifest, rechecked_manifest_sha = read_v7_linux_manifest(manifest_path, overlay_root, manifest_sha)
    if rechecked_manifest_sha != manifest_sha or rechecked_manifest != manifest or \
            artifact_identity(manifest_copy)["sha256"] != manifest_sha:
        fail("A V7 overlay manifest input or its in-project copy changed during Unity export.")
    if hash_copy_inventory(input_inventory) != preflight["copyInventorySha256"]:
        fail("The measured V7 preflight inventory object was modified in memory.")
    final_inventory = capture_inventory(project_root)
    before_by_path = {str(row["path"]): row for row in input_inventory}
    after_by_path = {str(row["path"]): row for row in final_inventory}
    expected_outputs = expected_v7_output_paths()
    if set(after_by_path) - set(before_by_path) != expected_outputs or set(before_by_path) - set(after_by_path):
        added = sorted(set(after_by_path) - set(before_by_path))
        removed = sorted(set(before_by_path) - set(after_by_path))
        fail(f"The Unity V7 output delta is not exactly eight generated assets plus fixed metadata; added={added[:5]} removed={removed[:5]}.")
    for relative, previous in before_by_path.items():
        current = after_by_path.get(relative)
        if current != previous:
            fail(f"Unity V7 export changed a frozen input's bytes or Linux physical identity: {relative}")
    output_rows = [after_by_path[path] for path in sorted(expected_outputs)]
    _bundle_root, revision_id, bundle_manifest_sha, _bundle_manifest = verify_v7_bundle(
        project_root, after_by_path, expected_outputs)

    preflight_identity = artifact_identity(preflight_path)
    if preflight_identity["sha256"] != preflight_sha or preflight_identity["length"] != len(preflight_bytes):
        fail("The create-only V7 preflight receipt changed during Unity export.")
    export_receipt = {
        "schema": V7_EXPORT_RECEIPT_SCHEMA,
        "schemaVersion": 1,
        "route": V7_ROUTE,
        "status": "technical-candidate",
        "projectRoot": str(project_root),
        "unityVersion": UNITY_VERSION,
        "sourceZipSha256": SOURCE_ZIP_SHA256,
        "sourceInventorySha256": SOURCE_INVENTORY_SHA256,
        "sourceSnapshotSha256": source_snapshot_sha,
        "overlayManifestPath": V7_OVERLAY_MANIFEST_COPY_PATH,
        "overlayManifestSha256": manifest_sha,
        "preflightReceiptPath": V7_PREFLIGHT_RECEIPT_PATH,
        "preflightReceiptSha256": preflight_sha,
        "inputSnapshotSha256": snapshot_sha,
        "inputInventorySha256": hash_copy_inventory(input_inventory),
        "finalInventorySha256": hash_copy_inventory(final_inventory),
        "bundleRootRelativePath": V7_BUNDLE_ROOT,
        "revisionId": revision_id,
        "bundleManifestSha256": bundle_manifest_sha,
        "finalInventory": final_inventory,
        "outputFiles": output_rows,
        "runnerTempRoot": str(temp_root),
        "sourceCheckoutRoot": str(workspace),
        "workflowName": workflow_name,
        "jobName": job_name,
        "repository": context["GITHUB_REPOSITORY"],
        "runId": context["GITHUB_RUN_ID"],
        "runAttempt": context["GITHUB_RUN_ATTEMPT"],
        "workflowRef": context["GITHUB_WORKFLOW_REF"],
    }
    receipt_path = path_below(project_root, V7_EXPORT_RECEIPT_PATH)
    write_create_only(receipt_path,
                      (json.dumps(export_receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8"), 0o444)
    if capture_inventory(project_root) != final_inventory:
        fail("Writing the V7 technical receipt unexpectedly changed the protected project inventory.")
    verify_artifact_unchanged(source_zip, source_zip_identity)
    verify_artifact_unchanged(source_inventory, source_inventory_identity)
    _run_outcome = "passed"
    print("F10 V7 Linux technical export completed: exact create-only preflight, eight generated assets, one measured bundle revision.")
    print(f"V7 revision: {revision_id}")
    print(f"V7 bundle-manifest SHA-256: {bundle_manifest_sha}")
    print("Status: technical-candidate; no human review or opening approval is implied.")


def verify_v7_bundle(project_root: Path, final_by_path: dict[str, dict[str, object]],
                     expected_outputs: set[str]) -> tuple[Path, str, str, dict[str, object]]:
    revisions_root = path_below(project_root, V7_BUNDLE_ROOT)
    require_plain_directory(revisions_root)
    entries = list(revisions_root.iterdir())
    if len(entries) != 1 or not entries[0].name.startswith("revision-"):
        fail("The V7 bundle root must contain exactly one newly created revision directory.")
    revision = entries[0]
    require_plain_directory(revision)
    revision_id = revision.name.removeprefix("revision-")
    if not re.fullmatch(r"[0-9]{8}T[0-9]{13}Z-[0-9a-f]{32}", revision_id):
        fail("The V7 revision directory name does not match the exporter's unique revision format.")
    manifest_path = revision / "bundle-manifest.json"
    manifest_info = require_plain_file(manifest_path)
    if manifest_info.st_nlink != 1:
        fail("The V7 bundle manifest must be a unique plain file with link count one.")
    manifest_sha = sha256_file(manifest_path)
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        fail(f"The V7 bundle manifest cannot be parsed: {exc}")
    if not isinstance(manifest, dict) or manifest.get("schema") != V7_BUNDLE_SCHEMA or \
            manifest.get("revisionId") != revision_id or not isinstance(manifest.get("payloads"), list) or \
            not isinstance(manifest.get("dependencies"), list) or not isinstance(manifest.get("packageDependencies"), list):
        fail("The V7 bundle manifest schema/revision/payload or dependency inventory is incomplete.")

    payloads: dict[str, dict[str, object]] = {}
    observed_files = {"bundle-manifest.json"}
    for row in manifest["payloads"]:
        if not isinstance(row, dict):
            fail("The V7 bundle payload list contains a malformed row.")
        relative = safe_relative(row.get("path"))
        if relative in payloads:
            fail(f"The V7 bundle manifest repeats a payload path: {relative}")
        if not isinstance(row.get("sha256"), str) or not SHA256_RE.fullmatch(row["sha256"]) or \
                not isinstance(row.get("bytes"), int) or row["bytes"] < 0 or \
                not isinstance(row.get("sourcePath"), str) or not isinstance(row.get("role"), str):
            fail(f"The V7 bundle payload row is incomplete: {relative}")
        payload_path = path_below(revision, relative)
        info = require_plain_file(payload_path)
        if info.st_nlink != 1 or info.st_size != row["bytes"] or sha256_file(payload_path) != row["sha256"]:
            fail(f"V7 bundle payload bytes, length, or physical link count differ from its manifest: {relative}")
        payloads[relative] = row
        observed_files.add(relative)
    disk_files: set[str] = set()
    disk_directories: set[str] = set()
    for current, directories, files in os.walk(revision, topdown=True, followlinks=False):
        current_path = Path(current)
        require_plain_directory(current_path)
        for name in directories:
            directory_path = current_path / name
            require_plain_directory(directory_path)
            disk_directories.add(directory_path.relative_to(revision).as_posix())
        for name in files:
            file_path = current_path / name
            require_plain_file(file_path)
            disk_files.add(file_path.relative_to(revision).as_posix())
    if disk_files != observed_files:
        fail("The V7 revision contains an unlisted/missing file or an extra bundle payload.")
    expected_directories = {
        parent.as_posix()
        for relative in observed_files
        for parent in PurePosixPath(relative).parents
        if parent.as_posix() != "."
    }
    if disk_directories != expected_directories:
        fail("The V7 revision contains an unlisted or missing directory outside its exact payload structure.")

    dependencies: dict[str, dict[str, object]] = {}
    for row in manifest["dependencies"]:
        if not isinstance(row, dict):
            fail("The V7 bundle dependency set contains a malformed row.")
        asset_path = safe_relative(row.get("assetPath"))
        if asset_path in dependencies:
            fail(f"The V7 bundle dependency set repeats an asset: {asset_path}")
        if not isinstance(row.get("guid"), str) or not row["guid"] or \
                not isinstance(row.get("assetSha256"), str) or not SHA256_RE.fullmatch(row["assetSha256"]) or \
                not isinstance(row.get("metaSha256"), str) or not SHA256_RE.fullmatch(row["metaSha256"]):
            fail(f"The V7 bundle dependency row is incomplete: {asset_path}")
        asset_payload = safe_relative(row.get("assetPayloadPath"))
        meta_payload = safe_relative(row.get("metaPayloadPath"))
        asset_row = payloads.get(asset_payload)
        meta_row = payloads.get(meta_payload)
        if asset_row is None or meta_row is None or asset_row.get("sourcePath") != asset_path or \
                meta_row.get("sourcePath") != asset_path + ".meta" or \
                asset_row.get("sha256") != row["assetSha256"] or meta_row.get("sha256") != row["metaSha256"]:
            fail(f"The V7 bundle dependency does not match its exact asset/.meta payload: {asset_path}")
        dependencies[asset_path] = row
        if asset_path.startswith("Assets/"):
            asset_input = path_below(project_root, asset_path)
            meta_input = path_below(project_root, asset_path + ".meta")
            if require_plain_file(asset_input).st_nlink != 1 or require_plain_file(meta_input).st_nlink != 1 or \
                    sha256_file(asset_input) != row["assetSha256"] or sha256_file(meta_input) != row["metaSha256"]:
                fail(f"A V7 project dependency differs from its exported payload measurement: {asset_path}")
    output_assets = {path for path in expected_outputs if not path.endswith(".meta") and path not in V7_GENERATED_DIRECTORY_METAS}
    if not output_assets.issubset(dependencies):
        fail("The V7 bundle dependency set omits one or more of the eight generated assets.")

    package_paths = {path for path in dependencies if path.startswith("Packages/")}
    package_files: set[str] = set()
    for package in manifest["packageDependencies"]:
        if not isinstance(package, dict) or not isinstance(package.get("files"), list):
            fail("The V7 package dependency evidence contains a malformed BuiltIn package row.")
        if package.get("packageSource") != "BuiltIn" or package.get("resolvedRootKind") != "builtin-editor":
            fail("The V7 bundle permits only the exact Editor BuiltIn package source policy.")
        for file_row in package["files"]:
            if not isinstance(file_row, dict):
                fail("A V7 package file evidence row is malformed.")
            asset_path = safe_relative(file_row.get("projectRelativePath"))
            if asset_path in package_files or asset_path not in package_paths:
                fail(f"V7 package evidence repeats or adds an unlisted dependency path: {asset_path}")
            package_files.add(asset_path)
            dependency = dependencies[asset_path]
            if file_row.get("sha256") != dependency["assetSha256"] or \
                    file_row.get("metaSha256") != dependency["metaSha256"] or \
                    file_row.get("linkCount") != 1 or file_row.get("metaLinkCount") != 1:
                fail(f"V7 BuiltIn package statx/hash evidence differs from the exact bundle payload: {asset_path}")
    if not package_files.issubset(package_paths):
        fail("V7 BuiltIn package evidence adds a path outside the exact Packages/ dependency set.")
    local_package_paths = package_paths - package_files
    for asset_path in local_package_paths:
        dependency = dependencies[asset_path]
        for relative, expected_sha in ((asset_path, dependency["assetSha256"]),
                                       (asset_path + ".meta", dependency["metaSha256"])):
            row = final_by_path.get(relative)
            if row is None or row["linkCount"] != 1 or row["sha256"] != expected_sha:
                fail(f"A project-local V7 package dependency is absent from the protected inventory or differs from its bundle hash: {relative}")
            local_path = path_below(project_root, relative)
            if require_plain_file(local_path).st_nlink != 1 or sha256_file(local_path) != expected_sha:
                fail(f"A project-local V7 package dependency changed after input inventory capture: {relative}")
    return revision, revision_id, manifest_sha, manifest


def run_acceptance(args: argparse.Namespace) -> None:
    global _preservation_context, _run_outcome
    workspace, temp_root, project_root, state_path, results_root = verify_environment(args)
    if args.route == V7_ROUTE:
        run_v7_export(args, workspace, temp_root, project_root, state_path, results_root)
        return
    source_zip = canonical(args.source_zip)
    source_inventory = canonical(args.source_inventory)
    linux_manifest_path = canonical(args.linux_manifest)
    overlay_root = canonical(args.overlay_root)
    unity = canonical(args.unity_editor)
    if project_root.exists() or project_root.is_symlink():
        fail(f"The temporary Unity project destination already exists; it will not be reused: {project_root}")
    reject_symlink_chain(project_root, allow_missing_leaf=True)
    if state_path.exists() or state_path.is_symlink():
        fail(f"The wrapper state file already exists; prior evidence preserved: {state_path}")
    if results_root.exists() or results_root.is_symlink():
        fail(f"The Unity result directory already exists; prior evidence preserved: {results_root}")

    results_root.mkdir(parents=True, exist_ok=False)
    require_plain_directory(results_root)
    unity_logs_root = results_root / "unity"
    unity_logs_root.mkdir()
    require_plain_directory(unity_logs_root)
    _preservation_context = {
        "projectRoot": str(project_root),
        "sourceInventory": str(source_inventory),
        "linuxManifest": str(linux_manifest_path),
        "statePath": str(state_path),
        "resultsRoot": str(results_root),
        "unityLogsRoot": str(unity_logs_root),
    }

    source_zip_identity = artifact_identity(source_zip)
    source_inventory_identity = artifact_identity(source_inventory)
    source_artifacts = [source_zip_identity, source_inventory_identity]
    source_rows, source_snapshot_sha = source_rows_from_manifest(source_inventory)
    verify_source_zip(source_zip, source_rows)
    manifest, manifest_sha = read_linux_manifest(linux_manifest_path, overlay_root)
    extract_source_zip(source_zip, project_root, source_rows)
    base_rows = capture_inventory(project_root)
    verify_no_artifact_inode_reuse(base_rows, source_artifacts)
    if content_rows_from_physical(base_rows) != source_rows:
        fail("The newly extracted source tree did not match the frozen logical inventory.")
    apply_overlay(manifest, overlay_root, project_root)

    # Logs are outside the measured Assets/Packages/ProjectSettings + fixed art inventory.
    for relative in (RUNTIME_RECEIPT_PATH, MATERIALIZATION_RECEIPT_PATH, OVERLAY_MANIFEST_COPY_PATH):
        destination = path_below(project_root, relative)
        destination.parent.mkdir(parents=True, exist_ok=True)
        require_plain_directory(destination.parent)
    manifest_copy = path_below(project_root, OVERLAY_MANIFEST_COPY_PATH)
    if manifest_copy.exists() or manifest_copy.is_symlink():
        fail("A prior in-copy Linux overlay manifest exists; it was preserved.")
    write_create_only(manifest_copy, linux_manifest_path.read_bytes(), 0o444)

    pre_materialization_inventory = capture_inventory(project_root)
    snapshot_sha = hash_inventory_content(content_rows_from_physical(pre_materialization_inventory))
    if snapshot_sha == source_snapshot_sha:
        fail("The Linux overlay snapshot is indistinguishable from its frozen source; overlay delta was not applied.")
    for output_path in expected_output_paths():
        target = path_below(project_root, output_path)
        if target.exists() or target.is_symlink():
            fail(f"A generated F10 output path was already occupied before materialization: {output_path}")
    if hash_inventory_content(rows_without_materialized_outputs(pre_materialization_inventory)) != snapshot_sha:
        fail("Pre-materialization snapshot canonicalization is inconsistent.")
    state = {
        "schema": "ci-linux-diagnostic-wrapper-state",
        "schemaVersion": 1,
        "projectRoot": str(project_root),
        "sourceZipPath": str(source_zip),
        "sourceInventoryPath": str(source_inventory),
        "linuxManifestPath": str(linux_manifest_path),
        "linuxManifestSha256": manifest_sha,
        "sourceZipSha256": SOURCE_ZIP_SHA256,
        "sourceInventorySha256": SOURCE_INVENTORY_SHA256,
        "sourceZipPhysicalIdentity": source_zip_identity,
        "sourceInventoryPhysicalIdentity": source_inventory_identity,
        "sourceSnapshotSha256": source_snapshot_sha,
        "snapshotSha256": snapshot_sha,
        "preMaterializationInventory": pre_materialization_inventory,
    }
    save_state(state_path, state)
    project_version = path_below(project_root, "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    if f"m_EditorVersion: {UNITY_VERSION}" not in project_version:
        fail("The frozen project ProjectVersion.txt does not select Unity 6000.6.10f1.")
    materialization_log = unity_logs_root / "materialization.unity.log"
    materialization_stdout = unity_logs_root / "materialization.stdout.log"
    if materialization_log.exists() or materialization_stdout.exists():
        fail("A prior Unity materialization log exists; it was preserved.")
    materialize_command = [str(unity), "-batchmode", "-nographics", "-projectPath", str(project_root),
                           "-executeMethod", UNITY_MATERIALIZER, "-logFile", str(materialization_log), "-quit"]
    materialize_exit = run_unity(unity, project_root, materialize_command, materialization_stdout,
                                 materialization_log, args.materialization_timeout)
    if materialize_exit != 0:
        fail(f"Unity materialization exited {materialize_exit}; no acceptance test was started.")

    after_materialization_inventory = capture_inventory(project_root)
    verify_artifact_unchanged(source_zip, source_zip_identity)
    verify_artifact_unchanged(source_inventory, source_inventory_identity)
    verify_no_artifact_inode_reuse(after_materialization_inventory, source_artifacts)
    materialization_path, materialization_sha, _ = verify_materialization_receipt(
        project_root, pre_materialization_inventory, after_materialization_inventory)
    if hash_inventory_content(rows_without_materialized_outputs(after_materialization_inventory)) != snapshot_sha:
        fail("The post-materialization inventory cannot reconstruct the pre-materialization snapshot SHA.")
    runtime_receipt_path = path_below(project_root, RUNTIME_RECEIPT_PATH)
    if runtime_receipt_path.exists() or runtime_receipt_path.is_symlink():
        fail("The CI receipt destination is occupied before the Python create-only write.")
    write_runtime_receipt(args, manifest_sha, source_snapshot_sha, snapshot_sha, project_root, temp_root,
                          workspace, source_zip, source_inventory, materialization_sha,
                          after_materialization_inventory)
    runtime_receipt_sha = sha256_file(runtime_receipt_path)
    materialization_path.chmod(0o444)

    verify_inventory_unchanged(project_root, after_materialization_inventory, "before Process A")
    run_test(unity, project_root, unity_logs_root, PROCESS_A, PROCESS_A_NAME, "process-a", args.test_timeout)
    if sha256_file(runtime_receipt_path) != runtime_receipt_sha or sha256_file(materialization_path) != materialization_sha:
        fail("Process A modified a create-only CI or Unity materialization receipt.")
    verify_inventory_unchanged(project_root, after_materialization_inventory, "after Process A")
    process_a_receipt = path_below(project_root, "Logs/F10V6Resume-20261120/acceptance/process-a-receipt.json")
    require_plain_file(process_a_receipt)
    a_receipt_hash = sha256_file(process_a_receipt)
    if process_a_receipt.stat().st_mode & 0o222:
        fail("Process A's resume evidence receipt is not read-only before Process B.")

    # A and B are intentionally sequential, distinct Unity processes on the same copy/store.
    run_test(unity, project_root, unity_logs_root, PROCESS_B, PROCESS_B_NAME, "process-b", args.test_timeout)
    if sha256_file(runtime_receipt_path) != runtime_receipt_sha or sha256_file(materialization_path) != materialization_sha:
        fail("Process B modified a create-only CI or Unity materialization receipt.")
    if sha256_file(process_a_receipt) != a_receipt_hash or process_a_receipt.stat().st_mode & 0o222:
        fail("Process B changed or unlocked Process A's evidence receipt.")
    verify_inventory_unchanged(project_root, after_materialization_inventory, "after Process B")
    verify_artifact_unchanged(source_zip, source_zip_identity)
    verify_artifact_unchanged(source_inventory, source_inventory_identity)
    _run_outcome = "passed"
    print("F10 V6 Linux physical A/B diagnostic passed: one frozen copy, two sequential Unity processes, exact XML selectors.")
    print(f"Process A XML: {unity_logs_root / 'process-a.xml'}")
    print(f"Process B XML: {unity_logs_root / 'process-b.xml'}")
    print(f"Runtime receipt SHA-256: {runtime_receipt_sha}")
    print(f"Materialization receipt SHA-256: {materialization_sha}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--route", choices=("v6", V7_ROUTE), default="v6",
                        help="Explicitly select the preserved V6 A/B route or the separate V7 export route.")
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--source-zip", required=True, type=Path)
    parser.add_argument("--source-inventory", required=True, type=Path)
    parser.add_argument("--linux-manifest", type=Path,
                        help="V6-only manifest input; forbidden when --route=v7-export.")
    parser.add_argument("--v7-linux-manifest", type=Path,
                        help="Root-frozen exact V7 overlay-union manifest; only accepted with --route=v7-export.")
    parser.add_argument("--v7-linux-manifest-sha256",
                        help="Exact lowercase SHA-256 of the V7 overlay-union manifest frozen before CI.")
    parser.add_argument("--overlay-root", required=True, type=Path,
                        help="Root used to resolve each source path in the Linux overlay manifest.")
    parser.add_argument("--temp-root", required=True, type=Path)
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument("--state", required=True, type=Path)
    parser.add_argument("--unity-editor", required=True, type=Path)
    parser.add_argument("--results-root", required=True, type=Path)
    parser.add_argument("--materialization-timeout", type=int, default=900)
    parser.add_argument("--test-timeout", type=int, default=2400)
    parser.add_argument("--export-timeout", type=int, default=1800)
    return parser


def main() -> int:
    global _failure_summary
    signal.signal(signal.SIGTERM, on_signal)
    signal.signal(signal.SIGINT, on_signal)
    args = build_parser().parse_args()
    exit_code = 0
    try:
        run_acceptance(args)
    except ProcessGroupUnconfirmed as exc:
        _failure_summary = str(exc)
        label = "F10 V7 Linux export" if args.route == V7_ROUTE else "F10 V6 Linux diagnostic"
        print(f"{label} failed closed: {exc}", file=sys.stderr)
        exit_code = 93
    except (AcceptanceError, OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as exc:
        _failure_summary = str(exc)
        if not stop_child():
            _failure_summary += "; Unity process-group termination remains unconfirmed"
        label = "F10 V7 Linux export" if args.route == V7_ROUTE else "F10 V6 Linux diagnostic"
        print(f"{label} failed: {exc}", file=sys.stderr)
    except KeyboardInterrupt:
        stopped = stop_child()
        _failure_summary = ("interrupted; Unity process group stopped before license return" if stopped else
                            "interrupted; Unity process group termination remains unconfirmed")
        label = "F10 V7 Linux export" if args.route == V7_ROUTE else "F10 V6 Linux diagnostic"
        print(f"{label} interrupted; Unity process group cleanup was attempted.", file=sys.stderr)
        exit_code = 130
    finally:
        try:
            if not stop_child():
                exit_code = 93
        finally:
            preservation_errors = preserve_available_evidence()
            if preservation_errors:
                for error in preservation_errors:
                    print(f"Evidence preservation issue: {error}", file=sys.stderr)
                if exit_code == 0:
                    exit_code = 1
    if _failure_summary and exit_code == 0:
        exit_code = 1
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
