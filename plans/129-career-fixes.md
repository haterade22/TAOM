# Plan 129: Port the career fixes: mount bonuses, typed perks, review follow-ups, promoted careers, screen and quest fixes

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. The plan has three Parts (A, B, C). Run them in that
> order on one branch; each Part can be its own session and ends with its own done criteria. Do not
> edit `plans/README.md`: every WP branch shares it, so the integrator records this plan's status
> and test count when merging. Put the numbers the "Done criteria (whole WP)" section asks for in
> your final message instead.
>
> **Drift check (run first, in your worktree, Step 0)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/CareerSystem Main/Features/SpecialResources Main/Features/FieldCommission Main/Features/AiPartySize/AiPartySizeSettingsWatcher.cs Main/Features/CulturalFeats Main/Features/DevConsole/Cheats Main/Features/CoopInterop/CoopSessionPolicy.cs Main/Adapters Main/IoC.cs Main/SubModule.cs Main/_Module/ModuleData/career_system Main/_Module/ModuleData/field_commission Main/_Module/GUI/PreFabs/CareerSystem TAOM.Tests/Features/CareerSystem TAOM.Tests/Features/SpecialResources TAOM.Tests/Features/FieldCommission TAOM.Tests/Features/AiPartySize TAOM.Tests/Features/CoopInterop/CoopSessionPolicyTests.cs TAOM.Tests/Migration/IlCallScanner.cs docs/features/career-system.md docs/features/special-resources.md docs/features/field-commission.md docs/features/career-quest-system.md docs/reference/gamemodel-registry.md .claude/rules/gamemodels.md`
> Expected changes from earlier WPs: `Main/SubModule.cs` and `Main/IoC.cs` (WP104, WP105 and most
> later WPs), `CareerPerkMissionBehavior.cs` plus a new `CareerPerkOffThreadTests.cs` (WP106),
> `SpecialResourcesBehavior.cs` (WP119), `IlCallScanner.cs` (WP118), `MutationParams.cs` and
> `career-system.md` (WP112). STOP only on a real mismatch in a file a step transplants (each step's
> pre-check names them).

## Status

- **Priority**: P1
- **Effort**: L (15 commits in three Parts; about 20 1.5.x commits read)
- **Risk**: MED (shared career, special-resource and party-size code; two single-owner files touched
  at three named lines; every change is test-pinned, several engine seams verified on v1.4.8)
- **Depends on**: plans/105-composition-root.md (WP105, which brings WP104's
  `TAOM.Tests/Infrastructure/RepoPaths.cs`) and the WP123 plan (always-on cost cuts, which ports the
  non-career half of `e0ad0db76`). Both merged on your base. WP106, WP118 and WP119 normally merge
  earlier too; the steps handle them present or absent.
- **Category**: bug (shared-feature fixes; one perf item)
- **Wave / lane**: wave 4, lane B
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (CareerSystem,
  SpecialResources, FieldCommission and AiPartySize never got a 1.5.x-style module on this line, so
  they keep their hand wiring), D7 (no co-op feature; the career quest fix cancels only in single
  player), D8 (`v2.0.28` subjects), D9 (drop every `RequiresGame` test category), D10, D19 (git
  through the Bash tool only), D20 (ModuleData commits only on a laptop with the WP101 Armory)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. (`origin/bannerlord-1.5.x` has moved on: at cold review it was `ca1bc5f2e`, and
  `95a348a60`, `4d9ae0e8a` and `32aeb9e10` past `e45f6df2c` touch career paths. They are not in this
  WP's pack: do not port them. Every step names its 1.5.x source by SHA, never by the branch tip.)
- **Issue**: port of #611, #613, #612, #766, #767, #768, #770, #790, #971 (with #378) and the #769
  screen fixes (root perk, keystone passives, MountHealth display). #771 is doc-only here (its code
  fix is already on this line as `1b00d1acd`). The promoted-culture archetype rows and the heir
  career-quest cancel carry no 1.5.x issue of their own: the orchestrator creates one issue for each
  before Part C lands.

## Why this matters

Players on the 1.4.8 build pick career perks that do nothing or do the wrong thing. The
`MountChargeDamage` passive (92 pips in the 1.4.x data) and the mount half of the Cavalry ability are
multiplied on the rider, but v1.4.8 writes those two values only on the horse and reads the horse's
copy, so they are inert. Five resistance pips authored `Blunt` or `Cut` resist every hit, because
the mask enum has no damage kinds and the parser falls back to `All`. The Ammo passive (20 pips)
skips javelins and writes above an unchanged maximum after spawn. All 2,010 nested career choices
load with an empty group, so the tier lock, the one-keystone-per-tier rule and the foreign-choice
save repair never fire. The career resource gain scales only daily town income; PartySize picks
(67 pips) leak to caravans and garrisons, get multiplied by the AI party size setting and show only
after a roster change; raids pay the battle credit, resisted raids and send-troops hideout clears pay
twice. The 18 careers of the six cultures promoted in `cc1713eb3` have no archetype, so their battle
ability shows its toast and applies nothing. Battlefield promotion skips every soldier race but human, dwarf and elf, an heir
keeps the dead hero's career quest forever, and the career screen prints nothing for MountHealth
picks (61 pips) and hides root and keystone passives. After this WP each of these behaves as the
text promises, `taom.career_perks` shows a tester what each perk reaches, and the per-hit perk lines
are aggregated so the 146 TroopDamage pips cannot flood the log.

## Current state

### Prerequisites from earlier work packages (verify in Step 0; absent means STOP)

| Prerequisite | From | Check |
|---|---|---|
| `TAOM.Tests/Infrastructure/RepoPaths.cs` with `ReadSource(string relativePath, bool stripComments = false)` and `RepoPath(params string[] parts)` | WP104 (via WP105) | Step 0 greps |
| `Main/Composition/FeatureModules.cs` | WP105 | Step 0 `ls` |
| A commit whose message carries `Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120` | WP123 | Step 0 `git log --grep` |
| No `_hitTallies`, `ApplyMountStatModifiers`, `AttackTypeMaskMatch`, `IPartySizeCacheInvalidator`, `ScaleEarned`, `PaysBattleCredit`, `IsFormerPlayerHeroQuest` or `RootPerkDescription` anywhere under `Main/` | nobody ported this WP's work early | Step 0 grep prints nothing |

Handled either way (record which in Step 0): WP106's deferral queue in `CareerPerkMissionBehavior.cs`
(`_deferred`) and its `CareerPerkOffThreadTests.cs`; WP118's `IlCallScanner.Fingerprint`; an existing
`CoopSessionPolicy.IsSinglePlayerAuthority`.

### Files and their roles (on `origin/bannerlord-1.4.5`)

| File | Role |
|---|---|
| `Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs` | Applies career passives and buffs to agent stats and per-hit damage. Rider-side mount multiplies at `:142-143` (passive), `:156-160` (self buff), `:175-178` (ally buff) |
| `Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs` | Its contract; gains `ApplyMountStatModifiers`, `AmmoBonus`, `ResetDiagnostics` |
| `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` (77 lines) | Thin `SandboxAgentStatCalculateModel` override; ctor `:29` takes 4 services; `UpdateAgentStats` `:58-76` |
| `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs` | Abstract parent of `TaomCombatMechanicsModel`; `HitMask` derives only Melee or Ranged |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` (225 lines before WP106) | Mission boundary; field `_passives` `:28`, ctor parameter `ICareerPassiveService passives` `:43`, Ammo refill in `OnAgentBuild` `:109-143`, fallen-hero ally loop `:192-197`, `OnEndMission` `:202-219` |
| `Main/Features/CareerSystem/CareerConfigProvider.cs` | Parses careers; `ParseChoice(XElement el)` `:234`, mask `ParseEnum<AttackTypeMask>` `:261`, `groupId: el.Attribute("group_id")` `:289` |
| `Main/Features/CareerSystem/CareerPassiveService.cs` | Passive cache; ctor `CareerPassiveService(IModLogger logger)` `:37` |
| `Main/Features/CareerSystem/CareerSystemIoC.cs` | Archetype map; the last mapped block ends `["fell_rider"] = CareerArchetype.Cavalry,` `:165`, then `// ═══ UMBAR ═══` `:167` |
| `Main/Features/CareerSystem/UI/CareerEffectDisplayMap.cs` | Active Effects formatting; `FlatValueEffects` `:27-34` lists `InventoryCapacity` and `MountHealth` as counts |
| `Main/Features/CareerSystem/UI/CareerScreenVM.cs` | Career screen VM; `PublishActiveEffects` `:240-256`, group walk `:277-331` sums only non-keystone choices found in groups |
| `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` | Career button mixin; dead `ExecuteOpenCareerScreen` `:101-115`, `using TaleWorlds.MountAndBlade.View;` `:6` |
| `Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs` | The career button's live click handler (identical to the 1.5.x pre-image) |
| `Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs`, `CareerQuestService.cs`, `ICareerQuestService.cs` | Career quest offers; quest id `"taom_cq_" + def.Id` `:102`; no cancel for a former hero's quest |
| `Main/Features/SpecialResources/SpecialResourceService.cs`, `SpecialResourcesBehavior.cs`, `SpecialResourceEarnPolicy.cs`, `UI/SpecialResourceMapBarMixin.cs`, `Domain/DailyResourceBreakdown.cs` | Special resource earning; `OnRaidCompleted` `:324-334`, `OnHideoutCompleted` `:368-378`, `NotifyEarning(... float? before = null)` `:406` |
| `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs` | Party size; `ApplyFlat(CareerPassiveHero.ResolveId(party), ...)` `:49` runs before the AI lord factor `:55` |
| `Main/Features/AiPartySize/AiPartySizeSettingsWatcher.cs` | MCM watcher; sweeps `MobileParty.All` itself |
| `Main/Features/FieldCommission/{Domain/FieldCommissionConfig.cs,FieldCommissionConfigProvider.cs,FieldCommissionMeritService.cs}` | Promotion race allow-list `{ "human", "dwarf", "elf" }` (`FieldCommissionConfig.cs:52`, provider `:21`) |
| `Main/Features/CoopInterop/CoopSessionPolicy.cs` | Authority statics `:45`, `:51`, `:83`, `:104`; no `IsSinglePlayerAuthority` |
| `Main/IoC.cs` (single owner) | `RegisterCoreServices` `:208-221`; last line `container.Register<ICampaignSessionAdapter, CampaignSessionAdapter>(Reuse.Singleton);` `:220` |
| `Main/SubModule.cs` (single owner) | `:1103` constructs `TaomAgentStatCalculateModel(careerAgentStat, elephantAttackService, spiderAttackService, mumakilAttackService)`; `:1905` passes `IoC.Resolve<Features.CareerSystem.ICareerPassiveService>(),` to `CareerPerkMissionBehavior` |
| `TAOM.Tests/Migration/IlCallScanner.cs` (166 lines) | Has `ExtractCalledMethods` `:111`, no `Fingerprint`; equal to `a27c6b338^` |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`)

```csharp
// TaomAgentStatCalculateModel.cs:58-66 (then the creature mount-lock ternary :68-75)
    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);
        _agentStatService.ApplyAgentStatModifiers(
            heroId: (agent.Character as CharacterObject)?.HeroObject?.StringId,
            agentIndex: agent.Index,
            isHuman: agent.IsHuman,
            isHero: agent.IsHero,
            agentDrivenProperties);
```

```csharp
// CareerAgentStatService.cs:142-143 (the same shape at :157-160 and :175-178 for the buffs)
        var chargeBonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.MountChargeDamage);
        if (chargeBonus != 0f) props.MountChargeDamage *= (1f + chargeBonus);
```

```csharp
// CareerPerkMissionBehavior.cs:125-132, inside OnAgentBuild
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var weapon = agent.Equipment[slot];
                if (weapon.IsEmpty || !weapon.IsAnyAmmo()) continue;

                int boosted = CareerPassiveMath.BoostAmmo(weapon.ModifiedMaxAmount, weapon.Amount, bonus);
                if (boosted > weapon.Amount)
                    agent.SetWeaponAmountInSlot(slot, (short)boosted, enforcePrimaryItem: false);
```

```csharp
// CareerEffectDisplayMap.cs:27-34
    private static readonly HashSet<PassiveEffectType> FlatValueEffects = new HashSet<PassiveEffectType>
    {
        PassiveEffectType.Health,
        PassiveEffectType.PartySize,
        PassiveEffectType.CompanionLimit,
        PassiveEffectType.InventoryCapacity,
        PassiveEffectType.MountHealth,
    };
