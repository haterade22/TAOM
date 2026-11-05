# Deep-dive: boot and campaign load time (1.4.x line on Bannerlord v1.4.8)

Run 2026-11-04-port148. Lens: boot and campaign load time (XML merge, patch application, crash capture,
load callbacks). Read-only investigation; nothing here edits source.

Refs: working tree = c5b84fb41 (local bannerlord-1.4.5); origin/bannerlord-1.4.5 tip 1b00d1acd; 1.5.x =
origin/bannerlord-1.5.x tip bf3299143. Evidence E1..E4 per the shared brief.

Status: complete (11 findings, ranked in the Summary at the end).

## Findings

### LOAD-TIME-01 (P1): the engine's quadratic module-XML merge is 23 to 37 s of every campaign load

**Player impact.** Every save load and every new campaign pays it, in full, every time. The E2 player
loaded 12 times in one process (between 19:23 and 19:38 eight reloads of one save, save-scumming a lord
persuasion: E2 L21230-L21485 is one round), and those 12 XML windows sum to 292.9 s, about 4.9 minutes
of merge plus object creation in one session.

**Evidence (player logs, Bannerlord v1.4.8 build 119303).**
- E1 load at 19:39:04 (rgl_log_1744 L3881): per-type merge spans from the engine's own `opening` lines
  (L3937-L4720): NPCCharacters 54 files 15.1 s, Items 142 files 6.7 s, GameText 26 files 1.3 s,
  EquipmentRosters 31 files 1.2 s, Settlements 2 files 1.1 s. The window from "Initializing saved game
  end" (L3940, 19:39:38.166) to the map atmosphere (19:40:05) is 27.2 s of a 70.5 s load (LOAD
  STATISTICS 19:39:04.134 to `MapScreen::HandleInitialize` L4913, 19:40:14.664).
- E2, 12 loads (rgl_log_22112 LOAD STATISTICS at L3884, 9780, 12252, 20189, 21485, 23063, 24338, 25626,
  27171, 28446, 30019, 32294): the same window is 32.8 s on the first load of the process, then 22.6 to
  24.0 s on every later load (NPCCharacters 13.0 to 15.1 s, Items 7.7 to 9.4 s each time). Load to
  `MapScreen::HandleInitialize`: 92.8 s (L3884 to L4938), 88.4 s, 83.3 s, 86.1 s (L20189 to L21230),
  84.3 s, and 111.3 s for the last (L32294 to L33335).
- E3 (Harad player, a day-4 save, second-resolution timestamps): `[SaveLoad] ObjectsInitialized
  t=+10172ms` (L30) to `GameLoaded t=+49284ms` (L79). By the v1.4.8 order below, that window holds
  TAOM's `OnGameStart` (its lines at 20:00:46, L31-L75) and every campaign `LoadXML`. The Patch18 postfix
  on `Campaign.InitializeDefaultCampaignObjects` logs "CulturalFeats: all TAOM cultural feats
  registered" at 20:01:02 (L77), splitting it into about 16 s (basic files, Items, EquipmentRosters,
  partyTemplates) and about 21 s (SPCultures, Concepts, NPCCharacters, WorkshopTypes,
  LocationComplexTemplates, Settlements). E4 repeats it: ObjectsInitialized +10281 ms (L29),
  CulturalFeats 20:17:44 (L75), GameLoaded +49336 ms (L77). So about 37 s of a 67 s load (LoadRequested
  L24 20:00:34 to MapScreen L154 20:01:41) on that machine is XML loading, merge plus object creation.

**Engine mechanism (v1.4.8, verified on the installed DLL with
`pwsh tools/taom-src.ps1 path TaleWorlds.ObjectSystem.MBObjectManager`, cache
`C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.ObjectSystem.MBObjectManager.cs`; the dump file
`C:\Decompiled_Bannerlord\_categories_v1.4.8\Core\TaleWorlds.ObjectSystem\TaleWorlds.ObjectSystem\MBObjectManager.cs`
has the same lines).**
- `LoadXML` (:786-797) calls `GetMergedXmlForManaged` (:877-918), which collects every active module's
  file for the type plus a sibling `.xsl`/`.xslt` per entry (`HandleXsltList`, :949-964), then
  `CreateMergedXmlFile` (:966-982).
- `CreateMergedXmlFile` loops over every file. If the entry has an XSLT, `ApplyXslt` (:984-995) builds a
  NEW `XslCompiledTransform`, compiles the stylesheet and transforms the WHOLE document merged so far.
  Then `CreateDocumentFromXmlFile` (:1343-1359) loads the file through `LoadXmlWithValidation`
  (:1061-1129), which builds and compiles a fresh `XmlSchemaSet` (plus the `_replaceWhileMerging`
  overlay and a second compile) and reads the file twice. Then `MergeTwoXmls` (:997-1010) converts the
  WHOLE accumulated document from `XmlDocument` to `XDocument`, merges (`MergeElements`, :820-875, which
  rebuilds a dictionary over every accumulated child), and converts it back. The cost of file N grows
  with everything merged before it: in E1 each TAOM troop or NPC file costs about 0.17 to 0.47 s, 0.281 s
  on average (L4608-L4692), whatever its own size (the 43 files run from 3,046 bytes,
  `characters/spider_creature.xml`, to 2,732,743, `characters/lords.xml`, median 73 KB), because the
  characters merged before it (vanilla, SandBox, TAOM_Map's spnpccharacters and the TAOM files already
  merged) are re-converted twice for each file.
- Call sites in a saved-campaign load (installed v1.4.8 `TaleWorlds.CampaignSystem.Campaign`,
  `OnInitialize` :1366-1453): `InitializeDefaultCampaignObjects` (:1398, body :1466-1494: basic files,
  `Items`, `EquipmentRosters`, `partyTemplates`), `InitializeBasicObjectXmls` (:1410, `SPCultures`,
  `Concepts`), `SandBoxManager.OnCampaignStart` (:1415) into `InitializeSandboxXMLs`
  (`TaleWorlds.CampaignSystem.SandBoxManager` :360-380: `NPCCharacters`, `WorkshopTypes`,
  `LocationComplexTemplates`, `Settlements`). Nothing caches the result between loads, so the same
  bytes are rebuilt on every load of one process (E2: 12 times).

**TAOM data that triggers it (1.4.x working tree c5b84fb41; the campaign set is unchanged at the origin
tip 1b00d1acd, whose only SubModule.xml change is a `CustomGame`-only NPCCharacters node).**
`Main/_Module/SubModule.xml` declares 101 `<XmlNode>` entries: 44 NPCCharacters, 28 EquipmentRosters,
15 GameText. The TAOM files merge LAST (load order `... LOTRLOME_Armory, TAOM_Map, TAOM, BetterTime`, E1
L29, and BetterTime opens only a language file, L549, no object XML), after the large vanilla and TAOM_Map
documents, which is the worst position in a quadratic loop. The 142 Items
files are almost all in the unversioned live `LOTRLOME_Armory` (E1 L4068-L4350).

