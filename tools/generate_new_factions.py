#!/usr/bin/env python3
"""Generate all data files for the three new TAOM factions:
  - culture 'goblin'            (race goblin)            -> Goblins kingdom (town_GT1)
  - culture 'mistymountainorcs' (race orc)               -> Misty Mountain Orcs kingdom (town_MM1..3 + castles)
  - kingdom 'lindon'            (reuses culture rivendell) -> Lindon kingdom (town_LN1)

Strategy: the two orc cultures are CLONES of the 'gundabad' templates (closest existing
orc culture) with ids/culture/race/loc-keys renamed and equipment item-ids PRESERVED
(reusing gundabad's valid LOTRLOME_Armory items as placeholders, per the user's
"placeholder equipment until I come back" instruction).

Lindon reuses the existing 'rivendell' culture wholesale (troops/npcs/equipment/party
templates/feats) — only kingdom + clans + lords + settlements + a recruitment pool are
new. Those are produced by the kingdom/clan/lord generators (separate steps), not here.

This script (PART 1) writes the standalone NEW files:
  Main/_Module/ModuleData/troops/troops_{c}.xml
  Main/_Module/ModuleData/characters/npcs_{c}.xml
  Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_{c}.xml

Run:  python tools/generate_new_factions.py
"""
import os, re, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mordor_armor_remap import remap_orc_armor, remap_orc_weapons, strip_cavalry

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")

# culture id -> (race, [Tag] display, race-word display, short loc prefix)
ORC_CULTURES = {
    "goblin":            {"race": "goblin", "tag": "Goblin",         "raceword": "Goblin", "short": "gob"},
    "mistymountainorcs": {"race": "orc",    "tag": "Misty Mountains","raceword": "Orc",    "short": "mmo"},
}

# Tokens that must survive the blanket gundabad->culture rename (they reference real
# items / body / sub-culture / skill-sets that exist only under the gundabad name).
# Protect LONGER strings first so substrings don't double-substitute.
PROTECT = [
    "Item.wm_gundabad",                      # gundabad weapons (real Armory items)
    "BodyProperty.fighter_gundabad",         # reuse gundabad orc face/body
    "Culture.gundabad_raiders",              # bandit sub-culture (kept; bandit troop dropped)
    "SkillSet.spc_wanderer_gundabad",        # reuse gundabad wanderer skill-sets
]


def clone_transform(text, culture, cfg):
    sent = {}
    for i, tok in enumerate(PROTECT):
        s = f"\x00P{i}\x00"
        sent[s] = tok
        text = text.replace(tok, s)
    # blanket rename of the culture id everywhere it remains
    text = text.replace("gundabad", culture)
    # restore protected tokens
    for s, tok in sent.items():
        text = text.replace(s, tok)
    # race swap
    text = text.replace('race="pale_uruk"', f'race="{cfg["race"]}"')
    # short loc-key prefix used by npcs file (aom_gb_*) -> unique per culture
    text = text.replace("aom_gb_", f"aom_{cfg['short']}_")
    # display polish (placeholder names read better; user can rename)
    text = text.replace("[Gundabad]", f"[{cfg['tag']}]")
    # Free-text source-culture race words in player-facing display names + comments. The clone
    # source's notables/troops describe their race as "Pale Uruk"/"Pale Orc"/"pale orc" and their
    # faction as "Gundabad" -- the id rename on line 54 is a case-sensitive lowercase replace and
    # never touches these capital/display strings, so the clone shipped notables like "Gundabad
    # Caravan Master" and "Cautious pale orc trader". Map them to this culture's race word. Safe:
    # the protected tokens are lowercase Item./BodyProperty./Culture./SkillSet. ids (restored above),
    # and the npcs/troops/equip files contain no "Gundabad Orc"/"Mount Gundabad" phrasing that the
    # bare "Gundabad" replace would mangle (verified). (Codex review + deep-review follow-up 2026-06-02.)
    text = text.replace("Pale Uruk", cfg["raceword"])
    text = text.replace("Pale Orc", cfg["raceword"])
    text = text.replace("pale orc", cfg["raceword"].lower())
    text = text.replace("Gundabad", cfg["raceword"])
    # Plain race word "orc" in notable display names ("Ruthless orc provisioner") -> this culture's
    # race word. Culture-AWARE and a no-op for mistymountainorcs (raceword "Orc".lower() == "orc"),
    # but for the goblin culture it makes the goblin-town notables read as "goblin" like their
    # siblings instead of an inconsistent "orc". Word-boundary spaces avoid mangling the orc-armor
    # item ids (sk_md_orc_*/sk_gn_orc_* have no surrounding spaces). (Completeness-audit 2026-06-02.)
    text = text.replace(" orc ", f" {cfg['raceword'].lower()} ")
    # user directive: orcs/goblins wear ORC armor (sk_md_orc_*/sk_gn_orc_*) + mixed orc weapons
    text = remap_orc_armor(text)
    text = remap_orc_weapons(text)
    return text


