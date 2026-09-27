#!/usr/bin/env python3
"""Decompile native engine code (TaleWorlds.Native.dll) to pseudo-C, with headless Ghidra.

Three ways in, one per question:
  --rva            a crash site: the decompiler step of /native-crash-triage, after
                   native_crash_triage.py has named the site
  --engine-method  research: what a managed `[EngineMethod]` call really does. The name
                   resolves through the engine's id registration (native_engine_methods.py)
  --string         research: which native code uses a string (an attribute the engine
                   parses, a source-file or assert text), decompiled
Every printed function that implements engine methods is labelled with each of them.
native_sig_author.py stays the tool for RTTI, vtables and xrefs.

The binary is imported and auto-analysed once, into a Ghidra project outside the repo
(default E:\\ghidra\\TAOM). The project is named after the DLL's build folder and the
SHA-256 of its bytes, so a Steam in-place overwrite, or the wEditor build updating on its
own schedule, gets a fresh analysis instead of a stale one. Later runs open the saved
analysis read-only and take seconds. Two runs cannot share a project at once: Ghidra
locks it, and the second run says so. The first run with a complete engine-method map also
seeds the project: a function at every registered engine-method implementation (nothing
calls them directly, so analysis misses many), named after its method when one method owns
the address, saved once. The applied seed version is a program option, so it dies with the
project and a newer version re-seeds.

Needs Ghidra 12.1+ at $GHIDRA_INSTALL_DIR and pyghidra. Ghidra's pyghidra pins a JPype
with no wheel for every Python, so it lives in its own venv ($TAOM_GHIDRA_PYTHON, default
E:\\Tools\\ghidra-venv); when the running Python cannot import pyghidra, the tool re-runs
itself under that interpreter. The setup is in docs/reference/development-machines.md.

Usage:
  python tools/native_decompile.py --rva 0x634396
  python tools/native_decompile.py --rva 0x634396 --callers 1
  python tools/native_decompile.py --rva 0x5FE0C9 --dll "<path-to-native-dll>"
  python tools/native_decompile.py --engine-method IMBAgent.GetCurrentActionType
  python tools/native_decompile.py --string monster_usage.cpp
"""
import argparse
import hashlib
import importlib.util
import os
import re
import subprocess
import sys
import time
import traceback
from collections import namedtuple
from pathlib import Path

import native_engine_methods as nem
from native_crash_triage import DEFAULT_DLL

INSTALL_ENV = "GHIDRA_INSTALL_DIR"
PYTHON_ENV = "TAOM_GHIDRA_PYTHON"
CHILD_ENV = "TAOM_GHIDRA_CHILD"  # set on the re-run, so a launcher without pyghidra cannot loop
DEFAULT_GHIDRA_PYTHON = r"E:\Tools\ghidra-venv\Scripts\python.exe"
SETUP_DOC = "docs/reference/development-machines.md"
DEFAULT_PROJECT_DIR = r"E:\ghidra\TAOM"
DECOMPILE_TIMEOUT_S = 120
MAX_CALLERS = 8  # per function per level; C for every caller of a hub function floods the terminal
MAX_STRINGS = 8  # strings containing the --string text; the rest are counted, not shown
SEED_CATEGORY, SEED_OPTION = "TAOM", "engine_method_seed"  # a program option, so it dies with the project
SEED_VERSION = 3  # 2: shared addresses are no longer named; 3: single-owner thunks are named too

Function = namedtuple("Function", "name entry_rva size")


class ToolError(Exception):
    """Bad input or a missing prerequisite: main() prints it and exits 2."""


