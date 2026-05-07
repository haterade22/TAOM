# Codex Research Prompt: Special Resource System

> **Usage:** Copy the prompt below into a Codex session (or use `/codex:rescue` with this content).
> Compare Codex findings with Claude's research at `docs/research/tor-resource-system.md`.

---

## Prompt

You are researching how to implement a **per-kingdom special resource system** for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion mod for Bannerlord v1.3.15.

### Background

The Old Realms (TOR_Core) Warhammer mod has a working implementation of this exact concept. Their code is at:
- **Repo:** https://github.com/TheOldRealms/TOR_Core/tree/development
- **C# Source:** https://github.com/TheOldRealms/TOR_Core/tree/development/CSharpSourceCode

TOR implements culture-specific resources: "Teef" (orcs), "Oath Gold" (dwarves), "Winds of Magic" (spellcasters), "Prestige" (Empire), "Chivalry" (Bretonnia), "Forest Harmony" (wood elves), "Waaagh" meter (greenskins). These resources are required to recruit/upgrade elite troops, drain as daily upkeep, and are earned through battles, settlements, and faction-specific mechanics.

### Your Research Tasks

**Task 1: Analyze TOR's Implementation**

Clone the repo: `git clone --branch development https://github.com/TheOldRealms/TOR_Core /tmp/TOR_Core`

Examine these specific files:

1. **Resource Definition Layer** — Read these in order:
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResource.cs` (data model)
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResourceManager.cs` (singleton orchestrator, 10 resources registered, 7 CampaignEvent subscriptions)
2. **Storage & Persistence** — How are resource values stored per-hero?
   - `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/HeroExtendedInfo.cs` ([SaveableField(2)] Dictionary<string, float>)
   - `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/ExtendedInfoManager.cs` (CampaignBehaviorBase managing per-hero info)
3. **Earning Mechanics** — How do resources accumulate?
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/TeefBehavior.cs` (Teef — closest to War Spoils)
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/OathGoldBehavior.cs`
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/PrestigeNobleTownBehavior.cs`
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/EonirFavorEnvoyTownBehavior.cs`
   - Helpers: `TeefHelper.cs`, `OathGoldHelper.cs`, `ChivalryHelper.cs`, `FavorHelper.cs`, `ForestHarmonyHelper.cs` (all in `CampaignMechanics/CustomResources/`)
4. **Spending Mechanics** — How are troop upgrades gated?
   - `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/CustomResourcePatches.cs` (4 Harmony patches on PartyCharacterVM, PartyVM, PartyScreenLogic)
5. **GameModel Overrides**:
   - `/tmp/TOR_Core/CSharpSourceCode/Models/TORCustomResourceModel.cs`
   - `/tmp/TOR_Core/CSharpSourceCode/Models/TORPartyWageModel.cs`
6. **UI/HUD Display** — How is the resource shown?
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghBehavior.cs`
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghMeterVM.cs`
   - `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/TORMapNotificationView.cs`
   - Also check `Extensions/UI/TORMapInfoVMExtension.cs` and `TORMapBarSpriteWidget.cs` if they exist
7. **Troop Cost Definitions** — How are per-troop costs defined in XML?
   - `/tmp/TOR_Core/ModuleData/tor_custom_xmls/tor_extendedunitproperties.xml` (ResourceCost elements with ResourceType, UpkeepCost, UpgradeCost)
   - `/tmp/TOR_Core/ModuleData/tor_custom_xmls/tor_config.xml` (MaximumCustomResourceValue=5000)
   - `/tmp/TOR_Core/CSharpSourceCode/SubModule.cs` (lines ~140-200 for registration order)

**Task 2: Analyze TAOM's Architecture for Integration**

Read TAOM's codebase to understand where this system would plug in:

1. **Existing Economy Models** — Read `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` and `Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs`. How do they work? What services do they depend on?
2. **IoC Pattern** — Read `Main/IoC.cs` and any feature IoC file (e.g., `Main/Features/Diplomacy/DiplomacyIoC.cs`). How are services registered?
3. **Save/Load Pattern** — Read `Main/Features/HeroRace/RacePersistenceService.cs` or any `CampaignBehaviorBase` with `SyncData`. How does TAOM persist custom data?
4. **Adapter Pattern** — Read `Main/Adapters/` directory. How do services avoid touching sealed TaleWorlds types?
5. **Config Loading** — Read `Main/Features/StartupResources/StartupResourcesConfigProvider.cs`. How does TAOM load XML config?
6. **UIExtenderEx Usage** — Search for `BaseViewModelMixin` or `ViewModelMixin` in the codebase. How does TAOM inject into vanilla UI?
7. **Cultural Feats** — Read `Main/Features/CulturalFeats/TaomCulturalFeats.cs`. How are per-culture bonuses implemented?
8. **Feature Module Pattern** — Pick any complete feature (e.g., `Main/Features/Arena/` or `Main/Features/Siege/`) and document the file structure, IoC registration, service-adapter boundary, and test patterns.

