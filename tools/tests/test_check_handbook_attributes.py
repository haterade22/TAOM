#!/usr/bin/env python3
"""Unit tests for tools/check_handbook_attributes.py (the modding-handbook gate).

The handbook under docs/modding/ documents XML attributes per engine class. Each
attribute table carries an `<!-- engine-table type= file= method= inert= -->`
marker naming the decompiled deserializer it was sourced from, and each worked
example carries an `<!-- example file= id= -->` marker. This tool diffs the
tables against the read idioms in the cited decompile file and confirms every
example id exists in its file.

Every test builds a SYNTHETIC tree in a tempdir: a docs folder, a fake decompile
dump, a fake repo and a fake game Modules folder. The suite never touches the
real dump or the real install, so it runs on a machine without either. Each
guard is exercised in BOTH directions (clean and broken), because a check that
has only ever been seen passing is not known to be able to fail.

Run:  python -m pytest tools/tests/test_check_handbook_attributes.py -q
  or:  python -m unittest discover -s tools/tests -p "test_*.py"
"""
import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_handbook_attributes as cha  # noqa: E402


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


# A deserializer in the shape ILSpy emits for the v1.4.8 dump: every read idiom
# the tool must recognise appears at least once, plus a private helper the
# method calls, a private helper it does NOT call, and a public method that
# reads an attribute the table must NOT be charged with.
FAKE_CS = '''using System.Xml;

namespace Fake.Engine;

public class Widget : MBObjectBase
{
	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
	{
		base.Deserialize(objectManager, node);
		XmlAttribute a1 = node.Attributes["plain_attr"];
		string s = node.Attributes?["optional_attr"]?.Value;
		string g = element.GetAttribute("get_attr");
		Culture = (BasicCultureObject)objectManager.ReadObjectReferenceFromXml("culture", typeof(BasicCultureObject), node);
		Owner = objectManager.ReadObjectReferenceFromXml<Hero>("owner", node);
		IsHero = XmlHelper.ReadBool(node, "is_hero");
		XmlHelper.ReadInt(ref _level, node, "level");
		Weight = XmlHelper.ReadFloat(node, "weight", 1f);
		Tag = XmlHelper.ReadString(node, "tag");
		Offset = ReadVec3(node, "offset");
		HeadBone = DeserializeBoneIndex(node, "head_bone", flag ? HeadBone : b, b, validateHasParentBone: true);
		DeserializeBoneIndexArray(list, node, flag, "ragdoll_bone_", b, validateHasParentBone: false);
		string weird = $"{{brace}} {a1.Value}";
		string other = "}";
		ReadExtras(node);
		foreach (XmlNode childNode in node.ChildNodes)
		{
			if (childNode.Name == "Flags" || childNode.Name == "flags")
			{
				continue;
			}
			if (!(childNode.Name == "Capsules"))
			{
				continue;
			}
			if (childNode.Name.Equals("Buildings", StringComparison.InvariantCultureIgnoreCase))
			{
				continue;
			}
			switch (childNode.Name)
			{
			case "Armor":
				break;
			case "Weapon":
				break;
			default:
				throw new Exception("Wrong type.");
			}
		}
		if (node.Name == "CraftedItem")
		{
			return;
		}
	}

	private void ReadExtras(XmlNode node)
	{
		XmlAttribute e = node.Attributes["extra_attr"];
		ReadDeeper(node);
	}

	private void ReadDeeper(XmlNode node)
	{
		XmlAttribute d = node.Attributes["deeper_attr"];
	}

	private void NeverCalled(XmlNode node)
	{
		XmlAttribute n = node.Attributes["never_called_attr"];
	}

	public void UnrelatedPublic(XmlNode node)
	{
		XmlAttribute u = node.Attributes["unrelated_attr"];
	}
}
'''

FAKE_CS_REL = "Core/Fake.Engine/Fake.Engine/Widget.cs"

# The complete, correct table for FAKE_CS: every read name documented, nothing
# extra. Tests break it one row at a time.
ALL_ATTRS = ["plain_attr", "optional_attr", "get_attr", "culture", "owner", "is_hero",
             "level", "weight", "tag", "offset", "head_bone", "ragdoll_bone_0",
             "extra_attr", "deeper_attr"]
ALL_ELEMENTS = ["Flags", "flags", "Capsules", "Buildings", "Armor", "Weapon", "CraftedItem"]


def _marker(type_name="Fake.Engine.Widget", file=FAKE_CS_REL, method="Deserialize", inert=""):
    return (f'<!-- engine-table type="{type_name}" file="{file}" '
            f'method="{method}" inert="{inert}" -->')


