#!/usr/bin/env python3
"""Every input generate_new_faction_kingdoms.py reads by id exists, in exactly one place.

Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms_inputs

#648 folded the vanilla-id lords' characters/lords.xml rows into lords.xslt. Two of those rows were template
lords (lord_1_57_1, lord_4_16_1) and sixteen carried the Gondor faces Arthedain was generated from, so every run
died on a missing row and a repaired one would have re-faced Arthedain. Those inputs now live, frozen, in
tools/data/new_faction_kingdom_templates.json. This catches the next deleted or renamed input before the
eight --check runs in test_generator_regions_in_sync fail on it.
"""
import os
import re
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_new_faction_kingdoms as gen  # noqa: E402


class TemplateInputTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lords = gen.read_doc(os.path.join(gen.CH, "lords.xml"))[0]
        cls.fixture = gen.load_fixture()

    def test_the_fixture_file_exists(self):
        self.assertTrue(os.path.isfile(gen.TEMPLATES_FIXTURE), gen.TEMPLATES_FIXTURE)

    def test_every_template_lord_is_one_lords_xml_row_or_one_fixture_copy(self):
        ids = [i for pair in gen.LORD_TEMPLATES.values() for i in pair]
        self.assertEqual(len(ids), 12)
        for lord_id in ids:
            with self.subTest(lord=lord_id):
                rows = len(re.findall(r'<NPCCharacter\b[^>]*\bid="%s"' % re.escape(lord_id), self.lords))
                copies = 1 if lord_id in self.fixture["lords"] else 0
                # Neither: the run dies. Both: the frozen copy silently shadows a live row.
                self.assertEqual(rows + copies, 1, f"{rows} lords.xml rows, {copies} fixture copies")

    def test_every_fixture_lord_is_a_whole_row_of_its_id_and_is_used(self):
        used = {i for pair in gen.LORD_TEMPLATES.values() for i in pair}
        self.assertTrue(self.fixture["lords"])
        for lord_id, text in self.fixture["lords"].items():
            with self.subTest(lord=lord_id):
                self.assertIn(lord_id, used)
                self.assertEqual(ET.fromstring(text).get("id"), lord_id)

    def test_every_faces_from_culture_has_a_pinned_pool_of_faces(self):
        cultures = {k["faces_from"] for k in gen.KINGDOMS.values() if k.get("faces_from")}
        self.assertEqual(cultures, {"gondor"})
        for culture in cultures:
            with self.subTest(culture=culture):
                male, female = gen.pinned_faces(self.fixture, culture)
                self.assertTrue(male and female)
                for face in male + female:
                    self.assertRegex(face, r'(?s)^<face>.*\bkey="[0-9A-Fa-f]+".*</face>$')

    def test_the_template_kingdoms_and_clan_exist(self):
        spk = gen.read_doc(os.path.join(gen.MD, "taom_spkingdoms.xml"))[0]
        clans = gen.read_doc(os.path.join(gen.CH, "clans.xml"))[0]
        for kid in ("gundabad", "rivendell"):
            self.assertRegex(spk, r'<Kingdom\b[^>]*\bid="%s"' % kid)
        self.assertRegex(clans, r'<Faction\b\s*\n\s*id="clan_gundabad_1"')

    def test_an_id_in_neither_source_is_an_error(self):
        with self.assertRaisesRegex(RuntimeError, "not found"):
            gen.template_lord("<NPCCharacters />", {"lords": {}}, "lord_missing_1")

    def test_a_culture_with_no_pinned_pool_is_an_error(self):
        with self.assertRaisesRegex(RuntimeError, "no face pool"):
            gen.pinned_faces({"face_pools": {}}, "gondor")


if __name__ == "__main__":
    unittest.main()
