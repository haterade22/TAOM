#!/usr/bin/env python3
"""upsert() in generate_new_faction_kingdoms.py: a re-run keeps its marker block where it stands.

Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms

#947: the Dale expansion (64345bf3a) appended its own marker block after the Arthedain block in
clans.xml, lords.xml and heroes.xml. upsert() deleted the Arthedain block and re-appended it before
the closing tag, so --check reported drift for a change of position alone, and --apply would have
moved Arthedain below Dale.
"""
import glob
import os
import re
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_new_faction_kingdoms as gen  # noqa: E402

MARKER = "TAOM-NEWFACTIONS:arthedain"


def doc(arthedain_rows, after=""):
    return ("<Factions>\n"
            "  <Faction id=\"vanilla\" />\n"
            f"  <!-- {MARKER}:BEGIN -->\n{arthedain_rows}  <!-- {MARKER}:END -->\n"
            f"{after}"
            "</Factions>\n")


DALE = ("  <!-- TAOM-DALE-EXPANSION:BEGIN -->\n"
        "  <Faction id=\"clan_sturgia_10\" />\n"
        "  <!-- TAOM-DALE-EXPANSION:END -->\n")


class UpsertTests(unittest.TestCase):
    def test_same_payload_with_a_block_after_it_changes_nothing(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = doc(rows, after=DALE)
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), text)

    def test_new_payload_replaces_the_block_in_place(self):
        old = "  <Faction id=\"clan_arthedain_1\" />\n"
        new = "  <Faction id=\"clan_arthedain_1\" tier=\"5\" />\n"
        self.assertEqual(gen.upsert(doc(old, after=DALE), "</Factions>", new, MARKER), doc(new, after=DALE))

    def test_absent_block_is_appended_before_the_closing_tag(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = "<Factions>\n  <Faction id=\"vanilla\" />\n</Factions>\n"
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_a_second_region_is_refused(self):
        # The old strip-all upsert made --check report a stale second copy; replacing only the first would hide it,
        # and the engine loads the later row of a repeated id.
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        stale = f"  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_arthedain_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=DALE + stale), "</Factions>", rows, MARKER)

    def test_a_begin_without_its_end_is_refused(self):
        text = f"<Factions>\n  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_arthedain_1\" />\n</Factions>\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(text, "</Factions>", "  <Faction id=\"clan_arthedain_1\" />\n", MARKER)

    def test_an_end_without_its_begin_is_refused(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        orphan = f"  <Faction id=\"clan_arthedain_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=orphan), "</Factions>", rows, MARKER)

    def test_a_missing_closing_tag_is_refused(self):
        with self.assertRaises(RuntimeError):
            gen.upsert("<Factions>\n", "</Factions>", "  <Faction id=\"x\" />\n", MARKER)

    def test_a_missing_closing_tag_is_refused_when_the_region_exists(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows).replace("</Factions>\n", ""), "</Factions>", rows, MARKER)

    def test_an_end_marker_straight_before_the_closing_tag_is_a_region(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = doc(rows).replace(f":END -->\n</Factions>", ":END --></Factions>")
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_payload_backslashes_are_written_verbatim(self):
        rows = "  <Faction id=\"x\" name=\"a\\1b\" />\n"
        self.assertIn("a\\1b", gen.upsert(doc("", after=DALE), "</Factions>", rows, MARKER))


class BannerTests(unittest.TestCase):
    """A cloned clan keeps clan_gundabad_1's orc crest unless its realm names a banner (Lindon did not, 2026-10-28)."""

    def test_every_regeneratable_realm_names_a_banner_for_its_kingdom_and_each_clan(self):
        for rid in gen.REGENERATABLE:
            k = gen.KINGDOMS[rid]
            self.assertTrue(k.get("banner"), rid)
            self.assertIn("kingdom_icon", k, rid)
            self.assertEqual(len(k["clans"]), len(k.get("clan_icons", [])), rid)
            self.assertLessEqual(set(k.get("clan_banner_keys", {})), {c[0] for c in k["clans"]}, rid)

    def test_a_banner_with_an_emblem_colour_slot_names_that_colour_for_every_clan(self):
        for rid in gen.REGENERATABLE:
            k = gen.KINGDOMS[rid]
            if "{fg}" not in k["banner"]:
                continue
            self.assertIn("sigil", k, rid)
            self.assertEqual(len(k["clans"]), len(k.get("clan_sigils", k["clans"])), rid)
            for ci in range(len(k["clans"])):
                self.assertNotIn("None", gen.clan_banner(k, ci), (rid, ci))

    def test_cardolan_flies_white_on_green_and_tharbad_gold(self):
        k = gen.KINGDOMS["cardolan"]
        self.assertEqual(gen.kingdom_banner(k), "11.2027.2027.1528.1528.764.764.1.0.0.31005.2004.2004.700.700.764.764.0.0.0")
        self.assertEqual(gen.clan_banner(k, 0), gen.kingdom_banner(k))
        self.assertIn(".11006.238.238.", gen.clan_banner(k, 1))
        self.assertIn(".11000.2004.2004.", gen.clan_banner(k, 2))
        self.assertIn(".11001.2004.2004.", gen.clan_banner(k, 3))

    def test_cardolan_houses_dress_in_the_banner_green_and_white(self):
        # Patch23 tints troops, portraits and map parties from the clan colours, so they follow the banner
        for color, color2 in gen.KINGDOMS["cardolan"]["clan_colors"]:
            r, g, b = (int(color[i:i + 2], 16) for i in (2, 4, 6))
            self.assertTrue(g > r and g > b, color)
            self.assertTrue(all(int(color2[i:i + 2], 16) >= 0xE0 for i in (2, 4, 6)), color2)

    def test_a_hand_made_banner_key_wins_over_the_template(self):
        # Layered banners (72715c417, Ered Luin and Arthedain) cannot come from the one-icon template.
        k = dict(banner="11.1.1.1528.1528.764.764.1.0.0.{icon}.2.2.700.700.764.764.0.0.0",
                 kingdom_icon=10, clan_icons=[10, 11], kingdom_banner_key="KINGDOM_KEY",
                 clans=[("clan_a", 1, "town", "A"), ("clan_b", 1, "town", "B")], clan_banner_keys={"clan_b": "CLAN_KEY"})
        self.assertEqual("KINGDOM_KEY", gen.kingdom_banner(k))
        self.assertEqual("CLAN_KEY", gen.clan_banner(k, 1))
        self.assertEqual("11.1.1.1528.1528.764.764.1.0.0.10.2.2.700.700.764.764.0.0.0", gen.clan_banner(k, 0))

    def test_a_realm_without_the_slot_keeps_its_one_emblem_colour(self):
        k = gen.KINGDOMS["arthedain"]
        self.assertEqual(gen.kingdom_banner(k), "11.190.190.1528.1528.764.764.1.0.0.11004.110.110.700.700.764.764.0.0.0")

class KingdomNameTests(unittest.TestCase):
    """A kingdom's name is the bare realm name, as Erebor, Imladris and Lindon are; "Kingdom of X" belongs to the
    title and the faction card (#1050: Arthedain, Cardolan, Rhudaur and Ered Luin shipped as "Kingdom of X")."""

    # Each language's word for "kingdom", from the old "Kingdom of Arthedain" rows the translator wrote.
    KINGDOM_WORDS = {
        "BR": ["Reino"], "SP": ["Reino"], "IT": ["Regno"], "FR": ["Royaume"], "DE": ["Königreich"],
        "PL": ["Królestwo"], "RU": ["Королевство"], "TR": ["Krallığı"], "CNs": ["王国"], "CNt": ["王國"],
        "JP": ["王国"], "KO": ["왕국"],
    }
    NAME = re.compile(r'<Kingdom\b[^>]*?\bname="\{\{?=([^}]+)\}\}?([^"]*)"', re.S)
    ROW = re.compile(r'<string id="([^"]+)" text="([^"]*)"')

    def shipped_names(self, md=None):
        md = md or gen.MD
        names = []
        for f in ("taom_spkingdoms.xml", "spkingdoms.xslt"):
            with open(os.path.join(md, f), encoding="utf-8") as fh:
                names += self.NAME.findall(fh.read())
        return names

    def language_offenders(self, md=None):
        md = md or gen.MD
        bare = [(key, text) for key, text in self.shipped_names(md) if "Kingdom" not in text]
        found = []
        for lang, words in self.KINGDOM_WORDS.items():
            rows = {}
            for path in glob.glob(os.path.join(md, "Languages", lang, "*.xml")):
                with open(path, encoding="utf-8") as fh:
                    rows.update(self.ROW.findall(fh.read()))
            for key, _ in bare:
                row = rows.get(key)
                if row is None or any(w in row for w in words):
                    found.append(f"{lang} {key}: {row!r}")
        return found

    def test_no_generated_kingdom_name_starts_with_kingdom_of(self):
        for rid, k in gen.KINGDOMS.items():
            self.assertFalse(k["name"].startswith("Kingdom of "), f"{rid}: {k['name']!r}; put it in title=")

    def test_no_shipped_kingdom_name_starts_with_kingdom_of(self):
        names = self.shipped_names()
        self.assertGreaterEqual(len(names), 20, "the kingdom regex matched too little to prove anything")
        self.assertEqual([t for _, t in names if t.startswith("Kingdom of ")], [])

    def test_no_language_row_of_a_bare_kingdom_name_adds_the_word_for_kingdom(self):
        # Also refuses a missing row: a bare name with no row in a language cannot be proven bare there.
        self.assertEqual(self.language_offenders(), [])

if __name__ == "__main__":
    unittest.main()
