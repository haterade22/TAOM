#!/usr/bin/env python3
"""Unit tests for upsert_party_template() and the read/write pair in generate_clan_heraldry.py.

Operation C replaces a whole <MBPartyTemplate> from the spec roster, so a spec that has fallen
behind the live file is authority to delete whatever the live file gained since. The guard added on
2026-08-17 refused only when the spec would DROP a troop id. That is one of the two ways a spec falls
behind; the other is a party-size retarget (`rebalance_party_template_maxes.py`), which changes no
id and every max_value. On 2026-09-13 (#584) the Black Numenorean stacks left 13 Mordor clan
templates, so for those the live id set and the stale spec's id set became identical, and the
id-only guard stopped seeing that `clan_heraldry/mordor.json` still carries the pre-retarget counts
(max 2 to 4 against a live 23 to 44). These tests pin both halves of "behind".
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import generate_clan_heraldry as gh  # noqa: E402


def live_file(stacks, eol="\n"):
    body = eol.join(
        '\t\t\t<PartyTemplateStack min_value="%d" max_value="%d" troop="NPCCharacter.%s" />' % (mn, mx, t)
        for t, mn, mx in stacks)
    return eol.join([
        '<partyTemplates>',
        '\t<MBPartyTemplate id="kingdom_hero_party_x_template">',
        '\t\t<stacks>',
        body,
        '\t\t</stacks>',
        '\t</MBPartyTemplate>',
        '</partyTemplates>'])


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

    def test_a_crlf_file_gets_crlf_lines(self):
        # taom_partyTemplates.xml is CRLF; a "\n" join left replaced templates with LF lines (#589).
        text = live_file([("a", 1, 30)], eol="\r\n")
        out = gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 1, 30), ("b", 0, 4)]))
        self.assertNotIn("\n", out.replace("\r\n", ""), "a bare LF was written into a CRLF file")
        same = gh.upsert_party_template(text, "kingdom_hero_party_x_template", roster([("a", 1, 30)]))
        self.assertEqual(text, same, "re-applying the live roster must be a no-op")


class ReadWriteKeepTheFilesBytes(unittest.TestCase):
    """write() re-prepends a BOM only when the file had one, and never translates line endings.
    Until #589 it wrote utf-8-sig unconditionally, so a real --apply added a BOM to
    characters/clans.xml while the in-memory no-op invariant passed."""

    def roundtrip(self, data):
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, "f.xml")
            with open(p, "wb") as f:
                f.write(data)
            gh.write(p, gh.read(p))
            with open(p, "rb") as f:
                out = f.read()
            with open(p + ".bak", "rb") as f:
                bak = f.read()
        return out, bak

    def test_no_bom_crlf_file_stays_without_bom(self):
        data = b'<?xml version="1.0"?>' + b"\r\n" + b"<Factions>" + b"\r\n" + b"</Factions>" + b"\r\n"
        out, bak = self.roundtrip(data)
        self.assertEqual(data, out)
        self.assertEqual(data, bak)

    def test_bom_lf_file_keeps_its_bom(self):
        data = gh.BOM + b'<?xml version="1.0"?>' + b"\n" + b"<Factions>" + b"\n" + b"</Factions>" + b"\n"
        out, bak = self.roundtrip(data)
        self.assertEqual(data, out)
        self.assertEqual(data, bak)


if __name__ == "__main__":
    unittest.main()
