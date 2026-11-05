# Plan 134: Fix kingdom policies, Lindon's balance, alignment sides, wanderer backstories, generated lord kits and three Nazgul definitions on 1.4.x

> **Executor instructions**: Follow this plan step by step in the worktree named under "Git workflow".
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. Parts A, B and C are independent and land
> in that order on one branch (one session can do them all; another session may pick up a later part in
> the same worktree). Never edit `plans/README.md`: the integrator owns it. When done, give the hand-off
> report in Step C6.
>
> **Drift check (run first, inside Step 0)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/_Module/ModuleData/spkingdoms.xslt Main/_Module/ModuleData/taom_spkingdoms.xml Main/_Module/ModuleData/TroopWeights/troop_weights.xml Main/_Module/ModuleData/configs/battle_balance_config.json Main/Features/BattleBalance/BattleBalanceConfig.cs Main/Adapters/IPlayerContextAdapter.cs Main/Adapters/PlayerContextAdapter.cs Main/Features/Execution Main/Features/WandererAllegiance Main/Features/PrisonerRecruitment Main/Features/MarriageAlignment/MarriageAlignmentService.cs Main/Features/TaomSettings.cs TAOM.Tests/Core TAOM.Tests/Adapters TAOM.Tests/Features/BattleBalance TAOM.Tests/Features/TroopWeight TAOM.Tests/Features/BannerBearers TAOM.Tests/Features/Execution TAOM.Tests/Features/WandererAllegiance TAOM.Tests/Features/PrisonerRecruitment docs/features/kingdom-creation.md docs/modding/kingdoms.md docs/features/battle-balance.md docs/features/troop-weight-system.md docs/features/banner-bearers.md docs/features/alignment-aware-execution.md docs/features/execution.md docs/features/wanderer-allegiance.md docs/features/prisoner-recruitment.md docs/features/marriage-alignment.md Main/_Module/ModuleData/taom_wanderer_strings.xml Main/_Module/ModuleData/Languages Main/_Module/SubModule.xml Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml Main/_Module/ModuleData/lords.xslt Main/_Module/ModuleData/characters/lords.xml Main/_Module/ModuleData/characters/custom_battle_lords.xml Main/_Module/ModuleData/module_sounds.xml docs/modding/equipment-rosters.md docs/features/nazgul-family.md docs/features/custom-battles.md docs/features/kingdom-voices.md`
> Earlier WPs of this programme are expected to change some of these: `TaomSettings.cs` (many WPs),
> `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` and a new `WandererAllegianceModule.cs`
> plus `WandererAllegianceWiringTests.cs` (WP104/WP105), `IPlayerContextAdapter.cs` and
> `PlayerContextAdapter.cs` (WP128 adds `GetPlayerClanId`), `TAOM.Tests/Core` and
> `TAOM.Tests/Features/BannerBearers` (new tests from other WPs), the language files (English rows from
> other WPs), `Main/_Module/SubModule.xml` (many WPs). Step 0 lists the exact checks; STOP only when one of them fails.

## Status

- **Priority**: P1
- **Effort**: M (Part A small; Part B medium; Part C small to medium)
- **Risk**: MED (Part A LOW: data and tests. Part B changes how the execution Harmony path, Wanderer
  Allegiance and Prisoner Recruitment pair a side's id and culture. Part C LOW to MED: data and tests, but
  Steps C3 and C4 change which kit generated lords and three Nazgul wear)
- **Depends on**: `plans/101-baseline-sync.md` merged, including its laptop Armory refresh (D20: Parts A
  and C touch ModuleData), and `plans/104-guarded-patch-categories.md` merged (it brings
  `TAOM.Tests/Infrastructure/RepoPaths.cs`, which Part B's `FactionSidesTests` reads). Everything merged
  before this WP is also in the base (expected drift is listed in the drift check above).
- **Category**: data / bug
- **Wave / lane**: wave 4, lane F
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (Execution and
  PrisonerRecruitment keep their hand wiring and WandererAllegiance keeps WP105's module; nothing here
  changes registration, so `Main/IoC.cs` and `Main/SubModule.cs` stay untouched), D8 (subjects carry
  `v2.0.28`), D10 (an ordinary session executes this file), D13 (each data item decided in this WP's
  notes; no balance retune), D19 (git only through the Bash tool), D20 (no data commit before the
  laptop Armory refresh)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved (to `c075c92f0` when this was written); every
  source here is a fixed commit sha, so the move does not matter.
- **Issue**: port of #756 (`685db7e46`), #749 (`0e75ec50a`), #823 (`736fa2fef`, test only) and #957
  part 2 (`5e43518df`); #757 (`9dd2f594f`); `bb68eeca3` item 3 with its `db805427b` comment (neither names an
  issue); the `lord_1_48_1/2/3` part of #644 (`9804f67b9`); the elf horn path has no 1.5.x commit or issue
  (plan 121, MEMORY-10). The orchestrator creates the 1.4.x tracking issue (one for WP134, or one per item) before implementation lands.

## Why this matters

- **Kingdom policies.** All 14 kingdoms in `taom_spkingdoms.xml` list `policy_lord_prerogative` and
  `policy_religious_privilege`, 28 rows that Bannerlord v1.4.8 does not define (`DefaultPolicies`
  registers 32 ids, none of them these). `Kingdom.Deserialize` drops an unknown id without a message.
  Gondor's only kingdom policy is misspelt as `policy_land_grants_for_veterans` (`spkingdoms.xslt:71`);
  the engine id is `policy_land_grands_for_veteran`, so every new campaign starts Gondor without Land
  Grants for Veterans (militia veterancy +10%, village tax -5%) and with only its culture's Senate.
  Nothing tests policy ids today.
- **Lindon.** Lindon became its own culture in `cc1713eb3` (on 1.4.x), but `troop_weights.xml` has no
  `lindon` row and the survival-bonus table has `rivendell` and no `lindon`. Lindon's 22 elite troops
  therefore cost one party slot each where their Rivendell twins cost two or three (a party holds three
  times as many Mithlond Marchwardens as Imladris ones), and Lindon parties lose Rivendell's 0.4
  survival bonus (death chance times 0.6). All 30 `lindon_` troops have a Rivendell twin of the same
  level on the merged tree.
- **Banner keys.** On v1.4.8 the engine itself banners a formation captained by a lord who carries a
  banner item, with no heraldry check, and the banner items a lord can be given are those of his own
  culture or of none. The native banner tableau access-violates on a banner whose code yields no group of ten
  integers (1.4.x RCA `docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md`). All 239 shipped keys
  parse today; nothing stops a bad one from shipping. This adds the gate.
- **Alignment sides (#957).** Executions, Wanderer Allegiance and Prisoner Recruitment pair a realm's
  id with the hero's OWN culture (or the player clan's), and `AlignmentService.ResolveSide` falls back
  to that culture whenever the realm reads Neutral. Four realms are Neutral in `alignment.json`
  (`battania` Khand, `shaghana`, `abanissa`, `umbar`). So a hero whose own culture is Free or Evil but
  who serves a Neutral realm is sided by his birth: a Free player executing a Gondor-born lord of Umbar
  pays the kinslaying relation hit and the Honor penalty; a Gondor-cultured lord of Khand waives
  prisoner morale for Rohan prisoners; a Gondor-born vassal of Umbar is refused by Evil wanderers. At
  campaign start every clan of Umbar (6), Shaghana (9) and Abanissa (8) carries its realm's culture, so
  the defect appears as heroes move between realms (the player, a lord who changes kingdom, a
  companion); how often in a live campaign is UNVERIFIED. The Marriage Alignment MCM hint and two code
  comments also call Dunland Neutral; `alignment.json` has `empire` (Dunland) Evil and `battania`
  (Khand) Neutral.
- **Wanderer backstories (Part C).** 40 of 210 wanderer templates (10 each of goblin,
  mistymountainorcs, bluecraig, lindon) have none of the 7 required backstory rows (280 rows missing),
  so their introductions show "ERROR: Text with id backstory_c doesn't exist!". The defect is verified on
  1.4.x data and the fix needs no OUT content, so D13 ports it (Disposition).
- **Generated lords (Part C).** A lord the game creates during a campaign (coming of age, a companion made
  a lord, a rebel leader) dresses from every roster of the hero's culture whose flags match exactly, and
  SandBoxCore's and SandBox's rosters stay loaded. A generated Dunland lord picks among 26 vanilla Empire
  battle sets and one TAOM set; the other five renamed cultures are alike (15 to 37 vanilla battle sets
  against one). Dale's one TAOM set is itself vanilla Sturgian gear.
- **Three lesser Nazgul (Part C).** `lord_1_48_1/2/3` are defined twice, in `lords.xslt` and
  `characters/lords.xml`. The row wins each attribute it states (race `uruk`, age 20) and the two definitions'
  equipment sets union, so each new campaign dresses each of them in a Nazgul kit or a generic Mordor kit at
  random.
- **Elf horn (Part C).** `module_sounds.xml:5` registers `LOTR/Elves/Alert/elf_horn.wav`; the file is
  `horn_elf.wav`. Nothing names the sound, so no player hears a difference. It is the one broken path of 485,
  and nothing tests them.

## Current state

### Files and their roles (`origin/bannerlord-1.4.5` line numbers)

| File | Role |
|---|---|
| `Main/_Module/ModuleData/spkingdoms.xslt` | Rewrites the 8 vanilla kingdoms. Gondor template (`Kingdom[@id='empire_w']`) has the misspelt policy at :71; Dunland :39 and Mordor :103 policies are valid |
| `Main/_Module/ModuleData/taom_spkingdoms.xml` | The 14 TAOM kingdoms; each `<policies>` block has 4 rows, 2 dead (first pair at :82-85) |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Flat id-to-weight table; Rivendell block ends with `rivendell_noble` at :41; 105 live rows, 0 `lindon` |
| `Main/_Module/ModuleData/configs/battle_balance_config.json` | Shipped balance config; `CulturalSurvivalBonuses` has `"rivendell": 0.4` at :24, no `lindon` |
| `Main/Features/BattleBalance/BattleBalanceConfig.cs` | Compiled defaults; `CulturalSurvivalBonuses` dictionary at :28-33 (`rivendell` 0.4 at :31) |
| `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs` | Reads the bonus by party culture id; `ApplyCulturalSurvivalBonus` :95-104 multiplies death chance by `1 - bonus` (not edited) |
| `TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs` | Level rule for weights; loads `_weights` and `_levels` (`troops/troops_*.xml`); 3 tests |
| `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs` | Holds `BattleBalanceConfigCasualtyRatiosTests` with an 8-row survival-bonus data test |
| `TAOM.Tests/Core/CultureDataFixture.cs` | `ModuleDataPath()` at :56 (walks up to `Main/_Module/ModuleData`) |
| `Main/Features/Execution/AlignmentService.cs` | `ResolveSide(kingdomId, cultureId)` at :36-43: kingdom side, else culture side when the kingdom reads Neutral |
| `Main/Features/Execution/IAlignmentService.cs` | Interface; `ResolveSide` doc at :17-26 |
| `Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs` | `Patch14_Execution` prefix on `KillCharacterAction.ApplyInternal`; snapshots kingdom id plus the hero's own culture at :31-35 |
| `Main/Features/Execution/Hooks/ExecutionContext.cs` | Thread-local snapshot: four strings plus an active flag, `TrySet`/`ClearIfOwned`/`Resolve*` |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs` | Prefix that builds two `ExecutionParticipant`s from the snapshot (:26-34) |
| `Main/Features/Execution/Models/TaomExecutionRelationModel.cs` | GameModel override; builds participants at :25-31 (player kingdom + `GetPlayerCultureId()`, victim and evaluator kingdom + own culture) |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionParticipant` struct (:8-18), service contract, `ExecutionRelationResult` |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation rule; private `Resolve` at :42-43 calls `ResolveSide(KingdomId, CultureId)` |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs`, `IOnExecutionAction.cs` | Honor-penalty rule on two participants |
| `Main/Adapters/IPlayerContextAdapter.cs`, `PlayerContextAdapter.cs` | `GetPlayerKingdomId`, `GetPlayerCultureId` (only caller: the execution model), `IsUnderMercenaryService` |
| `Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs` | Dialog entry point; `Verdict()` :70-87 passes the player clan's kingdom with the clan's culture |
| `Main/Features/WandererAllegiance/IWandererAllegianceService.cs`, `WandererAllegianceService.cs` | Pure rule; `Evaluate(heroId, cultureId, playerKingdomId, playerCultureId)` at :48, `ResolveSide` at :61 |
| `Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs` | GameModel boundary; passes `leader?.Clan?.Kingdom?.StringId` with `leader?.Culture?.StringId` at :37-41; returns `int` |
| `Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs`, `IPrisonerRecruitmentMoraleService.cs` | Two waiver rules; private `ResolveRecruiterSide` :59-65 repeats the fallback |
| `Main/Features/CaravanTrade/CaravanTradeService.cs`, `Main/Features/WarOfTheRingMomentum/MomentumEnrollmentService.cs` | Private side helpers that already pair a faction id with that faction's own culture. **Not edited** (see Disposition) |
| `Main/Features/TaomSettings.cs` | Marriage Alignment hint at :889-890 says "Neutral cultures (Umbar, Shaghana, Abanissa, Dunland)" |
| `Main/_Module/ModuleData/execution/alignment.json` | 24 keys; Neutral: `battania` :17, `shaghana` :19, `abanissa` :20, `umbar` :21; `empire` (Dunland) Evil :5; no clan ids |
| `Main/_Module/SubModule.xml` | `equipmentsets/taom_lord_template_equipment` is the last `EquipmentRosters` node (:853); the `lords` node (:108) has `lords.xslt` and no `lords.xml`; `characters/custom_battle_lords` is CustomGame only (:101) |
| `Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` | TAOM's lord, teen and ruler rosters, 226 over 22 cultures; the `STURGIA` section holds 10 rosters of vanilla Sturgian gear |
| `Main/_Module/ModuleData/lords.xslt`, `characters/lords.xml` | Both define `lord_1_48_1/2/3` (templates :6107, :6162, :6217; rows :24935, :24986, :25037; 51 lines each) |
| `Main/_Module/ModuleData/characters/custom_battle_lords.xml` | CustomGame stubs `lords.xslt` rebuilds; a comment at :25 says the three need no stub because `characters/lords.xml` defines them |
| `Main/_Module/ModuleData/module_sounds.xml` | :5 registers the elf horn under a file name that does not exist |

### Excerpts (`origin/bannerlord-1.4.5`)

`Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs:29-35`:
```csharp
        // Captured here, at the top of ApplyInternal, because the method destroys the victim's clan
        // (nulling Clan.Kingdom) before it fires OnHeroKilled, which is what drives the relation pass.
        __state = ExecutionContext.TrySet(
            victim?.Clan?.Kingdom?.StringId,
            victim?.Culture?.StringId,
            killer?.Clan?.Kingdom?.StringId,
            killer?.Culture?.StringId);
```

`Main/Features/Execution/Models/TaomExecutionRelationModel.cs:24-31`:
```csharp
        int baseDelta = base.GetRelationChangeForExecutingHero(victim, hero, out bool baseShowNotification);
        var executor = ExecutionContext.ResolveExecutor(
            _playerContext.GetPlayerKingdomId(),
            _playerContext.GetPlayerCultureId());
        var victimParticipant = ExecutionContext.ResolveVictim(
            victim?.Clan?.Kingdom?.StringId,
            victim?.Culture?.StringId);
        var evaluator = new ExecutionParticipant(hero?.Clan?.Kingdom?.StringId, hero?.Culture?.StringId);
```

`Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs:74-80`:
```csharp
            var wanderer = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;
            return _service.Evaluate(
                wanderer?.StringId,
                wanderer?.Culture?.StringId,
                playerClan?.Kingdom?.StringId,
                playerClan?.Culture?.StringId ?? Hero.MainHero?.Culture?.StringId);
```

`Main/SubModule.cs:994-995` (single-owner, NOT edited) builds the model as
`new TaomExecutionRelationModel(executionRelationService, playerContext)`, so its constructor stays.

### Engine facts (v1.4.8, read this session with `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`; cache `C:\Users\mikew\.taom-src\v1.4.8`)

- `TaleWorlds.CampaignSystem.DefaultPolicies`: `private void RegisterAll()` at :143; 32 `Create("policy_...")`
  calls at :145-176, the last `_policyLandGrandsForVeteran = Create("policy_land_grands_for_veteran");` at
  :176. Its description (:218): "Settlement militia veterancy increased by +10%{newline}Village tax income -5%".
  The 32-id set in `685db7e46`'s `KingdomPolicyIdsTests` equals the v1.4.8 set exactly (compared this session).
- `TaleWorlds.CampaignSystem.Kingdom`: `Deserialize` policies loop :801-812:
  `PolicyObject policyObject = Game.Current.ObjectManager.GetObject<PolicyObject>(childNode3.Attributes["id"].Value); if (policyObject != null) { AddPolicy(policyObject); }`.
  `InitializeKingdom` (:560) adds `Culture.DefaultPolicyList` at :574-577; `AddPolicy` :726-732
  de-duplicates; `[SaveableField(26)] private MBList<PolicyObject> _activePolicies;` at :79-80 (a saved field).
- `TaleWorlds.CampaignSystem.CultureObject`: `default_policies` loop :369-375 adds
  `objectManager.GetObject<PolicyObject>(id)` to `_defaultPolicyList` with no null check.
- `TaleWorlds.Core.Banner.TryGetBannerDataFromCode(string bannerCode, out List<BannerData> bannerDataList)`
  :576-593: `for (int i = 0; i + 10 <= array.Length; i += 10)`, ten `int.TryParse` per group; a non-integer
  clears the list and returns false; a trailing partial group is ignored.
- `TaleWorlds.MountAndBlade.GeneralsAndCaptainsAssignmentLogic` :262-264:
  `if (_bannerLogic != null && captain.FormationBanner != null) ... _bannerLogic.SetFormationBanner(formation, captain.FormationBanner);`
- `TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel.GetPossibleRewardBannerItemsForHero` :31:
  `if ((item.Culture == null || item.Culture == hero.Culture) && (item.ItemComponent as BannerComponent).BannerLevel == bannerItemLevelForHero)`.
