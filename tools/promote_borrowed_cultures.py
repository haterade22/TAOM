#!/usr/bin/env python3
"""Promote a kingdom that BORROWS another culture into a culture of its own.

Two TAOM kingdoms shipped without a culture: `bluecraig` runs on Culture.goblin and `lindon` on
Culture.rivendell (taom_spkingdoms.xml). They are therefore real factions on the map that are
indistinguishable from their host at character creation — picking Blue Craig starts the player in
Goblin-town, picking Lindon starts them in Rivendell, because the starting settlement is a property
of the CULTURE, not of the faction-map region clicked.

This script writes the culture-DATA half of the promotion. It deliberately does NOT retag the
existing kingdom/clan/lord/settlement rows onto the new culture — that is a separate, ordered step,
because a culture that owns no settlement at runtime makes vanilla SpawnLordParty throw on the daily
clan tick (#374, LANDLESS_CULTURE). Data first, then the retag, never the reverse.

WHY NOT REUSE insert_new_factions.py: that script is hardcoded to clone `gundabad` and applies
orc-only transforms (remap_orc_armor / remap_orc_weapons, a race swap off race="pale_uruk"). Lindon
clones rivendell — elves — and every one of those transforms is wrong for it. It also rewrites each
target file wholesale from the CURRENT gundabad block, so re-running it now would regenerate the
shipped goblin/mistymountainorcs blocks against a source that has moved since. This script only ever
adds its own marker regions and never re-derives an existing culture.

Idempotent: each inserted region is wrapped in <!-- TAOM-NEWCULTURE:<id>:BEGIN/END --> and stripped
before re-insert, so re-running regenerates cleanly. Byte-faithful: each file is written back with
the line endings and BOM it arrived with.

Run:  python tools/promote_borrowed_cultures.py --only arthedain            # dry run: each file same or edit
      python tools/promote_borrowed_cultures.py --only arthedain --check    # exit 1 if a run would change a file
      python tools/promote_borrowed_cultures.py --only arthedain --apply
"""
import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData"
SUBMODULE = ROOT / "Main" / "_Module" / "SubModule.xml"
LAYOUT = json.loads((ROOT / "tools" / "taom_new_factions_layout.json").read_text(encoding="utf-8"))
CAPITALS = {s["id"]: s for s in LAYOUT["settlements"]}

MARKER = "TAOM-NEWCULTURE"

