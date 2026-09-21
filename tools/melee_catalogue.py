#!/usr/bin/env python3
"""Load the installed modules' crafted weapons and price them with `melee_damage`.

`melee_damage` is pure physics. This module is the wiring: it reads the crafting pieces,
the crafting templates and the weapon descriptions the way the engine merges them, resolves
each item's primary usage, and hands back priced weapons.

Why the merge matters. `MBObjectManager.CreateMergedXmlFile` applies every module's XSLT onto
Native's base XML, so the Armory's `crafting_templates.xslt` and `weapon_descriptions.xslt`
change both the build orders and the available-piece sets. Reading Native alone, or matching
the XSLT with a regex, gets a different weapon than the one that ships.

Two things that are easy to get wrong and that this module gets from the data rather than a
hardcoded table:

  * **Build order.** `TwoHandedPolearm` carries a Guard at order 1 and the Blade at order 2,
    not the Handle/Blade/Pommel shape the axes and maces use. Assuming otherwise drops a
    piece out of the assembly and moves the centre of mass.
  * **Primary usage.** A template lists its `WeaponDescription`s in priority order, and the
    FIRST one whose `<AvailablePieces>` contains every piece the item actually uses becomes
    the primary (`Crafting.GenerateCraftedItem`, Crafting.cs:571-608). `TwoHandedPolearm`
    lists `OneHandedPolearm` first, and that description carries `WideGrip` WITHOUT
    `NotUsableWithOneHand`, so a spear that matches it is simulated one-handed. Same pieces,
    very different speed.

Read-only.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import melee_damage as md

try:
    from lxml import etree as LET
except ImportError:  # pragma: no cover - reported by the caller, never silently skipped
    LET = None


# Templates whose weapons are melee. Throwing templates are priced by a different engine
# path (`CalculateMissileDamage`) and are excluded rather than mispriced.
MELEE_TEMPLATES = {
    "OneHandedSword",
    "TwoHandedSword",
    "OneHandedAxe",
    "TwoHandedAxe",
    "Mace",
    "TwoHandedMace",
    "OneHandedPolearm",
    "TwoHandedPolearm",
    "Pike",
    "Dagger",
}

THROWN_TEMPLATES = {"Javelin", "ThrowingAxe", "ThrowingKnife"}


class CatalogueError(Exception):
    pass


@dataclass(frozen=True)
class Template:
    id: str
    item_type: str
    build_order: list[tuple[str, int]]
    description_ids: list[str]


@dataclass(frozen=True)
class Description:
    id: str
    weapon_class: str
    flags: frozenset[str]
    available: frozenset[str]
    item_usage_features: tuple[str, ...]


@dataclass(frozen=True)
class Item:
    id: str
    name: str
    culture: str
    template: str
    pieces: tuple[tuple[str, str, int], ...]  # (piece id, slot type, scale percentage)
    source: str


@dataclass(frozen=True)
class Usage:
    """One mode the engine builds for an item: a description plus the stats under its flags."""

    description: str
    is_primary: bool
    features: tuple[str, ...]
    usage_set: str
    strikes: frozenset[str]
    stats: md.WeaponStats

    @property
    def can_swing(self) -> bool:
        return "swing" in self.strikes

    @property
    def can_thrust(self) -> bool:
        return "thrust" in self.strikes

    def swing_dps(self, stun: float = md.STUN_PERIOD_SWING) -> float:
        """Sustained swing damage per second, 0 if this mode cannot swing."""
        if not self.can_swing or self.stats.raw_swing_speed <= 0:
            return 0.0
        return md.sustained_damage(
            self.stats.swing_damage, md.swing_cycle_time(self.stats.raw_swing_speed, stun)
        )

    def thrust_dps(self, stun: float = md.STUN_PERIOD_THRUST) -> float:
        if not self.can_thrust or self.stats.raw_thrust_speed <= 0:
            return 0.0
        return md.sustained_damage(
            self.stats.thrust_damage, md.thrust_cycle_time(self.stats.raw_thrust_speed, stun)
        )


@dataclass
class Priced:
    """Every mode of one crafted weapon.

    A crafted item is not one weapon. `GenerateCraftedItem` adds a `WeaponComponentData` for
    EVERY description whose available pieces cover the item, and the player or AI switches
    between them. The first is primary; the rest are alternates.

    This matters for balance, not just pedantry. `OneHandedPolearm`'s usage features are
    `onehanded_polearm:block:long:rshield:thrust` with no `swing`, so a spear whose primary is
    that mode CANNOT swing in it at all: the swing damage the physics computes there is a
    number the game never uses. Judging such a weapon on its primary swing understates it by
    an order of magnitude. Judge each attack on the modes that actually permit it.
    """

    item: Item
    usages: tuple[Usage, ...]
    blade_piece: str
    tier: int

    @property
    def primary(self) -> Usage:
        return self.usages[0]

    @property
    def description(self) -> str:
        return self.primary.description

    @property
    def swing_usage(self) -> Usage | None:
        """The mode giving the best swing, among those that can swing. None if it never swings."""
        candidates = [u for u in self.usages if u.can_swing]
        return max(candidates, key=lambda u: u.stats.swing_damage) if candidates else None

    @property
    def thrust_usage(self) -> Usage | None:
        candidates = [u for u in self.usages if u.can_thrust]
        return max(candidates, key=lambda u: u.stats.thrust_damage) if candidates else None

    @property
    def swing_damage(self) -> int:
        u = self.swing_usage
        return u.stats.swing_damage if u else 0

    @property
    def thrust_damage(self) -> int:
        u = self.thrust_usage
        return u.stats.thrust_damage if u else 0

    @property
    def swing_speed(self) -> int:
        u = self.swing_usage
        return u.stats.swing_speed if u else 0

    @property
    def thrust_speed(self) -> int:
        u = self.thrust_usage
        return u.stats.thrust_speed if u else 0

    @property
    def best_damage(self) -> int:
        """The weapon's headline number: the harder of its two usable attacks."""
        return max(self.swing_damage, self.thrust_damage)

    @property
    def swing_dps(self) -> float:
        """Sustained swing damage per second, from the mode that swings hardest.

        Note this reads the SAME mode `swing_damage` does, chosen on damage rather than on DPS.
        Measured 2026-09-20: the two choices disagree for 0 of the 364 Armory melee weapons, on
        both attacks, because damage and speed come off the same physics so a mode that hits
        harder also swings no slower. Choosing per-stat would report a weapon's damage and its
        DPS from two different modes, which is not a weapon anyone can wield.
        `test_mode_choice_agrees_between_damage_and_dps` pins it.
        """
        u = self.swing_usage
        return u.swing_dps() if u else 0.0

    @property
    def thrust_dps(self) -> float:
        u = self.thrust_usage
        return u.thrust_dps() if u else 0.0

    @property
    def best_dps(self) -> float:
        """The weapon's sustained output: the better of its two usable attacks.

        This is the number to rank a weapon by. `best_damage` is one blow; a fast weapon with a
        smaller blow can out-damage a heavy one over time, and several TAOM lines trade exactly
        that way.
        """
        return max(self.swing_dps, self.thrust_dps)

    @property
    def stats(self) -> md.WeaponStats:
        """Shape (weight, reach, inertia) is mode-independent, so the primary speaks for it."""
        return self.primary.stats


