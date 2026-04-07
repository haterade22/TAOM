# Codex Adversarial Review: TAOM SpecialResources vs TOR_Core CustomResources

> **Purpose:** Tear apart TAOM's special resource implementation by comparing it line-for-line against the proven TOR_Core system. Find every gap, missed edge case, silent failure, and architectural weakness. Be ruthless -- we want the most efficient and correct product possible.

---

## Setup

Before starting, clone the TOR reference implementation:
```bash
git clone --branch development --depth 1 https://github.com/TheOldRealms/TOR_Core /tmp/TOR_Core
```

## Your Role

You are an adversarial code reviewer. Your job is to find problems, not praise good work. Assume every file has at least one bug until proven otherwise. Compare TAOM's simplified implementation against TOR's battle-tested production code and flag every place where TAOM cut corners, missed mechanics, or introduced fragility.

## Codebases

### TAOM (our implementation -- the one under review)
All files at `c:/Users/mikew/source/repos/TAOM/`:

**Core:**
- `Main/Features/SpecialResources/Domain/SpecialResource.cs` -- resource definition
- `Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs` -- per-troop cost
- `Main/Features/SpecialResources/ISpecialResourceService.cs` -- service interface
- `Main/Features/SpecialResources/SpecialResourceService.cs` -- **core logic -- scrutinize heavily**
- `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` -- storage interface
- `Main/Features/SpecialResources/SpecialResourceStorageService.cs` -- persistence layer
- `Main/Features/SpecialResources/ISpecialResourceConfigProvider.cs` -- config interface
- `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs` -- XML loader

**Entry points:**
- `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` -- **CampaignBehavior -- scrutinize event handlers heavily**
- `Main/Features/SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs` -- Harmony postfix
- `Main/Features/SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs` -- Harmony postfix
- `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs` -- hook interface
- `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs` -- hook impl

**UI:**
- `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` -- UIExtenderEx mixin
- `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs`
- `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs`

**Config:**
- `Main/_Module/ModuleData/special_resources/special_resources_config.xml`
- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`

**Tests:**
- `TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs`
- `TAOM.Tests/Features/SpecialResources/SpecialResourceStorageServiceTests.cs`

**Integration points (read for context, check for correctness):**
- `Main/SubModule.cs` -- lines 49-51 (usings), lines 267-273 (behavior+model registration), lines 307-312 (Patch26 hook init)
- `Main/IoC.cs` -- line 66 (feature IoC call)
- `Main/Features/SpecialResources/SpecialResourcesIoC.cs` -- DryIoc registrations
- `Main/Features/SpecialResources/Models/TaomSpecialResourceModel.cs` -- GameModel facade

**Docs (context only -- do not review prose):**
- `docs/features/special-resources.md` -- feature doc
- `docs/research/tor-resource-system.md` -- our TOR research notes

### TOR_Core (the reference implementation -- proven in production)
All files at `/tmp/TOR_Core/` (already cloned):

**Core resource system:**
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResource.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/CustomResourceManager.cs` -- **their equivalent of our service+behavior -- compare directly**

**Per-faction helpers (strategy pattern):**
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/TeefHelper.cs` -- orc currency (closest to our Mordor Scraps)
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/OathGoldHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/ChivalryHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/FavorHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/ForestHarmonyHelper.cs`

**Per-faction behaviors:**
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/TeefBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/OathGoldBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/PrestigeNobleTownBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/EonirFavorEnvoyTownBehavior.cs`

**Waaagh meter (advanced UI example):**
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghHelper.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghMeterMapView.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomResources/WaaaghMeter/WaaaghMeterVM.cs`

**Persistence:**
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/HeroExtendedInfo.cs` -- see CustomResources dictionary
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/ExtendedInfoManager.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/HeroExtensions.cs` -- resource-related extension methods

**GameModel:**
- `/tmp/TOR_Core/CSharpSourceCode/Models/TORCustomResourceModel.cs` -- **compare against our TaomSpecialResourceModel**
- `/tmp/TOR_Core/CSharpSourceCode/Models/TORPartyWageModel.cs` -- wage integration with resources

**Harmony patches:**
- `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/CustomResourcePatches.cs` -- **compare patch targets and approach**

**UI:**
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/UI/TORMapInfoVMExtension.cs` -- map bar resource display
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/UI/PartyVMExtension.cs` -- party screen resource display
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/UI/PartyCharacterVMExtension.cs` -- per-troop resource display

**Troop cost XML (how TOR defines per-troop resource costs):**
- `/tmp/TOR_Core/ModuleData/tor_custom_xmls/tor_extendedunitproperties.xml` -- look for `ResourceCost` elements
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/CharacterExtendedInfo.cs` -- ResourceCostTuple class

**Config:**
- `/tmp/TOR_Core/CSharpSourceCode/TORConfig.cs` -- MaximumCustomResourceValue and other caps

