#!/usr/bin/env python
"""Tests for tools/knowledge.py, the knowledge store's check and query tool (#1613, ADR-016).

Each broken value is tested alone on a small fixture tree, so a finding names one cause. The real-tree tests at the end
are the gate: they run the same check on the repository's own store, with a floor, because a check that scans nothing
reads like a clean run (ADR-011, `trap-index-missing`).

Run:  python -m unittest tools.tests.test_knowledge
"""
import contextlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import knowledge as kn  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent

GOOD = '''title = "Report a detached Blender job from the shared wrapper"
line = "A Blender script that a caller polls runs its body inside blender_job.py job(), which writes the report on every exit."
why = "The Store build of Blender detaches and prints nothing, so the caller trusts a report file."
area = "build-tooling-workflow"
status = "active"

[when]
paths = ["*/tools/blender/*.py"]
outside = ["*/asset_work/*.py"]
tasks = ["blender script", "detached job"]

[[evidence]]
grade = "measured"
value = "five scripts wrote their own completion file and three left a stale ok"
method = "the RCA of 2026-10-16, finding 3"
date = 2026-10-16

[[acts]]
state = "code"
ref = "tools/blender/blender_job.py::def job(path):"

[[acts]]
state = "gate"
ref = "tools/tests/test_blender_job.py::class JobTests"

[[source]]
lesson = "docs/reviews/lessons/build-tooling-workflow.md"
heading = "### Write the completion file from one shared wrapper"
'''

SENTENCE = '''title = "Hold an even rhythm in a stack of bands"
line = "Build a stack of bands on one smooth field with one step between bands."
why = "The eye reads equal spacing as made and a changing tilt as warped."
area = "asset-craft"
status = "active"
next = "The neck build prints the pitch spread and a gate holds a limit."
ask = "Does the build report print the pitch spread of the band stack?"

[when]
outside = ["*/asset_work/*neck*.py"]
tasks = ["neck armour"]

[[evidence]]
grade = "maintainer"
value = "Black neck piece needs to be reworked"
date = 2026-11-25

[[acts]]
state = "instance"
where = "the neck builder of one armour, in an asset work folder outside the repo"

[[source]]
teardown = "2026-11-26-neck"
'''

FILES = {
    "tools/blender/blender_job.py": "import sys\n\n\ndef job(path):\n    return path\n",
    "tools/tests/test_blender_job.py": "import unittest\n\n\nclass JobTests(unittest.TestCase):\n    pass\n",
    "docs/reviews/lessons/build-tooling-workflow.md":
        "# Build\n\n### Write the completion file from one shared wrapper\ntext\n\n### Another lesson (2026-01-01)\n",
}
TEARDOWN = {"docs/knowledge/teardowns/2026-11-26-neck.md": "# Teardown of a neck piece\n\nMeasured.\n"}


def make_root(tmp, entries=None, files=None, drop=()):
    """A fixture repository under tmp: the entry files, the files they name, and the list a git ls-files would give.
    The teardown record is there when an entry cites it."""
    root = Path(tmp)
    for child in root.iterdir():        # a second call in one test starts from nothing, not from the first call's files
        if child.name != ".git":
            shutil.rmtree(child) if child.is_dir() else child.unlink()
    entries = {"blender-job-report": GOOD, "band-stack-even-rhythm": SENTENCE} if entries is None else entries
    all_files = dict(FILES)
    if any('teardown = "2026-11-26-neck"' in text for text in entries.values()):
        all_files.update(TEARDOWN)
    all_files.update(files or {})
    for name in drop:
        all_files.pop(name, None)
    for rel, text in all_files.items():
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8", newline="\n")
    tech = root / "docs" / "knowledge" / "techniques"
    tech.mkdir(parents=True, exist_ok=True)
    for stem, text in entries.items():
        (tech / (stem + ".toml")).write_text(text, encoding="utf-8", newline="\n")
    tracked = sorted(all_files) + ["docs/knowledge/techniques/%s.toml" % s for s in entries]
    return root, tracked


class Case(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.tmp = self._tmp.name

    def codes(self, entries=None, files=None, drop=(), tracked="fixture"):
        root, listed = make_root(self.tmp, entries, files, drop)
        return [f.code for f in kn.check(root, tracked=listed if tracked == "fixture" else tracked)]

    def one(self, text, **kw):
        """The finding codes of a store that holds only this one entry."""
        return self.codes({"blender-job-report": text}, **kw)


class GoodStoreTests(Case):
    def test_the_fixture_store_is_clean(self):
        self.assertEqual(self.codes(), [])

    def test_a_level_is_the_highest_state_an_entry_reaches(self):
        root, _ = make_root(self.tmp)
        store = kn.load(root)
        self.assertEqual(store.entries["blender-job-report"].level, "code")
        self.assertEqual(store.entries["band-stack-even-rhythm"].level, "instance")

    def test_an_entry_with_no_acts_row_is_a_sentence(self):
        text = SENTENCE.replace('[[acts]]\nstate = "instance"\n'
                                'where = "the neck builder of one armour, in an asset work folder outside the repo"\n\n', "")
        root, _ = make_root(self.tmp, {"band-stack-even-rhythm": text})
        self.assertEqual(kn.load(root).entries["band-stack-even-rhythm"].level, "sentence")


class SchemaTests(Case):
    def test_a_missing_key_is_named(self):
        self.assertEqual(self.one(GOOD.replace('why = "The Store build of Blender detaches and prints nothing, '
                                               'so the caller trusts a report file."\n', "")), ["key-missing"])

    def test_an_unknown_top_level_key_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('status = "active"', 'status = "active"\nstatement = "x"')), ["key-unknown"])

    def test_an_unknown_key_inside_a_table_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('tasks = ["blender script", "detached job"]',
                                               'tasks = ["blender script"]\npath = ["*/x/yyy/*"]')), ["key-unknown"])

    def test_text_that_is_not_toml_is_one_finding_for_that_file(self):
        self.assertEqual(self.codes({"blender-job-report": GOOD, "broken-entry": "title = \n[[["}), ["toml-invalid"])

    def test_a_stem_with_capitals_or_an_underscore_is_no_id(self):
        self.assertEqual(self.codes({"Blender_Job": GOOD}), ["id-invalid"])

    def test_an_unknown_area_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('area = "build-tooling-workflow"', 'area = "tooling"')), ["value-invalid"])

    def test_an_area_may_be_one_of_the_listed_extras(self):
        self.assertEqual(self.one(GOOD.replace('area = "build-tooling-workflow"', 'area = "asset-craft"')), [])

    def test_a_status_outside_the_two_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('status = "active"', 'status = "draft"')), ["value-invalid"])

    def test_a_wrong_type_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('tasks = ["blender script", "detached job"]', 'tasks = "blender"')),
                         ["value-invalid"])


class TextTests(Case):
    def test_an_em_dash_in_any_text_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace("so the caller trusts", "so the caller \u2014 trusts")), ["text-dash"])

    def test_an_en_dash_is_a_finding_too(self):
        self.assertEqual(self.one(GOOD.replace("five scripts", "5\u20136 scripts")), ["text-dash"])

    def test_a_line_over_220_characters_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace("which writes the report on every exit.",
                                               "which writes the report on every exit. " + "x" * 130)), ["line-too-long"])

    def test_a_line_break_in_the_delivered_line_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace("runs its body inside", "runs its body\\ninside")), ["text-control"])

    def test_a_tab_in_the_reviewers_question_is_a_finding(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace("Does the build", "Does\\tthe build")}),
                         ["text-control"])


