"""Generate low-stat career-archetype starter armor for the 12 non-Gondor cultures.

Mirrors the hand-authored Gondor pattern (LOTRLOME_items/gondor/starter_armors.xml):
for each culture x archetype, clone the culture's chest + boots items (so mesh / material /
cover flags / gender variations render correctly), strip their armor numbers, and re-set
ONLY the slot's primary stat to the TAOM starter anchor:

    Ranged = ~5   Cavalry = ~7   Infantry = ~9

Only Body + Leg are authored: the starter kit is chest + legs + weapons (+ mount for
cavalry) by design -- no helmet, shoulders/cape, or gloves. Items carry NO explicit value=,
so DefaultItemValueModel prices them from their (now tiny) tier -> trivial resale. Donors
are the chest/boots items each culture's career roster already uses.

Writes <folder>/starter_armors.xml per culture (auto-loads via the folder's existing
<XmlName id="Items" path="LOTRLOME_items/<folder>"/> registration). Gondor is excluded
(hand-tuned separately).

Usage:
    python tools/generate_starter_armor.py            # dry-run (default)
    python tools/generate_starter_armor.py --apply
    python tools/generate_starter_armor.py --armory-path "<path to LOTRLOME_Armory>"
"""
import argparse
import os
import glob
import xml.etree.ElementTree as ET

DEFAULT_ARMORY = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory"

# (taom_culture_id, folder, body_donor_id, leg_donor_id)  -- body/leg donors = current roster items
CULTURES = [
    ("mordor",    "mordor",     "sk_uruk_mordor_chainmail_light_a",     "sk_md_orc_boots_a"),
    ("isengard",  "isengard",   "sk_uruk_hai_tunic_a1",                 "sk_uruk_hai_shoes_a1"),
    ("gundabad",  "gundabad",   "sk_gb_uruk_chest_light_a",             "sk_gb_uruk_boots_light_a"),
    ("dolguldur", "dol_guldur", "sk_dg_uruk_chest_light_a",             "sk_dg_uruk_boots_light_a"),
    ("erebor",    "erebor",     "sk_dwarf_erebor_chest_leather_light_a","sk_dwarf_erebor_boots_light_a"),
    ("rivendell", "rivendell",  "rivendell_torso_light_light_tier1",    "rivendell_boots_leather1"),
    ("mirkwood",  "mirkwood",   "mkwd_inf3_chest",                      "mirkwood_boots"),
    ("vlandia",   "rohan",      "rohan_militia_tunic_a",                "cts_rohan_boots3"),
    ("empire",    "dunland",    "dunland_caerdh_chainmail_light_a",     "dunland_caerdh_boots_light_a"),
    ("aserai",    "harad",      "harad08_torso",                        "harad08_boots"),
    ("khuzait",   "rhun",       "sk_rh_loke_tunic_a",                   "easterling02_v1_boots"),
    ("sturgia",   "dale",       "sk_dale_chest_archer_a01",             "sk_dale_boots_archer_a01"),
]

# slot stat templates per archetype. body/cape = (body_armor, arm_armor); others = single stat.
TEMPLATES = {
    "ranged":   {"head": 5, "body": (5, 2), "leg": 5, "cape": (4, 1), "gloves": 4},
    "cavalry":  {"head": 7, "body": (7, 3), "leg": 7, "cape": (5, 2), "gloves": 5},
    "infantry": {"head": 9, "body": (9, 4), "leg": 9, "cape": (6, 2), "gloves": 6},
}

CULTURE_DISPLAY = {
    "mordor": "Mordor", "isengard": "Isengard", "gundabad": "Gundabad",
    "dolguldur": "Dol Guldur", "erebor": "Erebor", "rivendell": "Rivendell",
    "mirkwood": "Mirkwood", "vlandia": "Rohan", "empire": "Dunland",
    "aserai": "Harad", "khuzait": "Rhun", "sturgia": "Dale",
}
ARCH_DISPLAY = {"ranged": "Ranged", "cavalry": "Cavalry", "infantry": "Infantry"}
SLOT_DISPLAY = {"head": "Helm", "body": "Armor", "leg": "Boots", "cape": "Cloak", "gloves": "Gloves"}
SLOT_TYPE = {"head": "HeadArmor", "body": "BodyArmor", "leg": "LegArmor",
             "cape": "Cape", "gloves": "HandArmor"}
