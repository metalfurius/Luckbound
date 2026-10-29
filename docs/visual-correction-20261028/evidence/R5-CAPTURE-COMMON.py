"""Serve one immutable CI WebGL artifact silently on loopback.

Only the HTML wrapper is instrumented; build bytes and gameplay are untouched.
The requested canvas dimensions are visible in the URL and capture manifest.
"""
import argparse
import mimetypes
import re
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--build', type=Path, required=True)
parser.add_argument('--width', type=int, choices=(960, 1280), required=True)
parser.add_argument('--height', type=int, choices=(600, 720), required=True)
parser.add_argument('--port', type=int, default=8093)
args = parser.parse_args()
build = args.build.resolve(strict=True)
assert (args.width, args.height) in ((960, 600), (1280, 720))
assert (build / 'index.html').is_file()

instrumentation = '''<script>
// Audio is disconnected before Unity starts, including after input gestures.
AudioNode.prototype.connect = function(destination) { return destination; };
AudioNode.prototype.disconnect = function() {};
const captureNativePlay = HTMLMediaElement.prototype.play;
HTMLMediaElement.prototype.play = function() {
  this.muted = true; this.volume = 0;
  return captureNativePlay.apply(this, arguments);
};
// Preserve errors in console without a host-visible alert dialog.
window.alert = function(message) { console.error('CAPTURE_ALERT', message); };
document.documentElement.dataset.captureSilent = 'true';
</script>'''

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(build), **kw)

    def do_GET(self):
        if self.path.split('?')[0] in ('/', '/index.html'):
            body = (build / 'index.html').read_text(encoding='utf-8')
            body = body.replace('<head>', '<head>' + instrumentation, 1)
            body = re.sub(r'(id="unity-canvas" width=")\d+(" height=")\d+',
                          lambda m: m[1] + str(args.width) + m[2] + str(args.height), body)
            # Unity's desktop template assigns CSS dimensions after creation.
            body = body.replace('canvas.style.width = "960px"',
                                f'canvas.style.width = "{args.width}px"')
            body = body.replace('canvas.style.height = "600px"',
                                f'canvas.style.height = "{args.height}px"')
            body = body.replace('fullscreenContainer.style.width = "960px"',
                                f'fullscreenContainer.style.width = "{args.width}px"')
            body = body.replace('fullscreenContainer.style.height = "600px"',
                                f'fullscreenContainer.style.height = "{args.height}px"')
            # Capture the actual game canvas at the requested dimensions,
            # without the template's outside-game logo/fullscreen footer.
            style = f'''<style>
html,body {{ margin:0; padding:0; overflow:hidden; }}
#unity-container.unity-desktop {{ position:absolute; left:0; top:0; transform:none; }}
#unity-canvas {{ width:{args.width}px !important; height:{args.height}px !important; }}
#unity-footer {{ display:none; }}
</style>'''
            body = body.replace('</head>', style + '</head>', 1)
            data = body.encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.send_header('Cache-Control', 'no-store')
            self.end_headers()
            self.wfile.write(data)
        else:
            super().do_GET()

    def guess_type(self, path):
        return mimetypes.guess_type(path[:-3] if path.endswith('.br') else path)[0] or 'application/octet-stream'

    def end_headers(self):
        if self.path.split('?')[0].endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        super().end_headers()

print(f'Capture only: {build}; canvas {args.width}x{args.height}; loopback {args.port}', flush=True)
ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
