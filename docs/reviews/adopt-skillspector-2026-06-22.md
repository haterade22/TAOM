# Adoption review — NVIDIA/SkillSpector

- **Date:** 2026-06-22
- **Source:** https://github.com/NVIDIA/SkillSpector
- **License:** Apache-2.0 (the bundled `.yar` files: DRL-1.1 / unstated — see below)
- **Procedure:** [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)
- **Outcome:** SELECTIVE adoption (Tier 1 = port deterministic patterns + AST + clean-room YARA into `tools/audit_claude_config.py`; Tier 2 = document SkillSpector as the heavyweight static-only external-vet tool). LangGraph/LLM runtime NOT installed.

## What it is

NVIDIA's "Security scanner for AI agent skills" — a Python 3.12+ LangGraph agent that scans a skill directory for 64 vulnerability patterns across 16 categories and reports Terminal/JSON/Markdown/SARIF. Apache-2.0, 9.3k stars, actively maintained (pushed 2026-06-16). Three analyzer layers:

- **Deterministic (portable):** 11 `static_patterns_*.py` (pure `re`, no LLM/network — verified by reading the source), `static_yara.py` + 4 `.yar` files, `behavioral_ast.py` (stdlib `ast`) + `behavioral_taint_tracking.py`.
- **LLM-backed (not portable):** 3 `semantic_*.py` analyzers + `providers/{anthropic,openai,nv_build}` + `graph.py` orchestration. The "Stage 2" pass; optional; raises precision to ~87%; needs an API key; sends scanned content to a provider.
- **Network:** `osv_client.py` (live OSV.dev CVE lookups); `langsmith` dependency (tracing phone-home only if env-configured).

Grounded in "Agent Skills in the Wild: An Empirical Study of Security Vulnerabilities at Scale" (Liu et al., 2026; 42,447 skills, 26.1% with ≥1 vulnerability, 5.2% likely-malicious). That statistic is the project's own citation; we did not independently read the paper.

## Security vet (gating) — verdict two ways

Install surface read verbatim: `Makefile` (`make install` = `uv sync` / `pip install -e .`), `pyproject.toml` (Hatchling build backend, standard data-file inclusion — no custom build hooks), `.pre-commit-config.yaml`. No `postinstall`/`preinstall` hooks, no `curl | bash`, no remote script execution, no `eval`/base64-decode-then-run, no obfuscation. Dependencies are mainstream (typer, rich, httpx, pydantic, pyyaml, yara-python, openai, langgraph, langchain-\*, langsmith).

- **Safe to LEARN FROM (read/port patterns): YES.** The deterministic regex, AST, and YARA-indicator logic is Apache-2.0 text we can port.
- **Safe to INSTALL/RUN: "yes, but" — never into TAOM's harness.** Running it pulls a heavy langchain/langgraph dependency tree, optionally needs an LLM API key, the LLM stage sends scanned content to an external provider, and `osv_client` makes outbound calls. Per TAOM's port-never-install rule it is not installed into the Claude config. If used as a standalone vet tool, run **static-only** (`skillspector scan`, no key — confirmed by the README: "Static-only scanning requires no credentials") in an **isolated venv/Docker** against a *foreign* tree.

### `.yar` license finding (gated the YARA decision)

All four bundled rule files (`webshells.yar`, `malware.yar`, `cryptominers.yar`, `hacktools.yar`) are derived from **Neo23x0/signature-base**. `webshells.yar` declares **DRL-1.1** (Detection Rule License — free non-commercial use *with attribution*, no commercial resale; a non-standard license); the other three carry only a "based on patterns from Neo23x0/signature-base" comment with **no explicit license header**. SkillSpector's own `THIRD_PARTY_NOTICES.md` lists the `yara-python` *library* but never the `.yar` *content*. Verdict: **do not vendor.** TAOM authored a **clean-room** ruleset (`tools/yara_rules/taom_skill_signatures.yar`) covering the same publicly-documented IOC categories (tool names, protocol strings, syscall shapes — facts, not copyrightable expression), carrying TAOM's own copyright.

## Novel vs duplicative (vs TAOM's `tools/audit_claude_config.py`)

TAOM already had 5 stdlib categories: `secrets`, `permissions`, `hook-exfil`, `mcp-risk`, `prompt-injection`.

