#!/usr/bin/env python3
"""Unit tests for tools/harness_retro.py.

Run:  python -B -m unittest tools.tests.test_harness_retro

Every transcript is synthetic and written into a temp directory; no test reads a real session.
"""
import contextlib
import io
import json
import os
import sys
import tempfile
import time
import unittest
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import harness_retro as hr  # noqa: E402

TS = "2026-10-10T12:00:00.000Z"
OLD_TS = "2026-08-01T12:00:00.000Z"


def rec(kind, session="s1", ts=TS, **extra):
    r = {"type": kind, "sessionId": session, "timestamp": ts, "isSidechain": False}
    r.update(extra)
    return r


def tool_use(tid, name, session="s1", ts=TS, **inp):
    return rec("assistant", session, ts, message={"content": [
        {"type": "tool_use", "id": tid, "name": name, "input": inp}]})


def tool_result(tid, text, error=True, session="s1", ts=TS):
    return rec("user", session, ts, message={"content": [
        {"type": "tool_result", "tool_use_id": tid, "is_error": error, "content": text}]})


def human(text, session="s1", ts=TS, **extra):
    return rec("user", session, ts, message={"content": text}, **extra)


def attachment(session="s1", ts=TS, **att):
    return rec("attachment", session, ts, attachment=att)


class Env:
    def __init__(self, tc):
        self.root = Path(tc.enterContext(tempfile.TemporaryDirectory()))
        self.tr = self.root / "tr"
        self.repo = self.root / "repo"
        self.mem = self.root / "mem"
        self.out = self.root / "out"
        for d in (self.tr, self.repo / ".claude", self.repo / "docs" / "reviews", self.mem / "features"):
            d.mkdir(parents=True)

    def write(self, name, records, raw_lines=()):
        p = self.tr / name
        p.parent.mkdir(parents=True, exist_ok=True)
        lines = [json.dumps(r) for r in records] + list(raw_lines)
        p.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return p

    def run(self, *extra, since="2026-10-01"):
        argv = ["--transcripts", str(self.tr), "--repo", str(self.repo), "--memory", str(self.mem),
                "--out", str(self.out)]
        if since:
            argv += ["--since", since]
        argv += list(extra)
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(buf):
            code = hr.main(argv)
        return code, buf.getvalue()

    def signals(self):
        return json.loads((self.out / "signals.json").read_text(encoding="utf-8"))

    def report(self):
        return (self.out / "report.md").read_text(encoding="utf-8")


class Base(unittest.TestCase):
    def setUp(self):
        self.env = Env(self)


class HookTests(Base):
    def test_pretooluse_deny_counted_by_basename(self):
        text = ('PreToolUse:Bash hook error: ["$CLAUDE_PROJECT_DIR"/.claude/hooks/mcp-health-check.sh]: '
                "server is down")
        self.env.write("a.jsonl", [tool_use("t1", "Bash", command="x"), tool_result("t1", text)])
        code, _ = self.env.run()
        self.assertEqual(code, 0)
        deny = self.env.signals()["hook_denies"]
        self.assertEqual(len(deny), 1)
        self.assertEqual(deny[0]["hook"], "mcp-health-check")
        self.assertEqual(deny[0]["event"], "PreToolUse")
        self.assertEqual(deny[0]["tool"], "Bash")
        self.assertEqual(deny[0]["count"], 1)

    def test_blocking_error_attachment(self):
        self.env.write("a.jsonl", [attachment(type="hook_blocking_error", hookName="Stop",
                                              hookEvent="Stop",
                                              blockingError="check-verification-evidence: no test run")])
        self.env.run()
        blocks = self.env.signals()["hook_blocks"]
        self.assertEqual(blocks[0]["hook"], "check-verification-evidence")
        self.assertEqual(blocks[0]["count"], 1)

    def test_blocking_error_nested_dict(self):
        # Claude Code 2.1.29x records the Stop block reason one level down:
        # {"blockingError": {"blockingError": "<hook>: <reason>"}}.
        self.env.write("a.jsonl", [attachment(type="hook_blocking_error", hookName="Stop",
                                              hookEvent="Stop",
                                              blockingError={"blockingError": "check-version-tagged: no tag"})])
        self.env.run()
        blocks = self.env.signals()["hook_blocks"]
        self.assertEqual(blocks[0]["hook"], "check-version-tagged")
        self.assertIn("no tag", blocks[0]["sample"])

    def test_rows_carry_first_and_last_seen(self):
        later = "2026-10-15T09:00:00.000Z"
        text = 'PreToolUse:Bash hook error: [validate-push]: BLOCKED'
        self.env.write("a.jsonl", [tool_use("t1", "Bash"), tool_result("t1", text),
                                   tool_use("t2", "Bash", ts=later), tool_result("t2", text, ts=later)])
        self.env.run()
        row = self.env.signals()["hook_denies"][0]
        self.assertEqual((row["first_seen"], row["last_seen"]), ("2026-10-10", "2026-10-15"))
        self.assertIn("2026-10-15", self.env.report())

    def test_non_blocking_error_grouped(self):
        recs = [attachment(session=s, type="hook_non_blocking_error", hookName="PreToolUse:Bash",
                           hookEvent="PreToolUse", command='bash "$D"/.claude/hooks/foo.sh',
                           exitCode=1, stderr="line 12: boom 404\nmore", stdout="")
                for s in ("s1", "s2")]
        self.env.write("a.jsonl", recs[:1])
        self.env.write("b.jsonl", recs[1:])
        self.env.run()
        broken = self.env.signals()["hook_broken"]
        self.assertEqual(len(broken), 1)
        self.assertEqual(broken[0]["script"], "foo.sh")
        self.assertEqual(broken[0]["count"], 2)
        self.assertEqual(broken[0]["sessions"], 2)

    def test_cancelled_hook(self):
        self.env.write("a.jsonl", [attachment(type="hook_cancelled", hookName="Stop",
                                              command="bash /x/y/slow.sh")])
        self.env.run()
        self.assertEqual(self.env.signals()["hook_cancelled"][0]["count"], 1)

    def test_cancelled_hook_counts_timeouts(self):
        # A timed-out gate was killed, so it failed open: the retro must see that, not just "cancelled".
        recs = [attachment(type="hook_cancelled", hookName="PreToolUse:Bash", command="bash /x/gate.sh",
                           timedOut=flag, durationMs=24199, timeoutMs=5000) for flag in (True, True, False)]
        self.env.write("a.jsonl", recs)
        self.env.run()
        row = self.env.signals()["hook_cancelled"][0]
        self.assertEqual((row["count"], row["timed_out"], row["example"]), (3, 2, "s1"))
        self.assertIn("Timed out", self.env.report())

    def test_no_fires_listing(self):
        settings = {"hooks": {
            "PreToolUse": [{"matcher": "Bash", "hooks": [
                {"command": "bash \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/never-fires.sh"},
                {"command": "bash \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/mcp-health-check.sh"}]}],
            "Stop": [{"hooks": [{"command": "bash .claude/hooks/stop-quiet.sh"}]}],
            "SessionStart": [{"hooks": [{"command": "bash .claude/hooks/start.sh"}]}]}}
        (self.env.repo / ".claude" / "settings.json").write_text(json.dumps(settings), encoding="utf-8")
        text = 'PreToolUse:Bash hook error: [.claude/hooks/mcp-health-check.sh]: down'
        self.env.write("a.jsonl", [tool_use("t1", "Bash"), tool_result("t1", text)])
        self.env.run()
        quiet = self.env.signals()["no_fires"]
        self.assertIn("stop-quiet", quiet)
        self.assertIn("never-fires", quiet)
        self.assertNotIn("mcp-health-check", quiet)
        self.assertNotIn("start", quiet)
        self.assertIn("No fires in window (rare-event guard? check before removing)", self.env.report())

    def test_ask_only_gate_listed_apart(self):
        # An approved ask leaves no trace in a transcript, so zero fires proves nothing about it.
        hooks = self.env.repo / ".claude" / "hooks"
        hooks.mkdir(parents=True)
        (hooks / "confirm-gate.sh").write_text(
            '#!/bin/bash\n# never a "deny" here\necho \'{"permissionDecision":"ask"}\'\n', encoding="utf-8")
        (hooks / "deny-gate.sh").write_text('echo \'{"permissionDecision":"deny"}\'\n', encoding="utf-8")
        # Asks on overrun but blocks with exit 2: not ask-only (the validate-push shape).
        (hooks / "exit2-gate.sh").write_text(
            'echo \'{"permissionDecision":"ask"}\'\nexit 2\n', encoding="utf-8")
        # "task" contains "ask"; a Stop reminder blocks through its helper.
        (hooks / "stop-gate.sh").write_text(
            '# the task is done\nsource "$D/_stop_reminder.sh"\n', encoding="utf-8")
        settings = {"hooks": {"PreToolUse": [{"matcher": "Bash", "hooks": [
            {"command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/confirm-gate.sh"},
            {"command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/deny-gate.sh"},
            {"command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/exit2-gate.sh"}]}],
            "Stop": [{"hooks": [{"command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/stop-gate.sh"}]}]}}
        (self.env.repo / ".claude" / "settings.json").write_text(json.dumps(settings), encoding="utf-8")
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="x")])
        self.env.run()
        sig = self.env.signals()
        self.assertEqual(sig["no_fires"], ["deny-gate", "exit2-gate", "stop-gate"])
        self.assertEqual(sig["ask_only_gates"], ["confirm-gate"])
        self.assertIn("Ask-only gates", self.env.report())


