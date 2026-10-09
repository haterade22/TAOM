"""analyze_war_ledger: reads the [WarLedger] lines and measures how close the AI war stays to a stalemate.

Builds small synthetic logs in a temp dir, so it runs anywhere with no game install and no real logs.
Covers the parser (logger prefix, malformed lines, unknown version), the reload de-duplication, every
metric, the two target checks, the --csv and --compare outputs and the exit codes.

The line helpers below are modelled on WarLedgerFormatter. The real contract is the shared fixture
tools/tests/fixtures/war_ledger_v1.txt, which WarLedgerFormatterTests pins the C# formatter to and
test_fixture_lines_parse_to_their_values parses with the real analyzer.
"""
import csv
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import analyze_war_ledger as awl  # noqa: E402

PFX = "[2026-10-08 12:00:00] [INFO] "
FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "war_ledger_v1.txt")


def kl(day, k, side="free", pts=10, tier=0, cap="1", base="na", cid="c1", v="1", drop=None):
    line = (f"{PFX}[WarLedger] v={v} t=kingdom cid={cid} day={day} phase=FullWar k={k} side={side} "
            f"ai=1 war=1 towns=3 castles=1 pts={pts} base={base} loss=0.500 cap={cap} str=100 pris=2 "
            f"tier={tier} vr=1.00 pe=1.00")
    if drop:
        line = " ".join(t for t in line.split(" ") if not t.startswith(drop + "="))
    return line


def kdays(days, k="a", **kw):
    return [kl(d, k, **kw) for d in days]


