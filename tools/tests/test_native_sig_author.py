#!/usr/bin/env python3
"""Unit tests for native_sig_author.py.

Run:  python -m unittest tools.tests.test_native_sig_author

The xref sweep is tested on a synthetic .text blob, so no DLL is needed. capstone is required;
pefile is not (the tests never open a PE), so a stand-in module is used when it is missing.
"""
import contextlib
import importlib
import io
import os
import struct
import sys
import types
import unittest
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

try:
    import capstone  # noqa: F401  (availability probe)
    HAVE_CAPSTONE = True
except ImportError:
    HAVE_CAPSTONE = False


def _load_tool():
    modules = {}
    try:
        import pefile  # noqa: F401  (availability probe)
    except ImportError:
        modules["pefile"] = types.ModuleType("pefile")
    with mock.patch.dict(sys.modules, modules), mock.patch.object(sys, "path", [TOOLS] + sys.path):
        sys.modules.pop("native_sig_author", None)
        return importlib.import_module("native_sig_author")


BASE = 0x180000000
TEXT_VA = 0x1000


def _image(tool, blob):
    return types.SimpleNamespace(
        base=BASE,
        data=blob,
        text=(".text", TEXT_VA, len(blob), 0, len(blob)),
        md=tool.make_disassembler())


def _lea_rax_to(target_rva, at_rva):
    # lea rax, [rip + disp32]: 48 8D 05 d32, 7 bytes, next ip = at_rva + 7.
    return bytes([0x48, 0x8D, 0x05]) + struct.pack("<i", target_rva - (at_rva + 7))


@unittest.skipUnless(HAVE_CAPSTONE, "capstone is not installed")
class XrefSweepTests(unittest.TestCase):
    def _xref(self, tool, blob, target):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            tool.cmd_xref(_image(tool, blob), types.SimpleNamespace(rva=f"{target:x}"))
        return out.getvalue()

    def test_reference_after_an_undecodable_byte_is_found(self):
        # 0x06 (push es) does not exist in 64-bit mode: a plain linear sweep stops on it (#780).
        tool = _load_tool()
        target = 0x9000
        prefix = bytes([0x90, 0x06, 0x90])
        lea_at = TEXT_VA + len(prefix)
        blob = prefix + _lea_rax_to(target, lea_at)

        out = self._xref(tool, blob, target)

        self.assertIn(f"xrefs to 0x{target:X}: 1", out)
        self.assertIn(f"0x{lea_at:X}  lea", out)

    def test_reference_before_the_byte_is_still_found_once(self):
        tool = _load_tool()
        target = 0x9000
        blob = _lea_rax_to(target, TEXT_VA) + bytes([0x06, 0x90])

        out = self._xref(tool, blob, target)

        self.assertIn(f"xrefs to 0x{target:X}: 1", out)


if __name__ == "__main__":
    unittest.main()
