#!/usr/bin/env python3
"""Unit tests for tools/generate_name_localization_strings.py, on synthetic XML, no game install.

Every test pins a way the generator could ship wrong content and still look right in a dry run:
a key already registered elsewhere getting a second, duplicate row; two troops sharing a key
losing one silently instead of keeping the first; the wrong XML root shape (the per-language
<base type="string"> wrapper instead of the bare <strings> root the English source files use,
which would make the file fail to parse as an English source at all); and unstable ordering that
would make every regeneration a full-file diff.
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_name_localization_strings as gen  # noqa: E402


def write(path: Path, content: str) -> Path:
    path.write_text(content, encoding="utf-8")
    return path


class ExtractFromFileTests(unittest.TestCase):
    def test_ExtractFromFile_SingleKey_ReturnsKeyAndDefault(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "troops.xml",
                      '<NPCCharacters><NPCCharacter id="t1" '
                      'name="{=aom_t1_name}[Gondor] Soldier" /></NPCCharacters>')
            entries = gen.extract_from_file(f)
            self.assertEqual(entries, [("aom_t1_name", "[Gondor] Soldier")])

    def test_ExtractFromFile_DuplicateKeyAcrossElements_FirstOccurrenceWins(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "troops.xml",
                      '<NPCCharacters>'
                      '<NPCCharacter id="t1" name="{=aom_dup_name}First Text" />'
                      '<NPCCharacter id="t2" name="{=aom_dup_name}Second Text" />'
                      '</NPCCharacters>')
            entries = gen.extract_from_file(f)
            self.assertEqual(entries, [("aom_dup_name", "First Text")])

    def test_ExtractFromFile_NoKeyedAttributes_ReturnsEmpty(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "clans.xml",
                      '<Factions><Faction id="c1" name="Plain Text No Key" /></Factions>')
            self.assertEqual(gen.extract_from_file(f), [])

    def test_ExtractFromFile_MultipleAttributeNames_ExtractsAll(self):
        # Kingdom identity spans name/short_name/title/ruler_title/text on one element.
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "kingdoms.xml",
                      '<Kingdoms><Kingdom id="k1" '
                      'name="{=taom_k1_name}Erebor" '
                      'short_name="{=taom_k1_short_name}Erebor" '
                      'title="{=taom_k1_title}Kingdom of Erebor" '
                      'ruler_title="{=taom_k1_ruler_title}King" '
                      'text="{=taom_k1_desc}The Lonely Mountain." /></Kingdoms>')
            entries = dict(gen.extract_from_file(f))
            self.assertEqual(len(entries), 5)
            self.assertEqual(entries["taom_k1_ruler_title"], "King")


class RegisteredIdsTests(unittest.TestCase):
    def test_RegisteredIds_ExistingSourceFile_ReturnsItsIds(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "taom_module_strings.xml",
                      '<strings>'
                      '<string id="taom_str_x" text="{=taom_str_x}Some Text" />'
                      '<string id="taom_str_y" text="{=taom_str_y}Other Text" />'
                      '</strings>')
            self.assertEqual(gen.registered_ids([f]), {"taom_str_x", "taom_str_y"})

    def test_RegisteredIds_MissingFile_SkippedWithoutError(self):
        missing = Path(tempfile.gettempdir()) / "does_not_exist_12345.xml"
        self.assertEqual(gen.registered_ids([missing]), set())


class BuildCategoryEntriesTests(unittest.TestCase):
    def test_BuildCategoryEntries_KeyAlreadyRegistered_Excluded(self):
        with tempfile.TemporaryDirectory() as td:
            tdir = Path(td)
            src = write(tdir / "troops_x.xml",
                        '<NPCCharacters>'
                        '<NPCCharacter id="t1" name="{=aom_registered_name}Already Done" />'
                        '<NPCCharacter id="t2" name="{=aom_new_name}Brand New" />'
                        '</NPCCharacters>')
            gen.CATEGORIES["_test_cat"] = {
                "sources": lambda: [src],
                "output": tdir / "out.xml",
                "header": "test",
            }
            try:
                entries = gen.build_category_entries("_test_cat", {"aom_registered_name"})
                self.assertEqual(entries, [("aom_new_name", "Brand New")])
            finally:
                del gen.CATEGORIES["_test_cat"]

    def test_BuildCategoryEntries_MultipleSourceFiles_SortedByKey(self):
        with tempfile.TemporaryDirectory() as td:
            tdir = Path(td)
            src1 = write(tdir / "troops_a.xml",
                         '<NPCCharacters><NPCCharacter id="t1" name="{=z_name}Z Troop" /></NPCCharacters>')
            src2 = write(tdir / "troops_b.xml",
                         '<NPCCharacters><NPCCharacter id="t2" name="{=a_name}A Troop" /></NPCCharacters>')
            gen.CATEGORIES["_test_cat"] = {
                "sources": lambda: [src1, src2],
                "output": tdir / "out.xml",
                "header": "test",
            }
            try:
                entries = gen.build_category_entries("_test_cat", set())
                self.assertEqual(entries, [("a_name", "A Troop"), ("z_name", "Z Troop")])
            finally:
                del gen.CATEGORIES["_test_cat"]


class BuildXmlTests(unittest.TestCase):
    def test_BuildXml_Entries_ProducesBareStringsRoot(self):
        """The English source files at ModuleData root use a bare <strings> root, NOT the
        <base type="string"><tags>...<strings> wrapper the per-language translation files use
        (confirmed against taom_module_strings.xml / taom_enlistment_strings.xml). Getting this
        wrong would make the generated file invisible to _parse_string_xml's source-file path."""
        xml_text = gen.build_xml([("aom_x_name", "Gondor Soldier")], "test header")
        root = ET.fromstring(xml_text)
        self.assertEqual(root.tag, "strings")
        strings = root.findall("string")
        self.assertEqual(len(strings), 1)
        self.assertEqual(strings[0].get("id"), "aom_x_name")
        self.assertEqual(strings[0].get("text"), "{=aom_x_name}Gondor Soldier")

    def test_BuildXml_EmptyEntries_StillParsesAsWellFormedXml(self):
        xml_text = gen.build_xml([], "test header")
        root = ET.fromstring(xml_text)
        self.assertEqual(root.tag, "strings")
        self.assertEqual(root.findall("string"), [])

    def test_BuildXml_RunTwiceSameInput_ByteIdentical(self):
        """Stable ordering: a re-run with no new content should not churn the file."""
        entries = [("b_key", "B Text"), ("a_key", "A Text")]
        first = gen.build_xml(entries, "header")
        second = gen.build_xml(entries, "header")
        self.assertEqual(first, second)


if __name__ == "__main__":
    unittest.main()
