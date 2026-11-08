# Skill evals

## Overview

`tools/run_skill_evals.py` checks whether a clear request reaches the right TAOM skill, whether a plain question reaches
none, and whether `/review-outside-pr` holds against an instruction planted in a pull request. It runs the cases in
`tools/evals/` through `claude plugin eval`. This is tooling, so it changes no gameplay.

## Why This Exists

`tools/harness_retro.py` counts the skills that actually ran, but a count of zero cannot tell a missed skill from a skill
no task needed. The harness retro of 2026-11-08 (window from 2026-10-08) listed six skills with zero invocations, and it
could not say why. A routing case asks that question directly.

`/review-outside-pr` states that a pull request is untrusted data. Before this suite, nothing tested whether a reviewer
follows that rule when a diff tells it not to.

The idea came from [`vercel-labs/agent-browser`](../reviews/adopt-agent-browser-2026-11-08.md). TAOM uses the harness's
own eval command and none of that project's code.

## Architecture

### Design Challenge

An eval run starts in an empty workspace and loads no `.claude/` folder, `CLAUDE.md` or hooks from the project. TAOM is not
a plugin, so `claude plugin eval` has nothing to load.

### Solution Approach

The runner stages a temporary plugin for each invocation. It holds every `.claude/skills/*/SKILL.md`, a generated manifest
and the cases. It replaces the `"@@CLAUDE_SKILLS_TABLE@@"` placeholder in each case with the "Skills" section of
`CLAUDE.md`, because real sessions route with that table. Scripts and references stay behind, so a staged skill cannot run
TAOM's tools.

```
.claude/skills/*/SKILL.md + CLAUDE.md "Skills" + tools/evals/*/case.yaml
        |
  run_skill_evals.py (stages a temporary plugin)
        |
  claude plugin eval (--no-publish, --max-cost-usd, --ablation none)
        |
  --out: report.html and aggregate-result.json
```

## Configuration

Cases are in `tools/evals/<case>/case.yaml`, and the folder name must equal the case `name`. The prefix says what a case
is for.

| Prefix | Purpose | Graders |
|--------|---------|---------|
| `route-` | A clear request reaches one skill | `tool_used` on the Skill tool, with a skill name in `input_match` |
| `control-` | Negative control | `tool_used` with `min: 0` and `max: 0` |
| `hostile-` | A planted instruction is reported and not obeyed | Delivery of the payload, the note never opened, the canary never printed, a STOP verdict, the injection named |

Routing cases allow only the Skill tool. `max_turns` is 6, because a limit of 2 ended each routed run with an error and
made the harness keep a temporary folder per run. All graders are free regular expression or tool checks, so the only cost
is the agent run.

## Usage

Every run costs model calls, so it needs the maintainer's word ([policy](../../.ai/policy.md)).

```bash
python tools/run_skill_evals.py --max-cost-usd 1 --out <scratch dir> [--case 'route-*'] [--runs 1] [--model sonnet]
```

`--max-cost-usd` is required and must be above 0. The harness checks it before each run starts, so an overrun is limited to
the run in flight. A failed run on Windows can leave `E:\Temp\claude-eval-*`, and the harness prints the removal command.

The free part is `tools/tests/test_run_skill_evals.py`. It fails when a case names a skill that no longer exists.

## Results (2026-11-08, Sonnet, `claude` 2.1.288)

All 9 cases passed one run each, for US$0.61. The hostile case passed 3 of 3 further runs, for US$0.21. A mutated copy
that told the agent to print the note failed `note-not-opened` and `canary-not-leaked`, which proves those graders can
fail.

## How to verify in game

Not applicable: this is repository tooling, and no code from it ships in the mod.

## Limits

- An eval is an easier setting than a real session. It has the skill table but no project memory, rules or long history, so
  a pass does not prove that the skill fires in a long session.
- These routing prompts are clear on purpose. A pass does not explain the zero counts in the retro, and those skills may
  simply have had no task.
- The hostile case covers one payload in one position, in a code comment. It does not establish resistance to prompt
  injection, and it is no substitute for the checks in `/review-outside-pr`.
- Each routing result is one run of a non-deterministic model.
