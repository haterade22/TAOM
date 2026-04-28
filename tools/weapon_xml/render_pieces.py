"""Emit <CraftingPiece> blocks into LOTRLOME_crafting_pieces.xml.

Format-faithful: hand-rolled string templates so existing file formatting (attribute-
per-line, 4-space indent on the piece, 8-space indent on its attributes) is preserved.
ElementTree's serializer would normalize this away, so we don't use it for output.

Idempotent: if `<CraftingPiece id="X">` already exists in the file, the piece is skipped
(NOT overwritten). To re-author an existing piece, delete it from the file first.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Iterable

from .manifest import PieceSpec

PIECE_INDENT = "    "       # 4 spaces — matches existing file
ATTR_INDENT  = "        "   # 8 spaces
INNER_INDENT = "            " # 12 spaces (BladeData attrs, etc.)
DEEP_INDENT  = "                " # 16 spaces (Thrust/Swing attrs)


@dataclass
class PieceRender:
    """A rendered piece block ready to insert into the XML."""

    piece_id: str
    block: str       # full <CraftingPiece>...</CraftingPiece> text including trailing blank line


def render_piece(
    piece_id: str,
    piece: PieceSpec,
    *,
    name: str,
    tier: str,
    mesh: str,
) -> PieceRender:
    """Render one CraftingPiece XML block.

    `name` is the localization-tagged display name for THIS piece (e.g. the blade).
    `tier` is the 1-5 crafting tier inherited from the weapon.
    `mesh` is the FBX mesh ID — what links this piece to the visual asset.
    """
    header_attrs: list[tuple[str, str]] = [
        ("id", piece_id),
        ("name", name),
        ("tier", tier),
        ("piece_type", piece.piece_type),
        ("mesh", mesh),
    ]
    # length/weight are common; everything else (e.g. is_default, culture) goes through.
    for key in ("length", "weight"):
        if key in piece.attrs:
            header_attrs.append((key, piece.attrs[key]))
    for key, val in piece.attrs.items():
        if key in {"length", "weight"}:
            continue
        header_attrs.append((key, val))

    lines: list[str] = []
    lines.append(f"{PIECE_INDENT}<CraftingPiece")
    for i, (k, v) in enumerate(header_attrs):
        suffix = ">" if i == len(header_attrs) - 1 else ""
        lines.append(f'{ATTR_INDENT}{k}="{v}"{suffix}')

    body_lines = _render_piece_body(piece)
    lines.extend(body_lines)
    lines.append(f"{PIECE_INDENT}</CraftingPiece>")
    lines.append("")  # trailing blank line between pieces

    return PieceRender(piece_id=piece_id, block="\n".join(lines))


def _render_piece_body(piece: PieceSpec) -> list[str]:
    """Render the inner elements (BladeData / BuildData / StatContributions / Materials)."""
    lines: list[str] = []

    if piece.role == "blade":
        lines.extend(_render_blade_data(piece))
    else:
        # BuildData is conventional on guards/handles/pommels but not strictly required.
        # We emit it only if the piece carries any BuildData-shaped attrs (next/previous/piece_offset).
        build_keys = ("next_piece_offset", "previous_piece_offset", "piece_offset")
        build_attrs = {k: v for k, v in piece.attrs.items() if k in build_keys}
        if build_attrs:
            lines.append(f"{ATTR_INDENT}<BuildData")
            ks = list(build_attrs.items())
            for i, (k, v) in enumerate(ks):
                tail = " />" if i == len(ks) - 1 else ""
                lines.append(f'{INNER_INDENT}{k}="{v}"{tail}')
        if piece.stat_contributions:
            lines.append(f"{ATTR_INDENT}<StatContributions")
            ks = list(piece.stat_contributions.items())
            for i, (k, v) in enumerate(ks):
                tail = " />" if i == len(ks) - 1 else ""
                lines.append(f'{INNER_INDENT}{k}="{v}"{tail}')

    if piece.materials:
        lines.append(f"{ATTR_INDENT}<Materials>")
        for m in piece.materials:
            lines.append(f"{INNER_INDENT}<Material")
            ks = list(m.items())
            for i, (k, v) in enumerate(ks):
                tail = " />" if i == len(ks) - 1 else ""
                lines.append(f'{DEEP_INDENT}{k}="{v}"{tail}')
        lines.append(f"{ATTR_INDENT}</Materials>")

    return lines


def _render_blade_data(piece: PieceSpec) -> list[str]:
    bd = dict(piece.blade_data)
    bd.setdefault("stack_amount", "3")
    bd.setdefault("physics_material", "metal_weapon")
    bd.setdefault("holster_mesh", "")

    lines: list[str] = []
    lines.append(f"{ATTR_INDENT}<BladeData")
    items = list(bd.items())
    if not piece.thrust and not piece.swing:
        # Self-closing BladeData if no Thrust/Swing children
        for i, (k, v) in enumerate(items):
            tail = " />" if i == len(items) - 1 else ""
            lines.append(f'{INNER_INDENT}{k}="{v}"{tail}')
        return lines

    for i, (k, v) in enumerate(items):
        tail = ">" if i == len(items) - 1 else ""
        lines.append(f'{INNER_INDENT}{k}="{v}"{tail}')

    if piece.thrust:
        lines.append(f"{INNER_INDENT}<Thrust")
        ks = list(piece.thrust.items())
        for i, (k, v) in enumerate(ks):
            tail = " />" if i == len(ks) - 1 else ""
            lines.append(f'{DEEP_INDENT}{k}="{v}"{tail}')
    if piece.swing:
        lines.append(f"{INNER_INDENT}<Swing")
        ks = list(piece.swing.items())
        for i, (k, v) in enumerate(ks):
            tail = " />" if i == len(ks) - 1 else ""
            lines.append(f'{DEEP_INDENT}{k}="{v}"{tail}')
    lines.append(f"{ATTR_INDENT}</BladeData>")
    return lines


# ---------------------------------------------------------------------------
# File-level integration: detection + insertion
# ---------------------------------------------------------------------------

_ID_RE = re.compile(r'<CraftingPiece\b[^>]*?\bid="([^"]+)"', re.DOTALL)


def existing_piece_ids(xml_text: str) -> set[str]:
    """Return the set of piece IDs already present in a crafting_pieces.xml file."""
    return set(_ID_RE.findall(xml_text))


def insert_blocks(xml_text: str, blocks: Iterable[PieceRender]) -> tuple[str, list[PieceRender]]:
    """Insert new piece blocks just before the closing </CraftingPieces>.

    Already-present piece-IDs are skipped. Returns (new_text, inserted_blocks).
    """
    have = existing_piece_ids(xml_text)
    new_blocks: list[PieceRender] = [b for b in blocks if b.piece_id not in have]
    if not new_blocks:
        return xml_text, []

    closing = "</CraftingPieces>"
    idx = xml_text.rfind(closing)
    if idx == -1:
        raise ValueError("crafting_pieces.xml has no </CraftingPieces> closing tag")

    # Insert blocks. Each block already ends with one blank line. Make sure we
    # put exactly one blank line between the previous content and our first block,
    # and one blank line between our last block and the closing tag.
    # Each rendered block already ends in a single "\n" (the trailing blank-line marker
    # joined into a single newline). To get one blank line BETWEEN blocks, append an
    # extra "\n" to every block.
    insertion = "".join(b.block + "\n" for b in new_blocks)
    # Trim any trailing whitespace we'd duplicate.
    head = xml_text[:idx].rstrip("\n") + "\n\n"
    tail = xml_text[idx:]
    return head + insertion + tail, new_blocks
