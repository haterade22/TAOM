---
name: adopt-external
description: Use when reviewing an external repo or article to adopt practices into TAOM. Security-vet first, map novel vs duplicative, port (never install), review.
allowed-tools: Read, Write, Edit, Bash, WebFetch, WebSearch
---

# Adopt External Repo / Article

Evaluate an external repo or article and fold the genuinely-useful parts into TAOM — without importing bloat or risk. Thin entry point; the full procedure, principles, and accumulated gotchas live in **[docs/ai-includes/external-repo-adoption.md](../../../docs/ai-includes/external-repo-adoption.md)** — read it first.

## Trigger

User shares a repo/article "to see what we can adopt." One source at a time. A repo listed in `docs/reviews/upstream-watch.json` (yotthani's) goes through `/upstream-review` instead, which reviews only the delta and advances its reviewed SHA.

## The cycle

Nine phases — **read the full procedure in the doc before starting**: Identify → Security-pass-FIRST → Map novel-vs-duplicative → Tiered recommendation → Implement (port, never install) → Adversarial review → Fix + RCA → Commit MINE only → Push.

See **[external-repo-adoption.md § The cycle](../../../docs/ai-includes/external-repo-adoption.md)** for each phase in detail. (This skill stays thin and points to the doc rather than restating it — per `external-skill-ports.md` "Don't amplify a rule — point to it".)

## Top gotchas

- Never install: port text.
- Name an adopted feature in Tolkien diction, never the source mod's name, for its folder and MCM group too. Grep `Main/**/*.cs`, `docs/features` and ModuleData strings for clashes first (Mike, 2026-10-18).
- **Vet a foreign *skill* automatically** during the security pass: `python tools/audit_claude_config.py --root <repo> --external` fires TAOM's SkillSpector-derived categories at full severity. Heavyweight option: NVIDIA SkillSpector run static-only in an isolated venv/Docker (never installed). It supplements, never replaces, the manual read.
- After porting config/hooks, run **`/security-scan`** on our own result.

## Pair with

- `/security-scan` — runs at the end of the implement phase.
- `/deep-review` on C# changes; `/review-codex` only on the maintainer's word (paid).
- `external-skill-ports.md`, `evidence-over-claims.md`, `simplicity-criterion.md` — the rules this cycle leans on.