class GlobTests(Case):
    def test_a_glob_must_start_with_a_star(self):
        self.assertEqual(self.one(GOOD.replace('"*/tools/blender/*.py"', '"tools/blender/*.py"')), ["glob-invalid"])

    def test_a_bracket_class_is_refused_because_bash_and_python_read_it_differently(self):
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*/asset_work/[a-z]*.py"')), ["glob-invalid"])

    def test_a_capital_letter_is_refused_because_paths_are_matched_in_lower_case(self):
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*/Asset_work/*.py"')), ["glob-invalid"])

    def test_a_glob_that_is_only_an_extension_is_refused(self):
        for glob in ("*.py", "*/*.py", "*", "*.*", "*/*"):
            with self.subTest(glob=glob):
                self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"%s"' % glob)), ["glob-invalid"])

    def test_a_named_file_kind_is_specific_enough(self):
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*rharm*.py"')), [])

    def test_an_in_repo_trigger_that_matches_no_tracked_file_is_dead(self):
        self.assertEqual(self.one(GOOD.replace('"*/tools/blender/*.py"', '"*/tools/blunder/*.py"')), ["trigger-dead"])

    def test_an_outside_trigger_is_not_held_to_the_tracked_files(self):
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*/no_such_folder/*.py"')), [])

    def test_an_active_entry_needs_a_trigger_of_some_kind(self):
        text = GOOD.replace('paths = ["*/tools/blender/*.py"]\noutside = ["*/asset_work/*.py"]\n'
                            'tasks = ["blender script", "detached job"]', "paths = []")
        self.assertEqual(self.one(text), ["trigger-none"])

    def test_a_star_crosses_folders_and_case_and_slashes_fold(self):
        self.assertTrue(kn.glob_matches("*/tools/blender/*.py", r"E:\Repos\TAOM-x\tools\blender\sub\A.py"))
        self.assertTrue(kn.glob_matches("*/assetsources/*.fbx", "e:/steam/m & b/Modules/Armory/AssetSources/a/b.FBX"))
        self.assertFalse(kn.glob_matches("*/tools/blender/*.py", "e:/repos/taom/tools/other/a.py"))


class ActsTests(Case):
    def test_a_gate_needs_a_reference(self):
        self.assertEqual(self.one(GOOD.replace('state = "gate"\nref = "tools/tests/test_blender_job.py::class JobTests"',
                                               'state = "gate"')), ["acts-invalid"])

    def test_a_reference_is_a_path_and_a_text(self):
        self.assertEqual(self.one(GOOD.replace('"tools/blender/blender_job.py::def job(path):"',
                                               '"tools/blender/blender_job.py"')), ["ref-invalid"])

    def test_a_reference_to_a_file_that_is_gone_fails(self):
        self.assertEqual(self.one(GOOD, drop=("tools/blender/blender_job.py",)), ["ref-missing", "trigger-dead"])

    def test_a_reference_whose_text_is_gone_fails_though_the_file_stays(self):
        self.assertEqual(self.one(GOOD, files={"tools/blender/blender_job.py": "def run(path):\n    return path\n"}),
                         ["ref-text-missing"])

    def test_a_part_of_the_text_elsewhere_in_the_file_is_not_the_text(self):
        self.assertEqual(self.one(GOOD, files={"tools/blender/blender_job.py": "# job\ndef job_old(path):\n    pass\n"}),
                         ["ref-text-missing"])

    def test_an_instance_says_where_it_lives(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace(
            'where = "the neck builder of one armour, in an asset work folder outside the repo"\n', "")}), ["acts-invalid"])

    def test_sentence_is_the_absence_of_a_row_and_never_a_state(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace('state = "instance"', 'state = "sentence"')}),
                         ["acts-invalid"])

    def test_an_entry_below_a_gate_says_what_would_make_it_act(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace(
            'next = "The neck build prints the pitch spread and a gate holds a limit."\n', "")}), ["next-missing"])

    def test_a_report_alone_still_owes_its_next_step(self):
        text = GOOD.replace('state = "code"', 'state = "report"').replace(
            '\n[[acts]]\nstate = "gate"\nref = "tools/tests/test_blender_job.py::class JobTests"\n', "")
        self.assertEqual(self.one(text), ["next-missing"])


class EvidenceAndSourceTests(Case):
    def test_an_entry_with_no_evidence_is_a_finding(self):
        start, end = GOOD.index("[[evidence]]"), GOOD.index("[[acts]]")
        self.assertEqual(self.one(GOOD[:start] + GOOD[end:]), ["evidence-missing"])

    def test_a_grade_outside_the_three_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('grade = "measured"', 'grade = "seen"')), ["evidence-invalid"])

    def test_a_measured_number_names_its_method(self):
        self.assertEqual(self.one(GOOD.replace('method = "the RCA of 2026-10-16, finding 3"\n', "")), ["evidence-invalid"])

    def test_evidence_carries_a_date(self):
        self.assertEqual(self.one(GOOD.replace("date = 2026-10-16\n", "")), ["evidence-invalid"])

    def test_an_entry_with_no_source_is_a_finding(self):
        self.assertEqual(self.one(GOOD[:GOOD.index("[[source]]")]), ["source-missing"])

    def test_a_lesson_heading_must_be_a_whole_line_of_its_file(self):
        self.assertEqual(self.one(GOOD.replace("### Write the completion file from one shared wrapper",
                                               "### Write the completion file")), ["source-invalid"])

    def test_a_lesson_file_that_is_gone_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace("build-tooling-workflow.md", "no-such-file.md")), ["source-invalid"])

    def test_a_teardown_source_names_a_record_that_exists(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace("2026-11-26-neck", "2026-11-27-neck")}),
                         ["source-invalid"])

    def test_a_wrong_teardown_name_leaves_the_real_record_uncited(self):
        self.assertEqual(self.codes({"band-stack-even-rhythm": SENTENCE.replace("2026-11-26-neck", "2026-11-27-neck")},
                                    files=TEARDOWN), ["source-invalid", "teardown-uncited"])

    def test_the_maintainers_words_come_with_their_date(self):
        text = GOOD[:GOOD.index("[[source]]")] + '[[source]]\nmaintainer = "2026-11-25"\n'
        self.assertEqual(self.one(text), ["source-invalid"])
        self.assertEqual(self.one(text + 'words = "Fill these gaps"\n'), [])

    def test_a_source_row_is_one_kind_only(self):
        self.assertEqual(self.one(GOOD + 'teardown = "2026-11-26-neck"\n'), ["source-invalid"])

    def test_a_teardown_record_that_no_entry_cites_is_not_closed(self):
        self.assertEqual(self.codes({"blender-job-report": GOOD}, files=TEARDOWN), ["teardown-uncited"])

    def test_a_teardown_that_says_why_it_gave_no_technique_is_closed(self):
        self.assertEqual(self.codes({"blender-job-report": GOOD}, files={
            "docs/knowledge/teardowns/2026-11-26-neck.md": "# Teardown\n\nNo technique: the reference was worse.\n"}), [])


class RetireTests(Case):
    def test_a_retired_entry_says_when_and_why(self):
        self.assertEqual(self.one(GOOD.replace('status = "active"', 'status = "retired"')), ["retired-invalid"])
        self.assertEqual(self.one(GOOD.replace('status = "active"',
                                               'status = "retired"\nretired = "2026-12-01: the wrapper was removed"')), [])

    def test_an_active_entry_carries_no_retirement(self):
        self.assertEqual(self.one(GOOD.replace('status = "active"', 'status = "active"\nretired = "2026-12-01: x"')),
                         ["retired-invalid"])

    def test_superseded_by_names_an_active_entry(self):
        old = GOOD.replace('status = "active"', 'status = "retired"\nretired = "2026-12-01: replaced"\n'
                                                'superseded_by = "band-stack-even-rhythm"')
        self.assertEqual(self.codes({"blender-job-report": old, "band-stack-even-rhythm": SENTENCE}), [])
        self.assertEqual(self.codes({"blender-job-report": old.replace("band-stack-even-rhythm", "no-such-entry"),
                                     "band-stack-even-rhythm": SENTENCE}), ["retired-invalid"])

    def test_a_retired_entry_is_not_served(self):
        old = GOOD.replace('status = "active"', 'status = "retired"\nretired = "2026-12-01: replaced"')
        root, _ = make_root(self.tmp, {"blender-job-report": old})
        store = kn.load(root)
        self.assertEqual(kn.matching(store, "e:/x/tools/blender/a.py"), [])
        self.assertEqual([e.id for e in kn.matching(store, "e:/x/tools/blender/a.py", include_retired=True)],
                         ["blender-job-report"])
        self.assertNotIn("blender-job-report", kn.index_text(store))


class SeeTests(Case):
    def test_see_names_a_file_that_exists(self):
        self.assertEqual(self.one(GOOD.replace('status = "active"', 'status = "active"\nsee = "docs/no-such.md"')),
                         ["see-missing"])

    def test_see_may_name_a_heading_of_that_file(self):
        with_see = GOOD.replace('status = "active"',
                                'status = "active"\nsee = "docs/reviews/lessons/build-tooling-workflow.md#Another lesson (2026-01-01)"')
        self.assertEqual(self.one(with_see), [])
        self.assertEqual(self.one(with_see.replace("Another lesson (2026-01-01)", "A lesson nobody wrote")), ["see-missing"])


class StoreStateTests(Case):
    def test_a_missing_store_is_a_finding_and_never_a_clean_run(self):
        self.assertEqual([f.code for f in kn.check(Path(self.tmp), tracked=[])], ["store-missing"])

    def test_an_empty_store_is_a_finding(self):
        self.assertEqual(self.codes({}, drop=("docs/knowledge/teardowns/2026-11-26-neck.md",)), ["store-empty"])

    def test_when_the_tracked_files_cannot_be_listed_the_check_says_unchecked(self):
        root, _ = make_root(self.tmp)
        with mock.patch.object(kn, "tracked_files", return_value=None):
            findings = kn.check(root)
        self.assertEqual([f.code for f in findings], ["unchecked-tracked"])
        self.assertEqual(kn.exit_code(findings), 2)

    def test_exit_codes(self):
        self.assertEqual(kn.exit_code([]), 0)
        self.assertEqual(kn.exit_code([kn.Finding("a", "key-missing", "m")]), 1)
        self.assertEqual(kn.exit_code([kn.Finding("a", "unchecked-tracked", "m"), kn.Finding("a", "key-missing", "m")]), 1)


class QueryTests(Case):
    def setUp(self):
        super().setUp()
        self.root, _ = make_root(self.tmp)
        self.store = kn.load(self.root)

    def test_for_finds_an_entry_by_a_path_in_any_checkout(self):
        self.assertEqual([e.id for e in kn.matching(self.store, r"E:\repos\taom-other\tools\blender\x.py")],
                         ["blender-job-report"])

    def test_for_resolves_a_relative_path_against_the_folder_it_runs_in(self):
        self.assertEqual([e.id for e in kn.matching(self.store, "tools/blender/x.py", cwd=self.root)],
                         ["blender-job-report"])

    def test_entries_that_do_not_act_yet_come_first(self):
        both = kn.matching(self.store, "e:/asset_work/tools/blender/neck_build.py")
        self.assertEqual([e.id for e in both], ["band-stack-even-rhythm", "blender-job-report"])

    def test_a_miss_names_what_was_searched(self):
        out = self.run_main("for", "e:/elsewhere/readme.txt")
        self.assertIn("no entry matched", out)
        self.assertIn("2 entries scanned", out)
        self.assertIn("docs/knowledge/techniques", out.replace("\\", "/"))

    def test_for_names_the_entry_that_a_file_makes_act(self):
        """The file is no trigger of the entry, so only its `ref` brings the entry up (what the hook's watch note does)."""
        out = self.run_main("for", str(self.root / "tools" / "tests" / "test_blender_job.py"))
        self.assertNotIn("no entry matched", out)
        self.assertIn("blender-job-report [code] acts through this file", out)
        self.assertIn("docs/knowledge/techniques/blender-job-report.toml", out)
        self.assertEqual(kn.enforced_by(self.store, "e:/elsewhere/tests/test_blender_job.py"), [])     # the whole path

    def test_brief_takes_paths_and_task_phrases(self):
        out = kn.brief_text(self.store, paths=[], tasks=["neck armour"], cwd=self.root)
        self.assertIn("band-stack-even-rhythm [instance]", out)
        self.assertNotIn("blender-job-report", out)
        out = kn.brief_text(self.store, paths=["tools/blender/a.py"], tasks=[], cwd=self.root)
        self.assertIn("blender-job-report [code]", out)

    def test_brief_with_no_match_says_so_with_its_scope(self):
        out = kn.brief_text(self.store, paths=["docs/x.md"], tasks=["harmony"], cwd=self.root)
        self.assertIn("No entry matched", out)
        self.assertIn("2 entries scanned", out)

    def test_find_ranks_by_the_words_an_entry_holds(self):
        self.assertEqual([e.id for e in kn.find(self.store, ["detached", "report"])][:1], ["blender-job-report"])
        self.assertEqual(kn.find(self.store, ["harmony"]), [])

    def test_debt_counts_by_area_and_level_and_lists_what_does_not_act(self):
        debt = kn.debt(self.store)
        self.assertEqual(debt["by_level"], {"sentence": 0, "instance": 1, "report": 0, "gate": 0, "code": 1})
        self.assertEqual(debt["by_area"]["asset-craft"]["instance"], 1)
        self.assertEqual([(d["id"], d["level"]) for d in debt["owed"]], [("band-stack-even-rhythm", "instance")])
        self.assertIn("pitch spread", debt["owed"][0]["next"])

    def run_main(self, *argv):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            kn.main(list(argv) + ["--root", str(self.root)])
        return buf.getvalue()

    def test_show_prints_an_entry_and_fails_on_an_unknown_id(self):
        self.assertIn("Report a detached Blender job", self.run_main("show", "blender-job-report"))
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(kn.main(["show", "no-such-entry", "--root", str(self.root)]), 2)


class IndexTests(Case):
    def test_the_index_holds_a_line_per_entry_and_glob_and_one_per_enforcing_file(self):
        root, _ = make_root(self.tmp)
        lines = kn.index_text(kn.load(root)).splitlines()
        self.assertTrue(lines[0].startswith("#knowledge-index v1\tentries=2\tskipped=0\t"))
        rows = [ln.split("\t") for ln in lines[1:]]
        self.assertTrue(all(len(r) == 6 for r in rows), rows)
        self.assertEqual([(r[0], r[1], r[2], r[3]) for r in rows], [
            ("t", "band-stack-even-rhythm", "instance", "*/asset_work/*neck*.py"),
            ("t", "blender-job-report", "code", "*/asset_work/*.py"),
            ("t", "blender-job-report", "code", "*/tools/blender/*.py"),
            ("w", "blender-job-report", "code", "*/tools/blender/blender_job.py"),
            ("w", "blender-job-report", "code", "*/tools/tests/test_blender_job.py"),
        ])
        self.assertEqual(rows[0][5], "Does the build report print the pitch spread of the band stack?")
        self.assertIn("runs its body inside", rows[1][4])

    def test_an_entry_that_cannot_be_read_is_left_out_and_counted(self):
        root, _ = make_root(self.tmp, {"blender-job-report": GOOD, "broken-entry": "title = \n"})
        first = kn.index_text(kn.load(root)).splitlines()[0]
        self.assertIn("entries=1\tskipped=1", first)

    def test_index_writes_its_file_through_a_rename(self):
        root, _ = make_root(self.tmp)
        out = Path(self.tmp) / "ledger" / "s1.kindex"
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(kn.main(["index", "--out", str(out), "--root", str(root)]), 0)
        self.assertTrue(out.read_text(encoding="utf-8").startswith("#knowledge-index v1\t"))
        self.assertEqual([p.name for p in out.parent.iterdir()], ["s1.kindex"])


