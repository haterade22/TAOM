#!/usr/bin/env python3
"""Tests for tools/analyze_reviews.py: the REVIEW-LOG parser and the per-lens, per-tier yield."""
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import analyze_reviews as ar  # noqa: E402

HEAD = ("| # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | "
        "Missed Bugs | Prompt Version |\n|---|---|---|---|---|---|---|---|---|\n")


def row(first, date, feature, codex, claude, real, fps, missed, prompt):
    return f"| {first} | {date} | {feature} | {codex} | {claude} | {real} | {fps} | {missed} | {prompt} |\n"


class ParseReviewsTests(unittest.TestCase):
    def test_rows_with_a_non_numeric_first_cell_are_parsed(self):
        text = HEAD + row("(at merge)", "2026-10-20", "Dale", "not run", "agree", "2 HIGH, 9 MEDIUM", 0, 0, "none") \
            + row("career-e11", "2026-10-15", "Career", "issues-found", "agree", "3 P3", 0, 0, "gpt-6, xhigh") \
            + row("132b", "2026-09-24", "Plan 009", "issues-found", "agree", 2, 0, 5, "gpt-6, high") \
            + row("7", "2026-04-05", "CustomBattles", "no-ship", "agree", "1 confirmed", 0, 0, "v4")
        self.assertEqual(len(ar.parse_reviews(text)), 4)

    def test_an_exact_duplicate_is_dropped_but_a_reused_number_is_kept(self):
        a = row("17", "2026-04-07", "SpecialResources", "needs-attention", "partial", 2, 1, 0, "v6")
        b = row("17", "2026-04-06", "Arena", "no-ship", "agree", 1, 0, 0, "v6")
        reviews = ar.parse_reviews(HEAD + a + a + b)
        self.assertEqual(sorted(r.feature for r in reviews), ["Arena", "SpecialResources"])

    def test_header_and_non_date_rows_are_skipped(self):
        self.assertEqual(ar.parse_reviews(HEAD), [])

    def test_severity_counts_lift_the_real_total(self):
        text = HEAD + row("(at merge)", "2026-10-20", "Dale", "not run", "agree",
                          "2 HIGH, 9 MEDIUM, 5 LOW", 0, 0, "none")
        r = ar.parse_reviews(text)[0]
        self.assertEqual(r.real_bugs, 16)
        self.assertEqual(r.severity, {"HIGH": 2, "MEDIUM": 9, "LOW": 5})

    def test_a_plain_leading_integer_still_counts(self):
        r = ar.parse_reviews(HEAD + row("3", "2026-04-05", "X", "approve", "agree", "2 confirmed + 1 valid",
                                        0, 0, "v4"))[0]
        self.assertEqual(r.real_bugs, 2)
        self.assertEqual(r.severity, {})

    def test_tier_comes_from_the_prompt_cell(self):
        cases = {"gpt-6-astra, xhigh; RCA x": "xhigh", "gpt-6-astra, max": "max",
                 "gpt-6, high": "high", "v6-adversarial": "none", "none known": "none"}
        for cell, tier in cases.items():
            r = ar.parse_reviews(HEAD + row("(at merge)", "2026-10-01", "F", "issues-found", "agree",
                                            1, 0, 0, cell))[0]
            self.assertEqual(r.tier, tier, cell)

    def test_found_by_is_codex_only_when_codex_ran(self):
        ran = ar.parse_reviews(HEAD + row("1", "2026-04-05", "A", "issues-found (1 P2)", "agree", 1, 0, 0, "v6"))[0]
        skipped = ar.parse_reviews(HEAD + row("2", "2026-04-05", "B", "not run (6 Claude agents)", "agree",
                                              1, 0, 0, "v6"))[0]
        none = ar.parse_reviews(HEAD + row("3", "2026-04-05", "C", "(no Codex pass)", "agree", 1, 0, 0, "v6"))[0]
        self.assertEqual((ran.found_by, skipped.found_by, none.found_by), ("codex", "claude", "claude"))


class ParenthesisedCountTests(unittest.TestCase):
    CLEAN_CELL = ("0 — CLEAN; 7/8 suspects DISPUTED + 1 PARTIAL (Damage stage-move, non-defect); corroborated "
                  "the 6-dim in-house deep-review (0 HIGH/6 MED/3 LOW, all fixed)")

    def test_counts_inside_parentheses_are_not_real_bugs(self):
        r = ar.parse_reviews(HEAD + row("20", "2026-06-26", "Career", "clean/ship", "agree", self.CLEAN_CELL,
                                        0, 0, "adversarial-xhigh"))[0]
        self.assertEqual(r.real_bugs, 0)
        self.assertEqual(r.severity, {})


