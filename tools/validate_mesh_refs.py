#!/usr/bin/env python3
"""Mesh-reference validator for Bannerlord item XML — does every mesh / collision
body named in an item resolve to a real packaged asset?

Purpose (the hypothesis it exists to confirm/eliminate): an item referencing a
missing `bo_` collision mesh is suspected of causing intermittent infinite
battle-load hangs. This tool extracts every mesh / body reference from the
LOTRLOME_Armory item XML and checks each one against three independent tiers of
evidence so the hypothesis can be supported or weakened with data, not guesswork.

It is the pure-stdlib, body-aware, rgl-log-aware successor to
`tools/Audit-MeshRefs.ps1` (which depends on the TpacTool C# DLLs and OMITS the
collision-body attributes). CLI / exit-code / report conventions mirror
`tools/validate_moduledata.py`.

THREE-TIER EXISTENCE CHECK
  Tier A  (LEAD, authoritative — needs an rgl_log):  parse the engine's own
          content warnings. `get_object failed for body: X` is the smoking gun
          for a missing collision body; `Unable to find material for mesh X`
          and duplicate-registration lines are corroborating. This is the only
          tier that observes the *running* engine, so it is the lead signal.
  Tier B  (offline, visual meshes only):  build the present-Metamesh set from the
          .tpac tables-of-contents and flag any visual `mesh=` ref that is absent.
  Tier C  (offline, exact, opt-in):  `bo_` collision bodies ARE first-class .tpac
          TOC items (PhysicsShape), so flag any `body_name=` ref absent from the
          present-PhysicsShape set. Falls back to a coarse NUL-framed raw-byte
          scan only for packs that soft-failed to parse. Default ON only when no
          rgl_log is available.
          (Until #352 this tier assumed bodies were NOT in the TOC and byte-scanned
          for everything — hence the old "coarse, confirm via rgl_log" caveat.
          They are: pack1.tpac exposes 382 PhysicsShape items, a count confirmed
          independently against TpacTool's own PhysicsShape.TYPE_GUID.)

ISSUE CODES
  MISSING_MESH            (ERROR,   Tier B)  visual mesh not in any .tpac TOC
  MISSING_BODY            (ERROR,   Tier C)  collision body not in any .tpac TOC
  RUNTIME_MISSING_BODY    (ERROR,   Tier A)  engine logged get_object failed for body
  RUNTIME_MISSING_MATERIAL(WARNING, Tier A)  engine logged missing material for mesh
  DUPLICATE_MESH_NAME     (WARNING, Tier A)  engine logged a duplicate registration
  UNVERIFIED_MESH         (WARNING, Tier B-degraded)  would-be-missing but a .tpac
                                              we needed could not be parsed
  UNPARSED_TPAC           (WARNING)          a .tpac soft-failed to parse
  PREFAB_REF              (INFO)             prefab="X" (not a mesh; recorded only)
  UNREFERENCED_MESH       (INFO,   reverse)  packaged mesh no item XML references

REVERSE AUDIT (--unreferenced)
  Tiers A/B/C all ask "does every REFERENCED mesh exist?". --unreferenced asks the
  opposite: "does every PACKAGED mesh have an item?" — the check that answers a
  'the art shipped but it's not in the game' report. It reuses the same present-set
  and ref extraction, so it costs nothing extra once Tier B has run. INFO severity:
  an unreferenced mesh is an audit signal, not a failure, so the exit code is
  unaffected.

Usage:
  python tools/validate_mesh_refs.py [--scan-bodies] [--rgl-log <path>]
  python tools/validate_mesh_refs.py --items <dir> --tpac-modules LOTRLOME_Armory Native SandBoxCore
  python tools/validate_mesh_refs.py --json report.json --code MISSING_BODY
  python tools/validate_mesh_refs.py --unreferenced --prefix sk_gd_   (per-culture reverse audit)

Exit code (mirrors validate_moduledata.py): 1 if any ERROR (or any WARNING with
--warnings-as-errors), 2 if an input path is bad, else 0.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import struct
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

DEFAULT_GAME = Path(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
)
# ModuleData root, NOT ModuleData/LOTRLOME_items: crafting pieces live in
# `LOTRLOME_crafting_pieces.xml` directly under ModuleData/, and a `body_name`
# typo there hangs mission load exactly like one on an item. Scoping to
# LOTRLOME_items/ missed both v2.0.8 hang typos (#352) — this tool was
# body-aware the whole time, just pointed one directory too narrow.
DEFAULT_ITEMS = DEFAULT_GAME / "Modules" / "LOTRLOME_Armory" / "ModuleData"
# Shared meshes live outside the Armory (Native/SandBoxCore), so the present-set
# is the UNION across these modules' AssetPackages.
DEFAULT_TPAC_MODULES = ["LOTRLOME_Armory", "Native", "SandBoxCore"]
DEFAULT_RGL_LOG_DIR = Path(r"C:\ProgramData\Mount and Blade II Bannerlord\logs")


# --------------------------------------------------------------------------- #
# Issue model (mirrors taom_schema.Issue: severity + location + message)       #
# --------------------------------------------------------------------------- #
class Severity(Enum):
    ERROR = "ERROR"
    WARNING = "WARNING"
    INFO = "INFO"


_SEVERITY_ORDER = {Severity.ERROR: 0, Severity.WARNING: 1, Severity.INFO: 2}


@dataclass
class Issue:
    severity: Severity
    code: str
    file: str
    line: int
    entry_id: str
    message: str

    def sort_key(self):
        return (_SEVERITY_ORDER[self.severity], self.code, self.file, self.line)


# --------------------------------------------------------------------------- #
# Mesh reference extraction (the source-of-truth attribute set, derived from a #
# grep of the LOTRLOME_Armory + SandBoxCore item XML — see docs)               #
# --------------------------------------------------------------------------- #
# kind ∈ {visual_mesh, collision_body, prefab}
VISUAL_MESH_ATTRS = {
    "mesh", "holster_mesh", "holster_mesh_with_weapon", "flying_mesh",
}
COLLISION_BODY_ATTRS = {
    "body_name", "shield_body_name",
}
# False-positive traps that END in mesh/body but are NOT mesh names:
#   mesh_maturity_type (enum), holster_mesh_length (numeric), recalculate_body
#   (bool), covers_body (bool). These are excluded by the exact-name allowlists
#   above (we match exact attribute names, never an "ends-with" heuristic).

# Defensive support for multi-mesh container forms (0× in the Armory today, but
# present in some vanilla / future item XML): `multi_mesh="X"`, and child
# elements `<meshes>` / `<Mesh name="X" />`. Treated as visual_mesh.
_MULTI_MESH_ATTR = "multi_mesh"
_MESH_ELEMENT_RE = re.compile(r'<Mesh\b[^>]*?\bname="([^"]+)"')

# Built once: an attribute-name -> kind map, plus a single regex that captures
# attr + value in one pass. Anchored on `<attr>="value"` with a word boundary so
# `holster_mesh_length=` never matches `holster_mesh` etc.
_ATTR_KIND = {a: "visual_mesh" for a in VISUAL_MESH_ATTRS}
_ATTR_KIND.update({a: "collision_body" for a in COLLISION_BODY_ATTRS})
_ATTR_KIND[_MULTI_MESH_ATTR] = "visual_mesh"

_ATTR_RE = re.compile(
    r'\b(' + "|".join(re.escape(a) for a in sorted(_ATTR_KIND, key=len, reverse=True))
    + r')="([^"]+)"')
_PREFAB_RE = re.compile(r'\bprefab="([^"]+)"')


@dataclass
class MeshRef:
    name: str
    attr: str
    kind: str          # visual_mesh | collision_body | prefab
    file: str          # path relative to the scan root
    line: int
    item_id: str
    culture: str       # folder under LOTRLOME_items/, or "" if not derivable


def _lineno(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


_ITEM_OPEN_RE = re.compile(r"<(?:Item|CraftedItem)\b")
_ITEM_ID_RE = re.compile(r'\bid="([^"]+)"')
# Definitions/refs inside XML comments must never be extracted (a commented item
# example would otherwise look like a real ref). Mirrors taom_schema._COMMENT_RE.
_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


def _culture_from_path(rel: str) -> str:
    """Derive the culture/folder from a path under LOTRLOME_items/<culture>/...

    Files directly under LOTRLOME_items (LOTRAOM_weapons.xml etc.) have no
    culture folder -> "". Returns the first path segment if the path is already
    relative to LOTRLOME_items."""
    parts = Path(rel).as_posix().split("/")
    # Locate the LOTRLOME_items anchor if present, else assume rel is already
    # relative to the scan root (the common case for this tool).
    if "LOTRLOME_items" in parts:
        i = parts.index("LOTRLOME_items")
        tail = parts[i + 1:]
    else:
        tail = parts
    # tail = [<culture>, file.xml] or [file.xml]
    if len(tail) >= 2:
        return tail[0]
    return ""


def _entry_id_by_line(text: str) -> dict:
    """Best-effort map line-number -> owning <Item>/<CraftedItem> id. Handles
    multi-line element opens (item attributes can span lines)."""
    result = {}
    current = ""
    awaiting = False
    for i, line in enumerate(text.splitlines(), start=1):
        if _ITEM_OPEN_RE.search(line):
            awaiting = True
        if awaiting:
            mid = _ITEM_ID_RE.search(line)
            if mid:
                current = mid.group(1)
                awaiting = False
        result[i] = current
    return result


def extract_refs_from_text(text: str, rel: str) -> list:
    """Extract every mesh/body/prefab reference from one XML document's text.

    Pure function over text — the unit tests call this directly with synthetic
    XML, no game install required. Comments are stripped first (but the original
    text is used for line numbers so reports point at the real source line)."""
    stripped = _COMMENT_RE.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)
    line_id = _entry_id_by_line(text)
    culture = _culture_from_path(rel)
    refs = []

    for m in _ATTR_RE.finditer(stripped):
        attr, val = m.group(1), m.group(2)
        if not val:
            continue
        line = _lineno(stripped, m.start())
        refs.append(MeshRef(
            name=val, attr=attr, kind=_ATTR_KIND[attr], file=rel, line=line,
            item_id=line_id.get(line, ""), culture=culture,
        ))

    for m in _MESH_ELEMENT_RE.finditer(stripped):
        line = _lineno(stripped, m.start())
        refs.append(MeshRef(
            name=m.group(1), attr="Mesh", kind="visual_mesh", file=rel, line=line,
            item_id=line_id.get(line, ""), culture=culture,
        ))

    for m in _PREFAB_RE.finditer(stripped):
        line = _lineno(stripped, m.start())
        refs.append(MeshRef(
            name=m.group(1), attr="prefab", kind="prefab", file=rel, line=line,
            item_id=line_id.get(line, ""), culture=culture,
        ))

    return refs


def extract_refs(items_root: Path) -> list:
    """Walk an item-XML root and extract every mesh/body/prefab reference."""
    items_root = Path(items_root)
    refs = []
    for xml in sorted(items_root.rglob("*.xml")):
        try:
            text = xml.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        try:
            rel = xml.relative_to(items_root).as_posix()
        except ValueError:
            rel = xml.as_posix()
        refs.extend(extract_refs_from_text(text, rel))
    return refs


# --------------------------------------------------------------------------- #
# Tier B/C: .tpac present-set (lifted from tools/tpac_skeleton_scan.py)        #
# --------------------------------------------------------------------------- #
TPAC_MAGIC = 0x43415054
# Metamesh (visual mesh) TYPE_GUID a08f8b97-197c-4bea-b95b-53846cae834e, as the
# .NET little-endian byte form the container stores (first 3 fields LE).
METAMESH_TYPE_GUID = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")
# PhysicsShape (collision body) TYPE_GUID e8528e0e-64b6-4e61-bae0-7569c0452aea,
# same LE encoding. Read off TpacTool.Lib's PhysicsShape.TYPE_GUID via reflection,
# not guessed. Collision bodies ARE first-class TOC items — see #352.
PHYSICSSHAPE_TYPE_GUID = bytes.fromhex("0e8e52e8b664614ebae07569c0452aea")


@dataclass
class TpacScanResult:
    """Outcome of scanning one .tpac: the Metamesh + PhysicsShape names it
    exposes, plus a soft-fail flag so an unparsable pack degrades gracefully
    (UNVERIFIED_MESH / UNPARSED_TPAC) instead of aborting or emitting a false
    MISSING."""
    path: str
    metamesh_names: set = field(default_factory=set)
    physicsshape_names: set = field(default_factory=set)
    parsed_ok: bool = True
    error: str = ""


def _read_sized_string(f) -> str:
    n = struct.unpack("<i", f.read(4))[0]
    if n <= 0:
        return ""
    return f.read(n).decode("utf-8", errors="replace")


def scan_tpac_metameshes(path) -> TpacScanResult:
    """Stream the .tpac table-of-contents and collect every Metamesh and
    PhysicsShape name.

    Lifted from tpac_skeleton_scan.scan_tpac, with the spec'd fixes:
      * no 50-result cap;
      * on parse drift / suspicious udep_count, SOFT-FAIL this one pack
        (parsed_ok=False) instead of aborting the whole run.
    Container version 2; each item: type_guid(16) item_guid(16) [ver(4) if v>1]
    name(sized) meta_size(8) <meta> checksum(8) seg_count(4) segs(69 each)
    udep_count(4) <udep*48>.
    """
    path = str(path)
    res = TpacScanResult(path=path)
    try:
        with open(path, "rb") as f:
            magic = struct.unpack("<I", f.read(4))[0]
            if magic != TPAC_MAGIC:
                res.parsed_ok = False
                res.error = f"not a tpac (magic=0x{magic:08x})"
                return res
            ver = struct.unpack("<I", f.read(4))[0]
            f.read(16)                              # package_guid
            num_items = struct.unpack("<I", f.read(4))[0]
            f.read(4)                               # padding
            f.read(4)                               # padding

            for i in range(num_items):
                type_guid = f.read(16)
                f.read(16)                          # item_guid
                if ver > 1:
                    f.read(4)                       # item_ver
                name = _read_sized_string(f)
                meta_size = struct.unpack("<q", f.read(8))[0]
                f.seek(meta_size, 1)                # skip metadata
                f.read(8)                           # checksum
                seg_count = struct.unpack("<i", f.read(4))[0]
                if seg_count < 0 or seg_count > 100000:
                    res.parsed_ok = False
                    res.error = f"suspicious seg_count={seg_count} at item {i} ({name!r})"
                    return res
                f.seek(seg_count * 69, 1)           # skip segment headers

                udep_count = struct.unpack("<i", f.read(4))[0]
                if 0 <= udep_count <= 1000:
                    f.seek(udep_count * 48, 1)
                else:
                    # Parser drift — record and stop THIS pack only.
                    res.parsed_ok = False
                    res.error = f"suspicious udep_count={udep_count} at item {i} ({name!r})"
                    return res

                if name:
                    if type_guid == METAMESH_TYPE_GUID:
                        res.metamesh_names.add(name)
                    elif type_guid == PHYSICSSHAPE_TYPE_GUID:
                        res.physicsshape_names.add(name)
    except (OSError, struct.error) as e:
        res.parsed_ok = False
        res.error = f"{type(e).__name__}: {e}"
    return res


@dataclass
class PresentSet:
    """The offline asset inventory: visual-mesh and collision-body names present
    across all parsed .tpacs, plus the list of packs that soft-failed (so missing
    assets can be downgraded to UNVERIFIED rather than reported as false
    MISSING)."""
    metameshes: set = field(default_factory=set)
    physicsshapes: set = field(default_factory=set)
    tpac_paths: list = field(default_factory=list)
    unparsed: list = field(default_factory=list)   # [(path, error)]

    @property
    def fully_parsed(self) -> bool:
        return not self.unparsed


def tpac_paths_for_modules(game_dir: Path, modules: list) -> list:
    """All *.tpac under <game>/Modules/<m>/AssetPackages for each module m."""
    game_dir = Path(game_dir)
    out = []
    for m in modules:
        ap = game_dir / "Modules" / m / "AssetPackages"
        if ap.exists():
            out.extend(sorted(ap.glob("*.tpac")))
    return out


def build_present_set(tpac_paths: list) -> PresentSet:
    """Union the Metamesh + PhysicsShape TOCs across the given .tpac files."""
    ps = PresentSet(tpac_paths=[str(p) for p in tpac_paths])
    for p in tpac_paths:
        res = scan_tpac_metameshes(p)
        if res.parsed_ok:
            ps.metameshes |= res.metamesh_names
            ps.physicsshapes |= res.physicsshape_names
        else:
            ps.unparsed.append((res.path, res.error))
    return ps


def _base_mesh_name(name: str) -> str:
    """Strip the `.lodN` suffix the engine appends — refs and TOC entries match
    on the base name."""
    return re.sub(r"\.lod\d+$", "", name, flags=re.IGNORECASE)


# --------------------------------------------------------------------------- #
# Reverse audit: packaged meshes that NO item XML references                    #
# --------------------------------------------------------------------------- #
# The engine resolves a female body variant by appending `_slim` to the mesh
# name, so `<mesh>_slim` is never named in item XML and is NOT an orphan when its
# base mesh is referenced. Without this suppression the Gondor armour set alone
# reports 100 false positives.
_SLIM_SUFFIX = "_slim"


@dataclass
class ReverseAudit:
    """Result of the --unreferenced pass."""
    orphans: list = field(default_factory=list)   # sorted packaged mesh names
    slim_suppressed: int = 0
    referenced: int = 0        # candidates an item does reference
    considered: int = 0        # packaged meshes after the --prefix filter
    prefix: str = ""


def reverse_audit(present: PresentSet, refs: list, prefix: str = "") -> ReverseAudit:
    """Find packaged Metameshes that no scanned item XML references.

    `prefix` filters the CANDIDATE (packaged) side only — the referenced set is
    always built from every scanned ref, so restricting to one culture can never
    invent an orphan that some other culture's item actually references.

    Matching is case-insensitive here, unlike Tier B's exact lookup. That is
    deliberate and conservative in this direction: a casing mismatch makes a mesh
    look REFERENCED (silently dropped from the report) rather than manufacturing a
    false orphan.
    """
    referenced = {
        _base_mesh_name(r.name).casefold()
        for r in refs if r.kind == "visual_mesh"
    }
    pfx = prefix.casefold()

    candidates = set()
    for raw in present.metameshes:
        base = _base_mesh_name(raw)
        if pfx and not base.casefold().startswith(pfx):
            continue
        candidates.add(base)

    result = ReverseAudit(prefix=prefix, considered=len(candidates))
    for base in candidates:
        folded = base.casefold()
        if folded in referenced:
            result.referenced += 1
            continue
        if folded.endswith(_SLIM_SUFFIX) and folded[: -len(_SLIM_SUFFIX)] in referenced:
            result.slim_suppressed += 1
            continue
        result.orphans.append(base)

    result.orphans.sort(key=str.casefold)
    return result


def reverse_audit_issues(audit: ReverseAudit) -> list:
    """One INFO Issue per orphan, so --json and --code work on them like any other."""
    return [
        Issue(Severity.INFO, "UNREFERENCED_MESH", "(asset packages)", 0, name,
              f'packaged mesh "{name}" is referenced by no scanned item XML '
              f'— shipped art with no item entry, or an intentional variant/'
              f'scene-only mesh')
        for name in audit.orphans
    ]


# Bytes that can legally continue an asset name. A real token boundary is one
# where the byte AFTER the needle is NOT one of these (NUL, length prefix, etc.).
_NAMEBYTE = set(b"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-")
# Read .tpac files in bounded chunks so a 2.4 GB pack never loads whole into RAM.
_TPAC_CHUNK = 64 * 1024 * 1024


def _scan_one_tpac_for_bodies(path, needles: dict, found: set) -> None:
    """Scan one .tpac for any needle in `needles` (bytes -> name), recording hits
    in `found`. Streams in overlapping chunks (overlap = longest needle) so a
    match that straddles a chunk boundary is still seen, without holding the whole
    file in memory. A hit requires a framed boundary (the byte after the needle is
    not a name byte) to avoid `bo_x` matching the longer `bo_xyz`."""
    if not needles:
        return
    remaining = dict(needles)            # shrink as we confirm hits
    overlap = max(len(n) for n in remaining) + 1
    try:
        with open(path, "rb") as f:
            tail = b""
            while True:
                chunk = f.read(_TPAC_CHUNK)
                if not chunk:
                    break
                buf = tail + chunk
                for needle in list(remaining):
                    start = 0
                    while True:
                        idx = buf.find(needle, start)
                        if idx < 0:
                            break
                        after = idx + len(needle)
                        if after < len(buf):
                            if buf[after] not in _NAMEBYTE:
                                found.add(remaining.pop(needle))
                                break
                            start = idx + 1
                        else:
                            # boundary byte is in the next chunk's overlap; defer
                            start = idx + 1
                if not remaining:
                    return
                tail = buf[-overlap:] if len(buf) > overlap else buf
    except OSError:
        return


def bodies_present_in_tpacs(bodies, tpac_paths: list) -> set:
    """Tier C FALLBACK (batch): which of `bodies` appear (framed) in the raw bytes
    of any of `tpac_paths`. Reads each pack ONCE for ALL needles — O(total bytes),
    not O(bodies × bytes) — so it stays tractable against ~150 Native packs.

    Prefer PresentSet.physicsshapes: bodies ARE first-class TOC items, so the TOC
    set is exact and this scan is only for packs that soft-failed to parse (#352
    corrected the old "bodies aren't in the TOC" premise this was built on).
    Coarse: a hit means the name's bytes exist in a pack, not that the engine can
    load it."""
    needles = {b.encode("utf-8"): b for b in bodies}
    found = set()
    for p in tpac_paths:
        if needles and set(needles.values()) - found:
            _scan_one_tpac_for_bodies(p, {n: nm for n, nm in needles.items()
                                          if nm not in found}, found)
    return found


def body_present_in_tpacs(body_name: str, tpac_paths: list) -> bool:
    """Single-body convenience wrapper over bodies_present_in_tpacs (used by the
    unit tests; the CLI path uses the batch form)."""
    return body_name in bodies_present_in_tpacs({body_name}, tpac_paths)


# --------------------------------------------------------------------------- #
# Tier A: rgl_log parsing (the authoritative, running-engine signal)           #
# --------------------------------------------------------------------------- #
@dataclass
class RglFindings:
    path: str
    missing_bodies: set = field(default_factory=set)     # get_object failed for body
    missing_materials: set = field(default_factory=set)  # Unable to find material for mesh
    duplicate_names: set = field(default_factory=set)     # same name and type already exist


_RGL_BODY_RE = re.compile(r"get_object failed for body:\s*(\S+)")
_RGL_MAT_RE = re.compile(r"Unable to find material for mesh\s+(\S.*?)\s*$")
# The duplicate line carries the name inside `...tpac(NAME)`:
#   Package item with same name and type already exist in $BASE/...foo.tpac(foo)
_RGL_DUP_RE = re.compile(r"same name and type already exist.*?\.tpac\(([^)]+)\)")


def parse_rgl_log(path) -> RglFindings:
    """Parse a single rgl_log[_errors] file for the three content-warning
    classes. Pure over file content."""
    path = str(path)
    f = RglFindings(path=path)
    try:
        text = Path(path).read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return f
    return parse_rgl_text(text, path)


def parse_rgl_text(text: str, path: str = "<text>") -> RglFindings:
    f = RglFindings(path=path)
    for line in text.splitlines():
        m = _RGL_BODY_RE.search(line)
        if m:
            f.missing_bodies.add(m.group(1))
            continue
        m = _RGL_MAT_RE.search(line)
        if m:
            f.missing_materials.add(m.group(1).strip())
            continue
        m = _RGL_DUP_RE.search(line)
        if m:
            f.duplicate_names.add(m.group(1))
    return f


def find_newest_rgl_log(log_dir) -> Path | None:
    """Newest rgl_log_errors_*.txt (preferred) or rgl_log_*.txt under log_dir,
    by mtime. Returns None if none found."""
    log_dir = Path(log_dir)
    if not log_dir.exists():
        return None
    candidates = sorted(log_dir.glob("rgl_log_errors_*.txt"),
                        key=lambda p: p.stat().st_mtime, reverse=True)
    if not candidates:
        candidates = sorted(log_dir.glob("rgl_log_*.txt"),
                            key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0] if candidates else None


# --------------------------------------------------------------------------- #
# Classification — combine the tiers into issues                               #
# --------------------------------------------------------------------------- #
def classify(refs: list,
             present: PresentSet | None,
             rgl: RglFindings | None,
             scan_bodies: bool,
             body_tpac_paths: list | None = None) -> list:
    """Turn extracted refs + tier evidence into Issues.

    refs              -- from extract_refs()
    present           -- Tier B present-set, or None if Tier B did not run
    rgl               -- Tier A findings, or None if no rgl_log
    scan_bodies       -- run Tier C raw-byte body scan
    body_tpac_paths   -- .tpac files to byte-scan for Tier C (defaults to
                         present.tpac_paths)
    """
    issues = []
    referenced_visual = {_base_mesh_name(r.name) for r in refs if r.kind == "visual_mesh"}
    referenced_bodies = {r.name for r in refs if r.kind == "collision_body"}

    # ---- Tier B: visual meshes vs present Metamesh TOC --------------------- #
    if present is not None:
        for r in refs:
            if r.kind != "visual_mesh":
                continue
            base = _base_mesh_name(r.name)
            if base in present.metameshes:
                continue
            if not present.fully_parsed:
                # A pack we needed could not be parsed — never assert MISSING.
                issues.append(Issue(
                    Severity.WARNING, "UNVERIFIED_MESH", r.file, r.line, r.item_id,
                    f'visual mesh "{r.name}" (attr {r.attr}) not found in the parsed '
                    f'.tpac TOCs, but {len(present.unparsed)} pack(s) failed to parse '
                    f'— cannot confirm MISSING (verify via rgl_log)',
                ))
            else:
                issues.append(Issue(
                    Severity.ERROR, "MISSING_MESH", r.file, r.line, r.item_id,
                    f'visual mesh "{r.name}" (attr {r.attr}) is referenced by item '
                    f'"{r.item_id or "?"}" but exists in no scanned .tpac',
                ))
        for path, err in present.unparsed:
            issues.append(Issue(
                Severity.WARNING, "UNPARSED_TPAC", path, 0, "",
                f'.tpac could not be fully parsed ({err}); visual meshes it may '
                f'contain are reported as UNVERIFIED_MESH, not MISSING',
            ))

    # ---- Tier C: collision bodies vs present PhysicsShape TOC -------------- #
    if scan_bodies:
        scan_paths = body_tpac_paths
        if scan_paths is None:
            scan_paths = [Path(p) for p in (present.tpac_paths if present else [])]
        if present is not None and present.physicsshapes:
            # Exact: bodies are first-class PhysicsShape TOC items (#352).
            present_bodies = referenced_bodies & present.physicsshapes
            unresolved = referenced_bodies - present_bodies
            # Only if a pack we needed soft-failed do we fall back to the coarse
            # byte scan — never assert MISSING on evidence we couldn't read.
            if unresolved and not present.fully_parsed:
                present_bodies |= bodies_present_in_tpacs(unresolved, scan_paths)
        else:
            # No TOC available (Tier B skipped) — coarse byte scan is all we have.
            present_bodies = bodies_present_in_tpacs(referenced_bodies, scan_paths)
        for body in sorted(referenced_bodies):
            if body in present_bodies:
                continue
            # Report against every ref of this missing body.
            for r in refs:
                if r.kind == "collision_body" and r.name == body:
                    issues.append(Issue(
                        Severity.ERROR, "MISSING_BODY", r.file, r.line, r.item_id,
                        f'collision body "{body}" (attr {r.attr}) referenced by item '
                        f'"{r.item_id or "?"}" found in no scanned .tpac raw bytes '
                        f'(coarse — confirm via rgl_log)',
                    ))

    # ---- Tier A: rgl_log content warnings --------------------------------- #
    if rgl is not None:
        # Map body name -> referencing refs, for cross-reference attribution.
        body_refs = defaultdict(list)
        for r in refs:
            if r.kind == "collision_body":
                body_refs[r.name].append(r)
        mesh_refs = defaultdict(list)
        for r in refs:
            if r.kind == "visual_mesh":
                mesh_refs[_base_mesh_name(r.name)].append(r)

        for body in sorted(rgl.missing_bodies):
            refs_for = body_refs.get(body, [])
            if refs_for:
                for r in refs_for:
                    issues.append(Issue(
                        Severity.ERROR, "RUNTIME_MISSING_BODY", r.file, r.line, r.item_id,
                        f'engine logged "get_object failed for body: {body}" — '
                        f'referenced by item "{r.item_id or "?"}" (attr {r.attr})',
                    ))
            else:
                # Logged by the engine but not referenced by any scanned item =>
                # almost always a SCENE body, not an item body. Informational —
                # proves the cross-reference works end to end.
                issues.append(Issue(
                    Severity.INFO, "RUNTIME_MISSING_BODY", rgl.path, 0, "",
                    f'engine logged "get_object failed for body: {body}" but NO '
                    f'scanned item references it — runtime-missing body not '
                    f'referenced by any scanned item (likely a scene body)',
                ))

        for mesh in sorted(rgl.missing_materials):
            refs_for = mesh_refs.get(_base_mesh_name(mesh), [])
            if refs_for:
                for r in refs_for:
                    issues.append(Issue(
                        Severity.WARNING, "RUNTIME_MISSING_MATERIAL", r.file, r.line, r.item_id,
                        f'engine logged "Unable to find material for mesh {mesh}" — '
                        f'referenced by item "{r.item_id or "?"}"',
                    ))
            else:
                issues.append(Issue(
                    Severity.WARNING, "RUNTIME_MISSING_MATERIAL", rgl.path, 0, "",
                    f'engine logged "Unable to find material for mesh {mesh}" '
                    f'(not referenced by any scanned item — likely a scene mesh)',
                ))

        for name in sorted(rgl.duplicate_names):
            issues.append(Issue(
                Severity.WARNING, "DUPLICATE_MESH_NAME", rgl.path, 0, name,
                f'engine logged a duplicate registration for "{name}" '
                f'(same name and type already exist)',
            ))

    # ---- prefab refs (informational) -------------------------------------- #
    for r in refs:
        if r.kind == "prefab":
            issues.append(Issue(
                Severity.INFO, "PREFAB_REF", r.file, r.line, r.item_id,
                f'prefab "{r.name}" referenced (not a mesh — recorded only)',
            ))

    issues.sort(key=lambda i: i.sort_key())
    return issues


# --------------------------------------------------------------------------- #
# Reporting (mirrors taom_schema.format_report + adds the tier/grouping body)  #
# --------------------------------------------------------------------------- #
def format_report(issues: list, refs: list, present: PresentSet | None,
                  rgl: RglFindings | None, scan_bodies: bool,
                  reverse: ReverseAudit | None = None) -> str:
    out = []
    out.append("=== MESH REFERENCE VALIDATION ===")

    # -- which tiers ran -- #
    tiers = []
    tiers.append("A (rgl_log: " + (rgl.path if rgl else "OFFLINE — launch a game "
                 "session for the authoritative check") + ")")
    tiers.append("B (offline .tpac TOC visual-mesh)" if present is not None
                 else "B (skipped — no .tpac present-set)")
    # Which method Tier C actually used depends on whether a PhysicsShape TOC was
    # available — say the one that ran, never assume.
    body_toc = present is not None and bool(present.physicsshapes)
    if not scan_bodies:
        tiers.append("C (skipped — pass --scan-bodies)")
    elif body_toc:
        tiers.append("C (offline .tpac TOC collision-body, EXACT)")
    else:
        tiers.append("C (offline raw-byte collision-body, COARSE — no PhysicsShape TOC)")
    out.append("Tiers run:")
    for t in tiers:
        out.append(f"  - Tier {t}")
    if scan_bodies and not body_toc:
        out.append("  NOTE: no PhysicsShape TOC was available, so Tier C fell back to a "
                   "coarse substring scan with length/NUL framing; a hit means the body "
                   "name's bytes exist in a .tpac, not that the engine can load it — "
                   "confirm via rgl_log.")

    # -- counts -- #
    n_visual = sum(1 for r in refs if r.kind == "visual_mesh")
    n_body = sum(1 for r in refs if r.kind == "collision_body")
    n_prefab = sum(1 for r in refs if r.kind == "prefab")
    uniq_visual = {_base_mesh_name(r.name) for r in refs if r.kind == "visual_mesh"}
    uniq_body = {r.name for r in refs if r.kind == "collision_body"}
    out.append("")
    out.append("Refs extracted:")
    out.append(f"  visual meshes : {n_visual} ({len(uniq_visual)} unique)")
    out.append(f"  collision bodies: {n_body} ({len(uniq_body)} unique)")
    out.append(f"  prefab refs   : {n_prefab}")
    if present is not None:
        present_visual = sum(1 for v in uniq_visual if v in present.metameshes)
        out.append(f"  .tpac files scanned: {len(present.tpac_paths)} "
                   f"({len(present.unparsed)} unparsed)")
        out.append(f"  present Metameshes in TOCs: {len(present.metameshes):,}")
        out.append(f"  unique visual meshes present (Tier B): {present_visual}/{len(uniq_visual)}")

    n_missing_mesh = sum(1 for i in issues if i.code == "MISSING_MESH")
    n_missing_body = sum(1 for i in issues if i.code == "MISSING_BODY")
    n_runtime_body = sum(1 for i in issues if i.code == "RUNTIME_MISSING_BODY"
                         and i.severity is Severity.ERROR)
    out.append("  missing-by-tier:")
    out.append(f"    Tier A runtime-missing bodies (item-referenced): {n_runtime_body}")
    out.append(f"    Tier B missing visual meshes: {n_missing_mesh}")
    out.append(f"    Tier C missing collision bodies: {n_missing_body}")

    # -- reverse audit: packaged meshes with no item ------------------------- #
    if reverse is not None:
        out.append("")
        out.append("Reverse audit (--unreferenced):")
        out.append(f"  prefix filter             : {reverse.prefix or '(none)'}")
        out.append(f"  packaged meshes considered: {reverse.considered:,}")
        out.append(f"  referenced by an item     : {reverse.referenced:,}")
        out.append(f"  _slim variants suppressed : {reverse.slim_suppressed:,}")
        out.append(f"  UNREFERENCED (no item)    : {len(reverse.orphans):,}")
        if present is not None and len(present.tpac_paths) > 20 and not reverse.prefix:
            out.append("  NOTE: the candidate side spans every module in "
                       "--tpac-modules, so vanilla's scene/prop/weapon meshes count "
                       "as 'unreferenced' against mod item XML and swamp the result. "
                       "For a modded-art audit pass --tpac-modules LOTRLOME_Armory "
                       "(or narrow with --prefix).")

    # -- the issue list -- #
    out.append("")
    if not issues:
        out.append("PASS: no mesh-reference issues found.")
    else:
        by_code = defaultdict(int)
        for i in issues:
            by_code[i.code] += 1
            loc = f"{i.file}:{i.line}" if i.line else i.file
            eid = f" [{i.entry_id}]" if i.entry_id else ""
            out.append(f"  {i.severity.value:<7} {i.code:<24} {loc}{eid}")
            out.append(f"            {i.message}")

    # -- grouped: missing mesh -> referencing items, then by culture/folder -- #
    missing = [i for i in issues if i.code in ("MISSING_MESH", "MISSING_BODY")]
    if missing:
        out.append("")
        out.append("--- Missing assets grouped by mesh/body -> referencing items ---")
        ref_by_name = defaultdict(list)
        for r in refs:
            ref_by_name[r.name].append(r)
        seen = set()
        for i in missing:
            # entry_id-free; recover the asset name from the message-bearing ref
            pass
        # Group by the asset name extracted from refs that are missing.
        missing_names = set()
        for i in missing:
            # parse the quoted name out of the message
            m = re.search(r'"([^"]+)"', i.message)
            if m:
                missing_names.add(m.group(1))
        for name in sorted(missing_names):
            rs = ref_by_name.get(name, [])
            out.append(f'  {name}  ({len(rs)} ref(s)):')
            for r in rs:
                out.append(f'    {r.file}:{r.line}  item="{r.item_id or "?"}"')
        # secondary grouping by culture/folder
        out.append("")
        out.append("  by culture/folder:")
        by_culture = defaultdict(set)
        for name in missing_names:
            for r in ref_by_name.get(name, []):
                by_culture[r.culture or "(root)"].add(name)
        for culture in sorted(by_culture):
            out.append(f'    {culture}: {len(by_culture[culture])} missing '
                       f'({", ".join(sorted(by_culture[culture]))})')

    # -- summary line + interpretation footer -- #
    n_err = sum(1 for i in issues if i.severity is Severity.ERROR)
    n_warn = sum(1 for i in issues if i.severity is Severity.WARNING)
    n_info = sum(1 for i in issues if i.severity is Severity.INFO)
    out.append("")
    out.append("=== SUMMARY ===")
    out.append(f"  {n_err} error(s), {n_warn} warning(s), {n_info} info")
    by_code = defaultdict(int)
    for i in issues:
        by_code[i.code] += 1
    for code, n in sorted(by_code.items(), key=lambda kv: -kv[1]):
        out.append(f"    {code:<26} {n}")

    out.append("")
    out.append("--- Interpretation (bo-mesh / battle-load-hang) ---")
    item_runtime_missing = n_runtime_body
    total_missing_bodies = n_missing_body + item_runtime_missing
    if total_missing_bodies == 0 and n_missing_mesh == 0:
        out.append("  No missing collision bodies and no missing visual meshes were "
                   "found in scanned items. Nothing in the SCANNED SCOPE can cause "
                   "the load hang — but scope is exactly what this tool got wrong "
                   "before (#352: it scanned ModuleData/LOTRLOME_items/ while the "
                   "two live typos sat in ModuleData/LOTRLOME_crafting_pieces.xml). "
                   "Confirm --items covers every XML that names a body before "
                   "concluding; then look elsewhere (scene bodies, prefab refs, "
                   "async asset load order, GPU-driver stalls per "
                   "feedback_bannerlord_async_load_check_gpu_first.md).")
    else:
        out.append(f"  {total_missing_bodies} missing collision-body ref(s) and "
                   f"{n_missing_mesh} missing visual-mesh ref(s) found. A missing "
                   "body is a CONFIRMED cause of the infinite load hang, not a "
                   "hypothesis (#352): PreloadHelper.WaitForMeshesToBeLoaded polls "
                   "every registered body name and never exits until each resolves. "
                   "Check each ref for a near-match in bodies-present first — both "
                   "real cases were suffix typos with the correct body shipped and "
                   "sitting one character away; fix the ref, don't delete the item.")
    if reverse is not None:
        if reverse.orphans:
            out.append(f"  Reverse audit: {len(reverse.orphans)} packaged mesh(es) "
                       "are referenced by no item XML. That is 'art shipped, no item "
                       "entry' — the shape behind a 'this armour isn't in the game' "
                       "report. Rule out the benign cases first: scene/prefab-only "
                       "meshes, crafting-piece meshes outside --items, and variant "
                       "meshes the engine resolves by name convention (the `_slim` "
                       "female variants are already suppressed).")
        else:
            out.append("  Reverse audit: every packaged mesh in scope is referenced "
                       "by an item. 'Missing in game' reports for this scope are NOT "
                       "a data gap — look at reachability instead (does any troop "
                       "wear it, does any market stock it).")
    if rgl is None:
        out.append("  Tier A did not run (no rgl_log). The OFFLINE tiers cannot "
                   "observe the running engine — for the authoritative check, "
                   "launch a battle then re-run with --rgl-log <newest>.")
    if present is not None and not present.fully_parsed:
        out.append(f"  WARNING: {len(present.unparsed)} .tpac(s) failed to parse; "
                   "Tier B is degraded (UNVERIFIED_MESH entries are unconfirmed, "
                   "not clean).")

    return "\n".join(out)


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def main() -> int:
    # Reports contain a few non-ASCII glyphs (em-dash, ×). Force UTF-8 on the
    # console so a cp1252 Windows terminal / piped consumer never crashes with
    # UnicodeEncodeError (the file the report represents is UTF-8 regardless).
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--items", default=str(DEFAULT_ITEMS),
                    help="Root dir of item XML to scan (default: LOTRLOME_items)")
    ap.add_argument("--game", default=str(DEFAULT_GAME),
                    help="Bannerlord install dir (for --tpac-modules AssetPackages)")
    ap.add_argument("--tpac-modules", nargs="+", default=DEFAULT_TPAC_MODULES,
                    help="Module names whose AssetPackages/*.tpac form the "
                         "present-set (default: LOTRLOME_Armory Native SandBoxCore)")
    ap.add_argument("--rgl-log", default=None,
                    help="Path to an rgl_log[_errors].txt for Tier A (default: "
                         "auto-discover the newest under the ProgramData logs dir)")
    ap.add_argument("--no-rgl-log", action="store_true",
                    help="Disable Tier A entirely (don't auto-discover an rgl_log)")
    ap.add_argument("--scan-bodies", action="store_true",
                    help="Run Tier C raw-byte collision-body scan (auto-on when "
                         "no rgl_log is available)")
    ap.add_argument("--no-tier-b", action="store_true",
                    help="Skip Tier B (.tpac TOC visual-mesh) — e.g. on a machine "
                         "without the asset packages")
    ap.add_argument("--json", dest="json_out", default=None,
                    help="Write the full issue list to this JSON file")
    ap.add_argument("--code", action="append", default=None,
                    help="Only report this issue code (repeatable)")
    ap.add_argument("--unreferenced", action="store_true",
                    help="Reverse audit: also report packaged meshes that NO item "
                         "XML references (INFO — does not affect the exit code). "
                         "Requires Tier B.")
    ap.add_argument("--prefix", default="",
                    help="With --unreferenced, restrict the packaged-mesh candidates "
                         "to this id prefix (e.g. sk_gd_ for Gondor). The referenced "
                         "set is never filtered.")
    ap.add_argument("--warnings-as-errors", action="store_true",
                    help="Exit non-zero if any WARNING is found, not just ERROR")
    args = ap.parse_args()

    items_root = Path(args.items)
    game_dir = Path(args.game)

    if not items_root.exists():
        print(f"ERROR: item XML root not found: {items_root}", file=sys.stderr)
        return 2

    # -- extract refs (always works; no install needed) -- #
    refs = extract_refs(items_root)
    print(f"Extracted {len(refs)} mesh/body/prefab refs from {items_root}",
          file=sys.stderr)

    # -- Tier A: rgl_log -- #
    rgl = None
    if not args.no_rgl_log:
        rgl_path = None
        if args.rgl_log:
            rgl_path = Path(args.rgl_log)
            if not rgl_path.exists():
                print(f"WARNING: --rgl-log not found: {rgl_path} — Tier A skipped",
                      file=sys.stderr)
                rgl_path = None
        else:
            rgl_path = find_newest_rgl_log(DEFAULT_RGL_LOG_DIR)
            if rgl_path is None:
                print(f"WARNING: no rgl_log found under {DEFAULT_RGL_LOG_DIR} — "
                      f"Tier A skipped (offline only)", file=sys.stderr)
        if rgl_path is not None:
            rgl = parse_rgl_log(rgl_path)
            print(f"Tier A: parsed {rgl_path} "
                  f"({len(rgl.missing_bodies)} missing-body, "
                  f"{len(rgl.missing_materials)} missing-material, "
                  f"{len(rgl.duplicate_names)} duplicate)", file=sys.stderr)

    # -- Tier B: present-set from .tpac TOCs -- #
    present = None
    tpac_paths = []
    if not args.no_tier_b:
        if not game_dir.exists():
            print(f"WARNING: game dir not found: {game_dir} — Tier B/C skipped "
                  f"(need AssetPackages). Ref extraction + Tier A still run.",
                  file=sys.stderr)
        else:
            tpac_paths = tpac_paths_for_modules(game_dir, args.tpac_modules)
            if not tpac_paths:
                print(f"WARNING: no .tpac found for modules {args.tpac_modules} "
                      f"under {game_dir} — Tier B/C skipped", file=sys.stderr)
            else:
                print(f"Tier B: scanning {len(tpac_paths)} .tpac files across "
                      f"{args.tpac_modules}...", file=sys.stderr)
                present = build_present_set(tpac_paths)
                print(f"Tier B: {len(present.metameshes):,} Metameshes present, "
                      f"{len(present.unparsed)} pack(s) unparsed", file=sys.stderr)

    # -- Tier C default: ON only when no rgl_log available -- #
    scan_bodies = args.scan_bodies or (rgl is None and bool(tpac_paths))
    if scan_bodies and not tpac_paths:
        print("WARNING: --scan-bodies requested but no .tpac files available — "
              "Tier C skipped", file=sys.stderr)
        scan_bodies = False

    issues = classify(refs, present, rgl, scan_bodies, body_tpac_paths=tpac_paths or None)

    # -- reverse audit (opt-in; needs the Tier B present-set) -- #
    reverse = None
    if args.unreferenced:
        if present is None:
            print("WARNING: --unreferenced needs the Tier B present-set — skipped "
                  "(no .tpac scanned)", file=sys.stderr)
        else:
            reverse = reverse_audit(present, refs, args.prefix)
            issues.extend(reverse_audit_issues(reverse))
            issues.sort(key=lambda i: i.sort_key())
            print(f"Reverse audit: {len(reverse.orphans)} unreferenced of "
                  f"{reverse.considered} packaged mesh(es) in scope "
                  f"({reverse.slim_suppressed} _slim suppressed)", file=sys.stderr)
    elif args.prefix:
        print("WARNING: --prefix has no effect without --unreferenced",
              file=sys.stderr)

    if args.code:
        wanted = set(args.code)
        issues = [i for i in issues if i.code in wanted]

    print(format_report(issues, refs, present, rgl, scan_bodies, reverse))

    if args.json_out:
        payload = [{
            "severity": i.severity.value, "code": i.code, "file": i.file,
            "line": i.line, "entry_id": i.entry_id, "message": i.message,
        } for i in issues]
        Path(args.json_out).write_text(json.dumps(payload, indent=2), encoding="utf-8")
        print(f"\nWrote {len(payload)} issues to {args.json_out}", file=sys.stderr)

    n_err = sum(1 for i in issues if i.severity is Severity.ERROR)
    n_warn = sum(1 for i in issues if i.severity is Severity.WARNING)
    if n_err or (args.warnings_as_errors and n_warn):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