def _table(names):
    lines = ["| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |",
             "|---|---|---|---|---|---|"]
    for n in names:
        lines.append(f"| `{n}` | string | no | none | does a thing | `Widget.cs:10` |")
    return "\n".join(lines)


def _chapter(attr_names=None, element_names=None, inert="", extra=""):
    attr_names = ALL_ATTRS if attr_names is None else attr_names
    element_names = ALL_ELEMENTS if element_names is None else element_names
    return ("# Widgets\n\n## Attributes\n\n" + _marker(inert=inert) + "\n\n"
            + _table(attr_names) + "\n\n## Child elements\n\n" + _marker(inert=inert) + "\n\n"
            + _table(f"<{e}>" for e in element_names) + "\n" + extra)


class _Tree(unittest.TestCase):
    """Base: a synthetic docs folder, dump, repo and Modules folder."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.docs = self.root / "docs" / "modding"
        self.docs.mkdir(parents=True)
        self.dump = self.root / "dump"
        _write(self.dump / FAKE_CS_REL, FAKE_CS)
        self.repo = self.root / "repo"
        self.modules = self.root / "Modules"
        self.repo.mkdir()
        self.modules.mkdir()

    def tearDown(self):
        self._tmp.cleanup()

    def run_tool(self, **overrides):
        kwargs = dict(docs_dir=self.docs, dump_root=self.dump, repo_root=self.repo,
                      game_modules=self.modules, manifest_path=None)
        kwargs.update(overrides)
        return cha.run(**kwargs)

    @staticmethod
    def labels(report):
        return sorted(f["label"] for f in report["findings"])

    @staticmethod
    def names(report, label):
        return sorted(f["name"] for f in report["findings"] if f["label"] == label)


# ---------------------------------------------------------------------------
# Marker and table parsing
# ---------------------------------------------------------------------------

class MarkerParsingTests(unittest.TestCase):
    def test_engine_table_marker_fields_are_parsed(self):
        text = "intro\n" + _marker(inert="dead_one, dead_two") + "\n"
        markers = cha.parse_markers(text)
        self.assertEqual(len(markers), 1)
        m = markers[0]
        self.assertEqual(m["kind"], "engine-table")
        self.assertEqual(m["line"], 2)
        self.assertEqual(m["type"], "Fake.Engine.Widget")
        self.assertEqual(m["file"], FAKE_CS_REL)
        self.assertEqual(m["method"], "Deserialize")
        self.assertEqual(m["inert"], ["dead_one", "dead_two"])

    def test_example_marker_fields_are_parsed(self):
        text = '<!-- example file="Main/_Module/ModuleData/foo.xml" id="bar_1" -->\n'
        markers = cha.parse_markers(text)
        self.assertEqual(markers[0]["kind"], "example")
        self.assertEqual(markers[0]["file"], "Main/_Module/ModuleData/foo.xml")
        self.assertEqual(markers[0]["id"], "bar_1")

    def test_angle_brackets_inside_inert_do_not_break_the_marker(self):
        text = _marker(inert="<Module>, weight") + "\n"
        markers = cha.parse_markers(text)
        self.assertEqual(len(markers), 1)
        self.assertEqual(markers[0]["inert"], ["<Module>", "weight"])

    def test_other_html_comments_are_ignored(self):
        text = "<!-- measured: rg -c foo 2026-09-05 -->\n<!-- lint-allow-dash -->\n"
        self.assertEqual(cha.parse_markers(text), [])

    def test_method_defaults_to_deserialize_when_omitted(self):
        text = f'<!-- engine-table type="Fake.Engine.Widget" file="{FAKE_CS_REL}" -->\n'
        self.assertEqual(cha.parse_markers(text)[0]["method"], "Deserialize")


class TableParsingTests(unittest.TestCase):
    def test_column_one_backticked_names_are_collected(self):
        text = _marker() + "\n\n" + _table(["alpha", "beta"]) + "\n"
        lines = text.splitlines()
        rows = cha.parse_table_after(lines, 0)
        self.assertEqual([(r["name"], r["is_element"]) for r in rows],
                         [("alpha", False), ("beta", False)])

    def test_angle_bracketed_name_is_an_element_row(self):
        text = _marker() + "\n" + _table(["<Flags>", "plain"]) + "\n"
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([(r["name"], r["is_element"]) for r in rows],
                         [("Flags", True), ("plain", False)])

    def test_row_line_numbers_are_one_based_file_lines(self):
        text = "# H\n\n" + _marker() + "\n\n" + _table(["alpha"]) + "\n"
        rows = cha.parse_table_after(text.splitlines(), 2)
        # header on line 5, separator on 6, alpha on 7
        self.assertEqual(rows[0]["line"], 7)

    def test_no_table_after_marker_returns_none(self):
        text = _marker() + "\n\nSome prose instead of a table.\n"
        self.assertIsNone(cha.parse_table_after(text.splitlines(), 0))

    def test_table_stops_at_first_non_table_line(self):
        text = (_marker() + "\n" + _table(["alpha"]) + "\n\nprose\n"
                + _table(["not_mine"]) + "\n")
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([r["name"] for r in rows], ["alpha"])

    def test_rows_without_a_backticked_name_are_skipped(self):
        text = _marker() + "\n" + _table(["alpha"]) + "\n| (none) | | | | | |\n"
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([r["name"] for r in rows], ["alpha"])

    def test_element_at_attribute_row_expands_to_one_claim_per_part(self):
        # `Deps/Dep@Id` claims two elements and one attribute; `@Optional` alone
        # claims an attribute; `Subs/Sub` claims two elements.
        text = _marker() + "\n" + _table(["Deps/Dep@Id", "@Optional", "Subs/Sub"]) + "\n"
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([(r["name"], r["is_element"]) for r in rows],
                         [("Deps", True), ("Dep", True), ("Id", False),
                          ("Optional", False), ("Subs", True), ("Sub", True)])
        self.assertEqual(rows[0]["raw"], "Deps/Dep@Id")

    def test_xpath_wildcard_segments_are_not_element_claims(self):
        # `IncludedGameTypes/*/@value` (module-taom.md) means "every child": the
        # `*` is a wildcard, not an element the engine reaches for by name.
        text = _marker() + "\n" + _table(["IncludedGameTypes/*/@value", "a/./b"]) + "\n"
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([(r["name"], r["is_element"]) for r in rows],
                         [("IncludedGameTypes", True), ("value", False),
                          ("a", True), ("b", True)])

    def test_every_backticked_token_in_column_one_is_a_claim(self):
        text = (_marker() + "\n| A | T |\n|---|---|\n"
                "| `Deps/Dep@Id` (+ `@DependentVersion`, `@Optional`) | list |\n")
        rows = cha.parse_table_after(text.splitlines(), 0)
        self.assertEqual([r["name"] for r in rows],
                         ["Deps", "Dep", "Id", "DependentVersion", "Optional"])


# ---------------------------------------------------------------------------
# Decompile read extraction
# ---------------------------------------------------------------------------

class ExtractReadsTests(unittest.TestCase):
    def setUp(self):
        self.reads = cha.extract_reads(FAKE_CS, "Deserialize")

    def test_method_is_found(self):
        self.assertTrue(self.reads["found"])

    def test_every_attribute_idiom_is_extracted(self):
        expected = {"plain_attr", "optional_attr", "get_attr", "culture", "owner",
                    "is_hero", "level", "weight", "tag", "offset", "head_bone"}
        self.assertTrue(expected <= set(self.reads["attributes"]),
                        expected - set(self.reads["attributes"]))

    def test_bone_index_array_literal_is_a_prefix(self):
        # The literal is the 4th argument in the real Monster.cs call, and it is a
        # prefix: the engine appends 0, 1, 2 ... to it.
        self.assertIn("ragdoll_bone_", self.reads["prefixes"])
        self.assertNotIn("ragdoll_bone_", self.reads["attributes"])

    def test_every_element_idiom_is_extracted(self):
        expected = {"Flags", "flags", "Capsules", "Buildings", "Armor", "Weapon", "CraftedItem"}
        self.assertEqual(expected, set(self.reads["elements"]))

    def test_private_helpers_called_from_the_method_are_followed_transitively(self):
        self.assertIn("extra_attr", self.reads["attributes"])
        self.assertIn("deeper_attr", self.reads["attributes"])
        self.assertEqual(sorted(self.reads["helpers"]), ["ReadDeeper", "ReadExtras"])

    def test_uncalled_and_public_methods_are_not_charged(self):
        self.assertNotIn("never_called_attr", self.reads["attributes"])
        self.assertNotIn("unrelated_attr", self.reads["attributes"])

    def test_read_lines_are_recorded(self):
        # `plain_attr` is read on line 10 of FAKE_CS.
        self.assertEqual(self.reads["attributes"]["plain_attr"], 10)

    def test_base_deserialize_call_is_recorded(self):
        self.assertTrue(self.reads["calls_base"])

    def test_braces_inside_string_literals_do_not_end_the_body(self):
        # `$"{{brace}}"` and `"}"` both sit inside the body before ReadExtras;
        # a naive brace count would close the method early and lose the helper.
        self.assertIn("extra_attr", self.reads["attributes"])

    def test_missing_method_is_reported_not_raised(self):
        reads = cha.extract_reads(FAKE_CS, "NoSuchMethod")
        self.assertFalse(reads["found"])
        self.assertEqual(reads["attributes"], {})

    def test_dynamic_attribute_lookups_are_not_extracted(self):
        src = ('class A { void Deserialize(XmlNode node) { '
               'XmlAttribute x = node.Attributes[value.ToString()]; '
               'string y = node.Attributes[name].Value; } }')
        reads = cha.extract_reads(src, "Deserialize")
        self.assertEqual(reads["attributes"], {})

    def test_overloads_with_the_same_name_are_all_scanned(self):
        src = ('class A {\n'
               'public void Deserialize(string key) { X = key; }\n'
               'public override void Deserialize(MBObjectManager m, XmlNode node)\n'
               '{ XmlAttribute a = node.Attributes["from_xml"]; }\n'
               '}\n')
        reads = cha.extract_reads(src, "Deserialize")
        self.assertIn("from_xml", reads["attributes"])

    def test_xpath_selects_are_element_reads(self):
        # ModuleInfo.LoadWithFullPath reads SubModule.xml through SelectSingleNode /
        # SelectNodes (ModuleInfo.cs:80-149); each path segment is an element.
        reads = cha.extract_reads(SUBMODULE_CS, "LoadWithFullPath")
        self.assertEqual(set(reads["elements"]),
                         {"Module", "Name", "DependedModules", "DependedModule", "Nested", "Leaf"})
        self.assertEqual(set(reads["attributes"]), {"value", "Id", "Optional"})


# A loader in the shape of TaleWorlds.ModuleManager.ModuleInfo: elements reached
# by XPath, attributes on each. Documented with `Element@attr` rows.
SUBMODULE_CS = '''namespace Fake.Engine;

public class Loader
{
	public void LoadWithFullPath(string fullPath)
	{
		XmlNode xmlNode = xmlDocument.SelectSingleNode("Module");
		Name = xmlNode.SelectSingleNode("Name").Attributes["value"].InnerText;
		XmlNodeList list = xmlNode.SelectSingleNode("DependedModules")?.SelectNodes("DependedModule");
		string id = list[0].Attributes["Id"].InnerText;
		bool.TryParse(list[0].Attributes["Optional"]?.InnerText, out var result);
		XmlNode deep = xmlNode.SelectSingleNode("Nested/Leaf");
	}
}
'''
SUBMODULE_CS_REL = "Platform/Fake.Engine/Loader.cs"


# ---------------------------------------------------------------------------
# Table checks: FABRICATION and GAP
# ---------------------------------------------------------------------------

class TableCheckTests(_Tree):
    def test_complete_table_is_clean(self):
        _write(self.docs / "widgets.md", _chapter())
        report = self.run_tool()
        self.assertEqual(report["findings"], [])
        self.assertEqual(report["markers"], 2)
        self.assertEqual(cha.exit_code_for(report), 0)

    def test_documented_but_unread_attribute_is_fabrication(self):
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["invented_attr"]))
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["FABRICATION"])
        f = report["findings"][0]
        self.assertEqual(f["name"], "invented_attr")
        self.assertEqual(f["doc"], "widgets.md")
        self.assertEqual(f["type"], "Fake.Engine.Widget")
        self.assertGreater(f["line"], 0)
        self.assertEqual(cha.exit_code_for(report), 1)

    def test_fabrication_hint_mentions_base_deserialize(self):
        # `id` is read by MBObjectBase, not by the cited class: the author needs a
        # second marker for the base file, and the finding must say so.
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["id"]))
        report = self.run_tool()
        self.assertIn("base.Deserialize", report["findings"][0]["message"])

    def test_read_but_undocumented_attribute_is_gap(self):
        _write(self.docs / "widgets.md",
               _chapter(attr_names=[a for a in ALL_ATTRS if a != "weight"]))
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["GAP"])
        self.assertEqual(report["findings"][0]["name"], "weight")
        self.assertEqual(cha.exit_code_for(report), 1)

    def test_inert_attribute_is_not_a_gap(self):
        _write(self.docs / "widgets.md",
               _chapter(attr_names=[a for a in ALL_ATTRS if a != "weight"], inert="weight"))
        self.assertEqual(self.run_tool()["findings"], [])

    def test_inert_attribute_may_still_be_documented(self):
        # The contract puts inert attrs in the table too ("read but has no effect").
        _write(self.docs / "widgets.md", _chapter(inert="weight"))
        self.assertEqual(self.run_tool()["findings"], [])

    def test_read_but_undocumented_element_is_gap_with_angle_brackets(self):
        _write(self.docs / "widgets.md",
               _chapter(element_names=[e for e in ALL_ELEMENTS if e != "Armor"]))
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["GAP"])
        self.assertEqual(report["findings"][0]["name"], "<Armor>")

    def test_inert_accepts_angle_bracketed_element_names(self):
        _write(self.docs / "widgets.md",
               _chapter(element_names=[e for e in ALL_ELEMENTS if e != "Armor"], inert="<Armor>"))
        report = self.run_tool()
        self.assertEqual(report["findings"], [])
        # A `>` inside inert= once stopped the marker from parsing at all, which
        # made this exact assertion pass with zero markers checked.
        self.assertEqual(report["markers"], 2)

    def test_element_row_is_matched_against_element_idioms_only(self):
        # `Flags` written WITHOUT angle brackets is an attribute claim, and no
        # attribute called Flags is read: FABRICATION, even though the element is.
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["Flags"]))
        report = self.run_tool()
        self.assertEqual(self.names(report, "FABRICATION"), ["Flags"])

    def test_attribute_name_in_element_table_is_fabrication(self):
        _write(self.docs / "widgets.md", _chapter(element_names=ALL_ELEMENTS + ["weight"]))
        report = self.run_tool()
        self.assertEqual(self.names(report, "FABRICATION"), ["<weight>"])

    def test_indexed_name_matches_a_bone_array_prefix(self):
        # ragdoll_bone_3 documents the ragdoll_bone_ prefix; ragdoll_bone_x does not.
        _write(self.docs / "widgets.md",
               _chapter(attr_names=[a for a in ALL_ATTRS if a != "ragdoll_bone_0"]
                        + ["ragdoll_bone_3"]))
        self.assertEqual(self.run_tool()["findings"], [])
        _write(self.docs / "widgets.md",
               _chapter(attr_names=[a for a in ALL_ATTRS if a != "ragdoll_bone_0"]
                        + ["ragdoll_bone_x"]))
        report = self.run_tool()
        self.assertEqual(self.names(report, "FABRICATION"), ["ragdoll_bone_x"])
        self.assertEqual(self.names(report, "GAP"), ["ragdoll_bone_"])

    def test_undocumented_prefix_is_a_gap(self):
        _write(self.docs / "widgets.md",
               _chapter(attr_names=[a for a in ALL_ATTRS if a != "ragdoll_bone_0"]))
        report = self.run_tool()
        self.assertEqual(self.names(report, "GAP"), ["ragdoll_bone_"])

    def test_cited_file_absent_from_dump_is_missing_source(self):
        _write(self.docs / "widgets.md",
               "# W\n\n" + _marker(file="Core/Nope/Nope.cs") + "\n\n" + _table(["a"]) + "\n")
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["MISSING_SOURCE"])
        self.assertIn("Core/Nope/Nope.cs", report["findings"][0]["message"])

    def test_cited_method_absent_is_missing_method(self):
        _write(self.docs / "widgets.md",
               "# W\n\n" + _marker(method="Nope") + "\n\n" + _table(["a"]) + "\n")
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["MISSING_METHOD"])

    def test_marker_without_a_table_is_reported(self):
        _write(self.docs / "widgets.md", "# W\n\n" + _marker() + "\n\nNo table here.\n")
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["NO_TABLE"])

    def test_two_chapters_are_reported_separately(self):
        _write(self.docs / "a.md", _chapter(attr_names=ALL_ATTRS + ["fake_a"]))
        _write(self.docs / "b.md", _chapter(attr_names=ALL_ATTRS + ["fake_b"]))
        report = self.run_tool()
        docs = sorted((f["doc"], f["name"]) for f in report["findings"])
        self.assertEqual(docs, [("a.md", "fake_a"), ("b.md", "fake_b")])

    def test_nested_docs_are_scanned(self):
        _write(self.docs / "sub" / "deep.md", _chapter(attr_names=ALL_ATTRS + ["fake"]))
        report = self.run_tool()
        self.assertEqual(report["findings"][0]["doc"], "sub/deep.md")

    def test_empty_docs_folder_is_clean_with_zero_markers(self):
        report = self.run_tool()
        self.assertEqual(report["findings"], [])
        self.assertEqual(report["markers"], 0)
        self.assertEqual(cha.exit_code_for(report), 0)

    def test_dump_root_absent_raises_not_a_silent_clean(self):
        _write(self.docs / "widgets.md", _chapter())
        with self.assertRaises(cha.DumpRootMissing):
            self.run_tool(dump_root=self.root / "no_such_dump")


class SubModuleStyleTableTests(_Tree):
    """Tables for `<Name value="..."/>` files, written as `Element@attr` rows."""

    ROWS = ["Name@value", "DependedModules/DependedModule@Id", "@Optional", "Nested/Leaf"]

    def setUp(self):
        super().setUp()
        _write(self.dump / SUBMODULE_CS_REL, SUBMODULE_CS)

    def _doc(self, rows, inert="<Module>"):
        marker = _marker(type_name="Fake.Engine.Loader", file=SUBMODULE_CS_REL,
                         method="LoadWithFullPath", inert=inert)
        _write(self.docs / "loader.md", "# L\n\n" + marker + "\n\n" + _table(rows) + "\n")

    def test_complete_path_table_is_clean(self):
        self._doc(self.ROWS)
        self.assertEqual(self.run_tool()["findings"], [])

    def test_root_element_must_be_documented_or_inert(self):
        self._doc(self.ROWS, inert="")
        report = self.run_tool()
        self.assertEqual(self.names(report, "GAP"), ["<Module>"])

    def test_invented_element_in_a_path_row_is_fabrication(self):
        self._doc(self.ROWS + ["Nope@value"])
        report = self.run_tool()
        self.assertEqual(self.names(report, "FABRICATION"), ["<Nope>"])
        self.assertIn("Nope@value", report["findings"][0]["message"])

    def test_invented_attribute_in_a_path_row_is_fabrication(self):
        self._doc(self.ROWS + ["Name@nope"])
        report = self.run_tool()
        self.assertEqual(self.names(report, "FABRICATION"), ["nope"])

    def test_omitted_attribute_in_a_path_table_is_gap(self):
        self._doc([r for r in self.ROWS if r != "@Optional"])
        report = self.run_tool()
        self.assertEqual(self.names(report, "GAP"), ["Optional"])


# ---------------------------------------------------------------------------
# Example markers
# ---------------------------------------------------------------------------

class ExampleCheckTests(_Tree):
    XML = ('<Items>\n  <Item id="gondor_sword" name="Sword" />\n'
           '  <Item id="gondor_sword_2" owner_id="gondor_shield" />\n</Items>\n')

    def _doc(self, file, ident):
        _write(self.docs / "ex.md",
               f'# Ex\n\n<!-- example file="{file}" id="{ident}" -->\n\n```xml\n<Item />\n```\n')

    def test_repo_relative_example_id_is_found(self):
        _write(self.repo / "Main" / "_Module" / "ModuleData" / "items.xml", self.XML)
        self._doc("Main/_Module/ModuleData/items.xml", "gondor_sword")
        report = self.run_tool()
        self.assertEqual(report["findings"], [])
        self.assertEqual(report["markers"], 1)

    def test_module_relative_example_id_is_found_under_game_modules(self):
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "x.xml", self.XML)
        self._doc("LOTRLOME_Armory/ModuleData/x.xml", "gondor_sword_2")
        self.assertEqual(self.run_tool()["findings"], [])

    def test_absent_id_is_missing_example(self):
        _write(self.repo / "items.xml", self.XML)
        self._doc("items.xml", "rohan_sword")
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["MISSING_EXAMPLE"])
        self.assertEqual(report["findings"][0]["name"], "rohan_sword")
        self.assertEqual(cha.exit_code_for(report), 1)

    def test_absent_file_is_missing_example_naming_both_roots(self):
        self._doc("Main/nope.xml", "gondor_sword")
        report = self.run_tool()
        self.assertEqual(self.labels(report), ["MISSING_EXAMPLE"])
        msg = report["findings"][0]["message"]
        self.assertIn(str(self.repo), msg)
        self.assertIn(str(self.modules), msg)

    def test_id_match_is_whole_value_not_substring(self):
        _write(self.repo / "items.xml", self.XML)
        self._doc("items.xml", "gondor_sw")
        self.assertEqual(self.labels(self.run_tool()), ["MISSING_EXAMPLE"])

    def test_id_match_ignores_suffixed_attribute_names(self):
        # owner_id="gondor_shield" must not satisfy id="gondor_shield".
        _write(self.repo / "items.xml", self.XML)
        self._doc("items.xml", "gondor_shield")
        self.assertEqual(self.labels(self.run_tool()), ["MISSING_EXAMPLE"])

    def test_id_exists_in_file_handles_single_quotes_and_spacing(self):
        p = self.repo / "q.xml"
        _write(p, "<A>\n<B id = 'spaced_id'/>\n</A>\n")
        self.assertTrue(cha.id_exists_in_file(p, "spaced_id"))
        self.assertFalse(cha.id_exists_in_file(p, "spaced"))

    def test_submodule_xml_identity_element_satisfies_the_id(self):
        # A module's identity is `<Id value="..."/>` (Dependencies/_Module/SubModule.xml:4).
        p = self.repo / "SubModule.xml"
        _write(p, '<Module>\n\t<Id value="TAOM.Dependencies" />\n\t<Name value="TAOM" />\n</Module>\n')
        self.assertTrue(cha.id_exists_in_file(p, "TAOM.Dependencies"))
        self.assertFalse(cha.id_exists_in_file(p, "TAOM.Dependencies2"))
        self.assertFalse(cha.id_exists_in_file(p, "TAOM"), "<Name value> is not an identity")

    def test_examples_do_not_need_the_dump(self):
        # A machine without the dump can still check examples when the manifest
        # covers the tables; with no engine-table markers there is nothing to look up.
        _write(self.repo / "items.xml", self.XML)
        self._doc("items.xml", "gondor_sword")
        report = self.run_tool(dump_root=self.root / "no_dump")
        self.assertEqual(report["findings"], [])


# ---------------------------------------------------------------------------
# Manifest: --update and dump-less checking
# ---------------------------------------------------------------------------

class ManifestTests(_Tree):
    def test_manifest_records_read_sets_per_marker(self):
        _write(self.docs / "widgets.md", _chapter())
        report = self.run_tool()
        manifest = report["manifest"]
        key = cha.marker_key({"type": "Fake.Engine.Widget", "file": FAKE_CS_REL,
                              "method": "Deserialize"})
        self.assertIn(key, manifest["markers"])
        entry = manifest["markers"][key]
        self.assertEqual(entry["attributes"], sorted(entry["attributes"]))
        self.assertIn("plain_attr", entry["attributes"])
        self.assertIn("Flags", entry["elements"])
        self.assertEqual(entry["prefixes"], ["ragdoll_bone_"])
        self.assertEqual(manifest["dump_root_name"], "dump")

    def test_update_writes_manifest_file(self):
        _write(self.docs / "widgets.md", _chapter())
        out = self.root / "manifest.json"
        cha.write_manifest(self.run_tool()["manifest"], out)
        data = json.loads(out.read_text(encoding="utf-8"))
        self.assertIn("markers", data)
        self.assertFalse(out.read_bytes().startswith(b"\xef\xbb\xbf"))

    def test_manifest_is_stable_across_runs(self):
        _write(self.docs / "widgets.md", _chapter())
        a = json.dumps(self.run_tool()["manifest"], sort_keys=True)
        b = json.dumps(self.run_tool()["manifest"], sort_keys=True)
        self.assertEqual(a, b)

    def test_without_dump_tables_are_checked_against_the_manifest(self):
        _write(self.docs / "widgets.md", _chapter())
        out = self.root / "manifest.json"
        cha.write_manifest(self.run_tool()["manifest"], out)
        # Now break the table and hide the dump: the manifest must still catch it.
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["invented"]))
        report = self.run_tool(dump_root=self.root / "no_dump", manifest_path=out)
        self.assertEqual(report["source"], "manifest")
        self.assertEqual(self.names(report, "FABRICATION"), ["invented"])

    def test_marker_absent_from_manifest_is_manifest_stale(self):
        _write(self.docs / "widgets.md", _chapter())
        out = self.root / "manifest.json"
        cha.write_manifest(self.run_tool()["manifest"], out)
        _write(self.docs / "other.md",
               "# O\n\n" + _marker(type_name="Fake.Engine.Other", file="Core/Other.cs")
               + "\n\n" + _table(["x"]) + "\n")
        report = self.run_tool(dump_root=self.root / "no_dump", manifest_path=out)
        self.assertEqual(self.labels(report), ["MANIFEST_STALE"])
        self.assertEqual(cha.exit_code_for(report), 1)

    def test_dump_wins_over_manifest_when_both_exist(self):
        _write(self.docs / "widgets.md", _chapter())
        out = self.root / "manifest.json"
        cha.write_manifest(self.run_tool()["manifest"], out)
        report = self.run_tool(manifest_path=out)
        self.assertEqual(report["source"], "dump")

    def test_no_dump_and_no_manifest_raises(self):
        _write(self.docs / "widgets.md", _chapter())
        with self.assertRaises(cha.DumpRootMissing):
            self.run_tool(dump_root=self.root / "no_dump", manifest_path=self.root / "absent.json")


# ---------------------------------------------------------------------------
# Dump-root resolution and the CLI
# ---------------------------------------------------------------------------

class DumpRootResolutionTests(unittest.TestCase):
    def test_cli_value_wins(self):
        env = {cha.DUMP_ENV_VAR: "D:/env"}
        self.assertEqual(cha.resolve_dump_root("D:/cli", env), Path("D:/cli"))

    def test_env_var_beats_default(self):
        env = {cha.DUMP_ENV_VAR: "D:/env"}
        self.assertEqual(cha.resolve_dump_root(None, env), Path("D:/env"))

    def test_blank_env_var_is_unset(self):
        env = {cha.DUMP_ENV_VAR: "   "}
        self.assertEqual(cha.resolve_dump_root(None, env), Path(cha.DEFAULT_DUMP_ROOT))

    def test_default_names_the_v148_dump(self):
        self.assertTrue(cha.DEFAULT_DUMP_ROOT.endswith("_categories_v1.4.8"))


class CliTests(_Tree):
    def _main(self, *argv):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cha.main(list(argv))
        return code, out.getvalue(), err.getvalue()

    def _base_args(self):
        return ["--docs", str(self.docs), "--dump-root", str(self.dump),
                "--repo-root", str(self.repo), "--game-modules", str(self.modules),
                "--manifest", str(self.root / "manifest.json")]

    def test_clean_run_exits_0(self):
        _write(self.docs / "widgets.md", _chapter())
        code, out, _ = self._main(*self._base_args())
        self.assertEqual(code, 0)
        self.assertIn("2 marker", out)

    def test_findings_exit_1_and_are_printed_with_label_doc_and_line(self):
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["invented"]))
        code, out, _ = self._main(*self._base_args())
        self.assertEqual(code, 1)
        self.assertRegex(out, r"FABRICATION\s+widgets\.md:\d+ .*invented")

    def test_absent_dump_root_exits_2_with_a_clear_message(self):
        _write(self.docs / "widgets.md", _chapter())
        args = self._base_args()
        args[args.index("--dump-root") + 1] = str(self.root / "no_dump")
        code, _, err = self._main(*args)
        self.assertEqual(code, 2)
        self.assertIn(str(self.root / "no_dump"), err)
        self.assertIn(cha.DUMP_ENV_VAR, err)
        self.assertIn("--update", err)

    def test_update_writes_the_manifest_and_exits_on_findings(self):
        _write(self.docs / "widgets.md", _chapter())
        code, _, _ = self._main(*self._base_args(), "--update")
        self.assertEqual(code, 0)
        data = json.loads((self.root / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(len(data["markers"]), 1)

    def test_update_without_dump_exits_2(self):
        _write(self.docs / "widgets.md", _chapter())
        args = self._base_args()
        args[args.index("--dump-root") + 1] = str(self.root / "no_dump")
        code, _, err = self._main(*args, "--update")
        self.assertEqual(code, 2)
        self.assertIn("--update", err)

    def test_json_writes_the_report(self):
        _write(self.docs / "widgets.md", _chapter(attr_names=ALL_ATTRS + ["invented"]))
        out_path = self.root / "report.json"
        code, _, _ = self._main(*self._base_args(), "--json", str(out_path))
        self.assertEqual(code, 1)
        data = json.loads(out_path.read_text(encoding="utf-8"))
        self.assertEqual(data["findings"][0]["label"], "FABRICATION")
        self.assertEqual(data["exit_code"], 1)

    def test_missing_dump_falls_back_to_manifest_and_says_so(self):
        _write(self.docs / "widgets.md", _chapter())
        self._main(*self._base_args(), "--update")
        args = self._base_args()
        args[args.index("--dump-root") + 1] = str(self.root / "no_dump")
        code, out, _ = self._main(*args)
        self.assertEqual(code, 0)
        self.assertIn("manifest", out.lower())


if __name__ == "__main__":
    unittest.main()


class MarkersInsideCodeAreIllustrationsTests(unittest.TestCase):
    """A chapter documents its own marker syntax; those literals are not claims.

    Regression for 2026-09-05: the hub's "how to read a chapter" section tripped NO_TABLE and
    MISSING_EXAMPLE on its own explanation, and escaping the delimiter to get past the gate
    rendered the HTML entity literally to the reader, because a markdown code span does not
    decode entities. The gate has to ignore code, so the prose can stay correct.
    """

    def test_marker_in_inline_code_span_is_ignored(self):
        text = 'Explain `<!-- engine-table type="X" file="Y" method="Deserialize" -->` here.\n'
        self.assertEqual(cha.parse_markers(text), [])

    def test_marker_in_fenced_block_is_ignored(self):
        text = 'Intro\n\n```\n<!-- example file="a.xml" id="b" -->\n```\n\nOutro\n'
        self.assertEqual(cha.parse_markers(text), [])

    def test_real_marker_outside_code_is_still_found(self):
        text = ('`<!-- engine-table type="Ignored" file="i.cs" -->`\n\n'
                '<!-- engine-table type="Real.Type" file="r.cs" method="Deserialize" -->\n\n'
                '| Attribute |\n|---|\n| `id` |\n')
        markers = cha.parse_markers(text)
        self.assertEqual([m.get('type') for m in markers], ['Real.Type'])

    def test_line_numbers_survive_masking(self):
        text = 'a\n`<!-- example file="x" id="y" -->`\nb\n<!-- example file="real.xml" id="z" -->\n'
        markers = cha.parse_markers(text)
        self.assertEqual(len(markers), 1)
        self.assertEqual(markers[0]['line'], 4)