SKILL = "---\nname: %s\ndescription: %s\n---\n\n# %s\n\nSteps.\n"


class ProcedureTests(Case):
    def setUp(self):
        super().setUp()
        see = GOOD.replace('status = "active"', 'status = "active"\nsee = ".claude/skills/blender-asset/SKILL.md"')
        self.root, _ = make_root(self.tmp, {"blender-job-report": see}, files={
            ".claude/skills/blender-asset/SKILL.md": SKILL % ("blender-asset", "Use when a mesh is built in Blender.", "Blender"),
            ".claude/skills/groom-hair/SKILL.md": SKILL % ("groom-hair", "Use when authoring hair.", "Hair"),
            ".agents/skills/taom-build/SKILL.md": SKILL % ("taom-build", "Use to build.", "Build"),
            "docs/ai-includes/weapon-creation-workflow.md": "# Weapon creation\n\n1. Step.\n",
            "docs/ai-includes/architecture.md": "# Architecture\n\nLayers.\n",
            "docs/modding/recipe-add-a-culture.md": "# Add a culture\n\n1. Step.\n",
            "docs/modding/id-cheatsheet.md": "# Ids\n",
            "docs/adrs/012-code-graph-in-the-workflow.md": "# ADR-012\n",
            "docs/reviews/rca-weapon-workflow-2026-01-01.md": "# RCA\n",
            ".ai/policy.md": "# Shared policy\n\nRules for every client.\n",
            "docs/reference/doc-lookup.md":
                "| Need to... | Read |\n|---|---|\n"
                "| Make a weapon mesh | [weapon workflow](../ai-includes/weapon-creation-workflow.md) |\n"
                "| Work on a mesh | the `/blender-asset` skill |\n",
            "docs/INDEX.md": "[architecture](ai-includes/architecture.md)\n",
            "CLAUDE.md": "# CLAUDE\n",
        }, drop=("docs/knowledge/teardowns/2026-11-26-neck.md",))
        self.records = {r["id"]: r for r in kn.procedures(self.root)}

    def test_every_skill_and_workflow_doc_is_a_record(self):
        """A decision record and a review record are never procedures, whatever their file names hold."""
        self.assertEqual(sorted(self.records), [
            "codex-skill:taom-build", "doc:.ai/policy.md", "doc:docs/ai-includes/architecture.md",
            "doc:docs/ai-includes/weapon-creation-workflow.md", "doc:docs/modding/recipe-add-a-culture.md",
            "skill:blender-asset", "skill:groom-hair"])

    def test_a_skills_trigger_is_its_description(self):
        self.assertEqual(self.records["skill:blender-asset"]["trigger"], "Use when a mesh is built in Blender.")
        self.assertEqual(self.records["skill:blender-asset"]["home"], ".claude/skills/blender-asset/SKILL.md")

    def test_a_docs_title_is_its_first_heading_and_its_trigger_the_lookup_row(self):
        rec = self.records["doc:docs/ai-includes/weapon-creation-workflow.md"]
        self.assertEqual(rec["title"], "Weapon creation")
        self.assertEqual(rec["trigger"], "Make a weapon mesh")

    def test_indexed_by_lists_the_indexes_that_name_a_record(self):
        self.assertEqual(self.records["skill:blender-asset"]["indexed_by"], ["docs/reference/doc-lookup.md"])
        self.assertEqual(self.records["doc:docs/ai-includes/architecture.md"]["indexed_by"], ["docs/INDEX.md"])
        self.assertEqual(self.records["skill:groom-hair"]["indexed_by"], [])

    def test_orphans_are_the_records_that_no_index_names(self):
        self.assertEqual(sorted(r["id"] for r in kn.procedures(self.root) if not r["indexed_by"]),
                         ["codex-skill:taom-build", "doc:.ai/policy.md", "doc:docs/modding/recipe-add-a-culture.md",
                          "skill:groom-hair"])

    def test_find_answers_with_the_procedures_and_the_lookup_rows_for_the_words(self):
        records, rows = kn.find_procedures(self.root, ["weapon"])
        self.assertEqual([r["id"] for r in records], ["doc:docs/ai-includes/weapon-creation-workflow.md"])
        self.assertEqual(rows, [("Make a weapon mesh", ["docs/ai-includes/weapon-creation-workflow.md"])])
        records, rows = kn.find_procedures(self.root, ["mesh", "blender"])
        self.assertEqual(records[0]["id"], "skill:blender-asset")
        self.assertIn(("Work on a mesh", []), rows)             # a row that names a skill and links no file is a row
        self.assertEqual(kn.find_procedures(self.root, ["no-such-word"]), ([], []))

    def test_a_record_lists_the_entries_that_point_at_it(self):
        self.assertEqual(self.records["skill:blender-asset"]["entries"], ["blender-job-report"])
        self.assertEqual(self.records["skill:groom-hair"]["entries"], [])


