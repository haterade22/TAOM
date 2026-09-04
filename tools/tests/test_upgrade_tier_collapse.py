#!/usr/bin/env python3
"""Unit tests for the UPGRADE_TIER_COLLAPSE and UPGRADE_INDEX_EMPTY validator gates.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_upgrade_tier_collapse.py

Synthetic data only for the gates themselves; the tests that read the shipped ModuleData skip
when it is not there.

THE CONTRACT
------------
Vanilla DefaultPartyTroopUpgradeModel.GetXpCostForUpgrade sums a per-tier table over

    for (i = characterObject.Tier + 1; i <= upgradeTarget.Tier; i++)

so an edge whose target does not reach a higher tier exits the loop immediately and the method
returns 0. CharacterObject.Tier is clamp(ceil((level - 5) / 5), 0, MaxCharacterTier), a pure
function of the level= attribute in these files, so this is a data defect and nothing else.

Three engine consumers read that zero, and all three are wrong at it:

  * CampaignUIHelper.GetTroopXPTooltip evaluates `troop.Xp % cost`. A player hovering a stack of
    dg_uruk_foul (level 11, tier 2) whose only upgrade target dg_uruk_warrior sat at level 13,
    also tier 2, took a DivideByZeroException straight through PatchShield, which swallows only
    Missing*/TypeLoad. Crash bundle a7dc3a20, 2026-09-03.
  * PartyUpgraderCampaignBehavior gates on `cost > 0`, so AI parties promote the whole stack
    instantly for gold alone.
  * PartyBase.OnXpChanged clamps roster XP to Number * maxCost, permanently wiping the XP of any
    troop whose every target is priced at zero.

TaomPartyTroopUpgradeModel.GetXpCostForUpgrade floors the cost at runtime, so a new collapse is no
longer a crash. This gate exists so it is still a decision someone made rather than something
discovered in a player's crash bundle. Ten same-level edges are deliberate (the elf tier-10
capstone fan-out, the two borrowed-culture chosen_of_tharzog capstones, the uruk ranged branch, the
Dol Guldur villager entry point) and live in _LATERAL_BY_DESIGN with a stated reason each.
"""
import math
import os
import re
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import taom_schema as ts  # noqa: E402

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHIPPED_MODULEDATA = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData")
NL = chr(10)

XML_HEADER = '<?xml version="1.0" encoding="utf-8"?>' + NL + "<NPCCharacters>" + NL
XML_FOOTER = "</NPCCharacters>" + NL


def _troop(troop_id, level, upgrades=(), omit_level=False, skill_template=None):
    targets = NL.join(
        '      <upgrade_target id="NPCCharacter.{0}" />'.format(u) for u in upgrades)
    level_attr = "" if omit_level else ' level="{0}"'.format(level)
    template_attr = ("" if skill_template is None
                     else NL + '      skill_template="NPCCharacter.{0}"'.format(skill_template))
    return NL.join([
        '  <NPCCharacter id="{0}"{1} default_group="Infantry"'.format(troop_id, level_attr)
        + template_attr,
        '      name="{{=x_{0}}}{0}" occupation="Soldier" culture="Culture.gondor">'.format(troop_id),
        "    <skills>",
        '      <skill id="Athletics" value="50" />',
        "    </skills>",
        "    <upgrade_targets>",
        targets,
        "    </upgrade_targets>",
        "  </NPCCharacter>",
        "",
    ])


def _write_troops(root, troops_xml, filename="troops/troops_test.xml"):
    path = os.path.join(root, *filename.split("/"))
    with open(path, "w", encoding="utf-8") as f:
        f.write(XML_HEADER + troops_xml + XML_FOOTER)


def _validator(root):
    return ts.Validator(root, [], ts.Registries({}, {}, {}, {}, {}))


