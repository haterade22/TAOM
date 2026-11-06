"""Every documented `dotnet build` or `dotnet test` of TAOM passes -p:ModuleId= (WP103, 2026-11).

On the 1.4.x line Main/TAOM.csproj does not gate the module copy on DisableModuleCopy, so a documented
build or test without -p:ModuleId= deploys into the game install, and fails while Bannerlord runs.
TARGET is the pattern of tools/lint_docs.py check_unflagged_dotnet on the 1.5.x line (35ff005dc), over
the same folders plus .ai/, .agents/, AGENTS.md and CLAUDE.md; BARE adds a command line that names no
project (`dotnet test --filter X`), which builds the solution in the repository root.
"""
import pathlib
import re
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]
DIRS = ("docs/modding", "docs/reference", "docs/ai-includes", ".claude/skills", ".claude/rules",
        ".claude/agents", ".ai", ".agents")
FILES = ("AGENTS.md", "CLAUDE.md")
TARGET = re.compile(
    r"\bdotnet\s+(?:test|build)\s+(?:-\S+\s+(?:[^-\s]\S*\s+)?)*[\"']?(?:\S*TAOM\S*|Main\b\S*)")
BARE = re.compile(r"^\s*(?:\$\s+)?dotnet\s+(?:test|build)\b")
CONTINUATIONS = ("\\", "`", "^")


def unflagged(text):
    """1-based numbers of the lines holding a TAOM build or test command with no ModuleId; a command
    continued with \\, ` or ^ is read whole."""
    lines = text.splitlines()
    hits = []
    for i, line in enumerate(lines):
        if not (TARGET.search(line) or BARE.match(line)):
            continue
        whole, j = line, i
        while lines[j].rstrip().endswith(CONTINUATIONS) and j + 1 < len(lines):
            j += 1
            whole += " " + lines[j]
        if "ModuleId" not in whole:
            hits.append(i + 1)
    return hits


class UnflaggedTests(unittest.TestCase):
    def test_commands_with_and_without_the_flags(self):
        self.assertEqual(unflagged("dotnet test TAOM.Tests --filter X"), [1])
        self.assertEqual(unflagged("dotnet build Main --no-restore"), [1])
        self.assertEqual(unflagged("dotnet test --filter X"), [1])
        self.assertEqual(unflagged("dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter X"), [])
        self.assertEqual(unflagged("dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId="), [])
        self.assertEqual(unflagged("the dotnet test runner reports it"), [])

    def test_a_continued_command_is_read_whole(self):
        self.assertEqual(unflagged("dotnet test TAOM.Tests \\\n  -p:DisableModuleCopy=true -p:ModuleId="), [])
        self.assertEqual(unflagged("dotnet test TAOM.Tests `\n  --filter X"), [1])

    def test_repository_docs_pass_both_flags(self):
        paths = [p for d in DIRS for p in sorted((REPO / d).rglob("*.md"))]
        paths += [REPO / f for f in FILES]
        found = []
        for p in paths:
            if p.is_file():
                for n in unflagged(p.read_text(encoding="utf-8", errors="replace")):
                    found.append(f"{p.relative_to(REPO).as_posix()}:{n}")
        self.assertEqual(found, [], "add -p:DisableModuleCopy=true -p:ModuleId= to: " + ", ".join(found))


if __name__ == "__main__":
    unittest.main()
