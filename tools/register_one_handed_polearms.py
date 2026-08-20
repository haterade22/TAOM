#!/usr/bin/env python3
"""Register shield-compatible polearms' crafting pieces under `OneHandedPolearm` in the Armory's XSLT.

WHY
---
The four Dale spears (`dale_spear_a/b`, `dale_winged_spear_a/b`) and the Black Numenorean lance
(`sm_md_num_lance_a`) are crafted on the `TwoHandedPolearm` template with no inline `<Weapon>`, so
every usage they get comes from `WeaponDescription` matching.
`LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt` registers 19 cultures' spear pieces under
`OneHandedPolearm` and none of theirs, so they resolve strictly two-handed:

  * A description applies only when EVERY used piece is in its `AvailablePieces`
    (`Crafting.cs:566-608` -- a counter starting at 4 that decrements per invalid slot and per
    matched piece, and applies the description at <= 0). Blade and handle both, or neither.
  * `TwoHandedPolearm`'s primary usage set is `polearm_block_long_shield_swing_thrust`, flagged
    `requires_no_shield` (`Native/ModuleData/item_usage_sets.xml`).
  * All 28 Dale spear rosters and all 8 Black Numenorean lance rosters also carry a shield, so
    combat AI abandons the polearm the moment a fight starts and fights sword+shield instead.
    Before the fight the troop still holds it, because spawn wield is plain slot order -- which is
    why this reads as "the cavalry draw swords when the battle begins".

`OneHandedPolearm` is listed FIRST in the `TwoHandedPolearm` template's `<WeaponDescriptions>`
(`Native/ModuleData/crafting_templates.xml`), and `Crafting.cs` marks only the first match as
primary, so registering these pieces makes one-handed-with-shield the primary usage and leaves
two-handed as the alternate -- the exact layout Rohan's spears already have.

SCOPE
-----
The hardcoded input is the ITEM list (which weapons should be one-handed -- a design decision).
The piece ids are DERIVED from those items' own `<CraftedItem>` definitions, so a re-modelled
spear that swaps a handle moves the registration with it instead of silently un-fixing itself.

Dale's halberds, poleaxe and war spear stay two-handed on purpose: they are real two-handers, no
roster pairs them with a shield, and although they share handles with the spears, the
every-piece-must-match rule means a shared handle alone never grants them the one-handed usage.

ADDING AN ITEM
--------------
Append its id to `ONE_HANDED_ITEMS` and re-run with `--apply`. Do that only when the gate
(`audit_polearm_shield_parity.py`) reports it, and only when one-handed-with-shield is the right
call for the weapon -- for a genuine two-hander the roster is what should change instead.

WHERE THIS EDIT LIVES
---------------------
`LOTRLOME_Armory` is shipped to players (README.md, `tools/package_release.py`) but is NOT in
this repo, so the edit is unversioned and an Armory refresh reverts it silently. Per CLAUDE.md's
dependency-module trap this script is the replay half; `tools/audit_polearm_shield_parity.py` is
the gate that notices when it has been reverted, and
`docs/reference/lotrlome-armory-snapshot/README.md` carries the written record.

USAGE
-----
    python tools/register_one_handed_polearms.py             # dry run
    python tools/register_one_handed_polearms.py --apply
    python tools/register_one_handed_polearms.py --revert    # drop the marker block

A full game RESTART is required -- weapon descriptions are read at process launch.
"""
from __future__ import annotations

import argparse
import os
import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _gamedir import ENV_VAR, ensure_exists, game_modules  # noqa: E402

DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# The design decision: these items should fight one-handed-with-shield. Their pieces are derived,
# never listed here. Everything else crafted on TwoHandedPolearm stays two-handed.
ONE_HANDED_ITEMS = (
    "dale_spear_a",
    "dale_spear_b",
    "dale_winged_spear_a",
    "dale_winged_spear_b",
    "sm_md_num_lance_a",
)

TARGET_DESCRIPTION = "OneHandedPolearm"
MARKER_START = "<!-- TAOM-1H-POLEARM:START -->"
MARKER_END = "<!-- TAOM-1H-POLEARM:END -->"
BACKUP_SUFFIX = ".bak-1hpolearm"

# The Dale-only predecessor of the marker above. An install patched before the Black Numenorean
# lance was added still carries it, and the current patterns do not match it -- so without an
# explicit sweep the Dale pieces would end up listed twice, under two different markers, and
# `--revert` would leave half the edit behind. Remove after a release or two.
LEGACY_MARKERS = (("<!-- TAOM-DALE-1H:START -->", "<!-- TAOM-DALE-1H:END -->"),)

