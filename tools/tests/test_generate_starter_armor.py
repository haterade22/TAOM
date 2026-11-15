#!/usr/bin/env python
"""Unit tests for tools/generate_starter_armor.py, on synthetic XML, no game install.

Run:  python -m unittest tools.tests.test_generate_starter_armor

The starter armour is the kit of a tier 1 to 2 recruit. A body carries no arm or leg armour below the heavy
band, and the literal TEMPLATES would out-armour that recruit in some cultures, so each value is the smaller
of the literal and the culture's light band value.
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_starter_armor as gs  # noqa: E402
import rebalance_armor as ra  # noqa: E402

ARCHETYPES = ("ranged", "cavalry", "infantry")


def _light(folder, slot_type, taom="x", arch="infantry", slot="body"):
    key = ra.kingdom_key(f"starter_{arch}_{taom}_{slot}_a", folder)
    return ra.cap_value(ra.KINGDOM_CAPS[key], slot_type, "light")


class StarterValueTests(unittest.TestCase):
    def test_no_starter_value_passes_the_light_band_of_its_culture(self):
        for taom, folder, _body, _leg in gs.CULTURES:
            for arch in ARCHETYPES:
                key = ra.kingdom_key(f"starter_{arch}_{taom}_body_a", folder)
                tmpl = gs.TEMPLATES[arch]
                body = {k: int(v) for k, v in gs.armor_overrides("body", tmpl, key).items()}
                leg = {k: int(v) for k, v in gs.armor_overrides("leg", tmpl, key).items()}
                where = f"{taom}/{arch}"
                self.assertLessEqual(body["body_armor"], _light(folder, "body", taom, arch), where)
                self.assertLessEqual(body["body_armor"], tmpl["body"][0], where)         # never above the literal
                self.assertEqual(body["arm_armor"], 0, where)                             # no secondary below heavy
                self.assertLessEqual(leg["leg_armor"], _light(folder, "leg", taom, arch, "leg"), where)
                self.assertLessEqual(leg["leg_armor"], tmpl["leg"], where)

    def test_the_values_for_a_cap_37_culture_and_a_cap_52_culture(self):
        rohan = ra.kingdom_key("starter_infantry_vlandia_body_a", "rohan")
        erebor = ra.kingdom_key("starter_infantry_erebor_body_a", "erebor")
        t = gs.TEMPLATES
        # Rohan (cap 37): light chest 37 * 0.16 = 5.9 -> 6, light greaves 37 * 0.45 * 0.16 = 2.7 -> 3
        self.assertEqual(gs.armor_overrides("body", t["infantry"], rohan), {"body_armor": "6", "arm_armor": "0"})
        self.assertEqual(gs.armor_overrides("leg", t["infantry"], rohan), {"leg_armor": "3"})
        # the ranged literal chest is 5, under the light value 6, so it stays 5
        self.assertEqual(gs.armor_overrides("body", t["ranged"], rohan), {"body_armor": "5", "arm_armor": "0"})
        # Erebor (cap 52): chest 8.3 -> 8, greaves 3.7 -> 4; the literal 9 and 9 give way
        self.assertEqual(gs.armor_overrides("body", t["infantry"], erebor), {"body_armor": "8", "arm_armor": "0"})
        self.assertEqual(gs.armor_overrides("leg", t["infantry"], erebor), {"leg_armor": "4"})

    def test_a_secondary_follows_the_curve_rule_not_the_literal(self):
        """Raise the secondary band to light and the literal arm share (4 of 9) is kept as a share, capped."""
        old = ra.SECONDARY_FROM_BAND
        try:
            ra.SECONDARY_FROM_BAND = "light"
            key = ra.kingdom_key("starter_infantry_erebor_body_a", "erebor")
            # chest 8; arm share 4/9 is capped at 0.30 of the primary: 8 * 0.30 = 2.4 -> 2
            self.assertEqual(gs.armor_overrides("body", gs.TEMPLATES["infantry"], key)["arm_armor"], "2")
        finally:
            ra.SECONDARY_FROM_BAND = old


class GeneratedFileTests(unittest.TestCase):
    def test_the_generated_items_carry_the_light_band_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / "ModuleData" / "LOTRLOME_items" / "rohan"
            folder.mkdir(parents=True)
            (folder / "donors.xml").write_text(
                '<Items>'
                '<Item id="rohan_militia_tunic_a" name="t" Type="BodyArmor" weight="3"><ItemComponent>'
                '<Armor body_armor="20" arm_armor="8" material_type="Cloth" modifier_group="cloth" />'
                '</ItemComponent></Item>'
                '<Item id="cts_rohan_boots3" name="b" Type="LegArmor" weight="1"><ItemComponent>'
                '<Armor leg_armor="12" material_type="Leather" modifier_group="leather" />'
                '</ItemComponent></Item>'
                '</Items>', encoding="utf-8")
            content, warnings = gs.generate_culture("vlandia", "rohan", "rohan_militia_tunic_a",
                                                    "cts_rohan_boots3", tmp)
        self.assertEqual(warnings, [])
        items = {i.get("id"): i.find(".//Armor").attrib
                 for i in ET.fromstring(content.split("-->", 1)[1]).iter("Item")}
        self.assertEqual(len(items), 6)
        inf_body, inf_leg = items["starter_infantry_vlandia_body_a"], items["starter_infantry_vlandia_leg_a"]
        self.assertEqual((inf_body["body_armor"], inf_body["arm_armor"], inf_leg["leg_armor"]), ("6", "0", "3"))
        rng_body = items["starter_ranged_vlandia_body_a"]
        self.assertEqual((rng_body["body_armor"], rng_body["arm_armor"]), ("5", "0"))


class WriteGeneratedTests(unittest.TestCase):
    OLD = b'\xef\xbb\xbf<?xml version="1.0"?>\r\n<Items>\r\n</Items>\r\n'
    NEW = '<?xml version="1.0" encoding="utf-8"?>\n<Items>\n    <Item id="a" />\n</Items>\n'

    def test_a_replaced_file_keeps_its_bom_and_line_endings(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "starter_armors.xml"
            out.write_bytes(self.OLD)
            gs.write_generated(str(out), self.NEW)
            self.assertEqual(out.read_bytes(), b'\xef\xbb\xbf' + self.NEW.replace("\n", "\r\n").encode("utf-8"))
            self.assertEqual((Path(tmp) / "starter_armors.xml.bak-startergear").read_bytes(), self.OLD)

    def test_a_new_file_is_plain_lf_without_a_bom(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "starter_armors.xml"
            gs.write_generated(str(out), self.NEW)
            self.assertEqual(out.read_bytes(), self.NEW.encode("utf-8"))

    def test_content_that_does_not_parse_is_refused_before_anything_is_touched(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "starter_armors.xml"
            out.write_bytes(self.OLD)
            with self.assertRaises(ET.ParseError):
                gs.write_generated(str(out), "<Items><Item></Items>")
            self.assertEqual(out.read_bytes(), self.OLD)
            self.assertFalse((Path(tmp) / "starter_armors.xml.bak-startergear").exists())


if __name__ == "__main__":
    unittest.main()
