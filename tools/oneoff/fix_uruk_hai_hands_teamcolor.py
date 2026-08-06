#!/usr/bin/env python3
"""Set UseTeamColor="true" on the Isengard items whose mesh binds `m_uruk_hai_hands_a1` (#389).

WHY
---
`m_uruk_hai_hands_a1` is authored to require the shader flag
`use_double_colormap_with_mask_texture`. The ONLY thing that adds that flag is
`AgentVisuals.AddTeamColorToMesh` (v1.4.7), which the engine calls only when the ITEM carries
`UseTeamColor="true"`:

    meshAtIndex.Color = color1;  meshAtIndex.Color2 = color2;
    Material val = meshAtIndex.GetMaterial().CreateCopy();
    val.AddMaterialShaderFlag("use_double_colormap_with_mask_texture", false);
    meshAtIndex.SetMaterial(val);

Measured at runtime (render census, Patch67):
    UseTeamColor="false" -> m_uruk_hai_hands_a1        flags 0x480090  -> BLACK
    UseTeamColor="true"  -> m_uruk_hai_hands_a1(copy)  flags 0x4C0090  -> renders correctly

Delta is exactly 0x40000 = the mask flag. 13 Isengard head items already set it and render fine;
this script brings the remaining ones into line.

SCOPE
-----
Edits ONLY items whose `mesh=` is in the set of meshes that bind `m_uruk_hai_hands_a1`, derived
live from the armory's own `shader_compile_report.log` — never a hardcoded id list, so a re-export
that changes the bundling changes the target set too.

BACKUPS
-------
Written as `<file>.xml.bak-teamcolor` — deliberately NOT a `.xml` extension. Bannerlord registers
these folders and globs `*.xml` at campaign start, so an `.xml` backup left beside the original
injects duplicate item ids (documented in `.claude/rules/moduledata-validation.md`).

USAGE
-----
    python tools/oneoff/fix_uruk_hai_hands_teamcolor.py            # dry run
    python tools/oneoff/fix_uruk_hai_hands_teamcolor.py --apply
    python tools/oneoff/fix_uruk_hai_hands_teamcolor.py --revert   # restore from backups

A full game RESTART is required — item XML loads at process launch, not on save-load.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import shutil
import sys

ARMORY = os.environ.get(
    "LOTRLOME_ARMORY_DIR",
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory",
)
REPORT = os.path.join(ARMORY, "Shaders", "D3D11", "shader_compile_report.log")
ITEMS = os.path.join(ARMORY, "ModuleData", "LOTRLOME_items")
TARGET_MATERIAL = "m_uruk_hai_hands_a1"
BACKUP_SUFFIX = ".bak-teamcolor"

ITEM_RE = re.compile(r"<Item\b.*?</Item>|<Item\b[^>]*/>", re.S)
FLAGS_RE = re.compile(r"<Flags\b([^>]*?)(/?)>")


def meshes_binding_material() -> set[str]:
    """Mesh names whose compiled material set includes TARGET_MATERIAL."""
    if not os.path.isfile(REPORT):
        sys.exit(f"shader report not found: {REPORT}")
    found: set[str] = set()
    with open(REPORT, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            # Exact token compare on the Material column, NOT `TARGET_MATERIAL in line`. Substring
            # containment would also match a future superstring material (an `_a10`/`_a1b` export
            # variant) and silently pull that material's meshes into the edit set.
            parts = line.split()
            if len(parts) > 1 and parts[1] == TARGET_MATERIAL:
                found.add(parts[0])
    return found


def set_team_color_true(block: str, eol: str = "\r\n") -> tuple[str, str]:
    """Return (new_block, action). Action is 'flipped' | 'added-attr' | 'added-flags' | 'noop'.

    `eol` is the file's own line ending. The added-flags branch inserts NEW lines, and hardcoding
    "\\n" into an otherwise-CRLF file yields mixed line endings — the exact diff-noise this script
    goes out of its way to avoid everywhere else.
    """
    m = FLAGS_RE.search(block)
    if m:
        attrs = m.group(1)
        if 'UseTeamColor="true"' in attrs:
            return block, "noop"
        if "UseTeamColor=" in attrs:
            new_attrs = re.sub(r'UseTeamColor="[a-z]+"', 'UseTeamColor="true"', attrs)
            return block[: m.start()] + f"<Flags{new_attrs}{m.group(2)}>" + block[m.end():], "flipped"
        # A <Flags> element exists but says nothing about team colour.
        sep = "" if attrs.endswith(" ") or not attrs else " "
        new_attrs = f'{attrs}{sep}UseTeamColor="true"'
        if m.group(2) and not new_attrs.endswith(" "):
            new_attrs += " "
        return block[: m.start()] + f"<Flags{new_attrs}{m.group(2)}>" + block[m.end():], "added-attr"

    # No <Flags> element at all — insert one just before </Item>.
    if block.rstrip().endswith("/>"):
        # Self-closing <Item ... /> must become a container to hold <Flags>.
        head = block.rstrip()[:-2].rstrip()
        return f'{head}>{eol}    <Flags UseTeamColor="true" />{eol}  </Item>', "added-flags"
    idx = block.rfind("</Item>")
    return block[:idx] + f'  <Flags UseTeamColor="true" />{eol}  ' + block[idx:], "added-flags"


def revert() -> int:
    n = 0
    for bak in glob.glob(os.path.join(ITEMS, "**", "*" + BACKUP_SUFFIX), recursive=True):
        original = bak[: -len(BACKUP_SUFFIX)]
        shutil.copyfile(bak, original)
        os.remove(bak)
        print(f"  restored {os.path.basename(original)}")
        n += 1
    print(f"reverted {n} file(s)")
    return 0 if n else 1


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write changes (default is a dry run)")
    ap.add_argument("--revert", action="store_true", help="restore from .bak-teamcolor backups")
    args = ap.parse_args()

    if args.revert:
        return revert()

    meshes = meshes_binding_material()
    print(f"meshes binding {TARGET_MATERIAL}: {len(meshes)}")

    total_changed = 0
    already = 0
    per_file: dict[str, list[tuple[str, str]]] = {}

    for path in sorted(glob.glob(os.path.join(ITEMS, "**", "*.xml"), recursive=True)):
        try:
            # Full byte round-trip — the sanctioned idiom in tools/README.md "XML I/O convention":
            # read binary, decode utf-8 (a BOM survives as a leading U+FEFF *character* and is
            # re-emitted verbatim on encode), edit, write binary. Binary on both ends means no
            # universal-newline translation, so CRLF is preserved exactly — letting Python normalise
            # newlines turned a 79-attribute change into a 6,666-line whole-file diff on this
            # script's first run. Plain `utf-8` TEXT i/o is what silently strips a BOM
            # (RCA docs/reviews/rca-scene-tooling-2026-05-28.md); these files have none today, but
            # this script is explicitly a template for re-running against a changed target set.
            src = open(path, "rb").read().decode("utf-8", errors="replace")
        except OSError:
            continue
        eol = "\r\n" if "\r\n" in src else "\n"

        out, cursor, changes = [], 0, []
        for m in ITEM_RE.finditer(src):
            block = m.group(0)
            mesh = re.search(r'\bmesh="([^"]+)"', block)
            if not mesh or mesh.group(1) not in meshes:
                continue
            iid = re.search(r'\bid="([^"]+)"', block)
            new_block, action = set_team_color_true(block, eol)
            if action == "noop":
                already += 1
                continue
            out.append(src[cursor:m.start()])
            out.append(new_block)
            cursor = m.end()
            changes.append((iid.group(1) if iid else "<no-id>", action))

        if not changes:
            continue
        out.append(src[cursor:])
        new_src = "".join(out)
        rel = os.path.relpath(path, ITEMS)
        per_file[rel] = changes
        total_changed += len(changes)

        if args.apply:
            bak = path + BACKUP_SUFFIX
            if not os.path.exists(bak):
                shutil.copyfile(path, bak)
            open(path, "wb").write(new_src.encode("utf-8"))

    verb = "CHANGED" if args.apply else "would change"
    for rel, changes in sorted(per_file.items()):
        kinds = ", ".join(sorted({a for _, a in changes}))
        print(f"  {verb} {len(changes):3} item(s) in {rel}   [{kinds}]")
    print(f"\n{verb}: {total_changed} item(s); already true (left alone): {already}")

    if args.apply:
        print(f"\nBackups written as *{BACKUP_SUFFIX} (NOT .xml — these folders are globbed).")
        print("A full game RESTART is required; item XML loads at process launch.")
    else:
        print("\nDry run. Re-run with --apply to write.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
