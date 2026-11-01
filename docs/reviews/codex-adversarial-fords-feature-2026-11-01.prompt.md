Adversarial review: Fords of War mission mode as a whole (TAOM, Bannerlord v1.5.6)

This is a READ-ONLY review. Do not edit, create or delete any file in this repository or the game folder. Return the full report as your FINAL MESSAGE; the dispatcher writes it to docs/reviews/raw/codex-adversarial-fords-feature-2026-11-01.md, so do NOT write that path yourself.

Scope: the Fords of War feature as it stands at c2c3a6f28 on bannerlord-1.5.x. It arrived in four steps: the mode merge 47bf2fdc9, the self-check logging merge a87fe7e3c, the start and panel fix e6dbea3e0, and the night batch db8f7c10e. No Codex review has seen the first three. A separate Codex run reviews the night batch diff (d4fb51a50..c2c3a6f28); here, focus on the mode itself: the domain rules, the mission lifecycle, the Harmony patch, the game manager, the client panel and camera, and the self-check report.

Fords of War is a campaign-free lane-push mission mode started from its own main-menu entry: 3 lanes with two towers per side and a keep, free timed waves plus bought packs, an AI commander using the same lane orders (push, hold, fall back) as the human, gold for kills and capture points, heroes with respawn and buyback. The player is a commander only (free camera, command panel), with no player agent.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- AGENTS.md and .ai/review-reference.md (project rules, severity and evidence rules).
- docs/features/fords-of-war.md and docs/features/fords-of-war/build-spec.md.
- docs/reviews/rca-fords-of-war-2026-10-31.md (everything Claude's reviews already found and fixed; do not re-report a fixed row unless the fix is wrong).
- Config: Main/_Module/ModuleData/fords_of_war/match.json, factions.json (troop and hero ids must exist in TAOM data: check them against Main/_Module/ModuleData and the item and troop files the feature names), maps.json.
- Registries: docs/reference/harmony-patch-registry.md (Patch131_FordsOfWar).
- Engine source: decompile with ilspycmd or the ilspy MCP from the installed DLLs under E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/; cached decompiles at C:/Users/mikew/.taom-src/v1.5.6/<FullTypeName>.cs.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence):
1. Tower damage path (Main/Features/FordsOfWar/Hooks/FordsTowerHitPatch.cs, Domain/TowerDamageRules.cs, TowerChain.cs, FordsMissionDirector.AdjustTowerDamage): the prefix on DestructableComponent.OnHit adjusts or cancels damage for locked towers, own-side blows, ranged/hero/siege factors and weaponless blows. Suspect: a blow that bypasses the prefix (another OnHit caller, TriggerOnHit, a missile path); the rules and the entity disagreeing on hit points; the keep unlock rule when a lane has no towers; a blow on a tower already destroyed.
2. Mission lifecycle (Hooks/FordsOfWarMissionLogic.cs, FordsMissionExit.cs, FordsExitClock.cs, Hooks/FordsMissionLauncher.cs, Client/Features/FordsOfWar/FordsOfWarGameManager.cs): the match starts on the first mission tick (never inside AfterStart), the exit clock leaves 25 s after a winner or 3 s after a failed start, EndGame returns to the main menu. Suspect: a double start or double end; a mission reload by the engine after an exception; state left in a singleton (FordsSession, FordsRunReport, FordsAgentRegistry) into the next match in the same process; save or campaign code reached without a campaign.
3. Domain economy and AI (Domain/FordsMatch*.cs, AiCommander.cs, WaveScheduler.cs, GoldLedger.cs, BountyRules.cs, HeroRoster.cs, CapturePointRules.cs): Suspect: gold created or lost (refunds, kill credit for a unit killed by a tower, a hero bought and never spawned), a wave stream that stalls at the per-lane cap, the AI commander holding a lane forever, hero respawn and buyback arithmetic, capture point flip logic.
4. Agent registry and threading (Main/Adapters/FordsAgentRegistry.cs, FordsBattlefieldAdapter.cs, FordsTowerAdapter.cs, FordsMissionDirector OnAgentRemoved, DeferredCallbackQueue use): Suspect: an agent keyed after removal, an engine callback off the main thread writing shared state without the gate, a gunner or a tower entity kept after the mission ends.
5. Client (Client/Features/FordsOfWar/*: commander camera and probe, command panel VM and view, hotkeys, Gauntlet prefab Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml): Suspect: a camera that leaves the boundary, input that leaks to the engine, a VM that refreshes from a stale session, player-facing text not wrapped as {=KEY}text (TAOM localization rule), a binding to a member that does not exist.
6. Self-check report (Main/Features/FordsOfWar/Diagnostics/*): verdicts PASS, FAIL, PARTIAL, NOT_SEEN per check, a final=1 pass at the main-menu return, one JSON per run. Suspect: a rule that can never FAIL or never PASS, evidence charged to the wrong run, a lock gap between the mission thread and a callback thread, pruning that deletes a report the launcher still needs.
7. TAOM-wide mission behaviors in a Fords mission (no main agent, NoTeamAI, no campaign, late spawns): Dread aura (Main/Features/DreadAura, DreadMissionGate admits Fords), warg riders (Main/Features/Warg), DualWield's behavior, creature behaviors. Suspect: a behavior that assumes Agent.Main or Campaign.Current and throws or misbehaves.

FILES: Main/Features/FordsOfWar/**, Main/Adapters/Fords*.cs and IFords*.cs, Client/Features/FordsOfWar/**, Main/_Module/GUI/PreFabs/FordsOfWar/**, Main/_Module/ModuleData/fords_of_war/*.json, the TAOM_FOW_ language entries under Main/_Module/ModuleData/Languages, TAOM.Tests/Features/FordsOfWar/**.

REQUIRED SECTIONS:
1. VANILLA CODE: paste the engine methods you relied on (DestructableComponent.OnHit, Mission.AfterStart and the behavior loops, MBGameManager.EndGame, Mission.SpawnTroopWithAgentBuildData, MovementOrder.MovementOrderAttackEntity and AttackEntityOrderSecondaryDetachment, CustomGameManager) as code blocks with file and line.
2. DEEP ANALYSIS: walk each Known Suspect with a concrete scenario and give CONFIRMED or DISPUTED.
3. CONFIG CROSS-REFERENCE: factions.json troop and hero ids against TAOM's troop data; match.json keys against FordsMatchConfigReader; maps.json scenes against the installed scenes.
4. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, impact, and a reproduction or proving code; mark UNVERIFIED what you cannot prove. Then OBSERVATIONS.

QUALITY GATES: every claim cites file:line in this repository or in a decompile; do not report vanilla-matching behaviour as a TAOM bug; read each whole method before you judge it; do not skip the hard sections (Harmony patch, threading, lifecycle).

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
