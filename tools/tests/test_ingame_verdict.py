#!/usr/bin/env python3
"""Unit tests for the in-game verdict reader (tools/ingame_verdict.py) and its manifests.

Run:  python -m unittest tools.tests.test_ingame_verdict -v

Each test builds a throwaway game tree (and dedicated server folders) from the fixture logs in
fixtures/ingame_verdict/, sets the file times the session logic reads, and runs main() with a fake
runner, so no test calls the real git or gh. The fixture lines are scrubbed copies of real log
lines. The only [ServerSettings] lines on disk come from a .dirty build (a staged server log of
2026-10-23), which the shipped #981 manifest leaves NOT REACHED, and no "layer attached" or probe
install line is on disk, so those fixtures render the format strings of ServerSettingsReport.cs,
ServerSettingsReader.cs, HitchProbeLines.cs and BattleActionBarMissionView.cs. No career screen line is
on disk either, so the career_* fixtures render GauntletCareerScreen.cs, CareerScreenChecks.cs and
ViewModel_ExecuteCommand_CareerScreen_Patch.cs (CareerScreenChecksTests pins their [Check] lines).
"""
import io
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from datetime import datetime
from pathlib import Path
from unittest import mock

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS))

import ingame_verdict as iv  # noqa: E402

REPO = TOOLS.parent
FIX = Path(__file__).resolve().parent / "fixtures" / "ingame_verdict"
GOOD = "2434d62aa33776fae18839632e30781bcbbde5e4"
OLD = "cdd5fe38ccb31ab7873d0f83a7ca5f6d0f7a83b7"


class FakeRunner:
    """git merge-base answers 0 for a build sha in `ancestors`, else 1; gh answers canned output."""

    def __init__(self, ancestors=(GOOD,), git_rc=None, gh_list=(0, "[]"), post_rc=0):
        self.ancestors = set(ancestors)
        self.git_rc = git_rc
        self.gh_list = gh_list
        self.post_rc = post_rc
        self.calls = []

    def __call__(self, args, input=None):
        self.calls.append((list(args), input))
        if args[0] == "git":
            if self.git_rc is not None:
                return self.git_rc, ""
            return (0 if args[-1] in self.ancestors else 1), ""
        if args[:3] == ["gh", "issue", "list"]:
            return self.gh_list
        if args[:3] == ["gh", "issue", "comment"]:
            return self.post_rc, ""
        raise AssertionError(f"unexpected command {args}")

    def gh_calls(self):
        return [c for c in self.calls if c[0][0] == "gh"]


def stamp_time(text):
    return time.mktime(datetime.strptime(text, "%Y-%m-%d %H:%M:%S").timetuple())


class Tree:
    """A throwaway game install, server data folder, server engine and rgl folder."""

    def __init__(self, tc):
        tmp = tempfile.TemporaryDirectory()
        tc.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        self.game = self.root / "game"
        self.client = self.game / "bin" / "Win64_Shipping_Client"
        self.ds_data = self.root / "ds-data"
        self.ds_engine = self.root / "ds-engine"
        self.rgl = self.root / "rgl"
        self.checks = self.root / "checks"
        for d in (self.client / "Logs", self.ds_data / "logs",
                  self.ds_engine / "bin" / "Win64_Shipping_Server" / "Logs", self.rgl, self.checks):
            d.mkdir(parents=True)

    def _put(self, fixture, dest, mtime):
        shutil.copyfile(FIX / fixture, dest)
        if mtime:
            t = stamp_time(mtime)
            os.utime(dest, (t, t))
        return dest

    def taom(self, fixture, name_stamp, mtime=None):
        return self._put(fixture, self.client / "Logs" / f"taom_debug_{name_stamp}.log", mtime)

    def ds_taom(self, fixture, name_stamp, mtime=None):
        logs = self.ds_engine / "bin" / "Win64_Shipping_Server" / "Logs"
        return self._put(fixture, logs / f"taom_debug_{name_stamp}.log", mtime)

    def ds(self, fixture, name_stamp, mtime):
        return self._put(fixture, self.ds_data / "logs" / f"coop-server-{name_stamp}.log", mtime)

    def coop_client(self, fixture, mtime, name="Coop_client.log"):
        return self._put(fixture, self.client / name, mtime)

    def rgl_log(self, fixture, name, mtime):
        return self._put(fixture, self.rgl / name, mtime)

    def manifest(self, name, source=FIX / "manifests"):
        shutil.copyfile(Path(source) / name, self.checks / name)

    def real_manifest(self, name):
        self.manifest(name, TOOLS / "ingame_checks")

    def run(self, argv, runner=None, ds=False):
        runner = runner or FakeRunner()
        out, err = io.StringIO(), io.StringIO()
        full = list(argv) + ["--game", str(self.game)]
        if ds:
            full += ["--ds-data", str(self.ds_data), "--ds-engine", str(self.ds_engine)]
        code = iv.main(full, runner=runner, out=out, err=err, checks_dir=self.checks,
                       rgl_dir=self.rgl, repo=REPO)
        return code, out.getvalue(), err.getvalue()


def verdict_of(out, check_id):
    m = re.search(r"^\s*(PASS|FAIL|NOT REACHED)\s+" + re.escape(check_id) + r"\b", out, re.M)
    return m.group(1) if m else None


