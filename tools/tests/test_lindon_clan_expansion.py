"""Shipped-data gates for the 2026-10-27 Lindon expansion (#1039, TAOM-LINDON-EXPANSION blocks).

Run:  python -m pytest tools/tests/test_lindon_clan_expansion.py -q

The blocks were written by tools/oneoff/author_lindon_clans.py; the XML is the source of truth, so
these read the shipped files. The fief, rename and face tests need the install (the live TAOM_Map
settlements.xml, vanilla SandBox / SandBoxCore ModuleData) and skip, naming why, when it is absent.
"""
import codecs
import re
import sys
import unittest
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from _gamedir import game_dir  # noqa: E402

MD = REPO / "Main" / "_Module" / "ModuleData"
BLOCK = re.compile(r"<!-- TAOM-LINDON-EXPANSION:BEGIN -->(.*?)<!-- TAOM-LINDON-EXPANSION:END -->", re.S)
HEX_KEY = re.compile(r"[0-9A-Fa-f]{100,}")
GAME = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
VANILLA_MD = [GAME / "Modules" / "SandBox" / "ModuleData", GAME / "Modules" / "SandBoxCore" / "ModuleData"]
LIVE_SETTLEMENTS = GAME / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"

NEW_CLANS = {
    "clan_lindon_3": ("town_LN3", "Nos Galdor", "5", "lord_LN3_1"),
    "clan_lindon_4": ("castle_LN1", "Nos Brithombar", "3", "lord_LN4_1"),
    "clan_lindon_5": ("castle_LN2", "Wardens of Ered Luin", "3", "lord_LN5_1"),
    "clan_lindon_6": ("castle_LN3", "Nos Eglarest", "2", "lord_LN6_1"),
}
# One fief per clan; Mithlond stays with the Falathrim.
FIEF_OWNERS = {
    "town_LN1": "clan_lindon_1", "town_LN2": "clan_lindon_2", "town_LN3": "clan_lindon_3",
    "castle_LN1": "clan_lindon_4", "castle_LN2": "clan_lindon_5", "castle_LN3": "clan_lindon_6",
}
NEW_LORDS = 13
LORD_CLANS = {
    "lord_LN3_1": "clan_lindon_3", "lord_LN3_2": "clan_lindon_3", "lord_LN3_3": "clan_lindon_3", "lord_LN3_4": "clan_lindon_3",
    "lord_LN4_1": "clan_lindon_4", "lord_LN4_2": "clan_lindon_4", "lord_LN4_3": "clan_lindon_4",
    "lord_LN5_1": "clan_lindon_5", "lord_LN5_2": "clan_lindon_5", "lord_LN5_3": "clan_lindon_5",
    "lord_LN6_1": "clan_lindon_6", "lord_LN6_2": "clan_lindon_6", "lord_LN6_3": "clan_lindon_6",
}
# Display-name renames that keep the map free of duplicates once Harlond and Forlond exist. The live module is
# unversioned, so this is the repo's only gate on them: a reinstall that restores the old names fails here.
RENAMED = {"castle_EW1": "Harlond Quays", "village_LN1_1": "Eryn Lhûn", "village_LN1_3": "Ninglas"}
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]


def read(path):
    return Path(path).read_bytes().decode("utf-8")


def read_loose(path):
    raw = Path(path).read_bytes()
    if raw.startswith((codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)):
        return raw.decode("utf-16")
    return raw.decode("utf-8", errors="replace")


def block(path):
    m = BLOCK.search(read(path))
    assert m, f"no TAOM-LINDON-EXPANSION block in {path}"
    return m.group(1)


def npc_rows(text):
    rows = []
    for m in re.finditer(r"<NPCCharacter\b([^>]*)>(.*?)</NPCCharacter>", text, re.S):
        ident = re.search(r'\bid="([^"]+)"', m.group(1))
        rows.append((ident.group(1) if ident else "?", m.group(0)))
    return rows


def faction_attrs(text, cid):
    m = re.search(r'<Faction\s[^>]*?id="%s"[^>]*?/>' % re.escape(cid), text, re.S)
    assert m, f"{cid} not found"
    return dict(re.findall(r'(\w+)="([^"]*)"', m.group(0)))


