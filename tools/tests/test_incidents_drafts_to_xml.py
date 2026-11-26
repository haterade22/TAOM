#!/usr/bin/env python3
"""tools/incidents_drafts_to_xml.py: drafts (TSV) to the English game-text rows of Incident Voices, and its validation."""
import os
import sys
import tempfile
import unittest
from pathlib import Path
from xml.etree import ElementTree

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import incidents_drafts_to_xml as conv  # noqa: E402

DASH = chr(0x2014)

CATALOG = {
    "vocabulary": ["GODS", "COIN"],
    "incidents": {
        "rooster_theft": {"id": "incident_rooster_theft", "versions": ["all"], "options": 2, "vars": ["PRONOUN", "RANK"]},
        "souvenirs": {"id": "incident_souvenirs", "versions": ["free", "shadow"], "options": 1, "vars": ["AMOUNT"]},
        "job_offer": {"id": "incident_job_offer", "versions": ["all"], "options": 3, "vars": ["TOWN"], "vanilla_text": True},
    },
}


def row(key, incident="incident_rooster_theft", side="all", slot="title", text="A text", where="t.tsv:2"):
    return {"key": key, "incident_id": incident, "side": side, "slot": slot, "text": text, "notes": "", "where": where}


def good(slot="title", text="A text"):
    return row(f"taom_inc_rooster_theft_all_{slot}", slot=slot, text=text)


