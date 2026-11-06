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
        # A main tree gets the shared root. Pinned on a temporary repo, so the test also passes when
        # it runs from a linked worktree, whose own checkout rightly gets TAOM-wt-<name>.
        _git(self.repo, "init", "-q")
        self.assertEqual(gt.out_root({}, repo=self.repo), gt.DEFAULT_OUT)
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


def _node(nid, label, source_file="", loc="", klass=False):
    node = {"id": nid, "label": label, "source_file": source_file, "source_location": loc,
            "norm_label": label.lower()}
    if klass:
        node["_callable_class"] = True
    return node


def _link(src, tgt, rel, source_file="x.cs", loc="L1"):
    return {"source": src, "target": tgt, "relation": rel, "source_file": source_file,
            "source_location": loc}


def fake_graph() -> dict:
    """IFoo (own file), Foo : IFoo, Bar (production) references IFoo, FooTests references Foo,
    Derived : BaseThing, and an external stub MissionLogic with no source file."""
    nodes = [
        _node("ifoo", "IFoo", "Main/Features/Foo/IFoo.cs", "L5", True),
        _node("ifoo_file", "IFoo.cs", "Main/Features/Foo/IFoo.cs", "L1"),
        _node("ifoo_stub", "IFoo", "", ""),
        _node("foo", "Foo", "Main/Features/Foo/Foo.cs", "L9", True),
        _node("bar", "Bar", "Main/Features/Foo/Bar.cs", "L3", True),
        _node("footests", "FooTests", "TAOM.Tests/Features/Foo/FooTests.cs", "L7", True),
        _node("footests_m", ".Works()", "TAOM.Tests/Features/Foo/FooTests.cs", "L9"),
        _node("footests_file", "FooTests.cs", "TAOM.Tests/Features/Foo/FooTests.cs", "L1"),
        _node("basething", "BaseThing", "Main/Core/BaseThing.cs", "L4", True),
        _node("derived", "Derived", "Main/Core/Derived.cs", "L6", True),
        _node("missionlogic", "MissionLogic", "", ""),
        _node("twin_a", "Twin", "Main/A/Twin.cs", "L1", True),
        _node("twin_b", "Twin", "Main/B/Twin.cs", "L1", True),
        _node("ifoo_m", ".DoIt()", "Main/Features/Foo/IFoo.cs", "L7"),
        _node("foo_m", ".DoIt()", "Main/Features/Foo/Foo.cs", "L11"),
        _node("baz_m", ".Run()", "Main/Features/Foo/Baz.cs", "L10"),
        _node("qux_m", ".Go()", "Main/Q.cs", "L2"),
    ]
    links = [
        _link("foo", "ifoo", "implements"),
        _link("bar", "ifoo", "references"),
        _link("footests", "foo", "references"),
        _link("derived", "basething", "inherits"),
        _link("derived", "missionlogic", "inherits"),
        _link("ifoo", "ifoo_m", "method"),
        _link("foo", "foo_m", "method"),
        _link("baz_m", "ifoo_m", "calls", "Main/Features/Foo/Baz.cs", "L12"),
        _link("qux_m", "foo_m", "calls", "Main/Q.cs", "L3"),
        _link("footests_m", "foo_m", "calls", "TAOM.Tests/Features/Foo/FooTests.cs", "L10"),
        _link("foo_m", "foo_m", "calls", "Main/Features/Foo/Foo.cs", "L12"),
    ]
    return {"nodes": nodes, "links": links}


def write_graph(directory: Path, graph=None) -> Path:
    path = directory / "graph.json"
    path.write_text(json.dumps(graph or fake_graph()), encoding="utf-8")
    return path


def affected_text(label, *hits) -> str:
    lines = [f"Affected nodes for {label}", "Relations: calls, references", "Depth: 2"]
    lines += [f"- {h}" for h in hits]
    return "\n".join(lines) + "\n"


class GraphDataTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.data = gt.GraphData.load(write_graph(Path(self._tmp.name)))

    def tearDown(self):
        self._tmp.cleanup()

    def test_class_name_finds_the_interface_it_implements(self):
        self.assertEqual(self.data.supertypes("Foo"), [("IFoo", "Main/Features/Foo/IFoo.cs")])

    def test_file_path_finds_the_interface_too(self):
        self.assertEqual(self.data.supertypes("Main/Features/Foo/Foo.cs"),
                         [("IFoo", "Main/Features/Foo/IFoo.cs")])

    def test_a_base_class_counts_and_an_external_stub_does_not(self):
        self.assertEqual(self.data.supertypes("Derived"),
                         [("BaseThing", "Main/Core/BaseThing.cs")])

    def test_a_type_without_supertypes_has_none(self):
        self.assertEqual(self.data.supertypes("Bar"), [])

    def test_candidates_put_the_defining_node_before_the_stub(self):
        found = self.data.candidates("IFoo")
        self.assertEqual([n["id"] for n in found][:2], ["ifoo", "ifoo_file"])
        self.assertIn("ifoo_stub", [n["id"] for n in found])

    def test_candidates_are_capped_at_ten(self):
        graph = fake_graph()
        graph["nodes"] += [_node(f"s{i}", f"FooHelper{i}", f"Main/S{i}.cs", "L1", True)
                           for i in range(30)]
        with tempfile.TemporaryDirectory() as d:
            data = gt.GraphData.load(write_graph(Path(d), graph))
            self.assertEqual(len(data.candidates("Foo")), 10)

    def test_one_defining_file_is_the_retry_target(self):
        self.assertEqual(self.data.sole_definition("IFoo"), "Main/Features/Foo/IFoo.cs")

    def test_two_defining_files_give_no_retry_target(self):
        self.assertIsNone(self.data.sole_definition("Twin"))

    def test_test_classes_in_a_file(self):
        self.assertEqual(self.data.classes_in("TAOM.Tests/Features/Foo/FooTests.cs"),
                         ["FooTests"])

    def test_member_callers_of_a_class(self):
        label, lines = self.data.member_callers("Foo")
        self.assertEqual(label, "Foo")
        self.assertIn("- Go() [calls DoIt()] Main/Q.cs:L3", lines)
        self.assertIn("- Works() [calls DoIt()] TAOM.Tests/Features/Foo/FooTests.cs:L10", lines)

    def test_a_types_own_members_are_not_its_callers(self):
        _, lines = self.data.member_callers("Foo")
        self.assertFalse([l for l in lines if "Foo.cs" in l])

    def test_member_callers_of_an_interface_found_by_path(self):
        label, lines = self.data.member_callers("Main/Features/Foo/IFoo.cs")
        self.assertEqual(label, "IFoo")
        self.assertEqual(lines, ["- Run() [calls DoIt()] Main/Features/Foo/Baz.cs:L12"])

    def test_a_type_without_members_has_no_member_callers(self):
        self.assertEqual(self.data.member_callers("Bar")[1], [])

    def test_unreadable_graph_is_none(self):
        self.assertIsNone(gt.GraphData.load(Path(self._tmp.name) / "missing.json"))


class FakeGraphify:
    """Stands in for the graphify subprocess: answers by the query, the first arg after the verb."""

    def __init__(self, answers):
        self.answers = answers
        self.calls = []

    def __call__(self, cmd, env):
        self.calls.append(cmd)
        return 0, self.answers.get(cmd[2], "No unique node match for " + cmd[2] + "\n"), ""


class AffectedAnswerTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.gpath = write_graph(Path(self._tmp.name))
        self.cache = gt.GraphCache(self.gpath)
        self.fake = FakeGraphify({
            "Foo": affected_text("Foo", "FooTests [references] TAOM.Tests/Features/Foo/FooTests.cs:L7"),
            "Main/Features/Foo/IFoo.cs": affected_text(
                "IFoo.cs",
                "Bar [references] Main/Features/Foo/Bar.cs:L3",
                "FooTests [references] TAOM.Tests/Features/Foo/FooTests.cs:L7"),
            "Main/Features/Foo/Foo.cs": affected_text(
                "Foo.cs", "FooTests [references] TAOM.Tests/Features/Foo/FooTests.cs:L7"),
        })
        self._orig = gt._capture
        gt._capture = self.fake

    def tearDown(self):
        gt._capture = self._orig
        self._tmp.cleanup()

    def run_affected(self, rest, interfaces=True, cache=None):
        return gt.affected_answer("graphify", self.gpath, {}, rest, interfaces,
                                  cache or self.cache)

    def test_supertype_callers_are_added_under_a_via_header(self):
        rc, out, _ = self.run_affected(["Foo", "--depth", "2"])
        self.assertEqual(rc, 0)
        self.assertIn("Via IFoo (Main/Features/Foo/IFoo.cs):", out)
        self.assertIn("- Bar [references] Main/Features/Foo/Bar.cs:L3", out)

    def test_lines_already_printed_are_dropped_from_the_via_block(self):
        _, out, _ = self.run_affected(["Foo"])
        self.assertEqual(out.count("FooTests [references]"), 1)

    def test_the_supertype_query_keeps_the_depth_flag(self):
        self.run_affected(["Foo", "--depth", "3"])
        self.assertEqual(self.fake.calls[1][2:5], ["Main/Features/Foo/IFoo.cs", "--depth", "3"])

    def test_no_interfaces_runs_graphify_once(self):
        _, out, _ = self.run_affected(["Foo"], interfaces=False)
        self.assertEqual(len(self.fake.calls), 1)
        self.assertNotIn("Via ", out)

    def test_unreadable_graph_keeps_graphifys_answer_and_says_so(self):
        cache = gt.GraphCache(Path(self._tmp.name) / "missing.json")
        _, out, _ = self.run_affected(["Foo"], cache=cache)
        self.assertIn("FooTests [references]", out)
        self.assertIn("NOTE", out)

    def test_member_callers_of_the_target_and_its_supertype_are_listed(self):
        _, out, _ = self.run_affected(["Foo"])
        self.assertIn("Member callers of Foo:\n- Go() [calls DoIt()] Main/Q.cs:L3", out)
        self.assertIn("Member callers of IFoo:\n- Run() [calls DoIt()] "
                      "Main/Features/Foo/Baz.cs:L12", out)

    def test_no_interfaces_keeps_the_targets_own_member_callers(self):
        _, out, _ = self.run_affected(["Foo"], interfaces=False)
        self.assertIn("Member callers of Foo:", out)
        self.assertNotIn("Member callers of IFoo:", out)
        self.assertEqual(len(self.fake.calls), 1)

    def test_a_member_caller_line_already_printed_is_dropped(self):
        self.fake.answers["Foo"] = affected_text("Foo", "Go() [calls DoIt()] Main/Q.cs:L3")
        _, out, _ = self.run_affected(["Foo"])
        self.assertEqual(out.count("Go() [calls DoIt()]"), 1)

    def test_a_failed_name_retries_with_its_one_defining_file(self):
        self.fake.answers["IFoo"] = "No unique node match for IFoo\n"
        rc, out, _ = self.run_affected(["IFoo", "--depth", "2"])
        self.assertEqual(self.fake.calls[1][2], "Main/Features/Foo/IFoo.cs")
        self.assertIn("retrying with Main/Features/Foo/IFoo.cs", out)
        self.assertIn("Bar [references]", out)

    def test_a_failed_name_with_two_definitions_lists_candidates(self):
        _, out, _ = self.run_affected(["Twin"])
        self.assertEqual(len(self.fake.calls), 1)
        self.assertIn("Candidates for Twin:", out)
        self.assertIn("twin_a  Twin  Main/A/Twin.cs:L1", out)
        self.assertIn("twin_b  Twin  Main/B/Twin.cs:L1", out)

    def test_a_failed_name_with_no_candidate_says_so(self):
        _, out, _ = self.run_affected(["Nothing"])
        self.assertIn("No node label contains", out)


class StaleScopeTests(unittest.TestCase):
    def test_only_files_in_the_answer_are_named(self):
        detail = ["Main/A.cs", "Main/B.cs", "docs/x.sh"]
        text = "- Foo [calls] Main/B.cs:L4\n"
        self.assertEqual(gt.stale_in_answer(detail, text), ["Main/B.cs"])

    def test_backslash_paths_in_the_answer_match(self):
        self.assertEqual(gt.stale_in_answer(["Main/B.cs"], "Main\\B.cs:L4"), ["Main/B.cs"])

    def test_warning_leads_with_the_answers_files_then_the_total(self):
        lines = gt.stale_warning(["Main/A.cs", "Main/B.cs"], "x Main/B.cs:L1")
        self.assertEqual(lines[0], "STALE in this answer: Main/B.cs: Read them directly before "
                                   "acting.")
        self.assertIn("2 code file(s) changed", lines[1])
        self.assertIn("python tools/graphify_taom.py refresh", lines[1])

    def test_warning_without_a_hit_has_only_the_total(self):
        lines = gt.stale_warning(["Main/A.cs"], "nothing")
        self.assertEqual(len(lines), 1)