def elven_sheet_icons():
    """{icon id: its group's name} of TAOM's banner icons. The group, not the sheet: #1047 packed the
    elven icons onto shared taom_banners_packed_* sheets."""
    return {i.get("id"): g.get("name") for g in ET.parse(MD / "banner_icons.xml").iter("BannerIconGroup")
            for i in g.iter("Icon")}


def sigil_layers(case, owner, key, icons):
    """The sigil icon ids of a banner key, after asserting it has at least one and each is on an elven sheet."""
    parts = key.split(".")
    case.assertEqual(0, len(parts) % 10, owner)
    layers = [parts[i:i + 10] for i in range(0, len(parts), 10)]
    case.assertGreater(len(layers), 1, f"{owner} flies no sigil")
    for layer in layers[1:]:
        case.assertRegex(icons.get(layer[0], "undefined"), r"^\{=taom_banner_category_elven\}", (owner, layer[0]))
    return tuple(layer[0] for layer in layers[1:])


class LindonClanExpansionDataTests(unittest.TestCase):
    def test_new_clans_names_tiers_homes_and_owners(self):
        text = block(MD / "characters" / "clans.xml")
        got = {}
        for cid in NEW_CLANS:
            a = faction_attrs(text, cid)
            name = re.sub(r"^\{=[^}]+\}", "", a["name"])
            got[cid] = (a["initial_home_settlement"].removeprefix("Settlement."), name, a["tier"],
                        a["owner"].removeprefix("Hero."))
            self.assertEqual("Culture.lindon", a["culture"], cid)
            self.assertEqual("Kingdom.lindon", a["super_faction"], cid)
        self.assertEqual(NEW_CLANS, got)

    def test_edhil_mithlond_moves_home_to_harlond(self):
        a = faction_attrs(read(MD / "characters" / "clans.xml"), "clan_lindon_2")
        self.assertEqual("Settlement.town_LN2", a["initial_home_settlement"])

    def test_every_lindon_clan_flies_its_own_elven_banner(self):
        # The generator cloned every new-faction clan from clan_gundabad_1, so all six Lindon clans flew its orc crest
        # (icons 22000 and 22001) until Mike's 2026-10-28 report; each house now has its own Noldor or Sindar sigil.
        icons = elven_sheet_icons()
        keys = {f.get("id"): f.get("banner_key") or "" for f in ET.parse(MD / "characters" / "clans.xml").iter("Faction")
                if f.get("super_faction") == "Kingdom.lindon"}
        self.assertLessEqual({f"clan_lindon_{n}" for n in range(1, 7)}, set(keys))
        sigils = {cid: sigil_layers(self, cid, key, icons) for cid, key in keys.items()}
        self.assertEqual(len(keys), len(set(sigils.values())), sigils)

    def test_the_lindon_kingdom_flies_an_elven_banner_of_its_own(self):
        # Kingdom.lindon was cloned from Kingdom.rivendell banner and all; the ruling clan shows the kingdom's banner.
        kingdoms = {k.get("id"): k.get("banner_key") or "" for k in ET.parse(MD / "taom_spkingdoms.xml").iter("Kingdom")}
        sigil_layers(self, "Kingdom.lindon", kingdoms["lindon"], elven_sheet_icons())
        self.assertNotEqual(kingdoms["rivendell"], kingdoms["lindon"])

    def test_every_new_lord_is_a_hero_of_its_clan_and_each_owner_leads_his_own(self):
        heroes = dict(re.findall(r'id="(lord_LN\d_\d+)"[^/]*?faction="Faction\.(clan_lindon_\d)"',
                                 block(MD / "characters" / "heroes.xml"), re.S))
        lords = [lid for lid, _ in npc_rows(block(MD / "characters" / "lords.xml"))]
        self.assertEqual(NEW_LORDS, len(lords))
        self.assertEqual(sorted(lords), sorted(heroes))
        self.assertEqual(LORD_CLANS, heroes)
        for cid, (_h, _n, _t, owner) in NEW_CLANS.items():
            self.assertEqual(cid, heroes.get(owner), f"{owner} must belong to {cid}")

    def test_ladies_carry_their_own_hair_tag_and_no_beard(self):
        bad = []
        for lid, body in npc_rows(block(MD / "characters" / "lords.xml")):
            female = 'is_female="true"' in body
            if female and ("<hair_tag " not in body or "<beard_tags>" in body):
                bad.append(lid)
        self.assertEqual([], bad)

    @unittest.skipUnless(LIVE_SETTLEMENTS.is_file(), f"live TAOM_Map settlements.xml not found under {GAME}")
    def test_each_lindon_fief_is_held_by_its_clan(self):
        live = read(LIVE_SETTLEMENTS)
        got = {}
        for sid in FIEF_OWNERS:
            m = re.search(r'<Settlement id="%s"[^>]*?\sowner="Faction\.([^"]+)"' % re.escape(sid), live)
            got[sid] = m.group(1) if m else None
        self.assertEqual(FIEF_OWNERS, got)

    @unittest.skipUnless(LIVE_SETTLEMENTS.is_file(), f"live TAOM_Map settlements.xml not found under {GAME}")
    def test_renamed_settlements_keep_their_new_names_in_every_language(self):
        live = read(LIVE_SETTLEMENTS)
        got = {}
        for sid in RENAMED:
            m = re.search(r'<Settlement id="%s" name="\{=[^}]+\}([^"]+)"' % re.escape(sid), live)
            got[sid] = m.group(1) if m else None
        self.assertEqual(RENAMED, got)
        bad = []
        for lang in LANGS:
            loc = read(LIVE_SETTLEMENTS.parent / "Languages" / lang / "loc_settlements.xml")
            for sid, name in RENAMED.items():
                m = re.search(r'id="Settlements\.Settlement\.name\.%s"\s+text="(?:\{\.[A-Z]+\})?([^"]*)"'
                              % re.escape(sid), loc)
                if not m or m.group(1) != name:
                    bad.append((lang, sid, m.group(1) if m else None))
        self.assertEqual([], bad)

    @unittest.skipUnless(all(p.is_dir() for p in VANILLA_MD), f"vanilla ModuleData not found under {GAME}")
    def test_new_lord_faces_exist_nowhere_else(self):
        """A face key in the block appears nowhere else in TAOM or vanilla SandBox/SandBoxCore, except on
        vanilla rows that lords.xslt replaces (so the vanilla face never loads)."""
        lords_text = read(MD / "characters" / "lords.xml")
        mine = block(MD / "characters" / "lords.xml")
        keys = [k.upper() for k in re.findall(r'<BodyProperties[^>]*?key="([0-9A-Fa-f]+)"', mine)]
        self.assertEqual(NEW_LORDS, len(keys))
        self.assertEqual(NEW_LORDS, len(set(keys)), "a face key is used by two new lords")

        rest = Counter()
        for f in MD.rglob("*"):
            if f.is_file() and f.suffix.lower() in (".xml", ".xslt", ".json", ".txt"):
                text = lords_text.replace(mine, "") if f == MD / "characters" / "lords.xml" else read(f)
                rest.update(k.upper() for k in HEX_KEY.findall(text))
        self.assertEqual([], [k for k in keys if rest[k]], "key reused inside Main/_Module/ModuleData")

        # The same rule the one-off picked donors by: a lords.xslt template for the id that writes its own face.
        xslt = re.sub(r"<!--.*?-->", "", read(MD / "lords.xslt"), flags=re.S)
        replaced = {m.group(1) for m in re.finditer(
            r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">(?:(?!</xsl:template>).)*?<BodyProperties",
            xslt, re.S)}
        owners = {k: [] for k in keys}
        for root in VANILLA_MD:
            for f in root.rglob("*.xml"):
                text = read_loose(f)
                upper = text.upper()
                found = [k for k in keys if k in upper]
                if not found:
                    continue
                rows = [(lid, body.upper()) for lid, body in npc_rows(text)]
                for k in found:
                    owners[k].extend([lid for lid, b in rows if k in b]
                                     or [f"{f.name} (outside an NPCCharacter)"])
        self.assertEqual([], [k for k, o in owners.items() if not o], "a key has no vanilla donor: nothing was scanned")
        bad = {k: o for k, o in owners.items() if any(x not in replaced for x in o)}
        self.assertEqual({}, bad)


if __name__ == "__main__":
    unittest.main()
