#!/usr/bin/env python3
"""Unit tests for upsert_party_template() in generate_clan_heraldry.py.

Operation C replaces a whole <MBPartyTemplate> from the spec roster, so a spec that has fallen
behind the live file is authority to delete whatever the live file gained since. The guard added on
2026-08-17 refused only when the spec would DROP a troop id. That is one of the two ways a spec falls
behind; the other is a party-size retarget (`rebalance_party_template_maxes.py`), which changes no
id and every max_value. On 2026-09-13 (#584) the Black Numenorean stacks left 13 Mordor clan
templates, so for those the live id set and the stale spec's id set became identical, and the
id-only guard stopped seeing that `clan_heraldry/mordor.json` still carries the pre-retarget counts
(max 2 to 4 against a live 23 to 44). These tests pin both halves of "behind".
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import generate_clan_heraldry as gh  # noqa: E402


def live_file(stacks):
    body = "\n".join(
        '\t\t\t<PartyTemplateStack min_value="%d" max_value="%d" troop="NPCCharacter.%s" />' % (mn, mx, t)
        for t, mn, mx in stacks)
    return ('<partyTemplates>\n'
            '\t<MBPartyTemplate id="kingdom_hero_party_x_template">\n'
            '\t\t<stacks>\n' + body + '\n'
            '\t\t</stacks>\n'
            '\t</MBPartyTemplate>\n'
            '</partyTemplates>')


def roster(stacks):
    return [{"troop": t, "min": mn, "max": mx} for t, mn, mx in stacks]


class UpsertRefusesASpecBehindTheLiveFile(unittest.TestCase):

    def test_dropping_a_live_troop_refuses(self):
        text = live_file([("a", 1, 10), ("b", 0, 5)])
        with self.assertRaises(gh.TemplateWouldShrink) as ctx:
            gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 1, 10)]))
        self.assertIn("b", str(ctx.exception))

    def test_same_ids_with_a_lower_max_sum_refuses(self):
        # The post-#584 shape: every id matches, the spec predates a retarget.
        text = live_file([("a", 1, 30), ("b", 0, 20)])
        with self.assertRaises(gh.TemplateWouldShrink) as ctx:
            gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 1, 3), ("b", 0, 2)]))
        self.assertIn("50", str(ctx.exception))
        self.assertIn("5", str(ctx.exception))

    def test_same_ids_and_sum_replaces(self):
        text = live_file([("a", 1, 30), ("b", 0, 20)])
        out = gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 2, 30), ("b", 0, 20)]))
        self.assertIn('min_value="2" max_value="30" troop="NPCCharacter.a"', out)

    def test_growing_the_template_replaces(self):
        text = live_file([("a", 1, 10)])
        out = gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 1, 10), ("c", 0, 4)]))
        self.assertIn('troop="NPCCharacter.c"', out)

    def test_a_new_template_is_inserted(self):
        text = live_file([("a", 1, 10)])
        out = gh.upsert_party_template(text, "kingdom_hero_party_y_template", roster([("z", 0, 1)]))
        self.assertIn('<MBPartyTemplate id="kingdom_hero_party_y_template">', out)
        self.assertIn('<MBPartyTemplate id="kingdom_hero_party_x_template">', out)


if __name__ == "__main__":
    unittest.main()