class CliTests(Case):
    def run_cli(self, *argv):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
            code = kn.main(list(argv))
        return code, out.getvalue()

    def test_check_prints_one_line_per_finding_and_exits_1(self):
        root, _ = make_root(self.tmp, {"blender-job-report": GOOD.replace('status = "active"', 'status = "draft"')})
        with mock.patch.object(kn, "tracked_files", return_value=sorted(FILES)):
            code, out = self.run_cli("check", "--root", str(root))
        self.assertEqual(code, 1)
        self.assertRegex(out, r"docs/knowledge/techniques/blender-job-report\.toml: value-invalid: ")

    def test_check_on_a_clean_store_exits_0_and_says_what_it_read(self):
        root, tracked = make_root(self.tmp)
        with mock.patch.object(kn, "tracked_files", return_value=tracked):
            code, out = self.run_cli("check", "--root", str(root))
        self.assertEqual(code, 0)
        self.assertIn("2 entries", out)

    def test_debt_json_is_machine_readable(self):
        root, _ = make_root(self.tmp)
        code, out = self.run_cli("debt", "--json", "--root", str(root))
        self.assertEqual(code, 0)
        self.assertEqual(json.loads(out)["by_level"]["code"], 1)

    def test_no_command_is_a_usage_error(self):
        code, _ = self.run_cli()
        self.assertEqual(code, 2)


