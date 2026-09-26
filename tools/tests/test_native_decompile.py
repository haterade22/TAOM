#!/usr/bin/env python3
"""Unit tests for native_decompile.py (headless Ghidra decompile of a native RVA).

Run:  python -m unittest tools.tests.test_native_decompile
  or:  python tools/tests/test_native_decompile.py

The unit tests need no Ghidra: report() and main() take a fake backend with the same
three calls the real one answers (function_at, decompile, callers). The real backend is
covered by one opt-in integration test, which runs only with GHIDRA_INSTALL_DIR set and
TAOM_GHIDRA_IT=1 because the first analysis of a binary takes minutes. It checks the
function entry Ghidra reports against the .pdata start native_crash_triage.py reports
for the same RVA: two independent readings of the same binary.
"""
import contextlib
import io
import os
import re
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_decompile as nd  # noqa: E402


class FakeBackend:
    """Stands in for GhidraBackend: functions by entry RVA, C text and callers by entry."""

    def __init__(self, functions, code=None, callers=None):
        self.functions = functions            # [nd.Function]
        self.code = code or {}                # entry_rva -> C text
        self.caller_map = callers or {}       # entry_rva -> [entry_rva]
        self.entered = False

    def __enter__(self):
        self.entered = True
        return self

    def __exit__(self, *exc):
        return False

    def _by_entry(self, entry):
        return next(f for f in self.functions if f.entry_rva == entry)

    def function_at(self, rva):
        for f in self.functions:
            if f.entry_rva <= rva < f.entry_rva + f.size:
                return f
        return None

    def decompile(self, fn):
        return self.code.get(fn.entry_rva, f"void {fn.name}(void) {{}}\n")

    def callers(self, fn):
        return [self._by_entry(e) for e in self.caller_map.get(fn.entry_rva, [])]


def _run_main(argv, backend=None, env=None):
    """main() with captured streams; returns (exit code, stdout, stderr)."""
    out, err = io.StringIO(), io.StringIO()
    factory = (lambda *a, **k: backend) if backend is not None else None
    with mock.patch.dict(os.environ, env or {}, clear=False), \
            contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = nd.main(argv, backend_factory=factory)
    return code, out.getvalue(), err.getvalue()


class CacheKeyTests(unittest.TestCase):
    def _dll(self, root, variant, data):
        p = Path(root) / variant / "TaleWorlds.Native.dll"
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
        return p

    def test_key_names_the_build_folder_and_a_hash_prefix(self):
        with tempfile.TemporaryDirectory() as d:
            key = nd.cache_key(self._dll(d, "Win64_Shipping_Client", b"MZ one"))
            self.assertRegex(key, r"^Win64_Shipping_Client-[0-9a-f]{16}$")

    def test_same_bytes_give_the_same_key(self):
        with tempfile.TemporaryDirectory() as d:
            a = nd.cache_key(self._dll(d, "Win64_Shipping_Client", b"MZ same"))
            b = nd.cache_key(self._dll(d, "Win64_Shipping_Client", b"MZ same"))
            self.assertEqual(a, b)

    def test_changed_bytes_give_a_new_key(self):
        # A Steam in-place overwrite changes the bytes, never the path.
        with tempfile.TemporaryDirectory() as d:
            p = self._dll(d, "Win64_Shipping_Client", b"MZ v1.5.2")
            before = nd.cache_key(p)
            p.write_bytes(b"MZ v1.5.3")
            self.assertNotEqual(before, nd.cache_key(p))

    def test_folder_characters_ghidra_rejects_are_made_safe(self):
        # A copy of the DLL in a crash-bundle folder is the normal case; Ghidra refuses '#',
        # '&' and friends in a project name.
        with tempfile.TemporaryDirectory() as d:
            key = nd.cache_key(self._dll(d, "crash #635 & co", b"MZ bundle"))
            self.assertRegex(key, r"^[A-Za-z0-9_-]+-[0-9a-f]{16}$")

    def test_client_and_editor_builds_never_share_a_key(self):
        with tempfile.TemporaryDirectory() as d:
            client = nd.cache_key(self._dll(d, "Win64_Shipping_Client", b"MZ x"))
            editor = nd.cache_key(self._dll(d, "Win64_Shipping_wEditor", b"MZ x"))
            self.assertNotEqual(client, editor)


