"""Tests for tools/work_claims.py (#1248): one live session owns an issue or a red test at a time."""

import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import work_claims as wc  # noqa: E402


class Rig:
    """A claim store, a session registry and a set of live pids in a temporary folder."""

    def __init__(self, case):
        tmp = tempfile.TemporaryDirectory()
        case.addCleanup(tmp.cleanup)
        root = Path(tmp.name)
        self.store = root / "claims"
        self.registry = root / "sessions"
        self.registry.mkdir()
        self.alive = set()

    def session(self, pid, sid, name, worktree="E:/repos/wt", branch="fix/x"):
        (self.registry / f"{pid}.json").write_text(json.dumps({"pid": pid, "sessionId": sid, "name": name, "status": "busy"}), encoding="utf-8")
        self.alive.add(pid)
        return wc.Me(session_id=sid, pid=pid, name=name, worktree=worktree, branch=branch)

    def end(self, pid):
        self.alive.discard(pid)

    def claim(self, key, me, note="", now=1000):
        return wc.claim(key, me, self.store, self.registry, now, note=note, pid_alive=self.alive.__contains__)

    def held(self, key, me):
        return wc.holder(key, me, self.store, self.registry, pid_alive=self.alive.__contains__)


class KeyTests(unittest.TestCase):
    def test_an_issue_number_in_any_form_is_one_key(self):
        self.assertEqual(wc.normalize_key("1245"), "issue-1245")
        self.assertEqual(wc.normalize_key("#1245"), "issue-1245")
        self.assertEqual(wc.normalize_key(" issue-1245 "), "issue-1245")

    def test_a_free_key_is_folded_to_a_safe_file_name(self):
        self.assertEqual(wc.normalize_key("Trunk: LanguageDataXmlTests"), "trunk-languagedataxmltests")
        self.assertEqual(wc.normalize_key("../../etc"), "etc")

    def test_an_empty_key_is_refused(self):
        with self.assertRaises(ValueError):
            wc.normalize_key(" #  ")


class ClaimTests(unittest.TestCase):
    def setUp(self):
        self.rig = Rig(self)
        self.first = self.rig.session(101, "sid-a", "taom-b8", worktree="E:/repos/taom-ld-split")
        self.later = self.rig.session(202, "sid-b", "taom-72", worktree="E:/repos/TAOM")

    def test_a_free_key_is_taken(self):
        result = self.rig.claim("1245", self.first, note="trunk red: language rows")

        self.assertEqual(result.code, 0)
        self.assertEqual(result.claim["name"], "taom-b8")
        self.assertEqual(result.claim["worktree"], "E:/repos/taom-ld-split")
        self.assertEqual(result.claim["note"], "trunk red: language rows")

    def test_a_later_session_is_refused_and_told_who_holds_it(self):
        self.rig.claim("1245", self.first, note="trunk red: language rows")

        result = self.rig.claim("#1245", self.later)

        self.assertEqual(result.code, 3)
        self.assertIn("taom-b8", result.message)
        self.assertIn("E:/repos/taom-ld-split", result.message)
        self.assertIn("trunk red: language rows", result.message)

    def test_the_holder_claiming_again_keeps_its_claim_and_its_start(self):
        self.rig.claim("1245", self.first, now=1000)

        result = self.rig.claim("1245", self.first, now=5000, note="new note")

        self.assertEqual(result.code, 0)
        self.assertEqual(result.claim["since"], 1000)
        self.assertEqual(result.claim["note"], "new note")

    def test_a_claim_of_a_session_that_ended_is_taken_over_with_a_note(self):
        self.rig.claim("1245", self.first)
        self.rig.end(101)

        result = self.rig.claim("1245", self.later)

        self.assertEqual(result.code, 0)
        self.assertEqual(result.claim["name"], "taom-72")
        self.assertIn("taom-b8", result.message)
        self.assertIn("ended", result.message)

    def test_a_reused_pid_is_not_the_same_session(self):
        self.rig.claim("1245", self.first)
        self.rig.session(101, "sid-other", "taom-99")

        self.assertEqual(self.rig.claim("1245", self.later).code, 0, "the registry names another session for that pid")

    def test_holder_names_another_live_session_only(self):
        self.assertIsNone(self.rig.held("1245", self.later))
        self.rig.claim("1245", self.first)

        self.assertEqual(self.rig.held("1245", self.later)["name"], "taom-b8")
        self.assertIsNone(self.rig.held("1245", self.first), "a session's own claim is no conflict")

    def test_a_claim_needs_a_session(self):
        nobody = wc.Me(session_id="", pid=0, name="", worktree="E:/repos/wt", branch="")

        self.assertEqual(self.rig.claim("1245", nobody).code, 2)

    def test_a_claim_the_registry_cannot_confirm_is_reported(self):
        stray = wc.Me(session_id="sid-c", pid=303, name="", worktree="E:/repos/wt", branch="")
        self.rig.alive.add(303)

        result = self.rig.claim("1245", stray)

        self.assertEqual(result.code, wc.UNCONFIRMED, "no registry row names sid-c, so every other session reads the claim as ended")
        self.assertIn("UNCONFIRMED", result.message)
        self.assertIsNone(self.rig.held("1245", self.later))

    def test_the_claim_text_names_the_session_id_beside_its_name(self):
        self.assertIn("taom-b8 (sid-a)", self.rig.claim("1245", self.first).message)

    def test_a_damaged_claim_file_reads_as_free(self):
        self.rig.store.mkdir(parents=True)
        (self.rig.store / "issue-1245.json").write_text("{not json", encoding="utf-8")

        self.assertEqual(self.rig.claim("1245", self.later).code, 0)


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.rig = Rig(self)
        self.first = self.rig.session(101, "sid-a", "taom-b8")
        self.later = self.rig.session(202, "sid-b", "taom-72")

    def release(self, key, me):
        return wc.release(key, me, self.rig.store, self.rig.registry, pid_alive=self.rig.alive.__contains__)

    def test_the_holder_releases_its_claim(self):
        self.rig.claim("1245", self.first)

        self.assertEqual(self.release("1245", self.first).code, 0)
        self.assertEqual(self.rig.claim("1245", self.later).code, 0)

    def test_another_live_session_cannot_release_it(self):
        self.rig.claim("1245", self.first)

        self.assertEqual(self.release("1245", self.later).code, 3)
        self.assertEqual(self.rig.held("1245", self.later)["name"], "taom-b8")

    def test_releasing_a_free_key_is_no_error(self):
        self.assertEqual(self.release("1245", self.first).code, 0)


