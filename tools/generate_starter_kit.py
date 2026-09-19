#!/usr/bin/env python3
"""Author the player's starter kit: a weak `starter_<donor>` twin of every item a player-start
roster hands out, same meshes, stats floored to a per-class anchor.

WHY
---
The kit a new character gets at character creation is built from two roster layers
(`player_char_creation_{culture}_{title}_{m|f}` and `player_career_{culture}_{archetype}_{m|f}`)
and, until this tool, those rosters named the culture's REAL weapons: the Gondor starter sword
swung above the one-handed-sword median, the Rivendell and Harad starters sat near the top of
the whole game, career bows ran 83 to 99 damage against vanilla's hunting bow at 40. Troops
must keep the real items, so the fix is a duplicate per donor with much lower XML stats.

WHAT IT WRITES (live Armory and, when present, the lotraom-assets mirror)
-------------------------------------------------------------------------
  LOTRLOME_items/<folder>/starter_kit.xml   one generated file per donor culture folder
  LOTRLOME_crafting_pieces.xml              one marker block of starter blade pieces
  weapon_descriptions.xslt                  a marker block per description the donor blade is in
  crafting_templates.xslt                   a marker block per template the donor blade is in

A crafted weapon's damage is the geometry magnitude times the Blade piece's damage_factor
(Crafting.cs 134-135, 363-381), and the clone keeps the donor's pieces, weight and length, so
its damage is exactly donor x (new factor / old factor). Only the blade is cloned; the donor's
guard, handle and pommel are reused (pieces resolve from the global registry, ItemObject.cs
425-470). The starter blade is `tier="1"` and `is_hidden="true"`: hidden only from the smithing
designer, so it must still be registered in every stylesheet block the donor blade appears in,
which this tool derives by reading the Armory stylesheets AND Native's base files (a vanilla
donor's blade is registered only in Native).

Plain items (bows, arrows, shields, armour) carry literal numbers, which are min()-ed against
the anchor. Every clone ships `is_merchandise="false"` (out of loot, shops, workshops and
tournament prizes; the player can still sell it), `difficulty="0"`, and no `value=`.

RULES THIS TOOL FOLLOWS (tools/README.md "XML I/O convention")
---------------------------------------------------------------
binary round-trip with the BOM re-prepended, the file's MAJORITY newline for inserted lines,
a `.bak-starterkit` sidecar taken once (never a `.xml` extension: the item folders are globbed),
a re-parse before every write, dry-run by default, idempotent on re-run, `--revert` exact.

A NEW item file loads only at process launch: after `--apply`, restart Bannerlord fully and
start a new campaign before believing anything (docs/features/starting-equipment-tuning.md).

USAGE
-----
    python tools/generate_starter_kit.py                 # dry run: the full report
    python tools/generate_starter_kit.py --apply         # write to the Armory (+ asset repo)
    python tools/generate_starter_kit.py --verify        # exit 1 if any registration drifted
    python tools/generate_starter_kit.py --revert        # remove every generated block and file
"""
from __future__ import annotations

import argparse
import copy
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _gamedir import ASSET_REPO, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_MODULES = Path(DEFAULT_GAME) / "Modules"
DEFAULT_ARMORY = DEFAULT_MODULES / "LOTRLOME_Armory"
DEFAULT_ASSET_REPO = ASSET_REPO
DEFAULT_MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
# Not the career file: since #629 its rosters name the gear each culture's lowest troops carry,
# and cloning those would author twins nobody asked for. Its old twins are kept by
# RETIRED_DONORS.
DEFAULT_ROSTERS = [
    DEFAULT_MODULEDATA / "equipmentsets" / "taom_char_creation_equipment.xml",
]
# Donors whose twins no roster names since #629 (the pre-#629 career kits). Saves started since
# #569 hold these ids, so they stay planned whatever the install holds, in the order the old
# plan wrote them (so the list never reorders a file on its own). Remove one only with a
# save-compat decision.
RETIRED_DONORS: tuple[str, ...] = (
    "wm_gondor_bow", "bodkin_arrows_a", "wm_gondor_spear_a", "battered_kite_shield",
    "wm_isengard_bow_a01", "empire_sword_1_t2", "wm_isengard_shield_a01", "wm_gundabad_shield_a01",
    "wm_dol_goldur_axe_a01", "wm_mirkwood_bow_a01", "wm_rohan_ws_bow_starter", "wm_rohan_ws_sword_a01",
    "wm_rohan_ws_spear_a01", "dunland_caerdh_spear_a", "wm_harad_bow_a01", "wm_harad_sword_a01",
    "wm_harad_spear_a01", "northern_spear_1_t2",
)

MARKER_START = "<!-- TAOM-STARTER-KIT:START -->"
MARKER_END = "<!-- TAOM-STARTER-KIT:END -->"
BACKUP_SUFFIX = ".bak-starterkit"
BACKUP_TAG = "starterkit"
ITEMS_FILE_NAME = "starter_kit.xml"
STARTER_PREFIX = "starter_"
PIECES_FILE = "LOTRLOME_crafting_pieces.xml"
XSLT_FILES = {"WeaponDescription": "weapon_descriptions.xslt", "CraftingTemplate": "crafting_templates.xslt"}
# kind -> (list element, row element, row attribute)
XSLT_SHAPE = {
    "WeaponDescription": ("AvailablePieces", "AvailablePiece", "id"),
    "CraftingTemplate": ("UsablePieces", "UsablePiece", "piece_id"),
}

PLAYER_SLOTS = frozenset({"Item0", "Item1", "Item2", "Item3", "Body", "Leg", "Cape", "Head", "Gloves"})
TARGET_PREFIXES = ("player_char_creation_", "player_career_")
EXCLUDED_ROSTER_MARKERS = ("_childhood_age_", "_education_age_", "_show_")

ARMOUR_STATS = ("head_armor", "body_armor", "leg_armor", "arm_armor")

# The floors. A value is min(anchor, donor); a stat the donor does not carry is never added.
# Crafted classes are keyed by crafting_template and hold damage_factor floors (proportional:
# the finished damage is donor x new/old, so the floors sit at or below each class's p5 across
# TAOM + vanilla, measured 2026-09-12). Plain weapons are keyed by weapon_class and armour by
# Type; those numbers are the literal stats. A class with no row aborts the run.
ANCHORS: dict[str, dict[str, float]] = {
    "OneHandedSword": {"swing": 2.5, "thrust": 1.7},
    "TwoHandedSword": {"swing": 2.8, "thrust": 2.0},
    "OneHandedAxe": {"swing": 2.3},
    "TwoHandedAxe": {"swing": 2.3},
    "Mace": {"swing": 1.9},
    "TwoHandedMace": {"swing": 2.0},
    "TwoHandedPolearm": {"thrust": 1.4, "swing": 1.4},
    "Pike": {"thrust": 1.4, "swing": 1.4},
    "Javelin": {"thrust": 1.7},
    "Dagger": {"swing": 2.0, "thrust": 1.9},
    "ThrowingAxe": {"swing": 2.0},
    "ThrowingKnife": {"thrust": 1.5},
    "Bow": {"thrust_damage": 45},
    "Crossbow": {"thrust_damage": 55},
    "Arrow": {"thrust_damage": 1},
    "Bolt": {"thrust_damage": 1},
    "LargeShield": {"hit_points": 220, "body_armor": 1},
    "SmallShield": {"hit_points": 220, "body_armor": 1},
    "BodyArmor": {"body_armor": 9, "arm_armor": 4},
    "LegArmor": {"leg_armor": 9},
    "HeadArmor": {"head_armor": 9},
    "HandArmor": {"arm_armor": 6},
    "Cape": {"body_armor": 6, "arm_armor": 2},
}

