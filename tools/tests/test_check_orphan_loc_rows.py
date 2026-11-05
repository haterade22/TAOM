#!/usr/bin/env python
"""Unit tests for tools/check_orphan_loc_rows.py (#1018, the unread-localization-row gate).

Every test builds a synthetic repo in a tempdir, so the suite never reads the real strings files.
Each guard runs in both directions (a reader passes, no reader fails). The fixture carries an empty
stub for every file in tools/_loc_sources.py, as the real tree does, so a deleted source shows.

Run:  python -m unittest tools.tests.test_check_orphan_loc_rows
"""
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_orphan_loc_rows as cor  # noqa: E402
from _loc_sources import TAOM_SOURCES  # noqa: E402

SUBMODULE = (
    '<Module><Xmls>'
    '<XmlNode><XmlName id="GameText" path="taom_test_strings"/></XmlNode>'
    '</Xmls></Module>'
)
STUB = '<base><strings/></base>'
N_FILES = len(TAOM_SOURCES) + 1   # the table plus taom_test_strings.xml, which SubModule.xml registers
TOOL = Path(cor.__file__)


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


def _strings(*ids: str) -> str:
    rows = "".join('<string id="%s" text="English %s"/>' % (i, i) for i in ids)
    return '<base><strings>%s</strings></base>' % rows


