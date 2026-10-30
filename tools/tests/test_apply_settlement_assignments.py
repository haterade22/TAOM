#!/usr/bin/env python3
"""Unit tests for apply_settlement_assignments.py (2026-10-30).

The tool writes the owners and cultures pinned in tools/settlement_assignments.json back into the
LIVE TAOM_Map settlements.xml, which is unversioned. It must change only those attributes, keep the
file's bytes (BOM, CRLF) otherwise identical, carry the culture to bound villages, and refuse to
write when an id is missing or ambiguous.
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import apply_settlement_assignments as asa  # noqa: E402

MAP = (
    '﻿<?xml version="1.0" encoding="utf-8"?>\r\n<Settlements>\r\n'
    '  <Settlement id="castle_K4" name="{=x}Kruk" owner="Faction.clan_khuzait_1" posX="1" posY="2" '
    'culture="Culture.khuzait" gate_posX="1">\r\n'
    '    <Components>\r\n      <Town id="c" is_castle="true" prosperity="420" />\r\n    </Components>\r\n'
    '  </Settlement>\r\n'
    '  <Settlement id="castle_K40" owner="Faction.clan_khuzait_1" culture="Culture.khuzait">\r\n'
    '    <Components>\r\n      <Town id="d" is_castle="true" prosperity="420" />\r\n    </Components>\r\n'
    '  </Settlement>\r\n'
    '  <Settlement id="castle_village_K4_1"\r\n'
    '    name="{=y}Vil" posX="1" posY="2"\r\n    culture="Culture.khuzait">\r\n'
    '    <Components>\r\n      <Village id="v" hearth="465" bound="Settlement.castle_K4" />\r\n'
    '    </Components>\r\n  </Settlement>\r\n'
    '</Settlements>\r\n'
)
PINS = {"castle_K4": {"owner": "clan_battania_8", "culture": "battania", "reason": "Khand"}}


class ApplySettlementAssignmentsTests(unittest.TestCase):

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.map = Path(self._tmp.name) / "settlements.xml"
        self.map.write_bytes(MAP.encode("utf-8"))

    def tearDown(self):
        self._tmp.cleanup()

    def _text(self):
        return self.map.read_bytes().decode("utf-8")

    def test_plan_sets_owner_culture_and_bound_village_culture(self):
        new_text, changed = asa.plan(MAP, PINS)
        self.assertEqual(changed, ["castle_K4", "castle_village_K4_1"])
        self.assertIn('id="castle_K4" name="{=x}Kruk" owner="Faction.clan_battania_8"', new_text)
        self.assertIn('culture="Culture.battania" gate_posX="1"', new_text)
        self.assertIn('\r\n    culture="Culture.battania">', new_text)

    def test_plan_leaves_a_longer_id_with_the_same_prefix_alone(self):
        new_text, _ = asa.plan(MAP, PINS)
        self.assertIn('id="castle_K40" owner="Faction.clan_khuzait_1" culture="Culture.khuzait"',
                      new_text)

    def test_plan_changes_nothing_else_in_the_bytes(self):
        new_text, _ = asa.plan(MAP, PINS)
        expected = (MAP.replace('owner="Faction.clan_khuzait_1" posX', 'owner="Faction.clan_battania_8" posX')
                       .replace('culture="Culture.khuzait" gate_posX', 'culture="Culture.battania" gate_posX')
                       .replace('    culture="Culture.khuzait">', '    culture="Culture.battania">'))
        self.assertEqual(new_text, expected)

    def test_plan_is_idempotent(self):
        once, _ = asa.plan(MAP, PINS)
        twice, changed = asa.plan(once, PINS)
        self.assertEqual(twice, once)
        self.assertEqual(changed, [])

    def test_plan_refuses_an_id_the_map_does_not_have(self):
        with self.assertRaises(asa.AssignmentError):
            asa.plan(MAP, {"castle_nowhere": PINS["castle_K4"]})

    def test_plan_refuses_a_village_id(self):
        with self.assertRaises(asa.AssignmentError):
            asa.plan(MAP, {"castle_village_K4_1": PINS["castle_K4"]})

    def test_main_dry_run_does_not_write(self):
        spec = Path(self._tmp.name) / "spec.json"
        spec.write_text(json.dumps({"settlements": PINS}), encoding="utf-8")
        rc = asa.main(["--settlements", str(self.map), "--spec", str(spec)])
        self.assertEqual(rc, 0)
        self.assertEqual(self._text(), MAP)

    def test_main_apply_writes_and_backs_up_without_an_xml_extension(self):
        spec = Path(self._tmp.name) / "spec.json"
        spec.write_text(json.dumps({"settlements": PINS}), encoding="utf-8")
        rc = asa.main(["--settlements", str(self.map), "--spec", str(spec), "--apply"])
        self.assertEqual(rc, 0)
        self.assertIn('owner="Faction.clan_battania_8"', self._text())
        self.assertTrue(self._text().startswith("﻿"), "BOM lost")
        backups = [p for p in Path(self._tmp.name).iterdir() if ".bak" in p.name]
        self.assertEqual(len(backups), 1)
        self.assertFalse(backups[0].name.endswith(".xml"), backups[0].name)
        self.assertEqual(backups[0].read_bytes(), MAP.encode("utf-8"))

    def test_main_missing_map_fails(self):
        spec = Path(self._tmp.name) / "spec.json"
        spec.write_text(json.dumps({"settlements": PINS}), encoding="utf-8")
        rc = asa.main(["--settlements", str(self.map) + ".gone", "--spec", str(spec)])
        self.assertEqual(rc, 1)


if __name__ == "__main__":
    unittest.main()
