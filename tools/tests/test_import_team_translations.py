#!/usr/bin/env python3
"""Unit tests for tools/import_team_translations.py (#1385).

Run:  python -m unittest discover -s tools/tests -t .

A synthetic team drop (a zip with one top folder, as the Chinese team ships it) and a synthetic pair of
our files: English source and language file. Each test breaks one rule the importer keeps.
"""
import json
import os
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import import_team_translations as imp  # noqa: E402

ENGLISH = """<?xml version="1.0" encoding="utf-8"?>
<strings>
  <string id="taom_hello" text="{=taom_hello}Hello {NAME}." />
  <string id="taom_plain" text="{=taom_plain}Plain" />
  <string id="taom_same" text="{=taom_same}Same" />
  <string id="taom_untranslated" text="{=taom_untranslated}Still English" />
  <string id="realms_held" text="{=realms_held}Held" />
  <string id="taom_runtime_only" text="{=taom_runtime_only}Runtime" />
  <string id="taom_override_only" text="{=taom_override_only}Override" />
</strings>
"""

OURS = """<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" type="string">
  <tags>
    <tag language="简体中文" />
  </tags>
  <strings>
    <string id="taom_hello" text="你好{NAME}" />
    <string id="taom_plain" text="旧" />
    <string id="taom_same" text="相同" />
    <string id="taom_untranslated" text="Still English" />
    <string id="realms_held" text="旧" />
    <string id="taom_runtime_only" text="旧" />
    <string id="taom_override_only" text="旧" />
  </strings>
</base>
"""


def team_language_file(rows):
    body = "".join(f'<string id="{k}" text="{v}" />' for k, v in rows.items())
    return f'<?xml version="1.0" encoding="utf-8"?><base type="string"><strings>{body}</strings></base>'


TEAM_ROWS = {
    "taom_hello": "你好，{NAME}。",     # good: placeholders kept
    "taom_plain": "新的",              # good
    "taom_same": "相同",               # already equal
    "realms_held": "新",               # skipped by prefix
    "taom_theirs_only": "多余",         # no row of ours
}
TEAM_BAD = {"taom_hello": "你好。"}     # drops {NAME}


def make_drop(folder: Path, rows=None, extra=None) -> Path:
    path = folder / "TAOM_CN.zip"
    with zipfile.ZipFile(path, "w") as z:
        z.writestr("TAOM_CN/ModuleData/Languages/CNs/a_key.xml", team_language_file(rows or TEAM_ROWS))
        z.writestr("TAOM_CN/ModuleData/Languages/CNs/language_data.xml", "<LanguageData />")
        z.writestr("TAOM_CN/ModuleData/RuntimeTranslations.xml",
                   '<translations><method token="1">'
                   '<string source="x" text="{=taom_runtime_only}运行时" />'
                   '<string source="y" text="{=taom_plain}运行时不该赢" /></method></translations>')
        z.writestr("TAOM_CN/Overrides/TAOM/ModuleData/thing.json",
                   json.dumps({"a": [{"name": "{=taom_override_only}覆盖"}, "{=!}ignored"]}, ensure_ascii=False))
        for name, text in (extra or {}).items():
            z.writestr(name, text)
    return path


class ImportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = Path(self.tmp.name)
        (root / "src").mkdir()
        (root / "Languages" / "CNs").mkdir(parents=True)
        (root / "src" / "taom_module_strings.xml").write_text(ENGLISH, encoding="utf-8")
        self.ours = root / "Languages" / "CNs" / "std_taom_module_strings_zho-CN.xml"
        self.ours.write_bytes(OURS.encode("utf-8"))
        self.root = root
        self.cache = root / "cache"
        self.cache.mkdir()

    def tearDown(self):
        self.tmp.cleanup()

    def targets(self):
        return imp.taom_targets("CNs", lang_root=self.root / "Languages", source_root=self.root / "src")

    def plan(self, drop_rows=None, skip=("realms_",), overrides=None):
        drop = imp.Drop(make_drop(self.root, drop_rows))
        team = imp.team_rows(drop, "CNs", [])
        return imp.make_plan(team, self.targets(), overrides or {}, skip)

    def test_good_rows_are_planned_and_equal_rows_are_counted(self):
        plan = self.plan()
        rows = plan.writes[self.ours]
        self.assertEqual("你好，{NAME}。", rows["taom_hello"])
        self.assertEqual("新的", rows["taom_plain"])
        self.assertEqual(1, plan.already_equal)
        self.assertNotIn("taom_same", rows)

    def test_language_rows_beat_runtime_rows_and_runtime_and_overrides_fill_gaps(self):
        rows = self.plan().writes[self.ours]
        self.assertEqual("新的", rows["taom_plain"])
        self.assertEqual("运行时", rows["taom_runtime_only"])
        self.assertEqual("覆盖", rows["taom_override_only"])

    def test_placeholder_mismatch_is_rejected_not_written(self):
        plan = self.plan(drop_rows=TEAM_BAD)
        self.assertNotIn("taom_hello", plan.writes.get(self.ours, {}))
        self.assertEqual(["taom_hello"], [r["id"] for r in plan.rejected])

    def test_skipped_prefix_is_left_alone(self):
        plan = self.plan()
        self.assertNotIn("realms_held", plan.writes[self.ours])
        self.assertEqual(["realms_held"], plan.skipped_prefix)

    def test_unknown_id_is_never_added_and_is_reported(self):
        plan = self.plan()
        self.assertNotIn("taom_theirs_only", plan.writes[self.ours])
        self.assertIn("taom_theirs_only", plan.theirs_only)

    def test_our_untranslated_row_they_lack_is_reported(self):
        self.assertEqual(["taom_untranslated"], self.plan().ours_untranslated_they_lack)

    def test_english_text_in_the_drop_does_not_replace_a_translation(self):
        plan = self.plan(drop_rows={"taom_plain": "Plain"})
        self.assertNotIn("taom_plain", plan.writes.get(self.ours, {}))
        self.assertEqual(["taom_plain"], plan.english_in_drop)

    def test_override_that_differs_is_reported(self):
        plan = self.plan(overrides={"taom_plain": "钦定"})
        self.assertEqual([{"id": "taom_plain", "override": "钦定", "theirs": "新的"}], plan.override_conflicts)

    def test_apply_writes_the_row_keeps_the_tag_and_fills_the_cache_and_a_second_run_is_idempotent(self):
        plan = self.plan()
        imp.apply_plan(plan, "CNs", self.targets(), cache_dir=self.cache)
        text = self.ours.read_text(encoding="utf-8")
        self.assertIn('<string id="taom_plain" text="新的" />', text)
        self.assertIn('<string id="taom_hello" text="你好，{NAME}。" />', text)
        self.assertIn('<tag language="简体中文" />', text)
        self.assertIn('<string id="realms_held" text="旧" />', text)
        cache = json.loads((self.cache / "cns.json").read_text(encoding="utf-8"))
        self.assertEqual("新的", cache["taom_plain"])
        self.assertEqual("相同", cache["taom_same"])
        self.assertNotIn("taom_theirs_only", cache)

        again = self.plan()
        self.assertEqual({}, again.writes)
        self.assertEqual(again.already_equal, len(again.cache))

    def test_a_folder_drop_reads_like_the_zip(self):
        folder = self.root / "unzipped"
        with zipfile.ZipFile(make_drop(self.root)) as z:
            z.extractall(folder)
        from_folder = imp.team_rows(imp.Drop(folder), "CNs", [])
        from_zip = imp.team_rows(imp.Drop(make_drop(self.root)), "CNs", [])
        self.assertEqual({k: v[0] for k, v in from_zip.items()}, {k: v[0] for k, v in from_folder.items()})

    def test_a_drop_file_that_does_not_parse_is_reported_and_the_rest_still_read(self):
        drop = imp.Drop(make_drop(self.root, extra={"TAOM_CN/ModuleData/Languages/CNs/broken.xml": "<base"}))
        problems = []
        team = imp.team_rows(drop, "CNs", problems)
        self.assertEqual(1, len(problems))
        self.assertIn("taom_plain", team)


if __name__ == "__main__":
    unittest.main()
