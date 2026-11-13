"""analyze_war_ledger: reads the [WarLedger] lines and measures how close the AI war stays to a stalemate.

Builds small synthetic logs in a temp dir, so it runs anywhere with no game install and no real logs.
Covers the parser (logger prefix, malformed lines, unknown version), the reload de-duplication and rewind, every
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


def ev_pledge(day, k, side, by="ai", cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=event cid={cid} day={day} ev=pledge k={k} side={side} by={by}"


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

    def test_pledge_event_without_its_keys_is_malformed(self):
        self.assert_malformed(ev_pledge(3, "cardolan", "free").replace(" by=ai", ""))
        self.assert_malformed(ev_pledge(3, "cardolan", "free").replace(" side=free", ""))
        self.assert_malformed(ev_pledge(3, "cardolan", "free").replace(" k=cardolan", ""))

    def test_pledge_event_to_a_side_nobody_joins_is_malformed(self):
        self.assert_malformed(ev_pledge(3, "cardolan", "neutral"))
        self.assert_malformed(ev_pledge(3, "cardolan", "good"))

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
        self.assertEqual([(f["k"], f["side"], f["by"]) for f in by_kind["pledge"]], [("cardolan", "free", "ai")])
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

    # War Pledge (#1113): a Neutral realm joined a side. One line per join, listed by day.
    def test_pledge_events(self):
        m = metrics([ev_pledge(90, "rhudaur", "evil"), ev_pledge(60, "cardolan", "free", by="console")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "console"), (90, "rhudaur", "evil", "ai")])

    def test_a_repeated_pledge_line_counts_once(self):
        m = metrics([ev_pledge(60, "cardolan", "free"), ev_pledge(60, "cardolan", "free")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "ai")])

    def test_a_load_before_a_pledge_drops_it(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=50"
        m = metrics([ev_pledge(40, "cardolan", "free"), ev_pledge(60, "rhudaur", "evil"), load])
        self.assertEqual(m["pledges"], [(40, "cardolan", "free", "ai")])

    def test_report_lists_the_pledges(self):
        out = awl.format_report("c1", metrics([ev_pledge(60, "cardolan", "free"), ev_pledge(90, "rhudaur", "evil", by="console")]))
        self.assertIn("Realms pledged\n  day 60: cardolan to free (ai)\n  day 90: rhudaur to evil (console)\n", out)

    def test_report_says_none_without_a_pledge(self):
        self.assertIn("Realms pledged\n  none\n", awl.format_report("c1", metrics([kl(1, "a")])))

    # A reload of the pledge day keeps that day's lines from both runs. One realm pledges once, so the
    # last line read for a kingdom is the pledge that stood, as for a chronicle resolution.
    def test_a_realm_pledged_again_after_a_same_day_reload_counts_once_and_the_last_line_stands(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load,
                     ev_pledge(60, "cardolan", "evil", by="console")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "evil", "console")])
        self.assertEqual(dict(awl.flat_metrics(m))["realms_pledged"], 1)

    def test_a_replayed_pledge_line_is_the_one_that_stands(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        free = ev_pledge(60, "cardolan", "free", by="console")
        evil = ev_pledge(60, "cardolan", "evil", by="console")
        m = metrics([free, load, evil, load, free])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "console")])

    # A load of a save made on the pledge day, before the pledge, keeps the pledge line: that day is kept
    # from both runs. A pledge has no end, so a kingdom line read after it that carries another side is
    # from a run in which the load took the pledge back.
    def test_a_pledge_a_same_day_load_took_back_is_dropped_by_a_later_kingdom_line_of_another_side(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load, kl(61, "cardolan", side="neutral")])
        self.assertEqual(m["pledges"], [])
        self.assertEqual(dict(awl.flat_metrics(m))["realms_pledged"], 0)

    def test_a_later_kingdom_line_of_the_pledged_side_keeps_the_pledge(self):
        m = metrics([kl(60, "cardolan", side="neutral"), ev_pledge(60, "cardolan", "free"),
                     kl(61, "cardolan", side="free")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "ai")])

    def test_a_kingdom_line_is_judged_against_the_last_pledge_line_read(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load,
                     ev_pledge(60, "cardolan", "evil", by="console"), kl(61, "cardolan", side="evil")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "evil", "console")])

    def test_another_realms_kingdom_line_does_not_drop_a_pledge(self):
        m = metrics([ev_pledge(60, "cardolan", "free"), kl(61, "rhudaur", side="neutral")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "ai")])

    # A kingdom line of a day before the pledge says nothing about it, whatever order the lines are read in.
    def test_a_kingdom_line_of_an_earlier_day_does_not_drop_a_pledge(self):
        m = metrics([ev_pledge(60, "cardolan", "free"), kl(59, "cardolan", side="neutral")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "ai")])

    # A save made before that day's ledger tick writes the day's kingdom lines again after the load.
    def test_a_kingdom_line_of_the_pledge_day_read_after_a_load_drops_the_pledge(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load, kl(60, "cardolan", side="neutral")])
        self.assertEqual(m["pledges"], [])

    def test_a_pledge_taken_back_brings_no_older_pledge_line_of_its_realm_back_and_leaves_other_realms(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(40, "arthedain", "free"), ev_pledge(60, "cardolan", "free", by="console"), load,
                     ev_pledge(60, "cardolan", "evil", by="console"), load, kl(61, "cardolan", side="neutral")])
        self.assertEqual(m["pledges"], [(40, "arthedain", "free", "ai")])

    # The pledge line stays stored, so a later load of a save that holds the pledge lists it again. The load
    # writes no second pledge line: only the kingdom lines after it say which run the log is in.
    def test_a_later_load_of_a_save_that_holds_the_pledge_lists_it_again(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        lines = [ev_pledge(60, "cardolan", "free", by="console"), load, kl(61, "cardolan", side="neutral")]
        self.assertEqual(metrics(lines)["pledges"], [])
        lines += [load]
        self.assertEqual(metrics(lines)["pledges"], [(60, "cardolan", "free", "console")],
                         "the load dropped the day-61 line, so nothing contradicts the pledge")
        lines += [kl(61, "cardolan", side="free")]
        self.assertEqual(metrics(lines)["pledges"], [(60, "cardolan", "free", "console")])

    def test_a_load_of_a_later_day_from_the_run_that_holds_the_pledge_lists_it_again(self):
        lines = [ev_pledge(60, "cardolan", "free", by="console"), f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60",
                 kl(61, "cardolan", side="neutral"), f"{PFX}[WarLedger] v=1 t=load cid=c1 day=62",
                 kl(63, "cardolan", side="free")]
        self.assertEqual(metrics(lines)["pledges"], [(60, "cardolan", "free", "console")])

    # A load keeps its own day from both runs, for this record as for every other one.
    def test_a_load_keeps_the_side_a_kingdom_line_of_its_own_day_carried(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load, kl(60, "cardolan", side="neutral"), load])
        self.assertEqual(m["pledges"], [])

    def test_a_realm_that_pledges_again_after_a_load_took_its_pledge_back_is_listed(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load, kl(61, "cardolan", side="neutral"),
                     ev_pledge(62, "cardolan", "evil")])
        self.assertEqual(m["pledges"], [(62, "cardolan", "evil", "ai")])

    # Until a kingdom line of that realm is read after the load, the log cannot say that the pledge is gone.
    def test_a_pledge_a_load_took_back_stays_listed_while_no_later_kingdom_line_of_its_realm_is_read(self):
        load = f"{PFX}[WarLedger] v=1 t=load cid=c1 day=60"
        m = metrics([ev_pledge(60, "cardolan", "free", by="console"), load, kl(61, "rhudaur", side="neutral")])
        self.assertEqual(m["pledges"], [(60, "cardolan", "free", "console")])

    # A kingdom's side changes on the day it pledges, so the first capital loss of a side is read on every
    # loss, not only on a kingdom's first one.
    def test_capital_loss_per_side_counts_a_loss_after_the_kingdom_changed_side(self):
        lines = [kl(10, "cardolan", side="neutral", cap="1"), kl(20, "cardolan", side="neutral", cap="0"),
                 kl(30, "cardolan", side="neutral", cap="1"), ev_pledge(60, "cardolan", "free"),
                 kl(61, "cardolan", side="free", cap="1"), kl(80, "cardolan", side="free", cap="0")]
        m = metrics(lines)
        self.assertEqual(m["capital_loss"], {"cardolan": 20})
        self.assertEqual(m["capital_loss_side"], {"neutral": 20, "free": 80})

    # Net change per side is the side's total, so a realm that joins reads as a gain for its new side and a
    # loss for neutral with no fief changing hands. The report and the comparison say so.
    PLEDGE_NOTE = "note: a realm that pledged counts for its new side from the day it joined"

    def test_report_notes_that_net_change_includes_pledged_realms(self):
        lines = [sl(1, "free", 20, base=20), sl(1, "neutral", 10, base=10),
                 ev_pledge(60, "cardolan", "free"), sl(61, "free", 30, base=30), sl(61, "neutral", 0)]
        out = awl.format_report("c1", metrics(lines))
        self.assertIn("  free: +10\n", out)
        self.assertIn(self.PLEDGE_NOTE, out)

    def test_report_has_no_pledge_note_without_a_pledge(self):
        out = awl.format_report("c1", metrics([sl(1, "free", 20, base=20), sl(61, "free", 30, base=30)]))
        self.assertNotIn(self.PLEDGE_NOTE, out)

    def test_compare_notes_pledges_only_when_a_side_has_one(self):
        quiet = metrics([sl(1, "free", 20, base=20)])
        joined = metrics([sl(1, "free", 20, base=20), ev_pledge(60, "cardolan", "free")])
        self.assertIn(self.PLEDGE_NOTE, awl.format_compare("off", "on", quiet, joined))
        self.assertNotIn(self.PLEDGE_NOTE, awl.format_compare("off", "off", quiet, quiet))

    def test_flat_metrics_count_the_pledges(self):
        rows = dict(awl.flat_metrics(metrics([ev_pledge(60, "cardolan", "free"), ev_pledge(90, "rhudaur", "evil")])))
        self.assertEqual(rows["realms_pledged"], 2)

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

    def test_chronicle_resolutions_counted_per_outcome(self):
        m = metrics([ev_chron(20, "fellowship", "Occurred"), ev_chron(49, "hornburg", "Held"),
                     ev_chron(55, "lorien", "Lapsed"), ev_chron(60, "osgiliath", "Fell"),
                     ev_chron(61, "dale", "Lapsed"), ev_chron(70, "ents", "Skipped")])
        self.assertEqual(m["chronicle_counts"],
                         {"Occurred": 1, "Held": 1, "Fell": 1, "Lapsed": 2, "Skipped": 1})

    def test_chronicle_count_keeps_the_last_resolution_per_event(self):
        # A reload from before a resolution replays the event: the later line is the outcome that stood.
        m = metrics([ev_chron(49, "hornburg", "Fell"), ev_chron(69, "hornburg", "Held")])
        self.assertEqual(m["chronicle_counts"], {"Held": 1})

    def test_chronicle_count_keeps_a_repeat_read_after_another_outcome(self):
        # Held, Lapsed and Fell-at-close resolve on the saved close day, so a reload that ends the same way
        # writes a byte-identical line: it must still count as the last one read.
        m = metrics([ev_chron(69, "hornburg", "Held"), ev_chron(60, "hornburg", "Fell"), ev_chron(69, "hornburg", "Held")])
        self.assertEqual(m["chronicle_counts"], {"Held": 1})

    def test_chronicle_count_follows_read_order_not_day(self):
        m = metrics([ev_chron(69, "hornburg", "Held"), ev_chron(60, "hornburg", "Fell")])
        self.assertEqual(m["chronicle_counts"], {"Fell": 1})

    def test_chronicle_table_has_one_row_per_event_the_last_read(self):
        m = metrics([ev_chron(69, "hornburg", "Held"), ev_chron(60, "hornburg", "Fell")])
        self.assertEqual(m["chronicle"], [(60, "hornburg", "Fell")])

    def test_chronicle_counts_in_the_summary(self):
        m = metrics([ev_chron(49, "hornburg", "Held"), ev_chron(55, "lorien", "Lapsed"), ev_chron(56, "dale", "Lapsed")])
        report = awl.format_report("c1", m)
        self.assertIn("resolutions by outcome: Held 1, Lapsed 2", report)

    def test_no_chronicle_lines_counts_nothing(self):
        m = metrics([sh(1, "0.5")])
        self.assertEqual(m["chronicle_counts"], {})
        self.assertNotIn("resolutions by outcome", awl.format_report("c1", m))


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

    def test_a_capital_unheld_in_the_first_observation_fails_as_lost_by_that_day(self):
        m = metrics(kdays([50], k="w", cap="0"))
        self.assertEqual(m["target_capital"], "FAIL")
        self.assertIn("lost by day 50", m["target_capital_reason"])
        m = metrics(kdays(range(1, 120), k="w", cap="0"))
        self.assertEqual(m["target_capital"], "FAIL")
        self.assertIn("lost by day 1", m["target_capital_reason"])

    def test_an_observed_transition_reports_the_exact_day_not_lost_by(self):
        m = metrics([kl(50, "a", cap="1"), kl(51, "a", cap="0")])
        self.assertNotIn("lost by", m["target_capital_reason"])

    def test_an_unheld_capital_on_day_120_or_later_is_not_a_failure_by_itself(self):
        self.assertNotEqual(metrics(kdays([120, 121], cap="0"))["target_capital"], "FAIL")

    def test_one_kingdoms_complete_rows_do_not_hide_anothers_gap(self):
        m = metrics(kdays(range(1, 120), k="empire_s", side="evil")
                    + [kl(1, "empire_w", cap="1"), kl(120, "empire_w", cap="0")])
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("empire_w", m["target_capital_reason"])
        self.assertIn("first missing day 2", m["target_capital_reason"])

    def test_two_kingdoms_with_complete_rows_pass(self):
        m = metrics(kdays(range(1, 120), k="a") + kdays(range(1, 120), k="b", side="evil"))
        self.assertEqual(m["target_capital"], "PASS")

    def test_a_kingdom_with_only_na_capital_rows_blocks_pass(self):
        m = metrics(kdays(range(1, 120), k="a") + kdays(range(1, 120), k="b", cap="na"))
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("b", m["target_capital_reason"])

    def test_na_days_inside_one_kingdoms_series_are_a_hole(self):
        m = metrics(kdays(range(1, 120), k="a") + kdays(range(1, 120), k="b")
                    + [kl(60, "b", cap="na")])
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("first missing day 60", m["target_capital_reason"])

    def test_a_kingdom_first_seen_after_day_1_blocks_pass(self):
        m = metrics(kdays(range(1, 120), k="a") + kdays(range(50, 120), k="b"))
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("first missing day 1", m["target_capital_reason"])

    def test_a_destroyed_kingdom_needs_coverage_only_until_its_destruction(self):
        base = kdays(range(1, 120), k="a")
        gone = kdays(range(1, 60), k="b") + [ev_destroyed(60, "b")]
        self.assertEqual(metrics(base + gone)["target_capital"], "PASS")
        short = kdays(range(1, 40), k="b") + [ev_destroyed(60, "b")]
        m = metrics(base + short)
        self.assertEqual(m["target_capital"], "UNKNOWN")
        self.assertIn("first missing day 40", m["target_capital_reason"])

    def test_a_destroyed_kingdom_with_no_rows_blocks_pass(self):
        m = metrics(kdays(range(1, 120), k="a") + [ev_destroyed(60, "b")])
        self.assertEqual(m["target_capital"], "UNKNOWN")

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
        # Real emission order: a day's side lines, then its share line, which closes the day.
        path = self.write("a.log", [sl(1, "free", 7), sl(1, "evil", 5), sl(1, "neutral", 2),
                                    sh(1, "0.4"), sh(2, "na")])
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


def day_block(day, share, k_pts=10, cid="c1"):
    """One daily tick as WarLedgerService writes it: kingdom lines, the three side lines, then the share."""
    return [kl(day, "gondor", pts=k_pts, cid=cid), sl(day, "free", k_pts, cid=cid),
            sl(day, "evil", 10, cid=cid), sl(day, "neutral", 0, cid=cid), sh(day, share, cid=cid)]


def ev_load(day, cid="c1"):
    return f"{PFX}[WarLedger] v=1 t=load cid={cid} day={day}"


def tick(day, share, k_pts=10, chronicle=(), tiers=(), cid="c1"):
    """One daily tick in the order the game writes it. WarChronicleModule adds WarChronicleBehavior before
    WarChronicleEventsBehavior and MbEvent listeners run newest-first, so the chronicle's resolution lines
    come first, then the rally's tier lines (RunDaily logs them before WriteDaily), then the day's block."""
    lines = [ev_chron(day, eid, outcome, cid=cid) for eid, outcome in chronicle]
    lines += [ev_tier(day, k, frm, to, cid=cid) for k, frm, to in tiers]
    return lines + day_block(day, share, k_pts=k_pts, cid=cid)


class LoadMarkerTests(unittest.TestCase):
    """A load writes `t=load day=S`: every record of that campaign after day S belongs to the abandoned run.

    On a load the engine waits only the elapsed part of the day before the next daily tick
    (Campaign.CreateCampaignEvents, v1.5.5 Campaign.cs:1024-1030), so a save at fraction f of day S next
    ticks at S + 2f: day S again when f < 0.5, day S + 1 otherwise. Day S is kept from both runs.
    """

    def abandoned(self):
        # Days 1 to 4 played. Day 2's tick resolves dale; the game is saved on day 2 after a destruction
        # between ticks, then hornburg falls later the same elapsed day. Day 3 rallies gondor; Rohan dies on day 4.
        lines = tick(1, "0.5") + tick(2, "0.5", chronicle=[("dale", "Occurred")]) + [ev_destroyed(2, "isengard")]
        lines += [ev_chron(2, "hornburg", "Fell")]
        lines += tick(3, "0.9", tiers=[("gondor", 0, 1)]) + tick(4, "0.9", chronicle=[("erebor", "Lapsed")])
        return lines + [ev_destroyed(4, "rohan")]

    def test_a_load_in_the_first_half_of_day_s_rewrites_it_and_loses_none_of_its_events(self):
        # Saved in the first half of elapsed day 2: the reloaded run ticks day 2 again (an exact repeat of dale), then day 3.
        lines = self.abandoned() + [ev_load(2)]
        lines += tick(2, "0.5", chronicle=[("dale", "Occurred")]) + tick(3, "0.4", k_pts=7)
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual(c.shares, {1: 0.5, 2: 0.5, 3: 0.4})
        self.assertEqual(c.kingdoms[(3, "gondor")]["pts"], "7")
        self.assertEqual(m["destroyed"], [(2, "isengard")], "day 2's event stays, day 4's goes")
        self.assertEqual(m["chronicle"], [(2, "dale", "Occurred"), (2, "hornburg", "Fell")])
        self.assertEqual([f["ev"] for d, f in c.events if f["ev"] == "tier"], [], "the abandoned day-3 rally is gone")

    def test_a_load_late_in_the_day_drops_the_abandoned_days_before_the_next_block(self):
        lines = self.abandoned() + [ev_load(2)] + tick(3, "0.4", k_pts=7)
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual(c.days(), [1, 2, 3])
        self.assertNotIn((4, "gondor"), c.kingdoms)
        self.assertEqual(m["destroyed"], [(2, "isengard")])
        self.assertEqual(m["tiers"]["gondor"]["changes"], 0)
        self.assertNotIn("erebor", [eid for _, eid, _ in m["chronicle"]])

    def test_a_load_after_an_abandoned_run_that_closed_exactly_at_s_plus_1_drops_that_day(self):
        # The abandoned run's last lines are day 3's; the reloaded run writes day 3 with no events.
        lines = tick(1, "0.5") + tick(2, "0.5")
        lines += tick(3, "0.9", chronicle=[("dale", "Lapsed")], tiers=[("gondor", 0, 1)])
        lines += [ev_load(2)] + tick(3, "0.4")
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual(c.events, [])
        self.assertEqual((m["chronicle"], m["chronicle_counts"]), ([], {}))
        self.assertEqual(c.shares[3], 0.4)

    def test_the_reloaded_runs_own_next_day_lines_are_kept_in_the_order_the_game_writes_them(self):
        lines = self.abandoned() + [ev_load(2)]
        lines += tick(3, "0.4", chronicle=[("hornburg", "Held")], tiers=[("gondor", 0, 2)]) + [ev_destroyed(3, "dale")]
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual([(d, f["to"]) for d, f in c.events if f["ev"] == "tier"], [(3, "2")])
        self.assertEqual(m["destroyed"], [(2, "isengard"), (3, "dale")])
        self.assertEqual(m["chronicle"], [(2, "dale", "Occurred"), (3, "hornburg", "Held")],
                         "the line read last stands for hornburg")

    def test_without_a_load_marker_nothing_is_rewound(self):
        # Logs from builds before the marker: a reload of an older save keeps the abandoned run's days.
        lines = self.abandoned() + tick(3, "0.4", k_pts=7)
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual(c.shares, {1: 0.5, 2: 0.5, 3: 0.4, 4: 0.9})
        self.assertEqual(c.kingdoms[(3, "gondor")]["pts"], "7", "the last line of a day still wins")
        self.assertEqual(m["destroyed"], [(2, "isengard"), (4, "rohan")])

    def test_a_load_marker_counts_as_a_ledger_line_and_is_not_malformed(self):
        p = parse([ev_load(5)])
        self.assertEqual((p.ledger_lines, p.malformed), (1, 0))

    def test_a_load_marker_without_a_day_is_malformed(self):
        p = parse([f"{PFX}[WarLedger] v=1 t=load cid=c1"])
        self.assertEqual(p.malformed, 1)

    def test_reload_of_the_same_save_double_counts_nothing(self):
        # Days 1 to 3 with their events, then the day-1 save again: days 2 and 3 come back unchanged.
        def from_day_2():
            return (tick(2, "0.6", chronicle=[("dale", "Lapsed")], tiers=[("gondor", 0, 1)])
                    + tick(3, "0.7") + [ev_destroyed(3, "rohan")])
        lines = tick(1, "0.5") + from_day_2() + [ev_load(1)] + from_day_2()
        c = parse(lines).campaigns["c1"]
        m = awl.compute_metrics(c)
        self.assertEqual(len(c.events), 3)
        self.assertEqual(m["destroyed"], [(3, "rohan")])
        self.assertEqual(m["chronicle_counts"], {"Lapsed": 1})
        self.assertEqual((m["day_count"], m["share_days"]), (3, 3))

    def test_a_load_is_per_campaign(self):
        lines = tick(5, "0.5", cid="a") + tick(5, "0.5", cid="b") + [ev_load(2, cid="b")]
        p = parse(lines)
        self.assertEqual(sorted(p.campaigns["a"].shares), [5])
        self.assertEqual(sorted(p.campaigns["b"].shares), [])


if __name__ == "__main__":
    unittest.main()