class TierFormulaTests(unittest.TestCase):
    """_troop_tier must equal the engine's GetTier for every level TAOM ships."""

    def test_matches_the_engine_formula(self):
        for level in range(0, 80):
            expected = min(max(math.ceil((level - 5) / 5.0), 0), ts.Validator._MAX_CHARACTER_TIER)
            self.assertEqual(expected, ts.Validator._troop_tier(level), "level {0}".format(level))

    def test_the_canonical_ladder_climbs_one_tier_per_rung(self):
        # 657 of the repo's 697 upgrade edges are +5, and this is why that spacing is the house
        # convention: each rung of 1/6/11/16/... lands in the next bracket.
        ladder = [1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51]
        self.assertEqual(list(range(0, 11)), [ts.Validator._troop_tier(l) for l in ladder])

    def test_max_character_tier_matches_the_shipped_game_model(self):
        # Reads the C# override rather than restating the number. Asserting `10 == 10` here would
        # be a tautology: it could not fail when someone edits TaomCharacterStatsModel, which is
        # the only drift this pin exists to catch.
        model = os.path.join(REPO_ROOT, "Main", "Features", "TroopProgression", "Models",
                             "TaomCharacterStatsModel.cs")
        if not os.path.isfile(model):
            self.fail("TaomCharacterStatsModel.cs not found at {0}; a pin that cannot find its "
                      "source has checked nothing".format(model))
        with open(model, encoding="utf-8-sig") as f:
            source = f.read()
        m = re.search(r"MaxCharacterTier\s*=>\s*(\d+)", source)
        self.assertIsNotNone(
            m, "could not find `MaxCharacterTier => N` in TaomCharacterStatsModel.cs; this pin "
               "needs updating to match however the override is now written")
        self.assertEqual(
            int(m.group(1)), ts.Validator._MAX_CHARACTER_TIER,
            "TaomCharacterStatsModel.MaxCharacterTier is {0} but taom_schema._MAX_CHARACTER_TIER "
            "is {1}. The gate's tier maths would silently disagree with the engine's."
            .format(m.group(1), ts.Validator._MAX_CHARACTER_TIER))


