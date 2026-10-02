# RCA: FactionUI deep review (#704, Kysaro's TAOM_FactionUI port, 2026-10-01)

## Top line

The nine-lens `/deep-review` of the FactionUI port (uncommitted on bannerlord-1.5.x over `c17541f2`) found
four HIGH findings (one of them, a per-frame allocation, HIGH only by the efficiency lens's per-frame
rubric), 16 MEDIUM and a long LOW tail (each row below merges the same defect as several lenses
reported it), after Mike had deployed the first build and called it fantastic. Two of the HIGH defects were invisible in that look: the load-on-demand image release Mike
decided on never ran, because the patch that drove it sat on `GauntletLayer.ReleaseMovie`, a path the
engine never takes when a screen closes (Mike's own 14:14 log: one `images loaded` line, no `released`
line in about 20 minutes on the campaign map); and a hero picked and then un-picked left his whole skill
sheet on a custom character. The third disabled two face-generator steppers through a binding resolved in
the wrong data context. Every finding below is fixed, deliberately not applied with a reason, or owed to Mike. Mike chose
the hero-preset design (Option A) and delegated the rest ("do what you recommend").

Lens reports: the session scratchpad `review/lens*.md` (not committed). A pre-deploy audit (eight agents)
ran earlier the same day to answer "is a sprite bake needed?" (no); its minor findings overlap rows A1,
A6, A7, B8 and F5 below.

## Root-cause classes

**A. An engine seam taken from the donor or from a method's name, not from its callers.** The port
reproduced Kysaro's patch set and the claims beside it. Where his module assumed a seam (release on
`ReleaseMovie`, a Random cascade, a `DeactivateMenuMode` override), the port inherited the assumption,
and its unit tests called the service methods those seams were supposed to reach, so they proved the
bookkeeping and never the engine call. `harmony-patches.md` asks for every caller of a target only when a
patch infers an actor; a lifecycle hook (release, finalize, close) needs the same caller list for a
different reason: the hook is only as good as the set of paths that reach it.

**B. State opened in a UI flow with closers on the happy path only.** The session-reset rule was applied
(a second campaign starts clean, with tests), but the flow's other exits were never enumerated: backing
out of a pick, Custom Character after a pick, quitting to the main menu from the faction screen, turning a
setting off, a load that throws. Each left something behind: skills, a race, a latch for the process, a
screen and its heroes, an image hold.

**C. Config readers validated values but not keys.** NaN and range gates were in place; a misspelt key,
a wrong shape or an unknown faction was silently ignored, and one file had no summary warning.

**D. Gauntlet data-context and dead-binding defects in ported prefabs.** No test walked the faction screen
or the face-generator prefabs against their view models; a binding written on an element whose own
`DataSource` changes its scope resolved against the wrong view model and disabled a panel.

**E. Data authored literally from Kysaro's asks without modelling the copy.** The picker copies every
skill and both gear sets; legends listed three to five skills, archers lost their sidearm, and a hair-tag
trial had no engine reader for a hero.

**F. Per-frame and repeated work carried over from the donor.** Widgets polled in `OnLateUpdate`, files
were re-read per screen, the main menu loaded 75 images to draw 8.

**G. Donor structure kept where TAOM's architecture or an engine method already does it.** Sealed types
in a service, hand-rolled widget searches, copied tables, a second JSON parse.

**H. Completeness: docs, provenance, gates and sibling features.**

## Findings

Severity is the reporting lens's. Lens key: 1 Standards, 2fe/2cc Engine (front end, character creation),
3 Efficiency, 4 Completeness, 5fe/5cc Data flow, 6 Design, 7 XML.

