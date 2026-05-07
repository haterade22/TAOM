# Codex Adversarial Review: Career Selection in Character Creation

## Feature Description

TAOM adds a 6th narrative menu stage to Bannerlord's character creation that lets players choose their career (50 careers across 16 cultures). The menu is inserted after the adulthood stage via AddNewMenu with InputMenuId="narrative_adulthood_menu". Career options are filtered by the player's selected culture using EligibleCultureIds from taom_careers.xml. The selected career is stored in a singleton service and applied during CC finalization via ICareerCreationHandler.OnCareerSelected().

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- Main/Features/CharacterCreation/CareerMenuService.cs -- core service that builds the career NarrativeMenu
- Main/Features/CharacterCreation/CareerMenuDataProvider.cs -- loads career_menu.json
- Main/Features/CharacterCreation/CharacterCreationContentService.cs -- CC orchestrator, AssignCareer method
- Main/Features/CharacterCreation/TaomCharacterCreationContentHandler.cs -- entry point, calls RegisterCareerMenu
- Main/_Module/ModuleData/charactercreation/career_menu.json -- 50 career CC bonus definitions
- Main/_Module/ModuleData/career_system/taom_careers.xml -- 50 career definitions with EligibleCultures
- Main/Features/CareerSystem/ICareerCreationHandler.cs -- handler interface
- Main/Features/CareerSystem/CareerCreationHandler.cs -- handler implementation
- Main/Features/CareerSystem/CareerCampaignBehavior.cs -- OnSessionLaunched legacy fallback

## KNOWN SUSPECTS

1. SINGLETON STATE LEAK (ALREADY FIXED -- verify the fix is correct): CareerMenuService is Reuse.Singleton. SelectedCareerStringId was not cleared between CC sessions. We added SelectedCareerStringId = null at top of RegisterCareerMenu(). CONFIRM the fix is sufficient or DISPUTE if there are other paths that bypass RegisterCareerMenu.

2. EQUIPMENT ROSTER IDS: GetCareerMenuCharacterArgs constructs equipment roster IDs like "player_char_creation_{cultureId}_{titleType}_{f/m}". If these rosters don't exist in ObjectManager, Bannerlord asserts in ModifyMenuCharacters. Check whether the titleType fallback "guard" produces valid roster IDs for all 16 cultures. CONFIRM rosters exist or DISPUTE with specific missing cultures.

3. CAREER COUNT MISMATCH: career_menu.json has 50 entries. taom_careers.xml should have exactly 50 Career elements. Cross-reference every career_string_id in JSON against every Career id in XML. CONFIRM 1:1 match or DISPUTE with specific gaps.

4. CULTURE FILTER CORRECTNESS: Each career has EligibleCultures in XML (e.g., gondor + empire_w for Gondor careers). The onCondition lambda checks SelectedCulture.StringId against these. For XSLT cultures, the SelectedCulture uses vanilla IDs (vlandia, empire, aserai, etc.). CONFIRM all EligibleCultures in XML use correct vanilla IDs for XSLT cultures, or DISPUTE with specific mismatches.

5. BACK-NAVIGATION STALE STATE: If the player navigates back from career menu to adulthood then forward again, does the old SelectedCareerStringId persist until overwritten by a new selection? Is this acceptable (last-selection-wins) or a bug? CONFIRM or DISPUTE.

6. CC FINALIZATION ORDER: AssignCareer is called AFTER TeleportToStartingSettlement and SetPlayerRace. Does the career assignment depend on Hero.Culture being set? Check whether ICareerCreationHandler.OnCareerSelected reads Hero.Culture. CONFIRM safe or DISPUTE with the dependency chain.

## FILES TO REVIEW

### New Service Files
- Main/Features/CharacterCreation/CareerMenuService.cs
- Main/Features/CharacterCreation/CareerMenuDataProvider.cs
- Main/Features/CharacterCreation/ICareerMenuService.cs
- Main/Features/CharacterCreation/ICareerMenuDataProvider.cs
- Main/Features/CharacterCreation/Models/CareerMenuOptionDefinition.cs

