"""Emit <CraftedItem> and single-piece <Item> blocks into LOTRAOM_weapons.xml.

Format-faithful, idempotent. Skip if the id already exists.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Iterable

from .manifest import CraftedWeaponSpec, SinglePieceWeaponSpec, PIECE_TAG_TO_PIECE_TYPE

ITEM_INDENT  = "    "       # 4 spaces — root child
ATTR_INDENT  = "        "
INNER_INDENT = "            "
DEEP_INDENT  = "                "


@dataclass
class ItemRender:
    item_id: str
    block: str    # full <CraftedItem>... or <Item>... text including trailing blank line


def render_crafted_item(spec: CraftedWeaponSpec, *, piece_ids_by_role: dict[str, str]) -> ItemRender:
    """Render a <CraftedItem> referencing 4-piece IDs.

    `piece_ids_by_role` maps lowercase role name -> piece ID, e.g.
    {'blade': 'wm_x_blade', 'guard': 'wm_x_guard', 'hilt': 'wm_x_hilt', 'pommel': 'wm_x_pommel'}.
    """
    header_attrs: list[tuple[str, str]] = [
        ("id", spec.base_id),
        ("name", spec.name),
        ("crafting_template", spec.weapon_class),
    ]
    if spec.culture:
        header_attrs.append(("culture", _culture_value(spec.culture)))
    for k, v in spec.extra_item_attrs.items():
        header_attrs.append((k, v))

    lines: list[str] = []
    lines.append(f"{ITEM_INDENT}<CraftedItem")
    for i, (k, v) in enumerate(header_attrs):
        tail = ">" if i == len(header_attrs) - 1 else ""
        lines.append(f'{ATTR_INDENT}{k}="{v}"{tail}')

    lines.append(f"{ATTR_INDENT}<Pieces>")
    # Emit pieces in conventional order: Blade, Guard, Handle, Pommel.
    role_order = ["blade", "guard", "hilt", "handle", "pommel"]
    seen: set[str] = set()
    for role in role_order:
        if role in piece_ids_by_role and role not in seen:
            piece_type = PIECE_TAG_TO_PIECE_TYPE[role.capitalize()]
            piece_id = piece_ids_by_role[role]
            lines.append(f"{INNER_INDENT}<Piece")
            lines.append(f'{DEEP_INDENT}id="{piece_id}"')
            lines.append(f'{DEEP_INDENT}Type="{piece_type}"')
            lines.append(f'{DEEP_INDENT}scale_factor="100" />')
            seen.add(role)
    lines.append(f"{ATTR_INDENT}</Pieces>")
    lines.append(f"{ITEM_INDENT}</CraftedItem>")
    lines.append("")  # trailing blank line

    return ItemRender(item_id=spec.base_id, block="\n".join(lines))


def render_single_piece_item(spec: SinglePieceWeaponSpec) -> ItemRender:
    """Render a single-piece <Item> (bow, javelin, throwing weapon)."""
    header_attrs: list[tuple[str, str]] = [
        ("id", spec.item_id),
        ("name", spec.name),
    ]
    if spec.body_name:
        header_attrs.append(("body_name", spec.body_name))
    # mesh + Type + weight come from item_attrs; they are conventional.
    for k, v in spec.item_attrs.items():
        if k != "Type":
            header_attrs.append((k, v))
    if "Type" in spec.item_attrs:
        header_attrs.append(("Type", spec.item_attrs["Type"]))
    if spec.culture:
        header_attrs.append(("culture", _culture_value(spec.culture)))

    lines: list[str] = []
    lines.append(f"{ITEM_INDENT}<Item")
    for i, (k, v) in enumerate(header_attrs):
        tail = ">" if i == len(header_attrs) - 1 else ""
        lines.append(f'{ATTR_INDENT}{k}="{v}"{tail}')

    if spec.weapon_attrs or spec.weapon_flags:
        lines.append(f"{ATTR_INDENT}<ItemComponent>")
        lines.append(f"{INNER_INDENT}<Weapon")
        wattrs = list(spec.weapon_attrs.items())
        # If we have weapon flags, the <Weapon> tag has children, so don't self-close.
        if spec.weapon_flags:
            for i, (k, v) in enumerate(wattrs):
                tail = ">" if i == len(wattrs) - 1 else ""
                lines.append(f'{DEEP_INDENT}{k}="{v}"{tail}')
            flag_pairs = " ".join(f'{k}="{v}"' for k, v in spec.weapon_flags.items())
            lines.append(f"{DEEP_INDENT}<WeaponFlags {flag_pairs} />")
            lines.append(f"{INNER_INDENT}</Weapon>")
        else:
            for i, (k, v) in enumerate(wattrs):
                tail = " />" if i == len(wattrs) - 1 else ""
                lines.append(f'{DEEP_INDENT}{k}="{v}"{tail}')
        lines.append(f"{ATTR_INDENT}</ItemComponent>")

    lines.append(f"{ITEM_INDENT}</Item>")
    lines.append("")

    return ItemRender(item_id=spec.item_id, block="\n".join(lines))


def _culture_value(culture: str) -> str:
    """`gondor` -> `Culture.gondor`. Pass-through if already qualified."""
    return culture if culture.startswith("Culture.") else f"Culture.{culture}"


# ---------------------------------------------------------------------------
# File integration
# ---------------------------------------------------------------------------

_ID_RE = re.compile(r'<(?:CraftedItem|Item)\b[^>]*?\bid="([^"]+)"', re.DOTALL)


def existing_item_ids(xml_text: str) -> set[str]:
    return set(_ID_RE.findall(xml_text))


def insert_blocks(xml_text: str, blocks: Iterable[ItemRender]) -> tuple[str, list[ItemRender]]:
    have = existing_item_ids(xml_text)
    new_blocks = [b for b in blocks if b.item_id not in have]
    if not new_blocks:
        return xml_text, []

    closing = "</Items>"
    idx = xml_text.rfind(closing)
    if idx == -1:
        raise ValueError("LOTRAOM_weapons.xml has no </Items> closing tag")

    insertion = "".join(b.block + "\n" for b in new_blocks)
    head = xml_text[:idx].rstrip("\n") + "\n\n"
    tail = xml_text[idx:]
    return head + insertion + tail, new_blocks
