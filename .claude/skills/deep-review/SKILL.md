---
name: deep-review
description: Use before committing C# or XML work. Senior reviewers audit standards, engine compatibility, efficiency, completeness, data flow, XML integrity and design, then apply every better way found.
argument-hint: "[feature-name]"
effort: max
---

# Deep Review

Launch as many review agents as the work needs. Which lenses run follows what is in scope (Step 2 table), and there is NO LIMIT on the total: if the scope demands 10, 20 or 100 agents, launch them, at most four at a time (Step 2 "Waves of four"). Scale the review to match the risk.

Run this AFTER completing a feature or fix, BEFORE closing out.

**Every reviewer is the most senior role available** (Mike, 2026-09-18). Every lens runs as `subagent_type: deep-reviewer` (`.claude/agents/deep-reviewer.md`: Fable at `effort: max`, read-only). **Never pass `model` on these Agent calls:** a per-invocation `model` overrides the definition's (`.claude/rules/harness-facts.md`), which would quietly downgrade the reviewer.

**The review does not stop at a report.** Two lenses (Agent 3 Efficiency, Agent 6 Design & Elegance) propose better ways to do what the change does; Step 4 applies them. The reviewers stay read-only; the orchestrator applies, after every lens has reported.

The feature or area to review: `$ARGUMENTS` (if empty, review all uncommitted changes).

## Step 0 (Optional): Codex Independent Pre-Review

**Trigger when:** `$ARGUMENTS` contains `--codex` (strip `--codex` from the feature name before proceeding).

If triggered:
1. Identify changed files (same logic as Step 1 below).
2. **Dispatch Codex directly via Bash** -- Claude does this itself, no terminal hand-off:
   - Pre-flight: `codex login status` -- expect `Logged in using ChatGPT`. If not, surface the message and continue WITHOUT the Codex pre-review (don't block the Claude agents).
   - Write a focused prompt to `docs/reviews/codex-prereview-{feature}-{date}.prompt.md` (short version of the `/review-codex` prompt -- focus on Known Suspects + architectural risks; skip the heavy vanilla-decompile block).
   - Run via Bash:
     ```
     command: cd "<repo-root>" && mkdir -p docs/reviews/raw && codex exec -c project_doc_max_bytes=65536 - < "docs/reviews/codex-prereview-{feature}-{date}.prompt.md" > "docs/reviews/raw/codex-prereview-{feature}-{date}.md" 2>&1
     run_in_background: true
     timeout: 600000
     ```
   - See `.claude/skills/review-codex/SKILL.md` "Codex CLI invocation contract" for full dispatch semantics.
3. Continue to Step 1 immediately; do NOT wait for Codex here. The Claude agents run in parallel with the Codex background job.
4. After all Claude agents complete (Step 2), check if the Codex background job has notified. If yes, read `docs/reviews/raw/codex-prereview-{feature}-{date}.md`. If not yet (Claude agents finish faster on this kind of work), Codex result will arrive later -- proceed with Step 3 using just the Claude agent results and append Codex when it arrives.
5. Include Codex findings in the Step 3 compiled report as its own section:
   ```
   CODEX REVIEW:  [PASS/ISSUES — N findings]
   [Codex findings grouped by severity]
   ```

If Codex and any Claude agent disagree on a finding, flag the disagreement explicitly — it is valuable signal.

If `--codex` is not present, skip this step entirely.

## Step 1: Identify Scope

Determine what to review:
- If `$ARGUMENTS` is provided, focus on that feature/area
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files
- **Add every live `TAOM_Map` or `LOTRLOME_Armory` file changed since the last review.** They are unversioned, so git cannot list them, and a live edit missing from the list is an edit nobody reviews, including one a script made in an earlier session. Before launching anything, take the time of the last review OF THIS CHANGE (its RCA or report date), else the start of the work in scope. **Not** the last `agent_type=deep-reviewer` line in `.claude/logs/agent-audit.log` on its own: every session writes to that log, so its latest line can be another session's review of other files (2026-09-18: using it would have dropped a session's whole morning of live edits). Then run `find "<game>/Modules/TAOM_Map/ModuleData" "<game>/Modules/LOTRLOME_Armory/ModuleData" "<game>/Modules/LOTRLOME_Armory/SubModule.xml" "<game>/Modules/TAOM_Map/SubModule.xml" "<game>/Modules/TAOM_Map/Assets" "<game>/Modules/LOTRLOME_Armory/Assets" -type f -newermt "<that time>" ! -name "*.bak*"`. Include each file with its absolute path, or name it NOT IN SCOPE in the report; never drop one. Concurrent sessions edit the same live modules, so attribute each file (this change's, or another session's and out of scope) before it goes to a lens. **Binary packages** (`Assets/**/*.tpac`) are in scope too, though no lens reads their bytes: list each with the tool or Kit step that wrote it and its backup, and give the writing script to the Tooling lens.

