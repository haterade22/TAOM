"""Tests for tools/lint_handbook.py, the deterministic handbook chapter linter."""
import os
import sys
import tempfile
import textwrap
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

import lint_handbook  # noqa: E402


class LintHandbookTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = self.tmp.name
        self.docs = os.path.join(self.root, 'docs', 'modding')
        os.makedirs(self.docs)
        os.makedirs(os.path.join(self.root, 'docs', 'features'))
        with open(os.path.join(self.root, 'docs', 'features', 'troops.md'), 'w', encoding='utf-8') as f:
            f.write('# Troops\n')

    def tearDown(self):
        self.tmp.cleanup()

    def write(self, name, body):
        path = os.path.join(self.docs, name)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(textwrap.dedent(body))
        return path

    def lint(self, name):
        return lint_handbook.lint_file(os.path.join(self.docs, name), repo_root=self.root)

    def test_clean_file_chapter_has_no_findings(self):
        self.write('troops.md', '''\
            # Troops

            Intro.

            ## What this file is
            ## Where it lives and how it is registered
            ## Attributes
            ## Child elements
            ## Worked example
            ## Recipes: Add / Modify / Delete
            ### Add
            1. Do it.
            Check: `python tools/validate_moduledata.py`
            Takes effect: full game restart
            Code: No code changes needed
            ### Modify
            1. Do it.
            Check: `python tools/validate_moduledata.py`
            Takes effect: live
            Code: No code changes needed
            ### Delete
            1. Do it.
            Check: `python tools/validate_moduledata.py`
            Takes effect: new campaign only
            Code: No code changes needed
            ## Gotchas: what fails silently and what crashes
            ## Numbers in this chapter
            ## Read next
            [Troops feature](../features/troops.md)
            ''')
        self.assertEqual(self.lint('troops.md'), [])

    def test_dashes_and_drive_paths_are_reported(self):
        self.write('x.md', '# X\n\nA sentence \u2014 with a dash and E:\\repos path.\n')
        codes = {f.code for f in self.lint('x.md')}
        self.assertIn('LONG_DASH', codes)
        self.assertIn('DRIVE_PATH', codes)

    def test_dead_relative_link_is_reported(self):
        self.write('x.md', '# X\n\n[gone](../features/nope.md) and [ok](../features/troops.md)\n')
        findings = self.lint('x.md')
        self.assertEqual([f.code for f in findings], ['DEAD_LINK'])
        self.assertIn('nope.md', findings[0].message)

    def test_missing_skeleton_heading_reported_for_file_chapter(self):
        self.write('troops.md', '# Troops\n\n## What this file is\n## Attributes\n')
        codes = [f.code for f in self.lint('troops.md')]
        self.assertIn('MISSING_HEADING', codes)

    def test_recipe_without_trailer_is_reported(self):
        self.write('x.md', '# X\n\n### Add\n1. step\n\n### Modify\n1. step\nCheck: x\nTakes effect: live\nCode: No code changes needed\n')
        findings = self.lint('x.md')
        self.assertEqual(len([f for f in findings if f.code == 'RECIPE_TRAILER']), 1)
        self.assertIn('Add', findings[0].message)

    def test_invented_takes_effect_value_is_reported(self):
        self.write('x.md', '# X\n\n### Add\n1. step\nCheck: c\nTakes effect: whenever\nCode: No code changes needed\n')
        findings = [f for f in self.lint('x.md') if f.code == 'TAKES_EFFECT_VALUE']
        self.assertEqual(len(findings), 1)
        self.assertIn('whenever', findings[0].message)

    def test_allowed_takes_effect_value_with_a_qualifier_is_clean(self):
        self.write('x.md', '# X\n\n### Add\n1. step\nCheck: c\n'
                           'Takes effect: new campaign only, because Clan.Color is [SaveableProperty]\n'
                           'Code: No code changes needed\n')
        self.assertEqual([f for f in self.lint('x.md') if f.code == 'TAKES_EFFECT_VALUE'], [])

    def test_ai_vocabulary_is_reported(self):
        self.write('x.md', '# X\n\nWe delve into the landscape in order to explain.\n')
        codes = [f.code for f in self.lint('x.md')]
        self.assertEqual(codes.count('AI_TELL'), 1)

    def test_live_path_without_reinstall_warning_is_reported(self):
        self.write('x.md', '# X\n\nEdit `LOTRLOME_Armory/ModuleData/skins.xml`.\n')
        codes = [f.code for f in self.lint('x.md')]
        self.assertIn('LIVE_PATH_NO_WARNING', codes)

    def test_live_path_with_warning_is_clean(self):
        self.write('x.md', '# X\n\nEdit `LOTRLOME_Armory/ModuleData/skins.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits.\n')
        codes = [f.code for f in self.lint('x.md')]
        self.assertNotIn('LIVE_PATH_NO_WARNING', codes)

    def test_bom_is_reported_but_crlf_is_not(self):
        """core.autocrlf=true writes CRLF into the worktree on checkout, by design.

        Flagging it failed every chapter the moment it was committed (2026-09-05). Git stores
        LF regardless, which is what actually matters, so only the BOM is a finding.
        """
        path = os.path.join(self.docs, 'x.md')
        with open(path, 'wb') as f:
            f.write(b'\xef\xbb\xbf# X\r\n\r\ntext\r\n')
        codes = {f.code for f in lint_handbook.lint_file(path, repo_root=self.root)}
        self.assertIn('BOM', codes)
        self.assertNotIn('CRLF', codes)


if __name__ == '__main__':
    unittest.main()
