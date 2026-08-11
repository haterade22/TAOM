#!/usr/bin/env python3
"""Clone Gundabad's three orc careers for the new orc cultures (goblin, mistymountainorcs).

Both cultures shipped CC-selectable in June 2026 with zero eligible careers, so the career stage
offered only the "No specialization" fallback. The crash that a zero-option culture would otherwise
cause is already guarded (TrySwitchToNextMenu_Patch), which is exactly why this stayed invisible.

Cloning rather than authoring from scratch is the house pattern for these two cultures: their troop
trees are Gundabad clones (docs/features/new-factions-misty-mountains-lindon.md) and their CC
narrative menus were cloned from Gundabad by tools/insert_new_faction_cc_menus.py. This script is
the third instance of the same move, and it deliberately mirrors that script's conventions.

What is cloned, per career, into four files:
  taom_careers.xml            1 <Career>          (LF line endings — unlike its three siblings)
  taom_ability_templates.xml  1 <AbilityTemplate>
  taom_career_choices.xml     1 root <Choice> + 6 <ChoiceGroup> x 5 <Choice> = 31 choices
  career_menu.json            1 entry

Art and FX ids (portrait_sprite, icon_sprite, sprite, particle_effect, sound_effect) are REUSED from
the source career, never renamed. A renamed sprite id would point at an asset nobody has authored;
reusing the source's keeps the clone exactly as well-resourced as its original. Only 21 of the 50
shipped careers have a portrait registered in TAOMSpriteData.xml, so inventing six more ids would
have widened an existing gap rather than closed it.

Textual insert (not an XML re-serialize) so every existing element stays byte-for-byte identical and
each file's own line-ending convention survives. Idempotent: a culture already present is skipped.

Run:  python tools/insert_new_faction_careers.py            # dry run
      python tools/insert_new_faction_careers.py --apply
"""
import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAREER_DIR = ROOT / "Main" / "_Module" / "ModuleData" / "career_system"
CAREERS_XML = CAREER_DIR / "taom_careers.xml"
ABILITIES_XML = CAREER_DIR / "taom_ability_templates.xml"
CHOICES_XML = CAREER_DIR / "taom_career_choices.xml"
MENU_JSON = ROOT / "Main" / "_Module" / "ModuleData" / "charactercreation" / "career_menu.json"

# (source career id, source loc abbreviation) -> per-target (new id, new abbreviation, display name)
#
# The abbreviation is a second, independent id-space: choice descriptions key off `{=taom_ctm_t1_a_key}`
# and `career_choice_ctm_t1_a_key`, not off the career id. Remapping only the career id would leave
# every cloned choice sharing Gundabad's localization keys, so the goblin player would read Gundabad's
# wording no matter what the Career element said.
SOURCES = [
    ("cave_troll_master", "ctm"),
    ("goblin_sniper", "gbs"),
    ("warg_pack_leader", "wpl"),
]

# Blue Craig and Lindon were promoted out of Goblin-town and Rivendell, so they clone the careers of
# the culture they were carved from rather than Gundabad's. Lindon's source set is Rivendell's three
# elven careers, which is a different SOURCES list entirely — hence the per-culture override below.
SOURCES_FOR = {
    "bluecraig": [("goblin_troll_driver", "gtd"), ("goblin_tunnel_stalker", "gts"),
                  ("goblin_warg_master", "gwm")],
    "lindon": [("blade_dancer", "bd"), ("elven_archer", "elar"), ("rivendell_knight", "rvk")],
    # Shaghâna (Near Harad) and Âbanissa (Far Harad) are independent Harad-region kingdoms that
    # never had careers at all — they sat in CareerCultureCoverageTests' documentedExceptions from
    # Review #24 until 2026-08-11. Aserai is the nearest peer on the evidence: Shaghâna's own NPC
    # file says it "uses harad face templates and aserai civilian equipment", both cultures carry
    # a *DesertSpeedFeat, and VolunteerRecruitmentService already routes shaghana volunteers to
    # harad_levy. Roles stay faithful to the source trio — infantry, javelineer, beast-rider.
    "shaghana": [("tribesman_of_jelut", "toj"), ("pezarsani_javelineer", "pj"),
                 ("mahud_beast_rider", "mbr")],
    "abanissa": [("tribesman_of_jelut", "toj"), ("pezarsani_javelineer", "pj"),
                 ("mahud_beast_rider", "mbr")],
}

