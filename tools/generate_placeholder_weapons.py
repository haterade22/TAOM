#!/usr/bin/env python3
"""Generate placeholder melee weapons and hand them to the troops the melee ladder could not arm.

WHY
---
`tools/fix_melee_ladder.py` can only swap a troop onto a weapon its own culture already owns, and for
many culture/class pairs there is none in the tier band (36 of 63 pairs, see restat_melee_blades.py).
Pass 2 restats the shared blade, which also raises every low-tier troop carrying it. A placeholder is
the third route: a NEW weapon for the elite tiers only, with the donor's look, until the Armory has a
real one. Every id carries the tag in `tools/placeholder_weapons.json` (`taom_ph_`), so
`git grep taom_ph_` finds every placeholder, and replacing one is a roster swap plus deleting its spec
entry.

WHAT IT WRITES (all in the repo; the unversioned Armory is only read)
---------------------------------------------------------------------
  Main/_Module/ModuleData/placeholder_weapons/ph_crafting_pieces.xml
      one cloned blade piece per weapon: the donor blade with a new id, its Swing damage_factor
      multiplied by k_swing and its Thrust damage_factor by k_thrust. Both start at (ladder target
      DPS / donor DPS); DPS is linear in damage_factor (restat_melee_blades.py, Crafting.cs:374,
      :380), so the clone lands on the target. Each factor stops at that attack's own tier limit
      (melee_ladders.json `class_limits` scaled to the placeholder's tier and culture by
      `melee_ladder.tier_limit`: one blow and DPS per crafting template), so a thrust at its limit
      leaves the swing free to rise, and a capped clone sits under its tier target. Run the tool AFTER
      restat_melee_blades.py: k is relative to the donor, and the restat moves the donor.
  Main/_Module/ModuleData/placeholder_weapons/ph_items.xml
      one CraftedItem per weapon: the donor item with the new id, the new name key, the cloned blade
      and is_merchandise="false" (no shop flood; a NotMerchandise item never drops as loot).
  Main/_Module/ModuleData/placeholder_weapons/ph_weapon_descriptions.xslt, ph_crafting_templates.xslt
      the engine builds a crafted weapon only from pieces its WeaponDescription and CraftingTemplate
      list, so each cloned blade is appended to every description (AvailablePieces) and to the
      template (UsablePieces) that lists its donor blade. The Armory's own stylesheets REPLACE those
      lists, so these must run after them: TAOM has to load after LOTRLOME_Armory (the documented
      launcher order). Loaded before it, the Armory wipes the additions and the placeholders get no
      weapon usage. Stylesheet-only rows: no sibling .xml exists (docs/modding/file-catalogue.md).
  Main/_Module/SubModule.xml
      four XmlNodes (CraftingPieces, Items, WeaponDescriptions, CraftingTemplates), added once.
  Main/_Module/ModuleData/troops/troops_*.xml
      every troop of a listed culture whose tier is inside `tiers` and whose battle kit carries the
      donor gets the placeholder in each slot that held the donor (fix_upgrade_armour_regressions
      .write_changes: BOM and newlines kept, every file parsed before any is written). The rule is
      `placeholder_for`, and the troop generators that write a whole file
      (fix_melee_ladder.GENERATED_TROOP_FILES: realm_troops.py for the realms,
      generate_lothlorien_troops.py, generate_snow_orc_troops.py) call it as they render, so this
      tool writes no swap into their files: it lists the ones still owed and --verify fails until
      that generator runs with --apply.

TIER CLONES (owner, 2026-11-16)
-------------------------------
Most entries are tier clones: a copy of the weapon a troop carries, restated to its tier, for a slot
the roster pass could not fill from the troop's own culture. Each is named as the next Roman numerals
of its donor's line, and the tool refuses a numbered line that repeats a numeral or falls in DPS at a
step a placeholder takes part in (`line_faults`). The spec rules that need no Armory (tag, unique id,
tier inside its span, one entry per donor, culture and tier, one craft row per entry) are checked when
the spec loads (`check_spec`), so every generator that reads the swap rule gets a checked spec.

Dry run by default; `--apply` writes; a second `--apply` writes nothing; `--verify` exits 1 when the
generated files or the troops differ from the spec. Needs the installed Armory and lxml (the shared
melee_catalogue). After --apply the new item files load only at a full game restart.

USAGE
    python tools/generate_placeholder_weapons.py            # plan
    python tools/generate_placeholder_weapons.py --apply
    python tools/generate_placeholder_weapons.py --verify
"""
from __future__ import annotations

