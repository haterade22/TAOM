#!/usr/bin/env python3
"""render_xslt_culture() and the `culture_xslt` source in promote_borrowed_cultures.py: a culture that exists only
as an XSLT rewrite of a vanilla one (Dale is vanilla `sturgia` through spcultures.xslt) is cloned from the culture
the game builds, laid out as the generated blocks are.

Run:  python -m unittest tools.tests.test_promote_xslt_culture
"""
import importlib.util
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import promote_borrowed_cultures as promo  # noqa: E402

VANILLA = """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
\t<Culture id="sturgia" name="{=x}Sturgia" board_game_type="Konane">
\t\t<male_names>
\t\t\t<name name="{=a}Olek" />
\t\t</male_names>
\t</Culture>
\t<Culture id="vlandia" name="{=y}Vlandia" />
</SPCultures>
"""

XSLT = """<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
\t<xsl:template match="@*|node()"><xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy></xsl:template>
\t<xsl:template match="Culture[@id='sturgia']">
\t\t<xsl:copy>
\t\t\t<xsl:apply-templates select="@*"/>
\t\t\t<xsl:attribute name="name">{=TAOM_sturgia_culture}Barding</xsl:attribute>
\t\t\t<xsl:attribute name="text">{=TAOM_sturgia_desc}Bows &amp; "trade"</xsl:attribute>
\t\t\t<xsl:attribute name="basic_troop">NPCCharacter.dale_recruit</xsl:attribute>
\t\t\t<xsl:apply-templates select="*"/>
\t\t</xsl:copy>
\t</xsl:template>
</xsl:stylesheet>
"""


@unittest.skipUnless(importlib.util.find_spec("lxml") is not None, "lxml not installed")
class RenderXsltCultureTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.xslt = Path(self.dir.name) / "spcultures.xslt"
        self.source = Path(self.dir.name) / "spcultures.xml"
        self.xslt.write_text(XSLT, encoding="utf-8")
        self.source.write_text(VANILLA, encoding="utf-8")

    def tearDown(self):
        self.dir.cleanup()

    def render(self, cid="sturgia"):
        return promo.render_xslt_culture(cid, self.xslt, self.source)

    def test_the_stylesheet_runs_over_the_vanilla_culture(self):
        block = self.render()
        self.assertIn('name="{=TAOM_sturgia_culture}Barding"', block)
        self.assertIn('basic_troop="NPCCharacter.dale_recruit"', block)
        self.assertIn('board_game_type="Konane"', block)  # a vanilla attribute the template keeps
        self.assertNotIn("Vlandia", block)

    def test_the_block_is_laid_out_as_a_generated_one(self):
        block = self.render()
        self.assertTrue(block.startswith('  <Culture\n    id="sturgia"\n    name='), block[:60])
        self.assertIn("\n    <male_names>\n      <name name=\"{=a}Olek\" />\n    </male_names>\n", block)
        self.assertTrue(block.endswith("\n  </Culture>\n"), block[-40:])
        self.assertNotIn("\t", block)

    def test_attribute_values_stay_escaped(self):
        self.assertIn('text="{=TAOM_sturgia_desc}Bows &amp; &quot;trade&quot;"', self.render())

    def test_a_culture_the_file_lacks_is_refused(self):
        with self.assertRaises(SystemExit) as ctx:
            self.render("khuzait")
        self.assertIn("found 0", str(ctx.exception))

    def test_source_blocks_names_it_as_the_source_culture(self):
        real = promo.render_xslt_culture
        promo.render_xslt_culture = lambda cid: real(cid, self.xslt, self.source)
        try:
            block = promo.source_blocks("culture", "", "dale", {
                "culture_xslt": "sturgia", "culture_xslt_keys": promo.NORTHMAN_SCAFFOLDS["dale"]["culture_xslt_keys"]})
        finally:
            promo.render_xslt_culture = real
        self.assertIn('\n    id="dale"\n', block)
        self.assertIn('name="{=aom_dale_name}Barding"', block)
        self.assertIn('text="{=aom_dale_desc}', block)
        self.assertNotIn("TAOM_sturgia", block)


if __name__ == "__main__":
    unittest.main()
