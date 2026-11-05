# V01: verification of SAVE-TIME-01 (save size grows with every in-session reload)

Run 2026-11-04-port148, verifier V01, adversarial and read-only. Claim under test:
`plans/_audit/2026-11-04-port148/deepdive/save-time.md`, SAVE-TIME-01 ("every in-session reload adds about 88,000
objects and 71,000 containers to the next save, permanently"), plus brief.md fact 1.

Refs read: working tree `c5b84fb41` (for C# this equals the public build `8df90444c`, which adds only a 4-line change
to `VolunteerRecruitmentService.Gondor.cs`), `origin/bannerlord-1.4.5` = `1b00d1acd`, `origin/bannerlord-1.5.x` =
`e45f6df2c`. Engine: the v1.4.8 dump `C:\Decompiled_Bannerlord\_categories_v1.4.8` ("dump") and the taom-src cache of
the installed DLLs `C:\Users\mikew\.taom-src\v1.4.8` ("installed"). Evidence: E1 = `SP\evidence\rgl_1744\rgl_log_1744.txt`,
E2 = `SP\evidence\rgl_22112\rgl_log_22112.txt` (one process, PID 22112), E3/E4 = the two taom_debug logs. Numbers in
parentheses after E1/E2 are grep line numbers in those files.

## Verdict: PARTLY-CONFIRMED

| Part of the claim | Status | Basis |
|---|---|---|
| Counts are flat within play and step up across in-session loads | CONFIRMED, high | 12 loads and 15 saves re-derived below; four observable steps total 12.0 increments of about 71,300 to 72,500 containers |
| The first load in a fresh process adds nothing | CONFIRMED | E1 (one load, two saves) and E2 load #1 |
| The load itself is the trigger, not the player's actions | CONFIRMED for loads #2 and #3 | game 2 and game 3 had no persuasion; game 3 had no mission, conversation or battle at all |
| "eight reloads of one byte-identical file gave eight jumps" | NOT PROVEN as worded | same section sizes, not bytes; no save between the eight loads, so only the total (8.0 increments) is visible, and the 19:40 save also follows a successful clan defection |
| "permanently" | NOT PROVEN | every carry-forward in the logs is inside one process, which cannot separate "carried in the file" from "re-attached from process state at each save" |
| The lens report's strong candidate class (a stale XML object drags the previous game's static world into the save) | REFUTED | in v1.4.8 those types save nothing beyond `MBObjectBase`'s three properties (section 3, candidate 1) |
| Mechanism | UNVERIFIED | no TAOM or vanilla holder found fits; the census in section 5 names the type |

## 1. Counts re-derived from the rgl logs

### E2 loads (all 12 completed: "Initializing saved game begin/end" pairs at 3935/3943, 9842/9850, 12314/12322, 20251/20259, 21547/21555, 23125/23133, 24400/24408, 25688/25696, 27233/27241, 28508/28516, 30081/30089, 32356/32364; no "Initializing new game" line exists)

| Load | LOAD STATISTICS lines | Time | Objects | Containers | Strings | File read (matched by section sizes) | Opened from |
|---|---|---|---|---|---|---|---|
| #1 | 3884-3890 | 17:21:56 | 1,302,023 | 1,026,584 | 1,218,989 | a save from an earlier session | main menu (GauntletInitialScreen 3869-3877, save/load screen 3880), fresh process |
| #2 | 9780-9786 | 18:03:20 | 1,301,837 | 1,027,153 | 1,202,673 | 17:56 "dunlandwar" (SaveDataSizeRecord 9725) | save/load screen over the map (9776) |
| #3 | 12252-12258 | 18:13:42 | 1,386,205 | 1,099,176 | 1,212,596 | 18:10 "uznash" (12185) | over the map (12248) |
| #4 to #11 | 20189, 21485, 23063, 24338, 25626, 27171, 28446, 30019 | 19:23:36 to 19:38:31 | 1,472,783 each | 1,171,471 each | 1,202,554 each | 19:12 "nyclan1" (20039, 20046, 20048) | over the map (20185, 21481, 23059, 24334, 25622, 27167, 28442, 30015) |
| #12 | 32294-32300 | 20:32:10 | 2,181,910 | 1,742,626 | 1,208,905 | 20:10 "goblins" (32140) | over the map (32290) |

