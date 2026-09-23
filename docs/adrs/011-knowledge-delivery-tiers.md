# ADR-011: Knowledge Delivery Tiers

**Status**: Accepted

**Date**: 2026-09-23

**Priority**: Standard

## Context

Every Claude session, and every custom subagent it spawns, starts by loading the same preamble:
CLAUDE.md, the AGENTS.md it imports, and every `.claude/rules/` file without a `paths:` field. On
2026-09-23 that preamble was about 104 KB (CLAUDE.md 48,954 B, AGENTS.md 3,690 B, seven unscoped
rules 51,076 B), and a main session added MEMORY.md for about 123 KB.

The 2026-08-05 diet had left CLAUDE.md at 28.4 KB under a 46 KB lint cap. It passed the cap on
2026-09-14 and reached 48.9 KB by 2026-09-22, in steps of 300 to 900 bytes that were nearly all
Traps rows, because the "Documentation Requirements" section told every feature to add one. The cap
lived in a single Claude PreToolUse hook (`check-doc-config-drift.sh`), and several of the commits
that grew the file carry no version label, which `check-commit-subject-version.sh` would have
refused: they were made from outside Claude, where no hook runs. The review of this ADR's first
batch found the gate had not held inside Claude either: it printed its decision at the top level
of its JSON, where Claude Code ignores it, and so did eight other TAOM gates.

Three other layers had drifted the same way:

- **MEMORY.md** held 15 dead links and a project tracker whose labels git contradicted for 15 of
  the 20 items marked uncommitted or unpushed.
- **The lessons record** reached 1.34 MB across 13 category files (788 lessons), while the rules
  told Claude to read the whole category file before touching a subsystem.
- **Nine hooks** wrote reminders to channels Claude never sees, and one data gate
  (`check-polearm-shield-parity.sh`) failed silently the same way.

The common fault is an inversion. Knowledge that matters in one area was promoted into the
always-loaded tier because the on-demand channels were not trusted to fire; commits `16018d34` and
`03cd026b` moved traps into CLAUDE.md "where it gets read". The on-demand channels do fire, each on
a specific trigger. The fix is to match each fact to the trigger that reaches it.

### Delivery facts this decision rests on

DOC-BACKED: code.claude.com/docs/en `memory`, `sub-agents`, `context-window` and `hooks`, fetched
2026-09-23. EMPIRICAL: this repository, 2026-09-23, Claude Code 2.1.241 in the VS Code extension.

| Fact | Source |
|---|---|
| A CLAUDE.md should stay under 200 lines; longer files "consume more context and reduce adherence". An `@path` import loads at launch, so it organises but saves nothing. | memory (DOC) |
| Custom and general-purpose subagents load the CLAUDE.md hierarchy, AGENTS.md and unscoped rules. The built-in Explore and Plan agents load none of them. No subagent loads auto memory. | sub-agents, memory (DOC) |
| Auto memory is machine-local: "not shared across machines or cloud environments". | memory (DOC) |
| A `paths:` rule loads when Claude reads a matching file. `paths` is the only frontmatter field Claude Code reads; a rule whose YAML fails to parse loads unconditionally. | memory (DOC) |
| A `paths:` rule does not fire for a file outside the repository, even through a `**/` glob. | EMPIRICAL: reading the live `TAOM_Map/ModuleData/settlements.xml` loaded no rule; reading the repo's shadow copy loaded three |
| After compaction, CLAUDE.md, unscoped rules, auto memory, the git status and each invoked skill's first 5,000 tokens are re-injected. Path rules reload on the next matching read. The skill-description listing is not re-injected. | context-window (DOC) |
| Hook stdout reaches Claude only for SessionStart, UserPromptSubmit, UserPromptExpansion and PostModelSwitch. Stderr from a hook that exits 0 never reaches Claude; a PreToolUse deny reason and exit-2 stderr do, and an ask reason reaches the user only. A PreToolUse decision counts only under `hookSpecificOutput`: nine TAOM gates printed it at the top level and were ignored until #647. | hooks (DOC); EMPIRICAL: `suggest-compact.sh` printed about 18 times in one session and nothing arrived |
| MCP tool schemas are deferred behind tool search; only the names load. | context-window (DOC); EMPIRICAL: this session's deferred tool list |
| Claude Code asks for an AI co-author trailer on commits unless `attribution.commit` is `""`, which hides it. | settings-reference (DOC) |

