# External Repo / Article Adoption

How TAOM evaluates an external repo or article and folds the genuinely-useful parts into our own setup — without importing bloat or risk. This is the executable procedure behind the `/adopt-external` skill. Precedents: [obra/superpowers](https://github.com/obra/superpowers) and [affaan-m/ECC](https://github.com/affaan-m/ECC), both reviewed 2026-05-29.

## When

The user shares a repo or article "to see what we can adopt into our own project." One source at a time. The end-state is a critical, security-vetted, tiered recommendation — and, on approval, the adopted parts ported into TAOM.

## Principles

- **Port reviewed TEXT into TAOM-owned files. NEVER install the external plugin/package.** Installing eagerly loads its skill descriptions into every session (context tax we manage), its SessionStart bootstrap injects high-authority context (prompt-injection-by-design — a malicious or future-compromised fork could weaponize it), and it conflicts with our curated setup.
- **Most of any general "Claude operator" repo is irrelevant or duplicative for TAOM.** Wrong languages/domains (we're C#/.NET Bannerlord modding), or we've independently built the meta-tooling (context-budget, skill-stocktake, agent-introspection-debugging all exist on both sides). Be honest and critical — "some of it won't be useful for us" is the expected outcome.
- **Calibrate, don't blind-port.** A rule that's correct upstream can be wrong here. Example: TAOM *mandates* fail-open hooks, so the upstream "fail-open is a smell" rule would be all false-positives. Tune every ported rule to TAOM's conventions first.
- **Apply [simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md):** tiny gain + added complexity = reject.
- **Right-size the fan-out.** Scout inline first. If the README + structure make the verdict obvious (clearly out-of-domain → skip), do a LIGHT inline pass and verify the few load-bearing claims yourself — don't spin up a 3-agent workflow to confirm the obvious. Reserve multi-agent fan-out for genuinely adjacent/large repos that don't fit one context. (2026-05-29: open-design's domain-orthogonality was decisive from the README alone; the full workflow was over-machined — and still got a security detail wrong that had to be redone by hand.)
- **Verify load-bearing security claims yourself before relaying them.** A subagent's security verdict is a hypothesis (`evidence-over-claims.md` A.4) — spot-check its key claims (telemetry defaults, install-time exec, exfil) against the actual file before reporting to the user.

## The cycle

1. **Identify.** WebFetch the README + GitHub API metadata. Be skeptical of star counts and small-model summaries — verify claims from raw files. Note what it actually is, its domain, its license, and how recently it's maintained.
2. **Security pass FIRST** (standing requirement — gating). Pull the runnable surface verbatim and read it:
   - `package.json` lifecycle scripts (`postinstall`/`preinstall`/`prepare`/`prepack` — the #1 supply-chain vector); install scripts (`install.sh`/`.ps1`, npx installers); hooks; any `*.js`/`*.py`/`*.sh` that runs on install or on tool events.
   - Flag: outbound network, piping a downloaded script into a shell, `eval`/`Function()`, base64-decode-then-run, reads of credentials / env / ssh / tokens, telemetry / phone-home, obfuscation, opt-in closed-source SDKs.
   - Give the verdict **two ways**: safe to LEARN FROM (read) vs safe to INSTALL (run). We only ever need the former.
   - After porting, run **`/security-scan`** (`tools/audit_claude_config.py`) on our own result.
3. **Map novel vs duplicative.** Inventory the repo's surface; compare against TAOM's existing rules, skills, hooks, agents, memory, Model Routing, Codex integration, `/context-budget`, `/deep-review`. Don't adopt what we already have (ours is usually better and tuned to Bannerlord modding).
4. **Tiered recommendation.** Tier 1 (genuinely additive) / Tier 2 (marginal) / Skip (each with a one-line reason). Present it; let the user choose breadth. Surface any security findings.
5. **Implement (on approval).** Port reviewed text into TAOM-owned files, calibrated to TAOM. Author new skills per [external-skill-ports.md](../../.claude/rules/external-skill-ports.md) (description = *when to use*, ≤30 words; thin entry point that points to a doc).
6. **Adversarial review.** Run a `Workflow` find→verify over the changeset (dimensions: correctness/robustness, consistency, simplicity/coherence). Verify each finding against the files before acting ([evidence-over-claims.md](../../.claude/rules/evidence-over-claims.md)).
7. **Fix confirmed findings + RCA.** If a finding shares a root cause with past ones, write an RCA (`docs/reviews/rca-*.md`) and institutionalize prevention in an always-load rule + a memory.
8. **Commit MINE only.** Stage only the files this work produced. Leave shared/community files (`CLAUDE.md`, `AGENTS.md`) for the user's batch unless told otherwise — they're pushed last. Respect concurrent writers: re-read a shared file (`CHANGELOG.md`) immediately before editing it; the pre-commit hook requires `CHANGELOG.md` staged alongside any `.claude/` change. No AI attribution in commit messages (CLAUDE.md convention).
9. **Push.** Current feature branch only; never force-push a protected branch; never `--no-verify`.

## Gotchas (accumulated)

- The plugin install is itself prompt-injection-by-design. Don't install; port text.
- A review finding is a hypothesis, not a verdict — verify before implementing (evidence-over-claims.md; review accuracy is ~95%, not 100%).
- New always-load rules cost context on every session — prefer folding into an existing rule; justify each addition.
- When authoring docs/skills that DESCRIBE security patterns, avoid embedding the literal trigger strings — they can self-flag `/security-scan`. Describe the category instead, or put `audit-allow:` on the line.

## Precedents

- **obra/superpowers** → `.claude/rules/evidence-over-claims.md` + verification hooks + review-discipline edits. See `CHANGELOG.md` 2026-05-29 and `docs/reviews/rca-superpowers-enforcement-2026-05-29.md`.
- **affaan-m/ECC** → `tools/audit_claude_config.py` + `/security-scan` + this skill/doc. See `CHANGELOG.md` 2026-05-29.