Collect the list of changed files for the agents, split into **C# / C++**; **XML / XSLT** (ModuleData, stylesheets, GUI prefabs, `SubModule.xml`, `project.mbproj`, language files, repo and live); **scripts** (`tools/**`, `.claude/hooks/**`); **harness** (`.claude/**`, `CLAUDE.md`, `AGENTS.md`, `.ai/**`); and **docs**. The split decides which lenses launch in Step 2. A module's `SubModule.xml` sits at its root, outside `ModuleData/`, which is why the sweep above names both (#619's edit to TAOM_Map's was invisible to a `ModuleData`-only sweep).

## Step 2: Launch the Review Lenses

Each lens is a prompt file under `lenses/` (next to this file) that the reviewer reads itself, so the orchestrator never pastes one. Every lens, the Step 2b agent and every Step 2c expansion launches as `subagent_type: deep-reviewer`, never with a `model` parameter.

**XML is code** (Mike, 2026-09-18). One wrong attribute spawns a naked troop, hangs a battle load or kills a dedicated server, and the engine reports almost none of it, so XML gets the same review as C#.

### Which lenses run

Decide from the Step 1 split. Every changeset shape is covered; a lens with nothing in its domain is reported NOT IN SCOPE in Step 3, never dropped silently.

| Agent | Lens file | Runs when |
|---|---|---|
| 1 Standards | `lenses/1-standards.md` | C# or C++ in scope, or harness files (`.claude/**`, `CLAUDE.md`, `AGENTS.md`, `.ai/**`) |
| 2 Engine compatibility | `lenses/2-engine-compat.md` | C# or C++ in scope, or changed text or tooling that states engine behaviour |
| 3 Efficiency | `lenses/3-efficiency.md` | C# or C++ in scope, or a script under `tools/` or `.claude/hooks/` |
| 4 Completeness | `lenses/4-completeness.md` | always |
| 5 Data flow | `lenses/5-data-flow.md` | always; the highest-value lens (every HIGH Codex has found in this project was a data-flow gap) |
| 6 Design & Elegance | `lenses/6-design.md` | always |
| 7 XML & ModuleData | `lenses/7-xml.md` | any XML or XSLT in scope, repo or live install |
| Tooling correctness | `lenses/tooling.md` | any script under `tools/` or `.claude/hooks/`, read-only ones included |

### Waves of four

**At most four agents in flight.** Launch a wave, let every agent in it report, then launch the next. A subagent that hits the usage limit returns nothing, so a wave boundary caps what a limit can cost (memory `agent-budget-discipline`; on 2026-09-18 a six-wide review lost three agents that way). Fill the first wave with the defect lenses in scope (1, 2, 5, 7), then the others in order. A later wave's prompts may carry the earlier waves' CRITICAL and HIGH findings, so the design lens does not polish code about to be restructured. There is no limit on the total, only on what is in flight.

### The spawn prompt

```
Lens: Agent <N> <name>. Read .claude/skills/deep-review/lenses/<file> first; it is your whole task and its output format.
FILES: <the Step 1 list for this lens; absolute paths for live-install files>
SCOPE NOTES: <the change's intent; in shared files, which hunks are this change's; earlier waves' CRITICAL/HIGH findings>
```

Add only what the lens cannot know. The agent definition already carries the operating-manual briefing and the read-only rules.

