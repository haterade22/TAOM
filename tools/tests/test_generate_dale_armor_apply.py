#!/usr/bin/env python3
"""generate_dale_armor.apply() must append only missing items, never rewrite existing ones.

Until 2026-10-01 apply() rewrote all five Dale slot files from the manifest, wiping every
hand-tuned value (the #541 restat, the 2026-10-01 beard covers in
docs/reference/lotrlome-beard-cover-changes.md). The other armor generators skip ids that
already exist; these tests pin Dale to the same contract, byte-faithfully.

Run:  python -m unittest tools/tests/test_generate_dale_armor_apply.py
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_dale_armor as gen  # noqa: E402

HELM_A = "sk_dale_helmet_chivlary_a03"
HELM_B = "sk_dale_helmet_chivlary_a04"

HAND_TUNED = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    '<Items>\r\n'
    '\t<Item id="' + HELM_A + '" name="hand tuned" Type="HeadArmor">\r\n'
    '\t\t<ItemComponent>\r\n'
    '\t\t\t<Armor head_armor="99" beard_cover_type="type1" />\r\n'
    '\t\t</ItemComponent>\r\n'
    '\t</Item>\r\n'
    '</Items>\r\n'
)


def _head_slot(ids):
    per_slot, slim, _ = gen.categorize(ids)
    return {k: v for k, v in per_slot.items() if v}, slim


class DaleApplyAppendsOnly(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _head_file(self, per_slot):
        (slot_key,) = per_slot.keys()
        return self.dir / gen.SLOT_FILENAME[slot_key]

    def test_existing_item_bytes_are_untouched_and_missing_one_is_appended(self):
        per_slot, slim = _head_slot([HELM_A, HELM_B])
        path = self._head_file(per_slot)
        path.write_bytes(HAND_TUNED.encode("utf-8"))

        gen.apply(str(self.dir), per_slot, slim)

        out = path.read_bytes().decode("utf-8")
        hand_block = HAND_TUNED[HAND_TUNED.index("\t<Item"):HAND_TUNED.index("</Item>") + len("</Item>")]
        self.assertIn(hand_block, out)
        self.assertEqual(out.count(f'id="{HELM_A}"'), 1)
        self.assertEqual(out.count(f'id="{HELM_B}"'), 1)
        self.assertEqual(out.count("\n"), out.count("\r\n"), "CRLF file gained bare LFs")
        ids = [i.get("id") for i in ET.fromstring(out.encode("utf-8")).iter("Item")]
        self.assertEqual(ids, [HELM_A, HELM_B])

    def test_rerun_with_nothing_missing_writes_nothing(self):
        per_slot, slim = _head_slot([HELM_A])
        path = self._head_file(per_slot)
        path.write_bytes(HAND_TUNED.encode("utf-8"))

        gen.apply(str(self.dir), per_slot, slim)

        self.assertEqual(path.read_bytes(), HAND_TUNED.encode("utf-8"))

    def test_missing_file_is_created_and_parses(self):
        per_slot, slim = _head_slot([HELM_A])
        path = self._head_file(per_slot)

        gen.apply(str(self.dir), per_slot, slim)

        ids = [i.get("id") for i in ET.parse(path).getroot().iter("Item")]
        self.assertEqual(ids, [HELM_A])


if __name__ == "__main__":
    unittest.main()