class ValidateTests(unittest.TestCase):
    def test_AGoodRow_HasNoProblem(self):
        self.assertEqual(conv.validate([good()], CATALOG), [])

    def test_ANoteRow_IsSkipped(self):
        note = row("taom_inc_job_offer_all_-", "incident_job_offer", slot="-", text="")
        self.assertEqual(conv.validate([note], CATALOG), [])

    def test_AKeyThatDoesNotMatchItsParts_IsRefused(self):
        errors = conv.validate([row("taom_inc_rooster_theft_free_title")], CATALOG)
        self.assertTrue(any("should be taom_inc_rooster_theft_all_title" in e for e in errors), errors)

    def test_AnUnknownIncident_IsRefused(self):
        errors = conv.validate([row("taom_inc_nope_all_title", "incident_nope")], CATALOG)
        self.assertTrue(any("not in the catalog" in e for e in errors), errors)

    def test_ASideTheIncidentDoesNotHave_IsRefused(self):
        errors = conv.validate([row("taom_inc_rooster_theft_shadow_title", side="shadow")], CATALOG)
        self.assertTrue(any("not 'shadow'" in e for e in errors), errors)

    def test_ASplitIncidentTakesFreeAndShadowButNotAll(self):
        ok = [row("taom_inc_souvenirs_free_title", "incident_souvenirs", "free"), row("taom_inc_souvenirs_shadow_title", "incident_souvenirs", "shadow")]
        self.assertEqual(conv.validate(ok, CATALOG), [])
        bad = conv.validate([row("taom_inc_souvenirs_all_title", "incident_souvenirs", "all")], CATALOG)
        self.assertTrue(any("not 'all'" in e for e in bad), bad)

    def test_AnOptionBeyondVanillasCount_IsRefused(self):
        errors = conv.validate([good("opt3")], CATALOG)
        self.assertTrue(any("has 2 options" in e for e in errors), errors)

    def test_AnUnknownSlot_IsRefused(self):
        errors = conv.validate([row("taom_inc_rooster_theft_all_body", slot="body")], CATALOG)
        self.assertTrue(any("is not title, desc or optN" in e for e in errors), errors)

    def test_AVanillaTextIncident_TakesNoText(self):
        errors = conv.validate([row("taom_inc_job_offer_all_title", "incident_job_offer")], CATALOG)
        self.assertTrue(any("another developer's text" in e for e in errors), errors)

    def test_AVariableVanillaDoesNotSet_IsRefused_AndOneItDoesIsFine(self):
        self.assertEqual(conv.validate([good("desc", "Your {RANK} stole it, said {PRONOUN}.")], CATALOG), [])
        errors = conv.validate([good("desc", "Your {SOLDIER} stole it.")], CATALOG)
        self.assertTrue(any("{SOLDIER}" in e for e in errors), errors)

    def test_EngineTagsAndAttributeFormsAreNotVariables(self):
        text = "{?PLAYER.GENDER}He{?}She{\\?} greets {PLAYER.NAME}{=abc}"
        self.assertEqual(conv.variables_in(text), ["PLAYER"])

    def test_AVocabularyWord_NeedsBothSidesInTheSameRun(self):
        errors = conv.validate([good("desc", "By {GODS}.")], CATALOG)
        self.assertEqual(len([e for e in errors if "vocabulary row" in e]), 2, errors)
        vocab = [row("taom_inc_voc_free_gods", "voc", "free", "gods", "the Valar"), row("taom_inc_voc_shadow_gods", "voc", "shadow", "gods", "the Dark Lord")]
        self.assertEqual(conv.validate([good("desc", "By {GODS}.")] + vocab, CATALOG), [])

    def test_AVocabularyRowWithAWrongWordOrKey_IsRefused(self):
        errors = conv.validate([row("taom_inc_voc_free_pets", "voc", "free", "pets", "cats")], CATALOG)
        self.assertTrue(any("not a vocabulary word" in e for e in errors), errors)
        errors = conv.validate([row("taom_inc_voc_free_gods_x", "voc", "free", "gods", "the Valar")], CATALOG)
        self.assertTrue(any("should be taom_inc_voc_free_gods" in e for e in errors), errors)

    def test_APeoplesVocabularyRow_IsAccepted_AndDoesNotReplaceTheSideRows(self):
        for side in ("dwarves", "isengard", "orcs", "southrons_shadow", "elves_free"):
            rows = [row(f"taom_inc_voc_{side}_gods", "voc", side, "gods", "Aule")]
            self.assertEqual(conv.validate(rows, CATALOG), [], side)
        errors = conv.validate([good("desc", "By {GODS}."), row("taom_inc_voc_dwarves_gods", "voc", "dwarves", "gods", "Aule")], CATALOG)
        self.assertEqual(len([e for e in errors if "vocabulary row" in e]), 2, "a people row never stands in for the free and shadow rows")

    def test_APeoplesVocabularyRow_WithAnUnknownPeopleOrAWrongKey_IsRefused(self):
        errors = conv.validate([row("taom_inc_voc_hobbits_gods", "voc", "hobbits", "gods", "Yavanna")], CATALOG)
        self.assertTrue(any("must be free, shadow or a people" in e for e in errors), errors)
        errors = conv.validate([row("taom_inc_voc_dwarves_power_oath", "voc", "dwarves", "gods", "Aule")], CATALOG)
        self.assertTrue(any("should be taom_inc_voc_dwarves_gods" in e for e in errors), errors)

    def test_AVocabularyTextMayNotHoldAVariable(self):
        errors = conv.validate([row("taom_inc_voc_free_gods", "voc", "free", "gods", "the {COIN}")], CATALOG)
        self.assertTrue(any("must not hold the variable" in e for e in errors), errors)

    def test_Dashes_BannedWords_AndWhitespaceAreRefused(self):
        for text, needle in [("a " + DASH + " b", "dash"), ("Heaven help us", "banned word"), ("In Calradia", "banned word"), ("a\tb", "tab"), ("", "no text")]:
            errors = conv.validate([good("desc", text)], CATALOG)
            self.assertTrue(any(needle in e for e in errors), (text, errors))

    def test_ARepeatedKey_IsRefused(self):
        errors = conv.validate([good(), good()], CATALOG)
        self.assertTrue(any("is repeated" in e for e in errors), errors)


class RenderTests(unittest.TestCase):
    def test_ARowBecomesAGameTextRowWithTheTranslationKey_AndIsEscaped(self):
        block = conv.render_block([good("desc", 'Tom & "Jerry" <ride>')])

        self.assertIn('<string id="str_taom_inc.rooster_theft_all_desc" text="{=taom_inc_rooster_theft_all_desc}Tom &amp; &quot;Jerry&quot; &lt;ride&gt;" />', block)
        self.assertTrue(block.lstrip().startswith("<!-- INCIDENT-VOICES-BEGIN"))
        self.assertTrue(block.rstrip().endswith("INCIDENT-VOICES-END -->"))

    def test_TheBlockIsWellFormedXml_AndOrdersTitleDescThenOptionsNumerically(self):
        rows = [good("opt10", "j"), good("opt2", "b"), good("desc", "d"), good("title", "t"), good("opt1", "a")]
        block = conv.render_block(rows)

        root = ElementTree.fromstring("<strings>" + block + "</strings>")
        ids = [s.get("id") for s in root.iter("string")]
        self.assertEqual([i.rsplit("_", 1)[1] for i in ids], ["title", "desc", "opt1", "opt2", "opt10"])

    def test_VocabularyRowsComeFirst(self):
        rows = [good(), row("taom_inc_voc_free_gods", "voc", "free", "gods", "the Valar")]
        ids = [s.get("id") for s in ElementTree.fromstring("<strings>" + conv.render_block(rows) + "</strings>").iter("string")]
        self.assertEqual(ids[0], "str_taom_inc.voc_free_gods")


