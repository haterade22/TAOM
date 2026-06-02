#!/usr/bin/env python3
"""PART 2: insert the two orc cultures' shared-file content (cloned from gundabad).

Idempotent: each inserted region is wrapped in <!-- TAOM-NEWFACTIONS:<id>:BEGIN/END -->
markers and stripped before re-insert, so re-running is safe.

Targets:
  taom_wanderers.xml                       <- 10 wanderers per culture
  equipmentsets/taom_wanderer_equipment.xml<- wanderer companion roster per culture
  taom_partyTemplates.xml                  <- 12 kingdom party templates per culture
  taom_spcultures.xml                      <- <Culture> block per culture (NO cultural_feats)
  taom_module_strings.xml                  <- str_faction_*/str_culture_* per culture
  ../SubModule.xml                         <- 3 XmlNode registrations per culture

Run: python tools/insert_new_factions.py
"""
import os, re, json, sys
import xml.etree.ElementTree as ET
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mordor_armor_remap import remap_orc_armor, remap_orc_weapons, strip_cavalry_stacks

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")
SUBMODULE = os.path.join(ROOT, "Main", "_Module", "SubModule.xml")
LAYOUT = json.load(open(os.path.join(ROOT, "tools", "taom_new_factions_layout.json"), encoding="utf-8"))
_DC = os.path.join(ROOT, "tools", "_orc_dropped_cavalry.json")
DROPPED_CAV = json.load(open(_DC)) if os.path.exists(_DC) else {}

# Free-text "Gundabad" display strings cloned from the gundabad source blocks. The blanket
# id-rename (transform line ~48) is a case-sensitive lowercase replace and never touches these
# capital-G player-facing strings, so the clone shipped culture/faction names + descriptions still
# saying "Gundabad". The descriptions are Gundabad-LORE-specific ("Mount Gundabad", "stronghold of
# Mount Gundabad") so a word-swap can't fix them -- they are rewritten wholesale. Ordered LONGEST
# phrase first so substrings don't double-substitute; `adjective` is the bare-word catch-all applied
# last. Acceptance test after re-run: zero capital "Gundabad" survives in any NEWFACTIONS block.
# (Codex/deep-review follow-up 2026-06-02.)
_GUND_SPC_DESC = ("The Gundabad Orcs of the Misty Mountains are savage and relentless. From the "
    "ancient stronghold of Mount Gundabad, they raid the surrounding lands, driven by a thirst for "
    "vengeance and conquest. Under the rule of powerful chieftains, they amass legions of goblins, "
    "wargs, and trolls. Gundabad's warriors are brutish and cunning, masters of ambush and "
    "overwhelming force.")
_GUND_STR_DESC = ("The Orcs of Gundabad, residing in the mist-shrouded mountains, are a brutal and "
    "savage force. Led by their warlords, they wage endless raids on neighboring lands. Driven by "
    "hatred and cruelty, they are relentless in their quest to dominate the north.")
_GUND_HIDEOUT = ("Beneath the crags of the Misty Mountains you find a foul warren of Gundabad orcs "
    "burrowed into the bare rock.")