def sl(day, side, pts, base="na", alive=3, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=side cid={cid} day={day} side={side} pts={pts} base={base} alive={alive}"


def sh(day, share, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=share cid={cid} day={day} share={share}"


def ev_destroyed(day, k, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=event cid={cid} day={day} ev=destroyed k={k}"


def ev_chron(day, eid, outcome, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=event cid={cid} day={day} ev=chronicle id={eid} outcome={outcome}"


def ev_tier(day, k, frm, to, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=event cid={cid} day={day} ev=tier k={k} from={frm} to={to} loss=0.700"


def parse(lines):
    return awl.parse_lines(lines)


def metrics(lines, cid="c1"):
    return awl.compute_metrics(parse(lines).campaigns[cid])


class ParserTests(unittest.TestCase):
    def test_prefix_is_ignored(self):
        p = parse(["noise line", kl(1, "gondor")])
        self.assertEqual(list(p.campaigns), ["c1"])
        self.assertEqual(p.malformed, 0)
        self.assertEqual(p.campaigns["c1"].kingdoms[(1, "gondor")]["pts"], "10")

    def test_malformed_lines_are_skipped_and_counted(self):
        bad_missing = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=3"
        bad_number = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=x share=0.5"
        bad_float = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=4 share=abc"
        p = parse([bad_missing, bad_number, bad_float, sh(5, "0.5")])
        self.assertEqual(p.malformed, 3)
        self.assertEqual(list(p.campaigns["c1"].shares), [5])

    def test_first_three_malformed_lines_are_kept_verbatim(self):
        bad = [f"{PFX}[WarLedger] v=1 t=share cid=c1 day={d}" for d in range(5)]
        p = parse(bad)
        self.assertEqual(p.malformed, 5)
        self.assertEqual(p.malformed_samples, bad[:3])

    def assert_malformed(self, line):
        p = parse([line])
        self.assertEqual((p.ledger_lines, p.malformed), (1, 1), line)
        self.assertEqual(p.campaigns, {}, line)

    def test_non_finite_shares_are_malformed(self):
        for text in ("nan", "inf", "-inf"):
            self.assert_malformed(sh(6, text))

    def test_non_integer_points_are_malformed(self):
        self.assert_malformed(kl(1, "a", pts="x"))

    def test_non_integer_tier_is_malformed(self):
        self.assert_malformed(kl(1, "a", tier="x"))

    def test_kingdom_line_missing_cap_is_malformed(self):
        self.assert_malformed(kl(1, "a", drop="cap"))

    def test_tier_event_without_to_is_malformed(self):
        self.assert_malformed(ev_tier(3, "a", 0, 1).replace(" to=1", ""))

    def test_token_without_equals_is_malformed(self):
        self.assert_malformed(f"{PFX}[WarLedger] v=1 t=share cid=c1 day=3 share")

    def test_unknown_record_type_is_malformed(self):
        self.assert_malformed(f"{PFX}[WarLedger] v=1 t=bogus cid=c1 day=3")

    def test_unknown_event_kind_is_malformed(self):
        self.assert_malformed(f"{PFX}[WarLedger] v=1 t=event cid=c1 day=3 ev=bogus")

    def test_side_line_with_a_bad_base_is_malformed(self):
        self.assert_malformed(sl(1, "free", 5, base="4x"))

    def test_unknown_version_raises(self):
        with self.assertRaises(awl.UnknownVersion) as ctx:
            parse([kl(1, "gondor", v="2")])
        self.assertIn("2", str(ctx.exception))

    def test_reload_keeps_last_line(self):
        p = parse([kl(5, "gondor", pts=10), sh(5, "0.9"), sl(5, "free", 1), ev_destroyed(5, "x"),
                   kl(5, "gondor", pts=99), sh(5, "0.5"), sl(5, "free", 2), ev_destroyed(5, "x")])
        c = p.campaigns["c1"]
        self.assertEqual(c.kingdoms[(5, "gondor")]["pts"], "99")
        self.assertEqual(c.shares[5], 0.5)
        self.assertEqual(c.sides[(5, "free")]["pts"], "2")
        self.assertEqual(len(c.events), 1)

    def test_two_cids_reported_separately(self):
        lines = [sh(1, "0.5", cid="a"), sh(2, "0.5", cid="a"), sh(1, "0.9", cid="b")]
        p = parse(lines)
        self.assertEqual(sorted(p.campaigns), ["a", "b"])
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "x.log")
            with open(path, "w", encoding="utf-8") as f:
                f.write("\n".join(lines))
            out = io.StringIO()
            with redirect_stdout(out):
                code = awl.main([path])
            self.assertEqual(code, 0)
            self.assertIn("cid a", out.getvalue())
            self.assertIn("cid b", out.getvalue())
            out = io.StringIO()
            with redirect_stdout(out):
                awl.main([path, "--cid", "b"])
            self.assertNotIn("cid a", out.getvalue())


class FixtureTests(unittest.TestCase):
    """The shared contract with the C# formatter: tools/tests/fixtures/war_ledger_v1.txt."""

    def fixture_lines(self):
        with open(FIXTURE, encoding="utf-8") as f:
            return [ln for ln in f.read().splitlines() if ln]

    def test_fixture_lines_parse_to_their_values(self):
        lines = self.fixture_lines()
        logged = [f"{PFX}{ln}\r\n" for ln in lines]
        p = parse(logged)
        self.assertEqual((p.ledger_lines, p.malformed), (len(lines), 0))
        c = p.campaigns["c1"]

        full = c.kingdoms[(12, "empire_w")]
        self.assertEqual((full["pts"], full["cap"], full["tier"], full["base"]), ("7", "1", "1", "10"))
        bare = c.kingdoms[(0, "rebels")]
        self.assertEqual((bare["base"], bare["loss"], bare["cap"]), ("na", "na", "na"))

        evil = c.sides[(12, "evil")]
        self.assertEqual((evil["pts"], evil["base"]), ("31", "40"))
        self.assertEqual(c.sides[(5, "neutral")]["base"], "na")

        self.assertEqual(c.shares, {12: 0.524, 13: 0.0, 14: None})

        by_kind = {}
        for _, f in c.events:
            by_kind.setdefault(f["ev"], []).append(f)
        self.assertEqual(len(by_kind["tier"]), 2)
        self.assertEqual([f["k"] for f in by_kind["destroyed"]], ["vlandia"])
        self.assertEqual((by_kind["chronicle"][0]["id"], by_kind["chronicle"][0]["outcome"]), ("hornburg", "Held"))
        clamped = [f for f in by_kind["tier"] if f["k"] == "k"][0]
        self.assertEqual((clamped["from"], clamped["to"], clamped["loss"]), ("0", "2", "0.000"))

    def test_net_change_uses_points_when_the_base_is_na(self):
        m = awl.compute_metrics(parse(self.fixture_lines()).campaigns["c1"])
        self.assertEqual(m["net_change"]["evil"], 31 - 40)
        self.assertEqual(m["net_change"]["neutral"], 0)


class MetricTests(unittest.TestCase):
    def test_days_covered(self):
        m = metrics([sh(3, "0.5"), sh(4, "0.5"), sh(9, "0.5")])
        self.assertEqual((m["first_day"], m["last_day"], m["day_count"]), (3, 9, 3))

    def test_share_metrics_with_na(self):
        m = metrics([sh(0, "0.5"), sh(1, "0.75"), sh(2, "na"), sh(3, "0.3"), sh(4, "0.55")])
        self.assertEqual(m["share_days"], 4)
        self.assertEqual(m["na_days"], 1)
        self.assertAlmostEqual(m["mean_dev"], (0.0 + 0.25 + 0.2 + 0.05) / 4)
        self.assertAlmostEqual(m["max_dev"], 0.25)
        self.assertEqual(m["max_dev_day"], 1)
        self.assertAlmostEqual(m["in_band_fraction"], 2 / 4)

    def test_band_edges_are_inclusive(self):
        m = metrics([sh(1, "0.400"), sh(2, "0.600"), sh(3, "0.900")])
        self.assertAlmostEqual(m["in_band_fraction"], 2 / 3)
        m = metrics([sh(1, "0.399"), sh(2, "0.601")])
        self.assertEqual(m["in_band_fraction"], 0.0)

    def test_lead_change_needs_the_dead_band(self):
        wobble = [sh(i, s) for i, s in enumerate(["0.55", "0.49", "0.51", "0.47", "0.5", "0.53"])]
        # 0.55 (+) -> 0.49 in band -> 0.51 in band -> 0.47 (-): one change; then 0.53 (+): second
        self.assertEqual(metrics(wobble)["lead_changes"], 2)
        inside = [sh(i, s) for i, s in enumerate(["0.51", "0.49", "0.52", "0.48", "0.5"])]
        self.assertEqual(metrics(inside)["lead_changes"], 0)
        same_side = [sh(i, s) for i, s in enumerate(["0.6", "0.5", "0.7"])]
        self.assertEqual(metrics(same_side)["lead_changes"], 0)

    def test_first_capital_loss(self):
        lines = [kl(1, "gondor", cap="1"), kl(2, "gondor", cap="na"), kl(3, "gondor", cap="0"),
                 kl(4, "gondor", cap="0"),
                 kl(1, "mordor", side="evil", cap="1"), kl(2, "mordor", side="evil", cap="0"),
                 kl(1, "rohan", cap="na"), kl(2, "rohan", cap="0")]
        m = metrics(lines)
        self.assertEqual(m["capital_loss"], {"gondor": 3, "mordor": 2})
        self.assertEqual(m["capital_loss_side"], {"free": 3, "evil": 2})
        self.assertEqual(m["earliest_capital_loss"], 2)

    def test_a_recaptured_capital_keeps_the_first_loss_day(self):
        m = metrics([kl(1, "a", cap="1"), kl(2, "a", cap="0"), kl(3, "a", cap="1"), kl(4, "a", cap="0")])
        self.assertEqual(m["capital_loss"], {"a": 2})
        self.assertEqual(m["earliest_capital_loss"], 2)

    def test_earliest_loss_per_side_is_the_minimum_whatever_the_order(self):
        lines = []
        for k, lost in (("a", 50), ("b", 20), ("c", 70)):
            lines += [kl(1, k, cap="1"), kl(lost, k, cap="0")]
        self.assertEqual(metrics(lines)["capital_loss_side"], {"free": 20})

    def test_destroyed_events(self):
        m = metrics([ev_destroyed(40, "rohan"), ev_destroyed(70, "mordor")])
        self.assertEqual(m["destroyed"], [(40, "rohan"), (70, "mordor")])

    def test_churn_buckets(self):
        lines = [kl(0, "a", pts=10), kl(5, "a", pts=14), kl(10, "a", pts=11), kl(19, "a", pts=11),
                 kl(0, "b", pts=3), kl(12, "b", pts=8)]
        m = metrics(lines)
        # a: day5 +4 (bucket 0), day10 -3 (bucket 1), day19 0; b: day12 +5 (bucket 1)
        self.assertEqual(m["churn"], {0: 4, 1: 8})

    def test_tier_days_and_changes(self):
        lines = [kl(1, "a", tier=0), kl(2, "a", tier=1), kl(3, "a", tier=2), kl(4, "a", tier=2),
                 kl(5, "a", tier=1), kl(6, "a", tier=0)]
        t = metrics(lines)["tiers"]["a"]
        self.assertEqual((t["tier1_days"], t["tier2_days"], t["changes"]), (2, 2, 4))

    def test_net_change_per_side(self):
        lines = [sl(1, "free", 100, base="90"), sl(9, "free", 80, base="90"),
                 sl(1, "evil", 50), sl(9, "evil", 70)]
        net = metrics(lines)["net_change"]
        self.assertEqual(net["free"], -10)
        self.assertEqual(net["evil"], 20)

    def test_chronicle_table(self):
        m = metrics([ev_chron(30, "ev_b", "Fell"), ev_chron(10, "ev_a", "Held")])
        self.assertEqual(m["chronicle"], [(10, "ev_a", "Held"), (30, "ev_b", "Fell")])


class TargetTests(unittest.TestCase):
    def test_target_checks_pass(self):
        shares = [sh(i, "0.5") for i in range(10)]
        m = metrics(shares + kdays(range(1, 131)) + [kl(131, "a", cap="0")])
        self.assertTrue(m["target_share_pass"])
        self.assertEqual(m["target_capital"], "PASS")

    def test_target_checks_fail(self):
        shares = [sh(i, "0.5") for i in range(7)] + [sh(i, "0.9") for i in range(7, 10)]
        m = metrics(shares + [kl(10, "a", cap="1"), kl(11, "a", cap="0")])
        self.assertFalse(m["target_share_pass"])
        self.assertEqual(m["target_capital"], "FAIL")
        self.assertIn("FAIL", awl.format_report("c1", m))

    def test_exactly_eighty_percent_in_band_passes(self):
        shares = [sh(i, "0.5") for i in range(8)] + [sh(i, "0.9") for i in range(8, 10)]
        m = metrics(shares)
        self.assertAlmostEqual(m["in_band_fraction"], 0.8)
        self.assertTrue(m["target_share_pass"])
        shares = [sh(i, "0.5") for i in range(7)] + [sh(i, "0.9") for i in range(7, 10)]
        self.assertFalse(metrics(shares)["target_share_pass"])

    def test_capital_target_without_a_capital_state_is_no_data(self):
        self.assertEqual(metrics([sh(1, "0.5")])["target_capital"], "NO DATA")
        self.assertEqual(metrics(kdays(range(1, 130), cap="na"))["target_capital"], "NO DATA")

    def test_capital_target_on_a_log_starting_mid_campaign_is_unknown(self):
        m = metrics([kl(150, "gondor", cap="0"), kl(151, "gondor", cap="0")])
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("first missing day 1", m["target_capital_reason"])

    def test_capital_target_on_a_run_ending_before_day_120_is_unknown(self):
        self.assertEqual(metrics(kdays(range(1, 61)))["target_capital"], "UNKNOWN")
        self.assertEqual(metrics([kl(1, "a", cap="1")])["target_capital"], "UNKNOWN")

    def test_capital_target_with_a_gap_before_day_120_is_unknown(self):
        m = metrics(kdays(range(1, 111)) + kdays(range(130, 141), cap="0"))
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("first missing day 111", m["target_capital_reason"])

    def test_no_capital_loss_through_day_119_passes(self):
        m = metrics(kdays(range(1, 120)))
        self.assertEqual(m["target_capital"], "PASS")
        self.assertIn("PASS", awl.format_report("c1", m))
        self.assertEqual(metrics(kdays(range(1, 119)))["target_capital"], "UNKNOWN")

    def test_capital_lost_on_day_120_passes_and_on_day_119_fails(self):
        self.assertEqual(metrics(kdays(range(1, 120)) + [kl(120, "a", cap="0")])["target_capital"], "PASS")
        self.assertEqual(metrics(kdays(range(1, 119)) + [kl(119, "a", cap="0")])["target_capital"], "FAIL")

    def test_observed_loss_before_day_120_fails_without_full_coverage(self):
        m = metrics([kl(50, "a", cap="1"), kl(51, "a", cap="0")])
        self.assertEqual(m["target_capital"], "FAIL")
        self.assertIn("day 51", m["target_capital_reason"])


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def write(self, name, lines):
        path = os.path.join(self.tmp.name, name)
        with open(path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        return path

    def run_main(self, argv):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = awl.main(argv)
        return code, out.getvalue(), err.getvalue()

    def test_unknown_version_exits_2(self):
        path = self.write("a.log", [kl(1, "a", v="7")])
        code, _, err = self.run_main([path])
        self.assertEqual(code, 2)
        self.assertIn("7", err)

    def test_empty_input_exits_1(self):
        path = self.write("a.log", ["nothing here", "still nothing"])
        code, out, err = self.run_main([path])
        self.assertEqual(code, 1)
        self.assertIn("No [WarLedger]", out + err)

    def test_all_malformed_log_says_so(self):
        bad = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=3"
        path = self.write("a.log", [bad, bad])
        code, out, _ = self.run_main([path])
        self.assertEqual(code, 1)
        self.assertIn("2 [WarLedger] lines, all malformed", out)
        code, out, err = self.run_main(["--compare", path, path])
        self.assertEqual(code, 1)
        self.assertIn("2 [WarLedger] lines, all malformed", out + err)

    def test_compare_on_a_file_with_no_ledger_lines_exits_1(self):
        path = self.write("a.log", ["nothing here"])
        good = self.write("b.log", [sh(1, "0.5")])
        code, out, err = self.run_main(["--compare", path, good])
        self.assertEqual(code, 1)
        self.assertIn("No [WarLedger] lines found in", out + err)

    def test_summary_is_ascii_and_reports_malformed(self):
        path = self.write("a.log", [sh(1, "0.5"), f"{PFX}[WarLedger] v=1 t=share cid=c1"])
        code, out, _ = self.run_main([path])
        self.assertEqual(code, 0)
        out.encode("ascii")
        self.assertIn("malformed lines skipped: 1", out)

    def test_malformed_lines_warn_before_any_verdict(self):
        bad = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=3"
        path = self.write("a.log", [sh(1, "0.5"), bad])
        code, out, _ = self.run_main([path])
        self.assertEqual(code, 0)
        warning = out.index("WARNING: 1 of 2 [WarLedger] lines malformed and skipped; first: ")
        self.assertIn(bad, out)
        self.assertLess(warning, out.index("Target check"))

    def test_drifted_kingdom_key_warns_and_is_not_pass(self):
        drifted = [kl(d, "gondor").replace(" castles=", " fortresses=") for d in range(1, 130)]
        path = self.write("a.log", [sh(1, "0.5")] + drifted)
        code, out, _ = self.run_main([path])
        self.assertEqual(code, 0)
        self.assertIn("WARNING: 129 of 130", out)
        self.assertIn("NO DATA", out)
        self.assertNotIn("(b) earliest capital loss on day >= 120: PASS", out)

    def test_compare_reports_each_sides_malformed_lines(self):
        bad = f"{PFX}[WarLedger] v=1 t=share cid=c1 day=3"
        a = self.write("a.log", [sh(1, "0.5"), bad])
        b = self.write("b.log", [sh(1, "0.5"), bad, bad])
        code, out, _ = self.run_main(["--compare", a, b])
        self.assertEqual(code, 0)
        self.assertIn("WARNING: A: 1 of 2", out)
        self.assertIn("WARNING: B: 2 of 3", out)
        self.assertIn("target A: share PASS, capital NO DATA", out)

    def test_csv_output(self):
        path = self.write("a.log", [sh(1, "0.4"), sh(2, "na"), sl(1, "free", 7), sl(1, "evil", 5),
                                    sl(1, "neutral", 2)])
        csv_path = os.path.join(self.tmp.name, "out.csv")
        code, _, _ = self.run_main([path, "--csv", csv_path])
        self.assertEqual(code, 0)
        with open(csv_path, newline="") as f:
            rows = list(csv.reader(f))
        self.assertEqual(rows[0], ["cid", "day", "share", "free_pts", "evil_pts", "neutral_pts"])
        self.assertEqual(rows[1], ["c1", "1", "0.4", "7", "5", "2"])
        self.assertEqual(rows[2][:3], ["c1", "2", "na"])

    def test_unwritable_csv_is_an_error_not_a_traceback(self):
        path = self.write("a.log", [sh(1, "0.4")])
        missing = os.path.join(self.tmp.name, "no_such_dir", "out.csv")
        code, _, err = self.run_main([path, "--csv", missing])
        self.assertEqual(code, 1)
        self.assertIn("cannot write", err)

    def test_unknown_cid_exits_1(self):
        path = self.write("a.log", [sh(1, "0.5")])
        code, _, err = self.run_main([path, "--cid", "zz"])
        self.assertEqual(code, 1)
        self.assertIn("cid zz not found", err)

    def test_no_default_log_exits_1(self):
        saved = awl.DEFAULT_LOG_GLOB
        awl.DEFAULT_LOG_GLOB = os.path.join(self.tmp.name, "nothing_*.log")
        self.addCleanup(setattr, awl, "DEFAULT_LOG_GLOB", saved)
        code, out, _ = self.run_main([])
        self.assertEqual(code, 1)
        self.assertIn("no log files match", out)

    def test_compare_shows_difference(self):
        a = self.write("a.log", [sh(i, "0.5") for i in range(4)])
        b = self.write("b.log", [sh(i, "0.5") for i in range(2)] + [sh(i, "0.9") for i in range(2, 4)])
        code, out, _ = self.run_main(["--compare", a, b])
        self.assertEqual(code, 0)
        self.assertIn("max_dev", out)
        line = [ln for ln in out.splitlines() if ln.startswith("max_dev")][0]
        self.assertIn("+0.4", line)

    def test_compare_picks_cid_with_most_days(self):
        a = self.write("a.log", [sh(1, "0.5", cid="x"), sh(1, "0.5", cid="y"), sh(2, "0.5", cid="y")])
        b = self.write("b.log", [sh(1, "0.5", cid="x")])
        code, out, _ = self.run_main(["--compare", a, b])
        self.assertEqual(code, 0)
        self.assertIn("using cid y", out)

    def test_compare_path_cid_suffix(self):
        a = self.write("a.log", [sh(1, "0.5", cid="x"), sh(1, "0.5", cid="y"), sh(2, "0.5", cid="y")])
        code, out, _ = self.run_main(["--compare", a + ":x", a + ":y"])
        self.assertEqual(code, 0)
        self.assertIn("day_count", out)
        self.assertIn(f"A: {a} (cid x)", out)
        self.assertIn(f"B: {a} (cid y)", out)

    def test_compare_labels_name_the_days_each_side_covers(self):
        a = self.write("a.log", [sh(3, "0.5"), sh(9, "0.5")])
        _, out, _ = self.run_main(["--compare", a, a])
        self.assertIn("days 3 to 9", out)

    def test_compare_with_an_unknown_cid_names_it(self):
        a = self.write("a.log", [sh(1, "0.5", cid="x")])
        code, _, err = self.run_main(["--compare", a + ":zz", a])
        self.assertEqual(code, 1)
        self.assertIn("cid zz not found", err)

    def test_compare_golden_target_lines(self):
        good = self.write("a.log", [sh(i, "0.5") for i in range(10)] + kdays(range(1, 120)))
        bad = self.write("b.log", [sh(i, "0.9") for i in range(10)] + [kl(4, "a", cap="1"), kl(5, "a", cap="0")])
        _, out, _ = self.run_main(["--compare", good, bad])
        self.assertIn("target A: share PASS, capital PASS", out)
        self.assertIn("target B: share FAIL, capital FAIL", out)

    def test_compare_with_other_options_is_a_usage_error(self):
        a = self.write("a.log", [sh(1, "0.5")])
        csv_path = os.path.join(self.tmp.name, "cmp.csv")
        for extra in (["--csv", csv_path], ["--cid", "x"], [a]):
            with redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as cm:
                awl.main(["--compare", a, a] + extra)
            self.assertEqual(cm.exception.code, 2, extra)
        self.assertFalse(os.path.exists(csv_path))

    def test_split_path_cid_keeps_drive_letter(self):
        self.assertEqual(awl.split_path_cid(r"C:\logs\a.log"), (r"C:\logs\a.log", None))
        self.assertEqual(awl.split_path_cid(r"C:\logs\a.log:run2"), (r"C:\logs\a.log", "run2"))


class ReportTests(unittest.TestCase):
    def test_report_lines_for_every_section(self):
        lines = [kl(0, "a", pts=10, tier=0, cap="1"), kl(5, "a", pts=14, tier=1, cap="1"),
                 kl(10, "a", pts=11, tier=2, cap="0"), kl(19, "a", pts=11, tier=2, cap="0"),
                 sl(1, "free", 100, base="90"), sl(9, "free", 80, base="90"),
                 sl(1, "evil", 50), sl(9, "evil", 70)]
        text = awl.format_report("c1", metrics(lines))
        out = text.splitlines()
        for expected in ("  a: day 10", "  earliest free: day 10", "  days 0-9: 4", "  days 10-19: 3",
                         "  a: 1, 2, 2", "  free: -10", "  evil: +20"):
            self.assertIn(expected, out)
        self.assertIn("a capture counts for both kingdoms", text)

    def test_report_names_the_capital_verdict_and_its_reason(self):
        text = awl.format_report("c1", metrics([kl(150, "a", cap="0")]))
        self.assertIn("(b) earliest capital loss on day >= 120: UNKNOWN (", text)


class OrderingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def write(self, name, lines, mtime=None):
        path = os.path.join(self.tmp.name, name)
        with open(path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        if mtime is not None:
            os.utime(path, (mtime, mtime))
        return path

    def test_logs_are_ordered_oldest_first_by_their_name_stamp(self):
        older = self.write("taom_debug_2026-10-08_10-00-00.log", [sh(5, "0.9")])
        newer = self.write("taom_debug_2026-10-08_11-00-00.log", [sh(5, "0.5")])
        self.assertEqual(awl.order_logs([newer, older]), [older, newer])

    def test_newer_log_wins_whatever_the_argument_order(self):
        older = self.write("taom_debug_2026-10-08_10-00-00.log", [sh(5, "0.9")])
        newer = self.write("taom_debug_2026-10-08_11-00-00.log", [sh(5, "0.5")])
        for order in ([older, newer], [newer, older]):
            self.assertEqual(awl.read_files(order).campaigns["c1"].shares[5], 0.5)

    def test_unstamped_logs_fall_back_to_modified_time_and_a_repeat_is_dropped(self):
        old = self.write("x.log", [sh(5, "0.9")], mtime=1_000_000_000)
        new = self.write("y.log", [sh(5, "0.5")], mtime=1_100_000_000)
        self.assertEqual(awl.order_logs([new, old, new]), [old, new])


if __name__ == "__main__":
    unittest.main()
