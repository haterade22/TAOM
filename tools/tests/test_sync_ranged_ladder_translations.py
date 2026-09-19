#!/usr/bin/env python3
"""Unit tests for tools/sync_ranged_ladder_translations.py (#617): the translated ladder names move
from the retired band ids to the per-tier ids, byte-faithfully.

Run:  python -m unittest discover -s tools/tests -p "test_sync_ranged_ladder_translations.py"
"""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ranged_ladder as rl  # noqa: E402
import sync_ranged_ladder_translations as st  # noqa: E402

EOL = "\r\r\n"   # the translated Armory files end every line this way


def _spec():
    t = {str(i): 10 * i + 40 for i in range(11)}
    return {
        "bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
        "stats": {"anchor_rank": 1,
                  "damage": {"Bow": t, "Crossbow": t, "step": {str(i): 0.0 for i in range(11)}},
                  "accuracy": {"top": {str(i): 80 + i for i in range(11)}, "rank_step": 1, "crossbow_bonus": 0, "max": 99},
                  "speed": {"tier_base": {str(i): 60 + i for i in range(11)}, "rank_step": 1},
                  "skill": {"anchor": {str(i): 20 * i for i in range(11)}, "step": {str(i): 0 for i in range(11)}, "min": 0}},
        "lines": [
            {"id": "elf", "folder": "elf", "files": ["elf"], "ranks": {"overall": 1, "damage": 1, "accuracy": 1},
             "tiers": {"Bow": [2, 5]}, "donor": {"Bow": "elf_bow"}},
            {"id": "ithilien", "folder": "gondor", "files": ["gondor"], "prefixes": ["gondor_ith_"],
             "ranks": {"overall": 1, "damage": 1, "accuracy": 1}, "tiers": {"Bow": [7]}, "donor": {"Bow": "ith_bow"}},
        ],
    }


def _loc(rows, extra_before="", extra_after=""):
    body = "".join(f'    <string id="{i}" text="{t}" />{EOL}' for i, t in rows)
    return (f"<?xml version=\"1.0\" encoding=\"utf-8\"?>{EOL}<base type=\"string\">{EOL}  <strings>{EOL}"
            f"{extra_before}{body}{extra_after}  </strings>{EOL}</base>{EOL}")


class SyncTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.md = self.modules / "LOTRLOME_Armory" / "ModuleData"
        (self.md / "Languages" / "DE").mkdir(parents=True)
        (self.md / "Languages" / "JP").mkdir(parents=True)
        self.de_elf = self.md / "Languages" / "DE" / "loc_elf.xml"
        self.de_elf.write_bytes(_loc(
            [("ladder_elf_bow_e", "[Elb] Langbogen I"), ("ladder_elf_bow_v", "[Elb] Langbogen III"),
             ("ladder_elf_bow_c", "[Elb] Langbogen V")],
            extra_before=f'    <string id="other_key" text="Andere" />{EOL}',
            extra_after=f'    <string id="tail_key" text="Ende &amp; mehr" />{EOL}').encode("utf-8"))
        self.jp_gondor = self.md / "Languages" / "JP" / "loc_gondor.xml"
        self.jp_gondor.write_bytes(_loc([("ladder_gondor_special_bow_x", "イシリエンの弓 IV")]).encode("utf-8"))
        self.spec = root / "spec.json"
        self.spec.write_text(json.dumps(_spec()), encoding="utf-8")
        # The translator's cache: rebuild_translation_files.py resolves override, then cache, then
        # English, so a cache still keyed on the retired ids rebuilds every carried name in English.
        self.cache = root / "cache"
        self.cache.mkdir()
        (self.cache / "de.json").write_text(json.dumps(
            {"ladder_elf_bow_e": "[Elb] Langbogen I", "ladder_elf_bow_v": "[Elb] Langbogen III",
             "ladder_elf_bow_c": "[Elb] Langbogen V", "other_key": "Andere"}), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return st.main(["--game-modules", str(self.modules), "--asset-repo", str(self.modules / "none"),
                        "--spec", str(self.spec), "--cache-dir", str(self.cache), *extra])

    def _cache(self, lang):
        return json.loads((self.cache / f"{lang}.json").read_text(encoding="utf-8"))

    def test_apply_keeps_the_translation_cache_in_step(self):
        self.assertEqual(self._run("--apply"), 0)
        de = self._cache("de")
        self.assertEqual(de["ladder_elf_bow_t2"], "[Elb] Langbogen II")
        self.assertEqual(de["ladder_elf_bow_t5"], "[Elb] Langbogen V")
        self.assertNotIn("ladder_elf_bow_e", de)                     # retired ids leave the cache
        self.assertEqual(de["other_key"], "Andere")                  # everything else is kept
        self.assertEqual(self._cache("jp")["ladder_ithilien_bow_t7"], "イシリエンの弓 VII")
        self.assertEqual(self._run("--verify"), 0)
        de.pop("ladder_elf_bow_t2")
        (self.cache / "de.json").write_text(json.dumps(de), encoding="utf-8")
        self.assertEqual(self._run("--verify"), 1)                   # a cache out of step is drift

    def test_apply_backs_up_each_live_file_once(self):
        original = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        backup = self.de_elf.with_name(self.de_elf.name + st.BACKUP_SUFFIX)
        self.assertEqual(backup.read_bytes(), original)
        self.assertFalse(backup.name.endswith(".xml"))               # never globbed by the engine
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(backup.read_bytes(), original)

    def test_a_ladder_row_the_tool_cannot_parse_is_refused(self):
        # Second review (#617): a row written text-first was kept as an ordinary line, so the
        # current id was written twice and --verify still passed.
        self.de_elf.write_bytes(self.de_elf.read_bytes().replace(
            b'    <string id="tail_key"', b'    <string text="[Elb] Langbogen V" id="ladder_elf_bow_t5" />\r\r\n    <string id="tail_key"'))
        before = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)

    def test_dry_run_writes_nothing(self):
        before, cache = self.de_elf.read_bytes(), (self.cache / "de.json").read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual(self.de_elf.read_bytes(), before)
        self.assertEqual((self.cache / "de.json").read_bytes(), cache)      # the cache too
        self.assertFalse((self.cache / "jp.json").exists())

    def test_apply_moves_the_translated_base_to_the_tier_ids_in_place(self):
        self.assertEqual(self._run("--apply"), 0)
        raw = self.de_elf.read_bytes()
        text = raw.decode("utf-8")
        self.assertEqual(text.count("\r\r\n"), text.count("\n"))        # every line keeps its doubled CR
        self.assertNotIn("ladder_elf_bow_e", text)                       # retired rows gone
        self.assertIn('<string id="ladder_elf_bow_t2" text="[Elb] Langbogen II" />', text)   # T2 is band E
        self.assertIn('<string id="ladder_elf_bow_t5" text="[Elb] Langbogen V" />', text)    # T5 is band V
        self.assertLess(text.index("other_key"), text.index("ladder_elf_bow_t2"))
        self.assertLess(text.index("ladder_elf_bow_t5"), text.index("tail_key"))
        self.assertIn("Ende &amp; mehr", text)                            # other rows byte-identical
        jp = self.jp_gondor.read_bytes().decode("utf-8")
        self.assertIn('<string id="ladder_ithilien_bow_t7" text="イシリエンの弓 VII" />', jp)  # renamed line
        self.assertNotIn("gondor_special", jp)

    def test_apply_is_idempotent_and_verify_sees_drift(self):
        self.assertEqual(self._run("--verify"), 1)
        self.assertEqual(self._run("--apply"), 0)
        first = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self.de_elf.read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)

    def test_a_row_that_cannot_be_derived_writes_nothing(self):
        self.jp_gondor.write_bytes(_loc([("ladder_gondor_special_bow_e", "弓 I")]).encode("utf-8"))  # no X band row
        before = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)

    def test_a_spec_that_contradicts_itself_or_cannot_be_read_is_refused(self):
        # Second review (#617): the tool planned items from an unvalidated spec, and an unreadable
        # spec was a traceback rather than a refusal.
        bad = _spec()
        bad["lines"][0]["ranks"]["overall"] = 0
        self.spec.write_text(json.dumps(bad), encoding="utf-8")
        before = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)
        self.spec.write_text("{ not json", encoding="utf-8")
        self.assertEqual(self._run("--verify"), 2)

    def test_a_cache_the_tool_cannot_read_is_refused(self):
        # Fix-diff review (#617): a corrupt or non-object cache was a traceback after the loc files
        # were planned, and a mistyped --cache-dir planned a fresh cache folder with exit 0.
        before = self.de_elf.read_bytes()
        (self.cache / "de.json").write_text("{ not json", encoding="utf-8")
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)
        (self.cache / "de.json").write_text("[1, 2]", encoding="utf-8")
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)
        self.assertEqual(st.main(["--game-modules", str(self.modules), "--asset-repo", str(self.modules / "none"),
                                  "--spec", str(self.spec), "--cache-dir", str(self.cache / "nope"), "--apply"]), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)
        self.assertFalse((self.cache / "nope").exists())

    def test_the_fixture_spec_is_valid(self):
        self.assertEqual(rl.validate_spec(_spec()), [])

    def test_translated_base_strips_only_a_separate_numeral(self):
        self.assertEqual(st.translated_base("[Elb] Langbogen IV"), "[Elb] Langbogen")
        with self.assertRaises(st.SyncError):
            st.translated_base("Bogen der Rohirrim")          # no numeral: never eat a trailing letter

    def test_retired_id_maps_the_split_gondor_lines(self):
        item = rl.LadderItem("ladder_blackroot_bow_t3", "blackroot", "Bow", 3, "R", 1, 1, 1, "d", "gondor")
        self.assertEqual(st.retired_id(item), "ladder_gondor_special_bow_r")


if __name__ == "__main__":
    unittest.main()