class FormatTests(unittest.TestCase):
    def test_check_line_regex_is_the_shared_contract(self):
        self.assertEqual(iv.CHECK_LINE.pattern, r"\[Check\] id=(\S+) result=(pass|fail|skip)(.*)$")
        self.assertEqual(iv.CHECK_ID.pattern, r"^[a-z0-9]+(\.[a-z0-9-]+)+$")
        m = iv.CHECK_LINE.search("[2026-10-31 12:00:00] [INFO] [Check] id=taom.a-b.c result=pass field=True")
        self.assertEqual(m.groups(), ("taom.a-b.c", "pass", " field=True"))

    def test_build_stamp_reads_sha_and_dirty(self):
        line = (f"[2026-10-30 12:57:23] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261030-175608Z+{GOOD}.dirty "
                f"TAOM.Dependencies=v0.1.0.0 build.20261030-175606Z+{GOOD}.dirty (pair OK)")
        self.assertEqual(iv.parse_build_stamp(line), (GOOD, True))
        self.assertEqual(iv.parse_build_stamp(line.replace(".dirty", "")), (GOOD, False))
        client = f"[x] [INFO] [Client] composition applied: (client=v2.0.0.0 build.1Z+{GOOD}.dirty)"
        self.assertIsNone(iv.parse_build_stamp(client))

    def test_scrub_replaces_the_user_profile_path(self):
        text = r"no file at C:\Users\tester\CoopDS\x.json and C:/Users/tester/y and C:\\Users\\tester\\z"
        self.assertEqual(iv.scrub(text, profile=""),
                         r"no file at %USERPROFILE%\CoopDS\x.json and %USERPROFILE%/y and %USERPROFILE%\\z")
        self.assertEqual(iv.scrub(r"D:\Profiles\me\a", profile=r"D:\Profiles\me"), r"%USERPROFILE%\a")

    def test_coop_log_is_placed_by_mtime_and_first_time(self):
        mtime = datetime(2026, 10, 23, 14, 22, 3)
        start = iv.place_coop(["junk", "[(90596) 14:09:50 INF Coop.CoopMod] ===="], mtime)
        self.assertEqual(start, datetime(2026, 10, 23, 14, 9, 50))
        # A log that ran past midnight: its first time is later than the mtime's clock.
        self.assertEqual(iv.place_coop(["[23:59:50 INF Coop.Server] up"], datetime(2026, 10, 24, 0, 5, 0)),
                         datetime(2026, 10, 23, 23, 59, 50))
        self.assertIsNone(iv.place_coop(["   at TaleWorlds.X()"], mtime))


class ManifestTests(unittest.TestCase):
    def write(self, text, number=9200):
        d = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, d)
        p = d / f"{number}.toml"
        p.write_text(text, encoding="utf-8")
        return p

    BASE = ('issue = 9200\nfixed_in = "40385aa5b"\nlogs = ["taom"]\n'
            '[[check]]\nid = "taom.fixture.a"\npass = [\'x\']\n')

    def test_a_good_manifest_loads(self):
        m = iv.load_manifest(self.write(self.BASE), 9200)
        self.assertEqual([c.id for c in m.checks], ["taom.fixture.a"])
        self.assertEqual(m.checks[0].absence, "fail")
        self.assertEqual(m.checks[0].min, 1)

    def test_bad_manifests_raise_code_2(self):
        cases = {
            "unknown key": self.BASE + "absense = 'fail'\n",
            "bad id": self.BASE.replace("taom.fixture.a", "Taom"),
            "issue mismatch": self.BASE.replace("issue = 9200", "issue = 9201"),
            "bad fixed_in": self.BASE.replace("40385aa5b", "HEAD"),
            "unknown log": self.BASE.replace('["taom"]', '["taom", "steam"]'),
            "no taom log": self.BASE.replace('["taom"]', '["ds"]'),
            "no pass": self.BASE.replace("pass = ['x']", "reached = ['x']"),
            "bad absence": self.BASE + "absence = 'maybe'\n",
            "bad min": self.BASE + "min = 0\n",
            "no checks": 'issue = 9200\nfixed_in = "40385aa5b"\nlogs = ["taom"]\n',
            "duplicate id": self.BASE + '[[check]]\nid = "taom.fixture.a"\npass = [\'y\']\n',
            "human without claim": self.BASE + "[[human]]\nwhat = 'x'\n",
            "check not a table": 'issue = 9200\nfixed_in = "40385aa5b"\nlogs = ["taom"]\ncheck = [1]\n',
            "human not a list": self.BASE.replace("issue = 9200\n", "issue = 9200\nhuman = 1\n"),
            "title not a string": self.BASE.replace("issue = 9200\n", "issue = 9200\ntitle = 1\n"),
            # An unescaped dot ends the named id at "taom", which every source grep would find.
            "named id not dot.kebab": self.BASE.replace(
                "pass = ['x']", r"pass = ['\[Check\] id=taom.battle-action-bar.atach result=pass']"),
            # A scope cuts each log on its own, so a pass line in a second kind of log would be dropped.
            "scope over two log kinds": self.BASE.replace('["taom"]', '["taom", "rgl"]') + "scope = 'x'\n",
        }
        for label, text in cases.items():
            with self.subTest(label):
                with self.assertRaises(iv.VerdictError) as cm:
                    iv.load_manifest(self.write(text), 9200)
                self.assertEqual(cm.exception.code, 2)

    def test_a_pattern_that_matches_an_empty_line_is_refused(self):
        # It matches every line, so as a pass pattern it would pass every bound session.
        for pattern in ("", "(x)?", "|never", "x*"):
            for key in ("pass", "reached", "fail", "scope"):
                with self.subTest(key=key, pattern=pattern):
                    value = f"'{pattern}'" if key == "scope" else f"['{pattern}']"
                    text = self.BASE + f"{key} = {value}\n" if key != "pass" else \
                        self.BASE.replace("pass = ['x']", f"pass = {value}")
                    with self.assertRaises(iv.VerdictError) as cm:
                        iv.load_manifest(self.write(text), 9200)
                    self.assertEqual(cm.exception.code, 2)
                    self.assertIn("empty", str(cm.exception))

    def test_a_manifest_that_is_not_utf8_raises_code_2(self):
        p = self.write("")
        p.write_bytes(self.BASE.replace("pass = ['x']", "pass = ['x—']").encode("cp1252"))  # 0x97
        with self.assertRaises(iv.VerdictError) as cm:
            iv.load_manifest(p, 9200)
        self.assertEqual(cm.exception.code, 2)
        self.assertIn("UTF-8", str(cm.exception))

    def test_named_check_ids_reads_escaped_ids_from_patterns(self):
        text = self.BASE.replace("pass = ['x']", r"pass = ['\[Check\] id=taom\.battle-action-bar\.attach result=pass']")
        m = iv.load_manifest(self.write(text), 9200)
        self.assertEqual(iv.named_check_ids(m), {"taom.battle-action-bar.attach"})


