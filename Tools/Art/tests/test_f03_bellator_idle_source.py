"""Offline contract tests for the F03 Bellator Idle source producer."""

from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib


LOCKED_PATHS = {
    "sourceGenerated": "docs/agent_staging/f03-bellator-idle-review-20261024/source_generated.png",
    "referencePrototypeV2": "docs/agent_staging/f03-bellator-idle-review-20261024/BellatorIdle_Prototype_v2.png",
    "legacyNormalizerV2": "docs/agent_staging/f03-bellator-idle-review-20261024/normalize-bellator-idle-v2.ps1",
    "originRecordV1": "docs/agent_staging/f03-bellator-idle-review-20261024/RESOURCE_RECORD.v1.json",
    "literalPrompt": "docs/agent_staging/f03-bellator-idle-review-20261024/GENERATION_PROMPT.txt",
    "promptReferenceDocument": "docs/art/F01_master_reference.md",
}


def _find_source_project(start: Path) -> Path:
    for candidate in (start, *start.parents):
        if (candidate / LOCKED_PATHS["sourceGenerated"]).is_file():
            return candidate
    raise RuntimeError("Could not find the Luckbound snapshot inputs from the test file location.")


SOURCE_PROJECT = _find_source_project(Path(__file__).resolve())
AUTHORING_ART_DIR = Path(__file__).resolve().parents[1]
AUTHORING_TOOL = AUTHORING_ART_DIR / "build_f03_bellator_idle_source.py"
AUTHORING_MANIFEST = AUTHORING_ART_DIR / "f03_bellator_idle_inputs.json"

_spec = importlib.util.spec_from_file_location("f03_bellator_idle_source", AUTHORING_TOOL)
if _spec is None or _spec.loader is None:
    raise RuntimeError("Could not load the F03 source builder module.")
_builder = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_builder)


def _chunk(chunk_type: bytes, payload: bytes) -> bytes:
    crc = zlib.crc32(chunk_type + payload) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + chunk_type + payload + struct.pack(">I", crc)


def _minimal_png(width: int, height: int, color_type: int = 6) -> bytes:
    bytes_per_pixel = 4 if color_type == 6 else 3
    ihdr = struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)
    raw = b"".join(b"\x00" + bytes(width * bytes_per_pixel) for _ in range(height))
    return (
        b"\x89PNG\r\n\x1a\n"
        + _chunk(b"IHDR", ihdr)
        + _chunk(b"IDAT", zlib.compress(raw, level=9))
        + _chunk(b"IEND", b"")
    )