All eight loads of the batch report the same six values (header 81,638,791, object count 1,472,783, object data
318,515,982, container count 1,171,471, container data 610,793,860, strings 1,202,554), which equal the 19:12 save's
SaveDataSizeRecord and SAVE STATISTICS. That makes "the same file" near-certain; the logs give sizes, not bytes.

### E2 saves (save names from the "SaveAsCurrentGame" / "AutoSaveCurrentGame" lines)

| Time | Name (line) | Objects | Containers | Collect::Containers s | Whole save s |
|---|---|---|---|---|---|
| 17:31 | recruit (5896) | 1,305,904 | 1,028,264 | 165.5 | 222.7 |
| 17:36 | nyclan1 (6171) | 1,305,740 | 1,028,195 | 59.5 | 114.7 |
| 17:44 | dunland2 (7814) | 1,300,960 | 1,026,479 | 47.2 | 91.5 |
| 17:56 | dunlandwar (9711) | 1,301,837 | 1,027,153 | 68.1 | 137.3 |
| 18:10 | uznash (12171) | 1,386,205 | 1,099,176 | 87.5 | 131.8 |
| 18:16 | rivenzinho (13507) | 1,475,675 | 1,171,631 | 139.8 | 220.2 |
| 18:23 | lugdush (14124) | 1,475,226 | 1,171,736 | 138.8 | 262.0 |
| 18:41 | woop (15260) | 1,475,235 | 1,171,673 | 76.1 | 175.3 |
| 18:50 | ny clan (16902) | 1,472,812 | 1,171,191 | 67.3 | 174.4 |
| 18:59 | saveauto2, autosave (17822) | 1,474,412 | 1,171,921 | 175.7 | 230.7 |
| 19:12 | nyclan1 (20025) | 1,472,783 | 1,171,471 | 166.7 | 244.4 |
| 19:40 | dunlandwar (31608) | 2,181,266 | 1,742,139 | 651.3 | 1092.4 |
| 20:10 | goblins (32126) | 2,181,910 | 1,742,626 | 601.2 | 948.5 |
| 20:34 | uznash (33633) | 2,344,661 | 1,885,054 | 490.7 | 968.9 |
| 21:18 | saveauto2, autosave (34360) | 2,343,646 | 1,884,752 | 1033.9 | 1460.8 |

Every row matches the lens report's table. Two by-products: the two autosaves are confirmed (230.7 s and 1,460.8 s),
and the player overwrote three named slots with bloated versions ("nyclan1" 17:36 then 19:12, "dunlandwar" 17:56 then
19:40, "uznash" 18:10 then 20:34).

### Increments

Containers move less with play than objects, so they are the cleaner series. Baseline: the 17:56 file that load #2 read.

| Save | Containers above 1,027,153 | In units of 71,500 | Loads in the process when saved |
|---|---|---|---|
| 17:56 | 0 | 0 | 1 |
| 18:10 | 72,023 | 1.0 | 2 |
| 18:16 to 19:12 (six saves) | 144,038 to 144,768 | 2.0 | 3 |
| 19:40, 20:10 | 714,986, 715,473 | 10.0 | 11 |
| 20:34, 21:18 | 857,901, 857,599 | 12.0 | 12 |

| Step | Objects | Containers | Object bytes per object | Container bytes per container |
|---|---|---|---|---|
| #2 (17:56 to 18:10) | +84,368 | +72,023 | 179.7 | 513.2 |
| #3 (18:10 to 18:16) | +89,470 | +72,455 | 210.3 | 472.8 |
| #4 to #11 (19:12 to 19:40) | +708,483 | +570,668 | 206.6 | 507.7 |
| #12 (20:10 to 20:34) | +162,751 | +142,428 | 215.9 | 399.3 |

