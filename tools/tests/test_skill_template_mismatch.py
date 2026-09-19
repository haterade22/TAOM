"""Unit tests for SKILL_TEMPLATE_MISMATCH (validate_moduledata.skill_template_mismatch_issues, #626).

Run:  python -m pytest tools/tests/test_skill_template_mismatch.py -q

The rule: a character may carry a skill_template AND an inline <skills> block, but every inline value
must equal the template's. Since Bannerlord v1.5.2 BasicCharacterObject.Deserialize copies the
template and lays the inline rows over it, so a differing row silently changes the character while
every tool that reads the SkillSet (the source of truth, tools/sync_lord_inline_skills.py) sees the
template's number. The old gate, SKILL_TEMPLATE_SHADOWS_SKILLS, rested on v1.4.8, which discarded the
inline block, and refused any character declaring both.
"""
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

import validate_moduledata as vm  # noqa: E402

VANILLA_SETS = """<?xml version="1.0" encoding="utf-8"?>
<SkillSets>
  <SkillSet id="spc_x_skills_rookie">
    <skill id="OneHanded" value="50" />
    <skill id="Bow" value="40" />
  </SkillSet>
</SkillSets>
"""

TAOM_SETS = """<?xml version="1.0" encoding="utf-8"?>
<SkillSets>
  <SkillSet id="taom_a_skills">
    <skill id="OneHanded" value="80" />
    <skill id="Bow" value="60" />
  </SkillSet>
</SkillSets>
"""


def npc(char_id, template, rows):
    skills = "".join(f'\n      <skill id="{s}" value="{v}" />' for s, v in rows.items())
    tmpl = f' skill_template="SkillSet.{template}"' if template else ""
    return (f'  <NPCCharacter id="{char_id}" level="21" occupation="Lord"{tmpl}>\n'
            f'    <skills>{skills}\n    </skills>\n  </NPCCharacter>\n')


def doc(body):
    return f'<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n{body}</NPCCharacters>\n'


class SkillTemplateMismatchTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="taom_tmpl_"))
        self.game = self.root / "game"
        sandbox = self.game / "Modules" / "SandBox" / "ModuleData"
        sandbox.mkdir(parents=True)
        (sandbox / "sandbox_skill_sets.xml").write_text(VANILLA_SETS, encoding="utf-8")
        self.md = self.root / "ModuleData"
        (self.md / "characters").mkdir(parents=True)
        (self.md / "troops").mkdir()
        (self.md / "taom_lord_skill_sets.xml").write_text(TAOM_SETS, encoding="utf-8")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def _lords(self, body):
        (self.md / "characters" / "lords.xml").write_text(doc(body), encoding="utf-8")

    def _run(self):
        return vm.skill_template_mismatch_issues(self.game, self.md)

    def test_matching_rows_pass_for_a_repo_and_a_vanilla_template(self):
        self._lords(npc("lord_ok", "taom_a_skills", {"OneHanded": 80, "Bow": 60})
                    + npc("lord_vanilla", "spc_x_skills_rookie", {"OneHanded": 50, "Bow": 40})
                    + npc("lord_empty", "taom_a_skills", {}))
        self.assertEqual([], self._run())

    def test_a_row_that_differs_from_its_template_is_an_error(self):
        self._lords(npc("lord_ok", "taom_a_skills", {"OneHanded": 80, "Bow": 60})
                    + npc("lord_drift", "taom_a_skills", {"OneHanded": 90, "Bow": 60}))
        issues = self._run()
        self.assertEqual(["lord_drift"], [i.entry_id for i in issues])
        i = issues[0]
        self.assertEqual(("SKILL_TEMPLATE_MISMATCH", vm.ts.Severity.ERROR), (i.code, i.severity))
        self.assertEqual("characters/lords.xml", i.file)
        self.assertEqual(9, i.line)                                   # the character's opening tag
        self.assertIn("OneHanded 90", i.message)
        self.assertIn("80", i.message)
        self.assertIn("sync_lord_inline_skills.py --apply", i.message)

    def test_a_template_that_resolves_nowhere_is_an_error_when_it_has_rows(self):
        self._lords(npc("lord_typo", "taom_nope_skills", {"Bow": 60}) + npc("lord_typo_empty", "taom_nope_skills", {}))
        issues = self._run()
        self.assertEqual(["lord_typo"], [i.entry_id for i in issues])
        # The sync tool leaves an unresolved template alone, so its --apply is not the repair.
        self.assertIn("SkillSet.taom_nope_skills", issues[0].message)
        self.assertNotIn("--apply", issues[0].message)

    def test_two_drifted_rows_on_one_character_are_one_issue(self):
        self._lords(npc("lord_drift", "taom_a_skills", {"OneHanded": 90, "Bow": 61}))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertIn("OneHanded 90 -> 80", issues[0].message)
        self.assertIn("Bow 61 -> 60", issues[0].message)

    def test_finding_no_templated_character_at_all_is_one_error_not_a_pass(self):
        # The C# twin asserts checkedBlocks > 0; a moved folder or a regex that matches nothing
        # must not read as agreement.
        self._lords(npc("lord_plain", None, {"Bow": 60}))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("", issues[0].entry_id)

    def test_a_file_that_is_not_utf8_is_a_named_error_not_a_crash(self):
        self._lords(npc("lord_ok", "taom_a_skills", {"OneHanded": 80}))
        (self.md / "characters" / "npcs_bad.xml").write_bytes(b"<NPCCharacters><!-- caf\xe9 --></NPCCharacters>")
        issues = self._run()
        self.assertEqual(["characters/npcs_bad.xml"], [i.file for i in issues])
        self.assertEqual(0, issues[0].line)
        self.assertIn("UTF-8", issues[0].message)

    def test_a_troop_declaring_both_is_fine_when_they_match(self):
        # SKILL_TEMPLATE_SHADOWS_SKILLS refused this on the 1.4.8 premise; on 1.5.3 it is harmless.
        (self.md / "troops" / "troops_x.xml").write_text(
            doc(npc("t", "spc_x_skills_rookie", {"OneHanded": 50})), encoding="utf-8")
        self._lords("")
        self.assertEqual([], self._run())

    def test_a_commented_out_character_is_not_judged(self):
        self._lords("  <!--\n" + npc("lord_old", "taom_a_skills", {"OneHanded": 1}) + "  -->\n"
                    + npc("lord_ok", "taom_a_skills", {"OneHanded": 80}))
        self.assertEqual([], self._run())

    def test_the_stylesheet_literal_block_is_judged_too(self):
        self._lords("")
        (self.md / "lords.xslt").write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
            '  <xsl:template match="NPCCharacter[@id=\'lord_1_15\']">\n    <xsl:copy>\n'
            '      <xsl:attribute name="skill_template">SkillSet.taom_a_skills</xsl:attribute>\n'
            '      <skills>\n        <skill id="Bow" value="99" />\n      </skills>\n'
            '    </xsl:copy>\n  </xsl:template>\n</xsl:stylesheet>\n', encoding="utf-8")
        issues = self._run()
        self.assertEqual(["lord_1_15"], [i.entry_id for i in issues])
        self.assertEqual("lords.xslt", issues[0].file)

    def test_no_skill_sets_at_all_is_one_error_not_a_pass(self):
        (self.md / "taom_lord_skill_sets.xml").unlink()
        shutil.rmtree(self.game / "Modules")
        self._lords(npc("lord_ok", "taom_a_skills", {"OneHanded": 80}))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("", issues[0].entry_id)
        self.assertIn("no SkillSet", issues[0].message)


if __name__ == "__main__":
    unittest.main()