### A. Engine seams

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| A1 | HIGH | The image release never ran: layers release their movies through `OnFinalize` -> `ClearContext` -> `movie.Release()`, never the patched `ReleaseMovie`; chrome (47.5 MB) and every viewed portrait stayed for the process, and the held identifiers pinned each screen's view model and widget tree | 2fe F1, 3 F1, 4 #1, 5fe T1, 6 P1 | `ReleaseMovie` patch deleted. `FrontEndMovieService` records each themed movie with the image groups it holds; `FactionUITicker` asks `IFrontEndStateAdapter.IsMovieReleased` (the engine's `IGauntletMovie.IsReleased`) once per tick and prunes; `FrontEndSpriteService.Tick(held)` frees a group 60 ticks after nothing holds it. A resource refresh, which releases and reloads a movie on the same identifier inside one call, is invisible to a per-tick poll. Tests: `Tick_AThemedMovieTheEngineReleased_GivesUpItsGroups` and four more |
| A2 | MED | `Texture.Release()` on a texture loaded from a path only drops the managed reference; vanilla's sheet unload uses `ReleaseImmediately` | 2fe (UNVERIFIED), 3 | `FrontEndResourceAdapter.ReleaseTexture` calls `ReleaseImmediately` behind an `IsReleased` guard, as vanilla does |
| A3 | LOW | The Random-button cascade could never fire: every backstory menu is one view, one movie and one button instance | 2cc F2, 5cc #5, 6 P3 | Cascade, its static flag and the stage table deleted; one random option and Next per click; docs and checklist reworded |
| A4 | LOW | The `DeactivateMenuMode` prefix re-implemented vanilla's own body (`MenuModeLeave` ignores the command when psai never entered menu mode) | 2fe F6, 6 P4 | Deleted with its service method, tests and co-op veto entry |
| A5 | LOW | "The theme returns in character creation" was wrong (`CharacterCreationState` is no music-menu state; the theme returns on the new-game loading screen) | 2fe F4, 5fe T6 | MCM tooltip, service doc and feature doc corrected |
| A6 | LOW | A missing themed prefab never throws (the movie builds empty, a blank main menu), and a build that throws left its root widget attached under the vanilla fallback | 2fe F3 | `IFrontEndResourceAdapter.HasPrefab` (`WidgetFactory.IsCustomType`) gates every swap with one warning; the finalizer removes what the failed build attached, then loads vanilla's movie and still starts the screen's effects (5cc #6) |
| A7 | LOW | The menu video sat in `Videos/initial_menu`, which vanilla scans in every module, so it played about one launch in nine with the setting off; the "already ours" guard compared against a root ending in `/` and never matched | 5fe T5 | Videos moved to `Videos/FactionUI/{menu,splash}`; guard deleted. The deployed folder still holds the old copy (checklist step 1) |
| A8 | LOW | The loading window's constructor set an image while hidden, decoding a painting (up to 31.6 MB) never shown | 3 F4 | The setter prefix returns while `LoadingWindowViewModel.Enabled` is false |
| A9 | LOW | "Hide Game Version" applies only at launch (`GauntletGameVersionView.Initialize` runs once) but said nothing; `RequireRestart = true` is forbidden by `SettingRequireRestartPostureTests` | 2fe F5, 5fe T4, 4 #9 | Tooltip states the restart; every front-end tooltip now says when it applies |
| A10 | LOW | Wrong engine claims in comments: Patch77 "prefixes" the constructor (it postfixes), "a UI debug reload" (it is a runtime module load), "every screen's movie load comes through" the public `LoadMovie` | 2cc F3, 2fe F7 | Corrected; the resource-refresh path got its own postfix on `UIResourceManager.Refresh`, which re-registers every image, font and brush before the open screens are rebuilt |
| A11 | LOW | The version line swapped on the movie name alone, against the class's own rule of name and view-model type | 5fe T11 | Requires `GameVersionVM`; `BeginLoad_GameVersionDrivenByAnotherViewModel_IsLeftAlone` |

**Why missed (class A):** the port was built to Kysaro's behaviour and verified by unit tests that invoked
the hooks' service methods directly; nothing asked which engine paths reach each hook, and the in-game look
cannot show a release that never happens. **Preventive action:** the lesson appended to
`lessons/harmony-il.md` (track a movie's lifetime by polling `IsReleased`; a lifecycle hook gets a written
caller list of every teardown path), and `FactionUIBindingTests` now pins the engine surfaces the port
depends on, including the swap table's nine vanilla movie and view-model names.

### B. State left behind by a UI flow

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| B1 | HIGH | Picking a hero copied his name and every skill when the face generator opened; backing out to Custom Character left the skills (Sauron's 280 to 330) on the new character | 2cc F1, 5cc #1, 6 P2 | Option A (Mike): the face generator copies only the look and gear; name and skills only at the 1060 finalize; the look before the first pick is captured once and restored on un-pick. Tests in `FactionPresetServiceTests` |
| B2 | MED | Patch9 snapped a picked hero's race to the culture's first race in the face generator (Sauron, the Nazgul, Saruman, Bolg, the Mouth) | 5cc #2 | Patch9 returns early while `FactionPresetService.HasPick` |
| B3 | MED | Quitting to the main menu from the faction screen popped the state without closing the culture stage: the screen, its heroes and the pick stayed, and the faction screen's widgets kept ticking | 5cc #3 | `SubModule.OnGameEnd` calls `FactionScreenLauncher.ResetForGameEnd` (screen, pick, Player Switcher) |
| B4 | MED | The faction screen called Player Switcher's process-lifetime failure latch, so after one faction-screen creation the switcher stayed off until restart, even with the picker turned off, and logged a warning | 1 S2, 4 #6, 5cc #7 | `IPlayerSwitchPolicyProvider.SetSuppressedForCharacterCreation`, set when the screen shows, cleared when it declines and on game end; tests in `PlayerSwitchPolicyProviderTests` and `FactionScreenLauncherTests` |
| B5 | MED | A wanderer's preview gear and granted gear were separate random draws with three different empty-gear fallbacks | 5cc #4 | One resolve per pick (`IPresetAppearanceAdapter.Resolve`) holds both gear sets, reused for display and both copies |
| B6 | LOW | An image hold leaked when a load threw after the count was taken, which after A1 would have pinned the chrome for the process | 5fe T10, 6 P1b | No counts: what is held is derived from the live movies each tick |
| B7 | LOW | Turning Themed Loading Screens off never released the last painting | 5fe T7 | `LoadingImageService` releases it when the setting is off |
| B8 | LOW | Dead references until the next screen of the same kind: the menu shimmer's widget arrays, the face generator's tabs and event subscription, the faction screen's card lists, a polled `ReadyToRender` on a closed face generator | 3 F9, 3 F1, 2cc U1, 5fe T12 | Each widget set detaches when its movie is released; `FactionScreenVM.OnFinalize` clears its lists |
| B9 | LOW | One `try` around all four ticker jobs, and a pending image released before it was removed from the list, so a throwing release would retry forever and starve the music and effects jobs | 5fe T9 | Three guarded jobs, each reporting once; `RemoveAt` before `Release` |

**Why missed (class B):** the session-reset walk covered entries (a new character creation, a second
campaign) and the latches rule's "closer coverage per opener path" was read as a mission-diagnostics
rule. Nobody listed the exits of a character-creation flow. **Preventive action:** the lesson appended to
`lessons/state-lifecycle-save.md`; `FactionScreenLauncherTests` and the preset tests now walk decline,
game end, un-pick and Custom Character.

### C. Config readers

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| C1 | LOW | `sprite_overrides.json` dropped bad entries and missing images silently and was re-read at every main menu | 1 S4, 4 #9, 3 F7 | Read once; a bad shape or a missing image is each warned once, then one summary warning (`ApplySkillIcons_AnySkippedEntry_EndsWithOneSummaryWarning`, `..._IsReportedOnceNotAtEveryMainMenu`, both proven red first; the summary was added by this RCA's own claim check) |
| C2 | LOW | `facegen_camera.json` ignored a misspelt screen, a non-object screen and an unknown offset key without a word | 1 S4, 4 #9 | Each warned; tests for distance, fov, the summary, misspelt and misshapen entries, notes |
| C3 | LOW | The four `faction_*.json` files had no summary warning, never checked that a key names a playable faction, and accepted a misspelt viewport property | 1 S4, 4 #9 | Summary warning; a viewport object may hold only `offset`, `hide_weapons`, `race`; the catalog warns once on a key that names no playable faction |
| C4 | LOW | The thirteen MCM booleans were mapped positionally, so a swapped pair would compile | 4 #3 | Named arguments; `FactionUISettingsProviderTests` flips each toggle and asserts only its own setting moves (proven by a deliberate swap, which it caught) |
| C5 | MED | Nothing tested that the config files' faction keys, character ids and kingdom ids, or the art table's card ids, resolve | 7 #6, 4 #7 | `FactionUIConfigIdsTests` (seven tests) |
| C6 | MED | Themed Skill Icons never applied with Themed Main Menu off: the apply sat after the menu's early return | 5fe T3 | Applied before it; `BeginLoad_MainMenuThemeOffSkillIconsOn_StillAppliesTheIcons` |

**Why missed (class C):** the config rule's numbered checklist is about values (range, NaN, ordering,
sign); unknown keys sit in a later paragraph about string fields. **Preventive action:** the lesson
appended to `lessons/testing-qa.md` covers the mapping test; recommended to Mike: promote "warn and skip a
key the schema does not know" to a numbered item of the config-provider rule (that rule file is over its
size budget, so it is his call where the line goes).

