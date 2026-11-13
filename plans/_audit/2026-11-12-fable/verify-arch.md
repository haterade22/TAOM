# Verify lane arch: refuter (run 2026-11-12-fable)

Read-only refuter for ARCH-01 to ARCH-07. Baseline `7087fa633`, worktree `wt-baseline`. No dotnet run.
Commands were run from the worktree root (`git -C E:/repos/taom-improve/2026-11-12/wt-baseline ...`).

## ARCH-07: delete members nothing calls

**Verdict: CONFIRMED** (all eight members dead in every scope I searched), with three corrections.

**Ran** (worktree root): `git grep -n -w '<name>'` over all tracked files for `TryInvokeStatic`,
`GetAssemblySimpleName`, `GetBool`, `GetInt`, `IsRoutable`, `FromCurrent`, `ExecuteToggleExpand`,
`ToggleExpand`, `OnWeaponUsageToggleRequested`, `_weaponUsageToggleRequested`, `WeaponUsageToggleRequested`,
`IsExpanded`. A `-w` search over every tracked file covers `nameof(...)`, `AccessTools.Method(..., "X")`,
`GetMethod("X")`, Harmony `TargetMethod` strings, console attributes and every GUI XML under
`Main/_Module/GUI` and `Client` (they are tracked files). Also: `grep -r -l -E` for the command and
ReflectionUtils names over the live `TAOM_Map` and `LOTRLOME_Armory` `*.xml`/`*.xslt` (no hit), and
`grep -r -l -a 'TryInvokeStatic|GetAssemblySimpleName' --include=*.dll` over every installed module under
`Modules/` (hits only the declaring `TAOM.Dependencies.dll` copies and AWOIAF's own `AWOIAF.Dependencies.dll`;
no consumer assembly carries a member reference to them).

| Member | Result | Hits outside the declaration |
|---|---|---|
| `ReflectionUtils.TryInvokeStatic` (`Dependencies/Foundation/ReflectionUtils.cs:51`) | dead | only `docs/changelog-archive/CHANGELOG-2026-H1.md:4622` (archive) |
| `ReflectionUtils.GetAssemblySimpleName` (`:70`) | dead | same archive line |
| `MutationParams.GetBool` (`Main/Features/CareerSystem/Mutations/MutationParams.cs:29`) | dead | `plans/002-...md:284,329` (a merged plan told its executor to leave them alone; scope note, not a keep decision) |
| `MutationParams.GetInt` (`:35`) | dead | the same plan lines, `CHANGELOG.md:26839`, `docs/reviews/deep-review-002-...md:183` (history) |
| `FormationRouting.IsRoutable` (`Main/Features/CultureDoctrine/Domain/FormationRouting.cs:54`) | dead | none |
| `StackFrameSnapshotBuilder.FromCurrent` (`Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs:21`) | dead | none (`FromException` and the private `Build` stay) |
| `CareerChoiceGroupObjectVM.ExecuteToggleExpand` (`Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:89`) | dead | `docs/features/gui-sprite-system.md:241`, `docs/reviews/rca-career-e4e7-2026-10-14.md:78` (both say no prefab binds it) |
| `AutonomousMovementPlayerController.OnWeaponUsageToggleRequested` (`Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:649`) plus the field `:680`, the clause `:556` and the reset `:559` | dead | none; the class is a `MissionView` (`:17`), not a `MissionMainAgentController`, so the vanilla callers that resolve `MissionMainAgentController` cannot reach it, and `IAutonomousMovementController` does not declare it (`-w` found one hit only) |

**Corrections to the lane:**

