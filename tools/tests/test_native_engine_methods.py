#!/usr/bin/env python3
"""Unit tests for native_engine_methods.py (managed [EngineMethod] -> native implementation).

Run:  python -m unittest tools.tests.test_native_engine_methods

No install needed. The registration code is built from the exact instruction bytes of the v1.5.3
client's three registration functions (0x62F200, 0x376DE0, 0x73D380): both call forms, both
orders of `mov rcx,rbx`, the `xor` id 0, and the two tail-call endings a byte pattern misses.
The opt-in integration test (TAOM_GHIDRA_IT=1, needs only the install) pins real ids.
"""
import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_engine_methods as nem  # noqa: E402

try:
    import capstone  # noqa: F401  (CI runs tools/tests with no pip install)
    HAVE_CAPSTONE = True
except ImportError:
    HAVE_CAPSTONE = False

LEA_RDX = b"\x48\x8D\x15"
LEA_R8 = b"\x4C\x8D\x05"
XOR_ECX = b"\x33\xC9"
MOV_RCX_RBX = b"\x48\x8B\xCB"
CALL_RBX_108 = b"\xFF\x93\x08\x01\x00\x00"
CALL_RBX_70 = b"\xFF\x53\x70"
TAIL_JMP_RAX = b"\x48\x8B\x83\x08\x01\x00\x00" + b"\x48\x83\xC4\x20" + b"\x5B" + b"\x48\xFF\xE0"
MOV_RCX_RDX = b"\x48\x8B\xCA"
CALL_R10 = b"\x41\xFF\xD2"


class Code:
    """Assembles a byte blob at `base` and remembers each lea's target."""

    def __init__(self, base=0x1000):
        self.base = base
        self.buf = bytearray()
        self.expected = []

    @property
    def here(self):
        return self.base + len(self.buf)

    def lea(self, opcode, disp):
        target = self.here + 7 + disp
        self.buf += opcode + struct.pack("<i", disp)
        return target

    def raw(self, b):
        self.buf += b

    def mov_ecx(self, v):
        self.buf += b"\xB9" + struct.pack("<I", v)

    def mov_edx(self, v):
        self.buf += b"\xBA" + struct.pack("<I", v)

    def call_rel(self):
        self.buf += b"\xE8" + struct.pack("<i", 0x100)

    def jmp_rel(self):
        self.buf += b"\xE9" + struct.pack("<i", 0x100)


def form_a(code, ident, disp=0x200, call=CALL_RBX_108):
    t = code.lea(LEA_RDX, disp)
    code.raw(XOR_ECX) if ident == 0 else code.mov_ecx(ident)
    code.raw(call)
    code.expected.append((ident, t))


@unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
class RegistrationSitesTests(unittest.TestCase):
    def test_form_a_with_xor_zero_and_mov_ids(self):
        c = Code()
        for i in range(3):
            form_a(c, i, disp=0x100 * (i + 1))
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), c.expected)

    def test_short_displacement_call(self):
        c = Code()
        form_a(c, 816, call=CALL_RBX_70)
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), c.expected)

    def test_form_b_in_both_orders(self):
        # 0x379DC9: lea r8; mov rcx,rbx; mov edx; call. 0x379DDD: lea r8; mov edx; mov rcx,rbx; call.
        c = Code()
        t = c.lea(LEA_R8, 0x300)
        c.raw(MOV_RCX_RBX)
        c.mov_edx(817)
        c.call_rel()
        c.expected.append((817, t))
        t = c.lea(LEA_R8, 0x400)
        c.mov_edx(818)
        c.raw(MOV_RCX_RBX)
        c.call_rel()
        c.expected.append((818, t))
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), c.expected)

    def test_tail_call_through_rax_is_counted(self):
        # The MountAndBlade function ends: lea rdx; mov ecx,688; mov rax,[rbx+0x108]; ...; jmp rax.
        c = Code()
        form_a(c, 687)
        t = c.lea(LEA_RDX, 0x500)
        c.mov_ecx(688)
        c.raw(TAIL_JMP_RAX)
        c.expected.append((688, t))
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), c.expected)

    def test_tail_jump_to_the_helper_is_counted(self):
        # The Engine function ends: lea r8; mov edx,1560; mov rcx,rbx; add rsp; pop rbx; jmp helper.
        c = Code()
        t = c.lea(LEA_R8, 0x600)
        c.mov_edx(1560)
        c.raw(MOV_RCX_RBX + b"\x48\x83\xC4\x20" + b"\x5B")
        c.jmp_rel()
        c.expected.append((1560, t))
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), c.expected)

    def test_a_clobbered_register_is_not_a_registration(self):
        # The DotNet head: lea rdx; mov rcx,rdx; call r10 registers nothing.
        c = Code()
        c.lea(LEA_RDX, 0x700)
        c.raw(MOV_RCX_RDX + CALL_R10)
        self.assertEqual(nem.registration_sites(bytes(c.buf), c.base), [])