- `TaleWorlds.CampaignSystem.Hero`: `public CultureObject Culture;` (field) :118;
  `public Clan Clan { get { return CompanionOf ?? _clan; } ... }` :499-503 (a companion reads the clan it
  serves); `public IFaction MapFaction` :567-590 (clan's kingdom or the clan; else null for a special hero;
  else `HomeSettlement.MapFaction` :580-583; else the party's).
- `TaleWorlds.CampaignSystem.Clan`: `public CultureObject Culture { get; set; }` :108; `public Kingdom Kingdom` :144;
  `public static Clan PlayerClan => Campaign.Current.PlayerDefaultFaction;` :276 (dereferences `Campaign.Current`);
  `public IFaction MapFaction { get { if (Kingdom != null) return Kingdom; return this; } }` :339-349.
- `TaleWorlds.CampaignSystem.IFaction`: `string StringId { get; }` :16, `CultureObject Culture { get; }` :28.
- `TaleWorlds.CampaignSystem.Campaign`: `public static Campaign Current { get; private set; }` :376; the player
  clan's id is `"player_faction"` (:1323). The class has no static constructor and no static field
  initializer (one `const` only), so a test can read `Current` (null) safely.
- `TaleWorlds.CampaignSystem.Actions.KillCharacterAction`:
  `private static void ApplyInternal(Hero victim, Hero killer, KillCharacterActionDetail actionDetail, bool showNotification, bool isForced = false)` :31.
- `TaleWorlds.CampaignSystem.GameComponents.DefaultExecutionRelationModel`:
  `public override int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)` :28
  (`hero` is the evaluating clan leader).
- `TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel`:
  `public override int GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)` :53
  (returns `int` on v1.4.8; the 1.5.x line returns `float`, keep `int`).
- `TaleWorlds.CampaignSystem.CharacterDevelopment.TraitLevelingHelper`: `public static void OnLordExecuted()` :62.
- (Part C) `TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior`:
  `conversation_wanderer_preintroduction_on_condition` :1719-1736 reads `prebackstory` with `FindText` and
  `generic_backstory` with `TryGetText` (vanilla fallback lines); `conversation_wanderer_introduction_on_condition`
  :1738-1766 reads `backstory_a`..`backstory_d`, `response_1`, `response_2` with `FindText`.
- (Disposition of `546d1181f`) `CompanionsCampaignBehavior`: the spawn pool is every template with
  `Occupation.Wanderer` from every module (:348), and a spawned wanderer goes to a town of its template's
  culture (:384).
- (Step C3) `TaleWorlds.CampaignSystem.GameComponents.DefaultEquipmentSelectionModel`: private
  `GetSuitableEquipmentSet` :57-79 adds `IsFemaleTemplate` for a woman, then pools every set of every roster in
  `MBEquipmentRosterExtensions.All` of the requested type where `EquipmentCulture == culture` and
  `EquipmentCategories == customFlags` (exact equality, :81-89), and returns `GetRandomElement()`; an empty pool
  yields null. `TaleWorlds.ObjectSystem.MBObjectManager`: `CreateMergedXmlFile` :966-982 applies each node's
  stylesheet to what is merged BEFORE it; `GetMergedXmlForManaged` :878-918 gives a node with no XML file an
  empty entry and its stylesheet. `TaleWorlds.Core.MBEquipmentRoster.Deserialize` reads `<Flags>` attributes in a
  loop (:73-82), so an emptied `<Flags />` reads as no category.
- (Step C4) `MBObjectManager.MergeElements` :820-875: a later definition overwrites the attributes it states,
  `Equipments` is `AlwaysPreferMerge` and `EquipmentSet`'s unique key is `@id`, `@civilian`, `@stealth`
  (`XmlSchemas/NPCCharacters.xsd`), so sets with different ids union. `TaleWorlds.CampaignSystem.Hero.SetInitialValuesFromCharacter`
  :2237: `_battleEquipment = list.GetRandomElement().Clone()` at :2274. `BasicCharacterObject.Deserialize` reads
  `<skills>` only when no `skill_template` resolved, so the rows' skill lists are inert today. SandBox registers
  its `lords` node for Campaign and CampaignStoryMode only (`Modules/SandBox/SubModule.xml:66-70`).

### Data facts (measured this session on the 1.4.x tip and on the public build that WP101 merges)

- Policy ids (regex `<policy\s+id="([^"]+)"` over raw text, the test's own rule): `taom_spkingdoms.xml`
  56 rows (`royal_privilege` 14, `lord_prerogative` 14, `religious_privilege` 14, `castle_charters` 14);
  `spkingdoms.xslt` 3 (`feudal_inheritance`, `land_grants_for_veterans`, `royal_privilege`);
  `taom_spcultures.xml` 20 and `spcultures.xslt` 24, all valid. Gondor's culture default is `policy_senate`.
- Lindon: 30 `lindon_` troops on the public tree; the 22 rows `0e75ec50a` adds all name existing troops;
  each `lindon_X` has a twin (`rivendell_X`, else `X`) of the same level and, after the port, the same
  weight; no level-rule violation anywhere in the file after the port. The public build does not touch
  `troop_weights.xml`. After the port: 127 live rows (59 at 2.0, 66 at 3.0, one 4.0, one 10.0). Rivendell
  and Lindon each have 22 of 30 ids weighted. `lindon` is a culture id in `taom_spcultures.xml` (:4925).
- Banner keys: `characters/clans.xml` 145, `taom_spkingdoms.xml` 14, `spclans.xslt` 72, `spkingdoms.xslt` 8;
  0 fail the rule. The public build changes none of these files.
- Alignment: no clan id in `characters/clans.xml` or `spclans.xslt` (238 ids) equals an `alignment.json` key.
- Wanderers (Part C and `546d1181f`): `taom_wanderers.xml` has 210 wanderer templates over 20 cultures,
  none for `sturgia` (Dale) or `battania` (Khand), none female. `taom_wanderer_strings.xml` has 1,337
  rows; 280 required rows are missing for exactly 40 wanderers; 0 orphan rows, 0 bad keys, 0 duplicates.
  The 40 wanderer definitions equal the 1.5.x parent's. `taom_wanderer_strings.xml` and 9 of the 12
  language files equal `9dd2f594f^`; KO, RU and TR differ from it by in-place line changes only.
- Part C evidence: `9dd2f594f` is 2026-10-08, `caed64ae1` (Oaths of Service) 2026-10-20 and a descendant of it;
  the commit touches no Oaths of Service file. Its added rows for the 40 wanderers are 320 per file in all 13.
- Lord templates, adult `IsLordTemplate` only, battle and civilian sets, SandBoxCore plus SandBox against TAOM:
  `vlandia` 25 and 2, `empire` 26 and 16, `aserai` 24 and 8, `khuzait` 21 and 18, `sturgia` 15 and 6, `battania`
  37 and 15; TAOM has 1 and 1 for each. TAOM's rosters fill all 60 pools the engine requests for these cultures,
  and no non-`taom_` roster in `equipmentsets/` carries an adult template flag for them. No XSLT strips vanilla's
  today. The 1.5.x `STURGIA` section is the 1.4.x one with seven item ids swapped in 10 rosters (70 lines); all
  seven ids resolve on the laptop's v2.0.26 install and `troops_dale.xml` already uses the Dale pieces.
- Nazgul: the three rows and templates agree on the face key and traits. They differ in age (row 20; templates 31,
  9, 11), race (row `uruk`; templates none), face age (row 22.19; templates none), kit (row `mordor_*_template_*`;
  templates `nazgul_*`, `nazgul_v1_*` for `_2`), and the row's hair, beard and tattoo tags. So today's effective
  race and age need no new decision: Step C4 moves them. `custom_battle_commanders.json` lists all three, so
  deleting the rows without stubs would drop them from Custom Battle. 179 ids are in both files; the other 176
  are 1.5.x's #648, not this WP.
- Sounds: `module_sounds.xml` has 249 `module_sound` and 286 `variation` elements, 485 `path` attributes, all
  resolving under `Main/_Module/ModuleSounds` except the elf horn. Apart from that line, no `elf_horn` or `horn_elf`
  in `Main` code or data, `TAOM.Tests`, `tools`, or the installed `TAOM_Map` and Armory data. The sound data is the
  same on `8df90444c`.

### Files byte-identical to the 1.5.x parent (checked against `1b00d1acd`; Step 0 re-checks them)

For these, `git checkout <sha> <path>` yields exactly "1.4.x plus that commit's hunk":
- vs `0e75ec50a^`: `Main/Features/BattleBalance/BattleBalanceConfig.cs`,
  `Main/_Module/ModuleData/configs/battle_balance_config.json`, `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs`.
- vs `5e43518df^`: `Main/Features/Execution/ExecutionRelationService.cs`,
  `Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs`,
  `Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs`,
  `Main/Features/WandererAllegiance/WandererAllegianceService.cs`,
  `Main/Features/MarriageAlignment/MarriageAlignmentService.cs`,
  `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`,
  `TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs`,
  `TAOM.Tests/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleServiceTests.cs`,
  `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceServiceTests.cs`, `docs/features/marriage-alignment.md`.

### Not on 1.4.x yet (`git grep` on `origin/bannerlord-1.4.5` this session)

None of `FactionSides`, `SideIds`, `KingdomPolicyIdsTests`, `ShippedHeraldryBannerKeyTests`,
`ShippedBattleBalanceConfigTests`, `EveryLindonTroop`, `WandererBackstoryCoverageTests`,
`GetPlayerSideIds`, `policy_land_grands_for_veteran`, `lord_template_rosters`, `LordTemplateRosterTests`,
`LesserNazgulDefinitionTests`, `ModuleSoundPathTests` exists in `Main`, `TAOM.Tests`, `tools` or `docs`.
`TAOM.Tests/Infrastructure/RepoPaths.cs` is absent until WP104.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types. `FactionSides` lives in `Main/Adapters`
  and is the only place a side pair is built from `Hero`/`Clan`/`IFaction`; services take `SideIds`.
  GameModels and Harmony patches are entry points and may call it at their boundary.
- ADR-002 thin entry points (under 150 lines, delegate to services): the dialog behavior, both GameModels
  and both patches only convert at the boundary and delegate.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`: `ExecutionParticipant` and `GetPlayerCultureId` are
  removed with every caller migrated in the same commit, no shim; ADR-005 no `#if DEBUG` outside IoC.
- `.claude/rules/gamemodels.md` rule 4: a GameModel body is boundary conversion plus one delegate, no
  inline branching. Keep it so in both models.
- TDD: each step runs its test RED first (a failing assertion or, for signature changes, a compile
  failure), then GREEN.
- MCM json2 rule (rename a persisted setting, never flip its default): no setting is renamed or
  re-defaulted; only one `HintText` string changes, which is not persisted.
- `.claude/rules/harmony-patches.md` (MovementOrder postfixes through the shared deferred category): no
  patch target or category changes; `Patch14_Execution` keeps its prefix and finalizer signatures.
- Line endings: `core.autocrlf=true` on this laptop (worktree CRLF, blobs LF). Use the Edit tool for text
  edits (it keeps line endings) and the byte-level Python snippets given here; never convert a file.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/134 &&` (Bash tool). Give full test
runs a 600000 ms timeout. Never `./build.ps1`; never two builds at once in one worktree.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | as each step says |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Docs | `python tools/lint_docs.py` | no finding on a line this plan added |

Both `-p:` flags are mandatory on every build and test: on 1.4.x `Main/TAOM.csproj` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game
install. Run Build before every Filtered or Test run (both use `--no-build`).

## Scope

**Part A in scope**:
- `Main/_Module/ModuleData/spkingdoms.xslt`, `Main/_Module/ModuleData/taom_spkingdoms.xml`
- `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`, `Main/_Module/ModuleData/configs/battle_balance_config.json`,
  `Main/Features/BattleBalance/BattleBalanceConfig.cs`
- New: `TAOM.Tests/Core/KingdomPolicyIdsTests.cs`, `TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs`,
  `TAOM.Tests/Features/BannerBearers/ShippedHeraldryBannerKeyTests.cs`
- `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs`, `TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs`
- `docs/features/kingdom-creation.md`, `docs/modding/kingdoms.md`, `docs/features/battle-balance.md`,
  `docs/features/troop-weight-system.md`, `docs/features/banner-bearers.md`

**Part B in scope**:
- New: `Main/Adapters/FactionSides.cs`, `Main/Features/Execution/SideIds.cs`,
  `Main/Features/Execution/AlignmentServiceExtensions.cs`, `TAOM.Tests/Adapters/FactionSidesTests.cs`,
  `TAOM.Tests/Features/Execution/SidePairScanTests.cs`
- `Main/Features/Execution/IAlignmentService.cs`, `IExecutionRelationService.cs`, `ExecutionRelationService.cs`,
  `Hooks/ExecutionActionHook.cs`, `Hooks/IOnExecutionAction.cs`, `Hooks/ExecutionContext.cs`,
  `Hooks/KillCharacterAction_ApplyInternal_Patch.cs`, `Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs`,
  `Models/TaomExecutionRelationModel.cs` (all under `Main/Features/Execution/`)
- `Main/Adapters/IPlayerContextAdapter.cs`, `Main/Adapters/PlayerContextAdapter.cs`
- `Main/Features/WandererAllegiance/IWandererAllegianceService.cs`, `WandererAllegianceService.cs`,
  `Hooks/WandererAllegianceDialogBehavior.cs`
- `Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs`, `PrisonerRecruitmentMoraleService.cs`,
  `Models/TaomPrisonerRecruitmentCalculationModel.cs`
- `Main/Features/MarriageAlignment/MarriageAlignmentService.cs` (comment), `Main/Features/TaomSettings.cs` (one `HintText`)
- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`, `ExecutionRelationServiceTests.cs`,
  `ExecutionActionHookTests.cs`, `ExecutionContextTests.cs`;
  `TAOM.Tests/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleServiceTests.cs`;
  `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceServiceTests.cs`
- `docs/features/alignment-aware-execution.md`, `docs/features/execution.md`,
  `docs/features/wanderer-allegiance.md`, `docs/features/prisoner-recruitment.md`, `docs/features/marriage-alignment.md`

**Part C in scope**: new `TAOM.Tests/Core/WandererBackstoryCoverageTests.cs`;
`Main/_Module/ModuleData/taom_wanderer_strings.xml` and the 12
`Main/_Module/ModuleData/Languages/*/std_taom_wanderer_strings_*.xml`. New `Main/_Module/ModuleData/lord_template_rosters.xslt`,
`TAOM.Tests/Core/LordTemplateRosterTests.cs`, `TAOM.Tests/Features/NazgulFamily/LesserNazgulDefinitionTests.cs`,
`TAOM.Tests/Core/ModuleSoundPathTests.cs`; `Main/_Module/SubModule.xml` (one node),
`Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` (`STURGIA` section),
`Main/_Module/ModuleData/lords.xslt` (three templates), `Main/_Module/ModuleData/characters/lords.xml` (three rows),
`Main/_Module/ModuleData/characters/custom_battle_lords.xml`, `Main/_Module/ModuleData/module_sounds.xml` (one line);
`docs/modding/equipment-rosters.md`, `docs/features/nazgul-family.md`, `docs/features/custom-battles.md`, `docs/features/uncapturable-heroes.md`, `TAOM.Tests/Features/UncapturableHeroes/UncapturableRegistryTests.cs`,
`docs/features/kingdom-voices.md`.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` get no edit (the project is
SDK-style and picks up new `.cs` files; `InternalsVisibleTo TAOM.Tests` is already at `Main/TAOM.csproj:119-121`).
Needing an edit there is a STOP.

**Out of scope** (do not touch, even though the 1.5.x commits do):
- `CaravanTradeService.cs`, `MomentumEnrollmentService.cs` and their tests (behaviour-equivalent
  refactors, not ported; plan 110 owns `MomentumEnrollmentService.SweepEnrollment`).
- Everything of `685db7e46` except the policy hunks: markets (`CultureMarketplace*`, `StockRule`,
  `culture_marketplace_config.xml`), `ArmourAcquisition` (absent on 1.4.x), `ItemPoolAdapter.cs`,
  `tools/validate_moduledata.py`, `tools/rebalance_armor.py`, their tests, `.claude/rules/moduledata-validation.md`,
  `.claude/skills/engine-bump/SKILL.md` (routed to WP143).
- Everything of `736fa2fef` except the heraldry test: `Main/Features/LordBanners/**`, `TaomBannerItemModel`,
  `lord_banner_cultures.json`, the BannerBearers comment edits, `.claude/rules/gamemodels.md`, registry rows.
- Everything of `5e43518df` listed as skipped in the Disposition table (MinorClanMigration, Arena,
  LordCourtesies, HardQuestions, FromTheAshes, GoodDeeds, BondsOfBattle, TokensOfEsteem, minor-clan data in
  `alignment.json`, `lords.xslt`, `spclans.xslt`, the custom-battle filter, `tools/complete_lords_xslt.py`).
- Everything of `bb68eeca3` and `db805427b` except the Step C3 stylesheet, its node, its test and the Dale rows
  (WP123 owns `bb68eeca3` items 1 and 2; GeneratedLordKits is 1.5.x-only). Everything of `9804f67b9` except the
  `lord_1_48_1/2/3` definition; race `nazghul` stays out, the three stay `uruk`. The other 176 lords in both files.
- `546d1181f` entirely (Disposition). `1641fb985` (Dunland `default_group` retags) belongs to WP130 under D13.
- `CHANGELOG.md` (D1), `docs/reviews/**` (RCAs, lessons, REVIEW-LOG), `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md`, `.claude/**` (WPs 142 to 144 sync these).
- Any balance number other than Lindon's 22 weight rows and its survival bonus, each copied from its
  Rivendell twin (D13).
- Anything in the game install; the main checkout's `.serena/project.yml` and any stash there.

## Git workflow

- Worktree (create once; reuse it if it exists):
  `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/134 -b port148/134-campaign-data-defects bannerlord-1.4.5`
  Base: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one. Work only there.
- Machine: the maintainer's laptop; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install. At most four executor worktrees at once.
- Git only through the Bash tool (D19). Stage explicit paths; never `git add -A`, `git add .` or
  `git commit -a`. Never stash, `reset --hard`, pull, push, tag, open a PR or merge into
  `bannerlord-1.4.5`; the integrator merges.
- Per-port procedure (binding). This plan applies no whole commit: `685db7e46`, `736fa2fef` and
  `5e43518df` would drag in 1.5.x-only features (the merge-tree simulation this session conflicts in 28,
  7 and 78 paths), and `0e75ec50a` is applied file by file so its tests can run RED first. So there is no
  `git cherry-pick` here at all. Files byte-identical to the 1.5.x parent are taken with
  `git checkout <sha> <path>` (this also stages them; that is expected); everything else is edited by
  hand as each step says. Never let git create a commit from a 1.5.x message (`GIT_EDITOR=true` on this
  laptop would commit it unedited).
- Commit: write the message with the Write tool to `C:\Users\mikew\source\repos\taom-port\msg-134-<n>.txt`
  (outside every worktree), stage with explicit `git add <paths>`, then run
  `git commit -F C:/Users/mikew/source/repos/taom-port/msg-134-<n>.txt` in the NEXT sequential Bash call,
  never in the same command and never through PowerShell.
- Commit template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters (each subject
  below is measured); body wrapped at 72, written for 1.4.x readers (under plan 020 bodies become
  release notes), no em or en dashes, no Co-Authored-By or "Generated with" line; trailers `Research:`,
  `Not-tested:`, optional `Save-compat:`/`Rejected:`, and one `Ported-from: <40-hex sha>` per included
  1.5.x commit. Types and scopes follow the hunk actually ported (for example `fix(kingdoms)`, not the
  `feat(markets)` commit the policy fix rode in). The exact messages are given in each step.
- After every commit: `git log -1 --format=%s` prints that step's subject and `git status --porcelain`
  prints nothing. Build and test after each commit; full suite at the end of each part.

## Steps

### Step 0: Preflight (read-only apart from the worktree and the build)

1. Create the worktree (command above), then:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git status --porcelain | wc -l && git rev-parse HEAD && echo "$BANNERLORD_GAME_DIR"
   ```
   Expected: `0`, a sha (record it as `BASE`), the game path. An empty game path is an environment
   failure: STOP and report; never set the variable yourself.
2. Preconditions:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && \
   grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml"; \
   test -f TAOM.Tests/Infrastructure/RepoPaths.cs && echo REPOPATHS-OK; \
   grep -c 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
   grep -c 'public static IEnumerable<MethodBase> EnumerateMethods(Assembly assembly)' TAOM.Tests/Migration/IlCallScanner.cs; \
   grep -c 'public static IEnumerable<MethodBase> ExtractCalledMethods(MethodBase method, byte\[\] il)' TAOM.Tests/Migration/IlCallScanner.cs; \
   grep -c 'public static bool EnsureLoaded()' TAOM.Tests/Migration/GameAssemblies.cs; \
   git grep -n -e 'FactionSides' -e 'SideIds' -e 'KingdomPolicyIdsTests' -e 'ShippedHeraldryBannerKeyTests' -e 'ShippedBattleBalanceConfigTests' -e 'WandererBackstoryCoverageTests' -e 'GetPlayerSideIds' -e 'policy_land_grands_for_veteran' -e 'lord_template_rosters' -e 'LordTemplateRosterTests' -e 'LesserNazgulDefinitionTests' -e 'ModuleSoundPathTests' -- Main TAOM.Tests; echo "grep exit $?"; \
   git grep -n 'GetPlayerCultureId' -- Main TAOM.Tests; \
   git grep -n '\.ResolveSide(' -- Main
   ```
   Expected, in order: an Armory version that is NOT `v2.0.26` (WP101 installs the public 2026-09-29
   build, `v2.0.29.5` per plan 101); `REPOPATHS-OK`; `1`; `1`; `1`; `1`; no `git grep` line, then
   `grep exit 1`; exactly three `GetPlayerCultureId` lines (`Main/Adapters/IPlayerContextAdapter.cs`,
   `Main/Adapters/PlayerContextAdapter.cs`, `Main/Features/Execution/Models/TaomExecutionRelationModel.cs`);
   exactly four `.ResolveSide(` lines (`ExecutionRelationService.cs` one, `Hooks/ExecutionActionHook.cs`
   two, `WandererAllegianceService.cs` one). The Armory check gates Parts A and C only; RepoPaths gates
   Part B only.
3. Drift check (the command at the top), then the byte-identical files:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && \
   for p in Main/Features/BattleBalance/BattleBalanceConfig.cs Main/_Module/ModuleData/configs/battle_balance_config.json TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs; do git diff --quiet 0e75ec50a^ HEAD -- "$p" && echo "SAME $p" || echo "DIFF $p"; done; \
   for p in Main/Features/Execution/ExecutionRelationService.cs Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs Main/Features/WandererAllegiance/WandererAllegianceService.cs Main/Features/MarriageAlignment/MarriageAlignmentService.cs TAOM.Tests/Features/Execution/AlignmentServiceTests.cs TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs TAOM.Tests/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleServiceTests.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceServiceTests.cs docs/features/marriage-alignment.md; do git diff --quiet 5e43518df^ HEAD -- "$p" && echo "SAME $p" || echo "DIFF $p"; done
   ```
   Expected: 13 `SAME` lines. For any `DIFF`, do not check that file out later: apply that commit's
   hunk for it by hand (`git show <sha> -- <path>` prints it) and note it in your report. STOP only if
   the hand application meets code this plan does not describe.
4. Text anchors the edits need (each must print `1`):
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && \
   grep -c 'id="policy_land_grants_for_veterans" />' Main/_Module/ModuleData/spkingdoms.xslt; \
   grep -c '<TroopWeight id="rivendell_noble" weight="2.0" />' Main/_Module/ModuleData/TroopWeights/troop_weights.xml; \
   grep -c 'public void EveryWeightedId_ResolvesToATroopOrIsANamedMountPackage()' TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs; \
   grep -c 'Neutral cultures (Umbar, Shaghana, Abanissa, Dunland) marry anyone.' Main/Features/TaomSettings.cs; \
   grep -c 'playerClan?.Culture?.StringId ?? Hero.MainHero?.Culture?.StringId);' Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs; \
   grep -c 'leader?.Clan?.Kingdom?.StringId,' Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs; \
   grep -c 'The `<policies>` block should include at minimum:' docs/features/kingdom-creation.md; \
   grep -c '^## Testing' docs/features/banner-bearers.md; \
   grep -c 'path="equipmentsets/taom_lord_template_equipment"/>' Main/_Module/SubModule.xml; \
   grep -c '<!-- ==================== STURGIA' Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml; \
   grep -c "NPCCharacter\[@id='lord_1_48_1'\]" Main/_Module/ModuleData/lords.xslt; \
   grep -c 'lord_1_48_1/2/3 (the lesser Nazgul) are defined in characters/lords.xml' Main/_Module/ModuleData/characters/custom_battle_lords.xml; \
   grep -c 'path="LOTR/Elves/Alert/elf_horn.wav"' Main/_Module/ModuleData/module_sounds.xml; \
   grep -c 'IsTeenagerEquipmentTemplate` | 44 | teenager' docs/modding/equipment-rosters.md; \
   grep -c '^## Tests' docs/features/nazgul-family.md; \
   grep -c 'the curated Mordor, Gondor and Rohan commanders now exist in Custom Battle' docs/features/custom-battles.md; \
   grep -c 'defined twice\*\*~~ | RESOLVED 2026-08-28' docs/features/kingdom-voices.md
   ```
5. Baseline build and tests:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
   ```
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
   ```
   Expected: build exit 0; `Failed: 0`. Record `BASE_TOTAL`, `BASE_PASSED`, `BASE_SKIPPED` (compare with
   the count the previous merged WP recorded in the main checkout's `plans/README.md`). Then run Filtered with
   `--filter "FullyQualifiedName~BattleBalanceConfigCasualtyRatiosTests"`: `11` results means data rows
   are reported one per row (use the expected totals as written below); `4` means data rows fold into
   one result (use the "folded" totals).

**Verify**: every expectation above holds.

## Part A: data fixes and data gates

### Step A1: Kingdoms list only policies the game defines (`685db7e46`, partial, by hand)

Source: `685db7e46` "feat(markets): v2.0.34 - Arthedain and Lindon sell their own gear" (Closes #755,
#756). Take only the #756 hunks: `spkingdoms.xslt`, the dead rows in `taom_spkingdoms.xml`, and
`TAOM.Tests/Core/KingdomPolicyIdsTests.cs`.

1. **RED.** Create the test from the 1.5.x blob:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git show 685db7e46:TAOM.Tests/Core/KingdomPolicyIdsTests.cs > TAOM.Tests/Core/KingdomPolicyIdsTests.cs
   ```
   Then, with the Edit tool, re-cite its comments for v1.4.8 (keep the 32-id set unchanged; it equals
   v1.4.8's):
   - `(Kingdom.cs:796-807, v1.5.4)` becomes `(Kingdom.cs:801-812, v1.4.8)`.
   - `all 15 TAOM kingdoms listed two (#756)` becomes `all 14 kingdoms in taom_spkingdoms.xml listed two (#756)`.
   - `(CultureObject.cs:376-377)` becomes `(CultureObject.cs:369-375, v1.4.8)`.
   - `// DefaultPolicies.RegisterAll, v1.5.4 (` becomes `// DefaultPolicies.RegisterAll (DefaultPolicies.cs:143-176), v1.4.8, re-read 2026-11-04 (`.
   - `Re-read on an engine bump (/engine-bump lists this set).` becomes `Re-read it on an engine bump.`
   Build, then Filtered `--filter "FullyQualifiedName~KingdomPolicyIdsTests"`.
   **Verify RED**: 4 results (1 folded), `Failed: 2`: the `taom_spkingdoms.xml` row names
   `policy_lord_prerogative, policy_religious_privilege`, the `spkingdoms.xslt` row names
   `policy_land_grants_for_veterans`; the two culture rows pass.
2. **GREEN.** In `Main/_Module/ModuleData/spkingdoms.xslt` (Gondor, :71) replace
   `id="policy_land_grants_for_veterans" />` with `id="policy_land_grands_for_veteran" />` (Edit tool).
   Remove the 28 dead rows from `taom_spkingdoms.xml`, byte for byte:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && python - <<'EOF'
   import pathlib, re
   p = pathlib.Path("Main/_Module/ModuleData/taom_spkingdoms.xml")
   data = p.read_bytes()
   pat = re.compile(rb'[ \t]*<policy\r?\n[ \t]*id="policy_(?:lord_prerogative|religious_privilege)" />\r?\n')
   new, n = pat.subn(b"", data)
   print("removed", n)
   assert n == 28, "expected 28 dead policy rows"
   p.write_bytes(new)
   EOF
   ```
   **Verify**: prints `removed 28`; `git diff --stat -- Main/_Module/ModuleData/taom_spkingdoms.xml` shows
   `56 deletions(-)` and no insertion; `git grep -c 'policy_lord_prerogative\|policy_religious_privilege' -- Main/_Module/ModuleData`
   prints nothing; Build, then the Filtered run above shows `Failed: 0` (4 passed, 1 folded).
3. **Docs** (Edit tool):
   - `docs/features/kingdom-creation.md`: replace the line `The `<policies>` block should include at minimum:`
     with `The `<policies>` block lists ids the engine defines (`DefaultPolicies`); an unknown id is dropped without a message, and `KingdomPolicyIdsTests` fails on one (#756). TAOM's kingdoms start with:`
     and delete the two lines `    <policy id="policy_lord_prerogative" />` and
     `    <policy id="policy_religious_privilege" />` from the example below it. Replace
     `There are no automated unit tests for kingdom XML data. Validation is performed by:` with
     `` `KingdomPolicyIdsTests` checks every kingdom and culture policy id against the engine's list (#756); the rest of a kingdom's data is validated by:``.
   - `docs/modding/kingdoms.md`: in the "Every reference needs the `Type.id` dot form" bullet change
     `` `Kingdom.cs:806` `` to `` `Kingdom.cs:807` (v1.4.8) ``; in the `<policies>` table row change `` `Kingdom.cs:800` `` to
     `` `Kingdom.cs:801` (v1.4.8) ``; in the `id` row change `An id that does not resolve is skipped in silence.`
     to `An id that does not resolve is skipped in silence; `KingdomPolicyIdsTests` fails on one. Vanilla's own `policy_land_grants_for_veterans` is such an id (the engine spells it `policy_land_grands_for_veteran`, `DefaultPolicies.cs:176`), fixed in `spkingdoms.xslt` (#756), so new campaigns start Gondor with Land Grants for Veterans.`
     and its `` `Kingdom.cs:806` `` to `` `Kingdom.cs:807-811` (v1.4.8) ``; in the "Policies are additive"
     paragraph change `Kingdom.cs:573-576` to `Kingdom.cs:574-577` and `Kingdom.cs:725-731` to
     `Kingdom.cs:726-732`; delete the four lines of the two dead `<policy` blocks in the worked example;
     change `(`Kingdom.cs:806-810`)` to `(`Kingdom.cs:807-811`, v1.4.8; `KingdomPolicyIdsTests` now fails on one, #756)`;
     in Numbers change `**56** `<policy>` rows` to `**28** `<policy>` rows` and that line's
     `measured 2026-09-05` to `measured 2026-11-04` (re-check: `grep -c '<relationship\b' Main/_Module/ModuleData/taom_spkingdoms.xml`
     prints `220`, `grep -c '<policy\b' ...` prints `28`); change `**4** `<policy>` rows on the Erebor entry`
     to `**2** `<policy>` rows on the Erebor entry` and that line's date likewise.
4. **Commit** (message file `msg-134-1.txt`; stage `Main/_Module/ModuleData/spkingdoms.xslt`
   `Main/_Module/ModuleData/taom_spkingdoms.xml` `TAOM.Tests/Core/KingdomPolicyIdsTests.cs`
   `docs/features/kingdom-creation.md` `docs/modding/kingdoms.md`):
   ```text
   fix(kingdoms): v2.0.28 - drop dead policy ids, fix Gondor's policy

   Kingdoms no longer list two policies the game does not have. All 14
   kingdoms in taom_spkingdoms.xml listed Lord's Prerogative and
   Religious Privilege; Bannerlord 1.4.8 defines neither id and drops
   them without a message, so nothing changes in play.

   Gondor's only kingdom policy used the policy's display spelling
   instead of the engine id (policy_land_grands_for_veteran), so Gondor
   started without it. New campaigns now start Gondor with Land Grants
   for Veterans: settlement militia veterancy +10%, village tax income
   -5%. A saved campaign keeps the policy list it saved.

   A new test fails on any kingdom or culture policy id the game does
   not define.

   Research: taom-src v1.4.8 DefaultPolicies.RegisterAll (32 ids, :145-176)
   Research: taom-src v1.4.8 Kingdom.Deserialize (Kingdom.cs:801-812)
   Save-compat: no new data; Kingdom._activePolicies is saved (:79-80)
   Not-tested: Gondor's policy list in a new campaign
   Ported-from: 685db7e46cfae268fe3ac765cab7ac6e1468cc84
   ```
   **Verify**: `git log -1 --format=%s` is the subject above; `git status --porcelain` is empty.

### Step A2: Lindon troops weigh and survive like Rivendell's (`0e75ec50a`, file by file)

Source: `0e75ec50a` "fix(balance): v2.0.34 - Lindon troops weigh and survive like Rivendell's" (Refs #749).
Its code, data and test hunks simulate clean onto 1.4.x; only docs and lessons conflict. The weights are
1.5.x's, copied as integrity (each equals its twin's), never retuned (D13).

1. **RED (tests first).**
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git checkout 0e75ec50a TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs
   ```
   (The second file adds one `[DataRow("lindon", 0.4f)]`; it is byte-identical to the parent per Step 0.3.)
   In `TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs` (Edit tool): in the class summary
   replace the line `/// the file.` with
   ```csharp
   /// the file. The rule only sees troops that have a row, so a cloned tree that ships with no rows at
   /// all passes it; the clone-parity test below covers that case for Lindon.
   ```
   and insert, directly above the `[TestMethod]` line that precedes
   `public void EveryWeightedId_ResolvesToATroopOrIsANamedMountPackage()`:
   ```csharp
       /// <summary>
       /// Lindon's tree is a clone of Rivendell's under a <c>lindon_</c> prefix: <c>lindon_X</c> twins
       /// <c>rivendell_X</c> where that troop exists, else plain <c>X</c> (the <c>imladris_*</c> and
       /// unprefixed elites). Lindon shipped with no rows at all, so every Mithlond elite paid 1.0.
       /// </summary>
       [TestMethod]
       public void EveryLindonTroop_WeighsTheSameAsItsRivendellTwin()
       {
           const string prefix = "lindon_";
           var lindon = _levels.Keys.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
           Assert.IsTrue(lindon.Count > 0, "no lindon_ troop resolved; the sweep is empty");

           float WeightOf(string id) => _weights.TryGetValue(id, out var w) ? w : 1.0f;

           var mismatched = new List<string>();
           var unpaired = new List<string>();
           foreach (var id in lindon.OrderBy(id => id, StringComparer.Ordinal))
           {
               var bare = id.Substring(prefix.Length);
               var prefixed = "rivendell_" + bare;
               var twin = _levels.ContainsKey(prefixed) ? prefixed : bare;
               if (!_levels.ContainsKey(twin))
                   unpaired.Add(id);
               else if (WeightOf(id) != WeightOf(twin))
                   mismatched.Add($"{id} = {WeightOf(id).ToString(CultureInfo.InvariantCulture)}, "
                       + $"{twin} = {WeightOf(twin).ToString(CultureInfo.InvariantCulture)}");
           }

           Assert.AreEqual(0, unpaired.Count,
               $"{unpaired.Count} Lindon troops have no Rivendell twin (a renamed twin, or a troop of Lindon's own: "
               + "weigh it by the level rule and exempt it here by name):\n  " + string.Join("\n  ", unpaired));
           Assert.AreEqual(0, mismatched.Count,
               $"{mismatched.Count} Lindon troops weigh differently from their Rivendell twin:\n  "
               + string.Join("\n  ", mismatched));
       }

   ```
   (This is `0e75ec50a`'s method verbatim; the file already has the `System`, `System.Collections.Generic`,
   `System.Globalization` and `System.Linq` usings.) Build, then Filtered
   `--filter "FullyQualifiedName~ShippedBattleBalanceConfigTests|FullyQualifiedName~TroopWeightLevelBandTests|FullyQualifiedName~BattleBalanceConfigCasualtyRatiosTests"`.
   **Verify RED**: 17 results (9 folded), `Failed: 3`: `ShippedConfig_Lindon_MatchesRivendellSurvivalBonus`
   ("has no lindon row"), `EveryLindonTroop_WeighsTheSameAsItsRivendellTwin` (22 mismatched), and the
   `lindon` row of `GetCulturalSurvivalBonus_AllDefinedCultures_ReturnExpectedBonus`.
2. **GREEN.**
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git checkout 0e75ec50a Main/Features/BattleBalance/BattleBalanceConfig.cs Main/_Module/ModuleData/configs/battle_balance_config.json
   ```
   In `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` (Edit tool) insert after the line
   `    <TroopWeight id="rivendell_noble" weight="2.0" />` this block (a blank line before it, the
   existing blank line after it stays):
   ```xml

       <!-- ===== LINDON / MITHLOND (Falathrim Elves) ===== -->
       <!-- A clone of the Rivendell tree under a lindon_ prefix: every row matches its Rivendell twin
            (TroopWeightLevelBandTests.EveryLindonTroop_WeighsTheSameAsItsRivendellTwin). -->
       <!-- Level 51 (3.0) -->
       <TroopWeight id="lindon_glorfindel_guard" weight="3.0" />
       <TroopWeight id="lindon_gondolin_battlemaster" weight="3.0" />
       <TroopWeight id="lindon_high_captain" weight="3.0" />
       <TroopWeight id="lindon_knight_golden_flower" weight="3.0" />
       <TroopWeight id="lindon_warden_gondolin" weight="3.0" />
       <!-- Level 41 and 46 (3.0) -->
       <TroopWeight id="lindon_royal_knight" weight="3.0" />
       <TroopWeight id="lindon_royal_guard" weight="3.0" />
       <TroopWeight id="lindon_noldorin_lancer" weight="3.0" />
       <TroopWeight id="lindon_rider_of_himring" weight="3.0" />
       <TroopWeight id="lindon_battlemaster_of_the_first_age" weight="3.0" />
       <TroopWeight id="lindon_imladris_blademaster" weight="3.0" />
       <TroopWeight id="lindon_imladris_horse_archer" weight="3.0" />
       <TroopWeight id="lindon_imladris_marchwarden" weight="3.0" />
       <TroopWeight id="lindon_imladris_outrider" weight="3.0" />
       <TroopWeight id="lindon_imladris_warden" weight="3.0" />
       <!-- Level 21 to 36 (2.0) -->
       <TroopWeight id="lindon_imladris_guardsman" weight="2.0" />
       <TroopWeight id="lindon_imladris_marksman" weight="2.0" />
       <TroopWeight id="lindon_imladris_nobleman" weight="2.0" />
       <TroopWeight id="lindon_imladris_swordguard" weight="2.0" />
       <TroopWeight id="lindon_imladris_archer" weight="2.0" />
       <TroopWeight id="lindon_imladris_bowman" weight="2.0" />
       <TroopWeight id="lindon_noble" weight="2.0" />
   ```
   **Verify**: `grep -c 'id="lindon_' Main/_Module/ModuleData/TroopWeights/troop_weights.xml` prints `22`;
   `python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/TroopWeights/troop_weights.xml').getroot();w=[x.get('weight') for x in r.findall('.//TroopWeight')];print(len(w),sorted(collections.Counter(w).items()))"`
   prints `127 [('10.0', 1), ('2.0', 59), ('3.0', 66), ('4.0', 1)]` (if an earlier WP changed other rows,
   the Lindon part must still be +22: +15 at 3.0, +7 at 2.0); Build, then the Filtered run of step 1
   shows `Failed: 0`.
3. **Docs** (Edit tool; keep the 1.4.x text, add only these facts):
   - `docs/features/battle-balance.md`: in the JSON excerpt change `"erebor": 0.3, "rivendell": 0.4,` to
     `"erebor": 0.3, "rivendell": 0.4, "lindon": 0.4,`; after the line that starts with
     `- [TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs]` add
     `- [TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs](../../TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs): **1 test**, Lindon's row in the shipped JSON equals Rivendell's (#749). It reads the file with `ObjectCreationHandling.Replace`, because the provider's default merge would fill a missing key from the compiled dictionary and hide its absence.`;
     under `## Changelog` add as the first bullet
     `- <today's date>: Lindon gets Rivendell's 0.4 survival bonus in the JSON and the compiled default (#749, ported from 1.5.x `0e75ec50a`). It had none since Lindon became its own culture in `cc1713eb3` (2026-08-11).`
   - `docs/features/troop-weight-system.md`: after `Rivendell has 22 of its 30 troop ids weighted` insert
     ` (Lindon's clone of that tree the same 22 of 30 since #749)`; replace `than a gap. Nobody has written down which.`
     with `than a gap. Lindon, a clone of the Rivendell tree, has carried the same weights since #749 and is in the same position. Nobody has written down which.`;
     `**105 live rows**` becomes `**127 live rows**`; in the tier table `| 3.0 | 51 |` becomes `| 3.0 | 66 |`,
     `the Rivendell Gondolin line (5)` becomes `the Rivendell and Lindon Gondolin lines (5 each)`,
     `the Imladris and Mirkwood elites` becomes `the Imladris, Lindon and Mirkwood elites`, `| 2.0 | 52 |`
     becomes `| 2.0 | 59 |`, `the lower Imladris/Mirkwood elves` becomes `the lower Imladris/Lindon/Mirkwood elves`;
     after the "The level rule (#585, 2026-09-13)." paragraph insert the paragraph
     `**The clone rule (#749).** The level rule sees only troops that have a row, so a cloned tree that ships with no rows passes it: Lindon did from 2026-08-11 until #749. Every `lindon_X` troop now weighs the same as its Rivendell twin (`rivendell_X` where that troop exists, else `X`), and `TroopWeightLevelBandTests.EveryLindonTroop_WeighsTheSameAsItsRivendellTwin` fails on a Lindon troop that differs from its twin or has none.`;
     in the `<!-- measured: ... -->` comment after the level-rule paragraph and in the one inside the
     Key Files row change the date `2026-09-13` to today; in the Key Files row change `**105 live rows: 52 at 2.0, 51 at 3.0, one at 4.0, one at 10.0.** A raw grep returns 106`
     to `**127 live rows: 59 at 2.0, 66 at 3.0, one at 4.0, one at 10.0.** A raw grep returns 128`; under
     `## Changelog` add the first bullet `- <today's date>: Lindon's 22 elite rows, each its Rivendell twin's weight (15 at 3.0, 7 at 2.0), plus the clone-parity test (#749, ported from 1.5.x `0e75ec50a`).`;
     in the GitHub issue list add `- **Bug fix:** #749: Lindon troops take one party slot and get no survival bonus`.
4. **Commit** (message file `msg-134-2.txt`; stage `TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs`
   `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs` `TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs`
   `Main/Features/BattleBalance/BattleBalanceConfig.cs` `Main/_Module/ModuleData/configs/battle_balance_config.json`
   `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` `docs/features/battle-balance.md` `docs/features/troop-weight-system.md`):
   ```text
   fix(balance): v2.0.28 - Lindon troops weigh and survive like Rivendell's

   Lindon's troops now take party space and survive battle the way
   Rivendell's do. Both were lost when Lindon became its own culture
   (cc1713eb3): its troop tree and culture id never reached the two
   tables keyed on Rivendell's.

   - Lindon's 22 elite troops take the same party space as their
     Rivendell twins: the 15 at level 41 and above count as three
     troops each, the 7 from level 26 to 36 as two. Before, every
     Lindon troop counted as one. Recruits, infantry, militia and
     mercenaries stay at one, as Rivendell's do.
   - Parties and garrisons of Lindon's culture get Rivendell's survival
     bonus: a soldier struck down in battle dies 40 percent less often.

   Saved campaigns pick both up on load. A Lindon AI party that already
   holds too many elites sheds the surplus the next time it upgrades
   troops; the player's party is never trimmed and shows its weighted
   size on the party screen.

   A new test fails if any Lindon troop stops weighing the same as its
   Rivendell twin.

   Research: 30 lindon_ troops checked on the merged tree, twins by level
   Not-tested: party screen weights and survival in a live campaign
   Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d
   ```

### Step A3: Gate every shipped heraldry banner key (`736fa2fef`, test only, relocated)

Source: `736fa2fef` "fix(banners): v2.0.35 - lords of TAOM cultures carry battle banners" (#823). Take only
`ShippedHeraldryBannerKeyTests`, moved under BannerBearers, reading files through `CultureDataFixture`
(no RepoPaths dependency in Part A). The data passes today, so there is no RED for the four data tests;
the five rule rows exercise the rule itself.

1. Take the 1.5.x file into its new folder:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git show 736fa2fef:TAOM.Tests/Features/LordBanners/ShippedHeraldryBannerKeyTests.cs > TAOM.Tests/Features/BannerBearers/ShippedHeraldryBannerKeyTests.cs
   ```
   Then edit it (Edit tool):
   - `using static TAOM.Tests.Infrastructure.RepoPaths;` becomes `using TAOM.Tests.Core;`.
   - `namespace TAOM.Tests.Features.LordBanners;` becomes `namespace TAOM.Tests.Features.BannerBearers;`.
   - The line `var text = File.ReadAllText(RepoPath(("Main/_Module/ModuleData/" + relativePath).Split('/')));`
     becomes the two lines
     ```csharp
             var parts = new[] { CultureDataFixture.ModuleDataPath() }.Concat(relativePath.Split('/')).ToArray();
             var text = File.ReadAllText(Path.Combine(parts));
     ```
   - Replace the class summary (from `/// <summary>` to `/// </summary>` above `[TestClass]`) with
     ```csharp
     /// <summary>
     /// Pins the heraldry every banner relies on. On v1.4.8 the engine itself banners a formation captained
     /// by a lord who carries a banner item, with no heraldry check (GeneralsAndCaptainsAssignmentLogic.cs:262-264),
     /// and the banner items a lord can be given are those of his own culture or of none
     /// (DefaultBannerItemModel.cs:31). The native banner tableau access-violates when a bearer's banner parses
     /// to zero groups of ten integers (an empty banner data list,
     /// docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md), so every shipped <c>banner_key</c> must be a
     /// non-zero whole multiple of ten dot-separated integers. The engine (Banner.TryGetBannerDataFromCode,
     /// Banner.cs:576-593) reads whole groups of ten and ignores a trailing partial group, so this gate is a
     /// stricter form of its rule, the safe direction.
     /// </summary>
     ```
   - In the assertion message, the two lines
     `+ "data list, the precondition of the native banner-tableau crash, and since #823 every lord-captained "` and
     `+ "formation is bannered by the engine without TAOM's heraldry guard: " + string.Join(" | ", bad));` become
     `+ "data list, the precondition of the native banner-tableau crash on the engine's own captain-banner "` and
     `+ "path: " + string.Join(" | ", bad));`.
   **Verify**: `grep -c '#823\|RepoPath\|LordBanners' TAOM.Tests/Features/BannerBearers/ShippedHeraldryBannerKeyTests.cs`
   prints `0`; the file keeps its four data tests and the five-row `TheRule_AcceptsWholeGroupsOfTenIntegersOnly`.
   Build, then Filtered `--filter "FullyQualifiedName~ShippedHeraldryBannerKeyTests"`.
   **Verify**: 9 results (5 folded), `Failed: 0`.
2. **Docs**: in `docs/features/banner-bearers.md`, in the `## Testing` table after the
   `ShippedBannerBearerConfigTests.cs` row, add the row below, and add 9 to the count in the
   `74 tests:` line above the table (earlier WPs may have raised it; add 9 to what is there):
   `| `ShippedHeraldryBannerKeyTests.cs` (9) | Every shipped `banner_key` in `characters/clans.xml` (145), `taom_spkingdoms.xml` (14), `spclans.xslt` (72) and `spkingdoms.xslt` (8) is a non-zero whole multiple of ten integers, the native banner tableau's precondition on the engine's captain-banner path (`GeneralsAndCaptainsAssignmentLogic.cs:262-264`), plus five rule rows. Counts measured 2026-11-04. |`
3. **Commit** (message file `msg-134-3.txt`; stage the test and the doc):
   ```text
   test(banners): v2.0.28 - gate every shipped heraldry banner key

   A new test fails if any banner_key in clans.xml, taom_spkingdoms.xml,
   spclans.xslt or spkingdoms.xslt is not a whole number of groups of
   ten integers. Bannerlord reads a banner code ten numbers at a time,
   and a code that yields no group gives an empty banner, which the
   native banner tableau crashes on. The engine itself banners a
   formation captained by a lord who carries a banner item, with no
   heraldry check, so one bad key in a clan or kingdom row could crash
   a battle. All 239 shipped keys pass today; this keeps it that way.

   Research: taom-src v1.4.8 Banner.TryGetBannerDataFromCode (:576-593)
   Research: GeneralsAndCaptainsAssignmentLogic.cs:262-264
   Research: DefaultBannerItemModel.cs:31
   Ported-from: 736fa2fef65a96812648df924efca0b5153a84d0
   ```

### Step A4: Part A wrap-up

1. Full Build and Test. **Verify**: `Failed: 0`; total = `BASE_TOTAL + 16` (folded: `+ 8`); record it as `A_TOTAL`.
2. `python tools/validate_moduledata.py` (about 40 s). **Verify**: no ERROR outside the WP101 baseline.
3. `python tools/lint_docs.py`. **Verify**: no finding on a line Part A added.
4. `git log --format=%s "$BASE"..HEAD` shows the three Part A subjects; `git status --porcelain` is empty.

## Part B: every side follows the realm it serves (#957)

Source for both steps: `5e43518df` "fix(alignment): v2.0.35 - every side follows the realm it serves"
(126 files, 78 conflicted paths in the simulation). Treat it as a re-implementation: take the pair
infrastructure, then rewrite the side pairs only in the three features where the 1.4.x bug exists.

### Step B1: Add the one-entity side pair (no caller yet)

1. **RED.**
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git show 5e43518df:TAOM.Tests/Adapters/FactionSidesTests.cs > TAOM.Tests/Adapters/FactionSidesTests.cs && git checkout 5e43518df TAOM.Tests/Features/Execution/AlignmentServiceTests.cs
   ```
   Edit `TAOM.Tests/Adapters/FactionSidesTests.cs` (Edit tool): delete the line `[TestCategory("RequiresGame")]`
   (1.4.x has no such category, D9); delete the whole `OfCharacter_NullCharacter_IsNone` test method
   (this plan does not port `OfCharacter`, which has no 1.4.x caller); in
   `OfHero_ReadsTheClansMapFaction_NeverTheHeroesMapFaction` change the end marker
   `"public static SideIds OfCharacter("` to `"public static SideIds OfPlayer("`; change `(Clan.cs:284)` to
   `(Clan.cs:276, v1.4.8)` and `(Hero.cs:569-591)` to `(Hero.cs:567-590, v1.4.8)`. Build.
   **Verify RED**: the build fails with CS0246/CS0103 errors naming `SideIds`, `FactionSides` or the
   `ResolveSide(SideIds)` overload, and no other error.
2. **GREEN.** Take the two small files verbatim:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git checkout 5e43518df Main/Features/Execution/SideIds.cs Main/Features/Execution/AlignmentServiceExtensions.cs
   ```
   Write `Main/Adapters/FactionSides.cs` with the Write tool (the 1.5.x file without `OfCharacter`,
   cites moved to v1.4.8):
   ```csharp
   using TaleWorlds.CampaignSystem;
   using TAOM.Features.Execution;

   namespace TAOM.Adapters;

   /// <summary>
   /// The one place a <see cref="SideIds"/> pair is built from engine objects. A pair is the id and the
   /// culture of the SAME entity, because <see cref="IAlignmentService.ResolveSide(string, string)"/> falls
   /// back from an unlisted or Neutral-listed id to that culture. A kingdom id zipped with a hero's own
   /// culture sides a Neutral realm's lord by a culture the realm does not have (#957).
   /// </summary>
   internal static class FactionSides
   {
       /// <summary>The faction's id and the faction's own culture; <see cref="SideIds.None"/> for null.</summary>
       public static SideIds Of(IFaction? faction)
       {
           if (faction == null)
               return SideIds.None;
           return new SideIds(faction.StringId ?? string.Empty, faction.Culture?.StringId ?? string.Empty);
       }

       /// <summary>
       /// A hero in a clan is the clan's map faction (the kingdom, or the clan itself) with that faction's
       /// culture; a companion's <c>Hero.Clan</c> is the clan it serves (v1.4.8 Hero.cs:499-503). A clanless
       /// hero (a wanderer) is its own entity: no faction id and its own culture, because
       /// <c>Hero.MapFaction</c> would give it the home town's realm (v1.4.8 Hero.cs:567-590).
       /// </summary>
       public static SideIds OfHero(Hero? hero)
       {
           if (hero == null)
               return SideIds.None;
           var clan = hero.Clan;
           if (clan != null)
               return Of(clan.MapFaction);
           return new SideIds(string.Empty, hero.Culture?.StringId ?? string.Empty);
       }

       /// <summary>
       /// The player's clan map faction. <c>Clan.PlayerClan</c> dereferences <c>Campaign.Current</c>
       /// (v1.4.8 Clan.cs:276), so there is no pair outside a campaign.
       /// </summary>
       public static SideIds OfPlayer()
       {
           if (Campaign.Current == null)
               return SideIds.None;
           return Of(Clan.PlayerClan?.MapFaction);
       }
   }
   ```
   In `Main/Features/Execution/IAlignmentService.cs` replace the whole `ResolveSide` summary (the
   `/// <summary>` block above `FactionSide ResolveSide(string kingdomId, string cultureId);`) with:
   ```csharp
       /// <summary>
       /// Side of a participant identified by faction id first (a kingdom id, or a clan id), falling back to
       /// culture id when the id does not classify. Catches the cases where <see cref="GetKingdomSide"/> alone
       /// reads Neutral and would silently disable alignment logic: a kingdom-less hero (independent or
       /// enlisted player, a minor clan leader serving no kingdom, a victim whose clan was destroyed by the
       /// kill that is being evaluated) and a player-founded kingdom whose id is absent from alignment.json.
       /// CaravanTrade and WarOfTheRingMomentum keep private copies of this rule; each pairs a faction id with
       /// that faction's own culture.
       /// <para>
       /// The fallback fires when the id is unlisted OR listed Neutral, so pass the culture of the SAME
       /// entity the id names (a faction with its own culture, a clanless hero with the hero's own). A
       /// kingdom paired with another entity's culture sides a Neutral or player-founded owner as that
       /// entity. alignment.json lists no clan ids on this line, so a clan that serves no kingdom is sided
       /// by the clan's own culture.
       /// </para>
       /// <para>
       /// Callers hold the pair as a <see cref="SideIds"/> built by <c>TAOM.Adapters.FactionSides</c> (the
       /// only place that zips engine ids) and resolve it with the
       /// <c>ResolveSide(this IAlignmentService, SideIds)</c> extension (#957).
       /// </para>
       /// </summary>
   ```
   Build, then Filtered `--filter "FullyQualifiedName~FactionSidesTests|FullyQualifiedName~AlignmentServiceTests"`.
   **Verify GREEN**: `Failed: 0`, `FactionSidesTests` 7 passed, `AlignmentServiceTests` one more than
   before (35 on `1b00d1acd`, so 36), and `git grep -n 'OfCharacter' -- Main TAOM.Tests` prints nothing.
3. **Commit** (message file `msg-134-4.txt`; stage `Main/Adapters/FactionSides.cs`
   `Main/Features/Execution/SideIds.cs` `Main/Features/Execution/AlignmentServiceExtensions.cs`
   `Main/Features/Execution/IAlignmentService.cs` `TAOM.Tests/Adapters/FactionSidesTests.cs`
   `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`):
   ```text
   refactor(alignment): v2.0.28 - add the one-entity side pair

   Adds SideIds, the id and the culture of one entity, and FactionSides,
   the only place that builds one from engine objects: a hero in a clan
   is the clan's realm (or the clan itself) with that realm's own
   culture, a clanless hero is his own culture, and the player is the
   player clan's realm. IAlignmentService gains a ResolveSide(SideIds)
   extension. Nothing uses them yet, so nothing changes in play; the
   next commit moves the alignment features onto the pair.

   Research: v1.4.8 Hero.Clan (:499-503), Hero.MapFaction (:567-590)
   Research: v1.4.8 Clan.MapFaction (:339-349), Clan.PlayerClan (:276)
   Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839
   ```

### Step B2: Route executions, wanderer allegiance and prisoner recruitment through the pair

1. **RED (behaviour).** Create the IL scan with the 1.4.x allow-list:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git show 5e43518df:TAOM.Tests/Features/Execution/SidePairScanTests.cs > TAOM.Tests/Features/Execution/SidePairScanTests.cs
   ```
   Edit it (Edit tool): replace the body of the `TwoArgumentCallers` dictionary initializer (the nine
   `["TAOM.Features...."] = "...",` lines) with these two lines:
   ```csharp
           ["TAOM.Features.Execution.AlignmentServiceExtensions"] = "the extension that resolves a SideIds pair",
           // CaravanTradeService and MomentumEnrollmentService resolve sides through private helpers over
           // GetKingdomSide/GetCultureSide, each pairing a faction id with that faction's own culture; they never
           // call ResolveSide, so they are not listed.
   ```
   Keep `SideIdsBuilders`, the three tests and every probe class unchanged. Build, then Filtered
   `--filter "FullyQualifiedName~SidePairScanTests"`.
   **Verify RED**: 3 results, `Failed: 1`: `TwoArgumentResolveSide_IsCalledOnlyByTheListedTypes`, whose
   message contains `outside the allow-list: TAOM.Features.Execution.ExecutionRelationService, TAOM.Features.Execution.Hooks.ExecutionActionHook, TAOM.Features.WandererAllegiance.WandererAllegianceService.`
   The other two pass. `Inconclusive` instead of pass/fail means the game assemblies did not load: STOP.
2. **RED (signatures): update the tests.**
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git checkout 5e43518df TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs TAOM.Tests/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleServiceTests.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceServiceTests.cs
   ```
   `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs` (Edit tool):
   - In `Setup`, `Side(PlayerKingdom, null, FactionSide.Free);`, `Side(AllyKingdom, null, FactionSide.Free);`
     and `Side(VictimKingdom, null, FactionSide.Evil);` take `""` instead of `null`; delete the line
     `Side(null, FreeCulture, FactionSide.Free);` (the `""` line above it already covers it).
   - Replace the helper with
     `private static SideIds P(string kingdomId, string cultureId = null) => new SideIds(kingdomId ?? "", cultureId ?? "");`
   - In `ShouldApplyHonorPenalty_VictimKingdomNulledByClanDestruction_ReturnsFalse` change
     `_alignmentService.ResolveSide(null, EvilCulture)` to `_alignmentService.ResolveSide("", EvilCulture)`.
   `TAOM.Tests/Features/Execution/ExecutionContextTests.cs` (Edit tool):
   - Add `using TAOM.Features.Execution;`.
   - Below the four `const string` fields add
     `private static readonly SideIds Victim = new(VictimKingdom, VictimCulture);`,
     `private static readonly SideIds Executor = new(ExecutorKingdom, ExecutorCulture);` and
     `private static readonly SideIds Live = new("live_kingdom", "live_culture");`.
   - Every `ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture)` becomes
     `ExecutionContext.TrySet(Victim, Executor)`; `TrySet("isengard", "isengard", "vlandia", "vlandia")`
     becomes `TrySet(new SideIds("isengard", "isengard"), new SideIds("vlandia", "vlandia"))`;
     `TrySet(null, VictimCulture, ExecutorKingdom, ExecutorCulture)` becomes
     `TrySet(new SideIds(string.Empty, VictimCulture), Executor)`.
   - `ResolveVictim("live_kingdom", "live_culture")` and `ResolveExecutor("live_kingdom", "live_culture")`
     take `Live`; `ResolveVictim(null, null)` becomes `ResolveVictim(SideIds.None)`.
   - Every `.KingdomId` becomes `.FactionId`; `Assert.IsNull(victim.KingdomId);` becomes
     `Assert.AreEqual(string.Empty, victim.FactionId);`.
   - Add the test:
     ```csharp
         [TestMethod]
         public void GetVictimAndGetExecutor_WithNoContext_AreNone()
         {
             Assert.AreSame(SideIds.None, ExecutionContext.GetVictim());
             Assert.AreSame(SideIds.None, ExecutionContext.GetExecutor());
         }
     ```
   Build. **Verify RED**: compile errors only in these test files, about `SideIds` arguments to
   `GetRelationModifier`, `ShouldApplyHonorPenalty`, `ShouldWaiveMoralePenalty`, `Evaluate`, `TrySet`,
   `ResolveVictim`, `ResolveExecutor`, `GetVictim`, `GetExecutor`.
3. **GREEN: Execution.**
   - `git checkout 5e43518df Main/Features/Execution/ExecutionRelationService.cs`
   - `Main/Features/Execution/IExecutionRelationService.cs`: delete the `ExecutionParticipant` struct and
     its summary (lines 3-18 on `1b00d1acd`); change the three parameters of `GetRelationModifier` to
     `SideIds executor`, `SideIds victim`, `SideIds evaluator`; in the remarks replace
     `The model converts sealed TaleWorlds heroes to <see cref="ExecutionParticipant"/>` and the rest of that
     sentence with `The model converts sealed TaleWorlds heroes to <see cref="SideIds"/> pairs (<c>TAOM.Adapters.FactionSides</c>, the id and the culture of one entity) at its boundary, then delegates the entire decision (side resolution, kinslaying multiplier, cross-alignment zeroing, notification suppression on zero-modified) to this service.`,
     and in the same remark replace its two long dashes (after `Phase 9b #147` and before `no inline`)
     with a colon and a comma, because the commit must add no long dash; in the `<returns>` text change
     `resolved by kingdom id` to `resolved by faction id`.
   - `Main/Features/Execution/Hooks/IOnExecutionAction.cs`: `bool ShouldApplyHonorPenalty(SideIds victim, SideIds executor);`
   - `Main/Features/Execution/Hooks/ExecutionActionHook.cs`: the method becomes
     ```csharp
         public bool ShouldApplyHonorPenalty(SideIds victim, SideIds executor)
         {
             var victimSide = _alignmentService.ResolveSide(victim);
             var executorSide = _alignmentService.ResolveSide(executor);

             return !_alignmentService.AreEnemyAlignments(executorSide, victimSide);
         }
     ```
     and in its summary `Sides resolve by kingdom id with a culture-id` becomes `Sides resolve by faction id with a culture-id`
     and `(independent, mercenary, or enlisted player)` becomes `(an independent or enlisted player)`.
   - `Main/Features/Execution/Hooks/ExecutionContext.cs`: replace the four string `ThreadLocal`s and
     every member that reads them, keeping `Active`, `HasContext` and each member's existing XML doc
     (reworded from "kingdom id" to "pair" where it says so), so the class body reads:
     ```csharp
         private static readonly ThreadLocal<bool> Active = new ThreadLocal<bool>();
         private static readonly ThreadLocal<SideIds?> Victim = new ThreadLocal<SideIds?>();
         private static readonly ThreadLocal<SideIds?> Executor = new ThreadLocal<SideIds?>();

         public static bool TrySet(SideIds victim, SideIds executor)
         {
             if (Active.Value)
                 return false;

             Victim.Value = victim;
             Executor.Value = executor;
             Active.Value = true;
             return true;
         }

         public static void ClearIfOwned(bool owned)
         {
             if (!owned)
                 return;

             Victim.Value = null;
             Executor.Value = null;
             Active.Value = false;
         }

         public static bool HasContext => Active.Value;

         /// <summary>The victim's pair as of the start of the kill; <see cref="SideIds.None"/> when none was taken.</summary>
         public static SideIds GetVictim() => Victim.Value ?? SideIds.None;

         /// <summary>The executor's pair as of the start of the kill; <see cref="SideIds.None"/> when none was taken.</summary>
         public static SideIds GetExecutor() => Executor.Value ?? SideIds.None;

         public static SideIds ResolveVictim(SideIds live) => Active.Value ? GetVictim() : live;

         public static SideIds ResolveExecutor(SideIds live) => Active.Value ? GetExecutor() : live;
     ```
     Add one sentence at the end of the class remarks: `Each pair is built by <c>TAOM.Adapters.FactionSides.OfHero</c>: the hero's clan map faction with that faction's own culture, or a clanless hero's own culture (#957). This class stores and returns pairs; it never builds one (<c>SidePairScanTests</c>).`
   - `Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs`: add `using TAOM.Adapters;`;
     the `TrySet` call becomes `__state = ExecutionContext.TrySet(FactionSides.OfHero(victim), FactionSides.OfHero(killer));`
     and append to the comment above it: `// Each side is the hero's clan map faction with that faction's own culture, never the kingdom id zipped with the hero's own culture (#957).`
   - `Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs`: replace the two
     `new ExecutionParticipant(...)` locals and the return with
     ```csharp
             // Skip the vanilla -1000 Honor XP when this was a cross-alignment kill.
             return _hook.ShouldApplyHonorPenalty(ExecutionContext.GetVictim(), ExecutionContext.GetExecutor());
     ```
   - `Main/Adapters/IPlayerContextAdapter.cs`: replace `GetPlayerCultureId()` and its summary with
     ```csharp
         /// <summary>
         /// The player clan's map faction id with that faction's own culture (<c>FactionSides.OfPlayer</c>):
         /// the kingdom served, or the player clan itself when it serves none. <c>SideIds.None</c> outside a
         /// campaign. Places the player on a Free/Evil/Neutral side (#957).
         /// </summary>
         SideIds GetPlayerSideIds();
     ```
     and add `using TAOM.Features.Execution;` at the top. Keep every other member (WP128 may have added
     `GetPlayerClanId`).
   - `Main/Adapters/PlayerContextAdapter.cs`: replace the `GetPlayerCultureId` line with
     `public SideIds GetPlayerSideIds() => FactionSides.OfPlayer();` and add `using TAOM.Features.Execution;`.
   - `Main/Features/Execution/Models/TaomExecutionRelationModel.cs`: the method body becomes
     ```csharp
             // Boundary: one id-and-culture pair per entity (FactionSides: the clan's map faction with that
             // faction's own culture) + the vanilla baseline. Executor and victim prefer the snapshot taken
             // before the kill mutated them; the evaluator is untouched by the kill, so it is always read live.
             int baseDelta = base.GetRelationChangeForExecutingHero(victim, hero, out bool baseShowNotification);
             var executor = ExecutionContext.ResolveExecutor(_playerContext.GetPlayerSideIds());
             var victimSide = ExecutionContext.ResolveVictim(FactionSides.OfHero(victim));
             var evaluator = FactionSides.OfHero(hero);

             // Delegate: all decisions live in IExecutionRelationService.
             var result = _service.GetRelationModifier(
                 executor,
                 victimSide,
                 evaluator,
                 baseDelta,
                 baseShowNotification);

             showQuickNotification = result.ShowNotification;
             return result.RelationDelta;
     ```
     (`using TAOM.Adapters;` is already there; the constructor and its `SubModule.cs:995` call stay.)
4. **GREEN: Wanderer Allegiance** (the interface stays; 1.5.x deleted it, but WP105's
   `WandererAllegianceIoC` still registers `IWandererAllegianceService, WandererAllegianceService` and
   `WandererAllegianceWiringTests` resolves the dialog behavior through it; before WP105 that test
   asserts the same registration string in `WandererAllegianceIoC.cs`).
   - `git checkout 5e43518df Main/Features/WandererAllegiance/WandererAllegianceService.cs`, then change
     its class line `public class WandererAllegianceService` to
     `public class WandererAllegianceService : IWandererAllegianceService`.
   - `Main/Features/WandererAllegiance/IWandererAllegianceService.cs` becomes:
     ```csharp
     using TAOM.Features.Execution;

     namespace TAOM.Features.WandererAllegiance;

     /// <summary>
     /// The Free/Evil hire rule for wanderers. Keys on CULTURE StringIds (the keys of
     /// <c>execution/alignment.json</c>) for the wanderer, because a tavern wanderer has no clan and no
     /// kingdom, and on the player clan's map faction with that faction's own culture for the player,
     /// because the side a player fights for is the realm they serve: a Gondor-born mercenary of Mordor is
     /// Evil-aligned for this purpose, and a vassal of a Neutral realm is Neutral (#957).
     /// </summary>
     public interface IWandererAllegianceService
     {
         /// <summary>
         /// Decides whether the wanderer refuses. Any null or unknown culture resolves to Neutral, and
         /// Neutral on either side allows the hire, so the rule fails open to vanilla.
         /// </summary>
         /// <param name="wandererHeroId">The wanderer's <c>Hero.StringId</c>; consulted only by the named-companions-only scope.</param>
         /// <param name="wandererCultureId">The wanderer's culture StringId.</param>
         /// <param name="player">The player clan's map faction id with that faction's culture (<c>FactionSides.OfPlayer()</c>).</param>
         WandererHireVerdict Evaluate(string? wandererHeroId, string? wandererCultureId, SideIds player);
     }
     ```
   - `Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs`: add `using TAOM.Adapters;`;
     in `Verdict()` delete `var playerClan = Clan.PlayerClan;` and make the call
     ```csharp
                 return _service.Evaluate(
                     wanderer?.StringId,
                     wanderer?.Culture?.StringId,
                     FactionSides.OfPlayer());
     ```
     and replace the comment above `Verdict()` with
     ```csharp
         // Boundary conversion only: the wanderer's ids and the player's faction pair into the pure service.
         // Read live on every call, never cached, because Player Switcher changes the player clan mid-session.
         // Hero.Culture is a plain field, while Hero.OneToOneConversationHero and Clan.PlayerClan (inside
         // FactionSides.OfPlayer) ARE computed statics, which is what the try/catch is for. A throw defers
         // to vanilla hiring, safe here because nothing has been mutated.
     ```
     The field and constructor keep `IWandererAllegianceService`.
5. **GREEN: Prisoner Recruitment, marriage text, MCM hint.**
   - `git checkout 5e43518df Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs Main/Features/MarriageAlignment/MarriageAlignmentService.cs`
   - `Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs`: add
     `using TAOM.Adapters;`; change `leader?.Clan?.Kingdom?.StringId,` to `FactionSides.OfHero(leader),`.
     Keep the `int` return type and `? 0` (v1.4.8 signature). Add to the end of the summary:
     `The recruiter's side pair is the leader's clan map faction with that faction's culture (FactionSides.OfHero, #957); the leader's own culture feeds only the same-culture rule.`
   - `Main/Features/TaomSettings.cs`: in the Marriage Alignment `HintText` replace
     `Neutral cultures (Umbar, Shaghana, Abanissa, Dunland) marry anyone.` with
     `Neutral cultures (Khand, Umbar, Shaghana, Abanissa) marry anyone; Dunland is Evil.`
   Build, then Filtered
   `--filter "FullyQualifiedName~TAOM.Tests.Features.Execution|FullyQualifiedName~TAOM.Tests.Features.WandererAllegiance|FullyQualifiedName~TAOM.Tests.Features.PrisonerRecruitment|FullyQualifiedName~FactionSidesTests"`,
   then the same command with `--filter "TestCategory=BindingVerification"`.
   **Verify GREEN**: build exit 0; `Failed: 0` in both runs; `SidePairScanTests` 3 passed (not inconclusive);
   `git grep -n 'ExecutionParticipant\|GetPlayerCultureId' -- Main TAOM.Tests` prints nothing;
   `git grep -n 'new SideIds(' -- Main` lists only `Main/Adapters/FactionSides.cs` (`SideIds.cs` builds its
   `None` with a target-typed `new(`, so it does not match).
6. **Docs** (Edit tool; 1.4.x text otherwise unchanged):
   - `docs/features/marriage-alignment.md`: `git checkout 5e43518df docs/features/marriage-alignment.md`
     (it changes the two Neutral lists and adds a changelog bullet), then change that bullet's date
     `**2026-10-20**` to today and append ` Ported to 1.4.x from 1.5.x `5e43518df`.` to it.
   - `docs/features/prisoner-recruitment.md`: apply by hand the hunks `git show 5e43518df -- docs/features/prisoner-recruitment.md`
     prints (the "Recruiter side resolution" paragraph, the two `32 service tests` counts that become
     `31`, the Testing bullet and the in-game smoke sentence); they apply to the 1.4.x text as printed.
   - `docs/features/wanderer-allegiance.md`: apply by hand the hunks of
     `git show 5e43518df -- docs/features/wanderer-allegiance.md` for the overview Neutral list, "The
     rule" signature and table row, the "Why culture ..." and "Neutral semantics" paragraphs, "Where the
     ids come from" and the Tests bullet. Do NOT apply the Key Files hunk: keep both rows, and change the
     interface row's text to `Its interface (two wanderer ids and the player's SideIds pair in, a verdict out)`.
   - `docs/features/execution.md`: `kingdom AND culture; Finalizer clears)` becomes
     `one SideIds pair each; Finalizer clears)`; the sentence starting
     `` `ExecutionContext` holds thread-local kingdom **and** culture ids for both victim and executor`` becomes
     `` `ExecutionContext` holds a thread-local `SideIds` pair for the victim and one for the executor (each from `FactionSides.OfHero`: the clan's map faction with that faction's own culture), plus an explicit active flag (a hero legitimately has no kingdom, so the flag cannot be inferred from an empty id).``
     (the rest of that paragraph stays); `passing an `ExecutionParticipant` (kingdom id plus culture id) for each.`
     becomes `passing a `SideIds` pair (one entity's faction id and that entity's culture, from `FactionSides`) for each; the executor's live pair is `IPlayerContextAdapter.GetPlayerSideIds()`.`;
     after `tries the kingdom first and falls back to the culture.` add ` Every caller passes one entity's pair (#957).`;
     the Key Files rows `` `ExecutionParticipant`, `ExecutionRelationResult`, relation contract`` becomes
     `` `ExecutionRelationResult`, relation contract (participants are `SideIds`)`` and
     `` `ThreadLocal<string>` victim/executor kingdom-ID pair`` becomes `` `ThreadLocal<SideIds?>` victim/executor pairs``;
     replace the paragraph that starts `No `Adapter` interfaces` with
     `The feature operates on `SideIds` pairs that `TAOM.Adapters.FactionSides` builds in the patches and the model override, the only places that touch `Hero`, `Clan` or `Kingdom` (entry points per ADR-002 / ADR-007); the model reads the player's pair through `IPlayerContextAdapter.GetPlayerSideIds()`.`;
     `AlignmentServiceTests.cs`: **35 tests** becomes **36 tests**; `ExecutionRelationServiceTests.cs`: **18 tests** becomes **17 tests**; add the Tests bullets
     `` - `TAOM.Tests/Adapters/FactionSidesTests.cs`: **7 tests**, the pair builders' null guards and source pins (a hero reads its clan's map faction, never `Hero.MapFaction`; the player pair needs a campaign).`` and
     `` - `TAOM.Tests/Features/Execution/SidePairScanTests.cs`: **3 tests**, an IL scan of TAOM.dll: only the `ResolveSide(SideIds)` extension calls the two-argument `ResolveSide`, and only `FactionSides` builds a `SideIds`.``;
     add a Changelog bullet `- <today's date>: #957 ported from 1.5.x `5e43518df`: every participant is one entity's `SideIds` pair; a Neutral realm's lord is no longer sided by his own culture.`
   - `docs/features/alignment-aware-execution.md`: `2. Build three `ExecutionParticipant` values (kingdom id + culture id).`
     becomes `2. Build three `SideIds` pairs (`TAOM.Adapters.FactionSides`: the hero's clan map faction id with that faction's own culture, or a clanless hero's own culture; #957).`;
     at the end of the paragraph that starts `**`ResolveSide(string kingdomId, string cultureId)`** takes the kingdom side first`
     add ` Since #957 every caller passes the id and the culture of ONE entity as a `SideIds` pair, built only by `FactionSides` and resolved with the `ResolveSide(this IAlignmentService, SideIds)` extension; a kingdom id zipped with a hero's own culture sided a Neutral realm's lord by his birth. `SidePairScanTests` fails on a two-argument `ResolveSide` call or a `SideIds` built anywhere else. PrisonerRecruitment's private copy of the fallback went with #957.`;
     `each participant: ResolveSide(kingdomId, cultureId)` becomes `each participant: ResolveSide(SideIds pair)`;
     in Key Files `` `ExecutionParticipant`, `ExecutionRelationResult`, service contract`` becomes
     `` `ExecutionRelationResult`, service contract (participants are `SideIds`)``,
     `Thread-local victim/killer snapshot (kingdom + culture)` becomes `Thread-local victim/killer snapshot (one `SideIds` pair each)`,
     and add rows for `Main/Adapters/FactionSides.cs` (builds every pair from engine objects),
     `Main/Features/Execution/SideIds.cs` (the pair record) and `Main/Features/Execution/AlignmentServiceExtensions.cs`
     (`ResolveSide(SideIds)`); add the same Changelog bullet as in `execution.md`.
7. **Commit** (message file `msg-134-5.txt`). Stage exactly these paths; the trailing
   `git status --porcelain` must show only staged lines (`M ` or `A ` in the first two columns) and no
   `??`, ` M` or other line, which would mean a file was missed or an out-of-scope file changed:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && git add \
     TAOM.Tests/Features/Execution/SidePairScanTests.cs TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs \
     TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs TAOM.Tests/Features/Execution/ExecutionContextTests.cs \
     TAOM.Tests/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleServiceTests.cs \
     TAOM.Tests/Features/WandererAllegiance/WandererAllegianceServiceTests.cs \
     Main/Features/Execution/ExecutionRelationService.cs Main/Features/Execution/IExecutionRelationService.cs \
     Main/Features/Execution/Hooks/IOnExecutionAction.cs Main/Features/Execution/Hooks/ExecutionActionHook.cs \
     Main/Features/Execution/Hooks/ExecutionContext.cs Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs \
     Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs Main/Features/Execution/Models/TaomExecutionRelationModel.cs \
     Main/Adapters/IPlayerContextAdapter.cs Main/Adapters/PlayerContextAdapter.cs \
     Main/Features/WandererAllegiance/WandererAllegianceService.cs Main/Features/WandererAllegiance/IWandererAllegianceService.cs \
     Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs \
     Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs Main/Features/PrisonerRecruitment/PrisonerRecruitmentMoraleService.cs \
     Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs \
     Main/Features/MarriageAlignment/MarriageAlignmentService.cs Main/Features/TaomSettings.cs \
     docs/features/marriage-alignment.md docs/features/prisoner-recruitment.md docs/features/wanderer-allegiance.md \
     docs/features/execution.md docs/features/alignment-aware-execution.md && git status --porcelain
   ```
   Message:
   ```text
   fix(alignment): v2.0.28 - every side follows the realm it serves

   Executions, Wanderer Allegiance and Prisoner Recruitment judged a
   hero by his realm's id but his own culture. Whenever the realm counts
   as Neutral (Khand, Umbar, Shaghana, Abanissa), the hero's own culture
   decided instead, so a Free-born lord or player serving one of them
   was treated as Free. All three now read the realm and the realm's
   own culture:

   - Executions: the relation change and the Honor penalty. Executing a
     lord of a Neutral realm is no longer kinslaying for a Free player
     just because the lord was born Free.
   - Wanderer Allegiance: a player sworn to a Neutral realm is Neutral,
     so no wanderer refuses him for his birth.
   - Prisoner Recruitment: a lord of a Neutral realm waives the morale
     cost only for prisoners of his own culture, never for his birth
     side. The same-culture rule still uses his own culture.

   A clanless hero is still judged by his own culture, and a clan that
   serves no kingdom by the clan's culture. The Marriage Alignment hint
   no longer calls Dunland Neutral: Dunland is Evil and Khand Neutral.

   Research: taom-src v1.4.8 KillCharacterAction.ApplyInternal (:31)
   Research: DefaultExecutionRelationModel (:28)
   Research: DefaultPrisonerRecruitmentCalculationModel (:53, int)
   Save-compat: no saved data changes; ExecutionContext is thread-local
   Not-tested: the three seams in a live campaign
   Ported-from: 5e43518df7980ebaaec4d22a7370d2df726dd839
   ```

### Step B3: Part B wrap-up

1. Full Build and Test. **Verify**: `Failed: 0`; total = `A_TOTAL + 11` (data rows do not change this).
2. `python tools/lint_docs.py`. **Verify**: no finding on a line Part B added.
3. Machine checks:
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && \
   git log --format=%s "$BASE"..HEAD; \
   git diff --name-only "$BASE"..HEAD; \
   git log --format=%B "$BASE"..HEAD | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'; \
   git diff "$BASE"..HEAD -U0 -- docs Main TAOM.Tests | grep '^+' | grep -v '^+++' | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'; \
   git diff --quiet "$BASE" HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj CHANGELOG.md Main/Features/CaravanTrade Main/Features/WarOfTheRingMomentum Main/_Module/ModuleData/execution/alignment.json Main/_Module/ModuleData/lords.xslt && echo UNTOUCHED
   ```
   Replace `$BASE` with the Step 0 sha. **Verify**: the five subjects of Steps A1 to B2, each with
   `v2.0.28 - ` and at most 72 characters; only in-scope paths; `0`; `0`; `UNTOUCHED`.
4. Record the total as `B_TOTAL`, then go on to Part C. The hand-off report is in Step C6.

## Part C: wanderer backstories and the data defects routed here

Steps C1 and C2 give the 40 cloned wanderers their backstories (`9dd2f594f`, partial). Steps C3 to C5 close the
three data items plans 123, 126 and 121 routed to this WP under D13. Source for C1 and C2: `9dd2f594f` "fix(wanderers): v2.0.34 - give 53
wanderers their own backstories" (Refs #757, #257). Take the rows of the 40 wanderers that exist on 1.4.x
(goblin, mistymountainorcs, bluecraig, lindon) and the coverage test; leave the 13 Arthedain wanderers
(content absent on 1.4.x), the `LanguageTextIntegrityTests` and `LocalizationKeyConsistencyTests` hunks (the
rule they edit, and the second file, arrive with WP135), `.claude/rules/moduledata-validation.md`, and docs.
The Armory check of Step 0.2 gates this part (D20).

### Step C1: RED

```bash
cd /c/Users/mikew/source/repos/taom-port/134 && git show 9dd2f594f:TAOM.Tests/Core/WandererBackstoryCoverageTests.cs > TAOM.Tests/Core/WandererBackstoryCoverageTests.cs
```
Edit its summary (Edit tool): `the goblin, mistymountainorcs, bluecraig, lindon and arthedain wanderers were`
becomes `the goblin, mistymountainorcs, bluecraig and lindon wanderers were`; `53 characters in all` becomes
`40 characters in all`; `(v1.5.4 conversation_wanderer_preintroduction_on_condition)` becomes
`(v1.4.8 conversation_wanderer_preintroduction_on_condition, LordConversationsCampaignBehavior.cs:1719-1736)`.
Build, then Filtered `--filter "FullyQualifiedName~WandererBackstoryCoverageTests"`.
**Verify RED**: 4 results, `Failed: 1` (`WandererStrings_EveryWanderer_HasEveryRequiredBackstoryRow`,
"280 backstory rows missing").

### Step C2: GREEN

Append the four culture blocks to the English file and the matching rows to each language file, keeping
each file's line ending; the script takes the commit's added lines and stops at the first Arthedain line:
```bash
cd /c/Users/mikew/source/repos/taom-port/134 && python - <<'EOF'
import pathlib, subprocess
SHA = "9dd2f594f"
files = ["Main/_Module/ModuleData/taom_wanderer_strings.xml"] + sorted(
    p.as_posix() for p in pathlib.Path("Main/_Module/ModuleData/Languages").glob("*/std_taom_wanderer_strings_*.xml"))
assert len(files) == 13, files
for f in files:
    diff = subprocess.run(["git", "diff", "--no-color", SHA + "^", SHA, "--", f],
                          capture_output=True, check=True).stdout.decode("utf-8")
    added = [l[1:] for l in diff.splitlines() if l.startswith("+") and not l.startswith("+++")]
    keep = []
    for l in added:
        if "ARTHEDAIN" in l or "spc_wanderer_arthedain_" in l:
            break
        keep.append(l)
    while keep and keep[-1].strip() == "":
        keep.pop()
    rows = [l for l in keep if "<string " in l]
    assert len(rows) == 320, (f, len(rows))
    p = pathlib.Path(f)
    raw = p.read_bytes()
    nl = "\r\n" if b"\r\n" in raw else "\n"
    text = raw.decode("utf-8")
    idx = text.rfind("</strings>")
    assert idx > 0, f
    start = text.rfind("\n", 0, idx) + 1
    block = nl.join(keep) + nl
    if f.endswith("/taom_wanderer_strings.xml"):
        block += nl
    p.write_bytes((text[:start] + block + text[start:]).encode("utf-8"))
    print(f, "inserted", len(rows))
EOF
```
**Verify**: 13 lines `inserted 320`; `grep -c 'spc_wanderer_arthedain' Main/_Module/ModuleData/taom_wanderer_strings.xml`
prints `0`; Build, then the Filtered run of C1 shows `Failed: 0` (4 passed), and Filtered
`--filter "FullyQualifiedName~Localization"` shows `Failed: 0`; `python tools/validate_moduledata.py` shows no
new ERROR.

Commit (message file `msg-134-6.txt`; stage the test and the 13 data files):
```text
fix(wanderers): v2.0.28 - give 40 wanderers their own backstories

Wanderers of the Goblin-town, Misty Mountain Orc, Blue Craig and
Lindon cultures showed "ERROR: Text with id backstory_c doesn't
exist!" instead of their introduction: their templates were cloned
from other cultures without the dialogue rows the conversation reads.
All 40 now tell their own stories. Other languages show the English
text until the translation run.

A new test fails on any wanderer missing one of the seven rows the
conversation reads, so a new culture cannot ship this gap again.

Research: taom-src v1.4.8 LordConversationsCampaignBehavior.cs:1719-1766
Not-tested: the conversation text in game
Ported-from: 9dd2f594fd887f792a5cd336781b6832ff6ff753
```
**Verify**: full Test `Failed: 0`, total = `B_TOTAL + 4`.

### Step C3: Generated lords stop drawing Calradian kits (`bb68eeca3` item 3, `db805427b`)

D13 verdict: ported. The defect is verified on v1.4.8 and the merged data (Engine facts, Data facts), and the fix
needs no OUT content: the Dale items are already in the Armory and in `troops_dale.xml`. Child templates keep
their flags, because an empty pool hands the hero null equipment.

RED. Take the test, then edit it (Edit tool):
```bash
cd /c/Users/mikew/source/repos/taom-port/134 && git show bb68eeca3:TAOM.Tests/Core/LordTemplateRosterTests.cs > TAOM.Tests/Core/LordTemplateRosterTests.cs
```
`(v1.5.4) pools every loaded` becomes `(v1.4.8, DefaultEquipmentSelectionModel.cs:57-79) pools every loaded`. Delete the
whole method `TaomLords_NoNamedRosterSharesALordId` with its `[TestMethod]` line: it guards `LordKitDonorAdapter`, which
is 1.5.x-only. Build, then Filtered `--filter "FullyQualifiedName~LordTemplateRosterTests"`.
**Verify RED**: 4 results, `Failed: 4` (the stylesheet is missing; `SubModule.xml` has no such node).

GREEN. Take the stylesheet's end state and re-cite it (Edit tool: `(v1.5.4) pools every EquipmentRoster` becomes
`(v1.4.8) pools every EquipmentRoster`):
```bash
cd /c/Users/mikew/source/repos/taom-port/134 && git show db805427b:Main/_Module/ModuleData/lord_template_rosters.xslt > Main/_Module/ModuleData/lord_template_rosters.xslt
```
In `Main/_Module/SubModule.xml` (Edit tool), directly after the `</XmlNode>` that closes the LAST `EquipmentRosters` node
(`equipmentsets/taom_lord_template_equipment` at planning; an earlier WP may have added one after it), insert:
```xml
    <!-- XSLT that takes the lord and ruler template flags off vanilla's rosters for the six renamed
         cultures, so generated Rohan, Dunland, Harad, Easterling, Dale and Khand lords draw the
         TAOM templates above instead of Calradic gear. It rewrites only the roster nodes listed
         before it, so keep this the LAST EquipmentRosters node (LordTemplateRosterTests). -->
    <XmlNode>
      <XmlName id="EquipmentRosters" path="lord_template_rosters"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value ="CustomGame"/>
        <GameType value ="EditorGame"/>
      </IncludedGameTypes>
    </XmlNode>
```
Give Dale's rosters Dale's mounted-noble kit, byte for byte (the same 70 lines as `bb68eeca3`'s hunk of this file):
```bash
cd /c/Users/mikew/source/repos/taom-port/134 && python - <<'EOF'
import pathlib
p = pathlib.Path("Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml")
data = p.read_bytes()
start = data.index(b"<!-- ==================== STURGIA")
end = data.index(b"<!-- ====", start + 10)
section = data[start:end]
swaps = {
    b"sturgia_noble_sword_1_t5": b"sturgia_noble_sword_3_t5",
    b"chivalric_kite_shield": b"wm_dale_shield_a03",
    b"sturgian_helmet_closed": b"sk_dale_helmet_chivlary_a04",
    b"sturgia_cavalry_armor": b"sk_dale_chest_chivalry_a04",
    b"scarf": b"sk_dale_shoulder_chivlary_a04",
    b"plated_strip_gauntlets": b"sk_dale_gauntlet_chivlary_a04",
    b"plated_strip_boots": b"sk_dale_boots_chivlary_a04",
}
for old, new in swaps.items():
    needle = b'id="Item.' + old + b'"'
    assert section.count(needle) == 10, old
    section = section.replace(needle, b'id="Item.' + new + b'"')
assert section.count(b"<EquipmentRoster") == 10
p.write_bytes(data[:start] + section + data[end:])
print("swapped 70")
EOF
```
Docs (`docs/modding/equipment-rosters.md`, Edit tool): after the flag table's `IsTeenagerEquipmentTemplate` row and its
following blank line, add the paragraph
`` `lord_template_rosters.xslt` takes `IsLordTemplate` and `IsKingdomRulerTemplate` off vanilla's adult, teen and ruler rosters of the six renamed cultures (`vlandia`, `empire`, `aserai`, `khuzait`, `sturgia`, `battania`), so `taom_lord_template_equipment.xml` is their only such pool; child templates keep their flags. It rewrites only rosters merged before it, so its `SubModule.xml` node must stay the last `EquipmentRosters` node. `LordTemplateRosterTests` pins both, and fails if a pool the engine requests goes empty. ``
and a blank line.

**Verify**: prints `swapped 70`; `git diff --stat -- Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml`
shows `70 insertions(+), 70 deletions(-)`; Build, then Filtered
`--filter "FullyQualifiedName~LordTemplateRosterTests|FullyQualifiedName~ConfigIdValidationTests"` shows `Failed: 0` with
4 `LordTemplateRosterTests` passed; `python tools/validate_moduledata.py` shows no new ERROR. Any new ERROR naming a Dale
item is a STOP: the public Armory differs from what this step read.

Commit (message file `msg-134-7.txt`; stage `Main/_Module/ModuleData/lord_template_rosters.xslt` `Main/_Module/SubModule.xml`
`Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` `TAOM.Tests/Core/LordTemplateRosterTests.cs`
`docs/modding/equipment-rosters.md`):
```text
fix(lords): v2.0.28 - generated lords stop drawing Calradian kits

Lords the game creates during a campaign now dress from TAOM's lord
templates. A Dunland lord who came of age could pick among 26 vanilla
Empire battle kits and one TAOM kit; Rohan, Harad, Easterling, Dale
and Khand lords were alike. A new stylesheet takes the lord and ruler
template flags off vanilla's rosters for those six cultures. Noble
teens of those cultures now wear the TAOM teen kit too; children keep
their usual clothes.

Dale's template now uses Dale's mounted-noble armour instead of
vanilla Sturgian gear.

A new test fails if a pool the game asks for goes empty, or if another
roster file is registered after the stylesheet.

Research: taom-src v1.4.8 DefaultEquipmentSelectionModel (:57-89)
Research: taom-src v1.4.8 MBObjectManager.CreateMergedXmlFile
Save-compat: lords already generated keep the gear they have
Not-tested: a generated lord's kit in a live campaign
Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93
Ported-from: db805427b89cc354abb7206f926f91eab4a12ad8
```
**Verify**: `git log -1 --format=%s` is the subject above; `git status --porcelain` is empty.

### Step C4: Three lesser Nazgul get one definition and one kit (`9804f67b9`, the `lord_1_48_*` part)

D13 verdict: fixed. The union is verified on v1.4.8 (Engine facts). Plan 126's note says the fix needs a race and age
decision; it does not, because this step moves today's effective race (`uruk`) and age (20) into the templates, so
neither changes. The kit follows the rule the 1.5.x commit records (a vanilla lord lives in the XSLT), so the
templates' Nazgul kits stay and the rows' generic Mordor kits go. Race `nazghul` is 1.5.x content and stays out. The
rows' hair, beard and tattoo tags go with them, as on 1.5.x; the face key does not change.

RED. Create `TAOM.Tests/Features/NazgulFamily/LesserNazgulDefinitionTests.cs` (Write tool):
```csharp
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.NazgulFamily;

/// <summary>
/// lord_1_48_1/2/3 are vanilla lord ids, so lords.xslt alone defines them. A characters/lords.xml row
/// is a second definition: on v1.4.8 it wins each attribute it states and its equipment sets union with
/// the template's (MBObjectManager.MergeElements), and Hero.SetInitialValuesFromCharacter picks a kit at
/// random, so each campaign dressed these three in a Nazgul or a generic Mordor kit. The race and ages
/// came only from the rows, so the templates must carry them.
/// </summary>
[TestClass]
public class LesserNazgulDefinitionTests
{
    private static readonly string[] TheThree = { "lord_1_48_1", "lord_1_48_2", "lord_1_48_3" };

    [TestMethod]
    public void LordsXml_DefinesNoneOfTheThree()
    {
        var lords = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "lords.xml"));

        var duplicates = lords.Descendants("NPCCharacter")
            .Select(e => (string?)e.Attribute("id"))
            .Where(id => TheThree.Contains(id))
            .ToArray();

        Assert.AreEqual(0, duplicates.Length,
            "characters/lords.xml defines a lord lords.xslt already defines: " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void LordsXslt_TheThree_CarryTheirRaceAgeAndOneNazgulKit()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt"));
        var stub = new XDocument(new XElement("NPCCharacters",
            TheThree.Select(id => new XElement("NPCCharacter", new XAttribute("id", id)))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(stub.CreateReader(), null, writer);

        foreach (var id in TheThree)
        {
            var lord = output.Descendants("NPCCharacter").Single(e => (string?)e.Attribute("id") == id);
            Assert.AreEqual("uruk", (string?)lord.Attribute("race"), $"{id} race");
            Assert.AreEqual("20", (string?)lord.Attribute("age"), $"{id} age");
            Assert.AreEqual("22.19", (string?)lord.Element("face")?.Element("BodyProperties")?.Attribute("age"), $"{id} face age");

            var kit = lord.Element("Equipments")!.Elements("EquipmentSet").Select(s => (string?)s.Attribute("id")).ToArray();
            Assert.AreEqual(2, kit.Length, $"{id}: one battle and one civilian set");
            Assert.IsTrue(kit.All(k => k != null && k.StartsWith("nazgul_")), $"{id} kit: {string.Join(", ", kit)}");
        }
    }
}
```
Build, then Filtered `--filter "FullyQualifiedName~LesserNazgulDefinitionTests"`. **Verify RED**: 2 results, `Failed: 2`.

GREEN:
```bash
cd /c/Users/mikew/source/repos/taom-port/134 && python - <<'EOF'
import pathlib, re
md = pathlib.Path("Main/_Module/ModuleData")
x = md / "lords.xslt"
data = x.read_bytes()
for n, age in ((b"1", b"31"), (b"2", b"9"), (b"3", b"11")):
    start = data.index(b"<xsl:template match=\"NPCCharacter[@id='lord_1_48_" + n + b"']\">")
    end = data.index(b"</xsl:template>", start)
    t = data[start:end]
    t, a = re.subn(rb'(<xsl:attribute name="age">)' + age + rb'(</xsl:attribute>)', rb'\g<1>20\g<2>', t)
    t, b = re.subn(rb'([ \t]*)(<xsl:attribute name="culture">Culture\.mordor</xsl:attribute>)(\r?\n)',
                   rb'\1\2\3\1<xsl:attribute name="race">uruk</xsl:attribute>\3', t)
    t, c = re.subn(rb'<BodyProperties version="4" weight="0\.0116"',
                   rb'<BodyProperties version="4" age="22.19" weight="0.0116"', t)
    assert (a, b, c) == (1, 1, 1), (n, a, b, c)
    data = data[:start] + t + data[end:]
x.write_bytes(data)
l = md / "characters" / "lords.xml"
new, n = re.subn(rb'[ \t]*<NPCCharacter id="lord_1_48_[123]"[^>]*>.*?</NPCCharacter>\r?\n', b"", l.read_bytes(), flags=re.S)
print("removed", n)
assert n == 3
l.write_bytes(new)
EOF
```
In `Main/_Module/ModuleData/characters/custom_battle_lords.xml` (Edit tool) replace the comment line
`  <!-- lord_1_48_1/2/3 (the lesser Nazgul) are defined in characters/lords.xml on this branch: no stub. -->` with
```xml
  <NPCCharacter id="lord_1_48_1" culture="Culture.mordor" />
  <NPCCharacter id="lord_1_48_2" culture="Culture.mordor" />
  <NPCCharacter id="lord_1_48_3" culture="Culture.mordor" />
```
Without these, Custom Battle loses the three: SandBox's lords load for Campaign only, and the rows were their only source there.

Docs (Edit tool):
- `docs/features/nazgul-family.md`: directly under `## Tests` add the paragraph
  `` `LesserNazgulDefinitionTests` (2) pins that `lords.xslt` alone defines `lord_1_48_1/2/3`, with race `uruk`, age 20 and one Nazgul kit. A `characters/lords.xml` row is a second definition: it wins each attribute it states and unions its equipment sets with the template's, so a campaign picked the kit at random. ``
  and a blank line.
- `docs/features/custom-battles.md`: in the Changelog, directly above the line
  `- 2026-10-02: the curated Mordor, Gondor and Rohan commanders now exist in Custom Battle...`, add
  `- <today's date>: `lord_1_48_1/2/3` left `characters/lords.xml`, so they are stubs too: 24 commanders on this branch are vanilla SandBox lords rebuilt by `lords.xslt`.`
- `docs/features/uncapturable-heroes.md`: in the `lord_1_48_1`, `lord_1_48_2`, `lord_1_48_3` row, the third cell becomes `` `lords.xslt` templates ``.
- `TAOM.Tests/Features/UncapturableHeroes/UncapturableRegistryTests.cs`, the summary comment: `in <c>characters/lords.xml</c>` becomes `in <c>lords.xslt</c>`.

**Verify**: prints `removed 3`; `git diff --stat -- Main/_Module/ModuleData/characters/lords.xml` shows `153 deletions(-)` and no
insertion; `grep -c 'name="race">uruk<' Main/_Module/ModuleData/lords.xslt` is 3 more than `git show HEAD:Main/_Module/ModuleData/lords.xslt | grep -c 'name="race">uruk<'`;
Build, then Filtered
`--filter "FullyQualifiedName~LesserNazgulDefinitionTests|FullyQualifiedName~CustomBattle|FullyQualifiedName~LordIdentityConsistencyTests|FullyQualifiedName~LordFamilyTransformTests|FullyQualifiedName~CultureRaceConsistencyTests|FullyQualifiedName~CharacterFaceCoverageTests|FullyQualifiedName~NazgulFamily"`
shows `Failed: 0`; `python tools/validate_moduledata.py` shows no new ERROR.

Commit (message file `msg-134-8.txt`; stage `Main/_Module/ModuleData/lords.xslt` `Main/_Module/ModuleData/characters/lords.xml`
`Main/_Module/ModuleData/characters/custom_battle_lords.xml` `TAOM.Tests/Features/NazgulFamily/LesserNazgulDefinitionTests.cs`
`docs/features/nazgul-family.md` `docs/features/custom-battles.md` `docs/features/uncapturable-heroes.md`
`TAOM.Tests/Features/UncapturableHeroes/UncapturableRegistryTests.cs`):
```text
fix(nazgul): v2.0.28 - three lesser Nazgul get one kit each

The Tainted, the Shadow of the Northmen and the Shadow of Umbar were
defined twice, so each new campaign dressed each of them in a Nazgul
kit or a generic Mordor lord kit at random. They now always wear the
Nazgul kit. Their race (uruk) and age (20) do not change: those values
moved from the second definition into the one that stays. They stay
available in Custom Battle.

A new test fails if characters/lords.xml defines them again.

Research: taom-src v1.4.8 MBObjectManager.MergeElements (:820-875)
Research: taom-src v1.4.8 Hero.SetInitialValuesFromCharacter (:2237)
Save-compat: a saved campaign keeps the kit these lords already wear
Not-tested: the three lords' kit and face in a new campaign
Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14
```
**Verify**: `git log -1 --format=%s` is the subject above; `git status --porcelain` is empty.

### Step C5: The elf horn registration names the shipped file (plan 121, MEMORY-10)

D13 verdict: fixed. Verified on the 1.4.x data and on `8df90444c`; `horn_elf.wav` ships, so no content is needed. Nothing plays
the sound, so the worth of this step is the gate on all 485 paths.

RED. Create `TAOM.Tests/Core/ModuleSoundPathTests.cs` (Write tool):
```csharp
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every path in module_sounds.xml (module_sound and variation elements) must name a file under ModuleSounds.
/// The elf horn registered elf_horn.wav while the file shipped as horn_elf.wav, and nothing reported it.
/// </summary>
[TestClass]
public class ModuleSoundPathTests
{
    [TestMethod]
    public void EveryModuleSoundPath_NamesAShippedFile()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var sounds = Path.Combine(moduleData, "..", "ModuleSounds");
        var paths = XDocument.Load(Path.Combine(moduleData, "module_sounds.xml")).Descendants()
            .Select(e => (string?)e.Attribute("path"))
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        Assert.IsTrue(paths.Count > 0, "module_sounds.xml yielded no path attributes; the file shape changed");

        var missing = paths.Where(p => !File.Exists(Path.Combine(sounds, p!))).ToList();

        Assert.AreEqual(0, missing.Count, "module_sounds.xml paths with no file:\n  " + string.Join("\n  ", missing));
    }
}
```
Build, then Filtered `--filter "FullyQualifiedName~ModuleSoundPathTests"`. **Verify RED**: `Failed: 1`, naming only
`LOTR/Elves/Alert/elf_horn.wav`. Any other path is a STOP: report it, do not edit sound data by judgment.

GREEN. In `Main/_Module/ModuleData/module_sounds.xml` (Edit tool) replace the line
`<module_sound name="LOTR/Elves/Alert/elf_horn.wav" sound_category="alert" path="LOTR/Elves/Alert/elf_horn.wav" />` with
`<module_sound name="LOTR/Elves/Alert/horn_elf.wav" sound_category="alert" path="LOTR/Elves/Alert/horn_elf.wav" />`
(nothing reads this name, and the neighbouring entries' names equal their paths). In `docs/features/kingdom-voices.md` (Edit tool),
directly after the `uruk_01` defined-twice row of the Known defects table, add
`` | ~~**Elf horn path**~~ | FIXED <today's date>. `module_sounds.xml` registered `elf_horn.wav` for the shipped `horn_elf.wav`; nothing plays it. `ModuleSoundPathTests` now checks every path. | ``

**Verify**: `grep -c elf_horn Main/_Module/ModuleData/module_sounds.xml` prints `0`; Build, then the Filtered run above shows
`Failed: 0`.

Commit (message file `msg-134-9.txt`; stage `Main/_Module/ModuleData/module_sounds.xml` `TAOM.Tests/Core/ModuleSoundPathTests.cs`
`docs/features/kingdom-voices.md`):
```text
fix(sounds): v2.0.28 - elf horn registers the file that ships

The elf horn alert was registered as elf_horn.wav while the file ships
as horn_elf.wav. Nothing plays this sound today, so players hear no
change. A new test fails on any sound path with no file behind it;
this was the only one of 485.

Not-tested: the sound in game (nothing plays it)
```
**Verify**: `git log -1 --format=%s` is the subject above; `git status --porcelain` is empty.

### Step C6: Part C wrap-up and hand-off

1. Full Build and Test. **Verify**: `Failed: 0`; total = `B_TOTAL + 11`.
2. `python tools/validate_moduledata.py`. **Verify**: no ERROR outside the WP101 baseline.
3. `python tools/lint_docs.py`. **Verify**: no finding on a line Part C added.
4. Machine checks (replace `$BASE` with the Step 0 sha):
   ```bash
   cd /c/Users/mikew/source/repos/taom-port/134 && \
   git log --format=%s "$BASE"..HEAD; \
   git diff --name-only "$BASE"..HEAD; \
   git log --format=%B "$BASE"..HEAD | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'; \
   git diff --quiet "$BASE" HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj CHANGELOG.md Main/Features/CaravanTrade Main/Features/WarOfTheRingMomentum Main/_Module/ModuleData/execution/alignment.json && echo UNTOUCHED
   ```
   **Verify**: the nine subjects of Steps A1 to C5, each with `v2.0.28 - ` and at most 72 characters; only in-scope paths;
   `0`; `UNTOUCHED`.
5. Delete the `C:\Users\mikew\source\repos\taom-port\msg-134-*.txt` files. Do not edit `plans/README.md`;
   the integrator records the row after the merge. Hand-off report: status (`DONE`; Parts A, B and C);
   branch `port148/134-campaign-data-defects`; each commit hash with its subject;
   `BASE`, `BASE_TOTAL`, `A_TOTAL`, `B_TOTAL` and the final total, passed and skipped counts; any Step 0.3 `DIFF` file
   applied by hand; the `tools/validate_moduledata.py` and `tools/lint_docs.py` results. For the integrator:
   no old-plan row closes with this WP; the plan 121 `elf_horn.wav` note, plan 123's roster XSLT and plan 126's
   `lord_1_48_*` follow-up are closed by Steps C5, C3 and C4; WP135 translates Part C's English rows;
   `docs/modding/lords-and-heroes.md`'s measured counts are stale by three rows (Maintenance notes).

## Disposition (every commit in the pack, plus the items plans 121, 123 and 126 routed here)

| 1.5.x commit | Subject | Disposition | Detail |
|---|---|---|---|
| `685db7e46cfae268fe3ac765cab7ac6e1468cc84` | feat(markets): v2.0.34 - Arthedain and Lindon sell their own gear | adapted (partial, by hand), Step A1 | Taken: the Gondor policy id in `spkingdoms.xslt`, the 28 dead rows of `taom_spkingdoms.xml` (14 kingdoms on 1.4.x, 15 on 1.5.x), `KingdomPolicyIdsTests` re-cited to v1.4.8, the policy lines of `kingdom-creation.md` and `kingdoms.md`. Skipped: the markets feature (`Stock` rows, `StockRule`, the Arthedain and Lindon market config, `CultureMarketplace*` and `ItemPoolAdapter` code and tests), `LadderPieces` and all ArmourAcquisition files (feature absent on 1.4.x), the validator and `rebalance_armor.py` rules and their tests, the `moduledata-validation.md` rule row; the `engine-bump` skill line that lists this test's id set goes to WP143; the RCA and lessons to WP142 |
| `0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d` | fix(balance): v2.0.34 - Lindon troops weigh and survive like Rivendell's | ported (file by file), Step A2 | Three byte-identical files and the new `ShippedBattleBalanceConfigTests` checked out, the troop-weight rows and the twin test hand-inserted (both files diverged on 1.5.x but the hunks are local); feature docs hand-applied with 1.4.x counts. Skipped: the RCA and the two lessons files (WP142) |
| `736fa2fef65a96812648df924efca0b5153a84d0` | fix(banners): v2.0.35 - lords of TAOM cultures carry battle banners | adapted (test only), Step A3 | Taken: `ShippedHeraldryBannerKeyTests`, moved to `TAOM.Tests/Features/BannerBearers/`, reading through `CultureDataFixture`, `#823` wording replaced by the v1.4.8 engine path. Skipped: the LordBanners feature (module, service, map, config provider, `TaomBannerItemModel`, `lord_banner_cultures.json`, their tests), the BannerBearers comment edits, `banner_bearers_config.json`, `FeatureModules.cs`, the `gamemodels.md` rule and registry rows (new feature) |
| `5e43518df7980ebaaec4d22a7370d2df726dd839` | fix(alignment): v2.0.35 - every side follows the realm it serves | adapted (partial re-implementation), Steps B1 and B2 | See the hunk table below |
| `9dd2f594fd887f792a5cd336781b6832ff6ff753` | fix(wanderers): v2.0.34 - give 53 wanderers their own backstories | ported (partial), Steps C1 and C2 (D13) | The WP134 pack note's reason for skipping ("built on Oaths of Service") does not hold: this commit (2026-10-08) predates Oaths of Service (`caed64ae1`, 2026-10-20) and touches no Oaths of Service file, and its defect is verified on 1.4.x (40 wanderers, 280 rows). Taken: the 40 wanderers' rows and the test. Skipped: the 13 Arthedain wanderers (content absent on 1.4.x), the `LanguageTextIntegrityTests` and `LocalizationKeyConsistencyTests` hunks (WP135), the rule row and docs |
| `546d1181f2f68e64ced2955ee7e0d87f7792916f` | fix(wanderers): v2.0.34 - stop Calradian wanderers spawning in TAOM | skipped (D13 condition fails) | The note allows the `lords.xslt` retag only if no TAOM culture is left with zero wanderer templates. On 1.4.x (and the public build) `taom_wanderers.xml` has none for `sturgia` (Dale) or `battania` (Khand), and v1.4.8 places a spawned wanderer in a town of its template's culture (`CompanionsCampaignBehavior.cs:384`), so both towns' cultures would lose all wanderers of their own; all 210 TAOM templates are male, so the retag would also remove every female wanderer (#761). The fix needs the 20 Dale and Khand wanderers, which are content. `tools/complete_lords_xslt.py` is WP118's (its rewrite does not need the retag) |
| `bb68eeca3aeee49ba95ea4956f2b3941820a3f93` | Refactor and enhance various components of TAOM | adapted (item 3 only, routed by plan 123), Step C3 (D13: verified, no OUT content) | Taken: `lord_template_rosters.xslt`, its `SubModule.xml` node, `LordTemplateRosterTests` less `TaomLords_NoNamedRosterSharesALordId` (it guards a 1.5.x-only adapter), the 70 Dale rows of `taom_lord_template_equipment.xml`. Items 1 and 2 are WP123's. Skipped: GeneratedLordKits and the rest (1.5.x-only, recorded in plan 123) |
| `db805427b89cc354abb7206f926f91eab4a12ad8` | fix(lords): v2.0.34 - generated lords wear their culture's own kits | adapted (the stylesheet's comment), Step C3 | Its other hunks are GeneratedLordKits (1.5.x-only) and bookkeeping (WP142) |
| `9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14` | feat(nazgul): v2.0.30 - the Nine become race nazghul (#644) | adapted (the `lord_1_48_1/2/3` definition only, routed by plan 126), Step C4 (D13: verified, no OUT content) | Taken: rows deleted, age and face age moved into the three templates, the stubs (1.5.x's `custom_battle_lords.xml` has them). Changed: the templates emit race `uruk`, today's effective race, not `nazghul`; the test is a new, smaller one. Everything else stays with plan 126's disposition of this commit. The other 176 lords in both files (#648) are not ported |
| none (plan 121, MEMORY-10) | `module_sounds.xml` elf horn path | fixed, Step C5 (D13: verified, no OUT content) | No 1.5.x commit; unfixed on both lines at planning. Gate: `ModuleSoundPathTests` |

Hunks of `5e43518df`:

| Hunk | Disposition |
|---|---|
| `Main/Adapters/FactionSides.cs`, `Main/Features/Execution/SideIds.cs`, `AlignmentServiceExtensions.cs`, `IAlignmentService.cs` doc, `FactionSidesTests`, `AlignmentServiceTests` | taken (B1); `FactionSides.OfCharacter` and its test left out (no 1.4.x caller; the 1.5.x caller is the absent tournament filter); doc and cites adapted to 1.4.x |
| `ExecutionRelationService.cs`, `IExecutionRelationService.cs`, `IOnExecutionAction.cs`, `ExecutionActionHook.cs`, `ExecutionRelationServiceTests`, `ExecutionActionHookTests` | taken, adapted to the 1.4.x members (no blood-feud `GetRelationModifier` on the hook) (B2) |
| `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs`, `TraitLevelingHelper_OnBloodFeudStarted_Patch.cs`, `ExecutionIoC.cs` | skipped: the blood-feud seams are v1.5.x engine API, absent on 1.4.x; 1.4.x `ExecutionIoC` needs no change. Their pairing rule is re-implemented in the 1.4.x seams instead: `KillCharacterAction_ApplyInternal_Patch`, `ExecutionContext`, `TraitLevelingHelper_OnLordExecuted_Patch`, `TaomExecutionRelationModel`, `ExecutionContextTests` (B2) |
| `IPlayerContextAdapter.cs`, `PlayerContextAdapter.cs` (`GetPlayerCultureId` removed) | adapted: replaced by `GetPlayerSideIds()`, so the model keeps its constructor and `Main/SubModule.cs` stays untouched |
| `WandererAllegianceDialogBehavior.cs`, `WandererAllegianceService.cs`, `WandererAllegianceServiceTests`, `IWandererAllegianceService.cs` deletion, `WandererAllegianceIoC.cs` | adapted: the side pair taken; the interface and its registration kept (WP105 module, `WandererAllegianceWiringTests`) |
| `IPrisonerRecruitmentMoraleService.cs`, `PrisonerRecruitmentMoraleService.cs`, `TaomPrisonerRecruitmentCalculationModel.cs`, `PrisonerRecruitmentMoraleServiceTests` | taken; the model line hand-merged (1.4.x keeps `int`) |
| `MarriageAlignmentService.cs` comment, `TaomSettings.cs` hint, `marriage-alignment.md` | taken (Dunland is Evil, Khand Neutral on 1.4.x too) |
| `CaravanTradeService.cs` + tests, `MomentumEnrollmentService.cs` + tests | skipped: behaviour-equivalent refactors; on 1.4.x both already pair a faction id with that faction's own culture (the WP134 note restricts #957 to parts whose bug exists here; V06 reached the same verdict for momentum). |
| Arena (`TournamentEntrant.cs`, `TournamentEntrantMapper.cs`, `TournamentAlignmentFilterService.cs`, its tests) | skipped: the tournament alignment filter (#744) is absent on 1.4.x; 1.4.x `TournamentEntrant` has no side fields |
| LordCourtesies, HardQuestions, FromTheAshes, GoodDeeds, BondsOfBattle, TokensOfEsteem (+ their adapters and tests) | skipped: features absent on 1.4.x |
| MinorClanMigration (module, service, behavior, adapter, tests), `Main/Composition/FeatureModules.cs` | skipped: new feature |
| `alignment.json` minor-clan rows, `lords.xslt`, `spclans.xslt`, `custom_battle_harad_troop_filter.xslt`, `CustomBattleTroopFilterTests`, `ShippedClanSideCoverageTests`, `tools/complete_lords_xslt.py` + tests | skipped: content (minor-clan sides and troop cultures) |
| `ShippedMainCultureAlignmentCoverageTests` comment, `alignment-desertion.md` | skipped: comment-only in an untouched file; minor-clan desertion text (content) |
| `prisoner-recruitment.md`, `wanderer-allegiance.md`, `execution.md`, `alignment-aware-execution.md` | adapted by hand (B2) |
| Docs of absent features, `docs/modding/*`, `docs/reference/*`, `docs/INDEX.md`, lessons, RCA, `tools/README.md` | skipped: absent features or bookkeeping (WPs 142 to 144) |

## Test plan

- New: `KingdomPolicyIdsTests` (1 method, 4 rows), `ShippedBattleBalanceConfigTests` (1),
  `ShippedHeraldryBannerKeyTests` (4 + 1 method with 5 rows), `FactionSidesTests` (7),
  `SidePairScanTests` (3); `EveryLindonTroop_WeighsTheSameAsItsRivendellTwin`, the `lindon` survival row,
  `ResolveSide_SideIds_MatchesTheStringOverload`, `GetVictimAndGetExecutor_WithNoContext_AreNone`,
  `Evaluate_PlayerWithNoSide_IsNeutralAndAllowedByAnEvilWanderer`,
  `ShouldWaiveMoralePenalty_FreeCultureLordOfNeutralRealm_DoesNotWaiveOtherFreeCulture`. Removed with
  their behaviour: two kingdom-null cases in `PrisonerRecruitmentMoraleServiceTests` and one in
  `ExecutionRelationServiceTests` (covered by `AlignmentServiceTests` and the empty-id cases).
- Part C: `WandererBackstoryCoverageTests` (4), `LordTemplateRosterTests` (4), `LesserNazgulDefinitionTests` (2),
  `ModuleSoundPathTests` (1).
- RED proof: A1 (2 failing rows), A2 (3 failures), B1 and B2.2 (compile), B2.1 (scan lists three
  types), C1 (280 rows), C3 (4 failures), C4 (2), C5 (1). A3's data tests start green by design; its rule rows prove the rule.
- Patterns: `TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs` (shipped-data gates),
  `TAOM.Tests/Features/BattleBalance/PartyOwnerGetterBanTests.cs` (an `IlCallScanner` ban over TAOM.dll, the
  same machinery `SidePairScanTests` uses).
- Untestable here (commit `Not-tested:` trailers, In-game checks): Gondor's policies in a new campaign,
  party-screen weights, the Harmony capture inside a real execution, the dialog and party-screen seams,
  a generated lord's kit, the three Nazgul's kit and face.
- Totals: Part A `BASE_TOTAL + 16` (folded `+ 8`); Part B `+ 11`; Part C `+ 11`.

## Done criteria

Part A:
- [ ] Build exits 0; Test `Failed: 0`, total `BASE_TOTAL + 16` (folded `+ 8`).
- [ ] `git grep -c 'policy_lord_prerogative\|policy_religious_privilege\|policy_land_grants_for_veterans' -- Main/_Module/ModuleData` prints nothing; `grep -c 'policy_land_grands_for_veteran' Main/_Module/ModuleData/spkingdoms.xslt` prints `1`.
- [ ] `grep -c 'id="lindon_' Main/_Module/ModuleData/TroopWeights/troop_weights.xml` prints `22`; `grep -c '"lindon": 0.4' Main/_Module/ModuleData/configs/battle_balance_config.json` prints `1`.
- [ ] `TAOM.Tests/Features/BannerBearers/ShippedHeraldryBannerKeyTests.cs` exists; its 9 results pass.
- [ ] `python tools/validate_moduledata.py` shows no ERROR outside the WP101 baseline.
Part B:
- [ ] Build exits 0; Test `Failed: 0`, total `A_TOTAL + 11`; `SidePairScanTests` 3 passed, none inconclusive.
- [ ] `git grep -n 'ExecutionParticipant\|GetPlayerCultureId\|OfCharacter' -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -n 'new SideIds(' -- Main` lists only `Main/Adapters/FactionSides.cs`.
- [ ] `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `CHANGELOG.md`, `Main/Features/CaravanTrade`, `Main/Features/WarOfTheRingMomentum`, `alignment.json`, `lords.xslt` unchanged at the end of Part B (Step B3.3 prints `UNTOUCHED`; Step C4 then edits `lords.xslt`).
Both:
- [ ] `git log --format=%s BASE..HEAD` shows nine subjects, each with `v2.0.28 - ` and at most 72 characters; no body line and no added doc or code line holds an em or en dash.
- [ ] `git status --porcelain` prints nothing; `git diff --name-only BASE..HEAD` lists only in-scope paths.
- [ ] `git diff --name-only BASE..HEAD -- plans/README.md` prints nothing; the Step C6.5 hand-off report carries the status, hashes and counts.
Part C:
- [ ] Build exits 0; Test `Failed: 0`, total `B_TOTAL + 11`; Step C6.4 prints `UNTOUCHED`.
- [ ] `WandererBackstoryCoverageTests` 4 passed; no `spc_wanderer_arthedain` row in any of the 13 files.
- [ ] `LordTemplateRosterTests` 4 passed; `grep -c 'sk_dale_chest_chivalry_a04' Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` prints `10`.
- [ ] `LesserNazgulDefinitionTests` 2 passed; `grep -c 'id="lord_1_48_[123]"' Main/_Module/ModuleData/characters/lords.xml` prints `0`; `grep -c 'id="lord_1_48_[123]"' Main/_Module/ModuleData/characters/custom_battle_lords.xml` prints `3`.
- [ ] `ModuleSoundPathTests` 1 passed.

## STOP conditions

Stop and report (do not improvise) if:

- The installed Armory reads `v2.0.26` before Part A or Part C (D20), or `BANNERLORD_GAME_DIR` is empty
  (environment: report it, never set it).
- `TAOM.Tests/Infrastructure/RepoPaths.cs` or its `ReadSource(string, bool)` is missing before Part B
  (WP104 not merged), or an `IlCallScanner`/`GameAssemblies` member in Step 0.2 is missing.
- Step 0.2 finds any symbol of this plan already present (an earlier WP took part of it), a fourth
  `GetPlayerCultureId` caller, or a `.ResolveSide(` caller other than the four listed: the orchestrator
  must re-scope (never delete a member with an extra caller, never allow-list an unknown caller).
- A Step 0.4 anchor count is not `1`, or an edit script's assertion fails (28 policy rows, 320 rows per file).
- A1's RED names other dead ids than the three expected, or a culture row fails: data drifted; report it.
- After A2's data, `EveryLindonTroop_WeighsTheSameAsItsRivendellTwin` or any level-band test fails (a
  Lindon troop without a twin, a twin of another level): do not change a weight by judgment (D13).
- A3's data tests fail on arrival: report the bad key; do not edit heraldry (content).
- B2.1's RED lists types other than the three expected, B2's GREEN scan still finds a caller besides
  `AlignmentServiceExtensions`, the scan reports method bodies it could not read, or `SidePairScanTests`
  is `Inconclusive` (game assemblies not loaded).
- `BindingVerification` tests fail after B2, or the build reports any engine member in "Current state"
  missing: the installed engine differs from v1.4.8 as documented.
- The work appears to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `CHANGELOG.md`,
  `MomentumEnrollmentService.cs`, `CaravanTradeService.cs` or any other out-of-scope path, or a hook
  blocks a commit (subject label, ModuleData validation, a CHANGELOG demand): never bypass a hook.
- `python tools/validate_moduledata.py` reports an ERROR the WP101 baseline does not list.
- A step's verification fails twice after a reasonable fix attempt, or a wrap-up total differs from the
  expected delta.
- C3's stylesheet leaves a pool empty (`TaomRosters_AfterTransform_FillEveryPoolTheEngineRequests` fails), or C4 breaks a lord or
  Custom Battle test: report it, never restore a vanilla flag or a row by judgment (D13).
- C5's RED names any path other than the elf horn.

## In-game checks owed (maintainer, after the integrator deploys a build that contains this WP)

1. **Gondor's policies.** Start a new campaign; open Kingdom, Policies for Gondor. Look for: "Land Grants
   for Veterans" active beside "Senate". No log line is expected either way (the engine drops unknown
   ids silently).
2. **Lindon weights.** Recruit Lindon elites (for example `lindon_imladris_blademaster`) and their
   Imladris twins. Look for: the party screen tags both `x3` (level 41+) or `x2` (26 to 36) alike, and the
   party-size header counts them the same.
3. **Alignment sides.** As a Gondor-cultured player, swear to Umbar or Khand as a vassal.
   (a) Ask a Mordor-culture wanderer to join: the hire goes ahead (before: "You march with the
   Elf-friends..." refusal). (b) On the party screen, a Rohan prisoner's recruit cost reads `-1` (vanilla)
   and a Gondor prisoner's `0` (same-culture rule). (c) Optional: execute a lord and compare the
   pre-confirm relation preview with the applied changes; they must agree. In the newest
   `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_<timestamp>.log` there must be no
   `[WandererAllegiance] could not evaluate the hire verdict` error.
4. **Wanderers.** Talk to a goblin, Misty Mountain orc, Blue Craig or Lindon wanderer
   in a tavern. Look for: an introduction and backstory, no "ERROR: Text with id backstory_c doesn't exist!".
5. **Generated lords.** In a new campaign, let time pass until a lord of Dunland, Rohan, Harad, Rhun, Dale or Khand
   is generated during play (for example one who comes of age). Look for: a TAOM template kit, no Calradian
   gear, no bare hero. No log line is expected.
6. **Lesser Nazgul.** Start two new campaigns; open the encyclopedia pages of the Tainted, the Shadow of the
   Northmen and the Shadow of Umbar (`lord_1_48_1/2/3`). Look for: the Nazgul kit both times, an uruk body, the same
   face as before. In Custom Battle, pick one as a Mordor commander: it loads with a name, a face and the kit.

## Maintenance notes

- **Part C decision (D13, decided).** The WP134 pack note skips #757 "built on Oaths of Service", which
  appears to conflate it with the named-companion backstories of batch note [946]. `9dd2f594f` is a pure
  string-data fix for 40 shipped wanderers (defect verified on 1.4.x data, no OUT content), so it is ported.
  WP135 translates its English rows, and nothing in plan 135 needs anything else from it (its preflight
  counts `RegisteredEnglish` in `LanguageTextIntegrityTests.cs`, which this plan never touches, and its A8
  adopts that hunk itself).
- **Plan 110** records that no WP changes `MomentumEnrollmentService.ResolveSide`; this plan does not
  touch it (binding note, V06), so plan 110's both-sides start rule and this plan never meet.
- **WP142's** `caravan-trade.md` correction assumes the private `ResolveSide` in `CaravanTradeService.cs`;
  it is still there after this plan.
- **WP128** adds `IPlayerContextAdapter.GetPlayerClanId`; this plan replaces `GetPlayerCultureId` with
  `GetPlayerSideIds`. Either merge order works; a later port that names `GetPlayerCultureId` must use
  `GetPlayerSideIds` or `FactionSides.OfPlayer()`.
- **Future ports touching sides** must build pairs with `FactionSides` and add any new two-argument
  `ResolveSide` caller to `SidePairScanTests.TwoArgumentCallers` with its reason, or the scan fails. The
  1.4.x execution path (`KillCharacterAction` prefix, `ExecutionContext`, `OnLordExecuted`, the relation
  model) differs from 1.5.x's blood-feud seams; adapt, never copy, 1.5.x Execution code.
- **The interface stays.** 1.5.x deleted `IWandererAllegianceService`; a later doc sync from the 1.5.x tip
  must not reintroduce "no interface" text into `wanderer-allegiance.md`.
- **WP135** brings `LanguageTextIntegrityTests`' writing-system rule. 1.5.x made it skip rows still equal
  to English (part of `9dd2f594f`); English placeholder rows from any wave-4 WP need that skip or the
  translation run first.
- **WP143**: the `engine-bump` skill line from `685db7e46` that names `KingdomPolicyIdsTests.EnginePolicyIds`.
  **WP142**: the RCAs and lessons of #749, #756, #823, #957.
- **Follow-ups not taken (content or out of scope).** `docs/features/alignment-desertion.md:5` lists Khand
  among Evil lords (alignment.json says Neutral); the vanilla-wanderer retag (`546d1181f`) waits for Dale and
  Khand wanderers; the other 176 lords in both `lords.xslt` and `characters/lords.xml` (1.5.x's #648).
- **Routed items, closed here.** Plan 123's roster XSLT (Step C3), plan 126's `lord_1_48_1/2/3` (Step C4; no race
  or age decision is needed because today's effective values move), plan 121's elf horn (Step C5).
- **Counts Step C4 makes stale.** `docs/modding/lords-and-heroes.md` states 1184 rows in `characters/lords.xml`
  and 59 `uruk`, measured with these three rows (1181 and 56 after). No plan of this programme re-measures that
  page; the next edit to it should.
- **Reviewer focus** (the orchestrator runs `/deep-review`; C# in more than two files): `ExecutionContext`
  ownership semantics unchanged (nested kills, `__state`); `FactionSides.OfHero` on a companion reads the
  clan it serves; the model keeps no inline branching (gamemodels rule 4); the allow-list is exact; the
  two GameModels still return what vanilla's base returns when no rule fires. `/verify-bindings` is not
  needed: no Harmony target, category or reflection site changes.