CRAFTED_ITEM_RE = re.compile(r"<CraftedItem\b.*?</CraftedItem>", re.S)
# The template body runs from the OneHandedPolearm match to its closing tag. Anchoring the
# insert on `</AvailablePieces>` rather than on the `<xsl:apply-templates>` line keeps this
# working if the Armory ever drops the passthrough call from this one template.
TEMPLATE_RE = re.compile(
    r"(<xsl:template\s+match=\"WeaponDescription\[@id='%s'\]/AvailablePieces\">)(.*?)(</xsl:template>)"
    % TARGET_DESCRIPTION,
    re.S,
)


def read_text(path: Path) -> str:
    """Binary read + decode, per tools/README.md's XML I/O convention.

    Text-mode i/o would strip a BOM and normalise CRLF, turning a seven-line insert into a
    whole-file diff. This file is CRLF and BOM-less today; the round-trip keeps it that way
    whatever it becomes.
    """
    return path.read_bytes().decode("utf-8", errors="replace")


def derive_pieces(items_xml: Path) -> tuple[list[str], dict[str, list[str]]]:
    """(ordered piece ids, {item id: its pieces}) for ONE_HANDED_ITEMS."""
    src = read_text(items_xml)
    per_item: dict[str, list[str]] = {}
    for match in CRAFTED_ITEM_RE.finditer(src):
        block = match.group(0)
        found = re.search(r'\bid="([^"]+)"', block)
        if not found or found.group(1) not in ONE_HANDED_ITEMS:
            continue
        per_item[found.group(1)] = re.findall(r'<Piece\s+id="([^"]+)"', block)

    missing = [i for i in ONE_HANDED_ITEMS if i not in per_item]
    if missing:
        sys.exit(f"ERROR: no <CraftedItem> found for {', '.join(missing)} in {items_xml}")

    # Blades before handles, matching how the surrounding culture blocks in the XSLT read.
    ordered: list[str] = []
    for wanted in ("blade", "head", "handle"):
        for item in ONE_HANDED_ITEMS:
            for piece in per_item[item]:
                if wanted in piece and piece not in ordered:
                    ordered.append(piece)
    leftover = [p for pieces in per_item.values() for p in pieces if p not in ordered]
    if leftover:
        sys.exit(
            "ERROR: piece(s) matched none of blade/head/handle, so slot order is unknown: "
            + ", ".join(sorted(set(leftover)))
        )
    return ordered, per_item


def check_pieces_exist(pieces_xml: Path, pieces: list[str]) -> list[str]:
    """Piece ids with no `<CraftingPiece>` definition. An XSLT typo fails silently in game."""
    src = read_text(pieces_xml)
    defined = set(re.findall(r'<CraftingPiece\b[^>]*\bid="([^"]+)"', src))
    return [p for p in pieces if p not in defined]


def block_text(pieces: list[str], indent: str, eol: str) -> str:
    lines = [f"{indent}{MARKER_START}"]
    lines += [f'{indent}<AvailablePiece id="{p}"/>' for p in pieces]
    lines.append(f"{indent}{MARKER_END}")
    return eol.join(lines) + eol


def block_re(start: str, end: str) -> re.Pattern:
    """Match a marker block with its leading indent and trailing newline, so removal leaves no gap.

    The leading `[ \\t]*` matters on the apply path: `block_text` carries the indent of its first
    line, so without it the match can never equal the wanted text and an unchanged file reports
    "updated" forever.
    """
    return re.compile(
        r"[ \t]*" + re.escape(start) + r".*?" + re.escape(end) + r"[ \t]*(?:\r\n|\n)?", re.S
    )


def strip_legacy(text: str) -> tuple[str, bool]:
    """Drop any predecessor marker block. Returns (text, found_any)."""
    found = False
    for start, end in LEGACY_MARKERS:
        text, count = block_re(start, end).subn("", text)
        found = found or bool(count)
    return text, found


