"""The /improve Workflow scripts (.claude/skills/improve/workflows/*.js).

Claude Code's Workflow tool runs these scripts, and CI cannot run that tool, so this file checks
what can fail without it. Each check answers a failure of the first review sprint that used them:

- CRLF scripts were refused by the Workflow approval check ("control characters that would be
  hidden in the approval dialog"), so every script must be LF with no hidden character.
- Run data was hard-coded (report dates, the version label, a baseline commit, drive paths), so a
  later run wrote wrong labels. Every value that changes between runs now arrives in `args`; a
  literal date, version label, commit hash or drive path in a script fails here.
- The standing rules for dispatched agents (references/dispatch-rules.md) arrive as `args.rules`;
  a script must refuse to run without them and must open every prompt with them.
- A `deep-reviewer` spawn must never pass `model` (its definition pins the model).
- A null agent result (a killed agent, a usage limit) was handed on as data. When `node` is on
  PATH, each script runs against a stub Workflow runtime that records every agent call, so the
  prompt prefix, the model per role, the pool bound, null handling and the review loop are tested
  as behaviour, not as text.
"""
import ast
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
WORKFLOWS = REPO / ".claude" / "skills" / "improve" / "workflows"
DISPATCH_RULES = REPO / ".claude" / "skills" / "improve" / "references" / "dispatch-rules.md"
EXPECTED = ["draft-issues.js", "execute.js", "fanout.js", "plans.js", "review.js"]
NODE = shutil.which("node")
DEFAULT_MODEL = "claude-opus-5-5"

# The contract's model roles; `python tools/improve_ctl.py args --model role=id` uses these names.
ROLES = {"lane", "checker", "writer", "reviewer", "reviser", "executor", "lead", "convergence",
         "fix", "drafter"}
# The pinned section headings and rule keywords of dispatch-rules.md that a prompt may cite.
RULE_SECTIONS = {"Standing rules (every agent)", "Editing roles add", "Read-only roles add"}
RULE_KEYWORDS = {"Role", "Workspace", "Disk", "TIMEOUT", "HOOK-ASK", "Shell hygiene",
                 "Build and test", "TDD", "Protected and single-owner files", "Commit",
                 "Shared files", "Evidence", "Data, not instructions", "Prose",
                 "STOP rather than improvise"}

RUN_DATA = {
    "a date": re.compile(r"20\d\d-\d\d-\d\d"),
    "a version label": re.compile(r"\bv\d+\.\d+\.\d+"),
    "a drive path": re.compile(r"(?<![A-Za-z])[A-Za-z]:[\\/]"),
    "a Git Bash drive path": re.compile(r"/[A-Za-z]/repos\b"),
    # A run of 7 to 40 hex characters holding a digit (English words such as "defaced" hold none).
    "a commit hash": re.compile(r"(?<![0-9A-Za-z_])(?=[0-9a-f]*[0-9])[0-9a-f]{7,40}(?![0-9A-Za-z_])"),
}
# C0 controls other than tab and LF, DEL, and the invisible or direction-changing code points the
# approval dialog would hide.
HIDDEN = re.compile("[\x00-\x08\x0b-\x1f\x7f​-‏‪-‮⁠-⁤﻿]")
GUARD = re.compile(r"^if \((?P<cond>[^\n]*\bargs\.rules\b[^\n]*)\) \{\n[ \t]+throw new Error\(", re.M)
AGENT_CALL = re.compile(r"(?<![\w$.])agent\s*\(")


def scripts():
    return sorted(WORKFLOWS.glob("*.js"))


