"""Shipped-data gates for the 2026-10-20 Dale lord expansion (TAOM-DALE-EXPANSION blocks).

Run:  python -m pytest tools/tests/test_dale_lord_expansion.py -q

The blocks were written by tools/author_dale_lords.py (now tools/oneoff/); the XML is the source of
truth, so these read the shipped files. The face test needs the vanilla SandBox / SandBoxCore
ModuleData and skips, naming why, when the install is absent.
"""
import codecs
import re
import sys
import unittest
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from _gamedir import game_dir  # noqa: E402

MD = REPO / "Main" / "_Module" / "ModuleData"
BLOCK = re.compile(r"<!-- TAOM-DALE-EXPANSION:BEGIN -->(.*?)<!-- TAOM-DALE-EXPANSION:END -->", re.S)
HEX_KEY = re.compile(r"[0-9A-Fa-f]{100,}")
GAME = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
VANILLA_MD = [GAME / "Modules" / "SandBox" / "ModuleData", GAME / "Modules" / "SandBoxCore" / "ModuleData"]
DALE_BANNER = "11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0"


def read(path):
    return Path(path).read_bytes().decode("utf-8")


def read_loose(path):
    raw = Path(path).read_bytes()
    if raw.startswith((codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)):
        return raw.decode("utf-16")
    return raw.decode("utf-8", errors="replace")


def block(path):
    m = BLOCK.search(read(path))
    assert m, f"no TAOM-DALE-EXPANSION block in {path}"
    return m.group(1)


def npc_rows(text):
    rows = []
    for m in re.finditer(r"<NPCCharacter\b([^>]*)>(.*?)</NPCCharacter>", text, re.S):
        ident = re.search(r'\bid="([^"]+)"', m.group(1))
        rows.append((ident.group(1) if ident else "?", m.group(0)))
    return rows


class DaleLordExpansionDataTests(unittest.TestCase):
    def test_bowmen_carry_a_bow_roster(self):
        """Only dale_bat_template_medium_a and _b carry a bow; a bowman on another roster fires nothing."""
        bad = []
        for lid, body in npc_rows(block(MD / "characters" / "lords.xml")):
            if "SkillSet.taom_dale_bowman_skills" not in body:
                continue
            ids = re.findall(r'<EquipmentSet id="(dale_bat_template_medium_\w)"', body)
            if not ids or any(i not in ("dale_bat_template_medium_a", "dale_bat_template_medium_b") for i in ids):
                bad.append((lid, ids))
        self.assertEqual([], bad)

    def test_every_new_clan_uses_the_shared_dale_banner(self):
        keys = re.findall(r'banner_key="([^"]+)"', block(MD / "characters" / "clans.xml"))
        self.assertEqual(5, len(keys))
        self.assertEqual({DALE_BANNER}, set(keys))
        self.assertIn(f">{DALE_BANNER}<", read(MD / "spclans.xslt"))

    def test_new_clan_names_and_homes(self):
        text = block(MD / "characters" / "clans.xml")
        rows = {m.group(1): m for m in re.finditer(
            r'id="(clan_sturgia_\d+)"\s+initial_home_settlement="Settlement\.(\w+)"\s+name="\{=([^}]+)\}([^"]+)"', text)}
        got = {cid: (m.group(2), m.group(3), m.group(4)) for cid, m in rows.items()}
        self.assertEqual({
            "clan_sturgia_10": ("castle_S4", "aom_clan_sturgia_10_name", "Falkes"),
            "clan_sturgia_11": ("castle_S1", "aom_clan_sturgia_11_name", "Calmar"),
            "clan_sturgia_12": ("castle_S3", "aom_clan_sturgia_12_name", "Macher"),
            "clan_sturgia_13": ("town_S4", "aom_clan_sturgia_13_name", "Foriking"),
            "clan_sturgia_14": ("town_S5", "aom_clan_sturgia_14_name", "Hill"),
        }, got)

    @unittest.skipUnless(all(p.is_dir() for p in VANILLA_MD), f"vanilla ModuleData not found under {GAME}")
    def test_new_lord_faces_exist_nowhere_else(self):
        """A face key in the block appears nowhere else in TAOM or vanilla SandBox/SandBoxCore (campaign
        data; Custom Battle's own characters are not scanned), except on vanilla rows that
        lords.xslt replaces (so the vanilla face never loads)."""
        lords_text = read(MD / "characters" / "lords.xml")
        mine = block(MD / "characters" / "lords.xml")
        keys = [k.upper() for k in re.findall(r'<BodyProperties[^>]*?key="([0-9A-Fa-f]+)"', mine)]
        self.assertEqual(27, len(keys))
        self.assertEqual(27, len(set(keys)), "a face key is used by two new lords")

        rest = Counter()
        for f in MD.rglob("*"):
            if f.is_file() and f.suffix.lower() in (".xml", ".xslt", ".json", ".txt"):
                text = lords_text.replace(mine, "") if f == MD / "characters" / "lords.xml" else read(f)
                rest.update(k.upper() for k in HEX_KEY.findall(text))
        self.assertEqual([], [k for k in keys if rest[k]], "key reused inside Main/_Module/ModuleData")

        xslt = re.sub(r"<!--.*?-->", "", read(MD / "lords.xslt"), flags=re.S)
        replaced = set(re.findall(r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">", xslt))
        owners = {k: [] for k in keys}
        for root in VANILLA_MD:
            for f in root.rglob("*.xml"):
                text = read_loose(f)
                for k in keys:
                    if k not in text.upper():
                        continue
                    for lid, body in npc_rows(text):
                        if k in body.upper():
                            owners[k].append(lid)
                    if not any(k in b.upper() for _l, b in npc_rows(text)):
                        owners[k].append(f"{f.name} (outside an NPCCharacter)")
        bad = {k: o for k, o in owners.items() if any(x not in replaced for x in o)}
        self.assertEqual({}, bad)


if __name__ == "__main__":
    unittest.main()
