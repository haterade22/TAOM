# Prompt audit (2026-10-08)

`/doctor prompt-audit` run under P5 of the [Claude Code update review](adopt-claude-code-2026-10-08.md),
following the bundled `claude-api` skill's `shared/prompt-audit.md` (Claude Code 2.1.292). Four
read-only Sonnet agents each scanned one slice; the main session spot-checked the load-bearing claims
and grouped the findings into five decisions for Mike, taken one at a time.

## Assumptions

- **Target model:** `claude-opus-5-5`, the model these files load into. Agents that pin a model were
  audited against it (`implementer` and the specialists on Sonnet 5.5, `fast-reader` on Haiku 4.5).
- **Scope:** CLAUDE.md with its imports (AGENTS.md, `docs/ai-includes/orientation.md`), the 24 rules,
  the 17 skills not pending removal with the reference files of `deep-review`, `improve` and
  `review-codex`, the 9 agents, and `~/.claude/output-styles/asd-ste-100.md` (user scope, affects every
  project). No nested CLAUDE.md or AGENTS.md exists; no project commands or output styles.
- **Skipped:** settings files, `.mcp.json` and `~/.claude.json` (the guide forbids reading them); the
  14 skills the [skill usage audit](skill-usage-audit-2026-10-08.md) has approved for removal; the
  claude.ai synced skills (owned by the sync, switched off for TAOM); plugin skills (every plugin is off).
- **Moving tree:** another session was removing skills and editing CLAUDE.md during the audit. Line
  numbers are from the working copy at read time; re-read each file before applying its hunk.

## Spot checks by the main session

Ten load-bearing claims re-checked from the repo, all confirmed: no `.github/workflows` exists; the pin
is `v1.5.4` while `taleworlds-researcher.md:19,29,30,49`, `feature-builder.md:37` and
`native-crash-triage/SKILL.md:74` say `v1.5.2`; `taleworlds-researcher.md` names `mcp__ilspy` three
times under a `tools:` allowlist without it; `feature-builder.md:88` and `release/SKILL.md:41` run
`./build.ps1 -RunTests`; `check-freeze.sh` tests only that `freeze-dir.txt` exists, so a boundary
survives a new session while `unfreeze/SKILL.md:29` says ending the conversation clears it;
`translate_with_claude.py:902` takes one required `--lang`; CLAUDE.md has no routing table although
`harmony-patches.md:31-32` says it keeps one; `docs/INDEX.md:47` is now a heading, not the text
`provenance.md:28` quotes; `gamemodels.md`'s snippet names a `GetGameModels` that does not exist.

## Summary

The always-loaded surface is nearly clean: two medium findings, both small. The weight is elsewhere:

1. **Facts the repo contradicts** (high): GitHub CI is described in seven skill files though it was
   removed on 2026-10-05; three files pin engine v1.5.2 against the v1.5.4 pin; subagent and release
   steps still run the deploying `./build.ps1`; four files contradict themselves or a newer file.
2. **Incident narratives inside rules** (medium, the bulk): dated war stories, review numbers and
   one-time counts inside `csharp-architecture`, `gui-ui`, `harmony-patches`, `moduledata-validation`,
   `vanilla-data-comparison`, `troops`, `xml-data`, `hook-authoring` and `external-skill-ports`. The
   rules stand without them, and five of these files are over the 12,288 B rule cap that
   `lint_docs.py` reports.
3. **Dated prompt idioms** (medium, few): "think harder", "think through trade-offs before acting", a
   `CRITICAL` header, and generic method sections in three agent files.

Counts: Group 1 (dated prompt text) 10; Group 2 (configuration files) 64; Group 3 (tool descriptions)
1 flag; Group 4 (request config) not applicable, roster check clean. Low-confidence flags: 25.

## Decisions