@dataclass
class Catalogue:
    pieces: dict[str, md.Piece] = field(default_factory=dict)
    templates: dict[str, Template] = field(default_factory=dict)
    descriptions: dict[str, Description] = field(default_factory=dict)
    items: dict[str, Item] = field(default_factory=dict)
    # usage-set id -> (base_set id or None, the strike types it declares directly)
    usage_sets: dict[str, tuple[str | None, frozenset[str]]] = field(default_factory=dict)
    # usage-set id -> its resolved flags (e.g. requires_no_shield)
    usage_flags: dict[str, frozenset[str]] = field(default_factory=dict)
    # piece id -> the usage-feature tokens that piece removes
    excluded_features: dict[str, frozenset[str]] = field(default_factory=dict)

    def strike_types(self, usage_set: str) -> frozenset[str]:
        """Which attacks a usage set permits, following `base_set` and unioning the chain.

        The union is load-bearing. `onehanded_shield_axe` declares no `<usages>` of its own
        and inherits `swing` from `onehanded_block_shield_swing`; taking only the first set
        in the chain that declares any would say an axe cannot swing, which is how an earlier
        pass of this tool decided 100 weapons had no attacks.
        """
        seen: set[str] = set()
        out: set[str] = set()
        cursor: str | None = usage_set
        while cursor and cursor in self.usage_sets and cursor not in seen:
            seen.add(cursor)
            base, strikes = self.usage_sets[cursor]
            out |= strikes
            cursor = base
        return frozenset(out)


