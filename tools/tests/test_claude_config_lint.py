#!/usr/bin/env python
"""Claude config lint gate (issue #1020): skill and agent frontmatter, and hook script paths.

Checks, each a function taking a repo root so the tests can plant violations in a temp dir:
  * every .claude/skills/*/SKILL.md and .claude/agents/*.md has YAML frontmatter that parses and a
    non-empty `description` of at most 30 words (folded and block descriptions are measured on the
    parsed text, not the first line);
  * agents need a non-empty `name`. A skill's `name` is optional (Claude Code falls back to the
    directory name; tools/test_hooks.sh 3b only fails a name that differs from the directory), so
    a skill `name` is checked only when present;
  * a skill uses only the consumed frontmatter keys (.claude/rules/harness-skills-agents.md), so a typo
    such as `triggers:` cannot silently turn a setting off;
  * every hook command in .claude/settings.json and in skill frontmatter that names a script under
    CLAUDE_PROJECT_DIR points at a file that exists.

PyYAML is not stdlib. Without it the tests FAIL naming `python -m pip install pyyaml` (tools/lint_docs.py
reports the same gap as a finding); TAOM_ALLOW_NO_YAML=1 skips knowingly.

Run:  python -m unittest tools.tests.test_claude_config_lint
"""
import json
import os
import re
import tempfile
import unittest
from pathlib import Path

try:
    import yaml
except ImportError:  # setUp fails or skips by env var, never a silent pass
    yaml = None

REPO = Path(__file__).resolve().parents[2]
MAX_DESCRIPTION_WORDS = 30
ALLOWLIST_ROW = "Consumed frontmatter (the skill allowlist):"
MIN_SKILL_KEYS = 10
FRONTMATTER = re.compile(r"\A---[ \t]*\r?\n(.*?)\r?\n---[ \t]*(?:\r?\n|\Z)", re.DOTALL)
# A path under CLAUDE_PROJECT_DIR: either wholly inside quotes (spaces allowed), or bare/with the
# variable quoted, ending at whitespace, a quote or a shell separator (; & | ( )).
PROJECT_DIR_PATH = re.compile(
    r"\"\$\{?CLAUDE_PROJECT_DIR\}?(/[^\"]+)\""
    r"|'\$\{?CLAUDE_PROJECT_DIR\}?(/[^']+)'"
    r"|\$\{?CLAUDE_PROJECT_DIR\}?[\"']?(/[^\s\"';&|()]+)")

NO_YAML = ("PyYAML is not installed for this interpreter, so the Claude config lint did not run. "
           "Install it with: python -m pip install pyyaml (set TAOM_ALLOW_NO_YAML=1 to skip knowingly).")


def skill_keys(root):
    """The skill frontmatter allowlist, read from the harness-skills-agents.md row so there is one source."""
    path = Path(root, ".claude", "rules", "harness-skills-agents.md")
    if not path.is_file():
        raise AssertionError("harness-skills-agents.md is missing: %s" % path)
    for line in path.read_text(encoding="utf-8").splitlines():
        if ALLOWLIST_ROW in line:
            cell = line.split("|")[1].split(ALLOWLIST_ROW, 1)[1]
            cell = re.sub(r"\([^)]*\)", "", cell)  # `context` (`fork`): fork is a value, not a key
            keys = set(re.findall(r"`([^`]+)`", cell))
            if len(keys) < MIN_SKILL_KEYS:
                raise AssertionError("allowlist row parsed to %d keys (need %d): %s"
                                     % (len(keys), MIN_SKILL_KEYS, sorted(keys)))
            return keys
    raise AssertionError("harness-skills-agents.md has no %r row" % ALLOWLIST_ROW)


def parse_frontmatter(path):
    """Return (dict, None) or (None, problem)."""
    text = Path(path).read_text(encoding="utf-8")
    m = FRONTMATTER.match(text)
    if not m:
        return None, "no leading --- frontmatter block"
    try:
        data = yaml.safe_load(m.group(1))
    except yaml.YAMLError as exc:
        return None, "frontmatter is not valid YAML: " + str(exc).replace("\n", " ")
    if not isinstance(data, dict):
        return None, "frontmatter is not a mapping"
    return data, None


def _description_problems(data):
    desc = data.get("description")
    if not isinstance(desc, str) or not desc.strip():
        return ["missing or empty description"]
    words = len(desc.split())
    if words > MAX_DESCRIPTION_WORDS:
        return ["description is %d words, limit %d" % (words, MAX_DESCRIPTION_WORDS)]
    return []


def _hook_commands(node):
    """Every `command` string nested anywhere under a hooks mapping."""
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "command" and isinstance(value, str):
                yield value
            else:
                yield from _hook_commands(value)
    elif isinstance(node, list):
        for item in node:
            yield from _hook_commands(item)


def missing_hook_scripts(commands, root):
    """Commands naming a CLAUDE_PROJECT_DIR path whose file does not exist."""
    out = []
    for cmd in commands:
        for match in PROJECT_DIR_PATH.finditer(cmd):
            rel = next(g for g in match.groups() if g)
            if not (Path(root) / rel.lstrip("/")).is_file():
                out.append("%s (from: %s)" % (rel, cmd))
    return out


