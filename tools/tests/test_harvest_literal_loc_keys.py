#!/usr/bin/env python3
"""Unit tests for tools/harvest_literal_loc_keys.py insert_rows (issue #919; same bug as #781, #922).

Run:  python -m unittest discover -s tools/tests -t .

insert_rows must leave exactly one blank line before </strings> however many blank lines the file
had and however many times it runs, in LF and CRLF files, without touching any existing row. The
expected text is compared whole, so a fix that overwrites the row above </strings> also fails.
"""
import os
import sys
import tempfile
import unittest
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