_GOBLIN_SUBS = [
    (_GUND_SPC_DESC,
     "The Goblins of Goblin-town swarm the tunnels beneath the High Pass of the Misty Mountains. "
     "Cowardly alone but overwhelming in number, they breed endlessly in the dark and pour out to "
     "ambush travellers on the mountain roads. Under their Great Goblin and his captains they harry "
     "Elves, Dwarves, and Men alike, serving the growing Shadow for plunder and spite."),
    (_GUND_STR_DESC,
     "The Goblins of the High Pass are a teeming, vicious rabble dwelling in the warrens of "
     "Goblin-town. Led by their Great Goblin and petty captains, they raid the mountain roads "
     "without end, driven by greed and an ancient hatred of the Free Peoples."),
    (_GUND_HIDEOUT,
     "Beneath the crags of the High Pass you find a reeking warren of goblins burrowed deep into "
     "the bare rock."),
    ("a chieftainess of Gundabad", "a chieftainess of Goblin-town"),
    ("a chieftain of Gundabad", "a chieftain of Goblin-town"),
    ("Mount Gundabad Tribe", "Goblin-town Horde"),
    ("Gundabad Orc Raiders", "Goblin Raiders"),
    ("Orcs of Gundabad", "Goblins of Goblin-town"),
    ("Gundabad Orcs", "Goblins"),
]
_MMO_SUBS = [
    (_GUND_SPC_DESC,
     "The Orcs of the Misty Mountains are a savage war-host dwelling among the high peaks and deep "
     "places of the range. Driven by an ancient hatred of Elves and Dwarves, they muster vast and "
     "ill-armed legions and fall upon the lands below, serving the dark powers that stir in the East."),
    (_GUND_STR_DESC,
     "The Orcs of the Misty Mountains are a brutal and numberless host of the high peaks. Led by "
     "their warlords, they wage endless raids on the lands below, relentless in their cruelty and "
     "their hunger for conquest."),
    (_GUND_HIDEOUT,
     "Beneath the crags of the Misty Mountains you find a foul warren of orcs burrowed into the "
     "bare rock."),
    ("a chieftainess of Gundabad", "a chieftainess of the Misty Mountains"),
    ("a chieftain of Gundabad", "a chieftain of the Misty Mountains"),
    ("Mount Gundabad Tribe", "Misty Mountain Host"),
    ("Gundabad Orc Raiders", "Orc Raiders"),
    ("Orcs of Gundabad", "Orcs of the Misty Mountains"),
    ("Gundabad Orcs", "Misty Mountain Orcs"),
]

ORC = {
    "goblin":            {"race": "goblin", "tag": "Goblin", "raceword": "Goblin", "short": "gob",
                          "capital": "town_GT1", "color": "0xFF1C2A1C", "color2": "0xFF44582E",
                          "adjective": "Goblin", "subs": _GOBLIN_SUBS,
                          "feats": ["taom_goblin_party_size", "taom_goblin_volunteer_rate",
                                    "taom_goblin_snow_speed", "taom_goblin_food_consumption"]},
    "mistymountainorcs": {"race": "orc", "tag": "Misty Mountains", "raceword": "Orc", "short": "mmo",
                          "capital": "town_MM1", "color": "0xFF2E2E2E", "color2": "0xFF6E5A40",
                          "adjective": "Orc", "subs": _MMO_SUBS,
                          "feats": ["taom_mistymountainorcs_army_influence_cost", "taom_mistymountainorcs_party_size",
                                    "taom_mistymountainorcs_snow_speed", "taom_mistymountainorcs_food_consumption"]},
}
PROTECT = ["Item.wm_gundabad", "BodyProperty.fighter_gundabad", "Culture.gundabad_raiders",
           "SkillSet.spc_wanderer_gundabad"]
CAP = {s["id"]: s for s in LAYOUT["settlements"]}


def transform(text, culture, cfg):
    sent = {}
    for i, tok in enumerate(PROTECT):
        s = f"\x00P{i}\x00"; sent[s] = tok; text = text.replace(tok, s)
    text = text.replace("gundabad", culture)
    for s, tok in sent.items():
        text = text.replace(s, tok)
    text = text.replace('race="pale_uruk"', f'race="{cfg["race"]}"')
    text = text.replace("aom_gb_", f"aom_{cfg['short']}_")
    text = text.replace("[Gundabad]", f"[{cfg['tag']}]")
    text = text.replace("Pale Uruk", cfg["raceword"])
    # bespoke free-text "Gundabad" display strings (longest first), then bare-word catch-all so no
    # capital "Gundabad" survives in player-facing text. Runs before the Item-id remaps (which only
    # touch lowercase Item.wm_gundabad ids, already protected/restored above).
    for old, new in cfg.get("subs", []):
        text = text.replace(old, new)
    text = text.replace("Gundabad", cfg["adjective"])
    text = remap_orc_armor(text)    # orcs/goblins wear orc armor (sk_md_orc_*/sk_gn_orc_*)
    text = remap_orc_weapons(text)  # mixed Gundabad/Mordor/Dol Guldur weapons
    return text


