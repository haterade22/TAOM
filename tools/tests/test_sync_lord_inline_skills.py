"""Unit tests for tools/sync_lord_inline_skills.py.

Run:  python -m pytest tools/tests/test_sync_lord_inline_skills.py -q
  or:  python -m unittest discover -s tools/tests -p "test_*.py"

The engine facts these pin: since Bannerlord v1.5.2 an NPCCharacter's inline <skills> block is
applied on top of a copy of its skill_template SkillSet, so a listed skill's inline value wins; a
skill the block omits keeps the template value; a skill the set does not define reads as 0.
"""
import os
import sys
import tempfile
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

import sync_lord_inline_skills as sync  # noqa: E402

BOM = b"\xef\xbb\xbf"

SETS = {
    "spc_matriarch_skills_rookie": {"OneHanded": 80, "Riding": 100, "Leadership": 140},
    "taom_test_set": {"Bow": 120},
}


def npc(char_id, template, skills_inner, nl="\n"):
    lines = [
        f'    <NPCCharacter id="{char_id}" name="{{=x}}Test" age="30"',
        f'        skill_template="{template}">' if template else '        default_group="Infantry">',
        "        <face><BodyProperties version=\"4\" /></face>",
        "        <skills>",
        skills_inner,
        "        </skills>",
        "    </NPCCharacter>",
    ]
    return nl.join(lines)


def doc(body, nl="\n"):
    return nl.join(['<?xml version="1.0" encoding="utf-8"?>', "<NPCCharacters>", body, "</NPCCharacters>", ""])


class SyncFileTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="taom_sync_")
        self.path = os.path.join(self.dir, "lords.xml")

    def write(self, text, bom=False):
        with open(self.path, "wb") as f:
            f.write((BOM if bom else b"") + text.encode("utf-8"))

    def read_bytes(self):
        with open(self.path, "rb") as f:
            return f.read()

    def test_drifted_block_is_rewritten_to_the_skillset_values(self):
        inner = '            <skill id="OneHanded" value="18" />\n            <skill id="Riding" value="20" />'
        self.write(doc(npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner)))

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 2))
        text = self.read_bytes().decode("utf-8")
        self.assertIn('<skill id="OneHanded" value="80" />', text)
        self.assertIn('<skill id="Riding" value="100" />', text)
        first = scan.drifts[0]
        self.assertEqual((first.char_id, first.skill, first.inline, first.expected), ("lord_x", "OneHanded", "18", 80))
        self.assertEqual(first.describe(), "OneHanded 18 -> 80 (spc_matriarch_skills_rookie)")

    def test_report_mode_never_writes(self):
        inner = '            <skill id="OneHanded" value="18" />'
        original = doc(npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner))
        self.write(original)

        scan = sync.sync_file(self.path, SETS)                     # report mode is the default

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 1))
        self.assertEqual(self.read_bytes().decode("utf-8"), original)

    def test_bom_and_crlf_survive_a_rewrite(self):
        inner = '            <skill id="OneHanded" value="18" />'
        self.write(doc(npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner, nl="\r\n"), nl="\r\n"), bom=True)

        sync.sync_file(self.path, SETS, apply=True)

        raw = self.read_bytes()
        self.assertTrue(raw.startswith(BOM), "BOM dropped")
        self.assertNotIn(b"\xef\xbb\xbf", raw[3:], "a second BOM appeared")
        self.assertNotIn(b"\n", raw.replace(b"\r\n", b""), "a bare LF appeared in a CRLF file")
        self.assertIn(b'value="80"', raw)

    def test_skill_the_set_does_not_define_becomes_zero(self):
        inner = '            <skill id="Bow" value="120" />\n            <skill id="Crossbow" value="55" />'
        self.write(doc(npc("lord_x", "SkillSet.taom_test_set", inner)))

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual(len(scan.drifts), 1)
        text = self.read_bytes().decode("utf-8")
        self.assertIn('<skill id="Bow" value="120" />', text)
        self.assertIn('<skill id="Crossbow" value="0" />', text)

    def test_unresolved_template_is_reported_and_left_alone(self):
        inner = '            <skill id="OneHanded" value="18" />'
        original = doc(npc("lord_x", "SkillSet.does_not_exist", inner))
        self.write(original)

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 0))
        self.assertEqual([(c, t) for c, _, t in scan.unresolved], [("lord_x", "SkillSet.does_not_exist")])
        self.assertEqual(self.read_bytes().decode("utf-8"), original)

    def test_inline_only_and_empty_blocks_are_not_touched(self):
        inline_only = npc("lord_inline", None, '            <skill id="OneHanded" value="18" />')
        empty = npc("lord_empty", "SkillSet.spc_matriarch_skills_rookie", "")
        original = doc(inline_only + "\n" + empty)
        self.write(original)

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (0, 0))
        self.assertEqual(self.read_bytes().decode("utf-8"), original)

    def test_second_apply_changes_nothing(self):
        inner = '            <skill id="OneHanded" value="18" />'
        self.write(doc(npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner)))
        sync.sync_file(self.path, SETS, apply=True)
        after_first = self.read_bytes()

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 0))
        self.assertEqual(self.read_bytes(), after_first)

    # Comments (#626 review): the engine never loads text inside <!-- -->, so neither may the tool.
    def test_a_comment_naming_npccharacter_does_not_swallow_the_next_character(self):
        inner = '            <skill id="OneHanded" value="18" />'
        self.write(doc('    <!-- see <NPCCharacter id="ref"> below -->\n' + npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner)))

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 1))
        text = self.read_bytes().decode("utf-8")
        self.assertIn('<skill id="OneHanded" value="80" />', text)
        self.assertIn('<!-- see <NPCCharacter id="ref"> below -->', text)

    def test_a_commented_out_skills_block_is_neither_judged_nor_rewritten(self):
        character = (
            '    <NPCCharacter id="lord_x" skill_template="SkillSet.spc_matriarch_skills_rookie">\n'
            '        <!-- <skills><skill id="OneHanded" value="1" /></skills> -->\n'
            '        <skills>\n            <skill id="OneHanded" value="5" />\n        </skills>\n'
            '    </NPCCharacter>')
        self.write(doc(character))

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual([(d.skill, d.inline) for d in scan.drifts], [("OneHanded", "5")])
        text = self.read_bytes().decode("utf-8")
        self.assertIn('<!-- <skills><skill id="OneHanded" value="1" /></skills> -->', text)
        self.assertIn('<skill id="OneHanded" value="80" />', text)

    def test_a_commented_out_row_is_not_drift(self):
        inner = '            <skill id="OneHanded" value="80" />\n            <!-- <skill id="Riding" value="1" /> -->'
        original = doc(npc("lord_x", "SkillSet.spc_matriarch_skills_rookie", inner))
        self.write(original)

        scan = sync.sync_file(self.path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 0))
        self.assertEqual(self.read_bytes().decode("utf-8"), original)

    def test_each_finding_carries_its_own_characters_line(self):
        inner = '            <skill id="OneHanded" value="18" />'
        self.write(doc(npc("lord_a", "SkillSet.spc_matriarch_skills_rookie", inner) + "\n"
                       + npc("lord_b", "SkillSet.spc_matriarch_skills_rookie", inner)))

        scan = sync.sync_file(self.path, SETS)

        self.assertEqual([(d.char_id, d.line) for d in scan.drifts], [("lord_a", 3), ("lord_b", 10)])