# --- parsing ------------------------------------------------------------------------------


def _parse(path: Path):
    return LET.parse(str(path), LET.XMLParser(recover=True, huge_tree=True))


def merged(modules: Path, name: str):
    """Native's XML with every module's same-named XSLT chained on, as the engine merges it.

    Copied in shape from `tools/audit_polearm_shield_parity.py:merged`, which established
    that a sorted module order gives the same document as load order for TAOM's additive
    transforms.
    """
    if LET is None:
        raise CatalogueError("lxml is required to apply the module XSLT chain")
    base = modules / "Native" / "ModuleData" / f"{name}.xml"
    if not base.is_file():
        raise CatalogueError(f"missing base XML: {base}")
    doc = _parse(base)
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        sheet = module / "ModuleData" / f"{name}.xslt"
        if sheet.is_file():
            doc = LET.XSLT(_parse(sheet))(doc)
    return doc


def load_templates(modules: Path) -> dict[str, Template]:
    doc = merged(modules, "crafting_templates")
    out: dict[str, Template] = {}
    for node in doc.getroot().iter("CraftingTemplate"):
        tid = node.get("id")
        if not tid:
            continue
        datas = node.find("PieceDatas")
        build: list[tuple[str, int]] = []
        if datas is not None:
            for pd in datas.findall("PieceData"):
                ptype = pd.get("piece_type")
                order = pd.get("build_order")
                if ptype in md.PIECE_TYPES and order is not None:
                    build.append((ptype, int(order)))
        block = node.find("WeaponDescriptions")
        # findall, not iteration: lxml yields comment nodes as children and the Armory's
        # sheets are commented per culture block.
        descs = (
            [d.get("id") for d in block.findall("WeaponDescription") if d.get("id")]
            if block is not None
            else []
        )
        out[tid] = Template(tid, node.get("item_type") or "", build, descs)
    return out


def load_descriptions(modules: Path) -> dict[str, Description]:
    doc = merged(modules, "weapon_descriptions")
    out: dict[str, Description] = {}
    for node in doc.getroot().iter("WeaponDescription"):
        did = node.get("id")
        if not did:
            continue
        wf = node.find("WeaponFlags")
        flags = (
            frozenset(f.get("value") for f in wf.findall("WeaponFlag") if f.get("value"))
            if wf is not None
            else frozenset()
        )
        ap = node.find("AvailablePieces")
        available = (
            frozenset(p.get("id") for p in ap.findall("AvailablePiece") if p.get("id"))
            if ap is not None
            else frozenset()
        )
        features = tuple(t for t in (node.get("item_usage_features") or "").split(":") if t)
        out[did] = Description(did, node.get("weapon_class") or "", flags, available, features)
    return out


def load_pieces(modules: Path) -> dict[str, md.Piece]:
    """Every crafting piece from every module. Later modules do not silently win.

    A duplicate id across modules is a real data defect elsewhere; here the first definition
    wins and the duplicate is ignored, which matches how the other TAOM audits behave.
    """
    out: dict[str, md.Piece] = {}
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in sorted(
            list(data.glob("*crafting_pieces*.xml")) + list(data.glob("*/*crafting_pieces*.xml"))
        ):
            for pid, piece in md.parse_crafting_pieces(path).items():
                out.setdefault(pid, piece)
    return out