| # | Theme | Findings | Confidence | Recommendation | Decision |
|---|---|---|---|---|---|
| D1 | Facts the repo contradicts | 20 | High | Apply | Approved |
| D2 | Incident narratives out of rules, into lessons | about 45 | Medium | Apply, file by file, after the lessons de-dup (ECC Step 11b) or with it | Approved; deferred to a dedicated session with Step 11b and a closing `/deep-review` |
| D3 | Dated prompt idioms and generic method text | 9 | Medium | Apply | Approved |
| D4 | `model-effort-selection.md` lines 14-16 against its own table | 1 | Medium | Flag, no edit: lines 14-16 are the newer passage (`d0e4bc947`, a decision Mike approved in another session the same day), and the guide keeps the newer one | Flag recorded |
| D5 | Low-confidence flags | 25 | Low | Record only | Recorded |

## D1. Facts the repo contradicts (high)

| # | Location | Fix |
|---|---|---|
| 1 | `improve/references/run-protocol.md:251` | `8. **Trunk check**: no CI runs on the trunk (GitHub CI was removed 2026-10-05). The proof is the step 6 verification on the integration tip; record its totals.` |
| 2 | `improve/SKILL.md:31` | `(the CI replay adds its own flags)` to `(the no-game replay in .ai/verification.md adds its own flags)` |
| 3 | `improve/references/dispatch-rules.md:81` | `fails hosted CI` to `fails the no-game replay` |
| 4 | `engine-bump/SKILL.md:189` | delete `, which CI runs too` |
| 5 | `deep-review/lenses/tooling.md:31` | `(CI's tools-tests job installs nothing)` to `(a machine without the dependency must skip, not error)` |
| 6 | `improve/references/audit-playbook.md:36,95,114,186` | `D Tests and CI` to `D Tests` (and "the CI half of 7" to "the test-infrastructure half of 7"); delete the `CI workflows:` line; `Build and CI:` to `Build and test scripts:`; `CI gaps against local gates` to `gaps in the no-game replay against local gates` |
| 7 | `agents/taleworlds-researcher.md:19,29,30,49` | name the version through `.claude/pinned-game-version.txt` instead of `v1.5.2`; line 29: `E:\Decompiled_Bannerlord\` "browse-only; it can lag the installed engine", drop the baselines clause; delete line 30; line 49: cache under `~/.taom-src/<version>/` |
| 8 | `agents/feature-builder.md:37` | drop both `v1.5.2` claims: "decompiles the installed DLL"; "`E:\Decompiled_Bannerlord\` can lag an engine bump; use it only to browse" |
| 9 | `native-crash-triage/SKILL.md:74` | `both are now v1.5.2 (rewritten 2026-09-14; ...)` to `each carries the version its own Version.xml states (compare both with the pin)` |
| 10 | `agents/taleworlds-researcher.md:51-59,96-108` | delete the "Fallback: ILSpy MCP Server" block (its tools are not in `tools:`); fallback chain becomes `taom-src`, then `ilspycmd` via Bash, then `strings | grep`, then stop after three failures and report |
| 11 | `agents/feature-builder.md:88` | `Run dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= to verify (never ./build.ps1: it deploys into the game install).` (CLAUDE.md "Subagents") |
| 12 | `release/SKILL.md:41` | `/verify (build, full test suite, Python tool tests; it never deploys).` (`verify/SKILL.md` runs the non-deploying form; Phase 8's deliberate `build.ps1` stays) |
| 13 | `unfreeze/SKILL.md:29`, `freeze/SKILL.md:29` | unfreeze: `The state file lives in .claude/tmp/freeze/ (gitignored) and persists across sessions until cleared: re-run /unfreeze if a stale boundary is still active when you start work.`; freeze: `for the remainder of the session` to `until /unfreeze` |
| 14 | `localize/SKILL.md:48` | `Three external modules` to `Two external modules` |
| 15 | `localize/SKILL.md:14,31` | `(one language per run: repeat for each of BR CNs CNt DE FR IT JP KO PL RU SP TR; overrides always win)`; line 31 appends `, once per language` |
| 16 | `issue/SKILL.md:25,57,77-83` | replace the `--body "$(cat <<'EOF' ...)"` heredocs: Write the body to a file, run `python tools/check_public_text.py <file>`, then `gh issue create --title ... --label ... --body-file <file>` (CLAUDE.md: heredocs mangle backslashes and quotes) |
| 17 | `harmony-patches.md:29-32` | delete "CLAUDE.md keeps only the thin routing table (category \| feature \| target \| status)." |
| 18 | `gamemodels.md:44-60` | delete the "Registration Pattern" block; rule 7 at line 40 states it correctly |
| 19 | `provenance.md:28-29` | `De-naming does not work in practice: a link target or file name always carries the name anyway.` |
| 20 | `csharp-architecture.md:273-276` against `harmony-patches.md:140` | the two files list different "observed off-main" callbacks; point csharp-architecture at the harmony-patches table ("Which thread runs your target") as the single list |

Also high but tied to the moving tree: `ship/SKILL.md:20` says "6+ parallel senior deep-reviewer
agents" while `deep-review/SKILL.md:83` says "at most four agents in flight": rewrite to "senior
deep-reviewer agents, one per lens, in waves of four".

## D2. Incident narratives out of rules, into lessons (medium)

The rule, its reason and its RCA path stay; the dated story, review number and one-time counts move to
the lessons file. One finding per row; each is "delete the clause, keep the rule".

| File | Lines | Move to `docs/reviews/lessons/` |
|---|---|---|
| `csharp-architecture.md` | 58, 84, 87, 88, 91, 93, 119-120, 156, 180, 204, 210, 220 | `testing-qa.md` (NaN gates), `state-lifecycle-save.md` (camps, #23), `adapters-taleworlds-api.md` (race filter), `gamemodels-services.md` (LotrIssues) |
| `harmony-patches.md` | 16-22, 41-45, 92, 98, 104, 123, 164-165 | `harmony-il.md` |
| `gamemodels.md` | 42 (rule 9 worked example, about 450 to 120 words) | `gamemodels-services.md` |
| `gui-ui.md` | 24-31, 38-39, 63, 77-79, 85, 138-146, 165, 195 | `localization-ui.md` |
| `adapters.md` | 25, 47 | `adapters-taleworlds-api.md` |
| `tests.md` | 56 (duplicate of csharp-architecture 58) | none; delete |
| `native-cpp-ports.md` | 12, 16, 22, 25 | `native-cpp-port.md` |
| `moduledata-validation.md` | 16-22, 45-53, 55-59, 82-115 (table-row counts), 119-148, 188-202 | `xslt-moduledata.md`, `data-content-cultures.md`; counts go to `docs/features/moduledata-validation.md` |
| `vanilla-data-comparison.md` | 55-66, 76-93, 118-142 | `data-content-cultures.md`, `localization-ui.md` |
| `troops.md` | 115-149, 189-193 | `data-content-cultures.md` |
| `xml-data.md` | 45, 67, 80, 102, 110-121 | `data-content-cultures.md` |
| `xslt.md` | 20, 52-55 | `xslt-moduledata.md` |
| `external-skill-ports.md` | 65, 82-86, 134-143 | `build-tooling-workflow.md` |
| `hook-authoring.md` | 55, 70, 86-89, 112-113, 160-161 | `build-tooling-workflow.md` (the headings exist there) |
| `provenance.md` | 19-29, 75-76 | none; the live rule is "name the source" |
| `review-codex/SKILL.md` | 34 | none; keep the trust requirement only |
| `refactoring-specialist.md` | 107 (the removed `deslop` skill) | none |

Also in this pass: memory-file pointers (`feedback_*`) inside rules, which no subagent can load
(`gui-ui.md:42,48,144`, `gamemodels.md:42`, `native-cpp-ports.md:27`), and `adapters.md:14`,
`gui-ui.md:174` naming `ilspycmd` where AGENTS.md names `pwsh tools/taom-src.ps1 path <Type>`.

## D3. Dated prompt idioms and generic method text (medium)

| # | Location | Fix |
|---|---|---|
| 1 | `working-discipline.md:17-18` | `think harder first: re-read the transcript, recombine near-misses` to `first re-read the transcript and recombine near-misses` (effort, not prose, sets thinking depth on Opus 5.5) |
| 2 | `agents/architect.md:12` | delete `Think through trade-offs before acting.` |
| 3 | `external-skill-ports.md:22` | `**CRITICAL divergence from the upstream source.**` to `**Divergence from the upstream source.**` (the reason follows in the same sentence) |
| 4 | `csharp-architecture.md:242-251` | delete "Stale-file re-read": the harness already refuses an edit on a stale read |
| 5 | `tests.md:23-37` | delete the generic "AAA Pattern" section; the naming convention and MSTest facts stay |
| 6 | `csharp-patterns.md:72-74` | `Transpilers iterate List<CodeInstruction> by hand; Harmony's CodeMatcher is available but unused here.` |
| 7 | `agents/refactoring-specialist.md:34-41,65-72` | delete "When to invoke" thresholds and the textbook move list; the Iron Rule, graphify mandate and doc sweep stay |
| 8 | `agents/debugger.md:26-33` | delete the generic four-phase method; the `/investigate` boundary and report format stay |
| 9 | `agents/error-detective.md:75-78`, `agents/feature-builder.md:29-36,70-78` | delete the duplicate read-only note and port archaeology; replace feature-builder rules 1-7 with a pointer to AGENTS.md "Always" plus the MSTest and `taom-src` deltas; keep only the graphify sentence of the retrieval cycles |