class ToolAbsenceTests(unittest.TestCase):
    """A missing tool is an EngineMapError, which callers turn into 'unavailable', never a crash."""

    def test_no_capstone_is_a_map_error(self):
        with mock.patch.dict(sys.modules, {"capstone": None}):
            with self.assertRaises(nem.EngineMapError):
                nem.registration_sites(b"\x90", 0x1000)

    def test_an_ilspycmd_timeout_is_a_map_error(self):
        with mock.patch.object(nem.subprocess, "run", side_effect=subprocess.TimeoutExpired("ilspycmd", 300)):
            with self.assertRaises(nem.EngineMapError):
                nem.decompile_enum_owner(Path("x.dll"))


@unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
class ScanTests(unittest.TestCase):
    def test_only_functions_with_many_sites_are_returned(self):
        c = Code(base=0x1000)
        for i in range(nem.MIN_SITES):
            form_a(c, i)
        big_end = c.here
        for i in range(3):
            form_a(c, i)
        text = bytes(c.buf)
        pdata = [(0x1000, big_end), (big_end, c.here)]
        found = nem.scan_registrations(text, 0x1000, pdata)
        self.assertEqual(list(found), [0x1000])
        self.assertEqual(found[0x1000], dict(c.expected[:nem.MIN_SITES]))


MB_TEXT = """namespace ManagedCallbacks;
internal static class ScriptingInterfaceObjects
{
	private enum CoreInterfaceGeneratedEnum
	{
		enm_IMono_MBAgent_get_current_action_type = 69,
		enm_IMono_MBAgent_set_attack_state = 176,
		enm_IMono_MBMission_orphan = 177
	}
	public static void SetFunctionPointer(int id, IntPtr pointer)
	{
		switch ((CoreInterfaceGeneratedEnum)id)
		{
		case CoreInterfaceGeneratedEnum.enm_IMono_MBAgent_get_current_action_type:
			ScriptingInterfaceOfIMBAgent.call_GetCurrentActionTypeDelegate = (ScriptingInterfaceOfIMBAgent.GetCurrentActionTypeDelegate)Marshal.GetDelegateForFunctionPointer(pointer, typeof(ScriptingInterfaceOfIMBAgent.GetCurrentActionTypeDelegate));
			break;
		case CoreInterfaceGeneratedEnum.enm_IMono_MBAgent_set_attack_state:
			ScriptingInterfaceOfIMBAgent.call_SetAttackStateDelegate = (ScriptingInterfaceOfIMBAgent.SetAttackStateDelegate)Marshal.GetDelegateForFunctionPointer(pointer, typeof(ScriptingInterfaceOfIMBAgent.SetAttackStateDelegate));
			break;
		}
	}
}
You are not using the latest version of the tool, please update.
"""

