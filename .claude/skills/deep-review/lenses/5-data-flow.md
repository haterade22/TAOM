# Agent 5 lens: Cross-System Data Flow Tracing

CROSS-SYSTEM DATA FLOW REVIEW — trace data declarations through the codebase to find gaps where declared data is never consumed, or where parallel code paths use inconsistent logic.

FILES: the list in your spawn prompt.

This review exists because per-file reviews consistently miss bugs that span multiple files. Every check below has caught real bugs in this project.

HARNESS CHANGES: when `.claude/**`, `CLAUDE.md`, `AGENTS.md`, `.ai/**` or a workflow doc is in scope, the data is the contract itself: agent and lens names, step numbers, trigger rules, tool names, flags and exit codes, each declared in one file and consumed in others. Trace every changed declaration to all of its consumers (grep the whole repo, not only the scope), including a hook that reads another hook's output format.

TRACE THESE DATA FLOWS:

1. **XML Config → C# Consumption:** For every configurable value declared in XML (ModuleData/**/*.xml), trace it to the C# code that reads and acts on it. Flag any XML attribute that is parsed but never used at runtime.
   - Read ALL changed XML files. For each attribute/element, grep the C# codebase for where it's consumed.
   - Example bug pattern: XML declares `charge_type="DamageDone"` but the only code that emits charges uses `ChargeType.Kills`.

2. **Enum Coverage:** For every enum type referenced in changed files, check that ALL enum values have at least one handler. Flag any enum value with zero callsites.
   - Example bug pattern: `PassiveEffectType` has 50 values but only 15 are wired into GameModels.
   - Example bug pattern: `ChargeType` has 5 values but only 1 is emitted by mission behavior.

2b. **MCM toggle coverage (MANDATORY — applies to EVERY toggle, not a hand-listed subset):** For every MCM `AttributeGlobalSettings<>` / `AttributePerCampaignSettings<>` derived class in the changed files, enumerate EVERY property (excluding metadata: Id/DisplayName/FormatType/FolderName/etc.) and grep the entire feature's source for read sites. For each property:
   - If the property has ZERO read sites, it's a dead toggle — flag as HIGH.
   - If the property has EXACTLY ONE read site at startup (`SubModule.OnSubModuleLoad`, `IoC.Configure`, a static initializer), AND the MCM hint text promises runtime behavior (words like "when off", "disables", "no-op", "stops"), the toggle promise mismatches the implementation — flag as HIGH (user-facing-promise pattern).
   - If the property has runtime read sites, verify each read actually gates behavior — a read-but-discard (e.g., logged but not branched on) is the same as dead.
   - **Master-toggle fold check (when any hint promises "off = vanilla/pre-feature behavior"):** enumerate EVERY override in the feature's GameModel(s)/patch(es) — including constant-returning getters like `GetXxx()` — and confirm each read path folds the master toggle (directly or via a settings-provider getter that folds it). A single unconditionally-read config value breaks the promise. Why: CombatMechanics (2026-07-02) — `GetHorseChargePenetration()` returned the TUNED config value with the feature toggled off; every other read folded the master. Caught only because the agent enumerated all 9 overrides instead of the "interesting" ones.
   - **Why this rule is rigid:** the deep-review Agent 5 on the CrashReport feature (2026-05-25) cross-referenced 5 of 6 toggles but missed the master toggle (`EnableCrashCapture`); Codex caught it as HIGH. The agent prompt previously listed toggles to check by name and missed the one not on the list. Generalised: enumerate from the class itself, not from a list in the prompt.

