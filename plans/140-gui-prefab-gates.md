# Plan 140: Gate TAOM's Gauntlet prefabs against the installed v1.4.8 game and drop the stale and identical copies

> **Executor instructions**: Follow this plan step by step, in your own worktree (see "Git workflow").
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has four Parts, each a clean
> hand-off point with its own done criteria: Part A (element, XPath, insert-position and hint gates),
> Part B (copy-drift gates, the troop manager popup re-base, eleven identical copies deleted), Part C
> (brush, attribute-name and stretch-in-cover gates, dead prefab settings and files, the `data_gates.py` row), Part D (the release
> packager refuses TAOM files the release commit does not track). Do not edit `plans/README.md`: the
> integrator owns it during this programme. Put every count you measured in your hand-off (Step Z).
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/_Module/GUI TAOM.Tests/Migration TAOM.Tests/Infrastructure TAOM.Tests/Features/Execution Main/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs .claude/rules/gui-ui.md .claude/rules/moduledata-validation.md .claude/rules/vanilla-data-comparison.md .claude/rules/tests.md .ai/review-reference.md docs/reference/engine/mission-and-missionbehavior-lifecycle.md docs/features/career-system.md docs/features/equip-presets.md docs/reference/release-process.md tools/package_release.py tools/tests/test_package_release.py tools/data_gates.py tools/tests/test_data_gates.py`
> Earlier WPs of this programme are expected to have changed several of these: WP104 adds
> `TAOM.Tests/Infrastructure/RepoPaths.cs`, WP115 rewrites the packager and its tests, WP122 changes the
> binding tests, WP129 edits `CareerScreen.xml`, WP130 edits `BattleActionBar.xml`, WP131 creates
> `PrefabExtensionBrushTests.cs` and adds gate lines to two rules, WP136 creates `tools/data_gates.py` and its
> test, refactors `PrefabExtensionBrushTests.cs` and adds a section to `moduledata-validation.md`. Compare the live files with
> "Current state" and STOP only on a real mismatch (the list is in Step 0.2).

## Status

- **Priority**: P2
- **Effort**: L (four Parts, about twelve commits; Parts A and B fit one session, Parts C and D another)
- **Risk**: LOW for the gates (tests and one behaviour-neutral C# override). MED for two player-visible
  changes: the party screen's troop upgrade and recruit pop-up is re-based on the v1.4.8 file (header
  layout and key hints change, the used-horses tooltip comes back), and nine brush names are repointed
  (career picker text, custom battle army slider, battle action bar labels, inventory presets label).
  The 19 file deletions are behaviour-neutral: seven are loaded by nothing, eleven are identical to the
  vanilla file they shadow as the engine reads it (proved in Step B1), and `AgentStatus.xml` is vanilla
  plus one hidden block (see "Current state").
- **Depends on**: WP131 (`plans/131-campaign-fixes-messengers-recruitment.md`; it creates
  `TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`, which Part C extends) and WP136
  (`plans/136-data-integrity-gates.md`, same wave, lane A; it creates `tools/data_gates.py`, whose `gui-tests`
  row Step C6 extends, and refactors `PrefabExtensionBrushTests.cs`). Transitively, and
  merged in earlier waves: WP104 (`TAOM.Tests/Infrastructure/RepoPaths.cs`, used by Parts B and C) and
  WP115 (`tools/package_release.py` `require_build`, used by Part D only). Step 0.3 checks each one.
- **Category**: tests
- **Wave / lane**: wave 5, lane E
- **Executor**: laptop
- **Decisions applied**: D1 (no plan stages `CHANGELOG.md`; the commit body is the entry), D2 (no feature
  module; nothing touches `Main/IoC.cs` or `Main/SubModule.cs`), D6 (`f48ef74dc` instant transitions not
  ported; only its test hunk), D7 (1.4.x has no `TAOM.Client`: every gate scans the TAOM assembly), D8
  (subjects carry the `Main/_Module/SubModule.xml` version, `v2.0.28` at planning), D9 (no CI; the plan
  010 test categories `RequiresGameIL` and `LiveInstall` are removed from ported tests), D10, D19 (git only
  through the Bash tool), D20 (no ModuleData is touched, so the laptop Armory state does not matter).
  Orchestrator notes for this WP (binding): K1 (port `42148af4c`'s hint-parent gate on the TAOM assembly
  with a floor of 5, STOP if fewer), K2 (port `540848795`'s tuple parity test, STOP if red on a clean
  tree), `62fb6bb1d` needs WP131's brush test, the copy-drift group needs `450332b74`, and only
  `8d5cfc623`'s test part.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved (`ca1bc5f2e` at review); four newer commits touch two of
  the ported files (`498d07e08`, `ed3f911eb`, `0385cc041` in `PrefabAttributeNameBindingTests.cs`,
  `95a348a60` in `GauntletStretchInCoverRatchetTests.cs`). They are unclassified and NOT ported here;
  every 1.5.x SHA below is a commit, so the blobs this plan copies are stable.
- **Issue**: port of #382 (`720e421fa`), #858 (`69b47d00b`), #910 (gate only, `42148af4c`), #1005
  (`62fb6bb1d`), #1022 (`103636439`), #1024 (`450332b74`), #1027 (`abb718c2f`), #1031 (parity test only,
  `540848795`), #1051 (`fc932d4ca`, `7012783b3`), #1078 (`53a4e4e52`), #850 (test hunk only, `f48ef74dc`),
  #388 and #866 (`d4fb51a50`). No new issue: the three extra deletions (`TroopTypeSelectionPopUp.xml` and
  the two party tuple rows) are #1051's rule applied to 1.4.x data. Deep-dive finding owned: XML-DATA-08
  (the insert-position gate with a one-assembly list, Step A2, and `53a4e4e52`, Step C4).

## Why this matters

TAOM ships 33 prefab files that replace a vanilla file of the same name (counted this session on
`origin/bannerlord-1.4.5` against the installed v1.4.8 Native, SandBoxCore, SandBox, StoryMode,
CustomBattle, Multiplayer and BirthAndDeath prefab trees). Eleven of them are identical to the vanilla
file as the engine reads it, so they do nothing except hide whatever a future engine patch changes there.
One is already stale on v1.4.8: the party screen's troop manager pop-up lacks the
`TertiaryInputKeyVisualParent` the v1.4.8 file declares, so `PartyManageTroopPopupWidget.OnLateUpdate`
returns early (v1.4.8 dump line 81) and the gamepad key hints never show, and it lacks the
`DataSource="{UsedHorsesHint}"` binding, so hovering the horse count shows nothing. Nine brush names in
TAOM screens are declared by no module TAOM loads (the widgets draw with the default brush), two dead
files carry a `Brush.Fontsize` typo, about fifteen attributes are ones the engine never applies (for
example `VerticalAlinment`, `AcceptEvents`, `EaseIn`), seven prefab files are loaded by nothing, and the
`AgentStatus.xml` copy is vanilla plus a hidden (`IsVisible="false"`) block naming a widget class that
exists nowhere.

None of this is caught offline on 1.4.x: the XML is well formed, the C# compiler never sees it, and the
line has no CI (`ccef172ad` removed it). The 1.5.x line built engine-agnostic gates for exactly these
classes of mistake. This plan ports them, runs them against the installed v1.4.8 game on the laptop,
and fixes what they find on day one. It has no link to the save, freeze or memory symptoms; it is
prevention plus two small UI repairs, and the shadowing hazard bites only if TaleWorlds ships another
1.4.x patch.

## Current state

### Files and roles (`origin/bannerlord-1.4.5` unless marked)

- `TAOM.Tests/Migration/GameAssemblies.cs` (132 lines): loads the game module DLLs for binding tests.
  `public static string GameDir` `:28`, `Diagnostics` `:30`, `EnsureLoaded()` `:36` (returns false when
  `BANNERLORD_OVERRIDE_DIR` / `BANNERLORD_GAME_DIR` do not resolve). It does NOT load `TAOM.dll`; a test
  that needs TAOM's own widget classes must touch `typeof(TAOM.IoC).Assembly` first (this plan adds that
  line where the 1.5.x tests touch `TAOM.Client`). `TAOM.IoC` is `public static class IoC` in namespace
  `TAOM` (`Main/IoC.cs:73-75`).
- `TAOM.Tests/Migration/` holds `GameAssemblies.cs`, `GameModelOverrideBindingTests.cs`,
  `HarmonyFieldInjectionNamingTests.cs`, `HarmonyPatchBindingTests.cs`, `IlCallScanner.cs`,
  `ReflectionSiteBindingTests.cs`. None of the eight test files this plan creates exists.
- `Directory.Build.props:5-6`: `<LangVersion>10.0</LangVersion>`, `<Nullable>enable</Nullable>`, so the
  1.5.x syntax (`new(...)`, `string?`, `!`) compiles. `Main/TAOM.csproj:117-122` grants
  `InternalsVisibleTo` to TAOM.Tests (the internal prefab classes are reachable). `TAOM.Tests.csproj` is
  SDK-style with no explicit `Compile` items (new `.cs` files are picked up) and is identical to the 1.5.x
  file at `71484b8e4` (checked: `git diff a4c6d7c30 71484b8e4` and `a4c6d7c30 origin/bannerlord-1.4.5` over
  it are empty), so `using Bannerlord.UIExtenderEx.Attributes;` compiles as it did there.
- In-code PrefabExtensions (the TAOM assembly, all `internal`, all with implicit parameterless ctors):
  - `Main/Features/CareerSystem/UI/CareerButtonPrefab.cs`: `CareerButtonPrefab`, `[PrefabExtension("CharacterDeveloper", "descendant::Widget[@Id='TopPanelParent']")]`, `InsertType.Append`.
  - `Main/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs:24-30`:
    ```
    24 [PrefabExtension("AgentStatus",
    25     "descendant::Widget[@VisualDefinition='AgentStatus']/Children")]
    26 internal class CareerEnergyBarPrefab : PrefabExtensionInsertPatch
    27 {
    28     public override InsertType Type => InsertType.Child;
    29
    30     [PrefabExtensionXmlDocument]
    ```
    A Child insert with no `Index` override: it lands first, which is where it has always been.
  - `Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs`: `[PrefabExtension("EncyclopediaHeroPage", "descendant::RichTextWidget[@Text='@InformationText']")]`, Append. WP131 (`6647f8f57`, `2c7f7fae9`) moves its `HintWidget` under a plain wrapper `Widget` and repoints its two brushes.
  - `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs`: class `SpecialResourceIconPrefab`, MapBar, `InsertType.Replace`.
  - `Main/Features/TimeAcceleration/UI/TimeAccelerationPrefab.cs`: four `PrefabExtensionSetAttributePatch` classes and one insert, `PrefabInsertExtraFastForward` (MapBar, Append on `ButtonWidget[@Id='PauseButton']`).
  So there are 9 `[PrefabExtension]` attributes and exactly 5 classes with a `[PrefabExtensionXmlDocument]` method (the K1 floor).
- `Main/_Module/GUI/` holds 65 `*.xml` files, all well formed (parsed this session). `Main/_Module/GUI/PreFabs/`
  holds the prefabs (git spells the folder `PreFabs`; a checkout may hold `Prefabs` on disk).
- `Main/_Module/GUI/PreFabs/Mission/AgentStatus.xml`: the installed v1.4.8 `Native/GUI/Prefabs/Mission/AgentStatus.xml`
  plus one dead block, lines 109-189 (`diff -w --strip-trailing-cr` shows only `109,189d108`); the block names
  `<CareerEnergyBarWidget>`, a class that exists nowhere. `CareerSystem/AbilityHUD.xml` is an empty
  override. Neither is referenced by any `.cs`, test or tool (only comments mention them).
- `Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml` (217 lines): `:14`
  root `PartyManageTroopPopupWidget` without `IsTertiaryActionAvailable` / `TertiaryInputKeyVisualParent`;
  `:21` a single centred Done button (TAOM's edit: no `PositionXOffset`, no Cancel button); `:150`
  `HorseLabelContainer` without `DataSource="{UsedHorsesHint}"`; `:195-200` TAOM's top-right close
  `ButtonWidget` calling `ExecuteCancel` (TAOM's edit). Its history on this line is `c31570ff5` (created)
  and `ad836d114` (direction flip); everything else that differs from v1.4.8 is old vanilla, not a TAOM edit.
- Shared rules and docs: `.claude/rules/gui-ui.md` (brush paragraph `:24-31`, the "Why" line `:63` before
  "Safe pattern"; no "Hover targets" paragraph on 1.4.x), `.claude/rules/moduledata-validation.md` (165
  lines; only the validator-code and module-scan tables, no GUI prefab row; `## Discipline` at `:135`),
  `.claude/rules/vanilla-data-comparison.md` (heading `:51`, clone paragraph `:53`, `**How to fix a stale clone:**`
  `:68`, steps 1-2 at `:69-70`), `.claude/rules/tests.md` (98
  lines; `## Test Organization` at `:97`; no gate-state paragraph), `.ai/review-reference.md:161` (the
  "GameModel constructors receiving services via `IoC.Resolve<>()`" exception line),
  `docs/reference/engine/mission-and-missionbehavior-lifecycle.md` (`### Lifecycle order (typical)` at
  `:55`, its code block ends `:63`, `## ⚠️ The ... gotcha` at `:65`).

### What the gates find on the 1.4.x data (replicated this session, read-only; the real runs are authoritative)

| Gate (1.5.x source) | Replica result on `origin/bannerlord-1.4.5` against installed v1.4.8 |
|---|---|
| `PrefabElementTypeBindingTests` (`71484b8e4`) | one offender: `Mission\AgentStatus.xml: <CareerEnergyBarWidget>` |
| `PrefabExtensionBindingTests` (`f48ef74dc` blob) | all 8 distinct XPaths match exactly once in the winning prefab, before and after this plan's deletions; all 4 movie names match a file name in exact case |
| `PrefabInsertPositionTests` (`69b47d00b`) | one problem: `CareerEnergyBarPrefab` (Child, no Index) |
| `PrefabHintParentGateTests` (`42148af4c`) | 5 documents; no `HintWidget` under a `DoNotAcceptEvents="true"` parent |
| `PrefabCloneWidgetReferenceTests` widget refs + `DataSource` (`7012783b3` blob) | two gaps, both in the popup: root lacks `TertiaryInputKeyVisualParent`, `HorseLabelContainer` lacks `DataSource="{UsedHorsesHint}"` |
| identical-copy test (`fc932d4ca`) | 11 copies: `CustomBattle/CompositionSlider.xml`, `CustomBattle/SimpleDropdown.xml`, `CustomBattle/TroopTypeSelectionPopUp.xml` (SandBoxCore); `Nameplate/PartyNameplate.xml`, `Nameplate/SettlementNameplate.xml`, `Party/PartyComposition.xml`, `Party/PartySortController.xml`, `Party/PartyTroopManagerPopUp/PartyTroopRecruitItem.xml`, `Party/PartyTroopManagerPopUp/PartyTroopUpgradeItem.xml`, `Party/PartyTroopTuple.xml`, `Party/PartyTroopTupleLeft.xml` (SandBox). `TroopTypeSelectionPopUp.xml` is also `diff -w` identical; 1.5.x edited it in `450332b74` before `fc932d4ca` ran, which is why 1.5.x did not delete it |
| tuple parity (`540848795`) | both copies equal vanilla element for element (188 and 114 elements) |
| `EveryGuiPrefabBrush_IsDeclaredByAModule` (`62fb6bb1d`) | 9 entries: `ButtonBrush1.Text` (PresetsOverlay), `CharacterDeveloper.DescriptionText` and `.SkillNameText` (CareerScreen), `Clan.TextInput`, `Header.Frame`, `SPCharacterDeveloper.Statistics.Fill`, `TextBox.Background` (ArmyComposition), `Popup.Text.Small` (BattleActionBar), `UI.SummaryScreen.Text` (EventXml) |
| `EveryGuiPrefabBrushProperty_NamesABrushProperty` (`103636439`) | `Brush.Fontsize` in `EventXml.xml` and `PreviewPanel.xml` only; the 4 `Brush.*` literals in PrefabExtension sources are valid |
| `PrefabAttributeNameBindingTests` (`7012783b3` blob) | problems: `EaseIn` on 3 `VisualDefinition`s in `CustomBattleScreen.xml`, 1 in `PreBuildCharacterSelection.xml`. Bad uses: the `450332b74` set (`Widget.VerticalAlinment`, `ButtonWidget.AcceptEvents` x3, `BrushWidget.StackLayout.LayoutMethod`, `ListPanel.StackLayout.LayoutImp` x3, `AlwaysRefreshAll` on the popup list) plus 16 vanilla-parity pairs (listed in Step C3) |
| `GauntletStretchInCoverRatchetTests` (`53a4e4e52`) | 65 files scanned; offenders `MomentumView/MomentumView.xml` 5, `CareerSystem/CareerScreen.xml` 3, `BattleActionBar.xml` 1, `FacGen/PreBuildCharacterSelection.xml` 1 |

The 19 files this plan deletes are referenced only by comments and by prefab tags in other TAOM
prefabs, which resolve by base name to the identical vanilla files once the copy is gone (checked with
`git grep -w` over `Main`, `TAOM.Tests` and `tools`). No other WP plan or pack edits any of them.

### Engine and library facts (verified this session; do not re-derive)

v1.4.8 dump = `C:\Decompiled_Bannerlord\_categories_v1.4.8`; taom-src = `C:\Users\mikew\.taom-src\v1.4.8`.

- Gauntlet prefab resolution (taom-src `TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory.cs`):
  `GetPrefabNamesAndPathsFromCurrentPath` keys on `Path.GetFileNameWithoutExtension(text)` in a default
  (case-sensitive) `Dictionary<string, string>` (`:80-85`), and on a duplicate name it raises a
  `FailedAssert` and then overwrites (`:92-93`), so the later module's file wins. `CreateBuiltinWidget`
  `FailedAssert`s on an unknown type (`:148`).
- Attribute application: `WidgetExtensions.GetObjectAndProperty` looks the name up with
  `GetProperty(name, BindingFlags.Instance | BindingFlags.Public)` (taom-src `:14-17`), exact case.
- `WidgetPrefab.LoadVisualDefinitions` (taom-src `:30-77`) reads only `Name`, `TransitionDuration`,
  `EaseType`, `EaseFunction`, `DelayOnBegin`; `EaseType` and `EaseFunction` go through `Enum.TryParse`
  without `ignoreCase` (`:43`, `:48`). `EaseIn` is never read. `AnimationInterpolation.Type` is
  `{ Linear, EaseIn, EaseOut, EaseInOut }` and `Function` includes `Sine`, `Quad`, `Quint` (`:9-23`).
- `VisualState.PositionXOffset` has a public setter (`:40`); `GotPositionXOffset { get; private set; }` (`:146`).
- `Widget.LayoutImp { get; protected set; }` (taom-src `Widget.cs:1184`); `GridWidget.GridLayout { get; private set; }`
  (`:25`) with `base.LayoutImp = GridLayout;` (`:141`); `ListPanel.StackLayout { get; private set; }` (`:13`)
  with `base.LayoutImp = StackLayout;` (`:40`); no other dump widget assigns a GridLayout or StackLayout.
  `GridLayout` has settable `VerticalLayoutMethod` and `HorizontalLayoutMethod` (`:12`, `:14`); `StackLayout`
  has `LayoutMethod` (`:25`) and no `VerticalLayoutMethod`.
- `TaleWorlds.GauntletUI.Brush` public properties include `FontSize` (`:53`), `TextHorizontalAlignment`
  (`:66`), `FontColor` (`:192`); there is no `Fontsize`.
- `ScrollablePanel.MouseScrollAxis` is a public FIELD (dump `ScrollablePanel.cs:115`), so it is a bad use
  vanilla also carries. `PartyUpgradeButtonWidget.MarinerTroopBrush` is a `BrushWidget` property (dump `:58`).
- `HintWidget.OnConnectedToRoot` subscribes `base.ParentWidget.EventFire` (dump `HintWidget.cs:15`) and
  relays `HoverBegin`/`HoverEnd` (`:31`, `:34`); `EventManager` hit-tests only `!widget.DoNotAcceptEvents`
  (dump `EventManager.cs:861`, `:897`).
- `PartyManageTroopPopupWidget.OnLateUpdate` (dump `:73`) returns at `:81` when `hoveredWidget`,
  `PrimaryInputKeyVisualParent`, `SecondaryInputKeyVisualParent` or `TertiaryInputKeyVisualParent` is null.
- `DefaultLayout.MeasureChildren` (taom-src `:26-59`) measures every visible child with the parent's own
  `measureSpec` and keeps the largest, so a `StretchToParent` child grows a `CoverChildren` parent to the
  space offered; `GridLayout.UpdateCellSizes` (`:138`) is the grid exemption.
- `UIResourceManager` adds each module's `/GUI/` location in `ModuleHelper.GetModules()` order (taom-src `:117-122`).
- Brush locations on v1.4.8: `CustomBattle.Slider.*` and `CustomBattle.SliderValue.*` in
  `SandBoxCore/GUI/Brushes/CustomBattle.xml`; `Tooltip.Text` in `Native/GUI/Brushes/Tooltip.xml`;
  `Popup.Button.Text` in `Native/GUI/Brushes/Popup.xml`; `CareerSystem.Effects.Title` / `.Passive` in
  TAOM's `Main/_Module/GUI/Brushes/CareerSystem.xml`. TAOM's DependedModules are Native, SandBoxCore,
  Sandbox, CustomBattle, TAOM.Dependencies (`Main/_Module/SubModule.xml:11-23`); the Armory and TAOM_Map
  are not among them and ship no `GUI/Brushes`, so the brush gate reads the same set on desktop and laptop.
- UIExtenderEx 2.13.2 (the package `Main/TAOM.csproj:111` compiles against; `ilspycmd` on
  `~/.nuget/packages/bannerlord.uiextenderex/2.13.2/lib/netstandard2.0/Bannerlord.UIExtenderEx.dll`):
  `PrefabExtensionInsertPatch.Index` is `public virtual int Index { get; }` (default 0); `InsertType.Child`
  calls `InsertAsChild`, which does `InsertBefore(importedNode, ChildNodes[Math.Max(0, index)])`;
  `Append`/`Prepend` insert a sibling (`node.ParentNode.InsertAfter/InsertBefore`); the XPath is applied with
  `SelectSingleNode`. `PrefabComponent.MoviePatches` is a default-comparer `ConcurrentDictionary` and
  `ProcessMovieIfNeeded` returns silently when the movie name is not a key (`:37`, `:172`).
  `ViewModelComponent` builds a mixin with `Activator.CreateInstance(mixinType, instance)` (`:135`).
- Mission start (dump `Mission.cs`, `MissionState.cs`): `public List<MissionBehavior> MissionBehaviors`
  (`Mission.cs:1329`); `AfterStart` (`:3799`) runs `EarlyStart` and then `AfterStart` with `foreach` over
  `MissionBehaviors` (`:3817-3826`) and sets `CurrentState = State.Continuing` on its last line (`:3835`);
  `AddMissionBehavior` appends (`:4615-4617`); `OnAgentCreated` (`:4061`) and `OnAgentBuild` (`:4344`,
  `:4358`) are `foreach` loops; `OnMissionTick` is a reverse index loop (`:3741-3744`).
  `MissionState.OnTick` calls `TickLoading` while the state is NewlyCreated or Initializing (`:89-96`);
  `TickLoading` calls `LoadMission`, which runs `CurrentMission.Initialize()` (`:221-244`);
  `FinishMissionLoading` clears `_missionInitializing` (`:335`) before `CurrentMission.AfterStart()` (`:345`).

### Installed vanilla files this plan reads (fingerprints taken this session)

- `<game>/Modules/SandBox/GUI/Prefabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml`: 18376 bytes, CRLF,
  SHA-256 `fdb8a3a6ed66c2627514dfd11273f3fadab333cf5f7315880cd7c003c9bb2130`.
- `<game>/Modules/Native/GUI/Prefabs/Mission/AgentStatus.xml`: 29639 bytes, SHA-256
  `91c657b7a4fc83e22978c8032e90a759dfa3a592cb1705616452baef4cf2a274`.
- `<game>/bin/Win64_Shipping_Client/Version.xml` names `v1.4.8`. `<game>` is `$BANNERLORD_GAME_DIR`.

### Conventions that bind this work

- TDD: every gate is run RED before its fix and GREEN after; the RED output must match this plan.
- ADR-007 adapters (services never touch sealed TaleWorlds types): not engaged, no service code changes.
- ADR-002 thin entry points (under 150 lines), ADR-003 no `#region`, ADR-004 no `[Obsolete]`, ADR-005 no
  `#if DEBUG` outside IoC registration: the ported files comply; keep them so.
- MCM json2 rule (rename a persisted setting, never flip its default): no setting is touched.
- `.claude/rules/harmony-patches.md` (MovementOrder postfixes through the shared deferred category): not engaged.
- Single-owner files `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`:
  NOT edited by this plan.

## Commands

Run everything through the Bash tool, prefixed with `cd /c/Users/mikew/source/repos/taom-port/140 &&`.
Python is `C:\Python314\python.exe` (`python` in the commands below).

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (warnings allowed) |
| Test (full) | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<A>\|FullyQualifiedName~<B>"` | as the step says |
| Plain binding gate | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification"` | `Failed: 0` |
| Strict binding gate (only if `TAOM.Tests/binding-gate.runsettings` exists, from WP122) | the plain gate plus `--settings TAOM.Tests/binding-gate.runsettings` | no new failure or skip vs Step 0 |
| Packager tests | `python -m unittest tools.tests.test_package_release` | `OK` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure Step 0 did not have |
| Docs | `python tools/lint_docs.py` | no new finding in a touched file |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` does not gate the copy
target on `DisableModuleCopy`). Never `./build.ps1`. Never two builds in one worktree. In a filter, the
pipe that separates clauses is written `|` inside the double quotes (the table shows it escaped only
because of Markdown). Git Bash rewrites `git show <ref>:.<dir>/...` arguments; this plan never shows a
dot-folder path, but if any `git show <ref>:<path>` reports a missing path, run `export MSYS_NO_PATHCONV=1`
and retry before treating it as a STOP.

## Scope

**In scope (the only files you modify):**
- New tests: `TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs`, `TAOM.Tests/Migration/PrefabExtensionBindingTests.cs`,
  `TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs`, `TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs`,
  `TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs`, `TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs`,
  `TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs`, `TAOM.Tests/Features/Execution/PartyTupleVanillaParityTests.cs`.
- Extended test: `TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` (created by WP131).
- C#: `Main/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs` (one override plus a comment).
- Prefabs edited: `Main/_Module/GUI/PreFabs/{BattleActionBar.xml, PresetsOverlay.xml, OOBButtonsOverlay.xml,
  CareerSystem/CareerScreen.xml, CustomBattle/ArmyComposition.xml, CustomBattle/CustomBattleScreen.xml,
  CharacterCreation/CharacterCreationNarrativeStage.xml, FacGen/PreBuildCharacterSelection.xml,
  MomentumView/MomentumView.xml, Party/PartyScreen.xml, Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml}`.
- Prefabs deleted (19): `Mission/AgentStatus.xml`, `CareerSystem/AbilityHUD.xml`, `EventXml.xml`,
  `PreviewPanel.xml`, `mainmenuHUD.xml`, `PoppyHUD.xml`, `ROTsprite.xml`, `MomentumView/KingdomIcon.xml`, and the
  eleven copies listed in the replica table (all under `Main/_Module/GUI/PreFabs/`).
- Rules and docs: `.claude/rules/gui-ui.md`, `.claude/rules/moduledata-validation.md`,
  `.claude/rules/vanilla-data-comparison.md`, `.claude/rules/tests.md`, `.ai/review-reference.md`,
  `docs/reference/engine/mission-and-missionbehavior-lifecycle.md`, `docs/features/career-system.md`,
  `docs/features/equip-presets.md`.
- Part C, Step C6: `tools/data_gates.py` (the `gui-tests` row only).
- Part D only: `tools/package_release.py`, `tools/tests/test_package_release.py`, `docs/reference/release-process.md`.

**Out of scope (do NOT touch):**
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`,
  `.claude/settings*.json`, `CHANGELOG.md` (D1), any `Main/_Module/ModuleData/**`, any `Client/**` path.
- `.claude/skills/**` (the engine-bump and release skill lines go to WP143 through the hand-off),
  `docs/reviews/lessons/**`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144 sync these once), every `docs/reviews/rca-*.md`.
