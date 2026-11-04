#!/usr/bin/env python3
"""Unit tests for the Armory reference audit (tools/audit_armory_refs.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_audit_armory_refs.py

Pure stdlib, synthetic fixtures, no game install and no real .tpac: every
engine function takes its inputs directly, mirroring test_validate_mesh_refs.py
and test_audit_deleted_mesh_impact.py.

Each test maps to one part of the contract (#599):
  - a catalogue DELETE / RENAME is "referenced" by what the LIVE XML names today,
    never by the flag the committed catalogue carried (the flag that kept saying
    "18 will break" after the 18 were repaired)
  - a broken item is joined to every troop that carries it, directly or through
    a standalone equipment roster
  - the report names the body, the item and the troop on one line, is stable
    across runs, and the exit code is 1 on any MISSING_BODY / MISSING_MESH /
    dead item id / referenced delete, else 0
  - a clean run says so in words a reader can grep for
"""
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_armory_refs as aa  # noqa: E402
import audit_deleted_mesh_impact as am  # noqa: E402
import validate_mesh_refs as vm  # noqa: E402


def _issue(code, entry_id, name, file="LOTRLOME_items/x.xml", line=1):
    sev = vm.Severity.ERROR if code in ("MISSING_BODY", "MISSING_MESH") else vm.Severity.WARNING
    return vm.Issue(severity=sev, code=code, file=file, line=line, entry_id=entry_id,
                    message=f"{code}: {name}")


class ReferencedFlagsFromLiveRefs(unittest.TestCase):
    def test_delete_flag_comes_from_live_refs_not_committed_catalogue(self):
        changes = {
            "delete": [("wm_elven_bow_v1", "Y"), ("wm_old_helmet", "Y")],
            "rename": [("sm_shield_hevy_a", ["sm_shield_heavy_a"], "Y")],
            "move": [], "moved_same_name": [], "new": ["wm_elven_bow_a01"],
        }
        live_refs = {"wm_elven_bow_v1", "sm_shield_heavy_a"}
        flagged = aa.flag_breakage(changes, live_refs)
        self.assertEqual([n for n, _ in flagged["delete"] if _], ["wm_elven_bow_v1"])
        # the committed flag said Y for wm_old_helmet; nothing names it any more
        self.assertIn(("wm_old_helmet", False), flagged["delete"])
        # a rename whose OLD name nothing names any more is repaired, not breakage
        self.assertEqual(flagged["rename"], [("sm_shield_hevy_a", ["sm_shield_heavy_a"], False)])
        self.assertEqual(aa.count_breaking(flagged), 1)


class RosterImpactJoin(unittest.TestCase):
    def test_direct_and_roster_hop_consumers_both_appear(self):
        troops = (
            '<NPCCharacter id="elf_archer">\n'
            '  <equipment slot="Item0" id="Item.starter_highelf_longbow"/>\n'
            '</NPCCharacter>\n'
            '<NPCCharacter id="elf_lord">\n'
            '  <Equipments><EquipmentSet id="noldor_lord_kit"/></Equipments>\n'
            '</NPCCharacter>\n')
        roster = (
            '<EquipmentRoster id="noldor_lord_kit">\n'
            '  <equipment slot="Item0" id="Item.starter_highelf_longbow"/>\n'
            '</EquipmentRoster>\n')
        item_refs = (am.extract_item_refs_from_text(troops, "troops/troops_rivendell.xml")
                     + am.extract_item_refs_from_text(roster, "equipmentsets/lords.xml"))
        roster_refs = am.extract_roster_refs_from_text(troops, "troops/troops_rivendell.xml")
        impact = aa.join_impact({"starter_highelf_longbow"}, item_refs, roster_refs)
        row = impact["starter_highelf_longbow"]
        self.assertIn("elf_archer", row.direct_owners)
        self.assertIn("noldor_lord_kit", row.direct_owners)   # the roster is a consumer too
        self.assertIn("elf_lord", row.roster_owners)
        self.assertEqual(row.consumer_count, 3)

    def test_unused_item_is_orphan(self):
        impact = aa.join_impact({"nobody_wears_this"}, [], [])
        self.assertEqual(impact["nobody_wears_this"].consumer_count, 0)