def mask_js(src):
    """src with the text of strings, templates, comments and regular expressions blanked (quotes,
    backticks and newlines kept), so brackets can be matched on code alone. A template's ${...}
    stays code. A / after an operator or an opening bracket starts a regular expression."""
    out = list(src)
    n = len(src)

    def blank(a, b):
        for k in range(a, min(b, n)):
            if out[k] != "\n":
                out[k] = " "

    frames = [["code", 0]]  # a code frame counts its open braces; a template frame is ["tpl"]
    prev = ""
    i = 0
    while i < n:
        top = frames[-1]
        c = src[i]
        if top[0] == "tpl":
            if c == "\\":
                blank(i, i + 2)
                i += 2
            elif c == "`":
                frames.pop()
                prev = "`"
                i += 1
            elif src.startswith("${", i):
                frames.append(["code", 0])
                prev = "{"
                i += 2
            else:
                blank(i, i + 1)
                i += 1
            continue
        if c in " \t\r\n":
            i += 1
            continue
        if src.startswith("//", i):
            j = src.find("\n", i)
            j = n if j < 0 else j
            blank(i, j)
            i = j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            blank(i, j)
            i = j
            continue
        if c in "'\"":
            j = i + 1
            while j < n and src[j] != c and src[j] != "\n":
                j += 2 if src[j] == "\\" else 1
            blank(i + 1, j)
            i = j + 1
            prev = c
            continue
        if c == "`":
            frames.append(["tpl"])
            i += 1
            continue
        if c == "/" and (prev == "" or prev in "(,=:[!&|?{};+-*%<>~^"):
            j = i + 1
            in_class = False
            while j < n and src[j] != "\n":
                if src[j] == "\\":
                    j += 2
                    continue
                if src[j] == "[":
                    in_class = True
                elif src[j] == "]":
                    in_class = False
                elif src[j] == "/" and not in_class:
                    break
                j += 1
            blank(i + 1, j)
            i = j + 1
            while i < n and src[i].isalpha():
                i += 1
            prev = "/"
            continue
        if c == "{":
            top[1] += 1
        elif c == "}":
            if len(frames) > 1 and top[1] == 0:
                frames.pop()  # the end of a ${...}: back in the template
                i += 1
                prev = "}"
                continue
            top[1] -= 1
        prev = c
        i += 1
    return "".join(out)


def close_of(masked, open_at):
    """Index of the bracket that closes the one at open_at."""
    pairs = {"(": ")", "[": "]", "{": "}"}
    stack = []
    for k in range(open_at, len(masked)):
        ch = masked[k]
        if ch in pairs:
            stack.append(pairs[ch])
        elif ch in ")]}":
            if not stack or stack.pop() != ch:
                raise ValueError(f"unbalanced {ch!r} at offset {k}")
            if not stack:
                return k
    raise ValueError(f"no close for the bracket at offset {open_at}")


def split_top(masked, a, b):
    """(start, end) spans of the comma-separated parts of masked[a:b] at bracket depth 0."""
    parts, depth, start = [], 0, a
    for k in range(a, b):
        ch = masked[k]
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif ch == "," and depth == 0:
            parts.append((start, k))
            start = k + 1
    if masked[start:b].strip():
        parts.append((start, b))
    return parts


def agent_calls(src):
    """Each agent(...) call as (line, first argument text, {option key: value text} or None, spread)."""
    masked = mask_js(src)
    calls = []
    for m in AGENT_CALL.finditer(masked):
        open_at = m.end() - 1
        close = close_of(masked, open_at)
        args = split_top(masked, open_at + 1, close)
        line = src.count("\n", 0, m.start()) + 1
        first = src[args[0][0]:args[0][1]].strip() if args else ""
        options, spread = None, False
        if len(args) > 1:
            a, b = args[1]
            text = src[a:b].strip()
            if text.startswith("{") and text.endswith("}"):
                lead = a + (len(src[a:b]) - len(src[a:b].lstrip()))
                end = close_of(masked, lead)
                options = {}
                for pa, pb in split_top(masked, lead + 1, end):
                    entry = masked[pa:pb].strip()
                    if entry.startswith("..."):
                        spread = True
                        continue
                    km = re.match(r"([A-Za-z_$][\w$]*)\s*(:|$)", entry)
                    if km:
                        value = src[pa:pb].split(":", 1)[1].strip() if km.group(2) == ":" else km.group(1)
                        options[km.group(1)] = value
        calls.append((line, first, options, spread))
    return calls


LITERAL_TOKEN = re.compile(r"""\s+|//[^\n]*|(?P<p>[{}\[\]:,])|(?P<s>'(?:[^'\\\n]|\\.)*'|"(?:[^"\\\n]|\\.)*")"""
                           r"""|(?P<n>-?\d+(?:\.\d+)?)|(?P<i>[A-Za-z_$][\w$]*)""")