ENGINE_TEXT = """namespace ManagedCallbacks;
internal static class ScriptingInterfaceObjects
{
	private enum EngineInterfaceGeneratedEnum
	{
		enm_IMono_AsyncTask_create_with_function,
		enm_IMono_AsyncTask_invoke,
		enm_IMono_Scene_get_name
	}
	public static void SetFunctionPointer(int id, IntPtr pointer)
	{
		switch ((EngineInterfaceGeneratedEnum)id)
		{
		case EngineInterfaceGeneratedEnum.enm_IMono_AsyncTask_create_with_function:
			ScriptingInterfaceOfIAsyncTask.call_CreateWithDelegateDelegate = (ScriptingInterfaceOfIAsyncTask.CreateWithDelegateDelegate)Marshal.GetDelegateForFunctionPointer(pointer, typeof(ScriptingInterfaceOfIAsyncTask.CreateWithDelegateDelegate));
			break;
		case EngineInterfaceGeneratedEnum.enm_IMono_AsyncTask_invoke:
			ScriptingInterfaceOfIAsyncTask.call_InvokeDelegate = (ScriptingInterfaceOfIAsyncTask.InvokeDelegate)Marshal.GetDelegateForFunctionPointer(pointer, typeof(ScriptingInterfaceOfIAsyncTask.InvokeDelegate));
			break;
		case EngineInterfaceGeneratedEnum.enm_IMono_Scene_get_name:
			ScriptingInterfaceOfIScene.call_GetNameDelegate = (ScriptingInterfaceOfIScene.GetNameDelegate)Marshal.GetDelegateForFunctionPointer(pointer, typeof(ScriptingInterfaceOfIScene.GetNameDelegate));
			break;
		}
	}
}
"""


DOTNET_TEXT = """namespace ManagedCallbacks;
internal static class ScriptingInterfaceObjects
{
	private enum LibraryInterfaceGeneratedEnum
	{
		enm_IMono_Managed_decrease_reference_count,
		enm_IMono_Managed_increase_reference_count
	}
	public static void SetFunctionPointer(int id, IntPtr pointer)
	{
		switch ((LibraryInterfaceGeneratedEnum)id)
		{
		case LibraryInterfaceGeneratedEnum.enm_IMono_Managed_decrease_reference_count:
			ScriptingInterfaceOfIManaged.call_DecreaseReferenceCountDelegate = null;
			break;
		case LibraryInterfaceGeneratedEnum.enm_IMono_Managed_increase_reference_count:
			ScriptingInterfaceOfIManaged.call_IncreaseReferenceCountDelegate = null;
			break;
		}
	}
}
"""


class ParseTests(unittest.TestCase):
    def test_explicit_values_and_the_switch_pairing(self):
        methods = {m.engine_name: m for m in nem.parse_generated_enum(MB_TEXT, "MountAndBlade")}
        m = methods["get_current_action_type"]
        self.assertEqual((m.assembly, m.id, m.iface, m.method), ("MountAndBlade", 69, "IMBAgent", "GetCurrentActionType"))
        self.assertEqual(methods["set_attack_state"].id, 176)

    def test_ordinal_values_count_from_zero(self):
        methods = nem.parse_generated_enum(ENGINE_TEXT, "Engine")
        self.assertEqual([(m.id, m.engine_name) for m in methods],
                         [(0, "create_with_function"), (1, "invoke"), (2, "get_name")])

    def test_the_csharp_name_comes_from_the_switch_not_from_the_engine_name(self):
        # create_with_function is CreateWithDelegate on the C# side: never derive one from the other.
        m = nem.parse_generated_enum(ENGINE_TEXT, "Engine")[0]
        self.assertEqual((m.iface, m.method), ("IAsyncTask", "CreateWithDelegate"))

    def test_a_member_without_a_switch_case_keeps_its_engine_name(self):
        methods = {m.id: m for m in nem.parse_generated_enum(MB_TEXT, "MountAndBlade")}
        self.assertEqual((methods[177].engine_name, methods[177].method), ("MBMission_orphan", None))

    def test_text_without_an_enum_is_an_error(self):
        with self.assertRaises(nem.EngineMapError):
            nem.parse_generated_enum("no enum here", "DotNet")