class GateCase(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.build(Path(self._tmp.name))

    def build(self, root: Path):
        self.root = root
        for src, _ in TAOM_SOURCES:
            _write(root / "Main/_Module/ModuleData" / src, STUB)
        _write(root / "Main/_Module/SubModule.xml", SUBMODULE)
        self.families = root / "tools/loc_composed_families.json"
        self.allow = root / "tools/loc_orphan_allowlist.json"
        _write(self.families, "{}")
        _write(self.allow, "{}")

    def strings(self, *ids):
        _write(self.root / "Main/_Module/ModuleData/taom_test_strings.xml", _strings(*ids))

    def module_rows(self, body):
        """Write taom_module_strings.xml, the one file the override loader reads."""
        _write(self.root / "Main/_Module/ModuleData/taom_module_strings.xml",
               '<base><strings>%s</strings></base>' % body)

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cor.main(["--repo-root", str(self.root)])
        return code, out.getvalue(), err.getvalue()

    def family(self, **entries):
        _write(self.families, json.dumps(entries))


class ReaderTests(GateCase):
    def test_unread_row_fails(self):
        self.strings("taom_dead")
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("taom_dead", out)

    def test_csharp_literal_reader_passes(self):
        self.strings("taom_a", "taom_b")
        _write(self.root / "Main/Feature.cs",
               'class F { void M() { var t = new TextObject("{=taom_a}Hi"); Use("taom_b"); } }')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("checked 2 rows in %d files" % N_FILES, out)

    def test_client_csharp_literal_reader_passes(self):
        self.strings("taom_a")
        _write(self.root / "Client/Feature.cs", 'class F { string s = "{=taom_a}Hi"; }')
        self.assertEqual(self.run_gate()[0], 0)

    def test_csharp_comment_is_not_a_reader(self):
        self.strings("taom_a")
        _write(self.root / "Main/Feature.cs", "class F { } // was taom_a\n/* taom_a */")
        self.assertEqual(self.run_gate()[0], 1)

    def test_verbatim_string_is_a_reader(self):
        self.strings("taom_a")
        _write(self.root / "Main/F.cs", 'class F { string s = @"say ""hi"" {=taom_a}Hi"; }')
        self.assertEqual(self.run_gate()[0], 0)

    def test_quote_char_literal_does_not_open_a_string(self):
        self.strings("taom_a")
        _write(self.root / "Main/F.cs", "class F { char c = '\"'; string s = \"taom_a\"; }")
        self.assertEqual(self.run_gate()[0], 0)

    def test_literal_nested_in_an_interpolation_hole_is_not_seen(self):
        # Known limit, documented in the tool: the scanner ends the outer string at the inner quote.
        self.strings("taom_a")
        _write(self.root / "Main/F.cs", 'class F { string s = $"a {Key("taom_a")} b"; }')
        self.assertEqual(self.run_gate()[0], 1)

    def test_bin_and_obj_are_skipped(self):
        self.strings("taom_a")
        _write(self.root / "Main/bin/Gen.cs", 'class G { string s = "taom_a"; }')
        _write(self.root / "Main/obj/Gen.cs", 'class G { string s = "taom_a"; }')
        self.assertEqual(self.run_gate()[0], 1)

    def test_data_file_reader_passes(self):
        self.strings("taom_a", "taom_b")
        _write(self.root / "Main/_Module/ModuleData/other.xml", '<x name="{=taom_a}Hi" id="taom_b"/>')
        self.assertEqual(self.run_gate()[0], 0)

    def test_xml_comment_is_not_a_reader(self):
        self.strings("taom_a", "taom_b")
        _write(self.root / "Main/_Module/ModuleData/other.xml",
               '<x id="taom_b"><!-- <y name="{=taom_a}Hi"/>\n --></x>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("UNREAD taom_a", out)
        self.assertNotIn("UNREAD taom_b", out)

    def test_languages_copies_and_vanilla_keys_are_ignored(self):
        self.strings("vanilla_key", "taom_a")
        _write(self.root / "Main/F.cs", 'class F { string s = "taom_a"; }')
        _write(self.root / "Main/_Module/ModuleData/Languages/DE/std_x.xml", _strings("taom_a", "taom_ghost"))
        self.assertEqual(self.run_gate()[0], 0)

    def test_a_strings_file_naming_another_rows_id_in_its_text_is_not_a_reader(self):
        self.strings("taom_a")
        _write(self.root / "Main/_Module/ModuleData/taom_wotr_strings.xml",
               '<base><strings><string id="taom_b" text="see taom_a"/></strings></base>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("UNREAD taom_a", out)
        self.assertIn("UNREAD taom_b", out)

    def test_languages_copy_is_not_a_reader(self):
        self.strings("taom_a")
        _write(self.root / "Main/_Module/ModuleData/Languages/DE/std_x.xml", _strings("taom_a"))
        self.assertEqual(self.run_gate()[0], 1)

    def test_xslt_comment_is_not_a_reader(self):
        self.strings("taom_a")
        _write(self.root / "Main/_Module/ModuleData/x.xslt", '<s><!-- {=taom_a}Hi --></s>')
        self.assertEqual(self.run_gate()[0], 1)

    def test_a_root_under_a_folder_named_languages_still_scans_data(self):
        self.build(Path(self._tmp.name) / "Languages" / "repo")
        self.strings("taom_a")
        _write(self.root / "Main/_Module/ModuleData/other.xml", '<x id="taom_a"/>')
        self.assertEqual(self.run_gate()[0], 0)

    def test_rows_are_found_from_the_loc_sources_table_alone(self):
        (self.root / "Main/_Module/SubModule.xml").unlink()
        self.module_rows('<string id="taom_dead" text="English"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("UNREAD taom_dead", out)


class VanillaRewordTests(GateCase):
    def test_vanilla_reword_row_in_the_override_file_is_read_by_the_engine(self):
        self.module_rows('<string id="taom_wrap" text="{=0cdXddA9}Alliance"/>'
                         '<string id="taom_own" text="{=taom_own}Own key"/>'
                         '<string id="taom_other" text="{=taom_elsewhere}Other taom key"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertNotIn("taom_wrap", out)
        self.assertIn("taom_own", out)
        self.assertIn("taom_other", out)

    def test_a_vanilla_key_outside_the_override_file_is_not_exempt(self):
        self.strings("taom_wrap")
        _write(self.root / "Main/_Module/ModuleData/taom_test_strings.xml",
               '<base><strings><string id="taom_wrap" text="{=0cdXddA9}Alliance"/></strings></base>')
        self.assertEqual(self.run_gate()[0], 1)

    def test_a_non_vanilla_shaped_key_is_not_exempt(self):
        self.module_rows('<string id="taom_bang" text="{=!}Alliance"/>'
                         '<string id="taom_long" text="{=0cdXddA9x}Alliance"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("taom_bang", out)
        self.assertIn("taom_long", out)

    def test_a_shadowed_row_fails_and_names_the_winner(self):
        self.module_rows('<string id="taom_old" text="{=0cdXddA9}Old"/>'
                         '<string id="taom_new" text="{=0cdXddA9}New"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("SHADOWED taom_old", out)
        self.assertIn("taom_new", out)
        self.assertNotIn("SHADOWED taom_new", out)


    def test_the_same_id_repeated_shadows_the_earlier_row(self):
        self.module_rows('<string id="taom_x" text="{=0cdXddA9}Old"/>'
                         '<string id="taom_x" text="{=0cdXddA9}New"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("SHADOWED taom_x", out)

    def test_a_later_non_taom_row_with_the_same_key_shadows_a_taom_row(self):
        self.module_rows('<string id="taom_old" text="{=0cdXddA9}Old"/>'
                         '<string id="0cdXddA9" text="{=0cdXddA9}New"/>')
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("SHADOWED taom_old", out)

    def test_the_allowlist_suppresses_a_shadowed_row(self):
        self.module_rows('<string id="taom_old" text="{=0cdXddA9}Old"/>'
                         '<string id="taom_new" text="{=0cdXddA9}New"/>')
        _write(self.allow, json.dumps({"taom_old": "kept on purpose"}))
        self.assertEqual(self.run_gate()[0], 0)


class DeclarationTests(GateCase):
    def test_composed_family_passes_when_its_stem_is_in_the_data(self):
        self.strings("taom_duty_x_title", "taom_duty_x_body")
        _write(self.root / "Main/Duty.cs", "class D { }")
        _write(self.root / "Main/_Module/ModuleData/duties.json", '{"id": "x"}')
        self.family(duty={"regex": "taom_duty_(?P<stem>[a-z]+)_(title|body)",
                          "reason": "built by D.KeyFor", "site": "Main/Duty.cs"})
        self.assertEqual(self.run_gate()[0], 0)

    def test_composed_family_row_of_a_removed_stem_fails(self):
        self.strings("taom_duty_x_title", "taom_duty_gone_title")
        _write(self.root / "Main/Duty.cs", "class D { }")
        _write(self.root / "Main/_Module/ModuleData/duties.json", '{"id": "x"}')
        self.family(duty={"regex": "taom_duty_(?P<stem>[a-z]+)_(title|body)",
                          "reason": "built by D.KeyFor", "site": "Main/Duty.cs"})
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("UNREAD taom_duty_gone_title", out)
        self.assertNotIn("UNREAD taom_duty_x_title", out)

    def test_composed_family_stem_named_only_in_csharp_fails(self):
        self.strings("taom_duty_x_title")
        _write(self.root / "Main/Duty.cs", 'class D { string s = "x"; }')
        self.family(duty={"regex": "taom_duty_(?P<stem>[a-z]+)_(title|body)",
                          "reason": "r", "site": "Main/Duty.cs"})
        self.assertEqual(self.run_gate()[0], 1)

    def test_family_without_a_stem_group_matches_on_the_regex_alone(self):
        self.strings("taom_duty_x_title")
        _write(self.root / "Main/Duty.cs", "class D { }")
        self.family(duty={"regex": "taom_duty_.*", "reason": "r", "site": "Main/Duty.cs"})
        self.assertEqual(self.run_gate()[0], 0)

    def test_stale_family_fails(self):
        self.strings("taom_a")
        _write(self.root / "Main/Duty.cs", 'class D { string s = "taom_a"; }')
        self.family(gone={"regex": "taom_zzz_.*", "reason": "r", "site": "Main/Duty.cs"})
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("STALE FAMILY gone", out)

    def test_family_with_a_missing_site_fails(self):
        self.strings("taom_a")
        self.family(fam={"regex": "taom_a", "reason": "r", "site": "Main/Nowhere.cs"})
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("Main/Nowhere.cs", out)

    def test_allowlisted_row_passes(self):
        self.strings("taom_kept")
        _write(self.allow, json.dumps({"taom_kept": "kept for old saves"}))
        self.assertEqual(self.run_gate()[0], 0)

    def test_stale_allowlist_entry_fails(self):
        self.strings("taom_a")
        _write(self.root / "Main/F.cs", 'class F { string s = "taom_a"; }')
        _write(self.allow, json.dumps({"taom_missing": "reason"}))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("STALE ALLOWLIST taom_missing", out)

    def test_allowlist_entry_for_a_winning_reword_row_fails(self):
        self.module_rows('<string id="taom_wrap" text="{=0cdXddA9}Alliance"/>')
        _write(self.allow, json.dumps({"taom_wrap": "unneeded"}))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("STALE ALLOWLIST taom_wrap", out)

    def test_allowlist_entry_for_a_family_covered_row_fails(self):
        self.strings("taom_duty_x_title")
        _write(self.root / "Main/Duty.cs", "class D { }")
        _write(self.root / "Main/_Module/ModuleData/duties.json", '{"id": "x"}')
        self.family(duty={"regex": "taom_duty_(?P<stem>[a-z]+)_(title|body)",
                          "reason": "r", "site": "Main/Duty.cs"})
        _write(self.allow, json.dumps({"taom_duty_x_title": "unneeded"}))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("STALE ALLOWLIST taom_duty_x_title", out)

    def test_allowlist_entry_that_now_has_a_reader_fails(self):
        self.strings("taom_a")
        _write(self.root / "Main/F.cs", 'class F { string s = "taom_a"; }')
        _write(self.allow, json.dumps({"taom_a": "was parked"}))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("read another way", out)


class BadInputTests(GateCase):
    def test_allowlist_entry_without_reason_exits_2(self):
        self.strings("taom_a")
        _write(self.allow, json.dumps({"taom_a": ""}))
        self.assertEqual(self.run_gate()[0], 2)

    def test_family_missing_a_key_exits_2(self):
        self.strings("taom_a")
        self.family(fam={"regex": "taom_a", "reason": "r"})
        self.assertEqual(self.run_gate()[0], 2)

    def test_family_with_a_bad_regex_exits_2(self):
        self.strings("taom_a")
        self.family(fam={"regex": "(", "reason": "r", "site": "Main/F.cs"})
        self.assertEqual(self.run_gate()[0], 2)

    def test_malformed_json_exits_2(self):
        self.strings("taom_a")
        _write(self.families, "{")
        self.assertEqual(self.run_gate()[0], 2)

    def test_non_object_json_exits_2(self):
        self.strings("taom_a")
        _write(self.allow, "[]")
        self.assertEqual(self.run_gate()[0], 2)

    def test_malformed_xml_exits_2_without_a_traceback(self):
        _write(self.root / "Main/_Module/ModuleData/taom_test_strings.xml", "<base><strings>")
        done = subprocess.run([sys.executable, str(TOOL), "--repo-root", str(self.root)],
                              capture_output=True, text=True)
        self.assertEqual(done.returncode, 2)
        self.assertIn("malformed XML", done.stderr)
        self.assertNotIn("Traceback", done.stderr)

    def test_missing_root_exits_2(self):
        empty = Path(self._tmp.name) / "nowhere"
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cor.main(["--repo-root", str(empty)])
        self.assertEqual(code, 2)
        self.assertIn("no English strings files", err.getvalue())

    def test_files_with_no_rows_exit_2(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cor.main(["--repo-root", str(self.root)])
        self.assertEqual(code, 2)
        self.assertIn("no taom_ rows", err.getvalue())

    def test_a_deleted_listed_source_is_a_finding(self):
        self.strings("taom_a")
        (self.root / "Main/_Module/ModuleData/taom_wotr_strings.xml").unlink()
        code, out, _ = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("MISSING SOURCE taom_wotr_strings.xml", out)


if __name__ == "__main__":
    unittest.main()