def taom_source_text():
    chunks = []
    for folder in ("Main", "Client", "Dependencies"):
        for p in (REPO / folder).rglob("*.cs"):
            if "\\obj\\" in str(p) or "/obj/" in str(p) or "\\bin\\" in str(p) or "/bin/" in str(p):
                continue
            chunks.append(p.read_text(encoding="utf-8", errors="replace"))
    return "\n".join(chunks)


class RepoManifestGateTests(unittest.TestCase):
    """Every manifest in tools/ingame_checks loads, and every [Check] id it names is emitted somewhere."""

    def manifests(self):
        paths = sorted((TOOLS / "ingame_checks").glob("*.toml"))
        self.assertTrue(paths, "tools/ingame_checks holds no manifest")
        return [iv.load_manifest(p, int(p.stem)) for p in paths]

    def test_every_manifest_loads_and_every_regex_compiles(self):
        for m in self.manifests():
            for c in m.checks:
                for p in c.reached + c.passes + c.fails + ([c.scope] if c.scope else []):
                    self.assertIsInstance(p, re.Pattern)

    def test_every_named_check_id_appears_in_a_taom_source_file(self):
        source = None
        for m in self.manifests():
            for check_id in sorted(iv.named_check_ids(m)):
                source = source if source is not None else taom_source_text()
                # The id is a const string literal (design 3b rule 5); a mention in a comment does not count.
                self.assertIn(f'"{check_id}"', source,
                              f"#{m.issue} names [Check] id={check_id}, which no TAOM source emits as a string literal")

    def test_970_attach_names_no_check_line_yet(self):
        m = iv.load_manifest(TOOLS / "ingame_checks" / "970.toml", 970)
        self.assertEqual([c.id for c in m.checks], ["taom.battle-action-bar.attach"])
        self.assertEqual(iv.named_check_ids(m), set())

    def test_981_holds_only_the_issue_s_server_items(self):
        # #981 closes when its six items pass; a client join is none of them.
        m = iv.load_manifest(TOOLS / "ingame_checks" / "981.toml", 981)
        self.assertEqual([c.id for c in m.checks], [
            "taom.server-settings.no-file", "taom.server-settings.one-key", "taom.server-settings.bad-value",
            "taom.server-settings.fingerprint", "taom.server-settings.hitch-probe-off"])

    def test_971_names_the_open_and_init_checks(self):
        m = iv.load_manifest(TOOLS / "ingame_checks" / "971.toml", 971)
        ids = ["taom.career-screen.open", "taom.career-screen.init"]
        self.assertEqual([c.id for c in m.checks], ids)
        self.assertEqual(iv.named_check_ids(m), set(ids))

    def test_every_fixed_in_is_a_commit_in_this_clone(self):
        rc, shallow = iv.run_command(["git", "-C", str(REPO), "rev-parse", "--is-shallow-repository"])
        if rc != 0:
            self.skipTest("git is not available")
        if shallow.strip() == "true":
            self.skipTest("shallow history")
        for m in self.manifests():
            rc, _ = iv.run_command(["git", "-C", str(REPO), "cat-file", "-e", f"{m.fixed_in}^{{commit}}"])
            self.assertEqual(rc, 0, f"#{m.issue} fixed_in {m.fixed_in} is not a commit in this clone")


class VerdictRuleTests(unittest.TestCase):
    """Rules 1 to 4 over the real #970 manifest, one fixture each."""

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("970.toml")

    def test_rule_1_a_fail_line_is_fail(self):
        self.t.taom("attach_failed.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 1)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("layer attach failed", out)

    def test_rule_2_every_pass_line_is_pass(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "PASS")
        self.assertRegex(out, r"taom_debug_2026-10-30_12-57-22\.log:\d+  .*layer attached")
        self.assertIn("still owed by a person", out)
        self.assertIn("with Esc open, a bar button highlights on click", out)
        self.assertIn("a ground click with the order menu open gives a move order", out)
        self.assertIn("turning the setting off mid-battle removes the bar, and turning it on again shows it once", out)

    def test_rule_1_a_fail_line_beats_complete_pass_evidence(self):
        self.t.taom("attach_pass_and_failed.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("a fail pattern matched", out)

    def test_rule_3_reached_without_pass_fails_by_default(self):
        self.t.manifest("9007.toml")
        self.t.taom("attach_absent.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9007"])
        self.assertEqual(code, 1)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("reached", out)
        self.assertIn("no [Shutdown] line: still running, or ended by a debugger stop, a crash or a kill", out)

    def test_970_a_battle_that_ends_in_deployment_is_not_reached(self):
        # The attach runs on the first Battle-mode tick, and no line marks the end of deployment.
        self.t.taom("attach_absent.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")
        self.assertIn("absence = not-reached", out)

    def test_970_a_battle_left_in_deployment_does_not_hide_a_passed_one(self):
        self.t.taom("attach_two_battles.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "PASS")

    def test_970_does_not_need_battle_load_diagnostics(self):
        # [BattleLoad] lines are written only while that player toggle is on; the attach lines are not.
        for fixture, code, verdict in (("attach_pass_no_battle_load.txt", 0, "PASS"),
                                       ("attach_failed_no_battle_load.txt", 1, "FAIL")):
            with self.subTest(fixture):
                t = Tree(self)
                t.real_manifest("970.toml")
                t.taom(fixture, "2026-10-30_12-57-22")
                got, out, _ = t.run(["--issue", "970"])
                self.assertEqual(got, code, out)
                self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), verdict)

    def test_rule_3_absence_not_reached(self):
        self.t.manifest("9002.toml")
        self.t.taom("attach_absent.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9002"])
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")

    def test_rule_4_nothing_matches_is_not_reached(self):
        self.t.taom("no_battle.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")

    def test_scope_judges_each_battle_and_fail_wins(self):
        self.t.manifest("9007.toml")
        self.t.taom("attach_two_battles.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9007"])
        self.assertEqual(code, 1)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")

    def test_min_counts_matching_lines(self):
        self.t.manifest("9005.toml")
        self.t.taom("attach_two_battles.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9005"])
        self.assertEqual(code, 1)
        self.assertIn("1 of the 2", out)

    def test_a_skip_line_names_why_it_was_not_reached(self):
        self.t.manifest("9002.toml")
        self.t.taom("check_skip.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9002"])
        self.assertEqual(code, 4)
        self.assertIn("skipped: reason=setting-off", out)