class AssignTests(unittest.TestCase):
    def _methods(self, assembly, ids):
        return [nem.EngineMethod(assembly, i, f"I{assembly}", f"M{i}", f"m_{i}") for i in ids]

    def test_each_assembly_takes_the_function_with_its_exact_id_set(self):
        methods = (self._methods("MountAndBlade", [0, 1, 3]) + self._methods("Engine", range(8))
                   + self._methods("DotNet", range(5)))
        functions = {0x100: {0: 0xA0, 1: 0xA1, 3: 0xA3},
                     0x200: {i: 0xB0 + i for i in range(5)},
                     0x300: {i: 0xC0 + i for i in range(8)}}
        resolved, problems = nem.assign(functions, methods)
        self.assertEqual(problems, [])
        by = {(r.assembly, r.id): r.rva for r in resolved}
        self.assertEqual(by[("MountAndBlade", 3)], 0xA3)
        self.assertEqual(by[("DotNet", 4)], 0xB4)

    def test_a_partial_sweep_never_loses_to_a_superset_function(self):
        # Engine's function holds every MountAndBlade id. Ranked by shared ids, one missed
        # MountAndBlade site handed MountAndBlade to Engine's function: ~1,400 wrong answers.
        methods = self._methods("MountAndBlade", [i for i in range(10) if i != 2]) + self._methods("Engine", range(21))
        functions = {0x100: {i: 0xA0 + i for i in range(10) if i not in (2, 5)},   # one site missed
                     0x200: {i: 0xB0 + i for i in range(21)}}
        resolved, problems = nem.assign(functions, methods)
        by = {(r.assembly, r.id): r.rva for r in resolved}
        self.assertEqual(by[("MountAndBlade", 3)], 0xA3)
        self.assertEqual(by[("Engine", 3)], 0xB3)
        self.assertTrue(any(p.startswith("MountAndBlade:") and "5" in p for p in problems), problems)

    def test_a_superset_function_never_stands_in_for_a_missing_one(self):
        # If MountAndBlade's own function is not found at a bump, Engine's (which holds every
        # MountAndBlade id) must not be taken for it: --rva would print wrong labels, exit 0.
        methods = self._methods("MountAndBlade", range(10)) + self._methods("Engine", range(21))
        resolved, problems = nem.assign({0x200: {i: 0xB0 + i for i in range(21)}}, methods)
        by = {(r.assembly, r.id): r.rva for r in resolved}
        self.assertNotIn(("MountAndBlade", 3), by)
        self.assertEqual(by[("Engine", 3)], 0xB3)
        self.assertTrue(any(p.startswith("MountAndBlade:") and "no native registration" in p for p in problems))

    def test_a_superset_cannot_stand_in_even_for_a_larger_assembly(self):
        # With MountAndBlade over half Engine's size, the half-share rule alone passes Engine's
        # function for it; exact pairs must claim their functions first.
        methods = self._methods("MountAndBlade", range(15)) + self._methods("Engine", range(21))
        resolved, problems = nem.assign({0x200: {i: 0xB0 + i for i in range(21)}}, methods)
        by = {(r.assembly, r.id): r.rva for r in resolved}
        self.assertNotIn(("MountAndBlade", 3), by)
        self.assertEqual(sum(1 for r in resolved if r.assembly == "Engine"), 21)
        self.assertTrue(any(p.startswith("MountAndBlade:") and "no native registration" in p for p in problems))

    def test_a_function_sharing_under_half_the_ids_is_no_match(self):
        methods = self._methods("DotNet", range(33))
        functions = {0x300: {16: 0xB2ACDC, 32: 0xAECA90, 64: 1, 128: 2}}
        resolved, problems = nem.assign(functions, methods)
        self.assertEqual(resolved, [])
        self.assertTrue(any(p.startswith("DotNet:") and "no native registration" in p for p in problems))

    def test_an_assembly_with_no_parsed_ids_is_a_problem(self):
        # A codegen rename at a bump parses to nothing; silence would read as a clean map.
        resolved, problems = nem.assign({0x100: {0: 0xA0}}, self._methods("MountAndBlade", [0]))
        self.assertTrue(any(p.startswith("Engine:") and "no managed ids" in p for p in problems), problems)
        self.assertTrue(any(p.startswith("DotNet:") for p in problems), problems)

    def test_a_managed_native_mismatch_is_reported_not_hidden(self):
        # After an engine bump the two halves can disagree; that is a finding in itself.
        methods = self._methods("DotNet", range(6))
        functions = {0x200: {i: 0xB0 + i for i in range(5)}}
        resolved, problems = nem.assign(functions, methods)
        self.assertEqual(len(resolved), 5)
        self.assertTrue(any("DotNet" in p and "5" in p for p in problems), problems)