- The nameplate clones (`PartyNameplateItem.xml`, `PartyPlayerNameplateItem.xml`) and `PartyScreen.xml`'s
  widget types: the 1.5.x re-bases (`71484b8e4`, `8d5cfc623`) follow v1.5.2/v1.5.3 renames that v1.4.8 does
  not have (`BoolStateChangerWidget.cs` exists in the v1.4.8 dump; `NameBloodFeudContainer` and
  `ShipBannerContainerWidget` do not).
- The custom battle panel easing (`EaseType="EaseOut" EaseFunction="Quint"`, `abb718c2f`): v1.4.8's own
  SandBoxCore `CustomBattleScreen.xml` carries the same dead `EaseIn`, so there is no vanilla behaviour to
  match; adding the ease would be a UX change. `PanelEaseTests` is not ported for the same reason.
- The game install: never write there. The deployed `Modules/TAOM/GUI/Prefabs/` copies are a maintainer step.

## Git workflow

- Worktree and branch (skip the first command if the integrator already made it):
  `cd /c/Users/mikew/source/repos/TAOM && git worktree add C:/Users/mikew/source/repos/taom-port/140 -b port148/140-gui-prefab-gates bannerlord-1.4.5`
  (forward slashes: Git Bash strips unquoted backslashes). Work only there. Never push, open a PR, tag or merge.
