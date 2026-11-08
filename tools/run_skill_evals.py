#!/usr/bin/env python3
"""Run the skill routing and hostile content evals in tools/evals/ through `claude plugin eval`.

An eval run loads nothing from the project, and TAOM is not a plugin. So this script stages a
temporary plugin: every `.claude/skills/*/SKILL.md`, the cases, and the CLAUDE.md "Skills" table
(real sessions route with that table). The staged skills carry no scripts or references.

    python tools/run_skill_evals.py --max-cost-usd 1 --out <dir> [--case 'route-*'] [--runs 1] [--model sonnet]

Every run costs model calls, so it needs the maintainer's word (.ai/policy.md). --max-cost-usd is
required and must be above 0. Reports stay under --out and are never published. The harness checks
the cap before each run starts, so the overrun is bounded to the run in flight.

Exit: the exit code of `claude plugin eval` (0 all passed, 1 a case failed, 2 the cap was hit), or 2
when `claude` is not on PATH. Doc: docs/features/skill-evals.md.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PLUGIN_NAME = "taom-skills"
# Quoted so that an unstaged case.yaml is valid YAML too.
PLACEHOLDER = '"@@CLAUDE_SKILLS_TABLE@@"'


def skills_section(claude_md: str) -> str:
    match = re.search(r"^## Skills\s*$(.*?)(?=^## |\Z)", claude_md, re.MULTILINE | re.DOTALL)
    if not match:
        raise ValueError('CLAUDE.md has no "## Skills" section')
    return "## Skills" + match.group(1).rstrip() + "\n"


def named_skills(case_text: str) -> set[str]:
    """The skills a case's input_match patterns name."""
    return set(re.findall(r"\)\?([a-z][a-z-]+)\"", case_text))


def stage_plugin(repo: Path, dest: Path) -> Path:
    (dest / ".claude-plugin").mkdir(parents=True)
    manifest = {"name": PLUGIN_NAME, "version": "0.0.0", "description": "TAOM skills staged for evals"}
    (dest / ".claude-plugin" / "plugin.json").write_text(json.dumps(manifest), encoding="utf-8")
    for skill in sorted((repo / ".claude" / "skills").glob("*/SKILL.md")):
        target = dest / "skills" / skill.parent.name / "SKILL.md"
        target.parent.mkdir(parents=True)
        shutil.copyfile(skill, target)
    shutil.copytree(repo / "tools" / "evals", dest / "evals")
    table = json.dumps(skills_section((repo / "CLAUDE.md").read_text(encoding="utf-8")))
    for case in (dest / "evals").glob("*/case.yaml"):
        case.write_text(case.read_text(encoding="utf-8").replace(PLACEHOLDER, table), encoding="utf-8")
    return dest


def positive_cost(text: str) -> float:
    value = float(text)
    if not math.isfinite(value) or value <= 0:
        raise argparse.ArgumentTypeError("the cap must be a finite number above 0")
    return value


def positive_runs(text: str) -> int:
    value = int(text)
    if value < 1:
        raise argparse.ArgumentTypeError("runs must be at least 1")
    return value


def build_command(plugin: Path, out: Path, max_cost: float, runs: int, model: str, case: str | None,
                  claude: str = "claude") -> list[str]:
    command = [claude, "plugin", "eval", str(plugin), "--no-publish", "--trust-plugin", "--scaffold",
               "--ablation", "none", "--max-cost-usd", str(max_cost), "--runs", str(runs), "--model", model,
               "--output-dir", str(out), "--report", str(out / "report.html")]
    if case is not None:
        command += ["--case", case]
    return command


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--max-cost-usd", type=positive_cost, required=True, dest="max_cost")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--case", help="case name glob")
    parser.add_argument("--runs", type=positive_runs, default=1)
    parser.add_argument("--model", default="sonnet")
    args = parser.parse_args(argv)

    claude = shutil.which("claude")
    if claude is None:
        print("run_skill_evals: claude is not on PATH", file=sys.stderr)
        return 2
    args.out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="taom-skill-evals-") as tmp:
        plugin = stage_plugin(REPO_ROOT, Path(tmp) / "plugin")
        command = build_command(plugin, args.out.resolve(), args.max_cost, args.runs, args.model, args.case, claude)
        return subprocess.run(command, cwd=tmp, check=False).returncode


if __name__ == "__main__":
    sys.exit(main())
