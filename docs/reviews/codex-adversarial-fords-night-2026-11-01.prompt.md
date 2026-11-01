Adversarial review: Fords of War night batch (TAOM, Bannerlord v1.5.6)

This is a READ-ONLY review. Do not edit, create or delete any file in this repository or the game folder. Return the full report as your FINAL MESSAGE; the dispatcher writes it to docs/reviews/raw/codex-adversarial-fords-night-2026-11-01.md, so do NOT write that path yourself.

Scope: git diff d4fb51a50 c2c3a6f28 (two commits on bannerlord-1.5.x: db8f7c10e "towers fall, unattended test runs" and c2c3a6f28 "destructible damage stages move forward only"). Fords of War is a campaign-free lane-push mission mode: lanes with chains of towers, waves auto-march, an AI commander, gold and heroes. The batch makes AI lanes attack towers (lead group and tower reach), fixes an engine exception from tower regeneration (damage-stage hook), adds a layout fitter and retry planner, adds self-check evidence, and adds an env-armed unattended autorun with a Python launcher.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- AGENTS.md and .ai/review-reference.md (project rules, severity and evidence rules).
- docs/features/fords-of-war.md (sections Lane rules, Layout fitting, Unattended runs (autorun), the self-check report and Known limitations).
- docs/reviews/rca-fords-of-war-2026-10-31.md, the night addendum (rows U1-U2, R1-R11): what Claude's own review already found and fixed. Do not re-report those as new unless the fix is wrong or incomplete.
- Config: Main/_Module/ModuleData/fords_of_war/match.json, maps.json, factions.json.
- Engine source: decompile with ilspycmd or the ilspy MCP from the installed DLLs under E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/ (TaleWorlds.MountAndBlade.dll, TaleWorlds.Engine.dll); cached decompiles at C:/Users/mikew/.taom-src/v1.5.6/<FullTypeName>.cs.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence):
1. Lead group (Main/Features/FordsOfWar/Domain/LanePath.cs LeadGroup, LaneMarchRules.LeadOf and TowerInReach, FordsMatch.Lanes.cs SituationOf): the front is the k-th most advanced unit, k = min(n, max(3, (n+9)/10)), the lead group lies within the band on both sides of the front; a lead level with or past the next standing enemy tower attacks it. Suspect: an edge where the lead is null and the centre is used (a NaN centre projects to arc 0, which for East reads as past every tower); an East/West arc direction slip; a lane path whose fitted vertices moved; k for n below 3.
2. Damage-stage hook (Main/Adapters/FordsTowerAdapter.cs SpawnTower, Main/Adapters/FordsTowerStage.cs): DestructableComponent.OnCalculateDestructionStateIndex is set so the shown stage never moves backward. Suspect: a tower or keep that never gets the hook; state shared between towers; a destroyed tower healed; DestructableComponent.Reset; overlapping hits; the third delegate argument (DestructionStates.Length is the string length). Verify against DestructableComponent.CalculateNextDestructionLevel, SetDestructionLevel and ReplaceEntityWithBrokenEntity in the installed DLL.
3. Autorun wiring (Main/Features/FordsOfWar/Autorun/*, Client/Features/FordsOfWar/FordsAutorunHost.cs, Client/ClientSubModule.cs OnApplicationTick, Client/TaomClientComposition.cs, Main/Features/FordsOfWar/FordsOfWarModule.cs): a null host in the module, the client host replaces it, the driver is built only when TAOM_FORDS_AUTORUN arms a plan. Suspect: an unarmed player session that still builds config or logs; a fault path that throws from OnApplicationTick every frame; EndGame or QuitGame called at an unsafe time; a run that never writes the done file.
4. Launcher (tools/run_fords_autorun.py): preflight, safely_exited reset, the dialog click (only the engine's "Mod change detected" notice), the kill path (only its own game and the Watchdog that watches it), judge rules (fail on a FAIL row, a [CrashReport] block inside the run window, or a crash at quit). Suspect: a path that kills or edits something it did not start; a pass that hides a crash; the log window for the CrashReport scan; Windows ctypes types.
5. Self-check evidence (Main/Features/FordsOfWar/Diagnostics/*, FordsFieldProbe.cs, FordsMissionDirector.cs, Hooks/FordsTelemetryLogic*.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs): the per-stream ladder counts blows only at the stream's current AttackTower target, set by the 1 Hz probe; bypass is measured from unit positions; OffNavmesh is counted once per command; check 03 'exceptions' counts exceptions swallowed by CrashReport's finalizers since the mission started (they can arrive on a non-main thread). Suspect: blows attributed to a stale target between probe looks; a lock or thread gap in the exception counter; a verdict part that cannot fail or cannot pass.
6. Layout (Main/Features/FordsOfWar/FordsLayoutFitter.cs, FordsLayoutPlanner.cs, FordsBaseFinder.cs, Main/Adapters/FordsSceneAdapter.cs, FordsMissionDirector.cs PlanLayout/Generate/BuildTowers): ring search, tower slide, rejection when a lane loses every tower of a side (at the fit and after BuildTowers), the planner ladder and its play-best-with-warning rule. Suspect: a layout played with an open keep; a slid tower that leaves its lane order wrong; a long first-tick block from path queries.

FILES (all under the repository root): Main/Features/FordsOfWar/** (Domain, Diagnostics, Autorun, Hooks, Config and the top-level files), Main/Adapters/Fords*.cs and IFords*.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs, Client/Features/FordsOfWar/**, Client/ClientSubModule.cs, Client/TaomClientComposition.cs, tools/run_fords_autorun.py, tools/tests/test_run_fords_autorun.py, TAOM.Tests/Features/FordsOfWar/**, Main/_Module/ModuleData/fords_of_war/*.json.

REQUIRED SECTIONS:
1. VANILLA CODE: paste the engine methods you relied on (DestructableComponent stage methods, Mission hit callbacks to MissionBehavior.OnMeleeHit/OnMissileHit, Agent alarm and scripted combat flags, MBSubModuleBase.OnApplicationTick, MBGameManager.EndGame, Utilities.QuitGame) as code blocks with file and line.
2. DEEP ANALYSIS: walk each Known Suspect with a concrete scenario (positions, hit sequence, tick order) and give CONFIRMED or DISPUTED.
3. CONFIG CROSS-REFERENCE: match.json and maps.json keys against FordsMatchConfigReader and FordsMapsReader (ranges, defaults, the leadBand key, the scene list).
4. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, impact, and a reproduction or proving code; mark UNVERIFIED what you cannot prove. Then OBSERVATIONS (non-defects worth knowing).

QUALITY GATES: every claim cites file:line in this repository or in a decompile; do not report vanilla-matching behaviour as a TAOM bug; read each whole method before you judge it; do not skip the hard sections (engine hook, threading, launcher kill path).

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
