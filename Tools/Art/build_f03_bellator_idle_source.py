#!/usr/bin/env python3
"""Reproduce the frozen Bellator Idle v2 pixels without external packages."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import stat
import struct
import subprocess
import sys
import zlib


TOOL_REPO_PATH = "Tools/Art/build_f03_bellator_idle_source.py"
INPUT_MANIFEST_REPO_PATH = "Tools/Art/f03_bellator_idle_inputs.json"
INPUT_SET_ID = "F03-BELLATOR-IDLE-20261030"
STATUS = "Prototype/source-only/not-imported"
OUTPUT_NAMES = {
    "png": "BellatorIdle_Prototype_v2.png",
    "record": "ART-F03-BELLATOR-IDLE-001.v2.json",
    "buildResults": "BUILD-RESULTS.json",
}

LOCKED_INPUTS = {
    "sourceGenerated": {
        "path": "docs/agent_staging/f03-bellator-idle-review-20261024/source_generated.png",
        "sha256": "1641ca09dc66efdd85d26bd4d8f8bbe172095e142a5b8289897491ad7f7963cd",
        "width": 2033,
        "height": 773,
    },
    "referencePrototypeV2": {
        "path": "docs/agent_staging/f03-bellator-idle-review-20261024/BellatorIdle_Prototype_v2.png",
        "sha256": "10b4b6c9455083cc3c0a3a51d56ce67855c6aae010f68ba0819baa98716f537e",
        "width": 128,
        "height": 48,
    },
    "legacyNormalizerV2": {
        "path": "docs/agent_staging/f03-bellator-idle-review-20261024/normalize-bellator-idle-v2.ps1",
        "sha256": "594ecd7bc5a6436c68cf9639941e356d965ba301395ee8bd268e26d4cda9f2b0",
    },
    "originRecordV1": {
        "path": "docs/agent_staging/f03-bellator-idle-review-20261024/RESOURCE_RECORD.v1.json",
        "sha256": "d1fc775d9a425bf1b6847462a46052b13451cbbbcb5159d68d74a76013b584cd",
    },
    "literalPrompt": {
        "path": "docs/agent_staging/f03-bellator-idle-review-20261024/GENERATION_PROMPT.txt",
        "sha256": "a09260a896a3bc85aeed5ce2f123227a3b0d2677962977787886387a3b0b558d",
    },
    "promptReferenceDocument": {
        "path": "docs/art/F01_master_reference.md",
        "sha256": "33b03e7a1a4762f8177257341d4088cde4582a97c32192bc4d43d0ba24e3ed61",
    },
}

PALETTE_HEX = (
    "#10111B",
    "#1B1D2B",
    "#2A2E40",
    "#3F465C",
    "#59647A",
    "#8290A5",
    "#BDC6CC",
    "#E8E3CD",
    "#32251F",
    "#50382B",
    "#765137",
    "#A7794D",
    "#54232A",
    "#83333A",
    "#BC5148",
    "#E98664",
    "#5E3F22",
    "#9A6026",
    "#D68F32",
    "#F0C15A",
    "#294C69",
    "#4C86A0",
    "#8FC1C1",
)

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MAX_INPUT_BYTES = 2 * 1024 * 1024
MAX_CHUNK_BYTES = 2 * 1024 * 1024
MAX_CHUNKS = 128
MAX_WIDTH = 4096
MAX_HEIGHT = 2048
MAX_PIXELS = 8_000_000
MAX_DECOMPRESSED_BYTES = 40 * 1024 * 1024


class BuildError(Exception):
    """A controlled input, PNG, or output-contract failure."""


def _strict_json_loads(raw: bytes, label: str):
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise BuildError(f"{label} is not valid UTF-8: {exc}") from exc

    def reject_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise BuildError(f"{label} has duplicate JSON key {key!r}.")
            result[key] = value
        return result

    try:
        return json.loads(text, object_pairs_hook=reject_duplicates)
    except json.JSONDecodeError as exc:
        raise BuildError(f"{label} is not valid JSON: {exc}") from exc


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _is_reparse_point(path: Path) -> bool:
    try:
        info = path.lstat()
    except FileNotFoundError:
        return False
    return stat.S_ISLNK(info.st_mode) or bool(
        getattr(info, "st_file_attributes", 0) & 0x400
    )


def _lexical_absolute(root: Path, argument: str) -> Path:
    value = Path(argument)
    if not value.is_absolute():
        value = root / value
    return Path(os.path.abspath(os.fspath(value)))


def _assert_no_reparse_components(root: Path, path: Path, label: str) -> None:
    try:
        relative = path.relative_to(root)
    except ValueError as exc:
        raise BuildError(f"{label} escapes --project-root.") from exc

    current = root
    for component in relative.parts:
        current = current / component
        if _is_reparse_point(current):
            raise BuildError(f"{label} contains a symbolic link or reparse point: {current}.")


def _project_file(root: Path, relative_path: str, label: str) -> Path:
    candidate = _lexical_absolute(root, relative_path)
    _assert_no_reparse_components(root, candidate, label)
    try:
        resolved = candidate.resolve(strict=True)
    except FileNotFoundError as exc:
        raise BuildError(f"Missing {label}: {relative_path}.") from exc
    try:
        resolved.relative_to(root)
    except ValueError as exc:
        raise BuildError(f"{label} resolves outside --project-root.") from exc
    if not resolved.is_file():
        raise BuildError(f"{label} is not a regular file: {relative_path}.")
    return resolved


def _read_locked_file(root: Path, item: dict, label: str, max_bytes: int = MAX_INPUT_BYTES):
    path = _project_file(root, item["path"], label)
    try:
        size = path.stat().st_size
    except OSError as exc:
        raise BuildError(f"Cannot stat {label}: {exc}") from exc
    if size > max_bytes:
        raise BuildError(f"{label} exceeds the {max_bytes}-byte input limit.")
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise BuildError(f"Cannot read {label}: {exc}") from exc
    actual = _sha256(data)
    if actual != item["sha256"]:
        raise BuildError(
            f"{label} SHA-256 mismatch: expected {item['sha256']}, got {actual}."
        )
    return path, data, actual


def _validate_manifest(manifest: dict) -> None:
    if not isinstance(manifest, dict) or set(manifest) != {
        "schemaVersion",
        "inputSet",
        "inputs",
        "outputs",
    }:
        raise BuildError("Input manifest must contain only schemaVersion, inputSet, inputs, and outputs.")
    if manifest["schemaVersion"] != 1 or manifest["inputSet"] != INPUT_SET_ID:
        raise BuildError("Input manifest schema or inputSet is not the locked F03 contract.")
    inputs = manifest["inputs"]
    if not isinstance(inputs, dict) or set(inputs) != set(LOCKED_INPUTS):
        raise BuildError("Input manifest has a missing or unexpected input entry.")
    for name, locked in LOCKED_INPUTS.items():
        item = inputs[name]
        if not isinstance(item, dict) or item != locked:
            raise BuildError(f"Input manifest entry {name!r} differs from the locked contract.")
    if manifest["outputs"] != OUTPUT_NAMES:
        raise BuildError("Input manifest output names differ from the locked contract.")


def _validate_output_destination(root: Path, argument: str) -> Path:
    destination = _lexical_absolute(root, argument)
    art_source = Path(os.path.abspath(os.fspath(root / "Logs" / "ArtSource")))
    try:
        relative = destination.relative_to(art_source)
    except ValueError as exc:
        raise BuildError("--output-dir must be a new child directory under Logs/ArtSource.") from exc
    if not relative.parts:
        raise BuildError("--output-dir must name a new child of Logs/ArtSource.")

    _assert_no_reparse_components(root, destination, "--output-dir")
    try:
        resolved_art_source = art_source.resolve(strict=False)
        resolved_destination = destination.resolve(strict=False)
        resolved_destination.relative_to(resolved_art_source)
    except (OSError, ValueError) as exc:
        raise BuildError("--output-dir resolves outside Logs/ArtSource.") from exc

    current = root
    for component in destination.relative_to(root).parts:
        current = current / component
        if _is_reparse_point(current):
            raise BuildError(f"--output-dir contains a symbolic link or reparse point: {current}.")
        if current.exists() and current != destination and not current.is_dir():
            raise BuildError(f"--output-dir parent is not a directory: {current}.")

    if destination.exists() or destination.is_symlink():
        raise BuildError(f"--output-dir already exists: {destination}.")
    return destination


def _png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    crc = zlib.crc32(chunk_type + payload) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + chunk_type + payload + struct.pack(">I", crc)


def _paeth_predictor(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    distance_left = abs(estimate - left)
    distance_above = abs(estimate - above)
    distance_upper_left = abs(estimate - upper_left)
    if distance_left <= distance_above and distance_left <= distance_upper_left:
        return left
    if distance_above <= distance_upper_left:
        return above
    return upper_left


def decode_png_rgba(
    data: bytes,
    expected_dimensions: tuple[int, int] | None = None,
) -> tuple[int, int, bytes]:
    """Decode only bounded, non-interlaced, 8-bit RGBA PNGs using stdlib zlib."""
    if len(data) > MAX_INPUT_BYTES:
        raise BuildError("PNG exceeds the compressed input size limit.")
    if len(data) < len(PNG_SIGNATURE) or data[:8] != PNG_SIGNATURE:
        raise BuildError("Input is not a PNG with the standard signature.")

    cursor = 8
    chunk_count = 0
    saw_ihdr = False
    saw_idat = False
    idat_closed = False
    saw_iend = False
    saw_plte = False
    compressed = bytearray()
    width = height = 0

    while cursor < len(data):
        chunk_count += 1
        if chunk_count > MAX_CHUNKS:
            raise BuildError("PNG has too many chunks.")
        if cursor + 12 > len(data):
            raise BuildError("PNG has a truncated chunk header or CRC.")
        length = struct.unpack_from(">I", data, cursor)[0]
        chunk_type = data[cursor + 4 : cursor + 8]
        if length > MAX_CHUNK_BYTES:
            raise BuildError("PNG chunk exceeds the per-chunk size limit.")
        chunk_end = cursor + 12 + length
        if chunk_end > len(data):
            raise BuildError("PNG chunk extends past the input bytes.")
        if any(not (65 <= byte <= 90 or 97 <= byte <= 122) for byte in chunk_type):
            raise BuildError("PNG has an invalid chunk type name.")
        if chunk_type[2] & 0x20:
            raise BuildError("PNG uses an invalid lowercase reserved chunk-type bit.")

        payload_start = cursor + 8
        payload_end = payload_start + length
        payload = data[payload_start:payload_end]
        expected_crc = struct.unpack_from(">I", data, payload_end)[0]
        actual_crc = zlib.crc32(chunk_type + payload) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            raise BuildError(f"PNG CRC mismatch in {chunk_type.decode('ascii')} chunk.")

        if not saw_ihdr and chunk_type != b"IHDR":
            raise BuildError("PNG IHDR must be the first chunk.")

        if chunk_type == b"IHDR":
            if saw_ihdr or chunk_count != 1 or length != 13:
                raise BuildError("PNG must contain one 13-byte IHDR as its first chunk.")
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", payload
            )
            if width == 0 or height == 0 or width > MAX_WIDTH or height > MAX_HEIGHT:
                raise BuildError("PNG dimensions are outside the supported limits.")
            if width * height > MAX_PIXELS:
                raise BuildError("PNG pixel count exceeds the supported limit.")
            if expected_dimensions is not None and (width, height) != expected_dimensions:
                raise BuildError(
                    f"PNG dimensions are {width}x{height}; expected "
                    f"{expected_dimensions[0]}x{expected_dimensions[1]}."
                )
            if (bit_depth, color_type, compression, filtering, interlace) != (8, 6, 0, 0, 0):
                raise BuildError("Only non-interlaced 8-bit RGBA PNG (color type 6) is supported.")
            saw_ihdr = True
        elif chunk_type == b"IDAT":
            if idat_closed:
                raise BuildError("PNG IDAT chunks must be consecutive.")
            saw_idat = True
            compressed.extend(payload)
            if len(compressed) > MAX_INPUT_BYTES:
                raise BuildError("PNG compressed image data exceeds the size limit.")
        elif chunk_type == b"IEND":
            if length != 0 or not saw_idat:
                raise BuildError("PNG IEND must be empty and follow image data.")
            saw_iend = True
            cursor = chunk_end
            if cursor != len(data):
                raise BuildError("PNG has trailing bytes after IEND.")
            break
        else:
            if saw_idat:
                idat_closed = True
            if chunk_type == b"PLTE":
                if saw_plte or saw_idat or length == 0 or length > 768 or length % 3:
                    raise BuildError("PNG PLTE chunk has invalid placement or length.")
                saw_plte = True
            elif chunk_type in (b"acTL", b"fcTL", b"fdAT"):
                raise BuildError("Animated PNG chunks are not supported.")
            elif not (chunk_type[0] & 0x20):
                raise BuildError(f"Unsupported critical PNG chunk {chunk_type.decode('ascii')}.")

        cursor = chunk_end

    if not saw_ihdr or not saw_idat or not saw_iend:
        raise BuildError("PNG is missing IHDR, IDAT, or IEND.")

    expected_raw_size = height * (1 + width * 4)
    if expected_raw_size > MAX_DECOMPRESSED_BYTES:
        raise BuildError("PNG decoded image would exceed the decompressed data limit.")
    try:
        inflater = zlib.decompressobj(wbits=15)
        raw = inflater.decompress(bytes(compressed), expected_raw_size + 1)
    except zlib.error as exc:
        raise BuildError(f"PNG image data has invalid zlib compression: {exc}") from exc
    if len(raw) != expected_raw_size:
        raise BuildError("PNG decompressed byte count does not match its dimensions.")
    if inflater.unconsumed_tail or inflater.unused_data or not inflater.eof:
        raise BuildError("PNG zlib stream is incomplete, oversized, or has trailing data.")
    try:
        remainder = inflater.flush()
    except zlib.error as exc:
        raise BuildError(f"PNG zlib stream could not be finalized: {exc}") from exc
    if remainder:
        raise BuildError("PNG zlib stream contains extra decompressed bytes.")

    rgba = bytearray(width * height * 4)
    bytes_per_pixel = 4
    stride = width * bytes_per_pixel
    source_offset = 0
    previous = bytearray(stride)
    for row_index in range(height):
        filter_type = raw[source_offset]
        source_offset += 1
        filtered = raw[source_offset : source_offset + stride]
        source_offset += stride
        if filter_type > 4:
            raise BuildError(f"PNG row {row_index} uses unsupported filter type {filter_type}.")

        current = bytearray(stride)
        for index in range(stride):
            sample = filtered[index]
            left = current[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = previous[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            else:
                predictor = _paeth_predictor(left, above, upper_left)
            current[index] = (sample + predictor) & 0xFF

        destination_offset = row_index * stride
        rgba[destination_offset : destination_offset + stride] = current
        previous = current

    return width, height, bytes(rgba)


def _palette() -> tuple[tuple[int, int, int], ...]:
    return tuple(
        (int(value[1:3], 16), int(value[3:5], 16), int(value[5:7], 16))
        for value in PALETTE_HEX
    )


def normalize_source_rgba(source: bytes, width: int, height: int) -> tuple[bytes, list[dict]]:
    if (width, height) != (2033, 773):
        raise BuildError(f"Source image is {width}x{height}; expected 2033x773.")
    palette = _palette()
    output_width, output_height = 128, 48
    output = bytearray(output_width * output_height * 4)
    frame_stats = []

    for frame in range(4):
        cell_left = (width * frame) // 4
        cell_right = (width * (frame + 1)) // 4
        frame_width = cell_right - cell_left
        min_x = cell_right
        max_x = cell_left - 1
        min_y = height
        max_y = -1
        for y in range(height):
            row_start = (y * width + cell_left) * 4
            for x in range(cell_left, cell_right):
                if source[row_start + (x - cell_left) * 4 + 3] >= 128:
                    if x < min_x:
                        min_x = x
                    if x > max_x:
                        max_x = x
                    if y < min_y:
                        min_y = y
                    if y > max_y:
                        max_y = y
        if max_x < min_x or max_y < min_y:
            raise BuildError(f"Source frame {frame} has no opaque subject.")

        body_center = (min_x + max_x) / 2.0
        cell_center = (cell_left + cell_right - 1) / 2.0
        crop_left = cell_left - (cell_center - body_center)
        crop_top = 5.0
        crop_height = 762.0

        for y in range(output_height):
            source_y = int((crop_top + ((y + 0.5) * crop_height / output_height)) // 1)
            for x in range(32):
                source_x = int((crop_left + ((x + 0.5) * frame_width / 32.0)) // 1)
                output_x = frame * 32 + x
                output_offset = (y * output_width + output_x) * 4
                if source_x < 0 or source_x >= width or source_y < 0 or source_y >= height:
                    output[output_offset : output_offset + 4] = b"\xff\xff\xff\x00"
                    continue

                source_offset = (source_y * width + source_x) * 4
                red, green, blue, alpha = source[source_offset : source_offset + 4]
                if alpha < 128:
                    output[output_offset : output_offset + 4] = b"\xff\xff\xff\x00"
                    continue

                best = palette[0]
                best_distance = 2**31 - 1
                for candidate in palette:
                    red_delta = red - candidate[0]
                    green_delta = green - candidate[1]
                    blue_delta = blue - candidate[2]
                    distance = (
                        red_delta * red_delta
                        + green_delta * green_delta
                        + blue_delta * blue_delta
                    )
                    if distance < best_distance:
                        best_distance = distance
                        best = candidate
                output[output_offset : output_offset + 4] = bytes((*best, 255))

        frame_stats.append(
            {
                "frame": frame,
                "opaqueSourceBounds": f"{min_x},{min_y}-{max_x},{max_y}",
                "horizontalRecenterPixels": round(cell_center - body_center, 2),
                "outputCell": f"{frame * 32},0,32,48",
            }
        )

    return bytes(output), frame_stats


def _frame_rgba(pixels: bytes, frame_index: int) -> bytes:
    result = bytearray(32 * 48 * 4)
    destination = 0
    for y in range(48):
        start = (y * 128 + frame_index * 32) * 4
        row = pixels[start : start + 32 * 4]
        result[destination : destination + len(row)] = row
        destination += len(row)
    return bytes(result)


def analyze_candidate(pixels: bytes) -> dict:
    if len(pixels) != 128 * 48 * 4:
        raise BuildError("Candidate pixel buffer is not 128x48 RGBA.")
    alpha_values = set(pixels[3::4])
    if not alpha_values.issubset({0, 255}):
        raise BuildError("Candidate alpha values are not binary 0/255.")

    allowed_colors = set(_palette())
    used_colors = set()
    opaque_per_frame = []
    baselines = []
    component_counts = []
    frames = [_frame_rgba(pixels, frame) for frame in range(4)]

    for frame in range(4):
        frame_pixels = frames[frame]
        opaque_cells = set()
        opaque_count = 0
        baseline = -1
        for y in range(48):
            for x in range(32):
                offset = (y * 32 + x) * 4
                red, green, blue, alpha = frame_pixels[offset : offset + 4]
                if alpha:
                    opaque_count += 1
                    baseline = max(baseline, y)
                    opaque_cells.add(y * 32 + x)
                    used_colors.add((red, green, blue))
                    if (red, green, blue) not in allowed_colors:
                        raise BuildError("Candidate contains a color outside the Bellator palette.")
        if not opaque_cells:
            raise BuildError(f"Candidate frame {frame} is empty.")
        opaque_per_frame.append(opaque_count)
        baselines.append(baseline)

        remaining = set(opaque_cells)
        components = 0
        while remaining:
            components += 1
            stack = [remaining.pop()]
            while stack:
                cell = stack.pop()
                x = cell % 32
                y = cell // 32
                neighbors = []
                if x > 0:
                    neighbors.append(cell - 1)
                if x < 31:
                    neighbors.append(cell + 1)
                if y > 0:
                    neighbors.append(cell - 32)
                if y < 47:
                    neighbors.append(cell + 32)
                for neighbor in neighbors:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        stack.append(neighbor)
        component_counts.append(components)

    distances = {}
    for left in range(4):
        for right in range(left + 1, 4):
            left_frame = frames[left]
            right_frame = frames[right]
            distance = sum(
                1
                for offset in range(0, len(left_frame), 4)
                if left_frame[offset : offset + 4] != right_frame[offset : offset + 4]
            )
            distances[f"{left}-{right}"] = distance
            if distance == 0:
                raise BuildError("Candidate idle frames are not all distinct.")

    if len(set(baselines)) != 1:
        raise BuildError("Candidate frames do not share one opaque baseline.")
    opaque_total = sum(opaque_per_frame)
    if opaque_total != 2215 or opaque_per_frame != [552, 552, 559, 552]:
        raise BuildError("Candidate silhouette counts differ from the frozen v2 reference.")
    if len(used_colors) != 19 or component_counts != [1, 1, 1, 1]:
        raise BuildError("Candidate palette or silhouette connectivity differs from frozen v2.")

    return {
        "dimensions": "128x48 RGBA",
        "frameCount": 4,
        "frameSize": "32x48",
        "alphaValues": sorted(alpha_values),
        "opaquePixels": opaque_total,
        "opaquePixelsPerFrame": opaque_per_frame,
        "paletteColorCount": len(used_colors),
        "paletteViolations": 0,
        "opaqueBaselineYPerFrame": baselines,
        "fourNeighborComponentsPerFrame": component_counts,
        "pairwiseFramePixelDifferences": distances,
    }


def encode_png_rgba(width: int, height: int, pixels: bytes) -> bytes:
    if width <= 0 or height <= 0 or len(pixels) != width * height * 4:
        raise BuildError("Cannot encode a PNG with inconsistent RGBA dimensions.")
    scanlines = bytearray()
    stride = width * 4
    for y in range(height):
        scanlines.append(0)
        start = y * stride
        scanlines.extend(pixels[start : start + stride])
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    compressed = zlib.compress(bytes(scanlines), level=9)
    return (
        PNG_SIGNATURE
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", compressed)
        + _png_chunk(b"IEND", b"")
    )


def _build_record(
    origin_record: dict,
    prompt_reference_hash: str,
    output_hash: str,
) -> bytes:
    record = copy.deepcopy(origin_record)
    previous_processing = copy.deepcopy(record.get("processing", {}))
    record["recordVersion"] = 2
    record["sourcePath"] = OUTPUT_NAMES["png"]
    record["sha256"] = output_hash
    record["promptReferenceSha256"] = prompt_reference_hash
    record["previousProcessing"] = previous_processing
    record["processing"] = {
        "tool": "Python standard library",
        "version": "f03-bellator-idle-source/1",
        "procedure": TOOL_REPO_PATH,
        "parameters": (
            "Exact pixel-algorithm port of normalize-bellator-idle-v2.ps1: split the locked "
            "2033x773 RGBA source into four floor-divided cells; find each alpha>=128 source "
            "bounds; recenter horizontally by the bounds center; sample the fixed crop "
            "(top=5, height=762) to 32x48 with floor nearest-neighbor coordinates; set alpha "
            "below 128 to transparent white RGBA (255,255,255,0), other alpha to 255; "
            "quantize RGB to the locked 23-color Bellator palette using squared Euclidean "
            "distance and original palette order for ties. This is source reproduction only; "
            "no pose, silhouette, direction, or new art is introduced."
        ),
        "referencePixelOracle": {
            "path": LOCKED_INPUTS["referencePrototypeV2"]["path"],
            "sha256": LOCKED_INPUTS["referencePrototypeV2"]["sha256"],
            "role": "read-only technical pixel oracle; not an artistic acceptance",
        },
        "paletteHex": list(PALETTE_HEX),
        "outputEncoding": "deterministic RGBA8 PNG; pixel RGBA must equal the v2 oracle",
    }
    record["status"] = STATUS
    record["sourceGeneration"] = (
        "The original v1 image generation and literal prompt are preserved; this build "
        "performs no new image generation."
    )
    encoded = json.dumps(record, ensure_ascii=False, indent=2, separators=(",", ": "))
    return (encoded + "\n").encode("utf-8")


def _read_commit(root: Path) -> str | None:
    try:
        completed = subprocess.run(
            ["git", "--no-optional-locks", "-C", os.fspath(root), "rev-parse", "--verify", "HEAD"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=3,
            check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except (OSError, subprocess.SubprocessError):
        return None
    if completed.returncode != 0:
        return None
    value = completed.stdout.strip()
    return value if value else None


def _write_new_bundle(destination: Path, files: list[tuple[str, bytes]]) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.mkdir(exist_ok=False)
    created = []
    try:
        for name, payload in files:
            target = destination / name
            with target.open("xb") as stream:
                created.append(target)
                stream.write(payload)
    except OSError as exc:
        for target in reversed(created):
            try:
                target.unlink()
            except OSError:
                pass
        try:
            destination.rmdir()
        except OSError:
            pass
        raise BuildError(f"Could not write new output bundle: {exc}") from exc


def build(project_root: str, inputs: str, output_dir: str) -> dict:
    try:
        root = Path(project_root).resolve(strict=True)
    except OSError as exc:
        raise BuildError(f"--project-root cannot be resolved: {exc}") from exc
    if not root.is_dir():
        raise BuildError("--project-root must be a directory.")

    manifest_path = _lexical_absolute(root, inputs)
    _assert_no_reparse_components(root, manifest_path, "--inputs")
    try:
        manifest_resolved = manifest_path.resolve(strict=True)
        manifest_relative = manifest_resolved.relative_to(root).as_posix()
    except (OSError, ValueError) as exc:
        raise BuildError("--inputs must name a manifest inside --project-root.") from exc
    if manifest_relative != INPUT_MANIFEST_REPO_PATH:
        raise BuildError(f"--inputs must be {INPUT_MANIFEST_REPO_PATH} inside --project-root.")
    if not manifest_resolved.is_file() or manifest_resolved.stat().st_size > 64 * 1024:
        raise BuildError("--inputs must be a regular JSON file no larger than 64 KiB.")

    tool_path = _project_file(root, TOOL_REPO_PATH, "build tool")
    try:
        tool_bytes = tool_path.read_bytes()
    except OSError as exc:
        raise BuildError(f"Cannot read build tool bytes: {exc}") from exc
    tool_hash = _sha256(tool_bytes)
    try:
        manifest_bytes = manifest_resolved.read_bytes()
    except OSError as exc:
        raise BuildError(f"Cannot read input manifest: {exc}") from exc
    manifest_hash = _sha256(manifest_bytes)
    manifest = _strict_json_loads(manifest_bytes, "Input manifest")
    _validate_manifest(manifest)

    destination = _validate_output_destination(root, output_dir)

    loaded = {}
    input_hashes = {
        "manifest": {"path": INPUT_MANIFEST_REPO_PATH, "sha256": manifest_hash},
    }
    for name, locked in LOCKED_INPUTS.items():
        label = f"input {name}"
        path, data, digest = _read_locked_file(root, locked, label)
        loaded[name] = {"path": path, "bytes": data, "sha256": digest}
        input_hashes[name] = {"path": locked["path"], "sha256": digest}

    prompt_text = loaded["literalPrompt"]["bytes"].decode("utf-8")
    origin_record = _strict_json_loads(loaded["originRecordV1"]["bytes"], "Origin RESOURCE_RECORD.v1")
    if not isinstance(origin_record, dict):
        raise BuildError("Origin RESOURCE_RECORD.v1 must be a JSON object.")
    if origin_record.get("recordVersion") != 1:
        raise BuildError("Origin record is not the frozen v1 record.")
    if origin_record.get("literalPrompt") != prompt_text:
        raise BuildError("Literal prompt file differs from the original v1 record prompt.")
    if origin_record.get("sha256") != LOCKED_INPUTS["referencePrototypeV2"]["sha256"]:
        raise BuildError("Origin record does not identify the frozen v2 pixel oracle.")
    raw_source = origin_record.get("rawGeneratedSource")
    if not isinstance(raw_source, dict) or raw_source.get("sha256") != LOCKED_INPUTS["sourceGenerated"]["sha256"]:
        raise BuildError("Origin record does not identify the frozen generated source.")
    if origin_record.get("promptReference") != LOCKED_INPUTS["promptReferenceDocument"]["path"]:
        raise BuildError("Origin record prompt reference path differs from the locked F01 document.")

    source_width, source_height, source_rgba = decode_png_rgba(
        loaded["sourceGenerated"]["bytes"], (2033, 773)
    )
    reference_width, reference_height, reference_rgba = decode_png_rgba(
        loaded["referencePrototypeV2"]["bytes"], (128, 48)
    )
    candidate_pixels, frame_stats = normalize_source_rgba(source_rgba, source_width, source_height)
    if candidate_pixels != reference_rgba:
        raise BuildError(
            "Ported RGBA pixels differ from the frozen v2 reference; expected pixels were not changed."
        )
    verification = analyze_candidate(candidate_pixels)
    png_bytes = encode_png_rgba(128, 48, candidate_pixels)
    output_hash = _sha256(png_bytes)
    record_bytes = _build_record(
        origin_record,
        loaded["promptReferenceDocument"]["sha256"],
        output_hash,
    )

    commit = _read_commit(root)
    build_results = {
        "schemaVersion": 1,
        "status": STATUS,
        "commitExecuted": commit,
        "commitLookup": "git rev-parse HEAD; read-only; null only for a non-Git snapshot",
        "inputs": input_hashes,
        "tool": {
            "path": TOOL_REPO_PATH,
            "sha256": tool_hash,
            "runtime": f"Python {sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}; stdlib only",
        },
        "outputs": {
            "png": {"path": OUTPUT_NAMES["png"], "sha256": output_hash},
            "record": {
                "path": OUTPUT_NAMES["record"],
                "sha256": _sha256(record_bytes),
            },
        },
        "normalization": {
            "pixelMatchReferenceV2": True,
            "sourceDimensions": f"{source_width}x{source_height}",
            "frames": frame_stats,
            "verification": verification,
        },
    }
    build_results_bytes = (
        json.dumps(build_results, ensure_ascii=False, indent=2, separators=(",", ": ")) + "\n"
    ).encode("utf-8")

    # Recheck immediately before creating the destination; the operation never replaces one.
    destination = _validate_output_destination(root, output_dir)
    _write_new_bundle(
        destination,
        [
            (OUTPUT_NAMES["png"], png_bytes),
            (OUTPUT_NAMES["record"], record_bytes),
            (OUTPUT_NAMES["buildResults"], build_results_bytes),
        ],
    )
    return {
        "status": STATUS,
        "outputDir": destination.relative_to(root).as_posix(),
        "pngSha256": output_hash,
        "recordSha256": _sha256(record_bytes),
        "commitExecuted": commit,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", required=True, help="Snapshot or checkout root.")
    parser.add_argument("--inputs", required=True, help="Locked input manifest inside the project root.")
    parser.add_argument(
        "--output-dir",
        required=True,
        help="New output bundle directory below project-root/Logs/ArtSource.",
    )
    arguments = parser.parse_args(argv)
    try:
        result = build(arguments.project_root, arguments.inputs, arguments.output_dir)
    except BuildError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