import argparse
import collections
import dataclasses
import functools
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import analyze_melee_ladder as report  # noqa: E402
import fix_melee_ladder as roster  # noqa: E402
import melee_catalogue as mc  # noqa: E402
import melee_damage as md  # noqa: E402
import melee_ladder as ladder  # noqa: E402
from fix_upgrade_armour_regressions import write_changes  # noqa: E402

SPEC = REPO / "tools" / "placeholder_weapons.json"
OUT_DIR = REPO / "Main" / "_Module" / "ModuleData" / "placeholder_weapons"
PIECES_FILE = OUT_DIR / "ph_crafting_pieces.xml"
ITEMS_FILE = OUT_DIR / "ph_items.xml"
DESC_XSLT = OUT_DIR / "ph_weapon_descriptions.xslt"
TEMPLATE_XSLT = OUT_DIR / "ph_crafting_templates.xslt"
SUBMODULE = REPO / "Main" / "_Module" / "SubModule.xml"
ARMORY = "LOTRLOME_Armory"
HEADER = ("<!-- GENERATED by tools/generate_placeholder_weapons.py from tools/placeholder_weapons.json."
          " Do not edit by hand. Every id here is a placeholder (tag {tag}): replace it with a real"
          " Armory weapon when one exists. -->")
GAME_TYPES = ("Campaign", "CampaignStoryMode", "CustomGame", "EditorGame")
SUBMODULE_ANCHOR = '      <XmlName id="Items" path="long_defeat/long_defeat_items"/>'


class PlaceholderError(Exception):
    pass


def element_text(path: Path, tag: str, element_id: str) -> str:
    text = path.read_bytes().decode("utf-8-sig")
    m = re.search(r'<%s\b[^>]*?\bid="%s"[\s\S]*?</%s>' % (tag, re.escape(element_id), tag), text)
    if not m:
        raise PlaceholderError(f"{tag} {element_id} not found in {path}")
    return m.group(0)


def find_item_file(armory: Path, item_id: str) -> Path:
    for path in sorted((armory / "ModuleData" / "LOTRLOME_items").rglob("*.xml")):
        if f'id="{item_id}"' in path.read_bytes().decode("utf-8-sig", errors="replace"):
            return path
    raise PlaceholderError(f"donor {item_id} not found in the Armory items")


def scale_factors(piece_xml: str, k: float, k_thrust: float | None = None) -> str:
    """Scale the blade's damage factors: the Swing one by `k`, the Thrust one by `k_thrust` (default `k`)."""
    k_thrust = k if k_thrust is None else k_thrust

    def repl(m):
        factor = k_thrust if m.group(2) == "Thrust" else k
        return f'{m.group(1)}{ladder.floor_factor(float(m.group(3)) * factor):.2f}{m.group(4)}'
    out, n = re.subn(r'(<(Thrust|Swing)\b[^>]*?damage_factor=")([0-9.]+)(")', repl, piece_xml)
    if n == 0:
        raise PlaceholderError("blade piece has no damage_factor")
    return out


def check_spec(spec, lspec=None):
    """Raise PlaceholderError on a spec rule that needs no install; return the spec.

    One entry per (donor, culture, tier) keeps `placeholder_for` to one match. The cultures of one
    entry share a craft row, because the band is rated by the first culture (`spec_craft`)."""
    lspec = ladder.load_spec() if lspec is None else lspec
    by_id = {}
    for w in spec["weapons"]:
        wid = w["id"]
        if not wid.startswith(spec["tag"]):
            raise PlaceholderError(f"{wid} does not start with the tag {spec['tag']}")
        if wid in by_id:
            raise PlaceholderError(f"duplicate id {wid}")
        if not w["tiers"][0] <= w["tier"] <= w["tiers"][1]:
            raise PlaceholderError(f"{wid}: tier {w['tier']} is outside its tiers {w['tiers']}")
        rows = {repr(lspec["craft"].get(c, lspec["craft_default"])) for c in w["cultures"]}
        if len(rows) > 1:
            raise PlaceholderError(f"{wid}: its cultures {w['cultures']} have different craft rows")
        for other in by_id.values():
            if (other["donor"] == w["donor"] and set(other["cultures"]) & set(w["cultures"])
                    and other["tiers"][0] <= w["tiers"][1] and w["tiers"][0] <= other["tiers"][1]):
                raise PlaceholderError(f"{wid} and {other['id']} serve the same donor, culture and tier")
        by_id[wid] = w
    return spec


