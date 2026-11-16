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


class KeyPrefixTests(unittest.TestCase):
    """#1130: the Realms features write realms_ keys and the crash notifier TAOM_ keys; the
    harvest skipped both."""

    def test_literal_RealmsKey_MatchesWithItsDefault(self):
        m = h.LITERAL.search('new TextObject("{=realms_war_title}War of the Realms")')
        self.assertIsNotNone(m)
        self.assertEqual(("realms_war_title", "War of the Realms"), (m.group(2), m.group(3)))

    def test_literal_InterpolatedRealmsKey_Matches(self):
        m = h.LITERAL.search('$"{{=realms_gold}}{{GOLD}} gold"')
        self.assertIsNotNone(m)
        self.assertEqual("realms_gold", m.group(2))

    def test_literal_UpperCaseTaomKey_Matches(self):
        m = h.LITERAL.search('new TextObject("{=TAOM_CrashReport_Title}TAOM caught a crash")')
        self.assertIsNotNone(m)
        self.assertEqual("TAOM_CrashReport_Title", m.group(2))

    def test_literal_OtherPrefix_DoesNotMatch(self):
        self.assertIsNone(h.LITERAL.search('"{=str_vanilla_id}Text"'))


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

    def _main(self, mode, declared, split=None, registered=None):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(sys, "argv", ["harvest_literal_loc_keys.py", mode]), \
             mock.patch.object(h, "declared_keys", return_value=declared), \
             mock.patch.object(h, "registered_keys", return_value=registered or set()), \
             mock.patch.object(h, "split_keys", return_value=split or {}), \
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


