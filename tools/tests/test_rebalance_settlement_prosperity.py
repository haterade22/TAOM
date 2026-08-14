#!/usr/bin/env python3
"""Unit tests for the culture-floor path of rebalance_settlement_prosperity.py (2026-08-14).

This tool WRITES A LIVE GAME FILE outside the repo. Every test below pins a failure mode that
was found by review rather than by running the tool, because the tool's own output looked
correct in all of them:

  - "lift-only" that silently LOWERED a value above the cap (Codex P2);
  - a regex that matched `max-prosperity`, an attribute VALUE containing the attribute name, or
    across `</Settlement >` with a space, each producing exactly one match so the fail-loud
    assertion passed while the wrong bytes were rewritten (Codex P2);
  - a no-op --apply that overwrote the .bak and destroyed the previous run's rollback point
    (Codex P2), which is exactly what re-running to confirm idempotency does.

No test here writes to the game install; the writer is exercised against temp files.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import rebalance_settlement_prosperity as rb  # noqa: E402


class CultureFloorParsingTests(unittest.TestCase):
    def test_parses_multiple_cultures_from_one_spec(self):
        floors = rb.parse_culture_floors(["goblin,gundabad:4800/950/500"])
        self.assertEqual(set(floors), {"goblin", "gundabad"})
        self.assertEqual(floors["goblin"], {"town": 4800, "castle": 950, "hearth": 500})

    def test_values_are_clamped_to_the_caps_at_parse_time(self):
        floors = rb.parse_culture_floors(["goblin:99999/99999/99999"])
        self.assertEqual(floors["goblin"]["town"], rb.PROSPERITY_CAP)
        self.assertEqual(floors["goblin"]["castle"], rb.PROSPERITY_CAP)
        self.assertEqual(floors["goblin"]["hearth"], rb.HEARTH_CAP)

    def test_malformed_specs_abort(self):
        for bad in ("goblin:4800/950", "goblin4800/950/500", "goblin:a/b/c",
                    "goblin:-1/950/500"):
            with self.assertRaises(SystemExit, msg=f"{bad!r} must abort"):
                rb.parse_culture_floors([bad])

    def test_same_culture_twice_with_different_values_aborts(self):
        with self.assertRaises(SystemExit):
            rb.parse_culture_floors(["goblin:4800/950/500", "goblin:4000/900/400"])

    def test_same_culture_twice_with_identical_values_is_allowed(self):
        floors = rb.parse_culture_floors(["goblin:4800/950/500", "goblin:4800/950/500"])
        self.assertEqual(floors["goblin"]["town"], 4800)


class CultureFloorFileTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_committed_spec_loads(self):
        spec = Path(rb.__file__).resolve().parent / "settlement_economy_floor.json"
        floors = rb.load_culture_floor_file(spec)
        self.assertTrue(floors)
        for entry in floors.values():
            self.assertEqual(set(entry), {"town", "castle", "hearth"})

    def test_missing_file_gives_fatal_not_traceback(self):
        with self.assertRaises(SystemExit) as ctx:
            rb.load_culture_floor_file(self.dir / "nope.json")
        self.assertIn("FATAL", str(ctx.exception))

    def test_malformed_json_gives_fatal_not_traceback(self):
        p = self.dir / "bad.json"
        p.write_text("{not json", encoding="utf-8")
        with self.assertRaises(SystemExit) as ctx:
            rb.load_culture_floor_file(p)
        self.assertIn("FATAL", str(ctx.exception))

    def test_missing_floor_keys_abort(self):
        p = self.dir / "partial.json"
        p.write_text('{"floor": {"town": 4800}, "cultures": ["goblin"]}', encoding="utf-8")
        with self.assertRaises(SystemExit):
            rb.load_culture_floor_file(p)


class HearthTargetTests(unittest.TestCase):
    FLOOR = {"goblin": {"town": 4800, "castle": 950, "hearth": 500}}

    def _village(self, hearth, culture="goblin"):
        return [{"id": "v1", "kind": "village", "culture": culture,
                 "hearth": hearth, "bound": "town_1"}]

    def test_below_floor_is_raised(self):
        self.assertEqual(rb.compute_hearth_targets(self._village(300), self.FLOOR), {"v1": 500})

    def test_at_floor_is_untouched(self):
        self.assertEqual(rb.compute_hearth_targets(self._village(500), self.FLOOR), {})

    def test_above_floor_is_never_lowered(self):
        """The lift-only contract. An outer min(..., HEARTH_CAP) here turned 900 into 825."""
        self.assertEqual(rb.compute_hearth_targets(self._village(900), self.FLOOR), {})

    def test_unfloored_culture_is_untouched(self):
        self.assertEqual(rb.compute_hearth_targets(self._village(10, "gondor"), self.FLOOR), {})


class AttrPatternTests(unittest.TestCase):
    """Each case below yields exactly one match under a naive pattern, so the tool's fail-loud
    exactly-once assertion cannot catch any of them."""

    def _value(self, text, sid, tag, attr):
        found = rb._attr_pattern(sid, tag, attr).findall(text)
        return found[0][1] if len(found) == 1 else None

    def test_does_not_match_underscore_prefixed_attribute(self):
        text = ('<Settlement id="t1"><Town max_prosperity="123" prosperity="456">'
                '</Town></Settlement>')
        self.assertEqual(self._value(text, "t1", "Town", "prosperity"), "456")

    def test_does_not_match_hyphen_prefixed_attribute(self):
        """`-` is not a word char, so \\b alone happily matched inside `max-prosperity`."""
        text = ('<Settlement id="t1"><Town max-prosperity="123" prosperity="456">'
                '</Town></Settlement>')
        self.assertEqual(self._value(text, "t1", "Town", "prosperity"), "456")

    def test_does_not_match_inside_an_attribute_value(self):
        text = ('<Settlement id="t1"><Town note=\'prosperity="123"\' prosperity="456">'
                '</Town></Settlement>')
        self.assertEqual(self._value(text, "t1", "Town", "prosperity"), "456")

    def test_does_not_cross_a_spaced_closing_tag(self):
        """`</Settlement >` is valid XML; a literal `</Settlement>` guard does not see it."""
        text = ('<Settlement id="a"><Town is_castle="true"></Town></Settlement >'
                '<Settlement id="b"><Village hearth="300"></Village></Settlement>')
        self.assertEqual(rb._attr_pattern("a", "Village", "hearth").findall(text), [])

    def test_id_prefix_does_not_collide(self):
        text = ('<Settlement id="town_MM1"><Town prosperity="111"></Town></Settlement>'
                '<Settlement id="town_MM10"><Town prosperity="222"></Town></Settlement>')
        self.assertEqual(self._value(text, "town_MM1", "Town", "prosperity"), "111")
        self.assertEqual(self._value(text, "town_MM10", "Town", "prosperity"), "222")


class ApplyToFileTests(unittest.TestCase):
    HEADER = '<?xml version="1.0" encoding="utf-8"?>\r\n<Settlements>\r\n'
    FOOTER = '</Settlements>\r\n'

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.path = str(Path(self._tmp.name) / "settlements.xml")
        body = ('  <Settlement id="town_1" culture="Culture.goblin">'
                '<Components><Town prosperity="3000"/></Components></Settlement>\r\n'
                '  <Settlement id="village_1" culture="Culture.goblin">'
                '<Components><Village hearth="300"/></Components></Settlement>\r\n')
        with open(self.path, "wb") as f:
            f.write(b"\xef\xbb\xbf" + (self.HEADER + body + self.FOOTER).encode("utf-8"))

    def tearDown(self):
        self._tmp.cleanup()

    def _read(self):
        return open(self.path, "rb").read()

    def test_dry_run_writes_nothing(self):
        before = self._read()
        rb.apply_to_file(self.path, {"town_1": 4800}, False, {"village_1": 500})
        self.assertEqual(self._read(), before)
        self.assertFalse(os.path.exists(self.path + ".bak"))

    def test_apply_preserves_bom_and_crlf(self):
        rb.apply_to_file(self.path, {"town_1": 4800}, True, {"village_1": 500})
        after = self._read()
        self.assertTrue(after.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(after.count(b"\r\n"), 5)
        self.assertEqual(after.count(b"\n") - after.count(b"\r\n"), 0)
        self.assertIn(b'prosperity="4800"', after)
        self.assertIn(b'hearth="500"', after)

    def test_no_op_apply_leaves_the_backup_alone(self):
        """Re-running to confirm idempotency must not cost you the previous rollback point."""
        rb.apply_to_file(self.path, {"town_1": 4800}, True, {})
        first_backup = open(self.path + ".bak", "rb").read()
        rb.apply_to_file(self.path, {}, True, {})
        self.assertEqual(open(self.path + ".bak", "rb").read(), first_backup)

    def test_a_second_real_apply_preserves_the_older_backup(self):
        rb.apply_to_file(self.path, {"town_1": 4000}, True, {})
        original = open(self.path + ".bak", "rb").read()
        rb.apply_to_file(self.path, {"town_1": 4800}, True, {})
        stamped = [p for p in os.listdir(os.path.dirname(self.path)) if ".bak-" in p]
        self.assertTrue(stamped, "the first backup must survive under a stamped name")
        kept = open(os.path.join(os.path.dirname(self.path), stamped[0]), "rb").read()
        self.assertEqual(kept, original)

    def test_unknown_id_aborts_before_writing(self):
        before = self._read()
        with self.assertRaises(SystemExit):
            rb.apply_to_file(self.path, {"town_missing": 4800}, True, {})
        self.assertEqual(self._read(), before)
        self.assertFalse(os.path.exists(self.path + ".bak"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
