#!/usr/bin/env python3
"""
Serve the Unity web build (Build/Web) to other devices on the local network.

    python3 Tools/serve_web.py                 # http://<this-mac>:8000
    python3 Tools/serve_web.py --port 9000
    python3 Tools/serve_web.py --dir path/to/other/build

Standard library only (Python 3.7+). Unity-specific details:
  * .wasm is served as application/wasm so browsers can stream-compile it.
  * .gz / .br build files (if compression is ever turned back on) get the matching
    Content-Encoding header so the loader can use them directly.
  * Everything is sent with Cache-Control: no-cache, so a phone always picks up a fresh
    build after you rebuild, instead of mixing old and new files.
"""
import argparse
import datetime
import errno
import functools
import http.server
import os
import re
import socket
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DEFAULT_DIR = os.path.join(ROOT, "Build", "Web")

CONTENT_TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
    ".json": "application/json",
    ".html": "text/html; charset=utf-8",
    ".png": "image/png",
    ".ico": "image/x-icon",
}
ENCODINGS = {".gz": "gzip", ".br": "br"}


class BuildHandler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        base, ext = os.path.splitext(path)
        if ext in ENCODINGS:                      # Web.wasm.br -> type of Web.wasm
            base, ext = os.path.splitext(base)
        return CONTENT_TYPES.get(ext.lower()) or super().guess_type(path)

    def end_headers(self):
        ext = os.path.splitext(self.path.split("?", 1)[0])[1]
        if ext in ENCODINGS:
            self.send_header("Content-Encoding", ENCODINGS[ext])
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
        super().end_headers()

    def log_message(self, fmt, *args):
        # One short line per request: time, client, status, path.
        stamp = datetime.datetime.now().strftime("%H:%M:%S")
        sys.stderr.write("%s  %-15s %s\n" % (stamp, self.client_address[0], fmt % args))


def lan_address():
    """IP of the interface on the default route (no packets are actually sent)."""
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(("192.0.2.1", 9))
        return s.getsockname()[0]
    except OSError:
        return None
    finally:
        s.close()


def bonjour_name():
    """The .local name other devices can resolve. On macOS that is LocalHostName, not the DHCP hostname."""
    if sys.platform == "darwin":
        try:
            import subprocess
            name = subprocess.run(["scutil", "--get", "LocalHostName"], capture_output=True, text=True, timeout=3).stdout.strip()
            if name:
                return name + ".local"
        except (OSError, subprocess.SubprocessError):
            pass
    name = socket.gethostname().split(".")[0]
    return name + ".local" if name else None


def build_version(directory):
    try:
        with open(os.path.join(directory, "index.html"), encoding="utf-8") as f:
            m = re.search(r'productVersion:\s*"([^"]*)"', f.read())
            return m.group(1) if m else "unknown"
    except OSError:
        return None


def bind(host, port, handler, tries=20):
    for p in range(port, port + tries):
        try:
            return http.server.ThreadingHTTPServer((host, p), handler)
        except OSError as e:
            if e.errno != errno.EADDRINUSE:
                raise
    sys.exit("No free port between %d and %d." % (port, port + tries - 1))


def main():
    ap = argparse.ArgumentParser(description="Serve the Squad Rush web build on the local network.")
    ap.add_argument("--port", type=int, default=8000, help="first port to try (default 8000; the next free one is used)")
    ap.add_argument("--host", default="0.0.0.0", help="interface to bind (default: all)")
    ap.add_argument("--dir", default=DEFAULT_DIR, help="build folder containing index.html (default: Build/Web)")
    args = ap.parse_args()

    directory = os.path.abspath(args.dir)
    version = build_version(directory)
    if version is None:
        sys.exit("No index.html in %s. Build the web version first (SquadRush > Build Web in Unity)." % directory)

    handler = functools.partial(BuildHandler, directory=directory)
    server = bind(args.host, args.port, handler)
    port = server.server_address[1]

    print("Serving %s (build %s)" % (os.path.relpath(directory, os.getcwd()), version))
    print()
    ip = lan_address()
    if ip:
        print("  On your phone or another computer:  http://%s:%d/" % (ip, port))
    name = bonjour_name()
    if name:
        print("  By name (Apple devices):            http://%s:%d/" % (name, port))
    print("  On this Mac:                        http://localhost:%d/" % port)
    print()
    print("Devices must be on the same network. Press Ctrl+C to stop.")
    sys.stdout.flush()

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