class ListTests(unittest.TestCase):
    def setUp(self):
        self.rig = Rig(self)
        self.first = self.rig.session(101, "sid-a", "taom-b8", worktree="E:/repos/taom-ld-split", branch="fix/language-rows")
        self.later = self.rig.session(202, "sid-b", "taom-72")

    def lines(self, me, others=True):
        return wc.brief_lines(wc.live_claims(self.rig.store, self.rig.registry, pid_alive=self.rig.alive.__contains__), me if others else None)

    def test_the_brief_list_names_each_other_live_claim_once(self):
        self.rig.claim("1245", self.first, note="trunk red: language rows", now=1794630000)
        self.rig.claim("1161", self.later)

        lines = self.lines(self.later)

        self.assertEqual(len(lines), 1)
        self.assertIn("#1245", lines[0])
        self.assertIn("taom-b8", lines[0])
        self.assertIn("E:/repos/taom-ld-split", lines[0])
        self.assertIn("fix/language-rows", lines[0])
        self.assertIn("trunk red: language rows", lines[0])

    def test_a_claim_of_an_ended_session_is_not_listed(self):
        self.rig.claim("1245", self.first)
        self.rig.end(101)

        self.assertEqual(self.lines(self.later), [])

    def test_an_empty_store_lists_nothing(self):
        self.assertEqual(self.lines(self.later), [])


class CliTests(unittest.TestCase):
    def setUp(self):
        self.rig = Rig(self)
        self.first = self.rig.session(101, "sid-a", "taom-b8")

    def run_cli(self, *argv, me=None):
        out = io.StringIO()
        with redirect_stdout(out):
            code = wc.main(list(argv), me=me or self.first, store=self.rig.store, registry=self.rig.registry, pid_alive=self.rig.alive.__contains__, now=1000)
        return code, out.getvalue()

    def test_claim_check_and_release_through_the_command_line(self):
        later = self.rig.session(202, "sid-b", "taom-72")
        self.assertEqual(self.run_cli("claim", "1245", "--note", "trunk red")[0], 0)

        code, out = self.run_cli("check", "1245", me=later)
        self.assertEqual(code, 3)
        self.assertIn("taom-b8", out)

        self.assertEqual(self.run_cli("release", "1245")[0], 0)
        self.assertEqual(self.run_cli("check", "1245", me=later)[0], 0)

    def test_list_others_leaves_out_the_session_a_hook_names(self):
        self.run_cli("claim", "1245")
        nobody = wc.Me(session_id="", pid=0, name="", worktree="E:/repos/wt", branch="")

        self.assertIn("taom-b8", self.run_cli("list", "--others", "--brief", me=nobody)[1])
        self.assertEqual(self.run_cli("list", "--others", "--brief", "--session", "sid-a", me=nobody), (0, ""))

    def test_check_of_your_own_claim_says_you_hold_it(self):
        self.run_cli("claim", "1245")

        code, out = self.run_cli("check", "1245")

        self.assertEqual(code, 0)
        self.assertIn("held by you", out)

    def test_list_without_a_registry_is_unchecked(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = wc.main(["list", "--others", "--brief"], me=self.first, store=self.rig.store, registry=self.rig.registry / "missing", pid_alive=self.rig.alive.__contains__, now=1000)

        self.assertEqual(code, wc.UNCONFIRMED)
        self.assertIn("UNCHECKED", out.getvalue())

    def test_a_store_that_cannot_be_written_exits_1(self):
        self.rig.store.write_text("a file, not a folder", encoding="utf-8")

        with redirect_stderr(io.StringIO()):
            code = self.run_cli("claim", "1245")[0]

        self.assertEqual(code, wc.STORE_ERROR)

    def test_list_others_brief_prints_nothing_when_no_one_else_claims(self):
        self.run_cli("claim", "1245")

        code, out = self.run_cli("list", "--others", "--brief")

        self.assertEqual((code, out), (0, ""))


if __name__ == "__main__":
    unittest.main()