1. `AutonomousMovementPlayerController` is in the shrink-only baseline at 665
   (`TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`). `EntryPointLineCountGateTests.Compare` fails a listed
   class that is *below* its baseline (`TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs:78`: "lines is below
   its baseline ... lower it"). Deleting the method, field and reset line therefore fails the suite unless the executor
   regenerates that row (`TAOM_WRITE_ENTRY_POINT_BASELINE=1`, run `Baseline_Regenerate_WhenAsked`). The lane did not
   say so.
2. `IsExpanded` (`CareerChoiceGroupObjectVM.cs:48`) and `_isExpanded` (`:10`) become write-never-read once the command
   goes; the only other `IsExpanded` hits are a different VM (`Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs:301`,
   bound by `TAOMEncyclopediaItemList.xml:31,35`). No test names it (`git grep -n IsExpanded -- 'TAOM.Tests/*'`: no hit).
   It is a `[DataSourceProperty]`; deleting it is safe only because no prefab in the repo or the two live modules binds
   it on the career group. The docs `gui-sprite-system.md:241` and `:304` name these members and need the same edit.
3. The ReflectionUtils class summary (`ReflectionUtils.cs:7-9`) says the helpers are used by "PatchShield, SaveShield,
   VersionProbe"; after the deletion only `FindTypeAcrossLoadedAssemblies` is left, used by `IncompatibleModDetector.cs:222`,
   `SaveShield.cs:195`, `VersionProbe.cs:68` and CoopInterop (skip list, unaffected). Correct the summary in the same commit.

**Conditions for a behaviour-preserving change:** delete exactly these members; keep `ProcessWeaponToggleInput` reading
`Input.IsGameKeyPressed(17)` and setting `ToggleAlternativeWeapon` (the `|| false` clause and the reset are the only
removals there); keep `WargControllerSwapTests` source pins green (they read `SwitchMainAgentController`, untouched);
regenerate the one baseline row; fix the two doc lines and the ReflectionUtils summary. `TAOM.Dependencies.dll` is a
separately shipped assembly: removing two public statics is a binary break only for a consumer, and none exists in the
repo or the installed modules (scope above). No save, Harmony or prefab effect. The existing suite plus a build proves it;
no in-game check needed.

**Recalibrated:** impact LOW (about 40 lines and three stale doc comments), effort S, risk LOW.

## ARCH-02: one static helper for the LotrIssue templates

**Verdict: CONFIRMED**, with one correction that widens it slightly (a fourth copy of the frequency map).

**Re-read:** `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:44-47` (`EnsureDef`), `:83-93` (`GetFrequency`),
`:125-143` (`CanPlayerTakeQuestConditions`); `DeliverGoodsLotrIssue.cs:46-49`, `:100-110`, `:143-161`;
`DeliverPersonnelLotrIssue.cs:44-47`, `:83-93`, `:125-143`. Read side by side, the three bodies of each method are
token-identical at the baseline. The shared bodies are:

```csharp
public override IssueFrequency GetFrequency()
{
    EnsureDef();
    if (_def == null) return IssueFrequency.Common;
    switch (_def.Frequency)
    {
        case IssueFrequencyTier.VeryCommon: return IssueFrequency.VeryCommon;
        case IssueFrequencyTier.Rare: return IssueFrequency.Rare;
        default: return IssueFrequency.Common;
    }
}

protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
{
    skill = null; relationHero = null; requiredGold = 0; flag = PreconditionFlags.None;
    EnsureDef();
    if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
    { flag |= PreconditionFlags.Relation; relationHero = issueGiver; }
    if (issueGiver.CurrentSettlement != null
        && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
    { flag |= PreconditionFlags.AtWar; }
    return flag == PreconditionFlags.None;
}

private void EnsureDef()
{
    if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
}
```

**Ran:** `git grep -n -E 'IssueFrequencyTier|PreconditionFlags' -- 'Main/*.cs'`, `git grep -n MapFrequency`,
`git grep -n -l -E 'CombatLotrIssue|DeliverGoodsLotrIssue|DeliverPersonnelLotrIssue' -- 'TAOM.Tests/*' 'tools/*'`,
`git grep -n 'LotrIssues/Templates'`.

**Corrections to the lane:**

1. The frequency map exists a **fourth** time: `Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:144-152`
   (`private static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)`, used at `:112` for
   `PotentialIssueData`). The helper should be `MapFrequency(IssueFrequencyTier)` (the null-def `Common` fallback stays
   in each template, or the helper takes `LotrIssueDefinition?`), and the behavior's private copy should call it too.
   `LotrIssuesCampaignBehavior` is an entry point but is not in the line-count baseline, so its shrink needs no
   baseline edit.
2. `EnsureDef` is identical but reads and writes instance fields (`_def`, `_defId`, `Service`); it cannot move to a
   static helper without `ref` plumbing that adds more than it removes. The lane's own sketch already leaves it; the
   evidence line listing it as shareable overstates the win.
3. Tests that cover the templates: `TAOM.Tests/Features/LotrIssues/LotrIssueTemplateInvariantsTests.cs:23-40` (one
   reflection test, `IssueQuestCanBeDuplicated`, three `DataRow`s) and one source-text pin,
   `TAOM.Tests/Features/Enlistment/AttributionAudit/EnlistedSiegeCreditTests.cs:309-311`, which reads
   `CombatLotrIssue.cs` for `CareerQuest.IsPlayersCapture(capturer, Campaign.Current?.MainParty?.Party, false)` (in the
   quest class, untouched by this change). No test exercises `GetFrequency` or `CanPlayerTakeQuestConditions` today, so
   the existing suite proves nothing about the moved logic; the new helper tests are the proof.
   `tools/oneoff/_harvest_lotr_issue_strings.py:5-9` reads the three template files for `{=KEY}` literals; the change
   moves no string, so its output is unchanged.

**Save:** `LotrIssueSaveableTypeDefiner.cs:30-37` registers the six template and quest classes by id; a new
`internal static` class has no instance and is not registered, adds no class to any `IssueBase` hierarchy and moves no
`[SaveableField]`. Save-safe, as the lane says (the September rejection was of a shared base class).

**Conditions for a behaviour-preserving change:**
- Precondition helper keeps: the out-parameter defaults (`null`, `null`, `0`, `None`); `EnsureDef()` runs before
  `_def` is read; the relation floor default `-10` when `_def` or `RelationMin` is null; strict `<`; `relationHero` set
  to the giver only on the relation failure; the at-war test only when `issueGiver.CurrentSettlement != null`
  (short-circuit: the caller must compute `giverSettlementAtWar` with the same `&&`, or `Hero.MainHero.MapFaction` is
  read where it was not before); `GetRelationWithPlayer()` always evaluated once; return `flag == None`.
- Frequency helper keeps `VeryCommon` and `Rare` mapped, everything else (including `Common` and unknown values) to
  `Common`, and the null-def `Common` fallback.
- The overrides stay in each template (the engine calls them virtually); only their bodies shrink.
- New unit tests: the frequency map over every `IssueFrequencyTier` value, and the precondition decision over relation
  below, at and above the floor, null floor, and at war or not. Engine enums (`IssueBase.IssueFrequency`,
  `IssueBase.PreconditionFlags`) are plain values; if the test touches no other engine type it needs no
  `RequiresGame` category, but the executor must run the RefAsm unit step (`.ai/verification.md`) to prove it.
- No in-game check needed: the moved code is pure decision logic over values.

**Recalibrated:** impact LOW to MED (four copies of the frequency map, three of the take rule, about 60 lines), effort
S, risk LOW.

## ARCH-03: extract RefugeService's save-load repair

**Verdict: NARROWED.** The extraction is sound and save-neutral, but the lane's sketch (pass `CampaignTime now`) would
break two existing tests and change when the engine clock is read. It is behaviour-preserving only with lazy seams.

**Re-read:** `Main/Features/Refuge/RefugeService.cs:643-666` (`LoadFrom`), `:755-790` (`RepairLoadedRow`), `:114`
(`MinBuildTargetHours = 0.1f`, also used by the build-target clamp at `:878-879`), `:988` (`protected virtual
CampaignTime NowTime() => CampaignTime.Now`), `:992` (`protected virtual double DaysSince(CampaignTime time) =>
(CampaignTime.Now - time).ToDays`). Only caller of `RepairLoadedRow`: `:661`. Only production caller of `LoadFrom`:
`Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs:88`. The lane's line numbers are correct.

**Ran:** `grep -n -E 'RepairLoadedRow|LoadFrom' Main/Features/Refuge/**`, `grep -n -E 'LoadFrom\(' TAOM.Tests/Features/Refuge/*.cs`,
`grep -n 'override .*(DaysSince|NowTime)|class .* : RefugeService' TAOM.Tests/Features/Refuge/*.cs`,
`git grep -n -l 'RefugeService.cs' -- 'TAOM.Tests/*' 'tools/*'` (no source-text pin reads the file).

**Test seams (the question asked):** there are two subclasses.
- `TestableRefugeService` (`TAOM.Tests/Features/Refuge/RefugeServiceTests.cs:30`) overrides `NowTime() => default`
  (`:99`) and `DaysSince(...) => Days` (`:101`). `LoadFrom_FutureBuildStart_ResetsSoTheBuildCanFinish` (`:1262-1290`)
  depends on both: `Days = -41.0` makes the row "future", and it asserts the reset lands exactly on `default`
  (`NowTime()`).
- `HeroMergeProbeService` (`:2312`) overrides **neither** seam, and its two tests call `probe.LoadFrom(book, 0)` with
  non-building rows (`:2365`, `:2392`). Today they never reach `CampaignTime.Now` because `DaysSince` is behind
  `data.Building &&` and `NowTime()` is behind the future-start test. An extraction that evaluates `NowTime()` once up
  front (the lane's `CampaignTime now` parameter) calls the unoverridden base, reads `CampaignTime.Now` with no live
  campaign, and those two tests fail; in game it adds a clock read on every load that had none.

**Corrections to the lane:**
1. The unit must take `Func<CampaignTime, double> daysSince` and `Func<CampaignTime> now` (method groups bound to the
   instance virtuals: `RefugeBookRepair.RepairRow(key, data, DaysSince, NowTime, _logger)`; a method-group delegate to a
   virtual dispatches to the override), invoked in the same conditional positions as today. Not a `CampaignTime` value.
2. More tests than the seven touch `LoadFrom`: also `:364`, `:1176` (save/load round trip) and the two probe tests
   above (11 calls in all). All must pass unchanged.
3. The whole `RefugeServiceTests` class is `[TestCategory("RequiresGame")]` (`:27`), so the RefAsm no-game replay
   (`.ai/verification.md:30`, `:44`) skips every one of these tests. The proof run must be the desktop's game-backed
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. New direct tests on the unit can avoid the tag only
   if they construct no engine type beyond `CampaignTime` defaults (the round-trip comment at `:1268-1270` explains why
   `CampaignTime` factories need a campaign); otherwise tag them too.

**Conditions for a behaviour-preserving change:**
- Keep the order inside the row repair: key canonicalization, unknown tier, non-finite target (only when `Building`),
  future start (only when `Building`, `daysSince(start) < 0.0`), then the two militia floors.
- Keep the five warning texts byte-identical (`:759-760`, `:765`, `:773-774`, `:782-783`, and the null-row line at `:658`
  if it moves); `LoadFrom_NullRows_ScrubbedWithOneWarning` asserts exactly one warning containing "2" (`:2069-2075`).
- Keep `FiniteFloatValidator.IsFinite` as the test (zero and negative targets left alone) and the floor value `0.1f`
  (two tests assert `0.1f`); share the constant rather than copy it, since `:878` uses it too.
- `LoadFrom` keeps the null-book fallback, the counter floor (`counter < 0 ? 0`), and the three cache resets
  (`_visualShown`, `_holdNoteShown`, `_nextFrameWorkHours`) in the service; it mutates the rows in place and keeps the
  same dictionary instance (`SaveInto` hands `_refuges` back by reference, `:668-672`).
- No `RefugeData` field changes, so no save effect. No Harmony or prefab involvement. No in-game check needed.

**Recalibrated:** impact LOW to MED (moves about 50 lines out of a 1,448-line file; the readability win is modest),
effort S, risk LOW with the lazy-seam condition, MED without it.

## ARCH-01: messenger arrival verdict into MessengerService

**Verdict: NARROWED.** The duplication and the ADR-002 placement are real, but the lane misdescribes the arrival rule:
prisoners are dropped as lost on arrival, not deferred, and the "deferred" branch is half unreachable. An executor
who follows the lane's description (or the code comment at `MessengerCampaignBehavior.cs:304-305`) changes behaviour.
The move is behaviour-preserving only if it encodes the rule as it executes, not as the comments describe it.

**Re-read:** `Main/Features/Messengers/MessengerCampaignBehavior.cs:205-240` (hourly loop), `:265-327`
(`ArrivedHandleOutcome`, `HandleArrivedMessenger`), `:348-358` (`NotifyMessengerLost`), `:613-632` (`ResolveHero`,
`IsTargetAvailableNow`, `IsPlayerAvailable`), `:634-680` (`GetReason`); `MessengerService.cs:26-68`
(`CanSendMessenger`); `MessengerTargetReader.cs:15-38`; `Domain/HeroSnapshot.cs:3-42`; the engine's
`TaleWorlds.CampaignSystem.Hero` (taom-src v1.5.7 cache, `Hero.cs:28-38` `CharacterStates`, `:303-319`
`IsDead/IsFugitive/IsPrisoner/IsActive/IsDisabled/IsAlive`, `:330` `IsWanderer`).

**Engine fact that settles the semantics:** `HeroState` is one enum (`NotSpawned, Active, Fugitive, Prisoner,
Released, Dead, Disabled, Traveling`); `IsActive => HeroState == Active`, `IsPrisoner => HeroState == Prisoner`,
`IsAlive => !IsDead`. So the arrival "lost" test at `:283-287` reduces to: **reachable on arrival iff `HeroState ==
Active`, or `IsWanderer && HeroState == NotSpawned`**; every other state is lost.

**Every semantic difference the classification must keep (arrival vs. send):**

| Case | Send (`CanSendMessenger`) | Arrival (`HandleArrivedMessenger`) |
|---|---|---|
| Hero not found | `NullTarget` | `ResolveHero` null: removed **silently**, no notice (`:274-276`); `ResolveHero` uses `Hero.FindFirst` so dead unregistered heroes are found (`:613-618`) |
| Human player character | refused | not checked |
| Dead | `HeroDead` | lost |
| Prisoner (any occupation) | `HeroPrisoner` | **lost** (state `Prisoner` is not `Active` and not `NotSpawned`); the `IsPrisoner` test in `IsTargetAvailableNow` (`:622`) is unreachable, and so is its `!IsAlive` test |
| Fugitive | `HeroFugitive` | lost |
| Child, otherwise reachable | `HeroChild` | **wait** (deferred every tick, `:306-307`, `:622`) |
| Non-wanderer not `Active` (Released, Traveling, Disabled, NotSpawned) | `TargetUnavailable` | lost |
| Wanderer, `NotSpawned` | passes | passes |
| Wanderer, `Released`, `Traveling` or `Disabled` | **passes** (`IsWanderer && !IsActive`, `:46-47`) | **lost** (`:287`) |
| In the player's party | `IsInPlayerParty`: false when either party is null (`MessengerTargetReader.cs:19-21`) | `target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo` (`:293`): **true when both are null** (a partyless target while the main hero has no party, or `Hero.MainHero` null); shows the "confused" inquiry and removes |
| Same settlement, same army | refused unless in person | not checked |
| Gold, already pending | checked | not checked |
| Player busy (no main party, a `PlayerEncounter`, not on the map or at a menu) | n/a | wait (`IsPlayerAvailable`, `:626-632`) |
| Target's party in a map event | n/a | wait (`:623`) |

Order on arrival: not found, then lost, then confused, then wait (player before target), then ready. Lost runs before
confused, so a dead hero in the player's party is "lost", not "confused".

**Source-text and other pins on these files** (`git grep -n -l MessengerCampaignBehavior -- TAOM.Tests tools .claude`,
then each read):
- `TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs`: `:60-62` the behavior must call
  `MessengerTargetReader.ReadSnapshot(` and must not contain `new HeroSnapshot(`; `:83` the reader holds exactly two
  `DoesLeaderPartyAndAttachedPartiesContain(`; `:102` the client mixin calls `behavior.GetReason(offer, hero)`, so
  `GetReason` must stay a public instance method on the behavior (it may delegate; moving it out without a delegate
  breaks the pin and `Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs`); `:112-114` the reader keeps its
  quote-input lines; `:131` the reader must not contain `inPerson`; plus the conversation, dispatch and gold pins
  (`:22-75`, `:90-94`, `:140`), untouched by this change.
- `TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs:191-206` constructs the behavior with six arguments and
  calls `OnHourlyTick` on a co-op client: the constructor signature and the authority gate before any settings read stay.
- `MessengerCampaignBehaviorTests.cs:34`, `:45` read `IoC.cs` and `SubModule.cs` (unaffected).
- `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` (`MessengerCampaignBehavior 668`): the gate fails a
  listed class that drops below its number (`EntryPointLineCountGateTests.cs:78`), so the row must be regenerated.
- `MessengerServiceTests.cs` builds `HeroSnapshot` 13 times positionally (`git grep -c 'new HeroSnapshot('`): new
  snapshot facts must be optional trailing constructor parameters, or the 13 sites change.

**Conditions for a behaviour-preserving change:**
1. Read the raw `HeroState` into the snapshot (a TAOM enum mapped one to one, all eight values) rather than adding
   `IsDisabled`/`IsSpawned` flags that re-derive it; the classification then tests "Active, or wanderer and NotSpawned".
2. Carry the arrival's raw party equality (null equals null) as its own fact, or keep that one comparison in the
   behavior and pass the bool; do not reuse `IsInPlayerParty`.
3. Keep prisoner as lost and child as wait. Do not "fix" the unreachable prisoner deferral in a refactor; that is a
   behaviour change (see the bug lead below).
4. Keep `ResolveHero`'s `Hero.FindFirst` scan and the silent removal on a null hero; keep `NotifyMessengerLost`'s and
   the confused inquiry's texts and keys byte-identical (`taom_messenger_recipient_gone`, `taom_messenger_lost_title`,
   `taom_messenger_confused`, `taom_messenger_error`).
5. Keep `_processingArrivedMessenger` set only on the ready path, so later arrived messengers in the same hourly pass are
   still skipped (`:218-223`).
6. Unit-test the classification over every `HeroState` value times wanderer or not, child or not, party match, target in
   a map event and player available: that table is the proof. The reader itself stays engine-bound and untested, so
   it must be a pure field read.

With those, no in-game check is needed: the decision moves, the engine reads do not change. Without condition 1 to 3,
it needs one.

**Bug lead (Plan candidate: no, behaviour change):** a messenger that arrives while its target is a prisoner is
dropped as "lost", although the comment at `:304-305` and the arrival design intend a wait ("all of which the player may
resolve"); and an inactive wanderer who is `Released` or `Traveling` can be sent a messenger that is then always lost on
arrival. For the orchestrator to raise; not for a refactor executor.

**Recalibrated:** impact MED (largest baseline entry, an untested rule), effort M, risk MED.

## ARCH-06: share the CultureStageView type probe

**Verdict: CONFIRMED.**

**Re-read:** `Main/Features/FactionMap/Hooks/CultureStageView_Constructor_Patch.cs:16-30`, `CultureStageView_Finalize_Patch.cs:16-30`,
`CultureStageView_Tick_Patch.cs:16-30`: the `PossibleTypeNames` array (the same two strings,
`SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView` and
`SandBox.View.CharacterCreation.CharacterCreationCultureStageView`, same order) and `FindCultureStageViewType()` (loop
over `AccessTools.TypeByName`, first non-null wins) are character-identical in all three. `Prepare()` is identical
(`:32`); `TargetMethod()` differs per patch (first constructor, `OnFinalize`, `Tick(float)`), and so do the hook field
types and the Prefix or Postfix.

**Ran:** `git grep -n -E 'CultureStageView|PossibleTypeNames|FindCultureStageViewType'` over `TAOM.Tests`, `tools`,
the API snapshot and the harmony registry; read each hit. Read `HarmonyPatchBindingTests.cs:43,119-160`,
`ServerJitGateTests.cs:14-35`, `Main/PatchCategoryIndex.cs:58`.

**Tests and bindings that touch these files:**
- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (`[TestCategory("BindingVerification")]`, `:43`, `:205`) finds patch
  types by a static parameterless `TargetMethod` (`:119`, `:126`) and invokes `Prepare` (`:137`) and `TargetMethod`
  (`:157`) **by name** on the patch type. Both must stay on each patch class with the same static parameterless
  signature. This suite runs in the binding gate, not the unit step, so the executor must run the binding-gate command
  from `.ai/verification.md:31-33` (or the game-backed equivalent) as part of the proof.
- `TAOM.Tests/Composition/ClientFrontEndCompositionTests.cs:31-32`, `:35-36`, `:64-66` read each patch's private static
  `_hook` field by reflection (`GetField("_hook", NonPublic | Static)`): the field name and its place stay.
- `docs/reference/taleworlds-api-snapshot/patch-targets.md:170-172` lists the three resolved targets (generated);
  unchanged because the targets are unchanged.
- No test reads these three files as source text (none of the hits is a `ReadSource` of them).
- `ServerJitGateTests` (`RequiresGame`) flags server-reachable TAOM.dll methods that reference a client assembly, and
  exempts `[ClientOnly]` classes (`:20-22`). The helper references only `System.Type` and `HarmonyLib.AccessTools`, and
  the view type names are strings, so it adds no offender with or without `[ClientOnly]`. `PatchCategoryIndex.cs:58`
  reads `[ClientOnly]` only on patch types, so the helper needs it only for consistency.

**Conditions for a behaviour-preserving change:** same two strings in the same order; same first-match loop over
`AccessTools.TypeByName`; no caching (today each `Prepare` and `TargetMethod` call re-probes; caching a `null` from an
early call would change behaviour if the assembly loads later, so keep it uncached); `Prepare`, `TargetMethod`,
`_hook`, the attributes `[HarmonyPatch]`, `[HarmonyPatchCategory("Patch7_FactionMap")]`, `[ClientOnly]` and the
Prefix/Postfix stay on each patch class. Build plus the unit suite plus the binding gate prove it; no in-game check.

**Recalibrated:** impact LOW (removes two copies of a 15-line probe; the future-rename hazard is real but rare), effort
S, risk LOW.

## ARCH-04: one single-charge tree for war ram, elk and Animalia

**Verdict: CONFIRMED** on the duplication (trees only), with three corrections; **behaviour-preserving only with an
in-game check**, because no test can build the tree.

**Re-read:** `Main/Features/Elk/ElkBehaviorTree.cs:29-66`, `Main/Features/WarRam/WarRamBehaviorTree.cs:33-71`,
`Main/Features/Animalia/AnimaliaBehaviorTree.cs:23-58`. Same four blackboard properties, same constructor
(`base(10)` and four `BTBlackboardValue` initializers), same node list: `main` selector, `has rider` selector with
`HasRiderDecorator`, `enemy in range` selector with `ElephantLikeEngageDecorator(profile)`, a sequence with
`ElephantLikeAttackOffCooldownDecorator(profile, Trample, <cooldown>)` holding `ElephantLikeTrampleTask(profile)` and a
300 ms sleep, a 200 ms idle sleep, then `no rider` with `HasNoRiderDecorator` and a 4 s sleep. Differences: the sequence
label (`"antler charge"` Elk `:55`, `"kick attack"` WarRam `:60`, `"antler attack"` Animalia `:47`), the cooldown
constant (`ElkConfig.cs:54`, `WarRamConfig.cs:52`, `AnimaliaConfig.cs:47`, all `35.0`), the profile source, and the
entry signature (`public static new BehaviorTree BuildTree(object[])` for Elk and WarRam against
`public static BehaviorTree? Build(object[], ElephantLikeCombatProfile)` for Animalia).
Registrations: `ElkMissionBehavior.cs:48` (`"ElkTree"`), `WarRamMissionBehavior.cs:54` (`"WarRamTree"`),
`AnimaliaMissionBehavior.cs:46-47` (`"AnimaliaElkTree"`, `"AnimaliaMooseTree"`, consts at `:24-25`), each through a
lambda, not by reflection on the tree type.

**Ran:** `git grep -n -E 'ElkBehaviorTree|WarRamBehaviorTree|AnimaliaBehaviorTree|"ElkTree"|"WarRamTree"'` over all
tracked files; `git grep -n '\bName\b' -- Main/BehaviorTrees Main/BehaviorTreeWrapper`; read `BehaviorTreesCore.cs:85-127`
and `ElkCombat.cs:10-31`; listed the creature test folders and grepped them for `ReadSource` and the tree names.

**Corrections to the lane:**
1. The tree type's name is not only in exception texts. `BehaviorTreeBuilder` names the root node after the tree type
   (`Main/BehaviorTrees/BehaviorTreesCore.cs:127`: `new Sequence(TreeBeingBuild, typeof(TTree).Name)`), and node names
   reach two "should never happen" log lines (`Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs:209`, `:294`). A shared
   tree class renames the root node. No test, tool or run judge reads node names (`git grep BehaviorTree -- 'tools/*.py'`:
   no hit), so this is a log-text change on an unreachable path; name it in the plan. The sequence label must be a
   parameter to keep `"antler charge"` and `"kick attack"`.
2. "No unit test can build the tree" holds, for a reason the lane did not give: each profile resolves its four attack
   action caches eagerly at first touch (`ElkCombat.cs:10-11`, `:18-29`; same shape in `WarRamCombat.cs:14`), so any
   test that builds a tree touches the engine's action index cache. The tests the lane names (`ElkConfigTests`,
   `WarRamConfigTests`, `AnimaliaWiringTests`) and `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs:49-51`
   (mission behavior types only) do not exercise the tree shape.
3. Docs name the files: `docs/features/elk.md:57`, `:102`, `:220`; `docs/features/war-ram.md:102`, `:265`;
   `docs/features/animalia-elk-moose.md:287`. Moving or deleting the classes needs those rows corrected.

**Conditions for a behaviour-preserving change:** one builder taking `(objects, profile, cooldownSeconds, sequenceLabel)`;
each caller passes its own config constant (do not collapse to one `35.0`: they are three tuning knobs); identical node
order, decorator arguments and sleep spans (`new(0,0,0,0,300)`, `new(0,0,0,0,200)`, `new(0,0,4)`); `base(10)`; the four
blackboard properties with the same names (the builder injects blackboard values by interface and property,
`BehaviorTreesCore.cs:108-117`); registration names `"ElkTree"`, `"WarRamTree"`, `"AnimaliaElkTree"`,
`"AnimaliaMooseTree"` unchanged; `CreatureTreeTracker` arguments and the `[Elk]`/`[WarRam]` log prefixes unchanged. The
build and the existing suite then prove only that it compiles and that config and wiring tests still pass.
**Condition: an in-game ride of a war ram and a great elk (and an Animalia elk or moose if its class moves), showing the
charge firing on an enemy in front and idling on cooldown.** The optional mission behavior fold needs the same check and
more.

**Recalibrated:** impact LOW (two 70-line classes, comment drift), effort S for the trees, risk LOW in code, but the
proof costs a game session; rank it below the changes the suite can prove.

## ARCH-05: one supply row VM

**Verdict: CONFIRMED**, and the lane's open question is answerable from engine source: no in-game check is required.

**Re-read:** `diff Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs Main/Features/SupplyLines/UI/SupplyTroopRowVM.cs`:
differences are the doc comment (`:8-10`), class and constructor names (`:12`, `:18`), the label literal (`:25`) and its
count variable name (`:27`, `STOCK` against `COUNT`). Users: `SupplyOrderScreenVM.cs:31-32`, `:256`, `:259`, `:660`,
`:664`, `:753`. Prefab `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml:225-269` (`{Goods}`) and
`:309-353` (`{Troops}`) bind the same names in each item template: `@Label` (`:234`, `:318`), `@QtyText` (`:254`,
`:338`), `ExecuteMinus` (`:243`, `:327`), `ExecutePlus` (`:258`, `:342`).

**Engine evidence (taom-src v1.5.7):** `TaleWorlds.Library.ViewModel.cs:239-262` caches properties per runtime
`GetType()` and resolves them by name (`:451-460`); commands resolve by name on the runtime type (`:485-497`,
`type.GetMethod(commandName, ...)`); `TaleWorlds.GauntletUI.Data.GauntletView.cs:166-168`, `:391` bind a list through the
non-generic `IMBBindingList`, so `MBBindingList<SupplyRowVM>` binds as `MBBindingList<SupplyGoodRowVM>` did. An item
template never names a VM type.

**Ran:** the diff above; `git grep -n -E 'SupplyGoodRowVM|SupplyTroopRowVM'`; `git grep -n -E 'taom_sl_(good|troop)_row'`;
read `SupplyOrderPrefabBindingTests.cs:21-41`, `:175-200`; `taom-src path ViewModel` and `path GauntletView`.

**Corrections to the lane:** `SupplyOrderPrefabBindingTests` is `[TestCategory("RequiresGame")]` (`:21`), so the RefAsm
no-game replay skips it; the proof must be the desktop's game-backed run. Its two type maps (`:37-40`, `:184-189`) must
list the one type; with one type for both collections the "every DataSourceProperty is bound" check (`:177-199`) still
passes because both templates bind `Label` and `QtyText`.

**Conditions for a behaviour-preserving change:** the label is still built in the constructor from the clamped
`Available` (`Math.Max(0, item?.Available ?? 0)`) and the `NAME` fallback to `ItemId`; the two literals stay
byte-identical, keys `taom_sl_good_row` and `taom_sl_troop_row` with variables `STOCK` and `COUNT` (registered in
`Main/_Module/ModuleData/taom_module_strings.xml:2812`, `:2830`, cached per language under `tools/translation_cache/`);
pass the literal and the count variable name (or a `Func<int, TextObject>`), not a prebuilt `TextObject`, which would make
the caller repeat the clamp; public members `ItemId`, `Available`, `UnitPrice`, `Qty`, `QtyText`, `Label`, `ExecutePlus`,
`ExecuteMinus`, `ResetQty` and the `OnPropertyChanged(nameof(QtyText))` then `_onChanged` order unchanged;
`[DataSourceProperty]` on the same two properties. Proof: build, `SupplyOrderScreenVMTests`, the prefab binding test on
the game-backed run, and a new test that each label form renders its own key. No in-game check needed.

**Recalibrated:** impact LOW (one 79-line twin), effort S, risk LOW.

## Summary of verdicts

| Id | Verdict | Behaviour-preserving | Main correction |
|---|---|---|---|
| ARCH-01 | NARROWED | only with conditions | arrival drops prisoners as lost (the prisoner wait is unreachable); inactive wanderers other than NotSpawned are lost; party equality is true for null and null; `GetReason` stays on the behavior (client mixin pin) |
| ARCH-02 | CONFIRMED | yes | a fourth copy of the frequency map in `LotrIssuesCampaignBehavior.cs:144-152`; `EnsureDef` cannot usefully move; no existing test covers the moved logic |
| ARCH-03 | NARROWED | only with conditions | seams must be lazy delegates; an eager `NowTime()` breaks the two `HeroMergeProbeService` tests; whole test class is RequiresGame |
| ARCH-04 | CONFIRMED | only with conditions (in-game ride) | the root node is named after the tree type; profiles resolve engine action caches, so nothing can build the tree in a test |
| ARCH-05 | CONFIRMED | yes | engine binds by property name and through `IMBBindingList` (v1.5.7 source), so no in-game check; prefab test is RequiresGame |
| ARCH-06 | CONFIRMED | yes | proof must include the binding gate (`HarmonyPatchBindingTests`, BindingVerification), which invokes `Prepare`/`TargetMethod` by name |
| ARCH-07 | CONFIRMED | yes | deleting from `AutonomousMovementPlayerController` trips the shrink-only line gate unless its row is regenerated; `IsExpanded` dies too; three doc and comment fixes |

Two of the seven touch an entry point in the shrink-only baseline (ARCH-01 `MessengerCampaignBehavior 668`, ARCH-07
`AutonomousMovementPlayerController 665`): `EntryPointLineCountGateTests.cs:78` fails a listed class that ends below its
number, so each executor regenerates its row with `TAOM_WRITE_ENTRY_POINT_BASELINE=1`.

Three of the seven are proven partly by tests the RefAsm unit step leaves out (ARCH-03 `RefugeServiceTests` and
ARCH-05 `SupplyOrderPrefabBindingTests` and `SupplyOrderScreenVMTests`, all `RequiresGame`; ARCH-06
`HarmonyPatchBindingTests`, `BindingVerification`, plus the `RequiresGame` server JIT gate): the RefAsm no-game unit
step alone is not enough evidence for them; the desktop's game-backed `dotnet test` (and, for ARCH-06, the binding
gate) is.

