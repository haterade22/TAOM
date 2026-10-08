# Skill usage audit (2026-10-08)

Which of the 45 skills in `.claude/skills/` do the models need? Mike's test: if the models do a
skill's work correctly the first time without it, the skill goes. This audit measured that from the
session transcripts, then checked each skill's references and the lines found only in it. Mike
approves or declines each group one at a time.

## Method

- **Transcripts:** 134 main sessions and 2,625 subagent transcripts under
  `~/.claude/projects/e--repos-TAOM/`, 2026-08-19 to 2026-10-08. The audit session is excluded.
- **Usage:** every `Skill` tool call and every typed slash command, per skill.
- **Without the skill:** for each skill with 0 to 2 calls, the sessions where its task happened
  anyway: direct runs of its script, edits to its target files, `error CS####` results for
  `/build-fix`, commits for `/commit-split`. Rework signals: the same failure repeated in one
  session, a user correction in the next 6 prompts, a PreToolUse hook deny.
- **Static:** inbound references (CLAUDE.md, rules, agents, other skills, hooks, tests, tools), the
  lines found only in the skill, its git history, and lessons dated 2026-08-15 or later.

## Coverage limits

- Seven weeks is short for rare work (engine bumps, new cultures, new map props). Those skills are
  judged on their content as much as on their call count.
- Codex sessions and the `.agents/skills/` Codex skills are not in these transcripts.
- Correction markers are noisy, so every topical hit was read by hand. Mike seldom writes an
  explicit correction, so a zero is weak evidence alone; hook denies are the stronger signal.
- Five skills were already `"off"` in the gitignored `.claude/settings.local.json`
  (`skillOverrides`): migration-status, context-budget, knowledge-compile, skill-stocktake and
  agent-introspection-debugging. That is why the model listing did not show them on this machine.

## Findings

- 21 of 45 skills had no call in the window, and 13 had one or two. Subagents invoke no skills.
  Nobody typed a slash command for any of the 21.
- The model runs the wrapped scripts directly, and they work: `taom-src.ps1` about 5,100 calls in
  81 sessions, `lint_docs.py` about 2,100 in 96, the binding gate 138 runs in 16.
- 301 `error CS####` build results in 49 sessions were fixed without `/build-fix`. The longest run
  of failing builds was 5 (the v1.5.0 bump), so its 4-attempt stop never triggered.
- The mistakes that recurred were caught by gates, not skills: `check-commit-subject-version`
  (24 denies), the `lint_docs.py` dash check, the compiler, the binding tests,
  `verify_mount_assets` and `SKILL_TEMPLATE_MISMATCH`.
