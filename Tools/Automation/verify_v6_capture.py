"""Verify an explicit Unity V6 capture; never generate or update expected bytes."""

import argparse
import hashlib
import json
import re
from pathlib import Path


EXPECTED_IDS = {
    "V6-P0-profile-no-active",
    "V6-P1-active-known-zero",
    "V6-P1-active-known-nontrivial",
    "V6-P2-active-unknown-null",
    "V6-P2-active-unknown-inline-empty",
    "V6-P3-receipts-full",
    "V6-P4-null-empty-boundary",
    "V6-P5-flag-payload-conflict",
    "V6-S0-settings",
    "V6-checkpoint-empty-slots",
    "V6-checkpoint-null-slots",
}
ALLOWED_UNITY_VERSIONS = frozenset({"6000.6.6f1", "6000.6.7f1"})
SHA256 = re.compile(r"[0-9a-f]{64}\Z")
COMMIT = re.compile(r"[0-9a-f]{40}\Z")
REQUIRED_SOURCES = {
    "Assets/Luckbound/Scripts/App/Compatibility/SaveV6HistoricalDtos.cs",
    "Assets/Luckbound/Scripts/App/Compatibility/SaveV6ChecksumInspector.cs",
    "Assets/Luckbound/Scripts/Core/Saves/SaveDocuments.cs",
    "Assets/Luckbound/Scripts/Core/Fortune/FortuneOfferBookSnapshot.cs",
    "Assets/Luckbound/Scripts/App/SaveRepository.cs",
    "Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs",
    "Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json",
    "Assets/Luckbound/Scripts/App/Luckbound.App.asmdef",
    "Assets/Luckbound/Scripts/Core/Luckbound.Core.asmdef",
    "Assets/Luckbound/Tests/PlayMode/Luckbound.PlayMode.Tests.asmdef",
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "ProjectSettings/ProjectVersion.txt",
}


class CaptureError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise CaptureError(message)


def safe_file(root, relative):
    require(isinstance(relative, str) and relative, "Missing relative file path")
    require("\\" not in relative and ":" not in relative, "Nonportable file path")
    pieces = relative.split("/")
    require(all(p not in ("", ".", "..") for p in pieces), "Unsafe file path")
    require(not relative.startswith("/"), "Absolute file path")
    target = root.joinpath(*pieces)
    current = root
    for piece in pieces:
        current = current / piece
        require(not current.is_symlink(), "Symlink in evidence path")
    require(target.resolve().is_relative_to(root.resolve()), "Evidence path escaped root")
    require(target.is_file(), "Missing evidence file: " + relative)
    return target


def load_object(path):
    # A malformed/partial JSON never counts as a completed descriptor.
    result = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(result, dict), "Expected JSON object: " + path.name)
    return result


def hash_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def checked_files(root, entries):
    require(isinstance(entries, list) and entries, "Missing output-file inventory")
    found = {}
    for entry in entries:
        require(isinstance(entry, dict), "Invalid output-file record")
        relative = entry.get("path")
        path = safe_file(root, relative)
        require(relative not in found, "Duplicate output file")
        size = entry.get("bytes")
        digest = entry.get("sha256")
        require(type(size) is int and size >= 0, "Invalid output size")
        require(isinstance(digest, str) and SHA256.fullmatch(digest), "Invalid output digest")
        require(path.stat().st_size == size, "Output size mismatch: " + relative)
        require(hash_file(path) == digest, "Output digest mismatch: " + relative)
        found[relative] = entry
    actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()}
    require(actual == set(found) | {"descriptor.json"}, "Unlisted or missing attempt files")
    return found


def source_inventory(entries):
    require(isinstance(entries, list) and entries, "Missing source inventory")
    result = {}
    for entry in entries:
        require(isinstance(entry, dict), "Invalid source record")
        path, digest, size = entry.get("path"), entry.get("sha256"), entry.get("bytes")
        require(isinstance(path, str) and path and path not in result, "Duplicate/missing source path")
        require(isinstance(digest, str) and SHA256.fullmatch(digest), "Invalid source digest")
        require(type(size) is int and size >= 0, "Invalid source size")
        result[path] = (size, digest)
    return result