| SkillSpector category | TAOM disposition |
|---|---|
| Prompt Injection / Output Handling / Data Exfiltration / Privilege Escalation / Supply Chain | Partly DUPLICATIVE — already covered by the 5 existing categories; not re-ported (output-handling's *code-sink* half was ported as new) |
| Excessive Agency, Memory Poisoning, System Prompt Leakage, Tool Misuse, Rogue Agent, Output Handling (code sinks) | **PORTED** — 6 new deterministic categories TAOM lacked |
| Behavioral AST | **PORTED** — stdlib `ast`, no new dependency |
| YARA Signatures | **PORTED as clean-room** — import-guarded `yara` layer; TAOM-authored `.yar` (license, above) |
| MCP Least Privilege / Tool Poisoning | Deterministic core covered by the ported `agency-wildcard-tools` (LP2) + TAOM's existing zero-width check (TP2); no separate scanner |
| Trigger Abuse | **SKIPPED** — no deterministic analyzer exists upstream (it's the LLM `semantic_quality_policy`); duplicative of TAOM's `/skill-stocktake` + ≤30-word description discipline |
| Taint Tracking | **SKIPPED v1** — complex multi-file dataflow, low ROI for TAOM's small script surface |
| LangGraph runtime, `semantic_*` LLM analyzers, providers, langsmith | **SKIPPED** — not portable, duplicative of Claude + Codex for semantic review, context/dependency tax |

## What shipped

- `tools/audit_claude_config.py` — 6 new regex categories (`excessive-agency`, `memory-poisoning`, `prompt-leakage`, `tool-misuse`, `rogue-agent`, `output-handling`), a stdlib-`ast` scan (`ast-exec`, with an `exec`-wrapping-decoded-source chain = CRITICAL), and an import-guarded YARA layer. Apache-2.0 attribution preserved in the module docstring + section comment.
- `tools/yara_rules/taom_skill_signatures.yar` — 7 clean-room rules (reverse-shell, webshell, C2, info-stealer, cryptominer, backdoor-persistence, hacktool).
- `tools/tests/test_audit_skillspector.py` — 25 tests (regex categories, AST incl. chain=CRITICAL, calibration, YARA compile+match, layer guard). AV-safe: analyzers driven in-memory, YARA samples fragment-assembled.
- Docs: `.claude/skills/security-scan/SKILL.md`, `docs/ai-includes/external-repo-adoption.md`, `.claude/skills/adopt-external/SKILL.md`, this review, `CHANGELOG.md`.

## Calibration (the load-bearing design decision)

The ported regex categories are **advisory (INFO) on a TAOM self-audit** and **full severity under `--external`**. Reason: TAOM's own config legitimately references attack patterns — the hook that *blocks* `--no-verify`, docs quoting "from now on, always…" as examples. A naive port flagged 4 MED false positives on TAOM's own safety hooks; routing the self-audit to INFO (with a documentation-example line-skip) drops the self-scan to zero new non-INFO findings while `--external` stays loud for the "skills from the internet aren't harmless" use case. The genuine teeth — `hook-exfil`, the AST chain, and YARA — fire at full severity regardless of `--external`. The auditor stays stdlib-only; YARA is the one optional layer and is import-guarded so a missing `yara-python` never blocks the CI gate.

## Verification

- `python -m unittest discover -s tools/tests` — 166 tests pass (25 new).
- Self-audit (`python tools/audit_claude_config.py`): exit 0, zero new non-INFO findings (the one MED is the pre-existing `mcp-npx-unpinned`).
- `--external` scan of a synthetic malicious skill: fires CRITICAL (AST exec-chain) + HIGH across excessive-agency/memory-poisoning/prompt-leakage/tool-misuse/output-handling; exit 2.
- All 7 clean-room YARA rules verified matching in-memory (after fixing YARA's no-`(?:...)` regex constraint).

## Notes / known limitations

- **Windows Defender quarantines on-disk webshell/malware signature files** before the scanner can read them; the auditor fail-opens on unreadable files (correct), and the tests therefore drive YARA in-memory. A real foreign-repo scan on a machine with aggressive AV may see some signature files skipped — the heavyweight SkillSpector path (or disabling AV in a sandbox) is the fallback.
- `yara-python` was `pip install`ed locally to verify the layer; it is optional and not added to any TAOM requirements manifest.
- `CLAUDE.md` was left unedited (the new `tools/yara_rules/` path + auditor categories warrant a one-line note, but `CLAUDE.md`/`AGENTS.md` are shared files pushed in the user's batch per the adoption convention + config-protection hook).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
