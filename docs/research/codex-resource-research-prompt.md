# Independent Research: TOR_Core Custom Resource System

## Objective
Reverse-engineer the custom resource system from TOR_Core (Warhammer Bannerlord mod) and assess how to port it to TAOM (LOTR Bannerlord mod). You are providing an independent second opinion — another agent is doing the same research separately.

## Source Repository
Clone: `git clone --branch development https://github.com/TheOldRealms/TOR_Core /tmp/TOR_Core`

## Primary Files to Analyze

### Core System (~5 files)
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResource.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResourceManager.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Models/TORCustomResourceModel.cs`
- `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/CustomResourcePatches.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Items/InventoryUseScripts/CustomResourceContainerScript.cs`

### Behaviors (~4 files)
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/TeefBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/OathGoldBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/PrestigeNobleTownBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/EonirFavorEnvoyTownBehavior.cs`

### Helpers (~5 files)
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/TeefHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/OathGoldHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/ChivalryHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/FavorHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/ForestHarmonyHelper.cs`

### Save/Load System (~2 files)
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/HeroExtendedInfo.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/ExtendedInfoManager.cs`

### UI (~4 files)
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghMeterVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/TORMapNotificationView.cs`

### Integration & Data
- `/tmp/TOR_Core/CSharpSourceCode/SubModule.cs` (registration lines ~140-200)
- `/tmp/TOR_Core/CSharpSourceCode/Models/TORPartyWageModel.cs`
- `/tmp/TOR_Core/ModuleData/tor_custom_xmls/tor_extendedunitproperties.xml` (ResourceCost per troop)
- `/tmp/TOR_Core/ModuleData/tor_custom_xmls/tor_config.xml` (MaximumCustomResourceValue)

## TAOM Context (for portability assessment)

TAOM is a LOTR total conversion mod targeting Bannerlord v1.3.15. Key constraints:
- **Adapter pattern mandatory**: Services use `IHeroAdapter`, `IClanAdapter` — never raw TaleWorlds sealed types
- **31 GameModel overrides** already registered (wages, upgrades, feats, etc.)
- **CulturalFeats system**: 16 culture feats modifying economic values via `FeatObject` + `ExplainedNumber`
- **IoC container**: DryIoc, all services constructor-injected
- **TDD mandatory**: Tests use MSTest + NSubstitute
- **Existing economic models**: `TaomPartyTroopUpgradeModel`, `TaomPartyWageModel`, `TaomClanFinanceModel`, `TaomBattleRewardModel`
- **16 cultures/factions** (Gondor, Rohan, Mordor, Isengard, Gundabad, Lothlorien, Rivendell, Mirkwood, Erebor, Umbar, Harad, Rhun, Dunland, Khand, Dale/Barding, Dol Guldur)
- **Pre-decompiled Bannerlord 1.3.15 source** available at `E:\Decompiled_Bannerlord\`

## Research Questions (answer ALL)

### A. Architecture
1. What is CustomResource's full class definition? (properties, methods, inheritance)
2. How does CustomResourceManager store per-party/per-hero resource balances? What is its lifecycle (init, runtime, teardown)?
3. What is the save/load mechanism? (trace from HeroExtendedInfo through ExtendedInfoManager SyncData)
4. What Bannerlord version does TOR target? (check SubModule.xml, csproj)

### B. Gameplay Loop
5. For Teef specifically: what events earn Teef? What spends it? What are the exact formulas with code snippets?
6. How do resources gate troop recruitment and upgrade? (trace the full Harmony patch chain on PartyCharacterVM.InitializeUpgrades)
7. Do AI parties earn and spend resources, or is it player-only? (search for MainHero checks vs generic hero checks)
8. Is there resource decay/upkeep, or only earn/spend? (check UpkeepCost in XML and daily tick handlers)
9. How does pending resource tracking work during party screen? (the _resourceChanges + _massBudget pattern)

### C. Integration Points
10. What Harmony patches intercept vanilla systems? (exact target class + method + patch type for each)
11. Which GameModel(s) does TORCustomResourceModel override? What base methods does it extend?
12. How does TORPartyWageModel incorporate custom resources alongside gold?
13. What CampaignEvents do the behaviors subscribe to? (complete list across all behavior classes)

### D. UI
14. How are resource balances displayed? (HUD overlay? party screen patch? tooltip injection?)
15. Does TOR use UIExtenderEx for resource UI integration? (search for ViewModelMixin, PrefabExtension attributes)
16. What Gauntlet prefabs exist for resource display? (search GUI/ and Prefabs/ directories)
17. How does WaaaghMeter work as a gameplay system, not just UI? (levels, thresholds, effects on morale/damage)

### E. Portability to TAOM
18. What TOR-specific systems is the resource system coupled to? (list every `using TOR_Core.*` in resource files and classify each as: portable, needs-adapter, skip)
19. What is the minimum viable subset for a resource system WITHOUT TOR's magic/ability/career systems?
20. What would need to change to use TAOM's adapter pattern instead of TOR's direct `Hero.MainHero` access?
21. Are there any Bannerlord 1.3.15 API incompatibilities with TOR's approach? Specifically verify these against decompiled source at `E:\Decompiled_Bannerlord\`:
    - `PartyCharacterVM.InitializeUpgrades` method signature
    - `PartyVM.TransferAllCharacters` method signature
    - `PartyScreenLogic.PartyCommand.TotalNumber` field/property existence
    - `PartyScreenLogic.AddCommand` method signature
    - All 7 CampaignEvents used by CustomResourceManager

## Output Format

Write findings to `docs/research/codex-tor-resource-analysis.md` with:

1. **Architecture Summary** — text-based class diagram showing all components, their relationships, and data flow
2. **Teef Deep Dive** — complete earning/spending/formula reference with actual code snippets from source
3. **Integration Map** — table of every Harmony patch (target, method, type, purpose), every GameModel override, every CampaignEvent subscription
4. **Save/Load Analysis** — exact mechanism for data persistence, including field attributes and serialization flow
5. **UI Analysis** — how resources are displayed, what Gauntlet prefabs exist, Waaagh meter mechanics
6. **Portability Assessment** — what ports clean to TAOM, what needs adaptation, what to skip, with specific file-by-file analysis
7. **Recommended TAOM Design** — your independent recommendation for class hierarchy, file structure, and implementation order
8. **Risk Flags** — anything fragile, version-dependent, tightly coupled, or that uses reflection/private field access
9. **1.3.15 API Verification** — results of checking each integration point against decompiled Bannerlord source