- Per-port procedure (binding): never let `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a
  commit. This plan does not use `git cherry-pick`: each 1.5.x payload it takes is one or two test files
  (copied from a named 1.5.x blob with `git show <sha>:<path> > <path>`), deletions (`git rm`), or exact
  prefab edits (`sed`), because every source commit also carries `Client/`, feature or bookkeeping files
  1.4.x must not get. The result is the same as `git cherry-pick -n` followed by dropping those paths.
- Stage with explicit paths (`git add <paths>`, never `-A`, `-u` or `.`). Write the message to
  `/c/Users/mikew/source/repos/taom-port/140-msg.txt` (overwrite per commit), then in the NEXT, separate Bash
  call run `cd /c/Users/mikew/source/repos/taom-port/140 && git commit -F ../140-msg.txt && git log -1 --format='%h %s' && git status --porcelain`.
  Never through PowerShell. Build and run the step's tests after each commit.
- Commit message template:

```
<type>(<scope>): v2.0.28 - <description>        (at most 72 characters)

<Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
and why. Never paste a 1.5.x body. No em or en dashes. No
Co-Authored-By or "Generated with" line.>

Research: <what was verified on v1.4.8>
Not-tested: <what needs an in-game check>
Ported-from: <40-hex sha>          (one line per included 1.5.x commit)
```

  The version label must equal the `<Version>` in `Main/_Module/SubModule.xml`
  (`grep -o 'v[0-9][0-9.]*' Main/_Module/SubModule.xml | head -1`); `v2.0.28` at planning (D8).

## Steps

### Step 0: Preflight (no commit)

0.1 Create the worktree (Git workflow). `cd` into it for every later command.

0.2 Drift check (top of this plan). STOP only if: any of the 19 files to delete is already missing or has
changed since `1b00d1acd` (`git diff --stat 1b00d1acd..HEAD -- <path>` non-empty); `CareerEnergyBarPrefab.cs`
no longer matches the excerpt; `git diff 1b00d1acd..HEAD -- Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml`
is non-empty; or one of the eight new test files already exists.

0.3 Prerequisites (each must print what is shown):

```bash
cd /c/Users/mikew/source/repos/taom-port/140
ls TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs   # both listed
grep -c 'public static string RepoPath(params string\[\] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs   # 1
grep -c 'CodeRoots = { "Main" }' TAOM.Tests/Infrastructure/RepoPaths.cs                                  # 1
grep -c 'TheDeclaredSet_ExcludesBrushesOfModulesTaomDoesNotDependOn\|EveryPrefabExtensionBrush_IsDeclaredByAModule\|HashSet<string> DeclaredBrushes()' TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs   # 3
grep -c 'def require_build(\|^REPO_ROOT = \|^def _git(' tools/package_release.py                      # 3 (Part D only)
ls tools/data_gates.py tools/tests/test_data_gates.py                                                 # both listed (WP136)
grep -c 'Gate("gui-tests"' tools/data_gates.py                                                        # 1 (WP136)
grep -o 'v1.4.8' "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/Version.xml"                          # v1.4.8
sha256sum < "$BANNERLORD_GAME_DIR/Modules/SandBox/GUI/Prefabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml"   # fdb8a3a6ed66c262...2130
git ls-files Main/_Module/GUI | grep -ci '\.xml$'                                                     # record as N0 (65 at 1b00d1acd)
```

A missing `RepoPaths.cs` or brush test, or an unexpected count, is a STOP for the Parts that need it (B
and C need `RepoPaths`; C needs the brush test; D needs the packager functions). A missing
`tools/data_gates.py` or `tools/tests/test_data_gates.py`, or a `gui-tests` count other than 1, means WP136 is
not merged: STOP for the whole plan. A different popup hash
or game version is a STOP for the whole plan (report it as an environment fact).

0.4 Baseline: Restore, Build, full Test, plain binding gate and (if present) the strict binding gate.
Record total, passed, skipped as T0 and the gate results. Pre-existing failures are a STOP.

---

## Part A: element, XPath, insert-position and hint gates

### Step A1: Element-type and extension XPath gates; delete the two HUD leftovers

Sources: `71484b8e4` (test files only), `f48ef74dc` (only its `PrefabExtensionBindingTests.cs` hunk; D6),
`720e421fa` (the two deletions; its only other hunk is `CHANGELOG.md`, D1).

1. Create the tests from the 1.5.x blobs:
   ```bash
   git show 71484b8e4:TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs > TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs
   git show f48ef74dc:TAOM.Tests/Migration/PrefabExtensionBindingTests.cs > TAOM.Tests/Migration/PrefabExtensionBindingTests.cs
   sed -i '/^    \[TestCategory("RequiresGameIL")\]\r\?$/d' TAOM.Tests/Migration/PrefabExtensionBindingTests.cs
   ```
   The `f48ef74dc` blob is `71484b8e4`'s file plus `b8c000452`'s category line (plan 010, D9: deleted by the
   `sed`) plus `f48ef74dc`'s movie check. Check: `grep -c 'RequiresGameIL\|TAOM.Client' TAOM.Tests/Migration/Prefab*BindingTests.cs`
   prints `0` for both files; `grep -c 'differs in case' TAOM.Tests/Migration/PrefabExtensionBindingTests.cs` prints `1`.
2. In `PrefabElementTypeBindingTests.cs`, method `private static HashSet<string> WidgetTypeNames()`, insert
   these two lines as the first lines of its body (above its `var names = ...`; the file has three such
   lines, edit only the one in this method):
   ```csharp
           // TAOM loads lazily: touch it so the TAOM widget classes are in the AppDomain in a filtered run.
           _ = typeof(TAOM.IoC).Assembly;
   ```
   Why: TAOM prefabs use TAOM widget classes (`TaomCareerDiamondWidget`, `MapContainerWidget`,
   `PolygonWidget`, `BannerWidget`, `FactionImageWidget`, `TaomSettlementPlateWidget`), and
   `GameAssemblies` never loads `TAOM.dll`. 1.5.x adds the same line for `TAOM.Client` in `10f1f0b80`.
3. RED: Build, then Filtered with `FullyQualifiedName~PrefabElementTypeBindingTests|FullyQualifiedName~PrefabExtensionBindingTests`.
   Expect `Failed: 1, Passed: 1`; the failure lists exactly one offender, `Mission\AgentStatus.xml: <CareerEnergyBarWidget>`.
   Any other offender, or an XPath failure, is a STOP.
4. GREEN: `git rm -q Main/_Module/GUI/PreFabs/Mission/AgentStatus.xml Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml`.
   Build; the same filter shows `Passed: 2`.
5. Stage: `git add TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs TAOM.Tests/Migration/PrefabExtensionBindingTests.cs`.
   `git status --porcelain` shows exactly 2 `A` and 2 `D` lines.
6. Commit `fix(ui): v2.0.28 - prefab element and XPath gates, drop HUD leftovers`. Body: the two binding
   gates and what they check; the AgentStatus copy was v1.4.8 vanilla plus an 81-line dead block naming a
   class that exists nowhere, and AbilityHUD.xml an unreferenced empty override; the energy bar's XPath
   resolves in vanilla's file. `Research:` WidgetFactory prefab index and CreateBuiltinWidget,
   UIExtenderEx PrefabComponent movie lookup (v1.4.8, 2.13.2). `Not-tested:` the combat HUD and the career
   energy bar in a battle. `Ported-from:` `720e421fa41423d563d1beec4b2174ec57b80a0c`,
   `71484b8e4651b05e14bed6133d95655ac9a3749a`, `f48ef74dc42d9eebb68af634a6320947384d5fc7`.

**Verify**: Build exit 0; the filter `FullyQualifiedName~PrefabElementTypeBindingTests|FullyQualifiedName~PrefabExtensionBindingTests` shows `Passed: 2, Failed: 0`.

### Step A2: Insert-position gate; the energy bar states its Index

Source: `69b47d00b` (adapted: the TAOM assembly only; the ScreenFaultLog, Namegiving, ReevesLedger,
ServerJit and deep-review lens hunks are 1.5.x-only and skipped).

1. `git show 69b47d00b:TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs > TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs`
2. Edit it (Edit tool):
   - Replace the two comment lines above `private static IEnumerable<Type> InsertPatches()` and its body
     `TaomAssemblies.All.SelectMany(LoadableTypes)` with:
     ```csharp
         // The TAOM assembly (1.4.x has no TAOM.Client). Types that reference an engine assembly the test host cannot load
         // come back null from a partial load; a patch class references only UIExtenderEx, so it always survives.
         private static IEnumerable<Type> InsertPatches() =>
             LoadableTypes(typeof(TAOM.IoC).Assembly)
                 .Where(t => !t.IsAbstract && typeof(PrefabExtensionInsertPatch).IsAssignableFrom(t));
     ```
   - In `EveryInsertPatch_LandsWhereItsInsertTypeSays`, change the message `"the reflection is not reading the TAOM assemblies"` to `"the reflection is not reading the TAOM assembly"`.
   Check: `grep -c 'TaomAssemblies\|TAOM.Client' TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs` prints `0`.
3. RED: Build; Filtered `FullyQualifiedName~PrefabInsertPositionTests`. Expect `Failed: 1, Passed: 1`; the
   failure has one line beginning `CareerEnergyBarPrefab: InsertType.Child without an Index override lands FIRST`.
   Any other line is a STOP.
4. GREEN: in `Main/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs`, directly after the line
   `    public override InsertType Type => InsertType.Child;` and its following blank line, insert:
   ```csharp
       // First child, as shipped: UIExtenderEx 2.13.2 inserts a Child patch before child[Index] and Index defaults to 0,
       // so this states the position the bar has always had.
       public override int Index => 0;

   ```
   Build; the filter shows `Passed: 2`.
5. Rules (exact mechanism lines):
   - `.claude/rules/gui-ui.md`: after the paragraph that starts `**Why:** the four checks above are the general rule`
     and its blank line, insert this paragraph and a blank line:
     `` `PrefabInsertPositionTests` fails a Child insert without its own Index or not on `/Children`, and an Append or Prepend on `/Children`. ``
   - `.ai/review-reference.md`: after the line that starts ``- GameModel constructors receiving services via `IoC.Resolve<>()` ``, insert:
     ``- `IoC.Resolve<T>()` in a UIExtenderEx `[ViewModelMixin]` constructor: the library builds the mixin with `Activator.CreateInstance(type, viewModel)` (UIExtenderEx 2.13.2 `ViewModelComponent`), so it cannot take constructor injection. A resolve in the mixin's `OnRefresh`, tick or command handlers is still a violation``
6. Stage the four files; commit `test(ui): v2.0.28 - gate every prefab insert on where it lands`. `Research:`
   UIExtenderEx 2.13.2 `PrefabComponent.InsertAsChild`, `PrefabExtensionInsertPatch.Index`, `ViewModelComponent`.
   `Ported-from:` `69b47d00bcb77c3f689efab03c9a8f2756f687c3`.

**Verify**: Filtered `FullyQualifiedName~PrefabInsertPositionTests` shows `Passed: 2, Failed: 0`;
`git show --stat HEAD` lists exactly the four files.

### Step A3: Hint-parent gate (K1)

Source: `42148af4c` (SKIP-FEATURE Smith's Measure; K1 takes only `PrefabHintParentGateTests.cs` and one rule sentence).

1. `git show 42148af4c:TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs > TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs`
2. Edit it (Edit tool):
   - Replace the whole `/// <summary> ... /// </summary>` block above `[TestClass]` with:
     ```csharp
     /// <summary>
     /// A Gauntlet <c>HintWidget</c> has no hover of its own: it relays its parent widget's hover events (v1.4.8 HintWidget.cs:15,
     /// :31-35), and hit-testing never picks a widget with <c>DoNotAcceptEvents="true"</c> (EventManager.cs:861, :897). A hint under
     /// such a parent never shows. The trap shipped three times on the 1.5.x line (#857, #898, #910; Messengers, #898, is on both
     /// lines), so this gate builds the XML of every in-code PrefabExtension in TAOM and fails on a hint whose parent refuses events.
     /// A hint at the root of an insert has a vanilla parent and is skipped.
     /// </summary>
     ```
   - In `Documents()`, replace `typeof(TAOM.Client.ClientSubModule).Assembly` with `typeof(TAOM.IoC).Assembly`.
   - Change the comment `// Some Client types name game assemblies` to `// Some TAOM types name game assemblies`.
   - In `TheGate_ScansEveryInCodePrefabExtension`, replace the assertion with:
     ```csharp
             // Five on 1.4.x at planning: CareerButtonPrefab, CareerEnergyBarPrefab, MessengerEncyclopediaPrefabExtension,
             // SpecialResourceIconPrefab, PrefabInsertExtraFastForward.
             Assert.IsTrue(Documents().Count() >= 5, "fewer in-code prefab extensions than TAOM ships: the scan found nothing to check");
     ```
   Check: `grep -c 'TAOM.Client' TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs` prints `0`.
