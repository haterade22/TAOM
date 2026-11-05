#!/usr/bin/env python
"""Unit tests for tools/harvest_duty_strings.py.

Run:  python -m unittest tools.tests.test_harvest_duty_strings

Pure stdlib over synthetic text, plus tests over the shipped files. Each test pins one part of
the contract:
  - collect() reads title/success/failure (field duties) and title/body/opta/optb (popups) keys
    from the JSON rows and flags malformed text, bad ids and unread sections
  - plan() reports a missing row as new and a differing whole text as changed, and ignores a match
  - apply_plan() inserts new rows with the file's line endings, rewrites only the changed rows,
    escapes what XML needs, keeps a BOM, and refuses output that would not parse
  - main() exit codes: 0 ok, 1 drift (--check), 2 error, in every mode
  - the shipped JSON and XML are in step (--check exits 0 with 0 changes)
"""
import io
import json
import os
import stat
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import harvest_duty_strings as h  # noqa: E402

DATA = {
    "fieldDuties": [
        {
            "id": "road_patrol",
            "title": "{=taom_enlist_duty_road_patrol_title}a road patrol",
            "success": "{=taom_enlist_duty_road_patrol_success}The road is quiet.",
            "failure": "{=taom_enlist_duty_road_patrol_failure}You miss it.",
        },
    ],
    "interactiveDuties": [
        {
            "id": "night_patrol",
            "title": "{=taom_enlist_duty_night_patrol_title}Night Watch",
            "body": "{=taom_enlist_duty_night_patrol_body}Walk the rounds.",
            "optionA": {"key": "walk", "label": "{=taom_enlist_duty_night_patrol_opta}Walk Rounds"},
            "optionB": {"key": "watch", "label": "{=taom_enlist_duty_night_patrol_optb}Don't Sleep"},
        },
    ],
    "incidents": [],
}

RESULT_ROWS = (
    '\t<string id="taom_enlist_duty_night_patrol_success" text="{=taom_enlist_duty_night_patrol_success}It went well." />\r\n'
    '\t<string id="taom_enlist_duty_night_patrol_failure" text="{=taom_enlist_duty_night_patrol_failure}It didn&#x27;t go as planned." />'
)
TITLE_ROW = '\t<string id="taom_enlist_duty_road_patrol_title" text="{=taom_enlist_duty_road_patrol_title}a road patrol" />'


def xml_with(rows: str, nl: str = "\r\n") -> str:
    return nl.join(["<strings>", rows, "</strings>"]) + nl


def deep(data):
    return json.loads(json.dumps(data))