TARGETS = {
    "goblin": {
        "cave_troll_master": ("goblin_troll_driver", "gtd", "Goblin-town Troll-Driver"),
        "goblin_sniper": ("goblin_tunnel_stalker", "gts", "Goblin-town Tunnel Stalker"),
        "warg_pack_leader": ("goblin_warg_master", "gwm", "Goblin-town Warg Master"),
    },
    "mistymountainorcs": {
        "cave_troll_master": ("misty_troll_goad", "mtg", "Moria Troll-Goad"),
        "goblin_sniper": ("misty_deep_marksman", "mdm", "Moria Deep-Marksman"),
        "warg_pack_leader": ("misty_warg_chieftain", "mwc", "Misty Mountain Warg Chieftain"),
    },
    "bluecraig": {
        "goblin_troll_driver": ("craig_pit_driver", "cpd", "Blue Craig Pit-Driver"),
        "goblin_tunnel_stalker": ("craig_crag_stalker", "ccs", "Blue Craig Crag Stalker"),
        "goblin_warg_master": ("craig_warg_master", "cwm", "Blue Craig Warg Master"),
    },
    "lindon": {
        "blade_dancer": ("falathrim_blade_dancer", "fbd", "Falathrim Blade-Dancer"),
        "elven_archer": ("falathrim_sentinel", "fsn", "Falathrim Sentinel"),
        "rivendell_knight": ("falathrim_mariner", "fmr", "Falathrim Mariner"),
    },
    # Named for each kingdom's own towns in the live TAOM_Map settlements.xml — Zajâna, Chatâk and
    # Sormedân are Shaghâna's; Damudûr and Jîret are Âbanissa's. Âbanissa's notables are ivory,
    # gold, gem and spice traders with caravan lords and house stewards, so its wording is
    # mercantile-dynastic where Shaghâna's stays tribal.
    "shaghana": {
        "tribesman_of_jelut": ("shaghana_zajana_tribesman", "szt", "Tribesman of Zajâna"),
        "pezarsani_javelineer": ("shaghana_chatak_javelineer", "scj", "Chatâk Javelineer"),
        "mahud_beast_rider": ("shaghana_beast_rider", "sbr", "Sormedân Beast Rider"),
    },
    "abanissa": {
        "tribesman_of_jelut": ("abanissa_house_guard", "ahg", "House-Guard of Damudûr"),
        "pezarsani_javelineer": ("abanissa_jiret_javelineer", "ajj", "Jîret Javelineer"),
        "mahud_beast_rider": ("abanissa_ivory_rider", "air", "Ivory-Road Beast Rider"),
    },
}

# The source culture whose <Culture id="..."> row is swapped for the target's. Gundabad for the two
# orc kingdoms; the promoted cultures take theirs from the culture they were carved out of.
ELIGIBILITY_SRC = {
    "goblin": "gundabad", "mistymountainorcs": "gundabad",
    "bluecraig": "goblin", "lindon": "rivendell",
    "shaghana": "aserai", "abanissa": "aserai",
}