Strings barely move with the steps (1,202,554 at 19:12, 1,208,931 at 19:40 against +708,483 objects), so the added
objects reuse strings already in the save, which fits duplicates of existing content.

"Loads in the process minus one" predicts 0, 1, 2, 10 and 11 increments; observed 0, 1, 2, 10 and 12. The #12 step
is two increments by containers, and its containers are smaller (399 bytes against 473 to 513), so the second
increment at #12 may be a different structure. Section 2 (c) gives the confounder.

### E1 (one load in a fresh process)

| Event | Lines | Objects | Containers | Collect::Containers s | Whole s |
|---|---|---|---|---|---|
| Load 19:39 | 3881-3887 | 1,071,840 | 832,466 | | |
| Save 19:45 | 5498, 5516, 5518 | 1,073,131 | 833,207 | 65.0 | 85.5 |
| Save 19:51 | 5923, 5941, 5943 | 1,073,861 | 833,528 | 34.1 | 52.3 |

## 2. Alternative explanations, tested

(a) A different or larger file. Refuted. Each load's six section values match the immediately preceding save of the
same session (table above).

(b) Campaign days. Refuted. Counts stay flat through long stretches of campaign time: game 1 from Winter 17, 1091 (daily
tick at 5087) to Summer 3, 1092 (9645), saves 1,305,904 to 1,301,837; game 3 from Autumn 1, 1092 (13463) to Spring 2,
1093 (19992), saves 1,475,675 to 1,472,783; game 12 from Spring 5 (33661) to Spring 15, 1093 (34300), 2,344,661 to
2,343,646. 107 daily ticks in the session; none produces a step.

(c) The player's own actions.
- Persuasion is not the trigger. No `lord_ask_recruit` line exists between the 17:56 save and the 18:16 save (lines
  9714 to 13510), yet both loads in that window added one increment. Game 1's successful recruitment ("The Gorkil
  changed from the Goblins to the Blackorcs", 6056, 17:35:29) was followed by three saves with no step.
- Load #3 isolates the load. Between the end of load #3 ("Loading Save Game", 13289, 18:15:04) and the 18:16:56 save
  (13510) the player opened the kingdom screen three times (13406, 13430, 13474) and let about 30 seconds of map time
  pass: 14 vanilla notable creations (`HeroCreator.CreateHero`, from 13443), deserter party merges, one daily tick
  (13463). No mission, conversation, battle or recruitment. The save still gained 89,470 objects and 72,455 containers.
- Confounder for the plus or minus one increment: two later recruitments succeeded seconds before a save. "The Awzalikä
  changed from the Harad to the Blackorcs" (31309, 19:40:31) precedes the 19:40 save; "The Zarûnik changed from the
  Gondor to the Blackorcs" (33552, 20:34:22) precedes the 20:34 save. Either load #12 added two increments, or a
  defection in a later game adds one and one batch load added none. These logs cannot separate the two.

(d) Plain engine behaviour on load. Not testable from these logs: E1 holds only a first load. It needs the vanilla cell
of the repro matrix (section 6).

(e) New confounder: an in-game load versus a second load in the process. Loads #2 to #12 were all made from the
save/load screen pushed over the campaign map, while the old game was still alive; load #1 came from the main menu in a
fresh process. So "in-game load" and "not the first load of the process" are confounded. Whether quitting to the main
menu before loading avoids the growth is untested.

## 3. Hunt for the holder

Constraints from section 1: C1 the holder outlives a game (first load adds nothing); C2 each increment has a fixed size
(about 71,500 containers, 85,000 to 90,000 objects) whether the abandoned world held 1.30 M or 2.18 M objects; C3 no new
strings; C4 it reaches the save only through saveable members, because the save walks from `Game`, whose saved members
are `_nextUniqueTroopSeed`, `GameType`, `PlayerTroop` and `RandomGenerator` (dump `Core/TaleWorlds.Core/TaleWorlds.Core/Game.cs:13-57`);
`MBObjectManager` is not saved and statics are never walked.