**Task 3: Design Recommendations**

Based on your analysis, propose:

1. **Feature module file structure** following TAOM conventions (IoC, service, adapter, behavior, model, hooks, tests)
2. **Pilot kingdom is Mordor with "War Spoils"** — orcs earning plunder from battles/raids/sieges. Validate this choice and propose detailed earning/spending mechanics.
3. **Key design decisions:**
   - Where to store resource definitions (XML? JSON? Hard-coded?)
   - How to define troop resource costs (extend troop XML? Separate sidecar?)
   - How to gate upgrades in the party screen (Harmony patch? ExplainedNumber manipulation? Both?)
   - How to display resources in the HUD (UIExtenderEx mixin pattern? Custom MapView?)
   - How to handle save/load (SyncData pattern with Dictionary?)
4. **What adapters are needed** — which sealed TaleWorlds types must be wrapped?
5. **What Harmony patches are needed** — target methods, prefix vs postfix, patch category name
6. **What GameModels need changes** — which existing TAOM models need modification vs new models needed
7. **Risks and gotchas** — what could go wrong? Save compatibility? Performance? UI conflicts?
8. **Testing strategy** — what test cases are critical? What's untestable (requires live game)?

### Output Format

Structure your response as:

```
## TOR Analysis
### Resource Definition
### Storage & Persistence
### Earning Mechanics
### Spending Mechanics
### GameModel Overrides
### UI/HUD
### Troop Cost Definitions

## TAOM Integration Analysis
### Existing Economy Models
### IoC Pattern
### Save/Load Pattern
### Adapter Pattern
### Config Loading
### UI Pattern
### Cultural Feats
### Feature Module Pattern

## Design Proposal
### File Structure
### Pilot Kingdom Recommendation
### Key Design Decisions
### Adapters Needed
### Harmony Patches Needed
### GameModel Changes
### Risks & Gotchas
### Testing Strategy

## Comparison Notes
[Flag anything where your recommendation differs from what you'd expect a separate reviewer to propose — highlight trade-offs]
```

### Critical API Verification

Before proposing Harmony patches, verify these methods exist in Bannerlord 1.3.15 by checking `E:\Decompiled_Bannerlord\`:
- `PartyCharacterVM.InitializeUpgrades` — method signature, parameters
- `PartyVM.TransferAllCharacters` — method signature, parameters
- `PartyVM.OnTransferTroop` — method signature, parameters
- `PartyScreenLogic.AddCommand` — method signature
- `PartyScreenLogic.PartyCommand.TotalNumber` — field or property? Public or private?
- All CampaignEvents: `OnMissionStartedEvent`, `OnPlayerBattleEndEvent`, `OnHideoutBattleCompletedEvent`, `HeroPrisonerReleased`, `TournamentFinished`, `HeroLevelledUp`, `OnIssueUpdatedEvent`

### Key Findings to Validate

Our preliminary analysis found:
- TOR resources are **player-only** — AI doesn't earn/spend. Confirm this is correct.
- TOR's `UpkeepCost` XML attribute exists but is **0 for all troops**. Confirm.
- TOR uses pure **Harmony patches** for party screen UI, **NOT UIExtenderEx**. Confirm.
- TOR's `PartyScreenLogic.PartyCommand.TotalNumber` is accessed via **reflection**. Confirm and assess fragility.
- Resources are capped at **5000** via `TORConfig.MaximumCustomResourceValue`. Confirm.

### Rules
- Follow TAOM's architecture: `[Entry Point] → Service → IAdapter` (ADR-007)
- Services NEVER touch sealed TaleWorlds types (Hero, Settlement, etc.)
- Entry points (patches, models, behaviors) must be <150 lines (ADR-002)
- TDD mandatory — design for testability
- Research TaleWorlds APIs in `E:\Decompiled_Bannerlord\` before assuming behavior
- Reference specific file paths and line numbers when possible
- Build the COMPLETE system (earning, spending, UI, save/load, all components) — do not defer subsystems to "phase 2"
