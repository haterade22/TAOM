#!/usr/bin/env python3
"""tools/apply_armour_cover_flags.py: the cover and visibility attributes of the Armory's armour, held in a repo spec.

Why it exists: the live LOTRLOME_Armory is unversioned, so a reinstall, a sync from the mirror or an art drop puts
back the values Mike corrected by hand (beards under helmets, crowns, gloves, boots). The spec holds the rules and a
snapshot of every item; these tests cover the tool on small synthetic Armories in temp folders, plus the shipped spec.

Run: python -m unittest tools.tests.test_apply_armour_cover_flags
"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_armour_cover_flags as tool  # noqa: E402
import audit_leg_cover_flags  # noqa: E402

BOM = b"\xef\xbb\xbf"
HEAD_REL = "LOTRLOME_items/gondor/head_armors.xml"
LEG_REL = "LOTRLOME_items/gondor/leg_armors.xml"

# A commented-out item, an attribute name inside a value, a multi-line <Armor> and a one-line <Armor>.
HEAD = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n<Items>\r\n'
    '    <!-- <Item id="ghost" Type="HeadArmor"><ItemComponent><Armor hair_cover_type="all" /></ItemComponent></Item> -->\r\n'
    '    <Item\r\n        id="h_crown"\r\n        name="{=x}A > B Crown hair_cover_type=&quot;none&quot;"\r\n'
    '        mesh="m_crown"\r\n        Type="HeadArmor">\r\n        <ItemComponent>\r\n            <Armor\r\n'
    '                head_armor="20"\r\n                hair_cover_type="all"\r\n                modifier_group="plate"\r\n'
    '                beard_cover_type="none" />\r\n        </ItemComponent>\r\n    </Item>\r\n'
    '    <Item id="h_helm" name="Helm" mesh="m_helm" Type="HeadArmor"><ItemComponent>'
    '<Armor head_armor="30" hair_cover_type="Type2" /></ItemComponent></Item>\r\n'
    '</Items>\r\n')
LEGS = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n<Items>\r\n'
    '    <Item id="l_boot" mesh="boot_a" Type="LegArmor"><ItemComponent><Armor leg_armor="9" covers_legs="true" />'
    '</ItemComponent></Item>\r\n'
    '    <Item id="l_shoe" mesh="shoe_a" Type="LegArmor"><ItemComponent><Armor leg_armor="3" />'
    '</ItemComponent></Item>\r\n'
    '</Items>\r\n')

SNAP_ALL = {
    HEAD_REL: {"h_crown": {"hair_cover_type": "all", "beard_cover_type": "none"}, "h_helm": {"hair_cover_type": "type2"}},
    LEG_REL: {"l_boot": {"covers_legs": "true"}, "l_shoe": {}},
}


def rule(select, set_, reason="test reason", date="2026-11-14"):
    return {"date": date, "reason": reason, "select": select, "set": set_}


def spec_text(rules=(), files=None):
    return tool.dump_spec(tool.Spec(comment=["test"], rules=list(rules), taken="2026-11-14",
                                    files=SNAP_ALL if files is None else files))


NEW_REL = "LOTRLOME_items/gondor/new_armors.xml"
NEW_BAD = (
    '<Items>\r\n'
    '    <Item id="n_helm" mesh="m_new" Type="HeadArmor"><ItemComponent><Armor head_armor="3" hair_cover_type="tpye1" />'
    '</ItemComponent></Item>\r\n'
    '    <Item id="n_gloves" mesh="g_new" Type="HandArmor"><ItemComponent><Armor arm_armor="3" covers_hands="yes" />'
    '</ItemComponent></Item>\r\n'
    '</Items>\r\n')


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.armory = Path(self.tmp.name) / "LOTRLOME_Armory"
        self.put(HEAD_REL, BOM + HEAD.encode("utf-8"))
        self.put(LEG_REL, LEGS.encode("utf-8"))
        self.spec = Path(self.tmp.name) / "spec.json"

    def put(self, rel, data):
        path = self.armory / "ModuleData" / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def raw(self, rel):
        return (self.armory / "ModuleData" / rel).read_bytes()

    def text(self, rel):
        return self.raw(rel).decode("utf-8-sig")

    def write_spec(self, rules=(), files=None):
        self.spec.write_text(spec_text(rules, files), encoding="utf-8", newline="\n")

    def run_tool(self, *args, armory=None):
        out = io.StringIO()
        with redirect_stdout(out):
            code = tool.main(["--armory-path", str(armory or self.armory), "--spec", str(self.spec), *args])
        return code, out.getvalue()

    def baks(self):
        return sorted(p.name for p in (self.armory / "ModuleData").rglob("*") if ".bak-" in p.name)


class SpecTests(unittest.TestCase):
    def load(self, **kw):
        path = Path(tempfile.mkdtemp()) / "s.json"
        path.write_text(spec_text(**kw), encoding="utf-8", newline="\n")
        return tool.load_spec(path)

    def test_the_dump_is_canonical(self):
        text = spec_text([rule({"ids": ["a", "b"]}, {"hair_cover_type": "none"})])
        path = Path(tempfile.mkdtemp()) / "s.json"
        path.write_text(text, encoding="utf-8", newline="\n")
        self.assertEqual(tool.dump_spec(tool.load_spec(path)), text)

    def test_a_snapshot_item_is_one_line(self):
        text = spec_text()
        self.assertIn('"h_crown": {"hair_cover_type": "all", "beard_cover_type": "none"}', text)

    def test_an_unknown_attribute_is_refused(self):
        with self.assertRaises(tool.SpecError):
            self.load(rules=[rule({"ids": ["a"]}, {"weight": "2"})])

    def test_an_unknown_cover_value_is_refused_in_a_rule(self):
        for value in ("type5", "type9", "ALL ", ""):
            with self.subTest(value=value), self.assertRaises(tool.SpecError):
                self.load(rules=[rule({"ids": ["a"]}, {"hair_cover_type": value})])

    def test_an_unknown_cover_value_is_refused_in_the_snapshot(self):
        with self.assertRaises(tool.SpecError):
            self.load(files={HEAD_REL: {"h": {"beard_cover_type": "type9"}}})

    def test_every_value_the_engine_enum_has_is_accepted(self):
        # HairCoverTypes and BeardCoverTypes: None, Type1 to Type4, All (ArmorComponent.cs, v1.5.7)
        for value in ("none", "type1", "type2", "type3", "type4", "all"):
            with self.subTest(value=value):
                self.load(rules=[rule({"ids": ["a"]}, {"hair_cover_type": value, "beard_cover_type": value})])

    def test_a_value_must_suit_its_attribute(self):
        for attr, value in (("mane_cover_type", "type3"), ("tail_cover_type", "type1"),
                            ("covers_legs", "all"), ("body_mesh_type", "none")):
            with self.subTest(attr=attr), self.assertRaises(tool.SpecError):
                self.load(rules=[rule({"ids": ["a"]}, {attr: value})])

    def test_a_rule_needs_a_reason_a_date_a_selector_and_a_value(self):
        good = rule({"ids": ["a"]}, {"hair_cover_type": "none"})
        for broken in (dict(good, reason=""), dict(good, date="yesterday"), dict(good, select={}),
                       dict(good, select={"ids": []}), dict(good, select={"colour": ["red"]}), dict(good, set={})):
            with self.subTest(broken=broken), self.assertRaises(tool.SpecError):
                self.load(rules=[broken])

    def test_an_id_twice_in_one_selector_is_refused(self):
        with self.assertRaises(tool.SpecError):
            self.load(rules=[rule({"ids": ["a", "a"]}, {"hair_cover_type": "none"})])

    def test_an_id_in_two_snapshot_files_is_refused(self):
        with self.assertRaises(tool.SpecError):
            self.load(files={HEAD_REL: {"h": {}}, LEG_REL: {"h": {}}})


class ShippedSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.path = Path(tool.DEFAULT_SPEC)
        cls.spec = tool.load_spec(cls.path)

    def test_the_file_is_the_canonical_dump(self):
        self.assertEqual(self.path.read_bytes().decode("utf-8").replace("\r\n", "\n"), tool.dump_spec(self.spec))

    def test_every_rule_id_appears_once(self):
        seen = {}
        for r in self.spec.rules:
            for item_id in r["select"].get("ids", []):
                self.assertNotIn(item_id, seen, f"{item_id} is in two rules: {seen.get(item_id)} and {r['reason'][:40]}")
                seen[item_id] = r["reason"][:40]

    def test_the_crowns_carry_none_and_none(self):
        crowns = ("sk_ar_art_crown_king_a", "sk_dwarf_dain_crown_a", "rivendell_helmet_lord_circlet_silver",
                  "rivendell_helmet_lord_circletmask_silver", "rivendell_helmet_lord_circletmask_silvergold",
                  "gondor_king_crown", "rivendell_helmet_lord_circlet", "rivendell_helmet_lord_circletmask",
                  "sk_ll_gl_galad_crown_a", "thranduil_helmet")
        for crown in crowns:
            with self.subTest(crown=crown):
                got = {}
                for r in self.spec.rules:
                    if crown in r["select"].get("ids", []):
                        got.update(r["set"])
                self.assertEqual({k: got.get(k) for k in ("hair_cover_type", "beard_cover_type")},
                                 {"hair_cover_type": "none", "beard_cover_type": "none"})

    def test_the_bare_shin_rule_names_the_meshes_the_leg_audit_names(self):
        meshes = set()
        for r in self.spec.rules:
            if r["select"].get("type") == "LegArmor" and r["set"].get("covers_legs") == "false":
                meshes |= set(r["select"]["mesh"])
        self.assertEqual(meshes, set(audit_leg_cover_flags.BARE_SHIN_MESHES))

    def test_the_snapshot_has_no_item_twice_and_every_value_is_valid(self):
        # load_spec enforces both; this pins that the shipped file went through it and is not empty
        self.assertGreater(sum(len(v) for v in self.spec.files.values()), 3000)


class ApplyTests(Fixture):
    def test_a_rule_changes_the_value_and_nothing_else(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        before_leg = self.raw(LEG_REL)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 0, out)
        want = HEAD.replace('head_armor="20"\r\n                hair_cover_type="all"',
                            'head_armor="20"\r\n                hair_cover_type="none"')
        self.assertEqual(self.raw(HEAD_REL), BOM + want.encode("utf-8"))
        self.assertEqual(self.raw(LEG_REL), before_leg)
        self.assertIn("h_crown", out)
        self.assertIn("hair_cover_type", out)

    def test_a_missing_attribute_is_added_in_the_same_tag_with_the_tags_own_layout(self):
        self.write_spec([rule({"ids": ["h_crown", "h_helm"]}, {"covers_head": "true"})])
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        want = (HEAD.replace('beard_cover_type="none" />', 'beard_cover_type="none"\r\n                covers_head="true" />')
                .replace('hair_cover_type="Type2" />', 'hair_cover_type="Type2" covers_head="true" />'))
        self.assertEqual(self.raw(HEAD_REL), BOM + want.encode("utf-8"))

    def test_an_lf_only_file_stays_lf_only(self):
        self.put(HEAD_REL, HEAD.replace("\r\n", "\n").encode("utf-8"))
        self.write_spec([rule({"ids": ["h_crown"]}, {"covers_head": "true"})])
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        self.assertNotIn(b"\r", self.raw(HEAD_REL))
        self.assertIn(b'beard_cover_type="none"\n                covers_head="true" />', self.raw(HEAD_REL))

    def test_the_bom_and_crlf_survive_and_the_result_parses(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "type1"})])
        self.run_tool("--apply", "--backup-tag", "t1")
        data = self.raw(HEAD_REL)
        self.assertTrue(data.startswith(BOM))
        self.assertEqual(data.count(b"\r\n"), data.count(b"\n"))
        import xml.etree.ElementTree as ET
        ET.fromstring(data.lstrip(BOM))

    def test_the_commented_out_item_and_the_attribute_in_a_value_are_left_alone(self):
        self.write_spec([rule({"ids": ["ghost", "h_crown"]}, {"hair_cover_type": "type3"})])
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        text = self.text(HEAD_REL)
        self.assertIn('<Armor hair_cover_type="all" /></ItemComponent></Item> -->', text)
        self.assertIn("hair_cover_type=&quot;none&quot;", text)
        self.assertIn("ghost", out)   # reported as missing, not edited

    def test_an_absent_attribute_equals_the_engine_default(self):
        # ArmorComponent.Deserialize: no hair_cover_type attribute means HairCoverTypes.None
        self.write_spec([rule({"ids": ["l_shoe"]}, {"covers_legs": "false"})])
        before = self.raw(LEG_REL)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 0)
        self.assertEqual(self.raw(LEG_REL), before)
        self.assertEqual(self.baks(), [])
        self.assertIn("already set", out)

    def test_a_case_difference_is_not_a_change(self):
        self.write_spec([rule({"ids": ["h_helm"]}, {"hair_cover_type": "type2"})])
        before = self.raw(HEAD_REL)
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        self.assertEqual(self.raw(HEAD_REL), before)

    def test_a_snapshot_value_is_restored_after_a_reset(self):
        self.put(HEAD_REL, BOM + HEAD.replace('beard_cover_type="none"', 'beard_cover_type="all"').encode("utf-8"))
        self.write_spec()
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        self.assertEqual(self.raw(HEAD_REL), BOM + HEAD.encode("utf-8"))

    def test_an_attribute_the_snapshot_records_as_absent_is_not_added_but_a_stray_value_goes_back_to_default(self):
        self.put(LEG_REL, LEGS.replace('<Armor leg_armor="3" />', '<Armor leg_armor="3" covers_legs="true" />').encode("utf-8"))
        self.write_spec()
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        self.assertIn('<Armor leg_armor="3" covers_legs="false" />', self.text(LEG_REL))
        self.assertNotIn("covers_head", self.text(HEAD_REL))

    def test_a_rule_beats_the_snapshot(self):
        self.write_spec([rule({"ids": ["l_boot"]}, {"covers_legs": "false"})])
        self.run_tool("--apply", "--backup-tag", "t1")
        self.assertIn('<Armor leg_armor="9" covers_legs="false" />', self.text(LEG_REL))

    def test_a_type_rule_with_mesh_exceptions(self):
        self.write_spec([rule({"type": "LegArmor", "not_mesh": ["shoe_a"]}, {"covers_legs": "true"}),
                         rule({"type": "LegArmor", "mesh": ["shoe_a"]}, {"covers_legs": "false"})])
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        text = self.text(LEG_REL)
        self.assertIn('<Armor leg_armor="9" covers_legs="true" />', text)
        self.assertNotIn('<Armor leg_armor="3" covers_legs', text)   # false is the default: absent stays absent

    def test_two_rules_that_disagree_are_refused_and_nothing_is_written(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"}),
                         rule({"type": "HeadArmor"}, {"hair_cover_type": "all"})])
        before = self.raw(HEAD_REL)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)   # 2 would read as "NOT RUN (no install)" in tools/data_gates.py
        self.assertIn("h_crown", out)
        self.assertEqual(self.raw(HEAD_REL), before)

    def test_an_item_defined_in_another_file_is_found(self):
        self.put("LOTRLOME_items/gondor/starter_armors.xml", LEGS.replace("l_boot", "starter_boot").replace("l_shoe", "s2").encode("utf-8"))
        self.write_spec([rule({"ids": ["starter_boot"]}, {"covers_legs": "false"})], files={})
        self.assertEqual(self.run_tool("--apply", "--backup-tag", "t1")[0], 0)
        self.assertIn('id="starter_boot" mesh="boot_a" Type="LegArmor"><ItemComponent><Armor leg_armor="9" covers_legs="false"',
                      self.text("LOTRLOME_items/gondor/starter_armors.xml"))

    def test_the_dry_run_writes_nothing_and_lists_the_change(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        before = (self.raw(HEAD_REL), self.raw(LEG_REL))
        code, out = self.run_tool()
        self.assertEqual(code, 0)
        self.assertEqual((self.raw(HEAD_REL), self.raw(LEG_REL)), before)
        self.assertEqual(self.baks(), [])
        self.assertRegex(out, r"CHANGE .*h_crown.*hair_cover_type.*all.*none")

    def test_the_second_apply_writes_nothing_and_takes_no_second_backup(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        self.run_tool("--apply", "--backup-tag", "t1")
        after_first = self.raw(HEAD_REL)
        baks = self.baks()
        mtime = (self.armory / "ModuleData" / HEAD_REL).stat().st_mtime_ns
        code, out = self.run_tool("--apply", "--backup-tag", "t2")
        self.assertEqual(code, 0)
        self.assertEqual(self.raw(HEAD_REL), after_first)
        self.assertEqual(self.baks(), baks)
        self.assertEqual((self.armory / "ModuleData" / HEAD_REL).stat().st_mtime_ns, mtime)

    def test_the_backup_holds_the_original_and_does_not_end_in_xml(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        before = self.raw(HEAD_REL)
        self.run_tool("--apply", "--backup-tag", "cover-1")
        self.assertEqual(self.baks(), ["head_armors.xml.bak-cover-1"])
        self.assertEqual((self.armory / "ModuleData" / "LOTRLOME_items/gondor/head_armors.xml.bak-cover-1").read_bytes(), before)
        self.assertFalse(any(name.lower().endswith(".xml") for name in self.baks()))

    def test_an_existing_backup_is_never_overwritten(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        bak = self.armory / "ModuleData" / "LOTRLOME_items/gondor/head_armors.xml.bak-keep"
        bak.write_bytes(b"pristine")
        self.run_tool("--apply", "--backup-tag", "keep")
        self.assertEqual(bak.read_bytes(), b"pristine")

    def test_apply_needs_a_backup_tag_and_the_tag_cannot_make_an_xml_name(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        before = self.raw(HEAD_REL)
        for args in (("--apply",), ("--apply", "--backup-tag", "x.xml"), ("--apply", "--backup-tag", "a/b"),
                     ("--apply", "--backup-tag", "")):
            with self.subTest(args=args), redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()), \
                    self.assertRaises(SystemExit) as ctx:
                tool.main(["--armory-path", str(self.armory), "--spec", str(self.spec), *args])
            self.assertEqual(ctx.exception.code, 2)
        self.assertEqual(self.raw(HEAD_REL), before)

    def test_a_file_that_does_not_parse_stops_the_run_before_any_write(self):
        self.put("LOTRLOME_items/gondor/broken.xml", b'<Items><Item id="b" Type="HeadArmor"><Armor hair_cover_type="all"></Items>')
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        before = self.raw(HEAD_REL)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertIn("broken.xml", out)
        self.assertEqual(self.raw(HEAD_REL), before)


class InvalidValueTests(Fixture):
    def setUp(self):
        super().setUp()
        self.put(NEW_REL, NEW_BAD.encode("utf-8"))
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])

    def test_the_dry_run_reports_the_same_invalid_values_and_writes_nothing(self):
        before = (self.raw(HEAD_REL), self.raw(NEW_REL))
        code, out = self.run_tool()
        self.assertEqual(code, 1)
        self.assertRegex(out, r"INVALID .*n_helm hair_cover_type.*tpye1")
        self.assertRegex(out, r"INVALID .*n_gloves covers_hands.*yes")
        self.assertEqual((self.raw(HEAD_REL), self.raw(NEW_REL)), before)

    def test_apply_refuses_to_write_anything_while_one_exists_and_says_so(self):
        before = (self.raw(HEAD_REL), self.raw(NEW_REL))
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"INVALID .*n_helm hair_cover_type.*tpye1")
        self.assertIn("refus", out)
        self.assertEqual((self.raw(HEAD_REL), self.raw(NEW_REL)), before)
        self.assertEqual(self.baks(), [])


class CheckTests(Fixture):
    def test_a_matching_armory_exits_zero(self):
        self.write_spec()
        code, out = self.run_tool("--check")
        self.assertEqual(code, 0, out)

    def test_drift_exits_one_and_names_the_item_the_attribute_and_both_values(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})])
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"DRIFT .*h_crown.*hair_cover_type.*all.*none")
        self.assertEqual(self.baks(), [])

    def test_a_reset_snapshot_value_is_drift(self):
        self.write_spec()
        self.put(HEAD_REL, BOM + HEAD.replace('beard_cover_type="none"', 'beard_cover_type="all"').encode("utf-8"))
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"DRIFT .*h_crown.*beard_cover_type")

    def test_a_missing_rule_id_is_reported_and_fails(self):
        self.write_spec([rule({"ids": ["h_crown", "no_such_helm"]}, {"hair_cover_type": "all"})])
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"MISSING .*no_such_helm")

    def test_a_snapshot_item_the_armory_no_longer_has_is_drift(self):
        self.write_spec(files={**SNAP_ALL, HEAD_REL: {**SNAP_ALL[HEAD_REL], "gone": {"hair_cover_type": "all"}}})
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"MISSING .*gone")

    def test_a_new_item_is_a_warning_with_a_count_and_does_not_fail(self):
        self.write_spec(files={HEAD_REL: SNAP_ALL[HEAD_REL]})   # the legs are now "new"
        code, out = self.run_tool("--check")
        self.assertEqual(code, 0, out)
        self.assertRegex(out, r"WARNING 2 armour item\(s\) are not in the snapshot")

    def test_a_new_item_must_still_meet_a_type_rule(self):
        self.write_spec([rule({"type": "LegArmor"}, {"covers_legs": "true"})], files={HEAD_REL: SNAP_ALL[HEAD_REL]})
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"DRIFT .*l_shoe.*covers_legs")

    def test_a_new_item_with_a_value_the_engine_rejects_fails_and_is_named(self):
        self.write_spec()
        self.put(NEW_REL, NEW_BAD.encode("utf-8"))
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"INVALID .*new_armors\.xml n_helm hair_cover_type.*tpye1")

    def test_a_bad_boolean_on_a_new_item_is_invalid_too(self):
        self.write_spec()
        self.put(NEW_REL, NEW_BAD.encode("utf-8"))
        code, out = self.run_tool("--check")
        self.assertRegex(out, r"INVALID .*n_gloves covers_hands.*yes")

    def test_a_valid_new_item_stays_a_warning(self):
        self.write_spec()
        self.put(NEW_REL, NEW_BAD.replace("tpye1", "Type1").replace('"yes"', '"true"').encode("utf-8"))
        code, out = self.run_tool("--check")
        self.assertEqual(code, 0, out)
        self.assertRegex(out, r"WARNING 2 armour item\(s\) are not in the snapshot")
        self.assertNotIn("INVALID", out)

    def test_an_invalid_value_on_a_snapshot_item_is_named_as_invalid_as_well_as_drift(self):
        self.write_spec()
        self.put(HEAD_REL, BOM + HEAD.replace('hair_cover_type="all"', 'hair_cover_type="tpye1"').encode("utf-8"))
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertRegex(out, r"INVALID .*h_crown hair_cover_type.*tpye1")

    def test_a_garbled_body_mesh_type_reads_as_the_default_and_is_not_invalid(self):
        self.write_spec()
        self.put(NEW_REL, NEW_BAD.replace("tpye1", "none").replace('"yes"', '"true"')
                 .replace('<Armor', '<Armor body_mesh_type="Upperbodyy"', 1).encode("utf-8"))
        self.assertNotIn("INVALID", self.run_tool("--check")[1])

    def test_no_install_is_exit_two_with_a_skipped_message(self):
        self.write_spec()
        code, out = self.run_tool("--check", armory=Path(self.tmp.name) / "nowhere")
        self.assertEqual(code, 2)
        self.assertIn("SKIPPED", out)

    def test_an_armory_with_no_armour_is_exit_two_not_a_pass(self):
        empty = Path(self.tmp.name) / "Empty"
        (empty / "ModuleData").mkdir(parents=True)
        self.write_spec()
        code, out = self.run_tool("--check", armory=empty)
        self.assertEqual(code, 2)
        self.assertIn("SKIPPED", out)

    def test_a_bad_spec_is_exit_one_not_a_skipped_run(self):
        self.spec.write_text("{not json", encoding="utf-8")
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("ERROR", out)


class SnapshotTests(Fixture):
    def test_the_snapshot_writes_the_spec_and_never_the_armory(self):
        self.write_spec([rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"})], files={})
        before = (self.raw(HEAD_REL), self.raw(LEG_REL))
        code, out = self.run_tool("--snapshot", "--expect", "4")   # four items the empty snapshot lacks
        self.assertEqual(code, 0, out)
        self.assertEqual((self.raw(HEAD_REL), self.raw(LEG_REL)), before)
        self.assertEqual(self.baks(), [])
        spec = tool.load_spec(self.spec)
        self.assertEqual(spec.files, {
            HEAD_REL: {"h_crown": {"hair_cover_type": "all", "beard_cover_type": "none"},
                       "h_helm": {"hair_cover_type": "type2"}},   # "Type2" is held lower case
            LEG_REL: {"l_boot": {"covers_legs": "true"}, "l_shoe": {}},   # an absent attribute is not recorded
        })

    def test_the_snapshot_keeps_the_rules_and_is_sorted_one_item_per_line(self):
        rules = [rule({"ids": ["h_crown"]}, {"hair_cover_type": "none"}, reason="why")]
        self.write_spec(rules, files={})
        self.run_tool("--snapshot", "--expect", "4")
        text = self.spec.read_text(encoding="utf-8")
        self.assertEqual(tool.load_spec(self.spec).rules, rules)
        self.assertLess(text.index(HEAD_REL), text.index(LEG_REL))
        self.assertLess(text.index('"h_crown"'), text.index('"h_helm"'))
        self.assertEqual(sum(1 for line in text.splitlines() if line.strip().startswith('"h_helm"')), 1)

    def test_a_second_snapshot_is_byte_identical(self):
        self.write_spec(files={})
        self.run_tool("--snapshot", "--expect", "4")
        first = self.spec.read_bytes()
        self.run_tool("--snapshot")
        self.assertEqual(self.spec.read_bytes(), first)

    def test_the_dry_run_after_a_snapshot_has_nothing_to_change(self):
        self.write_spec(files={})
        self.run_tool("--snapshot", "--expect", "4")
        code, out = self.run_tool()
        self.assertEqual(code, 0)
        self.assertNotIn("CHANGE", out)
        self.assertEqual(self.run_tool("--check")[0], 0)

    def test_a_snapshot_refuses_a_value_the_engine_would_reject(self):
        self.put(HEAD_REL, BOM + HEAD.replace('hair_cover_type="all"', 'hair_cover_type="tpye1"').encode("utf-8"))
        self.write_spec(files={})
        before = self.spec.read_bytes()
        code, out = self.run_tool("--snapshot")
        self.assertEqual(code, 1)
        self.assertIn("h_crown", out)
        self.assertEqual(self.spec.read_bytes(), before)

    def reset_beard(self):
        self.put(HEAD_REL, BOM + HEAD.replace('beard_cover_type="none"', 'beard_cover_type="all"').encode("utf-8"))

    def test_a_reset_value_is_listed_and_the_write_is_refused_without_expect(self):
        self.write_spec()
        self.reset_beard()
        before = self.spec.read_bytes()
        code, out = self.run_tool("--snapshot")
        self.assertEqual(code, 1)
        self.assertIn(f"SNAPSHOT-CHANGE {HEAD_REL} h_crown beard_cover_type none -> all", out)
        self.assertIn("1 difference", out)
        self.assertEqual(self.spec.read_bytes(), before)

    def test_the_right_expect_writes_the_reset_value(self):
        self.write_spec()
        self.reset_beard()
        code, out = self.run_tool("--snapshot", "--expect", "1")
        self.assertEqual(code, 0, out)
        self.assertEqual(tool.load_spec(self.spec).files[HEAD_REL]["h_crown"]["beard_cover_type"], "all")

    def test_a_wrong_expect_is_refused(self):
        self.write_spec()
        self.reset_beard()
        before = self.spec.read_bytes()
        for wrong in ("0", "2"):
            with self.subTest(expect=wrong):
                code, out = self.run_tool("--snapshot", "--expect", wrong)
                self.assertEqual(code, 1)
                self.assertEqual(self.spec.read_bytes(), before)

    def test_an_expect_with_no_difference_is_refused(self):
        self.write_spec()
        before = self.spec.read_bytes()
        self.assertEqual(self.run_tool("--snapshot", "--expect", "3")[0], 1)
        self.assertEqual(self.spec.read_bytes(), before)

    def test_added_and_removed_items_are_listed_and_counted(self):
        self.write_spec(files={HEAD_REL: {**SNAP_ALL[HEAD_REL], "gone": {"hair_cover_type": "all"}}, LEG_REL: {"l_boot": {"covers_legs": "true"}}})
        code, out = self.run_tool("--snapshot")
        self.assertEqual(code, 1)
        self.assertIn(f"SNAPSHOT-REMOVED {HEAD_REL} gone", out)
        self.assertIn(f"SNAPSHOT-ADDED {LEG_REL} l_shoe", out)
        self.assertIn("2 difference", out)

    def test_an_unchanged_armory_needs_no_expect(self):
        self.write_spec()
        code, out = self.run_tool("--snapshot")
        self.assertEqual(code, 0, out)
        self.assertNotIn("SNAPSHOT-CHANGE", out)

    def test_the_snapshot_without_an_install_is_exit_two_and_leaves_the_spec(self):
        self.write_spec()
        before = self.spec.read_bytes()
        code, out = self.run_tool("--snapshot", armory=Path(self.tmp.name) / "nowhere")
        self.assertEqual(code, 2)
        self.assertEqual(self.spec.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