class LookupTests(unittest.TestCase):
    def setUp(self):
        self.map = nem.EngineMethodMap([
            nem.Resolved("MountAndBlade", 69, "IMBAgent", "GetCurrentActionType", "get_current_action_type", 0x6E19B0),
            nem.Resolved("MountAndBlade", 5, "IMBMission", "GetName", "get_name", 0x5000),
            nem.Resolved("Engine", 2, "IScene", "GetName", "get_name", 0x6000),
        ])

    def test_every_spelling_finds_the_method(self):
        for q in ("get_current_action_type", "GetCurrentActionType", "IMBAgent.GetCurrentActionType",
                  "MBAgent.get_current_action_type", "MBAPI.IMBAgent.GetCurrentActionType",
                  "TaleWorlds.MountAndBlade.IMBAgent.GetCurrentActionType",
                  "enm_IMono_MBAgent_get_current_action_type",
                  "IMono_MBAgent::get_current_action_type"):
            with self.subTest(q=q):
                self.assertEqual([r.rva for r in self.map.lookup(q)], [0x6E19B0])

    def test_a_name_on_two_interfaces_returns_both(self):
        self.assertEqual(len(self.map.lookup("get_name")), 2)
        self.assertEqual([r.iface for r in self.map.lookup("IScene.GetName")], ["IScene"])

    def test_an_unknown_name_returns_nothing_and_suggests(self):
        self.assertEqual(self.map.lookup("get_current_action"), [])
        self.assertIn("IMBAgent.GetCurrentActionType (get_current_action_type)",
                      self.map.suggest("get_current_action"))

    def test_reverse_lookup_by_rva(self):
        self.assertEqual([r.engine_name for r in self.map.by_rva(0x6E19B0)], ["get_current_action_type"])
        self.assertEqual(self.map.by_rva(0x1234), [])


