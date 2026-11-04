# Agent 5 lens: Cross-System Data Flow Tracing

CROSS-SYSTEM DATA FLOW REVIEW — trace data declarations through the codebase to find gaps where declared data is never consumed, or where parallel code paths use inconsistent logic.

FILES: the list in your spawn prompt.

This review exists because per-file reviews consistently miss bugs that span multiple files. Every check below has caught real bugs in this project; each names the record that holds its history.

HARNESS CHANGES: when `.claude/**`, `CLAUDE.md`, `AGENTS.md`, `.ai/**` or a workflow doc is in scope, the data is the contract itself: agent and lens names, step numbers, trigger rules, tool names, flags and exit codes, each declared in one file and consumed in others. Trace every changed declaration to all of its consumers (grep the whole repo, not only the scope), including a hook that reads another hook's output format.

TRACE THESE DATA FLOWS:

1. **XML Config → C# Consumption:** For every configurable value declared in XML (ModuleData/**/*.xml), trace it to the C# code that reads and acts on it. Flag any XML attribute that is parsed but never used at runtime.
   - Read ALL changed XML files. For each attribute/element, grep the C# codebase for where it's consumed.
   - Example bug pattern: XML declares `charge_type="DamageDone"` but the only code that emits charges uses `ChargeType.Kills`.

2. **Enum Coverage:** For every enum type referenced in changed files, check that ALL enum values have at least one handler. Flag any enum value with zero callsites.
   - Example bug pattern: `PassiveEffectType` has 50 values but only 15 are wired into GameModels.

2b. **MCM toggle coverage (MANDATORY: applies to EVERY toggle, not a hand-listed subset):** For every MCM `AttributeGlobalSettings<>` / `AttributePerCampaignSettings<>` derived class in the changed files, enumerate EVERY property (excluding metadata: Id/DisplayName/FormatType/FolderName/etc.) from the class itself, never from a list in your prompt, and grep the entire feature's source for read sites. For each property:
   - ZERO read sites: a dead toggle: HIGH.
   - EXACTLY ONE read site at startup (`SubModule.OnSubModuleLoad`, `IoC.Configure`, a static initializer) while the hint text promises runtime behavior ("when off", "disables", "no-op", "stops"): the promise mismatches the implementation: HIGH.
   - Runtime read sites: verify each read actually gates behavior; a read-but-discard (logged but not branched on) is the same as dead.
   - **Master-toggle fold check (when any hint promises "off = vanilla/pre-feature behavior"):** enumerate EVERY override in the feature's GameModel(s)/patch(es), constant-returning getters included, and confirm each read path folds the master toggle (directly or via a settings-provider getter that folds it). One unconditionally read config value breaks the promise.
   - Source: CrashReport master toggle (2026-05-25, `docs/reviews/agents-md-review-lessons-archive.md`); CombatMechanics `GetHorseChargePenetration` (`docs/reviews/rca-combat-mechanics-2026-07-02.md`).

2c. **DTO non-empty-output trace (MANDATORY):** For every DTO collection field (`IReadOnlyList<T>`, `List<T>`, etc.) populated by a collector in the changed files, trace whether non-empty values are actually produced under normal operation, not just whether the populator runs:
   - Find the populator, trace every code path that adds items, and name each path's precondition (parameter non-null, branch taken).
   - Check the caller(s): is the precondition satisfied in production? A field that is structurally populated but whose precondition no caller meets is always empty: dead code, HIGH if the field appears in user-facing docs or the CHANGELOG.
   - Source: `HarmonyCorrelationCollector.Collect(stack, frames=null)` (Codex review 41, `docs/reviews/lessons/harmony-il.md`).

3. **Mutation/Transform Chain Completeness:** When data is transformed through a pipeline (raw → mutated → applied), verify every stage connects to the next.
   - Example bug pattern: Mutation service mutates `MaxCharge` on template, but `CareerAbility` reads `MaxCharge` from career definition (unmutated source).
   - Check: For every `property="X"` in mutation XML, trace X through the mutation service to where the mutated value is consumed.

4. **Parallel Method Consistency:** When multiple methods serve the same purpose (e.g., checking cost, applying cost, displaying cost), verify they ALL use the same calculation.
   - Example bug pattern: `CanAffordUpgrade` uses `baseCost * count` while `SpendForUpgrade` uses `GetEffectiveUpgradeCost()`.
   - Check: Find method families (CanAfford/Spend/Clamp/Display) and verify they share the same cost derivation.

