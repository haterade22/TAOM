#!/usr/bin/env python
"""Unit tests for tools/graphify_taom.py, the one sanctioned way TAOM builds and
queries its graphify code graph (#677).

Run:  python -m unittest discover -s tools/tests -p "test_graphify_taom.py"

Hermetic: git-backed cases build a throwaway repo in a tempdir and set file
mtimes explicitly, so no case reads the live repo, the live graph under
E:\\graphify, or the clock's rounding.
"""
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import graphify_taom as gt  # noqa: E402


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-c", "user.name=t", "-c", "user.email=t@t", "-c", "core.autocrlf=false", *args],
        cwd=repo, check=True, capture_output=True, text=True).stdout.strip()


def _touch(path: Path, text: str, mtime: float) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    os.utime(path, (mtime, mtime))


class OutRootTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.base = Path(self._tmp.name)
        self.repo = self.base / "repo"
        self.repo.mkdir()

    def tearDown(self):
        self._tmp.cleanup()

    def test_env_override_wins_over_default(self):
        self.assertEqual(gt.out_root({"TAOM_GRAPHIFY_OUT": str(self.base / "g")}), self.base / "g")

    def test_default_is_on_the_e_drive(self):
        self.assertEqual(gt.out_root({}), gt.DEFAULT_OUT)
        self.assertTrue(str(gt.DEFAULT_OUT).upper().startswith("E:"))

    def test_linked_worktree_gets_its_own_out_root(self):
        # A builder in `isolation: "worktree"` refreshing the shared graph would overwrite
        # it with the worktree's code; each linked worktree builds its own.
        _git(self.repo, "init", "-q")
        (self.repo / "a.txt").write_text("a\n", encoding="utf-8")
        _git(self.repo, "add", "-A")
        _git(self.repo, "commit", "-q", "-m", "init")
        wt = self.base / "feature"
        _git(self.repo, "worktree", "add", "-q", str(wt))
        self.assertEqual(gt.out_root({}, self.repo), gt.DEFAULT_OUT)
        self.assertEqual(gt.out_root({}, wt), gt.DEFAULT_OUT.parent / "TAOM-wt-feature")
        self.assertEqual(gt.out_root({"TAOM_GRAPHIFY_OUT": str(self.base / "g")}, wt),
                         self.base / "g")

    def test_out_root_outside_the_repo_is_accepted(self):
        gt.validate_out_root(self.base / "graphify", self.repo)

    def test_out_root_inside_the_repo_is_refused(self):
        with self.assertRaises(ValueError):
            gt.validate_out_root(self.repo / "Main", self.repo)

    def test_out_root_equal_to_the_repo_is_refused(self):
        with self.assertRaises(ValueError):
            gt.validate_out_root(self.repo, self.repo)


class CommandTests(unittest.TestCase):
    def test_extract_pins_code_only_and_out(self):
        cmd = gt.extract_command("graphify", Path("E:/repos/TAOM"), Path("E:/graphify/TAOM"))
        self.assertEqual(cmd[:3], ["graphify", "extract", str(Path("E:/repos/TAOM"))])
        self.assertIn("--code-only", cmd)
        self.assertEqual(cmd[cmd.index("--out") + 1], str(Path("E:/graphify/TAOM")))
        for banned in ("--mode", "--backend", "--global", "--force"):
            self.assertNotIn(banned, cmd)

    def test_cluster_never_labels_or_draws(self):
        cmd = gt.cluster_command("graphify", Path("E:/graphify/TAOM"))
        self.assertEqual(cmd[:3], ["graphify", "cluster-only", str(Path("E:/graphify/TAOM"))])
        self.assertIn("--no-label", cmd)
        self.assertIn("--no-viz", cmd)

    def test_env_pins_an_absolute_graphify_out(self):
        # An incremental extract wrote graphify-out/cache/stat-index.json into the scanned
        # repo despite --out (2026-09-26); only an absolute GRAPHIFY_OUT pins that cache.
        out = Path("E:/graphify/TAOM")
        env = gt.graphify_env(out, {"PATH": "x"})
        self.assertTrue(os.path.isabs(env["GRAPHIFY_OUT"]))
        self.assertEqual(Path(env["GRAPHIFY_OUT"]), Path(os.path.abspath(gt.graph_dir(out))))
        self.assertEqual(env["PATH"], "x")

    def test_query_pins_the_graph(self):
        graph = Path("E:/graphify/TAOM/graphify-out/graph.json")
        cmd = gt.query_command("graphify", "affected", ["IModLogger", "--depth", "2"], graph)
        self.assertEqual(cmd, ["graphify", "affected", "IModLogger", "--depth", "2",
                               "--graph", str(graph)])

    def test_query_refuses_a_write_verb(self):
        with self.assertRaises(ValueError):
            gt.query_command("graphify", "update", [], Path("g.json"))

    def test_query_refuses_a_second_graph(self):
        with self.assertRaises(ValueError):
            gt.query_command("graphify", "explain", ["X", "--graph", "other.json"], Path("g.json"))