3. Build; Filtered `FullyQualifiedName~PrefabHintParentGateTests`. Expect `Passed: 2` (the gate starts green;
   K1). If the floor test fails (fewer than 5 documents) STOP (K1). If `NoHintWidget_SitsUnderAParentThatRefusesEvents`
   fails, STOP and report the line it prints: it names a real hint defect in code another WP brought.
4. `.claude/rules/gui-ui.md`: directly after the line added in A2 and its blank line, insert:
   `` **Hover targets:** a `HintWidget` relays only its parent's hover (v1.4.8 `HintWidget.cs:15`) and hit-testing skips a widget with `DoNotAcceptEvents="true"` (`EventManager.cs:897`), so a hint under such a parent never shows. `PrefabHintParentGateTests` fails any in-code PrefabExtension hint whose parent sets `DoNotAcceptEvents="true"` (#910, the third repeat). ``
5. Stage the two files; commit `test(ui): v2.0.28 - gate prefab hints whose parent refuses events`.
   `Research:` HintWidget, EventManager (v1.4.8 dump). `Ported-from:` `42148af4cd851687f55b5bf94195cb57287c5b35`.

**Verify**: Filtered `FullyQualifiedName~PrefabHintParentGateTests` shows `Passed: 2, Failed: 0`.

### Part A done criteria

- [ ] Build exit 0; full Test `Failed: 0`; total = T0 + 6.
- [ ] `git ls-files Main/_Module/GUI/PreFabs/Mission/AgentStatus.xml Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml` prints nothing.
- [ ] `git grep -n 'TAOM.Client\|RequiresGameIL\|LiveInstall\|TaomAssemblies' -- TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs TAOM.Tests/Migration/PrefabExtensionBindingTests.cs TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs TAOM.Tests/Infrastructure/PrefabHintParentGateTests.cs` prints nothing.
- [ ] Plain binding gate `Failed: 0`; strict gate (if present) no new failure or skip vs Step 0.
- [ ] Three commits, each subject carrying the SubModule.xml version.

---

## Part B: copy-drift gates, the popup re-base, eleven identical copies deleted

### Step B1: RED run of the copy gates and the K2 proof (no commit yet)

Sources: `8d5cfc623` (the clone test as born), `fc932d4ca` (the identical-copy test), `7012783b3` (the
`DataSource` rule); `540848795` (K2, one test only).

1. Clone test from the `7012783b3` blob (that blob is `8d5cfc623` + `b8c000452` + `10f1f0b80` + `fc932d4ca` + `7012783b3`):
   ```bash
   git show 7012783b3:TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs > TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs
   F=TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs
   sed -i '/^    \[TestCategory("RequiresGameIL")\]\r\?$/d; /^    \[TestCategory("LiveInstall")\]\r\?$/d' $F
   sed -i 's|        // TAOM.Client loads lazily: touch it so the front-end widgets that moved there (ADR-013) are in the AppDomain.|        // TAOM loads lazily: touch it so the TAOM widget classes are in the AppDomain in a filtered run.|' $F
   sed -i 's|_ = typeof(TAOM.Client.ClientSubModule).Assembly;|_ = typeof(TAOM.IoC).Assembly;|; s| && simple != "TAOM.Client")|)|' $F
   grep -c 'TAOM.Client\|RequiresGameIL\|LiveInstall' $F    # 0
   grep -c 'TestCategory("BindingVerification")' $F       # 2
   ```
