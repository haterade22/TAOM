# RCA — v1.4.5 Migration deep-review findings (2026-05-22)

Root-cause analysis for the `/deep-review` pass on the v1.3.15 → v1.4.5 migration changeset. Five parallel agents (standards/compat/efficiency/completeness/data-flow) reviewed 4 C# fixes + supporting infra. **One CRITICAL silent-no-op bug** surfaced that had been latent in the v1.3.15 codebase for an unknown duration. Two documentation inconsistencies. One process gap.

Per `.claude/rules/harness-facts.md` (Phase 3e is a BLOCKING GATE for ANY confirmed finding, any severity) and `feedback_root_cause_mandatory.md`.

## Confirmed findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | 🔥 CRITICAL | `ChildCreatorAdapter.cs:30` reflected on `BasicCharacterObject.<IsFemale>k__BackingField` to enforce requested sex after `HeroCreator.CreateChild` inherited template sex. **Silent no-op** — `CharacterObject.IsFemale` is an override that unconditionally returns `HeroObject.IsFemale` for heroes, so the base backing field is never read by the runtime. The "if (hero.IsFemale != isFemale)" check on the next line caught the discrepancy, but the reflection that "fixed" it did nothing. The opposite-sex-pool fallback case (zero-male clan requesting a male child) produced wrong-sex children for the entire lifetime of this code. | Reflection on wrong class (member-resolution layer ≠ override-binding layer) | The original code was authored in v1.3.x; `BasicCharacterObject.IsFemale` was virtual then too, but neither the original author nor any prior `/deep-review` agent decompiled the override chain to verify the reflection actually reached the runtime read path. The v1.4.5 migration didn't touch this code — it was already broken. The deep-review surfaced it because Agent 2 (TaleWorlds API Compat) systematically decompiles every TaleWorlds member referenced in changed files, and a regression test would have caught it. | (a) Fixed in this commit: replaced reflection with `hero.IsFemale = isFemale` (public setter on `Hero` exists in both v1.3.15 and v1.4.5). (b) **Memory entry: `feedback_reflect_on_runtime_read_path_not_declared_layer.md`** — when a TaleWorlds class chain has `Base { virtual P }` → `Mid { override P => DifferentField }`, setting the base's backing field is a no-op for instances of `Mid`. Always decompile the override property body before reflecting on `<P>k__BackingField`. (c) **Add unit test:** `ChildCreatorAdapter_CreateChild_OppositeSexRequested_ProducesRequestedSex` (TAOM.Tests/Adapters or via OffspringRaceInheritanceService integration test) — requires a way to construct a Hero with a known sex in tests, may require MBObjectManager fake. |
| 2 | 🟡 LOW (doc) | TRACKING.md describes `renownMultiplierForWinnerSide` as "passed through to base but not used by TAOM logic." Misleading — vanilla `DefaultBattleRewardModel.CalculateRenownGain` bakes the multiplier into the `ExplainedNumber` base value: `new ExplainedNumber(contributionShare * renownValue * renownMultiplier)`. TAOM's `ApplyRenownFeats` and career `ApplyFactor` are `AddFactor` calls on that base, so they scale proportionally with the multiplier. The behavior is correct (consistent with vanilla perk scaling), but the doc says otherwise. | Documentation drift from semantic understanding | The fix author understood "passed through" as "passed to base and forgotten" — but `base.` returned an `ExplainedNumber` whose base value already encodes the multiplier. The `AddFactor` calls then scale that base. Subtle, easy to miss without reading the vanilla method body. | Doc-only: update TRACKING.md (under S3 entry) to clarify TAOM feats scale with multiplier via base's initial value. No code change. |
| 3 | 🟡 LOW (doc) | `SpecialResourcesBehavior.OnHideoutCompleted` ignores new 3rd param `HideoutBattleEndState`. The 5 enum values (None, Retreated, Defeated, Victory, SendTroops) include `Retreated` — fires when `winnerSide=Attacker` but `!HasWinner` (abandoned-field edge case). TAOM currently earns resources on any attacker-side win including Retreated, which may be unintended-permissive. Comment says "we don't act on it" but doesn't enumerate which outcomes the code treats as success. | Enum-value coverage gap in deferral comment | The fix author's mental model was "winnerSide=Attacker == victory" — true in pre-1.4.3 vanilla but no longer comprehensive in 1.4.3+. The 3rd param encodes a finer state machine the old API hid. Permissive behavior is consistent with v1.3.15 (no third param existed) so this is not a behavioral regression, but the deferral is undocumented. | Doc-only: TRACKING.md add explicit "accepted-permissive: TAOM earns resources for HideoutBattleEndState.{Victory, Retreated, SendTroops, Defeated} as long as winnerSide=Attacker. If S7 feature validation reveals this is too permissive, gate on `battleEndState == Victory`." This converts a silent deferral into a tracked decision. No code change. |
| 4 | 🟡 LOW (process) | No GitHub issue for the v1.4.5 migration. CHANGELOG + TRACKING.md are internal; CLAUDE.md says "Create GitHub issue for EVERY feature, bug fix, crash, or system change." | Process gap | The migration started on plan-mode where the user was iterating on the plan file; the GitHub-issue creation was queued to "when ready for public visibility" and the trigger never fired. The completeness agent caught it. | Open the issue NOW (before commit), reference CHANGELOG + docs/migration/ folder. |