class ValidatorGateTests(unittest.TestCase):
    """The UPGRADE_TIER_COLLAPSE check in taom_schema.Validator."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="taom_tier_gate_")
        os.makedirs(os.path.join(self.root, "troops"))
        os.makedirs(os.path.join(self.root, "characters"))

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def _write(self, troops_xml, filename="troops/troops_test.xml"):
        _write_troops(self.root, troops_xml, filename)

    def _run(self):
        return _validator(self.root)._upgrade_tier_collapse()

    def test_a_bracket_crossing_edge_reports_nothing(self):
        self._write(_troop("t1", 11, ["t2"]) + _troop("t2", 16))
        self.assertEqual([], self._run())

    def test_a_same_tier_edge_is_an_error(self):
        # The shipped defect exactly: level 11 and level 13 are both tier 2.
        self._write(_troop("t1", 11, ["t2"]) + _troop("t2", 13))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("UPGRADE_TIER_COLLAPSE", issues[0].code)
        self.assertEqual(ts.Severity.ERROR, issues[0].severity)
        self.assertEqual("t1", issues[0].entry_id)
        self.assertIn("without leaving tier 2", issues[0].message)

    def test_a_same_level_edge_is_an_error(self):
        self._write(_troop("t1", 36, ["t2"]) + _troop("t2", 36))
        self.assertEqual(1, len(self._run()))

    def test_a_downhill_edge_is_an_error(self):
        self._write(_troop("t1", 36, ["t2"]) + _troop("t2", 11))
        self.assertEqual(1, len(self._run()))

    def test_every_collapsed_target_is_reported_not_just_the_first(self):
        # GetTroopXPTooltip only ever reads index 0, but the AI upgrader and OnXpChanged walk every
        # target, so a collapse at index 2 is still a real defect.
        self._write(_troop("t1", 51, ["t2", "t3"]) + _troop("t2", 51) + _troop("t3", 51))
        self.assertEqual(2, len(self._run()))

    def test_an_allowlisted_pair_is_exempt(self):
        self._write(_troop("lateral_src", 51, ["lateral_dst"]) + _troop("lateral_dst", 51))
        saved = ts.Validator._LATERAL_BY_DESIGN
        merged = dict(saved)
        merged[("lateral_src", "lateral_dst")] = "test exemption"
        ts.Validator._LATERAL_BY_DESIGN = merged
        try:
            self.assertEqual([], self._run())
        finally:
            ts.Validator._LATERAL_BY_DESIGN = saved

    def test_the_allowlist_is_directional(self):
        # An exemption for A->B must not silently exempt B->A.
        self._write(_troop("lateral_dst", 51, ["lateral_src"]) + _troop("lateral_src", 51))
        saved = ts.Validator._LATERAL_BY_DESIGN
        merged = dict(saved)
        merged[("lateral_src", "lateral_dst")] = "test exemption"
        ts.Validator._LATERAL_BY_DESIGN = merged
        try:
            self.assertEqual(1, len(self._run()))
        finally:
            ts.Validator._LATERAL_BY_DESIGN = saved

    def test_a_troop_with_no_level_is_skipped(self):
        # The engine defaults an absent level, but guessing which way would let the gate invent a
        # defect. Refuse to judge the edge instead. A tree where NOTHING has a level is a different
        # problem and UPGRADE_INDEX_EMPTY owns it.
        self._write(_troop("t1", 0, ["t2"], omit_level=True) + _troop("t2", 13))
        self.assertEqual([], self._run())

    def test_a_target_with_no_level_is_skipped(self):
        self._write(_troop("t1", 11, ["t2"]) + _troop("t2", 0, omit_level=True))
        self.assertEqual([], self._run())

    def test_an_unresolvable_target_is_left_to_broken_troop_ref(self):
        self._write(_troop("t1", 11, ["nowhere"]))
        self.assertEqual([], self._run())

    def test_a_commented_out_edge_is_not_counted(self):
        self._write(_troop("t1", 11, ["t2"]) + _troop("t2", 16)
                    + "  <!--" + NL + _troop("t3", 11, ["t4"]) + _troop("t4", 13)
                    + "  -->" + NL)
        self.assertEqual([], self._run())

    def test_the_multi_line_attribute_shape_is_parsed(self):
        # Every real troop file spreads attributes one per line. A matcher that only handled the
        # single-line form would read every troop as level-less and the gate would pass silently.
        multiline = NL.join([
            '  <NPCCharacter',
            '        id="t1"',
            '        default_group="Infantry"',
            '        level="11"',
            '        name="{=x}x"',
            '        occupation="Soldier"',
            '        culture="Culture.gondor">',
            '    <upgrade_targets>',
            '      <upgrade_target id="NPCCharacter.t2" />',
            '    </upgrade_targets>',
            '  </NPCCharacter>',
            '',
        ])
        self._write(multiline + _troop("t2", 13))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("t1", issues[0].entry_id)

    def test_a_decoy_level_suffixed_attribute_is_not_read_as_the_level(self):
        # The negative lookbehind is what stops `tier_level="99"` or `min_level="1"` being taken as
        # the troop's level. If it broke, this pair would misread and the collapse would be missed.
        decoy = NL.join([
            '  <NPCCharacter id="t1" tier_level="99" level="11" min_level="1"',
            '      name="{=x}x" occupation="Soldier" culture="Culture.gondor">',
            '    <upgrade_targets>',
            '      <upgrade_target id="NPCCharacter.t2" />',
            '    </upgrade_targets>',
            '  </NPCCharacter>',
            '',
        ])
        self._write(decoy + _troop("t2", 13))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertIn("without leaving tier 2", issues[0].message)

    def test_villager_files_outside_troops_are_covered(self):
        # villager_<culture> lives in characters/npcs_*.xml and upgrades into the tree's bottom
        # rung, which is exactly where one of the ten real laterals sits.
        self._write(_troop("v1", 1, ["t1"]) + _troop("t1", 1),
                    filename="characters/npcs_test.xml")
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("v1", issues[0].entry_id)


class AllowlistFreshnessTests(unittest.TestCase):
    """A dead exemption is worse than no exemption: it reads as a considered decision."""

    def setUp(self):
        if not os.path.isdir(os.path.join(SHIPPED_MODULEDATA, "troops")):
            self.skipTest("shipped ModuleData not present")
        self.validator = _validator(SHIPPED_MODULEDATA)

    def test_every_allowlisted_edge_still_exists_in_the_data(self):
        troops, _ = self.validator._upgrade_troop_index()
        missing = [
            "{0} -> {1}".format(src, dst)
            for (src, dst) in ts.Validator._LATERAL_BY_DESIGN
            if src not in troops or dst not in troops[src]["upgrades"]
        ]
        self.assertEqual(
            [], missing,
            "these _LATERAL_BY_DESIGN entries no longer name a real upgrade edge; a rename or a "
            "deletion left the exemption behind: " + ", ".join(missing))

    def test_every_allowlisted_edge_actually_collapses(self):
        # An entry that no longer collapses is dead weight, and it would keep the pair exempt if a
        # later level change reintroduced the collapse for an entirely different reason.
        troops, _ = self.validator._upgrade_troop_index()
        pointless = []
        for (src, dst) in ts.Validator._LATERAL_BY_DESIGN:
            if src not in troops or dst not in troops:
                continue
            if troops[src]["level"] is None or troops[dst]["level"] is None:
                continue
            src_tier = ts.Validator._troop_tier(troops[src]["level"])
            dst_tier = ts.Validator._troop_tier(troops[dst]["level"])
            if dst_tier > src_tier:
                pointless.append("{0} -> {1}".format(src, dst))
        self.assertEqual(
            [], pointless,
            "these _LATERAL_BY_DESIGN entries now cross a tier bracket on their own and the "
            "exemption should be deleted: " + ", ".join(pointless))

    def test_every_allowlist_entry_states_a_reason(self):
        blank = ["{0} -> {1}".format(s, d)
                 for (s, d), why in ts.Validator._LATERAL_BY_DESIGN.items()
                 if not (why or "").strip()]
        self.assertEqual([], blank)

    def test_the_shipped_data_is_clean(self):
        self.assertEqual([], self.validator._upgrade_tier_collapse())


class SharedIndexTests(unittest.TestCase):
    """The two upgrade checks share one memoised parse. That refactor could double-report."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="taom_shared_index_")
        os.makedirs(os.path.join(self.root, "troops"))
        os.makedirs(os.path.join(self.root, "characters"))
        # A troop declaring BOTH a skill_template and inline skills is what triggers
        # SKILL_TEMPLATE_SHADOWS_SKILLS, which is emitted from inside the shared index.
        _write_troops(
            self.root,
            _troop("shadowed", 11, ["t2"], skill_template="some_template") + _troop("t2", 16))

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def test_shadow_issue_is_emitted_exactly_once_across_both_checks(self):
        v = _validator(self.root)
        codes = [i.code for i in v._upgrade_skill_regressions() + v._upgrade_tier_collapse()]
        self.assertEqual(
            1, codes.count("SKILL_TEMPLATE_SHADOWS_SKILLS"),
            "the shared index emits this from one place; both checks reading it must not "
            "duplicate or drop it. Got: {0}".format(codes))

    def test_calling_the_skill_check_twice_does_not_duplicate_or_drop(self):
        # `issues = list(issues)` in _upgrade_skill_regressions is what makes this safe. Without
        # the copy, the method's own appends would accumulate into the cached list.
        v = _validator(self.root)
        first = [i.code for i in v._upgrade_skill_regressions()]
        second = [i.code for i in v._upgrade_skill_regressions()]
        self.assertEqual(first, second)
        self.assertEqual(1, first.count("SKILL_TEMPLATE_SHADOWS_SKILLS"))