class TierYieldTests(unittest.TestCase):
    def test_yield_by_tier_sums_real_false_positive_and_severity(self):
        text = HEAD + row("(at merge)", "2026-10-01", "A", "issues-found", "agree", "1 HIGH, 2 MEDIUM", 1, 0, "g, max") \
            + row("(at merge)", "2026-10-02", "B", "issues-found", "agree", "1 LOW", 0, 0, "g, max") \
            + row("(at merge)", "2026-10-03", "C", "issues-found", "agree", "2 MEDIUM", 3, 0, "g, high")
        by_tier = ar.yield_by(ar.parse_reviews(text), "tier")
        self.assertEqual(by_tier["max"]["real"], 4)
        self.assertEqual(by_tier["max"]["fps"], 1)
        self.assertEqual(by_tier["max"]["severity"], {"HIGH": 1, "MEDIUM": 2, "LOW": 1})
        self.assertEqual(by_tier["high"]["fps"], 3)

    def test_yield_by_found_by(self):
        text = HEAD + row("1", "2026-04-05", "A", "issues-found", "agree", 2, 0, 0, "v6") \
            + row("2", "2026-04-05", "B", "not run", "agree", 1, 0, 0, "v6")
        by = ar.yield_by(ar.parse_reviews(text), "found_by")
        self.assertEqual((by["codex"]["real"], by["claude"]["real"]), (2, 1))


RCA = """# RCA

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| F1 | HIGH | a bug | Tooling | c | w | p |
| F2 | HIGH (XML) / MED (Data flow) / P2 (Codex) | b | XML, Data flow, Codex | c | w | p |
| F3 | MED | c | Engine (writer), Codex (O1) | c | w | p |

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F4 | LOW | no found-by column here | c | w | p |
"""


LENS_TABLE = """# RCA

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| 1 | HIGH | a | XML lens, Codex | f |
| 2 | LOW | b | 5 Data flow | f |

| # | Sev | Finding (lens) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 3 | MED | Scale returned the value unscaled (standards 2, data flow 1) | c | w | p |
| 4 | LOW | The drift gate never ran (tooling F4, XML M1, M2) | c | w | p |
"""

PIPED_CODE_SPAN = """| # | Sev | Bug | Found by | Category |
|---|---|---|---|---|
| 12 | LOW | Hook commands with `;`, `&&`, `|` or a quote read the separator as a name | Codex P3 | Tooling |
"""


class RcaLensTests(unittest.TestCase):
    def test_only_tables_with_a_found_by_column_are_read(self):
        rows = ar.parse_rca_findings(RCA)
        self.assertEqual([sorted(r[0]) for r in rows], [["tooling"], ["codex", "data flow", "xml"],
                                                         ["codex", "engine"]])

    def test_severity_is_the_first_grade_in_the_cell(self):
        rows = ar.parse_rca_findings(RCA)
        self.assertEqual([r[1] for r in rows], ["HIGH", "HIGH", "MEDIUM"])

    def test_yield_by_lens_counts_each_credited_lens(self):
        lens = ar.yield_by_lens(ar.parse_rca_findings(RCA))
        self.assertEqual(lens["codex"]["real"], 2)
        self.assertEqual(lens["tooling"]["severity"], {"HIGH": 1})
        self.assertEqual(lens["xml"]["real"], 1)

    def test_lens_and_finding_lens_columns_are_read(self):
        rows = ar.parse_rca_findings(LENS_TABLE)
        self.assertEqual([sorted(r[0]) for r in rows], [["codex", "xml"], ["data flow"],
                                                         ["data flow", "standards"], ["tooling", "xml"]])

    def test_spellings_of_one_lens_count_together(self):
        text = LENS_TABLE + "\n| # | Sev | Found by |\n|---|---|---|\n| 5 | LOW | XML |\n| 6 | LOW | Data-flow |\n"
        lens = ar.yield_by_lens(ar.parse_rca_findings(text))
        self.assertEqual((lens["xml"]["real"], lens["data flow"]["real"]), (3, 3))   # LENS_TABLE rows 1 and 4, plus one

    def test_a_lens_table_without_a_sev_column_is_not_a_findings_table(self):
        text = "| Lens | Result | Why |\n|---|---|---|\n| 3 Efficiency | no issues | none |\n| 6 Design | 0 KEEP | none |\n"
        self.assertEqual(ar.parse_rca_findings(text), [])

    def test_a_lens_key_credits_the_lens_it_numbers(self):
        text = "| # | Sev | Finding | Lens |\n|---|---|---|---|\n| A1 | LOW | x | 2fe F1, 3 F1, 6 P1 |\n"
        self.assertEqual(sorted(ar.parse_rca_findings(text)[0][0]), ["design", "efficiency", "engine"])

    def test_lens_keys_cover_the_forms_the_rcas_use(self):
        cases = {"2cc F2, 5cc #5, 6 P3": ["data flow", "design", "engine"],
                 "7 #7, #8": ["xml"],
                 "2fe (UNVERIFIED), 3": ["efficiency", "engine"],
                 "Deep-review Agent 5 (container half) + Codex (O1)": ["codex", "data flow"],
                 "Architect design pass": ["design"],
                 "Elegance": ["design"],
                 "Data flow nit": ["data flow"],
                 "dataflow": ["data flow"],
                 "completeness 2, 3, 9": ["completeness"],
                 "completeness 4, 5; design P2, P3; standards 4, 5": ["completeness", "design", "standards"]}
        for cell, want in cases.items():
            self.assertEqual(sorted(ar.clean_lenses(cell)), want, cell)

    def test_a_cell_with_no_lens_word_gets_no_credit(self):
        for cell in ("this session", "all lenses", "seed-only review F1", "Orchestrator", "step 2c", "12 items"):
            self.assertEqual(ar.clean_lenses(cell), [], cell)

    def test_the_heading_counts_findings_with_no_lens_credit(self):
        out = ar.render_yield([], [(["xml"], "LOW"), ([], "LOW"), ([], "?")], (1, 1))
        self.assertIn("3 findings from 1 of 1 RCAs, 2 findings with no lens credit", out)

    def test_sole_finder_counts_rows_where_that_lens_alone_is_credited(self):
        lens = ar.yield_by_lens([(["xml"], "LOW"), (["xml", "engine"], "LOW"), (["engine"], "HIGH")])
        self.assertEqual((lens["xml"]["real"], lens["xml"]["sole"]), (2, 1))
        self.assertEqual((lens["engine"]["real"], lens["engine"]["sole"]), (2, 1))
        self.assertIn("sole=  1", ar.render_yield([], [(["xml"], "LOW"), (["xml", "engine"], "LOW")], (1, 1)))

    def test_a_pipe_inside_a_code_span_does_not_split_the_cell(self):
        rows = ar.parse_rca_findings(PIPED_CODE_SPAN)
        self.assertEqual(rows, [(["codex"], "LOW")])

    def test_split_cells_keeps_a_code_span_whole(self):
        self.assertEqual(ar.split_cells("| a | `x | y` | b |"), ["a", "`x | y`", "b"])