ARMOR_STAT_KEYS = ("head_armor", "body_armor", "leg_armor", "arm_armor")


class ItemRec:
    __slots__ = ("attrib", "armor", "flags", "type")

    def __init__(self, el):
        self.attrib = dict(el.attrib)
        self.type = el.get("Type")
        armor = el.find(".//Armor")
        self.armor = dict(armor.attrib) if armor is not None else None
        flags = el.find("Flags")
        self.flags = dict(flags.attrib) if flags is not None else None


def load_folder(folder_path):
    """Return (by_id, by_type) over all item defs in the folder."""
    by_id, by_type = {}, {}
    for f in glob.glob(os.path.join(folder_path, "*.xml")):
        try:
            root = ET.parse(f).getroot()
        except ET.ParseError:
            continue
        for el in root.iter():
            if el.tag in ("Item", "CraftedItem") and el.get("id"):
                rec = ItemRec(el)
                by_id.setdefault(el.get("id"), rec)
                if rec.type:
                    by_type.setdefault(rec.type, []).append(rec)
    return by_id, by_type


def primary_stat(rec, key):
    try:
        return float(rec.armor.get(key, 0)) if rec.armor else 0.0
    except (TypeError, ValueError):
        return 0.0


def lowest_of_type(by_type, item_type, stat_key):
    """Lowest-stat item of a Type that actually has the stat (only its mesh is borrowed)."""
    cands = [r for r in by_type.get(item_type, []) if r.armor and r.armor.get(stat_key)]
    if not cands:
        return None
    return min(cands, key=lambda r: primary_stat(r, stat_key))


def armor_overrides(slot, tmpl):
    v = tmpl[slot]
    if slot == "head":
        return {"head_armor": str(v)}
    if slot == "leg":
        return {"leg_armor": str(v)}
    if slot == "gloves":
        return {"arm_armor": str(v)}
    if slot == "body":
        return {"body_armor": str(v[0]), "arm_armor": str(v[1])}
    if slot == "cape":
        return {"body_armor": str(v[0]), "arm_armor": str(v[1])}
    raise ValueError(slot)


def build_item_xml(donor, new_id, new_name, taom_culture, slot, overrides):
    # item attributes: copy donor, override identity + culture + merchandise
    attrs = dict(donor.attrib)
    attrs["id"] = new_id
    attrs["name"] = new_name
    attrs["culture"] = "Culture.%s" % taom_culture
    attrs["is_merchandise"] = "true"
    if donor.type:
        attrs["Type"] = donor.type
    # armor: copy donor, strip numeric stats, apply overrides
    aattrs = dict(donor.armor) if donor.armor else {}
    for k in ARMOR_STAT_KEYS:
        aattrs.pop(k, None)
    aattrs.update(overrides)

    order = ["id", "name", "subtype", "mesh", "body_name", "culture",
             "is_merchandise", "weight", "difficulty", "appearance", "Type"]
    ordered = [(k, attrs[k]) for k in order if k in attrs]
    ordered += [(k, v) for k, v in attrs.items() if k not in order]
    item_attr_str = " ".join('%s="%s"' % (k, v) for k, v in ordered)

    aorder = ["head_armor", "body_armor", "leg_armor", "arm_armor",
              "has_gender_variations", "covers_body", "covers_legs", "covers_hands",
              "hair_cover_type", "beard_cover_type", "mane_cover_type",
              "modifier_group", "material_type", "family_type"]
    aordered = [(k, aattrs[k]) for k in aorder if k in aattrs]
    aordered += [(k, v) for k, v in aattrs.items() if k not in aorder]
    armor_str = " ".join('%s="%s"' % (k, v) for k, v in aordered)

    lines = ['    <Item %s>' % item_attr_str,
             '        <ItemComponent>',
             '            <Armor %s />' % armor_str,
             '        </ItemComponent>']
    if donor.flags:
        flag_str = " ".join('%s="%s"' % (k, v) for k, v in donor.flags.items())
        lines.append('        <Flags %s />' % flag_str)
    lines.append('    </Item>')
    return "\n".join(lines)