def apply_block(src: str, pieces: list[str], eol: str) -> tuple[str, str]:
    """Return (new_src, action) where action is 'inserted' | 'updated' | 'noop'."""
    match = TEMPLATE_RE.search(src)
    if not match:
        sys.exit(
            f"ERROR: no <xsl:template> for {TARGET_DESCRIPTION}/AvailablePieces. The Armory's "
            "weapon_descriptions.xslt has been restructured -- re-derive the anchor before rerunning."
        )
    head, body, tail = match.group(1), match.group(2), match.group(3)

    indent_match = re.search(r'\n([ \t]*)<AvailablePiece\b', body)
    indent = indent_match.group(1) if indent_match else "\t\t\t"
    wanted = block_text(pieces, indent, eol)

    # Converge an install patched under an older marker before deciding insert-vs-update, or the
    # same pieces end up registered twice under two markers.
    body, had_legacy = strip_legacy(body)

    existing = block_re(MARKER_START, MARKER_END).search(body)
    if existing:
        if existing.group(0) == wanted and not had_legacy:
            return src, "noop"
        new_body = body[: existing.start()] + wanted + body[existing.end():]
        action = "updated"
    else:
        # Insert immediately before the close of <AvailablePieces>, i.e. after the passthrough
        # call. Position inside the list does not affect matching -- Crafting.cs tests membership,
        # never order -- so the tail keeps the diff to one contiguous hunk.
        close = body.rfind("</AvailablePieces>")
        if close == -1:
            sys.exit(f"ERROR: {TARGET_DESCRIPTION} template has no </AvailablePieces> close tag.")
        line_start = body.rfind("\n", 0, close) + 1
        new_body = body[:line_start] + wanted + body[line_start:]
        action = "inserted"
    return src[: match.start()] + head + new_body + tail + src[match.end():], action


def revert_block(src: str) -> tuple[str, bool]:
    """Remove the marker block, and any predecessor, so a revert never half-clears the edit."""
    new_src, count = block_re(MARKER_START, MARKER_END).subn("", src)
    new_src, had_legacy = strip_legacy(new_src)
    return new_src, bool(count) or had_legacy


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--apply", action="store_true", help="write changes (default is a dry run)")
    parser.add_argument("--revert", action="store_true", help="remove the marker block")
    parser.add_argument("--game-modules", help="override the Modules folder")
    args = parser.parse_args()

    modules = Path(args.game_modules) if args.game_modules else game_modules(DEFAULT_GAME_ROOT)
    armory = ensure_exists(Path(modules) / "LOTRLOME_Armory", "the LOTRLOME_Armory module")
    xslt = ensure_exists(armory / "ModuleData" / "weapon_descriptions.xslt", "the Armory's weapon_descriptions.xslt")
    items_xml = ensure_exists(
        armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml", "the Armory's weapon items"
    )
    pieces_xml = ensure_exists(
        armory / "ModuleData" / "LOTRLOME_crafting_pieces.xml", "the Armory's crafting pieces"
    )

    src = read_text(xslt)
    eol = "\r\n" if "\r\n" in src else "\n"

    if args.revert:
        new_src, found = revert_block(src)
        if not found:
            print("Nothing to revert -- the marker block is not present.")
            return 1
        if args.apply:
            xslt.write_bytes(new_src.encode("utf-8"))
            print(f"Removed the {MARKER_START.strip('<!- >')} block from {xslt}")
        else:
            print("Dry run: would remove the marker block. Re-run with --apply --revert.")
        return 0

    pieces, per_item = derive_pieces(items_xml)
    undefined = check_pieces_exist(pieces_xml, pieces)
    if undefined:
        print("ERROR: piece id(s) referenced by a target weapon have no <CraftingPiece> definition:", file=sys.stderr)
        for piece in undefined:
            print(f"       {piece}", file=sys.stderr)
        return 1

    print(f"Armory: {armory}")
    for item in ONE_HANDED_ITEMS:
        print(f"  {item:22s} -> {', '.join(per_item[item])}")
    print(f"\n{len(pieces)} distinct piece(s) to register under {TARGET_DESCRIPTION}:")
    for piece in pieces:
        print(f"  {piece}")

    new_src, action = apply_block(src, pieces, eol)
    if action == "noop":
        print("\nAlready registered and up to date -- nothing to do.")
        return 0

    if args.apply:
        backup = Path(str(xslt) + BACKUP_SUFFIX)
        if not backup.exists():
            shutil.copyfile(xslt, backup)
        xslt.write_bytes(new_src.encode("utf-8"))
        print(f"\n{action.upper()} the marker block in {xslt.name}")
        print(f"Backup: {backup.name} (suffix is not .xslt/.xml -- ModuleData folders are globbed)")
        print("A full game RESTART is required; weapon descriptions load at process launch.")
    else:
        print(f"\nDry run: would have {action} the marker block. Re-run with --apply.")
        print(f"(${ENV_VAR} selects the install.)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
