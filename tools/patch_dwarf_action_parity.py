#!/usr/bin/env python3
"""Restore action-type parity to a standalone race action_set.

A standalone race action_set (one with NO ``base_set``, e.g. ``as_dwarf_warrior``
in LOTRLOME_Armory) inherits nothing, so any action type the engine gained since
the set was authored is simply absent. When the engine then requests that type
(falling into water -> ``act_dive_*``, a melee stagger -> ``act_stagger_*``) it
logs "<set> does not contain <act>" and can crash.

This tool reads the engine's authoritative ``as_human_warrior`` action set from the
installed Native module and adds every ``<action>`` whose ``type`` is MISSING from
the target set, using Native's own animation binding (the same human-placeholder
convention the dwarf set already uses throughout). It is:

  * additive only   -- existing entries are never touched or reordered;
  * a text splice   -- the rest of the (3.8 MB) file stays byte-for-byte identical,
                       so the hand-authored comments/partials/other sets are preserved;
  * idempotent      -- a second run finds nothing missing and writes nothing.

Why explicit entries and not just ``base_set="as_human_warrior"``: TAOM's elf-CC RCA
(docs/reviews/rca-elf-cc-facegen-2026-05-22.md) showed the engine has action-lookup
paths that do NOT fall through ``base_set``. Explicit entries always satisfy the
"set contains type" check; a base_set is a gamble against that known quirk.

Run once per file (LIVE LOTRLOME copy + the tracked snapshot). See
docs/features/race-age-system.md and the lesson in docs/reviews/LESSONS-LEARNED.md.
"""
from __future__ import annotations

import argparse
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

DEFAULT_NATIVE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\Native\ModuleData\action_sets.xml"
)


def base_action_nodes(native_path: str, base_set_id: str):
    """Return [(type, animation, alternative_group)] for every <action> in the base set."""
    try:
        root = ET.parse(native_path).getroot()
    except (ET.ParseError, OSError) as exc:
        sys.exit(f"ERROR: cannot read base action_sets.xml at {native_path}: {exc}")
    base = next((s for s in root.findall("action_set") if s.get("id") == base_set_id), None)
    if base is None:
        sys.exit(f"ERROR: action_set id={base_set_id!r} not found in {native_path}")
    nodes = []
    for action in base.findall("action"):
        atype = action.get("type")
        if not atype:
            continue
        nodes.append((atype, action.get("animation") or "", action.get("alternative_group")))
    return nodes


def find_block(text: str, set_id: str):
    """Return (id_index, close_index) for the action_set block, by text scan.

    close_index is the index of the '<' in the block's closing </action_set>.
    """
    m = re.search(r'\bid="' + re.escape(set_id) + r'"', text)
    if not m:
        sys.exit(f"ERROR: action_set id={set_id!r} not found in target file")
    close = text.find("</action_set>", m.end())
    if close == -1:
        sys.exit(f"ERROR: no closing </action_set> after id={set_id!r}")
    return m.start(), close


def render(nodes, nl: str) -> str:
    """Render <action> entries in the dwarf block's tab-indented multi-line style."""
    out = []
    for atype, anim, alt in nodes:
        out.append("\t\t<action")
        out.append(f'\t\t\ttype="{atype}"')
        if alt:
            out.append(f'\t\t\tanimation="{anim}"')
            out.append(f'\t\t\talternative_group="{alt}" />')
        else:
            out.append(f'\t\t\tanimation="{anim}" />')
    return nl.join(out)


def cluster_summary(types) -> str:
    """Human-readable breakdown of the missing types by recognizable cluster."""
    buckets = {
        "water (dive/swim)": lambda t: "dive" in t or "swim" in t,
        "stagger (hit react)": lambda t: t.startswith("act_stagger_"),
        "weapon stance (flail/dagger/sling/backstab)": lambda t: any(
            k in t for k in ("flail", "dagger", "sling", "backstab")
        ),
        "naval / war sails": lambda t: any(
            k in t for k in ("row", "rudder", "ship", "climb_net", "naval", "rower", "xebec")
        ),
        "character creation": lambda t: t.startswith("act_character_creation_"),
    }
    lines = []
    claimed = set()
    for label, pred in buckets.items():
        hits = [t for t in types if pred(t) and t not in claimed]
        claimed.update(hits)
        if hits:
            lines.append(f"    {label}: {len(hits)}")
    other = [t for t in types if t not in claimed]
    if other:
        lines.append(f"    other: {len(other)}")
    return "\n".join(lines)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--native", default=DEFAULT_NATIVE, help="installed Native action_sets.xml (authoritative base)")
    ap.add_argument("--target", required=True, help="action_sets.xml file to patch")
    ap.add_argument("--set-id", default="as_dwarf_warrior", help="standalone set to bring to parity")
    ap.add_argument("--base-set-id", default="as_human_warrior", help="engine base set to mirror")
    ap.add_argument("--apply", action="store_true", help="write the change (default is dry-run)")
    args = ap.parse_args()

    base_nodes = base_action_nodes(args.native, args.base_set_id)

    target_path = Path(args.target)
    try:
        raw = target_path.read_bytes()
    except OSError as exc:
        # Common on an engine-bump re-run: wrong path, or the live file is locked
        # because the game is running. Fail with a clear message, not a stack trace.
        sys.exit(f"ERROR: cannot read target {target_path}: {exc}")
    bom = b"\xef\xbb\xbf"
    has_bom = raw.startswith(bom)
    text = (raw[3:] if has_bom else raw).decode("utf-8")
    nl = "\r\n" if "\r\n" in text else "\n"

    id_idx, close = find_block(text, args.set_id)
    block_text = text[id_idx:close]
    # Strip XML comments before reading existing types: TaleWorlds/LOTRLOME disable
    # actions by commenting them out. A commented-out <action type="X"> must NOT count
    # as "present" (it isn't, at runtime) -- otherwise we'd skip adding an active X.
    # This mirrors how the base side is read (ET, which already ignores comments).
    block_active = re.sub(r"<!--.*?-->", "", block_text, flags=re.S)
    have = set(re.findall(r'type="([^"]+)"', block_active))

    to_add = [(t, a, g) for (t, a, g) in base_nodes if t not in have]
    missing_types = sorted({t for (t, _, _) in to_add})

    print(f"Base  {args.base_set_id}: {len(base_nodes)} action nodes")
    print(f"Target {args.set_id}: {len(have)} existing action types")
    print(f"Missing types: {len(missing_types)}  (nodes to insert, incl. alt-group variants: {len(to_add)})")
    if missing_types:
        print(cluster_summary(missing_types))

    if not to_add:
        print("Already at parity -- nothing to write.")
        return

    if not args.apply:
        print("\nDRY RUN. Re-run with --apply to write. First 15 missing types:")
        for t in missing_types[:15]:
            print("   ", t)
        return

    insertion = render(to_add, nl) + nl
    line_start = text.rfind("\n", 0, close) + 1  # start of the line holding </action_set>
    new_text = text[:line_start] + insertion + text[line_start:]

    backup = target_path.with_suffix(target_path.suffix + ".bak")
    shutil.copy2(target_path, backup)
    target_path.write_bytes((bom if has_bom else b"") + new_text.encode("utf-8"))
    print(f"\nInserted {len(to_add)} action entries into {args.set_id}.")
    print(f"Backup written: {backup}")


if __name__ == "__main__":
    main()
