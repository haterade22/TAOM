"""check_generator_item_refs.py: every item id a generator would write must exist.

The pure parts (XML ref extraction, the table walk, the check) run everywhere.
The last class runs the real generators against the real install and is the
gate; it skips, never fakes, without the install.
"""
import os
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))

import check_generator_item_refs as cg  # noqa: E402
import validate_moduledata as vm  # noqa: E402
import taom_schema as ts  # noqa: E402


class RefsFromXmlTests(unittest.TestCase):
    def test_takes_item_refs_only(self):
        text = ('<equipment slot="Item0" id="Item.wm_gondor_sword_a01" />'
                '<equipment slot="Horse" id="Item.saddle_horse" />'
                '<NPCCharacter id="gondor_militia" culture="Culture.gondor" />')
        self.assertEqual(cg.refs_from_xml(text), {"wm_gondor_sword_a01", "saddle_horse"})

    def test_empty_text(self):
        self.assertEqual(cg.refs_from_xml(""), set())


class RefsFromTablesTests(unittest.TestCase):
    def test_walks_nested_dicts_and_lists(self):
        table = {"gondor": {"weapons": ["wm_gondor_sword_a01", "sm_gd_shield_a1"],
                            "head": "sk_gd_ano_inf_helmet_med_a", "axe": None}}
        self.assertEqual(cg.refs_from_tables(table),
                         {"wm_gondor_sword_a01", "sm_gd_shield_a1", "sk_gd_ano_inf_helmet_med_a"})

    def test_strips_item_prefix(self):
        self.assertEqual(cg.refs_from_tables(["Item.hunting_bow"]), {"hunting_bow"})

    def test_reads_embedded_xml_template(self):
        block = '<Equipment slot="Item1" id="Item.sm_gd_shield_a1" />\n<Equipment slot="Body" id="Item.starter_cavalry_gondor_body_a" />'
        self.assertEqual(cg.refs_from_tables(block), {"sm_gd_shield_a1", "starter_cavalry_gondor_body_a"})

    def test_skips_label_keys_and_labels_without_underscore(self):
        table = {"khuzait": {"folder": "dol_guldur", "extra_folders": ["iron_hills"],
                             "culture_id": "khuzait", "inf": ["empire_sword_1_t2"],
                             "group": "Infantry", "region": "rhun"}}
        self.assertEqual(cg.refs_from_tables(table), {"empire_sword_1_t2"})

    def test_tuple_from_skips_leading_label_columns(self):
        rows = [("khuzait", "rhun", "sk_rh_loke_tunic_a", "sk_rh_loke_boots_a")]
        self.assertEqual(cg.refs_from_tables(rows, tuple_from=2),
                         {"sk_rh_loke_tunic_a", "sk_rh_loke_boots_a"})
        # Without the offset the folder label "rhun" is dropped by shape and the
        # culture id has no underscore either, so the result is the same here,
        # but a folder like "dol_guldur" would leak in; the offset is the contract.
        self.assertIn("dol_guldur", cg.refs_from_tables([("x", "dol_guldur", "a_b")]))
        self.assertNotIn("dol_guldur", cg.refs_from_tables([("x", "dol_guldur", "a_b")], tuple_from=2))


class CollectAndCheckTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "tools").mkdir()

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, rel, body):
        p = self.root / rel
        p.write_text(textwrap.dedent(body), encoding="utf-8")
        return p

    def test_run_mode_reads_printed_xml(self):
        self._write("tools/gen.py", '''
            print('<equipment slot="Item0" id="Item.wm_gondor_sword_a01" />')
            print('<equipment slot="Item1" id="Item.retired_thing" />')
        ''')
        spec = cg.GeneratorSpec("tools/gen.py", "run")
        self.assertEqual(cg.collect(spec, self.root), {"wm_gondor_sword_a01", "retired_thing"})

    def test_run_mode_failure_is_an_error_not_a_pass(self):
        self._write("tools/broken.py", "import sys\nsys.exit(3)\n")
        with self.assertRaises(RuntimeError):
            cg.collect(cg.GeneratorSpec("tools/broken.py", "run"), self.root)

    def test_tables_mode_imports_without_running_main(self):
        self._write("tools/tab.py", '''
            RAN = []
            PICKS = {"gondor": {"sword": "wm_gondor_sword_a01", "folder": "gondor"}}
            def main():
                RAN.append(1)
                raise SystemExit("must not run on import")
            if __name__ == "__main__":
                main()
        ''')
        spec = cg.GeneratorSpec("tools/tab.py", "tables", ("PICKS",))
        self.assertEqual(cg.collect(spec, self.root), {"wm_gondor_sword_a01"})

    def test_tables_mode_stale_table_name_raises(self):
        self._write("tools/tab2.py", "PICKS = {}\n")
        with self.assertRaises(AttributeError):
            cg.collect(cg.GeneratorSpec("tools/tab2.py", "tables", ("GONE",)), self.root)

    def test_check_reports_only_missing_ids(self):
        self._write("tools/gen.py", '''
            print('<equipment slot="Item0" id="Item.present_a" />')
            print('<equipment slot="Item1" id="Item.retired_b" />')
        ''')
        specs = (cg.GeneratorSpec("tools/gen.py", "run"),)
        findings = cg.check({"present_a"}, specs, self.root)
        self.assertEqual([(f.generator, f.unresolved, f.total) for f in findings],
                         [("tools/gen.py", ["retired_b"], 2)])
        self.assertEqual(cg.check({"present_a", "retired_b"}, specs, self.root), [])

    def test_report_names_the_generator_and_the_ids(self):
        text = cg.format_report([cg.Finding("tools/gen.py", ["retired_b"], 2)], 1, 10)
        self.assertIn("tools/gen.py", text)
        self.assertIn("retired_b", text)
        self.assertIn("FAIL", text)
        self.assertIn("PASS", cg.format_report([], 1, 10))


class RegistryContractTests(unittest.TestCase):
    """The GENERATORS table must describe scripts that exist and expose what it names."""

    def test_every_registered_generator_exists(self):
        for spec in cg.GENERATORS:
            with self.subTest(spec.path):
                self.assertTrue((cg.REPO_ROOT / spec.path).is_file(), spec.path)
                self.assertIn(spec.mode, ("run", "tables"))

    def test_tables_mode_generators_expose_their_tables(self):
        for spec in cg.GENERATORS:
            if spec.mode != "tables":
                continue
            with self.subTest(spec.path):
                module = cg.load_module(cg.REPO_ROOT / spec.path)
                for table in spec.tables:
                    self.assertTrue(hasattr(module, table), f"{spec.path} lost {table}")


class ValidatorWiringTests(unittest.TestCase):
    def test_missing_id_becomes_a_warning_under_the_code(self):
        # A registry holding every id but one that a real generator writes.
        game_modules = Path(os.environ.get("BANNERLORD_GAME_MODULES") or
                            Path(os.environ.get("BANNERLORD_GAME_DIR") or
                                 r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord") / "Modules")
        if not game_modules.exists():
            self.skipTest(f"no game install at {game_modules}")
        items = set(ts.build_registries(vm.MODULEDATA, game_modules).items)
        self.assertEqual(vm.generator_item_ref_issues(items), [], "the gate itself is red")
        items.discard("wm_gondor_sword_a01")
        issues = vm.generator_item_ref_issues(items)
        self.assertTrue(issues)
        for issue in issues:
            self.assertEqual(issue.code, vm.GENERATOR_CODE)
            self.assertIs(issue.severity, ts.Severity.WARNING)
            self.assertIn("wm_gondor_sword_a01", issue.message)


class LiveInstallGateTests(unittest.TestCase):
    """THE gate: every registered generator resolves against the live install."""

    def test_no_generator_writes_a_retired_id(self):
        game_modules = Path(os.environ.get("BANNERLORD_GAME_MODULES") or
                            Path(os.environ.get("BANNERLORD_GAME_DIR") or
                                 r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord") / "Modules")
        if not game_modules.exists():
            self.skipTest(f"no game install at {game_modules}")
        registry = cg.build_registry(game_modules)
        findings = cg.check(registry)
        self.assertEqual(
            findings, [],
            "\n" + cg.format_report(findings, len(cg.GENERATORS), len(registry)) +
            "\nThe Armory retired an id a generator still writes. Replace it with one "
            "the live LOTRLOME_Armory defines (or a vanilla id); never leave it.")


if __name__ == "__main__":
    unittest.main()
