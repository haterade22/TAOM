---
name: security-scan
description: Use when auditing TAOM's Claude Code config for security issues — committed secrets, over-broad permissions, hook exfiltration, MCP risk, hidden-unicode injection. Runs tools/audit_claude_config.py.
allowed-tools: Bash, Read
---

# Security Scan — audit TAOM's own Claude config

A deterministic, offline security scan of TAOM's Claude Code configuration surface (`.claude/`, `.mcp.json`, `settings*.json`, `CLAUDE.md`, `AGENTS.md`). Ported as a TAOM-calibrated subset of obra-style "scan your own agent config" tooling (affaan-m/ECC AgentShield), 2026-05-29. See `docs/reviews/` for the review that motivated it.

## When to use

- Before a release / `/ship`, or periodically alongside `/skill-stocktake`.
- After adding or editing a hook, an MCP server, a permission grant, or a config file.
- Whenever you've pulled in external config (the `/adopt-external` flow ends here).

## How to run

```bash
python tools/audit_claude_config.py            # human report, all severities
python tools/audit_claude_config.py --min HIGH # only HIGH+CRITICAL
python tools/audit_claude_config.py --json      # machine output (CI)
```

Exit code: **2** if any CRITICAL finding (CI gate), 1 on usage error, 0 otherwise.

## What it checks (5 deterministic categories)

| Category | Catches |
|----------|---------|
| `secrets` | committed API keys / tokens / private keys / DB URIs (placeholder-suppressed) |
| `permissions` | over-broad grants in `settings*.json` allow-lists (`Bash(*)`, `Write(*)`, permission-bypass flags) |
| `hook-exfil` | hook scripts that phone home, open reverse shells, read credential stores, or tamper logs |
| `mcp-risk` | hardcoded secrets in MCP `env`, auto-approve of project servers, unpinned `npx -y` |
| `prompt-injection` | hidden zero-width / bidi unicode, override directives, download-then-run instructions in model-facing files |

## TAOM calibration (important)

This is **not** a blind port of the upstream ruleset. TAOM *mandates* fail-open hooks (`|| true`, `2>/dev/null`, always `exit 0`) per `.claude/rules/harness-facts.md`, so that pattern is deliberately NOT flagged here (it is upstream). When porting any future rule, calibrate it to TAOM's conventions first.

## Reading findings

- **CRITICAL / HIGH** — fix before commit. A committed secret means rotate the credential, then purge it.
- **MED / LOW** — review; many are advisory (e.g. unpinned `npx -y` on an MCP server is a real-but-low risk).
- **INFO** — injection-style phrases found in `docs/` or `.claude/rules/` are dampened to INFO because security docs legitimately quote them as examples.

## Suppressing a known-safe match

Put `audit-allow: <rule-id>` in a comment on the SAME line (e.g. when a doc must quote a trigger phrase as an example). Suppressions are reported in the summary, so they stay visible.

## Gotchas

- The scan is read-only and makes no network calls.
- Run from the repo root (it resolves `.claude/`, `.mcp.json` etc. relative to `--root`, default cwd).
- It does NOT replace `/deep-review` or `/review-codex` — it audits *configuration*, not feature code.