# Donor culture -> Armory item folder. A culture listed here whose folder is not registered in
# the Armory's SubModule.xml is an error (a file there never loads); a culture NOT listed falls
# back to the folder the donor's own definition lives in, then to `mercenary`.
CULTURE_TO_FOLDER = {
    "gondor": "gondor", "mordor": "mordor", "erebor": "erebor", "rivendell": "rivendell",
    "mirkwood": "mirkwood", "isengard": "isengard", "gundabad": "gundabad", "dolguldur": "dol_guldur",
    "lothlorien": "rivendell", "lindon": "rivendell",
    "vlandia": "rohan", "empire": "dunland", "aserai": "harad", "khuzait": "rhun", "sturgia": "dale",
    "battania": "rhun",
}
FALLBACK_FOLDER = "mercenary"
# Explicit price on every crafted starter clone; see clone_crafted_item for the formula that
# makes a computed price useless here. 0 or None leaves the engine to compute it.
CRAFTED_STARTER_VALUE = 150


class StarterKitError(Exception):
    """A condition the run must not paper over: a missing donor, a class with no anchor,
    a folder the engine never loads, a blade registered nowhere."""


# --------------------------------------------------------------------------- #
# Byte-faithful file I/O                                                        #
# --------------------------------------------------------------------------- #
def read_xml(path: Path) -> tuple[str, bool]:
    """(text, had_bom). Never a plain text read: that strips the BOM and normalises CRLF."""
    raw = Path(path).read_bytes()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    return raw.decode("utf-8-sig" if had_bom else "utf-8"), had_bom


def write_xml(path: Path, text: str, had_bom: bool) -> None:
    prefix = b"\xef\xbb\xbf" if had_bom else b""
    Path(path).write_bytes(prefix + text.encode("utf-8"))


def dominant_newline(text: str) -> str:
    """The MAJORITY line ending, not one that merely occurs; Armory files are genuinely mixed."""
    crlf = text.count("\r\n")
    bare_lf = text.count("\n") - crlf
    return "\r\n" if crlf > bare_lf else "\n"


def checked_write(path: Path, text: str, had_bom: bool, tag: str, backup: bool = True) -> str | None:
    """Parse, back up once, write. Returns an error string (and writes nothing) on bad XML.
    `backup=False` for files that are ours to regenerate or that live in a git repo."""
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as exc:
        return f"refusing to write {path}: transform produced malformed XML: {exc}"
    path = Path(path)
    if backup and path.exists():
        backup = path.with_name(path.name + f".bak-{tag}")
        if not backup.exists():
            backup.write_bytes(path.read_bytes())
    write_xml(path, text, had_bom)
    return None


# --------------------------------------------------------------------------- #
# Rosters                                                                       #
# --------------------------------------------------------------------------- #
def target_roster_ids(root: ET.Element) -> list[str]:
    """The player's own rosters: the adult title rosters and the career rosters. The
    childhood/education/show rosters and the parents' rosters share the prefix and are not
    the kit the player walks out with."""
    ids = []
    for roster in root.iter("EquipmentRoster"):
        rid = roster.get("id") or ""
        if not rid.startswith(TARGET_PREFIXES):
            continue
        if any(marker in rid for marker in EXCLUDED_ROSTER_MARKERS):
            continue
        ids.append(rid)
    return ids


def resolve_donor(starter: str, items: dict) -> str | None:
    """The donor a `starter_` id was cloned from, or None for a hand-authored starter item
    (the older `starter_{archetype}_{culture}_{slot}_a` armour) that has no donor to re-clone.
    `starter_id` strips a trailing `_starter`, so both spellings are tried."""
    base = starter[len(STARTER_PREFIX):]
    # the `_starter` spelling first: `starter_gondor_steel_bow` was cloned from the Armory's
    # own `gondor_steel_bow_starter`, and the full `gondor_steel_bow` also exists
    if base + "_starter" in items:
        return base + "_starter"
    if base in items:
        return base
    return None


def collect_donors(roots: list[ET.Element], items: dict | None = None) -> "OrderedDict[str, set[str]]":
    """donor item id -> the slots it fills, over EVERY equipment set of every target roster.
    The civilian set is applied independently of the battle set, so it is rewired too.

    After `wire_starter_kit_rosters.py` has run, the rosters name the `starter_` twins, not
    the donors. With an item index those ids are mapped back to their donors in place, so a
    re-run plans the same clones in the same order; without one (the pure roster scan) they
    are skipped. A generator that could not see its donors after its own wiring step would
    plan nothing and shrink every marker block on the next apply (deep review, 2026-09-12)."""
    donors: "OrderedDict[str, set[str]]" = OrderedDict()
    for root in roots:
        wanted = set(target_roster_ids(root))
        for roster in root.iter("EquipmentRoster"):
            if roster.get("id") not in wanted:
                continue
            for eq in roster.iter("Equipment"):
                slot = eq.get("slot") or ""
                if slot not in PLAYER_SLOTS:
                    continue
                iid = (eq.get("id") or "").replace("Item.", "", 1)
                if not iid:
                    continue
                if iid.startswith(STARTER_PREFIX):
                    if items is None:
                        continue
                    iid = resolve_donor(iid, items)
                    if iid is None:
                        continue
                donors.setdefault(iid, set()).add(slot)
    return donors


# --------------------------------------------------------------------------- #
# Naming, classification, anchors, folders                                      #
# --------------------------------------------------------------------------- #
def starter_id(donor: str) -> str:
    """`starter_` + donor, with a trailing `_starter` stripped first, so the six bows that
    already carry that suffix become `starter_<bow>` rather than `starter_<bow>_starter`."""
    return STARTER_PREFIX + re.sub(r"_starter$", "", donor)


def starter_name(name: str | None, new_id: str) -> str:
    text = name or new_id
    match = re.match(r"\{=[^}]*\}(.*)", text, re.S)
    if match:
        text = match.group(1)
    return "{=%s}%s (Starter)" % (new_id, text)