class EmptyIndexGuardTests(unittest.TestCase):
    """A gate that silently checks nothing is worse than no gate."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="taom_empty_index_")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def _codes(self):
        return [i.code for i in _validator(self.root)._upgrade_skill_regressions()]

    def test_no_troop_files_at_all_fails_loud(self):
        os.makedirs(os.path.join(self.root, "troops"))
        self.assertIn("UPGRADE_INDEX_EMPTY", self._codes())

    def test_a_renamed_troops_directory_fails_loud(self):
        # The globs are literal and non-recursive, so this is the realistic way the gates go blind.
        os.makedirs(os.path.join(self.root, "troop_definitions"))
        _write_troops(self.root, _troop("t1", 11, ["t2"]) + _troop("t2", 13),
                      filename="troop_definitions/troops_test.xml")
        self.assertIn("UPGRADE_INDEX_EMPTY", self._codes())

    def test_troops_with_no_levels_at_all_fails_loud(self):
        os.makedirs(os.path.join(self.root, "troops"))
        _write_troops(self.root, _troop("a", 0, ["b"], omit_level=True)
                      + _troop("b", 0, omit_level=True))
        self.assertIn("UPGRADE_INDEX_EMPTY", self._codes())

    def test_a_populated_tree_does_not_trip_the_guard(self):
        os.makedirs(os.path.join(self.root, "troops"))
        _write_troops(self.root, _troop("t1", 11, ["t2"]) + _troop("t2", 16))
        self.assertNotIn("UPGRADE_INDEX_EMPTY", self._codes())

    def test_the_shipped_tree_does_not_trip_the_guard(self):
        if not os.path.isdir(os.path.join(SHIPPED_MODULEDATA, "troops")):
            self.skipTest("shipped ModuleData not present")
        codes = [i.code for i in _validator(SHIPPED_MODULEDATA)._upgrade_skill_regressions()]
        self.assertNotIn("UPGRADE_INDEX_EMPTY", codes)


class WiringTests(unittest.TestCase):
    def test_both_checks_run_as_part_of_the_validator(self):
        import inspect
        source = inspect.getsource(ts.Validator.run)
        self.assertIn("_upgrade_tier_collapse", source)
        self.assertIn("_upgrade_skill_regressions", source)


if __name__ == "__main__":
    unittest.main()
