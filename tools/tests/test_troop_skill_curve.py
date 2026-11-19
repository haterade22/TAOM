#!/usr/bin/env python3
"""The troop skill curve (#343): one role table on the vanilla range, spread over TAOM's ten tiers.

Run:  python -m unittest tools.tests.test_troop_skill_curve

The main carried weapon takes N x (1 + the culture's percent), where N is the tier's number on the
curve (vanilla's 20, 40, 70, 100, 130 at tiers 1 to 5, then even steps to 260). Every other skill
is a share of N (docs/features/troop-skill-balance.md). Synthetic kits, real culture rows; the
roster tests at the end read the shipped troop files and the install.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ranged_ladder as rl     # noqa: E402
import rebalance_troops as rb  # noqa: E402
import troop_purpose as tp     # noqa: E402

SWORD = tp.kit_of([{"Item0": "OneHanded", "Item1": "Shield"}])
SPEAR_AND_SWORD = tp.kit_of([{"Item0": "Polearm", "Item1": "Shield", "Item2": "OneHanded"}])
TWO_HANDER = tp.kit_of([{"Item0": "TwoHanded"}])
ARCHER = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}])
LANCER = tp.kit_of([{"Item0": "Polearm", "Item1": "OneHanded", "Item2": "Shield"}])
THROWER = tp.kit_of([{"Item0": "Throwing", "Item1": "OneHanded", "Item2": "Shield"}])


ELVES = ("rivendell", "lindon", "mirkwood", "lothlorien")
DWARVES = ("erebor", "iron_hills", "eredluin")
MEN = ("gondor", "rohan", "dale", "dunland", "rhun", "umbar", "harad", "arthedain", "cardolan", "rhudaur",
       "anduinvale", "angaladh", "dorwinion", "southrhovanion", "mordor_numenorean")
PLAIN_ORCS = ("mordor", "gundabad", "mistymountainorcs", "goblin", "feral_orcs")
ELITE_ORCS = ("isengard", "isengard_orthanc", "mordor_uruk", "dolguldur")


def skills(culture, level, kit, troop_id="x_troop", **kw):
    return rb.calculate_skills(culture, level, troop_id, kit, **kw)


def _mean(cultures, skill):
    return rb.Fraction(sum(rb.CULTURE_PCT[c][skill] for c in cultures), len(cultures))


class CurveTests(unittest.TestCase):
    def test_the_curve_is_candidate_c(self):
        self.assertEqual([rb.TIER_CURVE[t] for t in range(1, 11)],
                         [20, 40, 70, 100, 130, 156, 182, 208, 234, 260])

    def test_a_tier_0_troop_takes_the_tier_1_number(self):
        self.assertEqual(skills("gondor", 1, SWORD), skills("gondor", 6, SWORD))

    def test_an_off_grid_level_takes_its_engine_tier(self):
        self.assertEqual(skills("gondor", 7, SWORD), skills("gondor", 6, SWORD))

    def test_values_round_to_the_nearest_five_half_up(self):
        self.assertEqual(rb.round5(rb.Fraction(125, 1) / 1), 125)
        self.assertEqual(rb.round5(rb.Fraction(1225, 10)), 125)   # 122.5 rounds up
        self.assertEqual(rb.round5(rb.Fraction(1224, 10)), 120)

    def test_an_exact_half_from_a_culture_share_rounds_up(self):
        # Mirkwood's +25 on a tier 3 Bow is 70 x 1.25 = 87.5 exactly; a float or a banker's
        # rounding would give 85.
        self.assertEqual(skills("mirkwood", 20, ARCHER)["Bow"], 90)


class RoleTableTests(unittest.TestCase):
    def test_the_main_weapon_is_n_times_the_culture_share(self):
        # gondor OneHanded 135 and 275, rivendell 155 and 305, erebor TwoHanded 145 and 290 (its +12
        # since the maintainer's order put the Dwarves over Men).
        self.assertEqual(skills("gondor", 26, SWORD)["OneHanded"], 135)
        self.assertEqual(skills("gondor", 51, SWORD)["OneHanded"], 275)
        self.assertEqual(skills("rivendell", 26, SWORD)["OneHanded"], 155)
        self.assertEqual(skills("rivendell", 51, SWORD)["OneHanded"], 305)
        self.assertEqual(skills("erebor", 26, TWO_HANDER)["TwoHanded"], 145)
        self.assertEqual(skills("erebor", 51, TWO_HANDER)["TwoHanded"], 290)

    def test_a_second_carried_weapon_is_nine_tenths(self):
        s = skills("gondor", 26, SPEAR_AND_SWORD)
        self.assertEqual(s["Polearm"], 135)     # 130 x 1.03
        self.assertEqual(s["OneHanded"], 125)   # 0.9 x 130 x 1.05 = 122.85

    def test_a_melee_class_not_carried_is_half_and_a_launcher_or_throwing_two_fifths(self):
        s = skills("gondor", 26, SWORD)
        self.assertEqual((s["TwoHanded"], s["Polearm"]), (65, 65))
        self.assertEqual((s["Bow"], s["Crossbow"], s["Throwing"]), (50, 50, 50))   # 52 rounds to 50

    def test_athletics_is_n_on_foot_and_half_mounted(self):
        self.assertEqual(skills("gondor", 26, SWORD)["Athletics"], 135)
        self.assertEqual(skills("gondor", 26, LANCER, mounted=True)["Athletics"], 65)

    def test_riding_is_n_mounted_and_two_fifths_on_foot(self):
        self.assertEqual(skills("rohan", 26, LANCER, mounted=True)["Riding"], 150)   # 130 x 1.15
        self.assertEqual(skills("rohan", 51, LANCER, mounted=True)["Riding"], 300)
        self.assertEqual(skills("rohan", 26, SWORD)["Riding"], 50)

    def test_a_rider_is_never_under_his_horse(self):
        # The engine mounts only when Riding >= MountDifficulty (Agent.CheckSkillForMounting).
        self.assertEqual(skills("rohan", 6, LANCER, mounted=True, mount_difficulty=80)["Riding"], 80)
        self.assertEqual(skills("rohan", 51, LANCER, mounted=True, mount_difficulty=80)["Riding"], 300)

    def test_a_thrower_takes_the_main_number_on_throwing(self):
        s = skills("gondor", 26, tp.kit_of([{"Item0": "OneHanded", "Item1": "Throwing"}]),
                   troop_id="gondor_har_javelineer")
        self.assertEqual(s["Throwing"], 125)    # 130 x 0.95 = 123.5
        self.assertEqual(s["OneHanded"], 135)

    def test_a_javelin_in_item0_is_the_main_weapon(self):
        s = skills("rhun", 31, THROWER)
        self.assertEqual(s["Throwing"], 150)    # 156 x 0.97 = 151.3
        self.assertGreaterEqual(s["Throwing"], max(s[c] for c in tp.MELEE + tp.LAUNCHERS))

    def test_a_kit_with_no_weapon_has_no_formula(self):
        self.assertIsNone(rb.calculate_skills("gondor", 26, "x", None))

    def test_alternative_weapons_tie_at_the_main_value(self):
        kit = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows", "Item2": "TwoHanded"},
                         {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}])
        s = skills("lothlorien", 31, kit)
        self.assertEqual(s["TwoHanded"], s["OneHanded"])


class CultureTableTests(unittest.TestCase):
    def test_every_row_names_all_eight_skills(self):
        for culture, row in rb.CULTURE_PCT.items():
            self.assertEqual(set(row), set(rb.SKILL_NAMES), culture)

    def test_a_culture_with_no_row_is_an_error_not_a_zero(self):
        with self.assertRaises(rb.MissingCultureRow):
            skills("no_such_culture", 26, SWORD)

    def test_every_culture_on_disk_has_a_row(self):
        # The Lindon lesson (#522): a missing key rebaselines a whole faction to the bare curve.
        missing = set()
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            fc = path.name[len("troops_"):-len(".xml")]
            for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
                culture = rb.detect_culture(npc.get("id", ""), fc)
                if culture not in rb.CULTURE_PCT:
                    missing.add(culture)
        self.assertEqual(sorted(missing), [])

    def test_every_row_belongs_to_one_people(self):
        # A new row must be placed among the peoples, or the order tests below never see it.
        peoples = ELVES + DWARVES + MEN + PLAIN_ORCS + ELITE_ORCS
        self.assertEqual(len(peoples), len(set(peoples)))
        self.assertEqual(set(peoples), set(rb.CULTURE_PCT))

    def test_on_melee_the_elves_lead_then_the_dwarves_then_men(self):
        # The maintainer's order (#343): Elves best, Dwarves next, then Men. The test pins the
        # order of the means, not the numbers, so a row can move inside its people.
        for s in tp.MELEE:
            elves, dwarves, men = (_mean(group, s) for group in (ELVES, DWARVES, MEN))
            self.assertGreater(elves, dwarves, s)
            self.assertGreater(dwarves, men, s)

    def test_no_plain_orc_row_sits_over_men_on_melee(self):
        # Against the mean of the mannish rows: Harad's TwoHanded -5 and Polearm -3 are its own
        # identity, and no orc row is meant to match each mannish low.
        for s in tp.MELEE:
            men = _mean(MEN, s)
            for o in PLAIN_ORCS:
                self.assertLessEqual(rb.CULTURE_PCT[o][s], men, f"{o} {s}")

    def test_no_orc_row_sits_over_any_dwarf_row_on_melee(self):
        # The elite uruks reach Gondor's level and a little over on TwoHanded and Polearm, never
        # the Dwarves'.
        for s in tp.MELEE:
            floor = min(rb.CULTURE_PCT[d][s] for d in DWARVES)
            for o in PLAIN_ORCS + ELITE_ORCS:
                self.assertLessEqual(rb.CULTURE_PCT[o][s], floor, f"{o} {s}")

    def test_each_people_keeps_its_strength(self):
        row = rb.CULTURE_PCT
        self.assertEqual([c for c in row if row[c]["Riding"] == max(r["Riding"] for r in row.values())],
                         ["rohan"])
        self.assertEqual([c for c in row if row[c]["Bow"] == max(r["Bow"] for r in row.values())],
                         ["mirkwood"])
        self.assertGreater(row["dale"]["Polearm"], max(row[c]["Polearm"] for c in MEN if c != "dale"))
        top_two_handed = max(row[c]["TwoHanded"] for c in row if c not in ELVES)
        self.assertIn(top_two_handed, (row["erebor"]["TwoHanded"], row["iron_hills"]["TwoHanded"]))

    def test_the_black_numenoreans_are_men_with_their_own_row(self):
        self.assertEqual(rb.detect_culture("mordor_num_knight", "mordor"), "mordor_numenorean")
        self.assertEqual(rb.detect_culture("mordor_orc_warrior", "mordor"), "mordor")
        self.assertEqual(rb.CULTURE_PCT["mordor_numenorean"],
                         {"Athletics": 3, "Riding": 3, "OneHanded": 5, "TwoHanded": 5, "Polearm": 5,
                          "Bow": 0, "Crossbow": 0, "Throwing": 0})

    def test_no_other_hand_tuned_line_has_a_route(self):
        # gondor_loss_noble, the dwarf ram riders and the Ironfang troops stay on their culture.
        self.assertEqual(rb.detect_culture("gondor_loss_noble_knight", "gondor"), "gondor")
        self.assertEqual(rb.detect_culture("ironpass_ram_rider", "erebor"), "erebor")
        self.assertEqual(rb.detect_culture("goblin_bolgs_ironfang", "goblin"), "goblin")

    def test_the_feature_doc_prints_the_table_the_code_holds(self):
        doc = (Path(__file__).resolve().parents[2] / "docs" / "features" / "troop-skill-balance.md")
        text = doc.read_text(encoding="utf-8").replace("\r\n", "\n")
        table = text.split("### The culture table (percent of N)\n", 1)[1].strip("\n").split("\n\n", 1)[0]
        printed = {}
        for line in table.splitlines()[2:]:
            cells = [c.strip() for c in line.strip("|").split("|")]
            for culture in cells[0].split(", "):
                printed[culture] = dict(zip(rb.SKILL_NAMES, (int(v) for v in cells[1:])))
        self.assertEqual(printed, rb.CULTURE_PCT)


class ArcherTests(unittest.TestCase):
    def test_an_archers_sword_is_nine_tenths_of_its_cell_rounded_down(self):
        s = skills("gondor", 26, ARCHER, ladder={"Bow": 130})
        self.assertEqual(s["Bow"], 130)
        self.assertEqual(s["OneHanded"], 115)   # 117 rounded down to 5

    def test_mirkwoods_tier_10_archer_keeps_its_bow_on_top(self):
        cell = rl.skill_cell("mirkwood", 10, rl.load_spec())
        s = skills("mirkwood", 51, ARCHER, ladder={"Bow": cell})
        self.assertEqual(s["OneHanded"], 285)   # 0.9 x 320 = 288
        self.assertEqual(max(s[c] for c in tp.MELEE + tp.LAUNCHERS), cell)

    def test_an_archer_off_the_ladder_reads_its_own_launcher_value(self):
        s = skills("gondor", 26, ARCHER)
        self.assertEqual(s["Bow"], 130)
        self.assertEqual(s["OneHanded"], 115)

    def test_no_melee_or_spare_launcher_sits_over_a_militia_archers_cell(self):
        # A militia archer takes the tier 4 row on everything but its launcher, whose ladder cell is
        # by its own tier; nothing may then sit over the launcher.
        cell = 32
        s = skills("dunland", 11, ARCHER, troop_id="dunland_militia_archer_test", ladder={"Bow": cell},
                   militia=True)
        self.assertLessEqual(max(s[c] for c in tp.MELEE), cell)
        self.assertLessEqual(s["Crossbow"], cell)
        self.assertEqual(tp.kit_mismatches(s, ARCHER), [])

    def test_a_melee_troops_ladder_bow_never_tops_its_main_weapon(self):
        kit = tp.kit_of([{"Item0": "OneHanded", "Item1": "Bow", "Item2": "Arrows"}])
        s = skills("mirkwood", 41, kit, ladder={"Bow": 256})
        self.assertEqual(s["OneHanded"], 256)
        self.assertEqual(tp.kit_mismatches(s, kit), [])


class MilitiaTests(unittest.TestCase):
    def test_militia_take_the_tier_4_row(self):
        self.assertEqual(rb.MILITIA_TIER, 4)
        s = skills("gondor", 11, SPEAR_AND_SWORD, militia=True)
        self.assertEqual(s, skills("gondor", 21, SPEAR_AND_SWORD))

    def test_the_elite_militia_add_ten_on_every_skill(self):
        self.assertEqual(rb.MILITIA_ELITE_BONUS, 10)
        basic = skills("gondor", 11, SPEAR_AND_SWORD, militia=True)
        vet = skills("gondor", 16, SPEAR_AND_SWORD, militia=True, elite_militia=True)
        for s in rb.SKILL_NAMES:
            self.assertEqual(vet[s], basic[s] + 10, s)

    def test_the_real_bindings_decide_who_is_militia(self):
        self.assertEqual(skills("gondor", 11, SPEAR_AND_SWORD, troop_id="gondor_militia_spearman"),
                         skills("gondor", 11, SPEAR_AND_SWORD, militia=True))
        self.assertEqual(skills("gondor", 16, SPEAR_AND_SWORD, troop_id="gondor_militia_veteran_spearman"),
                         skills("gondor", 16, SPEAR_AND_SWORD, militia=True, elite_militia=True))


class LadderSpecTests(unittest.TestCase):
    def test_the_ladder_anchors_on_the_curve(self):
        sk = rl.load_spec()["stats"]["skill"]
        self.assertEqual({int(t): v for t, v in sk["anchor"].items()}, rb.TIER_CURVE)
        self.assertEqual({int(t): v for t, v in sk["step"].items()},
                         {t: int(rb.Fraction(4, 100) * n + rb.Fraction(1, 2)) for t, n in rb.TIER_CURVE.items()})

    def test_the_spec_is_clean(self):
        self.assertEqual(rl.validate_spec(rl.load_spec()), [])


class ShippedRosterTests(unittest.TestCase):
    """The troop files against the formula, with the install's item and horse files."""

    @classmethod
    def setUpClass(cls):
        if not os.path.isdir(rb.DEFAULT_GAME_MODULES):
            raise unittest.SkipTest("game install not found: %s" % rb.DEFAULT_GAME_MODULES)
        import taom_schema as ts
        cls.classes = ts.build_item_class_registry(rb.MODULEDATA_DIR, rb.DEFAULT_GAME_MODULES)
        cls.mounts = ts.build_mount_difficulty_registry(rb.MODULEDATA_DIR, rb.DEFAULT_GAME_MODULES)
        cls.records = []
        cls.npcs = {}
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            cls.records += rb.process_file(str(path), item_classes=cls.classes, mounts=cls.mounts)
            for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
                cls.npcs[npc.get("id")] = npc

    def test_no_mounted_troop_rides_under_its_horse(self):
        under = []
        for r in self.records:
            mounted, difficulty = rb.troop_mount(self.npcs[r["id"]], self.mounts)
            if mounted and r["old"].get("Riding", 0) < difficulty:
                under.append(f"{r['id']} Riding {r['old'].get('Riding', 0)} < {difficulty}")
        self.assertEqual(under, [])

    def _settled(self):
        files = [str(p) for p in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml"))]
        recs = rb.load_roster(files, self.classes, self.mounts)
        rb.settle_purpose(recs)
        return recs

    def test_the_shipped_skills_are_a_fixed_point_of_the_writer(self):
        # Every value on disk is what --apply plans, so a hand edit or a drifted rule shows here.
        recs = self._settled()
        off = [f"{c['id']} {s} {c['old'].get(s, 0)} -> {c['final'][s]}" for c in recs
               if not c.get("external") for s in rb.SKILL_NAMES if c["old"].get(s, 0) != c["final"][s]]
        self.assertEqual(off, [], f"{len(off)} values off the plan")
        self.assertGreater(len(recs), 1000)
        # The Fords of War copies are on the curve too (the second root), so they are in the plan.
        fords = [c for c in recs if c["file"] == "fords_troops.xml" and c.get("new") is not None]
        self.assertGreater(len(fords), 200)

    def test_a_promoted_villager_equals_its_source_villager(self):
        # promote_borrowed_cultures.py copies the source culture's npcs file, so its --check holds
        # only while the copy's villager has the source villager's skills.
        def villager(culture):
            path = Path(rb.CHARACTERS_DIR) / f"npcs_{culture}.xml"
            if not path.exists():
                return None
            for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
                if npc.get("id") == f"villager_{culture}" and npc.find("skills") is not None:
                    return {s.get("id"): s.get("value") for s in npc.iter("skill")}
            return None
        pairs = [(t, s) for t, s in sorted(rb.promoted_npcs_sources().items())
                 if villager(t) is not None and villager(s) is not None]
        self.assertIn(("rhudaur", "gondor"), pairs)
        self.assertEqual([(t, s) for t, s in pairs if villager(t) != villager(s)], [])

    def test_the_villagers_are_written_on_the_tier_1_row(self):
        recs = {c["id"]: c for c in self._settled()}
        gondor = recs["villager_gondor"]
        self.assertFalse(gondor.get("external"))
        self.assertEqual(gondor["file"], "npcs_gondor.xml")
        self.assertEqual(gondor["new"]["Polearm"], 20)     # the pitchfork, N 20 x 1.03
        self.assertEqual(gondor["new"]["Throwing"], 15)    # the stone, 0.9 x 20 x 0.95 = 17.1
        # A villager with no <skills> block stays a read-only source: the writer adds no block.
        self.assertTrue(recs["villager_rohan"].get("external"))

    def test_the_analyzer_measures_against_the_settled_values(self):
        import analyze_troop_balance as atb
        troops = []
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            troops += atb.parse_troop_file(str(path), self.classes, self.mounts)[1]
        atb.analyze(troops, {}, 0, settled=atb.settled_reference(self.classes, self.mounts))
        off = [f"{t['id']} {t['delta']:+d}" for t in troops if t["ref"] is not None and t["delta"]]
        self.assertEqual(off, [], "the clamp's raises read as outliers")


def _npc(troop_id, level, skills_values, items, upgrades=(), extra=""):
    skills = "".join(f'\n      <skill id="{s}" value="{v}" />' for s, v in zip(rb.SKILL_NAMES, skills_values))
    ups = "".join(f'\n      <upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
    eq = "".join(f'\n        <equipment slot="Item{i}" id="Item.{w}" />' for i, w in enumerate(items))
    return (f'  <NPCCharacter id="{troop_id}" level="{level}" default_group="Infantry"{extra}>\n'
            + (f"    <skills>{skills}\n    </skills>\n" if skills_values else "")
            + (f"    <upgrade_targets>{ups}\n    </upgrade_targets>\n" if ups else "")
            + f"    <Equipments>\n      <EquipmentRoster>{eq}\n      </EquipmentRoster>\n    </Equipments>\n"
            + "  </NPCCharacter>\n")


class _FixtureRun(unittest.TestCase):
    """main() over a temporary troops/ and characters/ folder with a fixture item registry."""

    CLASSES = {"fixture_sword": "OneHanded", "fixture_fork": "Polearm", "fixture_stone": "Throwing"}

    def setUp(self):
        import tempfile
        self.tmp = Path(tempfile.mkdtemp())
        (self.tmp / "troops").mkdir()
        (self.tmp / "characters").mkdir()
        # main() refuses an install with no vanilla SkillSet file (#626), so the fixture install
        # ships one; the SkillSets are read from the parent of the Modules folder.
        self.install = Path(tempfile.mkdtemp())
        native = self.install / "Modules" / "Native" / "ModuleData"
        native.mkdir(parents=True)
        (native / "skill_sets.xml").write_bytes(
            b'<SkillSets><SkillSet id="any"><skill id="Bow" value="10" /></SkillSet></SkillSets>')

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)
        shutil.rmtree(self.install, ignore_errors=True)

    def write(self, rel, body, bom=False, crlf=False):
        text = "<NPCCharacters>\n" + body + "</NPCCharacters>\n"
        data = (text.replace("\n", "\r\n") if crlf else text).encode("utf-8")
        (self.tmp / rel).write_bytes((b"\xef\xbb\xbf" if bom else b"") + data)

    def run_main(self, *argv):
        import io
        from contextlib import redirect_stdout
        from unittest import mock
        out = io.StringIO()
        with mock.patch.object(rb, "TROOPS_DIR", str(self.tmp / "troops")), \
                mock.patch.object(rb, "CHARACTERS_DIR", str(self.tmp / "characters")), \
                mock.patch.object(rb, "EXTRA_ROOT_DIR", str(self.tmp)), \
                mock.patch("taom_schema.build_item_class_registry", lambda *a: dict(self.CLASSES)), \
                mock.patch("taom_schema.build_mount_difficulty_registry", lambda *a: {}), \
                mock.patch.object(sys, "argv", ["rebalance_troops.py", *argv, "--game-modules", str(self.install / "Modules")]), \
                redirect_stdout(out):
            try:
                rb.main()
                code = 0
            except SystemExit as exc:
                code = exc.code or 0
        return code, out.getvalue()


class ClampOnlyRunTests(_FixtureRun):
    """A culture with no CULTURE_PCT row stops --apply and nothing else (a second troop root, such
    as the Fords of War copies, may carry one)."""

    def setUp(self):
        super().setUp()
        self.write("troops/troops_atlantis.xml",
                   _npc("atlantis_recruit", 6, [50] * 8, ["fixture_sword"], upgrades=["atlantis_soldier"])
                   + _npc("atlantis_soldier", 11, [30] * 8, ["fixture_sword"]))

    def test_a_clamp_only_run_plans_the_clamp_and_exits_0(self):
        code, out = self.run_main("--fix-monotonicity")
        self.assertEqual(code, 0, out)
        self.assertIn("SKIPPED (no culture row)", out)
        text = (self.tmp / "troops" / "troops_atlantis.xml").read_text(encoding="utf-8")
        soldier = text.split('id="atlantis_soldier"', 1)[1]
        self.assertNotIn('value="30"', soldier)

    def test_a_fix_purpose_run_exits_0(self):
        code, out = self.run_main("--fix-purpose")
        self.assertEqual(code, 0, out)

    def test_apply_still_stops_and_names_the_culture(self):
        code, out = self.run_main("--apply", "--dry-run")
        self.assertEqual(code, 1, out)
        self.assertIn("atlantis", out)

    def test_an_install_with_no_vanilla_skillset_stops_before_anything_is_written(self):
        # The repo's own SkillSet files still load, so only the vanilla half proves the install
        # was read (#626 review).
        (self.install / "Modules" / "Native" / "ModuleData" / "skill_sets.xml").unlink()
        before = (self.tmp / "troops" / "troops_atlantis.xml").read_bytes()
        code, out = self.run_main("--fix-monotonicity")
        self.assertEqual(code, 1, out)
        self.assertIn("no vanilla SkillSet file", out)
        self.assertEqual((self.tmp / "troops" / "troops_atlantis.xml").read_bytes(), before)


class VillagerWriteTests(_FixtureRun):
    """--apply writes a villager's skill values into its npcs file and touches nothing else there."""

    def setUp(self):
        super().setUp()
        self.write("troops/troops_gondor.xml", _npc("gondor_ano_peasant", 6, [20] * 8, ["fixture_sword"]))
        self.npcs = (_npc("villager_gondor", 1, [30, 0, 30, 20, 30, 0, 0, 20], ["fixture_fork", "fixture_stone"],
                          upgrades=["gondor_ano_peasant"])
                     + _npc("child_gondor", 1, [30, 0, 30, 20, 30, 0, 0, 20], ["fixture_fork"])
                     + _npc("villager_gondor_bare", 1, [], ["fixture_fork"], upgrades=["gondor_ano_peasant"]))
        self.write("characters/npcs_gondor.xml", self.npcs, bom=True, crlf=True)

    def test_only_the_villagers_skill_values_change(self):
        before = (self.tmp / "characters" / "npcs_gondor.xml").read_bytes()
        code, out = self.run_main("--apply")
        self.assertEqual(code, 0, out)
        after = (self.tmp / "characters" / "npcs_gondor.xml").read_bytes()
        self.assertTrue(after.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(after.count(b"\r\n"), before.count(b"\r\n"))
        a, b = before.decode("utf-8-sig").split("\r\n"), after.decode("utf-8-sig").split("\r\n")
        self.assertEqual(len(a), len(b))
        changed = [i for i, (x, y) in enumerate(zip(a, b)) if x != y]
        villager = range(a.index('  <NPCCharacter id="villager_gondor" level="1" default_group="Infantry">'),
                         a.index('  <NPCCharacter id="child_gondor" level="1" default_group="Infantry">'))
        self.assertTrue(changed)
        self.assertTrue(all(i in villager and "<skill " in a[i] for i in changed), changed)
        root = ET.fromstring(after.decode("utf-8-sig"))
        vill = {s.get("id"): int(s.get("value")) for s in root.find("NPCCharacter").iter("skill")}
        self.assertEqual(vill["Polearm"], 20)
        self.assertEqual(vill["Throwing"], 15)


class PromotedVillagerTests(_FixtureRun):
    """A villager in an npcs file that promote_borrowed_cultures.py copies takes its copy source's
    row, so the copy stays equal to the source villager and the promoter's --check holds."""

    def test_rhudaur_is_a_copy_of_gondor(self):
        self.assertEqual(rb.promoted_npcs_sources()["rhudaur"], "gondor")

    def test_the_rhudaur_villager_takes_gondors_row(self):
        self.write("troops/troops_rhudaur.xml", _npc("rhudaur_levy", 6, [20] * 8, ["fixture_sword"]))
        self.write("characters/npcs_rhudaur.xml",
                   _npc("villager_rhudaur", 1, [30, 0, 30, 20, 30, 0, 0, 20], ["fixture_fork", "fixture_stone"],
                        upgrades=["rhudaur_levy"]))
        code, out = self.run_main("--apply")
        self.assertEqual(code, 0, out)
        root = ET.fromstring((self.tmp / "characters" / "npcs_rhudaur.xml").read_text(encoding="utf-8"))
        vill = {s.get("id"): int(s.get("value")) for s in root.iter("skill")}
        # The stone: 0.9 x 20 x 0.95 = 17.1 on Gondor's row (15); Rhudaur's own +5 would give 20.
        self.assertEqual(vill["Throwing"], 15)


FORDS = "fords_of_war/fords_troops.xml"


class SecondRootTests(_FixtureRun):
    """--apply puts the Fords of War copies (troop_purpose.EXTRA_TROOP_FILES) on the curve: each by
    its own level and kit, on the row of the campaign troop it copies, skill values only. No edge
    that touches the second root moves a campaign troop."""

    def setUp(self):
        super().setUp()
        gondor = ' culture="Culture.gondor"'
        self.write("troops/troops_gondor.xml",
                   _npc("gondor_recruit", 6, [20] * 8, ["fixture_sword"], upgrades=["gondor_soldier"], extra=gondor)
                   + _npc("gondor_soldier", 11, [40] * 8, ["fixture_sword"], extra=gondor))
        (self.tmp / "fords_of_war").mkdir()
        self.write(FORDS,
                   _npc("fow_gondor_soldier", 16, [99] * 8, ["fixture_fork"], extra=gondor)
                   + _npc("fow_gondor_pikeman", 11, [99] * 8, ["fixture_fork"], extra=gondor)
                   + _npc("fow_gondor_bare", 11, [], ["fixture_fork"], extra=gondor)
                   + _npc("fow_gondor_captain", 51, [300] * 8, ["fixture_sword"], upgrades=["gondor_soldier"],
                          extra=gondor),
                   crlf=True)

    def _skills(self, rel, troop_id):
        root = ET.fromstring((self.tmp / rel).read_text(encoding="utf-8"))
        npc = next(n for n in root.iter("NPCCharacter") if n.get("id") == troop_id)
        return {s.get("id"): int(s.get("value")) for s in npc.iter("skill")}

    def test_a_copy_takes_its_own_level_and_kit_on_its_source_troops_row(self):
        code, out = self.run_main("--apply")
        self.assertEqual(code, 0, out)
        polearm = tp.kit_of([{"Item0": "Polearm"}])
        self.assertEqual(self._skills(FORDS, "fow_gondor_soldier"),
                         rb.calculate_skills("gondor", 16, "fow_gondor_soldier", polearm))

    def test_a_copy_of_no_campaign_troop_takes_the_row_its_culture_attribute_names(self):
        self.run_main("--apply")
        polearm = tp.kit_of([{"Item0": "Polearm"}])
        self.assertEqual(self._skills(FORDS, "fow_gondor_pikeman"),
                         rb.calculate_skills("gondor", 11, "fow_gondor_pikeman", polearm))

    def test_only_skill_values_change_and_a_copy_without_skills_is_left(self):
        before = (self.tmp / FORDS).read_bytes()
        self.run_main("--apply")
        after = (self.tmp / FORDS).read_bytes()
        a, b = before.decode("utf-8").split("\r\n"), after.decode("utf-8").split("\r\n")
        self.assertEqual(len(a), len(b))
        changed = [i for i, (x, y) in enumerate(zip(a, b)) if x != y]
        self.assertTrue(changed)
        self.assertTrue(all("<skill " in a[i] for i in changed), changed)

    def test_a_second_root_edge_never_raises_a_campaign_troop(self):
        # fow_gondor_captain (tier 10) upgrades into gondor_soldier (tier 2) in this fixture.
        self.run_main("--apply")
        sword = tp.kit_of([{"Item0": "OneHanded"}])
        self.assertEqual(self._skills("troops/troops_gondor.xml", "gondor_soldier"),
                         rb.calculate_skills("gondor", 11, "gondor_soldier", sword))

    def test_a_second_run_plans_nothing(self):
        self.run_main("--apply")
        code, out = self.run_main("--apply", "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertIn("Changed: 0,", out)


class SecondRootRuleTests(_FixtureRun):
    """A copy follows its campaign source's rule: the exclusion list, the militia row and the
    thrown-primary list are keyed by the source id, which is the copy's id without COPY_PREFIX."""

    CLASSES = dict(_FixtureRun.CLASSES, fixture_shield="Shield")

    def setUp(self):
        super().setUp()
        gondor = ' culture="Culture.gondor"'
        self.write("troops/troops_gondor.xml",
                   _npc("gondor_recruit", 6, [20] * 8, ["fixture_sword"], extra=gondor)
                   + _npc("gondor_guard", 11, [40] * 8, ["fixture_shield", "fixture_sword"], extra=gondor))
        (self.tmp / "fords_of_war").mkdir()
        self.write(FORDS,
                   _npc("fow_cave_troll", 26, [77] * 8, ["fixture_sword"], extra=gondor)
                   + _npc("fow_gondor_militia_spearman", 11, [99] * 8, ["fixture_fork"], extra=gondor)
                   + _npc("fow_gondor_militia_veteran_spearman", 16, [99] * 8, ["fixture_fork"], extra=gondor)
                   + _npc("fow_gondor_har_skirmisher", 16, [99] * 8, ["fixture_sword", "fixture_stone"], extra=gondor)
                   + _npc("fow_gondor_shieldman", 11, [40] * 8, ["fixture_shield", "fixture_sword"], extra=gondor),
                   crlf=True)

    def _skills(self, troop_id):
        root = ET.fromstring((self.tmp / FORDS).read_text(encoding="utf-8"))
        npc = next(n for n in root.iter("NPCCharacter") if n.get("id") == troop_id)
        return {s.get("id"): int(s.get("value")) for s in npc.iter("skill")}

    def test_a_copy_of_an_excluded_troop_keeps_its_values(self):
        code, out = self.run_main("--apply")
        self.assertEqual(code, 0, out)
        self.assertEqual(set(self._skills("fow_cave_troll").values()), {77})

    def test_a_militia_copy_takes_its_twins_militia_row(self):
        self.run_main("--apply")
        fork = tp.kit_of([{"Item0": "Polearm"}])
        twin = rb.calculate_skills("gondor", 11, "gondor_militia_spearman", fork)
        self.assertNotEqual(twin, rb.calculate_skills("gondor", 11, "fow_gondor_militia_spearman", fork))
        self.assertEqual(self._skills("fow_gondor_militia_spearman"), twin)

    def test_an_elite_militia_copy_adds_the_elite_bonus(self):
        self.run_main("--apply")
        fork = tp.kit_of([{"Item0": "Polearm"}])
        self.assertEqual(self._skills("fow_gondor_militia_veteran_spearman"),
                         rb.calculate_skills("gondor", 16, "gondor_militia_veteran_spearman", fork))

    def test_a_thrown_primary_copy_takes_the_main_number_on_throwing(self):
        self.run_main("--apply")
        kit = tp.kit_of([{"Item0": "OneHanded", "Item1": "Throwing"}])
        twin = rb.calculate_skills("gondor", 16, "gondor_har_skirmisher", kit)
        self.assertNotEqual(twin, rb.calculate_skills("gondor", 16, "fow_gondor_har_skirmisher", kit))
        self.assertEqual(self._skills("fow_gondor_har_skirmisher"), twin)

    def test_fix_purpose_changes_no_equipment_line_in_the_second_root(self):
        # Trunk's --fix-purpose never read the second root; only --main-weapon-slots moves a copy's
        # Item0. A campaign troop still gets its shield out of Item0, as on trunk.
        self.run_main("--apply")
        fords = (self.tmp / FORDS).read_bytes()
        code, out = self.run_main("--fix-purpose", "--apply")
        self.assertEqual(code, 0, out)
        self.assertEqual((self.tmp / FORDS).read_bytes(), fords)
        guard = (self.tmp / "troops" / "troops_gondor.xml").read_text(encoding="utf-8").split('id="gondor_guard"')[1]
        self.assertIn('slot="Item0" id="Item.fixture_sword"', guard)


class ExtraRootPathTests(_FixtureRun):
    def test_a_listed_second_root_file_that_is_missing_warns(self):
        import io
        from contextlib import redirect_stdout
        from unittest import mock
        out = io.StringIO()
        with mock.patch.object(rb, "EXTRA_ROOT_DIR", str(self.tmp)), redirect_stdout(out):
            self.assertEqual(rb.extra_troop_paths(), [])
        self.assertIn("WARNING", out.getvalue())
        self.assertIn(FORDS, out.getvalue())

    def test_the_processing_line_names_only_files_that_exist(self):
        self.write("troops/troops_gondor.xml", _npc("gondor_recruit", 6, [20] * 8, ["fixture_sword"]))
        code, out = self.run_main("--apply", "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertNotIn(f"Processing {FORDS}", out)


if __name__ == "__main__":
    unittest.main()