class CollectTests(unittest.TestCase):
    def test_reads_every_key_from_the_rows(self):
        strings, ids, problems = h.collect(DATA)
        self.assertEqual([], problems)
        self.assertEqual(["road_patrol", "night_patrol"], ids)
        self.assertEqual(
            {
                "taom_enlist_duty_road_patrol_title": "a road patrol",
                "taom_enlist_duty_road_patrol_success": "The road is quiet.",
                "taom_enlist_duty_road_patrol_failure": "You miss it.",
                "taom_enlist_duty_night_patrol_title": "Night Watch",
                "taom_enlist_duty_night_patrol_body": "Walk the rounds.",
                "taom_enlist_duty_night_patrol_opta": "Walk Rounds",
                "taom_enlist_duty_night_patrol_optb": "Don't Sleep",
            },
            strings,
        )

    def test_missing_title_success_or_failure_is_a_problem_on_a_field_duty(self):
        data = deep(DATA)
        del data["fieldDuties"][0]["title"]
        del data["fieldDuties"][0]["success"]
        _s, _i, problems = h.collect(data)
        self.assertEqual(["road_patrol: title text is missing", "road_patrol: success text is missing"], problems)

    def test_popup_rows_need_body_and_both_labels(self):
        data = deep(DATA)
        del data["interactiveDuties"][0]["body"]
        del data["interactiveDuties"][0]["optionB"]["label"]
        _s, _i, problems = h.collect(data)
        self.assertEqual(
            ["night_patrol: body text is missing", "night_patrol: optionB.label text is missing"], problems)

    def test_a_missing_option_object_is_a_problem(self):
        data = deep(DATA)
        del data["interactiveDuties"][0]["optionB"]
        _s, _i, problems = h.collect(data)
        self.assertEqual(["night_patrol: optionB.label text is missing"], problems)

    def test_a_popup_row_needs_no_success_text_and_a_field_row_needs_no_body(self):
        _s, _i, problems = h.collect(DATA)
        self.assertEqual([], problems)

    def test_wrong_key_and_blank_english_are_problems(self):
        data = deep(DATA)
        data["fieldDuties"][0]["title"] = "{=taom_enlist_duty_other_title}a road patrol"
        data["interactiveDuties"][0]["title"] = "{=taom_enlist_duty_night_patrol_title}   "
        _s, _i, problems = h.collect(data)
        self.assertEqual(2, len(problems))
        self.assertIn("must start with", problems[0])
        self.assertIn("no English", problems[1])

    def test_an_id_outside_the_key_alphabet_is_a_problem(self):
        data = deep(DATA)
        data["fieldDuties"][0]["id"] = 'Road"Patrol'
        _s, ids, problems = h.collect(data)
        self.assertEqual(1, len(problems))
        self.assertIn("must match [a-z0-9_]+", problems[0])
        self.assertNotIn('Road"Patrol', ids)

    def test_a_duplicate_row_id_is_a_problem(self):
        data = deep(DATA)
        data["incidents"].append(deep(data["interactiveDuties"][0]))
        _s, _i, problems = h.collect(data)
        self.assertEqual(["night_patrol: duplicate row id"], problems)

    def test_a_list_section_the_tool_does_not_read_is_a_problem(self):
        data = deep(DATA)
        data["ambushes"] = [{"id": "x", "title": "{=taom_enlist_duty_x_title}x"}]
        _s, _i, problems = h.collect(data)
        self.assertEqual(1, len(problems))
        self.assertIn("'ambushes'", problems[0])

    def test_nothing_read_is_a_problem(self):
        self.assertEqual(["no duty string was read from the JSON"], h.collect({"fieldDuties": []})[2])
        self.assertEqual(["the JSON root is not an object"], h.collect([])[2])


class PlanTests(unittest.TestCase):
    def test_matching_row_is_not_a_change(self):
        new, changed = h.plan({"taom_enlist_duty_road_patrol_title": "a road patrol"}, xml_with(TITLE_ROW))
        self.assertEqual(([], []), (new, changed))

    def test_escaped_apostrophe_in_the_xml_matches_the_plain_json_text(self):
        row = '\t<string id="k_title" text="{=k_title}It didn&#x27;t go" />'
        self.assertEqual(([], []), h.plan({"k_title": "It didn't go"}, xml_with(row)))

    def test_missing_row_is_new_and_differing_english_is_changed(self):
        strings = {"taom_enlist_duty_road_patrol_title": "a patrol of the road", "taom_enlist_duty_forage_title": "to forage"}
        new, changed = h.plan(strings, xml_with(TITLE_ROW))
        self.assertEqual([("taom_enlist_duty_forage_title", "to forage")], new)
        self.assertEqual(
            [("taom_enlist_duty_road_patrol_title",
              "{=taom_enlist_duty_road_patrol_title}a road patrol",
              "{=taom_enlist_duty_road_patrol_title}a patrol of the road")], changed)

    def test_a_registered_row_with_the_wrong_key_prefix_is_changed(self):
        row = '\t<string id="k_title" text="{=other_key}same" />'
        new, changed = h.plan({"k_title": "same"}, xml_with(row))
        self.assertEqual([], new)
        self.assertEqual([("k_title", "{=other_key}same", "{=k_title}same")], changed)

    def test_an_id_named_only_in_a_comment_is_not_registered(self):
        xml = xml_with("\t<!-- taom_enlist_duty_forage_title is planned -->\r\n" + TITLE_ROW)
        new, _changed = h.plan({"taom_enlist_duty_forage_title": "to forage"}, xml)
        self.assertEqual(1, len(new))

    def test_missing_results_names_each_unregistered_success_and_failure_row(self):
        xml = xml_with(RESULT_ROWS)
        self.assertEqual([], h.missing_results(["night_patrol"], xml))
        self.assertEqual(
            ["taom_enlist_duty_road_patrol_success", "taom_enlist_duty_road_patrol_failure"],
            h.missing_results(["night_patrol", "road_patrol"], xml))
        self.assertEqual(
            ["taom_enlist_duty_road_patrol_failure"],
            h.missing_results(["road_patrol"], xml, {"taom_enlist_duty_road_patrol_success": "x"}))

    def test_a_result_row_registered_under_another_rows_key_counts_as_missing(self):
        copied = xml_with(
            '\t<string id="taom_enlist_duty_night_patrol_success" text="{=taom_enlist_duty_night_patrol_success}It went well." />\r\n'
            '\t<string id="taom_enlist_duty_night_patrol_failure" text="{=taom_enlist_duty_night_patrol_success}It went well." />')
        self.assertEqual(["taom_enlist_duty_night_patrol_failure"], h.missing_results(["night_patrol"], copied))

    def test_orphans_lists_registered_text_rows_of_a_row_the_json_no_longer_has(self):
        extra = [
            '<string id="taom_enlist_duty_gone_body" text="{=taom_enlist_duty_gone_body}x" />',
            '<string id="taom_enlist_duty_night_patrol_success" text="{=taom_enlist_duty_night_patrol_success}ok" />',
            '<string id="taom_enlist_other" text="{=taom_enlist_other}ok" />',
        ]
        xml = xml_with("\r\n".join([TITLE_ROW] + ["\t" + row for row in extra]))
        self.assertEqual(
            ["taom_enlist_duty_gone_body"],
            h.orphans({"taom_enlist_duty_road_patrol_title": "a road patrol"}, xml, ["road_patrol", "night_patrol"]))