@functools.lru_cache(maxsize=1)
def load_spec():
    return check_spec(json.loads(SPEC.read_text(encoding="utf-8")))


def spec_weapons():
    """The spec's entries: the rows `placeholder_for` reads by default."""
    return tuple(load_spec()["weapons"])


def serves(w, culture, tier):
    """True when the entry goes to a troop of this culture at this melee-ladder tier."""
    return culture in w["cultures"] and w["tiers"][0] <= tier <= w["tiers"][1]


def placeholder_for(item_id, culture, tier, weapons=None):
    """The placeholder that replaces `item_id` on a troop of `culture` at melee-ladder `tier`, else
    `item_id`. The one swap rule: step 4 below and every generator of a whole troop file call it."""
    for w in spec_weapons() if weapons is None else weapons:
        if w["donor"] == item_id and serves(w, culture, tier):
            return w["id"]
    return item_id


def placeholder_for_troop(item_id, troop_id, culture, level, weapons=None):
    """`placeholder_for` at the troop's melee-ladder tier (analyze_melee_ladder.melee_tier). A troop
    the melee ladder exempts (melee_ladders.json `exempt_troops`) keeps its weapon."""
    if ladder.is_exempt_troop(troop_id, ladder_spec()):
        return item_id
    return placeholder_for(item_id, culture, report.melee_tier(troop_id, level), weapons)


@functools.lru_cache(maxsize=1)
def ladder_spec():
    return ladder.load_spec()


def carriers_of(w, troops):
    """The troops that hold or will hold this placeholder: a listed culture, a tier inside `tiers`,
    and the donor (or the placeholder itself after an earlier --apply) in the battle kit. A troop the
    melee ladder exempts keeps its donor (`placeholder_for_troop`)."""
    return [t for t in troops
            if serves(w, t.culture, t.tier) and (w["id"] in t.items or w["donor"] in t.items)
            and not ladder.is_exempt_troop(t.id, ladder_spec())]


def roman(n):
    return report.roman(n)


def numbered_lines(names_dps, placeholder_ids):
    """{line: [(rank, DPS or None, id, is placeholder)]} sorted by rank, from {id: (name, DPS)}."""
    lines = collections.defaultdict(list)
    for iid, (name, dps) in names_dps.items():
        key = report.line_key(name)
        if key:
            lines[key[0]].append((report.NUMERAL_RANK[key[1]], dps, iid, iid in placeholder_ids))
    return {line: sorted(m, key=lambda x: (x[0], x[2])) for line, m in lines.items()}


def line_faults(lines):
    """A numeral used twice in a line, or a step a placeholder takes part in where DPS falls (more than
    analyze_melee_ladder.DPS_TOLERANCE). A step between two Armory weapons is the Armory's own."""
    faults = []
    for line, members in sorted(lines.items()):
        ranks = collections.Counter(m[0] for m in members)
        for rank, n in sorted(ranks.items()):
            if n > 1:
                ids = ", ".join(m[2] for m in members if m[0] == rank)
                faults.append(f"{line}: numeral {roman(rank)} is used {n} times ({ids})")
        ordered = sorted(members, key=lambda m: (m[0], m[2]))
        for a, b in zip(ordered, ordered[1:]):
            if not (a[3] or b[3]) or a[1] is None or b[1] is None:
                continue
            if b[1] < a[1] - report.DPS_TOLERANCE:
                faults.append(f"{line}: {b[2]} ({roman(b[0])}, {b[1]:.1f} DPS) falls below "
                              f"{a[2]} ({roman(a[0])}, {a[1]:.1f} DPS)")
    return faults


