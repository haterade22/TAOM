"""Insert <UsablePiece>/<AvailablePiece> entries into the two XSLT files.

The Armory's XSLTs use the full-replace pattern: each
`<xsl:template match="CraftingTemplate[@id='OneHandedSword']/UsablePieces">` rewrites
the entire `<UsablePieces>` node, intentionally dropping vanilla pieces. So adding a
new piece = inserting a new `<UsablePiece piece_id="..." />` (or `<AvailablePiece id="..." />`)
into the matching template's body.

If the matching template doesn't exist yet (rare — would happen if we add support for a
new weapon_class the Armory has never used), we synthesize the whole
`<xsl:template match=...>` block and insert it before `</xsl:stylesheet>`.

Idempotent: skip the insertion if the entry already appears in the matching block.
"""

from __future__ import annotations

import re
from dataclasses import dataclass


XSLT_INDENT_INNER = "\t\t\t"   # entries inside UsablePieces/AvailablePieces use 3 tabs (matches existing files)
XSLT_INDENT_OUTER = "\t\t"     # the wrapper element uses 2 tabs


@dataclass
class XsltInsertion:
    weapon_class: str
    piece_ids: list[str]
    inserted: list[str]            # piece_ids that were actually inserted (others were duplicates)


def insert_usable_pieces(xslt_text: str, weapon_class: str, piece_ids: list[str]) -> tuple[str, XsltInsertion]:
    """Insert <UsablePiece piece_id="..." /> into crafting_templates.xslt."""
    return _insert_into_xslt_block(
        xslt_text,
        match_attr=f"CraftingTemplate[@id='{weapon_class}']/UsablePieces",
        wrapper_tag="UsablePieces",
        entry_tag="UsablePiece",
        entry_id_attr="piece_id",
        weapon_class=weapon_class,
        ids=piece_ids,
    )


def insert_available_pieces(xslt_text: str, weapon_class: str, piece_ids: list[str]) -> tuple[str, XsltInsertion]:
    """Insert <AvailablePiece id="..." /> into weapon_descriptions.xslt."""
    return _insert_into_xslt_block(
        xslt_text,
        match_attr=f"WeaponDescription[@id='{weapon_class}']/AvailablePieces",
        wrapper_tag="AvailablePieces",
        entry_tag="AvailablePiece",
        entry_id_attr="id",
        weapon_class=weapon_class,
        ids=piece_ids,
    )


def _insert_into_xslt_block(
    xslt_text: str,
    *,
    match_attr: str,
    wrapper_tag: str,
    entry_tag: str,
    entry_id_attr: str,
    weapon_class: str,
    ids: list[str],
) -> tuple[str, XsltInsertion]:
    if not ids:
        return xslt_text, XsltInsertion(weapon_class=weapon_class, piece_ids=ids, inserted=[])

    # Find the <xsl:template match="..."> block that targets this weapon class.
    # Pattern handles single OR double quotes around the match attribute.
    template_open_re = re.compile(
        rf'<xsl:template\s+match\s*=\s*["\']{re.escape(match_attr)}["\']\s*>',
        re.DOTALL,
    )
    open_match = template_open_re.search(xslt_text)
    if not open_match:
        # No template yet — synthesize one before </xsl:stylesheet>.
        synthesized = _synthesize_xslt_template(match_attr, wrapper_tag, entry_tag, entry_id_attr, ids)
        return _insert_before_close(xslt_text, synthesized), XsltInsertion(
            weapon_class=weapon_class, piece_ids=ids, inserted=list(ids)
        )

    # Find the closing </xsl:template> for this template.
    close_match = re.search(r'</xsl:template>', xslt_text[open_match.end():])
    if not close_match:
        raise ValueError(f"XSLT template <xsl:template match='{match_attr}'> has no closing tag")
    block_start = open_match.end()
    block_end = open_match.end() + close_match.start()
    block = xslt_text[block_start:block_end]

    # Detect already-present IDs.
    existing = set(re.findall(rf'<{entry_tag}\s+{entry_id_attr}\s*=\s*"([^"]+)"', block))
    new_ids = [i for i in ids if i not in existing]
    if not new_ids:
        return xslt_text, XsltInsertion(weapon_class=weapon_class, piece_ids=ids, inserted=[])

    # Insert at the START of the line containing the wrapper's closing tag — i.e. backtrack
    # to the previous newline so we don't capture leading whitespace from that line into our
    # first inserted entry, and so the closing-tag line keeps its original indentation.
    wrapper_close = f"</{wrapper_tag}>"
    wc_idx = block.rfind(wrapper_close)
    if wc_idx == -1:
        raise ValueError(f"XSLT template '{match_attr}' has no </{wrapper_tag}> closing tag")
    line_start = block.rfind("\n", 0, wc_idx) + 1   # position of first char on the closing tag's line

    insertion_text = "".join(
        f'{XSLT_INDENT_INNER}<{entry_tag} {entry_id_attr}="{pid}" />\n' for pid in new_ids
    )
    new_block = block[:line_start] + insertion_text + block[line_start:]

    new_xslt = xslt_text[:block_start] + new_block + xslt_text[block_end:]
    return new_xslt, XsltInsertion(weapon_class=weapon_class, piece_ids=ids, inserted=new_ids)


def _synthesize_xslt_template(
    match_attr: str, wrapper_tag: str, entry_tag: str, entry_id_attr: str, ids: list[str]
) -> str:
    lines = [
        f'\t<xsl:template match="{match_attr}">',
        f'\t\t<{wrapper_tag}>',
    ]
    for pid in ids:
        lines.append(f'\t\t\t<{entry_tag} {entry_id_attr}="{pid}" />')
    lines.append(f'\t\t</{wrapper_tag}>')
    lines.append(f'\t</xsl:template>')
    lines.append('')
    return "\n".join(lines)


def _insert_before_close(xslt_text: str, fragment: str) -> str:
    closing = "</xsl:stylesheet>"
    idx = xslt_text.rfind(closing)
    if idx == -1:
        raise ValueError("XSLT has no </xsl:stylesheet> closing tag")
    head = xslt_text[:idx].rstrip("\n") + "\n\n"
    tail = xslt_text[idx:]
    return head + fragment + "\n" + tail