| # | Candidate | Evidence for | Evidence against | Verdict |
|---|---|---|---|---|
| 1 | Stale XML objects from an earlier game (the lens report's strong candidate class) | fits C1 | dump `ItemObject.cs`, `CultureObject.cs`, `BasicCultureObject.cs`, `BasicCharacterObject.cs`, `MBEquipmentRoster.cs` declare no `[SaveableField]`/`[SaveableProperty]`; only `MBObjectBase.StringId`, `Id`, `IsRegistered` are saved (`MBObjectBase.cs:11-25`). `CharacterObject` saves only `_heroObject` and `_originCharacter` (`Campaign/.../CharacterObject.cs:20-24`). A stale troop, item or culture is one leaf object; 88,000 objects would need 88,000 distinct stale XML objects, while TAOM defines about 3,774 characters and about 3,600 items (lens report table) | REFUTED |
| 2 | A stale campaign object (Hero, Clan, Settlement, party) from an earlier game written into current state | fits C1; `MBObjectManager.Destroy` only unregisters (`MBObjectManager.cs:370-374`, `ClearAllObjects` at 1461; `OnUnregistered` at `MBObjectBase.cs:113`), every field survives | its reach is the whole old campaign, on the order of a million objects, and it would grow with the old world's own extras; observed increments are constant (C2) | does not fit E2 |
| 3 | `MarriageClanPoolCache` (Patch81) | a real cross-game holder: `PoolByCulture` is static (`Main/Features/MarriageAlignment/MarriageClanPoolCache.cs:25`); the stamp clears only when (UniqueGameId, clan count, day) changes (`MarriageClanPoolStamp.cs:46-59`), and `UniqueGameId` is saved (dump `Campaign.cs:219-220`), so a same-day reload with an unchanged clan count serves the previous game's `Clan` objects. Vanilla `CheckNpcMarriages` (installed `RomanceCampaignBehavior`) then iterates the drawn clan's `AliveLords` and can call `MarriageAction.Apply` with one of them | fires only on a marriage roll after such a reload, and its result would be a whole-campaign jump (row 2), not a constant increment | not E2's carrier; a separate latent bug. Identical on all three refs (`git diff --stat` empty), no 1.5.x fix, so new work. Whether a stale clan passes `IsClanSuitableForNpcMarriage` and `ShouldNpcMarriageBetweenClansBeAllowed` is UNVERIFIED |
| 4 | Other TAOM statics typed with engine types | a Main grep found five: Patch42 `_checkRecruiting` (delegate), `SkillMap` in `CareerMenuService.cs:30` and `NarrativeMenuBuilder.cs:18` (resolver funcs), `Patch70_FiefGrantDecisionSwap.cs:57` `_faultReportedFor` (a whole `Campaign`), MarriageClanPoolCache, `SupplyCaravanService.cs:88` `_bearingSetter` (delegate) | none is written into saved state; static TextObjects (`AiPartySizeService.cs:21-25`, `TroopWeightService.cs:94`) are explanation labels | no. Patch70 pins an old campaign in memory until the next fault (memory lens), not save-reachable |
| 5 | Singleton services and adapters | `MobilePartyAttachmentAdapter._cachedCommanderParty` survives games (`MobilePartyAttachmentAdapter.cs:161-163`) | it only copies a position (`:197`); `StaleCharacterAdapter._found` is cleared per scan (`StaleCharacterAdapter.cs:71-72, 123`); TroopWeight caches are weak tables (`TroopWeightService.cs:23, 101`) | no |
| 6 | TAOM `SyncData` stores | | 31 `SyncData(..., ref x)` calls in Main, none names a static field (scripted scan); stores are id and primitive dictionaries (lens SAVE-TIME-05) | no (lens claim confirmed) |
| 7 | TAOM runtime registration or per-load creation | two runtime registrations exist: `TaomCulturalFeats.cs:1087-1088` and `StaleCharacterAdapter.cs:178-179` | CulturalFeats rebuild per campaign (`Campaign_InitializeDefaultCampaignObjects_Patch.cs:14`, `TaomCulturalFeats.cs:363-368`) and `CultureObject` saves no feats; the body-property registration runs only for stale characters. Creators such as `ClanBannerAdapter.cs:25`, `KingdomBannerAdapter.cs:22`, `CultureConversionAdapter.cs:152`, `CastleNotableMaintainer.cs:112` would also run on a first load (fail C1) | no |
| 8 | ButterLib Object System (TAOM.Dependencies, installed `Bannerlord.ButterLib.Implementation.1.4.5.dll`, decompiled read-only with ilspycmd 9.1) | `MBObjectKeeper` is the right shape: `Keep` writes `_references[mbObject.Id] = mbObject`, `Sync` saves `keepReferences` only when saving and never reloads it; `MBObjectExtensionDataStore.SyncData` calls it every save | keyed by `MBGUID`, so re-keeping an id overwrites; no TAOM code uses `IMBObjectKeeper`, `IMBObjectExtensionDataStore` or ButterLib object extensions (grep of Main and Dependencies) | no TAOM path; use by BetterTime or MCM UNVERIFIABLE (lens SAVE-TIME-09 confirmed) |
| 9 | SaveShield swallowing a load callback on a second load | `Dependencies/Foundation/SaveShield.cs:73-89` finalizes `LoadResult.InitializeObjects` and `AfterInitializeObjects` and swallows SAVE-LOAD faults outside co-op unless the disable flag exists (`:159-169`, `:249-300`). A callback that threw only on a later load would leave every later object unregistered (`MBObjectBase.OnBeforeLoad`, `MBObjectBase.cs:70-77`), XML would then build fresh instances beside them, and both could be saved: a fixed-size, second-load-only, carried-forward duplicate set | TAOM declares no load callbacks (grep); E2's rgl has no exception, duplicate-key, error or failure line other than sound events and `taom_body_length`; E3 and E4 each hold one load | coherent, no evidence. `LoadResult_InitializeCallbacks_Patch.cs:31-44` already stamps `phase=LoadFault`, so one player taom_debug from a reload session settles it |
| 10 | Vanilla statics and load path | | `ConversationManager._persuasion` is static (`ConversationManager.cs:87`) but `Clear` nulls it (`:1115-1118`) and `Campaign.OnDestroy` calls it with `MBTextManager.ClearAll` and `GameTexts.ClearInstance` (`Campaign.cs:1121-1133`); `Persuasion` is not saveable. `MBSaveLoad` statics hold no game objects (`MBSaveLoad.cs:11-39`). No `IObjectResolver` implementation exists in the dump (`TypeDefinition.cs:64-89` returns the loaded object). No `[SaveableProperty]` in the Campaign dump has a setter body (0 of 344 by scan; Hero saves backing fields), so in-game deserialization, which runs while the old campaign is still current (old map torn down only at 20224, callbacks at 20251), runs no game logic that could cross-link games | nothing found. Not an exhaustive enumeration of vanilla statics; vanilla stays possible (repro cell 4) |
| 11 | BetterTime | loaded by the player (E2 "Loading assembly ... BetterTime.dll") | not installed on this laptop (Modules or workshop) | UNVERIFIABLE here; the laptop repro is a no-BetterTime cell for free |
| 12 | 1.5.x group issue-628-race-fertility | live on 1.4.x: `ComputeBaseChance` multiplies the race bonus after the clan brake (`origin/bannerlord-1.4.5:Main/Features/RaceAge/Models/TaomPregnancyModel.cs:91-98`); fix in 1.5.x `83bdad853` (code and tests), RCA in `925db38d1`; the E2 player's kingdom is orc-kin ("Blackorcs") | births are play-time growth, and E2 counts are flat over the long stretches in section 2 (b) | not the per-load step; a slow campaign-days source worth porting as a bug fix |
| 13 | Vanilla state that never prunes (lens SAVE-TIME-04) | | long-term growth only | not re-verified; not per-load |

## 4. Server GC lead (batch 30 note 4)

What the installed v1.4.8 holds (read-only):
- `.exe.config` files exist only for `bin/CrashUploader.Publish/CrashUploader.Publish.exe` (a binding redirect) and the
  third-party BLSE launchers `bin/Win64_Shipping_Client/Bannerlord.BLSE.{Launcher,LauncherEx,Standalone}.exe` (each sets
  only `legacyCorruptedStateExceptionsPolicy`). `Bannerlord.exe`, `TaleWorlds.MountAndBlade.Launcher.exe`,
  `Bannerlord.Native.exe` and `Launcher.Native.exe` have none. Nothing sets `gcServer` or `gcConcurrent`.
- The install supports three runtime hosts:
  1. `Bannerlord.exe` and `TaleWorlds.MountAndBlade.Launcher.exe` are managed PE32+ assemblies (CLR header present).
     Their `Main` forwards to `TaleWorlds.Starter.Library.Program.Main`, which P/Invokes `TaleWorlds.Native.dll`
     `WotsMainSDLL` in the same process (dump `Core/TaleWorlds.Starter.Library/.../Program.cs:34-91`, `MBDotNet.cs:11-13`);
     the official launcher runs the game in its own process after Play (`Launcher.Library/.../Program.cs:107-112`).
     Under .NET Framework, a `<gcServer>` element in that host's `.exe.config` is read at CLR start (standard .NET
     Framework behaviour, not tested in game).
  2. `Launcher.Native.exe` is a CoreCLR host: its strings name `coreclr.dll`, `coreclr_initialize`,
     `TRUSTED_PLATFORM_ASSEMBLIES`, `coreclr_create_delegate` and `TaleWorlds.MountAndBlade.Launcher.Library.Program`
     `NativeMain` (`Program.cs:46`). The runtime ships in `bin/Win64_Shipping_Client/Microsoft.NETCore.App/` (.NET 6.0.12
     per its `deps.json`). A host that starts CoreCLR through `coreclr_initialize` reads no `.exe.config`; GC mode comes
     from the properties the host passes or from runtime environment knobs.
  3. `TaleWorlds.Native.dll` also carries `WotsMainNative` with Mono (`mono_jit_init`; `mono-2.0-sgen.dll` is installed).
