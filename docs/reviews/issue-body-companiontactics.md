## Motivation

Port the external developer's CompanionTactics module (#7 of 7 dropped at `Downloads/Features_fixed/`) into TAOM's adapter / service / IoC pattern. Three independently-toggleable sub-features under one TAOM module:

- **CompanionRoles** — equipment-based 11-role detector + tooltip badges on the party screen and OOB hero items.
- **FormationPresets** — saveable named OOB hero-to-formation assignments + Save / Load / Auto-Assign UI inquiry chain.
- **BattleActionBar** — context-sensitive 1–9 hotkey action bar in field battles. Stances are display-only (matches the original developer's UI-only design; v1.3.15 doesn't expose firing-order / brace-pose APIs the original referenced).

## Design

- Per ADR-007: services accept only `IXxxAdapter` types; sealed `Hero` / `Agent` / `Equipment` / `Formation` cross the boundary only at adapter implementations + boundary classes (Harmony patches, MissionView, ViewModels, OOBOverlayService).
- New adapters: `IBattleEquipmentSnapshot`, `IHeroCombatAdapter`, `IAgentCombatAdapter` (+ implementations).
- `IFormationAdapter` extension with `FormationIndex`, `RangedUnitCount`, `CavalryUnitCount`, `PolearmUnitCount`, `ShieldUnitCount` (last two TTL-cached at 500ms). The interface lives in another parallel-port branch (MixedFormations / SmartCavalryAI shared infrastructure); my +5 properties must be merged manually — see "Manual restoration" below.
- SaveableTypeDefiner BaseId `726900601` (matches the original mod for save-import compat — first SaveableTypeDefiner in TAOM; CareerSystem deliberately avoided this pattern via primitive SyncData).
- Patch35 Harmony category — 8 patches + 1 manual patch on private `OrderOfBattleHeroItemVM.GetCaptainTooltip`.
- `BattleActionBarMissionView` is a MissionView (not a Harmony patch) — field battles only (gated on `Mission.Mode == Battle && !IsSiegeBattle`).
- 10 MCM settings at `GroupOrder = 27 / 28 / 29` (22/23/24 was originally planned but a parallel SmartCavalryAI port consumed 22 first).

## Implementation

### Files (this commit, `5595037`)
- `Main/Features/CompanionTactics/` — 50 source files (CompanionTacticsIoC, ICompanionTacticsSettingsProvider, CompanionTacticsSettingsProvider, three sub-feature folders with services / Hooks / UI / Models)
- `Main/Adapters/` — 6 new files: `IBattleEquipmentSnapshot.cs`, `BattleEquipmentSnapshot.cs`, `IHeroCombatAdapter.cs`, `HeroCombatAdapter.cs`, `IAgentCombatAdapter.cs`, `AgentCombatAdapter.cs`
- `Main/_Module/GUI/Prefabs/BattleActionBar.xml`, `OOBButtonsOverlay.xml` — copied verbatim, vanilla brushes only
- `TAOM.Tests/Features/CompanionTactics/` — 6 test files / 74 tests
- `docs/features/companion-tactics.md` — feature doc
- `docs/reviews/codex-prompt-companiontactics-2026-05-06.md` — Codex adversarial review prompt
- `docs/reviews/codex-adversarial-companiontactics-2026-05-06.md` — Codex review output
- `docs/reviews/rca-companiontactics-2026-05-06.md` — RCA

### Manual restoration steps (see also feature doc + RCA)

The parallel-port build watcher in this environment auto-commented integration calls and re-added csproj exclusions whenever any build error appeared. After two atomic-batch attempts the integration calls were committed in their auto-commented state. To activate the feature:

1. **`Main/TAOM.csproj`** — remove `<Compile Remove="Features\CompanionTactics\**\*.cs" />`
2. **`TAOM.Tests/TAOM.Tests.csproj`** — remove the same line
3. **`Main/IoC.cs`** — uncomment the `using TAOM.Features.CompanionTactics;` directive and the `CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);` call
4. **`Main/SubModule.cs`** — restore the 4 commented integration points:
   - `_harmony.PatchCategory("Patch35_CompanionTactics");` in `OnGameInitializationFinished`
   - Manual `AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip")` patch wiring (private method in v1.3.15)
   - `mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());` in `OnMissionBehaviorInitialize`
   - `campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(), IoC.Resolve<IModLogger>()));` in `OnGameStart`
