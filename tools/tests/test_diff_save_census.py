#!/usr/bin/env python3
"""Unit tests for tools/diff_save_census.py.

Run:  python -m unittest tools.tests.test_diff_save_census

Pure stdlib over synthetic log text; no game install needed. Each test pins one part of the contract:
  - a summary line opens a block, and only phase=SaveCensus lines are read
  - rows come back largest absolute change first
  - a type absent from one block counts as zero
  - a log with fewer than two blocks exits 2 with a message
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import diff_save_census  # noqa: E402


def block(reason, objects, containers, *rows):
    prefix = "2026-11-04 10:00:00 [INFO] [SaveLoad] seq=4 t=+9ms phase=SaveCensus "
    lines = [
        f"{prefix}reason={reason} loadsThisProcess=2 objects={objects} containers={containers} "
        "objectTypes=1 containerTypes=1 unregistered=0 duplicateIds=0 passMs=3"
    ]
    lines.extend(prefix + row for row in rows)
    return lines


FIRST = block(
    "afterReload", 1000, 500,
    "obj n=100 type='TaleWorlds.Hero'",
    "obj n=10 unregistered=0 dupIds=0 type='TaleWorlds.Item'",
    "ctr n=50 elements=200 type='System.Collections.Generic.List<System.Int32>'",
)
SECOND = block(
    "afterReload", 1400, 900,
    "obj n=110 type='TaleWorlds.Hero'",
    "obj n=500 unregistered=4 dupIds=490 type='TaleWorlds.Item'",
    "ctr n=30 elements=90 type='System.Collections.Generic.List<System.Int32>'",
    "ctr n=7 elements=7 type='Taom.New'",
)


class ParseTests(unittest.TestCase):
    def test_parses_blocks_by_summary_line(self):
        noise = ["2026-11-04 10:00:00 [INFO] [SaveLoad] seq=3 t=+1ms phase=SaveCollected objects=1 containers=1"]
        blocks = diff_save_census.parse_blocks(noise + FIRST + noise + SECOND)

        self.assertEqual(len(blocks), 2)
        self.assertEqual(blocks[0]["objects"], 1000)
        self.assertEqual(blocks[1]["containers"], 900)
        self.assertEqual(blocks[1]["rows"][("obj", "TaleWorlds.Item")], 500)
        self.assertEqual(blocks[1]["rows"][("ctr", "Taom.New")], 7)


class DiffTests(unittest.TestCase):
    def setUp(self):
        self.first, self.second = diff_save_census.parse_blocks(FIRST + SECOND)

    def test_diff_reports_largest_deltas_first(self):
        result = diff_save_census.diff_blocks(self.first, self.second, top=2)

        self.assertEqual(result["objects"], 400)
        self.assertEqual(result["containers"], 400)
        self.assertEqual(
            [(row["kind"], row["type"], row["delta"]) for row in result["rows"]],
            [
                ("obj", "TaleWorlds.Item", 490),
                ("ctr", "System.Collections.Generic.List<System.Int32>", -20),
            ],
        )

    def test_type_missing_from_one_block_counts_as_zero(self):
        result = diff_save_census.diff_blocks(self.first, self.second, top=40)

        new = next(row for row in result["rows"] if row["type"] == "Taom.New")
        self.assertEqual((new["from"], new["to"], new["delta"]), (0, 7, 7))


class CliTests(unittest.TestCase):
    def test_cli_with_fewer_than_two_blocks_exits_2(self):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "taom_debug.log")
            with open(path, "w", encoding="utf-8") as log:
                log.write("\n".join(FIRST) + "\n")
            stderr = io.StringIO()
            with contextlib.redirect_stderr(stderr):
                code = diff_save_census.main([path])

        self.assertEqual(code, 2)
        self.assertIn("fewer than two", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