def placeholder_line_faults(cat, priced, spec, new_prices):
    """`line_faults` over every numbered line that holds a placeholder: the Armory's item names (priced
    where the engine port can) plus each placeholder at its own price."""
    names_dps = {iid: (it.name, priced[iid].best_dps if iid in priced else None)
                 for iid, it in cat.items.items() if not iid.startswith(spec["tag"])}
    ph = {w["id"] for w in spec["weapons"]}
    for w in spec["weapons"]:
        names_dps[w["id"]] = (w["name"], new_prices[w["id"]].best_dps)
    lines = numbered_lines(names_dps, ph)
    return line_faults({k: v for k, v in lines.items() if any(m[3] for m in v)})


def plan_from_install(modules):
    """(spec, cat, priced, troops, build() result) from the installed modules and this tree's troops:
    the one loader `main` and `live_line_faults` share."""
    spec = load_spec()
    cat = mc.load(modules, item_modules={ARMORY})
    priced, _failures = mc.price_all(cat)
    troops = report.load_troops(report.TROOPS)
    built = build(spec, cat, priced, troops, modules / ARMORY, frozenset(mc.load_shield_ids(modules)))
    return spec, cat, priced, troops, built


def live_line_faults():
    """`placeholder_line_faults` on the installed Armory and this tree's spec (the LiveInstall test)."""
    spec, cat, priced, _troops, (_p, _i, plan, _s, _d, _t) = plan_from_install(report.MODULES)
    return placeholder_line_faults(cat, priced, spec, price_new(cat, plan))


def spec_craft(w, lspec):
    """The craft row of the spec's first culture, routed through `ladder.craft_key` like every caller."""
    return ladder.craft_key(w["id"], w["cultures"][0], lspec)


def carrier_rows(w, users, lspec):
    """{(tier, craft key): [troop ids]} the weapon must stay inside: every real carrier, plus the
    spec's own tier and first culture (an empty list when no troop sits there). The weapon goes to
    every troop in `tiers` of every listed culture, so the spec's tier alone would let a lower
    tier carry a blow it may not."""
    rows = {}
    for t in sorted(users, key=lambda t: (t.tier, t.id)):
        rows.setdefault((t.tier, ladder.craft_key(t.id, t.culture, lspec)), []).append(t.id)
    rows.setdefault((w["tier"], spec_craft(w, lspec)), [])
    return rows


def row_views(p, ids, shielded, default_held):
    """The weapon as the carriers of one row wield it: behind a shield only the modes a shield allows
    (melee_catalogue.Priced.with_shield), else the whole weapon. A row with no carrier (the spec row)
    takes `default_held`, the view the placeholder is banded on."""
    if not ids:
        return [p.with_shield() if default_held else p]
    views = []
    if any(i in shielded for i in ids):
        views.append(p.with_shield())
    if any(i not in shielded for i in ids):
        views.append(p)
    return views


def row_breaches(priced_weapon, rows, lspec, shielded=frozenset(), default_held=False):
    """[((tier, craft key), troop ids, breaches)] for each row whose tier limit the weapon is over,
    each carrier judged in the mode it holds (`row_views`)."""
    out = []
    for (tier, craft), ids in sorted(rows.items()):
        over = []
        for v in row_views(priced_weapon, ids, shielded, default_held):
            over += ladder.limit_breaches(
                ladder.tier_limit(v.class_template, tier, craft, lspec),
                v.swing_damage, v.thrust_damage, v.swing_dps, v.thrust_dps)
        if over:
            out.append(((tier, craft), ids, over))
    return out


def shield_carriers(users, shields):
    """(ids of the carriers with a shield, whether every carrier has one). A placeholder whose every
    carrier holds a shield is banded on the modes a shield allows."""
    shielded = frozenset(t.id for t in users if any(i in shields for i in t.items))
    return shielded, bool(users) and len(shielded) == len(users)