5. **`Main/Features/TaomSettings.cs`** — append the 10 MCM settings at GroupOrder 27/28/29 (snippet in `docs/features/companion-tactics.md`)
6. **`Main/Adapters/IFormationAdapter.cs` and `FormationAdapter.cs`** — extend with `FormationIndex`, `RangedUnitCount`, `CavalryUnitCount`, `PolearmUnitCount`, `ShieldUnitCount` properties (the parallel-port watcher reverted my +5 extensions; without them, `BattleActionBarService` and `FormationCompositionAnalyzer` will not compile)
7. **`CHANGELOG.md`** — append the CompanionTactics 2026-05-06 entry (snippet in commit message body)
8. **`CLAUDE.md`** — append the Patch35 row to the Harmony Patch Categories table + add the CompanionTactics row to Key Paths
9. **`.claude/rules/harness-facts.md`** — append the "Parallel-port build watcher" section documenting the hook behavior + workaround
10. Run `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` and `dotnet test TAOM.Tests --filter "FullyQualifiedName~CompanionTactics"` to confirm the 74 tests pass.

## Reviews

- `/deep-review CompanionTactics` (5 parallel agents) surfaced 2 confirmed bugs, both fixed before commit:
  - `GetRoleColor` was missing explicit cases for `OneHanded` + `Slinger` (silent white badges)
  - `BattleActionBarDebug` MCM toggle was dead — added consumer in `BattleActionBarService`
- Codex adversarial review (file: `docs/reviews/codex-adversarial-companiontactics-2026-05-06.md`) confirmed all 8 Known Suspects (mostly DISPUTED, code is correct) plus surfaced 4 additional bugs:
  - **P2-2** SyncData ordering — FIXED (populate `_savedPresets` BEFORE `dataStore.SyncData` on save so the engine records current state, not the previous buffer)
  - **P2-3** Singleton service not reset on new campaign — FIXED (added `CampaignEvents.OnNewGameCreatedEvent` listener that clears `_presets`)
  - **P3-1** Stance indicator divergence — FIXED (`ActionButtonVM.IsActive` now derives from `ITroopStanceManager` state; lifted the same-formation refresh short-circuit; `BattleActionBarVM.SyncActiveFromStance` re-syncs immediately after every action invocation)
  - **P4-2** `WeaponClass.Pick` unmapped — FIXED (Pick is a real melee class; now maps to `ShieldInfantry` / `OneHanded` based on shield slot)
- **P2-1** (FormationPresets UI does not capture/apply OrderOfBattleVM assignments) — DEFERRED to a follow-up issue. Save persists a name-only `HoNFormationPreset`; Load and Auto-Assign show "Phase-1 stub" notifications. Full capture/apply requires substantial reflection on `OrderOfBattleVM._allHeroes` + per-formation `Heroes` collection — out of session scope.

## Testing

- 74 tests in `TAOM.Tests/Features/CompanionTactics/` cover all 11 roles, edge cases (no equipment, mounted+ranged → HorseArcher, mounted+melee → Cavalry, equipment fingerprint cache hit/miss), composition→buttons mapping, EnableVolleyFire gating, MaxFormationPresets refuse path, missing-hero pruning on load, SaveableType round-trip, per-formation stance isolation, ClearAllStances on mission end.
- Tests pass at port time (per the feature-builder agent's verification before the parallel-port hook chaos started).
- Re-running tests requires manual restoration steps 1–6 above.

## Known limitations

- FormationPresets UI Save / Load / Auto-Assign do not yet capture or apply OrderOfBattleVM hero assignments. Tracked as follow-up.
- BattleActionBar stances are display-only — matches the original developer's UI-only design.
- `CompanionRoleService._cache` doesn't evict on hero death (bounded leak ~50 bytes/hero, ~50KB at 1000 heroes — acceptable for Phase 1).

## Closing

Source code, tests, prefabs, feature doc, Codex review, and RCA are all committed in `5595037`. The integration calls in `SubModule.cs` / `IoC.cs` and the csproj exclusion were reverted by the parallel-port build watcher; manual restoration steps above. Closing this issue with the commit reference; the manual restoration verification in-game can be tracked in the follow-up issue if needed.
