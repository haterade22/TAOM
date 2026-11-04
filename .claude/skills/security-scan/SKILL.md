---
name: security-scan
description: Use when auditing TAOM's Claude Code config for security issues — committed secrets, over-broad permissions, hook exfiltration, MCP risk, hidden-unicode injection. Runs tools/audit_claude_config.py.
allowed-tools: Bash, Read
---

# Security Scan — audit TAOM's own Claude config

A deterministic, offline security scan of TAOM's Claude Code configuration surface (`.claude/`, `.mcp.json`, `settings*.json`, `CLAUDE.md`, `AGENTS.md`), plus a repo-wide secret sweep over every git-tracked file. Ported as a TAOM-calibrated subset of obra-style "scan your own agent config" tooling (affaan-m/ECC AgentShield), 2026-05-29; the skill-threat categories + AST + YARA layers were added from NVIDIA SkillSpector (2026-06-22, `docs/reviews/adopt-skillspector-2026-06-22.md`). See `docs/reviews/` for the reviews that motivated it.

## When to use

- After editing hooks, settings, MCP config or CLAUDE.md (the CLAUDE.md skill row).
- Whenever you've pulled in external config (the `/adopt-external` flow ends here).
- **To vet a foreign skill before adopting it** — point `--root` at it and pass `--external` (see below).

## How to run

```bash
python tools/audit_claude_config.py            # human report, all severities
python tools/audit_claude_config.py --min HIGH # only HIGH+CRITICAL
python tools/audit_claude_config.py --json      # machine output
python tools/audit_claude_config.py --root <dir> --external   # vet an untrusted/foreign tree
python tools/audit_claude_config.py --no-repo-secrets           # config surface only (faster)
python tools/audit_claude_config.py --user                      # this machine's user scope
```

**User scope (`--user`).** Machine-local, so its results are reported, never committed: `~/.claude/settings*.json`,
user skills, agents, rules, hooks and commands, every `MEMORY.md`, the MCP servers in `~/.claude.json`
(top level and per project; only those server maps are scanned, and nothing else from the file is
printed), and every installed plugin's folder, once, vetted at `--external` severity (any project can
enable a plugin; TAOM's settings list eight, all off). A plugin your user settings enable but whose folder
is gone is `user-plugin-missing`; an absent or unreadable `~/.claude.json` is `user-config-absent`.
`--external` vets the whole foreign tree (a plugin repo, a marketplace, one skill folder) except `.git`
and `node_modules`; exit **3** (`UNCHECKED`) means no config was found to scan, which is not a pass.

**Two scopes, one run.** Every layer runs over the config surface (about 110 files). The `secrets` rules additionally run over every git-tracked text file under 2 MB (about 4,200 files, roughly 2.5s), because a credential is far more likely to be pasted into a `.ps1`, a C# const, or a doc than into `settings.json`. The other layers stay scoped to the config surface on purpose: they are calibrated for it, and turning `excessive-agency` loose on 4,000 game-data files would bury a real finding in advisory noise. The sweep needs a git work tree; without one it reports an INFO note instead of silently passing.

Exit code: **2** if any CRITICAL finding, 3 if nothing was collected (`UNCHECKED`), 1 on usage error, 0 otherwise.

**Optional dependency:** `pip install yara-python` enables the clean-room YARA signature layer (webshell / malware / C2 / cryptominer / hacktool IOCs). Without it the scan still runs and the layer is skipped with an INFO note — the auditor is never blocked by a missing dep.

## What it checks

The original 5 categories (stdlib-only):

| Category | Catches |
|----------|---------|
| `secrets` | committed API keys / tokens / private keys / DB URIs (placeholder-suppressed), in the config surface **and in every git-tracked file**; plus `secret-tracked-key` / `-env` / `-cred` for a tracked file that IS credential material by name (`*.pem`, `id_rsa`, `.env`, `.npmrc`), which `.gitignore` is powerless to prevent once the file is committed |
| `permissions` | over-broad grants in `settings*.json` allow-lists (`Bash(*)`, `Write(*)`, permission-bypass flags) |
| `hook-exfil` | hook scripts that phone home, open reverse shells, read credential stores, or tamper logs |
| `mcp-risk` | hardcoded secrets in MCP `env`, auto-approve of project servers, unpinned `npx -y` |
| `prompt-injection` | hidden zero-width / bidi unicode, override directives, download-then-run instructions in model-facing files |

The SkillSpector-derived layers (deterministic; advisory on a self-audit, full severity under `--external`):

