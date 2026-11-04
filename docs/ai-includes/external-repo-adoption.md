# External Repo / Article Adoption

How TAOM evaluates an external repo or article and folds the genuinely-useful parts into our own setup — without importing bloat or risk. This is the executable procedure behind the `/adopt-external` skill. Precedents: [obra/superpowers](https://github.com/obra/superpowers) and [affaan-m/ECC](https://github.com/affaan-m/ECC), both reviewed 2026-05-29.

## When

The user shares a repo or article "to see what we can adopt into our own project." One source at a time. The end-state is a critical, security-vetted, tiered recommendation — and, on approval, the adopted parts ported into TAOM.

A watched repo (listed in `docs/reviews/upstream-watch.json`, today yotthani's two) is reviewed with `/upstream-review`, which covers only the commits since its last reviewed SHA and advances that SHA when the review lands.

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
   - Before porting any file from a vendored drop, sweep it for inline package credentials, archives included, because grep cannot see inside a `.tar.gz` (extract a `.zip` or `.nupkg` first): `grep -rlE 'packageSourceCredentials|ClearTextPassword|<password|<token' <vendor-dir>`, then `find <vendor-dir> -type f \( -name '*.tar.gz' -o -name '*.tgz' \) | while IFS= read -r a; do tar -xzOf "$a" | grep -qE 'packageSourceCredentials|ClearTextPassword|<password|<token' && echo "$a"; done`, which reaches archives at any depth. The same `-E` pattern works in the Grep tool, where `\|` would match a literal pipe. Scrub any hit, and tell upstream when the token is theirs ([plan 005](../../plans/005-security-hygiene.md); the second `[SEC-02]` in `plans/_audit/2026-06-12-harvest.md`).
   - Give the verdict **two ways**: safe to LEARN FROM (read) vs safe to INSTALL (run). We only ever need the former.
   - **Automated supplement to the manual read** (don't replace it): for a foreign *skill* repo, run `python tools/audit_claude_config.py --root <repo> --external`. `<repo>` may be a plugin repo, a marketplace or one skill folder: the whole tree is read except `.git` and `node_modules`, and every script and JSON file in it gets the hook rules. Check the "N files scanned" line against the tree, and treat `UNCHECKED` (exit 3, no config found) as not vetted. Before 2026-09-29 it read only `.claude/**`, so a plugin repo was mostly unscanned ([adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md)). It fires TAOM's SkillSpector-derived categories (excessive-agency / memory-poisoning / prompt-leakage / tool-misuse / rogue-agent / output-handling + Python-AST + clean-room YARA) at full severity. For deeper coverage (LLM intent analysis, taint tracking, OSV CVE lookups) the heavyweight option is **NVIDIA SkillSpector** run **static-only** (`skillspector scan <repo>`, no API key) in an **isolated venv/Docker**, never installed into TAOM's harness (it's a LangGraph/LLM agent with network egress; we port patterns, we don't install it).
   - After porting, run **`/security-scan`** (`tools/audit_claude_config.py`) on our own result.
3. **Map novel vs duplicative.** Inventory the repo's surface; compare against TAOM's existing rules, skills, hooks, agents, memory, model and effort selection, Codex integration, the context budget scan, `/deep-review`. Don't adopt what we already have (ours is usually better and tuned to Bannerlord modding).
4. **Tiered recommendation.** Tier 1 (genuinely additive) / Tier 2 (marginal) / Skip (each with a one-line reason). Present it; let the user choose breadth. Surface any security findings.
5. **Implement (on approval).** Port reviewed text into TAOM-owned files, calibrated to TAOM. Add the source's row to [provenance-register.md](../reference/provenance-register.md) (`comparison-only` when only ideas came over, `behavioural-port` when text or behaviour did), and an `interop-only` row for every tool installed to run a gate ([provenance.md](../../.claude/rules/provenance.md) "Adopting something new": the adoption is not complete until the row exists). Author new skills per [external-skill-ports.md](../../.claude/rules/external-skill-ports.md) (description = *when to use*, ≤30 words; steps and traps in the skill, long reference in a doc it points to).
6. **Adversarial review.** Run a `Workflow` find→verify over the changeset (dimensions: correctness/robustness, consistency, simplicity/coherence). Verify each finding against the files before acting ([evidence-over-claims.md](../../.claude/rules/evidence-over-claims.md)).
7. **Fix confirmed findings + RCA.** If a finding shares a root cause with past ones, write an RCA (`docs/reviews/rca-*.md`) and route the prevention by CLAUDE.md "Where new knowledge goes", cheapest form first.
8. **Commit.** On the maintainer's word, commit by the AGENTS.md rules, staging only this work's files; re-read a shared file just before editing it.
9. **Push.** Only on the maintainer's word.

## Gotchas (accumulated)

- A review finding is a hypothesis, not a verdict: verify before implementing (evidence-over-claims.md).
- When authoring docs/skills that DESCRIBE security patterns, avoid embedding the literal trigger strings — they can self-flag `/security-scan`. Describe the category instead, or put `audit-allow:` on the line.

## Precedents

One entry per reviewed source: [adoption-history.md](../reviews/adoption-history.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reviews/adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md)
- [docs/reviews/adopt-ghidra-hindsight-2026-09-26.md](../reviews/adopt-ghidra-hindsight-2026-09-26.md)
- [docs/reviews/adopt-kingdom-borders-2026-09-30.md](../reviews/adopt-kingdom-borders-2026-09-30.md)
- [docs/reviews/adopt-mattpocock-improve-architecture-2026-06-22.md](../reviews/adopt-mattpocock-improve-architecture-2026-06-22.md)
- [docs/reviews/adopt-mattpocock-teach-handoff-2026-06-22.md](../reviews/adopt-mattpocock-teach-handoff-2026-06-22.md)
- [docs/reviews/adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md)
- [docs/reviews/adopt-mithrilforge-engine-perf-2026-10-02.md](../reviews/adopt-mithrilforge-engine-perf-2026-10-02.md)
- [docs/reviews/adopt-ponytail-2026-06-18.md](../reviews/adopt-ponytail-2026-06-18.md)
- [docs/reviews/adopt-skillspector-2026-06-22.md](../reviews/adopt-skillspector-2026-06-22.md)
- [docs/reviews/adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)
- [docs/reviews/adopt-yotthani-2026-10-15.md](../reviews/adopt-yotthani-2026-10-15.md)
- [docs/reviews/adopt-yotthani-2026-10-16.md](../reviews/adopt-yotthani-2026-10-16.md)
- [docs/reviews/adopt-yotthani-animation-handoff-2026-09-18.md](../reviews/adopt-yotthani-animation-handoff-2026-09-18.md)
- [docs/reviews/adopt-yotthani-bannerlord-2026-09-30.md](../reviews/adopt-yotthani-bannerlord-2026-09-30.md)
- [docs/reviews/adopt-yotthani-shadercachekeeper-2026-10-06.md](../reviews/adopt-yotthani-shadercachekeeper-2026-10-06.md)

<!-- backlinks-end -->