def classify(item: ET.Element) -> tuple[str, str]:
    """("crafted", crafting_template) | ("weapon", weapon_class) | ("armour", Type)."""
    if item.tag == "CraftedItem":
        template = item.get("crafting_template")
        if not template:
            raise StarterKitError(f"{item.get('id')}: CraftedItem without a crafting_template")
        return "crafted", template
    weapon = item.find("ItemComponent/Weapon")
    if weapon is not None:
        wclass = weapon.get("weapon_class")
        if not wclass:
            raise StarterKitError(f"{item.get('id')}: Weapon component without a weapon_class")
        return "weapon", wclass
    armour = item.find("ItemComponent/Armor")
    if armour is not None:
        itype = item.get("Type")
        if not itype:
            raise StarterKitError(f"{item.get('id')}: Armor component on an item with no Type")
        return "armour", itype
    raise StarterKitError(f"{item.get('id')}: not a crafted weapon, a weapon or armour; no starter clone can be made")


def anchor_for(key: str) -> dict[str, float]:
    try:
        return dict(ANCHORS[key])
    except KeyError:
        raise StarterKitError(
            f"no starter anchor for class {key!r}; add a row to ANCHORS rather than letting "
            "the donor's numbers through") from None


def registered_item_folders(submodule_text: str) -> set[str]:
    """The `LOTRLOME_items/<folder>` directories the Armory registers as Items paths. The
    three `LOTRAOM_*` rows register single FILES by name (armory-guide.md), not folders."""
    names = re.findall(r'<XmlName\s+id="Items"\s+path="LOTRLOME_items/([^"/]+)"', submodule_text)
    return {n for n in names if not n.lower().startswith("lotraom_")}


def folder_for(culture: str | None, own_folder: str | None, registered: set[str]) -> str:
    cid = culture.split(".", 1)[1] if culture and "." in culture else culture
    if cid and cid in CULTURE_TO_FOLDER:
        folder = CULTURE_TO_FOLDER[cid]
        if folder not in registered:
            raise StarterKitError(
                f"culture {cid!r} maps to folder {folder!r}, which the Armory's SubModule.xml does not "
                "register as an Items path; a file written there never loads")
        return folder
    if own_folder and own_folder in registered:
        return own_folder
    if FALLBACK_FOLDER in registered:
        return FALLBACK_FOLDER
    raise StarterKitError(f"no registered folder for culture {culture!r} and no {FALLBACK_FOLDER!r} fallback")


def weapon_tier(piece_tiers: list[int]) -> int:
    """int(mean) of the fitted pieces' tiers, as Crafting.cs 406-421 computes it."""
    return int(sum(piece_tiers) / len(piece_tiers))


def _floor(current: str, floor: float) -> str:
    """The donor's own string when it is already at or below the floor, else the floor."""
    try:
        value = float(current)
    except ValueError:
        return current
    return current if value <= floor else str(floor)


# --------------------------------------------------------------------------- #
# Clones                                                                        #
# --------------------------------------------------------------------------- #
def clone_plain_item(donor: ET.Element, new_id: str, floors: dict[str, float]) -> ET.Element:
    """A verbatim copy (mesh, body, holsters, Flags, AdditionalMeshes, every covers_* and
    cover-type attribute) with the identity swapped and the anchored stats floored. Armour
    stats the anchor does not name are dropped: a chest must not carry leg armour into the
    starter tier."""
    out = copy.deepcopy(donor)
    out.set("id", new_id)
    out.set("name", starter_name(donor.get("name"), new_id))
    out.set("is_merchandise", "false")
    out.set("difficulty", "0")
    out.attrib.pop("value", None)
    kind, _ = classify(out)
    if kind == "weapon":
        weapon = out.find("ItemComponent/Weapon")
        for stat, floor in floors.items():
            if stat in weapon.attrib:
                weapon.set(stat, _floor(weapon.get(stat), floor))
    elif kind == "armour":
        armour = out.find("ItemComponent/Armor")
        for stat in ARMOUR_STATS:
            if stat not in armour.attrib:
                continue
            if stat in floors:
                armour.set(stat, _floor(armour.get(stat), floors[stat]))
            else:
                del armour.attrib[stat]
    else:
        raise StarterKitError(f"{donor.get('id')}: clone_plain_item called on a crafted item")
    return out


def clone_blade(piece: ET.Element, new_id: str, floors: dict[str, float]) -> ET.Element:
    """The one new crafting piece per crafted donor: tier 1, hidden from the smithing
    designer, everything else verbatim, only the two damage factors floored."""
    out = copy.deepcopy(piece)
    out.set("id", new_id)
    out.set("name", starter_name(piece.get("name"), new_id))
    out.set("tier", "1")
    out.set("is_hidden", "true")
    for key, tag in (("swing", "Swing"), ("thrust", "Thrust")):
        node = out.find(f"BladeData/{tag}")
        if node is None or key not in floors or "damage_factor" not in node.attrib:
            continue
        node.set("damage_factor", _floor(node.get("damage_factor"), floors[key]))
    return out


def clone_crafted_item(donor: ET.Element, new_id: str, blade_id: str,
                       value: int | None = CRAFTED_STARTER_VALUE) -> ET.Element:
    """Same template, same fittings and scale factors; only the Blade piece is the starter one.

    The price is pinned rather than computed: DefaultItemValueModel prices a crafted weapon
    at 100 x 2.75^Tierf with Tierf = 0.6 x stats tier + 0.4 x crafted tier, and the crafted
    tier comes from the fitted pieces' tiers and iron grades (lines 41-49, 171-215), which
    the clone keeps. A floored blade on tier-5 elven fittings would still price in the
    thousands, which is the resale problem starting-equipment-tuning.md exists to stop."""
    out = copy.deepcopy(donor)
    out.set("id", new_id)
    out.set("name", starter_name(donor.get("name"), new_id))
    out.set("is_merchandise", "false")
    out.attrib.pop("value", None)
    if value:
        out.set("value", str(value))
    swapped = 0
    for piece in out.iter("Piece"):
        if piece.get("Type") == "Blade":
            piece.set("id", blade_id)
            swapped += 1
    if swapped != 1:
        raise StarterKitError(f"{donor.get('id')}: expected exactly one Blade piece, found {swapped}")
    return out


# --------------------------------------------------------------------------- #
# Stylesheets                                                                   #
# --------------------------------------------------------------------------- #
def _template_re(kind: str, target_id: str | None = None) -> re.Pattern:
    list_elem = XSLT_SHAPE[kind][0]
    ident = re.escape(target_id) if target_id else r"[^']+"
    return re.compile(
        r"(<xsl:template\s+match=\"" + kind + r"\[@id='(" + ident + r")'\]/" + list_elem
        + r"\"\s*>)(.*?)(</xsl:template>)", re.S)