class BuildBindingTests(unittest.TestCase):
    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("970.toml")

    def test_ancestor_counts(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        r = FakeRunner(ancestors=(GOOD,))
        code, _, _ = self.t.run(["--issue", "970"], r)
        self.assertEqual(code, 0)
        git = [c[0] for c in r.calls if c[0][0] == "git"]
        self.assertEqual(git, [["git", "-C", str(REPO), "merge-base", "--is-ancestor", "40385aa5b", GOOD]])

    def test_not_ancestor_is_not_reached(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"], FakeRunner(ancestors=()))
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")
        self.assertIn("predates fixed_in 40385aa5b", out)

    def test_dirty_build_needs_allow_dirty(self):
        self.t.taom("attach_dirty.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4)
        self.assertIn("allow_dirty", out)

    def test_dirty_build_counts_with_allow_dirty(self):
        self.t.manifest("9006.toml")
        self.t.taom("attach_dirty.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "9006"])
        self.assertEqual(code, 0, out)

    def test_unknown_build_sha_is_not_reached(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"], FakeRunner(git_rc=128))
        self.assertEqual(code, 4)
        self.assertIn("could not compare", out)

    def test_a_taom_log_without_a_git_sha_stamp_is_not_reached(self):
        # attach_no_stamp has no [BuildStamp] line; attach_nogit's stamp ends in .nogit (git failed at build time).
        for fixture in ("attach_no_stamp.txt", "attach_nogit.txt"):
            with self.subTest(fixture):
                t = Tree(self)
                t.real_manifest("970.toml")
                t.taom(fixture, "2026-10-30_12-57-22")
                code, out, _ = t.run(["--issue", "970"])
                self.assertEqual(code, 4, out)
                self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")
                self.assertIn("no [BuildStamp] line with a git sha in taom_debug_2026-10-30_12-57-22.log", out)

    def test_git_missing_exits_3(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, _, err = self.t.run(["--issue", "970"], FakeRunner(git_rc=127))
        self.assertEqual(code, 3)
        self.assertIn("ERROR:", err)


class SessionTests(unittest.TestCase):
    def setUp(self):
        self.t = Tree(self)

    def test_two_sessions_pair_a_save_and_its_load(self):
        self.t.manifest("9001.toml")
        self.t.taom("session_save.txt", "2026-10-21_12-10-00")
        self.t.taom("session_load.txt", "2026-10-22_14-20-00")
        code, out, _ = self.t.run(["--issue", "9001", "--sessions", "2"])
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, "taom.fixture.save"), "PASS")
        self.assertEqual(verdict_of(out, "taom.fixture.load"), "PASS")
        code, out, _ = self.t.run(["--issue", "9001"])
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.fixture.save"), "NOT REACHED")

    def test_the_newest_decisive_verdict_wins(self):
        self.t.real_manifest("970.toml")
        self.t.taom("attach_failed.txt", "2026-10-29_12-57-22")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        self.t.taom("no_battle.txt", "2026-10-31_12-57-22")
        code, out, _ = self.t.run(["--issue", "970", "--sessions", "3"])
        self.assertEqual(code, 0, out)
        self.assertRegex(out, r"PASS\s+taom\.battle-action-bar\.attach\s+\(session 2\)")

    def test_an_older_fail_is_named_beside_the_newer_pass(self):
        self.t.real_manifest("970.toml")
        self.t.taom("no_battle.txt", "2026-10-29_12-57-22")
        self.t.taom("attach_failed.txt", "2026-10-30_12-57-22")
        self.t.taom("attach_pass.txt", "2026-10-31_12-57-22")
        for fmt in ("text", "md"):
            with self.subTest(format=fmt):
                code, out, _ = self.t.run(["--issue", "970", "--sessions", "3", "--format", fmt])
                self.assertEqual(code, 0, out)
                self.assertIn("; FAIL in session 2: a fail pattern matched", out)

    def test_not_reached_quotes_the_newest_bound_session(self):
        self.t.real_manifest("970.toml")
        self.t.taom("no_battle.txt", "2026-10-29_12-57-22")
        self.t.taom("attach_dirty.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970", "--sessions", "2"])
        self.assertEqual(code, 4)
        self.assertRegex(out, r"NOT REACHED\s+taom\.battle-action-bar\.attach\s+\(session 2\)")
        self.assertIn("no line matched a reached or pass pattern", out)

    def test_session_path_reads_that_log(self):
        self.t.real_manifest("970.toml")
        old = self.t.taom("attach_pass.txt", "2026-10-29_12-57-22")
        self.t.taom("no_battle.txt", "2026-10-30_12-57-22")
        code, _, _ = self.t.run(["--issue", "970", "--session", str(old)])
        self.assertEqual(code, 0)

    def test_coop_log_without_a_date_joins_by_mtime(self):
        self.t.manifest("9003.toml")
        self.t.taom("client_coop_session.txt", "2026-10-23_14-09-49", mtime="2026-10-23 14:22:19")
        self.t.coop_client("coop_client.txt", mtime="2026-10-23 14:22:03")
        code, out, _ = self.t.run(["--issue", "9003"])
        self.assertEqual(code, 0, out)
        self.assertIn("Coop_client.log:5", out)

    def test_coop_log_from_another_day_does_not_join(self):
        self.t.manifest("9003.toml")
        self.t.taom("client_coop_session.txt", "2026-10-23_14-09-49")
        self.t.coop_client("coop_client.txt", mtime="2026-10-25 14:22:03")
        code, out, _ = self.t.run(["--issue", "9003"])
        self.assertEqual(code, 4)

    def test_coop_log_that_cannot_be_placed_is_named(self):
        self.t.manifest("9003.toml")
        self.t.taom("client_coop_session.txt", "2026-10-23_14-09-49")
        self.t.coop_client("coop_client_no_time.txt", mtime="2026-10-23 14:22:03")
        code, out, _ = self.t.run(["--issue", "9003"])
        self.assertEqual(code, 4)
        self.assertIn("could not place Coop_client.log", out)

    def test_rgl_log_joins_by_mtime(self):
        self.t.manifest("9004.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        self.t.rgl_log("rgl_log.txt", "rgl_log_74808.txt", mtime="2026-10-30 13:28:30")
        code, out, _ = self.t.run(["--issue", "9004"])
        self.assertEqual(code, 0, out)
        self.t.rgl_log("rgl_log.txt", "rgl_log_74808.txt", mtime="2026-10-30 20:44:00")
        code, _, _ = self.t.run(["--issue", "9004"])
        self.assertEqual(code, 4)

    def test_rgl_slack_stops_at_the_next_session(self):
        # The rgl log written two seconds into the next (dirty) session is that session's, not the earlier one's.
        self.t.manifest("9004.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        self.t.taom("attach_dirty.txt", "2026-10-30_13-30-00")
        self.t.rgl_log("rgl_log.txt", "rgl_log_222.txt", mtime="2026-10-30 13:30:02")
        code, out, _ = self.t.run(["--issue", "9004", "--sessions", "2"])
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "engine.fixture.rgl"), "NOT REACHED")
        self.assertNotIn("rgl_log_222.txt:", out)

    def test_the_next_process_s_rgl_errors_log_does_not_join(self):
        # The engine writes rgl_log_errors_<pid>.txt seconds after the next process starts, inside the slack
        # after this session's last line; it holds a Build Version line too, but it is not this session's.
        self.t.manifest("9004.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        self.t.rgl_log("rgl_log_errors.txt", "rgl_log_errors_222.txt", mtime="2026-10-30 13:28:30")
        code, out, _ = self.t.run(["--issue", "9004"])
        self.assertEqual(code, 4, out)
        self.assertNotIn("rgl_log_errors_222.txt", out)

    def test_session_path_on_a_server_taom_log_reads_it_as_taom_ds(self):
        self.t.real_manifest("981.toml")
        p = self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        code, out, _ = self.t.run(["--issue", "981", "--session", str(p)], ds=True)
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "taom.server-settings.no-file"), "PASS")
        self.assertIn("also read: coop-server-20261023-135846.log (ds)", out)

    def test_path_kind_resolves_a_relative_path(self):
        logs = self.t.ds_engine / "bin" / "Win64_Shipping_Server" / "Logs"
        self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        old = os.getcwd()
        os.chdir(logs)
        self.addCleanup(os.chdir, old)
        self.assertEqual(iv.path_kind("taom_debug_2026-10-23_13-58-49.log"), "taom-ds")

    def test_session_path_in_the_client_folder_is_a_client_log(self):
        # The client log also carries [SettingsFingerprint] and [TickProfiler] lines; #981 reads server logs only.
        self.t.real_manifest("981.toml")
        p = self.t.taom("ds_taom_one_key.txt", "2026-10-23_14-30-10")
        code, out, _ = self.t.run(["--issue", "981", "--session", str(p)], ds=True)
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "taom.server-settings.fingerprint"), "NOT REACHED")
        self.assertEqual(verdict_of(out, "taom.server-settings.one-key"), "NOT REACHED")
        self.assertIn("is a taom log", out)

    def test_session_path_skips_a_manifest_that_does_not_read_its_kind(self):
        self.t.real_manifest("970.toml")
        self.t.real_manifest("981.toml")
        self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        p = self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        code, out, err = self.t.run(["--issue", "970", "981", "--session", str(p)], ds=True)
        self.assertEqual(code, 4, err)
        self.assertIn("is a ds log, which this manifest does not read", out)
        self.assertEqual(verdict_of(out, "taom.server-settings.no-file"), "PASS")