### Modified Files
- Main/Features/CharacterCreation/CharacterCreationContentService.cs
- Main/Features/CharacterCreation/TaomCharacterCreationContentHandler.cs
- Main/Features/CharacterCreation/ICharacterCreationContentService.cs
- Main/Features/CharacterCreation/CharacterCreationIoC.cs

### Config Files
- Main/_Module/ModuleData/charactercreation/career_menu.json
- Main/_Module/ModuleData/career_system/taom_careers.xml

### Related Career System Files
- Main/Features/CareerSystem/ICareerCreationHandler.cs
- Main/Features/CareerSystem/CareerCreationHandler.cs
- Main/Features/CareerSystem/CareerCampaignBehavior.cs
- Main/Features/CareerSystem/ICareerRegistry.cs
- Main/Features/CareerSystem/CareerRegistry.cs
- Main/Features/CareerSystem/Domain/CareerDefinition.cs

### Test Files
- TAOM.Tests/Features/CharacterCreation/CareerMenuServiceTests.cs
- TAOM.Tests/Features/CharacterCreation/CareerMenuDataProviderTests.cs
- TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs

### Vanilla Decompilation Targets
- TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager -- AddNewMenu, TrySwitchToNextMenu, ApplyFinalEffects
- TaleWorlds.CampaignSystem.CharacterCreationContent.NarrativeMenu -- constructor, InputMenuId/OutputMenuId chain
- TaleWorlds.CampaignSystem.CharacterCreationContent.NarrativeMenuOption -- constructor, OnSelect, OnConsequence, ApplyFinalEffects

Use: ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "Full.Type.Name"

## REQUIRED SECTIONS

### VANILLA CODE
Decompile CharacterCreationManager.TrySwitchToNextMenu and CharacterCreationManager.ApplyFinalEffects from the installed DLL. Paste as code blocks. Verify:
- TrySwitchToNextMenu uses InputMenuId matching to traverse the menu chain
- ApplyFinalEffects calls OnCharacterCreationFinalize on all handlers
- ApplyFinalEffects calls ApplyFinalEffects on all selected NarrativeMenuOptions (skill/attribute bonuses)

### CAREER MENU CHAIN ANALYSIS
- Trace the menu chain: start -> narrative_parent_menu -> ... -> narrative_adulthood_menu -> narrative_career_menu
- Verify that adding a menu with InputMenuId="narrative_adulthood_menu" correctly inserts after adulthood
- Verify that TrySwitchToNextMenu returns false after career menu (no menu has InputMenuId="narrative_career_menu") to trigger finalization
- Check: does the career menu's ApplyFinalEffects get called? The career option's skill/attribute bonuses are set via getNarrativeMenuOptionArgs. Trace through ApplyFinalEffects to confirm these bonuses are applied.

### CONFIG CROSS-REFERENCE
- Compare every career_string_id in career_menu.json against Career id in taom_careers.xml
- For each career in XML, check EligibleCultures use correct culture IDs (custom or vanilla as appropriate)
- Check that skill names in career_menu.json are all valid (must be in: OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics, Crafting, Scouting, Tactics, Roguery, Charm, Leadership, Trade, Steward, Medicine, Engineering)
- Check that attribute names are all valid (must be in: Vigor, Control, Endurance, Cunning, Social, Intelligence)

### FINDINGS OR OBSERVATIONS
Group by severity: CRITICAL / HIGH / MEDIUM / LOW / INFO

## QUALITY GATES
- Did you decompile vanilla types from installed DLLs (not E:\Decompiled_Bannerlord)?
- Did you paste code blocks from both TAOM source and vanilla decompiled source?
- Did you cross-reference ALL config IDs against the cheatsheet?
- Did you trace the full lifecycle: menu registration -> option selection -> finalization -> career assignment?
- Did you check what happens on CC cancellation and restart?

## PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## OUTPUT TO
docs/reviews/codex-adversarial-career-cc-2026-04-14.md