class SurvivorTests(Case):
    """One test for each check that the mutation run of 2026-11-26 could remove with every test still green."""

    def test_a_required_key_of_the_wrong_type_is_a_finding_and_not_a_silent_skip(self):
        text = GOOD.replace('title = "Report a detached Blender job from the shared wrapper"', "title = 5")
        self.assertEqual(self.one(text), ["value-invalid"])

    def test_a_row_that_is_no_table_is_a_finding(self):
        start, end = GOOD.index("[[evidence]]"), GOOD.index("[[acts]]")
        self.assertEqual(self.one(GOOD[:start].replace("[when]", 'evidence = ["x"]\n\n[when]') + GOOD[end:]),
                         ["evidence-invalid"])
        start, end = GOOD.index("[[acts]]"), GOOD.index("[[source]]")
        self.assertEqual(self.one(GOOD[:start].replace("[when]", 'acts = ["x"]\nnext = "y"\n\n[when]') + GOOD[end:]),
                         ["acts-invalid"])
        start = GOOD.index("[[source]]")
        self.assertEqual(self.one(GOOD[:start].replace("[when]", 'source = ["x"]\n\n[when]')), ["source-invalid"])

    def test_evidence_with_an_empty_value_is_a_finding(self):
        value = 'value = "five scripts wrote their own completion file and three left a stale ok"'
        self.assertEqual(self.one(GOOD.replace(value, 'value = ""')), ["evidence-invalid"])
        self.assertEqual(self.one(GOOD.replace(value + "\n", "")), ["evidence-invalid"])

    def test_an_issue_source_is_a_positive_number(self):
        base = GOOD[:GOOD.index("[[source]]")] + "[[source]]\n"
        self.assertEqual(self.one(base + "issue = 1613\n"), [])
        self.assertEqual(self.one(base + "issue = 0\n"), ["source-invalid"])
        self.assertEqual(self.one(base + 'issue = "1613"\n'), ["source-invalid"])
        self.assertEqual(self.one(base + "issue = true\n"), ["source-invalid"])

    def test_an_adopt_source_names_an_adoption_record_that_exists(self):
        base = GOOD[:GOOD.index("[[source]]")] + '[[source]]\nadopt = "docs/reviews/adopt-x-2026-01-01.md"\n'
        self.assertEqual(self.one(base), ["source-invalid"])
        self.assertEqual(self.one(base, files={"docs/reviews/adopt-x-2026-01-01.md": "# Adopted\n"}), [])
        self.assertEqual(self.one(base.replace("docs/reviews/adopt-x-2026-01-01.md",
                                               "docs/reviews/lessons/build-tooling-workflow.md")), ["source-invalid"])

    def test_a_teardown_source_that_is_no_text_is_a_finding_and_no_crash(self):
        self.assertEqual(self.one(GOOD + '\n[[source]]\nteardown = ["x"]\n'), ["source-invalid"])

    def test_an_unknown_key_in_a_row_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace('grade = "measured"', 'grade = "measured"\ncolour = "x"')), ["key-unknown"])
        self.assertEqual(self.one(GOOD.replace('state = "code"', 'state = "code"\nscop = "x"')), ["key-unknown"])
        self.assertEqual(self.one(GOOD + 'extra = "x"\n'), ["key-unknown"])

    def test_a_lesson_heading_is_a_third_level_heading_and_no_other_line(self):
        self.assertEqual(self.one(GOOD.replace('heading = "### Write the completion file from one shared wrapper"',
                                               'heading = "# Build"')), ["source-invalid"])

    def test_an_entry_that_cannot_be_opened_is_a_finding(self):
        root, tracked = make_root(self.tmp, {"blender-job-report": GOOD})
        (root / "docs" / "knowledge" / "techniques" / "a-folder.toml").mkdir()
        self.assertEqual([f.code for f in kn.check(root, tracked=tracked)], ["toml-invalid"])
        self.assertIn("skipped=1", kn.index_text(kn.load(root)).splitlines()[0])

    def test_a_title_or_a_question_over_its_limit_is_a_finding(self):
        self.assertEqual(self.one(GOOD.replace("from the shared wrapper", "from the shared wrapper " + "x" * 60)),
                         ["line-too-long"])
        long_ask = SENTENCE.replace("of the band stack?", "of the band stack " + "x" * 170 + "?")
        self.assertEqual(self.codes({"band-stack-even-rhythm": long_ask}), ["line-too-long"])

    def test_a_glob_with_a_line_break_or_a_question_mark_is_refused(self):
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*/asset_work/*.py\\n"')), ["glob-invalid"])
        self.assertEqual(self.one(GOOD.replace('"*/asset_work/*.py"', '"*/asset_work/a?b/*.py"')), ["glob-invalid"])
        self.assertIsNotNone(kn.glob_problem("*/asset_work/*.py\n"))

    def test_a_reference_path_holds_no_pattern_character_and_no_tab(self):
        self.assertEqual(self.one(GOOD.replace("tools/blender/blender_job.py::", "tools/blender/[b]lender_job.py::")),
                         ["ref-invalid"])
        self.assertEqual(self.one(GOOD.replace("tools/blender/blender_job.py::", "tools/blender/blender\\tjob.py::")),
                         ["ref-invalid"])

    def test_a_crash_of_the_check_is_not_judged_and_never_a_clean_run(self):
        root, _ = make_root(self.tmp)
        out = io.StringIO()
        with mock.patch.object(kn, "check", side_effect=RuntimeError("boom")), \
                contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
            code = kn.main(["check", "--root", str(root)])
        self.assertEqual(code, 2)
        self.assertIn("NOT judged", out.getvalue())


class OrderTests(Case):
    """The ids here sort against the levels, so an order by name alone would fail."""

    def setUp(self):
        super().setUp()
        self.root, _ = make_root(self.tmp, {"aaa-acts-in-code": GOOD, "zzz-does-not-act": SENTENCE})
        self.store = kn.load(self.root)

    def test_matching_puts_the_entry_that_does_not_act_first(self):
        hits = kn.matching(self.store, "e:/asset_work/tools/blender/neck_build.py")
        self.assertEqual([e.id for e in hits], ["zzz-does-not-act", "aaa-acts-in-code"])

    def test_the_index_puts_it_first_too(self):
        ids = [ln.split("\t")[1] for ln in kn.index_text(self.store).splitlines()[1:] if ln.startswith("t\t")]
        self.assertEqual(ids, ["zzz-does-not-act", "aaa-acts-in-code", "aaa-acts-in-code"])

    def test_debt_lists_what_is_owed_by_level(self):
        self.assertEqual([d["id"] for d in kn.debt(self.store)["owed"]], ["zzz-does-not-act"])

    def test_find_puts_the_entry_with_more_of_the_words_first_whatever_its_level(self):
        hits = kn.find(self.store, ["blender", "detached", "bands"])     # two words in the code entry, one in the other
        self.assertEqual([e.id for e in hits], ["aaa-acts-in-code", "zzz-does-not-act"])
        hits = kn.find(self.store, ["rhythm", "bands", "blender"])       # two in the instance, one in the code entry
        self.assertEqual([e.id for e in hits], ["zzz-does-not-act", "aaa-acts-in-code"])


class WholeWordTests(Case):
    def setUp(self):
        super().setUp()
        self.root, _ = make_root(self.tmp)
        self.store = kn.load(self.root)

    def test_find_matches_whole_words_and_not_letters_inside_a_word(self):
        self.assertEqual(len(kn.find(self.store, ["and"])), 2)             # a word of both entries
        self.assertEqual(kn.find(self.store, ["ban"]), [])                 # inside "bands"
        self.assertEqual(kn.find(self.store, ["port"]), [])                # inside "report"

    def test_brief_takes_an_entry_when_every_word_of_one_of_its_task_phrases_is_given(self):
        self.assertIn("band-stack-even-rhythm", kn.brief_text(self.store, [], ["the neck armour of a horse"], self.root))
        self.assertIn("No entry matched", kn.brief_text(self.store, [], ["neck"], self.root))
        self.assertIn("No entry matched", kn.brief_text(self.store, [], ["armour"], self.root))

    def test_brief_does_not_read_the_title_or_the_id(self):
        self.assertIn("No entry matched", kn.brief_text(self.store, [], ["rhythm"], self.root))


class BriefCapTests(Case):
    def test_brief_shows_twelve_entries_and_says_how_many_matched(self):
        root, _ = make_root(self.tmp, {"entry-%02d" % i: GOOD for i in range(13)})
        out = kn.brief_text(kn.load(root), ["tools/blender/a.py"], [], root)
        self.assertEqual(sum(1 for ln in out.splitlines() if ln.startswith("- entry-")), 12)
        self.assertIn("(12 of 13 matches shown; not shown: entry-12.", out)     # the cut is named, never silent
        self.assertIn("(13 of 13 entries matched", out)

    def test_the_cap_does_not_cut_an_entry_that_matches_a_path_and_a_task_phrase(self):
        entries = {"entry-%02d" % i: GOOD for i in range(13)}       # all match the path; the last by id also the task
        entries["entry-12"] = GOOD.replace('tasks = ["blender script", "detached job"]', 'tasks = ["wheel hub"]')
        root, _ = make_root(self.tmp, entries)
        out = kn.brief_text(kn.load(root), ["tools/blender/a.py"], ["wheel hub"], root)
        shown = [ln.split()[1] for ln in out.splitlines() if ln.startswith("- entry-")]
        self.assertEqual(shown[0], "entry-12")
        self.assertEqual(len(shown), 12)

    def test_a_brief_orders_by_what_matched_before_the_level(self):
        by_path = SENTENCE.replace('tasks = ["neck armour"]', 'tasks = ["other work"]')              # instance
        by_task = GOOD.replace('"*/asset_work/*.py"', '"*/other_work/*.py"')                         # code
        root, _ = make_root(self.tmp, {"band-stack-even-rhythm": by_path, "blender-job-report": by_task})
        store = kn.load(root)
        ids = [e.id for e in kn.brief_entries(store, ["e:/asset_work/a_neck.py"], ["blender script"], root)]
        self.assertEqual(ids, ["blender-job-report", "band-stack-even-rhythm"])      # a task match, then a path match
        both = GOOD.replace('"*/asset_work/*.py"', '"*/asset_work/*neck*.py"')
        root, _ = make_root(self.tmp, {"band-stack-even-rhythm": by_path, "blender-job-report": both})
        ids = [e.id for e in kn.brief_entries(kn.load(root), ["e:/asset_work/a_neck.py"], ["blender script"], root)]
        self.assertEqual(ids, ["blender-job-report", "band-stack-even-rhythm"])      # both, then a path match


class IndexSafetyTests(Case):
    """The hook builds its index without running the check, so the index itself keeps a bad value out."""

    def test_a_glob_the_check_refuses_gets_no_row(self):
        root, _ = make_root(self.tmp, {"blender-job-report": GOOD.replace('"*/asset_work/*.py"', '"*/asset_work/[a-z]*.py"')})
        globs = [ln.split("\t")[3] for ln in kn.index_text(kn.load(root)).splitlines()[1:]]
        self.assertNotIn("*/asset_work/[a-z]*.py", globs)
        self.assertIn("*/tools/blender/*.py", globs)

    def test_a_tab_or_a_line_break_in_a_line_cannot_shift_the_fields(self):
        text = GOOD.replace("runs its body inside", "runs its body\\tinside\\nthe wrapper")
        root, _ = make_root(self.tmp, {"blender-job-report": text})
        lines = kn.index_text(kn.load(root)).splitlines()
        rows = [ln.split("\t") for ln in lines[1:]]
        self.assertEqual(len(lines), 5)
        self.assertTrue(all(len(r) == 6 for r in rows), rows)

    def test_the_printed_line_names_the_see_target(self):
        see = GOOD.replace('status = "active"', 'status = "active"\nsee = "docs/reviews/lessons/build-tooling-workflow.md"')
        root, _ = make_root(self.tmp, {"blender-job-report": see})
        row = kn.index_text(kn.load(root)).splitlines()[1].split("\t")
        self.assertTrue(row[4].endswith("(see docs/reviews/lessons/build-tooling-workflow.md)"), row[4])


class RetiredHistoryTests(Case):
    def test_a_retired_entry_may_name_a_doc_a_heading_and_an_enforcer_that_are_gone(self):
        old = GOOD.replace('status = "active"', 'status = "retired"\nretired = "2026-12-01: the wrapper was removed"\n'
                                                'see = "docs/gone.md"')
        old = old.replace("### Write the completion file from one shared wrapper", "### A heading that was reworded")
        self.assertEqual(self.one(old, drop=("tools/blender/blender_job.py", "tools/tests/test_blender_job.py")), [])

    def test_a_retired_entry_still_closes_the_teardown_it_cites(self):
        old = SENTENCE.replace('status = "active"', 'status = "retired"\nretired = "2026-12-01: replaced"')
        self.assertEqual(self.codes({"band-stack-even-rhythm": old, "blender-job-report": GOOD}), [])


