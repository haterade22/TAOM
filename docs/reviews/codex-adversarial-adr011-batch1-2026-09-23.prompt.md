# Codex adversarial review: ADR-011 batch 1 (#647), 2026-09-23

You are reviewing a restructure of how TAOM (a Bannerlord total-conversion mod) delivers instructions to AI agents, and the gates that enforce it. There is no C# or game data in scope. Assume the author (Claude) missed something. Read AGENTS.md first; it is the project's rule set.

## What changed

CLAUDE.md went from 48,954 B to about 7 KB and now holds only Claude-harness matters; it imports AGENTS.md (the provider-neutral rule set, kept under 8,192 B by tools/reviewctl.py) and docs/ai-includes/orientation.md (the map and a 38-row trap index). The always-loaded rules (.claude/rules/*.md without a paths: field) went from 51 KB to about 12 KB; harness-facts.md became path-scoped. docs/adrs/011-knowledge-delivery-tiers.md records the design: every fact has one home, chosen by the narrowest trigger that fires every time it is needed.

A /deep-review (six lenses plus a convergence pass) then found and fixed 22 defects, recorded in docs/reviews/rca-adr011-batch1-2026-09-23.md. The largest:

- Nine PreToolUse gates printed {"permissionDecision": ...} at the top level of their JSON; Claude Code reads a PreToolUse decision only under hookSpecificOutput, so none of them ever blocked or asked. All nine now print {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":...,"permissionDecisionReason":...}}. Proven live on the commit-label gate.
- Hook Python read piped stdin in cp1252, so one non-cp1252 character in a command blinded every Python-parsed gate. Fixed by exporting PYTHONIOENCODING=utf-8 from .claude/hooks/_pybin.sh.
- tools/lint_docs.py gained the context budget (check_context_budget, entry_docs() reading CLAUDE.md's @-imports, --drift-only for the commit hook, --context-budget-json for /context-budget's scan.sh). .github/workflows/doc-budget.yml runs lint_docs.py --fail-on-drift on every branch.
- check-commit-subject-version.sh refuses an AI attribution line in -m, a heredoc, an -F file or --trailer; .claude/settings.json sets attribution.commit and attribution.pr to "" so the harness stops asking for one.

## Scope

The working tree on branch bannerlord-1.5.x, uncommitted. Other sessions have unrelated uncommitted edits in the same tree: review ONLY these files (git diff HEAD -- <file>; new files are untracked, read them whole). In docs/reviews/lessons/build-tooling-workflow.md and tools/README.md, only the hunks about #647 are this change's.

Harness: CLAUDE.md, AGENTS.md, README.md, .ai/review-reference.md, .claude/settings.json, .github/workflows/doc-budget.yml (new), .claude/rules/{environment-failures,evidence-over-claims,harness-facts,output-style,simplicity-criterion,think-before-coding,working-discipline,external-skill-ports,moduledata-validation,vanilla-data-comparison,hook-authoring}.md, .claude/agents/{debugger,error-detective,feature-builder,refactoring-specialist,taleworlds-researcher,deep-reviewer}.md.

Hooks: .claude/hooks/{_pybin,block-broad-git-add,block-dangerous-git,check-changelog-changed,check-claude-files-tracked,check-commit-subject-version,check-doc-config-drift,check-moduledata-validation,check-native-dll-crt,detect-docs-gaps,session-start,validate-push}.sh, .claude/skills/freeze/check-freeze.sh.

Skills: .claude/skills/{context-budget/SKILL.md,context-budget/scan.sh,context-restore/SKILL.md,context-save/SKILL.md,deep-review/lenses/1-standards.md,deep-review/lenses/7-xml.md,engine-bump/SKILL.md,freeze/SKILL.md,improve/SKILL.md,improve/references/audit-playbook.md,improve/references/closing-the-loop.md,lint-docs/SKILL.md,new-feature/SKILL.md,release/SKILL.md,ship/SKILL.md,skill-stocktake/SKILL.md,taom-src/SKILL.md}.

Docs: docs/adrs/{011-knowledge-delivery-tiers (new),010-knowledge-base-architecture,README}.md, docs/ai-includes/{orientation (new),git-and-commits (new),agent-operating-manual,agent-teams,completion-workflow,lord-skills-authoring,weapon-creation-workflow}.md, docs/features/{doc-health-linter,enlistment,lord-spawn-guard,moduledata-validation,multi-culture-armor-revamp,ranged-ladders,smart-cavalry-ai,weapon-xml-pipeline}.md, docs/investigations/native-skin-fixes-load-failure-2026-06-18.md, docs/modding/{README,module-dependencies,modules-overview,settlements}.md, docs/reference/{codex-integration,doc-lookup,harmony-patch-registry,hooks-catalog,mcp-servers,rule-provenance,rules-catalog,taom-map-settlement-naming}.md, docs/reference/engine/{gamemodel-system,gauntletui-viewmodel-screen,mount-and-rider-runtime}.md, docs/reviews/rca-adr011-batch1-2026-09-23.md (new).

Tools and tests: tools/{lint_docs.py,test_hooks.sh,audit_claude_config.py,apply_rohan_spear_reforge.py,apply_starting_fief_spread.py,audit_mbproj_registration.py,taom_schema.py}, tools/oneoff/retag_khand_to_variag.py, tools/tests/{test_lint_docs.py,test_audit_skillspector.py,test_live_ram_bardings.py,test_melee_ladder.py,test_register_one_handed_polearms.py}.

## Read first

docs/adrs/011-knowledge-delivery-tiers.md, docs/reviews/rca-adr011-batch1-2026-09-23.md, .claude/rules/harness-facts.md (the harness facts the design rests on, each with its source), docs/reference/hooks-catalog.md. The Claude Code documentation the facts cite: https://code.claude.com/docs/en/hooks (see "PreToolUse decision control"), https://code.claude.com/docs/en/memory, https://code.claude.com/docs/en/sub-agents, https://code.claude.com/docs/en/context-window, https://code.claude.com/docs/en/settings-reference. If you cannot fetch a page, say so and mark the dependent finding UNVERIFIED.

## Known suspects: CONFIRM or DISPUTE each with evidence

S1. Gate output. Every PreToolUse gate listed above must print either {} or a single valid JSON object whose decision sits under hookSpecificOutput with hookEventName "PreToolUse", on every path (deny, ask, the rc-124 timeout asks, the degraded paths). Drive each gate with hostile commands (quotes, backslashes, Windows paths, newlines, tabs, non-ASCII) and parse the output. Name any path that still emits invalid JSON or the ignored top-level form.

S2. check-commit-subject-version.sh. Find any git commit form that carries an AI attribution line and still passes, or that blocks a legitimate commit: $'...' quoting, -m with an embedded newline, --trailer=, -F - fed by a pipe, --template, commit.template, a message built by a script, several chained commands. The early allows for -C/-c/--fixup/--squash now sit below the attribution check; check nothing else depended on their old position.

S3. PYTHONIOENCODING=utf-8 exported from _pybin.sh reaches every Python a hook starts. Identify any hook or launched tool whose behaviour changes for the worse (a file written, bytes parsed, output consumed by bash in another encoding).

S4. tools/lint_docs.py. Does entry_docs() match Claude Code's documented import rules (relative to the importing file, four hops, code spans and fenced blocks skipped)? Can a legitimate @ in prose be misread as an import, or a real import be missed? Does --drift-only gate exactly as --fail-on-drift does for the three gating checks? Does doc-budget.yml's run (ubuntu-latest, python3, LF checkout, no gitignored docs/reviews/raw) depend on anything that exists only on the author's Windows machine?

S5. Consistency of the rules. Does any statement in CLAUDE.md, AGENTS.md, orientation.md, the six unscoped rules, harness-facts.md or the ADR contradict the code, the Claude Code documentation or its own linked owning doc? Is any rule now stated in two of the always-loaded files, or in none? Check every orientation.md trap row against the doc it links.

S6. Anything a subagent or a Codex session would now fail to learn. The old CLAUDE.md is at git show HEAD:CLAUDE.md. For each of its sections, confirm the content survives somewhere that the intended reader actually loads (ADR-011's tier table says which reader loads what), or that its removal is recorded as deliberate.

## Quality gates

A finding needs a file:line, the evidence you read or ran, a proving command where one exists, the impact, and a proposed fix. Try to refute each finding before reporting it. Say UNVERIFIED where you could not run or fetch something. Do not report the other sessions' files, the pre-existing items listed under "Follow-ups not taken" in the RCA, or the seven path-scoped rules the linter reports as size-warn (a later batch owns them).

## Prior review lessons

SUCCESSES: reading the harness documentation directly caught contract errors that reading TAOM's own docs could not; driving a hook with a real payload and parsing its output caught what text matching missed.
FAILURES: accepting a restated contract without checking its source; judging a gate green because a test matched a substring.

## Boundary

This is a review, not a fix. Do not edit, create, move or delete any file in the repository, and run no git command that changes state (add, commit, stash, checkout, restore, reset, clean). Other sessions are working in this tree. Running a hook with a payload piped to its stdin, running the tests, and running the linter are all fine; if something you run writes a log, say so in the report.

## Output

Return the full report as your FINAL MESSAGE, grouped by severity (HIGH, MED, LOW), then a line per known suspect saying CONFIRMED, DISPUTED or UNVERIFIED. The dispatcher redirects your stdout to docs/reviews/raw/; do not write any file under docs/reviews/raw yourself.
