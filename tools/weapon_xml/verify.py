"""Cross-reference checks across the four output files.

Run after a render to confirm the new IDs are wired up consistently.
Returns a list of human-readable error strings; empty list = clean.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from .render_pieces import existing_piece_ids
from .render_items import existing_item_ids


@dataclass
class VerifyReport:
    errors: list[str]
    warnings: list[str]

    @property
    def ok(self) -> bool:
        return not self.errors


def verify_cross_refs(
    *,
    pieces_xml: str,
    items_xml: str,
    crafting_templates_xslt: str,
    weapon_descriptions_xslt: str,
    new_piece_ids: list[str],
    new_crafted_ids: list[str],
    new_single_ids: list[str],
    weapon_class_by_piece_id: dict[str, str],
) -> VerifyReport:
    errors: list[str] = []
    warnings: list[str] = []

    pieces_present = existing_piece_ids(pieces_xml)
    items_present = existing_item_ids(items_xml)

    # 1. Every new piece appears in crafting_pieces.xml.
    for pid in new_piece_ids:
        if pid not in pieces_present:
            errors.append(f"Piece {pid!r} expected in crafting_pieces.xml but missing")

    # 2. Every new crafted item / single-piece item appears in items XML.
    for iid in new_crafted_ids + new_single_ids:
        if iid not in items_present:
            errors.append(f"Item {iid!r} expected in LOTRAOM_weapons.xml but missing")

    # 3. Every new piece appears in BOTH XSLTs under the right weapon_class block.
    for pid in new_piece_ids:
        wc = weapon_class_by_piece_id.get(pid)
        if not wc:
            warnings.append(f"Piece {pid!r}: no weapon_class recorded; cannot verify XSLT wiring")
            continue
        if not _xslt_contains(crafting_templates_xslt, wc, "UsablePiece", "piece_id", pid):
            errors.append(f"Piece {pid!r} missing <UsablePiece piece_id> in crafting_templates.xslt under {wc}")
        if not _xslt_contains(weapon_descriptions_xslt, wc, "AvailablePiece", "id", pid):
            errors.append(f"Piece {pid!r} missing <AvailablePiece id> in weapon_descriptions.xslt under {wc}")

    return VerifyReport(errors=errors, warnings=warnings)


def _xslt_contains(xslt_text: str, weapon_class: str, entry_tag: str, id_attr: str, piece_id: str) -> bool:
    """True iff the entry exists inside the template targeting this weapon_class."""
    # Find the template block for this weapon_class, then scope the search.
    template_re = re.compile(
        rf"<xsl:template\s+match\s*=\s*[\"'][^\"']*\[@id='{re.escape(weapon_class)}'\][^\"']*[\"']\s*>(.*?)</xsl:template>",
        re.DOTALL,
    )
    m = template_re.search(xslt_text)
    if not m:
        return False
    block = m.group(1)
    return bool(re.search(rf'<{entry_tag}\s+{id_attr}\s*=\s*"{re.escape(piece_id)}"', block))