**Triage ordering, spec before quality** (`docs/ai-includes/agent-teams.md` "Subagent review ordering"): resolve Agent 1, 2, 5 and 7 findings before acting on Agent 3 and 6 proposals. A standards violation can make quality feedback moot; don't optimise code that is about to be restructured.

## Step 2b: Adversarial Escalation (conditional)

**Only if Agent 1 reports a CRITICAL violation:** direct TaleWorlds sealed-type usage in a service class (ADR-007 breach), a Harmony patch that touches game state without an adapter, or an entry point over 150 lines that does business logic itself. Launch one additional `deep-reviewer` on only the offending files with `lenses/adversarial.md`.

## Step 2c: Adaptive Expansion (always evaluate)

After the core agents complete, assess whether the findings warrant additional focused agents. There is NO upper limit on the total. Every additional agent is a `deep-reviewer` too, launched in waves of four like the core set. Before spawning one, ask whether an existing gate script answers its question: a deterministic check run once beats an agent re-deriving it (memory `agent-budget-discipline`).

**Launch additional agents when:**
- Agent 5 (Data Flow) finds gaps → launch per-gap investigation agents to trace the full chain and propose fixes
- Multiple XML config files changed → launch one agent per config file to cross-reference all consumers
- Multiple Harmony patches changed → launch one agent per patch to verify target method signatures and side effects
- Multiple GameModel overrides changed → launch one agent to verify all overrides are registered and don't conflict
- Any agent reports >3 issues → launch a focused agent on just those files to determine root cause
- Feature spans >3 features/ subdirectories → launch per-feature agents with full context of that feature

(Script tooling is no longer an expansion: the Tooling correctness lens in Step 2 covers every script under `tools/` or `.claude/hooks/`, read-only ones included.)

**Launch additional Codex passes when:**
- Any Claude agent and Codex disagree → dispatch a second Codex pass focused on the disputed finding
- Data Flow agent finds a gap Codex missed → dispatch Codex with the specific gap description to get independent verification

**The review is done when:** All agents have reported, all disagreements are resolved, no agent's findings suggest an unexplored area, and Step 4 has applied or accounted for every proposal.

## Step 3: Compile Report

After every launched agent completes, compile their results into a single report. Mark a lens the Step 2 table did not launch as NOT IN SCOPE. The three IMPROVEMENTS lists and the VERDICT are filled in by Step 4:

```
DEEP REVIEW REPORT
===================
Feature: [name or "uncommitted changes"]
Date: [today]

Scope:   [C#/C++, XML/XSLT, scripts, harness, docs; live files swept since <time>]
Waves:   [which agents ran in which wave]

STANDARDS:     [PASS/FAIL — N violations / NOT IN SCOPE]
COMPATIBILITY: [PASS/FAIL — N incompatible, N unverified / NOT IN SCOPE]
EFFICIENCY:    [PASS/FAIL — N issues (H high, M medium, L low) / NOT IN SCOPE]
COMPLETENESS:  [COMPLETE/INCOMPLETE — list missing items]
DATA FLOW:     [PASS/FAIL — N gaps, N inconsistencies]
DESIGN:        [N KEEP proposals (A apply, F follow-up) / ALREADY OPTIMAL]
XML:           [PASS/FAIL — G gates (F failed, X not run), N findings / NOT IN SCOPE]
TOOLING:       [PASS/FAIL — N findings / NOT IN SCOPE]

─────────────────────────
DETAILS
─────────────────────────

[Agent 1 results — Standards]

[Agent 2 results — Compatibility]

[Agent 3 results — Efficiency]

[Agent 4 results — Completeness]

[Agent 5 results — Data Flow]

[Agent 6 results — Design & Elegance]

[Agent 7 results — XML & ModuleData Integrity, incl. OWED in-game checks]

[Tooling correctness results]

─────────────────────────
ACTION ITEMS
─────────────────────────
1. [Most critical issue first]
2. ...

─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:     [file:line, what changed, the test that proves it]
NOT APPLIED: [file:line, one-line reason each]
FOLLOW-UP:   [pre-existing code outside the change; its issue number, or why none is filed]

VERDICT: READY FOR COMMIT / NEEDS FIXES
```