class PermissionTests(Base):
    def test_classifier_and_user_rejection(self):
        self.env.write("a.jsonl", [
            tool_use("t1", "Bash"),
            tool_result("t1", "Permission for this action was denied by the Claude Code auto mode classifier."),
            tool_use("t2", "Edit"),
            tool_result("t2", "The user doesn't want to proceed with this tool use. Stop."),
        ])
        self.env.run()
        sig = self.env.signals()
        self.assertEqual(sig["classifier_denials"], {"Bash": 1})
        self.assertEqual(sig["user_rejections"], {"Edit": 1})
        self.assertEqual(sig["tool_errors"], [])


class ErrorSignatureTests(Base):
    def test_recurring_marker(self):
        for s in ("s1", "s2", "s3"):
            self.env.write(f"{s}.jsonl", [tool_use("t", "Bash", session=s),
                                          tool_result("t", "Exit code 1\nfile not found: 12", session=s)])
        for s in ("s4", "s5"):
            self.env.write(f"{s}.jsonl", [tool_use("t", "Read", session=s),
                                          tool_result("t", "different failure", session=s)])
        self.env.run()
        rows = {r["signature"]: r for r in self.env.signals()["tool_errors"]}
        a = rows["file not found: #"]
        b = rows["different failure"]
        self.assertTrue(a["recurring"])
        self.assertEqual(a["sessions"], 3)
        self.assertFalse(b["recurring"])
        self.assertIn("RECURRING", self.env.report())

    def test_normalization(self):
        n = hr.normalize_signature
        self.assertEqual(n(r"cannot open E:\repos\TAOM\a_b.txt line 5"),
                         n(r"cannot open C:\x\y\z.txt line 77"))
        self.assertEqual(n("no such file /usr/lib/foo/bar.so 9"), n("no such file /home/u/baz/q.so 10"))
        self.assertEqual(n("id 123e4567-e89b-12d3-a456-426614174000 bad"), "id <id> bad")
        self.assertEqual(n("<tool_use_error>File missing</tool_use_error>"), "File missing")
        tb = "Traceback (most recent call last):\n  File \"x.py\", line 3\nValueError: bad value 42"
        self.assertEqual(n(tb), "ValueError: bad value #")
        self.assertLessEqual(len(n("x" * 500)), 100)
        self.assertEqual(n(""), "(empty result)")
        self.assertEqual(n("Exit code 1\n   \n"), "(empty result)")

    def test_runaway_three_but_not_two(self):
        recs = []
        for i in range(3):
            recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
        self.env.write("a.jsonl", recs)
        recs = []
        for i in range(2):
            recs += [tool_use(f"b{i}", "Bash", session="s2"),
                     tool_result(f"b{i}", "other failure", session="s2")]
        self.env.write("b.jsonl", recs)
        self.env.run()
        runs = self.env.signals()["runaways"]
        self.assertEqual(len(runs), 1)
        self.assertEqual(runs[0]["signature"], "same failure")
        self.assertEqual(runs[0]["length"], 3)

    def test_success_breaks_runaway(self):
        recs = []
        for i in range(2):
            recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
        recs += [tool_use("ok", "Bash"), tool_result("ok", "fine", error=False)]
        recs += [tool_use("a9", "Bash"), tool_result("a9", "same failure")]
        self.env.write("a.jsonl", recs)
        self.env.run()
        self.assertEqual(self.env.signals()["runaways"], [])

    def test_parallel_calls_of_one_message_are_not_a_runaway(self):
        # Six Edits sent in one assistant message that all fail are one batch, not a retry loop.
        msg = rec("assistant", message={"id": "msg_1", "content": [
            {"type": "tool_use", "id": f"e{i}", "name": "Edit", "input": {}} for i in range(4)]})
        recs = [msg] + [tool_result(f"e{i}", "File has not been read yet.") for i in range(4)]
        self.env.write("a.jsonl", recs)
        self.env.run()
        self.assertEqual(self.env.signals()["runaways"], [])

    def test_human_turn_breaks_runaway(self):
        recs = [tool_use("a0", "Bash"), tool_result("a0", "same failure"), human("try again")]
        for i in (1, 2):
            recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
        self.env.write("a.jsonl", recs)
        self.env.run()
        self.assertEqual(self.env.signals()["runaways"], [])

    def test_marked_human_record_with_an_injected_block_breaks_runaway(self):
        for turn, broken in ((human("<ide_opened_file>x</ide_opened_file>", origin={"kind": "human"}), True),
                             (rec("user", "s1", TS, message={"content": [{"type": "image", "source": {}}]},
                                  turnOrigin="human"), True),
                             (human("<ide_opened_file>x</ide_opened_file>"), False)):
            with self.subTest(broken=broken):
                recs = [tool_use("a0", "Bash"), tool_result("a0", "same failure"), turn]
                for i in (1, 2):
                    recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
                self.env.write("a.jsonl", recs)
                self.env.run()
                self.assertEqual(len(self.env.signals()["runaways"]), 0 if broken else 1)

    def test_queued_human_turn_breaks_runaway_like_a_user_record(self):
        for turn in (human("try again"),
                     attachment(type="queued_command", humanTurn=True, prompt=[{"type": "text", "text": "try again"}])):
            recs = [tool_use("a0", "Bash"), tool_result("a0", "same failure"), turn]
            for i in (1, 2):
                recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
            self.env.write("a.jsonl", recs)
            self.env.run()
            self.assertEqual(self.env.signals()["runaways"], [])

    def test_queued_command_without_human_turn_does_not_break_runaway(self):
        recs = [tool_use("a0", "Bash"), tool_result("a0", "same failure"),
                attachment(type="queued_command", prompt="a task notification")]
        for i in (1, 2):
            recs += [tool_use(f"a{i}", "Bash"), tool_result(f"a{i}", "same failure")]
        self.env.write("a.jsonl", recs)
        self.env.run()
        self.assertEqual(len(self.env.signals()["runaways"]), 1)


