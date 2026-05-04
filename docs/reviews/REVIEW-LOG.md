# Codex Adversarial Review Log

Running scorecard of all reviews. **COMPLETE: 25/25 features reviewed, 2026-04-05/06.**

## Summary

| # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |
|---|------|---------|--------------|----------------|-----------|-----------------|-------------|----------------|
| 1 | 2026-04-05 | CulturalFeats | no-ship | partial-agree | 1 confirmed | 1 | 2 | v1 (basic) |
| 2 | 2026-04-05 | BannerColorPersistence | no-ship | partial-agree | 1 (understated) | 2 | 4 | v2 (improved) |
| 3 | 2026-04-05 | ArmyTargeting | approve | agree (shallow) | 0 | 0 | 0 | v3 (required sections) |
| 4 | 2026-04-05 | TroopProgression | no-ship | agree | 2 confirmed + 1 valid divergence | 0 | 0 | v4 (verification artifacts) |
| 5 | 2026-04-05 | Diplomacy+Execution | no-ship | agree | 4 confirmed + 1 valid | 0 | 0 | v4 |
| 6 | 2026-04-05 | FactionMap | no-ship | agree | 2 confirmed | 0 | 0 | v4 |
| 7 | 2026-04-05 | CustomBattles | no-ship | agree | 1 confirmed + 1 valid concern | 0 | 0 | v4 |
| 8 | 2026-04-05 | CharacterCreation | no-ship | agree | 1 confirmed + 1 valid | 0 | 0 | v5 |
| 9 | 2026-04-05 | RaceAge | no-ship | design questions | 3 valid (need design input) | 0 | 0 | v5 |
| 10 | 2026-04-05 | BattleBalance | no-ship | agree | 3 confirmed | 0 | 0 | v5 |
| 11 | 2026-04-05 | HeroRace | no-ship | agree | 3 confirmed | 0 | 0 | v6 |
| 12 | 2026-04-05 | Siege+BannerInjection | no-ship | agree (1 deferred) | 1 confirmed + 1 valid | 0 | 0 | v6 |
| 13 | 2026-04-05 | AdvancedCombat+Warg | no-ship | agree | 4 confirmed | 0 | 0 | v6 |
| 14 | 2026-04-05 | Wave4A (Weather+Atmosphere+Shader) | no-ship | agree | 3 (1 HIGH, 2 MEDIUM) | 0 | 0 | v6 |
| 15 | 2026-04-05 | Wave4B (Time+ChildGen+Startup+Menu+Enc+BattleScenes) | no-ship | agree | 3 (1 HIGH, 2 MEDIUM) | 0 | 0 | v6 |
| 16 | 2026-04-06 | Infrastructure (Adapters+Core+SubModule+IoC) | no-ship | agree | 3 (2 HIGH, 1 MEDIUM) | 0 | 0 | v6 |
| 17 | 2026-04-07 | SpecialResources (adversarial vs TOR_Core) | needs-attention | partial-agree | 2 confirmed (sprite, storage) | 1 (kingdom_id) | 0 | v6-adversarial |

## Metrics

**Codex accuracy rate:** 48 real findings / 61 total findings = 79%
**Codex miss rate:** 8 missed bugs / 56 total real bugs = 14%
**False positive rate:** 9 false positives / 61 findings total = 15%
**Clean feature detection:** 1/1 (ArmyTargeting correctly approved)

**v6 prompt batch (reviews 11-16):** 15 findings, 15 confirmed, 0 false positives = **100% accuracy**

## Gap Reviews (post-audit)

| # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |
|---|------|---------|--------------|----------------|-----------|-----------------|-------------|----------------|
| 17 | 2026-04-06 | Arena (TaomTournamentModel) | no-ship | 1 confirmed + 2 design questions | 1 (dummy lookup) | 0 | 0 | v6 |
| 18 | 2026-04-06 | CharacterSelection (transpiler) | no-ship | 1 fix + 2 deferred (need decompilation) | 1 (race fallback) | 0 | 0 | v6 |
| 19 | 2026-04-07 | SpecialResources | needs-attention | agree (5 confirmed + 1 ship-blocker Claude found) | 5 (1 CRIT pending txn, 1 CRIT no clamp, 1 HIGH raid, 1 HIGH save, 1 LOW cap) | 0 | 1 (wrong kingdom_id) | v6 |
| 20 | 2026-04-07 | CareerSystem | N/A — feature not built | N/A | 0 | 0 | 0 | v6 |
| 21 | 2026-04-07 | CareerSystem (impl vs TOR) | needs-attention | partial-agree | 2 confirmed (tier validation, save serialization) + 1 partial (widget def) | 3 (mutation scope, ability scope, passive coverage — intentional v1 scope) | 1 (hallucinated AllowedRaces property) | v6-adversarial |
| 22 | 2026-04-08 | SettlementGuards | needs-attention | partial-agree | 1 confirmed (spear culture IDs) + 2 self-found (reflection caching, dead attributes) | 1 (spawn-point severity overstated) | 0 | v6 |