## Step 3e: Root Cause Analysis (MANDATORY — BLOCKING GATE before commit)

If any agent (or Codex pre-review, if Step 0 ran) returned ANY confirmed finding (any severity, including LOW), Phase 3e RCA applies before the closing commit. Per `.claude/rules/harness-facts.md` and `feedback_root_cause_mandatory.md` — this is not optional, not severity-gated, not "only HIGH." The literal text: *"Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features."*

The recurring failure is conflating severity with importance for RCA: we patch LOW symptoms but never extract the systemic lesson, and the same category of bug ships again. Three examples on file: Career cooldown review #31 (NaN gate), EditorCacheRebuild review #38 (NaN gate again), scene-scripts CS_Road 2026-05-13 (NaN gate, THIRD time). All caught by the same rule that was scope-gapped on each project.

**For EVERY confirmed finding (not just HIGH/MED):**

**First, confirm it is actually confirmed.** Per `.claude/rules/evidence-over-claims.md`, a finding is "confirmed" only if you (or the agent) re-read the actual TAOM source / decompiled vanilla and verified the bug exists — not because an agent reported it confidently. If you took an agent's finding on faith, re-read the code now; an unverified finding is re-flagged for investigation, not entered into RCA.

1. Write the finding text + severity.
2. **Why missed:** what assumption, scope gap, or pattern blindness let it through? Be specific — name the rule that should have caught it, name the file/line that exhibits the pattern.
3. **Preventive action:** generalizable rule, feedback-memory entry, or scope extension to an existing rule? Or one-off?
4. If the pattern has shipped before (grep `docs/reviews/rca-*.md` + `~/.claude/projects/.../memory/feedback_*.md` for it), call that out — repeat-offender bugs need stronger preventive action than first-time bugs.

**Write the result to `docs/reviews/rca-<feature>-<YYYY-MM-DD>.md`** following the format of `docs/reviews/rca-quickactions-2026-05-06.md` or `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md`:
- Top-line summary
- Findings table: # | Sev | Bug | Category | Why Missed | Preventive Action
- Root-cause pattern section (if 2+ findings share a theme)
- "Why each agent missed these" section: for each deep-review agent that ran and didn't catch the finding, state why their rule set didn't apply or why the agent's scope was too narrow
- "Feedback memories to codify" section — only if there's a genuine systemic pattern; don't manufacture rules

**Append the durable lesson to the master record.** For each systemic finding, also add an entry to the matching category file under `docs/reviews/lessons/` (e.g. `lessons/gamemodels-services.md`, `lessons/harmony-il.md`; index: `docs/reviews/LESSONS-LEARNED.md`), in the house shape (`### <imperative rule>` → `**Why missed:**` → `**Prevent:**` → `**Source:**`). The per-feature `rca-*.md` is the incident report; the lessons entry is the cross-feature rule that stops the *category* from recurring — it is the canonical, always-consulted record (indexed from the harness `MEMORY.md`). Read the relevant category FILE before the next review of that subsystem (per-category files keep the read cheap). Only touch `MEMORY.md` if a new feature/topic memory file is involved.

**This file MUST exist BEFORE the closing commit.** The commit message should reference the RCA path.

If the RCA reveals a rule that's already documented but wasn't followed (scope gap or agent prompt missing the rule), update the rule file or agent prompt in a follow-up commit. Commit graph: review → fixes → RCA → preventive-rule update.

## Step 4: Apply improvements (MANDATORY)

A better way that the review found and left in a report is a better way nobody gets. When Agent 6 returns a KEEP proposal or Agent 3 an APPLY-scoped fix for the changed code (together, "proposals" below), the orchestrator applies it in this session. The reviewers never edit; you do.

