#!/usr/bin/env python3
"""Unit tests for the in-game verdict reader (tools/ingame_verdict.py) and its manifests.

Run:  python -m unittest tools.tests.test_ingame_verdict -v

Each test builds a throwaway game tree (and dedicated server folders) from the fixture logs in
fixtures/ingame_verdict/, sets the file times the session logic reads, and runs main() with a fake
runner, so no test calls the real git or gh. The fixture lines are scrubbed copies of real log
lines. The only [ServerSettings] lines on disk come from a .dirty build (a staged server log of
2026-10-23), which the shipped #981 manifest leaves NOT REACHED, and no "layer attached" or probe
install line is on disk, so those fixtures render the format strings of ServerSettingsReport.cs,
ServerSettingsReader.cs, HitchProbeLines.cs and BattleActionBarMissionView.cs. The #970 [Check]
lines render OutcomeLog's line with the details BattleActionBarMissionView.cs and
BattleActionBarService.cs pass.
No career screen line is on disk either, so the career_* fixtures render GauntletCareerScreen.cs,
CareerScreenChecks.cs and ViewModel_ExecuteCommand_CareerScreen_Patch.cs (CareerScreenChecksTests pins
their [Check] lines). The session_summary* fixtures render OutcomeLog.cs's [Session] and [Swallowed] lines.
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


# One C# token at a time, leftmost first: a // or /* */ comment, a verbatim or regular string, or a char literal.
# A "//" inside a string is eaten by the string, so it opens no comment, and '"' opens no string. An interpolated
# string's text between its holes reads as regular strings, which hold "{" or "}" and so never equal a check id.
# Raw string literals need C# 11; the repo builds C# 10 (Directory.Build.props LangVersion).
CS_TOKEN = re.compile(r"""//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\\n])*"|'(?:\\.|[^'\\\n])+'""", re.S)


def cs_string_literals(text):
    """The values of the string literals in C# source, outside comments."""
    values = set()
    for m in CS_TOKEN.finditer(text):
        token = m.group(0)
        if token.startswith('@"'):
            values.add(token[2:-1].replace('""', '"'))
        elif token.startswith('"'):
            values.add(token[1:-1])
    return values


def taom_source_files():
    # Main, Client and CoopCompat only: a [Check] line goes through IOutcomeLog, which lives in Main, and
    # TAOM.Dependencies cannot reference Main. Build output (obj, bin) is skipped.
    for folder in ("Main", "Client", "CoopCompat"):
        for p in sorted((REPO / folder).rglob("*.cs")):
            if not {"obj", "bin"} & set(p.relative_to(REPO).parts):
                yield p


def taom_source_literals():
    values = set()
    for p in taom_source_files():
        values |= cs_string_literals(p.read_text(encoding="utf-8", errors="replace"))
    return values


class CsLiteralTests(unittest.TestCase):
    """The gate's C# lexer: a [Check] id counts only as a string literal outside a comment."""

    SAMPLE = r'''internal const string A = "taom.a.code";
// "taom.b.line-comment"
/// <c>"taom.c.doc-comment"</c>
/* "taom.d.block"
   still "taom.e.block" */
Foo(); // "taom.f.trailing"
var u = "http://x"; Bar("taom.g.after-url");
var q = '"'; // "taom.h.after-char"
Log($"{(a ? "x" : "y")} n={n}"); Baz("taom.i.after-interp");
var v = @"C:\dir\"; Qux(@"taom.j.verbatim");
var s = "taom.k.inside // not a comment";
var w = "prefix taom.l.sub";
var e = "a\"b"; Q("taom.m.after-escape");
var c = '\''; R("taom.n.after-escaped-char");
'''

    def test_a_check_id_counts_only_as_a_literal_outside_comments(self):
        literals = cs_string_literals(self.SAMPLE)
        for found in ("taom.a.code", "taom.g.after-url", "taom.i.after-interp", "taom.j.verbatim",
                      "taom.m.after-escape", "taom.n.after-escaped-char", "taom.k.inside // not a comment"):
            with self.subTest(found=found):
                self.assertIn(found, literals)
        # The old gate ('"' + id + '"' anywhere in the text) accepted b to f and h.
        for absent in ("taom.b.line-comment", "taom.c.doc-comment", "taom.d.block", "taom.e.block",
                       "taom.f.trailing", "taom.h.after-char", "taom.k.inside", "taom.l.sub"):
            with self.subTest(absent=absent):
                self.assertNotIn(absent, literals)

    def test_the_gate_reads_main_client_and_coopcompat_only(self):
        files = list(taom_source_files())
        self.assertTrue(files)
        for p in files:
            parts = p.relative_to(REPO).parts
            self.assertIn(parts[0], {"Main", "Client", "CoopCompat"}, p)
            self.assertFalse({"obj", "bin"} & set(parts), p)


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
        literals = None
        for m in self.manifests():
            for check_id in sorted(iv.named_check_ids(m)):
                literals = literals if literals is not None else taom_source_literals()
                # The id is a string literal in code (design 3b rule 5); a mention in a comment does not count.
                # assertTrue, not assertIn: the message would print every literal in the repo.
                self.assertTrue(check_id in literals,
                                f"#{m.issue} names [Check] id={check_id}, which no .cs file under Main, Client or CoopCompat "
                                "holds as a string literal outside a comment")

    def test_970_names_the_attach_and_stance_checks(self):
        m = iv.load_manifest(TOOLS / "ingame_checks" / "970.toml", 970)
        self.assertEqual([c.id for c in m.checks], ["taom.battle-action-bar.attach", "taom.battle-action-bar.stance"])
        self.assertEqual(iv.named_check_ids(m), {"taom.battle-action-bar.attach", "taom.battle-action-bar.stance"})
        attach = m.checks[0]
        self.assertEqual(attach.absence, "not-reached")
        # [BattleLoad] lines need their own player toggle, so a scope on them would leave sessions NOT REACHED.
        self.assertIsNone(attach.scope)
        # "layer attached" is also written when the movie did not load, so it cannot pass the check.
        self.assertFalse([p for p in attach.passes if "layer attached" in p.pattern])

    def test_981_holds_only_the_issue_s_server_items(self):
        # #981 closes when its six items pass; a client join is none of them.
        m = iv.load_manifest(TOOLS / "ingame_checks" / "981.toml", 981)
        self.assertEqual([c.id for c in m.checks], [
            "taom.server-settings.no-file", "taom.server-settings.one-key", "taom.server-settings.bad-value",
            "taom.server-settings.fingerprint", "taom.server-settings.hitch-probe-off"])

    def test_918_holds_only_the_ai_off_check(self):
        # The design's garrison-kept check is not here: vanilla garrisons recruit without a recruit event.
        m = iv.load_manifest(TOOLS / "ingame_checks" / "918.toml", 918)
        self.assertEqual([c.id for c in m.checks], ["taom.castle-recruit.ai-off"])
        self.assertEqual(iv.named_check_ids(m), {"taom.castle-recruit.ai-off"})

    def test_971_names_the_open_and_init_checks(self):
        m = iv.load_manifest(TOOLS / "ingame_checks" / "971.toml", 971)
        ids = ["taom.career-screen.open", "taom.career-screen.init"]
        self.assertEqual([c.id for c in m.checks], ids)
        self.assertEqual(iv.named_check_ids(m), set(ids))

    def test_1011_names_two_checks_and_no_posthumous(self):
        # The posthumous-son check has no line: the template call runs before the son exists and cannot fail.
        m = iv.load_manifest(TOOLS / "ingame_checks" / "1011.toml", 1011)
        ids = ["taom.race-persist.capture", "taom.race-persist.restore"]
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


class HollowParent1076Tests(unittest.TestCase):
    """The real #1076 manifest: the repair line is written before the birth runs, so only the birth line proves it."""

    REPAIR = "taom.stale-character.parent-repair"
    BIRTH = "taom.stale-character.parent-birth"

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("1076.toml")

    def judge(self, fixture):
        self.t.taom(fixture, "2026-11-02_11-42-07")
        code, out, _ = self.t.run(["--issue", "1076"])
        return code, verdict_of(out, self.REPAIR), verdict_of(out, self.BIRTH), out

    def test_a_finished_birth_passes(self):
        code, repair, birth, out = self.judge("hollow_parent_birth_finished.txt")
        self.assertEqual((code, repair, birth), (0, "PASS", "PASS"), out)

    def test_a_birth_that_threw_fails(self):
        code, repair, birth, out = self.judge("hollow_parent_birth_threw.txt")
        self.assertEqual((code, repair, birth), (1, "PASS", "FAIL"), out)

    def test_a_throw_on_a_second_birth_is_not_hidden_by_a_first_that_finished(self):
        code, _, birth, out = self.judge("hollow_parent_second_birth_threw.txt")
        self.assertEqual((code, birth), (1, "FAIL"), out)

    def test_a_process_killed_after_the_repair_fails(self):
        code, repair, birth, out = self.judge("hollow_parent_process_killed.txt")
        self.assertEqual((code, repair, birth), (1, "PASS", "FAIL"), out)

    def test_a_healthy_game_is_not_reached(self):
        code, repair, birth, out = self.judge("hollow_parent_healthy_game.txt")
        self.assertEqual((code, repair, birth), (4, "NOT REACHED", "NOT REACHED"), out)