4b. **Engine-Float Gate NaN Polarity (MANDATORY for every decision gate on an ENGINE-sourced float):** For every comparison that gates behavior on a float the ENGINE hands in at runtime (momentum, velocity, damage, resistance, health, distance; config floats are covered at load by the FiniteFloatValidator rule), check the comparison's polarity against NaN. All NaN comparisons return false, so:
   - An inverted early-exit (`if (x <= 0f) return;` / `if (x < min || x > max) reject;`) lets NaN PASS into the active branch: flag it. The gate must be a positive requirement (`x > 0f` to proceed / `!(x > 0f)` to reject) or an explicit `float.IsNaN` check.
   - For owned-verdict services (`bool?` fall-through), a NaN input must produce `null` (defer to vanilla), never an owned true/false computed from garbage.
   - **float→int casts feeding an integer guard:** `(int)float.NaN` and `(int)float.PositiveInfinity` are both `int.MinValue` on net472/x64, and `int.MinValue - 1` underflows to `int.MaxValue`, so `int head = v - 1; if (head <= 0) return 0;` passes a poisoned value as the largest budget. Require finiteness AT the cast and gate the value itself. **Check every float→decision path in a touched method, not only the added lines.**
   - Rule: `.claude/rules/csharp-architecture.md` "Engine-Float Decision Gates". Source: `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

5. **Lifecycle Completeness (State Matrix):** For every "set" operation, verify there is a corresponding "clear" for ALL entity lifecycle states.
   - Entity states to check: alive, killed, unconscious, removed, mission-end, screen-close, session-end.
   - Example bug pattern: `CareerAbilityBuffTracker.SetBuff()` on activation, but `ClearBuff()` only on timeout — not on hero death.
   - Check: For every static dictionary, cached field, or session-scoped state, trace all paths that clear it.

5b. **Observation State Machines (BOUNDARY ENUMERATION):** For every static field that polls or change-detects EXTERNAL state (engine counts, file sizes, network responses, MBObjectManager queries), enumerate the four boundary states and classify every transition between them. Rule 5 asks "when does this entity die?"; this asks "what values can the poll return, in what order, and which transitions mean what?"
   - **States:** (1) sentinel or uninitialized (`-1`, `null`, `default(T)`); (2) first real observation, before any work (often `0`, `false`, empty); (3) in-progress values; (4) terminal value (often `0`, `null`, `false`).
   - **Sentinel-to-first-observation collision:** walk the logic for 1 → 2 and for 3 → 4. If both fire the same completion branch, flag it; the fix is a `_hasObservedWork`-style flag that is set only after a state-3 value was seen.
   - Example bug pattern: `_lastShaderCount = -1`, the first poll after reset returns `0` before compiling starts, and the patch takes the completion branch and stops before any real work. Source: shader-precompilation latch (`docs/reviews/lessons/build-tooling-workflow.md`).

5c. **GameModel Cross-Entity Propagation (MANDATORY when a `Taom*Model` override returns a per-entity capability/value).** A per-entity value (per `MobileParty`, `Hero`, `Settlement`) is not an isolated decision if the engine PROPAGATES it to related entities or RECOMPUTES it across a group, and only the **engine consumer** shows that.
   - Decompile the engine property/method that calls `Campaign.Current.Models.<Model>.<Method>(entity)` (installed DLLs). Grep it for the value being pushed onto attached/child collections (`_attachedParties`, `BoundVillages`, family/companions) and for per-entity recompute getters the engine drives across the group.
   - If the value propagates or recomputes per entity, confirm the override MIRRORS the engine's inheritance (an attached party inherits its army leader's capability). An override that ignores attached/child entities is HIGH.
   - LIFECYCLE: an entity already mid-transition must keep the capability to complete or exit; gates govern only NEW transitions. Stripping it from an in-transition entity is a soft-lock: MED.
   - For a port of a donor model (vanilla/DLC), DIFF the donor's same method and confirm no limb was dropped.
   - Source: `TaomPartyNavigationModel.HasNavalNavigationCapability` (`docs/reviews/rca-navaltravel-2026-06-24.md`).

5d. **Latch Closer Coverage + Toggle Gating (MANDATORY for any window/latch flag opened in one hook and closed in others: `_windowActive`, `_inflight`, static loading-window latches):**
   - **Closer per opener path:** every code path that OPENS the latch has a closer, or the opener is gated to the paths the closers cover (e.g. `Campaign.Current != null`). An any-mission opener with campaign-only closers leaks the latch: MED+.
   - **Toggles gate I/O, never state transitions:** an `if (!IsEnabled) return;` ABOVE a `_latch = false` latches the flag on a mid-window toggle-off. Required order: state transition (unconditional), then the toggle gate, then logging.
   - **Outermost-gate verification:** for every method whose state transition is unconditional, grep ALL CALLERS for `IsEnabled`-style early-outs that re-condition it; service-layer tests cannot see hook-level gates. A toggle-gating finding is fixed only when every call path passes the transition through.
   - Rule: `.claude/rules/harmony-patches.md` "Latches & Toggle Gates"; master record: `docs/reviews/LESSONS-LEARNED.md` "State, Lifecycle & Save" (diagnostics latches). Source: `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`.

5e. **Agent-handle identity and thread placement (MANDATORY for every mission-time changeset).** Two questions per file, both answered from the decompile, not from the method name:
   - **Who holds an `Agent`, `IAgentAdapter` or `Agent.Index` past the frame it was obtained in?** List every field, dictionary key, closure capture and shadow list. Each must be evicted in `OnAgentDeleted` (the engine recycles the index from there), and every use gated by `AgentSlotIdentity.IsCurrentOccupant`, not the engine's `IsActive()`, which answers for the slot's new tenant. A store "cleared at mission end" only is a finding.
   - **Which thread runs this code?** `MissionBehavior.OnMissionTick` and the engine's agent callbacks are main-thread; `AgentComponent.OnTick`/`OnTickParallel`, `TickAsAI`, `Team.Tick`, `TeamAI`, AI-issued `Formation.SetMovementOrder`, `Formation.GetOrderPositionOfUnit` for AI units, and `AfterAsyncTickTick` are not (`.claude/rules/harmony-patches.md` "Which thread runs your target"). Off-main-thread code that registers a blow, plays an action, spawns or fades an agent, or touches a collection a main-thread callback writes is HIGH. A team filter is not a thread filter.
   - Source: #592, #595 (`docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`).

5f. **Safety Claims and Missing Guards (MANDATORY for every new or changed patch, handler, menu callback or event subscriber).**
   - **A claim is not evidence:** a comment or name that asserts safety ("validated upstream", "never null here", "only called on the main thread", a `Safe...` wrapper) is a claim. Verify the invariant in the visible code or the engine source (rule 5e for threads); a wrapper guards only the paths that go through it.
   - **Compare with siblings:** read the sibling handlers in the same file or feature. When they check a guard (`IsMainThread`, hero alive, settlement not null, co-op authority, `IsEnabled`) and the new one does not, the missing check is the finding. This is the same gap as the entity state matrix in rule 5: an `OnGameLoaded` mutation that skips a state its neighbours guard.

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

8b. **Prefab Command Handler Trace (MANDATORY for every TAOM-authored prefab or PrefabExtension in scope, changed or not).** For every `Command.Click` / `Command.*` in a TAOM prefab XML (inline in a `PrefabExtensionInsertPatch`, a `SetAttribute` patch, or a file under `Main/_Module/GUI/PreFabs/`), resolve the handler: a TAOM `[DataSourceMethod]` on a mixin or a TAOM VM method, or a VANILLA VM method? A TAOM control bound straight to a vanilla method inherits vanilla's semantics and applies none of the mod's state. Trace the TAOM handler to the mod-owned write it exists for, and confirm its parameter count equals the prefab's `CommandParameter` count (`ViewModel.ExecuteCommand` silently skips a mismatch). A vanilla-bound TAOM control is HIGH; a code comment calling a shipped control a "known limitation" is an unfiled bug.
   - Source: #574 (`docs/reviews/rca-time-acceleration-button-2026-09-12.md`).

9. **Harmony Patch Category Registration (MANDATORY: fires for every `[HarmonyPatch]` class in scope).** TAOM patches by category only; `Harmony.PatchAll()` is never called. A class with `[HarmonyPatch(...)]` but no `[HarmonyPatchCategory(...)]`, or with both but no matching `TryPatchCategory("CategoryName")` in `Main/SubModule.cs`, never engages, silently.
   - **Check (every changed file under `Main/` or `Client/` containing `[HarmonyPatch]`; a test-assembly probe in `TAOM.Tests` is applied by its own test):**
     - Grep the file for `[HarmonyPatchCategory(`. Absent: HIGH.
     - Grep `Main/SubModule.cs` for `TryPatchCategory("<category-name>")` (a category applied in a loop, such as the Patch61 and Patch89 sub-categories or the character-preview batch, appears as a string literal in that loop's array). Absent: HIGH. A direct `_harmony.PatchCategory(` call is itself a finding: `PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails on it.
     - The two names match exactly (case-sensitive).
     - **APPLY-TIMING sub-check (registered ≠ applied in time):** find WHICH `SubModule` lifecycle method holds the `TryPatchCategory(...)` call and confirm it runs BEFORE the patched target can first render. The late batch (`OnGameInitializationFinished`, gated `_gameInitPatchesApplied`) fires on CAMPAIGN INIT, which is right only for in-game and character-creation screens. A type rendered on a **main-menu / pre-campaign screen** (Save/Load, main menu, launcher; decompile to find the instantiator, e.g. `BasicCharacterTableau` is built only by `SaveLoadHeroTableauTextureProvider` on the cold Load Game screen) needs its category applied in `OnSubModuleLoad` or `OnBeforeInitialModuleScreenSetAsRoot`: HIGH/CRITICAL otherwise. "The category is registered" is not sufficient.
   - Pattern to copy: `Patch38_SettlementNameplateFade` (its class attribute and its `TryPatchCategory` line). Source: `Patch39_BanditPartySize` and `Patch2_RefreshTableau` (`docs/reviews/lessons/harmony-il.md`; `docs/reviews/rca-savetableau-2026-06-24.md`).

10. **Cross-Module XML Data Dependency (MANDATORY: fires whenever modified XML in module A references entities defined in module B: `TAOM` (Main), `TAOM_Map`, `LOTRLOME_Armory`, `TAOM.Dependencies`, the alias stubs).** For each `culture=`, `troop=`, `*_party_template=`, settlement-id and item-id reference in modified XML, find the producing module and confirm the entity exists there: `python tools/validate_moduledata.py` resolves references across all three content modules (on the laptop, missing live installs are environment gaps).
    - **Do not flag a missing `<DependedModule>` row between TAOM content modules.** It is a standing decision (Mike, 2026-09-21): the Modding Kit force-loads every declared dependency, so no TAOM content module declares one. In single-player `<DependedModules>` does not order XML merging either; the launcher's saved module order does (`docs/modding/module-dependencies.md` "What `<DependedModules>` does in singleplayer"). The `TAOM.Dependencies` and official-module rows in `Main/_Module/SubModule.xml` are the #371 version pairing, not a load-order device.
    - Instead, check that the consumer survives the producer's absence: lookups by name with a logged error, not an unguarded dereference.
    - External modules (Native, SandBoxCore, Sandbox, CustomBattle, StoryMode) have a stable load order; skip them.

12. **Bandit Culture / Clan Pair Coverage (fires when any `<Culture is_bandit="true">` row is added in the changeset).** A `Culture is_bandit="true"` row does NOT create a bandit clan: `Hideout.MapFaction` resolves via `clan.IsBanditFaction`, loaded from `<Faction is_bandit="true">` rows in `spclans`/`taom_spclans`/`characters/clans.xml`.
    - For each such culture, grep `Main/_Module/ModuleData/characters/clans.xml` (and any other `spclans*.xml` in scope) for a `<Faction>` row with `is_bandit="true"` AND `culture="Culture.<the-new-culture>"`. Absent: HIGH (hideouts get an unresolvable `MapFaction`, and `BanditSpawnCampaignBehavior` may NRE on spawn).
    - The clan row also needs `initial_home_settlement` on a real settlement of the right type (typically a hideout) and `default_party_template` on a real party template.
    - Source: the same Bandit Management review (`docs/reviews/codex-adversarial-bandit-management-2026-05-27.prompt.md`).

OUTPUT FORMAT:
For each trace:
- DATA FLOW: [source] → [transform] → [consumer]
- STATUS: ✅ CONNECTED / ❌ GAP FOUND / ⚠️ INCONSISTENT
- If GAP/INCONSISTENT: describe exactly what's missing and which files are involved

Summary: N flows traced, X gaps found, Y inconsistencies found
