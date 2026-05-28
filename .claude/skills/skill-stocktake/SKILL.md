---
name: skill-stocktake
description: Periodic quality audit of installed skills + agents. Quick scan (recently changed only) or full audit. Catches decay (broken cross-references, stale paths, bloated descriptions) before sessions silently degrade.
allowed-tools:
  - Bash
  - Read
  - Grep
  - Glob
  - AskUserQuestion
---

# /skill-stocktake — Quality Audit of Skills + Agents

Adapted from [affaan-m/everything-claude-code/skills/skill-stocktake](https://github.com/affaan-m/everything-claude-code/tree/main/skills/skill-stocktake) — TAOM version drops the multi-subagent batch evaluation and uses a leaner deterministic-scan + LLM-judgment two-pass.

## When to use

- Quarterly audit (or after a major harness change)
- After porting skills from external suites — confirm none silently broken
- When `/context-budget` flags bloat or staleness in any component
- When a skill's behavior surprised you and you suspect decay

## Modes

| Mode | Trigger | What it does |
|------|---------|--------------|
| Quick scan | `/skill-stocktake` (default) | Audit only skills/agents modified in last 30 days |
| Full audit | `/skill-stocktake full` | Audit every skill + agent in the repo |
| Specific | `/skill-stocktake <name>` | Audit one skill or agent by name |

## Audit checklist

For each skill/agent under audit, check:

### Frontmatter
- [ ] `name:` matches the directory/file name
- [ ] `description:` is ≤30 words (per `harness-facts.md` description-bloat rule)
- [ ] `description:` is single-line YAML, not a multiline block (per `scan.sh extract_description` limitation)
- [ ] No `triggers:` field (not consumed by current Claude Code; per `harness-facts.md`)
- [ ] `allowed-tools:` matches what the skill body actually uses (mismatch = either dead permission or undeclared dependency)
- [ ] `effort:` if present, is one of `low|medium|high|max|inherit`. `effort: low` should NOT be set on skills that do significant inline reasoning (use `inherit` instead) — caught in review #29
- [ ] `hooks:` if present, command paths resolve (file exists, is executable, in tracked git)
- [ ] If a `paths:` field is present (rules only), it's intentional — `paths: ["**/*"]` is conditional, not always-load (per `harness-facts.md`)

### Hook integrity (PreToolUse(Bash) hooks specifically)
- [ ] If the hook detects `git commit` invocations, it uses the canonical two-stage pattern from `harness-facts.md:58-84` ("Git invocation forms hooks must handle"). Forms it must catch: `git commit`, `git -C path commit`, `git -c key=val commit`. Forms it must REJECT: `git commit-tree`, `git commit-graph`. **Catching review #28's recursion-risk: a bare `*"git commit"*` substring match is a CONFIRMED FAILURE — both Codex pass 1 and pass 4 found this exact bug class.**
- [ ] If the hook handles `git commit --amend`, it does NOT blanket-skip amends. Per `harness-facts.md` "Amend exemptions": diff-based gates compute `staged ∪ HEAD`; working-tree-state gates don't exempt at all. Caught as a HIGH bypass in review #28.

### Documentation labeling (per harness-facts.md rule 5)
- [ ] If the skill or its referenced rules state facts about Claude Code runtime behavior, each fact is labeled DOC-BACKED (with URL) or EMPIRICAL (with observation context). Vague "verified" claims are forbidden.

### Cross-references
- [ ] Every `/skill-name` mentioned in the body resolves to an existing skill at `.claude/skills/<name>/SKILL.md`
- [ ] Every `feedback_*.md` memory citation exists at the actual memory path
- [ ] Every `.claude/rules/*.md` reference exists
- [ ] Every `docs/...` link resolves
- [ ] Every ADR reference (e.g. `ADR-007`) maps to a real `docs/adrs/*.md` file

### Workflow coverage (the "workflow → skill" convention)
- [ ] No qualifying documented workflow is missing a skill. Per CLAUDE.md "Workflow → Skill convention", a process that is **recurring + multi-step + gotcha-bearing** should be a skill. Scan for prose workflows that qualify but aren't skilled, and flag each as `[LOW] un-skilled workflow: <doc> — candidate /<name>`:
  - Numbered/phased authoring guides in `docs/ai-includes/*.md` with no matching `.claude/skills/<name>/`.
  - "Workflow (MANDATORY)" / "sequence" / "phases" blocks in CLAUDE.md that only chain other skills as prose.
  - Recurring `generate → apply → validate` tool pipelines in `tools/README.md` used across ≥2 features.
  Do NOT auto-create — one-offs, single-command operations, and reference-only docs are exempt (a skill for those just taxes eager context for no benefit). Surface as a human decision.

### Behavior consistency
- [ ] Promises in the description match what the skill actually does (e.g., a description claiming "auto-engages /freeze" must have a hook block in frontmatter that engages it)
- [ ] No claim that a state file alone activates a hook (per Codex review #28; see `harness-facts.md` "Inline-hook activation")

### Hardcoded values
- [ ] EXACT-tagged constants still match their source (e.g., MCP server tool counts — re-spot-check)
- [ ] HEURISTIC-tagged values flagged for re-verification

## How to run

### Phase 1 — Deterministic scan

```bash
# List skills and agents to audit
if [[ "$1" == "full" ]]; then
    SKILLS=$(find .claude/skills -mindepth 1 -maxdepth 1 -type d -printf '%f\n')
    AGENTS=$(find .claude/agents -maxdepth 1 -name '*.md' -printf '%f\n' | sed 's/\.md$//')
elif [[ -n "$1" ]]; then
    # Specific skill or agent
    SKILLS=$(find .claude/skills -mindepth 1 -maxdepth 1 -type d -name "$1" -printf '%f\n')
    AGENTS=$(find .claude/agents -maxdepth 1 -name "$1.md" -printf '%f\n' | sed 's/\.md$//')
else
    # Quick scan: changed in last 30 days
    SKILLS=$(find .claude/skills -mindepth 1 -maxdepth 1 -type d -mtime -30 -printf '%f\n')
    AGENTS=$(find .claude/agents -maxdepth 1 -name '*.md' -mtime -30 -printf '%f\n' | sed 's/\.md$//')
fi

echo "Skills to audit:"; echo "$SKILLS"
echo "Agents to audit:"; echo "$AGENTS"
```

For each skill/agent, mechanically check what can be checked deterministically:
- frontmatter parses as YAML (use `python3 -c "import yaml; yaml.safe_load(...)"`)
- `name` field matches dir/file name
- description word count
- presence of `triggers:` field (flag — undocumented in Claude Code)
- hook command paths exist and are tracked + executable
- paths to `.claude/rules/*`, `feedback_*.md`, `docs/`, `ADR-*` resolve

### Phase 2 — LLM judgment (the audit checklist)

For each skill/agent, read the SKILL.md / agent.md and judge the remaining items in the checklist that aren't deterministic — promise vs behavior consistency, EXACT-tagged-value freshness, `allowed-tools` matching actual usage.

This is the model's job, not a script's. Output one verdict per skill/agent:

```
## /<skill-name>
Status: PASS | NEEDS FIXES | WARN

Findings (if any):
- [SEVERITY] What's wrong — file:line — concrete fix
```

### Phase 3 — Summary report

Aggregate into a single audit report:

```
SKILL STOCKTAKE — <date>
========================
Mode: <quick scan / full / specific>
Skills audited: N
Agents audited: M
Total findings: X (Y HIGH, Z MEDIUM, W LOW)

Top findings (sorted by severity):
1. [HIGH] <skill> — <issue>
2. ...

PASS: skill-a, skill-b, skill-c
NEEDS FIXES: skill-d (3 findings), skill-e (1 finding)
WARN: skill-f (cross-ref to deprecated path)
```

If full-audit and findings exist, suggest creating GitHub issues for HIGH/MEDIUM (one issue per skill, not per finding, to keep tracking clean).

## Pair with

- `/context-budget` — token-cost audit. Stocktake is correctness/quality; context-budget is cost. Run both quarterly.
- `/deep-review` — applies to one feature/diff; stocktake applies to the harness as a whole.
- `external-skill-ports.md` — when fixing findings on a ported skill, re-check the port-drift checklist there.

## Notes

- Read-only by default. Suggested fixes are NOT applied automatically.
- Quick scan keeps the audit cheap (5-10 min). Full audit is heavier (~20-30 min depending on skill count) and should be a deliberate sit-down.
- Findings rarely need to block other work — log them and address in a dedicated harness-cleanup commit.