## Decision

**Every fact has exactly one home, chosen by the narrowest trigger that fires every time the fact
is needed. Anything a machine can check becomes a gate, and the prose shrinks to one line naming
the gate.**

### Tiers

| Tier | Fires | Home | Holds |
|---|---|---|---|
| Constitution | every session, every custom agent, Codex; survives compaction | `AGENTS.md`, a bootstrap under 8 KB | target, invariants, commands, git and commit rules, the documentation duty |
| Orientation | Claude: every session, imported by CLAUDE.md. Other clients: the link in AGENTS.md | `docs/ai-includes/orientation.md` | where things are, the trap index |
| Claude layer | every Claude session and custom agent; survives compaction | `CLAUDE.md` (imports the two above) and the unscoped rules | how context is delivered, where knowledge goes, the skill index, subagents, models, MCP; per-turn reflexes |
| Area | Claude reads an in-repo file | `.claude/rules/*.md` with `paths:` | the rules and traps of that area, including promoted lessons |
| Procedure | a skill is invoked | `.claude/skills/*/SKILL.md`, most important text first | the workflow and its traps; the only channel into out-of-repo work on the live Armory, TAOM_Map or the Modding Kit |
| Reference | a link is followed | `docs/features/`, `docs/reference/`, `docs/ai-includes/` | full detail |
| Archive | grep | `docs/reviews/lessons/`, RCAs, `CHANGELOG.md`, `docs/reference/rule-provenance.md` | evidence and history |
| Machine | main session on this machine | auto memory: `MEMORY.md` and resume cards | uncommitted or unpushed work on this machine |
| Gates | a tool call, a commit, a push | hooks on visible channels, tests, CI | everything a machine can check |

### Where new knowledge goes

1. **A machine can check it:** write the gate (test, validator, hook, CI step); add one line naming
   the gate where the rule is read.
2. **Needed on every task by every AI client:** AGENTS.md. Needed only by Claude's harness:
   CLAUDE.md.
3. **Needed when working on in-repo files of one area:** that area's `paths:` rule.
4. **Needed during a procedure, or for out-of-repo work:** that skill, near the top.
5. **Explains a feature or engine behaviour:** `docs/features/<name>.md` or `docs/reference/`.
6. **A trap:** the full text in its owning doc, plus one line in the orientation.md trap index.
   When the index is full, merge a line or move one into its area rule.
7. **A lesson:** append it to `docs/reviews/lessons/<category>.md`. If it is a standing rule,
   promote one line into the rule or skill from items 3 or 4, linking back to the lesson.
8. **The state of uncommitted or unpushed work on this machine:** a memory resume card, deleted
   when the work is pushed.

Never put these in the first three tiers: counts nothing computes, incident narratives, or copies
of text that lives in another tier. Link instead.

### Ownership rules

- **AGENTS.md, orientation.md and CLAUDE.md.** AGENTS.md holds the rules any builder or reviewer
  needs, written provider-neutrally, and stays a small bootstrap: `tools/reviewctl.py` fails it at
  8,192 bytes, because every Codex session pays for it too. The map and the trap index, also
  provider-neutral, live in `docs/ai-includes/orientation.md`, which AGENTS.md links and CLAUDE.md
  imports. CLAUDE.md holds only what concerns Claude's harness: skills, rules, hooks, subagents,
  memory, models and MCP. Nothing is stated in two of them.