def parse_literal(text):
    """A JS object literal of strings, numbers, true, false, null, arrays and objects, as Python.
    Raises ValueError on anything else: a variable, a call, a template, an operator."""
    toks, pos = [], 0
    while pos < len(text):
        m = LITERAL_TOKEN.match(text, pos)
        if not m or m.end() == pos:
            raise ValueError(f"not a literal at {text[pos:pos + 30]!r}")
        pos = m.end()
        for kind in ("p", "s", "n", "i"):
            if m.group(kind) is not None:
                toks.append((kind, m.group(kind)))
                break

    def value(k):
        kind, t = toks[k]
        if t == "{":
            return container(k, "}")
        if t == "[":
            return container(k, "]")
        if kind == "s":
            return ast.literal_eval(t), k + 1
        if kind == "n":
            return (float(t) if "." in t else int(t)), k + 1
        if kind == "i" and t in ("true", "false", "null"):
            return {"true": True, "false": False, "null": None}[t], k + 1
        raise ValueError(f"{t!r} is not a literal value")

    def container(k, closer):
        is_obj = closer == "}"
        result = {} if is_obj else []
        k += 1
        while toks[k][1] != closer:
            if is_obj:
                kind, key = toks[k]
                if kind not in ("i", "s"):
                    raise ValueError(f"{key!r} is not an object key")
                key = ast.literal_eval(key) if kind == "s" else key
                if toks[k + 1][1] != ":":
                    raise ValueError(f"no ':' after the key {key!r} (shorthand keys are variables)")
                result[key], k = value(k + 2)
            else:
                item, k = value(k)
                result.append(item)
            if toks[k][1] == ",":
                k += 1
            elif toks[k][1] != closer:
                raise ValueError(f"expected ',' or {closer!r}, found {toks[k][1]!r}")
        return result, k + 1

    result, k = value(0)
    if k != len(toks):
        raise ValueError("text after the literal")
    return result


def meta_of(src):
    first = src.split("\n", 1)[0]
    if first.rstrip() != "export const meta = {":
        raise ValueError(f"line 1 is {first!r}, not 'export const meta = {{'")
    start = src.index("{")
    end = close_of(mask_js(src), start)
    return parse_literal(src[start:end + 1])