class EngineVersionTests(unittest.TestCase):
    def test_reads_version_xml_beside_the_dll(self):
        with tempfile.TemporaryDirectory() as d:
            dll = Path(d) / "TaleWorlds.Native.dll"
            dll.write_bytes(b"MZ")
            (Path(d) / "Version.xml").write_text(
                '<Version>\n\t<Singleplayer Value="v1.5.3"/>\n</Version> ', encoding="utf-8")
            self.assertEqual(nd.engine_version(dll), "v1.5.3")

    def test_none_without_version_xml(self):
        with tempfile.TemporaryDirectory() as d:
            dll = Path(d) / "TaleWorlds.Native.dll"
            dll.write_bytes(b"MZ")
            self.assertIsNone(nd.engine_version(dll))


class ReportTests(unittest.TestCase):
    CRASH = nd.Function("FUN_180001040", 0x1040, 0x40)
    CALLER = nd.Function("FUN_180002000", 0x2000, 0x80)
    GRAND = nd.Function("FUN_180003000", 0x3000, 0x20)

    def _backend(self):
        return FakeBackend(
            [self.CRASH, self.CALLER, self.GRAND],
            code={0x1040: "int FUN_180001040(void)\n{\n  return *(int *)0;\n}\n"},
            callers={0x1040: [0x2000], 0x2000: [0x3000]})

    def _report(self, rva, levels):
        lines = []
        nd.report(self._backend(), rva, levels, out=lines.append)
        return "\n".join(lines)

    def test_names_the_function_and_the_offset_into_it(self):
        text = self._report(0x1050, 0)
        self.assertIn("function: FUN_180001040  entry 0x1040  size 0x40  (rva at +0x10)", text)

    def test_prints_the_decompiled_c(self):
        text = self._report(0x1050, 0)
        self.assertIn("return *(int *)0;", text)

    def test_no_caller_section_at_zero_levels(self):
        self.assertNotIn("callers of", self._report(0x1050, 0))

    def test_one_level_decompiles_the_caller_only(self):
        text = self._report(0x1050, 1)
        self.assertIn("L1 callers of 0x1040: 1", text)
        self.assertIn("--- FUN_180002000  entry 0x2000 ---", text)
        self.assertNotIn("FUN_180003000", text)

    def test_two_levels_climb_to_the_grandcaller(self):
        text = self._report(0x1050, 2)
        self.assertIn("L2 callers of 0x2000: 1", text)
        self.assertIn("--- FUN_180003000  entry 0x3000 ---", text)

    def test_callers_beyond_the_cap_are_counted_not_decompiled(self):
        # The cap keeps a hub function's callers from flooding an agent's context.
        many = [nd.Function(f"FUN_{0x5000 + i * 0x100:X}", 0x5000 + i * 0x100, 0x10)
                for i in range(nd.MAX_CALLERS + 2)]
        backend = FakeBackend([self.CRASH, *many], callers={0x1040: [f.entry_rva for f in many]})
        lines = []
        nd.report(backend, 0x1050, 1, out=lines.append)
        text = "\n".join(lines)
        self.assertEqual(text.count("\n--- FUN_"), nd.MAX_CALLERS)
        self.assertIn("(+2 more callers not decompiled)", text)

    def test_a_caller_shared_by_two_functions_is_printed_once(self):
        a = nd.Function("FUN_A", 0x2000, 0x80)
        b = nd.Function("FUN_B", 0x2100, 0x80)
        shared = nd.Function("FUN_SHARED", 0x3000, 0x20)
        backend = FakeBackend([self.CRASH, a, b, shared],
                              callers={0x1040: [0x2000, 0x2100], 0x2000: [0x3000], 0x2100: [0x3000]})
        lines = []
        nd.report(backend, 0x1050, 2, out=lines.append)
        text = "\n".join(lines)
        self.assertEqual(text.count("--- FUN_SHARED"), 1)
        self.assertIn("L2 callers of 0x2100: 0", text)

    def test_recursion_is_printed_once(self):
        # A function that calls itself must not be decompiled again as its own caller.
        backend = FakeBackend([self.CRASH], callers={0x1040: [0x1040]})
        lines = []
        nd.report(backend, 0x1050, 3, out=lines.append)
        text = "\n".join(lines)
        self.assertIn("L1 callers of 0x1040: 0", text)
        self.assertNotIn("--- FUN_180001040", text)

    def test_rva_outside_every_function_is_a_tool_error(self):
        with self.assertRaises(nd.ToolError) as cm:
            nd.report(self._backend(), 0x9999, 0, out=lambda _s: None)
        self.assertIn("0x9999", str(cm.exception))


