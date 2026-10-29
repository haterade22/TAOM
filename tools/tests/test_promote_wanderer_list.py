#!/usr/bin/env python3
"""A promoted culture lists every wanderer it clones (promote_borrowed_cultures.py).

The Faction screen's Wanderer tab reads the culture's `notable_templates`, so a cloned
`spc_wanderer_<c>_N` the list omits exists in the world and never shows there. South Rhovanion was
cut from Rohan, whose own list misses rohan_9 (#762), so its clone inherited the gap.

Run:  python -m unittest tools.tests.test_promote_wanderer_list
"""
import os
import re
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import promote_borrowed_cultures as promo  # noqa: E402

BLOCK = (
    '  <Culture\n    id="zed">\n'
    '    <notable_templates>\n'
    '      <template name="NPCCharacter.spc_wanderer_zed_0" />\n'
    '      <template name="NPCCharacter.spc_wanderer_zed_1" />\n'
    '      <template name="NPCCharacter.spc_notable_zed_0" />\n'
    '    </notable_templates>\n'
    '  </Culture>\n'
)


class ListUnlistedWandererTests(unittest.TestCase):
    def test_a_missing_wanderer_is_appended_after_the_last_listed_one(self):
        out = promo.list_unlisted_wanderers(BLOCK, "zed", ["spc_wanderer_zed_0", "spc_wanderer_zed_1", "spc_wanderer_zed_2"])
        lines = out.splitlines()
        i = lines.index('      <template name="NPCCharacter.spc_wanderer_zed_2" />')
        self.assertEqual(lines[i - 1], '      <template name="NPCCharacter.spc_wanderer_zed_1" />')
        self.assertEqual(lines[i + 1], '      <template name="NPCCharacter.spc_notable_zed_0" />')

    def test_a_complete_list_is_unchanged(self):
        self.assertEqual(promo.list_unlisted_wanderers(BLOCK, "zed", ["spc_wanderer_zed_0", "spc_wanderer_zed_1"]), BLOCK)

    def test_a_block_with_no_wanderer_row_is_refused(self):
        bare = BLOCK.replace('      <template name="NPCCharacter.spc_wanderer_zed_0" />\n', "") \
                    .replace('      <template name="NPCCharacter.spc_wanderer_zed_1" />\n', "")
        with self.assertRaises(SystemExit):
            promo.list_unlisted_wanderers(bare, "zed", ["spc_wanderer_zed_0"])


class ShippedCulturesListTheirWanderersTests(unittest.TestCase):
    def test_every_target_lists_every_wanderer_the_wanderers_file_defines(self):
        spc = (promo.MD / "taom_spcultures.xml").read_text(encoding="utf-8-sig")
        wanderers = (promo.MD / "taom_wanderers.xml").read_text(encoding="utf-8-sig")
        checked = 0
        for target in sorted(promo.TARGETS):
            block = re.search(r'<Culture\b[^>]*\bid="%s".*?</Culture>' % target, spc, re.DOTALL)
            defined = set(re.findall(r'<NPCCharacter\b[^>]*\bid="(spc_wanderer_%s_\d+)"' % target, wanderers))
            if not block or not defined:
                continue
            checked += 1
            listed = set(re.findall(r'name="NPCCharacter\.(spc_wanderer_%s_\d+)"' % target, block.group(0)))
            with self.subTest(target=target):
                self.assertEqual(sorted(defined - listed), [])
        self.assertGreater(checked, 0, "no target had both a culture block and wanderers: the check tested nothing")


if __name__ == "__main__":
    unittest.main()