class ServerSettings981Tests(unittest.TestCase):
    """The real #981 manifest over three server boots, each its own session."""

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("981.toml")

    def boots(self, with_ds=True):
        self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        self.t.ds_taom("ds_taom_one_key.txt", "2026-10-23_14-30-10")
        self.t.ds_taom("ds_taom_bad_value.txt", "2026-10-23_15-00-05")
        if with_ds:
            self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
            self.t.ds("ds_serving.txt", "20261023-143005", mtime="2026-10-23 14:40:00")
            self.t.ds("ds_serving.txt", "20261023-150002", mtime="2026-10-23 15:10:00")

    def test_three_boots_pass_every_check(self):
        self.boots()
        code, out, _ = self.t.run(["--issue", "981", "--sessions", "3"], ds=True)
        self.assertEqual(code, 0, out)
        for check_id in ("taom.server-settings.no-file", "taom.server-settings.one-key",
                         "taom.server-settings.bad-value", "taom.server-settings.fingerprint",
                         "taom.server-settings.hitch-probe-off"):
            self.assertEqual(verdict_of(out, check_id), "PASS", check_id)
        self.assertIn("item 6: on a client, the TAOM MCM page opens and saves as before", out)
        # A dedicated server never logs [Shutdown], so the note would say nothing there.
        self.assertNotIn("no [Shutdown] line", out)

    def test_a_later_boot_that_times_out_does_not_fail_an_earlier_bad_value(self):
        # Boot A: the -5 value, reverted, then SERVING. Boot B: another valid value, then a LOAD TIMEOUT.
        self.t.ds_taom("ds_taom_bad_value.txt", "2026-10-23_15-00-05")
        self.t.ds("ds_serving.txt", "20261023-150002", mtime="2026-10-23 15:10:00")
        self.t.ds_taom("ds_taom_other_value.txt", "2026-10-23_16-00-05")
        self.t.ds("ds_load_timeout.txt", "20261023-160002", mtime="2026-10-23 16:10:03")
        code, out, _ = self.t.run(["--issue", "981", "--sessions", "2"], ds=True)
        self.assertEqual(code, 4, out)
        self.assertRegex(out, r"PASS\s+taom\.server-settings\.bad-value\s+\(session 2\)")
        # Boot B set CastleNotablesPerCastle = 2: not item 2's boot, so it proves nothing about it.
        self.assertEqual(verdict_of(out, "taom.server-settings.one-key"), "NOT REACHED")

    def test_a_later_boot_with_the_probe_on_does_not_fail_hitch_probe_off(self):
        # Item 5 is the no-file boot; a later file may set EnableHitchProbe, and its install line proves nothing.
        self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        self.t.ds_taom("ds_taom_probe_on.txt", "2026-10-23_16-30-10")
        self.t.ds("ds_serving.txt", "20261023-163005", mtime="2026-10-23 16:40:00")
        for k, code, verdict in (("2", 4, "PASS"), ("1", 4, "NOT REACHED")):
            with self.subTest(sessions=k):
                got, out, _ = self.t.run(["--issue", "981", "--sessions", k], ds=True)
                self.assertEqual(got, code, out)
                self.assertEqual(verdict_of(out, "taom.server-settings.hitch-probe-off"), verdict)

    def test_a_no_file_boot_that_installs_the_probe_fails_hitch_probe_off(self):
        self.t.ds_taom("ds_taom_no_file_probe_on.txt", "2026-10-23_17-00-05")
        self.t.ds("ds_serving.txt", "20261023-170002", mtime="2026-10-23 17:10:00")
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, "taom.server-settings.hitch-probe-off"), "FAIL")
        self.assertEqual(verdict_of(out, "taom.server-settings.no-file"), "PASS")

    def test_a_no_file_boot_that_stopped_before_serving_is_not_reached_for_hitch_probe_off(self):
        # Only the boot line and the template line, and a ds log with the launcher banner: the server never
        # reached SERVING, so the missing "probe off" line proves nothing.
        self.t.ds_taom("ds_taom_no_file_stopped.txt", "2026-10-23_13-58-49")
        self.t.ds("ds_banner_only.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(verdict_of(out, "taom.server-settings.hitch-probe-off"), "NOT REACHED", out)
        self.assertEqual(verdict_of(out, "taom.server-settings.no-file"), "PASS")
        self.assertEqual(code, 4, out)

    def test_a_bad_value_that_is_accepted_fails_bad_value(self):
        self.t.ds_taom("ds_taom_bad_value_accepted.txt", "2026-10-23_15-00-05")
        self.t.ds("ds_serving.txt", "20261023-150002", mtime="2026-10-23 15:10:00")
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(verdict_of(out, "taom.server-settings.bad-value"), "FAIL", out)
        self.assertEqual(code, 1, out)

    def test_a_good_value_that_is_rejected_fails_one_key(self):
        self.t.ds_taom("ds_taom_one_key_rejected.txt", "2026-10-23_15-00-05")
        self.t.ds("ds_serving.txt", "20261023-150002", mtime="2026-10-23 15:10:00")
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(verdict_of(out, "taom.server-settings.one-key"), "FAIL", out)
        self.assertEqual(code, 1, out)

    def test_the_newest_boot_alone_leaves_the_others_not_reached(self):
        self.boots()
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.server-settings.bad-value"), "PASS")
        self.assertEqual(verdict_of(out, "taom.server-settings.no-file"), "NOT REACHED")

    def test_absence_needs_every_named_log(self):
        self.boots(with_ds=False)
        code, out, _ = self.t.run(["--issue", "981", "--ds-engine", str(self.t.ds_engine)])
        self.assertEqual(code, 4)
        self.assertEqual(verdict_of(out, "taom.server-settings.bad-value"), "NOT REACHED")
        self.assertIn("the ds log was not read", out)

    def test_unread_settings_page_fails_the_fingerprint(self):
        self.t.ds_taom("ds_taom_fingerprint_unread.txt", "2026-10-23_09-23-19")
        self.t.ds("ds_serving.txt", "20261023-092310", mtime="2026-10-23 09:30:00")
        code, out, _ = self.t.run(["--issue", "981"], FakeRunner(ancestors=(OLD,)), ds=True)
        self.assertEqual(code, 1)
        self.assertEqual(verdict_of(out, "taom.server-settings.fingerprint"), "FAIL")
        # This boot has no no-file line, so it is not item 5's boot.
        self.assertEqual(verdict_of(out, "taom.server-settings.hitch-probe-off"), "NOT REACHED")

    def test_a_boot_still_loading_after_the_banner_fails_bad_value(self):
        # The banner names "[DedicatedServer] SERVING" before the server serves, so it is no SERVING line. This
        # server log ends while the game still loads: a run during the boot, or a boot that stopped there. The
        # tool knows no end of a server session yet, so the reached bad-value check without SERVING is a FAIL.
        self.t.ds_taom("ds_taom_bad_value.txt", "2026-10-23_15-00-05")
        self.t.ds("ds_banner_only.txt", "20261023-150002", mtime="2026-10-23 15:10:00")
        code, out, _ = self.t.run(["--issue", "981"], ds=True)
        self.assertEqual(code, 1)
        self.assertEqual(verdict_of(out, "taom.server-settings.bad-value"), "FAIL")
        self.assertIn("SERVING", out)

    def test_ds_logs_alone_cannot_bind_a_build(self):
        self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        code, out, _ = self.t.run(["--issue", "981", "--ds-data", str(self.t.ds_data)])
        self.assertEqual(code, 4)
        self.assertIn("no TAOM log", out)
        self.assertEqual(verdict_of(out, "taom.server-settings.fingerprint"), "NOT REACHED")


class Issue971Tests(unittest.TestCase):
    """The real #971 manifest: the career button opens the career screen, which initialises."""

    OPEN = "taom.career-screen.open"
    INIT = "taom.career-screen.init"

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("971.toml")

    def judge(self, fixture):
        self.t.taom(fixture, "2026-10-31_14-00-00")
        return self.t.run(["--issue", "971"])

    def test_a_click_that_opens_and_initialises_passes_both(self):
        code, out, _ = self.judge("career_open_pass.txt")
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, self.OPEN), "PASS")
        self.assertEqual(verdict_of(out, self.INIT), "PASS")
        self.assertIn("the click plays one sound, not two", out)
        self.assertIn("- the career screen draws its career tree\n", out)

    def test_a_click_that_throws_fails_open_and_leaves_init_not_reached(self):
        code, out, _ = self.judge("career_open_threw.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.OPEN), "FAIL")
        self.assertIn("reason=NullReferenceException", out)
        self.assertEqual(verdict_of(out, self.INIT), "NOT REACHED")

    def test_a_click_that_leaves_another_screen_on_top_fails_open(self):
        code, out, _ = self.judge("career_open_not_on_top.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.OPEN), "FAIL")
        self.assertIn("reason=not-on-top", out)

    def test_an_open_with_no_init_line_fails_init(self):
        code, out, _ = self.judge("career_open_no_init.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.OPEN), "PASS")
        self.assertEqual(verdict_of(out, self.INIT), "FAIL")
        self.assertRegex(out, r"FAIL\s+taom\.career-screen\.init\b.*\n\s+reached, but")

    def test_a_dialogue_career_switch_alone_is_not_reached(self):
        # The switch mode init line is not the button's; the dialogue writes no open line.
        code, out, _ = self.judge("career_switch_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.OPEN), "NOT REACHED")
        self.assertEqual(verdict_of(out, self.INIT), "NOT REACHED")

    def test_a_build_before_the_check_lines_is_not_reached(self):
        # It has the fix and its CareerSystem lines, but no [Check] line, so it must not read as a FAIL.
        code, out, _ = self.judge("career_pre_checks.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.OPEN), "NOT REACHED")
        self.assertEqual(verdict_of(out, self.INIT), "NOT REACHED")
        self.assertIn("no line matched", out)


class CrashReportEchoTests(unittest.TestCase):
    """A crash report writes the last log lines back into the log behind "[CrashReport] "."""

    def setUp(self):
        self.t = Tree(self)
        self.t.manifest("9102.toml")
        self.t.taom("crash_echo.txt", "2026-10-30_12-57-22")

    def test_an_echoed_check_line_does_not_count_toward_min(self):
        code, out, _ = self.t.run(["--issue", "9102"])
        self.assertNotEqual(verdict_of(out, "taom.fixture.days"), "PASS", out)

    def test_an_echoed_scope_line_opens_no_segment(self):
        code, out, _ = self.t.run(["--issue", "9102"])
        self.assertEqual(verdict_of(out, "taom.fixture.attach"), "PASS", out)
        self.assertNotIn("segment", out)

    def test_evidence_keeps_the_line_numbers_of_the_log(self):
        code, out, _ = self.t.run(["--issue", "9102"])
        self.assertRegex(out, r"taom_debug_2026-10-30_12-57-22\.log:3  ")


class PostTests(unittest.TestCase):
    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("981.toml")
        self.t.ds_taom("ds_taom_no_file.txt", "2026-10-23_13-58-49")
        self.t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")

    def test_no_post_runs_no_gh(self):
        r = FakeRunner()
        self.t.run(["--issue", "981"], r, ds=True)
        self.assertEqual(r.gh_calls(), [])

    def test_post_comments_once_with_the_profile_scrubbed(self):
        r = FakeRunner()
        code, _, _ = self.t.run(["--issue", "981", "--post"], r, ds=True)
        self.assertEqual(code, 4)
        gh = r.gh_calls()
        self.assertEqual([c[0] for c in gh], [["gh", "issue", "comment", "981", "--body-file", "-"]])
        body = gh[0][1]
        self.assertIn("%USERPROFILE%", body)
        self.assertNotIn(r"C:\Users\tester", body)
        self.assertIn("| `taom.server-settings.no-file` | PASS |", body)
        self.assertIn(GOOD[:9], body)

    def test_post_failure_exits_3(self):
        code, _, err = self.t.run(["--issue", "981", "--post"], FakeRunner(post_rc=1), ds=True)
        self.assertEqual(code, 3)
        self.assertIn("ERROR:", err)


class ArgumentTests(unittest.TestCase):
    def setUp(self):
        self.t = Tree(self)

    def test_no_selection_exits_2(self):
        code, _, err = self.t.run([])
        self.assertEqual(code, 2)
        self.assertIn("--issue", err)

    def test_missing_manifest_exits_2(self):
        code, _, err = self.t.run(["--issue", "4242"])
        self.assertEqual(code, 2)
        self.assertIn("4242", err)

    def test_malformed_manifests_exit_2(self):
        for name in ("9100.toml", "9101.toml"):
            with self.subTest(name):
                self.t.manifest(name)
                code, _, err = self.t.run(["--issue", name[:-5]])
                self.assertEqual(code, 2)
                self.assertIn("ERROR:", err)
        with self.subTest("not UTF-8"):
            # A cp1252 em dash (0x97), as an editor on this machine may save it.
            (self.t.checks / "9102.toml").write_bytes(
                b'issue = 9102\nfixed_in = "40385aa5b"\nlogs = ["taom"]\n'
                b"[[check]]\nid = \"taom.fixture.a\"\nneeds = \"a battle \x97 any\"\npass = ['x']\n")
            code, _, err = self.t.run(["--issue", "9102"])
            self.assertEqual(code, 2, err)
            self.assertIn("ERROR:", err)

    def test_session_path_with_several_sessions_exits_2(self):
        self.t.real_manifest("970.toml")
        p = self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, _, _ = self.t.run(["--issue", "970", "--session", str(p), "--sessions", "2"])
        self.assertEqual(code, 2)

    def test_missing_ds_data_folder_exits_2(self):
        self.t.real_manifest("981.toml")
        code, _, _ = self.t.run(["--issue", "981", "--ds-data", str(self.t.root / "nope")])
        self.assertEqual(code, 2)

    def test_owed_keeps_issues_with_a_manifest(self):
        self.t.real_manifest("970.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        r = FakeRunner(gh_list=(0, '[{"number": 970}, {"number": 1011}, {"number": 918}]'))
        code, out, _ = self.t.run(["--owed"], r)
        self.assertEqual(code, 0, out)
        self.assertEqual(r.gh_calls()[0][0], ["gh", "issue", "list", "--state", "all", "--label",
                                              "triage-needs-ingame", "--limit", "1000", "--json", "number"])
        self.assertIn("no manifest: #1011, #918", out)

    def test_owed_with_nothing_judged_is_not_pass(self):
        for listing in ("[]", '[{"number": 4242}]'):
            with self.subTest(listing):
                code, out, _ = self.t.run(["--owed"], FakeRunner(gh_list=(0, listing)))
                self.assertEqual(code, 4, out)
                self.assertIn("nothing judged", out)

    def test_owed_gh_failure_exits_3(self):
        code, _, err = self.t.run(["--owed"], FakeRunner(gh_list=(1, "")))
        self.assertEqual(code, 3)
        self.assertIn("gh auth status", err)

    def test_md_format(self):
        self.t.real_manifest("970.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970", "--format", "md"])
        self.assertEqual(code, 0)
        self.assertIn("| `taom.battle-action-bar.attach` | PASS |", out)
        self.assertNotIn("needs:", out)
        self.assertIn("- [ ] after Begin battle", out)

    def test_md_tells_the_tester_what_to_do_next_time(self):
        # --post publishes the markdown, so it carries the needs, the session notes and the logs not found.
        self.t.real_manifest("970.toml")
        self.t.real_manifest("981.toml")
        self.t.taom("attach_absent.txt", "2026-10-30_12-57-22")
        self.t.ds_taom("ds_taom_bad_value.txt", "2026-10-23_15-00-05")
        code, out, _ = self.t.run(["--issue", "970", "--format", "md"])
        self.assertEqual(code, 4)
        self.assertIn("needs: a field battle with Show Battle Action Bar on", out)
        self.assertIn("no [Shutdown] line: still running", out)
        code, out, _ = self.t.run(["--issue", "981", "--format", "md", "--ds-engine", str(self.t.ds_engine)])
        self.assertEqual(code, 4)
        self.assertIn("not found for this session: ds", out)
        self.assertIn("needs: item 1: delete taom-server-settings.json", out)

    def test_stdout_that_cannot_encode_a_line_does_not_crash(self):
        # The evidence line holds an em dash; a console code page that lacks it must not end the run.
        self.t.real_manifest("970.toml")
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        raw = io.BytesIO()
        stdout = io.TextIOWrapper(raw, encoding="ascii")
        with mock.patch.object(iv.sys, "stdout", stdout):
            code = iv.main(["--issue", "970", "--game", str(self.t.game)], runner=FakeRunner(),
                           err=io.StringIO(), checks_dir=self.t.checks, rgl_dir=self.t.rgl, repo=REPO)
            stdout.flush()
        self.assertEqual(code, 0)
        self.assertIn(b"\\u2014 layer attached", raw.getvalue())


class RunCommandTests(unittest.TestCase):
    def test_commands_run_in_the_taom_repo(self):
        # gh picks its repository from the working folder; run from another checkout it would post there.
        done = subprocess.CompletedProcess(["gh"], 0, stdout="", stderr="")
        with mock.patch.object(iv.subprocess, "run", return_value=done) as run:
            iv.run_command(["gh", "issue", "list"])
        self.assertEqual(run.call_args.kwargs["cwd"], str(iv.REPO))


if __name__ == "__main__":
    unittest.main()
