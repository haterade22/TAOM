#!/usr/bin/env python3
"""The clan_heraldry specs mirror the live files, and applying them changes nothing (#589).

`generate_clan_heraldry.py` replaces a whole <MBPartyTemplate> from a spec, so a spec that has
fallen behind the live file is authority to revert whatever landed since. On 2026-09-13, 176 of
192 spec rosters and 6 Gondor bindings were behind. Two invariants stop that recurring:

  1. every spec clan with a roster names the template its clan is actually bound to, and its
     roster equals that template's live stacks;
  2. running every spec through the tool's three writer functions leaves all three live files
     byte-identical (the no-op invariant, the one a user of `--all --apply` actually relies on).

The pure sync in `sync_clan_specs_from_live.py` is unit-tested on a fixture below.
"""
import glob
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

import generate_clan_heraldry as gh  # noqa: E402
import sync_clan_specs_from_live as sync  # noqa: E402


class ShippedSpecsMirrorTheLiveFiles(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.live = sync.LiveState(gh.read(gh.CLANS_XML), gh.read(gh.SPCLANS_XSLT), gh.read(gh.PARTY_TMPL))
        cls.specs = {os.path.basename(p): json.load(open(p, encoding="utf-8"))
                     for p in sorted(glob.glob(os.path.join(gh.SPEC_DIR, "*.json")))}
        cls.assertTrue(len(cls.specs) >= 20, "the spec folder is unexpectedly small")

    def test_every_spec_clan_matches_its_live_binding_and_roster(self):
        drift = []
        for name, spec in self.specs.items():
            for clan in spec["clans"]:
                if not clan.get("roster"):
                    continue
                expected = sync.live_clan(self.live, clan)
                self.assertIsNotNone(expected, f"{name}: {clan['id']} has no live faction")
                if gh.template_id_for(clan) != expected["template_id"]:
                    drift.append(f"{name} {clan['id']}: binding {gh.template_id_for(clan)} vs live {expected['template_id']}")
                elif clan["roster"] != expected["roster"]:
                    drift.append(f"{name} {clan['id']}: roster differs from live {expected['template_id']}")
        self.assertEqual([], drift, "specs behind the live files (run tools/sync_clan_specs_from_live.py --apply):\n  " + "\n  ".join(drift))

    def test_applying_every_spec_is_a_no_op(self):
        clans, xslt, party = self.live.clans, self.live.xslt, self.live.party
        for name, spec in self.specs.items():
            for clan in spec["clans"]:
                roster = clan.get("roster") or []
                tid = gh.template_id_for(clan) if roster else None
                if clan["source"] == "xml":
                    clans = gh.set_clansxml_attrs(clans, clan["id"], clan["color"], clan["color2"], tid)
                else:
                    xslt = gh.upsert_xslt_override(xslt, clan["id"], clan["color"], clan["color2"], tid)
                if roster:
                    party = gh.upsert_party_template(party, tid, roster)
        self.assertEqual(self.live.clans, clans, "clans.xml would change")
        self.assertEqual(self.live.xslt, xslt, "spclans.xslt would change")
        self.assertEqual(self.live.party, party, "taom_partyTemplates.xml would change")


CLANS_XML = """<Factions>
\t<Faction id="clan_x_1"
\t\tcolor="FF111111"
\t\tcolor2="FF222222"
\t\tdefault_party_template="PartyTemplate.kingdom_hero_party_x_seat_template"
\t\tbanner_key="1" />
</Factions>"""

XSLT = """<xsl:stylesheet>
  <xsl:template match="Faction[@id='clan_x_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'color' and local-name() != 'color2' and local-name() != 'default_party_template']"/>
      <xsl:attribute name="color">FF333333</xsl:attribute>
      <xsl:attribute name="color2">FF444444</xsl:attribute>
      <xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_clan_x_2_template</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>"""

PARTY = """<partyTemplates>
\t<MBPartyTemplate id="kingdom_hero_party_x_seat_template">
\t\t<stacks>
\t\t\t<PartyTemplateStack min_value="2" max_value="30" troop="NPCCharacter.x_a" />
\t\t\t<PartyTemplateStack min_value="0" max_value="20" troop="NPCCharacter.x_b" />
\t\t</stacks>
\t</MBPartyTemplate>
\t<MBPartyTemplate id="kingdom_hero_party_clan_x_2_template">
\t\t<stacks>
\t\t\t<PartyTemplateStack min_value="1" max_value="9" troop="NPCCharacter.x_c" />
\t\t</stacks>
\t</MBPartyTemplate>
</partyTemplates>"""


def fixture_spec():
    return {"culture": "x", "clans": [
        {"id": "clan_x_1", "source": "xml", "color": "FF111111", "color2": "FF222222",
         "template_id": "kingdom_hero_party_x_old_template",
         "roster": [{"troop": "x_a", "min": 1, "max": 3}]},
        {"id": "clan_x_2", "source": "xslt", "color": "FF333333", "color2": "FF444444",
         "roster": [{"troop": "x_c", "min": 1, "max": 2}]},
        {"id": "clan_x_3", "source": "xslt", "color": "FF555555", "color2": "FF666666"},
    ]}


class SyncSpecFromLive(unittest.TestCase):

    def setUp(self):
        self.live = sync.LiveState(CLANS_XML, XSLT, PARTY)

    def test_binding_and_roster_come_from_the_live_files(self):
        spec, changes = sync.sync_spec(fixture_spec(), self.live)
        c1, c2 = spec["clans"][0], spec["clans"][1]
        self.assertEqual("kingdom_hero_party_x_seat_template", c1["template_id"])
        self.assertEqual([{"troop": "x_a", "min": 2, "max": 30}, {"troop": "x_b", "min": 0, "max": 20}], c1["roster"])
        self.assertNotIn("template_id", c2, "a binding equal to the default id is not written as an explicit key")
        self.assertEqual([{"troop": "x_c", "min": 1, "max": 9}], c2["roster"])
        self.assertEqual({"(file)", "clan_x_1", "clan_x_2"}, {c for c, _ in changes})

    def test_a_synced_spec_says_where_its_rosters_come_from(self):
        spec, changes = sync.sync_spec(fixture_spec(), self.live)
        keys = list(spec.keys())
        self.assertEqual(sync.ROSTERS_NOTE, spec["_rosters"])
        self.assertLess(keys.index("_rosters"), keys.index("clans"), "the note sits in the header, above the clans")
        self.assertIn(("(file)", "_rosters provenance note"), changes)

    def test_a_colour_only_spec_gets_no_rosters_note(self):
        spec = {"culture": "x", "clans": [{"id": "clan_x_3", "source": "xslt", "color": "FF555555", "color2": "FF666666"}]}
        out, changes = sync.sync_spec(spec, self.live)
        self.assertNotIn("_rosters", out)
        self.assertEqual([], changes)

    def test_colours_and_colour_only_clans_are_untouched(self):
        spec, changes = sync.sync_spec(fixture_spec(), self.live)
        self.assertEqual(("FF111111", "FF222222"), (spec["clans"][0]["color"], spec["clans"][0]["color2"]))
        self.assertEqual({"id": "clan_x_3", "source": "xslt", "color": "FF555555", "color2": "FF666666"}, spec["clans"][2])

    def test_second_sync_changes_nothing(self):
        once, _ = sync.sync_spec(fixture_spec(), self.live)
        twice, changes = sync.sync_spec(json.loads(json.dumps(once)), self.live)
        self.assertEqual(once, twice)
        self.assertEqual([], changes)

    def test_a_clan_bound_to_a_missing_template_is_refused(self):
        live = sync.LiveState(CLANS_XML.replace("x_seat_template", "x_gone_template"), XSLT, PARTY)
        with self.assertRaises(sync.LiveTemplateMissing):
            sync.sync_spec(fixture_spec(), live)

    def test_a_clan_whose_live_faction_binds_no_template_is_refused(self):
        # The engine falls back to the CULTURE template, so guessing kingdom_hero_party_<id>_template
        # would write a roster the clan never fields.
        xslt = XSLT.replace(
            '      <xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_clan_x_2_template</xsl:attribute>\n', "")
        live = sync.LiveState(CLANS_XML, xslt, PARTY)
        with self.assertRaises(sync.LiveBindingMissing):
            sync.sync_spec(fixture_spec(), live)

    def test_the_note_is_reworded_only_where_rosters_exist(self):
        old = "Per-clan color = x; rosters archetype-composed from troops_*.xml (culture=x)."
        with_rosters = fixture_spec(); with_rosters["_note"] = old
        out, changes = sync.sync_spec(with_rosters, self.live)
        self.assertIn("originally archetype-composed", out["_note"])
        self.assertTrue(out["_note"].endswith("; see _rosters for their current source."))
        self.assertIn(("(file)", "_note reworded"), changes)
        colour_only = {"culture": "x", "_note": old,
                       "clans": [{"id": "clan_x_3", "source": "xslt", "color": "FF555555", "color2": "FF666666"}]}
        out2, changes2 = sync.sync_spec(colour_only, self.live)
        self.assertEqual(old, out2["_note"], "no _rosters key to point at, so the note stays")
        self.assertEqual([], changes2)

    def test_a_clan_with_no_live_faction_is_refused(self):
        spec = fixture_spec(); spec["clans"][0]["id"] = "clan_nobody"
        with self.assertRaises(sync.LiveFactionMissing):
            sync.sync_spec(spec, self.live)


class SerializationKeepsEachFilesShape(unittest.TestCase):

    def test_crlf_no_trailing_newline_ascii_escaped(self):
        original = b'{\r\n  "culture": "x",\r\n  "clans": [\r\n    {\r\n      "theme": "x \\u2014 y"\r\n    }\r\n  ]\r\n}'
        out = sync.serialize({"culture": "x", "clans": [{"theme": "x — y"}]}, original)
        self.assertEqual(original, out)

    def test_lf_trailing_newline_utf8_literal(self):
        original = '{\n  "culture": "x",\n  "clans": [\n    {\n      "theme": "x — y"\n    }\n  ]\n}\n'.encode("utf-8")
        out = sync.serialize({"culture": "x", "clans": [{"theme": "x — y"}]}, original)
        self.assertEqual(original, out)


if __name__ == "__main__":
    unittest.main()
