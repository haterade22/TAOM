# Reviewing mattpocock/skills again (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: small additions to existing files.** No skill, script or hook came over, and nothing was installed.

## Source, and what was read

`mattpocock/skills` at `4ae2317ddb22b046e26455e4f9c9b3aaaf62212d` (MIT, plugin version 1.3.2). TAOM reviewed this
repository on 2026-05-29 and twice on 2026-06-22. At least 100 commits landed after the June reviews (the query stops at
100), so this review read the current text rather than a diff.

| Depth | Files |
|---|---|
| Read in full | Every runnable file; every `SKILL.md` in the Skip table below except `teach`; `PHASE-BOUNDARIES.md`, `DEEPENING.md`, `DESIGN-IT-TWICE.md`, `SKILL-MECHANICS.md`, `tdd/mocking.md`, `tdd/tests.md`; the README, scope and index files |
| Pattern sweep only | The `.out-of-scope` files, `setup-matt-pocock-skills`, `setup-ts-deep-modules`, the in-progress `writing-*` skills, the `software-factory` surveys, `migrate-to-shoehorn`, `scaffold-exercises`, `setup-pre-commit` |
| Not re-read | `teach`, skipped on 2026-06-22 |

## Security

Safe to learn from: yes. Safe to install: not needed. The plugin updates itself and would add its skill descriptions to
every session.

- `package.json` has no lifecycle scripts, and the plugin manifest lists skills only (no hooks, no MCP servers).
- An in-memory sweep of all 183 files found no binaries and no hidden Unicode characters. Its dangerous-pattern hits were
  documentation words such as "webhook". `package-lock.json` and `CHANGELOG.md` were outside the Unicode and pattern checks.
- `python tools/audit_claude_config.py --root <extracted tarball> --external` scanned 183 files, matching the tree: HIGH 1,
  INFO 1. The HIGH (`promptleak-reveal`) is a false positive on a shell comment in
  `diagnosing-bugs/scripts/hitl-loop.template.sh:10`, which documents a helper that prints a step to a human. Nothing from
  that file was ported. The INFO says the secret sweep was skipped because the extracted folder is not a git work tree.
- `tar` could not create the symlink `AGENTS.md`, which points to `CLAUDE.md`, so the content was still scanned.

## What was adopted

| Source | TAOM change | Reason |
|---|---|---|
| `code-review`, Spec axis | [lens 4](../../.claude/skills/deep-review/lenses/4-completeness.md) item 10 and a Spec output line | Item 4 only checks that an issue exists. Nothing in `/deep-review` checked that the diff does what the issue asked. |
| `diagnosing-bugs` | [`/investigate`](../../.claude/skills/investigate/SKILL.md): a named check that goes red, three ranked hypotheses with predictions, a unique prefix on temporary lines, "no seam" in the report | Step 4 had no done condition and Phase 1 asked for one hypothesis. Strippable diagnostics have a lesson behind them (`lessons/build-tooling-workflow.md`, "Temporary diagnostics stay strippable"). |
| `writing-for-agents` | [external-skill-ports.md](../../.claude/rules/external-skill-ports.md) "Body structure": completion criteria, the no-op test, a positive target beside a prohibition | The rule has `paths:`, so it adds no always-loaded context. Upstream has no automated eval for these. |
| `PHASE-BOUNDARIES.md` | [session-phase-boundaries.md](../reference/session-phase-boundaries.md), reached from `doc-lookup.md` | Handoff became a resume card, and upstream's 150k figure was not carried over as fact. |
| `DESIGN-IT-TWICE.md` | [agent-teams.md](../ai-includes/agent-teams.md) section, only when Mike asks | It costs an agent per design. |
| `wayfinder` | [`/issue`](../../.claude/skills/issue/SKILL.md): `Not yet specified` and `Out of scope` on a multi-session feature | The skill itself makes each decision a public issue, so only the split came over. |
| ADR test in `domain-modeling` | A comment in [`000-template.md`](../adrs/000-template.md): hard to reverse, surprising without context, the result of a real trade-off | The maintainer asked for this edit by name. The hook that guards ADR files refused it twice, so the edit used that hook's session marker for one Edit, and the marker was then deleted. This review found no needless ADR, so the comment is a prompt and fixes no known fault. |

**This reverses one earlier verdict.** The 2026-05-29 review called `diagnose` duplicative. The current `diagnosing-bugs`
still overlaps `/investigate` for the most part, and only the four points above were missing.

**Calibration.** Upstream's "seconds, not minutes" and "no loop, no hypothesis" were dropped: a Bannerlord run takes
minutes, and a player's crash report often has only logs. The three-hypotheses change comes from the skill text. A search of
`docs/reviews` found no TAOM incident of anchoring on a first hypothesis.

**The phase-boundary page and #1078.** The usage baseline comparison is due about 2026-11-10. The page loads only when
someone opens it, so it adds no always-loaded context to that measurement.

## Review of this change

One read-only `implementer` agent reviewed the diff instead of the `Workflow` the procedure names, because the change is a few short additions. It raised eight findings, and seven were corrected after a check against the files. The largest two: the phase-boundary page sent work bound for another machine or client to a memory card that stays on one machine, and `/investigate` Phases 2 and 4 still assumed one hypothesis after Phase 1 began to list three. The eighth is a lint report, not a failure: `lint_docs.py` names links to the two new files while they are untracked. One note was wrong: the agent could not find `git-guardrails` upstream, but the skill is at `skills/misc/git-guardrails-claude-code`.

## Skipped

| Item | Reason |
|---|---|
| `tdd` | `rules/tests.md` and lens 4 item 9 cover it. Agreeing a seam before each test would add a question per test. |
| Fowler smell list in `code-review` | Lenses 1 and 6 and `/simplify` overlap it. |
| `codebase-design`, `improve-codebase-architecture` | The shallow-module lens came over in June. "One adapter is a hypothetical seam" conflicts with ADR-007. `audit-playbook.md` already uses churn. |
| `grilling`, `grill-me`, `loop-me` | Rounds of many questions conflict with the maintainer's one-item-per-message rule. |
| `to-spec`, `to-tickets`, `implement`, `implement-spec`, `triage`, `setup-matt-pocock-skills` | `/new-feature`, `/improve` plans and `/issue` own this, and these assume `GLOSSARY.md` and public tickets. |
| `domain-modeling`, apart from its ADR test | `id-cheatsheet.md` and the ADRs serve this. |
| `retro`, `chief-of-staff` | `/harness-retro` measures from transcripts, and "all work in subagents" conflicts with the main-session rule. |
| `handoff`, `claude-handoff` | Memory resume cards own this. |
| `wait-what` | The maintainer's output style already uses Simplified Technical English. |
| `git-guardrails` | Became `.claude/hooks/block-dangerous-git.sh` on 2026-05-29. |
| `research`, `prototype`, `pr`, `to-questionnaire`, `wizard`, `software-factory`, `ask-matt`, `teach`, the TypeScript and writing skills | Out of domain, or covered by `taleworlds-researcher` and the commit-body rule. |