def drop_bandit_troops(text):
    """Remove any <NPCCharacter ... occupation="Bandit" ...> ... </NPCCharacter> block
    (gundabad_raiders_boss) so the clone has no orphan Culture.gundabad_raiders troop."""
    # NPCCharacter blocks are not self-closing here; match minimally up to closing tag
    pattern = re.compile(r'[ \t]*<NPCCharacter\b[^>]*occupation="Bandit".*?</NPCCharacter>\s*\n',
                         re.DOTALL)
    return pattern.sub("", text)


def main():
    src_troops = open(os.path.join(MD, "troops", "troops_gundabad.xml"), encoding="utf-8").read()
    src_npcs = open(os.path.join(MD, "characters", "npcs_gundabad.xml"), encoding="utf-8").read()
    src_equip = open(os.path.join(MD, "equipmentsets", "taom_equipment_sets_gundabad.xml"), encoding="utf-8").read()

    import xml.etree.ElementTree as ET
    dropped_cavalry = {}
    for culture, cfg in ORC_CULTURES.items():
        # ---- troops ----
        t = drop_bandit_troops(src_troops)
        t = clone_transform(t, culture, cfg)
        # user directive: infantry + archer lines only, no cavalry
        t, dropped = strip_cavalry(t)
        dropped_cavalry[culture] = dropped
        p = os.path.join(MD, "troops", f"troops_{culture}.xml")
        open(p, "w", encoding="utf-8").write(t)
        ET.parse(p)
        ntroops = t.count("<NPCCharacter ")
        # ---- npcs ----
        n = clone_transform(src_npcs, culture, cfg)
        p = os.path.join(MD, "characters", f"npcs_{culture}.xml")
        open(p, "w", encoding="utf-8").write(n)
        ET.parse(p)
        nnpcs = n.count("<NPCCharacter ")
        # ---- equipment sets ----
        e = clone_transform(src_equip, culture, cfg)
        p = os.path.join(MD, "equipmentsets", f"taom_equipment_sets_{culture}.xml")
        open(p, "w", encoding="utf-8").write(e)
        ET.parse(p)
        nrost = e.count("<EquipmentRoster ")
        print(f"[{culture}] race={cfg['race']}: troops_{culture}.xml (~{ntroops} troops), "
              f"npcs_{culture}.xml (~{nnpcs} npcs), taom_equipment_sets_{culture}.xml (~{nrost} rosters) — all well-formed")

    json.dump(dropped_cavalry, open(os.path.join(ROOT, "tools", "_orc_dropped_cavalry.json"), "w"), indent=2)
    print(f"\nStripped cavalry/horse-archer troops: {dropped_cavalry}")
    print("\nPART 1 done. Verify no Item.wm_<culture>_ / BodyProperty.fighter_<culture> leaked:")
    for culture in ORC_CULTURES:
        for f in [f"troops/troops_{culture}.xml", f"characters/npcs_{culture}.xml", f"equipmentsets/taom_equipment_sets_{culture}.xml"]:
            body = open(os.path.join(MD, f), encoding="utf-8").read()
            leaks = []
            if f"Item.wm_{culture}" in body: leaks.append("wm_ item leak")
            if f"BodyProperty.fighter_{culture}" in body: leaks.append("BodyProperty leak")
            if f"Culture.{culture}_raiders" in body: leaks.append("raiders culture leak")
            if leaks: print(f"   !! {f}: {leaks}")
    print("   (no lines above = clean)")

    # Durable completeness guard (Codex review 2026-06-02 finding 1 + recommendation): a clone
    # ships player-facing display text describing the SOURCE culture's race/faction. Every such
    # phrase must be rewritten to this culture's words, or settlement notables / encyclopedia show
    # "Cautious pale orc trader" on a goblin. These DISPLAY phrases never occur in the preserved
    # lowercase technical ids (Item.wm_gundabad / BodyProperty.fighter_gundabad), so a bare scan is
    # safe. RAISE so a future clone-source change can never silently reintroduce the class.
    FORBIDDEN = ("Gundabad", "Pale Uruk", "pale uruk", "Pale Orc", "pale orc")
    offenders = []
    for culture in ORC_CULTURES:
        for f in [f"troops/troops_{culture}.xml", f"characters/npcs_{culture}.xml", f"equipmentsets/taom_equipment_sets_{culture}.xml"]:
            body = open(os.path.join(MD, f), encoding="utf-8").read()
            for phrase in FORBIDDEN:
                if phrase in body:
                    offenders.append(f"{f}: contains source-culture display phrase {phrase!r}")
    if offenders:
        raise RuntimeError("clone-leftover display text not remapped:\n  " + "\n  ".join(offenders))
    print("   No source-culture display leftovers (Gundabad / Pale Uruk / Pale Orc) — clean.")


if __name__ == "__main__":
    main()
