#!/usr/bin/env python3
"""Tests for tools/check_provenance.py: the register vocabulary, the shipped binaries and the adopt records."""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_provenance as cp  # noqa: E402

REGISTER = """# Provenance Register

<!-- provenance-register-start -->

| Source | Tokens | License | Derivation | Covers | Status |
|---|---|---|---|---|---|
| Lib.Harmony | `0Harmony.dll` | MIT | redistributed | build-acquired | cleared |
| Serilog | `Serilog` | Apache-2.0 | redistributed | `Serilog*.dll` | cleared |
| Old thing | `Old` | MIT | redistributed | gone | removed |
| Some mod | `Some` | MIT | behavioural-port | `tools/x.py` ([audit](../reviews/adopt-some-2026-01-01.md)) | cleared |

<!-- provenance-register-end -->
"""


class RegisterTests(unittest.TestCase):
    def test_rows_are_parsed_between_the_markers(self):
        rows = cp.parse_register(REGISTER)
        self.assertEqual([r["source"] for r in rows], ["Lib.Harmony", "Serilog", "Old thing", "Some mod"])
        self.assertEqual(rows[0]["derivation"], "redistributed")
        self.assertEqual(rows[2]["status"], "removed")

    def test_vocabulary_accepts_the_closed_sets(self):
        self.assertEqual(cp.check_vocabulary(cp.parse_register(REGISTER)), [])

    def test_an_unknown_derivation_or_status_is_a_miss(self):
        bad = REGISTER.replace("behavioural-port", "inspired-by").replace("| cleared |\n\n", "| mostly-cleared |\n\n")
        misses = cp.check_vocabulary(cp.parse_register(bad))
        self.assertEqual(len(misses), 2)
        self.assertTrue(any("inspired-by" in m for m in misses))
        self.assertTrue(any("mostly-cleared" in m for m in misses))


    def test_a_row_with_too_few_cells_is_a_miss(self):
        short = REGISTER.replace("| Old thing |", "| Lonely | `x` | MIT |\n| Old thing |")
        misses = cp.check_vocabulary(cp.parse_register(short))
        self.assertEqual(len(misses), 1)
        self.assertIn("Lonely", misses[0])
        self.assertIn("3 cells", misses[0])


class BinaryTests(unittest.TestCase):
    def rows(self):
        return cp.parse_register(REGISTER)

    def test_a_mapped_binary_with_a_live_redistributed_row_passes(self):
        files = ["Dependencies/_Module/bin/Win64_Shipping_Client/0Harmony.dll",
                 "Dependencies/_Module/bin/Win64_Shipping_Client/Serilog.Sinks.File.dll"]
        self.assertEqual(cp.check_binaries(self.rows(), files), [])

    def test_a_taom_built_binary_and_a_non_binary_are_skipped(self):
        files = ["Main/_Module/bin/Win64_Shipping_Client/TAOM.dll",
                 "Main/_Module/bin/Win64_Shipping_Client/readme.txt"]
        self.assertEqual(cp.check_binaries(self.rows(), files), [])

    def test_an_unmapped_binary_is_a_miss(self):
        misses = cp.check_binaries(self.rows(), ["Main/_Module/bin/Win64_Shipping_Client/Mystery.dll"])
        self.assertEqual(len(misses), 1)
        self.assertIn("Mystery.dll", misses[0])

    def test_a_mapped_binary_whose_row_is_missing_removed_or_not_redistributed_is_a_miss(self):
        rows = [r for r in self.rows() if r["source"] != "Serilog"]
        self.assertEqual(len(cp.check_binaries(rows, ["a/_Module/bin/Serilog.dll"])), 1)
        removed = cp.parse_register(REGISTER.replace("| Serilog | `Serilog` | Apache-2.0 | redistributed | `Serilog*.dll` | cleared |",
                                                     "| Serilog | `Serilog` | Apache-2.0 | redistributed | x | removed |"))
        self.assertEqual(len(cp.check_binaries(removed, ["a/_Module/bin/Serilog.dll"])), 1)
        other = cp.parse_register(REGISTER.replace("| Serilog | `Serilog` | Apache-2.0 | redistributed",
                                                   "| Serilog | `Serilog` | Apache-2.0 | interop-only"))
        self.assertEqual(len(cp.check_binaries(other, ["a/_Module/bin/Serilog.dll"])), 1)


class AdoptRecordTests(unittest.TestCase):
    def test_a_record_named_by_the_register_passes(self):
        records = {"adopt-some-2026-01-01.md": "# Adopt Some\nWe ported a tool."}
        self.assertEqual(cp.check_adopt_records(REGISTER, records), [])

    def test_a_record_stating_nothing_taken_passes(self):
        records = {"adopt-other-2026-01-02.md": "Verdict: nothing taken. The idea was already present."}
        self.assertEqual(cp.check_adopt_records(REGISTER, records), [])

    def test_a_record_neither_named_nor_nothing_taken_is_a_miss(self):
        records = {"adopt-other-2026-01-02.md": "We took the retry loop."}
        misses = cp.check_adopt_records(REGISTER, records)
        self.assertEqual(len(misses), 1)
        self.assertIn("adopt-other-2026-01-02.md", misses[0])


class RepoTests(unittest.TestCase):
    def test_main_reports_misses_with_exit_1_and_clean_with_0(self):
        import io
        from unittest import mock
        with mock.patch.object(cp, "run_checks", return_value=["a miss"]), \
                mock.patch("sys.stdout", new_callable=io.StringIO) as out:
            self.assertEqual(cp.main([]), 1)
        self.assertIn("a miss", out.getvalue())
        with mock.patch.object(cp, "run_checks", return_value=[]), \
                mock.patch("sys.stdout", new_callable=io.StringIO):
            self.assertEqual(cp.main([]), 0)

    def run_main_in(self, root, register_text=REGISTER):
        import io
        import pathlib
        from unittest import mock
        register = pathlib.Path(root) / "register.md"
        if register_text is not None:
            register.write_text(register_text, encoding="utf-8")
        ceiling = {"GIT_CEILING_DIRECTORIES": str(pathlib.Path(root).parent)}
        with mock.patch.object(cp, "REPO_ROOT", pathlib.Path(root)), mock.patch.object(cp, "REGISTER", register), \
                mock.patch.dict(os.environ, ceiling), \
                mock.patch("sys.stdout", new_callable=io.StringIO), mock.patch("sys.stderr", new_callable=io.StringIO) as err:
            return cp.main([]), err.getvalue()

    def test_a_root_outside_a_git_tree_exits_2_not_0(self):
        import tempfile
        with tempfile.TemporaryDirectory() as root:
            rc, err = self.run_main_in(root)
        self.assertEqual(rc, 2)
        self.assertIn("cannot judge", err)

    def test_a_missing_register_exits_2_not_a_traceback(self):
        import tempfile
        with tempfile.TemporaryDirectory() as root:
            rc, err = self.run_main_in(root, register_text=None)
        self.assertEqual(rc, 2)
        self.assertIn("cannot judge", err)


if __name__ == "__main__":
    unittest.main()
