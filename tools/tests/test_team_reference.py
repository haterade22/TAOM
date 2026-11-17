#!/usr/bin/env python3
"""Unit tests for the language team reference (#1421): tools/_team_reference.py, the translator's use of it, and
tools/build_translation_reference.py.

Run:  python -m unittest discover -s tools/tests -t .
"""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _team_reference as ref  # noqa: E402
import build_translation_reference as build  # noqa: E402
import import_team_translations as imp  # noqa: E402
import translate_with_claude as twc  # noqa: E402

PAIRS = [
    ["Gondor", "刚铎"],
    ["Minas Tirith", "米那斯提力斯"],
    ["Default: on", "默认：开启"],
    ["On", "开启"],
    ["Rohan", "罗翰"],
    ["Rohan", "罗翰"],
    ["Rohan", "洛汗"],
    ["GENDER", "性别"],
    ["horse", "骑兵队"],
    ["Riders of Rohan guard the plains of the Riddermark against raiders", "罗翰骠骑守护里德马克平原，抵御劫掠者。"],
    ["Archers of Gondor hold the walls of Minas Tirith", "刚铎弓箭手坚守米那斯提力斯的城墙。"],
    ["A short line", "短句"],
]


def entry(sid, text):
    return twc.Entry(Path("x.xml"), sid, text, text)


class TermTests(unittest.TestCase):
    def setUp(self):
        self.reference = ref.Reference(PAIRS)

    def test_terms_MultiWordTerm_MatchesWithoutCase(self):
        self.assertIn(("Minas Tirith", "米那斯提力斯"), self.reference.terms(["the gates of minas tirith"]))

    def test_terms_PunctuationInTheTerm_MatchesByWords(self):
        self.assertIn(("Default: on", "默认：开启"), self.reference.terms(["Applies at once. Default: on."]))

    def test_terms_ShortSingleWord_IsNeverATerm(self):
        # "On" would turn every "on" in a sentence into 开启.
        self.assertNotIn(("On", "开启"), self.reference.terms(["On the walls"]))

    def test_terms_SingleWord_MatchesOnlyWithItsCase(self):
        self.assertEqual([], [t for t in self.reference.terms(["a gondor spear"]) if t[0] == "Gondor"])
        self.assertIn(("Gondor", "刚铎"), self.reference.terms(["a Gondor spear"]))

    def test_terms_TwoRenderings_TakeTheOneTheTeamUsesMost(self):
        # 罗翰 sorts after 洛汗, so only the count can pick it.
        self.assertEqual([("Rohan", "罗翰")], self.reference.terms(["Rohan"]))

    def test_terms_LowercaseSingleWord_IsNeverATerm(self):
        # The team's "horse" is a log label (cavalry unit), not the word in a sentence.
        self.assertEqual([], self.reference.terms(["his horse threw him"]))

    def test_terms_AllCapsSingleWord_IsNeverATerm(self):
        self.assertEqual([], self.reference.terms(["GENDER"]))

    def test_terms_TheTeamsSentencesUseAnotherText_IsNotATerm(self):
        # The team's name row says 吕西卡, its own sentences about her say 芬达瑞尔.
        reference = ref.Reference(PAIRS + [
            ["Findariel", "吕西卡"],
            ["Findariel, wife of Ciryandur, manages the household", "芬达瑞尔是奇瑞安杜尔的妻子，管理家中事务。"],
            ["Findariel rides out with her guard at dawn", "芬达瑞尔黎明时与卫队一同出行。"],
        ])
        self.assertEqual([], [t for t in reference.terms(["Findariel"]) if t[0] == "Findariel"])

    def test_terms_ThreeLetterWord_TheTeamsSentencesConfirm_IsATerm(self):
        reference = ref.Reference(PAIRS + [
            ["Orc", "奥克"],
            ["Orc raiders burn the farms of the west", "奥克劫掠者焚烧西部的农庄。"],
            ["An Orc captain leads the warband north", "一名奥克队长带领战帮北上。"],
        ])
        self.assertIn(("Orc", "奥克"), reference.terms(["Dol Guldur Orc Axe"]))

    def test_terms_ThreeLetterWord_WithoutTeamSentences_IsNeverATerm(self):
        self.assertEqual([], ref.Reference([["All", "全部"]]).terms(["All troops hold"]))

    def test_guide_TellsTheModelNotToCopyNamesFromExamples(self):
        guide = ref.Reference(PAIRS).guide("Simplified Chinese", ["Archers of Gondor guard the walls of the city"])
        self.assertIn("never copy a name from an example", guide)

    def test_terms_WordInsideAPlaceholder_IsNotATerm(self):
        self.assertEqual([], self.reference.terms(["{Gondor} {?Minas Tirith}x{?}y{\?}"]))

    def test_terms_LongerTermsComeFirst_AndTheCapHolds(self):
        terms = self.reference.terms(["Minas Tirith of Gondor"], cap=1)
        self.assertEqual([("Minas Tirith", "米那斯提力斯")], terms)