class ReportAndExitCode(unittest.TestCase):
    def _summary(self, issues):
        return aa.Summary(
            catalogue={"delete": [("wm_elven_bow_v1", True)], "rename": [], "move": [],
                       "moved_same_name": [], "new": ["wm_elven_bow_a01"]},
            issues=issues,
            impact=aa.join_impact({"starter_highelf_longbow"}, [
                am.ItemRef("starter_highelf_longbow", "troops/troops_rivendell.xml", 3,
                           "prefixed", "elf_archer")], []),
            dead_item_refs=[],
            generator_checks=[("generate_ranged_ladder_items.py --verify", 0, "OK")],
            unreferenced_new=["wm_elven_bow_a01"],
            tpac_count=4364, mesh_count=4495, body_count=388,
        )

    def test_report_names_body_item_and_troop_on_one_row_and_is_stable(self):
        s = self._summary([_issue("MISSING_BODY", "starter_highelf_longbow", "bo_wm_elven_bow_v1",
                                  "LOTRLOME_items/rivendell/starter_kit.xml", 56)])
        text1 = aa.render_report(s, when="2026-09-15", head="abc1234")
        text2 = aa.render_report(s, when="2026-09-15", head="abc1234")
        self.assertEqual(text1, text2)
        row = [l for l in text1.splitlines() if "bo_wm_elven_bow_v1" in l and "starter_highelf_longbow" in l]
        self.assertTrue(row, text1)
        self.assertIn("elf_archer", row[0])
        self.assertIn("MISSING_BODY", text1)
        self.assertIn("wm_elven_bow_a01", text1)          # new art nothing uses
        self.assertNotIn("—", text1)                  # no long dashes in produced prose
        self.assertEqual(aa.exit_code(s), 1)

    def test_clean_run_is_greppable_and_exits_zero(self):
        s = self._summary([])
        s.catalogue["delete"] = []
        s.unreferenced_new = []
        text = aa.render_report(s, when="2026-09-15", head="abc1234")
        self.assertIn("CLEAN", text)
        self.assertEqual(aa.exit_code(s), 0)

    def test_dead_item_ref_alone_fails(self):
        s = self._summary([])
        s.catalogue["delete"] = []
        s.dead_item_refs = [am.ItemRef("gone_sword", "troops/troops_gondor.xml", 9, "prefixed", "gondor_sword_1")]
        self.assertEqual(aa.exit_code(s), 1)
        self.assertIn("gone_sword", aa.render_report(s, when="2026-09-15", head="abc1234"))

    def test_generator_drift_warns_but_does_not_fail(self):
        s = self._summary([])
        s.catalogue["delete"] = []
        s.generator_checks = [("generate_starter_kit.py --verify", 1, "DRIFT: x missing")]
        self.assertEqual(aa.exit_code(s), 0)
        self.assertEqual(aa.warning_count(s), 1)
        text = aa.render_report(s, when="2026-09-15", head="abc1234")
        self.assertIn("CLEAN, 1 warning", text)
        self.assertIn("DRIFT: x missing", text)


class GeneratorChecks(unittest.TestCase):
    def test_the_server_skeleton_package_is_checked_after_an_armory_sync(self):
        # An art drop that changes a creature skeleton leaves the dedicated server's
        # DsAssetPackages copy stale (#972); the audit is what runs after every sync.
        self.assertIn(["build_ds_skeleton_pack.py", "--check"], aa.GENERATOR_VERIFIES)

    def test_every_check_names_a_script_that_exists(self):
        tools = Path(aa.__file__).resolve().parent
        for argv in aa.GENERATOR_VERIFIES:
            self.assertTrue((tools / argv[0]).is_file(), argv[0])


class CompanionChecks(unittest.TestCase):
    """The audit folds the checks that used to be run by hand into its verdict (a reinstall reverts
    the troll gear lock, the orc locomotion rows and the FBX LOD passes, and the audit alone read CLEAN)."""

    def _clean(self):
        s = ReportAndExitCode()._summary([])
        s.catalogue["delete"] = []
        s.unreferenced_new = []
        return s

    def test_the_named_companions_include_every_hand_run_check(self):
        scripts = {argv[0]: argv for argv in aa.COMPANION_CHECKS}
        for name in ("wire_hill_troll_race.py", "register_armory_names.py", "bind_orc_goblin_locomotion.py",
                     "audit_fbx_lods.py", "validate_moduledata.py"):
            self.assertIn(name, scripts)
        tail = scripts["validate_moduledata.py"]
        self.assertIn("COLLISION_BODY_BORROWED", tail)
        self.assertIn("CREATURE_GEAR_OBTAINABLE", tail)

    def test_every_companion_names_a_script_that_exists(self):
        tools = Path(aa.__file__).resolve().parent
        for argv in aa.COMPANION_CHECKS:
            self.assertTrue((tools / argv[0]).is_file(), argv[0])

    def test_a_failing_companion_makes_the_verdict_broken(self):
        s = self._clean()
        s.companion_checks = [("wire_hill_troll_race.py --check", 1, "an unkeyed troll clip")]
        self.assertEqual(aa.exit_code(s), 1)
        text = aa.render_report(s, when="2026-09-15", head="abc1234")
        self.assertIn("BROKEN", text)
        self.assertIn("an unkeyed troll clip", text)

    def test_passing_companions_leave_the_verdict_clean(self):
        s = self._clean()
        s.companion_checks = [("audit_fbx_lods.py --check", 0, "OK")]
        self.assertEqual(aa.exit_code(s), 0)
        self.assertIn("audit_fbx_lods.py --check", aa.render_report(s, when="2026-09-15", head="abc1234"))

    def test_a_companion_that_reports_drift_and_gaps_shows_both(self):
        out = "/x/lod-audit.md:\nDRIFT: the committed report does not reproduce\nGAPS: 2 shipped chains lack a level"
        done = mock.Mock(returncode=1, stdout="", stderr=out)
        with mock.patch.object(aa.subprocess, "run", return_value=done):
            got = aa.run_companion_checks(Path(aa.__file__).resolve().parent.parent)
        _, status, detail = got[0]
        self.assertEqual(status, 1)
        self.assertIn("DRIFT", detail)
        self.assertIn("GAPS: 2 shipped chains", detail)

    def test_a_skipped_companion_has_not_passed(self):
        self.assertEqual(aa.companion_status(0, "check_race: SKIPPED, no install"), 2)
        self.assertEqual(aa.companion_status(0, "OK"), 0)
        self.assertEqual(aa.companion_status(1, "OK"), 1)

    def test_a_companion_that_exits_two_did_not_run_rather_than_failed(self):
        self.assertEqual(aa.companion_status(2, ""), 2)
        self.assertEqual(aa.companion_status(3, ""), 1)
        text = aa.render_report(self._with_companion(2), when="2026-09-15", head="abc1234")
        self.assertIn("NOT PASSED", text)

    def _with_companion(self, status):
        s = self._clean()
        s.companion_checks = [("wire_hill_troll_race.py --check", status, "the game is running")]
        return s


if __name__ == "__main__":
    unittest.main()