class ApplyTests(unittest.TestCase):
    XML = '<?xml version="1.0" encoding="utf-8"?>\r\n<strings>\r\n\t<string id="a" text="A" />\r\n\r\n</strings>\r\n'

    def test_TheFirstRunAddsTheBlockBeforeTheClosingTag_AndKeepsTheRest(self):
        out = conv.apply_block(self.XML, conv.render_block([good()], "\r\n"))

        self.assertIn('<string id="a" text="A" />', out)
        self.assertLess(out.index("INCIDENT-VOICES-BEGIN"), out.index("</strings>"))
        ElementTree.fromstring(out.split("?>", 1)[1])

    def test_ARerunReplacesTheBlock_AndIsIdempotent(self):
        first = conv.apply_block(self.XML, conv.render_block([good()], "\r\n"))
        second = conv.apply_block(first, conv.render_block([good(), good("desc", "more")], "\r\n"))
        third = conv.apply_block(second, conv.render_block([good(), good("desc", "more")], "\r\n"))

        self.assertEqual(second.count("INCIDENT-VOICES-BEGIN"), 1)
        self.assertIn("more", second)
        self.assertEqual(second, third)

    def test_LineEndingsStayCrlf(self):
        out = conv.apply_block(self.XML, conv.render_block([good()], "\r\n"))
        self.assertNotIn("\n", out.replace("\r\n", ""))


class ShippedCatalogTests(unittest.TestCase):
    def test_TheShippedCatalogLoads_With101Incidents_AndTheVocabulary(self):
        catalog = conv.load_catalog(conv.DEFAULT_CATALOG)

        self.assertEqual(len(catalog["incidents"]), 101)
        self.assertEqual(catalog["vocabulary"], ["GODS", "POWER_OATH", "HOLY_PLACE", "HOLY_PERSON", "DARK_FORCE", "LORD_TITLE", "COIN"])
        self.assertEqual(catalog["incidents"]["market_manipulation"]["vars"], ["AMOUNT", "MERCHANT", "MERCHANT_NAME", "PLACE", "TOWN"])

    def test_AFullPipelineRun_WritesTheBlockIntoACopy(self):
        with tempfile.TemporaryDirectory() as tmp:
            drafts = Path(tmp) / "batch-1.tsv"
            drafts.write_text(
                "key\tincident_id\tside\tslot\ttext\tnotes\n"
                "taom_inc_rooster_theft_all_title\tincident_rooster_theft\tall\ttitle\tThe Rooster\t\n"
                "taom_inc_rooster_theft_all_desc\tincident_rooster_theft\tall\tdesc\tA {RANK} took a hen.\t\n",
                encoding="utf-8")
            xml = Path(tmp) / "strings.xml"
            xml.write_bytes(ApplyTests.XML.encode("utf-8"))

            self.assertEqual(conv.main([str(drafts), "--xml", str(xml)]), 0)
            self.assertEqual(xml.read_bytes().decode("utf-8"), ApplyTests.XML, "without --apply nothing is written")
            self.assertEqual(conv.main([str(drafts), "--xml", str(xml), "--apply"]), 0)

            out = xml.read_bytes().decode("utf-8")
            self.assertIn("{=taom_inc_rooster_theft_all_desc}A {RANK} took a hen.", out)

    def test_AnInvalidRun_WritesNothing_AndExitsOne(self):
        with tempfile.TemporaryDirectory() as tmp:
            drafts = Path(tmp) / "batch-1.tsv"
            drafts.write_text("key\tincident_id\tside\tslot\ttext\tnotes\ntaom_inc_rooster_theft_all_title\tincident_rooster_theft\tall\ttitle\tHeaven\t\n", encoding="utf-8")
            xml = Path(tmp) / "strings.xml"
            xml.write_bytes(ApplyTests.XML.encode("utf-8"))

            self.assertEqual(conv.main([str(drafts), "--xml", str(xml), "--apply"]), 1)
            self.assertEqual(xml.read_bytes().decode("utf-8"), ApplyTests.XML)


if __name__ == "__main__":
    unittest.main()