class CorrectionTests(Base):
    def test_corrections(self):
        self.env.write("a.jsonl", [
            human("no, I said use the adapter"),
            human("<task-notification>no stop</task-notification>"),
            human("no way this is meta", isMeta=True),
            human("No, the earlier summary said otherwise", isCompactSummary=True),
            tool_use("t1", "Bash"),
            tool_result("t1", "no I said it", error=False),
            human("looks good"),
        ])
        self.env.run()
        c = self.env.signals()["corrections"]
        self.assertEqual(c["count"], 1)
        self.assertEqual(c["sessions"], 1)

    def test_queued_human_turn_is_read_like_a_user_message(self):
        queued = lambda prompt: attachment(type="queued_command", humanTurn=True, prompt=prompt)
        self.env.write("a.jsonl", [
            queued([{"type": "text", "text": "no, I said use the adapter"}]),
            queued("wait, undo that"),
            queued([{"type": "image", "source": {}}]),
            attachment(type="queued_command", prompt="no, this one is not a human turn"),
            queued([{"type": "text", "text": "looks good"}]),
        ])
        self.env.run()
        c = self.env.signals()["corrections"]
        self.assertEqual((c["count"], c["sessions"]), (2, 1))

    def test_queued_slash_command_counts_as_a_slash_command(self):
        # The shape is assumed, not observed: no queued slash command record exists on this machine.
        self.env.write("a.jsonl", [attachment(type="queued_command", humanTurn=True,
                                              prompt="<command-name>/model</command-name>")])
        self.env.run()
        self.assertEqual(self.env.signals()["usage"]["slash_commands"], {"model": 1})

    IDE = "<ide_opened_file>The user opened a.cs in the IDE.</ide_opened_file>\n"

    def test_marked_record_opening_with_an_injected_block_is_read_for_corrections(self):
        for extra in ({"origin": {"kind": "human"}}, {"turnOrigin": "human"}):
            with self.subTest(extra=extra):
                self.env.write("a.jsonl", [human(self.IDE + "no, I said use the adapter", **extra)])
                self.env.run()
                self.assertEqual(self.env.signals()["corrections"]["count"], 1)

    def test_the_same_record_without_the_marker_is_not_a_human_turn(self):
        self.env.write("a.jsonl", [human(self.IDE + "no, I said use the adapter")])
        self.env.run()
        self.assertEqual(self.env.signals()["corrections"]["count"], 0)

    def test_marked_record_with_only_an_injected_block_is_not_a_correction(self):
        self.env.write("a.jsonl", [human("<pasted_content>no, I said use the adapter</pasted_content>",
                                         origin={"kind": "human"})])
        self.env.run()
        self.assertEqual(self.env.signals()["corrections"]["count"], 0)

    def test_marked_record_with_a_slash_command_still_counts_as_one(self):
        self.env.write("a.jsonl", [human(self.IDE + "<command-name>/model</command-name>",
                                         origin={"kind": "human"})])
        self.env.run()
        self.assertEqual(self.env.signals()["usage"]["slash_commands"], {"model": 1})

    def test_secret_redacted_in_excerpt(self):
        secret = "sk_" + "live_" + "A1b2C3d4E5f6G7h8I9j0"
        self.env.write("a.jsonl", [human("no, I said do not use " + secret)])
        self.env.run()
        text = self.env.report() + json.dumps(self.env.signals())
        self.assertNotIn(secret, text)
        self.assertIn("[REDACTED:", text)

    def test_generic_secret_redacted(self):
        value = "Tr0ub4dor" + "&3xyz"
        self.assertNotIn(value, hr.redact('no, I said use pass' + 'word="' + value + '"'))
        self.assertNotIn("Plural-Z", hr.normalize_signature('auth_' + 'token="zz9' + 'Plural-Z-Alpha" rejected'))


class UsageTests(Base):
    def test_skill_slash_and_agent(self):
        self.env.write("a.jsonl", [
            tool_use("t1", "Skill", skill="deep-review"),
            human("<command-name>/verify</command-name> now"),
            tool_use("t2", "Agent", subagent_type="feature-builder", model="sonnet"),
            tool_use("t3", "Task", prompt="x"),
        ])
        self.env.run()
        u = self.env.signals()["usage"]
        self.assertEqual(u["skills"]["deep-review"], 1)
        # Slash commands are counted apart: built-ins such as /model are not skills.
        self.assertNotIn("verify", u["skills"])
        self.assertEqual(u["slash_commands"]["verify"], 1)
        self.assertEqual(u["subagent_types"]["feature-builder"], 1)
        self.assertEqual(u["subagent_types"]["general-purpose"], 1)
        self.assertEqual(u["models"]["sonnet"], 1)
        self.assertEqual(u["models"]["(definition)"], 1)
        self.assertEqual(self.env.signals()["corrections"]["count"], 0)

    def test_unused_skills_listed(self):
        for name in ("used-skill", "idle-skill", "typed-skill"):
            d = self.env.repo / ".claude" / "skills" / name
            d.mkdir(parents=True)
            (d / "SKILL.md").write_text("x", encoding="utf-8")
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="used-skill"),
                                   human("<command-name>/typed-skill</command-name>")])
        self.env.run()
        self.assertEqual(self.env.signals()["unused_skills"], ["idle-skill"])


class WindowTests(Base):
    def test_since_excludes_old_record(self):
        self.env.write("a.jsonl", [tool_use("t1", "Skill", ts=OLD_TS, skill="old-skill"),
                                  tool_use("t2", "Skill", skill="new-skill")])
        self.env.run()
        skills = self.env.signals()["usage"]["skills"]
        self.assertIn("new-skill", skills)
        self.assertNotIn("old-skill", skills)

    def test_parse_failure_counted(self):
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="x")], raw_lines=["{not json", "[1,2]"])
        code, _ = self.env.run()
        self.assertEqual(code, 0)
        self.assertEqual(self.env.signals()["sessions"]["parse_failures"], 1)

    def test_main_and_subagent_counts(self):
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="x")])
        self.env.write("a/subagents/agent-1.jsonl", [tool_use("t1", "Skill", skill="y")])
        self.env.run()
        s = self.env.signals()["sessions"]
        self.assertEqual(s["main_files"], 1)
        self.assertEqual(s["subagent_files"], 1)

    def test_zero_records_exit_3(self):
        self.env.write("a.jsonl", [tool_use("t1", "Skill", ts=OLD_TS, skill="x")])
        code, out = self.env.run()
        self.assertEqual(code, 3)
        self.assertIn("UNCHECKED: no transcript records parsed in", out)

    def test_default_since_from_newest_retro(self):
        rev = self.env.repo / "docs" / "reviews"
        (rev / "harness-retro-2026-09-01.md").write_text("x", encoding="utf-8")
        (rev / "harness-retro-2026-10-05.md").write_text("x", encoding="utf-8")
        self.assertEqual(hr.default_since(self.env.repo, today=hr.date(2026, 10, 21)).isoformat(),
                         "2026-10-05")

    def test_default_since_fallback_30_days(self):
        self.assertEqual(hr.default_since(self.env.repo, today=hr.date(2026, 10, 21)).isoformat(),
                         "2026-09-21")


class OutDirTests(Base):
    def test_out_inside_repo_refused(self):
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="x")])
        self.env.out = self.env.repo / "docs" / "out"
        code, _ = self.env.run()
        self.assertEqual(code, 2)
        self.assertFalse(self.env.out.exists())


