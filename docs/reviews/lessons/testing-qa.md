# Lessons — Testing & QA

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Testing & QA lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### If you didn't watch the test fail, mutate the code until it does
TDD's RED step is not ceremony — it is the only evidence that a test *can* fail, and therefore that a green run means anything. Writing the tests first but running them for the first time *after* the implementation lands (the natural rhythm when batching file writes) skips it silently: you get "48 passed" and no idea whether any of those 48 would notice the feature being deleted. A test that cannot fail is indistinguishable from a test that passes.
- **Why missed:** PrisonerRecruitment (2026-07-16). Tests were authored before the implementation — satisfying the letter of test-first — but both were written before anything was executed, so RED was never observed. The suite went green on the first run and the gap was invisible; it surfaced only because the author flagged it against their own work.
- **Prevent:** run the suite once before the implementation exists, even if it just fails to compile. If that ordering has already been lost, recover the evidence with **mutation testing**: disable each load-bearing rule in turn (`if (false && …)`) and confirm the expected tests fail, and that the COUNT matches what that rule owns. On PrisonerRecruitment: disabling the same-culture rule failed exactly 3 tests, removing the Neutral guard exactly 2 — that, not the green run, is what proved the tests real. Cheap (two edits + two filtered runs) and it also pins which test owns which behavior. Corollary for derived/scanning tests: add a floor assertion on the scan count (`Assert.IsTrue(found >= 8)`) so a regex matching nothing fails instead of vacuously passing every row.
- **Source:** docs/reviews/rca-prisoner-recruitment-2026-07-16.md finding 4