- **Memory.** Auto memory is limited to resume cards for work that is uncommitted or unpushed on
  this machine, plus machine-local paths. A correction or preference that should hold on another
  machine, or for another AI client, goes into the repository.
- **Hooks.** A hook exists only if it gates (deny or ask, exit 2) or prints on a channel Claude
  sees. A hook that blocks or asks carries its own instructions in its message, and a PreToolUse
  gate prints its decision under `hookSpecificOutput` (`tools/test_hooks.sh` 5c fails otherwise).
- **Owed work.** An in-game check or a decision still owed when an issue closes is a label on that
  issue (`triage-needs-ingame`, `triage-blocked-decision`), so the backlog is a query every machine
  and client can run, never a hand-kept list.
- **Lessons.** The category files are the evidence archive. Each still-binding lesson is promoted
  as one line into the area rule or skill that loads when the lesson applies; each category file's
  header names where that is.

### Budgets

`tools/lint_docs.py` enforces these: `--fail-on-drift` in CI on every push and pull request of every
branch (`.github/workflows/doc-budget.yml`), and `--drift-only` in the Claude commit hook. The
constants hold the current numbers; the targets below are the ceilings they ratchet toward.

| Constant | Target |
|---|---|
| `ENTRY_DOCS_MAX_BYTES` (CLAUDE.md with its `@`-imports, read from CLAUDE.md: everything it loads at launch) | at most 24 KB |
| `ENTRY_DOC_MAX_LINES` (each entry doc) | at most 200 lines |
| AGENTS.md alone (`tools/reviewctl.py`, already enforced) | under 8,192 bytes |
| `TRAP_INDEX_MAX_ROWS`, `TRAP_INDEX_MAX_ROW_CHARS` | 45 rows, 180 characters |
| `UNSCOPED_RULES_MAX_BYTES` | at most 16 KB in total |
| `SCOPED_RULE_MAX_BYTES` | at most 12 KB per rule |
| MEMORY.md (reported by `scan.sh`; machine-local, so not in CI) | at most 40 lines and 4 KB |

An import that resolves to no file, a trap index that is no longer imported or has lost its
heading, and a rule whose frontmatter does not parse (Claude Code loads it everywhere) are
findings in their own right (`import-missing`, `trap-index-missing`, `rule-frontmatter-invalid`):
otherwise a rename or a typo would switch a cap off without a word.

## Consequences

### Positive

- The preamble every custom agent pays drops from about 104 KB toward 40 KB, and a main session
  from about 123 KB toward 44 KB.
- One copy of the invariants serves Claude, Codex and any other client, where there had been three
  drifting copies (CLAUDE.md, AGENTS.md, `.ai/review-reference.md`).
- The laptop and Codex see every durable rule, because none of it lives in machine-local memory.
- Area knowledge loads when its area is opened, instead of costing every session.
- A budget overrun fails CI on every branch for every committer, not only for commits Claude
  makes.

### Negative

- An area rule reaches Claude only after a matching read, and it drops at compaction until the next
  read. Editing in-repo files without reading them first skips it. The one-line trap index in
  orientation.md stays visible to Claude either way.
- Codex reads orientation.md only when it follows AGENTS.md's link; the 8 KB bootstrap cap buys
  every Codex session a small preamble at that price.
- Out-of-repo work depends on the right skill being invoked, because no rule can fire there.
- CI gains a check that can fail a push for a documentation reason.

### Neutral

- CLAUDE.md keeps its filename (`AssemblyRedirectListTests` walks up to it to find the repository
  root) and its `@AGENTS.md` import (Claude Code 2.1.241 predates reading AGENTS.md directly).
- The lessons files stay append-only; only their header and the promoted lines change.

## Alternatives Considered

### Alternative 1: Trim CLAUDE.md again

- **Pros**: smallest change.
- **Cons**: the 2026-08-05 trim regrew by 20 KB in six weeks, because the process adds a row per
  feature and the only gate misses commits made outside Claude.
