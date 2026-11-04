#!/usr/bin/env python3
"""Tests for tools/check_stale_loc_rows.py: the staged-edit report for English text that changed
while the language rows and the cache kept the old translation."""
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_stale_loc_rows as cs  # noqa: E402

EN_DIR = "Main/_Module/ModuleData"
SOURCE = "taom_messenger_strings.xml"
LANG_FILE = "std_taom_messenger_strings_{locale}.xml"


def english(a="Send Messenger", b="Messenger not available."):
    return ('<?xml version="1.0" encoding="utf-8"?>\n<strings>\n'
            f'<string id="k_send" text="{{=k_send}}{a}" />\n'
            f'<string id="k_none" text="{{=k_none}}{b}" />\n</strings>\n')


def lang(a, b):
    return ('<?xml version="1.0" encoding="utf-8"?>\n<base><strings>\n'
            f'<string id="k_send" text="{a}" />\n<string id="k_none" text="{b}" />\n</strings></base>\n')


class ParseTests(unittest.TestCase):
    def test_english_rows_lose_their_key_prefix(self):
        self.assertEqual(cs.english_rows(english()),
                         {"k_send": "Send Messenger", "k_none": "Messenger not available."})

    def test_language_rows_are_read_verbatim(self):
        self.assertEqual(cs.language_rows(lang("Enviar", "Nada")), {"k_send": "Enviar", "k_none": "Nada"})

    def test_english_rows_are_keyed_by_the_prefix_when_the_id_differs(self):
        xml = '<strings><string id="pre.spc_0" text="{=aom_pre.spc_0_text}Hello" /></strings>'
        self.assertEqual(cs.english_rows(xml), {"aom_pre.spc_0_text": "Hello"})

    def test_english_rows_without_a_prefix_are_keyed_by_the_id(self):
        xml = '<strings><string id="wotr_1" text="Hello" /></strings>'
        self.assertEqual(cs.english_rows(xml), {"wotr_1": "Hello"})

    def test_a_greater_than_inside_the_text_does_not_end_the_row_early(self):
        xml = ('<strings><string id="taom_fat_envoy_delay_hint" '
               'text="{=taom_fat_envoy_delay_hint}In {DAYS} {?DAYS>1}days{?}day{\\?}." /></strings>')
        self.assertEqual(cs.english_rows(xml), {"taom_fat_envoy_delay_hint": "In {DAYS} {?DAYS>1}days{?}day{\\?}."})
        self.assertEqual(cs.language_rows(xml)["taom_fat_envoy_delay_hint"],
                         "{=taom_fat_envoy_delay_hint}In {DAYS} {?DAYS>1}days{?}day{\\?}.")


class FindStaleTests(unittest.TestCase):
    def test_changed_english_with_an_untouched_row_and_cache_is_stale(self):
        stale = cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "Velho"}, {"k": "Velho"}, {"k": "Velho"}, {"k": "Velho"})
        self.assertEqual(stale, ["k"])

    def test_unchanged_english_is_not_reported(self):
        self.assertEqual(cs.find_stale({"k": "Same"}, {"k": "Same"}, {"k": "V"}, {"k": "V"}, {}, {}), [])

    def test_a_row_changed_in_the_same_commit_passes(self):
        self.assertEqual(cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "Velho"}, {"k": "Novo"}, {}, {}), [])

    def test_a_row_equal_to_the_new_english_passes(self):
        self.assertEqual(cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "New"}, {"k": "New"}, {}, {}), [])

    def test_a_row_still_holding_the_old_english_is_untranslated_not_stale(self):
        self.assertEqual(cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "Old"}, {"k": "Old"}, {}, {}), [])

    def test_a_changed_cache_entry_passes(self):
        self.assertEqual(cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "Velho"}, {"k": "Velho"},
                                       {"k": "Velho"}, {"k": "Novo"}), [])

    def test_a_deleted_cache_entry_passes(self):
        self.assertEqual(cs.find_stale({"k": "Old"}, {"k": "New"}, {"k": "Velho"}, {"k": "Velho"},
                                       {"k": "Velho"}, {}), [])

    def test_a_new_key_is_not_a_change(self):
        self.assertEqual(cs.find_stale({}, {"k": "New"}, {}, {"k": "x"}, {}, {}), [])