2. Create `TAOM.Tests/Features/Execution/PartyTupleVanillaParityTests.cs` with exactly this content (the
   `540848795` test minus its blood-feud method, `LiveInstall` replaced by the 1.4.x category, and a guard
   so that a deleted copy reads as "vanilla loads", never as `Inconclusive`, which WP122's strict gate maps to failed):
   ```csharp
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Xml;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using TAOM.Tests.Migration;
   using static TAOM.Tests.Infrastructure.RepoPaths;

   namespace TAOM.Tests.Features.Execution;

   /// <summary>
   /// Pins any TAOM copy of a party tuple prefab to the installed vanilla file apart from the two edits the 1.5.x line makes there
   /// (#1031). The 1.4.x line ships no copy: both were identical to v1.4.8 and were deleted (#1051), so vanilla loads. A port that
   /// brings a copy back (for example #902's TaomPrisonerBanners include) is compared element by element from then on.
   /// </summary>
   [TestClass]
   public class PartyTupleVanillaParityTests
   {
       [DataTestMethod]
       [TestCategory("BindingVerification")]
       [DataRow("PartyTroopTuple.xml")]
       [DataRow("PartyTroopTupleLeft.xml")]
       public void EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits(string file)
       {
           var taomPath = RepoPath("Main", "_Module", "GUI", "PreFabs", "Party", file);
           if (!File.Exists(taomPath))
               return; // no TAOM copy: the vanilla file loads, which is the parity this test exists for
           if (!GameAssemblies.EnsureLoaded())
               Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
           var vanillaPath = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "Party", file);
           if (!File.Exists(vanillaPath))
               Assert.Inconclusive("vanilla prefab not found at " + vanillaPath);

           CollectionAssert.AreEqual(Flatten(vanillaPath), Flatten(taomPath), file);
       }

       // Every element except the two 1.5.x edits: the #902 include and the zero transition durations (Shadowfax Screens).
       private static List<string> Flatten(string path)
       {
           var doc = new XmlDocument();
           doc.Load(path);
           return doc.SelectNodes("//*[not(self::TaomPrisonerBanners)]")!.Cast<XmlElement>()
               .Select(e => e.Name + " " + string.Join(" ", e.Attributes.Cast<XmlAttribute>()
                   .Where(a => !(e.Name == "VisualDefinition" && a.Name == "TransitionDuration"))
                   .OrderBy(a => a.Name).Select(a => a.Name + "=" + a.Value)))
               .ToList();
       }
   }
   ```
3. RED: Build; Filtered `FullyQualifiedName~PrefabCloneWidgetReferenceTests|FullyQualifiedName~PartyTupleVanillaParityTests`.
   Expect `Failed: 2, Passed: 2`:
   - `PartyTupleVanillaParityTests` passes for both rows. **This is the K2 proof**: the copies still exist and
     equal vanilla. If either row fails, STOP (K2): a tuple copy carries a TAOM edit and must not be deleted.
   - `EveryShadowedVanillaWidgetReference_SurvivesInTheTaomClone` fails with exactly two lines, both for
     `Party\PartyTroopManagerPopUp\PartyTroopManagerPopUp.xml (shadows SandBox)`: `root <PartyManageTroopPopupWidget> lacks TertiaryInputKeyVisualParent="TertiaryInputKeyVisualParent"`
     and `<Widget Id="HorseLabelContainer"> lacks DataSource="{UsedHorsesHint}"`.
   - `NoTaomPrefab_IsAnUnchangedCopyOfTheVanillaFileItShadows` fails listing exactly the eleven files in the
     replica table (each `(same as SandBox)` or `(same as SandBoxCore)`).
   Any other line in either failure is a STOP (it names drift this plan has not analysed).

### Step B2: Re-base the troop manager pop-up on the installed v1.4.8 file (commit 1 of Part B)

Sources: `7012783b3` (the re-base, which used v1.5.6; here the base is v1.4.8) and `8d5cfc623`'s splice of
`IsTertiaryActionAvailable` / `TertiaryInputKeyVisualParent` (subsumed by the re-base). TAOM's deliberate
edits are kept: a single centred Done button with no Cancel button, and the top-right close `ButtonWidget`.

```bash
cd /c/Users/mikew/source/repos/taom-port/140
P=Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml
V="$BANNERLORD_GAME_DIR/Modules/SandBox/GUI/Prefabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml"
git show 1b00d1acd:$P | sed -n '195,201p' > ../140-closebutton.txt      # TAOM's close button block, 7 lines
head -1 ../140-closebutton.txt                                            # prints:         <!--Close Button-->
tr -d '\r' < "$V" > $P
sed -i 's/ PositionYOffset="-62" PositionXOffset="150" Parameter.ButtonAction="ExecuteDone"/ PositionYOffset="-62" Parameter.ButtonAction="ExecuteDone"/' $P
sed -i '/<!--Cancel Button-->/d; /<Standard.PopupBackButton /d' $P
N=$(grep -n '<Widget Id="PrimaryInputKeyVisualParent"' $P | cut -d: -f1); sed -i "$((N-1))r ../140-closebutton.txt" $P
diff <(tr -d '\r' < "$V") $P
grep -c 'TertiaryInputKeyVisualParent' $P; grep -c 'DataSource="{UsedHorsesHint}"' $P; grep -c 'Command.Click="ExecuteCancel"' $P; grep -c 'Standard.PopupBackButton' $P
rm -f ../140-closebutton.txt
```

**Check**: the `diff` prints exactly three hunks, `21c21` (the Done button loses `PositionXOffset="150"`),
`23,24d22` (the Cancel comment and `Standard.PopupBackButton`) and `196a195,201` (TAOM's close button
block, ending with a blank line); the four counts print `2`, `1`, `1`, `0` (the two `Tertiary` lines are
the root attribute and the `TertiaryInputKeyVisualParent` widget, both from v1.4.8). This recipe was
dry-run in memory against the installed file during planning and produced exactly those hunks. Do NOT
remove vanilla's `AlwaysRefreshAll="true"` here; Step C3 does.

Build; Filtered `FullyQualifiedName~PrefabCloneWidgetReferenceTests`: `EveryShadowedVanillaWidgetReference...`
now passes; the identical-copy test still fails with the same eleven files.

Stage only `$P`; commit `fix(ui): v2.0.28 - the troop upgrade pop-up shows used horses again`. Body: the
copy was an old vanilla file; it now follows the v1.4.8 file (header layout, list, key-hint placement,
the used-horses tooltip and the tertiary key hint parent the widget needs) with TAOM's centred Done
button and top-right close button kept. `Research:` PartyManageTroopPopupWidget.OnLateUpdate (v1.4.8 dump
:73-81). `Not-tested:` the pop-up in game (mouse and gamepad). `Ported-from:`
`7012783b35f0329c128e5bc1d3aaf2781a24ad79`, `8d5cfc6231a8638610b6c1a4e2225cb98c92ca50`.

### Step B3: Delete the eleven identical copies and land the gates (commit 2 of Part B)

```bash
cd /c/Users/mikew/source/repos/taom-port/140 && D=Main/_Module/GUI/PreFabs
git rm -q $D/CustomBattle/CompositionSlider.xml $D/CustomBattle/SimpleDropdown.xml $D/CustomBattle/TroopTypeSelectionPopUp.xml \
  $D/Nameplate/PartyNameplate.xml $D/Nameplate/SettlementNameplate.xml $D/Party/PartyComposition.xml $D/Party/PartySortController.xml \
  $D/Party/PartyTroopManagerPopUp/PartyTroopRecruitItem.xml $D/Party/PartyTroopManagerPopUp/PartyTroopUpgradeItem.xml \
  $D/Party/PartyTroopTuple.xml $D/Party/PartyTroopTupleLeft.xml
```

Then edit `.claude/rules/vanilla-data-comparison.md` (Edit tool):
- In the paragraph under `## GUI prefab clones go stale across versions`, the first parenthesis ends with
  `game-menu`, then a long dash, then `~32 of TAOM's 48 prefabs are clones)`. Delete the long dash and that
  clause (they are stale counts) so the parenthesis reads exactly
  `(party screen, custom-battle, encyclopedia, nameplates, game-menu)`.
- Under `**How to fix a stale clone:**`, replace steps 1 and 2 with:
  ```
  1. `diff -w --strip-trailing-cr <installed-vanilla> <taom-clone>` and classify each delta: **rename-casualty / stale-attribute** vs **intentional TAOM customization**.
  2. If the clone has **no** intentional content, **delete it** so vanilla loads, from the repo and from the deployed `Modules/TAOM/GUI/Prefabs/` (a deploy never deletes). `NoTaomPrefab_IsAnUnchangedCopyOfTheVanillaFileItShadows` (#1051) fails only on a copy that already matches the installed file; a stale copy passes it, so step 1 finds those. A clone with TAOM tweaks is re-synced: the installed file verbatim, with only those tweaks re-applied.
  ```

GREEN: Build; Filtered `FullyQualifiedName~PrefabCloneWidgetReferenceTests|FullyQualifiedName~PartyTupleVanillaParityTests|FullyQualifiedName~PrefabElementTypeBindingTests|FullyQualifiedName~PrefabExtensionBindingTests`
shows `Passed: 6, Failed: 0` (the tuple rows now return early). Then the full Test: `Failed: 0`.

Stage the two test files and the rule (the deletions are already staged); `git status --porcelain` shows
11 `D`, 2 `A`, 1 `M`. Commit `fix(gui): v2.0.28 - drop eleven screen files copied from the base game`. Body:
the eleven files were identical to the v1.4.8 files they replaced (the eight 1.5.x deleted, plus both
party tuple rows and the custom battle troop type pop-up, which are identical on 1.4.x), so nothing
changes for players; the gates that keep it so; installs must remove the old `Modules\TAOM` folder or the
copies stay. `Research:` WidgetFactory last-wins prefab index (v1.4.8). `Not-tested:` the party screen,
custom battle and campaign nameplates in game. `Ported-from:` `fc932d4ca22a6741366c8c13b4f98d7532a3c7de`,
`8d5cfc6231a8638610b6c1a4e2225cb98c92ca50`, `7012783b35f0329c128e5bc1d3aaf2781a24ad79`,
`5408487956f6f7bf41ca21bc6a4aeecafd5c4b18`.

### Part B done criteria

- [ ] Build exit 0; full Test `Failed: 0`; total = T0 + 6 (Part A) + 4 (two clone tests, two tuple rows).
- [ ] `git ls-files Main/_Module/GUI | grep -ci '\.xml$'` = N0 - 13.
- [ ] `git diff 1b00d1acd -- Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml | grep -c '^+.*TertiaryInputKeyVisualParent'` prints at least `1`.
- [ ] Plain binding gate `Failed: 0`; strict gate (if present) no new failure or skip.

---

## Part C: brush, attribute-name and stretch-in-cover gates; dead settings and files

### Step C1: Brush gate for GUI prefabs and the nine repoints

Source: `62fb6bb1d` (prefab repoints, `EventXml.xml` deletion, `EveryGuiPrefabBrush_IsDeclaredByAModule`).
WP131 created the file from `2c7f7fae9`; this step adds members to it.

1. In `TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`, directly after the closing `}` of
   `TheDeclaredSet_ExcludesBrushesOfModulesTaomDoesNotDependOn()`, insert a blank line and lines 78-139 of
   `git show 62fb6bb1d:TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` (from
   `    // Values a GUI prefab may carry that are not a brush defined by a module.` to the closing `    }` of
   `EveryGuiPrefabBrush_IsDeclaredByAModule()`). Print them with `git show 62fb6bb1d:TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs | sed -n '78,139p'`
   and confirm the first and last lines before pasting. Then replace the `AllowedNonDeclaredNames` initializer body with:
   ```csharp
       {
           // Empty on 1.4.x: no v1.4.8 vanilla prefab names a brush outside TAOM's module set (1.5.x lists a NavalDLC
           // ship-banner frame here, which only v1.5.3 vanilla prefabs carry).
       };
   ```
   Keep `WidgetIdAttribute` (`MarinerTroopBrush` is a `BrushWidget` property on v1.4.8 too, dump `PartyUpgradeButtonWidget.cs:58`).
   WP136 (`8caac7da5`) merged first and keeps `DeclaredBrushes()`; if it is renamed anyway, call whatever method
   now returns the declared set; if none does, STOP.
2. Replace the class summary above `public class PrefabExtensionBrushTests` (keep WP131's attributes on the class) with:
   ```csharp
   /// <summary>
   /// A brush-typed attribute (<c>Brush</c>, <c>IconBrush</c>, ...) in a PrefabExtension's injected XML or in a TAOM GUI/PreFabs
   /// file, and a GUI prefab's <c>Constant BrushName</c>, must name a brush the engine loads for TAOM's module set. Gauntlet
   /// resolves an unknown name to the default brush without an error, so the widget just draws unstyled (#989: the Messengers
   /// button named two brushes no module defines). PrefabExtension names are read from every C# source under Main/ that
   /// declares one; GUI prefabs are parsed as XML (comments ignored, any quoting). Bindings (<c>{</c>), <c>@</c> values and
   /// Gauntlet <c>!Constant</c> / <c>*Parameter</c> references are skipped. The engine loads the top-level GUI/Brushes/*.xml of
   /// each module in launcher order, keyed by file name so a later module's file of the same name replaces an earlier one, and
   /// TAOM's own folder last (v1.4.8 UIResourceManager.cs:117-122). The test models that with TAOM's DependedModule list followed
   /// by TAOM. A brush from a module TAOM does not depend on (NavalDLC) is therefore not declared. Inconclusive without the install.
   /// </summary>
   ```
3. RED: Build; Filtered `FullyQualifiedName~PrefabExtensionBrushTests`. Expect exactly one failure,
   `EveryGuiPrefabBrush_IsDeclaredByAModule`, listing the nine entries of the replica table. Anything else is a STOP.
4. GREEN, the repoints (each command prints the count shown; run them from the worktree root):
   ```bash
   D=Main/_Module/GUI/PreFabs
   sed -i 's/Brush="Popup.Text.Small"/Brush="Tooltip.Text"/g' $D/BattleActionBar.xml; grep -c 'Popup.Text.Small' $D/BattleActionBar.xml     # 0 (2 or 3 sites changed: WP130 may have removed the hotkey badge)
   sed -i 's/Brush="CharacterDeveloper.DescriptionText"/Brush="CareerSystem.Effects.Passive"/g; s/Brush="CharacterDeveloper.SkillNameText"/Brush="CareerSystem.Effects.Title" Brush.TextHorizontalAlignment="Left"/g' $D/CareerSystem/CareerScreen.xml
   grep -c 'CharacterDeveloper\.' $D/CareerSystem/CareerScreen.xml                                                                       # 0
   F=$D/CustomBattle/ArmyComposition.xml
   sed -i 's/SuggestedWidth="362" SuggestedHeight="38" HorizontalAlignment="Center" VerticalAlignment="Center" Brush="Header.Frame"/SuggestedWidth="362" SuggestedHeight="38" HorizontalAlignment="Center" VerticalAlignment="Center" Brush="CustomBattle.Slider.Canvas"/' $F
   sed -i 's/SuggestedWidth="400" SuggestedHeight="65" HorizontalAlignment="Center" VerticalAlignment="Center" Brush="Header.Frame"/SuggestedWidth="400" SuggestedHeight="65" HorizontalAlignment="Center" VerticalAlignment="Center" Brush="CustomBattle.Slider.Frame"/' $F
   sed -i 's/Brush="SPCharacterDeveloper.Statistics.Fill"/Brush="CustomBattle.Slider.Fill"/g; s/Brush="ButtonBrush1" DoNotAcceptEvents="true" GamepadNavigationIndex="0"/Brush="CustomBattle.Slider.Handle" DoNotAcceptEvents="true" GamepadNavigationIndex="0"/; s/Brush="Clan.TextInput"/Brush="CustomBattle.SliderValue.Text"/; s/Brush="TextBox.Background"/Brush="CustomBattle.SliderValue.Input"/' $F
   grep -c 'Header.Frame\|SPCharacterDeveloper\|Clan.TextInput\|TextBox.Background\|Brush="ButtonBrush1"' $F                         # 0
   grep -c 'CustomBattle.Slider' $F                                                                                                        # 7
   sed -i 's/Brush="ButtonBrush1.Text"/Brush="Popup.Button.Text"/' $D/PresetsOverlay.xml; grep -c 'ButtonBrush1.Text' $D/PresetsOverlay.xml   # 0
   git rm -q $D/EventXml.xml
   ```
   (`CareerScreen.xml` held 3 `DescriptionText` and 2 `SkillNameText` at `1b00d1acd`; none of those lines
   carries `Brush.TextHorizontalAlignment`, so no attribute is duplicated.) Build; the filter shows `Failed: 0`.
5. Feature docs (Edit tool):
   - `docs/features/career-system.md`: after the bullet that starts `- **Owed:** in-game verification of the two 2026-08-06 effect fixes`, add the bullet
     ``- #1005 (ported to 1.4.x): the `CharacterDeveloper.SkillNameText`/`.DescriptionText` brush cleanup (RCA finding #3) is done; career names, descriptions and ability names use `CareerSystem.Effects.Title` and `CareerSystem.Effects.Passive`.``
   - `docs/features/equip-presets.md`: in the step that starts ``1. Edit `Main/_Module/GUI/Prefabs/PresetsOverlay.xml`.``,
     the parenthesis ``(`ButtonBrush1`, `ButtonBrush1.Text`, etc.)`` is followed by a long dash and
     `no custom sprites are registered for this feature.` Rewrite that tail so it reads exactly
     ``(`ButtonBrush1`, `Popup.Button.Text`, etc.); no custom sprites are registered for this feature.``
     (the long dash becomes a semicolon).
6. Stage the test, the four prefabs, the two docs (the deletion is staged). Commit
   `fix(ui): v2.0.28 - nine screen widgets draw with their real brushes`. Body: what each screen now uses
   (action bar `Tooltip.Text`; career picker `CareerSystem.Effects.*`; custom battle slider vanilla's
   `CustomBattle.Slider.*` and number box `CustomBattle.SliderValue.*`; presets label `Popup.Button.Text`),
   the unused `EventXml.xml`, the new GUI prefab brush test. `Research:` brush files of Native, SandBoxCore
   and TAOM (v1.4.8). `Not-tested:` the four screens in game. `Ported-from:` `62fb6bb1deaac84d4daceada5657c30151ea811c`.

**Verify**: Filtered `FullyQualifiedName~PrefabExtensionBrushTests` shows `Failed: 0`.

### Step C2: Brush property gate; delete PreviewPanel.xml

Source: `103636439` (the deletion and `PrefabBrushPropertyTests`; removing installed copies is a maintainer step).

1. Append to `TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` a blank line and lines 171-238 of
   `git show 103636439:TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` (from `/// <summary>` above
   `public class PrefabBrushPropertyTests` to the file's last `}`). Add `using System.Reflection;` to the
   usings if it is not there. Check: `grep -c 'class PrefabBrushPropertyTests' TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` prints `1`.
2. RED: Build; Filtered `FullyQualifiedName~PrefabBrushPropertyTests`. Expect `Failed: 1, Passed: 1`; the
   failure lists only `Brush.Fontsize  (Main\_Module\GUI\PreFabs\PreviewPanel.xml)`. Anything else is a STOP.
3. GREEN: `git rm -q Main/_Module/GUI/PreFabs/PreviewPanel.xml`; Build; the filter shows `Passed: 2`.
4. Stage the test; commit `fix(ui): v2.0.28 - unused weapon preview prefab removed`. `Research:`
   `TaleWorlds.GauntletUI.Brush` public properties (v1.4.8 taom-src). `Ported-from:` `10363643999af79aaec0d9e49e0d5f0447167531`.

**Verify**: Filtered `FullyQualifiedName~PrefabExtensionBrushTests|FullyQualifiedName~PrefabBrushPropertyTests` shows `Failed: 0`.

### Step C3: Attribute-name gate; delete what the engine never applied

Sources: `450332b74` (test, dead attributes, four dead screens), `abb718c2f` (ease-value test cases and
the unused `PreBuildCharacterSelection.xml` animation; its panel easing is skipped), `7012783b3` (a test
comment). Not applied here: `450332b74`'s `TroopTypeSelectionPopUp.xml` edit (deleted in B3) and its
`PartyTroopManagerPopUp.xml` edit (re-based in B2; the attribute is removed below), and its packager part (Part D).

1. Test from the `7012783b3` blob (= `450332b74` + `abb718c2f` + `7012783b3`):
   ```bash
   T=TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs
   git show 7012783b3:$T > $T
   sed -i '/^    \[TestCategory("RequiresGameIL")\]\r\?$/d' $T
   sed -i 's|        // TAOM.Client loads lazily: touch it so the front-end widgets that moved there (ADR-013) are in the AppDomain.|        // TAOM loads lazily: touch it so the TAOM widget classes are in the AppDomain in a filtered run.|; s|_ = typeof(TAOM.Client.ClientSubModule).Assembly;|_ = typeof(TAOM.IoC).Assembly;|' $T
   grep -c 'RequiresGameIL\|TAOM.Client' $T                   # 0
   grep -c 'TestCategory("BindingVerification")' $T          # 2
   ```
   In the `KnownRuntimeType` doc comment (blob lines 512-516, from `/// The one interface-typed path` to
   `/// v1.5.5 decompile; no other widget reassigns it to either of those layouts.`), replace those five lines with:
   ```csharp
           /// The one interface-typed path TAOM prefabs use is <c>LayoutImp</c>. Widget.LayoutImp is an ILayout
           /// (Widget.cs:1184) that each subclass constructor assigns: GridWidget.cs:141 (<c>base.LayoutImp = GridLayout;</c>)
           /// and ListPanel.cs:40 (<c>base.LayoutImp = StackLayout;</c>). Verified against v1.4.8 (taom-src and the dump);
           /// no other widget assigns either of those layouts.
   ```
2. Replace the body of `VanillaParityAllowlist` (everything between its `{` and `};`) with the v1.4.8 list below.
   Each entry is a (widget type, attribute) pair a TAOM prefab uses and v1.4.8 vanilla also carries (found
   this session at the file:line shown, relative to the game's `Modules` folder):
   ```csharp
           // SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaUnitTreeNodeItem.xml:13
           "EncyclopediaUnitTreeNodeItemBrushWidget.ButtonContainer",
           // Native/GUI/Prefabs/GameMenu/GameMenuItem.xml:2
           "GameMenuItemWidget.ProgressBar",
           "GameMenuItemWidget.ProgressTextWidget",
           // SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml:171
           "GridWidget.AutoScrollTopOffset",
           "GridWidget.AutoScrollBottomOffset",
           // Native/GUI/Prefabs/InputDebug.xml:9
           "GridWidget.LayoutImp",
           // SandBox/GUI/Prefabs/Nameplate/PartyNameplateItem.xml:11
           "PartyNameplateWidget.Brush",
           // SandBox/GUI/Prefabs/Nameplate/PartyPlayerNameplateItem.xml:13
           "PartyPlayerNameplateWidget.Brush",
           // SandBox/GUI/Prefabs/Party/PartyScreen.xml:232
           "PartyQuestProgressWidget.Brush",
           // SandBox/GUI/Prefabs/Party/PartyScreen.xml:104
           "PartyScreenWidget.FirstUpgradeButton",
           "PartyScreenWidget.SecondUpgradeButton",
           "PartyScreenWidget.NumOfUpgrades",
           // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemLarge.xml:143
           "QuestMarkerBrushWidget.MarkerScale",
           // Native/GUI/Prefabs/GameMenu/GameMenuTournamentLeaderboard.xml:75 (a public field, not a property, on v1.4.8)
           "ScrollablePanel.MouseScrollAxis",
           // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemLarge.xml:66
           "SettlementNameplateItemWidget.SettlementNameplateCapsuleWidget",
           // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemSmall.xml:66
           "SettlementNameplateItemWidget.SettlementNameplateQuestWidget",
   ```
3. RED: Build; Filtered `FullyQualifiedName~PrefabAttributeNameBindingTests`. Expect exactly one failure,
   `EveryPrefabAttribute_NamesASettableWidgetProperty`, listing: four `<VisualDefinition> EaseIn` problems
   (`CustomBattle/CustomBattleScreen.xml` three times, `FacGen/PreBuildCharacterSelection.xml` once) and these
   bad uses: `<Widget> VerticalAlinment` (CharacterCreationNarrativeStage), `<ButtonWidget> AcceptEvents`
   (OOBButtonsOverlay twice, PresetsOverlay once), `<BrushWidget> StackLayout.LayoutMethod` (MomentumView),
   `<ListPanel> StackLayout.LayoutImp` (PartyScreen three times), `<ListPanel> AlwaysRefreshAll` (the pop-up).
   The other 17 tests pass (the allowlist-is-used test included). If the output differs, apply this table and
   re-run; STOP if more than five pairs fall outside it:

   | What the run reports | Action |
   |---|---|
   | a bad use (`Unknown` or `NotSettable`) whose (type, attribute) pair v1.4.8 vanilla also carries, found with `grep -rn '<Type[^>]*Attribute=' "$BANNERLORD_GAME_DIR"/Modules/{Native,SandBoxCore,SandBox}/GUI/Prefabs` | add it to the allowlist with that file:line comment |
   | a misspelling (`VertialAlignment`, `VerticalAlinment`), `AlwaysRefreshAll`, or any pair vanilla does not carry | delete the attribute from the TAOM prefab (the engine never applied it) |
   | a stale allowlist entry (`VanillaParityAllowlist_EveryEntryIsStillUsed` fails) | remove the entry |
   | `RuntimeTypeUnknown`, `Ambiguous`, "does not resolve to a widget type", "not well-formed", an ease value that is not an enum name | STOP and report |

4. GREEN, the deletions (from the worktree root; each check prints `0`):
   ```bash
   D=Main/_Module/GUI/PreFabs
   sed -i 's/ VerticalAlinment="Center"//' $D/CharacterCreation/CharacterCreationNarrativeStage.xml; grep -c 'VerticalAlinment' $D/CharacterCreation/CharacterCreationNarrativeStage.xml
   sed -i 's/ EaseIn="true"//g' $D/CustomBattle/CustomBattleScreen.xml; grep -c 'EaseIn=' $D/CustomBattle/CustomBattleScreen.xml
   sed -i '/<VisualDefinitions>/,/<\/VisualDefinitions>/d' $D/FacGen/PreBuildCharacterSelection.xml; grep -c 'VisualDefinition' $D/FacGen/PreBuildCharacterSelection.xml
   sed -i 's/ StackLayout.LayoutMethod="VerticalTopToBottom" Brush="Frame1Canvas"/ Brush="Frame1Canvas"/' $D/MomentumView/MomentumView.xml; grep -c 'LayoutMethod="VerticalTopToBottom" Brush="Frame1Canvas"' $D/MomentumView/MomentumView.xml
   sed -z -i -E 's/"true"\r?\n[[:blank:]]*AcceptEvents="true">/"true">/g' $D/OOBButtonsOverlay.xml $D/PresetsOverlay.xml; grep -c '^[[:space:]]*AcceptEvents=' $D/OOBButtonsOverlay.xml $D/PresetsOverlay.xml
   sed -i 's/ StackLayout.LayoutImp="HorizontalLeftToRight"//g' $D/Party/PartyScreen.xml; grep -c 'StackLayout.LayoutImp' $D/Party/PartyScreen.xml
   sed -i 's/ AlwaysRefreshAll="true"//' $D/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml; grep -c 'AlwaysRefreshAll' $D/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml
   git rm -q $D/mainmenuHUD.xml $D/PoppyHUD.xml $D/ROTsprite.xml $D/MomentumView/KingdomIcon.xml
   ```
   (`PreBuildCharacterSelection.xml` holds one `VisualDefinitions` block, lines 30-34 at `1b00d1acd`, and no
   widget names `VisualDefinition="LeftPanel"`; `DoNotAcceptEvents` lines are untouched by the `sed -z`.)
   Build; the filter shows `Passed: 18, Failed: 0`.
5. `.claude/rules/gui-ui.md` (Edit tool):
   - In the paragraph that starts ``**The same rule applies to `Brush="X"` (added 2026-08-05).**``, replace the
     text from `the widget renders with default styling, no crash, no log.` to the end of the paragraph
     (`` see `docs/reviews/rca-career-ux-arc-2026-08-05.md` finding #3).``) with:
     ```
     the widget renders with default styling, no crash, no log. Before writing any `Brush="X"`, run
     `PrefabExtensionBrushTests` rather than grepping (SandBoxCore declares the `CustomBattle.*` brushes TAOM's custom battle prefabs use, and a
     brush in one of the vanilla brush files TAOM replaces whole is never loaded, so a grep finds names
     the engine never sees). The vanilla `"<X>.Text"` naming pattern is NOT auto-derived: each `.Text`
     brush is its own declaration. Shipped instances of this bug: `ButtonBrush1.Text` (AbilityHUD,
     PresetsOverlay), `CharacterDeveloper.SkillNameText`/`.DescriptionText` (CareerScreen.xml; all
     since repointed, see `docs/reviews/rca-career-ux-arc-2026-08-05.md` finding #3).
     ```
   - Replace the line that starts ``**Gate:** `PrefabExtensionBrushTests` `` (WP131 added it) with the line
     below; if no such line exists, insert it after that paragraph with a blank line before it:
     ```
     **Gate:** `PrefabExtensionBrushTests` fails a brush-typed attribute (`Brush`, `IconBrush`, ...) in a PrefabExtension that no module in TAOM's DependedModule set declares (#989); `EveryGuiPrefabBrush_IsDeclaredByAModule` does the same for every literal brush and `Constant BrushName` in `Main/_Module/GUI/PreFabs/**/*.xml`, skipping the `MarinerTroopBrush` attribute (on v1.4.8 `PartyUpgradeButtonWidget.MarinerTroopBrush` is a `BrushWidget` that takes a child widget Id, not a brush name); `EveryGuiPrefabBrushProperty_NamesABrushProperty` fails a `Brush.<Property>` attribute, in GUI prefab XML and in PrefabExtension sources, whose first segment after `Brush.` is not a public property of `TaleWorlds.GauntletUI.Brush`, compared case-sensitively (Gauntlet drops an unknown name silently); `EveryPrefabAttribute_NamesASettableWidgetProperty` fails any other attribute that is not an exact-case public property with a public setter on the element's resolved widget type (a prefab tag resolves to its prefab's root widget), outside a vanilla-parity allowlist, so a misspelt `VertialAlignment` fails; the same test fails a `VisualDefinition` attribute other than `Name`, `TransitionDuration`, `EaseType`, `EaseFunction` and `DelayOnBegin` (v1.4.8 `WidgetPrefab.LoadVisualDefinitions` reads only those) and a `VisualState` attribute that is not `State` or a public settable property of `TaleWorlds.GauntletUI.VisualState`. In a shipped prefab, delete it: players have never seen it applied. In markup you are writing, fix the name and look in game.
     ```
6. Stage the test, the eight edited prefabs, the rule (deletions are staged). Commit
   `fix(ui): v2.0.28 - screens drop settings the game never applied`. Body: the removed settings and four
   unloaded screens; nothing changes on screen; the new gate and its v1.4.8 allowlist; the custom battle
   panel ease is left alone because v1.4.8's own file carries the same dead `EaseIn`. `Research:`
   WidgetPrefab.LoadVisualDefinitions, WidgetExtensions.GetObjectAndProperty, GridWidget, ListPanel,
   StackLayout (v1.4.8 taom-src). `Ported-from:` `450332b74cf82403d78033463687dafec446fedf`,
   `abb718c2fc835297e2e5a6e9ab9973acaf291f79`, `7012783b35f0329c128e5bc1d3aaf2781a24ad79`.

**Verify**: Filtered `FullyQualifiedName~PrefabAttributeNameBindingTests` shows `Passed: 18, Failed: 0`; full Test `Failed: 0`.

### Step C4: Stretch-in-cover ratchet; name the GUI prefab gates in the rules

Sources: `e6dbea3e0` (the test only; every Fords of War file stays out), `53a4e4e52` (its fixed form),
`d4fb51a50` (the rule row, rewritten as a 1.4.x section).

1. `git show 53a4e4e52:TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs > TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs`
2. Edit it (Edit tool):
   - Replace the first three summary lines (blob lines 12-14, from `/// Gauntlet measures every visible child`
     to `covered the whole scene).`) with:
     ```csharp
     /// Gauntlet measures every visible child against the parent's whole space (DefaultLayout.MeasureChildren, v1.4.8 taom-src), so a
     /// StretchToParent child of a CoverChildren parent with no MaxWidth or MaxHeight grows the parent to everything it is offered. It
     /// shipped three times on the 1.5.x line (#388, #866 and a Fords of War panel on 2026-10-31).
     ```
     and on the summary line that contains it, replace `The five older files below` with `The four older files below`.
   - Delete the line `        ["Main/_Module/GUI/PreFabs/StagedSiege/StagedSiegeHud.xml"] = 1,` (that file does not exist on 1.4.x).
   - Replace `        Assert.IsTrue(scanned > 50, "the prefab folder was not found");` with:
     ```csharp
             // 1.4.x holds about 46 GUI XML files once plan 140's deletions land (65 at 1b00d1acd); 30 still proves the folder was found.
             Assert.IsTrue(scanned > 30, "the prefab folder was not found");
     ```
   `RepoPaths` comes from WP104 (Step 0.3 checked it); do not add a local helper.
3. Build; Filtered `FullyQualifiedName~GauntletStretchInCoverRatchetTests`. Expect `Passed: 6`. If
   `NoPrefab_GainsAStretchChildInsideACoverChildrenParent` fails on a count for one of the four known files:
   run `git log --oneline 1b00d1acd..HEAD -- <that file>`; if an earlier WP's commit is listed, set that
   `Known` value to the live count and report it in the hand-off; if nothing is listed, STOP. A failure naming
   any other file is a STOP.
4. `.claude/rules/tests.md`: insert before `## Test Organization`:
   ```
   ## Gates that key on repo paths

   A gate that builds a repo-relative key from the disk compares it with `OrdinalIgnoreCase`: git tracks
   `Main/_Module/GUI/PreFabs`, and a checkout can hold the folder as `Prefabs` on a case-insensitive
   filesystem (`GauntletStretchInCoverRatchetTests`, #1078).

   ```
5. `.claude/rules/moduledata-validation.md`: insert before `## Discipline` (if WP131 added a line naming
   `PrefabExtensionBrushTests` elsewhere in this file, leave that line as it is):
   ```
   ## GUI prefab gates (`Main/_Module/GUI`)

   These tests gate TAOM's Gauntlet prefabs and in-code PrefabExtensions. The ones marked (install) read the
   installed game through `BANNERLORD_GAME_DIR` and go Inconclusive without it.

   | Test | Fails on |
   |---|---|
   | `PrefabElementTypeBindingTests` (install) | an element that names neither a widget class nor a prefab |
   | `PrefabExtensionBindingTests` (install) | a `[PrefabExtension]` movie that is missing or differs in case, or an XPath that matches nothing in the winning prefab |
   | `PrefabInsertPositionTests` | a Child insert with no Index or not on `/Children`; an Append or Prepend on `/Children` |
   | `PrefabHintParentGateTests` | an in-code `HintWidget` whose parent sets `DoNotAcceptEvents="true"` |
   | `PrefabCloneWidgetReferenceTests` (install) | a TAOM copy of a vanilla prefab that drops a Widget-typed reference or a `DataSource` the vanilla file declares (Id-paired elements); a TAOM prefab identical to the vanilla file it shadows |
   | `PartyTupleVanillaParityTests` (install) | a TAOM copy of a party tuple prefab that differs from vanilla beyond the two 1.5.x edits |
   | `PrefabExtensionBrushTests`, `PrefabBrushPropertyTests` | a brush no module in TAOM's set declares; a `Brush.<Property>` that is not a `Brush` property |
   | `PrefabAttributeNameBindingTests` (install) | an attribute the engine cannot set, outside a vanilla-parity allowlist; a `VisualDefinition` or `VisualState` attribute the engine never reads |
   | `GauntletStretchInCoverRatchetTests` | a new `StretchToParent` child of a `CoverChildren` parent with no max size (grid parents exempt); four older files are counted and may only fall |

   ```
6. Stage the test and the two rules; commit `test(ui): v2.0.28 - gate stretch children inside CoverChildren`.
   `Research:` DefaultLayout.MeasureChildren, GridLayout.UpdateCellSizes (v1.4.8 taom-src). `Ported-from:`
   `e6dbea3e028e4a296815de81258741c7f9dea734`, `53a4e4e52a2a67a1098edfb588ab7f5a71900056`,
   `d4fb51a5056be817c48692e6e4783b52bb9bdbfe`.

**Verify**: Filtered `FullyQualifiedName~GauntletStretchInCoverRatchetTests` shows `Passed: 6, Failed: 0`.

### Step C5: Engine note: never spawn an agent inside AfterStart

Source: `e6dbea3e0`'s paragraph in `docs/reference/engine/mission-and-missionbehavior-lifecycle.md`,
rewritten on the v1.4.8 dump (the 1.5.x text cites v1.5.6 lines, a third-party mod and Fords of War). If
`grep -c 'Never spawn an agent inside' docs/reference/engine/mission-and-missionbehavior-lifecycle.md`
already prints `1` (WP144 brought it), skip this step and say so in the hand-off.

Insert this paragraph and a blank line directly above the line that starts `## ⚠️ The` (the `: MissionLogic`
gotcha heading), below the `### Lifecycle order (typical)` code block:

```
**Never spawn an agent inside `AfterStart` (v1.4.8).** `Mission.AfterStart` runs every behaviour's
`EarlyStart` and then every behaviour's `AfterStart` with `foreach` over `MissionBehaviors`, a
`List<MissionBehavior>` (Mission.cs:1329, 3817-3826), and `AddMissionBehavior` appends to that same list
(Mission.cs:4615-4617). An agent spawned inside the loop runs every agent-creation hook, and a hook
(TAOM's or another mod's) that adds a behaviour on that spawn changes the list mid-loop, so the
enumerator's next step throws. `MissionState.FinishMissionLoading` clears `_missionInitializing` before it
calls `AfterStart` (MissionState.cs:335, 345), and `AfterStart` sets `State.Continuing` only on its last
line (Mission.cs:3835), so the next `MissionState.OnTick` reaches `LoadMission` and `Mission.Initialize`
again on the same mission object (MissionState.cs:89-96, 221-244). The engine also walks
`MissionBehaviors` with `foreach` for `OnAgentCreated` (Mission.cs:4061) and `OnAgentBuild` (4344, 4358),
so never spawn from a callback the engine dispatches with `foreach`; `OnMissionTick` is an index loop
(3741-3744). Spawn on the first mission tick instead. Line numbers: the v1.4.8 dump (`_categories_v1.4.8`).
```

Check: `python tools/lint_docs.py` reports no new finding in this file. Stage it; commit
`docs(engine): v2.0.28 - never spawn an agent inside AfterStart`. `Research:` Mission.AfterStart,
AddMissionBehavior, MissionState.TickLoading/FinishMissionLoading (v1.4.8 dump). `Ported-from:`
`e6dbea3e028e4a296815de81258741c7f9dea734`.

### Step C6: `data_gates.py` runs this plan's gates

Source: WP136's hand-off (`plans/136-data-integrity-gates.md` Step B7 and "Hand-off to WP140"). No 1.5.x commit
is taken here. Why: `python tools/data_gates.py` is the one command for data gates, and WP136 kept in its
`gui-tests` row only the names that existed on its tree, so without this step none of this plan's gates runs from
it. This step runs even if C5 was skipped.

1. In `tools/data_gates.py`, in the `_dotnet(...)` call of the `Gate("gui-tests", ...)` row, keep every name already
   there and add these nine (Edit tool, wrapping the lines as the row does):
   ```python
   "PrefabElementTypeBindingTests", "PrefabExtensionBindingTests", "PrefabInsertPositionTests",
   "PrefabHintParentGateTests", "PrefabCloneWidgetReferenceTests", "PartyTupleVanillaParityTests",
   "PrefabBrushPropertyTests", "PrefabAttributeNameBindingTests", "GauntletStretchInCoverRatchetTests",
   ```
   `_dotnet` turns each name into a `FullyQualifiedName~<name>` clause. `PrefabBrushPropertyTests` is listed although
   it lives in `PrefabExtensionBrushTests.cs`: it is a class of its own, so the `PrefabExtensionBrushTests` clause does
   not select it. Change nothing else in the file.
2. Check the row:
   ```bash
   python -c "import sys;sys.path.insert(0,'tools');import data_gates as d;f=next(g for g in d.GATES if g.id=='gui-tests').cmd[-1].split('|');print(sum('FullyQualifiedName~'+n in f for n in 'PrefabElementTypeBindingTests PrefabExtensionBindingTests PrefabInsertPositionTests PrefabHintParentGateTests PrefabCloneWidgetReferenceTests PartyTupleVanillaParityTests PrefabBrushPropertyTests PrefabAttributeNameBindingTests GauntletStretchInCoverRatchetTests'.split()))"   # 9
   ```
3. Stage: `git add tools/data_gates.py`. Commit `test(ui): v2.0.28 - data gates run the GUI prefab gates`. Body: the
   one data-gate command now runs the nine prefab gates this plan added whenever a GUI prefab changes. No
   `Ported-from:` line (WP136 ported the table; this adds names on 1.4.x).

**Verify**: `python -m unittest tools.tests.test_data_gates` prints `OK`; the check in sub-step 2 prints `9`;
`git show --stat HEAD` lists only `tools/data_gates.py`.

### Part C done criteria

- [ ] Build exit 0; full Test `Failed: 0`; total = T0 + 37 (Part A 6, Part B 4, C1 1, C2 2, C3 18, C4 6;
      the two `DataTestMethod`s count one result per `DataRow`).
- [ ] `git ls-files Main/_Module/GUI | grep -ci '\.xml$'` = N0 - 19, and is greater than 30.
- [ ] `git grep -c 'Brush="Popup.Text.Small"\|CharacterDeveloper.SkillNameText\|CharacterDeveloper.DescriptionText\|Brush="Header.Frame"\|Brush="ButtonBrush1.Text"\|Brush.Fontsize\|VerticalAlinment\|EaseIn="true"' -- Main/_Module/GUI` prints nothing.
- [ ] Plain binding gate `Failed: 0`; strict gate (if present) no new failure or skip vs Step 0.
- [ ] `python tools/lint_docs.py` shows no new finding in a file this plan touched.
- [ ] `python -m unittest tools.tests.test_data_gates` prints `OK`, and the Step C6 check prints `9`.

---

## Part D: the packager refuses TAOM files the release commit does not track

Source: `450332b74`'s `tools/package_release.py` and `tools/tests/test_package_release.py` hunks,
re-implemented on WP115's packager (the 1.4.x tip file is the old 397-line shape; WP115 transplants the
plan 017 file, `9ca99437f`, which has `REPO_ROOT` `:104`, `_git` `:145`, `require_build` `:165` and the
`for name in requested:` loop `:176`). Why it belongs here: this plan deletes 19 prefabs, and a deploy
never deletes, so a release packaged from an old install would ship them again.

### Step D1 (TDD): stale_module_files

1. RED: in `tools/tests/test_package_release.py`, insert lines 542-656 of
   `git show 450332b74:tools/tests/test_package_release.py` (the whole `class TestStaleModuleFiles`, nine tests,
   and the two blank lines after it) directly above the line `class TestBuildStamp(unittest.TestCase):`. Confirm the
   file already imports `subprocess`, `tempfile`, `mock` and defines `_write`; if any is missing, STOP. If
   `class TestRequireBuildCli` defines `def _taom(`, also insert lines 940-949 of the same blob (the test
   `test_dry_run_with_require_build_refuses_a_stale_file` and its trailing blank line) directly above its
   `def test_refuses_when_a_requested_module_is_absent`; otherwise skip that one test and say so in the hand-off.
   Run `python -m unittest tools.tests.test_package_release`: the new tests error with
   `AttributeError` naming `stale_module_files`; every other test passes.
2. GREEN in `tools/package_release.py`:
   - Insert lines 539-589 of `git show 450332b74:tools/package_release.py` (the comment block,
     `REPO_MODULE_ROOTS`, `_BUILD_OUTPUT_PREFIX`, `_EDITOR_PACK` and `def stale_module_files`) and one blank line
     directly above the line `ARMORY_MODULE = "LOTRLOME_Armory"`; if that line does not exist, directly after the
     end of `def test_troop_files`; if neither exists, STOP.
   - In `def require_build`, directly after the three-line loop that starts `    for name in requested:` and ends
     `            problems.append(f"{name}: requested but not present in the source")`, add this line at function
     indent (4 spaces, outside the loop): `    problems.extend(stale_module_files(plans, sha))`.
   - In the module docstring's exit-code paragraph, after the words `failed --require-build`, insert
     ` (a DLL that is not a clean build of the release commit, or a TAOM file the commit does not track)`.
   - In the `--require-build` help string, append ` and every TAOM file is tracked there` before `(checked in --dry-run too)`.
   - Where the CLI prints `ERROR: not a clean build of {args.require_build}; refusing to package:`, change the
     message to `ERROR: not a clean build of {args.require_build}, or untracked TAOM files; refusing to package:`.
   Run the packager tests: `OK`. Run the tool tests: no failure Step 0 did not have.
3. `docs/reference/release-process.md`: if it mentions `--require-build`, add after the sentence that describes
   what that gate refuses: ``It also refuses any TAOM file the release commit does not track: `stale_module_files` compares the copy list (outside `bin/`, `Shaders/D3D11/` and `RuntimeDataCache*`) with the commit's `Main/_Module` tree, case-insensitively, and exits 2 on a stray file, a git failure or an empty tree. Delete the file from the folder the message names, or commit it first if it is Kit output the repo has not taken yet. `TAOM.Dependencies` is exempt.``
   If the doc does not mention `--require-build`, skip this sub-step and say so.
4. Stage the two tool files (and the doc if edited); commit `fix(tools): v2.0.28 - packager refuses TAOM files the commit lacks`.
   `Ported-from:` `450332b74cf82403d78033463687dafec446fedf`.

### Part D done criteria

- [ ] `python -m unittest tools.tests.test_package_release` prints `OK`; the new class ran nine tests (ten with the CLI test).
- [ ] `git grep -n 'def stale_module_files\|problems.extend(stale_module_files' -- tools/package_release.py` shows two lines.
- [ ] Full tool tests: no failure Step 0 did not have.

---

### Step Z: Hand-off report (your final message)

The status of each Part (done, or stopped and why), for the integrator to record in `plans/README.md`; no
older plan's row needs updating for this WP. T0 and the final total, passed, skipped; N0 and the final GUI XML count; the RED output of each gate as you
saw it (one line each); the final `VanillaParityAllowlist` keys and `Known` values, with any deviation from
this plan and why; skipped sub-steps (C5 if WP144 brought the paragraph, the Part D CLI test, the release
doc sentence); the strict binding gate result; every commit hash and subject. Copy these lines for the
integrator, unchanged:

- For WP143 (harness rules sync), engine-bump skill: in `.claude/skills/engine-bump/SKILL.md`, where the
  binding gates are listed, name `PrefabCloneWidgetReferenceTests` as the test to read when a vanilla prefab
  moved (a TAOM clone replaces the vanilla file, so a widget reference the engine adds is a null the widget
  dereferences), and say: re-base the clone on the installed file and re-apply TAOM's edits; a clone with no
  TAOM edit is deleted, in the repo and in the deployed `Modules/TAOM/GUI/Prefabs/` (`8d5cfc623`, `fc932d4ca`).
- For WP143, release skill: the packager now refuses untracked TAOM files under `--require-build`
  (`450332b74`'s `.claude/skills/release/SKILL.md` hunk, rewritten for the 1.4.x skill).
- For WP142 (lessons sync): the lessons these commits carry were not taken here: `localization-ui.md`
  from `8d5cfc623`, `f48ef74dc`, `69b47d00b`, `450332b74`, `abb718c2f`, `e6dbea3e0`, `540848795` and
  `42148af4c` (its hint-parent entry only); `build-tooling-workflow.md` from `103636439`, `450332b74`,
  `7012783b3`, `53a4e4e52`; `testing-qa.md` from `f48ef74dc`; `gamemodels-services.md` from `69b47d00b`
  (the mixin-constructor entry). Prune the 1.5.x-only feature content when syncing.

## Disposition table

| 1.5.x commit | Subject (short) | Group | Disposition | Where |
|---|---|---|---|---|
| `720e421fa` | drop the two HUD leftovers | prefab-binding-gates | Ported: both deletions. `CHANGELOG.md` dropped (D1) | A1 |
| `71484b8e4` | prefab clones re-based on v1.5.2, element-name gate | prefab-binding-gates | Adapted: the two test files, plus a TAOM-assembly load line. The nameplate and party screen edits skipped (v1.5.2 renames; v1.4.8 still has `BoolStateChangerWidget`) | A1 |
| `8d5cfc623` | Bannerlord v1.5.3 and the nameplate clone break | prefab-binding-gates | Adapted: the clone test (through the `7012783b3` blob); the popup splice is subsumed by the B2 re-base. v1.5.3 bump, nameplate re-base, handbook tool, pins, migration docs skipped (1.5-engine); engine-bump skill line handed to WP143; lesson to WP142; RCA skipped (1.5.x incident) | B1-B3 |
| `f48ef74dc` | Shadowfax Screens: screens open without slides | prefab-binding-gate | Adapted: its `PrefabExtensionBindingTests.cs` hunk only (through its blob). The feature, prefab edits, docs and lessons skipped (D6) | A1 |
| `69b47d00b` | gate every prefab insert on where it lands | issue-858-prefab-insert-gate | Adapted: the test on the TAOM assembly, `Index => 0` at the 1.4.x Main path, the gui-ui and review-reference lines. ScreenFaultLog, Namegiving, ReevesLedger, ServerJit, the deep-review lens line (no lenses dir on 1.4.x), feature docs skipped (1.5.x-only); lesson to WP142 | A2 |
| `62fb6bb1d` | nine screen widgets draw with their real brushes | ui-prefab-validation-gates | Ported: the repoints (all `Popup.Text.Small` sites), `EventXml.xml` deletion, the GUI prefab brush test with an empty allowlist, the career-system and equip-presets doc lines; the gui-ui text lands in C3. The companion-tactics doc hunk skipped (the 1.4.x doc never recorded the defect) | C1, C3 |
| `103636439` | unused weapon preview prefab removed | ui-prefab-validation-gates | Ported: the deletion and `PrefabBrushPropertyTests`. Removing installed copies is a maintainer step; lesson to WP142; RCA skipped | C2 |
| `450332b74` | screens drop settings the game never applied | ui-prefab-validation-gates | Adapted: the test with a v1.4.8 allowlist, the dead-attribute deletions, the four dead screens; the `TroopTypeSelectionPopUp.xml` edit replaced by deleting the file (B3); the popup edit re-done after the re-base; the packager refusal re-implemented (Part D). Release skill hunk to WP143; `v1.5.2-impact.md` skipped; lessons to WP142; RCA skipped | C3, D1 |
| `abb718c2f` | custom battle panels ease in like vanilla's | ui-prefab-validation-gates | Adapted: the ease-value test cases (through the `7012783b3` blob) and the unused `PreBuildCharacterSelection.xml` animation removal. The `EaseOut`/`Quint` panel change and `PanelEaseTests` skipped (no v1.4.8 vanilla referent; UX change); docs, lesson, RCA skipped | C3 |
| `fc932d4ca` | drop eight screen files copied from the base game | gui-prefab-copy-drift | Adapted: the identical-copy test, the eight deletions plus three more identical on 1.4.x, the vanilla-data-comparison wording. Engine-bump skill line to WP143; `v1.5.2-impact.md` skipped; RCA skipped | B3 |
| `7012783b3` | the troop upgrade pop-up shows used horses again | gui-prefab-copy-drift | Adapted: re-based on the installed v1.4.8 file (not v1.5.6); the `DataSource` rule; the attribute-test comment; its moduledata-validation line folded into C4's section. Lesson to WP142; RCA skipped (1.5.x findings) | B2, B3, C3 |
| `540848795` (skipped in group, K2) | the Execute button shows the feud mark | gui-prefab-copy-drift | Adapted (K2): `EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits` only, guarded for a deleted copy, proven green before the deletion. The blood-feud test and prefab edit, HearthRoll hunks, docs skipped (1.5-engine); lesson to WP142 | B1, B3 |
| `42148af4c` (SKIP-FEATURE, K1) | smelting yields capped, Make Tools card | (issue-858 home) | Adapted (K1): `PrefabHintParentGateTests` on the TAOM assembly, floor 5, and one gui-ui sentence. Smith's Measure skipped | A3 |
| `e6dbea3e0` | Fords of War: start on the first tick | tests-gauntlet-stretch-cover-ratchet | Adapted: the ratchet (through `53a4e4e52`) and the AfterStart engine paragraph re-cited on v1.4.8. Every Fords of War file skipped (feature); lesson to WP142; RCA skipped | C4, C5 |
| `d4fb51a50` | name the Gauntlet stretch-in-cover gate | tests-gauntlet-stretch-cover-ratchet | Adapted: a row in the new moduledata-validation section (1.4.x has no GUI row) | C4 |
| `53a4e4e52` | the prefab gate ignores path case | tests-gauntlet-stretch-gate | Ported in its fixed form; the tests.md sentence added fresh. Lesson to WP142; RCA skipped (a 1.5.x checkout incident; the fix arrives pre-applied) | C4 |
| `2c7f7fae9` (WP131) | send-messenger button gets its button look | issue-989 (WP131) | Not here: prerequisite; Step 0.3 checks its file | Step 0 |
| `f1675c058` (WP136) | validator codes and one command for data gates | tools-data-gates-validator-codes (WP136) | Not here: prerequisite; Step 0.3 checks its files and Step C6 adds this plan's names to its `gui-tests` row | Step 0, C6 |
| `498d07e08`, `ed3f911eb`, `0385cc041`, `95a348a60` | later 1.5.x edits to two of these gates | unclassified (newer than `e45f6df2c`) | Not ported; see Maintenance notes | n/a |

## Test plan

- New test results: 37 (A1 2, A2 2, A3 2, B 4, C1 1, C2 2, C3 18, C4 6). Models: the 1.5.x blobs named in each step.
- RED evidence per step as listed; each RED list must match exactly, or the step STOPs.
- Self-tests that prove a rule can fail: `PrefabInsertPositionTests.EachRule_FailsTheMistakeItNames`, the 16
  resolver and visual-attribute tests in `PrefabAttributeNameBindingTests` (every test there except the two
  binding-gate tests), the four `Offenders_*` methods in the ratchet (five results), `PrefabBrushPropertyTests.BrushPropertyLiteral_MatchesTheFirstSegmentCaseSensitively`,
  and the nine `TestStaleModuleFiles` tool tests.
- `tools/tests/test_data_gates.py` (from WP136) stays `OK` after Step C6.
- Structurally untestable (for `Not-tested:`): how the re-based pop-up, the repointed brushes and the
  energy bar draw in the running game; that a deleted copy loads vanilla's identical file at runtime.
- Install-dependent tests (`BindingVerification`, the brush tests) run on this laptop because
  `BANNERLORD_GAME_DIR` is set; without it they read Inconclusive (WP122's strict gate turns that into a failure).

## Done criteria (whole plan)

- [ ] Every Part's done criteria hold.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD` lists only in-scope paths.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD -- CHANGELOG.md Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj TAOM.Tests/TAOM.Tests.csproj .claude/settings.json .claude/skills Main/_Module/ModuleData` prints nothing.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -vc ' v2.0.28 - '` prints `0` (or the current SubModule.xml version).
- [ ] `git log --format=%B $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -c '^Ported-from: '` prints at least `19`.
- [ ] `git log --format=%B $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -c 'Co-Authored-By\|Generated with'` prints `0`.
- [ ] `git status --porcelain` is empty.

## STOP conditions

Stop and report (do not improvise) if:

- A RED run lists anything other than what its step says (each gate's expected output is exact), or a
  gate names a prefab or PrefabExtension this plan does not touch: report the output; do not edit another
  WP's code or prefab.
- K2: `PartyTupleVanillaParityTests` fails while both tuple copies still exist (Step B1).
- K1: `PrefabHintParentGateTests` finds fewer than 5 documents.
- The installed popup's SHA-256 differs from `fdb8a3a6...2130`, `Version.xml` does not name `v1.4.8`, or
  `BANNERLORD_GAME_DIR` is unset (environment: report, do not fix).
- `TAOM.Tests/Infrastructure/RepoPaths.cs` is absent or lacks `RepoPath(params string[] parts)` (Parts B and
  C), `PrefabExtensionBrushTests.cs` is absent or lacks its declared-set method (Part C), or the packager lacks
  `require_build`, `REPO_ROOT` or `_git` (Part D only; Parts A to C can still land).
- `tools/data_gates.py` or `tools/tests/test_data_gates.py` is absent, or `tools/data_gates.py` has no single
  `gui-tests` row (WP136 not merged; the whole plan).
- An identical-copy run lists a twelfth file, or the attribute gate reports more than five pairs outside
  the Step C3 table, or a ratchet count changes for a file no earlier WP touched.
- A `git apply`, `sed` or Edit leaves the expected count unmet twice after a careful retry.
- A step would need an out-of-scope file (`Main/IoC.cs`, `Main/SubModule.cs`, a csproj, `CHANGELOG.md`,
  `.claude/settings*.json`, `.claude/skills/**`, ModuleData).
- A commit hook demands `CHANGELOG.md` (D1 says no plan stages it) or refuses the subject version.
- A build or test fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Deploy a build with this branch merged, remove the 19 deleted files from `<game>/Modules/TAOM/GUI/Prefabs/`
first (or reinstall the module), then check. Watch `rgl_log_*.txt` for `Couldn't find Custom Widget type`,
`builtin widget type not found`, `This prefab has already been added` and `Failed to apply extension` lines
naming a TAOM screen.

1. Party screen, troop upgrade and recruit pop-up: hovering the horse count shows the used-horses tooltip;
   the header columns line up with the rows; TAOM's single centred Done button and the top-right close button
   still work; with a gamepad, the key hints appear beside the hovered row's buttons.
2. Party screen sort bar, composition panel and troop rows; custom battle army sliders, dropdown and troop type
   pop-up; campaign party and settlement nameplates: all unchanged (they now load the identical vanilla files).
3. Battle: the career energy bar sits above the health cluster as before.
4. Battle action bar (if enabled): the formation name and button labels use the tooltip text style.
5. Career screen: the switch-mode subtitle, the no-targets message, career names, descriptions and ability
   names use the career screen styles instead of the default font.
6. Custom battle: the army size slider shows vanilla's track, fill, frame and knob, and the number box has its frame.
7. Inventory: the Presets button label uses the popup button text style.
8. Character creation narrative stage, custom battle screen panels, order-of-battle overlay buttons and the
   party screen header buttons look and behave as before.

## Maintainer steps

- After merging: delete the 19 removed files from every deployed `Modules/TAOM/GUI/Prefabs/` (a deploy never
  deletes). Executors never write to the game install.
- On the desktop (loose Armory and TAOM_Map), once after merging, run the Filtered command with
  `FullyQualifiedName~PrefabExtensionBrushTests`: the source commit's notes ask for both machines. The gate reads only
  TAOM's DependedModules, so the result should equal the laptop's; a difference is a finding to report.
- Next release note (v2.4.0, D8): "If you install over an older TAOM, remove the old `Modules\TAOM` folder
  first, or nineteen old screen files stay and keep replacing the game's own."
- Part D: the next `/release` dry run with `--require-build` will refuse untracked files still in the install.
  On this laptop at planning that includes `Modules/TAOM/ModuleData.zip` and the deleted prefabs
  (`EventXml.xml`, `PreviewPanel.xml`, `PoppyHUD.xml`, `ROTsprite.xml`, `mainmenuHUD.xml` and others): prune them.

## Maintenance notes

- Four 1.5.x commits newer than the planning anchor change two ported gates (`498d07e08` dead visual
  definitions, `ed3f911eb` and `0385cc041` gate holes, all in `PrefabAttributeNameBindingTests.cs`;
  `95a348a60` in the ratchet). They are unclassified; a follow-up WP should classify them before anyone
  "syncs" these files from the 1.5.x tip.
- `3f3af7181` (a SKIP-FEATURE fief commit) added a generic check to `PrefabExtensionBindingTests` that fails an
  XPath matching more than one node (UIExtenderEx patches only the first, verified: `SelectSingleNode`). Not
  in this pack, not ported; all eight 1.4.x XPaths match exactly once today, so it would start green.
- WP136 (`8caac7da5`) also edits `PrefabExtensionBrushTests.cs` (it refactors `DeclaredBrushes` into
  `ModuleFolders`/`GameDir`). WP136 merges first, so Part C edits its result; the two changes touch
  different parts of the file.
- The ratchet's `Known` counts may only fall. When a fix lowers one, lower it in the same change.
- Reviewer focus for `/deep-review`: the B2 diff against the installed v1.4.8 file (exactly three hunks), the
  C3 allowlist (each entry carried by vanilla), the deletions (each loaded by nothing or identical to vanilla),
  and that no ported test still names `TAOM.Client`, `RequiresGameIL` or `LiveInstall`.
- A quick grep found no direct `SpawnAgent` or `AddMissionBehavior` within the first lines of the four 1.4.x
  `AfterStart` overrides (CustomBattleTeamFixBehavior, EnlistmentBattleRoleMissionBehavior,
  EnlistmentMeritMissionBehavior, FieldCommissionMissionLogic). Not audited deeper; mission-start guards are WP109's area.
- Recommended skills for the orchestrator after this WP: `/deep-review` (C# in two or more files),
  `/verify`, and `/verify-bindings` (new BindingVerification tests).