class ExampleTests(unittest.TestCase):
    def setUp(self):
        self.reference = ref.Reference(PAIRS)

    def test_examples_PickTheSentenceWithTheMostSharedWords(self):
        examples = self.reference.examples(["Swordsmen of Gondor defend the walls of the citadel"], k=1)
        self.assertEqual([next(p for p in PAIRS if p[0].startswith("Archers"))], [list(e) for e in examples])

    def test_examples_TooFewSharedWords_GiveNone(self):
        self.assertEqual([], self.reference.examples(["Nothing in common here"]))

    def test_examples_ShortRows_AreNeverExamples(self):
        self.assertEqual([], self.reference.examples(["A short line"]))


class GuideTests(unittest.TestCase):
    def test_guide_NoReference_IsEmpty(self):
        self.assertEqual("", ref.Reference([]).guide("Simplified Chinese", ["Gondor"]))

    def test_guide_NamesTheTeamAndListsTermsAndExamples(self):
        guide = ref.Reference(PAIRS).guide("Simplified Chinese", ["Archers of Gondor guard the walls of the city"])
        self.assertIn("Simplified Chinese translation team", guide)
        self.assertIn("Gondor => 刚铎", guide)
        self.assertIn("EN: Archers of Gondor hold the walls of Minas Tirith", guide)
        self.assertIn("ZH: 刚铎弓箭手坚守米那斯提力斯的城墙。", guide)

    def test_load_NoFile_IsAnEmptyReference(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual("", ref.load("CNs", Path(d)).guide("Simplified Chinese", ["Gondor"]))

    def test_load_ReadsThePairs(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "cns.json").write_text(json.dumps({"pairs": PAIRS}, ensure_ascii=False), encoding="utf-8")
            self.assertIn("Gondor => 刚铎", ref.load("CNs", Path(d)).guide("Simplified Chinese", ["Gondor"]))


class RequestTests(unittest.TestCase):
    def test_main_PassesTheLanguagesReferenceToEveryBatch(self):
        seen = []

        def fake_model(provider, client, lang_name, batch, reference=None):
            seen.append(reference)
            return {e.string_id: "一支刚铎长矛" for e in batch}, (0, 0)

        with tempfile.TemporaryDirectory() as td:
            f = Path(td) / "std_x_zho-CN.xml"
            f.write_text('<base><strings><string id="a" text="A Gondor spear" /></strings></base>', encoding="utf-8")
            with mock.patch.object(twc, "discover_entries", return_value=[twc.Entry(f, "a", "A Gondor spear", "A Gondor spear")]),                     mock.patch.object(twc, "load_cache", return_value={}),                     mock.patch.object(twc, "load_overrides", return_value={}),                     mock.patch.object(twc, "save_cache"),                     mock.patch.object(twc, "require_key"),                     mock.patch.object(twc, "call_model", side_effect=fake_model),                     mock.patch.object(twc._team_reference, "load", return_value=ref.Reference(PAIRS)),                     mock.patch.object(twc, "REPO_ROOT", Path(td) / "repo"),                     contextlib.redirect_stdout(io.StringIO()):
                twc.main(["--lang", "CNs", "--module", "TAOM", "--provider", "deepseek", "--apply"])
        self.assertEqual(1, len(seen))
        self.assertIn("Gondor => 刚铎", seen[0].guide("Simplified Chinese", ["A Gondor spear"]))

    def test_build_request_WithAReference_PutsTheGuideInTheUserMessage(self):
        req = twc.build_request("Simplified Chinese", [entry("a", "A Gondor spear")], reference=ref.Reference(PAIRS))
        self.assertIn("Gondor => 刚铎", req["messages"][0]["content"])

    def test_build_request_WithoutAReference_IsUnchanged(self):
        batch = [entry("a", "A Gondor spear")]
        self.assertEqual(twc.build_request("German", batch),
                         twc.build_request("German", batch, reference=ref.Reference([])))

    def test_call_claude_batched_SendsTheGuideInEveryChunk(self):
        sent = []
        client = mock.MagicMock()
        client.messages.batches.create.side_effect = lambda requests: sent.extend(requests) or mock.MagicMock(id="b")
        client.messages.batches.retrieve.return_value = mock.MagicMock(processing_status="ended")
        client.messages.batches.results.return_value = []
        twc.call_claude_batched(client, "Simplified Chinese", [[entry("a", "A Gondor spear")]], poll_seconds=0,
                                reference=ref.Reference(PAIRS))
        self.assertIn("Gondor => 刚铎", sent[0]["params"]["messages"][0]["content"])


ENGLISH = """<?xml version="1.0" encoding="utf-8"?>
<strings>
  <string id="taom_gondor" text="{=taom_gondor}Gondor" />
  <string id="taom_drafted" text="{=taom_drafted}Drafted" />
  <string id="taom_mcm_x" text="{=taom_mcm_x}Enable Gondor" />
  <string id="taom_kept" text="{=taom_kept}Harolding" />
</strings>
"""

OURS = """<?xml version="1.0" encoding="utf-8"?>
<base type="string"><strings>
  <string id="taom_gondor" text="刚铎" />
  <string id="taom_drafted" text="草稿" />
  <string id="taom_mcm_x" text="启用刚铎" />
  <string id="taom_kept" text="哈罗丁家族" />
</strings></base>
"""

TEAM = ('<?xml version="1.0" encoding="utf-8"?><base type="string"><strings>'
        '<string id="taom_gondor" text="刚铎" /><string id="taom_drafted" text="另一个" />'
        '<string id="taom_kept" text="埃奥尔一族" /></strings></base>')
RUNTIME = ('<?xml version="1.0" encoding="utf-8"?><strings>'
           '<string source="Enable Gondor" text="启用刚铎" /><string source="{=k}Keyed" text="键" /></strings>')


class BuildTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        root = Path(self.dir.name)
        self.zip = root / "TAOM_CN.zip"
        with zipfile.ZipFile(self.zip, "w") as z:
            z.writestr("TAOM_CN/ModuleData/Languages/CNs/std_x_zho-CN.xml", TEAM)
            z.writestr("TAOM_CN/ModuleData/RuntimeTranslations.xml", RUNTIME)
        (root / "en.xml").write_text(ENGLISH, encoding="utf-8")
        (root / "cns.xml").write_text(OURS, encoding="utf-8")
        self.target = imp.Target(root / "cns.xml", imp._file_rows(root / "en.xml", strip_keys=True),
                                 imp._file_rows(root / "cns.xml", strip_keys=False), live=False)
        self.out = root / "out"

    def tearDown(self):
        self.dir.cleanup()

    def pairs(self):
        with imp.Drop(self.zip) as drop:
            return build.reference_pairs(drop, "CNs", [self.target])

    def test_reference_pairs_TakeOnlyRowsThatHoldTheTeamsText(self):
        # taom_drafted is our draft and taom_kept is our KEEP_OURS row: neither is the team's text.
        self.assertEqual([["Enable Gondor", "启用刚铎"], ["Gondor", "刚铎"]], self.pairs())

    def test_main_DryRun_WritesNothing(self):
        with mock.patch.object(build, "targets_for", return_value=[self.target]):
            self.assertEqual(0, build.main(["--lang", "CNs", "--from", str(self.zip), "--out-dir", str(self.out)]))
        self.assertFalse(self.out.exists())

    def test_reference_pairs_TwoTeamTexts_KeepTheOneUsedMost(self):
        self.target.english.update({"r1": "Rohan", "r2": "Rohan", "r3": "Rohan"})
        self.target.current.update({"r1": "罗翰", "r2": "罗翰", "r3": "洛汗"})
        runtime = ('<strings><string source="Rohan" text="罗翰" /><string source="Rohan" text="洛汗" /></strings>')
        with zipfile.ZipFile(self.zip, "a") as z:
            z.writestr("TAOM_CN/ModuleData/RuntimeTranslations.TAOM.Client.xml", runtime)
        self.assertIn(["Rohan", "罗翰"], self.pairs())

    def test_main_DropWithoutRowsForTheLanguage_WritesNothingAndExits2(self):
        with mock.patch.object(build, "targets_for", return_value=[self.target]):
            code = build.main(["--lang", "CNt", "--from", str(self.zip), "--out-dir", str(self.out), "--apply"])
        self.assertEqual(2, code)
        self.assertFalse((self.out / "cnt.json").exists())

    def test_main_Apply_WritesTheReference(self):
        with mock.patch.object(build, "targets_for", return_value=[self.target]):
            build.main(["--lang", "CNs", "--from", str(self.zip), "--out-dir", str(self.out), "--apply"])
        data = json.loads((self.out / "cns.json").read_text(encoding="utf-8"))
        self.assertEqual([["Enable Gondor", "启用刚铎"], ["Gondor", "刚铎"]], data["pairs"])


if __name__ == "__main__":
    unittest.main()