## What I did not cover

- I did not run `dotnet build` or `dotnet test` (task rule). Every "the suite passes unchanged" statement above is a
  condition for the executor to prove, not a result.
- ARCH-07: I re-searched only the eight members the lane lists, not the lane's whole 18-hit scan or dead members whose
  names are shared with other members. Live-module search covered `TAOM_Map` and `LOTRLOME_Armory` XML and XSLT and the
  DLLs under the installed `Modules/` folder; it did not cover other machines, the Steam Workshop folder, or mods not
  installed on this desktop.
- ARCH-01: I derived the arrival semantics from the engine's `Hero` state properties (v1.5.7 decompile); I did not
  check how often each state occurs in play, and I did not trace `StartMessengerConversation` beyond its null check.
- ARCH-02: I did not check whether `IssueBase.PreconditionFlags` or `IssueBase.IssueFrequency` load without the game
  under the RefAsm stubs; the executor's RefAsm unit step answers that.
- ARCH-04: I did not read the decorator and task constructors to see whether a tree could be built with a fake profile;
  the profile type is sealed and resolves action caches, so I judged it not unit-buildable.
- ARCH-05: the September verifier's line numbers for the prefab were re-read; the Gauntlet list binding was checked in
  `GauntletView.cs` only, not in every list widget type.
- ARCH-08, ARCH-09, ARCH-10 were not in my assignment and were not checked.
- No GitHub issue search (no `gh` calls).