def build(spec, cat, priced, troops, armory, shields=frozenset()):
    lspec = ladder.load_spec()
    pieces, items, plan, swaps = [], [], [], []
    desc_add, tmpl_add = {}, {}
    check_spec(spec, lspec)
    for w in spec["weapons"]:
        wid = w["id"]
        donor = priced.get(w["donor"])
        if donor is None:
            raise PlaceholderError(f"donor {w['donor']} is not a priced Armory melee weapon")
        culture = spec_craft(w, lspec)
        users = carriers_of(w, troops)
        rows = carrier_rows(w, users, lspec)
        shielded, all_held = shield_carriers(users, shields)
        # The donor's class decides the band (melee_ladder.target), so each class spreads over the
        # ten tiers; its attacks decide which of the class limits it tops out at. A donor that
        # thrusts harder than it swings is a spear (dominant_flags), whatever its template says.
        # When every carrier holds a shield, the band is the one of the mode they wield (a spear's
        # one handed thrust), as the validator judges them.
        template = cat.items[w["donor"]].template
        aim = donor.with_shield() if all_held else donor
        band = ladder.band(w["tier"], culture, lspec, aim.class_template,
                           *ladder.dominant_flags(aim.class_template, aim.swing_damage, aim.thrust_damage,
                                                  aim.swing_dps, aim.thrust_dps, lspec))
        # blend < 1 (owner choice) lands part way: donor DPS + blend x (ladder target - donor DPS).
        blend = float(w.get("blend", 1.0))
        if not 0.0 < blend <= 1.0:
            raise PlaceholderError(f"{wid}: blend must be in (0, 1]")
        k = (aim.best_dps + blend * (band.target - aim.best_dps)) / aim.best_dps
        # The tier limit (melee_ladders.json `class_limits` scaled to the placeholder's tier and
        # culture) binds before the tier target: a top tier target would let a one handed sword
        # swing past any tier's blow, and a tier 4 placeholder may not carry a tier 10 blow either.
        # Run this tool after restat_melee_blades.py: k is relative to the donor's DPS, so a
        # restat that moves the donor moves k.
        # The cap is the tightest tier limit over every row of carrier_rows, not the spec's tier.
        # Each attack has its own damage_factor and is held by its own limits (#1296, as
        # restat_melee_blades.class_ratios does): one factor for both would stop a spear's swing
        # because its thrust is at the thrust limit. An attack the donor lacks follows the other.
        # Each carrier is held in the mode it wields (row_views). A shield view also keeps the whole
        # weapon inside its class limit: a player or a troop without a shield can use the other modes.
        checks = [(ladder.tier_limit(v.class_template, tier, craft, lspec), v)
                  for (tier, craft), ids in rows.items()
                  for v in row_views(donor, ids, shielded, all_held)]
        if shielded:
            checks.append((ladder.class_limit(template, lspec), donor))

        def attack_cap(swing: bool) -> float:
            return min(ladder.max_factor(limit, v.swing_damage, 0, v.swing_dps, 0.0) if swing
                       else ladder.max_factor(limit, 0, v.thrust_damage, 0.0, v.thrust_dps)
                       for limit, v in checks)
        k_swing = min(k, attack_cap(True))
        k_thrust = min(k, attack_cap(False))
        if not donor.swing_damage:
            k_swing = k_thrust
        if not donor.thrust_damage:
            k_thrust = k_swing
        effective = max(aim.swing_dps * k_swing, aim.thrust_dps * k_thrust) / aim.best_dps
        capped = effective < k - 1e-9
        k = effective
        blade_id = donor.blade_piece
        new_blade = f"{wid}_blade"
        piece_xml = element_text(armory / "ModuleData" / "LOTRLOME_crafting_pieces.xml", "CraftingPiece", blade_id)
        piece_xml = piece_xml.replace(f'id="{blade_id}"', f'id="{new_blade}"', 1)
        # The donor's name with runs of spaces closed up (the Armory writes "Iron Hills  Sword Blade A").
        piece_xml = re.sub(r'name="\{=[^}]*\}([^"]*)"', lambda m: f'name="{{={new_blade}_name}}{" ".join(m.group(1).split())} (placeholder)"',
                           piece_xml, count=1)
        # A troop weapon's part, never a smithing choice: hidden on the weapon designer
        # (is_hidden is CraftingPiece.IsHiddenOnDesigner), as the Armory hides its own troop-only parts.
        tag, close, body = piece_xml.partition(">")
        tag = re.sub(r'\s+is_hidden="[^"]*"', '', tag)
        tag = re.sub(r'(\s+)(name="[^"]*")', r'\1\2\1is_hidden="true"', tag, count=1)
        piece_xml = tag + close + body
        piece_xml = scale_factors(piece_xml, k_swing, k_thrust)
        item_xml = element_text(find_item_file(armory, w["donor"]), "CraftedItem", w["donor"])
        item_xml = item_xml.replace(f'id="{w["donor"]}"', f'id="{wid}"', 1)
        item_xml = re.sub(r'name="[^"]*"', f'name="{{={wid}_name}}{w["name"]}"', item_xml, count=1)
        if "is_merchandise=" in item_xml:
            item_xml = re.sub(r'is_merchandise="[^"]*"', 'is_merchandise="false"', item_xml, count=1)
        else:
            item_xml = item_xml.replace("<CraftedItem", '<CraftedItem is_merchandise="false"', 1)
        if item_xml.count(f'id="{blade_id}"') != 1:
            raise PlaceholderError(f"{w['donor']} does not name its blade {blade_id} exactly once")
        item_xml = item_xml.replace(f'id="{blade_id}"', f'id="{new_blade}"')
        descs = sorted(d.id for d in cat.descriptions.values() if blade_id in d.available)
        if not descs:
            raise PlaceholderError(f"no WeaponDescription lists the donor blade {blade_id}")
        for d in descs:
            desc_add.setdefault(d, []).append(new_blade)
        tmpl_add.setdefault(cat.items[w["donor"]].template, []).append(new_blade)
        pieces.append(f"    <!-- {wid}: donor blade {blade_id}, damage_factor swing x{k_swing:.3f}, thrust x{k_thrust:.3f} -->\n    {piece_xml}")
        items.append(f"    <!-- PLACEHOLDER {wid}: look of {w['donor']}, tier {w['tier']} ladder target -->\n    {item_xml}")
        for t in users:
            new = placeholder_for(w["donor"], t.culture, t.tier, spec["weapons"])
            for slot in sorted(t.slots_of.get(w["donor"], ())):  # empty after an earlier --apply
                swaps.append({"file": t.path, "troop": t.id, "slot": slot, "old": w["donor"], "new": new})
        plan.append((w, donor, band, k, new_blade, piece_xml, users, capped))
    return pieces, items, plan, swaps, desc_add, tmpl_add


