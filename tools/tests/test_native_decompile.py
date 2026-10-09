#!/usr/bin/env python3
"""Unit tests for native_decompile.py (headless Ghidra decompile of native engine code).

Run:  python -m unittest tools.tests.test_native_decompile
  or:  python tools/tests/test_native_decompile.py

The unit tests need no Ghidra: report() and main() take a fake backend with the same
calls the real one answers (function_at, decompile, callers, referencing_functions,
thunk_target), and the real backend's thunk logic is tested against a mocked program.
The real backend runs in opt-in integration tests (GHIDRA_INSTALL_DIR set and
TAOM_GHIDRA_IT=1), because the first analysis of a binary takes minutes. One checks the
function entry Ghidra reports against the .pdata start native_crash_triage.py reports for
the same RVA, two independent readings of the same binary; the others pin v1.5.3 answers
of each mode and skip on any other engine.
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
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import native_decompile as nd  # noqa: E402
import native_engine_methods as nem  # noqa: E402
from test_native_crash_triage_dump import _build_pe  # noqa: E402


class FakeBackend:
    """Stands in for GhidraBackend: functions by entry RVA, C text and callers by entry."""

    def __init__(self, functions, code=None, callers=None, refs=None, thunks=None):
        self.functions = functions            # [nd.Function]
        self.code = code or {}                # entry_rva -> C text
        self.caller_map = callers or {}       # entry_rva -> [entry_rva]
        self.refs = refs or {}                # data rva -> [entry_rva of referencing functions]
        self.thunks = thunks or {}            # thunk entry_rva -> the entry_rva it jumps to
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

    def referencing_functions(self, rva):
        return [self._by_entry(e) for e in self.refs.get(rva, [])]

    def thunk_target(self, fn):
        target = self.thunks.get(fn.entry_rva)
        return None if target is None else self._by_entry(target)


def _run_main(argv, backend=None, env=None, engine_map=None, map_error=None):
    """main() with captured streams; returns (exit code, stdout, stderr). Without engine_map
    or map_error the real map loader runs, and finds no managed DLLs beside a fake DLL."""
    out, err = io.StringIO(), io.StringIO()
    factory = (lambda *a, **k: backend) if backend is not None else None
    map_factory = None
    if engine_map is not None or map_error is not None:
        def map_factory(*_a):
            if map_error is not None:
                raise nem.EngineMapError(map_error)
            return engine_map
    with mock.patch.dict(os.environ, env or {}, clear=False), \
            contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = nd.main(argv, backend_factory=factory, map_factory=map_factory)
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

        def factory(dll, project_dir, key, **_kw):
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


ENGINE_MAP = nem.EngineMethodMap([
    nem.Resolved("MountAndBlade", 69, "IMBAgent", "GetCurrentActionType", "get_current_action_type", 0x1040),
    nem.Resolved("MountAndBlade", 5, "IMBMission", "GetName", "get_name", 0x2000),
    nem.Resolved("Engine", 2, "IScene", "GetName", "get_name", 0x3000),
])


class EngineMethodModeTests(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.dll = Path(self._dir.name) / "Win64_Shipping_Client" / "TaleWorlds.Native.dll"
        self.dll.parent.mkdir()
        self.dll.write_bytes(b"MZ fake")

    def tearDown(self):
        self._dir.cleanup()

    def _run(self, *args, backend=None, **kw):
        argv = ["--dll", str(self.dll), "--project-dir", str(Path(self._dir.name) / "p"), *args]
        return _run_main(argv, backend or FakeBackend([ReportTests.CRASH]), **kw)

    def test_resolves_the_implementation_and_decompiles_it(self):
        code, out, err = self._run("--engine-method", "IMBAgent.GetCurrentActionType", engine_map=ENGINE_MAP)
        self.assertEqual(code, 0, err)
        self.assertIn("resolved 'IMBAgent.GetCurrentActionType' -> 0x1040", out)
        self.assertIn("function: FUN_180001040  entry 0x1040", out)
        self.assertIn("engine method: IMBAgent.GetCurrentActionType = get_current_action_type "
                      "(MountAndBlade id 69)", out)

    def test_a_blank_name_exits_2(self):
        code, _, err = self._run("--engine-method", " ", engine_map=ENGINE_MAP)
        self.assertEqual(code, 2)
        self.assertIn("empty", err)

    def test_a_problem_in_another_assembly_does_not_refuse(self):
        partial = nem.EngineMethodMap(ENGINE_MAP.methods, ["DotNet: 1 managed id(s) not registered"])
        code, out, err = self._run("--engine-method", "IMBAgent.GetCurrentActionType", engine_map=partial)
        self.assertEqual(code, 0, err)
        self.assertIn("-> 0x1040", out)

    def test_a_thunk_is_followed_to_the_code_it_jumps_to(self):
        # 100 single-owner implementations are thunks; their own C is one jump.
        target = nd.Function("FUN_180002000", 0x2000, 0x80)
        backend = FakeBackend([ReportTests.CRASH, target], thunks={0x1040: 0x2000},
                              code={0x2000: "void FUN_180002000(void)\n{\n  preload();\n}\n"})
        code, out, err = self._run("--engine-method", "IMBAgent.GetCurrentActionType", backend=backend,
                                   engine_map=ENGINE_MAP)
        self.assertEqual(code, 0, err)
        self.assertIn("thunk to: FUN_180002000  entry 0x2000", out)
        self.assertIn("preload();", out)

    def test_an_assembly_whose_map_is_incomplete_is_refused(self):
        # A partial sweep is an unverified answer; exiting 0 with it is the failure to avoid.
        partial = nem.EngineMethodMap(ENGINE_MAP.methods, ["MountAndBlade: 1 managed id(s) not registered"])
        code, _, err = self._run("--engine-method", "IMBAgent.GetCurrentActionType", engine_map=partial)
        self.assertEqual(code, 2)
        self.assertIn("incomplete", err)

    def test_any_map_failure_leaves_rva_working(self):
        def broken(*_a):
            raise ValueError("Expecting value: line 1 column 41")

        argv = ["--dll", str(self.dll), "--project-dir", str(Path(self._dir.name) / "p"), "--rva", "0x1050"]
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = nd.main(argv, backend_factory=lambda *a, **k: FakeBackend([ReportTests.CRASH]),
                           map_factory=broken)
        self.assertEqual(code, 0, err.getvalue())
        self.assertIn("engine methods: unavailable (ValueError: Expecting value", out.getvalue())

    def test_a_name_on_two_interfaces_exits_2_naming_both(self):
        code, _, err = self._run("--engine-method", "get_name", engine_map=ENGINE_MAP)
        self.assertEqual(code, 2)
        self.assertIn("IMBMission.GetName", err)
        self.assertIn("IScene.GetName", err)

    def test_an_unknown_name_exits_2_with_suggestions(self):
        code, _, err = self._run("--engine-method", "current_action", engine_map=ENGINE_MAP)
        self.assertEqual(code, 2)
        self.assertIn("IMBAgent.GetCurrentActionType (get_current_action_type)", err)

    def test_the_mode_needs_the_map(self):
        code, _, err = self._run("--engine-method", "get_name", map_error="ilspycmd is not on PATH")
        self.assertEqual(code, 2)
        self.assertIn("ilspycmd is not on PATH", err)

    def test_an_rva_in_a_registered_implementation_is_named(self):
        code, out, _ = self._run("--rva", "0x1050", engine_map=ENGINE_MAP)
        self.assertEqual(code, 0)
        self.assertIn("engine method: IMBAgent.GetCurrentActionType = get_current_action_type "
                      "(MountAndBlade id 69)", out)

    def test_an_rva_without_a_map_still_decompiles(self):
        code, out, _ = self._run("--rva", "0x1050", map_error="no AutoGenerated DLLs here")
        self.assertEqual(code, 0)
        self.assertIn("engine methods: unavailable (no AutoGenerated DLLs here)", out)
        self.assertIn("function: FUN_180001040", out)

    def test_the_backend_receives_the_map_to_seed_its_project(self):
        seen = {}

        def factory(dll, project_dir, key, engine_map=None):
            seen["map"] = engine_map
            return FakeBackend([ReportTests.CRASH])

        argv = ["--dll", str(self.dll), "--project-dir", str(Path(self._dir.name) / "p"), "--rva", "0x1050"]
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            nd.main(argv, backend_factory=factory, map_factory=lambda *_a: ENGINE_MAP)
        self.assertIs(seen["map"], ENGINE_MAP)

    def test_rva_engine_method_and_string_are_exclusive(self):
        with self.assertRaises(SystemExit):
            with contextlib.redirect_stderr(io.StringIO()):
                nd.main(["--rva", "0x10", "--string", "x"])


class StringModeTests(unittest.TestCase):
    """_build_pe() holds 'monster_usage.cpp' at .rdata rva 0x2000; a UTF-16 string is added."""

    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.dll = Path(self._dir.name) / "Win64_Shipping_Client" / "TaleWorlds.Native.dll"
        self.dll.parent.mkdir()
        pe = bytearray(_build_pe())
        wide = "IMono_Test::probe".encode("utf-16-le") + b"\x00\x00"
        pe[0x420:0x420 + len(wide)] = wide
        self.dll.write_bytes(bytes(pe))

    def tearDown(self):
        self._dir.cleanup()

    def _run(self, text, refs=None):
        backend = FakeBackend([ReportTests.CRASH], refs=refs or {})
        argv = ["--dll", str(self.dll), "--project-dir", str(Path(self._dir.name) / "p"), "--string", text]
        return _run_main(argv, backend, map_error="none")

    def test_an_ascii_string_reports_and_decompiles_its_referencing_functions(self):
        code, out, err = self._run("monster_usage.cpp", refs={0x2000: [0x1040]})
        self.assertEqual(code, 0, err)
        self.assertIn("string 'monster_usage.cpp' at 0x2000 (ascii): 1 referencing function(s)", out)
        self.assertIn("--- FUN_180001040  entry 0x1040 ---", out)

    def test_a_substring_reports_the_whole_string(self):
        _, out, _ = self._run("usage", refs={0x2000: [0x1040]})
        self.assertIn("string 'monster_usage.cpp' at 0x2000 (ascii)", out)

    def test_utf16_strings_are_found(self):
        _, out, _ = self._run("probe")
        self.assertIn("string 'IMono_Test::probe' at 0x2020 (utf-16)", out)

    def test_a_string_no_code_references_says_so(self):
        _, out, _ = self._run("monster_usage.cpp")
        self.assertIn("no code references", out)

    def test_text_absent_from_the_dll_exits_2(self):
        code, _, err = self._run("not in this binary")
        self.assertEqual(code, 2)
        self.assertIn("not found", err)

    def test_empty_text_exits_2_instead_of_matching_everything(self):
        code, _, err = self._run("")
        self.assertEqual(code, 2)
        self.assertIn("empty", err)

    def test_callers_with_string_is_refused_not_ignored(self):
        backend = FakeBackend([ReportTests.CRASH])
        argv = ["--dll", str(self.dll), "--project-dir", str(Path(self._dir.name) / "p"),
                "--string", "usage", "--callers", "1"]
        code, _, err = _run_main(argv, backend, map_error="none")
        self.assertEqual(code, 2)
        self.assertIn("--callers", err)


class SeedTests(unittest.TestCase):
    def test_only_an_address_one_method_owns_gets_a_name(self):
        # 108 methods share one v1.5.3 stub; naming it after any of them mislabels every caller.
        m = nem.EngineMethodMap([
            nem.Resolved("MountAndBlade", 1, "IMBAgent", "GetName", "get_name", 0x100),
            nem.Resolved("Engine", 7, "IDebug", "GetShowDebugInfo", "get_show_debug_info", 0x200),
            nem.Resolved("Engine", 8, "IUtil", "GetBuildNumber", "get_build_number", 0x200),
        ])
        self.assertEqual(nd.seed_names(m), {0x100: "IMBAgent_GetName"})

    def test_seeding_waits_for_a_complete_map_and_an_older_seed(self):
        clean = nem.EngineMethodMap(ENGINE_MAP.methods)
        partial = nem.EngineMethodMap(ENGINE_MAP.methods, ["DotNet: no managed ids parsed"])
        self.assertTrue(nd.should_seed(clean, applied=0))
        self.assertFalse(nd.should_seed(clean, applied=nd.SEED_VERSION))
        self.assertFalse(nd.should_seed(partial, applied=0))
        self.assertFalse(nd.should_seed(None, applied=0))


class ThunkTests(unittest.TestCase):
    """GhidraBackend.thunk_target against a mocked program: no Ghidra needed."""

    def _backend(self, target):
        jfn = mock.Mock()
        jfn.isThunk.return_value = True
        jfn.getThunkedFunction.return_value = target
        program = mock.Mock()
        program.getFunctionManager.return_value.getFunctionAt.return_value = jfn
        backend = nd.GhidraBackend.__new__(nd.GhidraBackend)
        backend.program = program
        return backend

    def test_a_thunk_to_an_import_has_no_target_to_decompile(self):
        # jmp [__imp_AcquireSRWLockShared]: the target is EXTERNAL, and its address cannot be
        # subtracted from the image base (the crash seen on --rva 0x9DB690).
        target = mock.Mock()
        target.isExternal.return_value = True
        self.assertIsNone(self._backend(target).thunk_target(nd.Function("stub", 0x9DB690, 7)))

    def test_a_thunk_to_code_in_the_binary_returns_it(self):
        target = mock.Mock()
        target.isExternal.return_value = False
        target.getName.return_value = "FUN_180153850"
        target.getEntryPoint.return_value.subtract.return_value = 0x153850
        target.getBody.return_value.getNumAddresses.return_value = 0x40
        got = self._backend(target).thunk_target(nd.Function("thunk", 0x4A0CF0, 5))
        self.assertEqual(got, nd.Function("FUN_180153850", 0x153850, 0x40))

    def test_a_thunk_target_is_not_printed_again_as_a_caller(self):
        target = nd.Function("FUN_180002000", 0x2000, 0x80)
        backend = FakeBackend([ReportTests.CRASH, target], thunks={0x1040: 0x2000}, callers={0x1040: [0x2000]})
        lines = []
        nd.report(backend, 0x1050, 1, out=lines.append)
        self.assertNotIn("--- FUN_180002000", "\n".join(lines))


class CallerLabelTests(unittest.TestCase):
    def test_a_caller_that_implements_an_engine_method_is_labelled(self):
        caller = nd.Function("FUN_180002000", 0x2000, 0x80)
        backend = FakeBackend([ReportTests.CRASH, caller], callers={0x1040: [0x2000]})
        lines = []
        nd.report(backend, 0x1050, 1, out=lines.append, engine_map=ENGINE_MAP)
        text = "\n".join(lines)
        self.assertIn("--- FUN_180002000  entry 0x2000 ---\nengine method: IMBMission.GetName = get_name", text)


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

    # Client addresses per engine version. Add a row at every engine bump (derive the values with
    # native_engine_methods / this CLI); an unlisted version skips rather than passing silently.
    # The stub is the REX `48 FF 25` jump to KERNEL32!AcquireSRWLockShared.
    PINS = {
        "v1.5.3": {"set_attack_state": "0x6DF670", "weapon_equipped": "0x6E0100",
                   "process_preload_queue": "0x4A0CF0", "import_stub": "0x9DB690",
                   "get_current_action_type": 0x6E19B0},
        "v1.5.4": {"set_attack_state": "0x6DF900", "weapon_equipped": "0x6E0390",
                   "process_preload_queue": "0x4A0D50", "import_stub": "0x9DB920",
                   "get_current_action_type": 0x6E1C40},
        "v1.5.5": {"set_attack_state": "0x6DFE30", "weapon_equipped": "0x6E08C0",
                   "process_preload_queue": "0x4A1270", "import_stub": "0x9DC050",
                   "get_current_action_type": 0x6E2170, "thunk_target": "0x153D50"},
    }

    def _pins(self):
        """The installed client's row of PINS, or a skip."""
        import native_crash_triage as nct
        dll = Path(nct.DEFAULT_DLL)
        version = nd.engine_version(dll) if dll.is_file() else None
        if version not in self.PINS:
            self.skipTest(f"no address pins for client {version}; add its row to PINS at the engine bump")
        return self.PINS[version]

    def _cli(self, *args):
        r = subprocess.run([sys.executable, nd.__file__, *args], capture_output=True, timeout=3600)
        return r.returncode, r.stdout.decode("utf-8", errors="replace"), r.stderr.decode("utf-8", errors="replace")

    def test_an_engine_method_resolves_to_its_registered_implementation(self):
        entry = self._pins()["set_attack_state"]
        code, out, err = self._cli("--engine-method", "IMBAgent.SetAttackState")
        self.assertEqual(code, 0, out + err)
        self.assertIn(f"-> {entry}", out)
        self.assertIn(f"function: IMBAgent_SetAttackState  entry {entry}", out)

    def test_a_string_leads_to_the_function_the_id_map_predicts(self):
        # Two independent paths agree: the assert text the function holds, and its registration id.
        entry = self._pins()["weapon_equipped"]
        code, out, err = self._cli("--string", "IMono_MBAgent::weapon_equipped")
        self.assertEqual(code, 0, out + err)
        self.assertIn(f"--- IMBAgent_WeaponEquipped  entry {entry} ---", out)
        self.assertIn("engine method: IMBAgent.WeaponEquipped = weapon_equipped (MountAndBlade id 252)", out)

    def test_a_thunked_engine_method_shows_the_code_it_jumps_to(self):
        entry = self._pins()["process_preload_queue"]
        code, out, err = self._cli("--engine-method", "IPhysicsShape.ProcessPreloadQueue")
        self.assertEqual(code, 0, out + err)
        self.assertIn(f"function: IPhysicsShape_ProcessPreloadQueue  entry {entry}", out)
        # The thunk's target moved at v1.5.5; v1.5.3 and v1.5.4 share 0x153850.
        target = self._pins().get("thunk_target", "0x153850")
        self.assertIn(f"thunk to: FUN_18{target[2:].lower().zfill(7)}  entry {target}", out)

    def test_a_stub_that_jumps_to_an_import_does_not_crash(self):
        code, out, err = self._cli("--rva", self._pins()["import_stub"])
        self.assertEqual(code, 0, out + err)
        self.assertNotIn("thunk to:", out)

    def test_a_site_inside_a_registered_only_function_decompiles(self):
        # The function is reached only through the registration table: analysis alone found no
        # function there, so a crash inside it failed to decompile until the project was seeded.
        entry = self._pins()["get_current_action_type"]
        code, out, err = self._cli("--rva", hex(entry + 4))
        self.assertEqual(code, 0, out + err)
        self.assertIn(f"function: IMBAgent_GetCurrentActionType  entry 0x{entry:X}", out)

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