def git(root, *args):
    subprocess.run(["git", "-C", str(root), *args], check=True, capture_output=True, text=True)


class StagedEditTests(unittest.TestCase):
    """One synthetic edit in a throwaway repo: the English of one key changes and only DE is redone."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        git(self.root, "init", "-q")
        git(self.root, "config", "user.email", "t@example.com")
        git(self.root, "config", "user.name", "t")
        self.write(f"{EN_DIR}/{SOURCE}", english())
        for code, locale in (("BR", "por-BR"), ("DE", "deu-DE")):
            self.write(f"{EN_DIR}/Languages/{code}/{LANG_FILE.format(locale=locale)}", lang("Old " + code, "Nada " + code))
            self.write(f"tools/translation_cache/{code.lower()}.json", json.dumps({"k_send": "Old " + code}))
        git(self.root, "add", "-A")
        git(self.root, "commit", "-q", "-m", "base")
        patches = [mock.patch.object(cs, "REPO_ROOT", self.root),
                   mock.patch.object(cs, "LANGUAGES", {"BR": "por-BR", "DE": "deu-DE"}),
                   mock.patch.object(cs, "SOURCES", [(SOURCE, LANG_FILE)])]
        for p in patches:
            p.start()
            self.addCleanup(p.stop)

    def tearDown(self):
        self._tmp.cleanup()

    def write(self, rel, text):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8", newline="\n")

    def run_staged(self):
        out = io.StringIO()
        with mock.patch("sys.stdout", out):
            rc = cs.main(["--staged"])
        return rc, out.getvalue()

    def test_nothing_staged_reports_nothing(self):
        rc, out = self.run_staged()
        self.assertEqual(rc, 0)
        self.assertIn("0 stale", out)

    def test_an_english_edit_with_one_language_redone_reports_the_other(self):
        self.write(f"{EN_DIR}/{SOURCE}", english(a="Send a Messenger"))
        self.write(f"{EN_DIR}/Languages/DE/{LANG_FILE.format(locale='deu-DE')}", lang("Neu DE", "Nada DE"))
        git(self.root, "add", "-A")
        rc, out = self.run_staged()
        self.assertEqual(rc, 0, "report only: never fails")
        self.assertIn("k_send", out)
        self.assertIn("BR", out)
        self.assertNotIn("DE:", out)
        self.assertIn("1 stale", out)

    def test_an_english_row_whose_id_differs_from_its_key_is_reported(self):
        diff = lambda a: ('<strings>\n<string id="pre.k_send" text="{=aom_k_send_text}' + a + '" />\n</strings>\n')
        self.write(f"{EN_DIR}/{SOURCE}", diff("Send Messenger"))
        self.write(f"{EN_DIR}/Languages/BR/{LANG_FILE.format(locale='por-BR')}",
                   '<base><strings><string id="aom_k_send_text" text="Velho" /></strings></base>\n')
        self.write("tools/translation_cache/br.json", json.dumps({"aom_k_send_text": "Velho"}))
        git(self.root, "add", "-A")
        git(self.root, "commit", "-q", "-m", "ids differ")
        self.write(f"{EN_DIR}/{SOURCE}", diff("Send a Messenger"))
        git(self.root, "add", "-A")
        _, out = self.run_staged()
        self.assertIn("aom_k_send_text  BR", out)

    def test_outside_a_git_tree_it_says_no_source_was_read(self):
        with tempfile.TemporaryDirectory() as bare, mock.patch.object(cs, "REPO_ROOT", Path(bare)):
            _, out = self.run_staged()
        self.assertIn("no source was read", out)

    def test_the_other_key_is_not_reported(self):
        self.write(f"{EN_DIR}/{SOURCE}", english(a="Send a Messenger"))
        git(self.root, "add", "-A")
        _, out = self.run_staged()
        self.assertNotIn("k_none", out)


if __name__ == "__main__":
    unittest.main()