class OutputSizeTests(Base):
    def test_share_by_tool_and_family(self):
        self.env.write("a.jsonl", [
            tool_use("t1", "Bash", command="cd /x && GIT_PAGER=cat git diff HEAD~1"),
            tool_result("t1", "d" * 300, error=False),
            tool_use("t2", "Bash", command="grep -rn foo ."),
            tool_result("t2", "g" * 100, error=False),
            tool_use("t3", "Read", file_path="/x/y.txt"),
            tool_result("t3", "r" * 600, error=False),
        ])
        self.env.run()
        o = self.env.signals()["output_size"]
        self.assertEqual(o["total_chars"], 1000)
        tools = {r["tool"]: r for r in o["tools"]}
        self.assertEqual(tools["Read"]["share"], 0.6)
        self.assertEqual(tools["Bash"]["share"], 0.4)
        fams = {r["family"]: r for r in o["families"]}
        self.assertEqual(fams["git diff"]["chars"], 300)
        self.assertEqual(fams["grep"]["chars"], 100)
        self.assertIn("## 9. Tool output size", self.env.report())

    def test_error_results_count_too(self):
        self.env.write("a.jsonl", [tool_use("t1", "Bash", command="ls"),
                                   tool_result("t1", "e" * 50, error=True)])
        self.env.run()
        self.assertEqual(self.env.signals()["output_size"]["total_chars"], 50)

    def test_command_family(self):
        f = hr.command_family
        self.assertEqual(f("FOO=1 python tools/x.py"), "python x.py")
        self.assertEqual(f("dotnet test a"), "dotnet test")
        self.assertEqual(f("git -C /e/x diff"), "git diff")
        self.assertEqual(f(""), "(unknown)")

    def test_command_family_reads_past_wrappers(self):
        f = hr.command_family
        self.assertEqual(f("timeout 600 dotnet test TAOM.Tests"), "dotnet test")
        self.assertEqual(f("timeout -k 5 60 git log -3"), "git log")
        self.assertEqual(f("time dotnet build x"), "dotnet build")
        self.assertEqual(f("env FOO=1 git status"), "git status")
        self.assertEqual(f('& "C:/Python314/python.exe" tools/x.py'), "python x.py")

    def test_command_family_uses_the_hook_reader(self):
        f = hr.command_family
        self.assertEqual(f("env -i FOO=1 dotnet test"), "dotnet test")
        self.assertEqual(f("nice -n 5 dotnet test"), "dotnet test")
        self.assertEqual(f("git -c a=b --git-dir x status"), "git status")
        self.assertEqual(f("cd /e/x; S=/e/y; for f in a; do git clean -n; done"), "git clean")
        self.assertEqual(f("python - <<'EOF'\nprint(1)\nEOF"), "python")
        self.assertEqual(f('& "C:/Program Files/dotnet/dotnet.exe" test', "PowerShell"), "dotnet test")
        self.assertEqual(f("timeout /t 5", "PowerShell"), "timeout")

    def test_command_family_keeps_inline_scripts_out_of_the_key(self):
        f = hr.command_family
        self.assertEqual(f('bash -lc "git status && dotnet test"'), "bash")
        self.assertEqual(f("pwsh -NoProfile -EncodedCommand ZQBjAGgAbwA=", "PowerShell"), "pwsh")
        self.assertEqual(f("pwsh -NoProfile -ExecutionPolicy Bypass -File tools/x.ps1"), "pwsh x.ps1")
        self.assertEqual(f("gh -R a/b pr list"), "gh pr")

    def _two_families(self):
        self.env.write("a.jsonl", [
            tool_use("t1", "Bash", command="git diff HEAD~1"), tool_result("t1", "d" * 300, error=False),
            tool_use("t2", "Bash", command="grep -rn foo ."), tool_result("t2", "g" * 100, error=False),
            tool_use("t3", "Read", file_path="/x"), tool_result("t3", "r" * 600, error=False)])

    def test_growth_against_the_previous_run(self):
        self._two_families()
        prev = self.env.root / "2026-10-01"
        prev.mkdir()
        (prev / "signals.json").write_text(json.dumps({
            "window": {"since": "2026-09-24", "today": "2026-10-01"},
            "output_size": {"families": [{"family": "git diff", "share": 0.10},
                                         {"family": "grep", "share": 0.10}]}}), encoding="utf-8")
        code, out = self.env.run()
        g = self.env.signals()["output_size"]["growth"]
        self.assertEqual(g["baseline_today"], "2026-10-01")
        self.assertEqual([r["family"] for r in g["rows"]], ["git diff"])
        self.assertAlmostEqual(g["rows"][0]["delta"], 0.20)
        self.assertIn("### Growth since the previous run", self.env.report())
        self.assertIn("output growth: 1 shell family", out)

    def test_growth_without_a_previous_run_says_unchecked(self):
        self._two_families()
        code, out = self.env.run()
        self.assertIsNone(self.env.signals()["output_size"]["growth"]["baseline"])
        self.assertIn("growth UNCHECKED", self.env.report())
        self.assertIn("output growth: UNCHECKED", out)

    def test_read_path_folds_repos_and_worktrees(self):
        f = hr.read_path
        self.assertEqual(f("E:\\repos\\taom-wrapper-peel\\Tools\\harness_retro.py"), "tools/harness_retro.py")
        self.assertEqual(f("e:/repos/TAOM/.claude/rules/x.md"), ".claude/rules/x.md")
        self.assertEqual(f("C:/Users/someone/.claude/projects/p/memory/MEMORY.md"), "~/.claude/projects/p/memory/memory.md")
        self.assertEqual(f("D:/a/b/c/d.txt"), ".../b/c/d.txt")
        self.assertEqual(f(""), "(unknown)")

    def _reads(self):
        self.env.write("a.jsonl", [
            tool_use("r1", "Read", file_path="E:/repos/TAOM/docs/a.md"), tool_result("r1", "a" * 300, error=False),
            tool_use("r2", "Read", file_path="E:/repos/taom-x/docs/a.md", offset=10, limit=5),
            tool_result("r2", "a" * 100, error=False),
            tool_use("r3", "Read", file_path="E:/repos/TAOM/main/b.cs"), tool_result("r3", "b" * 600, error=False)])

    def test_top_files_read(self):
        self._reads()
        self.env.run()
        files = {r["file"]: r for r in self.env.signals()["output_size"]["files"]}
        self.assertEqual(files["main/b.cs"]["read_share"], 0.6)
        self.assertEqual(files["docs/a.md"]["results"], 2)
        self.assertEqual(files["docs/a.md"]["whole"], 1)
        self.assertIn("### Top files read", self.env.report())

    def test_file_growth_against_the_previous_run(self):
        self._reads()
        prev = self.env.root / "2026-10-01"
        prev.mkdir()
        (prev / "signals.json").write_text(json.dumps({
            "window": {"today": "2026-10-01"},
            "output_size": {"families": [], "files": [{"file": "docs/a.md", "read_share": 0.1},
                                                      {"file": "main/b.cs", "read_share": 0.6}]}}), encoding="utf-8")
        code, out = self.env.run()
        g = self.env.signals()["output_size"]["file_growth"]
        self.assertEqual([r["file"] for r in g["rows"]], ["docs/a.md"])
        self.assertAlmostEqual(g["rows"][0]["delta"], 0.3)
        self.assertIn("file growth: 1 file", out)

    def test_tool_table_folds_past_top(self):
        recs = []
        for i, name in enumerate(["Read", "Bash", "Grep", "Glob"]):
            recs += [tool_use("t%d" % i, name, command="ls"), tool_result("t%d" % i, "x" * (400 - 100 * i), error=False)]
        self.env.write("a.jsonl", recs)
        self.env.run("--top", "2")
        names = [r["tool"] for r in self.env.signals()["output_size"]["tools"]]
        self.assertEqual(names, ["Read", "Bash", "(other)"])


