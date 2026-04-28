"""Parse weapon manifests (XML, schema-aligned with crafting_pieces output).

A manifest is a small XML document that names one or more CraftedWeapon and/or
SinglePieceWeapon entries. Each entry carries the human-input fields the FBX
cannot supply (tier, damage_factor, weight, length, name, etc.). The tool fills
in id/mesh/body_name from the FBX side via the classify module.

Schema (intentionally close to the output XML):

    <WeaponManifest>
      <CraftedWeapon id="wm_swan_knight_sword_c"
                     name="{=key}Swan Knight Sword (variant C)"
                     weapon_class="OneHandedSword"
                     culture="gondor"
                     tier="4">
        <Blade  length="92.4" weight="0.93" body_name="bo_sword_one_handed">
          <Thrust damage_type="Pierce" damage_factor="2.4" />
          <Swing  damage_type="Cut"    damage_factor="2.8" />
        </Blade>
        <Guard  length="2.93" weight="0.15" armor_bonus="5" />
        <Hilt   length="11.2" weight="0.20" />
        <Pommel length="3.6"  weight="0.10" />
      </CraftedWeapon>

      <SinglePieceWeapon id="gondor_warbow_a"
                         name="{=key}Gondor Warbow"
                         weapon_class="Bow"
                         culture="gondor"
                         body_name="bo_warbow_a">
        <Weapon thrust_speed="92" speed_rating="87" missile_speed="84"
                weapon_length="106" accuracy="99"
                thrust_damage="30" thrust_damage_type="Pierce"
                item_usage="bow" physics_material="wood_weapon">
          <WeaponFlags RangedWeapon="true" HasString="true" StringHeldByHand="true" />
        </Weapon>
      </SinglePieceWeapon>
    </WeaponManifest>
"""

from __future__ import annotations

import os
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Any

SINGLE_PIECE_CLASSES = {"Bow", "Crossbow", "Javelin", "ThrowingKnife", "ThrowingAxe", "Stone"}

PIECE_TAG_TO_PIECE_TYPE = {
    "Blade": "Blade",
    "Guard": "Guard",
    "Hilt": "Handle",
    "Handle": "Handle",
    "Pommel": "Pommel",
}


@dataclass
class PieceSpec:
    """One crafting piece's human-input fields, before id/mesh/body_name fill-in."""

    role: str                              # blade/guard/hilt/handle/pommel (lowercased tag)
    piece_type: str                        # Blade/Guard/Handle/Pommel
    attrs: dict[str, str] = field(default_factory=dict)         # length, weight, etc.
    blade_data: dict[str, str] = field(default_factory=dict)    # body_name, holster_mesh, stack_amount, physics_material
    thrust: dict[str, str] | None = None                         # damage_type, damage_factor
    swing: dict[str, str] | None = None
    stat_contributions: dict[str, str] = field(default_factory=dict)  # armor_bonus etc. (Guard pieces)
    materials: list[dict[str, str]] = field(default_factory=list)     # CraftingCost mats


@dataclass
class CraftedWeaponSpec:
    """One <CraftedWeapon> entry from a manifest."""

    base_id: str               # e.g. wm_swan_knight_sword_c (pieces become <id>_blade etc.)
    name: str                  # localization-tagged name
    weapon_class: str          # OneHandedSword, TwoHandedPolearm, ...
    culture: str | None        # gondor, mordor, ... or None to be resolved later
    tier: str | None
    extra_item_attrs: dict[str, str] = field(default_factory=dict)   # is_merchandise, value, modifier_group, ...
    pieces: list[PieceSpec] = field(default_factory=list)


@dataclass
class SinglePieceWeaponSpec:
    """One <SinglePieceWeapon> entry — does not use the crafting system."""

    item_id: str
    name: str
    weapon_class: str
    culture: str | None
    body_name: str | None
    item_attrs: dict[str, str] = field(default_factory=dict)         # mesh, weight, Type, holster_mesh, ...
    weapon_attrs: dict[str, str] = field(default_factory=dict)
    weapon_flags: dict[str, str] = field(default_factory=dict)


@dataclass
class Manifest:
    crafted: list[CraftedWeaponSpec] = field(default_factory=list)
    single: list[SinglePieceWeaponSpec] = field(default_factory=list)


class ManifestError(ValueError):
    """Raised on malformed manifest input."""


def parse_manifest(path: str) -> Manifest:
    """Parse a single manifest .xml file."""
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        raise ManifestError(f"{path}: invalid XML: {e}") from e
    root = tree.getroot()
    if root.tag != "WeaponManifest":
        raise ManifestError(f"{path}: expected <WeaponManifest> root, got <{root.tag}>")

    manifest = Manifest()
    for child in root:
        if child.tag == "CraftedWeapon":
            manifest.crafted.append(_parse_crafted(child, path))
        elif child.tag == "SinglePieceWeapon":
            manifest.single.append(_parse_single(child, path))
        elif isinstance(child.tag, str):
            raise ManifestError(f"{path}: unexpected element <{child.tag}>")
    return manifest