### D. Prefab bindings

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| D1 | HIGH | `TFGDetails.xml` put `IsEnabled="@IsEyesEnabled"` and `"@IsMouthEnabled"` on elements whose own `DataSource` is a `FaceGenPropertyVM`; the binding resolved there, read null, set false, and the eyebrow and teeth steppers could not be clicked | 7 #1 | `IsEnabled` moved to wrapper widgets with no `DataSource`, for all six sections |
| D2 | LOW | `ExecuteHoverDiag`, an empty debug handler, was bound in the faction screen | 1 S9 | Removed |
| D3 | LOW | Dead view-model properties: `HeroItemVM.Character` (a `CharacterViewModel` filled per card and never drawn), `FactionScreenVM.ScreenTitle`, `FactionItemVM.Name`, `FactionDetailVM.Name` | 3 F5, 5cc #10, this session | Removed; the last two were found by the new binding test |
| D4 | LOW | `TextHorizontalAlignment` (a brush property) and `FromTarget`/`ToTarget` on widgets that do not have them, both ignored by the engine | 7 #7, #8 | Removed, keeping the look Mike approved |
| D5 | LOW | The face generator's tab hotkey hints no longer matched the visible page; the main menu dropped vanilla's disabled-entry hints | 7 #9, #10 | Hint icons removed; the hint widgets restored |
| D6 | MED | Five unused prefabs and thirteen unused brushes | 7 #5 | Deleted (the deployed copies are in checklist step 1) |