class CoopCompat967Tests(unittest.TestCase):
    """The real #967 manifest, C3 checks. The lines render what the CoopCompat services write through
    IOutcomeLog: FileLogger prefixes "[<time>] [INFO] " and OutcomeLog.Record writes "[Check] id=<id>
    result=<pass|fail>" and the sanitised detail. None is on disk yet."""

    SETTINGS_SERVER = "taom.coopcompat.settings.server"
    SETTINGS_CLIENT = "taom.coopcompat.settings.client"
    MOMENTUM_SERVER = "taom.coopcompat.momentum.server"
    MOMENTUM_CLIENT = "taom.coopcompat.momentum.client"
    SETTINGS_PASS = "result=pass verdict=match groups=0 build_mismatch=false"
    MOMENTUM_PASS = "result=pass sequence=1 entries=610"

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("967.toml")

    def judge(self, *checks):
        stamp = (f"[2026-11-12 10:00:00] [INFO] [BuildStamp] TAOM=v2.0.38 build.20261112-100000Z+{GOOD} "
                 f"TAOM.Dependencies=v2.0.38 build.20261112-095958Z+{GOOD} (pair OK)")
        lines = [stamp] + [f"[2026-11-12 10:05:00] [INFO] [Check] id={cid} {tail}" for cid, tail in checks]
        path = self.t.client / "Logs" / "taom_debug_2026-11-12_10-00-00.log"
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        code, out, _ = self.t.run(["--issue", "967"])
        return code, out

    def test_the_four_pass_lines_pass_each_check(self):
        code, out = self.judge((self.SETTINGS_SERVER, self.SETTINGS_PASS),
                               (self.SETTINGS_CLIENT, self.SETTINGS_PASS),
                               (self.MOMENTUM_SERVER, self.MOMENTUM_PASS),
                               (self.MOMENTUM_CLIENT, self.MOMENTUM_PASS))
        for check_id in (self.SETTINGS_SERVER, self.SETTINGS_CLIENT, self.MOMENTUM_SERVER, self.MOMENTUM_CLIENT):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    def test_a_client_build_mismatch_fails_the_client_settings_check(self):
        code, out = self.judge((self.SETTINGS_CLIENT, "result=fail verdict=match groups=0 build_mismatch=true"))
        self.assertEqual(verdict_of(out, self.SETTINGS_CLIENT), "FAIL", out)

    def test_a_log_with_no_momentum_lines_leaves_the_momentum_check_not_reached(self):
        code, out = self.judge((self.SETTINGS_SERVER, self.SETTINGS_PASS))
        self.assertEqual(verdict_of(out, self.MOMENTUM_SERVER), "NOT REACHED", out)


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
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.stance"), "PASS")
        self.assertRegex(out, r"taom_debug_2026-10-30_12-57-22\.log:\d+  .*\[Check\] id=taom\.battle-action-bar\.attach result=pass")
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

    def test_970_never_attached_fails_after_an_earlier_pass(self):
        # Battle 1 attached and took a click; battle 2 reached Battle mode with the setting on and never attached.
        self.t.taom("attach_never.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("reason=never-attached", out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.stance"), "PASS")

    def test_970_a_movie_that_did_not_load_fails_beside_layer_attached(self):
        self.t.taom("attach_movie_not_loaded.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("reason=movie-not-loaded", out)

    def test_970_setting_off_is_not_reached_with_its_reason(self):
        self.t.taom("attach_setting_off.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")
        self.assertIn("skipped: reason=setting-off mission=1", out)

    def test_970_an_unregistered_view_fails_on_its_null_screen_init(self):
        # Only RegisterView sets MissionScreen and ticks or finalizes the view, so an unregistered one writes no
        # end line: its NULL screen init line is the only evidence.
        self.t.taom("attach_unregistered.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "FAIL")
        self.assertIn("MissionScreen=NULL", out)

    def test_970_a_build_before_1072_is_not_reached(self):
        # Only the legacy "layer attached" line: that build writes no [Check] line, and the legacy line cannot pass.
        self.t.taom("attach_legacy_only.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "NOT REACHED")
        self.assertIn("absence = not-reached", out)

    def test_970_attach_without_a_stance_click_is_not_reached(self):
        self.t.taom("attach_pass_no_stance.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "PASS")
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.stance"), "NOT REACHED")

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


class CastleRecruit918Tests(unittest.TestCase):
    """The real #918 manifest over the day lines CastleRecruitmentService writes. The fixtures render the
    exact strings the C# tests in CastleRecruitmentServiceTests assert; no such line is on disk yet."""

    CHECK = "taom.castle-recruit.ai-off"

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("918.toml")

    def judge(self, fixture):
        self.t.taom(fixture, "2026-10-30_12-57-22")
        return self.t.run(["--issue", "918"])

    def test_three_days_with_a_blocked_recruit_pass(self):
        code, out, _ = self.judge("castle_ai_off_pass.txt")
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, self.CHECK), "PASS")
        self.assertIn("at a castle of your own alignment whose notables are not hostile to you", out)

    def test_an_ai_recruit_while_off_fails(self):
        code, out, _ = self.judge("castle_ai_off_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CHECK), "FAIL")

    def test_two_days_is_too_short_a_run(self):
        code, out, _ = self.judge("castle_ai_off_two_days.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.CHECK), "NOT REACHED")
        self.assertIn("2 of the 3", out)
        # A reload in one process can drop a rerun day, so the tester is told to restart before a retry.
        self.assertIn("restart the game before a retry", out)

    def test_days_with_no_blocked_query_prove_nothing(self):
        # Three pass lines with blocked=0: no AI lord tried a castle, so the run tested nothing.
        code, out, _ = self.judge("castle_ai_off_no_blocks.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.CHECK), "NOT REACHED")

    def test_toggle_on_days_are_skipped(self):
        code, out, _ = self.judge("castle_toggle_on.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.CHECK), "NOT REACHED")
        self.assertIn("skipped: reason=toggle-on", out)


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


class Issue1028Tests(unittest.TestCase):
    """The real #1028 manifest, judged on the lines MissionStartGuardService writes (MissionStartGuardChecks.cs). The literals are
    the ones MissionStartGuardManifestTests renders through the real OutcomeLog, so a change to the line format fails one of the two."""

    STAMP = "[2026-11-12 10:00:00] [INFO] "
    PASS = STAMP + "[Check] id=taom.mission-start-guard.deferred-add result=pass queued=2 added=2"
    SHORT = STAMP + "[Check] id=taom.mission-start-guard.deferred-add result=fail queued=2 added=1 reason=engine-add-threw"
    LISTCHANGE = STAMP + ("[Check] id=taom.mission-start-guard.list-change result=fail call=AfterStart owner=Mod.Foo "
                          "list=MissionBehaviors before=3 after=4 newest=Mod.Newest")
    ESCAPED = ("[2026-11-12 10:00:00] [ERROR] [MissionStartGuard] Mission.AfterStart threw and was not survived "
               "(a call the guard does not wrap, or the toggle is off; 1 of 3 logged per process): boom")
    OTHER = STAMP + "[Session] something else"

    class _Log:
        kind = "taom"
        name = "taom_debug_test.log"

        def __init__(self, lines):
            self.lines = list(enumerate(lines, 1))

    def judge(self, *lines):
        manifest = iv.load_manifest(TOOLS / "ingame_checks" / "1028.toml", 1028)
        return iv.judge_check(manifest.checks[0], [self._Log(lines)], [])

    def test_a_queued_add_that_landed_passes(self):
        self.assertEqual(self.judge(self.PASS).verdict, "PASS")

    def test_a_flush_that_lost_an_add_fails(self):
        self.assertEqual(self.judge(self.SHORT).verdict, "FAIL")

    def test_a_pass_beside_an_unsurvived_throw_fails(self):
        # The flush also runs after a throw; the mission then loads again, so the session is not a pass.
        self.assertEqual(self.judge(self.PASS, self.ESCAPED).verdict, "FAIL")

    def test_a_list_change_inside_the_walk_fails_even_without_a_deferred_add(self):
        self.assertEqual(self.judge(self.LISTCHANGE, self.OTHER).verdict, "FAIL")

    def test_a_session_that_never_started_the_fight_is_not_reached(self):
        self.assertEqual(self.judge(self.OTHER).verdict, "NOT REACHED")


class HeroRace1011Tests(unittest.TestCase):
    """The real #1011 manifest. The fixtures render the [Check] lines RacePersistenceService writes through
    IOutcomeLog (OutcomeLog.Record) and the legend warning text in RestoreHeroRaces; none is on disk yet."""

    CAPTURE = "taom.race-persist.capture"
    RESTORE = "taom.race-persist.restore"

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("1011.toml")

    def one(self, fixture):
        self.t.taom(fixture, "2026-10-31_12-00-00")
        return self.t.run(["--issue", "1011"])

    def test_a_save_then_its_load_pass_both_checks(self):
        self.t.taom("race_save.txt", "2026-10-31_12-00-00")
        self.t.taom("race_load_pass.txt", "2026-10-31_13-00-00")
        code, out, _ = self.t.run(["--issue", "1011", "--sessions", "2"])
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, self.CAPTURE), "PASS")
        self.assertEqual(verdict_of(out, self.RESTORE), "PASS")
        self.assertIn("his son born after that shows the father's race", out)

    def test_a_load_alone_passes_restore_only(self):
        code, out, _ = self.one("race_load_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "PASS")
        self.assertEqual(verdict_of(out, self.CAPTURE), "NOT REACHED")

    def test_a_read_back_mismatch_fails_restore(self):
        code, out, _ = self.one("race_load_mismatch.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "FAIL")
        self.assertIn("first=lord_1_1:4->0", out)

    def test_a_load_with_no_dead_hero_restored_is_not_reached(self):
        code, out, _ = self.one("race_load_no_dead.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "NOT REACHED")
        self.assertIn("reached, but", out)

    def test_a_new_campaign_names_its_skip_reason(self):
        code, out, _ = self.one("race_new_campaign.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "NOT REACHED")
        self.assertIn("skipped: reason=new-campaign", out)

    def test_a_save_with_no_race_record_names_its_skip_reason(self):
        code, out, _ = self.one("race_load_no_record.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "NOT REACHED")
        self.assertIn("skipped: reason=no-record", out)

    def test_a_load_without_race_data_fails_restore(self):
        code, out, _ = self.one("race_load_no_data.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "FAIL")

    def test_a_refused_capture_fails_capture(self):
        code, out, _ = self.one("race_capture_refused.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURE), "FAIL")

    def test_a_save_with_no_dead_hero_fails_capture(self):
        code, out, _ = self.one("race_capture_no_dead.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURE), "FAIL")

    def test_a_later_dead_zero_save_fails_despite_an_earlier_pass_in_the_session(self):
        code, out, _ = self.one("race_capture_mixed.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURE), "FAIL")

    def test_a_load_that_saw_no_dead_hero_fails_restore(self):
        code, out, _ = self.one("race_load_no_dead_seen.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "FAIL")

    def test_a_regressed_load_fails_even_after_an_older_session_passed(self):
        self.t.taom("race_load_pass.txt", "2026-10-31_12-00-00")
        self.t.taom("race_load_no_dead_seen.txt", "2026-10-31_13-00-00")
        code, out, _ = self.t.run(["--issue", "1011", "--sessions", "2"])
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "FAIL")

    def test_a_corrupt_legend_warning_fails_restore_despite_its_pass_line(self):
        code, out, _ = self.one("race_legend_corrupt.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RESTORE), "FAIL")


class SeatOfSeeingManifestTests(unittest.TestCase):
    """The real Seat of Seeing manifest. The fixtures render the [Check] lines SeatSession writes through IOutcomeLog (SeatChecks.cs
    holds the ids; SeatOfSeeingChecksRenderTests in TAOM.Tests pins the rendered lines) and the three failure lines of the feature."""

    NAME, ISSUE = "0.toml", "0"  # the rename changes these two

    ENTER = "taom.seat-of-seeing.enter"
    FRAMES = "taom.seat-of-seeing.frames"
    LEAVE = "taom.seat-of-seeing.leave"
    GLIDE = "taom.seat-of-seeing.glide"
    CURSOR = "taom.seat-of-seeing.cursor"
    FOCUS = "taom.seat-of-seeing.focus"
    REOPENED = "taom.seat-of-seeing.panel-reopened"
    HIDE = "taom.seat-of-seeing.hide"
    KEEP = "taom.seat-of-seeing.keep"
    LET_GO = "taom.seat-of-seeing.let-go"
    FIGHT_ON = "taom.seat-of-seeing.fight-on"
    TAKE = "taom.seat-of-seeing.take"
    TIME = "taom.seat-of-seeing.time"
    RISE = "taom.seat-of-seeing.rise"
    STAGE_ONE = (ENTER, FRAMES, LEAVE, GLIDE)
    NEW = (CURSOR, FOCUS, REOPENED, HIDE, KEEP, LET_GO)
    STAGE_FOUR_FIVE = (FIGHT_ON, TAKE)
    STAGE_SIX_SEVEN = (TIME, RISE)
    EARLIER = STAGE_ONE + NEW
    ALL = EARLIER + STAGE_FOUR_FIVE + STAGE_SIX_SEVEN

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest(self.NAME)

    def judge(self, fixture):
        self.t.taom(fixture, "2026-11-03_14-00-00")
        return self.t.run(["--issue", self.ISSUE])

    def manifest(self):
        return iv.load_manifest(TOOLS / "ingame_checks" / self.NAME, int(self.ISSUE))

    def check(self, check_id):
        return next(c for c in self.manifest().checks if c.id == check_id)

    def test_it_names_the_checks_the_session_writes(self):
        ids = list(self.ALL)
        # The manifest is shared with Marshal's Word, whose checks follow the Seat's.
        self.assertEqual([c.id for c in self.manifest().checks][:len(ids)], ids)
        # The fault id is named by every fail list, and has no check of its own: it must stay at zero. The set is exact, so a check
        # added or renamed without the test is noticed.
        named = {i for i in iv.named_check_ids(self.manifest()) if i.startswith("taom.seat-of-seeing.")}
        self.assertEqual(named, set(ids) | {"taom.seat-of-seeing.fault"})

    def test_a_full_visit_of_stage_one_passes_its_four_checks_and_leaves_the_later_ones_not_reached(self):
        code, out, _ = self.judge("seat_visit_pass.txt")
        self.assertEqual(code, 4, out)  # Marshal's Word's checks, in the same manifest, are not reached in a Seat-only session
        for check_id in self.STAGE_ONE:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)
        for check_id in self.NEW + self.STAGE_FOUR_FIVE + self.STAGE_SIX_SEVEN:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "NOT REACHED", out)
        self.assertIn("the career ability key and the perspective toggle (game key 27) still act from the Seat", out)

    def test_a_full_visit_of_stages_two_to_seven_passes_every_check(self):
        code, out, _ = self.judge("seat_panel_visit_pass.txt")
        self.assertEqual(code, 4, out)  # Marshal's Word's checks, in the same manifest, are not reached in a Seat-only session
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    def test_a_latched_fault_fails_every_check(self):
        code, out, _ = self.judge("seat_fault.txt")
        self.assertEqual(code, 1, out)
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "FAIL", out)

    def test_a_reopen_that_cannot_reach_the_view_model_fails_only_that_check(self):
        code, out, _ = self.judge("seat_panel_reopen_site_missing.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.REOPENED), "FAIL", out)
        self.assertIn("reason=site", out)
        self.assertEqual(verdict_of(out, self.ENTER), "PASS", out)
        self.assertEqual(verdict_of(out, self.CURSOR), "NOT REACHED", out)

    def test_a_reopen_the_engine_refused_reads_not_reached_with_its_reason_and_is_not_a_pass(self):
        code, out, _ = self.judge("seat_panel_reopen_refused.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.REOPENED), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=refused", out)
        self.assertNotRegex(out, r"(?m)^\s*FAIL\s")  # A verdict line: the shared manifest's needs text says FAIL too

    def test_a_press_that_finds_nobody_is_skipped_with_its_reason_and_is_not_a_failure(self):
        code, out, _ = self.judge("seat_keep_nobody.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.KEEP), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=nobody", out)
        self.assertNotRegex(out, r"(?m)^\s*FAIL\s")  # A verdict line: the shared manifest's needs text says FAIL too

    def test_an_interface_that_was_hidden_and_never_restored_fails_and_exits_1(self):
        code, out, _ = self.judge("seat_hide_stuck.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.HIDE), "FAIL", out)

    def test_a_visit_that_never_hid_the_interface_leaves_the_hide_check_not_reached(self):
        code, out, _ = self.judge("seat_enter_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HIDE), "NOT REACHED", out)

    def test_the_hide_check_pairs_nothing_per_mission_and_the_row_says_so(self):
        """A named limit: one mission that restores and a later one that does not still reads PASS."""
        text = (TOOLS / "ingame_checks" / self.NAME).read_text(encoding="utf-8")
        self.assertIn("pairs nothing per mission", text)
        self.assertEqual(self.check(self.HIDE).absence, "fail")

    def test_a_keep_then_a_flight_let_go_passes_both_with_their_kinds(self):
        code, out, _ = self.judge("seat_keep_and_flight.txt")
        self.assertEqual(verdict_of(out, self.KEEP), "PASS", out)
        self.assertEqual(verdict_of(out, self.LET_GO), "PASS", out)

    def test_the_new_pass_patterns_take_their_own_details_and_no_others(self):
        cases = {
            self.CURSOR: ("cursor=shown", "cursor=hidden"),
            self.FOCUS: ("focus=cursor", "focus=centre"),
            self.REOPENED: ("panel=reopened", "panel=closed"),
            self.KEEP: ("kind=Soldier", "kind=Horse"),
        }
        for check_id, (good, bad) in cases.items():
            with self.subTest(check_id=check_id):
                pattern = self.check(check_id).passes[0]
                line = "[Check] id=%s result=pass mission=3 " % check_id
                self.assertTrue(pattern.search(line + good))
                self.assertFalse(pattern.search(line + bad))

    def test_the_hide_pair_reaches_on_the_hide_and_passes_on_a_restore_with_a_known_reason(self):
        check = self.check(self.HIDE)
        line = "[Check] id=taom.seat-of-seeing.hide result=pass mission=3 interface=%s"
        self.assertTrue(check.reached[0].search(line % "hidden"))
        self.assertFalse(check.reached[0].search(line % "restored reason=key"))
        for reason in ("key", "panel", "dialog", "layer", "leave", "fault"):
            with self.subTest(reason=reason):
                self.assertTrue(check.passes[0].search(line % ("restored reason=" + reason)))
        self.assertFalse(check.passes[0].search(line % "restored reason=whim"))

    def test_the_let_go_pass_pattern_takes_each_reason_and_kind(self):
        pattern = self.check(self.LET_GO).passes[0]
        line = "[Check] id=taom.seat-of-seeing.let-go result=pass mission=3 reason=%s kind=%s"
        for reason in ("key", "flight", "gone"):
            for kind in ("Soldier", "Company"):
                with self.subTest(reason=reason, kind=kind):
                    self.assertTrue(pattern.search(line % (reason, kind)))
        self.assertFalse(pattern.search(line % ("boredom", "Soldier")))
        self.assertFalse(pattern.search(line % ("leave", "Soldier")), "a leave only says the Seat was left with a target kept")
        self.assertFalse(pattern.search(line % ("leave", "Company")))
        self.assertFalse(pattern.search(line % ("key", "Horse")))
        self.assertFalse(pattern.search("[Check] id=taom.seat-of-seeing.let-go result=pass mission=3 reason=key"), "a line with no kind")

    def test_a_keep_then_a_leave_alone_reaches_the_let_go_check_and_does_not_pass_it(self):
        code, out, _ = self.judge("seat_keep_then_leave.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.KEEP), "PASS", out)
        self.assertEqual(verdict_of(out, self.LET_GO), "NOT REACHED", out)

    def test_the_closed_keys_have_a_human_row_and_the_career_key_row_is_narrowed(self):
        claims = list(self.manifest().human)
        self.assertTrue(any("game keys 13 and 22" in c and "does nothing" in c for c in claims), claims)
        self.assertFalse(any(c == "the drop key and the career key still act from the Seat" for c in claims))
        self.assertTrue(any("(game key 27) still act from the Seat" in c for c in claims))

    def test_the_human_rows_of_the_second_pass_are_in_the_manifest_and_the_wrong_claims_are_gone(self):
        claims = list(self.manifest().human)
        for fragment in ("in a town, an indoor scene, a village", "a Transfer order from the panel", "the pointer stays where it was",
                         "left pad down from the Seat opens no walk mode control", "a soldier behind a hill is not picked",
                         "Insert pressed in the Seat takes no screenshot", "the hero stands under real movement keys",
                         "with the keys Insert and End", "also game key 77", "does not press it yet: it gives a Move order"):
            with self.subTest(fragment=fragment):
                self.assertTrue(any(fragment in c for c in claims), fragment)
        self.assertFalse(any("the drop and equip key (game key 22), the career" in c for c in claims), "key 22 is closed by stage S2")
        self.assertFalse(any("the walk mode key (30)" in c for c in claims), "game key 30 is read by a controller the Seat already shuts")
        self.assertTrue(any("letting go by the key or by flying shows no message" in c for c in claims))

    def test_a_missing_elevation_setter_fails_only_the_glide(self):
        code, out, _ = self.judge("seat_elevation_missing.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.GLIDE), "FAIL")
        self.assertIn("elevation=missing", out)
        for check_id in (self.ENTER, self.FRAMES, self.LEAVE):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    def test_an_enter_with_no_later_line_passes_enter_and_leaves_the_rest_not_reached(self):
        code, out, _ = self.judge("seat_enter_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.ENTER), "PASS")
        for check_id in (self.FRAMES, self.LEAVE, self.GLIDE):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "NOT REACHED", out)

    def test_each_failure_line_of_the_feature_fails_the_enter_check(self):
        for fixture in ("seat_could_not_start.txt", "seat_hotkey_failed.txt", "seat_order_ray_failed.txt", "seat_enter_failed.txt", "seat_patch_failed.txt",
                        "seat_order_panel_anchor.txt"):
            with self.subTest(fixture=fixture):
                self.t = Tree(self)
                self.t.real_manifest(self.NAME)
                code, out, _ = self.judge(fixture)
                self.assertEqual(code, 1, out)
                self.assertEqual(verdict_of(out, self.ENTER), "FAIL", out)

    def test_the_order_panel_line_fails_the_hand_off_check_too(self):
        code, out, _ = self.judge("seat_order_panel_anchor.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "FAIL", out)

    def test_every_error_line_the_feature_writes_at_a_fixed_prefix_fails_the_enter_check(self):
        """A new LogError of the Seat that no fail pattern names would read PASS under --issue 0 while the trial judge fails the run."""
        found = []
        for root in (REPO / "Client/Features/SeatOfSeeing", REPO / "Main/Features/SeatOfSeeing"):
            for path in root.rglob("*.cs"):
                found += re.findall(r'LogError\(\s*\$?"(\[SeatOfSeeing\] [^"{:]+)', path.read_text(encoding="utf-8"))
        found = [line for line in found if "the Seat failed" not in line]  # the fault line has its own check
        self.assertGreaterEqual(len(found), 4, found)
        fails = self.check(self.ENTER).fails
        for line in found:
            with self.subTest(line=line):
                self.assertTrue(any(p.search(line) for p in fails), line)

    def test_a_session_of_refused_key_presses_is_not_reached_and_says_why(self):
        code, out, _ = self.judge("seat_skips_only.txt")
        self.assertEqual(code, 4, out)
        for check_id in (self.ENTER, self.FRAMES, self.LEAVE, self.GLIDE):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=setting-off", out)

    def test_a_leave_with_no_agent_to_follow_skips_the_glide_with_its_reason(self):
        code, out, _ = self.judge("seat_visit_no_agent.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.LEAVE), "PASS")
        self.assertEqual(verdict_of(out, self.GLIDE), "NOT REACHED")
        self.assertIn("skipped: mission=1 reason=no-agent", out)

    def test_the_leave_pass_pattern_takes_each_reason_and_no_other(self):
        pattern = self.check(self.LEAVE).passes[0]
        line = "[Check] id=taom.seat-of-seeing.leave result=pass mission=3 frames=12 glide=1 reason=%s"
        for reason in ("key", "setting-off", "interlude", "rage", "finish"):
            with self.subTest(reason=reason):
                self.assertTrue(pattern.search(line % reason))
        self.assertFalse(pattern.search(line % "teleport"))
        self.assertFalse(pattern.search(line % "key-and-more"))

    def test_the_dead_leave_fail_pattern_is_gone_and_the_fault_pattern_is_on_every_check(self):
        self.assertEqual([p.pattern for p in self.check(self.LEAVE).fails],
                         [r"\[Check\] id=taom\.seat-of-seeing\.fault result=fail"])
        fault = r"\[Check\] id=taom\.seat-of-seeing\.fault result=fail"
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertIn(fault, [p.pattern for p in self.check(check_id).fails])

    def test_a_visit_with_a_level_step_only_reaches_the_time_check_and_does_not_pass_it(self):
        code, out, _ = self.judge("seat_time_level_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.TIME), "NOT REACHED", out)
        self.assertIn("event=stay", out)

    def test_refused_time_keys_read_not_reached_and_say_why(self):
        code, out, _ = self.judge("seat_time_skips.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.TIME), "NOT REACHED", out)
        # The verdict prints the first skip line it finds; the other two reasons are in the fixture to be read as skips and not as failures.
        self.assertIn("skipped: mission=1 reason=denied", out)
        self.assertNotRegex(out, r"(?m)^\s*FAIL\s")  # A verdict line: the shared manifest's needs text says FAIL too

    def test_a_cancel_that_left_mission_time_on_fails_the_time_check_and_exits_1(self):
        code, out, _ = self.judge("seat_time_cancel_failed.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.TIME), "FAIL", out)
        self.assertIn("reason=cancel-failed", out)

    def test_the_time_pass_patterns_need_a_level_a_stay_and_a_resume_and_no_level_the_ladder_lacks(self):
        level, stay, resume = self.check(self.TIME).passes
        line = "[Check] id=taom.seat-of-seeing.time result=pass mission=3 level=%s event=%s"
        for name in ("0.25", "0.5", "1", "2", "4", "10"):
            with self.subTest(level=name):
                self.assertTrue(level.search(line % (name, "level")))
        self.assertTrue(stay.search(line % ("1", "stay")))
        self.assertTrue(resume.search(line % ("1", "resume")))
        for bad_level in ("3", "0.3", "100", "2x"):
            with self.subTest(bad_level=bad_level):
                self.assertFalse(level.search(line % (bad_level, "level")))
        for event, pattern in (("stay", level), ("level", stay), ("resume", stay), ("stay", resume), ("level-and-more", level)):
            with self.subTest(event=event):
                self.assertFalse(pattern.search(line % ("1", event)))

    def test_the_rise_pattern_takes_only_the_two_heights_and_a_mission_without_a_lift_is_not_reached(self):
        (height,) = self.check(self.RISE).passes
        line = "[Check] id=taom.seat-of-seeing.rise result=pass mission=3 height=%s"
        self.assertTrue(height.search(line % "7.0"))
        self.assertTrue(height.search(line % "3.0"))
        for bad in ("0.0", "5.0", "7", "7.0-and-more", "17.0"):
            with self.subTest(height=bad):
                self.assertFalse(height.search(line % bad))
        code, out, _ = self.judge("seat_visit_pass.txt")
        self.assertEqual(verdict_of(out, self.RISE), "NOT REACHED", out)

    def test_the_time_and_rise_human_rows_ask_what_only_a_person_can_see(self):
        claims = list(self.manifest().human)
        for fragment in ("step mission time through 0.25, 0.5, 1, 2, 4 and 10", "under a pause the Seat's view still flies",
                         "level 10 outside the Seat with a living hero stops at 4", "the battle has its own speed control",
                         "any slow time, fast time or pause they made ends at the next press of a time key", "lifts the view over the ranks",
                         "the target lock key starts no lock", "under a gatehouse or a roof the view rises through it",
                         "Quicken time (TAOM) with Page Up", "the first F after the Seat's level 10 writes true and changes nothing"):
            with self.subTest(fragment=fragment):
                self.assertTrue(any(fragment in c for c in claims), fragment)

    def test_a_visit_of_the_hand_off_passes_fight_on_and_leaves_take_not_reached(self):
        code, out, _ = self.judge("seat_fight_visit_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "PASS", out)
        self.assertEqual(verdict_of(out, self.TAKE), "NOT REACHED", out)

    def test_a_visit_of_the_hand_off_and_a_take_passes_both_checks(self):
        code, out, _ = self.judge("seat_take_visit_pass.txt")
        self.assertEqual(code, 4, out)  # Marshal's Word's checks, in the same manifest, are not reached in a Seat-only session
        for check_id in self.STAGE_FOUR_FIVE:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    def test_a_hand_off_with_no_hand_back_is_reached_and_not_passed(self):
        """The mission ended inside the stay: Finish writes no agent and no end line, so the round trip is not proved."""
        code, out, _ = self.judge("seat_fight_no_end.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "NOT REACHED", out)
        self.assertIn("event=end", out)

    def test_a_refused_hand_off_reads_not_reached_with_its_reason(self):
        code, out, _ = self.judge("seat_fight_refused.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=refused why=not-player", out)
        self.assertNotRegex(out, r"(?m)^\s*FAIL\s")  # A verdict line: the shared manifest's needs text says FAIL too

    def test_a_throw_from_the_hand_off_fails_the_check_and_exits_1(self):
        code, out, _ = self.judge("seat_fight_throw.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "FAIL", out)

    def test_a_take_that_handed_over_no_formation_is_reached_and_not_passed(self):
        code, out, _ = self.judge("seat_take_no_command.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.TAKE), "NOT REACHED", out)
        self.assertIn("formations=[1-9]", out)

    def test_a_hero_who_fell_while_handed_is_reached_with_no_end_line_and_the_reason_is_not_printed(self):
        code, out, _ = self.judge("seat_fight_fell.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIGHT_ON), "NOT REACHED", out)
        self.assertIn("event=end", out)
        self.assertNotIn("reason=fell", out)

    def test_refused_takes_read_not_reached_and_say_why(self):
        code, out, _ = self.judge("seat_take_skips.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.TAKE), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=nobody", out)

    def test_the_fight_on_pass_patterns_need_the_begin_and_the_end_and_take_no_other_event(self):
        begin, end = self.check(self.FIGHT_ON).passes
        line = "[Check] id=taom.seat-of-seeing.fight-on result=pass mission=3 event=%s"
        self.assertTrue(begin.search(line % "begin"))
        self.assertTrue(end.search(line % "end"))
        for other in ("retreat-stopped", "fell", "begin-and-more"):
            with self.subTest(event=other):
                self.assertFalse(begin.search(line % other))
                self.assertFalse(end.search(line % other))
        self.assertFalse(begin.search("[Check] id=taom.seat-of-seeing.fight-on result=skip mission=3 event=begin"))

    def test_the_take_pass_pattern_takes_each_kind_and_at_least_one_formation(self):
        pattern = self.check(self.TAKE).passes[0]
        line = "[Check] id=taom.seat-of-seeing.take result=pass mission=3 kind=%s formations=%s"
        for kind in ("Soldier", "Hero"):
            for count in ("1", "3", "12"):
                with self.subTest(kind=kind, count=count):
                    self.assertTrue(pattern.search(line % (kind, count)))
        self.assertFalse(pattern.search(line % ("Soldier", "0")), "a take that handed nothing over proves no hand-over")
        self.assertFalse(pattern.search(line % ("Horse", "3")))
        self.assertFalse(pattern.search("[Check] id=taom.seat-of-seeing.take result=pass mission=3 kind=Soldier"), "a line with no formation count")

    def test_the_hand_off_and_take_rows_are_named_and_the_human_rows_ask_what_only_a_person_can_see(self):
        claims = list(self.manifest().human)
        for fragment in ("fighting inside his company", "leaving the Seat with the hero fighting on gives the controls back", "where an AI hero stands",
                         "a hero on a ladder or in a ladder queue", "outside the mission boundary", "one message names the Take this soldier key",
                         "Take this soldier (TAOM)", "keeps the orders: every company the fallen hero owned", "quit text reads Retreat",
                         "vanilla Kick key", "in a campaign field battle (the one check owed beyond the custom battle)", "refused with one message",
                         "a hero of another party is refused (reason=hero)", "a soldier on the allied team is refused (reason=other-team)",
                         "a plain soldier of a party under the player's command or in his army", "Cannot issue order while dead.",
                         "a howdah or mumak crew member, a soldier who belongs to no formation"):
            with self.subTest(fragment=fragment):
                self.assertTrue(any(fragment in c for c in claims), fragment)

    def test_the_seats_checks_come_before_marshals_word_and_its_rows_stay_in_order(self):
        ids = [c.id for c in self.manifest().checks]
        seat = [i for i, c in enumerate(ids) if c.startswith("taom.seat-of-seeing.")]
        marshal = [i for i, c in enumerate(ids) if c.startswith("taom.marshals-word.")]
        self.assertEqual(seat, list(range(len(seat))))
        self.assertGreater(min(marshal), max(seat))
        self.assertEqual(ids[:len(self.ALL)], list(self.ALL))

    def test_the_enter_fail_list_names_the_features_own_failure_lines(self):
        fails = [p.pattern for p in self.check(self.ENTER).fails]
        for line in (r"\[SeatOfSeeing\] the Seat could not start", r"\[SeatOfSeeing\] hotkey registration failed",
                     r"\[SeatOfSeeing\] order ray failed", r"\[SeatOfSeeing\] expected exactly one Agent\.IsPlayerControlled read",
                     r"\[Check\] id=taom\.seat-of-seeing\.enter result=fail",
                     r"\[PatchApply\] \S*Patch140_\S* (?:FAILED|SKIPPED)",
                     r"\[ERROR\] \[TAOM\] Seat(?:Hero|Screen)Adapter\.\w+ ran off the main mission thread"):
            with self.subTest(line=line):
                self.assertIn(line, fails)

    def test_the_seats_thread_guard_line_fails_the_enter_check_and_another_features_guard_line_does_not(self):
        patterns = self.check(self.ENTER).fails
        tail = " ran off the main mission thread (thread 7, main 1). An engine callback here is parked for the next mission tick, which is expected; a TAOM blow, creature tree or grid read here is a regression (#592, #595, #634)."
        for site in ("SeatScreenAdapter.SetPanelLift", "SeatScreenAdapter.ApplyFastTime", "SeatHeroAdapter.StandFast"):
            with self.subTest(site=site):
                self.assertTrue(any(p.search("[2026-11-04 10:00:05] [ERROR] [TAOM] " + site + tail) for p in patterns), site)
        for other in ("MarshalOrderAdapter.QueueFlag", "FordsMissionAdapter.Apply", "SeatScreen.NotAnAdapter"):
            with self.subTest(other=other):
                self.assertFalse(any(p.search("[2026-11-04 10:00:05] [ERROR] [TAOM] " + other + tail) for p in patterns), other)
        self.assertFalse(any(p.search("[2026-11-04 10:00:05] [INFO] [TAOM] SeatScreenAdapter.SetPanelLift" + tail) for p in patterns), "only an [ERROR] line counts")

    def test_a_seat_patch_that_failed_or_was_skipped_fails_the_enter_check_and_another_features_does_not(self):
        patterns = self.check(self.ENTER).fails
        failed = "[2026-11-04 10:00:05] [ERROR] [PatchApply] Patch140_SeatOfSeeing FAILED (Harmony stops a category at its first failing class): boom"
        skipped = ("[2026-11-04 10:00:05] [ERROR] [PatchApply] TAOM.Features.SeatOfSeeing.Hooks.Patch140_CrosshairArrows SKIPPED "
                   "(its attributes cannot be read, so its patches are off; every other class still applies): boom")
        other = "[2026-11-04 10:00:05] [ERROR] [PatchApply] Patch95_FactionUI FAILED (Harmony stops a category at its first failing class): boom"
        info = "[2026-11-04 10:00:05] [INFO] [PatchApply] category applied max_category=Patch140_SeatOfSeeing"
        self.assertTrue(any(p.search(failed) for p in patterns))
        self.assertTrue(any(p.search(skipped) for p in patterns))
        self.assertFalse(any(p.search(other) for p in patterns))
        self.assertFalse(any(p.search(info) for p in patterns))


class MarshalsWordManifestTests(unittest.TestCase):
    """The Marshal's Word checks of the shared draft manifest. The fixtures render the [Check] lines the session and the two drivers write
    through IOutcomeLog (MarshalChecks.cs holds the ids). A session with no Seat lines leaves the Seat's checks NOT REACHED, so a
    run of these fixtures exits 4 unless a check fails."""

    NAME, ISSUE = SeatOfSeeingManifestTests.NAME, SeatOfSeeingManifestTests.ISSUE  # the manifest is shared, and so is its rename

    PREFIX = "taom.marshals-word."
    CAPTURED = PREFIX + "captured"
    STARTED = PREFIX + "started"
    FINISHED = PREFIX + "finished"
    PATIENCE = PREFIX + "patience"
    CLEARED = PREFIX + "cleared"
    DECLINED = PREFIX + "declined"
    ARRAY = PREFIX + "array"
    MARCH = PREFIX + "march"
    SPREAD = PREFIX + "march-spread"
    FIRE = PREFIX + "fire"
    WORD = PREFIX + "word"
    HOLD = PREFIX + "volley-hold"
    RELEASE = PREFIX + "volley-release"
    MARK = PREFIX + "mark"
    EFFECT = PREFIX + "mark-effect"
    MASSED = PREFIX + "massed"
    HOLD_TOGGLE = PREFIX + "hold"
    HOLD_LINE = PREFIX + "hold-line"
    HOLD_SLOT = PREFIX + "hold-slot"
    HOLD_STRIKE = PREFIX + "hold-strike"
    SQUARE = PREFIX + "square"
    SPACING = PREFIX + "spacing"
    THROWERS_STAND = PREFIX + "throwers-stand"
    THROWERS_THROW = PREFIX + "throwers-throw"
    THROWERS_MELEE = PREFIX + "throwers-melee"
    THROWERS_COST = PREFIX + "throwers-cost"
    STAGE_TWO = [CAPTURED, STARTED, FINISHED, PATIENCE, CLEARED, DECLINED, ARRAY, MARCH, SPREAD]
    NEW = [FIRE, WORD, HOLD, RELEASE, MARK, EFFECT, MASSED, HOLD_TOGGLE, HOLD_LINE, HOLD_SLOT, HOLD_STRIKE, SQUARE, SPACING, THROWERS_STAND, THROWERS_THROW, THROWERS_MELEE, THROWERS_COST]
    ALL = STAGE_TWO + NEW

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest(self.NAME)

    def judge(self, fixture):
        self.t.taom(fixture, "2026-11-04_14-00-00")
        return self.t.run(["--issue", self.ISSUE])

    def manifest(self):
        return iv.load_manifest(TOOLS / "ingame_checks" / self.NAME, int(self.ISSUE))

    def check(self, check_id):
        return next(c for c in self.manifest().checks if c.id == check_id)

    def test_the_manifest_names_the_checks_the_session_writes_after_the_seats_in_stage_order(self):
        ids = [c.id for c in self.manifest().checks]
        self.assertEqual(ids[len(SeatOfSeeingManifestTests.ALL):], self.ALL)
        # The fault id is named by every fail list, and has no check of its own: it must stay at zero. The set is exact.
        named = {i for i in iv.named_check_ids(self.manifest()) if i.startswith(self.PREFIX)}
        self.assertEqual(named, set(self.ALL) | {self.PREFIX + "fault"})
        fault = r"\[Check\] id=taom\.marshals-word\.fault result=fail"
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertIn(fault, [p.pattern for p in self.check(check_id).fails])

    def test_every_error_line_the_feature_writes_at_its_fixed_prefix_fails_the_captured_check(self):
        folders = [REPO / "Main" / "Features" / "MarshalsWord", REPO / "Client" / "Features" / "MarshalsWord"]
        files = [path for folder in folders for path in folder.rglob("*.cs")]
        files += [path for adapters in (REPO / "Main" / "Adapters", REPO / "Client" / "Adapters") for path in adapters.glob("Marshal*.cs")]
        written = []
        for path in files:
            written += re.findall(r'LogError\(\s*\$?"(\[MarshalsWord\][^"]*)"', path.read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(written), 4, "the scan found the lines the feature writes: " + repr(written))
        fails = self.check(self.CAPTURED).fails
        for text in written:
            line = "[2026-11-04 14:00:00] [ERROR] " + re.sub(r"\{[^}]*\}", "x", text)
            with self.subTest(line=line):
                self.assertTrue(any(pattern.search(line) for pattern in fails), "no fail pattern of the captured check matches: " + line)


    # ---- Massed fire, hold the line, the square and the spacing repair ----

    def test_a_massed_word_and_the_volley_after_it_pass_the_massed_check_with_the_eligible_count_on_the_line(self):
        code, out, _ = self.judge("marshal_massed_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.MASSED), "PASS", out)
        self.assertEqual(verdict_of(out, self.RELEASE), "PASS", out)
        self.assertIn("eligible=28", out)

    def test_a_massed_word_that_the_archers_did_not_answer_fails_the_massed_check_and_the_release_check(self):
        code, out, _ = self.judge("marshal_massed_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.MASSED), "FAIL", out)
        self.assertEqual(verdict_of(out, self.RELEASE), "FAIL", out)

    def test_a_massed_word_given_to_too_few_holders_reads_not_reached_with_its_reason(self):
        code, out, _ = self.judge("marshal_massed_few.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.MASSED), "NOT REACHED", out)
        self.assertIn("reason=too-few-holding", out)

    def test_the_massed_pass_needs_five_holding_and_no_fail_line(self):
        pattern = self.check(self.MASSED).passes[0]
        self.assertTrue(pattern.search("[Check] id=taom.marshals-word.massed result=pass mission=1 company=1 holding=5 shots=4 eligible=6 stale=0"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.massed result=pass mission=1 company=1 holding=4 shots=4 eligible=6 stale=0"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.massed result=skip mission=1 reason=too-few-holding company=1 holding=3"))

    def test_both_presses_of_hold_the_line_are_needed_for_the_hold_check(self):
        code, out, _ = self.judge("marshal_hold_line_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HOLD_TOGGLE), "PASS", out)
        self.assertEqual(verdict_of(out, self.HOLD_LINE), "PASS", out)
        code, out, _ = self.judge("marshal_hold_line_fail.txt")
        self.assertEqual(verdict_of(out, self.HOLD_TOGGLE), "NOT REACHED", out)

    def test_a_held_company_that_struck_as_often_as_the_control_fails_the_hold_line_check(self):
        code, out, _ = self.judge("marshal_hold_line_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.HOLD_LINE), "FAIL", out)
        self.assertIn("blows=37", out)

    def test_a_control_that_fought_too_little_reads_not_reached_with_its_reason(self):
        code, out, _ = self.judge("marshal_hold_line_no_control.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HOLD_LINE), "NOT REACHED", out)
        self.assertIn("reason=no-control", out)

    def test_a_quiet_control_skip_followed_by_a_busy_windows_pass_reads_pass(self):
        code, out, _ = self.judge("marshal_hold_line_quiet_then_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HOLD_LINE), "PASS", out)

    def test_a_clamp_that_ended_more_soldiers_nearer_passes_and_one_that_walked_them_away_fails(self):
        code, out, _ = self.judge("marshal_hold_slot_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HOLD_SLOT), "PASS", out)
        code, out, _ = self.judge("marshal_hold_slot_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.HOLD_SLOT), "FAIL", out)
        self.assertIn("closer=2 farther=6", out)

    def test_the_slot_pass_names_the_order_and_needs_five_qualified_soldiers(self):
        pattern = self.check(self.HOLD_SLOT).passes[0]
        line = "[Check] id=taom.marshals-word.hold-slot result=pass mission=1 company=1 qualified=%s far=1 order=%s steered=8 closer=6 farther=2"
        self.assertTrue(pattern.search(line % (5, "Stop")))
        self.assertFalse(pattern.search(line % (4, "Stop")))
        self.assertFalse(pattern.search(line % (30, "Move")), "a moving slot is never judged, so a pass line for it proves nothing")
        self.assertFalse(pattern.search(line % (30, "None")))

    def test_the_slot_pass_carries_the_numbers_it_judged_and_a_line_without_them_does_not_match(self):
        pattern = self.check(self.HOLD_SLOT).passes[0]
        bare = "[Check] id=taom.marshals-word.hold-slot result=pass mission=1 company=1 qualified=30 far=1 order=Stop"
        self.assertFalse(pattern.search(bare), "the line that proves the slot names the steered reads and the two sides")
        self.assertTrue(pattern.search(bare + " steered=8 closer=6 farther=2"))
        self.assertFalse(pattern.search(bare + " steered=8 closer=6"))

    def test_the_slot_pass_needs_the_five_steered_reads_the_check_waits_for(self):
        pattern = self.check(self.HOLD_SLOT).passes[0]
        bare = "[Check] id=taom.marshals-word.hold-slot result=pass mission=1 company=1 qualified=30 far=1 order=Stop"
        for steered in ("0", "1", "4", "05"):
            self.assertFalse(pattern.search(bare + " steered=%s closer=4 farther=0" % steered), "steered=" + steered)
        for steered in ("5", "8", "12", "48"):
            self.assertTrue(pattern.search(bare + " steered=%s closer=5 farther=0" % steered), "steered=" + steered)

    def test_a_wound_answered_with_the_flag_passes_the_strike_check_and_one_left_disarmed_fails(self):
        code, out, _ = self.judge("marshal_hold_strike_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.HOLD_STRIKE), "PASS", out)
        code, out, _ = self.judge("marshal_hold_strike_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.HOLD_STRIKE), "FAIL", out)
        self.assertIn("flagOn=0", out)

    def test_the_strike_pass_needs_a_settled_wound_and_a_flag_that_is_on(self):
        pattern = self.check(self.HOLD_STRIKE).passes[0]
        line = "[Check] id=taom.marshals-word.hold-strike result=pass mission=1 company=1 struck=%s flagOn=%s"
        self.assertTrue(pattern.search(line % (3, 2)))
        self.assertFalse(pattern.search(line % (0, 0)))
        self.assertFalse(pattern.search(line % (3, 0)))

    def test_a_square_that_gave_every_soldier_a_slot_passes_and_one_that_left_two_without_fails(self):
        code, out, _ = self.judge("marshal_square_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SQUARE), "PASS", out)
        code, out, _ = self.judge("marshal_square_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.SQUARE), "FAIL", out)
        self.assertIn("placed=38", out)

    def test_a_square_whose_soldiers_take_their_places_from_the_mixed_layout_is_not_a_pass_of_the_square_row(self):
        code, out, _ = self.judge("marshal_square_laid_out.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SQUARE), "NOT REACHED", out)
        code, out, _ = self.judge("marshal_square_pass.txt")
        self.assertEqual(verdict_of(out, self.SQUARE), "PASS", out)

    def test_the_square_pass_needs_the_square_alone_to_place_the_soldiers(self):
        pattern = self.check(self.SQUARE).passes[0]
        line = "[Check] id=taom.marshals-word.square result=pass mission=1 company=1 side=14.5 files=9 ranks=5 units=40 placed=40 laidOut=%s"
        self.assertTrue(pattern.search(line % 0))
        self.assertFalse(pattern.search(line % 1), "the side has no effect on a company the layout places, so its line proves nothing")

    def test_a_spacing_transpiler_that_left_a_method_as_it_was_fails_the_spacing_check_and_the_captured_check(self):
        code, out, _ = self.judge("marshal_spacing_degraded.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.SPACING), "FAIL", out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "FAIL", out)

    def test_a_swallowed_callback_fault_fails_the_rows_whose_proof_rests_on_the_callback(self):
        code, out, _ = self.judge("marshal_component_fault.txt")
        self.assertEqual(code, 1, out)
        for check_id in (self.HOLD, self.MASSED, self.HOLD_SLOT):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "FAIL", out)

    def test_every_site_the_component_swallows_at_fails_the_rows_that_rest_on_the_callback(self):
        source = (REPO / "Main" / "Features" / "MarshalsWord" / "MarshalWordComponent.cs").read_text(encoding="utf-8")
        sites = re.findall(r'Outcomes\.Swallowed\("MarshalsWord", "(MarshalWordComponent\.\w+)"', source)
        self.assertTrue(sites, "the component swallows at its callback")
        for site in sites:
            line = "[2026-11-04 14:00:00] [WARNING] [Swallowed] feature=MarshalsWord site=" + site + " first=NullReferenceException. This line is logged once per site"
            for check_id in (self.HOLD, self.MASSED, self.HOLD_SLOT):
                with self.subTest(site=site, check_id=check_id):
                    self.assertTrue(any(pattern.search(line) for pattern in self.check(check_id).fails), "no fail pattern of " + check_id + " matches " + line)

    def test_limits_that_read_as_foot_soldiers_pass_the_spacing_check_and_cavalry_limits_fail_it(self):
        code, out, _ = self.judge("marshal_spacing_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SPACING), "PASS", out)
        code, out, _ = self.judge("marshal_spacing_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.SPACING), "FAIL", out)
        self.assertIn("taom.marshals-word.spacing result=fail", out)

    def test_a_repair_that_is_not_applied_reads_not_reached_with_the_reason_it_names(self):
        code, out, _ = self.judge("marshal_spacing_not_applied.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SPACING), "NOT REACHED", out)
        self.assertIn("reason=coop", out)

    def test_the_spacing_pass_needs_a_rider_in_the_company(self):
        pattern = self.check(self.SPACING).passes[0]
        line = "[Check] id=taom.marshals-word.spacing result=pass mission=1 company=1 units=41 riders=%d minDistance=0.00 maxDistance=0.40 minInterval=0.00 maxInterval=0.38 infantry=0.00/0.40/0.00/0.38 cavalry=1.70/2.00/0.18/0.50 reads=Infantry"
        self.assertTrue(pattern.search(line % 1))
        self.assertFalse(pattern.search(line % 0))

    def test_a_full_session_passes_every_check(self):
        code, out, _ = self.judge("marshal_full_pass.txt")
        self.assertEqual(code, 4, out)  # every one of the Seat's checks is absent from this fixture, so each reads not reached
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    def test_a_latched_fault_fails_every_check_it_can_reach(self):
        code, out, _ = self.judge("marshal_fault.txt")
        self.assertEqual(code, 1, out)
        for check_id in self.ALL:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "FAIL", out)

    def test_a_hold_that_lets_arrows_go_fails_the_hold_check_and_only_it(self):
        code, out, _ = self.judge("marshal_volley_hold_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.HOLD), "FAIL", out)
        self.assertEqual(verdict_of(out, self.RELEASE), "NOT REACHED", out)
        self.assertEqual(verdict_of(out, self.FIRE), "PASS", out)

    def test_a_word_that_looses_too_few_fails_the_release_check_and_leaves_the_hold_check_passing(self):
        code, out, _ = self.judge("marshal_volley_release_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.RELEASE), "FAIL", out)
        self.assertEqual(verdict_of(out, self.HOLD), "PASS", out)
        self.assertEqual(verdict_of(out, self.WORD), "PASS", out)

    def test_a_marked_ranged_company_that_draws_no_aim_fails_the_effect_check(self):
        code, out, _ = self.judge("marshal_mark_effect_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.EFFECT), "FAIL", out)
        self.assertEqual(verdict_of(out, self.MARK), "PASS", out)

    def test_the_effect_check_passes_on_the_ranged_case_alone_so_one_battle_reaches_it(self):
        code, out, _ = self.judge("marshal_mark_effect_ranged_pass.txt")
        self.assertEqual(code, 4, out)  # the other checks of this one-battle log are not reached
        self.assertEqual(verdict_of(out, self.EFFECT), "PASS", out)

    def test_the_effect_check_names_one_context_and_no_melee_case(self):
        passes = [p.pattern for p in self.check(self.EFFECT).passes]
        self.assertEqual(len(passes), 1)
        self.assertIn("context=RangedOnStop", passes[0])
        self.assertNotIn("MeleeOnMove", (TOOLS / "ingame_checks" / self.NAME).read_text(encoding="utf-8"))

    def test_the_skip_lines_of_the_new_keys_read_not_reached_with_their_reason(self):
        code, out, _ = self.judge("marshal_m3_m4_skips.txt")
        self.assertEqual(code, 4, out)
        for check_id, reason in ((self.FIRE, "reason=ai-controlled"), (self.WORD, "reason=mode-free"), (self.MARK, "reason=not-applied")):
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "NOT REACHED", out)
                self.assertIn(reason, out)

    def test_the_word_checks_reached_line_is_the_fire_pass_and_not_its_skip(self):
        pattern = self.check(self.WORD).reached[0]
        self.assertTrue(pattern.search("[Check] id=taom.marshals-word.fire result=pass mission=1 mode=OnWord company=1"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.fire result=skip mission=1 reason=ai-controlled"))

    def test_the_hold_and_release_passes_name_their_numbers_so_a_report_shows_holding_and_shots(self):
        for check_id in (self.HOLD, self.RELEASE):
            with self.subTest(check_id=check_id):
                pattern = self.check(check_id).passes[0].pattern
                self.assertIn("holding=[0-9]+", pattern)
                self.assertIn("shots=[0-9]+", pattern)

    def test_one_press_of_the_fire_key_is_not_a_round_trip_and_reads_not_reached(self):
        code, out, _ = self.judge("marshal_fire_one_press.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIRE), "NOT REACHED", out)
        self.assertIn("mode=Free", out)

    def test_a_word_given_with_nobody_holding_is_not_a_pass(self):
        code, out, _ = self.judge("marshal_word_nobody_holding.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FIRE), "PASS", out)
        self.assertEqual(verdict_of(out, self.WORD), "NOT REACHED", out)

    def test_the_word_pattern_takes_five_or_more_holding_and_nothing_under(self):
        pattern = self.check(self.WORD).passes[0]
        line = "[Check] id=taom.marshals-word.word result=pass mission=1 company=1 holding=%s"
        for holding in ("5", "6", "12", "40"):
            with self.subTest(holding=holding):
                self.assertTrue(pattern.search(line % holding))
        for holding in ("0", "4", "05"):
            with self.subTest(holding=holding):
                self.assertFalse(pattern.search(line % holding))

    def test_each_failure_line_of_the_feature_fails_the_capture_check(self):
        for fixture in ("marshal_could_not_start.txt", "marshal_hotkey_failed.txt", "marshal_release_failed.txt", "marshal_patch_failed.txt", "marshal_patch_skipped.txt"):
            with self.subTest(fixture=fixture):
                self.t = Tree(self)
                self.t.real_manifest(self.NAME)
                code, out, _ = self.judge(fixture)
                self.assertEqual(code, 1, out)
                self.assertEqual(verdict_of(out, self.CAPTURED), "FAIL", out)

    def test_a_session_that_never_went_live_says_why_and_is_not_reached(self):
        code, out, _ = self.judge("marshal_session_off.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "NOT REACHED", out)
        self.assertIn("skipped: mission=1 reason=setting-off", out)

    def test_a_captured_step_that_never_starts_fails_the_started_check(self):
        code, out, _ = self.judge("marshal_captured_never_started.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "PASS", out)
        self.assertEqual(verdict_of(out, self.STARTED), "FAIL", out)

    def test_a_capture_dropped_inside_its_first_pass_reads_started_fail_and_the_report_names_the_dropped_line(self):
        code, out, _ = self.judge("marshal_captured_then_dropped.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "PASS", out)
        self.assertEqual(verdict_of(out, self.STARTED), "FAIL", out)
        self.assertIn("cleared, dropped and end=ImplicitArrival lines", self.check(self.STARTED).needs)
        self.assertIn("cleared, dropped and end=ImplicitArrival lines", out)

    def test_a_capture_behind_a_walk_that_a_direct_order_clears_reads_started_fail_and_the_report_names_the_implicit_wait(self):
        code, out, _ = self.judge("marshal_captured_behind_a_walk_cleared.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "PASS", out)
        self.assertEqual(verdict_of(out, self.STARTED), "FAIL", out)
        self.assertEqual(verdict_of(out, self.CLEARED), "PASS", out)
        self.assertIn("ImplicitArrival", self.check(self.STARTED).needs)
        self.assertIn("behind a walk that is the whole walk", self.check(self.STARTED).needs)

    def test_the_started_pass_pattern_takes_each_real_end_and_not_the_implicit_wait(self):
        pattern = self.check(self.STARTED).passes[0]
        line = "[Check] id=taom.marshals-word.started result=pass mission=3 end=%s companies=2"
        for end in ("AtOnce", "Arrival", "TargetGone", "EntityOrderEnded"):
            with self.subTest(end=end):
                self.assertTrue(pattern.search(line % end))
        for end in ("ImplicitArrival", "Arrival-and-more", "Other"):
            with self.subTest(end=end):
                self.assertFalse(pattern.search(line % end))

    def test_one_lone_move_is_not_a_chain(self):
        code, out, _ = self.judge("marshal_one_arrival.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FINISHED), "NOT REACHED", out)
        self.assertIn("no line matched pass pattern", out)

    def test_two_lone_moves_are_not_a_chain_either(self):
        code, out, _ = self.judge("marshal_two_lone_moves.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FINISHED), "NOT REACHED", out)
        self.assertIn("behind=0", out)

    def test_a_move_queued_behind_another_passes_the_finished_check_and_leaves_patience_for_eight_steps(self):
        code, out, _ = self.judge("marshal_two_moves.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FINISHED), "PASS", out)
        self.assertEqual(verdict_of(out, self.PATIENCE), "NOT REACHED", out)

    def test_a_step_that_ends_by_patience_is_not_an_arrival(self):
        code, out, _ = self.judge("marshal_arrival_by_patience_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.FINISHED), "NOT REACHED", out)
        self.assertIn("reason=patience", out)

    def test_patience_too_often_fails_the_patience_check_and_the_arrival_check(self):
        code, out, _ = self.judge("marshal_patience_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.PATIENCE), "FAIL", out)
        self.assertEqual(verdict_of(out, self.FINISHED), "FAIL", out)

    def test_declines_write_skip_lines_that_name_their_reason(self):
        # A full queue needs twelve captures first, so a real log with reason=full already holds a captured pass: no fixture has it alone.
        code, out, _ = self.judge("marshal_declines_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.DECLINED), "PASS", out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "NOT REACHED", out)
        self.assertIn("no line matched", out)
        self.assertNotIn("reason=full", out)

    def test_the_decline_pass_pattern_takes_each_reason_and_no_other(self):
        pattern = self.check(self.DECLINED).passes[0]
        line = "[Check] id=taom.marshals-word.declined result=skip mission=3 reason=%s"
        for reason in ("none-selected", "not-allowed", "deployment", "ai-controlled"):
            with self.subTest(reason=reason):
                self.assertTrue(pattern.search(line % reason))
        self.assertFalse(pattern.search(line % "lost-control"))
        self.assertFalse(pattern.search(line % "deployment-and-more"))

    def test_a_layout_that_threw_fails_the_array_check(self):
        code, out, _ = self.judge("marshal_array_fault.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.ARRAY), "FAIL", out)

    def test_a_click_alone_leaves_the_array_and_the_march_checks_not_reached(self):
        code, out, _ = self.judge("marshal_array_click_only.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.ARRAY), "NOT REACHED", out)
        self.assertEqual(verdict_of(out, self.MARCH), "NOT REACHED", out)

    def test_the_array_check_needs_both_a_click_and_a_drag(self):
        pattern_kinds = sorted(p.pattern.split("kind=")[1].split(" ")[0] for p in self.check(self.ARRAY).passes)
        self.assertEqual(pattern_kinds, ["click", "drag"])

    def test_a_spread_beyond_the_allowance_fails_the_march_check(self):
        code, out, _ = self.judge("marshal_spread_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.SPREAD), "FAIL", out)
        self.assertEqual(verdict_of(out, self.MARCH), "PASS", out)

    def test_a_march_whose_walks_prove_nothing_is_not_reached_and_says_no_test(self):
        code, out, _ = self.judge("marshal_march_no_test.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SPREAD), "NOT REACHED", out)
        self.assertEqual(verdict_of(out, self.MARCH), "PASS", out)
        self.assertIn("reason=no-test", out)

    def test_the_spread_pass_pattern_carries_the_shortest_time(self):
        pattern = self.check(self.SPREAD).passes[0]
        self.assertTrue(pattern.search("[Check] id=taom.marshals-word.march-spread result=pass mission=3 spread=1.5 longest=24.0 shortest=12.0"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.march-spread result=pass mission=3 spread=1.5 longest=24.0"))
        self.assertIn("2.5:1", self.check(self.SPREAD).needs)
        self.assertIn("in Line", self.check(self.SPREAD).needs)

    def test_a_march_ended_by_a_later_order_is_not_reached_and_says_why(self):
        code, out, _ = self.judge("marshal_march_reordered.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.SPREAD), "NOT REACHED", out)
        self.assertIn("reason=reordered", out)

    def test_the_finished_pass_pattern_takes_the_arrival_of_a_move_queued_behind_another_and_needs_one(self):
        check = self.check(self.FINISHED)
        pattern = check.passes[0]
        line = "[Check] id=taom.marshals-word.finished result=pass mission=3 reason=%s end=Arrival company=2 behind=1"
        self.assertTrue(pattern.search(line % "arrival"))
        for reason in ("at-once", "target-gone", "entity-ended", "patience", "arrival-and-more"):
            with self.subTest(reason=reason):
                self.assertFalse(pattern.search(line % reason))
        # A lone Move and the implicit wait ahead of a step write behind=0, and behind=1 alone proves the chain.
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.finished result=pass mission=3 reason=arrival end=Arrival company=2 behind=0"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.finished result=pass mission=3 reason=arrival end=ImplicitArrival company=2 behind=1"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.finished result=pass mission=3 reason=arrival end=Arrival company=2 behind=1-and-more"))
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.finished result=pass mission=3 reason=arrival end=Arrival company=2"))
        self.assertEqual(check.min, 1)

    def test_every_check_that_waits_on_a_later_stage_keeps_its_not_reached_absence(self):
        # Removing `absence` from one of these makes a session that did not reach it FAIL instead (the default is "fail").
        for check_id in (self.CAPTURED, self.FINISHED, self.PATIENCE, self.CLEARED, self.DECLINED, self.ARRAY, self.MARCH, self.SPREAD):
            with self.subTest(check_id=check_id):
                self.assertEqual(self.check(check_id).absence, "not-reached")

    def test_one_session_of_each_feature_exits_0_with_sessions_2(self):
        # The manifest is shared, so a Seat-only or a Marshal-only session leaves the other feature's checks NOT REACHED and exits 4.
        self.t.taom("seat_panel_visit_pass.txt", "2026-11-03_14-00-00")
        self.t.taom("marshal_full_pass.txt", "2026-11-04_14-00-00")
        code, out, _ = self.t.run(["--issue", self.ISSUE, "--sessions", "2"])
        self.assertEqual(code, 0, out)
        for check_id in self.ALL + [SeatOfSeeingManifestTests.ENTER, SeatOfSeeingManifestTests.GLIDE]:
            with self.subTest(check_id=check_id):
                self.assertEqual(verdict_of(out, check_id), "PASS", out)

    # ---- throwers on an advance ----

    def test_a_stand_inside_the_overshoot_passes_a_walk_in_fails_and_a_run_that_never_stood_reads_not_reached(self):
        code, out, _ = self.judge("marshal_throwers_stand_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.THROWERS_STAND), "PASS", out)
        code, out, _ = self.judge("marshal_throwers_stand_walked_in.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.THROWERS_STAND), "FAIL", out)
        self.assertIn("reason=walked-in", out)
        code, out, _ = self.judge("marshal_throwers_stand_skip.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.THROWERS_STAND), "NOT REACHED", out)
        self.assertIn("why=held", out)

    def test_the_postfix_fault_line_fails_the_stand_check_and_the_captured_check(self):
        code, out, _ = self.judge("marshal_throwers_stand_fault.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.THROWERS_STAND), "FAIL", out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "FAIL", out)

    def test_the_shared_category_failing_to_apply_fails_the_captured_check(self):
        code, out, _ = self.judge("marshal_throwers_category_failed.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.CAPTURED), "FAIL", out)

    def test_ten_javelins_loosed_pass_the_throw_check_and_a_stand_that_threw_too_little_fails(self):
        code, out, _ = self.judge("marshal_throwers_throw_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.THROWERS_THROW), "PASS", out)
        code, out, _ = self.judge("marshal_throwers_throw_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.THROWERS_THROW), "FAIL", out)
        self.assertIn("reason=few-throws", out)
        code, out, _ = self.judge("marshal_throwers_throw_skip.txt")
        self.assertEqual(verdict_of(out, self.THROWERS_THROW), "NOT REACHED", out)

    def test_the_throw_pass_needs_ten_shots(self):
        pattern = self.check(self.THROWERS_THROW).passes[0]
        line = "[Check] id=taom.marshals-word.throwers-throw result=pass mission=1 company=1 shots=%s stood_s=4.0"
        for shots in ("0", "1", "9"):
            self.assertFalse(pattern.search(line % shots), "shots=" + shots)
        for shots in ("10", "12", "120"):
            self.assertTrue(pattern.search(line % shots), "shots=" + shots)

    def test_blows_after_the_release_pass_the_melee_check_and_none_within_the_wait_fails(self):
        code, out, _ = self.judge("marshal_throwers_melee_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.THROWERS_MELEE), "PASS", out)
        code, out, _ = self.judge("marshal_throwers_melee_fail.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.THROWERS_MELEE), "FAIL", out)
        self.assertIn("reason=no-melee", out)
        code, out, _ = self.judge("marshal_throwers_melee_skip.txt")
        self.assertEqual(verdict_of(out, self.THROWERS_MELEE), "NOT REACHED", out)

    def test_blows_with_a_company_that_did_not_go_in_fail_the_melee_check_as_no_advance(self):
        code, out, _ = self.judge("marshal_throwers_melee_no_advance.txt")
        self.assertEqual(code, 1, out)
        self.assertEqual(verdict_of(out, self.THROWERS_MELEE), "FAIL", out)
        self.assertIn("reason=no-advance", out)

    def test_the_melee_pass_needs_at_least_one_blow(self):
        pattern = self.check(self.THROWERS_MELEE).passes[0]
        line = "[Check] id=taom.marshals-word.throwers-melee result=pass mission=1 company=1 blows=%s melee_s=3.0 advanced_m=4.0"
        self.assertFalse(pattern.search(line % "0"))
        self.assertTrue(pattern.search(line % "1"))
        self.assertTrue(pattern.search(line % "40"))

    def test_the_melee_pass_needs_the_advanced_fact(self):
        pattern = self.check(self.THROWERS_MELEE).passes[0]
        self.assertFalse(pattern.search("[Check] id=taom.marshals-word.throwers-melee result=pass mission=1 company=1 blows=1 melee_s=5.0"))
        self.assertTrue(pattern.search("[Check] id=taom.marshals-word.throwers-melee result=pass mission=1 company=1 blows=1 melee_s=5.0 advanced_m=3.0"))

    def test_a_cheap_epoch_passes_the_cost_check_and_a_slow_mean_or_one_slow_call_fails_it(self):
        code, out, _ = self.judge("marshal_throwers_cost_pass.txt")
        self.assertEqual(code, 4, out)
        self.assertEqual(verdict_of(out, self.THROWERS_COST), "PASS", out)
        for fixture, reason in (("marshal_throwers_cost_fail_mean.txt", "reason=mean"), ("marshal_throwers_cost_fail_max.txt", "reason=max")):
            code, out, _ = self.judge(fixture)
            self.assertEqual(code, 1, out)
            self.assertEqual(verdict_of(out, self.THROWERS_COST), "FAIL", out)
            self.assertIn(reason, out)
        code, out, _ = self.judge("marshal_throwers_cost_skip.txt")
        self.assertEqual(verdict_of(out, self.THROWERS_COST), "NOT REACHED", out)
        self.assertIn("reason=too-few-calls", out)

    def test_the_cost_pass_needs_three_hundred_calls(self):
        pattern = self.check(self.THROWERS_COST).passes[0]
        line = "[Check] id=taom.marshals-word.throwers-cost result=pass mission=1 epoch=1 calls=%s mean_us=4.0 max_us=9.0"
        for calls in ("0", "12", "299", "30"):
            self.assertFalse(pattern.search(line % calls), "calls=" + calls)
        for calls in ("300", "999", "1000", "52000"):
            self.assertTrue(pattern.search(line % calls), "calls=" + calls)

    def test_every_thrower_check_names_the_postfix_fault_and_the_shared_category_in_its_fail_list(self):
        for check_id in (self.THROWERS_STAND, self.THROWERS_THROW, self.THROWERS_MELEE, self.THROWERS_COST):
            patterns = [p.pattern for p in self.check(check_id).fails]
            with self.subTest(check_id=check_id):
                self.assertIn(r"\[MarshalsWord\] the thrower stand failed", patterns)
                self.assertIn(r"\[PatchApply\] Patch_MissionTime_SetMovementOrder (?:FAILED|SKIPPED)", patterns)



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


class SessionSummaryTests(unittest.TestCase):
    """The [Session] lines (#1074). session_summary*.txt render OutcomeLog.WriteSummary's and Swallowed's
    formats (Main/Core/Logging/OutcomeLog.cs) behind FileLogger's prefix; attach_pass.txt has no summary."""

    def setUp(self):
        self.t = Tree(self)
        self.t.real_manifest("970.toml")

    def test_the_last_summary_line_is_printed_with_its_fields(self):
        self.t.taom("session_summary.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertIn("    summary (taom_debug_2026-10-30_12-57-22.log:9): reason=exit uptime_s=1864 checks_pass=2 "
                      "checks_fail=0 swallowed=14 shield_swallowed=4 checks_skip=0 errors=0 dropped=0", out)
        self.assertNotIn("reason=heartbeat", out)
        self.assertNotIn("WARNING", out)

    def test_the_swallowed_sites_of_the_last_summary_are_listed(self):
        self.t.taom("session_summary.txt", "2026-10-30_12-57-22")
        _, out, _ = self.t.run(["--issue", "970"])
        self.assertIn("    swallowed: feature=CompanionTactics site=OOBOverlayService.Refresh n=2 "
                      "first=InvalidOperationException", out)
        self.assertIn("    swallowed: feature=Enlistment site=GameMenuManagerSetNextMenuPatch n=12 "
                      "first=NullReferenceException", out)
        self.assertNotIn("n=3", out)

    def test_errors_above_zero_warn_and_leave_the_verdicts_alone(self):
        self.t.taom("session_summary_errors.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertEqual(verdict_of(out, "taom.battle-action-bar.attach"), "PASS")
        self.assertIn("reason=crash", out)
        self.assertRegex(out, r"\n    WARNING: errors=2: ")
        self.assertNotIn("    swallowed:", out)

    def test_a_session_without_a_summary_says_so(self):
        self.t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        code, out, _ = self.t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertIn("    no [Session] summary line in taom_debug_2026-10-30_12-57-22.log: ", out)
        self.assertIn("a game still running in its first 10 minutes", out)

    def test_a_swallowed_warning_after_the_last_summary_leaves_that_summary_alone(self):
        # [Swallowed]'s fixed text names "the [Session] summary"; only FileLogger's INFO line is a summary.
        self.t.taom("session_summary_swallowed_tail.txt", "2026-10-30_12-57-22")
        _, out, _ = self.t.run(["--issue", "970"])
        self.assertIn("    summary (taom_debug_2026-10-30_12-57-22.log:2): reason=heartbeat uptime_s=600 ", out)
        self.assertRegex(out, r"\n    WARNING: errors=2: ")
        self.assertIn("    swallowed: feature=Enlistment site=GameMenuManagerSetNextMenuPatch n=3 "
                      "first=NullReferenceException", out)
        self.assertNotIn("counts the repeats", out)

    def test_a_log_with_only_a_swallowed_warning_has_no_summary(self):
        self.t.taom("session_summary_swallowed_only.txt", "2026-10-30_12-57-22")
        _, out, _ = self.t.run(["--issue", "970"])
        self.assertIn("    no [Session] summary line in taom_debug_2026-10-30_12-57-22.log: ", out)
        self.assertNotIn("    summary (", out)

    def test_each_session_read_reports_its_own_summary(self):
        self.t.taom("session_summary_errors.txt", "2026-10-30_12-57-22")
        self.t.taom("attach_pass.txt", "2026-10-29_10-00-00")
        _, out, _ = self.t.run(["--issue", "970", "--sessions", "2"])
        first, second = out.split("  session 2:")
        self.assertIn("errors=2", first)
        self.assertIn("no [Session] summary line in taom_debug_2026-10-29_10-00-00.log", second)

    def test_md_reports_the_summary_warning_swallowed_sites_and_absence(self):
        self.t.taom("session_summary.txt", "2026-10-30_12-57-22")
        _, out, _ = self.t.run(["--issue", "970", "--format", "md"])
        self.assertIn("Session summaries:", out)
        self.assertIn("- Session 1, `taom_debug_2026-10-30_12-57-22.log:9`: `reason=exit uptime_s=1864", out)
        self.assertIn("  - swallowed: `feature=Enlistment site=GameMenuManagerSetNextMenuPatch n=12 "
                      "first=NullReferenceException`", out)
        t = Tree(self)
        t.real_manifest("970.toml")
        t.taom("session_summary_errors.txt", "2026-10-30_12-57-22")
        _, out, _ = t.run(["--issue", "970", "--format", "md"])
        self.assertIn("  - **Warning:** errors=2: ", out)
        t = Tree(self)
        t.real_manifest("970.toml")
        t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        _, out, _ = t.run(["--issue", "970", "--format", "md"])
        self.assertIn("- Session 1: no `[Session] summary` line in `taom_debug_2026-10-30_12-57-22.log`: ", out)


class SessionSummaryEdgeTests(unittest.TestCase):
    """Summary edge cases of #1074: a session with no TAOM log, a non-ASCII digit, the reason printed once."""

    def test_a_session_with_no_taom_log_says_so_in_both_renderers(self):
        t = Tree(self)
        t.real_manifest("981.toml")
        t.ds("ds_join.txt", "20261023-135846", mtime="2026-10-23 14:22:03")
        _, out, _ = t.run(["--issue", "981", "--ds-data", str(t.ds_data)])
        self.assertIn("    no [Session] summary line: this session has no TAOM log", out)
        _, md, _ = t.run(["--issue", "981", "--ds-data", str(t.ds_data), "--format", "md"])
        self.assertIn("- Session 1: no `[Session] summary` line; this session has no TAOM log", md)

    def test_a_superscript_digit_in_errors_does_not_raise(self):
        t = Tree(self)
        t.real_manifest("970.toml")
        dest = t.taom("session_summary_errors.txt", "2026-10-30_12-57-22")
        dest.write_text(dest.read_text(encoding="utf-8").replace("errors=2", "errors=²"), encoding="utf-8")
        code, out, _ = t.run(["--issue", "970"])
        self.assertEqual(code, 0, out)
        self.assertNotIn("WARNING: errors", out)

    def test_the_no_summary_reason_prints_once_per_report(self):
        t = Tree(self)
        t.real_manifest("970.toml")
        t.taom("attach_pass.txt", "2026-10-30_12-57-22")
        t.taom("attach_pass.txt", "2026-10-29_10-00-00")
        _, out, _ = t.run(["--issue", "970", "--sessions", "2"])
        self.assertEqual(out.count("a game still running in its first 10 minutes"), 1, out)
        self.assertEqual(out.count("no [Session] summary line in taom_debug_"), 2, out)
        _, md, _ = t.run(["--issue", "970", "--sessions", "2", "--format", "md"])
        self.assertEqual(md.count("a game still running in its first 10 minutes"), 1, md)


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
        # The evidence line holds an em dash; a console code page that lacks it must not end the run. 9006 passes on
        # the legacy "layer attached" line, which still carries one.
        self.t.manifest("9006.toml")
        self.t.taom("attach_dirty.txt", "2026-10-30_12-57-22")
        raw = io.BytesIO()
        stdout = io.TextIOWrapper(raw, encoding="ascii")
        with mock.patch.object(iv.sys, "stdout", stdout):
            code = iv.main(["--issue", "9006", "--game", str(self.t.game)], runner=FakeRunner(),
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