def block_re() -> re.Pattern:
    """A marker block with its leading indent and trailing newline, so removal leaves no gap
    and an unchanged file compares equal to the wanted text."""
    return re.compile(r"[ \t]*" + re.escape(MARKER_START) + r".*?" + re.escape(MARKER_END)
                      + r"[ \t]*(?:\r\n|\n)?", re.S)


def blocks_for_piece(piece_id: str, xslt_text: str, native_root: ET.Element | None, kind: str) -> list[str]:
    """Every description (or template) id the donor blade is registered under: the Armory's
    override blocks first, in file order, then Native's base file. De-duplicated, because a
    blade can be listed twice inside one block."""
    list_elem, row_elem, attr = XSLT_SHAPE[kind]
    found: list[str] = []
    needle = f'"{piece_id}"'
    for match in _template_re(kind).finditer(xslt_text):
        body = match.group(3)
        # only rows outside our own marker blocks count as the donor's registrations
        body = block_re().sub("", body)
        if needle in body and match.group(2) not in found:
            found.append(match.group(2))
    if native_root is not None:
        for node in native_root.iter(kind):
            nid = node.get("id")
            if not nid or nid in found:
                continue
            for row in node.iter(row_elem):
                if row.get(attr) == piece_id:
                    found.append(nid)
                    break
    return found


def _rows(kind: str, ids: list[str], indent: str, eol: str) -> str:
    _, row_elem, attr = XSLT_SHAPE[kind]
    return "".join(f'{indent}<{row_elem} {attr}="{i}"/>{eol}' for i in ids)


def _created_block(kind: str, target_id: str, ids: list[str], indent: str, eol: str) -> str:
    list_elem = XSLT_SHAPE[kind][0]
    lines = [
        f"{indent}{MARKER_START}",
        f"{indent}<xsl:template match=\"{kind}[@id='{target_id}']/{list_elem}\">",
        f"{indent}\t<{list_elem}>",
    ]
    lines += [line for line in _rows(kind, ids, indent + "\t\t", eol).split(eol) if line]
    lines += [
        f"{indent}\t\t<xsl:apply-templates select=\"@*|node()\"/>",
        f"{indent}\t</{list_elem}>",
        f"{indent}</xsl:template>",
        f"{indent}{MARKER_END}",
    ]
    return eol.join(lines) + eol


def _find_created_block(text: str, kind: str, target_id: str) -> re.Match | None:
    list_elem = XSLT_SHAPE[kind][0]
    signature = f"match=\"{kind}[@id='{target_id}']/{list_elem}\""
    for match in block_re().finditer(text):
        if signature in match.group(0):
            return match
    return None


def apply_xslt(text: str, kind: str, targets: dict[str, list[str]]) -> tuple[str, dict[str, str]]:
    """Register `targets[id]` under description/template `id`. Inside an existing Armory
    template the rows go in a marker block before the list's close tag (after the
    passthrough; membership is all Crafting.cs tests). Where the Armory has no template for
    that id, a whole marker-wrapped template is created before `</xsl:stylesheet>`, passthrough
    included so vanilla's rows survive. Returns (text, {id: inserted|updated|created|noop})."""
    eol = dominant_newline(text)
    list_elem, row_elem, _ = XSLT_SHAPE[kind]
    actions: dict[str, str] = {}
    for target_id, ids in targets.items():
        ids = list(dict.fromkeys(ids))
        match = _template_re(kind, target_id).search(text)
        created = _find_created_block(text, kind, target_id)
        if match and created and created.start() <= match.start() < created.end():
            # the only template for this id is the one an earlier run created: keep it on
            # the created-block path rather than treating our own block as the Armory's
            match = None
        if match:
            # a stale created block for the same id (the Armory grew a template since) is removed
            if created:
                text = text[:created.start()] + text[created.end():]
                match = _template_re(kind, target_id).search(text)
            head, body, tail = match.group(1), match.group(3), match.group(4)
            indent_match = re.search(r"\n([ \t]*)<" + row_elem + r"\b", body)
            indent = indent_match.group(1) if indent_match else "\t\t\t"
            wanted = f"{indent}{MARKER_START}{eol}" + _rows(kind, ids, indent, eol) + f"{indent}{MARKER_END}{eol}"
            existing = block_re().search(body)
            if existing:
                if existing.group(0) == wanted:
                    actions[target_id] = "noop"
                    continue
                new_body = body[:existing.start()] + wanted + body[existing.end():]
                actions[target_id] = "updated"
            else:
                close = body.rfind(f"</{list_elem}>")
                if close == -1:
                    raise StarterKitError(f"{kind} {target_id}: template has no </{list_elem}> close tag")
                line_start = body.rfind("\n", 0, close) + 1
                new_body = body[:line_start] + wanted + body[line_start:]
                actions[target_id] = "inserted"
            text = text[:match.start(3)] + new_body + text[match.end(3):]
            continue
        indent_match = re.search(r"(?:^|\n)([ \t]*)<xsl:template\b", text)
        indent = indent_match.group(1) if indent_match else "\t"
        wanted = _created_block(kind, target_id, ids, indent, eol)
        existing = _find_created_block(text, kind, target_id)
        if existing:
            if existing.group(0) == wanted:
                actions[target_id] = "noop"
                continue
            text = text[:existing.start()] + wanted + text[existing.end():]
            actions[target_id] = "updated"
        else:
            close = text.rfind("</xsl:stylesheet>")
            if close == -1:
                raise StarterKitError(f"{kind}: stylesheet has no </xsl:stylesheet> close tag")
            line_start = text.rfind("\n", 0, close) + 1
            text = text[:line_start] + wanted + text[line_start:]
            actions[target_id] = "created"
    return text, actions


def revert_xslt(text: str) -> tuple[str, int]:
    return block_re().subn("", text)


# --------------------------------------------------------------------------- #
# Crafting pieces file and item files                                           #
# --------------------------------------------------------------------------- #
def _serialize(element: ET.Element, indent: str, eol: str, level: int = 1) -> str:
    """Pretty-printed XML for one element at `level`, newlines normalised to `eol`."""
    node = copy.deepcopy(element)
    node.tail = None
    ET.indent(node, space=indent, level=level)
    text = ET.tostring(node, encoding="unicode")
    return (indent * level) + text.replace("\r\n", "\n").replace("\n", eol) + eol


def render_pieces_block(pieces: list[ET.Element], eol: str, indent: str = "    ") -> str:
    lines = f"{indent}{MARKER_START}{eol}"
    for piece in pieces:
        lines += _serialize(piece, indent, eol, level=1)
    lines += f"{indent}{MARKER_END}{eol}"
    return lines


