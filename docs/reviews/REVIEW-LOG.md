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
| 17 | 2026-04-07 | SpecialResources (adversarial) | needs-attention | partial-agree | 2 confirmed (sprite, storage) | 1 (kingdom_id) | 0 | v6-adversarial |
| 18 | 2026-06-24 | SaveLoadTableauGuard (#299) | issues-found | agree | 1 confirmed (CRITICAL patch-apply timing) + 1 LOW | 0 | 0 | adversarial-xhigh |
| 19 | 2026-06-25 | ShaderPrecompile Phase 0 (#287) | ship | agree | 1 LOW (doc test-name drift) — confirmed all 6 suspects + the disputed boundary-direct-read non-defect | 0 | 0 | adversarial-xhigh |
| 20 | 2026-06-26 | Career phantom-passive wiring | clean/ship | agree | 0 — CLEAN; 7/8 suspects DISPUTED + 1 PARTIAL (Damage stage-move, non-defect); corroborated the 6-dim in-house deep-review (0 HIGH/6 MED/3 LOW, all fixed) | 0 | 0 | adversarial-xhigh |
| 21 | 2026-07-06 | TournamentExitHang #331 (exit diagnostics + Patch60) | issues-found | agree | 1 confirmed P2 — hook-level `IsEnabled` gates bypassed the deep-review fix's unconditional window-closers (fix verified only at the service layer); S1-S6 suspects all resolved with decompiles | 0 | 0 | adversarial-xhigh |
| 22 | 2026-07-10 | TournamentExitHang #331 ROUND 2 (ExitStallSampler + PatchShield exclusion) | issues-found | agree | 2 P2 (Timer reentrancy on Poll; no independent sampler toggle) + 4 P3 (suspended-window logging, main-thread invariant [deferred+documented], 2 drift) — all addressed; deep-review compat agent separately caught the false ctor comment + the still-shielded Patch38 hot target | 0 | 0 | adversarial-xhigh |

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
| 21 | 2026-04-07 | CareerSystem (impl) | needs-attention | partial-agree | 2 confirmed (tier validation, save serialization) + 1 partial (widget def) | 3 (mutation scope, ability scope, passive coverage — intentional v1 scope) | 1 (hallucinated AllowedRaces property) | v6-adversarial |
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
| 32 | 2026-05-04 | CustomBattles NRE+diagnostic (Prefix guard + Refresh-based rebuilder + Phase 2A equipment-slot diagnostic + LOW fix-loop) | NEEDS-FIXES (1 P2 / 1 P3) | agree | 2 confirmed (P2: vanilla `RefreshValues` calls `UpdateCharacterVisual` after our Prefix skipped OnCharacterSelection — sister NRE; P3: diagnostic catch logged `ex.Message` only, lost type+stack) | 0 | 0 | v6-focused-enhancement |
| 60 | 2026-06-17 | LotrIssues Wave 0 (vanilla-quest LOTR conversion: framework + suppress-all-43 + T1 DeliverGoods) | ISSUES FOUND (0C/0H/2M) | agree | 2 confirmed (M1: provider accepts `category:` source no Wave-0 template resolves → silent no-spawn; M2: trimmed vanilla refresh events → stale food-deliverable progress) — all 8 Known Suspects DISPUTED/PARTIAL, framework confirmed correct (dispatch/load-order/stay-alive/saveable-id) | 0 | 0 | v6-adversarial |
| 61 | 2026-06-20 | LotrIssues full feature (Combat + DeliverPersonnel templates, 43 configs, localization) | ISSUES FOUND (0C/1H/1M/1L) | agree | 3 confirmed: H1 `IssueQuestCanBeDuplicated`=>false caps player at 1 active quest per shared template type (5-agent deep-review found the spawn throttle but missed this accept gate); M1 Combat `variant` unvalidated → typo silently routes to DefeatRaids; L1 limitation doc omitted DeliverPersonnel bucket — all fixed in-session. 9 of 11 Known Suspects DISPUTED (re-entrancy/save-fields/saveable-ids/signatures/config/loc all verified correct) | 1 (the H1 accept-gate, vs the deep-review) | 0 | v6-adversarial |
| 71 | 2026-07-04 | CaravanTrade (4 postfixes on `CaravansCampaignBehavior` + 2 `TaomCaravanModel` overrides; range/war-gate/diversity levers; #329) | ISSUES FOUND (0C/0H/4M/1L) | agree | 5 confirmed: C1 range lever mutated the `_*VeryFarCache` fields once → mid-session master-off didn't revert (moved to a scale-on-read getter postfix); C2 range lever engine-global, can't be player-scoped (doc'd honestly); C3 war-gate isPlayer faction-level vs clan-level others (doc'd); C4 player-founded kingdom resolves Neutral → trades across the Free/Evil line (culture fallback, same class as review 70); L1 enum doc drift. 5 of 7 Known Suspects DISPUTED incl. Codex disproving my seeded player-detection hypothesis by decompiling the caravan-creation path. Deep-review already caught+fixed 1 HIGH (AreEnemyAlignments Neutral inversion) pre-Codex. Rows 62–70 tracked in AGENTS.md + per-feature RCAs. | 0 | 1 (the deep-review's own HIGH, pre-Codex) | v6-adversarial |

**Post-codebase reviews:** 19-35. 35 Codex reviews total, 96 bugs found across codebase.

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

Recursive review: Codex reviewed our fixes from review #26. Verdict NEEDS FIXES (0 HIGH, 1 MEDIUM, 3 LOW + 2 process violations). Self-review file at `docs/archive/codex-reviews-2026-04/codex-selfreview-tier1-fixes-2026-04-26.md`.

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

Review file: `docs/archive/codex-reviews-2026-04/codex-adversarial-tier2-3-2026-04-26.md`. Issue: #94.

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
**Report:** `codex-adversarial-cultural-feats-2026-04-05.md` (not archived)

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
**Report:** `codex-adversarial-banner-color-persistence-2026-04-05.md` (not archived)

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
**Report:** `codex-adversarial-army-targeting-2026-04-05.md` (not archived)

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
**Report:** `codex-adversarial-troop-progression-2026-04-05.md` (not archived)

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
**Report:** `codex-adversarial-named-companions-2026-04-08.md` (not archived)

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
**Report:** `codex-adversarial-career-cc-2026-04-14.md` (not archived)

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
**Report:** `codex-adversarial-spider-2026-04-23.md` (not archived)
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

### Review 32 — CustomBattles NRE+Diagnostic Root Cause Analysis

Adversarial review of two commits (`a9e0bba` NRE+diagnostic, `25415b1` deep-review LOW fix-loop). Codex returned 2 findings, both confirmed.

**The bugs.**

P2 (HIGH): The previous commit added a Prefix on `CustomBattleSideVM.OnCharacterSelection` returning `false` when `selector.SelectedItem == null`. The Prefix skipped the vanilla body — but vanilla `RefreshValues()` calls `UpdateCharacterVisual()` UNCONDITIONALLY immediately after the SelectedIndex assignment that fired the now-skipped OnCharacterSelection. `UpdateCharacterVisual` derefs `SelectedCharacter.Equipment[(EquipmentIndex)5]` directly. Since the OnCharacterSelection Prefix skipped the body, `SelectedCharacter` was never set — it remained null at construction. NRE moved one method down the call chain, exactly when the Prefix was supposed to prevent it.

P3 (LOW): The Phase 2A equipment-slot diagnostic wrapped per-commander reads in `try/catch` but logged only `ex.Message`. For a diagnostic specifically meant to identify equipment-resolution failures, the exception type and stack frame are as valuable as the slot-by-slot output.

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | OnCharacterSelection Prefix skips body, but vanilla RefreshValues continues into UpdateCharacterVisual which derefs SelectedCharacter | Missing vanilla gate / partial blast-radius mitigation | When patching a callback to skip on bad state, I traced the callback's body but did NOT trace what the caller does AFTER the callback returns. Vanilla RefreshValues has SelectedIndex setter call -> OnCharacterSelection (Prefix skipped) -> UpdateCharacterVisual (still runs, NREs). Defensive guards must cover the full call chain a callback participates in, not just the callback itself. | Added new `AGENTS.md` "What Codex does well" entry codifying this pattern. Future defensive Prefixes that target callback methods must enumerate ALL methods the caller invokes (in vanilla source) using the same state, and patch each that derefs the now-unset state. The new sister Prefix on `UpdateCharacterVisual` extends the guard to cover the full RefreshValues call chain. |
| 2 | Diagnostic catch logged `ex.Message` only; lost type and stack | Logic error / convention inconsistency | Mirrored the existing pattern from other TAOM catch blocks (most use `ex.Message`). For PRODUCTION error handling, message-only is fine; for DIAGNOSTIC code that exists specifically to identify failures, full `ex.ToString()` is required. The two have different needs but I copied the wrong template. | Code-level fix: switched to `ex.ToString()` for the diagnostic-specific catch. Generalizable rule: when wrapping a try/catch around code whose ENTIRE PURPOSE is producing diagnostic output, log full exception, not just message. |

### Fixes Implemented (Review 32)

1. **P2 (Codex):** New `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_UpdateCharacterVisual_Patch.cs` — Prefix returns `false` when `__instance.SelectedCharacter == null`. Patch count rises to 10. The OnCharacterSelection Prefix from `a9e0bba` is preserved (kills the OnCharacterSelection NRE); the new sister Prefix kills the cascading UpdateCharacterVisual NRE that vanilla RefreshValues triggers regardless.
2. **P3 (Codex):** `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` LogEquipmentDiagnosticOnce catch — switched `{ex.Message}` to `{ex}` (full ToString). Comment explains why this is intentional for diagnostic-only catch.

### Build & Test (Review 32)
- `dotnet build Main/TAOM.csproj` — clean.
- `dotnet test --filter CustomBattles` — 38/38 passing (no test changes; defensive Prefixes are entry-point-tier per ADR-008).


### Review 33 — CharacterCreation Race Filter (Patch9_RaceFilter re-implementation)

Adversarial review of the new culture-restricted race-dropdown feature: filter service, FaceGenVM rebuilder (reflection-heavy), and `SetPlayerRace` finalize logic. Codex returned 2 findings; both confirmed.

**The bugs.**

F1 (HIGH): `FaceGenRaceSelectorRebuilder.Apply` mutated the private `_raceSelector` field via reflection, then attempted to fire the property-change notification by reflectively invoking `OnPropertyChangedWithValue(object, string)` on `FaceGenVM`. The actual method on the `ViewModel` base is generic `OnPropertyChangedWithValue<T>(T, string) where T : class`. `AccessTools.Method` looking up by `(typeof(object), typeof(string))` returns `null` (verified by Codex: `AccessTools.Method(FaceGenVM, "OnPropertyChangedWithValue", object, string) => NULL`). The notification never fires; Gauntlet's `GauntletView` does not call `RefreshBindingWithChildren()`; the dropdown UI stays bound to the prior unfiltered selector. First-construction can mask this because `BodyGeneratorView.LoadMovie("FaceGen", DataSource)` reads the field directly after construction — but any subsequent `Refresh(true)` (every race change, every FaceGen reopen) silently rebinds to vanilla's full selector.

F2 (MEDIUM): `RaceManager.GetRaceNameFromId` falls back to `"human"` for unknown IDs (a documented warning-and-default). `SetPlayerRace` accepted that fallback name, checked it against the culture's allow-list, and — for cultures that allow `human` — preserved the original invalid integer. `Hero.CharacterObject.Race` accepts arbitrary integers; downstream engine calls (`FaceGen.GetBaseMonsterFromRace`, body property generation) would receive a junk race ID for cultures like Mordor (allow-list = `[uruk, orc, human]`).

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| F1 | Reflected generic method without `MakeGenericMethod` produces unusable MethodInfo (returns `null`); UI rebinding silently no-ops | Reflection target wrong | Reached for reflection because `_raceSelector` is private — but the corresponding public property setter `RaceSelector { set; }` was right there, would fire the notification correctly, and was simpler. Pattern: when a private field has a public property wrapping it, the property is the right knob; only reflect if the property doesn't exist. | Code-level fix: `faceGenVM.RaceSelector = newSelector` replaces the field-mutation + reflection-notify pair. Removed `_raceSelectorField` and `_onPropertyChangedWithValueMethod` static caches. Generalizable rule (added to AGENTS.md): before reflecting on a private field-setter operation, search for a public property that already wraps it. |
| F2 | Validator-fallback masks invalid input as a "valid" choice for cultures that allow the fallback | Missing null/invalid guard | Trusted `GetRaceNameFromId` to return a meaningful string per ID. Did not read `RaceManager.cs` carefully enough to notice the silent "human" fallback. Pattern: when state from an entity feeds an allow-list comparison, validate the state's validity *before* using it as the comparison key. | Code-level fix: gate on `_raceManager.IsValidRaceId(faceGenRaceId)` before resolving the name. Added regression test `SetPlayerRace_InvalidFaceGenRaceId_DoesNotPreserve_FallsBackToCultureDefault`. Generalizable rule (added to AGENTS.md): when a lookup function returns a fallback value for invalid input, the caller MUST validate before the lookup; the fallback is for logging-and-survival, not for security decisions. |

### Fixes Implemented (Review 33)

1. **F1 (Codex):** `FaceGenRaceSelectorRebuilder.Apply` now uses `faceGenVM.RaceSelector = newSelector` instead of `_raceSelectorField.SetValue + _onPropertyChangedWithValueMethod.Invoke`. The cached `_raceSelectorField` and `_onPropertyChangedWithValueMethod` static fields removed.
2. **F2 (Codex):** `CharacterCreationContentService.SetPlayerRace` now gates `faceGenChoiceAllowed` on `_raceManager.IsValidRaceId(faceGenRaceId)`. Three existing `SetPlayerRace` tests updated to stub `IsValidRaceId(...).Returns(true)`. New regression test added.
3. **Process note:** Codex went off-scope mid-review and started implementing a separate `Patch29_CCBodyProperties` feature (per-culture default body properties, applied on culture-select). Those changes were preserved (not part of the race-filter scope but functional and tested). One build error in Codex's new patch (`CultureObject` namespace missing) was fixed.

### Build & Test (Review 33)
- `./build.ps1 -RunTests` — clean.
- 1288/1288 tests passing (was 1287 before the regression test addition). 52 directly cover the race-filter feature (24 filter service + 12 rebuilder helpers + 16 SetPlayerRace).

### Review 33 — In-Game Verification Follow-ups (commits `2ccbdfc` and `896ace5`)

After landing the Codex Review 33 fixes, in-game verification surfaced two further bugs neither the deep-review nor the Codex pass caught.

**Follow-up bug A (HIGH, user-visible): dropdown order followed engine order, not config order.** `BuildGlobalIndexMap` iterated the engine's race-name array and added entries when present in the allow-list. Engine puts `human` at index 0; for cultures whose allow-list also contains human (Mordor, Isengard, Gundabad, Dol Guldur, elven cultures), the resulting filtered list started with human, surfacing `human` in dropdown position 1 instead of the lore-canonical race the user listed first in `cultures.json`. Fix in commit `2ccbdfc`: iterate the allow-list (config order) and resolve each name to its engine index via a name → index dictionary. Two existing tests had their expectations flipped from engine-order to allowed-order; two new regression cases pin Mordor and Isengard.

**Follow-up bug B (HIGH, user-visible): dropdown defaulted to human even after order was fixed.** Vanilla `FaceGenVM.Refresh(bool)` line 1779 sets `_selectedRace = _faceGenerationParams.CurrentRace`, which the engine initializes to `0` (human) regardless of culture. For Isengard's allow-list `[uruk_hai, berserker, human]`, `MapGlobalIndexToFiltered(0, [...])` returned 2 (human's filtered position). The original force-switch logic only fired when the current race was *not* in the allow-list — so no switch happened, and the dropdown header still showed human even though the user expected uruk_hai (Races[0]) as the canonical default. Fix in commit `896ace5`: per-`FaceGenVM`-instance session tracking via `ConditionalWeakTable<FaceGenVM, RaceFilterSession>` records the last applied culture id; on the first Apply for a given culture, force-switch to filtered position 0 when the current race isn't already there. Subsequent Apply calls preserve the player's choice. Decision logic extracted into pure helper `ShouldForceSwitchToDefault(currentFilteredIdx, firstApplyForThisCulture)` for testability — four new tests cover not-allowed-always-switch, first-apply-non-default-switches, first-apply-already-default-no-op, subsequent-apply-preserves.

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| A | Filtered list ordered by iteration source (engine), not config | Logic error / iteration direction | I picked the natural-feeling "iterate the universe, keep what's allowed" pattern. Correct for the SET, wrong for the ORDER. The deep-review's data-flow agent verified mechanical correctness but didn't trace order-as-UX. | Memory entry `feedback_filter_order_and_default` Trap 1 codifies the iteration-source rule. The new `BuildGlobalIndexMap_Mordor_UrukFirstNotHuman` and `BuildGlobalIndexMap_Isengard_UrukHaiBerserkerHumanInThatOrder` tests pin the regression. |
| B | Engine-default selection state landed in allowed-but-non-canonical filtered position | Logic error / default-state semantics | Traced the success path ("does the player's current race resolve to a valid filtered position?") but not the UX path ("what does the user expect the default to be?"). Codex traced `Refresh → 1779` and flagged a different bug (the OnPropertyChangedWithValue reflection bug); neither reviewer enumerated default-state expectations against engine-default state. | Same memory file Trap 2: detect "first encounter" with each filter context via `ConditionalWeakTable`, force-switch when engine-default falls on a non-canonical position. New `ShouldForceSwitchToDefault` helper + four cases. |

### Build & Test (Review 33 follow-ups)
- `./build.ps1 -RunTests` — clean.
- 1294/1294 tests passing (was 1288 after the initial Review 33 fixes; +2 for Trap A, +4 for Trap B).
- In-game verified by the user: Mordor → uruk, Isengard → uruk_hai, Gundabad → pale_uruk, Dol Guldur → dg_uruk, elven cultures → elf, Erebor → dwarf. Player race choice persists across mid-CC navigation; switching culture resets to the new culture's Races[0].

### Review 34 — SiegeDismount (port from external developer, Codex follow-up to /deep-review)

Pipeline: `/deep-review` (5-agent core) → 2 HIGH gaps caught and fixed in same session → `/review-codex` produced 3 ADDITIONAL findings (2 HIGH + 1 MEDIUM) the deep-review missed. All confirmed and fixed in same session per "no silent deferrals."

**Source brief:** [docs/reviews/codex-prompt-siegedismount-2026-05-06.md](codex-prompt-siegedismount-2026-05-06.md). 8 Known Suspects (4 confirming /deep-review fixes, 4 new attack lines).

**Codex findings file:** [docs/reviews/codex-adversarial-siegedismount-2026-05-06.md](raw/codex-adversarial-siegedismount-2026-05-06.md). Reconstructed from stdout because Codex's `apply_patch` was rejected by read-only sandbox; `ilspycmd`/`dotnet` also rejected by shell policy, so vanilla decompilation code blocks were verified separately by Claude.

**Verdict from Codex:** needs-attention (no-ship).

| # | Severity | Codex Finding | Claude Verdict | Fix |
|---|----------|---------------|----------------|-----|
| 1 | HIGH | `SceneSiegeKeywords` still includes `siege`, matches 24 vanilla `Location id="center"` scene names like `empire_siege_001`. False-positive dismount during non-siege settlement-center missions. | CONFIRMED via grep — 24 occurrences in [settlements.xml](../../Main/_Module/ModuleData/settlements.xml). | Removed keyword fallback entirely. `IsSiegeMission` now returns `isSiegeBattle` directly. Modded sieges that don't set the engine flag won't trigger — documented requirement. 9-row data-test pins the new contract. |
| 2 | HIGH | `MountSnapshot` stores only `StringId`, deposit uses `AddToCounts(ItemObject, int)` which drops `ItemModifier`. Persistent equipment data loss. | CONFIRMED via `ilspycmd` — `ItemRoster.AddToCounts(EquipmentElement, int)` overload exists and preserves modifier. The `(ItemObject, int)` overload internally calls the former with `new EquipmentElement(item)`, dropping modifier. | `MountSnapshot` carries full `EquipmentElement` (internal — TaleWorlds types stay inside). New `(EquipmentElement, EquipmentElement)` constructor for adapter; old `(string, string)` retained for tests. `PartyMountInventoryAdapter.Deposit/Withdraw` and `PlayerMountAdapter.Restore` switched to the modifier-preserving overload via concrete-type cast. |
| 3 | MEDIUM | `DismountKeepOnMap` is silent no-op despite MCM hint promising "horse on map, player on foot." | CONFIRMED — original developer's decompiled module had the same pre-existing bug; ported verbatim. Full implementation requires `Mission.SpawnAgent` plumbing not in Phase 1. | Mode 1 retained for save-compat, logs `LogWarning` explaining mode is "Reserved / equivalent to Vanilla until somebody implements the actual map-side horse spawn." MCM hint and dropdown label updated to "(currently equivalent to Vanilla — full implementation deferred)." |

### Root Cause Analysis (Review 34)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Scene-name keyword fallback matched 24 vanilla siege center scenes | Logic error / convention inconsistency | Narrowed list during /deep-review but didn't remove the keyword fallback entirely. Assumed center scenes only loaded during real sieges without verifying. | Future feature ports interpreting scene names: grep across ALL `ModuleData/*.xml` for substring overlap. Codified in [AGENTS.md](../../AGENTS.md) "Catching incomplete narrowing of substring-keyword fallback lists" lesson. |
| 2 | ItemModifier loss on capture/deposit/restore round trip | Missing modifier-aware API | Used `AddToCounts(ItemObject, int)` overload which drops modifier. Documented as "known limitation" instead of fixing. | When adapter touches inventory/equipment slots that vanilla treats as `EquipmentElement`-shaped, prefer the `EquipmentElement`-overload. Audit API surface for both before settling on the simpler `ItemObject` overload. Codified in [AGENTS.md](../../AGENTS.md) "Catching modifier-loss-on-roundtrip via API-overload audit" lesson. |
| 3 | `DismountKeepOnMap` silent no-op despite MCM hint | Convention inconsistency / inherited bug | Ported original developer's tested behavior verbatim. Did not challenge whether user-visible promise (MCM hint) matched implementation. | Read user-facing strings (MCM hints, dropdown labels, tooltips) and trace each to implementation. If promise doesn't match code, fix one or the other — never ship the mismatch. Codified in [AGENTS.md](../../AGENTS.md) "Catching user-facing-promise-mismatch from inherited dev code" lesson. |

### Things Codex did particularly well (Review 34)

- Caught the scene-name false-positive that the prior /deep-review Agent 5 (Data Flow) missed. Agent 5 caught two TAOM-specific castles (`gate`/`wall`); Codex extended the same class to 24 vanilla settlements (`siege` substring).
- Verified the modifier-aware `AddToCounts(EquipmentElement, int)` overload exists, turning a "deferred to follow-up" doc-only limitation into an immediate fix.
- Flagged the inherited-bug pattern (mode 1 promise mismatch) — the kind of bug Claude is trained to overlook ("the original developer tested it, must be intentional").

### Things Codex did less well (Review 34)

- Could not write the output review file due to sandbox `apply_patch` rejection; required Claude to reconstruct from stdout.
- Could not run `ilspycmd` due to shell policy — vanilla decompilation code blocks not produced inline. Claude verified separately.
- Did not engage with the Known Suspects section's confirm/dispute format — reported its own findings instead. Findings 1 and 3 were related to /deep-review's incomplete fixes; Known Suspects framing would have caught them faster.

### Fixes Implemented (Review 34)

1. **Finding 1 (Codex):** Removed keyword fallback from [`SiegeDismountService.IsSiegeMission`](../../Main/Features/SiegeDismount/SiegeDismountService.cs). Tests rewritten — replaced 5-row scene-keyword data test and 4-row TAOM-castle false-positive regression with 9-row `OnMissionStart_NotIsSiegeBattle_DoesNotTriggerRegardlessOfSceneName` pinning the new IsSiegeBattle-only contract.
2. **Finding 2 (Codex):** [`MountSnapshot`](../../Main/Features/SiegeDismount/Models/MountSnapshot.cs) now carries full `EquipmentElement`; production constructor `(EquipmentElement, EquipmentElement)`. [`PlayerMountAdapter.Capture`](../../Main/Adapters/PlayerMountAdapter.cs) and [`Restore`](../../Main/Adapters/PlayerMountAdapter.cs) preserve modifier. [`PartyMountInventoryAdapter.Deposit/Withdraw`](../../Main/Adapters/PartyMountInventoryAdapter.cs) use the modifier-aware overload via concrete-type cast.
3. **Finding 3 (Codex):** [`SiegeDismountService` switch](../../Main/Features/SiegeDismount/SiegeDismountService.cs) — `DismountKeepOnMap` case logs warning and is a full no-op. MCM dropdown label and hint updated in [`TaomSettings.cs`](../../Main/Features/TaomSettings.cs). Tests added: `OnMissionStart_DismountKeepOnMap_FullNoOp`, `OnMissionStart_DismountKeepOnMap_LogsWarningAboutDeferredImplementation`.

### Build & Test (Review 34)
- `./build.ps1 -RunTests` — clean (architecture mismatch warnings unchanged from baseline).
- 1405/1405 tests passing. 33 SiegeDismount tests (same count as before review — replaced false-positive scene-name tests with new IsSiegeBattle-only tests; added KeepOnMap warning test; otherwise behavior preserved).
- In-game verification: deferred to user. Pre-commit gate.

### Review 35 — Player Startup Gold + CC Equipment Persistence (port from LOTRAOM `StartingEquipmentGold`)

| Phase | Source | Verdict | Findings |
|-------|--------|---------|----------|
| 35a | `/codex:review` first pass | ISSUES FOUND (1 P1 / 1 P2) | civilian guard wrong singleton (P1); shaghana/abanissa missing from XML (P2); + 3 unrelated Messengers findings flagged for separate owner |
| 35b | `/codex:review` Phase 3 self-review of fixes | ISSUES FOUND (1 HIGH / 1 LOW) | shaghana/abanissa narrative menu coverage missing → player flow dead-ends (HIGH); XML header comment misattributes influence to NPC lords (LOW) |

| | Date | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |
|---|------|--------------|----------------|-----------|-----------------|-------------|----------------|
| 35 | 2026-05-06 | ISSUES FOUND (Phase 1: 1 P1 / 1 P2; Phase 3b: 1 HIGH / 1 LOW) | agree (all confirmed) | 4 confirmed (1 P1: civilian guard singleton; 1 P2: shaghana/abanissa missing config rows; 1 HIGH: shaghana/abanissa narrative menu dead-end; 1 LOW: XML header wording) | 0 | 0 | adversarial-self-review-v1 |

### Root Cause Analysis (Review 35)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | PlayerEquipmentAdapter civilian guard targeted DeadBattleEquipment instead of DeadCivilianEquipment | Reflection target wrong (sealed-type fallback path) / agent-paraphrase-of-decompilation accepted | Claude `taleworlds-researcher` deep-review agent's API-fact summary table reported BOTH equipment getters fall back to `DeadBattleEquipment` (incorrect symmetry). Claude trusted the paraphrase without re-running ilspycmd. | When two reviews disagree on an API or an agent's "symmetric" parallel-API summary looks too clean, re-run ilspycmd against the installed v1.3.15 DLL. Codified in [AGENTS.md](../../AGENTS.md) "Re-decompiling property-getter bodies when an agent's paraphrase contradicts another agent's" lesson. New memory entry: `feedback_codex_caught_api_misread.md`. |
| 2 | shaghana/abanissa missing from startup_resources_config.xml | Missing config row / enumeration from existing rows not source-of-truth | Claude copied existing 15-row culture list and added new attribute to each. Source-of-truth (`cultures.json`) had additional cultures not present in the existing config. Claude `/deep-review` Agent 5 flagged but dismissed as "may be intentional." | When extending config with new attribute, enumerate from upstream source-of-truth (cultures.json), not from existing config rows. New memory entry: `feedback_enumerate_from_source_of_truth.md`. Pushback rule for hedge language codified in [AGENTS.md](../../AGENTS.md) "Pushing back on 'may be intentional' hedge from prior agent" lesson. |
| 3 | shaghana/abanissa misclassified as "Aserai-region cultures with no NPC clans" when applying Codex fix #2 | Other (ID classification by single-source assumption) | Claude read `cultures.json` and saw `town_A6`/`town_A14` starting settlements, classified as Aserai-region. Did not grep `taom_spkingdoms.xml`, `clans.xml`, or `lords.xml` to verify. Existing memory `kingdom-culture-mapping.md` had the correct classification but wasn't loaded before classifying. | When classifying unfamiliar TAOM IDs, exhaustive grep across kingdom/clan/lord XML AND memory directory FIRST. New memory entry: `feedback_classify_by_grep_not_by_assumption.md`. Process improvement: load relevant memory entries at task START, not after correction. |
| 4 | shaghana/abanissa narrative menu coverage missing → player flow dead-ends | Missing pipeline-stage coverage / enumeration not extended through full pipeline | Claude verified shaghana/abanissa exist in `cultures.json` but did not trace forward through the 5 narrative menu JSONs (parents/childhood/education/youth/adulthood). The cultures register at the entry point but have ZERO entries in any narrative menu — vanilla CC crashes on advance from empty SelectionList. | Enumeration from source-of-truth must extend through the FULL pipeline a feature touches. For new culture entries: grep all 5 menu JSONs + youth equipment XML + finalize config in one pass. Codified in [AGENTS.md](../../AGENTS.md) "Tracing the FULL player pipeline for new culture entries, not just the entry point" lesson. Filed follow-up issue [#111](https://github.com/haterade22/TAOM/issues/111). |
| 5 | XML header comment misattributed `influence` to NPC lords | Convention inconsistency / paraphrase from memory | Claude wrote XML header comment by paraphrasing mental model of feature. Did not re-read `StartupInfluenceService.cs` to confirm consumer (it applies to clans, not lords). | Doc/comment text near user-editable config files must be verified against consuming code, not paraphrased. Codified in [AGENTS.md](../../AGENTS.md) "Comment-vs-consumer mismatch in user-editable config docs" lesson. |

### Things Codex did particularly well (Review 35)

- **Caught what 5 parallel Claude agents and a first-pass Codex review all missed.** The Phase 3 self-review trace-through-pipeline approach (`shaghana picks at culture step → what happens at parents_menu? childhood_menu? ...`) found the HIGH dead-end that no prior review traced. This validates the mandatory Phase 3 self-review-of-fixes step.
- **Re-decompiled instead of trusting prior agent paraphrase.** When the Claude API agent reported both equipment getters fall back to `DeadBattleEquipment`, Codex re-ran ilspycmd and got the correct `DeadCivilianEquipment` separate fallback — a P1 bug Claude's deep-review built into its own fix.
- **Pushed back on hedge language from prior agent.** Where Claude's data-flow agent said shaghana/abanissa "may be intentional zero-gold cultures," Codex correctly treated the hedge as an open question and verified intent via kingdom XML grep.
- **Suspect-by-suspect CONFIRMED/DISPUTED structure produced concrete verdicts.** The prompt's per-suspect format with required CONFIRMED/DISPUTED tag forced Codex to either verify or refute each known-risk area. No vague "looks fine" passes.
- **End-to-end pipeline tracing.** The shaghana player-flow trace (cultures.json → SetSelectedCulture → parents → childhood → ... → finalize) is the canonical "what does the user actually experience?" question that surfaced the dead-end.

### Things Codex did less well (Review 35)

- The first-pass review (35a) flagged shaghana/abanissa as missing from XML but did not extend the trace into the narrative menu JSONs — the Phase 3 self-review (35b) did. The first pass treated "row missing from one config" as the full bug; the second pass treated it as one symptom of "culture coverage incomplete across the feature pipeline."
- First-pass review surfaced 3 unrelated Messengers findings (P1+P2+P2) that Claude deferred as "out of scope, separate owner." Useful but distracted from the player-startup-gold review focus.

### Fixes Implemented (Review 35)

**From 35a (first pass):**
1. **Finding 1 (P1):** [`PlayerEquipmentAdapter.cs`](../../Main/Adapters/PlayerEquipmentAdapter.cs) — track `deadBattle` and `deadCivilian` separately; check each slot against its OWN dead-equipment singleton.
2. **Finding 2 (P2):** [`startup_resources_config.xml`](../../Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) — added shaghana/abanissa rows with `gold="50000" influence="100" playerGold="4000"`.

**From user feedback (Phase 3a):**
3. **shaghana/abanissa misclassification:** corrected XML comment from "Aserai-region cultures with no NPC clans" to "Independent Harad-region kingdoms (full kingdoms with NPC clans + lords; Shaghana 9 lords, Abanissa 8 lords)". Updated `gold`/`influence` values from `0`/`0` to actual Harad-tier values so 17 NPC lords get their startup resources.

**From 35b (Phase 3 self-review):**
4. **HIGH:** Out of scope for #110 (narrative menu authoring is a separate feature). Filed follow-up issue [#111](https://github.com/haterade22/TAOM/issues/111). Added defensive XML comment on the shaghana/abanissa rows flagging the dependency on narrative coverage so future tuners don't think the rows are functional.
5. **LOW:** Corrected XML header comment — `influence` is granted to "each eligible CLAN of this culture" (not "NPC lords").

### Build & Test (Review 35)

- `./build.ps1` — clean (only LF/CRLF warnings).
- 85/85 session-targeted tests passing. 1340/1340 total project tests passing.
- v1.3.15 API verification: 9 calls verified via ilspycmd against installed DLL — `GiveGoldAction.ApplyBetweenCharacters`, `MBObjectManager.GetObject<MBEquipmentRoster>`, `MBEquipmentRoster.AllEquipments`, `Equipment.IsBattle`/`IsCivilian`/`FillFrom`, `Hero.FindFirst`, `Hero.BattleEquipment`/`CivilianEquipment` (with separate dead-equipment fallback singletons), `CharacterCreationContent.SelectedTitleType`/`SelectedCulture`.
- In-game smoke test: deferred to user.
- Commits: `ab0910f` (feature), `6d1d668` (Phase 3 doc gap + comment).
- Closes: [#110](https://github.com/haterade22/TAOM/issues/110). Follow-up: [#111](https://github.com/haterade22/TAOM/issues/111).


### Review 36 — MixedFormations (port from external developer, Codex follow-up to /deep-review)

Pipeline: `/deep-review` (5-agent core) returned PASS on standards/compatibility/completeness/data-flow with 1 MEDIUM + 1 LOW efficiency finding (fixed in same session) → `/review-codex` produced 2 ADDITIONAL findings (1 HIGH + 1 MEDIUM) the deep-review missed. All confirmed and fixed in same session.

**Source brief:** [docs/reviews/codex-prompt-mixedformations-2026-05-06.md](codex-prompt-mixedformations-2026-05-06.md). 9 Known Suspects (2 confirming /deep-review fixes, 7 new attack lines).

**Codex findings file:** [docs/reviews/codex-adversarial-mixedformations-2026-05-06.md](raw/codex-adversarial-mixedformations-2026-05-06.md). Reconstructed from stdout because Codex's `apply_patch` was rejected by read-only sandbox; `ilspycmd` also blocked by shell policy, so vanilla decompilation code blocks were verified separately by Claude outside the sandbox.

**Verdict from Codex:** needs-attention (no-ship).

| # | Severity | Codex Finding | Claude Verdict | Fix |
|---|----------|---------------|----------------|-----|
| 1 | HIGH | Patch30 bypasses vanilla `Mission.IsFormationUnitPositionAvailable` check buried in `GetOrderPositionOfUnitAux`. Custom layout positions can land on cliffs/walls/siege props/non-navigable terrain. | CONFIRMED via `ilspycmd` against installed v1.3.15 — vanilla Hold path delegates to `GetOrderPositionOfUnitAux` which validates the candidate then falls back to `unit.GetWorldPosition()` if unavailable. /deep-review Agent 5 traced only the entry method and missed the gate. | [Patch30_FormationGetOrderPositionOfUnit.Prefix](../../Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) now calls `mission.IsFormationUnitPositionAvailable(ref candidate, team)` before setting `__result`. If unavailable → returns true (vanilla handles via `unit.GetWorldPosition()` fallback). |
| 2 | MEDIUM | `FormationLayoutService` mutates `Dictionary` caches and `SlotAssignment.ByAgentIndex` from the hot Prefix path without synchronization. Vanilla shows `_MT`-suffix multi-threaded helpers + `TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock)` + `Formation.OrderPositionLock` — clear engine threading markers. | CONFIRMED via `ilspycmd` — `Formation.OrderPositionLock` exists; `IsFormationUnitPositionAvailableAuxMT` uses `TWSharedMutexReadLock`. Engine threads positioning queries; our patch fires from those threads; cache writes can race against `OnMissionTick`-driven writes. | Added `private readonly object _lock = new();` to [FormationLayoutService](../../Main/Features/MixedFormations/FormationLayoutService.cs). All dict + `SlotAssignment.ByAgentIndex` mutations now lock. Reads on the hot path lock briefly (~25ns uncontended); pure math runs outside the lock. Two regression tests added: `ConcurrentTaskBattery_SetLayoutAndCompute_DoesNotThrowOrCorruptCache` (8 tasks × 100 ops) and `ComputeAndCycle_RapidSequentialAlternation_RemainsCoherent`. |

### Root Cause Analysis (Review 36)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Patch30 bypassed vanilla navmesh availability check | Missing vanilla gate / incomplete call-chain trace | /deep-review Agent 5 traced `Formation.GetOrderPositionOfUnit` itself but not the helper `GetOrderPositionOfUnitAux` it delegates to in the Hold branch. Verdict "vanilla path is read-only" was based on entry method only; the safety gate (`Mission.IsFormationUnitPositionAvailable`) lives one frame deeper. | Codified in `feedback_replicate_vanilla_safety_gates_in_prefix.md` — when a Prefix returns false, decompile EVERY method the entry calls and replicate every safety gate. The entry's body is routing; the helpers contain load-bearing logic. Auto-loaded every Claude session for this repo. |
| 2 | Cache + assignment mutations from worker-thread Prefix without synchronization | Missing concurrency awareness / engine-threading inference | Did not notice the `_MT` suffix on Bannerlord positioning helpers (`CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`, `GetNavMeshMT`) which is the engine's convention for multi-threaded helpers. Did not search for `TWSharedMutexReadLock` or `Formation.OrderPositionLock` in the vanilla source. | Codified in `feedback_detect_engine_threading_via_mt_suffix.md` — before patching Formation/Mission/Scene methods, grep the vanilla type for `_MT` suffix and lock patterns. If present, patch fires from worker threads; service must be thread-safe via lock or immutable state. Auto-loaded every Claude session. |

### Build & Test (Review 36)
- `dotnet build TAOM.Tests/TAOM.Tests.csproj -c Debug -p:DisableModuleCopy=true` — clean (the `DisableModuleCopy` flag bypasses a post-build deploy step that fails when Bannerlord is running and locks the `.rdc` file).
- 38 MixedFormations tests pass (was 36 before this review; +2 thread-safety regression tests).
- In-game verification: deferred to user. Pre-commit gate.


### Review 38 — EditorCacheRebuild (post-`/deep-review` Codex follow-up)

Pipeline: `/deep-review` (5-agent core) caught 1 CRITICAL (`_navigationType` field-vs-property reflection mismatch) + 1 standards violation (service-locator in adapter) + 1 cross-system inconsistency (`SortedPathKey` sort order vs vanilla); all 3 fixed in same session. HIGH-perf items (ThreadStatic arg pools + ConcurrentQueue swap) also implemented per user direction. THEN `/review-codex` returned **6 additional findings: 2 P1 + 2 P2 + 2 P3**, all confirmed and fixed in same session.

**Source brief:** [docs/reviews/codex-adversarial-editorcacherebuild-2026-05-12.md](raw/codex-adversarial-editorcacherebuild-2026-05-12.md). 7 Known Suspects.

**Codex findings file:** [docs/reviews/codex-adversarial-editorcacherebuild-2026-05-12-review.md](raw/codex-adversarial-editorcacherebuild-2026-05-12-review.md).

**Verdict from Codex:** ISSUES FOUND (no-ship — until P1s fixed).

| # | Severity | Codex Finding | Claude Verdict | Fix |
|---|----------|---------------|----------------|-----|
| 1 | P1 | Incremental moved-settlement rebuild writes duplicate distance keys. Deserialize loads full old dict; Phase 1 `RunFiltered` calls vanilla `SetSettlementToSettlementDistanceWithLandRatio` which ends in `Dictionary.Add` → `ArgumentException` on every existing key touching a changed settlement. | CONFIRMED via re-reading vanilla setter — `value.Add(settlement2, (distance, landRatio))` is `Dictionary.Add`, throws on duplicate. | Added `INavigationCacheAdapter.RemoveDistanceEntriesFor(HashSet<string> ids)`. `CacheBuilderService.Build` calls it AFTER `DeserializeCache` and BEFORE Phase 1 to clear all entries (outer OR inner key) touching changed settlements. |
| 2 | P1 | Incremental Deserialize clobbers fresh Phase 0 closest-face cache AND keeps stale `_fortificationNeighbors` for Phase 2 to append onto. Vanilla `GenerateNeighborSettlementsCache` opens with `_fortificationNeighbors.Clear()`; our parallel Phase 2 builders don't. | CONFIRMED — vanilla `Deserialize` replaces all three subcaches; vanilla `GenerateNeighborSettlementsCache` clears. | Two fixes: (a) Phase 0 (`RunClosestSettlementCache`) now SKIPPED when `willDeserialize` is true — deserialize provides it and CRC verification guarantees freshness. (b) Added `INavigationCacheAdapter.ClearFortificationNeighbors()`; called in `CacheBuilderService.Build` whenever we deserialized (incremental OR resume — defensive even though resume's checkpoint is saved BEFORE Phase 2). |
| 3 | P2 | Patch37 catch-block fell back to vanilla via `return true`, but the service had already mutated `_closestSettlementsToFaceIndices` via Phase 0. Vanilla `GenerateClosestSettlementToFaceCache` then re-runs and hits `SetClosestSettlementToFaceIndex` → `Dictionary.Add` → throws on duplicate face id. | CONFIRMED — `_closestSettlementsToFaceIndices.Add(faceId, settlement)` is Add-only per decompile. | `Patch37_CacheBuildOverride.Prefix` catch-block now `return false` (don't run vanilla on a partially-mutated cache). Documented in the catch-block comment that the user must re-click the button to retry from a fresh cache instance. |
| 4 | P2 | `SettlementSnapshotStore.Save` reads `s.GatePosition.Face.FaceIndex`; `CampaignVec2.Face` lazy-resolves via `Campaign.Current.MapSceneWrapper.GetFaceIndex(this)`. In editor mode `Campaign.Current` may be null → NRE. Vanilla editor cache builder never touches `.Face`; it uses `Scene` directly. | CONFIRMED — `Face` getter unconditionally dereferences `Campaign.Current`. | Removed `GateFace`/`PortFace` integer fields from `SettlementSnapshot`. Snapshot store now stores positions-only via `s.GatePosition.ToVec2()` (pure cached-position read, doesn't touch Campaign). `SettlementDiffer.HasMoved` compares positions only. Face index would be re-derivable from position via the scene if ever needed. |
| 5 | P3 | Float config range validators don't reject `NaN`/`Infinity`. `NaN` comparisons always return false so `< min || > max` evaluates `false` and `NaN` sneaks through. A `NaN` `smokeTestDistanceTolerance` silently disables the smoke-test gate because `maxDelta > NaN` is always false. | CONFIRMED — same IEEE-754 NaN-passes-range-check pattern caught earlier in Career cooldown review 31. | Added `IsFiniteNumber(float)` helper in `CacheRebuildConfigProvider`. Both `IncrementalSpatialRadius` and `SmokeTestDistanceTolerance` validators now check `!IsFinite || out-of-range` and revert to default on either condition. |
| 6 | P3 | `SortedPathKey` matches vanilla `NavigationCacheElement<T>.Sort` for normal inputs (already fixed pre-Codex via /deep-review). Degenerate self-pairs (`id1 == id2 && isPort1 == isPort2`) are not covered by existing tests. | CONFIRMED test gap only. | Added two regression tests in `SortedPathKeyTests`: `Ctor_SameIdSameGateGate_Canonicalized` + `Ctor_SameIdSamePortPort_Canonicalized`. Both pass — the existing condition handles the case correctly. |

### Root Cause Analysis (Review 38)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Incremental Phase 1 dup-key on `Dictionary.Add` | Missing vanilla gate / wrong-API-shape assumption | The pre-Codex /deep-review verified diff logic, position-comparison, and CRC validation BUT did not decompile the vanilla setter to confirm `Set...WithLandRatio` semantics. Assumed "set" semantics from the method name; actual semantics are Add-only with `Debug.FailedAssert + Dictionary.Add`. The "Set" prefix in the method name was misleading. | Added pattern to AGENTS.md "Bugs Codex caught": when a feature deserializes a vanilla cache and then mutates it via vanilla "Set/Add" APIs, decompile the setter body. Confirm whether it overwrites or throws on duplicate. Same trace also reveals which subcaches the deserialize touches. |
| 2 | Stale `_fortificationNeighbors` + Phase 0 overwrite | Missing vanilla gate / lifecycle mismatch | /deep-review traced Phase 1 + Phase 2 builders independently and missed that vanilla's neighbor builder opens with `.Clear()`. Also missed that our Phase 0 invocation runs BEFORE deserialize, so deserialize overwrites it (wasted work + Phase 0 isn't idempotent if it ever runs twice). | Codified in AGENTS.md and CHANGELOG. The pattern is: when patching a cache-builder with a phased structure where some phases are skippable (resume/incremental), verify each phase's vanilla precondition (Clear, ResetCache, etc.) is replicated externally. |
| 3 | Vanilla fallback on partial mutation | Missing vanilla gate / Add-only consistency | Patch37's `return true` fallback was added defensively without tracing what vanilla does on re-entry to a mutated instance. Same Add-only-Dictionary issue as Finding 1; the fallback path was actually MORE unsafe than just skipping. | Pattern added to AGENTS.md: once a Prefix mutates `__instance`, returning `true` is unsafe unless the Prefix can rollback OR the vanilla path is idempotent. Default to `return false` after logging. |
| 4 | `CampaignVec2.Face` editor-mode NRE | Wrong API surface chosen / under-decompile | Initial implementation used `Face.FaceIndex` for diff comparison without checking the Face getter's body. The getter calls `Campaign.Current.MapSceneWrapper.GetFaceIndex(...)` which has implicit Campaign dependency. /deep-review Agent 5 flagged this as a YELLOW risk; the fix was to switch to position-only. | Pattern added: when snapshotting `CampaignVec2`-typed data in an editor context, prefer `ToVec2()` over `Face.FaceIndex`. Position scalars don't touch Campaign. Same pattern applies to any TaleWorlds struct whose computed properties have hidden global dependencies. |
| 5 | NaN/Infinity in float config | Logic error / IEEE-754 pitfall | Range-check validators were copy-pasted from RevoltTuning (which had the same gap until Career cooldown review 31 surfaced it). The new feature inherited the gap. | Pattern already in AGENTS.md from Career review 31; this is a repeat — should add a unit-test convention or shared validator helper. Filed as v2 improvement: `Main/Core/Validation/FiniteFloatValidator.cs` to centralize. |
| 6 | Degenerate self-pair test gap | Test coverage | Sort-equivalence tests covered cross-id cases and one same-id port/gate mix. Same-id same-port wasn't enumerated. | Two new tests added. Pattern: when reviewing a key/comparator class, enumerate the four boundary cases: (s1<s2), (s1>s2), (s1==s2 with field differ), (s1==s2 fully). |

### Build & Test (Review 38)

- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` — clean.
- 96 EditorCacheRebuild tests pass (was 96 before Codex fixes — 2 new tests added for Finding 6; existing tests still cover the fixed code paths).
- 1800/1800 total project tests still pass.
- In-game verification: blocked on user re-running the editor. Phase 14 integration test pending.
- Files changed for fixes: `Main/Adapters/{INavigationCacheAdapter, NavigationCacheAdapter}.cs`, `Main/Features/EditorCacheRebuild/{CacheBuilderService, CacheRebuildConfigProvider}.cs`, `Main/Features/EditorCacheRebuild/Diff/{SettlementSnapshot, SettlementSnapshotStore, SettlementDiffer}.cs`, `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`, `TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs`.

### Things Codex did particularly well (Review 38)

- Decompiled the vanilla setters and serializer/deserializer bodies in full, not just the signatures.
- Independently verified all 7 Known Suspects with citations; produced precise CONFIRMED/DISPUTED verdicts.
- Caught a 2-link chain bug (Finding 2: deserialize-overwrites-Phase-0 AND Phase-2-appends-without-clear) that /deep-review's 5-agent core had treated as a single concern.
- Disputed the /deep-review false positive (vanilla `Serialize` unreachable after Prefix) with citations, confirming our pre-Codex dispute.

### Things Codex did less well (Review 38)

- Test run blocked by sandbox: first attempt to write `C:\Users\CodexSandboxOffline\.dotnet` failed, retry with workspace `DOTNET_CLI_HOME` blocked by `Microsoft SDKs` ACL. Codex noted this in Quality Gates but could not independently verify the test suite was green. Acceptable given the sandbox is enforced for safety, but a `--no-build` `dotnet test` should be in the sandbox allowlist for review workflows.
- Did not catch that `phase1SkipReversePathfind` config field is orphaned (Codex listed it as "reserved" without challenging whether the reservation is intentional). Minor — already documented as reserved in feature doc.


## Review 39 — EditorCacheRebuild MCM-Trigger Pivot (Codex follow-up to /codex-verify on the in-game runtime path)

**Scope:** 3 commits since `a502ade`: `646484b` (MCM trigger + comprehensive logging), `024e9e9` (Patch37 try/catch in singleplayer), `6230c0c` (ICampaignSessionAdapter refactor + 11 new tests). Built on top of the original Review 38 work — same feature, different entry point.

**Verdict:** 0 P1 + 2 P2 + 2 P3. All confirmed, all fixed in same session.

### Findings (Review 39)

| # | Severity | Title | Status |
|---|---|---|---|
| 1 | P2 | Round-trip verification can fail or be blind while user gets success popup | CONFIRMED, fixed |
| 2 | P2 | Final cache replacement is not crash-atomic across two rename steps | CONFIRMED, fixed |
| 3 | P3 | `cache_rebuild_config.json` exposes reserved/dead knobs as if they're active | CONFIRMED, fixed |
| 4 | P3 | New tests stop at `SpawnBuild` and miss the production background path | CONFIRMED, partially fixed (7 added; full RunBuild orchestration test deferred) |

Plus 8 Known Suspects walked: 4 DISPUTED with citations, 4 CONFIRMED (matched the 2 P2 + 2 P3 above).

### Root Cause Analysis (Review 39)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Void-returning verification, popup runs unconditionally | Logic error / consumer-side blindspot | When verification logic was refactored from "throw on failure" to "log and continue" during comprehensive-logging work, the caller wasn't updated to gate the success popup. Original logic threw → caller's catch handled it. New logic just logs → caller never saw the signal. | Pattern added to AGENTS.md. Made `VerifyOutputRoundTrip` return `VerificationResult { Ok, Reason, ActualDistanceCount, ActualNeighborCount }`. Caller branches on `Ok` and surfaces red popup with `.prev` restoration instructions on failure. |
| 2 | Multi-step `File.Move` masquerading as atomic | Missing API knowledge / inherited pattern | 3-step rename is the standard Linux-derived approach (no atomic File.Replace there). Windows .NET Framework has `File.Replace` calling `ReplaceFile` Win32 — single atomic transaction. The implementer wasn't aware of `File.Replace`. | Pattern added to AGENTS.md. Replaced 3-step sequence with `File.Replace(temp, final, backup, ignoreMetadataErrors: true)` when final exists. Kept `File.Move` for first-build case. Crash window eliminated. |
| 3 | 8 dead config fields shipped in JSON | Convention inconsistency / scope creep | Original design had 19 fields. Phases 9 (spatial index), 12 (path reuse), 13 (multi-pass quality check), and UI overlay were dropped, but config fields remained in shipped JSON to "preserve test coverage and future hook points" per Review 38. Codex correctly argued that's user-facing debt. | Stripped 8 fields from `cache_rebuild_config.json`. Kept in `CacheRebuildConfig.cs` with `<summary>Reserved...</summary>` doc comments. Pattern added to AGENTS.md: every field in shipped JSON must have at least one production consumer. |
| 4 | Test seam intercepts production path | Insufficient test extraction | The `SpawnBuild` no-op override was correct for testing gate logic but also skipped `RunBuild`, `VerifyOutputRoundTrip`, `WriteOutputAtomically`, and the `finally` cleanup. None were unit-tested. | Made `VerifyOutputRoundTrip` and `WriteOutputAtomically` `internal virtual`. Added 7 tests: 3 atomic-write scenarios + 4 verification scenarios. Future work: synthetic test driving `RunBuild` end-to-end (deferred — bigger refactor). |

### Build & Test (Review 39)

- `dotnet build Main/TAOM.csproj -p:BuildForWindows=false -p:BuildForWindowsStore=false -p:ModuleId=` — clean.
- 1850/1850 tests pass (was 1829 — added 7 new RuntimeCacheRebuildServiceTests; total RuntimeCacheRebuildServiceTests count now 18, was 11).
- In-game verification: PASSED. Phase 1 (1m 27s) + checkpoint resume + Phase 2 (5m 37s) = 7m total; round-trip "OK"; output written atomically. Also verified navmesh-change CRC detection (`C98EA790 → F8E047D8`) correctly forced full rebuild over stale incremental snapshot.

### Codex Quality Notes (Review 39)

- **Decompilation thoroughness**: Re-ran `ilspycmd` for `NavigationCache<T>.AddNeighbor`, `Deserialize`, and `SandBoxNavigationCache` constructor. Pasted full bodies — the Deserialize trace led directly to P2-1.
- **Memory-model claim verification**: Disputed Suspect 1 (`_runningFlag` race) with explicit ECMA-335 reasoning. Right depth for a memory-ordering claim.
- **MCMv5 deep-decompile**: Decompiled `BaseSettingsJsonConverter.WriteJson`/`ReadJson` to verify MCMv5 doesn't persist Action-typed properties. Right answer for Suspect 7b.
- **Sandbox limitation**: Couldn't run `dotnet test` due to ACL on `Microsoft SDKs` directory. Same as Review 38.
- **Drift detection**: Stayed in scope (no adjacent-feature drift this run).

### Tracking issues opened from Review 39

- [#120 — Extend NavigationType iteration for NavalDLC / port support](https://github.com/haterade22/TAOM/issues/120) — orthogonal to Codex findings; surfaced during the "vanilla parity audit" follow-up. Filed because TAOM currently has 0 ports and `Default`-only rebuild is correct today, but a future map with coastal settlements would need 3-way rebuild.


## Review 40 — BehaviorTrees + BehaviorTreeWrapper Inlining (Codex caught a HIGH RCA misdiagnosis)

**Scope:** Decompiled and inlined two vendored DLLs (`BehaviorTreeWrapper.dll` ~1300 LOC, `BehaviorTrees.dll` ~980 LOC) into `Main/BehaviorTreeWrapper/` + `Main/BehaviorTrees/`. Deleted both vendored binaries. Original session framed this as a fix for a user-reported NRE in `Mission.CheckMissionEnded` on every battle (looter encounter was the first crash trigger for two users on `bannerlord-1.4.5`). 7 inherited perf issues fixed in-session after `/deep-review` surfaced them.

**Verdict:** 0 P1 + 2 P2 + 0 P3. Both P2 CONFIRMED — and the first one invalidated the entire RCA's root-cause story.

### Findings (Review 40)

| # | Severity | Title | Status |
|---|---|---|---|
| 1 | P2 | Stop manually ticking attached BT components | CONFIRMED, fixed (real v1.4.5 double-tick regression) |
| 2 | P2 | RCA evidence doesn't match the deleted DLL | CONFIRMED, RCA + CHANGELOG revised — original root-cause claim was wrong |

### Root Cause Analysis (Review 40)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | `WargMissionBehavior.cs:127` + `SpiderMissionBehavior.cs:152` manually called `comp.OnTick(dt)` after the `OnTickAsAI → OnTick` rename. v1.4.5 `Agent.Tick:4768` already auto-calls `component.OnTick(dt)` on every active agent every frame, so we shipped a 2×-ticks-per-frame regression on player wargs (AI was 2× in v1.3.15 too, so no change there). | API-rename-induced regression / lifecycle gap | The deep-review's taleworlds-researcher agent confirmed the `OnTick` signature exists in v1.4.5 but did NOT trace whether vanilla auto-calls it. Verifying a method's existence isn't the same as verifying who-calls-it; the latter requires reading the consumer code (`Agent.Tick`). | AGENTS.md updated with "do not infer enum value from bug story" pattern. Manual ticks removed; vanilla auto-tick handles both player- and AI-controlled BT agents. Long-term: deep-review's Bannerlord-compat agent should ALSO check who-calls-the-method, not just whether-it-exists. |
| 2 | RCA claimed `BehaviorTreeWrapper.dll`'s `BehaviorTreeMissionLogic` was the source of the null entry in `MissionLogics`. Codex decompiled the deleted DLL from `git show HEAD`, confirmed `BehaviorType => (MissionBehaviorType)1`, and decompiled v1.4.5 `MissionBehaviorType { Logic, Other }` — value 1 is `Other`, so the DLL would have gone to `_otherMissionBehaviors`, never to `MissionLogics`. The RCA's root cause cannot have caused the user's crash. | Enum-value-from-bug-story inference / verification gap | Claude assumed `(MissionBehaviorType)1 == Logic` because the user's crash was in `MissionLogics`-iteration and the bug pattern looked like the 2026-05-14 fix. Should have decompiled the enum first and mapped value-to-name explicitly before writing the RCA. | feedback memory `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` extended with mandatory enum-value verification step. AGENTS.md updated with "infer-from-story" failure pattern. Real root cause remains unidentified — follow-up investigation opened (likely a community DLL in TAOM.Dependencies — MCM, ButterLib, UIExtenderEx). |

### What this commit DOES deliver (post-Codex re-framing)

The inlining was originally pitched as a bug fix. After Codex's correction, the fix-story is gone but three real wins remain:

1. **Single-DLL ship surface + full source ownership** of both BT libraries (no upstream source repo for either, so this was a one-shot extraction).
2. **Codex F1 fix**: real v1.4.5 double-tick regression eliminated.
3. **7 inherited perf issues fixed** (E1–E7 from `/deep-review`) — would have remained latent in the vendored DLL forever.

### Build & Test (Review 40)

- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` — clean.
- `dotnet test TAOM.Tests/TAOM.Tests.csproj` — 2416 passing (was 2415; new `BehaviorTreeMissionLogicInheritanceTests` adds 1), 1 pre-existing failure unrelated (`GetVolunteerTroopId_EreborCulture_HighRoll` — Rhun recruitment in flight on this branch), 2 skipped.

### Codex Quality Notes (Review 40)

- **Caught Claude's framing bias.** The RCA looked plausible; only by decompiling the deleted DLL and the v1.4.5 enum did Codex find the value-mapping error. This is exactly the "verify, don't accept" stance the prompt asked for.
- **Sandbox cleanup conflict.** Codex tried to delete its own temp directories with PowerShell `Remove-Item` and was repeatedly blocked by sandbox policy; eventually succeeded via a small Python script. Not a finding, but worth noting that PowerShell cleanup commands get denied even for Codex-created subdirectories.
- **Stayed in scope.** Two findings, both backed by decompiled evidence. No drift into adjacent features.

---

## Review 41 — CrashReport feature (2026-05-25)

**Context.** New TAOM feature module: 60-file comprehensive crash diagnostic capture inspired by BetterExceptionWindow v8.0.0 (AGPL — design reference only). 10 Harmony Finalizers + reflection-attached AutoGenerated patches + AppDomain hook + ZIP bundle writer + MCM settings page. Authored end-to-end in the same session, BUILD + 2440/2440 tests green, then the author tried to declare it done WITHOUT running `/deep-review` or `/review-codex`. The user prompted both. **This review is the result of the workflow being applied to a feature that nearly shipped without it.**

**Dispatch mode.** First review run via the new direct-dispatch contract — Claude called `codex exec - < prompt.md > output.md 2>&1` from Bash inside the skill, no user terminal hand-off. Codex `model=gpt-5.5` `reasoning_effort=xhigh`. Output: 1.88 MB / ~32 KB rendered.

### Findings vs Verdict (Review 41)

| # | Codex Severity | Claude Verdict | Agree? | Reason |
|---|---|---|---|---|
| C-H1 | HIGH | HIGH | yes | Static `CrashReportPatchHelper._service` survives `OnSubModuleUnloaded` → post-reload Finalizers call disposed `FileLogger`. Confirmed by reading source. |
| C-H2 | HIGH | HIGH | yes | `EnableCrashCapture` MCM hint promised runtime no-op + AppDomain unsubscribe; property read only at startup. Same shape as Phase 1 `SuspendButterLibHandler` (already fixed in /deep-review). Second instance in same feature. |
| C-M1 | MED | MED | yes | Patch37 attached at SubModule line 108 but throws in 88-107 (IoC.Configure, UIExtender setup, ITimeAccelerationService resolve, Harmony ctor, settings.Instance read) are uncatchable. Confirmed. |
| C-M2 | MED | **CRITICAL** (Codex understated) | partial | `HarmonyCorrelationCollector.Collect(stack, frames=null)` produced empty per-frame patches lists in EVERY report. The "Harmony patches per stack frame" feature advertised in CHANGELOG + docs was DEAD CODE. Codex rated MED; Claude argues this is CRITICAL because it was the feature's primary value-add over BUTR's reporter. Fix is the same regardless. |
| C-M3 | MED | MED | yes | AppDomain.UnhandledException can fire on TaleWorlds worker threads; Mission/Campaign collectors read main-thread-only engine state; InformationManager.ShowInquiry invokes UI subscribers off-thread. Confirmed via vanilla decompile showing `TWParallel.For` agent ticks. |
| C-M4 | MED | MED | yes | `_butterLibSuspended` one-shot flag prevents re-disable after user re-enables ButterLib at runtime. `Disable()` is idempotent per decompile. Confirmed. |
| C-L1 | LOW | LOW | yes | `CrashReportSettings.Instance` is a provider scan, not a static-field read; called per-app-tick by dev triggers. Codex decompiled MCMv5 `GetSettings(id)` to prove cost. Confirmed. |
| C-L2 | LOW | LOW | yes | `CrashBundleWriter.Write` returned the zip path even after mid-write `catch`; player gets pointed at a broken bundle. Confirmed. |
| Obs | — | — | yes | Comment said "10 Harmony Finalizers" — should be 9 Finalizers + 1 Postfix + run-time reflection patches. Trivial doc fix. |

**Summary:** 8 findings, 8 confirmed, 0 false positives. Combined with the Phase 1 deep-review's 6 findings (1 HIGH + 2 MED + 3 LOW), the workflow caught **14 total bugs** in a feature that nearly shipped without either review running.

### Root Cause Analysis (Review 41)

Full RCA at [`docs/reviews/rca-crash-report-codex-2026-05-25.md`](rca-crash-report-codex-2026-05-25.md). Summary:

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| C-H1 | Static cache survives unload | Lifecycle hook missing | Author didn't enumerate the unload lifecycle for the static field even though `feedback_lifecycle_state_matrix.md` exists | Added `ResetForUnload()` + call site; AGENTS.md extended with the IoC-cache lifecycle pattern |
| C-H2 | MCM toggle promise mismatch | User-facing-promise vs code | SAME shape as Phase 1 SuspendButterLibHandler (decorative toggle) — author wrote the second instance in the same session after fixing the first | Runtime gates added in HandleAndSwallow + OnUnhandled + both dev triggers; deep-review prompt update planned to apply toggle-cross-ref to EVERY MCM toggle |
| C-M1 | Patch37 attach not actually first | Reasoning-order vs source-order | Author convinced themselves "registered FIRST" was true relative to other PatchCategory calls and didn't enumerate non-PatchCategory throwables before line 108 | Patch37 moved to immediately after IoC.Configure(); residual blind-spot (IoC.Configure itself) documented |
| C-M2 | Dead Harmony correlation | Optional-parameter trap + uncovered integration | Collector took optional `frames` parameter; sole caller skipped it; renderer faithfully rendered empty lists; no test covered non-empty output | New `CollectFromException(exception, stack)` overload that builds raw StackFrame[] internally; service calls the new overload; AGENTS.md extended with the optional-parameter integration-test pattern |
| C-M3 | Off-main-thread collectors | Single-thread assumption without enforcement | Author knew about engine threading (`feedback_detect_engine_threading_via_mt_suffix.md`) but didn't connect "AppDomain hook fires from worker threads" to "my collectors read main-thread-only state" | Main thread id captured at Subscribe(); off-thread captures tag exception.Data; service switches to reduced-capture mode skipping Mission/Campaign + UI inquiry |
| C-M4 | One-shot guard against idempotent op | Premature optimisation | Author assumed ButterLib `Disable()` was expensive without decompiling; added flag to skip re-calls | Flag removed; TrySuspend called per crash (op confirmed idempotent via decompile); AGENTS.md extended with "don't guard against idempotent operations" pattern |
| C-L1 | MCM Instance per-tick provider scan | Wrong API cost assumption | Author assumed `.Instance` was a static field read; Codex decompiled MCMv5 to prove provider scan | Cached `_cachedSettings ??= CrashReportSettings.Instance` in both dev triggers; AGENTS.md extended with "decompile .Instance accessors in per-frame paths" pattern |
| C-L2 | Error path returned success-looking value | Caller-contract mismatch | "Leave broken file for inspection" comment vs caller's "show this path to player" — combined to confuse player about bundle validity | On mid-write failure: rename to `*.zip.partial` + return null; caller can distinguish |

**Repeat-offender pattern.** Phase 1 (deep-review) caught 5 declared-without-consumer bugs (memory: `feedback_no_aspirational_enum_values.md`, 3rd occurrence in 19 days). Phase 2 (Codex) caught C-H2 which is a sibling instance — user-facing-promise mismatch (memory: `feedback_user_facing_promise_must_match_code.md`, now 4th instance). Both rules existed in memory; author didn't consult them at design time. Pattern triple-codified across deep-review + Codex + this REVIEW-LOG entry, but only author discipline can prevent recurrence.

### Build & Test (Review 41)

- `dotnet build TAOM.Tests/TAOM.Tests.csproj -p:ModuleId=` — clean, **0 Warnings, 0 Errors** (after fixing pre-existing BHA0001 attribute-order + MSB3277 System.Management version-conflict warnings introduced earlier in the session).
- `dotnet test TAOM.Tests/TAOM.Tests.csproj --no-build` — **2440/2440 passing, 2 skipped, 0 failed.**
- Full `./build.ps1` deploy step environmentally-blocked by `0Harmony.dll` lock in `<game>\Modules\TAOM.Dependencies\bin\` (Bannerlord process holding the handle). Not from these changes — same lock as the start of the session, clears when the game closes.

### Codex Quality Notes (Review 41)

- **Decompiled every named target** without spoonfeeding. Identified the v1.4.5 installed-DLL path for `MissionView` (under `Modules/Native/`) on first try.
- **Caught the dead-code feature** that 5 Claude deep-review agents passed cleanly. This was the most valuable finding — the "Harmony patches per stack frame" feature was the marquee differentiator vs BUTR's stock reporter, and it produced empty output in every report.
- **Decompiled ButterLib's `Disable()` body** to prove idempotency, then recommended removing TAOM's one-shot guard. Decompile-to-prove-cheap caught what Claude rationalised away as "expensive, must guard."
- **Decompiled MCMv5's `Instance` getter + `BaseSettingsProvider.GetSettings(id)`** to prove the per-tick cost wasn't a static field read. Same pattern.
- **Stayed in scope.** All 8 findings sit inside `Main/Features/CrashReport/` + `SubModule.cs` wiring + `IModLogger`. No drift.
- **No false positives.** 8 findings, 8 confirmed.
- **Calibration:** CRITICAL=0 / HIGH=2 / MED=4 / LOW=2 matched Claude's verification on all rows except C-M2 (Codex MED, Claude says CRITICAL for the dead-code-marquee-feature). Fix is the same regardless.

### Process improvement triggered by Review 41

1. **AGENTS.md** updated with 7 new lessons (one per confirmed finding).
2. **REVIEW-LOG.md** updated (this entry).
3. **`.claude/skills/deep-review/SKILL.md`** Phase 5 Agent 5 prompts to be updated: (a) toggle-cross-reference applies to EVERY MCM toggle in the page, not a hand-listed subset; (b) DTO Completeness trace extended from "is this populated?" to "are non-empty values actually produced under normal operation?". Both gaps directly let C-H2 and C-M2 slip past deep-review.
4. **Direct-dispatch contract validated end-to-end** — first time this contract carried a real review without user terminal hand-off. No rough edges; harness notification arrived cleanly; auto-resume worked. The CHANGELOG entry for the contract change references this review as the first user.

---

## Review 42 — Dependencies/Foundation (DR3 Phase 4 BetaDeps parity, 2026-05-27)

**Context.** New `Dependencies/Foundation/` namespace porting BetaDeps v0.7.5.1's runtime error-tolerance framework (11 classes: `DiagLog`, `RuntimeLog`, `ReflectionUtils`, `VersionProbe`, `IncompatibleModDetector`, `PatchShield`, `SaveShield` + `FailureRecord` + `FailedModsCatalog`, `SubModuleConstructionGuard`, `CollectAssemblyTypesShim`). Wired into `AliasStubSubModule.ctor` (early phase, never fires because launcher skips bin-less stub ctors) + `Dependencies/SubModule.OnSubModuleLoad` (late phase, actual install site) + `OnGameInitializationFinished` (PatchShield pass 2 for late-registered third-party patches). Authored end-to-end in same session; in-game verification surfaced 3 v1.4.5 signature drift bugs (SaveShield 4 stale targets, VersionProbe wrong class, SubModuleConstructionGuard missing AddSubModule patch site) which were fixed before Codex was dispatched.

**Dispatch mode.** Direct dispatch via `codex exec - < prompt.md > output.md 2>&1` (background). `model=gpt-5.5` `reasoning_effort=xhigh`. Prompt enumerated 8 Known Suspects derived from architectural risk (owner-filter scope, dedupe collision, by-ref Finalizer signature legality, etc.) — Codex confirmed 6 and disputed 2 with code citations.

### Findings vs Verdict (Review 42)

| # | Codex Severity | Claude Verdict | Agree? | Reason |
|---|---|---|---|---|
| S1 | HIGH | HIGH | yes | `PatchShield.TryUnpatchOffendingPatches` filtered `owner.StartsWith("TAOM")` only. ButterLib registers as `Bannerlord.ButterLib.*` / `butterlib.*` / etc. — first MissingMethodException in any ButterLib patch would auto-unpatch ButterLib's entire patch set. Confirmed by enumerating `new Harmony("X")` call sites in every vendored DLL via ilspycmd. |
| S5 | HIGH | HIGH | yes | `SubModuleConstructionGuard.SwallowFinalizer` attributed exceptions via `ex.TargetSite` — for `Module.AddSubModule(SubModuleInfo, Assembly)`, that's the vanilla method itself, not the offending SubModule class. Confirmed via decompile; fix uses `__args[0]` (SubModuleInfo.SubModuleClassTypeName) + `__args[1]` (Assembly.GetType(className)) for authoritative attribution. |
| S2 | MED | MED | yes | `SaveShield.IsEngineAssembly` used `StartsWith("TAOM")` to skip our own assemblies during culprit attribution — matched `TAOM.Foo`, `TAOMBar`, anything beginning with `TAOM`. Exact-match for `TAOM` / `TAOM.Dependencies` + dot-prefix for `TAOM.` sub-namespaces. Also added missing vendored prefixes (`Bannerlord.MBOptionScreen`, `Bannerlord.ModuleLoader`, `MCM.UI.Adapter`, `BUTR.CrashReport`) and removed redundant raw `TAOM` from `_enginePrefixes`. |
| A1 | MED | MED | yes | `IncompatibleModDetector.ReadCurrentModlist` scanned every `Modules/X/` directory — returned ALL installed modules, not just enabled ones. Diff comments said "enabled" but implementation returned "installed". Now tries `TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()` first; falls back to folder scan only when reflection fails. |
| A2 | LOW | LOW | yes | `PatchShield.ShieldFinalizerWithResult/Void` incremented `_swallowedOther` on the `return false` (re-throw) path. Counter name implied "swallowed by us" but the value included re-thrown exceptions. Re-thrown == not swallowed; removed the increment from the re-throw branch. |
| A3 | LOW | LOW | yes | `PatchShield._shieldedMethods` dedupe key was `(declaringType.FullName + "::" + method.Name)` — overload methods with same name collided, second overload silently skipped. Changed to `(method.Module.ModuleVersionId + ":" + method.MetadataToken)` with `method.ToString()` fallback. Real risk on overload-heavy targets like `Mission.SpawnTroop`. |
| S3 | (disputed) | DISPUTED | yes | Codex DISPUTED `VersionProbe.DetectViaApplicationVersion` calling `FromParametersFile(null)` with null arg. Confirmed safe: reflective lookup uses `Type.EmptyTypes` parameter array, vanilla method is parameterless. Not a bug. |
| S4 | (disputed) | DISPUTED | yes | **Highest-risk suspect.** Codex was asked whether `ref Type[] __result` on `CollectAssemblyTypesShim.SwallowFinalizer` is a valid Harmony Finalizer signature for an `Assembly.GetTypes()` patch (return type `Type[]`). Codex DISPUTED that it's wrong, citing `Lib.Harmony 2.4.2 MethodCreatorTools.EmitCallParameter` showing by-ref `__result` parameters use `Ldloca` (load local address). Confirmed by reading vendored 0Harmony source. Would have been CRITICAL had Codex confirmed wrong. |
| S6 | (disputed) | DISPUTED | yes | Codex DISPUTED concern about `PatchShield` double-install on second OnSubModuleLoad call. Reviewed install method — `Interlocked.CompareExchange(ref _passOneInstalled, 1, 0)` guards pass 1 properly; pass 2 has its own guard. Not a bug. |
| S7 | (disputed) | DISPUTED | yes | Codex DISPUTED concern about `IncompatibleModDetector` regex stripping XML comments. Confirmed `Regex.Replace(text, @"<!--[\s\S]*?-->", "")` is correct multi-line strip via `[\s\S]` character class. Not a bug. |
| S8 | (disputed) | DISPUTED | yes | Codex DISPUTED concern about `SaveShield._shieldedMethods` dedupe key. SaveShield's dedupe is independent from PatchShield's; uses `MethodInfo` reference equality which is stable for distinct overloads. Not a bug. |

**Summary:** 8 suspects + 3 adversarial findings = 11 evaluated. 6 confirmed, 5 disputed with code citations, 0 false positives.

### Root Cause Analysis (Review 42)

Full RCA at [`docs/reviews/rca-dependencies-foundation-2026-05-27.md`](rca-dependencies-foundation-2026-05-27.md). Summary:

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| S1 | Harmony owner allowlist too narrow | Reflection-target enumeration | Author wrote `StartsWith("TAOM")` based on architectural assumption ("we're protecting TAOM code") without enumerating actual vendored Harmony IDs via ilspycmd | New memory entry `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration`; allowlist now enumerated from `new Harmony("X")` call sites in every vendored DLL |
| S5 | TargetSite attribution wrong for AddSubModule | Reflection-source vs reflection-target confusion | Author used `ex.TargetSite` (the vanilla method that threw) for culprit attribution, didn't decompile `Module.AddSubModule` parameter shape to realise `__args[0].SubModuleClassTypeName` was the authoritative source | Method signature changed to accept `object[] __args`; `TryResolveAddSubModuleTarget` reads SubModuleInfo + Assembly for authoritative attribution |
| S2 | StartsWith("TAOM") matches TAOMBar | Substring-vs-exact-match | Same anti-pattern as `feedback_substring_keyword_matches_external_data.md` — broad StartsWith without considering shorter-name collisions. Also missed adding vendored MBOptionScreen/ModuleLoader/MCM.UI.Adapter/BUTR.CrashReport prefixes | New `IsEngineAssembly(asmName)` helper with exact + dot-prefix checks; `_enginePrefixes` expanded to cover all vendored DLL namespaces |
| A1 | "active modules" actually returned "installed modules" | Documented-vs-actual semantic mismatch | Diff comments said "enabled mods diff" but author wrote folder enumeration — disabled mods would appear in both old and new modlists, hiding the real culprit | Strategy-1 reflection on `ModuleHelper.GetActiveModules()`; strategy-2 folder scan only as fallback (very early init) |
| A2 | Counter name lied about counted set | Counter-naming-vs-control-flow drift | Counter incremented on both swallow path and rethrow path; name suggested only swallows. Audit failed because counter was emitted into session summary log without correlating to actual swallows | Removed from rethrow path; counter now matches its name |
| A3 | Overload dedupe collision | Non-unique dedupe key | Dedupe key was DeclaringType.FullName + Name, overload methods collide. Real risk on `Mission.SpawnTroop` (4 overloads in v1.4.5) and similar | Key is now `(MethodBase.Module.ModuleVersionId, MethodBase.MetadataToken)` with `method.ToString()` fallback |

**Root-cause pattern.** All 6 confirmed bugs share the shape "filter/attribute/enumerate based on assumption rather than enumeration of upstream/vendored data." S1 + S2 enumerate Harmony owners / engine prefixes from convention; A1 enumerates "modules" from convention; S5 attributes via TargetSite from convention; A2 counts via name-from-convention; A3 dedupes via Type.FullName from convention. The general lesson: when filtering/attributing/enumerating against external data, enumerate the source-of-truth via decompile/ilspycmd, don't assume.

### Build & Test (Review 42)

- `dotnet build TAOM.sln` — clean, **0 Errors**.
- `dotnet test TAOM.Tests` — **2,520/2,522 passing, 2 skipped, 0 failed.**
- In-game verification: prior diag.log session showed PatchShield installing successfully (+42 pass 1, +324 pass 2); SaveShield 11/11 shielded; VersionProbe detecting v1.4.5; SubModuleConstructionGuard installed on 2 sites. Post-Codex-fix behavior identical (Codex findings were exception-path corner cases not exercised in normal main-menu reach).

### Codex Quality Notes (Review 42)

- **Decompiled Lib.Harmony source** to definitively settle S4 (by-ref `__result` Finalizer legality). Highest-risk suspect, would have been CRITICAL if confirmed wrong; instead Codex DISPUTED with citation. This is exactly the value of an independent verifier.
- **Enumerated `new Harmony("X")` call sites across every vendored DLL** for S1, producing the concrete allowlist needed for the fix. Claude's first attempt would have used architectural reasoning ("anything starting with `Bannerlord.`"); Codex's enumeration found `butterlib.delayedsubmoduleloader.static` (lowercase, no dots after first segment) and `bannerlord.mcm.ui.optionsgauntletscreenpatch` (lowercase) which prefix matching would miss.
- **Disputed 5 suspects with code citations.** The disputed answers are at least as valuable as the confirmed ones — they prevent over-engineering and surface where the author's risk model differed from actual code shape.
- **Stayed in scope.** All findings in `Dependencies/Foundation/` + immediate wiring. No drift into the BUTR DLL bundle or AliasStubSubModule architecture.
- **Calibration.** HIGH=2, MED=2, LOW=2 matched Claude's verification on every row.

### Process improvement triggered by Review 42

1. **AGENTS.md** updated with 7 new lessons (one per confirmed finding + the disputed-S4 lesson on citing Harmony source for by-ref parameter legality).
2. **New feedback memory** `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration` — sibling rule to `feedback_substring_keyword_matches_external_data`. Both share the "enumerate from source-of-truth, don't filter by convention" shape.
3. **REVIEW-LOG.md** updated (this entry).
4. **Numbering collision corrected.** Initial pass labelled this as "Review 41"; existing Review 41 (CrashReport, 2026-05-25) already existed. Renumbered to 42 in AGENTS.md header + footer + RCA reference + feedback memory.

### Review 43 — Cultural-Feats Terrain Movement-Speed Feats (issue #248, post-`/deep-review` Codex follow-up)

Pipeline: `/deep-review` (5 agents, verdict READY — its one CRITICAL "TerrainType.Snow doesn't exist" was a self-contradictory false positive, refuted by decompile: `Snow = 3`) → `/review-codex` (gpt-5.5 xhigh).

Codex findings: **0 CRITICAL / 1 HIGH / 3 MEDIUM / 1 LOW.** 3 confirmed bugs fixed, 2 declined-with-reason.

| # | Sev | Finding | Verdict | Resolution |
|---|-----|---------|---------|------------|
| 2 | MED | Mordor night feat applied at sea (vanilla night penalty is land-only) | Confirmed | Fixed — `isNight && !IsCurrentlyAtSea` |
| 3 | MED | Feat-culture resolved via `Owner.Culture` only vs vanilla `PartyBaseHelper.HasFeat` precedence | Confirmed | Fixed — `ResolvePartyCulture` mirrors vanilla |
| 5 | LOW | Orphaned/stacked XML-doc summary | Confirmed | Fixed — reordered |
| 1 | HIGH | Snow feats key off terrain not weather | Declined (by design) | TAOM_Map navmesh painted terrain id 3 (Snow); terrain-only is intentional. Codex correctly hedged ("cannot prove the map has zero Snow faces"). |
| 4 | MED | Adapter allocation per speed recalc | Declined | Pre-existing; speed recalc is cached (not per-frame); shared-adapter cache = scope creep for marginal GC win (simplicity-criterion) |

Disputed-as-bug (Codex confirmed no-bug): S2 (Snow enum exists), S3 (XSLT preserves vanilla feats + no double-emit, verified by transform), S4 (additive AddFactor stacking correct), S5 (Mordor magnitudes 0.05/0.05/0.10), S6 (Harad/Aserai double-desert intentional), S7 (feat registration ordering safe).

Root-cause pattern (bugs #2, #3): additive GameModel override read vanilla for the *value* but dropped vanilla's *application conditions* (night land-gate; culture precedence). RCA: `docs/reviews/rca-cultural-feats-terrain-2026-05-28.md`. Generalized the existing `feedback_replicate_vanilla_safety_gates_in_prefix` memory (Prefix-returns-false → also additive GameModel overrides). AGENTS.md updated (review 43, 2 new "what Codex does well" bullets incl. the calibrated-hedge lesson).

Build & test: `dotnet test TAOM.Tests` (deploy-skip flags, game running) → **2624 passed / 0 failed / 2 skipped.** Model fixes are thin-entry-point boundary logic — verified by build + in-game per gamemodels rule.

### Review 44 — Cultural-Feats 3-Pack: party-size retune + volunteer respawn + notable count (issue #255, post-`/deep-review` Codex follow-up)

Pipeline: `/deep-review` (5 agents) → 1 HIGH gap caught (new `_23` RuralNotable NPCs added to `npcs_*.xml` but UNREGISTERED in the culture's `<notable_templates>` spawn pool — engine would have reused `_21`/`_22` and spawned clone notables) → fixed → `/review-codex` (gpt-5.5 xhigh).

Codex findings: **0 CRITICAL / 0 HIGH / 1 MEDIUM / 1 LOW.** All 10 Known Suspects no-bug or disputed.

| # | Sev | Finding | Verdict | Resolution |
|---|-----|---------|---------|------------|
| C1 | MED | `TaomPartyTroopUpgradeModel.cs:26` still used `Owner?.Culture ?? party.Culture` — same systemic pattern Codex 43 fixed for speed model | Confirmed | Fixed — adopted `CultureFeatAdapter.FromOrNull(party)` |
| C2 | LOW | `docs/features/cultural-feats.md` party-size table still showed pre-retune values (Gundabad +30%, Dol Guldur +25%, Gondor +10%, Mordor +30%) | Confirmed | Fixed — table updated with "retuned 2026-05-31" annotation |

**Broader audit triggered by C1.** Author's grep of `Main/Features/**/Models/Taom*Model.cs` for the same culture-resolver pattern found **3 additional sibling models** Codex didn't enumerate: `TaomFoodConsumptionModel`, `TaomPartyMoraleModel`, `TaomPartyHealingModel`. All 4 fixed in one batch. `CultureFeatAdapter` gained a public static `ResolvePartyCulture(PartyBase?)` returning raw `CultureObject?` so the healing model (config-StringId lookup, not feat check) can use the same precedence walk.

Disputed-as-bug (Codex confirmed no-bug): S1 (`_23` two-layer registration complete after deep-review fix), S2 (vanilla `hero.CurrentSettlement` NRE — DISPUTED cleanly: invariant holds since sole vanilla caller passes settlement-staying notables), S3 (`Settlement.OwnerClan` correctly resolves through `Village.Bound.OwnerClan` for villages), S4 (ceiling rounding behaves as expected for all bases × bonuses), S5 (CultureFeatAdapter mirrors vanilla precedence exactly), S6 (SubModule hoist clean, no duplicate `culturalFeats` decl), S7 (test reflection table + descriptions match production), S8 (per-culture sum = 92), S9 (XSLT transform has exactly one `<cultural_feats>` per culture, no duplicates), S10 (no Mordor/Gondor party-size references outside the feat layer).

Root-cause pattern (3 consecutive reviews now): the *partial replication of a multi-layer convention* family in `feedback_replicate_vanilla_safety_gates_in_prefix` has a new sub-pattern — **when fixing a per-model boundary convention in one GameModel, audit ALL sibling `Default*Model` overrides for the same pattern.** Codex 43 fixed the resolver in speed model; the natural follow-up "do other models do the same lookup?" wasn't asked at design time. Codex 44 caught the gap. Memory updated to require sibling-audit step.

RCA: `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md` (now has "Codex follow-up" section). AGENTS.md updated (review 44, 2 new "What Codex does well" bullets: sibling-pattern audits + calibrated dispute on plausible vanilla NREs).

Build & test: `dotnet test TAOM.Tests` (deploy-skip flags, game running) → **2760 passed / 0 failed / 2 skipped** (up from 2735 — new auto-discovered tests for the shared helper paths). ModuleData validator clean. Model fixes are boundary-only — verified by build + in-game per gamemodels rule.

### Review 45 — Cultural-Feats Per-Occupation Town Notable Counts (2026-05-31)

Follow-up refactor on commit `582275f`. In-game testing showed Isengard town had only ~8 notables and uniform AddFactor multipliers collapsed all 4 cultures to the same count due to ceiling rounding on small int targets. Refactor: TAOM-owned `NotableOccupationKind` enum, 9 per-(culture, occupation) `Add` feats replace 4 uniform `AddFactor` feats, 17 new GL NPCs (8 Isengard + 9 Dol Guldur), two-layer registration in both `npcs_*.xml` and `taom_spcultures.xml` `<notable_templates>`.

Pipeline: `/verify` → `/deep-review` (5 agents, all PASS) → `/review-codex` (gpt-5.5 xhigh) → 1 HIGH + 1 MED → fixed → final `/verify`.

Codex findings: **0 CRITICAL / 1 HIGH / 1 MEDIUM / 0 LOW.** 8 Known Suspects: 1 CONFIRMED-NUANCE, 7 DISPUTED-WITH-EVIDENCE.

| # | Sev | Finding | Verdict | Resolution |
|---|-----|---------|---------|------------|
| C1 | HIGH | `ApplyNotableCountFeat_DolGuldurArtisan_AddsOne` missing — the service branch at `CulturalFeatsService.cs:295-296` is reachable, the feat is declared, registered, XML-bound, and in the reflection-init table, but no direct dispatch test exists. | Confirmed | Fixed — added the test between `_DolGuldurGangLeader_AddsThirteen` and `_MordorGangLeader_AddsTwo`. |
| C2 | MED | Template pool size = target (Isengard 14/14, Dol Guldur 15/15). Vanilla `GetRandomTemplateByOccupation` samples with replacement → expected `~0.632 N` distinct archetypes (~9 of 14, ~9.5 of 15), with the remaining ~5 slots being duplicates. | Confirmed (design trade-off, not code bug) | Documented in `docs/features/cultural-feats.md` "Known characteristic — duplicate archetype selection at target = pool size." User can request pool headroom or a no-replacement Harmony patch after in-game observation. Per `simplicity-criterion.md`: tiny cosmetic gain + significant authoring/patching cost = reject as default fix. |

Disputed-as-bug (Codex confirmed no-bug): S2 (`baseCount <= 0` guard is safe for the 5 spawn-pool occupations vanilla returns >0 for), S3 (`MapOccupation` maps exactly the 5 occupations vanilla asks the notable spawn model about; everything else → `Other` → vanilla unchanged), S4 (all 9 `Initialize(...)` bonuses match the target table), S5 (9 C# `Register(...)` IDs match 9 XML `<feat id=...>` lines), S6 (4 deleted uniform IDs absent from live production/config/test/feature-doc surfaces), S7 (17/17 NPCs pass two-layer audit), S8 (village isolation intact — RuralNotable/Headman branch only reads village feats).

Root-cause pattern: **per-axis test count masks cross-product cell gap.** `CulturalFeatsService.ApplyNotableCountFeat` is a switch on `NotableOccupationKind` with per-culture HasFeat branches inside each arm. Tests existed for each occupation axis (5 covered) AND each culture axis (4 covered), and the deep-review Completeness agent confirmed "tests exist for each occupation dispatching to its correct feat" — but `(Dol Guldur × Artisan)` had no dedicated cell test. Codex enumerated the cross-product and caught it. New `feedback_per_branch_dispatch_test_enumeration.md` memory codifies the per-cell rule. AGENTS.md updated (review 45, 2 new "What Codex does well" bullets: per-cell dispatch test gaps in cross-product switches + tracing template-selection / sampling semantics downstream of count-target overrides).

RCA: [`docs/reviews/rca-cultural-feats-per-occupation-2026-05-31.md`](rca-cultural-feats-per-occupation-2026-05-31.md).

Build & test (deploy-skip flags, game running): **2772 passed / 0 failed / 2 skipped** (up from 2771 — +1 confirms the new dispatch test ran). ModuleData validator clean.

### 2026-06-01 — CareerQuestSystem (new feature, full ship workflow)

Codex (gpt-5.5 xhigh) adversarial pass after a 5-agent `/deep-review`. **Deep-review found the 1 HIGH** (`CareerQuest` missing `SpecialQuestType` → `QuestManager.OnGameLoaded` silently cancels the quest on the first save-load); Codex **independently confirmed** that fix + the `List<JournalLog>` save-graph identity assumption + the 4th-persistence-dict correctness by decompiling the installed 1.4.5 save system, then found **5 more (all real, 0 false positives):** `KillEnemyLords` no at-war check (MED), `VisitSettlementType`/`UnlockTier` config-validation gaps (MED/LOW), `GrantItem` silent no-op on a bad id (LOW), `_offerPending` stuck-on-throw (LOW). All fixed in-session + tested. Division of labor: deep-review caught the engine-lifecycle HIGH (it decompiled `QuestManager.OnGameLoaded`); Codex caught game-semantics + config-completeness gaps the plumbing-correct code still had. RCA: [rca-career-quest-system-2026-06-01.md](rca-career-quest-system-2026-06-01.md). Build 0 err, suite **2877 pass**. Codex accuracy this review: 5/5 confirmed, 0 false positives.

### 2026-06-01 — CareerSystem Switch Picker + Effect-Scope Badges (review #46 / meta-RCA — skipped review gates)

**META-RCA: I shipped without running `/deep-review` or `/review-codex` first** — a direct violation of the CLAUDE.md mandatory completion workflow. The user asked "Did you do a /deep-review and a /review-codex" and the honest answer was "no." Both reviews were then run via the Workflow tool ([deep-review](deep-review-career-switch-picker-2026-06-01.md): 49 agents in adversarial-verify-plus-completeness-critic pattern) and Codex (gpt-5.5 xhigh, [codex review](raw/codex-adversarial-career-switch-picker-2026-06-01.md)). The same skipped-gate failure mode shipped at the Cooldown rework two reviews ago (#31, RCA: same — stopped at build-green instead of running the documented closeout).

**Division of labor:**
- Deep-review (5 dimensions × 2 skeptics each + completeness critic = 49 agents) confirmed 20 findings: brush dead, empty-state binding mismatch, multiple dead `[DataSourceProperty]`s, loc-key propagation gap, discarded ctor param, cold-path allocations, test gaps.
- Codex (1 agent, focused suspect prompt + vanilla decompilation) confirmed **4** findings — including **one BLOCKING bug deep-review's 49 agents missed:** `state.IsSwitchMode` is set AFTER vanilla synchronously constructs the screen (`GameStateManager.CreateState<T>` → `HandleCreateState` → `OnCreateState` listeners → `GameStateScreenManager.CreateScreen` → `Activator.CreateInstance(type, state)`). Result: in-flight dialogue path opened the screen in NORMAL mode, never the picker — the entire Concern 2 feature was non-functional in production.

**Codex caught what deep-review missed because** Codex got an 8-suspect prompt that explicitly told it to investigate `GameStateManager.CreateState<T>()` vs `PushState(state)` ordering and decompile the relevant types. Deep-review's compat dimension had similar instructions but the verifier-pair refuted the original raised concern as "Suspect 6 DISPUTED" because it focused on `state.IsSwitchMode = true` being "before PushState" without tracing through to `HandleCreateState`'s synchronous listener fanout. Single-agent-with-targeted-prompt + full vanilla decompilation > 49-agent-with-broad-prompt for narrowly-targeted vanilla lifecycle questions.

| # | Sev | Source | Finding | Resolution |
|---|-----|--------|---------|------------|
| C1 | **HIGH (blocking)** | Codex | `GauntletCareerScreen.cs:48` reads `state.IsSwitchMode` in ctor, but `CreateState<T>` invokes the ctor synchronously via `Activator.CreateInstance` BEFORE `OpenCareerScreen` sets the flag. Picker mode never engaged in production. | Fixed: moved `state.IsSwitchMode` read + adapter creation from ctor to `OnInitialize` (which fires after `PushState`). |
| D1 | HIGH | Both | `Popup.GreenButton` and `Popup.GreenButton.Text` don't exist in vanilla 1.4.5 brushes. Choose button unstyled/invisible. | Fixed: replaced with `Popup.Done.Button.NineGrid` + `Popup.Button.Text`. |
| D2 | MED | Both | Empty-state UX: outer panel gated on `@IsSwitchMode` but VM comment + design intent was `@IsBrowsingTargets`. Empty target list shows blank scroll canvas. Plus `IsBrowsingTargets` computed property never fires `OnPropertyChanged` after `_eligibleSwitchTargets.Clear() + Add()`. | Fixed: kept outer gate on `@IsSwitchMode` (header still shows), added new `HasNoSwitchTargets` VM property + empty-state TextWidget bound to `@NoTargetsMessage`, gated `ScrollablePanel` on `@IsBrowsingTargets`, and added explicit `OnPropertyChanged` for `IsBrowsingTargets`/`HasNoSwitchTargets` after `RebuildEligibleSwitchTargets` mutates the list. |
| D3 | LOW | Both | `EffectScopeTooltip` `[DataSourceProperty]` authored but never bound; `SwitchModeTitle` authored but never bound (redundant with `ScreenTitle`); `AbilitySpriteName` on `CareerSwitchTargetVM` authored but never bound. | Fixed: deleted all three + 2 dead loc keys (`_passive_tooltip`, `_keystone_tooltip`) per simplicity criterion. |
| D4 | LOW | Deep-review | Discarded `switchService` ctor param in `CareerSwitchDialogueBehavior` (`_ = switchService;`). | Fixed: removed param + updated `SubModule.cs` registration. |
| D5 | LOW | Deep-review | `RebuildEligibleSwitchTargets` silently succeeds on empty list — debug triage difficult. | Fixed: added `LogWarning` for empty result. |
| D6 | LOW | Deep-review | Test gaps for null `currentCareerId`, empty `StringId`. | Fixed: 2 boundary tests added. |
| — | — | Both | 8 new loc keys not propagated to 12 language files. | Deferred — `translate_with_claude.py` run at closeout (per existing project workflow). |
| — | — | Deep-review | Cold-path allocations (target VM TextObjects, GetEligibleSwitchTargets list, scroll layout 1-target empty canvas). | Acknowledged, no fix — cold-path acceptable. |

Build 0 err, suite **2896 pass / 2 skipped** (up from 2894 — +2 boundary tests added).

**Preventive actions written:**
1. AGENTS.md "What Codex does well" (review 46): targeted-suspect + vanilla-decompilation prompt catches engine-lifecycle ordering issues that broad multi-agent fan-outs miss when verifiers refute the raised concern at the framing layer rather than tracing to the underlying vanilla code.
2. Memory `feedback_skipped_review_gates_meta_pattern.md` (TODO): SECOND occurrence of the "stop at build-green, skip the documented closeout" pattern in two consecutive shipped features. The CLAUDE.md rule wasn't sticking; mechanizing via session-stop hook that blocks commits touching `Main/Features/**/*.cs` when no recent `/deep-review` or `/review-codex` artifact exists in `docs/reviews/`.

---

### Review 47 — New Factions (Misty Mountain Orcs / Goblins / Goblin Town / Blue Craig / Lindon) (2026-06-02)

4-kingdom / 2-culture data changeset cloned from `gundabad`, reviewed AFTER a proactive clone-leftover fix. **Codex (gpt-5.5 xhigh): 0 CRITICAL / 0 HIGH / 2 MED / 2 LOW.** Then a 5-agent adversarial completeness-audit workflow on top found 1 MED + 1 LOW that both Codex and the prior `/deep-review` (7 agents) missed. All findings verified against source + fixed at the generator source AND live files. Full detail + root-cause table: [rca-new-factions-2026-06-02.md](rca-new-factions-2026-06-02.md) (Phase 2/3).

| # | Sev | Source | Finding | Resolution |
|---|-----|--------|---------|------------|
| C1 | MED | Codex | Goblin Town + Moria faction-map cards advertised Warg-riders/wolf-cavalry, but cavalry was stripped (infantry+archer only). | Reworked to surviving units in `make_new_factions_playable.py`; regenerated factions.json + harvested strings. |
| C2 | MED | Codex | 14×2 notable names still said "pale orc" (clone sibling the "Pale Uruk"→raceword rule missed). | Added "pale orc"/"Pale Orc" remap + post-gen assertion; regenerated. |
| **W1** | **MED** | **Completeness workflow** | `execution/alignment.json` had no row for the 4 new kingdoms → `AlignmentService` Neutral fallback mis-scored execution-relation penalties + disabled the diplomacy same-alignment war-block backstop. | Added orcs=`evil`, lindon=`free`. |
| C3 | LOW | Codex | Lindon strength_1 "unifies armies cheaply" contradicted its own +25% army-influence-cost penalty. | Reworded to describe the +35% influence award; both files. |
| C4 | LOW | Codex | Layout missing `clan_bluecraig_3..5`. | Added (sibling of the goblin-clan sync). |
| W2 | LOW | Completeness workflow | 6 goblin-culture notables read "orc" not "goblin". | Culture-aware " orc "→raceword remap (no-op for the orc culture). |
| — | DISPUTED | Codex + workflow | diplomacy graph (130 rels, 0 invalid/dup/contradictory), feat wiring (8×5 locations), recruitment pools, faction-map↔feat magnitudes, troop structure | Independently verified clean by both — no change. |

Proactive (pre-Codex) clone-leftover DISPLAY-TEXT fix (2 culture names + 2 descriptions + 2 clan-pool names + ~24 loc strings + 36 notable names) — Codex CONFIRMED it had landed. Build 0 err, suite **2914 pass / 2 skipped**, validate_moduledata PASS.

**Preventive actions written:**
1. AGENTS.md "Bugs Codex typically misses" (review 47): whole-file config omissions when adding a faction (enumerate every kingdom-keyed config, don't only audit data present); clone-leftover DISPLAY text.
2. AGENTS.md "What Codex does well" (review 47): good confirm/dispute calibration on a mostly-data changeset; blind spot = whole-file config omissions (needs a completeness pass).
3. Memory `feedback_clone_leftover_display_text.md` (NEW) + generalized `feedback_faction_map_update_with_cultural_feats.md` to cover ALL kingdom-enumerating configs incl. `alignment.json`.
4. Process lesson: the highest-value catch came from the completeness-audit workflow that ENUMERATED the configs that should have a row — not from the targeted reviewers that audited what existed. Keep the completeness pass in the new-faction review chain.

### 2026-06-02 — CareerSystem #102 (refactor) + #104 Option B (CooldownReduction) (review #48)

**Scope.** Two cooperating issues closed in one session: `CareerPerkMissionBehavior.cs` 302 → 139 LOC via three controllers + two adapters (#102), and 98 dead `MaxCharge` mutations in `taom_career_choices.xml` repurposed as `CooldownReduction` with a 5s `MinCooldownSeconds` floor (#104).

**Reviews run (in order):**

| # | Review | Engine | Findings | Notes |
|---|--------|--------|----------|-------|
| 1 | First Codex pass | gpt-5.5 xhigh | 1 MED + 1 LOW | MED: `_attachedScreen` ownership — `ScreenBase.RemoveLayer` calls `HandleFinalize()` unconditionally with no `_layers` ownership check (verified v1.4.5). LOW: `dt` semantics — vanilla `Mission.OnTick` scales by `Scene.TimeSpeed` and splits fast-forward, comment tightened. |
| 2 | Deep-review fan-out | Sonnet × 23 (5 dimensions + adversarial verify + completeness critic) | 2 HIGH + 4 MED + 9 LOW confirmed; 6 refuted | HIGH #7 (HUD layer screen-mismatch + Singleton state leak on Cleanup throw) corroborated Codex #1 MED with the vanilla `ScreenBase` decompile; HIGH #11 (ADR-008 test coverage on `AbilityHudController` + `AbilityEffectExecutor`) refuted by ADR-008 boundary-class rule. MED #4 (single-outcome enum drops legacy dual-toast UX when ability becomes ready AND V pressed on same frame) was the only behavior-change finding the first Codex missed — it framed the refactor as structural-only. MED systemic ("singleton-controller-per-mission-behavior lifetime asymmetry") captured the WHOLE PATTERN behind HIGH #7 — encoded as new memory entry. |
| 3 | Second Codex pass (post-fix diff) | gpt-5.5 xhigh | 1 HIGH + 2 LOW | HIGH: **CooldownReduction was dead.** The XML emitted `-6`/`-9`; `flat` calculator is `baseValue + value` so mutated property was negative; `AbilityEffectExecutor` guards on `> 0f` → 100% of activations skipped the adjustment. Fixed via positive-value re-run (50× `6`, 48× `9`) + idempotent script regex that repairs the sign on re-application. LOW#1: cooldown adjustment moved AFTER `executor.Execute(context)` so a throw in the effect executor no longer shortens cooldown for a failed activation. LOW#2 (dead `MaxCharge` property remains): deferred — domain shape still supports the charge-based path; removal is back-compat-affecting and tracked as future cleanup. |

**Fix verdict (all three review passes):** 9 actionable findings applied (2 from Codex pass-1, 5 from deep-review, 1 HIGH + 1 LOW from Codex pass-2), 8 refuted with explicit rationale (boundary-class ADR-008 exemption, simplicity-criterion YAGNI, vanilla-decompile-confirmed-current-behavior), 4 deferred as out-of-scope or tracked elsewhere (sprite-bake gap → #101, eager-logger-interpolation latent pattern, `AbilityTemplateData.MaxCharge` property kept for back-compat, end-to-end XML→guard test).

**Net counts:** 28 new tests (13 `AbilityActivationControllerTests` post-flags-struct rewrite + 15 `CooldownReductionTests`). 2942/2944 tests passing. Build green. `CareerPerkMissionBehavior.cs` 139 LOC under ADR-002 150 ceiling. 0 `MaxCharge` XML mutations remain; 98 `CooldownReduction` (50× -6, 48× -9).

**Highest-value catch:** **Codex pass-2 HIGH — `CooldownReduction` was dead.** Of all three review passes, this is the only finding that would have shipped a non-functional feature. The deep-review's 5 dimensions × adversarial verify + Codex pass-1 BOTH validated each layer in isolation (XML rewrite correct count, `AdjustCooldown` math correct on positive inputs, executor plumbing correct) but neither traced the full XML-value → flat-calculator → mutated-template → executor-guard path with a real shipped XML example. Codex pass-2 grepped `CooldownReduction` values in the actual XML, traced through `BuiltInCalculators.flat`, and noted the executor's `> 0f` guard would skip all 98 mutations. Phase 3e RCA: when a feature gates on a sign or comparison, the test surface must include an end-to-end smoke that drives the full XML → mutation → guard path with a real shipped choice, not just synthetic unit tests of each layer.

**Second-highest catch:** deep-review MED systemic finding ("singleton-controller-per-mission-behavior lifetime asymmetry"). The HIGH dataflow finding had named the symptom in `AbilityHudController`; the systemic finding named the CLASS — same pattern applies to `AbilityActivationController` (if `_activationController.Reset()` were skipped, `_abilityReadyNotified=true` leaks across missions and the green toast never re-fires). The deep-review completeness critic asked "is this a feature or a category of bug?" and surfaced the rule. New memory: `feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md`.

**Preventive actions written:**
1. New memory `feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md` — when extracting state machines from per-mission MissionBehavior into Reuse.Singleton controllers, every controller field becomes cross-mission state; mitigation is per-step try/catch in OnEndMission. Names other TAOM features with the same shape for proactive audit.
2. AGENTS.md "Lessons" updated with the review 48 row — calls out Codex pass-2's `CooldownReduction` HIGH as the only would-have-shipped-broken finding and the deep-review systemic capture as the second-highest finding.
3. New memory `feedback_end_to_end_xml_to_guard_smoke_required.md` (NEW) — when a feature gates on a sign / comparison / non-zero check downstream of a config calculator pipeline, the test surface MUST include an end-to-end smoke driving a real shipped XML choice through the full pipeline. RCA: Codex pass-2 #48 caught `CooldownReduction` dead because the layer-by-layer unit tests passed but the XML→calculator→guard composition was broken.
4. CHANGELOG `2026-06-02` section lists all surviving findings + their resolutions per CLAUDE.md "Documentation Requirements (MANDATORY)".
5. `docs/features/career-system.md` rewrites the OnMissionTick lifecycle section + adds a CooldownReduction Mutations subsection (positive-value designer convention, application order with executor-throws-first ordering, history including Codex pass-2 sign-flip catch).

---

## Review 49 — CultureConversion (2026-06-02)

New feature: conquered town/castle (+ bound villages) gradually adopts the new owner's culture + troops. Reviewed via a 16-agent deep-review **workflow** (5 dimensions × adversarial-verify per finding + completeness critic, each critic finding re-verified) then a Codex `gpt-5.5 xhigh` adversarial pass.

**Deep-review:** 10 findings raised → **6 confirmed, 4 refuted**. The 4 refutations included a plausible HIGH (R6 "store cleared while `Settlement.Culture` still holds an in-memory converted value") proven architecturally impossible by the verifier. Confirmed: 1 real code bug (`ReapplyConvertedCultures` discarded `SetSettlementCulture` failure → stale `IsConverted` record on mod-version culture removal), 2 cross-feature doc gaps (RevoltTuning loyalty coupling, CultureMarketplace goods hold-window lag), 3 test gaps. All fixed/deferred-with-rationale.

**Codex:** 1 HIGH + 3 LOW; **DISPUTED 5 of 7 Known Suspects with decompiled evidence** (event-ordering, R6, save/load guard, cascade-refactor all verified safe). The HIGH (`HasCulturePool` gate excluded 5 playable cultures — Rohan/Khand/Harad/Mirkwood/Umbar — because it was defined by the existing `CultureMap` keys, not the full playable-culture set) was the highest-value catch and no deep-review dimension found it.

**Preventive actions:**
1. Added `CultureMap["vlandia"]` (Rohan) + `["aserai"]` (Harad) culture pools — the 2 missing cultures with existing recruitable troops; documented Khand/Mirkwood/Umbar as a known gap (no `is_basic_troop` set authored).
2. New enumeration test `HasCulturePool_PlayableCultureWithTroops_ReturnsTrue` (+ `..._WithoutTroopSet_ReturnsFalse_KnownGap`) pins the FULL playable-culture domain against the gate — institutionalizes "enumerate the domain, don't spot-check the entries."
3. AGENTS.md "Lessons" review-49 row + new "What Codex does well" bullet (enumerate-gate-domain). Same lesson as review 47's `alignment.json` whole-file omission.
4. RCA: docs/reviews/rca-culture-conversion-2026-06-02.md (deep-review + Codex sections). CHANGELOG `2026-06-02` updated. Build green, **3026/3028 tests pass** (+23 review tests).

---

## Review 50 — Cultural-Feats Wave 1 Expansion (2026-06-07)

24 new Q-class cultural feats across 11 cultures (105 → 129), each plugging into an existing `CulturalFeatsService.Apply*` method via a `HasFeat` check. Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`. **Both reviews were run AFTER commit + push** — a process miss (the user asked "did we do a deep review and codex review?"); documented as the headline RCA lesson.

**Deep-review:** 4 of 5 agents PASS clean (Standards, Compatibility — no new API, Efficiency, Data-Flow 24/24 CONNECTED). Completeness flagged 1 HIGH process gap: **no GitHub issue** existed at commit time → created [#273](https://github.com/haterade22/TAOM/issues/273) retroactively. The per-(culture,axis) dispatch-test coverage (the Review 45 / `feedback_per_branch_dispatch_test_enumeration` lesson) was correctly applied this time — 24/24 dispatch tests present.

**Codex:** **0 CRITICAL / 0 HIGH / 1 MEDIUM / 2 LOW**, all 7 Known Suspects CONFIRMED CLEAN (sign/flag conventions, army-influence penalty direction, negative-Add loyalty mechanics + balance via vanilla drift decompile, XSLT passthrough safety, register↔XML exact match, no U+2212, no axis collision). MEDIUM = production feat metadata (EffectBonus sign / IsPositive / AdditionType / string-id) for the 24 feats was **unpinned by tests** — the dispatch tests use a mirror table of fake FeatObjects, and `RegisterAll_UsesCorrectStringIds` (despite its name) only counts fields. A production sign-flip would pass every test. LOW = stale feature-doc table + stale XSLT comment.

**Preventive actions:**
1. Added `Wave1Feats_ProductionMetadata_MatchesSpec` — source-parses `TaomCulturalFeats.cs` to pin all 24 feats' `Register("id")` + `Initialize(bonus, isPositive, AdditionType)` against a canonical spec (closes the MEDIUM; a sign-flip now fails the build).
2. New memories: `feedback_review_before_commit_not_after` (the process miss — repeat of rca-crash-report-2026-05-25) and `feedback_mirror_table_drifts_from_production` (mirror tables need a mirror==production assertion).
3. AGENTS.md "Lessons" review-50 row + new "What Codex does well" bullet (catch mirror-table drift) + new "Bugs Codex typically misses" note for the deep-review Completeness agent (test *presence* ≠ test *power*).
4. Promoted the Wave roadmap to `docs/research/cultural-feats-roadmap.md`; feature-doc Wave-1 section added; LOW fixes applied.
5. RCA: docs/reviews/rca-cultural-feats-wave1-2026-06-07.md (deep-review + Codex sections). CHANGELOG `2026-06-07` updated. Build green, **3092/3094 tests pass** (+1 metadata test on top of the +48 Wave-1 test cases).

## Review 51 — TroopWeight Phantom-Wounded Display Fix (2026-06-07)

Four display-only Postfixes that fix phantom "wounded" troops caused by the TroopWeight feature weighting `PartyBase.NumberOfAllMembers` but not its sibling `NumberOfHealthyMembers` (vanilla derives `wounded = all − healthy` on 4 surfaces). User-reported. Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh` — both BEFORE commit this time.

**Deep-review:** Standards / Compatibility (8/8 v1.4.5 members verified) / Completeness PASS. Found 1 HIGH (per-call `List` allocation on the nameplate hot path) + 1 MED (missing version cache) — both fixed (shared zero-alloc `WeightedContribution` helper + leak-free `ConditionalWeakTable` cache). 1 MED rounding note documented (integer weights only → never manifests).

**Codex:** **1 CRITICAL / 0 HIGH / 1 MED / 1 LOW.**
- **MED (CONFIRMED, fixed):** `GameMenuPartyItemVM.PartyWoundedSize` vanilla setter has a copy-paste guard bug (`value != _partySize`), silently dropping a wounded write equal to the current PartySize. Fixed with a PartySize-nudge before the wounded write. **This is the bug deep-review missed** — the Compatibility agent confirmed the property is public-set but never read the setter BODY. Generalised to AGENTS.md + memory `feedback_taleworlds_vm_setter_decompile`.
- **LOW (CONFIRMED, fixed):** healing-block strip also removed the next section's leading spacer; now preserved.
- **CRITICAL (DISPUTED, declined w/ evidence):** ADR-007 — service exposes sealed `PartyBase`/`TroopRoster`. Pre-existing across 4 shipped methods; this change added ZERO new sealed types (and added an engine-free pure method). Refactoring the whole service behind a roster adapter is a legitimate follow-up, not a ship-blocker for a display fix. Codex over-weighted a pre-existing condition — logged as a new Codex false-positive pattern in AGENTS.md ("`git blame` the signature before rating an architectural finding CRITICAL/blocking").
- Suspects 1 (VersionNo cache — independently confirmed valid via `AddToCountsAtIndex` → `UpdateVersion()`), 3 (rounding — 84 integer weights), 5 (toggle off), 6 (surface completeness — exactly 4) all DISPUTED by Codex = no bug; matched my analysis.

**Preventive actions:** memory `feedback_weighted_getter_in_derived_family` (override one operand of an engine `derived = A op B` getter family → audit every sibling-combining consumer, which lives in unrelated files); AGENTS.md review-51 bullets (Codex does-well: VM setter-body decompile; false-positive: pre-existing-condition-as-CRITICAL). RCA: `docs/reviews/rca-troopweight-phantom-wounded-2026-06-07.md`. CHANGELOG `2026-06-07`. Build green, **3106/3108 tests pass**.

## Review 52 — Elephant Behavior-Tree Cooldown Attack System (2026-06-10)

The AI war-elephant's attacks reworked from the upstream pack's random per-tick trample roll into a per-agent behavior tree (warg pattern) with deterministic cooldowns: enemy-in-range → trample (10s) → else left/right tusk swing by enemy bearing (4s) → else idle (engine mount AI continues). Reviewed via a custom 13-agent adversarial workflow FIRST, then the stock `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh` — all three BEFORE any commit (feature still uncommitted; TEMP `harad_militia` test entry deliberately out of the tree). **First all-clean sweep in the log: deep-review READY (0 HIGH/MED), Codex CLEAN (0/0/0/0), and the two AGREE on every Known Suspect.**

**Custom adversarial workflow (ran first, front-loaded the findings):** 13 agents, 10 raised → 10 confirmed / 0 refuted (1 MED + 9 LOW; no HIGH). The MED was a self-contradicting feature-doc Key Files table (stale `ShouldAiTrample`/`TrampleChancePerTick` API). 4 acted on: (1) MED doc table corrected; (2) engage gate switched from per-eval `GetCurrentAction(0).GetName().Contains("attack")` (native string marshal) to a zero-alloc Index compare against shared `ElephantAttackActions` caches — also collision-immune; (3) `act_none` Armory-drift guard added to `ElephantMissionBehavior.Initialize` (the bad-name → channel-0 locomotion-kill "slide" class that shipped 2026-06-09); (4) `base(10)` documented as a dead int-division throttle (SleepTasks are the real pacing knobs). 5 recorded as accepted-behavior in `docs/features/elephant.md` "Review notes" (full radial side-swing damage, near-arbitrary left/right in crowds, ~10.7–12s effective trample period, wall-clock cooldowns, 253-line MissionBehavior incl. the deferred crew spawn).

**Deep-review (5 agents):** Standards PASS (service TaleWorlds-free; old `ShouldAiTrample` API fully removed per ADR-004; lazy-cached IoC; multi-class file matches warg precedent). Compatibility 32/32 v1.4.5 APIs verified, 0 incompatible. Completeness COMPLETE (16/16 service tests cover all 4 methods + edges incl. the no-enemy −1 sentinel + cooldown exact-boundary + future-stamp clock-skew). Data-Flow 8/8 traced, **0 gaps** — independently confirmed the #1 risk (blackboard property-copy contract + `BTBlackboardValue` reference-sharing so cooldown stamps engage) and the TargetBearing write-before-read ordering. 1 LOW (first-tick linear de-dup scan) DECLINED per simplicity-criterion (parallel `HashSet` for <20 elephants = tiny-gain-plus-complexity).

**Codex (gpt-5.5 xhigh):** **VERDICT CLEAN, 0 findings.** All 8 Known Suspects DISPUTED-with-evidence (6) or confirmed-accepted-behavior (2). Standout: independently decompiled `Vec2.LeftVec()=(-y,x)` to confirm bearing handedness (positive cross-z = LEFT) and verified `BTBlackboardValue<T>` is a class (reference-shared) so cooldowns engage — both load-bearing assumptions read from source. No engine-API, lifecycle, or isolation finding. **Zero disagreement with deep-review** = the highest-confidence outcome available.

**Preventive actions:** none required (0 confirmed bugs across all three passes). AGENTS.md review-52 line + new "What Codex does well" bullet (decompile-to-settle-a-convention). No RCA file (the gate is per-confirmed-bug). Build green, **16/16 ElephantAttackService tests pass**. NOTE: the verified clip-role mapping (attack_3/4 = trample thrash, attack_1 = left, attack_2 = right) was settled by Blender trajectory measurement, not in-game — the one remaining game-only check is the attack *rhythm* feel.

---

## Review 53 — CulturalFeats `PartyBase.Culture` NRE fix + party-culture chokepoint migration (2026-06-15)

A campaign-map `NullReferenceException` (`Army.OnSiegeStarted` → party `EstimatedStrength`/`Morale`/`PartySizeLimit`) root-caused to `CultureFeatAdapter.ResolvePartyCulture` calling `party.Culture`, which is the unguarded vanilla `PartyBase.Culture => MapFaction.Culture` (PartyBase.cs:255) — NREs inside the getter when `MapFaction == null` (a faction-less party). Fix: null-safe `?.` chain + migration of all 9 party-culture GameModels onto the single chokepoint. Reviewed via `/deep-review` (5 agents, READY) + 2 verification workflows + Codex `gpt-5.5 xhigh`.

**Codex VERDICT: ISSUES FOUND — 0 CRITICAL / 2 HIGH / 2 MED / 1 LOW.** The highest-value catch of the whole chain, missed by all 7 prior agents: every `TaomXxxModel` calls `base.XxxMethod()` FIRST, and 4 vanilla base methods (`DailyBeingAtArmyInfluenceAward`, `CalculateRenownGain`, `CalculateFinalSpeed`, `GetGoldCostForUpgrade`) call vanilla `PartyBaseHelper.HasFeat` → the same throwing `party.Culture`, so the NRE can still fire inside the base call — which TAOM-side null-safety cannot reach. A `Main/`-scoped data-flow sweep is structurally blind to a crash inside vanilla `Helpers.PartyBaseHelper`.

**Verification (2 researcher + 1 design agent, v1.4.6 decompile):** 2 of the 4 base-method findings reachable exactly as Codex stated (ArmyManagement HIGH, BattleReward MED-downgraded-from-HIGH); 1 over-attributed (`CalculatePartyInfluenceCost` NREs on `LeaderHero.GetRelation` line 64 before reaching `HasFeat`); the 2 MED (Speed, TroopUpgrade) confirmed. Completeness sweep cleared 4 un-flagged base methods (PartySize/Morale/FoodConsumption/Raid have no `HasFeat`), confirming the original crash was TAOM-side.

**Root fix (1 patch, not Codex's per-model "inline the vanilla calculation"):** Harmony Prefix on `Helpers.PartyBaseHelper.HasFeat` (`PartyBaseHelper_HasFeat_Patch`, `Patch18_CulturalFeats`) → `ResolvePartyCulture(party)?.HasFeat(feat) ?? false`. Fixes every base caller + future caller, behaviorally identical to vanilla for non-crashing inputs. Plus the LOW doc-comment fix in `IWageModifierService.cs`.

**Preventive:** RCA Phase 3e addendum (`rca-culturefeat-partyculture-nre-2026-06-15.md`) with the generalizable rule (override-calls-base → audit the base's derefs on degenerate inputs); memory `feedback_taleworlds_computed_getter_nre_route_through_chokepoint` extended with the base-call lesson; AGENTS.md "What Codex does well" bullet added; `.claude/rules/adapters.md` strengthened with the named computed-getter trap. Build clean (0 errors); suite green except 9 pre-existing `GetVolunteerTroopId_DolGuldur*` failures from an unrelated working-tree `TEMP-SPIDER-TEST` weight bump (2835 passed with that class excluded). HasFeat Prefix is test-via-game (ADR-008); in-game confirmed 2026-06-15 (no recurrence in normal play). Committed 0046eaf; issue #281 closed.

---

## Review 54 — Player Alliance Freedom (player-founded kingdoms can form alliances) (2026-06-16)

A user reported player-founded kingdoms can't make alliances. Root cause (decompile-verified, v1.4.6): **not** a TAOM block — two vanilla gates (player can never *initiate*; a new player kingdom can't clear `CanMakeAlliance`'s 50f score wall, so AI never offers and the vanilla Kingdom-screen button stays greyed). Fix scoped to `Main/Features/Diplomacy/`: player-aware score (+1000) + permission bypass on the two GameModels (turns the existing vanilla button on — no custom UI), plus a `PlayerAllianceProposalBehavior` dialog to initiate. Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`.

**Deep-review:** 1 HIGH caught + fixed — a duplicate `<string id="taom_alliance_formed">` collision with a pre-existing harvested string (the **data-flow** agent's string-key→consumption trace found it; the completeness agent checked presence but not uniqueness). Renamed the new key to `taom_player_alliance_formed`. Two other flags triaged as a disputed ADR-002 stylistic note (pre-existing permission-gate idiom) and an overstated efficiency flag (`CanPlayerProposeAlliance` is dialog-only, not in any AI tick).

**Codex VERDICT: ISSUES FOUND — 0 CRITICAL / 1 HIGH / 1 MED / 1 LOW**, all verified against source + v1.4.6 decompile and all confirmed real; fixed in-session. Codex correctly DISPUTED the core mechanic (verified +1000 clears every `CanMakeAlliance` gate in both directions incl. `CanMakeAllianceWithPlayerSupport`; confirmed the string-key fix complete + the 2-arg regression path byte-identical).
- **HIGH (highest-value catch, missed by all 5 deep-review agents):** `InvolvesPlayerKingdom` (both GameModels) used `Clan.PlayerClan?.Kingdom` without checking the player *rules* it — a **vassal/mercenary** player's AI-ruled liege kingdom would get the freedom bypass, changing AI-vs-AI diplomacy. The dialog helper had the correct `RulingClan == PlayerClan` check; the two model helpers diverged from it. Fixed by extracting one `PlayerKingdomHelper.GetPlayerRuledKingdom()` used by all three sites (single source of truth).
- **MED:** dialog target accepted any ruling-*clan* member, not the kingdom **leader** — fixed to require `kingdom.Leader == hero`.
- **LOW:** dialog showed "alliance forged" after a possibly-no-op `void FormPlayerAlliance` — made it return `bool` (confirms `AreAllied`), gated the message, +2 tests.

**Root-cause pattern:** HIGH+MED are the same shape — a too-loose identity predicate (membership vs *rulership*); the HIGH specifically came from **helper duplication drift** (a predicate that must agree across N entry points should have one definition). Same lesson as the string-id finding from another angle. Deep-review's data-flow agent verified `involvesPlayer` was symmetric + null-safe but didn't enumerate the player's own faction-role states (ruler / vassal / mercenary) — a state-enumeration gap analogous to the entity-state-matrix rule applied to political status.

**Result:** build clean; Diplomacy suite 35/35 (the repo's 9 `GetVolunteerTroopId_DolGuldur*` failures are pre-existing, unrelated working-tree troop drift). New memory `feedback_verify_string_id_unique_before_add`. RCA: `docs/reviews/rca-player-alliance-freedom-2026-06-16.md` (deep-review + Codex sections). Issue #284. Reviews ran BEFORE commit (no after-push miss this time).

---

## Review 55 — Player Alliance Durability (follow-up to #284) (2026-06-17)

A first-pass fix for the in-game "player alliance vanishes from the encyclopedia" report: `DiplomacyService.IsWarAllowed` was extended to block war between the kingdom the player *rules* and a current ally (stopping vanilla `OnWarDeclared → EndAlliance` from auto-dissolving a `Neutral`-tier player alliance, which TAOM's end-protection covered only for `Permanent` pairs). Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`.

**Deep-review: READY (missed the real bugs).** Standards/Compat/Completeness PASS; its two efficiency flags were dismissed on code re-read (the `AreAllied` scan is short-circuited behind the player-involvement check; the diag string is inside the guarded `if`). Its data-flow agent **wrongly asserted "the player can break the alliance via the vanilla Break Alliance UI"** and so cleared the design — there is no such UI.

**Codex VERDICT: ISSUES FOUND — 0 CRITICAL / 2 HIGH / 1 LOW. Outcome: war-block REVERTED, diagnostics-only shipped.**
- **HIGH C1 (soft-lock, the highest-value catch):** v1.4.6 `KingdomDiplomacyVM` exposes only propose-Alliance / declare-War / declare-Peace / TradeAgreement — **no break-alliance action**. The player's only exit from an alliance is to *declare war on the ally* (`OnDeclareWar` → `DeclareWarDecision` → `DeclareWarAction.ApplyByKingdomDecision`). The fix blocked that at both `TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` and the `DeclareWarAction` prefix → player trapped for ~100 years. **Verified by me:** decompiled the VM (no break action) + grepped all 3 `EndAlliance` callers (all internal: expiry/war/daily-cleanup).
- **HIGH C2 (call-to-war atomicity):** `StartCallToWarAgreement` commits agreement + gold + event + bonuses before `ApplyByCallToWarAgreement`; blocking the war at `ApplyInternal` leaves a paid agreement with no war.
- **LOW C3:** the player-ruled predicate was duplicated in `AllianceAdapter.GetPlayerRuledKingdomId` + `PlayerKingdomHelper` (review-54 drift shape).

**Decision (user-confirmed): revert.** `IsWarAllowed` restored to prior behavior; `GetPlayerRuledKingdomId` (no remaining consumer) + its 4 tests removed; only the `[Diplomacy][diag]` logging (StartAlliance Postfix + EndAlliance line) ships, to confirm form-then-broken vs never-persists in-game before any behavioral fix. 35/35 Diplomacy tests green after revert.

**Root-cause pattern:** (1) shipped a behavioral fix for an **unconfirmed root cause** (the diagnostics that would confirm the trigger hadn't run in-game) — violating the plan's own diagnose-first Iron Law; (2) **blocking a state transition trapped the entity** — "protect the alliance" was implemented as "block the war," but the war was the player's only exit. Lesson: before blocking a transition to protect state, enumerate the entity's exits and confirm a deliberate one survives. Memory extended: `feedback_new_engine_state_audit_what_undoes_it` (exit-survival corollary). RCA: `docs/reviews/rca-player-alliance-freedom-2026-06-16.md` (Codex-review-of-durability-fix section). Codex caught a soft-lock by decompiling the VM to enumerate real player actions — the same independent-decompile strength as prior reviews; the deep-review's failure was an unverified UI assertion relayed as fact (evidence-over-claims §A.4).

---

## Review 56 — AlignmentRecruitment (2026-06-17)

New feature (issue #286): a recruiter cannot recruit volunteers at a settlement controlled by an enemy-aligned kingdom, via a single override of `VolunteerModel.MaximumIndexHeroCanRecruitFromHero` in `TaomVolunteerModel` returning `-1` (the engine's own "recruit nothing from this notable" signal). Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`.

**Deep-review: READY (0 code findings).** Standards/Efficiency/Data-flow clean; Completeness flagged only the (then-)missing GitHub issue. Its Compatibility agent verified the override signature and that `-1` blocks all slots — but **falsely asserted `MaximumIndexGarrisonCanRecruitFromHero` has "zero callers"** (a cache-only grep miss).

**Codex VERDICT: ISSUES FOUND — 0 CRITICAL / 1 HIGH / 1 LOW. All 6 Known Suspects DISPUTED / DESIGN-QUESTION with decompiled evidence.**
- **Suspect 1 (recruiter-basis asymmetry) DISPUTED — Codex's highest-value work:** decompiled `Hero.MapFaction` (= `Clan.Kingdom ?? Clan`) and the mercenary-service chain (`StartMercenaryServiceAction → ChangeKingdomAction.ApplyByJoinFactionAsMercenary` sets `clan.Kingdom`), proving `buyerHero.Clan.Kingdom.StringId` is alignment-equivalent to `MapFaction.StringId` in every case. The asymmetry I flagged as the lead suspect is a non-defect — no change made. **Verified independently** (Hero.cs:566; garrison/merc citations).
- **Suspect 2 (garrison) DISPUTED — corrected my deep-review agent:** the Garrison method IS live (`GarrisonRecruitmentCampaignBehavior` calls `VolunteerModel.n(town.Settlement, notable)` ×2). But recruiter = town owner, source = same town → same kingdom → never blocked; non-override is safe.
- Suspects 3/6 DISPUTED (Village.MapFaction = vanilla parity; `useValueAsRelation` hard `-1` correct for an alignment block — relation can't unlock alignment). Suspects 4/5 DESIGN-QUESTION (MCM-over-JSON precedence; companion-led-party `isPlayer`) — both documented as intentional.
- **HIGH (Codex) / MEDIUM (mine): test-coverage gap** — `GoodRejectsEvilMode_MatchesMatrix` covered 6/9 (recruiterSide × sourceSide) cells while the doc claimed per-cell; the symmetric matrix was full 9 but the second mode was trimmed. **No runtime risk** (the 3 omitted cells are all Neutral→no-block, behaviorally covered by the shared Neutral early-return). Fixed → 9/9; suite 28→31, all green.
- **LOW: doc** — How-To overstated JSON runtime control; MCM shadows JSON in-game. Fixed (How-To now states MCM is authoritative, JSON is the compiled/test default).

**Process:** both reviews ran BEFORE the closing commit; the GitHub issue was created before the closing commit (not retroactively, reversing the review-50/54/55 misses). No behavioral fix needed — the override, config validation, ADR compliance, and API usage were clean.

**Root-cause pattern (second-instance trimming):** the primary case was done right and the secondary under-specified — symmetric matrix full 9 / second mode trimmed; first config knob documented / MCM-over-JSON precedence unstated for both knobs. Same shape as the native-port hot-path miss. Lessons: (1) every mode matrix must be full N×N when the doc claims per-cell; (2) review "zero callers"/"not found" claims must grep the full `E:\Decompiled_Bannerlord` tree, not the taom-src cache. RCA: `docs/reviews/rca-AlignmentRecruitment-2026-06-17.md`.

---

## Review 57 — ShaderPrecompilation re-enable + scene-walk (issue #287) (2026-06-17)

Re-enabled the "Pre-compile Shaders" InitialStateOption (disabled 2026-05-22) and extended it from a single all-characters custom battle to a WALK of each TAOM battle scene, so each scene's terrain + forced-atmosphere shaders compile up front — the class behind the intermittent `d3dcompiler` battle-load CTD (some players, not others; GPU/driver-dependent runtime compile at `Mission.Initialize`). The runner chains custom battles via `MBGameManager.StartNewGame → EndGame → next item`; per-item compile detection is the pure unit-tested `ShaderPrecompileDecider`. Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`. ADR-008: the engine orchestration is in-game-only, so the reviews targeted the runtime SEAM (clock source, callback identity, teardown signal, scene loadability), not signatures (already v1.4.6-verified).

**Deep-review: 1 HIGH (its own catch) + the orchestration otherwise clean.** Agent 5 (data-flow / observation-matrix) caught the HIGH; Agents 1/3/4 (standards/efficiency/completeness) passed the orchestration; Agent 2 (API-compat) surfaced the teardown concern but reached the WRONG conclusion (see below).

**Codex VERDICT: ISSUES FOUND — 0 CRITICAL / 1 HIGH / 2 MED / 2 LOW (1 deferred limitation). Pure core (planner/decider/scene-provider) DISPUTED-clean; every confirmed defect was in the engine-coupling seam.**
- **HIGH (deep-review Agent 5, NOT Codex) — premature-advance clock source:** the decider's "nothing to compile, advance after grace" counted item-START (StartGame) time, not RENDER time. A heavy `_forceatmo` scene still LOADING on an HDD would exceed the 20s grace and be SKIPPED before any shader compiled — defeating #287 on exactly the heavy scenes. Fixed: thread `LoadingWindow.IsLoadingWindowActive` into `Decide`; grace now counts from the first non-loading frame (`_renderStartedMs`). Regression test `Decide_StillLoading_NeverAdvancesOnZero_EvenPastGrace`.
- **Codex's highest-value catch (NO deep-review agent found it) — stale cross-item static callback:** `TaomShaderGameManager.NotifyItemRendering/Failed` forwarded to the reused runner with only a `_state==Starting` guard, which could NOT distinguish item N from N+1. A timed-out item N's late engine-fired `OnLoadFinished` would flip N+1 to Running on N's callback, corrupting N+1's clock (`NotifyItemFailed` had no guard at all). Fixed: per-item GENERATION tag — `StartCurrentItem` does `++_generation`, the game manager captures it, callbacks echo it, the runner ignores any whose generation ≠ current. Removed the now-dead `IsShaderBattleActive` static.
- **MED (Codex) — teardown timeout, corrected a disputed agent claim:** I had cut `EndTimeoutMs` 60s→30s on Agent 2's (wrong) claim that `Game.Current` never nulls after a custom-battle `EndGame`. Codex decompiled the clean chain `EndGame → Mission.EndMission → MissionState CleanStates → Game destroyed → Game.Current==null` and proved it DOES fire — a 30s force-advance risks `StartNewGame`-ing the next item onto a half-torn-down state stack. Fixed: `EndTimeoutMs` reverted to a generous 90s LAST-RESORT backstop; the clean `Game.Current==null` path is the normal exit; `TickEnding` logs the live state at 1 Hz to confirm which path fires in the first walk. (`feedback_codex_caught_api_misread`: decompile when reviewers disagree — don't tune toward the more confident one.)
- **LOW (Codex) — orphan scene:** `taom_dwarves_battle_001_forceatmo` was in the default scene list (built from a filesystem `find`) but is absent from `custom_battle_scenes.xml` — it can't load as a custom battle and has zero coverage value. Fixed: excluded from `DefaultScenes` + the config (9→8 scenes).
- **LOW (Codex, deferred limitation, no silent defer):** the character battle adds up to 3000 troops/side but the engine caps actual deployed/render size, so not every troop's shaders compile in one pass (pre-existing). DOCUMENTED as a known limitation (CHANGELOG + feature doc); the #287 SCENE shaders are fully covered. Full character coverage (roster batching across multiple battles) = iteration 2.
- **Watchpoint (Codex DISPUTED as a shipped bug):** render-grace relies on `IsLoadingWindowActive==false` as a proxy for "scene rendered, shaders queued"; bumped 20s→30s for margin and flagged as the #1 thing to watch in the first walk's log.

**Process:** both reviews ran BEFORE any commit; the feature is still uncommitted (selective-staging pending — parallel AlignmentRecruitment + BattleLoadDiagnostics work is in the tree). 24 shader unit tests green; Main compiles clean.

**Root-cause pattern (engine-coupling seam):** the pure decider value-logic was correctly extracted and unit-tested (and the review confirmed no regression of the 2026-05-04 initial-zero-latch RCA), but all four real bugs live where the pure logic meets the engine — *when* the clock starts vs when rendering begins, *which* item an async callback belongs to, *what* state teardown leaves behind, *whether* a scene is actually loadable. These are exactly the ADR-008 in-game-only surfaces the unit tests cannot reach; the highest-value review for an engine-orchestration feature is the lifecycle-seam trace, not the pure-core tests. Lessons: (1) for a polling decider, verify the elapsed-clock SOURCE, not just the count transitions; (2) an engine-constructed object that fires a callback to a reused orchestrator must tag each callback with a generation/id. RCA: `docs/reviews/rca-shaderprecompilation-2026-06-17.md`.

---

## Review 58 — re-patch crash fix (#288) (2026-06-18)

A 2-file crash fix surfaced by review 57's shader walk: it crashed entering item 2/9 with a `HarmonyException`. Root cause — `SubModule.OnGameInitializationFinished` applies all ~26 patch categories on EVERY game init with no guard; Harmony patches are process-global, so the 2nd game init re-applied everything and the non-idempotent `DeliverOffSpring` transpiler, chained twice, couldn't find its already-NOPped `Debug.SilentAssert` anchor and threw. **General latent bug** (any 2nd game/custom-battle in one session), not shader-specific. Fix = (1) a `_gameInitPatchesApplied` once-per-process guard (mirrors `_missionTimePatchesApplied`); (2) the transpiler soft-fails (returns unmodified IL) instead of throwing (mirrors `RefreshCharacterEntityAuxPatch`). Reviewed via `/deep-review` (5 agents) + Codex `gpt-5.5 xhigh`.

**Deep-review: READY (1 LOW fixed).** Agent 5 (data-flow) classified every statement in `OnGameInitializationFinished` as process-global patch-wiring (the `game` param is never used in the guarded body; per-game `AddBehavior`/`AddModel` live in `OnGameStart`; the watchdog is a process-lifetime singleton with its own `if(_timer!=null)return`). Agent 2 re-verified against installed v1.4.6 that `DeliverOffSpring(Hero,Hero,bool)` + the `Debug.SilentAssert`/`get_Race` IL anchors are still present, so the happy path is unaffected. Agent 1 caught a LOW (dead `using System;` after the throw-removal) — fixed in-session.

**Codex VERDICT: SHIP — 0 CRITICAL / 0 HIGH / 0 MED / 0 LOW.** All 5 Known Suspects CONFIRMED clean with file:line evidence (suspect 4, PatchShield, "DISPUTED as a ship-blocking regression" — i.e. it agreed no mitigation is required). Codex's value on a clean changeset was breadth: it (a) SWEPT every transpiler in `Hooks/`+`Patches/` and confirmed no other throwing-anchor transpiler remains (the RCA's preventive lesson, verified independently), (b) confirmed `PatchCategory` is only ever called in `SubModule.cs` (no uncovered re-application path), (c) confirmed thread-safety (synchronous `GameLoadingState.OnTick` → no concurrent entry; static-vs-instance matches the existing `_missionTimePatchesApplied` guard), and (d) surfaced a verified-but-out-of-scope nuance: PatchShield's `ProtectedOwnerPrefixes` lists `"TAOM"` but the main gameplay owner is `"com.taom.mod"` (doesn't match the prefix), so gameplay patches can be unpatched on a version-mismatch — likely intentional (protect the shield infra, let incompatible gameplay patches disable), no change made.

**Root-cause pattern:** the same lesson was learned once and not generalized — `RefreshCharacterEntityAuxPatch` was converted from throw-to-soft-fail in Phase 9b #160 ("any lookup throwing ArgumentException at PatchCategory time crashed the mod during OnGameInitializationFinished"), but the sweep never reached `DeliverOffSpring_RaceAssert_Patch`, which then crashed the moment a code path (the shader walk) re-applied its category. Lessons: (1) Harmony patch *application* is process-global, once-per-process — gating per-game-init patch blocks belongs guarded (`_missionTimePatchesApplied` pattern), never run per-game; (2) an IL-mutating transpiler must be idempotent OR its category gated once — a throw-on-missing-anchor transpiler is a latent crash the moment anything re-applies it; when you convert one to soft-fail, SWEEP every sibling of the same shape. Both reviews ran BEFORE any commit. RCA: `docs/reviews/rca-repatch-crash-2026-06-18.md`.

---

## Review 59 — SettlementFood (TaomSettlementFoodModel) (2026-06-18)

New feature: a `DefaultSettlementFoodModel` override fixing the Troop-Weight garrison food leak (the food model read the Patch17-weighted `NumberOfAllMembers` for the garrison term → elite garrisons ate 2–3× intended; the override adds back `(weighted − raw)/divisor` so the term uses the raw body count) + vanilla food constants exposed as MCM/JSON-tunable knobs. Thin model → pure `SettlementFoodService` (delta math) → `TownFoodSnapshot` boundary; validated config provider.

**Deep-review (5 agents): PASS / 0 findings.** Standards clean (override body = constant-ternaries + boundary-convert-then-delegate). Compat 12/12 verified against installed DLLs (noted installed game is v1.4.6 vs branch's stated 1.4.5 — pre-existing drift, types stable). Data-flow traced 7 flows, 0 gaps — including the two load-bearing ones (garrison divisor identical in the model's overridden constant AND the service correction; every service term is a delta vs base → no double-count). Efficiency: no HIGH; 1 MED + 2 LOW consciously declined per `simplicity-criterion.md` (per-*day* path; the `Enabled` 5×-singleton-read can't be cached without breaking the live MCM toggle).

**Codex (gpt-5.5 xhigh): 0 CRITICAL / 0 HIGH / 0 MED / 1 LOW.** All 6 Known Suspects CONFIRMED clean with file:line evidence; Codex independently hand-computed the worked scenario TWO ways (base-plus-delta vs intended-absolute) → same +2.333, proving no double-count, and scanned `troop_weights.xml` confirming 0 weights < 1.0 (so the `weighted > raw` guard can't under-correct). The lone LOW was a doc/contract inaccuracy, not a logic bug: the `SettlementFoodConfig` summary comment said "Production knobs ADD food", but `TownBaseFood`/`CastleBaseFood`/`VillageFoodMultiplier` are absolute REPLACEMENT values (default = vanilla) — `townBaseFood=0` passes `[0,10000]`, then the service applies `0 − 15` (a reduction). Fixed (Codex's option b): corrected the comment + feature-doc to state replacement-vs-additive semantics (knobs tune both directions, consistent with the divisors; only `flatFoodBonus` is purely additive), + regression test `ComputeFoodDelta_BelowVanillaTownBaseFood_ProducesNegativeDelta` (28 tests green). No production-logic change — the behavior was correct, the prose was not.

**Process:** both reviews ran BEFORE any commit (feature uncommitted). Reference doc `docs/reference/engine/settlement-economy-food-prosperity.md` + feature doc + INDEX + CHANGELOG written. RCA: `docs/reviews/rca-settlement-food-2026-06-18.md`. CLAUDE.md table row + GitHub issue pending user OK (config-protection hook blocked CLAUDE.md). Codex prompt + raw output: `docs/reviews/codex-adversarial-settlement-food-2026-06-18.{prompt.md,md}`.

---

## Review 62 — NavalTravel (TaomPartyNavigationModel) (2026-06-24)

New feature: unlocks Bannerlord's base-engine naval-travel system (campaign-map water pathing, embark/disembark, native party-as-ship rendering) for everyone WITHOUT the paid Naval DLC, by overriding `DefaultPartyNavigationModel`. The override is a faithful port of the official NavalDLC's internal `NavalPartyNavigationModel` (same naval terrain rules, `0.5` embark threshold, naval-aware `CanPlayerNavigateToPosition`) with the **single change** that the ship-ownership capability gate becomes a TAOM config/MCM gate. Thin model → pure `INavalTravelService` → MCM-over-JSON settings → validated config provider. No Harmony patch (the engine's `NavigationHelper.CanPlayerNavigateToPosition` routes through the model).

**Deep-review (5 agents, then a focused 3-agent re-pass after fixes): clean.** Standards PASS (override body = boundary-extract-then-delegate). Compat 21/21 verified against installed v1.4.6 (incl. the `GetPathDistanceBetweenAIFaces` `out float` 7th-arg signature). Data-flow 7 traced / 0 gaps — but it traced TAOM-internal flow and reasoned the ungated terrain methods "harmless," missing the engine-internal cross-party propagation. Efficiency: 2 micro-opts (O(n) terrain scan → ctor `HashSet`; `new int[0]` → `Array.Empty`), both fixed.

**Codex (gpt-5.5 xhigh): 0 CRITICAL / 1 HIGH / 1 MED / 0 LOW.** 4 of 6 Known Suspects DISPUTED with file:line evidence (port fidelity + arg/cost order, GameModel replacement via end-of-list `GetGameModel` scan, terrain-set faithfulness, snapshot-vs-live config). **HIGH (highest-value, all deep-review agents missed across both passes):** `HasNavalNavigationCapability` keyed only on `IsMainParty`; the engine force-propagates `IsCurrentlyAtSea` down the army attachment tree (`MobileParty.cs:493-496`) and recomputes `NavigationCapability` per party (`:464-479`), so with `ApplyToAi=false` a player-led army's attached AI parties are dragged to sea with `Default`-only nav → stranded/desync. Codex DECOMPILED `MobileParty` to prove the propagation rather than assert it. **MED:** live-disabling mid-voyage soft-locked an at-sea party. Both fixed via the unified pure `INavalTravelService.HasNavalCapability(isMain, isAtSea, attachedLeaderCanSail)` — already-at-sea ⇒ keep capability (reach land); else gates govern embark-from-land; else inherit army leader capability — pinned by a 9-cell matrix test (suite 33 → 42, all green).

**Process:** both reviews ran BEFORE any commit (feature uncommitted). Feature doc + CHANGELOG + GitHub issue #296 + CLAUDE.md→v1.4.6 done. RCA: `docs/reviews/rca-navaltravel-2026-06-24.md`; memory `feedback_gamemodel_capability_engine_propagation`. Codex prompt + raw output: `docs/reviews/codex-adversarial-navaltravel-2026-06-24.{prompt.md,md}`.

---

## Review 65 — CustomBattle curated commander lists (2026-06-27)

New feature: a data-driven `custom_battle/custom_battle_commanders.json` maps each faction (culture StringId; Rohan = `vlandia`) to an ordered curated lord list. `CustomBattleService.GetCommanderIdsForFaction` branches on `ICustomBattleCommandersProvider.HasCuratedEntry` and returns the exact list, bypassing the 2-segment-id regex, the `takeMax` cap, and the culture filter; unconfigured factions keep the default top-3 alphabetical. Validating `Lazy` singleton provider; master `GetCommanderIds()` left regex-filtered. Also reassigned 3 lesser Nazgûl (`lord_1_48_1/2/3`) `dolguldur`→`mordor`.

**Deep-review (5 agents): clean** — Standards PASS, Compat all-verified (no new API surface), Efficiency 0 HIGH (UI-path micro-opts only, mostly pre-existing), Completeness COMPLETE, Data-flow 6 traced / 0 gaps (all 43 ids verified to resolve).

**Codex (gpt-5.5 xhigh): 0 CRITICAL / 1 HIGH / 0 MED / 1 LOW.** DISPUTED 5 of 6 Known Suspects with decompiled evidence; verified all 43 shipped ids. **HIGH (all 5 deep-review agents missed):** a curated faction whose ids all fail to resolve leaves the dropdown on the vanilla global unfiltered list instead of falling back to the faction's real lords — the fail-safe was load-layer only, not runtime-resolvability. Fixed: service filters curated ids by character existence + falls through to default when none survive; +2 fallback tests +2 shipped-data regression tests (61→65 CustomBattles tests, all green). **LOW:** doc "No external configuration files" drift — fixed.

**Process:** the feature was committed (`656daae8`) then reviewed at the user's request; fixes land in a follow-up commit. Issue #302. RCA: `docs/reviews/rca-custom-battle-lords-2026-06-27.md`. Codex prompt + raw output: `docs/reviews/codex-adversarial-custom-battle-lords-2026-06-27.{prompt.md,md}`. Lesson: a "fall back to default" fail-safe must cover runtime-unresolvable inputs, not just load-invalid ones.

---

## Review 66 — AlignmentDesertion (2026-06-27)

New feature: troops whose CULTURE alignment (Free vs Evil) is opposed to their lord's KINGDOM alignment desert daily from mobile parties + garrisons (50%/day per type, min 1; MCM-gated by player/AI and parties/garrisons). Pure `AlignmentDesertionService` + thin `DailyTickPartyEvent`/`DailyTickSettlementEvent` behavior; reuses Execution `IAlignmentService` via a new `GetCultureSide`; added `gondor`=free / `mordor`=evil culture keys to `alignment.json`.

**Deep-review (5 agents + adversarial verify): 0 HIGH / 0 MED / 3 LOW.** 8 findings raised, 5 refuted as NOT_A_BUG on re-read. Confirmed (all LOW): missing `AlignmentDesertionConfigProviderTests` (fixed in-session, 12 tests), localization gap (English-only, engine falls back), pending issue number (intended pre-close state).

**Codex (gpt-5.5 xhigh): 0 CRITICAL / 0 HIGH / 1 MEDIUM / 2 LOW** — all 3 confirmed + fixed. **MEDIUM (all 5 deep-review agents missed):** `OnDailyTickParty` gated only on `LeaderHero?.Clan?.Kingdom` — `DailyTickPartyEvent` fires for ALL mobile parties, so companion-led player/AI caravans (clan + kingdom present) were processed, contradicting the "lords' field parties" intent + risking garrison double-processing. Fixed: gate on `IsLordParty || IsMainParty`. LOWs: stale "mercenaries exempt" comment (they keep `Kingdom`=employer and DO purge — comment corrected); rate 0 shed 1 via the min-1 floor (added `rate <= 0` guard + pinning test). Codex proved the catch by decompiling `CaravanPartyComponent.Leader`; it verified the `AddToCounts` removal + periodic-ticker lifecycle correct.

**Process:** feature committed (`ac20b2d7`) at the user's request, then deep-review + Codex review; fixes land in a follow-up commit. Build green; `--filter AlignmentDesertion|AlignmentServiceTests` → 82 passed. RCA: `docs/reviews/rca-alignment-desertion-2026-06-27.md`. Codex prompt + raw output: `docs/reviews/codex-adversarial-alignmentdesertion-2026-06-27.{prompt.md,md}`. Lesson: a `DailyTickPartyEvent` handler that means "lords + player" must gate on `IsLordParty || IsMainParty`, not a "hero-led clan with a kingdom" proxy.

---

## Review 67 — Patch55 race allow-list (2026-07-02)

User report: an uruk save previewed as a bald human on the Load Game screen. Root cause was TAOM's own Patch55 coercing EVERY custom race to human (dwarf had proven the #295 morph AV; no other race was tested). An instrumented pass-through build proved the agentless native build renders uruk fine, so `BasicTableauRaceGuard` was refactored from the hardcoded int set (`{0}`) to a name-based per-race empirical allow-list (`TableauSafeRaceNames = {"uruk"}`) resolved per call via `IRaceManager` (validate-before-lookup, throw fail-safe → human). Committed `4697ada5`, issue #316.

**Deep-review (5 agents): clean — 0 findings.** Standards PASS; Compat 8/8 verified against installed v1.4.6 (incl. sole-instantiation-site sweep of every game DLL); Efficiency 0; Completeness COMPLETE; Data-flow 6 traced / 0 gaps (confirmed Mordor CC race = exactly `"uruk"`; `uruk_hai`/`pale_uruk`/`dg_uruk` are distinct ids that correctly stay coerced; no pre-`OnLoadCommonFinished` RaceManager call path exists). Agent 2's process note (the `____race` field injection isn't covered by generic target resolution) was implemented in-session as `Patch55BasicTableauRaceGuardBindingTests` (Patch58 precedent).

**Codex (gpt-5.5 xhigh): 0 P1 / 0 P2 / 0 P3 — VERDICT CLEAN.** All 6 Known Suspects DISPUTED with decompiled installed-DLL evidence (cross-session race-index drift = vanilla-equivalent residual the guard cannot detect — the visual code carries only a format version, no race-table fingerprint; single-sample verification = documented by-design residual, narrowed by its own 10-entry skins.xml audit showing shared skeleton/head mesh across gender/maturity; init-latch unreachable today; catch-all justified since Harmony doesn't swallow prefix exceptions; void-Prefix + ref-field-injection semantics confirmed; mock-reality divergence none). Codex sandbox couldn't run dotnet tests (env restriction, not a failure) — local suite 3726 green.

**Process:** review ran pre-commit (deep-review) + post-commit (Codex, dispatched pre-commit, returned after the user-requested push); no fixes needed, no RCA (zero confirmed findings). Codex prompt + raw output: `docs/reviews/codex-adversarial-patch55-race-allowlist-2026-07-02.{prompt.md,md}`. Lesson (process, positive): the per-race empirical verification recipe (temp pass-through → render test → name-based allow-list) converts a wholesale crash-guard tradeoff into an incrementally reversible one.

---

## Review 68 — SettlementEconomy town-gold regen (2026-07-02)

User report: towns drain to 0 gold and never recover. Root cause (verified installed 1.4.6): vanilla regen constants (`0.25 × (10000 + P×12 − gold)` daily) vs TAOM's ~2× drains (~2.2× computed LOTRLOME loot values + 22% more villager deliveries). New feature `Main/Features/SettlementEconomy/`: thin `TaomSettlementEconomyModel` overriding ONLY `GetTownGoldChange` → pure `SettlementEconomyService` (banker's-rounding parity) → validated JSON config (shipped base **25000**, slope/rate vanilla) + MCM toggle. 29 tests. Data companions: prosperity analyzer + lift-only quantile rebaseline for TAOM_Map (dry-run validated; `--apply` deferred to user). Follow-ups #318 (LOTRLOME values) + #319 (CultureMarketplace filter defeats the price-crash guard).

**Deep-review (6 agents: 5 core + Step 2c tooling): C# clean across all 5 dimensions; tooling agent found 2 HIGH + 1 MED.** HIGHs: the rebaseline's optional flags broke its idempotency claim (`--preserve`/`--pin-zero-village` left frozen fiefs in the ranking population → per-run rank drift; `--town-uplift` stacked cumulatively). Fixed (frozen fiefs excluded from ranking; uplift applied pre-clamp) and proven per flag combination (0 changes on run 2, all 6 combos). MED: BOM idiom divergence from the tools/README convention — resolved by sanctioning the byte-round-trip alternative. API-compat advisory (null-town guard deferred an NRE to base) also fixed. RCA: `docs/reviews/rca-settlement-economy-2026-07-02.md`; LESSONS-LEARNED "Build, Tooling & Workflow" gained "Test an advertised tool invariant per flag combination".

**Codex (gpt-5.5 xhigh): 0 P1 / 0 P2 / 1 P3 — VERDICT CLEAN.** Rounding parity CONFIRMED bit-identical; castle exclusion verified via all-assembly caller sweep (zero `GetTownGoldChange` references outside TaleWorlds.CampaignSystem); 10-day convergence + toggle-OFF transition hand-computed benign; config cross-reference zero drift. P3 (validation permits self-defeating-but-finite configs, e.g. `rate=0`) = documented design intent; resolved with a feature-doc note, no code change. Codex sandbox couldn't run dotnet tests (MSBuild SDK probe) — local suite 3,755 green.

**Process:** both reviews ran BEFORE any commit; the 2 tooling HIGHs were fixed before Codex dispatch, and Codex was told not to re-report them unless the fixes were wrong (it didn't). Codex prompt + raw output: `docs/reviews/codex-adversarial-settlement-economy-2026-07-02.{prompt.md,md}`. Lesson: an advertised tool invariant (idempotency) must be tested per flag combination — the default-path proof does not transfer to flags that mutate the output outside the core transform.

---

## Review 69 — CombatMechanics (2026-07-02)

New feature (#320): clean-room adaptation of five combat mechanics (spec-doc-only implementation) + two TAOM-original systems (weight-driven charge knockdown from `Monster.Weight` ratios; per-race combat-modifier table). `TaomCombatMechanicsModel` derives from the now-abstract CareerSystem `TaomAgentApplyDamageModel` in the engine's single `AgentApplyDamageModel` slot; 9 thin overrides → 4 pure services + race resolver; 107 tests. Built via 5 parallel builder agents against frozen contracts — a first for a C# feature.

**Deep-review (6 agents: 5 core + spec-conformance): 8 findings, all fixed in-session.** Standards/compat/spec-math PASS. Standouts: per-hit `Substring` allocation in cleave normalization (HIGH — replaced with construction-time variant expansion, which also fixed a cross-service config-semantics divergence); engine-input NaN polarity holes (`momentumRemaining <= 0f` passes NaN — 4th instance of the NaN-gate class, new LESSONS-LEARNED rule "positive-polarity gates on engine floats"); `GetHorseChargePenetration` bypassing the master toggle; MCM slider bypassing the JSON ordering invariant. Root-cause pattern: **parallel-builder seams** — every finding lived at a boundary between independently-authored components; new LESSONS-LEARNED rule "shared sub-problems get ONE prescribed solution in the contract". RCA: `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

**Codex (gpt-5.5 xhigh): 0 P1 / 0 P2 / 2 P3 — VERDICT CLEAN.** All six seeded Known Suspects DISPUTED-as-bugs with decompile evidence: monster-vs-shield fall-through is spec-conformant; shield blocks carry damage into `InflictedDamage` via `ComputeBlowDamageOnShield` (cleave chains through damaging blocks); `BasicCharacterObject.Race` and `IRaceManager` share the FaceGen id space; `ChargeDamageCallback` sets KnockBack on the same `Blow` before the knockdown call; stagger multiplier applies once; MCM-over-JSON matches the AlignmentDesertion precedent. Codex independently re-derived the calibration arithmetic (horse-vs-man Branch B == vanilla verdict; dwarf 118-threshold vs damage 50; mûmakil Branch A at ratio ~125). P3s closed: monster-id lists not resolvability-validated (documented known limitation — typo = inert; an adapter for a diagnostic fails the simplicity criterion) + cleave MCM hint overpromised the zero-shield-damage edge (hint reworded).

**Process:** both reviews ran BEFORE any commit; issue #320 opened before close-out (deep-review completeness agent caught it missing — the plan had deferred it to close-out, against CLAUDE.md's create-before-implementation rule). Codex prompt + extracted review: `docs/reviews/codex-adversarial-combat-mechanics-2026-07-02.{prompt.md,md}` (1.3MB session log discarded, final message kept).

---

## Review 70 — CultureConversion notable replacement (2026-07-03)

User report confirmed: a Mordor-captured Gondor town flips `Settlement.Culture` at conversion but its notables stay Gondorian forever — no TAOM/vanilla path re-cultures a living notable, and vanilla heir replacement COPIES the dead notable's culture. New: `ApplyConversion` now replaces still-alive culture-mismatched notables (town + villages, after the culture flip) via `CultureConversionAdapter.ReplaceNotable` — template pre-check → `CreateNotable` → workshop/alley/caravan transfer → issue cancel → power-zero (heir-spawn suppression) → `ApplyByRemove`. Toggle default on. 9 new tests; suite 3873 green. Issue #325.

**Deep-review (5 agents): 2 process findings, 0 code findings.** Standards PASS; Compat 24/24 verified on installed 1.4.6 (heir gate, `IssueFinalized` ordering, transfer APIs; only interacting patch Patch14 is a no-op for `Lost`); Efficiency 0 HIGH (2 LOW rejected per simplicity criterion); Data-flow 10 traced / 0 gaps (snapshot loop safe vs `_notablesCache` recollection; NaN-power lands on the safe side of the heir gate). Completeness caught: CHANGELOG test count fabricated ("+10" vs actual 9 — evidence-over-claims §C repeat, fixed + LESSONS-LEARNED entry) and the missing GitHub issue (created as #325 before the closing commit). RCA: `docs/reviews/rca-culture-conversion-notables-2026-07-03.md`.

**Codex (gpt-5.5 xhigh): 0 P1 / 0 P2 / 0 P3 — VERDICT CLEAN.** All 6 seeded Known Suspects DISPUTED-as-bugs with decompiled evidence (heir suppression sound — `Power` field-backed, unclamped; `CompleteIssueWithCancel` deterministically nulls `Hero.Issue` before the kill assert; DTO-snapshot loop immune to mid-tick mutation; property-transfer APIs precondition-free and vanilla-identical to the heir path; template pre-check exactly mirrors the engine's null-return condition; no re-entrancy). Residual observation (non-blocking, no fix): `ReplaceNotable` is non-transactional after the replacement spawns — no concrete normal-case throw path exists after the pre-check; accepted residual, noted in the feature doc's in-game verification list.

**Process:** both reviews ran BEFORE any commit; the two deep-review process findings were fixed before Codex dispatch. Codex prompt + extracted review: `docs/reviews/codex-adversarial-culture-conversion-notables-2026-07-03.{prompt.md,md}` (794KB session log discarded, final message kept). No AGENTS.md update — zero false positives, no new miss category.

---

## Review 72 — HeroRace persistence legend (#330) (2026-07-05)

User-identified bug confirmed: `RacePersistenceService` persisted hero races as raw ints — indices into the merged skins.xml race list (`FaceGen.GetRaceNames()` order) — so an insert/remove/reorder between save and load (LOTRLOME skins.xml change, Native-race patch, third-party race mod toggle) silently remapped every hero's race; the `IsValidRaceId` guard is in-range-only and cannot detect a shift. Fix: `CaptureHeroRaces` snapshots the ordered race-name list (`IRaceManager.GetOrderedRaceNames`, new) as a `;`-joined legend under `_taom_raceNameLegend`; restore translates savedInt→name→current id (validate-before-lookup; removed race skips+warns); absent legend = legacy raw-int path byte-for-byte; `SyncRaceData` clears map+legend on `IsLoading` (fixes the same-process stale-map leak, #130-R1 class). +14 tests; suite 4,120 green. TDD RED proven pre-implementation (CS1061 ×4).

**Deep-review (5 agents): 0 code findings, 2 doc items.** Standards/efficiency PASS; Compat 8/8 on installed 1.4.6 (`IsLoading` load-pass semantics, absent-key SyncData leaves ref unchanged, `SyncData<string>` + `Dictionary<string,int>` both registered save types); Data-flow 9 traced / 0 gaps — capture-before-write ordering PROVEN synchronous from decompiled `SaveHandler.SaveTick` (OnBeforeSave drains listeners before the MBSaveLoad write), new-campaign/load-after-load state matrix fully covered, both keys single-owner. Completeness caught a PRE-EXISTING stale doc line (hero-race.md:109 still described the pre-#130 "skips race 0" capture) — fixed in the same session's doc update.

**Codex (gpt-5.5 xhigh): 0 P1 / 0 P2 / 0 P3 — VERDICT CLEAN.** All 6 seeded Known Suspects DISPUTED with decompiled evidence: duplicate-key `_records.Add` throw unreachable (one sync per key per pass); clear-on-load unreachable during save-as-then-continue (save side always constructs `BehaviorSaveData(true)`); legend-path race-0 divergence from legacy is correct name-based behavior, not a regression; degraded-legend capture has no real-game path (FaceGen.CreateInstance runs from native `OnLoadCommonFinished`, long before first `OnBeforeSave`); old builds tolerate the extra unqueried record (no strict-consumption check); the restore-loop refactor checked against concrete (saved, hero, legend, mapping) tuples on both paths. All 8 lifecycle scenarios (shift, mod-removal, pre-#330, pre-TAOM, same-process mixed loads, new campaign, dead heroes, CC round-trip) PASS. Codex sandbox again couldn't run dotnet tests; local suite green.

**Process:** issue #330 opened BEFORE implementation; both reviews ran BEFORE any commit; docs (hero-race.md legend section + stale-line fix) and CHANGELOG landed pre-commit. No AGENTS.md update — zero false positives, no new miss category. Codex prompt + extracted review: `docs/reviews/codex-adversarial-race-persistence-legend-2026-07-05.{prompt.md,md}` (1.9MB session log discarded, final message kept). RCA: `docs/reviews/rca-race-persistence-legend-2026-07-05.md` (0 code findings; doc-drift item recorded).

## Review 74 — BannerBearers (#351) (2026-07-16)

New feature: TAOM formations raise their faction standard, and a bearer keeps its race. Reviewed the third-party "Raise your Banner" mod (v16.1.7, 4,535 lines decompiled) as reference and did **not** port it — the engine already ships `BannerBearerLogic` in every battle and only fails to switch on because `SetFormationBanner` requires a hero captain carrying a banner item, which TAOM's lords never do. Supplying that one call is the whole feature (~400 lines, no Harmony, no art, no new agents). Race safety is inherited: the engine's `UpdateAgent` converts an EXISTING agent in place, so race cannot drift. RYB's clone approach is structurally unfixable (`AgentRace` is MP-only — 0 refs in `Mission.cs`; `AgentData.Race()` sets `GenderOverriden` without a gender; skin comes from `Character.Race` on a shared singleton).

**Deep-review (5 agents): 2 CRITICAL + 2 HIGH + 1 MED + 1 LOW confirmed, 1 CRITICAL disputed.** All fixed in-session. **4 of the 5 agents passed the feature — only Data Flow found anything real.** (1) `CultureBanners` keyed six factions on their LOTR display names; `spcultures.xslt` overrides `<name>` but never `id`, so Rohirrim is `vlandia`, Dunlendings `empire` etc. — six of the highest-volume factions silently flew a generic Gondor standard. A dead `Dictionary<string,string>` key is silent at every layer. (2) Master-toggle leak: disabled, the model returned `0` bearers instead of deferring to vanilla's `1`, SUPPRESSING banners for formations vanilla's own hero-captain path had bannered — worse than vanilla, not equal. Three sibling overrides leaked the same way; all four now `return base.<Method>(...)`. (3) ADR-002: a `for`+`if` loop inside the GameModel — the author had deviated from the approved plan's "fix it in data" and the deviation cost an ADR breach, a nullable question, two perf findings and a toggle leak, all of which dissolved when the data fix was applied. The DISPUTED CRITICAL (nullable contract) was a false positive: `Main/TAOM.csproj:9` NoWarns CS8600-8604/8618/8625 and a clean rebuild emits zero nullable warnings — the agent inferred the rule from a PackageReference without reading `NoWarn`.

**Author-found between reviews (HIGH):** `DefaultBannerItemId` shipped `standard_of_duty_t1`, so every UNMAPPED culture flew a Gondor standard. 38 cultures are registered; 28 were mapped. The other 10 are vanilla leftovers still carrying ~99 live refs in TAOM's own data (`looters`, `sea_raiders`, `forest_bandits`…) — every vanilla-culture bandit warband would have raised the Standard of Duty. Now `""` (fail closed). Found by asking "how many cultures exist?" rather than "are my keys right?" — fixing six wrong keys says nothing about cultures never keyed at all.

**Codex (gpt-5.5 xhigh): 0 CRITICAL / 0 HIGH / 2 MED / 1 LOW — VERDICT SHIP WITH FIXES.** Zero disagreements; every finding verified true on first read. All 6 seeded Known Suspects came back favourable, independently confirming the fixes and the riskiest bet: **S1 the freeze guard is sufficient** (hideout/arena/tournament builders don't even add `BannerBearerLogic`); S2 toggle fold fixed; S3 threshold stable (`Lazy<T>`+Singleton, no reload path); S4 fail-closed default confirmed; S5 agrees `FormationIndex` over `PhysicalClass`; S6 N≤6 engine-supported. **Codex found zero of the bugs the internal review found and two it structurally could not have — both about whether a CHOICE is semantically right, not whether code is correct.** (C1 MED) culture came from `formation.GetFirstUnit()`, which is literally `Arrangement.GetAllUnits()[0]` — an arrangement slot, not a culture owner; a mixed-culture formation (allied army, mercenary-heavy party) flies whichever standard landed in slot 0. Fixed with a majority vote + ordinal tie-break. The API agent had verified `GetFirstUnit()`'s null behaviour without ever questioning its *semantics*. (C2 MED) **MixedFormations' `Patch30` blanket-suppresses vanilla `GetOrderPositionOfUnit` for every unit in a field battle, overriding the engine's banner-bearer slotting** — standards scatter through the ranks. This was a KNOWN UNKNOWN flagged in the plan, the feature doc AND the Data Flow agent's brief, and still went unresolved because tracing it means reading a DIFFERENT feature's patch — cross-feature work per-feature review scopes structurally exclude. Fixed: `if (unit?.Banner != null) return true;` before the IoC resolve. (C3 LOW) `ExcludedRaces` typos fail open — documented at the time and consciously deferred; documenting a gap is not closing it. Fixed: validate on first use where `IRaceManager` is live, warn once.

**What Codex does well (this review):** semantic-choice auditing and cross-feature interaction tracing — the two things a scoped per-feature agent panel cannot do. Zero false positives, likely because the prompt named six real uncertainties instead of asking for a generic sweep.

**Meta-lesson: the answer to the CRITICAL was already in the repo.** `.claude/skills/review-codex/SKILL.md`'s own ID cheatsheet says verbatim *"'rohan' is NOT a valid ID. Rohan uses 'vlandia'"* — phrased as a correction of a prior error. It needed no research, just consulting a cheatsheet that exists precisely for this mistake. It didn't fire because the knowledge lives in a REVIEW skill and the task was AUTHORING a config; `vanilla-data-comparison.md` has the same fact and is `paths:`-scoped away from new feature folders. **The failure is not missing knowledge, it is knowledge with no trigger** — so the fix is the regression test, which fires on every build regardless of what the author knew.

**Process:** issue #351 created (retroactively — a real process miss the Completeness agent correctly flagged as blocking); both reviews ran BEFORE any commit; 61 BannerBearers tests + full suite green (no total quoted — the tree carries an unrelated in-flight feature's untracked tests, so the number moves for reasons unrelated to this changeset); `validate_moduledata.py` PASS; XML parse smoke OK. 7 lessons codified across 5 category files. RCA: `docs/reviews/rca-banner-bearers-2026-07-16.md`. Codex prompt + raw: `docs/reviews/codex-adversarial-bannerbearers-2026-07-16.prompt.md` + `docs/reviews/raw/codex-adversarial-bannerbearers-2026-07-16.md`. In-game verification still owed.

## Review 75 — battle-load blind window + FileLogger durability (#350) (2026-07-16)

Triage of a player CTD (v2.0.12, attacking Deserters at Nan Angren, vanilla scene `battania_village_c`) that could not be root-caused: the `[BattleLoad]` log ended at `MissionOpenNew` and the reporter had no further artifacts. **The crash was deliberately NOT fixed** — the Iron Law forbids a fix without a root cause; what shipped is the forensics that make the next report self-localizing. Four hypotheses were killed first, each of which would have been a plausible wrong fix: stale scene ref (the scene exists in installed SandBox), cave troll (no party in that battle can field one; #346 was cosmetic guard-picking, not a crash), the `h0/a0` zero-troop party (the `Formation.cs` count divisions are **float** — they yield NaN, they cannot throw; `MapEventSide.cs:439` shows vanilla expects 0-member parties), and TroopWeight/`TaomPartySizeModel` (not in the mission-load path). Shipped: `FileLogger` INFO/WARNING/ERROR now drain synchronously (a background writer with a 50 ms idle sleep and `IsBackground` had been dropping the undrained queue on every hard crash — **the instrument systematically lost the lines it exists to capture, which is *why* this log was unlocalizable**), plus 4 stamps splitting `OpenNew`→`Initialize` (Patch43 11→14 hooks, apply now try/catch-guarded). Registry correction: `Mission.Initialize` is **public** (`Mission.cs:1798`), not private as claimed since the feature shipped.

**Deep-review (6 agents — 5 core + 1 hand-rolled concurrency agent): 2 MED confirmed, both in the fix itself, both found ONLY by the 6th agent.** All five core agents read `FileLogger.cs` and passed it. (1) **A regression I introduced:** `Drain()` early-returned on a null writer *before* dequeuing, so post-`Dispose` the queue never empties and `ProcessQueue`'s `!_queue.IsEmpty` loop spins a core at 100% until process exit — the old loop always dequeued (writing via `_logFile?.`) and could not spin. RED-proven against the commit: *"Drain() left 200 item(s) queued after Dispose."* (2) The empty `catch` gave a write fault zero signal — a crash-forensics instrument that looks healthy while silently dropping lines, during exactly the incident it documents. Now counted and self-reported as a `WARNING` on the next successful drain. Agent 2 verified 4/4 new bindings against installed v1.4.7 (incl. the private `MissionState.LoadMission`) and independently confirmed all 4 engine claims; Agent 5 traced 7 flows / 0 gaps; Standards and Completeness PASS.

**Agent disagreement — worth recording because averaging would have been wrong.** Agent 3 (Efficiency) claimed a *"blocking duration if collision: up to 50ms"* game-thread stall; Agent 6 refuted it and **Agent 6 is right**: `Thread.Sleep(50)` sits in `ProcessQueue`, entirely outside `Drain()`'s lock, so a durable write can never block on it. Agent 3 conflated the writer's wake interval with lock-hold time. The CHANGELOG's ~15 ms cost claim survives only because that number was refuted rather than believed. Separately **Agent 4 fabricated a count** (claimed 15 FileLogger tests; the measured run showed 12) — agent-reported numerics are claims, not evidence, which is the standing `evidence-over-claims` §C rule fired at a reviewer instead of an author.

**Meta-lesson: the changeset's NOMINAL risk and its ACTUAL risk were different, and only the nominal one had reviewers.** The Harmony half (4 new bindings, one against a private engine method) was covered from three directions and was genuinely clean. The 20-line lock/liveness rewrite of the logger *every feature depends on* had no rule, no agent, and no prompt asking about it — it got reviewed only because the orchestrator noticed the gap and wrote a 6th agent. The core five are calibrated for TAOM's usual work (Harmony/GameModel/adapter/XML); they correctly passed the part they cover, and 100% of the defects were in the part they don't. The `/deep-review` skill already says the five are a floor, not a ceiling — this is the worked example.

**Process:** issue #350 filed (retroactively as repair — the `[RaidSpawn]` diag comment had referenced an issue that was never created, and this log is its first wild capture; the diag is now marked KEEP until #350 closes). No Codex pass. Suite 4,292 green; BindingVerification green with no Inconclusive. 3 lessons codified in `lessons/build-tooling-workflow.md`. RCA: `docs/reviews/rca-battle-load-blind-window-2026-07-16.md`. Root cause of the CTD remains **unproven** — native-vs-managed needs a reporter's `Logs/` listing (bundle present ⇒ managed ⇒ `/investigate`; absent ⇒ native ⇒ `/native-crash-triage`). In-game smoke still owed.

## Review 76 — Siege guards / field-battle-only gate (#349) (2026-07-16)

Defensive fix, **not** a confirmed root-cause fix. A playtester (engine v1.4.6.115628, TAOM v2.0.12) hard-crashed to desktop in his first siege — native CTD, no managed exception, nothing caught by the crash pipeline — during OrderOfBattle formation distribution ~1s after `BattlePlayable` (`SiegeMissionWithDeployment`, `sturgia_castle_c`, 862-strong dwarf army joined via `join_siege_event`). Log triage put the death inside formation setup, not agent spawn: all 109 troops got body properties first, and the scene loaded clean. Two TAOM formation features had **no mission-type guard**: SmartCavalryAI (`Patch31`) synchronously re-enters native `Formation.SetPositioning` on a player-team *cavalry-classed* formation (dismounted dwarves are still cavalry-classed, and the log shows the attacker army being split into exactly such a `Cavalry` formation), MixedFormations (`Patch30`) overrides unit slots. Both are open-field-only by design. Gated both on `Mission.IsFieldBattle`. +2 tests; suite 4,220 green.

**Deep-review (5 agents): 1 HIGH + 1 MED + 2 LOW confirmed, 1 false positive refuted. Again, only Data Flow found anything real.** (HIGH) **The changeset did not do what it claimed.** The gate went into `CavalryChargeService.HandleChargeOrder`/`.Tick`, but `SmartCavalryAIMissionBehavior.OnMissionTick` also calls `ApplyCollisionAvoidance`, which writes `agent.SetMovementDirection` per mounted unit per frame **bypassing the service entirely** — so with `AvoidFriendlies` (default `true`) the feature kept manipulating cavalry every frame in a siege, which is precisely what the change existed to stop. Fixed by hoisting the gate to the top of the tick (which also skips the per-formation adapter build). (MED) ADR-002: a 4-line comment pushed `MixedFormationsMissionBehavior` 147→151 (cap 150); fixed by condensing my own comment, **not** by refactoring `TryGetTeamAdapters` — that would be scope creep into pre-existing code. (LOW) one test built its `IBattlefieldQueryAdapter` substitute off-helper, so the new `IsFieldBattle` defaulted `false` and it passed for the right reason only by guard ordering. (LOW, **rejected with reason**) Patch31's postfix does pre-gate adapter/scan work — perf-only, ~24 iterations; a fourth redundant gate fails `simplicity-criterion.md`, recorded so it stays a decision rather than an oversight.

**The scope lesson: the changed-file list was the wrong review scope.** The HIGH lived in `SmartCavalryAIMissionBehavior.cs` — **a file not in the diff**. Agents 1 (Standards) and 3 (Efficiency) are explicitly diff-scoped and structurally could not see it; Agent 5 found it only because its brief said "read the whole feature folder and enumerate every path to a native write." Worse, an earlier research pass in the same session *had* surfaced `ApplyCollisionAvoidance` — but only to refute a null-mount NRE theory, so it was filed "not a crash risk" and never re-examined as a *suppression* path. Generalised into `lessons/gamemodels-services.md` → "Gating a feature OFF requires path enumeration, not layer gating", widening the existing master-toggle-fold rule (scoped to GameModel overrides) to behavior-class boundary work.

**Agent 2 (API) earned its slot.** It diffed the load-bearing regions against **both v1.4.7 (dev) and v1.4.6 (the playtester's build)** — byte-for-byte identical, so the gate is safe on the install that actually crashed. It independently proved the live-read design is not optional: `MissionCombatantsLogic.EarlyStart` assigns `MissionTeamAIType`, and `Mission.AfterStart` (:3799-3826) runs **every** `OnBehaviorInitialize` before **any** `EarlyStart` — so caching `IsFieldBattle` at init would read `NoTeamAI` 100% of the time and silently disable both features in every battle. It also CONFIRMED (rather than refuted) the documented limitation: `OpenSiegeMissionNoDeployment` hardcodes `(MissionTeamAITypeEnum)1` = `FieldBattle` (`SandBoxMissions.cs:1582`), so relief-force assaults still run both features — accepted; the crashing mission was `SiegeMissionWithDeployment` → `Siege(2)`, which the gate does suppress.

**False positive (Agent 4): "CHANGELOG MISSING — blocking."** The entry was at line 121; the agent scanned only the newest `##` section. Refuted by grep before acting — `feedback_audit_findings_not_always_correct`'s ~95%-not-100% rate, in practice. Its other two findings (missing GitHub issue; the off-helper mock) were genuine.

**Convergence: Reviews 74 and 76 independently landed fall-through guards on `Patch30` on the same day, for unrelated reasons** — BannerBearers' Codex pass found it scattered banner bearers (C2 MED); this review found it ran in sieges. Same underlying class: **a positioning Prefix that returns `false` is a silent monopoly on an engine decision**, and every consumer needing the vanilla path breaks with no error. The registry's Patch30 section now carries a standing "Fall-through cases (do not remove)" list naming both — add to it rather than rediscovering the class a third time.

**Cross-check with Review 75 — two independent triages killed the same hypothesis on the same day.** The rival lead here was a `cave_troll` garrison agent hitting the documented `AutoGenerated.dll` DivideByZero. This review de-prioritised it from the Armory snapshot (`monsters.xml:974-1080`: `IsHumanoid="true"`, `monster_usage="human"`, `human_skeleton`, **valid** `body_capsule radius="0.37"`; `troll-race.md` records it "confirmed working in battle" — a formation-capable humanoid, not the spider's riderless non-humanoid). Review 75 independently killed the troll for a *different* battle and added the stronger general point: **the `Formation.cs` count divisions are `float` — they yield NaN, they cannot throw.** Together these substantially undercut the "cave troll → DivideByZero" framing that `formations-and-team-ai.md:82-90` still carries as the spider lead; both findings are now noted there so a third session doesn't re-derive them.

**Process:** issue #349 created (retroactively — the Completeness agent correctly flagged it blocking); deep-review ran BEFORE the commit; **no Codex pass** (not requested — notable, given Review 74's C2 was exactly the cross-feature class Codex catches and per-feature agent panels don't). RCA: `docs/reviews/rca-siege-guards-2026-07-16.md`. Commit `0a372849`. **Root cause still unconfirmed** — SmartCavalryAI ships OFF by default, so it only fired if the player opted in. The decisive artifact is the player's Event Log fault module/offset (`AutoGenerated.dll` ⇒ geometry; `0xC0000005` near `SetPositioning` ⇒ SmartCavalryAI; neither ⇒ likely vanilla siege instability at 862 agents), plus the `Erebor 11` save and a "did you enable Smart Cavalry AI?" answer. In-game siege smoke still owed.

## Review 77 — Education crash-fix (#354) deep-review + Codex adversarial pass (2026-07-21)

Data fix for the age-8 child-education CTD (lothlorien + umbar/goblin/mistymountainorcs missing
stage_2 tutor templates; engine null-derefs the lookup) + `MISSING_EDUCATION_TEMPLATES` validator
rule + PatchShield original-exception rethrow. **6 Claude agents (5 core + tooling): 0 confirmed
findings. Codex (gpt-5.5, xhigh): CLEAN — 0 findings at every severity, all 6 Known Suspects
resolved with evidence.** Rare full-clean double review; attributed to the fix being clone-from-
verified-precedent data plus two small, heavily pre-verified code changes (the engine contract was
decompile-confirmed BEFORE authoring, per `/investigate` discipline).

Notable verifications rather than findings: (1) Harmony 2.4.2 `MethodCreator` emits `Ldloc; Throw`
for any exception-returning finalizer — never stack-preserving `Rethrow` — proving the old
PatchShield unwrap+rethrow destroyed real stacks (the reason bundle `94c7b795` blamed
`ViewModel.ExecuteCommand`); (2) the validator's stage_2/page_0/branch-0-5 contract is provably
exact against `CreateStage8` (6 options, `_keyIndex` 0-based); (3) template leakage disputed —
engine lord/notable creation draws from `CultureObject.LordTemplates`/`NotableTemplates`, never a
global occupation scan; (4) raider-culture children unreachable (education iterates
`Clan.PlayerClan.Heroes` only) — noted on #354 as a future-authoring caveat. False-positive note:
the Completeness agent mis-listed pre-existing PatchShield code (owner prefixes) as part of the
diff — refuted by `git diff` before acting.

**Process:** issue #354 created BEFORE implementation; RCA
`docs/reviews/rca-education-crash-fix-2026-07-21.md`; lesson appended to
`lessons/data-content-cultures.md`; `kingdom-creation.md` File 8 now carries the enforcement note.
Suite 4,386 green + validator PASS + 30/30 tool tests. In-game education-screen smoke owed (user).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)

<!-- backlinks-end -->