class StaticChecks(unittest.TestCase):
    """Checks on the text of each script; they need nothing but Python."""

    def test_the_five_workflows_exist(self):
        self.assertEqual([p.name for p in scripts()], EXPECTED)

    def test_lf_only_and_no_hidden_characters(self):
        for path in scripts():
            with self.subTest(path.name):
                raw = path.read_bytes()
                cr = raw.find(b"\r")
                line = raw.count(b"\n", 0, cr) + 1
                self.assertEqual(cr, -1, f"a CR byte on line {line}: the Workflow approval check refuses the script")
                bad = HIDDEN.search(raw.decode("utf-8"))
                self.assertIsNone(bad, f"a hidden character {bad.group() if bad else ''!r}")

    def test_meta_is_a_pure_literal(self):
        for path in scripts():
            with self.subTest(path.name):
                try:
                    meta = meta_of(path.read_text(encoding="utf-8"))
                except ValueError as e:
                    self.fail(f"meta is not a pure literal: {e}")
                self.assertEqual(meta.get("name"), "improve-" + path.stem)
                self.assertIsInstance(meta.get("description"), str)
                self.assertTrue(meta.get("phases"), "meta.phases is empty")
                for ph in meta["phases"]:
                    self.assertIsInstance(ph.get("title"), str)

    def test_no_run_data_literals(self):
        for path in scripts():
            text = path.read_text(encoding="utf-8")
            for what, pattern in RUN_DATA.items():
                with self.subTest(path.name, kind=what):
                    hits = [f"line {text.count(chr(10), 0, m.start()) + 1}: {m.group()!r}"
                            for m in pattern.finditer(text)]
                    self.assertEqual(hits, [], f"{what} in the script; pass it in args instead")

    def test_refuses_to_run_without_args_rules(self):
        for path in scripts():
            with self.subTest(path.name):
                text = path.read_text(encoding="utf-8")
                guard = GUARD.search(text)
                self.assertIsNotNone(guard, "no top-level `if (... args.rules ...) {` that throws")
                self.assertIn("const RULES = args.rules", text)
                first_call = AGENT_CALL.search(mask_js(text))
                self.assertIsNotNone(first_call, "the script never calls agent()")
                self.assertLess(guard.start(), first_call.start(), "the guard must come before any agent() call")

    def test_every_agent_call_opens_with_the_rules_and_keeps_model_off_deep_reviewer(self):
        for path in scripts():
            for line, first, options, spread in agent_calls(path.read_text(encoding="utf-8")):
                with self.subTest(path.name, line=line):
                    self.assertTrue(first.startswith("`${RULES}"),
                                    f"the prompt does not open with the standing rules: {first[:40]!r}")
                    self.assertIsNotNone(options, "the options must be an object literal, so the model rule can be read")
                    agent_type = options.get("agentType")
                    if agent_type is not None:
                        self.assertRegex(agent_type, r"^'[^']*'$|^\"[^\"]*\"$", "agentType must be a string literal")
                    if agent_type in ("'deep-reviewer'", '"deep-reviewer"'):
                        self.assertNotIn("model", options, "a deep-reviewer call passes model")
                        self.assertFalse(spread, "a deep-reviewer call spreads options, which could carry model")

    def test_roles_and_rule_citations_are_the_contract_names(self):
        for path in scripts():
            with self.subTest(path.name):
                text = path.read_text(encoding="utf-8")
                roles = set(re.findall(r"\brunAgent\(\s*'([^']*)'", text))
                self.assertTrue(roles, "no runAgent('<role>', ...) call")
                self.assertLessEqual(roles, ROLES)
                sections = set(re.findall(r"rules\\?' section \"([^\"]+)\"", text))
                self.assertTrue(sections, "no prompt tells the agent which rules section applies to its role")
                self.assertLessEqual(sections, RULE_SECTIONS)
                self.assertLessEqual(set(re.findall(r'standing rule "([^"]+)"', text)), RULE_KEYWORDS)

    @unittest.skipUnless(NODE, "node is not on PATH")
    def test_node_parses_each_script(self):
        # The runtime runs the body as an async function (top-level await and return), so check it
        # wrapped the same way; `export` is only legal in a module, hence the rename.
        for path in scripts():
            with self.subTest(path.name), tempfile.TemporaryDirectory() as tmp:
                body = path.read_text(encoding="utf-8").replace("export const meta", "const meta", 1)
                wrapped = Path(tmp) / "wrapped.js"
                wrapped.write_text("(async function workflow(args) {\n" + body + "\n})\n", encoding="utf-8")
                done = subprocess.run([NODE, "--check", str(wrapped)], capture_output=True, text=True, timeout=60)
                self.assertEqual(done.returncode, 0, done.stderr)


@unittest.skipUnless(DISPATCH_RULES.exists(), "dispatch-rules.md is not on this branch")
class DispatchRulesFile(unittest.TestCase):
    def test_the_pinned_headings_and_rules(self):
        text = DISPATCH_RULES.read_text(encoding="utf-8")
        lines = {ln.rstrip() for ln in text.splitlines()}
        for heading in sorted(RULE_SECTIONS):
            self.assertIn("## " + heading, lines)
        self.assertIn("HOOK-ASK", text)
        self.assertIn("TIMEOUT", text)
        self.assertNotIn("—", text)
        self.assertNotIn("–", text)


