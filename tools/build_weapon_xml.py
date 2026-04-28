#!/usr/bin/env python3
"""FBX -> 4-XML weapon-build pipeline (project-agnostic).

Usage:
    python tools/build_weapon_xml.py [--fbx weapon.fbx] --manifest weapon.xml [--apply]
    python tools/build_weapon_xml.py --manifest-dir weapons/ [--apply]
    python tools/build_weapon_xml.py --init                   # write weapon_xml.toml after a prompt

Resolves output target via flag, weapon_xml.toml in CWD, or interactive prompt.
See docs/features/weapon-xml-pipeline.md for the manifest schema.
"""

from __future__ import annotations

import argparse
import difflib
import os
import sys

from weapon_xml import config, extract, pipeline, verify
from weapon_xml.manifest import Manifest, parse_manifest, parse_manifest_dir


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)

    if args.init:
        return _run_init(args)

    if not args.manifest and not args.manifest_dir:
        print("error: pass --manifest <file> or --manifest-dir <dir>", file=sys.stderr)
        return 2

    paths = config.resolve_output_paths(
        cli_module_data=args.module_data,
        cwd=os.getcwd(),
        interactive=not args.no_interactive,
    )

    _check_target_files(paths)

    manifest = _load_manifest(args)
    fbx_meshes = _maybe_extract(args)

    result = pipeline.build_deltas(
        manifest,
        pieces_xml_path=paths.crafting_pieces_xml,
        items_xml_path=paths.items_xml,
        crafting_templates_xslt_path=paths.crafting_templates_xslt,
        weapon_descriptions_xslt_path=paths.weapon_descriptions_xslt,
        fbx_meshes=fbx_meshes,
        interactive_culture=not args.no_interactive,
    )

    _print_summary(result)

    if args.apply:
        _write_deltas(result)
        # Verify post-write — re-reads disk so we catch any disk/encoding issues.
        report = verify.verify_cross_refs(
            pieces_xml=_read(paths.crafting_pieces_xml),
            items_xml=_read(paths.items_xml),
            crafting_templates_xslt=_read(paths.crafting_templates_xslt),
            weapon_descriptions_xslt=_read(paths.weapon_descriptions_xslt),
            new_piece_ids=result.new_piece_ids,
            new_crafted_ids=result.new_crafted_ids,
            new_single_ids=result.new_single_ids,
            weapon_class_by_piece_id=result.weapon_class_by_piece_id,
        )
        if not report.ok:
            print("\nVERIFY FAILED:", file=sys.stderr)
            for e in report.errors:
                print(f"  ERROR: {e}", file=sys.stderr)
            return 1
        if report.warnings:
            print("\nWarnings:")
            for w in report.warnings:
                print(f"  WARN: {w}")
    else:
        _print_diffs(result)

    return 0