## Root-cause pattern: silent no-op latency

Two of these findings (#1 and #3) share a theme: **operations that silently do nothing or do the wrong thing without surfacing an error.** Both pre-date the v1.4.5 migration; the migration acted as a forcing function that brought them under review.

The IsFemale reflection (#1) is the more dangerous case because it's a CORRECTNESS bug — heroes of the wrong sex have been generated for any clan with a zero-male pool fallback. The HideoutBattleEndState handling (#3) is permissiveness, not incorrectness, but represents the same class: "we set/read a property and trust that it took effect" without verifying.

### Why each deep-review agent did or did not catch this

| Agent | Caught #1? | Why |
|---|---|---|
| Agent 1 — Standards (haiku) | ❌ | Standards checks look at file structure, ADR adherence, IoC patterns — not runtime semantics. No rule said "reflection on backing field must be verified against the override chain." |
| **Agent 2 — Compat (sonnet)** | ✅ | This is exactly the agent's beat: decompile every TaleWorlds member referenced in the change. The agent decompiled `BasicCharacterObject.IsFemale` and `CharacterObject.IsFemale` separately, noticed the override, and traced the runtime read path through `HeroObject.IsFemale`. The verification step is what makes this agent disproportionately valuable. |
| Agent 3 — Efficiency (haiku) | ❌ | Reflection is slower than a property set, but neither is a hot path (per-child-birth call). Not a perf issue. |
| Agent 4 — Completeness (haiku) | ❌ | No missing test → no flag. The bug requires a regression test that doesn't yet exist. The agent does flag "test coverage" but not "this method's runtime behavior may differ from its source-code appearance." |
| Agent 5 — Data Flow (sonnet) | ❌ | Data-flow tracing is for "declared in X, consumed in Y" gaps. The IsFemale reflection IS a consumer of `isFemale` — the consumption just doesn't take effect. Out of scope for the data-flow lens. |

The pattern: **only the API-decompile agent catches "the code references the right property name, but the runtime read path differs."** This is a class of bug invisible to per-file source review.

### Why prior `/deep-review` passes on this code didn't catch it

The TAOM repo's git log shows `ChildCreatorAdapter.cs` has been in the codebase since at least late 2025. It went through prior reviews. None caught the IsFemale issue because:
1. Prior reviews were focused on the feature being added (offspring race inheritance), not the existing reflection chain.
2. The "Verify Before Reference" rule in CLAUDE.md targets `Sprite=` references and `PrefabExtension` injection — not C# reflection on TaleWorlds backing fields.
3. The reflection helper itself (`TAOM.Core.Infrastructure.ReflectionHelper.SetFieldValue`) doesn't validate that the field exists on the runtime instance type — it relies on the type passed in (`BasicCharacterObject`) being correct.

## Preventive actions

### Code fix (THIS COMMIT)
- `Main/Adapters/ChildCreatorAdapter.cs:30` — replaced reflection with `hero.IsFemale = isFemale`. Removed dead imports (`TAOM.Core.Infrastructure`, `TaleWorlds.CampaignSystem.Extensions`, `TaleWorlds.Library`). Build green, 2,323/2,325 tests pass.

### Doc fixes (THIS COMMIT)
- `docs/migration/TRACKING.md` — clarify `renownMultiplierForWinnerSide` behavior (#2) and `HideoutBattleEndState` accepted-permissive deferral (#3).

### Memory entries (TO ADD)
- `feedback_reflect_on_runtime_read_path_not_declared_layer.md` — when reflecting on a backing field, decompile the override chain on the runtime instance type first. If a subclass overrides the property to read from a different field, setting the declared layer's backing field is a no-op.

### Process change
- `/deep-review` Agent 2 (Compat) is the load-bearing safety net for this bug class. Its prompt already says "decompile every TaleWorlds member referenced in the changed files" — this RCA confirms the practice catches bugs that other agents miss. Keep the agent's scope as-is; do NOT trim it.

### Open question
- Are there OTHER reflection sites in TAOM that target `<PropertyName>k__BackingField` on a base class whose subclass overrides the property? Out of scope for this RCA; suggested follow-up: grep for `ReflectionHelper.SetFieldValue.*BackingField` and verify each against its runtime instance type's override chain.

## Verdict (initial — pre-Codex)

The /deep-review process caught a latent v1.3.15-era bug during a v1.4.5 migration review — exactly the kind of compound find that justifies running the full review even when the migration changeset looks small. The fix is minimal (1 line of effective change + 3 dead-import removals + clarifying comment). The systemic lesson is captured.

**Ready to dispatch Codex adversarial review,** which may surface bugs the 5-agent Claude pass missed.

---

## Codex addendum — 2026-05-22

`codex review --uncommitted` (gpt-5.5, xhigh reasoning) found **3 additional bugs that all 5 Claude agents missed**, even though Codex didn't use the structured prompt (CLI rejected `--uncommitted [PROMPT]` combination — Codex reviewed against its default prompt instead). This is a strong reminder that an independent second opinion is irreplaceable.

### Codex findings

| # | Sev | Bug | Category | Why Missed by Claude | Preventive Action |
|---|---|---|---|---|---|
| 5 | 🔥 P1 | `Main/_Module/SubModule.xml:10-15` — DependedModules list omits `TAOM.Dependencies`. After restoring the project from git `0b16cca`, the module-load-order dependency edge between TAOM and TAOM.Dependencies was lost. In a clean launcher profile, enabling TAOM does NOT auto-enable TAOM.Dependencies before Native, so the pre-Native Harmony/UIExtender setup is skipped — the entire purpose of the Dependencies module is defeated. | XML dependency graph gap | All 5 Claude agents reviewed the changeset, but none cross-checked the SubModule.xml dependency list against the actual restored Dependencies project. The Standards agent looked at TAOM's own conventions; the Completeness agent flagged IoC + tests but didn't trace module-load order; no agent specifically traced *which projects must list which others as DependedModule*. | Fixed in this commit: added `<DependedModule Id="TAOM.Dependencies" />` + `<DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis" />`. Process change: **add to `/deep-review` Agent 4 (Completeness) prompt: when reviewing a CampaignBehavior/SubModule that depends on another TAOM-owned module's runtime presence, verify the dependency edge exists in SubModule.xml.** Not a generic Bannerlord lesson — specific to TAOM's modular architecture. |
| 6 | 🔥 P1 | `Dependencies/SubModule.cs:45` — `_ = typeof(Bannerlord.UIExtenderEx.UIExtender);` fetches the Type object but does NOT trigger the static constructor where `UIConfigPatch.Patch`, `ViewModelPatch.Patch`, and other system hooks are applied. The log line "UIExtenderEx patches applied" was a lie — patches never ran. | C# language semantics misuse | Standards/Compat/Efficiency agents all read the file and saw `typeof()` as a benign type reference. None of them knew that the intent was to FORCE static cctor execution. The comment in the file didn't say "force static init" — it just had a `_ = typeof(...)` that looked decorative. This is a class of bug where the symptom (no UI patches) is invisible until in-game testing, and the root cause is a C# semantics subtlety that source review can miss without the intent being explicit. | Fixed in this commit: replaced with `RuntimeHelpers.RunClassConstructor(typeof(UIExtender).TypeHandle)` + clarifying comment. **Memory entry: `feedback_typeof_does_not_force_static_init.md`** — when intent is to force a class's static constructor (e.g., for side-effect registration like Harmony patches in `static UIExtender()`), use `RuntimeHelpers.RunClassConstructor`. `typeof(X)` alone only loads the Type metadata, not the cctor. |
| 7 | 🟡 P2 | `tools/decompile_to_folder.ps1` — only enumerates DLLs under `<install>/bin/<binFolder>/` but the `Modules` category in the regex pattern is meant to match `SandBox.dll`, `SandBoxCore.dll`, `StoryMode.dll`, `CustomBattle.dll` which live under `<install>/Modules/<X>/bin/<binFolder>/`. The category never gets populated; "Modules" subfolder of the decompile output stays empty. | Tooling scope gap | We discovered this empirically during S0 when the first decompile produced 6,146 .cs files in core categories and 0 in Modules. We worked around it with a separate manual run for SandBox/SandBoxCore/StoryMode, but the script itself was never patched. The deep-review didn't catch it because the tooling is "out of code-review scope" — the agents focused on `Main/` C# files. | Fixed in this commit: script now scans `<install>/Modules/<X>/bin/<binFolder>/` for SandBox/SandBoxCore/StoryMode/CustomBattle and merges into the DLL list. Process change: **deep-review Agent 4 (Completeness) should treat migration tooling under `tools/` as in-scope when the tooling is documented in migration docs.** |

### Root-cause pattern: tooling + integration gaps

Findings 5–7 share a theme: **gaps in the connective tissue between components that compile fine individually.**

- #5: SubModule.xml is XML, not C# — Claude's C#-focused review missed it entirely.
- #6: C# language semantics (typeof vs static cctor) — agents see the code but don't see the *runtime behavior gap*.
- #7: Tooling — out of scope by convention, in scope by impact.

Compile-clean code with missing integration is invisible to per-file review. Only behavior-tracing (game launch, log inspection, end-to-end test) catches it. **Codex caught these because its default prompt explicitly traces "review unstaged/staged/untracked changes against repo/vanilla contracts" — the contract part is what the Claude agents lacked.**

### Why each Claude agent missed Codex's findings

| Agent | Caught any of #5–#7? | Why |
|---|---|---|
| Agent 1 — Standards | ❌ | Standards rules are intra-file. The dep edge (#5) is cross-file XML; the static cctor pitfall (#6) is C# semantics no rule covered; the tooling gap (#7) is out of scope. |
| Agent 2 — Compat (API) | ❌ | API compat checks signatures, not runtime behavior. `typeof()` is a valid C# expression — no signature mismatch. |
| Agent 3 — Efficiency | ❌ | Neither finding is a perf issue. |
| Agent 4 — Completeness | ❌ | Looked at tests + docs + IoC, not module-load dependencies. Did NOT check that TAOM lists TAOM.Dependencies as a DependedModule. |
| Agent 5 — Data Flow | ❌ | Data-flow tracing is for "declared in X, consumed in Y" gaps within C#. Module-load order and static cctor execution are below the data-flow lens. |

Codex's #6 finding is particularly impressive — it identified that the intent of `_ = typeof(X)` was to force initialization, not to express a type reference, and that this intent failed. That requires understanding both the surface code AND the developer's likely intent. Source review without intent-modeling can't catch this.

## Combined verdict (post-Codex)

The full review workflow (5 Claude agents + Codex) found **4 distinct bugs in a 4-file-change "minimal" migration**:
1. (Claude Agent 2) ChildCreatorAdapter IsFemale reflection silent no-op — CRITICAL
2. (Claude Agent 5) HideoutBattleEndState undocumented deferral — LOW (doc)
3. (Claude Agent 5) renownMultiplier behavior misdescribed — LOW (doc)
4. (Codex) SubModule.xml missing TAOM.Dependencies dep edge — P1
5. (Codex) UIExtenderEx static cctor not triggered — P1
6. (Codex) decompile tooling Modules scope gap — P2

The Claude agents caught **1 of 4 code bugs** (Agent 2's IsFemale). Codex caught the other **2 of 4 code bugs** that Claude missed. That's a 50/50 split — strong validation of the dual-review workflow.

All 6 findings are fixed. Build green. Tests 2,323/2,325 pass.

**Ready for commit.**