class XsltTemplateTests(unittest.TestCase):
    """lords.xslt writes vanilla-id lords through xsl:template blocks; the same rule applies."""

    XSLT = (
        '<?xml version="1.0" encoding="utf-8"?>\r\n'
        '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\r\n'
        '    <xsl:template match="NPCCharacter[@id=\'lord_1_15\']">\r\n'
        '        <xsl:copy>\r\n'
        '            <xsl:attribute name="id">lord_1_15</xsl:attribute>\r\n'
        '            <xsl:attribute name="skill_template">SkillSet.taom_test_set</xsl:attribute>\r\n'
        '            <skills>\r\n'
        '                <skill id="Bow" value="90" />\r\n'
        '            </skills>\r\n'
        '        </xsl:copy>\r\n'
        '    </xsl:template>\r\n'
        '    <xsl:template match="NPCCharacter[@id=\'lord_1_16\']">\r\n'
        '        <xsl:copy>\r\n'
        '            <skills>\r\n'
        '                <skill id="Bow" value="90" />\r\n'
        '            </skills>\r\n'
        '        </xsl:copy>\r\n'
        '    </xsl:template>\r\n'
        '</xsl:stylesheet>\r\n'
    )

    def test_template_with_skill_template_attribute_is_synced_and_one_without_is_left_alone(self):
        d = tempfile.mkdtemp(prefix="taom_xslt_")
        path = os.path.join(d, "lords.xslt")
        with open(path, "wb") as f:
            f.write(BOM + self.XSLT.encode("utf-8"))

        scan = sync.sync_file(path, SETS, apply=True)

        self.assertEqual((scan.checked, len(scan.drifts)), (1, 1))
        raw = open(path, "rb").read()
        self.assertTrue(raw.startswith(BOM))
        text = raw[3:].decode("utf-8")
        self.assertEqual(text.count('<skill id="Bow" value="120" />'), 1, text)
        self.assertEqual(text.count('<skill id="Bow" value="90" />'), 1, "the template without skill_template must keep its value")
        self.assertNotIn("\n", text.replace("\r\n", ""), "CRLF endings must survive")
        self.assertEqual([(d.char_id, d.describe()) for d in scan.drifts], [("lord_1_15", "Bow 90 -> 120 (taom_test_set)")])


class LoadSkillSetsMergeTests(unittest.TestCase):
    """The engine merges same-id SkillSets across modules per skill (MBObjectManager.MergeElements)."""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="taom_sets_")

    def write(self, name, xml):
        path = os.path.join(self.dir, name)
        with open(path, "wb") as f:
            f.write(xml.encode("utf-8"))
        return path

    def test_later_file_overrides_listed_skills_and_keeps_omitted_ones(self):
        earlier = self.write("a_skill_sets.xml", '<SkillSets><SkillSet id="shared"><skill id="OneHanded" value="10" /><skill id="Bow" value="30" /></SkillSet></SkillSets>')
        later = self.write("b_skill_sets.xml", '<SkillSets><SkillSet id="shared"><skill id="OneHanded" value="20" /></SkillSet></SkillSets>')

        sets = sync.load_skill_sets_from([earlier, later])

        self.assertEqual(sets["shared"], {"OneHanded": 20, "Bow": 30})

    def test_replace_while_merging_drops_the_earlier_skills(self):
        earlier = self.write("a_skill_sets.xml", '<SkillSets><SkillSet id="shared"><skill id="OneHanded" value="10" /><skill id="Bow" value="30" /></SkillSet></SkillSets>')
        later = self.write("b_skill_sets.xml", '<SkillSets><SkillSet id="shared" _replaceWhileMerging="true"><skill id="OneHanded" value="20" /></SkillSet></SkillSets>')

        sets = sync.load_skill_sets_from([earlier, later])

        self.assertEqual(sets["shared"], {"OneHanded": 20})

    def test_a_commented_out_row_is_not_loaded(self):
        # Vanilla SandBox and SandBoxCore carry commented-out rows inside SkillSet bodies; the
        # engine reads none of them (#626 review).
        path = self.write("a_skill_sets.xml", '<SkillSets><SkillSet id="s"><skill id="OneHanded" value="10" />'
                                             '<!--<skill id="Shield" value="60" />--></SkillSet></SkillSets>')

        self.assertEqual(sync.load_skill_sets_from([path])["s"], {"OneHanded": 10})

    def test_vanilla_files_come_before_the_repo_files(self):
        files = sync.skill_set_files(os.path.join(self.dir, "no-such-install"))
        # Without an install only the repo files remain, and they are the tail of the list by construction.
        self.assertTrue(all(sync.MODULE_DATA in f for f in files), files)

    def test_require_vanilla_refuses_an_install_root_with_no_vanilla_skillset(self):
        # TAOM's own SkillSet files always load, so a non-empty result cannot prove the install
        # was read; the gate and the two writers refuse a root with no vanilla file (#626 review).
        repo = os.path.join(self.dir, "md")
        os.makedirs(repo)
        self.write(os.path.join("md", "taom_x_skill_sets.xml"),
                   '<SkillSets><SkillSet id="taom"><skill id="Bow" value="10" /></SkillSet></SkillSets>')
        inst = os.path.join(self.dir, "inst")
        os.makedirs(os.path.join(inst, "Modules"))
        self.assertEqual(sync.load_skill_sets(inst, repo), {"taom": {"Bow": 10}})   # the default keeps its old reading
        with self.assertRaises(ValueError) as ctx:
            sync.load_skill_sets(inst, repo, require_vanilla=True)
        self.assertIn(inst, str(ctx.exception))
        native = os.path.join(inst, "Modules", "Native", "ModuleData")
        os.makedirs(native)
        with open(os.path.join(native, "skill_sets.xml"), "w", encoding="utf-8") as f:
            f.write('<SkillSets><SkillSet id="v"><skill id="Bow" value="20" /></SkillSet></SkillSets>')
        self.assertEqual(sync.load_skill_sets(inst, repo, require_vanilla=True),
                         {"v": {"Bow": 20}, "taom": {"Bow": 10}})

    def test_require_vanilla_refuses_a_root_whose_vanilla_files_define_no_skillset(self):
        # The installed Native ships native_skill_sets.xml as an empty stub; a root with only that
        # file has a vanilla file but no vanilla SkillSet, so every vanilla template would read as
        # empty (#626 review, round 1).
        repo = os.path.join(self.dir, "md")
        os.makedirs(repo)
        self.write(os.path.join("md", "taom_x_skill_sets.xml"),
                   '<SkillSets><SkillSet id="taom"><skill id="Bow" value="10" /></SkillSet></SkillSets>')
        native = os.path.join(self.dir, "inst", "Modules", "Native", "ModuleData")
        os.makedirs(native)
        with open(os.path.join(native, "native_skill_sets.xml"), "wb") as f:
            f.write(b'\xef\xbb\xbf<?xml version="1.0" encoding="utf-8"?>\r\n<SkillSets></SkillSets>')
        with self.assertRaises(ValueError) as ctx:
            sync.load_skill_sets(os.path.join(self.dir, "inst"), repo, require_vanilla=True)
        self.assertIn("define no SkillSet", str(ctx.exception))


