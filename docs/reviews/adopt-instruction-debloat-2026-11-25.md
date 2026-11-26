# Reviewing eight articles on instruction-file bloat (2026-11-25)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: two lines of text and one test for the always-loaded tier came over. The larger change came from
TAOM's own evidence:** the [harness retro](harness-retro-2026-11-25.md) run for this review showed that path
rules do not load in sibling worktrees. Nothing was installed and no code was run.

## Sources, and what was read

All eight pages were read in full on 2026-11-25. Earlier verdict on the same author: `mattpocock/skills`,
PARTIAL on 2026-05-29, with its "caveman" and "grill-me" skills rejected as against TAOM's style
([adoption-history.md](adoption-history.md)).

| Source | Updated | Subject |
|---|---|---|
| aihero.dev, "A complete guide to AGENTS.md" | 2026-01-18 | A small root file, an instruction budget, progressive disclosure, a five-step refactor |
| aihero.dev, "How to kill the bloat in Claude Code's system prompt" | 2026-07-07 | `/context`, settings that remove tools and bundled skills |
| aihero.dev, "My AGENTS.md file for building plans you actually read" | 2026-01-13 | Two plan-mode rules |
| aihero.dev, "How to make codebases AI agents love" | 2026-02-26 | Deep modules, an interface on top of the detail |
| aihero.dev, "My 7 phases of AI development" | 2026-03-16 | Idea, research, prototype, PRD, tickets, execution, QA |
| techloom.it, "Compress your CLAUDE.md" | 2026-02-24 | Strip markdown, collapse prose to notation |
| techloom.it, "I ran 540 benchmarks to prove CLAUDE.md compression works" | 2026-03-01 | The same author's test of that claim, 1,188 runs |
| the-agent-daily.org, "Make Claude Code give you answers, not essays" | not dated | One line: be concise and sacrifice grammar |

## Security

Safe to learn from: yes; the sources are prose. Safe to install: not needed, and not done. Three runnable
things are linked and none was fetched or run: a logging proxy (`proxy.mjs`, a gist) that would sit between
Claude Code and the API and so would see credentials, a `/setup-matt-pocock-skills` skill, and a
`pip install claude-benchmark` package.

The settings keys the second article names were checked against the Claude Code documentation before any
use. The article's `disableArtifact` is deprecated in favour of `enableArtifact: false`. `skillOverrides`
takes `on`, `name-only`, `user-invocable-only` and `off`. A bare tool name in `permissions.deny` removes the
tool from context; with tool search active the saving for a deferred tool is not documented.

## Novel against duplicative

TAOM already had the tiers the first article argues for
([ADR-011](../adrs/011-knowledge-delivery-tiers.md)): a constitution, path rules, skills, linked docs, and
byte caps in `tools/lint_docs.py`. What the review tested was whether the content still passes the
article's question, "does every single task need this line?".

| Tier | Finding |
|---|---|
| Adopt | The "every task" question, as a three-part test for the always-loaded tier (below) |
| Adopt | Two plan rules, in TAOM's words, in `think-before-coding.md`: a concise plan with its grammar intact, and a closing list of unresolved questions |
| Adopt | Measure with `/context`, then trim tools, bundled skills and MCP servers by settings (Mike picks; pending his `/context all` output) |
| Adopt | A short top with linked detail inside the two skills that pass the 5,000 tokens a compaction keeps |
| Adopt | The benchmark's cautions: keep markdown structure, cut generic rules, keep a rule that guards a failure that repeats |
| Skip | Notation-style compression: the author's own benchmark scored it worse on Haiku and Sonnet, which TAOM's subagents use, for a 5 to 13% token saving |
| Skip | "Sacrifice grammar": Mike's decision of 2026-11-25; the 2026-05-29 verdict stands, and the STE100 output style forbids telegraphic text |
| Skip | `disableBundledSkills` and `disableWorkflows`: all-or-nothing, and TAOM uses bundled skills and workflows |
| Skip | A three-line root file, nested AGENTS.md files, a symlink: Codex and worktree sessions get no path rules, ADR-011 rejected nested files, and the `@AGENTS.md` import exists |
| Skip | The PRD and ticket workflow and "grill me": `/issue`, `/improve` and `/manage` cover it; deep modules for code came over on 2026-06-22 |