| Category | Catches |
|----------|---------|
| `excessive-agency` | wildcard tool grants (`tools: ["*"]`), skip-confirmation, auto-approve, unbounded retries/resource use |
| `memory-poisoning` | persistent-context injection ("from now on, always…"), inject-false-memories, memory wipe/poison, identity rewrite |
| `prompt-leakage` | reveal/encode/exfiltrate the system prompt or instructions |
| `tool-misuse` | `shell=True`, `rm -rf /`, `--no-verify`, world-writable perms, TLS verification off |
| `rogue-agent` | self-modification, disable-safety-at-runtime, cron / shell-rc persistence |
| `output-handling` | model output piped to `exec`/`eval`/`innerHTML`/SQL; unbounded output |
| `ast-exec` | (`.py`) `exec`/`eval`/`compile`/`__import__`/`os`-exec/`subprocess`/dynamic-`getattr`; `exec`-wrapping-decoded-source chain = CRITICAL |
| `yara-*` | (optional) clean-room webshell / malware / C2 / cryptominer / hacktool signatures in any scanned file |

## TAOM calibration (important)

This is **not** a blind port of either upstream ruleset.

- TAOM *mandates* fail-open hooks (`|| true`, `2>/dev/null`, always `exit 0`) per `.claude/rules/harness-facts.md`, so that pattern is deliberately NOT flagged (it is upstream in AgentShield).
- The SkillSpector regex categories are **advisory (INFO) on a TAOM self-audit** because TAOM's own hooks and docs legitimately reference attack patterns (the hook that *blocks* `--no-verify`; a doc quoting "from now on, always…" as an example). Pass `--external` to raise them to full severity when vetting an untrusted tree. The genuine code/IOC teeth — `hook-exfil`, the AST `exec`-chain (CRITICAL), and YARA — fire at full severity regardless of `--external`.
- The YARA rules in `tools/yara_rules/` are **TAOM clean-room originals**, NOT the DRL-1.1 / unlicensed Neo23x0-derived `.yar` files bundled with SkillSpector (those were deliberately not vendored).

When porting any future rule, calibrate it to TAOM's conventions first.

## Vetting a foreign skill (the "skills from the internet aren't harmless" path)

For a one-off vet of a skill you're about to adopt, point the auditor at it and pass `--external` so the SkillSpector categories fire loudly:

```bash
python tools/audit_claude_config.py --root /path/to/foreign-skill --external
```

For deeper coverage (LLM intent analysis, taint tracking, OSV CVE lookups) the heavyweight option is NVIDIA SkillSpector itself, run **static-only** (`skillspector scan <dir>`, no API key) in an **isolated venv/Docker** — never installed into TAOM's harness. See `docs/ai-includes/external-repo-adoption.md`.

## Git history (Gitleaks)

The sweep above reads the current tree only; a secret committed and later deleted lives on in
history. Each `/security-scan` run (see "When to use") scans the history too
(about 100 s for 2,700 commits, so give the Bash call a 300 s timeout; offline, values redacted):

```bash
root=$(git rev-parse --show-toplevel) && "${TAOM_GITLEAKS:-E:/Tools/gitleaks-8.30.1/gitleaks.exe}" git --redact --no-banner --exit-code 2 --gitleaks-ignore-path "$root/.gitleaksignore" "$root"
```

Gitleaks 8.30.1 sits on the desktop at `E:\Tools\gitleaks-8.30.1` (release zip checked against its
SHA-256). Read the summary before the exit code:

- `N commits scanned` must be close to `git rev-list --all --no-merges --count` (2,746 of 2,777 on
  2026-10-26: Gitleaks skips merges and a few others). Gitleaks reports
  `0 commits scanned` and `no leaks found` with exit 0 when git fails, which is a failed scan.
- Exit 2 is a new finding: read the redacted match and the line with `git show <commit>:<file>`.
  A real secret is rotated, then purged. A false positive goes into `.gitleaksignore` as its
  fingerprint, under the header's rule.
- Exit 1 with an `FTL` line means Gitleaks itself failed.

The baseline (#1020, 2026-10-26): 19 false positives, no real secret.

## Reading findings

- **CRITICAL / HIGH** — fix before commit. A committed secret means rotate the credential, then purge it.
- **MED / LOW** — review; many are advisory (e.g. unpinned `npx -y` on an MCP server is a real-but-low risk).
- **INFO** — injection-style phrases in `docs/`/`.claude/rules/`, and ALL SkillSpector regex-category matches on a self-audit, are dampened to INFO (security docs legitimately quote attack phrases). Also where the `yara-unavailable` note appears when `yara-python` isn't installed.

## Suppressing a known-safe match

Put `audit-allow: <rule-id>` in a comment on the SAME line (e.g. when a doc must quote a trigger phrase as an example). Suppressions are reported in the summary, so they stay visible.

## Gotchas

- The scan is read-only and makes no network calls.
- Run from the repo root (it resolves `.claude/`, `.mcp.json` etc. relative to `--root`, default cwd).
- It does NOT replace `/deep-review` or `/review-codex` — it audits *configuration*, not feature code.