- Stale skills gave wrong instructions:
  - `xslt-check` step 2 maps spkingdoms, spclans, lords, heroes and module_strings to SandBoxCore.
    Those files exist only in SandBox (checked against the install).
  - `lord-skills` says a lord defined in both `lords.xslt` and `characters/lords.xml` is dead code
    in the XSLT. The engine merges both ([lesson #644](lessons/xslt-moduledata.md)), and
    `docs/ai-includes/lord-skills-authoring.md` repeats the claim.
  - `build-fix` builds without `-p:DisableModuleCopy=true -p:ModuleId=` and cites v1.3
    namespaces. `taom-src` names v1.5.2 and `research` v1.3.12. `migration-status` describes the
    finished v1.2 to v1.3 move. `lint-cleanup-loop` targets the frozen 1.4.5 trunk.

## Decision rule

A skill stays if the models invoke it (3 or more calls), if it holds a multi-step protocol with
TAOM traps that has no other home, or if code, hooks or tests read files in its folder. Otherwise
its unique lines move to the doc that owns them, and the CLAUDE.md skills table routes to the
script or the doc.

## Decisions

| Group | Skills | Recommendation | Decision |
|---|---|---|---|
| Keep | 17 skills (below) | Keep | |
| A | migration-status, agent-introspection-debugging, knowledge-compile, skill-stocktake, lint-cleanup-loop, finish-branch, context-save, context-restore | Remove | Approved, removed |
| B | humanizer, doc-graph, lint-docs, codex-verify, deslop, scope-check | Remove | Approved, removed |
| C | taom-src, research, build-fix, verify-bindings, xslt-check, commit-split, context-budget | Remove | Approved; waits until another session commits its edits to `engine-bump/SKILL.md` and `.claude/rules/moduledata-validation.md` |
| D | author-armor, new-culture, lord-skills, new-adr, new-map-prop, refine-creature-anim, new-creature-mount | Remove | Approved; waits with group C |

One change from the plan: skill-stocktake's "un-skilled workflow" check was dropped rather than moved,
since it asks for more skills and this audit found the opposite problem.

## Keep (17)

| Skill | Why |
|---|---|
| deep-review, issue, review-codex, investigate, verify, localize, new-feature, adopt-external, native-crash-triage, release, security-scan | 3 to 109 calls each |
| engine-bump | A protocol with no other home. Its SKILL.md was read at every bump (1.5.0, 1.5.2, 1.5.3, 1.5.4), and the step the 1.5.4 first pass missed became a step here |
| improve | `tools/improve_ctl.py` and two pytest files read its `references/` and `workflows/` |
| ship | Without it, 12 of 22 completion runs skipped the second Codex pass; with it, that pass found a real bug |
| armory-audit | The SessionStart banner names it, and it holds companion checks found nowhere else |
| freeze, unfreeze | `/investigate`'s hooks run `freeze/check-freeze.sh`, `tools/test_hooks.sh` tests it, and the deny text names `/unfreeze` |

## Per-skill evidence

### Group A: unused or user-only, no code reads them

| Skill | Evidence | Lines to move |
|---|---|---|
| migration-status | 0 calls, off, stale | none |
| agent-introspection-debugging | 0 calls, off; no cross-turn looping seen | none (generic) |
| knowledge-compile | 0 calls, off; `compile_research.py` never ran | phase order to `docs/research/README.md` |
| skill-stocktake | 0 calls, off; `tools/test_hooks.sh` automates its deterministic checks | two checks (a cited `/skill` resolves; the un-skilled workflow scan) to `.claude/rules/external-skill-ports.md` |
| lint-cleanup-loop | user-only, never typed, stale | none |
| finish-branch | user-only, never typed; 53 merges done by hand | footer staging and the `git branch -d` merge check to `docs/ai-includes/git-and-commits.md` |
| context-save, context-restore | 0 calls; 661 memory writes and 69 resumes after compaction did the job | none |

### Group B: the model does the task correctly without them

| Skill | Evidence | Lines to move |
|---|---|---|
| humanizer | 1 dash in 863 commit commands; the lint dash check catches doc dashes; 0 corrections | none (`output-style.md` Part 2 holds the TAOM rules) |
| doc-graph | `graph_query.py` run directly; 0 rework | none |
| lint-docs | `lint_docs.py` run about 2,100 times directly | "A zero is not self-validating" to `docs/features/doc-health-linter.md` |
| codex-verify | duplicates `/review-codex` (24 calls) | none |
| deslop | 0 calls, 0 requests | the Harmony patch class limit to `.claude/agents/refactoring-specialist.md` |
| scope-check | 0 calls, 0 scope corrections | "dropping the other item silently is not an option" to `.claude/rules/think-before-coding.md` |

### Group C: engine and build wrappers with many references

| Skill | Evidence | Lines to move |
|---|---|---|
| taom-src | about 5,100 direct script calls; CLAUDE.md and AGENTS.md already cite the script | none |
| research | 90 sessions researched without it; 1 wrong engine guess, caught by the compiler | none |
| build-fix | 301 build errors fixed without it; its stop never triggered | the 4-attempt retry budget to `docs/ai-includes/agent-operating-manual.md` |
| verify-bindings | 138 direct runs; its failures were real engine changes | "Skipped is never green" to `docs/reference/taleworlds-api-snapshot/README.md` |
| xslt-check | 21 XSLT edits without it and 0 corrections; transforms and tests caught the real bugs | a corrected file map to `.claude/rules/xslt.md` |
| commit-split | 21 multi-commit sessions without it; the version hook caught every slip, inside the skill too | the path-to-commit-type table to `docs/ai-includes/git-and-commits.md` |
| context-budget | off; `check-doc-config-drift` enforces the budget at commit | `scan.sh` to `tools/`, with `tools/test_hooks.sh` section 8 updated |

### Group D: thin pointers to content-pipeline docs

| Skill | Evidence | Routes to |
|---|---|---|
| author-armor | 8 sessions ran its scripts directly; the validator gates it | `docs/features/troop-tree-revamp.md` |
| new-culture | 1 call (Arthedain, passed first time) | `docs/ai-includes/new-culture-authoring.md` |
| lord-skills | 2 sessions did it without; a hook and a test gate it | `docs/ai-includes/lord-skills-authoring.md` |
| new-adr | 2 calls; the ADR index update was missed even with the skill | `docs/adrs/000-template.md` |
| new-map-prop | no prop built yet; the skill is 9 days old | `docs/reference/tpac-static-prop-authoring.md` |
| refine-creature-anim | the 1 run with it had a correction; the run without had none | `docs/ai-includes/creature-animation-blender-mcp-workflow.md` |
| new-creature-mount | `verify_mount_assets` caught the misses | `docs/ai-includes/creature-mount-authoring.md` |

## Follow-ups

- The binding gate passes with skipped tests: 44% of binding runs after 2026-09-23 omitted
  `binding-gate.runsettings`. A gate that fails on a non-zero Skipped count beats any text.
- The ARMORY ART DRIFT banner fired in 29 sessions from 2026-10-02 and the catalogue was never
  regenerated, so it never cleared.
- P5 (prune the skill listing) and P10 (change `/build-fix`) in the 2026-10-08 Claude Code update
  review overlap this audit.