def generate_culture(taom, folder, body_donor, leg_donor, armory):
    folder_path = os.path.join(armory, "ModuleData", "LOTRLOME_items", folder)
    if not os.path.isdir(folder_path):
        return None, ["MISSING folder %s" % folder_path]
    by_id, by_type = load_folder(folder_path)
    warnings = []

    donors = {}
    donors["body"] = by_id.get(body_donor)
    donors["leg"] = by_id.get(leg_donor)
    if donors["body"] is None:
        warnings.append("body donor %s not found" % body_donor)
    if donors["leg"] is None:
        warnings.append("leg donor %s not found" % leg_donor)

    blocks = []
    for arch in ("ranged", "cavalry", "infantry"):
        tmpl = TEMPLATES[arch]
        blocks.append("\n    <!-- %s %s -->" % (CULTURE_DISPLAY[taom], ARCH_DISPLAY[arch]))
        for slot in ("body", "leg"):
            donor = donors[slot]
            if donor is None:
                continue
            new_id = "starter_%s_%s_%s_a" % (arch, taom, slot)
            new_name = "{=%s}%s %s %s" % (new_id, CULTURE_DISPLAY[taom],
                                          ARCH_DISPLAY[arch], SLOT_DISPLAY[slot])
            blocks.append(build_item_xml(donor, new_id, new_name, taom, slot,
                                         armor_overrides(slot, tmpl)))

    donor_summary = {s: (donors[s].attrib.get("id") if donors[s] else None)
                     for s in ("body", "leg")}
    header = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!--\n'
        '  TAOM career-archetype starter armor for %s. GENERATED by\n'
        '  tools/generate_starter_armor.py (do not hand-edit; re-run the generator).\n'
        '  Clones culture items (mesh/material/cover flags borrowed), armor re-set to the\n'
        '  starter anchors Ranged~5 / Cavalry~7 / Infantry~9. No explicit value= -> trivial\n'
        '  computed resale. Donors: %s\n'
        '-->\n'
        '<Items>\n' % (CULTURE_DISPLAY[taom], donor_summary)
    )
    content = header + "\n".join(blocks) + "\n\n</Items>\n"
    return content, warnings


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write files (default: dry-run)")
    ap.add_argument("--armory-path", default=DEFAULT_ARMORY)
    args = ap.parse_args()

    total_items = 0
    for taom, folder, body_donor, leg_donor in CULTURES:
        content, warnings = generate_culture(taom, folder, body_donor, leg_donor, args.armory_path)
        n = content.count("<Item ") if content else 0
        total_items += n
        print("=" * 60)
        print("%-10s (%s): %d items" % (taom, folder, n))
        for w in warnings:
            print("  WARN:", w)
        if content is None:
            continue
        out_path = os.path.join(args.armory_path, "ModuleData", "LOTRLOME_items",
                                folder, "starter_armors.xml")
        if args.apply:
            if os.path.exists(out_path):
                bak = out_path + ".bak-startergear"
                if not os.path.exists(bak):
                    os.replace(out_path, bak)
            with open(out_path, "w", encoding="utf-8") as fh:
                fh.write(content)
            print("  WROTE", out_path)
        else:
            # show first item as a sample
            sample = content.split("\n    <Item ", 1)
            if len(sample) > 1:
                print("  sample:\n    <Item " + sample[1].split("</Item>")[0] + "</Item>")
    print("=" * 60)
    print("TOTAL: %d items across %d cultures (%s)" %
          (total_items, len(CULTURES), "APPLIED" if args.apply else "DRY-RUN"))


if __name__ == "__main__":
    main()