class ApplyTests(unittest.TestCase):
    def test_new_rows_use_the_files_line_endings_and_escape_the_text(self):
        out = h.apply_plan(xml_with(TITLE_ROW), [("k_opta", 'Say "no" & go\tnow\nthen')], [])
        self.assertEqual(out.count("\n"), out.count("\r\n"))
        self.assertIn('<string id="k_opta" text="{=k_opta}Say &quot;no&quot; &amp; go&#9;now&#10;then" />', out)
        self.assertTrue(out.endswith("</strings>\r\n"))

    def test_lf_file_stays_lf(self):
        out = h.apply_plan(xml_with(TITLE_ROW, "\n"), [("k_title", "x")], [])
        self.assertNotIn("\r", out)

    def test_the_key_is_escaped_in_the_id_attribute(self):
        out = h.apply_plan(xml_with(TITLE_ROW), [('k"><evil', "x")], [])
        self.assertIn('id="k&quot;&gt;&lt;evil"', out)
        out = h.apply_plan(xml_with(TITLE_ROW), [('k&x', "x")], [])
        self.assertIn('<string id="k&amp;x"', out)

    def test_changed_row_is_rewritten_in_place(self):
        out = h.apply_plan(xml_with(TITLE_ROW), [], [(
            "taom_enlist_duty_road_patrol_title", "{=taom_enlist_duty_road_patrol_title}a road patrol",
            "{=taom_enlist_duty_road_patrol_title}the road")])
        self.assertIn('text="{=taom_enlist_duty_road_patrol_title}the road"', out)
        self.assertNotIn("a road patrol", out)

    def test_apply_touches_only_the_changed_and_new_rows(self):
        header = "<!-- header comment, kept as is -->"
        odd = "<string   id='x'   text=\"{=x}odd spacing\"/>"
        other = '\t<string id="taom_enlist_other" text="{=taom_enlist_other}Kept &amp; untouched" />'
        before = xml_with("\r\n".join([
            "\t" + header, other, "\t" + odd, TITLE_ROW, RESULT_ROWS, other.replace("other", "later")]))
        changed = [("taom_enlist_duty_road_patrol_title",
                    "{=taom_enlist_duty_road_patrol_title}a road patrol",
                    "{=taom_enlist_duty_road_patrol_title}a patrol of the road")]
        new = [("taom_enlist_duty_forage_title", "to forage")]

        out = h.apply_plan(before, new, changed)

        inserted = "\r\n\t" + h.BLOCK_COMMENT + '\r\n\t<string id="taom_enlist_duty_forage_title" text="{=taom_enlist_duty_forage_title}to forage" />'
        self.assertEqual(1, out.count(inserted))
        self.assertEqual(
            before.replace(TITLE_ROW, TITLE_ROW.replace("a road patrol", "a patrol of the road")),
            out.replace(inserted, "", 1))

    def test_a_bom_survives_apply(self):
        out = h.apply_plan("\ufeff" + xml_with(TITLE_ROW), [("k_title", "x")], [])
        self.assertTrue(out.startswith("\ufeff<strings>"))

    def test_output_that_would_not_parse_raises(self):
        with self.assertRaises(ValueError):
            h.apply_plan("<strings>\r\n<broken\r\n</strings>\r\n", [("k_title", "x")], [])

    def test_apply_then_plan_reports_nothing(self):
        strings, _ids, _problems = h.collect(DATA)
        before = xml_with(TITLE_ROW)
        out = h.apply_plan(before, *h.plan(strings, before))
        self.assertEqual(([], []), h.plan(strings, out))


