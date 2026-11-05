# K1: completeness critic for the SKIP-FEATURE rows (part A)

Run 2026-11-04-port148, read-only verifier K1. Status: complete.

Refs: 1.4.x = `origin/bannerlord-1.4.5` (tip 1b00d1acd); 1.5.x = `origin/bannerlord-1.5.x`. Engine = the v1.4.8 dump at
`C:\Decompiled_Bannerlord\_categories_v1.4.8` (manifest `version: v1.4.8`, decompiled 2026-09-06) plus taom-src on the
installed DLLs (`C:\Users\mikew\.taom-src\v1.4.8`). Input: the 230 rows of `SP\agg\classified.jsonl` whose verdict is
SKIP-FEATURE (SP = the run scratchpad named in brief.md).

## Bottom line

1. Three SKIP-FEATURE commits carry an in-scope payload that no PORT row brings, proven from the hunks:
   **92839bc52** (a test helper that a PORT row's new test calls), **9f58c58de** (the only 1.5.x pattern and rule text
   for the RNG lock WP107 plans), **42148af4c** (a generic prefab hint gate whose engine claims hold on v1.4.8).
2. Ten more skipped commits do carry an in-scope hunk, but a PORT row's port_notes already absorb it. They need no
   reclassification and must not be ported twice (table below).
3. Five PORT rows add code that depends on SKIP-FEATURE (or later-WP) code without saying so in their notes:
   0ab3dc35a, 0761c01b7, 1ebcf86a8, 852678f86, 8caac7da5. One WP136 doc hunk (50e8dc7ae) would write 1.5.x-only facts
   into a correct 1.4.x engine doc. These are traps, listed with the fix.
4. Every other skip I tested holds. Four engine leads riding inside skipped commits were checked on v1.4.8 and have no
   1.4.x trigger (one of them, the fdb683039 load loop, is refuted for single player).

## Method

1. Read subject, why and file list of all 230 rows; split each row's files into new-on-1.5.x feature folders,
   bookkeeping, and everything else (shared features, `Main/Core`, `Main/Adapters`, test infrastructure, tools, harness,
   engine docs).
2. Read the hunks (`git show`) of every row with files in the last bucket, starting with the leads in
   `SP\agg\batch_notes.md` (batches 12, 14, 19, 21-26, 32-34).
3. Two dependency sweeps (scripts `SP\scripts\k1_helpers2.py` and `SP\scripts\k1_created3.py`): (a) the 31 members that
   a SKIP-FEATURE commit added to a helper file that exists on 1.4.x, and (b) the 382 helper-like files a SKIP-FEATURE
   commit created outside new feature folders; each cross-checked against the lines that PORT, PORT-PARTIAL and UNSURE
   rows add. Every hit was then read by hand; name collisions, doc-only mentions and hunks the row's notes already
   leave out were discarded.
4. Engine claims re-checked on the v1.4.8 dump or with taom-src.

Limits: no `dotnet build` or `dotnet test` (session rule), so "does not compile" below is inferred from the member
being absent from the file on `origin/bannerlord-1.4.5`. The sweeps cannot see dependencies through reflection
strings, XML or docs. Lessons riding in skipped commits were not enumerated one by one (see "End-state syncs").

## Misclassified commits

### K1-01 92839bc52: a test helper a PORT row needs (SKIP-FEATURE to PORT-PARTIAL, P2)

- 92839bc52 ("siege troop picker and troll siege role") adds `IlCallScanner.ExtractOpCodes(byte[])` to
  `TAOM.Tests/Migration/IlCallScanner.cs` (hunk `@@ -142,6 +142,26 @@`, 20 lines; line 179 on the 1.5.x tip). It walks
  IL opcodes and throws on an unknown opcode instead of stopping.