def lint_skills(root, allowed=None):
    problems = []
    allowed = allowed or skill_keys(REPO)
    for path in sorted(Path(root, ".claude", "skills").glob("*/SKILL.md")):
        label = path.parent.name
        data, bad = parse_frontmatter(path)
        if bad:
            problems.append("skill %s: %s" % (label, bad))
            continue
        problems += ["skill %s: %s" % (label, p) for p in _description_problems(data)]
        name = data.get("name")
        if "name" in data and (not isinstance(name, str) or name != label):
            problems.append("skill %s: name %r differs from its directory" % (label, name))
        for key in sorted(set(data) - allowed):
            problems.append("skill %s: frontmatter key %r is not consumed by Claude Code" % (label, key))
        for rel in missing_hook_scripts(_hook_commands(data.get("hooks")), root):
            problems.append("skill %s: hook script missing: %s" % (label, rel))
    return problems


def lint_agents(root):
    problems = []
    for path in sorted(Path(root, ".claude", "agents").glob("*.md")):
        label = path.stem
        data, bad = parse_frontmatter(path)
        if bad:
            problems.append("agent %s: %s" % (label, bad))
            continue
        if not isinstance(data.get("name"), str) or not data["name"].strip():
            problems.append("agent %s: missing or empty name" % label)
        problems += ["agent %s: %s" % (label, p) for p in _description_problems(data)]
    return problems


def lint_settings_hooks(root):
    path = Path(root, ".claude", "settings.json")
    if not path.is_file():
        return ["settings.json is missing"]
    settings = json.loads(path.read_text(encoding="utf-8"))
    return ["settings.json hook script missing: " + p
            for p in missing_hook_scripts(_hook_commands(settings.get("hooks")), root)]