def parse_manifest_dir(directory: str) -> Manifest:
    """Concatenate every .xml manifest under a directory."""
    merged = Manifest()
    for entry in sorted(os.listdir(directory)):
        if entry.endswith(".xml"):
            sub = parse_manifest(os.path.join(directory, entry))
            merged.crafted.extend(sub.crafted)
            merged.single.extend(sub.single)
    return merged


def _parse_crafted(elem: ET.Element, src: str) -> CraftedWeaponSpec:
    base_id = _required(elem, "id", src)
    name = _required(elem, "name", src)
    weapon_class = _required(elem, "weapon_class", src)
    extra: dict[str, str] = {}
    for k, v in elem.attrib.items():
        if k not in {"id", "name", "weapon_class", "culture", "tier"}:
            extra[k] = v

    spec = CraftedWeaponSpec(
        base_id=base_id,
        name=name,
        weapon_class=weapon_class,
        culture=elem.attrib.get("culture"),
        tier=elem.attrib.get("tier"),
        extra_item_attrs=extra,
    )

    for piece_elem in elem:
        if piece_elem.tag not in PIECE_TAG_TO_PIECE_TYPE:
            raise ManifestError(f"{src}: <CraftedWeapon id={base_id!r}> has unknown child <{piece_elem.tag}>")
        spec.pieces.append(_parse_piece(piece_elem))

    return spec


def _parse_piece(elem: ET.Element) -> PieceSpec:
    role = elem.tag.lower()
    piece_type = PIECE_TAG_TO_PIECE_TYPE[elem.tag]

    blade_data: dict[str, str] = {}
    thrust: dict[str, str] | None = None
    swing: dict[str, str] | None = None
    stat_contributions: dict[str, str] = {}
    materials: list[dict[str, str]] = []
    attrs: dict[str, str] = {}

    # Pieces accept a few attributes that move into BladeData rather than the piece header.
    blade_only_attrs = {"body_name", "holster_mesh", "stack_amount", "physics_material"}
    for k, v in elem.attrib.items():
        if k in blade_only_attrs:
            blade_data[k] = v
        elif k in {"armor_bonus", "speed_rating", "thrust_speed"}:
            stat_contributions[k] = v
        else:
            attrs[k] = v

    for sub in elem:
        if sub.tag == "Thrust":
            thrust = dict(sub.attrib)
        elif sub.tag == "Swing":
            swing = dict(sub.attrib)
        elif sub.tag == "StatContributions":
            stat_contributions.update(sub.attrib)
        elif sub.tag == "Material":
            materials.append(dict(sub.attrib))
        elif sub.tag == "BladeData":
            blade_data.update(sub.attrib)
            for nested in sub:
                if nested.tag == "Thrust":
                    thrust = dict(nested.attrib)
                elif nested.tag == "Swing":
                    swing = dict(nested.attrib)

    return PieceSpec(
        role=role,
        piece_type=piece_type,
        attrs=attrs,
        blade_data=blade_data,
        thrust=thrust,
        swing=swing,
        stat_contributions=stat_contributions,
        materials=materials,
    )


def _parse_single(elem: ET.Element, src: str) -> SinglePieceWeaponSpec:
    item_id = _required(elem, "id", src)
    name = _required(elem, "name", src)
    weapon_class = _required(elem, "weapon_class", src)
    body_name = elem.attrib.get("body_name")

    item_attrs: dict[str, str] = {}
    for k, v in elem.attrib.items():
        if k not in {"id", "name", "weapon_class", "culture", "body_name"}:
            item_attrs[k] = v

    weapon_attrs: dict[str, str] = {}
    weapon_flags: dict[str, str] = {}
    weapon_elem = elem.find("Weapon")
    if weapon_elem is not None:
        weapon_attrs = dict(weapon_elem.attrib)
        flags_elem = weapon_elem.find("WeaponFlags")
        if flags_elem is not None:
            weapon_flags = dict(flags_elem.attrib)

    return SinglePieceWeaponSpec(
        item_id=item_id,
        name=name,
        weapon_class=weapon_class,
        culture=elem.attrib.get("culture"),
        body_name=body_name,
        item_attrs=item_attrs,
        weapon_attrs=weapon_attrs,
        weapon_flags=weapon_flags,
    )


def _required(elem: ET.Element, key: str, src: str) -> str:
    if key not in elem.attrib:
        raise ManifestError(f"{src}: <{elem.tag}> missing required attribute {key!r}")
    return elem.attrib[key]


def is_single_piece_class(weapon_class: str) -> bool:
    return weapon_class in SINGLE_PIECE_CLASSES
