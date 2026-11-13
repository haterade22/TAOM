# Lane arch: findings (run 2026-11-12-fable)

Baseline `7087fa633`, worktree `wt-baseline`. Read-only lane. Scope: production C# under `Main/`,
`Client/`, `Dependencies/`, minus the BRIEF skip list. Findings are appended as found and ranked in the
summary table at the end.

## Measurements

All run from the worktree root at the baseline.

- **File list:** `git ls-files 'Main/*.cs' 'Client/*.cs' 'Dependencies/*.cs'` minus
  `Features/(HeroDefence|FieldBallista|CoopInterop|FordsOfWar|LongDefeat)/`: 4,217 files.
- **Sizes:** `wc -l` over that list, sorted. Top in-scope files: `Main/Features/TaomSettings.cs` 3,174
  (rejected as a god module earlier, plans/README.md:109), `Main/SubModule.cs` 2,393 (single-owner),
  `Main/Features/Refuge/RefugeService.cs` 1,448, `Main/Features/CulturalFeats/TaomCulturalFeats.cs`
  1,227 (table-driven rewrite rejected, plans/README.md:109),
  `Client/Features/SeatOfSeeing/Trial/CommandTrialScript.Seat.cs` 1,193,
  `Client/Features/FactionMap/Widgets/PolygonWidget.cs` 1,051, `Main/Features/FieldCamp/CampService.cs`
  1,028, `Main/Features/SupplyLines/SupplyCaravanService.cs` 943,
  `Main/Features/Enlistment/EnlistmentReconciler.cs` 907.
- **Duplication:** jscpd is not installed on this machine, so I wrote a read-only clone finder
  (`E:\repos\taom-improve\2026-11-12\scratch\lane-arch\dupes.py`): it drops blank, brace, using,
  attribute and comment lines, collapses whitespace, hashes every window of W normalized lines, and
  merges matching windows into runs. Run as
  `python -B dupes.py <worktree> files.txt 8 150`. Result at W=8: 79 clone runs, 878 duplicated
  normalized lines across 4,217 files; at W=12: 27 runs, 392 lines. The repo has little copy-paste;
  the findings below are the clusters with three or more copies or a real divergence risk. Output:
  `scratch\lane-arch\dupes8.txt`, `dupes12.txt`.
- **ADR-002 baseline:** `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` lists 18 entry
  points over 150 lines (shrink-only). The largest TAOM-owned ones: `MessengerCampaignBehavior` 668,
  `AutonomousMovementPlayerController` 665, `WarOfTheRingMomentumBehavior` 307,
  `EnlistmentBattleBehavior` 289, `SpecialResourcesBehavior` 262.
- **Composition:** `Main/Composition/FeatureModules.cs` lists 92 modules
  (`grep -c -E 'new Features\.' Main/Composition/FeatureModules.cs`); `Main/IoC.cs` still makes about
  100 hand-wired `XIoC.RegisterXFeature` calls (lines 97 to 216), and `Main/SubModule.cs` holds 57
  `AddBehavior` and 55 `AddModel`-family calls (`grep -c`). Module content is added after every
  hand-wired behavior (`Main/SubModule.cs:943-945`).

## Findings

### [ARCH-01] Move the messenger arrival verdict out of the largest ADR-002 entry point into MessengerService