class MainTests(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.dll = Path(self._dir.name) / "Win64_Shipping_Client" / "TaleWorlds.Native.dll"
        self.dll.parent.mkdir()
        self.dll.write_bytes(b"MZ fake")
        self.project = Path(self._dir.name) / "projects"

    def tearDown(self):
        self._dir.cleanup()

    def _argv(self, *extra):
        return ["--dll", str(self.dll), "--project-dir", str(self.project), *extra]

    def test_hex_rva_reaches_the_backend(self):
        backend = FakeBackend([ReportTests.CRASH])
        code, out, err = _run_main(self._argv("--rva", "0x1050"), backend)
        self.assertEqual(code, 0, err)
        self.assertTrue(backend.entered)
        self.assertIn("(rva at +0x10)", out)

    def test_header_names_the_module_key_and_project(self):
        code, out, _ = _run_main(self._argv("--rva", "0x1050"), FakeBackend([ReportTests.CRASH]))
        self.assertEqual(code, 0)
        self.assertTrue(out.startswith(f"module: {self.dll} (7 bytes)"), out)
        self.assertIn("engine: unknown (no Version.xml beside the DLL)", out)
        self.assertIn(f"ghidra project: {self.project / nd.cache_key(self.dll)}", out)

    def test_a_relative_project_dir_reaches_the_backend_absolute(self):
        # Ghidra's ProjectLocator refuses a relative path.
        seen = {}

        def factory(dll, project_dir, key):
            seen["project_dir"] = project_dir
            return FakeBackend([ReportTests.CRASH])

        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = nd.main(["--dll", str(self.dll), "--project-dir", "ghidra-cache", "--rva", "0x1050"],
                           backend_factory=factory)
        self.assertEqual(code, 0, err.getvalue())
        self.assertTrue(seen["project_dir"].is_absolute(), seen["project_dir"])

    def test_rva_outside_every_function_exits_2(self):
        code, _, err = _run_main(self._argv("--rva", "0x9999"), FakeBackend([ReportTests.CRASH]))
        self.assertEqual(code, 2)
        self.assertIn("0x9999", err)

    def test_missing_dll_exits_2(self):
        missing = str(self.dll) + ".nope"
        code, _, err = _run_main(["--dll", missing, "--rva", "0x10"], FakeBackend([]))
        self.assertEqual(code, 2)
        self.assertIn("not found", err)

    def _no_venv(self):
        return str(Path(self._dir.name) / "no-venv" / "python.exe")

    def test_missing_ghidra_install_exits_2_and_names_the_setup_doc(self):
        env = {nd.INSTALL_ENV: "", nd.PYTHON_ENV: self._no_venv()}
        code, _, err = _run_main(self._argv("--rva", "0x1050"), env=env)
        self.assertEqual(code, 2)
        self.assertIn(nd.INSTALL_ENV, err)
        self.assertIn(nd.SETUP_DOC, err)

    def test_missing_pyghidra_without_a_ghidra_python_exits_2_and_names_the_setup_doc(self):
        env = {nd.INSTALL_ENV: self._dir.name, nd.PYTHON_ENV: self._no_venv()}
        with mock.patch.dict(sys.modules, {"pyghidra": None}):
            code, _, err = _run_main(self._argv("--rva", "0x1050"), env=env)
        self.assertEqual(code, 2)
        self.assertIn("pyghidra", err)
        self.assertIn(nd.SETUP_DOC, err)

    def test_missing_pyghidra_reruns_under_the_ghidra_python(self):
        # pyghidra pins a JPype with no wheel for the system Python, so it lives in its own
        # venv; the command line must stay `python tools/native_decompile.py`.
        venv_python = Path(self._dir.name) / "venv" / "python.exe"
        venv_python.parent.mkdir()
        venv_python.write_bytes(b"")
        env = {nd.INSTALL_ENV: self._dir.name, nd.PYTHON_ENV: str(venv_python), nd.CHILD_ENV: ""}
        argv = self._argv("--rva", "0x1050")
        done = mock.Mock(returncode=3)
        with mock.patch.dict(sys.modules, {"pyghidra": None}), \
                mock.patch.object(nd.subprocess, "run", return_value=done) as run:
            code, out, _ = _run_main(argv, env=env)
        self.assertEqual(code, 3)
        run.assert_called_once()
        self.assertEqual(run.call_args.args[0], [str(venv_python), nd.__file__, *argv])
        self.assertEqual(run.call_args.kwargs["env"][nd.CHILD_ENV], "1")
        self.assertEqual(out, "", "the child prints the header, not the parent")

    def test_a_rerun_child_never_reruns_again(self):
        # A $TAOM_GHIDRA_PYTHON that is a launcher (py.exe) re-execs an interpreter without
        # pyghidra, which would re-exec again without bound.
        venv_python = Path(self._dir.name) / "venv" / "python.exe"
        venv_python.parent.mkdir()
        venv_python.write_bytes(b"")
        env = {nd.INSTALL_ENV: self._dir.name, nd.PYTHON_ENV: str(venv_python), nd.CHILD_ENV: "1"}
        with mock.patch.dict(sys.modules, {"pyghidra": None}), \
                mock.patch.object(nd.subprocess, "run") as run:
            code, _, err = _run_main(self._argv("--rva", "0x1050"), env=env)
        run.assert_not_called()
        self.assertEqual(code, 2)
        self.assertIn("pyghidra", err)


class ExitTests(unittest.TestCase):
    """A failure part-way through Ghidra's OSGi start leaves a non-daemon FelixDispatchQueue
    thread running, and the JVM then never lets the process end (thread dump, 2026-09-26)."""

    def test_exits_hard_once_the_jvm_has_started(self):
        jpype = mock.Mock()
        jpype.isJVMStarted.return_value = True
        with mock.patch.dict(sys.modules, {"jpype": jpype}), \
                mock.patch.object(nd.os, "_exit") as hard_exit:
            nd.exit_process(2)
        hard_exit.assert_called_once_with(2)

    def test_exits_normally_without_a_jvm(self):
        with mock.patch.dict(sys.modules, {"jpype": None}), \
                mock.patch.object(nd.os, "_exit") as hard_exit:
            with self.assertRaises(SystemExit) as cm:
                nd.exit_process(0)
        self.assertEqual(cm.exception.code, 0)
        hard_exit.assert_not_called()


def _unchained_pdata_rvas(pe, count=3):
    """RVAs just inside `count` .pdata functions spread across the table. Chained unwind
    entries are skipped: they describe a fragment of a parent function, which Ghidra
    folds into that parent."""
    po, psz = pe.pdata[3], pe.pdata[4]
    entries = []
    for i in range(0, psz - 11, 12):
        start, end, unwind = struct.unpack_from("<III", pe.d, po + i)
        if start == 0 or end - start < 0x20:
            continue
        off = pe.rva_to_off(unwind & ~1)
        if off is None or (pe.d[off] >> 3) & 0x4:  # UNW_FLAG_CHAININFO
            continue
        entries.append(start)
    step = max(1, len(entries) // (count + 1))
    return [entries[step * (n + 1)] + 4 for n in range(count)]


@unittest.skipUnless(os.environ.get(nd.INSTALL_ENV) and os.environ.get("TAOM_GHIDRA_IT") == "1",
                     "integration: set GHIDRA_INSTALL_DIR and TAOM_GHIDRA_IT=1")
class GhidraIntegrationTests(unittest.TestCase):
    """The whole CLI, venv re-run included, against the installed client DLL."""

    def test_function_entries_match_pdata(self):
        import native_crash_triage as nct
        dll = Path(nct.DEFAULT_DLL)
        if not dll.is_file():
            self.skipTest(f"no native DLL at {dll}")
        pe = nct.Pe(str(dll))
        for rva in _unchained_pdata_rvas(pe):
            with self.subTest(rva=hex(rva)):
                r = subprocess.run([sys.executable, nd.__file__, "--rva", hex(rva)],
                                   capture_output=True, timeout=3600)
                out = r.stdout.decode("utf-8", errors="replace")
                self.assertEqual(r.returncode, 0, out + r.stderr.decode("utf-8", errors="replace"))
                # Ghidra's C carries CRLF on Windows; printed as-is, text-mode stdout makes it CRCRLF.
                self.assertNotIn(b"\r\r\n", r.stdout)
                m = re.search(r"\bentry 0x([0-9A-F]+)\b", out)
                self.assertIsNotNone(m, out)
                self.assertEqual(int(m.group(1), 16), pe.func_of(rva)[0], out)
                self.assertIn("{", out[m.end():], "no decompiled body")

    def test_a_lone_gpr_marker_exits_2_with_the_cleanup_advice(self):
        # Deleting only the big .rep folder to free disk leaves a .gpr that Ghidra's
        # createProject cannot get past; without advice, every later run fails the same way.
        import native_crash_triage as nct
        dll = Path(nct.DEFAULT_DLL)
        if not dll.is_file():
            self.skipTest(f"no native DLL at {dll}")
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / f"{nd.cache_key(dll)}.gpr").write_bytes(b"")
            r = subprocess.run([sys.executable, nd.__file__, "--rva", "0x6590B9", "--project-dir", d],
                               capture_output=True, timeout=600)
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
        self.assertIn(b"delete", r.stderr)


if __name__ == "__main__":
    unittest.main()