class TokenTests(Base):
    def usage_rec(self, uuid, request, inp=1, out=1, read=0, create=0, session="s1", stop=None, **extra):
        u = {"input_tokens": inp, "output_tokens": out, "cache_read_input_tokens": read,
             "cache_creation_input_tokens": create}
        return rec("assistant", session, uuid=uuid, requestId=request,
                   message={"id": "msg_" + request, "usage": u, "content": [], "stop_reason": stop}, **extra)

    def skill_call(self, request="r0"):
        return rec("assistant", uuid="u_" + request, requestId=request, message={
            "id": "msg_" + request, "usage": {"output_tokens": 2}, "stop_reason": "tool_use",
            "content": [{"type": "tool_use", "id": "t1", "name": "Skill", "input": {"skill": "verify"}}]})

    def _skill_then_human(self, turn):
        self.env.write("a.jsonl", [self.skill_call(), tool_result("t1", "ok", error=False),
                                   self.usage_rec("u1", "r1", out=30, stop="end_turn"), turn,
                                   self.usage_rec("u2", "r2", out=500, stop="end_turn")])
        self.env.run()
        return {r["name"]: r["total"] for r in self.tokens()["skills"]}

    def test_human_message_ends_skill_attribution(self):
        self.assertEqual(self._skill_then_human(human("now do something else")), {"verify": 31})

    def test_marked_human_record_with_an_injected_block_ends_skill_attribution(self):
        turn = human("<ide_opened_file>x</ide_opened_file>", origin={"kind": "human"})
        self.assertEqual(self._skill_then_human(turn), {"verify": 31})

    def test_unmarked_record_with_an_injected_block_does_not_end_skill_attribution(self):
        turn = human("<ide_opened_file>x</ide_opened_file>")
        self.assertNotEqual(self._skill_then_human(turn), {"verify": 31})

    def test_queued_command_attachment_ends_skill_attribution(self):
        turn = attachment(type="queued_command", humanTurn=True, prompt="typed while busy")
        self.assertEqual(self._skill_then_human(turn), {"verify": 31})

    def test_queued_image_only_turn_ends_skill_attribution(self):
        turn = attachment(type="queued_command", humanTurn=True, prompt=[{"type": "image", "source": {}}])
        self.assertEqual(self._skill_then_human(turn), {"verify": 31})

    def test_token_tables_split_output_and_cache_write_per_skill_and_type(self):
        def skill_call(request, skill):
            return rec("assistant", uuid="u_" + request, requestId=request, message={
                "id": "msg_" + request, "usage": {}, "stop_reason": "tool_use",
                "content": [{"type": "tool_use", "id": "t" + request, "name": "Skill", "input": {"skill": skill}}]})
        # Both skills total 1000 tokens, split differently.
        self.env.write("a.jsonl", [skill_call("s1", "alpha"),
                                   self.usage_rec("u1", "r1", inp=10, out=500, read=450, create=40, stop="end_turn"),
                                   skill_call("s2", "beta"),
                                   self.usage_rec("u2", "r2", inp=10, out=20, read=900, create=70, stop="end_turn")])
        self.env.write("a/subagents/agent-1.jsonl", [self.usage_rec("u3", "r3", inp=1, out=7, read=20, create=3)])
        (self.env.tr / "a" / "subagents" / "agent-1.meta.json").write_text(
            json.dumps({"agentType": "deep-reviewer"}), encoding="utf-8")
        self.env.run()
        t = self.tokens()
        rows = {r["name"]: r for r in t["skills"]}
        self.assertEqual(rows["alpha"]["total"], rows["beta"]["total"])
        self.assertEqual((rows["alpha"]["input"], rows["alpha"]["output"], rows["alpha"]["cache_read"],
                          rows["alpha"]["cache_creation"]), (10, 500, 450, 40))
        self.assertEqual((rows["beta"]["output"], rows["beta"]["cache_creation"]), (20, 70))
        agent = t["subagent_types"][0]
        self.assertEqual((agent["name"], agent["input"], agent["output"], agent["cache_read"],
                          agent["cache_creation"], agent["total"]), ("deep-reviewer", 1, 7, 20, 3, 31))
        report = self.env.report()
        self.assertIn("| Skill | Output | Cache write | Tokens |", report)
        self.assertIn("| alpha | 500 | 40 | 1000 |", report)
        self.assertIn("| beta | 20 | 70 | 1000 |", report)
        self.assertIn("| Type | Output | Cache write | Tokens |", report)
        self.assertIn("| deep-reviewer | 7 | 3 | 31 |", report)

    def test_output_partial_counts_requests_without_a_final_stop_reason(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", inp=10, out=5, read=100),
                                   self.usage_rec("u2", "r1", inp=10, out=9, read=100)])
        self.env.run()
        t = self.tokens()
        self.assertEqual((t["output_partial"], t["requests"], t["input"], t["cache_read"]), (1, 1, 10, 100))
        self.assertIn("Output is a floor: 1 requests (100%) were written mid-stream with no final count.",
                      self.env.report())

    def test_request_with_a_final_record_is_not_partial(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", out=5),
                                   self.usage_rec("u2", "r1", out=40, stop="tool_use")])
        self.env.run()
        self.assertEqual(self.tokens()["output_partial"], 0)
        self.assertNotIn("Output is a floor", self.env.report())

    def test_final_stop_reason_on_a_non_max_record_is_not_partial(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", out=40),
                                   self.usage_rec("u2", "r1", out=5, stop="end_turn")])
        self.env.run()
        self.assertEqual(self.tokens()["output_partial"], 0)

    def tokens(self):
        return self.env.signals()["tokens"]

    def test_duplicate_request_keeps_max_output(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", inp=10, out=5, read=100),
                                   self.usage_rec("u2", "r1", inp=10, out=40, read=100),
                                   self.usage_rec("u3", "r1", inp=10, out=20, read=100)])
        self.env.run()
        t = self.tokens()
        self.assertEqual((t["requests"], t["input"], t["output"], t["cache_read"]), (1, 10, 40, 100))

    def test_request_copied_into_another_file_counted_once(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", out=7)])
        self.env.write("b.jsonl", [self.usage_rec("u1", "r1", out=7, session="s2"),
                                   self.usage_rec("u2", "r2", out=3, session="s2")])
        self.env.run()
        t = self.tokens()
        self.assertEqual((t["requests"], t["output"]), (2, 10))

    def test_subagent_attributed_by_meta_else_unattributed(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", out=1)])
        self.env.write("a/subagents/agent-1.jsonl", [self.usage_rec("u2", "r2", out=100)])
        (self.env.tr / "a" / "subagents" / "agent-1.meta.json").write_text(
            json.dumps({"agentType": "deep-reviewer"}), encoding="utf-8")
        self.env.write("a/subagents/agent-2.jsonl", [self.usage_rec("u3", "r3", out=50)])
        self.env.run()
        t = self.tokens()
        by = {r["name"]: r["total"] for r in t["subagent_types"]}
        self.assertEqual(by, {"deep-reviewer": 101, "subagents (unattributed)": 51})
        self.assertIn("## 10. Tokens and cache", self.env.report())

    def test_skill_gets_the_tokens_after_its_invocation(self):
        skill = rec("assistant", uuid="u0", requestId="r0", message={
            "id": "msg_r0", "usage": {"output_tokens": 2},
            "content": [{"type": "tool_use", "id": "t1", "name": "Skill", "input": {"skill": "verify"}}]})
        self.env.write("a.jsonl", [skill, self.usage_rec("u1", "r1", out=30)])
        self.env.run()
        self.assertEqual({r["name"]: r["total"] for r in self.tokens()["skills"]}, {"verify": 31})

    def test_cache_break_counted_above_100k_creation(self):
        self.env.write("a.jsonl", [self.usage_rec("u1", "r1", create=100001),
                                   self.usage_rec("u2", "r2", create=100000),
                                   self.usage_rec("u3", "r3", read=500000)])
        self.env.run()
        t = self.tokens()
        self.assertEqual(t["cache_breaks"], 1)
        self.assertAlmostEqual(t["cache_read_share"], 500000 / (100001 + 100000 + 500000 + 3), places=4)

    def test_missing_usage_is_zero_and_none_says_no_usage_data(self):
        bare = rec("assistant", uuid="u9", requestId="r9", message={"id": "msg_r9", "content": []})
        self.env.write("a.jsonl", [bare, tool_use("t1", "Skill", skill="x")])
        code, _ = self.env.run()
        self.assertEqual(code, 0)
        self.assertEqual(self.tokens()["requests"], 0)
        self.assertIn("no usage data", self.env.report())
        odd = rec("assistant", uuid="u8", requestId="r8", message={"id": "m", "usage": {"output_tokens": 4}})
        self.env.write("b.jsonl", [odd])
        self.env.run()
        self.assertEqual((self.tokens()["input"], self.tokens()["output"]), (0, 4))


class TokenAgentTests(Base):
    def asst(self, mid, model, create=0, out=0, inp=0, read=0, ts=TS, session="s1", line="a"):
        u = {"input_tokens": inp, "cache_creation_input_tokens": create,
             "cache_read_input_tokens": read, "output_tokens": out}
        return rec("assistant", session, ts, uuid="%s-%s" % (mid, line), requestId=mid,
                   message={"id": mid, "model": model, "usage": u, "content": []})

    def sub(self, name, agent, records):
        self.env.write("s1/subagents/agent-%s.jsonl" % name, records)
        (self.env.tr / "s1" / "subagents" / ("agent-%s.meta.json" % name)).write_text(
            json.dumps({"agentType": agent}), encoding="utf-8")

    def usage(self):
        return self.env.signals()["tokens"]

    def test_a_request_split_over_lines_counts_once(self):
        self.env.write("a.jsonl", [self.asst("m1", "claude-opus-5-5", create=10, out=5, inp=2, read=100, line="a"),
                                   self.asst("m1", "claude-opus-5-5", create=10, out=5, inp=2, read=100, line="b"),
                                   self.asst("m2", "claude-opus-5-5", create=1, out=1, ts=OLD_TS)])
        self.env.run()
        row = self.usage()["by_agent_model"][0]
        self.assertEqual(row["agent"], "main / claude-opus-5-5")
        self.assertEqual((row["messages"], row["input"], row["cache_creation"], row["cache_read"], row["output"]),
                         (1, 2, 10, 100, 5))

    def test_skips_synthetic_and_all_zero_requests(self):
        self.env.write("a.jsonl", [self.asst("m1", "<synthetic>"), self.asst("m2", "claude-opus-5-5"),
                                   self.asst("m3", "claude-opus-5-5", out=3)])
        self.env.run()
        self.assertEqual([(r["agent"], r["messages"]) for r in self.usage()["by_agent_model"]],
                         [("main / claude-opus-5-5", 1)])

    def test_request_copied_to_another_file_counts_once(self):
        self.env.write("a.jsonl", [self.asst("m1", "claude-opus-5-5", out=3)])
        self.env.write("b.jsonl", [self.asst("m1", "claude-opus-5-5", out=3, session="s2")])
        self.env.run()
        self.assertEqual(self.usage()["by_agent_model"][0]["messages"], 1)

    def test_b_subagent_uses_meta_and_main_is_main(self):
        self.env.write("a.jsonl", [self.asst("m1", "claude-opus-5-5", create=1)])
        self.sub("1", "implementer", [self.asst("m2", "claude-sonnet-5-5", create=7)])
        self.env.run()
        names = {r["agent"] for r in self.usage()["by_agent_model"]}
        self.assertEqual(names, {"main / claude-opus-5-5", "implementer / claude-sonnet-5-5"})
        self.assertEqual(self.usage()["transcripts"][0]["transcript"], "implementer 1")
        self.assertIn("### By agent type and model", self.env.report())

    def test_c_shares_and_other_fold(self):
        self.env.write("a.jsonl", [self.asst("m1", "claude-opus-5-5", create=250, out=50)])
        self.sub("1", "implementer", [self.asst("m2", "claude-sonnet-5-5", create=60, out=40)])
        self.sub("2", "architect", [self.asst("m3", "claude-opus-5-5", create=70, out=30)])
        self.env.run("--top", "1")
        rows = self.usage()["by_agent_model"]
        self.assertEqual([r["agent"] for r in rows], ["main / claude-opus-5-5", "(other)"])
        self.assertEqual((rows[0]["share"], rows[1]["share"]), (0.6, 0.4))
        self.assertEqual((rows[1]["messages"], rows[1]["cache_creation"], rows[1]["output"]), (2, 130, 70))

    def _previous(self, rows):
        prev = self.env.root / "2026-10-01"
        prev.mkdir()
        sig = {"window": {"today": "2026-10-01"}, "output_size": {}, "tokens": {}}
        if rows is not None:
            sig["tokens"]["by_agent_model"] = rows
        (prev / "signals.json").write_text(json.dumps(sig), encoding="utf-8")

    def _two_agents(self):
        self.env.write("a.jsonl", [self.asst("m1", "claude-opus-5-5", create=600)])
        self.sub("1", "implementer", [self.asst("m2", "claude-sonnet-5-5", create=400)])

    def test_d_growth_flags_only_a_five_point_riser(self):
        self._two_agents()
        self._previous([{"agent": "main / claude-opus-5-5", "share": 0.58},
                        {"agent": "implementer / claude-sonnet-5-5", "share": 0.10}])
        code, out = self.env.run()
        g = self.usage()["growth"]
        self.assertEqual([r["agent"] for r in g["rows"]], ["implementer / claude-sonnet-5-5"])
        self.assertIn("token growth: 1 agent(s) grew 5+ points since 2026-10-01", out)
        self.assertIn("### Token growth since the previous run", self.env.report())

    def test_d_previous_run_without_by_agent_model_is_unchecked(self):
        self._two_agents()
        self._previous(None)
        code, out = self.env.run()
        self.assertIsNone(self.usage()["growth"]["baseline"])
        self.assertIn("token growth: UNCHECKED", out)

    def _agent_def(self, model):
        d = self.env.repo / ".claude" / "agents"
        d.mkdir(parents=True, exist_ok=True)
        (d / "x.md").write_text("---\nname: x\nmodel: %s\n---\nbody\n" % model, encoding="utf-8")

    def test_e_override_when_model_family_differs(self):
        self._agent_def("sonnet")
        self.sub("1", "x", [self.asst("m1", "claude-opus-5-5", create=1), self.asst("m2", "claude-opus-5-5", create=1),
                            self.asst("m3", "claude-sonnet-5-5", create=1)])
        code, out = self.env.run()
        self.assertEqual(self.usage()["overrides"],
                         [{"agent": "x", "defined": "sonnet", "observed": "claude-opus-5-5", "messages": 2}])
        self.assertIn("### Model overrides", self.env.report())
        self.assertIn("model overrides: 1", out)

    def test_e_no_override_when_matching_or_inherit_or_undefined(self):
        self._agent_def("claude-sonnet-5-5")
        self.sub("1", "x", [self.asst("m1", "claude-sonnet-5-5", create=1)])
        self.sub("2", "Explore", [self.asst("m2", "claude-opus-5-5", create=1)])
        self.env.run()
        self.assertEqual(self.usage()["overrides"], [])
        self._agent_def("inherit")
        self.env.run()
        self.assertEqual(self.usage()["overrides"], [])


class MemoryTests(Base):
    def test_stale_card_and_memory_size(self):
        (self.env.mem / "MEMORY.md").write_bytes(b"one\ntwo\nthree\n")
        old = self.env.mem / "features" / "old-card.md"
        old.write_text("---\nmodified: 2020-01-01\n---\nbody\n", encoding="utf-8")
        fresh = self.env.mem / "features" / "fresh-card.md"
        fresh.write_text("body\n", encoding="utf-8")
        self.env.write("a.jsonl", [tool_use("t1", "Skill", skill="x")])
        self.env.run()
        m = self.env.signals()["memory"]
        self.assertEqual(m["memory_md_lines"], 3)
        self.assertEqual(m["memory_md_bytes"], len("one\ntwo\nthree\n"))
        names = [c["name"] for c in m["stale_cards"]]
        self.assertIn("old-card.md", names)
        self.assertNotIn("fresh-card.md", names)


OPUS = "claude-opus-5-5"
MILLION = 1_000_000


def request(req, ts=TS, model=OPUS, inp=0, out=0, read=0, create=0, one_hour=None, thinking=None, tools=(),
            session="s1"):
    """One assistant record of API request `req`, shaped like a real transcript line."""
    usage = {"input_tokens": inp, "output_tokens": out, "cache_read_input_tokens": read,
             "cache_creation_input_tokens": create}
    if one_hour is not None:
        usage["cache_creation"] = {"ephemeral_1h_input_tokens": one_hour,
                                   "ephemeral_5m_input_tokens": create - one_hour}
    if thinking is not None:
        usage["output_tokens_details"] = {"thinking_tokens": thinking}
    content = [{"type": "tool_use", "id": t, "name": "Read", "input": {}} for t in tools]
    return rec("assistant", session, ts, uuid="u_" + req, requestId=req,
               message={"id": "msg_" + req, "model": model, "usage": usage, "content": content,
                        "stop_reason": "tool_use"})


class CostTests(Base):
    def subagent(self, name, kind, records, session="sess"):
        self.env.write("%s/subagents/agent-%s.jsonl" % (session, name), records)
        (self.env.tr / session / "subagents" / ("agent-%s.meta.json" % name)).write_text(
            json.dumps({"agentType": kind}), encoding="utf-8")

    def cost(self):
        self.env.run()
        return self.env.signals()["cost"]

    def row(self, agent="main"):
        return next(r for r in self.cost()["by_agent"] if r["agent"] == agent)

    def test_each_token_class_is_priced_at_its_own_rate(self):
        self.env.write("a.jsonl", [request("r1", inp=MILLION, out=MILLION, read=MILLION,
                                           create=MILLION, one_hour=MILLION)])
        r = self.row()
        self.assertAlmostEqual(r["input"], 4.0)
        self.assertAlmostEqual(r["output"], 20.0)
        self.assertAlmostEqual(r["cache_read"], 0.2)
        self.assertAlmostEqual(r["cache_write"], 8.0)
        self.assertAlmostEqual(r["cost"], 32.2)

    def test_five_minute_write_costs_less_than_one_hour(self):
        self.env.write("a.jsonl", [request("r1", create=MILLION, one_hour=0)])
        self.assertAlmostEqual(self.row()["cache_write"], 5.0)

    def test_write_with_no_ttl_split_is_priced_as_five_minute(self):
        self.env.write("a.jsonl", [request("r1", create=MILLION)])
        self.assertAlmostEqual(self.row()["cache_write"], 5.0)

    def test_one_m_model_suffix_is_priced_as_its_model(self):
        self.env.write("a.jsonl", [request("r1", model=OPUS + "[1m]", out=MILLION)])
        self.assertAlmostEqual(self.row()["output"], 20.0)

    def test_sonnet_has_its_own_rates(self):
        self.env.write("a.jsonl", [request("r1", model="claude-sonnet-5-5", out=MILLION, read=MILLION)])
        r = self.row()
        self.assertAlmostEqual(r["output"], 10.0)
        self.assertAlmostEqual(r["cache_read"], 0.2)

    def test_unknown_model_is_reported_not_priced_or_guessed(self):
        self.env.write("a.jsonl", [request("r1", model="claude-future-9", out=MILLION)])
        c = self.cost()
        self.assertEqual(c["unpriced"], {"claude-future-9": 1})
        self.assertEqual(c["total"], 0)
        self.assertEqual(c["by_agent"][0]["requests"], 1)

    def test_synthetic_and_all_zero_requests_are_not_counted(self):
        self.env.write("a.jsonl", [request("r1", model="<synthetic>", out=5), request("r2")])
        self.assertEqual(self.cost()["by_agent"], [])

    def test_request_written_in_several_records_counts_once_with_all_its_tool_calls(self):
        self.env.write("a.jsonl", [request("r1", out=10, tools=("t1",)), request("r1", out=20, tools=("t2",)),
                                   request("r1", out=20, tools=("t2",))])
        r = self.row()
        self.assertEqual(r["requests"], 1)
        self.assertEqual(r["tool_calls_per_request"], 2.0)
        self.assertAlmostEqual(r["output"], 20 * 20 / MILLION)

    def test_request_seen_in_two_files_counts_once(self):
        self.env.write("a.jsonl", [request("r1", out=MILLION)])
        self.env.write("b.jsonl", [request("r1", out=MILLION)])
        self.assertAlmostEqual(self.cost()["total"], 20.0)

    def test_request_before_the_window_is_excluded(self):
        self.env.write("a.jsonl", [request("old", ts=OLD_TS, out=MILLION), request("new", out=1)])
        self.assertEqual(self.row()["requests"], 1)

    def test_rows_are_per_agent_and_sorted_by_cost_with_shares(self):
        self.env.write("a.jsonl", [request("r1", out=MILLION, tools=("t1",))])
        self.subagent("a", "deep-reviewer", [request("r2", out=3 * MILLION, tools=("t2", "t3"))])
        rows = self.cost()["by_agent"]
        self.assertEqual([r["agent"] for r in rows], ["deep-reviewer", "main"])
        self.assertEqual(rows[0]["tool_calls_per_request"], 2.0)
        self.assertEqual(rows[1]["tool_calls_per_request"], 1.0)
        self.assertEqual(rows[0]["share"], 0.75)

    def test_mean_context_counts_input_read_and_write(self):
        self.env.write("a.jsonl", [request("r1", inp=10, read=100, create=90),
                                   request("r2", read=400)])
        self.assertEqual(self.row()["mean_context"], 300)

    def test_thinking_share_uses_only_requests_that_report_thinking(self):
        self.env.write("a.jsonl", [request("r1", out=1000, thinking=750), request("r2", out=9000)])
        self.assertEqual(self.row()["thinking_share"], 0.75)

    def test_thinking_share_is_none_when_no_request_reports_it(self):
        self.env.write("a.jsonl", [request("r1", out=1000)])
        self.assertIsNone(self.row()["thinking_share"])

    def test_main_context_buckets_and_their_edges(self):
        sizes = (199_999, 200_000, 300_000, 500_000)
        self.env.write("a.jsonl", [request("r%d" % s, read=s) for s in sizes])
        self.subagent("a", "implementer", [request("sub", read=900_000)])
        m = self.cost()["main_context"]
        self.assertEqual([(b["bucket"], b["requests"]) for b in m["buckets"]],
                         [("under 200k", 1), ("200k to 300k", 1), ("300k to 500k", 1), ("500k and over", 1)])
        self.assertEqual(m["mean"], sum(sizes) // 4)
        self.assertAlmostEqual(sum(b["share"] for b in m["buckets"]), 1.0, places=3)

    def test_cache_rebuilds_are_split_by_who_and_idle_gap(self):
        def at(minutes):
            return (hr.datetime(2026, 10, 10, 12) + hr.timedelta(minutes=minutes)).isoformat() + "Z"
        self.env.write("a.jsonl", [
            request("r1", ts=at(0), create=200_000),            # first request of the file
            request("r2", ts=at(1), create=120_000),            # under 5 minutes
            request("r3", ts=at(11), create=150_000),           # 5 minutes to 1 hour
            request("r4", ts=at(131), create=450_000, one_hour=450_000),  # over 1 hour
            request("r5", ts=at(132), create=100_000),          # not above the break threshold
        ])
        self.subagent("a", "implementer", [request("s1", ts=at(0), read=1), request("s2", ts=at(10), create=200_000)])
        rows = {(r["who"], r["gap"]): r for r in self.cost()["cache_rebuilds"]}
        self.assertEqual(set(rows), {("main", "first request"), ("main", "under 5 min"), ("main", "5 min to 1 h"),
                                     ("main", "over 1 h"), ("subagent", "5 min to 1 h")})
        self.assertEqual(rows["main", "over 1 h"]["events"], 1)
        self.assertEqual(rows["main", "over 1 h"]["mean_tokens"], 450_000)
        self.assertAlmostEqual(rows["main", "over 1 h"]["cost"], 3.6)

    def test_review_runs_split_by_session_and_a_one_hour_gap(self):
        def at(minutes):
            return (hr.datetime(2026, 10, 10, 12) + hr.timedelta(minutes=minutes)).isoformat() + "Z"

        def lens(name, minutes, session="sess"):
            self.subagent(name, "deep-reviewer", [request(name + "1", ts=at(minutes), out=MILLION, read=100),
                                                  request(name + "2", ts=at(minutes + 1), read=500)], session)
        lens("a", 0)
        lens("b", 50)     # within an hour of the previous lens: same run
        lens("c", 200)    # a new run
        lens("d", 0, "other")
        self.subagent("e", "implementer", [request("impl", ts=at(0), out=MILLION)])
        r = self.cost()["review_runs"]
        self.assertEqual(r["runs"], 3)
        self.assertEqual(r["lenses"], 4)
        self.assertEqual(r["lenses_per_run_max"], 2)
        self.assertAlmostEqual(r["run_cost_max"], 40.0, places=2)
        self.assertEqual(r["lens_requests_p50"], 2)
        self.assertEqual(r["lens_peak_context_p50"], 500)

    def test_workflow_subagent_transcripts_are_scanned(self):
        self.subagent("w", "implementer", [request("r1", out=MILLION)])
        nested = self.env.tr / "sess" / "subagents" / "workflows" / "wf_1"
        nested.mkdir(parents=True)
        (nested / "agent-n.jsonl").write_text(json.dumps(request("r2", out=MILLION)) + "\n", encoding="utf-8")
        (nested / "agent-n.meta.json").write_text(json.dumps({"agentType": "Explore"}), encoding="utf-8")
        c = self.cost()
        self.assertEqual(self.env.signals()["sessions"]["subagent_files"], 2)
        self.assertEqual({r["agent"] for r in c["by_agent"]}, {"implementer", "Explore"})

    def test_lens_in_a_workflow_folder_belongs_to_its_session(self):
        self.subagent("a", "deep-reviewer", [request("r1", out=1)])
        nested = self.env.tr / "sess" / "subagents" / "workflows" / "wf_1"
        nested.mkdir(parents=True)
        (nested / "agent-n.jsonl").write_text(json.dumps(request("r2", out=1)) + "\n", encoding="utf-8")
        (nested / "agent-n.meta.json").write_text(json.dumps({"agentType": "deep-reviewer"}), encoding="utf-8")
        r = self.cost()["review_runs"]
        self.assertEqual((r["runs"], r["lenses"]), (1, 2))

    def test_no_review_runs_is_zero_not_an_error(self):
        self.env.write("a.jsonl", [request("r1", out=1)])
        self.assertEqual(self.cost()["review_runs"]["runs"], 0)

    def test_report_names_the_proxy_and_shows_the_tables(self):
        self.env.write("a.jsonl", [request("r1", out=MILLION, read=250_000, tools=("t1",), thinking=5)])
        self.env.run()
        text = self.env.report()
        self.assertIn("## 11. Cost at list prices", text)
        self.assertIn("proxy", text)
        self.assertIn("| main |", text)
        self.assertIn("200k to 300k", text)

    def test_no_usage_data_reads_as_that_in_the_report(self):
        self.env.write("a.jsonl", [human("hello")])
        self.env.run()
        self.assertEqual(self.env.signals()["cost"]["total"], 0)
        self.assertIn("no priced requests in the window", self.env.report())

    def test_unparseable_timestamp_does_not_break_the_run(self):
        self.env.write("a.jsonl", [request("r1", ts="not-a-time", create=200_000)])
        code, _ = self.env.run(since=None)
        self.assertEqual(code, 0)


class ExcludeSessionTests(Base):
    def _two_sessions(self):
        self.env.write("keep.jsonl", [tool_use("t1", "Bash", session="keep", command="ls")])
        self.env.write("keep/subagents/agent-a1.jsonl", [tool_use("t2", "Bash", session="keep", command="ls")])
        self.env.write("skip.jsonl", [tool_use("t3", "Bash", session="skip", command="ls")])
        self.env.write("skip/subagents/agent-a2.jsonl", [tool_use("t4", "Bash", session="skip", command="ls")])
        self.env.write("skip/subagents/workflows/wf_1/agent-a3.jsonl",
                       [tool_use("t5", "Bash", session="skip", command="ls")])

    def _files(self):
        s = self.env.signals()["sessions"]
        return s["main_files"], s["subagent_files"]

    def test_without_the_flag_every_session_counts(self):
        self._two_sessions()
        code, _ = self.env.run()
        self.assertEqual(code, 0)
        self.assertEqual(self._files(), (2, 3))

    def test_excluded_session_loses_its_main_subagent_and_workflow_transcripts(self):
        self._two_sessions()
        code, _ = self.env.run("--exclude-session", "skip")
        self.assertEqual(code, 0)
        self.assertEqual(self._files(), (1, 1))

    def test_excluding_every_session_reads_as_unchecked_not_as_clean(self):
        self._two_sessions()
        code, out = self.env.run("--exclude-session", "skip", "--exclude-session", "keep")
        self.assertEqual(code, 3)
        self.assertIn("UNCHECKED", out)

    def test_report_names_what_it_left_out(self):
        self._two_sessions()
        self.env.run("--exclude-session", "skip")
        self.assertEqual(self.env.signals()["window"]["excluded_sessions"], ["skip"])
        self.assertRegex(self.env.report(), r"Excluded sessions.*skip")

    def test_an_unknown_id_warns_and_is_not_recorded_as_excluded(self):
        self._two_sessions()
        code, out = self.env.run("--exclude-session", "skip", "--exclude-session", "sk1p")
        self.assertEqual(code, 0)
        self.assertRegex(out, r"warning: --exclude-session sk1p matches no transcript")
        self.assertEqual(out.count("matches no transcript"), 1, "only the unknown id warns")
        self.assertEqual(self.env.signals()["window"]["excluded_sessions"], ["skip"])
        self.assertNotRegex(self.env.report(), r"Excluded sessions.*sk1p")


class BannerVersionRuleTests(Base):
    def banner(self, text, session):
        return attachment(session=session, type="hook_success", hookEvent="SessionStart",
                          hookName="SessionStart:startup", content="=== TAOM ===\n" + text + "\n")

    def test_banner_table_pairs_printing_invocation_and_direct_command(self):
        self.env.write("s1.jsonl", [
            self.banner("HARNESS RETRO DUE: last retro 2026-10-21", "s1"),
            tool_use("t1", "Skill", session="s1", skill="harness-retro")])
        self.env.write("s2.jsonl", [
            self.banner("HARNESS RETRO DUE: last retro 2026-10-21", "s2"),
            human("<command-name>/harness-retro</command-name>", session="s2"),
            tool_use("t2", "Bash", session="s2", command="python tools/harness_retro.py --since 2026-10-01")])
        self.env.write("s3.jsonl", [self.banner("GAME VERSION DRIFT: installed x", "s3")])
        self.env.run()
        rows = {r["banner"]: r for r in self.env.signals()["banners"]}
        retro = rows["HARNESS RETRO DUE"]
        self.assertEqual((retro["skill"], retro["sessions_printing"], retro["invocations"],
                          retro["direct_sessions"]), ("harness-retro", 2, 2, 1))
        drift = rows["GAME VERSION DRIFT"]
        self.assertEqual((drift["sessions_printing"], drift["invocations"], drift["direct_sessions"]), (1, 0, 0))
        self.assertEqual(rows["ARMORY ART DRIFT"]["sessions_printing"], 0)
        self.assertIn("## 7. Usage", self.env.report())
        self.assertIn("HARNESS RETRO DUE", self.env.report())

    def test_a_command_that_only_names_the_script_is_not_a_direct_run(self):
        self.env.write("s1.jsonl", [
            tool_use("t1", "Bash", session="s1", command="git diff tools/harness_retro.py"),
            tool_use("t2", "Bash", session="s1", command="git status -- tools/tests/test_harness_retro.py"),
            tool_use("t3", "Bash", session="s1", command="grep -n BANNERS tools/harness_retro.py")])
        self.env.write("s2.jsonl", [
            tool_use("t4", "Bash", session="s2", command="cd /x && python -B tools/harness_retro.py --top 5")])
        self.env.write("s3.jsonl", [
            tool_use("t5", "Bash", session="s3", command="ls && ./tools/audit_armory_refs.py")])
        self.env.run()
        rows = {r["banner"]: r["direct_sessions"] for r in self.env.signals()["banners"]}
        self.assertEqual((rows["HARNESS RETRO DUE"], rows["ARMORY ART DRIFT"]), (1, 1))

    def test_a_test_module_named_after_the_script_is_not_a_direct_run(self):
        """`harness_retro.py` ends `test_harness_retro.py`, so an unanchored pattern counted the test run."""
        scripts = {"GAME VERSION DRIFT": "native_archive.py", "ARMORY ART DRIFT": "audit_armory_refs.py",
                   "UPSTREAM AHEAD": "upstream_watch.py", "HARNESS RETRO DUE": "harness_retro.py"}
        for banner, _skill, rx in hr.BANNERS:
            script = scripts[banner]
            self.assertTrue(hr.command_runs_script(f"python -B tools/{script}", rx), script)
            self.assertTrue(hr.command_runs_script(f"python -B tools\\\\{script}", rx), script)
            self.assertTrue(hr.command_runs_script(f"./tools/{script}", rx), script)
            self.assertFalse(hr.command_runs_script(f"python tools/tests/test_{script}", rx), script)

    def test_a_banner_outside_session_start_output_is_not_counted(self):
        self.env.write("s1.jsonl", [attachment(session="s1", type="hook_success", hookEvent="PostToolUse",
                                               content="HARNESS RETRO DUE")])
        self.env.run()
        self.assertEqual({r["banner"]: r["sessions_printing"] for r in self.env.signals()["banners"]}
                         ["HARNESS RETRO DUE"], 0)

    def test_versions_are_listed_with_their_session_counts(self):
        self.env.write("a.jsonl", [rec("user", "s1", message={"content": "hi"}, version="2.1.301"),
                                   rec("user", "s2", message={"content": "hi"}, version="2.1.314")])
        self.env.write("b.jsonl", [rec("user", "s3", message={"content": "hi"}, version="2.1.314"),
                                   rec("user", "s3", message={"content": "again"}, version="2.1.314")])
        self.env.run()
        sig = self.env.signals()
        self.assertEqual(sig["versions"], [{"version": "2.1.301", "sessions": 1},
                                           {"version": "2.1.314", "sessions": 2}])
        self.assertIn("| 2.1.314 | 2 |", self.env.report())

    def test_versions_sort_numerically(self):
        self.env.write("a.jsonl", [rec("user", "s1", message={"content": "x"}, version="2.1.9"),
                                   rec("user", "s2", message={"content": "x"}, version="2.1.100")])
        self.env.run()
        self.assertEqual([v["version"] for v in self.env.signals()["versions"]], ["2.1.9", "2.1.100"])

    def test_rule_files_loaded_come_from_instructions_and_nested_memory(self):
        files = [{"path": "e:\\repos\\TAOM\\CLAUDE.md", "type": "Project"},
                 {"path": "e:\\repos\\TAOM\\.claude\\rules\\output-style.md", "type": "Project"}]
        self.env.write("s1.jsonl", [
            attachment(session="s1", type="instructions", files=files),
            attachment(session="s1", type="nested_memory",
                       path="e:\\repos\\TAOM\\.claude\\rules\\xml-data.md",
                       content={"path": "e:\\repos\\TAOM\\.claude\\rules\\xml-data.md", "content": "x"})])
        self.env.write("s1/subagents/agent-1.jsonl", [
            attachment(session="s1", type="instructions", files=files)])
        self.env.run()
        rules = {r["rule"]: r for r in self.env.signals()["rules_loaded"]}
        self.assertEqual((rules["output-style.md"]["sessions"], rules["output-style.md"]["subagents"]), (1, 1))
        self.assertEqual((rules["xml-data.md"]["sessions"], rules["xml-data.md"]["subagents"]), (1, 0))
        self.assertNotIn("CLAUDE.md", rules)


if __name__ == "__main__":
    unittest.main()