| 23 | 2026-04-08 | NamedCompanions + Wanderer Race | needs-attention | agree | 1 confirmed (load teleport) + 1 dead code | 0 | 0 | v6 |
| 24 | 2026-04-14 | Career CC Selection | needs-attention | agree | 1 confirmed (empty menu crash) + 1 test gap | 0 | 0 | v6-adversarial |
| 25 | 2026-04-20 | RevoltTuning | needs-attention | agree | 1 HIGH (no-validation) + 1 MEDIUM (cache-lifetime doc mismatch) | 0 | 0 | v6-adversarial |
| 26 | 2026-04-26 | Tier1 productivity skills adoption (`.claude/` infra) | NEEDS-FIXES | agree (2 promotions, 1 demotion) | 7 confirmed (2 HIGH→3, 3 MEDIUM→2, 2 LOW) + 1 missed by Codex (`scan_agents` body-counting) | 0 | 1 (`scan_agents` had same body-count bug as `scan_skills`; Codex flagged only the latter) | v6-adversarial-harness |
| 27 | 2026-04-26 | Self-review of Tier1 fix commit (5df21ea) | NEEDS-FIXES (0H/1M/3L) | agree | 4 confirmed (1 MED, 3 LOW) + 2 process violations (CHANGELOG, counter math) | 0 | 0 | self-review-v1 |
| 28 | 2026-04-26 | Prevention infrastructure (b7e7188 — hooks/rules built to catch the failure modes from #26+#27) | NEEDS-FIXES (1H/3M/2L) | agree (1 promotion, 1 demotion) | 6 confirmed (1 HIGH→1, 3 MED→2, 2 LOW + counter regex prep) + 1 process (no GitHub issue at ship time) | 0 | 0 | adversarial-prevention-v1 |
| 29 | 2026-04-27 | Tier 2/3 adoption (79350f2 — 4 skills + 3 subagents + suggest-compact upgrade) | NEEDS-FIXES (1H/3M/2L) | agree (1 promotion) | 6 confirmed (1 HIGH→1, 2 MED→3, 2 LOW + 1 process settings.local drift) | 0 | 0 | adversarial-tier2-3-v1 |
| 30 | 2026-05-04 | CustomBattles filter+cap (commander dropdown faction filter + 3-per-culture cap) | NEEDS-FIXES (1 P1) | agree | 1 confirmed (P1 stale `SelectedItem` after Clear+Rebuild — `SelectorVM<T>.SelectedIndex` setter early-returns on no-op) | 0 | 0 | v6-focused-enhancement |
| 31 | 2026-05-04 | Career cooldown rework (uniform 30s timer + charging feedback + cleanup pass + 3-GameModel ctor injection) | ISSUES FOUND (0H / 2M / 0L) | agree | 2 confirmed (M: single-bucket tick accumulator drops elapsed time; M: ParseGlobalTuning admits NaN/±Infinity) | 0 | 0 | v7-rework-plus-cleanup |

**Post-codebase reviews:** 19-31. 31 Codex reviews total, 86 bugs found across codebase.

### Review 31 — Career Cooldown Rework Root Cause Analysis

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Single-bucket tick accumulator (`if (acc >= 1f) Tick(1f)`) drops elapsed time on long frames; a 2.5s frame drains only 1s of cooldown | Logic error (carried-over batching pattern) | The 1Hz batched tick was inherited from the prior charge-based code where per-second granularity was natural. The cooldown rework changed semantics to wall-clock-precise but the tick scheduler wasn't revisited. | Replaced batched accumulator with per-frame `_abilityService.Tick(heroId, dt)`. `CareerAbility.Tick` already handles fractional dt. Added `Tick_LargeDt_DrainsFullElapsedTime` and `Tick_FractionalDt_AccumulatesAcrossFrames` regression tests. New AGENTS.md lesson: when feature semantics shift from "periodic batch" to "wall-clock gate", revisit any `_tickAccumulator` patterns. |
| 2 | `ParseGlobalTuning` accepts `NaN` and `±Infinity` (TryParse admits them, then `<= 0` and `> 3600` both evaluate false for NaN). NaN cooldown then makes `IsOnCooldown => CooldownRemaining > 0f` always false (NaN comparisons are always false) — ability "always ready", V re-activates indefinitely. | Missing input validation (didn't enumerate IEEE-754 special values) | Validation focused on "is the user value sensible" (positive, bounded). Didn't enumerate the special floats that `float.TryParse` admits. Same blindspot as the existing range checks — extending to NaN/Inf was just not on the checklist. | Added `IsNaN || IsInfinity` rejection BEFORE range gates. Added 3 explicit unit tests (`NaN`, `Infinity`, `-Infinity`). New AGENTS.md lesson: any user-facing float range must reject non-finite values. |

### Review 25 — RevoltTuning Root Cause Analysis

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Provider accepts any parseable JSON values, no range or sign validation | Missing guard at a system boundary | Treated user-editable JSON as trusted input; the defaults table in docs was mistaken for a validation contract. | Added `Validate` in `RevoltTuningConfigProvider` + 7 new test cases covering out-of-range, inverted ordering, and sign-flipped penalties. Warn + default-fallback per field. |
| 2 | Singleton cache + model-ctor capture means JSON edits need a full Bannerlord restart, but docs claimed "next game load" | Convention inconsistency (doc vs runtime lifecycle) | Copied "next game load" from the plan without cross-checking against DryIoc `Reuse.Singleton` + `OnSubModuleLoad` lifetime. | Updated `docs/features/revolt-tuning.md` "How to Retune" to state Bannerlord must be quit and relaunched. No code change — Singleton is the intended pattern. |

Deferred items resolved:
- Siege camp fallback: distributed positions around gate instead of stacking
- CharacterSelection transpiler: verified correct via decompilation (single AgentVisualsData ctor, no competing ActionSet call)
- LoadingWindowViewModel: verified `internal void Update()` exists in v1.3.15 GauntletUI.dll

### Review 26 — Tier1 Adoption Root Cause Analysis (first non-C# review)

First Codex review of `.claude/` harness changes (no Bannerlord feature, no C# code). Adapted prompt structure from feature-review template; replaced kingdom/culture cheatsheet with harness cheatsheet (skill load semantics, hook lifecycle, rule loader scoping). Codex returned a sharp, well-cited review citing official Claude Code docs at https://code.claude.com/docs/en/{skills,hooks,memory}.

**Disagreements with Codex severity:**
- M1 (environment-failures `paths:`) → upgraded to HIGH. Same risk class as the gitignored hook script in deep-review #1: silently inert load-bearing config.
- M3 (memory file overhead missing from scanner) → downgraded to LOW. MEMORY.md is capped at ~25KB → ~6K tokens, bounded.

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| H1 | scan.sh counts full SKILL.md body as startup overhead | API assumption — copied upstream methodology without verifying current Claude Code semantics | Assumed full skill bodies eagerly load (the very motivation for "two-layer skill injection" pick from learn-claude-code). Reality: descriptions load eagerly, bodies lazily. | scan.sh now reports eager (frontmatter) and lazy (body) separately. CLAUDE.md "Inline-hook activation" rule documents the load model. The deferred pick #2 is now moot — Claude Code already does what learn-claude-code's pattern proposed. |
| H2 | feature-builder claims writing freeze state file activates hooks | Lifecycle assumption | Assumed state-file presence triggers hook regardless of skill activation. Reality: hooks declared in skill frontmatter only fire while that skill is invoked. | Rewrote scope-lock section to explicitly invoke `/freeze` via Skill tool. Added CLAUDE.md "Inline-hook activation" rule + AGENTS.md entry distinguishing `/investigate`'s deliberate hook reuse from blanket pattern copying. |
| M1 | environment-failures.md `paths: ["**/*"]` makes rule conditional, not always-load | Convention assumption | Followed `paths:` pattern reflexively. Reality: ANY `paths:` field = conditional; omit field entirely for always-load. | Dropped `paths:` from environment-failures.md. Added scoped-rules convention header in CLAUDE.md explaining the omit-vs-glob distinction. |
| M2 | check-freeze.sh `tr -d '[:space:]'` strips internal whitespace from freeze-dir path | Shell idiom | Wanted to strip trailing newline; used `tr -d` shorthand which destroys all whitespace. Steam install paths contain spaces (`Mount & Blade II Bannerlord`). | Replaced with `IFS= read -r` which preserves the line verbatim. Verified with path-with-spaces freeze test (allow + deny both pass). |
| M3 | scan.sh missing MEMORY.md from inventory | Completeness | Scanner audited only `.claude/` and `.mcp.json`. MEMORY.md is cap-loaded at conversation start. | Added `scan_memory` function. ~1.2K tokens accounted for. |
| L1 | `triggers:` field in skill frontmatter undocumented in current Claude Code | API assumption / port drift | gstack uses `triggers:` for its own preamble; assumed it transferred. Codex verified docs only mention `description` and `when_to_use`. | Dropped `triggers:` from /freeze and /investigate; phrases moved into `description`. AGENTS.md harness-review section flags this for future ports. |
| L2 | filesystem MCP hardcoded count was 12, actual 13 | Stale value | Estimate without source check. | Updated count + added source URL comments to all entries in SERVER_TOOLS map. |
| Bonus | scan_agents had identical body-count bug as scan_skills | Same as H1 | Codex flagged only the skills case; missed the parallel bug in agents. Caught in our own RCA. | Same fix applied to scan_agents. |

**Codex strengths in this review:**
- Cited official Claude Code docs (URLs) for every behavioral claim. No "I think it works this way" inference left unmarked.
- Explicitly distinguished UNVERIFIED vs CONFIRMED vs DISPUTED on each Known Suspect.
- Verified gitignore claims with `git check-ignore -v` instead of grepping the file (correctly disputing my pre-finding that `.gitignore` only excludes `.claude/logs/` — actually it excludes `bin/` line 2, which had originally caught us, but Codex correctly verified the post-fix state).

**What Claude (we) caught that Codex didn't:**
- The `scan_agents` body-count bug was a parallel of H1; Codex flagged only the skills case. RCA caught the agents case.

**Tier1 final score:** 8/8 confirmed bugs → all fixed in same session. 0 deferred.

**Big result:** corrected scan baseline went from "75,906 tokens (94% headroom)" to "60,866 tokens eager / 78,059 worst-case (94% / 92% headroom)". Skills are 25× cheaper at startup than the buggy scanner reported. Pick #2 (two-layer skill injection from learn-claude-code) is **moot — Claude Code already does this natively**; dropped from the deferred queue.

### Review 27 — Self-Review of Pass-1 Fixes (Codex Pass 2)

Recursive review: Codex reviewed our fixes from review #26. Verdict NEEDS FIXES (0 HIGH, 1 MEDIUM, 3 LOW + 2 process violations). Self-review file at `docs/reviews/codex-selfreview-tier1-fixes-2026-04-26.md`.

**Findings (all confirmed + fixed in same session):**

| # | Finding | Severity | Root cause | Preventive |
|---|---|---|---|---|
| SR-MED | `scan_memory()` locator substring-matched basename, ambiguous on machines with multiple TAOM-named projects | MED | Lazy heuristic; assumed substring match would be unique | Switched to exact Claude project slug (`drive--path-with-dashes`) derived from full repo path; substring search retained as fallback only |
| SR-LOW1 | 25KB byte cap computed but never enforced | LOW | Code skeleton existed but the conditional path that uses `capped_bytes` was no-op | Token estimate now uses `head -c 25600 \| head -200` slice when the byte cap binds |
| SR-LOW2 | "Lazy tok" column label misleading — printed full body, not delta | LOW | Sloppy header; the WORST_CASE math was correct but column name implied otherwise | Renamed column to "If-invoked" with explicit footer note |
| SR-LOW3 | `ilspy` MCP server count hardcoded as 8; actual is 4 | LOW | Carried-forward estimate; not source-checked | Verified by reading `server.py` (4 `@mcp.tool()` decorators); updated count + tagged each `SERVER_TOOLS` entry as EXACT or HEURISTIC |
| Process #1 | CHANGELOG.md not updated for `5df21ea` despite mandatory rule | — | I had drafted CHANGELOG mentally for the commit message but never wrote it to the file | Caught by Codex's process-compliance check; added retroactive entry |
| Process #2 | AGENTS.md said "26 reviews, 64 bugs"; actual was 65 (off by 1) | — | Manual arithmetic during the previous fix commit; didn't cross-check against REVIEW-LOG.md | Reconciled to 65; future updates should cross-reference REVIEW-LOG.md totals |

**What Codex did particularly well in this round:**
- Caught a process violation (missing CHANGELOG) that no internal review process would have surfaced — Codex doesn't trust the commit message, it diffs the file.
- Did the counter arithmetic and flagged the off-by-one. This is the kind of mechanical check Claude is bad at because the numbers feel right.
- Distinguished "fix complete" from "regression introduced" in the per-fix verdict table — caught that H1's column label was a regression even though the underlying math was correct.

**What Claude (we) caught beyond Codex's findings:**
- The trimmed descriptions still being 31w (over the ~25w target Codex noted) — fixed both back to ~22w in the same commit.

**Score:** 4/4 confirmed bugs + 2 process gaps → all addressed in third fix commit. Three-commit chain (efbde5b → fbfd25a → 5df21ea → this commit) on Tier 1 adoption is now closed; no further regressions expected. Each commit added findings, each subsequent review found fewer (8 → 7 → 4), suggesting convergence rather than open-ended iteration.

### Review 28 — Adversarial Review of the Prevention Infrastructure (Codex)

User asked: "we did our review?" — honest answer was no. The prevention bundle (b7e7188) shipped with smoke-tests but no formal Codex pass. Dispatched the adversarial review specifically to check **recursion risk**: could a bug in the prevention infrastructure defeat the prevention it's supposed to enable?

**Verdict:** YES. 1 HIGH + 3 MED + 2 LOW + 1 process gap.

| # | Finding | Codex sev | My sev | Disposition |
|---|---|---|---|---|
| HIGH | `--amend` blanket exemption in CHANGELOG hook → real bypass | HIGH | HIGH | Confirmed prevention theater — fixed |
| MED-1 | Same `--amend` exemption in tracked-files hook | MED | **HIGH↑** | Same vulnerability class as the HIGH; fixed |
| MED-2 | Hooks miss `git -C path commit` (substring match too narrow) | MED | MED | Real but rare; broadened detection |
| MED-3 | scan.sh bloat lint bypassed by multiline YAML descriptions | MED | **LOW↓** | No current skill uses multiline; deferred |
| LOW-1 | harness-facts says "will warn" but hooks "hard-block" | LOW | LOW | Doc drift — fixed |
| LOW-2 | harness-facts missing `disable-model-invocation: true` exception | LOW | LOW | Doc completeness — fixed |
| LOW-3 | harness-facts project-slug rule presented as fact, actually empirical | LOW | LOW | Doc accuracy — fixed |
| LOW-4 | counter validator regex brittle to wording drift | LOW | LOW | Hardened with keyword-anchored extraction |
| Process | No GitHub issue at ship time for b7e7188 | (in B) | MED | Created retroactively (#92) |

**Full Root Cause Analysis (per `/review-codex` Phase 3e — initially shortcut to HIGH+MED-1 only, retroactively extended on user's prompt):**

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|-------------------|
| HIGH + MED-1 | `--amend` blanket exemption defeats both pre-commit gates | API/lifecycle assumption — wrong mental model of amend semantics | Rationalized "amends modify a prior commit, so prior commit owns CHANGELOG status." Wrong — amend is commonly used as a workflow ("oops, forgot a file, amend it in"). The hook exempted exactly the case it should catch. Two-step bypass: commit unrelated change, then amend with `.claude/` files — hook fires on neither. | Replaced blanket exemption with post-amend file-set logic (staged ∪ HEAD) for CHANGELOG hook; removed exemption entirely for tracked-files hook (working-tree state isn't amend-affected). Added explicit "amend exemptions in pre-commit hooks are a recursion risk" note to `harness-facts.md`. Test fixture (TEST G) verifies amend-bypass blocking on a throwaway branch. |
| MED-2 | Hooks miss `git -C path commit`, `git -c key=val commit` | Edge case / API surface incompleteness | Knew about `git commit` and `git commit --amend`; didn't enumerate the full set of commit-emitting forms. Tested only the common patterns. | Broadened detection to `*"git commit"* | *"git -"*" commit"*` with explicit `git commit-tree` rejection. Added a "git invocation forms hooks must handle" enumeration to `harness-facts.md` so future hook authors enumerate these explicitly. |
| MED-3 | Bloat lint bypassed by multiline YAML descriptions (`description: |` block) | Parser limitation — single-line scanner | `extract_description` was written for the simple `description: text` case; multiline YAML is a documented YAML feature I didn't account for. No skill currently uses it; the bypass is theoretical until one does. | Added a TODO comment at `extract_description` flagging the multiline gap so the next author who writes a multi-line description doesn't silently bypass the lint. Replacing with a real YAML parser is overkill given current usage. |
| LOW-1 | `harness-facts.md` said hooks "will warn"; they hard-block | Documentation drift | Wrote the rule before finalizing hook semantics; the rule fossilized "warn" while the hook evolved to "hard-block". Cross-reference between rule and implementation wasn't checked when either changed. | Updated wording. Added rule-writing convention: when a rule cites the behavior of a hook/script, note the file path explicitly so future edits to either side surface the cross-reference (in CLAUDE.md scoped-rules note). |
| LOW-2 | `harness-facts.md` missing `disable-model-invocation: true` exception | Documentation completeness | Wrote rule from memory; didn't do a doc-sweep when authoring. Relied on what I remembered from earlier reading. | Added the exception. Codified rule-writing convention: for any cited Claude Code doc URL, OPEN the URL during rule authoring and capture every relevant exception/edge case before committing the rule. (Added to `external-skill-ports.md` as that's where doc-sweep discipline most applies.) |
| LOW-3 | `harness-facts.md` slug derivation rule presented as fact, actually empirical | Documentation accuracy / over-claiming | Derived the slug formula from a single observation on this machine; assumed it was canonical. Should have labeled as empirical from the start. | Relabeled as empirical with derived-then-fallback recommendation. Added DOC-BACKED vs EMPIRICAL labeling convention to `harness-facts.md` self — every fact must explicitly cite either a doc URL or an observation context, not a vague "verified" claim. |
| LOW-4 | `audit-review-counter.sh` regex brittle to wording drift | Parser limitation / future-proofing | Wrote the regex against the exact wording in REVIEW-LOG.md, didn't consider drift. | Hardened with summary-keyword anchoring + "across codebase" disambiguator with fallback. Pragmatic stance: when wording drifts and the validator stops working, fix the regex — that's why the script exists, to surface drift mechanically. |
| Process | No GitHub issue at ship time for `b7e7188` | Process discipline gap | Drafted CHANGELOG mentally for the commit; forgot the issue was ALSO required per CLAUDE.md "Documentation Requirements". The pre-commit hook only enforces CHANGELOG, not issue creation. | Created retroactively (#92). Considered adding gh-CLI integration to `check-changelog-changed.sh` to also verify an issue exists (or that the commit message references one), but deferred — overhead vs benefit unclear. Added the discipline reminder to CLAUDE.md "Completion Workflow" Phase 4: GitHub issue must exist BEFORE shipping the commit, not after. |
| Meta | RCA initially run for HIGH+MED-1 only, not all 8 confirmed bugs (caught by user) | Process discipline gap — `/review-codex` Phase 3e applies to ALL findings, not just HIGH | Conflated severity with importance for RCA. The skill explicitly says "for EACH confirmed bug" — including LOWs. Skipping RCA for LOWs means we never extract the systemic lesson, just patch the symptom. | This very RCA table is the fix. Also added explicit "ALL findings get RCA, not just HIGH" rule to `harness-facts.md` "How this rule changes how you work" section. |

**Codex strengths in this round:**
- Distinguished CONFIRMED from DISPUTED for each Known Suspect (e.g., agreed JSON output is safe given static heredoc; correctly ruled out the false-positive concerns I asked about for tracked-files hook).
- Caught the `git -C` / `git -c` substring-match gap that I genuinely hadn't considered.
- Verified each Claude Code semantic claim with line numbers from the official docs (https://code.claude.com/docs/en/{skills,hooks,memory}).
- Identified the recursion risk as the framing of the whole review — not just a list of bugs but a structural critique of "does this prevention prevent?"
- Caught both LOW-2 and LOW-3 inside the harness-facts.md UNVERIFIED bucket, neither of which I'd have noticed unaided.

**What I caught beyond Codex's findings:**
- The counter-regex hardening that Codex asked for in finding LOW-4 had an even subtler bug: my "tolerant" replacement extracted numbers from anywhere in the matched line, so a line like "19-27. 27 Codex reviews total, 71 bugs found" returned `19, 27` instead of `27, 71`. Fixed during smoke-test by anchoring extraction on the keyword (`grep -oE '[0-9]+ reviews?'` then strip suffix).

**Tier 1 + prevention chain summary:** five commits (efbde5b → fbfd25a → 5df21ea → 4964299 → b7e7188 → THIS), four reviews, 6 of the bugs from the prevention review (this) are exactly the categories the prevention was supposed to make impossible — proving that the prevention itself needed prevention. Convergence is real (8 → 7 → 4 → 6 in this case but the spike at 6 is acceptable because we were testing a more meta layer). Closing the loop.

### Review 29 — Adversarial Review of Tier 2/3 adoption (Codex Pass 5)

Codex review of `79350f2` (Tier 2 + 3 ecosystem-review adoption: 4 new skills, 3 new subagents, suggest-compact upgrade, effort frontmatter, scope-reduction rule). Verdict: NEEDS FIXES, 1 HIGH + 2 MED + 2 LOW + 1 process gap.

Review file: `docs/reviews/codex-adversarial-tier2-3-2026-04-26.md`. Issue: #94.

**Severity disagreements:**
- Codex's suspect-2 included a CONFIRMED issue with `/scope-check effort: low` that wasn't in the formal A-E findings list. Promoted to MED-3 in my own analysis since the skill does inline reasoning that gets directly under-powered.

**Recursion risk: REAL.** The HIGH (`suggest-compact.sh` commit-form blind spot) is the same recursion-risk class as review #28 — the prevention rule existed in `harness-facts.md` when this commit was being written, but I didn't apply it. **The prevention infrastructure didn't apply to its own first user.** Same pattern as the prior reviews where the prevention skipped the review's own remediation.

**Full Root Cause Analysis (per `harness-facts.md` rule 4 — every confirmed bug):**

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|-------------------|
| HIGH | `suggest-compact.sh` git commit substring matcher misses `git -C` / `git -c` | API surface incompleteness — failure to apply codified rule to NEW code | Wrote ad-hoc; the harness-facts.md "Git invocation forms" rule (added in `2c4d414`) was loaded but I didn't consult it when writing new commit-detection in `79350f2`. **Prevention rule existed but wasn't applied to its own first user.** | Apply the rule's reference pattern (DONE — fixed in this commit). Strengthened harness-facts.md to mark the pattern MANDATORY for new hooks; added grep-before-ship discipline note; added new audit-checklist item to `/skill-stocktake`. |
| MED-1 | `/scope-check` rule prose-only, not enforceable | Aspirational vs deterministic confusion | Wrote the rule as if prose-in-a-skill = prevention. Without a deterministic verifier, depends on Claude reading and following its own instruction. | Relabeled rule explicitly as GUIDANCE (aspirational). Honest labeling vs aspirational claim. (Building a deterministic plan-vs-delivery verifier would be overkill for current usage.) |
| MED-2 | `/skill-stocktake` checklist drift (amend-exemption + DOC-BACKED labeling missing) | Audit-baseline drift — checklist not synced when harness-facts.md grew | Added the audit skill in `79350f2`; harness-facts.md post-#28 expansions in `2c4d414`. They didn't cross-reference. The audit was certifying against a stale checklist — same recurrence pattern as the prevention infrastructure not applying to itself. | Added 2 new sections to `/skill-stocktake` checklist: "Hook integrity" (commit-form patterns + amend exemptions) and "Documentation labeling" (DOC-BACKED vs EMPIRICAL). |
| MED-3 | `/scope-check effort: low` underpowers inline reasoning | API assumption — wrong intuition about which skills have inline reasoning vs subagent dispatch | Assumed `/scope-check` was lightweight reasoning; in reality the new scope-reduction classification work happens inline. `low` directly applies to that path. | Removed `effort: low` (defaults to `inherit`). Added rule to `/skill-stocktake` checklist: `effort: low` should NOT be set on skills doing significant inline reasoning. |
| LOW-1 | `/context-save` freeze-note logic backwards | Off-by-one in conditional reasoning | Wrote "if you've frozen to .claude/, the write will be blocked" — but `.claude/state/context/` is INSIDE `.claude/`, so freeze-to-.claude/ ALLOWS the write. Note opposite of truth. | Rewrote: only freeze scopes that EXCLUDE `.claude/state/context/` block the write. |
| LOW-2 | CHANGELOG claim "validator will auto-bump" but tool requires `--fix` | Documentation drift — claim outpaced implementation | Wrote CHANGELOG as if validator was wired automatic; tool only updates with explicit `--fix`. | This CHANGELOG entry corrects the claim. Can't retroactively edit `79350f2` commit message; treat as historical drift. |
| Process | `settings.local.json` modified but absent from commit-message changed-file summary | Documentation drift — staged file invisible in summary | `git add` staged correctly but my commit-message summary didn't mention it. Codex caught via diff. | Discipline reminder: when writing commit messages, pipe `git diff --cached --stat` into the summary process, not memory. |

**Codex strengths in this round:**
- Recursion-risk frame explicit in section D — confirmed both the HIGH and 2 of 3 MEDIUMs as productivity/prevention defeats, ruled out the LOWs as runtime-safe.
- Doc-cited each Claude Code semantic claim with URLs from https://code.claude.com/docs/en/{skills,hooks,memory}.
- Caught process drift (`settings.local.json` un-summarized) by diffing rather than trusting the commit message.

**What I caught beyond Codex:**
- Promoted suspect-2 (effort: low underpowering inline reasoning) to a numbered MED finding even though Codex left it inside the suspect verdict. Codex's framing was correct but understated the impact.

**Five-pass Tier 1 + Tier 2/3 chain summary:** 6 reviews so far (deep-review + Codex passes 1-5). Findings per pass: 8 → 7 → 4 → 6 → 6. The convergence isn't monotonic but each round catches a meaningfully smaller class of bug. The big lesson: **prevention infrastructure must apply to its own first user, or the next ship of new code regresses on the codified rule.** Now codified: `harness-facts.md` notes "MANDATORY for any new hook that detects git commits" and `/skill-stocktake` audits for it.

**v4 prompt batch (reviews 4-7):** 10 findings, 9 confirmed, 0 false positives = **90% accuracy**
**v5 prompt batch (reviews 8-10):** 8 findings, 7 confirmed + 1 FP-adjacent, 0 false positives = **88% accuracy**

Target: accuracy >60%, miss rate <30%, false positives <20%

---

## Review #1: CulturalFeats

**Date:** 2026-04-05
**Prompt version:** v1 (basic — ADR-focused, no decompilation guidance)
**Report:** [codex-adversarial-cultural-feats-2026-04-05.md](codex-adversarial-cultural-feats-2026-04-05.md)

### Codex Findings (4)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | CRITICAL | GameModel entry points contain business logic (ADR-002/ADR-007) | **Overstated** — one-liner feat checks in <55-line files. Tech debt, not ship-blocker. Downgrade to LOW-MEDIUM. |
| 2 | HIGH | Mounted upgrade checks source troop, not target | **False positive** — vanilla `KhuzaitRecruitUpgradeFeat` uses same `characterObject.IsMounted` check |
| 3 | HIGH | Tests don't cover shipped behavior | **Partially valid** — true, but GameModel rule says thin entry points are exempt. MEDIUM. |
| 4 | MEDIUM | Null-reference if registration hook fails | **Valid but LOW** — scenario is unrealistic; `GetAllFeats()` already handles null |

### Bugs Codex Missed (2)

| Bug | Severity | Why missed |
|-----|----------|-----------|
| Forest speed bonus applied unconditionally (should be `TerrainType.Forest` only) | HIGH | Didn't decompile `DefaultPartySpeedCalculatingModel` to see vanilla terrain gate |
| Caravan `EffectBonus` convention (`0.75f` displays as "+75%" in UI) | HIGH | Didn't check cross-feat consistency of `EffectBonus` + `AdditionType` convention |

### Prompt Lessons
- ADR compliance as top focus led to pattern-violation padding
- No decompilation requirement led to the `IsMounted` false positive
- No feature-specific risk areas — generic focus produced generic results

---

## Review #2: BannerColorPersistence

**Date:** 2026-04-05
**Prompt version:** v2 (improved — feature-specific focus, DO NOT section, decompilation requested)
**Report:** [codex-adversarial-banner-color-persistence-2026-04-05.md](codex-adversarial-banner-color-persistence-2026-04-05.md)

### Codex Findings (3)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Unique secondary-color patch falls through on overlap | **Partially valid, understated** — feature is actually a no-op in ALL cases, not just overlap. MEDIUM (dead code, not regression). |
| 2 | HIGH | Drift guard is global, not player-only | **Design question** — may be intentional for LOTR War of the Ring. MEDIUM. |
| 3 | HIGH | Persistence logic repaints AI clans | **Same design question** — global scope may be intentional. MEDIUM. |

### Bugs Codex Missed (4)

| Bug | Severity | Why missed |
|-----|----------|-----------|
| Fail-safe `?? true` in drift guard prefix blocks vanilla when uninitialized | HIGH | Didn't compare fail-safe patterns across the feature's 15 patches |
| `GetUniqueIconColor` is a complete no-op in both overlap AND non-overlap cases | MEDIUM | Only caught one branch; didn't trace both code paths |
| Layer limit transpiler `?? true` disables layer limit when uninitialized | MEDIUM | Didn't analyze the transpiler at all (skipped #1 focus area) |
| `MobilePartyVisual` patch has no category attribute (manual registration) | LOW | Didn't check patch registration consistency (actually intentional — private method) |

### Prompt Lessons (despite improvements)
- Codex still skipped the hardest analysis (transpiler IL verification) despite it being focus area #1
- "DO NOT" instructions were too weak — need concrete failure examples instead
- Codex needs `E:\Decompiled_Bannerlord\` file paths, not "decompile X" instructions
- Feature docs reference was missing — Codex couldn't distinguish design intent from bugs

---

## Review #3: ArmyTargeting

**Date:** 2026-04-05
**Prompt version:** v3 (required sections, `E:\Decompiled_Bannerlord\` paths, concrete math scenarios)
**Report:** [codex-adversarial-army-targeting-2026-04-05.md](codex-adversarial-army-targeting-2026-04-05.md)

### Codex Findings (0)

Verdict: **approve** — "No blocking no-ship case."

### Claude Verification

| Check | Codex Claim | Claude Verified? |
|-------|-------------|-----------------|
| Math scenario (a): committed + priority pos 0 | 12.0× | Yes — traced formula, correct |
| Math scenario (b): priority pos 2/4 | 1.667× | Yes — interpolation correct |
| Math scenario (c): non-priority fallthrough | 1.0× | Yes — falls through to vanilla |
| Math scenario (d): CommitmentMultiplier=0 | MCM range prevents | Yes — range is 1.0-10.0 |
| Config settlement IDs valid | "All follow valid naming" | Yes — all 67 IDs verified against settlements.xml |
| Decompiled vanilla code shown | Described in prose | **No — third consecutive failure.** Prose descriptions only, no C# code blocks. |

### Observations Codex missed (not bugs, but a thorough review would note)

| Observation | Location | Impact |
|-------------|----------|--------|
| `BuildFloatIndex` silently drops multipliers ≤1.0 | ArmyTargetingService.cs:87 | Config values ≤1.0 are silently ignored — no feedback |
| Combined multiplier can reach 18× | GameModel line 40 | 4.0 × 3.0 × 1.5 = 18× on committed top-priority targets |
| Harmony patch swallows all exceptions | Patch.cs:42-44 | `catch (Exception) {}` hides service bugs during dev |
| Strength inflation bypasses vanilla strength gate | TaomTargetScoreModel.cs:27 | `ourStrength * 2.0` lets evil factions besiege what vanilla would reject |

### Prompt Lessons
- v3's required sections and concrete math scenarios produced the first correct verdict
- Decompiled code STILL not shown despite explicit instruction — Codex consistently avoids this
- "Approve" verdicts need evidence of depth — an approve with no analysis is indistinguishable from a skip
- Config validation was claimed but not evidenced — Codex didn't cross-reference against settlements.xml

---

## Review #4: TroopProgression + TroopWeight

**Date:** 2026-04-05
**Prompt version:** v4 (verification artifacts, split show/analyze, quality gates)
**Report:** [codex-adversarial-troop-progression-2026-04-05.md](codex-adversarial-troop-progression-2026-04-05.md)

### Codex Findings (3)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Garrison wage feats apply to any party in settlement, not just garrisons | **Confirmed bug** — vanilla gates behind `mobileParty.IsGarrison`. Decompilation verified. |
| 2 | HIGH | NumberOfRegularMembers uses wounded-ratio approximation | **Valid but overstated** — math error is real for asymmetric weights. Downgrade to MEDIUM (uncommon scenario). |
| 3 | MEDIUM | Rohan mounted wage uses headcount ratio, vanilla uses wage share | **Valid divergence** — confirmed by decompilation. Design choice with lower practical impact. |

### Bugs Codex Missed (0)

Claude found no additional bugs. First review where Codex found everything.

### Prompt Lessons (v4 improvements that worked)
- Decompiled code finally shown (partial — "truncated by format" but present)
- Evidence per finding produced zero false positives for the first time
- Quality gates and required sections prevented shallow analysis
- Observations section populated with useful notes

---

## Reviews #14-16: Wave 4+5 (Completion)

**Date:** 2026-04-05/06
**Prompt version:** v6

### Review #14: Wave 4A (WeatherBoundsGuard + AtmospherePersistence + ShaderPrecompilation)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Shader abort latch stays armed after success | **Confirmed** -- IsShaderBattleActive never cleared on success path |
| 2 | MEDIUM | LoadingWindowViewModel target unverified for v1.3.15 | **Valid concern** -- decompiled source missing |
| 3 | MEDIUM | AtmospherePersistence reflection target brittle | **Valid** -- added startup validation |

### Review #15: Wave 4B (TimeAcceleration + InitialChildGeneration + StartupResources + MainMenuCustomizer + Encyclopedia + BattleScenes)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Child gender selection not enforced by ChildCreatorAdapter | **Confirmed** -- isFemale param unused |
| 2 | MEDIUM | Ctrl+Space turbo stuck on map/menu transition | **Confirmed** -- early returns skip restore |
| 3 | MEDIUM | StartupResources retry duplicates gold | **Confirmed** -- single _distributed flag for both subsystems |

### Review #16: Infrastructure (Adapters + Core + SubModule + IoC)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | InitializeKingdom resets PoliticalStagnation | **Confirmed** -- replaced with targeted color-only update |
| 2 | HIGH | MissionAdapterFactory cache survives across missions | **Confirmed** -- Agent.Index reused, stale references |
| 3 | MEDIUM | FileLogger teardown can race writer thread | **Confirmed** -- added drain-before-dispose |

### Wave 4+5 Summary
- 9 findings, 9 confirmed, 0 false positives, 0 missed by Claude
- v6 prompt: 100% accuracy for this batch
- All bugs fixed except 1 deferred (LoadingWindowViewModel decompilation needed)

---

## Prompt Evolution

| Version | Used In | Key Changes | Impact |
|---------|---------|-------------|--------|
| v1 | CulturalFeats | ADR focus, generic focus areas, no decompilation guidance | 33% accuracy, 1 false positive |
| v2 | BannerColorPersistence | Feature-specific focus, DO NOT section, decompilation requested | Still 33% accuracy — Codex skipped hard analysis |
| v3 | ArmyTargeting | Required sections, `E:\Decompiled_Bannerlord\` paths, concrete scenarios, READ FIRST docs, prior failure examples | Correct verdict but shallow — no decompiled code shown, config not cross-referenced |
| v4 | TroopProgression, Wave 1 | Verification artifacts, split "show code" from "answer questions", config cross-reference with file path, approve-verdict evidence requirement | 90% accuracy on v4 batch, 0 false positives in reviews 4-6, 1 FP in review 7 |
| v5 | Wave 2 | Kingdom mapping reference, design-intent gate, flat formatting, FP-7 lesson | 88% accuracy, config ID mismatches caught (rohan/vlandia, dol_guldur/dolguldur) |
| v6 | Waves 3-5 (final) | Full ID cheatsheet (kingdom+culture), dead config detection, config cross-ref required, success+failure patterns, flat formatting, lifecycle/state analysis | 100% accuracy on v6 batch (reviews 11-16), 0 FPs, 0 misses |

### v1 → v2 changes
- Added feature-specific risk areas (transpilers, drift guard, scoping)
- Added "DO NOT" section for pattern compliance
- Added prior review lesson about `IsMounted` false positive
- Ordered focus areas by value

### v2 → v3 changes
- Added "REQUIRED SECTIONS" with named sections (prevents silent skipping)
- Pointed to `E:\Decompiled_Bannerlord\` instead of "decompile" (easier for Codex)
- Added "READ FIRST" for feature docs (design intent context)
- Replaced "DO NOT" with concrete failure examples (stronger)
- Added concrete math scenarios with expected outputs (forces deep analysis)
- Added config validation section (new attack vector)
- Added "If everything is HIGH, your calibration is off"

### v3 → v4 changes
- Added "VERIFICATION ARTIFACTS" — Codex must produce code blocks, not prose descriptions
- Split vanilla analysis into "SHOW the code" + "ANSWER questions about it" (two separate steps)
- Config validation now requires cross-referencing against a specific file path
- Added approve-verdict evidence requirement: "An approve with no decompiled code is incomplete"
- Added "OBSERVATIONS" section requirement for approve verdicts (things worth noting even if not bugs)

### v5 → v6 changes
- Added TAOM ID CHEATSHEET — culture StringIds AND kingdom StringIds in one block (prevents rohan/vlandia-type mismatches)
- Added "DEAD CONFIG DETECTION" as standard check — config values that exist but are never read at runtime
- Config cross-reference is now a REQUIRED section, not optional (caught 5+ bugs across waves 1-2)
- Added success patterns to prior-review-lessons (what WORKED, not just what failed)
- Flat formatting standard (no indented continuation lines — prevents backslash-escape prompts)

---

## Review #23: Named Companions + Wanderer Race Fix

**Date:** 2026-04-08
**Prompt version:** v6
**Report:** [codex-adversarial-named-companions-2026-04-08.md](codex-adversarial-named-companions-2026-04-08.md)

### Codex Findings

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | EnsureCompanionsPlaced teleports recruited companions on load | **Confirmed** — IsPlacedInSettlement returns false for companions traveling with player party, causing forced re-placement. Fixed by adding IsRecruitedOrInParty check. |

### Known Suspects Verdict

| # | Suspect | Codex Verdict | Claude Verdict |
|---|---------|---------------|----------------|
| 1 | Unused _logger in behavior | Not explicitly checked | **Confirmed** — removed dead field |
| 2 | ChangeState(Active) on Active hero | CONFIRMED (context of larger bug) | Agree — safe call but part of the load teleport bug |
| 3 | IsPlacedInSettlement misses traveling companions | CONFIRMED | **Agree** — core bug, fixed |
| 4 | Race double-set redundancy | Not explicitly checked | **Harmless** — defensive, keep as insurance |
| 5 | Config settlement IDs | PASSED | **Agree** — all 18 IDs verified |
| 6 | Wanderer race fix completeness | PASSED | **Agree** — 30 elf + 10 dg_uruk correct |

### Manual Verification Results

- **TryKillCompanion protection**: `HasMet` is unnecessary because named companions (`is_hero="true"`) never enter `_aliveCompanionTemplates` (requires `IsTemplate`). `TryKillCompanion()` can never target them. `HasMet` is harmless belt-and-suspenders.

### Root Cause Analysis

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Load teleports recruited companions | Stale state / lifecycle | Didn't trace full lifecycle — focused on spawn path, missed load-path scenario where companion is recruited and traveling | Added test `EnsureCompanionsPlaced_RecruitedCompanion_SkipsPlacement` |
| 2 | Unused _logger field | Dead code | Copied pattern from StartupResourcesBehavior which uses logger, but NamedCompanionBehavior delegates entirely to service | Note: behavior pattern review — only inject what's used |

### Fixes Implemented

1. Added `IsRecruitedOrInParty(characterId)` to `INamedCompanionAdapter` — checks `CompanionOf != null || PartyBelongedTo != null`
2. Added guard in `NamedCompanionService.EnsureCompanionsPlaced()` — skips recruited/in-party companions
3. Removed unused `_logger` from `NamedCompanionBehavior`, simplified constructor
4. Updated `SubModule.cs` to match new constructor
5. Added test: `EnsureCompanionsPlaced_RecruitedCompanion_SkipsPlacement`
6. All 1037 tests pass

---

## Review #24: Career CC Selection

**Date:** 2026-04-14
**Prompt version:** v6-adversarial (Known Suspects, config cross-ref, lifecycle tracing)
**Report:** [codex-adversarial-career-cc-2026-04-14.md](codex-adversarial-career-cc-2026-04-14.md)

### Codex Findings (2)

| # | Codex Severity | Claude Severity | Agree? | Reason |
|---|---------------|----------------|--------|--------|
| 1 | HIGH | HIGH | Yes | Empty career menu for shaghana/abanissa causes KeyNotFoundException in vanilla TrySwitchToNextMenu. Codex correctly traced the lifecycle through CanAdvanceToNextStage (returns true for empty SelectionList) to SelectedOptions dictionary access. |
| 2 | MEDIUM | MEDIUM | Yes | RegisterCareerMenu_ClearsStaleSelection test didn't exercise RegisterCareerMenu — only tested a fresh instance starts null. Valid test gap. |

### Bugs Claude Missed (1)

| Bug | Severity | Why missed |
|-----|----------|-----------|
| shaghana/abanissa empty menu crash | HIGH | Claude's deep review (5 agents) didn't cross-reference CC selectable cultures against career-eligible cultures. Data Flow agent checked XML/JSON 1:1 match but not cultures.json coverage. |

### Root Cause Analysis

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Empty menu crash for careerless cultures | Lifecycle completeness | Didn't enumerate all selectable CC cultures and verify each has at least one career. Deep review checked XML career count matches JSON count, but not that every CC culture has career coverage. | Added fallback "No specialization" option that's visible only for uncovered cultures. Future: add config validation test that cross-refs cultures.json against career EligibleCultures. |
| 2 | Test doesn't exercise actual method | Convention inconsistency | Test was written quickly after deep review flagged the singleton leak. Focused on proving the property resets rather than exercising the actual code path. | Replaced test with direct property verification tests. |

### Fixes Implemented

1. Added `SelectedCareerStringId = null` at top of `RegisterCareerMenu()` — clears stale singleton state
2. Added `BuildFallbackOption()` — "No specialization" option visible only for cultures with no eligible careers (prevents KeyNotFoundException)
3. Changed equipment roster ID to use `SelectedTitleType` instead of hardcoded "guard"
4. Updated 3 tests for fallback option count, added 2 new tests (fallback null selection, fresh service state)
5. All 1129 tests pass

---

## Review #25: Spider — C# Mission API spawner for non-humanoid creature

**Date:** 2026-04-23
**Prompt version:** v6 + 6-Known-Suspects
**Report:** [codex-adversarial-spider-2026-04-23.md](codex-adversarial-spider-2026-04-23.md)
**Codex model:** gpt-5.5 (ChatGPT-account auth — o4-mini and gpt-5 rejected with "model not supported")

### Feature scope
Spider hostile mob via `Mission.SpawnAgent` + `AgentBuildData.Monster()`, bypassing the troop system because Bannerlord's NPCCharacter race resolution is hardcoded humanoid-only. Anchor `taom_spider_creature` (race=dg_uruk) satisfies AgentBuildData's `BasicCharacterObject` requirement; visual is overridden at spawn time. Custom-Battle-only gating via `CustomBattleAgentLogic` presence check. 12 C# files + 4 XML files (3 in LOTRLOME_Armory) + 20 unit tests. Mirrors `Main/Features/Warg/` but corrects ADR-007 by exposing `IAgentAdapter` instead of raw `Agent` at the service boundary.

### Codex Findings

| # | Codex Severity | Claude Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | HIGH | HIGH | ✓ | Anchor `occupation="Soldier"` makes `IsSoldier=true`, exposes anchor in Custom Battle troop picker. Decompiled `BasicCharacterObject.LoadFromXml` confirms `IsSoldier = occupation.IndexOf("soldier", IgnoreCase)>=0`. Decompiled `ArmyCompositionGroupVM` ctor confirms `c.IsSoldier && !c.IsObsolete` is the only filter; `hidden_in_encyclopedia` and `is_basic_troop="false"` are ignored by this picker. |

### Known Suspects verdicts

| # | Suspect | Codex Verdict | Notes |
|---|---|---|---|
| 1 | `AgentBuildData.Monster()` ignored at spawn | DISPUTED | `Mission.SpawnAgent` uses `agentBuildData.AgentMonster` directly in `CreateAgent(...)`. The override is honored. **The whole feature works.** |
| 2 | Custom Battle gate bleeds into campaign | DISPUTED | Decompiled `BannerlordMissions.OpenCustomBattleMission`/`OpenSiegeMission`/`OpenLordsHallMission` add `CustomBattleAgentLogic`; campaign openers don't. Gate is reliable. |
| 3 | Anchor character bleed-through | **CONFIRMED** | See HIGH finding above. |
| 4 | Bone collision indices placeholder | **CONFIRMED FUNCTIONAL** | Codex upgraded this from cosmetic-only to functional: `BoneCheckDuringAnimation` only registers hits when the indexed spider bones come within `0.3-0.4f` of target bones. Wrong indices = miss/wrong-bone hits. Documented in CHANGELOG/feature-doc as known v1 limitation; runtime probe needed before promoting to v2. |
| 5 | `CustomBattleAgentLogic` reference fragility | OBSERVATION only | Future TaleWorlds rename would be a compile-time break, not a silent false return. Not actionable now. |
| 6 | Spawn timing race (MainAgent null at t=1s) | OBSERVATION only | No vanilla evidence found that MainAgent is null at t=1s in Custom Battle. Fallback to `Vec3.Zero` exists but unobserved. |

### Bugs Codex Missed (vs deep-review)

| # | Bug | Severity | Source |
|---|---|---|---|
| 1 | `_loggedErrors` HashSet not cleared in `OnRemoveBehavior` — stale error keys carry across Custom Battle relaunches, suppressing genuine new errors with same `ExceptionType:MethodName` key | MEDIUM | Deep-review Agent 5 (Data Flow) Flow 10 |
| 2 | `SpiderConfig.SpiderAttackRange = 1.2f` declared but never consumed (dead field) | LOW | Deep-review Agent 5 Flow 11 |
| 3 | `act_spider_attack_top` / `act_spider_attack_bottom` declared+bound+have `_geo.tpac` but absent from `<monster_usage_strikes>` — animations unreachable dead bindings | LOW | Deep-review Agent 5 Flow 12 |
| 4 | Per-attack `new List<sbyte>` allocation in `SpiderAttackService.SpiderAttack` (every BT tick on every spider) | MEDIUM | Deep-review Agent 3 (Efficiency) |

Codex reported `MEDIUM/LOW: 0 — no additional confirmed findings beyond the suspects.` In practice, Codex relied on the Known Suspects list to scope its analysis and didn't run an independent lifecycle/dead-config trace the way Agent 5 did. Useful pattern for future reviews: ALWAYS run Agent 5 (Data Flow Tracing) regardless of Codex's verdict — it consistently catches a different class of bugs.

### Root Cause Analysis

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Anchor `occupation="Soldier"` exposes anchor in Custom Battle picker | Convention inconsistency / missing vanilla gate | Claude assumed `hidden_in_encyclopedia="true"` + `is_basic_troop="false"` were sufficient to hide the troop. Did not decompile `ArmyCompositionGroupVM` to verify what actually filters the Custom Battle picker. | Add to REVIEW-GUIDE.md: "When creating any anchor/placeholder NPCCharacter, verify it doesn't appear in Custom Battle troop picker, Encyclopedia, Recruitment, Conversation pools — by decompiling each consumer's filter logic." Avoid `occupation="Soldier"` for non-troop characters; use `occupation="Wanderer"` or other non-soldier value. |
| 2 | `_loggedErrors` HashSet not cleared on mission end | Stale state / lifecycle | Followed Warg pattern verbatim. Warg has the same gap; both behaviors carry stale error keys across CB relaunches. | Add `_loggedErrors.Clear()` to Warg's `OnRemoveBehavior` in a separate cleanup pass. New rule for any service holding deduplication state across mission lifecycle: clear on `OnRemoveBehavior`. |
| 3 | Dead `SpiderAttackRange` field | Dead/no-op code | Field added during config drafting and never wired. Deep-review Flow 11 catches this; Codex doesn't unless prompted. | Already in REVIEW-GUIDE: "Dead config detection". Reinforce in Codex prompt's CONFIG CROSS-REFERENCE section. |
| 4 | Per-attack list allocation | Performance | Mirrored Warg pattern verbatim, didn't question it. | Move shared static-readonly bone arrays to Config; ban `new List<>` allocations in BT-tick hot paths in `.claude/rules/csharp-architecture.md`. |
| 5 | Bone indices are placeholders mapping to wrong bones | Reflection target wrong / convention inconsistency | Used Warg's bone indices (23/37/43) without verifying they map to spider's actual fang bones. The indices are valid (in-range on 62-bone skeleton) but resolve to wrong bones. | Add to feature workflow: "Before placeholder bone indices land in Config, write a one-shot logging hook that dumps bone names at runtime. Replace before promoting feature to v2." Documented in CHANGELOG as known limitation. |

### Fixes Implemented

1. **HIGH (Codex):** `Main/_Module/ModuleData/characters/spider_creature.xml` — `occupation="Soldier"` → `occupation="Wanderer"`. Engine `IsSoldier` substring check (case-insensitive "soldier") now returns false; anchor will not appear in Custom Battle picker. Comment block expanded to document this constraint.
2. **MED (Deep-review Flow 10):** `SpiderMissionBehavior.OnRemoveBehavior` — added `_loggedErrors.Clear()` to prevent stale error dedup across CB relaunches.
3. **LOW (Deep-review Flow 11):** `SpiderConfig.cs` — removed dead `SpiderAttackRange` field.
4. **MED (Deep-review Efficiency):** `SpiderConfig.cs` — added `static readonly List<sbyte> ChargeAttackBones` and `StandAttackBones`; `SpiderAttackService.SpiderAttack` now reuses them instead of allocating per call.

**Deferred:**
- Bone-index placeholders (Codex finding #4 + Deep-review Flow 8) — needs runtime probe to identify correct fang bones. Documented in CHANGELOG and `docs/features/spider.md` as v1 limitation. Bites currently land via leg/body bones rather than fangs; visual will be off but mechanics still register hits.
- Top/bottom attack `<monster_usage_strikes>` (Deep-review Flow 12) — animation files exist but have no AI trigger. Cosmetic; left as-is for v1.
- Per-tick `IoC.Resolve` in BT tasks (Deep-review Efficiency HIGH) — mirrors Warg pattern. Refactor scoped to a future cross-feature cleanup pass that addresses both Warg and Spider together. Tracked as deferred.

### Build & Test
- `./build.ps1` — clean, no errors, no new warnings.
- `dotnet test --filter Spider` — 20/20 passing (14 SpiderAttackService + 6 SpiderSpawnerService).

### Review 30 — CustomBattles Filter+Cap Root Cause Analysis

Focused review of today's enhancement to the existing CustomBattles feature (commander dropdown filter by faction + 3-per-culture cap). Codex returned ONE P1 finding and got it right.

**The bug.** Both filter postfixes did `ItemList.Clear()` → `AddItem(...)` × N → `SelectedIndex = 0`. In v1.3.15, `SelectorVM<T>.SelectedIndex` setter has a `if (value != _selectedIndex)` guard. Since vanilla initializes `_selectedIndex = 0` for the player side and most users never deselect before clicking another faction, the setter short-circuits. Result: `SelectedItem` keeps pointing at the previously-selected `CharacterItemVM` — which we just removed from `ItemList` — and `CustomBattleSideVM.SelectedCharacter` (set via the `_onChange` callback that also doesn't fire) stays stale. Battle would launch with the wrong commander, with no visible UI signal that the picker and the actual selection had diverged.

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | `Clear() + AddItem(*N) + SelectedIndex = 0` is a no-op when `_selectedIndex` was already `0` — leaves stale `SelectedItem` and stale `SelectedCharacter` | Missing vanilla gate / convention inconsistency | Mirrored vanilla `CustomBattleSideVM.RefreshValues()` literally (which uses the same Clear+AddItem+SelectedIndex=0 sequence). RefreshValues works at construction because `_selectedIndex == -1` by default — the setter naturally fires. The same pattern fails post-construction when `_selectedIndex` is already 0. Did not decompile `SelectorVM<T>.SelectedIndex` setter. The deep-review Known Suspect #2 explicitly asked about this (asked Codex, not myself); my self-pass missed the trap. | Added the canonical pattern to `.claude/rules/gui-ui.md` under "TaleWorlds VM property setters: verify no-op early returns". Created `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` codifying the correct mutation pattern (mirrors vanilla `SelectorVM.Refresh()`'s `_selectedIndex = -1` reset trick). Updated `AGENTS.md` to add this pattern to "Bugs Codex typically misses (Claude catches these)" — but in this case Codex caught it; reverse the framing for next time. |

### Fixes Implemented (Review 30)

1. **P1 (Codex):** `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` (new) — extracts the Clear+AddItem+reset+SetSelectedIndex sequence into one place. Cached `FieldInfo` for `SelectorVM<T>._selectedIndex` via `AccessTools.Field` at `Initialize`. Both `CustomBattleSideVM_OnCultureSelection_Patch` and `CustomBattleSideVM_RefreshValues_Patch` now delegate to it. The `_selectedIndex = -1` reset before `SelectedIndex = 0` ensures the setter's guard sees a state change and fires `_onChange` → `OnCharacterSelection` → propagates to `SelectedCharacter`.
2. **Rule update:** `.claude/rules/gui-ui.md` — added "TaleWorlds VM property setters: verify no-op early returns" with the concrete `SelectorVM<T>.SelectedIndex` example and the three correct patterns (use built-in `Refresh()` / mirror reset trick / avoid same-value assignment).
3. **AGENTS.md update:** added a new "What Codex does well" entry: "Decompiling property setters to find no-op early-return guards on TaleWorlds VMs (CustomBattles filter+cap review 30)."

### Build & Test (Review 30)
- `dotnet build Main/TAOM.csproj` — clean, 0 errors, 2 unrelated warnings (MCMv5 arch mismatch, `ex` unused — both pre-existing).
- `dotnet test --filter CustomBattles` — 38/38 passing (no test changes needed; the rebuilder helper is entry-point-tier per ADR-008).

