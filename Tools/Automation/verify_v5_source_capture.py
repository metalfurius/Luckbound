#!/usr/bin/env python3
"""Verify the exact files and NUnit results from an explicit historical V5 capture.

This is an offline artifact gate. It hashes captured bytes as written and parses
JSON only for contract assertions; it never serializes or canonicalizes a save
preimage, creates expected bytes, or treats metrics as proof of Unity execution.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import stat
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path
from typing import Any


SCHEMA_SHA256 = "cbd9e685acb4301e28f40e08ae0e075c1c7c87af81f11a989de524353d6caa50"
CONTRACT_SHA256 = "69c1213a7cfc3b08663c3237f30fafdc4392e3d62c040162a854283f74d0873f"
ADDENDUM_SHA256 = "603310159854c9fb752f7753852295cab87ff7fd5f477480ca515959b6b2fc01"
HISTORICAL_COMMIT = "a2590e141c46729e6efa7d01ea33647768b58711"
HISTORICAL_TREE = "658f45412efb0b6f554d6c458400116838369a97"
HISTORICAL_EDITOR = "6000.6.6f1"
HISTORICAL_EDITOR_REVISION = "d20c9ac777da"
FULL_TREE_PROOF_SHA256 = "394cae064afb63346056b7f6ffa02f2a9143c1bee6ad2a3f91dc713c80a0cc1e"
FULL_TREE_BLOB_COUNT = 4195
FULL_TREE_BYTES = 159244821

SHA256_RE = re.compile(r"[0-9a-f]{64}\Z")
COMMIT_RE = re.compile(r"[0-9a-f]{40}\Z")
UTC_TIMESTAMP_RE = re.compile(
    r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z\Z"
)

RUNTIME_ADDITIONS = {
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureInputs.cs",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureInputs.cs.meta",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureTests.cs",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/V5SourceCaptureTests.cs.meta",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/Luckbound.V5SourceCapture.Tests.asmdef",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture/Luckbound.V5SourceCapture.Tests.asmdef.meta",
    "Assets/Luckbound/Tests/EditMode/V5SourceCapture.meta",
    "Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs",
    "Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs.meta",
    ".github/workflows/v5-source-capture.yml",
}

BASE_CASE_OUTPUTS = {
    "recipe.json",
    "envelope.json",
    "write-preimage.utf8",
    "read-preimage.utf8",
    "checksum.txt",
    "decode-result.json",
}
POSITIVE_ROLES = {"positive-api-and-codec", "positive-settings-api-and-codec"}
ENCODED_WRITE_CASES = {
    "P0-no-active", "P1-known-zero", "P1-known-nontrivial", "P2-unknown-null",
    "P2-unknown-inline", "P3-receipts-completed", "B0-arrays-null", "B1-arrays-empty",
    "M0-false-marker-payload", "M1-true-marker-null", "S0-settings",
}
DECODE_ENUMS = {
    "missing": "Missing",
    "valid": "Valid",
    "corrupted": "Corrupted",
    "incompatible": "Incompatible",
}
API_OUTPUTS = {"api-envelope.json", "api-observation.json"}
JSON_OUTPUTS = {
    "fixture-index.json",
    "recipe.json",
    "envelope.json",
    "decode-result.json",
    "api-envelope.json",
    "api-observation.json",
    "active-strings.json",
}
HEADER_FIELDS = {"schemaVersion", "contentVersion", "generation", "sequence", "savedAtUtc"}
API_HEADER_FIELDS = HEADER_FIELDS | {"checksum"}
FROZEN_UPGRADE_WIRE_RE = re.compile(
    r'(?<!\\)"frozenPermanentUpgrades"\s*:\s*(null|\{[^{}]*\})', re.DOTALL
)

CANDIDATE_ACTIVE_PAYLOAD_PRESENT = {
    "P0-no-active": False,
    "P1-known-zero": True,
    "P1-known-nontrivial": True,
    "P2-unknown-null": True,
    "P2-unknown-inline": True,
    "P3-receipts-completed": True,
    "B0-arrays-null": False,
    "B1-arrays-empty": False,
    "M0-false-marker-payload": True,
    "M1-true-marker-null": False,
    "S0-settings": False,
    "N0-tampered": True,
}
NO_CANDIDATE_PAYLOAD_CASES = {case_id for case_id, present in CANDIDATE_ACTIVE_PAYLOAD_PRESENT.items() if not present}
REAL_ACTIVE_PAYLOAD_CASES = {case_id for case_id, present in CANDIDATE_ACTIVE_PAYLOAD_PRESENT.items() if present}
EMPTY_PAYLOAD_PROVENANCE = "none; source candidate has no active expedition payload"

# These are the historical V5 DTO members that can occur inside a wholly empty
# ExpeditionSaveData placeholder. `fortunaCharges` is a property, not a field,
# and is intentionally excluded. Child objects are limited to the declared DTOs.
PLACEHOLDER_DTO_FIELDS = {
    "ExpeditionSaveData": {
        "runId", "seed", "generationVersion", "contentVersion", "classId", "raceId",
        "selectedSkillIds", "startingEquipmentInstanceIds", "routeSnapshotJson", "currentNodeId",
        "visitedNodeIds", "completedRoomIds", "completedRoomResults", "roomEntryCheckpointJson",
        "inventorySnapshotJson", "maximumHealth", "currentHealth", "maximumEnergy", "currentEnergy",
        "healthPotionCharges", "frozenPermanentUpgrades", "hasFrozenPermanentUpgrades",
        "accumulatedRenown", "pactIds", "fortuneOfferBookSnapshot", "collectedItemIds", "claimedRewardIds",
    },
    "ExpeditionPermanentUpgradeSnapshotSaveData": {
        "healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel",
    },
    "FortuneOfferBookSnapshot": {
        "snapshotVersion", "generationVersion", "seed", "raceId", "charges", "offers",
        "processedGuardianIds", "rerollReceipts", "fortuneBrewReceipts",
    },
}
PLACEHOLDER_CHILD_DTO = {
    ("ExpeditionSaveData", "frozenPermanentUpgrades"): "ExpeditionPermanentUpgradeSnapshotSaveData",
    ("ExpeditionSaveData", "fortuneOfferBookSnapshot"): "FortuneOfferBookSnapshot",
}

# Closed V5 absence placeholder from the historical DTO member set above and
# its declared V5 defaults, then confirmed against the exact R4 wire captures.
# The 100/100/50/50/2 defaults are present in the historical expedition DTO;
# all remaining scalars use their typed zero defaults and arrays serialize as
# empty arrays. `fortunaCharges` is a property, not a wire field. Do not derive
# this object from the payload currently being verified or from the live V6 DTO.
V5_EMPTY_EXPEDITION_PLACEHOLDER = {
    "runId": "",
    "seed": 0,
    "generationVersion": 0,
    "contentVersion": 0,
    "classId": "",
    "raceId": "",
    "selectedSkillIds": [],
    "startingEquipmentInstanceIds": [],
    "routeSnapshotJson": "",
    "currentNodeId": "",
    "visitedNodeIds": [],
    "completedRoomIds": [],
    "completedRoomResults": [],
    "roomEntryCheckpointJson": "",
    "inventorySnapshotJson": "",
    "maximumHealth": 100,
    "currentHealth": 100,
    "maximumEnergy": 50,
    "currentEnergy": 50,
    "healthPotionCharges": 2,
    "frozenPermanentUpgrades": {
        "healthUpgradeLevel": 0,
        "energyUpgradeLevel": 0,
        "additionalPotionUpgradeLevel": 0,
    },
    "hasFrozenPermanentUpgrades": False,
    "accumulatedRenown": 0,
    "pactIds": [],
    "fortuneOfferBookSnapshot": {
        "snapshotVersion": 0,
        "generationVersion": 0,
        "seed": 0,
        "raceId": "",
        "charges": 0,
        "offers": [],
        "processedGuardianIds": [],
        "rerollReceipts": [],
        "fortuneBrewReceipts": [],
    },
    "collectedItemIds": [],
    "claimedRewardIds": [],
}


class CaptureError(ValueError):
    """Raised when a capture artifact contradicts the frozen contract."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise CaptureError(message)


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _is_reparse_or_symlink(path: Path) -> bool:
    """Reject symlinks and Windows reparse points, including junctions."""
    try:
        info = path.lstat()
    except OSError as exc:
        raise CaptureError(f"Cannot inspect path: {path}: {exc}") from exc
    if stat.S_ISLNK(info.st_mode):
        return True
    if getattr(info, "st_file_attributes", 0) & 0x400:  # FILE_ATTRIBUTE_REPARSE_POINT
        return True
    is_junction = getattr(path, "is_junction", None)
    return bool(callable(is_junction) and is_junction())


def _strict_root(root: Path, label: str) -> Path:
    require(root.exists() and root.is_dir(), f"Missing {label} directory: {root}")
    require(not _is_reparse_or_symlink(root), f"Linked {label} directory: {root}")
    try:
        return root.resolve(strict=True)
    except OSError as exc:
        raise CaptureError(f"Cannot resolve {label} directory: {root}: {exc}") from exc