class BuildMapTests(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.bin = Path(self._dir.name) / "Win64_Shipping_Client"
        self.bin.mkdir()
        self.dll = self.bin / "TaleWorlds.Native.dll"
        self.dll.write_bytes(b"MZ native")
        for a in nem.ASSEMBLIES:
            (self.bin / f"TaleWorlds.{a}.AutoGenerated.dll").write_bytes(f"MZ {a}".encode())
        self.cache = Path(self._dir.name) / "cache"
        self.calls = {"decompile": 0, "scan": 0}

    def tearDown(self):
        self._dir.cleanup()

    FULL_SCAN = {0x100: {69: 0x6E19B0, 176: 0x6DF670, 177: 0x6DF700},
                 0x200: {0: 0xE0, 1: 0xE1, 2: 0xE2},
                 0x300: {0: 0xD0, 1: 0xD1}}

    def _decompile(self, managed_dll):
        self.calls["decompile"] += 1
        return {"MountAndBlade": MB_TEXT, "Engine": ENGINE_TEXT, "DotNet": DOTNET_TEXT}[
            managed_dll.name.split(".")[1]]

    def _scan(self, dll):
        self.calls["scan"] += 1
        return self.FULL_SCAN

    def _build(self, scan=None):
        return nem.load_or_build(self.dll, self.cache, "key1", decompile=self._decompile,
                                 scan=scan or self._scan)

    def test_the_fixture_map_is_clean(self):
        self.assertEqual(self._build().problems, [])

    def test_a_map_with_problems_is_not_cached(self):
        # Fixing the sweep must take effect at once; a cached partial map would keep refusing.
        def partial(_dll):
            self.calls["scan"] += 1
            return {**self.FULL_SCAN, 0x100: {69: 0x6E19B0, 177: 0x6DF700}}

        self.assertTrue(self._build(scan=partial).problems)
        fixed = self._build()
        self.assertEqual(self.calls["scan"], 2)
        self.assertEqual(fixed.problems, [])

    def test_a_cache_from_an_older_format_is_rebuilt(self):
        self._build()
        cache = self.cache / "key1.engine-methods.json"
        data = json.loads(cache.read_text(encoding="utf-8"))
        data["format"] = nem.FORMAT - 1
        cache.write_text(json.dumps(data), encoding="utf-8")
        self._build()
        self.assertEqual(self.calls["scan"], 2)

    def test_the_map_is_built_once_then_read_from_the_cache(self):
        first = self._build()
        self.assertEqual([r.rva for r in first.lookup("get_current_action_type")], [0x6E19B0])
        second = self._build()
        self.assertEqual(self.calls, {"decompile": 3, "scan": 1})
        self.assertEqual([r.rva for r in second.lookup("set_attack_state")], [0x6DF670])

    def test_a_cut_short_cache_is_rebuilt_not_raised(self):
        # A run killed mid-write must not leave every later run failing on the JSON.
        self._build()
        cache = self.cache / "key1.engine-methods.json"
        cache.write_text(cache.read_text(encoding="utf-8")[:40], encoding="utf-8")
        again = self._build()
        self.assertEqual(self.calls["scan"], 2)
        self.assertEqual([r.rva for r in again.lookup("get_current_action_type")], [0x6E19B0])

    def test_a_changed_managed_dll_rebuilds(self):
        self._build()
        (self.bin / "TaleWorlds.MountAndBlade.AutoGenerated.dll").write_bytes(b"MZ patched")
        self._build()
        self.assertEqual(self.calls["scan"], 2)

    def test_missing_managed_dlls_are_an_error_naming_the_folder(self):
        (self.bin / "TaleWorlds.Engine.AutoGenerated.dll").unlink()
        with self.assertRaises(nem.EngineMapError) as cm:
            self._build()
        self.assertIn("TaleWorlds.Engine.AutoGenerated.dll", str(cm.exception))

    def test_the_cache_file_is_json_keyed_by_the_native_key(self):
        self._build()
        data = json.loads((self.cache / "key1.engine-methods.json").read_text(encoding="utf-8"))
        self.assertEqual(data["native"], "key1")


@unittest.skipUnless(os.environ.get("TAOM_GHIDRA_IT") == "1", "integration: set TAOM_GHIDRA_IT=1")
class InstalledEngineTests(unittest.TestCase):
    """The installed client: every id read from the registration functions (the check that
    matters after an engine bump), then the v1.5.3 addresses, pinned only on that build."""

    def test_known_methods_resolve_to_their_registered_implementations(self):
        import native_crash_triage as nct
        import native_decompile as nd
        dll = Path(nct.DEFAULT_DLL)
        if not dll.is_file():
            self.skipTest(f"no native DLL at {dll}")
        with tempfile.TemporaryDirectory() as d:
            m = nem.load_or_build(dll, Path(d), "it")
        self.assertEqual(m.problems, [])
        # Client addresses per engine version; add a row at every engine bump.
        pins = {
            "v1.5.3": (("get_current_action_type", 0x6E19B0), ("set_attack_state", 0x6DF670),
                       ("set_scripted_target_entity", 0x6E2540)),
            "v1.5.4": (("get_current_action_type", 0x6E1C40), ("set_attack_state", 0x6DF900),
                       ("set_scripted_target_entity", 0x6E27D0)),
        }
        version = nd.engine_version(dll)
        if version not in pins:
            self.skipTest(f"no address pins for client {version}; add its row at the engine bump")
        for name, rva in pins[version]:
            with self.subTest(name=name):
                self.assertEqual([r.rva for r in m.lookup(f"MBAgent.{name}")], [rva])
        counts = {a: sum(1 for r in m.methods if r.assembly == a) for a in nem.ASSEMBLIES}
        self.assertEqual(counts, {"MountAndBlade": 688, "Engine": 1561, "DotNet": 33})


if __name__ == "__main__":
    unittest.main()