def render_xslt(tag, parent, child, attr, additions):
    lines = ['<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">',
             '  ' + HEADER.format(tag=tag),
             '  <xsl:output omit-xml-declaration="yes"/>',
             '  <xsl:template match="@*|node()">',
             '    <xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>',
             '  </xsl:template>']
    for key in sorted(additions):
        lines.append(f'  <xsl:template match="{parent}[@id={chr(39)}{key}{chr(39)}]/{child}s">')
        lines.append('    <xsl:copy>')
        lines.append('      <xsl:apply-templates select="@*|node()"/>')
        for piece in sorted(set(additions[key])):
            lines.append(f'      <{child} {attr}="{piece}"/>')
        lines.append('    </xsl:copy>')
        lines.append('  </xsl:template>')
    lines.append('</xsl:stylesheet>')
    return "\r\n".join(lines) + "\r\n"


def render(tag, root, body):
    return ('<?xml version="1.0" encoding="utf-8"?>\r\n' + HEADER.format(tag=tag) + "\r\n<" + root + ">\r\n"
            + "\r\n\r\n".join(body).replace("\r\n", "\n").replace("\n", "\r\n") + "\r\n</" + root + ">\r\n")


def price_new(cat, plan):
    """Price each placeholder with the engine port: the cloned blade parsed from the generated XML."""
    out = {}
    for w, donor, band, k, new_blade, piece_xml, _, _ in plan:
        piece = md.parse_piece(ET.fromstring(piece_xml))
        if piece is None:
            raise PlaceholderError(f"{new_blade} did not parse as a piece")
        cat.pieces[new_blade] = piece
        for did, d in list(cat.descriptions.items()):
            if donor.blade_piece in d.available:
                cat.descriptions[did] = dataclasses.replace(d, available=d.available | {new_blade})
        d = cat.items[w["donor"]]
        pieces = tuple((new_blade if p == donor.blade_piece else p, s, sc) for p, s, sc in d.pieces)
        item = mc.Item(id=w["id"], name=w["name"], culture=d.culture, template=d.template, pieces=pieces, source="ph")
        cat.items[w["id"]] = item
        out[w["id"]] = mc.price(cat, item)
    return out


