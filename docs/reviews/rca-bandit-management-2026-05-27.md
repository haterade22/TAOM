# RCA — Bandit Management Deep Review (2026-05-27)

**Feature:** [Bandit Management](../features/bandit-management.md) — LOTR bandit culture replacement + PlayerProgress scaling
**Issue:** [#247](https://github.com/haterade22/TAOM/issues/247)
**CHANGELOG entry:** 2026-05-27 `feat(bandit-management)`

## Top-line summary

`/deep-review bandit-management` launched 5 parallel agents (Standards, Compatibility, Efficiency, Completeness, Data Flow). Results:

| Agent | Verdict | Findings |
|---|---|---|
| Standards | PASS | 0 |
| Bannerlord API Compatibility | PASS | 0 (all v1.4.5 APIs verified via `taom-src`) |
| Efficiency | PASS | 0 |
| Completeness | INCOMPLETE | 1 missing GH issue (process) |
| Data Flow | **FAIL** | **1 confirmed gap** — `MinPartiesToInfest` was a DEAD FIELD |

One confirmed code finding. Per `feedback_root_cause_mandatory.md` (Phase 3e is BLOCKING for every confirmed finding regardless of severity), this RCA is mandatory.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `MinPartiesToInfest` declared in `BanditScalingConfig.cs`, loaded + validated in `BanditScalingConfigProvider.Validate`, then never consumed by any downstream class (interface, service, GameModel, patch). Pure dead config field. | Data flow gap — DTO populated but no consumer | Authored the POCO + provider + validation first, then forgot to extend `IBanditScalingSettingsProvider` + `IBanditScalingService` + `TaomBanditDensityModel` to actually read the field. Trust-the-test-suite blind spot: 14 tests covered the provider's validation behavior but no test exercised the "is this field ever READ by a consumer?" question — because it's a cross-class observation, not a within-class assertion. | **Fixed in same session.** Extended `IBanditScalingSettingsProvider` + `IBanditScalingService` + `TaomBanditDensityModel` to override `NumberOfMinimumBanditPartiesInAHideoutToInfestIt`. Added 1 unit test for the delegation chain. |

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 2 | LOW (process) | No GitHub issue existed before the closing commit. | Process gap — workflow violation | Author was focused on the technical implementation depth (5 cultures × N templates × 99 hideouts × 12 loc files) and skipped the "open issue first" step. The `/deep-review` completeness agent caught this. | **Fixed in same session.** Issue #247 created before closing commit. |

## Root-cause pattern: "POCO declares it, validator validates it, no consumer reads it"

This is a sibling of three documented patterns:

1. **`feedback_no_aspirational_enum_values.md`** — enum values declared but never produced/consumed.
2. **DTO non-empty-output trace** (Agent 5 rule 2c per the `/deep-review` skill) — collection field is structurally populated but no caller ever sets up the precondition to populate it.
3. **`feedback_enumerate_from_source_of_truth.md`** — added a new attribute by enumerating the existing config row list, not the upstream truth.

All three share: **adding fields/values to a data model without simultaneously wiring a producer AND a consumer in the same PR.** Three has shipped before; this is now four. Each instance had its own narrow cause:

- Career SlotApplyOutcome (2026-05-06): aspirational enum value for a code path that never materialized.
- CrashReport HarmonyCorrelationCollector frames (2026-05-25): structurally populated DTO field, sole caller passed null, advertised feature was silently empty.
- Player startup gold (2026-05-XX): new culture attribute added but only 14 of 16 cultures in the source-of-truth XML got the row.
- **This RCA:** `MinPartiesToInfest` JSON field declared, validated, never read.

The pattern is now common enough to be a class. The existing `/deep-review` rules cover it — Agent 5 caught it correctly here in only-its-second documented hit. The fix is to **keep applying Agent 5 rigorously and not relax the rule** when "the feature is small and obvious."

## Why each agent did or didn't catch this

| Agent | Caught? | Explanation |
|---|---|---|
| Standards | No (out of scope) | Agent 1 checks ADR compliance, not data-flow connectivity. Standards passed — code structure is fine. |
| Compatibility | No (out of scope) | Agent 2 verifies TaleWorlds APIs exist with correct signatures, not whether OUR fields get used. |
| Efficiency | No (out of scope) | Agent 3 checks hot-path allocations, not whether config values are read. |
| Completeness | No (different gap) | Agent 4 caught the missing GH issue. It does not trace data-flow connectivity. |
| **Data Flow** | **Yes** | Agent 5 enumerated all 6 fields in `BanditScalingConfig` and traced each through the consumer chain. The 6th field (`MinPartiesToInfest`) terminated at the validator — explicitly flagged as DEAD FIELD. **This is exactly the bug class Agent 5 exists for.** |

This is the validating-the-validator data point: Agent 5's "JSON config field coverage" rule is doing the job. The cost was 1 extra parallel agent (Sonnet, ~700 words output) — caught a real bug. Continue running Agent 5 on every feature; do not relax it for "small" features.

## Feedback memories to codify

No new memory needed — the pattern is already covered by:

- `feedback_no_aspirational_enum_values.md` (don't ship dead enum values)
- `/deep-review` Agent 5 rule 2c (DTO non-empty-output trace)

Both fire on this category of bug. The system caught the bug as designed. No new rule warranted.

The MEMORY.md index already lists both. No update needed.

## Fix verification

After fixing `MinPartiesToInfest` wiring:

- `dotnet build Main/TAOM.csproj` — 0 errors.
- `dotnet test --filter BanditManagement` — 31/31 pass (added 1 new test for `MinPartiesToInfest` delegation).
- Full suite re-run: 2551/2553 (2 preexisting skips, zero regressions).
- GH issue #247 opened.

`MinPartiesToInfest` now flows: JSON → `BanditScalingConfig` → `BanditScalingConfigProvider` (validate) → `BanditScalingSettingsProvider` (live-clamp to `MaxPartiesPerHideoutCap` to preserve `min ≤ max` invariant at runtime) → `BanditScalingService.MinPartiesToInfest` → `TaomBanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt`.

## Codex adversarial review (Phase 2)

After Phase 1 (deep-review + 1 fix), `/review-codex` ran against the post-fix tree. Codex confirmed **4 additional bugs Claude's 5 deep-review agents missed** (1 CRITICAL, 3 HIGH). All 4 fixed in same session. Prompt + output at [`codex-adversarial-bandit-management-2026-05-27.prompt.md`](codex-adversarial-bandit-management-2026-05-27.prompt.md) / [`.md`](raw/codex-adversarial-bandit-management-2026-05-27.md).

| # | Sev | Bug | Why Claude missed | Fix |
|---|---|---|---|---|
| C1 | CRITICAL | `taom_partyTemplates.xml:1482` — XML comment contained `--` (double-hyphen). XML spec FORBIDS `--` anywhere inside a `<!-- ... -->` body. Engine XML parser rejects the file at load. | Agent 1 (Standards) was C#-focused. Agent 5 (Data Flow) walks XML semantically but doesn't run an XML parser. None of the 5 deep-review agents ran `[xml]$x = Get-Content` on the modified XML files. | Replaced `--` with `=` in the affected comment lines. Added preflight: `pwsh -Command '[xml]$x = Get-Content -Raw "<file>"'` for every modified TAOM XML before commit. |
| C2 | HIGH | `Patch39_BanditPartySize` had `[HarmonyPatch(...)]` attribute but NO `[HarmonyPatchCategory]`. TAOM exclusively uses category-based patching (`SubModule.cs` calls `_harmony.PatchCategory("PatchN_X")` for every patch family) — `PatchAll()` is NOT used. Result: Patch39 was DEAD CODE — bandit party size scaling never engaged at runtime. | Agent 1 checked patch architecture but didn't grep `SubModule.cs` for whether the category was actually registered. Agent 2 (Compatibility) verified the API signatures of the patch target but not the patch registration path. The deep-review prompts don't currently enforce "every new `[HarmonyPatch]` class must have a matching `_harmony.PatchCategory(...)` call." | Added `[HarmonyPatchCategory("Patch39_BanditPartySize")]` to the patch class. Added `_harmony.PatchCategory("Patch39_BanditPartySize");` to `SubModule.cs` after the existing Patch24 line in the main category-registration block. |
| C3 | HIGH | New `is_bandit="true"` cultures in `taom_spcultures.xml` were authored without matching bandit **clan** rows. Vanilla `Hideout.MapFaction` resolves via iterating `party.IsBandit` then falling back to `clan.IsBanditFaction` (which is loaded from `<Faction is_bandit="true">` rows). With no matching clan rows, the 99 migrated hideouts referencing the new culture IDs would have no resolvable MapFaction → `BanditSpawnCampaignBehavior` would stall on spawn or NRE. | Agent 5 (Data Flow) traced the XML refs but only checked culture → troop references inside party templates. It did NOT trace `Culture.is_bandit="true"` → matching `Faction.is_bandit="true"` consumer. This is exactly the bug class the rule exists for but the prompt asked "are referenced troops defined?" not "are the bandit clans defined?" | Authored 5 `<Faction is_bandit="true" is_outlaw="true">` rows in `Main/_Module/ModuleData/characters/clans.xml` — one per new culture, each pointing `initial_home_settlement` at a migrated hideout, `default_party_template` at the corresponding `_raider_party_template`. Schema mirrored from vanilla `forest_bandits` / `sea_raiders` faction entries in SandBoxCore. |
| C4 | HIGH | TAOM_Map module's `settlements.xml` now references LOTR bandit cultures defined in TAOM Main module's `taom_spcultures.xml`, but TAOM_Map's `SubModule.xml` does NOT declare a dependency on TAOM. Load-order is accidental — if a launcher profile loads TAOM_Map before TAOM, the culture references fail to resolve and hideout creation crashes. | Agent 5 traced data flow within Main TAOM but did NOT check cross-module dependency contracts. The deep-review skill does not currently include a "cross-module data dependency" rule — it assumes the changeset stays within one module. | Added `<DependedModule Id="TAOM"/>` + `<DependedModuleMetadata id="TAOM" order="LoadBeforeThis"/>` to `TAOM_Map/SubModule.xml`. Backup saved at `SubModule.xml.bak`. Now TAOM Main always loads first, providing the culture/clan definitions before TAOM_Map registers the hideouts. |

### Phase-2 root-cause pattern: "data declared in one place, referenced in another, no connection check"

C1, C2, C3, and C4 all share the same architectural shape:

- **C1:** New XML content was authored without running an XML parser against it.
- **C2:** New Harmony patch class was authored without grepping for the category-registration call site.
- **C3:** New culture was authored without authoring the matching clan that the engine looks up by `is_bandit`.
- **C4:** Cross-module XML reference was authored without updating the consuming module's dependency manifest.

In all four cases, the per-file review (Agents 1-4) didn't catch them because the bug only manifests when the dependent file/reference is missing — and the per-file review is, by definition, looking AT files, not at what's NOT there. Agent 5 (Data Flow) caught the SIMPLER form of this (`MinPartiesToInfest` dead field) but missed all four CROSS-FILE variants.

**Generalization:** Agent 5's prompt enumerates a list of trace categories ("MCM toggle coverage", "JSON config field coverage", etc.). Each new feature potentially adds new categories. For bandit-management specifically, the missed traces were:
- "Every C# class with `[HarmonyPatch]` must have a matching `_harmony.PatchCategory(...)` call somewhere"
- "Every `Culture.is_bandit='true'` must have a matching `<Faction is_bandit='true'>`"
- "Every cross-module XML reference must have a matching `<DependedModule>` in the consuming module's SubModule.xml"
- "Every XML file modification must be validated with an XML parser"

These could be captured as new permanent rules in the `/deep-review` skill prompt, OR as a "feature-class checklist" appended per feature type (Harmony-using, GameModel-using, cross-module-using). The pragmatic move: append a "Harmony registration verification" rule + "cross-module XML dependency verification" rule to the skill prompt, since both are general patterns that will recur.

### Feedback memories to codify

New memory: **`feedback_harmony_patch_category_registration_verification.md`** — every `[HarmonyPatch]` class without a `[HarmonyPatchCategory]` + matching `_harmony.PatchCategory(...)` call is silently dead code. Add a pre-commit grep gate. (Will write after closeout.)

New memory: **`feedback_cross_module_data_dependency_declaration.md`** — when one TAOM-managed module's XML references entities defined in another TAOM-managed module, the consuming module's `SubModule.xml` MUST declare a `<DependedModule>` on the producer. The launcher does not infer this from XML cross-references. (Will write after closeout.)

New memory: **`feedback_xml_parser_smoke_test_before_commit.md`** — every modified or new ModuleData XML must pass `[xml]$x = Get-Content` before commit. XML spec edge cases (`--` in comments, unescaped entities, mismatched tags) shouldn't be caught only by the engine at game-load. (Will write after closeout.)

The bandit clan finding (C3) is a sibling of `feedback_classify_by_grep_not_by_assumption.md` + `feedback_enumerate_from_source_of_truth.md` — both already documented. The trap was: assumed engine creates clans automatically from `is_bandit` cultures; the truth is clans are independently authored. Neither prior memory mentioned this specific case. Add a one-line note to `feedback_enumerate_from_source_of_truth.md` cross-referencing the bandit-clan case.

## Closeout

- Issue #247 created before closing commit ✓
- CHANGELOG updated ✓
- Feature doc updated ✓
- Tests added ✓
- Build green (post all 5 fixes) ✓
- RCA written (this file) ✓
- Codex Phase 2 findings (4) all fixed + documented ✓

Ready for closing commit.
