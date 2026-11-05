# Deep-dive: save time growth (vanilla SaveContext Containers phase)

Run: 2026-11-04-port148. Lens: SAVE-TIME. Read-only investigation of the 1.4.x line (working tree c5b84fb41;
the 52 drifted files read from origin/bannerlord-1.4.5) on Bannerlord v1.4.8, with the 1.5.x line used for leads and
fixes to port. Findings are appended as they are confirmed; the summary is at the end.

## Findings (appended as found)

### Evidence base: what E1 and E2 actually measure (read 2026-11-04)

The vanilla save prints three things per save, all present in both rgl logs: the five `PerformanceTestBlock`
phase timings, a `[SaveDataSizeRecord]` line (`SaveContext.cs:23-28`, v1.4.8 dump), and a SAVE STATISTICS
block whose `ObjectData Length` and `ContainerData Length` are the object and container COUNTS. Every load
prints the same counts as LOAD STATISTICS. That turns "saves get slower" into a volume series:

| E2 save (line) | objects | containers | uncompressed MB | Collect::Containers s | Saving Objects s | Saving Containers s | whole save s |
|---|---|---|---|---|---|---|---|
| 17:31 (5898) | 1,305,904 | 1,028,264 | 857.6 | 165.5 | 2.9 | 32.9 | 222.7 |
| 17:36 (6173) | 1,305,740 | 1,028,195 | 857.4 | 59.5 | 41.2 | 1.4 | 114.7 |
| 17:44 (7816) | 1,300,960 | 1,026,479 | 854.4 | 47.2 | 12.7 | 20.8 | 91.5 |
| 17:56 (9713) | 1,301,837 | 1,027,153 | 856.6 | 68.1 | 1.7 | 56.8 | 137.3 |
| 18:10 (12173) | 1,386,205 | 1,099,176 | 911.0 | 87.5 | 1.9 | 28.3 | 131.8 |
| 18:16 (13509) | 1,475,675 | 1,171,631 | 966.4 | 139.8 | 56.5 | 8.6 | 220.2 |
| 18:23 (14126) | 1,475,226 | 1,171,736 | 967.0 | 138.8 | 72.4 | 36.6 | 262.0 |
| 18:41 (15262) | 1,475,235 | 1,171,673 | 968.6 | 76.1 | 50.1 | 34.9 | 175.3 |
| 18:50 (16904) | 1,472,812 | 1,171,191 | 966.0 | 67.3 | 1.3 | 88.4 | 174.4 |
| 18:59 (17824) | 1,474,412 | 1,171,921 | 967.6 | 175.7 | 2.9 | 37.0 | 230.7 |
| 19:12 (20027) | 1,472,783 | 1,171,471 | 965.3 | 166.7 | 57.6 | 4.6 | 244.4 |
| 19:40 (31610) | 2,181,266 | 1,742,139 | 1418.9 | 651.3 | 307.3 | 102.8 | 1092.4 |
| 20:10 (32128) | 2,181,910 | 1,742,626 | 1418.9 | 601.2 | 2.5 | 313.5 | 948.5 |
| 20:34 (33635) | 2,344,661 | 1,885,054 | 1515.7 | 490.7 | 3.0 | 444.1 | 968.9 |
| 21:18 (34362) | 2,343,646 | 1,884,752 | 1514.9 | 1033.9 | 3.6 | 385.8 | 1460.8 |

E1 (rgl_1744:5497-5947): one load of 1,071,840 objects / 832,466 containers, two saves of 1,073,131 /
833,207 and 1,073,861 / 833,528 objects/containers (708 MB uncompressed), Collect::Containers 65.0 s and 34.1 s.
For scale, the Harad player's day-4 campaign in E3/E4 saves in 5.1 to 8.7 s (E4 lines 2388-3260, TAOM
`[SaveLoad]` probe).

Load reads (E2 LOAD STATISTICS, lines 3886, 9782, 12254, 20191 ... 30021, 32296): every load read the
latest save, EXCEPT that the eight loads between 19:23 and 19:38 all read the 19:12 file byte for byte
(header 81,638,791, object data 318,515,982, container data 610,793,860, strings 1,202,554 at each of
lines 20190-30024).

### SAVE-TIME-01 (P0): every in-session reload adds about 88,000 objects and 71,000 containers to the next save, permanently

**Evidence (E2, rgl_22112, LOAD/SAVE STATISTICS).** Object counts are flat within a play stretch and step up only
across a load. Within one game the count even drifts down (17:31 1,305,904 to 17:44 1,300,960; 18:16 1,475,675 to
19:12 1,472,783, an hour of play). Across loads:

| load (line) | file read (objects / containers) | next save (objects / containers) | step |
|---|---|---|---|
| #1 17:21 (3886) | 1,302,023 / 1,026,584 | 17:31: 1,305,904 / 1,028,264 | +3.9k / +1.7k (normal play) |
| #2 18:03 (9782) | 1,301,837 / 1,027,153 (the 17:56 save) | 18:10: 1,386,205 / 1,099,176 | +84,368 / +72,023 |
| #3 18:13 (12254) | 1,386,205 / 1,099,176 (the 18:10 save) | 18:16: 1,475,675 / 1,171,631 | +89,470 / +72,455 |
| #4-#11 19:23-19:38 (20191-30021) | 1,472,783 / 1,171,471 each time (the 19:12 save) | 19:40: 2,181,266 / 1,742,139 | +708,483 / +570,668 = 8 x (88,560 / 71,334) |
| #12 20:32 (32296) | 2,181,910 / 1,742,626 (the 20:10 save) | 20:34: 2,344,661 / 1,885,054 | +162,751 / +142,428 = 2 x 71,214 containers |