def cache_key(dll):
    """`<build folder>-<sha256[:16]>`: one Ghidra project per distinct binary. The folder name
    is reduced to characters Ghidra allows in a project name (a crash-bundle copy can sit in
    a folder named `crash #635`)."""
    h = hashlib.sha256()
    with open(dll, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    folder = re.sub(r"[^A-Za-z0-9_-]", "_", Path(dll).absolute().parent.name)
    return f"{folder}-{h.hexdigest()[:16]}"


def engine_version(dll):
    """The Singleplayer version in the Version.xml beside the DLL, or None."""
    try:
        text = (Path(dll).parent / "Version.xml").read_text(encoding="utf-8-sig")
    except OSError:
        return None
    m = re.search(r'<Singleplayer\s+Value="([^"]+)"', text)
    return m.group(1) if m else None


def ghidra_python():
    """The interpreter that has pyghidra ($TAOM_GHIDRA_PYTHON, else the E: venv), or None."""
    value = os.environ.get(PYTHON_ENV, "").strip() or DEFAULT_GHIDRA_PYTHON
    return value if Path(value).is_file() else None


def _pyghidra_importable():
    try:
        return importlib.util.find_spec("pyghidra") is not None
    except (ValueError, AttributeError):  # a None entry in sys.modules
        return False


class GhidraBackend:
    """The real backend: the analysed program, opened for lookups and decompilation."""

    PROGRAM = "TaleWorlds.Native.dll"

    def __init__(self, dll, project_dir, key, engine_map=None):
        self.dll = Path(dll)
        self.project_dir = Path(project_dir)
        self.key = key
        self.engine_map = engine_map
        self.project = None
        self.program = None
        self.decompiler = None

    def __enter__(self):
        try:
            import pyghidra
        except ImportError:
            raise ToolError(
                f"pyghidra is not importable by {sys.executable}; see {SETUP_DOC}") from None
        pyghidra.start()  # reads $GHIDRA_INSTALL_DIR, which main() has checked
        try:
            self._open(pyghidra)
        except BaseException:
            self.__exit__(None, None, None)
            raise
        return self

    def _open(self, pyghidra):
        from ghidra.app.decompiler import DecompInterface
        from ghidra.base.project import GhidraProject
        from ghidra.framework.model import ProjectLocator
        from ghidra.framework.store import LockException
        from java.io import File

        location = str(self.project_dir)
        try:
            if ProjectLocator(location, self.key).exists():
                self.project = GhidraProject.openProject(location, self.key, True)
            else:
                self.project_dir.mkdir(parents=True, exist_ok=True)
                self.project = self._create(GhidraProject, location)
        except LockException:
            raise ToolError(f"Ghidra project {self.project_dir / self.key} is open in another "
                            "run; wait for it to finish") from None

        if self.project.getRootFolder().getFile(self.PROGRAM) is not None:
            self.program = self.project.openProgram("/", self.PROGRAM, True)
            applied = int(self.program.getOptions(SEED_CATEGORY).getInt(SEED_OPTION, 0))
            if should_seed(self.engine_map, applied):
                self.project.close(self.program)
                self.program = self.project.openProgram("/", self.PROGRAM, False)
                self._seed()
                self.project.save(self.program)
        else:
            print(f"\nfirst run for this binary: importing and analysing (minutes, once) ...",
                  flush=True)
            started = time.monotonic()
            self.program = self.project.importProgram(File(str(self.dll)))
            if self.program is None:
                raise ToolError(f"Ghidra could not import {self.dll}")
            pyghidra.analyze(self.program)
            if should_seed(self.engine_map, 0):
                self._seed()
            # Saved only after analysis completes, so a killed run leaves no half-analysed
            # program behind: the next run imports again.
            self.project.saveAs(self.program, "/", self.PROGRAM, True)
            print(f"analysed and saved in {time.monotonic() - started:.0f} s", flush=True)

        self.decompiler = DecompInterface()
        if not self.decompiler.openProgram(self.program):
            raise ToolError(f"the decompiler failed to open the program: "
                            f"{self.decompiler.getLastMessage()}")

    def _create(self, ghidra_project, location):
        # Fails on a .gpr left without its .rep (the .rep deleted to free disk), and when a
        # second first run races this one; either way the next run would fail identically.
        try:
            return ghidra_project.createProject(location, self.key, False)
        except Exception as e:  # JPype raises Java exceptions as Python ones
            raise ToolError(f"could not create Ghidra project {self.project_dir / self.key}: {e}. "
                            f"If no other run is analysing this binary, delete {self.key}.gpr and "
                            f"{self.key}.rep in {self.project_dir} and rerun") from None

    def _seed(self):
        """A function at every registered engine-method implementation, and a name on each one a
        single method owns. The registration table is their only reference, so analysis leaves
        many undiscovered (`get_current_action_type` at 0x6E19B0 on v1.5.3), and a crash inside
        one would otherwise not decompile. A shared address (a `return false` stub serves 35
        methods) keeps Ghidra's name: any one method's name would mislabel every caller."""
        from ghidra.app.cmd.function import CreateFunctionCmd
        from ghidra.program.model.symbol import SourceType
        from ghidra.util.task import TaskMonitor

        fm = self.program.getFunctionManager()
        names = seed_names(self.engine_map)
        created = named = 0
        tx = self.program.startTransaction("TAOM engine-method entry points")
        try:
            for rva in sorted({r.rva for r in self.engine_map.methods}):
                addr = self._address(rva)
                jfn = fm.getFunctionAt(addr)
                if jfn is None and fm.getFunctionContaining(addr) is None:
                    if CreateFunctionCmd(addr).applyTo(self.program, TaskMonitor.DUMMY):
                        created += 1
                    jfn = fm.getFunctionAt(addr)
                if jfn is not None and rva in names and str(jfn.getName()).startswith(("FUN_", "thunk_FUN_")):
                    try:
                        jfn.setName(names[rva], SourceType.ANALYSIS)
                        named += 1
                    except Exception:  # a name the program already uses elsewhere
                        pass
            self.program.getOptions(SEED_CATEGORY).setInt(SEED_OPTION, SEED_VERSION)
        finally:
            self.program.endTransaction(tx, True)
        print(f"engine-method entry points: {created} functions created, {named} named "
              "(once per binary)", flush=True)

    def __exit__(self, *exc):
        if self.decompiler is not None:
            self.decompiler.dispose()
        if self.project is not None:
            self.project.close()
        return False

    def _address(self, rva):
        return self.program.getImageBase().add(rva)

    def _function(self, jfn):
        base = self.program.getImageBase()
        return Function(str(jfn.getName(True)),
                        int(jfn.getEntryPoint().subtract(base)),
                        int(jfn.getBody().getNumAddresses()))

    def _java_function(self, fn):
        return self.program.getFunctionManager().getFunctionAt(self._address(fn.entry_rva))

    def function_at(self, rva):
        jfn = self.program.getFunctionManager().getFunctionContaining(self._address(rva))
        return None if jfn is None else self._function(jfn)

    def decompile(self, fn):
        from ghidra.util.task import TaskMonitor
        res = self.decompiler.decompileFunction(self._java_function(fn), DECOMPILE_TIMEOUT_S,
                                                TaskMonitor.DUMMY)
        if not res.decompileCompleted():
            return f"/* decompile failed: {res.getErrorMessage()} */"
        # CRLF from Ghidra would print as CR CR LF through Windows text-mode stdout.
        return str(res.getDecompiledFunction().getC()).replace("\r\n", "\n")

    def callers(self, fn):
        from ghidra.util.task import TaskMonitor
        found = self._java_function(fn).getCallingFunctions(TaskMonitor.DUMMY)
        return sorted((self._function(j) for j in found), key=lambda f: f.entry_rva)

    def thunk_target(self, fn):
        jfn = self._java_function(fn)
        if jfn is None or not jfn.isThunk():
            return None
        target = jfn.getThunkedFunction(True)
        if target is None or target.isExternal():  # an import: no code in this binary to decompile
            return None
        return self._function(target)

    def referencing_functions(self, rva):
        fm = self.program.getFunctionManager()
        found = {}
        for ref in self.program.getReferenceManager().getReferencesTo(self._address(rva)):
            jfn = fm.getFunctionContaining(ref.getFromAddress())
            if jfn is not None:
                fn = self._function(jfn)
                found[fn.entry_rva] = fn
        return [found[k] for k in sorted(found)]


def symbol_name(r):
    return f"{r.iface}_{r.method}" if r.iface and r.method else f"{r.assembly}_{r.engine_name}"


def seed_names(engine_map):
    """{rva: name} for the addresses exactly one engine method owns."""
    owners = {}
    for r in engine_map.methods:
        owners.setdefault(r.rva, []).append(r)
    return {rva: symbol_name(rs[0]) for rva, rs in owners.items() if len(rs) == 1}


def should_seed(engine_map, applied):
    """Seed only from a map with no problems (a partial sweep would name functions wrongly, and a
    seeded name is never replaced), and only when this version has not been applied yet."""
    return engine_map is not None and not engine_map.problems and applied < SEED_VERSION


def describe(r):
    who = f"{r.iface}.{r.method} = {r.engine_name}" if r.method else r.engine_name
    return f"{who} ({r.assembly} id {r.id})"


def _engine_lines(engine_map, entry_rva):
    return [f"engine method: {describe(r)}" for r in engine_map.by_rva(entry_rva)] if engine_map else []


def _printable(d, i, unit):
    return 32 <= d[i] < 127 and (unit == 1 or (i + 1 < len(d) and d[i + 1] == 0))


def find_strings(dll, text):
    """[(rva, whole string, encoding)] for every ASCII or UTF-16 string outside .text that
    contains `text`, found in the file bytes; Ghidra is asked only for the references."""
    import native_crash_triage as nct

    pe = nct.Pe(str(dll))
    t_lo = pe.text[1]
    t_hi = t_lo + max(pe.text[2], pe.text[4])
    found = {}
    for enc, needle, unit in (("ascii", text.encode("utf-8"), 1), ("utf-16", text.encode("utf-16-le"), 2)):
        pos = pe.d.find(needle)
        while pos != -1:
            start = pos
            while start - unit >= 0 and _printable(pe.d, start - unit, unit):
                start -= unit
            rva = pe.off_to_rva(start)
            if rva is not None and not (t_lo <= rva < t_hi) and rva not in found:
                chars, i = [], start
                while i + unit <= len(pe.d) and _printable(pe.d, i, unit) and len(chars) < 200:
                    chars.append(chr(pe.d[i]))
                    i += unit
                found[rva] = (rva, "".join(chars), enc)
            pos = pe.d.find(needle, pos + 1)
    return [found[k] for k in sorted(found)]


def report_strings(backend, found, out=print, engine_map=None):
    """Each string's referencing functions, as C."""
    for rva, text, enc in found[:MAX_STRINGS]:
        fns = backend.referencing_functions(rva)
        out(f"\nstring {text!r} at 0x{rva:X} ({enc}): {len(fns)} referencing function(s)")
        if not fns:
            out("  no code references (reached through a table or a data pointer, or unused)")
        for fn in fns[:MAX_CALLERS]:
            out(f"\n--- {fn.name}  entry 0x{fn.entry_rva:X} ---")
            for line in _engine_lines(engine_map, fn.entry_rva):
                out(line)
            out(backend.decompile(fn).rstrip())
        if len(fns) > MAX_CALLERS:
            out(f"\n... (+{len(fns) - MAX_CALLERS} more referencing functions not decompiled)")
    if len(found) > MAX_STRINGS:
        out(f"\n... (+{len(found) - MAX_STRINGS} more strings contain the text; narrow it)")


def report(backend, rva, levels, out=print, engine_map=None):
    """Print the function containing `rva` as C, then `levels` of callers as C."""
    fn = backend.function_at(rva)
    if fn is None:
        raise ToolError(f"RVA 0x{rva:X} is not inside any function Ghidra found; "
                        "check the base/offset math")
    off = rva - fn.entry_rva
    sign = "+" if off >= 0 else "-"
    out(f"\nfunction: {fn.name}  entry 0x{fn.entry_rva:X}  size 0x{fn.size:X}  "
        f"(rva at {sign}0x{abs(off):X})")
    for line in _engine_lines(engine_map, fn.entry_rva):
        out(line)
    out("")
    out(backend.decompile(fn).rstrip())
    thunk = backend.thunk_target(fn)
    if thunk is not None:  # the function is one jump; the code worth reading is its target
        out(f"\nthunk to: {thunk.name}  entry 0x{thunk.entry_rva:X}")
        for line in _engine_lines(engine_map, thunk.entry_rva):
            out(line)
        out(backend.decompile(thunk).rstrip())

    seen = {fn.entry_rva} | ({thunk.entry_rva} if thunk else set())
    frontier = [fn]
    for level in range(1, levels + 1):
        nxt = []
        for target in frontier:
            calls = [c for c in backend.callers(target) if c.entry_rva not in seen]
            out(f"\nL{level} callers of 0x{target.entry_rva:X}: {len(calls)}")
            for c in calls[:MAX_CALLERS]:
                seen.add(c.entry_rva)
                out(f"\n--- {c.name}  entry 0x{c.entry_rva:X} ---")
                for line in _engine_lines(engine_map, c.entry_rva):
                    out(line)
                out(backend.decompile(c).rstrip())
                nxt.append(c)
            if len(calls) > MAX_CALLERS:
                out(f"\n... (+{len(calls) - MAX_CALLERS} more callers not decompiled)")
        frontier = nxt
        if not frontier:
            break


def resolve_engine_method(engine_map, query):
    matches = engine_map.lookup(query)
    if not matches:
        hint = engine_map.suggest(query)
        raise ToolError(f"no engine method named {query!r}" +
                        (f"; did you mean: {', '.join(hint)}" if hint else ""))
    if len({m.rva for m in matches}) > 1:
        raise ToolError(f"{query!r} names {len(matches)} engine methods; qualify it with the "
                        f"interface: {', '.join(f'{m.iface}.{m.method}' for m in matches)}")
    return matches[0]


def main(argv=None, backend_factory=None, map_factory=None):
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--rva", type=lambda x: int(x, 0),
                      help="RVA inside the function (e.g. an Event Log fault offset)")
    mode.add_argument("--engine-method", metavar="NAME",
                      help="a managed [EngineMethod]: get_current_action_type, "
                           "IMBAgent.GetCurrentActionType, MBAgent.get_current_action_type")
    mode.add_argument("--string", metavar="TEXT",
                      help="decompile the native code that uses a string containing TEXT")
    ap.add_argument("--dll", default=DEFAULT_DLL,
                    help="module on disk (default: shipping TaleWorlds.Native.dll)")
    ap.add_argument("--callers", type=int, default=0,
                    help="caller levels to decompile as well (default 0)")
    ap.add_argument("--project-dir", default=DEFAULT_PROJECT_DIR,
                    help=f"where the analysed Ghidra projects live (default {DEFAULT_PROJECT_DIR})")
    argv = sys.argv[1:] if argv is None else list(argv)
    args = ap.parse_args(argv)

    try:
        dll = Path(args.dll)
        if not dll.is_file():
            raise ToolError(f"DLL not found: {dll}")
        if backend_factory is None:
            install = os.environ.get(INSTALL_ENV, "").strip()
            if not install or not Path(install).is_dir():
                raise ToolError(f"${INSTALL_ENV} is not set to a Ghidra install; see {SETUP_DOC}")
            if not _pyghidra_importable() and not os.environ.get(CHILD_ENV):
                python = ghidra_python()
                if python:
                    return subprocess.run([python, __file__, *argv],
                                          env={**os.environ, CHILD_ENV: "1"}).returncode
        key = cache_key(dll)
        version = engine_version(dll)
        project_dir = Path(args.project_dir).absolute()  # Ghidra refuses a relative location
        print(f"module: {dll} ({dll.stat().st_size:,} bytes)")
        print(f"engine: {version}" if version else
              "engine: unknown (no Version.xml beside the DLL)")
        print(f"ghidra project: {project_dir / key}", flush=True)
        try:
            engine_map = (map_factory or nem.load_or_build)(dll, project_dir, key)
            print(f"engine methods: {len(engine_map.methods)} mapped", flush=True)
            for problem in engine_map.problems:
                print(f"WARNING: engine-method map: {problem}")
        except nem.EngineMapError as e:
            engine_map, map_error = None, str(e)
            print(f"engine methods: unavailable ({map_error})", flush=True)
        except Exception as e:  # the map is optional for --rva and --string; never let it abort them
            engine_map, map_error = None, f"{type(e).__name__}: {e}"
            print(f"engine methods: unavailable ({map_error})", flush=True)

        rva, found = args.rva, None
        if args.engine_method is not None:
            if not args.engine_method.strip():
                raise ToolError("--engine-method is empty")
            if engine_map is None:
                raise ToolError(f"--engine-method needs the engine-method map: {map_error}")
            target = resolve_engine_method(engine_map, args.engine_method)
            if any(p.startswith(f"{target.assembly}:") for p in engine_map.problems):
                raise ToolError(f"the engine-method map is incomplete for {target.assembly} (the "
                                "WARNING lines above), so this answer is unverified")
            rva = target.rva
            print(f"\nresolved {args.engine_method!r} -> 0x{rva:X}", flush=True)
        elif args.string is not None:
            if not args.string.strip():
                raise ToolError("--string is empty")
            if args.callers:
                raise ToolError("--callers applies to --rva and --engine-method, not --string")
            found = find_strings(dll, args.string)
            if not found:
                raise ToolError(f"text {args.string!r} not found in {dll} (ASCII or UTF-16, "
                                "outside .text)")

        factory = backend_factory or GhidraBackend
        with factory(dll, project_dir, key, engine_map=engine_map) as backend:
            if found is not None:
                report_strings(backend, found, engine_map=engine_map)
            else:
                report(backend, rva, args.callers, engine_map=engine_map)
    except ToolError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 2
    return 0


def exit_process(code):
    """Exit with `code`, even with a Ghidra thread still alive. A failure part-way through
    starting Ghidra's OSGi layer leaves its non-daemon FelixDispatchQueue thread running, and
    the JVM then never lets the process end. The project is already closed by this point."""
    sys.stdout.flush()
    sys.stderr.flush()
    jpype = sys.modules.get("jpype")
    if jpype is not None and jpype.isJVMStarted():
        os._exit(code)
    else:
        sys.exit(code)


if __name__ == "__main__":
    try:
        exit_code = main()
    except Exception:
        traceback.print_exc()
        exit_code = 1
    exit_process(exit_code)
