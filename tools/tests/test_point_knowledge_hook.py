#!/usr/bin/env python
"""Tests for .claude/hooks/point-knowledge.sh (#1613, ADR-016).

Run:  python -m unittest tools.tests.test_point_knowledge_hook -v

Each test builds a throwaway project folder, points CLAUDE_PROJECT_DIR at it and runs the real hook with
bash on a JSON payload. Most tests write the index file themselves (`<project>/.claude/logs/sessions/
<session id>.kindex`), so the hook needs no Python for them. One test lets the hook build the index through
a copy of tools/knowledge.py, and the parity test holds the hook's glob match to knowledge.glob_matches.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
HOOK = REPO / ".claude" / "hooks" / "point-knowledge.sh"
BASH = shutil.which("bash") or "C:/Program Files/Git/bin/bash.exe"

sys.path.insert(0, str(REPO / "tools"))
import knowledge  # noqa: E402

ARMORY = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\items.xml"

GOOD_A = '''title = "Report a detached Blender job from the shared wrapper"
line = "A Blender script that a caller polls runs its body inside blender_job.py job()."
why = "The Store build of Blender detaches and prints nothing."
area = "ai-harness"
status = "active"
next = "A gate holds the wrapper."

[when]
paths = ["*/tools/blender/*.py"]

[[evidence]]
grade = "maintainer"
value = "said so"
date = 2026-11-25

[[source]]
issue = 1
'''
GOOD_B = GOOD_A.replace("Report a detached Blender job", "Name a Blender mesh").replace(
    "A Blender script that a caller polls runs its body inside blender_job.py job().", "Name each mesh after its part."
).replace('paths = ["*/tools/blender/*.py"]', 'paths = ["*/tools/blender/*.py"]')


def hook_env(project):
    env = dict(os.environ, CLAUDE_PROJECT_DIR=Path(project).as_posix())
    env.pop("TAOM_PYBIN", None)
    return env


class HookCase(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="taom-knowhook-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.proj = self.tmp / "proj"
        (self.proj / "docs" / "knowledge" / "techniques").mkdir(parents=True)
        self.sid = "s" + uuid.uuid4().hex[:12]
        self.ledger = self.proj / ".claude" / "logs" / "sessions"
        self.ledger.mkdir(parents=True)
        self.index_path = self.ledger / (self.sid + ".kindex")

    # --- helpers ---------------------------------------------------------------------------------------------

    def write_index(self, rows, skipped=0, header=None):
        """rows: tuples (kind, id, level, glob, text, ask)."""
        store = (self.proj / "docs" / "knowledge" / "techniques").as_posix()
        head = header if header is not None else "#knowledge-index v1\tentries=%d\tskipped=%d\tstore=%s" % (
            len({r[1] for r in rows if r[0] == "t"}), skipped, store)
        text = "\n".join([head] + ["\t".join(r) for r in rows]) + "\n"
        self.index_path.write_text(text, encoding="utf-8", newline="\n")

    def payload(self, tool="Read", path="E:/repos/x/tools/blender/a.py", agent_id=None, agent_type=None,
                event="PostToolUse", sid=None, ensure_ascii=True, with_path=True):
        data = {"session_id": sid or self.sid, "hook_event_name": event}
        if event == "PostToolUse":
            data["tool_name"] = tool
            data["tool_input"] = {"file_path": path} if with_path else {"command": "ls"}
        if agent_id:
            data["agent_id"] = agent_id
        if agent_type:
            data["agent_type"] = agent_type
        return json.dumps(data, ensure_ascii=ensure_ascii).encode("utf-8")

    def popen(self, payload):
        return subprocess.Popen([BASH, HOOK.as_posix()], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, env=hook_env(self.proj)), payload

    def run_hook(self, payload):
        proc = subprocess.run([BASH, HOOK.as_posix()], input=payload, capture_output=True, env=hook_env(self.proj))
        return proc.returncode, proc.stdout.decode("utf-8"), proc.stderr.decode("utf-8")

    def note(self, payload):
        """The additionalContext the hook printed ('' for silence); proves exit 0 and valid JSON."""
        rc, out, err = self.run_hook(payload)
        self.assertEqual(rc, 0, err)
        if not out.strip():
            return ""
        data = json.loads(out)["hookSpecificOutput"]
        self.assertEqual(data["hookEventName"], "PostToolUse")
        self.assertNotIn("\n", out.strip())
        return data["additionalContext"]

    def parallel(self, payloads):
        """Start one hook per payload, give every one its whole input, and only then read: each hook blocks on its
        stdin until it closes, so feeding them one after the other would run them one after the other."""
        procs = [self.popen(p)[0] for p in payloads]
        for proc, payload in zip(procs, payloads):
            proc.stdin.write(payload)
        for proc in procs:
            proc.stdin.close()
        outs = [proc.stdout.read().decode("utf-8") for proc in procs]
        for proc in procs:
            proc.stderr.read()
            proc.wait()
            proc.stdout.close()
            proc.stderr.close()
        return outs

    def real_tool(self, entries):
        """A copy of the tool and these entry files in the project, so the hook builds its own index."""
        (self.proj / "tools").mkdir(exist_ok=True)
        shutil.copy(REPO / "tools" / "knowledge.py", self.proj / "tools" / "knowledge.py")
        for stem, text in entries.items():
            (self.proj / "docs" / "knowledge" / "techniques" / (stem + ".toml")).write_text(
                text, encoding="utf-8", newline="\n")

    def age(self, path, seconds):
        """Set a file's time into the past, so "newer than" does not rest on the clock's resolution."""
        then = time.time() - seconds
        os.utime(path, (then, then))

    def entries(self, n, glob="*/tools/blender/*.py"):
        return [("t", "entry-%d" % i, "sentence", glob, "Text of entry %d." % i, "") for i in range(1, n + 1)]

    def markers(self):
        return sorted(p.name for p in self.ledger.iterdir())


class Matching(HookCase):
    def test_a_read_of_a_matching_file_names_the_entry_with_level_text_and_store(self):
        self.write_index([("t", "blender-job-report", "gate", "*/tools/blender/*.py", "Run the body inside job().", "")])
        text = self.note(self.payload())
        self.assertIn("blender-job-report [gate]: Run the body inside job().", text)
        self.assertIn("matches 1 knowledge entry: how this kind of work is done here", text)
        self.assertIn("Follow each, or say in your report why it does not apply.", text)
        self.assertIn("(store: ", text)
        self.assertIn("docs/knowledge/techniques)", text)
        self.assertIn("python tools/knowledge.py show <id>", text)

    def test_a_path_in_another_checkout_and_the_live_armory_path_match_in_both_slash_forms(self):
        self.write_index([("t", "blender-job-report", "gate", "*/tools/blender/*.py", "Job.", ""),
                          ("t", "armory-ids", "report", "*/modules/lotrlome_armory/moduledata/*.xml", "Armory.", "")])
        cases = [("E:\\repos\\other\\tools\\blender\\x.py", "blender-job-report"),
                 ("E:/repos/other/tools/blender/y.py", "blender-job-report"),
                 (ARMORY, "armory-ids"),
                 (ARMORY.replace("\\", "/"), "armory-ids")]
        for n, (path, entry) in enumerate(cases):
            with self.subTest(path=path):
                sid = "case%d%s" % (n, self.sid)
                shutil.copy(self.index_path, self.ledger / (sid + ".kindex"))
                text = self.note(self.payload(path=path, sid=sid))
                self.assertIn(entry + " [", text)

    def test_a_repeat_of_the_same_read_is_silent(self):
        self.write_index(self.entries(3))
        self.assertIn("entry-1 [", self.note(self.payload()))
        self.assertEqual(self.note(self.payload()), "")

    def test_a_subagent_gets_its_own_note_after_the_main_session_and_its_repeat_is_silent(self):
        self.write_index(self.entries(1))
        self.assertIn("entry-1 [", self.note(self.payload()))
        self.assertIn("entry-1 [", self.note(self.payload(agent_id="agent7")))
        self.assertEqual(self.note(self.payload(agent_id="agent7")), "")
        self.assertEqual(self.note(self.payload()), "")

    def test_six_parallel_runs_of_one_payload_give_exactly_one_note(self):
        self.write_index(self.entries(1))
        outs = self.parallel([self.payload()] * 6)
        self.assertEqual(len([o for o in outs if o.strip()]), 1, outs)

    def test_six_parallel_runs_over_five_entries_print_every_entry_exactly_once(self):
        self.write_index(self.entries(5))
        outs = self.parallel([self.payload()] * 6)
        joined = "".join(json.loads(o)["hookSpecificOutput"]["additionalContext"] for o in outs if o.strip())
        for i in range(1, 6):
            self.assertEqual(joined.count("entry-%d [" % i), 1, joined)

    def test_six_parallel_first_reads_that_all_build_the_index_give_no_false_unchecked_note(self):
        self.real_tool({"blender-job-report": GOOD_A, "name-a-mesh": GOOD_B})
        outs = self.parallel([self.payload()] * 6)
        joined = "".join(json.loads(o)["hookSpecificOutput"]["additionalContext"] for o in outs if o.strip())
        self.assertNotIn("unchecked", joined)
        self.assertEqual(joined.count("blender-job-report ["), 1, joined)
        self.assertEqual(joined.count("name-a-mesh ["), 1, joined)
        self.assertEqual([n for n in self.markers() if ".part-" in n], [])

    def test_an_entry_with_two_matching_globs_is_counted_once_among_the_ones_to_come(self):
        rows = self.entries(4) + [("t", "entry-4", "sentence", "*/blender/*", "Text of entry 4.", "")]
        self.write_index(rows)
        first = self.note(self.payload())
        self.assertIn("1 more entries match", first)
        self.assertIn("entry-4 [", self.note(self.payload()))
        self.assertEqual(self.note(self.payload()), "")

    def test_a_reviewer_gets_the_question_where_there_is_one_and_the_text_where_there_is_none(self):
        self.write_index([("t", "with-ask", "sentence", "*/tools/blender/*.py", "The text of A.", "Did you print the pitch?"),
                          ("t", "no-ask", "sentence", "*/tools/blender/*.py", "The text of B.", "")])
        text = self.note(self.payload(agent_type="deep-reviewer"))
        self.assertIn("As the reviewer, check the change against each line and answer each question with evidence.", text)
        self.assertIn("with-ask [sentence]: Did you print the pitch?", text)
        self.assertNotIn("The text of A.", text)
        self.assertIn("no-ask [sentence]: The text of B.", text)
        self.assertNotIn("Follow each, or say in your report", text)

    def test_five_matching_entries_come_three_then_two_then_silence(self):
        self.write_index(self.entries(5))
        first = self.note(self.payload())
        for i in (1, 2, 3):
            self.assertIn("entry-%d [" % i, first)
        for i in (4, 5):
            self.assertNotIn("entry-%d [" % i, first)
        self.assertIn("matches 3 knowledge entries", first)
        self.assertIn("2 more entries match and come with the next files you touch", first)
        self.assertIn('python tools/knowledge.py for "E:/repos/x/tools/blender/a.py"', first)
        second = self.note(self.payload())
        self.assertIn("entry-4 [", second)
        self.assertIn("entry-5 [", second)
        self.assertNotIn("entry-1 [", second)
        self.assertNotIn("more entries", second)
        self.assertIn("matches 2 knowledge entries", second)
        self.assertEqual(self.note(self.payload()), "")

    def test_an_edit_of_a_named_enforcer_prints_the_watch_sentence_and_a_read_does_not(self):
        self.write_index([("t", "other", "gate", "*/tools/blender/*.py", "Other.", ""),
                          ("w", "job-gate", "gate", "*/tools/gate.py", "tools/gate.py", "")])
        path = "E:/repos/x/tools/gate.py"
        self.assertEqual(self.note(self.payload(tool="Read", path=path)), "")
        text = self.note(self.payload(tool="Edit", path=path))
        self.assertIn("point-knowledge: " + path + " is named by the knowledge entry job-gate as what makes it act (gate).", text)
        self.assertIn("update docs/knowledge/techniques/job-gate.toml in the same change.", text)
        self.assertTrue(text.startswith("point-knowledge: " + path + " is named"))
        self.assertEqual(self.note(self.payload(tool="Write", path=path)), "")

    def test_the_watch_sentence_carries_the_line_of_the_entry_it_names(self):
        self.write_index([("t", "job-gate", "code", "*/somewhere/else/*.py", "Run the body inside job().", ""),
                          ("w", "job-gate", "code", "*/tools/gate.py", "tools/gate.py", "")])
        text = self.note(self.payload(tool="Edit", path="E:/repos/x/tools/gate.py"))
        self.assertIn("as what makes it act (code): Run the body inside job(). If this change alters that", text)

    def test_skipped_entries_add_the_could_not_be_read_sentence(self):
        self.write_index(self.entries(1), skipped=2)
        text = self.note(self.payload())
        self.assertIn("entry-1 [", text)
        self.assertTrue(text.endswith("point-knowledge: 2 knowledge entries could not be read, so an entry for this file "
                                      "may be missing: run python tools/knowledge.py check."), text)

    def test_skipped_entries_are_reported_once_though_no_entry_matched(self):
        self.write_index(self.entries(1, glob="*/nowhere/else/*.py"), skipped=2)
        text = self.note(self.payload())
        self.assertTrue(text.startswith("point-knowledge: 2 knowledge entries could not be read"), text)
        self.assertEqual(self.note(self.payload()), "")
        self.assertIn("2 knowledge entries could not be read", self.note(self.payload(agent_id="agent7")))

    def test_a_clean_index_has_no_could_not_be_read_tail(self):
        self.write_index(self.entries(1))
        self.assertNotIn("could not be read", self.note(self.payload()))


class Lifecycle(HookCase):
    def test_session_start_forgets_the_index_and_the_main_markers_but_keeps_a_subagents(self):
        self.write_index(self.entries(1))
        self.assertIn("entry-1 [", self.note(self.payload()))
        self.assertIn("entry-1 [", self.note(self.payload(agent_id="agent7")))
        self.assertTrue(self.index_path.exists())
        self.assertEqual(self.note(self.payload(event="SessionStart")), "")
        left = self.markers()
        self.assertFalse(self.index_path.exists(), left)
        self.assertNotIn(self.sid + ".k-entry-1", left)
        self.assertIn(self.sid + ".agent7.ak-entry-1", left)
        self.write_index(self.entries(1))
        self.assertIn("entry-1 [", self.note(self.payload()))
        self.assertEqual(self.note(self.payload(agent_id="agent7")), "")

    def test_session_start_lets_a_subagent_that_was_told_unchecked_try_again(self):
        shutil.rmtree(self.proj / "docs" / "knowledge" / "techniques")
        self.assertIn("unchecked", self.note(self.payload(agent_id="agent7")))
        self.assertIn(self.sid + ".agent7.ak-_unchecked", self.markers())
        self.note(self.payload(event="SessionStart"))
        self.assertNotIn(self.sid + ".agent7.ak-_unchecked", self.markers())

    def test_an_entry_file_newer_than_the_index_makes_the_hook_build_it_again(self):
        self.real_tool({"blender-job-report": GOOD_A})
        self.assertIn("blender-job-report [", self.note(self.payload()))
        self.age(self.index_path, 60)
        self.real_tool({"name-a-mesh": GOOD_B})
        text = self.note(self.payload(path="E:/repos/x/tools/blender/b.py"))
        self.assertIn("name-a-mesh [sentence]: Name each mesh after its part.", text)
        self.assertNotIn("blender-job-report [", text)

    def test_an_index_older_than_no_entry_is_kept(self):
        self.real_tool({"blender-job-report": GOOD_A})
        self.note(self.payload())
        before = self.index_path.stat().st_mtime_ns
        for p in (self.proj / "docs" / "knowledge" / "techniques").iterdir():
            self.age(p, 120)
        self.age(self.proj / "docs" / "knowledge" / "techniques", 120)
        self.age(self.proj / "tools" / "knowledge.py", 120)
        self.note(self.payload(path="E:/repos/x/tools/blender/b.py"))
        self.assertEqual(self.index_path.stat().st_mtime_ns, before)

    def test_a_write_under_the_stores_own_folder_deletes_the_index_and_prints_nothing(self):
        self.write_index(self.entries(1, glob="*/docs/knowledge/*"))
        path = (self.proj / "docs" / "knowledge" / "techniques" / "x.toml").as_posix()
        self.assertEqual(self.note(self.payload(tool="Write", path=path)), "")
        self.assertFalse(self.index_path.exists())

    def test_a_read_under_the_stores_own_folder_keeps_the_index_and_prints_nothing(self):
        self.write_index(self.entries(1, glob="*/docs/knowledge/*"))
        path = (self.proj / "docs" / "knowledge" / "techniques" / "x.toml").as_posix()
        self.assertEqual(self.note(self.payload(tool="Read", path=path)), "")
        self.assertTrue(self.index_path.exists())


class Unchecked(HookCase):
    def test_no_techniques_folder_gives_the_unchecked_note_once_then_silence(self):
        shutil.rmtree(self.proj / "docs" / "knowledge" / "techniques")
        text = self.note(self.payload())
        self.assertIn("point-knowledge: unchecked, not absent.", text)
        self.assertIn("no folder docs/knowledge/techniques in ", text)
        self.assertIn("Run python tools/knowledge.py check.", text)
        self.assertEqual(self.note(self.payload()), "")

    def test_an_index_whose_first_line_is_not_the_header_is_deleted_and_noted(self):
        self.write_index(self.entries(1), header="not an index")
        text = self.note(self.payload())
        self.assertIn("the index file is not an index", text)
        self.assertFalse(self.index_path.exists())

    def test_a_tool_that_cannot_build_the_index_gives_the_unchecked_note(self):
        text = self.note(self.payload())
        self.assertIn("unchecked, not absent", text)
        self.assertIn("tools/knowledge.py failed or overran", text)
        self.assertEqual(self.note(self.payload()), "")

    COUNTING_TOOL = """import os, sys
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "runs.txt"), "a") as f:
    f.write("run\\n")