def _parse_args(argv: list[str] | None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--manifest", help="Path to a single weapon manifest .xml")
    p.add_argument("--manifest-dir", help="Directory of .xml manifests to batch-process")
    p.add_argument("--fbx", help="Path to an FBX file (used to validate mesh names)")
    p.add_argument("--module-data", help="Override target ModuleData directory")
    p.add_argument("--apply", action="store_true", help="Write changes (default is dry-run with diff)")
    p.add_argument("--init", action="store_true", help="Write a weapon_xml.toml in CWD after the ModuleData prompt")
    p.add_argument("--no-interactive", action="store_true",
                   help="Fail rather than prompt for a missing ModuleData or culture")
    p.add_argument("--cache-dir", help="Where to write Blender mesh-name caches (default: alongside FBX)")
    p.add_argument("--refresh-cache", action="store_true", help="Re-extract mesh names even if a cache exists")
    return p.parse_args(argv)


def _run_init(args: argparse.Namespace) -> int:
    paths = config.resolve_output_paths(
        cli_module_data=args.module_data,
        cwd=os.getcwd(),
        interactive=True,
    )
    written = config.write_starter_config(os.getcwd(), paths.module_data)
    print(f"wrote {written}")
    return 0


def _load_manifest(args: argparse.Namespace) -> Manifest:
    if args.manifest:
        return parse_manifest(args.manifest)
    return parse_manifest_dir(args.manifest_dir)


def _maybe_extract(args: argparse.Namespace) -> list[str] | None:
    if not args.fbx:
        return None
    res = extract.extract_mesh_names(args.fbx, cache_dir=args.cache_dir, refresh=args.refresh_cache)
    print(f"[extract] {len(res.mesh_names)} meshes from {args.fbx}", file=sys.stderr)
    return res.mesh_names


def _check_target_files(paths: config.OutputPaths) -> None:
    missing = [p for p in paths.all_paths() if not os.path.isfile(p)]
    if missing:
        print("Target files missing — cannot proceed:", file=sys.stderr)
        for m in missing:
            print(f"  - {m}", file=sys.stderr)
        sys.exit(1)


def _print_summary(result: pipeline.PipelineResult) -> None:
    print(f"== weapon_xml summary ==")
    print(f"new pieces:        {len(result.new_piece_ids)}")
    print(f"new crafted items: {len(result.new_crafted_ids)}")
    print(f"new single items:  {len(result.new_single_ids)}")
    print(f"skipped (existed): {len(result.skipped_piece_ids)} pieces, {len(result.skipped_item_ids)} items")
    for name, delta in result.deltas.items():
        marker = "modified" if delta.changed else "unchanged"
        print(f"  {name:25s} -> {delta.path} ({marker})")


def _print_diffs(result: pipeline.PipelineResult) -> None:
    for name, delta in result.deltas.items():
        if not delta.changed:
            continue
        print(f"\n--- diff: {name} ({delta.path}) ---")
        diff = difflib.unified_diff(
            delta.before_text.splitlines(keepends=True),
            delta.after_text.splitlines(keepends=True),
            fromfile=f"{name} (before)",
            tofile=f"{name} (after)",
            n=2,
        )
        sys.stdout.writelines(diff)


def _write_deltas(result: pipeline.PipelineResult) -> None:
    """Write all changed files atomically.

    Phase 1: write each delta to a sibling `<path>.tmp.<pid>` file.
    Phase 2: only after all temp writes succeed, atomically rename each into place.
    If any phase-1 write fails, all temp files are removed and the originals are untouched.
    Cross-file consistency matters here: the four output files have ID references between
    each other, so a partial write would leave a broken state. (deep-review #1, atomicity)
    """
    changed = [d for d in result.deltas.values() if d.changed]
    if not changed:
        return

    pid = os.getpid()
    temp_paths: list[tuple[str, str]] = []   # (final_path, temp_path)
    try:
        for delta in changed:
            tmp = f"{delta.path}.tmp.{pid}"
            with open(tmp, "w", encoding="utf-8", newline="") as f:
                f.write(delta.after_text)
            temp_paths.append((delta.path, tmp))
    except Exception:
        # Phase 1 failed — nuke any temps we created and re-raise.
        for _, tmp in temp_paths:
            try:
                os.remove(tmp)
            except OSError:
                pass
        raise

    # Phase 2: atomic rename. os.replace is atomic on the same filesystem on both
    # POSIX and Windows. If a rename fails partway through, prior renames have
    # committed and we cannot roll those back — but every file written has been
    # successfully buffered to disk in phase 1, so phase-2 failure is rare.
    for final, tmp in temp_paths:
        os.replace(tmp, final)
        print(f"  wrote {final}")


def _read(path: str) -> str:
    # newline="" preserves the file's original line-ending style (CRLF vs LF). Pairing
    # this with newline="" on _write_deltas keeps git diffs clean across edits.
    # (deep-review #1, newline handling)
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


if __name__ == "__main__":
    sys.exit(main())