class MainTests(unittest.TestCase):
    def run_main(self, args, data, xml, raw_json=None):
        with tempfile.TemporaryDirectory() as tmp:
            jp, xp = os.path.join(tmp, "d.json"), os.path.join(tmp, "s.xml")
            with open(jp, "w", encoding="utf-8") as f:
                f.write(raw_json if raw_json is not None else json.dumps(data))
            if xml is not None:
                with open(xp, "w", encoding="utf-8", newline="") as f:
                    f.write(xml)
            code = h.main(args + ["--json", jp, "--xml", xp])
            after = None
            if os.path.exists(xp):
                with open(xp, encoding="utf-8", newline="") as f:
                    after = f.read()
            return code, after

    def synced_xml(self):
        strings, _i, _p = h.collect(DATA)
        base = xml_with(RESULT_ROWS)
        return h.apply_plan(base, *h.plan(strings, base))

    def test_dry_run_never_writes_and_exits_0_even_on_drift(self):
        xml = xml_with(RESULT_ROWS)
        self.assertEqual((0, xml), self.run_main(["--dry-run"], DATA, xml))

    def test_check_exits_1_on_drift_and_0_when_in_step(self):
        xml = xml_with(RESULT_ROWS)
        self.assertEqual((1, xml), self.run_main(["--check"], DATA, xml))
        synced = self.synced_xml()
        self.assertEqual((0, synced), self.run_main(["--check"], DATA, synced))

    def test_check_exits_1_when_a_row_id_has_no_registered_result_rows(self):
        synced = self.synced_xml()
        without = "\r\n".join(l for l in synced.split("\r\n") if "night_patrol_failure" not in l)
        self.assertEqual(1, self.run_main(["--check"], DATA, without)[0])

    def test_check_exits_1_when_a_result_row_carries_another_rows_key(self):
        synced = self.synced_xml()
        copied = synced.replace(
            'text="{=taom_enlist_duty_night_patrol_failure}', 'text="{=taom_enlist_duty_night_patrol_success}')
        self.assertNotEqual(synced, copied)
        self.assertEqual(1, self.run_main(["--check"], DATA, copied)[0])

    def test_a_section_that_is_not_a_list_exits_2_in_every_mode(self):
        for bad in (5, True, 0, "rows", {"id": "x"}):
            data = deep(DATA)
            data["incidents"] = bad
            for mode in ("--dry-run", "--check", "--apply"):
                self.assertEqual(2, self.run_main([mode], data, xml_with(RESULT_ROWS))[0], (bad, mode))

    def test_a_bomd_json_file_is_read(self):
        raw = chr(0xFEFF) + json.dumps(DATA)
        code, _after = self.run_main(["--check"], None, self.synced_xml(), raw_json=raw)
        self.assertEqual(0, code)

    def test_an_unwritable_xml_exits_2_and_leaves_the_file_unchanged(self):
        xml = xml_with(RESULT_ROWS)
        with tempfile.TemporaryDirectory() as tmp:
            jp, xp = os.path.join(tmp, "d.json"), os.path.join(tmp, "s.xml")
            with open(jp, "w", encoding="utf-8") as f:
                json.dump(DATA, f)
            with open(xp, "w", encoding="utf-8", newline="") as f:
                f.write(xml)
            os.chmod(xp, stat.S_IREAD)
            try:
                code = h.main(["--apply", "--json", jp, "--xml", xp])
            finally:
                os.chmod(xp, stat.S_IWRITE | stat.S_IREAD)
            with open(xp, encoding="utf-8", newline="") as f:
                self.assertEqual(xml, f.read())
        self.assertEqual(2, code)

    def test_the_tool_and_its_tests_hold_no_literal_bom_character(self):
        for path in (h.__file__, __file__):
            with open(path, encoding="utf-8") as f:
                self.assertNotIn(chr(0xFEFF), f.read(), path)

    def test_apply_writes_and_a_second_check_is_clean(self):
        code, after = self.run_main(["--apply"], DATA, xml_with(RESULT_ROWS))
        self.assertEqual(0, code)
        self.assertEqual(0, self.run_main(["--check"], DATA, after)[0])

    def test_apply_refuses_when_a_result_row_is_missing_and_writes_nothing(self):
        xml = xml_with(TITLE_ROW)
        self.assertEqual((2, xml), self.run_main(["--apply"], DATA, xml))

    def test_malformed_text_exits_2_in_every_mode_and_writes_nothing(self):
        data = deep(DATA)
        data["interactiveDuties"][0]["body"] = "Walk the rounds."
        xml = xml_with(RESULT_ROWS)
        for mode in ("--dry-run", "--check", "--apply"):
            self.assertEqual((2, xml), self.run_main([mode], data, xml), mode)

    def test_missing_or_malformed_inputs_exit_2_without_a_traceback(self):
        xml = xml_with(RESULT_ROWS)
        for mode in ("--dry-run", "--check", "--apply"):
            self.assertEqual(2, self.run_main([mode], None, xml, raw_json="{ not json")[0], mode)
            self.assertEqual(2, self.run_main([mode], DATA, None)[0], mode)
            self.assertEqual(2, self.run_main([mode], DATA, "<strings><oops></strings>")[0], mode)

    def test_a_bom_in_the_xml_survives_apply(self):
        code, after = self.run_main(["--apply"], DATA, "\ufeff" + xml_with(RESULT_ROWS))
        self.assertEqual(0, code)
        self.assertTrue(after.startswith("\ufeff<strings>"))

    def test_every_changed_key_is_printed_and_the_guide_is_named(self):
        data = deep(DATA)
        data["fieldDuties"][0]["title"] = "{=taom_enlist_duty_road_patrol_title}the road"
        data["interactiveDuties"][0]["title"] = "{=taom_enlist_duty_night_patrol_title}The Watch"
        base = self.synced_xml()
        buffer = io.StringIO()
        real, sys.stdout = sys.stdout, buffer
        try:
            code, _after = self.run_main(["--check"], data, base)
        finally:
            sys.stdout = real
        printed = buffer.getvalue()
        self.assertEqual(1, code)
        self.assertIn("taom_enlist_duty_road_patrol_title", printed)
        self.assertIn("taom_enlist_duty_night_patrol_title", printed)
        self.assertIn("The checklist when you edit an existing English string", printed)
        self.assertIn("{=taom_enlist_duty_road_patrol_title}a road patrol", printed)
        self.assertIn("{=taom_enlist_duty_road_patrol_title}the road", printed)

    def test_a_long_changed_text_is_printed_in_full(self):
        long_text = "word " * 40
        data = deep(DATA)
        data["fieldDuties"][0]["success"] = "{=taom_enlist_duty_road_patrol_success}" + long_text
        buffer = io.StringIO()
        real, sys.stdout = sys.stdout, buffer
        try:
            self.run_main(["--dry-run"], data, self.synced_xml())
        finally:
            sys.stdout = real
        self.assertIn(long_text, buffer.getvalue())


class ShippedFilesTests(unittest.TestCase):
    def test_the_shipped_json_and_xml_are_in_step(self):
        self.assertEqual(0, h.main(["--check"]))

    def test_every_shipped_row_has_a_title_and_registered_results(self):
        data = h.json.loads(h.read_text(h.DUTIES_JSON).lstrip("\ufeff"))
        strings, ids, problems = h.collect(data)
        rows = sum(len(data.get(section) or []) for section in h.SECTIONS)
        self.assertEqual([], problems)
        self.assertEqual(rows, len(ids))
        self.assertEqual(rows, sum(1 for key in strings if key.endswith("_title")))
        self.assertEqual([], h.missing_results(ids, h.read_text(h.STRINGS_XML), strings))


if __name__ == "__main__":
    unittest.main()