def load_items(modules: Path, only: set[str] | None = None) -> dict[str, Item]:
    """Every `<CraftedItem>` under the given modules, keyed by id.

    `only` restricts to a set of module directory names (e.g. just the Armory, or just
    SandBoxCore for the vanilla envelope).
    """
    if LET is None:
        raise CatalogueError("lxml is required")
    out: dict[str, Item] = {}
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        if only is not None and module.name not in only:
            continue
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in sorted(data.rglob("*.xml")):
            try:
                root = _parse(path).getroot()
            except (LET.XMLSyntaxError, OSError):
                continue
            if root is None:
                continue
            for node in root.iter("CraftedItem"):
                iid = node.get("id")
                if not iid:
                    continue
                pieces = []
                for p in node.iter("Piece"):
                    pid = p.get("id")
                    slot = p.get("Type")
                    if not pid or slot not in md.PIECE_TYPES:
                        continue
                    raw = p.get("scale_factor")
                    try:
                        scale = int(raw) if raw else 100
                    except ValueError:
                        scale = 100
                    pieces.append((pid, slot, scale))
                out.setdefault(
                    iid,
                    Item(
                        id=iid,
                        name=strip_loc(node.get("name") or ""),
                        culture=(node.get("culture") or "").replace("Culture.", ""),
                        template=node.get("crafting_template") or "",
                        pieces=tuple(pieces),
                        source=f"{module.name}/{path.relative_to(data).as_posix()}",
                    ),
                )
    return out


def strip_loc(name: str) -> str:
    """Drop a leading `{=key}` localisation marker from a display name."""
    if name.startswith("{=") and "}" in name:
        return name.split("}", 1)[1]
    return name


def load_combat_parameters(modules: Path) -> tuple[float, float]:
    """(swing stun, thrust stun) from `managed_core_parameters.xml`, in seconds.

    These are the attacker's recovery after a connecting blow, and they are the one part of the
    attack cycle that is authored data rather than derived from the weapon. A module may
    override the file, so read it rather than trusting the shipped defaults. Falls back to
    `melee_damage`'s documented v1.5.3 values when the file or a key is absent, which keeps a
    DPS number available on a machine without the install.
    """
    swing, thrust = md.STUN_PERIOD_SWING, md.STUN_PERIOD_THRUST
    path = modules / "Native" / "ModuleData" / "managed_core_parameters.xml"
    if not path.is_file():
        return swing, thrust
    try:
        root = _parse(path).getroot()
    except (LET.XMLSyntaxError, OSError):
        return swing, thrust
    for node in root.iter():
        key, raw = node.get("id"), node.get("value")
        if not key or raw is None:
            continue
        try:
            value = float(raw)
        except ValueError:
            continue
        if key == "StunPeriodAttackerSwing":
            swing = value
        elif key == "StunPeriodAttackerThrust":
            thrust = value
    return swing, thrust


def load_usage_sets(modules: Path) -> dict[str, tuple[str | None, frozenset[str]]]:
    """`item_usage_set` id -> (base_set, the strike types its own `<usage>` rows declare)."""
    path = modules / "Native" / "ModuleData" / "item_usage_sets.xml"
    if not path.is_file():
        raise CatalogueError(f"missing {path}")
    out: dict[str, tuple[str | None, frozenset[str]]] = {}
    for node in _parse(path).getroot().iter("item_usage_set"):
        sid = node.get("id")
        if not sid:
            continue
        usages = node.find("usages")
        strikes = (
            frozenset(
                u.get("strike_type") for u in usages.findall("usage") if u.get("strike_type")
            )
            if usages is not None
            else frozenset()
        )
        out[sid] = (node.get("base_set"), strikes)
    return out


