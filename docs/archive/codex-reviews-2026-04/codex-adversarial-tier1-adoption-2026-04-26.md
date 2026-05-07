NEEDS FIXES — HIGH: 2 | MEDIUM: 3 | LOW: 2

## A. INLINE-HOOK SEMANTICS VERDICT

**Verdict: the inline hooks are real and supported in current Claude Code.**

How verified:
- Official Claude Code hooks docs say hooks can be defined directly in skills and subagents using frontmatter, are "scoped to the component's lifecycle", and "only run when that component is active": https://code.claude.com/docs/en/hooks
- Official skills docs list `hooks` as a supported `SKILL.md` frontmatter field: https://code.claude.com/docs/en/skills

Implication for TAOM:
- `.claude/skills/freeze/SKILL.md:14-27` and `.claude/skills/investigate/SKILL.md:20-29` are using a supported pattern.
- The pattern is **not** theater.
- But lifecycle scoping matters: the hooks exist only while that skill is active. That directly breaks one downstream claim in `feature-builder` (see HIGH finding #2).

## B. KNOWN SUSPECTS RESPONSE

1. **INLINE-HOOK ACTIVATION SEMANTICS** — **DISPUTED**
- Evidence: `.claude/skills/freeze/SKILL.md:14-27`, `.claude/skills/investigate/SKILL.md:20-29`
- Verification method: official docs, not inference.
- Result: current Claude Code explicitly supports hooks in skill frontmatter. They activate only while the skill is active.

2. **TRIGGERS FIELD UTILITY** — **UNVERIFIED**
- Evidence: `.claude/skills/freeze/SKILL.md:8-13`, `.claude/skills/investigate/SKILL.md:13-19`, `CHANGELOG.md:32`
- Verification method: official skills docs frontmatter reference.
- Result: current docs enumerate `description` and `when_to_use` as the discovery fields and do **not** document `triggers`. I found no current Claude Code doc showing `triggers:` is consumed. Treat this as unsupported until proven otherwise.

3. **JSON ESCAPE COMPLETENESS** — **DISPUTED for realistic Windows paths**
- Evidence: `.claude/skills/freeze/check-freeze.sh:128-145`
- Verification method: code inspection plus Windows filename constraints.
- Result: `_json_escape` correctly escapes backslashes and quotes, which are the relevant characters for realistic Windows paths. I did **not** confirm safe handling of literal control characters/newlines, but those are not valid Windows filename characters. Separate issue: the script corrupts freeze paths containing spaces by deleting all whitespace (`:28`).

4. **GITIGNORE BLAST RADIUS** — **DISPUTED**
- Evidence:
  - `.gitignore:1-25`
  - `.claude/tmp/freeze/.gitignore:1-2`
  - `git check-ignore -v` results:
    - `.claude/skills/freeze/check-freeze.sh` → not ignored
    - `.claude/skills/context-budget/scan.sh` → not ignored
    - `.claude/rules/environment-failures.md` → not ignored
    - `docs/context-budget-baseline.md` → not ignored
    - `docs/reviews/codex-adversarial-tier1-adoption-2026-04-26.md` → not ignored
    - `.claude/tmp/freeze/freeze-dir.txt` → intentionally ignored by `.claude/tmp/freeze/.gitignore:1`
    - `.claude/logs/test.log` → intentionally ignored by `.gitignore:22`
    - `Main/obj/test.txt` → ignored by `.gitignore:3`
    - `Main/bin/test.txt` → ignored by `.gitignore:2`
- Result: I found no remaining accidental ignore on shipped `.claude/` or `docs/` artifacts in this changeset.

5. **CLAUDE.md "PROACTIVE INVOKE" CLAIMS** — **DISPUTED, with one nuance**
- Evidence: `CLAUDE.md:80-123`
- Verification method: official skills docs.
- Result:
  - Official docs say Claude can automatically load/invoke skills when relevant unless `disable-model-invocation: true` is set: https://code.claude.com/docs/en/skills
  - So "proactively invoke" is a real capability, not just user-only `/skill-name` dispatch.
  - Nuance: this is model behavior, not a deterministic router. The table is guidance, not a guarantee.
  - The confidence gates are written in language the model can evaluate at decision time; I did not find a concrete contradiction there.

6. **ENVIRONMENT-FAILURES PATHS GLOB** — **CONFIRMED**
- Evidence: `.claude/rules/environment-failures.md:1-4`, `CLAUDE.md:139`, `CHANGELOG.md:18`
- Verification method: official memory/rules docs.
- Result: the problem is not whether `**/*` is a legal glob. The problem is that **any** `paths:` field makes the rule conditional. Official docs say rules without `paths` load at launch; path-scoped rules load only when matching files are opened: https://code.claude.com/docs/en/memory

7. **SCAN.SH ACCURACY GAPS** — **CONFIRMED**
- Evidence:
  - `.claude/skills/context-budget/scan.sh:100-123`, `:168-257`, `:281-325`
  - `docs/context-budget-baseline.md:42-48`, `:67-80`
- Verification method: official skills, memory, and context-window docs plus one server-doc spot check.
- Result:
  - The biggest gap is unasked-but-critical: `scan.sh` counts full `SKILL.md` bodies as startup baseline, while official docs say skill descriptions load at startup and full skill content loads only when invoked.
  - The baseline doc already explains that system prompt boilerplate is omitted (`docs/context-budget-baseline.md:44-48`), so that sub-point is fine.
  - The MCP tool counts are heuristic and already acknowledged as estimates, but the hardcoded map is inaccurate for at least one server: the official filesystem MCP server docs list at least 14 tools, while `scan.sh` hardcodes `filesystem=12`.
  - The `200 tokens per server` constant is undocumented in the repo.
  - Auto memory is omitted even though official docs say `MEMORY.md` is loaded at the start of every conversation.

8. **SKILL CROSS-REFERENCE COHERENCE** — **CONFIRMED PARTIAL BREAK**
- Evidence:
  - `CLAUDE.md:90`, `:106-109`
  - `.claude/skills/new-feature/SKILL.md:44-46`
  - `.claude/skills/build-fix/SKILL.md:56-63`
  - `.claude/skills/deep-review/SKILL.md:386-390`
  - `.claude/agents/feature-builder.md:72-86`
- Verification method: repo trace plus official hooks lifecycle docs.
- Result:
  - `/new-feature` → suggest `/freeze`: coherent.
  - `/build-fix` retry-budget → `/investigate`: coherent.
  - `/deep-review` fix-loop → suggest `/freeze`: coherent.
  - `feature-builder` is **not** coherent: it says writing the state file directly will cause `/freeze` hooks to block edits, but skill hooks are only active while `/freeze` is active. Without actually invoking `/freeze`, writing `freeze-dir.txt` does nothing.

## C. CROSS-REFERENCE AUDIT

| Check | Result |
|------|--------|
| CLAUDE skill list vs real skill dirs | 18 mentioned, 18 real dirs, no missing skills |
| Routing-table skill mentions vs real skill dirs | All mentioned skills resolve to `.claude/skills/<name>/SKILL.md` |
| Hook command path in `/freeze` | `.claude/skills/freeze/check-freeze.sh` exists, tracked by `git ls-files`, not ignored by `git check-ignore` |
| Hook command path in `/investigate` | Same path exists, tracked, not ignored |
| `environment-failures.md` reference in scoped-rules table | Name matches file; description matches intent; "always-load" claim does **not** match current semantics |
| Freeze state file ignore status | Intentionally ignored by `.claude/tmp/freeze/.gitignore:1` |
| Other likely generic-ignore collisions in shipped files | None found in `.claude/` or `docs/` after `git check-ignore -v` |

Resolved skill set:
`/build-fix`, `/codex-verify`, `/commit-split`, `/context-budget`, `/deep-review`, `/deslop`, `/freeze`, `/investigate`, `/issue`, `/migration-status`, `/new-adr`, `/new-feature`, `/research`, `/review-codex`, `/scope-check`, `/unfreeze`, `/verify`, `/xslt-check`

## D. UPSTREAM DRIFT

### gstack (`/freeze`, `/investigate`)

Material drift observed:
- TAOM intentionally removed gstack telemetry/state under `~/.gstack/` and made freeze state project-local in `.claude/tmp/freeze/`. That is a reasonable simplification.
- TAOM kept the inline hook pattern from gstack. Current Claude Code docs now support it, so this drift is acceptable.
- TAOM kept `triggers:` from gstack, but current Claude Code docs do not document that field. In Claude Code, `when_to_use` would be the documented equivalent.
- TAOM simplified `/investigate` from gstack's broader ecosystem workflow into a TAOM-specific Bannerlord debugging workflow. That is intentional and mostly coherent.
- TAOM dropped gstack's explicit hook-status messaging/telemetry. No functional loss by itself.

Net: no material functionality loss in `/freeze` or `/investigate` from the port itself; the real problems are TAOM-local claims around hook lifecycle and unsupported `triggers:`.

### everything-claude-code (`/context-budget`)

Material drift observed:
- TAOM reduced the upstream skill to a smaller repo-local shell scanner. That simplification lost important methodological nuance.
- Upstream descriptions emphasize auditing loaded components and worst-case overhead separately. TAOM's `scan.sh` conflates them by charging full `SKILL.md` bodies to startup baseline.
- TAOM's scanner does not account for auto memory, despite current Claude Code docs making that startup-loaded.
- TAOM hardcodes MCP tool counts and a per-server overhead without local justification.

Net: the port kept the "audit context budget" idea but lost accuracy. This is the most serious drift in the changeset.

## E. FINDINGS OR OBSERVATIONS

### HIGH

1. `.claude/skills/context-budget/scan.sh:100-123`, `.claude/skills/context-budget/scan.sh:281-325`, `docs/context-budget-baseline.md:67-80` — Baseline methodology is wrong — The scanner counts full `SKILL.md` bodies as startup overhead, but current Claude Code loads skill descriptions at startup and full skill content only when invoked — This inflates the published 75,906-token "baseline" and undermines the decision to defer context work — Fix: count startup-visible skill metadata only (`name` + `description` + `when_to_use`), or relabel the current number as worst-case/after-invocation instead of baseline.

2. `.claude/agents/feature-builder.md:78-86` — Scope-lock claim is non-functional — The agent writes `freeze-dir.txt` directly and claims "`/freeze` PreToolUse hooks will then block", but official hook lifecycle semantics say skill hooks run only while that skill is active. If `feature-builder` writes the state file without actually invoking `/freeze`, nothing is enforcing the boundary — This gives a false safety guarantee during feature work — Fix: require explicit `/freeze` invocation, or move the freeze check into a global/project hook in `.claude/settings.json` if state-file-driven locking is meant to work outside the `/freeze` skill.

### MEDIUM

1. `.claude/rules/environment-failures.md:1-4`, `CLAUDE.md:139`, `CHANGELOG.md:18` — "Always-load via `**/*`" is incorrect — Path-scoped rules are conditional; only rules without `paths` load at launch — The "report, don't fix infra" rule can be absent until a file matching the glob is read — Fix: remove the `paths:` frontmatter entirely if this rule is meant to be unconditional.

2. `.claude/skills/freeze/check-freeze.sh:28` — Freeze boundary path is whitespace-destructive — `tr -d '[:space:]'` removes legitimate spaces from the saved directory path — A boundary like `C:/Users/Mike W/source/repos/TAOM` becomes invalid and matching behavior becomes unpredictable — Fix: read the line verbatim with `IFS= read -r FREEZE_DIR < "$FREEZE_FILE"` and strip only trailing CR/LF if needed.

3. `docs/context-budget-baseline.md:44-48` — Auto memory is omitted from "what's not counted" even though it is startup-loaded — Current Claude Code loads the first 200 lines or 25KB of `MEMORY.md` at the start of every conversation — The published baseline understates real startup overhead even after fixing the skill-body overcount — Fix: include `~/.claude/projects/<project>/memory/MEMORY.md` in the scan, or explicitly label the omission as a known undercount.

### LOW

1. `.claude/skills/freeze/SKILL.md:8-13`, `.claude/skills/investigate/SKILL.md:13-19`, `CHANGELOG.md:32`, `docs/context-budget-baseline.md:67-80` — `triggers:` is undocumented in current Claude Code — I found no current Claude Code doc showing this frontmatter is consumed; current docs point authors to `description` and `when_to_use` instead — Fix: move these phrases into `when_to_use` or the description and drop `triggers:` unless you verify live behavior.

2. `.claude/skills/context-budget/scan.sh:220-252`, `docs/context-budget-baseline.md:15`, `:75-80` — MCP heuristics are under-justified and at least one hardcoded count is stale — The scanner hardcodes `filesystem=12`, but the official filesystem MCP server docs list at least 14 tools. The `200 tokens/server` constant is also unexplained in-repo — Fix: either document the source of each estimate, or label MCP counts as rough heuristics and lower the precision of the reported totals.

No other accidental ignore/exclude problems found in the shipped `.claude/` and `docs/` artifacts.