**Fix direction.** Port 1.5.x plan 042 (`git show origin/bannerlord-1.5.x:plans/042-xml-merge-load-time.md`):
a Harmony prefix on `MBObjectManager.CreateMergedXmlFile` that keeps ONE `XDocument` across the loop,
converts only before an XSLT and once at the end, and caches each compiled `XslCompiledTransform` by
path, last-write time and length. Only `skipValidation == false` calls take it; it stands aside when a
foreign mod patches a bypassed method, re-runs the engine merge on any exception, and disables itself
for the session if the engine succeeds where it failed. A live harness proves byte-identical output for
every type before the patch is trusted. 1.5.x numbers (desktop, offline prototype, v1.5.3):
NPCCharacters 5.4 s to 1.8 to 2.4 s, Items 2.4 s to 0.36 s, EquipmentRosters 0.57 s to 0.10 s, GameText
0.38 s to 0.22 s, identical output.

**Expected saving on 1.4.8: UNVERIFIED.** Scaling the prototype's ratio (27 to 35 percent of engine time)
onto the E1/E2 spans suggests roughly 12 to 17 s per load on the E1/E2 machine and more on the E3
machine, but 1.5.x never measured the fast path in game either (a27c6b338 body: "The in-game load time
has not been measured yet"; its `docs/features/xml-merge-fast-path.md` has harness numbers only).
Verify with the plan's live harness (`TAOM_RUN_BENCHMARKS=1`) on the v1.4.8 install, then one in-game
load comparing the rgl window "Initializing saved game end" to `Main_map/atmosphere.xml`.

**1.5.x commits.** a27c6b338 (perf(xml-merge): feature, adapter, Patch99, harness and binding tests,
2026-10-04), the PatchShield exclusion it depends on from plan 040 (346f3e093), and the integration
fixups 43ca92ee9 (`Dependencies/Foundation/PatchShieldPolicy.cs`). **Ports to v1.4.8, not unchanged.**
The engine code it replaces is identical on v1.4.8 (same methods and calls; line numbers shifted by +4
from plan 042's v1.5.3 excerpts). But the commit registers through `Main/Composition/FeatureModules.cs`
(plan 018's composition root; `Main/Composition` does not exist on the 1.4.x working tree); the plan's
STOP check pins `TaleWorlds.ObjectSystem.dll` at 61,792 bytes and `v1.5.3` (the installed v1.4.8 DLL is
59,864 bytes; `.claude/pinned-game-version.txt` reads `v1.4.8`); and the commit body says "Ship it only
with plan 040", whose `PatchShieldPolicy.ExcludedTargetMethods` entry keeps PatchShield's second pass off
`CreateMergedXmlFile`. On 1.4.x: wire the category in `OnSubModuleLoad` (single-owner
`Main/SubModule.cs`; recommend, do not edit) and add the PatchShield exclusion in the same change.
Effort M to L. Recommend `/research` on `MBObjectManager` and `/verify-bindings` after the port.

### LOAD-TIME-02 (P1, root cause owned by the save-time lens): every load phase scales with the save's object count, and one player's save grew 80 percent in one session

**Player impact.** Load time grows as the campaign ages. In E2 the same player's loads took 83 to 93 s
on the 1.30 M to 1.47 M object saves and 111 s on the 2.18 M object save (LOAD-TIME-01 lists each);
the per-load phases below grow in step with the object count.

**Evidence (E2, rgl_log_22112).**
- Save sizes from `[GameData.Write]` at each save: 1,305,904 objects and 1,028,264 containers at 17:34
  (L5917); 1,386,205 at 18:12 (L12192); 1,475,675 at 18:20 (L13528) and about 1.47 M through 19:17
  (L20046); then 2,181,266 objects and 1,742,139 containers (900.5 MB of container data) at 19:58
  (L31629), and 2,344,661 at 20:51 (L33654). The +709 K object step happened in about 19 minutes of play
  (map at L31060 19:39:58 to the save at L31629), a window holding one daily tick, a lord-recruitment
  persuasion and a castle visit. That window is the lead for the save-time lens.
- LoadContext phases per load (vanilla `PerformanceTestBlock` lines; header / create objects / object
  datas / container datas / callbacks / after callbacks, seconds):
  load #2 at 1,301,837 objects (L9780): 5.06 / 4.16 / 2.38 / 9.02 / 9.08 / 1.34, sum 31.0 s;
  loads #4 to #11 at 1,472,783 objects (L20189 to L28446): about 5.0-6.1 / 4.1-5.1 / 2.5-2.8 /
  10.4-10.8 / 9.4-9.9 / 1.3, sum about 33.6 s;
  load #12 at 2,181,910 objects (L32294): 7.91 / 6.86 / 4.10 / 16.41 / 14.24 / 1.91, sum 51.4 s.
  E1's 1,071,840-object save (L3881): 3.30 / 3.14 / 1.64 / 6.67 / 9.19 / 0.89.
- On top of that the file read itself (`GameData.Read`, the gap from LOAD STATISTICS to "Load Headers
  block is started") and the full `GC.Collect()` the engine runs between phases (cost grows with the
  heap) grow too.

**Engine mechanism (v1.4.8 dump, `TaleWorlds.SaveSystem.Load\LoadContext.cs`).** `Load` (:54-241)
walks every object and container in each phase: header readers (:92-111, `TWParallel.ForWithoutRenderThread`),
`CreateObject` for each object and container (:114-133, serial), `CreateLoadData` per object (:184-194,
parallel), a fresh `ArchiveDeserializer` plus four `ContainerLoadData` calls per container (:217-232,
parallel), with a `GC.Collect()` after most phases (:135, :146, :168, :235). The Callbacks phase
(`LoadCallbackInitializator.InitializeObjects`, `LoadCallbackInitializator.cs:26-62`) calls
`MethodInfo.GetParameters()` and `MethodInfo.Invoke` with a new argument array for every
`[LoadInitializationCallback]` of every loaded object, then `GC.Collect()`. Cost is linear in the object
count by construction. TAOM declares no `[LoadInitializationCallback]` of its own (grep over `Main` and
`Dependencies`: none), so TAOM adds to this phase only through the objects it causes the save to hold.

**Observation, UNVERIFIED cause.** E2's first load (L3884) spent 17.63 s in Callbacks against 9.08 s for
the same-size save on the next load (L9780); E1's first load did not show that penalty (9.19 s for 1.07 M
objects). Free memory at boot was lower in E2 ("Windows Free Memory: %21.473", L3829) than in E1
("%28.301", L3826), so paging is a candidate; JIT warm-up is another. Verify with the plan 040 stamps
(LOAD-TIME-05) plus `[MemStation]` lines on a first and second load of one save.

**Fix direction.** Whatever the save-time lens finds that bloats `Containers` also cuts load time linearly;
rank its fixes by objects removed per save. For load specifically: nothing TAOM can patch in
`LoadContext` safely; the lever is fewer saved objects and containers.

**1.5.x commits.** None on 1.5.x targets load-time scaling directly; see the save-time lens for the
container-growth fixes (`plans/_audit/2026-11-04-port148/deepdive/save-time.md`).

### LOAD-TIME-03 (P2): TAOM's always-on Patch61 finalizers bind `__originalMethod` on about 8.5 M calls per load, most of them on the parallel load workers

**Player impact.** A per-call tax on every object and container of every save load, from the first load
of a process; PatchShield doubles it from the second load. Magnitude on players' machines UNVERIFIED;
the arithmetic below bounds it at roughly 0.5 to 2 s per load plus about 2 GB of short-lived garbage on
an E2-size save.

**Evidence (1.4.x working tree c5b84fb41; identical on origin/bannerlord-1.4.5, these files are not in
the 52 drifted paths).**
- `Main/SubModule.cs:294-334` applies `Patch61_SaveLoadDiagnostics` and its three sub-categories
  unconditionally in `OnSubModuleLoad`.
- `Main/Features/SaveLoadDiagnostics/Hooks/ContainerLoadData_Fill_Patch.cs:36-65`: a finalizer with
  parameters `(Exception? __exception, object __instance, MethodBase __originalMethod)` on
  `ContainerLoadData.InitializeReaders`, `FillCreatedObject`, `Read` and `FillObject`, i.e. four calls per
  container, all inside the parallel container phase (`LoadContext.cs:217-232`).
- `Main/Features/SaveLoadDiagnostics/Hooks/HeaderLoadData_Readers_Patch.cs:24-34`: the same
  `__originalMethod` binding on `ObjectHeaderLoadData.InitialieReaders` (once per object, parallel,
  `LoadContext.cs:92-101`), `ContainerHeaderLoadData.InitialieReaders` (once per container, parallel,
  :102-111) and `ContainerHeaderLoadData.CreateObject` (once per container, serial, :126-131).
- Also per object or container, without the binding: `ObjectHeaderLoadData_CreateObject_Patch.cs:13-43`
  (postfix plus finalizer), `ContainerHeaderLoadData_GetObjectTypeDefinition_Patch.cs:11-26` (postfix),
  `LoadContext_CreateLoadData_Patch.cs:13-25` (finalizer, once per object, parallel),
  `ArchiveDeserializer_LoadFrom_Patch.cs:31-47` (finalizer, once per container, parallel).
- Call count on the E2 #4 save (1,472,783 objects, 1,171,471 containers): the bound finalizers run
  4 x 1.17 M + 1.47 M + 2 x 1.17 M = about 8.5 M times per load; all Patch61 hooks together about 13.8 M.
- PatchShield's second pass (`Dependencies/SubModule.cs:288`, at `OnGameInitializationFinished`) shields
  every patched TaleWorlds method that is not TAOM-declared, not in three GauntletUI namespaces and not a
  SaveShield target (`Dependencies/Foundation/PatchShield.cs:169-218`); SaveShield's list
  (`SaveShield.cs:76-87`) does not include these methods, so from the second load of a process they carry a
  second finalizer that also binds `__originalMethod` (`PatchShield.cs:244`, `:255`).

**Mechanism (lead from 1.5.x, same Harmony 2.4.2 as the players run: E3 L44 "0Harmony v2.4.2.0").**
5ee49d05f measured PatchShield's identical binding outside the game: "about 64 ns and 241 bytes of
garbage per call on one thread, and about 1.1 microseconds per call with 8 threads calling at once",
against 5.4 ns for a finalizer that takes only the exception, because a `__originalMethod` parameter makes
the patched method call `MethodBase.GetMethodFromHandle` on every call. At 64 ns and 241 B, 8.5 M calls is
about 0.5 s of CPU and about 2 GB of garbage per load, before contention on the parallel phases and before
PatchShield's second binding. E2 gives no clean baseline (every load already carries TAOM's own binding);
load #2 against load #1 on the same save (after versus before PatchShield pass 2) moved the parallel phases
by +0.63 s, +0.45 s and +0.37 s, at the edge of the noise seen across the identical loads #4 to #11.

**Fix direction.** (1) Drop `__originalMethod` from the two TAOM finalizers: one finalizer class per
target with a constant site name, or resolve the method from the stack only when `__exception != null`
(the 5ee49d05f technique); behaviour on a throw stays identical. (2) Port 5ee49d05f so PatchShield's own
finalizers stop binding it. (3) Optionally exclude the `TaleWorlds.SaveSystem.Load` per-object internals
from PatchShield: TAOM's finalizers there are void and observe-only, so the shield adds nothing. Verify
with a stopwatch around `LoadContext.Load` phases on one save loaded twice, before and after.

**1.5.x commits.** For TAOM's own Patch61 binding: none on 1.5.x (both hook files are unchanged since
7b7a8dcec, 2026-07-07, before the fork; `__originalMethod` counts identical on both lines). For
PatchShield's binding: 5ee49d05f (plan 034). It does not cherry-pick cleanly: it edits the exclusion code
that 2a3f71b12 (plan 007) moved from `PatchShield.cs` into `PatchShieldPolicy.cs`, so port 2a3f71b12 and
its follow-ups first (LOAD-TIME-06). Engine-independent; no v1.5.x API involved.