# Each target names the culture it is carved out of. `subs` is the player-facing wording remap,
# longest phrase first so a substring never double-substitutes; the contamination gate at the end
# is what proves the table is complete.
TARGETS = {
    "lindon": {
        "src": "rivendell",
        "capital": "town_LN1",
        "race": "elf",
        # Cirdan's Falathrim of the Grey Havens are Sindar mariners, not Imladris Noldor — the
        # distinction is the whole reason Lindon earns its own culture rather than staying a
        # rivendell skin. Troop identity beyond the clone is follow-up work, tracked in the doc.
        "raceword": "Elf",
        "adjective": "Lindon",
        "tag": "Lindon",
        "short": "lin",
        "color": "0xFF50A090",
        "color2": "0xFF7FC4B4",
        "orc_remap": False,
        "subs": [
            # Rivendell's culture is named for the Ñoldor of Imladris. Lindon is Círdan's realm —
            # Falathrim Sindar of the Grey Havens, the mariners who keep the last ships. Cloning the
            # roster is a fair starting point; inheriting the people's NAME is not.
            ("Ñoldor Elves", "Falathrim Elves"),
            ("Ñoldor", "Falathrim"),
            ("Rivendell", "Lindon"),
            ("Imladris", "Mithlond"),
        ],
        "culture_name": "Falathrim Elves",
        "culture_desc": (
            "The Falathrim of Lindon are the last of the Sindar shipwrights, dwelling at Mithlond "
            "where the Gulf of Lune meets the sea. Círdan has kept the Grey Havens since the First "
            "Age, and it is from his quays that the white ships pass into the West."),
        # The SOURCE culture's feat ids, verbatim and deliberately un-renamed. CulturalFeatsService
        # dispatches on FeatObject identity against the ids TaomCulturalFeats.Register() creates, so
        # a `taom_lindon_*` id would resolve to nothing and be dropped without a word — and the
        # faction card on the CC map already advertises these bonuses to the player.
        "feats": ["taom_rivendell_army_influence", "taom_rivendell_hearth_growth",
                  "taom_rivendell_army_influence_cost", "taom_rivendell_food_consumption",
                  "taom_rivendell_loyalty", "taom_rivendell_forest_speed"],
    },
    "bluecraig": {
        "src": "goblin",
        "capital": "town_GBC1",
        "race": "goblin",
        "raceword": "Goblin",
        "adjective": "Blue Craig",
        "tag": "Blue Craig",
        "short": "bcg",
        "color": "0xFF2A3A5A",
        "color2": "0xFF44586E",
        # The source is already an orc culture wearing orc armor, so the orc remap that
        # insert_new_factions.py applies when cloning FROM gundabad would be a no-op at best and a
        # double-remap at worst. Off.
        "orc_remap": False,
        "culture_name": "Blue Craig Goblins",
        "culture_desc": (
            "The goblins of Blue Craig hold the western spurs of the Ered Luin, cut off from their "
            "kin under the High Pass by the length of Eriador. They raid the Dwarf-roads and the "
            "shores of Lune, and the Elves of Mithlond count them the nearer enemy."),
        "subs": [
            ("Goblin-town", "Blue Craig"),
            ("the High Pass", "the Ered Luin"),
        ],
        # Goblin-town's registered feats, reused verbatim — see the note on Lindon's above.
        "feats": ["taom_goblin_party_size", "taom_goblin_volunteer_rate", "taom_goblin_snow_speed",
                  "taom_goblin_food_consumption", "taom_goblin_smithing", "taom_goblin_raid_damage"],
    },
    # Arthedain is not a borrowed kingdom being promoted but a NEW culture, scaffolded from Gondor
    # (the other Dunedain realm) and then given its own troop tree. The keys below the line are
    # what makes that different from Lindon and Blue Craig, and every one is opt-in, so those two
    # targets behave exactly as before.
    "arthedain": {
        "src": "gondor",
        "capital": "town_AN2",  # Fornost Erain (Mike, 2026-10-07)
        # Gondor's men carry no race attribute (plain humans), so there is nothing to rewrite.
        "race": None,
        "raceword": "Man",
        "adjective": "Arthedain",
        "tag": "Arthedain",
        "short": "arth",
        # Gondor's NPC name keys are aom_gd_*, not aom_gon_*, so the default src[:3] remap would
        # leave 68 Gondor keys carrying Arthedain text: one key, two texts.
        "src_short": "gd",
        "color": "0xFF4870B8",
        "color2": "0xFFC8CCD8",
        "orc_remap": False,
        "subs": [
            ("Minas Tirith", "Fornost"),
            ("Pelargir", "Annúminas"),
            ("Gondorians", "Dúnedain"),
            ("Gondorian", "Arthedain"),
            ("Gondor", "Arthedain"),
        ],
        "culture_name": "Arthedain",
        "culture_desc": (
            "The Dúnedain of Arthedain are the last heirs of Arnor, the North-kingdom Elendil founded. "
            "From Fornost upon the North Downs the line of Arvedui still keeps the watch that Isildur's "
            "heirs have kept for a thousand years, though their towns are few, their lords are poor and "
            "the shadow of Angmar never leaves the hills. They are fewer than their southern kin and "
            "prouder than their numbers warrant, and every season the wild lands press closer."),
        # Gondor's feats, verbatim (see Lindon's note), less the two that make a realm strong in the
        # field: army influence and party size. Arthedain is meant to be the realm others carve up.
        "feats": ["taom_gondor_garrison_wage", "taom_gondor_hearth_growth", "taom_gondor_loyalty",
                  "taom_gondor_morale", "taom_gondor_plain_speed"],
        # ---- opt-in keys ----
        # Clone only these standalone files. The troop tree is tools/generate_arthedain_troops.py.
        "standalone": ["characters/npcs_{c}.xml", "equipmentsets/taom_equipment_sets_{c}.xml"],
        # Of Gondor's 74 equipment rosters keep the lord template family a to e only: the culture block
        # names _a, and generate_new_faction_kingdoms.py assigns letters a to e by lord (Arthedain's four
        # lords per clan use a to d). The rest are Gondor's named lords' kits.
        "rosters_kept": r"^arthedain_(bat_template_medium|civ_template_default)_[a-e]$",
        # The cloned lord, ruler, heir and enlistment rosters wear the Arnor kit (Mike, 2026-10-07):
        # Gondor's armour piece -> the Arnor piece of the same slot and tier; weapons, horses and
        # harnesses keep. Applied only to these files' cloned blocks.
        "item_map_files": ["equipmentsets/taom_equipment_sets_{c}.xml",
                           "equipmentsets/taom_lord_template_equipment.xml",
                           "equipmentsets/taom_enlistment_equipment.xml"],
        "item_map": {
            "sk_gd_mns_cita_helmet_heavy_a": "sk_ar_art_helmet_noble_heavy_a",
            "sk_gd_mns_cita_helmet_heavy_b": "sk_ar_art_helmet_noble_heavy_b",
            "sk_gd_mns_fount_helmet_heavy_a": "sk_ar_art_helmet_noble_elite_a",
            "sk_gd_mns_noble_helmet_heavy_a": "sk_ar_art_helmet_noble_lord_b",
            "sk_gd_ano_noble_helmet_med_a": "sk_ar_art_helmet_noble_med_a",
            "sk_gd_mns_citadel_chest_med_a": "sk_ar_art_chest_noble_med_a",
            "sk_gd_mns_citadel_chest_heavy_a": "sk_ar_art_chest_noble_heavy_a",
            "sk_gd_mns_fount_chest_heavy_a": "sk_ar_art_chest_noble_heavy_b",
            "sk_gd_ano_pauld_cape_fount_elite_a": "sk_ar_art_pauld_cape_noble_elite_a",
            "sk_gd_ano_pauld_noble_heavy_a": "sk_ar_art_pauld_noble_heavy_a",
            "sk_gd_ano_bracer_noble_heavy_a": "sk_ar_art_bracer_noble_heavy_a",
            "sk_gd_ano_bracer_noble_med_a": "sk_ar_art_bracer_noble_med_a",
            "sk_gd_ano_grvs_noble_med_a": "sk_ar_art_grvs_noble_med_a",
            "sk_gd_ano_inf_chest_heavy_a": "sk_ar_art_chest_inf_heavy_a",
            "sk_gd_ano_inf_chest_heavy_b": "sk_ar_art_chest_inf_heavy_b",
            "sk_gd_ano_pauld_inf_heavy_a": "sk_ar_art_pauld_inf_heavy_a",
            "sk_gd_ano_pauld_inf_med_a": "sk_ar_art_pauld_inf_med_a",
            "sk_gd_ano_pauld_inf_med_b": "sk_ar_art_pauld_inf_med_b",
            "sk_gd_ano_pauld_inf_elite_a": "sk_ar_art_pauld_inf_elite_a",
            "sk_gd_ano_gloves_a": "sk_ar_art_gloves_a",
            "sk_gd_ano_bracer_inf_med_a": "sk_ar_art_bracer_inf_med_a",
            "sk_gd_ano_grvs_inf_light_a": "sk_ar_art_grvs_inf_light_a",
            "sk_gd_ano_grvs_inf_med_a": "sk_ar_art_grvs_inf_med_a",
            "sk_gd_ano_grvs_inf_heavy_a": "sk_ar_art_grvs_inf_heavy_a",
            "sk_gd_ano_boots_a": "sk_ar_art_boots_a",
            "sk_gd_ano_cape_a": "sk_ar_art_cape_a",
            "sk_gd_ano_chainmail_half_b": "sk_ar_art_chainmail_half_b",
            "sk_gd_ano_chainmail_full_a": "sk_ar_art_chainmail_full_a",
            "sk_gd_bel_inf_chainmail_b": "sk_ar_art_chainmail_half_b",
            "sk_gd_ano_inf_chest_med_a": "sk_ar_art_chest_inf_med_a",
            "sk_gd_bel_inf_chest_med_a": "sk_ar_art_chest_inf_med_a",
            "sk_gd_bel_inf_chest_med_b": "sk_ar_art_chest_inf_med_b",
            "sk_gd_anf_inf_chest_med_b": "sk_ar_art_chest_inf_med_b",
            "sk_gd_pin_inf_chest_med_b": "sk_ar_art_chest_inf_med_b",
            "sk_gd_vale_chest_heavy_a": "sk_ar_art_chest_inf_heavy_a",
            "sk_gd_pin_inf_chest_heavy_a": "sk_ar_art_chest_inf_heavy_b",
            "sk_gd_osg_pauld_cape_inf_elite_a": "sk_ar_art_pauld_cape_inf_elite_a",
            "sk_gd_vale_pauld_med_a": "sk_ar_art_pauld_inf_med_b",
            "sk_gd_ano_inf_helmet_med_a": "sk_ar_art_helmet_inf_med_a",
            "sk_gd_pin_inf_helmet_med_a": "sk_ar_art_helmet_inf_med_b",
            "sk_gd_ano_inf_helmet_heavy_a": "sk_ar_art_helmet_inf_hvy_a",
            "sk_gd_pin_spear_helmet_heavy_a": "sk_ar_art_helmet_inf_hvy_b",
            "sk_gd_vale_helmet_heavy_a": "sk_ar_art_helmet_warden_heavy_a",
            "sk_gd_anf_cav_helmet_heavy_a": "sk_ar_art_helmet_cav_heavy_a",
        },
        # Clone only the templates that name townsfolk; the generator writes the nine soldier ones.
        "party_names": ["villager_{c}_template", "caravan_template_{c}", "elite_caravan_template_{c}"],
        # References the blanket rename would point at Gondor troops Arthedain does not have. Applied
        # as whole tokens after the rename, so the culture's troop slots and the villager's upgrade
        # path land on the generated tree.
        "id_overrides": {
            "arthedain_ano_peasant": "arthedain_levy",
            "arthedain_ano_footman": "arthedain_dunadan_youth",
        },
        # The culture's <basic_mercenary_troops>, rebuilt outright: the tavern sells a _merc leaf
        # copy of the rarest pool entry, which the troop generator emits (TavernMercenaryDataTests).
        "mercenaries": ["arthedain_dunadan_youth_merc"],
        # Gondor's culture strings name a Steward; Arthedain has a king. Stated outright per id.
        "string_overrides": {
            "str_faction_ruler.arthedain": "King",
            "str_faction_ruler.arthedain_f": "Queen",
            "str_faction_ruler_term_in_speech.arthedain": "{?RULER.GENDER}the Queen{?}the King{\\?} {RULER.NAME}",
            "str_faction_ruler_name_with_title.arthedain": "{?RULER.GENDER}Queen{?}King{\\?} {RULER.NAME}",
            "str_faction_formal_name_for_culture.arthedain": "Arthedain",
            "str_faction_informal_name_for_culture.arthedain": "the Dúnedain of the North",
            "str_neutral_term_for_culture.arthedain": "Dúnedain",
            "str_culture_rich_name.arthedain": "Dúnedain of Arthedain",
            "str_adjective_for_culture.arthedain": "Arthedain",
            "str_player_father_name.arthedain": "Aravir",
            "str_player_mother_name.arthedain": "Ivorwen",
        },
        # The name pools are the people's identity; Gondor's (Boromir, Denethor) cannot stand.
        # Kings and chieftains of Arthedain from LOTR Appendix A I iii, the Dunedain of the Angle,
        # and Sindarin-pattern names. Aragorn, Arathorn and Arwen are left out on purpose.
        "names": {
            "male_names": [
                "Amlaith", "Beleg", "Mallor", "Celepharn", "Celebrindor", "Malvegil", "Argeleb",
                "Arveleg", "Araphor", "Arvegil", "Araval", "Araphant", "Arvedui", "Aranarth",
                "Arahael", "Aranuir", "Aravir", "Aragost", "Aravorn", "Arahad", "Argonui", "Arador",
                "Arassuil", "Dírhael", "Malbeth", "Halbarad", "Calmir", "Dorlas", "Galdir", "Haldan",
                "Iorhael", "Mablost", "Nardil", "Rodnor", "Tharion", "Saelon", "Morvegil", "Cirdir",
                "Hirvegil", "Hallatan"],
            "female_names": [
                "Gilraen", "Ivorwen", "Fíriel", "Dírwen", "Eirien", "Elanwen", "Gilwen", "Lothwen",
                "Meneliel", "Rhiannel", "Silivren", "Ithilwen", "Aerwen", "Calwen", "Elthiel",
                "Maethil", "Varwen", "Anwiel", "Cúwen", "Faerwen", "Hethlin", "Luinil", "Tirwen",
                "Arwiel"],
            "clan_names": [
                "House of Amlaith", "House of Fornost", "House of Annúminas", "House of Amon Sûl",
                "House of the North Downs", "House of Evendim", "House of the Baranduin",
                "House of the Angle", "House of Bree", "House of Deadmen's Dike",
                "House of the Weather Hills", "House of Nenuial"],
        },
        # Register this culture's files in a marker region of its own, so a run with --only never
        # rebuilds the shared TAOM-NEWCULTURE-REG block (which no longer matches what this script
        # would emit for bluecraig: its troop file was retired).
        "own_reg": True,
    },
}