- 0ab3dc35a (PORT-PARTIAL P2, group issue-1072-ingame-check-lines, WP133) adds the test
  `Patch27Postfix_GuardsTheCloseAndTheOpen_AndItsCatchFailsThenRethrows`, which calls `IlCallScanner.ExtractOpCodes`
  (`0ab3dc35a:TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs:111`) and `IlCallScanner.Fingerprint` (:159,
  :163). Its prereqs are cb4eeaa3d, c8d69e426 and 0761c01b7; neither 92839bc52 nor a27c6b338 is named.
- `origin/bannerlord-1.4.5:TAOM.Tests/Migration/IlCallScanner.cs` has `OpCodeMap` (:91) and `OperandSize` (:145) but
  neither `ExtractOpCodes` nor `Fingerprint`, so the ported test cannot compile without both helpers.
- `Fingerprint` comes from a27c6b338 (PORT-PARTIAL P1, plan-042-xml-merge-fast-path, WP117; +94 lines in that file).
  The file at 92839bc52^ is byte-identical to the file at a27c6b338 (`git diff a27c6b338 92839bc52^ -- <file>` is
  empty) and a27c6b338 is an ancestor of 92839bc52, so the two hunks lift in that order with no conflict.
- Port: only the 20-line `ExtractOpCodes` hunk. Leave SiegeForces, CreatureSiegeRole, the model seams, the
  `CombatMechanicsModelInvariantsTests` row and the `ReflectionSiteBindingTests` rows.

### K1-02 9f58c58de: pattern and rule text for WP107's RNG lock (SKIP-FEATURE to PORT-PARTIAL, P2)

The runtime change sits in `CareerTroopHooks`, a 1.5.x-only file, but the hazard it fixes exists on 1.4.x and 1.5.x
never fixed its own copy.

- v1.4.8 declares the defend callback multi-threaded: `[MBCallback(null, true)] internal void GetDefendCollisionResults`
  (`...\_categories_v1.4.8\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6455-6456`),
  which calls `MissionCombatMechanicsHelper.GetDefendCollisionResults` (:6459).
- v1.4.8 `MBRandom.RandomFloat` is `Random.NextFloat()` over `Game.Current.RandomGenerator`
  (`...\Core\TaleWorlds.Core\TaleWorlds.Core\MBRandom.cs:22`, `:32`); that is an `MBFastRandom` (`Game.cs:57`) whose four
  `uint` fields (`MBFastRandom.cs:23-32`) advance through `XorShift(ref _x, ref _y, ref _z, ref _w)` (`:83`). Neither
  file takes a lock.
- 1.4.x: `TaomCombatMechanicsModel.DecideCrushedThrough`
  (`origin/bannerlord-1.4.5:Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs:81-85`) builds
  `BuildCrushThroughContext`, which draws `randomRoll: MBRandom.RandomFloat` at :177. The 1.5.x tip still draws it in
  `CombatMechanicsHooks.cs:142` and `:185`.