class F03BellatorIdleSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="f03-bellator-idle-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "snapshot"
        self.root.mkdir()

        for relative in LOCKED_PATHS.values():
            destination = self.root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(SOURCE_PROJECT / relative, destination)

        art_dir = self.root / "Tools" / "Art"
        art_dir.mkdir(parents=True)
        shutil.copyfile(AUTHORING_TOOL, art_dir / AUTHORING_TOOL.name)
        shutil.copyfile(AUTHORING_MANIFEST, art_dir / AUTHORING_MANIFEST.name)

        for name in ("Assets", "Packages", "ProjectSettings"):
            sentinel = self.root / name / "f03-source-test-sentinel.txt"
            sentinel.parent.mkdir(parents=True, exist_ok=True)
            sentinel.write_text(f"preserve {name}\n", encoding="utf-8")

    def _run_cli(self, output: str, inputs: str = "Tools/Art/f03_bellator_idle_inputs.json"):
        return subprocess.run(
            [
                sys.executable,
                str(self.root / "Tools" / "Art" / "build_f03_bellator_idle_source.py"),
                "--project-root",
                str(self.root),
                "--inputs",
                inputs,
                "--output-dir",
                output,
            ],
            cwd=self.root,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=60,
            check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )

    def _protected_tree_snapshot(self) -> dict[str, str]:
        snapshot = {}
        for relative in (*LOCKED_PATHS.values(), "Tools/Art/f03_bellator_idle_inputs.json"):
            path = self.root / relative
            snapshot[relative] = (
                hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else "<missing>"
            )
        for folder in ("Assets", "Packages", "ProjectSettings"):
            for path in sorted((self.root / folder).rglob("*")):
                if path.is_file():
                    relative = path.relative_to(self.root).as_posix()
                    snapshot[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        return snapshot

    def test_two_runs_are_byte_equal_and_decode_to_frozen_v2_pixels(self):
        before = self._protected_tree_snapshot()
        first = self._run_cli("Logs/ArtSource/run-a")
        second = self._run_cli("Logs/ArtSource/run-b")
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(second.returncode, 0, second.stderr)

        first_dir = self.root / "Logs" / "ArtSource" / "run-a"
        second_dir = self.root / "Logs" / "ArtSource" / "run-b"
        names = (
            "BellatorIdle_Prototype_v2.png",
            "ART-F03-BELLATOR-IDLE-001.v2.json",
            "BUILD-RESULTS.json",
        )
        for name in names:
            self.assertEqual((first_dir / name).read_bytes(), (second_dir / name).read_bytes(), name)

        reference_path = self.root / LOCKED_PATHS["referencePrototypeV2"]
        reference = _builder.decode_png_rgba(reference_path.read_bytes(), (128, 48))
        produced = _builder.decode_png_rgba(
            (first_dir / "BellatorIdle_Prototype_v2.png").read_bytes(), (128, 48)
        )
        self.assertEqual(produced, reference)

        v1 = json.loads((self.root / LOCKED_PATHS["originRecordV1"]).read_text(encoding="utf-8"))
        v2 = json.loads(
            (first_dir / "ART-F03-BELLATOR-IDLE-001.v2.json").read_text(encoding="utf-8")
        )
        prompt = (self.root / LOCKED_PATHS["literalPrompt"]).read_text(encoding="utf-8")
        self.assertEqual(v2["recordVersion"], 2)
        self.assertEqual(v2["literalPrompt"], v1["literalPrompt"])
        self.assertEqual(v2["literalPrompt"], prompt)
        self.assertEqual(v2["previousProcessing"], v1["processing"])
        self.assertEqual(v2["status"], "Prototype/source-only/not-imported")
        self.assertIn("no new image generation", v2["sourceGeneration"])

        results = json.loads((first_dir / "BUILD-RESULTS.json").read_text(encoding="utf-8"))
        self.assertEqual(results["status"], "Prototype/source-only/not-imported")
        self.assertIsNone(results["commitExecuted"])  # Snapshot fixture has no .git directory.
        self.assertEqual(results["outputs"]["png"]["sha256"], hashlib.sha256((first_dir / names[0]).read_bytes()).hexdigest())
        self.assertEqual(results["outputs"]["record"]["sha256"], hashlib.sha256((first_dir / names[1]).read_bytes()).hexdigest())
        self.assertEqual(self._protected_tree_snapshot(), before)

    def test_candidate_has_four_cells_palette_alpha_baseline_silhouettes_and_distinct_frames(self):
        result = self._run_cli("Logs/ArtSource/metrics")
        self.assertEqual(result.returncode, 0, result.stderr)
        output = self.root / "Logs" / "ArtSource" / "metrics"
        png = (output / "BellatorIdle_Prototype_v2.png").read_bytes()
        width, height, pixels = _builder.decode_png_rgba(png, (128, 48))
        self.assertEqual((width, height), (128, 48))
        metrics = _builder.analyze_candidate(pixels)
        self.assertEqual(metrics["frameCount"], 4)
        self.assertEqual(metrics["frameSize"], "32x48")
        self.assertEqual(metrics["alphaValues"], [0, 255])
        self.assertEqual(metrics["paletteColorCount"], 19)
        self.assertEqual(metrics["paletteViolations"], 0)
        self.assertEqual(metrics["opaquePixels"], 2215)
        self.assertEqual(metrics["opaquePixelsPerFrame"], [552, 552, 559, 552])
        self.assertEqual(metrics["fourNeighborComponentsPerFrame"], [1, 1, 1, 1])
        self.assertEqual(len(set(metrics["opaqueBaselineYPerFrame"])), 1)
        self.assertEqual(
            metrics["pairwiseFramePixelDifferences"],
            {"0-1": 191, "0-2": 280, "0-3": 188, "1-2": 277, "1-3": 205, "2-3": 283},
        )

    def test_hash_tamper_is_rejected_without_writing_inputs_or_protected_trees(self):
        source_path = self.root / LOCKED_PATHS["sourceGenerated"]
        changed = bytearray(source_path.read_bytes())
        changed[-1] ^= 1
        source_path.write_bytes(changed)
        before = self._protected_tree_snapshot()

        result = self._run_cli("Logs/ArtSource/hash-negative")
        self.assertEqual(result.returncode, 2)
        self.assertIn("SHA-256 mismatch", result.stderr)
        self.assertEqual(self._protected_tree_snapshot(), before)
        self.assertFalse((self.root / "Logs" / "ArtSource" / "hash-negative").exists())

    def test_crc_damage_is_rejected_by_the_png_decoder(self):
        source_path = self.root / LOCKED_PATHS["sourceGenerated"]
        damaged = bytearray(source_path.read_bytes())
        first_chunk_length = struct.unpack_from(">I", damaged, 8)[0]
        crc_offset = 8 + 8 + first_chunk_length
        damaged[crc_offset] ^= 1
        with self.assertRaisesRegex(_builder.BuildError, "CRC mismatch"):
            _builder.decode_png_rgba(bytes(damaged))

    def test_wrong_dimensions_and_color_format_are_rejected(self):
        wrong_dimensions = _minimal_png(1, 1)
        with self.assertRaisesRegex(_builder.BuildError, "dimensions are 1x1"):
            _builder.decode_png_rgba(wrong_dimensions, (2033, 773))

        rgb_png = _minimal_png(1, 1, color_type=2)
        with self.assertRaisesRegex(_builder.BuildError, "Only non-interlaced 8-bit RGBA"):
            _builder.decode_png_rgba(rgb_png)

    def test_missing_input_is_rejected_without_output(self):
        (self.root / LOCKED_PATHS["literalPrompt"]).unlink()
        before = self._protected_tree_snapshot()
        result = self._run_cli("Logs/ArtSource/missing-input")
        self.assertEqual(result.returncode, 2)
        self.assertIn("Missing input literalPrompt", result.stderr)
        self.assertEqual(self._protected_tree_snapshot(), before)
        self.assertFalse((self.root / "Logs" / "ArtSource" / "missing-input").exists())

    def test_existing_output_is_rejected_without_replacing_anything(self):
        output = self.root / "Logs" / "ArtSource" / "existing"
        output.mkdir(parents=True)
        sentinel = output / "keep.txt"
        sentinel.write_text("keep this file\n", encoding="utf-8")
        before = self._protected_tree_snapshot()
        result = self._run_cli("Logs/ArtSource/existing")
        self.assertEqual(result.returncode, 2)
        self.assertIn("already exists", result.stderr)
        self.assertEqual(sentinel.read_text(encoding="utf-8"), "keep this file\n")
        self.assertEqual(self._protected_tree_snapshot(), before)

    def test_output_outside_art_source_is_rejected_without_creating_it(self):
        outside = self.root.parent / "outside-f03-output"
        result = self._run_cli(str(outside))
        self.assertEqual(result.returncode, 2)
        self.assertIn("under Logs/ArtSource", result.stderr)
        self.assertFalse(outside.exists())
        self.assertFalse((self.root / "Logs").exists())


if __name__ == "__main__":
    unittest.main()
