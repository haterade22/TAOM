# External Repo / Article Adoption

How TAOM evaluates an external repo or article and folds the genuinely-useful parts into our own setup — without importing bloat or risk. This is the executable procedure behind the `/adopt-external` skill. Precedents: [obra/superpowers](https://github.com/obra/superpowers) and [affaan-m/ECC](https://github.com/affaan-m/ECC), both reviewed 2026-05-29.

## When

The user shares a repo or article "to see what we can adopt into our own project." One source at a time. The end-state is a critical, security-vetted, tiered recommendation — and, on approval, the adopted parts ported into TAOM.

## Principles

- **Port reviewed TEXT into TAOM-owned files. NEVER install the external plugin/package.** Installing eagerly loads its skill descriptions into every session (context tax we manage), its SessionStart bootstrap injects high-authority context (prompt-injection-by-design — a malicious or future-compromised fork could weaponize it), and it conflicts with our curated setup.
- **The never-install rule has exactly one narrow exception: a MEASURED trial, decided explicitly by the user.** When the verdict genuinely turns on a claim only running the tool can settle (does it actually see our file types, how many nodes, what does it cost), a trial install is allowed. It is never the default and never something an agent decides on its own. Precedent: graphify v8 on 2026-08-18, which lifted the earlier content-egress rejection on an explicit decision. The conditions that made it acceptable, and which any future trial must reproduce: installed into an **isolated venv outside the repo**; **no `install` subcommand ever run** (graphify's `claude install` writes a section into CLAUDE.md plus a PreToolUse hook, and `codex install` writes into AGENTS.md, both of which TAOM guards); output forced outside the repo with an explicit `--out`, because it otherwise defaults to the scanned directory; `git status --porcelain` compared before and after to prove nothing was written; no MCP registration; nothing wired into a hook or CI afterwards. A trial install settles a factual question. **It is not adoption**, and the default verdict after one is still "port reviewed text, or reject". See [adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md).
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
   - **Automated supplement to the manual read** (don't replace it): for a foreign *skill* repo, run `python tools/audit_claude_config.py --root <repo> --external` — this fires TAOM's SkillSpector-derived categories (excessive-agency / memory-poisoning / prompt-leakage / tool-misuse / rogue-agent / output-handling + Python-AST + clean-room YARA) at full severity. For deeper coverage (LLM intent analysis, taint tracking, OSV CVE lookups) the heavyweight option is **NVIDIA SkillSpector** run **static-only** (`skillspector scan <repo>`, no API key) in an **isolated venv/Docker** — never installed into TAOM's harness (it's a LangGraph/LLM agent with network egress; we port patterns, we don't install it).
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
- **DietrichGebert/ponytail** → `.claude/rules/think-before-coding.md` ("Reuse ladder" section) — its YAGNI decision ladder, TAOM-translated to engine API → existing service/adapter → one-line delegation → minimal new code. The rest was duplicative of `simplicity-criterion.md` / `/deslop` / `/deep-review` / `/improve`; plugin not installed. See `CHANGELOG.md` 2026-06-18 and `docs/reviews/adopt-ponytail-2026-06-18.md`.
- **NVIDIA/SkillSpector** → `tools/audit_claude_config.py` gained 6 deterministic skill-threat categories + a stdlib-`ast` scan + an import-guarded clean-room YARA layer (`tools/yara_rules/`), all calibrated to TAOM (advisory on self-audit, full severity under `--external`). The LangGraph/LLM runtime was NOT installed; the DRL-1.1/unlicensed Neo23x0 `.yar` files were NOT vendored (clean-room rewrite instead). SkillSpector itself is documented above as the heavyweight static-only foreign-skill vet tool. See `CHANGELOG.md` 2026-06-22 and `docs/reviews/adopt-skillspector-2026-06-22.md`.
- **mattpocock/skills `improve-codebase-architecture`** → one analytical lens folded into `/improve`'s architecture audit-playbook: the Ousterhout "deep vs shallow module" detector + the concentrate-vs-move "deepening deletion test", plus a one-line distinction in `simplicity-criterion.md`. ~90% of the skill was duplicative of `/improve` (itself a shadcn/improve port); the HTML report and the 3-skill companion suite (`/codebase-design`, `/grilling`, `/domain-modeling` + `CONTEXT.md`) were skipped as duplicative of TAOM's markdown-plan + ADR + knowledge-graph model. First production use of the `--external` foreign-skill vet (clean). See `CHANGELOG.md` 2026-06-22 and `docs/reviews/adopt-mattpocock-improve-architecture-2026-06-22.md`.
- **mattpocock/skills `teach` + `handoff`** → `teach` SKIPPED (out-of-domain: a personal-learning HTML-lesson framework; TAOM's agent-facing knowledge base is served by ADRs/RCAs/memories/engine docs). `handoff` was ~85% duplicative of `/context-save` (which is richer + persists in-repo vs handoff's OS temp); folded its one nugget — a "Suggested skills for the next session" field + a secret-redaction note — into `/context-save`. See `CHANGELOG.md` 2026-06-22 and `docs/reviews/adopt-mattpocock-teach-handoff-2026-06-22.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reviews/adopt-mattpocock-improve-architecture-2026-06-22.md](../reviews/adopt-mattpocock-improve-architecture-2026-06-22.md)
- [docs/reviews/adopt-mattpocock-teach-handoff-2026-06-22.md](../reviews/adopt-mattpocock-teach-handoff-2026-06-22.md)
- [docs/reviews/adopt-ponytail-2026-06-18.md](../reviews/adopt-ponytail-2026-06-18.md)
- [docs/reviews/adopt-skillspector-2026-06-22.md](../reviews/adopt-skillspector-2026-06-22.md)

<!-- backlinks-end -->
