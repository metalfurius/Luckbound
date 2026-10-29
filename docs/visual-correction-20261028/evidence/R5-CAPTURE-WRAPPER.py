"""Run the existing silent WebGL capture helper with a local screenshot sink.

The helper remains the page server and audio-silencing wrapper. This narrow
adapter only accepts native screenshot bytes from the private capture tab and
writes the unchanged JPEG or PNG payload to the new capture-attempt folder.
"""
from http.server import ThreadingHTTPServer as BaseCaptureServer
from pathlib import Path
import http.server
import json
import os
import re
import runpy
import sys

ROOT = Path(__file__).resolve().parents[2]
HELPER = ROOT / "Logs" / "VisualCorrection-20261028" / "serve_capture.py"
BUILD = ROOT / "Logs" / "CI-40872571136-review" / "builds" / "WebGL" / "extracted" / "WebGL"
DESTINATION = ROOT / "docs" / "progress_visual" / "2026-10-29-ci-8526985" / "fresh-origin-18526"


class CaptureServer(BaseCaptureServer):
    def __init__(self, address, handler_class, bind_and_activate=True):
        base_handler = handler_class

        class ImageSinkHandler(base_handler):
            def end_headers(self):
                self.send_header("Cache-Control", "no-store")
                super().end_headers()

            def do_POST(self):
                if self.path == '/__capture_metadata__':
                    length = int(self.headers.get('Content-Length', '0'))
                    if not 0 < length <= 1024 * 1024:
                        self.send_error(413)
                        return
                    payload = self.rfile.read(length)
                    metadata = json.loads(payload)
                    if not isinstance(metadata, dict) or not isinstance(metadata.get('captures'), list):
                        self.send_error(400)
                        return
                    DESTINATION.mkdir(parents=True, exist_ok=True)
                    (DESTINATION / 'SESSION-RAW.json').write_bytes(payload)
                    self.send_response(201)
                    self.send_header('Content-Length', '0')
                    self.end_headers()
                    return
                match = re.fullmatch(r"/__capture__/([A-Za-z0-9_.-]+\.(?:png|jpg))", self.path)
                if match is None:
                    self.send_error(404)
                    return
                try:
                    length = int(self.headers.get("Content-Length", "0"))
                except ValueError:
                    self.send_error(400, "invalid content length")
                    return
                if length <= 0 or length > 20 * 1024 * 1024:
                    self.send_error(413, "capture size out of bounds")
                    return
                image_bytes = self.rfile.read(length)
                suffix = Path(match.group(1)).suffix.lower()
                expected_magic = b"\x89PNG\r\n\x1a\n" if suffix == ".png" else b"\xff\xd8\xff"
                if len(image_bytes) != length or not image_bytes.startswith(expected_magic):
                    self.send_error(400, "image extension and screenshot bytes do not match")
                    return
                destination = DESTINATION / match.group(1)
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(image_bytes)
                body = f"stored {destination.name} ({len(image_bytes)} bytes)".encode("ascii")
                self.send_response(201)
                self.send_header("Content-Type", "text/plain; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

        super().__init__(address, ImageSinkHandler, bind_and_activate)


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("960", "1280"):
        raise SystemExit("usage: serve_capture_review.py 960|1280")
    width = int(sys.argv[1])
    height = 600 if width == 960 else 720
    if not HELPER.is_file() or not (BUILD / "index.html").is_file():
        raise SystemExit("capture helper or verified WebGL build is missing")
    DESTINATION.mkdir(parents=True, exist_ok=True)
    log_stream = (Path(__file__).resolve().parent / "server-console.log").open(
        "a", encoding="utf-8", buffering=1
    )
    sys.stdout = log_stream
    sys.stderr = log_stream
    print(f"R5 capture server starting; pid={os.getpid()}; argv={sys.argv!r}", flush=True)
    http.server.ThreadingHTTPServer = CaptureServer
    sys.argv = [
        str(HELPER),
        "--build", str(BUILD),
        "--width", str(width),
        "--height", str(height),
        "--port", "18526",
    ]
    runpy.run_path(str(HELPER), run_name="__main__")


if __name__ == "__main__":
    main()