class SeeBringsAProcedureInTests(Case):
    def test_a_see_target_that_no_pattern_finds_becomes_a_record(self):
        see = GOOD.replace('status = "active"', 'status = "active"\nsee = "docs/reference/armory-guide.md#Redo"')
        root, _ = make_root(self.tmp, {"blender-job-report": see},
                            files={"docs/reference/armory-guide.md": "# Armory guide\n\n## Redo\n\n1. Step.\n",
                                   "docs/INDEX.md": "[guide](reference/armory-guide.md)\n"})
        rec = {r["id"]: r for r in kn.procedures(root)}["doc:docs/reference/armory-guide.md"]
        self.assertEqual(rec["entries"], ["blender-job-report"])
        self.assertEqual(rec["title"], "Armory guide")
        self.assertIn("runs its body inside", rec["trigger"])
        self.assertEqual(rec["indexed_by"], ["docs/INDEX.md"])


class GitCase(Case):
    """A real repository in the temp folder, for what only git can answer."""

    def setUp(self):
        super().setUp()
        self.root, _ = make_root(self.tmp)
        self.git("init", "-q")
        self.git("config", "user.email", "t@example.invalid")
        self.git("config", "user.name", "t")
        self.git("config", "core.autocrlf", "false")
        self.git("add", "-A")
        self.git("commit", "-q", "-m", "base")

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.root), *args], capture_output=True, text=True, check=True)

    def write(self, rel, text):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8", newline="\n")

    def staged(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
            code = kn.main(["check", "--staged", "--root", str(self.root)])
        return code, out.getvalue()


class TrackedFilesTests(GitCase):
    def test_a_file_deleted_on_disk_is_not_listed_and_a_new_file_is(self):
        (self.root / "tools" / "blender" / "blender_job.py").unlink()
        self.write("tools/new_tool.py", "x = 1\n")
        self.write(".gitignore", "ignored/\n")
        self.write("ignored/a.py", "x = 1\n")
        listed = kn.tracked_files(self.root)
        self.assertNotIn("tools/blender/blender_job.py", listed)
        self.assertIn("tools/new_tool.py", listed)
        self.assertNotIn("ignored/a.py", listed)
        self.assertIn("tools/tests/test_blender_job.py", listed)

    def test_the_check_on_a_real_repository_sees_the_deleted_enforcer(self):
        self.assertEqual([f.code for f in kn.check(self.root)], [])
        (self.root / "tools" / "blender" / "blender_job.py").unlink()
        self.assertEqual([f.code for f in kn.check(self.root)], ["ref-missing", "trigger-dead"])

    def test_a_folder_that_is_no_repository_means_unchecked(self):
        with tempfile.TemporaryDirectory() as plain:
            self.assertIsNone(kn.tracked_files(Path(plain)))
            self.assertIsNone(kn.index_files(Path(plain)))
            self.assertIsNone(kn.staged_changes(Path(plain)))


class CommitTreeTests(GitCase):
    def test_the_texts_of_a_commit_come_from_git_whatever_the_disk_holds(self):
        odd = "docs/a b/é.md"                          # a space, and a letter outside ASCII, in the path
        self.write(odd, "one\n")
        self.write("docs/empty.md", "")
        self.git("add", "-A")
        self.write(odd, "two\n")                            # the disk moves on; the index does not
        tree = kn.Tree(self.root, set(kn.index_files(self.root)), commit=True)
        tree.fetch([odd, "docs/not-there.md", "docs/empty.md"])
        self.assertEqual(tree.text(odd), "one\n")
        self.assertEqual(tree.text("docs/empty.md"), "")
        with self.assertRaises(OSError):
            tree.text("docs/not-there.md")
        self.assertEqual(tree.text("tools/blender/blender_job.py"), FILES["tools/blender/blender_job.py"])   # no fetch

    def test_the_last_commit_is_a_tree_of_its_own(self):
        self.write("docs/new.md", "x\n")
        self.git("add", "docs/new.md")
        self.write("tools/blender/blender_job.py", "staged = 1\n")
        self.git("add", "tools/blender/blender_job.py")
        head = kn.Tree(self.root, set(kn.head_files(self.root)), commit=True, rev="HEAD")
        self.assertFalse(head.has("docs/new.md"))
        self.assertEqual(head.text("tools/blender/blender_job.py"), FILES["tools/blender/blender_job.py"])
        index = kn.Tree(self.root, set(kn.index_files(self.root)), commit=True)
        self.assertTrue(index.has("docs/new.md"))
        self.assertEqual(index.text("tools/blender/blender_job.py"), "staged = 1\n")

    def test_a_repository_with_no_commit_has_no_last_commit(self):
        with tempfile.TemporaryDirectory() as fresh:
            subprocess.run(["git", "-C", fresh, "init", "-q"], capture_output=True, check=True)
            self.assertIsNone(kn.head_files(Path(fresh)))


class StagedTests(GitCase):
    """`check --staged`, the commit gate. It judges what the commit records: the names and the text both come from the
    git index and never from the work tree, so another session's unfinished work in the same checkout refuses
    nothing. It judges only when the commit can change the verdict, and it refuses only a finding that the last
    commit did not already have."""

    def half_written(self):
        self.write("docs/knowledge/techniques/half-written.toml", "title = \n")

    def stage_an_unrelated_deletion(self):
        """Stage a change that makes the gate judge the whole store and that touches nothing the store names."""
        self.write("docs/unrelated.md", "x\n")
        self.git("add", "docs/unrelated.md")
        self.git("commit", "-q", "-m", "unrelated")
        self.git("rm", "-q", "docs/unrelated.md")

    def break_the_last_commit(self):
        """A finding that HEAD already has: the file an entry names as its gate went in a commit that no gate saw."""
        self.git("rm", "-q", "tools/tests/test_blender_job.py")
        self.git("commit", "-q", "-m", "a commit that no gate saw")

    # The work tree of a shared checkout never decides (the review of 2026-11-26, impact finding 1).

    def test_an_unstaged_break_of_an_entry_does_not_refuse_another_commit(self):
        self.stage_an_unrelated_deletion()
        self.write("docs/knowledge/techniques/blender-job-report.toml", "title = \n")     # another session, mid-edit
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("as the commit records it", out)
        self.assertEqual([f.code for f in kn.check(self.root)], ["toml-invalid"])          # the work tree does see it

    def test_an_unstaged_reword_of_a_cited_heading_does_not_refuse_another_commit(self):
        self.stage_an_unrelated_deletion()
        self.write("docs/reviews/lessons/build-tooling-workflow.md", "# Build\n\n### A heading that was reworded\n")
        code, out = self.staged()
        self.assertEqual(code, 0, out)

    def test_an_unstaged_edit_of_an_enforcer_does_not_refuse_another_commit(self):
        self.stage_an_unrelated_deletion()
        self.write("tools/blender/blender_job.py", "def run(path):\n    return path\n")
        code, out = self.staged()
        self.assertEqual(code, 0, out)

    def test_a_lesson_file_renamed_on_disk_only_does_not_refuse_another_commit(self):
        self.stage_an_unrelated_deletion()
        lessons = self.root / "docs" / "reviews" / "lessons"
        (lessons / "build-tooling-workflow.md").rename(lessons / "build-tooling.md")
        code, out = self.staged()
        self.assertEqual(code, 0, out)

    def test_a_staged_break_is_refused_though_the_disk_copy_is_whole(self):
        entry = "docs/knowledge/techniques/blender-job-report.toml"
        whole = (self.root / entry).read_text(encoding="utf-8")
        self.write(entry, "title = \n")
        self.git("add", entry)
        self.write(entry, whole)                            # repaired on disk, and the repair is not staged
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        self.assertIn("blender-job-report.toml: toml-invalid", out)

    # Only what this commit adds refuses (impact findings 2 and 4).

    def test_a_finding_the_last_commit_already_had_does_not_refuse(self):
        self.break_the_last_commit()
        self.stage_an_unrelated_deletion()
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("ref-missing", out)                   # the tool prints it; the hook shows nothing on exit 0
        self.assertIn("already in the last commit", out)

    def test_a_new_finding_is_refused_though_the_last_commit_had_another(self):
        self.break_the_last_commit()
        self.write("docs/reviews/lessons/build-tooling-workflow.md", "# Build\n\n### A heading that was reworded\n")
        self.git("add", "docs/reviews/lessons/build-tooling-workflow.md")
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        lines = out.splitlines()
        old_at = next(i for i, ln in enumerate(lines) if "already in the last commit" in ln)
        new_at = next(i for i, ln in enumerate(lines) if "source-invalid" in ln)
        self.assertLess(old_at, new_at)                     # the old ones first: the hook shows the end of the output
        self.assertIn("ref-missing", lines[old_at + 1])
        self.assertNotIn("ref-missing", "\n".join(lines[new_at:]))

    def test_a_second_copy_of_an_old_finding_is_refused(self):
        """Two bad rows of one kind print the same line, so the baseline counts lines and does not only hold them."""
        entry = "docs/knowledge/techniques/blender-job-report.toml"
        self.write(entry, GOOD + "\n[[source]]\nissue = 0\n")
        self.git("add", entry)
        self.git("commit", "-q", "-m", "one bad row that no gate saw")
        self.write(entry, GOOD + "\n[[source]]\nissue = 0\n\n[[source]]\nissue = 0\n")
        self.git("add", entry)
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        self.assertIn("1 new finding ", out)

    def test_the_new_finding_and_the_way_out_are_in_the_last_lines_whatever_the_last_commit_held(self):
        """.githooks/pre-commit shows the last 30 lines of a refusal."""
        for i in range(30):
            self.write("docs/knowledge/techniques/old-%02d.toml" % i, "title = \n")
        self.git("add", "docs/knowledge/techniques")
        self.git("commit", "-q", "-m", "thirty broken entries that no gate saw")
        self.write("docs/knowledge/techniques/brand-new.toml", "title = \n")
        self.git("add", "docs/knowledge/techniques/brand-new.toml")
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        tail = "\n".join(out.splitlines()[-30:])
        self.assertIn("brand-new.toml: toml-invalid", tail)
        self.assertIn("docs/knowledge/README.md", tail)
        self.assertIn("30 findings were already in the last commit", out)

    def test_an_intent_to_add_entry_of_another_session_does_not_refuse_a_judged_commit(self):
        """`git add -N` puts a file into the index and not into the commit."""
        self.stage_an_unrelated_deletion()
        self.half_written()
        self.git("add", "-N", "docs/knowledge/techniques/half-written.toml")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertNotIn("half-written", out)

    def test_a_path_commit_is_judged_on_its_own_index(self):
        """`git commit -- <paths>` hands the hook a temporary index in GIT_INDEX_FILE. Another session's staged
        broken entry is in the checkout's index and not in that one."""
        self.half_written()
        self.git("add", "docs/knowledge/techniques/half-written.toml")         # the other session, in the real index
        other = str(Path(self.tmp) / "other-index")
        env = dict(os.environ, GIT_INDEX_FILE=other)
        for args in (("read-tree", "HEAD"), ("rm", "-q", "--cached", "docs/reviews/lessons/build-tooling-workflow.md")):
            subprocess.run(["git", "-C", str(self.root), *args], capture_output=True, text=True, check=True, env=env)
        with mock.patch.dict(os.environ, {"GIT_INDEX_FILE": other}):
            code, out = self.staged()
        self.assertEqual(code, 1, out)
        self.assertIn("source-invalid", out)                # the lesson file this path commit takes out
        self.assertNotIn("half-written", out)
        code, out = self.staged()                           # the checkout's own index holds the other session's entry
        self.assertEqual(code, 1, out)
        self.assertIn("half-written.toml: toml-invalid", out)

    def test_a_commit_that_repairs_an_old_finding_passes_and_has_nothing_to_tell(self):
        self.break_the_last_commit()
        self.write("tools/tests/test_blender_job.py", FILES["tools/tests/test_blender_job.py"])
        self.git("add", "tools/tests/test_blender_job.py")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertNotIn("ref-missing", out)

    def test_a_commit_that_removes_the_store_is_refused(self):
        self.git("rm", "-q", "-r", "docs/knowledge")
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        self.assertIn("store-missing", out)

    def test_a_store_that_the_last_commit_already_lacked_refuses_nothing(self):
        self.git("rm", "-q", "-r", "docs/knowledge")
        self.git("commit", "-q", "-m", "the store went in a commit that no gate saw")
        self.write("src/a.cs", "class A {}\n")
        self.git("add", "src/a.cs")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("store-missing", out)
        self.assertIn("already in the last commit", out)

    def test_the_first_commit_of_a_repository_has_no_baseline_and_is_judged_whole(self):
        with tempfile.TemporaryDirectory() as fresh:
            root, _ = make_root(fresh, {"blender-job-report": GOOD.replace('status = "active"', 'status = "draft"')})
            for args in (("init", "-q"), ("add", "-A")):
                subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=True, check=True)
            out = io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
                code = kn.main(["check", "--staged", "--root", str(root)])
        self.assertEqual(code, 1, out.getvalue())
        self.assertIn("value-invalid", out.getvalue())

    def test_a_refusal_names_the_store_and_the_ways_out(self):
        """A committer who has never heard of the store can act on the refusal (impact finding 3)."""
        self.git("rm", "-q", "tools/blender/blender_job.py")
        code, out = self.staged()
        self.assertEqual(code, 1, out)
        self.assertIn("docs/knowledge/README.md", out)
        self.assertIn("retired", out)
        self.assertIn("knowledge.py show", out)

    def test_a_pass_prints_no_way_out(self):
        self.stage_an_unrelated_deletion()
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertNotIn("docs/knowledge/README.md", out)

    def test_a_commit_that_touches_nothing_the_store_names_is_not_judged(self):
        self.write("README.txt", "x\n")
        self.git("add", "README.txt")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("was not judged", out)

    def test_a_staged_entry_is_judged(self):
        self.half_written()
        self.git("add", "docs/knowledge/techniques/half-written.toml")
        code, out = self.staged()
        self.assertEqual(code, 1)
        self.assertIn("half-written.toml: toml-invalid", out)

    def test_an_untracked_broken_entry_of_another_session_does_not_refuse_a_judged_commit(self):
        self.half_written()
        self.write("tools/blender/blender_job.py", "import sys\n\n\ndef job(path):\n    return path  # a comment\n")
        self.git("add", "tools/blender/blender_job.py")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("as the commit records it", out)
        self.assertEqual([f.code for f in kn.check(self.root)], ["toml-invalid"])      # the work tree does see it

    def test_a_staged_edit_that_removes_the_text_an_entry_names_is_refused(self):
        self.write("tools/blender/blender_job.py", "def run(path):\n    return path\n")
        self.git("add", "tools/blender/blender_job.py")
        code, out = self.staged()
        self.assertEqual(code, 1)
        self.assertIn("ref-text-missing", out)

    def test_a_staged_edit_that_rewords_a_cited_lesson_heading_is_refused(self):
        self.write("docs/reviews/lessons/build-tooling-workflow.md", "# Build\n\n### A heading that was reworded\n")
        self.git("add", "docs/reviews/lessons/build-tooling-workflow.md")
        code, out = self.staged()
        self.assertEqual(code, 1)
        self.assertIn("source-invalid", out)

    def test_a_file_taken_out_of_the_commit_is_gone_though_it_stays_on_disk(self):
        self.git("rm", "-q", "--cached", "tools/blender/blender_job.py")
        self.assertTrue((self.root / "tools" / "blender" / "blender_job.py").is_file())
        code, out = self.staged()
        self.assertEqual(code, 1)
        self.assertIn("ref-missing", out)

    def test_a_file_deleted_on_disk_only_is_still_in_the_commit(self):
        (self.root / "tools" / "blender" / "blender_job.py").unlink()
        self.write("docs/reviews/lessons/build-tooling-workflow.md",
                   "# Build\n\n### Write the completion file from one shared wrapper\nmore text\n")
        self.git("add", "docs/reviews/lessons/build-tooling-workflow.md")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("as the commit records it", out)

    def test_a_staged_deletion_of_any_file_is_judged(self):
        self.write("docs/unrelated.md", "x\n")
        self.git("add", "docs/unrelated.md")
        self.git("commit", "-q", "-m", "unrelated")
        self.git("rm", "-q", "docs/unrelated.md")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("as the commit records it", out)

    def test_a_staged_deletion_of_the_last_file_a_trigger_matched_is_refused(self):
        self.git("rm", "-q", "tools/blender/blender_job.py")
        code, out = self.staged()
        self.assertEqual(code, 1)
        self.assertIn("trigger-dead", out)

    def test_a_staged_change_of_the_tool_is_judged(self):
        self.write("tools/knowledge.py", "# the tool\n")
        self.git("add", "tools/knowledge.py")
        code, out = self.staged()
        self.assertEqual(code, 0, out)
        self.assertIn("as the commit records it", out)

    def test_staged_matters_reads_the_names_and_the_deletions(self):
        store = kn.load(self.root)
        self.assertFalse(kn.staged_matters(store, [("M", "README.txt"), ("A", "docs/x.md")]))
        self.assertTrue(kn.staged_matters(store, [("M", "docs/knowledge/README.md")]))
        self.assertTrue(kn.staged_matters(store, [("M", "tools/knowledge.py")]))
        self.assertTrue(kn.staged_matters(store, [("M", "tools/tests/test_blender_job.py")]))
        self.assertTrue(kn.staged_matters(store, [("M", "docs/reviews/lessons/build-tooling-workflow.md")]))
        self.assertTrue(kn.staged_matters(store, [("D", "anything/at/all.txt")]))

    def test_with_no_repository_the_commit_is_not_judged_and_the_work_tree_is_not_read_in_its_place(self):
        with tempfile.TemporaryDirectory() as plain:
            root, _ = make_root(plain, {"blender-job-report": "title = \n"})       # a finding, if the disk were read
            out = io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
                code = kn.main(["check", "--staged", "--root", str(root)])
        self.assertEqual(code, 2)
        self.assertIn("NOT judged", out.getvalue())
        self.assertNotIn("toml-invalid", out.getvalue())