class SplitLiteralRefusalTests(unittest.TestCase):
    """#1385: LITERAL reads one C# literal, so a default split as "{=key}Half " + "rest" registered only
    its first half (three rows in Phase 3, caught by reading the rows)."""

    _main = ControlCharacterRefusalTests._main

    def test_continued_literals_PlusAndAnotherLiteral_IsListedWithItsLine(self):
        text = 'x();\nnew TextObject("{=taom_a}Half " +\n    "rest");'
        self.assertEqual([("taom_a", 2)], h.continued_literals(text))

    def test_continued_literals_InterpolatedContinuation_IsListed(self):
        self.assertEqual([("taom_b", 1)], h.continued_literals('"{=taom_b}One " + $"two {n}"'))

    def test_continued_literals_WholeLiteralOrPlusAVariable_ListsNothing(self):
        self.assertEqual([], h.continued_literals('"{=taom_c}Whole" + name; "{=taom_d}Also whole";'))

    def test_continued_literals_NewlineBeforePlus_IsListed(self):
        # The shape of all three Phase 3 rows: the + starts the next line.
        text = 'var s = new TextObject("{=taom_e}Half "\r\n        + "rest");'
        self.assertEqual([("taom_e", 1)], h.continued_literals(text))

    def test_continued_literals_VerbatimContinuations_AreListed(self):
        for tail in ('@"rest"', '$@"rest {n}"', '@$"rest {n}"'):
            with self.subTest(tail=tail):
                self.assertEqual([("taom_f", 1)], h.continued_literals('"{=taom_f}Half " + ' + tail))

    def test_continued_literals_ConcatenationLaterInTheFile_ListsNothing(self):
        self.assertEqual([], h.continued_literals('Use("{=taom_g}Whole");\nvar s = "a" + "b";'))

    def test_split_keys_SyntheticTree_FindsTheSplitSite(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            cs = root / "Main" / "Features" / "Foo" / "A.cs"
            cs.parent.mkdir(parents=True)
            h.write_text(cs, 'class A {\r\n  T t = new T("{=taom_s}Half "\r\n    + "rest");\r\n'
                             '  T u = new T("{=taom_w}Whole");\r\n}\r\n')
            with mock.patch.object(h, "REPO_ROOT", root):
                self.assertEqual({"taom_s": ("Main/Features/Foo/A.cs", 2)}, h.split_keys())

    def test_split_keys_RealTree_HasNone(self):
        # The guard judges only new keys; this keeps a registered key from being split later, which no
        # other gate sees (LocalizationKeyConsistencyTests compares the first literal only).
        self.assertGreater(sum(1 for _ in h.code_files()), 1000, "the walk found almost no C# files")
        self.assertEqual({}, h.split_keys())

    def test_main_DryRunWithASplitKey_Refuses(self):
        code, _insert, err = self._main("--dry-run", {"taom_split": ("Half ", "Main/A.cs", 4)},
                                        split={"taom_split": ("Main/A.cs", 4)})
        self.assertEqual(1, code)
        self.assertIn("taom_split  (Main/A.cs:4)", err)

    def test_main_NewSplitKey_RefusesWithItsPlace(self):
        code, insert, err = self._main("--apply", {"taom_split": ("Half ", "Main/A.cs", 4)},
                                       split={"taom_split": ("Main/A.cs", 4)})
        self.assertEqual(1, code)
        insert.assert_not_called()
        self.assertIn("taom_split  (Main/A.cs:4)", err)

    def test_main_SplitKeyAlreadyRegistered_IsNotJudged(self):
        code, insert, _err = self._main("--apply", {"taom_old": ("Half ", "Main/A.cs", 4)},
                                        split={"taom_old": ("Main/A.cs", 4)}, registered={"taom_old"})
        self.assertEqual(0, code)
        insert.assert_not_called()


class McmGroupPathTests(unittest.TestCase):
    """#1385 Phase 4: MCM splits a group path on '/' and translates each part, so each part carries its own key.
    LITERAL reads the first key with the whole rest of the path as its default."""

    def test_group_parts_TwoKeyedParts_GiveTwoRows(self):
        self.assertEqual([("taom_mcm_group_a", "Battle Tactics"), ("taom_mcm_group_b", "Mixed Formations")],
                         h.group_parts("taom_mcm_group_a", "Battle Tactics/{=taom_mcm_group_b}Mixed Formations"))

    def test_group_parts_OnePart_IsUnchanged(self):
        self.assertEqual([("taom_mcm_group_a", "Realm Borders")], h.group_parts("taom_mcm_group_a", "Realm Borders"))

    def test_group_parts_OtherKeyWithASlash_IsUnchanged(self):
        # Only an MCM group key is a path; any other default keeps its slash.
        self.assertEqual([("taom_x", "a/{=taom_y}b")], h.group_parts("taom_x", "a/{=taom_y}b"))

    def test_group_parts_UnkeyedChild_KeepsOnlyTheFirstPart(self):
        # MCM looks up the first part alone, so its row never holds the rest of the path.
        self.assertEqual([("taom_mcm_group_a", "A")], h.group_parts("taom_mcm_group_a", "A/B"))

    def test_group_parts_ThreeParts_GiveThreeRows(self):
        self.assertEqual([("taom_mcm_group_a", "A"), ("taom_mcm_group_b", "B"), ("taom_mcm_group_c", "C")],
                         h.group_parts("taom_mcm_group_a", "A/{=taom_mcm_group_b}B/{=taom_mcm_group_c}C"))

    def test_group_parts_NonTaomNestedKey_IsNotDeclared(self):
        # A vanilla key in taom_module_strings.xml overrides the vanilla text, so the harvester never registers one.
        self.assertEqual([("taom_mcm_group_a", "A")], h.group_parts("taom_mcm_group_a", "A/{=Xy12Ab34}Diplomacy"))

    def test_declared_keys_GroupPath_RegistersEachPart(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            cs = root / "Main" / "Features" / "S.cs"
            cs.parent.mkdir(parents=True)
            h.write_text(cs, '[SettingPropertyGroup("{=taom_mcm_group_a}A/{=taom_mcm_group_b}B")]\r\n')
            with mock.patch.object(h, "REPO_ROOT", root):
                found = h.declared_keys()
        self.assertEqual({"taom_mcm_group_a": ("A", "Main/Features/S.cs", 1),
                          "taom_mcm_group_b": ("B", "Main/Features/S.cs", 1)}, dict(found))


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
