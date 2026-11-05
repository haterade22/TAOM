# Plan 133: Port the inventory, banner-colour, map-bar and campaign UI fixes to the 1.4.x line

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm
> the expected result before moving on. If anything in "STOP conditions" occurs, stop and report; do not
> improvise. Do not edit `plans/README.md`: every WP branch shares it, so the integrator records this
> plan's status and test count when merging. Put the numbers each part's hand-off asks for in your final
> message instead. The plan has four parts (A to D). One session may run one or more parts, in order; each
> part ends green and is independently mergeable.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Adapters Main/Features/ShadowfaxScreens/Party/Hooks Main/Features/QuickActions Main/Features/EquipPresets/Hooks Main/Features/BannerColorPersistence Main/Features/BannerBearers Main/Features/DreadAura Main/Features/UncapturableHeroes Main/Features/Refuge Main/Features/SpecialResources/UI Main/Features/FiefManagement/Hooks Main/Features/FieldCamp/UI Main/Features/TaomSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Core/Validation Main/Composition Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages Main/_Module/ModuleData/module_strings.xslt Main/_Module/ModuleData/comment_strings.xslt Main/_Module/ModuleData/banner_bearers Main/_Module/ModuleData/culture_marketplace tools/generate_new_faction_kingdoms.py TAOM.Tests .claude/rules/gui-ui.md .claude/rules/csharp-patterns.md docs/features docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/ai-includes/new-culture-authoring.md docs/modding/recipe-add-a-kingdom.md`
> Earlier WPs of this programme are expected to have changed several of these paths (WP104 and WP105
> above all). Compare the live files against "Current state" (Step 0.5 checks every anchor) and STOP only
> on a real mismatch.

## Status

- **Priority**: P1
- **Effort**: L (19 pack commits: 16 ported by cherry-pick, one of them with a hand-adapted half; 2 re-implemented by hand; 1 skipped; 18 commits on the branch, four parts)
- **Risk**: MED (two Harmony retargets in Patch23, one new Patch75 postfix, one new patch category; 13 string files; no save-format change)
- **Depends on**: WP105 (`plans/105-composition-root.md`: `Main/Composition`, `FeatureModules.All`), and through it WP104 (`plans/104-guarded-patch-categories.md`: `TAOM.Tests/Infrastructure/RepoPaths.cs`, `Main/PatchCategoryApplier.cs`, `Main/PatchCategoryIndex.cs`, `TryPatchCategory`) and WP101; WP112 (`plans/112-crash-robustness.md`: creates `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs` and takes `PlayerEquipmentAdapter.cs` to the `1888b6f28` parent blob, A1); WP125 (`plans/125-campaign-ui-perf.md`: creates the party portrait cache patch whose comment B3.6 corrects)
- **Category**: bug
- **Wave / lane**: wave 4, lane E
- **Executor**: laptop
- **Decisions applied**: D1 (no plan stages `CHANGELOG.md`), D2 (Tavern Specialities arrives as its 1.5.x module plus one `FeatureModules.All` line), D7 (`[ClientOnly]` and `ClientComposition` dropped), D8 (subjects carry `v2.0.28`), D9 (no `RequiresGame`/`RequiresGameIL` test categories), D10 (ordinary session), D13 (the banner rows are verified on 1.4.x data; the troll-gear Armory policy is not ported), D19 (git only through the Bash tool), D20 (laptop Armory refreshed before any data commit)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`; 2026-11-04
- **Issue**: ports of #917, #924, #925, #926, #973 (Part A); #932, #993, #994 (Part B); #958, #977, #923, #947, #789 (gate only) (Part C); #937, #955 (Part D). The review 130 config-list fixes (`b2e387db7`) and the culture-market NotMerchandise skip (from `bc39f6e4d`) carry no issue: orchestrator creates the issue before implementation lands.

## Why this matters