# A stub of the Workflow runtime: runs a script's body as an async function, records every agent()
# call, answers from a scenario, and throws where the real runtime throws (Date.now(), Math.random(),
# new Date() without arguments).
HARNESS = r"""
'use strict'
const fs = require('fs')
const [scriptPath, scenarioPath] = process.argv.slice(2)
const source = fs.readFileSync(scriptPath, 'utf8').replace('export const meta', 'const meta')
const scenario = JSON.parse(fs.readFileSync(scenarioPath, 'utf8'))
const calls = []
const logs = []
let inFlight = 0
let maxInFlight = 0
// A label's reply: its own entry in replies (null stands for an agent that died), else the first
// matching prefix, else null.
function reply(label) {
  const replies = scenario.replies || {}
  if (Object.prototype.hasOwnProperty.call(replies, label)) return replies[label]
  for (const [prefix, value] of scenario.prefixes || []) if (String(label).startsWith(prefix)) return value
  return null
}
async function agent(prompt, opts) {
  const o = opts || {}
  inFlight++
  maxInFlight = Math.max(maxInFlight, inFlight)
  calls.push({ label: o.label, prompt, keys: Object.keys(o), model: o.model, agentType: o.agentType, schema: !!o.schema })
  for (let i = 0; i < 5; i++) await new Promise(r => setImmediate(r))
  inFlight--
  const r = reply(o.label)
  return r === null || r === undefined ? r : JSON.parse(JSON.stringify(r))
}
const StrictDate = new Proxy(Date, {
  construct(target, argv) { if (!argv.length) throw new Error('new Date() without arguments'); return new target(...argv) },
  apply() { throw new Error('Date() called as a function') },
  get(target, key) { if (key === 'now') return () => { throw new Error('Date.now()') }; return target[key] },
})
const StrictMath = Object.create(Math)
StrictMath.random = () => { throw new Error('Math.random()') }
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor
const run = new AsyncFunction('args', 'agent', 'parallel', 'pipeline', 'phase', 'log', 'Date', 'Math', source)
const parallel = thunks => Promise.all(thunks.map(t => t()))
const pipeline = () => { throw new Error('pipeline() is not stubbed') }
const out = o => process.stdout.write(JSON.stringify({ ...o, calls, logs, maxInFlight }))
run(scenario.args, agent, parallel, pipeline, () => {}, m => logs.push(String(m)), StrictDate, StrictMath)
  .then(result => out({ ok: true, result }), e => out({ ok: false, error: String((e && e.message) || e) }))
"""

RULES = "STANDING RULES FIXTURE (HOOK-ASK, TIMEOUT)"


def run_args(**extra):
    args = {"rules": RULES, "repo": "/w/repo", "runRoot": "/w/run", "scratch": "/w/scratch",
            "tmp": "/w/tmp", "date": "DATE-X", "version": "VERSION-X", "model": {}, "pool": 2}
    args.update(extra)
    return args