E1 (one load in a fresh process) shows no step: 1,071,840 read, 1,073,131 saved.

What this proves, and what it does not:
- The eight reloads read a byte-identical file (same four section sizes at lines 20190-30024), yet the save after
  them carries eight increments. So the duplicate lives in state that SURVIVES `Game.Destroy` and a reload, i.e.
  process-lifetime state that the next save's object graph can reach. A per-campaign object cannot do this.
- The first load in a fresh process adds nothing (E1, and E2 load #1).
- The bloat is permanent: load #12 read the bloated 2,181,910-object file and saved 2,344,661. Every later save of
  that campaign carries it, and each extra reload adds more.
- The increments look like duplicate instances of existing game objects: strings do not grow with them (1,202,554
  strings in the 19:12 save, 1,208,931 in the 19:40 save, against +708k objects), and bytes per object and per
  container in the increment (207 and 508) match the save average (216 and 521).
- Volume added in this one session: 12 increments (loads #2, #3, eight from #4-#11, two at #12). The 21:18 save holds
  2,343,646 objects / 1,884,752 containers against 1,302,023 / 1,026,584 in the file the session started from:
  +1,041,623 objects (44% of the save) and +858,168 containers, 1515 MB against 857 MB uncompressed. Normal play in
  the same session moved the count by a few thousand.
- The increments line up with the reload cycles, and each cycle in E2 contained a lord-recruit persuasion
  (`lord_ask_recruit_*` lines, 7-10 persuasion lines each, 19:23-19:40 and 20:31/20:34). The trigger is therefore
  either "a load" or "something done in a game that was then abandoned by a reload". The arithmetic "loads in the
  process minus loads the file had seen" fits loads #2, #3 and #4-#11 exactly but predicts one increment for #12, not
  two. UNVERIFIED which.

**Mechanism: UNVERIFIED.** What I ruled out by reading code this session:
- No TAOM `SyncData` persists a static field, and the only one in the whole v1.4.8 dump is vanilla's
  `LordsNeedsTutorIssueBehavior._alreadyChosenHeroes` (`TaleWorlds.CampaignSystem.Issues/LordsNeedsTutorIssueBehavior.cs:918,967`),
  which a load replaces whenever the save carries the key (it always does), so it does not explain E2.
- TAOM's persisted stores are flat id/primitive dictionaries rebuilt or replaced on load (Career
  `CareerPersistenceBehavior.cs:41-65,131`, EquipPresets `EquipmentPresetService.cs:52-79`, FormationPresets
  `FormationPresetCampaignBehavior.cs:45-66`, momentum JSON chunks `WarOfTheRingMomentumBehavior.cs:101-128`). None can
  add tens of thousands of containers per load.
- TAOM never injects XML into `MBObjectManager` at runtime (no `XmlInformationList` / `LoadXML` / `LoadOneXmlFromFile`
  use in Main), so the "static XML list grows per load" path is not TAOM's.
- The engine resets per game: `Game.LoadSaveGame` builds a fresh `MBObjectManager` and `Game.Destroy` clears it
  (`Core/TaleWorlds.Core/Game.cs:166-178,246-262`; `MBObjectManager.cs:363-374`). The static
  `ConversationManager._persuasion` (`ConversationManager.cs:87,1100-1118`) holds only the latest persuasion.
- TAOM.Dependencies' SaveShield only adds exception-swallowing finalizers (`Dependencies/Foundation/SaveShield.cs`).
- The vanilla in-game load path deserializes the new file while the old game is still current
  (`Modules/SandBox/Sandbox/SandBoxSaveHelper.cs:276`), but `MBSaveLoad.LoadSaveGameData` loads with
  `loadAsLateInitialize: true` (`Core/TaleWorlds.Core/MBSaveLoad.cs:120`), so the load callbacks run later, after the old
  game's screens are cleaned and a fresh object manager exists (E2 lines 20209-20253: `CleanScreens`, the loading screen,
  then "Initializing saved game begin" and `LoadContext::Callbacks`). No cross-game hand-off found there.
- ButterLib (vendored by TAOM) has the right shape in its `MBObjectKeeper` but no caller in TAOM (SAVE-TIME-09).
- Not inspectable here: BetterTime (third-party, loaded last in E1/E2).

A strong candidate class, still to be proven: a stale reference to an engine object from an earlier game in the
process (a troop `CharacterObject`, an `ItemObject`, a `CultureObject`) written into the current game's saved state.
`MBObjectBase` overrides `GetHashCode` (to `Id`) but NOT `Equals` (`MBObjectBase.cs:79-82`), so the save's identity
dictionary treats a stale instance as a NEW object and walks its whole reachable sub-graph, which for XML objects is
the previous game's static world (troops, equipment, items, cultures), a self-contained graph of plausibly this size.

**Fix direction.**
1. Name the carrier first with the probe in SAVE-TIME-07 (type histogram of `_childObjects` / `_childContainers`):
   diff a save made right after a fresh-process load against one made after one more in-session reload. The types
   whose counts jump by about 88k / 71k are the carrier; their referrer is the leak.
