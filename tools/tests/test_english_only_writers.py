#!/usr/bin/env python3
"""The writers of TAOM language rows leave out English-only overrides of vanilla keys.

A vanilla key TAOM rewords is an English-only override (LocalizationOverrideLoader): its row in
taom_module_strings.xml stays, and no language file carries one, because a language row replaces
vanilla's curated translation (VanillaKeyOverrideTests). translate_with_claude.py honours that
(skip_ids_for). rebuild_translation_files.py and generate_translation_template.py write a row for
every source key, so each was the way to put the deleted rows back.

Run:  python -m unittest tools.tests.test_english_only_writers
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_translation_template as gen  # noqa: E402
import rebuild_translation_files as rebuild  # noqa: E402

SOURCE = ('<strings>'
          '<string id="a" text="{=0CsbbwtN}Call {KINGDOM} to War" />'
          '<string id="b" text="{=taom_own}Own" />'
          '<string id="c" text="{=LjUhEJxz}Caravan of {OWNER.NAME}" />'
          '</strings>')
TARGET = ('<base><strings>\n'
          '<string id="LjUhEJxz" text="Karawane von {OWNER.NAME}" />\n'
          '</strings></base>')
TARGET_NAME = "std_taom_module_strings_deu-DE.xml"


def build_tree(root: Path) -> tuple[Path, Path]:
    module_data = root / "Main" / "_Module" / "ModuleData"
    lang_dir = module_data / "Languages" / "DE"
    lang_dir.mkdir(parents=True)
    source = module_data / "taom_module_strings.xml"
    source.write_text(SOURCE, encoding="utf-8")
    target = lang_dir / TARGET_NAME
    target.write_text(TARGET, encoding="utf-8")
    return source, target


def row_ids(path: Path) -> set:
    import xml.etree.ElementTree as ET
    return {el.get("id") for el in ET.parse(path).iter("string")}


class WriterTests(unittest.TestCase):

    def assert_override_rules(self, target: Path):
        ids = row_ids(target)
        self.assertNotIn("0CsbbwtN", ids, "an English-only override got a language row")
        self.assertIn("taom_own", ids, "a TAOM key lost its language row")
        self.assertIn("LjUhEJxz", ids, "a row the language file already held was dropped")

    def test_rebuild_translation_files_skips_the_override_and_keeps_a_held_row(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            _, target = build_tree(root)
            (root / "game" / "LOTRLOME_Armory" / "ModuleData" / "Languages").mkdir(parents=True)
            saved = rebuild.REPO_ROOT, rebuild.GAME_ROOT
            rebuild.REPO_ROOT, rebuild.GAME_ROOT = root, root / "game"
            try:
                with contextlib.redirect_stdout(io.StringIO()):
                    rebuild.rebuild_language("DE")
            finally:
                rebuild.REPO_ROOT, rebuild.GAME_ROOT = saved
            self.assert_override_rules(target)

    def test_generate_translation_template_skips_the_override_and_keeps_a_held_row(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            source, target = build_tree(root)
            saved = gen.SOURCES, gen.LANG_DIR
            gen.SOURCES = [(source, "std_taom_module_strings_{locale}.xml", "taom_module_strings")]
            gen.LANG_DIR = root / "Main" / "_Module" / "ModuleData" / "Languages"
            try:
                with contextlib.redirect_stdout(io.StringIO()):
                    gen.process_language("DE", dry_run=False)
            finally:
                gen.SOURCES, gen.LANG_DIR = saved
            self.assert_override_rules(target)


class ShippedModuleStringsPinTests(unittest.TestCase):
    """Every key of the shipped taom_module_strings.xml is a TAOM key or an 8-character vanilla-shaped id.

    english_only_override_ids treats the second kind as an English-only override, so a key that is neither
    would be skipped by --sync-ids without being one."""

    def test_every_key_is_a_taom_key_or_has_the_vanilla_shape(self):
        import translate_with_claude as t
        source = t.REPO_ROOT / "Main" / "_Module" / "ModuleData" / t.OVERRIDE_SOURCE
        keys = t._parse_string_xml(source, strip_keys=True)
        self.assertGreater(len(keys), 1000, "the shipped file was not found")
        odd = sorted(k for k in keys
                     if not k.startswith(("taom_", "TAOM_", "aom_")) and not t.VANILLA_ID_SHAPE.fullmatch(k))
        self.assertEqual(odd, [], "neither a TAOM key nor an 8-character vanilla id")


if __name__ == "__main__":
    unittest.main(verbosity=2)
