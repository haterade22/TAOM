#!/usr/bin/env python3
"""Generate the lord armour variants: Armory looks at lord armour values for the lord templates (#1333).

WHY
---
A lord wears one whole battle set, and the armour restat prices each Armory piece by its lowest
troop wearer (rebalance_armor._roster_first_plan). The looks a culture's lords wore before the
2026-11-15 kit pass (Mirkwood's plain, bronze and palace-guard sets, Harad's infantry chests) are
also worn by low troops, so they sit at 3 to 27 armour where a lord needs about 40 to 50. Raising
the Armory piece would raise every troop in it. A variant is the third route: a NEW item in the
TAOM module with the source's look and the lord band's armour, worn only by the lord templates.

WHAT A VARIANT IS
-----------------
A copy of one Armory armour item (mesh, culture, weight, appearance, cover and gender flags,
material_type, Flags and every other attribute or child, as the source declares them) with:
  * the id `taom_lord_<source id>` (derive_armor_tiers.LORD_VARIANT_PREFIX);
  * is_merchandise="false": no workshop makes it (WorkshopsCampaignBehavior.IsProducable), no
    loot roll gives it (DefaultBattleRewardModel.GetRandomItem), no tournament offers it;
  * its own name key `taom_lord_<source id>_name` and the source's English name with "Lord's"
    after the culture tag and without the tier words Light, Med, Medium, Heavy and Elite (a
    variant is at lord values); names two variants would share are numbered I, II, ... in spec
    order (unique_names);
  * armour at the lord band of the source's kingdom line (rebalance_armor.calculate_stats('lord')),
    or at the spec row's cap where the lord band would pass the culture's limit: the slot's
    primary; each secondary at the larger of the source's own share and the share of the
    culture's reference piece in that slot (kingdom_secondary, as the restat prices a lord piece);
    and where the source has no share, at least the row secondary the curve gives the slot (a
    chest's leg, a cape's arm), as generate_lothlorien_armor writes it. The reference piece is the
    one the troop that sets the culture's reference on the slot's part wears there
    (fix_lord_kits.culture_reference; a Cape's part is body): a lord does not lose arm armour
    because his chest's look carries no arm share. modifier_group follows the lord band
    (modifier_group_for); material_type and weight stay the source's.
A variant is made only for a piece whose primary the curve leaves short of the lord band (or the
cap) for good (fix_lord_kits.is_short): a piece a troop wears (derive_armor_tiers.parse_rosters),
or a piece no troop wears that has no lord-line token (the restat only lowers such a piece a lord
wears). An unworn lord-line piece is restated at the lord band, and a piece at lord values is worn
as it is. The refusal tests the lord-line token only, while the restat raises such a piece only when
it is also not hero-named (rebalance_armor.is_excluded): an unworn hero-named lord-line piece is
refused too, on the safe side. The class table classes every variant `named`
(generate_armour_classes.classify), so no shop, armoury upgrade, Lord's Ladder rung, harness or strip
of a captive lord hands one out. A variant is an id the spec lists (derive_armor_tiers.lord_variant_source),
not any id with the prefix.

WHAT IT WRITES (the unversioned Armory is only read)
---------------------------------------------------
  Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml   one Item per spec row, CRLF
  Main/_Module/SubModule.xml                                    one Items XmlNode, added once
The lord templates (equipmentsets/taom_equipment_sets_<culture>.xml) are edited by hand or by
fix_lord_kits.py, never here. Only a generic lord template or a fallback or ruler roster may name
a variant, and only a lord may wear one: --check fails when any other roster, *.xml or *.xslt names
one (roster_violations), and when a troop, companion, wanderer or other hero who is not a lord wears
one, inline or through a template roster by id (worn_violations).

The spec is tools/lord_armour_variants.json: per template culture, the source ids with a reason,
and an optional "cap": {"value": the highest primary, "reason": why}; a cap must bind (be under
the lord band) and stay at or under the culture's limit on the slot's part (its reference plus
fix_lord_kits.CEILING_MARGIN, check_cap), so a changed reference fails --check.

USAGE
    python tools/generate_lord_armour_variants.py            # dry run: the plan
    python tools/generate_lord_armour_variants.py --apply    # write
    python tools/generate_lord_armour_variants.py --check    # exit 1 on drift or a bad roster

Exit codes: 0 clean or written; 1 drift (--check), a bad spec or a roster that may not wear a
variant; 2 no Armory install (nothing checked); 3 a crash. After --apply the new items load only at
a full game restart, then python tools/derive_armor_tiers.py and generate_armour_classes.py --apply.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import traceback
import xml.etree.ElementTree as ET
from collections import namedtuple
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import derive_armor_tiers as dat  # noqa: E402
import fix_lord_kits as lk  # noqa: E402
import rebalance_armor as ra  # noqa: E402

SPEC = Path(dat.LORD_VARIANTS_SPEC)
SUBMODULE = REPO / "Main" / "_Module" / "SubModule.xml"
MODULEDATA = Path(dat.MODULEDATA_DIR)
SUBMODULE_ANCHOR = '      <XmlName id="Items" path="placeholder_weapons/ph_items"/>'
SUBMODULE_PATH = "lord_armour/lord_armour_variants"
GAME_TYPES = ("Campaign", "CampaignStoryMode", "CustomGame", "EditorGame")
ARMOUR_STATS = ("head_armor", "body_armor", "arm_armor", "leg_armor")
LORD_MARK = "Lord's "
# The Armory's tier words ("Med" is its short form of Medium); a variant's name drops them.
TIER_WORDS = frozenset(("light", "med", "medium", "heavy", "elite"))
ROMAN = ("I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X")
EQUIP_SLOT = {slot: equip for equip, slot in dat.EQUIP_SLOT_MAP.items()}
# A culture's lists of templates whose heroes are lords (spcultures.xml).
LORD_TEMPLATE_LISTS = ("lord_templates", "rebellion_hero_templates")
HEADER = ("<!-- GENERATED by tools/generate_lord_armour_variants.py from tools/lord_armour_variants.json and the"
          " live LOTRLOME_Armory. Do not edit by hand. Each item copies an Armory piece (id taom_lord_<source>)"
          " at the lord band for the lord templates only: docs/features/armor-balance.md -->")

Source = namedtuple("Source", "folder slot declared xml name")


class VariantError(Exception):
    pass


# =============================================================================
# The spec
# =============================================================================

def load_spec(path=None):
    try:
        return json.loads(Path(path or SPEC).read_bytes().decode("utf-8"))
    except (OSError, UnicodeDecodeError, ValueError) as exc:
        raise VariantError(f"the spec {path or SPEC} cannot be read: {exc}") from exc


def validate_spec(spec):
    cultures = spec.get("cultures") if isinstance(spec, dict) else None
    if not isinstance(cultures, dict) or not cultures:
        raise VariantError("the spec has no 'cultures' table")
    seen = {}
    for culture, rows in cultures.items():
        for row in rows or ():
            src, why = row.get("source"), (row.get("reason") or "").strip()
            if not src or not why:
                raise VariantError(f"{culture}: every row needs a source and a reason ({row})")
            if src in seen:
                raise VariantError(f"{src} is listed for {seen[src]} and {culture}: one culture per variant")
            seen[src] = culture
            if "cap" in row:
                _validate_cap(src, row["cap"])
    return spec


def _validate_cap(source_id, cap):
    """A cap is {"value": a whole number above 0, "reason": why the plain lord band is too high}."""
    value = cap.get("value") if isinstance(cap, dict) else None
    reason = (cap.get("reason") or "").strip() if isinstance(cap, dict) else ""
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0 or not reason:
        raise VariantError(f"{source_id}: a cap is {{\"value\": a whole number above 0, \"reason\": ...}} ({cap})")


def cap_of(row):
    return row["cap"]["value"] if "cap" in row else None


def variant_id(source_id):
    return dat.LORD_VARIANT_PREFIX + source_id


# =============================================================================
# Values
# =============================================================================

def lord_values(slot, folder, source_id, declared, shares=None, cap=None):
    """{stat: value} the variant's Armor element carries (see the module docstring). `shares` is
    {stat: share of the primary} of the culture's reference piece in this slot (culture_shares);
    `cap` an upper bound on the primary from the spec, which must bind."""
    if ra.kingdom_key(source_id, folder) is None:
        raise VariantError(f"{source_id} ({folder}) has no kingdom cap, so the curve has no lord band for it")
    row = ra.calculate_stats("lord", slot, folder, item_id=source_id)
    primary_stat = ra.GOVERNED_STATS[slot][0]
    primary = row[primary_stat]
    if cap is not None:
        if cap >= primary:
            raise VariantError(f"{source_id}: the cap {cap} does not bind (the lord band is {primary}); drop it")
        primary = cap
    old_primary = declared.get(primary_stat, 0)
    out = {primary_stat: primary}
    for stat in ARMOUR_STATS:
        if stat == primary_stat:
            continue
        cur = declared.get(stat)
        own = cur / old_primary if cur is not None and cur > 0 and old_primary > 0 else 0
        share = max(own, (shares or {}).get(stat, 0))
        value = ra.kingdom_secondary(slot, stat, "lord", primary, share) if share > 0 else 0
        if own <= 0 and row.get(stat, 0) > 0:
            value = max(value, row[stat])
        if value > 0 or cur is not None:
            out[stat] = value
    return out


def reference_shares(troop, items, slot):
    """{secondary stat: share of the primary} of the piece `troop` wears in `slot` (a rebalance_armor
    slot): of its battle sets' pieces there, the one with the lowest primary (the piece the troop's
    worst case, so the culture reference, counts), the lowest id on a tie. {} when there is none."""
    equip = EQUIP_SLOT[slot]
    primary_stat = ra.GOVERNED_STATS[slot][0]
    pieces = sorted({st.get(equip) for st in (troop or {}).get("sets", ()) if st.get(equip) in items})
    if not pieces:
        return {}
    stats = {iid: items[iid].get("stats", {}) for iid in pieces}
    piece = min(pieces, key=lambda iid: (stats[iid].get(primary_stat, 0), iid))
    primary = stats[piece].get(primary_stat, 0)
    if primary <= 0:
        return {}
    return {s: v / primary for s, v in stats[piece].items() if s != primary_stat and s in ARMOUR_STATS and v > 0}


def culture_shares(troops, items, culture):
    """{slot: reference_shares} for every slot, read from the troop that sets the culture's reference
    on the slot's part (fix_lord_kits.culture_reference and SLOT_PART: a cape's part is body)."""
    ref = lk.culture_reference(troops, items, culture)
    if not ref:
        return {}
    return {slot: reference_shares(troops.get(ref[lk.SLOT_PART[equip]][1]), items, slot)
            for slot, equip in EQUIP_SLOT.items()}


def check_cap(source_id, slot, cap, reference):
    """Raise when a spec cap passes the culture's limit on the slot's part (the reference plus
    fix_lord_kits.CEILING_MARGIN; a Cape's part is body), or cannot be judged for want of a reference:
    the cap is a copy of that limit, and a changed reference must not leave it stale."""
    if cap is None:
        return
    part = lk.SLOT_PART[EQUIP_SLOT[slot]]
    if not reference or part not in reference:
        raise VariantError(f"{source_id}: the culture has no {part} reference, so its cap {cap} cannot be judged")
    limit = reference[part][0] + lk.CEILING_MARGIN
    if cap > limit:
        raise VariantError(f"{source_id}: the cap {cap} is over the {part} limit {limit} (the reference "
                           f"{reference[part][0]} of {reference[part][1]} plus {lk.CEILING_MARGIN})")


def check_needed(source_id, slot, folder, declared, record, cap=None):
    """Raise unless the curve leaves the source short of the lord band (or of the spec's cap) for
    good: a troop wears it (it is priced at its lowest wearer's band), or no troop does and it is no
    lord-line piece (the restat only lowers such a piece a lord wears, rebalance_armor._roster_first_plan).
    An unworn lord-line piece a lord wears is restated at the lord band, so it needs no variant."""
    if (not record or record.get("anchorLevel") is None) and ra.is_lord_kit(source_id):
        raise VariantError(f"{source_id}: no troop wears it and it is lord-line kit, so the restat prices it "
                           "at the lord band once a lord wears it; no variant is needed")
    primary_stat = ra.GOVERNED_STATS[slot][0]
    have = declared.get(primary_stat, 0)
    want = lord_values(slot, folder, source_id, declared, cap=cap)[primary_stat]
    if not lk.is_short(have, want):
        raise VariantError(f"{source_id}: {primary_stat} {have} is within the tolerance of the lord band "
                           f"{want}; wear it as it is")


def lord_name(display):
    """The source's display name with "Lord's" after the culture tag and no tier word: a variant is
    at lord values, so "Light" or "Heavy" would misname it. The look letter or numeral stays."""
    m = re.match(r"^(\[[^\]]*\]\s*)?(.*)$", display)
    rest = " ".join(w for w in m.group(2).split() if w.lower() not in TIER_WORDS)
    return (m.group(1) or "") + LORD_MARK + rest


def unique_names(names):
    """`names` in order, each name two or more variants share numbered I, II, ... in that order."""
    count = {n: names.count(n) for n in names}
    seen = {}
    out = []
    for n in names:
        if count[n] > 1:
            seen[n] = seen.get(n, 0) + 1
            n = f"{n} {ROMAN[seen[n] - 1]}"
        out.append(n)
    clash = sorted({n for n in out if out.count(n) > 1})
    if clash:
        raise VariantError(f"two variants are named {clash[0]}: a numbered name meets a plain one")
    return out


# =============================================================================
# The Armory source
# =============================================================================

def _inside_comment(text, pos):
    start = text.rfind("<!--", 0, pos)
    return start != -1 and text.find("-->", start, pos) == -1


def _read_strict(path):
    raw = Path(path).read_bytes()
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise VariantError(f"{path} is not UTF-8 ({exc}); nothing read from it") from exc


def find_source(armory_items, source_id):
    """The live <Item> of `source_id` in the Armory's five slot files, skipping commented copies."""
    pat = re.compile(r'<Item\b[^>]*?\bid="%s"[\s\S]*?</Item>' % re.escape(source_id))
    for folder in sorted(os.listdir(armory_items)):
        for fn, slot in ra.SLOT_TYPES.items():
            path = Path(armory_items) / folder / fn
            if not path.is_file():
                continue
            text = _read_strict(path)
            if f'id="{source_id}"' not in text:
                continue
            for m in pat.finditer(text):
                if _inside_comment(text, m.start()):
                    continue
                xml = m.group(0)
                elem = ET.fromstring(xml)
                armor = elem.find(".//Armor")
                if armor is None:
                    raise VariantError(f"{source_id} in {path} has no Armor element")
                declared = {k: int(armor.get(k)) for k in ARMOUR_STATS if armor.get(k) is not None}
                return Source(folder, slot, declared, xml, ra.get_display_name(elem.get("name", "")))
    raise VariantError(f"{source_id} is not an armour item of the Armory under {armory_items}")


# =============================================================================
# Rendering
# =============================================================================

def majority_newline(text):
    crlf = text.count("\r\n")
    return "\r\n" if crlf >= text.count("\n") - crlf else "\n"


def _set_attr(xml, name, value, after_pattern):
    """Set attribute `name` on the first match; insert it after the last `after_pattern` attribute
    (same indentation) when the source does not declare it."""
    pat = re.compile(r'(\b%s=")[^"]*(")' % re.escape(name))
    if pat.search(xml):
        return pat.sub(lambda m: m.group(1) + str(value) + m.group(2), xml, count=1)
    anchors = list(re.finditer(r'(\s+)(%s)="[^"]*"' % after_pattern, xml))
    if not anchors:
        raise VariantError(f"no place to write {name}")
    last = anchors[-1]
    return xml[:last.end()] + f'{last.group(1)}{name}="{value}"' + xml[last.end():]


def render_item(source_xml, source_id, values, modifier_group, name=None):
    """The variant's <Item>; `name` is its English name (default: lord_name of the source's)."""
    vid = variant_id(source_id)
    xml = source_xml.replace(f'id="{source_id}"', f'id="{vid}"', 1)
    m = re.search(r'\bname="(?:\{=[^}]*\})?([^"]*)"', xml)
    if not m:
        raise VariantError(f"{source_id} has no name")
    xml = xml[:m.start()] + f'name="{{={vid}_name}}{name or lord_name(m.group(1))}"' + xml[m.end():]
    if re.search(r'\bis_merchandise="', xml):
        xml = re.sub(r'(\bis_merchandise=")[^"]*(")', r'\g<1>false\g<2>', xml, count=1)
    else:
        xml = re.sub(r'(\bid="%s")' % re.escape(vid), r'\1 is_merchandise="false"', xml, count=1)
    head, sep, tail = xml.partition("<Armor")
    if not sep:
        raise VariantError(f"{source_id} has no Armor element")
    for stat in ARMOUR_STATS:
        if stat in values:
            tail = _set_attr(tail, stat, values[stat], "(?:%s)" % "|".join(ARMOUR_STATS))
    tail = _set_attr(tail, "modifier_group", modifier_group, "(?:%s)" % "|".join(ARMOUR_STATS))
    return head + sep + tail


def render_file(blocks, newline="\r\n"):
    """blocks: [(source id, comment, item xml)] in spec order."""
    body = []
    for source_id, comment, xml in blocks:
        body.append(f"    <!-- {variant_id(source_id)}: {comment} -->\n    {xml.lstrip()}")
    text = ('<?xml version="1.0" encoding="utf-8"?>\n' + HEADER + "\n<Items>\n" + "\n\n".join(body)
            + "\n</Items>\n")
    return text.replace("\r\n", "\n").replace("\n", newline)


def submodule_text(text):
    if f'path="{SUBMODULE_PATH}"' in text:
        return text
    if SUBMODULE_ANCHOR not in text:
        raise VariantError("SubModule.xml anchor (the placeholder weapons' Items node) not found")
    types = "".join(f'        <GameType value="{g}"/>\n' for g in GAME_TYPES)
    block = ("    <!-- Lord armour variants (tools/generate_lord_armour_variants.py): ids start with taom_lord_ -->\n"
             f'    <XmlNode>\n      <XmlName id="Items" path="{SUBMODULE_PATH}"/>\n'
             f"      <IncludedGameTypes>\n{types}      </IncludedGameTypes>\n    </XmlNode>\n")
    nl = majority_newline(text)
    i = text.index(SUBMODULE_ANCHOR)
    j = text.index("</XmlNode>", i) + len("</XmlNode>")
    return text[:j] + nl + block.replace("\n", nl).rstrip(nl) + text[j:]


# =============================================================================
# Who may wear a variant
# =============================================================================

_ROSTER_OPEN_RE = re.compile(r'<EquipmentRoster\b[^>]*?\bid="([^"]+)"')


def roster_violations(moduledata, variant_ids):
    """[(file, roster id or '', variant id)] for every reference to a variant outside a generic lord
    template or a fallback or ruler roster. A variant named outside an EquipmentRoster (a troop's
    inline set, a character file) reports roster ''."""
    out = []
    variant_ids = set(variant_ids)
    paths = sorted(list(Path(moduledata).rglob("*.xml")) + list(Path(moduledata).rglob("*.xslt")))
    for path in paths:
        if Path(path).resolve() == Path(dat.LORD_VARIANTS_XML).resolve():
            continue
        text = Path(path).read_bytes().decode("utf-8-sig", errors="replace")
        if dat.LORD_VARIANT_PREFIX not in text:
            continue
        for m in re.finditer(r'Item\.(%s[A-Za-z0-9_]+)' % re.escape(dat.LORD_VARIANT_PREFIX), text):
            if m.group(1) not in variant_ids or _inside_comment(text, m.start()):
                continue
            opens = list(_ROSTER_OPEN_RE.finditer(text, 0, m.start()))
            closed = opens and text.find("</EquipmentRoster>", opens[-1].end(), m.start()) != -1
            roster = opens[-1].group(1) if opens and not closed else ""
            if roster and (lk.GENERIC_TEMPLATE_RE.match(roster) or lk.FALLBACK_ROSTER_RE.match(roster)):
                continue
            out.append((str(path), roster, m.group(1)))
    return out


def worn_violations(moduledata, variant_ids):
    """[(file, wearer id, variant id)] for every troop or hero who is not a lord and wears a variant in
    a battle set, inline or through a template roster by id (<EquipmentSet id="..."/>), as
    derive_armor_tiers._hero_battle_items resolves it: every NPCCharacter of the ModuleData's *.xml
    (troops, companions, wanderer templates, any character file) and every lords.xslt template that
    is not a lord's. A lord is a hero derive_armor_tiers._hero_elements(lords_only=True) yields, or a
    template a culture lists in <lord_templates> or <rebellion_hero_templates> (every hero made from
    it is a lord: spc_<culture>_lord_<n> wear the fallback rosters)."""
    variant_ids = set(variant_ids)
    templates = dat.load_battle_templates()
    lords = {hid for hid, _ in dat._hero_elements(lords_only=True)}
    elements = []
    for path in sorted(Path(moduledata).rglob("*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        elements.extend((str(path), npc.get("id", ""), npc) for npc in root.iter("NPCCharacter"))
        for tag in LORD_TEMPLATE_LISTS:
            for listed in root.iter(tag):
                lords.update(t.get("name", "").split(".", 1)[-1] for t in listed.iter("template"))
    elements.extend((dat.LORDS_XSLT, hid, el) for hid, el in dat._hero_elements()
                    if el.tag == dat.XSL_NS + "template")
    out = set()
    for path, wearer, element in elements:
        if wearer in lords:
            continue
        for vid in dat._hero_battle_items(element, templates) & variant_ids:
            out.add((path, wearer, vid))
    return sorted(out)


def wearer_violations(moduledata, variant_ids):
    """roster_violations and worn_violations together: what --check fails on."""
    return sorted(set(roster_violations(moduledata, variant_ids)) | set(worn_violations(moduledata, variant_ids)))


# =============================================================================
# Main
# =============================================================================

def reference_inputs(game_modules):
    """(reference troops, live item armour) as fix_lord_kits reads them."""
    import fix_upgrade_armour_regressions as fu
    import taom_schema as ts
    exempt = set(ts.Validator._ARMOUR_LADDER_EXEMPT) | set(ts.Validator._BODYLESS_BY_DESIGN)
    troops = lk.reference_troops(lk.load_troop_pool(dat.MODULEDATA_DIR), exempt)
    return troops, fu.load_item_armour(str(game_modules))


def _source_display(xml):
    m = re.search(r'\bname="(?:\{=[^}]*\})?([^"]*)"', xml)
    return m.group(1) if m else ""


def plan(spec, armory_items, troops, items):
    """[(culture, source id, reason, Source, values, modifier group, wearer level, name)] in spec order."""
    wearers = dat.parse_rosters()
    rows = []
    for culture, entries in spec["cultures"].items():
        shares = culture_shares(troops, items, culture)
        reference = lk.culture_reference(troops, items, culture)
        for row in entries:
            src = row["source"]
            found = find_source(armory_items, src)
            check_cap(src, found.slot, cap_of(row), reference)
            levels = sorted({w["level"] for w in wearers.get(src, ())})
            check_needed(src, found.slot, found.folder, found.declared, {"anchorLevel": levels[0] if levels else None},
                         cap=cap_of(row))
            values = lord_values(found.slot, found.folder, src, found.declared, shares.get(found.slot), cap_of(row))
            rows.append([culture, src, row["reason"], found, values, ra.modifier_group_for(found.slot, "lord"),
                         levels[0] if levels else None, lord_name(_source_display(found.xml))])
    for row, name in zip(rows, unique_names([r[7] for r in rows])):
        row[7] = name
    return [tuple(r) for r in rows]


def _fmt(values):
    return "/".join(str(values.get(k, "-")) for k in ARMOUR_STATS)


def main(argv=None):
    import rebalance_troops as rb
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--apply", action="store_true", help="write the variants file and the SubModule node")
    ap.add_argument("--check", action="store_true", help="exit 1 when a file differs from the render")
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES,
                    help=".../Mount & Blade II Bannerlord/Modules (the Armory is read from it)")
    args = ap.parse_args(argv)
    armory_items = Path(args.game_modules) / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items"
    if not armory_items.is_dir():
        print(f"SKIPPED: no LOTRLOME_Armory items under {args.game_modules}; nothing was checked.")
        return 2
    spec = validate_spec(load_spec())
    troops, items = reference_inputs(args.game_modules)
    rows = plan(spec, armory_items, troops, items)
    out_path = Path(dat.LORD_VARIANTS_XML)
    old_text = _read_strict(out_path) if out_path.exists() else None
    newline = majority_newline(old_text) if old_text else "\r\n"
    blocks = []
    print(f"Lord armour variants: {len(rows)} (head/body/arm/leg, the source -> the variant)")
    for culture, src, reason, found, values, group, level, name in rows:
        print(f"  {culture:<10} {variant_id(src):<46} {found.folder:<9} {found.slot:<8} "
              f"{_fmt(found.declared)} -> {_fmt(values)}  ({'worn from L%d' % level if level else 'no troop'})  {name}")
        comment = (f"{found.folder} {found.slot} {src} {_fmt(found.declared)} at the lord band "
                   f"{_fmt(values)} (head/body/arm/leg); {culture}: {reason}")
        blocks.append((src, comment.replace("--", "-"),
                       render_item(found.xml.replace("\r\n", "\n"), src, values, group, name)))
    new_text = render_file(blocks, newline)
    ET.fromstring(new_text.encode("utf-8"))
    sub_bom = SUBMODULE.read_bytes().startswith(b"\xef\xbb\xbf")
    sub_old = _read_strict(SUBMODULE)
    sub_new = submodule_text(sub_old)
    ET.fromstring(sub_new.encode("utf-8"))

    bad = wearer_violations(MODULEDATA, {variant_id(r[1]) for r in rows})
    for path, who, vid in bad:
        print(f"ERROR: {Path(path).relative_to(REPO) if Path(path).is_relative_to(REPO) else path} "
              f"{who or '(no roster)'} names or wears {vid}: only a lord, through a generic lord template or "
              "a fallback or ruler roster, may wear a lord armour variant")
    stale = []
    if old_text != new_text:
        stale.append(str(out_path.relative_to(REPO)))
    if sub_new != sub_old:
        stale.append("Main/_Module/SubModule.xml")
    if args.check:
        print("check:", "OK" if not stale else "DRIFT: " + "; ".join(stale))
        return 1 if stale or bad else 0
    if not args.apply:
        print("(dry run; would write: " + ("; ".join(stale) or "nothing") + ")")
        return 1 if bad else 0
    if stale:
        out_path.parent.mkdir(parents=True, exist_ok=True)
        if old_text != new_text:
            out_path.write_bytes(new_text.encode("utf-8"))
        if sub_new != sub_old:
            SUBMODULE.write_bytes((b"\xef\xbb\xbf" if sub_bom else b"") + sub_new.encode("utf-8"))
    print("written:", "; ".join(stale) or "nothing")
    return 1 if bad else 0


def entry(argv=None):
    """main() with a crash kept apart from drift: a traceback exits 3, a spec or source error 1."""
    try:
        return main(argv)
    except VariantError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    except Exception:
        traceback.print_exc()
        return 3


if __name__ == "__main__":
    sys.exit(entry())
