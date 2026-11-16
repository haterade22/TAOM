#!/usr/bin/env python3
"""Unit tests for tools/import_team_translations.py (#1385).

Run:  python -m unittest discover -s tools/tests -t .

A synthetic team drop (a zip with one top folder, as the Chinese team ships it) and a synthetic pair of
our files: English source and language file. Each test breaks one rule the importer keeps.
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import import_team_translations as imp  # noqa: E402

ENGLISH = """<?xml version="1.0" encoding="utf-8"?>
<strings>
  <string id="taom_hello" text="{=taom_hello}Hello {NAME}." />
  <string id="taom_plain" text="{=taom_plain}Plain" />
  <string id="taom_same" text="{=taom_same}Same" />
  <string id="taom_untranslated" text="{=taom_untranslated}Still English" />
  <string id="taom_drafted" text="{=taom_drafted}Drafted" />
  <string id="realms_held" text="{=realms_held}Held" />
  <string id="taom_stale" text="{=taom_stale}New wording" />
  <string id="taom_new" text="{=taom_new}Added later" />
  <string id="aom_rohan_clan_name_01" text="{=aom_rohan_clan_name_01}Harolding" />
</strings>
"""

# Our English at the drop's base version: taom_stale had other words, taom_new did not exist.
ENGLISH_AT_BASE = {
    "taom_hello": "Hello {NAME}.", "taom_plain": "Plain", "taom_same": "Same",
    "taom_untranslated": "Still English", "taom_drafted": "Drafted", "realms_held": "Held",
    "taom_stale": "Old wording", "aom_rohan_clan_name_01": "Harolding",
}

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
    <string id="taom_drafted" text="草稿" />
    <string id="realms_held" text="旧" />
    <string id="taom_stale" text="新措辞" />
    <string id="taom_new" text="旧" />
    <string id="aom_rohan_clan_name_01" text="哈罗丁家族" />
    <string id="taom_orphan" text="孤" />
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
    "taom_stale": "旧措辞",             # translates the English we have since changed
    "taom_new": "后加的",               # an id our English gained after the base version
    "aom_rohan_clan_name_01": "埃奥尔一族",  # another house: KEEP_OURS
    "taom_orphan": "孤儿",              # our row has no English source
    "taom_theirs_only": "多余",         # no row of ours
}
TEAM_BAD = {"taom_hello": "你好。"}     # drops {NAME}

SUBMODULE = """<?xml version='1.0' encoding='utf-8'?>
<Module><Id value="TAOM_CN" /><DependedModuleMetadatas>
<DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.5.7.*" />
<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" version="v2.0.38" />
</DependedModuleMetadatas></Module>"""


def make_drop(folder: Path, rows=None, extra=None, root="TAOM_CN/", name="TAOM_CN.zip") -> Path:
    path = folder / name
    with zipfile.ZipFile(path, "w") as z:
        if f"{root}SubModule.xml" not in (extra or {}):
            z.writestr(f"{root}SubModule.xml", SUBMODULE)
        z.writestr(f"{root}ModuleData/Languages/CNs/a_key.xml", team_language_file(rows or TEAM_ROWS))
        z.writestr(f"{root}ModuleData/Languages/CNs/language_data.xml", "<LanguageData />")
        # Their runtime and override copies are not language rows: the importer must not read them.
        z.writestr(f"{root}ModuleData/RuntimeTranslations.xml",
                   '<translations><method token="1">'
                   '<string source="x" text="{=taom_drafted}运行时" /></method></translations>')
        z.writestr(f"{root}Overrides/TAOM/ModuleData/thing.json",
                   json.dumps({"a": [{"name": "{=taom_untranslated}覆盖"}]}, ensure_ascii=False))
        for member, text in (extra or {}).items():
            z.writestr(member, text)
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

    def targets(self, base=lambda src: ENGLISH_AT_BASE):
        return imp.taom_targets("CNs", lang_root=self.root / "Languages", source_root=self.root / "src", base=base)

    def plan(self, drop_rows=None, skip=("realms_",), overrides=None, targets=None):
        drop = imp.Drop(make_drop(self.root, drop_rows))
        team = imp.team_rows(drop, "CNs", [])
        return imp.make_plan(team, targets if targets is not None else self.targets(), overrides or {}, skip)

    # ── What is taken ─────────────────────────────────────────────────────────

    def test_good_rows_are_planned_and_equal_rows_are_counted(self):
        plan = self.plan()
        rows = plan.writes[self.ours]
        self.assertEqual("你好，{NAME}。", rows["taom_hello"])
        self.assertEqual("新的", rows["taom_plain"])
        self.assertEqual(1, plan.already_equal)
        self.assertNotIn("taom_same", rows)

    def test_only_the_language_files_are_read(self):
        team = imp.team_rows(imp.Drop(make_drop(self.root)), "CNs", [])
        self.assertNotIn("taom_drafted", team)
        self.assertNotIn("taom_untranslated", team)

    def test_placeholder_mismatch_is_rejected_not_written(self):
        plan = self.plan(drop_rows=TEAM_BAD)
        self.assertNotIn("taom_hello", plan.writes.get(self.ours, {}))
        self.assertEqual(["taom_hello"], [r["id"] for r in plan.rejected])

    def test_text_an_attribute_cannot_carry_is_rejected_not_written(self):
        # XML can only bring a tab or a line break, as a character reference: written raw, it reads back as a space.
        plan = self.plan(drop_rows={"taom_plain": "新&#9;", "taom_hello": "你好{NAME}&#10;"})
        self.assertEqual(["taom_hello", "taom_plain"], [r["id"] for r in plan.rejected])
        self.assertEqual({}, plan.writes)
        self.assertNotIn("taom_plain", plan.cache)

    def test_a_line_break_is_rejected_even_when_our_english_has_one(self):
        # write_back escapes only & " < >: a raw line break in an attribute reads back as a space.
        target = self.live_target({"x": "A\nB"}, {"x": "旧"}, tiers=False)
        plan = imp.make_plan({"x": ("甲\n乙", "t")}, [target], {}, ())
        self.assertEqual(["x"], [r["id"] for r in plan.rejected])

    def test_a_brace_token_with_a_non_ascii_character_is_rejected(self):
        # The engine's tokenizer reads no such character and shows the whole string empty (Tokenizer.FindTokenMatchesAndText).
        plan = self.plan(drop_rows={"taom_hello": "你好{NAME}{玩家}"})
        self.assertEqual(["taom_hello"], [r["id"] for r in plan.rejected])
        self.assertNotIn("taom_hello", plan.writes.get(self.ours, {}))

    def test_expressions_are_cut_where_their_braces_balance(self):
        # Text between expressions and whitespace inside one are readable; nested braces stay one expression.
        self.assertEqual([], imp.unreadable_expressions("{?IS_FEMALE}她{?}他{\\?}，{ NAME }{　NAME}"))
        self.assertEqual(["{？}", "{A{玩家}}"], imp.unreadable_expressions("{？}她{A{玩家}}{B}"))
        self.assertEqual(["{未闭合"], imp.unreadable_expressions("名字{未闭合"))

    def test_skipped_prefix_is_left_alone(self):
        plan = self.plan()
        self.assertNotIn("realms_held", plan.writes[self.ours])
        self.assertEqual(["realms_held"], plan.skipped_prefix)

    def test_unknown_id_is_never_added_and_is_reported(self):
        plan = self.plan()
        self.assertNotIn("taom_theirs_only", plan.writes[self.ours])
        self.assertIn("taom_theirs_only", plan.theirs_only)

    def test_our_rows_they_lack_are_reported_untranslated_and_drafted_apart(self):
        plan = self.plan()
        self.assertEqual(["taom_untranslated"], plan.ours_untranslated_they_lack)
        self.assertEqual(["taom_drafted"], plan.ours_drafted_they_lack)

    def test_english_text_in_the_drop_does_not_replace_a_translation(self):
        plan = self.plan(drop_rows={"taom_plain": "Plain"})
        self.assertNotIn("taom_plain", plan.writes.get(self.ours, {}))
        self.assertEqual(["taom_plain"], plan.english_in_drop)

    def test_blank_team_text_does_not_replace_a_translation(self):
        plan = self.plan(drop_rows={"taom_plain": "  "})
        self.assertEqual({}, plan.writes)
        self.assertEqual(["taom_plain"], plan.english_in_drop)

    def test_an_orphan_row_is_listed_not_written(self):
        plan = self.plan()
        self.assertNotIn("taom_orphan", plan.writes[self.ours])
        self.assertEqual(["taom_orphan"], plan.orphans)

    # ── Our decisions win ─────────────────────────────────────────────────────

    def test_override_always_wins_and_a_difference_is_reported(self):
        # The file takes the override, the text every other tool resolves for the id.
        plan = self.plan(overrides={"taom_plain": "钦定", "taom_hello": "你好，{NAME}。", "taom_stale": "新措辞"})
        self.assertEqual("钦定", plan.writes[self.ours]["taom_plain"])
        self.assertEqual("你好，{NAME}。", plan.writes[self.ours]["taom_hello"])
        self.assertNotIn("taom_stale", plan.writes[self.ours])  # the override is the row already
        self.assertEqual([], plan.stale)
        self.assertEqual([{"id": "taom_plain", "override": "钦定", "theirs": "新的"},
                          {"id": "taom_stale", "override": "新措辞", "theirs": "旧措辞"}], plan.override_conflicts)

    def test_an_id_we_keep_is_never_written_and_says_why(self):
        plan = self.plan()
        self.assertNotIn("aom_rohan_clan_name_01", plan.writes[self.ours])
        self.assertNotIn("aom_rohan_clan_name_01", plan.cache)
        self.assertEqual(["aom_rohan_clan_name_01"], [k["id"] for k in plan.kept_ours])
        self.assertTrue(plan.kept_ours[0]["reason"])

    # ── Staleness against the drop's base version ─────────────────────────────

    def test_a_row_whose_english_changed_since_the_base_is_reported_not_written(self):
        plan = self.plan()
        self.assertNotIn("taom_stale", plan.writes[self.ours])
        self.assertNotIn("taom_stale", plan.cache)
        self.assertEqual([{"id": "taom_stale", "file": self.ours.name, "english_then": "Old wording",
                           "english_now": "New wording", "theirs": "旧措辞"}], plan.stale)

    def test_an_id_new_since_the_base_is_taken_and_listed(self):
        plan = self.plan()
        self.assertEqual("后加的", plan.writes[self.ours]["taom_new"])
        self.assertEqual(["taom_new"], plan.new_since_base)

    def test_an_id_new_since_the_base_whose_english_changed_after_is_stale(self):
        # The team saw the id's first words; our English has changed since then.
        targets = imp.taom_targets("CNs", lang_root=self.root / "Languages", source_root=self.root / "src",
                                   base=lambda src: ENGLISH_AT_BASE, later=lambda src: {"taom_new": ["Added first"]})
        plan = self.plan(targets=targets)
        self.assertNotIn("taom_new", plan.writes[self.ours])
        self.assertNotIn("taom_new", plan.new_since_base)
        self.assertIn({"id": "taom_new", "file": self.ours.name, "english_then": "Added first",
                       "english_now": "Added later", "theirs": "后加的"}, plan.stale)

    def test_a_row_whose_english_changed_and_changed_back_is_stale(self):
        targets = imp.taom_targets("CNs", lang_root=self.root / "Languages", source_root=self.root / "src",
                                   base=lambda src: ENGLISH_AT_BASE, later=lambda src: {"taom_plain": ["Other", "Plain"]})
        plan = self.plan(targets=targets)
        self.assertNotIn("taom_plain", plan.writes[self.ours])
        self.assertIn("taom_plain", [r["id"] for r in plan.stale])

    def test_english_after_a_ref_lists_each_text_of_the_later_commits(self):
        repo = self.root / "repo"
        source = repo / imp.ENGLISH_DIR / "taom_module_strings.xml"
        source.parent.mkdir(parents=True)

        def commit(rows):
            source.write_text("<strings>" + "".join(f'<string id="{k}" text="{{={k}}}{v}" />' for k, v in rows.items())
                              + "</strings>", encoding="utf-8")
            subprocess.run(["git", "-C", str(repo), "add", "-A"], check=True, capture_output=True)
            subprocess.run(["git", "-C", str(repo), "-c", "user.name=t", "-c", "user.email=t@example.invalid",
                            "commit", "-q", "-m", "x"], check=True, capture_output=True)
            return subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"], check=True, capture_output=True,
                                  text=True).stdout.strip()

        subprocess.run(["git", "init", "-q", str(repo)], check=True, capture_output=True)
        base = commit({"taom_a": "A"})
        commit({"taom_a": "A", "taom_b": "B1"})
        commit({"taom_a": "A2", "taom_b": "B2"})
        with mock.patch.object(imp, "REPO_ROOT", repo):
            read = imp.english_after(base)
            self.assertEqual({"taom_a": ["A", "A2"], "taom_b": ["B1", "B2"]}, read("taom_module_strings.xml"))
            self.assertEqual({}, read("no_such_source_strings.xml"))

    def test_a_target_with_no_history_is_taken_and_counted_unchecked(self):
        plan = self.plan(targets=self.targets(base=None))
        self.assertEqual("旧措辞", plan.writes[self.ours]["taom_stale"])
        self.assertEqual(len(plan.writes[self.ours]), plan.unchecked)

    def test_the_base_version_is_the_taom_version_the_drop_depends_on(self):
        self.assertEqual("v2.0.38", imp.base_version(imp.Drop(make_drop(self.root))))

    def test_a_drop_that_names_no_taom_version_has_no_base(self):
        drop = imp.Drop(make_drop(self.root, extra={"TAOM_CN/SubModule.xml": "<Module />"}))
        self.assertIsNone(imp.base_version(drop))

    def test_our_english_at_a_git_ref_is_read_and_a_missing_file_is_empty(self):
        read = imp.english_at("HEAD")
        self.assertIn("taom_realm_borders_entering", read("taom_module_strings.xml"))
        self.assertEqual({}, read("no_such_source_strings.xml"))

    # ── Names a player must tell apart ────────────────────────────────────────

    def live_target(self, english, current, tiers):
        path = self.root / "Languages" / "CNs" / "loc_items.xml"
        rows = "".join(f'<string id="{k}" text="{v}" />' for k, v in current.items())
        path.write_text(f'<?xml version="1.0" encoding="utf-8"?><base><strings>{rows}</strings></base>',
                        encoding="utf-8")
        return imp.Target(path, english, dict(current), live=True, distinct_names=True, tiers=tiers)

    def test_an_item_name_gets_the_english_tier_the_team_left_out(self):
        english = {"helm_1": "[Gundabad] Heavy Bracer I", "helm_2": "[Gundabad] Heavy Bracer II",
                   "spear": "Rivendell Spear Handle I"}
        target = self.live_target(english, {"helm_1": "旧 I", "helm_2": "旧 II", "spear": "旧"}, tiers=True)
        team = {"helm_1": ("[贡达巴德]重型护腕", "t"), "helm_2": ("[贡达巴德]重型护腕 II", "t"),
                "spear": ("幽谷矛 I 矛柄", "t")}
        plan = imp.make_plan(team, [target], {}, ())
        self.assertEqual({"helm_1": "[贡达巴德]重型护腕 I", "helm_2": "[贡达巴德]重型护腕 II", "spear": "幽谷矛 I 矛柄"},
                         plan.writes[target.path])
        self.assertEqual(["helm_1"], plan.tier_added)

    def test_names_the_import_would_merge_keep_ours(self):
        english = {"foot": "[Gondor] Anfalas Footman", "inf": "[Gondor] Anfalas Infantry",
                   "archer": "[Gondor] Anfalas Archer"}
        target = self.live_target(english, {"foot": "安法拉斯步卒", "inf": "安法拉斯步兵", "archer": "旧"},
                                  tiers=False)
        team = {"foot": ("安法拉斯步兵", "t"), "inf": ("安法拉斯步兵", "t"), "archer": ("安法拉斯弓手", "t")}
        plan = imp.make_plan(team, [target], {}, ())
        self.assertEqual({"archer": "安法拉斯弓手"}, plan.writes[target.path])
        self.assertEqual(["foot"], [c["id"] for c in plan.collisions])
        self.assertNotIn("foot", plan.cache)

    def test_lord_names_are_names_a_player_must_tell_apart(self):
        # The first re-import gave Amandir (aom_lord_EW_1_name) the Chinese name of Vorondir.
        src_name, template = next((s, t) for s, t in imp.TAOM_SOURCES if s == "taom_lord_name_strings.xml")
        (self.root / "src" / src_name).write_text(ENGLISH, encoding="utf-8")
        (self.root / "Languages" / "CNs" / template.format(locale=imp.LANGUAGES["CNs"][0])).write_bytes(
            OURS.encode("utf-8"))
        lords = [t for t in self.targets() if t.path.name.startswith("std_taom_lord_name_strings")]
        self.assertEqual([True], [t.distinct_names for t in lords])

    def test_a_collision_that_appears_only_after_a_row_goes_back_is_kept_ours_too(self):
        english = {"a": "Amandir", "b": "Vorondir", "c": "Celebrin"}
        target = self.live_target(english, {"a": "阿", "b": "沃", "c": "凯"}, tiers=False)
        # a and b clash on 新; a then returns to 阿, which the team gave c.
        team = {"a": ("新", "t"), "b": ("新", "t"), "c": ("阿", "t")}
        plan = imp.make_plan(team, [target], {}, ())
        self.assertEqual(["a", "b", "c"], sorted(c["id"] for c in plan.collisions))
        self.assertNotIn(target.path, plan.writes)

    def test_a_tier_with_a_track_or_a_digit_is_appended_once(self):
        self.assertEqual("[黑森林]步兵盔 II-T1", imp.with_tier("Infantry Helmet II-T1", "[黑森林]步兵盔"))
        self.assertEqual("矛 3", imp.with_tier("Spear 3", "矛"))
        self.assertEqual("步兵盔 II-T1", imp.with_tier("Infantry Helmet II-T1", "步兵盔 II-T1"))

    def test_plain_text_may_share_a_translation(self):
        plan = self.plan(drop_rows={"taom_plain": "相同", "taom_same": "相同"})
        self.assertEqual("相同", plan.writes[self.ours]["taom_plain"])
        self.assertEqual([], plan.collisions)

    # ── Apply ─────────────────────────────────────────────────────────────────

    def test_apply_writes_the_row_keeps_the_tag_bom_and_crlf_and_fills_the_cache_and_a_second_run_is_idempotent(self):
        self.ours.write_bytes(b"\xef\xbb\xbf" + OURS.replace("\n", "\r\n").encode("utf-8"))
        plan = self.plan()
        imp.apply_plan(plan, "CNs", self.targets(), cache_dir=self.cache)
        raw = self.ours.read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(raw.count(b"\n"), raw.count(b"\r\n"))
        text = raw.decode("utf-8-sig")
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

    def test_a_live_target_is_backed_up_before_the_write(self):
        before = self.ours.read_bytes()
        live = [imp.Target(t.path, t.english, t.current, live=True, english_then=t.english_then)
                for t in self.targets()]
        plan = self.plan(targets=live)
        backups = imp.apply_plan(plan, "CNs", live, cache_dir=self.cache)
        self.assertEqual(1, len(backups))
        self.assertEqual(before, Path(backups[0]).read_bytes())
        self.assertNotEqual(before, self.ours.read_bytes())

    def test_summary_lists_the_rows_each_file_gets(self):
        entry = imp.summary("CNs", self.plan())
        self.assertEqual({str(self.ours): ["taom_hello", "taom_new", "taom_plain"]}, entry["writes"])
        self.assertEqual(["taom_orphan"], entry["orphans"])

    # ── The drop ──────────────────────────────────────────────────────────────

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

    def test_two_different_texts_for_one_id_are_a_problem_and_neither_is_taken(self):
        second = team_language_file({"taom_plain": "另一个"})
        drop = imp.Drop(make_drop(self.root, extra={"TAOM_CN/ModuleData/Languages/CNs/b_key.xml": second}))
        problems = []
        team = imp.team_rows(drop, "CNs", problems)
        self.assertNotIn("taom_plain", team)
        self.assertEqual(1, len(problems))
        self.assertIn("taom_plain", problems[0])

    def test_a_zip_with_extra_root_files_and_mac_folders_still_reads(self):
        drop = imp.Drop(make_drop(self.root, extra={"README.txt": "hi", "__MACOSX/TAOM_CN/._a": "x"}))
        self.assertIn("taom_plain", imp.team_rows(drop, "CNs", []))
        # A Mac zip of the module's contents puts ModuleData at the root and mirrors it under __MACOSX/.
        flat = imp.Drop(make_drop(self.root, root="", name="flat.zip",
                                  extra={"__MACOSX/ModuleData/Languages/CNs/._a_key.xml": "x"}))
        self.assertIn("taom_plain", imp.team_rows(flat, "CNs", []))

    def test_lowercase_folders_read(self):
        drop = imp.Drop(make_drop(self.root, root="taom_cn/", extra={}))
        lower = self.root / "lower.zip"
        with zipfile.ZipFile(lower, "w") as z:
            z.writestr("TAOM_CN/moduledata/languages/CNs/a.xml", team_language_file({"taom_plain": "新的"}))
        self.assertIn("taom_plain", imp.team_rows(drop, "CNs", []))
        self.assertIn("taom_plain", imp.team_rows(imp.Drop(lower), "CNs", []))

    def test_a_zip_with_no_module_folder_stops(self):
        bare = self.root / "bare.zip"
        with zipfile.ZipFile(bare, "w") as z:
            z.writestr("TAOM_CN.zip", "a zip inside the zip")
        with self.assertRaises(SystemExit):
            imp.Drop(bare)

    def test_an_unparseable_file_of_ours_stops(self):
        self.ours.write_text("<base", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.targets()

    # ── main ──────────────────────────────────────────────────────────────────

    def run_main(self, *extra, rows=None, saved=None):
        drop = make_drop(self.root, rows)
        report = self.root / "report.json"
        saved = {} if saved is None else saved
        targets = self.targets()
        with mock.patch.object(imp, "taom_targets", lambda lang, base=None, later=None: targets), \
                mock.patch.object(imp, "load_overrides", lambda lang: {}), \
                mock.patch.object(imp, "load_cache", lambda lang, *a: {}), \
                mock.patch.object(imp, "save_cache", lambda lang, cache, *a: saved.update(cache)):
            code = imp.main(["--lang", "CNs", "--from", str(drop), "--base", "HEAD", "--report", str(report),
                             "--skip-prefix", "realms_", *extra])
        return code, json.loads(report.read_text(encoding="utf-8")), saved

    def test_main_reads_the_base_from_the_drop_and_passes_both_readers(self):
        drop, report, seen = make_drop(self.root), self.root / "report.json", {}
        targets = self.targets()

        def fake_targets(lang, base=None, later=None):
            seen["base"], seen["later"] = base, later
            return targets
        with mock.patch.object(imp, "english_at", lambda ref: ("at", ref)), \
                mock.patch.object(imp, "english_after", lambda ref: ("after", ref)), \
                mock.patch.object(imp, "ref_exists", lambda ref: True), \
                mock.patch.object(imp, "taom_targets", fake_targets), \
                mock.patch.object(imp, "load_overrides", lambda lang: {}):
            self.assertEqual(0, imp.main(["--lang", "CNs", "--from", str(drop), "--report", str(report)]))
        self.assertEqual({"base": ("at", "v2.0.38"), "later": ("after", "v2.0.38")}, seen)

    def test_main_stops_when_the_drop_names_no_taom_version_and_no_base_is_given(self):
        drop = make_drop(self.root, extra={"TAOM_CN/SubModule.xml": "<Module />"})
        with self.assertRaises(SystemExit):
            imp.main(["--lang", "CNs", "--from", str(drop), "--report", str(self.root / "report.json")])

    def test_a_written_file_that_does_not_parse_is_put_back(self):
        before = self.ours.read_bytes()
        plan = self.plan()

        def broken_write(path, rows, tag):
            path.write_text("<strings><string", encoding="utf-8")
            return len(rows), []
        with mock.patch.object(imp, "write_back", broken_write), self.assertRaises(SystemExit):
            imp.apply_plan(plan, "CNs", self.targets(), cache_dir=self.cache)
        self.assertEqual(before, self.ours.read_bytes())

    def test_main_without_apply_writes_only_the_report(self):
        before = self.ours.read_bytes()
        code, report, saved = self.run_main()
        self.assertEqual(0, code)
        self.assertEqual(before, self.ours.read_bytes())
        self.assertEqual({}, saved)
        self.assertEqual("HEAD", report["base"])
        self.assertEqual(3, report["languages"][0]["imported"])

    def test_a_repeated_lang_or_module_counts_once(self):
        _, once, _ = self.run_main()
        _, twice, _ = self.run_main("--lang", "CNs", "--module", "TAOM", "--module", "TAOM")
        self.assertEqual(1, len(twice["languages"]))
        self.assertEqual(once["languages"][0]["already_equal"], twice["languages"][0]["already_equal"])

    def test_a_drop_with_no_rows_for_the_language_returns_2(self):
        empty = self.root / "empty.zip"
        with zipfile.ZipFile(empty, "w") as z:
            z.writestr("TAOM_CN/SubModule.xml", SUBMODULE)
            z.writestr("TAOM_CN/ModuleData/Languages/CNt/a.xml", team_language_file({"taom_plain": "新"}))
        targets = self.targets()
        with mock.patch.object(imp, "taom_targets", lambda lang, base=None, later=None: targets), \
                mock.patch.object(imp, "load_overrides", lambda lang: {}):
            code = imp.main(["--lang", "CNs", "--from", str(empty), "--base", "HEAD",
                             "--report", str(self.root / "r2.json")])
        self.assertEqual(2, code)

    def test_a_failed_apply_still_writes_the_report_and_the_cache(self):
        self.ours.write_text(OURS.replace('<string id="taom_plain" text="旧" />',
                                          '<string text="旧" id="taom_plain" />'), encoding="utf-8")
        saved = {}
        with self.assertRaises(SystemExit):
            self.run_main("--apply", saved=saved)
        report = json.loads((self.root / "report.json").read_text(encoding="utf-8"))
        self.assertIn("error", report)
        self.assertEqual(1, len(report["languages"]))
        self.assertEqual("新的", saved["taom_plain"])


if __name__ == "__main__":
    unittest.main()