**SubModule registration order:**
- `/tmp/TOR_Core/CSharpSourceCode/SubModule.cs` -- how/when CustomResourceManager is initialized relative to behaviors

---

## Known Suspects (from our internal review -- confirm or dispute)

Our deep review flagged these issues. Codex must read the code and either CONFIRM with evidence or DISPUTE with counter-evidence:

1. **Pending transaction gap:** TAOM's `PartyScreenLogic_UpgradeTroop_Patch` deducts resources immediately in a postfix. TOR reportedly queues changes in `_resourceChanges` and only commits on party screen close. If true, TAOM loses resources when the player cancels the party screen. **Confirm this by reading TOR's `CustomResourceManager.cs` and `CustomResourcePatches.cs`.**

2. **Upgrade cost targets the wrong troop:** TAOM's `PartyCharacterVM_InitializeUpgrades_Patch` calls `_hook.GetUpgradeCost(targetId)` where `targetId` is the UPGRADE TARGET (the troop you're upgrading TO). But `PartyScreenLogic_UpgradeTroop_Patch` also uses `element.Character.UpgradeTargets[upgradeTargetIndex].StringId` (the target). **Verify both patches use the same troop ID consistently and that our `troop_resource_costs.xml` IDs match the upgrade TARGET, not the source troop.**

3. **GameModel is dead code:** `TaomSpecialResourceModel` exposes `GetCurrentResource()` and `CanAffordUpgrade()` but nothing in the codebase calls these methods. The hook goes directly to the service. **Confirm by grepping for `TaomSpecialResourceModel` usage outside SubModule registration.**

4. **Orphaned adapters:** `IHeroResourceAdapter`, `HeroResourceAdapter`, `ISettlementProductionAdapter`, `SettlementProductionAdapter` exist in `Main/Adapters/` but no service uses them. The service takes raw strings. **Confirm these are dead code and recommend deletion or migration.**