def apply_pieces(text: str, pieces: list[ET.Element]) -> tuple[str, str]:
    """Returns (text, inserted|updated|noop). The block sits just before `</CraftingPieces>`."""
    eol = dominant_newline(text)
    wanted = render_pieces_block(pieces, eol)
    existing = block_re().search(text)
    if existing:
        if existing.group(0) == wanted:
            return text, "noop"
        return text[:existing.start()] + wanted + text[existing.end():], "updated"
    close = text.rfind("</CraftingPieces>")
    if close == -1:
        raise StarterKitError("crafting pieces file has no </CraftingPieces> close tag")
    line_start = text.rfind("\n", 0, close) + 1
    return text[:line_start] + wanted + text[line_start:], "inserted"


def revert_pieces(text: str) -> tuple[str, bool]:
    new_text, count = block_re().subn("", text)
    return new_text, bool(count)


def render_items_file(clones: list["Clone"], folder: str, eol: str = "\r\n") -> str:
    donors = ", ".join(c.donor_id for c in clones)
    header = (
        f'<?xml version="1.0" encoding="utf-8"?>{eol}'
        f"<!--{eol}"
        f"  TAOM player starter kit for the {folder} folder. GENERATED by{eol}"
        f"  tools/generate_starter_kit.py (do not hand-edit; re-run the generator).{eol}"
        f"  Each item is a stat-floored twin of a player-start roster item, same meshes,{eol}"
        f"  is_merchandise=false; crafted twins carry an explicit value=, plain items compute{eol}"
        f"  theirs from the floored stats. Donors: {donors}{eol}"
        f"-->{eol}"
        f"<Items>{eol}"
    )
    body = "".join(_serialize(c.element, "    ", eol, level=1) for c in clones)
    return header + body + f"</Items>{eol}"


# --------------------------------------------------------------------------- #
# Indexes                                                                       #
# --------------------------------------------------------------------------- #
@dataclass
class ItemRec:
    element: ET.Element
    path: Path
    own_folder: str | None


def index_items(files: list[Path], items_root: Path | None = None,
                failures: list[str] | None = None) -> dict[str, ItemRec]:
    """id -> definition over every item file. Our own generated files are skipped so a re-run
    does not see its previous output as a collision. A file that does not parse is recorded
    in `failures` (never silently dropped: its items would then read as missing donors)."""
    index: dict[str, ItemRec] = {}
    for path in files:
        path = Path(path)
        if path.name == ITEMS_FILE_NAME:
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as exc:
            if failures is not None:
                failures.append(f"{path}: not well-formed, its items are invisible to this run ({exc})")
            continue
        own_folder = None
        if items_root is not None:
            try:
                rel = path.resolve().relative_to(items_root.resolve())
                if len(rel.parts) > 1:
                    own_folder = rel.parts[0]
            except ValueError:
                own_folder = None
        for node in root.iter():
            if node.tag in ("Item", "CraftedItem") and node.get("id"):
                index.setdefault(node.get("id"), ItemRec(node, path, own_folder))
    return index


def index_pieces(files: list[Path]) -> dict[str, ET.Element]:
    index: dict[str, ET.Element] = {}
    for path in files:
        if path is None or not Path(path).exists():
            continue
        root = ET.parse(path).getroot()
        for node in root.iter("CraftingPiece"):
            pid = node.get("id")
            if pid and not pid.startswith(STARTER_PREFIX):
                index.setdefault(pid, node)
    return index


def _blade_factors(item: ET.Element, pieces: dict[str, ET.Element]) -> dict[str, float]:
    for piece in item.iter("Piece"):
        if piece.get("Type") == "Blade":
            blade = pieces.get(piece.get("id"))
            if blade is None:
                return {}
            out = {}
            for key, tag in (("swing", "Swing"), ("thrust", "Thrust")):
                node = blade.find(f"BladeData/{tag}")
                if node is not None and node.get("damage_factor"):
                    try:
                        out[key] = float(node.get("damage_factor"))
                    except ValueError:
                        pass
            return out
    return {}


def factor_distribution(items: dict[str, ItemRec], pieces: dict[str, ET.Element]) -> dict[str, dict[str, list[float]]]:
    """crafting_template -> {swing: sorted factors, thrust: sorted factors} over every crafted
    item in the index, so a clone's percentile within its class can be printed."""
    dist: dict[str, dict[str, list[float]]] = {}
    for iid, rec in items.items():
        if rec.element.tag != "CraftedItem" or iid.startswith(STARTER_PREFIX):
            continue
        template = rec.element.get("crafting_template")
        for key, value in _blade_factors(rec.element, pieces).items():
            dist.setdefault(template, {}).setdefault(key, []).append(value)
    for template in dist.values():
        for values in template.values():
            values.sort()
    return dist


def percentile(values: list[float], value: float) -> float | None:
    if not values:
        return None
    return 100.0 * sum(1 for v in values if v <= value) / len(values)


# --------------------------------------------------------------------------- #
# The plan                                                                      #
# --------------------------------------------------------------------------- #
@dataclass
class Sources:
    rosters: list[Path]
    armory: Path
    vanilla_item_files: list[Path] = field(default_factory=list)
    native_pieces: Path | None = None
    native_descriptions: Path | None = None
    native_templates: Path | None = None
    menus_dir: Path | None = None
    crafted_value: int | None = CRAFTED_STARTER_VALUE


@dataclass
class Clone:
    donor_id: str
    new_id: str
    kind: str
    key: str
    folder: str
    element: ET.Element
    slots: set[str]
    blade: ET.Element | None = None
    tier: int | None = None
    ratios: dict[str, float] = field(default_factory=dict)
    stats: dict[str, tuple[str, str]] = field(default_factory=dict)
    percentiles: dict[str, float | None] = field(default_factory=dict)


@dataclass
class Plan:
    clones: list[Clone]
    pieces: list[ET.Element]
    registrations: dict[str, dict[str, list[str]]]
    gaps: list[str] = field(default_factory=list)
    notes: list[str] = field(default_factory=list)

    def by_folder(self) -> "OrderedDict[str, list[Clone]]":
        out: "OrderedDict[str, list[Clone]]" = OrderedDict()
        for clone in self.clones:
            out.setdefault(clone.folder, []).append(clone)
        return out


def _armory_item_files(armory_md: Path) -> list[Path]:
    root = armory_md / "LOTRLOME_items"
    return sorted(Path(p) for p in glob.glob(str(root / "**" / "*.xml"), recursive=True))