```

Data facts measured this session on `origin/bannerlord-1.4.5` with an XML parser:
`taom_career_choices.xml` holds 67 root choices, 2,010 nested choices and 402 groups, none nested
with a `group_id` attribute, and no duplicate or doubly-owned id; masks are 157 `Melee`, 121
`Ranged`, 4 `Blunt` (`black_uruk_captain_t1_a_p2`, `olog_hai_warchief_t1_a_p2`,
`watchman_of_stangard_t1_a_p4`, `avanc_luth_raider_t2_b_p2`) and 1 `Cut`
(`black_uruk_captain_t2_a_p3`); 61 `MountHealth` pips (0.10 x23, 0.13 x20, 0.15 x18), 0
`InventoryCapacity`, 92 `MountChargeDamage`, 20 `Ammo`, 67 `PartySize`, 146 `TroopDamage`, 41
`TroopResistance`, 1 `SpecialResourceGain` (`captain_of_osgiliath_t3_a_p4`, 0.10), 0
`SpecialResourceUpkeepModifier`. `taom_careers.xml` defines 67 active careers (a 68th,
`far_harad_halftroll`, is commented out); the archetype map holds 50 ids and misses exactly the 18
promoted-culture careers. `raceage/race_age_config.json` lists all ten races the #612 default names.

### Verified v1.4.8 engine facts (taom-src on the installed DLLs, `C:\Users\mikew\.taom-src\v1.4.8\`)

| Member | Fact |
|---|---|
| `SandboxAgentStatCalculateModel` | `public override void InitializeMissionEquipment(Agent agent)` `:93`; vanilla's extra-ammo perks call `equipment.SetAmountOfSlot(val6, (short)num2, true)` `:188`; `UpdateAgentStats` `:204`; `UpdateHorseStats` `:1126` writes `MountSpeed` `:1216`, `:1221` and `MountChargeDamage` `:1230`; `UpdateHumanStats` `:840` writes neither |
| `AgentStatCalculateModel` | `public virtual void InitializeMissionEquipment(Agent agent)` `:16`; `public abstract void UpdateAgentStats(Agent, AgentDrivenProperties)` `:28` |
| `AttackInformation` | `AttackerAgentMountChargeDamageProperty = attackerAgent.GetAgentDrivenPropertyValue(DrivenProperty.MountChargeDamage)` `:335`; `MissionWeapon AttackerWeapon` `:27`; `int WeaponAttachBoneIndex` `:131` |
| `Mission` | `ChargeDamageCallback(ref AttackCollisionData, Blow, Agent attacker, Agent victim)` `:6034` tests `attacker.RiderAgent` (the attacker is the horse); `OnMissionStateFinalize` `:2207` removes every behavior (`:2219`), which calls `OnRemoveBehavior` (`:4645`); `OnEndMissionInternal` call `:4585` |
| `MissionBehavior` | `OnAgentBuild` `:57`; `public virtual void OnEndMissionInternal()` `:121`; `OnEndMission` `:126`; `public virtual void OnRemoveBehavior()` `:130` |
| `MissionCombatMechanicsHelper` (`public static class` `:8`) | `public static bool IsCollisionBoneDifferentThanWeaponAttachBone(in AttackCollisionData collisionData, int weaponAttachBoneIndex)` `:157`; vanilla's Blunt correction (empty weapon, off the attach bone, alternative attack, fall, horse charge) `:192` |
| `AttackCollisionData` | `IsAlternativeAttack` `:80`, `IsMissile` `:86`, `IsHorseCharge` `:102`, `IsFallDamage` `:104`, `int DamageType` `:112` |
| `MissionEquipment` | `public void SetAmountOfSlot(EquipmentIndex slotIndex, short dataValue, bool addOverflowToMaxAmount = false)` `:506` |
| `MissionWeapon` | `Amount` `:114`, `ModifiedMaxAmount` `:132`, `IsEmpty` `:136`, `IsAnyAmmo()` `:408`, `IsAnyConsumable()` `:520` |
| `WeaponComponentData.IsAmmo` | `:99-108`: consumable AND no weapon flags, so a javelin is not ammo |
| `Agent` | `IsHuman` `:640`, `IsMount` `:642`, `RiderAgent` `:718`, `IsHero` `:841`, `AgentDrivenProperties` `:879`, `HealthLimit` `:885`, `MountAgent` `:1001`, `SetWeaponAmountInSlot` `:2258`, pickup merge capped at `ModifiedMaxAmount` `:3460-3462`, `UpdateAgentProperties()` `:3658` |
| `AgentDrivenProperties` | `MountSpeed` `:456`, `MountChargeDamage` `:480` |
| `MapEvent` | `enum BattleTypes` `:24-37` has 11 members: None, FieldBattle, Raid, IsForcingVolunteers, IsForcingSupplies, Siege, Hideout, SallyOut, SiegeOutside, BlockadeBattle, BlockadeSallyOutBattle (**no `SiegeAmbush`**); `PlayerSide` `:109`; `EventType` `:196`; `IsFinalized => _state == MapEventState.WaitingRemoval` `:225`; `IsPlayerMapEvent` `:282`; `FinalizeEventAux` `:2057` sets `WaitingRemoval` `:2078` before `Component?.BeforeFinalizeComponent()` `:2138` |
| `MapEventParty` | `HealthyManCountAtStart` `:63` |
| `RaidEventComponent` | `MapEventSettlement` `:36`; `OnBeforeFinalize` `:128-138` sets the village Looted (`:132`) then raises `RaidCompleted` (`:138`) |
| Hideout | `HideoutCampaignBehavior` `:229` raises `OnHideoutBattleCompleted` from the success menu; `HideoutEventComponent.OnBeforeFinalize` `:59-64` raises it again |
| `Village` | `enum VillageStates` `:16-22` (includes Looted); `VillageState` `:51` |
| `MobileParty` | `All` `:258`, `SpeedExplained` `:400`, `IsCurrentlyAtSea` `:482`, `Army` `:660`, `LeaderHero` `:776`, `SeeingRangeExplanation` `:890`, `MoraleExplained` `:971`, `MemberRoster` `:1072`, `IsMilitia` `:1235`, `IsVillager` `:1241`; `Army.LeaderParty` `:118` |
| `PartyBase.PartySizeLimit` | `:343-355` caches `(int)GetPartyMemberSizeLimit(this).ResultNumber` keyed on `MemberRoster.VersionNo`; `TroopRoster.UpdateVersion()` `:731` |
| Probe signatures (`CareerPerkCheats`) | `CharacterStatsModel.MaxHitpoints(CharacterObject, bool)` `:9`; `PartySizeLimitModel.GetPartyMemberSizeLimit(PartyBase, bool)` `:14`; `PartyWageModel.GetTotalWage(MobileParty, TroopRoster, bool)` `:13`; `PartyHealingModel.GetDailyHealingHpForHeroes(PartyBase, bool, bool)` `:16`; `ClanTierModel.GetCompanionLimit(Clan)` `:34`; `InventoryCapacityModel.CalculateInventoryCapacity(MobileParty, bool isCurrentlyAtSea, bool, ...)` `:9`; `ExplainedNumber.GetLines()` returns `List<(string name, float number)>` `:187` |
| Quests | `QuestManager.OnPlayerCharacterChanged` `:282-292` cancels only `IsOngoing && !IsSpecialQuest` with `{=bYdhYidf}`; `QuestBase.IsOngoing` `:63`, `IsSpecialQuest` `:87`, `CompleteQuestWithCancel(TextObject cancelLog = null)` `:223`; 1.4.x `CareerQuest.SpecialQuestType => "taom_career_quest"` (`CareerQuest.cs:79`), so vanilla never cancels it; `CampaignTime.ToMilliseconds` is a `double` `:132`; `JournalLogEntry.IsRelatedTo` matches `StringId` `:119-122` |
| Career button | `CharacterDeveloperVM.ExecuteDone()` `:731-735` calls the close callback; `GauntletCharacterDeveloperScreen` passes `CloseCharacterDeveloperScreen` (`:92`), which plays `event:/ui/default` (`:139-143`) |
| UIExtenderEx 2.13.2 (the version this line compiles against) | `ViewModelComponent` adds a mixin method to the view model only when it carries `[DataSourceMethod]` (ilspycmd on the package's `Bannerlord.UIExtenderEx.dll`) |
| Localization in tests | `MBTextManager._activeTextLanguageId = "English"` by default (`:36`), and `GetLocalizedText` returns the default text of a `{=id}text` string for English (`:264-267`), so `new TextObject("{=x}Root perk text").ToString()` gives `Root perk text` in a unit test |
| `GameStateManager.CleanStates` | exists, `:250` |
| #614 caller | v1.4.8 dump `PartyBase.CalculateVisibilityAndInspected` (`PartyBase.cs:1063`, call at `:1086`) is the only caller of `GetPartySpottingRatioForMainPartySeeingRange`; 1.5.x docs name it `CalculateMobilePartyVisibilityAndInspected` |

### Line-number map: 1.5.x citations to v1.4.8 (apply where a step says "re-cite")

| 1.5.x text | Replace with |
|---|---|
| `GetAttackCollisionResults:200` | `GetAttackCollisionResults:192, v1.4.8` |
| `SandboxAgentStatCalculateModel.cs:209` | `SandboxAgentStatCalculateModel.cs:188, v1.4.8` |
| `Agent.cs:3496` | `Agent.cs:3462, v1.4.8` |
| `(v1.5.5)` in the `bc59806f0` comment | `(v1.4.8 MapEvent.cs:2078, then :2138)` |
| `(v1.5.5 :127-131)` in `CareerButtonClickTests` | `(v1.4.8 :139-143)` |
| `CalculateMobilePartyVisibilityAndInspected` (docs) | `PartyBase.CalculateVisibilityAndInspected` |

### Conventions that bind this work

- ADR-007 adapters: services never touch sealed TaleWorlds types; `PartySizeCacheInvalidator` lives in
  `Main/Adapters` for that reason. ADR-002 thin entry points (under 150 lines, delegate to services):
  `TaomAgentStatCalculateModel` stays a boundary that extracts primitives. ADR-003 no `#region`,
  ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG` outside IoC registration.
- `.claude/rules/gamemodels.md` rule 4: no inline branching in model overrides; the decisions live in
  `CareerAgentStatService` and `CareerAmmoApplier`.
- TDD: a failing test before new code. The hand-written steps (C2, C5, C6) order RED before GREEN;
  the cherry-picked steps bring the tests 1.5.x wrote with the fix.
- MCM json2 rule (rename a persisted setting, never flip its default): no step adds or changes an MCM
  setting.
- `.claude/rules/harmony-patches.md`: no step adds a Harmony patch or a `MovementOrder` postfix.
- D9: delete every `[TestCategory("RequiresGame")]` line from a ported test; keep
  `[TestCategory("BindingVerification")]`.

## Commands you will need

Run from the worktree root. Both `-p:` flags are mandatory on every build and test
(`Main/TAOM.csproj:133-136` does not gate CopyModule on DisableModuleCopy). Never `./build.ps1`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | 0 failed, the count the step names |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Docs | `python tools/lint_docs.py` | no new finding in the files you touched |
| Dash scan | the command below | no output |

```bash
git diff bannerlord-1.4.5...HEAD -- docs .claude | python -c "import sys; [print(l) for l in sys.stdin.buffer.read().decode('utf-8','replace').splitlines() if l.startswith('+') and (chr(0x2013) in l or chr(0x2014) in l)]"
```

`BANNERLORD_GAME_DIR` must be set (the laptop sets it): `CareerMountBonusBindingTests` and
`CareerButtonClickTests` load the game assemblies and go Inconclusive without them. Inconclusive is
not a pass here.

## Scope

**In scope** (only these):

- Part A: `Main/Features/CareerSystem/Abilities/{CareerAgentStatService,ICareerAgentStatService,MissionAbilityExecutionContext}.cs`,
  new `Abilities/{CareerAmmoApplier,ActiveBuffsFormat}.cs`; `CareerPerkMissionBehavior.cs`,
  `CareerConfigProvider.cs`, `CareerPassiveService.cs`; `Domain/{AttackTypeMask,PassiveEffectConsumers}.cs`;
  `Models/{TaomAgentStatCalculateModel,TaomAgentApplyDamageModel}.cs`; new
  `Diagnostics/{CareerPerkConsumerMap,CareerPerkLogPolicy,CareerPerkReport,CareerPerkSnapshot}.cs`;
  new `Main/Features/DevConsole/Cheats/CareerPerkCheats.cs`;
  `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` (two XML comments); their tests
  (`CareerAgentStatServiceTests`, `CareerChoicesIntegrationTests`, `CareerConfigProviderTests`,
  `CareerPassiveServiceTests`, `CareerPerkOffThreadTests` if present, new `CareerMountBonusBindingTests`,
  `AttackTypeMaskTests`, `CareerPerkReportTests`, `CareerPerkMissionTeardownTests`); docs
  `career-system.md`, `dev-console.md`, `gamemodel-registry.md`, one row of `.claude/rules/gamemodels.md`,
  new `docs/reviews/rca-career-mount-bonuses-2026-09-17.md` and `rca-career-perks-2026-09-17.md`.
- Part B: `CareerConfigProvider.cs`, `CareerRegistry.cs`, `CareerCampaignBehavior.cs`,
  `CareerPassiveService.cs`, `CareerPassiveHero.cs`; `Main/Features/SpecialResources/**` (the five files
  above); `Main/Features/AiPartySize/AiPartySizeSettingsWatcher.cs`;
  `Main/Features/CulturalFeats/{Models/TaomPartySizeModel,Diagnostics/PartySizeLoadOrderDiagnostic}.cs`;
  new `Main/Adapters/{IPartySizeCacheInvalidator,PartySizeCacheInvalidator}.cs`;
  `taom_module_strings.xml`, `taom_career_strings.xml`, `taom_career_choices.xml` (one description),
  the 24 matching language files, 12 `tools/translation_cache/*.json`; their tests; docs
  `career-system.md`, `ai-party-size.md`, `cultural-feats.md`, `special-resources.md`, new
  `docs/reviews/rca-career-e1e3-2026-10-08.md`.
- Part C: `Main/Features/FieldCommission/**` (three files), `field_commission_config.json`;
  `CareerSystemIoC.cs`; `CharacterDeveloperCareerMixin.cs`, the Patch27 file; the three career quest
  files; `CoopSessionPolicy.cs` (one member, only if absent); `CareerEffectDisplayMap.cs`,
  `CareerScreenVM.cs`, `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` (one line); their
  tests (FieldCommission tests, `CareerArchetypeServiceTests`, new `CareerButtonClickTests`,
  `CareerQuestServiceTests`, new `CareerQuestWiringTests`, `CoopSessionPolicyTests`,
  `CareerEffectDisplayMapTests`, `CareerScreenVMTests`, new `CareerScreenPrefabBindingTests`);
  `TAOM.Tests/Migration/IlCallScanner.cs` (only if `Fingerprint` is absent); docs
  `field-commission.md`, `docs/modding/configs-balance.md`, `career-system.md`, `career-quest-system.md`,
  `gui-sprite-system.md`, `harmony-patch-registry.md` (Patch27 row), new RCAs
  `rca-field-commission-races-2026-09-17.md`, `rca-career-click-sound-971-2026-10-23.md`,
  `rca-career-quest-owner-change-2026-10-27.md`.

**Single-owner files**: `Main/SubModule.cs` is edited only in Step A2, at exactly two lines (the
`TaomAgentStatCalculateModel` construction and one `CareerPerkMissionBehavior` argument).
`Main/IoC.cs` is edited only in Step B3, one added registration in `RegisterCoreServices`.
`Main/TAOM.csproj` (SDK-style, new files compile by glob), `Directory.Build.props` and
`.claude/settings*.json` are NOT edited. If a step seems to need any of them, STOP.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `docs/reviews/lessons/*`,
`docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`,
`docs/reference/feature-map.md`, `tools/README.md` (WP142 to WP144 sync these); every Codex prompt
file the 1.5.x commits add; `Client/` (does not exist here); `docs/research/taom-headless-server.md`;
`docs/features/combat-mechanics.md` (its `2b1d1ef96` hunk documents #610, which this line lacks);
`docs/reviews/rca-cavalry-charge-2026-09-17.md` (#610's record, absent here);
`Main/Features/ArmourAcquisition/**` (feature absent); every Arthedain file of `b02467744`; every
redesign part of `bce566059` and `23799be82` (two-effect options, `TroopClassMask`, counters, forks);
the non-career hunks of `6ae924638` and `e0ad0db76` (other WPs own them);
`TAOM.Tests/Infrastructure/RepoPaths.cs` (WP104 owns it).

## Git workflow

1. Worktree, from the main checkout (Bash tool, D19):
   `cd /c/Users/mikew/source/repos/TAOM && git worktree add C:/Users/mikew/source/repos/taom-port/129 -b port148/129-career-fixes bannerlord-1.4.5`.
   Work only there. The folder is outside the session's working directory, so a `cd` does not persist
   between Bash calls: start EVERY command with `cd /c/Users/mikew/source/repos/taom-port/129 &&`.
   Do not touch `stash@{0}` or `.serena/project.yml` in the main checkout.
2. Per-port procedure (decisions brief). For a whole commit: `git cherry-pick -n <sha>`. Never let
   `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit (`GIT_EDITOR=true`
   on this laptop would commit the 1.5.x message unedited). Two equivalent forms take only some paths:
   - **Path-limited pick**: `git diff <sha>^ <sha> -- <paths> | git apply` (add `--3way` where the
     step says). It is `cherry-pick -n` restricted to those paths.
   - **New-file transplant**: `git show <sha>:<path> > <path>` for a file that does not exist here.
3. Bookkeeping: a conflicted `CHANGELOG.md` or lessons file: `git checkout --ours <path>` then
   `git add <path>`; a cleanly changed one: `git checkout HEAD <path>`. A path that does not exist
   here ("deleted by us"): `git rm -q -f <path>`. If the `block-dangerous-git` hook asks about one of
   these commands, approve it: it discards only the cherry-pick's own output in this worktree.
4. If `git status` reports a cherry-pick in progress after you resolved everything, run
   `git cherry-pick --quit` (keeps your index and worktree) before committing.
5. Commit: `git add <explicit paths>` (never `-A`, `-u` or `.`): exactly the files the step created
   or edited, which `git status --porcelain` must list and nothing else; then, in the NEXT separate
   Bash call, `git commit -F <message file>`. Write the message file outside the repo (for example
   `/c/Users/mikew/AppData/Local/Temp/wp129-<step>.txt`). Never commit through PowerShell.
6. Build and run the step's filtered tests after each commit; run the full suite at the end of each
   Part.
7. No push, PR, tag or merge. The integrator merges the branch.

Commit message template (each step gives the subject and the body; wrap the body at 72 columns):

```
<type>(<scope>): v2.0.28 - <description>        (at most 72 characters)

<body for 1.4.x readers, no em or en dashes, no Co-Authored-By or
"Generated with" line>

Research: <what was verified on v1.4.8>
Not-tested: <the in-game check owed>
Ported-from: <40-hex sha>                       (one line per 1.5.x commit)
```

## Step 0: Prepare and check prerequisites (no commit)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git log -1 --format='%h %s' && \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
ls Main/Composition/FeatureModules.cs; \
git log --oneline --grep=e0ad0db765d3f6f47cbce580eb8eb5662377d120 | head -1; \
git grep -n -E "_hitTallies|ApplyMountStatModifiers|AttackTypeMaskMatch|IPartySizeCacheInvalidator|ScaleEarned|PaysBattleCredit|IsFormerPlayerHeroQuest|RootPerkDescription" -- Main; echo "early-port grep exit $?"
```

**Verify**: `1`, `1`, the `FeatureModules.cs` path, one commit line (WP123), and for the last grep
no output and `exit 1`. Any other result: STOP.

Record (no STOP):

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && \
grep -c "_deferred" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs; \
ls TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs 2>&1; \
grep -c "public static IEnumerable<string> Fingerprint(MethodBase method, byte\[\] il)" TAOM.Tests/Migration/IlCallScanner.cs; \
grep -c "IsSinglePlayerAuthority" Main/Features/CoopInterop/CoopSessionPolicy.cs; \
git diff --quiet 1b00d1acd HEAD -- Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs; echo "stat model unchanged since 1b00d1acd: exit $?"
```

Note `WP106=yes` when the first count is non-zero, `FINGERPRINT=yes` when the third is `1`,
`SPAUTH=yes` when the fourth is non-zero. The last line must print `exit 0`; if it prints `exit 1`
another WP changed the stat model: STOP and report.

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
```

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
```

**Verify**: build exit 0; tests 0 failed. Record the total as `BASE_TOTAL` and the skipped count as
`BASE_SKIPPED`. Run `python tools/validate_moduledata.py` once and keep its ERROR count as
`BASE_DATA_ERRORS`.

## Part A: mount bonuses, typed perks and perk diagnostics (Steps A1 to A3)

### Step A1: Cavalry mount bonuses reach the mount (`2b1d1ef96`, #611)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 2b1d1ef96; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected unmerged paths (simulation onto `1b00d1acd`, this session): `CHANGELOG.md`,
`.claude/rules/gamemodels.md`, `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`,
`docs/reference/gamemodel-registry.md`, `docs/reviews/rca-cavalry-charge-2026-09-17.md`; plus
`Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` when `WP106=yes`. Any other unmerged
path: STOP. Everything else merges cleanly (`CareerAgentStatService.cs`, `ICareerAgentStatService.cs`,
`MissionAbilityExecutionContext.cs`, `PassiveEffectConsumers.cs`, the two test files,
`career-system.md`, `combat-mechanics.md`).

Resolve:

1. `git checkout --ours CHANGELOG.md && git add CHANGELOG.md`
2. `git rm -q -f docs/reviews/rca-cavalry-charge-2026-09-17.md` (the #610 record; absent here).
3. `git checkout HEAD docs/features/combat-mechanics.md` (its hunk documents #610's charge service
   and applier, which this line does not have).
4. `.claude/rules/gamemodels.md`: `git checkout HEAD .claude/rules/gamemodels.md`, then change the row
   ``| `TaomAgentStatCalculateModel` | `SandboxAgentStatCalculateModel` (SandBox) | `CareerSystem` |`` to
   ``| `TaomAgentStatCalculateModel` | `SandboxAgentStatCalculateModel` (SandBox) | `CareerSystem` (+ the career mount bonuses #611: mount-side rules key on `RiderAgent`, a mount's own `Character` is null) |``.
5. `docs/reference/gamemodel-registry.md`: `git checkout HEAD docs/reference/gamemodel-registry.md`; in the
   `TaomAgentStatCalculateModel` row replace the text `riding difficulty (#279) |` with
   ``riding difficulty (#279). Since #611 the career mount bonuses (`MountChargeDamage` passive, the Cavalry ability's mount speed and charge) apply on the mount's properties after base, keyed on the rider (`ApplyMountStatModifiers`; a mount's own `Character` is null) |``.
   In the same row replace the long dash after `deliberately EXEMPT` with a colon, so it reads
   `deliberately EXEMPT: remountable mid-battle`: the row is rewritten, so the Dash scan sees that old dash
   as a new line and would otherwise fail Part A's done criteria.
6. `TaomAgentStatCalculateModel.cs`: `git checkout HEAD Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` (the 1.4.x file; this works whether or not git flagged it), then:
   - insert immediately above `public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel`:
     ```csharp
     // 2026-09-17 (#611): the career mount bonuses (the MountChargeDamage passive, the Cavalry ability's
     // mount speed and charge) apply on the MOUNT's properties, where the engine reads them, keyed on the
     // rider: a mount agent's own Character is null.
     ```
   - in `UpdateAgentStats`, after the statement that ends `: agentDrivenProperties.MountDifficulty;`
     and before the method's closing brace, insert:
     ```csharp

             // Mount-side career bonuses (#611) ride the MOUNT's properties, after base rewrote them; the
             // rider is the identity (a mount's own Character is null). Non-mounts pass two nulls and the
             // service returns at once.
             var mountRider = agent.IsMount ? agent.RiderAgent : null;
             _agentStatService.ApplyMountStatModifiers(
                 riderHeroId: (mountRider != null && mountRider.IsHero) ? (mountRider.Character as CharacterObject)?.HeroObject?.StringId : null,
                 riderAgentIndex: mountRider?.Index,
                 agentDrivenProperties);
     ```
7. Only when `CareerPerkMissionBehavior.cs` is unmerged: `git checkout --ours` it, then in the
   fallen-hero ally loop (`foreach (var allyIndex in buffedAllies)`, inside `EndAbilitiesNow` when
   WP106 moved it there, else inside `OnAgentRemoved`) replace
   ```csharp
                       if (ally != null && ally.IsActive())
                           ally.UpdateAgentProperties();
   ```
   with
   ```csharp
                       if (ally != null && ally.IsActive())
                       {
                           ally.UpdateAgentProperties();
                           ally.MountAgent?.UpdateAgentProperties(); // #611: the mount carries the buff's mount fields
                       }
   ```
8. Create `docs/reviews/rca-career-mount-bonuses-2026-09-17.md` with exactly this text (the 1.5.x
   record of #611 lives inside the #610 RCA, which this line does not carry):

   ```markdown
   # RCA: career mount bonuses never reached the mount (#611), 2026-09-17

   Ported to the 1.4.x line by plan 129 (port148 programme, 2026-11). On the 1.5.x line this fix is
   recorded as a follow-up section of the cavalry charge RCA, which covers #610, a change this line
   does not carry; this file keeps the #611 part on its own.

   ## What was wrong

   `CareerAgentStatService` multiplied `MountChargeDamage` and `MountSpeed` on the rider's driven
   properties at three sites: the `MountChargeDamage` passive, the Cavalry ability's self buff and its
   ally buff, all behind `if (!isHuman) return;`. No base model writes either value on a human
   (v1.4.8 `SandboxAgentStatCalculateModel.UpdateHorseStats` writes them at :1216, :1221 and :1230),
   so the rider's copies stayed 0, and 0 times (1 + bonus) is 0. The engine reads the attacker's copy
   (`AttackInformation.cs:335`), and the charge callback's attacker is the horse
   (`Mission.ChargeDamageCallback`, :6034). The 92 `MountChargeDamage` pips of the 1.4.x data and the
   mount half of the Cavalry ability never did anything, while `PassiveEffectConsumers` listed the
   type as consumed.

   ## Fix

   `ICareerAgentStatService.ApplyMountStatModifiers(riderHeroId, riderAgentIndex, mountProps)` applies
   the passive and both buff sources on the mount's properties. `TaomAgentStatCalculateModel.UpdateAgentStats`
   reads the rider off a mount and calls it after base has rewritten both values. The buff apply and
   restore paths and the fallen-hero ally loop refresh `MountAgent` beside the human.

   ## Why missed

   The consumer set answers "is anything reading it", not "is it read where the engine looks": the
   blind spot `PassiveEffectConsumers.cs` names, after #394 and #395.

   ## Deferred, by decision

   The bonus keys on the horse's current rider when the engine recomputes the horse, as vanilla's own
   Riding skill bonuses on the horse do, so a rider who dies or changes horse during a buff leaves the
   first horse on its last numbers until the engine recomputes it. Snapshotting the horse would mean
   holding agent handles across frames (the #592 class).

   ## Verification

   `CareerAgentStatServiceTests` (each source on the mount, stacking, no rider, a non-hero rider, a
   hero rider without an index, a negative bonus, the human path leaving both mount values alone) and
   `CareerMountBonusBindingTests` (IL pins). The in-game check is owed (plan 129).
   ```

**Verify**:
- `git status --porcelain | grep -c "^UU\|^AA\|^DU\|^UD"` prints `0`.
- `grep -c "ApplyMountStatModifiers" Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` prints `1`;
  `grep -c "agent.RiderAgent" Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` prints `2`.
- `grep -c "MountAgent?.UpdateAgentProperties" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `1`.
- `grep -c "props.MountChargeDamage\|props.MountSpeed" Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs` prints `0`.
- Build exit 0. Filtered `CareerAgentStatServiceTests|CareerMountBonusBindingTests`
  (`--filter "FullyQualifiedName~CareerAgentStatServiceTests|FullyQualifiedName~CareerMountBonusBindingTests"`):
  `Failed: 0`, and `CareerMountBonusBindingTests` alone shows `Passed: 3`.

Do not stage `CHANGELOG.md` (it equals HEAD) or the removed RCA. Stage exactly:
`.claude/rules/gamemodels.md`; the six code files `Main/Features/CareerSystem/Abilities/{CareerAgentStatService,ICareerAgentStatService,MissionAbilityExecutionContext}.cs`,
`Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`, `Main/Features/CareerSystem/Domain/PassiveEffectConsumers.cs`,
`Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`; the two test files
`TAOM.Tests/Features/CareerSystem/{CareerAgentStatServiceTests,CareerMountBonusBindingTests}.cs`;
`docs/features/career-system.md`, `docs/reference/gamemodel-registry.md` and
`docs/reviews/rca-career-mount-bonuses-2026-09-17.md`. Commit:

```text
fix(career): v2.0.28 - cavalry mount bonuses reach the mount (#611)

The MountChargeDamage career passive and the mount half of the Cavalry
ability (mount speed and charge damage) were multiplied on the rider's
own driven properties. No base model writes those two values on a
human and the engine reads them off the horse, so the 92 pips and the
Cavalry ability's mount buff never did anything.

ApplyMountStatModifiers now applies the passive and both buff sources
on the mount's properties, keyed on its rider. The stat model calls it
after base has rewritten the mount, and the ability and fallen-hero
paths refresh the rider's mount beside the rider.

Research: taom-src v1.4.8 SandboxAgentStatCalculateModel
 UpdateHorseStats (:1216, :1221, :1230), AttackInformation (:335),
 Mission.ChargeDamageCallback (:6034), Agent.RiderAgent and MountAgent
Not-tested: a Cavalry career ability and a MountChargeDamage pick in
 battle (the horse's charge damage and speed rise)
Save-compat: none, mission-time only
Ported-from: 2b1d1ef96633676a130cd9064e5d112ace584a12
```

### Step A2: Typed resistances, ammo, perk diagnostics (`7ede923be`, #613)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 7ede923be; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected unmerged paths: `CHANGELOG.md`, `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`,
`Main/SubModule.cs`, `docs/reference/gamemodel-registry.md`,
`docs/reviews/lessons/adapters-taleworlds-api.md`, `docs/reviews/lessons/testing-qa.md`; plus
`CareerPerkMissionBehavior.cs` when `WP106=yes`. Any other unmerged path: STOP. The new files
(`ActiveBuffsFormat.cs`, `CareerAmmoApplier.cs`, the four `Diagnostics/*.cs`, `CareerPerkCheats.cs`,
`AttackTypeMaskTests.cs`, `CareerPerkReportTests.cs`, `docs/reviews/rca-career-perks-2026-09-17.md`)
and the edits to `CareerAgentStatService.cs`, `ICareerAgentStatService.cs`, `CareerConfigProvider.cs`,
`CareerPassiveService.cs`, `AttackTypeMask.cs`, `PassiveEffectConsumers.cs`,
`TaomAgentApplyDamageModel.cs`, `taom_ability_tuning.xml`, the test files, `career-system.md` and
`dev-console.md` merge cleanly.

Resolve:

1. `git checkout --ours CHANGELOG.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/testing-qa.md && git add CHANGELOG.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/testing-qa.md`
2. `docs/reference/gamemodel-registry.md`: `git checkout HEAD docs/reference/gamemodel-registry.md` (your Step A1 version); in the
   `TaomAgentStatCalculateModel` row append, before the final ` |`:
   ``. Since #613 also `InitializeMissionEquipment`: the Ammo passive raises a hero's consumable stacks the way vanilla's own perks do (`CareerAmmoApplier`, amount and max together, before build)``.
3. `TaomAgentStatCalculateModel.cs`: `git checkout HEAD Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` (your Step A1 version), then:
   - after `using TaleWorlds.MountAndBlade;` add `using TAOM.Core.Logging;`
   - after the Step A1 header comment (its last line ends `own Character is null.`) add
     `// Same day, #613: the Ammo passive rides InitializeMissionEquipment, the seam vanilla's own extra-ammo perks use.`
   - after `private readonly IMumakilAttackService _mumakil;` add `private readonly IModLogger? _logger;`
   - change the constructor signature's closing `IMumakilAttackService mumakil)` to
     `IMumakilAttackService mumakil, IModLogger? logger = null)` and add `_logger = logger;` after
     `_mumakil = mumakil;` (1.4.x has no `ICultureAggressionService` or `IChargeDamageService`
     parameters; add only the logger)
   - after the constructor's closing brace insert:
     ```csharp

         // The Ammo passive (#613) rides the seam vanilla's own extra-ammo perks use: before the agent is
         // built, raising the stack's max with the amount. Heroes only; CareerAmmoApplier has the evidence.
         public override void InitializeMissionEquipment(Agent agent)
         {
             base.InitializeMissionEquipment(agent);
             if (agent != null && agent.IsHero)
             {
                 var heroId = (agent.Character as CharacterObject)?.HeroObject?.StringId;
                 CareerAmmoApplier.Apply(agent, heroId, _agentStatService.AmmoBonus(heroId), _logger);
             }
         }
     ```
4. `Main/SubModule.cs`: `git checkout HEAD Main/SubModule.cs` (whether or not git flagged it), then make exactly two edits:
   - `campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerAgentStat, elephantAttackService, spiderAttackService, mumakilAttackService));`
     becomes
     `campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerAgentStat, elephantAttackService, spiderAttackService, mumakilAttackService, IoC.Resolve<IModLogger>()));`
   - inside the `new Features.CareerSystem.CareerPerkMissionBehavior(` argument list,
     `IoC.Resolve<Features.CareerSystem.ICareerPassiveService>(),` becomes
     `IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>(),`
5. Only when `CareerPerkMissionBehavior.cs` is unmerged: `git checkout --ours` it and make these edits
   (the merge already made them when it was clean):
   - field `private readonly ICareerPassiveService _passives;` becomes
     `private readonly ICareerAgentStatService _agentStats;`
   - delete the line `private bool _buildFailureLogged;`
   - constructor parameter `ICareerPassiveService passives,` becomes `ICareerAgentStatService agentStats,`
     and `_passives = passives;` becomes `_agentStats = agentStats;`
   - delete the whole `public override void OnAgentBuild(Agent agent, Banner banner)` method and the
     three comment lines above it (the first starts `// Ammo career passive`)
   - in `OnEndMission`, directly after the `try`/`catch` pair around `CareerAbilityBuffTracker.ClearAll();`, insert:
     ```csharp

             // #613: the [CareerPerks] dedupe lives on the singleton stat service; without this the next
             // battle's spawn logs nothing when the values match the last one.
             try { _agentStats.ResetDiagnostics(); }
             catch (Exception ex) { _logger?.LogWarning($"CareerSystem: OnEndMission ResetDiagnostics() threw: {ex.Message}"); }
     ```
6. When `TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs` exists (WP106): replace
   `Substitute.For<ICareerPassiveService>(),` with `Substitute.For<ICareerAgentStatService>(),`
   and make sure the file has `using TAOM.Features.CareerSystem.Abilities;`.
7. Re-cite in the code and test files only (leave the docs for Step C6):
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && sed -i 's/GetAttackCollisionResults:200/GetAttackCollisionResults:192, v1.4.8/g; s/SandboxAgentStatCalculateModel\.cs:209/SandboxAgentStatCalculateModel.cs:188, v1.4.8/g; s/Agent\.cs:3496/Agent.cs:3462, v1.4.8/g' Main/Features/CareerSystem/Abilities/CareerAmmoApplier.cs Main/Features/CareerSystem/Domain/AttackTypeMask.cs Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs TAOM.Tests/Features/CareerSystem/AttackTypeMaskTests.cs TAOM.Tests/Features/CareerSystem/CareerMountBonusBindingTests.cs
   ```
8. Append to the new `docs/reviews/rca-career-perks-2026-09-17.md`:
   ```markdown

   ## 1.4.x port (plan 129)

   Ported to the 1.4.x line by plan 129 of the port148 programme. Engine line numbers above come from
   the 1.5.x engine the review ran on. On v1.4.8 (taom-src) vanilla's Blunt correction is
   `MissionCombatMechanicsHelper.cs:192`, the extra-ammo `SetAmountOfSlot` call is
   `SandboxAgentStatCalculateModel.cs:188` and the pickup cap is `Agent.cs:3462`.
   ```

**Verify**:
- No unmerged path; `git grep -n -E "GetAttackCollisionResults:200|SandboxAgentStatCalculateModel\.cs:209|Agent\.cs:3496" -- Main TAOM.Tests` prints nothing.
- `grep -c "public override void OnAgentBuild\|_buildFailureLogged\|ICareerPassiveService" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `0`;
  `grep -c "_agentStats.ResetDiagnostics()" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `1`.
  With `WP106=yes`, `grep -c 'RunOrDefer\|_deferred.Drain\|_deferred.Clear\|MarkMainThread' Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` still prints `5`.
- `grep -c "IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>()," Main/SubModule.cs` prints `1`;
  `grep -c "mumakilAttackService, IoC.Resolve<IModLogger>()));" Main/SubModule.cs` prints `1`.
- Build exit 0. Filtered `CareerSystem` (`--filter "FullyQualifiedName~TAOM.Tests.Features.CareerSystem"`):
  `Failed: 0`; `AttackTypeMaskTests` `Passed: 10`, `CareerPerkReportTests` `Passed: 6`,
  `CareerMountBonusBindingTests` `Passed: 4`; filtered `ConsoleCommandBindingTests`: `Failed: 0`.
- `python tools/validate_moduledata.py`: ERROR count equals `BASE_DATA_ERRORS` (the XML change is two comments).

Commit (stage the new and changed files the pick and your edits produced; the lessons and
`CHANGELOG.md` equal HEAD and are not staged):

```text
fix(career): v2.0.28 - typed resistances, ammo, perk diagnostics (#613)

Five resistance pips authored a Blunt or Cut mask, which the mask enum
did not have, so they parsed as All and resisted every hit. The mask
now has a damage-kind axis (Cut, Pierce, Blunt) read from the engine's
damage type, corrected the way vanilla corrects it: a bare hand, a
haft, a kick or bash, a fall and a horse charge count as Blunt. An
unknown mask name warns at load and leaves the pip inert.

The Ammo passive skipped thrown weapons and pushed stacks above an
unchanged maximum after spawn. It now runs where vanilla's own extra
ammo perks run, in InitializeMissionEquipment, raising the maximum with
the amount, and covers javelins.

The new console command taom.career_perks lists every passive the
player holds, where it is consumed and what the engine holds for it,
and the debug log carries [CareerPerks] lines for stat, mount and ammo
application.

Research: taom-src v1.4.8 MissionCombatMechanicsHelper (:157, :192),
 AttackCollisionData.DamageType (:112), MissionEquipment.SetAmountOfSlot
 (:506), SandboxAgentStatCalculateModel.InitializeMissionEquipment
 (:93, SetAmountOfSlot at :188), WeaponComponentData.IsAmmo (:99)
Not-tested: taom.career_perks in a campaign and a battle; a blunt
 resistance career hit by a mace, a sword and a horse; a javelineer's
 stack at spawn
Save-compat: none, mission and console only
Ported-from: 7ede923be86130f4d74a274397350df1b3f2c7e1
```

### Step A3: Perk hit lines once per battle, then a summary (`e0ad0db76`, career half only)

WP123 ported the rest of `e0ad0db76` and skipped these hunks because it took #613 for 1.5.x-only.
They are the 1.5.x end state of the lines Step A2 just added. On this line `FileLogger.LogDebug`
enqueues every DEBUG line (`Main/Core/Logging/FileLogger.cs:80`), so without them every hit a
TroopDamage or TroopResistance pick touches writes a line.

Pre-check: `git grep -c "_hitTallies" -- Main/Features/CareerSystem` prints nothing (else STOP).

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git diff e0ad0db76^ e0ad0db76 -- Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs docs/features/career-system.md docs/features/dev-console.md > /c/Users/mikew/AppData/Local/Temp/wp129-a3.patch && git apply --check /c/Users/mikew/AppData/Local/Temp/wp129-a3.patch; echo "check exit=$?"
```

Expected `check exit=0` (the five files' context equals the Step A2 text: the "Testing a perk"
section and the console row are identical between `7ede923be` and `e0ad0db76^`). Then:

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git apply /c/Users/mikew/AppData/Local/Temp/wp129-a3.patch && git show e0ad0db76:TAOM.Tests/Features/CareerSystem/CareerPerkMissionTeardownTests.cs > TAOM.Tests/Features/CareerSystem/CareerPerkMissionTeardownTests.cs
```

If the check fails for a file, apply that file alone with `git apply --3way` (a three-way simulation
of these paths on top of `2b1d1ef96` and `7ede923be` merged with no conflict this session) and
resolve so the file keeps the Step A2 text plus the `e0ad0db76` additions; a failure you cannot
resolve that way: STOP.

Then:

1. `CareerAgentStatService.cs`: after `using TAOM.Core.Logging;` add `using TAOM.Core.Validation;`
   (`TallyHit` uses `FiniteFloatValidator`; 1.5.x added that using in `c79a58521`, which this line
   does not take).
2. `CareerPerkMissionBehavior.cs` (by hand): replace the Step A2 block (the two comment lines starting
   `// #613: the [CareerPerks] dedupe` and the `try`/`catch` around `_agentStats.ResetDiagnostics()`) with
   ```csharp
           // #613: the [CareerPerks] dedupe lives on the singleton stat service; without this the next
           // battle's spawn logs nothing when the values match the last one. It also writes the mission's hit summary.
           ResetStatDiagnostics("OnEndMission");
   ```
   and insert directly after `OnEndMission`'s closing brace:
   ```csharp

       // Plan 030: OnEndMission runs only through Mission.EndMission. A mission state finalised without it
       // (Mission.OnMissionStateFinalize removes every behavior) still reaches OnRemoveBehavior, so the hit
       // summary is written here too; after a normal end the tally is already empty and this writes nothing.
       public override void OnRemoveBehavior()
       {
           ResetStatDiagnostics("OnRemoveBehavior");
           base.OnRemoveBehavior();
       }

       private void ResetStatDiagnostics(string phase)
       {
           try { _agentStats.ResetDiagnostics(); }
           catch (Exception ex) { _logger?.LogWarning($"CareerSystem: {phase} ResetDiagnostics() threw: {ex.Message}"); }
       }
   ```
3. `CareerPerkMissionTeardownTests.cs`: delete the line `[TestCategory("RequiresGame")]` (D9). In
   the new "Per-hit summary lines" text of `docs/features/career-system.md`, delete
   `` (`RequiresGame`)`` after `` `CareerPerkMissionTeardownTests` `` to match.

**Verify**:
- `grep -c "_hitTallies" Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs` prints at least `1`;
  `grep -c "using TAOM.Core.Validation;" Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs` prints `1`;
  `grep -c "MountChargeMultiplier" Main/Features/CareerSystem/Abilities/*.cs` prints `0` for each file.
- `grep -c "ResetStatDiagnostics(" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `3`;
  `grep -c "public override void OnRemoveBehavior()" Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `1`.
- `grep -c "#### Per-hit summary lines" docs/features/career-system.md` prints `1`.
- Build exit 0. Filtered `CareerAgentStatServiceTests|CareerPerkMissionTeardownTests`: `Failed: 0`,
  `CareerPerkMissionTeardownTests` `Passed: 7`.

Commit (stage the two code files, `CareerPerkMissionBehavior.cs`, the two test files and the two docs):

```text
perf(career): v2.0.28 - perk hit lines once per battle with a summary

The per-hit [CareerPerks] amplification and reduction lines from #613
were written for every hit a passive changed. A TroopDamage or
TroopResistance pick touches every troop in the hero's party, and on
this line FileLogger writes every DEBUG line, so one battle could add
thousands of lines. Each combination of direction, hero or party
leader, hit mask and passives now writes its first hit at DEBUG and is
only counted after that; the mission end writes one INFO summary line
per combination. The summary is also written when the mission's
behaviours are removed without a normal end.

Research: taom-src v1.4.8 Mission.OnMissionStateFinalize removes every
 behaviour (:2207, :2219; OnRemoveBehavior at :4645); MissionBehavior
 OnEndMissionInternal (:121) and OnRemoveBehavior (:130)
Not-tested: a battle with a TroopDamage pick writes one hit amp line,
 then the summary at mission end
Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120
```

### Part A done criteria

- [ ] Build exit 0; full Test command: `Failed: 0`, total `BASE_TOTAL + 63`, skipped `BASE_SKIPPED`.
- [ ] `git log --oneline bannerlord-1.4.5..HEAD` shows exactly the three Part A commits, each subject with `v2.0.28 - `.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists no `CHANGELOG.md`, nothing under `docs/reviews/lessons/`, no `docs/features/combat-mechanics.md`, no `Main/IoC.cs`.
- [ ] `git grep -nE 'TestCategory\("RequiresGame"\)' -- TAOM.Tests/Features/CareerSystem` prints nothing.
- [ ] Dash scan prints nothing.

## Part B: the career review follow-ups (Steps B1 to B6)

Pre-check for the whole Part: `ls Main/Features/CareerSystem/Diagnostics/CareerPerkConsumerMap.cs Main/Features/DevConsole/Cheats/CareerPerkCheats.cs`
lists both (Part A landed). The classifier's notes said to drop the `CareerPerkConsumerMap.cs` and
`CareerPerkCheats.cs` hunks of `01ca483e2` and `c74ad5aed` because the files were absent; Step A2
created them, and neither file changed on 1.5.x between `7ede923be` and those commits
(`git diff --stat 7ede923be 01ca483e2^ -- <file>` and `7ede923be c74ad5aed^` are empty), so the hunks
are kept. A chained simulation (`2b1d1ef96`, `7ede923be`, then this series, onto `1b00d1acd`) showed
B1 to B4 merging with no conflict at all.

### Step B1: Link each career choice to its group (`259ae2cba`, #766)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 259ae2cba; echo "exit=$?"; git status --porcelain
```

Expected: `exit=0` and 22 changed paths (`CareerCampaignBehavior.cs`, `CareerConfigProvider.cs`,
`CareerRegistry.cs`, `taom_module_strings.xml`, 12 `std_taom_module_strings_*.xml`, six test files).
Any conflict: STOP.

**Verify**: build exit 0; filtered `TAOM.Tests.Features.CareerSystem`: `Failed: 0` (the new
`ShippedData_EveryOwnershipIdHasExactlyOneOwner` passes: the 1.4.x data has no doubly-owned id);
filtered `LanguageFileCoverageTests`: `Failed: 0`; Data: no new ERROR.

Commit (stage the 22 paths):

```text
fix(careers): v2.0.28 - link each career choice to its group (#766)

Career choices took their group from a group_id attribute that no
nested choice carries, so all 2,010 nested choices loaded with an
empty group. The tier lock on the career screen, the one keystone per
tier rule and the save repair for choices left from another career
never applied, and a level 1 hero could take a tier 3 choice. Each
choice now takes its group from the ChoiceGroup that holds it. An id
claimed by two careers gets no owner and the repair keeps it, and a
shipped-data test fails on such data. The player sees one message
when a repair refunds choices; its 12 translations show the English
until the next translation run.

Not-tested: the click refusals and the refund message in game
Ported-from: 259ae2cba5e460a59d9b27415911b30fa825e199
```

### Step B2: Career gain scales every earning (`01ca483e2`, #767)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 01ca483e2; echo "exit=$?"; git status --porcelain
```

Expected `exit=0`, eight changed paths: `CareerPerkConsumerMap.cs` (one line),
`DailyResourceBreakdown.cs`, `ISpecialResourceService.cs`, `SpecialResourceService.cs`,
`SpecialResourcesBehavior.cs`, `SpecialResourceMapBarMixin.cs`, `SpecialResourceBreakdownTests.cs`,
new `SpecialResourceGainTests.cs`. A conflict in `SpecialResourcesBehavior.cs` (WP119 edited the
SyncData and new-game region, not the daily tick this commit edits) or anywhere else: STOP.

**Verify**: build exit 0; filtered `SpecialResource`: `Failed: 0`, `SpecialResourceGainTests` `Passed: 24`.

Commit:

```text
fix(resources): v2.0.28 - career gain scales every earning (#767)

The SpecialResourceGain career passive scaled only the daily town
income, so a hero without towns got nothing from it. It now scales the
battle, raid, siege, prisoner, tournament and hideout earnings too, and
the map bar tooltip shows the scaled amounts. Daily upkeep is computed
in one place for the tooltip, the daily tick and desertion; a troop
whose career-reduced upkeep is zero no longer deserts, and an unusable
upkeep modifier falls back to the configured upkeep.

Not-tested: the scaled earnings and the tooltip rows in game
Ported-from: 01ca483e2e6538a8ac508eb288c06b5939e099fe
```

### Step B3: Party size picks apply at once, own party only (`c74ad5aed`, #768)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n c74ad5aed; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected `exit=0` and no unmerged path (simulated). It adds `Main/Adapters/IPartySizeCacheInvalidator.cs`
and `PartySizeCacheInvalidator.cs`, one registration in `Main/IoC.cs` `RegisterCoreServices` (after
the `ICampaignSessionAdapter` line), the new constructor parameters of `CareerPassiveService` and
`AiPartySizeSettingsWatcher` (both resolved by DryIoc from `CareerSystemIoC.cs:21` and
`AiPartySizeIoC.cs:10`), the leader-only `ApplyFlat` after the last `AddFactor` in
`TaomPartySizeModel`, the `CareerPerkCheats` cache probes, the Rohan text in
`taom_career_choices.xml`, `taom_career_strings.xml` and 12 language files, the removal of that
string's stale cached translation from 12 `tools/translation_cache/*.json` files, tests and docs.

Keep the translation cache removals: the translator's cache is keyed by string id
(`tools/translate_with_claude.py:716`, `:921`, `:955`), so a kept entry would restore the old
"garrison" translation over the corrected English at the next run.

If `docs/features/career-system.md` or `Main/IoC.cs` is unmerged after all: for the doc, take ours
and add the incoming `#766` and `#768` lines (the `PartySize` row, the "Group choices resolve through
their `<ChoiceGroup>`" paragraph, the "Accepted keystone trade" paragraph, the two Changelog entries
and the two `SpecialResource` rows); for `IoC.cs`, take ours and add exactly
`container.Register<IPartySizeCacheInvalidator, PartySizeCacheInvalidator>(Reuse.Singleton);` (with
its two comment lines) after `container.Register<ICampaignSessionAdapter, CampaignSessionAdapter>(Reuse.Singleton);`.
Any other unmerged path: STOP.

Then:

1. `TAOM.Tests/Features/SpecialResources/SpecialResourcesWiringTests.cs`: delete the line
   `[TestCategory("RequiresGame")]` (D9).
2. `docs/features/career-system.md`: in the "Accepted keystone trade" paragraph replace the sentence
   `The forks redesign removes the one-keystone-per-tier rule, so this is not being engineered around.`
   with `The 1.4.x line keeps the one-keystone-per-tier rule, so this stays a known limitation.`

**Verify**: `grep -c "container.Register<IPartySizeCacheInvalidator, PartySizeCacheInvalidator>(Reuse.Singleton);" Main/IoC.cs`
prints `1`; `grep -c "ApplyFlat(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySize)" Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs`
prints `1`; `grep -c "forks redesign" docs/features/career-system.md` prints `0`. Build exit 0;
filtered `CareerPassiveServiceTests|AiPartySize|SpecialResourcesWiringTests`: `Failed: 0`,
`SpecialResourcesWiringTests` `Passed: 3`; filtered `LanguageFileCoverageTests`: `Failed: 0`; Data:
no new ERROR.

Commit:

```text
fix(careers): v2.0.28 - party size picks apply at once, own party only

A PartySize career pick now changes the party limit at once (#768)
instead of after the next roster change: a passive refresh bumps the
cached limit of the parties the affected heroes lead. The bonus goes
only to the party its hero leads, so caravans, garrisons, militia,
villagers, patrols, supply caravans and refuges of the owner's clan
no longer get it, and it is added after every percentage factor, so
the AI lord party size setting no longer multiplies it. A party filled
to its old limit may be over the new one on an existing save; vanilla
desertion trims it over a few days.

The Rohan "Garrison Commander" text now reads "Your company swells
with volunteers"; its 12 translations show the English, and its cached
translations are removed so the next translation run redoes them.
The taom.career_perks report prints the engine's cached party limit.

Research: taom-src v1.4.8 PartyBase.PartySizeLimit caches on
 MemberRoster.VersionNo (:343-355); TroopRoster.UpdateVersion (:731);
 MobileParty.All (:258), LeaderHero (:776), Army.LeaderParty (:118)
Not-tested: the instant limit change and the console lines in game
Ported-from: c74ad5aed0c950247c0d187e21019b563885aa02
```

### Step B4: Raids stop paying the battle credit (`30cbcd465`, #770)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 30cbcd465; echo "exit=$?"; git status --porcelain
```

Expected `exit=0`; changed: `SpecialResourceEarnPolicy.cs`, `SpecialResourcesBehavior.cs`,
`SpecialResourceEarnPolicyTests.cs`. Build now: it FAILS with `CS0117` naming `SiegeAmbush` in
`SpecialResourceEarnPolicyTests.cs` (the 1.5.x engine has that battle type; v1.4.8 does not,
`MapEvent.cs:24-37`). Delete the line `        MapEvent.BattleTypes.SiegeAmbush,` from the
`KnownBattleTypes` array. Any other build error: STOP.

**Verify**: build exit 0; filtered `SpecialResourceEarnPolicyTests|SpecialResource`: `Failed: 0`
(`PaysBattleCredit_EnumStillHoldsExactlyTheValuesTheTestKnows` passes with the 11 v1.4.8 members).

Commit:

```text
fix(resources): v2.0.28 - raids stop paying the battle credit (#770)

A raid paid the battle credit on top of the raid credit, and a raid
where the militia fought first paid the raid credit twice. A raid now
pays the raid credit once, when the engine marks the village looted,
and only to the winner. A battle inside a raid or an extortion pays
the battle credit only when a real field party (neither militia nor
villagers, with healthy troops at the start) was beaten. Tournament,
hideout and raid messages show the amount actually credited. The
ported test lists v1.4.8's eleven battle types, without the 1.5.x
SiegeAmbush.

Research: taom-src v1.4.8 MapEvent.BattleTypes (:24-37), EventType
 (:196), MapEventParty.HealthyManCountAtStart (:63),
 RaidEventComponent.OnBeforeFinalize sets Looted, then raises
 RaidCompleted (:128-138)
Not-tested: a raid with a lord inside, defending a village, a raid
 against militia only
Ported-from: 30cbcd465844e98388123274f2fa7e5a0887277c
```

### Step B5: Hideout send-troops clear pays once (`bc59806f0`, #790; code)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n bc59806f0; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected: the code hunk applies; `docs/features/special-resources.md` is unmerged (its context is
`c96a02a4b`'s doc text, which Step B6 adds). Run
`git checkout --ours docs/features/special-resources.md && git add docs/features/special-resources.md`
(the doc lands in B6). Then in `SpecialResourcesBehavior.cs` replace the comment fragment
`just before that second raise (v1.5.5), so` with
`just before that second raise (v1.4.8 MapEvent.cs:2078, then :2138), so`.

**Verify**: `grep -c "if (!component.MapEvent.IsFinalized) return;" Main/Features/SpecialResources/SpecialResourcesBehavior.cs`
prints `1`; `grep -c "v1.5.5" Main/Features/SpecialResources/SpecialResourcesBehavior.cs` prints `0`;
build exit 0; filtered `SpecialResource`: `Failed: 0`.

Commit (stage only `SpecialResourcesBehavior.cs`):

```text
fix(resources): v2.0.28 - hideout send-troops clear pays once (#790)

A "send troops" hideout clear raises the completed event twice, from
the success menu and again when the map event is finalized, and both
paid the hideout reward. Only the finalized event pays now, so the
reward is paid once, also after a save made in the success menu.

Research: taom-src v1.4.8 HideoutCampaignBehavior (:229) and
 HideoutEventComponent.OnBeforeFinalize (:59-64) both raise
 OnHideoutBattleCompleted; MapEvent.FinalizeEventAux sets
 WaitingRemoval (:2078) before BeforeFinalizeComponent (:2138);
 IsFinalized (:225)
Not-tested: one send-troops clear writes one [SpecRes] HIDEOUT: line
Ported-from: bc59806f097a749d3125ea43fe50a911cc49a2bd
```

### Step B6: Resource docs and the review record (`c96a02a4b` docs, `86ef5c7af`, `4f5b412d7`)

`c96a02a4b`'s code is skipped: this line fixed #771 in its own way in `1b00d1acd` (a static owner
instance with remove-before-add in `SpecialResourcesBehavior`, remove-before-add in
`TroopCountDiagnosticsBehavior`), and `special-resources.md` already has its "Campaign reload
subscriptions" section for it.

1. Special resources doc:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git diff c96a02a4b^ c96a02a4b -- docs/features/special-resources.md | git apply --3way; git diff --name-only --diff-filter=U
   ```
   Resolve every conflict block by keeping this line's text and adding the incoming text that
   describes #767 or #770 (the Earn policy, Desertion, One daily breakdown and Career passive
   integration bullets, the faction lines, the "gain passive" sentence, the earn-policy Key Files row,
   the `SpecialResourceGainTests.cs`, `SpecialResourceBreakdownTests.cs` and `SpecialResourceEarnPolicyTests.cs`
   lines in Tests with every test count removed (the 1.5.x counts are not this line's; write a count
   only if you measure it with `grep -c "\[TestMethod\]\|\[DataRow("`), the Earning Rules sentence and
   its "What each event pays" block, the Desertion Mechanics bullet, and the `2026-10-08 (#767)` and
   `2026-10-09 (#770)` Changelog lines). Remove the incoming "## Party-screen subscription lifecycle"
   section and the incoming `2026-10-08 (#771)` Changelog line: they describe the 1.5.x
   implementation. Then apply `bc59806f0`'s doc text by hand: append to the "Hideout clear" bullet
   ` A "send troops" clear raises the completed event twice (from its success menu, then at finalize); only the finalized event pays per_hideout_clear, so it pays once, even across a save made in the success menu (#790).`
   and add the Changelog line `- 2026-10-09 (#790): a "send troops" hideout clear paid per_hideout_clear twice; it now pays only when the hideout event is finalized. No unit test covers it (campaign entry point). Owed in game: one send-troops clear writes one [SpecRes] HIDEOUT: line, also after saving in its success menu and loading.`
   above the `(#770)` line.
2. Review record:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git show 4f5b412d7:docs/reviews/rca-career-e1e3-2026-10-08.md > docs/reviews/rca-career-e1e3-2026-10-08.md
   ```
   and append:
   ```markdown

   ## 1.4.x port (plan 129)

   Ported to the 1.4.x line by plan 129 of the port148 programme. The review above ran against the
   1.5.x engine; the port re-checked the engine facts the fixes rely on against v1.4.8 (the port
   commits' Research trailers list them). #771 shipped on this line in a different shape
   (`1b00d1acd`: a static owner instance with remove-before-add), so its code was not ported. #774
   (War of the Ring momentum counts a resisted raid twice) is still open on this line.
   ```
   Skip `docs/reviews/codex-adversarial-career-e1e3-2026-10-09.prompt.md`, the REVIEW-LOG row and
   every lessons hunk of the three commits (WP142 syncs lessons).
3. Stage both docs: `git add docs/features/special-resources.md docs/reviews/rca-career-e1e3-2026-10-08.md`.

**Verify**: no unmerged path; `grep -c "ScaleEarned" docs/features/special-resources.md` prints at
least `2`; `grep -c "PaysBattleCredit" docs/features/special-resources.md` at least `1`;
`grep -c "#790" docs/features/special-resources.md` at least `2`;
`grep -c "OnPushScreenEvent" docs/features/special-resources.md` prints `0`;
`grep -c "## Campaign reload subscriptions" docs/features/special-resources.md` prints `1`;
`git diff --cached --name-only` lists only the two docs. Dash scan prints nothing; Docs: no new
finding.

Commit:

```text
docs(careers): v2.0.28 - RCA and resource docs for the career fixes

The special resources feature doc now describes the scaled earnings,
the single upkeep computation, the raid and extortion payout rule and
the once-only hideout payout. The review record of the career fixes
(#766 to #790) is added with a note on what the 1.4.x port
re-checked. This line's own party-screen handler fix for #771 keeps
its existing doc section.

Ported-from: c96a02a4bfea3960760ebbde49567958b43989ee
Ported-from: 86ef5c7af1137910c4f198d44716476890772ef3
Ported-from: 4f5b412d7dd1ad126410466bb8a35ef2cf916ea7
```

### Part B done criteria

- [ ] Build exit 0; full Test command: `Failed: 0`, total `BASE_TOTAL + 117`, skipped `BASE_SKIPPED`.
- [ ] `git log --oneline bannerlord-1.4.5..HEAD` shows the three Part A and six Part B commits.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists no `CHANGELOG.md`, nothing under
  `docs/reviews/lessons/`, no `docs/reviews/REVIEW-LOG.md`, no `TroopCountDiagnosticsBehavior.cs`.
- [ ] `python tools/validate_moduledata.py`: ERROR count equals `BASE_DATA_ERRORS`.
- [ ] Dash scan prints nothing.

## Part C: promotions, archetypes, career button, heir quests, career screen (Steps C1 to C6)

Pre-check: Part A landed (`ls Main/Features/CareerSystem/Diagnostics/CareerPerkConsumerMap.cs`).

### Step C1: Promote every soldier race (`35007dbb8`, #612)

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 35007dbb8; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected unmerged: `CHANGELOG.md`, `docs/reviews/lessons/data-content-cultures.md`,
`TAOM.Tests/Infrastructure/RepoPaths.cs` (both sides added it; WP104 owns it); possibly
`docs/features/field-commission.md` and `Main/Features/FieldCommission/FieldCommissionMeritService.cs`
(WP106 edited other parts of both) and
`TAOM.Tests/Features/Enlistment/EnlistmentDiagnosticsSettingsProviderTests.cs` (WP123 edited it).
Resolve:

1. `git checkout --ours CHANGELOG.md docs/reviews/lessons/data-content-cultures.md TAOM.Tests/Infrastructure/RepoPaths.cs && git add CHANGELOG.md docs/reviews/lessons/data-content-cultures.md TAOM.Tests/Infrastructure/RepoPaths.cs`
2. `git checkout HEAD TAOM.Tests/Features/Enlistment/EnlistmentDiagnosticsSettingsProviderTests.cs`
   (its hunk only swaps a local helper for RepoPaths; unrelated to #612). If git calls it unmerged,
   use `git checkout --ours` and `git add` instead.
3. `field-commission.md` or `FieldCommissionMeritService.cs` unmerged: keep both sides (this line's
   text plus the incoming #612 additions: the race allow-list bullet, the config table row, the
   2026-09-17 Changelog line; in the service, the `_raceNamesChecked` field, the
   `WarnUnknownRaceNamesOnce(allowed);` call and the method).
4. `git diff HEAD --stat -- TAOM.Tests/Infrastructure/RepoPaths.cs` prints nothing.
5. Append to the new `docs/reviews/rca-field-commission-races-2026-09-17.md`:
   ```markdown

   ## 1.4.x port (plan 129)

   Ported to the 1.4.x line by plan 129. The shared `RepoPaths` reader this record mentions reached
   this line earlier, through plan 104, so the port took only the field-commission change. The engine
   facts above were read on the 1.5.x DLLs; the 1.4.x port relies on `IRaceManager.IsValidRaceName`
   (`Main/Core/Domain/RaceManager.cs`) and on `raceage/race_age_config.json`, which lists all ten
   races of the new default on this line.
   ```

**Verify**: build exit 0; filtered `FieldCommission`: `Failed: 0` (three new tests:
`ShippedJson_AllowedRaceNames_MatchCompiledDefault`, `ShippedJson_AllowedRaceNames_AreAllKnownRaces`,
`CanPromote_ConfiguredRaceNameUnknownToEngine_WarnsOnceNamingTheEntry`); Data: no new ERROR.

Commit (stage the three FieldCommission code files, the JSON, three FieldCommission test files,
`field-commission.md`, `docs/modding/configs-balance.md`, `docs/reviews/rca-field-commission-races-2026-09-17.md`):

```text
fix(field-commission): v2.0.28 - promote every soldier race (#612)

Battlefield promotion allowed only human, dwarf and elf soldiers, so a
player of any evil faction never saw an offer. The default list now
holds every soldier race (human, dwarf, elf, orc, uruk, uruk_hai,
pale_uruk, dg_uruk, goblin, berserker); creatures and unique heroes
stay out. The shipped JSON, the provider's fallback and the config
default are pinned together, every name is pinned to the races
race_age_config.json lists, and a configured name the engine does not
know logs one warning.

Not-tested: an Isengard or Mordor campaign gets a promotion offer
 after a won fair battle
Ported-from: 35007dbb8d0324efd71a49ce356636262225d9ab
```

### Step C2: Promoted-culture careers get their archetypes (`b02467744`, two hunks only; TDD)

Pre-check: `git diff --quiet 1b00d1acd HEAD -- Main/Features/CareerSystem/CareerSystemIoC.cs TAOM.Tests/Features/CareerSystem/CareerArchetypeServiceTests.cs; echo $?`
prints `0` (both files equal `b02467744^`, checked this session). Else STOP.

RED:

```bash
cd /c/Users/mikew/source/repos/taom-port/129 && git diff b02467744^ b02467744 -- TAOM.Tests/Features/CareerSystem/CareerArchetypeServiceTests.cs | git apply
```

Build, then filtered `CareerArchetypeServiceTests`: exactly one failure,
`IoCMap_EveryCareerInTheData_HasAnArchetype`, naming the 18 ids `goblin_troll_driver` to
`abanissa_ivory_rider`.

GREEN: in `Main/Features/CareerSystem/CareerSystemIoC.cs`, between `["fell_rider"]             = CareerArchetype.Cavalry,`
plus its following blank line and `// ═══ UMBAR ═══`, insert (do NOT add the ARTHEDAIN block of
`b02467744`; Arthedain is a 1.5.x-only kingdom):

```csharp
        // ═══ PROMOTED CULTURES (cc1713eb3) ═══ each clones a mapped career, and takes its archetype
        ["goblin_troll_driver"]        = CareerArchetype.Infantry,
        ["goblin_tunnel_stalker"]      = CareerArchetype.Ranged,
        ["goblin_warg_master"]         = CareerArchetype.Cavalry,
        ["misty_troll_goad"]           = CareerArchetype.Infantry,
        ["misty_deep_marksman"]        = CareerArchetype.Ranged,
        ["misty_warg_chieftain"]       = CareerArchetype.Cavalry,
        ["craig_pit_driver"]           = CareerArchetype.Infantry,
        ["craig_crag_stalker"]         = CareerArchetype.Ranged,
        ["craig_warg_master"]          = CareerArchetype.Cavalry,
        ["falathrim_blade_dancer"]     = CareerArchetype.Infantry,
        ["falathrim_sentinel"]         = CareerArchetype.Ranged,
        ["falathrim_mariner"]          = CareerArchetype.Cavalry,
        ["shaghana_zajana_tribesman"]  = CareerArchetype.Infantry,
        ["shaghana_chatak_javelineer"] = CareerArchetype.Ranged,
        ["shaghana_beast_rider"]       = CareerArchetype.Cavalry,
        ["abanissa_house_guard"]       = CareerArchetype.Infantry,
        ["abanissa_jiret_javelineer"]  = CareerArchetype.Ranged,
        ["abanissa_ivory_rider"]       = CareerArchetype.Cavalry,

```

(`cc1713eb3` is on this line: `git merge-base --is-ancestor cc1713eb3 HEAD` exits 0.) Add one line
at the top of the "## Changelog" list in `docs/features/career-system.md`:
`- 2026-11 (port of the 1.5.x archetype rows): the 18 careers of the six cultures promoted in cc1713eb3 (goblin, Misty Mountains, Blue Craig, Lindon, Shaghana, Abanissa) had no archetype, so CareerAbilityEffectRegistry.GetExecutor returned its NoOp executor and their battle ability applied nothing. Each maps to the archetype of the career it clones; CareerArchetypeServiceTests.IoCMap_EveryCareerInTheData_HasAnArchetype fails on an unmapped career.`

**Verify**: `grep -c "warden_of_fornost\|ranger_of_the_north\|knight_of_arthedain" Main/Features/CareerSystem/CareerSystemIoC.cs`
prints `0`; build exit 0; filtered `CareerArchetypeServiceTests`: `Failed: 0`.

Commit:

```text
fix(careers): v2.0.28 - promoted-culture careers get their archetypes

The 18 careers of the six cultures promoted in cc1713eb3 (goblin,
Misty Mountains, Blue Craig, Lindon, Shaghana, Abanissa) had no
archetype, so their battle ability played its toast and cooldown and
applied nothing. Each now maps to the archetype of the career it was
cloned from, and a test fails when any career in taom_careers.xml has
no archetype.

Not-tested: a goblin, Lindon or Abanissa career ability applies its
 buff in battle
Ported-from: b02467744d0edbf63dc0774131c3d8f199014577
```

### Step C3: Career button keeps one click handler (`0761c01b7`, `7100f7d8d` one line; #971)

1. Helper (orchestrator note, K1): when `FINGERPRINT=yes` skip this. Otherwise:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git diff --quiet 1b00d1acd HEAD -- TAOM.Tests/Migration/IlCallScanner.cs && git diff a27c6b338^ a27c6b338 -- TAOM.Tests/Migration/IlCallScanner.cs | git apply; echo "exit=$?"
   ```
   Expected `exit=0` (+94 lines: `Fingerprint`, `FingerprintToken`, `ShortTypeName`,
   `ComparisonOpCodes`). A failure: STOP. Later, if the build reports `CS0117` or `CS1061` on
   `IlCallScanner`, STOP: lift the missing helper, never rewrite the test.
2. ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git cherry-pick -n 0761c01b7; echo "exit=$?"; git diff --name-only --diff-filter=U
   ```
   Expected unmerged: `docs/reviews/lessons/adapters-taleworlds-api.md` and
   `docs/reviews/rca-taom-client-b5-2026-10-22.md` (absent here). Git's rename detection maps the
   1.5.x `Client/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` onto this line's
   `Main/` copy (the two files differ in one call), so the method deletion auto-merges into
   `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` (simulated this session) and no
   `Client/` path appears. Resolve:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git rm -q -f docs/reviews/rca-taom-client-b5-2026-10-22.md docs/reviews/codex-prereview-career-click-sound-971-2026-10-23.prompt.md && git checkout --ours docs/reviews/lessons/adapters-taleworlds-api.md && git add docs/reviews/lessons/adapters-taleworlds-api.md
   ```
   If a `Client/` path is reported after all, `git rm -q -f` it too.
3. Check `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs`: the line
   `using TaleWorlds.MountAndBlade.View;` and the whole `public void ExecuteOpenCareerScreen()`
   method (1.4.x `:100-115`, with the blank line above it) must be gone. If the merge left them,
   delete them by hand (nothing else in the file uses `UISoundsHelper`).
4. `TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs`: delete `[TestCategory("RequiresGame")]`
   and replace `(v1.5.5 :127-131)` with `(v1.4.8 :139-143)`.
5. `docs/features/career-system.md`: replace the line that starts `- **#378 button feel:**` with the
   line `git show 7100f7d8d:docs/features/career-system.md | grep -F -- '- **#378 button feel:**'`
   prints (the 1.4.x line is identical to the 1.5.x pre-image; use the Edit tool).
6. Append to the new `docs/reviews/rca-career-click-sound-971-2026-10-23.md`:
   ```markdown

   ## 1.4.x port (plan 129)

   Ported to the 1.4.x line by plan 129. Here the mixin lives in `Main/` (this line has no `Client/`
   project), and the UIExtenderEx build it compiles against (2.13.2) adds a mixin method only when it
   carries `[DataSourceMethod]`, as the finding above says of the 1.5.x build. On v1.4.8 vanilla's
   close callback is `GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen` (:139-143).
   ```

**Verify**: `grep -c "ExecuteOpenCareerScreen\|UISoundsHelper" Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs`
prints `0`; `ls Client 2>&1 | grep -c "No such file"` prints `1`;
`grep -c "Fingerprint(" TAOM.Tests/Migration/IlCallScanner.cs` at least `1`; build exit 0; filtered
`CareerButtonClickTests`: `Passed: 4` (none Inconclusive).

Commit (stage the mixin, the Patch27 file, `CareerButtonClickTests.cs`, `IlCallScanner.cs` if step 1
changed it, `gui-sprite-system.md`, `harmony-patch-registry.md`, `career-system.md`, the new RCA):

```text
fix(career): v2.0.28 - career button keeps one click handler (#971)

The career button had a second click handler in its view-model mixin
that never ran: UIExtenderEx adds a mixin method to the view model
only when it carries [DataSourceMethod]. The dead method is gone and
the Patch27 postfix, the handler that always ran, says so. The click
sound was never missing: the postfix's ExecuteDone runs the developer
screen's close callback, which plays it. New IL tests pin the order of
the two calls, the absence of a second sound and vanilla's click.

Research: UIExtenderEx 2.13.2 ViewModelComponent adds only
 [DataSourceMethod] methods; taom-src v1.4.8
 CharacterDeveloperVM.ExecuteDone (:731-735),
 GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen plays
 event:/ui/default (:139-143)
Not-tested: one click sound, and the career screen opens
Ported-from: 0761c01b75d58d46eb6f97e06d94db568ac33d53
Ported-from: 7100f7d8d30816087a20827dc4286d7d77aef5e5
```

When step 1 lifted the helper, add to the body `IlCallScanner gains Fingerprint from the plan 042
port for these tests.` and the trailer `Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe`.

### Step C4: An heir no longer inherits a stale career quest (`6ae924638` career-quest hunks, `180696430`)

1. Only when `SPAUTH` is not `yes` (the predicate `6ae924638` calls; WP132 leaves `113d3483b`'s
   copy, so it is added here):
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git diff --quiet 1b00d1acd HEAD -- Main/Features/CoopInterop/CoopSessionPolicy.cs TAOM.Tests/Features/CoopInterop/CoopSessionPolicyTests.cs && git diff 113d3483b^ 113d3483b -- Main/Features/CoopInterop/CoopSessionPolicy.cs TAOM.Tests/Features/CoopInterop/CoopSessionPolicyTests.cs | git apply; echo "exit=$?"
   ```
   Expected `exit=0` (both files equal `113d3483b^`). A failure: STOP.
2. ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git diff 6ae924638^ 6ae924638 -- Main/Features/CareerSystem/CareerQuestService.cs Main/Features/CareerSystem/ICareerQuestService.cs Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs TAOM.Tests/Features/CareerSystem/CareerQuestServiceTests.cs docs/features/career-quest-system.md | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U
   ```
   Expected: the four code and test files apply without conflict (the two service files equal
   `6ae924638^`; the behavior and the test file differ only outside these hunks, simulated clean).
   `docs/features/career-quest-system.md` may conflict, because its 1.5.x neighbour bullet ("Non-career
   quests reuse the shell", the gear ladder) is not on this line: keep this line's lines and add the
   incoming "An heir takes over" bullet. A conflict in a code or test file: resolve keeping this
   line's text plus the incoming lines, or STOP if that does not compile.
3. ```bash
   cd /c/Users/mikew/source/repos/taom-port/129 && git show 180696430:TAOM.Tests/Features/CareerSystem/CareerQuestWiringTests.cs > TAOM.Tests/Features/CareerSystem/CareerQuestWiringTests.cs && git show 180696430:docs/reviews/rca-career-quest-owner-change-2026-10-27.md > docs/reviews/rca-career-quest-owner-change-2026-10-27.md
   ```
4. `docs/features/career-quest-system.md`, in the new "An heir takes over" bullet: replace
   ``(`docs/research/coop-compat-gap-ledger.md` R1b)`` with `(a co-op research note on the 1.5.x line)`
   and delete the sentence `It never cancels a lord's gear ladder quest.` (that 1.5.x feature is not
   on this line).
5. Append to the new RCA:
   ```markdown

   ## 1.4.x port (plan 129)

   Ported to the 1.4.x line by plan 129. The rows about `LadderQuests.cs`, `LordsLadderService.cs` and
   the lord's gear ladder concern Armour Acquisition, a 1.5.x-only feature, and do not apply here. On
   v1.4.8 `QuestManager.OnPlayerCharacterChanged` (:282-292) skips special quests, and this line's
   `CareerQuest` is special (`SpecialQuestType`, `CareerQuest.cs:79`), so the defect is live here.
   ```
6. Do not take `180696430`'s `LadderQuests.cs` and `LordsLadderService.cs` comment edits, its lessons
   hunk, or any Codex prompt file.

**Verify**: `grep -c "IsSinglePlayerAuthority" Main/Features/CoopInterop/CoopSessionPolicy.cs`
at least `1`; `grep -c '"taom_cq_" + def.Id' Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs`
prints `0`; `grep -c "coop-compat-gap-ledger\|gear ladder" docs/features/career-quest-system.md`
prints `0`; build exit 0; filtered `CareerQuestServiceTests|CareerQuestWiringTests|CoopSessionPolicyTests`:
`Failed: 0`, `CareerQuestWiringTests` `Passed: 3`.

Commit (stage `CoopSessionPolicy.cs` and `CoopSessionPolicyTests.cs` if step 1 ran, the three career
quest files, `CareerQuestServiceTests.cs`, `CareerQuestWiringTests.cs`, `career-quest-system.md`, the RCA):

```text
fix(career): v2.0.28 - an heir no longer inherits a stale career quest

When an heir takes over after the player's hero dies or retires,
vanilla cancels every running quest except special ones, and a career
quest is special, so the old hero's quest kept polling the old hero's
skill forever. Before each offer the career quest behaviour now
cancels a running career quest whose owner is not the main hero, with
vanilla's own reason text, and the heir gets the normal offer. Each
offer gets its own StringId, because the engine keys a quest's journal
record by StringId. It cancels only in single player.

Research: taom-src v1.4.8 QuestManager.OnPlayerCharacterChanged
 skips special quests (:282-292); QuestBase.CompleteQuestWithCancel
 (:223); JournalLogEntry.IsRelatedTo matches StringId (:119-122);
 CampaignTime.ToMilliseconds (:132)
Not-tested: an heir takes over during a career quest: the old quest is
 cancelled and the heir is offered their own
Save-compat: quests already started keep their old StringId; new
 offers use the longer one
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: 18069643062cd6fb0312ca33ecca015f13ef222e
```

When step 1 ran, add `Ported-from: 113d3483bb1c6b303546e96c63e369eb2d920d5d` and the body sentence
`The single-player predicate CoopSessionPolicy.IsSinglePlayerAuthority comes along from the 1.5.x
line with its test.`

### Step C5: MountHealth and InventoryCapacity print as percentages (`bce566059`, display fix only; TDD)

RED: in `TAOM.Tests/Features/CareerSystem/CareerEffectDisplayMapTests.cs` add `using TAOM.Features.CareerSystem.Diagnostics;`
and, after `Format_FlatEffect_RendersCount`, these two tests:

```csharp
    // #769 display fix (ported): both are FRACTIONS at runtime (CareerAgentStatService multiplies the
    // mount's base health by 1 + mountHealth; TaomInventoryCapacityModel feeds ApplyFactor), so a flat
    // reading rounded a 0.10 pick to 0 and printed nothing.
    [TestMethod]
    public void Format_MountHealthAndInventoryCapacity_RenderAsPercent()
    {
        Assert.AreEqual("+10% mount health", CareerEffectDisplayMap.Format(PassiveEffectType.MountHealth, 0.10f));
        Assert.AreEqual("+15% inventory capacity", CareerEffectDisplayMap.Format(PassiveEffectType.InventoryCapacity, 0.15f));
    }

    // The panel and the perk report must agree on which types are counts.
    [TestMethod]
    public void Format_FlatTypes_AreExactlyThePerkReportsCountTypes()
    {
        foreach (PassiveEffectType type in System.Enum.GetValues(typeof(PassiveEffectType)))
        {
            var line = CareerEffectDisplayMap.Format(type, 2f);
            Assert.AreEqual(!CareerPerkConsumerMap.IsFlat(type), line.Contains("%"), $"{type}: '{line}'");
        }
    }
```

Build, filtered `CareerEffectDisplayMapTests`: exactly these two fail. GREEN: in
`CareerEffectDisplayMap.cs` delete the two lines `PassiveEffectType.InventoryCapacity,` and
`PassiveEffectType.MountHealth,` from `FlatValueEffects`, and in the class summary replace
`and Health / PartySize as counts (25, 4)` with
`and Health / PartySize / CompanionLimit as counts (25, 4); MountHealth and InventoryCapacity are fractions too`.
In `docs/features/career-system.md` replace the end of the "Percent vs flat" bullet,
`TAOM authors Damage/Ammo/MovementSpeed as fractions and Health/PartySize/MountHealth as counts.`, with
`TAOM authors Damage/Ammo/MovementSpeed/MountHealth/InventoryCapacity as fractions and Health/PartySize (and CompanionLimit) as counts; CareerEffectDisplayMap lists exactly those three counts, the set CareerPerkConsumerMap.IsFlat holds, so a MountHealth pick prints "+10% mount health" (it used to round to 0 and print nothing).`

**Verify**: build exit 0; filtered `CareerEffectDisplayMapTests`: `Failed: 0`.

Commit:

```text
fix(careers): v2.0.28 - mount health perks show in the effects panel

The career screen's Active Effects panel treated MountHealth and
InventoryCapacity as counts, but both are fractions (61 shipped
MountHealth pips of 0.10 to 0.15), so a pick rounded to 0 and printed
nothing. Both now print as percentages, and a test keeps the panel's
count list equal to the one the perk report uses.

Not-tested: a MountHealth pick shows "+10% mount health" on the screen
Ported-from: bce566059fa0d9b0caeee51d18e1723cef921af0
```

### Step C6: The career screen shows the root perk and keystone passives (`23799be82`, adapted; TDD)

This line has the single-`Passive` choice model and no troop classes, so the 1.5.x hunks are
rewritten here, not picked.

RED, part 1: create `TAOM.Tests/Features/CareerSystem/CareerScreenPrefabBindingTests.cs` from
`git show 23799be82:TAOM.Tests/Features/CareerSystem/CareerScreenPrefabBindingTests.cs`, then delete its
`[TestCategory("RequiresGame")]` line. (A static check this session found every `@binding` and
`Command.*` of the 1.4.x prefab resolving on its VM, so only the root perk test should fail.)

RED, part 2: in `TAOM.Tests/Features/CareerSystem/CareerScreenVMTests.cs` rename
`ActiveEffects_NoChoicesTaken_BothListsEmpty` to `ActiveEffects_NoChoicesTakenAndNoRoot_BothListsEmpty`
and add, before `private void SetupHeroWithCareer()`:

```csharp
    // #769 (ported): the root perk is the career's always-held identity perk. CareerPassiveService.RefreshCache
    // applies it (taken or not) plus every stored choice, keystones included; the panel sums by the same rule.
    private static CareerChoiceDefinition RootChoice(float damage = 0.05f) => new CareerChoiceDefinition(
        id: "wb_root", groupId: "", type: ChoiceType.Passive,
        description: "{=taom_test_root}Root perk text", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.Damage, damage), mutations: null);

    private static CareerChoiceDefinition KeystoneWithPassive() => new CareerChoiceDefinition(
        id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
        description: "Keystone", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.Damage, 0.07f), mutations: null);

    [TestMethod]
    public void ActiveEffects_RootChoiceNeverTaken_ListsItsPassive()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootChoiceHeldInTheHeroData_IsCountedOnce()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        var vm = CreateVM();
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootChoiceAlsoListedInAGroup_IsCountedOnce()
    {
        var root = RootChoice();
        _registry.GetChoice("wb_root").Returns(root);
        _registry.GetChoicesForGroup("wb_brutality").Returns(
            new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice, root });
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        var vm = CreateVM();
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootAndATakenPassiveOfOneType_SumIntoOneLine()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_p1"); // Damage 0.1
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+15% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_StoredChoiceInNoGroupOfTheCareer_StillCountsLikeRefreshCache()
    {
        // RefreshCache applies every stored id the registry resolves, and RepairForeignChoices keeps an
        // id whose owner it cannot prove, so such a choice is applied: the panel must not hide it.
        _registry.GetChoice("wb_stray").Returns(new CareerChoiceDefinition(
            id: "wb_stray", groupId: "", type: ChoiceType.Passive, description: "Stray", iconSprite: "icon",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.1f), mutations: null));
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_stray", 10);
        var vm = CreateVM();
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+10% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_GroupListedTwiceByTheCareer_CountsATakenPassiveOnce()
    {
        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "A brute.",
            portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string> { "wb_brutality", "wb_brutality" }));
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_p1"); // Damage 0.1
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+10% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_TakenKeystoneWithAPassive_ListsBothTheDescriptionAndThePassive()
    {
        var keystone = KeystoneWithPassive();
        _registry.GetChoice("wb_brut_key").Returns(keystone);
        _registry.GetChoicesForGroup("wb_brutality").Returns(new List<CareerChoiceDefinition> { keystone, PassiveChoice });
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_key");
        Assert.AreEqual(1, vm.KeystoneEffectLines.Count);
        Assert.AreEqual("Keystone", vm.KeystoneEffectLines[0].LineText);
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+7% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_TakenKeystonePassiveAndRoot_SumIntoOneLine()
    {
        var keystone = KeystoneWithPassive();
        _registry.GetChoice("wb_brut_key").Returns(keystone);
        _registry.GetChoice("wb_root").Returns(RootChoice());
        _registry.GetChoicesForGroup("wb_brutality").Returns(new List<CareerChoiceDefinition> { keystone, PassiveChoice });
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_key");
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+12% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void RootPerkDescription_CareerWithARootChoice_ShowsItsText()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.IsTrue(vm.HasRootPerk);
        Assert.AreEqual("Root perk text", vm.RootPerkDescription);
    }

    [TestMethod]
    public void RootPerkDescription_RootChoiceNotInTheRegistry_IsEmptyAndHidden()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.IsFalse(vm.HasRootPerk);
        Assert.AreEqual("", vm.RootPerkDescription);
    }

    [TestMethod]
    public void RootPerkDescription_RootChoiceWithoutADescription_IsHidden()
    {
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive, description: "", iconSprite: "icon",
            passive: null, mutations: null));
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.IsFalse(vm.HasRootPerk);
        Assert.AreEqual("", vm.RootPerkDescription);
    }
```

Build: it fails with `CS1061` on `RootPerkDescription` and `HasRootPerk` (RED by compilation).

GREEN, in `Main/Features/CareerSystem/UI/CareerScreenVM.cs`:

1. After `private string _careerDescription;` add `private string _rootPerkDescription = "";`.
2. In `RebuildChoiceGroups`, replace the whole `if (isTaken) { ... }` block that follows the comment
   `// Issue #388` inside the choice loop (1.4.x `:302-315`, the keystone branch and the
   `else if (choice.Passive != null)` branch) with:
   ```csharp
                   // Issue #388: a taken keystone's description joins the Active Effects panel from this
                   // same walk; the passive totals come from the hero's stored choices after the walk.
                   if (isTaken && choice.Type == Domain.ChoiceType.Keystone && !string.IsNullOrEmpty(choice.Description))
                       takenKeystoneDescriptions.Add(choice.Description);
   ```
3. Directly before `PublishActiveEffects(takenKeystoneDescriptions, takenPassiveTotals);` insert:
   ```csharp
           // The passive totals follow CareerPassiveService.RefreshCache: every stored choice id once (a
           // group the career lists twice cannot double a pick), keystones included, then the root when
           // it is not stored. The root sits in no group, so it is read through the registry, and its
           // text is shown as the career's identity perk.
           var heldChoices = _dataService.GetOrCreateData(_heroStringId);
           foreach (var heldId in heldChoices.ChoiceIds)
               AddPassive(takenPassiveTotals, _registry.GetChoice(heldId)?.Passive);
           var rootChoice = string.IsNullOrEmpty(career.RootChoiceId) ? null : _registry.GetChoice(career.RootChoiceId);
           if (rootChoice != null && !heldChoices.HasChoice(rootChoice.Id))
               AddPassive(takenPassiveTotals, rootChoice.Passive);
           RootPerkDescription = rootChoice != null && !string.IsNullOrEmpty(rootChoice.Description)
               ? new TextObject(rootChoice.Description).ToString()
               : "";

   ```
4. After `RebuildChoiceGroups` add:
   ```csharp

       private static void AddPassive(Dictionary<Domain.PassiveEffectType, float> totals, Domain.PassiveEffect? passive)
       {
           if (passive == null) return;
           totals.TryGetValue(passive.EffectType, out var running);
           totals[passive.EffectType] = running + passive.Magnitude;
       }
   ```
5. After the `CareerDescription` property add:
   ```csharp

       /// <summary>The career's root choice text (its always-held identity perk), shown in the header bar.</summary>
       [DataSourceProperty]
       public string RootPerkDescription
       {
           get => _rootPerkDescription;
           set
           {
               if (_rootPerkDescription != value)
               {
                   _rootPerkDescription = value;
                   OnPropertyChangedWithValue(value, nameof(RootPerkDescription));
                   OnPropertyChanged(nameof(HasRootPerk));
               }
           }
       }

       [DataSourceProperty]
       public bool HasRootPerk => _rootPerkDescription.Length > 0;
   ```
6. `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml`: directly after the line with
   `Text="@CareerDescription"` (1.4.x `:47`) insert, with the same indentation:
   `<TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="8" Text="@RootPerkDescription" Brush="CareerSystem.Effects.Passive" Brush.FontSize="17" IsVisible="@HasRootPerk" />`
   (the brush exists: `Main/_Module/GUI/Brushes/CareerSystem.xml:65`).
7. Docs, `docs/features/career-system.md`: after the "## Effect-Scope Badges in Choice Tree (UX)"
   paragraph add:
   ```markdown

   **Active Effects panel (#388).** The right-hand column lists the descriptions of taken keystones and the passives of the career's root choice and of every stored choice, keystones included, summed per effect type (`CareerScreenVM.RebuildChoiceGroups` collects them, `CareerEffectDisplayMap.Format` prints them). The totals follow the rule `CareerPassiveService.RefreshCache` applies: each stored choice id the registry resolves is added once, then the root choice when the stored list does not hold it. Before the 1.4.x port of the 1.5.x fix (#769) the panel walked the career's groups and skipped keystones, so the root passive and every keystone passive were missing although both apply in game. Known gap: the panel ignores `attack_type_mask`, so a Melee pip and a Ranged pip of one type sum into one line.

   **Root perk line.** The header bar shows the root choice's description under the career description (`CareerScreenVM.RootPerkDescription`, gated on `HasRootPerk`, which is false when the root choice is missing from the registry or has no description). Some older root texts promise more than their single passive gives; that is a content question for the maintainer.
   ```
   and in the same file make the docs re-citations of Part A: replace `GetAttackCollisionResults:200`
   with `GetAttackCollisionResults:192, v1.4.8` and `CalculateMobilePartyVisibilityAndInspected` with
   `PartyBase.CalculateVisibilityAndInspected`.
8. `docs/features/gui-sprite-system.md`: in the Binding Chain block change
   `├── @ScreenTitle, @DoneLbl, @CareerName, @CareerDescription` to
   `├── @ScreenTitle, @DoneLbl, @CareerName, @CareerDescription, @RootPerkDescription (shown when @HasRootPerk)`.

**Verify**: `grep -c "@RootPerkDescription" Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml`
prints `1`; `grep -c "GetAttackCollisionResults:200\|CalculateMobilePartyVisibilityAndInspected" docs/features/career-system.md`
prints `0`; build exit 0; filtered `CareerScreenVMTests|CareerScreenPrefabBindingTests`: `Failed: 0`,
`CareerScreenPrefabBindingTests` `Passed: 3`; Data: no new ERROR (the prefab is not ModuleData, but run it).

Commit (stage `CareerScreenVM.cs`, `CareerScreen.xml`, the two test files, `career-system.md`,
`gui-sprite-system.md`):

```text
fix(careers): v2.0.28 - career screen shows the root perk, keystones

The Active Effects panel left out the career's root perk and the
passives of keystone picks, although the passive service applies
both. It now sums every stored choice once, keystones included, plus
the root when it is not stored (the rule CareerPassiveService uses),
and the header shows the root perk's text under the career
description. A new prefab test pins every binding of the career
screen and the new line's place.

Not-tested: the header line and the panel totals on the career screen
Ported-from: 23799be827f64d2f5a4b5f6743bbf4d03d202abe
```

### Part C done criteria

- [ ] Build exit 0; full Test command: `Failed: 0`, total `BASE_TOTAL + 160` (`+ 159` when `SPAUTH=yes`), skipped `BASE_SKIPPED`.
- [ ] `git log --oneline bannerlord-1.4.5..HEAD` shows 15 commits, each subject with `v2.0.28 - ` and at most 72 characters.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists no `CHANGELOG.md`, nothing under
  `docs/reviews/lessons/`, nothing under `Client/` or `Main/Features/ArmourAcquisition/`, no
  `TAOM.Tests/Infrastructure/RepoPaths.cs`, no `*.prompt.md`.
- [ ] `git grep -nE 'TestCategory\("RequiresGame"\)' -- TAOM.Tests/Features/CareerSystem TAOM.Tests/Features/SpecialResources` prints nothing.
- [ ] Dash scan prints nothing; Docs: no new finding in the touched docs.

## Disposition (every commit in the pack, plus the shared hunks)

| Commit | Subject | Group | Disposition |
|---|---|---|---|
| `2b1d1ef96` | fix(career): cavalry mount bonuses reach the mount (#611) | issue-611-career-mount-bonuses | **Ported** (A1, `cherry-pick -n`): stat model hand-resolved without the 1.5.x #608 aggression and #610 charge parameters; `combat-mechanics.md` hunk dropped (#610 text); `rca-cavalry-charge` dropped (a #610 file absent here) and a fresh #611 RCA written; `gamemodels.md` row and registry row rewritten; CHANGELOG dropped (D1) |
| `7ede923be` | fix(career): typed resistances, ammo, perk diagnostics (#613) | issue-613-career-perk-wiring | **Ported** (A2): model constructor adapted (logger only); two `SubModule.cs` lines; engine lines re-cited; RCA kept with a port note; two lessons deferred to WP142; CHANGELOG dropped |
| `e0ad0db76` (CareerSystem hunks) | perf(diagnostics): cheaper always-on mission diagnostics | plan-030 (WP123) | **Adopted here** (A3): the career hit tallies, `OnRemoveBehavior` summary and their tests, which WP123 skipped on the premise that #613 is 1.5.x-only; every other hunk is WP123's |
| `35007dbb8` | fix(field-commission): expand allowed race names for promotions | issue-612-field-commission-races | **Ported** (C1), re-subjected with #612: `RepoPaths.cs` hunk dropped (WP104 owns the file); the `EnlistmentDiagnosticsSettingsProviderTests` helper swap dropped (unrelated); lessons and CHANGELOG dropped; RCA kept |
| `b02467744` | feat(arthedain): playable kingdom of Arthedain (Arnor) | careers-archetype-map-coverage | **Adapted** (C2): only the promoted-cultures block of the archetype map and `IoCMap_EveryCareerInTheData_HasAnArchetype`; every Arthedain career, troop, settlement, string and tool OUT (content) |
| `259ae2cba` | fix(careers): link each career choice to its group (#766) | career-e1e3-review-fixes | **Ported** (B1, clean) |
| `01ca483e2` | fix(resources): career gain scales every earning (#767) | career-e1e3-review-fixes | **Ported** (B2); the `CareerPerkConsumerMap.cs` hunk KEPT (the file exists after A2) |
| `c74ad5aed` | fix(careers): party size picks apply at once, own party only | career-e1e3-review-fixes | **Ported** (B3): `CareerPerkCheats.cs` hunk KEPT (exists after A2); translation cache removals KEPT (the cache is keyed by string id); `IoC.cs` line; RequiresGame tag dropped (D9); one doc sentence adapted (no forks redesign here) |
| `30cbcd465` | fix(resources): raids stop paying the battle credit (#770) | career-e1e3-review-fixes | **Ported** (B4): the `SiegeAmbush` row removed from the test (not a v1.4.8 `BattleTypes` member) |
| `c96a02a4b` | fix(resources): one party-screen handler per session (#771) | career-e1e3-review-fixes | **Docs only** (B6): code skipped (this line fixed #771 in `1b00d1acd`); the #767 and #770 doc text taken, the #771 doc text skipped; lesson deferred to WP142 |
| `86ef5c7af` | docs(reviews): RCA and lessons for the career fixes review | career-e1e3-review-fixes | **RCA kept** (B6, with a port note); three lessons deferred to WP142 |
| `bc59806f0` | fix(resources): hideout send-troops clear pays once (#790) | career-e1e3-review-fixes | **Ported** (B5 code, comment re-cited to v1.4.8; doc line in B6) |
| `4f5b412d7` | docs(reviews): Codex review of the career fixes | career-e1e3-review-fixes | **RCA section kept** (B6); lesson line deferred to WP142; REVIEW-LOG row and prompt file skipped (a 1.5.x review record) |
| `0761c01b7` | fix(career): career button keeps one click handler | issue-971-career-click | **Ported** (C3): the `Client/` hunk applied to this line's `Main/` file; `rca-taom-client-b5` edit and prompt file skipped; lesson deferred to WP142; RequiresGame tag dropped; v1.5.5 citation re-cited; RCA kept |
| `7100f7d8d` | docs(coop): headless research and career docs catch up | issue-971-career-click | **Adapted** (C3): only the `career-system.md` #378 line; `taom-headless-server.md` OUT (D7, absent here) |
| `180696430` | test(career): pin the career quest cancel call sites | career-quest-owner-change | **Ported** (C4): `CareerQuestWiringTests` and the RCA end state; `LadderQuests.cs` and `LordsLadderService.cs` comment edits skipped (feature absent); lesson deferred to WP142 |
| `6ae924638` (career-quest hunks) | Add tests for NRE containment finalizers and improve documentation | shared (orchestrator note) | **Ported** (C4): `CareerQuestService`, `ICareerQuestService`, `CareerQuestCampaignBehavior`, `CareerQuestServiceTests`, the `career-quest-system.md` bullet (two 1.5.x references removed); its other hunks belong to WP112, WP119, WP123 and WP137 |
| `113d3483b` (one hunk pair) | refactor(coop): one single-player gate, survival roll split | battlebalance-enlisted-survival-roll (WP132) | **Prerequisite taken here when absent** (C4): `CoopSessionPolicy.IsSinglePlayerAuthority` and its test; WP132's notes leave this member, and `6ae924638` calls it |
| `a27c6b338` (one hunk) | perf(xml-merge): merge module XML in one document per load | plan-042 (WP118) | **Conditional** (C3): `IlCallScanner.Fingerprint` lifted only when WP118 has not landed it (orchestrator note K1) |
| `bce566059` | feat(careers): two-effect options, troop classes, counters | career-active-effects-panel | **Adapted** (C5): only the MountHealth and InventoryCapacity display fix, re-implemented by hand; the redesign, labels, counters and race ability edits OUT (feature) |
| `23799be82` | fix(careers): career screen shows the root perk, keystones | career-active-effects-panel | **Adapted** (C6): single-`Passive` model, no `TroopClassMask`; tests rewritten on this line's fixtures; `CareerScreenPrefabBindingTests` created from its 1.5.x shape |

Related, not taken: `0f57f29ff` (between `2b1d1ef96` and `7ede923be`; a creature mount retune,
content, skipped in WP130's group); `c79a58521`'s `MountChargeMultiplier` (the 1.5.x great elk antler
charge, #636, a 1.5.x-only feature).

## Test plan

| Step | Tests | New results |
|---|---|---|
| A1 | `CareerAgentStatServiceTests` (nine tests replace three rider-side tests, net +6), new `CareerMountBonusBindingTests` (3) | +9 |
| A2 | new `AttackTypeMaskTests` (10), new `CareerPerkReportTests` (6), `CareerMountBonusBindingTests` (+1 ammo seam pin), `CareerAgentStatServiceTests`, `CareerConfigProviderTests`, `CareerChoicesIntegrationTests` (the shipped-XML mask test expects the five kinded ids listed in Current state), `CareerPassiveServiceTests` | +30 |
| A3 | `CareerAgentStatServiceTests` (+17), new `CareerPerkMissionTeardownTests` (7) | +24 |
| B1 | `CareerChoiceIntegrityTests`, `CareerChoicesIntegrationTests`, `CareerConfigProviderTests`, `CareerLifecycleServiceTests`, `CareerRegistryTests`, `CareerScreenVMTests` | +14 |
| B2 | new `SpecialResourceGainTests` (21 methods plus one data method with 3 rows), `SpecialResourceBreakdownTests` (+2) | +26 |
| B3 | `AiPartySizeOrderingTests`, `AiPartySizeSettingsWatcherTests`, `CareerPassiveServiceTests`, new `SpecialResourcesWiringTests` (3) | +12 |
| B4 | `SpecialResourceEarnPolicyTests` | +2 |
| C1 | FieldCommission tests | +3 |
| C2 | `CareerArchetypeServiceTests` | +1 |
| C3 | new `CareerButtonClickTests` | +4 |
| C4 | `CoopSessionPolicyTests` (+1, only when added here), `CareerQuestServiceTests` (11 methods plus one data method with 4 rows), new `CareerQuestWiringTests` (3) | +19 (or +18) |
| C5 | `CareerEffectDisplayMapTests` | +2 |
| C6 | `CareerScreenVMTests` (+11), new `CareerScreenPrefabBindingTests` (3) | +14 |

Structurally untestable (name them in `Not-tested:`): the engine actually recomputing a horse after
an ability (A1), the ammo seam firing at spawn (A2), the summary on a window close (A3), the party
limit refresh on the map (B3), raid and hideout event order in a live campaign (B4, B5), the career
button sound (C3), an heir takeover (C4), the screen layout (C6). The IL and source pins cover each
call site.

## Done criteria (whole WP)

- [ ] Every Part's done criteria hold.
- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=`: `Failed: 0`, total
  `BASE_TOTAL + 160` (`+ 159` when `SPAUTH=yes`), skipped `BASE_SKIPPED`.
- [ ] `git grep -n -E "props\.MountChargeDamage|props\.MountSpeed|IsAnyAmmo\(\)|\"taom_cq_\" \+ def\.Id|CareerPassiveHero\.ResolveId\(party\), ref result, PassiveEffectType\.PartySize" -- Main` prints nothing.
- [ ] `git grep -c "MapEvent.BattleTypes.SiegeAmbush" -- TAOM.Tests` prints nothing.
- [ ] `python tools/validate_moduledata.py` ERROR count equals `BASE_DATA_ERRORS`.
- [ ] `git status --porcelain` is empty; nothing pushed.
- [ ] Your final message reports: `BASE_TOTAL`, the final total, the 15 commit hashes and subjects,
  and the `WP106`, `FINGERPRINT` and `SPAUTH` values.

## STOP conditions

Stop and report (do not improvise) if:

- A Step 0 prerequisite is missing, or this WP's symbols already exist under `Main/` (another WP
  ported part of it).
- `TaomAgentStatCalculateModel.cs` changed since `1b00d1acd` (Step 0), or `CareerSystemIoC.cs`,
  `CareerArchetypeServiceTests.cs`, `IlCallScanner.cs` (when lifting), `CoopSessionPolicy.cs` or its
  tests (when adding) changed since `1b00d1acd`.
- A cherry-pick leaves an unmerged path the step does not list, or a listed resolution does not
  compile.
- `CareerPerkMissionBehavior.cs` has no fallen-hero ally loop (`foreach (var allyIndex in buffedAllies)`)
  or no `OnEndMission` with the `CareerAbilityBuffTracker.ClearAll()` try/catch.
- The build fails on a TaleWorlds member this plan lists as verified (a signature differs from
  "Verified v1.4.8 engine facts"): report it; do not decompile and improvise.
- Step B4's build error is anything but `CS0117` for `SiegeAmbush`.
- `CareerButtonClickTests` reports `CS0117` or `CS1061` on `IlCallScanner` after the lift: lift the
  missing helper from its 1.5.x commit, never rewrite the test; if you cannot find it, STOP.
- `CareerScreenPrefabBindingTests.Prefab_EveryBindingAndCommand_ResolvesOnTheVmItsScopeBindsTo` fails
  on any binding other than `RootPerkDescription` or `HasRootPerk`: a pre-existing UI defect; report
  the list, do not edit the prefab.
- A shipped-data test (`RealChoicesXml_EveryAttackTypeMask_ParsesAndKindPipsAreKinded`,
  `ShippedData_EveryOwnershipIdHasExactlyOneOwner`, `ShippedJson_AllowedRaceNames_AreAllKnownRaces`,
  `IoCMap_EveryCareerInTheData_HasAnArchetype` after GREEN) fails: a data defect; report it, do not
  edit ModuleData to quiet it.
- `python tools/validate_moduledata.py` reports a new ERROR, or the ModuleData commit hook refuses a
  commit (D20: the laptop must carry the WP101 Armory).
- `CareerMountBonusBindingTests` or `CareerButtonClickTests` go Inconclusive (game assemblies did not
  load: `BANNERLORD_GAME_DIR`), or any test is skipped beyond `BASE_SKIPPED`.
- A step seems to need `Main/TAOM.csproj`, `Directory.Build.props`, `.claude/settings*.json`, a
  further `Main/IoC.cs` or `Main/SubModule.cs` edit, an MCM setting or a save-format change.
- A verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Log: the newest `Logs\taom_debug_<timestamp>.log` beside the game executable
(`bin\Win64_Shipping_Client`; `Main/Core/Logging/FileLogger.cs:26-40`). Use a campaign with a career
hero; `taom.career_perks` needs cheats on.

1. **Mount bonuses (#611)**: a Cavalry-archetype career with a `MountChargeDamage` pick: in battle,
   `taom.career_perks` shows the mount's `MountChargeDamage` above the base, and `[CareerPerks] mount
   stats for rider` appears once. Fire the Cavalry ability: the mount's speed and charge rise in the
   report, then fall back when the buff ends.
2. **Typed resistance (#613)**: a `black_uruk_captain` with `black_uruk_captain_t1_a_p2` (Blunt):
   `hit reduction` lines (first hit per combination, DEBUG) appear for mace and horse-charge hits and
   not for sword cuts.
3. **Ammo (#613)**: a javelin career with an Ammo pick spawns with more javelins than the printed
   maximum; `[CareerPerks] Ammo +N%` lines show amount and max before and after.
4. **Hit summary (A3)**: a hero with a TroopDamage pick fights a battle: one `hit amp` DEBUG line per
   combination, then at mission end `[CareerPerks] hit summary for this mission: ...` with a hit count.
5. **Groups (#766)**: at level 1 the career screen refuses a tier 3 choice; a save with a foreign
   choice shows one "Career choices that belonged to another career were refunded" message on load.
6. **Earnings (#767, #770, #790)**: with `captain_of_osgiliath_t3_a_p4` (+10% gain), a battle, raid
   and hideout each log a `[SpecRes]` earning 10% above base; a raid against militia only pays the
   raid credit once and no battle credit; a send-troops hideout clear writes one `[SpecRes] HIDEOUT:`
   line, also after saving in the success menu and loading.
7. **Party size (#768)**: take a PartySize pick: the party limit in the party screen rises at once;
   an owned caravan's limit does not. `taom.career_perks` prints the tooltip value, the enforcement
   value and the engine cache, all equal.
8. **Promotions (#612)**: an Isengard or Mordor campaign with Field Commission on gets a promotion
   offer after a won fair battle; no `[FieldCommission] allowedRaceNames entry` warning at start.
9. **Archetypes**: a goblin, Lindon or Abanissa career's ability applies its buff (damage, speed or
   mount line in `taom.career_perks` while it runs).
10. **Career button (#971)**: one click sound, and the career screen opens.
11. **Heir quest**: let the hero die with a career quest running and continue as the heir: the old
    quest shows cancelled with vanilla's clan-leader text; `CareerQuest: cancelled '...'` in the log;
    the heir is offered their own career's quest.
12. **Career screen (#769 fixes)**: the header shows the root perk line under the description; a
    MountHealth pick shows `+10% mount health`; a keystone that carries a passive adds its value to the
    Active Effects total.

## Maintainer steps

- Create the GitHub issues for the archetype map coverage (C2) and the heir career quest cancel (C4)
  before Part C lands; cite `b02467744`, `6ae924638` and `180696430`.
- Recommended for the orchestrator: `/deep-review` after each Part (probe the stat model resolution,
  the CareerPerkMissionBehavior hand edits against WP106's queue, the B3 `ApplyFlat` move against
  WP131's party-size edit, and the C6 VM change); `/verify-bindings` after Part A (a new
  `InitializeMissionEquipment` override and new IL pins).
- WP135 (localization, last) translates `taom_career_repair_refund` (B1) and the corrected
  `taom_ws_t3_a_p3_desc` (B3); that run spends money and is the maintainer's to start.
- No push, PR, tag or release from this WP.

## Maintenance notes

- Later WPs that touch this WP's output: WP139 extends `CareerButtonClickTests` and the Patch27 file
  (`0ab3dc35a`, `4e0c2a3b0`, `2d6934006`; `0ab3dc35a` also needs `IlCallScanner.ExtractOpCodes` from
  `92839bc52`, K1-01); WP142 syncs the lessons this WP deferred: `7ede923be` (adapters, testing-qa),
  `35007dbb8` (data-content-cultures), `c96a02a4b` (state-lifecycle-save correction: the orphaned
  static handler is not GC-eligible), `86ef5c7af` (campaign-mechanics, gamemodels-services,
  testing-qa), `4f5b412d7` (campaign-mechanics, #790), `0761c01b7` (adapters), `180696430`
  (state-lifecycle-save).
- Merge hot spots in wave 4: WP131 edits `TaomPartySizeModel.cs` near the troop-weight line, which B3
  moves the career `ApplyFlat` call in front of; WP130 (`b0154d86e`) builds a Custom Battle damage
  model next to `TaomAgentApplyDamageModel`, whose private `HitMask` A2 changes (its crush roll must not
  copy the `MBRandom` draw, K1-T6); `SubModule.cs` and `IoC.cs` are append-style in every WP.
- Review focus: the A2 seam has no try/catch around `CareerAmmoApplier.Apply` inside
  `InitializeMissionEquipment` (parity with 1.5.x; the applier null-checks agent, bonus and
  equipment), while the old `OnAgentBuild` refill caught (#595). The ally buff stays keyed on
  `Agent.Index`, which is pre-existing on both lines (#592 class).
- Still open on this line after the WP: `StealthBonus` (#614) is read by the wrong hook (the player
  seeing others); #774 (War of the Ring momentum counts a resisted raid twice); the Active Effects
  panel ignores `attack_type_mask`.
- Deliberately not ported: the 1.5.x great elk antler charge (`MountChargeMultiplier`, `c79a58521`),
  the career redesign (#769 forks, two-effect options, troop classes, counters), Arthedain.
