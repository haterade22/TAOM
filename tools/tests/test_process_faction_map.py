#!/usr/bin/env python3
"""Tests for tools/process_faction_map.py: file paths reach the child scripts as data.

Run:  python -m unittest tools.tests.test_process_faction_map -v

Both helpers run a child `python -c` script. Before plan 005 each path was pasted
into that script's source as r'<path>', so a quote in a folder name broke the
child with a SyntaxError and let the folder's text run as code. The parent then
reported the failure as "EMPTY (fully transparent)". These cases fail on that
version (a39a9c86) and pass once the paths travel through sys.argv.
"""
import io
import os
import struct
import sys
import tempfile
import unittest
import zlib
from contextlib import redirect_stderr, redirect_stdout

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import process_faction_map as pfm  # noqa: E402

WIDTH, HEIGHT = 16, 12
# Opaque block covers x 3..9 and y 2..7, so the box is (x, y, w, h, canvas_w, canvas_h).
BOX = (3, 2, 7, 6, WIDTH, HEIGHT)


def _chunk(kind, data):
    crc = zlib.crc32(kind + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", crc)


def _rgba_png():
    """A WIDTHxHEIGHT RGBA PNG, every row filter 0, one opaque block."""
    def pixel(x, y):
        return b"\xc8\x64\x32\xff" if 3 <= x <= 9 and 2 <= y <= 7 else b"\x00\x00\x00\x00"

    raw = b"".join(
        b"\x00" + b"".join(pixel(x, y) for x in range(WIDTH)) for y in range(HEIGHT))
    return (b"\x89PNG\r\n\x1a\n"
            + _chunk(b"IHDR", struct.pack(">IIBBBBB", WIDTH, HEIGHT, 8, 6, 0, 0, 0))
            + _chunk(b"IDAT", zlib.compress(raw))
            + _chunk(b"IEND", b""))


class PathsAreDataNotCode(unittest.TestCase):
    """find_alpha_bbox and crop_png_to_bbox work whatever the folder is called."""

    def _assert_round_trip(self, folder):
        with tempfile.TemporaryDirectory() as tmp:
            work = os.path.join(tmp, folder)
            os.makedirs(work)
            src = os.path.join(work, "region_x.png")
            out = os.path.join(work, "region_x_crop.png")
            with open(src, "wb") as f:
                f.write(_rgba_png())

            with redirect_stderr(io.StringIO()):
                self.assertEqual(pfm.find_alpha_bbox(src), BOX)
            with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                self.assertTrue(pfm.crop_png_to_bbox(src, out, BOX))

            with open(out, "rb") as f:
                header = f.read(24)
            self.assertEqual(struct.unpack(">II", header[16:24]), (7, 6))
            # The crop is fully opaque, so its own box is the whole image.
            self.assertEqual(pfm.find_alpha_bbox(out), (0, 0, 7, 6, 7, 6))

    def test_plain_folder(self):
        self._assert_round_trip("plain")

    def test_quote_in_folder(self):
        self._assert_round_trip("it's here")

    def test_python_text_in_folder_is_never_run(self):
        self._assert_round_trip("a'+str(__import__('sys').stderr.write('X'))+'")


if __name__ == "__main__":
    unittest.main()