def load_usage_flags(modules: Path) -> dict[str, frozenset[str]]:
    """usage-set id -> its flags, taking the FIRST set in the base chain that declares any.

    Own-flags-first, not a union, and that is deliberate. `audit_polearm_shield_parity.py`
    established it: in shipped data a child that declares any flags declares the complete set,
    so unioning would let a base's `requires_no_shield` leak onto a child that dropped it on
    purpose. Note this is the opposite convention to `Catalogue.strike_types`, which must
    union; the two fields answer different questions and the engine treats them differently.
    """
    path = modules / "Native" / "ModuleData" / "item_usage_sets.xml"
    if not path.is_file():
        return {}
    raw: dict[str, tuple[str | None, frozenset[str]]] = {}
    for node in _parse(path).getroot().iter("item_usage_set"):
        sid = node.get("id")
        if not sid:
            continue
        block = node.find("flags")
        flags = (
            frozenset(f.get("name") for f in block.findall("flag") if f.get("name"))
            if block is not None
            else frozenset()
        )
        raw[sid] = (node.get("base_set"), flags)

    resolved: dict[str, frozenset[str]] = {}
    for key in raw:
        seen: set[str] = set()
        cursor: str | None = key
        while cursor and cursor in raw and cursor not in seen:
            seen.add(cursor)
            base, flags = raw[cursor]
            if flags:
                resolved[key] = flags
                break
            cursor = base
        resolved.setdefault(key, frozenset())
    return resolved


def load_shield_ids(modules: Path) -> set[str]:
    """Every `<Item Type="Shield">` id across the loaded modules.

    Needed because a shield in a troop's kit changes which weapons it may be given at all.
    """
    if LET is None:
        raise CatalogueError("lxml is required")
    out: set[str] = set()
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in sorted(data.rglob("*.xml")):
            try:
                root = _parse(path).getroot()
            except (LET.XMLSyntaxError, OSError):
                continue
            if root is None:
                continue
            for node in root.iter("Item"):
                if (node.get("Type") or "") == "Shield" and node.get("id"):
                    out.add(node.get("id"))
    return out


def load_excluded_features(modules: Path) -> dict[str, frozenset[str]]:
    """piece id -> the usage-feature tokens it excludes, which reshape the usage-set id."""
    out: dict[str, frozenset[str]] = {}
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in sorted(
            list(data.glob("*crafting_pieces*.xml")) + list(data.glob("*/*crafting_pieces*.xml"))
        ):
            try:
                root = _parse(path).getroot()
            except (LET.XMLSyntaxError, OSError):
                continue
            for node in root.iter("CraftingPiece"):
                pid = node.get("id")
                if not pid:
                    continue
                raw = node.get("excluded_item_usage_features") or ""
                out.setdefault(pid, frozenset(t for t in raw.split(":") if t))
    return out


def load(modules: Path, item_modules: set[str] | None = None) -> Catalogue:
    cat = Catalogue()
    cat.pieces = load_pieces(modules)
    cat.templates = load_templates(modules)
    cat.descriptions = load_descriptions(modules)
    cat.usage_sets = load_usage_sets(modules)
    cat.usage_flags = load_usage_flags(modules)
    cat.excluded_features = load_excluded_features(modules)
    cat.items = load_items(modules, item_modules)
    return cat


# --- resolution + pricing -----------------------------------------------------------------


def matching_descriptions(cat: Catalogue, item: Item) -> list[Description]:
    """Every description the engine would build a mode from, in template order.

    `Crafting.GenerateCraftedItem` (Crafting.cs:571-608) walks the template's descriptions in
    order and emits a weapon for each whose `<AvailablePieces>` covers every valid used piece.
    The first match is the primary; the rest are alternates the wielder can switch to.
    """
    template = cat.templates.get(item.template)
    if template is None:
        return []
    used = {pid for pid, _slot, _scale in item.pieces}
    if not used:
        return []
    out = []
    for did in template.description_ids:
        desc = cat.descriptions.get(did)
        if desc is not None and used <= desc.available:
            out.append(desc)
    return out


def resolve_description(cat: Catalogue, item: Item) -> Description | None:
    """The item's PRIMARY usage: the first description that accepts every piece."""
    matches = matching_descriptions(cat, item)
    return matches[0] if matches else None