class ClaudeConfigLintTests(unittest.TestCase):
    def setUp(self):
        if yaml is None:
            if os.environ.get("TAOM_ALLOW_NO_YAML") == "1":
                self.skipTest(NO_YAML)
            self.fail(NO_YAML)

    # Real tree.
    def test_real_skills(self):
        self.assertEqual(lint_skills(REPO), [])

    def test_real_agents(self):
        self.assertEqual(lint_agents(REPO), [])

    def test_real_settings_hooks_exist(self):
        self.assertEqual(lint_settings_hooks(REPO), [])
        settings = json.loads(Path(REPO, ".claude", "settings.json").read_text(encoding="utf-8"))
        found = [m for c in _hook_commands(settings["hooks"]) for m in PROJECT_DIR_PATH.finditer(c)]
        self.assertGreaterEqual(len(found), 20, "hook path extraction found too few scripts")

    def test_real_allowlist_row(self):
        keys = skill_keys(REPO)
        self.assertTrue({"name", "description", "hooks", "paths"} <= keys, sorted(keys))
        self.assertNotIn("fork", keys)

    def test_planted_allowlist_row(self):
        root = self._root()
        rules = root / ".claude" / "rules"
        rules.mkdir(parents=True)
        names = ["k%d" % i for i in range(10)]
        row = "| Consumed frontmatter (the skill allowlist): " + ", ".join(
            "`%s`" % n for n in names) + ", `context` (`fork`) | src | why |\n"
        (rules / "harness-skills-agents.md").write_text("# x\n" + row, encoding="utf-8")
        self.assertEqual(skill_keys(root), set(names) | {"context"})

    def test_allowlist_row_missing_or_short_fails(self):
        root = self._root()
        with self.assertRaises(AssertionError):
            skill_keys(root)
        rules = root / ".claude" / "rules"
        rules.mkdir(parents=True)
        (rules / "harness-skills-agents.md").write_text("| other row |\n", encoding="utf-8")
        with self.assertRaises(AssertionError):
            skill_keys(root)
        (rules / "harness-skills-agents.md").write_text(
            "| Consumed frontmatter (the skill allowlist): `a`, `b` | s |\n", encoding="utf-8")
        with self.assertRaises(AssertionError):
            skill_keys(root)

    def test_path_extraction_stops_at_separators(self):
        for tail in ["; exit 0", " && true", " | cat", ")", '"', "' x", " arg"]:
            cmd = "bash ${CLAUDE_PROJECT_DIR}/.claude/hooks/x.sh" + tail
            self.assertEqual([next(g for g in m.groups() if g) for m in PROJECT_DIR_PATH.finditer(cmd)],
                             ["/.claude/hooks/x.sh"], cmd)

    def test_quoted_path_with_space(self):
        cmd = 'bash "$CLAUDE_PROJECT_DIR/my dir/x.sh" arg'
        m = list(PROJECT_DIR_PATH.finditer(cmd))
        self.assertEqual([next(g for g in x.groups() if g) for x in m], ["/my dir/x.sh"])

    def test_real_tree_is_not_vacuous(self):
        self.assertGreaterEqual(len(list(REPO.glob(".claude/skills/*/SKILL.md"))), 1)
        self.assertGreaterEqual(len(list(REPO.glob(".claude/agents/*.md"))), 1)

    # Planted violations in a temp root.
    def _root(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        return Path(tmp.name)

    def _skill(self, root, name, frontmatter):
        d = root / ".claude" / "skills" / name
        d.mkdir(parents=True, exist_ok=True)
        (d / "SKILL.md").write_text("---\n" + frontmatter + "\n---\nbody\n", encoding="utf-8")

    def _agent(self, root, name, frontmatter):
        d = root / ".claude" / "agents"
        d.mkdir(parents=True, exist_ok=True)
        (d / (name + ".md")).write_text("---\n" + frontmatter + "\n---\nbody\n", encoding="utf-8")

    def test_good_skill_and_agent_pass(self):
        root = self._root()
        self._skill(root, "ok", "name: ok\ndescription: Short and fine.")
        self._agent(root, "ok", "name: ok\ndescription: Short and fine.")
        self.assertEqual(lint_skills(root), [])
        self.assertEqual(lint_agents(root), [])

    def test_skill_without_name_passes(self):
        root = self._root()
        self._skill(root, "noname", "description: Directory name is used.")
        self.assertEqual(lint_skills(root), [])

    def test_long_folded_description_is_measured(self):
        root = self._root()
        words = " ".join(["word"] * 31)
        self._skill(root, "long", "name: long\ndescription: >\n  " + words)
        self.assertTrue(any("31 words" in p for p in lint_skills(root)))

    def test_long_block_description_is_measured(self):
        root = self._root()
        lines = "\n".join("  line%d a b c d e f" % i for i in range(5))
        self._agent(root, "long", "name: long\ndescription: |\n" + lines)
        self.assertTrue(any("description is" in p for p in lint_agents(root)))

    def test_missing_description(self):
        root = self._root()
        self._skill(root, "nodesc", "name: nodesc")
        self._agent(root, "nodesc", "name: nodesc")
        self.assertTrue(any("description" in p for p in lint_skills(root)))
        self.assertTrue(any("description" in p for p in lint_agents(root)))

    def test_agent_without_name(self):
        root = self._root()
        self._agent(root, "anon", "description: Fine.")
        self.assertTrue(any("name" in p for p in lint_agents(root)))

    def test_unparseable_yaml(self):
        root = self._root()
        self._skill(root, "bad", "name: bad\ndescription: [unclosed")
        self.assertTrue(any("not valid YAML" in p for p in lint_skills(root)))

    def test_triggers_key_fails(self):
        root = self._root()
        self._skill(root, "trig", "name: trig\ndescription: Fine.\ntriggers:\n  - go")
        self.assertTrue(any("'triggers'" in p for p in lint_skills(root)))

    def test_name_must_match_directory(self):
        root = self._root()
        self._skill(root, "dirname", "name: other\ndescription: Fine.")
        self.assertTrue(any("differs from its directory" in p for p in lint_skills(root)))

    def test_missing_frontmatter(self):
        root = self._root()
        d = root / ".claude" / "skills" / "plain"
        d.mkdir(parents=True)
        (d / "SKILL.md").write_text("# no frontmatter\n", encoding="utf-8")
        self.assertTrue(any("no leading" in p for p in lint_skills(root)))

    def test_crlf_frontmatter_parses(self):
        root = self._root()
        d = root / ".claude" / "skills" / "crlf"
        d.mkdir(parents=True)
        (d / "SKILL.md").write_bytes(b"---\r\nname: crlf\r\ndescription: Fine.\r\n---\r\nbody\r\n")
        self.assertEqual(lint_skills(root), [])

    def test_settings_hook_script_missing(self):
        root = self._root()
        hooks = root / ".claude" / "hooks"
        hooks.mkdir(parents=True)
        (hooks / "present.sh").write_text("#!/bin/sh\n", encoding="utf-8")
        settings = {"hooks": {"Stop": [{"hooks": [
            {"type": "command", "command": '"$CLAUDE_PROJECT_DIR"/.claude/hooks/present.sh'},
            {"type": "command", "command": '"$CLAUDE_PROJECT_DIR"/.claude/hooks/gone.sh'}]}]}}
        (root / ".claude" / "settings.json").write_text(json.dumps(settings), encoding="utf-8")
        problems = lint_settings_hooks(root)
        self.assertEqual(len(problems), 1)
        self.assertIn("gone.sh", problems[0])

    def test_skill_hook_script_missing(self):
        root = self._root()
        self._skill(root, "hooky", (
            "name: hooky\ndescription: Fine.\nhooks:\n  PreToolUse:\n    - matcher: Edit\n"
            "      hooks:\n        - type: command\n"
            '          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/hooky/check.sh"'))
        self.assertTrue(any("check.sh" in p for p in lint_skills(root)))
        (root / ".claude" / "skills" / "hooky" / "check.sh").write_text("#!/bin/sh\n", encoding="utf-8")
        self.assertEqual(lint_skills(root), [])


if __name__ == "__main__":
    unittest.main()