1. **After every lens, defects first.** Start only when all agents (Step 2b and 2c included) have reported: editing a file an agent is still reading invalidates its report (`.claude/rules/working-discipline.md`). Resolve Agent 1, 2, 5 and 7 findings before any improvement, per the Step 2 triage order.
2. **Changed code only** (Mike, 2026-09-18). A FOLLOW-UP proposal (pre-existing code the change did not modify) is never applied here; list it with its issue number, or with the reason none is filed.
3. **Re-verify, then apply or account.** Re-read the source behind each proposal before applying it (`.claude/rules/evidence-over-claims.md` §A). Every proposal not applied goes under NOT APPLIED with its reason: disproved, breaks an ADR, a single-owner file (`IoC.cs`, `SubModule.cs`) held by another session, or Mike declined. No silent skips.
4. **Behaviour-changing proposals: ask once.** Put every CHANGING proposal in one `AskUserQuestion` batch before applying any; PRESERVING ones need no question.
5. **Tests prove it.** The suite is green before you start. PRESERVING: an existing or new characterisation test, green before and after. CHANGING: its RED test first. Run at the `.claude/rules/evidence-over-claims.md` §B cadence and quote the final full suite. For XML, re-run the `.claude/rules/moduledata-validation.md` "Gate per file kind" gates for what you touched and the shipped-data tests Agent 7 named; edit XML per `tools/README.md` "XML I/O convention", never with `sed -i` on a CRLF file.
6. **One convergence pass, then stop.** Launch a single `deep-reviewer` on the diff of the applied improvements, checking standards and behaviour parity. Defects it finds go to the fix loop below; it may NOT open a new design round, which keeps the loop finite.
7. **Review-only assignments skip this step.** Under a `.ai` review packet or any review-only request, the no-fix boundary in `.ai/roles/reviewer.md` wins: report the proposals, apply nothing.

Then fill in the IMPROVEMENTS lists and the VERDICT in the Step 3 report. READY FOR COMMIT requires Step 4 complete and the final full suite green.

## Important

- The review agents are READ-ONLY; they never edit. Code changes happen only in Step 4 (improvements) and the fix loop below (defects), both done by the orchestrator.
- If any agent fails to launch (MCP issues, etc.), note it in the report and run the checks manually.
- Engine signatures and behaviour come from the installed DLLs (`pwsh tools/taom-src.ps1 path <Type>` or ilspycmd). The dump at E:\Decompiled_Bannerlord\ can lag an engine bump; use it to browse, never as the authority.
- Agent 5 (Data Flow) is the highest-value lens: it catches the class of bugs every other lens consistently misses.
- If the verdict is NEEDS FIXES, list the fixes needed in priority order.

## HIGH findings — no silent deferrals (MANDATORY)

If any agent reports a HIGH-severity finding (Agent 7's CRITICAL counts, as does a Codex P1):
1. The default action is FIX. Implement the fix in the same session.
2. If the user explicitly chooses to defer, the deferral MUST be recorded in one of:
   - A GitHub issue (`gh issue create`) with the finding text
   - A commit trailer `Deferred: <reason>` on the commit that would have fixed it
   - A CHANGELOG "Known limitation:" bullet

What is NOT allowed: quietly proceeding past a HIGH finding on informal reasoning ("only matters in case X") without writing the decision down. Past experience: Career System P2 (ally buff overwrite) was flagged HIGH by Agent 5 and dismissed — Codex independently caught the same bug later. Memory: `feedback_dont_defer_high_review_findings.md`.

## Fix-loop guidance

When the verdict is NEEDS FIXES and the user chooses to address findings now:

- If fixes are confined to one feature module, **suggest `/freeze`** scoped to that module before starting. Prevents the fix-loop from drifting into adjacent code that wasn't part of the review.
- If a finding is structural (root cause unclear, multiple symptoms in one area), invoke **`/investigate`** instead of fixing directly — its 6-phase workflow auto-engages `/freeze` and enforces the Iron Law.
- **Verify each fix at the OUTERMOST gate, not just the layer you edited.** When a fix makes a method's behavior unconditional (or changes its guard semantics), grep every CALLER of that method for early-outs that re-condition it before marking the finding fixed — and note that layer-local regression tests cannot see caller-level gates. (Codex review 72 caught a service-layer "unconditional close" fix bypassed by two hook-level `IsEnabled` gates one review after it shipped; RCA `rca-tournament-exit-hang-2026-07-06.md` finding #4.)
- After fixes land, re-run `/deep-review` (or `/deep-review --codex`) on the fix diff to confirm no new HIGH findings introduced. Its design lens sees only that diff, so the loop narrows each round.