class EffectiveSkillsTests(unittest.TestCase):
    """effective_skills is the one place the tools resolve a skill_template (#626): the SkillSet
    copied, then each inline row laid over its one skill (BasicCharacterObject.Deserialize)."""

    def setUp(self):
        self.sets = {"t": {"Polearm": 70, "Bow": 30}}

    def test_no_template_is_a_copy_of_the_inline_rows(self):
        inline = {"Bow": 5}
        result = sync.effective_skills(None, inline, None)
        self.assertEqual(result, {"Bow": 5})
        result["Bow"] = 99
        self.assertEqual(inline, {"Bow": 5})

    def test_inline_rows_are_laid_over_the_template(self):
        self.assertEqual(sync.effective_skills("SkillSet.t", {"Bow": 90}, self.sets),
                         {"Polearm": 70, "Bow": 90})

    def test_a_template_without_inline_rows_is_the_skillset(self):
        result = sync.effective_skills("SkillSet.t", {}, self.sets)
        self.assertEqual(result, {"Polearm": 70, "Bow": 30})
        result["Polearm"] = 1
        self.assertEqual(self.sets["t"], {"Polearm": 70, "Bow": 30})

    def test_a_template_that_names_no_skillset_reads_as_an_empty_one(self):
        self.assertEqual(sync.effective_skills("SkillSet.missing", {"Bow": 40}, self.sets), {"Bow": 40})

    def test_a_template_without_skillsets_is_refused(self):
        with self.assertRaises(ValueError) as ctx:
            sync.effective_skills("SkillSet.t", {}, None)
        self.assertIn("SkillSet.t", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