**Why missed (class D):** no committed test walked these prefabs, and Gauntlet's rule that an element's
own attributes bind in the scope its `DataSource` sets is easy to miss (the faction screen's own comment
records the same lesson from Kysaro's side). **Preventive action:** `FactionScreenPrefabBindingTests`
walks the faction screen type-aware in both directions; the lesson appended to `lessons/localization-ui.md`.

### E. Data

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| E1 | MED | The eight legends listed three to five skills; the copy writes every skill, so a legend pick zeroed the rest | 7 #3 | Full 18-skill sheets (144 rows) |
| E2 | MED | Arwen's, Haldir's and Tauriel's new archer kits had no melee weapon | 7 #4 | Swords added |
| E3 | MED | The Denethor hair-tag trial had no reader: a hero's face comes from his fixed key | 7 #2 | Reverted; the doc explains why tags cannot change a lord |
| E4 | LOW | Five `perk_2` strings in `factions.json` still named vanilla cultures, and two descriptions lost their "+10%" | 4 #8, 7 #11 | Reworded; the figure restored |
| E5 | LOW | Gandalf barefoot; card names shortened ("Thorin" for "Thorin Oakenshield"); the foul-and-fair quote paraphrased and given to Frodo | 7 #12 | Shoes, full names, Aragorn's own line ("I look foul and feel fair") and attribution |
| E6 | LOW | The ranged ceiling gate never saw the legends' bows, which reach the player at runtime | 7 #13 | `tools/ranged_ladder.py` covers the picker file |

**Why missed (class E):** Kysaro's asks were applied as written, without reading what the copy does with
a partial sheet or what a replaced kit had carried. **Preventive action:** the lesson appended to
`lessons/data-content-cultures.md`.

### F. Efficiency

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| F1 | MED | The main menu decoded all 75 chrome images (47.5 MB) to draw 8 | 3 F2 | Its own image group (`mm_*`, 8.4 MB) |
| F2 | MED | Every viewed portrait stayed through the later stages, which draw none | 5fe T2 | Faction art is its own group, held only by the faction screen |
| F3 | HIGH (rubric) | Slider rows and theme icons ran `OnLateUpdate` every frame, allocating per row (20 to 32 small allocations a frame while the face generator is open) | 3 F3, 6 P8 | Setter-driven; neither widget joins the late-update list now |
| F4 | LOW | The faction JSON was parsed twice per culture stage; the video folders and font folders were listed per menu | 3 F6, F7 | The seam passes the faction map's parsed data; folder lists cached |
| F5 | LOW | Settings were read (13 MCM reads and an allocation) for every movie the game loads | 3 F8, 2fe F7 | Read only once a movie has matched |

### G. Structure

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| G1 | MED | `FactionRosterLookup` was a service holding sealed types, engine statics, a direct file read and untested rules | 1 S1 | `IFactionRosterAdapter` returns `RosterEntry` tokens; `FactionRoster` holds the rules engine-free, tested; wanderers come from the culture's own templates (6 P7, Rohan loses one never-spawned template) |
| G2 | LOW | The `TAOM.CharacterDisplay` overlay suppressor reflected on a module TAOM does not have and removed a layer without the safe teardown | 1 S3 | Removed; the question is Kysaro's |
| G3 | LOW | One decision ("the themed face generator will load") made in two places | 1 S8 | `FrontEndMovieService.WillThemeFaceGenerator` |
| G4 | LOW | Hand-rolled widget searches duplicating the engine's | 1 S9, 6 P9 | `GetFirstInChildrenAndThisRecursive` |
| G5 | LOW | Two literals that must agree, each written twice | 6 P10 | `LoadingSpritePrefix`, `MainMenuCustomizerService.NewGameName` |
| G6 | LOW | A copied alignment table and difficulty strings; a second parser for the backstory files, which could throw a non-JSON exception into a widget with no catch | 6 P5, P6, 4 #4 | `FactionData.Side`, `IFactionSelectionService.FormatDifficultyText`, `INarrativeDataProvider` (its per-file catch covers the malformed entry) |
| G7 | LOW | `PresetAppearanceAdapter` re-implemented `Equipment.IsEmpty` and `FillFrom` | 2cc F5 | The engine methods |
| G8 | LOW | File names matching no class; extra public types in other files | 1 S5 | One patch class per file; `SpecialCharacter`, `ViewportTweak`, `FactionScreenServices` in their own files |
| G9 | LOW | `ICultureStageMovieOverride` had one implementation and no fake | 1 S7 | `CultureStageViewFinalizeHookTests` fakes it and pins that the replacement is told when the stage closes; a second test meant to pin the close order could not fail and was deleted by the convergence pass (the order is not load-bearing) |

### H. Completeness

| # | Sev | Finding | Lens | Fix |
|---|---|---|---|---|
| H1 | MED | No provenance row for Kysaro's module; `GUI/FactionUI/RuntimeFonts/**` fell under TAOM's content licence by default | 4 #2 | Three register rows (code `behavioural-port`, layout and tuning `data-port`, art `redistributed`, all `UNKNOWN` until Kysaro confirms) with a detail section; the runtime fonts in `LICENSE-CONTENT.md` and the module's `THIRD-PARTY-LICENSES.txt` (the two OFL faces with their notices) |
| H2 | MED | The checklist missed loading screens, the menu toggles, the camera, the new kits; Player Switcher's and the faction map's docs did not mention the picker | 4 #5, #6 | Checklist rewritten (twelve steps, the stale deployed files first); both sibling docs updated; INDEX row |
| H3 | LOW | Missing gates: SubModule registration, IoC wiring, the 1050 < 1060 < 1100 order, the reflected members, the swap table's vanilla names | 4 #7, 1 S10, 2fe F2, 2cc F4 | `SubModule_AppliesInitializesTicksAndResetsPatch95`, `FactionUIWiringTests`, `FactionPresetHandlerPriorityTests`, four `ReflectionSiteBindingTests` rows with catalogue rows, two swap-table drift tests, `patch-targets.md` regenerated |
| H4 | LOW | A NaN test for the shimmer gate | 1, 4 #4 | `PositionAt_ATimeThatIsNotFinite_IsParked`; every animation clamps a non-finite frame time (`FrameTime.Sanitize`) |
| H5 | LOW | Stale status lines, two unlisted dropped patches, and "Themed Face Generator off means no preset gear" (the pick still dresses the model) | 4 #8, 5fe T13, 5cc #8 | Doc status, a not-ported table, the settings row corrected |

### Not applied, with reasons

- **Test names with two parts** (1 S6): a soft convention (18.8% of the repo's tests); the names read clearly.
- **Widget class names without `Widget`** (1 S5): they are the tag names in Kysaro's prefabs.
- **Retire FactionMap's `RuntimeSprite`** (6 P11) and **move FactionMap's eager resolves out of registration**
  (6 P12): pre-existing FactionMap code, outside the change; P12 touches the single-owner IoC.
  `FactionUIWiringTests.IoC_RegistersPlayerSwitcherAndTheFactionUIBeforeFactionMap` guards the order
  meanwhile.
- **`RequireRestart = true` for Hide Game Version** (5fe T4): forbidden by `SettingRequireRestartPostureTests`
  (MCM discards a restart-flagged change on Cancel); the tooltip says it instead.
- **Aragorn's civilian outfit** (7 #12): unchanged by design; the ask was his battle kit.
- **The Custom Battle round trip** (5fe T6): the theme may play under the menu video after it, as in Kysaro's
  module; documented as a known limitation, checklist step 12.

### Owed to Mike (public or his decision)

- Issue #704's body says Eomer's double definition and the vanilla names in `factions.json` are "fixed here
  too"; the first is reported, not changed. A correcting comment is public: Mike's word.
- A translation follow-up issue for the new strings (#703 and #695 are the pattern): public.
- Kysaro's written OK for the provenance rows; the portraits' resemblance to the film cast.

## Convergence pass

One reviewer on the applied fixes returned PASS (no HIGH or MEDIUM) after walking the redesigned flows
(image lifetime on every teardown path, including a texture reloaded after `ReleaseImmediately`, which it
traced through the native loader; Option A's eight exits; the suppression's readers). Nine LOW findings,
all fixed:

- **Three gates that could not fail.** The finalize test asserting the faction map's view model is gone
  never had one to begin with (deleted, G9 reworded). The wiring test replaced every adapter with a
  substitute, hiding a missing registration: a new test asserts the six registrations before any
  replacement, and dropping one makes it fail (checked). The SubModule source pin omitted the 1060
  behavior: one more assertion.
- **The IoC order this RCA said was guarded was not:** a source-order test now pins Player Switcher and
  the faction UI before FactionMap.
- **The faction screen with its prefab missing** logged "vanilla kept" and built an empty stage, and a
  throwing build retried the same prefab, leaving the retry's root widget attached. The launcher now
  checks the prefab and declines with one warning, and a swap with no vanilla movie
  (`MovieSwap.HasVanillaFallback`) rethrows to the launcher instead of retrying; both tested.
- **`facegen_camera.json`** counted only reverted values in its summary; ignored keys and screens count
  now, tested.
- **Wrong memory claims:** the last loading painting stays until the next loading screen (six of the ten
  are 3840x2160, 31.6 MB each, read from the PNG headers), and only image groups log their loads and
  releases; tooltip and doc corrected. Releasing the painting when the window hides is not done: the
  recorded decision is one painting at a time, and the change would touch the most-shown screen's timing
  for at most 31.6 MB.
- `SimpleItemVMs.cs` split into `TextItemVM.cs` and `BenefitItemVM.cs`; two counts in this RCA corrected.

## Codex pass

Codex (gpt-6-astra at ultra, one pass over the fixed tree after convergence) returned PASS: 0 P0, 0 P1,
2 P2, 0 P3, 0 false positives. It walked every requested scenario with installed-engine evidence,
disputed S1, S2, S3 and S6 with quoted code (every teardown reaches `Movie.Release`; the restore runs
before any later culture selection; all five readers of Player Switcher's policy are accounted for;
every config id resolves after applying `lords.xslt`, `spkingdoms.xslt` and `spcultures.xslt` to
installed vanilla in memory), partly confirmed S4 (F2) and confirmed S5 only narrowly. Both P2s were
re-verified against the v1.5.3 decompile before acting:

- **F1, P2 (re-rated LOW): widget effects keep the widgets a UI resource refresh replaces.** True:
  `GauntletUISubModule.RefreshResources(false)` reloads every movie under its old identifier through the
  private `LoadMovie`, past the public-load patch, and the effects classes cache widget references.
  But the refresh runs only when `_areResourcesDirty` is set, only `OnNewModuleLoad` sets it (option
  and language changes do not), and the engine allows a runtime module load only while the main menu
  is active (`MBInitialScreenBase` sets `SetCanLoadModules(true)` on activate, `false` on deactivate).
  So it cannot reach the faction screen or the face generator; on the main menu the title shimmer and
  the featured entry's styling stop until the menu next opens. **Not applied:** re-attachment code for
  an unreachable path plus a cosmetic, self-healing one fails the simplicity criterion; recorded as a
  known limitation in the feature doc.
- **F2, P2: a themed build that throws stays referenced through the factories' events.** True: the
  movie's constructor subscribes to `WidgetFactory.PrefabChange` and `BrushFactory.BrushChange`
  (`GauntletMovie.cs:52-53`), only `Release` unsubscribes, and a throw in `Instantiate` (`:100`) loses
  the movie to its caller. The engine hands back no reference, so releasing it would take reflection
  into the factories' event fields. **Applied instead:** a themed movie that failed to build is not
  tried again until restart (`FrontEndMovieService.LoadFailed`), and the faction screen's launcher does
  the same after any failure, so at most one movie per broken prefab is ever retained. Tests:
  `LoadFailed_TheSameScreenLater_StaysVanillaUntilRestart`, `LoadFailed_OneScreen_LeavesTheOthersThemed`,
  `TryLoad_AfterTheScreenFailedOnce_LeavesTheFactionMapWithoutTryingAgain` (all three red first).
- **UNVERIFIED, decline before restore:** the launcher could decline after a pick copied earlier in the
  same creation and leave it for the finalize handler. No supported route was shown (the toggle would
  have to change mid-creation), but `Decline` now clears the pick, which makes it hold by construction
  (`TryLoad_Declining_PutsBackTheLookAnEarlierPickCopied`, red first).
- **UNVERIFIED, raw engine geometry:** no harmful producer shown. The minimap click's size check became
  a positive requirement (`!(width > 0f)`), and `NearestFaction_ANonFiniteClick_SelectsNothing` pins that
  a NaN or infinite click selects nothing.
- **Comment:** the picker file said `lord_2_1` does not exist; it does, as vanilla's Bard II re-themed
  by `lords.xslt`. Corrected.

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|-------------------|
| F1 | Effects classes cache widgets a resource refresh replaces | Stale state / lifecycle | The refresh postfix restored the image, font and brush tables; nobody asked what else held references into the rebuilt trees, nor when a refresh can happen at all | Lesson `lessons/localization-ui.md`; documented limitation |
| F2 | A themed build that throws stays alive through the factories' events | Stale state / lifecycle | The finalizer removed the visible orphan (lens 2fe F3) and stopped there; the movie's other roots (its event subscriptions) were not read | Lesson `lessons/harmony-il.md`; the no-retry latch and its three tests |

## Why each lens caught what it did

- **Engine compatibility** proved A1 from the decompile and Mike's log, and found the skill leak (B1) by
  reading what the engine resets on a culture re-selection and what it does not.
- **Data flow** traced every flow to its closers and found B2 to B5 and the menu video's dead guard; it is
  the lens that turned "the release is untested in game" into "the release cannot run".
- **XML** found the data-context bug (D1) with a scratch resolver over 448 bindings, which no unit test did.
- **Efficiency** measured the art and chrome from the PNG headers and matched the engine's own figure.
- **Completeness** found the missing gates, the licence-scope hole and the stale claims.
- **Design** turned the two HIGHs into designs that remove their class (polling; Option A) rather than
  patching the symptom (a second release hook; a skill snapshot).
- **Standards** found the ADR-007 breach in the roster and the latch misuse.

## Lessons appended

- `lessons/harmony-il.md`: a patch on `GauntletLayer.ReleaseMovie` never sees a screen close; poll
  `IGauntletMovie.IsReleased`, and give a lifecycle hook a caller list of every teardown path.
- `lessons/state-lifecycle-save.md`: state opened in a UI flow needs a closer on every exit (back, un-pick,
  quit to menu, a setting turned off, a throwing load), not only a reset on entry.
- `lessons/localization-ui.md`: an element's own attributes bind in the scope its `DataSource` sets.
- `lessons/testing-qa.md`: a test that calls the method an engine hook should reach proves the bookkeeping,
  not the hook; and N same-typed settings need a flip-one-at-a-time mapping test.
- `lessons/data-content-cultures.md`: a copy that writes every skill zeroes the ones a template omits.
- After Codex: `lessons/harmony-il.md`, a Gauntlet build that throws cannot be released by the code that
  recovers from it, so never retry that prefab; `lessons/localization-ui.md`, a UI resource refresh
  rebuilds every movie under its old identifier, and it can happen only while the main menu is active.
