#!/usr/bin/env python3
"""Decompile the native function at an RVA to pseudo-C, with headless Ghidra.

The decompiler step of /native-crash-triage. native_crash_triage.py names the crash site
(function bounds, strings, callers) from raw disassembly; this prints what that function
does, and its callers, as C. native_sig_author.py stays the tool for RTTI, vtables and
xrefs: find the builder with its `xref`, then decompile the builder here.

The binary is imported and auto-analysed once, into a Ghidra project outside the repo
(default E:\\ghidra\\TAOM). The project is named after the DLL's build folder and the
SHA-256 of its bytes, so a Steam in-place overwrite, or the wEditor build updating on its
own schedule, gets a fresh analysis instead of a stale one. Later runs open the saved
analysis read-only and take seconds. Two runs cannot share a project at once: Ghidra
locks it, and the second run says so.

Needs Ghidra 12.1+ at $GHIDRA_INSTALL_DIR and pyghidra. Ghidra's pyghidra pins a JPype
with no wheel for every Python, so it lives in its own venv ($TAOM_GHIDRA_PYTHON, default
E:\\Tools\\ghidra-venv); when the running Python cannot import pyghidra, the tool re-runs
itself under that interpreter. The setup is in docs/reference/development-machines.md.

Usage:
  python tools/native_decompile.py --rva 0x634396
  python tools/native_decompile.py --rva 0x634396 --callers 1
  python tools/native_decompile.py --rva 0x5FE0C9 --dll "<path-to-native-dll>"
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

from native_crash_triage import DEFAULT_DLL

INSTALL_ENV = "GHIDRA_INSTALL_DIR"
PYTHON_ENV = "TAOM_GHIDRA_PYTHON"
CHILD_ENV = "TAOM_GHIDRA_CHILD"  # set on the re-run, so a launcher without pyghidra cannot loop
DEFAULT_GHIDRA_PYTHON = r"E:\Tools\ghidra-venv\Scripts\python.exe"
SETUP_DOC = "docs/reference/development-machines.md"
DEFAULT_PROJECT_DIR = r"E:\ghidra\TAOM"
DECOMPILE_TIMEOUT_S = 120
MAX_CALLERS = 8  # per function per level; C for every caller of a hub function floods the terminal

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

    def __init__(self, dll, project_dir, key):
        self.dll = Path(dll)
        self.project_dir = Path(project_dir)
        self.key = key
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
        else:
            print(f"\nfirst run for this binary: importing and analysing (minutes, once) ...",
                  flush=True)
            started = time.monotonic()
            self.program = self.project.importProgram(File(str(self.dll)))
            if self.program is None:
                raise ToolError(f"Ghidra could not import {self.dll}")
            pyghidra.analyze(self.program)
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


def report(backend, rva, levels, out=print):
    """Print the function containing `rva` as C, then `levels` of callers as C."""
    fn = backend.function_at(rva)
    if fn is None:
        raise ToolError(f"RVA 0x{rva:X} is not inside any function Ghidra found; "
                        "check the base/offset math")
    off = rva - fn.entry_rva
    sign = "+" if off >= 0 else "-"
    out(f"\nfunction: {fn.name}  entry 0x{fn.entry_rva:X}  size 0x{fn.size:X}  "
        f"(rva at {sign}0x{abs(off):X})")
    out("")
    out(backend.decompile(fn).rstrip())

    seen = {fn.entry_rva}
    frontier = [fn]
    for level in range(1, levels + 1):
        nxt = []
        for target in frontier:
            calls = [c for c in backend.callers(target) if c.entry_rva not in seen]
            out(f"\nL{level} callers of 0x{target.entry_rva:X}: {len(calls)}")
            for c in calls[:MAX_CALLERS]:
                seen.add(c.entry_rva)
                out(f"\n--- {c.name}  entry 0x{c.entry_rva:X} ---")
                out(backend.decompile(c).rstrip())
                nxt.append(c)
            if len(calls) > MAX_CALLERS:
                out(f"\n... (+{len(calls) - MAX_CALLERS} more callers not decompiled)")
        frontier = nxt
        if not frontier:
            break


def main(argv=None, backend_factory=None):
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--rva", type=lambda x: int(x, 0), required=True,
                    help="RVA inside the function (e.g. an Event Log fault offset)")
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
        factory = backend_factory or GhidraBackend
        with factory(dll, project_dir, key) as backend:
            report(backend, args.rva, args.callers)
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
