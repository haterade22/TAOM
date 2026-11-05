"""complete_lords_xslt.py --apply adds the vanilla lords lords.xslt lacks and never rewrites a byte already in the
file (#959). Everything else in lords.xslt (the #758 and #957 blocks, Equipments, race, hand layout, comments) is the
file's own."""
import contextlib
import difflib
import io
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

try:
    from lxml import etree as LXML
except ImportError:
    LXML = None

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import complete_lords_xslt  # noqa: E402

SHIPPED = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
XSL = "{http://www.w3.org/1999/XSL/Transform}"
RULE = "    <!-- ============================================== -->"
BOM = b"\xef\xbb\xbf"

SYNTHETIC_XSLT = """<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- hand block, not a lord rule -->
    <xsl:template match="Foo">
        <xsl:copy/>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 1 - Empire/Dunland -->
    <!-- ============================================== -->

    <!-- lord_1_15 -->
    <xsl:template match="NPCCharacter[@id='lord_1_15']">
        <xsl:copy>
            <xsl:attribute name="id">lord_1_15</xsl:attribute>
            <xsl:attribute name="skill_template">SkillSet.hand_ordered</xsl:attribute>
            <xsl:attribute name="name">{=aom_lord_1_15_name}Witch-King</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <xsl:attribute name="culture">Culture.mordor</xsl:attribute>
            <xsl:attribute name="race">nazghul</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00"/>
            </face>
          <skills>
              <skill id="OneHanded" value="315" />
          </skills>
          <Traits>
              <Trait id="Honor" value="-2" />
          </Traits>
            <Equipments>
            \t<EquipmentSet id="witchking_bat_equipment" />
            \t<EquipmentSet id="witchking_civ_equipment" equipmentType="Civilian" />
            </Equipments>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits or self::Equipments)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_16 -->
    <xsl:template match="NPCCharacter[@id='lord_1_16']">
        <xsl:copy>
            <xsl:attribute name="id">lord_1_16</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00"/>
            </face>
            <skills/>
            <Traits>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 4 - Vlandia/Rohan -->
    <!-- ============================================== -->

    <!-- lord_4_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_1']">
        <xsl:copy>
            <xsl:attribute name="id">lord_4_1</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00"/>
            </face>
            <skills/>
            <Traits>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
"""

BASE_LORDS = {
    "lord_1_15": '<NPCCharacter id="lord_1_15" is_female="true" culture="Culture.vanilla"/>',
    "lord_1_16": '<NPCCharacter id="lord_1_16"/>',
    "lord_4_1": '<NPCCharacter id="lord_4_1"/>',
}


def vanilla_xml(**extra):
    rows = dict(BASE_LORDS)
    rows.update(extra)
    return "<NPCCharacters>\n" + "\n".join(f"    {row}" for row in rows.values()) + "\n</NPCCharacters>\n"


def encoded(text, eol):
    return text.replace("\n", eol).encode("utf-8")


def lines_only_inserted(before, after):
    """True when every line of before survives in after, in order: after only adds lines."""
    matcher = difflib.SequenceMatcher(None, before.split("\n"), after.split("\n"), autojunk=False)
    return all(tag in ("equal", "insert") for tag, *_ in matcher.get_opcodes())


def template_ids(text):
    return complete_lords_xslt.template_ids(text)