class StampAndStalenessTests(unittest.TestCase):
    """The graph is stale when a code file changed after the build started, or a
    code file was deleted that was still present at build time."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.repo = Path(self._tmp.name) / "repo"
        self.repo.mkdir()
        self.gdir = Path(self._tmp.name) / "out" / "graphify-out"
        self.gdir.mkdir(parents=True)
        (self.gdir / "graph.json").write_text("{}", encoding="utf-8")
        _git(self.repo, "init", "-q")
        self.t0 = time.time() - 1000
        _touch(self.repo / "Main" / "A.cs", "class A {}\n", self.t0)
        _touch(self.repo / "Main" / "B.cs", "class B {}\n", self.t0)
        _touch(self.repo / "README.md", "readme\n", self.t0)
        _git(self.repo, "add", "-A")
        _git(self.repo, "commit", "-q", "-m", "init")
        self.built_at = self.t0 + 100
        gt.write_stamp(self.gdir, gt.make_stamp(self.repo, self.built_at))

    def tearDown(self):
        self._tmp.cleanup()

    def state(self):
        return gt.staleness(self.repo, self.gdir)[0]

    def test_stamp_round_trips(self):
        stamp = gt.read_stamp(self.gdir)
        self.assertEqual(stamp["head"], _git(self.repo, "rev-parse", "HEAD"))
        self.assertEqual(stamp["built_at"], self.built_at)
        self.assertEqual(stamp["deleted_at_build"], [])

    def test_untouched_tree_is_fresh(self):
        self.assertEqual(self.state(), "fresh")

    def test_code_edit_after_the_build_is_stale(self):
        _touch(self.repo / "Main" / "A.cs", "class A { int x; }\n", self.built_at + 50)
        state, detail = gt.staleness(self.repo, self.gdir)
        self.assertEqual(state, "stale")
        self.assertIn("Main/A.cs", detail)

    def test_code_edit_before_the_build_is_already_in_the_graph(self):
        _touch(self.repo / "Main" / "A.cs", "class A { int x; }\n", self.built_at - 10)
        self.assertEqual(self.state(), "fresh")

    def test_new_untracked_code_file_is_stale(self):
        _touch(self.repo / "Main" / "C.cs", "class C {}\n", self.built_at + 50)
        self.assertEqual(self.state(), "stale")

    def test_non_code_edit_is_ignored(self):
        _touch(self.repo / "README.md", "changed\n", self.built_at + 50)
        self.assertEqual(self.state(), "fresh")

    def test_commit_after_the_build_is_stale(self):
        _touch(self.repo / "Main" / "B.cs", "class B { int y; }\n", self.built_at + 50)
        _git(self.repo, "commit", "-q", "-am", "later")
        self.assertEqual(self.state(), "stale")

    def test_deletion_after_the_build_is_stale(self):
        (self.repo / "Main" / "B.cs").unlink()
        self.assertEqual(self.state(), "stale")

    def test_deletion_recorded_at_build_is_already_in_the_graph(self):
        (self.repo / "Main" / "B.cs").unlink()
        gt.write_stamp(self.gdir, gt.make_stamp(self.repo, self.built_at))
        self.assertEqual(gt.read_stamp(self.gdir)["deleted_at_build"], ["Main/B.cs"])
        self.assertEqual(self.state(), "fresh")

    def test_missing_graph_is_missing(self):
        (self.gdir / "graph.json").unlink()
        self.assertEqual(self.state(), "missing")

    def test_missing_stamp_is_missing(self):
        (self.gdir / gt.STAMP_NAME).unlink()
        self.assertEqual(self.state(), "missing")

    def test_unknown_stamp_commit_is_stale(self):
        stamp = gt.read_stamp(self.gdir)
        stamp["head"] = "0" * 40
        gt.write_stamp(self.gdir, stamp)
        self.assertEqual(self.state(), "stale")


class LockTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.out = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_second_acquire_is_refused(self):
        self.assertTrue(gt.acquire_lock(self.out))
        self.assertFalse(gt.acquire_lock(self.out))

    def test_release_frees_the_lock(self):
        self.assertTrue(gt.acquire_lock(self.out))
        gt.release_lock(self.out)
        self.assertTrue(gt.acquire_lock(self.out))

    def test_abandoned_lock_is_taken_over(self):
        self.assertTrue(gt.acquire_lock(self.out))
        old = time.time() - gt.LOCK_STALE_SECONDS - 60
        os.utime(self.out / gt.LOCK_NAME, (old, old))
        self.assertTrue(gt.acquire_lock(self.out))


class ContaminationTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.repo = Path(self._tmp.name)
        _git(self.repo, "init", "-q")
        (self.repo / ".gitignore").write_text("graphify-out/\n", encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def test_clean_repo_reports_nothing(self):
        self.assertEqual(gt.repo_contamination(self.repo), [])

    def test_ignored_graphify_out_is_still_reported(self):
        (self.repo / "Main" / "graphify-out" / "cache").mkdir(parents=True)
        (self.repo / "Main" / "graphify-out" / "cache" / "x.json").write_text("{}", encoding="utf-8")
        self.assertEqual(gt.repo_contamination(self.repo), ["Main/graphify-out/"])


class JudgeCommandTests(unittest.TestCase):
    """judge_command backs the check-graphify-usage.sh PreToolUse gate."""

    DENIED = [
        ("Bash", "graphify update E:/graphify/TAOM"),
        ("Bash", "graphify extract E:/repos/TAOM --code-only"),
        ("Bash", "graphify extract E:/repos/TAOM --code-only --out E:/graphify/TAOM"),
        ("Bash", "graphify cluster-only E:/graphify/TAOM"),
        ("Bash", "graphify label E:/graphify/TAOM --backend claude-cli"),
        ("Bash", "graphify claude install"),
        ("Bash", "graphify codex install"),
        ("Bash", "graphify hook install"),
        ("Bash", "graphify watch ."),
        ("Bash", "graphify tree"),
        ("Bash", "graphify-mcp"),
        ("Bash", "GRAPHIFY_FORCE=1 graphify update x"),
        ("Bash", "cd /x\ngraphify update y"),
        ("Bash", "echo a; graphify update y"),
        ("Bash", "echo a && graphify update y 2>&1 | tail -5"),
        ("Bash", "timeout 600 graphify extract ."),
        ("Bash", "uvx graphify extract ."),
        ("Bash", "uv tool run graphify update x"),
        ("Bash", "bash -c \"graphify update x\""),
        ("Bash", "/c/Users/mikew/.local/bin/graphify update x"),
        ("PowerShell", "graphify update E:\\graphify\\TAOM"),
        ("PowerShell", "& \"C:\\Users\\mikew\\.local\\bin\\graphify.exe\" update x"),
        ("PowerShell", "$sw = 1; graphify extract E:\\repos\\TAOM --code-only 2>&1 | Select-Object -Last 5"),
        ("PowerShell", "pwsh -Command \"graphify update x\""),
    ]

    ALLOWED = [
        ("Bash", "graphify explain \"TaomPartySizeModel\" --graph g.json"),
        ("Bash", "graphify affected \"ICoopSessionProvider\" --depth 2 --graph g.json"),
        ("Bash", "graphify god-nodes --top 15 --graph g.json"),
        ("Bash", "graphify path \"A\" \"B\" --graph g.json"),
        ("Bash", "graphify query \"who logs\" --graph g.json"),
        ("Bash", "graphify --help"),
        ("Bash", "graphify"),
        ("Bash", "python tools/graphify_taom.py refresh"),
        ("Bash", "git commit -m \"docs: graphify update drops nodes\""),
        ("Bash", "grep -rn \"graphify update\" docs/"),
        ("Bash", "echo \"graphify extract\""),
        ("Bash", "ls E:/graphify/TAOM/graphify-out"),
        ("PowerShell", "graphify god-nodes --top 15 --graph E:\\graphify\\TAOM\\graphify-out\\graph.json 2>&1"),
        ("PowerShell", "python tools/graphify_taom.py refresh --if-stale"),
        ("PowerShell", "Select-String -Path docs\\x.md -Pattern 'graphify update'"),
    ]

    def test_write_verbs_are_denied(self):
        for tool, cmd in self.DENIED:
            with self.subTest(tool=tool, cmd=cmd):
                self.assertIsNotNone(gt.judge_command(cmd, tool))

    def test_query_verbs_and_mentions_are_allowed(self):
        for tool, cmd in self.ALLOWED:
            with self.subTest(tool=tool, cmd=cmd):
                self.assertIsNone(gt.judge_command(cmd, tool))

    def test_reason_names_the_wrapper(self):
        self.assertIn("tools/graphify_taom.py", gt.judge_command("graphify update x", "Bash"))

    def test_update_reason_names_its_specific_trap(self):
        self.assertIn("external", gt.judge_command("graphify update x", "Bash"))


class GateDecisionTests(unittest.TestCase):
    def payload(self, cmd, tool="Bash"):
        return json.dumps({"tool_name": tool, "tool_input": {"command": cmd},
                           "hook_event_name": "PreToolUse"})

    def test_denied_command_nests_the_decision(self):
        d = gt.gate_decision(self.payload("graphify update x"))
        h = d["hookSpecificOutput"]
        self.assertEqual(h["hookEventName"], "PreToolUse")
        self.assertEqual(h["permissionDecision"], "deny")
        self.assertIn("graphify_taom.py", h["permissionDecisionReason"])

    def test_allowed_command_is_empty(self):
        self.assertEqual(gt.gate_decision(self.payload("graphify explain X --graph g")), {})

    def test_escaped_letter_is_still_judged(self):
        raw = self.payload("graphify update x").replace("graphify", "\\u0067raphify")
        self.assertEqual(gt.gate_decision(raw)["hookSpecificOutput"]["permissionDecision"], "deny")

    def test_malformed_payload_fails_open(self):
        self.assertEqual(gt.gate_decision("{not json"), {})

    def test_gate_subcommand_prints_the_decision(self):
        script = Path(__file__).resolve().parent.parent / "graphify_taom.py"
        out = subprocess.run([sys.executable, str(script), "gate"],
                             input=self.payload("graphify update x"),
                             capture_output=True, text=True, timeout=30)
        self.assertEqual(out.returncode, 0, out.stderr)
        self.assertEqual(json.loads(out.stdout)["hookSpecificOutput"]["permissionDecision"], "deny")


if __name__ == "__main__":
    unittest.main()