def expected_roster_gaps(menus_dir: Path, present: set[str]) -> list[str]:
    """Roster ids cultures.json x youth_menu.json imply but the roster files lack, and
    cultures with rosters but no youth titles. Informational: the C# coverage test owns the
    first half, and the second half is the shaghana/abanissa anomaly (#570)."""
    cultures_path = menus_dir / "cultures.json"
    youth_path = menus_dir / "youth_menu.json"
    if not cultures_path.exists() or not youth_path.exists():
        return []
    cultures = [c.get("culture_id") for c in json.loads(cultures_path.read_text(encoding="utf-8")) if c.get("culture_id")]
    youth = json.loads(youth_path.read_text(encoding="utf-8"))
    titles: dict[str, set[str]] = {}
    for opt in youth:
        if opt.get("culture_id") and opt.get("title_type"):
            titles.setdefault(opt["culture_id"], set()).add(opt["title_type"])
    gaps = []
    for culture in cultures:
        if culture not in titles:
            if any(r.startswith(f"player_char_creation_{culture}_") for r in present):
                gaps.append(f"{culture}: has player_char_creation rosters but offers no youth title (#570)")
            continue
        for title in sorted(titles[culture]):
            for sex in ("m", "f"):
                rid = f"player_char_creation_{culture}_{title}_{sex}"
                if rid not in present:
                    gaps.append(f"{rid}: implied by youth_menu.json, not in the roster files")
    return gaps


def retained_donors(items: dict, planned: "OrderedDict[str, set[str]]") -> list[str]:
    """The RETIRED_DONORS no roster names, in list order. A committed list rather than a scan
    of the install, so a reinstall that wiped starter_kit.xml still restores them. A retired
    donor the item index cannot resolve is an error, never a silent drop."""
    missing = [d for d in RETIRED_DONORS if d not in items]
    if missing:
        raise StarterKitError(f"RETIRED_DONORS no longer defined anywhere: {missing}; restore the item "
                              "or make a save-compat decision before removing it from the list")
    return [d for d in RETIRED_DONORS if d not in planned]


def on_disk_folders(items_root: Path) -> dict[str, str]:
    """starter id -> the folder its twin already sits in. A twin stays where it is when its
    donor's culture later changes (vanilla 1.5.3 gave battered_kite_shield culture=empire),
    because a moved id would leave a duplicate or an orphan behind."""
    found: dict[str, str] = {}
    if not items_root.exists():
        return found
    for path in sorted(items_root.glob(f"*/{ITEMS_FILE_NAME}")):
        folder = path.parent.name
        for starter in _starter_ids_in(read_xml(path)[0]):
            if found.get(starter, folder) != folder:
                raise StarterKitError(f"{starter} is defined in two folders ({found[starter]}, {folder}); "
                                      "delete one before re-planning")
            found[starter] = folder
    return found


def build_plan(sources: Sources) -> Plan:
    armory_md = Path(sources.armory) / "ModuleData"
    roots = [ET.parse(p).getroot() for p in sources.rosters]
    present = {rid for root in roots for rid in target_roster_ids(root)}

    items_root = armory_md / "LOTRLOME_items"
    notes: list[str] = []
    items = index_items(_armory_item_files(armory_md) + [Path(p) for p in sources.vanilla_item_files],
                        items_root, failures=notes)
    donors = collect_donors(roots, items)
    for donor_id in retained_donors(items, donors):
        donors[donor_id] = set()
    kept_in = on_disk_folders(items_root)
    pieces = index_pieces([armory_md / PIECES_FILE, sources.native_pieces])
    submodule = Path(sources.armory) / "SubModule.xml"
    registered = registered_item_folders(submodule.read_text(encoding="utf-8-sig")) if submodule.exists() else set()
    if not registered:
        raise StarterKitError(f"no `LOTRLOME_items/<folder>` Items registrations found in {submodule}")

    xslt_text: dict[str, str] = {}
    for kind, name in XSLT_FILES.items():
        path = armory_md / name
        xslt_text[kind] = read_xml(path)[0] if path.exists() else ""
    native_roots: dict[str, ET.Element | None] = {
        "WeaponDescription": ET.parse(sources.native_descriptions).getroot()
        if sources.native_descriptions and Path(sources.native_descriptions).exists() else None,
        "CraftingTemplate": ET.parse(sources.native_templates).getroot()
        if sources.native_templates and Path(sources.native_templates).exists() else None,
    }
    distribution = factor_distribution(items, pieces)

    clones: list[Clone] = []
    blades: "OrderedDict[str, ET.Element]" = OrderedDict()
    registrations: dict[str, dict[str, list[str]]] = {kind: OrderedDict() for kind in XSLT_FILES}

    for donor_id, slots in donors.items():
        rec = items.get(donor_id)
        if rec is None:
            raise StarterKitError(
                f"roster item {donor_id!r} (slots {sorted(slots)}) is not defined in the Armory or the "
                "vanilla item files; repoint the roster or restore the item before cloning")
        kind, key = classify(rec.element)
        floors = anchor_for(key)
        new_id = starter_id(donor_id)
        if new_id in items:
            raise StarterKitError(f"{new_id} already exists as an item; the id scheme collided on {donor_id}")
        folder = folder_for(rec.element.get("culture"), rec.own_folder, registered)
        if new_id in kept_in and kept_in[new_id] != folder:
            if kept_in[new_id] not in registered:
                raise StarterKitError(f"{new_id} sits in {kept_in[new_id]!r}, which the Armory's SubModule.xml "
                                      "does not register as an Items path")
            notes.append(f"{new_id}: kept in {kept_in[new_id]} (its donor's culture now maps to {folder})")
            folder = kept_in[new_id]
        if kind == "crafted":
            blade_piece_id = next((p.get("id") for p in rec.element.iter("Piece") if p.get("Type") == "Blade"), None)
            if not blade_piece_id or blade_piece_id not in pieces:
                raise StarterKitError(f"{donor_id}: Blade piece {blade_piece_id!r} is not defined in any crafting pieces file")
            blade_new_id = starter_id(blade_piece_id)
            blade = blades.get(blade_new_id) or clone_blade(pieces[blade_piece_id], blade_new_id, floors)
            blades[blade_new_id] = blade
            element = clone_crafted_item(rec.element, new_id, blade_new_id, sources.crafted_value)
            fitted = []
            for piece in rec.element.iter("Piece"):
                if piece.get("Type") == "Blade":
                    fitted.append(1)
                else:
                    other = pieces.get(piece.get("id"))
                    fitted.append(int(other.get("tier", "1")) if other is not None else 1)
            tier = weapon_tier(fitted)
            if tier > 1:
                notes.append(f"{new_id}: fitted-piece tiers {fitted} give weapon tier {tier}; price scales with it")
            old = _blade_factors(rec.element, pieces)
            ratios, stats, pct = {}, {}, {}
            for stat_key, tag in (("swing", "Swing"), ("thrust", "Thrust")):
                node = blade.find(f"BladeData/{tag}")
                if node is None or stat_key not in old:
                    continue
                new_value = float(node.get("damage_factor"))
                ratios[stat_key] = new_value / old[stat_key] if old[stat_key] else 1.0
                stats[stat_key] = (str(old[stat_key]), node.get("damage_factor"))
                pct[stat_key] = percentile(distribution.get(key, {}).get(stat_key, []), new_value)
            for reg_kind in XSLT_FILES:
                targets = blocks_for_piece(blade_piece_id, xslt_text[reg_kind], native_roots[reg_kind], reg_kind)
                if not targets:
                    raise StarterKitError(
                        f"{donor_id}: blade {blade_piece_id} is registered under no {reg_kind}; the donor itself "
                        "cannot be resolving in game, fix that first")
                for target in targets:
                    rows = registrations[reg_kind].setdefault(target, [])
                    if blade_new_id not in rows:
                        rows.append(blade_new_id)
            clones.append(Clone(donor_id, new_id, kind, key, folder, element, slots, blade, tier, ratios, stats, pct))
        else:
            element = clone_plain_item(rec.element, new_id, floors)
            stats = {}
            src = rec.element.find("ItemComponent/Weapon" if kind == "weapon" else "ItemComponent/Armor")
            dst = element.find("ItemComponent/Weapon" if kind == "weapon" else "ItemComponent/Armor")
            for stat in (floors if kind == "weapon" else ARMOUR_STATS):
                if stat in src.attrib:
                    stats[stat] = (src.get(stat), dst.get(stat) if stat in dst.attrib else "(dropped)")
            clones.append(Clone(donor_id, new_id, kind, key, folder, element, slots, None, None, {}, stats, {}))

    gaps = expected_roster_gaps(sources.menus_dir, present) if sources.menus_dir else []
    return Plan(clones, list(blades.values()), registrations, gaps, notes)