### A warm-cache pass is not proof the cold path works — validate render/compile/deployment fixes cold
A shader-precompile / render / battle-load fix that "completes fine in-game" may be riding a WARM cache that short-circuits the very path the fix targets. The ShaderPrecompilation 1.4.7 fix (#336) completed the all-characters battle in 20s on a warm shader cache — so fast it settled *before* the deployment phase could matter, meaning the force-finish path (the item's specific hang fix) never actually fired. A cold run (the prior attempt) had hung on that same item. A fast green run nearly read as full validation; only the per-item log detail (no seed line, no force-finish line, 20s vs the earlier multi-hour hang) exposed that the targeted path was skipped.
- **Why missed:** "it completed, all items green" is a strong success signal and easy to over-trust. The structural fixes (NRE guard) ARE proven regardless of cache (they fire on the first tick); but the *duration-dependent* path (deployment-view hang) only manifests when the item takes long enough to reach it — which a warm cache prevents.
- **Prevent:** for a fix whose trigger is a slow/cold operation (shader compile, cold scene load, deployment wait), confirm the fix's own log signature actually fired (`force-finished deployment`, `seeded …`), not just that the walk completed. If it didn't fire, the path is unexercised — clear the cache (delete the shader `.sack` / cache dir) and re-run to force cold, or state explicitly that the path remains unvalidated.
- **Source:** docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md (#336; warm-cache masked the force-finish path)

### Write engine-float decision gates as positive requirements — NaN must FAIL the gate
`FiniteFloatValidator` on config floats does NOT protect runtime ENGINE inputs (momentum, velocity, resistance handed to a GameModel per hit). An inverted early-exit guard like `if (momentumRemaining <= 0f) return false;` lets NaN through — every NaN comparison is false — so garbage input takes the ACTIVE branch. Write the gate as the positive requirement (`!(momentumRemaining > 0f)` / `momentumRemaining > 0f` required to proceed), or explicitly `float.IsNaN(...) → fall through to base`. For owned-verdict services (bool? patterns), NaN inputs should return null (defer to vanilla), never an owned true/false computed from garbage. Add a NaN unit test per gate.
- **Why missed:** 4th instance of the NaN-gate class (Career cooldown #31, EditorCacheRebuild #38, CS_Road 2026-05-13, CombatMechanics 2026-07-02). The rule lived in "Config Providers MUST Validate" — scoped to config/MCM/editor fields, all of which WERE covered; the engine-runtime-input side had no rule, so `ShouldForceSliceThrough`'s `<= 0f` guard and `ChargeKnockdownService`'s NaN-to-owned-false both shipped to review. Caught only because the spec-conformance agent's prompt carried an explicit "NaN polarity on every gate" audit criterion.
- **Prevent:** when writing any per-hit/per-tick decision on an engine-sourced float, check the comparison's polarity against NaN before moving on; include the NaN-polarity audit line in review prompts for math-heavy features.
- **Source:** docs/reviews/rca-combat-mechanics-2026-07-02.md (findings 3+4).

### Changing a weighted recruitment pool breaks tests that stub `_random.Next(<hardcoded total>)`
`VolunteerRecruitmentService.PickWeighted` rolls `_random.Next(totalWeight)` where `totalWeight` is the SUM of the pool's weights. Tests stub a specific `_random.Next(N).Returns(roll)` for the pool's *current* total N. Add/remove a troop or change a weight and N changes; the stub no longer matches, NSubstitute returns the default `0`, and `PickWeighted` returns the FIRST troop — so every "top bucket / max roll" test silently fails by returning the lowest troop.
- **Why missed:** surfaced TWICE in one session. (1) Adding `harad_mumakil_rider` to `clan_aserai_1` (total 11→12) broke `GetVolunteerTroopId_ClanAserai1_TopBucket_RollsElephantRider` (stubbed `Next(11)`) → it returned `harad_levy`. (2) A *committed* `// TEMP-SPIDER-TEST: weight 1 -> 40` (`VolunteerRecruitmentService.cs:617/656`, comment literally said "REVERT before commit") had already bumped the Dol Guldur pool total, leaving 9 Dol Guldur `*_MaxRoll_*` tests red on the branch (all returning `dg_goblin_slave`, the first troop) — a pre-existing red suite that masks whether new work regressed.
- **Prevent:** when you edit any `AddClan(...)` / `CultureMap[...]` pool, grep the test file for `Next(` against that pool and update the total + cumulative breaks (prefer tests that derive the total from the pool, not hardcode it). Never commit a `TEMP-*` debug weight — grep for `TEMP-` before a recruitment commit. A red baseline suite hides regressions: if a test was already failing at HEAD, prove the new change didn't *change which* tests fail (stash the change, diff the failure set) before calling it "pre-existing."
- **Source:** docs/reviews/rca-mumakil-2026-06-29.md (Mûmakil Phase 1).

### Mock the lowest-level inputs, never the derived predicate your code depends on
CaravanTrade's `AllowWartimeTrade` called `IAlignmentService.AreEnemyAlignments(a,b)`; its tests stubbed `_alignment.AreEnemyAlignments("gondor","rohan").Returns(false)`. That validated the *assumed* contract, not the shipped implementation — which inverts Neutral (treats it as everyone's enemy). The suite was green while the feature's default policy silently blocked all Neutral-faction trade.
- **Why missed:** mocking the exact predicate under test turns the test into a tautology ("if the service returns X, my code does Y") that can never catch a wrong X. The real logic (`GetKingdomSide` → Free/Evil/Neutral branch) never ran.
- **Prevent:** mock the *inputs* one level below the decision you're testing (here `GetKingdomSide` returning `FactionSide.Neutral/Free/Evil`), so the real branching logic executes. Reserve mocking a high-level predicate for when that predicate is genuinely an external boundary you don't own. Regression cases to always include for an alignment-gated feature: same-side, opposite-side, Neutral-on-each-side. Sibling: Adapters & TaleWorlds API "Before reusing a shared TAOM decision service…".
- **Source:** docs/reviews/rca-caravan-trade-2026-07-04.md

### Never silently defer a HIGH/P1 review finding
When `/deep-review` or `/codex:review` flags a finding HIGH (or P1), the only allowed responses are: (1) fix it before commit, (2) open a GitHub issue tracking the known defect and add a commit trailer `Deferred: <issue-link> — <one-line reason>`, or (3) record a "Known limitation:" bullet in the CHANGELOG. Silently dismissing a HIGH finding on technical grounds without recording the decision anywhere is forbidden. When extending a system from single-source to multi-source, re-validate every "there can only be one" assumption — what was safe for 1 writer may not be safe for N.
- **Why missed:** In the Career AoE extension session, deep-review Agent 5 (Data Flow) flagged an ally-buff overwrite as HIGH and Codex independently rated the same bug P2 — two reviewers, two signals, both reasoned away as "only matters when companions get abilities." The bug was actually present in single-player too (any two archetype activations on the same troop).
- **Prevent:** Default is fix. If tempted to dismiss with "only matters when X", write down X and verify it is TRULY the only path before deferring — and if you defer, document it (issue + trailer or CHANGELOG bullet).
- **Source:** memory/feedback_dont_defer_high_review_findings.md

### Run `/deep-review` + `/review-codex` BEFORE the closing commit, not after
For any C# feature change touching ≥2 files (or any feature module), run `/verify` → `/deep-review` → fix → `/review-codex` → fix → RCA **before** the closing commit + push. The mandatory completion workflow in CLAUDE.md is a hard gate, not a risk-proportionate suggestion. "Additive + fully tested + validator-clean" is NOT a license to commit first and review after. Open the GitHub issue when STARTING the work (so it exists before the commit); the pre-commit hook enforces CHANGELOG, not issue creation.
- **Why missed:** 2026-06-07, cultural-feats Wave 1 (24 additive feats, 3091/0/2 tests, validator-clean) was committed + pushed (bf9226f, ce07ebe) before either review ran — the low perceived risk implicitly justified the skip. The user caught it by asking "did we do a deep review and codex review?". The retroactive reviews found a MED: production feat metadata (EffectBonus sign, IsPositive, AdditionType) for the 24 feats was unpinned by any test — a flipped `+0.15f` vs `-0.15f` on a cost-reduction feat would invert the feat and pass every existing test. Proof that "additive + tested" can still carry a test-invisible correctness bug. Repeat offender (`rca-crash-report-2026-05-25.md`).
- **Prevent:** Treat the thought "this is additive/trivial/well-tested, I'll just commit and review after" as the exact trigger to stop and run the reviews first.
- **Source:** memory/feedback_review_before_commit_not_after.md, docs/reviews/rca-cultural-feats-wave1-2026-06-07.md, docs/reviews/rca-crash-report-2026-05-25.md

### A test must be able to fail — ban vacuous assertions
A test that cannot fail is worse than no test: it reads as coverage while proving nothing. Before a test ships, ask "what change would make this assertion fail?" — if the answer is "nothing," it's vacuous. Banned patterns: `assertTrue(x or True)` / `assertTrue(x or 1)` (unconditionally true regardless of `x`); `assert value != <one of many>` when the claim is `value == <specific>`; an assertion that holds in EVERY branch the test can take; and "the path is exercised" comments on code paths that aren't actually reached. Every assertion must name the SPECIFIC expected value (`== "HIGH"`, `== "perm-star-wildcard"`), not a weak inequality, when the spec is specific.
- **Why missed:** All three patterns shipped in the SkillSpector test suite and were caught by `/deep-review` (2026-06-22). `test_audit_allow_suppresses_a_line` shipped `assertTrue(res.suppressed or True)` plus a comment claiming a suppression path the production code didn't implement — a vacuous assertion masking a real impl gap (rated HIGH). `test_external_fires_full_severity` asserted `severity != "INFO"` — would pass if every HIGH rule silently degraded to MED; the claim was "full severity," so it must anchor to `any(severity == "HIGH")`. `test_empty_file_list_does_not_crash` asserted only "no CRITICAL" — trivially true whether yara was present (empty findings) or absent (one INFO).
- **Prevent:** Delete `or True` / `or 1` guards on sight. When a test branches on environment (dep present/absent), assert the exact rule + severity in EACH branch. Pair with TDD — a test you can't make fail by breaking the code isn't testing the code.
- **Source:** memory/feedback_no_vacuous_test_assertions.md, docs/reviews/rca-skillspector-2026-06-22.md

### Test every populated `(case, branch-condition)` cell of a dispatcher, not just one axis
When a service method dispatches by switching on a value (enum, culture id, kingdom id, occupation) and each arm contains a cascade of per-condition `HasFeat` / `TryGet` / lookup checks, enumerate every populated `(case, branch-condition)` cell BEFORE writing tests and write exactly one dispatch test per cell. Per-axis coverage ("tests exist for each occupation" AND "tests exist for each culture") is necessary but not sufficient — the dispatcher is a cross-product. Name BOTH axes in the test name: `Method_IsengardArtisan_AddsOne`, not `Method_Artisan_AddsOne` or `Method_Isengard_BranchTaken`.
- **Why missed:** Codex review 2026-05-31, cultural-feats per-occupation refactor. `CulturalFeatsService.ApplyNotableCountFeat` switches on `NotableOccupationKind` (Merchant/Artisan/GangLeader/RuralNotable/Headman/Other) with per-culture `if (culture.HasFeat(X))` checks inside each arm. Tests existed for each occupation axis and each culture axis, but the `(Dol Guldur × Artisan)` cell — a real `if (culture.HasFeat(DolGuldurNotableCountTownArtisanFeat))` branch — had no dedicated test. Codex caught it (HIGH); all 5 deep-review agents missed it because each verified one axis at a time.
- **Prevent:** Before writing dispatcher tests, list every populated `(case, branch-condition)` cell and write one test per cell. The Completeness agent's "tests exist for each enum value" and the Data Flow agent's "is the branch reachable?" are each necessary-not-sufficient; per-cell coverage is the contract. Sibling: `.claude/rules/tests.md` "Skip-Guard Exhaustion".
- **Source:** memory/feedback_per_branch_dispatch_test_enumeration.md, docs/reviews/rca-cultural-feats-per-occupation-2026-05-31.md

### A copied config provider inherits the code but not the tests — add `*ConfigProviderTests` in the same PR
Every `*ConfigProvider` with a `Validate(...)` method must ship a `*ConfigProviderTests` in the same PR, one test per validation rule (each semantically-invalid-but-parseable value reverts to default + warns) plus missing-file / malformed-JSON / empty-object / cache cases. Mirror `RecruitmentAlignmentConfigProviderTests`. The "Config Providers MUST Validate" architecture rule's *Test requirement* applies to the provider, not the service.
- **Why missed:** AlignmentDesertion deep-review (2026-06-27). TDD drove the *service* (RED→GREEN, 18 tests) and the config provider was copied wholesale from `RecruitmentAlignmentConfigProvider` — so it "looked done" and its dedicated test class was skipped. The Completeness agent verified "service tests exist" and passed the feature; only the Data Flow agent caught the missing `AlignmentDesertionConfigProviderTests` (the `Rate` validator — above-1/below-0/NaN/Infinity — had zero coverage). Validation code was correct; a future refactor could have silently broken it with no failing test. Fixed in-session (12 tests added).
- **Prevent:** When you copy a config provider from a sibling, copy its test class too in the same edit. A `Validate` method with no `*ConfigProviderTests` is an automatic Completeness-agent finding — enumerate from the provider, not from "are there tests for this feature."
- **Source:** docs/reviews/rca-alignment-desertion-2026-06-27.md

### Treat audit/review findings as hypotheses — verify before applying
Audit findings (Phase 1-8 produced 134+) are HYPOTHESES, not verified fixes; net audit-quality rate is ~95%, not 100% (~46 of 79 correct-as-written, ~12 needed deferral, ~1 was actively wrong). Before applying any audit-recommended fix involving object lifetime / reference semantics (e.g. hoist-out-of-loop allocation), threading / lock placement, API substitution (`Remove*` vs `Clear*`), or vanilla Harmony Prefix-skip safety gates, verify the underlying assumption by tracing the actual call path or decompiling the relevant TaleWorlds method.
- **Why missed:** Phase 9b #169 (Custom Widgets allocation hoist, 2026-05-14). Audit recommended hoisting a per-iteration `SimpleMaterial` allocation out of `PolygonWidget.OnRender`'s edge-thickness loop (~17k allocs/sec saved). Autonomous investigation found TaleWorlds' `TwoDimensionDrawData` holds `SimpleMaterial` BY REFERENCE — queued draws read CURRENT values at end-of-frame in `DrawTo`, so sharing one material and mutating per-iter makes every queued draw pick up the last iteration's color/alpha → visual corruption. The audit's "obvious" fix would have broken rendering; closed with a `SimpleMaterial`-pool-with-cache-key design instead.
- **Prevent:** For a perf finding that LOOKS obvious ("hoist this allocation, share this object"), trace the consumer downstream first — reference semantics on engine-side `Data` types aren't visible from the producer side. For "should use X API" findings, verify X exists via `ilspycmd` against the installed DLL (several Codex suggestions cite v1.4 APIs absent in v1.3.15). For threading/lock concerns, check the engine actually fires off-thread (Gauntlet widgets are single-threaded for TAOM patches).
- **Second instance, worse retention window (2026-08-03):** a field report asked to cache the `new Equipment{…}` allocated per call in `CharacterTableauService.UpdateMount` (~17 allocations per inventory-screen open; the reporter classified it "Info"/"Trivial"). Decompiling the installed v1.4.7 `TaleWorlds.MountAndBlade.View.AgentVisuals` refuted it: it stores `private AgentVisualsData _data`, `AgentVisualsData.Equipment(e)` assigns the reference (`EquipmentData = equipment`), and `_data.EquipmentData` is dereferenced across ~30 sites in the class — mesh building, skin generation, and the equipment-diff in its refresh path — for the object's **whole lifetime**, not merely until end-of-frame as in the `TwoDimensionDrawData` case above. `UpdateMount` also deliberately keeps `oldMountVisuals` alive for 3 frames (`_mountVisualLoadingCounter = 3`) for the cross-fade, so a shared cached instance would retroactively mutate a LIVE visual. **REJECTED, deliberately, not overlooked:** per `.claude/rules/simplicity-criterion.md` a tiny win plus aliasing mutable state into engine-retained objects is a Reject, and this subsystem has already produced two CTDs (issue #299 save-list tableau; the 2026-07-31 prone-tableau RCA). Recorded here so a future reviewer does not re-propose it; the allocation is still there on purpose.
- **Source:** memory/feedback_audit_findings_not_always_correct.md, docs/audits/phase-9-completion.md; second instance from the Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing) — no commit, the code was deliberately left unchanged

### Detection rulesets need a positive firing test per rule + every host config format as a coverage axis
When you author or port a pattern-detection ruleset (regex categories, YARA signatures, AST checks), an untested rule only works by luck. Every rule needs at least one positive firing test (a sample input that MUST match) — non-negotiable for HIGH-severity rules — plus a negative test where false-positives are plausible (the rule must be SILENT on a benign sample). The host surface's config FORMAT VARIATIONS are a mandatory coverage axis you must enumerate, not assume: for Claude-config auditing that means BOTH YAML (skill frontmatter) AND JSON (`settings.json`, `.mcp.json`).
- **Why missed:** The SkillSpector port into `tools/audit_claude_config.py` (2026-06-22 deep-review) shipped a security gap: `agency-wildcard-tools` matched the YAML form (`allowed-tools: ["*"]`) but NOT the JSON `settings.json` form (`"tools": ["*"]`) — the leading quote broke the regex — and `scan_permissions` missed a bare `"*"` allow-grant entirely. A real wildcard grant slipped BOTH scanners because only the YAML form was tested. The same review found 26/36 regex rules + 3/8 AST rules had zero firing test (9 untested were HIGH).
- **Prevent:** One positive firing test per rule, minimum. Enumerate the host config's format variations as test cases (single-quote vs double-quote, attribute order, article insertion in prose patterns). Verify by running the ruleset against a synthetic malicious sample via the tool's loud mode (`--external` for the auditor), asserting per-rule firing — not just "it runs." Prefer a false-positive over a false-negative only when the FP is cheap to triage.
- **Source:** memory/feedback_detection_ruleset_per_rule_test_matrix.md, docs/reviews/rca-skillspector-2026-06-22.md

### Write an end-to-end XML→calculator→property→guard smoke test for any config-pipeline feature
When a feature gates on a sign / comparison / non-zero check downstream of a config calculator pipeline (XML → mutation → property → guard), the test surface MUST include an end-to-end smoke test that drives the FULL pipeline with a REAL shipped XML choice (not synthetic fixtures) and asserts the side effect — or its absence — at the end. Per-layer unit tests can ALL pass while the composition is broken at the join. Specifically test the gated value: if the guard is `> 0f`, cover a positive, zero, and (if reachable) negative value from the XML.
- **Why missed:** Codex review pass #48 (`docs/reviews/codex-adversarial-career-102-104-postfix-2026-06-02.output.md`), CareerSystem `CooldownReduction`. XML rewrite ✓, calculator (`flat = baseValue + value`) ✓, property default 0 + copy-ctor ✓, `AdjustCooldown` floor math ✓, executor guard `if (template.CooldownReduction > 0f)` ✓ — every layer passed its unit tests. But the XML emitted NEGATIVE values, the calculator added them to a 0 base, the resulting `CooldownReduction` was negative, the guard rejected it, and the entire feature was a no-op. Codex #1 (with vanilla decompilation), a 23-agent deep-review fan-out, and a completeness critic ALL validated the layers in isolation and missed the composition bug; Codex pass-2 caught it by grepping `CooldownReduction` in the shipped XML and tracing one value through the chain.
- **Prevent:** For any feature gating downstream of a config pipeline, write at least one end-to-end smoke test consuming a real shipped config entry. When dispatching `/review-codex` or `/deep-review`, the prompt MUST ask the reviewer to trace at least one real XML value through the full pipeline and report the property value at the guard call site — "validate the math on positive inputs" is not enough. TAOM at-risk pipelines: CareerSystem (`Duration`/`Radius`/`CooldownReduction` mutations), CulturalFeats, SpecialResources, RevoltTuning.
- **Source:** memory/feedback_end_to_end_xml_to_guard_smoke_required.md, docs/reviews/codex-adversarial-career-102-104-postfix-2026-06-02.output.md

### A mirror/expected-value table needs a test asserting mirror == production
If a test suite uses a hand-maintained "mirror table" of expected values because the production code that sets those values can't run in the unit-test harness, you MUST also have a test asserting the mirror equals production (e.g. a source-parse). Otherwise the mirror and production drift silently and every behavioral test passes against the (possibly wrong) mirror, not production. Pin the load-bearing fields specifically — for feats that's the SIGN of EffectBonus (a flip inverts the feat), `isPositiveEffect` (encyclopedia color), and `AdditionType` (Add vs AddFactor changes the math).
- **Why missed:** 2026-06-07, cultural-feats Wave 1 (Codex MEDIUM). `CulturalFeatsService` feats are `FeatObject`s whose metadata is set in `TaomCulturalFeats.InitializeAll()` → `FeatObject.Initialize`, which reaches into the TaleWorlds framework and can't run in MSTest. The workaround `CulturalFeatsServiceTests.EnsureFeatsInitialised` reflection-injects FAKE `FeatObject`s from a mirror table of `(field, stringId, effectBonus)` tuples; nothing asserted the mirror matched production `Initialize(...)`. A production sign-flip (`+0.15f` where `-0.15f` was intended) would invert the feat in-game yet pass every dispatch test (they use the correct-valued fake). `RegisterAll_UsesCorrectStringIds` looked like a backstop but despite its name only COUNTS fields, never verifies the ids.
- **Prevent:** When you see fakes/mocks injected from an expected-metadata table instead of the real initializer, ask "what asserts this table matches production?" — if nothing does, that's the gap. The fix used: `TaomCulturalFeatsDefinitionTests.Wave1Feats_ProductionMetadata_MatchesSpec`, a source-parsing test that reads `Main/Features/CulturalFeats/TaomCulturalFeats.cs` and asserts each feat's `Register("id")` binding and `Initialize(...)` `(effectBonus, isPositiveEffect, AdditionType)` against a canonical spec (deliberately brittle to renames). Generalization for the /deep-review Completeness agent: test *presence* is not test *power* — verify a mirror == production test exists (added to AGENTS.md "Bugs Codex typically misses").
- **Source:** memory/feedback_mirror_table_drifts_from_production.md, docs/reviews/rca-cultural-feats-wave1-2026-06-07.md

### Parse the REAL shipped config file in a test — synthetic XML misses schema/consumer mismatches
For any feature driven by bulk-authored config (many entities authored by hand or generator), unit tests that feed the parser synthetic hand-written XML cannot catch the case where the content uses a schema/convention the parser or consumer doesn't support — the synthetic XML always matches the code's assumptions because the same author wrote both. Add an integration test that loads the REAL shipped file and asserts: (1) every authored entity parses into a non-trivial in-memory object (e.g. every `<Choice type="Passive">` yields a non-null `Passive` with a recognized `EffectType`), and (2) every value referenced by the config (enum value, effect type, id) has a live consumer cross-referenced against the GameModels/services that read it.
- **Why missed:** Career party-size RCA 2026-05-29. Two defects shipped and survived the original feature review: (1) 310 career choices were authored in a `<PassiveEffects>` (plural) wrapper with a `value=` attribute, but the parser read only a direct `<PassiveEffect>` child and only `magnitude=` — whole careers across 16 cultures had completely dead passives, yet every unit test passed because they all fed the direct schema; (2) 5 `PassiveEffectType` values (`Ammo`, `HorseChargeDamage`, etc.) were authored in XML with no GameModel/service consumer — the magnitude parsed and cached to a float nothing read. Both are upstream of or invisible to standard deep-review checks: the data-flow enum-coverage trace starts from the *parsed* passive, but the wrapped entries never parsed, so they weren't in the cache to trace.
- **Prevent:** A single real-file integration test would have caught defect #1 immediately (310 null passives) and surfaced #2. Sibling rules: data-flow tracing, enumerate-from-source-of-truth.
- **Source:** memory/feedback_parse_real_config_in_tests.md
### A doc-vs-config consistency check cannot catch a defect present in both
The BannerBearers Completeness agent (2026-07-16) verified all 11 shipped config fields matched the feature doc's Configuration table **field-for-field and value-for-value, and passed** -- while six of the config's culture keys were dead and matched nothing in the game. The doc and the config were consistently wrong together, so cross-checking them proved only that they agreed. The same review's `ShippedBannerBearerConfigTests` validated every banner item **id** against vanilla's `banners.xml` and passed for the same reason: the values were all real; nobody validated the keys.
- **Why missed:** "does the doc match the shipped config?" and "does the shipped config match reality?" feel like the same question and are not. Only the second has teeth. Validating the value side of a map is the reflex; the key side is where the silent failure lives.
- **Prevent:** pin config against the **engine/ModuleData reality**, never against the documentation. For any map keyed on entity ids, ship a test asserting every key resolves against the real entity set at the same time you ship the config. Treat a doc-vs-config test as a drift detector only -- never as correctness evidence.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (finding 1; "why each agent missed these").
### Never defer a preventive action on "if recurrence happens" for a bug class that fails silently
The 2026-05-23 Rhûn/Gondor RCA correctly diagnosed pool entries referencing nonexistent troop ids, then wrote its own preventive action as *"a script-level check could catch this at PR time — open as follow-up **if recurrence happens**."* It was never built. Two months later the hole was not only still open but wider: the Gondor recruitment JSON added in the interim had no id validation at all. The conditional is the defect — a null `CharacterObject` silently drops a volunteer slot, so nobody ever files the bug report that would trigger the follow-up. The recurrence signal does not exist, so the deferral is permanent.
- **Why missed:** the deferral reads as prudent triage ("don't build tooling for a one-off"), and nothing re-reads closed RCAs looking for unexecuted follow-ups. The trigger condition sounds observable and is not.
- **Prevent:** when a bug's failure mode is *a plausible, working, wrong result* rather than an error, its preventive action must be built in the same session or filed as a real tracked issue with an owner — never gated on recurrence. Apply the test: "what observable event would tell me this happened again?" If the answer is "none, by construction", conditional deferral is equivalent to no prevention. Same shape covered the over-100 percentage group and the C#/JSON drift in the same 2026-07-27 review.
- **Source:** docs/reviews/rca-gondor-recruitment-2026-07-27.md (F2 + "Root cause pattern").
### Verify a new guard RED before accepting it GREEN — a guard never seen failing is not a guard
Three test gates shipped in the 2026-07-27 Gondor review (unfiltered JSON id check, per-group 100% total, C#↔JSON drift). Two of them were authored *after* the defect they guard was already fixed, so they passed on first run and proved nothing. Each was therefore verified by deliberately injecting its failure — a typo'd id into the production JSON, a perturbed weight into the C# pools — confirming the gate named the exact defect, then reverting. The drift guard in particular was self-certifying by construction: it validated a sync written by the same author in the same session.
- **Why missed:** a green new test feels like evidence. It is only evidence that the code is currently in the state the test encodes — it says nothing about whether the test can detect leaving that state. Guards written after the fix never pass through a natural RED phase, which is exactly when TDD would have proven them.
- **Prevent:** for any guard added post-fix (not TDD-first), inject the failure once and read the assertion message before accepting it. Especially when the guard validates work the same author just did.
- **Source:** docs/reviews/rca-gondor-recruitment-2026-07-27.md ("Preventive actions taken").

### When a method returns a report as well as a state change, test the report — not just the mutation
The `taom.add_special_resources` cheat clamped balances correctly in all 8 of its original tests, and still lied about it: the console echo keyed "did a clamp fire?" on the *sign of the request* (`After >= Cap && amount > 0f`) rather than on the unclamped result. A save whose balance predates a lowered cap clamps on a NEGATIVE grant too (550 − 10 → 500, not 540) and reported "(cap 500)" as though nothing happened; the floor-at-0 was never reported at all. Every test asserted the resulting balance and none asserted the message.
- **Why missed:** coverage was measured by *case count* (cap, floor, NaN, unresolved, already-at-cap — all present) rather than by *output surface*. The message was also stranded inside a static console method needing `Campaign.Current`, so "untestable" was accepted instead of being fixed by extracting the pure formatter.
- **Prevent:** enumerate every branch of a user-facing result string and construct the state each one describes — including states the happy path cannot reach (here: a stored balance above a cap lowered after the save was written). If the formatting is stranded in an untestable entry point, extract it to an `internal static` helper (`InternalsVisibleTo("TAOM.Tests")` is already wired in `Main/TAOM.csproj`) rather than accepting the gap. A debug tool that misreports its own effect is worse than one that does nothing.
- **Source:** docs/reviews/rca-specialresources-cheat-2026-07-30.md (F1).

### Pin the engine's reflection contract when the engine's discovery loop is unguarded
`CommandLineFunctionality.CollectCommandLineFunctions` builds every console command with a bare `Delegate.CreateDelegate(typeof(Func<List<string>, string>), method)` — no try/catch. A TAOM method carrying the attribute with the wrong shape throws inside that loop and aborts discovery for *every other command in the same pass*, including vanilla's `campaign.*` cheats. The blast radius of a bad refactor is the whole console, not our one command.
- **Why missed:** nothing in the changeset was wrong — the compatibility agent surfaced it by reading what the engine *does with* the binding rather than stopping at "the attribute exists." Per-file review cannot see this.
- **Prevent:** when binding to an engine mechanism that discovers TAOM members by reflection, add a reflection invariant test asserting the required shape and, where possible, performing the engine's own construction call (`ConsoleCommandBindingTests`). Ask of any reflection-based engine binding: *what happens to everything else if mine is malformed?* Note `Assembly.GetTypes()` throws `ReflectionTypeLoadException` in the test host for UI-dependent types — mirror the engine's `GetTypesSafe()` with a catch that keeps the non-null types.
- **Source:** docs/reviews/rca-specialresources-cheat-2026-07-30.md (F4).

### A guard test must be able to FAIL on the change it claims to guard against

Before committing a test whose comment says "regression guard against X", name the concrete edit that
should turn it red and confirm it would. A test over a hand-maintained literal cannot see a change to
the real thing.
- **Why missed:** `Detect_TaomOwnDefinerBaseIds_AreMutuallyDistinct` asserted `Count == Distinct().Count()`
  over a hardcoded four-element `TaomKnownBaseIds` array, with a comment promising it was a "cheap
  regression against someone adding a fifth definer by copy-paste". Nothing coupled that array to the
  real `SaveableTypeDefiner` subclasses, so exactly that scenario left it green. A genuine
  reflection-based check already existed in another feature's suite
  (`PresetSaveableTypeDefinerTests.BaseId_UniqueAcrossDiscoverableDefinersInTaomAssembly`), making the
  literal list pure drift surface. Both were deleted.
- **Prevent:** for any "these values must stay distinct/in sync" test, reflect over or read the real
  source of truth. If you cannot, the test is documentation, not a guard — say so in its name and
  comment rather than claiming coverage that doesn't exist.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` finding #13

### An allowlist entry that claims to mirror a dependency must be pinned by a test using that dependency's real value

A protected/exempt list whose comment says "mirrors X" is a claim, and comments do not run.
- **Why missed:** `PatchShield`'s protected-owner list carried `"Bannerlord.UIExtenderEx"` since
  2026-05-27, documented as mirroring the vendored DLLs. UIExtenderEx actually registers
  `bannerlord.uiextender.ex` and `bannerlord.uiextender.ex.viewmodels.<module>` — the real ids put a
  dot between "uiextender" and "ex", so the prefix matched neither, and PatchShield's rescue path
  would have unpatched TAOM's OWN UI mixins after an engine bump. The single allowlist test used a
  ButterLib id, which does match, so it passed while the gap sat next to it.
- **Prevent:** one test per allowlist entry, asserting against the value the dependency actually
  produces (grep the vendored source or decompile it). If the entry exists to protect a specific mod,
  the test must use that mod's real Harmony id / assembly name, not a plausible-looking one.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` finding #4

### A parser tested only against the format you invented will pass every test and fail every real input

The `#371` build-stamp detector parsed `InformationalVersion` by searching for `"+build."` and trimming a trailing `Z`. Six unit tests passed. It failed against **every** real assembly: `Directory.Build.props` is imported *before* the csproj's `PropertyGroup`, so `$(Version)` is empty there and the string has no version prefix; and `Bannerlord.BuildResources` appends its own `.{commit-sha}`/`+{commit-sha}` suffix, so the timestamp is not at the end. The detector reported "no build stamp — cannot verify pairing" for a stamp that was sitting in the DLL. A version-mismatch detector that always says "cannot verify" is worse than none: it looks like coverage.
- **Why missed:** every test literal was written from the format the code intended to emit, never from the format the toolchain actually produced. The build-time transformation (a NuGet package appending a suffix; MSBuild evaluation order emptying `$(Version)`) was invisible from the source. This is the sibling of the existing "mock the lowest-level inputs, never the derived predicate" lesson — same root, different surface: the test encoded the assumption instead of the observation.
- **Prevent:** for anything that parses a value produced by the **build** (assembly attributes, generated files, SourceLink/GitInfo output, packed resources), get one real value first — `[Reflection.Assembly]::LoadFrom(...)` and print the attribute — and paste it into a test verbatim before writing the parser. Then run the finished parser against the built artifact end-to-end, not only against literals. Slice fixed-width rather than trimming when a suffix may be appended by tooling you do not control.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md` "The detector shipped broken first"; `Main/Core/Diagnostics/BuildStampReport.cs`, commit `e0e4fd57`

### A safety guard needs its own self-test, or it can silently disable the fix it protects

The `ActionIndexCache` repair refused to write any field whose looked-up name the engine did not echo back via `GetName()` — a guard against writing a WRONG animation index into a vanilla static. But nothing verified that native names round-trip exactly. If they did not (different case, a canonical form, or the first of several aliases sharing an index), the guard would reject **every** field, the repair would write nothing, and the pass would then fall through to `_completed = true` and report success. A guard added to make the fix safer could therefore make it a permanent silent no-op — and the log would say "name-mismatched", pointing at the data rather than at the guard.
- **Why missed:** the guard was added in response to a review finding, and review-driven additions get less scrutiny than original code — it reads as pure risk-reduction, so "what if this rejects everything?" never gets asked. The self-review also framed the guard as the conservative option, which made its failure mode invisible.
- **Prevent:** any guard that can reject 100% of its inputs needs (a) a **self-test against a known-good input** before it is trusted — if the guard fails its own probe, disable the guard rather than the feature, and log which happened; and (b) a distinct outcome for "rejected everything" versus "nothing needed doing", because latching success on an all-rejected pass converts a recoverable state into a permanent one. Ask of every new guard: *if this returns false for every input, what does the caller do, and is that distinguishable from success?*
- **Source:** Codex adversarial review S1, `docs/reviews/raw/codex-adversarial-actionindexcache-repair-2026-08-01.md`; `Main/Features/HeroRace/ActionIndexCacheRepair.cs`

### Bounded retries: "return false so a later phase retries" is a per-frame rescan if the caller is a hot path

Fixing a deep-review finding (a completion flag latched before the work, so failures were permanent) introduced the opposite defect: `RepairFields` returned `false` on failure with no attempt cap, while its primary caller was a Harmony **prefix on `CharacterTableau.RefreshCharacterTableau`**. An unrecoverable failure would therefore re-run a 215-field reflection + native scan on every tableau refresh for the rest of the session.
- **Why missed:** the fix was evaluated against the finding it addressed ("failures must be retryable"), not against the call sites. The reviewer who found the latch bug did not own the call-site placement, and the call site had moved to a hot path in the *same* changeset.
- **Prevent:** when converting a one-shot to a retryable operation, bound the attempts and check what invokes it — "retry later" is only safe if "later" is rare. Re-read the call sites after any change to a completion/latch flag, especially when the same changeset also moved where the operation is invoked from.
- **Source:** Codex adversarial review S3, same file

---

---

### A negative assertion is vacuous unless the ARRANGE makes the guard load-bearing
The existing "a test must be able to fail" entry bans vacuous *assertions* syntactically (`or True`, `!= <one-of-many>`). This is the arrange-side form, and it is invisible to that check: `DidNotReceive()` / `IsNull` / `AreEqual(0, ...)` is syntactically normal but pins nothing when the arrange leaves the system unable to produce the effect anyway.
- **Why missed:** two `LordSpawnGuard` guard tests (#374, 2026-08-04) asserted `DidNotReceive().SetFactionInitialHomeSettlement(...)` without stubbing any anchor candidate — so `FindAnchor` returned null and the service wrote nothing whether the guard under test existed or not. Both assertions held in both worlds. A differently-named sibling test (`..._DoesNotScanSettlementsForTheCulture`) was the only real mutation kill, so after a regression the failure list would have named the wrong thing. Third instance of a test-that-cannot-fail (skillspector 2026-06-22, `rca-validator-silent-scope-2026-08-03`, now this).
- **Prevent:** for every guard-named test, name the ONE production line whose deletion turns it red — if you cannot, the arrange is incomplete. Mechanically: put the SUT in a state where removing the guard changes the outcome, then assert the negative. Worth making a required `/deep-review` completeness-agent output ("for each new test, name the line whose deletion reddens it") rather than another prose paragraph.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (deep-review M4/L4, 2026-08-04)

### Before building a diagnostic, prove the state it measures is REACHABLE given the symptom

A diagnostic whose only reachable outcome is "fine" is worse than none: it converts a foregone
conclusion into apparent evidence and burns a build/deploy/repro cycle doing it. Ask, of the
instrument's positive result, "what would the screen look like if this fired?" — and if that does not
match the reported symptom, the instrument is aimed wrong.

- **Why missed:** the #389 instrument reported "resource loading counter never cleared ⇒ black
  silhouette". But vanilla `CharacterTableau` hides the freshly-refreshed buffer and only shows a
  visual once **both** loading counters clear, so a character whose resources never load renders
  **blank**, not black. Correct geometry on screen therefore proved the counters had already cleared —
  the instrument could only ever log "fine". The author then reasoned that a double-"fine" result
  would still be informative ("it exonerates residency"), which is the specific trap: a negative from
  a test that cannot produce a positive carries no information. Caught by adversarial review.
- **Prevent:** write the expected log line for BOTH outcomes before writing the code, and check each
  against the observed symptom. Include a **known-good control** in every census/dump — the second
  #389 instrument reported `metaMeshCount=0` for every character *including ones that render
  correctly*, and only the control made that legible as an instrument fault rather than a finding.
- **Source:** #389 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

### On a per-frame hook, audit what runs BEFORE the early-out, not just what runs after it

A hot-path hook is only as cheap as its first statement. Reviewing the code that follows the early
return proves nothing about steady-state cost if work happens before it.

- **Why missed:** Patch67's OnTick postfix was adversarially reviewed for hot-path cost and cleared —
  the reviewer correctly established that every lambda and every reflected field read sat *after* the
  `if (result.Verdict == None) return;` and that the lock was uncontended. Nobody asked what the two
  statements *above* it did: the first built a key string via interpolation, so the "allocation-free
  steady state" allocated once per frame per live tableau. A later deep-review pass caught it as HIGH.
- **Prevent:** for any `OnTick`/per-frame/per-hit postfix, read the method top-down and mark the exact
  statement where the cheap early-out fires; everything above it is the true steady-state cost and must
  be allocation-free. Prefer a weak-table/identity lookup plus an ordinal compare over rebuilding a
  key. **Also verify an agent's proposed fix separately from its diagnosis** — the efficiency agent's
  suggested `Dictionary<CharacterTableau, string>` cache would have traded a bounded per-frame
  allocation for an unbounded leak of pinned engine objects.
- **Source:** #389 deep review (Review 82b), 2026-08-06 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

### A binding test that resolves a NAME does not test the CONVENTION that consumes it

`AccessTools.Field(type, "_race") != null` passes whether the Harmony parameter is spelled `___race`
(broken) or `____race` (correct) — it proves the field exists, never that the patch can bind it.

- **Why missed:** Patch67 shipped with three underscores, passed four dedicated binding tests and all
  5588 suite tests, and failed only at runtime — where TAOM's isolated patch batch *logs and swallows*
  the category failure, so the symptom was a patch that silently never ran. Found by reading the live
  game log after an in-game repro.
- **Prevent:** when a framework consumes a string/naming convention, test the **convention**, not the
  target. Gate: `TAOM.Tests/Migration/HarmonyFieldInjectionNamingTests.cs` scans every
  `[HarmonyPatch]` class, strips exactly three underscores from each `___` parameter, and asserts the
  remainder resolves — with a non-zero-coverage assertion so a broken scan cannot go vacuously green.
  Verified red-then-green by reintroducing the defect. Full rule in
  [`lessons/harmony-il.md`](harmony-il.md).
- **Source:** #389 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
### A diagnostic that cries wolf is worse than no diagnostic — prove the rule it asserts

Before shipping a check that tells players something is broken, prove its rule holds on a KNOWN-GOOD
install. If the check fires on vanilla, it is wrong by construction — and every other line that
tool emits loses credibility with it.

**Why missed:** `SaveDefinerCollisionGuard` grouped `SaveableTypeDefiner` subclasses by their base
save id and reported any shared id as a collision, with "the game will fail to start… Disable one of
them." But the engine keys on `_saveBaseId + saveId` (`SaveableTypeDefiner.AddClassDefinition`,
v1.4.7), so a shared BASE id is legal whenever per-type offsets differ — and vanilla does exactly
that: `SaveableCoreTypeDefiner` (TaleWorlds.Core) and `SaveableObjectSystemTypeDefiner`
(TaleWorlds.ObjectSystem) both use 10000 in a game that starts fine. Being in different assemblies,
they took the cross-assembly branch, so TAOM told players to disable a vanilla engine DLL. It sat at
the top of every collected user log. Nobody had run the detector's own rule against an unmodded
install; the unit tests only exercised synthetic records that obeyed the assumed model.

**Prevent:** (1) for any heuristic check, add a test using REAL values from a known-good baseline —
here, the actual vanilla pair and base id. (2) Match severity to certainty: a heuristic emits a
WARNING that says "may" and offers a lead, never an ERROR that says "will" and issues an order.
(3) Filter to what the user can act on — a fault confined to game-shipped assemblies is noise
regardless of what it means.

**Source:** reported from user logs 2026-08-01; root cause verified by decompiling v1.4.7 and
enumerating all 67 vanilla definer base ids (exactly one duplicate pair, both vanilla).

### When the failure mode is CORRUPTION rather than loss, assert the value — a count assertion passes against the bug it was written for
`RacePersistenceService.CapturedRaceCount` is `_heroRaceMap.Count`, i.e. the number of HEROES captured. The degenerate one-race capture the guard exists to reject writes exactly the same number of entries as a good capture and zeroes every value, so `Assert.AreEqual(2, CapturedRaceCount)` is true before and after the mass-humanize. A count is the reflex assertion for a capture, and it is blind to this entire class.
- **Why missed:** cardinality is the cheapest observable and reads as a proxy for correctness ("it captured everything, so it worked"). It is a real proxy for LOSS — a dropped hero, a filter that over-matched — and no proxy at all for CORRUPTION, where the shape is preserved and the contents are replaced. The two failure modes look identical at the count and opposite at the value.
- **Prevent:** ask which failure mode the test is guarding — if the bug preserves cardinality, demote the count to a stated precondition and assert the persisted state. `RacePersistenceServiceTests` does exactly that: `Assert.AreEqual(2, _sut.CapturedRaceCount, "precondition: rich capture succeeded")` sets the stage, then `Assert.AreEqual("human;dwarf;elf", store.LastSavedLegend, …)` makes the real claim through a round-trip data store, and `CaptureHeroRaces_OneRaceLegend_ThenRestore_DoesNotMassHumanizeHeroes` drives capture → degenerate capture → restore and asserts `DidNotReceive().SetHeroRace("hero_dwarf", 0)`. Same family as the vacuous-assertion and guard-must-be-able-to-fail entries above, with a sharper trigger: an assertion that is true in both the fixed and broken states is vacuous even when the number in it is correct.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit 7cf5be28

### An audit query that reports "zero found" needs a positive control before you believe it

Auditing whether any dwarf equipment roster carried a horse, an XPath of
`.//equipment[@slot='Horse']` returned **0 hits across every culture**. The element is `<Equipment>`;
XPath is case-sensitive. The query was incapable of matching anything, and its output — a clean zero —
is indistinguishable from the answer you were hoping for. It was caught only because a subagent
independently reported two rosters that *did* carry horses, contradicting the scan.

- **Why missed:** a search that finds nothing produces no error, no empty-file warning, no stack trace.
  Every other failure in an audit announces itself; this one renders as success, and it renders as
  success in exactly the direction that ends the investigation early.
- **Prevent:** before trusting any negative result, run the same query against a case you know is
  positive. The dwarf scan was re-run counting non-dwarf rosters with a horse in the same pass — 582 —
  which proves the matcher works before the zero for dwarves means anything. Cheap enough to be
  unconditional: one extra counter in the same loop. The `MOUNTED_DWARF` unit tests encode the same
  discipline with `test_non_dwarf_cavalry_with_a_horse_is_clean`, which fails if the check ever degrades
  into matching everything, and `test_lowercase_equipment_tag_is_still_caught`, which pins the exact
  case-sensitivity trap that caused this.
- **Generalises to:** grep sweeps for "no remaining references", validator runs reporting PASS, and any
  "I checked, it's clean" claim. `evidence-over-claims.md` §C already forbids inventing a result; this is
  the adjacent failure of *believing a real result produced by a broken instrument*.
- **Source:** dwarf-lord formation audit, 2026-08-04.

### A cross-language decision mirror is pinned by a boundary VALUE, not by mirroring its constants

**Why missed:** The [MemSample] contract (#386) pinned the log-line FORMAT with twin literal tests
(C# formatter ↔ Python fixture) and mirrored the threshold constants (2048/10/512) with a
"numerically identical" comment — but the ARITHMETIC diverged: C# `limit * 10 / 100` floors
(long division), Python `headroom * 100 < limit * percent` compares exactly. They disagree in the
~1 MB band below the true 10% line on any commit limit not divisible by 10 — essentially every real
machine. Every pre-existing test used round limits or cases decided by the floor rule, so both
suites were green around a live drift. Two review agents caught it independently with different
counterexamples.

**Prevent:** When a decision function is mirrored across languages (or across a game↔tool boundary),
add a boundary pin at a NON-ROUND input in BOTH implementations — a value where truncation,
rounding, or clamp semantics can differ (e.g. `limit=31646` → threshold 3164 floored from 3164.6;
assert headroom 3164 healthy / 3163 low on both sides). When briefing parallel lanes on a shared
contract, pin boundary VALUES alongside the literal strings.

**Source:** deep-review 2026-08-05, `docs/reviews/rca-memsample-telemetry-2026-08-05.md` finding #1.