def usage_set_id(cat: Catalogue, item: Item, desc: Description) -> str:
    """The `item_usage_set` this mode resolves to.

    The description's feature tokens minus anything the used pieces exclude, joined with `_`.
    Same construction as `tools/audit_polearm_shield_parity.py:primary_usage`.
    """
    excluded: set[str] = set()
    for pid, _slot, _scale in item.pieces:
        excluded |= cat.excluded_features.get(pid, frozenset())
    return "_".join(t for t in desc.item_usage_features if t not in excluded)


def weapon_flags(cat: Catalogue, item: Item, desc: Description) -> md.WeaponFlags:
    """Description flags OR'd with the design's own accumulated piece flags.

    `Crafting.cs:601` does `weaponDescription.WeaponFlags | weaponDesign.WeaponFlags`, and
    the design's flags are the union of each used piece's `AdditionalWeaponFlags`
    (`WeaponDesign.cs`). Using the description's alone drops piece-granted behaviour.
    """
    flags = set(desc.flags)
    for pid, _slot, _scale in item.pieces:
        piece = cat.pieces.get(pid)
        if piece is not None:
            flags |= piece.flags
    return md.WeaponFlags(frozenset(flags))


def elements_for(cat: Catalogue, item: Item) -> dict[str, md.Element]:
    """Map the item's pieces into `{slot type: Element}`, skipping unresolvable ids."""
    out: dict[str, md.Element] = {}
    for pid, slot, scale in item.pieces:
        piece = cat.pieces.get(pid)
        if piece is None:
            continue
        out[slot] = md.Element(piece, scale)
    return out


def price(cat: Catalogue, item: Item) -> Priced:
    """Price one crafted item. Raises `CatalogueError` with a stated reason on failure."""
    if item.template in THROWN_TEMPLATES:
        raise CatalogueError(f"{item.id}: {item.template} is thrown, not melee")
    template = cat.templates.get(item.template)
    if template is None:
        raise CatalogueError(f"{item.id}: unknown crafting template {item.template!r}")
    if not template.build_order:
        raise CatalogueError(f"{item.id}: template {item.template} declares no build order")

    missing = [pid for pid, _s, _c in item.pieces if pid not in cat.pieces]
    if missing:
        raise CatalogueError(f"{item.id}: undefined crafting piece(s) {', '.join(missing)}")

    matches = matching_descriptions(cat, item)
    if not matches:
        raise CatalogueError(
            f"{item.id}: no WeaponDescription of {item.template} accepts all its pieces"
        )
    if "MeleeWeapon" not in matches[0].flags:
        raise CatalogueError(f"{item.id}: primary usage {matches[0].id} is not a melee weapon")

    elements = elements_for(cat, item)
    usages = []
    for index, desc in enumerate(matches):
        if "MeleeWeapon" not in desc.flags:
            # A thrown alternate (TwoHandedPolearm_Thrown) is priced by the missile path,
            # which this module does not model. Skip it rather than price it wrongly.
            continue
        stats = md.compute(elements, template.build_order, weapon_flags(cat, item, desc))
        usage_set = usage_set_id(cat, item, desc)
        usages.append(
            Usage(
                description=desc.id,
                is_primary=index == 0,
                features=desc.item_usage_features,
                usage_set=usage_set,
                strikes=cat.strike_types(usage_set),
                stats=stats,
            )
        )

    blade = elements.get("Blade")
    return Priced(
        item=item,
        usages=tuple(usages),
        blade_piece=blade.piece.id if blade else "",
        tier=max((el.piece.tier for el in elements.values()), default=1),
    )


def price_all(cat: Catalogue) -> tuple[dict[str, Priced], list[tuple[str, str]]]:
    """Price every melee item. Returns (priced, failures) where failures are (id, reason).

    Failures are returned, never swallowed: a report that silently priced 300 of 364 items
    reads exactly like a clean run.
    """
    priced: dict[str, Priced] = {}
    failures: list[tuple[str, str]] = []
    for iid, item in sorted(cat.items.items()):
        if item.template in THROWN_TEMPLATES:
            continue
        if item.template not in MELEE_TEMPLATES:
            continue
        try:
            priced[iid] = price(cat, item)
        except (CatalogueError, md.MeleeDamageError) as exc:
            failures.append((iid, str(exc)))
    return priced, failures
