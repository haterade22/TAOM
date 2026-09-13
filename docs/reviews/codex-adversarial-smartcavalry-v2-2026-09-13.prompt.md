# Codex Adversarial Review -- SmartCavalryAI state machine v2 (#586), 2026-09-13

You are an independent adversarial reviewer. A 5-agent Claude deep review already ran on this changeset and every confirmed finding was fixed; your job is to find what those agents and their fixes missed. Verify every claim you make against the INSTALLED Bannerlord v1.4.8 DLLs with ilspycmd, never against memory and never against the E:\Decompiled_Bannerlord dump alone.

## Where the code is

Repo root (rules, AGENTS.md, docs): E:\repos\TAOM

The changeset under review is the working tree at:
C:\Users\mikew\AppData\Local\Temp\claude\e--repos-TAOM\c102e237-2208-4bf3-a1f0-eb20700b365f\scratchpad\wt-cav
That worktree is HEAD (d5e43caf) plus exactly this change and nothing else. READ THE SOURCE FILES THERE. Run dotnet build and dotnet test ONLY there, with: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (the shared tree E:\repos\TAOM has another session's uncommitted red test and must not be built). Current result there: 8817 passed, 0 failed, 2 skipped.

Installed engine DLLs: E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll (also TaleWorlds.Library.dll, TaleWorlds.Engine.dll, TaleWorlds.Core.dll in the same folder). Decompile a type with: ilspycmd -t TaleWorlds.MountAndBlade.Formation "<dll path>"

## Feature in one paragraph

Patch31 is a Harmony prefix+postfix on Formation.SetMovementOrder(MovementOrder input). When the PLAYER orders a player-team cavalry formation to Charge or ChargeToTarget in an open-field battle, the postfix hands control to CavalryChargeService, a per-formation state machine driven every frame by SmartCavalryAIMissionBehavior.OnMissionTick: Forming (SetPositioning a line 5 m ahead, then a MOVE order so riders take arrangement slots) -> Charging (ChargeToTarget; the charge direction is FROZEN at line time; contact when the centroid is within 10 m of the live target measured along that direction) -> PassingThrough (Move to a reform point centroid + dir * (max(along, 0) + ReformDistance), line facing back at the enemy) -> Reforming (hold the line) -> Forming again at the nearest live enemy (a hit-and-run loop). Rerouting moves around friendly infantry first. Every hold state has a dwell budget (MaxLineUpSeconds from MCM for Forming/Reforming, 10 s PassingThrough, 12 s Rerouting); every give-up is HandOff = a vanilla Charge and state Idle, so no exit leaves riders standing. A second F3 mid-cycle is "charge now". Any other player order cancels the cycle; any order on an IsAIControlled formation cancels it; the engine re-issuing the order already in force (BannerBearerLogic.FormationBannerController.RepositionFormation, BannerBearerLogic.cs:157) is detected by comparing the prefix-captured previous order with AreOrdersPracticallySame and does NOT cancel. With the MCM toggle off, HasActiveCycles keeps the behavior ticking until the service hands live cycles back to a vanilla charge.

The bugs this replaces (already fixed, do not re-report): the old code issued Stop (StandGround, riders hold their own positions, Formation.cs:1262) so no line ever formed; IsAligned projected onto RightVec (the line's own axis) so it could never be true; the second F3 was swallowed; PassingThrough stopped at 25 m from a position snapshot; no target-death handling; no cancel on other orders; no IsAIControlled gate.

## TAOM ID CHEATSHEET (for any config cross-reference you do)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". This feature has no culture-keyed config; the cheatsheet is here for completeness.

## READ FIRST

- docs/features/smart-cavalry-ai.md (STALE: describes v1; it is being rewritten after your review; do not report doc staleness)
- docs/reference/harmony-patch-registry.md, section Patch31_SmartCavalryAI
- .claude/rules/harmony-patches.md and .claude/rules/csharp-architecture.md (Engine-Float Decision Gates)
- GitHub issue #586 body (gh issue view 586) for the root-cause table

## KNOWN SUSPECTS -- confirm or dispute each with decompiled evidence

1. Harmony prefix/postfix state pairing. Patch31 declares Prefix(Formation __instance, out MovementOrder __state) and Postfix(Formation __instance, MovementOrder input, MovementOrder __state). Suspect: (a) __state must be passed by value into the postfix unchanged; (b) Formation.SetMovementOrder reassigns input = MovementOrder.MovementOrderStop when input.OrderEnum == Invalid (Formation.cs:688-691); state which value the postfix observes and whether the cancel/identical logic is right for both; (c) MovementOrder is a struct whose static fields' initialisers read Mission.Current.CurrentTime, which is why the category is applied late from SubModule.OnMissionBehaviorInitialize; confirm adding a prefix taking MovementOrder changes nothing about that timing hazard.

2. Identical-order escape. A non-charge order on a player-controlled formation skips CancelCharge when __state.AreOrdersPracticallySame(__state, input, isAIControlled: true) is true. Decompile AreOrdersPracticallySame (MovementOrder.cs:640-675). Suspect: a GENUINE new player order that reads as identical (a Move within 1 m of the machine's own line or reform point; F1 Stop pressed while the engine had already forced Stop; Follow with the same agent) escapes the cancel and the machine later overrides the player. Quantify how reachable each is.

3. Threading. Formation.Tick's substitute loop (Formation.cs:2291-2295) issues MovementOrderCharge when a ChargeToTarget's target empties, outside the feature's recursion guard, and per Patch35's own doc comment Formation.SetMovementOrder can run on the async AI tick thread. That re-enters Patch31 -> HandleChargeOrder -> ChargeNow -> CavalryCommandAdapter.IssueChargeToTarget (an engine write) and BattlefieldQueryAdapter.TryGetNearestEnemyFormation (iterates Mission.Teams and Team.FormationsIncludingEmpty) possibly off the main thread, while OnMissionTick runs the same service on the main thread under one lock. Suspect: a cross-thread engine write or collection enumeration the lock does not cover. Establish which thread Formation.Tick actually runs on in v1.4.8 (Mission.Tick vs the async AI tick) with citations.

4. Empty-formation lifecycle. Formation.RemoveUnit flips a player formation to IsAIControlled=true when it empties mid-battle (Formation.cs:2201-2203) and nothing resets it on refill. OnMissionTick skips formations with CountOfUnits == 0, so a formation that empties MID-CYCLE keeps a non-Idle state until something ticks it. Suspect: HasActiveCycles stays true for the rest of the mission after the toggle is turned off (defeating the early return, a per-frame cost), and the stale entry is only cleared by OnEndMission. Confirm or dispute, and say whether it matters.

5. Reform geometry. With the charge direction frozen at line time, the reform point is centroid + dir * (max(along, 0) + ReformDistance). Suspect: an enemy formation deeper than ReformDistance along the frozen axis (a column, or a line that rotated 90 degrees during the charge) puts the reform point INSIDE the enemy, which is the exact player complaint this change exists to fix. Also examine an enemy that retreats faster than the cavalry closes (along never < 10 m): the formation stays in Charging forever with no timeout by design; argue whether that is right.

6. Alignment metric. FormationAdapter.IsAligned reads Formation.Arrangement.GetWorldPositionOfUnitOrDefault(unit) for every unit in UnitsWithoutLooseDetachedOnes and passes the mean distance to LineAlignment.IsAligned. Suspect: under a Move order the arrangement slot is not where the engine actually drives the rider when Mission.IsFormationUnitPositionAvailable fails for that slot (Formation.cs:1219-1223), so the metric can disagree with the engine's own target and hold the formation until the timeout; and for a ColumnFormation arrangement the semantics differ. Decompile LineFormation/ColumnFormation GetWorldPositionOfUnitOrDefault and say what the metric measures in each.

7. Disabled-mid-cycle hand-off issues an engine write (IssueCharge) while the player has the feature OFF. Suspect: a player who turns the feature off because it misbehaved gets one more order from it. Argue whether Cancel-without-order would be the better exit, given the alternative leaves riders on a Move nobody lifts.

Known and OUT OF SCOPE (do not report): SmartCavalryLineSpacing is passed as the absolute UnitSpacing int so most of its slider range is inert; docs are stale pending rewrite; the per-order GetState lookup for non-charge orders; MixedFormations Patch30's own allocation.

## FILES (all under the worktree root)

Feature:
Main/Features/SmartCavalryAI/CavalryChargeService.cs
Main/Features/SmartCavalryAI/ICavalryChargeService.cs
Main/Features/SmartCavalryAI/LineAlignment.cs
Main/Features/SmartCavalryAI/CavalryPathPlanner.cs
Main/Features/SmartCavalryAI/ICavalryPathPlanner.cs
Main/Features/SmartCavalryAI/SmartCavalryRecursionGuard.cs
Main/Features/SmartCavalryAI/ISmartCavalryAISettingsProvider.cs
Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs
Main/Features/SmartCavalryAI/SmartCavalryAIIoC.cs
Main/Features/SmartCavalryAI/Models/CavalryState.cs
Main/Features/SmartCavalryAI/Models/CavalryFormationState.cs
Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs
Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs

Adapters:
Main/Adapters/IFormationAdapter.cs
Main/Adapters/FormationAdapter.cs
Main/Adapters/ICavalryCommandAdapter.cs
Main/Adapters/CavalryCommandAdapter.cs
Main/Adapters/IBattlefieldQueryAdapter.cs
Main/Adapters/BattlefieldQueryAdapter.cs

Settings: Main/Features/TaomSettings.cs, group "Battle Tactics/Smart Cavalry" (seven properties)

Registration: Main/Features/SmartCavalryAI/SmartCavalryAIIoC.cs; Main/SubModule.cs (search Patch_MissionTime_SetMovementOrder and SmartCavalryAIMissionBehavior)

Cross-feature neighbours: Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs, Main/Features/MixedFormations/FormationLayoutService.cs, Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs, Main/Features/BannerBearers/** (issues SetMovementOrder re-issues via the engine's BannerBearerLogic)

Tests:
TAOM.Tests/Features/SmartCavalryAI/CavalryChargeServiceTests.cs
TAOM.Tests/Features/SmartCavalryAI/LineAlignmentTests.cs
TAOM.Tests/Features/SmartCavalryAI/CavalryPathPlannerTests.cs
TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAIBindingTests.cs
TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs

## REQUIRED SECTIONS

### VANILLA CODE
Decompile from the installed DLL and paste the relevant bodies as code blocks: Formation.SetMovementOrder, Formation.GetOrderPositionOfUnit, Formation.GetOrderPositionOfUnitAux, Formation.Tick (the substitute-order loop), Formation.RemoveUnit (the IsAIControlled flip), Formation.SetControlledByAI, MovementOrder.MovementState, MovementOrder.OnApply, MovementOrder.IsApplicable, MovementOrder.GetSubstituteOrder, MovementOrder.AreOrdersPracticallySame, BannerBearerLogic.FormationBannerController.RepositionFormation, FormationAI.SetCurrentOrder (the IsAIControlled gate), Team.DelegateCommandToAI, Team.AddTeamAI, OrderController.SetOrder (the Charge case), IFormationArrangement.GetWorldPositionOfUnitOrDefault and one concrete implementation (LineFormation).

### STATE MACHINE WALK
Walk the machine with concrete numbers for three scenarios and state every order issued and every state transition with the tick time: (1) 30 riders at the origin, enemy line 100 m east standing still; (2) same but the enemy advances at 2 m/s; (3) the enemy is a 40-rider cavalry formation that moves 40 m north during the charge. For each, say where the reform point lands relative to the live enemy footprint and whether any rider is ordered into or through the enemy after contact.

### ENTRY-POINT MATRIX
Enumerate every path into Patch31 (player order kinds, AI order on an AI-controlled formation, engine re-issue, engine substitute, our own guarded writes, Invalid input, spectator mission with no PlayerTeam, siege) and state what happens to a formation that is mid-cycle in each. Same for OnMissionTick with the feature on and off, and with formations that emptied.

### THREADING
Cite which thread each entry runs on and whether the service lock plus the recursion guard's [ThreadStatic] depth make every state mutation and every engine write safe. If the recursion guard being thread-static means an engine write from thread A can re-enter the postfix on thread A while thread B holds the lock, say so and what follows.

### CONFIG CROSS-REFERENCE
For each of the seven MCM properties in the Smart Cavalry group, name the read site that gates behaviour and check the HintText against what the code does. Check SettingClamp ranges against the MCM attribute ranges.

### FINDINGS OR OBSERVATIONS
Per finding: SEVERITY (P1 crash/state corruption/user-visible misbehaviour in normal play, P2 misbehaviour under unusual but reasonable play, P3 latent risk or test gap), file:line, what, why it matters, a concrete repro scenario, the minimal fix, and why the deep-review agents missed it. If a suspect is DISPUTED, say why with the decompiled line.

## QUALITY GATES
- Every engine claim carries a decompiled citation (type, member, line) from the installed DLL.
- Every finding names a file and line in the worktree.
- Try to refute each of your own findings before reporting it.
- Do not report anything in the OUT OF SCOPE list or doc staleness.
- No em dashes in your report; use -- or a comma.

## Prior review lessons
SUCCESSES: Codex has caught cross-feature interactions per-feature agents missed (banner bearers vs positioning prefixes), attribute-vs-body semantics on virtual engine members, and lifecycle holes across missions. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex has reported unverified cost claims as HIGH.

## Output
Write the full report to: docs/reviews/raw/codex-adversarial-smartcavalry-v2-2026-09-13.md (under E:\repos\TAOM). Start the file with a one-line verdict, then the findings table, then the required sections.
