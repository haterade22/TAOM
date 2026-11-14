#!/usr/bin/env python3
"""Unit tests for tools/harvest_literal_loc_keys.py insert_rows (issue #919; same bug as #781, #922).

Run:  python -m unittest discover -s tools/tests -t .

insert_rows must leave exactly one blank line before </strings> however many blank lines the file
had and however many times it runs, in LF and CRLF files, without touching any existing row. The
expected text is compared whole, so a fix that overwrites the row above </strings> also fails.
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import harvest_literal_loc_keys as h  # noqa: E402

LF = "\n"
CRLF = "\r\n"
OLD_ROW = '  <string id="taom_old" text="{=taom_old}Old" />'

# The three real targets have <strings> as the root; the language files wrap it in <base>.
SHAPES = {
    "strings_root": (['<?xml version="1.0" encoding="utf-8"?>', "<strings>", OLD_ROW],
                     ["</strings>", ""]),
    "base_wrapper": (['<?xml version="1.0" encoding="utf-8"?>',
                      '<base xmlns:xsi="x" type="string">', "<strings>", OLD_ROW],
                     ["</strings>", "</base>", ""]),
}


def _rows(prefix):
    return [(f"taom_{prefix}_a", "Alpha", "Main/Features/Foo/Foo.cs", 1),
            (f"taom_{prefix}_b", "Beta", "Main/Features/Foo/Foo.cs", 2)]


def _block(prefix):
    return [f"  <!-- {h.MARKER} -->", "  <!-- Foo -->",
            f'  <string id="taom_{prefix}_a" text="{{=taom_{prefix}_a}}Alpha" />',
            f'  <string id="taom_{prefix}_b" text="{{=taom_{prefix}_b}}Beta" />']


class ControlCharacterRefusalTests(unittest.TestCase):
    """#702: a C# default with \\n, \\r or \\t would land raw in the XML attribute, which XML reads back
    as a space, so the registered English would differ from the inline default."""

    def test_unescape_CSharpNewlineEscape_BecomesARealNewline(self):
        # The refusal exists because of this: the escape reaches the row as a control character.
        self.assertEqual("a\n\nb", h.unescape("a\\n\\nb", interpolated=False))

    def test_unescape_EscapedBackslashBeforeN_StaysABackslashAndN(self):
        # C# "a\\nb" is a backslash and an n, not a newline, so the refusal must not fire on it.
        self.assertEqual("a\\nb", h.unescape("a\\\\nb", interpolated=False))
        self.assertEqual([], h.control_character_rows([("taom_x", h.unescape("a\\\\nb", False), "A.cs", 1)]))

    def test_control_character_rows_NewlineTabOrCarriageReturn_AreListed(self):
        rows = [("taom_n", "a\nb", "Main/A.cs", 3), ("taom_t", "a\tb", "Main/B.cs", 4),
                ("taom_r", "a\rb", "Main/C.cs", 5), ("taom_ok", "a{newline}b", "Main/D.cs", 6)]
        self.assertEqual(["taom_n", "taom_t", "taom_r"], [r[0] for r in h.control_character_rows(rows)])

    def test_control_character_rows_PlainDefaults_ListNothing(self):
        self.assertEqual([], h.control_character_rows(_rows("x")))

    def _main(self, mode, declared):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(sys, "argv", ["harvest_literal_loc_keys.py", mode]), \
             mock.patch.object(h, "declared_keys", return_value=declared), \
             mock.patch.object(h, "registered_keys", return_value=set()), \
             mock.patch.object(h, "insert_rows") as insert, \
             contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = h.main()
        return code, insert, err.getvalue()

    def test_main_ApplyWithANewlineDefault_RefusesAndWritesNothing(self):
        declared = {"taom_ok": ("Fine", "Main/Features/Foo/Foo.cs", 1),
                    "taom_nl": ("a\n\nb", "Main/Features/Foo/Foo.cs", 9)}
        code, insert, err = self._main("--apply", declared)
        self.assertEqual(1, code)
        insert.assert_not_called()
        self.assertIn("taom_nl", err)
        self.assertIn("Main/Features/Foo/Foo.cs:9", err)
        self.assertIn("{newline}", err)
        self.assertNotIn("taom_ok", err)

    def test_main_DryRunWithATabDefault_Refuses(self):
        code, _insert, err = self._main("--dry-run", {"taom_tab": ("a\tb", "Main/A.cs", 2)})
        self.assertEqual(1, code)
        self.assertIn("taom_tab", err)

    def test_main_ApplyWithPlainDefaults_Writes(self):
        code, insert, _err = self._main("--apply", {"taom_ok": ("Fine", "Main/Features/Foo/Foo.cs", 1)})
        self.assertEqual(0, code)
        insert.assert_called_once()


class InsertRowsBlankLineTests(unittest.TestCase):
    def test_insert_rows_AnyTrailingBlankRun_LeavesExactlyOneAcrossRuns(self):
        for shape, (head, tail) in SHAPES.items():
            for nl in (LF, CRLF):
                for blanks in (0, 1, 2):
                    with self.subTest(shape=shape, crlf=nl == CRLF, blanks=blanks), \
                            tempfile.TemporaryDirectory() as d:
                        path = Path(d) / "strings.xml"
                        h.write_text(path, nl.join(head + [""] * blanks + tail))

                        h.insert_rows(path, _rows("one"))
                        self.assertEqual(nl.join(head + _block("one") + [""] + tail),
                                         h.read_text(path))

                        h.insert_rows(path, _rows("two"))
                        text = h.read_text(path)
                        self.assertEqual(nl.join(head + _block("one") + _block("two") + [""] + tail),
                                         text)

                        root = ET.fromstring(text.encode("utf-8"))
                        strings = root if root.tag == "strings" else root.find("strings")
                        self.assertEqual(
                            ["taom_old", "taom_one_a", "taom_one_b", "taom_two_a", "taom_two_b"],
                            [s.get("id") for s in strings.findall("string")])

    def test_insert_rows_WhitespaceOnlyLines_CollapseToOneBlank(self):
        head, tail = SHAPES["strings_root"]
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "strings.xml"
            h.write_text(path, CRLF.join(head + ["\t", "   ", ""] + tail))
            h.insert_rows(path, _rows("one"))
            self.assertEqual(CRLF.join(head + _block("one") + [""] + tail), h.read_text(path))


if __name__ == "__main__":
    unittest.main()
