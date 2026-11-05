#!/usr/bin/env python3
"""Tests for the live language_data.xml processor check in tools/check_external_loc_coverage.py (#1026).

Each test builds a SYNTHETIC Modules tree, so the suite needs no game install.
"""
import contextlib
import io
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_external_loc_coverage as cx  # noqa: E402

NS = "TaleWorlds.Localization.TextProcessor.LanguageProcessors."
ITALIAN = NS + "ItalianTextProcessor"
ENGLISH = NS + "EnglishTextProcessor"
TURKISH = NS + "TurkishTextProcessor"
TR_ID = "Türkçe"
JP_ID = "日本語"


def _write(path: Path, text: str, encoding="utf-8") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode(encoding))


def _lang(attrs: str, encoding="utf-8") -> str:
    return f'<?xml version="1.0" encoding="{encoding}"?>\n<LanguageData {attrs}>\n</LanguageData>\n'


class ProcessorGateTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.mods = Path(self._tmp.name)
        _write(self.mods / "Native/ModuleData/Languages/IT/language_data.xml",
               _lang(f'id="Italiano" name="Italiano" text_processor="{ITALIAN}"'), "utf-8-sig")
        # Native's CNs file is UTF-16 and declares no processor.
        _write(self.mods / "Native/ModuleData/Languages/CNs/language_data.xml",
               _lang('id="简体中文" name="简体中文"', "utf-16"), "utf-16")
        # Native's JP file: the language exists and declares no processor.
        _write(self.mods / "Native/ModuleData/Languages/JP/language_data.xml",
               _lang(f'id="{JP_ID}" name="{JP_ID}"'), "utf-8-sig")

    def tearDown(self):
        self._tmp.cleanup()

    def _live(self, module, lang, attrs):
        _write(self.mods / module / "ModuleData/Languages" / lang / "language_data.xml", _lang(attrs))

    def _native_tr(self, raw: bytes):
        p = self.mods / "Native/ModuleData/Languages/TR/language_data.xml"
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(raw)

    def test_native_processors_reads_only_declared_ones(self):
        self.assertEqual({"Italiano": ITALIAN}, cx.native_processors(self.mods))

    def test_name_without_processor_is_reported_for_both_modules(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        self._live("LOTRLOME_Armory", "IT", 'id="Italiano" name="Italiano"')
        bad = cx.processor_mismatches(self.mods)
        self.assertEqual(2, len(bad))
        self.assertTrue(any("TAOM_Map/IT" in b for b in bad))
        self.assertTrue(any("LOTRLOME_Armory/IT" in b for b in bad))

    def test_name_with_native_processor_passes(self):
        self._live("TAOM_Map", "IT", f'id="Italiano" name="Italiano" text_processor="{ITALIAN}"')
        self.assertEqual([], cx.processor_mismatches(self.mods))

    def test_wrong_processor_is_reported(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano" text_processor="Other.Processor"')
        self.assertEqual(1, len(cx.processor_mismatches(self.mods)))

    def test_file_without_name_is_inert(self):
        self._live("TAOM_Map", "IT", 'id="Italiano"')
        self.assertEqual([], cx.processor_mismatches(self.mods))

    def test_language_native_declares_no_processor_for_passes_with_a_name_only(self):
        self._live("TAOM_Map", "JP", f'id="{JP_ID}" name="{JP_ID}"')
        self.assertEqual([], cx.processor_mismatches(self.mods))

    def test_processor_where_native_declares_none_is_reported(self):
        self._live("TAOM_Map", "JP", f'id="{JP_ID}" name="{JP_ID}" text_processor="{ITALIAN}"')
        bad = cx.processor_mismatches(self.mods)
        self.assertEqual(1, len(bad))
        self.assertIn("Native declares none", bad[0])

    def test_language_id_native_does_not_know_is_reported_when_it_has_a_processor(self):
        self._live("TAOM_Map", "XX", f'id="Elvish" name="Elvish" text_processor="{ITALIAN}"')
        self.assertIn("Native declares none", cx.processor_mismatches(self.mods)[0])

    def test_empty_name_is_not_reported(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name=""')
        self.assertEqual([], cx.processor_mismatches(self.mods))

    def test_no_live_language_data_file_exits_2(self):
        with contextlib.redirect_stderr(io.StringIO()) as err, self.assertRaises(SystemExit) as cm:
            cx.processor_mismatches(self.mods)
        self.assertEqual(2, cm.exception.code)
        self.assertIn("no language_data.xml", err.getvalue())

    def test_unreadable_file_by_os_or_value_error_exits_2(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        for exc in (OSError("denied"), ValueError("bad")):
            with mock.patch.object(cx, "_parse_like_engine", side_effect=exc), \
                 contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as cm:
                cx.processor_mismatches(self.mods)
            self.assertEqual(2, cm.exception.code)

    def test_root_english_file_without_processor_is_reported(self):
        _write(self.mods / "Native/ModuleData/Languages/language_data.xml",
               _lang(f'id="English" name="English" text_processor="{ENGLISH}"'))
        _write(self.mods / "LOTRLOME_Armory/ModuleData/Languages/language_data.xml",
               _lang('id="English" name="English"'))
        bad = cx.processor_mismatches(self.mods)
        self.assertEqual(1, len(bad))
        self.assertIn("LOTRLOME_Armory/(root)", bad[0])

    def test_native_utf16_turkish_file_is_read_and_a_bare_taom_map_file_is_reported(self):
        text = _lang(f'id="{TR_ID}" name="{TR_ID}" text_processor="{TURKISH}"', "utf-16")
        self._native_tr(text.encode("utf-16"))
        self.assertEqual(TURKISH, cx.native_processors(self.mods).get(TR_ID))
        self._live("TAOM_Map", "TR", f'id="{TR_ID}" name="{TR_ID}"')
        self.assertEqual(1, len(cx.processor_mismatches(self.mods)))

    def test_native_utf8_file_declaring_utf16_is_read_like_the_engine(self):
        # UTF-8 with a BOM that declares utf-16: the engine ignores the declaration.
        text = _lang(f'id="{TR_ID}" name="{TR_ID}" text_processor="{TURKISH}"', "utf-16")
        self._native_tr(bytes([0xEF, 0xBB, 0xBF]) + text.encode("utf-8"))
        self.assertEqual(TURKISH, cx.native_processors(self.mods).get(TR_ID))
        self._live("TAOM_Map", "TR", f'id="{TR_ID}" name="{TR_ID}"')
        self.assertEqual(1, len(cx.processor_mismatches(self.mods)))

    def test_unreadable_live_file_exits_2(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        p = self.mods / "TAOM_Map/ModuleData/Languages/IT/language_data.xml"
        p.write_bytes(p.read_bytes()[:-20])
        with contextlib.redirect_stderr(io.StringIO()) as err, self.assertRaises(SystemExit) as cm:
            cx.processor_mismatches(self.mods)
        self.assertEqual(2, cm.exception.code)
        self.assertIn("IT", err.getvalue())

    def test_no_native_processor_exits_2(self):
        shutil.rmtree(self.mods / "Native")
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as cm:
            cx.processor_mismatches(self.mods)
        self.assertEqual(2, cm.exception.code)

    def test_channel_shaped_tree_is_checked_against_the_real_native(self):
        channel = self.mods / "channel"
        _write(channel / "TAOM_Map/ModuleData/Languages/IT/language_data.xml",
               _lang('id="Italiano" name="Italiano"'))
        self.assertEqual(1, len(cx.processor_mismatches(channel, self.mods)))

    def _run_main(self):
        baseline = self.mods / "baseline.json"
        baseline.write_text("{}", encoding="utf-8")
        with mock.patch.dict(os.environ, {"BANNERLORD_GAME_MODULES": str(self.mods)}), \
             mock.patch.object(sys, "argv", ["check_external_loc_coverage.py"]), \
             mock.patch.object(cx, "BASELINE", baseline), \
             mock.patch.object(cx, "native_modules_root", return_value=self.mods), \
             contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return cx.main()

    def test_main_exits_0_on_a_clean_tree(self):
        self._live("TAOM_Map", "IT", f'id="Italiano" name="Italiano" text_processor="{ITALIAN}"')
        self.assertEqual(0, self._run_main())

    def test_main_exits_1_on_a_mismatch(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        self.assertEqual(1, self._run_main())

    def _main(self, argv, env, baseline=None, **patches):
        """Run cx.main() with argv, env and module attributes patched; return (code, stdout, stderr)."""
        if baseline is None:
            baseline = self.mods / "baseline.json"
            baseline.write_text("{}", encoding="utf-8")
        out, err = io.StringIO(), io.StringIO()
        with contextlib.ExitStack() as stack:
            stack.enter_context(mock.patch.dict(os.environ, env))
            stack.enter_context(mock.patch.object(sys, "argv", ["check_external_loc_coverage.py"] + argv))
            stack.enter_context(mock.patch.object(cx, "BASELINE", baseline))
            for name, value in patches.items():
                stack.enter_context(mock.patch.object(cx, name, value))
            stack.enter_context(contextlib.redirect_stdout(out))
            stack.enter_context(contextlib.redirect_stderr(err))
            try:
                code = cx.main()
            except SystemExit as exit_:
                code = exit_.code
        return code, out.getvalue(), err.getvalue()

    def test_main_reads_native_from_the_game_root_when_the_modules_folder_is_overridden(self):
        game = self.mods / "game"
        shutil.copytree(self.mods / "Native", game / "Modules" / "Native")
        channel = self.mods / "channel"
        _write(channel / "TAOM_Map/ModuleData/Languages/IT/language_data.xml",
               _lang('id="Italiano" name="Italiano"'))
        code, _, err = self._main(
            [], {"BANNERLORD_GAME_DIR": str(game), "BANNERLORD_GAME_MODULES": str(channel)})
        self.assertEqual(1, code)
        self.assertIn("TAOM_Map/IT", err)

    def test_main_prints_both_fail_headers_when_the_ratchet_regresses_and_a_file_mismatches(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        baseline = self.mods / "baseline.json"
        baseline.write_text('{"BR": {"taom_map": 0, "armory": 0}}', encoding="utf-8")
        current = {lang: {"taom_map": 0, "armory": 0} for lang in cx.LANGUAGES}
        current["BR"]["taom_map"] = 5
        code, _, err = self._main(
            [], {"BANNERLORD_GAME_MODULES": str(self.mods)}, baseline=baseline,
            native_modules_root=lambda: self.mods, measure=lambda: (current, self.mods))
        self.assertEqual(1, code)
        self.assertIn("reset the language's text processor", err)
        self.assertIn("external-module translations went backwards", err)

    def _processors_only(self, modules, baseline=None):
        return self._main(["--processors-only"], {"BANNERLORD_GAME_MODULES": str(modules)},
                          baseline=baseline or self.mods / "no-such-baseline.json",
                          native_modules_root=lambda: self.mods)

    def test_processors_only_exits_2_when_the_modules_folder_is_missing(self):
        code, _, err = self._processors_only(self.mods / "nowhere")
        self.assertEqual(2, code)
        self.assertIn("not found", err)

    def test_processors_only_reports_a_mismatch_without_the_baseline(self):
        self._live("TAOM_Map", "IT", 'id="Italiano" name="Italiano"')
        code, out, err = self._processors_only(self.mods)
        self.assertEqual(1, code)
        self.assertNotIn("Coverage", out)
        self.assertIn("TAOM_Map/IT", err)

    def test_processors_only_exits_0_on_a_clean_tree_and_counts_the_files(self):
        self._live("TAOM_Map", "IT", f'id="Italiano" name="Italiano" text_processor="{ITALIAN}"')
        code, out, _ = self._processors_only(self.mods)
        self.assertEqual(0, code)
        self.assertIn("PASS", out)
        self.assertIn("1 language_data.xml", out)

    def test_processors_only_exits_2_on_a_tree_with_no_language_data_files(self):
        code, _, _ = self._processors_only(self.mods)
        self.assertEqual(2, code)


if __name__ == "__main__":
    unittest.main()