2c. **DTO non-empty-output trace (MANDATORY):** For every DTO collection field (`IReadOnlyList<T>`, `List<T>`, etc.) populated by a collector in the changed files, trace whether non-empty values are actually produced under normal operation — NOT just whether the populator runs. Specifically:
   - Find the populator method.
   - Trace every code path that adds items to the collection.
   - For each path, identify the precondition (parameter non-null, branch taken, etc.).
   - Check the caller(s): is the precondition actually satisfied by the caller in production?
   - If a collection field is structurally populated but the precondition is never met by any caller, the field is dead code (always empty) — flag as HIGH if the field appears in user-facing docs/CHANGELOG.
   - **Why this rule exists:** Codex review 41 (CrashReport, 2026-05-25) caught `HarmonyCorrelationCollector.Collect(stack, frames=null)` — the optional `frames` parameter controlled the per-stack-frame patch-info block; the sole caller skipped it; the renderer faithfully rendered empty lists in every report; "Harmony patches per stack frame" feature advertised in CHANGELOG was DEAD CODE. The 5 deep-review agents all passed and the test suite passed because no test covered the integration. Generalisation: "is the field populated?" is not the same as "are non-empty values actually produced?" — extend traces to ask the second question explicitly.

3. **Mutation/Transform Chain Completeness:** When data is transformed through a pipeline (raw → mutated → applied), verify every stage connects to the next.
   - Example bug pattern: Mutation service mutates `MaxCharge` on template, but `CareerAbility` reads `MaxCharge` from career definition (unmutated source).
   - Check: For every `property="X"` in mutation XML, trace X through the mutation service to where the mutated value is consumed.

4. **Parallel Method Consistency:** When multiple methods serve the same purpose (e.g., checking cost, applying cost, displaying cost), verify they ALL use the same calculation.
   - Example bug pattern: `CanAffordUpgrade` uses `baseCost * count` while `SpendForUpgrade` uses `GetEffectiveUpgradeCost()`.
   - Check: Find method families (CanAfford/Spend/Clamp/Display) and verify they share the same cost derivation.

