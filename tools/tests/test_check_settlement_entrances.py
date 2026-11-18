"""Tests for tools/check_settlement_entrances.py (#1434)."""
import io
import os
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from unittest import mock

from tools import check_settlement_entrances as cse
from tools import data_gates as dg


def row(sid, pos, gate=None):
    g = f' gate_posX="{gate[0]}" gate_posY="{gate[1]}"' if gate else ""
    return f'  <Settlement id="{sid}" name="x" posX="{pos[0]}" posY="{pos[1]}" culture="Culture.empire"{g} />\n'


def doc(*rows):
    return "<Settlements>\n" + "".join(rows) + "</Settlements>\n"


def moved(skip=()):
    return [row(s, (b[0] + 1.0, b[1])) for s, b in cse.ISLAND_ENTRANCES.items() if s not in skip]


class EntranceTests(unittest.TestCase):
    def test_the_entrance_is_the_gate_when_there_is_one(self):
        self.assertEqual((1.5, 2.5), cse.entrances(doc(row("castle_X", (9, 9), (1.5, 2.5))))["castle_X"])

    def test_the_entrance_falls_back_to_the_position_without_a_gate(self):
        self.assertEqual((7.0, 8.0), cse.entrances(doc(row("hideout_X", (7, 8))))["hideout_X"])

    def test_a_coordinate_that_is_not_a_number_reads_as_none(self):
        text = doc('<Settlement id="a" posX="1,5" posY="2" />', '<Settlement id="b" posX="nan" posY="2" />')
        self.assertEqual({"a": (None, 2.0), "b": (None, 2.0)}, cse.entrances(text))

    def test_a_commented_out_row_is_not_read(self):
        bad = cse.ISLAND_ENTRANCES["castle_EN6"]
        text = doc(f"<!--{row('castle_EN6', (0, 0), bad)}-->", *moved())
        self.assertEqual([], cse.findings(text))


class FindingTests(unittest.TestCase):
    def test_every_known_entrance_is_reported(self):
        found = cse.findings(doc(*[row(sid, bad) for sid, bad in cse.ISLAND_ENTRANCES.items()]))
        self.assertEqual(len(cse.ISLAND_ENTRANCES), len(found))
        self.assertTrue(all("unreachable value" in f for f in found))

    def test_a_moved_entrance_passes(self):
        self.assertEqual([], cse.findings(doc(*moved())))

    def test_a_gate_added_to_a_settlement_without_one_passes(self):
        sid, bad = "hideout_forest_9", cse.ISLAND_ENTRANCES["hideout_forest_9"]
        self.assertEqual([], cse.findings(doc(*moved({sid}), row(sid, bad, (bad[0] - 0.4, bad[1] + 0.3)))))

    def test_float_noise_still_counts_as_the_old_value(self):
        sid, bad = "castle_EN6", cse.ISLAND_ENTRANCES["castle_EN6"]
        found = cse.findings(doc(*moved({sid}), row(sid, (593.654, 863.584), (bad[0] + 0.0001, bad[1]))))
        self.assertEqual(1, len(found))
        self.assertTrue(found[0].startswith("castle_EN6:") and "unreachable value" in found[0])

    def test_a_missing_settlement_is_a_finding(self):
        found = cse.findings(doc(*moved({"village_LN1_3"})))
        self.assertEqual(1, len(found))
        self.assertIn("village_LN1_3", found[0])

    def test_the_table_holds_the_fourteen_the_audit_found(self):
        # The 2026-11-17 audit run: five of Silas He's seven, plus nine more. Orthanc and Isengard passed the path check.
        self.assertEqual({"castle_EN6", "castle_A19", "castle_DO3", "hideout_forest_9", "hideout_desert_7", "village_LN1_3",
                          "village_CA2_1", "village_CA3_1", "castle_village_LN1_2", "castle_village_LN2_1",
                          "castle_village_EL1_2", "castle_village_EL4_2", "castle_village_AND1_1", "village_SRH1_1"},
                         set(cse.ISLAND_ENTRANCES))


class MainTests(unittest.TestCase):
    def run_main(self, path):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(cse, "LIVE", path), redirect_stdout(out), redirect_stderr(err):
            code = cse.main(["--check"])
        return code, out.getvalue() + err.getvalue()

    def write(self, text):
        handle = tempfile.NamedTemporaryFile("wb", suffix=".xml", delete=False)
        handle.write(b"\xef\xbb\xbf" + text.replace("\n", "\r\n").encode("utf-8"))
        handle.close()
        self.addCleanup(os.unlink, handle.name)
        return handle.name

    def test_a_missing_file_exits_two_and_data_gates_reads_not_run(self):
        code, output = self.run_main(os.path.join(tempfile.gettempdir(), "no-such-dir-1434", "settlements.xml"))
        self.assertEqual(2, code)
        self.assertEqual("NOT RUN", dg.verdict(code, output)[0])

    def test_the_old_values_in_a_bom_crlf_file_exit_one(self):
        code, output = self.run_main(self.write(doc(*[row(s, b) for s, b in cse.ISLAND_ENTRANCES.items()])))
        self.assertEqual(1, code)
        self.assertIn(f"{len(cse.ISLAND_ENTRANCES)} of {len(cse.ISLAND_ENTRANCES)}", output)

    def test_moved_entrances_exit_zero(self):
        code, output = self.run_main(self.write(doc(*moved())))
        self.assertEqual(0, code)
        self.assertIn("OK", output)

    def test_a_file_that_does_not_parse_exits_two(self):
        code, _ = self.run_main(self.write("<Settlements><Settlement"))
        self.assertEqual(2, code)


class GateSelectionTests(unittest.TestCase):
    def test_a_change_to_the_tool_selects_its_gate(self):
        names = [g.id for g in dg.select(dg.GATES, ["tools/check_settlement_entrances.py"])]
        self.assertIn("map-entrances", names)


if __name__ == "__main__":
    unittest.main()