def extract(text, pattern):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise RuntimeError(f"pattern not found: {pattern[:60]}")
    return m.group(1)


def upsert_before(text, close_tag, payload, marker):
    """Strip prior marker block, then insert payload before the LAST close_tag."""
    text = re.sub(r'[ \t]*<!-- ' + re.escape(marker) + r':BEGIN -->.*?<!-- ' + re.escape(marker) + r':END -->\n',
                  '', text, flags=re.DOTALL)
    block = f"  <!-- {marker}:BEGIN -->\n{payload}\n  <!-- {marker}:END -->\n"
    idx = text.rfind(close_tag)
    return text[:idx] + block + text[idx:]


def main():
    src_cult = open(os.path.join(MD, "taom_spcultures.xml"), encoding="utf-8").read()
    src_wand = open(os.path.join(MD, "taom_wanderers.xml"), encoding="utf-8").read()
    src_wequip = open(os.path.join(MD, "equipmentsets", "taom_wanderer_equipment.xml"), encoding="utf-8").read()
    src_party = open(os.path.join(MD, "taom_partyTemplates.xml"), encoding="utf-8").read()
    src_strings = open(os.path.join(MD, "taom_module_strings.xml"), encoding="utf-8").read()

    # extract gundabad source blocks
    gund_culture = extract(src_cult, r'([ \t]*<Culture\b[^>]*\bid="gundabad".*?</Culture>\n)')
    gund_wanderers = "".join(
        extract(src_wand, r'([ \t]*<NPCCharacter\b[^>]*\bid="spc_wanderer_gundabad_%d".*?</NPCCharacter>\n)' % n)
        for n in range(10))
    gund_wroster = extract(src_wequip, r'([ \t]*<EquipmentRoster\b[^>]*\bid="npc_companion_equipment_template_gundabad".*?</EquipmentRoster>\n)')
    party_ids = ["villager_gundabad_template", "caravan_template_gundabad", "elite_caravan_template_gundabad",
                 "kingdom_hero_party_gundabad_template", "kingdom_hero_party_mercenary_gundabad_template",
                 "kingdom_hero_party_outlaw_gundabad_template", "militia_gundabad_template",
                 "patrol_party_gundabad_template_level_1", "patrol_party_gundabad_template_level_2",
                 "patrol_party_gundabad_template_level_3", "rebels_gundabad_template", "vassal_reward_troops_gundabad"]
    gund_party = "".join(
        extract(src_party, r'([ \t]*<MBPartyTemplate\b[^>]*\bid="%s".*?</MBPartyTemplate>\n)' % re.escape(t))
        for t in party_ids)
    # gundabad localisation <string> lines (faction + culture + parent-name)
    gund_str_lines = [ln for ln in src_strings.splitlines()
                      if "gundabad" in ln and "<string" in ln and "taom_faction_" not in ln]
    gund_strings = "\n".join("  " + ln.strip() for ln in gund_str_lines) + "\n"

    out_cult, out_wand, out_wequip, out_party, out_strings = src_cult, src_wand, src_wequip, src_party, src_strings

    for culture, cfg in ORC.items():
        cap = CAP[cfg["capital"]]
        # --- culture block: transform, strip cultural_feats, set start point + colors ---
        cblock = transform(gund_culture, culture, cfg)
        feats_xml = ("        <cultural_feats>\n"
                     + "".join(f'            <feat id="{f}" />\n' for f in cfg["feats"])
                     + "        </cultural_feats>\n")
        cblock = re.sub(r'[ \t]*<cultural_feats>.*?</cultural_feats>\n', feats_xml, cblock, flags=re.DOTALL)
        cblock = re.sub(r'start_point_position_x="[^"]*"', f'start_point_position_x="{cap["posX"]}"', cblock)
        cblock = re.sub(r'start_point_position_y="[^"]*"', f'start_point_position_y="{cap["posY"]}"', cblock)
        cblock = re.sub(r'\bcolor="0x[0-9A-Fa-f]+"', f'color="{cfg["color"]}"', cblock, count=1)
        cblock = re.sub(r'\bcolor2="0x[0-9A-Fa-f]+"', f'color2="{cfg["color2"]}"', cblock, count=1)
        out_cult = upsert_before(out_cult, "</SPCultures>", cblock, f"TAOM-NEWFACTIONS:{culture}")
        # --- wanderers ---
        out_wand = upsert_before(out_wand, "</NPCCharacters>", transform(gund_wanderers, culture, cfg),
                                 f"TAOM-NEWFACTIONS:{culture}")
        # --- wanderer roster ---
        out_wequip = upsert_before(out_wequip, "</EquipmentRosters>", transform(gund_wroster, culture, cfg),
                                   f"TAOM-NEWFACTIONS:{culture}")
        # --- party templates (strip stacks referencing dropped cavalry troops) ---
        party_text = transform(gund_party, culture, cfg)
        party_text = strip_cavalry_stacks(party_text, DROPPED_CAV.get(culture, []))
        out_party = upsert_before(out_party, "</partyTemplates>", party_text,
                                  f"TAOM-NEWFACTIONS:{culture}")
        # --- strings ---
        out_strings = upsert_before(out_strings, "</strings>" if "</strings>" in out_strings else "</base>",
                                    transform(gund_strings, culture, cfg), f"TAOM-NEWFACTIONS:{culture}")

    for path, content in [(os.path.join(MD, "taom_spcultures.xml"), out_cult),
                          (os.path.join(MD, "taom_wanderers.xml"), out_wand),
                          (os.path.join(MD, "equipmentsets", "taom_wanderer_equipment.xml"), out_wequip),
                          (os.path.join(MD, "taom_partyTemplates.xml"), out_party),
                          (os.path.join(MD, "taom_module_strings.xml"), out_strings)]:
        open(path, "w", encoding="utf-8").write(content)
        ET.parse(path)
        print(f"  inserted + well-formed: {os.path.relpath(path, ROOT)}")

    # --- SubModule.xml: 3 XmlNode per culture after the gundabad equipment trio ---
    sm = open(SUBMODULE, encoding="utf-8").read()
    sm = re.sub(r'[ \t]*<!-- TAOM-NEWFACTIONS-REG:BEGIN -->.*?<!-- TAOM-NEWFACTIONS-REG:END -->\n', '',
                sm, flags=re.DOTALL)

    def xmlnode(idv, path):
        return (f'    <XmlNode>\n'
                f'      <XmlName id="{idv}" path="{path}"/>\n'
                f'      <IncludedGameTypes>\n'
                f'        <GameType value ="Campaign"/>\n'
                f'        <GameType value ="CampaignStoryMode"/>\n'
                f'        <GameType value = "CustomGame"/>\n'
                f'        <GameType value = "EditorGame"/>\n'
                f'      </IncludedGameTypes>\n'
                f'    </XmlNode>\n')
    reg = "    <!-- TAOM-NEWFACTIONS-REG:BEGIN -->\n"
    for culture in ORC:
        reg += xmlnode("NPCCharacters", f"troops/troops_{culture}")
        reg += xmlnode("NPCCharacters", f"characters/npcs_{culture}")
        reg += xmlnode("EquipmentRosters", f"equipmentsets/taom_equipment_sets_{culture}")
    reg += "    <!-- TAOM-NEWFACTIONS-REG:END -->\n"
    anchor = re.search(r'(<XmlNode>\s*<XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_gundabad"/>.*?</XmlNode>\n)',
                       sm, re.DOTALL)
    if not anchor:
        raise RuntimeError("SubModule anchor not found")
    sm = sm[:anchor.end()] + reg + sm[anchor.end():]
    open(SUBMODULE, "w", encoding="utf-8").write(sm)
    ET.parse(SUBMODULE)
    print(f"  inserted + well-formed: Main/_Module/SubModule.xml (6 XmlNode regs)")
    print("\nPART 2 done.")


if __name__ == "__main__":
    main()
