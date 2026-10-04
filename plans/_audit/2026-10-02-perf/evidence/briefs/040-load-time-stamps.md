Plan 040: stamp the load-time phases nobody times today, so game start, campaign load and custom-battle
start can be split between the engine's XML merge, TAOM's patch application and TAOM's own setup.

FACTS (audit 2026-10-02 at dffdf879, E3 and critic; re-read each):
- The engine merges XML at every game start (MBObjectManager.LoadXML per id): it compiles each XSLT
  afresh per apply (v1.5.3 TaleWorlds.ObjectSystem.cs:1274-1286), merges files one at a time
  (:1256-1272), round-trips the whole accumulated document through XDocument per step (:1288-1300),
  re-indexes children per step (:1114-1160), validates every file against a freshly built XmlSchemaSet and
  reads each file twice (:1083, :1350-1360, :1633-1647). TAOM registers 110 XmlNodes (47 NPCCharacters
  files, 28 EquipmentRosters, 19 GameText; lords.xslt 1,515,721 bytes and heroes.xslt 177,579 bytes, 397
  templates each); the Armory's 21 Items XmlNodes are folders giving 131 per-file merge steps. No TAOM
  timing of LoadXML exists; the [SaveLoad] LoadRequested-to-LoadDataOk window contains it unseen.
- TAOM's patch application: PatchCategoryApplier (Main/Features/... find it; plan 009's guarded
  TryPatchCategory, SubModule.cs:907) applies about 95 categories across OnSubModuleLoad,
  OnGameInitializationFinished and mission time with no timing; PatchShield pass 2 logs its own time in
  diag.log; Native2Managed logs its attach time.
- OnGameStart adds 57 behaviours and 52 models (grep counts); game init runs MonsterSize and ArmourGate
  passes (SubModule.cs:1551-1556).
WHAT: (1) a per-id LoadXML stamp: a prefix and finalizer on MBObjectManager.LoadXML (verify the exact
overloads and which one each load path calls with taom-src) logging `[LoadXml] id=<id> files=<n> ms=<x>
xslt=<n>` when the toggle is on, plus one total line per game start; (2) a per-category timing in
PatchCategoryApplier (`[PatchApply] phase=<OnSubModuleLoad|GameInit|Mission> category=<name> ms=<x>`, and a
phase total), always cheap enough to leave on? measure: a Stopwatch per category is microseconds, so log
only the phase totals by default and the per-category lines when the toggle is on; (3) phase stamps around
TAOM's OnGameStart and OnGameInitializationFinished bodies. All under the Battle Load Diagnostics page, a
new default-off toggle EnableLoadTimeStamps (the phase totals of (2) may stay always-on if they cost
under a millisecond in total; the plan measures and decides).
TESTS: the line formats (literal pins), the toggle, the applier's timing seam with a fake clock.
OUT OF SCOPE: changing what loads or how (consolidating XML is a later plan that this measurement
justifies or not).
STOP conditions to include: wrapping LoadXML changes its exception behaviour or order.
ADDENDUM (orchestrator, 2026-10-02, after the rgl-log measurement in
plans/_audit/2026-10-02-perf/evidence/load/load-time-rgl.md):
- The XML merge is now measured from the engine's own log (about 28 s of a 50 s new-campaign load, about
  15 s of a custom battle load) and plan 042 replaces it. Keep stamp (1): it confirms 042's effect in game,
  per type, on players' machines.
- Add (4): time each TAOM campaign behaviour's OnNewGameCreated, OnGameLoaded and OnSessionLaunched handler
  (and the vanilla-event fan-out around them), one line per handler over a threshold plus a phase total.
  Reason: the 2026-10-02 12:45 new campaign has two silent stretches with no line in either log, 12:45:42.1
  to 12:45:45.5 (inside hero creation) and 12:45:46.2 to 12:45:49.3 (after TAOM's handlers at :46 log, before
  the CultureMarketplace initial-seed sweep logs at :49); nothing attributes those 6.6 s today.
