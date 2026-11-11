TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are the adversarial reviewer of a change that is already on trunk: commit `65189fc49`, "feat(war-pledge): v2.0.38 - Let Neutral realms join the War of the Ring" (issue #1113, Slice 1). This working tree is a sibling worktree at `696c1e224`, one commit later; that later commit changes retopology tools only. This is a read-only review: do not edit, stage, build, test or commit anything, and write no file.

Feature in two lines: a Neutral realm can join the War of the Ring for good. A saved pledge per kingdom changes the realm's side (`AlignmentService.GetKingdomSide`) and its tier against the other side (`DiplomacyService.GetRelationshipTier` returns `Hostile`), so the existing Full War rules hold its wars; the AI's daily step pledges one realm for the side that is 25 or more behind on the momentum meter, at most one per 30 days.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

Other kingdom ids this feature names: cardolan, rhudaur, dorwinion, anduinvale, angaladh, southrhovanion (Neutral in `execution/alignment.json`), and goblin, mistymountainorcs, bluecraig (they hold `Natural` rows with umbar, shaghana and abanissa in `diplomacy/diplomacy.json`). Read each id's side from `alignment.json`; do not infer it from the name.

## Engine version

The installed game is Bannerlord v1.5.7 and the tree pins v1.5.7 (`.claude/pinned-game-version.txt`). Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file in its v1.5.7 cache. The dump at `E:\Decompiled_Bannerlord\` is for browsing only and can lag.

## READ FIRST

1. `docs/features/war-pledge.md`: the design, the limits the maintainer already knows ("Known Limits"), and the in-game steps.
2. `docs/reviews/rca-war-pledge-2026-11-10.md`: two earlier Codex passes and four review lenses read this change before it landed. The RCA lists 13 findings that were fixed, the items that were deliberately not applied, and two maintainer decisions that are still owed.
3. `Main/_Module/ModuleData/diplomacy/war_pledge.json`, `diplomacy/diplomacy.json`, `diplomacy/war_of_the_ring.json`, `execution/alignment.json`.

## What NOT to report

- Anything the RCA lists as fixed, not applied or owed, and anything "Known Limits" in the feature doc already states. If you think one of those is worse than the text says, report it only with a concrete scenario that the text does not cover, and say which line of the text you extend.
- The maintainer's decisions: Slice 1 is the AI only; a pledge has no end; a pledge is possible only in Full War; a realm cannot pledge beside a realm it has a static `Hostile` row with; the AI never pledges a realm a player rules; at most one AI join per 30 days; one MCM switch, default on; leans in JSON; the leans themselves.
- English text in the 12 language files (the paid translation is tracked in #763).
- The pinned settings counts (530, 495, 338).
- The numbers 25 and 30 as balance values. A defect in how they are read or compared is in scope.
- Files outside the commit, except as evidence. Other sessions own them.

## Known Suspects: CONFIRM or DISPUTE each, with file:line evidence (the v1.5.7 decompiled file and line for every engine claim)

These are the areas the earlier passes did not attack. Do the hard ones; do not skip a section because it needs engine reading.

1. Momentum for a realm that joins in the middle of the war. Read `MomentumEnrollmentService`, `MomentumAwardService`, `MomentumEventService`, `MomentumVictoryService`, `MomentumStateStore` and `MomentumQueryService`. When the enrollment sweep moves a pledged realm onto a side on the day after the join: what baseline does it get, do its past fief losses or battles count for or against its new side, can its enrollment move `SliderValue` by itself, and can the victory rules now end the war differently (a side that must also destroy or hold the new member, a capital count, a kingdom count)? Can a pledge push the meter so that the other side courts at once when the wait ends, and then back again, as a loop that pledges every leaning realm? Is any momentum state keyed by a side that was stored when the war began?
2. Forced peace and forced war from a daily tick, for the pairs a join touches. `WarPledgeService.TryJoin` calls `IAllianceAdapter.MakePeace` and `DeclareWar` (read `Main/Adapters/AllianceAdapter.cs`, TAOM's `MakePeaceAction_ApplyInternal_Patch`, `DeclareWarAction_ApplyInternal_Patch`, `PeaceActionHook`, and v1.5.7 `MakePeaceAction`, `DeclareWarAction`, `FactionManager`). Enumerate the states of the two realms at that moment and say what the engine does in each: a siege in progress between them; a field battle or a raid map event between their parties; the player in a `PlayerEncounter`, a siege, an army or a settlement of the other realm; prisoners each holds of the other; a pending kingdom decision (peace, war, call to war) that names the pair; a tribute. Which of these does the Full War transition already meet on day 44, and which are new because a join also makes peace?
3. The player inside a join. The AI rule is only "never a realm a player rules". What happens when the human player is a vassal or a mercenary of the realm that pledges, or rules or serves a native realm of the side it joins or of the side it turns on? Trace `FactionSides`, `AlignmentDesertionService`, `RecruitmentAlignmentService`, `EyeOfSauronService`, Kingdom Allegiance (`Main/Features/KingdomAllegiance/`), the player switcher and the enlistment service for a player whose own side differs from the pledge. Is there a state in which the player is stuck (cannot leave, loses troops every day, is at war with their own side, or holds a menu that the forced peace or war makes invalid)? In co-op, the same for a second human player who is not the host.
4. Every reader of the tier, in every phase. `DiplomacyService.GetRelationshipTier` now returns `Hostile` for a pledged pair in all phases, also after `WarEnded`. List each caller of `GetRelationshipTier`, `GetAllianceScoreModifier`, `IsAllianceAllowed`, `IsAllianceDecisionAllowed`, `IsWarAllowed`, `GetWarVeto`, `GetDeclareWarScorePenalty` and `ShouldBlockPeace` (the Harmony hooks in `Main/Features/Diplomacy/Hooks/` and the three models in `Main/Features/Diplomacy/Models/`), and say what changes for a pledged realm against: a realm of the other side, a realm of its new side (the same-side war veto now applies to it), a Neutral realm, and a kingdom that `alignment.json` does not list. Find a pair that ends in a contradictory state: allied or in a trade agreement while `Hostile`; at war with a realm of its own side with no way to end it; vetoed from a war the engine already stored; a call-to-war decision that the vetoes now refuse or now allow for the wrong side. Check what happens to an existing alliance between the pledged realm and a realm of the other side when `blockPeaceBetweenHostileTiers` is off, so that no war is declared.
5. Load path and lifecycle. `WarOfTheRingBehavior` calls `ReconcileDeclaredWars` at session launch. With a pledge in the save, that reconcile now covers pairs it never covered before. Is the store loaded before that call on every load path (a normal load, a save made on the pledge day, a co-op client, a dedicated server)? Can the reconcile declare a war on load for a pledged pair that the join deliberately left alone? Does a kingdom that was destroyed after it pledged, or a ruler change that makes the player the ruler of a pledged realm, leave any state that a later tick reads wrongly? Does the phase leave Full War (`WarEnded`) with pledged pairs in a state the native members are not in?
6. The daily step's own logic. Attack `RunDailyCourting` and `CanJoin` with sequences, not single calls: the first candidate is refused every day; the switch or Momentum Tracking goes off and on across the wait; the meter crosses zero between two joins; two realms of one lean where the first is destroyed; the day counter after a load that reruns a day; `daysBetweenAiJoins` at 1 and at 3650; a clock that is invalid on one day. Is there a sequence that pledges two realms inside one wait, never pledges, or pledges for the side that leads?
7. Tests that cannot fail. For `WarPledgeServiceTests`, `PledgedSideOverrideTests`, `WarPledgeStoreTests`, `WarPledgeBehaviorSessionResetTests` and `WarPledgeWiringTests`: name each decision in the production code (a condition, an order, a comparison operator, a default) whose inversion would leave every test green. The RCA's finding 13 and its "Fix re-review" list the ones already closed.
8. Text against code. `docs/features/war-pledge.md`, the MCM hint (`taom_war_pledge_mcm_enabled_hint` in `taom_module_strings.xml`), the console usage (`WarPledgeCheatReport`) and `tools/ingame_checks/1113.toml`: find a statement that the code or the engine contradicts. The RCA's finding 12 lists the ones already corrected.

## Files

Show a modified file's change with `git show 65189fc49 -- <path>`; read a new file whole. The full list: `git show --name-status --format= 65189fc49`.

New production files:
- `Main/Features/WarPledge/WarPledgeService.cs`, `WarPledgeStore.cs`, `WarPledgeBehavior.cs`, `WarPledgeConfig.cs`, `WarPledgeConfigProvider.cs`, `WarPledgeSettingsProvider.cs`, `WarPledgeIoC.cs`, `WarPledgeModule.cs`
- `Main/Features/WarPledge/Cheats/WarPledgeCheats.cs`, `WarPledgeCheatReport.cs`
- `Main/Adapters/IWarPledgeWorldAdapter.cs`, `Main/Adapters/WarPledgeWorldAdapter.cs`
- `Main/_Module/ModuleData/diplomacy/war_pledge.json`

Modified production files:
- `Main/Features/Execution/AlignmentService.cs` (the side override), `Main/Features/Diplomacy/DiplomacyService.cs` (the tier override)
- `Main/Composition/FeatureModules.cs` (one line), `Main/Features/TaomSettings.cs` (the `WarPledgeEnabled` block)
- `Main/Features/WarChronicle/Ledger/WarLedgerService.cs`, `WarLedgerFormatter.cs` (the `ev=pledge` line)
- `Main/Features/FireAndTribute/FireAndTributeService.OwnSide.cs` (a comment)
- `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 language files (four rows each)

Tests: `TAOM.Tests/Features/WarPledge/` (ten files), and the edits in `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs`, `TAOM.Tests/Features/Execution/SidePairScanTests.cs`, `TAOM.Tests/Features/WarChronicle/Ledger/`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs`.

Tools and docs: `tools/analyze_war_ledger.py`, `tools/tests/test_analyze_war_ledger.py`, `tools/tests/fixtures/war_ledger_v1.txt`, `tools/ingame_checks/1113.toml`, `docs/features/war-pledge.md`.

Context outside the commit (evidence only): `Main/Features/Diplomacy/WarOfTheRingService.cs`, `WarOfTheRingBehavior.cs`, `Main/Features/Diplomacy/Hooks/`, `Main/Features/Diplomacy/Models/`, `Main/Adapters/AllianceAdapter.cs`, `Main/Features/WarOfTheRingMomentum/`, `Main/Features/KingdomAllegiance/`, `Main/Features/AlignmentDesertion/`, `Main/Features/Execution/FactionSides.cs`, `Main/Composition/ModuleRunner.cs`, `Main/IoC.cs`, `Main/SubModule.cs`.

## REQUIRED SECTIONS

### 1. VANILLA CODE
Paste, as code blocks with the file path and line numbers from the v1.5.7 cache, the engine code you rely on. At least: `MakePeaceAction.ApplyInternal` and what it calls for sieges, map events and the player's encounter; `DeclareWarAction.ApplyInternal`; the part of `FactionManager` that stores a stance; the listeners of `CampaignEvents.OnWarDeclared` and of the peace event that act on the player (`PlayerEncounter`, siege and army handling); and the engine's own callers of `DiplomacyModel.IsAtConstantWar`.

### 2. SCENARIOS
For each Known Suspect, at least one concrete scenario with named kingdoms from the data (for example: Cardolan pledges Free on a day when a Gondor army besieges a Cardolan town), the call chain with file:line, and the end state. State CONFIRMED (a defect), DISPUTED (the code is right, with the line that makes it right) or UNVERIFIED (name the evidence you could not read).

### 3. CONFIG CROSS-REFERENCE
For each of the ten Neutral ids: its lean in `war_pledge.json`, every row it has in `diplomacy.json`, its culture id and that culture's side, and whether a join to its lean side and a console join to the other side pass `CanJoin`. Name every pair for which a join leaves a `Natural` or `Permanent` row between two realms that are now opposed, or a `Hostile` row between two realms that are now on one side.

### 4. FINDINGS OR OBSERVATIONS
P1, P2 and P3 findings, most severe first. Each finding: one sentence for the defect, the concrete inputs and state that produce it, the wrong outcome, file:line evidence, and the smallest fix you would accept. A finding with no reachable scenario is an observation, not a finding; list it separately. State explicitly where you disagree with a design choice.

## QUALITY GATES

- Every engine claim cites a v1.5.7 decompiled file and line. A claim without one is marked UNVERIFIED.
- Do not flag code that matches what vanilla or the Full War transition already does as a defect of this change; say that it is inherited and name the line.
- Do not re-report the RCA's findings.
- Do not skip sections 1 to 3.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## Output

Return the full report as your FINAL MESSAGE. The dispatcher redirects your output into `docs/reviews/raw/codex-adversarial-war-pledge-2026-11-10.md`. Do NOT write that path, or any other file, yourself.