class EncodingTests(Case):
    """The states a fresh checkout brings: a BOM, CRLF line ends and text outside ASCII (test-workflow.md)."""
    NO_TEARDOWN = ("docs/knowledge/teardowns/2026-11-26-neck.md",)

    def test_a_lesson_file_with_a_bom_and_crlf_still_holds_its_heading(self):
        root, tracked = make_root(self.tmp, {"blender-job-report": GOOD}, drop=self.NO_TEARDOWN)
        lesson = root / "docs" / "reviews" / "lessons" / "build-tooling-workflow.md"
        lesson.write_bytes(b"\xef\xbb\xbf" + lesson.read_bytes().replace(b"\n", b"\r\n"))
        self.assertEqual([f.code for f in kn.check(root, tracked=tracked)], [])

    def test_an_entry_file_with_a_bom_and_crlf_is_read(self):
        root, tracked = make_root(self.tmp, {"blender-job-report": GOOD}, drop=self.NO_TEARDOWN)
        entry = root / "docs" / "knowledge" / "techniques" / "blender-job-report.toml"
        entry.write_bytes(b"\xef\xbb\xbf" + entry.read_bytes().replace(b"\n", b"\r\n"))
        self.assertEqual([f.code for f in kn.check(root, tracked=tracked)], [])
        self.assertEqual(kn.load(root).entries["blender-job-report"].level, "code")

    def test_a_heading_with_a_letter_outside_ascii_is_matched_whole(self):
        heading = "### Write the completion file (Carn D\u00fbm map icon, 2026-10-16)"
        text = GOOD.replace("### Write the completion file from one shared wrapper", heading)
        files = {"docs/reviews/lessons/build-tooling-workflow.md": "# Build\n\n%s\ntext\n" % heading}
        self.assertEqual(self.one(text, files=files, drop=self.NO_TEARDOWN), [])

    def test_a_reference_path_is_held_to_the_case_git_records(self):
        self.assertEqual(self.one(GOOD.replace("tools/blender/blender_job.py::", "tools/Blender/blender_job.py::"),
                                  drop=self.NO_TEARDOWN), ["ref-missing"])