# Player-facing wording. Longest phrase FIRST so a substring never double-substitutes — the same
# ordering discipline insert_new_faction_cc_menus.py uses. The bare "Gundabad" catch-all runs last.
#
# The clone-contamination assertion at the end of this script is what actually enforces this table
# being complete; it fails the run if any source display word survives into a generated field.
TEXT_REMAP = {
    "goblin": [
        ("Gundabad Fell Warg Pack Leader", "Goblin-town Warg Master"),
        ("Gundabad Orc Hunter", "Goblin-town Tunnel Stalker"),
        ("Gundabad Berserker", "Goblin-town Frenzy"),
        ("Gundabad", "Goblin-town"),
    ],
    "mistymountainorcs": [
        ("Gundabad Fell Warg Pack Leader", "Misty Mountain Warg Chieftain"),
        ("Gundabad Orc Hunter", "Moria Deep-Marksman"),
        ("Gundabad Berserker", "Moria Frenzy"),
        ("Gundabad", "Moria"),
    ],
    # Blue Craig clones Goblin-town's careers, so the words being replaced are Goblin-town's own —
    # which are themselves this script's earlier output. The chain is deliberate: Gundabad ->
    # Goblin-town -> Blue Craig, each step renaming only what the previous step wrote.
    "bluecraig": [
        ("Goblin-town Tunnel Stalker", "Blue Craig Crag Stalker"),
        ("Goblin-town Warg Master", "Blue Craig Warg Master"),
        ("Goblin-town Troll-Driver", "Blue Craig Pit-Driver"),
        ("Goblin-town Frenzy", "Blue Craig Frenzy"),
        ("Goblin-town", "Blue Craig"),
    ],
    # Lindon clones Rivendell's three elven careers. Rivendell's are named for the Ñoldor; Círdan's
    # people are Falathrim Sindar, so the people-word changes even where the discipline does not.
    "lindon": [
        ("Ñoldor Blademaster", "Falathrim Blade-Dancer"),
        ("Ñoldor Sentinel", "Falathrim Sentinel"),
        ("Ñoldor Knight", "Falathrim Mariner"),
        ("Ñoldor", "Falathrim"),
        ("Rivendell", "Lindon"),
        ("Imladris", "Mithlond"),
    ],
    # Both Harad kingdoms clone Aserai, so only the words that name ASERAI's own places and peoples
    # change — Jelut, Pezarsan and Mahûd. The inherited group names that say "Haradwaith",
    # "Far Harad", "Southrons", "Scarlet" or "Mûmakan" stay: unlike the Ñoldor -> Falathrim case
    # those words are already correct for a Harad culture, and Tolkien gives the Haradrim chieftain
    # a black serpent upon scarlet, so Shaghâna keeps the serpent and the scarlet too.
    "shaghana": [
        ("Jelut Tribesman", "Zajâna Tribesman"),
        ("Pezarsan Javelineer", "Chatâk Javelineer"),
        ("Mahûd Beast-Tamer", "Sormedân Beast-Tamer"),
        ("Echoes of Mûmakan", "Horns of Mûmakan"),
        ("Poisoned Blades", "Serpent-Venom Blades"),
        ("Sunfire Volley", "Chatâk Volley"),
        ("Pezarsani", "Chatâk"),
        ("Pezarsan", "Chatâk"),
        ("Mahûd", "Sormedân"),
        ("Jelut", "Zajâna"),
    ],
    # Âbanissa's own notables are ivory, gold and gem traders, spice merchants, caravan lords and
    # house stewards, so its wording is mercantile-dynastic where Shaghâna's stays tribal.
    "abanissa": [
        ("Serpent-Banner Chieftain", "Ivory-Throne Chieftain"),
        ("Scarlet Spear-Master", "Gilded Spear-Master"),
        ("Jelut Tribesman", "Damudûr House-Guard"),
        ("Pezarsan Javelineer", "Jîret Javelineer"),
        ("Mahûd Beast-Tamer", "Ivory-Road Beast-Tamer"),
        ("Echoes of Mûmakan", "Tusk-Thunder of Mûmakan"),
        ("Sandstorm Raider", "Caravan Outrider"),
        ("Poisoned Blades", "Ivory-Ward Blades"),
        ("Sunfire Volley", "Jîret Volley"),
        ("Pezarsani", "Jîret"),
        ("Pezarsan", "Jîret"),
        ("Mahûd", "Ivory-Road"),
        ("Jelut", "Damudûr"),
    ],
}


def read(path):
    """Byte-faithful read. Returns (text_with_lf, newline) so the write can restore the original."""
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raise SystemExit(f"{path}: unexpected BOM — none of the career files ship one")
    text = raw.decode("utf-8")
    newline = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), newline


def write(path, text, newline):
    path.write_bytes(text.replace("\n", newline).encode("utf-8"))


# Attributes whose value names an ART or FX asset on disk rather than a data id. These must survive
# the rename untouched: a cloned career that asks for `goblin_troll_driver_particle` is asking for a
# particle system nobody has authored, and the same goes for its sprite and its activation sound.
# Only 21 of the 50 shipped careers even have a portrait registered in TAOMSpriteData.xml, so
# minting six more ids would have widened that gap instead of reusing what exists.
ASSET_ATTRS = ("portrait_sprite", "sprite", "icon_sprite", "particle_effect", "sound_effect")