class SkippedRowTests(unittest.TestCase):
    SHORT = "| 35 | 2026-05-06 | Phase 1 | ISSUES FOUND | agree | 4 confirmed | 0 | v6 |\n"

    def test_a_row_with_too_few_cells_is_counted(self):
        stats = {}
        reviews = ar.parse_reviews(HEAD + self.SHORT + row("1", "2026-04-05", "A", "approve", "agree", 1, 0, 0, "v6"), stats)
        self.assertEqual(len(reviews), 1)
        self.assertEqual(stats["short"], 1)

    def test_a_prose_missed_cell_is_counted_but_none_is_not(self):
        text = HEAD + row("(at merge)", "2026-10-18", "A", "issues-found", "agree", 1, 0, "a convergence pass added D1", "v6") \
            + row("(at merge)", "2026-10-19", "B", "issues-found", "agree", 1, 0, "none known", "v6") \
            + row("(at merge)", "2026-10-20", "C", "issues-found", "agree", 1, 0, "2 later", "v6")
        stats = {}
        ar.parse_reviews(text, stats)
        self.assertEqual(stats["prose_missed"], 1)

    def test_main_names_both_counts_on_one_stderr_line(self):
        text = HEAD + self.SHORT + row("(at merge)", "2026-10-18", "A", "issues-found", "agree", 1, 0, "prose here", "v6")
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / "REVIEW-LOG.md"
            log.write_text(text, encoding="utf-8")
            (Path(tmp) / "rca-x.md").write_text(RCA, encoding="utf-8")
            (Path(tmp) / "rca-y.md").write_text("# no tables\n", encoding="utf-8")
            err, out = io.StringIO(), io.StringIO()
            with mock.patch.object(ar, "REVIEW_LOG", log), mock.patch("sys.stderr", err), mock.patch("sys.stdout", out):
                self.assertEqual(ar.main(["--no-plot"]), 0)
        warn = [line for line in err.getvalue().splitlines() if "skipped" in line]
        self.assertEqual(len(warn), 1)
        self.assertIn("1 row(s) skipped", warn[0])
        self.assertIn("1 row(s) hold Missed Bugs as prose", warn[0])
        self.assertIn("from 1 of 2 RCAs", out.getvalue())


class LiveLogTests(unittest.TestCase):
    def test_the_real_log_parses_the_at_merge_rows(self):
        reviews = ar.parse_reviews(ar.REVIEW_LOG.read_text(encoding="utf-8", errors="replace"))
        self.assertTrue(any(r.label == "(at merge)" for r in reviews))
        self.assertGreater(len(reviews), 80)


if __name__ == "__main__":
    unittest.main()