- Neither player log says which host ran. The rgl "Mono Loading Step" and `IMono_*` lines are engine naming and print on
  every path. The v1.4.8 dump contains no `GCSettings` use, so nothing sets GC mode in code.

Verdict: UNVERIFIED that any config the player could edit is honoured, because the player's host is unknown. Not
recommended. Two more reasons for caution: server GC keeps per-core heaps and normally raises memory use, on machines
already at 80 to 88 % memory load in battle (E3/E4 MemSample); and the 1.5.x measurement (2.6 to 2.9 s down to 1.05 s)
was on another engine with a far smaller save. Cheapest way to settle the host: TAOM already records `Environment.Version`
in crash bundles (`Main/Features/CrashReport/Collectors/ProcessEnvironmentCollector.cs:79`; 4.0.30319.x means .NET
Framework, 6.0.x means CoreCLR) and logs `gcServer` and `latencyMode` from EditorCacheRebuild
(`RuntimeCacheRebuildService.cs:210-211`); add the same line at startup (probe A4).

## 5. Minimal probe that names the source on the next player session

What exists on 1.4.x (Patch61 SaveLoadDiagnostics, seen in E3 lines 24-164 and E4 21-3260): LoadRequested
(`SandBoxSaveHelper_TryLoadSave_Patch.cs:30`), LoadDataOk, ObjectsInitialized and LoadFault
(`LoadResult_InitializeCallbacks_Patch.cs:31-53`), GameLoaded and GameInitializationFinished with memory tokens, SaveBegin
(`SaveManager_Save_Patch.cs:17-22`) and SaveCompleted (`SaveOutput_PrintStatus_Patch.cs:36-48`). `seq` restarts per
attempt (`SaveLoadDiagnosticsService.cs:84-98`). It records no counts, no memory at save, no loads-per-process and no
load origin.