Every defect here is present on `origin/bannerlord-1.4.5` and was fixed on the 1.5.x line in October. In the
inventory, Quick Actions "Unequip All" writes equipment slots directly (`QuickActionsService.cs:209`), so the
open screen keeps showing the old gear and Cancel cannot undo it; a preset load and every batch sell leave the
paper doll, weight and Done button stale and sold-out rows at count 0; two MCM options ("Sell ... Equipped")
do nothing at all. Banner Color Persistence never reaches a portrait: five postfixes assign `Color1/Color2` on
a `CharacterCode` whose `Code` string was already built (v1.4.8 `CharacterCode.cs:18`, `:87`), and the
inventory doll gets clan colours only on a character switch. Refuge rows on the clan screen lose "(being
raised)" and the honest zero wage the moment the screen opens. Lindon and Blue Craig formations raise no banner
(no config row; verified on 1.4.x data). The map-bar resource counter appears two seconds late on every load
and stays after the hero loses the resource. Every TAOM-only realm's coronation shows an engine error text
instead of a title (v1.4.8 `GameTextManager.cs:91`), Dunland's says "Emperor of Calradia", and lords name their
liege with no title. The tavern maid offers the previous town's dishes or blanks in TAOM towns. F6 and the
field camp's Make camp can push a menu while the escape menu is open or the player is a prisoner. Culture
markets stock items the Armory marks not for sale (the laptop's installed v2.0.26 Armory carries 29
`is_merchandise="false"` attributes, troll armour among them). None of these is a crash or a freeze; together
they are the most visible campaign UI faults left on the line.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`). Never
  write into the game install (reading it is fine).
- Base branch: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one (WP104, WP105, WP112 and
  WP125 at least).
- Worktree: `C:\Users\mikew\source\repos\taom-port\133`, branch `port148/133-campaign-fixes-inventory-ui`. The
  folder is outside the session's working directory, so a `cd` does not persist: start EVERY Bash command with
  `cd /c/Users/mikew/source/repos/taom-port/133 &&`.
- Commit message files go in `C:\Users\mikew\source\repos\taom-port\` (for example `133-msg-A1.txt`); helper
  scripts in `C:\Users\mikew\source\repos\taom-port\wp133-scripts\`. Write both with your file-writing tool,
  not a shell heredoc.
- Python: `python`, or `/c/Python314/python.exe` if Git Bash cannot find it.
- Repo text files are LF in git and CRLF in the worktree (`core.autocrlf=true`; `git ls-files --eol` shows
  `i/lf w/crlf`), so a `git show <sha>:<path> > <path>` file arrives LF and `git add` normalises it with a
  harmless warning. The 12 language files are `i/-text`, which `seed_strings.py` handles. Edit with your Edit tool; never rewrite a whole tracked file from a shell redirect except where
  a step says `git show <sha>:<path> > <path>`. After you create or fully rewrite a file with your file-writing
  tool, normalise it to CRLF:
  `python -c "import pathlib,sys; p=pathlib.Path(sys.argv[1]); p.write_bytes(p.read_bytes().replace(b'\r\n', b'\n').replace(b'\n', b'\r\n'))" <path>`.
  If `git diff --cached --stat` shows every line of a file changed after a small edit, its line endings changed:
  run the same command on it and re-stage.

### What arrives from earlier WPs (Step 0.3 checks each; absent means STOP)

| Symbol or file | From | Used by |
|---|---|---|
| `TAOM.Tests/Infrastructure/RepoPaths.cs`: `ReadSource(string relativePath, bool stripComments = false)`, `StripComments(string)`, `RepoPath(params string[])`, `CSharpFiles(params string[])`, `CodeRoots = { "Main" }` | WP104 | C1, C4, C6, C7, B3 tests |
| `Main/PatchCategoryApplier.cs`, `private bool TryPatchCategory(string category)` in `Main/SubModule.cs` | WP104 | A6 comment, Patch75/Patch23 application |
| `Main/PatchCategoryIndex.cs` (`internal sealed class PatchCategoryIndex` in namespace `TAOM`, `Build(Assembly)`, `Apply(Harmony, string)`) | WP104 | D1 binding test |
| `Main/Composition/FeatureModules.cs` (`internal static readonly TaomFeatureModule[] All`), `TaomFeatureModule`, `PatchCategoryDecl(category, phase)`, `ApplyPhase.GameInit` | WP105 | D1 module, D2 test |
| `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs` with the `[DataRow("Main/Adapters/PlayerEquipmentAdapter.cs")]` row; `Main/Adapters/PlayerEquipmentAdapter.cs` reading `Campaign.Current?.MainParty?.Party` | WP112 | A1 |
| `Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs` with its pre-#993 comment; `TAOM.Tests/Features/ShadowfaxScreens/Party/PartyFastOpenBindingTests.cs` | WP125 | B3.6 |

### Files and roles

| Part | File (1.4.x) | Role in this WP |
|---|---|---|
| A | `Main/Adapters/InventoryVMAdapter.cs`, `IInventoryVMAdapter.cs` | Quick Actions' screen wrapper; gains the bound panel refresh and `RefreshPanel` |
| A | `Main/Adapters/PlayerEquipmentAdapter.cs`, `IPlayerEquipmentAdapter.cs` | Lose the direct-write `TryUnequipAllPlayerSlots` (CharacterCreation keeps `ApplyRosterToPlayer`) |
| A | `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs` | Loses the `PlayerEquipmentAdapter.cs` row of `AliasedSites` (WP112's file; A1 deletes the method it pinned) |
| A | `Main/Adapters/InventoryScreenAdapter.cs`, `IInventoryScreenAdapter.cs` | EquipPresets' wrapper; `RefreshActiveVM` switches to `RefreshPanel` |
| A | `Main/Adapters/IInventoryItemAdapter.cs`, `InventoryItemAdapter.cs`, `Main/Features/QuickActions/*`, `Main/Features/TaomSettings.cs`, `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | The two dead MCM options and `IsEquipped` go |
| A | `Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs` | Inventory doll postfix; retargeted to `UpdateCharacterArmorColor` |
| A | `Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs`, `.../Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs` | Comment corrections only |
| B | `Main/Core/Validation/EnumNames.cs` (new) | Name-only enum parse for config strings |
| B | `Main/Features/BannerBearers/*`, `DreadAura/DreadAuraConfigProvider.cs`, `UncapturableHeroes/UncapturableHeroesConfigProvider.cs`, `Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json` | List validation; Lindon and Blue Craig banner rows |
| B | `Main/Features/BannerColorPersistence/Hooks/*_GetCharacterCode_Patch.cs`, `OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs`, `ClanColoredCharacterCode.cs` (new) | Portrait recolour by rebuilding the code |
| B | `Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs`, `RefugeClanRowRefreshPatch.cs` (new), `Main/Features/Refuge/RefugeIoC.cs` | Refuge row decoration moves to a per-refresh postfix |
| B | `Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs` | Comment only: the party cache now stores the recoloured code (B3.6); `docs/features/shadowfax-screens.md` gets the matching paragraph |
| C | `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Map-bar entry insert and removal |
| C | `Main/Adapters/CommanderLordAdapter.cs` | Drops the ignored `affectRelatives: true` |
| C | `tools/generate_new_faction_kingdoms.py` | `upsert()` replaces its block in place |
| C | `Main/Adapters/MapMenuGate.cs` (new), `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs`, `Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs`, `ICampMenuActivationQuery.cs` | One map-menu gate |
| C | `Main/Adapters/ItemPoolAdapter.cs` | Culture-market pool skips NotMerchandise items |
| D | `Main/Features/TavernSpecialities/*` (new), `Main/Composition/FeatureModules.cs` | Patch111 prefix and its module |
| D | `Main/Features/LiegeTitles/TaomCultureTags.cs` (new), `taom_module_strings.xml`, the 12 language files, `module_strings.xslt`, `comment_strings.xslt` | Liege titles |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`)

```csharp
// Main/Features/QuickActions/QuickActionsService.cs:209 (UnequipAll): the direct-write route
            affected = _equipment.TryUnequipAllPlayerSlots();
// Main/Adapters/InventoryVMAdapter.cs:25, :101-106, :145
    public InventoryVMAdapter(IPlayerEquipmentAdapter playerEquipment, IModLogger logger)
    public void RefreshDisplay() { ... _active.RefreshValues(); ... }      // labels only on v1.4.8
        return _playerEquipment.TryUnequipAllPlayerSlots();                 // the fallback
// Main/Adapters/InventoryScreenAdapter.cs:58-63
    public void RefreshActiveVM() { if (_active == null) return; try { _active.RefreshValues(); } ... }
// Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:21-23
        AccessTools.Method(typeof(SPInventoryVM), "UpdateCurrentCharacterIfPossible", new[] { typeof(int), typeof(bool) });
// Main/Features/BannerColorPersistence/Hooks/CampaignUIHelper_GetCharacterCode_Patch.cs:22, :29-30
    public static void Postfix(CharacterCode __result, TaleWorlds.CampaignSystem.CharacterObject character)
        __result.Color1 = info.Value.Color1;   // never reaches the portrait
// Main/Features/BannerBearers/BannerBearerConfigProvider.cs:177
            if (!string.IsNullOrEmpty(entry) && Enum.TryParse<FormationClass>(entry, ignoreCase: true, out _))
// Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs:91-102 (decorates once, at list build) and :127-134
                var row = new ClanPartyItemVM(party.Party, onSelect, noop, noop, ClanPartyItemVM.ClanPartyType.Garrison, ...);
                SuppressWagePanel(row);  if (refuge.Building) { row.Name = ... "(being raised)" ... }
// Main/Features/Refuge/RefugeIoC.cs:29-37: InitializePatchStatics calls RefugeClanScreenPatch.Initialize(refugeService);
// Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs:31-32, :90-98, :123 (skips the first refresh)
        if (resource == null) return;
        if (_baseInitialized && !_itemAdded && ViewModel is MapInfoVM mapInfo) { mapInfo.SecondaryInfoItems.Add(_resourceInfo); _itemAdded = true; }
        _baseInitialized = true;
// Main/Adapters/CommanderLordAdapter.cs:199
            ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: true, showQuickNotification: false);
// Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs:38-57 and FieldCamp/UI/MapScreenCampMenuActivationQuery.cs:17-53:
//   two hand copies of MapScreen's modal guard list; neither checks the escape menu, a menu context or vanilla's map-action helper
// Main/Adapters/ItemPoolAdapter.cs:70-78: every ItemObject with a StringId enters the culture-market pool
// Main/Features/TaomSettings.cs:655-656, :668-669: SellDamagedEquipped, SellLowValueEquipped (both read only by QuickActionsService.cs:263, :276)
// tools/generate_new_faction_kingdoms.py:97-102: upsert() strips its block and re-appends it before the closing tag
```

1.4.x data facts (read this session from `origin/bannerlord-1.4.5`):
- `taom_spkingdoms.xml` defines 14 kingdoms; `special_resources_config.xml` (11 resources) maps 10 of them;
  `goblin`, `mistymountainorcs`, `bluecraig`, `lindon` have no resource. All 8 vanilla kingdoms that
  `spkingdoms.xslt` rewrites (aserai, battania, empire, empire_s, empire_w, khuzait, sturgia, vlandia) are mapped.
- The infantry troops that `taom_partyTemplates.xml` fields belong to 20 cultures; only `bluecraig` and `lindon`
  have no `CultureBanners` row; no `wild_trolls` troop exists (same result on `origin/fix/public-armory-ids-145`).
- 22 kingdom cultures (`culture="Culture.x"` in `taom_spkingdoms.xml` plus `spkingdoms.xslt`); the 16 TAOM-only
  ones (abanissa, bluecraig, dolguldur, erebor, goblin, gondor, gundabad, isengard, lindon, lothlorien,
  mirkwood, mistymountainorcs, mordor, rivendell, shaghana, umbar) all exist in `taom_spcultures.xml`;
  `arthedain` does not exist on 1.4.x.
- `characters/heroes.xml` holds 36 hand-authored `<Hero>` entries after the generator's
  `TAOM-NEWFACTIONS:lindon:END` marker (line 3770; `</Heroes>` at 3924).
- No `ConversationTag` subclass, `EnumNames`, `MapMenuGate`, `LiegeTitles` or `TavernSpecialities` exists in
  `Main/`; the highest literal patch category is `Patch88`.

### Engine facts verified on the installed v1.4.8 (taom-src cache `C:\Users\mikew\.taom-src\v1.4.8\`, this session)

| Fact | Where |
|---|---|
| `private void UpdateRightCharacter()` `:4377` calls `UpdateCharacterArmorColor()` `:4381`; `private void RefreshInformationValues()` `:3968`; `public void ExecuteRemoveZeroCounts()` `:3437`; `public override void RefreshValues()` `:3182` sets labels and hints only and is called once, from the constructor `:3163` | `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM.cs` |
| `private void AfterTransfer(InventoryLogic, List<TransferCommandResult>)` `:3838` clears `_isCharacterEquipmentDirty` `:3840` and only sets row counts `:3853-3856`; `ProcessEquipItem` `:3475-3486` (`RefreshInformationValues` `:3482`, `ExecuteRemoveZeroCounts` `:3483`); `ProcessUnequipItem` `:3488-3497` (`:3494`); `ProcessSellItem` prunes only when `!cameFromTradeData` `:3565-3568` | same file |
| `private void UpdateCharacterArmorColor()` `:4421`: stealth branch `:4423-4427` (`ActiveEquipment.IsStealth`), faction branch `:4428-4432`; repaints reached via `UpdateRightCharacter` from open `:3122`, outfit toggle `:1645-1647`, Reset `:3806`, character switch `UpdateCurrentCharacterIfPossible` (body from `:4294`, pair at `:4314-4315`); a plain equip refreshes without it `:4019-4024`; `ActiveEquipment` `:452` (stealth case `:462-463`); `enum EquipmentModes { Civilian, Battle, Stealth }` `:26-31`; fields `_currentCharacter` `:115`, `_equipmentMode` `:386`; `public int EquipmentMode` `:1633` | same file |
| `InventoryLogic` reads the destination index only for equipment sides `:890`, `:922-926` | `TaleWorlds.CampaignSystem.Inventory.InventoryLogic.cs` |
| `CharacterObject.FirstStealthEquipment` `:176`; `Equipment.IsStealth` `:54`; `Equipment(EquipmentType)` `:151`; `Hero._stealthEquipment` is a private property `:217` | `...CharacterObject.cs`, `TaleWorlds.Core.Equipment.cs`, `...Hero.cs` |
| `CharacterCode`: `BodyProperties` public field `:12`, `IsEmpty` `:14`, `EquipmentCode` `:16`, `Code { get; private set; }` `:18` built inside `CreateFrom` `:54-85`, `Color1/Color2` plain setters `:30-32`, `Race` `:34`, 8-argument `CreateFrom(string, BodyProperties, bool, bool, uint, uint, FormationClass, int)` `:87`, `CreateEmpty()` `:145`; `CharacterImageIdentifier(CharacterCode)` reads only `characterCode?.Code` (dump `Core/TaleWorlds.Core/TaleWorlds.Core.ImageIdentifiers/CharacterImageIdentifier.cs:5-7`) | `TaleWorlds.Core.CharacterCode.cs` |
| `public static CharacterCode GetCharacterCode(CharacterObject character, bool useCivilian = false)` `:2850`; `public static bool GetMapScreenActionIsEnabledWithReason(out TextObject disabledReason)` `:2387` (prisoner, raft, mission, encounter) | `TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper.cs` |
| `SandBoxUIHelper.GetCharacterCode(CharacterObject character, bool useCivilian = false)` `:365` | `SandBox.ViewModelCollection.SandBoxUIHelper.cs` |
| `PartyCharacterVM` ctor `:1091` sets `Side`, `Type`, then `Troop` `:1099` (its setter calls `GetCharacterCode(value, Type, Side)` `:175`), then `IsPrisoner` `:1103`; `private CharacterCode GetCharacterCode(CharacterObject, PartyScreenLogic.TroopType, PartyScreenLogic.PartyRosterSide)` `:1476`; `PartyRosterSide { None = 99, Right = 1, Left = 0 }` | `...Party.PartyCharacterVM.cs`, `...Party.PartyScreenLogic.cs` |
| `ClanPartyItemVM` (namespace `...ViewModelCollection.ClanManagement`): 7-argument ctor `:1040` ends with `RefreshValues()` `:1116`; `RefreshValues` `:1119-1123` calls `public void UpdateProperties()` `:1125`, which resets `Name` `:1147` and `PartyWageSubTitleText` `:1161`; properties `ShouldPartyHaveExpense` `:394`, `ExpenseItem` `:514`, `Name` `:684` | `...ClanManagement.ClanPartyItemVM.cs` |
| `ClanPartiesVM` (namespace `...ClanManagement.Categories`): ctor runs `RefreshPartiesList()` `:387` then `RefreshValues()` `:388`; `RefreshValues` reruns `RefreshPartiesList` `:401` and every row's `RefreshValues` `:402-413`; `RefreshPartiesList` `:425` clears the three lists `:427-429`; `private void OnPartySelection(ClanPartyItemVM)` `:555` | `...ClanManagement.Categories.ClanPartiesVM.cs` |
| v1.4.8 SandBox prefab binds the wage line: `IsHidden="@ShouldPartyHaveExpense"` `:54`, `Text="@PartyWageSubTitleText"` `:58`, wage cap panel `IsVisible="@ShouldPartyHaveExpense"` `:343` | `<game>\Modules\SandBox\GUI\Prefabs\Clan\ClanPartiesRightPanel.xml` |
| `OrderOfBattleHeroItemVM(Agent agent)` `:275` sets `ImageIdentifier` `:282` and calls `RefreshValues`, never `RefreshInformation` `:327`; `OrderOfBattleVM` builds heroes with `new OrderOfBattleHeroItemVM(heroAgent)` `:746` | `...OrderOfBattle.OrderOfBattleHeroItemVM.cs`, `...OrderOfBattleVM.cs` |
| `MapScreen`: `Instance` `:257`, `EncyclopediaScreenManager` `:284`, `IsEscapeMenuOpened` `:286`, `IsInMenu` `:292`, `IsInBattleSimulation` `:325`, `IsInArmyManagement` `:331`, `IsMarriageOfferPopupActive` `:339`, `IsMapCheatsActive` `:341`, `IsMapIncidentActive` `:343`, `IsHeirSelectionPopupActive` `:345`, `IsOverlayContextMenuEnabled` `:347`; vanilla map-action guard `:1355`; `Campaign.CurrentMenuContext` `:403` | `SandBox.View.Map.MapScreen.cs`, `...Campaign.cs` |
| `MapBarVM.Initialize` `:237` sets the refresh countdown to 2 s `:240` and runs `OnRefresh` `:251` (`MapInfo.Refresh` `:266`); later refreshes every 0.2 s (0.1 s fast-forward) `:278`; `MapInfoVM.CreateItems` `:142` clears `SecondaryInfoItems` `:149-153` and is called only from the ctor `:138` | `...Map.MapBar.MapBarVM.cs`, `...MapInfoVM.cs` |
| `public static void ApplyPlayerRelation(Hero gainedRelationWith, int relation, bool affectRelatives = true, bool showQuickNotification = true)` `:30-33` never reads `affectRelatives` | `TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.cs` |
| `ItemObject.NotMerchandise { get; private set; }` `:154`, set from XML `is_merchandise` `:428-432` (CraftedItem) and `:498-502` (Item); vanilla inflows skip it (dump `WorkshopsCampaignBehavior.cs:993`, `DefaultTournamentModel.cs:101`) | `TaleWorlds.Core.ItemObject.cs` |
| `private bool conversation_tavernmaid_offers_food_on_condition()` `:729-820`: seven culture branches (vlandia, empire, khuzait, aserai, sturgia, battania, nord) set `DRINK` and `FOOD`, none for any other culture; `MBTextManager.SetTextVariable(string, TextObject, bool sendClients = false)` `:178` | `SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.cs`, `TaleWorlds.Localization.MBTextManager.cs` |
| `ConversationTag` is abstract (`StringId`, `IsApplicableTo(CharacterObject)`); `ConversationManager()` `:227` calls `InitializeTags()` `:239`, which `Activator.CreateInstance`s every `ConversationTag` subclass in the active game assemblies and `Dictionary.Add`s its `StringId`; `FindMatchingTextOrNull(string, CharacterObject)` `:983` scores variations by tag | `...Conversation.Tags.ConversationTag.cs`, `...Conversation.ConversationManager.cs` |
| `GameTextManager.LoadFromXML` splits a row id on `.` into text id and variation and reads `<tags><tag tag_name weight/>`; `FindText(id, variation)` returns `ERROR: Text with id <id> doesn't exist! Variation: <v>` when missing (dump `GameTextManager.cs:91`); `BecomeKingSceneNotificationItem.TitleText` `:23` reads `str_become_king_empire` for culture `empire` `:30`, else `str_liege_title[_female]` with the culture id `:34`; `LordConversationsCampaignBehavior.GetLiegeTitle` uses `FindMatchingTextOrNull` (dump `:95`, `:97`) | `TaleWorlds.Core.GameTextManager.cs`; dump `Campaign/.../SceneInformationPopupTypes/BecomeKingSceneNotificationItem.cs` |

### Conventions that bind this WP

- **ADR-007 adapters**: services never touch sealed TaleWorlds types; engine reads live in `Main/Adapters` or in
  `Hooks`. `MapMenuGate` and the `ItemPoolAdapter` predicate follow this.
- **ADR-002 thin entry points** (under 150 lines, delegate); **ADR-003** no `#region`; **ADR-004** no
  `[Obsolete]`; **ADR-005** no `#if DEBUG` outside IoC registration.
- **TDD**: new work (Steps B3 refuge half, C6, C7) writes the failing test first. Ported commits bring their
  1.5.x tests.
- **D2 module rule** (`docs/features/composition-root.md`, from WP105): a 1.5.x feature with a `*Module.cs`
  arrives as that file plus one line appended at the END of `FeatureModules.All`; never `TryPatchCategory` it in
  `SubModule.cs`. `OwnsSaveData => true`, `GameModels`, `RegisterCampaignObjects` or anything from
  `ClientComposition`/`Client/` is a STOP. `TavernSpecialitiesModule` declares only one `PatchCategoryDecl`.
- **MCM json2 rule**: rename a persisted setting, never flip its default. Removing a setting (A4) is safe: MCM
  ignores the stale entry.
- **`.claude/rules/harmony-patches.md`**: `MovementOrder` postfixes go through the shared deferred category (not
  relevant here: no movement-order patch). A failed `[HarmonyPatch]` target stops its whole category, which
  `PatchCategoryApplier` logs.
- **D9**: delete every `[TestCategory("RequiresGame")]` and `[TestCategory("RequiresGameIL")]` line in ported
  tests; keep `BindingVerification`.
- **Patch numbers**: keep the 1.5.x numbers (`Patch111_TavernSpecialities`).
- **Bookkeeping** (per-port procedure): drop `CHANGELOG.md`, `docs/reviews/lessons/*`,
  `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md`. `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  is auto-generated: drop its hunks (WP137 regenerates the snapshot, as WP112 does). Keep
  `reflection-sites.md` rows (the binding gate's data source), the harmony patch registry, and each feature's own
  `docs/features/*.md`. Keep the two `.claude/rules` lines that document a mechanism this WP ports
  (`gui-ui.md` in C2, `csharp-patterns.md` in D2).
- **No em or en dash** in anything you write (docs, commit bodies, comments you add).

## Commands you will need

| Purpose | Command (prefix every one with `cd /c/Users/mikew/source/repos/taom-port/133 &&`) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 \| tail -3` | `0 Error(s)` (the BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 \| tail -5` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>" 2>&1 \| tail -5` | `Failed: 0` |
| Data | `python tools/validate_moduledata.py --json C:/Users/mikew/source/repos/taom-port/133-data-<label>.json > /dev/null; echo "exit=$?"` | no new ERROR vs Step 0.7 (compare command in 0.7) |
| Tool tests | `python -m unittest discover -s tools/tests -t . 2>&1 \| grep -E '^(Ran \|OK\|FAILED\|FAIL: \|ERROR: tools)'` | no new failure vs Step 0.7, `Ran` = P0 plus the tests added so far (about 4 minutes) |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links`, `orphan_features`, `missing_features` equal Step 0.7; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install. Never
`./build.ps1`. Never two builds in one worktree.

## Scope

**In scope** (the only files you modify): the files listed in each step's "Stage" list, nothing else.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are NOT edited by this WP. No
step needs them: DryIoc resolves the changed constructors, `RefugeIoC.InitializePatchStatics` (feature file)
initialises the new patch, the module runner applies `Patch111`, and the engine discovers the conversation
tags. If a build or test failure seems to need one of them: STOP.

**Out of scope** (do not touch, even though they look related):
- The 1.5.x Fiefs map-bar button (`Patch36_MapNavigationElements`, `TaomFiefsNavigationElement`, `FiefHubOpener`,
  `FiefHubHostAdapter`, `MapBar.xml` brushes, its strings) and the deletion of `FiefManagementNavItemVM`.
- Tournament prize bands, the armour acquisition gate, the troll-gear Armory edits, `CREATURE_GEAR_OBTAINABLE`,
  `tools/lock_creature_gear.py`, `tools/taom_schema.py` (all from `bc39f6e4d`).
- `CustomAttacksUtils.cs` comments and `CustomAttacksUtilsTests.cs` (from `b2e387db7`), `CultureRaceConsistencyTests.cs`
  and the Armory snapshot (from `30003668b`), Telchar's Mark files, `lord-banners.md`, `crown-guard.md`,
  `dale-lord-expansion.md` (1.5.x-only).
- `docs/reference/taleworlds-api-snapshot/patch-targets.md` (regenerated by WP137).
- The game install, the main checkout's `.serena/project.yml` and its `stash@{0}`.

## Git workflow

- Create the worktree from the main checkout (Step 0.1); work and commit only there, on
  `port148/133-campaign-fixes-inventory-ui`. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
- Git only through the Bash tool (D19). Stage explicit paths (`git add <paths>`, never `-A`, `-u` or `.`).
- Per-port procedure: apply a 1.5.x commit with `git cherry-pick -n <sha>` only; never let cherry-pick,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit (`GIT_EDITOR=true` on this laptop would commit
  the 1.5.x message unedited). After staging, if `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)"`
  succeeds, run `git cherry-pick --quit` (keeps index and worktree). Then run `git commit -F <msgfile>` in the
  NEXT sequential Bash call: never in the same command, never in a parallel call, never through PowerShell.
- Every step's apply command defines a `drop` helper: for each path, restore it from `HEAD` if `HEAD` has it,
  else remove it from index and worktree. It drops bookkeeping hunks and resets files this plan rewrites by hand:

  ```bash
  drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }
  ```

- Commit message template (each step gives the full text; wrap at 72; no em or en dashes; no Co-Authored-By
  or "Generated with" line; each trailer on ONE line):

  ```
  <type>(<scope>): v2.0.28 - <description>        (at most 72 characters)

  <body for 1.4.x readers>

  Research: ...      Not-tested: ...      Save-compat: ... (when relevant)
  Ported-from: <40-hex sha>
  ```

- Before every commit run: `git diff --cached --name-only` (must equal the step's Stage list),
  `git diff --cached | grep -c '^+.*\(<<<<<<<\|>>>>>>>\)'` (must print `0`) and
  `git diff --cached -U0 | grep -nE '^\+.*(v1\.5\.5|ClientOnly|ClientComposition|Client/)'` (must print nothing).
  Two hits are expected and fixed later, on purpose (editing them early would conflict with the next pick's
  hunk): `the v1.5.5 stealth set` in `docs/features/quick-actions.md` in A1 to A5 (A6 fixes it), and
  `(v1.4.8 and v1.5.5)` in the map-bar mixin in C1 (C2 fixes it). Any other hit: fix it before committing.
- After each commit: Build, then the step's Filtered command. Full Test at the end of each part.

## Steps

### Step 0: Set up the worktree, check the prerequisites, record the baseline

**0.1 Worktree.**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/133 -b port148/133-campaign-fixes-inventory-ui bannerlord-1.4.5
```

**Verify**: prints `Preparing worktree (new branch 'port148/133-campaign-fixes-inventory-ui')`. Branch or
folder already exists (and you are not resuming this plan): STOP. Resuming: `git log --oneline bannerlord-1.4.5..HEAD`
shows which steps are done; continue after the last one.

**0.2 Drift check**: the command in the header. Read every change; let 0.5 decide.

**0.3 Prerequisites (WP104, WP105, WP112, WP125).**

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && ls Main/Composition/FeatureModules.cs Main/PatchCategoryApplier.cs Main/PatchCategoryIndex.cs TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Core/MainPartyNullCheckScanTests.cs Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs TAOM.Tests/Features/ShadowfaxScreens/Party/PartyFastOpenBindingTests.cs && \
grep -cF 'internal static readonly TaomFeatureModule[] All' Main/Composition/FeatureModules.cs; \
grep -cF 'private bool TryPatchCategory(string category)' Main/SubModule.cs; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static IEnumerable<string> CSharpFiles(params string[] roots)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'CodeRoots = { "Main" }' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'internal static PatchCategoryIndex Build(Assembly assembly)' Main/PatchCategoryIndex.cs; \
grep -cF 'internal PatchCategoryDecl(string category, ApplyPhase phase)' Main/Composition/FeatureDecls.cs; \
grep -cF '[DataRow("Main/Adapters/PlayerEquipmentAdapter.cs")]' TAOM.Tests/Core/MainPartyNullCheckScanTests.cs; \
grep -cF 'var party = Campaign.Current?.MainParty?.Party;' Main/Adapters/PlayerEquipmentAdapter.cs; \
grep -cF "before Patch23's postfix sets Color1 and" Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs
```

**Verify**: the seven paths are listed, then `1` on each of the ten lines. Anything else: STOP ("WP104, WP105,
WP112 or WP125 not merged").

**0.4 Collision check** (nothing this WP creates may exist yet):

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && ls Main/Core/Validation/EnumNames.cs Main/Adapters/MapMenuGate.cs Main/Features/TavernSpecialities Main/Features/LiegeTitles Main/Features/Refuge/Hooks/RefugeClanRowRefreshPatch.cs Main/Features/BannerColorPersistence/Hooks/ClanColoredCharacterCode.cs 2>&1 | grep -c "No such file"; \
git grep -n '"Patch111_' -- Main | wc -l; git grep -n 'RefreshPanel\|IsMarketCandidate\|: ConversationTag' -- Main | wc -l
```

**Verify**: `6`, `0`, `0`. Otherwise STOP (another WP landed part of this one).

**0.5 Anchors.** Each must print `1`:

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && while IFS='|' read -r f a; do printf '%s  %s\n' "$(grep -cF "$a" "$f")" "$f"; done <<'EOF'
Main/Features/QuickActions/QuickActionsService.cs|affected = _equipment.TryUnequipAllPlayerSlots();
Main/Adapters/InventoryVMAdapter.cs|public InventoryVMAdapter(IPlayerEquipmentAdapter playerEquipment, IModLogger logger)
Main/Adapters/InventoryScreenAdapter.cs|try { _active.RefreshValues(); }
Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs|"UpdateCurrentCharacterIfPossible",
Main/Features/TaomSettings.cs|public bool SellLowValueEquipped { get; set; } = false;
Main/Features/BannerBearers/BannerBearerConfigProvider.cs|Enum.TryParse<FormationClass>(entry, ignoreCase: true, out _)
Main/Features/BannerColorPersistence/Hooks/CampaignUIHelper_GetCharacterCode_Patch.cs|public static void Postfix(CharacterCode __result, TaleWorlds.CampaignSystem.CharacterObject character)
Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs|private static void SuppressWagePanel(ClanPartyItemVM row)
Main/Features/Refuge/RefugeIoC.cs|RefugeClanScreenPatch.Initialize(refugeService);
Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs|if (_baseInitialized && !_itemAdded && ViewModel is MapInfoVM mapInfo)
Main/Adapters/CommanderLordAdapter.cs|ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: true, showQuickNotification: false);
tools/generate_new_faction_kingdoms.py|def upsert(text, close_tag, payload, marker):
Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs|if (__instance.IsMapIncidentActive) return;
Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs|if (screen.IsMapIncidentActive)
Main/Adapters/ItemPoolAdapter.cs|items.Add(new ItemPoolItem(item.StringId, attribCulture, prefixCulture));
Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json|"lothlorien": "archers_flag_t1",
Main/_Module/ModuleData/module_strings.xslt|<xsl:template match="string[@id='str_culture_description.sturgia']"/>
TAOM.Tests/Features/Refuge/Patch75BindingTests.cs|public void BothPatches_CarryThePatch75Category()
EOF
```

**Verify**: every line starts with `1`. A `0`: open the file, compare with "Current state"; a cosmetic move is
fine (find the new spot), a changed behaviour is a STOP.

**0.6 Laptop Armory (D20)**, read-only:

```bash
A="$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory"; grep -o '<Version value="[^"]*"' "$A/SubModule.xml"; ls "$A/ModuleData/Monsters/LOTR/lotr_monster_elk.xml"
```

**Verify**: the version is not `v2.0.26` and the elk file is listed (WP101 Part A done). Otherwise STOP before
any step that stages a `Main/_Module/ModuleData` file (B2, C7, D1, D2): "BLOCKED on WP101 Part A (D20)".
Parts A, B1, B3 and C1 to C6 may still run.

**0.7 Baseline** (record every number for the hand-off):

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && dotnet restore TAOM.sln > /dev/null && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3 && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -5
cd /c/Users/mikew/source/repos/taom-port/133 && python -m unittest discover -s tools/tests -t . 2>&1 | grep -E '^(Ran |OK|FAILED|FAIL: |ERROR: tools)'; python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5); python tools/validate_moduledata.py --json C:/Users/mikew/source/repos/taom-port/133-data-base.json > /dev/null; echo "exit=$?"
```

**Verify**: `0 Error(s)`; `Failed: 0` with total T0 (if the main checkout's
`C:\Users\mikew\source\repos\TAOM\plans\README.md` records a test count for the last merged WP, T0 must equal
it; a mismatch is a STOP; read it, never edit it); record the tool tests' `Ran` as P0 with their `FAIL:` and
`ERROR:` names (a baseline failure is not a STOP: the base `1b00d1acd` itself gave 1,501 tests with 3 failures
and 1 error on the laptop before WP101); record the lint values and `exit=` (the validator exits 1 on the laptop's
existing errors; only new rows count). Data
comparison command used after each data step (prints new ERROR rows; nothing means pass):

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && python -c "import json,sys; k=lambda p:{(i['code'],i['file'],i.get('entry_id')) for i in json.load(open(p,encoding='utf-8')) if i['severity']=='ERROR'}; print(sorted(k(sys.argv[2])-k(sys.argv[1])))" C:/Users/mikew/source/repos/taom-port/133-data-base.json C:/Users/mikew/source/repos/taom-port/133-data-<label>.json
```

Expected after each data step: `[]`.

---

## Part A: inventory fixes (`inventory-sweep-2026-10-23` and `issue-917-quickactions-unequip`)

Order is fixed: A2 and A3 need A1's bindings; A6 needs A1 to A5.

### Step A1: Unequip All through the open screen (`1888b6f28`, #917)

Method: cherry-pick. Simulated (chain, on the tree before WP112): unmerged `reflection-sites.md`, two lessons
files, `Main/Adapters/PlayerEquipmentAdapter.cs` and `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs`. WP112 clears
the last two: it creates the scan test and takes `2c09a4145`, which brings the adapter (blob `51f6bba1d`) to this
commit's parent blob (`035bd913b`), so both now apply from the pick.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 1888b6f28; echo "exit=$?"; drop docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/testing-qa.md docs/reference/taleworlds-api-snapshot/reflection-sites.md; git diff --name-only --diff-filter=U
```

**Verify**: `exit=1`, then no path remains unmerged (the last command prints nothing).

Resolve and edit:
1. `Main/Adapters/PlayerEquipmentAdapter.cs` and `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs` need no edit: the
   pick deletes the method and removes the adapter's `AliasedSites` row, which pinned that method's main-party read.
   **Verify**: `grep -c "TryUnequipAllPlayerSlots\|PartyBase\|StripEquipment" Main/Adapters/PlayerEquipmentAdapter.cs` prints `0`.
2. `Main/Adapters/InventoryVMAdapter.cs`: replace `(v1.5.5 :4377)` with `(v1.4.8 :4377)`, and
   `(InventoryLogic.cs:891, :923)` with `(InventoryLogic.cs:890, :922)`.
3. `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: directly after the row
   ``| `…ViewModelCollection.Inventory.SPInventoryVM` | `_inventoryLogic` | field | `InventoryScreenAdapter.cs:32` | EquipPresets transfer commands |``
   insert:

   ```
   | `…ViewModelCollection.Inventory.SPInventoryVM` | `UpdateRightCharacter` | method | `InventoryVMAdapter.cs:29` | QuickActions paper doll and stats after Unequip All |
   | `…ViewModelCollection.Inventory.SPInventoryVM` | `RefreshInformationValues` | method | `InventoryVMAdapter.cs:30` | QuickActions capacity and Done state after Unequip All |
   ```

   **Verify**: `grep -n "BindPanelRefresh(\"" Main/Adapters/InventoryVMAdapter.cs` prints lines 29 and 30; if
   not, use the printed numbers in both the rows above and the two `ReflectionSiteBindingTests` labels.
4. `docs/reviews/rca-quickactions-unequip-2026-10-19.md` (new, keep): replace `(v1.5.5 :3488-3497)` with
   `(v1.4.8 :3488-3497)`, and insert after the title line a blank line and:
   `> Recorded on the 1.5.x line; ported to the 1.4.x line by plan 133. Commit hashes in this file are 1.5.x commits; the 1.4.x ports are plan 133's Part A commits. The cited engine lines are unchanged on v1.4.8.`

Stage: `git add Main/Adapters/InventoryVMAdapter.cs docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reviews/rca-quickactions-unequip-2026-10-19.md`

Stage list (`git diff --cached --name-only`): `Main/Adapters/IInventoryVMAdapter.cs`,
`Main/Adapters/IPlayerEquipmentAdapter.cs`, `Main/Adapters/InventoryVMAdapter.cs`,
`Main/Adapters/PlayerEquipmentAdapter.cs`, `Main/Features/QuickActions/QuickActionsService.cs`,
`TAOM.Tests/Core/MainPartyNullCheckScanTests.cs`,
`TAOM.Tests/Features/QuickActions/InventoryPanelRefreshBindingTests.cs`,
`TAOM.Tests/Features/QuickActions/QuickActionsServiceTests.cs`, `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`,
`docs/features/quick-actions.md`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`,
`docs/reviews/rca-quickactions-unequip-2026-10-19.md`.

Message file `133-msg-A1.txt`:

```
fix(quickactions): v2.0.28 - Unequip All updates the open inventory

Quick Actions "Unequip All" now moves the main hero's battle and
civilian gear through the open inventory screen's own transfers and
then refreshes the character panel. The gear shows in the item list
at once, the slots empty, and the paper doll, armour, weight,
capacity, the no-saddle warning and the Done button update without
reopening the screen. Cancel and Reset undo it like a manual unequip.

Before, the service wrote the equipment slots directly, so the open
screen kept showing the old gear. The panel refresh calls the two
private methods the game runs when the shown outfit changes, bound
once and pinned by the reflection binding gate. The unreachable
direct-write fallback is removed.

Research: taom-src v1.4.8 SPInventoryVM :4377, :3968, :3840, :3494
Not-tested: in game, Unequip All with the inventory open, Reset, Cancel
Ported-from: 1888b6f2804a2e80337e20b8d906cbc043e91433
```

**Verify** (after the commit): Build `0 Error(s)`; Filtered
`FullyQualifiedName~QuickActions|FullyQualifiedName~InventoryPanelRefreshBindingTests|FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~MainPartyNullCheckScanTests`
gives `Failed: 0`; `FullyQualifiedName~InventoryPanelRefreshBindingTests` alone gives `Passed: 1`.

### Step A2: preset load refreshes the paper doll (`89e7af6c0`, #924)

Method: cherry-pick. Simulated after A1: unmerged Telchar's Mark `Client/...` file (1.5.x-only), the patch
registry (its hunk edits the 1.5.x-only Patch109 section) and `reflection-sites.md`.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 89e7af6c0; echo "exit=$?"; drop Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMAfterTransfer.cs docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing remains unmerged.

Edits:
1. `Main/Adapters/InventoryScreenAdapter.cs`: `(v1.5.5 SPInventoryVM.cs:3182)` becomes `(v1.4.8 SPInventoryVM.cs:3182)`.
2. `TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs`: delete the line `[TestCategory("RequiresGame")]`;
   `v1.5.5 SPInventoryVM.cs:3475-3486` becomes `v1.4.8 SPInventoryVM.cs:3475-3486`.
3. `reflection-sites.md`: in the two rows A1 added, the last cell becomes
   `QuickActions and EquipPresets paper doll and stats after a batch transfer` and
   `QuickActions and EquipPresets capacity and Done state after a batch transfer`.

Stage: `git add Main/Adapters/InventoryScreenAdapter.cs TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs docs/reference/taleworlds-api-snapshot/reflection-sites.md`

Stage list: `Main/Adapters/IInventoryScreenAdapter.cs`, `Main/Adapters/InventoryScreenAdapter.cs`,
`Main/Adapters/InventoryVMAdapter.cs`, `TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs`,
`docs/features/equip-presets.md`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`.

Message file `133-msg-A2.txt`:

```
fix(equippresets): v2.0.28 - preset load refreshes the paper doll

Loading an equipment preset with the inventory open now updates the
paper doll, armour, weight, capacity, the no-saddle warning and the
Done button at once, and a row whose last item the preset equipped
leaves the list instead of staying at a count of 0.

The load called the inventory's label refresh, which on v1.4.8
redraws text only. It now runs the same panel refresh as Quick Actions
Unequip All through one shared helper, InventoryVMAdapter.RefreshPanel:
the paper doll and the information panel, then the removal of empty
rows.

Research: taom-src v1.4.8 SPInventoryVM :3182, :3437, :3475-3486
Not-tested: in game, preset load with the inventory open
Ported-from: 89e7af6c0b9e7e3911afcc25a462eaf2736538b0
```

**Verify**: Build; `FullyQualifiedName~InventoryPanelRefreshTests` gives `Passed: 1`, `Failed: 0`;
`FullyQualifiedName~EquipPresets` gives `Failed: 0`.

### Step A3: batch sells remove sold-out rows (`eb89a3e53`, #926)

Method: cherry-pick; simulated clean after A2.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && git cherry-pick -n eb89a3e53; echo "exit=$?"; git diff --name-only --diff-filter=U
```

**Verify**: `exit=0`, nothing unmerged. No edits.

Stage list: `Main/Adapters/IInventoryVMAdapter.cs`, `Main/Adapters/InventoryVMAdapter.cs`,
`TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs`, `docs/features/quick-actions.md`.

Message file `133-msg-A3.txt`:

```
fix(quickactions): v2.0.28 - batch sells remove sold-out rows

Quick Actions Sell Damaged and Sell Low Value now remove a row from
the item list as soon as they sell its whole stack. Before, the row
stayed at a count of 0 until another row was clicked.

The batch sell passes cameFromTradeData, which skips the game's own
prune of empty rows (v1.4.8 SPInventoryVM.cs:3565-3568). The refresh
that Quick Actions runs after every batch now ends with that prune,
once per batch, through the helper the preset load uses.

Research: taom-src v1.4.8 SPInventoryVM ProcessSellItem :3565-3568
Not-tested: in game, Sell Low Value on a full stack
Ported-from: eb89a3e53a9dfa30c95cf2d057b568f24b3956bb
```

**Verify**: Build; `FullyQualifiedName~InventoryPanelRefreshTests` gives `Passed: 2`.

### Step A4: drop the "include equipped" sell options (`cd999f917`, #925)

Method: cherry-pick. Simulated after A3: unmerged `QuickActionsSettingsProvider.cs` (1.4.x reads
`TaomSettings.Instance?.` where 1.5.x caches `Settings?.`), `SettingsFingerprintTests.cs` and two co-op docs
(counts differ); the review prompt file is 1.5.x bookkeeping.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n cd999f917; echo "exit=$?"; drop docs/reviews/codex-prereview-inventory-sweep-2026-10-23.prompt.md Main/Features/QuickActions/QuickActionsSettingsProvider.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/bannerlord-together-compat.md docs/features/coop-interop.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits (relative numbers: earlier WPs may have moved the absolute ones; the values in brackets are the
`origin/bannerlord-1.4.5` values):
1. `Main/Features/QuickActions/QuickActionsSettingsProvider.cs`: delete the two property lines that name
   `SellDamagedEquipped` and `SellLowValueEquipped` (whatever their shape):
   `sed -i '/SellDamagedEquipped\|SellLowValueEquipped/d' Main/Features/QuickActions/QuickActionsSettingsProvider.cs`.
   **Verify**: `git grep -n "SellDamagedEquipped\|SellLowValueEquipped\|IsEquipped" -- Main TAOM.Tests` prints nothing.
2. `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`: in
   `AssertSplit(typeof(TaomSettings), reflected: R, covered: C);` replace R by R minus 2 [229 becomes 227];
   leave `covered` alone (both options were player-local).
3. `docs/features/coop-interop.md`, bullet "No MCM settings parity": the total `**N** settings` minus 2
   [243 becomes 241], "the split is X in `TaomSettings`" minus 2 [229 becomes 227], "The E excluded" minus 2
   [59 becomes 57]; the simulation-relevant count is unchanged.
4. `docs/features/bannerlord-together-compat.md`: "TAOM's N MCM settings" minus 2 [243 becomes 241].

Stage: `git add Main/Features/QuickActions/QuickActionsSettingsProvider.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md`

Stage list: `Main/Adapters/IInventoryItemAdapter.cs`, `Main/Adapters/InventoryItemAdapter.cs`,
`Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/Features/QuickActions/IQuickActionsSettingsProvider.cs`,
`Main/Features/QuickActions/QuickActionsService.cs`, `Main/Features/QuickActions/QuickActionsSettingsProvider.cs`,
`Main/Features/TaomSettings.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`,
`TAOM.Tests/Features/QuickActions/QuickActionsServiceTests.cs`, `docs/features/bannerlord-together-compat.md`,
`docs/features/coop-interop.md`, `docs/features/quick-actions.md`.

Message file `133-msg-A4.txt`:

```
fix(quickactions): v2.0.28 - drop the "include equipped" sell options

The MCM options "Sell Damaged Equipped" and "Sell Low Value Equipped"
are gone. They never did anything: the two sells take their items from
the inventory list, and gear a hero wears is never in that list, so
turning either option on sold nothing extra. Quick sells still never
sell equipped gear; run Unequip All first, then the sell.

Saved MCM settings load as before: the two old entries are ignored and
drop out on the next save. Both options were player-local, so the
co-op settings fingerprint is unchanged.

Save-compat: MCM file only; no save data involved
Not-tested: in game, the two options gone from MCM
Ported-from: cd999f9176bd9ce8600a87ea96d86162775db849
```

**Verify**: Build; `FullyQualifiedName~QuickActions|FullyQualifiedName~SettingsFingerprint` gives `Failed: 0`.

### Step A5: the inventory doll keeps the clan colours (`97426dcd0`, #973)

Method: cherry-pick. The 1.4.x patch file equals the 1.5.x pre-fix file. Simulated: unmerged
`banner-color-persistence.md` (1.4.x text differs) and a lessons file; the review prompt and `patch-targets.md`
are dropped.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 97426dcd0; echo "exit=$?"; drop docs/reviews/codex-prereview-banner-color-doll-2026-10-23.prompt.md docs/reviews/lessons/harmony-il.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/features/banner-color-persistence.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits:
1. `Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs`:
   `(v1.5.5 SPInventoryVM.cs:4421)` becomes `(v1.4.8 SPInventoryVM.cs:4421)`. Every other line number in its
   summary holds on v1.4.8 (table above).
2. `TAOM.Tests/Features/BannerColorPersistence/SPInventoryVM_ArmorColor_PatchTests.cs`: delete
   `[TestCategory("RequiresGame")]`; `(v1.5.5 :4421)` becomes `(v1.4.8 :4421)`.
3. `docs/features/banner-color-persistence.md` (the 1.4.x text):
   - In the Patch23 tree, `GetCharacterCode (×2), SPInventory, PartyVM,` becomes
     `GetCharacterCode (×2), SPInventoryVM.UpdateCharacterArmorColor, PartyVM,`.
   - Directly after the bullet starting `- **CharacterCode is mutable**`, insert:
     `- **Inventory doll on its colour writer**: the inventory postfix sits on the private `SPInventoryVM.UpdateCharacterArmorColor`, the only vanilla repaint of the doll's colours after the screen is built. It runs through `UpdateRightCharacter`: opening the screen, the outfit toggle, Reset, a character switch, and TAOM's `InventoryVMAdapter.RefreshPanel` after an EquipPresets load and after every Quick Actions batch (sells and Unequip All). An ordinary equip refreshes the doll without it and writes no colour, so the clan colours stay. It skips stealth mode, where vanilla paints the outfit one fixed colour. The class keeps its old name, `SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch`, because `Main/SubModule.cs` calls its `Initialize` (#973).`
   - In the bullet starting `- **MobilePartyVisual patched manually**`, replace
     `Harmony's category system cannot auto-discover private methods; it's patched manually via `AccessTools` in `OnGameInitializationFinished`.`
     with
     `It is patched by hand from `ManualPatchApplicator`, not through a category. (A category class can target a private method through `TargetMethod()`, as the inventory patch does.)`
   - Key files: after the `OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs` row insert
     ``| `Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs` | Patch23: inventory doll clan colours, a postfix on `SPInventoryVM.UpdateCharacterArmorColor` (the class name is historical, #973) |``
     and in the `TAOM.Tests/Features/BannerColorPersistence/` row put the file and method counts from the
     command below [7 test files, 38 test methods].
   - Tests table: after the `Clan_UpdateBannerColor_PatchTests.cs` row insert
     ``| `SPInventoryVM_ArmorColor_PatchTests.cs` | the inventory postfix's target, clan colours, the stealth guard both ways, colours not wanted (#973) |``;
     the sentence `N files, M `[TestMethod]` as of <date>.` takes the same counts and today's date.
   - Replace `Harmony patches themselves are not unit-testable (require live game) per ADR-008.` with
     `Most Harmony patches here are tested only through their services (ADR-008). A postfix whose inputs can be built bare is tested directly: `SPInventoryVM_ArmorColor_PatchTests` drives the inventory postfix on uninitialized engine objects.`
   - Changelog, first entry: `- <today>: the inventory doll keeps the clan colours on every refresh, not only a character switch, and keeps vanilla's stealth colour in stealth mode (#973). The postfix moved from `UpdateCurrentCharacterIfPossible` to `UpdateCharacterArmorColor`. Ported from the 1.5.x line by plan 133.`

   Count command: `cd /c/Users/mikew/source/repos/taom-port/133 && ls TAOM.Tests/Features/BannerColorPersistence/*.cs | wc -l; cat TAOM.Tests/Features/BannerColorPersistence/*.cs | grep -c '\[TestMethod\]\|\[DataTestMethod\]'`

Stage: `git add Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs TAOM.Tests/Features/BannerColorPersistence/SPInventoryVM_ArmorColor_PatchTests.cs docs/features/banner-color-persistence.md`

Stage list: those three paths only.

Message file `133-msg-A5.txt`:

```
fix(banner): v2.0.28 - inventory doll keeps the clan colours

With Banner Color Persistence on, the paper doll in the inventory now
keeps the clan's colours every time it redraws. Before, only switching
to another character showed them: opening the inventory, toggling the
outfit, Reset, loading an equipment preset and the Quick Actions all
repainted the doll in the kingdom's colours. In stealth mode the doll
keeps the game's own stealth colour.

The postfix moves from UpdateCurrentCharacterIfPossible to the private
UpdateCharacterArmorColor, the one step that paints the doll, which
every UpdateRightCharacter call reaches.

Research: taom-src v1.4.8 SPInventoryVM :4377-4381, :4421-4432
Not-tested: in game, doll colours on open, outfit toggle, preset load
Ported-from: 97426dcd07987a680d7a0e19a55cff634cffc2f6
```

**Verify**: Build; `FullyQualifiedName~SPInventoryVM_ArmorColor_PatchTests` gives `Passed: 5`;
`FullyQualifiedName~BannerColorPersistence|FullyQualifiedName~HarmonyPatchBindingTests` gives `Failed: 0`.

### Step A6: the inventory docs catch up (`399a7fe27`)

Method: cherry-pick; simulated after A5: unmerged feature map, registry and a lessons file.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 399a7fe27; echo "exit=$?"; drop docs/reference/feature-map.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/testing-qa.md docs/reference/harmony-patch-registry.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits:
1. `Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs`: `(v1.5.5 SPInventoryVM.cs:3163)` becomes
   `(v1.4.8 SPInventoryVM.cs:3163)`. The `Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs` comment names
   `PatchCategoryApplier`, which WP104 brought (Step 0.3): keep it as is.
2. `docs/features/equip-presets.md`: `(v1.5.5 `:3163`)` becomes `(v1.4.8 `:3163`)`.
3. `docs/features/quick-actions.md`: `and it leaves the v1.5.5 stealth set alone.` becomes
   `and it leaves the stealth set alone.` (v1.4.8 has stealth sets: `InventoryLogic.InventorySide.StealthEquipment`).
4. `docs/reference/harmony-patch-registry.md` (the 1.4.x text):
   - `## Patch23_BannerColorPersistence`: after its `**Target:**` line insert a blank line and
     `**Inventory doll (#973):** the `SPInventoryVM` postfix targets the private `UpdateCharacterArmorColor()` (v1.4.8 `:4421`), the vanilla repaint of the doll's colours that every `UpdateRightCharacter` call reaches (open, outfit toggle, Reset, character switch, TAOM's `InventoryVMAdapter.RefreshPanel`). It skips stealth mode when the shown set is a stealth set, as vanilla does. Until #973 it targeted `UpdateCurrentCharacterIfPossible`, and the class keeps that name because `SubModule.cs` calls its `Initialize`.`
   - `## Patch33_EquipPresets`: replace
     `Hot-ish path (fires every transaction tick while inventory is open): adapter + logger are lazily static-cached.`
     with
     `Fires when the VM is built (v1.4.8 `SPInventoryVM.cs:3163`); the screen never calls it again, and the Load path stopped calling it in #924 (it now runs `InventoryVMAdapter.RefreshPanel`). Adapter + logger are lazily static-cached.`

Stage: `git add Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs docs/features/equip-presets.md docs/features/quick-actions.md docs/reference/harmony-patch-registry.md`

Stage list: `Main/Features/BannerColorPersistence/Hooks/Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs`,
`Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs`, `docs/features/equip-presets.md`,
`docs/features/quick-actions.md`, `docs/reference/harmony-patch-registry.md`,
`docs/reviews/rca-quickactions-unequip-2026-10-19.md`.

Message file `133-msg-A6.txt`:

```
docs: v2.0.28 - inventory docs catch up with the fixes

No change in game. The Equipment Presets, Quick Actions and Banner
Color Persistence docs and the patch registry now match the inventory
fixes. Two wrong statements are corrected: the inventory label refresh
fires once, when the screen is built (v1.4.8 SPInventoryVM.cs:3163),
not on every transaction; and a renamed engine method does not make
Harmony skip a patch silently, it stops the whole patch category,
which PatchCategoryApplier logs.

Research: taom-src v1.4.8 SPInventoryVM constructor :3163
Ported-from: 399a7fe277a8874f20b2bb526caba9571818d4d4
```

**Verify**: Build; Docs command: `ai_dashes: 0`, other values equal Step 0.7.

### Part A done criteria

- [ ] Six commits on the branch since the base, subjects as above (`git log --oneline bannerlord-1.4.5..HEAD`).
- [ ] Build `0 Error(s)`; full Test `Failed: 0`, total = T0 + 6 (QuickActionsServiceTests 37 to 34, MainPartyNullCheckScanTests
      -1 row, +1 InventoryPanelRefreshBindingTests, +2 ReflectionSiteBindingTests rows, +2 InventoryPanelRefreshTests, +5
      SPInventoryVM_ArmorColor_PatchTests).
- [ ] `git diff bannerlord-1.4.5...HEAD -U0 -- Main/Adapters Main/Features TAOM.Tests docs/features docs/reviews/rca-quickactions-unequip-2026-10-19.md | grep -n '^+.*v1\.5\.5'` prints nothing (only this part's added lines are checked).
- [ ] `git grep -n "TestCategory(\"RequiresGame" -- TAOM.Tests/Adapters TAOM.Tests/Features/BannerColorPersistence TAOM.Tests/Features/QuickActions` prints nothing.
- [ ] Hand-off: T0, the new total, the six short hashes.

---

## Part B: config lists, banners, portraits and refuge rows

### Step B1: config lists drop blank entries, formations by name only (`b2e387db7`)

Method: cherry-pick. Simulated: unmerged `StrikeNames.cs` (SignatureStrikes is 1.5.x-only) plus bookkeeping.
`CustomAttacksUtils.cs`, its test and the spider RCA merge cleanly but are dropped (comment-only, optional, and
their engine claims were checked on v1.5.x only).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n b2e387db7; echo "exit=$?"; drop CHANGELOG.md Main/Features/SignatureStrikes/Domain/StrikeNames.cs Main/Features/AdvancedCombat/CustomAttacksUtils.cs TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs docs/reviews/LESSONS-LEARNED.md docs/reviews/REVIEW-LOG.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/gamemodels-services.md docs/reviews/lessons/misc.md docs/reviews/rca-nazgul-scream-2026-09-23.md docs/reviews/rca-spider-directional-attacks-2026-06-15.md; git diff --name-only --diff-filter=U; ls Main/Features/SignatureStrikes 2>&1 | head -1
```

**Verify**: nothing unmerged; `ls` reports `No such file or directory`.

Edits:
1. `Main/Core/Validation/EnumNames.cs` summary: replace the two lines
   `/// live value: "1" read as a SignatureStrikes Overhead row (Codex review 114, F2), and a padded,` and
   `/// numeric or combined BannerBearers formation entry never matched the service's name compare, so`
   with `/// live value: a padded, numeric or combined BannerBearers formation entry never matched the` and
   `/// service's name compare, so`. The next line (`/// as the only entry it switched every bearer off ...`) stays.
2. `TAOM.Tests/Core/Validation/EnumNamesTests.cs`: `/// The name-only enum parser shared by SignatureStrikes and BannerBearers. Enum.TryParse accepts`
   becomes `/// The name-only enum parser BannerBearers uses for AllowedFormationGroups. Enum.TryParse accepts`.
3. Re-derive the BannerBearers and DreadAura doc counts (another wave-4 WP may have changed the tests):

   ```bash
   cd /c/Users/mikew/source/repos/taom-port/133 && for d in BannerBearers DreadAura; do for f in TAOM.Tests/Features/$d/*.cs; do printf '%s %s\n' "$(awk '/\[TestMethod\]/{t++} /\[DataRow\(/{d++} END{print t+d}' "$f")" "$f"; done; done
   ```

   `docs/features/banner-bearers.md`: the `N tests:` line is the BannerBearers sum [107] and the per-file
   numbers in the table follow the output [Service 61, ConfigProvider 29, Shipped 13].
   `docs/features/dread-aura.md`: `N tests.` is the DreadAura sum [163] and `DreadAuraConfigProviderTests (N)` [45].
4. Five more comments the commit adds name the 1.5.x-only feature (`... Codex review 130 found in SignatureStrikes`):

   ```bash
   cd /c/Users/mikew/source/repos/taom-port/133 && sed -i 's/ found in SignatureStrikes/ found/' Main/Features/BannerBearers/BannerBearerConfigProvider.cs Main/Features/DreadAura/DreadAuraConfigProvider.cs Main/Features/UncapturableHeroes/UncapturableHeroesConfigProvider.cs TAOM.Tests/Features/DreadAura/DreadAuraConfigProviderTests.cs TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesConfigProviderTests.cs; grep -rl SignatureStrikes Main TAOM.Tests --include=*.cs | wc -l
   ```

   **Verify**: prints `0`.

Stage: `git add Main/Core/Validation/EnumNames.cs TAOM.Tests/Core/Validation/EnumNamesTests.cs docs/features/banner-bearers.md docs/features/dread-aura.md Main/Features/BannerBearers/BannerBearerConfigProvider.cs Main/Features/DreadAura/DreadAuraConfigProvider.cs Main/Features/UncapturableHeroes/UncapturableHeroesConfigProvider.cs TAOM.Tests/Features/DreadAura/DreadAuraConfigProviderTests.cs TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesConfigProviderTests.cs`

Stage list: `Main/Core/Validation/EnumNames.cs`, `Main/Features/BannerBearers/BannerBearerConfigProvider.cs`,
`Main/Features/DreadAura/DreadAuraConfigProvider.cs`,
`Main/Features/UncapturableHeroes/UncapturableHeroesConfigProvider.cs`, `TAOM.Tests/Core/Validation/EnumNamesTests.cs`,
`TAOM.Tests/Features/BannerBearers/BannerBearerConfigProviderTests.cs`,
`TAOM.Tests/Features/DreadAura/DreadAuraConfigProviderTests.cs`,
`TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesConfigProviderTests.cs`, `docs/features/banner-bearers.md`,
`docs/features/dread-aura.md`, `docs/features/uncapturable-heroes.md`.

Message file `133-msg-B1.txt`:

```
fix(config): v2.0.28 - list entries trimmed, formation names only

The DreadAura, UncapturableHeroes and BannerBearers config loaders now
drop a null or blank list entry with a warning and trim a padded one.
Before, a padded name matched nobody without a word, and a padded
excludeHeroIds entry left its hero uncapturable.

BannerBearers' AllowedFormationGroups kept anything Enum.TryParse
accepted (" infantry ", "2", "Infantry, Ranged"), which the service's
name compare never matches: as the only entry it switched every banner
bearer off. It now takes declared FormationClass names only, through
the new TAOM.Core.Validation.EnumNames, stored as the enum prints them.
No change with the shipped configs.

Not-tested: in game (no behaviour change with the shipped configs)
Ported-from: b2e387db7e4e2ed074fceb1edaa33427af29ab9f
```

**Verify**: Build; `FullyQualifiedName~EnumNamesTests` gives `Passed: 10`;
`FullyQualifiedName~BannerBearers|FullyQualifiedName~DreadAura|FullyQualifiedName~UncapturableHeroes` gives `Failed: 0`.

### Step B2: Lindon and Blue Craig troops raise banners (`210943076`, #932)

Requires 0.6 (data file). Method: cherry-pick. Simulated after B1: unmerged `banner-bearers.md` (one block in
"In-game verification still owed"), `new-culture-authoring.md` (1.4.x has no Voice map row),
`lord-banners.md` (1.5.x-only).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 210943076; echo "exit=$?"; drop docs/features/lord-banners.md; git diff --name-only --diff-filter=U
```

**Verify**: exactly `docs/ai-includes/new-culture-authoring.md` and `docs/features/banner-bearers.md` remain.

Resolve and edit:
1. `docs/features/banner-bearers.md`: replace the whole conflict block (from the `<<<<<<<` line to the
   `>>>>>>>` line) with these two lines:

   ```
   - A Lindon formation without a lord raises the Archer's Flag, and a Blue Craig one the orcs' standard.
   - No troll ever raises a banner.
   ```

   Then replace `(`ShippedConfig_MapsEveryInfantryTroopCultureToABanner`; `wild_trolls` is the one exception, its troops being race-gated out)`
   with `(`ShippedConfig_MapsEveryInfantryTroopCultureToABanner`)`.
2. `docs/ai-includes/new-culture-authoring.md`: replace the conflict block with the single row
   ``| **Bearer banner** | `Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json` and `BannerBearerConfig.cs` | modified (+ a `CultureBanners` row; gated by `ShippedConfig_MapsEveryInfantryTroopCultureToABanner`) |``
   (it sits between the `Troop registration` and `Culture wiring` rows).
3. `TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs`: replace

   ```csharp
       private static readonly Dictionary<string, string> NoBannerByDesign = new Dictionary<string, string>
       {
           ["wild_trolls"] = "its troops are trolls, which the race gate (ExcludedRaces cave_troll, hill_troll) keeps from carrying a standard",
       };
   ```

   with

   ```csharp
       // A culture whose troops may never carry a standard goes here, with the reason. None does on this line
       // (the 1.5.x wild_trolls entry has no troops here, and a stale entry fails the test).
       private static readonly Dictionary<string, string> NoBannerByDesign = new Dictionary<string, string>();
   ```

**Verify**: `grep -c '<<<<<<<\|>>>>>>>\|wild_trolls' docs/features/banner-bearers.md docs/ai-includes/new-culture-authoring.md TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs`
prints `0` for each file.

Stage: `git add docs/features/banner-bearers.md docs/ai-includes/new-culture-authoring.md TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs`

Stage list: `Main/Features/BannerBearers/Domain/BannerBearerConfig.cs`,
`Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json`,
`TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs`, `docs/ai-includes/new-culture-authoring.md`,
`docs/features/banner-bearers.md`.

Message file `133-msg-B2.txt`:

```
fix(banners): v2.0.28 - Lindon and Blue Craig troops raise banners

Formations of Lindon's and Blue Craig's own troops carried no banner
bearer unless a lord with a banner led them: the banner config had no
row for either culture, while both cultures' infantry march in TAOM's
party templates. Lindon now raises the elves' archers' flag
(archers_flag_t1) and Blue Craig the orcs' standard
(standard_of_fury_t1), like their kin.

A new test reads the shipped troop and party template data and fails
when the culture of any infantry troop a party template fields has no
banner row, so a new culture cannot miss one. On 1.4.x data it found
exactly these two.

Not-tested: in game, a Lindon and a Blue Craig formation without a lord
Ported-from: 21094307642c6802f4b1733592169a34787e9ef7
```

**Verify**: Build; `FullyQualifiedName~ShippedBannerBearerConfigTests` gives `Failed: 0`; Data (label `B2`) then
the compare command prints `[]`.

### Step B3: clan colours reach portraits, refuge rows keep names (`365f2ecfe`, #993, #994)

Method: cherry-pick for the banner half; the refuge half is re-implemented for v1.4.8 (1.5.x patches
`ClanPartyItemWithPartyVM`, a v1.5.x type; v1.4.8 has the concrete `ClanPartyItemVM.UpdateProperties`), test
first. 1.5.x dropped the honest-zero wage line because v1.5.5 binds `PartyWageSubTitleText` nowhere; v1.4.8's
`ClanPartiesRightPanel.xml:58` does bind it, so 1.4.x KEEPS that rewrite.

**B3.1 Apply.**

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 365f2ecfe; echo "exit=$?"; drop docs/reviews/lessons/localization-ui.md docs/reviews/rca-banner-colors-refuge-rows-2026-10-24.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reference/harmony-patch-registry.md docs/features/banner-color-persistence.md docs/features/refuge.md Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs Main/Features/Refuge/RefugeIoC.cs Main/Features/Refuge/Hooks/RefugeClanRowRefreshPatch.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/Refuge/Patch75BindingTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs; git diff --name-only --diff-filter=U; git diff --cached --name-only
```

**Verify**: nothing unmerged; staged are exactly six `Main/Features/BannerColorPersistence/Hooks/` files
(`CampaignUIHelper_GetCharacterCode_Patch.cs`, `ClanColoredCharacterCode.cs` (new), `ClanPartyItemVM_GetCharacterCode_Patch.cs`,
`OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs`, `PartyCharacterVM_GetCharacterCode_Patch.cs`,
`SandBoxUIHelper_GetCharacterCode_Patch.cs`), the three new `TAOM.Tests/Features/BannerColorPersistence/` tests and
`TAOM.Tests/Features/Refuge/RefugeClanRowDecisionTests.cs`.
`grep -c "ClientOnly" Main/Features/BannerColorPersistence/Hooks/*.cs` prints `0` for every file.

**B3.2 Banner half edits.** In `ClanColoredCharacterCodeTests.cs`, `HookColorAssignmentGateTests.cs` and
`OrderOfBattleHeroItemVMPatchTests.cs` delete every `[TestCategory("RequiresGame")]` and
`[TestCategory("RequiresGameIL")]` line. The `:1099`/`:1103` cites in `PartyCharacterVM_GetCharacterCode_Patch.cs`
and the test hold on v1.4.8.

**B3.3 Refuge tests first (RED).** Append this test to `TAOM.Tests/Features/Refuge/RefugeClanRowDecisionTests.cs`
(inside the class, after the last test):

```csharp
    [TestMethod]
    public void RefugeIoC_InitializesTheRowRefreshPatch_WithTheBook()
    {
        // RefugeIoC.InitializePatchStatics hands the read-only book to the row patch; a dropped line leaves the
        // postfix null-guarded and every refuge row undecorated, with no error.
        var src = RepoPaths.ReadSource("Main/Features/Refuge/RefugeIoC.cs", stripComments: true);

        StringAssert.Contains(src, "RefugeClanRowRefreshPatch.Initialize(container.Resolve<IRefugeBook>());");
    }
```

**Verify**: Build `0 Error(s)`; Filtered `FullyQualifiedName~RefugeClanRowDecisionTests` gives `Failed: 3`
(the screen patch still decorates, the row patch file is missing, the IoC line is missing);
`FullyQualifiedName~BannerColorPersistence` gives `Failed: 0`. Any other result: STOP.

**B3.4 Refuge implementation (GREEN).**
1. Create `Main/Features/Refuge/Hooks/RefugeClanRowRefreshPatch.cs`:

   ```csharp
   using HarmonyLib;
   using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
   using TaleWorlds.Core;
   using TaleWorlds.Localization;

   namespace TAOM.Features.Refuge.Hooks;

   /// <summary>
   /// Postfix on <see cref="ClanPartyItemVM"/>.<c>UpdateProperties</c> (#994): re-applies the refuge row
   /// decoration after every property refresh. <see cref="RefugeClanScreenPatch"/> used to decorate rows once,
   /// at list build, but <c>ClanPartiesVM.RefreshValues</c> runs each row's <c>RefreshValues</c> afterwards
   /// (v1.4.8 ClanPartiesVM.cs:402-413), and <c>UpdateProperties</c> (ClanPartyItemVM.cs:1125) resets
   /// <c>Name</c> to the plain party name (:1147) and <c>PartyWageSubTitleText</c> to the live wage (:1161).
   /// The row constructor ends with <c>RefreshValues</c> too (:1116), so a freshly built row is decorated here
   /// as well.
   ///
   /// <para><c>UpdateProperties</c> has just rewritten <c>Name</c> when this runs, so appending the
   /// "(being raised)" suffix once per call never stacks. The wage slider is hidden, because the Garrison-typed
   /// row builds one whose figure is not charged for a refuge (DefaultClanFinanceModel processes neither
   /// WarPartyComponents nor OwnedCaravans) and whose SetWagePaymentLimit is a silent no-op on the base
   /// PartyComponent. <c>ShouldPartyHaveExpense = false</c> hides the slider panel and shows the static wage
   /// line (v1.4.8 SandBox ClanPartiesRightPanel.xml:54, :343), which binds <c>PartyWageSubTitleText</c>
   /// (:58), so that line is re-rendered to the honest figure: 0.</para>
   ///
   /// <para>Whole body try/catch: a clan-screen refresh must never die on a refuge row; the failure mode is an
   /// undecorated row, which is the pre-patch state. Disjoint from Patch17's TroopWeight postfix on the same
   /// method, which restates the party-size fields.</para>
   /// </summary>
   [HarmonyPatch(typeof(ClanPartyItemVM), nameof(ClanPartyItemVM.UpdateProperties))]
   [HarmonyPatchCategory("Patch75_Refuge")]
   public static class RefugeClanRowRefreshPatch
   {
       private static IRefugeBook? _refuges;

       /// <summary>Called once from RefugeIoC.InitializePatchStatics at container build time.</summary>
       public static void Initialize(IRefugeBook refuges)
       {
           _refuges = refuges;
       }

       [HarmonyPostfix]
       public static void Postfix(ClanPartyItemVM __instance)
       {
           try
           {
               var refugeService = _refuges;
               if (refugeService == null || __instance == null)
                   return;

               var partyId = __instance.Party?.MobileParty?.StringId;
               var refuge = partyId == null ? null : refugeService.GetByPartyId(partyId);
               if (refuge == null)
                   return;

               SuppressWagePanel(__instance);
               if (refuge.Building)
               {
                   __instance.Name = new TextObject("{=taom_rf_clan_row_building}{NAME} (being raised)")
                       .SetTextVariable("NAME", __instance.Name).ToString();
               }
           }
           catch
           {
               // Decoration is cosmetic; an undecorated row is the safe degradation, and a throw here
               // would take the whole clan-screen refresh down.
           }
       }

       private static void SuppressWagePanel(ClanPartyItemVM row)
       {
           row.ShouldPartyHaveExpense = false;
           row.ExpenseItem = null;
           GameTexts.SetVariable("LEFT", GameTexts.FindText("str_party_wage"));
           GameTexts.SetVariable("RIGHT", 0);
           row.PartyWageSubTitleText = GameTexts.FindText("str_LEFT_colon_RIGHT").ToString();
       }
   }
   ```

2. `Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs`:
   - In the summary, replace the line `/// appended all refuges unfiltered). Building rows carry a "(being raised)" name suffix.`
     and everything up to and including `/// which is re-rendered to the honest figure: 0.</para>` with:

     ```
     /// appended all refuges unfiltered). The row decoration (the "(being raised)" name suffix, the
     /// hidden wage slider and the honest zero wage line) is not applied here:
     /// <see cref="RefugeClanRowRefreshPatch"/> re-applies it on every <c>UpdateProperties</c>, including
     /// the one the row constructor triggers, so it survives the refreshes that used to wipe it (#994).
     ///
     /// <para>The row wiring reuses the VM's own private <c>OnPartySelection</c> (via a cached
     /// reflection handle, per the hot-path rule; a UI refresh is not hot, but the lookup still runs
     /// once, not per refresh) so selecting a refuge row behaves exactly like selecting a vanilla
     /// garrison. Expense-change and change-leader callbacks are no-ops.</para>
     ```

   - Replace the block from `var existing = __instance.Garrisons.FirstOrDefault(row => row.Party == party.Party);`
     through `__instance.Garrisons.Add(row);` with:

     ```csharp
                     if (__instance.Garrisons.Any(row => row.Party == party.Party))
                         continue;

                     // The constructor ends with RefreshValues, so RefugeClanRowRefreshPatch has already
                     // decorated the row (#994).
                     var row = new ClanPartyItemVM(
                         party.Party, onSelect, noop, noop,
                         ClanPartyItemVM.ClanPartyType.Garrison,
                         disbandBehavior, teleportationBehavior);
                     __instance.Garrisons.Add(row);
     ```

   - Delete the `SuppressWagePanel` summary comment and method (from `/// <summary>Removes the non-functional wage control`
     through its closing brace) and the blank line after it; delete `using TaleWorlds.Localization;` if the build
     shows it unused (harmless either way).
3. `Main/Features/Refuge/RefugeIoC.cs`, in `InitializePatchStatics`, directly after
   `RefugeClanScreenPatch.Initialize(refugeService);` add
   `        RefugeClanRowRefreshPatch.Initialize(container.Resolve<IRefugeBook>());`.
4. `TAOM.Tests/Features/Refuge/Patch75BindingTests.cs`: rename `BothPatches_CarryThePatch75Category` to
   `AllPatches_CarryThePatch75Category` and change its array to
   `new[] { typeof(RefugeClanScreenPatch), typeof(RefugeEncounterPatch), typeof(RefugeClanRowRefreshPatch) }`.
   Leave the property list (`ShouldPartyHaveExpense`, `ExpenseItem`, `PartyWageSubTitleText`, `Name`) and its
   comment as they are.

**Verify**: Build; `FullyQualifiedName~RefugeClanRowDecisionTests` gives `Passed: 3`;
`FullyQualifiedName~Refuge|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~BannerColorPersistence`
gives `Failed: 0`.

**B3.5 Docs and gate rows.**
1. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: after the `CustomBattleSideVM` `OnCultureSelection`
   row insert
   `    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories.ClanPartiesVM", "ClanPartiesVM", "OnPartySelection", "Method", "RefugeClanScreenPatch.cs:L")]`
   where `L` is the line `grep -n "OnPartySelectionMethod =" Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs` prints.
2. `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: after the `OnCultureSelection` row insert
   ``| `…ViewModelCollection.ClanManagement.Categories.ClanPartiesVM` | `OnPartySelection` | method (private) | `RefugeClanScreenPatch.cs:L` | Refuge clan screen: a refuge row's selection handler is the VM's own. Missing: the method handle is null and the postfix adds no refuge rows |``.
3. `docs/features/refuge.md`: replace the bullet `- **Clan screen: every refuge listed, wage control suppressed.** ...`
   (through `recomposes the static wage line to the honest 0.`) with:

   ```
   - **Clan screen: every refuge listed, wage slider hidden.** All rows (ready, building with a
     "(being raised)" suffix, orphan-adopted) appear under Garrisons (source parity; a mid-raise
     refuge holding a deposited garrison must not vanish for the build window). The Garrison-typed
     `ClanPartyItemVM` ctor builds a live wage slider whose figure is never charged
     (`DefaultClanFinanceModel` processes neither `WarPartyComponents` nor `OwnedCaravans` for a
     refuge) and whose `SetWagePaymentLimit` is a silent base-`PartyComponent` no-op, so the slider
     is hidden (`ShouldPartyHaveExpense = false`, `ExpenseItem = null`) and the static wage line it
     reveals (v1.4.8 SandBox `ClanPartiesRightPanel.xml:58`) is recomposed to the honest 0. The
     decoration is applied by a postfix on `ClanPartyItemVM.UpdateProperties`
     (`RefugeClanRowRefreshPatch`, #994), not once at list build: `ClanPartiesVM.RefreshValues`
     re-runs every row's `UpdateProperties` after the build, which resets `Name` and the wage line,
     so a one-shot decoration lost the suffix and the zero wage. The row constructor's own refresh
     decorates new rows, and `IRefugeBook.GetByPartyId` picks the refuge and its `Building` flag.
   ```

   and the Key files row `| `Hooks/RefugeClanScreenPatch.cs` + `RefugeEncounterPatch.cs` | Patch75 |` becomes
   `| `Hooks/RefugeClanScreenPatch.cs` + `RefugeClanRowRefreshPatch.cs` + `RefugeEncounterPatch.cs` | Patch75 |`.
4. `docs/reference/harmony-patch-registry.md`:
   - Patch23: after the `**Inventory doll (#973):**` paragraph (A6) insert a blank line and
     `**GetCharacterCode postfixes (#993):** the five portrait postfixes (`CampaignUIHelper`, `SandBoxUIHelper`, `ClanPartyItemVM`, `PartyCharacterVM`, and the `OrderOfBattleHeroItemVM` rebuild, which targets both `RefreshInformation` and the `(Agent)` constructor via `TargetMethods()` because `OrderOfBattleVM` builds every hero through the constructor, v1.4.8 `OrderOfBattleVM.cs:746`, `OrderOfBattleHeroItemVM.cs:275`) return the result through `ClanColoredCharacterCode.Recolor`, which rebuilds the `CharacterCode` via the 8-argument `CreateFrom` (v1.4.8 `CharacterCode.cs:87`). `CharacterCode.Code` is built at creation and has a private setter (`:18`), and the portrait identifier reads only `Code`, so assigning `Color1/Color2` afterwards never reached the renderer. `PartyCharacterVM` sets `IsPrisoner` (`:1103`) only after the `Troop` assignment (`:1099`) has run `GetCharacterCode`, so the old prisoner check always read false; the postfix reads type and side from the arguments.`
   - Patch75: replace the `**Targets:**` line with
     ``**Targets:** `ClanPartiesVM.RefreshPartiesList()` (public, Postfix) + `ClanPartyItemVM.UpdateProperties()` (public, Postfix, #994) + `PlayerEncounter.DoMeeting()` (public static, Prefix). Three patch classes, one category.``;
     in the `**Feature:**` line `RefugeClanScreenPatch.cs` + `RefugeEncounterPatch.cs` becomes
     `RefugeClanScreenPatch.cs` + `RefugeClanRowRefreshPatch.cs` + `RefugeEncounterPatch.cs`; replace the
     sentence `The clan-screen postfix appends ready refuges to the Garrisons list as `ClanPartyItemVM` rows (selection wired via AccessTools to the private `OnPartySelection`).`
     with
     `The clan-screen postfix appends every refuge (ready, building, orphan-adopted) to the Garrisons list as `ClanPartyItemVM` rows (selection wired via AccessTools to the private `OnPartySelection`). The `UpdateProperties` postfix re-applies the refuge row decoration (hidden wage slider, the static wage line at 0, the "(being raised)" suffix on a building refuge) after every refresh, because vanilla's `UpdateProperties` resets `Name` and the wage line (v1.4.8 `ClanPartyItemVM.cs:1147`, `:1161`); it shares that method with Patch17's TroopWeight postfix and touches different fields.`
5. `docs/features/banner-color-persistence.md`:
   - Replace the bullet starting `- **CharacterCode is mutable**` with the two bullets
     `- **CharacterCode is rebuilt, not mutated**: `CharacterCode.Code` is built at creation (private setter, v1.4.8 `CharacterCode.cs:18`) and is the only field the portrait identifier reads, so every UI patch takes `ref CharacterCode __result` and returns `ClanColoredCharacterCode.Recolor(__result, color1, color2)` (#993).`
     and
     `- **Every hero is recoloured**: the portrait patches follow the feature's design and apply to every hero with a clan, AI lords included, so a portrait matches the clan colours his battlefield armour already shows.`
   - Key files: the `OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs` row's purpose becomes
     `Patch23: pre-battle deployment screen colors; `TargetMethods()` hooks both `RefreshInformation` and the `(Agent)` constructor, because `OrderOfBattleVM` builds every hero through the constructor, which never calls `RefreshInformation` (#993)`;
     after it insert ``| `Hooks/ClanColoredCharacterCode.cs` | `Recolor`: rebuilds a `CharacterCode` through the 8-argument `CreateFrom` so the colours land in `Code` (#993) |``;
     update the test-folder row and the `N files, M [TestMethod]` sentence with the A5 count command [10 files, 49].
   - Tests table, after the `SPInventoryVM_ArmorColor_PatchTests.cs` row:

     ```
     | `ClanColoredCharacterCodeTests.cs` | `Recolor` bakes colours into `Code`, keeps every other field, passes null, empty and already-coloured codes through; the party patch's `AppliesTo` and parameter names; the `CharacterCode` binding pin (#993) |
     | `OrderOfBattleHeroItemVMPatchTests.cs` | `TargetMethods()` resolves both `RefreshInformation` and the `(Agent)` constructor (#993) |
     | `HookColorAssignmentGateTests.cs` | source scan: no hook assigns `.Color1 =` or `.Color2 =` on a built code (#993) |
     ```

   - How-To, step 3 becomes
     `3. Use `_heroAdapter.GetClanColorInfo(characterObject)` then `_service.ShouldUseClanColor(info)` then `__result = ClanColoredCharacterCode.Recolor(__result, info.Color1, info.Color2)` on a `ref CharacterCode __result` parameter; assigning `Color1`/`Color2` on a built code changes nothing visible.`
   - Changelog, first entry: `- <today>: portrait colours now render on the five `GetCharacterCode` screens (party, clan parties, `CampaignUIHelper`, `SandBoxUIHelper`, order of battle) (#993): `ClanColoredCharacterCode.Recolor` rebuilds the code through the 8-argument `CreateFrom`. The order-of-battle patch also hooks the `(Agent)` constructor, and the party-screen patch reads its own `type` and `side` arguments. Ported from the 1.5.x line by plan 133.`

**B3.6 Party cache re-check (WP125's Maintenance note; comment from `963218f7f`).** WP125's portrait cache patch
says Patch23 writes `Color1` and `Color2` on a built code, which B3.1 ended. The cache stays safe if Patch23's postfix
runs first, so the cache stores the recoloured code and `Recolor` returns it unchanged on a hit. Check that order:

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && grep -n '"Patch23_BannerColorPersistence"\|RunPhase(ApplyPhase.GameInit' Main/SubModule.cs
```

**Verify**: exactly two lines, the Patch23 line first (both postfixes have the default priority, so application order
is run order). Reversed: STOP, the comment below would be false.

Edit `Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs` by hand,
never `git show 963218f7f:<path> > <path>` (that version also carries `[ClientOnly]`, D7). Replace the four summary
lines from `/// a miss runs vanilla and the postfix stores its result.` through
`/// PartyFastOpenBindingTests, covering CharacterImageIdentifier, CharacterImageIdentifierVM and UpgradeTargetVM). The key`
with:

```
/// a miss runs vanilla and the postfix stores its result. Safe next to BannerColorPersistence: Patch23's postfix (applied
/// earlier, same priority, so it runs first) replaces the result with a code rebuilt in the clan colours
/// (ClanColoredCharacterCode.Recolor, #993), so the cache stores the recoloured code, and on a hit Recolor returns it
/// unchanged. Nothing writes to a built code, so rows can share one (pinned by PartyFastOpenBindingTests, covering
/// CharacterImageIdentifier, CharacterImageIdentifierVM and UpgradeTargetVM). The key
```

In `docs/features/shadowfax-screens.md`, replace the paragraph starting `**Why this is safe next to BannerColorPersistence.**` with that paragraph from
`git show 963218f7f:docs/features/shadowfax-screens.md` (through `slower, not wrong.`). Take only that paragraph, not the file.

**Verify**: `grep -c 'ClanColoredCharacterCode.Recolor' docs/features/shadowfax-screens.md` prints at least `1`;
`grep -c 'ClanColoredCharacterCode.Recolor' Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs`
prints `1`; `git diff --numstat Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs`
prints `5`, `4`; Build `0 Error(s)`; Filtered `FullyQualifiedName~PartyFastOpenBindingTests` gives `Failed: 0`.

Stage: `git add Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs Main/Features/Refuge/Hooks/RefugeClanRowRefreshPatch.cs Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs Main/Features/Refuge/RefugeIoC.cs TAOM.Tests/Features/Refuge/Patch75BindingTests.cs TAOM.Tests/Features/Refuge/RefugeClanRowDecisionTests.cs TAOM.Tests/Features/BannerColorPersistence/ClanColoredCharacterCodeTests.cs TAOM.Tests/Features/BannerColorPersistence/HookColorAssignmentGateTests.cs TAOM.Tests/Features/BannerColorPersistence/OrderOfBattleHeroItemVMPatchTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/features/refuge.md docs/reference/harmony-patch-registry.md docs/features/banner-color-persistence.md docs/features/shadowfax-screens.md`

Stage list: the six `Main/Features/BannerColorPersistence/Hooks/` files, the three Refuge `Main` files,
`PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs`, the three new BannerColorPersistence tests, `Patch75BindingTests.cs`, `RefugeClanRowDecisionTests.cs`,
`ReflectionSiteBindingTests.cs`, `reflection-sites.md`, `refuge.md`, `harmony-patch-registry.md`,
`banner-color-persistence.md`, `shadowfax-screens.md` (21 paths).

Message file `133-msg-B3.txt`:

```
fix(ui): v2.0.28 - clan colours reach portraits, refuge rows keep names

Portraits now show clan colours. Every portrait patch of Banner Color
Persistence set the colours on a portrait code after the game had
already built the text the portrait is drawn from, so no clan colour
reached the party screen, the clan screen, hero panels or the Order of
Battle. The code is now rebuilt with the clan colours, and the Order of
Battle colours the hero pool as well as captains.

Refuges on the clan screen keep their "(being raised)" name, the
hidden wage slider and the zero wage line after the screen opens: the
clan screen refreshes every row after building the list, which reset
them. A postfix on ClanPartyItemVM.UpdateProperties now reapplies the
decoration on every refresh.

The party screen's portrait cache now stores the recoloured code; only
its comment changes.

Research: taom-src v1.4.8 CharacterCode :18, :87; ClanPartyItemVM :1125
Not-tested: in game, portraits, Order of Battle pool, refuge row name
Ported-from: 365f2ecfef13d435689138992ec866dd18784de8
Ported-from: 963218f7fe104ee848066086e1481dfafde51994
```

**Verify**: Build; Filtered `FullyQualifiedName~BannerColorPersistence|FullyQualifiedName~Refuge|FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~PartyFastOpenBindingTests`
gives `Failed: 0`.

### Part B done criteria

- [ ] Three more commits (nine in total); Build; full Test `Failed: 0`, total = Part A total + 39 (EnumNamesTests
      10, BannerBearerConfigProviderTests +10, DreadAuraConfigProviderTests +2, UncapturableHeroesConfigProviderTests
      +2, ClanColoredCharacterCodeTests 8, HookColorAssignmentGateTests 2, OrderOfBattleHeroItemVMPatchTests 1,
      RefugeClanRowDecisionTests 3, ReflectionSiteBindingTests +1).
- [ ] `git grep -n "ClientOnly\|ClientComposition\|ClanPartyItemWithPartyVM\|SignatureStrikes" -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -nE '\.Color[12]\s*=[^=]' -- Main/Features/BannerColorPersistence/Hooks` prints only `ClanColoredCharacterCode.cs` (or nothing).
- [ ] Data compare for B2 printed `[]`.
- [ ] Hand-off: totals and hashes.

---

## Part C: map bar, relations, generator, map-menu gate, culture markets

### Step C1: map-bar resource shows on the first frame (`fee906345`, #958)

Method: cherry-pick. 1.5.x moved the mixin to `Client/`; git's rename detection applies the hunk to
`Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` (simulated this session). Unmerged:
`special-resources.md` only (1.4.x paragraph differs).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n fee906345; echo "exit=$?"; drop docs/features/special-resources.md; git diff --name-only --diff-filter=U; git diff --cached --name-only; ls Client 2>&1 | head -1
```

**Verify**: nothing unmerged; staged: the `Main/...SpecialResourceMapBarMixin.cs`, the new
`TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs`, `docs/features/gui-sprite-system.md`;
`ls Client` says `No such file or directory`. A `Client/` path staged: STOP.

Edits:
1. `SpecialResourceMapBarMixinWiringTests.cs`: `"Client/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs"`
   becomes `"Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs"`. Leave the mixin comment
   `(v1.4.8 and v1.5.5)` for C2 (editing it here would conflict with C2's hunk).
2. `docs/features/special-resources.md`: in the "Map bar display" bullet replace
   `adds one `MapInfoItemVM` to `SecondaryInfoItems` (dormant ownership hazard,` with
   `adds one `MapInfoItemVM` to `SecondaryInfoItems` on its first refresh, guarded by `Contains` (`MapBarVM.Initialize` runs that refresh before the view loads and the next one 2 s later, #958) (dormant ownership hazard,`;
   in "Tests", before the `SpecialResourceCheatsFormatTests.cs` bullet insert
   `- `SpecialResourceMapBarMixinWiringTests.cs`: 3 source pins on the map-bar mixin (comments stripped): the entry is added on the first refresh behind `Contains` with only the three guard returns before it, and the no-resource guard comes before the insert (#958)`.

Stage: `git add TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs docs/features/special-resources.md`

Stage list: `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs`,
`TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs`, `docs/features/gui-sprite-system.md`,
`docs/features/special-resources.md`.

Message file `133-msg-C1.txt`:

```
fix(resources): v2.0.28 - map-bar resource shows on the first frame

The culture resource counter on the map bar's bottom row now appears
with the rest of the bar when a campaign loads. Before, it popped in
about two seconds later on every load: the mixin skipped the map bar's
first refresh, which MapBarVM.Initialize runs itself, and the next
refresh comes two seconds after the bar is built.

The entry is still added only once (a Contains check), and it comes
back if the bar ever rebuilds its list. A hero whose realm has no
resource still gets no entry.

Research: taom-src v1.4.8 MapBarVM :237-251, MapInfoVM :138-153
Not-tested: in game, counter visible on the first frame after a load
Ported-from: fee906345b53cac93ae22193ff310b86203eb699
```

**Verify**: Build; `FullyQualifiedName~SpecialResourceMapBarMixinWiringTests` gives `Passed: 3`;
`FullyQualifiedName~SpecialResources` gives `Failed: 0`. (The commit carries the 1.5.x comment text until C2;
skip the `v1.5.5` pre-commit grep for this step only.)

### Step C2: map-bar resource leaves when the hero loses it (`406dd739f`, #977)

Method: cherry-pick; simulated after C1: unmerged `special-resources.md` only.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 406dd739f; echo "exit=$?"; drop docs/features/special-resources.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits:
1. Mixin comment: `comes 2 s later (v1.4.8 and v1.5.5).` becomes `comes 2 s later (v1.4.8).`
2. `.claude/rules/gui-ui.md`: `In v1.4.8 and v1.5.5 that method is called from exactly one site` becomes
   `In v1.4.8 that method is called from exactly one site`.
3. `docs/features/special-resources.md`:
   - Overview: replace its first two sentences (from `Per-kingdom special resource system where all 18 TAOM kingdoms`
     through `shared balance within each group.`) with
     `Per-kingdom special resource system: 10 of the 14 kingdoms in `taom_spkingdoms.xml` and all 8 vanilla kingdoms that `spkingdoms.xslt` rewrites have a secondary currency required to recruit and maintain elite troops (goblin, mistymountainorcs, bluecraig and lindon have none, and their heroes get no map-bar entry). 11 unique resources are mapped to faction groups, with a shared balance within each group.`
   - Map bar bullet: after `the next one 2 s later, #958)` insert `, and removes it while the hero has no resource (#977)`.
   - Tests bullet from C1 becomes
     `- `SpecialResourceMapBarMixinWiringTests.cs`: 4 source pins on the map-bar mixin (comments stripped): the entry is added on the first refresh behind `Contains` with only the three guard returns before it (#958), the no-resource guard comes before the insert, and the no-resource branch removes the entry (#977)`.

Stage: `git add Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs .claude/rules/gui-ui.md docs/features/special-resources.md`

Stage list: `.claude/rules/gui-ui.md`, `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs`,
`TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs`, `docs/features/gui-sprite-system.md`,
`docs/features/special-resources.md`.

Message file `133-msg-C2.txt`:

```
fix(resources): v2.0.28 - map-bar resource leaves when the hero loses it

When the hero stops having a culture resource during a campaign, its
counter now leaves the map bar. Before, it stayed with the old amount,
warning colour and icon, and an empty tooltip, until the next load.
This happens when a goblin, Misty Mountains orc, Blue Craig or Lindon
hero leaves a kingdom that has a resource, or when the heir who takes
over after the player's death belongs to one of those peoples. The
counter comes back with the right amount if a resource returns.

The feature page no longer claims every kingdom has a resource: on
1.4.x data, 10 of the 14 kingdoms in taom_spkingdoms.xml have one.

Not-tested: in game, counter leaves after the hero loses the resource
Ported-from: 406dd739f4b0c7211b7e146ea8dc814ffe3bd87c
```

**Verify**: Build; `FullyQualifiedName~SpecialResourceMapBarMixinWiringTests` gives `Passed: 4`;
`git grep -n "v1\.5\.5" -- Main/Features/SpecialResources .claude/rules/gui-ui.md` prints nothing.

### Step C3: collection advice matches the ownership rule (`8e7d1f4cb`, #958)

Method: cherry-pick, clean (simulated exit 0).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && git cherry-pick -n 8e7d1f4cb; echo "exit=$?"; git diff --cached --name-only
```

**Verify**: `exit=0`; staged only `docs/features/gui-sprite-system.md`.

Message file `133-msg-C3.txt`:

```
docs(gui): v2.0.28 - collection advice matches the ownership rule

The GUI sprite guide still told modders never to add items to a
vanilla collection such as the map bar's SecondaryInfoItems. The GUI
rule has since replaced that with "discouraged, on ownership grounds".
The guide now says the same: prefer bound properties and a prefab
injection, and if an item must go into such a list, add it on every
refresh behind a Contains check, never skip the first refresh, and
remove it when it no longer applies, as the special resource entry
does.

Ported-from: 8e7d1f4cbe4e4d61e7c5f7c4fbe7b27648668cf9
```

**Verify**: Docs command unchanged except `ai_dashes: 0`.

### Step C4: relation notes match the engine (`645068836`, #923)

Method: cherry-pick; `IArmouryPlayerAdapter.cs` is 1.5.x-only (modify/delete).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 645068836; echo "exit=$?"; drop Main/Adapters/IArmouryPlayerAdapter.cs; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits:
1. `Main/Adapters/CommanderLordAdapter.cs`: `// No affectRelatives: v1.5.5 ignores it, and the change lands on GetHeroesForEffectiveRelation's pair (#923).`
   becomes `// No affectRelatives: v1.4.8 ignores it (ChangeRelationAction.cs:30-33); the change lands on GetHeroesForEffectiveRelation's pair (#923).`
2. `TAOM.Tests/Adapters/AffectRelativesGateTests.cs` summary: `/// v1.5.5 <c>ChangeRelationAction.ApplyPlayerRelation</c>`
   becomes `/// v1.4.8 <c>ChangeRelationAction.ApplyPlayerRelation</c>`, and
   `reads as that spread and misled the Hard Questions plan (#896, #923).` becomes `reads as that spread (#923).`

Stage: `git add Main/Adapters/CommanderLordAdapter.cs TAOM.Tests/Adapters/AffectRelativesGateTests.cs`

Stage list: those two paths.

Message file `133-msg-C4.txt`:

```
fix(relations): v2.0.28 - relation notes match the engine

No change in game. The enlisted commander adapter passed
affectRelatives: true to ChangeRelationAction.ApplyPlayerRelation,
which reads as a relation change that also spreads to the hero's kin.
v1.4.8 ignores the flag: the change lands on the pair the diplomacy
model's GetHeroesForEffectiveRelation picks. The flag is no longer
passed, and a new source test stops code from passing it as true.

Research: taom-src v1.4.8 ChangeRelationAction.cs:14-33
Ported-from: 6450688362da573898e49cdc8fe8c856a0a7f95a
```

**Verify**: Build; `FullyQualifiedName~AffectRelativesGateTests` gives `Passed: 1`.

### Step C5: kingdom generator keeps its block in place (`bd457ee6e`, #947)

Method: cherry-pick. The `upsert()` hunk applies to the older 1.4.x generator (simulated clean); the test is
synthetic. Do NOT run the generator itself (it rewrites tracked data).

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n bd457ee6e; echo "exit=$?"; drop docs/features/dale-lord-expansion.md docs/reviews/lessons/testing-qa.md; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged.

Edits:
1. `tools/generate_new_faction_kingdoms.py`, the first two comment lines in `upsert()`:
   `# Replace an existing block where it stands (#947): re-appending it moved it below any block added` and
   `# after it, which --check reported as drift. Only an absent block goes before the closing tag. A second`
   become
   `# Replace an existing block where it stands (#947): re-appending it moved it below any block or rows` and
   `# added after it (heroes.xml keeps hand-authored heroes after the lindon block). Only an absent block goes`
   and the third line `# region or a lone BEGIN or END is refused: the engine loads the later row of a repeated id, so a stale copy`
   becomes `# before the closing tag. A second region or a lone BEGIN or END is refused: the engine loads the later row of a repeated id, so a stale copy`.
2. `tools/tests/test_generate_new_faction_kingdoms.py`:
   `sed -i 's/TAOM-DALE-EXPANSION/TAOM-LATER-BLOCK/g; s/DALE/LATER/g; s/clan_sturgia_10/clan_later_1/g; s/arthedain/lindon/g' tools/tests/test_generate_new_faction_kingdoms.py`
   then replace the docstring (from the line after `#!/usr/bin/env python3` through the closing `"""`) with:

   ```
   """upsert() in generate_new_faction_kingdoms.py: a re-run keeps its marker block where it stands.

   Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms

   #947 (ported from the 1.5.x line): upsert() deleted its marker block and re-appended it before the
   closing tag, so a block or rows written after it moved on every re-run. On 1.4.x, heroes.xml holds 36
   hand-authored heroes after the lindon block. The texts here are synthetic.
   """
   ```

   **Verify**: `grep -ci "arthedain\|dale\|sturgia" tools/tests/test_generate_new_faction_kingdoms.py` prints `0`.

Stage: `git add tools/generate_new_faction_kingdoms.py tools/tests/test_generate_new_faction_kingdoms.py`

Stage list: `docs/features/new-factions-misty-mountains-lindon.md`, `docs/modding/recipe-add-a-kingdom.md`,
`tools/generate_new_faction_kingdoms.py`, `tools/tests/test_generate_new_faction_kingdoms.py`.

Message file `133-msg-C5.txt`:

```
fix(tools): v2.0.28 - kingdom generator keeps its block in place

No change in game. tools/generate_new_faction_kingdoms.py, which
writes the goblin, Misty Mountains orc, Blue Craig and Lindon kingdom,
clan, lord and hero blocks, now updates each marker block where it
stands. Before, a re-run deleted the block and re-appended it before
the closing tag, so anything written after it moved: on 1.4.x,
heroes.xml holds 36 hand-authored heroes after the Lindon block, so a
re-run would have reordered the file.

The generator also refuses a file that holds two copies of its block,
a lone start or end marker, or no closing tag, so a stale copy cannot
slip through. Ten unit tests cover upsert() on synthetic text.

Ported-from: bd457ee6ec9ab7c5bc9b7180be94922ee9078dcf
```

**Verify**: `python -m unittest tools.tests.test_generate_new_faction_kingdoms -v 2>&1 | tail -3` prints
`Ran 10 tests` and `OK`; Tool tests: no new failure vs Step 0.7, `Ran` = P0 + 10.

### Step C6: one map-menu gate for F6 and Make camp (`ca248d1b7`, gate only, #789)

Method: hand re-implementation, test first (the rest of `ca248d1b7` is the Fiefs button, out of scope).

**C6.1 RED.** Create `TAOM.Tests/Features/FiefManagement/MapMenuGateWiringTests.cs`:

```csharp
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FiefManagement;

/// <summary>
/// The one map-menu gate (from the 1.5.x #789 change, without its Fiefs button): F6 and the field camp's
/// "Make camp" query both ask MapMenuGate, which mirrors vanilla MapScreen.OnFrameTick's suppression set
/// (v1.4.8 MapScreen.cs:1355) plus the escape menu and a live menu context. Source pins, comments stripped.
/// </summary>
[TestClass]
public class MapMenuGateWiringTests
{
    private const string GateFile = "Main/Adapters/MapMenuGate.cs";
    private const string F6File = "Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs";

    [TestMethod]
    public void TheMapModalGuardList_IsWrittenOnce()
    {
        // Two copies of this list drifted apart (F6 and the field camp's query). One gate now; every other
        // file delegates. IsMapIncidentActive is the member only the list names.
        var mainDir = RepoPath("Main");
        var holders = Directory.EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => StripComments(File.ReadAllText(f)).Contains("IsMapIncidentActive"))
            .Select(f => Path.GetFullPath(f).Substring(Path.GetFullPath(RepoPath()).Length).TrimStart('\\', '/').Replace('\\', '/'))
            .OrderBy(f => f)
            .ToList();

        CollectionAssert.AreEqual(new[] { GateFile }, holders,
            "Only MapMenuGate.cs may name the map's modal flags. Holders: " + string.Join(", ", holders));
    }

    [DataTestMethod]
    [DataRow(F6File)]
    [DataRow("Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs")]
    public void TheGateConsumers_DelegateToMapMenuGate(string file)
    {
        StringAssert.Contains(ReadSource(file, stripComments: true), "MapMenuGate.IsMapClearForMenu()");
    }

    [TestMethod]
    public void TheGate_MirrorsVanillasFullSet_PlusTheStaleFlagAndEscapeMenuCases()
    {
        var source = ReadSource(GateFile, stripComments: true);

        foreach (var term in new[]
                 {
                     "ActiveState is MapState", "CurrentMenuContext", "IsInMenu", "IsInBattleSimulation", "IsInArmyManagement",
                     "IsMarriageOfferPopupActive", "IsHeirSelectionPopupActive", "IsMapCheatsActive", "IsMapIncidentActive",
                     "IsOverlayContextMenuEnabled", "IsEncyclopediaOpen", "IsEscapeMenuOpened",
                     "GetMapScreenActionIsEnabledWithReason",
                 })
            StringAssert.Contains(source, term, "MapMenuGate lost a guard: " + term);

        // The helper reads Hero.MainHero and MobileParty.MainParty unguarded, so it must come after the map-state check.
        Assert.IsTrue(source.IndexOf("ActiveState is MapState") < source.IndexOf("GetMapScreenActionIsEnabledWithReason"),
            "The helper must run after the MapState check.");
    }

    [TestMethod]
    public void F6_ChecksTheKeyFirst_AndKeepsTheEnlistedAttachedRefusal()
    {
        var source = ReadSource(F6File, stripComments: true);
        var key = source.IndexOf("IsF6Pressed", System.StringComparison.Ordinal);
        var gate = source.IndexOf("MapMenuGate.IsMapClearForMenu()", System.StringComparison.Ordinal);

        Assert.IsTrue(key >= 0 && gate > key, "F6 must test the key before the gate, so the gate runs only on a press.");
        StringAssert.Contains(source, "EnlistmentState.EnlistedAttached", "F6 must still refuse while enlisted-attached.");
    }
}
```

and `TAOM.Tests/Migration/MapMenuGateBindingTests.cs`:

```csharp
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Engine pin for MapMenuGate (Main/Adapters/MapMenuGate.cs): it calls vanilla
/// CampaignUIHelper.GetMapScreenActionIsEnabledWithReason as MapScreen.OnFrameTick does (v1.4.8
/// CampaignUIHelper.cs:2387, MapScreen.cs:1355). Pinned so a rename or a shape change after an engine update is
/// a red test rather than a MissingMethodException on the first F6 press.
/// </summary>
[TestClass]
public class MapMenuGateBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetMapScreenActionIsEnabledWithReason_IsAPublicStaticBoolWithAnOutTextObject()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var helper = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper);

        var method = helper.GetMethod("GetMapScreenActionIsEnabledWithReason", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(method, "CampaignUIHelper.GetMapScreenActionIsEnabledWithReason is gone; the map-menu gate lost vanilla's own term.");
        Assert.AreEqual(typeof(bool), method!.ReturnType);
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.IsTrue(parameters[0].IsOut);
        Assert.AreEqual(typeof(TaleWorlds.Localization.TextObject).MakeByRefType(), parameters[0].ParameterType);
    }
}
```

**Verify**: Build `0 Error(s)`; Filtered `FullyQualifiedName~MapMenuGate` gives `Failed: 5, Passed: 1`.
Anything else: STOP.

**C6.2 GREEN.**
1. `git show ca248d1b7:Main/Adapters/MapMenuGate.cs > Main/Adapters/MapMenuGate.cs` (the 1.5.x file; its
   code needs no change on v1.4.8: every member it reads is in the engine table above). Then replace its whole
   `/// <summary>` ... `/// </summary>` block (it names the 1.5.x Fiefs button and `FiefHubHostAdapter`) with the
   block below, so the file ends up as shown:

   ```csharp
   using SandBox.View.Map;
   using TaleWorlds.CampaignSystem;
   using TaleWorlds.CampaignSystem.GameState;
   using TaleWorlds.CampaignSystem.ViewModelCollection;
   using TaleWorlds.Core;

   namespace TAOM.Adapters;

   /// <summary>
   /// The one list of conditions under which TAOM may push a game menu onto the campaign map. Both menu-pushing
   /// entry points ask here: F6 (<c>Patch36_MapScreenF6</c>) and the field camp's "Make camp"
   /// (<c>MapScreenCampMenuActivationQuery</c>). Written once because the two copies had drifted apart.
   ///
   /// It mirrors vanilla <c>MapScreen.OnFrameTick</c>'s suppression set for map actions (v1.4.8 MapScreen.cs:1355:
   /// army management, battle simulation, the marriage and heir popups, map cheats, a map incident, the overlay
   /// context menu, the encyclopedia, and <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>,
   /// CampaignUIHelper.cs:2387, for the prisoner, raft, mission and encounter states). Codex review #38b caught
   /// that an IsInMenu-only guard let F6 fire during those modals, each of which can read
   /// <c>MainParty.CurrentSettlement</c> while the remote-fief swap has replaced it. On top of vanilla's set it
   /// also refuses while the escape menu is open and while the campaign has a menu context, because
   /// <c>IsInMenu</c> can lag a menu that has just opened.
   ///
   /// Field reads only, plus one vanilla helper that allocates a text only when it refuses: F6 asks on a key
   /// press, the field camp overlay four times a second. Main thread. Ported from the 1.5.x line (#789) without
   /// its Fiefs button.
   /// </summary>
   internal static class MapMenuGate
   {
       public static bool IsMapClearForMenu()
       {
           if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return false;

           // From here on the campaign, the main hero and the main party exist: the vanilla helper below
           // reads Hero.MainHero and MobileParty.MainParty unguarded.
           var campaign = Campaign.Current;
           if (campaign == null || campaign.CurrentMenuContext != null) return false;

           var screen = MapScreen.Instance;
           if (screen == null) return false;
           if (screen.IsInMenu) return false;
           if (screen.IsInBattleSimulation) return false;
           if (screen.IsInArmyManagement) return false;
           if (screen.IsMarriageOfferPopupActive) return false;
           if (screen.IsHeirSelectionPopupActive) return false;
           if (screen.IsMapCheatsActive) return false;
           if (screen.IsMapIncidentActive) return false;
           if (screen.IsOverlayContextMenuEnabled) return false;
           if (screen.IsEscapeMenuOpened) return false;
           if (screen.EncyclopediaScreenManager?.IsEncyclopediaOpen == true) return false;
           if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out _)) return false;
           return true;
       }
   }
   ```

2. Replace the whole content of `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs` with:

   ```csharp
   using HarmonyLib;
   using SandBox.View.Map;
   using TaleWorlds.CampaignSystem.GameMenus;
   using TaleWorlds.Library;
   using TaleWorlds.Localization;
   using TAOM.Adapters;
   using TAOM.Core.Logging;

   namespace TAOM.Features.FiefManagement.Hooks;

   /// <summary>
   /// F6 on the campaign map opens the fief hub. The map-state and modal guards live in
   /// <see cref="MapMenuGate"/>, which the field camp's "Make camp" query shares; this postfix adds the MCM
   /// toggle, the key, the enlisted-attached refusal and the "no fiefs" message.
   /// </summary>
   [HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
   [HarmonyPatchCategory("Patch36_FiefManagement")]
   public static class Patch36_MapScreenF6
   {
       private static IFiefManagementSettingsProvider _settings;
       private static IMapScreenInputAdapter _input;
       private static IFiefHubService _service;
       private static Features.Enlistment.IEnlistmentStateQuery _enlistment;
       private static IModLogger _logger;
       private static bool _exceptionLogged;

       [HarmonyPostfix]
       public static void Postfix()
       {
           try
           {
               var settings = _settings ??= IoC.Resolve<IFiefManagementSettingsProvider>();
               if (settings == null || !settings.EnableFiefManagement) return;

               // The key first, so the gate and the vanilla helper it calls run only on a press.
               var input = _input ??= IoC.Resolve<IMapScreenInputAdapter>();
               if (input == null || !input.IsF6Pressed) return;

               // The one map-menu gate: vanilla MapScreen's full modal set (v1.4.8 MapScreen.cs:1355), the
               // escape menu, a live menu context and vanilla's map-action check. Codex review #38b caught that an
               // IsInMenu-only guard let F6 fire during modals that read the swapped MainParty.CurrentSettlement.
               if (!MapMenuGate.IsMapClearForMenu()) return;

               // Enlisted-attached service is a modal state of its own (parked at the wait menu; the menu guard
               // owns transitions): F6 must not push fief_hub into it. The other service states (duty, grace,
               // captive) are free-roam and keep F6.
               var enlistment = _enlistment ??= IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>();
               if (enlistment != null && enlistment.State == Features.Enlistment.Domain.EnlistmentState.EnlistedAttached) return;

               var service = _service ??= IoC.Resolve<IFiefHubService>();
               if (service == null) return;

               if (service.Count <= 0)
               {
                   InformationManager.DisplayMessage(new InformationMessage(
                       new TextObject("{=taom_fief_no_fiefs}You don't own any fiefs yet.").ToString(),
                       Colors.Yellow));
                   if (settings.IsDebugMode)
                       (_logger ??= IoC.Resolve<IModLogger>())?.LogInfo("[FiefManagement] F6 pressed but player owns no fiefs");
                   return;
               }

               if (settings.IsDebugMode)
                   (_logger ??= IoC.Resolve<IModLogger>())?.LogInfo($"[FiefManagement] F6: opening fief_hub menu (count={service.Count})");
               GameMenu.ActivateGameMenu("fief_hub");
           }
           catch (System.Exception ex)
           {
               // F6 polling fires every frame: never throw out of the postfix. Log once per process so a
               // recurring exception is visible without spamming the log.
               if (!_exceptionLogged)
               {
                   _exceptionLogged = true;
                   try
                   {
                       (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapScreenF6.Postfix threw: {ex}");
                   }
                   catch
                   {
                       // If even the logger resolution fails, swallow: never throw from a per-frame postfix.
                   }
               }
           }
       }
   }
   ```

3. `git show ca248d1b7:Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs > Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs`
   and `git show ca248d1b7:Main/Features/FieldCamp/UI/ICampMenuActivationQuery.cs > Main/Features/FieldCamp/UI/ICampMenuActivationQuery.cs`
   (both 1.4.x files equal the 1.5.x parent, checked this session). Then in the query file
   `(F6 and the Fiefs button use it too)` becomes `(F6 uses it too)`, and in the interface the two summary lines
   `/// escape menu, or a state vanilla's map-action gate refuses). Both this query and F6 and the` and
   `/// Fiefs button delegate to the one <c>MapMenuGate</c>.</summary>` become
   `/// escape menu, or a state vanilla's map-action gate refuses). This query and F6 both` and
   `/// delegate to the one <c>MapMenuGate</c>.</summary>`.

**Verify**: Build; `FullyQualifiedName~MapMenuGate` gives `Passed: 6`, `Failed: 0`;
`FullyQualifiedName~FieldCamp|FullyQualifiedName~FiefManagement|FullyQualifiedName~HarmonyPatchBindingTests` gives `Failed: 0`.

**C6.3 Docs.**
1. `docs/features/fief-management.md`: replace the "1. **F6 hotkey**" item with
   `1. **F6 hotkey**: `Patch36_MapScreenF6` adds a `Postfix` on `SandBox.View.Map.MapScreen.OnFrameTick`. Guards, in order: the `EnableFiefManagement` setting, `IsKeyPressed(F6)`, the shared map-menu gate `MapMenuGate.IsMapClearForMenu()` (vanilla `MapScreen.OnFrameTick`'s modal set plus the escape menu, a live menu context and `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`; the field camp's Make camp query uses the same gate), and not enlisted-attached. Activates the menu and shows the "no fiefs" message at the boundary; the service stays query-only.`;
   in Key files after the `Patch36_MapScreenF6.cs` row insert
   ``| `Main/Adapters/MapMenuGate.cs` | The one map-menu gate F6 and the field camp's Make camp query share |``;
   in Tests add `- `TAOM.Tests/Features/FiefManagement/MapMenuGateWiringTests.cs`: 5 source pins (the modal list lives only in `MapMenuGate.cs`, both consumers delegate, the full guard set and its order, F6 tests the key first and keeps the enlisted-attached refusal); `TAOM.Tests/Migration/MapMenuGateBindingTests.cs`: the vanilla helper's shape.`;
   Changelog first entry `- <today>: F6 and the field camp's Make camp share one map-menu gate that also refuses during the escape menu, a menu context and vanilla's map-action refusals (prisoner, raft, mission, encounter). Taken from the 1.5.x #789 change without its Fiefs button (plan 133).`
2. `docs/reference/harmony-patch-registry.md`, `## Patch36_FiefManagement`: in the paragraph under `**Target:**`,
   replace all text before the sentence that starts "The `CreateScreen` Prefix intercepts" (keep that sentence
   and everything after it) with
   `Fief-management hub. The `OnFrameTick` Postfix polls `IMapScreenInputAdapter.IsF6Pressed` and, on a press, opens the `fief_hub` game menu (`GameMenu.ActivateGameMenu`) when the player owns fiefs (a no-fief press shows an information message instead). It is gated on the MCM `EnableFiefManagement` toggle, then the one map-menu gate `MapMenuGate` (`Main/Adapters/MapMenuGate.cs`: `MapState` active, no menu context, not in menu, battle simulation, army management, marriage offer, heir selection, map cheats, map incident, overlay context menu, escape menu or encyclopedia, and vanilla's `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`; v1.4.8 `MapScreen.cs:1355`, `CampaignUIHelper.cs:2387`), then not enlisted-attached. The field camp's "Make camp" query uses the same gate. Codex review #38b caught that an `IsInMenu`-only guard let F6 fire during modals that read the swapped `MainParty.CurrentSettlement`.`
   The `CreateScreen` sentences after it stay.

Stage: `git add Main/Adapters/MapMenuGate.cs Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs Main/Features/FieldCamp/UI/ICampMenuActivationQuery.cs TAOM.Tests/Features/FiefManagement/MapMenuGateWiringTests.cs TAOM.Tests/Migration/MapMenuGateBindingTests.cs docs/features/fief-management.md docs/reference/harmony-patch-registry.md`

Stage list: those eight paths.

Message file `133-msg-C6.txt` (type `fix`, not the 1.5.x `feat`: only the gate fix is ported, not the button):

```
fix(fiefs): v2.0.28 - one map-menu gate for F6 and Make camp

F6 (the fief hub) and the field camp's Make camp button now ask one
map-menu gate, Main/Adapters/MapMenuGate.cs, before they push a game
menu onto the campaign map. Besides vanilla MapScreen's own modal set,
which both already copied, the gate refuses while the escape menu is
open, while the campaign has a menu context, and whenever vanilla's
map-action check refuses (prisoner, raft, mission or encounter). Two
copies of the list had drifted apart; there is now one. F6 keeps its
enlisted-attached refusal and now checks the key first.

Taken from the 1.5.x Fiefs button change (#789) without the button.

Research: taom-src v1.4.8 MapScreen :1355, CampaignUIHelper :2387
Not-tested: in game, F6 with the escape menu open and while a prisoner
Ported-from: ca248d1b709376b681b096371352b355ab6c1323
```

**Verify** (after the commit): Build; `FullyQualifiedName~MapMenuGate` gives `Passed: 6`; Docs command: `ai_dashes: 0`,
other values equal Step 0.7.

### Step C7: culture markets skip not-for-sale items (from `bc39f6e4d`)

Requires 0.6 (the XML comment edit touches `ModuleData`). Method: hand re-implementation, test first. On 1.5.x
the armour acquisition gate keeps NotMerchandise items off the stalls; 1.4.x has no such gate, so the pool scan
skips them.

**C7.1 RED.** Create `TAOM.Tests/Features/CultureMarketplace/ItemPoolAdapterMerchandiseTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// The culture-market pool skips items the Armory marks not for sale (is_merchandise="false", which v1.4.8
/// ItemObject reads into NotMerchandise, ItemObject.cs:154, :428-432, :498-502). Vanilla's own inflows skip them
/// too (WorkshopsCampaignBehavior.cs:993, DefaultTournamentModel.cs:101 in the v1.4.8 dump).
/// </summary>
[TestClass]
public class ItemPoolAdapterMerchandiseTests
{
    [TestMethod]
    public void IsMarketCandidate_MerchandiseItem_IsKept()
        => Assert.IsTrue(ItemPoolAdapter.IsMarketCandidate("test_item", notMerchandise: false));

    [TestMethod]
    public void IsMarketCandidate_NotMerchandiseItem_IsSkipped()
        => Assert.IsFalse(ItemPoolAdapter.IsMarketCandidate("test_item", notMerchandise: true));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsMarketCandidate_ItemWithoutAnId_IsSkipped(string? id)
        => Assert.IsFalse(ItemPoolAdapter.IsMarketCandidate(id, notMerchandise: false));

    [TestMethod]
    public void GetAllItems_AsksIsMarketCandidate_ForEveryItem()
        => StringAssert.Contains(ReadSource("Main/Adapters/ItemPoolAdapter.cs", stripComments: true),
            "IsMarketCandidate(item.StringId, item.NotMerchandise)");
}
```

**Verify**: Build fails with `CS0117` naming `IsMarketCandidate` (and no other error). That is the RED state.

**C7.2 GREEN.** In `Main/Adapters/ItemPoolAdapter.cs`:
- In `GetAllItems`, replace the line `if (string.IsNullOrEmpty(item.StringId)) continue;` with
  `if (!IsMarketCandidate(item.StringId, item.NotMerchandise)) continue;`.
- After `ItemExists` (before `ResolveByPrefix`), add:

  ```csharp
      // An item the XML marks is_merchandise="false" (v1.4.8 ItemObject.NotMerchandise, ItemObject.cs:154, set
      // at :428-432 and :498-502) is hero-only or troop-only kit; vanilla's own inflows skip it, and so does
      // the culture-market pool (the 1.5.x line does this through its armour acquisition gate, bc39f6e4d). The
      // routed guaranteed-stock pass adds items by id and does not ask this.
      internal static bool IsMarketCandidate(string? stringId, bool notMerchandise) =>
          !string.IsNullOrEmpty(stringId) && !notMerchandise;
  ```

**Verify**: Build; `FullyQualifiedName~ItemPoolAdapterMerchandiseTests` gives `Passed: 5`;
`FullyQualifiedName~CultureMarketplace|FullyQualifiedName~ItemPoolAdapter` gives `Failed: 0` (note its `Passed`
count N for the doc).

**C7.3 Docs and the config comment.**
1. `docs/features/culture-marketplace.md`: in the "Auto-derived item pool" bullet, after
   `scan every `ItemObject` in `MBObjectManager`` insert ` (skipping any item the Armory marks `is_merchandise="false"`, since plan 133)`;
   in the `ItemPoolAdapter.cs` Key files row append `; skips NotMerchandise items (`IsMarketCandidate`)`;
   in Tests add `- `TAOM.Tests/Features/CultureMarketplace/ItemPoolAdapterMerchandiseTests.cs`: 5 tests: the predicate keeps merchandise, skips NotMerchandise and id-less items, and the pool scan asks it.`;
   set the `Total: N tests across M classes` line to the C7.2 count N and M =
   `ls TAOM.Tests/Features/CultureMarketplace/*.cs | wc -l`; Changelog first entry
   `- <today>: the pool skips items the Armory marks not for sale (troll gear among them); stock a town already holds stays until bought (plan 133, from 1.5.x bc39f6e4d).`
2. `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml`, in the Mouth of Sauron comment,
   replace
   `This line's pool builder takes every item with a culture and honours only
       this blacklist, and nothing here wears the helm, so without the row a Mordor town could sell it`
   with
   `This line's pool builder skips is_merchandise="false" items only since plan 133
       (ItemPoolAdapter.IsMarketCandidate) and the 1.4.x Armory build may not mark the helm, so the row stays;
       nothing here wears the helm, so without the row a Mordor town could sell it`
   (keep the line breaks and indentation of the original comment).

Stage: `git add Main/Adapters/ItemPoolAdapter.cs TAOM.Tests/Features/CultureMarketplace/ItemPoolAdapterMerchandiseTests.cs docs/features/culture-marketplace.md Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml`

Stage list: those four paths.

Message file `133-msg-C7.txt`:

```
fix(marketplace): v2.0.28 - culture markets skip not-for-sale items

Culture markets no longer stock items the Armory marks not for sale
(is_merchandise="false"), such as the troll gear. The market pool took
every loaded item with a culture and honoured only its per-culture
blacklist, so hero-only and troop-only kit could turn up on a town's
stalls. Vanilla's own inflows already skip these items (workshop
output, tournament prizes); the pool scan now does too. Stock a town
already holds stays until it is bought.

The 1.5.x line reached the same result through its armour acquisition
gate, which this line does not have; this is the 1.4.x form of the fix.

Research: taom-src v1.4.8 ItemObject.NotMerchandise :154, :428-432
Not-tested: in game, no troll gear in new market stock
Ported-from: bc39f6e4d0f55a70d4fbdc6b515b5439145ced21
```

**Verify**: Data (label `C7`) compare prints `[]`.

### Part C done criteria

- [ ] Seven more commits (16 in total); Build; full Test `Failed: 0`, total = Part B total + 16
      (SpecialResourceMapBarMixinWiringTests 4, AffectRelativesGateTests 1, MapMenuGateWiringTests 5,
      MapMenuGateBindingTests 1, ItemPoolAdapterMerchandiseTests 5); Tool tests `Ran` = P0 + 10, no new failure vs Step 0.7.
- [ ] `git grep -nE 'affectRelatives\s*:\s*true' -- Main` prints nothing; `git grep -n "IsMapIncidentActive" -- Main` prints only `Main/Adapters/MapMenuGate.cs`.
- [ ] `git grep -n "_baseInitialized\|_itemAdded" -- Main/Features/SpecialResources` prints nothing.
- [ ] Docs command: `ai_dashes: 0`, other values equal Step 0.7.
- [ ] Hand-off: totals, tool-test `Ran` and hashes.

---

## Part D: tavern specialities and liege titles (string files)

Both steps rewrite the same 13 string files with one helper script. Write
`C:\Users\mikew\source\repos\taom-port\wp133-scripts\seed_strings.py` with your file-writing tool, exactly:

```python
#!/usr/bin/env python3
"""WP133: copy module-string rows from a 1.5.x commit into the 1.4.x English file and the 12 language files.

Run from the worktree root:
    python ../wp133-scripts/seed_strings.py tavern
    python ../wp133-scripts/seed_strings.py liege
It writes only the 13 string files. A line starting with STOP: and exit code 1 is a STOP condition.
"""
import re
import subprocess
import sys
from pathlib import Path

EN = "Main/_Module/ModuleData/taom_module_strings.xml"
LANGS = [
    "BR/std_taom_module_strings_por-BR", "CNs/std_taom_module_strings_zho-CN",
    "CNt/std_taom_module_strings_zho-HK", "DE/std_taom_module_strings_deu-DE",
    "FR/std_taom_module_strings_fre-FR", "IT/std_taom_module_strings_ita-IT",
    "JP/std_taom_module_strings_jpn-JP", "KO/std_taom_module_strings_kor-KO",
    "PL/std_taom_module_strings_pol-PL", "RU/std_taom_module_strings_rus-RU",
    "SP/std_taom_module_strings_spa-LA", "TR/std_taom_module_strings_tur-TR",
]
ID = re.compile(r'<string id="([^"]+)"')


def stop(message):
    print("STOP: " + message)
    sys.exit(1)


def source_lines(sha, path):
    raw = subprocess.run(["git", "show", f"{sha}:{path}"], capture_output=True, check=True).stdout
    return raw.decode("utf-8-sig").replace("\r\n", "\n").split("\n")


def insert_before_close(path, rows):
    p = Path(path)
    raw = p.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    newline = "\r\n" if "\r\n" in text else "\n"
    lines = text.replace("\r\n", "\n").split("\n")
    for row in rows:
        m = ID.search(row)
        if m and f'id="{m.group(1)}"' in text:
            stop(f"{path} already has id {m.group(1)}")
    closes = [n for n, line in enumerate(lines) if line.strip() == "</strings>"]
    if len(closes) != 1:
        stop(f"{path}: expected one </strings> line, found {len(closes)}")
    lines[closes[0]:closes[0]] = rows
    p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + newline.join(lines).encode("utf-8"))
    print(f"{path}: +{len(rows)} lines")


def tavern():
    sha, keys = "d39370059", ("taom_tavern_drink_default", "taom_tavern_food_default")
    rows = [l for l in source_lines(sha, EN)
            if "<!-- TavernSpecialities -->" in l or any(f'id="{k}"' in l for k in keys)]
    if len(rows) != 3:
        stop(f"English source: expected 3 lines, found {len(rows)}")
    insert_before_close(EN, rows)
    for lang in LANGS:
        path = f"Main/_Module/ModuleData/Languages/{lang}.xml"
        rows = [l for l in source_lines(sha, path) if any(f'id="{k}"' in l for k in keys)]
        if len(rows) != 2:
            stop(f"{path}: expected 2 source rows, found {len(rows)}")
        insert_before_close(path, rows)


def liege():
    sha = "30003668b"
    en = source_lines(sha, EN)
    first = next(n for n, l in enumerate(en) if "Liege titles for the TAOM-only kingdom cultures" in l) - 1
    last = next(n for n, l in enumerate(en) if 'id="str_become_king_empire"' in l)
    if "====" not in en[first]:
        stop("the English block does not start with the ==== comment line")
    kept, skipping = [], False
    for line in en[first:last + 1]:
        if re.search(r'id="str_liege_title(_female)?\.arthedain"', line):
            skipping = True
        if not skipping:
            kept.append(line)
        elif line.strip() == "</string>":
            skipping = False
    titles = sum(1 for l in kept if '<string id="str_liege_title' in l)
    if titles != 32 or any("arthedain" in l for l in kept):
        stop(f"English block: expected 32 title rows and no arthedain, found {titles}")
    insert_before_close(EN, [""] + kept)
    for lang in LANGS:
        path = f"Main/_Module/ModuleData/Languages/{lang}.xml"
        rows = [l for l in source_lines(sha, path)
                if ('id="taom_str_liege_title' in l or 'id="taom_str_become_king_empire"' in l)
                and "arthedain" not in l]
        if len(rows) != 33:
            stop(f"{path}: expected 33 source rows, found {len(rows)}")
        insert_before_close(path, rows)


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    {"tavern": tavern, "liege": liege}.get(mode, lambda: stop("usage: seed_strings.py tavern|liege"))()
```

(Dry-run this session on the 1.5.x blobs: the liege block keeps 169 lines with 32 title rows and 32 tags; every
language file yields 33 liege rows and 2 tavern rows; the English tavern source yields 3 lines.)

### Step D1: tavern maids offer a dish in every town (`d39370059`, #937)

Requires 0.6. Method: cherry-pick; the feature module ports as a module (D2). Simulated: unmerged
`Main/Composition/FeatureModules.cs`, the 13 string files and bookkeeping.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n d39370059; echo "exit=$?"; drop docs/INDEX.md docs/reference/feature-map.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reference/harmony-patch-registry.md Main/Composition/FeatureModules.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged. `git show d39370059:Main/Features/TavernSpecialities/TavernSpecialitiesModule.cs | grep -c "OwnsSaveData\|GameModels\|RegisterCampaignObjects\|Client"` prints `0` (else STOP, D2).

Edits:
1. `Main/Composition/FeatureModules.cs`: append `        new Features.TavernSpecialities.TavernSpecialitiesModule(),`
   as the LAST entry of `All`.
2. The patch file `Main/Features/TavernSpecialities/Hooks/TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch.cs`:
   `(v1.5.5, TavernEmployeesCampaignBehavior.cs:485-535)` becomes `(v1.4.8, TavernEmployeesCampaignBehavior.cs:729-820)`.
3. `TAOM.Tests/Features/TavernSpecialities/TavernSpecialitiesBindingTests.cs`: delete every
   `[TestCategory("RequiresGameIL")]` and `[TestCategory("RequiresGame")]` line.
4. `docs/features/tavern-specialities.md` (new, keep): `v1.5.5, `SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.cs:485-535`` becomes
   `v1.4.8, `SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.cs:729-820``; under Changelog add
   `- <today>: ported to the 1.4.x line (plan 133); the module runs through `Main/Composition` (plan 105).`;
   Status becomes `- **Status:** ported to 1.4.x; in-game check owed`.
5. `docs/reference/harmony-patch-registry.md`: insert directly above the first `## ` heading after the
   numbered `## PatchNN_` sections whose number exceeds 111 or which has no number (on the base that is
   `## Patch_MissionTime_SetMovementOrder`):

   ```
   ## Patch111_TavernSpecialities

   **Target:** `private bool SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.conversation_tavernmaid_offers_food_on_condition()` (v1.4.8 `:729-820`, no parameters, resolved by name with `AccessTools.Method`), one prefix class `TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch` in `Main/Features/TavernSpecialities/Hooks/`, category `Patch111_TavernSpecialities`, applied at GameInit by `TavernSpecialitiesModule` through the feature-module runner (`Main/Composition`). #937; the number matches the 1.5.x line.

   Vanilla sets the `DRINK` and `FOOD` text variables inside seven culture branches (`vlandia`, `empire`, `khuzait`, `aserai`, `sturgia`, `battania`, `nord`; each sets both) and nothing for any other culture, then returns true, so a TAOM town offered the previous town's dishes or the line with both names blank. The void prefix presets `DRINK` to a mug of ale and `FOOD` to a bowl of stew before vanilla runs; vanilla's branches overwrite both for exactly their ids, so no culture list is kept here. The strings carry the keys `taom_tavern_drink_default` and `taom_tavern_food_default`, registered in the module strings. `TavernSpecialitiesBindingTests` (`BindingVerification`) pin that the target resolves, that the class is a prefix with no postfix, and that the category applies and JIT-compiles.

   **Callers and thread:** the conversation condition delegate registered by the behavior, evaluated by the conversation system on the main thread at every conversation start that reaches the maid's offer line.

   **PatchShield and co-op.** PatchShield may wrap the method like any other patched non-TAOM method; its finalizer swallows only `MissingMethodException`, `MissingFieldException` and `TypeLoadException` (and strips the offending patches) and rethrows anything else. A void prefix, so `CoopVetoClassificationTests` needs no entry (it classifies bool prefixes only). Feature: [tavern-specialities.md](../features/tavern-specialities.md). **ACTIVE**, not yet checked in game.
   ```

6. Strings: `cd /c/Users/mikew/source/repos/taom-port/133 && python ../wp133-scripts/seed_strings.py tavern`.
   **Verify**: 13 lines, `taom_module_strings.xml: +3 lines` and `+2 lines` for each language file, no `STOP:`;
   `grep -c 'taom_tavern_drink_default\|taom_tavern_food_default' Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`
   prints `2` for each file.

Stage: `git add Main/Composition/FeatureModules.cs Main/Features/TavernSpecialities/Hooks/TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch.cs TAOM.Tests/Features/TavernSpecialities/TavernSpecialitiesBindingTests.cs docs/features/tavern-specialities.md docs/reference/harmony-patch-registry.md Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`

Stage list: `Main/Composition/FeatureModules.cs`, the two `Main/Features/TavernSpecialities/` files, the 13 string
files, the two `TAOM.Tests/Features/TavernSpecialities/` files, `docs/features/tavern-specialities.md`,
`docs/reference/harmony-patch-registry.md` (20 paths).

Message file `133-msg-D1.txt`:

```
fix(taverns): v2.0.28 - tavern maids offer a dish in every town

In a tavern of a Middle-earth culture's town, the tavern maid offered
"our local speciality" with the food and drink of the last town of a
vanilla culture, or with both names blank: v1.4.8 sets the two text
variables only inside its seven vanilla culture branches. She now
offers a mug of ale with a bowl of stew wherever the game has no dish
of its own; towns of the vanilla cultures keep their dishes.

A void prefix, Patch111_TavernSpecialities, presets the two variables
before vanilla runs. It arrives as its 1.5.x feature module, listed in
FeatureModules.All (decision D2). The two dish names are English in
every language until the translation pass.

Research: taom-src v1.4.8 TavernEmployeesCampaignBehavior :729-820
Not-tested: in game, the tavern maid in a Gondor town and a Rohan town
Ported-from: d39370059aa0ccc74b222d3b0def69d5b65c2a23
```

**Verify**: Build; `FullyQualifiedName~TavernSpecialities` gives `Passed: 8`;
`FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~Localization|FullyQualifiedName~PatchCategoryApplierTests`
gives `Failed: 0`; Data (label `D1`) compare prints `[]`.

### Step D2: every realm's ruler has a liege title (`30003668b`, #955)

Requires 0.6. Method: cherry-pick of the #955 half; the #956 half (Sauron facegen set in the Armory,
`CultureRaceConsistencyTests`, the Armory snapshot) is left out. Simulated: unmerged 13 string files,
`CultureRaceConsistencyTests.cs`, `crown-guard.md` (1.5.x-only) and bookkeeping. `csharp-patterns.md` merges
cleanly but lands under "Transpiler Note", so it is redone by hand.

```bash
cd /c/Users/mikew/source/repos/taom-port/133 && drop(){ for p in "$@"; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; }; git cherry-pick -n 30003668b; echo "exit=$?"; drop TAOM.Tests/Core/CultureRaceConsistencyTests.cs docs/INDEX.md docs/features/crown-guard.md docs/reference/feature-map.md docs/reference/lotrlome-armory-snapshot/README.md docs/reference/lotrlome-armory-snapshot/action_sets.xml docs/reviews/lessons/animation-skeleton.md docs/reviews/lessons/testing-qa.md docs/reviews/rca-coronation-fixes-2026-10-20.md docs/features/liege-titles.md .claude/rules/csharp-patterns.md Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml; git diff --name-only --diff-filter=U
```

**Verify**: nothing unmerged; staged: `Main/Features/LiegeTitles/TaomCultureTags.cs`,
`Main/_Module/ModuleData/comment_strings.xslt`, `Main/_Module/ModuleData/module_strings.xslt`,
`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`.

Edits:
1. `Main/Features/LiegeTitles/TaomCultureTags.cs`: delete the `TaomArthedainCultureTag` class (from
   `public sealed class TaomArthedainCultureTag : ConversationTag` through its closing brace) and the blank line
   after it. **Verify**: `grep -c "public sealed class Taom.*CultureTag : ConversationTag" Main/Features/LiegeTitles/TaomCultureTags.cs`
   prints `16`; `grep -ci arthedain` on it prints `0`.
2. `TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`: delete `[TestCategory("RequiresGame")]`;
   `Assert.IsTrue(wanted.Length >= 17,` becomes `Assert.IsTrue(wanted.Length >= 16,` (1.4.x has 16 TAOM-only
   kingdom cultures). It keeps `using TAOM.Composition;` (`FeatureModules`, from WP105).
3. `.claude/rules/csharp-patterns.md`: insert directly above `## Anti-Patterns`:

   ```
   ## Conversation tags

   A `ConversationTag` subclass anywhere in TAOM.dll must be concrete, non-generic, public-parameterless and carry a unique `StringId`: the `ConversationManager` constructor instantiates every one at each campaign start and load (v1.4.8 `ConversationManager.cs:239`, `Activator.CreateInstance` then `Dictionary.Add` on the id), so one bad tag breaks every campaign. No shared base class. The gate is `EveryTaomConversationTag_IsInstantiableAndUnique` (`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`).

   ```

4. Strings: `cd /c/Users/mikew/source/repos/taom-port/133 && python ../wp133-scripts/seed_strings.py liege`.
   **Verify**: `taom_module_strings.xml: +170 lines` and `+33 lines` for each language file, no `STOP:`;
   `grep -c 'tag_name="taom_culture_' Main/_Module/ModuleData/taom_module_strings.xml` prints `32`;
   `grep -c 'id="str_become_king_empire"' Main/_Module/ModuleData/taom_module_strings.xml` prints `1`;
   `grep -c 'liege_title.*arthedain' Main/_Module/ModuleData/taom_module_strings.xml` prints `0`.
5. Create `docs/features/liege-titles.md` with the content below (replace `<today>`).
6. `docs/ai-includes/new-culture-authoring.md`: after the `Bearer banner` row (B2) insert
   ``| **Liege title** | `Main/Features/LiegeTitles/TaomCultureTags.cs` and `Main/_Module/ModuleData/taom_module_strings.xml` | modified (+ a tag class and two tagged rows, then seed the language files; gated by `EveryKingdomCulture_HasMaleAndFemaleLiegeTitle`, see [liege-titles.md](../features/liege-titles.md)) |``.

`docs/features/liege-titles.md`:

```markdown
# Liege Titles

## Overview

Every kingdom culture has a liege title for its ruler, male and female: "Lord of Gondor", "Lady of the
Galadhrim", "King under the Mountain", "Dark Lord of Mordor". The coronation scene's title line and the lords'
dialog lines that name their liege ("I am Denethor II, Lord of Gondor") read it. Ported from the 1.5.x line
(#955) by plan 133.

## Why This Exists

- **Vanilla behavior (v1.4.8):** the text `str_liege_title` (and `str_liege_title_female`) has one variation per
  vanilla culture id, each tagged with that culture's conversation tag. `GameTextManager.LoadFromXML` splits a row
  id on `.` into the text id and the variation id and reads the row's `<tags>`. The engine reads the text two ways:
  - `GameTexts.FindText(id, variation)` matches the id suffix. The coronation scene
    (`BecomeKingSceneNotificationItem.TitleText`, dump `:23-34`) reads it this way with the kingdom's culture id,
    except for the `empire` culture, which reads `str_become_king_empire` instead (`:30`).
  - `ConversationManager.FindMatchingTextOrNull(id, character)` (`ConversationManager.cs:983`) ignores the suffix
    and scores each variation's tags: an untagged variation scores 0, a tag that does not apply rules the
    variation out. `LordConversationsCampaignBehavior.GetLiegeTitle` reads it this way for the faction leader.
- **TAOM requirement:** the 16 TAOM-only kingdom cultures had no variation, so the coronation title read an engine
  error ("ERROR: Text with id str_liege_title doesn't exist! Variation: ...", `GameTextManager.cs:91`) and the
  dialog lines got no title (#955). Dunland (`empire`) read vanilla's "Emperor of Calradia" line.
- **Without this feature:** the error text in every TAOM coronation, and empty liege names in dialogs.

## Architecture

A variation for a TAOM culture needs both an id suffix (for the coronation) and a tag of its own (for dialogs): an
untagged row would score 0 for every TAOM-culture leader and win for all of them. No vanilla tag tests a TAOM
culture, so `Main/Features/LiegeTitles/TaomCultureTags.cs` defines one `ConversationTag` per culture, `StringId`
`taom_culture_<culture>`, true when the character's culture is that culture. The engine finds them itself:
`ConversationManager.InitializeTags` (run by its constructor, `ConversationManager.cs:239`) instantiates every
`ConversationTag` subclass in the game assemblies at each campaign start and load. That is why each class is
concrete and parameterless with a unique id, and why there is no shared base class (it would be instantiated too);
the rule is in `.claude/rules/csharp-patterns.md`.

The six reused vanilla ids keep their vanilla rows, rewritten by `comment_strings.xslt` (Brenin of Dunland, King of
Dale, Taskral of Harad, Khudriag of Khand, King of the Mark, Loke-Kan of Rhun). Dunland's coronation line is
TAOM's own: `module_strings.xslt` removes vanilla's `str_become_king_empire` and `taom_module_strings.xml`
supplies "was hailed as Brenin of Dunland" under a TAOM key.

The coronation keys on the kingdom's culture and dialogs on the ruler's own culture, so a ruler of another culture
is crowned with one title and addressed with the other, as in vanilla.

## Configuration

None. The 32 rows are in `Main/_Module/ModuleData/taom_module_strings.xml` (keys `taom_str_liege_title.<culture>`
and `taom_str_liege_title_female.<culture>`), seeded in English in the 12 language files until the translation
pass.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LiegeTitles/TaomCultureTags.cs` | One conversation tag per TAOM-only kingdom culture (16) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 32 tagged title rows and Dunland's coronation line |
| `Main/_Module/ModuleData/module_strings.xslt` | Removes vanilla's Calradian emperor line |
| `Main/_Module/ModuleData/comment_strings.xslt` | The six vanilla-id titles (existing) |

## Tests

- `TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`: every kingdom culture has a male and female
  title, each TAOM row's only tag is its own culture's tag, every TAOM conversation tag instantiates with a unique
  id that no vanilla tag uses, Dunland's line replaces vanilla's emperor line, no title mentions Calradia.

## Adding a kingdom culture

Add a tag class to `TaomCultureTags.cs` and two rows (male, female) to `taom_module_strings.xml`, then seed the
language files (`/localize`). `EveryKingdomCulture_HasMaleAndFemaleLiegeTitle` fails until both exist.

## In-game check

The next coronation in a TAOM-only realm shows its title (for example "Lord of Gondor"), not the error text;
Dunland's says "hailed as Brenin of Dunland"; a first conversation with a Gondor lord names "Lord of Gondor"; a
Rohan lord still says "King of the Mark". Not yet checked in game.

## Changelog

- <today>: ported from the 1.5.x line (#955, plan 133).

## GitHub Issue

- **Issue:** #955 Coronation title shows an engine error for TAOM cultures
- **Status:** ported to 1.4.x; in-game check owed
```

Stage: `git add Main/Features/LiegeTitles/TaomCultureTags.cs TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs .claude/rules/csharp-patterns.md docs/features/liege-titles.md docs/ai-includes/new-culture-authoring.md Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`

Stage list: `.claude/rules/csharp-patterns.md`, `Main/Features/LiegeTitles/TaomCultureTags.cs`, the 13 string files,
`Main/_Module/ModuleData/comment_strings.xslt`, `Main/_Module/ModuleData/module_strings.xslt`,
`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`, `docs/ai-includes/new-culture-authoring.md`,
`docs/features/liege-titles.md` (20 paths).

Message file `133-msg-D2.txt`:

```
fix(titles): v2.0.28 - every realm's ruler has a liege title

The coronation title showed an engine error text for every TAOM-only
realm, Dunland crowned its ruler "Emperor of Calradia", and lords of
those realms named their liege with no title in dialog. Every realm
now has a title for its ruler, king and queen alike (Lord of Gondor,
King under the Mountain, Elvenking of Mirkwood, Dark Lord of Mordor
and the rest), and Dunland's ruler is hailed as Brenin of Dunland.

The 16 TAOM-only kingdom cultures each get a conversation tag class
(Main/Features/LiegeTitles/TaomCultureTags.cs) and two tagged rows:
the coronation picks a row by its id suffix, dialogs pick it by tag.
These are the first ConversationTag subclasses on this line; a test
pins that each one instantiates with a unique id. The titles are
English in every language until the translation pass.

Research: v1.4.8 ConversationManager :239, :983; GameTextManager
Not-tested: in game, a TAOM coronation, first talk with a Gondor lord
Ported-from: 30003668bd201bf7707f44f792689aab151ed45f
```

**Verify**: Build; `FullyQualifiedName~LiegeTitleOverrideTests` gives `Passed: 7`, `Failed: 0`;
`FullyQualifiedName~Localization` gives `Failed: 0`; Data (label `D2`) compare prints `[]`; Docs command
`orphan_features` equals Step 0.7 (the new docs are linked from the registry and the culture guide).

### Part D done criteria

- [ ] Two more commits (18 in total); Build; full Test `Failed: 0`, total = Part C total + 11
      (TavernSpecialities 8, LiegeTitleOverrideTests +3).
- [ ] `git grep -nE '^[^/]*"Patch111_TavernSpecialities"' -- Main` prints only `TavernSpecialitiesModule.cs` (the
      pattern skips the patch file's `///` comment that names the registry section);
      `git grep -n 'TryPatchCategory("Patch111' -- Main` prints nothing.
- [ ] `git grep -nci arthedain -- Main/Features/LiegeTitles TAOM.Tests/Infrastructure/Localization` prints nothing.
- [ ] Hand-off: totals and hashes.

---

## Disposition table

Every commit in the pack (11 groups, 19 commits). "Shared hunks" assigned to this WP by its notes: none beyond
these; `bd457ee6e` is what WP136's `f963c6188` needs (landed in C5). One hunk from outside the pack is assigned by
WP125's Maintenance note: the last row, `963218f7f`.

| 1.5.x commit | Subject | Group | Disposition |
|---|---|---|---|
| `1888b6f28` | fix(quickactions): Unequip All updates the open inventory | issue-917-quickactions-unequip | **Ported**, A1 (`PlayerEquipmentAdapter.cs` and the one-line `AliasedSites` row removal in `MainPartyNullCheckScanTests.cs` apply on WP112's files; RCA kept with v1.4.8 re-cites; lessons to WP142) |
| `89e7af6c0` | fix(equippresets): preset load refreshes the paper doll | inventory-sweep-2026-10-23 | **Ported**, A2 (Telchar's Mark hunk and its registry section 1.5.x-only, dropped; `RequiresGame` dropped, D9) |
| `eb89a3e53` | fix(quickactions): batch sells remove sold-out rows | inventory-sweep-2026-10-23 | **Ported**, A3 (clean) |
| `cd999f917` | fix(quickactions): drop the "include equipped" sell options | inventory-sweep-2026-10-23 | **Ported**, A4 (provider and counts adapted; review prompt dropped) |
| `97426dcd0` | fix(banner): inventory doll keeps the clan colours | inventory-sweep-2026-10-23 | **Ported**, A5 (doc hand-applied to the 1.4.x text; `patch-targets.md` left to WP137; review prompt and lesson dropped) |
| `399a7fe27` | docs: inventory fixes, docs and lessons catch up | inventory-sweep-2026-10-23 | **Ported**, A6 (registry hand-applied; feature map and lessons to WP142) |
| `b2e387db7` | fix(config): Review 130 follow-ups: list entries, formations | review-130-config-list-entries | **Ported**, B1 (`StrikeNames.cs` 1.5.x-only, dropped; `CustomAttacksUtils` comment hunks, its test comment and the spider RCA note skipped: optional, comment-only, engine claims checked on v1.5.x only; RCA rows, REVIEW-LOG, lessons, CHANGELOG dropped) |
| `21094307642` | fix(banners): Lindon and Blue Craig troops raise banners | issue-932-banner-bearer-cultures | **Adapted**, B2 (`wild_trolls` exception removed: no such troops on 1.4.x; scan verified on 1.4.x data; `lord-banners.md` 1.5.x-only) |
| `365f2ecfe` | fix(ui): clan colours reach portraits, refuge rows keep names | issue-993-994-banner-colors-refuge-rows | **Ported** banner half, **re-implemented** refuge half for v1.4.8, B3 (`ClanPartyItemVM.UpdateProperties` instead of `ClanPartyItemWithPartyVM`; `[ClientOnly]` and `ClientComposition` dropped, D7; honest-zero wage line kept because v1.4.8 binds it; RCA and lesson dropped; `patch-targets.md` to WP137) |
| `fee906345` | fix(resources): map-bar resource shows on the first frame | ui-data-sweep-2026-10-23 | **Ported**, C1 (applied to `Main/` by rename detection; test path pointed at `Main/`) |
| `406dd739f` | fix(resources): map-bar resource leaves when the hero loses it | specialresources-map-bar-entry | **Ported**, C2 (kingdom count re-derived from 1.4.x data; `gui-ui.md` re-cited to v1.4.8 only) |
| `8e7d1f4cb` | docs(gui): collection advice matches the ownership rule | specialresources-map-bar-entry | **Ported**, C3 (clean) |
| `645068836` | fix(relations): relation notes match the engine | ui-data-sweep-2026-10-23 | **Ported**, C4 (`IArmouryPlayerAdapter.cs` 1.5.x-only, dropped; cites v1.4.8) |
| `bd457ee6e` | fix(tools): kingdom generator keeps its block in place | ui-data-sweep-2026-10-23 | **Adapted**, C5 (test markers renamed off Arthedain and Dale; `dale-lord-expansion.md` and lesson dropped) |
| `60dbf6429` | docs(reviews): RCA and lessons for the UI and data sweep | ui-data-sweep-2026-10-23 | **Skipped**: the RCA records a 1.5.x review session (worktree, v1.5.5 decompile, a `--check` mode the 1.4.x generator lacks, 1.5.x id counts) and would state false facts on 1.4.x; its #947 lesson reaches 1.4.x through WP142's lessons sync; the fixes it reviews landed in C1, C4, C5 |
| `ca248d1b7` | feat(fiefs): a Fiefs button on the campaign map bar | issue-789-fief-nav-button | **Re-implemented** (gate only), C6: `MapMenuGate`, F6 and camp query routing, new tests. **Skipped**: the button and everything that serves only it (feature, out of scope), `FiefManagementNavItemVM` deletion, `CampaignHotPathSettingsProvidersTests` hunk, RCA |
| `bc39f6e4d` | fix(arena): troll gear unobtainable, prizes capped at heavy | arena-troll-gear-prizes | **Re-implemented** (NotMerchandise skip only), C7. **Skipped**: prize bands (balance), armour acquisition and `IMarketplaceStockGate` (1.5.x-only feature), troll-gear Armory edits, `CREATURE_GEAR_OBTAINABLE`, `lock_creature_gear.py`, `taom_schema.py` (Armory content policy, D13) |
| `d39370059` | fix(taverns): tavern maids offer a dish in every town | issue-937-tavern-specialities | **Ported** through its module (D2), D1 (registry section written for 1.4.x; INDEX, feature map, `patch-targets.md` dropped) |
| `30003668b` | fix(titles): every realm's ruler has a liege title | issue-955-liege-titles | **Ported** (#955 half), D2 (Arthedain tag and rows removed; threshold 17 to 16; liege-titles doc written for 1.4.x). **Skipped**: #956 (Armory Sauron facegen set, `CultureRaceConsistencyTests` refactor with its hard-coded `E:\Steam` path, Armory snapshot), `crown-guard.md` (1.5.x-only), RCA, lessons |
| `963218f7f` | chore: merge Shadowfax screen open speed | not in the pack (WP125 Maintenance, #993 re-check) | **Adapted**, B3.6 (only the `PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs` comment, by hand, and one paragraph of `docs/features/shadowfax-screens.md`; `[ClientOnly]` dropped, D7; the series itself is WP125's) |

## Test plan

- New or changed dotnet tests: Part A +6, Part B +39, Part C +16, Part D +11: **+72** over T0. Python tool tests:
  **+10** (`tools/tests/test_generate_new_faction_kingdoms.py`).
- New work is test-first: B3.3 (refuge decision tests, RED `Failed: 3`), C6.1 (map-menu gate, RED `Failed: 5`),
  C7.1 (NotMerchandise predicate, RED compile error).
- Structural patterns to copy: source pins through `RepoPaths.ReadSource(path, stripComments: true)`
  (`TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs` after C1); binding pins with
  `GameAssemblies.EnsureLoaded()` (`TAOM.Tests/Migration/MapMenuGateBindingTests.cs`); bare engine objects via
  `FormatterServices.GetUninitializedObject` (`SPInventoryVM_ArmorColor_PatchTests.cs`).
- Structurally untestable here (commit `Not-tested:` trailers): every Harmony invocation in a running game, the
  inventory screen's visuals, portraits, the clan screen rows, the map bar's first frame, the coronation scene,
  the tavern maid's line, market stock over days.
- Gates that cover the retargets automatically: `HarmonyPatchBindingTests` (every patch target, including the
  `TargetMethods()` constructor and the new Patch75/Patch111 classes), `ReflectionSiteBindingTests` (three new
  rows), `LanguageFileCoverageTests` (every new key present in all 12 language files).

## Done criteria (whole WP)

- [ ] 18 commits on `port148/133-campaign-fixes-inventory-ui` since the base, subjects exactly as in the steps,
      each with its `Ported-from:` trailer (`git log --format='%s%n%(trailers:key=Ported-from)' bannerlord-1.4.5..HEAD`).
- [ ] Build `0 Error(s)`; full Test `Failed: 0`; total = T0 + 72.
- [ ] Tool tests: no new failure vs Step 0.7, `Ran` = P0 + 10.
- [ ] Docs command: `dead_links`, `orphan_features`, `missing_features` equal Step 0.7; `ai_dashes: 0`.
- [ ] Data compares (B2, C7, D1, D2) all printed `[]`.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD | grep -E '^(Main/(IoC|SubModule)\.cs|Main/TAOM\.csproj|CHANGELOG\.md|docs/reviews/lessons/|docs/INDEX\.md|docs/reference/feature-map\.md|docs/reference/taleworlds-api-snapshot/patch-targets\.md|Client/)'` prints nothing.
- [ ] `git grep -nE 'TestCategory\("(RequiresGame|RequiresGameIL)"\)' -- TAOM.Tests/Adapters TAOM.Tests/Features/BannerColorPersistence TAOM.Tests/Features/TavernSpecialities TAOM.Tests/Infrastructure/Localization TAOM.Tests/Features/QuickActions` prints nothing.
- [ ] `git status --porcelain` in the worktree is empty.
- [ ] Hand-off message: status (which parts are done), T0 and the final total, P0 and the final `Ran`, the 18 short hashes, any deviation. The integrator records these in `plans/README.md`; no older plan's row is affected by this WP.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.3 or 0.4 fails (WP104, WP105, WP112 or WP125 missing, or another WP already landed part of this one).
- An anchor in 0.5 is missing and the live code differs in behaviour from "Current state".
- 0.6 shows the laptop Armory still at `v2.0.26`: do not stage any `Main/_Module/ModuleData` file (B2, C7, D1, D2).
- A cherry-pick leaves an unmerged path the step does not list, or stages a `Client/` path, or the staged list
  differs from the step's Stage list after the edits.
- A RED run (B3.3, C6.1, C7.1) does not fail exactly as described, or a GREEN run still fails after one fix
  attempt; any filtered or full test run fails twice after a reasonable fix attempt.
- The fix seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `.claude/settings.json` or
  `Directory.Build.props`.
- `TavernSpecialitiesModule` (as cherry-picked) declares `OwnsSaveData`, `GameModels`, `RegisterCampaignObjects`
  or references `Client`/`ClientComposition` (D2).
- `seed_strings.py` prints `STOP:`, or a string file already holds one of the new ids.
- `HarmonyPatchBindingTests` reports `UpdateCharacterArmorColor`, `ClanPartyItemVM.UpdateProperties`, the
  `OrderOfBattleHeroItemVM(Agent)` constructor or `conversation_tavernmaid_offers_food_on_condition` unresolved:
  the installed engine differs from v1.4.8 build 119303 (check `bin/Win64_Shipping_Client/Version.xml`).
- `ShippedConfig_MapsEveryInfantryTroopCultureToABanner` names a culture other than lindon or bluecraig as missing
  (troop data changed since planning): report the culture; adding a banner row for it is a data decision.
- A commit hook refuses a commit (subject label, staged CHANGELOG, ModuleData validation): read its message; do
  not bypass it with `--no-verify`.

## In-game checks owed (maintainer)

Logs: TAOM's `taom_debug_*.log` and the game's `rgl_log_*.txt`. A failed patch category shows in the startup
or game-init patch report inquiry and in the TAOM log.

1. Inventory: with gear equipped, open the inventory, Quick Actions, Unequip All: the slots empty, the items appear
   in the list, the doll, armour, weight and capacity update; Reset and then Cancel restore the gear. No
   `[QuickActions] inventory panel refresh failed` line.
2. Load an equipment preset with the inventory open: the doll and stats update at once; load one that equips the
   last item of a stack: that row disappears. No `[EquipPresets] inventory panel refresh failed` line.
3. Sell Low Value on a whole stack: the row disappears at once. MCM: "Sell Damaged Equipped" and "Sell Low Value
   Equipped" are gone.
4. Banner Color Persistence on, player clan with custom colours: the inventory doll shows clan colours on open,
   outfit toggle, Reset and preset load; a stealth set keeps the vanilla stealth colour.
5. Portraits show clan colours on the party screen, clan screen, hero panels and the Order of Battle hero pool
   (AI lords included).
6. Clan screen with a refuge being raised: its Garrisons row keeps "(being raised)" after the screen opens, the
   wage slider is hidden and the wage line reads 0.
7. Field battle with a Lindon and a Blue Craig formation led by no lord: bearers raise the archers' flag and the
   orcs' standard.
8. Load a campaign: the special resource counter appears with the map bar at once; switch to a hero with no
   resource (or leave the kingdom with a goblin/Misty Mountains/Blue Craig/Lindon hero): the counter leaves.
9. F6 with the escape menu open and while a prisoner: nothing opens; normal F6 still opens the fief hub; the
   field camp's Make camp button is disabled while a menu is open.
10. Watch a few towns' markets over several days: no troll gear or other not-for-sale item appears in new stock.
11. Tavern maid in a Gondor town: "a mug of ale with a bowl of stew"; in a Rohan town: vanilla's dishes.
12. A coronation in a TAOM-only realm shows its title (no "ERROR: Text with id" in the popup or `rgl_log`);
    Dunland's says "Brenin of Dunland"; a first talk with a Gondor lord names "Lord of Gondor".

## Maintenance notes

- **Shared files with other wave-4 WPs** (the integrator merges in the README order and resolves): 
  `docs/reference/harmony-patch-registry.md`, `ReflectionSiteBindingTests.cs`, `reflection-sites.md`,
  `Main/Composition/FeatureModules.cs` (append-only), the 13 string files (append before `</strings>`),
  `TaomSettings.cs`, `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, `coop-interop.md`,
  `bannerlord-together-compat.md` (A4 subtracts 2 from counts other WPs also move), `Main/Adapters/CommanderLordAdapter.cs`
  (WP128 edits other hunks), BannerBearers tests and `banner-bearers.md` (WP130's race gate),
  `docs/ai-includes/new-culture-authoring.md`.
- **Later WPs that rely on this one**: WP135 translates the new English placeholder keys
  (`taom_tavern_drink_default`, `taom_tavern_food_default`, `taom_str_liege_title.<16 cultures>`,
  `taom_str_liege_title_female.<16>`, `taom_str_become_king_empire`); WP136's `f963c6188` needs C5's
  `upsert()`; WP123's deferred Mixed Formations hotkey rule needs `EnumNames`; WP137 regenerates
  `patch-targets.md` (new rows for Patch111, `RefugeClanRowRefreshPatch`, the Patch23 retarget); WP142 syncs the
  dropped lessons and feature-map rows; WP143 reconciles the two `.claude/rules` edits.
- **Review focus** (`/deep-review`, recommended before merge since this touches C# in many files): the Patch23
  retarget (stealth guard and target binding), the `ref CharacterCode __result` postfixes (Harmony binds `ref` on
  a reference-type result), `RefugeClanRowRefreshPatch` running on every clan party row (one dictionary lookup per
  row), the F6 guard order, the D2 module wiring, and the 13 string files (no duplicate ids).
- **Deferred, on purpose**: the Fiefs map-bar button (feature); the troll-gear Armory policy and its validator
  (content decision for the Armory); the `CustomAttacksUtils` comment corrections from `b2e387db7` (comment-only;
  re-verify `Vec3.Normalize` and `Agent.HandleBlow` on v1.4.8 first); the 1.5.x RCAs this plan skips.
- **Skills to recommend** (executors cannot run them): `/deep-review` per part, `/verify-bindings` after Parts A
  and B (two retargeted targets, one new constructor target), `/localize` belongs to WP135.