## D4. `model-effort-selection.md` direction (medium)

Lines 14-16 (committed today in `d0e4bc947`) send every refactor, new feature and non-obvious debug
through an `architect` plan; the table puts "refactors inside one module" and "normal bug fixes" at
`implementer`, and line 20 says "Start at the lowest tier that plausibly fits". Proposed text, if the
table is the principle:

> Specialists set their own tier in their definitions. An agent runs one model, so a cross-cutting
> refactor, a feature with several viable designs, or debugging with an unknown cause starts with an
> `architect` plan and then a Sonnet specialist executes it; a well-scoped one starts at the specialist.

## D5. Low-confidence flags (record only)

- CLAUDE.md skill-table rows for the 14 skills pending removal dangle once they go (the removal commit owns them).
- CLAUDE.md:58 says `/ship` runs `/review-codex`; `ship/SKILL.md:37` already asks before paid Codex.
- Tic lists with no recorded incident: `evidence-over-claims.md:25-26`, `output-style.md:46-50`.
- ASD-STE100 numeric sentence caps (user scope): the standard itself, with a "do not lose information" clause.
- Engine line-number cites pinned to v1.4.5 to v1.5.3 across `harmony-patches`, `gui-ui`, `csharp-architecture`, `troops`, `xslt`, `vanilla-data-comparison`: re-verify at the next touch.
- `gamemodels.md:9,62` say 54 overrides; the table has 53 rows; `lint_docs.py` checks only line 9.
- `MANDATORY` headings in the C# rules: each carries a reason, so density only.
- `harness-facts.md` item 4 (RCA for every confirmed bug) is a review rule cited circularly by `deep-review/SKILL.md:182`.
- Skill descriptions that summarize the workflow rather than the trigger (issue, freeze, unfreeze, new-feature, verify, review-codex, engine-bump, ship, investigate).
- About 20 `feedback_*` memory pointers in skills and lenses: machine-local, unread by subagents.
- `investigate:165`, `new-feature:59` run the deploying `./build.ps1 -RunTests` in the main session.
- `armory-audit:58` names a scratch script outside the repo; `security-scan` pins counts nothing computes.
- `xslt.md:83-87` restates its own lines 14-18 and 38-50.
- Agent roster: no redundant pair.
