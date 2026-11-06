"""loc_length_report.py: translated text that may not fit its widget (#1007).

Every test runs the tool on a temp ModuleData folder through --root; one smoke test runs it on the real
repo with a single real key."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
TOOL = TOOLS / "loc_length_report.py"

HEAD = '<?xml version="1.0" encoding="utf-8"?>\n<base type="string">\n  <strings>\n'
TAIL = '  </strings>\n</base>\n'
LOCALES = {"DE": "deu-DE", "JP": "jpn-JP", "FR": "fre-FR", "KO": "kor-KO"}


def rows(pairs, inline=False):
    out = ""
    for k, t in pairs.items():
        text = "{=%s}%s" % (k, t) if inline else t
        out += '    <string id="%s" text="%s" />\n' % (k, text)
    return HEAD + out + TAIL


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def make_root(root, english, langs):
    """english: {key: text}; langs: {"DE": {key: text}}."""
    write(root / "taom_module_strings.xml", rows(english, inline=True))
    for lang, pairs in langs.items():
        write(root / "Languages" / lang / ("std_taom_module_strings_%s.xml" % LOCALES[lang]),
              rows(pairs))


def run(root, *args, cwd=None):
    proc = subprocess.run([sys.executable, "-B", str(TOOL), "--root", str(root), *args],
                          capture_output=True, text=True, encoding="utf-8", cwd=cwd)
    return proc.returncode, proc.stdout, proc.stderr


class LengthReportTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name) / "ModuleData"

    def test_LongGermanRow_IsFlagged(self):
        make_root(self.root, {"k1": "Hire the soldiers"},
                  {"DE": {"k1": "Die Soldaten anheuern und ausbilden lassen"}})
        code, out, _ = run(self.root)
        self.assertEqual(code, 0)
        self.assertIn("k1", out)
        self.assertIn("DE", out)
        self.assertTrue(out.strip().splitlines()[-1].startswith("loc_length_report: 1 of 1 keys flagged"))

    def test_ShortLabelBelowMinExtra_IsNotFlagged(self):
        make_root(self.root, {"k1": "OK"}, {"DE": {"k1": "Okay!!"}})
        code, out, _ = run(self.root)
        self.assertEqual(code, 0)
        self.assertIn("0 of 1 keys flagged", out.strip().splitlines()[-1])

    def test_Placeholders_AreStrippedFromBothSides(self):
        english = {"k1": "Pay {AMOUNT} gold"}
        long_var = "{VERY_LONG_PLACEHOLDER_NAME_HERE}"
        make_root(self.root, english, {"DE": {"k1": "Zahle %s Gold" % long_var}})
        code, out, _ = run(self.root, "--json", "--ratio", "1.0", "--min-extra", "0")
        self.assertEqual(code, 0)
        row = json.loads(out)[0]
        self.assertEqual(row["english_length"], len("Pay  gold"))
        self.assertEqual(row["translated_length"], len("Zahle  Gold"))

    def test_CjkCharacters_WeighTwo(self):
        make_root(self.root, {"k1": "Hire soldiers"}, {"JP": {"k1": "兵士を雇う兵士を雇う"}})
        code, out, _ = run(self.root, "--json", "--ratio", "1.0", "--min-extra", "0")
        self.assertEqual(code, 0)
        row = json.loads(out)[0]
        self.assertEqual(row["language"], "JP")
        self.assertEqual(row["translated_length"], 20)

    def test_KeysFilter_ReportsOnlyThoseKeys(self):
        long = "Ein sehr langer deutscher Satz hier"
        make_root(self.root, {"a": "Short one", "b": "Short two"},
                  {"DE": {"a": long, "b": long}})
        code, out, _ = run(self.root, "--keys", "b", "--json")
        self.assertEqual([r["key"] for r in json.loads(out)], ["b"])

    def test_LangFilter_SkipsOtherLanguages(self):
        make_root(self.root, {"a": "Short one"},
                  {"DE": {"a": "Ein sehr langer deutscher Satz hier"},
                   "FR": {"a": "Une phrase francaise vraiment longue"}})
        code, out, _ = run(self.root, "--lang", "FR", "--json")
        self.assertEqual({r["language"] for r in json.loads(out)}, {"FR"})

    def test_Since_ReportsKeysChangedInTheDiff(self):
        long = "Ein sehr langer deutscher Satz hier"
        make_root(self.root, {"a": "Short one", "b": "Short two"},
                  {"DE": {"a": long, "b": long}})
        git = lambda *a: subprocess.run(["git", "-C", str(self.root), *a], check=True,
                                        capture_output=True, text=True)
        git("init", "-q")
        git("add", "-A")
        git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "base")
        de = self.root / "Languages" / "DE" / "std_taom_module_strings_deu-DE.xml"
        de.write_text(de.read_text(encoding="utf-8").replace(
            'id="b" text="%s"' % long, 'id="b" text="%s ganz neu"' % long), encoding="utf-8")
        code, out, _ = run(self.root, "--since", "HEAD", "--json")
        self.assertEqual(code, 0)
        self.assertEqual([r["key"] for r in json.loads(out)], ["b"])

    def test_MissingLanguageFolder_ExitsOne(self):
        make_root(self.root, {"a": "Short one"}, {})
        code, out, err = run(self.root)
        self.assertEqual(code, 1)
        self.assertIn("Languages", out + err)

    def test_RequestedLanguageMissing_ExitsOne(self):
        make_root(self.root, {"a": "Short one"}, {"DE": {"a": "Kurz"}})
        code, out, err = run(self.root, "--lang", "FR")
        self.assertEqual(code, 1)
        self.assertIn("FR", out + err)

    def test_UnknownLanguage_IsUsageError(self):
        make_root(self.root, {"a": "Short one"}, {"DE": {"a": "Kurz"}})
        code, _, _ = run(self.root, "--lang", "XX")
        self.assertEqual(code, 2)

    def test_Json_RowShape(self):
        make_root(self.root, {"k1": "Hire the soldiers"},
                  {"DE": {"k1": "Die Soldaten anheuern und ausbilden lassen"}})
        code, out, _ = run(self.root, "--json")
        self.assertEqual(code, 0)
        rows_ = json.loads(out)
        self.assertEqual(set(rows_[0]), {"key", "english_length", "language", "translated_length",
                                         "ratio", "english"})
        self.assertEqual(rows_[0]["key"], "k1")
        self.assertEqual(rows_[0]["english"], "Hire the soldiers")

    def test_RowsEqualToEnglish_AreUntranslatedAndNotFlagged(self):
        make_root(self.root, {"a": "Short one", "b": "Hire the soldiers"},
                  {"DE": {"a": "Short one", "b": "Hire the soldiers"},
                   "FR": {"a": "Short one"}})
        code, out, err = run(self.root, "--ratio", "1.0", "--min-extra", "0")
        self.assertEqual(code, 0)
        self.assertEqual(out.strip().splitlines()[-1],
                         "loc_length_report: 0 of 2 keys flagged (ratio >= 1.0); "
                         "2 untranslated or identical to English (names included), so their fit is unknown")

    def test_KeyTranslatedInOneLanguageOnly_IsJudgedOnThatLanguage(self):
        make_root(self.root, {"a": "Hire the soldiers"},
                  {"DE": {"a": "Hire the soldiers"},
                   "FR": {"a": "Engager les soldats et les former"}})
        code, out, _ = run(self.root, "--json")
        rows_ = json.loads(out)
        self.assertEqual([(r["key"], r["language"]) for r in rows_], [("a", "FR")])

    def test_Json_PutsSummaryOnStderr(self):
        make_root(self.root, {"a": "Short one"}, {"DE": {"a": "Short one"}})
        code, out, err = run(self.root, "--json")
        self.assertEqual(json.loads(out), [])
        self.assertIn("1 untranslated or identical to English (names included), so their fit is unknown", err)

    def test_GenderConditional_IsMeasuredByItsLongerBranch(self):
        make_root(self.root, {"k1": "Hire"},
                  {"DE": {"k1": "{?PLAYER.GENDER}Anheuern{?}Anheuerin{\?}"}})
        code, out, _ = run(self.root, "--json", "--ratio", "1.0", "--min-extra", "0")
        row = json.loads(out)[0]
        self.assertEqual(row["translated_length"], len("Anheuerin"))

    def test_HalfWidthKatakana_CountsSingle(self):
        make_root(self.root, {"k1": "Hire"}, {"JP": {"k1": "ﾊｲﾔｨｲﾔ"}})
        code, out, _ = run(self.root, "--json", "--ratio", "1.0", "--min-extra", "0")
        self.assertEqual(json.loads(out)[0]["translated_length"], 6)

    def test_HangulJamo_CountsDouble(self):
        make_root(self.root, {"k1": "Hire"}, {"KO": {"k1": "한"}})
        code, out, _ = run(self.root, "--json", "--ratio", "1.0", "--min-extra", "0")
        self.assertEqual(json.loads(out)[0]["translated_length"], 6)

    def test_Since_PicksUpKeysOfUntrackedFiles(self):
        long = "Ein sehr langer deutscher Satz hier"
        make_root(self.root, {"a": "Short one"}, {"DE": {"a": long}})
        git = lambda *a: subprocess.run(["git", "-C", str(self.root), *a], check=True,
                                        capture_output=True, text=True)
        git("init", "-q")
        git("add", "-A")
        git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "base")
        write(self.root / "taom_career_strings.xml", rows({"n": "Short two"}, inline=True))
        write(self.root / "Languages" / "DE" / "std_taom_career_strings_deu-DE.xml",
              rows({"n": long}))
        code, out, _ = run(self.root, "--since", "HEAD", "--json")
        self.assertEqual([r["key"] for r in json.loads(out)], ["n"])

    def test_RealRepo_OneKey_ExitsZero(self):
        env = dict(os.environ)
        proc = subprocess.run([sys.executable, "-B", str(TOOL), "--keys", "taom_str_faction_ruler.empire"],
                              capture_output=True, text=True, encoding="utf-8", env=env,
                              cwd=str(TOOLS.parent))
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("loc_length_report:", proc.stdout)
        self.assertIn("of 1 keys", proc.stdout)


if __name__ == "__main__":
    unittest.main()
