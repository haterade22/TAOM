#!/usr/bin/env python3
"""apply_culture_skills_traits.py --check: drift in the lord skill sets is reported, a reorder is not.

Run:  python -m unittest tools.tests.test_apply_culture_skills_traits_check
"""
import contextlib
import io
import os
import re
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import apply_culture_skills_traits as acst  # noqa: E402


class CheckSkillSetsTests(unittest.TestCase):
    def _check(self, text):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "sets.xml"
            path.write_text(text, encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                return acst.check_skill_sets(path)

    def test_the_generator_output_passes(self):
        self.assertEqual(self._check(acst.build_skill_sets_xml()), 0)

    def test_reordered_sets_pass(self):
        text = acst.build_skill_sets_xml()
        blocks = re.findall(r"  <SkillSet id=.*?</SkillSet>\n", text, flags=re.S)
        self.assertGreater(len(blocks), 10)
        head = text[:text.index(blocks[0])]
        self.assertEqual(self._check(head + "".join(reversed(blocks)) + "</SkillSets>\n"), 0)

    def test_a_changed_value_fails(self):
        text = acst.build_skill_sets_xml()
        changed = re.sub(r'(<skill id="OneHanded" value=")(\d+)', lambda m: m.group(1) + str(int(m.group(2)) + 1),
                         text, count=1)
        self.assertNotEqual(changed, text)
        self.assertEqual(self._check(changed), 1)

    def test_a_deleted_set_fails(self):
        text = acst.build_skill_sets_xml()
        self.assertEqual(self._check(re.sub(r"  <SkillSet id=.*?</SkillSet>\n", "", text, count=1, flags=re.S)), 1)


if __name__ == "__main__":
    unittest.main()