- **Evidence**: `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`: `MessengerCampaignBehavior 668`, the
  largest TAOM-owned entry point in the shrink-only baseline. `Main/Features/Messengers/MessengerCampaignBehavior.cs:272-327`
  (`HandleArrivedMessenger`) decides on the raw `Hero` whether an arrived messenger is lost (dead, disabled,
  fugitive, inactive non-wanderer, spawned inactive wanderer), confused (target in the player's party), deferred
  (`IsPlayerAvailable`, `IsTargetAvailableNow` at `:620-632`: prisoner, child, in a map event) or ready. The same
  reachability rule exists a second time, tested, in `Main/Features/Messengers/MessengerService.cs:26-68`
  (`CanSendMessenger`, on a `HeroSnapshot` built by `MessengerTargetReader.cs:15-38`). The text mapping
  `GetReason` (`MessengerCampaignBehavior.cs:635-680`, 46 lines) is a second block of non-boundary code in the
  entry point.
- **Impact**: the arrival rule is decision logic in an entry point (ADR-002), it is the only copy of the
  "is the target still reachable" rule with no unit test (the 45 tests in `MessengerServiceTests.cs` cover the
  send-time copy only), and the two copies already differ: the arrival path also drops `Disabled` and spawned
  inactive wanderers. A later change to one copy will not reach the other.
- **Effort**: M. Add two facts to `HeroSnapshot` (`IsDisabled`, `IsSpawnedState` or the raw `HeroState` mapped to
  a TAOM enum, and `InMapEvent`), read them in `MessengerTargetReader`, add `MessengerService.ClassifyArrival(snapshot,
  playerAvailable)` returning a TAOM enum (Lost, Confused, Wait, Ready), unit-test each cell, and let
  `HandleArrivedMessenger` switch on it. Optionally move `GetReason` to a static `MessengerReasonText`.
  Lower the baseline number to the new count.
- **Risk**: MED. Two semantics to keep exactly: the arrival path compares
  `target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo` (`:293`, true when both are null), while the snapshot's
  `IsInPlayerParty` (`MessengerTargetReader.cs:19-21`) is false when either is null; and `ResolveHero` must keep
  scanning with `Hero.FindFirst` (`:614-618`, comment explains why). `MessengerCampaignBehaviorSourceTests.cs`
  (13 source-text pins) reads this file and must still pass. No save impact (no persisted field changes).
- **Confidence**: HIGH (read both copies and the reader).
- **Fix sketch**: classify in the service from a snapshot, switch in the behavior; the behavior keeps only the
  inquiry and notification calls.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes; it shrinks the biggest baseline entry with the existing 45 service tests as the
  harness and adds the missing arrival tests.
- **Issue**: none found (not searched on GitHub; this lane makes no `gh` calls).

### [ARCH-02] Give the three LotrIssue templates one static helper for their byte-identical methods

- **Evidence**: normalized-body hashes (`scratch\lane-arch\methodcmp.py`, comments and whitespace stripped):
  `GetFrequency` identical in `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:83`, `DeliverGoodsLotrIssue.cs:100`,
  `DeliverPersonnelLotrIssue.cs:83`; `CanPlayerTakeQuestConditions` (19 lines) identical at `:125`, `:143`, `:125`;
  `EnsureDef` identical at `:44`, `:46`, `:44`. `IssueStayAliveConditions` and `Tx` are identical in Combat and
  Personnel and differ in Goods; `GetIssueEffectAmountInternal` is identical in Goods and Personnel and differs in
  Combat. The clone finder ranks this the largest cluster (14 of 79 runs at W=8, 171 of 878 normalized lines; pairwise, so overlapping).
- **Impact**: three copies of the precondition rule (relation floor, at-war check) and of the frequency mapping;
  a change to who may take a TAOM quest needs three identical edits.
- **Effort**: S. A static `LotrIssueTemplateRules` with `MapFrequency(LotrIssueDefinition?)` and
  `Preconditions(int relationWithPlayer, int? relationMin, bool giverSettlementAtWar)` returning
  `PreconditionFlags`; each template's override becomes a one-line call. New unit tests for the two pure
  functions (no engine type beyond the `PreconditionFlags` and `IssueFrequency` enums).
- **Risk**: LOW, and save-safe only in this form. A shared base class was rejected in September because a new
  intermediate class shifts the `classLevel` of `[SaveableField(1)] _defId` and breaks saves
  (`plans/_audit/2026-09-23-opus/lane-2.findings.md:318`, `REPORT.md:186`). The helper adds no class to the
  hierarchy and moves no field.
- **Confidence**: HIGH.
- **Fix sketch**: as above. Today's only template test, `LotrIssueTemplateInvariantsTests.cs`, checks
  `IssueQuestCanBeDuplicated` per type; add the helper tests beside it.
- **Delta**: pre-existing (recommended as "do it" in `lane-2.findings.md:318-328`, never planned).
- **P1**: no.
- **Plan candidate**: yes, together with ARCH-05 and ARCH-06 as one "fold the safe twins" plan.
- **Issue**: none known.

### [ARCH-03] Extract RefugeService's save-load repair into one unit the existing LoadFrom tests already pin

- **Evidence**: `Main/Features/Refuge/RefugeService.cs` is 1,448 lines. About 480 of them (`:967-1446`) are the
  ADR-007 protected-virtual engine seams, a decided pattern. Of the logic half, the load repair is the most
  self-contained part: `LoadFrom` (`:643-666`, null-row scrub, counter floor, cache reset) and `RepairLoadedRow`
  (`:755-790`: key canonicalization, unknown tier, non-finite `BuildTargetHours`, future `BuildStartTime`,
  negative militia). It reads only `DaysSince`, `NowTime`, `_logger` and two constants. Pinned today by
  `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs:1262` (future build start), `:2048` (null book), `:2060`
  (null rows), `:2143` (divergent inner id), `:2156` and `:2169` (NaN and infinite build target), `:2183`
  (unknown tier, negative militia).
- **Impact**: a reader of the 1,448-line service has to separate persistence repair from founding, raids, the
  frame tick and party operations (Kimi lead 2). The repair is the part most likely to grow (each new
  `RefugeData` field gets a repair line) and the cheapest to isolate.
- **Effort**: S to M. New `Main/Features/Refuge/Domain/RefugeBookRepair.cs` (internal static), taking the book,
  `Func<CampaignTime, double> daysSince`, `CampaignTime now` and the logger, returning the scrubbed book; the
  service's `LoadFrom` keeps the counter floor and cache resets and calls it with its own seams, so the test
  subclasses' overrides still apply. The seven tests above pass unchanged; optional direct tests on the unit.
- **Risk**: LOW. The order of the repairs, the warning texts (log lines may be read by people triaging) and the
  "only non-finite targets" rule must stay as they are. No persisted field changes.
- **Confidence**: HIGH (read the code and the test names; did not run the tests).
- **Fix sketch**: move, do not rewrite; one commit, the existing suite is the proof.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes; this is the "one extraction with a clean verification story" for RefugeService. The
  raid selection (`FindNearestHostile`, `:889-925`) is already a seam-fed pure loop with its own tests
  (`:1375`, `:1384`) and needs no move.
- **Issue**: none known.

### [ARCH-04] Build the war ram and great elk trees from the parameterized Animalia tree

- **Evidence**: `Main/Features/Elk/ElkBehaviorTree.cs` (67 lines), `Main/Features/WarRam/WarRamBehaviorTree.cs`
  (72) and `Main/Features/Animalia/AnimaliaBehaviorTree.cs` (59) have the same blackboard, the same constructor
  and the same node list; `diff` with names masked shows only comments and the sequence label differ
  ("antler charge" `Elk...:55`, "kick attack" `WarRam...:60`, "antler attack" `Animalia...:47`). All three
  cooldowns are 35.0 (`ElkConfig.cs:54`, `WarRamConfig.cs:52`, `AnimaliaConfig.cs:47`). Animalia already takes the
  profile as a parameter (`AnimaliaBehaviorTree.cs:40`, registered twice at `AnimaliaMissionBehavior.cs:46-47`);
  Elk and WarRam hard-code theirs. The mission behaviors repeat too: `ElkMissionBehavior.cs` against
  `WarRamMissionBehavior.cs` scores 0.95 similarity with names masked (`scratch\lane-arch\sim.py`; the diff is
  comments and log text).
- **Impact**: four registrations of one tree shape in three classes; a pacing or node change (the sleep
  cadence, a new decorator) needs three edits, and the base(10) comment already exists in three wordings.
- **Effort**: S for the trees: move the Animalia tree to `Main/Features/ElephantLike/` as
  `SingleChargeBehaviorTree.Build(objects, profile, cooldownSeconds)`, register `"ElkTree"` and `"WarRamTree"`
  through it, delete the two classes. M if the Elk and WarRam mission behaviors are folded too (keep the
  `[Elk]` and `[WarRam]` log prefixes and texts: run judges and people grep them).
- **Risk**: LOW for the trees. The registration names must stay; the tree class name appears only in the
  library's exception texts (`Main/BehaviorTrees/BehaviorTreesCore.cs:95-127`). No unit test can build the tree
  (it needs a live `Agent`), so the proof is the build, the existing `ElkConfigTests`, `WarRamConfigTests`,
  `AnimaliaWiringTests` and an in-game ride of each mount (owed).
- **Confidence**: HIGH on the duplication, MED on "no other reader of the tree type" (searched `*.cs` in the
  worktree with `git grep`).
- **Fix sketch**: one tree builder, three registrations; the mission behavior fold as an optional second step.
- **Delta**: introduced after the September audit (Elk `c79a58521` 2026-09-23, Animalia `709649c3f`
  2026-09-24; the September lane rejected only the Elephant and Mumakil pair, `lane-2.findings.md:319`).
- **P1**: no.
- **Plan candidate**: yes for the trees (S); the mission behavior fold only with an in-game check scheduled.
- **Issue**: none known.

### [ARCH-05] Fold SupplyGoodRowVM and SupplyTroopRowVM into one row VM that takes its label

- **Evidence**: `diff Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs Main/Features/SupplyLines/UI/SupplyTroopRowVM.cs`
  (79 lines each): the only code differences are the class and constructor names (`:12`, `:18`) and the label
  (`:25`, `:27`: `{=taom_sl_good_row}...{STOCK}` against `{=taom_sl_troop_row}...{COUNT}`). The comment at
  `SupplyTroopRowVM.cs:8-10` says the twin exists because the prefab binds two item templates; a Gauntlet item
  template binds property names, not VM types (the September lane's reading, `lane-2.findings.md:316`). Users:
  `SupplyOrderScreenVM.cs:31-32`, `:256`, `:259`, `:660`, `:664`, `:753`.
- **Impact**: two copies of the clamp-and-callback contract (the second-largest clone run, 24 normalized lines);
  a fix to one row's clamp is easy to miss in the other.
- **Effort**: S. One `SupplyRowVM(SupplyLineItem, Action, TextObject label)`; the two lists become
  `MBBindingList<SupplyRowVM>`; the label keys and variables stay byte-identical (no localization change).
- **Risk**: LOW to MED. `TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs:39-40`, `:187-188` map each
  list to its VM type and must be updated to the one type; whether Gauntlet resolves both item templates against
  one VM type is UNVERIFIED in game (the screen opening and both lists filling is the check).
- **Confidence**: MED (the code duplication is certain; the prefab claim is not run in game).
- **Fix sketch**: as above, one commit, `SupplyOrderScreenVMTests` and the prefab binding test as the proof.
- **Delta**: pre-existing (recommended in September, not planned).
- **P1**: no.
- **Plan candidate**: yes, inside the ARCH-02 "safe twins" plan.
- **Issue**: none known.

### [ARCH-06] Share the CultureStageView type probe that three FactionMap patches copy

- **Evidence**: `Main/Features/FactionMap/Hooks/CultureStageView_Constructor_Patch.cs:22`,
  `CultureStageView_Finalize_Patch.cs:22` and `CultureStageView_Tick_Patch.cs:22` each hold the same
  `PossibleTypeNames` list (two engine type names, `CultureStageView_Tick_Patch.cs:16-20`) and the same
  `FindCultureStageViewType()` (9 lines, identical normalized hash).
- **Impact**: the list is an engine-version probe; the next engine rename of the view needs three edits, and a
  missed one silently disables one of the three patches through `Prepare()` (`:32`).
- **Effort**: S. One `internal static class CultureStageViewType { static Type? Find(); }` in the same folder;
  each patch's `Prepare` and `TargetMethod` call it.
- **Risk**: LOW. Harmony calls `Prepare` and `TargetMethod` by name; they stay in each patch class. The binding
  gate and the patch category (`Patch7_FactionMap`) are unchanged. Applying the patch is engine-bound, so the
  build plus the existing FactionMap binding tests are the proof (no game run needed for a pure move).
- **Confidence**: HIGH.
- **Fix sketch**: move, three call sites.
- **Delta**: pre-existing (September "Keep, S", `lane-2.findings.md:322`).
- **P1**: no.
- **Plan candidate**: yes, inside the ARCH-02 plan.
- **Issue**: none known.

### [ARCH-07] Delete members nothing calls

- **Evidence**: `scratch\lane-arch\deadmethods.py` lists every method in scope whose name occurs exactly once in
  all tracked `.cs`, `.xml`, `.xslt`, `.json`, `.txt`, `.py` and `.ps1` files (the declaration itself; attributed,
  `override`, `virtual` and `abstract` members skipped): 18 hits. After reading each, these are dead:
  `Dependencies/Foundation/ReflectionUtils.cs:51` `TryInvokeStatic` and `:70` `GetAssemblySimpleName` (their doc
  comments still name `VersionProbe` and `SaveShield` as callers; neither calls them,
  `git grep -n 'ReflectionUtils\.'`); `Main/Features/CareerSystem/Mutations/MutationParams.cs:29` `GetBool` and
  `:35` `GetInt`; `Main/Features/CultureDoctrine/Domain/FormationRouting.cs:54` `IsRoutable`;
  `Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs:21` `FromCurrent`;
  `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:89` `ExecuteToggleExpand` (no prefab binds it; the
  diamond grid replaced the expandable panels, `docs/reviews/rca-career-e4e7-2026-10-14.md:78`);
  `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:649` `OnWeaponUsageToggleRequested`, the
  only writer of `_weaponUsageToggleRequested` (`:680`), so the `|| _weaponUsageToggleRequested` clause at `:556`
  and the reset at `:559` are dead too. The other hits are by design: five empty `IMissionListener` members in
  `MessengerCampaignBehavior.cs:501-505`, vendored BehaviorTrees code (`Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs:80`,
  `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:156`), and three `TcbVM` commands (ARCH-08).
- **Impact**: about 60 lines a reader must check for callers; two stale doc comments that name callers that do
  not exist.
- **Effort**: S.
- **Risk**: LOW. Gauntlet binds `Execute*` by name from prefabs: the search covered every tracked XML in this repo,
  not the live `TAOM_Map` or `LOTRLOME_Armory` installs (a TAOM VM bound from another module's prefab is
  unlikely but UNVERIFIED). `CareerChoiceGroupObjectVM.IsExpanded` (`:48`) may become dead with the command;
  check `CareerScreenPrefabBindingTests.cs` before removing it. The scan misses dead methods whose name is shared
  with another member, so this is a floor, not the full count.
- **Confidence**: HIGH for each listed member (read each and its zero-caller search).
- **Fix sketch**: one deletion commit; the full suite is the proof.
- **Delta**: pre-existing, except `OnWeaponUsageToggleRequested` (not dated).
- **P1**: no.
- **Plan candidate**: yes (S, pure deletion).
- **Issue**: none known.

### [ARCH-08] The custom battle muster VM exposes three commands no prefab binds (bug lead)

- **Evidence**: `Client/Features/CustomBattles/Muster/TcbVM.cs:93` `ExecuteUseOldScreen` and its text
  `OldScreenText` (`:54`), `:458` `ExecuteClosePalette`, `:468` `ExecuteCloseIcons`: none is named in any tracked
  XML (`git grep -n -E 'OldScreenText|ClosePalette|CloseIcons' -- Main/_Module/GUI` returns nothing), while
  `Client/Features/CustomBattles/Muster/Hooks/CustomBattleScreen_OnInitialize_Patch.cs:16-17` documents that
  "Old screen" removes the muster layer. The popups bind `IsPaletteOpen` and `IsIconsOpen`
  (`Main/_Module/GUI/PreFabs/CustomBattle/Muster/TcbPopups.xml:166`, `:205`). All of it landed today in
  `df8ba8484` (2026-11-12).
- **Impact**: if the prefab is final, the player has no way back to the vanilla screen that the patch comment
  promises, and the two popups may have no close button. If the VM is ahead of the prefab, this is work in
  flight. Either way it is not a refactor.
- **Effort**: S once the intent is known.
- **Risk**: n/a for this lane.
- **Confidence**: MED (bindings read; the screen not seen in game; the popups may close through another path,
  for example a swatch pick, which I did not trace).
- **Fix sketch**: ask the author whether the buttons are meant to exist; bind them or delete the commands.
- **Delta**: introduced (today).
- **P1**: no.
- **Plan candidate**: no (behaviour change); for the orchestrator to raise as an issue or a question.
- **Issue**: none known.

### [ARCH-09] Move the Messengers hand wiring into its existing feature module

- **Evidence**: Messengers is half migrated. `Main/Features/Messengers/MessengerSessionRulesModule.cs` is already
  in `Main/Composition/FeatureModules.cs` (patch category `Patch117_MessengerSessionRules`), but the services are
  still registered by hand (`Main/IoC.cs:171`, `MessengerIoC.RegisterMessengerFeature`) and the behavior is added
  by hand (`Main/SubModule.cs:1400`). The module contract (`Main/Composition/TaomFeatureModule.cs`) and a template
  (`Main/Features/InnRounds/InnRoundsModule.cs`: `RegisterServices`, `OwnsSaveData`, one `CampaignBehaviorDecl`)
  cover all three parts.
- **Impact**: one feature wired in three places, two of them single-owner files that parallel sessions collide
  in (plan 018's motivation). Kimi lead 1: move one leaf at a time; this is the leaf with the least new work
  because its module already exists.
- **Effort**: S to M, including the wiring tests (`TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs:34`
  and `:45` read `IoC.cs` and `SubModule.cs` as text and must move to the module, as `MessengerSessionRulesWiringTests`
  does).
- **Risk**: HIGH (single-owner files `Main/IoC.cs`, `Main/SubModule.cs`, `Main/Composition/FeatureModules.cs`).
  Behaviour note: module behaviors are added after every hand-wired one (`Main/SubModule.cs:943-945`), so the
  messenger behavior's event listeners move later in the add order; with newest-first listener order
  (orientation.md "Event listener order") its hourly tick would run earlier relative to other behaviors. Its
  hourly work reads only its own store, so I expect no change, but that is UNVERIFIED. `OwnsSaveData => true`
  is required (SyncData holds in-flight messengers), and the registration must stay unconditional
  (`SubModule.cs:1397-1399` comment).
- **Confidence**: MED.
- **Fix sketch**: rename the module to `MessengersModule`, add `RegisterServices` and the behavior decl, delete
  the two hand-wired lines, move the text pins.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, only through the single-owner process (the maintainer or the owning session lands the
  `IoC.cs` and `SubModule.cs` deletions).
- **Issue**: none known.

### [ARCH-10] Make FamilyTreeLayer a use of the generic encyclopedia popup layer

- **Evidence**: `Client/Features/Realms/Popups/RealmsPopupLayer.cs` (120 lines, `91469f715`, 2026-11-10) is the
  generic form: `Show(string movieName, Func<Action, ViewModel> buildViewModel)` (`:40`).
  `Client/Features/LinesOfDescent/UI/FamilyTreeLayer.cs` (127 lines) is the same class with a fixed movie and VM
  and one extra close condition, `LeftClanMembersTab()` (`_requireClanMembersTab`). `diff` of the two (comments
  stripped) shows only those differences plus the base constructor's third argument (`false` in Realms).
  The lifecycle code (tick close, deactivate, finalize, teardown with focus, input restrictions, movie release,
  event unsubscribe) is otherwise identical: clone runs at `FamilyTreeLayer.cs:56-76` and `:98-124`.
- **Impact**: two copies of a lifecycle with known traps (the finalize path must not remove the layer twice,
  `RealmsPopupLayer.cs` comment in `OnFinalize`); a fix in one will not reach the other.
- **Effort**: S. Move the generic layer to a shared client UI folder, give it an optional `Func<bool>` close
  predicate, and let the family tree pass its tab check and movie name.
- **Risk**: MED. The base constructor argument differs (`FamilyTreeLayer.cs:36` `base("TaomFamilyTree", LayerOrder)`
  against `RealmsPopupLayer.cs:33` `base("TaomRealmsPopup", LayerOrder, false)`); the executor must read `GauntletLayer`'s constructor with
  `pwsh tools/taom-src.ps1 path GauntletLayer` and keep each caller's value. No unit test covers either layer;
  the proof is opening both popups in game (owed).
- **Confidence**: MED.
- **Fix sketch**: as above.
- **Delta**: introduced (2026-11-10).
- **P1**: no.
- **Plan candidate**: yes, low priority; only with an in-game check.
- **Issue**: none known.

## Ranking

| Rank | Id | Title | Effort | Risk | Confidence |
|---|---|---|---|---|---|
| 1 | ARCH-02 | One static helper for the LotrIssue templates' identical methods | S | LOW | HIGH |
| 2 | ARCH-03 | Extract RefugeService's load repair | S to M | LOW | HIGH |
| 3 | ARCH-07 | Delete members nothing calls | S | LOW | HIGH |
| 4 | ARCH-01 | Messenger arrival verdict into MessengerService | M | MED | HIGH |
| 5 | ARCH-04 | One single-charge tree for war ram, elk and Animalia | S | LOW | HIGH |
| 6 | ARCH-06 | Share the CultureStageView type probe | S | LOW | HIGH |
| 7 | ARCH-05 | One supply row VM | S | LOW to MED | MED |
| 8 | ARCH-09 | Messengers hand wiring into its module | S to M | HIGH | MED |
| 9 | ARCH-10 | FamilyTreeLayer onto the generic popup layer | S | MED | MED |
| 10 | ARCH-08 | Unbound muster VM commands (bug lead) | S | n/a | MED |

ARCH-02, ARCH-05 and ARCH-06 fit one "safe twins" plan. ARCH-01 has the largest structural payoff (the biggest
baseline entry, an untested rule) but needs the semantic care listed under Risk.

## Considered and rejected

- **Single-implementation service interfaces:** `scratch\lane-arch\ifaces.py` finds 771 interfaces in scope, 130
  of them with one production implementer, no test implementer, no `Substitute.For`/`Mock` and not an adapter
  (18 of the 130 are `IOn*` patch hooks and three more are `*Hook` interfaces, allowed reason 4). ADR-002's 2026-09-24 amendment decides "not
  deleted in bulk; one goes when its file is next touched" (`docs/adrs/002-thin-entry-points.md:84`). Not a finding.
- **Service locator inside services:** 854 `IoC.Resolve` calls in 210 in-scope files; in files named
  `*Service`, `*Provider`, `*Policy`, `*Store`, `*Registry` or `*Rules`, only two hits: a comment in
  `CharacterCreationContentService.cs:35`, `:245` and a static test seam in `TaomStartOptionsProvider.cs:66`. Clean.
- **TaomCombatMechanicsModel against TaomCustomBattleDamageModel** (largest clone run, 27 lines): two engine bases
  (`SandboxAgentApplyDamageModel`, `CustomAgentApplyDamageModel`), each override is already a one-line
  delegation, and the chains differ on purpose (the campaign adds the career crush hold). Nothing to share.
- **RefugeService and CampService as god modules:** about half of each is ADR-007 protected-virtual seams
  (`RefugeService.cs:967-1446`, `CampService.cs:734-1023`), a decided pattern; the fortification search they
  shared is already one `SettlementSite`/`NearestDistance` (`CampService.cs:44-72`). One extraction (ARCH-03),
  no rewrite.
- **Elephant and Mumakil twins, Warg BT nodes, Spider nodes:** rejected in September (`lane-2.findings.md:319-321`);
  the clone runs at the baseline are 10 to 16 lines, unchanged in kind.
- **InnRounds against NotableServices reveal dialog** (15 lines, two copies): they already share
  `NotableServicesTexts`; the rest is per-feature fault labels.
- **PlayerSwitchContentHandler against PlayerSwitchRegistrationBehavior** (18 lines): constructor plumbing.
- **Fords and LongDefeat adapter clones** (`FordsMenuAdapter`/`LongDefeatMenuAdapter`, the agent registries, the
  battlefield and effects adapters): both features are on the skip list.
- **JSON config loaders:** 56 in-scope files call `JsonConvert.DeserializeObject`; the September triage owns the
  config-provider pattern (triage-B) and Kimi lead 4 advises against a global base. Watch item only.
- **Constant feature gates** (`HowdahCrewSpawner.CrewSpawnEnabled`, `TaomHowdahMachine.BoneTrackingEnabled`,
  `MumakilCrewSpawner.CrewSpawnEnabled`): documented diagnostic gates kept wired on purpose
  (`TaomHowdahMachine.cs:252`), not rolled-out flags.
- **Unreferenced types:** `scratch\lane-arch\deadtypes.py` finds 184 type names used only in their declaring
  file; on reading, nearly all are private nested records, DTOs read by JSON, console cheat classes found by
  reflection, or culture tag types; `IBTMovable` and `BannerlordNoWaitDecorator` belong to the inlined
  BehaviorTrees wrapper (decided). No dead type worth a finding.

## What I did not cover

- The skip-list folders (HeroDefence, FieldBallista, CoopInterop, CoopCompat, FordsOfWar, LongDefeat) and the
  single-owner files except where ARCH-09 recommends an edit.
- Category 6 (dependencies and migrations): no BUTR pin, NuGet or engine-drift check this run.
- Shallow-module deepening across the 771 interfaces beyond the count; I did not apply the deletion test to
  individual wrappers.
- The remaining ADR-002 baseline entries (`AutonomousMovementPlayerController` 665, `WarOfTheRingMomentumBehavior`
  307, `EnlistmentBattleBehavior` 289, `SpecialResourcesBehavior` 262 and the rest): no first extraction named.
- The large client files (`CommandTrialScript.Seat.cs` 1,193, `PolygonWidget.cs` 1,051, `SeatSession.cs` 850,
  `TcbVM.cs` 741) and the large services `SupplyCaravanService.cs` 943 and `EnlistmentReconciler.cs` 907.
- Dead members whose names are shared with another member (the scan counts names, not symbols), dead fields and
  never-read properties, and fully rolled-out MCM options.
- Nothing was built or tested (`dotnet` not run, by the task). No GitHub issue search.
