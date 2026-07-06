# Adversarial Review: race-persistence-legend (#330)

You are an adversarial code reviewer. Your job is to find real bugs in a small, high-stakes change to save/load persistence. Assume the author missed something; try to prove it. Verify every claim against actual source. Output findings with file:line evidence.

## Feature description

TAOM (Bannerlord 1.4.6 total-conversion mod) persists each hero's race across save/load because the engine does not. The race value is an int -- a position index into FaceGen.GetRaceNames(), which is the merged skins.xml race list in module load order. Issue #330: if that list is inserted into / removed from / reordered between save and load, every saved int silently re-points to a different race. The fix adds a "legend": at capture time the service snapshots the ordered race-name list as one ";"-joined string, synced under a NEW key `_taom_raceNameLegend` beside the existing `Dictionary<string,int>` under `_taom_heroRaceMap`. On restore, savedInt is translated legend[savedInt] -> name -> IRaceManager.GetRaceIdFromName(name) (current id). Empty/absent legend = pre-#330 save = legacy raw-int path unchanged. SyncRaceData now clears both fields when dataStore.IsLoading before calling SyncData (absent-key SyncData leaves ref values unchanged -- stale prior-session state would otherwise leak into an older-format save load).

Design constraint you should know: Dictionary<string,string> was deliberately NOT used because it failed to round-trip the engine IDataStore at ~1000 entries in another TAOM feature (WarOfTheRingMomentum, 2026-07-03, which now JSON-encodes to one string). The legend keeps the proven Dictionary<string,int> container and adds one small string.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Race names (from LOTRLOME_Armory skins.xml, appended after Native's races): dwarf, uruk, nazghul, orc, uruk_hai, berserker, cave_troll, hill_troll, pale_uruk, dg_uruk, goblin, elf, saruman, sauron. Race 0 is the Native "human".
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- Main/Features/HeroRace/RacePersistenceService.cs (the changed service -- READ EVERY LINE)
- Main/Features/HeroRace/RacePersistenceBehavior.cs (unchanged event wiring: OnNewGameCreatedEvent->ResetForNewCampaign, OnBeforeSaveEvent->CaptureHeroRaces, OnSessionLaunchedEvent->RestoreHeroRaces, SyncData->SyncRaceData)
- Main/Core/Domain/IRaceManager.cs + Main/Core/Domain/RaceManager.cs (new GetOrderedRaceNames + init-time _orderedRaceNames)
- Main/Adapters/IHeroRosterAdapter.cs + Main/Adapters/HeroRosterAdapter.cs (GetAllAliveHeroRaces / SetHeroRace boundary)
- Main/Adapters/FaceGenAdapter.cs (GetRaceNames wraps TaleWorlds.Core.FaceGen.GetRaceNames)
- TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs + TAOM.Tests/Core/Domain/RaceManagerTests.cs
- docs/features/hero-race.md (feature doc; its persistence section predates this change)
- Run `git diff` to see the exact delta -- only the 5 files above changed (2 production + 1 interface + 2 test).

## KNOWN SUSPECTS (CONFIRM or DISPUTE each, with evidence)

S1. Double-SyncData duplicate-key throw. The engine's save-side BehaviorSaveData._records.Add(key, data) throws on a duplicate key within one save pass. SyncRaceData syncs two keys once each; confirm no TAOM path can invoke SyncRaceData twice against the same IDataStore instance (grep all callers of SyncRaceData and RacePersistenceBehavior.SyncData).

S2. Clear-on-load data loss. SyncRaceData clears both fields whenever dataStore.IsLoading. Confirm the engine NEVER calls a behavior's SyncData with IsLoading==true at a moment where the in-memory map is the authoritative copy that must survive (e.g., mid-session reload flows, save-as-then-continue, campaign restart in same process). Decompile TaleWorlds.CampaignSystem CampaignBehaviorManager (OnBeforeSave/SaveBehaviorData/LoadBehaviorData) and the SaveHandler to enumerate every SyncData call site and its IsSaving value.

S3. Race-0 asymmetry between paths. The legacy path has a special case: savedRace != 0 gates the IsValidRaceId check (race 0 always restores). The legend path has NO race-0 special case -- legend[0] must resolve by name. If legend[0] is "human" and IsValidRaceName("human") is true this is fine; construct any modded scenario where race 0's name changes between save and load and decide whether the behavior (skip+warn, keep XML race) is correct or a regression vs legacy.

S4. Legend captured from an empty/fallback RaceManager. RaceManager falls back to ["human"] when FaceGen.GetRaceNames() returns null or throws, and stores an EMPTY array when GetRaceNames returns an empty array. If RaceManager initialized during a degraded state (editor mode? unit tests? cold start before FaceGen.CreateInstance?), CaptureHeroRaces writes a 1-entry or empty legend while heroes carry real race ints >0; on restore every non-human is out-of-range -> skip+warn -> heroes keep XML races. Confirm whether RaceManager can actually initialize before FaceGen is populated in a real game process (FaceGen.CreateInstance runs from the native OnLoadCommonFinished callback, before the initial screen -- see docs/features/hero-race.md). Is first-capture-time lazy init guaranteed to see the full race list? If a degraded legend CAN ship in a real save, is skip+warn acceptable or should capture refuse to write a legend shorter than the max saved race int?

S5. Backward compatibility with older TAOM builds. An old TAOM build loading a NEW save reads _taom_heroRaceMap (present, same container type) and never queries _taom_raceNameLegend. Confirm the engine tolerates an unqueried record in BehaviorSaveData (no strict-consumption check) and that the old build's behavior is exactly today's semantics.

S6. Restore-loop refactor regression. The old loop condition was `TryGetValue && hero.Race != savedRace` with the IsValidRaceId guard inside. The new loop does TryGetValue first, then branches legend/legacy, comparing hero.Race against the TRANSLATED id on the legend path and savedRace on the legacy path. Construct concrete (savedRace, hero.Race, legend, current-mapping) tuples and check both paths for: heroes skipped that should restore, heroes restored that should skip, warning spam for heroes whose translated id equals current race, and the restoredCount log accuracy.

## REQUIRED SECTIONS

### VANILLA CODE

Decompile (ilspycmd against E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/, or read E:/Decompiled_Bannerlord/ for browsing) and paste as code blocks:
- TaleWorlds.CampaignSystem.CampaignBehaviors.CampaignBehaviorManager -- OnBeforeSave, SaveBehaviorData, LoadBehaviorData
- The BehaviorSaveData / CampaignBehaviorDataStore class implementing IDataStore -- SyncData<T> body, IsSaving/IsLoading
- TaleWorlds.CampaignSystem.SaveHandler.SaveTick (OnBeforeSave-vs-write ordering)
- TaleWorlds.Core.FaceGen.GetRaceNames + TaleWorlds.MountAndBlade FaceGen ctor (race table construction)
- TaleWorlds.SaveSystem SaveableBasicTypeDefiner (string + Dictionary<string,int> registration)

### DEEP ANALYSIS (concrete scenarios)

For each scenario state PASS or FAIL with the exact code path:
A. Save on TAOM 2026-07 (legend written), LOTRLOME inserts a new race before "dwarf" in skins.xml, load. Expected: every dwarf hero restores as dwarf via name translation.
B. Save on TAOM 2026-07, user disables a hypothetical third-party race mod that occupied indices before LOTRLOME's block, load. Expected: name translation corrects all shifted ints; races whose names vanished skip+warn.
C. Pre-#330 save (no legend), load on new build. Expected: legacy path byte-for-byte (race-0 restores, invalid ints skipped via IsValidRaceId).
D. Pre-TAOM save (neither key), load. Expected: empty map, warning "No saved race data found", no restores.
E. Same-process sequence: play campaign 1 (new format) -> main menu -> load pre-#330 save. Expected: clear-on-load wipes campaign 1's map+legend; legacy path uses ONLY save 2's map.
F. Same-process sequence: play campaign 1 -> start NEW campaign. Expected: ResetForNewCampaign clears both; no restore happens on the new campaign.
G. Hero dies between save and load (map contains dead hero's StringId). Expected: GetAllAliveHeroRaces excludes them; entry is dead weight until next capture rebuilds the map. Confirm no leak/growth issue.
H. CC-created player hero with race set at CharacterCreation finalize -- confirm the capture->legend->restore round-trip preserves the player's chosen race under a shifted load-side mapping (the round-trip unit test models this; check the REAL production wiring matches the test's assumptions).

### CONFIG CROSS-REFERENCE

No config files changed. Confirm via git diff, then check that NO other TAOM feature persists race ints across save/load (grep SyncData callers for race-shaped data) -- if one exists, it has the same #330 bug and the review must flag it.

### FINDINGS OR OBSERVATIONS

Number every finding. Severity P1 (ship-blocking) / P2 (should fix) / P3 (nice-to-have). For each: file:line, the defect, a concrete failure scenario, and the minimal fix. If a Known Suspect is DISPUTED, say why with evidence. If you find nothing in a section, write "No findings" -- do not pad.

## QUALITY GATES

- Paste real decompiled code, not summaries, in VANILLA CODE.
- Every finding must cite file:line in TAOM source.
- Do not flag vanilla-matching behavior as a bug.
- Do not flag the deliberate Dictionary<string,int>+string design as "should be Dictionary<string,string>" -- see the design constraint above.
- Verify "missing X" claims by grepping before claiming.

## Prior review lessons

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output your review to stdout (it is redirected to docs/reviews/codex-adversarial-race-persistence-legend-2026-07-05.md).