### LOAD-TIME-04 (P2, load-failure risk, not a measured cost): one throwing patch category in the game-init batch skips every later category and escapes the engine's load

**Player impact.** None observed in E1 to E4 (the public build applies cleanly: E3 L94-L104 show every
preview category "applied OK"). The risk is a load that never finishes, or a half-patched campaign,
the moment any binding drifts or another mod (BetterTime is in the players' list, E1 L29) reshapes a
target's IL. Fits the brief's "infinite loading screen" history as a cause class worth closing.

**Evidence (1.4.x working tree c5b84fb41, unchanged at the origin tip).**
- `Main/SubModule.cs` has 80 live `_harmony.PatchCategory(` calls (82 lines including two commented
  out): about 14 bare and 7 guarded in `OnSubModuleLoad`, about 50 bare and 7 guarded in
  `OnGameInitializationFinished`, 1 in `OnMissionBehaviorInitialize` (a crude scan: a call counts as
  guarded when a `try` opens within the 8 lines above it).
- `OnGameInitializationFinished` sets `_gameInitPatchesApplied = true` (:1325) BEFORE the batch, so a
  throw from any bare call skips every later category (the Patch65 :1456, Patch82 :1472 and Patch84
  :1480 crash guards among them), `BattleLoadStallWatchdog.Start()` (:1677),
  `ManualPatchApplicator.ApplyAll` (:1715) and the Harmony census (:1727), and is never retried in the
  process.
- Engine side, verified on the installed v1.4.8 DLL (`pwsh tools/taom-src.ps1 path
  TaleWorlds.MountAndBlade.MBGameManager`): `OnGameInitializationFinished` loops
  `Module.CurrentModule.CollectSubModules()` and calls each override with no try/catch, then sets the
  `SkeletonScale` bone indices. A TAOM throw therefore also skips BetterTime's override (it loads after
  TAOM) and the bone-index setup, and propagates into `Campaign.OnInitialize` (:1452). What the player
  then sees (crash dialog, stuck loading screen) is UNVERIFIED on v1.4.8; plan 009 records "a saved game
  never finishes loading" for v1.5.x.

**Fix direction.** Port plan 009: every category through one `TryPatchCategory` helper backed by
`PatchCategoryApplier`, which logs `[PatchApply]` at Error with the cause, costs only that category,
shows one red on-screen line per phase, and keeps the existing side-effect rules (Patch37 hooks
subscribe only on success, Patch77 failure still disables the switcher). Includes a source gate test
against any new direct `PatchCategory` call in `Main`.