def safe_file(root: Path, relative: str, label: str = "evidence") -> Path:
    """Resolve one portable relative file without following a link or escaping root."""
    require(isinstance(relative, str) and bool(relative), f"Missing {label} path")
    require("\\" not in relative and ":" not in relative and "\x00" not in relative,
            f"Nonportable {label} path: {relative!r}")
    require(not relative.startswith("/"), f"Absolute {label} path: {relative!r}")
    pieces = relative.split("/")
    require(all(piece not in ("", ".", "..") for piece in pieces),
            f"Unsafe {label} path: {relative!r}")
    root_real = _strict_root(root, label)
    current = root
    for index, piece in enumerate(pieces):
        current = current / piece
        require(current.exists(), f"Missing {label} file: {relative}")
        require(not _is_reparse_or_symlink(current), f"Symlink/reparse point in {label} path: {relative}")
        if index < len(pieces) - 1:
            require(current.is_dir(), f"Non-directory component in {label} path: {relative}")
    require(current.is_file(), f"Expected regular {label} file: {relative}")
    try:
        resolved = current.resolve(strict=True)
        resolved.relative_to(root_real)
    except (OSError, ValueError) as exc:
        raise CaptureError(f"{label.title()} path escaped its root: {relative}") from exc
    return current


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        require(key not in result, f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def parse_json_bytes(payload: bytes, label: str, *, object_only: bool = False) -> Any:
    require(not payload.startswith(b"\xef\xbb\xbf"), f"UTF-8 BOM in {label}")
    try:
        text = payload.decode("utf-8", errors="strict")
    except UnicodeDecodeError as exc:
        raise CaptureError(f"Invalid UTF-8 in {label}") from exc
    try:
        value = json.loads(text, object_pairs_hook=_reject_duplicate_keys)
    except (json.JSONDecodeError, CaptureError) as exc:
        if isinstance(exc, CaptureError):
            raise
        raise CaptureError(f"Invalid JSON in {label}: {exc}") from exc
    if object_only:
        require(isinstance(value, dict), f"Expected JSON object in {label}")
    return value


def load_json_file(path: Path, label: str, *, object_only: bool = True) -> Any:
    try:
        return parse_json_bytes(path.read_bytes(), label, object_only=object_only)
    except OSError as exc:
        raise CaptureError(f"Cannot read {label}: {exc}") from exc


def validate_schema(schema_path: Path) -> dict[str, Any]:
    require(schema_path.is_file() and not _is_reparse_or_symlink(schema_path),
            f"Missing/linked capture schema: {schema_path}")
    schema_bytes = schema_path.read_bytes()
    require(sha256_bytes(schema_bytes) == SCHEMA_SHA256, "Capture schema SHA-256 differs from the reviewed schema")
    schema = parse_json_bytes(schema_bytes, "capture schema", object_only=True)
    require(schema.get("schemaVersion") == 1, "Unexpected schemaVersion")
    require(schema.get("artifactFormat") == "luckbound-v5-source-capture", "Unexpected artifact format")
    require(schema.get("captureKind") == "reconstructed-synthetic-from-versioned-v5-source",
            "Capture kind must remain reconstructed synthetic from versioned V5 source")
    authority = schema.get("authority")
    require(isinstance(authority, dict), "Missing schema authority")
    require(authority.get("reviewContractSha256") == CONTRACT_SHA256, "Review-contract identity changed")
    addendum = authority.get("reviewContractAddendum")
    require(isinstance(addendum, dict) and addendum.get("sha256") == ADDENDUM_SHA256,
            "Review-contract addendum identity changed")
    require(addendum.get("verifierPath") == "Tools/Automation/verify_v5_source_capture.py",
            "Verifier delivery path changed")
    require(authority.get("historicalCommit") == HISTORICAL_COMMIT, "Historical commit changed")
    require(authority.get("historicalTree") == HISTORICAL_TREE, "Historical tree changed")
    require(authority.get("historicalContentVersion") == 5, "Historical content version must be 5")
    require(authority.get("historicalSchemaVersion") == 1, "Historical schema version must be 1")
    require(authority.get("historicalEditor") == HISTORICAL_EDITOR, "Historical Editor changed")
    proof = authority.get("fullTreeProof")
    require(isinstance(proof, dict), "Missing full-tree proof identity")
    require(proof.get("path") == ".github/v5-source-capture/ORIGINAL-TREE-PROOF.json",
            "Full-tree proof path changed")
    require(proof.get("localEvidencePath") == "Logs/V5HistoricalProject-20261030-r2/ORIGINAL-TREE-PROOF.json",
            "Full-tree proof local evidence identity changed")
    require(proof.get("sha256") == FULL_TREE_PROOF_SHA256, "Full-tree proof hash changed")
    require(proof.get("trackedBlobCount") == FULL_TREE_BLOB_COUNT, "Full-tree proof blob count changed")
    require(proof.get("sumOriginalBlobBytes") == FULL_TREE_BYTES, "Full-tree proof byte total changed")
    header = schema.get("fixedInputHeader")
    require(isinstance(header, dict), "Missing fixed input header")
    require(header == {
        "schemaVersion": 1,
        "contentVersion": 5,
        "generation": 1,
        "sequence": 11,
        "savedAtUtc": "2000-01-01T00:00:00Z",
        "seed": 17320508,
    }, "Fixed input header changed")
    cultures = schema.get("cultures")
    require(isinstance(cultures, list) and [c.get("name") for c in cultures if isinstance(c, dict)] == ["en-US", "es-ES"],
            "Expected en-US and es-ES capture cultures")
    require(all(c.get("caseCount") == 12 and c.get("invocationCount") == 12 for c in cultures),
            "Culture invocation counts changed")
    cases = schema.get("cases")
    require(isinstance(cases, list) and len(cases) == 12, "Expected exactly twelve V5 case definitions")
    ids = [case.get("id") for case in cases if isinstance(case, dict)]
    require(len(ids) == 12 and len(set(ids)) == 12, "Schema case IDs are missing or duplicated")
    expected_roles = {
        "P0-no-active": ("positive-api-and-codec", False, "valid"),
        "P1-known-zero": ("positive-api-and-codec", True, "valid"),
        "P1-known-nontrivial": ("positive-api-and-codec", True, "valid"),
        "P2-unknown-null": ("positive-api-and-codec", True, "valid"),
        "P2-unknown-inline": ("positive-api-and-codec", True, "valid"),
        "P3-receipts-completed": ("positive-api-and-codec", True, "valid"),
        "B0-arrays-null": ("boundary-observation", False, "record-observed-result"),
        "B1-arrays-empty": ("boundary-observation", False, "record-observed-result"),
        "M0-false-marker-payload": ("synthetic-marker-negative", False, "rejected"),
        "M1-true-marker-null": ("synthetic-marker-negative", True, "rejected"),
        "S0-settings": ("positive-settings-api-and-codec", False, "valid"),
        "N0-tampered": ("checksum-negative", True, "corrupted"),
    }
    require(set(ids) == set(expected_roles), "Pinned V5 case ID set changed")
    for case in cases:
        role, active, decode = expected_roles[case["id"]]
        require(case.get("role") == role and case.get("active") is active
                and case.get("expectedDecode") == decode and case.get("mustBeCultureInvariant") is True,
                f"Pinned role/active/Decode expectation changed: {case['id']}")
    n0 = next(case for case in cases if case["id"] == "N0-tampered")
    require(n0.get("derivedFrom") == "P3-receipts-completed"
            and n0.get("tamper") == {
                "field": "availableRenown", "from": 33, "to": 34, "preserveOriginalChecksum": True,
            }, "N0 mutation contract changed")
    require(schema.get("expectedTotalCases") == 12 and schema.get("expectedTotalInvocations") == 24,
            "Expected capture matrix is not 12 cases x 2 cultures")
    require(isinstance(schema.get("requiredOriginalSources"), list)
            and len(schema["requiredOriginalSources"]) == 21
            and len(set(schema["requiredOriginalSources"])) == 21,
            "Expected the exact 21-source V5 provenance minimum")
    return schema


def _read_full_tree_proof(project_root: Path, proof_identity: dict[str, Any]) -> tuple[Path, dict[str, Any]]:
    proof_relative = proof_identity.get("path")
    require(proof_relative == ".github/v5-source-capture/ORIGINAL-TREE-PROOF.json",
            "Full-tree proof must use its frozen CI path")
    proof_path = safe_file(project_root, proof_relative, "full-tree proof")
    payload = proof_path.read_bytes()
    require(sha256_bytes(payload) == FULL_TREE_PROOF_SHA256, "Full-tree proof SHA-256 mismatch")
    proof = parse_json_bytes(payload, "full-tree proof", object_only=True)
    require(proof.get("historicalCommit") == HISTORICAL_COMMIT, "Full-tree proof commit mismatch")
    require(proof.get("historicalTree") == HISTORICAL_TREE, "Full-tree proof tree mismatch")
    files = proof.get("files")
    require(isinstance(files, list) and len(files) == FULL_TREE_BLOB_COUNT, "Full-tree proof file count mismatch")
    paths: set[str] = set()
    total = 0
    for record in files:
        require(isinstance(record, dict), "Invalid full-tree proof record")
        relative = record.get("path")
        require(isinstance(relative, str) and relative not in paths, "Missing/duplicate full-tree path")
        paths.add(relative)
        size = record.get("bytes")
        require(type(size) is int and size >= 0, f"Invalid full-tree size: {relative}")
        digest = record.get("sha256")
        require(isinstance(digest, str) and SHA256_RE.fullmatch(digest), f"Invalid full-tree hash: {relative}")
        total += size
    require(total == FULL_TREE_BYTES, "Full-tree proof byte total mismatch")
    return proof_path, proof


def source_inventory(entries: Any, expected_paths: set[str], project_root: Path, label: str) -> dict[str, tuple[int, str]]:
    require(isinstance(entries, list), f"Missing {label} inventory")
    observed: dict[str, tuple[int, str]] = {}
    for entry in entries:
        require(isinstance(entry, dict), f"Invalid {label} record")
        relative = entry.get("path")
        require(isinstance(relative, str) and relative not in observed, f"Missing/duplicate {label} path")
        size = entry.get("bytes")
        digest = entry.get("sha256")
        require(type(size) is int and size >= 0, f"Invalid {label} size: {relative}")
        require(isinstance(digest, str) and SHA256_RE.fullmatch(digest), f"Invalid {label} SHA-256: {relative}")
        source = safe_file(project_root, relative, label)
        raw = source.read_bytes()
        require(len(raw) == size, f"{label.title()} size differs from execution tree: {relative}")
        require(sha256_bytes(raw) == digest, f"{label.title()} hash differs from execution tree: {relative}")
        observed[relative] = (size, digest)
    require(set(observed) == expected_paths,
            f"{label.title()} path set differs; missing={sorted(expected_paths - set(observed))}, "
            f"extra={sorted(set(observed) - expected_paths)}")
    return observed


def checked_output_records(capture_dir: Path, entries: Any) -> dict[str, dict[str, Any]]:
    require(isinstance(entries, list), "Missing output-file inventory")
    found: dict[str, dict[str, Any]] = {}
    for entry in entries:
        require(isinstance(entry, dict), "Invalid output-file record")
        relative = entry.get("path")
        require(isinstance(relative, str) and relative not in found, "Missing/duplicate output path")
        path = safe_file(capture_dir, relative, "capture output")
        size = entry.get("bytes")
        digest = entry.get("sha256")
        require(type(size) is int and size >= 0, f"Invalid output size: {relative}")
        require(isinstance(digest, str) and SHA256_RE.fullmatch(digest), f"Invalid output SHA-256: {relative}")
        raw = path.read_bytes()
        require(len(raw) == size, f"Output byte length mismatch: {relative}")
        require(sha256_bytes(raw) == digest, f"Output SHA-256 mismatch: {relative}")
        encoding = entry.get("encoding")
        require(isinstance(encoding, str) and encoding.strip(), f"Missing output encoding annotation: {relative}")
        if Path(relative).name in JSON_OUTPUTS or relative.endswith(".utf8"):
            require(not raw.startswith(b"\xef\xbb\xbf"), f"UTF-8 BOM in output: {relative}")
            try:
                raw.decode("utf-8", errors="strict")
            except UnicodeDecodeError as exc:
                raise CaptureError(f"Invalid UTF-8 output: {relative}") from exc
        if Path(relative).name in JSON_OUTPUTS:
            parse_json_bytes(raw, relative, object_only=True)
        if Path(relative).name == "checksum.txt":
            require(encoding == "ascii-lower-hex-no-newline", f"Unexpected checksum encoding annotation: {relative}")
            try:
                checksum_text = raw.decode("ascii", errors="strict")
            except UnicodeDecodeError as exc:
                raise CaptureError(f"Checksum is not ASCII: {relative}") from exc
            require(SHA256_RE.fullmatch(checksum_text) is not None,
                    f"Checksum must be 64 lowercase hex bytes without newline: {relative}")
        elif Path(relative).name in JSON_OUTPUTS or relative.endswith(".utf8"):
            require(encoding == "utf-8-no-bom", f"Unexpected UTF-8 output annotation: {relative}")
        found[relative] = entry
    return found


def _walk_capture_tree(root: Path) -> tuple[set[str], set[str]]:
    files: set[str] = set()
    directories: set[str] = set()

    def walk(current: Path, relative_dir: str) -> None:
        try:
            children = list(os.scandir(current))
        except OSError as exc:
            raise CaptureError(f"Cannot enumerate capture directory {current}: {exc}") from exc
        for child in children:
            item = current / child.name
            require(not _is_reparse_or_symlink(item), f"Symlink/reparse point in capture tree: {item}")
            relative = f"{relative_dir}/{child.name}" if relative_dir else child.name
            try:
                info = child.stat(follow_symlinks=False)
            except OSError as exc:
                raise CaptureError(f"Cannot stat capture entry {item}: {exc}") from exc
            if stat.S_ISDIR(info.st_mode):
                directories.add(relative)
                walk(item, relative)
            else:
                require(stat.S_ISREG(info.st_mode), f"Non-regular file in capture tree: {relative}")
                files.add(relative)

    walk(root, "")
    return files, directories


def _case_output_names(case: dict[str, Any]) -> set[str]:
    names = set(BASE_CASE_OUTPUTS)
    if case.get("role") in POSITIVE_ROLES:
        names.update(API_OUTPUTS)
    # The harness records source strings for every profile-shaped input,
    # including the two deliberate marker conflicts and the N0 mutation.
    if case.get("id") != "S0-settings":
        names.add("active-strings.json")
    return names


def validate_case_rows(rows: Any, schema: dict[str, Any]) -> dict[tuple[str, str], dict[str, Any]]:
    require(isinstance(rows, list) and len(rows) == 24, "fixture-index must contain exactly 24 invocation records")
    case_definitions = {case["id"]: case for case in schema["cases"]}
    cultures = [culture["name"] for culture in schema["cultures"]]
    expected_pairs = {(case_id, culture) for case_id in case_definitions for culture in cultures}
    found: dict[tuple[str, str], dict[str, Any]] = {}
    for row in rows:
        require(isinstance(row, dict), "Invalid fixture-index case record")
        case_id, culture = row.get("caseId"), row.get("culture")
        pair = (case_id, culture)
        require(pair in expected_pairs, f"Unexpected V5 case/culture pair: {pair!r}")
        require(pair not in found, f"Duplicate V5 case/culture pair: {pair!r}")
        definition = case_definitions[case_id]
        require(row.get("role") == definition["role"], f"Case role changed: {case_id}/{culture}")
        require(type(row.get("hasActiveExpedition")) is bool
                and row["hasActiveExpedition"] is definition["active"],
                f"Active marker differs from schema: {case_id}/{culture}")
        expected_payload_present = CANDIDATE_ACTIVE_PAYLOAD_PRESENT[case_id]
        require(type(row.get("activePayloadPresent")) is bool
                and row["activePayloadPresent"] is expected_payload_present,
                f"Active payload-presence role differs from schema: {case_id}/{culture}")
        found[pair] = row
    require(set(found) == expected_pairs,
            f"Case/culture matrix incomplete: missing={sorted(expected_pairs - set(found))}")
    return found


def _capture_expected_files(case_rows: dict[tuple[str, str], dict[str, Any]], schema: dict[str, Any]) -> tuple[set[str], set[str]]:
    expected_files = {"fixture-index.json"}
    expected_dirs: set[str] = set()
    case_defs = {case["id"]: case for case in schema["cases"]}
    for (case_id, culture), row in case_rows.items():
        names = _case_output_names(case_defs[case_id])
        expected_paths = {f"cultures/{culture}/{case_id}/{name}" for name in names}
        actual_paths = set()
        records = row.get("outputs")
        require(isinstance(records, list), f"Missing outputs array: {case_id}/{culture}")
        for item in records:
            require(isinstance(item, dict), f"Invalid output record: {case_id}/{culture}")
            path = item.get("path")
            require(isinstance(path, str) and path not in actual_paths,
                    f"Missing/duplicate output record path: {case_id}/{culture}")
            actual_paths.add(path)
        require(actual_paths == expected_paths,
                f"Output path set differs for {case_id}/{culture}; "
                f"missing={sorted(expected_paths - actual_paths)}, extra={sorted(actual_paths - expected_paths)}")
        expected_files.update(expected_paths)
        expected_dirs.update({"cultures", f"cultures/{culture}", f"cultures/{culture}/{case_id}"})
    return expected_files, expected_dirs


def _parse_checksum_file(path: Path) -> str:
    raw = path.read_bytes()
    try:
        text = raw.decode("ascii", errors="strict")
    except UnicodeDecodeError as exc:
        raise CaptureError(f"Checksum is not ASCII: {path}") from exc
    require(SHA256_RE.fullmatch(text) is not None, f"Checksum must be 64 lowercase hex bytes without newline: {path}")
    return text


def verify_checksum_relation(
    case_id: str,
    write_preimage: bytes,
    checksum_text: str,
    envelope_checksum: Any,
    *,
    p3_checksum: str | None = None,
    p3_write_preimage: bytes | None = None,
) -> str:
    """Check checksums from captured bytes only; never rebuild a preimage."""
    require(SHA256_RE.fullmatch(checksum_text) is not None, f"Malformed stored checksum: {case_id}")
    require(envelope_checksum == checksum_text, f"Envelope/checksum.txt differ: {case_id}")
    actual = sha256_bytes(write_preimage)
    if case_id == "N0-tampered":
        require(isinstance(p3_checksum, str) and SHA256_RE.fullmatch(p3_checksum), "N0 is missing P3 checksum")
        require(isinstance(p3_write_preimage, bytes), "N0 is missing P3 preimage bytes")
        require(checksum_text == p3_checksum, "N0 did not retain P3's stored checksum")
        require(write_preimage != p3_write_preimage, "N0 preimage bytes did not change from P3")
        require(actual != checksum_text, "N0 checksum unexpectedly matches its tampered preimage")
    else:
        require(case_id in ENCODED_WRITE_CASES, f"Unexpected checksum relation case: {case_id}")
        require(actual == checksum_text, f"Captured write-preimage SHA-256 mismatch: {case_id}")
    return actual


def verify_read_checksum_relation(
    case_id: str,
    read_preimage: bytes,
    checksum_text: str,
    *,
    p3_checksum: str | None = None,
    p3_read_preimage: bytes | None = None,
) -> tuple[str, bool]:
    """Check the captured read preimage as bytes; negative roles remain observations."""
    require(SHA256_RE.fullmatch(checksum_text) is not None, f"Malformed stored checksum: {case_id}")
    actual = sha256_bytes(read_preimage)
    matches = actual == checksum_text
    if case_id == "N0-tampered":
        require(isinstance(p3_checksum, str) and SHA256_RE.fullmatch(p3_checksum), "N0 is missing P3 checksum")
        require(isinstance(p3_read_preimage, bytes), "N0 is missing P3 read preimage bytes")
        require(checksum_text == p3_checksum, "N0 did not retain P3's stored checksum")
        require(read_preimage != p3_read_preimage, "N0 read preimage bytes did not change from P3")
        require(not matches, "N0 read-preimage SHA-256 unexpectedly matches its retained checksum")
    elif case_id in {"P0-no-active", "P1-known-zero", "P1-known-nontrivial", "P2-unknown-null",
                     "P2-unknown-inline", "P3-receipts-completed", "S0-settings"}:
        require(matches, f"Captured positive read-preimage SHA-256 mismatch: {case_id}")
    return actual, matches


def _without_top_level_keys(value: dict[str, Any], keys: set[str]) -> dict[str, Any]:
    """Remove declared envelope header keys only at the envelope root."""
    return {key: child for key, child in value.items() if key not in keys}


def frozen_upgrade_wire_representation(raw_json: bytes, label: str) -> str:
    """Return the recorded raw JSON token without rebuilding the serialized document."""
    try:
        text = raw_json.decode("utf-8", errors="strict")
    except UnicodeDecodeError as exc:
        raise CaptureError(f"Invalid UTF-8 in {label}") from exc
    match = FROZEN_UPGRADE_WIRE_RE.search(text)
    return match.group(1).strip() if match else "omitted"


def _validate_recipe(
    recipe: dict[str, Any],
    row: dict[str, Any],
    case: dict[str, Any],
    schema: dict[str, Any],
    envelope_bytes: bytes,
    label: str,
) -> None:
    case_id = case["id"]
    require("culture" not in recipe, f"Recipe must be culture-free and byte-invariant: {label}")
    require(recipe.get("schemaVersion") == 1 and recipe.get("caseId") == case_id
            and recipe.get("role") == case["role"], f"Recipe identity mismatch: {label}")
    require(recipe.get("seed") == schema["fixedInputHeader"]["seed"], f"Recipe seed mismatch: {label}")
    require(recipe.get("fixedInputHeader") == schema["fixedInputHeader"]
            and recipe.get("fixedIds") == schema["fixedIds"], f"Recipe fixed inputs mismatch: {label}")
    expected = recipe.get("expected")
    require(isinstance(expected, dict), f"Recipe has no expectation object: {label}")
    recipe_decode = "corrupted" if case.get("expectedDecode") == "rejected" else case.get("expectedDecode")
    require(expected.get("decode") == recipe_decode, f"Recipe Decode expectation mismatch: {label}")
    require(expected.get("hasActiveExpedition") is row.get("hasActiveExpedition")
            and expected.get("activePayloadPresent") is row.get("activePayloadPresent"),
            f"Recipe active-state metadata differs from fixture-index: {label}")

    if case_id == "N0-tampered":
        tamper = case["tamper"]
        require(expected.get("derivedFrom") == case.get("derivedFrom")
                and expected.get("tamperField") == tamper["field"]
                and expected.get("tamperFrom") == tamper["from"]
                and expected.get("tamperTo") == tamper["to"],
                f"N0 recipe mutation metadata differs from pinned contract: {label}")
    else:
        require(expected.get("derivedFrom") == "" and expected.get("tamperField") == ""
                and expected.get("tamperFrom") == 0 and expected.get("tamperTo") == 0,
                f"Unexpected recipe derivation/tamper metadata: {label}")

    expected_p2_inputs = {
        "P2-unknown-null": "null",
        "P2-unknown-inline": "zero-inline",
    }
    p2_input = expected_p2_inputs.get(case_id, "not-applicable")
    require(expected.get("inputFrozenUpgradeRepresentation") == p2_input,
            f"Recipe P2 input representation mismatch: {label}")
    clone_snapshot = "null" if case_id in expected_p2_inputs else "not-applicable"
    require(expected.get("writerCloneMemorySnapshot") == clone_snapshot,
            f"Recipe writer Clone snapshot mismatch: {label}")
    if case_id in expected_p2_inputs:
        measured_wire = frozen_upgrade_wire_representation(envelope_bytes, f"{label}/envelope.json")
        require(isinstance(expected.get("fixedEnvelopeWireFrozenUpgradeRepresentation"), str)
                and expected["fixedEnvelopeWireFrozenUpgradeRepresentation"] == measured_wire,
                f"Recipe fixed-envelope wire measurement differs from captured envelope: {label}")
    else:
        require(expected.get("fixedEnvelopeWireFrozenUpgradeRepresentation") == "not-applicable",
                f"Unexpected fixed-envelope wire measurement: {label}")

    expected_array_input = {
        "B0-arrays-null": "contractCounters:null;progressionPurchaseTransactions:null",
        "B1-arrays-empty": "contractCounters:empty;progressionPurchaseTransactions:empty",
    }.get(case_id, "not-applicable")
    require(expected.get("boundaryArraysInputRepresentation") == expected_array_input,
            f"Recipe boundary input representation mismatch: {label}")


def compare_api_and_fixed_envelopes(api_envelope: dict[str, Any], fixed_envelope: dict[str, Any], label: str) -> None:
    require(_without_top_level_keys(api_envelope, API_HEADER_FIELDS)
            == _without_top_level_keys(fixed_envelope, API_HEADER_FIELDS),
            f"Raw API and fixed-header payloads differ outside declared envelope headers: {label}")
    require(api_envelope.get("schemaVersion") == 1, f"Raw API schemaVersion mismatch: {label}")
    require(api_envelope.get("contentVersion") == 5, f"Raw API contentVersion mismatch: {label}")
    require(api_envelope.get("generation") == 1, f"Raw API generation mismatch: {label}")
    require(api_envelope.get("sequence") == 1, f"Raw API sequence mismatch: {label}")
    require(isinstance(api_envelope.get("checksum"), str)
            and SHA256_RE.fullmatch(api_envelope["checksum"]), f"Raw API checksum is malformed: {label}")
    api_date = api_envelope.get("savedAtUtc")
    require(isinstance(api_date, str) and UTC_TIMESTAMP_RE.fullmatch(api_date),
            f"Invalid observed API savedAtUtc: {label}")
    require(api_date != "2000-01-01T00:00:00Z", f"API output appears to use the fixture's fixed date: {label}")


def _validate_api_observation(
    observation: dict[str, Any], api: dict[str, Any], fixed: dict[str, Any],
    case_id: str, culture: str, api_bytes: bytes, fixed_bytes: bytes, label: str,
) -> None:
    expected_kind = "settings" if case_id == "S0-settings" else "profile"
    require(observation.get("caseId") == case_id and observation.get("culture") == culture,
            f"API observation identity mismatch: {label}")
    require(observation.get("apiKind") == expected_kind, f"API observation kind mismatch: {label}")
    require(observation.get("apiSavedAtUtc") == api.get("savedAtUtc"), f"API observed timestamp mismatch: {label}")
    require(observation.get("apiGeneration") == api.get("generation") == 1,
            f"API observed generation mismatch: {label}")
    require(observation.get("apiSequence") == api.get("sequence") == 1,
            f"API observed sequence mismatch: {label}")
    require(observation.get("apiChecksum") == api.get("checksum"), f"API observed checksum mismatch: {label}")
    require(observation.get("fixedSavedAtUtc") == fixed.get("savedAtUtc") == "2000-01-01T00:00:00Z",
            f"Fixed-header observation timestamp mismatch: {label}")
    require(observation.get("fixedGeneration") == fixed.get("generation") == 1,
            f"Fixed-header observation generation mismatch: {label}")
    require(observation.get("fixedSequence") == fixed.get("sequence") == 11,
            f"Fixed-header observation sequence mismatch: {label}")
    require(observation.get("onlyHeaderFieldsDiffer") is True and observation.get("payloadsEqual") is True,
            f"API observation does not establish header-only difference: {label}")
    expected_differences = [field for field in sorted(HEADER_FIELDS) if api.get(field) != fixed.get(field)]
    require(expected_differences == ["savedAtUtc", "sequence"],
            f"Unexpected API/fixed header differences: {label}: {expected_differences}")
    require(observation.get("differingHeaderFields") == expected_differences,
            f"API observation differingHeaderFields mismatch: {label}")
    if expected_kind == "settings":
        api_wire = fixed_wire = "not-applicable"
    else:
        api_wire = frozen_upgrade_wire_representation(api_bytes, f"{label}/api-envelope.json")
        fixed_wire = frozen_upgrade_wire_representation(fixed_bytes, f"{label}/envelope.json")
    require(observation.get("apiFrozenUpgradeWireRepresentation") == api_wire,
            f"API frozen-upgrade wire observation mismatch: {label}")
    require(observation.get("fixedFrozenUpgradeWireRepresentation") == fixed_wire,
            f"Fixed frozen-upgrade wire observation mismatch: {label}")


def _validate_decode(row: dict[str, Any], result: dict[str, Any], case: dict[str, Any], label: str) -> None:
    indexed = row.get("decode")
    require(isinstance(indexed, dict), f"Missing decode observation: {label}")
    status = indexed.get("status")
    accepted = indexed.get("accepted")
    require(isinstance(status, str) and status, f"Missing decode status: {label}")
    require(type(accepted) is bool, f"Invalid decode acceptance: {label}")
    result_status = result.get("status")
    enum_status = result.get("statusEnum")
    normalized = result.get("statusNormalized")
    require(result.get("caseId") == row.get("caseId") and result.get("culture") == row.get("culture"),
            f"decode-result identity mismatch: {label}")
    require(isinstance(result_status, str) and result_status == result_status.casefold()
            and normalized == result_status and enum_status in DECODE_ENUMS.values()
            and DECODE_ENUMS.get(normalized) == enum_status,
            f"decode-result raw/normalized enum mapping mismatch: {label}")
    require(status == normalized and result.get("accepted") is accepted,
            f"decode-result.json and fixture-index disagree: {label}")
    require(type(result.get("decodedEnvelopePresent")) is bool and isinstance(result.get("message"), str),
            f"decode-result observation is incomplete: {label}")
    require(accepted is (enum_status == "Valid"),
            f"Decode acceptance does not match the historical enum: {label}")
    expected = case.get("expectedDecode")
    if expected == "valid":
        require(accepted and normalized == "valid" and enum_status == "Valid",
                f"Expected valid Decode result: {label}")
    elif expected == "rejected":
        require(not accepted and normalized == "corrupted" and enum_status == "Corrupted",
                f"Expected historical Corrupted result for marker-negative role: {label}")
    elif expected == "corrupted":
        require(not accepted and normalized == "corrupted" and enum_status == "Corrupted",
                f"Expected Corrupted Decode result: {label}")
    elif expected == "record-observed-result":
        require(enum_status in DECODE_ENUMS.values(), f"Boundary result is not a historical DecodeStatus: {label}")
    else:
        raise CaptureError(f"Unknown expectedDecode value in pinned schema: {expected!r}")


def _matches_exact_typed_value(actual: Any, expected: Any) -> bool:
    """Match a fixed JSON shape without Python's bool/int equality aliases."""
    if type(expected) is dict:
        return (
            type(actual) is dict
            and actual.keys() == expected.keys()
            and all(_matches_exact_typed_value(actual[key], expected[key]) for key in expected)
        )
    if type(expected) is list:
        return (
            type(actual) is list
            and len(actual) == len(expected)
            and all(_matches_exact_typed_value(actual_item, expected_item)
                    for actual_item, expected_item in zip(actual, expected))
        )
    return type(actual) is type(expected) and actual == expected


def _matches_declared_placeholder_dto_shape(payload: Any) -> bool:
    if type(payload) is not dict or payload.keys() != PLACEHOLDER_DTO_FIELDS["ExpeditionSaveData"]:
        return False
    for field in payload:
        child_dto = PLACEHOLDER_CHILD_DTO.get(("ExpeditionSaveData", field))
        child = payload[field]
        if child_dto is None:
            if type(child) is dict:
                return False
            continue
        if type(child) is not dict or child.keys() != PLACEHOLDER_DTO_FIELDS[child_dto]:
            return False
    return True


def is_empty_expedition_placeholder(payload: Any) -> bool:
    """Accept only the complete, typed historical V5 absent-candidate DTO value."""
    return (
        _matches_declared_placeholder_dto_shape(payload)
        and _matches_exact_typed_value(payload, V5_EMPTY_EXPEDITION_PLACEHOLDER)
    )


def _validate_m0_payload_matches_p1(p1_payload: Any, m0_payload: Any, label: str) -> None:
    require(isinstance(p1_payload, dict) and isinstance(m0_payload, dict),
            f"P1/M0 active payload missing: {label}")
    require(m0_payload == p1_payload,
            f"M0 payload differs from the exact P1-known-zero recipe: {label}")


def _verify_envelope_shape(case_id: str, case: dict[str, Any], envelope: dict[str, Any], schema: dict[str, Any], label: str) -> None:
    fixed = schema["fixedInputHeader"]
    for key in HEADER_FIELDS:
        require(envelope.get(key) == fixed[key], f"Fixed header {key} mismatch: {label}")
    require(isinstance(envelope.get("checksum"), str) and SHA256_RE.fullmatch(envelope["checksum"]),
            f"Malformed envelope checksum: {label}")
    if case_id == "S0-settings":
        settings = envelope.get("settings")
        require(isinstance(settings, dict), "Settings envelope has no settings payload")
        require("profile" not in envelope and "activeExpedition" not in envelope and "hasActiveExpedition" not in envelope,
                "Settings envelope contains profile-only fields")
        for name, expected in (("languageId", "es"), ("masterVolume", 0.83), ("musicVolume", 0.57),
                               ("effectsVolume", 0.31), ("ambienceVolume", 0.19)):
            actual = settings.get(name)
            require(isinstance(actual, (int, float)) if name != "languageId" else isinstance(actual, str),
                    f"Settings value is missing: {name}")
            if name == "languageId":
                require(actual == expected, f"S0 settings language mismatch: {label}")
            else:
                require(math.isclose(float(actual), expected, rel_tol=0.0, abs_tol=1e-6),
                        f"S0 settings volume mismatch: {name}/{label}")
        return

    require(isinstance(envelope.get("profile"), dict), f"Profile envelope has no profile payload: {label}")
    profile = envelope["profile"]
    marker = envelope.get("hasActiveExpedition")
    require(type(marker) is bool and marker is case.get("active"), f"Active marker differs from schema: {label}")
    require("activeExpedition" in envelope, f"Profile envelope omits the activeExpedition wire member: {label}")
    active_payload = envelope["activeExpedition"]
    require(active_payload is None or isinstance(active_payload, dict),
            f"activeExpedition wire member must be null or a DTO object: {label}")
    if case_id in NO_CANDIDATE_PAYLOAD_CASES:
        require(active_payload is None or is_empty_expedition_placeholder(active_payload),
                f"No-candidate active payload is neither null nor a valid empty V5 placeholder: {label}")
    else:
        require(isinstance(active_payload, dict), f"Real active payload is missing: {label}")
        require(active_payload.get("seed") == fixed["seed"],
                f"Real active expedition seed differs from fixed input: {label}")
        require(not is_empty_expedition_placeholder(active_payload),
                f"Real active payload unexpectedly has only placeholder values: {label}")
    if case_id == "P0-no-active":
        for name in ("acceptedContractIds", "claimedContractIds", "readStoryFragmentIds", "contractCounters",
                     "processedSettlementRunIds", "progressionPurchaseTransactions"):
            require(profile.get(name) == [], f"P0 {name} must be empty: {label}")
    if case_id == "P1-known-zero":
        require(tuple(profile.get(name) for name in
                      ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (0, 0, 0),
                f"P1-known-zero profile upgrades changed: {label}")
        require(isinstance(active_payload, dict) and active_payload.get("hasFrozenPermanentUpgrades") is True,
                f"P1-known-zero active snapshot must retain its known-upgrade marker: {label}")
        frozen = active_payload.get("frozenPermanentUpgrades")
        require(isinstance(frozen, dict) and tuple(frozen.get(name) for name in
                ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (0, 0, 0),
                f"P1-known-zero active frozen snapshot levels changed: {label}")
    if case_id == "M0-false-marker-payload":
        require(active_payload.get("hasFrozenPermanentUpgrades") is True,
                f"M0 P1-derived payload must retain the known-upgrade marker: {label}")
        frozen = active_payload.get("frozenPermanentUpgrades")
        require(isinstance(frozen, dict) and tuple(frozen.get(name) for name in
                ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (0, 0, 0),
                f"M0 P1-derived frozen snapshot levels changed: {label}")
    if case_id == "P1-known-nontrivial":
        require(tuple(profile.get(name) for name in
                      ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (2, 2, 1),
                f"P1-known-nontrivial profile upgrades changed: {label}")
        require(type(profile.get("availableRenown")) is int and profile["availableRenown"] > 0,
                f"P1-known-nontrivial resources must be positive: {label}")
        require(isinstance(active_payload, dict) and active_payload.get("hasFrozenPermanentUpgrades") is True,
                f"P1-known-nontrivial active snapshot must retain its known-upgrade marker: {label}")
        frozen = active_payload.get("frozenPermanentUpgrades")
        require(isinstance(frozen, dict) and tuple(frozen.get(name) for name in
                ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (2, 2, 1),
                f"P1-known-nontrivial active frozen snapshot levels changed: {label}")
    if case_id in {"P2-unknown-null", "P2-unknown-inline"}:
        require(isinstance(active_payload, dict), f"P2 active payload is missing: {label}")
        require(active_payload.get("hasFrozenPermanentUpgrades") is False,
                f"P2 unknown-upgrade marker must remain false: {label}")
    if case_id == "P3-receipts-completed":
        require(profile.get("availableRenown") == 33, "P3 availableRenown must be 33")
        require(tuple(profile.get(name) for name in
                      ("healthUpgradeLevel", "energyUpgradeLevel", "additionalPotionUpgradeLevel")) == (2, 2, 1),
                f"P3 profile upgrades changed: {label}")
        _verify_p3_receipts(envelope, schema, label)
    if case_id == "N0-tampered":
        require(profile.get("availableRenown") == 34, "N0 availableRenown must be 34")


def _verify_p3_receipts(envelope: dict[str, Any], schema: dict[str, Any], label: str) -> None:
    profile = envelope["profile"]
    for name in ("progressionPurchaseTransactions", "contractCounters", "processedSettlementRunIds"):
        value = profile.get(name)
        require(isinstance(value, list) and value, f"P3 {name} must be non-empty: {label}")
    active = envelope.get("activeExpedition")
    require(isinstance(active, dict), f"P3 active expedition is missing: {label}")
    fortune = active.get("fortuneOfferBookSnapshot")
    require(isinstance(fortune, dict), f"P3 Fortune snapshot is missing: {label}")
    for name in ("offers", "rerollReceipts", "fortuneBrewReceipts"):
        value = fortune.get(name)
        require(isinstance(value, list) and value, f"P3 Fortune {name} must be non-empty: {label}")
    require(any(isinstance(offer, dict) and isinstance(offer.get("history"), list) and offer["history"]
                for offer in fortune["offers"]), f"P3 Fortune offer history must be non-empty: {label}")
    inventory_json = active.get("inventorySnapshotJson")
    require(isinstance(inventory_json, str) and inventory_json, f"P3 inventory source string is missing: {label}")
    inventory = parse_json_bytes(inventory_json.encode("utf-8"), f"{label}/inventorySnapshotJson", object_only=True)
    transactions = inventory.get("transactions")
    materials = inventory.get("materials")
    require(isinstance(transactions, list) and transactions, f"P3 inventory transactions must be non-empty: {label}")
    expected_transaction = schema["fixedIds"]["inventoryTransactionGuidN"]
    require(any(isinstance(item, dict) and item.get("transactionId") == expected_transaction
                for item in transactions), f"P3 inventory receipt lacks its fixed Core TransactionId: {label}")
    require(isinstance(materials, list) and materials, f"P3 inventory materials must be non-empty: {label}")
    require(any(isinstance(item, dict) and type(item.get("count")) is int and item["count"] > 0
                for item in materials), f"P3 inventory has no positive material count: {label}")
    has_stack = any(isinstance(inventory.get(slot), dict) for slot in ("weapon", "armor", "relicOne", "relicTwo"))
    backpack = inventory.get("backpackSlots")
    has_stack = has_stack or isinstance(backpack, list) and any(isinstance(item, dict) for item in backpack)
    require(has_stack, f"P3 inventory must contain at least one stack: {label}")
    results = active.get("completedRoomResults")
    require(isinstance(results, list) and results, f"P3 completed-room result must be non-empty: {label}")
    checkpoint = active.get("roomEntryCheckpointJson")
    require(checkpoint in (None, ""), f"P3 checkpoint must be empty: {label}")


def _without_p3_renown_and_checksum(envelope: dict[str, Any]) -> dict[str, Any]:
    result = dict(envelope)
    profile = result.get("profile")
    if isinstance(profile, dict):
        profile_copy = dict(profile)
        profile_copy.pop("availableRenown", None)
        result["profile"] = profile_copy
    result.pop("checksum", None)
    return result


def _validate_active_strings(active_strings: dict[str, Any], envelope: dict[str, Any], case_id: str,
                             candidate_payload_present: bool, label: str) -> None:
    """Validate auxiliary exports against raw wire presence or declared candidate absence."""
    expected_active_string_fields = {
        "caseId", "hasActiveExpedition", "activePayloadPresent", "routeSnapshotJson",
        "inventorySnapshotJson", "roomEntryCheckpointJson", "completedRoomResults", "provenance",
    }
    require(set(active_strings) == expected_active_string_fields,
            f"active-strings fields differ from the frozen artifact shape: {label}")
    require("culture" not in active_strings and active_strings.get("caseId") == case_id,
            f"active-strings identity mismatch: {label}")
    require("activeExpedition" in envelope,
            f"Cannot determine active-string wire presence from an absent envelope member: {label}")
    active = envelope["activeExpedition"]
    active_present = isinstance(active, dict)
    require(active is None or active_present,
            f"Cannot determine active-string wire presence from a non-DTO envelope value: {label}")
    require(active_strings.get("hasActiveExpedition") is envelope.get("hasActiveExpedition"),
            f"active-strings marker differs from envelope: {label}")
    require(active_strings.get("activePayloadPresent") is active_present,
            f"active-strings wire-payload presence differs from raw envelope JSON: {label}")
    source_fields = ("routeSnapshotJson", "inventorySnapshotJson", "roomEntryCheckpointJson")
    provenance = active_strings.get("provenance")
    require(isinstance(provenance, dict), f"Missing active-string provenance object: {label}")
    expected_provenance_fields = {*source_fields, "completedRoomResults"}
    require(set(provenance) == expected_provenance_fields,
            f"active-string provenance fields differ from the frozen artifact shape: {label}")
    if not candidate_payload_present:
        for name in source_fields:
            require(active_strings.get(name) is None or active_strings.get(name) == "",
                    f"Absent-candidate active-strings {name} must be null or exactly empty: {label}")
        require(active_strings.get("completedRoomResults") == [],
                f"Absent-candidate completedRoomResults must be empty: {label}")
        for name in (*source_fields, "completedRoomResults"):
            require(provenance.get(name) == EMPTY_PAYLOAD_PROVENANCE,
                    f"Absent-candidate provenance must state no source payload: {label}/{name}")
        return

    require(active_present and isinstance(active, dict),
            f"Candidate real payload is absent from the raw envelope wire: {label}")
    for name in source_fields:
        expected = active.get(name)
        require(active_strings.get(name) == expected,
                f"active-strings {name} differs from the exact envelope string: {label}")
    results = active.get("completedRoomResults")
    require(isinstance(results, list), f"completedRoomResults must be present in active DTO: {label}")
    expected_results = []
    for item in results:
        require(isinstance(item, dict), f"Invalid completed-room result in active DTO: {label}")
        expected_results.append({
            "roomNodeId": item.get("roomNodeId"),
            "resultId": item.get("resultId"),
            "resultJson": item.get("resultJson"),
        })
    require(active_strings.get("completedRoomResults") == expected_results,
            f"active-strings completed results differ from exact envelope strings: {label}")
    for name in (*source_fields, "completedRoomResults"):
        record = provenance.get(name)
        require(isinstance(record, str) and record and record != EMPTY_PAYLOAD_PROVENANCE,
                f"Missing active-string producer provenance: {label}/{name}")


def _verify_case_output_files(
    capture_dir: Path,
    schema: dict[str, Any],
    case_rows: dict[tuple[str, str], dict[str, Any]],
    all_records: dict[str, dict[str, Any]],
) -> dict[tuple[str, str], dict[str, Any]]:
    case_definitions = {case["id"]: case for case in schema["cases"]}
    payloads: dict[tuple[str, str], dict[str, Any]] = {}
    p3_checksums: dict[str, str] = {}
    p3_preimages: dict[str, bytes] = {}
    p3_read_preimages: dict[str, bytes] = {}

    ordered_pairs = sorted(case_rows, key=lambda pair: (0 if pair[0] == "P3-receipts-completed" else 1, pair))
    for case_id, culture in ordered_pairs:
        row = case_rows[(case_id, culture)]
        label = f"{case_id}/{culture}"
        case = case_definitions[case_id]
        prefix = f"cultures/{culture}/{case_id}/"
        output_paths = [entry["path"] for entry in row["outputs"]]
        for relative in output_paths:
            require(relative in all_records, f"Output is absent from verified inventory: {relative}")

        def read(name: str) -> bytes:
            return safe_file(capture_dir, prefix + name, "capture output").read_bytes()

        envelope_bytes = read("envelope.json")
        write_bytes = read("write-preimage.utf8")
        read_bytes = read("read-preimage.utf8")
        envelope = parse_json_bytes(envelope_bytes, f"{label}/envelope.json", object_only=True)
        write_preimage = parse_json_bytes(write_bytes, f"{label}/write-preimage.utf8", object_only=True)
        read_preimage = parse_json_bytes(read_bytes, f"{label}/read-preimage.utf8", object_only=True)
        recipe = parse_json_bytes(read("recipe.json"), f"{label}/recipe.json", object_only=True)
        decode_result = parse_json_bytes(read("decode-result.json"), f"{label}/decode-result.json", object_only=True)
        _validate_recipe(recipe, row, case, schema, envelope_bytes, label)
        _verify_envelope_shape(case_id, case, envelope, schema, label)
        _validate_decode(row, decode_result, case, label)

        # Check only the captured preimage bytes and checksum text. The parsed
        # preimage is inspected for its empty checksum field, never re-encoded.
        require(write_preimage.get("checksum") == "", f"Write preimage checksum field is not empty: {label}")
        require(read_preimage.get("checksum") == "", f"Read preimage checksum field is not empty: {label}")
        checksum_text = _parse_checksum_file(safe_file(capture_dir, prefix + "checksum.txt", "capture checksum"))
        actual_write_hash = verify_checksum_relation(
            case_id,
            write_bytes,
            checksum_text,
            envelope.get("checksum"),
            p3_checksum=p3_checksums.get(culture),
            p3_write_preimage=p3_preimages.get(culture),
        )
        actual_read_hash, read_hash_matches_checksum = verify_read_checksum_relation(
            case_id,
            read_bytes,
            checksum_text,
            p3_checksum=p3_checksums.get(culture),
            p3_read_preimage=p3_read_preimages.get(culture),
        )
        if case_id == "P3-receipts-completed":
            p3_checksums[culture] = checksum_text
            p3_preimages[culture] = write_bytes
            p3_read_preimages[culture] = read_bytes

        fixed = schema["fixedInputHeader"]
        for header in HEADER_FIELDS:
            require(write_preimage.get(header) == fixed[header], f"Write-preimage header {header} mismatch: {label}")
            require(read_preimage.get(header) == fixed[header], f"Read-preimage header {header} mismatch: {label}")

        if case_id != "S0-settings":
            active_strings_bytes = read("active-strings.json")
            strings = parse_json_bytes(active_strings_bytes, f"{label}/active-strings.json", object_only=True)
            _validate_active_strings(strings, envelope, case_id, row["activePayloadPresent"], label)
        else:
            active_strings_bytes = None

        if case["role"] in POSITIVE_ROLES:
            api_bytes = read("api-envelope.json")
            api_envelope = parse_json_bytes(api_bytes, f"{label}/api-envelope.json", object_only=True)
            observation = parse_json_bytes(read("api-observation.json"), f"{label}/api-observation.json", object_only=True)
            compare_api_and_fixed_envelopes(api_envelope, envelope, label)
            _validate_api_observation(observation, api_envelope, envelope, case_id, culture,
                                      api_bytes, envelope_bytes, label)
            require(api_bytes != envelope_bytes, f"Raw API output was replaced by the fixed-header envelope: {label}")

        payloads[(case_id, culture)] = {
            "envelope": envelope,
            "envelopeBytes": envelope_bytes,
            "writeBytes": write_bytes,
            "readBytes": read_bytes,
            "recipeBytes": read("recipe.json"),
            "checksum": checksum_text,
            "actualWriteHash": actual_write_hash,
            "actualReadHash": actual_read_hash,
            "readHashMatchesChecksum": read_hash_matches_checksum,
            "activeStringsBytes": active_strings_bytes,
            "decode": row["decode"],
            "apiEnvelope": api_envelope if case["role"] in POSITIVE_ROLES else None,
        }

    for case in schema["cases"]:
        if not case.get("mustBeCultureInvariant"):
            continue
        case_id = case["id"]
        left = payloads[(case_id, "en-US")]
        right = payloads[(case_id, "es-ES")]
        for key in ("envelopeBytes", "writeBytes", "readBytes", "recipeBytes", "checksum", "activeStringsBytes"):
            require(left[key] == right[key], f"Culture variant differs for {case_id}: {key}")

    for culture in ("en-US", "es-ES"):
        p1 = payloads[("P1-known-zero", culture)]
        m0 = payloads[("M0-false-marker-payload", culture)]
        p1_active = p1["envelope"].get("activeExpedition")
        m0_active = m0["envelope"].get("activeExpedition")
        _validate_m0_payload_matches_p1(p1_active, m0_active, culture)

        p3 = payloads[("P3-receipts-completed", culture)]
        n0 = payloads[("N0-tampered", culture)]
        p3_profile = p3["envelope"].get("profile")
        n0_profile = n0["envelope"].get("profile")
        require(isinstance(p3_profile, dict) and isinstance(n0_profile, dict), f"P3/N0 profile missing: {culture}")
        require(p3_profile.get("availableRenown") == 33 and n0_profile.get("availableRenown") == 34,
                f"P3/N0 availableRenown mutation mismatch: {culture}")
        require(n0["checksum"] == p3["checksum"], f"N0 did not retain P3 checksum: {culture}")
        require(_without_p3_renown_and_checksum(p3["envelope"])
                == _without_p3_renown_and_checksum(n0["envelope"]),
                "P3 and N0 differ beyond their declared availableRenown mutation")
        require(p3["envelopeBytes"] != n0["envelopeBytes"], "N0 envelope bytes did not change from P3")
    return payloads


def verify_xml_results(xml_path: Path, expected_names: set[str]) -> dict[str, int]:
    require(xml_path.is_file() and not _is_reparse_or_symlink(xml_path), f"Missing/linked Unity XML: {xml_path}")
    raw = xml_path.read_bytes()
    # NUnit XML should be a small text report. Reject DTD/entity declarations
    # before parsing so this checker never resolves external XML entities.
    upper = raw.upper()
    require(b"<!DOCTYPE" not in upper and b"<!ENTITY" not in upper, "DTD/entity declarations are not allowed in Unity XML")
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as exc:
        raise CaptureError(f"Invalid NUnit XML: {exc}") from exc
    require(root.tag == "test-run", f"Unity XML root is not test-run: {root.tag}")
    require(root.get("result") == "Passed", f"NUnit test-run result is not Passed: {root.get('result')}")
    counts = {}
    for name in ("total", "passed", "failed", "inconclusive", "skipped"):
        raw_count = root.get(name)
        require(raw_count is not None and raw_count.isdigit(), f"Missing/invalid NUnit root count: {name}")
        counts[name] = int(raw_count)
    require(counts == {"total": 24, "passed": 24, "failed": 0, "inconclusive": 0, "skipped": 0},
            f"Unity XML does not report 24 Passed and zero skipped/fails: {counts}")
    test_cases = root.findall(".//test-case")
    names: list[str] = []
    for test_case in test_cases:
        name = test_case.get("name")
        require(isinstance(name, str) and name, "Unity XML contains a test-case without a name")
        names.append(name)
        require(test_case.get("result") == "Passed", f"Unity case did not Pass: {name} ({test_case.get('result')})")
        require(test_case.get("label") not in {"Ignored", "Skipped", "NotRunnable", "Inconclusive"},
                f"Unity case has a skip/ignore label: {name}")
    require(len(names) == 24 and len(set(names)) == 24, "Unity XML does not contain 24 unique test invocations")
    require(set(names) == expected_names,
            f"Unity XML case names differ; missing={sorted(expected_names - set(names))}, "
            f"extra={sorted(set(names) - expected_names)}")
    hidden = [node.tag for node in root.iter() if node.tag in {"failure", "error"}]
    require(not hidden, f"Unity XML contains failure/error elements: {hidden}")
    return counts


def _check_timestamp(value: Any, label: str) -> None:
    require(isinstance(value, str) and UTC_TIMESTAMP_RE.fullmatch(value), f"Invalid UTC timestamp: {label}")
    # Validate calendar/time ranges as well as syntax. Python's parser accepts
    # .NET's seven fractional digits and preserves UTC semantics.
    candidate = value[:-1]
    try:
        datetime.fromisoformat(candidate)
    except ValueError as exc:
        raise CaptureError(f"Invalid UTC timestamp: {label}") from exc


def _validate_fixture_index(
    capture_dir: Path,
    project_root: Path,
    commit: str,
    schema_path: Path,
    schema: dict[str, Any],
) -> tuple[dict[str, Any], dict[tuple[str, str], dict[str, Any]], dict[str, dict[str, Any]], dict[str, Any]]:
    index_path = safe_file(capture_dir, "fixture-index.json", "fixture index")
    index_bytes = index_path.read_bytes()
    index = parse_json_bytes(index_bytes, "fixture-index.json", object_only=True)
    require(index.get("schemaVersion") == schema["schemaVersion"], "fixture-index schemaVersion mismatch")
    require(index.get("captureKind") == schema["captureKind"], "fixture-index capture kind mismatch")
    captured_at = index.get("capturedAtUtc")
    _check_timestamp(captured_at, "fixture-index.capturedAtUtc")

    authority = schema["authority"]
    historical = index.get("historicalSource")
    require(isinstance(historical, dict), "Missing historicalSource identity")
    require(historical.get("commit") == HISTORICAL_COMMIT, "fixture-index historical commit mismatch")
    require(historical.get("tree") == HISTORICAL_TREE, "fixture-index historical tree mismatch")
    require(historical.get("contentVersion") == 5 and historical.get("schemaVersion") == 1,
            "fixture-index V5 content/schema identity mismatch")
    proof_info = historical.get("fullTreeProof")
    expected_proof = authority["fullTreeProof"]
    require(isinstance(proof_info, dict), "Missing fixture-index full-tree proof")
    for key in ("path", "sha256", "trackedBlobCount", "sumOriginalBlobBytes"):
        require(proof_info.get(key) == expected_proof.get(key), f"fixture-index fullTreeProof {key} mismatch")
    proof_path, proof = _read_full_tree_proof(project_root, expected_proof)
    require(proof.get("trackedBlobCount", len(proof["files"])) == FULL_TREE_BLOB_COUNT,
            "Full-tree proof summary count mismatch")
    require(proof.get("sumOriginalBlobBytes", sum(item["bytes"] for item in proof["files"])) == FULL_TREE_BYTES,
            "Full-tree proof summary byte total mismatch")

    execution = index.get("execution")
    require(isinstance(execution, dict), "Missing execution provenance")
    require(execution.get("commit") == commit, "Execution commit differs from requested commit")
    editor = execution.get("editor")
    require(isinstance(editor, dict), "Missing runtime Editor identity")
    require(editor.get("version") == HISTORICAL_EDITOR, "Runtime Editor version mismatch")
    require(editor.get("revision") == HISTORICAL_EDITOR_REVISION, "Runtime Editor revision mismatch")
    require(COMMIT_RE.fullmatch(commit) is not None, "Expected execution commit as 40 lowercase hex")

    project_version = safe_file(project_root, "ProjectSettings/ProjectVersion.txt", "execution source")
    version_text = project_version.read_text(encoding="utf-8")
    require(f"m_EditorVersion: {HISTORICAL_EDITOR}" in version_text, "ProjectVersion Editor version mismatch")
    require(f"m_EditorVersionWithRevision: {HISTORICAL_EDITOR} ({HISTORICAL_EDITOR_REVISION})" in version_text,
            "ProjectVersion Editor revision mismatch")

    packages = {
        "packageManifest": "Packages/manifest.json",
        "packageLock": "Packages/packages-lock.json",
    }
    for key, expected_path in packages.items():
        record = execution.get(key)
        require(isinstance(record, dict), f"Missing {key} execution identity")
        require(record.get("path") == expected_path, f"{key} path mismatch")
        source = safe_file(project_root, expected_path, "execution package source")
        raw = source.read_bytes()
        require(record.get("bytes") == len(raw), f"{key} byte length mismatch")
        require(record.get("sha256") == sha256_bytes(raw), f"{key} SHA-256 mismatch")

    required_sources = set(schema["requiredOriginalSources"])
    originals = source_inventory(index.get("requiredOriginalSources"), required_sources, project_root,
                                 "required original source")
    additions = source_inventory(index.get("runtimeAdditions"), RUNTIME_ADDITIONS, project_root,
                                 "runtime addition")

    require(index.get("fixedInputHeader") == schema["fixedInputHeader"], "Fixed input header differs from pinned schema")
    require(index.get("fixedIds") == schema["fixedIds"], "Fixed IDs differ from pinned schema")
    culture_rows = index.get("cultures")
    require(isinstance(culture_rows, list) and len(culture_rows) == 2, "fixture-index must record exactly two cultures")
    culture_map: dict[str, dict[str, Any]] = {}
    for culture in culture_rows:
        require(isinstance(culture, dict), "Invalid culture provenance")
        name = culture.get("name")
        require(name in {"en-US", "es-ES"} and name not in culture_map, "Missing/duplicate capture culture")
        require(culture.get("caseCount") == 12 and culture.get("invocationCount") == 12,
                f"Culture counts mismatch: {name}")
        culture_map[name] = culture
    require(set(culture_map) == {"en-US", "es-ES"}, "Culture set must be en-US and es-ES")

    case_rows = validate_case_rows(index.get("cases"), schema)
    expected_files, expected_dirs = _capture_expected_files(case_rows, schema)
    all_records: dict[str, dict[str, Any]] = {}
    for row in case_rows.values():
        for entry in row["outputs"]:
            path = entry.get("path")
            require(path not in all_records, f"Duplicate output inventory path: {path}")
            all_records[path] = entry
    verified = checked_output_records(capture_dir, list(all_records.values()))
    require(set(verified) == set(all_records), "Output inventory changed during verification")
    actual_files, actual_dirs = _walk_capture_tree(capture_dir)
    require(actual_files == expected_files,
            f"Capture file set differs; missing={sorted(expected_files - actual_files)}, "
            f"extra={sorted(actual_files - expected_files)}")
    require(actual_dirs == expected_dirs,
            f"Capture directory set differs; missing={sorted(expected_dirs - actual_dirs)}, "
            f"extra={sorted(actual_dirs - expected_dirs)}")

    return index, case_rows, all_records, {
        "requiredOriginalSources": originals,
        "runtimeAdditions": additions,
        "fullTreeProofPath": str(proof_path),
        "fullTreeProofSha256": sha256_bytes(proof_path.read_bytes()),
        "captureFiles": len(actual_files),
    }


def verify(capture_dir: Path, xml_path: Path, commit: str, project_root: Path, schema_path: Path) -> dict[str, Any]:
    require(COMMIT_RE.fullmatch(commit) is not None, "Expected exact 40-character execution commit")
    capture_dir = Path(capture_dir)
    xml_path = Path(xml_path)
    project_root = Path(project_root)
    schema_path = Path(schema_path)
    project_real = _strict_root(project_root, "project")
    if not capture_dir.is_absolute():
        capture_dir = project_root / capture_dir
    if not xml_path.is_absolute():
        xml_path = project_root / xml_path
    if not schema_path.is_absolute():
        schema_path = project_root / schema_path
    capture_real = _strict_root(capture_dir, "capture")
    try:
        capture_relative = capture_real.relative_to(project_real).as_posix()
    except ValueError as exc:
        raise CaptureError("Capture directory is outside the execution project") from exc
    require(re.fullmatch(r"Logs/Scenarios/V5Source/[0-9a-f]{40}-[0-9]+-[0-9]+", capture_relative) is not None,
            "Capture directory must be Logs/Scenarios/V5Source/{commit}-{run_id}-{attempt}")
    require(capture_relative.split("/")[-1].startswith(commit + "-"),
            "Capture directory run key does not match execution commit")
    try:
        schema_relative = schema_path.absolute().relative_to(project_root.absolute()).as_posix()
        xml_relative = xml_path.absolute().relative_to(project_root.absolute()).as_posix()
    except ValueError as exc:
        raise CaptureError("Schema and NUnit XML must be inside the execution project") from exc
    require(schema_relative == ".github/v5-source-capture/CAPTURE-SCHEMA.json",
            "Capture schema must use its frozen CI path")
    require(xml_relative == "test-results/editmode-results.xml",
            "Unity XML must use test-results/editmode-results.xml")
    schema_path = safe_file(project_root, schema_relative, "capture schema")
    xml_path = safe_file(project_root, xml_relative, "Unity XML")
    schema = validate_schema(schema_path)
    index, case_rows, output_records, provenance = _validate_fixture_index(
        capture_dir, project_root, commit, schema_path, schema
    )
    payloads = _verify_case_output_files(capture_dir, schema, case_rows, output_records)
    expected_names = {f"{case['id']}-{culture['name']}" for case in schema["cases"] for culture in schema["cultures"]}
    xml_counts = verify_xml_results(xml_path, expected_names)
    xml_bytes = xml_path.read_bytes()
    require(xml_path.resolve(strict=True) != capture_real and capture_real not in xml_path.resolve(strict=True).parents,
            "Unity XML must be preserved outside the capture directory")
    read_observations = []
    for (case_id, culture), payload in sorted(payloads.items()):
        if case_id not in {"B0-arrays-null", "B1-arrays-empty", "M0-false-marker-payload",
                           "M1-true-marker-null", "N0-tampered"}:
            continue
        read_observations.append({
            "caseId": case_id,
            "culture": culture,
            "readPreimageSha256": payload["actualReadHash"],
            "matchesStoredChecksum": payload["readHashMatchesChecksum"],
        })
    return {
        "result": "passed",
        "verificationScope": "Offline artifact integrity, source provenance and exact NUnit XML counts; not a human review or a substitute for the workflow's full-tree Git blob check.",
        "captureKind": schema["captureKind"],
        "historicalSourceCommit": HISTORICAL_COMMIT,
        "historicalSourceTree": HISTORICAL_TREE,
        "executionCommit": commit,
        "editor": {"version": HISTORICAL_EDITOR, "revision": HISTORICAL_EDITOR_REVISION},
        "captureDirectory": capture_relative,
        "captureIndexSha256": sha256_bytes(safe_file(capture_dir, "fixture-index.json").read_bytes()),
        "captureSchemaSha256": sha256_bytes(schema_path.read_bytes()),
        "fullTreeProofSha256": provenance["fullTreeProofSha256"],
        "fullTreeProofPath": provenance["fullTreeProofPath"],
        "requiredOriginalSourceCount": len(provenance["requiredOriginalSources"]),
        "runtimeAdditionCount": len(provenance["runtimeAdditions"]),
        "cultureCount": len(schema["cultures"]),
        "caseCount": len(schema["cases"]),
        "invocationCount": len(payloads),
        "captureFileCount": provenance["captureFiles"],
        "outputFileCount": len(output_records),
        "readPreimageObservations": read_observations,
        "xml": {"path": str(xml_path), "sha256": sha256_bytes(xml_bytes), **xml_counts},
        "capturedAtUtc": index["capturedAtUtc"],
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Verify an explicit Unity V5 capture without creating or rewriting expected save bytes.",
        epilog=("The XML and capture must come from the same execution commit. "
                "The schema and full-tree proof are pinned; output is JSON on stdout only."),
    )
    parser.add_argument("--capture-dir", type=Path, required=True, help="Exact capture directory under Logs/Scenarios/V5Source")
    parser.add_argument("--xml", type=Path, required=True, help="NUnit EditMode XML for the same Unity run")
    parser.add_argument("--commit", required=True, help="Full 40-character execution commit SHA")
    parser.add_argument("--project-root", type=Path, required=True, help="Complete historical Unity project checkout root")
    parser.add_argument("--schema", type=Path, required=True, help="Pinned CAPTURE-SCHEMA.json from the execution checkout")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        result = verify(args.capture_dir, args.xml, args.commit, args.project_root, args.schema)
    except (CaptureError, OSError, ValueError) as exc:
        print(f"V5 capture verification failed: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