@unittest.skipUnless(NODE, "node is not on PATH")
class StubRuntime(unittest.TestCase):
    """Each script run against the stub runtime, one scenario per behaviour the contract pins."""

    def run_script(self, name, scenario):
        with tempfile.TemporaryDirectory() as tmp:
            harness = Path(tmp) / "harness.js"
            harness.write_text(HARNESS, encoding="utf-8")
            scen = Path(tmp) / "scenario.json"
            scen.write_text(json.dumps(scenario), encoding="utf-8")
            done = subprocess.run([NODE, str(harness), str(WORKFLOWS / name), str(scen)],
                                  capture_output=True, text=True, encoding="utf-8", timeout=60)
        self.assertEqual(done.returncode, 0, done.stderr)
        return json.loads(done.stdout)

    EDITING_LABELS = ("lead-", "fix-", "exec-", "decide-", "stage-")

    def assert_contract(self, out, models=None):
        """Every call opens with the rules, then states its role (which rules sections bind it);
        deep-reviewer calls carry no model key; every other call carries its role's model and a
        schema; the pool bound held."""
        self.assertTrue(out["ok"], out.get("error"))
        models = models or {}
        for call in out["calls"]:
            with self.subTest(call=call["label"]):
                self.assertTrue(call["prompt"].startswith(RULES), "the prompt does not open with args.rules")
                role = "EDITING" if call["label"].startswith(self.EDITING_LABELS) else "READ-ONLY"
                self.assertTrue(call["prompt"][len(RULES):].lstrip("\n").startswith(f"YOUR ROLE: {role}."),
                                f"the line after the rules must state the role {role}")
                if call.get("agentType") == "deep-reviewer":
                    self.assertNotIn("model", call["keys"])
                else:
                    self.assertTrue(call["schema"], "an acted-on agent has no schema")
                    self.assertEqual(call.get("model"), models.get(call["label"], DEFAULT_MODEL))
        self.assertLessEqual(out["maxInFlight"], 2)

    def labels(self, out):
        return [c["label"] for c in out["calls"]]

    def test_every_script_refuses_to_run_without_rules(self):
        for name in EXPECTED:
            with self.subTest(name):
                args = run_args(items=[{"num": "001"}])
                del args["rules"]
                out = self.run_script(name, {"args": args})
                self.assertFalse(out["ok"])
                self.assertIn("args.rules", out["error"])
                self.assertEqual(out["calls"], [])

    # fanout.js ---------------------------------------------------------------------------------

    def fanout_args(self, **extra):
        return run_args(
            model={"lane": "LANE-MODEL"},
            items=[{"key": "a", "prompt": "TASK A", "out": "/w/run/a.md"},
                   {"key": "b", "prompt": "TASK B", "out": "/w/run/b.md"},
                   {"key": "c", "prompt": "TASK C", "out": "/w/run/c.md"}],
            checker={"prompt": "RECHECK", "selectFrom": {"field": "verdicts", "match": [{"verdict": "REFUTED"}]}},
            **extra)

    def test_fanout_sends_only_the_selected_rows_to_the_checker(self):
        out = self.run_script("fanout.js", {"args": self.fanout_args(), "replies": {
            "a": {"verdicts": [{"id": "A1", "verdict": "REFUTED"}, {"id": "A2", "verdict": "STILL_VALID"}]},
            "b": {"verdicts": [{"id": "B1", "verdict": "FIXED"}]},
            "c": {"verdicts": []},
            "checker": {"checks": [], "summary": "ok"}}})
        self.assert_contract(out, {"a": "LANE-MODEL", "b": "LANE-MODEL", "c": "LANE-MODEL"})
        self.assertEqual(self.labels(out)[-1], "checker")
        checker = out["calls"][-1]["prompt"]
        self.assertIn('"A1"', checker)
        self.assertNotIn('"A2"', checker)
        self.assertNotIn('"B1"', checker)
        self.assertEqual(out["result"]["failures"], [])

    def test_fanout_reports_a_dead_lane_and_holds_the_checker(self):
        out = self.run_script("fanout.js", {"args": self.fanout_args(), "replies": {
            "a": {"verdicts": [{"id": "A1", "verdict": "REFUTED"}]}, "b": None, "c": {"verdicts": []}}})
        self.assert_contract(out, {"a": "LANE-MODEL", "b": "LANE-MODEL", "c": "LANE-MODEL"})
        self.assertNotIn("checker", self.labels(out))
        self.assertEqual(len(out["result"]["failures"]), 1)
        self.assertIn("b", out["result"]["failures"][0])

    # plans.js ----------------------------------------------------------------------------------

    def plan_item(self, **extra):
        item = {"num": "031", "slug": "thing", "title": "T", "priority": "P2", "category": "DX",
                "depends_on": "", "brief": "BRIEF TEXT", "mode": "write"}
        item.update(extra)
        return item

    def plans_args(self, *items, **extra):
        return run_args(base="BASE-REF", baseline="BASELINE LINE", planDir="/w/repo/plans",
                        items=list(items), **extra)

    def test_plans_stops_at_not_planned(self):
        out = self.run_script("plans.js", {"args": self.plans_args(self.plan_item()), "replies": {
            "write-031": {"status": "NOT_PLANNED", "path": "/w/repo/plans/031-thing.md"}}})
        self.assert_contract(out)
        self.assertEqual(self.labels(out), ["write-031"])
        self.assertEqual(out["result"][0]["status"], "NOT_PLANNED")

    def test_plans_revises_after_blocking_items_and_stops_when_clean(self):
        blocking = {"blocking": ["step 3 has no expected output"], "non_blocking": [],
                    "excerpt_mismatches": [], "executable_by_weak_model": False}
        clean = {"blocking": [], "non_blocking": [], "excerpt_mismatches": [], "executable_by_weak_model": True}
        out = self.run_script("plans.js", {"args": self.plans_args(self.plan_item(), reviewRounds=2), "replies": {
            "write-031": {"status": "WRITTEN", "path": "/w/repo/plans/031-thing.md"},
            "review-031-r1": blocking,
            "revise-031-r1": {"summary": "fixed step 3", "not_fixed": []},
            "review-031-r2": clean}})
        self.assert_contract(out)
        self.assertEqual(self.labels(out), ["write-031", "review-031-r1", "revise-031-r1", "review-031-r2"])
        self.assertEqual(out["result"][0]["status"], "CLEAN")

    def test_plans_reports_what_the_last_revision_left_unreviewed(self):
        blocking = {"blocking": ["x"], "non_blocking": [], "excerpt_mismatches": [], "executable_by_weak_model": False}
        out = self.run_script("plans.js", {"args": self.plans_args(self.plan_item()), "replies": {
            "write-031": {"status": "WRITTEN", "path": "p"}, "review-031-r1": blocking,
            "revise-031-r1": {"summary": "s", "not_fixed": ["y: needs the maintainer"]}}})
        self.assert_contract(out)
        result = out["result"][0]
        self.assertEqual(result["status"], "REVISED")
        self.assertEqual(result["residual"], ["y: needs the maintainer"])

    def test_plans_reports_a_dead_reviewer(self):
        out = self.run_script("plans.js", {"args": self.plans_args(self.plan_item()), "replies": {
            "write-031": {"status": "WRITTEN", "path": "p"}, "review-031-r1": None}})
        self.assert_contract(out)
        self.assertEqual(out["result"][0]["status"], "FAILED")
        self.assertNotIn("revise-031-r1", self.labels(out))

    # execute.js --------------------------------------------------------------------------------

    def exec_item(self, num, **extra):
        item = {"num": num, "slug": "s" + num, "wt": "/w/wt-" + num, "branch": "improve/" + num,
                "base": "BASE-REF", "contract": "plan"}
        item.update(extra)
        return item

    def done(self, summary):
        return {"status": "DONE", "base_before": "B", "commit": "C-" + summary, "summary": summary,
                "files_changed": [], "red_evidence": "", "tests": "T", "stop_reason": "", "deviations": "", "owed": ""}

    def test_execute_stops_stages_at_the_first_that_is_not_done(self):
        stages = [{"key": "one", "prompt": "STAGE ONE"}, {"key": "two", "prompt": "STAGE TWO"},
                  {"key": "three", "prompt": "STAGE THREE"}]
        blocked = dict(self.done("second"), status="BLOCKED", stop_reason="a STOP fired")
        out = self.run_script("execute.js", {
            "args": run_args(items=[self.exec_item("040", contract="stages", stages=stages)]),
            "replies": {"stage-040-one": self.done("first stage summary"), "stage-040-two": blocked}})
        self.assert_contract(out)
        self.assertEqual(self.labels(out), ["stage-040-one", "stage-040-two"])
        self.assertIn("first stage summary", out["calls"][1]["prompt"])
        result = out["result"][0]
        self.assertEqual((result["status"], result["stoppedAt"], result["notRun"]), ("BLOCKED", "two", ["three"]))

    def test_execute_reports_a_dead_executor_and_bounds_the_pool(self):
        items = [self.exec_item(n) for n in ("041", "042", "043", "044")]
        items.append(self.exec_item("045", contract="decisions", decisions=["DECISION ONE"]))
        out = self.run_script("execute.js", {"args": run_args(items=items), "replies": {"exec-042": None},
                                             "prefixes": [["exec-", self.done("ok")], ["decide-", self.done("ok")]]})
        self.assert_contract(out)
        status = {r["num"]: r["status"] for r in out["result"]}
        self.assertEqual(status, {"041": "DONE", "042": "FAILED", "043": "DONE", "044": "DONE", "045": "DONE"})
        self.assertIn("DECISION ONE", [c for c in out["calls"] if c["label"] == "decide-045"][0]["prompt"])

    # review.js ---------------------------------------------------------------------------------

    def review_item(self, **extra):
        item = {"num": "050", "planSlug": "thing", "branch": "improve/050", "wt": "/w/wt-050",
                "base": "BASE-REF", "head": "HEAD-REF", "title": "T", "lenses": [1, "4", "tooling"],
                "files": {"cs": ["Main/A.cs"]}}
        item.update(extra)
        return item

    LEAD = {"verdict": "READY FOR COMMIT", "confirmed": 1, "false_positives": 0, "needs_mike": ["N1"],
            "applied": 0, "not_applied": 0, "commit": "LEAD-COMMIT", "suite_totals": "T", "codex": "not run"}
    DEFECT = {"findings": [{"severity": "LOW", "location": "a:1", "claim": "C", "proof": "P", "fix": "F"}],
              "evidence_summary": "E", "verdict": "DEFECTS"}
    CLEAN = {"findings": [], "evidence_summary": "E", "verdict": "CLEAN"}

    def test_review_runs_waves_then_a_bounded_convergence_loop_and_keeps_residuals(self):
        second = dict(self.DEFECT, findings=[dict(self.DEFECT["findings"][0], claim="STILL THERE")])
        out = self.run_script("review.js", {"args": run_args(items=[self.review_item()]), "replies": {
            "lead-050": self.LEAD, "converge-050-r1": self.DEFECT,
            "fix-050-r1": {"commit": "FIX-COMMIT", "fixed": ["C"], "false_positives": [],
                           "not_fixed": ["NF: needs the maintainer"], "suite_totals": "T"},
            "converge-050-r2": second}, "prefixes": [["lens-", "LENS REPORT"]]})
        self.assert_contract(out)
        labels = self.labels(out)
        self.assertEqual(labels[0], "lens-050-1")
        self.assertEqual(sorted(labels[1:3]), ["lens-050-4", "lens-050-tooling"])
        self.assertEqual(labels[3:], ["lead-050", "converge-050-r1", "fix-050-r1", "converge-050-r2"])
        self.assertIn("HEAD-REF..HEAD", out["calls"][4]["prompt"])
        self.assertIn("LEAD-COMMIT..HEAD", out["calls"][6]["prompt"])
        result = out["result"][0]
        self.assertEqual(result["status"], "RESIDUAL")
        self.assertEqual(result["needs_mike"], ["N1"])
        text = json.dumps(result["residual"])
        self.assertIn("STILL THERE", text)
        self.assertIn("NF: needs the maintainer", text)

    def test_review_stops_when_convergence_is_clean(self):
        out = self.run_script("review.js", {"args": run_args(items=[self.review_item()]), "replies": {
            "lead-050": self.LEAD, "converge-050-r1": self.CLEAN}, "prefixes": [["lens-", "LENS REPORT"]]})
        self.assert_contract(out)
        self.assertNotIn("fix-050-r1", self.labels(out))
        self.assertEqual((out["result"][0]["status"], out["result"][0]["residual"]), ("CLEAN", []))

    def test_review_stops_an_item_whose_lens_died(self):
        out = self.run_script("review.js", {"args": run_args(items=[self.review_item()]), "replies": {
            "lens-050-1": None}, "prefixes": [["lens-", "LENS REPORT"]]})
        self.assert_contract(out)
        self.assertEqual(self.labels(out), ["lens-050-1"])
        self.assertEqual(out["result"][0]["status"], "FAILED")
        self.assertIn("lens-050-1", out["result"][0]["failures"][0])

    def test_review_refuses_an_unknown_lens(self):
        out = self.run_script("review.js", {"args": run_args(items=[self.review_item(lenses=["4", "t"])])})
        self.assertFalse(out["ok"])
        self.assertIn('unknown lens "t"', out["error"])
        self.assertEqual(out["calls"], [])

    # draft-issues.js ---------------------------------------------------------------------------

    def test_draft_issues_reports_a_dead_drafter(self):
        items = [{"num": "060", "title": "A", "template": "bug", "label": "bug"},
                 {"num": "061", "title": "B", "template": "feature", "label": "enhancement"}]
        out = self.run_script("draft-issues.js", {"args": run_args(items=items), "replies": {
            "draft-060": {"title": "A", "label": "bug", "body_file": "/w/scratch/issues/060.md", "notes": ""},
            "draft-061": None}})
        self.assert_contract(out)
        self.assertEqual([d["ok"] for d in out["result"]["drafts"]], [True, False])
        self.assertEqual(len(out["result"]["failures"]), 1)


if __name__ == "__main__":
    unittest.main()