4b. **Engine-Float Gate NaN Polarity (MANDATORY for every decision gate on an ENGINE-sourced float):** For every comparison in the changeset that gates behavior on a float the ENGINE hands in at runtime (momentum, velocity, damage, resistance, health, distance — NOT config floats, which the FiniteFloatValidator rule covers at load), check the comparison's polarity against NaN. All NaN comparisons return false, so:
   - An inverted early-exit (`if (x <= 0f) return;` / `if (x < min || x > max) reject;`) lets NaN PASS into the active branch — flag it. The gate must be a positive requirement (`x > 0f` required to proceed / `!(x > 0f)` to reject) or an explicit `float.IsNaN` check.
   - For owned-verdict services (`bool?` fall-through patterns), a NaN input must produce `null` (defer to vanilla), never an owned true/false computed from garbage — trace what each formula emits when an input is NaN.
   - **float→int CASTS feeding an integer guard.** For every `(int)<float>` cast in or near the changed code, ask what int it produces for NaN/±Inf and whether that value defeats a downstream guard. `(int)float.NaN` and `(int)float.PositiveInfinity` are BOTH `int.MinValue` on net472/x64, and `int.MinValue - 1` underflows (unchecked) to `int.MaxValue` — so a guard like `int head = v - 1; if (head <= 0) return 0;` silently passes a poisoned value as the largest possible budget. Require finiteness AT the cast and gate the value itself, not arithmetic derived from it. **Check every float→decision path in a touched method, not only the added lines** — the 2026-07-17 instance sat two lines above a correctly-gated new one, in the same method.
   - **Why this rule exists:** 4th instance of the NaN-gate class (Career cooldown #31, EditorCacheRebuild #38, CS_Road 2026-05-13, CombatMechanics 2026-07-02 — `momentumRemaining <= 0f` passed NaN and could force SlicedThrough chains; a NaN charge velocity became an owned `false` suppressing vanilla knockdowns). The first three were CONFIG floats and produced the loader-side rule; this instance proved the ENGINE-input side had no rule and no agent prompt asking the question. See `.claude/rules/csharp-architecture.md` "Engine-Float Decision Gates" + `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

5. **Lifecycle Completeness (State Matrix):** For every "set" operation, verify there is a corresponding "clear" for ALL entity lifecycle states.
   - Entity states to check: alive, killed, unconscious, removed, mission-end, screen-close, session-end.
   - Example bug pattern: `CareerAbilityBuffTracker.SetBuff()` on activation, but `ClearBuff()` only on timeout — not on hero death.
   - Check: For every static dictionary, cached field, or session-scoped state, trace all paths that clear it.

5b. **Observation State Machines (BOUNDARY ENUMERATION):** For every static field that participates in polling or change-detection of EXTERNAL state (engine counts, file sizes, network responses, MBObjectManager queries), enumerate ALL four boundary states and classify every transition between adjacent states.
   - **Why this is separate from rule 5:** Lifecycle matrix asks *"when does this entity die?"* Observation matrix asks *"what values can this poll return, in what order, and which transitions mean what?"* Both are needed for state machines driven by external polling. Rule 5 alone is insufficient (RCA: shader-precompilation initial-zero latch, 2026-05-04).
   - **Boundary states to enumerate:**
     1. **Sentinel / uninitialized** — value set by reset/init (often `-1`, `null`, `default(T)`)
     2. **First real observation** — what the poll returns BEFORE any work has happened (often `0`, `false`, empty collection)
     3. **In-progress values** — the range during normal operation
     4. **Terminal value** — the value indicating completion (often `0`, `null`, `false`)
   - **Critical: sentinel-to-first-observation collision check.** If the sentinel value (state 1) is distinguishable from the terminal value (state 4) ONLY because state 1 has a different sentinel encoding, the change-detection logic must verify it observed at least one in-progress value (state 3) before treating a return-to-terminal as completion. A separate boolean flag (`_hasObservedWork`) is the standard fix.
   - **Example bug pattern (RCA shader-precompilation):** `_lastShaderCount = -1` (sentinel) → first frame after reset, engine returns `count = 0` (first observation, but the engine hasn't started compiling yet) → patch enters "completion" branch, calls `ResetShaderBattleActive()` → patch is dead before any real work arrives.
   - **Example bug pattern (general):** A polling loop initialised to `_lastSize = -1`, polling a file size. First poll returns `0` because the file isn't created yet. Loop fires the "file shrank to zero / vanished" branch and exits. File then grows to real size; loop is gone.
   - **Check (apply for every static state field that participates in polling):**
     - Find the field's reset/init location. What value does it start at? (state 1)
     - Find the polling source. What's the lowest possible value the poll can return? (often `0`, distinct from sentinel only by sentinel encoding) — this is state 2.
     - Walk the change-detection logic for the transition state-1 → state-2. Does it incorrectly classify this as a state-3 → state-4 (completion) transition?
     - Walk the same logic for state-3 → state-4. Confirm it IS classified as completion.
     - If both transitions fire the same code path, that's a sentinel collision — flag it. The fix is a `_hasObservedWork`-style flag that distinguishes "we're past the sentinel" from "we're at the terminal."

5c. **GameModel Cross-Entity Propagation (MANDATORY when a `Taom*Model` override returns a per-entity capability/value).** A `GameModel` override that returns a per-entity value (per `MobileParty`, per `Hero`, per `Settlement`) is NOT a per-entity-isolated decision if the engine PROPAGATES that value to related entities or RECOMPUTES it per-entity across a group. A naive gate (e.g. keying only on `IsMainParty`) desyncs the group. The per-file lenses and TAOM-internal flow tracing structurally CANNOT catch this — you must open the **engine consumer** of the override result.
   - **Check (apply for every `GameModel` override method in the changeset that returns a per-entity bool/value):**
     - Decompile the engine property/method that calls `Campaign.Current.Models.<Model>.<Method>(entity)` (installed DLLs). Grep it for the value being pushed onto attached/child collections (`_attachedParties`, `BoundVillages`, family/companions) and per-entity recompute getters (a `NavigationCapability`-style getter the engine drives across the group).
     - If the value propagates or recomputes per-entity, confirm the override MIRRORS the engine's inheritance (e.g. an attached party inherits its army leader's capability). If the override ignores attached/child entities, flag as HIGH.
     - Confirm LIFECYCLE: an entity already mid-transition must retain capability to complete/exit (a party already at sea keeps naval capability to reach land regardless of toggles — gates govern only NEW transitions). A disabled/gated path that strips capability from an in-transition entity is a soft-lock — flag as MED.
     - If the override is a port of a donor model (vanilla/DLC), DIFF the donor's same method and confirm no behavioral limb was dropped when the override changed one limb.
   - **Why this rule exists:** Codex review 62 (NavalTravel #296, 2026-06-24). `TaomPartyNavigationModel.HasNavalNavigationCapability` keyed only on `IsMainParty`; the engine force-propagates `MobileParty.IsCurrentlyAtSea` down the army attachment tree (`MobileParty.cs:493-496`) and recomputes `NavigationCapability` per party (`:464-479`), so with `ApplyToAi=false` a player-led army's attached AI parties were dragged to sea with `Default`-only nav → stranded. The data-flow agent traced TAOM-internal config flow and even reasoned the ungated terrain methods "harmless," but never opened `MobileParty` to see the cross-party propagation. All 5 deep-review agents (across two passes) missed it; Codex caught it by decompiling `MobileParty`. Memory: `feedback_gamemodel_capability_engine_propagation`; RCA: `docs/reviews/rca-navaltravel-2026-06-24.md`.

5d. **Latch Closer Coverage + Toggle Gating (MANDATORY for any window/latch flag opened in one hook and closed in others — `_windowActive`, `_inflight`, static loading-window latches).** Three checks, all from one shipped changeset (tournament-exit diagnostics, 2026-07-06; RCA `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`):
   - **Closer per opener path:** enumerate every code path that OPENS the latch and verify a closer exists on EACH (or the opener is gated to the paths the closers cover, e.g. `Campaign.Current != null`). An any-mission opener with campaign-only closers leaks the latch — flag as MED+.
   - **Toggles gate I/O, never state transitions:** any `if (!IsEnabled) return;` ABOVE a `_latch = false` (or equivalent state write) means a mid-window toggle-off latches the flag — flag it. Required shape: state transition first (unconditional), then the toggle gate, then logging.
   - **Outermost-gate verification:** for every method whose state transition is (or was made) unconditional, grep ALL CALLERS for `IsEnabled`-style early-outs that re-condition it. Service-layer tests are structurally blind to hook-level gates — the Codex pass caught exactly this bypass one review after the service-layer fix shipped. Do not mark a toggle-gating finding fixed until every call path passes the state transition through.
   - Rule file: `.claude/rules/harmony-patches.md` "Latches & Toggle Gates"; master record: LESSONS-LEARNED "State, Lifecycle & Save" → "Diagnostics latches".

5e. **Agent-handle identity and thread placement (MANDATORY for every mission-time changeset).** Two questions per file, both answered from the decompile, not from the method name (#592, #595; `docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`):
   - **Who holds an `Agent`, `IAgentAdapter` or `Agent.Index` past the frame it was obtained in?** List every field, dictionary key, closure capture and shadow list. For each: is it evicted in `OnAgentDeleted` (the engine recycles the index from there), and is every use gated by `AgentSlotIdentity.IsCurrentOccupant` rather than the engine's `IsActive()`, which answers for the slot's new tenant? A store that is "cleared at mission end" only is a finding.
   - **Which thread runs this code?** `MissionBehavior.OnMissionTick` and the engine's agent callbacks are main-thread; `AgentComponent.OnTick`/`OnTickParallel`, `TickAsAI`, `Team.Tick`, `TeamAI`, AI-issued `Formation.SetMovementOrder`, `Formation.GetOrderPositionOfUnit` for AI units, and `AfterAsyncTickTick` are not (`.claude/rules/harmony-patches.md` "Which thread runs your target"). Anything off the main thread that registers a blow, plays an action, spawns or fades an agent, or touches a collection a main-thread callback writes is HIGH. A team filter is not a thread filter.

6. **Event Hook Coverage:** For behaviors that register campaign/mission events, verify all relevant events are hooked.
   - Example bug pattern: `OnAgentRemoved` emits kill charges but no hook exists for damage-dealt charges, even though most careers use `DamageDone` charge type.
   - Check: Read the behavior's RegisterEvents/constructor, cross-reference with the data it needs to provide.

7. **Sprite/Asset Reference Verification:** For every `Sprite="X"` in XML prefabs or `GetSprite("X")` in C#, trace X through `TAOMSpriteData.xml` to verify the sprite ID is registered and matches the PNG filename in `SpriteParts/`.
   - Example bug pattern: Code writes `Sprite="TAOM\\CareerSystem\\career_button_placeholder"` but `TAOMSpriteData.xml` registers it as `CareerSystem\career_button_placeholder` (no module prefix). Silent failure — sprite just doesn't render.
   - Check: Read TAOMSpriteData.xml, extract all `<Name>` entries, cross-reference every `Sprite=` attribute in changed prefab XML and every `GetSprite(` call in changed C#.

8. **Vanilla Interaction Safety:** For every UIExtenderEx `PrefabExtension` that injects into a vanilla prefab, check whether vanilla code makes assumptions about the target container's children (hardcoded indices, typed casts, count-based iteration).
   - Example bug pattern: Adding items to `SecondaryInfoItems` collection — vanilla `HandlePanelSwitchingInput` indexes by hardcoded position, causing `IndexOutOfRangeException`.
   - Example bug pattern: Appending a non-template child to a data-bound `ListPanel` — vanilla teardown may cast all children to the template type.
   - Check: For each `PrefabExtension`, identify the target widget, then search decompiled vanilla code for how that widget's children are accessed. Flag any hardcoded indexing, typed iteration, or count assumptions.

8b. **Prefab Command Handler Trace (MANDATORY for every TAOM-authored prefab or PrefabExtension in scope, changed or not).** For every `Command.Click` / `Command.*` in a TAOM prefab XML (inline in a `PrefabExtensionInsertPatch`, a `SetAttribute` patch, or a file under `Main/_Module/GUI/PreFabs/`), resolve the handler name: is it a TAOM `[DataSourceMethod]` on a mixin or a TAOM VM method, or a VANILLA VM method? A TAOM control bound straight to a vanilla method inherits vanilla's semantics and applies none of the mod's state, however the tooltip reads. Then trace the TAOM handler to the mod-owned write it exists for (a service call that reaches an engine property), and confirm the handler's parameter count equals the prefab's `CommandParameter` count (`ViewModel.ExecuteCommand` silently skips a mismatch). Flag a vanilla-bound TAOM control as HIGH, and flag any code comment describing a shipped control as a "known limitation" as an unfiled bug.
   - **Why this rule exists:** #574 (2026-09-12). The map bar Extra Fast Forward button bound vanilla `ExecuteTimeControlChange(2)` (mode only) from the day it shipped; `Campaign.SpeedUpMultiplier` was never written by a click, so the MCM slider did nothing. Audit #168 found it, chose "Option A: redundant with vanilla", and left a comment; the keybind refactor's five agents and Codex pass reviewed their own diff and never opened the prefab. RCA: `docs/reviews/rca-time-acceleration-button-2026-09-12.md`.

9. **Harmony Patch Category Registration (MANDATORY: fires for every `[HarmonyPatch]` class in scope).** TAOM uses category-based Harmony patching exclusively. `Harmony.PatchAll()` is NEVER called. A patch class with only `[HarmonyPatch(...)]` and no `[HarmonyPatchCategory(...)]`, OR with both attributes but no matching `TryPatchCategory("CategoryName")` call in `Main/SubModule.cs`, is silently dead code (no error, no warning, the patch simply never engages at runtime). The failure mode is invisible: the feature ships, tests pass, but the Harmony patch does nothing.
   - **Check (apply for every changed file under `Main/` containing `[HarmonyPatch]`; a test-assembly probe in `TAOM.Tests` is applied by its own test, never by SubModule):**
     - Grep the changed file for `[HarmonyPatchCategory(`. If absent, flag as HIGH.
     - Grep `Main/SubModule.cs` for `TryPatchCategory("<category-name>")` (a category applied in a loop, such as the Patch61 and Patch89 sub-categories or the character-preview batch, appears as a string literal in that loop's array). If absent, flag as HIGH. A direct `_harmony.PatchCategory(` call is itself a finding: `PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails on it.
     - The category name in `[HarmonyPatchCategory]` and the string in `TryPatchCategory(...)` must match exactly (case-sensitive).
     - **APPLY-TIMING sub-check (registered ≠ applied in time):** find WHICH `SubModule` lifecycle method the `TryPatchCategory(...)` call lives in, and confirm it runs BEFORE the patched target can first render. TAOM's late batch (`OnGameInitializationFinished`, gated `_gameInitPatchesApplied`) fires on CAMPAIGN INIT, correct only for in-game / character-creation screens. If the patched type is rendered on a **main-menu / pre-campaign screen** (Save/Load, main menu, launcher; decompile to find the instantiator, e.g. `BasicCharacterTableau` is built only by `SaveLoadHeroTableauTextureProvider` on the cold Load Game screen), the category MUST be applied in `OnSubModuleLoad` or `OnBeforeInitialModuleScreenSetAsRoot` (process-static one-shot), NOT the late batch, else the prefix attaches too late and the guarded crash stays live. Flag as HIGH/CRITICAL. Do NOT accept "the category is registered" as sufficient.
   - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — `Patch39_BanditPartySize` had `[HarmonyPatch]` but no `[HarmonyPatchCategory]`. The apply-timing sub-check was added after issue #299 (2026-06-24): the Save/Load CTD guard reused `Patch2_RefreshTableau` (applied at campaign-init) to protect a cold-menu screen; all 5 agents passed (the Data Flow agent's init-trace conflated "after module load" with "after game-init"), Codex caught it CRITICAL. See `docs/reviews/rca-savetableau-2026-06-24.md`. The 5 deep-review agents all missed it because none of them grepped SubModule.cs for the registration. Result: the postfix scaling bandit party troop counts would have been completely dead in production. Memory: `feedback_harmony_patch_category_registration_verification.md`.
   - **Reference template (correct pattern from `Patch38_SettlementNameplateFade`):**
     ```csharp
     [HarmonyPatch(typeof(SettlementNameplateWidget), "DetermineTargetAlphaValue")]
     [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]   // ← REQUIRED
     public static class SettlementNameplateWidget_DetermineTargetAlphaValue_Patch
     ```
     ```csharp
     // In Main/SubModule.cs, alongside the other TryPatchCategory calls:
     TryPatchCategory("Patch38_SettlementNameplateFade");   // ← REQUIRED
     ```

10. **Cross-Module XML Data Dependency (MANDATORY — fires whenever modified XML in module A references entities defined in module B, both TAOM-managed).** TAOM ships multiple modules: `TAOM` (Main), `TAOM_Map`, `TAOM.Dependencies`, alias stubs. When module A's XML attribute references an entity defined in module B (`culture="Culture.X"` where X is in B; `troop="NPCCharacter.Y"`; `default_party_template="PartyTemplate.Z"`; settlement IDs; item IDs; etc.), module A's `SubModule.xml` MUST declare `<DependedModule Id="B"/>` AND `<DependedModuleMetadata id="B" order="LoadBeforeThis"/>`. The Bannerlord launcher does NOT infer load-order from XML cross-references — it reads only the `<DependedModules>` declaration.
    - **Check (apply when ANY modified XML lives in or references a TAOM-controlled module's `ModuleData/`):**
      - For each modified XML file in module A, grep its `culture=`, `troop=`, `*_party_template=`, settlement-id, and item-id references.
      - For each reference, identify the producing module (run `grep -l '<Culture\s\+id="X"' Modules/*/ModuleData/*.xml`).
      - If the producer is a different TAOM-managed module, open `A/SubModule.xml` and verify `<DependedModule Id="<producer>"/>` is present. If absent, flag as HIGH.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — 5 LOTR bandit cultures defined in TAOM Main's `taom_spcultures.xml`, then 99 hideouts in `TAOM_Map/settlements.xml` rewritten to reference them. `TAOM_Map/SubModule.xml` had no `<DependedModule Id="TAOM"/>`. Load-order was accidental. Memory: `feedback_cross_module_data_dependency_declaration.md`.
    - **Note:** External modules (Native, SandBoxCore, Sandbox, CustomBattle, StoryMode) have stable well-known load order and don't need this check applied to them. Apply only to TAOM-controlled modules.

11. **XML Parse Smoke Test (MANDATORY — fires for every modified ModuleData XML file).** Every new or modified XML file under any TAOM-managed module's `ModuleData/` must parse cleanly via PowerShell `[xml]$x = Get-Content -Raw <file>`. XML spec edge cases that pass eyeball review but reject at parse time:
    - `--` (double-hyphen) inside an XML comment body — XML spec forbids; engine rejects file.
    - Unescaped `&`, `<`, `>` in attribute values.
    - Mismatched element tags (`<Foo>...</Bar>`).
    - Duplicate attribute on same element.
    - Stray BOM in non-root position from copy-paste from concatenated files.
    - **Check (apply for every modified file in changeset matching `*.xml` under `ModuleData/`):**
      ```bash
      pwsh -Command '[xml]$x = Get-Content -Raw "<file path>"; "<file>: OK"'
      ```
      If any modified XML throws at parse, flag as CRITICAL — engine WILL reject the file at load.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — `taom_partyTemplates.xml` had `--` inside a comment body. 5 deep-review Claude agents read the XML semantically but none parsed it. Engine would have rejected the file silently at load. Memory: `feedback_xml_parser_smoke_test_before_commit.md`.

12. **Bandit Culture / Clan Pair Coverage (fires when any `<Culture is_bandit="true">` row is added in the changeset).** A `Culture.is_bandit="true"` row alone does NOT create a bandit clan in vanilla — `Hideout.MapFaction` resolves via `clan.IsBanditFaction`, which is loaded from `<Faction is_bandit="true">` rows in `spclans`/`taom_spclans`/`characters/clans.xml`. Every new bandit culture needs a matching bandit clan row.
    - **Check (apply when any added/modified culture has `is_bandit="true"`):**
      - For each such culture, grep `Main/_Module/ModuleData/characters/clans.xml` (and any other `spclans*.xml` in scope) for a `<Faction>` row where `is_bandit="true"` AND `culture="Culture.<the-new-culture>"`.
      - If absent, flag as HIGH. Hideouts referencing the culture will have unresolvable `MapFaction`, and `BanditSpawnCampaignBehavior` may NRE on spawn.
      - The bandit clan row must also specify `initial_home_settlement` pointing at a real settlement of the right type (typically a hideout), and `default_party_template` pointing at a real party template.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — 5 new bandit cultures authored without matching clan rows. The reasoning at design time was "the engine will auto-create bandit clans from `is_bandit` cultures" — vanilla does not do this. Memory cross-link: `feedback_classify_by_grep_not_by_assumption.md` (sibling pattern).

OUTPUT FORMAT:
For each trace:
- DATA FLOW: [source] → [transform] → [consumer]
- STATUS: ✅ CONNECTED / ❌ GAP FOUND / ⚠️ INCONSISTENT
- If GAP/INCONSISTENT: describe exactly what's missing and which files are involved

Summary: N flows traced, X gaps found, Y inconsistencies found