2. Repro matrix, cheapest first: (a) TAOM, load, reload x3, save, compare counts in rgl; (b) the same without
   BetterTime; (c) vanilla Native+SandBox+StoryMode only. (c) tells TaleWorlds-vs-mod.
3. Once named: fix the carrier, and add a load-time dedupe for already-bloated saves only if the duplicates are
   provably unreachable from live gameplay.

**1.5.x:** none. No 1.5.x commit, plan or doc read this session addresses per-reload save growth (checked
ae1a6b553, 16a34027d, 5479109bf and the 1.5.x `save-system.md` / `save-load-diagnostics.md`).

**Verify:** the repro matrix above on the laptop (v1.4.8). Pass = save counts after N in-session reloads equal the
counts after one fresh-process load (within normal play drift of a few thousand).

### SAVE-TIME-02 (P1, engine mechanism): why the Containers phase dominates and scales worse than volume

All paths below are the v1.4.8 dump, `Core/TaleWorlds.SaveSystem/TaleWorlds.SaveSystem.Save/`.

**Phase order** (`SaveContext.Save`, `SaveContext.cs:328-363`, run synchronously on the main thread from
`SaveManager.Save`, `TaleWorlds.SaveSystem/SaveManager.cs:97-103`; only the file write after it is async):
1. `CollectObjects` (`:175-250`): single-threaded BFS from the `Game` root. Each object and each container is added to
   `_childObjects`/`_childContainers` and to a `Dictionary<object,int>` (`_idsOfChildObjects`, `_idsOfChildContainers`,
   default comparer, pre-sized 131,072 at `:114-125`, so a 2.3 M-object save resizes it about five times).
2. `CollectSaveDatas` (`:127-173`): the Objects phase then the Containers phase, each a
   `TWParallel.ForWithoutRenderThread` over the whole list (`EnableSaveStatistics => false`, `:107`).
3. `WriteObjects` / `WriteContainers` (`:365-415`): parallel again; every object and container gets its own exact-size
   `BinaryWriter` byte array, all held in `byte[][]` until the end.

**Per container, in the Containers phase** (`CollectSaveDataForContainer`, `SaveContext.cs:497-509`):
- `ContainerSaveData.CollectChildren` (`ContainerSaveData.cs:71-128`) allocates two `ElementSaveData[elementCount]`
  arrays and one `ElementSaveData` per element (and per key for a Dictionary). Elements are read through the
  non-generic `IList` / `IDictionary` / `Array.GetValue`, so every value-type element is boxed.
- Each element's constructor does a `DefinitionContext.GetTypeDefinition(value.GetType())` lookup and, for class
  elements, `Context.GetObjectId(data)` against `_idsOfChildObjects` (`VariableSaveData.cs:39-90`).
- `CollectStructs` (`ContainerSaveData.cs:384-399`) creates a full `ObjectSaveData` for EVERY custom-struct element,
  used or empty. Each `ObjectSaveData` allocates three dictionaries and a list in its constructor
  (`ObjectSaveData.cs:47-70`) plus one Field/PropertySaveData per member (`:82-126`). The struct-element arrays are
  the bulky vanilla ones: `Equipment`'s `EquipmentElement[]`, `ItemRoster`'s `ItemRosterElement[]`, `TroopRoster`'s
  `TroopRosterElement[]`, so the cost follows array CAPACITY, not used slots.
- `CollectStrings` (`:364-374`) walks the elements a second time through `GetChildElements` (boxing again) only to
  find strings, and every string goes through `AddOrGetStringId`, which takes one global `lock (_locker)`
  (`SaveContext.cs:252-278`) shared by all parallel workers.
- `GetDataSize` (`:170-192`) walks every element again and calls `ShouldSaveStruct` for every child struct.

