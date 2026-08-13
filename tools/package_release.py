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

Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
from dataclasses import dataclass, field
from pathlib import Path, PurePosixPath

COPY = "copy"
EXCLUDE = "exclude"
UNKNOWN = "unknown"

# Default module set: the TAOM release modules that ship RDC, plus Dependencies.
# Alliance.Wargs is a redistributed companion (README.md) -- opt in explicitly.
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
    if name.endswith(".xml.bak"):
        return Decision(EXCLUDE, "XML_BAK", "ModuleData is glob-loaded: duplicate-registration hazard")
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
