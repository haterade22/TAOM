#!/usr/bin/env python3
"""Tests for the string extraction in tools/insert_new_factions.py (#1590).

Run:  python -m unittest tools.tests.test_insert_new_factions_strings
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import insert_new_factions as inf  # noqa: E402

SRC = """<strings>
	<string id="str_faction_name.gundabad" text="{=a}Gundabad" />
	<string id="str_culture_name.gundabad" text="{=b}Gundabad"></string>
	<string id="str_tagged.gundabad" text="{=c}Tagged">
		<tags>
			<tag name="a" />
		</tags>
	</string>
	<string id="str_liege_title.gundabad" text="{=d}Warlord">
		<tags>
			<tag name="x" />
		</tags>
	</string>
	<string id="str_other.gondor" text="{=e}Gondor" />
	<string id="taom_faction_x.gundabad" text="{=f}skip" />
</strings>"""


class GundabadStringsTests(unittest.TestCase):
    def test_tagged_string_is_copied_whole_and_parses(self):
        out = inf.extract_gundabad_strings(SRC)
        ET.fromstring("<strings>" + out + "</strings>")
        self.assertIn("<tags>", out)
        self.assertIn("</string>", out)

    def test_only_gundabad_rows_without_liege_titles(self):
        out = inf.extract_gundabad_strings(SRC)
        self.assertIn("str_faction_name.gundabad", out)
        self.assertIn("str_tagged.gundabad", out)
        self.assertNotIn("str_liege_title", out)
        self.assertNotIn("gondor", out)
        self.assertNotIn("taom_faction_", out)

    def test_a_comment_or_a_quoted_bracket_does_not_drop_a_row(self):
        src = """<strings>
	<!-- the <string> rows -->
	<string id="str_hint.gondor" text="{=g}{?DAYS>1}days{?}" />
	<string id="str_faction_name.gundabad" text="{=a}Gundabad" />
	<string id="str_after.gondor" text="{=h}x"></string>
</strings>"""
        out = inf.extract_gundabad_strings(src)
        self.assertIn("str_faction_name.gundabad", out)
        self.assertNotIn("gondor", out)

    def test_a_duplicate_string_id_refuses_the_run(self):
        doubled = '<strings><string id="a.goblin" text="x" /><string id="a.goblin" text="y" /></strings>'
        with self.assertRaises(SystemExit) as ex:
            inf.refuse_duplicate_string_ids("taom_module_strings.xml", doubled)
        self.assertIn("a.goblin", str(ex.exception))
        inf.refuse_duplicate_string_ids("taom_module_strings.xml",
                                        '<strings><string id="a.goblin" text="x" /></strings>')

    def test_real_strings_file_extracts_to_parseable_xml(self):
        path = os.path.join(inf.MD, "taom_module_strings.xml")
        src = open(path, encoding="utf-8").read()
        out = inf.extract_gundabad_strings(src)
        ET.fromstring("<strings>" + out + "</strings>")
        self.assertNotIn("str_liege_title", out)


if __name__ == "__main__":
    unittest.main()