Tier A, always on, a few field reads per save or load, every hook try/catch like Patch61:
- A1. Static `LoadsThisProcess`, bumped in `BeginLoadAttempt`, and `InGameLoadsThisProcess`, bumped when `Game.Current`
  is non-null at LoadRequested. Append `loadsThisProcess=N inGame=true|false` to LoadRequested and
  `loadsThisProcess=N inGameLoads=M` to SaveBegin and SaveCompleted.
- A2. `ProcessMemoryTokens.Format()` (`Main/Features/BattleLoadDiagnostics/ProcessMemoryTokens.cs:16-29`) on SaveBegin
  and SaveCompleted.
- A3. Postfix on the private no-argument `SaveContext.CollectObjects()` (installed `TaleWorlds.SaveSystem.Save.SaveContext.cs:175`,
  it runs right after the object walk; target it with `Type.EmptyTypes`, the one-argument overload also exists) reading
  `_childObjects.Count`, `_childContainers.Count`, `_strings.Count` (`:75`, `:79`, `:83`) through cached
  `AccessTools.FieldRefAccess`, logging `phase=SaveCollected objects= containers= strings= collectMs=`. Postfix on
  `LoadContext.Load(LoadData, bool)` (installed `LoadContext.cs:54`) reading `_objectCount`, `_stringCount`,
  `_containerCount` (`:11-15`) as `phase=LoadCounts`. `SaveContext.Save` does not clear the lists (`:328-363`).