def submodule_text(text):
    if 'path="placeholder_weapons/ph_items"' in text:
        return text
    if SUBMODULE_ANCHOR not in text:
        raise PlaceholderError("SubModule.xml anchor (long_defeat items node) not found")
    types = "".join(f'        <GameType value="{g}"/>\n' for g in GAME_TYPES)
    block = ("    <!-- Placeholder melee weapons (tools/generate_placeholder_weapons.py): ids start with taom_ph_ -->\n"
             "    <XmlNode>\n      <XmlName id=\"CraftingPieces\" path=\"placeholder_weapons/ph_crafting_pieces\"/>\n"
             f"      <IncludedGameTypes>\n{types}      </IncludedGameTypes>\n    </XmlNode>\n"
             "    <XmlNode>\n      <XmlName id=\"Items\" path=\"placeholder_weapons/ph_items\"/>\n"
             f"      <IncludedGameTypes>\n{types}      </IncludedGameTypes>\n    </XmlNode>\n"
             "    <!-- Stylesheet-only rows (no .xml): they must apply after LOTRLOME_Armory's, which replace these lists -->\n"
             "    <XmlNode>\n      <XmlName id=\"WeaponDescriptions\" path=\"placeholder_weapons/ph_weapon_descriptions\"/>\n"
             "    </XmlNode>\n"
             "    <XmlNode>\n      <XmlName id=\"CraftingTemplates\" path=\"placeholder_weapons/ph_crafting_templates\"/>\n"
             "    </XmlNode>\n")
    nl = "\r\n" if "\r\n" in text else "\n"
    # insert after the XmlNode that holds the anchor
    i = text.index(SUBMODULE_ANCHOR)
    j = text.index("</XmlNode>", i) + len("</XmlNode>")
    return text[:j] + nl + block.replace("\n", nl).rstrip(nl) + text[j:]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--verify", action="store_true")
    args = ap.parse_args(argv)
    modules = report.MODULES
    armory = modules / ARMORY
    if not armory.is_dir():
        print(f"SKIPPED: no Armory at {armory}", file=sys.stderr)
        return 2
    spec, cat, priced, troops, built = plan_from_install(modules)
    pieces, items, plan, swaps, desc_add, tmpl_add = built
    shields = frozenset(mc.load_shield_ids(modules))
    new_prices = price_new(cat, plan)
    lspec = ladder.load_spec()

    pieces_text = render(spec["tag"], "CraftingPieces", pieces)
    items_text = render(spec["tag"], "Items", items)
    desc_text = render_xslt(spec["tag"], "WeaponDescription", "AvailablePiece", "id", desc_add)
    tmpl_text = render_xslt(spec["tag"], "CraftingTemplate", "UsablePiece", "piece_id", tmpl_add)
    for t in (pieces_text, items_text, desc_text, tmpl_text):
        ET.fromstring(t.encode("utf-8"))
    sub_old = SUBMODULE.read_bytes().decode("utf-8")
    sub_new = submodule_text(sub_old)
    ET.fromstring(sub_new.encode("utf-8"))

    print(f"placeholder weapons: {len(plan)}; troop slots to swap: {len(swaps)}")
    for w, donor, band, k, new_blade, _, users, capped in plan:
        p = new_prices[w["id"]]
        shielded, all_held = shield_carriers(users, shields)
        # DPS and band are those of the view the placeholder is banded on (`build`): the shield
        # modes when every carrier has a shield. Swing and thrust are the whole weapon's.
        aim_donor = donor.with_shield() if all_held else donor
        aim_p = p.with_shield() if all_held else p
        print(f"  {w['id']}: donor {w['donor']} {aim_donor.best_dps:.0f} DPS -> {aim_p.best_dps:.0f} DPS"
              + (" in its shield modes" if all_held else "")
              + f" (T{w['tier']} band {band.low:.0f}-{band.high:.0f}, target {band.target:.0f}; x{k:.2f}); "
              f"swing {p.swing_damage}, thrust {p.thrust_damage}; {len(users)} troops: "
              + ", ".join(f"{t.id} T{t.tier}" for t in sorted(users, key=lambda t: (t.tier, t.id))))
        over = row_breaches(p, carrier_rows(w, users, lspec), lspec, shielded, all_held)
        if over:
            raise PlaceholderError(
                f"{w['id']} is over its tier limit: "
                + "; ".join(f"tier {tier} {craft} ({', '.join(ids) or 'the spec row'}): "
                            + ", ".join(f"{s} {v:.0f} > {m}" for s, v, m in b)
                            for (tier, craft), ids, b in over))
        if capped:
            if not aim_donor.best_dps < aim_p.best_dps <= band.high:
                raise PlaceholderError(f"{w['id']} prices at {aim_p.best_dps:.1f}: its tier limit leaves "
                                       "no room above its donor; remove it from the spec")
            print(f"    (tier limit of {aim_p.class_template}: held under the tier target)")
        elif float(w.get("blend", 1.0)) == 1.0:
            if not band.low <= aim_p.best_dps <= band.high:
                raise PlaceholderError(f"{w['id']} prices at {aim_p.best_dps:.1f}, outside its band")
        elif not aim_donor.best_dps < aim_p.best_dps <= band.high:
            raise PlaceholderError(f"{w['id']} prices at {p.best_dps:.1f}: a blend must beat its donor and stay under the band top")
        else:
            print(f"    (blend {w['blend']}: deliberately below the band)")
        if not users:
            raise PlaceholderError(f"{w['id']} has no troop; remove it from the spec")
    faults = placeholder_line_faults(cat, priced, spec, new_prices)
    if faults:
        raise PlaceholderError("a numbered line breaks:\n  " + "\n  ".join(faults))
    swaps, owed = roster.split_generated(swaps)

    stale = []
    if not PIECES_FILE.exists() or PIECES_FILE.read_bytes().decode("utf-8") != pieces_text:
        stale.append(str(PIECES_FILE.relative_to(REPO)))
    if not ITEMS_FILE.exists() or ITEMS_FILE.read_bytes().decode("utf-8") != items_text:
        stale.append(str(ITEMS_FILE.relative_to(REPO)))
    for f, t in ((DESC_XSLT, desc_text), (TEMPLATE_XSLT, tmpl_text)):
        if not f.exists() or f.read_bytes().decode("utf-8") != t:
            stale.append(str(f.relative_to(REPO)))
    if sub_new != sub_old:
        stale.append("Main/_Module/SubModule.xml")
    if swaps:
        stale.append(f"{len(swaps)} troop slots")
    if owed:
        owners = collections.Counter(roster.GENERATED_TROOP_FILES[Path(s["file"]).name] for s in owed)
        stale.append(f"{len(owed)} troop slots in generated files, owed by "
                     + ", ".join(f"{g} --apply ({n})" for g, n in sorted(owners.items())))
    if args.verify:
        print("verify:", "OK" if not stale else "DRIFT: " + "; ".join(stale))
        return 0 if not stale else 1
    if not args.apply:
        print("(dry run; would write: " + ("; ".join(stale) or "nothing") + ")")
        return 0
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    PIECES_FILE.write_bytes(pieces_text.encode("utf-8"))
    ITEMS_FILE.write_bytes(items_text.encode("utf-8"))
    DESC_XSLT.write_bytes(desc_text.encode("utf-8"))
    TEMPLATE_XSLT.write_bytes(tmpl_text.encode("utf-8"))
    if sub_new != sub_old:
        SUBMODULE.write_bytes(sub_new.encode("utf-8"))
    if swaps:
        print(f"wrote {write_changes(swaps)} roster file(s)")
    print("written:", "; ".join(stale) or "nothing")
    if owed:
        print("NEXT: run the generators named above with --apply; this tool does not write their files")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except PlaceholderError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