**1.5.x commits.** 45bcf80bd (plan 009, "apply every patch category through one guard"). Not a clean
cherry-pick: it rewrites 276 lines of `Main/SubModule.cs`, which differs from its base b2e387db by
+189/-10 lines on the 1.4.x line; `Main/PatchCategoryApplier.cs` and its tests are new files and apply
as is. Redo the SubModule edit by hand (mechanical; single-owner file, orchestrator edits). No v1.5.x
API involved. Plans 040 and 042 build on `PatchCategoryApplier`, so this goes first.

### LOAD-TIME-05 (P2, diagnostics): nothing in a 1.4.x log says where TAOM's share of a load goes, and the log has one-second resolution

**Player impact.** Indirect: every load-time decision on the 1.4.x line is made by inference from rgl
gaps, as this report had to. Example: E3's 8 s silence between "BannerColorConfigProvider: Loaded"
(L105, 20:01:25) and "BattleBalanceConfigProvider: Loaded" (L106, 20:01:33) cannot be attributed,
because both are lazy first-access lines, not phase markers.

**What the 1.4.x line has.** `[SaveLoad]` phase lines with millisecond offsets (`t=+NNNNms`, E3
L24-L93; hooks in `Main/Features/SaveLoadDiagnostics/Hooks/`, stamps at `Main/SubModule.cs:743-760`
and :1315) and the BattleLoad phases. **What it lacks:** per-type `LoadXML` timing, patch-apply timing,
and any split of the `OnGameLoaded` / `OnSessionLaunched` handler fan-out (E1: the 4.34 s between
"Ticking map scene for first initialization" L4884 and the first `SELIM_DEBUG` vote line, 19:40:13.327;
E3: 20:01:33 to 20:01:38, L107-L148). `Main/Core/Logging/FileLogger.cs:86` formats every line as
`yyyy-MM-dd HH:mm:ss` (same on 1.5.x, `:92`), so anything without its own `t=+ms` is unmeasurable below
one second.

**Harmony patch application cost, measured from E1 (answers the brief's question 4).** By the v1.4.8
order (`Campaign.OnInitialize` :1427-1452, then `DoLoadingForGameType` :1653-1657), TAOM's whole late
batch in `OnGameInitializationFinished` (about 57 `PatchCategory` call sites, one of them the loop over
nine preview categories at :1338-1361, plus `ManualPatchApplicator.ApplyAll` and the census) and
PatchShield pass 2 run between the last XML open (L4711, 19:40:04.484) and TAOM's
"Map Scene Load Diagnostics" line from `LoadMapScene` (19:40:05.350), so all of it, plus
`OnGameLoaded`, `LoadBehaviorData` and the MusicTracks object load, took at most 0.87 s on that player's
machine. Likewise every module's `OnSubModuleLoad` (TAOM's early categories and the 247-patch crash sweep
included, LOAD-TIME-06) runs after the last module assembly loads (L645, 19:38:28.104) and before
"Adding trace listener..." (L684, 19:38:33.629): at most 5.5 s for all ten modules. That bound is
code-backed: `Module.Initialize` (`TaleWorlds.MountAndBlade\Module.cs:267-268` in the v1.4.8 dump) calls
`LoadSubModules`, which loads every assembly and only then calls every `OnSubModuleLoad`
(`InitializeSubModuleBases`, :1102 and :201-207), and then prints "Adding trace listener...". The
GauntletUI font loads (L683, 19:38:31.567) sit inside that window; if they come from Native's submodule,
as their position suggests (not verified: the caller of the font loader was not found this session),
TAOM's share is inside the last 2.06 s, shared with SandBoxCore, CustomBattle, SandBox, StoryMode and
BetterTime (E2: 6.5 s overall, L645 17:21:15.799 to L684 17:21:22.302; 2.33 s after the fonts at L682).
The desktop's 30 s figures in plans 006, 007 and 040 do not describe players (the 1.5.x plans index
itself demoted 006 and 007 to P2 for that reason).

**Fix direction.** Port plan 040's always-on lines (`[LoadXml]` per type with merge and object-creation
split plus a summary, `[PatchApply] phase=... scope=total`, `[Lifecycle] dispatch=...` per campaign
dispatch) and its default-off detail toggle (per-group patch timing, `[LoadPhase]` inside TAOM's hooks,
per-handler `[Lifecycle]` lines for handlers of 10 ms or more). It also adds the PatchShield exclusions
that plan 042 needs (LOAD-TIME-01).

