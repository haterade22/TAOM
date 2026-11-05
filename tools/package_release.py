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

Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination /
            retired binaries in the source (RETIRED_BINARIES, dry runs included) / a shipped
            ModuleData file defining a taom_test_ id / an Armory shader with no source players
            have, or an Armory shader report that cannot be checked (all dry runs included).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import sys
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

# Binaries an earlier build shipped and the repo has since deleted: inlined into TAOM.dll on
# 2026-05-24. Deploys never delete, so the install, its _wEditor/_Server mirrors and a channel
# folder built from it keep them. The run refuses (dry runs included) until they are pruned.
# Compared case-insensitively by file name. MinHook.x64.dll and TAOM.NativeSkinFixes.dll stay
# allowed on the 1.4.x line: they are its vendored allowlist.
RETIRED_BINARIES = frozenset({"behaviortrees.dll", "behaviortreewrapper.dll"})

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

CANDIDATE_RULES = ("EM_ASSET_PACKAGES", "RACE_TEST")


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


def retired_binaries(plans) -> list:
    """<module>/<rel> of every RETIRED_BINARIES file under a planned module's bin/."""
    return [f"{p.name}/{rel}" for p in plans for rel, _size in p._copy_list
            if rel.split("/", 1)[0] == "bin"
            and PurePosixPath(rel).name.casefold() in RETIRED_BINARIES]


# A Custom-Battle test troop DEFINED by id; a {=taom_test_x} name key alone defines nothing.
_TEST_TROOP_ID_RE = re.compile(rb'\bid\s*=\s*"taom_test_')


def test_troop_files(plans) -> list:
    """<module>/<rel> of every shipped ModuleData .xml or .xslt that defines a taom_test_ id. A
    channel package is cut from the install, where deploys never delete, so a grep of the repo
    cannot vouch for it: the v2.0.35 testing package shipped five test riders that way."""
    found = []
    for p in plans:
        for rel, _size in p._copy_list:
            if rel.split("/", 1)[0] != "ModuleData" or not rel.casefold().endswith((".xml", ".xslt")):
                continue
            try:
                data = (p.root / rel).read_bytes()
            except OSError:
                continue
            if _TEST_TROOP_ID_RE.search(data):
                found.append(f"{p.name}/{rel}")
    return found


ARMORY_MODULE = "LOTRLOME_Armory"
SHADER_REPORT = "Shaders/D3D11/shader_compile_report.log"
# Right-anchored: a mesh name that fills its column leaves one space before the material, so a
# split on runs of two spaces skips those rows (168 of the laptop's 49,746).
_REPORT_ROW = re.compile(r"^(?P<mesh>.*\S)\s+(?P<material>\S+)\s+(?P<shader>\S+)\s+(?P<variants>\d+)\s*$")
_REPORT_TOTAL = re.compile(r"^Total variants:\s*(\d+)\s*$")


def parse_shader_report(text: str) -> tuple:
    """(usage, problem): {shader: sorted materials} from a shader_compile_report.log, and why the
    rows cannot be trusted (no trailer, or row variants that do not sum to it), else None."""
    usage: dict = {}
    variants = 0
    total = None
    for line in text.splitlines():
        trailer = _REPORT_TOTAL.match(line)
        if trailer:
            total = int(trailer.group(1))
            continue
        row = _REPORT_ROW.match(line)
        if row:
            usage.setdefault(row["shader"], set()).add(row["material"])
            variants += int(row["variants"])
    usage = {shader: sorted(mats) for shader, mats in usage.items()}
    if total is None:
        return usage, "has no Total variants: trailer"
    if total != variants:
        return usage, f"rows sum to {variants} variants but its Total variants: trailer says {total}"
    return usage, None


def missing_armory_shaders(plans, game_root: Path) -> tuple:
    """(info_lines, problems) for the Armory's shaders (decision D12). The 1.4.x line installs no
    shader sources, so any taom_ shader is a gap wherever an .rs file sits; a native shader needs
    <game_root>/Shaders/Sources/<shader>.rs. Fails closed: what cannot be checked is a problem."""
    armory = next((p for p in plans if p.name.casefold() == ARMORY_MODULE.casefold()), None)
    if armory is None:
        return [], []
    report = armory.root / SHADER_REPORT
    try:
        text = report.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return [], [f"{ARMORY_MODULE} ships no {SHADER_REPORT}, so its shaders cannot be checked"]
    usage, problem = parse_shader_report(text)
    if problem:
        return [], [f"{ARMORY_MODULE}/{SHADER_REPORT} {problem}"]

    problems = []
    sources = game_root / "Shaders" / "Sources"
    native = sorted(s for s in usage if not s.startswith("taom_"))
    if native and not sources.is_dir():
        problems.append(f"no {sources} to check the Armory's native shaders against; "
                        "pass --game-root <the game folder>")
    for shader in sorted(usage):
        mats = usage[shader]
        named = f"{shader}: {', '.join(mats[:6])}{f' (+{len(mats) - 6} more)' if len(mats) > 6 else ''}"
        if shader.startswith("taom_"):
            problems.append(f"{named} (a taom_ shader; the 1.4.x line installs no shader sources, "
                            "decision D12)")
        elif sources.is_dir() and not (sources / f"{shader}.rs").is_file():
            problems.append(f"{named} (no {shader}.rs in {sources})")
    return [f"  {shader}: {len(usage[shader])} materials" for shader in sorted(usage)], problems


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
    ap.add_argument("--game-root", metavar="PATH", default=None,
                    help="game folder whose Shaders/Sources the Armory's native shaders need "
                         "(default: the parent of --source)")
    args = ap.parse_args(argv)

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

    test_troops = test_troop_files(plans)
    if test_troops:
        print("\nERROR: Custom-Battle test troops (taom_test_ ids) in the source; delete these files "
              "there (the install, or the channel folder) and redeploy before packaging:", file=sys.stderr)
        for rel in test_troops:
            print(f"  {rel}", file=sys.stderr)
        return 2

    game_root = Path(args.game_root) if args.game_root else src.parent
    shader_info, shader_problems = missing_armory_shaders(plans, game_root)
    if shader_problems:
        print("", file=sys.stderr)
        for problem in shader_problems:
            print(f"ERROR: shader sources: {problem}", file=sys.stderr)
        return 2
    if shader_info:
        print("\nshader sources OK:")
        for line in shader_info:
            print(line)

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