class RunFromAnywhereTests(unittest.TestCase):
    def test_check_finds_the_repository_from_a_subfolder(self):
        here = os.getcwd()
        os.chdir(REPO / "tools" / "tests")
        try:
            out = io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
                code = kn.main(["check"])
        finally:
            os.chdir(here)
        self.assertEqual(code, 0, out.getvalue())


class RealTreeTests(unittest.TestCase):
    """The gate. A floor keeps an empty or unreadable scan from passing as a clean one."""
    ENTRY_FLOOR = 10
    SKILL_FLOOR = 20
    DOC_FLOOR = 15

    def test_the_repositorys_store_has_no_finding(self):
        findings = kn.check(REPO)
        self.assertEqual([str(f) for f in findings], [])

    def test_every_entry_file_was_read_and_there_are_enough_of_them(self):
        store = kn.load(REPO)
        files = sorted((REPO / "docs" / "knowledge" / "techniques").glob("*.toml"))
        self.assertEqual(len(store.entries), len(files))
        self.assertGreaterEqual(len(store.entries), self.ENTRY_FLOOR)

    def test_the_procedure_catalogue_finds_the_skills_and_the_workflow_docs(self):
        records = kn.procedures(REPO)
        self.assertGreaterEqual(sum(1 for r in records if r["kind"] == "skill"), self.SKILL_FLOOR)
        self.assertGreaterEqual(sum(1 for r in records if r["kind"] == "doc"), self.DOC_FLOOR)
        self.assertTrue(all(r["title"] for r in records), [r["id"] for r in records if not r["title"]])

    def test_the_index_of_the_real_store_has_only_six_field_rows(self):
        lines = kn.index_text(kn.load(REPO)).splitlines()
        self.assertIn("skipped=0", lines[0])
        self.assertTrue(all(len(ln.split("\t")) == 6 for ln in lines[1:]))

    #: Files that no entry is about. A trigger that fires on one of these is too broad: on 2026-11-26 one glob
    #: put twelve armour entries on every Python file of every asset job, a virtual environment included.
    SILENT = (
        "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/env/Lib/site-packages/numpy/core/numeric.py",
        "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/wall_build.py",
        "E:/LOTRAOMAssets/_auto_workspace/some_job/tpac_index.py",
        "E:/repos/TAOM/tools/knowledge.py",
        "E:/repos/TAOM/tools/validate_moduledata.py",
        "E:/repos/TAOM/Main/SubModule.cs",
        "E:/repos/TAOM/docs/INDEX.md",
        "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/ModuleData/spitems.xml",
        "C:/Users/someone/project/main.py",
        # Probes, sheets and renders of the armour and cart jobs: an entry names the builder files, not the job.
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_names.py",
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_blendwait.py",
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rharm_render.py",
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rhun2_probe_dims.py",
        # Scripts that only share a word with a builder: a LOD hash, the armour's placement on a cart, a strap probe.
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_lodhash.py",
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rhun2_armour_swap.py",
        "E:/LOTRAOMAssets/_auto_workspace/a_job/rhun2_probe_strapends.py",
    )

    #: Tasks that no entry is about. A task phrase of one common word fires on these: "gold" is also the game's
    #: currency, and "head armour" a troop's equipment slot.
    SILENT_TASKS = (
        "gold reward of the tournament", "write an outline of the plan", "fix wheel rim collision",
        "collar of the shirt", "hill crest spawn", "symmetry of the map", "clipping of the camera",
        "head armour of the troop", "blade damage of the sword", "write the report file", "prototype of the menu",
        "palette of the banner editor", "helm of the lord", "horse armour item values",
        "translate the strap names", "shield strap on an item", "a horse harness with reins",
        "set the culture colour", "chariot set item xml",
    )

    def test_no_entry_fires_on_a_file_it_is_not_about(self):
        store = kn.load(REPO)
        fired = {p: [e.id for e in kn.matching(store, p)] for p in self.SILENT}
        self.assertEqual({p: ids for p, ids in fired.items() if ids}, {})

    def test_no_entry_fires_on_a_task_it_is_not_about(self):
        store = kn.load(REPO)
        fired = {t: [e.id for e in kn.brief_entries(store, [], [t])] for t in self.SILENT_TASKS}
        self.assertEqual({t: ids for t, ids in fired.items() if ids}, {})

    def test_a_brief_for_a_builder_file_holds_the_entry_whose_one_build_lives_in_that_file(self):
        out = kn.brief_text(kn.load(REPO), ["E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_shell.py"], [])
        self.assertIn("- head-shell-one-piece ", out)

    def test_the_entries_fire_where_the_work_happens(self):
        store = kn.load(REPO)
        for path, wanted in (
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_bands.py", "band-end-rounded-and-level"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_shell.py", "head-shell-one-piece"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharm_design2.py", "band-stack-even-rhythm"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_paint.py", "gold-is-a-clean-worked-metal"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_lodnormals.py", "outline-geometry-where-it-shows"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_scales.py", "scale-mail-single-plates-on-a-backing"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rharmb_strapclip.py", "strap-ends-in-a-made-joint"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rhun2_a_blades.py", "blades-are-steel-or-iron"),
                ("E:/LOTRAOMAssets/_auto_workspace/a_job/rhun2_var_f.py", "blades-are-steel-or-iron"),
                ("E:/repos/taom-x/tools/blender/qa_views.py", "blender-job-report"),
                ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/"
                 "LOTRLOME_items/LOTRAOM_horses.xml", "live-module-edit-has-a-repo-spec")):
            with self.subTest(path=path):
                self.assertIn(wanted, [e.id for e in kn.matching(store, path)])


if __name__ == "__main__":
    unittest.main()
