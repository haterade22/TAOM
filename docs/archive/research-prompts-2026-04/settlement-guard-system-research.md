# Research Task: Bannerlord Settlement Guard Spawning System — Deep Dive

## Objective
Produce a comprehensive technical analysis of how Bannerlord 1.3 spawns, equips, and manages guards in towns, castles, and villages. The goal is to understand every hook point where TAOM can intercept to implement **per-settlement guard customization** — e.g., Minas Tirith spawns Citadel Guards and Fountain Guards, Edoras spawns Rohirrim Door Wardens, Isengard spawns Uruk-hai Sentinels.

## What to Research

### 1. GuardsCampaignBehavior (Primary Guard Spawner)
**File:** `E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\CampaignBehaviors\GuardsCampaignBehavior.cs`

This is the central class. Analyze thoroughly:

- **LocationCharactersAreReadyToSpawn** — the event handler that triggers guard placement. It calls `AddGarrisonAndPrisonCharacters` for fortifications, then `AddGuardsFromGarrison` for towns/castles.

- **AddGuardsFromGarrison** — the method that reads scene spawn points (`sp_guard`, `sp_guard_with_spear`, `sp_guard_patrol`, `sp_guard_unarmed`, `sp_guard_castle`) and fills them with guard characters. Document:
  - How spawn point counts are read from `unusedUsablePointCount`
  - How prosperity multiplier scales guard numbers
  - The castle vs town unarmed guard ratio (1.6f vs 0.4f)
  - How `lordshall` location excludes unarmed guards
  - Prison area_marker fallback guards

- **TakeGuardAgentDataFromGarrisonTroopList** — the troop selection method. This is the KEY interception point:
  - First priority: picks from actual garrison roster (`_garrisonTroops` list), weighted by troop level
  - Fallback: uses `culture.Guard` (the culture's default guard character)
  - Document the weighted selection algorithm and how troops are consumed from the list

- **PrepareGuardAgentDataFromGarrison** — equipment assembly:
  - How `GetRandomEquipmentElements` works for guards
  - Spear override logic and the hardcoded culture check (`battania` → `northern_spear_2_t3`, else `western_spear_3_t3`)
  - Unarmed stripping logic
  - Monster/race selection via `FaceGen.GetMonsterWithSuffix(race, "_settlement")`

- **Guard type factory methods** — document each:
  - `CreateCastleGuard` — uses spear override, `sp_guard_castle` spawn point, `_guard` action set
  - `CreateStandGuard` — basic guard, `sp_guard` spawn point
  - `CreateStandGuardWithSpear` — spear override, `sp_guard_with_spear`
  - `CreateUnarmedGuard` — stripped equipment, `sp_guard_unarmed`, `_unarmed_guard` action set, outdoor wanderer behavior
  - `CreatePatrollingGuard` — patrolling behavior, `sp_guard_patrol`
  - `CreatePrisonGuard` — uses `culture.PrisonGuard` directly (NOT garrison), `sp_prison_guard`

### 2. CultureObject Guard Properties
**File:** `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\CultureObject.cs`

Document all guard-related properties on CultureObject:
- `Guard` — default guard character (NPCCharacter)
- `PrisonGuard` — prison guard character
- `CaravanGuard` / `VeteranCaravanGuard` — caravan guards
- `GangLeaderBodyguard` — gang leader bodyguard
- `SettlementPatrolPartyTemplateWeak/Moderate/Strong/Naval` — patrol party templates
- How these are loaded from XML (`ReadObjectReferenceFromXml`)

### 3. Settlement Patrol System (World Map Patrols)
**Files:**
- `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\CampaignBehaviors\PatrolPartiesCampaignBehavior.cs`
- `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\ComponentInterfaces\SettlementPatrolModel.cs`
- `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\GameComponents\DefaultSettlementPatrolModel.cs`

Analyze:
- `DefaultSettlementPatrolModel.GetPartyTemplateForPatrolParty` — selects patrol template based on Guard House building level (1→Weak, 2→Moderate, 3→Strong)
- `CanSettlementHavePatrolParties` — requires non-rebel owner, IsTown, has Guard House
- `PatrolPartiesCampaignBehavior` — daily tick spawning, AI behavior, replenishment logic
- How `PatrolPartyComponent` works

### 4. Scene Spawn Points
Document the spawn point IDs that control WHERE guards stand in scenes:
- `sp_guard` — standard standing guard positions
- `sp_guard_with_spear` — spear guard positions
- `sp_guard_patrol` — patrol route points
- `sp_guard_unarmed` — civilian/unarmed guard positions
- `sp_guard_castle` — castle-specific guard positions
- `sp_prison_guard` — prison guard position
- `area_marker_1/2/3` — prison area fallback positions

### 5. Mission-Level Guard Behaviors
**Files:**
- `E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\Missions\AgentBehaviors\PatrollingGuardBehavior.cs`
- `E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\Missions\AgentBehaviors\StandGuardBehavior.cs`
- `E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\Missions\AgentBehaviors\PatrolAgentBehavior.cs`

How do guards behave once spawned? What behaviors are assigned? How do they react to crime/combat?

### 6. Existing TAOM Guard Infrastructure
**Files to check:**
- `c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml` — current guard= attributes per culture
- `c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt` — XSLT culture guard definitions
- `c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml` — patrol party templates
- Any existing NPC characters named `guard_*` in character XML files

### 7. Village Guards
Do villages have guards? Check:
- Whether `LocationCharactersAreReadyToSpawn` fires for villages
- Whether villages have `sp_guard` spawn points
- The `village_center` location vs `center` for towns

## Deliverable Format

Structure the research document as:

### A. Guard Spawning Pipeline (flow diagram)
Event fires → which methods → troop selection → equipment assembly → scene placement

### B. Interception Points (ranked by feasibility)
For each potential hook point, document:
1. What it controls
2. Harmony patch type needed (Prefix/Postfix/Transpiler)
3. What parameters are available (do we have Settlement context?)
4. Risk level (how much vanilla behavior do we override?)

### C. Data Model Design Considerations
What XML structure could drive per-settlement guard configuration? Consider:
- Settlement ID → list of guard troop IDs with weights
- Fallback chain: settlement → clan → culture (like VolunteerRecruitmentService)
- Equipment override capability (Citadel Guard vs Fountain Guard might be same troop, different equipment)
- Spawn point type mapping (which troop types go to which sp_guard_* points)

### D. Spear Culture Hardcode
The `GetSuitableSpear` method hardcodes `battania` → `northern_spear_2_t3`. Document what TAOM needs to do about this for 16 cultures.

### E. Risks and Gotchas
- Save compatibility implications
- Performance (garrison roster iteration per guard spawn)
- Interaction with existing TAOM patches (Patch8_SiegeCampGuard, Patch23_BannerColorPersistence)
- What happens when garrison is empty (falls back to culture.Guard)

## Key Insight from Decompilation

The critical discovery is that **guards are drawn from the actual garrison roster first**, weighted by level. `culture.Guard` is only the fallback when the garrison is empty. This means:
1. Whatever troops the player garrisons in a settlement will appear as guards
2. A per-settlement system could work by either:
   a. Intercepting `TakeGuardAgentDataFromGarrisonTroopList` to inject custom characters
   b. Intercepting `AddGuardsFromGarrison` to replace the entire spawn logic
   c. Using a custom `CreateLocationCharacterDelegate` per settlement

The `settlement.Culture` is passed to `AddLocationCharacters`, meaning all guards in a settlement share one culture. Per-settlement customization would need to bypass this.
