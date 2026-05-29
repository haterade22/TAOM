---
name: adopt-external
description: Use when reviewing an external repo or article to adopt practices into TAOM — security-vet first, map novel vs duplicative, port (never install), review, commit your changes.
allowed-tools: Read, Write, Edit, Bash, WebFetch, WebSearch
---

# Adopt External Repo / Article

Evaluate an external repo or article and fold the genuinely-useful parts into TAOM — without importing bloat or risk. Thin entry point; the full procedure, principles, and accumulated gotchas live in **[docs/ai-includes/external-repo-adoption.md](../../../docs/ai-includes/external-repo-adoption.md)** — read it first.

## Trigger

User shares a repo/article "to see what we can adopt." One source at a time.

## The cycle

Nine phases — **read the full procedure in the doc before starting**: Identify → Security-pass-FIRST → Map novel-vs-duplicative → Tiered recommendation → Implement (port, never install) → Adversarial review → Fix + RCA → Commit MINE only → Push.

See **[external-repo-adoption.md § The cycle](../../../docs/ai-includes/external-repo-adoption.md)** for each phase in detail. (This skill stays thin and points to the doc rather than restating it — per `external-skill-ports.md` "Don't amplify a rule — point to it".)

## Top gotchas

- **Never install the external plugin** — it's prompt-injection-by-design (SessionStart injects high-authority context) + context tax + conflicts with our curated setup. Port text instead.
- **Calibrate, don't blind-port** — a rule correct upstream can be wrong here (e.g. TAOM mandates fail-open hooks).
- **Most of a general operator repo is irrelevant or duplicative** for a C#/.NET Bannerlord mod. Be honest and critical.
- **Right-size the fan-out** — if the README makes the verdict obvious (clearly out-of-domain → skip), do a light inline pass; don't spin up a multi-agent workflow to confirm the obvious.
- **Verify load-bearing security claims yourself** before relaying a subagent's read — a subagent verdict is a hypothesis (`evidence-over-claims.md` A.4).
- After porting config/hooks, run **`/security-scan`** on our own result.
- When DESCRIBING security patterns in the skill/docs you author, avoid embedding literal trigger strings (they can self-flag `/security-scan`) — describe the category or use `audit-allow:`.

## Pair with

- `/security-scan` — runs at the end of the implement phase.
- `/deep-review`, `/review-codex` — for the adversarial-review phase on C# changes.
- `external-skill-ports.md`, `evidence-over-claims.md`, `simplicity-criterion.md` — the rules this cycle leans on.