5. **SpriteWidget brush layer conflict:** `SpecialResourceSpriteWidget` sets `layer.Sprite` on all brush layers, but `IconBrushWidget.UpdateIcon()` runs in `base.OnLateUpdate()` BEFORE our override and may reset sprites from the "special_resource" brush layer lookup (which won't exist). **Read `IconBrushWidget.UpdateIcon()` and determine if our sprite is overwritten each frame.**

6. **No cap enforcement on load:** When a save is loaded, `SyncData` restores the dictionary as-is. If a player save-edits their resource to 99999, there's no cap enforcement. TOR may or may not do this. **Check both codebases.**

---

## Review Tasks (in order of importance)

### 1. CRITICAL: Party Screen Upgrade Flow Comparison

Read TOR's `CustomResourcePatches.cs` and `CustomResourceManager.cs` thoroughly. Then read TAOM's two patch files.

Answer these questions:
- Does TOR use a **pending transaction** pattern (queue upgrades, commit on party screen close, revert on cancel)? If so, does TAOM? If TAOM deducts immediately on upgrade, what happens when the player clicks "Cancel" on the party screen -- are resources lost?
- Does TOR clamp the upgrade count BEFORE the upgrade executes (prefix) or deduct AFTER (postfix)? Which does TAOM do? Is the timing correct?
- Does TOR handle **multi-upgrade** scenarios (upgrading 5 troops at once) correctly? Does TAOM?
- What happens in TOR when resources hit exactly 0 mid-upgrade? Does TAOM handle this edge?

### 2. CRITICAL: Event Handler Completeness

Read TOR's `CustomResourceManager.cs` event subscriptions exhaustively. List EVERY CampaignEvent TOR hooks into for resource changes. Then check which ones TAOM is missing.

Specifically check:
- Does TOR earn resources from **tournaments**? Does TAOM?
- Does TOR earn resources from **quest completion**? Does TAOM?
- Does TOR earn resources from **caravan profits** or **trade**? Does TAOM?
- Does TOR earn resources from **executing prisoners**? Does TAOM?
- Does TOR have **hourly tick** logic (not just daily)? Does TAOM?
- How does TOR handle the **player switching kingdoms**? Does the resource reset? Transfer? Does TAOM handle this?
- How does TOR handle **new game started vs loaded game**? Does TAOM initialize correctly on both paths?

### 3. HIGH: GameModel Integration Depth

Read TOR's `TORCustomResourceModel.cs`. Compare against TAOM's `TaomSpecialResourceModel.cs`.

- TOR's model computes `GetCultureSpecificCustomResourceChange()` returning an `ExplainedNumber` with line-item breakdown. Does TAOM have anything comparable for campaign tooltip display?
- Does TOR's model integrate resource levels into **wage calculations** (TORPartyWageModel)? If so, TAOM is missing this entirely.
- Does TOR's model affect **party morale** based on resource levels? Does TAOM?
- Is TAOM's GameModel actually used by anything, or is it dead code?

### 4. HIGH: Persistence Edge Cases

Read TOR's `HeroExtendedInfo.cs` -- specifically how `CustomResources` dictionary is declared, saved, and restored.

Compare against TAOM's `SpecialResourceStorageService.cs` + `SpecialResourcesBehavior.SyncData()`:
- What happens if the player loads a save from BEFORE the SpecialResources feature existed? Does the dictionary deserialize as null? Empty? Does TAOM handle this gracefully?
- What happens if the player's kingdom changes between saves? Does stale resource data linger for the old kingdom?
- Does TOR cap resources on load (preventing save-edited values above cap)? Does TAOM?
- Is there a risk of dictionary key collision if hero StringIds are reused?

### 5. MEDIUM: Per-Faction Strategy Pattern

Read TOR's `TeefBehavior.cs` (closest analog to Mordor Scraps). Compare the earning mechanics:
- TOR has **settlement-specific NPCs** that convert gold to Teef. Does TAOM plan for this?
- TOR has **faction-specific earning events** beyond the generic battle/raid/siege. Does TAOM's XML-driven approach lose important gameplay depth by being too generic?
- Is TAOM's flat `per_battle_victory_base * ratio` formula too simplistic? What does TOR actually do for battle rewards?

### 6. MEDIUM: UI Completeness

Read TOR's `TORMapInfoVMExtension.cs` and `PartyVMExtension.cs`. Compare:
- Does TOR show resource cost in the **upgrade tooltip** (next to gold cost)? Does TAOM?
- Does TOR show resource balance in the **party screen header**? Does TAOM?
- Does TOR show a **daily change breakdown** somewhere (e.g., "+2.5 from towns, -1.3 upkeep = +1.2 net")? Does TAOM?
- Does TOR have **notifications/warnings** when resources are low? Does TAOM?

### 7. LOW: Test Coverage Gaps

Read TAOM's test files. Then think about what is NOT tested:
- Is there a test for the daily tick when kingdom is null (player is clanless)?
- Is there a test for earning when resource is already at cap?
- Is there a test for spending more than available (should it clamp or throw)?
- Is there a test for concurrent Get/Set from multiple callers?
- Are there tests for the ConfigProvider with malformed XML?
- Are the Harmony patches testable at all? If not, what integration risk does this create?

---

## Output Format

```
## CRITICAL FINDINGS
[Issues that will cause bugs, data loss, or silent failures in production]

1. [TITLE]
   TOR does: [what TOR does]
   TAOM does: [what TAOM does or does not do]
   Impact: [what goes wrong]
   Fix: [specific fix recommendation]

## HIGH FINDINGS
[Issues that degrade gameplay quality or miss important mechanics]

## MEDIUM FINDINGS
[Issues that reduce polish or miss optimization opportunities]

## LOW FINDINGS
[Nits, missing tests, minor improvements]

## WHAT TAOM DOES BETTER
[Any areas where TAOM's approach is actually cleaner than TOR's -- be honest]

## KNOWN SUSPECTS VERDICT
1. Pending transaction gap: CONFIRMED/DISPUTED — [evidence]
2. Upgrade cost target mismatch: CONFIRMED/DISPUTED — [evidence]
3. GameModel dead code: CONFIRMED/DISPUTED — [evidence]
4. Orphaned adapters: CONFIRMED/DISPUTED — [evidence]
5. SpriteWidget brush conflict: CONFIRMED/DISPUTED — [evidence]
6. No cap on load: CONFIRMED/DISPUTED — [evidence]

## ARCHITECTURE COMPARISON SUMMARY
| Aspect | TOR | TAOM | Verdict |
|--------|-----|------|---------|
| Persistence | HeroExtendedInfo [SaveableField] | Dictionary SyncData | [which is better and why] |
| Event coverage | [N events] | [N events] | [gap analysis] |
| Upgrade flow | [pending/immediate] | [pending/immediate] | [risk assessment] |
| UI integration | [depth] | [depth] | [gap] |
| Extensibility | [per-faction helpers] | [XML-driven] | [trade-offs] |
| Test coverage | [none] | [24 tests] | [assessment] |
```

## Rules
- Read EVERY file listed above. Do not summarize from file names alone.
- When you find a gap, cite the specific TOR file and line where the behavior exists.
- When you claim TAOM is missing something, verify by reading the TAOM code -- do not assume from interface names.
- Be specific about fixes -- "add error handling" is useless; "add null check on line 42 of SpecialResourcesBehavior.cs for hero.Clan when player is clanless" is useful.
- If TAOM's approach is genuinely better than TOR's in some area, say so -- we want honest analysis, not just criticism.
- For Bannerlord API verification, check decompiled source at `E:\Decompiled_Bannerlord\` (organized by: Campaign/, MountAndBlade/, Modules/, Core/, UI/).
- TAOM targets Bannerlord v1.3.15. TOR targets an older version. Flag any API that exists in TOR's target but was removed/changed in 1.3.
- For each Known Suspect, output: `CONFIRMED: [evidence]` or `DISPUTED: [counter-evidence]` -- do not skip any.
