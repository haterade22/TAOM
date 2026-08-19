# Adoption review — mattpocock/skills `teach` + `handoff`

- **Date:** 2026-06-22
- **Source:** https://github.com/mattpocock/skills/tree/main/skills/productivity/{teach,handoff}
- **License:** MIT
- **Procedure:** [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)
- **Outcome:** `teach` — SKIP (out-of-domain). `handoff` — SELECTIVE (Tier 1): one field folded into `/context-save`; the rest is duplicative.

Two sibling skills from mattpocock's `skills` repo (MIT, 141k stars), reviewed together as a batch. Both passed the `--external` foreign-skill vet with no findings (6 files scanned); both are markdown-only, no scripts/hooks, `disable-model-invocation: true`.

## `teach` — SKIP (out-of-domain)

**What it is:** a stateful, multi-session **personal-learning** framework. Builds a teaching workspace — `MISSION.md` (why you're learning), `RESOURCES.md` (trusted sources), `NOTES.md` (preferences), `lessons/*.html` (Tufte-style HTML lessons), `learning-records/*.md` (ADR-shaped insights), `reference/*.html` (cheat sheets), `assets/*` — grounded in learning science (fluency vs storage strength, zone of proximal development, spacing/interleaving/retrieval). 5 markdown files (SKILL.md + 4 FORMAT companions). The user is the *learner*; Claude is the teacher.

**Why skip:** wrong audience and domain. TAOM is a Bannerlord total-conversion mod; the only "teaching" in its workflow is *agent-facing* documentation — the knowledge base whose job is to stop future Claude sessions re-analyzing solved problems. That need is already served by ADRs, RCAs, feedback-memories, the engine-study docs (`docs/reference/engine/`), and the `docs/ai-includes/*` authoring guides. `teach`'s human-learner HTML-lesson + spaced-repetition model has no transferable kernel for that. Clean out-of-domain skip — a light inline pass was decisive without a multi-agent workflow.

## `handoff` — Tier 1 (one field into `/context-save`)

**What it is (full skill, 878 bytes):** summarize the current conversation into a handoff doc for a fresh agent; save to OS temp; include a "suggested skills" section; reference artifacts (PRDs/plans/ADRs/issues/diffs) by path instead of duplicating; redact secrets; tailor to a user-supplied "what's the next session for."

**Map vs TAOM `/context-save`:**

| handoff idea | TAOM `/context-save` | Disposition |
|---|---|---|
| Summarize session → doc for the next agent | Core function, richer (git state + decisions-with-why + blockers + files-in-flight + next step + "what surprised you") | Duplicative |
| Reference artifacts by path, don't duplicate | Already links the issue + names files | Duplicative |
| Save location | `.claude/state/context/` (in-repo, gitignored, persists; paired with `/context-restore`) **>** handoff's OS temp (ephemeral) | TAOM's is better — kept |
| **"Suggested skills" section** | Had "Next concrete step", no explicit suggested-skills field | **NOVEL — adopted** |
| Tailor to "what's the next session for" | Takes a slug descriptor; doesn't tailor content | Minor — not adopted |
| Explicit secret redaction | No explicit note (low risk: local + gitignored) | Minor — adopted (cheap) |

**What shipped:** `.claude/skills/context-save/SKILL.md` gains a **"Suggested skills for the next session"** field (capture list + output-format example) — given TAOM's large skill catalog + routing tables, naming the entry points ("`/context-restore`, then `/verify`, then `/deep-review`") saves the resuming session re-deriving the route — plus a one-line **secret-redaction** note. No new skill; handoff's OS-temp storage and standalone existence were not adopted (`/context-save` + `/context-restore` already own this, better).

## Why not more

Same pattern as the `improve-codebase-architecture` review earlier today: the bulk is machinery TAOM already has, often in a better-fitting form (`/context-save`'s persisted in-repo snapshot vs handoff's ephemeral temp file). The honest "do more effectively" answer was a single output-format field, not a new skill.

## Verification

- `--external` foreign-skill vet over all 6 files: no findings.
- `python tools/audit_claude_config.py` self-audit after the `/context-save` edit: exit 0, no new findings.
- Documentation-only change (one skill body); no code/tests affected.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
