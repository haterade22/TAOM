#!/usr/bin/env python3
"""Unit tests for the schema-driven ModuleData validator (tools/taom_schema.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_validate_moduledata.py

These tests build a SYNTHETIC ModuleData tree + synthetic registries (no game
install needed), so they encode the validator's contract independently of the
real LOTRLOME_Armory / vanilla data. They also load the REAL shipped schemas
under tools/schemas/ so a malformed schema file fails the suite.

Each test maps to one recurring TAOM bug class the validator must catch:
  - BROKEN_ITEM_REF        -> "underwear bug" (missing equipment item)
  - BROKEN_TROOP_REF       -> upgrade_target / culture pointing at a deleted troop
  - UNKNOWN_CULTURE        -> stale culture rename / "wrote rohan instead of vlandia"
  - DUPLICATE_NPC_ID       -> same NPCCharacter id defined twice in TAOM
  - MISSING_CIVILIAN_TYPE  -> civilian roster missing equipmentType="Civilian"
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import taom_schema as ts  # noqa: E402

SCHEMA_DIR = Path(__file__).resolve().parent.parent / "schemas"


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class ValidatorContractTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        # Registries the validator resolves refs against.
        self.registries = ts.Registries(
            items={"good_sword", "good_helm", "None"},
            item_def_files={},
            npccharacters={"hero_a", "hero_b"},
            cultures={"gondor", "vlandia", "neutral_culture"},
            party_templates={"good_template"},
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self):
        return ts.Validator(self.md, self.schemas, self.registries).run()

    def _codes(self, issues):
        return [i.code for i in issues]

    def test_clean_tree_has_no_errors(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.hero_b" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.good_sword" />
        <equipment slot="Head" id="Item.good_helm" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
  <NPCCharacter id="hero_b" default_group="Cavalry" culture="Culture.gondor" />
</NPCCharacters>
""")
        errors = [i for i in self._run() if i.severity is ts.Severity.ERROR]
        self.assertEqual(errors, [], f"clean tree should have no ERRORs, got: {errors}")

    def test_broken_item_ref_is_error(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Body" id="Item.does_not_exist_armor" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("BROKEN_ITEM_REF", self._codes(issues))
        bad = [i for i in issues if i.code == "BROKEN_ITEM_REF"][0]
        self.assertIn("does_not_exist_armor", bad.message)
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertGreater(bad.line, 0)

    def test_item_none_is_allowed(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <Equipments><EquipmentRoster>
      <equipment slot="Item1" id="Item.None" />
    </EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertNotIn("BROKEN_ITEM_REF", self._codes(self._run()))

    def test_dead_troop_ref_is_error(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.deleted_troop" />
    </upgrade_targets>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("BROKEN_TROOP_REF", self._codes(issues))
        self.assertIn("deleted_troop", [i.message for i in issues if i.code == "BROKEN_TROOP_REF"][0])

    def test_unknown_culture_is_error(self):
        # "rohan" is the classic mistake — the XSLT culture StringId is "vlandia".
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.rohan" />
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("UNKNOWN_CULTURE", self._codes(issues))

    def test_duplicate_npc_id_is_error(self):
        _write(self.md / "troops" / "troops_a.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor" />
</NPCCharacters>
""")
        _write(self.md / "characters" / "npcs_dup.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Ranged" culture="Culture.gondor" />
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("DUPLICATE_NPC_ID", self._codes(issues))

    def test_invalid_default_group_enum_is_flagged(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="NotARealGroup" culture="Culture.gondor" />
</NPCCharacters>
""")
        self.assertIn("INVALID_ENUM", self._codes(self._run()))

    def test_missing_civilian_equipment_type_is_flagged(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template_a" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Body" id="Item.good_helm" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_present_civilian_equipment_type_is_clean(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template_a" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian">
      <Equipment slot="Body" id="Item.good_helm" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_duplicate_item_definition_across_folders_is_flagged(self):
        # Simulates the multi-folder Armory duplicate-id bug via registry metadata.
        regs = ts.Registries(
            items={"shared_helm"},
            item_def_files={"shared_helm": ["erebor/head_armors.xml", "iron_hills/head_armors.xml"]},
            npccharacters=set(),
            cultures={"gondor"}, party_templates=set(),
        )
        _write(self.md / "troops" / "troops_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters></NPCCharacters>\n')
        issues = ts.Validator(self.md, self.schemas, regs).run()
        self.assertIn("DUPLICATE_ITEM_DEF", [i.code for i in issues])

    def test_troop_ref_via_troop_attribute_is_checked(self):
        # HIGH fix (deep-review 2026-05-30): party-template stacks reference troops
        # via troop="NPCCharacter.X", NOT id=. The attribute-agnostic ref pattern
        # must catch a dead troop ref here, not just in upgrade_target id=.
        _write(self.md / "taom_partyTemplates.xml", """<?xml version="1.0" encoding="utf-8"?>
<partyTemplates>
  <MBPartyTemplate id="some_template">
    <stacks>
      <PartyTemplateStack troop="NPCCharacter.deleted_troop" />
    </stacks>
  </MBPartyTemplate>
</partyTemplates>
""")
        issues = self._run()
        self.assertIn("BROKEN_TROOP_REF", self._codes(issues))
        self.assertIn("deleted_troop", [i.message for i in issues if i.code == "BROKEN_TROOP_REF"][0])

    def test_broken_party_template_ref_is_warning(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" villager_party_template="PartyTemplate.does_not_exist" />
</SPCultures>
""")
        issues = self._run()
        self.assertIn("BROKEN_PARTY_TEMPLATE_REF", self._codes(issues))
        bad = [i for i in issues if i.code == "BROKEN_PARTY_TEMPLATE_REF"][0]
        self.assertEqual(bad.severity, ts.Severity.WARNING)

    def test_valid_party_template_ref_is_clean(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" villager_party_template="PartyTemplate.good_template" />
</SPCultures>
""")
        self.assertNotIn("BROKEN_PARTY_TEMPLATE_REF", self._codes(self._run()))

    def test_duplicate_culture_id_is_flagged(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" />
  <Culture id="gondor" />
</SPCultures>
""")
        self.assertIn("DUPLICATE_CULTURE_ID", self._codes(self._run()))

    def test_duplicate_roster_id_is_flagged(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_a.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="dup_roster" culture="Culture.gondor"><EquipmentSet /></EquipmentRoster>
</EquipmentRosters>
""")
        _write(self.md / "equipmentsets" / "taom_lord_template_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="dup_roster" culture="Culture.gondor"><EquipmentSet /></EquipmentRoster>
</EquipmentRosters>
""")
        # taom_lord_template_equipment.xml must be covered by the broadened
        # equipmentsets/*.xml glob (HIGH fix, deep-review 2026-05-30).
        self.assertIn("DUPLICATE_ROSTER_ID", self._codes(self._run()))

    def test_no_schema_file_is_still_ref_swept(self):
        # A file matching no schema still gets the global cross-reference sweep.
        _write(self.md / "special_resources" / "config.xml", """<?xml version="1.0" encoding="utf-8"?>
<Config><cost id="Item.ghost_item" /></Config>
""")
        self.assertIn("BROKEN_ITEM_REF", self._codes(self._run()))

    def test_malformed_xml_does_not_crash(self):
        _write(self.md / "troops" / "troops_bad.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n  <NPCCharacter id="x" culture="Culture.gondor"\n')
        # Engine is regex-based and tolerant; run() must not raise.
        issues = self._run()
        self.assertIsInstance(issues, list)

    def test_unknown_special_rule_rejected_at_load(self):
        with self.assertRaises(ValueError):
            ts.Schema.from_json({
                "name": "bad", "applies_to": ["x.xml"], "entry_element": "X",
                "special_rules": ["not_a_real_rule"],
            })

    def test_missing_required_schema_field_rejected(self):
        with self.assertRaises(ValueError):
            ts.Schema.from_json({"name": "bad", "applies_to": ["x.xml"]})  # no entry_element

    # --- Codex review 2026-05-30 regression tests ---

    def test_child_template_roster_requires_civilian_type(self):
        # child_template_* rosters are civilian even without "_civ" in the id.
        _write(self.md / "equipmentsets" / "taom_child_equipment_templates.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="child_template_gondor_noble_male" culture="Culture.gondor">
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_education_template_roster_not_flagged(self):
        # child_education_* templates are 0/784 Civilian-tagged in real data (unknown
        # convention) — must NOT be flagged (would be mass false positives).
        _write(self.md / "equipmentsets" / "taom_education_equipment_templates.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="child_education_equipments_stage_1_gondor" culture="Culture.gondor">
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    # --- #354 (age-8 education CTD) regression tests ---

    def test_main_culture_missing_education_templates_is_error(self):
        # lothlorien shipped as is_main_culture with zero stage_2 education
        # templates -> guaranteed CTD at any child's age-8 education (#354).
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = self._run()
        self.assertIn("MISSING_EDUCATION_TEMPLATES", self._codes(issues))
        bad = [i for i in issues if i.code == "MISSING_EDUCATION_TEMPLATES"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertEqual(bad.entry_id, "gondor")
        self.assertIn("0, 1, 2, 3, 4, 5", bad.message)

    def test_main_culture_with_all_education_templates_is_clean(self):
        self.registries.npccharacters |= {
            f"child_education_templates_stage_2_page_0_branch_{b}_gondor" for b in range(6)
        }
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    def test_partial_education_templates_reports_missing_branches_only(self):
        self.registries.npccharacters |= {
            f"child_education_templates_stage_2_page_0_branch_{b}_gondor" for b in range(5)
        }  # branch 5 missing
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = [i for i in self._run() if i.code == "MISSING_EDUCATION_TEMPLATES"]
        self.assertEqual(len(issues), 1)
        self.assertIn("branch(es) 5 ", issues[0].message)

    def test_non_main_culture_needs_no_education_templates(self):
        # Bandit/minor cultures never raise children through the education flow.
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor_soldiers" />
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    def test_education_check_skipped_without_npc_registry(self):
        # Degraded mode (no game install) empties the troop registry; the
        # education check must skip rather than mass-false-positive.
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures={"gondor"}, party_templates=set())
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = ts.Validator(self.md, self.schemas, regs).run()
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", [i.code for i in issues])

    def test_commented_out_main_culture_not_checked(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" />
  <!-- <Culture id="ghost" is_main_culture="true" /> -->
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    # --- harness family_type regression tests (Riding Caparison, 2026-07-29) ---

    def test_harness_missing_family_type_is_error(self):
        # starter_cavalry_gondor_horse_armor_a shipped with no <Armor family_type>,
        # so ArmorComponent.FamilyType defaulted to 0 (human) and the inventory
        # screen silently refused it on every mount (SPInventoryVM.cs:4112).
        self.registries.harness_family_types = {
            "bad_barding": (None, "LOTRLOME_items/LOTRAOM_horses.xml"),
        }
        issues = self._run()
        self.assertIn("MISSING_HARNESS_FAMILY_TYPE", self._codes(issues))
        bad = [i for i in issues if i.code == "MISSING_HARNESS_FAMILY_TYPE"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertEqual(bad.entry_id, "bad_barding")

    def test_harness_with_family_type_is_clean(self):
        self.registries.harness_family_types = {
            "good_barding": (1, "LOTRLOME_items/LOTRAOM_horses.xml"),
        }
        self.assertNotIn("MISSING_HARNESS_FAMILY_TYPE", self._codes(self._run()))

    def test_equipment_set_harness_family_mismatch_is_error(self):
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"elephant_barding": (10, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_career_starting_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="career_cavalry_gondor" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.elephant_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        issues = self._run()
        self.assertIn("HARNESS_FAMILY_MISMATCH", self._codes(issues))
        bad = [i for i in issues if i.code == "HARNESS_FAMILY_MISMATCH"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertIn("elephant_barding", bad.message)
        self.assertIn("saddle_horse", bad.message)
        self.assertGreater(bad.line, 0)

    def test_equipment_set_matching_family_types_is_clean(self):
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"light_harness": (1, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_career_starting_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="career_cavalry_gondor" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_troop_equipment_roster_mismatch_is_flagged(self):
        # Troop rosters use lowercase <equipment slot="..."> with no <EquipmentSet>.
        self.registries.mount_family_types = {"warg_brown": 1}
        self.registries.harness_family_types = {"elephant_barding": (10, "x.xml")}
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Cavalry" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Horse" id="Item.warg_brown" />
        <equipment slot="HorseHarness" id="Item.elephant_barding" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_harnesses_in_separate_equipment_sets_are_not_cross_paired(self):
        # Two sets in one roster: each pairing is internally consistent, so the
        # roster-level scan must not pair set 1's mount with set 2's harness.
        self.registries.mount_family_types = {"saddle_horse": 1, "taom_war_elephant": 10}
        self.registries.harness_family_types = {
            "light_harness": (1, "x.xml"), "elephant_barding": (10, "x.xml"),
        }
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.taom_war_elephant" />
      <Equipment slot="HorseHarness" id="Item.elephant_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_missing_family_type_harness_reported_once_not_as_mismatch(self):
        # A harness with no family_type is reported at its definition site only;
        # every equipment set using it must NOT also raise a mismatch.
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"bad_barding": (None, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.bad_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        codes = self._codes(self._run())
        self.assertIn("MISSING_HARNESS_FAMILY_TYPE", codes)
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", codes)

    def test_harness_checks_skipped_without_harness_registry(self):
        # Degraded mode (no game install): both dicts are empty -> skip, no crash.
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures={"gondor"}, party_templates=set())
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.bad_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        codes = [i.code for i in ts.Validator(self.md, self.schemas, regs).run()]
        self.assertNotIn("MISSING_HARNESS_FAMILY_TYPE", codes)
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", codes)

    def test_civilian_rule_checks_all_equipment_sets(self):
        # A civilian roster with a second untagged EquipmentSet must be flagged
        # even when the first set is correctly tagged.
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian"><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))


    # ----------------------------------------------------------------- #
    # BodyProperty refs + the foreign-module sweep. Both gaps let the three
    # "Null object reference found with ID" entries in the 2026-08-02
    # dwarf-vs-Rhun crash log ship: BodyProperty.* was cross-checked by
    # nothing, and the sweep walked only TAOM's ModuleData while 28 of the 33
    # bad refs sat on LOTRLOME_Armory item files. The engine unregisters such
    # refs, so every consumer downstream reads null.
    # ----------------------------------------------------------------- #
    def test_broken_body_property_ref_is_reported(self):
        self.registries.body_properties = {"fighter_haradrim"}
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_umbar" />
    </face>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = [i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"]
        self.assertEqual(len(issues), 1, f"expected exactly one, got {self._codes(self._run())}")
        self.assertIn("fighter_umbar", issues[0].message)
        self.assertIs(issues[0].severity, ts.Severity.ERROR)

    def test_resolving_body_property_ref_is_clean(self):
        self.registries.body_properties = {"fighter_haradrim"}
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_haradrim" />
    </face>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"], [])

    def test_body_property_check_skipped_when_registry_unavailable(self):
        # Same empty-registry guard every other kind uses: without the game
        # install the registry is incomplete, and every ref would false-positive.
        self.registries.body_properties = set()
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face><face_key_template value="BodyProperty.anything" /></face>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"], [])

    def test_extra_ref_roots_are_swept_and_reported_with_module_prefix(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "rhun" / "head_armors.xml",
               '<?xml version="1.0"?>\n<Items>\n  <Item id="hat" culture="Culture.rhun" />\n</Items>\n')
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        broken = [i for i in issues if i.code == "UNKNOWN_CULTURE"]
        self.assertEqual(len(broken), 1)
        # The report must name the module -- "LOTRLOME_items/rhun/head_armors.xml"
        # alone reads as a TAOM path and sends the reader to the wrong repo.
        self.assertTrue(broken[0].file.startswith("LOTRLOME_Armory/"), broken[0].file)
        self.assertIn("rhun", broken[0].message)

    def test_extra_ref_roots_clean_when_refs_resolve(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "rhun" / "head_armors.xml",
               '<?xml version="1.0"?>\n<Items>\n  <Item id="hat" culture="Culture.gondor" />\n</Items>\n')
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        self.assertEqual([i for i in issues if i.code == "UNKNOWN_CULTURE"], [])

    def test_missing_extra_ref_root_is_recorded_not_silently_dropped(self):
        # THE failure this whole sweep exists to prevent. A renamed/missing Armory
        # folder must not make the tool print a clean PASS that is indistinguishable
        # from a real one -- that is exactly the under-coverage state that hid 28 of
        # the 33 dangling refs the engine reported on 2026-08-02.
        gone = Path(self._tmp.name) / "NoSuchModule" / "ModuleData"
        v = ts.Validator(self.md, self.schemas, self.registries, extra_ref_roots=[gone])
        v.run()
        self.assertEqual([Path(p) for p in v.missing_ref_roots], [gone])

    def test_present_extra_ref_root_is_not_reported_missing(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "x.xml", '<Items>\n  <Item id="a" culture="Culture.gondor" />\n</Items>\n')
        v = ts.Validator(self.md, self.schemas, self.registries, extra_ref_roots=[armory])
        v.run()
        self.assertEqual(v.missing_ref_roots, [])

    def test_extra_ref_roots_do_not_run_taom_only_schema_checks(self):
        # Duplicate-id / civilian-type / enum rules are TAOM schema contracts.
        # Applying them to a foreign module would report defects against a repo
        # this validator does not own.
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "troops" / "troops_foreign.xml", """<?xml version="1.0"?>
<NPCCharacters>
  <NPCCharacter id="dupe" default_group="NotAGroup" culture="Culture.gondor" />
  <NPCCharacter id="dupe" default_group="NotAGroup" culture="Culture.gondor" />
</NPCCharacters>
""")
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        self.assertEqual([i for i in issues if i.code in ("DUPLICATE_NPC_ID", "INVALID_ENUM")], [])


class BuildRegistriesTests(unittest.TestCase):
    """Codex review 2026-05-30: culture registry must come ONLY from authoritative
    culture-definition files, and XML comments must never pollute any registry."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def test_culture_registry_excludes_config_file_culture_ids(self):
        _write(self.md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
        # A feature config file that reuses <Culture id="..."> for kingdom/eligibility ids.
        _write(self.md / "career_system" / "taom_careers.xml",
               '<?xml version="1.0"?>\n<Careers>\n  <Culture id="empire_w" />\n  <Culture id="empire_s" />\n</Careers>\n')
        regs = ts.build_registries(self.md, None)  # None = no game install
        self.assertIn("gondor", regs.cultures)
        self.assertNotIn("empire_w", regs.cultures, "kingdom id from a config file must not become a valid culture")
        self.assertNotIn("empire_s", regs.cultures)

    # --- harness / mount family-type registries (Riding Caparison, 2026-07-29) ---

    def _write_harness_fixture(self, modules: Path) -> None:
        _write(modules / "Native" / "ModuleData" / "monsters.xml", """<?xml version="1.0"?>
<Monsters>
  <Monster id="horse" family_type="1" />
  <Monster id="elephant_base" family_type="10" />
  <Monster id="taom_war_elephant" base_monster="Monster.elephant_base" />
  <Monster id="ambiguous_beast" family_type="1" />
</Monsters>
""")
        _write(modules / "ADOD_Beasts" / "ModuleData" / "adod_beasts.xml", """<?xml version="1.0"?>
<Monsters>
  <Monster id="ambiguous_beast" family_type="7" />
</Monsters>
""")
        _write(modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "horses.xml", """<?xml version="1.0"?>
<Items>
  <Item id="good_barding" Type="HorseHarness">
    <ItemComponent><Armor body_armor="10" family_type="1" material_type="Leather" /></ItemComponent>
  </Item>
  <Item id="bad_barding" Type="HorseHarness">
    <ItemComponent><Armor body_armor="10" material_type="Leather" /></ItemComponent>
  </Item>
  <Item id="saddle_horse" Type="Horse">
    <ItemComponent><Horse speed="45" monster="Monster.horse" /></ItemComponent>
  </Item>
  <Item id="taom_war_elephant" Type="Horse">
    <ItemComponent><Horse speed="30" monster="Monster.taom_war_elephant" /></ItemComponent>
  </Item>
  <Item id="odd_beast" Type="Horse">
    <ItemComponent><Horse speed="30" monster="Monster.ambiguous_beast" /></ItemComponent>
  </Item>
</Items>
""")

    def test_harness_family_types_registered_with_missing_as_none(self):
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(regs.harness_family_types["good_barding"][0], 1)
        self.assertIsNone(regs.harness_family_types["bad_barding"][0],
                          "a harness with no family_type must register as None, not be omitted")
        self.assertIn("horses.xml", regs.harness_family_types["bad_barding"][1])
        self.assertNotIn("saddle_horse", regs.harness_family_types, "mounts are not harnesses")

    def test_mount_family_types_resolved_through_monster_and_base_monster(self):
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(regs.mount_family_types["saddle_horse"], 1)
        # HorseComponent never reads family_type off <Horse>; the monsters XML is
        # the only mount-side authority, and base_monster must be followed.
        self.assertEqual(regs.mount_family_types["taom_war_elephant"], 10)

    def test_conflicting_monster_family_types_resolve_to_none(self):
        # ADOD_Beasts redeclares ids that Native/LOTRLOME also define; engine
        # resolution is load-order dependent, so treat it as unknown rather than
        # guessing and emitting a false mismatch.
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertIsNone(regs.mount_family_types["odd_beast"])

    def test_harness_registries_empty_without_game_install(self):
        regs = ts.build_registries(self.md, None)
        self.assertEqual(regs.harness_family_types, {})
        self.assertEqual(regs.mount_family_types, {})

    def test_shrunken_body_property_registry_warns(self):
        # A renamed vanilla *_bodyproperties.xml silently shrinks the registry. Full
        # shrinkage would trip the empty-registry guard and skip the check entirely
        # (silent PASS); partial shrinkage floods false positives. Neither is
        # distinguishable from a healthy run without a floor.
        modules = Path(self._tmp.name) / "Modules"
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="only_one" />\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertTrue(regs.suspect_registries,
                        "a 1-entry body_properties registry with a game install must be flagged")
        self.assertIn("body_properties", " ".join(regs.suspect_registries))

    def test_healthy_registry_is_not_flagged_suspect(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBoxCore" / "ModuleData" / "sandboxcore_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n'
               + "".join(f'  <BodyProperty id="v{i}" />\n' for i in range(120))
               + '</BodyProperties>\n')
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="fighter_haradrim" />\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertEqual([s for s in regs.suspect_registries if "body_properties" in s], [])

    def test_no_game_install_does_not_flag_registries_suspect(self):
        # Empty-by-design, already reported by the CLI. Flagging it here would cry
        # wolf on every run without the game install.
        regs = ts.build_registries(self.md, None)
        self.assertEqual(regs.suspect_registries, [])

    def test_body_properties_registry_built_from_bodyproperties_files(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBoxCore" / "ModuleData" / "sandboxcore_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="fighter_empire" />\n</BodyProperties>\n')
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty\n\t\tid="fighter_haradrim">\n  </BodyProperty>\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        # TAOM authors the id on its OWN line after a newline+tabs; an id-on-the-
        # same-line regex silently registers nothing and every lord goes broken.
        self.assertIn("fighter_haradrim", regs.body_properties)
        self.assertIn("fighter_empire", regs.body_properties)
        self.assertNotIn("fighter_umbar", regs.body_properties)

    def test_commented_definition_not_registered(self):
        _write(self.md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n  <!-- <Culture id="ghost_example" /> -->\n</SPCultures>\n')
        regs = ts.build_registries(self.md, None)
        self.assertIn("gondor", regs.cultures)
        self.assertNotIn("ghost_example", regs.cultures, "a commented-out def must not enter the registry")


class LandlessCultureTests(unittest.TestCase):
    """LANDLESS_CULTURE (crash 099f650c, 2026-08-04). Vanilla SpawnLordParty ends with an unguarded
    Settlement.All.First(x => x.Culture == hero.Culture), so a Lord whose culture owns no settlement
    CTDs the daily clan tick as soon as his faction has no InitialHomeSettlement. TAOM_Map deletes
    every vanilla settlement, so 'every culture owns land' does not hold here."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None"},
            item_def_files={},
            npccharacters=set(),
            cultures={"gondor", "battania", "looters", "darshi"},
            party_templates=set(),
            settled_cultures={"gondor"},   # battania/looters/darshi own nothing
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _landless(self, registries=None):
        issues = ts.Validator(self.md, self.schemas, registries or self.registries).run()
        return [i for i in issues if i.code == "LANDLESS_CULTURE"]

    def test_lord_in_landless_culture_is_reported(self):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_5_1" occupation="Lord" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        found = self._landless()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_5_1")
        self.assertIn("battania", found[0].message)

    def test_lord_in_landed_culture_is_clean(self):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_1_1" occupation="Lord" culture="Culture.gondor" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._landless(), [])

    def test_clan_and_kingdom_in_landless_culture_are_reported(self):
        _write(self.md / "characters" / "clans.xml",
               '<?xml version="1.0"?>\n<Factions>\n'
               '  <Faction id="clan_battania_1" culture="Culture.battania" />\n'
               '</Factions>\n')
        _write(self.md / "taom_spkingdoms.xml",
               '<?xml version="1.0"?>\n<Kingdoms>\n'
               '  <Kingdom id="battania" culture="Culture.battania" />\n'
               '</Kingdoms>\n')
        self.assertEqual(
            sorted(i.entry_id for i in self._landless()), ["battania", "clan_battania_1"])

    def test_non_lord_occupation_is_ignored(self):
        # Bandit heroes never reach the throwing line: GetBestAvailableCommander filters on
        # Occupation.Lord.
        _write(self.md / "characters" / "bandits.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="looter_boss" occupation="Bandit" culture="Culture.looters" />\n'
               '  <NPCCharacter id="a_wanderer" occupation="Wanderer" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._landless(), [])

    def test_allowlisted_cultures_are_not_reported(self):
        # darshi/looters are landless by design and listed in _LANDLESS_BY_DESIGN with reasons.
        _write(self.md / "characters" / "minor.xml",
               '<?xml version="1.0"?>\n<Factions>\n'
               '  <Faction id="ghilman" culture="Culture.darshi" />\n'
               '  <Faction id="looters" culture="Culture.looters" />\n'
               '</Factions>\n')
        self.assertEqual(self._landless(), [])

    def test_check_skipped_when_settlement_registry_unavailable(self):
        # Degraded mode (no game install): an empty settled_cultures must SKIP the check, not
        # report every culture in TAOM as landless.
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_5_1" occupation="Lord" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        degraded = ts.Registries(
            items={"None"}, item_def_files={}, npccharacters=set(),
            cultures={"battania"}, party_templates=set(), settled_cultures=set())
        self.assertEqual(self._landless(degraded), [])


class SettledCultureRegistryTests(unittest.TestCase):
    """build_settled_cultures must model the world the GAME builds, not the union of every module's
    settlements.xml. TAOM_Map ships `<xsl:template match="Settlement"/>`, which deletes all 494
    vanilla settlements; a registry that ignored the strip would report every vanilla culture as
    landed and print PASS while the game crashed."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"

    def tearDown(self):
        self._tmp.cleanup()

    def _write_module(self, name, cultures, strip=False):
        md = self.modules / name / "ModuleData"
        _write(md / "settlements.xml",
               '<?xml version="1.0"?>\n<Settlements>\n' + "".join(
                   f'  <Settlement id="s_{name}_{i}" culture="Culture.{c}" />\n'
                   for i, c in enumerate(cultures)) + "</Settlements>\n")
        if strip:
            _write(md / "settlements.xslt",
                   '<?xml version="1.0"?>\n'
                   '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
                   '  <xsl:template match="Settlement"/>\n'
                   '</xsl:stylesheet>\n')

    def test_unconditional_strip_discards_earlier_modules(self):
        self._write_module("SandBox", ["battania", "khuzait", "sturgia"])
        self._write_module("TAOM_Map", ["gondor", "mordor"], strip=True)
        settled = ts.build_settled_cultures(self.modules)
        self.assertEqual(settled, {"gondor", "mordor"})
        self.assertNotIn("battania", settled,
                         "vanilla settlements are deleted by TAOM_Map's strip - counting them is "
                         "exactly the bug this registry exists to avoid")

    def test_without_a_strip_modules_merge(self):
        self._write_module("SandBox", ["battania"])
        self._write_module("TAOM_Map", ["gondor"])
        self.assertEqual(ts.build_settled_cultures(self.modules), {"battania", "gondor"})

    def test_no_game_install_yields_empty_registry(self):
        self.assertEqual(ts.build_settled_cultures(None), set())

    def test_registry_is_wired_into_build_registries_and_floored(self):
        md = Path(self._tmp.name) / "ModuleData"
        _write(md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
        self._write_module("TAOM_Map", ["gondor", "mordor"], strip=True)
        regs = ts.build_registries(md, self.modules)
        self.assertEqual(regs.settled_cultures, {"gondor", "mordor"})
        # Two entries is far below the floor: a broken source path must say so out loud rather
        # than skipping the check and printing a clean PASS.
        self.assertTrue(any("settled_cultures" in s for s in regs.suspect_registries))


class MountedDwarfTests(unittest.TestCase):
    """MOUNTED_DWARF. Dwarves use a custom, shorter skeleton whose rider bone is misaligned,
    so a mounted dwarf spawns INSIDE the horse mesh. TAOM already strips the mount for dwarf
    tournament participants (Patch46_TournamentDwarfDismount) — this is the data-layer half of
    the same invariant: no dwarf is ever authored as cavalry, and none is handed a mount.

    Both halves are needed. `default_group` and the Horse slot are independent knobs, so a lord
    left as Infantry but given a horse in his roster still spawns mounted."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None", "good_sword", "saddle_horse"},
            item_def_files={},
            npccharacters=set(),
            cultures={"erebor", "vlandia"},
            party_templates=set(),
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _mounted(self):
        issues = ts.Validator(self.md, self.schemas, self.registries).run()
        return [i for i in issues if i.code == "MOUNTED_DWARF"]

    def _write_lord(self, group="Infantry", race="dwarf", body=""):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="lord_E1_1" race="{race}" occupation="Lord" '
               f'culture="Culture.erebor" default_group="{group}">\n{body}'
               '  </NPCCharacter>\n</NPCCharacters>\n')

    # -- rule A: default_group ------------------------------------------- #
    def test_dwarf_cavalry_is_reported(self):
        self._write_lord(group="Cavalry")
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_E1_1")
        self.assertEqual(found[0].severity, ts.Severity.ERROR)
        self.assertIn("Cavalry", found[0].message)

    def test_dwarf_horse_archer_is_reported(self):
        self._write_lord(group="HorseArcher")
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("HorseArcher", found[0].message)

    def test_dwarf_infantry_is_clean(self):
        self._write_lord(group="Infantry")
        self.assertEqual(self._mounted(), [])

    def test_dwarf_ranged_is_clean(self):
        self._write_lord(group="Ranged")
        self.assertEqual(self._mounted(), [])

    # -- rule B: a mount in reachable battle equipment --------------------- #
    def test_dwarf_with_inline_horse_roster_is_reported(self):
        # Troop shape: equipment sits directly inside the NPCCharacter.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <Equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("saddle_horse", found[0].message)

    def test_dwarf_referencing_a_mounted_equipmentset_is_reported(self):
        # Lord shape: the NPCCharacter names a standalone roster defined elsewhere.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentSet id="erebor_bat_a" />\n'
            '    </Equipments>\n'))
        _write(self.md / "equipmentsets" / "taom_equipment_sets_erebor.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="erebor_bat_a" culture="Culture.erebor">\n'
               '    <EquipmentSet>\n'
               '      <Equipment slot="Horse" id="Item.saddle_horse" />\n'
               '    </EquipmentSet>\n'
               '  </EquipmentRoster>\n</EquipmentRosters>\n')
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_E1_1")
        self.assertIn("erebor_bat_a", found[0].message)

    def test_dwarf_referencing_a_footed_equipmentset_is_clean(self):
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentSet id="erebor_bat_a" />\n'
            '    </Equipments>\n'))
        _write(self.md / "equipmentsets" / "taom_equipment_sets_erebor.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="erebor_bat_a" culture="Culture.erebor">\n'
               '    <EquipmentSet>\n'
               '      <Equipment slot="Item0" id="Item.good_sword" />\n'
               '    </EquipmentSet>\n'
               '  </EquipmentRoster>\n</EquipmentRosters>\n')
        self.assertEqual(self._mounted(), [])

    # -- positive controls: the check must be dwarf-scoped ----------------- #
    def test_non_dwarf_cavalry_with_a_horse_is_clean(self):
        # Guards against a matcher that fires on everything. A broken scan that
        # silently matches nothing is caught by the reported-cases above; this
        # catches the opposite failure.
        self._write_lord(group="Cavalry", race="human", body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <Equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(self._mounted(), [])

    def test_race_attribute_absent_defaults_to_human_and_is_clean(self):
        # Omitting race= means human (the engine default), never dwarf.
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_1_1" occupation="Lord" '
               'culture="Culture.vlandia" default_group="Cavalry" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._mounted(), [])

    def test_lowercase_equipment_tag_is_still_caught(self):
        # TAOM ships both <Equipment> and <equipment> spellings. A case-sensitive
        # matcher reads one of them as "no horses at all" — the exact false-clean
        # this check exists to prevent.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(len(self._mounted()), 1)

    def test_commented_out_horse_is_ignored(self):
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <!-- <Equipment slot="Horse" id="Item.saddle_horse" /> -->\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(self._mounted(), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
