ADVERSARIAL REVIEW: FactionUI #704 (Kysaro's TAOM_FactionUI front end, ported into TAOM's Main module)

REVIEW ONLY. Do not modify, create or delete any file anywhere, including docs/reviews/raw/. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-faction-ui-2026-10-01.md, so never write that path yourself.

Feature: Kysaro, TAOM's lead UI designer, sent a compiled module (TAOM_FactionUI v0.1.0, DLL only) that reskins the splash, main menu, loading screens and every character-creation screen and adds a faction and hero picker that copies a named lord's or legend's look, gear, skills and name onto the new player character. It was decompiled and ported into TAOM's architecture (patch, service, adapter) as Patch95_FactionUI, uncommitted on branch bannerlord-1.5.x over c17541f2, Bannerlord v1.5.3. A nine-lens Claude deep review found 4 HIGH and about 20 MEDIUM findings; every one was fixed or accounted for. You are reviewing the FIXED working tree. The fix record, row by row, is docs/reviews/rca-faction-ui-2026-10-01.md.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
1. AGENTS.md, then .ai/review-reference.md (TAOM's review rules, severity and evidence format).
2. docs/features/faction-ui.md (design, settings, config files, the not-ported list, the in-game checklist).
3. docs/reviews/rca-faction-ui-2026-10-01.md (every finding and its fix; do not re-report a row as found unless the fix is wrong or incomplete).
4. docs/reference/harmony-patch-registry.md, section Patch95_FactionUI.
5. Main/_Module/ModuleData/FactionUI/*.json and Main/_Module/ModuleData/characters/faction_ui_picker_characters.xml.

DESIGN DECISIONS (the maintainer's; do not report them as defects, but do report a defect in how they are implemented)
-- A hero pick keeps Kysaro's outcome: a lord or leader ends up on the player with name, body, race, sex, gear and every skill; a named card or a wanderer template without the name.
-- Option A: the face generator copies only the look and gear (so it can show the pick); name and skills are copied only by the finalize handler at character-creation priority 1060 (after TAOM's 1050, before Player Switcher's 1100), which copies the look again. The look the character had before the first pick is captured once and restored when the pick is dropped.
-- While the faction screen is shown, it takes Player Switcher's place for that character creation (IPlayerSwitchPolicyProvider.SetSuppressedForCharacterCreation), cleared when the screen declines and on SubModule.OnGameEnd.
-- Images load on demand in three groups (main menu, character-creation frame, faction art), released 60 ticks after the last themed movie holding them is released; the loading-window frame, the three bitmap fonts and the skill icons stay for the process. No downscaling.
-- "Hide Game Version" applies after a restart; RequireRestart stays false because SettingRequireRestartPostureTests forbids true (MCM discards a restart-flagged change on Cancel).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code you read; cite file:line on both the TAOM and the engine side)
S1. Image holds drain on every teardown path. FrontEndMovieService records each themed movie object with its image groups; FactionUITicker.Tick asks IFrontEndStateAdapter.IsMovieReleased (GauntletMovieIdentifier.Movie.IsReleased) once per tick and prunes; FrontEndSpriteService.Tick(held) releases a group after 60 idle ticks. Hypothesis: some teardown leaves a held movie that is never reported released, so its group stays for the process. Walk: main menu to new game, to load game, to Custom Battle and back; each character-creation stage; the faction screen (loaded with layer.LoadMovie on the culture stage's own layer); quit to the main menu mid-creation (PopState without the culture view's finalize); a runtime UI resource refresh (GauntletUISubModule.RefreshResources: OnResourceRefreshBegin releases, OnResourceRefreshEnd reloads onto the same identifier within one call).
S2. Option A restore. FactionPresetService captures the look once (CaptureLook, at the first face-generator open after a pick) and restores it in Clear(). Hypothesis: the restore can put back a body or race captured under an earlier culture after the player has changed faction, or skip a restore that is owed (face generator opened twice; a template pick then a hero pick; finalize with only a pending pick). Check PresetAppearanceAdapter.CaptureLook/RestoreLook/ApplyLook/ApplyIdentity against what the engine's culture re-selection and TAOM's Patch9 race filter do afterwards (FaceGenVM_Refresh_RaceFilter_Patch exempts the face generator while FactionPresetService.HasPick).
S3. The Player Switcher suppression set when the faction screen shows lasts until a decline or OnGameEnd, which is after the campaign starts. Hypothesis: something Player Switcher gates on IPlayerSwitchPolicyProvider.Current.Enabled runs during the campaign and is wrongly switched off for that whole campaign. Enumerate every reader of Current.
S4. The movie-swap finalizer (Main/Features/FactionUI/Hooks/GauntletLayerLoadMoviePatch.cs). On a throw from a themed build it removes the root children the failed build attached, loads vanilla's movie through a re-entrant LoadMovie guarded by a [ThreadStatic] flag (which skips the prefix and postfix), then starts the screen's effects; for TAOM's own movie (the faction screen, MovieSwap.HasVanillaFallback false) it rethrows instead, and FactionScreenLauncher.TryLoad catches it and lets the faction map load. Hypothesis: a state the finalizer leaves inconsistent (the hold given back twice or never, effects started on a half-built tree, the identifier list of the layer, the exception swallowed when it should be rethrown, or the RethrowStackPreserver contract).
S5. Engine-sourced floats. Every animation clamps the engine frame time through FrameTime.Sanitize; the camera offsets come from facegen_camera.json through FiniteFloatValidator. Hypothesis: a float path that reaches a widget property, a camera or a Vec3 without passing one of those gates (ShimmerSweep, FactionScreenWidgets, CharacterCreationWidgets, MainMenuWidgets, BodyGeneratorViewInitCameraPatch).
S6. Config ids. The four faction_*.json files key by faction key and name character, kingdom and race values; FactionScreenArt names 34 card ids. Hypothesis: a key or id that resolves in tests but not in the game's merged data (XSLT-transformed lords, the picker characters' culture ids, the race index validated against FaceGen.GetRaceCount).

FILES
Patches (Main/Features/FactionUI/Hooks/): GauntletLayerLoadMoviePatch.cs, UIResourceManagerRefreshPatch.cs, VideoPlayerViewPlayVideoPatch.cs, VideoPlaybackStateSetStartingParametersPatch.cs, MBMusicManagerActivateMenuModePatch.cs, LoadingWindowViewModelLoadingImageNamePatch.cs, BodyGeneratorViewConstructorPatch.cs, BodyGeneratorViewInitCameraPatch.cs, FactionUIPatchContext.cs.
Services: Main/Features/FactionUI/Resources/{FrontEndSpriteService,FrontEndSpriteCatalog,FrontEndImageGroups,FrontEndSpriteKind}.cs; Main/Features/FactionUI/Menus/{FrontEndMovieService,MovieSwap,MenuMediaService,LoadingImageService}.cs; Main/Features/FactionUI/Presets/{FactionPresetService,FactionPresetRegistrationBehavior}.cs; Main/Features/FactionUI/FactionScreen/*.cs; Main/Features/FactionUI/CharacterCreation/*.cs; Main/Features/FactionUI/{FactionUITicker,FactionUISettings,FactionUISettingsProvider,FactionUIPaths,FactionUIIoC}.cs.
UI: Main/Features/FactionUI/UI/**/*.cs (view models, screen effects, custom widgets).
Adapters: Main/Adapters/{FrontEndResourceAdapter,IFrontEndResourceAdapter,FrontEndRuntimeSprite,FrontEndTexture,FrontEndStateAdapter,IFrontEndStateAdapter,MenuMusicAdapter,IMenuMusicAdapter,PresetAppearanceAdapter,IPresetAppearanceAdapter,FactionRosterAdapter,IFactionRosterAdapter,RosterEntry,TextLocalizerAdapter,ITextLocalizerAdapter,NinePatch}.cs.
Seams in other features: Main/Features/FactionMap/Hooks/{ICultureStageMovieOverride,CultureStageViewCreatedHook,CultureStageViewFinalizeHook}.cs; Main/Features/FactionMap/ViewModels/FactionSelectionVM.cs (ConfirmRegion); Main/Features/PlayerSwitcher/{IPlayerSwitchPolicyProvider,PlayerSwitchPolicyProvider}.cs; Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs; Main/Features/MainMenuCustomizer/MainMenuCustomizerService.cs (NewGameName).
Wiring (FactionUI hunks only; run git diff on these): Main/IoC.cs, Main/SubModule.cs (Patch95 init and TryPatchCategory, OnApplicationTick, OnGameEnd), Main/Features/TaomSettings.cs (the "Menus & Loading Screens" settings and the "Enable Player Switcher" hint), Main/Features/CoopInterop/CoopSettingsRelevance.cs.
Data: Main/_Module/GUI/Prefabs/FactionUI/*.xml (23 prefabs), Main/_Module/GUI/Brushes/TAOM{MainMenu,Loading,CharCreation,FactionScreen}.xml, Main/_Module/ModuleData/FactionUI/*.json, Main/_Module/ModuleData/characters/faction_ui_picker_characters.xml and its XmlNode in Main/_Module/SubModule.xml, Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_{rivendell,mirkwood,lothlorien,isengard}.xml, Main/_Module/ModuleData/characters/lords.xml, Main/_Module/ModuleData/named_companions/named_companions.xml, Main/_Module/ModuleData/factionmap/factions.json.
Tests: TAOM.Tests/Features/FactionUI/*.cs, TAOM.Tests/Features/FactionMap/CultureStageViewFinalizeHookTests.cs, TAOM.Tests/Features/PlayerSwitcher/PlayerSwitchPolicyProviderTests.cs, the FactionUI rows in TAOM.Tests/Migration/ReflectionSiteBindingTests.cs.

NOT IN SCOPE (another session's uncommitted work in the same tree; do not review): Main/_Module/ModuleData/Languages/**, tools/translation_cache/**, the five GameText XmlNodes in Main/_Module/SubModule.xml, taom_{battle_scene,career_data,character_name,culture_text,hero_text}_strings.xml, heroes.xml, npcs_lindon.xml, taom_xslt_strings.xml, the Dale armour files, lotrlome-beard-cover-changes.md, the taom_bc_advice_* keys. Two failing tests belong to that work: LanguageTextIntegrityTests.NoTranslatedString_MixesWritingSystems and NoCachedTranslation_MixesWritingSystems (the new strings are untranslated by the maintainer's decision).

REQUIRED SECTIONS

1. VANILLA CODE. Decompile from the installed v1.5.3 DLLs (the filesystem and ilspy MCP servers, or E:\Decompiled_Bannerlord\_categories_v1.5.3 for browsing) and paste as code blocks the members each suspect turns on: GauntletLayer.LoadMovie (both overloads), ReleaseMovie, OnFinalize, ClearContext, OnResourceRefreshBegin/End; GauntletMovie.Release and IsReleased; ScreenBase.RemoveLayer and HandleFinalize; UIResourceManager.Refresh and GauntletUISubModule.RefreshResources; MBMusicManager.ActivateMenuMode, DeactivateMenuMode and Update; LoadingWindowViewModel (Enabled, LoadingImageName, HandleEnable, SetNextGenericImage); the BodyGeneratorView constructor, OpenScene and InitCamera; VideoPlayerView.PlayVideo; VideoPlaybackState.SetStartingParameters; CharacterCreationManager (RegisterCharacterCreationContentHandler, ApplyFinalEffects, GoToStage); CharacterCreationCultureStageVM's culture re-selection; FaceGenVM.Refresh; HeroDeveloper.SetInitialSkillLevel; CharacterObject.GetBodyProperties for a hero.

2. DEEP ANALYSIS, concrete scenarios. For each, state the exact sequence of engine and TAOM calls and the end state:
a. Main menu (themed) -> Load Game -> campaign map: which groups are held at each step and when each is released.
b. New campaign -> faction screen -> pick Sauron (race sauron) -> Confirm -> face generator -> Previous -> faction screen -> Custom Character -> Confirm -> face generator -> finish creation: the player's race, sex, body, gear, skills and name at the end.
c. Same as b, but after Previous choose another faction (Gondor) and Custom Character.
d. Faction screen -> "Main Menu" button (or Esc) -> start a new campaign with the faction screen switched off in MCM: is Player Switcher offered? Is any image or pick left from the first attempt?
e. Themed main menu with Main Menu Video off: which video and which music play, and is anything themed still loaded.
f. A runtime UI resource refresh while the faction screen is open.

3. CONFIG CROSS-REFERENCE. Resolve every id in Main/_Module/ModuleData/FactionUI/*.json and FactionScreenArt.cs against the merged data (lords.xml plus lords.xslt over vanilla, the picker characters, kingdoms, cultures). Check the picker characters against the cheatsheet and TAOM's culture-race rules.

4. FINDINGS OR OBSERVATIONS. Every finding: severity (P0 to P3), file:line, the evidence you read (both codebases), a reproduction or proving scenario, and the minimal fix. Mark anything you could not prove UNVERIFIED. A finding that only restates an RCA row is not a finding unless the recorded fix is wrong.

QUALITY GATES
-- Cite the engine member you rely on for every engine claim; do not assume engine behaviour from a method name.
-- Before calling code dead, grep for its callers, Gauntlet prefab bindings (Command.*, @Property) and Harmony attribute targets.
-- Before calling an id broken, check XSLT output, not only the markup.
-- Do not report vanilla-matching behaviour as a TAOM bug.
-- Do not skip a suspect or a scenario; say UNVERIFIED with what you would need.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: your full report as the FINAL MESSAGE (sections 1 to 4 in order, then a one-line verdict: PASS if no P0 or P1 finding, else FAIL). Do not write any file.