# --------------------------------------------------------------------------- #
# Apply, verify, revert                                                         #
# --------------------------------------------------------------------------- #
def _items_path(md: Path, folder: str) -> Path:
    return md / "LOTRLOME_items" / folder / ITEMS_FILE_NAME


_ID_RE = re.compile(r'<(?:Item|CraftedItem|CraftingPiece|AvailablePiece|UsablePiece)\b[^>]*?\b(?:id|piece_id)="(starter_[^"]+)"')


def _starter_ids_in(text: str) -> set[str]:
    return set(_ID_RE.findall(text))


def _refuse_shrink(what: str, on_disk: set[str], planned: set[str], allow: bool) -> None:
    """A plan that drops starter ids already on disk is almost always a run that could not
    see its donors, not a decision; make it say so explicitly."""
    lost = sorted(on_disk - planned)
    if lost and not allow:
        raise StarterKitError(
            f"{what}: applying would shrink the starter set by {len(lost)} id(s) already on disk "
            f"({', '.join(lost[:5])}{', ...' if len(lost) > 5 else ''}). No roster names these and "
            "RETIRED_DONORS does not keep them: add the donor there to keep a twin old saves may hold, "
            "or re-run with --allow-shrink to drop it")


def apply_plan(plan: Plan, md: Path, write: bool, backups: bool = True, allow_shrink: bool = False) -> list[str]:
    """Write the plan into one ModuleData tree. Returns log lines; raises on malformed output
    and on a plan that would drop starter content already on disk (unless `allow_shrink`).
    The generated item files never get a sidecar (they are ours to regenerate); the three
    shared files get one when `backups` is set, which main() reserves for the live install
    (the asset-repo mirror has git)."""
    log: list[str] = []
    planned_by_folder = plan.by_folder()
    for existing in sorted((md / "LOTRLOME_items").glob(f"*/{ITEMS_FILE_NAME}")) if (md / "LOTRLOME_items").exists() else []:
        folder = existing.parent.name
        planned = {c.new_id for c in planned_by_folder.get(folder, [])}
        _refuse_shrink(f"{existing}", _starter_ids_in(read_xml(existing)[0]), planned, allow_shrink)
    pieces_path = md / PIECES_FILE
    if pieces_path.exists():
        block = block_re().search(read_xml(pieces_path)[0])
        _refuse_shrink(str(pieces_path), _starter_ids_in(block.group(0)) if block else set(),
                       {p.get("id") for p in plan.pieces}, allow_shrink)
    for kind, name in XSLT_FILES.items():
        path = md / name
        if path.exists():
            # per template, not per file: a blade that stays registered under one description
            # but loses another would otherwise pass a whole-file comparison
            text = read_xml(path)[0]
            for match in _template_re(kind).finditer(text):
                target = match.group(2)
                _refuse_shrink(f"{path} {kind}[{target}]", _starter_ids_in(match.group(3)),
                               set(plan.registrations[kind].get(target, [])), allow_shrink)

    for folder, clones in planned_by_folder.items():
        path = _items_path(md, folder)
        text = render_items_file(clones, folder)
        if path.exists() and path.read_bytes() == text.encode("utf-8"):
            log.append(f"noop     {path} ({len(clones)} items)")
            continue
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            err = checked_write(path, text, False, BACKUP_TAG, backup=False)
            if err:
                raise StarterKitError(err)
        log.append(f"{'wrote' if write else 'would write':8s} {path} ({len(clones)} items)")

    pieces_path = md / PIECES_FILE
    text, had_bom = read_xml(pieces_path)
    new_text, action = apply_pieces(text, plan.pieces)
    if action != "noop" and write:
        err = checked_write(pieces_path, new_text, had_bom, BACKUP_TAG, backup=backups)
        if err:
            raise StarterKitError(err)
    log.append(f"{action:8s} {pieces_path} ({len(plan.pieces)} starter blades)")

    for kind, name in XSLT_FILES.items():
        path = md / name
        text, had_bom = read_xml(path)
        new_text, actions = apply_xslt(text, kind, plan.registrations[kind])
        if any(a != "noop" for a in actions.values()) and write:
            err = checked_write(path, new_text, had_bom, BACKUP_TAG, backup=backups)
            if err:
                raise StarterKitError(err)
        for target, action in actions.items():
            log.append(f"{action:8s} {path.name} {kind}[{target}] ({len(plan.registrations[kind][target])} rows)")
    return log


def verify_plan(plan: Plan, md: Path) -> list[str]:
    """Drift between the plan and one ModuleData tree: missing files, items, pieces, rows."""
    drift: list[str] = []
    for folder, clones in plan.by_folder().items():
        path = _items_path(md, folder)
        if not path.exists():
            drift.append(f"missing file {path}")
            continue
        text = read_xml(path)[0]
        for clone in clones:
            if f'id="{clone.new_id}"' not in text:
                drift.append(f"{path.name} ({folder}): item {clone.new_id} missing")
    pieces_path = md / PIECES_FILE
    if not pieces_path.exists():
        drift.append(f"missing file {pieces_path}")
    else:
        text = read_xml(pieces_path)[0]
        for piece in plan.pieces:
            if f'id="{piece.get("id")}"' not in text:
                drift.append(f"{PIECES_FILE}: piece {piece.get('id')} missing")
    for kind, name in XSLT_FILES.items():
        path = md / name
        if not path.exists():
            drift.append(f"missing file {path}")
            continue
        text = read_xml(path)[0]
        for target, rows in plan.registrations[kind].items():
            match = _template_re(kind, target).search(text)
            body = match.group(3) if match else ""
            for row in rows:
                if f'"{row}"' not in body:
                    drift.append(f"{name}: {kind}[{target}] lacks {row}")
    return drift