def project_version_pin(project_root):
    path = safe_file(project_root, "ProjectSettings/ProjectVersion.txt")
    text = path.read_text(encoding="utf-8")
    versions = re.findall(r"(?m)^m_EditorVersion: ([^\r\n]+)$", text)
    require(len(versions) == 1, "ProjectVersion must declare exactly one m_EditorVersion")
    return versions[0]


def verify(capture_dir, commit, project_root, expected_unity_version="6000.6.6f1"):
    require(isinstance(expected_unity_version, str) and
            expected_unity_version in ALLOWED_UNITY_VERSIONS,
            "Unsupported expected Unity version")
    require(COMMIT.fullmatch(commit) is not None, "Expected exact commit SHA")
    require(capture_dir.is_dir() and not capture_dir.is_symlink(), "Missing/linked capture directory")
    session = load_object(safe_file(capture_dir, "session.json"))
    manifest = load_object(safe_file(capture_dir, "manifest.json"))
    session_id = session.get("sessionId")
    require(isinstance(session_id, str) and session_id, "Missing capture-session identity")
    for record in (session, manifest):
        require(record.get("schemaVersion") == 1, "Unknown capture schema")
        require(record.get("sessionId") == session_id, "Capture-session identity mismatch")
        require(record.get("commit") == commit, "Capture commit mismatch")
        ids = record.get("expectedCaseIds")
        require(isinstance(ids, list) and len(ids) == len(EXPECTED_IDS), "Wrong expected-case count")
        require(set(ids) == EXPECTED_IDS, "Expected-case IDs changed")
    require(manifest.get("dataComplete") is True, "Capture did not complete")
    before = source_inventory(manifest.get("sourceHashesBefore"))
    after = source_inventory(manifest.get("sourceHashesAfter"))
    require(REQUIRED_SOURCES.issubset(before), "Required source provenance omitted")
    require(before == after, "Source changed during Unity capture")
    for relative, (size, digest) in after.items():
        source = safe_file(project_root, relative)
        require(source.stat().st_size == size and hash_file(source) == digest,
                "Source differs from checked-out snapshot: " + relative)
    project_pin = project_version_pin(project_root)
    require(project_pin == expected_unity_version,
            "ProjectVersion pin mismatch: expected " + expected_unity_version +
            ", observed " + project_pin)
    for record_name, record in (("session", session), ("manifest", manifest)):
        require(record.get("unityVersion") == expected_unity_version,
                "Unexpected Unity runtime in " + record_name)
        require(record.get("projectVersionDeclared") == expected_unity_version,
                "ProjectVersion declaration mismatch in " + record_name)
    attempts = manifest.get("attempts")
    require(isinstance(attempts, list) and attempts, "No captured attempts")
    descriptors = set()
    seen_ids = set()
    output_count = 0
    for attempt in attempts:
        require(isinstance(attempt, dict), "Invalid attempt record")
        case_id = attempt.get("caseId")
        require(case_id in EXPECTED_IDS, "Unexpected case ID")
        require(attempt.get("complete") is True, "Partial capture attempt")
        require(attempt.get("semanticAcceptance") is True, "Capture failed its declared semantic role")
        directory = attempt.get("attemptDirectory")
        require(isinstance(directory, str) and re.fullmatch(
            r"cases/" + re.escape(case_id) + r"/attempt-[0-9]{4,}", directory), "Unexpected attempt path")
        descriptor_path = attempt.get("descriptorPath")
        require(descriptor_path == directory + "/descriptor.json", "Descriptor path mismatch")
        require(descriptor_path not in descriptors, "Duplicate attempt descriptor")
        descriptor_file = safe_file(capture_dir, descriptor_path)
        descriptor = load_object(descriptor_file)
        require(descriptor.get("schemaVersion") == 1, "Unknown descriptor schema")
        require(descriptor.get("sessionId") == session_id, "Descriptor session mismatch")
        require(descriptor.get("unityVersion") == expected_unity_version,
                "Unexpected Unity runtime in descriptor")
        require(descriptor.get("projectVersionDeclared") == expected_unity_version,
                "ProjectVersion declaration mismatch in descriptor")
        require(descriptor.get("caseId") == case_id, "Descriptor case mismatch")
        require(descriptor.get("complete") is True, "Incomplete attempt descriptor")
        require(descriptor.get("semanticAcceptance") is True, "Unaccepted attempt role")
        files = checked_files(descriptor_file.parent, descriptor.get("files"))
        if case_id.startswith("V6-checkpoint-"):
            checkpoint_paths = ("checkpoint-current.json", "checkpoint-legacy.json")
            require(set(checkpoint_paths).issubset(files), "Missing checkpoint DTO payloads")
            checkpoint_bytes = []
            for relative in checkpoint_paths:
                payload = (descriptor_file.parent / relative).read_bytes()
                require(not payload.startswith(b"\xef\xbb\xbf"), "BOM in checkpoint JSON")
                parsed = json.loads(payload.decode("utf-8", errors="strict"))
                require(isinstance(parsed, dict), "Checkpoint JSON is not an object")
                checkpoint_bytes.append(payload)
            require(checkpoint_bytes[0] == checkpoint_bytes[1], "Checkpoint DTO bytes differ")
        else:
            required = {"input.json", "envelope.json", "checksum-preimage.utf8", "checksum.sha256.txt"}
            require(required.issubset(files), "Missing envelope/checksum evidence")
            preimage = descriptor_file.parent / "checksum-preimage.utf8"
            require(not preimage.read_bytes().startswith(b"\xef\xbb\xbf"), "BOM in checksum preimage")
            digest = (descriptor_file.parent / "checksum.sha256.txt").read_text(encoding="ascii").strip()
            require(SHA256.fullmatch(digest) is not None and hash_file(preimage) == digest,
                    "Preimage checksum mismatch")
            emitted = json.loads((descriptor_file.parent / "envelope.json").read_text(encoding="utf-8"))
            require(emitted.get("checksum") == digest, "Encode checksum mismatch")
        output_count += len(files)
        descriptors.add(descriptor_path)
        seen_ids.add(case_id)
    require(seen_ids == EXPECTED_IDS, "Missing captured case IDs")
    actual_descriptors = {p.relative_to(capture_dir).as_posix()
                          for p in capture_dir.glob("cases/*/attempt-*/descriptor.json")}
    require(actual_descriptors == descriptors, "Unlisted/extra attempt descriptor")
    actual_attempts = {p.relative_to(capture_dir).as_posix()
                       for p in capture_dir.glob("cases/*/attempt-*") if p.is_dir()}
    require(actual_attempts == {a["attemptDirectory"] for a in attempts}, "Unlisted partial attempt")
    return {"commit": commit, "sessionId": session_id,
            "expectedUnityVersion": expected_unity_version,
            "observedUnityVersion": session.get("unityVersion"),
            "projectVersionDeclared": session.get("projectVersionDeclared"),
            "projectPinUnityVersion": project_pin,
            "caseCount": len(seen_ids),
            "attemptCount": len(attempts), "outputFileCount": output_count,
            "sourceCount": len(after), "manifestSha256": hash_file(capture_dir / "manifest.json"),
            "scope": "Artifact completeness and exact bytes; not independent golden acceptance or full Unity XML"}


class SingleExpectedUnityVersion(argparse.Action):
    def __call__(self, parser, namespace, values, option_string=None):
        if getattr(namespace, self.dest) is not None:
            parser.error("--expected-unity-version may be supplied only once")
        setattr(namespace, self.dest, values)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture-dir", type=Path, required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--expected-unity-version", action=SingleExpectedUnityVersion, default=None)
    args = parser.parse_args()
    print(json.dumps(verify(args.capture_dir, args.commit, args.project_root,
                            args.expected_unity_version if args.expected_unity_version is not None
                            else "6000.6.6f1"), indent=2))


if __name__ == "__main__":
    main()