**One quadratic path.** `ShouldSaveStruct` (`:194-212`) calls `IsKey` (`:214-230`), a linear scan of the container,
whenever a Dictionary key or value is an `ISavedStruct` whose `IsDefault()` is true. `ShouldSaveStruct` runs from six
call sites per child struct (the collect-time `GetDataSize`, then `GetDataSize`, `GetFolderCount`, `SaveDataFolder`,
`GetEntryCount` and `SaveTo` in `SaveSingleContainer`, `SaveContext.cs:540-553`), so such a dictionary costs about
6 x n^2/2 comparisons. Only six v1.4.8 structs implement `ISavedStruct`: `CampaignVec2`, `TroopRosterElement`,
`FlattenedTroopRosterElement`, `EquipmentElement`, `ItemRosterElement`, `UniqueTroopDescriptor`. No TAOM container has
that shape (TAOM's registered containers are listed in SAVE-TIME-05). UNVERIFIED whether a vanilla or third-party
dictionary of that shape is large in this player's save.

**Hash behaviour of the dedup dictionary.** `MBObjectBase.GetHashCode()` returns `Id.GetHashCode()`
(`Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs:79-82`) and `MBGUID.GetHashCode()` is the raw
id (`MBGUID.cs:100-103`); `Equals` is not overridden. Every `MBObjectBase` that shares an id (an unregistered object
has id 0; the stale duplicates of SAVE-TIME-01, if that is what they are, share their originals' ids) lands in one
bucket, and each lookup walks it. `TextObject` overrides both: the hash is a per-instance `_internalId` that loaded
objects receive from a `[LoadInitializationCallback]` (`Core/TaleWorlds.Localization/TaleWorlds.Localization/TextObject.cs:23-25,164-168,264-267`),
and `Equals` is VALUE equality (`:251-286`), so equal-content TextObjects collapse into one saved object. Neither is a
measured cost here; both are ways "more objects" can cost more than linear.

**Why Containers, not Objects, is the long phase.** It runs after the Objects phase has created one `ObjectSaveData`
per object, all still referenced from `_objectSaveDataList`, and it adds per-ELEMENT allocations on top. So it runs
against the largest live managed heap of the save with the highest allocation rate. Its best case in the logs is
already about 10x the Objects phase (E1: 34.1 s against 3.4 s; E2 17:44: 47.2 s against 4.0 s).

**TAOM's part.** TAOM does not patch this code. It feeds it: more heroes (several `Equipment` arrays each), more troop
types (bigger `TroopRosterElement[]`), a far larger item catalogue (bigger market `ItemRosterElement[]`), and the
SAVE-TIME-01 duplicates.

**Fix direction.** No safe in-mod rewrite is indicated: the 1.5.x `save-system.md` records a third-party result that a
byte-identical, faster offline rewrite of these paths was slower in game. The levers are volume (SAVE-TIME-01,
SAVE-TIME-04) and measurement (SAVE-TIME-07).

**1.5.x:** documentation only. origin/bannerlord-1.5.x `docs/reference/engine/save-system.md` adds "Performance: what a
save costs" (absent on origin/bannerlord-1.4.5). Its numbers are v1.5.3/1.5.4 third-party measurements, so they are
leads for v1.4.8; the code paths above are what I verified in the v1.4.8 dump.

**Verify:** the SAVE-TIME-07 histogram gives element counts per container type; GC counts and private bytes before and
after one save give the allocation volume on v1.4.8.

### SAVE-TIME-03 (P1): within a session, memory pressure drives the variance; volume sets the floor

**Evidence that it is not data volume alone** (E2 table above):
- At an unchanged 1.47 M objects / 1.17 M containers (saves 18:16 to 19:12), Collect::Containers ranges 67.3 to 175.7 s,
  Saving Objects 1.3 to 72.4 s, Saving Containers 4.6 to 88.4 s.
- Saving Objects and Saving Containers are mechanical writes of already-collected data, with floors at this volume of
  1.3 s and 1.4 s (18:50 and 17:36). When one is slow the other is usually fast: 17:36 41.2/1.4, 17:56 1.7/56.8,
  18:50 1.3/88.4, 19:12 57.6/4.6, 19:40 307.3/102.8, 20:10 2.5/313.5. That is one large stall landing in one phase or
  the other, the signature of a blocking collection or a paging storm, not of work.
- The single-threaded `CollectObjects` is close to linear in object count: 4.3 us/object at 1.30 M, 4.6 at 1.47 M,
  5.0 at 2.18-2.34 M. The parallel Containers phase is not: 41-57 us/container at best at 0.83-1.17 M containers, then
  260-549 us/container at 1.74-1.89 M. 1.6x the containers cost 5-10x per container.
- Load phases stay near linear in the same session (Load Container Datas 8.0 us/container at 832 k, 9.0 at 1.17 M, 9.4
  at 1.74 M), because a load does not hold a second representation of the graph the way a save does.

**Mechanism.** Until `SaveContext.Save` returns it keeps the whole graph (already live), one `ObjectSaveData` per
object, one `ContainerSaveData` plus per-element save data per container, and then every serialized byte array. The
1.5.x doc's third-party measurement (v1.5.3, 145-152 k objects) is about 1.72 GB allocated to write 106 MB, with 66-72%
of the save spent in GC. Scaled linearly to 2.34 M objects that is on the order of 25 GB allocated per save: an
ESTIMATE, UNVERIFIED for v1.4.8. A process already near 5 GB at the main menu (E1 line 3827, E2 line 3830) on a
machine with 21-28% free memory at boot (E2 line 3829, E1 line 3826) cannot hold its peak without paging, and paging
during a blocking collection is a minutes-long stall. After the write, `Game.OnSaveCompleted` forces one more blocking
`GC.Collect()` (`Core/TaleWorlds.Core/Game.cs:216-225`; `Common.MemoryCleanupGC`, `TaleWorlds.Library/Common.cs:242-246`).
The async write itself took about 52 s (19:58:55 to 19:59:47) and 2 min 16 s (21:42:53 to 21:45:09) for the 1.4-1.5 GB
saves.

What the logs cannot settle: E2 has no memory line near any save and does not print total RAM, so "paging" is
inferred from the timing pattern. UNVERIFIED.

**Fix direction.** Cut volume (SAVE-TIME-01 first). Add memory tokens to the existing `[SaveLoad] SaveBegin` /
`SaveCompleted` stamps (SAVE-TIME-07) so the next report separates the two. Lower the resident set before a save where
TAOM owns it (the memory lens covers that).

**1.5.x:** none in code. The 1.5.x `save-system.md` performance section documents the GC share and the forced
collection (a lead; the forced collection is re-checked above for v1.4.8).

**Verify:** an E2-style session with the SAVE-TIME-07 stamps: privMB/wsMB/heapMB/gc counts at SaveBegin and
SaveCompleted, plus Windows hard faults per second during a slow save.

### SAVE-TIME-04 (P2): the baseline is large because TAOM's world is 2-3x vanilla's, and some vanilla state never prunes

**Scale of the static world** (XML definitions counted this session; runtime counts are not available from the logs):

| Definition | TAOM | vanilla 1.4.8 |
|---|---|---|
| Heroes (`NPCCharacter is_hero="true"`) | 1,201 (`Main/_Module/ModuleData`) | 406 (SandBox) |
| All `NPCCharacter` | 3,774 | 1,531 (SandBox 804 + SandBoxCore 727) |
| Settlements | 988 (live `TAOM_Map/ModuleData/settlements.xml`) | 494 (SandBox) |
| Items + crafted items | 3,241 + 347 (live `LOTRLOME_Armory`) plus 17 in TAOM | 990 + 284 (SandBoxCore) |
| Kingdom elements / Faction elements | 32 / 145 | 8 / 98 |

So every per-hero, per-party, per-settlement and per-item-roster container that vanilla saves exists two to three
times over before any play. E1's save after the first load of a fresh process already held 1.07 M objects and 0.83 M
containers. For contrast only (different engine and mod), the 1.5.x doc's third-party bench saved 145-152 k objects.

**Vanilla state that does not prune.**
- Log history prunes by each entry's `KeepInHistoryTime`
  (`Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.LogEntries/LogEntryHistory.cs:61-79`), but
  `CharacterKilledLogEntry` returns `CampaignTime.Never` (same folder, `CharacterKilledLogEntry.cs:36`), and others keep
  1,680 days or 4 years. Every lord death stays in the save, and TAOM has three times vanilla's lords at war.
- Dead or disabled heroes stay in `CampaignObjectManager._deadOrDisabledHeroes`, a saved field
  (`TaleWorlds.CampaignSystem/CampaignObjectManager.cs:210-211`). Vanilla removes only some kinds: dead companions,
  notables and rebels (`CompanionsCampaignBehavior.cs:158,191`, `NotablesCampaignBehavior.cs:65`,
  `RebellionsCampaignBehavior.cs:142`). Dead lords, and every container they own, remain. Their share of this save is
  UNVERIFIED; the SAVE-TIME-07 histogram would give `Hero` and per-container counts directly.
- `CampaignBehaviorDataStore.LoadBehaviorData` (`TaleWorlds.CampaignSystem/CampaignBehaviorDataStore.cs:86-106`)
  never removes the record of a behaviour that no longer exists, so a removed TAOM behaviour's last data rides along in
  every later save. Small (one record each), but permanent.

**Play growth is small next to SAVE-TIME-01.** Within each E2 stretch the counts stay within a few thousand of
each other over up to an hour of play, while each reload added about 85 k objects. Between E1 (28 Oct, 1.07 M objects)
and E2's first file (1 Nov, 1.30 M) the logs cannot separate play growth from reloads in sessions we do not have.

**Fix direction.** None needed in TAOM data for save time alone; measure first (SAVE-TIME-07), then decide whether any
TAOM-amplified population (dead heroes, kill logs, market rosters) is worth trimming.

**1.5.x:** none found. **Verify:** SAVE-TIME-07 histogram on a long-running save.

### SAVE-TIME-05 (P3): inventory of what TAOM itself persists; all of it is small and bounded

TAOM writes 21 non-empty `SyncData` stores (working tree c5b84fb41; `SpecialResourcesBehavior.cs` read from
origin/bannerlord-1.4.5) and registers 8 `SaveableTypeDefiner`s. Every behaviour also costs one `BehaviorSaveData`
object plus its `_records` dictionary in the save, even when `SyncData` is empty
(`CampaignBehaviorDataStore.cs:10-13,65-79`). The whole TAOM-owned surface is on the order of a hundred containers in a
save of more than a million.

| Store (owner file:line) | Persisted shape | Appended by / pruned by | Size after 100-500 days |
|---|---|---|---|
| Hero race map (`HeroRace/RacePersistenceService.cs:18,67-81,179-180`) | `Dictionary<string,int>` + legend string | rebuilt from ALIVE heroes on capture | alive heroes, a few thousand ints |
| Culture conversion (`CultureConversion/Hooks/CultureConversionBehavior.cs:55-66`) | `Dictionary<string,string>` | per settlement; `CultureConversionService.cs:157,183,228` remove | at most 988 entries |
| Fief siege participation (`FiefGranting/Hooks/FiefGrantingCampaignBehavior.cs:51-64`) | `Dictionary<string,string>` | per settlement; `FiefSiegeParticipationService.cs:39,74` remove | at most settlements besieged |
| Career (`CareerSystem/CareerPersistenceBehavior.cs:41-65`) | 4 x `Dictionary<string,string>` | per hero with a career; replaced on load (`:131`) | 1 entry in E3 |
| Career quest declines (`CareerSystem/Quests/CareerQuestCampaignBehavior.cs:25,45,148-153`) | `Dictionary<string,string>` (CSV per hero) | deduped per quest id | bounded by quest definitions |
| Field commission (`FieldCommission/FieldCommissionSaveData.cs:21-27`) | `Dictionary<string,int>` x2 + `List<string>` | per troop type / promoted hero; load prune of dead heroes | bounded by troop types |
| Special resources (`SpecialResources/SpecialResourcesBehavior.cs:102-108`, origin) | `Dictionary<string,float>` | per resource | 1 entry in E3 |
| Momentum (`WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs:101-128`) | count + N JSON chunk strings | events capped at `MaxEventsPerType = 100` (`Domain/MomentumSideData.cs:15`) | a few chunks |
| Supply orders, refuges, field camps (`SupplyLines/Hooks/SupplyLinesCampaignBehavior.cs:62-79`, `Refuge/Hooks/RefugeCampaignBehavior.cs:74-95`, `FieldCamp/Hooks/FieldCampCampaignBehavior.cs:60-79`) | `Dictionary<string, SupplyOrder/RefugeData/CampState>` | removed on completion (`SupplyOrderService.cs:277`), refuge cap (`RefugeService.cs:121`) | active items only |
| Messengers, enlistment, enlistment content (`Messengers/MessengerCampaignBehavior.cs:72-87`, `Enlistment/Hooks/EnlistmentBehavior.cs:87-98`, `EnlistmentContentBehavior.cs:87-98`) | `Dictionary<string,string>` / one fixed-field string | removed on delivery / single record | constant |
| Equip presets (`EquipPresets/Hooks/EquipmentPresetCampaignBehavior.cs:33-44`) | `Dictionary<string, List<HoNEquipmentPreset>>`, each preset 2 x `List<HoNPresetItemReference>` | player-created; replaced on load | tens |
| Formation presets, banner exclusions, possession ids, siege events, inventory search, War of the Ring phase | small lists / ints | player actions / active only | tens |

Registered TAOM containers (`ConstructContainerDefinition`): `List<HoNFormationPreset>`
(`FormationPresetSaveableTypeDefiner.cs:36`), `List<HoNPresetItemReference>`, `List<HoNEquipmentPreset>`,
`Dictionary<string, List<HoNEquipmentPreset>>` (`PresetSaveableTypeDefiner.cs:31-33`), `Dictionary<string, CampState>`
(`FieldCampSaveDefiner.cs:28`), `Dictionary<string, RefugeData>` (`RefugeSaveDefiner.cs:29`), `Dictionary<string,
SupplyOrder>` (`SupplyLinesSaveDefiner.cs:42`). None is keyed or valued by an `ISavedStruct`, so none can reach the
`IsKey` quadratic path in SAVE-TIME-02. TAOM saveable classes that live inside the engine graph (LotrIssue and
CareerQuest issues/quests, `TaomSettlementClaimantDecision`, `RefugePartyComponent`, `SupplyCaravanComponent`) are
spawned and despawned by the engine's own managers.

**Ranking of unbounded stores:** none found. The largest by element count are the race map and the culture
conversion map, both bounded by world size.

**Fix direction.** None for save time. **1.5.x:** none needed. **Verify:** the SAVE-TIME-07 per-behaviour line would
confirm every store stays under a few hundred elements on a 500-day save.

### SAVE-TIME-06 (P3, robustness, port candidate): the momentum chunk loop trusts the saved count

`WarOfTheRingMomentumBehavior.SyncData` reads the chunk count from the save and calls `new List<string>(count)`
(`WarOfTheRingMomentumBehavior.cs:109-117`, working tree). A negative count from a corrupt save throws
`ArgumentOutOfRangeException` inside `LoadBehaviorData`; an absurd one allocates or loops until it fails. Either way
the load fails or SaveLoadDiagnostics reports a `BehaviorSyncFault`. Player impact needs a damaged save, so it is low.
The chunking itself is byte-safe on 1.4.x: 10,000 characters is at most 30,000 UTF-8 bytes against the engine's
32,763-byte string limit (`MomentumSyncChunker.cs:24-29`; limit re-checked in v1.4.8 `SaveContext.SaveStringTo`,
`SaveContext.cs:308-321`, which writes `(short)(4 + UTF-8 bytes)`).

**Fix direction.** Port the guard: a count below 0 or above 4,096 loads fresh state with a warning.
**1.5.x:** 16a34027d (`refactor(core): one guarded SyncData chunk loop for two saves`). Port only the
`Main/Core/Persistence/SyncDataChunker.cs` helper, its tests and the momentum call site; the War Chronicle half is a
1.5.x-only feature (OUT). **Verify:** the 1.5.x tests `SyncDataChunkerSyncTests` and
`WarOfTheRingMomentumBehaviorSyncDataTests`, ported.

### SAVE-TIME-07 (P1, diagnostics): the 1.4.x probe times the save but cannot say what grew

**What exists on 1.4.x.** SaveLoadDiagnostics (Patch61) stamps `SaveBegin` from a `SaveManager.Save` prefix
(`SaveLoadDiagnostics/Hooks/SaveManager_Save_Patch.cs:17-22`) and `SaveCompleted` from `SaveOutput.PrintStatus`, so
total save time is visible (E4 lines 2388-3260). It records no phase split, no object/container counts, no memory, no
per-behaviour size, and no count of loads in the process. The rgl log has phases and counts, but players rarely send
it, and it has no memory or reload context either. `ProcessMemoryTokens.Format()`
(`BattleLoadDiagnostics/ProcessMemoryTokens.cs:15-28`) already exists and is used by the `GameLoaded` stamps.

**Cheapest probe a release could ship** (all diagnostics-only, every hook try/catch-swallowed like Patch61):
1. Append `ProcessMemoryTokens.Format()` and `loadsThisProcess=N` to `SaveBegin` and `SaveCompleted`. N is a static
   counter bumped in the existing `LoadRequested` hook. This alone would have shown E2's pattern (reload count against
   object growth) and separated memory from volume. Cost: one syscall per save.
2. A postfix on `SaveContext.Save(object, MetaData, out string)` that logs
   `objects=_childObjects.Count containers=_childContainers.Count strings=_strings.Count collectMs=<prefix-to-postfix>`
   through `AccessTools.FieldRefAccess` (O(1) reads; the fields are at `SaveContext.cs:75-85`). Bind-guard it with the
   existing `HarmonyPatchBindingTests` pattern.
3. Per-behaviour `SyncData` size and time from the existing `CampaignBehaviorDataStore` hook category
   (`Patch61_SaveLoadDiagnostics_BehaviorData`): prefix/postfix stopwatch on `SaveBehaviorData`, then walk the new
   `BehaviorSaveData._records` once: entry count, total elements of any `ICollection` value, longest string in UTF-8
   bytes (flag anything over 30,000 against the 32,763 limit). One summary line per save plus a line for any behaviour
   over a threshold.
4. Opt-in census (console command or MCM toggle, one save only): in the same postfix as (2), group `_childObjects` by
   `GetType()` and `_childContainers` by type with summed element counts; log the top 25 of each. One O(n) pass over
   2-4 M entries on the frozen save frame, so never default-on. This is the tool that names SAVE-TIME-01's carrier.
   Do NOT try to flip `SaveContext.EnableSaveStatistics` instead: it is an expression-bodied constant (`=> false`,
   `SaveContext.cs:107`), a likely JIT-inline candidate, and turning it on also makes every phase single-threaded.

**1.5.x:** partial and indirect. cb4eeaa3d (`feat(logging): check lines, counters and a session summary`, #1071)
writes a `[Session] summary` from the same `SaveManager.Save` prefix, but it reports TAOM check/counter totals, not
save size or memory. 1.5.x plan 040 / `LoadTimeStamps` times loads, not saves. Nothing on 1.5.x implements 1-4.

**Verify:** unit-test the formatter and the census grouping (pure functions); in game, one save on the laptop prints
the new tokens, and one reload-then-save prints `loadsThisProcess=2`.

### SAVE-TIME-08 (P3, docs port): a load re-runs the save's daily tick on v1.4.8 too

The 1.5.x claim in ae1a6b553 holds for v1.4.8. `Campaign.CreateCampaignEvents` sets the first daily wait after a load
to the ELAPSED fraction of the current day, not the remainder (`TaleWorlds.CampaignSystem/Campaign.cs:1002-1010`), and
the hourly and quarter-hourly waits the same way (`:1012-1023`; the quarter-hourly wait is the quarter's elapsed
fraction expressed in hours, so it can skip up to four ticks). A save made early in a day is followed, after a load, by
a second daily tick for the same day number. Not a save-time cost, but any TAOM day-latched logic that a reload loop
like E2's exercises repeatedly sees it. The SyncData-limit half of that commit is covered in SAVE-TIME-06, and the
`PartyBase.MainParty` half is outside this lens.

**Fix direction.** Port the engine-doc text (IN scope: engine research that holds for v1.4.8), with v1.4.8 line
numbers. **1.5.x:** ae1a6b553 (docs) and e8504a62b (lessons). 5479109bf (war ledger marks each load) is War Chronicle,
a 1.5.x-only feature: OUT. **Verify:** read-only; already re-checked against the v1.4.8 dump above.

### SAVE-TIME-09 (P3): TAOM ships ButterLib with its Save System subsystem on, which replaces a save hot path

TAOM vendors ButterLib 2.11 in its own TAOM.Dependencies module (tracked in the repo under
`Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib*.dll`; E3's assembly list shows
`Bannerlord.ButterLib v2.11.0.0` and `Implementation.1.4.5`). Decompiled from the installed
`Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/Bannerlord.ButterLib.Implementation.1.4.5.dll` (ilspycmd, read-only):

- `SaveSystemSubSystem.Enable()` runs unless ButterLib's options explicitly disable it
  (`SettingsProvider.PopulateSubSystemSettings(this) ?? true`), so it is ON by default, and installs three patches.
- `TypeExtensionsPatch` is a skip-original prefix on `TaleWorlds.SaveSystem.TypeExtensions.IsContainer(Type, out ContainerType)`.
  For every call it does `GetGenericTypeDefinition()` on generic types plus `typeof(IDictionary).IsAssignableFrom` and
  `typeof(IList).IsAssignableFrom`, where vanilla compares a generic definition against five known types
  (`TaleWorlds.SaveSystem/TypeExtensions.cs:15-53`). It also reports `MBList<>`/`MBReadOnlyList<>` as plain `List` and
  makes ANY `IList`/`IDictionary` implementer a container.
- The save calls `IsContainer` once for every reference it dequeues in `CollectObjects`, before the duplicate check
  (`SaveContext.cs:183-191`), twice per unique object through `GetClassDefinition`
  (`Definition/DefinitionContext.cs:314-318`, called from `SaveContext.cs:235` and `ObjectSaveData.cs:57`), and once per
  container (`SaveContext.cs:500`). At 2.3 M objects that is tens of millions of calls, most of them on the
  single-threaded `CollectObjects` path (10.7-11.8 s in the late E2 saves). The added cost per call is UNVERIFIED; at an
  assumed 100-200 ns it would be several seconds per save. A secondary cost, not the driver.
- `BehaviourNamePatch` sets every non-official behaviour's `StringId` to its type's full name (stable), and
  `DefinitionContextPatch` only suppresses duplicate type definitions. Neither grows a save.
- The Object System subsystem's `MBObjectKeeper` (a process-lifetime `Dictionary<MBGUID, MBObjectBase>` written into
  every save as `keepReferences` and never cleared on load) has the exact shape of a SAVE-TIME-01 carrier, but nothing in
  TAOM calls ButterLib's keeper or extension store, and ButterLib's own flag/variable extensions do not call `Keep`. It
  would matter only if another mod (BetterTime?) does. UNVERIFIED.
- `GeopoliticsBehavior.SyncData` is empty: the distance matrices are rebuilt at every load and never saved, so they are
  a load-time and memory cost, not a save cost.

**Fix direction.** Measure before touching: a micro-benchmark of `IsContainer` over the real type mix with and without
the prefix, or the SAVE-TIME-07 collect timer with the subsystem on and off on a throwaway copy. Do NOT switch the
subsystem off on players' installs blind: its own description warns that saves written with it on may not load with
it off. The load-time sibling, ButterLib's Distance Matrix, already has a measured 1.5.x fix.

**1.5.x:** none for the Save System subsystem. Related port for load time and map stalls (not save time):
1325c36ea (`perf(campaign): switch off ButterLib's Distance Matrix`, #740), which applies to 1.4.x because the same
ButterLib 2.11 `GeopoliticsBehavior` ships there (decompiled above).

**Verify:** the benchmark above; and grep a player's `taom_debug` for the SAVE-TIME-07 `collectMs` with the subsystem
toggled in ButterLib's options on a disposable save.

## What an affected player can do today, without a patch

1. **Do not reload inside a session.** Each in-session load added about 85 k objects to the next save (SAVE-TIME-01),
   while the first load after starting the game added none (E1; E2 load #1). To retry something, quit to desktop,
   restart, then load. This stops NEW growth; it does not shrink a save that is already bloated.
2. **Prefer an older, smaller save if the current one is unbearable.** Bloat is carried forward by every later save
   (load #12 read 2,181,910 objects and saved 2,344,661). Among recent saves in Documents\Mount and Blade II
   Bannerlord\Game Saves (or the OneDrive Documents copy, `docs/SAVE-REPAIR-GUIDE.md:16-22`), the smallest file of the
   same campaign is the least inflated. Costs progress; only worth it for extreme cases.
3. **Save less often, deliberately.** Raise Options > Auto Save Interval, or set it to its lowest value, which the game
   shows as "Disabled" (`BannerlordConfig.cs:108,351-357`; `NumericOptionDataVM.cs:151-157`), and save manually at quiet
   moments. E2's two autosaves cost 230.7 s and 1,460.8 s.
4. **Give the save memory.** Close browsers and other heavy programs before saving, restart the game every hour or two
   (E2's slowest saves came late in a 4.5-hour session), and do not save straight after a large battle. This targets the
   paging-driven variance in SAVE-TIME-03; UNVERIFIED how much it helps on this player's machine.
5. **Report with both logs.** The rgl log carries the counts and phases; `taom_debug` carries the reload and memory
   context once SAVE-TIME-07 ships.

## Summary

The single most important conclusion: this player's saves are slow mainly because the save itself is huge and it keeps
growing in a way normal play does not explain. E2's object count is flat within each stretch of play and steps up by
about 88,000 objects and 71,000 containers across each in-session load, permanently; the eight reloads of one
byte-identical file at 19:23-19:38 produced exactly eight such steps. One session of save-scumming a lord recruitment
added 1.04 M objects (44% of the 21:18 save, 857 MB to 1515 MB uncompressed). The duplicate lives in process-lifetime
state that the next save can reach (SAVE-TIME-01, P0). I could not name the carrier: no TAOM `SyncData` store, static,
XML injection or cached engine object fits, the engine resets per game, and ButterLib's object keeper has the right shape
but no caller in TAOM. The SAVE-TIME-07 census (type histogram of the save's object lists, diffed across one reload) is
the cheap, decisive next step, followed by a TAOM / no-BetterTime / vanilla repro.

Second, the variance (47 s to 176 s at constant volume, then 490-1034 s) is memory pressure, not data alone: the two
"Saving" phases are mechanical writes with floors of about 1.3 s yet swing to 300-440 s, and when one is slow the other
is fast, while the single-threaded collect stays nearly linear (SAVE-TIME-03). The engine keeps a full second
representation of the graph alive during a save, and the Containers phase allocates per element, including a full
`ObjectSaveData` for every `EquipmentElement`/`ItemRosterElement`/`TroopRosterElement` array slot, against the largest
heap of the save (SAVE-TIME-02). Volume (SAVE-TIME-01, plus a world 2-3x vanilla's, SAVE-TIME-04) sets how close each
save runs to the paging cliff.

TAOM's own persisted data is small and bounded (SAVE-TIME-05). The 1.4.x save probe times saves but cannot say what
grew (SAVE-TIME-07). Port candidates from 1.5.x for this lens are modest: 16a34027d's chunk-count guard (SAVE-TIME-06),
the engine-doc text from ae1a6b553 and the 1.5.x `save-system.md` performance section, both re-checked for v1.4.8
(SAVE-TIME-08, SAVE-TIME-02), and, for load time rather than save time, 1325c36ea's ButterLib Distance Matrix
switch-off (SAVE-TIME-09). No 1.5.x commit addresses per-reload growth or save size.

Recommended next steps for the orchestrator (I could not run them): `/investigate` on SAVE-TIME-01 with the census
probe built first (a `/new-feature`-sized diagnostics change inside SaveLoadDiagnostics), then the repro matrix on the
laptop; `/research` on `SaveContext` if the census points at an engine type.