- **Why rejected**: it treats the symptom and leaves the growth mechanism in place.

### Alternative 2: Nested CLAUDE.md files as the on-demand channel

- **Pros**: they load by directory without globs.
- **Cons**: a second mechanism beside `paths:` rules, invisible to the existing rule catalog and
  linters.
- **Why rejected**: one on-demand mechanism is simpler to reason about and to lint.

### Alternative 3: `omitClaudeMd: true` on custom agents

- **Pros**: agents would start without the shared preamble.
- **Cons**: agents need the invariants, and the installed Claude Code 2.1.241 predates the field
  (v2.1.271).
- **Why rejected**: shrinking the preamble helps every agent; removing it breaks them.

### Alternative 4: A digest block at the top of each lessons file

- **Pros**: simple to build, keeps lessons in one place.
- **Cons**: Claude still has to choose to open the file.
- **Why rejected**: a promoted line in the area rule loads without anyone remembering to read it.

### Alternative 5: Keep durable corrections in auto memory

- **Pros**: the harness's default place for feedback.
- **Cons**: machine-local; the laptop and Codex never see it.
- **Why rejected**: a rule for this project belongs to the project.

## Examples

### Good (Follows This ADR)

A feature finds that a Stop order never forms a line. The mechanism and history go into
`docs/features/smart-cavalry-ai.md`. The orientation.md trap index gains one line naming the trigger and
the never-do, linked to that section. The lesson is appended to its lessons file and promoted as a
line in `harmony-patches.md`, which loads whenever a movement-order patch is opened.

Mike says a SubModule.xml must never gain a DependedModule row. A test pins the current set, and
`docs/modding/module-dependencies.md` states the rule in one line that names the test.

### Bad (Violates This ADR)

A 470-character CLAUDE.md row restating a feature doc that already holds the same facts.

A correction saved only as a feedback memory, so the laptop session never sees it and repeats the
mistake.

A Stop hook that prints its reminder to stderr and exits 0, so no one ever reads it.

## Migration Strategy

Applied in stop-anywhere phases, each ending green:

1. Safety net: back up the memory directory; record the baseline.
2. This ADR.
3. Give every fact in CLAUDE.md an owning doc before anything is removed.
4. Split AGENTS.md (constitution) and orientation.md (map and traps) from CLAUDE.md (Claude
   layer); replace copies with links.
5. Compress the unscoped rules; make `harness-facts.md` path-scoped.
6. Gates: the budgets above in `lint_docs.py` and a CI workflow on every branch,
   rule-frontmatter parsing in `test_hooks.sh`, an AI-trailer check in the commit hook with
   `attribution` hidden in `settings.json`, and every PreToolUse gate printing the documented
   decision shape.
7. Memory: move durable guidance into the repository; resume cards only.
8. Hooks: delete the ones Claude cannot see; convert the silent data gate.
9. Path-rule diet; then promote standing lessons into area rules and skills.
10. Assess consolidating the three navigation indexes; re-baseline.

## References

- `docs/context-budget-baseline.md`: the 2026-08-05 baseline this ADR's measurements compare against.
- `tools/lint_docs.py` `check_context_budget`, which replaced `check_claude_md_budget`.
- `docs/reviews/rca-adr011-batch1-2026-09-23.md`: what the review of the first batch found.
- Claude Code documentation: https://code.claude.com/docs/en/memory,
  https://code.claude.com/docs/en/sub-agents, https://code.claude.com/docs/en/context-window,
  https://code.claude.com/docs/en/hooks, https://code.claude.com/docs/en/settings-reference

## Related ADRs

- [ADR-010](./010-knowledge-base-architecture.md): Knowledge-Base Architecture. ADR-010 organises
  the mod-knowledge surface under `docs/`; this ADR decides how that knowledge, and the harness's
  own, is delivered into an agent's context.