A line stays always-loaded only if one of these holds:

1. Every task needs it.
2. A wrong act is irreversible or public, and no gate stops it.
3. No narrower trigger reaches the work (a sibling worktree, Codex, a path outside the repository), and the
   retro shows the failure repeats.

## What the evidence changed

The plan Mike approved cut the always-loaded tier by about a third, on the assumption that C#-only and
spawn-only rules could move into path rules. The retro refuted the assumption: of 200 transcripts that
touched C# only in a sibling worktree, the C# rule loaded in 2. So no rule moved to a path rule. The cuts
made are duplicates and delivery mechanics whose other home was checked first:

| File | Before | After | What left, and its home |
|---|--:|--:|---|
| `CLAUDE.md` | 6,416 B | 5,632 B | Memory channel detail (`harness-loading.md`); five doc-routing rows (`docs/reference/doc-lookup.md`, five rows added there); the pointer to an always-loaded rule; the sentence "`deep-reviewer` runs the `/deep-review` lenses" (that agent's description, listed in every session); in the Hooks line, "exit-2 stderr" and "a Stop hook's block reason" became "a deny or block reason" (`harness-facts.md`, the hooks catalog). Added: one sentence on the worktree hook |
| `AGENTS.md` | 8,018 B | 7,822 B | "Code Review Rules" folded into the Reviews row with every sentence kept; the sentence "This file is the single source for the project's rules" (ADR-011 "Ownership rules"); what the Codex operating guide is for, "local tools, instruction discovery, workflow skills and handoff details" (the guide itself, one link away) |
| `orientation.md` | 5,507 B | 5,495 B | The Localization line said new text is "translated", against `/localize`, which leaves translation to the project lead; it now names the skill. "12 languages, all AI first drafts; `tools/translation_overrides/<lang>.json` wins" left with it (`docs/localization/TRANSLATOR_GUIDE.md`, `/localize`). The trap index intro names the worktree hook |
| `model-effort-selection.md` | 1,953 B | 1,745 B | The purpose of each tier agent (each agent's description, listed in every session) |
| `think-before-coding.md` | 2,002 B | 2,174 B | Added: the two plan rules |

Entry docs 19,941 B to 18,949 B; unscoped rules 15,220 B to 15,184 B (`python tools/lint_docs.py
--context-budget-json`). The four C# rows of AGENTS.md stay as they are: other files cite them by name,
and each already names its gate.

Three retro proposals were approved the same day and are tracked in the retro record: a hook that points a
session in a sibling worktree to the rules its file matches, a 30 s timeout for the trunk edit gate, and an
Armory drift check in `/game-run`.

## Path rules, skills and caps

Each of these ran as a workflow of the same shape: a proposer wrote the change in a scratch folder with a
ledger that names the new home of every removed line, a skeptic tried to prove that some reader loses an
instruction, a repair followed, and one checker read the integrated result. The maintainer was away, so
nothing was applied without that chain and a last read by the session that ran it.

**Path rules: 167,610 B in 25 files to 163,302 B in 26.** The plan's figure, about 125 KB, came from an
auditor's estimate that 43 KB could go. Checked line by line under "no reader loses an instruction", 4,308 B
could.

- Round 1 removed 36 lines that another rule loaded for the same files already states, linked stories that
  a lessons file tells, and deleted seven generic lines: in `harmony-patches.md` "Prefix: runs before
  original method. Return false to skip original.", "Postfix: runs after original method. Can modify
  __result.", "Collection modification during iteration: use .ToList() copy" and "Event timing: verify when
  events fire vs when state changes"; in `adapters.md` "Cache expensive property lookups where appropriate";
  in `external-skill-ports.md` "Generic placeholder labels (step1, helper2)."; in `native-cpp-ports.md` the
  sentence that calls the rule a project-level discipline. Mike has not seen that list yet.
- Round 2 split `tests.md` by reader. The new `test-workflow.md` (1,926 B) loads with every test file and
  holds the workflow, the two-edit test, the gate rule and the coverage rules. `tests.md` (4,409 B) loads
  with C# tests and keeps naming, MSTest and NSubstitute, skip guards, transform tests, test categories
  and layout, which are the sections that nine C# test comments and the feature map cite it for. A Python
  tool test now loads 1,926 B of test rules, not 6,195 B, and a C# test loads 140 B more. The workflow
  had proposed a copy for Python tests; the split keeps one home per line.
  The Python note lost its "only this workflow and the gate rule below apply", which contradicted two
  coverage lines that RCAs of 2026-11-13 added to load with every test file.
- Round 2 also shortened stories in `gui-vm-properties.md`, `config-providers.md`, `adapters.md` and
  `harmony-patches.md` to the rule and a link, and moved one story into a new lessons entry
  ([gamemodels-services.md](lessons/gamemodels-services.md), "Validate a user-editable config's semantics
  at load, not only its syntax").
- Declined on evidence: a narrower glob for the session-reset and the agent-handle sections of
  `csharp-architecture.md` (the classes they are about share no file name pattern), every candidate in
  `xml-data.md` and `vanilla-data-comparison.md`, taking `TAOM.Tests` out of the C# rules, and dropping
  `Dependencies/Foundation/**` from `harmony-patches.md` (three files there install Harmony finalizers, a
  fourth holds the helper they need, and it is the only path rule that loads for `PatchShield.cs`).
- The five engine version pins in `xslt.md`, `adapters.md` and `troops.md` were read again on the
  installed v1.5.7 build, and all hold: `PartyBase.Culture` at `PartyBase.cs:256`, the two
  `ItemRoster.AddToCounts` overloads at lines 185 and 194, the 12-slot loop of
  `Equipment.GetRandomEquipmentElements`, the `caravan_party_templates` loop of
  `CultureObject.Deserialize` (now lines 488 to 494), and the vanilla input files of the stylesheet
  table. The labels say v1.5.7 now.
- Declined after a measurement: moving the Source and Why columns of `harness-facts.md` to the hooks
  catalog. Of its 11,377 B the Source column is 1,732 B and the Why column 2,097 B. The Why cell is the
  instruction, and the Source cell holds the date or the Claude Code version a fact was verified on,
  which is how a reader judges whether the fact is stale. Its nine globs each name a file where a hook
  or a settings fact applies, so the paths stay too.
- Declined after a search: a pass over duplicates between skills. What repeats is a command that a
  step runs (the no-deploy build line in 7 skills, the graphify command in 4, the work claim in 4,
  `codex exec` in 3) or the mechanics of the freeze skills themselves. A pointer in place of a command
  costs a file read at the step.
- One pointer that round 1 left behind: `Main/Adapters/KingdomBallotAdapter.cs` line 10 cites the row
  "Convert at boundary" of `csharp-architecture.md`, which is now part of "No sealed types in services;
  convert at boundary". Not changed here: an edit of C# needs its own review.
- Rejected by the session after three checks had accepted it: a split that stops `gui-ui.md` loading on
  ViewModel files. Its section on layer input is for the class that builds the layer. 16 files in `Main`
  and `Client` create a `GauntletLayer` (`git grep -l "new GauntletLayer" -- Main Client`), and the rule
  loads for none of them, so a touch of the sibling ViewModel or prefab is what delivers it. The lesson is in
  [build-tooling-workflow.md](lessons/build-tooling-workflow.md).

**Skills.** `tools/lint_docs.py` now measures each `SKILL.md` against 20,480 B, about what comes back after
a compaction, and fails a commit that grows a file past that size.

| File | Before | After | What moved |
|---|--:|--:|---|
| `deep-review/SKILL.md` | 26,123 B | 20,296 B | Four files in `deep-review/references/`: the `codex exec` command of Step 0, the Step 2c trigger list, the report template, the RCA and lessons-entry format. Each step keeps its trigger, its gates and the line that says when to read the file |
| `engine-bump/SKILL.md` | 23,311 B | 20,140 B | `engine-bump/references/branches.md`: what to do when the native archive check fails, and the two fix commands of the action-set audit. Dated examples moved to five new sections of `docs/migration/engine-bump-history.md`, or left where an RCA or an impact doc already tells them |
| `deep-reviewer.md`, `feature-builder.md`, `taleworlds-researcher.md` | 13,798 B | 13,010 B | Lines that AGENTS.md or an always-loaded rule states in full. The reviewer keeps its fallback evidence line |

Before the change, a session that compacted during a review lost Step 4 of `/deep-review`, and one that
compacted during a bump lost Phase 5 of `/engine-bump`. The first proposal for `engine-bump` reached the cap
by moving two gates into the reference file; the skeptic had both restored, and a second round found the
bytes in examples. `verify` and `localize` are unchanged: the saving in `verify` was about 250 B, and both
moves proposed for `localize` dropped a rule. The bodies of `review-codex` and `release` were not attempted:
both are under the cap and run seldom, and a lost release step costs more than the bytes. The descriptions
of `review-codex`, `ship` and `issue` now state a trigger and the gate.

**Caps.** `ENTRY_DOCS_MAX_BYTES` 22,528 to 20,000 with its warn at 19,500, and `UNSCOPED_RULES_MAX_BYTES`
16,384 to 16,000: the sizes reached plus about 5%. The cap for one path rule (12,288 B) and the AGENTS.md
cap in `tools/reviewctl.py` (8,192 B) stay, because the largest file plus 5% is over each. The amendment
of 2026-11-25 to [ADR-011](../adrs/011-knowledge-delivery-tiers.md) records the decisions.

**Checks on the closing state** (2026-11-25, in the worktree, before any commit). `python
tools/audit_claude_config.py` exits 0, with one MED on `.claude/hooks/_pushjudge.py`, a file this branch
does not change, and eight INFO. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 45,521
passed, 9 skipped, 1 failed. The failure is
`LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey`: 374 English keys have no row in
each of the 12 language files. The branch changes no file under `Main`, `Client` or `TAOM.Tests`, so that
test reads the same files as at the base commit, and the rows are the project lead's to seed. The
results of `tools/test_hooks.sh` and of the Python tool tests go in the commit body.

## Owed

- **Phase 1, the rest:** a second pass over the always-loaded tier, toward the one-third cut. Two kinds
  of cut are left. Moves into a path rule (the C# rows of AGENTS.md) wait for a live proof of the
  worktree hook, which can come only after the merge. Cuts whose other home is a linked doc (the "Also"
  list of `output-style.md`, the examples of `environment-failures.md`, a shorter "Where new knowledge
  goes") do not wait for the hook, and were not made: each takes text out of automatic delivery. Both
  kinds get one adversarial pass over the list of moved lines, and ten fixed questions asked of a fresh
  session, a Sonnet agent and a Haiku agent, in the main checkout and in a worktree.
- **The `/issue` policy:** CLAUDE.md says never auto-invoke `/issue`, and the standing word of 2026-11-12
  in `think-before-coding.md` says to file the issues the repository requires. The plan listed this as
  fixed in Phase 1; it is not. Mike decides which stands.
- **`native-cpp-ports.md`:** round 1 took out its dead `NativeHooks` path. Whether to keep, rewrite or
  retire the rule is Mike's decision. The path in `/armory-audit` that the plan called stale
  (`E:\repos\lotraom-assets`) exists, so that line stays.
- **Phase 2, the settings pass:** unused tools, bundled skills and MCP servers. It needs Mike's
  `/context all` output and his choice.
- **A larger cut of the path rules:** only by moving sections behind a link, which takes them out of
  automatic delivery. Mike decides which, if any.
- **Layer input rules reach no layer builder:** a row in the campaign and mission trap sheets would fix it.
  Not done here; no issue is filed.
- `MEMORY.md` (7,106 B against 4,096): a per-line proposal for Mike.
- A GitHub issue for this work: not filed, and not waived. Mike decides.
- A Codex pre-review of the hook and tool changes: not authorized, owed before merge.