sys.exit(1)
"""

    def test_after_one_unchecked_note_the_build_is_not_tried_again_on_every_read(self):
        (self.proj / "tools").mkdir()
        tool = self.proj / "tools" / "knowledge.py"
        tool.write_text(self.COUNTING_TOOL, encoding="utf-8", newline="\n")
        self.age(tool, 300)
        self.age(self.proj / "docs" / "knowledge" / "techniques", 300)
        self.assertIn("unchecked", self.note(self.payload()))
        self.assertEqual(self.note(self.payload()), "")
        self.assertEqual(self.note(self.payload(agent_id="agent7")), "")
        self.assertEqual((self.proj / "tools" / "runs.txt").read_text().count("run"), 1)

    def test_a_store_that_changed_after_the_unchecked_note_is_tried_again(self):
        (self.proj / "tools").mkdir()
        tool = self.proj / "tools" / "knowledge.py"
        tool.write_text(self.COUNTING_TOOL, encoding="utf-8", newline="\n")
        self.age(tool, 300)
        self.age(self.proj / "docs" / "knowledge" / "techniques", 300)
        self.assertIn("unchecked", self.note(self.payload()))
        self.age(self.ledger / (self.sid + ".k-_unchecked"), 120)
        self.real_tool({"blender-job-report": GOOD_A})
        self.assertIn("blender-job-report [", self.note(self.payload()))

    def test_the_index_is_built_through_the_real_tool_on_the_first_read(self):
        self.real_tool({"blender-job-report": GOOD_A, "name-a-mesh": GOOD_B})
        self.assertFalse(self.index_path.exists())
        text = self.note(self.payload())
        self.assertTrue(self.index_path.exists())
        self.assertIn("blender-job-report [sentence]: A Blender script that a caller polls", text)
        self.assertIn("name-a-mesh [sentence]: Name each mesh after its part.", text)

    def test_a_payload_with_no_session_id_is_silent(self):
        self.write_index(self.entries(1))
        data = json.loads(self.payload())
        del data["session_id"]
        self.assertEqual(self.note(json.dumps(data).encode()), "")

    def test_a_tool_payload_with_no_file_path_is_silent(self):
        self.write_index(self.entries(1))
        self.assertEqual(self.note(self.payload(with_path=False)), "")
        self.assertTrue(self.index_path.exists())


PARITY = [
    ("*/tools/blender/*.py", "E:\\Repos\\TAOM\\Tools\\Blender\\x.py", True),
    ("*/tools/blender/*.py", "/e/repos/taom/tools/blender/sub/x.py", True),
    ("*/tools/blender/*.py", "E:/repos/taom/tools/blender/x.pyc", False),
    ("*/tools/blender/*.py", "E:/repos/taom/tools/blenders/x.py", False),
    ("*/modules/lotrlome_armory/moduledata/*.xml", ARMORY, True),
    ("*/modules/lotrlome_armory/moduledata/*.xml", ARMORY.replace("\\", "/"), True),
    ("*rharm*.py", "E:/Assets/_auto_workspace/a job/RHARMB_Bands.py", True),
    ("*rharm*.py", "E:/Assets/_auto_workspace/a job/harm_bands.py", False),
    ("*/modules/taom_map/moduledata/*.xml", "E:/Steam/M & B/Modules/TAOM_Map/ModuleData/settlements.xml", True),
    ("*/modules/taom_map/moduledata/*.xml", "E:/Steam/M & B/Modules/TAOM_Map/ModuleData/settlements.xml.bak-x", False),
    ("*/tripo_in/*", "E:/a/[b]/tripo_in/model (1).glb", True),
    ("*/docs/*/lessons/*.md", "c:/r/docs/a/b/lessons/x.md", True),
    ("*/docs/*/lessons/*.md", "c:/r/docs/lessons/x.md", False),
    ("*/x-y_z.d/*", "c:/r/x-y_z.d/f", True),
    ("*/docs/knowledge/*", "C:/R/DOCS/KNOWLEDGE/A.TOML", True),
    ("*/main/*.cs", "/c/Users/x y/Main/Foo.CS", True),
    ("*/foo.txt", "c:/foo.txt", True),
    ("*/foo.txt", "c:/barfoo.txt", False),
    ("*/modules/*/items.xml", "E:/Sp\u00e4ce/modules/x/items.xml", True),
]


class Parity(HookCase):
    def test_the_hook_and_knowledge_glob_matches_give_the_same_verdict_on_every_row(self):
        self.assertGreaterEqual(len(PARITY), 12)
        for n, (glob, path, expected) in enumerate(PARITY):
            with self.subTest(glob=glob, path=path):
                self.assertEqual(knowledge.glob_matches(glob, path), expected, "the table disagrees with knowledge.py")
                sid = "par%d%s" % (n, self.sid)
                row = ("t", "row-%d" % n, "sentence", glob, "Text.", "")
                store = (self.proj / "docs" / "knowledge" / "techniques").as_posix()
                (self.ledger / (sid + ".kindex")).write_text(
                    "#knowledge-index v1\tentries=1\tskipped=0\tstore=%s\n%s\n" % (store, "\t".join(row)),
                    encoding="utf-8", newline="\n")
                text = self.note(self.payload(path=path, sid=sid, ensure_ascii=False))
                self.assertEqual(bool(text), expected, text)


if __name__ == "__main__":
    unittest.main()
