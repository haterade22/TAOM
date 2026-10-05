#!/usr/bin/env python3
"""Turn a TAOM dev install (or an editor publish output) into a public release build.

The editor's Publish Module step emits packaged tpacs but copies RuntimeDataCache into
every module. RDC is an *editor-side* artifact: the entire RDC write surface exists only
in Win64_Shipping_wEditor\\TaleWorlds.Native.dll, which states outright "External .rdc
file modification detected. RDC files cannot be updated outside the editor." The shipping
client reads it (13,795 ReadFile ops across 5,036 distinct .rdc files, Procmon 2026-08-08)
but can never write it, and vanilla Modules/Native ships 1,188 tpacs with zero .rdc and
runs fine. Developers need the cache; players do not.

This tool never deletes from the source. It COPIES an allowed set into a fresh
destination, so the dev install keeps its RDC untouched.

Three verdicts per path:
  COPY      ships
  EXCLUDE   editor-only / dead / hazardous -- proven, dropped by default
  UNKNOWN   not on the include-list. Never copied, always reported, and (outside
            --dry-run) fails the run until a human passes --allow-unknown. A new
            editor artifact can therefore never ride along silently, and a needed
            folder can never vanish silently either.

CANDIDATES are a fourth state layered on COPY: plausibly droppable but unproven, so they
ship unless named explicitly. EmAssetPackages is the cautionary one -- the native-commit
audit called it "editor-mode packs", but vanilla Native ships 26.36 GB of it (measured
2026-08-10), so dropping it by default would have been a guess wearing a fact's clothes.

Usage:
  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release --dry-run
  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release \\
      --modules TAOM TAOM_Map LOTRLOME_Armory TAOM.Dependencies --allow-unknown
  python tools/package_release.py ... --exclude-candidate RACE_TEST --json manifest.json
  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release --require-build v2.0.31 --dry-run

Every run also reports, and never refuses over: what each shipped SceneObj/<scene> carries in
ShaderCache/D3D11 (terrain shader header, compressed shader cache sack, the sack's format), with
a WARNING for a sack whose format differs from the majority; every module-level
<module>/Shaders/D3D11 sack the planned modules hold (size and format); and whether the JIT
optimizes each shipped copy of TAOM.dll and TAOM.Dependencies.dll (OFF, ON, or unknown with its
reason when the file, or its DebuggableAttribute, cannot be read). TAOM ships Debug builds on
purpose, so the JIT line is a fact to read, not a gate.

Module-level sacks are left out of the copy by policy, not by proof. The maintainer's policy
(2026-10-03) is to ship TAOM's and the Armory's and never TAOM_Map's, but only after a test proves
the game uses a Kit-built sack, so for now every module's is dropped (rule MODULE_SHADER_SACK) and
the report says so in one policy line. Scene sacks and the other files in Shaders/D3D11
(shader_mapping.bin, shader_compile_report.log) still ship.

Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination / failed --require-build /
            retired binaries in the source (RETIRED_BINARIES, dry runs included).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import struct
import subprocess
import sys
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path, PurePosixPath

COPY = "copy"
EXCLUDE = "exclude"
UNKNOWN = "unknown"

# Default module set: the TAOM release modules that ship RDC, plus Dependencies.
# Alliance.Wargs was absorbed into LOTRLOME_Armory on 2026-08-28 and is no longer shipped
# as its own module; see docs/reference/lotrlome-warg-changes.md.
DEFAULT_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory", "TAOM.Dependencies")

# Top-level directories observed across TAOM's release set AND vanilla Native /
# SandBoxCore (enumerated 2026-08-10). Evidence, not memory: SceneEditData and SceneObj
# are on this list precisely because vanilla ships them.
KNOWN_TOP_DIRS = frozenset({
    "AssetPackages", "Assets", "Atmospheres", "bin", "EmAssetPackages", "GUI",
    "LauncherGUI", "ModuleData", "ModuleSounds", "MultiplayerForcedAvatars", "Music",
    "NavMeshPrefabs", "Prefabs", "SceneEditData", "SceneObj", "Shaders", "Sounds",
    "TileSets", "Videos",
})

KNOWN_TOP_FILES = frozenset({
    "SubModule.xml", "THIRD-PARTY-LICENSES.txt", "coop-modules.txt",
    "engine_module.ini", "splash.bmp", "steam.target",
})

# Written by the mod at runtime; shipping them leaks a dev machine's state.
RUNTIME_STATE_FILES = frozenset({"diag.log", "last-good-modlist.txt", "failed-mods-catalog.txt"})

# Matched by PREFIX, not exact name. The A/B tool leaves `RuntimeDataCache.OFF`, and a
# half-finished editor cook leaves things like `RuntimeDataCache.editor-partial-<date>`
# (observed 2026-08-10, when the editor regenerated 6 .rdc files then asserted on
# rglIntrusive_ptr.h:151 with the real cache renamed away). No cache variant ever ships,
# so treat every RuntimeDataCache* directory as cache rather than as an unknown that
# would block the run.
RDC_DIR_PREFIX = "RuntimeDataCache"
NATIVE_DEBUG_EXT = frozenset({".pdb", ".exp", ".lib"})

# Binaries an earlier build shipped and the repo has since deleted. Deploys never delete, so the
# install, its _wEditor/_Server mirrors and a channel folder built from it keep them; a release
# that carried MinHook would also ship it without the BSD-2 notice removed with it. The run
# refuses (dry runs included) until they are pruned. Compared case-insensitively by file name.
RETIRED_BINARIES = frozenset({
    "behaviortrees.dll", "behaviortreewrapper.dll",    # inlined into TAOM.dll 2026-05-24
    "taom.nativeskinfixes.dll", "minhook.x64.dll",     # NativeSkinFixes removed 2026-10-05
})

# Backup sidecars the tools under tools/ leave beside the live file (tools/README.md "XML I/O
# convention"). The suffix sits AFTER the real extension, and the dated, topic-tagged forms are
# the bulk of them: the 2026-09-14 pre-release sweep found 164 in the install and not one ended
# in a bare ".bak", so an exact ".xml.bak" rule shipped every one. This is the same expression
# as $SuffixRx in tools/sweep_module_backups.ps1; change both or neither. A suffix BEFORE the
# real extension (foo.bak.xml) is deliberately NOT matched: the engine globs *.xml and parses
# it, so dropping it here would hide a duplicate-id hazard the validator should catch.
BACKUP_SUFFIX_RE = re.compile(
    r"(?i)\.(?:bak(?:\d+|[-_][A-Za-z0-9._-]*)?|backup|orig|prev(?:\d+|[-_][A-Za-z0-9._-]*)?"
    r"|old|tmp|transplanted-\d+)$"
)

# The Modding Kit writes SceneObj/Backups/<scene> and SceneEditData/Backups/<scene> on every
# scene save (437 MB for Main_map on 2026-09-14). Both parents are KNOWN_TOP_DIRS because
# vanilla ships them, so the include-list alone would wave the backups through.
SCENE_BACKUP_PARENTS = frozenset({"SceneObj", "SceneEditData"})

# A scene's compiled shaders live in SceneObj/<scene>/ShaderCache/D3D11/: the terrain shader
# header and the compressed shader cache (the "sack"). Sacks have been problematic for players
# (the maintainer, 2026-10-02), shipping them is his call, and vanilla ships header-only scenes
# too, so the packager only reports what each scene carries. The sack's format is the dword at
# bytes 4 to 7 (every scene sack in the dev install and the patreon channel read 0x0783 on
# 2026-10-03). A sack in another format than its shipped siblings is flagged; a set of scene
# sacks that lags the engine as a whole is not. Module-level <Module>/Shaders/D3D11 sacks are never
# compared with them: issue #448's three lagging sacks were exactly those.
SHADER_HEADER = "terrain_shaders_header_data.bin"
SHADER_SACK = "compressed_shader_cache.sack"

# The same file name directly in <Module>/Shaders/D3D11/ is a module's own compiled shader cache,
# not a scene's. The maintainer's policy (2026-10-03): the target is to ship TAOM's and the
# Armory's and never TAOM_Map's, but not yet, only after a test proves the game uses a Kit-built
# sack. Until then classify leaves every module's out (MODULE_SHADER_SACK) and the report prints
# this line. No release channel carried one on 2026-10-03. The folder's other files,
# shader_mapping.bin and shader_compile_report.log, still ship: whether they should is an open
# question for the maintainer.
MODULE_SACK_POLICY = ("module shader sacks are left out: shipping TAOM's and the Armory's waits on "
                      "a test that the game uses a Kit-built sack; TAOM_Map's never ships")

CANDIDATE_RULES = ("EM_ASSET_PACKAGES", "RACE_TEST")

REPO_ROOT = Path(__file__).resolve().parent.parent

# The two assemblies TAOM compiles, by module (keyed casefolded: "--modules taom" resolves on
# Windows). Each carries the build stamp from Directory.Build.props. The build writes a copy into
# every bin/<platform>/ folder (Steam, Game Pass, dedicated server, Modding Kit) and every copy
# ships, so the gate reads them all; the Win64 client copy must exist.
SHIPPED_DLLS = {"taom": "TAOM.dll", "taom.dependencies": "TAOM.Dependencies.dll"}

# InformationalVersion as the assembly metadata stores it (UTF-8): build.<yyyyMMdd-HHmmss>Z, then
# '+' ('.' on older builds), then the commit SHA or "nogit", then an optional .dirty or .nogit flag.
# The one definition of the format: read_build_stamp finds it, check_build_stamp parses it.
STAMP_RE = re.compile(
    r"build\.\d{8}-\d{6}Z[+.](?P<rev>[0-9a-f]{40}|nogit)(?:\.(?P<flag>dirty|nogit))?", re.ASCII)

# The Directory.Build.props target that writes the .dirty flag. A commit without it stamps a bare
# SHA whatever the tree held, so a clean-looking stamp from such a commit proves nothing.
DIRTY_FLAG_TARGET = "TaomStampWorkingTreeState"

# The MethodRefSig (ECMA-335 II.23.2.2) of each DebuggableAttribute constructor as the compilers
# write it: HASTHIS, the parameter count, void, then (Boolean, Boolean), or the DebuggingModes enum,
# a value type (0x11) named by one compressed TypeDefOrRef token of one, two or four bytes
# (II.23.2). All 16,813 attributes in a survey of 18,490 DLLs on the dev machine on 2026-10-03 (the
# .NET runtimes and GAC, the NuGet caches and this repo's builds) used the enum form with a one or
# two byte token; none used the Boolean form.
_BOOLS_CTOR = re.compile(rb"\x20\x02\x01\x02\x02")
_MODES_CTOR = re.compile(rb"\x20\x01\x01\x11(?:[\x00-\x7f]|[\x80-\xbf].|[\xc0-\xdf]...)", re.DOTALL)

# ECMA-335 II.24.2.6: the tables a HasCustomAttribute coded index can name, in tag order.
# Assembly (0x20) is tag 14.
_HAS_CUSTOM_ATTRIBUTE = (0x06, 0x04, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x00, 0x0E, 0x17, 0x14, 0x11,
                         0x1A, 0x1B, 0x20, 0x23, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B)


class _NoMetadata(Exception):
    """The file is not a managed assembly this reader understands."""


def jit_optimization(dll: Path) -> tuple:
    """(disabled, why): disabled is True when the assembly's DebuggableAttribute turns JIT
    optimization off (a Debug build: the value's first byte has its low bit set and its second
    byte is not zero; the reader's comment gives how that rule was measured),
    False when it does not, None when the file holds no readable .NET metadata (a native DLL, a
    stand-in, a truncated file) or a DebuggableAttribute that is malformed (a constructor or a
    value that is not one of the layouts ECMA-335 gives it). `why` says what stopped the read for
    None, and for a False that the assembly carries no DebuggableAttribute at all; it is empty
    when the attribute is there and leaves optimization on. Reads the metadata tables directly
    (ECMA-335 II.24), so the assembly is never loaded."""
    try:
        return _read_jit_optimizer_disabled(Path(dll).read_bytes())
    except OSError as e:
        return None, f"cannot read: {e.strerror or e}"
    except _NoMetadata as e:
        return None, str(e)
    except (struct.error, IndexError, ValueError) as e:
        return None, f"malformed metadata: {type(e).__name__}: {e}"


def _read_jit_optimizer_disabled(d: bytes) -> tuple:
    def u16(o):
        return struct.unpack_from("<H", d, o)[0]

    def u32(o):
        return struct.unpack_from("<I", d, o)[0]

    if d[:2] != b"MZ":
        raise _NoMetadata("no MZ header")
    pe = u32(0x3C)
    if d[pe:pe + 4] != b"PE\0\0":
        raise _NoMetadata("no PE signature")
    nsec, opt_size, opt = u16(pe + 6), u16(pe + 20), pe + 24
    cli_rva = u32(opt + (112 if u16(opt) == 0x20B else 96) + 14 * 8)   # data directory 14
    if cli_rva == 0:
        raise _NoMetadata("no CLI header (a native DLL)")
    sections = [struct.unpack_from("<IIII", d, opt + opt_size + i * 40 + 8) for i in range(nsec)]

    def at(rva):
        for vsize, vaddr, rsize, roff in sections:
            if vaddr <= rva < vaddr + max(vsize, rsize):
                return roff + rva - vaddr
        raise _NoMetadata(f"RVA {rva:#x} is in no section")

    root = at(u32(at(cli_rva) + 8))
    if u32(root) != 0x424A5342:                                        # "BSJB"
        raise _NoMetadata("no metadata signature")
    p = root + 16 + u32(root + 12)                                     # past the version string
    count, p, streams = u16(p + 2), p + 4, {}
    for _ in range(count):
        end = d.index(b"\0", p + 8)
        streams[d[p + 8:end].decode("ascii")] = root + u32(p)
        p = (end + 4) & ~3
    tables = streams.get("#~", streams.get("#-"))
    if tables is None or "#Strings" not in streams or "#Blob" not in streams:
        raise _NoMetadata("no table, string or blob stream")
    heap_sizes = d[tables + 6]
    valid = struct.unpack_from("<Q", d, tables + 8)[0]
    rows, p = {}, tables + 24
    for t in range(64):
        if valid >> t & 1:
            rows[t], p = u32(p), p + 4
    str_w, guid_w, blob_w = (4 if heap_sizes & bit else 2 for bit in (1, 2, 4))

    def index_w(t):
        return 2 if rows.get(t, 0) < 1 << 16 else 4

    def coded_w(tag_bits, targets):
        return 2 if max(rows.get(t, 0) for t in targets) < 1 << (16 - tag_bits) else 4

    scope_w = coded_w(2, (0x00, 0x1A, 0x23, 0x01))          # ResolutionScope
    tdor_w = coded_w(2, (0x02, 0x01, 0x1B))                 # TypeDefOrRef
    parent_w = coded_w(3, (0x02, 0x01, 0x1A, 0x06, 0x1B))   # MemberRefParent
    const_w = coded_w(2, (0x04, 0x08, 0x17))                # HasConstant
    hca_w = coded_w(5, _HAS_CUSTOM_ATTRIBUTE)
    cat_w = coded_w(3, (0x06, 0x0A))                        # CustomAttributeType
    row_w = [
        2 + str_w + 3 * guid_w,                             # 0x00 Module
        scope_w + 2 * str_w,                                # 0x01 TypeRef
        4 + 2 * str_w + tdor_w + index_w(0x04) + index_w(0x06),  # 0x02 TypeDef
        index_w(0x04),                                      # 0x03 FieldPtr
        2 + str_w + blob_w,                                 # 0x04 Field
        index_w(0x06),                                      # 0x05 MethodPtr
        8 + str_w + blob_w + index_w(0x08),                 # 0x06 MethodDef
        index_w(0x08),                                      # 0x07 ParamPtr
        4 + str_w,                                          # 0x08 Param
        index_w(0x02) + tdor_w,                             # 0x09 InterfaceImpl
        parent_w + str_w + blob_w,                          # 0x0A MemberRef
        2 + const_w + blob_w,                               # 0x0B Constant
        hca_w + cat_w + blob_w,                             # 0x0C CustomAttribute
    ]
    start, p = [], tables + 24 + 4 * len(rows)
    for t, w in enumerate(row_w):
        start.append(p)
        p += w * rows.get(t, 0)

    def read(o, w):
        return u16(o) if w == 2 else u32(o)

    def string(i):
        o = streams["#Strings"] + i
        return d[o:d.index(b"\0", o)].decode("utf-8", "replace")

    def blob(i):
        o = streams["#Blob"] + i
        if d[o] & 0x80 == 0:
            return d[o + 1:o + 1 + d[o]]
        if d[o] & 0xC0 == 0x80:
            return d[o + 2:o + 2 + (((d[o] & 0x3F) << 8) | d[o + 1])]
        n = ((d[o] & 0x1F) << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]
        return d[o + 4:o + 4 + n]

    for r in range(rows.get(0x0C, 0)):
        o = start[0x0C] + r * row_w[0x0C]
        parent, ctor = read(o, hca_w), read(o + hca_w, cat_w)
        if parent & 0x1F != 14 or ctor & 0x7 != 3:          # on the Assembly, via a MemberRef
            continue
        member = start[0x0A] + ((ctor >> 3) - 1) * row_w[0x0A]
        cls = read(member, parent_w)
        if cls & 0x7 != 1:                                  # declared on a TypeRef
            continue
        typeref = start[0x01] + ((cls >> 3) - 1) * row_w[0x01]
        namespace = string(read(typeref + scope_w + str_w, str_w))
        name = string(read(typeref + scope_w, str_w))
        if (namespace, name) != ("System.Diagnostics", "DebuggableAttribute"):
            continue
        sig = blob(read(member + parent_w + str_w, blob_w))   # the constructor's MethodRefSig
        value = blob(read(o + hca_w + cat_w, blob_w))
        return _debuggable_optimizer_disabled(sig, value), ""
    return False, "no DebuggableAttribute on the assembly"


def _debuggable_optimizer_disabled(sig: bytes, value: bytes) -> bool:
    """Whether the JIT generates unoptimized code for an assembly with this DebuggableAttribute.
    `sig` is the MethodRefSig of the constructor it names and `value` its CustomAttrib blob
    (ECMA-335 II.23.3): a prolog 01 00, the constructor's arguments, then NumNamed. The attribute
    has no settable member, so NumNamed is 00 00. The constructor fixes the layout, so a
    constructor this does not know, or a value that is not exactly that layout, is malformed:
    _NoMetadata, never a verdict."""
    if _BOOLS_CTOR.fullmatch(sig):
        args = 2        # isJITTrackingEnabled, isJITOptimizerDisabled
    elif _MODES_CTOR.fullmatch(sig):
        args = 4        # DebuggingModes, an int32
    else:
        raise _NoMetadata(f"unsupported DebuggableAttribute constructor {sig.hex()}")
    if len(value) != 4 + args or value[:2] != b"\x01\x00" or value[-2:] != b"\x00\x00":
        raise _NoMetadata(f"unexpected DebuggableAttribute value {value.hex()}")
    # The runtime's behaviour, measured, fits a rule on the first two bytes after the prolog,
    # whichever constructor wrote them: the two Booleans, or the low two bytes of the
    # DebuggingModes int32 (Default is bit 0 of the first, DisableOptimizations bit 0 of the
    # second). The JIT generates unoptimized code only when the first has its low bit set and the
    # second is not zero, so DisableOptimizations without Default (0x100) still leaves it
    # optimizing. Measured on .NET Framework 4.8.1 (clr.dll 4.8.9345.0, 64-bit) on 2026-10-04: an
    # assembly per value, then whether the JIT inlined a small callee and let a dead local be
    # collected; 1,304 DebuggingModes values and 30 pairs of Boolean bytes all fit. The 4.7.2
    # reference documentation (mscorlib.xml) says to combine DisableOptimizations with Default
    # and does not say what it does alone; no other 4.x build was run. A Debug build stamps 0x107
    # (Default | IgnoreSymbolStoreSequencePoints | EnableEditAndContinue | DisableOptimizations)
    # and a Release build 0x2, both read off real TAOM builds on 2026-10-02, and both fit.
    tracking, optimizer_disabled = value[2], value[3]
    return bool(tracking & 1 and optimizer_disabled)


def read_build_stamp(dll: Path) -> str | None:
    """The one build stamp in a compiled TAOM assembly; None when there is none or several."""
    found = {m.group(0) for m in STAMP_RE.finditer(dll.read_bytes().decode("latin-1"))}
    return found.pop() if len(found) == 1 else None


def check_build_stamp(stamp: str | None, expected_sha: str) -> str | None:
    """None when the stamp proves a clean build of expected_sha, otherwise why it does not."""
    if stamp is None:
        return "no single build stamp in the DLL (a pre-2026-08-01 build, or not a TAOM assembly)"
    m = STAMP_RE.fullmatch(stamp)
    if m is None:
        return f"unrecognised build stamp '{stamp}'"
    if m["rev"] == "nogit" or m["flag"] == "nogit":
        return f"built where git could not report the tree ({stamp})"
    if m["flag"] == "dirty":
        return f"built from a working tree with uncommitted changes ({stamp})"
    if m["rev"] != expected_sha.lower():
        return f"built at {m['rev'][:12]}, but the release is {expected_sha[:12]} ({stamp})"
    return None


def _git(*args: str):
    try:
        return subprocess.run(["git", *args], cwd=REPO_ROOT, capture_output=True, text=True)
    except OSError:
        return None


def resolve_commit(rev: str) -> str | None:
    """The full SHA of the commit `rev` names in this repository, or None."""
    r = _git("rev-parse", "--verify", "--quiet", f"{rev}^{{commit}}")
    sha = r.stdout.strip() if r else ""
    return sha if r and r.returncode == 0 and sha else None


def stamps_dirty_trees(sha: str) -> bool:
    """True when the commit's Directory.Build.props carries the .dirty-flag target."""
    r = _git("show", f"{sha}:Directory.Build.props")
    return bool(r) and r.returncode == 0 and DIRTY_FLAG_TARGET in r.stdout


def shipped_dll_copies(plan) -> list:
    """Module-relative path of every bin/<platform>/ copy of the TAOM assembly this module ships
    (none for a module outside SHIPPED_DLLS)."""
    dll_name = SHIPPED_DLLS.get(plan.name.casefold())
    if dll_name is None:
        return []
    return [rel for rel, _size in plan._copy_list
            if rel.casefold().startswith("bin/")
            and PurePosixPath(rel).name.casefold() == dll_name.casefold()]


def retired_binaries(plans) -> list:
    """<module>/<rel> of every RETIRED_BINARIES file under a planned module's bin/."""
    return [f"{p.name}/{rel}" for p in plans for rel, _size in p._copy_list
            if rel.split("/", 1)[0] == "bin"
            and PurePosixPath(rel).name.casefold() in RETIRED_BINARIES]


@dataclass(frozen=True)
class SceneShaderCache:
    """What one shipped scene carries in SceneObj/<scene>/ShaderCache/D3D11/. sack_format is the
    sack's format dword, None without a sack or when it cannot be read (then `note` says why)."""
    module: str
    scene: str
    header: bool
    sack: bool
    sack_format: int | None = None
    note: str = ""

    @property
    def where(self) -> str:
        return f"{self.module}/SceneObj/{self.scene}"


def read_sack_format(sack: Path) -> tuple:
    """(format, why): the dword at bytes 4 to 7 of a compressed shader cache, or None and the
    reason it could not be read."""
    try:
        with open(sack, "rb") as f:
            head = f.read(8)
    except OSError as e:
        return None, f"cannot read: {e.strerror or e}"
    if len(head) < 8:
        return None, f"{len(head)} bytes, shorter than the 8-byte prefix that holds the format"
    return struct.unpack_from("<I", head, 4)[0], ""


def scene_shader_caches(plan) -> list:
    """One SceneShaderCache per scene folder this module ships under SceneObj/, sorted by name,
    whether or not it has a ShaderCache. Reads the copy list, so an excluded path
    (SceneObj/Backups) is never judged; a file directly under SceneObj/ is not a scene."""
    scenes = {}
    for rel, _size in plan._copy_list:
        parts = PurePosixPath(rel).parts
        if len(parts) < 3 or parts[0].casefold() != "sceneobj":
            continue
        files = scenes.setdefault(parts[1], {})
        if (len(parts) == 5 and parts[2].casefold() == "shadercache"
                and parts[3].casefold() == "d3d11"):
            files[parts[4].casefold()] = rel
    caches = []
    for scene in sorted(scenes):
        files = scenes[scene]
        sack_rel = files.get(SHADER_SACK)
        fmt, note = read_sack_format(plan.root / sack_rel) if sack_rel else (None, "")
        caches.append(SceneShaderCache(plan.name, scene, SHADER_HEADER in files,
                                       sack_rel is not None, fmt, note))
    return caches


def stale_sack_formats(caches) -> tuple:
    """(majority, odd): the most common readable sack format (the newer one on a tie), and every
    cache whose sack reads a different format. (None, []) when no sack format was readable."""
    counts = Counter(c.sack_format for c in caches if c.sack_format is not None)
    if not counts:
        return None, []
    majority = max(counts, key=lambda f: (counts[f], f))
    return majority, [c for c in caches
                      if c.sack_format is not None and c.sack_format != majority]


def require_build(plans, rev: str, requested=()) -> tuple:
    """(problems, checked): every reason the planned TAOM assemblies are not a clean build of
    `rev` (empty: all good), and the module-relative path of every DLL copy read."""
    sha = resolve_commit(rev)
    if sha is None:
        return [f"cannot resolve '{rev}' to a commit in {REPO_ROOT}"], []
    if not stamps_dirty_trees(sha):
        return [f"{sha[:12]} predates the {DIRTY_FLAG_TARGET} target, so a DLL built there "
                "cannot prove its tree was clean"], []
    problems, checked = [], []
    planned = {p.name.casefold() for p in plans}
    for name in requested:
        if name.casefold() in SHIPPED_DLLS and name.casefold() not in planned:
            problems.append(f"{name}: requested but not present in the source")
    for p in plans:
        dll_name = SHIPPED_DLLS.get(p.name.casefold())
        if dll_name is None:
            continue
        copies = shipped_dll_copies(p)
        client = f"bin/Win64_Shipping_Client/{dll_name}"
        if client.casefold() not in {rel.casefold() for rel in copies}:
            problems.append(f"{p.name}: {client} is missing")
        for rel in copies:
            checked.append(f"{p.name}/{rel}")
            try:
                stamp = read_build_stamp(p.root / rel)
            except OSError as e:
                problems.append(f"{p.name}/{rel}: cannot read ({e})")
                continue
            reason = check_build_stamp(stamp, sha)
            if reason:
                problems.append(f"{p.name}/{rel}: {reason}")
    if not checked and not problems:
        problems.append("neither TAOM nor TAOM.Dependencies is in the module set; nothing to verify")
    return problems, checked


@dataclass(frozen=True)
class Decision:
    action: str
    rule: str = ""
    reason: str = ""
    candidate: bool = False


def classify(rel: str, *, keep_rdc: bool = False, exclude_candidates=()) -> Decision:
    """Classify one module-relative path. `rel` uses POSIX separators."""
    p = PurePosixPath(rel)
    parts = p.parts
    if not parts:
        return Decision(UNKNOWN, reason="empty path")
    top = parts[0]
    name = p.name
    cands = set(exclude_candidates or ())

    # --- RuntimeDataCache -----------------------------------------------------
    if top.startswith(RDC_DIR_PREFIX):
        if name.endswith(".rtemp"):
            return Decision(EXCLUDE, "RDC_RTEMP", "zero-byte editor leftover")
        if top != RDC_DIR_PREFIX:
            # A suffixed variant: .OFF from the A/B tool, or an editor partial cook.
            # Never ships, and never counts as the real cache under --keep-rdc.
            return Decision(EXCLUDE, "RDC_STRAY_FOLDER", f"stray cache folder '{top}'")
        if keep_rdc:
            return Decision(COPY, "RUNTIME_DATA_CACHE", "kept by --keep-rdc")
        return Decision(EXCLUDE, "RUNTIME_DATA_CACHE", "editor-generated; client cannot write it")

    # --- confident exclusions -------------------------------------------------
    if top == "AssetSources":
        return Decision(EXCLUDE, "ASSET_SOURCES", "editor-only, never loaded at runtime")
    if top == "Prefabs_Unused":
        return Decision(EXCLUDE, "PREFABS_UNUSED", "unreferenced prefab scratch")
    if BACKUP_SUFFIX_RE.search(name):
        return Decision(EXCLUDE, "BACKUP_SIDECAR", "tool backup beside the live file; .bak breaks the upload")
    if top in SCENE_BACKUP_PARENTS and len(parts) >= 2 and parts[1] == "Backups":
        return Decision(EXCLUDE, "SCENE_BACKUPS", "Modding Kit scene backups, regenerated on every save")
    if top == "bin" and p.suffix.lower() in NATIVE_DEBUG_EXT:
        return Decision(EXCLUDE, "NATIVE_DEBUG", "debug/link artifact")
    if len(parts) == 1 and name in RUNTIME_STATE_FILES:
        return Decision(EXCLUDE, "RUNTIME_STATE", "generated at runtime on a dev machine")
    # NOTE: project.mbproj must NEVER be excluded. It looks like an editor-only file and
    # is not, which is exactly the trap: the SHIPPING runtime calls
    # XmlResource.GetMbprojxmls(module.Id) for every module (TaleWorlds.MountAndBlade,
    # Module.LoadSubModules), and that reads ModuleData/project.mbproj's <file> nodes and
    # registers them as native resources. It is a DISJOINT loader from SubModule.xml's
    # <XmlName> glob, so a resource registered there appears nowhere else. TAOM's mbproj
    # is the only registration for the four voice-definition XMLs and module_sounds.xml;
    # LOTRLOME_Armory's is the only registration for its monsters and action sets, whose
    # absence is a documented native spawn CTD. Dropping it from the zip breaks a release
    # while leaving the repo and every test green.
    #
    # Visual Studio workspace state. It lands under a KNOWN_TOP_DIR (GUI/), so the
    # include-list below waves it straight through, and its contents are absolute paths
    # naming the maintainer's drive layout and whichever module folder the GUI was
    # authored in. Deployment is additive, so a stale one survives long after the repo
    # copy is gone -- exclude by path part rather than by name.
    if ".vs" in parts:
        return Decision(EXCLUDE, "VS_IDE_STATE", "IDE workspace state; leaks absolute dev paths")

    # --- policy exclusion: the maintainer's call, not a proof -------------------
    # A module's own compiled shader cache sits directly in Shaders/D3D11/, whatever the module
    # (MODULE_SACK_POLICY). A scene's sack is under SceneObj/<scene>/ShaderCache/D3D11/ and ships,
    # as does every other file in this folder. Names match whatever their case, as on Windows.
    if (len(parts) == 3 and parts[0].casefold() == "shaders" and parts[1].casefold() == "d3d11"
            and name.casefold() == SHADER_SACK):
        return Decision(EXCLUDE, "MODULE_SHADER_SACK",
                        "left out until a test shows the game uses a Kit-built sack")

    # --- candidates: ship unless explicitly named ------------------------------
    if top == "EmAssetPackages":
        if "EM_ASSET_PACKAGES" in cands:
            return Decision(EXCLUDE, "EM_ASSET_PACKAGES", "excluded on request")
        return Decision(COPY, "EM_ASSET_PACKAGES",
                        "vanilla Native ships 26.36 GB of these -- unproven as editor-only",
                        candidate=True)
    if len(parts) >= 2 and top == "Assets" and parts[1] == "Race Test":
        if "RACE_TEST" in cands:
            return Decision(EXCLUDE, "RACE_TEST", "excluded on request")
        return Decision(COPY, "RACE_TEST", "scratch-looking, but referenced-ness unverified",
                        candidate=True)

    # --- include-list ---------------------------------------------------------
    if len(parts) == 1:
        return (Decision(COPY) if name in KNOWN_TOP_FILES
                else Decision(UNKNOWN, reason="unrecognised top-level file"))
    if top in KNOWN_TOP_DIRS:
        return Decision(COPY)
    return Decision(UNKNOWN, reason="unrecognised top-level directory")


@dataclass
class ModulePlan:
    name: str
    root: Path
    copy_bytes: int = 0
    exclude_bytes: int = 0
    unknown_bytes: int = 0
    copy_files: int = 0
    by_rule: dict = field(default_factory=dict)
    unknown: list = field(default_factory=list)
    candidates_shipped: dict = field(default_factory=dict)
    # (module-relative path, size) of each module-level shader sack left out of the copy
    module_sacks: list = field(default_factory=list)
    _copy_list: list = field(default_factory=list, repr=False)

    @property
    def total_bytes(self) -> int:
        return self.copy_bytes + self.exclude_bytes + self.unknown_bytes


def plan_module(module_root: Path, *, keep_rdc: bool = False, exclude_candidates=()) -> ModulePlan:
    """Walk one module and decide every file. No I/O beyond stat()."""
    plan = ModulePlan(name=module_root.name, root=module_root)
    for dirpath, _dirnames, filenames in os.walk(module_root):
        for fn in filenames:
            full = Path(dirpath) / fn
            rel = full.relative_to(module_root).as_posix()
            try:
                size = full.stat().st_size
            except OSError:
                size = 0
            d = classify(rel, keep_rdc=keep_rdc, exclude_candidates=exclude_candidates)
            if d.action == COPY:
                plan.copy_bytes += size
                plan.copy_files += 1
                plan._copy_list.append((rel, size))
                if d.candidate:
                    plan.candidates_shipped[d.rule] = plan.candidates_shipped.get(d.rule, 0) + size
            elif d.action == EXCLUDE:
                plan.exclude_bytes += size
                plan.by_rule[d.rule] = plan.by_rule.get(d.rule, 0) + size
                if d.rule == "MODULE_SHADER_SACK":
                    plan.module_sacks.append((rel, size))
            else:
                plan.unknown_bytes += size
                if len(plan.unknown) < 200:
                    plan.unknown.append(rel)
    return plan


def execute(plan: ModulePlan, dest_root: Path) -> int:
    """Copy the planned set. Returns files copied."""
    n = 0
    for rel, _size in plan._copy_list:
        src = plan.root / rel
        dst = dest_root / plan.name / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        n += 1
        if n % 500 == 0:
            print(f"    {plan.name}: {n}/{plan.copy_files} files", flush=True)
    return n


def _gb(n: int) -> float:
    return round(n / (1024 ** 3), 2)


def _report(plans, args) -> None:
    print()
    print(f"{'module':<20}{'ship GB':>10}{'dropped GB':>12}{'unknown GB':>12}{'files':>10}")
    print("-" * 64)
    for p in plans:
        print(f"{p.name:<20}{_gb(p.copy_bytes):>10}{_gb(p.exclude_bytes):>12}"
              f"{_gb(p.unknown_bytes):>12}{p.copy_files:>10}")
    tc = sum(p.copy_bytes for p in plans)
    te = sum(p.exclude_bytes for p in plans)
    tu = sum(p.unknown_bytes for p in plans)
    print("-" * 64)
    print(f"{'TOTAL':<20}{_gb(tc):>10}{_gb(te):>12}{_gb(tu):>12}"
          f"{sum(p.copy_files for p in plans):>10}")
    print(f"\nsource {_gb(tc + te + tu)} GB  ->  release {_gb(tc)} GB   "
          f"({_gb(te)} GB dropped)")

    by_rule = {}
    for p in plans:
        for k, v in p.by_rule.items():
            by_rule[k] = by_rule.get(k, 0) + v
    if by_rule:
        print("\ndropped, by rule:")
        for k, v in sorted(by_rule.items(), key=lambda kv: -kv[1]):
            print(f"  {k:<24}{_gb(v):>10} GB")

    shipped = {}
    for p in plans:
        for k, v in p.candidates_shipped.items():
            shipped[k] = shipped.get(k, 0) + v
    if shipped:
        print("\nCANDIDATES SHIPPED (unproven; drop with --exclude-candidate NAME):")
        for k, v in sorted(shipped.items(), key=lambda kv: -kv[1]):
            print(f"  {k:<24}{_gb(v):>10} GB")

    if args.keep_rdc:
        print("\nNOTE: --keep-rdc is set, so RuntimeDataCache ships. Drop it once the")
        print("      Phase 1 A/B clears it (tools/Invoke-RdcAbTest.ps1).")
    elif by_rule.get("RUNTIME_DATA_CACHE"):
        # The client demonstrably reads RDC and can never rebuild it, so dropping it is
        # a measured bet, not a free win. Say so every single time rather than let the
        # default quietly harden into an assumption.
        print(f"\nWARNING: dropping {_gb(by_rule['RUNTIME_DATA_CACHE'])} GB of RuntimeDataCache.")
        print("  The shipping client READS it (13,795 ReadFile ops, Procmon 2026-08-08) and can")
        print("  never regenerate it. Vanilla ships none and runs, so this is very likely fine --")
        print("  but the cost is only measured by the A/B:  pwsh tools/Invoke-RdcAbTest.ps1 -Off")
        print("  Until that verdict is recorded, prefer --keep-rdc for a build players will run.")


def _shader_cache_report(plans) -> None:
    """Every shipped scene's shader cache, one summary line, and a WARNING per sack whose format
    differs from the majority of the shipped sacks. A report: whether a scene ships its sack is
    the maintainer's call (2026-10-02), so this never refuses. The comparison is among the
    shipped scene sacks only, so a set that lags the engine as a whole raises no WARNING.
    Module-level <Module>/Shaders/D3D11 sacks, where issue #448's lagging sacks were, are left to
    _module_sack_report and never compared."""
    caches = [c for p in plans for c in scene_shader_caches(p)]
    if not caches:
        print("\nscene shader caches: no SceneObj/<scene> folder in the planned modules")
        return
    print("\nscene shader caches, SceneObj/<scene>/ShaderCache/D3D11 (report only, never refuses):")
    for c in caches:
        if not c.sack:
            sack = "sack no"
        elif c.sack_format is None:
            sack = f"sack yes (format unreadable: {c.note})"
        else:
            sack = f"sack yes (format {c.sack_format:#06x})"
        print(f"  {c.where}: header {'yes' if c.header else 'no'}, {sack}")
    with_sack = sum(c.sack for c in caches)
    header_only = sum(c.header and not c.sack for c in caches)
    formats = Counter(c.sack_format for c in caches if c.sack_format is not None)
    unreadable = sum(c.sack and c.sack_format is None for c in caches)
    shown = ", ".join(f"{f:#06x} x{n}" for f, n in sorted(formats.items())) or "none"
    if unreadable:
        shown += f", {unreadable} unreadable"
    print(f"scene shader caches: {len(caches)} scenes, {with_sack} with a sack, "
          f"{len(caches) - with_sack} without ({header_only} with the header only); "
          f"sack formats: {shown}")
    majority, odd = stale_sack_formats(caches)
    for c in odd:
        print(f"WARNING: {c.where} ships a sack in format {c.sack_format:#06x}, not the majority "
              f"{majority:#06x} of the shipped sacks (one of the two formats may be stale, "
              f"see issue #448)")


def _module_sack_report(plans) -> None:
    """The one policy line for module-level sacks, then each <module>/Shaders/D3D11 sack the planned
    modules hold with its size and format. classify has already left them out of the copy, so this
    only reads: a failure here cannot ship a sack, and nothing here is compared or refused."""
    print(f"\n{MODULE_SACK_POLICY}")
    found = [(p, rel, size) for p in plans for rel, size in sorted(p.module_sacks)]
    if not found:
        print("  none in the planned modules")
    for p, rel, size in found:
        fmt, why = read_sack_format(p.root / rel)
        shown = f"format {fmt:#06x}" if fmt is not None else f"format unreadable: {why}"
        print(f"  {p.name}/{rel}: {size:,} bytes, {shown}")


def _jit_report(plans) -> None:
    """One line per shipped copy of a TAOM assembly: whether the JIT optimizes it. A report:
    TAOM ships Debug builds on purpose, so this never refuses."""
    taom = [p for p in plans if p.name.casefold() in SHIPPED_DLLS]
    if not taom:
        print("\nTAOM assemblies: no planned module ships TAOM.dll or TAOM.Dependencies.dll, "
              "so there is no JIT optimization line")
        return
    print("\nTAOM assemblies, JIT optimization (report only, never refuses):")
    for p in taom:
        copies = shipped_dll_copies(p)
        if not copies:
            print(f"  {p.name}: no bin/<platform>/{SHIPPED_DLLS[p.name.casefold()]} copy ships")
        for rel in copies:
            disabled, why = jit_optimization(p.root / rel)
            if disabled is None:
                verdict = f"unknown ({why})"
            elif disabled:
                verdict = "OFF (DebuggableAttribute disables optimization: a Debug build)"
            else:
                # A note means there is no attribute to read, which is not the same ON as an
                # attribute that asks for optimization.
                verdict = f"ON ({why})" if why else "ON"
            print(f"  {p.name}/{rel}: JIT optimization {verdict}")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", required=True, help="Modules root of the dev install or publish output")
    ap.add_argument("--dest", required=True, help="Destination Modules root (must be empty/absent)")
    ap.add_argument("--modules", nargs="*", default=None, help=f"default: {' '.join(DEFAULT_MODULES)}")
    ap.add_argument("--keep-rdc", action="store_true", help="ship RuntimeDataCache (pre-verdict default)")
    ap.add_argument("--exclude-candidate", action="append", default=[], metavar="RULE",
                    choices=CANDIDATE_RULES, help=f"one of: {', '.join(CANDIDATE_RULES)}")
    ap.add_argument("--allow-unknown", action="store_true",
                    help="proceed even though unrecognised entries were found (they are still not copied)")
    ap.add_argument("--dry-run", action="store_true", help="report only; writes nothing")
    ap.add_argument("--json", metavar="PATH", help="write the manifest as JSON")
    ap.add_argument("--require-build", metavar="REV",
                    help="refuse unless TAOM.dll and TAOM.Dependencies.dll are clean builds of "
                         "this tag or commit (checked in --dry-run too)")
    args = ap.parse_args(argv)
    # A piped stdout on Windows is cp1252: a folder name it cannot encode is escaped, never a
    # traceback that turns a clean run into exit 1.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="backslashreplace")

    src = Path(args.source)
    if not src.is_dir():
        print(f"ERROR: source not found: {src}", file=sys.stderr)
        return 2
    dest = Path(args.dest)

    names = args.modules if args.modules else list(DEFAULT_MODULES)
    plans = []
    for m in names:
        root = src / m
        if not root.is_dir():
            print(f"  skip {m} (not present in source)")
            continue
        print(f"  planning {m} ...", flush=True)
        plans.append(plan_module(root, keep_rdc=args.keep_rdc,
                                 exclude_candidates=args.exclude_candidate))
    if not plans:
        print("ERROR: no modules planned.", file=sys.stderr)
        return 1

    _report(plans, args)
    # The reports only read, and none may change the exit code or what ships: a failure in one is
    # printed and the run goes on as if it had not been there. (classify leaves the module sacks
    # out, not their report, so a report that fails cannot ship one.)
    for label, report in (("scene shader cache", _shader_cache_report),
                          ("module shader sack", _module_sack_report),
                          ("JIT optimization", _jit_report)):
        try:
            report(plans)
        except Exception as e:
            print(f"\n{label} report failed, ignored: {type(e).__name__}: {e}")

    unknown_total = sum(p.unknown_bytes for p in plans)
    has_unknown = any(p.unknown for p in plans)
    if has_unknown:
        print("\nUNRECOGNISED ENTRIES -- not copied. Review, then re-run with --allow-unknown")
        print("(or add them to KNOWN_TOP_DIRS/KNOWN_TOP_FILES if they belong in the build):")
        for p in plans:
            for rel in p.unknown[:20]:
                print(f"  {p.name}/{rel}")
            if len(p.unknown) > 20:
                print(f"  ... and {len(p.unknown) - 20} more in {p.name}")

    retired = retired_binaries(plans)
    if retired:
        print("\nERROR: retired binaries in the source; delete them there (install bin folders or "
              "the channel folder) and re-run:", file=sys.stderr)
        for rel in retired:
            print(f"  {rel}", file=sys.stderr)
        return 2

    if args.require_build is not None:
        problems, checked = require_build(plans, args.require_build, names)
        if problems:
            print(f"\nERROR: not a clean build of {args.require_build}; refusing to package:",
                  file=sys.stderr)
            for msg in problems:
                print(f"  {msg}", file=sys.stderr)
            return 2
        print(f"\nbuild stamp OK: {len(checked)} TAOM assembly copies are clean builds of "
              f"{args.require_build}:")
        for rel in checked:
            print(f"  {rel}")

    if args.json:
        manifest = {
            "source": str(src),
            "dest": str(dest),
            "keep_rdc": args.keep_rdc,
            "excluded_candidates": args.exclude_candidate,
            "modules": [
                {
                    "name": p.name, "copy_bytes": p.copy_bytes, "exclude_bytes": p.exclude_bytes,
                    "unknown_bytes": p.unknown_bytes, "copy_files": p.copy_files,
                    "by_rule": p.by_rule, "unknown": p.unknown,
                    "candidates_shipped": p.candidates_shipped,
                }
                for p in plans
            ],
            "totals": {
                "copy_bytes": sum(p.copy_bytes for p in plans),
                "exclude_bytes": sum(p.exclude_bytes for p in plans),
                "unknown_bytes": unknown_total,
            },
        }
        Path(args.json).write_text(json.dumps(manifest, indent=2))
        print(f"\nmanifest -> {args.json}")

    if args.dry_run:
        print("\n(dry run -- nothing written)")
        return 0

    if has_unknown and not args.allow_unknown:
        print("\nERROR: unrecognised entries found; refusing to package. "
              "Re-run with --allow-unknown once you have reviewed the list above.",
              file=sys.stderr)
        return 2

    if dest.exists() and any(dest.iterdir()):
        print(f"ERROR: destination is not empty: {dest}", file=sys.stderr)
        return 2

    print(f"\ncopying to {dest} ...")
    total = 0
    for p in plans:
        total += execute(p, dest)
        print(f"  {p.name}: {p.copy_files} files")
    print(f"\ndone -- {total} files, {_gb(sum(p.copy_bytes for p in plans))} GB")
    print("Next: python tools/validate_moduledata.py --game-modules "
          f"\"{dest}\"  then launch the packaged build and play a battle.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