# BANNERLORD_GAME_DIR overrides the install path (#404), as in tools/add_map_villages.py.
LIVE_SETTLEMENTS = (Path(game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord"))
                    / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml")


def capital_position(target, cfg):
    """(posX, posY) of the target's capital: the layout first, then the LIVE map, then a stated
    provisional borrow. Never a made-up coordinate."""
    if cfg["capital"] in CAPITALS:
        return CAPITALS[cfg["capital"]], False
    if not LIVE_SETTLEMENTS.exists():
        raise SystemExit(f"{target}: the live map is not at {LIVE_SETTLEMENTS} (set BANNERLORD_GAME_DIR)")
    live = LIVE_SETTLEMENTS.read_text(encoding="utf-8-sig")
    for sid in (cfg["capital"], cfg.get("provisional_start_from")):
        if not sid:
            continue
        m = re.search(r'<Settlement\b[^>]*\bid="%s"[^>]*>' % re.escape(sid), live)
        if m:
            head = m.group(0)
            pos = {"posX": re.search(r'\bposX="([^"]+)"', head).group(1),
                   "posY": re.search(r'\bposY="([^"]+)"', head).group(1)}
            return pos, sid != cfg["capital"]
    raise SystemExit(f"{target}: capital {cfg['capital']} is in neither the layout nor the live map, "
                     "and no provisional_start_from resolves")


def keep_rosters(text, pattern):
    """Keep only the <EquipmentRoster>s whose id matches `pattern`, and a section comment only while a
    roster under it survives. Returns (text, dropped ids). Gondor's file carries its named lords' kits
    (Boromir's, Faramir's...), which a clone would ship under new ids that nothing names."""
    keep = re.compile(pattern)
    parts = re.split(r"([ \t]*<!--.*?-->\n|[ \t]*<EquipmentRoster\b.*?</EquipmentRoster>\n)", text, flags=re.DOTALL)
    out, held, dropped = [], None, []
    for part in parts:
        if part.lstrip().startswith("<!--"):
            held = [part]
        elif part.lstrip().startswith("<EquipmentRoster"):
            rid = re.search(r'\bid="([^"]+)"', part).group(1)
            if keep.search(rid):
                out.extend(held or [])
                held = None
                out.append(part)
            else:
                dropped.append(rid)
        elif held is not None:
            held.append(part)
        else:
            out.append(part)
    if held:
        out.extend(p for p in held if not p.lstrip().startswith("<!--"))
    return re.sub(r"\n(?:[ \t]*\n){2,}", "\n\n", "".join(out)), dropped


def apply_string_overrides(text, overrides):
    """Replace the default text of named <string> rows, keeping each row's {=key}."""
    for sid, value in overrides.items():
        pat = r'(<string id="%s" text="\{=[^}]*\})[^"]*(")' % re.escape(sid)
        text, n = re.subn(pat, lambda m, v=value: m.group(1) + v.replace("&", "&amp;") + m.group(2), text)
        if n != 1:
            raise SystemExit(f"string override {sid}: expected exactly one row, found {n}")
    return text


def replace_name_pools(block, target, names):
    """Rebuild <male_names>, <female_names> and <clan_names> from the target's own lists."""
    keys = {"male_names": "male_name", "female_names": "female_name", "clan_names": "clan"}
    for tag, key in keys.items():
        if tag not in names:
            continue
        rows = "".join(f'      <name name="{{=aom_{target}_{key}_{i}}}{n}" />\n'
                       for i, n in enumerate(names[tag], 1))
        block, n = re.subn(rf"(<{tag}>\n).*?([ \t]*</{tag}>)", lambda m: m.group(1) + rows + m.group(2),
                           block, flags=re.DOTALL)
        if n != 1:
            raise SystemExit(f"{target}: expected one <{tag}> block, found {n}")
    return block

# Tokens that must survive the blanket source->target rename because they name a real Armory item,
# body property, sub-culture or skill-set that exists only under the SOURCE culture's name. Renaming
# one produces a reference to something nobody has authored — the failure mode that shipped six
# careers pointing at non-existent particle systems earlier today.
PROTECT_PREFIXES = ["Item.", "BodyProperty.", "SkillSet.", "Culture.%s_raiders"]

# (filename, close tag, how to pull the source's blocks out)
SHARED_FILES = [
    ("taom_spcultures.xml", "</SPCultures>", "culture"),
    ("taom_wanderers.xml", "</NPCCharacters>", "wanderers"),
    ("equipmentsets/taom_wanderer_equipment.xml", "</EquipmentRosters>", "wanderer_roster"),
    ("taom_partyTemplates.xml", "</partyTemplates>", "party"),
    ("taom_module_strings.xml", None, "strings"),
    ("equipmentsets/taom_child_equipment_templates.xml", "</EquipmentRosters>", "rosters"),
    ("equipmentsets/taom_lord_template_equipment.xml", "</EquipmentRosters>", "rosters"),
    ("equipmentsets/taom_education_equipment_templates.xml", "</EquipmentRosters>", "rosters"),
    # The six stage-2 tutor templates. A culture without them CTDs when a child reaches age 8 —
    # MISSING_EDUCATION_TEMPLATES exists because that shipped once already (#354).
    ("taom_education_character_templates.xml", "</NPCCharacters>", "education_chars"),
    # enlist_<culture>_{infantry,archer,cavalry,support}_{recruit,soldier,veteran,sergeant} since
    # #525, so up to 16 cells per culture rather than 4. A culture with none of them falls back to
    # enlist_default_* and is issued another culture's kit (#431). Note that a MISSING cell is not
    # by itself a defect any more: the generator omits a cell whose donor pool has nothing in band,
    # and the resolver descends within the culture.
    ("equipmentsets/taom_enlistment_equipment.xml", "</EquipmentRosters>", "rosters"),
]

# Standalone files cloned wholesale, one per culture.
STANDALONE_FILES = [
    ("troops/troops_{c}.xml", "NPCCharacters"),
    ("characters/npcs_{c}.xml", "NPCCharacters"),
    ("equipmentsets/taom_equipment_sets_{c}.xml", "EquipmentRosters"),
]


def read(path):
    raw = Path(path).read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig" if bom else "utf-8")
    newline = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), newline, bom


def write(path, text, newline, bom):
    data = text.replace("\n", newline).encode("utf-8")
    Path(path).write_bytes((b"\xef\xbb\xbf" if bom else b"") + data)


def build_id_map(src, target, texts):
    """Map every id DEFINED by the source culture's standalone files onto a target-unique id.

    A blanket `rivendell`->`lindon` rename is not enough, because a culture's id-space is not
    actually namespaced by its own name. troops_rivendell.xml defines 14 `imladris_*` ids alongside
    13 `rivendell_*` ones, plus `noldorin_*`, `rider_*` and `battlemaster_*`; the equipment sets add
    `glorfindel_*`. Every one of those would survive the rename unchanged and the clone would then
    re-define an id that already exists — DUPLICATE_NPC_ID / DUPLICATE_ROSTER_ID, and in-engine one
    of the two definitions silently shadows the other.

    So: ids containing the source name get it swapped; ids that do not get prefixed with the target.
    """
    defined = set()
    for text in texts:
        defined.update(re.findall(r'<(?:NPCCharacter|EquipmentRoster)\b[^>]*?\bid="([^"]+)"', text, re.DOTALL))
    mapping = {}
    for old in defined:
        if src in old:
            continue  # the blanket rename already handles these
        mapping[old] = f"{target}_{old}"
    return mapping


def apply_id_map(text, mapping):
    """Replace mapped ids as whole tokens. Longest first so one id is never rewritten inside another
    (`imladris_recruit` must not be touched while replacing `imladris_recruit_veteran`)."""
    for old in sorted(mapping, key=len, reverse=True):
        text = re.sub(rf"(?<![A-Za-z0-9_]){re.escape(old)}(?![A-Za-z0-9_])", mapping[old], text)
    return text


def transform(text, target, cfg, id_map=None):
    """Rename the source culture's id-space to the target's, then fix player-facing wording."""
    src = cfg["src"]
    # Shield every id that names a real asset before the blanket rename, restore it after.
    sentinels, guarded = {}, text
    protect = []
    for pref in PROTECT_PREFIXES:
        pref = pref % src if "%s" in pref else pref
        protect += re.findall(re.escape(pref) + r"[A-Za-z0-9_]*" + re.escape(src) + r"[A-Za-z0-9_]*", guarded)
    for i, tok in enumerate(sorted(set(protect), key=len, reverse=True)):
        s = f"\x00P{i}\x00"
        sentinels[s] = tok
        guarded = guarded.replace(tok, s)

    # EVERY rewrite happens while the assets are still sentinels, and the restore is the last thing
    # that runs. Restoring earlier is not a smaller version of this — it is a live bug: Lindon's
    # display remap ends in ("rivendell", "lindon"), which ran after an earlier restore and renamed
    # all 1763 `Item.rivendell_*` references straight back out of existence. 2470 validator errors,
    # from a table entry that was merely redundant.
    guarded = guarded.replace(src, target)

    if id_map:
        guarded = apply_id_map(guarded, id_map)

    if cfg["race"]:
        guarded = re.sub(r'race="[a-z_]+"', f'race="{cfg["race"]}"', guarded)
    guarded = guarded.replace(f"aom_{cfg.get('src_short', src[:3])}_", f"aom_{cfg['short']}_")
    for old, new in cfg["subs"]:
        guarded = guarded.replace(old, new)

    for s, tok in sentinels.items():
        guarded = guarded.replace(s, tok)
    return guarded


def extract_one(text, pattern, what):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise SystemExit(f"could not find {what} — the source file's shape has changed")
    return m.group(1)


def extract_rosters(text, substr):
    """Every <EquipmentRoster> whose OPENING tag's id contains substr. Opening tags span lines in
    the education-template file, so match the block first and read the id from its head."""
    out = []
    for m in re.finditer(r"[ \t]*<EquipmentRoster\b.*?</EquipmentRoster>\n?", text, re.DOTALL):
        block = m.group(0)
        head = block[: block.find(">") + 1]
        idm = re.search(r'\bid="([^"]*)"', head, re.DOTALL)
        if idm and substr in idm.group(1):
            out.append(block)
    return "".join(out)


def source_blocks(kind, text, src, cfg=None):
    if kind == "culture":
        return extract_one(text, r'([ \t]*<Culture\b[^>]*\bid="%s".*?</Culture>\n)' % src, f"<Culture> {src}")
    if kind == "wanderers":
        found = re.findall(r'[ \t]*<NPCCharacter\b[^>]*\bid="spc_wanderer_%s_\d+".*?</NPCCharacter>\n' % src,
                           text, re.DOTALL)
        if not found:
            raise SystemExit(f"no spc_wanderer_{src}_N found")
        return "".join(found)
    if kind == "wanderer_roster":
        return extract_one(
            text, r'([ \t]*<EquipmentRoster\b[^>]*\bid="npc_companion_equipment_template_%s".*?</EquipmentRoster>\n)' % src,
            f"wanderer roster {src}")
    if kind == "party":
        # Match the 12 CANONICAL template ids exactly, never "any id containing the source name".
        # The loose form also swept up the per-clan family that generate_new_faction_kingdoms.py
        # owns — `kingdom_hero_party_goblin_bluecraig_1_template` contains "goblin", so cloning it
        # for Blue Craig produced `kingdom_hero_party_bluecraig_bluecraig_1_template`: a duplicate
        # id, invisible to the validator, that the engine resolves by silently shadowing one copy.
        names = [f"villager_{src}_template", f"caravan_template_{src}", f"elite_caravan_template_{src}",
                 f"kingdom_hero_party_{src}_template", f"kingdom_hero_party_mercenary_{src}_template",
                 f"kingdom_hero_party_outlaw_{src}_template", f"militia_{src}_template",
                 f"patrol_party_{src}_template_level_1", f"patrol_party_{src}_template_level_2",
                 f"patrol_party_{src}_template_level_3", f"rebels_{src}_template",
                 f"vassal_reward_troops_{src}"]
        if cfg and cfg.get("party_names"):
            names = [n.format(c=src) for n in cfg["party_names"]]
        found = []
        for name in names:
            m = re.search(r'[ \t]*<MBPartyTemplate\b[^>]*\bid="%s".*?</MBPartyTemplate>\n' % re.escape(name),
                          text, re.DOTALL)
            if m:
                found.append(m.group(0))
        if not found:
            raise SystemExit(f"no canonical party templates found for {src}")
        return "".join(found)
    if kind == "strings":
        lines = [ln for ln in text.splitlines()
                 if f".{src}" in ln and "<string" in ln and "taom_faction_" not in ln]
        if not lines:
            raise SystemExit(f"no <string> lines found for {src}")
        return "\n".join("  " + ln.strip() for ln in lines) + "\n"
    if kind == "education_chars":
        found = re.findall(
            r'[ \t]*<NPCCharacter\b[^>]*\bid="child_education_templates_stage_2_page_0_branch_\d+_%s".*?</NPCCharacter>\n' % src,
            text, re.DOTALL)
        if len(found) != 6:
            raise SystemExit(f"expected 6 stage-2 education templates for {src}, found {len(found)}")
        return "".join(found)
    if kind == "rosters":
        return extract_rosters(text, src)
    raise SystemExit(f"unknown block kind {kind}")


def upsert(text, close_tag, payload, marker):
    text = re.sub(r"[ \t]*<!-- " + re.escape(marker) + r":BEGIN -->.*?<!-- " + re.escape(marker) + r":END -->\n",
                  "", text, flags=re.DOTALL)
    block = f"  <!-- {marker}:BEGIN -->\n{payload}\n  <!-- {marker}:END -->\n"
    if close_tag is None:
        close_tag = "</strings>" if "</strings>" in text else "</base>"
    idx = text.rfind(close_tag)
    if idx < 0:
        raise SystemExit(f"close tag {close_tag} not found")
    return text[:idx] + block + text[idx:]


def contamination_check(target, cfg, blob):
    """An id-rename is case-sensitive and leaves player-facing strings naming the source faction.
    That exact defect shipped once already (rca-new-factions-2026-06-02) and was only caught after
    release, so it is a hard gate here rather than a review item."""
    src = cfg["src"]
    # Three things legitimately still name the source and must not trip the gate: asset ids
    # (Item./BodyProperty./SkillSet. name real files), provenance comments, and race= — a culture id
    # can equal a race id, and Blue Craig goblins really are race="goblin". The transform sets race
    # explicitly from cfg, so whatever is there is intentional by construction.
    payload = re.sub(r'\b(Item|BodyProperty|SkillSet)\.[A-Za-z0-9_]+', "", blob)
    payload = re.sub(r"<!--.*?-->", "", payload, flags=re.DOTALL)
    payload = re.sub(r'\brace="[a-z_]+"', "", payload)
    # Feat ids intentionally keep the source culture's name. They are C#-registered identities that
    # CulturalFeatsService matches by FeatObject, not display text — renaming one to `taom_lindon_*`
    # would resolve to nothing and be dropped silently, while the CC faction card kept advertising
    # the bonus. So they are stripped before the scan, alongside the asset ids.
    payload = re.sub(r'<feat id="[^"]*"\s*/>', "", payload)
    # Remove this run's own authored text before scanning, or the gate reports itself: the
    # replacement "Blue Craig Goblins" contains the source word "Goblins", and a hand-written
    # description is free to say "goblins" or name the High Pass.
    #
    # Order matters. The explicit name/description are inserted verbatim AFTER the substitutions
    # run, so they are pristine and must be stripped FIRST — strip the substitution results first
    # and they carve holes in the description, which then no longer matches and never gets removed.
    for value in (cfg.get("culture_name"), cfg.get("culture_desc")):
        if value:
            payload = payload.replace(value, "")
    for _, new in sorted(cfg["subs"], key=lambda s: len(s[1]), reverse=True):
        payload = payload.replace(new, "")

    bad = []
    if src in payload:
        bad.append(src)
    for word, _ in cfg["subs"]:
        if word in payload:
            bad.append(word)
    if bad:
        ctx = []
        for w in set(bad):
            m = re.search(r".{0,60}" + re.escape(w) + r".{0,60}", payload, re.DOTALL)
            ctx.append(f"    '{w}' near: ...{m.group(0).strip()[:120]}..." if m else f"    '{w}'")
        raise SystemExit(f"clone contamination for {target}:\n" + "\n".join(ctx))


def sub_once(text, pattern, value, what):
    """Exactly-one substitution, or fail. A silent zero-match here is how a clone keeps the source's
    wording while every check reports success."""
    out, n = re.subn(pattern, lambda m: f'{m.group(1)}{value}"', text)
    if n != 1:
        raise SystemExit(f"expected to rewrite {what} exactly once, rewrote {n}")
    return out


def duplicate_id_check(target, generated):
    """No id the clone DEFINES may already exist anywhere in shipped ModuleData.

    This is the gate for the failure the id map exists to prevent. It is checked against the real
    files rather than against the map, because the map only knows what it was told to rename — an
    id-space this script has not learned about yet would slip straight past it.
    """
    # Collect as a LIST first. Using a set here was the hole that let seven duplicate party-template
    # ids ship: a set silently collapses an id the run minted twice, so the only duplicates this
    # check could ever see were collisions with other files — never the run's own.
    minted_list = []
    for block in generated:
        minted_list += re.findall(
            r'<(?:NPCCharacter|EquipmentRoster|MBPartyTemplate|Culture)\b[^>]*?\bid="([^"]+)"',
            block, re.DOTALL)
    if not minted_list:
        raise SystemExit(f"{target}: generated no ids at all — the extraction found nothing")
    seen, internal = set(), []
    for i in minted_list:
        if i in seen:
            internal.append(i)
        seen.add(i)
    if internal:
        raise SystemExit(f"{target}: {len(set(internal))} id(s) minted more than once by this run:\n    "
                         + "\n    ".join(sorted(set(internal))[:12]))
    minted = seen

    # "Already defined" must mean "defined by someone else". This script is idempotent — it strips
    # and re-inserts its own marker regions — so on a re-run its previous output is sitting in these
    # very files. Counting that as a collision would make the second run fail on the work the first
    # run did correctly. Exclude this target's own marker regions and its own standalone files.
    own_files = {MD / rel.format(c=target) for rel, _ in STANDALONE_FILES}
    own_region = re.compile(r"<!-- " + MARKER + f":{re.escape(target)}" + r":BEGIN -->.*?<!-- "
                            + MARKER + f":{re.escape(target)}" + r":END -->", re.DOTALL)
    # `<Culture id="x">` DEFINES a culture in exactly one file. Everywhere else — cc_body_properties,
    # startup_resources, taom_careers eligibility, the marketplace / emissary / guard configs — it is
    # keyed BY culture and is supposed to name it. Scanning those as definitions makes a correctly
    # wired culture look like a duplicate of itself, so the element is only counted where it defines.
    existing = set()
    for path in MD.rglob("*.xml"):
        if "Languages" in path.parts or path in own_files:
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except (UnicodeDecodeError, OSError):
            continue
        text = own_region.sub("", text)
        elements = "NPCCharacter|EquipmentRoster|MBPartyTemplate"
        if path.name == "taom_spcultures.xml":
            elements += "|Culture"
        existing.update(re.findall(rf'<(?:{elements})\b[^>]*?\bid="([^"]+)"', text, re.DOTALL))

    clash = sorted(minted & existing)
    if clash:
        raise SystemExit(
            f"{target}: {len(clash)} id(s) already defined in shipped ModuleData — the clone would "
            f"redefine them and the engine would silently shadow one:\n    " + "\n    ".join(clash[:12])
            + ("\n    ..." if len(clash) > 12 else ""))
    print(f"  {target}: {len(minted)} ids minted, none collide with shipped data")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    # Required, as in generate_new_faction_kingdoms.py: the older targets have drifted from their
    # shipped blocks (Lindon's ids are already defined), so a run over every target only refuses.
    ap.add_argument("--only", action="append", choices=sorted(TARGETS), required=True, help="the culture(s) to promote")
    ap.add_argument("--dump", help="write each target's generated blocks under this directory for review")
    ap.add_argument("--check", action="store_true", help="exit 1, writing nothing, if a run would change a file")
    args = ap.parse_args()
    targets = {k: v for k, v in TARGETS.items() if k in args.only}

    # Shared files accumulate across cultures. Keyed by path so the second culture upserts into the
    # FIRST culture's output rather than into a fresh read of the original — otherwise the last
    # write wins and the earlier culture's blocks vanish with nothing to show it happened.
    shared_state, pending_writes, summary = {}, [], []

    def load(path):
        if path not in shared_state:
            shared_state[path] = read(path)
        return shared_state[path]

    for target, cfg in targets.items():
        src = cfg["src"]
        cap, provisional = capital_position(target, cfg)
        if provisional:
            print(f"  WARNING {target}: {cfg['capital']} is not on the map yet; the culture's start point "
                  f"is borrowed from {cfg['provisional_start_from']} until it is. Re-run after placing it.")
        generated = []
        standalone = [(rel, root) for rel, root in STANDALONE_FILES
                      if "standalone" not in cfg or rel in cfg["standalone"]]

        # The id map has to be built from the SOURCE standalone files before anything is
        # transformed, because the culture block and the party templates reference those ids and
        # must be rewritten with exactly the same mapping.
        standalone_src = []
        for rel_tmpl, _ in standalone:
            p = MD / rel_tmpl.format(c=src)
            if not p.exists():
                raise SystemExit(f"source file missing: {p}")
            standalone_src.append(read(p)[0])
        id_map = build_id_map(src, target, standalone_src)

        def finish(text):
            """The per-target rewrites that run after the shared transform."""
            if cfg.get("id_overrides"):
                text = apply_id_map(text, cfg["id_overrides"])
            if cfg.get("string_overrides"):
                hits = {sid: v for sid, v in cfg["string_overrides"].items() if f'id="{sid}"' in text}
                text = apply_string_overrides(text, hits)
            return text

        for rel, close_tag, kind in SHARED_FILES:
            path = MD / rel
            text, nl, bom = load(path)
            block = finish(transform(source_blocks(kind, text, src, cfg), target, cfg, id_map))
            if rel in cfg.get("item_map_files", ()):
                block = apply_id_map(block, cfg["item_map"])

            if kind == "culture":
                feats = ("        <cultural_feats>\n"
                         + "".join(f'            <feat id="{f}" />\n' for f in cfg["feats"])
                         + "        </cultural_feats>\n") if cfg["feats"] else ""
                block = re.sub(r"[ \t]*<cultural_feats>.*?</cultural_feats>\n", feats, block, flags=re.DOTALL)
                block = re.sub(r'start_point_position_x="[^"]*"', f'start_point_position_x="{cap["posX"]}"', block)
                block = re.sub(r'start_point_position_y="[^"]*"', f'start_point_position_y="{cap["posY"]}"', block)
                block = re.sub(r'\bcolor="0x[0-9A-Fa-f]+"', f'color="{cfg["color"]}"', block, count=1)
                block = re.sub(r'\bcolor2="0x[0-9A-Fa-f]+"', f'color2="{cfg["color2"]}"', block, count=1)
                # The people's NAME and their description are the two fields a substring remap
                # cannot get right — it produced "The Blue Craig Goblins of Blue Craig swarm..." —
                # so they are stated outright. Same reasoning as a career's display_name.
                block = sub_once(block, r'(\sname="\{=aom_%s_name\})[^"]*"' % target,
                                 cfg["culture_name"], f"{target} culture name")
                block = sub_once(block, r'(\stext="\{=aom_%s_desc\})[^"]*"' % target,
                                 cfg["culture_desc"], f"{target} culture description")
                if cfg.get("names"):
                    block = replace_name_pools(block, target, cfg["names"])
                if cfg.get("mercenaries"):
                    rows = "".join(f'      <template name="NPCCharacter.{m}" />\n' for m in cfg["mercenaries"])
                    block, n = re.subn(r"(<basic_mercenary_troops>\n).*?([ \t]*</basic_mercenary_troops>)",
                                       lambda m: m.group(1) + rows + m.group(2), block, flags=re.DOTALL)
                    if n != 1:
                        raise SystemExit(f"{target}: expected one <basic_mercenary_troops> block, found {n}")
            if kind == "strings" and cfg.get("string_overrides"):
                missing = [sid for sid in cfg["string_overrides"] if f'id="{sid}"' not in block]
                if missing:
                    raise SystemExit(f"{target}: string overrides name rows the clone did not produce: {missing}")
                desc_id = f"str_culture_description.{target}"
                if f'id="{desc_id}"' in block:
                    block = apply_string_overrides(block, {desc_id: cfg["culture_desc"]})

            generated.append(block)
            shared_state[path] = (upsert(text, close_tag, block, f"{MARKER}:{target}"), nl, bom)

        for (rel_tmpl, _), text in zip(standalone, standalone_src):
            _, nl, bom = read(MD / rel_tmpl.format(c=src))
            body = finish(transform(text, target, cfg, id_map))
            if rel_tmpl in cfg.get("item_map_files", ()):
                body = apply_id_map(body, cfg["item_map"])
            if cfg.get("rosters_kept") and rel_tmpl.startswith("equipmentsets/"):
                body, dropped = keep_rosters(body, cfg["rosters_kept"])
                print(f"  {target}: kept the rosters matching {cfg['rosters_kept']}, dropped {len(dropped)}")
            generated.append(body)
            pending_writes.append((MD / rel_tmpl.format(c=target), body, nl, bom))

        contamination_check(target, cfg, "\n".join(generated))
        duplicate_id_check(target, generated)
        summary.append((target, src, cfg["capital"], len(id_map)))
        if args.dump:
            out = Path(args.dump) / target
            out.mkdir(parents=True, exist_ok=True)
            for i, g in enumerate(generated):
                (out / f"block_{i:02d}.xml").write_text(g, encoding="utf-8")

    # SubModule.xml registration. Without it the three new files per culture are never loaded, so
    # every troop the culture names resolves to null — the culture would validate on disk and be
    # empty in-engine, which is the "PASS != in-game loaded" trap in its purest form.
    def xml_node(idv, path):
        return ('    <XmlNode>\n'
                f'      <XmlName id="{idv}" path="{path}"/>\n'
                '      <IncludedGameTypes>\n'
                '        <GameType value ="Campaign"/>\n'
                '        <GameType value ="CampaignStoryMode"/>\n'
                '        <GameType value = "CustomGame"/>\n'
                '        <GameType value = "EditorGame"/>\n'
                '      </IncludedGameTypes>\n'
                '    </XmlNode>\n')

    sm_text, sm_nl, sm_bom = read(SUBMODULE)
    legacy = [t for t in targets if not TARGETS[t].get("own_reg")]
    if legacy:
        sm_text = re.sub(r"[ \t]*<!-- " + MARKER + r"-REG:BEGIN -->.*?<!-- " + MARKER + r"-REG:END -->\n",
                         "", sm_text, flags=re.DOTALL)
        reg = f"    <!-- {MARKER}-REG:BEGIN -->\n"
        for target in legacy:
            for idv, path in (("NPCCharacters", f"troops/troops_{target}"),
                              ("NPCCharacters", f"characters/npcs_{target}"),
                              ("EquipmentRosters", f"equipmentsets/taom_equipment_sets_{target}")):
                reg += xml_node(idv, path)
        reg += f"    <!-- {MARKER}-REG:END -->\n"
        anchor = re.search(r"[ \t]*<!-- TAOM-NEWFACTIONS-REG:BEGIN -->.*?<!-- TAOM-NEWFACTIONS-REG:END -->\n",
                           sm_text, re.DOTALL)
        if not anchor:
            raise SystemExit("SubModule.xml: TAOM-NEWFACTIONS-REG anchor not found — refusing to guess "
                             "where the new <XmlNode> registrations belong")
        sm_text = sm_text[:anchor.end()] + reg + sm_text[anchor.end():]
    # A culture with its own region is registered after the shared block and never disturbs it. Its
    # troop file is registered whether or not this script wrote it: a generated tree lives there.
    for target in (t for t in targets if TARGETS[t].get("own_reg")):
        mark = f"{MARKER}-REG:{target}"
        sm_text = re.sub(r"[ \t]*<!-- " + re.escape(mark) + r":BEGIN -->.*?<!-- " + re.escape(mark) + r":END -->\n",
                         "", sm_text, flags=re.DOTALL)
        reg = f"    <!-- {mark}:BEGIN -->\n"
        for idv, path in (("NPCCharacters", f"troops/troops_{target}"),
                          ("NPCCharacters", f"characters/npcs_{target}"),
                          ("EquipmentRosters", f"equipmentsets/taom_equipment_sets_{target}")):
            if not (MD / (path + ".xml")).exists() and not any(
                    str(p).endswith(path.replace("/", os.sep) + ".xml") for p, *_ in pending_writes):
                raise SystemExit(f"{target}: refusing to register {path}.xml, which neither exists nor is "
                                 "about to be written")
            reg += xml_node(idv, path)
        reg += f"    <!-- {mark}:END -->\n"
        anchor = re.search(r"[ \t]*<!-- " + MARKER + r"-REG:END -->\n", sm_text)
        if not anchor:
            raise SystemExit("SubModule.xml: TAOM-NEWCULTURE-REG:END anchor not found")
        sm_text = sm_text[:anchor.end()] + reg + sm_text[anchor.end():]
    pending_writes.append((SUBMODULE, sm_text, sm_nl, sm_bom))

    for path, (text, nl, bom) in shared_state.items():
        pending_writes.append((path, text, nl, bom))

    print(f"{len(summary)} culture(s) to promote:")
    for target, src, capital, n in summary:
        print(f"  {target:12s} <- clone of {src:12s} capital={capital:12s} ({n} block sets)")
    print(f"  -> {len(pending_writes)} file writes")

    def changes(path, text, nl, bom):
        return not Path(path).exists() or Path(path).read_bytes() != (
            (b"\xef\xbb\xbf" if bom else b"") + text.replace("\n", nl).encode("utf-8"))

    changed = {str(path) for path, text, nl, bom in pending_writes if changes(path, text, nl, bom)}
    if args.check or not args.apply:
        for path, _, _, _ in pending_writes:
            label = "NEW " if not Path(path).exists() else ("edit" if str(path) in changed else "same")
            print(f"     {label} {Path(path).relative_to(ROOT)}")
        if args.check:
            print(f"CHECK: {len(changed)} of {len(pending_writes)} file(s) would change")
            return 1 if changed else 0
        print("DRY RUN — re-run with --apply to write")
        return 0

    for path, text, nl, bom in pending_writes:
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        write(path, text, nl, bom)
        ET.parse(path)
        print(f"  written + well-formed: {Path(path).relative_to(ROOT)}")

    print("\nDONE, SubModule.xml registration included. NOT yet done, and required before this is safe to load:")
    print("  1. retag kingdoms/clans/lords/heroes and the LIVE TAOM_Map settlements onto the new culture")
    print("  2. character-creation wiring (cultures.json, cc_body_properties, narrative menus, CC gear)")
    print("  Until (1), each new culture owns no settlement -> LANDLESS_CULTURE / daily-tick CTD (#374).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