**1.5.x commits.** 346f3e093 (plan 040) plus the PatchShieldPolicy fixups in 43ca92ee9. Engine side
verified on the installed v1.4.8 DLLs: the seven targets exist with the plan's shapes
(`MBObjectManager.LoadXML` :786 and `CreateMergedXmlFile` :966; `CampaignEventDispatcher.OnSessionStart`
:1053, `OnAfterSessionStart` :1062, `OnNewGameCreated` :1071, `OnGameEarlyLoaded` :1080, `OnGameLoaded`
:1089; `MbEvent<T>` fields `Next`, `Action`, `Owner` and `_nonSerializedListenerList` at
`TaleWorlds.CampaignSystem.MbEvent`1.cs:9-22`). Repo side it needs plan 009's `PatchCategoryApplier`
(LOAD-TIME-04) and replaces its `Main/Composition/FeatureModules.cs` line (plan 018, absent on 1.4.x)
with direct wiring; `BattleLoadDiagnosticsSettings.cs` and `CoopSettingsRelevance.cs`, which it edits,
exist on 1.4.x. Its MCM toggle moves the pinned settings counts in `SettingsFingerprintTests`.

### LOAD-TIME-06 (P3 for load time): the 247-shim crash-capture sweep and PatchShield's re-shielding of the same shims cost players about 1 to 2 s per process, not 30 s

**Player impact.** Small for boot and first load: inside the 5.5 s (likely 2.06 s) and 0.87 s windows of
LOAD-TIME-05.
E3 and E4 (second resolution) show `[SaveDefiners]` and "[CrashReport] Native2Managed: attached 247
Finalizer(s)" one second apart (E3 L3-L4, 20:00:00 to 20:00:01; E4 L3-L4, 20:16:56 to 20:16:57). The
larger value of these plans is per call at run time (the native-to-managed shims are hot), which the
perf lenses own.

**Evidence (1.4.x).** `Main/SubModule.cs:154-180` installs crash capture in `OnSubModuleLoad`; the
`CrashReportSettings.Instance?... ?? true` reads at :162 and :168 always take the fallback there (plan
006 verified in MCMv5 that `BaseSettingsProvider.Instance` is assigned only in
`OnBeforeInitialModuleScreenSetAsRoot`; same vendored MCMv5 5.12.1 per E3 L55, not re-verified this
session). `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:72-95` patches every static method
of every `*CallbacksGenerated` type (247 in E3/E4). `Patch37_CrashReport.cs` carries all nine
finalizers, four of them on empty base virtuals per plan 006. PatchShield pass 2
(`Dependencies/SubModule.cs:288`) then adds its own `__originalMethod`-binding finalizer to the same 247
shims, because `ManagedCallbacks` is not in its exclusion list (`PatchShield.cs:60-69`). The core files
both plans edit are byte-identical to their base b2e387db (`git diff c5b84fb41 b2e387db` is empty for
`Native2ManagedPatcher.cs`, `Patch37_CrashReport.cs`, `CrashReportSettings.cs`, `CrashBundleThrottle.cs`,
`CrashReportPatchHelper.cs`, `PatchShieldPolicy.cs` and `Dependencies/SubModule.cs`); `PatchShield.cs`
differs by 31 lines (the rethrow stack preserver from c79a58521) and `CrashReportService.cs` by 2. Other
CrashReport files (collectors, rendering, IoC) also drifted on 1.5.x but plan 006 does not edit them.

**Fix direction.** Port plan 006 (six-entry allowlist instead of 247, both toggles live, the four dead
finalizers removed with a test, repeat-suppression logging at powers of ten) and plan 007 (PatchShield
skips namespace `ManagedCallbacks` through `PatchShieldPolicy.IsExcludedTargetNamespace`, and the shield
pass line reports elapsed time and ms per attach, which gives a field measurement of pass 2 for free).

**1.5.x commits.** Plan 006: 8bd44b924, 1d94df9c4, 9cfb91b15, fb52e619d, 6fe83bcad (docs) and follow-ups
65b691b6a, 707275290, 42624b95b, 8e6b0935c, 8c84fa20e, 9e8c99bdb. Plan 007: 2a3f71b12 and follow-ups
578ac7d61, 31a31f16b, 0bf2409e6, 900920dca, b0a93fb4c, 737929445 (docs: pass 2 runs at game start, not
the menu), 88408869f, 0ad253d5c (CHANGELOG). Engine-independent. Expect conflicts in `Main/SubModule.cs`
(006 edits :154-180's equivalent), `Dependencies/Foundation/PatchShield.cs` and the CHANGELOG/doc hot
spots. The `PatchShield.cs` drift is c79a58521 (2026-09-23, subject "Add scripts for generating and
validating animal animation clips", which also carried PatchShield's `RethrowStackPreserver`: a
rethrown exception keeps its real stack, player bundle 2d446100). c79a58521 touches 208 files; take
only `Dependencies/Foundation/PatchShield.cs` (+31), the new `Dependencies/Foundation/RethrowStackPreserver.cs`
(140 lines, absent on the 1.4.x tree) and `RethrowStackPreserverTests.cs`. Port that first, then 007,
then 5ee49d05f (LOAD-TIME-03).

### LOAD-TIME-07 (P1, TAOM data): TAOM's own NPCCharacters files cost 10.7 to 11.9 s of every load; merging them into one deployed file removes most of it with no engine patch

**Player impact.** The largest single TAOM-attributable piece of every campaign load. It is a subset of
LOAD-TIME-01, called out because it can be fixed in TAOM's own build without patching the engine.

**Evidence.** A campaign load merges all 43 TAOM NPCCharacters files (every one of TAOM's 44 nodes,
the XSLT-only `lords` node included, admits the `Campaign` game type). Per-file merge cost (time from a
file's `opening` line to the next file's), summed by module, excluding each type's last file because its
span also carries object creation (hence 42 of 43 below):

| Load | TAOM NPCCharacters | Armory Items | TAOM_Map spnpccharacters (+ `lords.xslt`) | TAOM EquipmentRosters |
|---|---|---|---|---|
| E1 (rgl_log_1744 L3937-L4720) | 42 files, 11.80 s (0.281 s each) | 131 files, 5.35 s (0.041 s each) | 1 file, 2.30 s | 27 files, 1.04 s |
| E2 load #1 (L3943-L4890) | 42 files, 11.92 s | 131 files, 6.16 s | 1 file, 1.93 s | 27 files, 1.17 s |
| E2 load #4 (L20259-L21210) | 42 files, 10.72 s | 131 files, 6.12 s | 1 file, 1.95 s | 27 files, 1.08 s |

A TAOM NPC file costs about seven times an Armory item file because by then the accumulated character
document is much larger (vanilla, SandBox and TAOM_Map's characters; plan 042 measured 7,276,693
characters for the final NPCCharacters document on 1.5.x), and the engine re-converts all of it twice per
file (LOAD-TIME-01).

**Mechanism and the safe-merge conditions (v1.4.8 `MBObjectManager.cs`).** The quadratic term is per
FILE, not per element: one file holding all 43 files' elements pays the conversion once. Two engine
details decide whether a concatenation is behaviour-identical:
- XSLT position. An entry's XSLT transforms the document merged so far before that entry's file
  (`CreateMergedXmlFile` :966-982). Among TAOM's NPCCharacters nodes only the XSLT-only `lords` node
  (`Main/_Module/SubModule.xml:98`, `lords.xslt`, no `lords.xml`) carries one, and it precedes every TAOM
  NPC file (`characters/lords` is :159, the troop files :183 onward), so files after it can be joined.
  TAOM ships no other NPCCharacters XSLT (the module's eight XSLTs are `lords`, `heroes`, `spclans`,
  `spcultures`, `spkingdoms`, `comment_strings`, `module_strings`, `action_strings`).
- Duplicate ids. `MergeElements` (:820-875) builds its id dictionary from the accumulated document
  BEFORE walking the incoming file and never adds to it during the walk, so two same-id elements in one
  incoming file are both appended, while the same pair in two files merges the second into the first.
  A generated file is equivalent only if no id appears in two of the joined files (or the build step
  applies the engine's merge itself).
- Game-type filters. `GetMergedXmlForManaged` (:877-918) drops a node whose `IncludedGameTypes` excludes
  the running game type. TAOM's 44 NPCCharacters nodes carry three filter sets (41 unfiltered, 1
  `Campaign, CampaignStoryMode`, 2 `Campaign, CampaignStoryMode, CustomGame, EditorGame`) and its 28
  EquipmentRosters nodes two (27 unfiltered, 1 campaign-only). Join only runs of adjacent nodes with the
  same filter set, keeping their order relative to the filtered ones.

**Fix direction.** A deploy-time build step that writes one generated NPCCharacters file (and one
EquipmentRosters file, about 1 s more) from the per-culture sources, which stay as they are in the repo;
`SubModule.xml` in the deployed module names the generated file. Gate it with a test that (a) fails on any
id shared by two source files and (b) proves the engine's own merge of the sources and of the generated
file produce the same document (plan 042's live harness already does this comparison; reuse its
`LiveMergeListBuilder`). Expected saving about 10 to 11 s per load on the E1/E2 machine, UNVERIFIED until
measured. With LOAD-TIME-01's fast path in place the per-file cost no longer grows with the document, so
this lever then saves much less; do one or the other first, measured. The Armory's 131 item files
(5.4 to 6.2 s) are the same pattern but live in the unversioned `LOTRLOME_Armory` install, so that part is
the maintainer's out-of-repo work.

**1.5.x commits.** None on 1.5.x. Plan 042 describes this lever and leaves it to the maintainer
("Decisions this plan does not take", item 2, `git show origin/bannerlord-1.5.x:plans/042-xml-merge-load-time.md`).

### LOAD-TIME-08 (P3, measurement): launch to main menu is about 50 s, and TAOM's managed share of it is a few seconds at most

**Player impact.** Boot is paid once per session and is mostly native engine work; TAOM can move only a
small part of it with the levers in LOAD-TIME-06. Recorded so the port programme does not spend effort
on boot for little return.

**Evidence, E1 (rgl_log_1744; machine "PIKABOO", RTX 3070).**

| Phase | From | To | Duration |
|---|---|---|---|
| Engine start to graphics adapter | L2 19:38:11.023 | L68 19:38:11.759 | 0.7 s (shader cache package read 0.21 s) |
| Silent gap after "Selected graphics adapter" | L68 | L69 19:38:14.103 | 2.34 s |
| Silent gap after "reading sound_files xml files" | L78 19:38:14.104 | L79 19:38:20.949 | 6.85 s |
| Asset package registration, all modules | L83 19:38:21.029 | about L199 19:38:22.084 | about 1.1 s |
| Managed assembly loads (TAOM.dll at L640, 19:38:27.974) | L279 19:38:23.217 | L645 19:38:28.104 | 4.9 s |
| Every module's `OnSubModuleLoad` (LOAD-TIME-05) | L645 | L684 19:38:33.629 | 5.5 s (TAOM's share likely within the last 2.06 s) |
| "Loading Module..." to the morph mapping jump ("gpu_morph_mapping: 5864058") | L2897 19:38:35.824 | L3532 19:38:43.213 | 7.4 s |
| Prefabs, 235 files (TAOM_Map 141 files sum 1.73 s; Native and SandBox spans include interleaved work) | L2885 | L3744 | span 21.9 s |
| Engine's own figure | L3821-L3823 | | "Loading Time: 46.548 secs" |
| Intro video to `GauntletInitialScreen::HandleInitialize` | | L3866 19:39:01.142 | 50.1 s from log start |

E2 (same machine, L1-L3869): 49.5 s to the main menu, engine "Loading Time: 51.844 secs" (L3826), every
`OnSubModuleLoad` within 6.5 s (L645 to L684), prefabs span 23.3 s with TAOM_Map's 141 files 1.80 s.
Memory at the end of boot: E1 "Windows Free Memory: %28.301", "Memory Usage: 5.003 Gb" (L3826-L3827); E2
"%21.473", "4982.980 Mb" (L3829-L3830). E3/E4 (another machine, second resolution): TAOM's first line
(`OnSubModuleLoad`) to "MainMenuCustomizer: menu customization applied" (`OnBeforeInitialModuleScreenSetAsRoot`)
is 17 s both times (E3 L1 20:00:00 to L16 20:00:17; E4 L1 20:16:56 to L16 20:17:13); in E3 the intro video
then played 16 s unskipped (L19 to L22) before the main menu (L23, 20:00:34).

**Conclusion.** TAOM-attributable boot time on the E1 machine is the managed `OnSubModuleLoad` share
(at most 5.5 s for all ten modules, likely 2 s for TAOM and the four modules after Native), TAOM_Map's
prefab files (about 1.7 s) and a few tenths of a second of TAOM language files. The rest (about 40 s) is
native engine start, sound banks, skeleton and morph setup and vanilla prefabs. The desktop's 30 s crash
sweep (plan 006) is not what players pay.

**Fix direction.** None needed beyond LOAD-TIME-06. If boot ever matters, measure first with the shield
pass timing that plan 007 adds and a `[LoadPhase]` stamp around TAOM's `OnSubModuleLoad` (plan 040's
detail mode).

**1.5.x commits.** None specific; 1d94df9c4 and 2a3f71b12 (LOAD-TIME-06) are the only boot-time changes.
Worth porting for boot diagnosis: d31026b5b ("flush TAOM.Dependencies startup lines"). TAOM.Dependencies
buffers its startup lines in `EarlyLog` until TAOM has a logger and nothing ever flushed the buffer; the
1.4.x tree has no `Main/Core/Logging/EarlyLogBridge.cs`, and E3 carries zero `[TAOM.Dependencies]` lines,
consistent with the same loss (its Harmony fork version, AssemblyResolve redirects and install failures
are exactly what E3's build-stamp MISMATCH, L2, needs). Small, engine-independent; touches
`Main/SubModule.cs` (4 lines, single-owner).

### LOAD-TIME-09 (P2): the last 9 to 17 s of every load, from game init to the map, is unattributed, and the 1.4.x line lacks the map-load timeline that solved the same blind spot on 1.5.0

**Player impact.** 9.4 s of E1's load and about 17 s of E3's and E4's sit between TAOM's
`GameInitializationFinished` stamp and the map screen, with private memory rising by about 2 GB in that
window (E3: `privMB=6699` at L93 to `privMB=9016` at L153; E4: 6778 at L91 to 8845 at L152). The 1.4.x code
itself records a worse case: `Main/SubModule.cs:736-740` notes "95 s and +2.7 GB between
AllBehaviorDataLoaded and the map screen with nothing in either log" on a 2026-09-12 live run and "#509 saw
the same window stall for 10 minutes". That is a campaign-load hang class on v1.4.8.

**Evidence of what is in the window (v1.4.8 order).** `Campaign.DoLoadingForGameType`
(`TaleWorlds.CampaignSystem.Campaign.cs:1641-1699`, installed DLL via taom-src): `LoadVisualsThirdState`
runs `LoadMapScene` (:1653-1657), whose SandBox `MapScene.Load` prints "Ticking map scene for first
initialization" and then runs a native `_scene.Tick(0.1f)` (`Modules\SandBox\Sandbox\MapScene.cs:244-245`
in the dump); `PostInitializeFourthState` then runs `OnDataLoadFinished`, `CalculateCachedValues`,
`OnGameLoaded` and `OnSessionStart` (the dispatcher fan-out to every campaign behaviour, vanilla and TAOM),
then `CheckInvalidEquipmentsAndReplaceIfNeeded` for every hero (:1660-1680). In E1 (rgl): map scene
creation from "Creating map scene" (L4717, 19:40:05.351) to the first-tick line (L4884, 19:40:08.984) is
3.6 s; the first tick plus the fan-out run 4.34 s to the first `SELIM_DEBUG` vote line (19:40:13.327); the
screen switch to `MapScreen::HandleInitialize` (L4913) and `HandleActivate` (L4914, 19:40:15.357) is about
2 s. In E3 the TAOM lines show the fan-out only coarsely: L107-L131 at 20:01:33
(PartySizeDiag, CultureMarketplace pool of 4,965 items, BannerInjection), L132-L152 at 20:01:38 (LotrIssues,
CareerSystem registry of 67 careers and 2,077 choices parsed from XML, WarOfTheRing, Diplomacy, RaceManager).

**Fix direction.** (1) Plan 040's `[Lifecycle]` lines (LOAD-TIME-05) split the fan-out per dispatch and,
in detail mode, per handler. (2) Port the map-load timeline: a 5 s heartbeat from a `Campaign.RealTick`
postfix with fps, tick ms, a per-type party census, campaign time, loading-window state and the whole game
state stack, plus a lifecycle trace of state pushes and pops, map seams and loading-window raises and
lowers with caller chains. Its doc says it found the v1.5.0 stuck-loading-screen cause "on its first run"
after seven single-point hypotheses failed. It MUST come with plan 012: as first shipped, its loading-window
postfix walked 12 stack frames and flushed a log line on every rendered frame of the main menu, party
screen and character creation (plan 012: an 84 MB log of 262,763 such lines in 35 minutes, 1.16 GB after
three hours on the main menu).

**1.5.x commits.** Map-load timeline: 502f7cde9 (2026-09-14, labelled v2.0.28, on origin/bannerlord-1.5.x
only; `Main/Features/MapLoadDiagnostics` is absent from the 1.4.x tree). Its per-frame fix, plan 012:
8028bf3df, e6c03411d, bce3c27f4, 15e5475a4, a4492fa89, 6f7ddd394 (CHANGELOG). Plan 012 on its own has
nothing to fix on 1.4.x (no patch on `LoadingWindow` there: grep for `DisableGlobalLoadingWindow`,
`EnableGlobalLoadingWindow` and `typeof(LoadingWindow)` under `Main` finds none). Port both or neither.
Engine side UNVERIFIED for v1.4.8: run `/verify-bindings` on its five patch classes
(`CampaignLifecycle_Trace_Patch`, `Campaign_RealTick_MapLoad_Patch`, `GameStateManager_Transitions_Patch`,
`LoadingWindow_Transitions_Patch`, `SceneReady_Trace_Patch`) before trusting it; it was written on the
v1.5.x engine. Touches `Main/IoC.cs` and `Main/SubModule.cs` (single-owner).

### LOAD-TIME-10 (P3): `lords.xslt` costs 1 to 2 s of every load; the 1.5.x dedent barely moves that, the XSLT cache does

**Player impact.** Part of the 1.93 to 2.30 s spent at TAOM_Map's spnpccharacters entry (LOAD-TIME-07),
which includes compiling and running `lords.xslt` over the whole accumulated character document before
TAOM's `characters/lords.xml`.

**Evidence.** `Main/_Module/ModuleData/lords.xslt` on the 1.4.x tree: 21,304 lines, 397 templates
(794 `xsl:template` occurrences), blob 1,493,730 bytes at both c5b84fb41 and origin/bannerlord-1.4.5
(1,515,034 bytes as checked out with CRLF, one extra byte per line, the same size as the copy installed
on this laptop). It carries the same runaway
indentation 5e45d2782 removed on 1.5.x: 650,828 bytes of leading whitespace, up to 4,960 on one line, 320
lines with more than 200 (1.5.x tip: 282,843 bytes, max 35). On a saved load, `lords.xslt`,
`spcultures.xslt` and the three GameText XSLTs run; `heroes.xslt`, `spclans.xslt` and `spkingdoms.xslt`
run only on a new campaign (`SandBoxManager.InitializeSandboxXMLs`, v1.4.8 :360-380, skips `Heroes`,
`Kingdoms` and `Factions` when `isSavedCampaign`). 1.5.x measured `lords.xslt` offline at 136 to 212 ms
to compile and about 1.0 s to transform (plan 042, "What was measured"), and a27c6b338's body: "lords.xslt
still costs about 1 s on each game process's first load and 0.05 to 0.1 s on later loads" once the compiled
transform is cached.

**Fix direction.** The cache in LOAD-TIME-01 is the lever that matters (E2's player reloaded 12 times in
one process). The dedent only shrinks what `XslCompiledTransform.Load` parses (24 percent fewer bytes, a
fraction of the 0.1 to 0.2 s compile); worth doing because it is free and it speeds the lord-skill tool
(5e45d2782 body: 10 s to 2 s). It cannot be cherry-picked (the 1.4.x and 1.5.x lord sets differ); re-run
the same whitespace normalisation on the 1.4.x file and prove it with the commit's own test: byte-identical
output under the .NET transform. The growth's source is not identified on either line (UNVERIFIED); check
the lord-skill tooling before regenerating, or the indentation returns.

**1.5.x commits.** 5e45d2782 (whitespace only, `git diff -w` empty). Related developer-tool fix outside
this lens: 9165a6727 (`tools/complete_lords_xslt.py` never rewrites existing bytes; the old `--apply` rebuilt
the file and dropped 389 lords' equipment).

### LOAD-TIME-11 (P3, engine fact): a load re-times the daily tick from the save's elapsed fraction of the day, so a save made early in a day repeats that day's tick soon after loading

**Player impact.** Small and indirect: some loads are followed shortly by a full daily tick (every daily
handler of every settlement, hero and party), which reads as the load running long. Not separable in the
evidence: the first daily tick after each E2 load fell 5.9 s to 79.0 s of real time after the map, depending
on time speed and pauses.

**Evidence (installed v1.4.8, `TaleWorlds.CampaignSystem.Campaign.cs`).** `CreateCampaignEvents`
(:1002-1025), called at the end of `OnDataLoadFinished` (:697-725), which `DoLoadingForGameType` runs for a
saved and for a new campaign (:1666, :1685): `initialWait` for the daily event is
`(numTicks % TimeTicksPerDay) / TimeTicksPerDay` days, the elapsed part of the current day, not the
remainder; the hourly and quarter-hourly events are computed the same way. So the v1.5.5 reading in 1.5.x's
ae1a6b553 holds unchanged for v1.4.8.

**Fix direction.** Documentation only (no TAOM code depends on it yet); port ae1a6b553's engine-doc
paragraphs with v1.4.8 line numbers. Any future TAOM daily-tick work that assumes "one tick per day after a
load" must account for it.

**1.5.x commits.** ae1a6b553 (docs: daily tick on load, the 32,763-byte SyncData string limit, MainParty).
Its engine claims were re-read here for `CreateCampaignEvents` only; the SyncData limit claim belongs to the
save-time lens.

<!-- findings-end -->

## Summary

**The single most important conclusion.** On the 1.4.x line, a campaign load is dominated by the engine's
module-XML merge (23 to 37 s of every load: LOAD-TIME-01), and the biggest TAOM-owned piece of it is the
43 TAOM NPCCharacters files (10.7 to 11.9 s: LOAD-TIME-07). Boot (about 50 s to the main menu) and Harmony
patch application are not where players lose time: TAOM's patching fits in at most 0.87 s at game init and
at most 5.5 s (likely about 2 s) at boot on the E1 machine. The 30 s desktop figures behind 1.5.x plans
006 and 007 do not describe players. The second driver is save size: every LoadContext phase is linear in
the save's object count, and one player's save grew 80 percent in a session (LOAD-TIME-02, root cause for
the save-time lens).

| Id | Priority | Finding | Port from 1.5.x |
|---|---|---|---|
| LOAD-TIME-01 | P1 | Quadratic engine XML merge, 23 to 37 s per load, rebuilt identically on every load | a27c6b338 (plan 042) with 346f3e093's PatchShield exclusion and 43ca92ee9; adapt (no composition root, re-pin the v1.4.8 DLL) |
| LOAD-TIME-07 | P1 | TAOM's 43 NPCCharacters files cost 10.7 to 11.9 s; one generated file removes most of it without an engine patch | none (lever described in plan 042, not taken) |
| LOAD-TIME-02 | P1 | Load phases linear in save objects; E2 save 1.31 M to 2.34 M objects in one session; loads 83 s to 111 s | none for load; see the save-time lens |
| LOAD-TIME-03 | P2 | TAOM's own Patch61 finalizers bind `__originalMethod` on about 8.5 M per-load calls, mostly on parallel workers; PatchShield adds a second binding from the second load | none for TAOM's hooks; 5ee49d05f (plan 034) for PatchShield |
| LOAD-TIME-04 | P2 | 50 bare `PatchCategory` calls in the once-only game-init batch; one throw skips the rest and escapes an unguarded engine loop | 45bcf80bd (plan 009), SubModule edit redone by hand |
| LOAD-TIME-05 | P2 | No per-type XML, patch or handler timing; one-second log resolution | 346f3e093 (plan 040), needs 009's applier |
| LOAD-TIME-09 | P2 | Game init to map (9 to 17 s, +2 GB) unattributed; a recorded 1.4.8 stall of this window (#509) | 502f7cde9 (map-load timeline) only together with plan 012 (8028bf3df, e6c03411d, bce3c27f4, 15e5475a4, a4492fa89, 6f7ddd394) |
| LOAD-TIME-06 | P3 | 247-shim crash sweep and PatchShield re-shielding: about 1 to 2 s per process for players | plan 006 (8bd44b924, 1d94df9c4, 9cfb91b15, fb52e619d, 6fe83bcad + 6 follow-ups), plan 007 (2a3f71b12 + 8 follow-ups), after c79a58521's PatchShield files |
| LOAD-TIME-08 | P3 | Boot timeline: 50 s to the main menu, TAOM's managed share a few seconds | d31026b5b (TAOM.Dependencies startup lines reach the log) for diagnosis |
| LOAD-TIME-10 | P3 | `lords.xslt` 1 to 2 s per load; the dedent is cosmetic for load, the XSLT cache is the lever | 5e45d2782 by regeneration, not cherry-pick |
| LOAD-TIME-11 | P3 | Daily tick re-timed from the elapsed part of the day on load (holds on v1.4.8) | ae1a6b553 (docs) |

**Suggested port order for this lens** (each step depends only on earlier steps):
1. `c79a58521`'s three PatchShield files (`PatchShield.cs` +31, `RethrowStackPreserver.cs`, its test).
2. Plan 009, `45bcf80bd` (guarded apply; `PatchCategoryApplier` is a prerequisite for 040 and 042).
3. Plan 007 (`2a3f71b12` and follow-ups), then plan 006 (`1d94df9c4` and the rest), then plan 034 (`5ee49d05f`).
4. Plan 040 (`346f3e093`, plus the `PatchShieldPolicy` parts of `43ca92ee9`), so the next step is measured.
5. Plan 042 (`a27c6b338`), proven by its live harness on the v1.4.8 install before the patch is wired; or
   LOAD-TIME-07's generated NPC file first if a no-patch change is preferred.
6. TAOM-only: drop `__originalMethod` from the two Patch61 finalizers (LOAD-TIME-03).
7. Optional: `502f7cde9` with all six plan 012 commits; `d31026b5b`; `5e45d2782` by regeneration;
   `ae1a6b553` docs.

**Answers to the brief's questions.**
1. Boot. E1: 50.1 s from log start to the main menu (L2 to L3866), engine "Loading Time: 46.548 secs"
   (L3823); E2: 49.5 s, "51.844 secs". Every module's `OnSubModuleLoad`, TAOM's 247-patch sweep
   included, fits in 5.5 s (E1) and 6.5 s (E2), TAOM's part likely within the last 2.06 s and 2.33 s;
   E3/E4 stamp the sweep's end one second after `[SaveDefiners]` (one-second resolution, so under 2 s). Boot-time module XML is native: 235
   prefab files over a 21.9 s span, TAOM_Map's 141 of them summing to 1.73 s. Plans 006 and 007 save
   players about 1 to 2 s per process; plan 034's value is per call (LOAD-TIME-03, -06, -08).
2. Campaign load. Callbacks 9.1 to 17.6 s and Load Container Datas 6.7 to 16.4 s are vanilla loops linear
   in the save's object count; TAOM declares no load callbacks (LOAD-TIME-02). Plan 042 targets the 23 to
   37 s merge (LOAD-TIME-01); plan 040 is the instrument (LOAD-TIME-05); plan 012 has nothing to fix on
   1.4.x unless the map-load timeline is ported (LOAD-TIME-09); the daily-tick-on-load fact (ae1a6b553)
   holds on v1.4.8 and is docs only (LOAD-TIME-11).
3. XSLT and XML merge on 1.4.x. TAOM ships 8 XSLTs: `lords.xslt` 1,493,730 bytes (blob; 21,304 lines,
   397 templates, 650,828 bytes of leading whitespace), `heroes` 179,151, `spclans` 105,335, `spcultures`
   98,364, `spkingdoms` 18,423, `comment_strings` 14,804, `module_strings` 7,970, `action_strings` 5,231
   (working-tree sizes). The installed TAOM_Map has one (`settlements.xslt`, 470 bytes) and the installed
   Armory eight (`weapon_descriptions` 76,102 down to `Animations/action_sets` 432). A saved load runs
   `lords`, `spcultures` and the three GameText XSLTs; `heroes`, `spclans` and `spkingdoms` only run on a
   new campaign. `MBObjectManager.LoadXML` per type on E1: NPCCharacters 15.1 s, Items 6.7 s, GameText
   1.3 s, EquipmentRosters 1.2 s, Settlements 1.1 s (LOAD-TIME-01, -07, -10).
4. Harmony patch application. At most 0.87 s for TAOM's whole game-init batch plus PatchShield pass 2 on
   the E1 machine; at most 5.5 s for every module's `OnSubModuleLoad` (LOAD-TIME-05). Plan 009's guarded
   helper is a robustness port (LOAD-TIME-04), not a speed-up.
5. Portability: every 1.5.x commit named above is engine-independent or re-verified against v1.4.8 here
   (the XML merge methods, the five `CampaignEventDispatcher` lifecycle methods and `MbEvent<T>` fields,
   `CreateCampaignEvents`). None cherry-picks cleanly: the composition root (plan 018) and
   `PatchCategoryApplier` (plan 009) are absent on 1.4.x, `Main/SubModule.cs` drifted (+189/-10 against
   the 1.5.x plan base b2e387db), `PatchShield.cs` drifted (c79a58521), and the lord sets differ. The
   map-load timeline's engine bindings are UNVERIFIED for v1.4.8.

**Not covered here (other lenses or unavailable).** Mission and battle loading, the Harad first-battle
freeze (E3 L217-L223), shader precompilation and render waits; save time and the root cause of save growth;
runtime per-call costs of PatchShield and the crash shims; memory (the +2 GB at map entry is noted only);
new-campaign and custom-battle loads (no evidence log; plan 042 measured custom battles at about 14 s of
merge on 1.5.x); in-game or harness measurements on this laptop (no `dotnet` in this run).

**Recommended next actions for the orchestrator.** `/research` on `MBObjectManager` and
`TaleWorlds.SaveSystem.Load.LoadContext` before the plan 042 and Patch61 work; `/verify-bindings` after
any port that adds patches (and on the five map-load timeline patch classes before porting them); run plan
042's live harness (`TAOM_RUN_BENCHMARKS=1`) on the v1.4.8 install as the gate; `/deep-review` on each
ported branch.