class TestsVerbTests(unittest.TestCase):
    def test_filter_command_joins_class_names(self):
        self.assertEqual(
            gt.test_filter_command(["A", "B"]),
            'dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= '
            '--filter "FullyQualifiedName~A|FullyQualifiedName~B"')

    def test_hits_parse_into_paths(self):
        text = affected_text("Foo", "FooTests [references] TAOM.Tests/F/FooTests.cs:L7",
                             "Bar [calls] Main/Bar.cs:L3")
        self.assertEqual(gt.hit_files(text), ["TAOM.Tests/F/FooTests.cs", "Main/Bar.cs"])

    def test_verb_prints_one_filter_line_for_the_reached_tests(self):
        with tempfile.TemporaryDirectory() as d:
            gpath = write_graph(Path(d))
            fake = FakeGraphify({
                "Foo": affected_text(
                    "Foo", "FooTests [references] TAOM.Tests/Features/Foo/FooTests.cs:L7"),
                "Main/Features/Foo/IFoo.cs": affected_text("IFoo.cs"),
            })
            orig = gt._capture
            gt._capture = fake
            try:
                note, line = gt.tests_for(["Foo"], "graphify", gpath, {}, 2, True)
            finally:
                gt._capture = orig
        self.assertIsNone(note)
        self.assertIn('--filter "FullyQualifiedName~FooTests"', line)

    def test_member_caller_files_come_from_the_member_section_only(self):
        text = (affected_text("Foo", "Bar [calls] Main/Bar.cs:L3")
                + "Member callers of Foo:\n- Go() [calls DoIt()] Main/Q.cs:L3\n"
                  "- Works() [calls DoIt()] TAOM.Tests/F/FooTests.cs:L10\n")
        self.assertEqual(gt.member_caller_files(text), ["Main/Q.cs", "TAOM.Tests/F/FooTests.cs"])

    def test_tests_reached_through_a_member_caller_are_included(self):
        with tempfile.TemporaryDirectory() as d:
            gpath = write_graph(Path(d))
            fake = FakeGraphify({
                "Foo": affected_text("Foo"),
                "Main/Features/Foo/IFoo.cs": affected_text("IFoo.cs"),
                "Main/Q.cs": affected_text("Q.cs", "QTests [imports] TAOM.Tests/Q/QTests.cs:L1"),
                "Main/Features/Foo/Baz.cs": affected_text("Baz.cs"),
            })
            orig = gt._capture
            gt._capture = fake
            try:
                _, line = gt.tests_for(["Foo"], "graphify", gpath, {}, 2, True)
            finally:
                gt._capture = orig
        self.assertIn("FullyQualifiedName~FooTests", line)
        self.assertIn("FullyQualifiedName~QTests", line)

    def test_no_test_reached_is_a_note(self):
        with tempfile.TemporaryDirectory() as d:
            gpath = write_graph(Path(d))
            orig = gt._capture
            gt._capture = FakeGraphify({"Bar": affected_text("Bar")})
            try:
                note, line = gt.tests_for(["Bar"], "graphify", gpath, {}, 2, True)
            finally:
                gt._capture = orig
        self.assertIsNone(line)
        self.assertIn("NOTE", note)


class DetachTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.out = Path(self._tmp.name) / "out"
        self.saved = {n: getattr(gt, n) for n in ("out_root", "staleness", "_bannerlord_running",
                                                  "_spawn_detached", "_graphify_exe")}
        gt.out_root = lambda *a, **k: self.out
        gt.staleness = lambda repo, gdir: ("stale", ["Main/A.cs"])
        gt._bannerlord_running = lambda timeout: False
        self.spawned = []
        gt._spawn_detached = lambda cmd, log: self.spawned.append((cmd, log)) or 4242
        gt._graphify_exe = lambda: "graphify"
        self.env = os.environ.pop("TAOM_GRAPHIFY_AUTOREFRESH", None)

    def tearDown(self):
        for name, value in self.saved.items():
            setattr(gt, name, value)
        os.environ.pop("TAOM_GRAPHIFY_AUTOREFRESH", None)
        if self.env is not None:
            os.environ["TAOM_GRAPHIFY_AUTOREFRESH"] = self.env
        self._tmp.cleanup()

    def detach(self, *extra):
        import contextlib
        import io
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = gt.main(["refresh", "--if-stale", "--detach", *extra])
        return rc, buf.getvalue()

    def test_stale_graph_starts_a_detached_refresh(self):
        rc, out = self.detach()
        self.assertEqual(rc, 0)
        self.assertEqual(len(self.spawned), 1)
        cmd, log = self.spawned[0]
        self.assertIn("refresh", cmd)
        self.assertNotIn("--detach", cmd)
        self.assertEqual(log, gt.graph_dir(self.out) / "autorefresh.log")
        self.assertIn("background refresh started (pid 4242, log", out)

    def test_fresh_graph_starts_nothing(self):
        gt.staleness = lambda repo, gdir: ("fresh", [])
        rc, out = self.detach()
        self.assertEqual((rc, self.spawned), (0, []))
        self.assertIn("current", out)

    def test_held_lock_starts_nothing(self):
        self.out.mkdir(parents=True)
        (self.out / gt.LOCK_NAME).write_text("1 1\n", encoding="utf-8")
        rc, out = self.detach()
        self.assertEqual((rc, self.spawned), (0, []))
        self.assertIn("holds", out)

    def test_env_switch_off_starts_nothing(self):
        os.environ["TAOM_GRAPHIFY_AUTOREFRESH"] = "0"
        rc, out = self.detach()
        self.assertEqual((rc, self.spawned), (0, []))
        self.assertIn("TAOM_GRAPHIFY_AUTOREFRESH=0", out)

    def test_running_game_starts_nothing(self):
        gt._bannerlord_running = lambda timeout: True
        rc, out = self.detach()
        self.assertEqual((rc, self.spawned), (0, []))
        self.assertIn("Bannerlord is running", out)

    def test_unknown_game_state_starts_nothing(self):
        gt._bannerlord_running = lambda timeout: None
        rc, out = self.detach()
        self.assertEqual((rc, self.spawned), (0, []))
        self.assertIn("skipped", out)

    def test_missing_graph_starts_a_refresh(self):
        gt.staleness = lambda repo, gdir: ("missing", ["no graph"])
        self.detach()
        self.assertEqual(len(self.spawned), 1)


class AutorefreshLogCapTests(unittest.TestCase):
    """Each stale session appends one refresh to the log, so a full log is moved aside first."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.log = Path(self._tmp.name) / "autorefresh.log"

    def tearDown(self):
        self._tmp.cleanup()

    def _spawn_and_wait(self):
        pid = gt._spawn_detached([sys.executable, "-c", "print('child ran')"], self.log)
        self.assertIsInstance(pid, int)
        deadline = time.time() + 10
        while time.time() < deadline and b"child ran" not in self.log.read_bytes():
            time.sleep(0.05)

    def test_a_log_over_the_cap_is_kept_once_as_dot_1(self):
        self.log.write_bytes(b"x" * (gt.AUTOREFRESH_LOG_CAP + 1))
        self._spawn_and_wait()
        rotated = self.log.with_name("autorefresh.log.1")
        self.assertEqual(rotated.stat().st_size, gt.AUTOREFRESH_LOG_CAP + 1)
        self.assertIn(b"child ran", self.log.read_bytes())
        self.assertLess(self.log.stat().st_size, 1000)

    def test_a_log_under_the_cap_is_appended(self):
        self.log.write_bytes(b"earlier run\n")
        self._spawn_and_wait()
        self.assertTrue(self.log.read_bytes().startswith(b"earlier run\n"))
        self.assertFalse(self.log.with_name("autorefresh.log.1").exists())


if __name__ == "__main__":
    unittest.main()