- A4. One startup line: `RuntimeInformation.FrameworkDescription`, `Environment.Version`, `GCSettings.IsServerGC`,
  `GCSettings.LatencyMode`.

Tier B, the census, O(n), in the same CollectObjects postfix, only on the first save after a load where
`LoadsThisProcess >= 2`, or when a `taom.save_census` console command (route through `TaomConsole`) arms the next save:
- B1. Per-type counts of `_childObjects` and per-type counts plus summed element counts of `_childContainers`
  (`ICollection.Count`, `Array.Length`); top 30 of each.
- B2. For `MBObjectBase` objects, per type: instances with `IsRegistered == false` (internal; cached accessor) and
  instances that are not the object `MBObjectManager.Instance.GetObject(MBGUID)` returns for their id. No per-object
  allocation.
- B3. One `[SaveCensus]` line per type, sorted, plus the pass time.
- Cost (estimate, not measured): two passes over about 2.3 M objects and 1.9 M containers at tens of nanoseconds per item,
  so well under a second against E2 saves of 91.5 to 1,460.8 s. It runs before `CollectSaveDatas`, so it adds nothing to
  the save's peak heap. What it names: the types whose counts jump by about 85,000 to 90,000 objects or 71,500 containers
  between a fresh-process save and a save after one reload. Unregistered or shadowed `MBObjectBase` duplicates would
  confirm the stale-copy class (and SaveShield row 9); anything else points elsewhere.

Tier C, the referrer, opt-in only on the maintainer's repro: postfixes on `ObjectSaveData.GetChildObjects` and
`ContainerSaveData.GetChildObjects` (verify both signatures with taom-src first) recording, for the first N children of
the type tier B named, the parent's type, plus an edge count per parent type. The walk is breadth-first from `Game`, so the
first parent recorded lies on a shortest path, which names the field that holds the duplicates. It costs a type check per
edge, millions per save, hence opt-in.

Gates: unit tests for the census aggregation and the formatters (pure functions); binding tests in the
`HarmonyPatchBindingTests` pattern for the overload and the six field names; in game, one save prints SaveCollected with
the same counts as rgl's SAVE STATISTICS, and a reload then save prints `loadsThisProcess=2` and a census.

## 6. Repro matrix (laptop, v1.4.8, any TAOM campaign save; BetterTime is absent here, so every TAOM cell is also a no-BetterTime cell)