- 9f58c58de is the only 1.5.x source for the fix shape: a `private static readonly Random` drawn under a private lock
  (`origin/bannerlord-1.5.x:Main/Features/CareerSystem/Hooks/CareerTroopHooks.cs:48-51`), a source pin
  (`TheCrushHoldRoll_UsesItsOwnLockedGenerator_NeverTheEngineOne`: no `MBRandom` in the source, `lock (RollGate)`
  present), and the `.claude/rules/harmony-patches.md` row ("two concurrent draws corrupt its XorShift state
  (reproduced on v1.5.5, 2026-10-15), so a new draw on this path uses its own locked generator"). The 1.4.x rule has no
  such row: a grep of `origin/bannerlord-1.4.5:.claude/rules/harmony-patches.md` for MBRandom, random or thread matches
  only lines 50, 67 and 99-117.
- Consequence on v1.4.8: UNVERIFIED. The corruption was reproduced on v1.5.5 (9f58c58de body); no player log shows it,
  and it is not shown to be the battle freeze.
- Port: write the 1.4.x fix in `TaomCombatMechanicsModel.BuildCrushThroughContext`, update the comment at
  `Main/Features/CombatMechanics/Domain/CrushThroughContext.cs:27`, add a source pin modelled on 9f58c58de's test, and
  add the harmony-patches.md multi-thread row naming `TaomCombatMechanicsModel` (not `CareerTroopHooks`).

### K1-03 42148af4c: the prefab hint-parent gate (SKIP-FEATURE to PORT-PARTIAL, P3)

- 42148af4c creates `TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs` (no later 1.5.x commit touches it). It
  builds every in-code PrefabExtension document and fails on a `HintWidget` whose widget parent sets
  `DoNotAcceptEvents="true"`. Its comment says the trap shipped three times, Messengers #898 among them, a feature on
  both lines.
- Both engine claims hold on v1.4.8: `HintWidget` subscribes to `ParentWidget.EventFire` and relays HoverBegin and
  HoverEnd (`...\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.Widgets\HintWidget.cs:15`,
  `:25-35`); hit-testing requires `!widget.DoNotAcceptEvents` (`...\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI\EventManager.cs:861`, `:897`).
- 1.4.x has five in-code PrefabExtensions (`git grep -l PrefabExtensionXmlDocument origin/bannerlord-1.4.5 -- Main`):
  CareerButtonPrefab, CareerEnergyBarPrefab, MessengerEncyclopediaPrefabExtension, SpecialResourcePrefab,
  TimeAccelerationPrefab. None nests a HintWidget under a `DoNotAcceptEvents` parent today (the Messengers and
  TimeAcceleration hints sit under ButtonWidgets without it), so the gate starts green; its value is preventive.
- No PORT row brings it: `PrefabHintParent` and `DoNotAcceptEvents` appear in no other classified row.
- Port: the test with `typeof(TAOM.Client.ClientSubModule).Assembly` replaced by the TAOM assembly and the floor 9
  replaced by 5, plus the one-sentence `.claude/rules/gui-ui.md` addition naming the gate (it extends the Hover targets
  paragraph that 267834a2e ports). Home: group issue-858-prefab-insert-gate (WP132), whose 69b47d00b already adapts the
  sibling `PrefabInsertPositionTests` to scan the TAOM assembly.

## In-scope hunks already absorbed by a PORT row (no reclassification)

| Skipped commit | In-scope hunk | Absorbed by (port_notes) | WP |
|---|---|---|---|
| c91380274 | controller null guard in `WargMissionBehavior.SwitchMainAgentController` and the tree callers | d96f349f1 ("re-implement c91380274's null guard") | 129 |
| 06a1a1b71 | messenger conversation open/close flag (`IMessengerConversationState`) | 1dc0a8d7c ("add only the open/close flag") | 114 |
| fcd140907 | localization-ui.md lesson "A hover target lies inside every ancestor's rectangle" | 267834a2e ("lift that lesson hunk too") | 134 |
| 0bd6abf33 | `ITextLocalizerAdapter` / `TextLocalizerAdapter` | 407b1e767 ("copy only those two small files") | 130 |
| eab517aad | `RegisterCustomBattleModels` seam | 25995dc91, b0154d86e (minimal re-implementation) | 110, 129 |
| 06b1deaab | `FellFoesMissionGate` | 639df895b (gate on `MountDespawnMissionGate`) | 112 |
| bc01a545c | `ObjectCreationHandling.Replace` lesson | dc275dc4e (state it inline or port that paragraph) | 136 |
| 813657bf3 | frozen `kitRank` save-key pin | 5a8c73d26 (relocate the test) | 125 |
| db805427b | `lord_template_rosters.xslt` data fix | bb68eeca3 item (3), maintainer call | 120 |
| 4528caf3f | `CampRestHealing` context in TaomPartyHealingModel | 5d70714c4 ("CampRestHealing do not exist on 1.4.x") | 125 |

End-state syncs: generic reviewer heuristics in `.ai/review-reference.md` (d13e91658 two entries, e0a6970dd two,
1d8c3ba76 one), `.claude/rules/csharp-architecture.md` rule 8 on bounding strings handed to native calls (d9b502771),
and generic lessons (for example 6b00881b6, 4eaedd8da, 10e2efb49) reach 1.4.x only through the WP134 and WP136
end-state transplants. Prune the Fords, Client and feature entries there; the source RCAs are absent on 1.4.x.

## Traps for plan writers

- **T1 (WP126, WP133).** 0761c01b7's `CareerButtonClickTests` already calls `IlCallScanner.Fingerprint`
  (`0761c01b7:TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs:94`), a27c6b338's helper, while its notes cite
  only `GameAssemblies`. 0ab3dc35a also needs `ExtractOpCodes` (K1-01).
- **T2 (WP132).** 1ebcf86a8's notes say take `test_item_registry_counts_only_items_documents`, but that test
  (`1ebcf86a8:tools/tests/test_validate_moduledata.py:672`) calls `vm.armour_acquisition_ref_issues` at :694, defined
  only by SKIP-FEATURE 04da4a0d0, and writes armour_acquisition fixtures. Port it without the armour fixtures and the :694
  assertion, using a non-Items fixture that 1.4.x has (a culture_marketplace_config-shaped file, per the row's why).
- **T3 (WP133).** 852678f86's `CastleRecruitmentService` constructor takes `VolunteerProductionService`
  (`852678f86:...CastleRecruitmentService.cs:35`; class created by SKIP-FEATURE d340a725c) and
  `CastleRecruitObserver.cs:50` calls `KingdomWarSnapshotAdapter.TryFloorElapsedDays` (file from d340a725c, helper from
  5ba8d6c82, SKIP-1.5-ONLY-FIX). The 1.4.x constructor is `CastleRecruitmentService(ICastleRecruitmentSettingsProvider
  settings)` (`origin/bannerlord-1.4.5:...CastleRecruitmentService.cs:20`). Keep it, add only the new outcome
  dependency, and host the 9-line finite-range-then-floor helper beside the observer.
- **T4 (WP133).** 8caac7da5's `ServiceSignatureGateTests` scans `TaomAssemblies.All` (:50, :63, :83), a helper created
  by SKIP-FEATURE a15563a3c (TAOM.Client split) and absent on 1.4.x; scan the TAOM assembly.
- **T5 (WP136).** 50e8dc7ae's notes say take the `docs/reference/engine` hunks, but its
  `settlement-economy-food-prosperity.md` hunk rewrites text that SKIP-FEATURE e91660cc9 (#769 E11) and 4528caf3f
  (#860) wrote: it would claim TaomPartyHealingModel overrides `GetDailyHealingForRegulars` (camp rest, WoundRecovery)
  and that TaomClanFinanceModel adds career DailyGold. 1.4.x overrides only `GetSurvivalChance` (:29) and
  `GetDailyHealingHpForHeroes` (:85), and the 1.4.x paragraph (`settlement-economy-food-prosperity.md:149-153`) is right.
  Skip that file's hunk.
- **T6 (WP129).** b0154d86e builds a 1.4.x Custom Battle damage model carrying the crush-through rule: do not copy the
  `MBRandom` draw into it (K1-02).

## Skips confirmed from the hunks (selection)

| Commit | Lead tested | Evidence | Outcome |
|---|---|---|---|
| fdb683039 | old save makes the map load loop forever (memory to 7 GB) | v1.4.8: `SandBoxNavigationCache.cs:28-31` returns `Settlement.Find(id)`; `NavigationCache.cs:542` opens the file, `:589` closes it, no try/finally. But the single-player caller catches: taom-src `SandBox.View.Map.SettlementPositionScript.cs:535-539` prints "Error when reading distance cache". E1 `rgl_log_1744.txt:4863-4869` and E2 `rgl_log_22112.txt:4866-4872` read TAOM_Map's `settlements_distance_cache_Default.bin` in 0.3 to 0.5 s with no error line | loop is server-only; the loading-hang lens can drop it for evidence item 11 |
| c0f290d9e, 2bf300972 | `Agent.FadeOut` leaves a rider linked (v1.5.6 native crash) | WP109's BattleCorpses only calls `Mission.SetMissionCorpseFadeOutTimeInSeconds` (`origin/bannerlord-1.5.x:Main/Features/BattleCorpses/Hooks/BattleCorpseMissionBehavior.cs:65`) | no live-mount fade gets ported |
| 3d70ca551, 1933508ee | a seat holding an agent hangs mission end | `origin/bannerlord-1.4.5:Main/Features/Elephant/ElephantMissionBehavior.cs:117` is the commented `TrySpawnHowdahCrew` call; `Main/Features/Mumakil` has no platform or standing-point file | no trigger on 1.4.x |
| 2a7653f23 | riding orders follow the formation | holds on v1.4.8 (`Formation.cs:2153`, `:2216`, `:2536-2541` called at `:2002` and `:2500`, `:2676`; `OrderController.cs:327`, `:337`; `HumanAIComponent.cs:267`), but `git grep SetRidingOrder origin/bannerlord-1.4.5 -- Main` is empty | P3 doc at most |
| cd67866b7 | Warg behaviour-tree edits | two `WargRageLog.Active?.` probes, fed by Fords telemetry | skip holds |
| 0700ca901, 92839bc52 (model part) | shared combat model edits | `extraFlags`, `ComposeBlowFlags` and verdict seams serve SignatureStrikes and SiegeForces | skip holds |
| 04da4a0d0 (tool part) | `audit_deleted_mesh_impact.py` | already on 1.4.x (8b4c16908); the commit only adds armour config reading | skip holds |
| ac0a3e0c0 | removes a per-call closure | closure came from 34b0e25a9; the 1.4.x TaomPartySizeModel has no lambda | no target |
| b4decf226 | renown and influence overflow clamps | 1.4.x TaomTournamentModel overrides only lines 25, 40, 46, 55, 64 (start and end chance, reward lists, participant armour) | no target |
| 646846cbd | edits the Patch69 crash guard | only threads the new alignment pass ahead of the guard; own MCM toggle | design change |
| 93a990e54 | new patrol-template gate | 1.4.x data passes (45 templates, 0 offenders); its 54-template floor would fail; the hazard (PatrolRosterService) is 1.5.x-only | skip holds |
| f25d8f5d6 | ViewCreatorModule gate | 1.4.x has no `[ViewCreatorModule]` class; its one `[DefaultView]` (`AutonomousMovementPlayerController.cs:15`) is handled by d96f349f1 | skip holds |
| 1b9c24234 | `Campaign.Current == null` guard in OOBOverlayService | presets default off (`TaomSettings.cs:786`) and the 1.4.x store is in-memory: UX, no crash path | skip holds |
| d340a725c | NaN path in TaomVolunteerModel | needs a non-finite vanilla base or feat result | too marginal |
| 5fcf8390c | DedicatedServerProvider to `GameNetwork.IsDedicatedServer` | both forms answer false on a client | co-op only |
| 8d883051e | unattended launcher, idle gate | needs the Fords autorun driver | skip holds |
| ea0592eb9 | #632 garrison re-manning, MELEE_LADDER_INVERSION gate | new behaviour with settings; the gate judges a new balance spec (`tools/melee_ladders.json`) | maintainer option, not reclassified |

## Recommendations for the orchestrator (I cannot run skills)

- WP107: run `/research` on `MBFastRandom` and `Mission.GetDefendCollisionResults` before coding the lock, and
  `/deep-review` after.
- WP126, WP132, WP133: after lifting the helpers, run the filtered tests for the touched files with the non-deploying
  commands in `.ai/verification.md`, then `/verify`.