def rename(block, src_id, src_abbr, new_id, new_abbr):
    """Rewrite both id-spaces, then put the asset references back."""
    block = block.replace(src_id, new_id)
    # `_ctm_` / `taom_ctm_` / `career_choice_ctm_` — always delimited by underscores, so anchoring on
    # them avoids mangling an unrelated word that merely contains the three letters.
    block = re.sub(rf"(?<=_){re.escape(src_abbr)}(?=_)", new_abbr, block)

    def restore(m):
        value = m.group(2).replace(new_id, src_id)
        value = re.sub(rf"(?<=_){re.escape(new_abbr)}(?=_)", src_abbr, value)
        return f'{m.group(1)}="{value}"'

    return re.sub(rf'({"|".join(ASSET_ATTRS)})="([^"]*)"', restore, block)


def retext(block, culture):
    for old, new in TEXT_REMAP[culture]:
        block = block.replace(old, new)
    return block


def extract(text, pattern, what):
    m = re.search(pattern, text, re.S)
    if not m:
        raise SystemExit(f"could not find {what} — the source file's shape has changed")
    return m.group(0)


def build(culture, src_id, src_abbr, new_id, new_abbr, display, careers, abilities, choices):
    """Return the four rendered blocks for one cloned career."""
    career = extract(careers, rf'  <Career id="{src_id}".*?</Career>', f"<Career> {src_id}")
    ability = extract(abilities, rf'  <AbilityTemplate id="{src_id}_ability".*?/>',
                      f"<AbilityTemplate> {src_id}_ability")
    root = extract(choices, rf'  <Choice id="{src_id}_root".*?</Choice>', f"root choice {src_id}")
    groups = re.findall(rf'  <ChoiceGroup id="{src_id}_[^"]*".*?</ChoiceGroup>', choices, re.S)
    if len(groups) != 6:
        raise SystemExit(f"{src_id}: expected 6 ChoiceGroups, found {len(groups)}")

    def fix(block):
        return retext(rename(block, src_id, src_abbr, new_id, new_abbr), culture)

    career = fix(career)
    # Repoint eligibility at the target culture. The source is single-culture, so a plain replace is
    # unambiguous; assert that rather than trust it.
    elig = ELIGIBILITY_SRC[culture]
    if career.count(f'<Culture id="{elig}" />') != 1:
        raise SystemExit(f"{src_id}: expected exactly one {elig} <Culture> row in <Career>")
    career = career.replace(f'<Culture id="{elig}" />', f'<Culture id="{culture}" />')
    # display_name carries the career's own name, which the TEXT_REMAP table cannot know — it would
    # otherwise inherit whatever the source ability was called ("Gundabad Berserker" -> "Goblin-town
    # Frenzy", which is the ABILITY's name, not the career's). The key is `{=taom_career_<id>}`.
    career, n = re.subn(rf'(display_name="\{{=taom_career_{re.escape(new_id)}\}})[^"]*"',
                        rf'\g<1>{display}"', career)
    if n != 1:
        raise SystemExit(f"{new_id}: expected 1 display_name to rewrite, rewrote {n}")

    return career, fix(ability), fix(root) + "\n\n" + "\n\n".join(fix(g) for g in groups)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    args = ap.parse_args()

    careers, careers_nl = read(CAREERS_XML)
    abilities, abilities_nl = read(ABILITIES_XML)
    choices, choices_nl = read(CHOICES_XML)
    menu_text, menu_nl = read(MENU_JSON)

    new_careers, new_abilities, new_choices, new_menu, planned = [], [], [], [], []

    for culture, mapping in TARGETS.items():
        if f'<Culture id="{culture}" />' in careers:
            print(f"skip {culture}: already has careers")
            continue
        for src_id, src_abbr in SOURCES_FOR.get(culture, SOURCES):
            new_id, new_abbr, display = mapping[src_id]
            c, a, ch = build(culture, src_id, src_abbr, new_id, new_abbr, display,
                             careers, abilities, choices)
            new_careers.append(c)
            new_abilities.append(a)
            new_choices.append(
                f"  <!-- ═══ {display} ({culture}) — cloned from {src_id} ═══ -->\n" + ch)
            new_menu.append({"career_string_id": new_id, "skills": [], "attribute": "",
                             "focus_to_add": 0, "skill_level_to_add": 0, "attribute_level_to_add": 0})
            planned.append((culture, new_id, display))

    if not planned:
        print("nothing to do")
        return

    # Clone-contamination gate. An id-rename is case-sensitive and leaves player-facing strings
    # naming the source faction — the exact defect the new-factions RCA recorded, caught there only
    # after it shipped. Fail the run rather than emit it.
    generated = "\n".join(new_careers + new_abilities + new_choices)
    # Two things legitimately still name the source and must not trip the gate: provenance comments
    # (recording where a clone came from is the point) and asset references (deliberately reused —
    # see ASSET_ATTRS). Everything else naming Gundabad is contamination.
    payload = re.sub(r"<!--.*?-->", "", generated, flags=re.S)
    payload = re.sub(rf'({"|".join(ASSET_ATTRS)})="[^"]*"', "", payload)
    # Remove the ids this run MINTED before scanning for source ids. A target id can contain its own
    # source id as a substring — `blade_dancer` -> `falathrim_blade_dancer` — and reporting that as
    # contamination flags a correct rename as a failure. Longest first so nested names clear cleanly.
    for _, new_id, _ in planned:
        payload = payload.replace(new_id, "")
    for _, new_abbr in sorted({(t, a) for t in TARGETS
                               for _, a, _ in TARGETS[t].values()}, key=lambda x: -len(x[1])):
        payload = payload.replace(f"_{new_abbr}_", "_")
    # Display words the id/abbr scan below cannot see. Every source faction whose careers get cloned
    # needs its own people- and place-words listed here, or a TEXT_REMAP entry missed by the author
    # ships Aserai wording under a Harad career exactly the way "Gundabad" once shipped under a
    # Goblin-town one. Longest first so a nested word is reported at its most specific form.
    SOURCE_DISPLAY_WORDS = ["Gundabad", "Pezarsani", "Pezarsan", "Mahûd", "Jelut"]
    source_words = SOURCE_DISPLAY_WORDS + [s for s, _ in SOURCES] + [f"_{a}_" for _, a in SOURCES]
    for tgt in TARGETS:
        for s, a in SOURCES_FOR.get(tgt, []):
            source_words += [s, f"_{a}_"]
    for word in source_words:
        if word in payload:
            raise SystemExit(f"clone contamination: '{word}' survived into generated output")
    ids = re.findall(r'<(?:Career|Choice|ChoiceGroup|AbilityTemplate) id="([^"]+)"', generated)
    clash = sorted(i for i in set(ids) if f'id="{i}"' in careers + abilities + choices)
    if clash:
        raise SystemExit(f"id collision with existing data: {clash[:8]}")
    if len(ids) != len(set(ids)):
        raise SystemExit("duplicate ids within generated output")

    print(f"{len(planned)} careers to add:")
    for culture, new_id, display in planned:
        print(f"  {culture:20s} {new_id:24s} {display}")
    print(f"  -> {len(ids)} new elements total")

    if not args.apply:
        print("DRY RUN — re-run with --apply to write")
        return

    careers = careers.replace("\n</Careers>", "\n" + "\n\n".join(new_careers) + "\n\n</Careers>")
    abilities = abilities.replace("\n</AbilityTemplates>",
                                  "\n" + "\n".join(new_abilities) + "\n\n</AbilityTemplates>")
    choices = choices.replace("\n</CareerChoices>",
                              "\n" + "\n\n".join(new_choices) + "\n\n</CareerChoices>")

    menu = json.loads(menu_text)
    menu.extend(new_menu)
    menu_out = json.dumps(menu, indent=4, ensure_ascii=False) + "\n"

    write(CAREERS_XML, careers, careers_nl)
    write(ABILITIES_XML, abilities, abilities_nl)
    write(CHOICES_XML, choices, choices_nl)
    write(MENU_JSON, menu_out, menu_nl)
    print(f"applied -> {CAREERS_XML.name}, {ABILITIES_XML.name}, {CHOICES_XML.name}, {MENU_JSON.name}")


if __name__ == "__main__":
    sys.exit(main())