def revert_plan(plan: Plan, md: Path, write: bool, backups: bool = True) -> list[str]:
    log: list[str] = []
    for folder in plan.by_folder():
        path = _items_path(md, folder)
        if path.exists():
            if write:
                path.unlink()
            log.append(f"{'removed' if write else 'would remove':8s} {path}")
    pieces_path = md / PIECES_FILE
    if pieces_path.exists():
        text, had_bom = read_xml(pieces_path)
        new_text, removed = revert_pieces(text)
        if removed and write:
            err = checked_write(pieces_path, new_text, had_bom, BACKUP_TAG, backup=backups)
            if err:
                raise StarterKitError(err)
        log.append(f"{'reverted' if removed else 'noop':8s} {pieces_path}")
    for name in XSLT_FILES.values():
        path = md / name
        if not path.exists():
            continue
        text, had_bom = read_xml(path)
        new_text, count = revert_xslt(text)
        if count and write:
            err = checked_write(path, new_text, had_bom, BACKUP_TAG, backup=backups)
            if err:
                raise StarterKitError(err)
        log.append(f"{'reverted' if count else 'noop':8s} {path} ({count} blocks)")
    return log


# --------------------------------------------------------------------------- #
# Report and CLI                                                                #
# --------------------------------------------------------------------------- #
def report(plan: Plan) -> str:
    lines = []
    for clone in plan.clones:
        detail = ", ".join(
            f"{stat} {old}->{new}"
            + (f" ({clone.ratios[stat]:.0%}" + (f", p{clone.percentiles[stat]:.0f}" if clone.percentiles.get(stat) is not None else "") + ")"
               if stat in clone.ratios else "")
            for stat, (old, new) in clone.stats.items())
        tier = f" tier={clone.tier}" if clone.tier is not None else ""
        lines.append(f"  {clone.kind:7s} {clone.key:18s} {clone.donor_id:40s} -> {clone.new_id:48s} [{clone.folder}]{tier} {detail}")
    lines.append("")
    lines.append(f"{len(plan.clones)} clones ({sum(1 for c in plan.clones if c.kind == 'crafted')} crafted, "
                 f"{sum(1 for c in plan.clones if c.kind == 'weapon')} plain weapons, "
                 f"{sum(1 for c in plan.clones if c.kind == 'armour')} armour), {len(plan.pieces)} starter blades")
    for kind, regs in plan.registrations.items():
        lines.append(f"  {kind}: " + ", ".join(f"{t}({len(r)})" for t, r in regs.items()))
    for note in plan.notes:
        lines.append(f"  note: {note}")
    for gap in plan.gaps:
        lines.append(f"  gap:  {gap}")
    return "\n".join(lines)


def default_sources(args) -> Sources:
    modules = Path(args.game_modules)
    vanilla = []
    for module in ("SandBoxCore", "Native", "SandBox"):
        vanilla += [Path(p) for p in glob.glob(str(modules / module / "ModuleData" / "items" / "*.xml"))]
    native_md = modules / "Native" / "ModuleData"
    return Sources(
        rosters=[Path(r) for r in args.rosters],
        armory=Path(args.armory_path),
        vanilla_item_files=vanilla,
        native_pieces=native_md / "crafting_pieces.xml",
        native_descriptions=native_md / "weapon_descriptions.xml",
        native_templates=native_md / "crafting_templates.xml",
        menus_dir=DEFAULT_MODULEDATA / "charactercreation",
        crafted_value=args.crafted_value or None,
    )


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write (default: dry run)")
    ap.add_argument("--verify", action="store_true", help="exit 1 if the Armory has drifted from the plan")
    ap.add_argument("--revert", action="store_true", help="remove every generated block and file")
    ap.add_argument("--armory-path", default=str(DEFAULT_ARMORY))
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO), help="second Armory copy to keep in step")
    ap.add_argument("--no-asset-repo", action="store_true")
    ap.add_argument("--game-modules", default=str(DEFAULT_MODULES))
    ap.add_argument("--rosters", nargs="+", default=[str(r) for r in DEFAULT_ROSTERS])
    ap.add_argument("--quiet", action="store_true", help="skip the per-clone report")
    ap.add_argument("--crafted-value", type=int, default=CRAFTED_STARTER_VALUE,
                    help="explicit value= on crafted starter clones; 0 lets the engine price them")
    ap.add_argument("--allow-shrink", action="store_true",
                    help="let --apply drop starter ids already on disk (a roster stopped naming a donor)")
    args = ap.parse_args()

    sources = default_sources(args)
    if not Path(sources.armory).exists():
        print(f"SKIP: Armory not found at {sources.armory} (set BANNERLORD_GAME_DIR)")
        return 0
    try:
        plan = build_plan(sources)
    except StarterKitError as exc:
        print(f"ERROR: {exc}")
        return 2

    if not args.quiet:
        print(report(plan))
        print()

    targets = [Path(sources.armory) / "ModuleData"]
    if not args.no_asset_repo and Path(args.asset_repo).exists():
        targets.append(Path(args.asset_repo) / "ModuleData")
    else:
        print(f"(asset repo {args.asset_repo} not present; live Armory only)")

    rc = 0
    for md in targets:
        backups = md == targets[0]   # the live install gets sidecars; the mirror has git
        print(f"=== {md} ===")
        try:
            if args.revert:
                for line in revert_plan(plan, md, write=args.apply, backups=backups):
                    print(line)
            elif args.verify:
                drift = verify_plan(plan, md)
                for line in drift:
                    print("DRIFT:", line)
                print("OK: no drift" if not drift else f"FAIL: {len(drift)} drift line(s)")
                rc = rc or (1 if drift else 0)
            else:
                for line in apply_plan(plan, md, write=args.apply, backups=backups, allow_shrink=args.allow_shrink):
                    print(line)
        except StarterKitError as exc:
            print(f"ERROR: {exc}")
            return 2
    if args.apply and not args.revert and not args.verify:
        print()
        print("!" * 64)
        print("RESTART REQUIRED: new item XML loads only at a fresh game launch. Fully restart")
        print("Bannerlord, start a NEW campaign, and check the starter items on the player")
        print("before considering this done. A green validator does not prove the engine saw them.")
        print("!" * 64)
    elif not args.apply and not args.verify:
        print("\n(dry run; pass --apply to write)")
    return rc


if __name__ == "__main__":
    sys.exit(main())