class ApplyToTests(unittest.TestCase):
    """--apply is an insert-only pipeline: it parses its result before writing and writes only when bytes change."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.vanilla = Path(self._tmp.name) / "vanilla_lords.xml"
        self.write_vanilla()
        self.xslt = Path(self._tmp.name) / "lords.xslt"

    def write_vanilla(self, **extra):
        self.vanilla.write_text(vanilla_xml(**extra), encoding="utf-8")

    def write_fixture(self, text, eol="\n", bom=b""):
        self.xslt.write_bytes(bom + encoded(text, eol))
        return self.xslt.read_bytes()

    def apply(self):
        return complete_lords_xslt.apply_to(str(self.xslt), str(self.vanilla))

    def test_a_file_missing_no_lord_is_left_byte_identical(self):
        for eol, bom in (("\n", b""), ("\r\n", BOM)):
            before = self.write_fixture(SYNTHETIC_XSLT, eol, bom)
            self.apply()
            self.assertEqual(before, self.xslt.read_bytes(), repr(eol))

    def test_a_missing_lord_is_added_and_nothing_else_changes(self):
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17" name="{=v17}Vanilla Name" culture="Culture.vanilla"/>')
        self.write_fixture(SYNTHETIC_XSLT)

        self.apply()

        after = self.xslt.read_text(encoding="utf-8")
        self.assertTrue(lines_only_inserted(SYNTHETIC_XSLT, after))
        self.assertIn("<!-- lord_1_17 — NEW: needs TAOM customization -->", after)
        self.assertIn('<xsl:attribute name="name">{=aom_lord_1_17_name}Vanilla Name</xsl:attribute>', after)
        self.assertLess(after.index("NPCCharacter[@id='lord_1_16']"), after.index("NPCCharacter[@id='lord_1_17']"))
        self.assertLess(after.index("NPCCharacter[@id='lord_1_17']"), after.index("Faction 4 - Vlandia/Rohan"))

    def test_an_lf_file_stays_lf_and_a_crlf_file_stays_crlf_with_its_bom(self):
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
        for eol, bom in (("\n", b""), ("\r\n", BOM)):
            self.write_fixture(SYNTHETIC_XSLT, eol, bom)
            self.apply()
            data = self.xslt.read_bytes()
            self.assertIn(b"lord_1_17", data)
            self.assertEqual(data.startswith(BOM), bool(bom), repr(eol))
            if eol == "\n":
                self.assertNotIn(b"\r\n", data)
            else:
                self.assertEqual(data.count(b"\n"), data.count(b"\r\n"))

    def test_a_malformed_result_raises_and_leaves_the_file_unchanged(self):
        broken = SYNTHETIC_XSLT.replace('<skill id="OneHanded" value="315" />', '<skill id="OneHanded" value="315">')
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
        before = self.write_fixture(broken)

        with self.assertRaises(ET.ParseError):
            self.apply()

        self.assertEqual(before, self.xslt.read_bytes())

    def test_escaped_values_are_written_as_they_are(self):
        text = SYNTHETIC_XSLT.replace("{=aom_lord_1_15_name}Witch-King", "{=aom_lord_1_15_name}Fish &amp; Chips &gt; All")
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17" name="{=v}Tom &amp; Jerry"/>')
        self.write_fixture(text)

        self.apply()
        first = self.xslt.read_bytes()
        self.apply()

        self.assertEqual(first, self.xslt.read_bytes(), "a second --apply adds nothing")
        self.assertIn(b"Fish &amp; Chips &gt; All", first)
        self.assertIn(b"Tom &amp; Jerry", first)
        self.assertNotIn(b"&amp;amp;", first)

    def test_every_shape_of_hand_data_survives_apply(self):
        marker = "        <xsl:copy>\n            <xsl:attribute name=\"id\">lord_1_15</xsl:attribute>"
        self.assertIn(marker, SYNTHETIC_XSLT)
        shapes = {
            "comment inside a template": SYNTHETIC_XSLT.replace(
                marker, "        <xsl:copy>\n            <!-- hand note -->\n" + marker.split("\n", 1)[1]),
            "extra child element": SYNTHETIC_XSLT.replace(
                "            <face>\n                <BodyProperties version=\"4\" key=\"00\"/>",
                "            <description>x</description>\n            <face>\n                <BodyProperties version=\"4\" key=\"00\"/>", 1),
            "xsl:attribute with xsl:value-of": SYNTHETIC_XSLT.replace(
                marker, marker + "\n            <xsl:attribute name=\"extra\"><xsl:value-of select=\"@id\"/></xsl:attribute>"),
            "Equipments not at a line start": SYNTHETIC_XSLT.replace(
                "</Traits>\n            <Equipments>", "</Traits> <Equipments>"),
            "extra top-level template": SYNTHETIC_XSLT.replace(
                "</xsl:stylesheet>",
                "    <xsl:template match=\"Bar\">\n        <xsl:copy/>\n    </xsl:template>\n</xsl:stylesheet>"),
            "orphan template id": SYNTHETIC_XSLT.replace(
                "</xsl:stylesheet>",
                "    <xsl:template match=\"NPCCharacter[@id='lord_9_9']\">\n        <xsl:copy>\n"
                "            <xsl:attribute name=\"id\">lord_9_9</xsl:attribute>\n        </xsl:copy>\n    </xsl:template>\n"
                "</xsl:stylesheet>"),
            "template with priority": SYNTHETIC_XSLT.replace(
                "match=\"NPCCharacter[@id='lord_1_16']\">", "match=\"NPCCharacter[@id='lord_1_16']\" priority=\"2\">"),
            "apostrophe entity in a name": SYNTHETIC_XSLT.replace("Witch-King", "Witch&apos;s King"),
        }
        block_start = SYNTHETIC_XSLT.index("    <!-- lord_1_16 -->")
        block_end = SYNTHETIC_XSLT.index("\n\n\n" + RULE, block_start)
        block = SYNTHETIC_XSLT[block_start:block_end]
        without = SYNTHETIC_XSLT[:block_start - 2] + SYNTHETIC_XSLT[block_end:]
        shapes["template moved to another section"] = without.replace(
            "\n\n</xsl:stylesheet>", "\n\n" + block + "\n\n</xsl:stylesheet>")
        for name, text in shapes.items():
            with self.subTest(name):
                self.assertNotEqual(SYNTHETIC_XSLT, text, name)
                before = self.write_fixture(text)
                self.assertEqual([], self.apply())
                self.assertEqual(before, self.xslt.read_bytes(), "no lord is missing, so no byte moves")

                self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
                self.assertEqual(["lord_1_17"], self.apply(), "exactly the missing lord is added")
                after = self.xslt.read_text(encoding="utf-8")
                self.assertTrue(lines_only_inserted(text, after), "only the missing lord's template is inserted")
                self.write_vanilla()

    def test_a_missing_whole_section_is_created_in_its_place(self):
        self.write_vanilla(lord_2_1='<NPCCharacter id="lord_2_1"/>', lord_5_1='<NPCCharacter id="lord_5_1"/>',
                           lord_5_2='<NPCCharacter id="lord_5_2"/>')
        self.write_fixture(SYNTHETIC_XSLT)

        self.apply()

        after = self.xslt.read_text(encoding="utf-8")
        self.assertTrue(lines_only_inserted(SYNTHETIC_XSLT, after))
        order = [after.index(h) for h in ("Faction 1 - ", "Faction 2 - ", "Faction 4 - ", "Faction 5 - ")]
        self.assertEqual(sorted(order), order, "a new section sits before the first later section, else at the end")
        self.assertEqual(["lord_2_1"], [i for i in template_ids(after[order[1]:order[2]])])
        self.assertEqual(["lord_5_1", "lord_5_2"], template_ids(after[order[3]:]))
        self.assertTrue(after.endswith("\n\n</xsl:stylesheet>\n"))

    def test_an_unassigned_id_lands_in_the_unassigned_section(self):
        self.write_vanilla(zz_odd='<NPCCharacter id="zz_odd"/>')
        self.write_fixture(SYNTHETIC_XSLT)

        self.apply()

        after = self.xslt.read_text(encoding="utf-8")
        header = after.index("<!-- Unassigned Lords -->")
        self.assertGreater(header, after.index("NPCCharacter[@id='lord_4_1']"))
        self.assertEqual(["zz_odd"], template_ids(after[header:]))

    def dry_run_report(self):
        merged = complete_lords_xslt.load_merged(str(self.vanilla), str(self.xslt))
        report = io.StringIO()
        with contextlib.redirect_stdout(report):
            complete_lords_xslt.dry_run(merged)
        return report.getvalue()

    def test_a_self_closing_template_counts_as_present_for_dry_run_and_apply(self):
        text = SYNTHETIC_XSLT.replace("    <!-- lord_4_1 -->",
                                      "    <xsl:template match=\"NPCCharacter[@id='lord_1_17']\"/>\n\n    <!-- lord_4_1 -->")
        self.assertNotEqual(SYNTHETIC_XSLT, text)
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
        before = self.write_fixture(text)

        self.assertIn("--apply would add 0 template(s)", self.dry_run_report())
        self.assertEqual([], self.apply())
        self.assertEqual(before, self.xslt.read_bytes())

    def test_a_template_with_a_non_canonical_match_counts_as_present(self):
        for match in ("NPCCharacter[@id = 'lord_1_17']","NPCCharacter[ @id='lord_1_17' ]"):
            for quote_style in ("double", "single"):
                with self.subTest(match=match, quotes=quote_style):
                    expr = match if quote_style == "double" else match.replace("'", '"')
                    outer = '"' if quote_style == "double" else "'"
                    extra = (f"    <xsl:template match={outer}{expr}{outer}>\n        <xsl:copy/>\n    </xsl:template>\n\n")
                    text = SYNTHETIC_XSLT.replace("    <!-- lord_4_1 -->", extra + "    <!-- lord_4_1 -->")
                    self.assertNotEqual(SYNTHETIC_XSLT, text)
                    self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
                    before = self.write_fixture(text)

                    self.assertIn("--apply would add 0 template(s)", self.dry_run_report())
                    self.assertEqual([], self.apply(), "no second template for an id that has one")
                    self.assertEqual(before, self.xslt.read_bytes())

    def test_a_template_for_another_element_or_id_does_not_count_as_present(self):
        for match in ("Foo[@id='lord_1_17']", "NPCCharacter[@id='lord_1_99']", "NPCCharacter[@name='lord_1_17']"):
            with self.subTest(match=match):
                extra = f"    <xsl:template match=\"{match}\">\n        <xsl:copy/>\n    </xsl:template>\n\n"
                text = SYNTHETIC_XSLT.replace("    <!-- lord_4_1 -->", extra + "    <!-- lord_4_1 -->")
                self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>')
                self.write_fixture(text)

                self.assertEqual(["lord_1_17"], self.apply())

    def test_insertion_survives_no_blank_line_before_the_stylesheet_close(self):
        text = SYNTHETIC_XSLT.replace("\n\n</xsl:stylesheet>", "\n</xsl:stylesheet>")
        self.assertNotEqual(SYNTHETIC_XSLT, text)
        self.write_vanilla(lord_4_2='<NPCCharacter id="lord_4_2"/>', lord_5_1='<NPCCharacter id="lord_5_1"/>')
        self.write_fixture(text)

        self.assertEqual(["lord_4_2", "lord_5_1"], self.apply())

        after = self.xslt.read_text(encoding="utf-8")
        ET.fromstring(after.encode("utf-8"))
        self.assertEqual(1, after.count("Faction 4 - Vlandia/Rohan"))
        self.assertEqual(1, after.count("Faction 5 - Battania/Woodland Realm"))
        self.assertEqual(["lord_4_1", "lord_4_2"], template_ids(after[after.index("Faction 4 - "):after.index("Faction 5 - ")]))
        self.assertEqual(["lord_5_1"], template_ids(after[after.index("Faction 5 - "):]))
        self.assertTrue(after.endswith("</xsl:template>\n</xsl:stylesheet>\n"), "the file's own closing spacing is kept")

    def test_insertion_survives_a_banner_with_one_blank_line_before_it(self):
        text = SYNTHETIC_XSLT.replace("\n\n\n" + RULE + "\n    <!-- Faction 4", "\n\n" + RULE + "\n    <!-- Faction 4")
        self.assertNotEqual(SYNTHETIC_XSLT, text)
        self.write_vanilla(lord_2_1='<NPCCharacter id="lord_2_1"/>', lord_4_2='<NPCCharacter id="lord_4_2"/>')
        self.write_fixture(text)

        self.assertEqual(["lord_2_1", "lord_4_2"], self.apply())

        after = self.xslt.read_text(encoding="utf-8")
        ET.fromstring(after.encode("utf-8"))
        self.assertEqual(1, after.count("Faction 4 - Vlandia/Rohan"), "no duplicate banner")
        f1, f2, f4 = (after.index(f"Faction {n} - ") for n in (1, 2, 4))
        self.assertTrue(f1 < f2 < f4)
        self.assertEqual(["lord_2_1"], template_ids(after[f2:f4]))
        self.assertEqual(["lord_4_1", "lord_4_2"], template_ids(after[f4:]))

    def test_a_vanilla_file_with_no_lords_raises_naming_its_path_and_writes_nothing(self):
        self.vanilla.write_text("<NPCCharacters/>", encoding="utf-8")
        before = self.write_fixture(SYNTHETIC_XSLT)

        with self.assertRaises(ValueError) as caught:
            self.apply()

        self.assertIn(str(self.vanilla), str(caught.exception))
        self.assertEqual(before, self.xslt.read_bytes())

    def test_dry_run_reports_how_many_templates_apply_would_add(self):
        self.write_vanilla(lord_1_17='<NPCCharacter id="lord_1_17"/>', lord_1_18='<NPCCharacter id="lord_1_18"/>')
        self.write_fixture(SYNTHETIC_XSLT)
        merged = complete_lords_xslt.load_merged(str(self.vanilla), str(self.xslt))

        report = io.StringIO()
        with contextlib.redirect_stdout(report):
            complete_lords_xslt.dry_run(merged)

        self.assertIn("--apply would add 2 template(s)", report.getvalue())
        self.assertIn("never rewritten", report.getvalue())

    def test_dry_run_on_a_complete_file_adds_nothing_and_lists_attributes_apply_cannot_carry(self):
        self.write_vanilla(lord_1_15='<NPCCharacter id="lord_1_15" is_template="true" occupation="Lord"/>')
        self.write_fixture(SYNTHETIC_XSLT)
        merged = complete_lords_xslt.load_merged(str(self.vanilla), str(self.xslt))

        report = io.StringIO()
        with contextlib.redirect_stdout(report):
            complete_lords_xslt.dry_run(merged)

        self.assertIn("--apply would add 0 template(s)", report.getvalue())
        self.assertIn("is_template", report.getvalue(), "an attribute outside ATTR_ORDER never reaches a template")


class NewLordTemplateTests(unittest.TestCase):
    """A template for a lord the file lacks: placeholder name, vanilla's face, and skill rows only where nothing else
    supplies them (the #626 gate wants rows beside a skill_template to equal the SkillSet)."""

    def parse(self, xml):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "vanilla_lords.xml"
            path.write_text(xml, encoding="utf-8")
            return complete_lords_xslt.parse_vanilla_lords(str(path))

    def template_for(self, vanilla, lord_id):
        merged = complete_lords_xslt.merge_lords(vanilla, {})
        return complete_lords_xslt.generate_lord_template(lord_id, merged[lord_id])

    def test_an_untemplated_lord_gets_vanillas_skill_rows(self):
        vanilla = self.parse(
            '<NPCCharacters><NPCCharacter id="lord_9_1"><skills><skill id="OneHanded" value="100"/>'
            '<skill id="Riding" value="50"/></skills></NPCCharacter></NPCCharacters>')

        out = self.template_for(vanilla, "lord_9_1")

        self.assertIn('                <skill id="OneHanded" value="100"/>', out)
        self.assertIn('                <skill id="Riding" value="50"/>', out)
        self.assertNotIn("<skills/>", out)

    def test_a_templated_lord_gets_empty_skills(self):
        vanilla = self.parse(
            '<NPCCharacters><NPCCharacter id="lord_9_2" skill_template="SkillSet.x"><skills>'
            '<skill id="OneHanded" value="100"/></skills></NPCCharacter></NPCCharacters>')

        out = self.template_for(vanilla, "lord_9_2")

        self.assertIn("            <skills/>", out)
        self.assertNotIn('<skill id=', out)

    def test_a_new_lords_template_carries_its_id_template_traits_and_passthrough(self):
        vanilla = self.parse(
            '<NPCCharacters><NPCCharacter id="lord_9_3" name="{=v}Old" is_female="false" skill_template="SkillSet.x">'
            '<Traits><Trait id="Honor" value="2"/></Traits></NPCCharacter></NPCCharacters>')

        out = self.template_for(vanilla, "lord_9_3")

        self.assertIn('            <xsl:attribute name="id">lord_9_3</xsl:attribute>', out)
        self.assertIn('            <xsl:attribute name="skill_template">SkillSet.x</xsl:attribute>', out)
        self.assertNotIn('name="is_female"', out)
        self.assertIn('                <Trait id="Honor" value="2"/>', out)
        self.assertIn('            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>', out)
        self.assertNotIn("self::Equipments", out, "Equipments passes through from vanilla")

    @unittest.skipIf(LXML is None, "needs lxml")
    def test_a_completed_stylesheet_gives_the_new_lord_vanillas_content_under_an_xslt_engine(self):
        vanilla_text = (
            '<NPCCharacters><NPCCharacter id="lord_9_3" name="{=v}Old" age="40" is_female="false" culture="Culture.c" '
            'skill_template="SkillSet.x" extra="1"><face><BodyProperties version="4" age="30" weight="0.5" build="0.5" '
            'key="AB"/></face><skills><skill id="OneHanded" value="100"/></skills>'
            '<Traits><Trait id="Honor" value="2"/><Trait id="Mercy" value="-1"/></Traits>'
            '<Equipments><EquipmentRoster><equipment slot="Item0" id="Item.sword"/></EquipmentRoster></Equipments>'
            "</NPCCharacter></NPCCharacters>")
        sheet = ('<?xml version="1.0" encoding="utf-8"?>\n'
                 '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
                 '    <xsl:template match="@*|node()">\n        <xsl:copy>\n            <xsl:apply-templates select="@*|node()"/>\n'
                 '        </xsl:copy>\n    </xsl:template>\n\n</xsl:stylesheet>\n')
        vanilla = self.parse(vanilla_text)

        completed = complete_lords_xslt.complete_text(sheet, vanilla)

        source = LXML.fromstring(vanilla_text.encode("utf-8"))
        result = LXML.XSLT(LXML.fromstring(completed.encode("utf-8")))(LXML.ElementTree(source)).getroot()
        lord = result.find("NPCCharacter")
        canon = lambda node: LXML.tostring(node, method="c14n")
        self.assertEqual({"id": "lord_9_3", "name": "{=aom_lord_9_3_name}Old", "age": "40", "culture": "Culture.c",
                          "skill_template": "SkillSet.x"}, dict(lord.attrib))
        self.assertEqual([], lord.findall("skills/skill"), "rows beside a skill_template would drift from the SkillSet")
        self.assertEqual([("Honor", "2"), ("Mercy", "-1")], [(t.get("id"), t.get("value")) for t in lord.findall("Traits/Trait")])
        self.assertEqual(canon(source[0].find("face")), canon(lord.find("face")))
        self.assertEqual(canon(source[0].find("Equipments")), canon(lord.find("Equipments")))

    def test_a_dead_lord_gets_the_dead_marker_and_vanillas_face(self):
        vanilla = self.parse(
            '<NPCCharacters><NPCCharacter id="dead_lord_2_1" name="{=t}Old Man"><face>'
            '<BodyProperties version="4" age="30" weight="0.5" build="0.5" key="AB"/></face>'
            "</NPCCharacter></NPCCharacters>")

        out = self.template_for(vanilla, "dead_lord_2_1")

        self.assertIn("<!-- dead_lord_2_1 — DEAD LORD: needs TAOM customization -->", out)
        self.assertIn('                <BodyProperties version="4" age="30" weight="0.5" build="0.5" key="AB"/>', out)

    def test_encode_like_keeps_a_bom_and_crlf(self):
        old = BOM + b"<a>\r\n</a>\r\n"

        self.assertEqual(BOM + b"<b>\r\n</b>\r\n", complete_lords_xslt.encode_like("<b>\n</b>\n", old))

    def test_encode_like_keeps_a_file_without_bom_and_lf(self):
        old = b"<a>\n</a>\n"

        self.assertEqual(b"<b>\n</b>\n", complete_lords_xslt.encode_like("<b>\n</b>\n", old))

    def test_encode_like_follows_the_dominant_line_ending(self):
        old = b"<a>\r\n<b/>\r\n<c/>\n"

        self.assertEqual(b"<x>\r\n</x>\r\n", complete_lords_xslt.encode_like("<x>\n</x>\n", old))


@unittest.skipUnless(Path(complete_lords_xslt.VANILLA_LORDS).exists(), "needs the installed SandBox lords.xml (desktop only)")
class ShippedFileTests(unittest.TestCase):
    """The shipped file against the real vanilla lords.xml (#959)."""

    @classmethod
    def setUpClass(cls):
        cls.shipped_bytes = SHIPPED.read_bytes()
        cls.shipped = cls.shipped_bytes.decode("utf-8-sig").replace("\r\n", "\n")
        cls.vanilla = complete_lords_xslt.parse_vanilla_lords(complete_lords_xslt.VANILLA_LORDS)

    def complete(self, text):
        return complete_lords_xslt.complete_text(text, self.vanilla)

    def strip_template(self, text, lord_id):
        stripped = re.sub(rf"[ \t]*<!-- {lord_id} -->\n[^\n]*match=\"NPCCharacter\[@id='{lord_id}'\]\".*?</xsl:template>\n\n?",
                          "", text, count=1, flags=re.DOTALL)
        self.assertNotEqual(text, stripped, lord_id)
        return stripped

    def strip_section(self, text, header):
        start = text.index(f"\n\n\n{RULE}\n    <!-- {header} -->\n{RULE}")
        nxt = text.find(f"\n\n\n{RULE}", start + 5)
        end = nxt if nxt >= 0 else text.rindex("\n\n</xsl:stylesheet>")
        return text[:start] + text[end:]

    def assert_completed(self, damaged, removed_ids):
        after = self.complete(damaged)
        ET.fromstring(after.encode("utf-8"))
        self.assertTrue(lines_only_inserted(damaged, after))
        self.assertEqual(sorted(template_ids(self.shipped)),
                         sorted(template_ids(after)))
        for lord_id in removed_ids:
            self.assertIn(f"<!-- {lord_id} — NEW: needs TAOM customization -->", after)
        return after

    def test_the_shipped_file_needs_no_lord(self):
        self.assertEqual(self.shipped, self.complete(self.shipped))

    def test_apply_to_a_copy_of_the_shipped_file_writes_the_shipped_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "lords.xslt"
            copy.write_bytes(self.shipped_bytes)

            complete_lords_xslt.apply_to(str(copy))

            self.assertEqual(self.shipped_bytes, copy.read_bytes())

    def test_a_copy_missing_one_lord_template_gets_it_back_in_its_section(self):
        damaged = self.strip_template(self.shipped, "lord_1_1")
        after = self.assert_completed(damaged, ["lord_1_1"])
        self.assertLess(after.index("NPCCharacter[@id='lord_1_1']"), after.index("Faction 2 - "))
        self.assertGreater(after.index("NPCCharacter[@id='lord_1_1']"), after.index("Faction 1 - "))

    def test_a_copy_missing_two_lords_gets_both_back(self):
        damaged = self.strip_template(self.strip_template(self.shipped, "lord_1_1"), "lord_4_6")
        self.assert_completed(damaged, ["lord_1_1", "lord_4_6"])

    def test_a_copy_missing_all_of_faction_5_gets_the_section_back_before_faction_6(self):
        damaged = self.strip_section(self.shipped, "Faction 5 - Battania/Woodland Realm")
        lords = [i for i in template_ids(self.shipped) if i.startswith("lord_5_")]
        self.assertTrue(lords)
        after = self.assert_completed(damaged, lords)
        self.assertLess(after.index("Faction 4 - "), after.index("Faction 5 - "))
        self.assertLess(after.index("Faction 5 - "), after.index("Faction 6 - "))

    def test_a_copy_missing_all_of_faction_1_gets_the_section_back_first(self):
        damaged = self.strip_section(self.shipped, "Faction 1 - Empire/Dunland")
        lords = [i for i in template_ids(self.shipped) if i.startswith("lord_1_")]
        after = self.assert_completed(damaged, lords)
        self.assertLess(after.index("Faction 1 - "), after.index("Faction 2 - "))

    def test_apply_to_a_crlf_bom_copy_missing_two_lords_only_inserts_lines(self):
        damaged = self.strip_template(self.strip_template(self.shipped, "lord_1_1"), "lord_4_6")
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "lords.xslt"
            before = BOM + damaged.replace("\n", "\r\n").encode("utf-8")
            copy.write_bytes(before)

            added = complete_lords_xslt.apply_to(str(copy))
            after = copy.read_bytes()
            second = complete_lords_xslt.apply_to(str(copy))

            self.assertEqual(["lord_1_1", "lord_4_6"], added)
            self.assertTrue(after.startswith(BOM))
            self.assertEqual(after.count(b"\n"), after.count(b"\r\n"), "no LF-only line ending")
            matcher = difflib.SequenceMatcher(
                None, before.decode("utf-8-sig").split("\r\n"), after.decode("utf-8-sig").split("\r\n"), autojunk=False)
            self.assertEqual({"equal", "insert"}, {tag for tag, *_ in matcher.get_opcodes()})
            self.assertEqual([], second)
            self.assertEqual(after, copy.read_bytes(), "a second apply leaves the bytes stable")

    def test_an_unassigned_vanilla_id_joins_the_end_of_the_unassigned_section(self):
        vanilla = dict(self.vanilla)
        vanilla["zz_unassigned"] = {"attrs": {"id": "zz_unassigned"}, "face": None, "skills": {}, "traits": []}

        after = complete_lords_xslt.complete_text(self.shipped, vanilla)

        self.assertTrue(lines_only_inserted(self.shipped, after))
        self.assertEqual("zz_unassigned", template_ids(after[after.index("<!-- Unassigned Lords -->"):])[-1])
        self.assertEqual(sorted(template_ids(self.shipped) + ["zz_unassigned"]), sorted(template_ids(after)))


if __name__ == "__main__":
    unittest.main()