1. Fresh process: load save S, save to slot A. Then load S from the in-game menu and save to slot B. Compare ObjectData
   and ContainerData Length in the rgl. Reproduced if B exceeds A by tens of thousands of containers.
2. Fresh process: load S, save A, quit to the main menu, load S, save B. Separates "in-game load" from "second load in
   the process".
3. Fresh process: load slot B from cell 1 and save at once to slot C. C about equal to B means the growth is carried in
   the file (permanent); C about equal to A means it is re-attached from process state, and a restart repairs a save.
4. Vanilla only (Native, SandBoxCore, Sandbox, StoryMode, CustomBattle) with a vanilla save, steps of cell 1. Separates
   TaleWorlds from mods.
5. With BetterTime, cell 1, only if cells 1 to 4 do not reproduce.

## 7. Player advice that is safe today

1. Do not reload inside a running session. To retry something, exit to the desktop, start the game again, then load.
   Basis: a first load in a fresh process added nothing (E1, E2 load #1); every in-game load added an increment,
   including load #3, which had no player action. It costs about a minute per retry. Quitting only to the main menu is
   untested.
2. Do not count on an already large save shrinking. Whether a restart removes the growth is unknown. To check, start the
   game fresh, load it, make one manual save to a new slot (expect it to take as long as recent saves), and compare the
   two file sizes in `Documents\Mount and Blade II Bannerlord\Game Saves`. Never overwrite the only copy.
3. Older saves of the same campaign are smaller (in E2 the 17:44 "dunland2" save held 1.30 M objects against 2.34 M in
   the 21:18 autosave), but going back loses progress. Only for extreme cases.
4. Lengthen the autosave interval and save by hand at quiet moments: E2's autosaves took 230.7 s and 1,460.8 s.
5. Send both logs (`rgl_log_*.txt` and `taom_debug_*.log`) with any report.

Not advised, unverified: editing an `.exe.config` for server GC, removing BetterTime from a running campaign, editing
save files.

## 8. Corrections to the lens report and other docs

- SAVE-TIME-01: "byte for byte" should read "the same six section sizes". "Each cycle in E2 contained a lord-recruit
  persuasion" is false for loads #2 and #3, which is what rules persuasion out as the trigger. "Permanently" is not
  shown by any evidence in hand. "Eight reloads gave eight jumps" is eight increments in total over eight reloads and
  one successful defection. The stale-XML "static world" candidate class is refuted (section 3, row 1). Add the
  in-game versus main-menu load confounder and load #3 as the isolating case.
- SAVE-TIME-02: equal-content `TextObject`s do not collapse in general. `GetHashCode` returns the per-instance
  `_internalId` (dump `Core/TaleWorlds.Localization/TaleWorlds.Localization/TextObject.cs:23-25, 71-76, 164-168,
  264-267`) and the save's `Dictionary<object,int>` uses the default comparer (`SaveContext.cs:114-125`), so distinct
  instances hash apart. Only instances that share an id collapse, in practice `GetEmpty()` ones with id 0 (`:64-69,
  170-173`), as batch 30 note 4 says.
- SAVE-TIME-09: confirmed; add that the keeper is keyed by `MBGUID`, so re-keeping overwrites.
- `docs/reference/bannerlord-engine-and-toolchain.md:186` says the managed code runs on Mono. The import table proves
  only that Mono is linked; v1.4.8 ships three hosts (section 4) and the one players use is unknown.

## 9. Skills to recommend (I cannot invoke them)

- `/investigate` on SAVE-TIME-01 once tier A and B are in a build, then the repro matrix; also on the
  MarriageClanPoolCache staleness (row 3).
- `/research` on `ObjectSaveData.GetChildObjects` and `ContainerSaveData.GetChildObjects` before tier C.
- `/verify-bindings` after adding the SaveContext and LoadContext patches.
- `/issue` for the probe, for the MarriageClanPoolCache bug and for the #628 port (issue-first rule).
