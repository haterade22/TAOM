# Plan 055: Keep the enlistment menu redirect armed while a soldier in the grace window stands inside a settlement with no encounter

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 3164713da..HEAD -- Main/Features/Enlistment/EnlistmentMenuService.cs Main/Features/Enlistment/IEnlistmentMenuService.cs Main/Features/Enlistment/IEnlistmentConfigProvider.cs Main/Features/Enlistment/DischargeService.cs TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs docs/features/enlistment.md docs/features/return-to-army.md docs/reference/harmony-patch-registry.md Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/ServiceAttachmentService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs Main/Features/Enlistment/TownLeavePolicy.cs Main/Features/Enlistment/Presentation/EnlistmentWaitMenuOptions.cs Main/Features/Enlistment/Hooks/GameMenuManagerSetNextMenuPatch.cs Main/Features/Enlistment/Hooks/MapStateEnterMenuModePatch.cs Main/Adapters/PlayerPresenceFlags.cs Main/Adapters/IMobilePartyAttachmentAdapter.cs Main/Adapters/MobilePartyAttachmentAdapter.cs`
> prints nothing. If an in-scope file changed since this plan was written, compare the "Current
> state" excerpts with the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW (one gate in one service method, reached only in the commander-lost grace window
  while the player is inside a settlement with no `PlayerEncounter`)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `3164713da`, 2026-11-18
- **Baseline at that commit**: build exit 0; dotnet `Failed: 3, Passed: 42911, Skipped: 9, Total:
  42923`, failing: `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles` (the
  localization files are the project lead's and lag the English keys; not this plan's). Python suite
  (not touched by this plan): 10453 ran, 3 failures, all in
  `tools.tests.test_run_long_defeat.PhaseTwoJudgeTests` (three sally tests), 13 skipped.
  `python tools/lint_docs.py --fail-on-drift` exit 0 (measured by the plan writer at 3164713da).
- **Issue**: #511

## Why this matters

An enlisted soldier who follows his commander into a town or castle is placed there with
`EnterSettlementAction.ApplyForParty` alone, so the engine has no `PlayerEncounter` for him. That
state is survivable only because a menu redirect (the "mask") rewrites every vanilla settlement menu
push to TAOM's service wait menu. The redirect is gated on `EnlistedAttached` and nothing else, so it
switches off the moment the commander is captured or loses his party and the state moves to
`CommanderUnavailable` (a grace window of up to 7 campaign days), while the soldier is still
standing inside. In that window any push of the engine's generic state menu lands on
`town_outside` or `castle_outside`, whose init dereferences `PlayerEncounter.EncounterSettlement`
and throws: a crash to desktop of the same class as #510.

After this plan the redirect stays armed in `CommanderUnavailable` while the player is inside a
settlement AND has no `PlayerEncounter`: the crash shape, and the same handling an `EnlistedAttached`
soldier in that encounter-less shape already gets. A grace player who walks into a town himself
arrives with a vanilla settlement encounter, so his menus are safe and stay vanilla's. Nothing
changes outside a settlement.

## Current state

### Files

- `Main/Features/Enlistment/EnlistmentMenuService.cs`: the single menu-guard policy. The bug is the
  state gate at lines 112 to 113 of `TryRedirectMenu`.
- `TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs`: its unit tests; the new tests go
  here. Model them on the existing `TryRedirectMenu_*` tests in this file.
- `Main/Features/Enlistment/IEnlistmentMenuService.cs`: the interface; its summary states the policy
  ("redirect ONLY while EnlistedAttached") and must be amended.
- `Main/Features/Enlistment/IEnlistmentConfigProvider.cs`: `RedirectMenuIds` doc comment states the
  same policy; amend.
- `Main/Features/Enlistment/DischargeService.cs`: a comment at lines 251 to 252 states the gate;
  amend.
- `docs/features/enlistment.md`: lines 97, 120 to 121 and the section "Known gap: the mask still
  disarms outside `EnlistedAttached` (#511)" at lines 2104 to 2129 describe the gate; amend.
- `docs/features/return-to-army.md`: lines 184 to 186 say "the redirect is gated on
  `EnlistedAttached` before the list is read"; amend.
- `docs/reference/harmony-patch-registry.md`: line 620 says "Redirect fires ONLY in
  `EnlistedAttached`"; amend.

All eight files have CRLF line endings. Edit them with the Edit tool only; never `sed -i`.

### The gate (EnlistmentMenuService.cs at 3164713da)

Constructor fields, lines 13 to 18 (the attachment adapter is already injected; no constructor or
IoC change is needed):

```csharp
    private readonly IEnlistmentStore _store;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IEncounterAdapter _encounter;
    private readonly Content.IEnlistmentContentConfigProvider _contentConfig;
    private readonly SitOutLedger _ledger;
    private readonly IModLogger _logger;
```

`TryRedirectMenu`, lines 106 to 147:

```csharp
    public bool TryRedirectMenu(string requestedMenuId, out string redirectedMenuId)
    {
        redirectedMenuId = null;
        if (string.IsNullOrEmpty(requestedMenuId) || requestedMenuId == ServiceWaitMenuId)
            return false;

        if (_store.Record.State != EnlistmentState.EnlistedAttached)
            return false;

        // NEVER redirect a battle menu while the player is genuinely IN a map event.
        ...
        if (IsBattleMenu(requestedMenuId) && _attachment.GetPresenceFlags().IsInMapEvent)
        {
            _logger?.LogInfo($"[EnlistDiag] not redirecting '{requestedMenuId}' — the player is in a live map event and this menu is the only thing that advances it");
            return false;
        }

        // SHORE LEAVE (field report 1). ...
        if (_store.Record.OnTownLeave && IsSettlementMenu(requestedMenuId))
        {
            _logger?.LogInfo($"[Enlistment] shore leave — '{requestedMenuId}' left to the player");
            return false;
        }

        if (_redirectIds.Contains(requestedMenuId))
        {
            redirectedMenuId = ServiceWaitMenuId;
            return true;
        }
        ...
```

`IsBattleMenu` (line 160) is `menuId == "encounter" || menuId == "join_encounter"`; `IsSettlementMenu`
(line 170) is `menuId == "town" || menuId == "castle" || menuId == "village"`.

The default redirect list (`IEnlistmentConfigProvider.cs` lines 43 to 68): `army_wait`,
`army_wait_at_settlement`, `town_wait`, `town_wait_menus`, `village_wait_menus`, `town_outside`,
`castle_outside`, `join_encounter`, `encounter`, `port_menu`, `naval_town_outside`, `town`, `castle`,
`village`.

`PlayerPresenceFlags` (`Main/Adapters/PlayerPresenceFlags.cs`) is a `readonly struct`; its default
value has `SettlementId == null`, so `IsInSettlement` is false. Its constructor is
`PlayerPresenceFlags(bool mainPartyExists, bool isCaptive = false, bool isActive = false, bool
isVisible = false, bool isInMapEvent = false, bool hasPlayerEncounter = false, string settlementId =
null)`; `public bool HasPlayerEncounter { get; }` is line 21 and `public bool IsInSettlement =>
!string.IsNullOrEmpty(SettlementId);` is line 27. The adapter method is
`PlayerPresenceFlags GetPresenceFlags();` on `IMobilePartyAttachmentAdapter` (line 40), an
allocation-free read of `MobileParty.MainParty` fields (`MobilePartyAttachmentAdapter.cs` lines 210
to 232, with `hasPlayerEncounter: PlayerEncounter.Current != null` and
`settlementId: main.CurrentSettlement?.StringId`).

### How the player reaches the crash shape

1. Follow path, `ServiceAttachmentService.FollowCommanderIntoSettlement` (lines 190 to 236):
   `RestorePresence()`, then `_attachment.MoveIntoSettlement(settlementId)`, which is
   `EnterSettlementAction.ApplyForParty(main, settlement);` (`MobilePartyAttachmentAdapter.cs` line
   322), then `EnsureMenuOpen(ServiceWaitMenuId)`. No `PlayerEncounter` is created.
2. The commander is lost: `ServiceAttachmentService.Assess` returns `Blocked /
   CommanderPartyMissing` when `commander == null || !commander.IsFit` (lines 86 to 89), and that
   check sits above every settlement rule. `EnlistmentReconciler.ReconcileBlocked`, lines 846 to 861:

   ```csharp
            case AttachmentBlockReason.CommanderPartyMissing:
                if (!snapshot.Exists || !snapshot.IsAlive)
                {
                    _discharge.Execute(DischargeReason.CommanderDead);
                    return;
                }

                // EnlistedBattle -> CommanderUnavailable is deliberately illegal; the
                // transition is retried after the battle resolves back to attached.
                if (_machine.TryTransition(EnlistmentState.CommanderUnavailable))
                {
                    record.GraceEndsAtDay = GraceDeadline(nowDays);
                    _attachment.RestorePresence();
                    ...
   ```

   `RestorePresence` (`MobilePartyAttachmentAdapter.cs` lines 90 to 121) sets `IsActive = true`,
   `IsVisible = true` and clears the army attachment; it never leaves the settlement.
3. `EnlistmentReconciler.ReconcileGrace` (lines 416 to 472) sweeps stranded encounters, discharges on
   a dead commander, returns to `EnlistedAttached` on recovery, and discharges at the deadline. It
   never moves the player out of a settlement. So the player stays inside with no encounter for the
   whole grace window.

`ServiceMaintenanceService.EnsureServiceMenu` (lines 484 to 539) has its own `EnlistedAttached` gate
at lines 497 to 498. This plan does NOT change it (see "Choice of fix").

### A grace player can also enter a settlement himself (the shape the fix must NOT mask)

In grace the player is not held. A commander lost while the column is on the map leaves him visible
and active on the map (`RestorePresence`, above). A shore-leave pass is revoked on any state change:
`TownLeavePolicy.ShouldRevokeLeave` (`TownLeavePolicy.cs` line 52) is
`onLeave && !(state == EnlistmentState.EnlistedAttached && insideSettlement)`, and the revoke in
`ServiceMaintenanceService.cs` lines 201 to 240 finishes the pass's settlement encounter and walks
him out. `EnsureServiceMenu` does not re-assert the wait menu outside `EnlistedAttached`. So a grace
player can click a friendly town or castle on the map, and vanilla runs (v1.5.7):

- `EncounterManager.StartSettlementEncounter` (`TaleWorlds.CampaignSystem.EncounterManager.cs` line
  101): `PlayerEncounter.Start();` at line 154 (which is
  `Campaign.Current.PlayerEncounter = new PlayerEncounter();`, `PlayerEncounter.cs` line 969), then
  `PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);` at line 183 or
  188.
- `PlayerEncounter.Init` (`PlayerEncounter.cs` line 648) computes `encounterMenu` at line 669
  (`town_outside` or `castle_outside` for a friendly settlement), calls `EnterSettlement();` at line
  708 (`EnterSettlementAction.ApplyForParty`, line 1595), and only then
  `GameMenu.ActivateGameMenu(encounterMenu);` at line 710.

At the `SetNextMenu` prefix, therefore, `IsInSettlement` is already true AND `PlayerEncounter.Current`
is set. A gate on `IsInSettlement` alone would rewrite that visit to the service wait menu, whose only
option while serving is "Ask to be released from service"
(`Presentation/EnlistmentWaitMenuOptions.cs` lines 102 to 108; the "Go on your way" leave option at
lines 117 to 123 shows only when nobody is serving). He would be held there with a live settlement
encounter, the shape `ServiceMaintenanceService.cs` lines 206 to 211 says nothing else takes down,
for up to 7 days. The encounter term in this plan's gate (`!HasPlayerEncounter`) keeps that visit
vanilla. With an encounter the approach menus are safe: their init reads
`PlayerEncounter.EncounterSettlement`, which the encounter carries.

### Engine facts (v1.5.7, from `pwsh tools/taom-src.ps1 path <Type>`, read by the plan writer)

- `Campaign.Tick`, `TaleWorlds.CampaignSystem.Campaign.cs` lines 1012 to 1019, pushes the generic
  state menu on every tick the map is not at a menu:

  ```csharp
		if (Game.Current.GameStateManager.ActiveState is MapState { AtMenu: false })
		{
			string genericStateMenu = Models.EncounterGameMenuModel.GetGenericStateMenu();
			if (!string.IsNullOrEmpty(genericStateMenu))
			{
				GameMenu.ActivateGameMenu(genericStateMenu);
			}
		}
  ```

  `MapState.AtMenu => MenuContext != null` (`MapState.cs` line 62).
- `DefaultEncounterGameMenuModel.GetGenericStateMenu()` (lines 215 onward). For a main party with no
  map event, no besieger camp, no `AttachedTo`, inside a fortification with no siege event and no
  settlement map event, and no `Army` led by another party, it reaches lines 299 to 311:

  ```csharp
				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting && currentSettlement.IsFortification)
				{
					return "town_wait_menus";
				}
				if (currentSettlement.IsCastle)
				{
					return "castle_outside";
				}
				if (MobileParty.MainParty.IsCurrentlyAtSea)
				{
					return "naval_town_outside";
				}
				return "town_outside";
  ```

  For a party inside a village the `CurrentSettlement != null` branch has no village case, the
  `else if (Settlement.CurrentSettlement != null)` branch is skipped, and the method returns `null`.
- Every vanilla menu change goes through `GameMenuManager.SetNextMenu`, where the redirect prefix
  sits: `GameMenu.ActivateGameMenu` (`GameMenu.cs` lines 345 to 366) calls
  `Campaign.Current.GameMenuManager.SetNextMenu(menuId)` when there is no menu context, otherwise
  `SwitchToMenu(menuId)`; `GameMenu.SwitchToMenu` (lines 368 to 384) calls
  `currentMenuContext.SwitchToMenu(menuId)`; `MenuContext.SwitchToMenu` (`MenuContext.cs` lines 56
  to 60) is `Campaign.Current.GameMenuManager.SetNextMenu(menuId); HandleStates();`.
  `GameMenuManager.SetNextMenu` (lines 42 to 45) is `NextGameMenuId = name;`.
- The crash: `EncounterGameMenuBehavior.game_menu_town_outside_on_init` (lines 2279 to 2283) and
  `game_menu_castle_outside_on_init` (lines 2372 to 2377) both open with
  `Settlement encounterSettlement = PlayerEncounter.EncounterSettlement; args.MenuTitle =
  encounterSettlement.Name;`, and `PlayerEncounter.EncounterSettlement =>
  Current?.EncounterSettlementAux;` (`PlayerEncounter.cs` line 346). With no encounter that is a
  `NullReferenceException` at menu init. The town menu's wait path has the same dereference:
  `PlayerTownVisitCampaignBehavior.game_menu_settlement_wait_on_init` (line 198) reads
  `PlayerEncounter.EncounterSettlement.IsVillage`.
- Immobility inside a settlement: `MobileParty.DoUpdatePosition` (`MobileParty.cs` line 3993) has
  `if (BesiegedSettlement != null || CurrentSettlement != null || ...) return;`.

Correction to the issue body, already recorded in the issue's own comment and in
`docs/features/enlistment.md`: the hazard is a crash at `town_outside` init, not a soft-lock at its
Leave option.

### Hooks that call the policy (no change)

- `Main/Features/Enlistment/Hooks/GameMenuManagerSetNextMenuPatch.cs` line 29: prefix on
  `GameMenuManager.SetNextMenu`, `if (_menuService.TryRedirectMenu(name, out var redirected)) name =
  redirected;`, fails open.
- `Main/Features/Enlistment/Hooks/MapStateEnterMenuModePatch.cs` line 27: postfix on
  `MapState.EnterMenuMode`, switches to the wait menu when `TryRedirectMenu(__instance.GameMenuId,
  ...)` is true; covers ids written straight to `MapStateData.GameMenuId` (the load path).

### Blast radius

`python tools/graphify_taom.py status --brief` at 3164713da in the plan writer's worktree reported
`graphify code graph: MISSING`, so callers come from `git grep -n -w`:

- `TryRedirectMenu` callers: the two hooks above, and `TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs`.
- `EnlistmentMenuService` is constructed only by the container (`Main/Features/Enlistment/EnlistmentIoC.cs`
  line 54, `container.Register<IEnlistmentMenuService, EnlistmentMenuService>(Reuse.Singleton);`)
  and by the test fixture (line 30). Every other `EnlistmentMenuService` hit is the
  `ServiceWaitMenuId` constant.
- `IEnlistmentMenuService` substitutes in `ServiceMaintenanceServiceTests`,
  `EnlistmentPumpAuthorityTests`, `EncounterGameMenuOrderAttackConditionPatchTests` and
  `MapEventHelperCanMainPartyLeaveBattlePatchTests` are unaffected: no signature changes.

### Existing test that already pins the outside-settlement case

`EnlistmentMenuServiceTests.cs` lines 276 to 289:

```csharp
    [DataTestMethod]
    [DataRow(EnlistmentState.EnlistedBattle)]
    [DataRow(EnlistmentState.EnlistedDetachedOnDuty)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.CommanderUnavailable)]
    public void TryRedirect_NonAttachedServiceStates_NeverRedirect(EnlistmentState state)
    {
        // EnlistedBattle: the battle service transitions BEFORE pushing encounter menus,
        // so battle menus flow through. Duty/captive/grace: the player roams free.
        MakeEnlisted(state);

        Assert.IsFalse(_service.TryRedirectMenu("encounter", out _));
        Assert.IsFalse(_service.TryRedirectMenu("army_wait", out _));
    }
```

The fixture builds the attachment substitute inline (line 30) and never sets `GetPresenceFlags`, so
it returns `default(PlayerPresenceFlags)`: not in a settlement. This test stays green after the fix
and is the outside-settlement pin; only its comment changes. `MakeEnlisted` (line 233) sets the
state and the hero ids and does not touch `OnTownLeave`.

### Choice of fix (simplicity criterion)

The issue offers two fixes.

1. **Keep the redirect armed in `CommanderUnavailable` while the player is inside a settlement with
   no `PlayerEncounter`** (chosen). Win: the crash shape, and only the crash shape, is masked with
   the handling an `EnlistedAttached` soldier in the same encounter-less shape already has; a grace
   player's own town visit (which carries an encounter) stays vanilla; saves already in the shape are
   covered on load (the `EnterMenuMode` postfix and the next `Campaign.Tick` push both reach the
   redirect). Cost: one condition and one private helper in one method, one presence read only in
   that state. Verdict: Keep.
2. **Walk the player out of the settlement when the state changes** (rejected). It changes
   gameplay (the soldier waits out up to 7 days visible on the open map instead of inside the
   walls), it does not repair a save already in the shape unless the grace tick also does it, and
   `LeaveSettlementAction` fires settlement-left events that other listeners act on. A behaviour
   change beyond removing the crash is proposed, never applied (docs/goals.md standing call).

Narrower calls that also hold:

- Gate on `IsInSettlement && !HasPlayerEncounter`, not `IsInSettlement` alone: see "A grace player
  can also enter a settlement himself". Step 3 proves the encounter term with its own mutation run.
- Do NOT widen the gate to `IsEnlisted`. `EnlistedBattle` needs the battle menus; captivity menus are
  vanilla's; `Discharging` runs inside `DischargeService.Execute`, which sets `NotEnlisted` and builds
  a settlement encounter before it opens a settlement menu (`DischargeService.cs` lines 150 to 154
  and 240 to 258).
- Do NOT change `EnsureServiceMenu`. The redirect alone catches every push (engine facts above);
  the pump's gate only stops the wait menu being re-asserted, it never tears one down.

The lesson `docs/reviews/lessons/state-lifecycle-save.md` "A redirect list is a MASK over an invalid
state" says the real fix is the state itself (a settlement encounter on the follow path). That fix
collides with the battle path's encounter-ownership rule R3 (issue #511 body, candidate 1) and stays
out of scope; this plan closes the unmasked window and records the remaining village case.

### Conventions that bind this change

- ADR-002 (`docs/adrs/002-thin-entry-points.md`): entry points under 150 lines delegate to services;
  the two hooks stay unchanged.
- ADR-007 (`docs/adrs/007-adapter-pattern.md`): services take adapters, never sealed TaleWorlds
  types. The new code reads `IMobilePartyAttachmentAdapter.GetPresenceFlags()`, already injected.
  Gate: `ServiceSignatureGateTests`.
- ADR-008 (`docs/adrs/008-testability-requirements.md`): services are unit testable with no game; no
  static TaleWorlds call in the service.
- ADR-003 / ADR-004 / ADR-005: no `#region`, no `[Obsolete]`, no `#if`. Gate:
  `BannedConstructsGateTests`.
- `.claude/rules/csharp-architecture.md`: constructor injection only; every decision path has a test
  (two-edit test).
- `.claude/rules/tests.md`: MSTest + NSubstitute, test names `MethodName_StateUnderTest_ExpectedBehavior`;
  RED before GREEN; skip-guard exhaustion (one test per skipped state).
- `.claude/rules/traps/campaign.md` row "Settlement menus": settlement menus need an encounter.
- Prose you write (comments, docs, commit body): no em or en dash (U+2014, U+2013). Existing log
  strings you do not edit may keep theirs.

## Commands you will need

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's 3 failures and no other, Passed = 42911 + the new results |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EnlistmentMenuServiceTests"` | the class's results only; record the count |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Never `./build.ps1`: it deploys into the game install. Python tools are not touched; the Python
suite is not required.

## Scope

**In scope** (the only files you modify):
- `Main/Features/Enlistment/EnlistmentMenuService.cs`
- `TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs`
- `Main/Features/Enlistment/IEnlistmentMenuService.cs` (summary comment only)
- `Main/Features/Enlistment/IEnlistmentConfigProvider.cs` (doc comment only)
- `Main/Features/Enlistment/DischargeService.cs` (comment at lines 251 to 252 only)
- `docs/features/enlistment.md`
- `docs/features/return-to-army.md` (lines 185 to 186, one sentence)
- `docs/reference/harmony-patch-registry.md` (line 620, one sentence)

**Out of scope** (do NOT touch, even though they look related):
- `Main/Features/Enlistment/ServiceMaintenanceService.cs` (`EnsureServiceMenu` keeps its gate),
  `TownLeavePolicy.cs`, `Presentation/EnlistmentWaitMenuOptions.cs`.
- `Main/Features/Enlistment/EnlistmentReconciler.cs`, `ServiceAttachmentService.cs`,
  `Main/Adapters/*` (no walk-out, no follow-path encounter; `HasPlayerEncounter` already exists).
- The `RedirectMenuIds` list itself (`IEnlistmentConfigProvider.cs` lines 43 to 68): no id added or
  removed.
- The unknown-id log line at `EnlistmentMenuService.cs` line 155 ("pushed while attached"): leave
  it; a wording change is not needed for the fix.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Main/Features/Enlistment/EnlistmentIoC.cs`:
  no registration change is needed. If you think one is, STOP.
- `CHANGELOG.md`, `plans/README.md`, any `docs/adrs/*.md`, `.claude/settings*.json`,
  `Directory.Build.props`.
- Historical narrative that was true when written: `docs/features/enlistment.md` lines 500 to 512
  and 1932 to 1942, `docs/reviews/**`, `docs/changelog-archive/**`.
- The gates themselves: never delete, `[Ignore]` or loosen a test, and never edit the three failing
  localization tests or the language files.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `fix(enlistment): <version> - keep the redirect inside a town in grace`, where `<version>`
  is the `value` of `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.40` at
  3164713da; then the subject is 67 characters). At most 72 characters. A hook refuses any other
  version.
- Body wrapped at 72, written for a reader of the release note; no `CHANGELOG.md` edit; no AI
  attribution trailer. Write the message to a file in your scratch folder and run
  `git commit -F "<file>"`. Never `--no-verify`.
- Stage the in-scope paths explicitly, by name.

## Steps

### Step 1: record the base

Run, in your worktree:

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (full suite; may take several
   minutes, run it under a 15 minute timeout)
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EnlistmentMenuServiceTests"`
4. `python tools/lint_docs.py --fail-on-drift`

Write all four results into your report: the build exit code, the full-suite totals line and its
failing test names, the filtered totals line (call its Passed count `B`; the plan writer's count of
the class's results at 3164713da is 54, if each `DataRow` counts as one result), and the lint exit
code.

**Verify**: build exit 0; full suite `Failed: 3, Passed: 42911, Skipped: 9, Total: 42923` with
exactly the three localization tests named in Status failing; filtered run `Failed: 0`; lint exit 0.
A difference you cannot explain is a STOP condition.

### Step 2: write the failing tests (RED)

Edit `TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs` with the Edit tool.

2a. Keep the attachment substitute in a field so tests can set the presence. Add the field after
line 19 (`private SitOutLedger _ledger = null!;`):

```csharp
    private IMobilePartyAttachmentAdapter _attachment = null!;
```

and replace the constructor line 30:

```csharp
        _service = new EnlistmentMenuService(_store, new EnlistmentConfigProvider(), Substitute.For<IMobilePartyAttachmentAdapter>(), _encounter, _contentConfig, _ledger, _logger);
```

with:

```csharp
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _service = new EnlistmentMenuService(_store, new EnlistmentConfigProvider(), _attachment, _encounter, _contentConfig, _ledger, _logger);
```

2b. Replace the comment inside `TryRedirect_NonAttachedServiceStates_NeverRedirect` (lines 283 to
284):

```csharp
        // EnlistedBattle: the battle service transitions BEFORE pushing encounter menus,
        // so battle menus flow through. Duty/captive/grace: the player roams free.
```

with:

```csharp
        // EnlistedBattle: the battle service transitions BEFORE pushing encounter menus,
        // so battle menus flow through. Duty, captivity, and grace outside a settlement: the
        // player's menus flow. Grace inside a settlement with no encounter is #511, pinned below.
```

2c. Append these tests at the end of the class, before its closing `}` (after
`TryRedirectMenu_OnShoreLeave_ApproachMenusStillRedirect`):

```csharp
    // ---- the commander-lost grace window inside a settlement (#511) ----

    private void PlaceInsideSettlement(bool inMapEvent = false, bool hasPlayerEncounter = false) =>
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true,
            isInMapEvent: inMapEvent, hasPlayerEncounter: hasPlayerEncounter, settlementId: "town_A1"));

    [DataTestMethod]
    [DataRow("town_outside")]
    [DataRow("castle_outside")]
    [DataRow("naval_town_outside")]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_CommanderUnavailableInsideSettlement_RedirectsToWaitMenu(string menuId)
    {
        // The follow path placed the player with EnterSettlementAction alone, so there is no
        // PlayerEncounter. Campaign.Tick pushes town_outside or castle_outside for that shape, and
        // both read PlayerEncounter.EncounterSettlement.Name at init. Losing the commander must not
        // lift the mask while the player still stands inside.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        PlaceInsideSettlement();

        Assert.IsTrue(_service.TryRedirectMenu(menuId, out var redirected),
            $"'{menuId}' must redirect in the grace window inside a settlement");
        Assert.AreEqual(EnlistmentMenuService.ServiceWaitMenuId, redirected);
    }

    [DataTestMethod]
    [DataRow("town_outside")]
    [DataRow("castle_outside")]
    [DataRow("naval_town_outside")]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_CommanderUnavailableInsideSettlementWithEncounter_NeverRedirects(string menuId)
    {
        // A grace player is loose on the map and may walk into a town himself. Vanilla
        // StartSettlementEncounter starts a PlayerEncounter first, and PlayerEncounter.Init enters
        // the settlement before it activates town_outside, so that push arrives inside WITH an
        // encounter. Its menus are safe; masking them would hold him in the wait menu.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        PlaceInsideSettlement(hasPlayerEncounter: true);

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out var redirected),
            $"'{menuId}' must flow for a grace player who entered with his own encounter");
        Assert.IsNull(redirected);
    }

    [DataTestMethod]
    [DataRow("town_outside")]
    [DataRow("castle_outside")]
    [DataRow("town")]
    [DataRow("encounter")]
    [DataRow("army_wait")]
    public void TryRedirectMenu_CommanderUnavailableOutsideSettlement_NeverRedirects(string menuId)
    {
        // Outside a settlement the grace window keeps its old behaviour: every menu flows.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true));

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out var redirected));
        Assert.IsNull(redirected);
    }

    [DataTestMethod]
    [DataRow("encounter")]
    [DataRow("join_encounter")]
    public void TryRedirectMenu_CommanderUnavailableInsideSettlementInMapEvent_LetsBattleMenusThrough(string menuId)
    {
        // The battle exception outranks the grace mask: the encounter menu is the only thing that
        // advances the player's own map event.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        PlaceInsideSettlement(inMapEvent: true);

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out _));
    }

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_CommanderUnavailableOnShoreLeaveInsideSettlement_SettlementMenusPassThrough(string menuId)
    {
        // The shore-leave exception outranks the grace mask too. No encounter is set here, so the
        // grace gate is armed and only the exception can let these menus through.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.OnTownLeave = true;
        PlaceInsideSettlement();

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out _));
    }

    [TestMethod]
    public void TryRedirectMenu_OtherNonAttachedStatesInsideSettlement_NeverRedirect()
    {
        // Narrow on purpose: not widened to IsEnlisted. Battle needs its menus, captivity is
        // vanilla's, and a discharge builds its own settlement encounter.
        PlaceInsideSettlement();
        foreach (EnlistmentState state in System.Enum.GetValues(typeof(EnlistmentState)))
        {
            if (state == EnlistmentState.EnlistedAttached || state == EnlistmentState.CommanderUnavailable)
                continue;

            MakeEnlisted(state);

            Assert.IsFalse(_service.TryRedirectMenu("town_outside", out _), $"state {state} must not redirect town_outside");
            Assert.IsFalse(_service.TryRedirectMenu("town", out _), $"state {state} must not redirect town");
        }
    }

    [TestMethod]
    public void TryRedirectMenu_EveryStateButCommanderUnavailable_DoesNotReadPresenceForANonBattleMenu()
    {
        // Hot path: the SetNextMenu prefix runs on every menu change. Only the grace window pays
        // for a presence read on a non-battle menu.
        foreach (EnlistmentState state in System.Enum.GetValues(typeof(EnlistmentState)))
        {
            if (state == EnlistmentState.CommanderUnavailable)
                continue;

            MakeEnlisted(state);
            _attachment.ClearReceivedCalls();

            _service.TryRedirectMenu("town_outside", out _);

            _attachment.DidNotReceive().GetPresenceFlags();
        }
    }
```

That is 24 new results: 6 + 6 + 5 + 2 + 3 + 1 + 1.

Run the filtered class.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EnlistmentMenuServiceTests"`
compiles (no `error CS` lines) and reports `Failed: 6`, `Passed: B + 18`. The six failures are all
`TryRedirectMenu_CommanderUnavailableInsideSettlement_RedirectsToWaitMenu`, one per menu id, each an
`Assert.IsTrue failed` carrying the message "must redirect in the grace window inside a
settlement". Quote the totals line and the six failing names in your report. Any other failing test,
or a compile error, is a STOP condition after one reasonable fix.

The six other new tests pass at the base on purpose: they pin the behaviour the fix must keep (a
self-entered visit, outside a settlement, the battle and shore-leave exceptions, the other states,
the hot path). Step 3 shows the self-entry and outside-settlement pins failing against wrong fixes.

### Step 3: implement the gate (GREEN), proving each term first

3a. Mutation proof of the settlement term. In `Main/Features/Enlistment/EnlistmentMenuService.cs`,
replace lines 112 to 113:

```csharp
        if (_store.Record.State != EnlistmentState.EnlistedAttached)
            return false;
```

with the naive widening (temporary):

```csharp
        var state = _store.Record.State;
        if (state != EnlistmentState.EnlistedAttached && state != EnlistmentState.CommanderUnavailable)
            return false;
```

Run the filtered class. **Verify**: `Failed: 12`, `Passed: B + 12`. The six `..._RedirectsToWaitMenu`
results now pass, and these fail: all five rows of
`TryRedirectMenu_CommanderUnavailableOutsideSettlement_NeverRedirects`, the `CommanderUnavailable`
row of `TryRedirect_NonAttachedServiceStates_NeverRedirect`, and all six rows of
`TryRedirectMenu_CommanderUnavailableInsideSettlementWithEncounter_NeverRedirects`. Quote the totals
line. This proves the outside-settlement pins bite.

3b. Mutation proof of the encounter term. Replace the naive gate line

```csharp
        if (state != EnlistmentState.EnlistedAttached && state != EnlistmentState.CommanderUnavailable)
```

with

```csharp
        if (state != EnlistmentState.EnlistedAttached && !IsGraceInsideSettlementWithoutEncounter(state))
```

(keep the `var state` line above it and the `return false;` below it), and add this private helper
directly after the `IsSettlementMenu` method (after line 171), inside the class. Its last line is
temporarily missing the encounter term:

```csharp
    /// <summary>
    /// #511. The commander-lost grace window keeps the mask while the player stands inside a
    /// settlement with no PlayerEncounter. The follow path put him there with EnterSettlementAction
    /// alone, and the approach menus Campaign.Tick pushes for that shape (town_outside,
    /// castle_outside) read PlayerEncounter.EncounterSettlement at init and throw. A grace player
    /// who enters a town himself arrives WITH an encounter (StartSettlementEncounter starts it
    /// before PlayerEncounter.Init enters the settlement and opens town_outside), so his visit stays
    /// vanilla. Outside a settlement the grace player's menus flow as before. The state check comes
    /// first, so no other state pays for the presence read.
    /// </summary>
    private bool IsGraceInsideSettlementWithoutEncounter(EnlistmentState state)
    {
        if (state != EnlistmentState.CommanderUnavailable)
            return false;

        var presence = _attachment.GetPresenceFlags();
        return presence.IsInSettlement;
    }
```

Run the filtered class. **Verify**: `Failed: 6`, `Passed: B + 18`, and the six failures are exactly
the rows of `TryRedirectMenu_CommanderUnavailableInsideSettlementWithEncounter_NeverRedirects`, each
with the message "must flow for a grace player who entered with his own encounter". Quote the
totals line. This proves the self-entry pin bites.

3c. Replace the helper's last line

```csharp
        return presence.IsInSettlement;
```

with

```csharp
        return presence.IsInSettlement && !presence.HasPlayerEncounter;
```

Leave the battle exception, the shore-leave exception and the id list below the gate exactly as
they are: they now apply to the grace case too, which is what the pins require.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0, then the
filtered class reports `Failed: 0, Passed: B + 24`. Quote both.

### Step 4: amend every statement of the old policy

Use the Edit tool for each. Supplied text is a draft: re-read each claim against the code you just
wrote before you keep it.

4a. `Main/Features/Enlistment/IEnlistmentMenuService.cs` lines 3 to 6. Replace:

```csharp
/// <summary>
/// The single menu-guard authority. Consulted by the Patch66 SetNextMenu prefix on every
/// menu transition (hot path — decisions are a state read + one HashSet probe).
/// Policy: redirect ONLY while EnlistedAttached. EnlistedBattle deliberately lets battle
```

with:

```csharp
/// <summary>
/// The single menu-guard authority. Consulted by the Patch66 SetNextMenu prefix on every
/// menu transition (hot path: decisions are a state read + one HashSet probe, plus one presence
/// read in the grace window only).
/// Policy: redirect while EnlistedAttached, and while CommanderUnavailable with the player inside
/// a settlement with no PlayerEncounter (#511: the follow path left him there without one, and the
/// approach menus the engine pushes for that shape throw at init). EnlistedBattle deliberately lets battle
```

(the rest of the summary, from "menus through" on, is unchanged).

4b. `Main/Features/Enlistment/IEnlistmentConfigProvider.cs` line 39. Replace:

```csharp
    /// Native menu ids rewritten to the service wait menu while EnlistedAttached. Seeded
```

with:

```csharp
    /// Native menu ids rewritten to the service wait menu while EnlistedAttached, and in
    /// CommanderUnavailable while the player is inside a settlement with no PlayerEncounter
    /// (#511). Seeded
```

4c. `Main/Features/Enlistment/DischargeService.cs` lines 251 to 252. Replace:

```csharp
            // The redirect list cannot cover this: it is gated on EnlistedAttached, and step 8 has
            // already set the record to NotEnlisted by the time we get here. That is the whole bug.
```

with:

```csharp
            // The redirect list cannot cover this: it is gated on EnlistedAttached (and on
            // CommanderUnavailable inside a settlement with no encounter, #511), and step 8 has
            // already set the record to NotEnlisted by the time we get here. That is the whole bug.
```

4d. `docs/features/enlistment.md` line 97. Replace:

```
do not repair it: `TryRedirectMenu` returns false for the service id and for any non-attached state.
```

with:

```
do not repair it: `TryRedirectMenu` returns false for the service id, and in every state but
`EnlistedAttached` and the grace window inside a settlement with no encounter (#511).
```

4e. `docs/features/enlistment.md` lines 120 to 121. Replace:

```
  feeds it otherwise). Menu-guard policy in `EnlistmentMenuService`: redirect ONLY while
  `EnlistedAttached`, from a config-driven id list (11 defaults incl. the 1.4.x naval
```

with:

```
  feeds it otherwise). Menu-guard policy in `EnlistmentMenuService`: redirect while
  `EnlistedAttached`, and in `CommanderUnavailable` while the player is inside a settlement with
  no `PlayerEncounter` (#511), from a config-driven id list (11 defaults incl. the 1.4.x naval
```

4f. `docs/features/enlistment.md`: replace the whole section from the heading
`### Known gap: the mask still disarms outside `EnlistedAttached` (#511)` (line 2104) up to and not
including the next heading `## A battle ending on the far side of the map tore the player out of his own (#551, 2026-09-06)`
(line 2131) with the text below, keeping one blank line before that next heading:

```markdown
### The mask stays on in the grace window inside a settlement with no encounter (#511, 2026-11-18)

The follow path places the player inside the commander's settlement with
`EnterSettlementAction.ApplyForParty` alone, so there is no `PlayerEncounter`. When the commander is
captured or loses his party during that stop, `ReconcileBlocked` moves the state to
`CommanderUnavailable` and calls `RestorePresence()`, which leaves the player inside, and the grace
path never walks him out. Until this fix `TryRedirectMenu` returned false in every state but
`EnlistedAttached`, so the mask was off for the whole grace window. `Campaign.Tick` pushes the
generic state menu on every tick the map is not at a menu, and for an encounter-less party inside a
fortification that is `town_outside` or `castle_outside`
(`DefaultEncounterGameMenuModel.GetGenericStateMenu`, v1.5.7). Both menus open with
`args.MenuTitle = PlayerEncounter.EncounterSettlement.Name`, and `EncounterSettlement` is
`Current?.EncounterSettlementAux`, so with no encounter the init throws inside `GameMenu.RunOnInit`:
a CTD of the #510 class, not a soft-lock.

`TryRedirectMenu` now also redirects in `CommanderUnavailable` while the player is inside a
settlement with no `PlayerEncounter` (`GetPresenceFlags()`: `IsInSettlement` and not
`HasPlayerEncounter`). That is the handling an `EnlistedAttached` player in the same encounter-less
shape already had, and the battle and shore-leave exceptions still apply first. Every vanilla menu
change reaches the redirect: `GameMenu.ActivateGameMenu` calls `GameMenuManager.SetNextMenu` or
`GameMenu.SwitchToMenu`, and `MenuContext.SwitchToMenu` calls `SetNextMenu` (v1.5.7). Pinned by the
`TryRedirectMenu_CommanderUnavailable*` tests in `EnlistmentMenuServiceTests`.

The encounter term keeps the grace player's own town visits vanilla. In grace he can be loose on the
map (a shore-leave pass is revoked on the state change and walks him out), and he may then enter a
town or castle himself. `EncounterManager.StartSettlementEncounter` starts a `PlayerEncounter`
first, and `PlayerEncounter.Init` enters the settlement before it activates `town_outside` or
`castle_outside` (v1.5.7), so that push arrives inside a settlement with an encounter. Those menus
are safe. Masking them would hold him in the wait menu, with a live settlement encounter and only
"Ask to be released" as an exit, for the rest of the grace window. Outside a settlement the grace
player's menus flow as before.

Rejected: walking the player out of the settlement when the state changes. He would wait out the
grace window visible on the open map instead of inside the walls, a save already in the shape would
not be repaired, and `LeaveSettlementAction` fires the settlement-left events other listeners act
on. Not widened to `IsEnlisted`: `EnlistedBattle` needs the battle menus, captivity menus are
vanilla's, and a discharge sets `NotEnlisted` and builds a settlement encounter before it opens a
settlement menu. `EnsureServiceMenu` keeps its `EnlistedAttached` gate: the redirect alone catches
every push.

Still open: inside a VILLAGE, `GetGenericStateMenu` returns null for an encounter-less party, so if
the wait menu ever closed there the player would stand in the village with no menu and could not
move (`MobileParty.DoUpdatePosition` returns while `CurrentSettlement` is set). Nothing found closes
the wait menu in the grace window, so it is not fixed here. The root fix, a settlement encounter on
the follow path, still collides with the battle path's R3 handling.
```

4g. `docs/reference/harmony-patch-registry.md` line 620. Replace the exact substring:

```
Redirect fires ONLY in `EnlistedAttached` (battle state is deliberately exempt so encounter/loot menus flow), from a config-driven list,
```

with:

```
Redirect fires in `EnlistedAttached`, and in `CommanderUnavailable` while the player is inside a settlement with no `PlayerEncounter` (#511); battle state is deliberately exempt so encounter/loot menus flow. The ids come from a config-driven list,
```

(the rest of the sentence, " fail-open with once-per-id diagnostics.", stays). The file has a UTF-8
BOM; the Edit tool keeps it.

4h. `docs/features/return-to-army.md` lines 185 to 186. Replace:

```
  `IEnlistmentConfigProvider` does carry `army_wait_at_settlement`, but the redirect is gated on
  `EnlistedAttached` before the list is read.
```

with:

```
  `IEnlistmentConfigProvider` does carry `army_wait_at_settlement`, but the redirect reads the list
  only in `EnlistedAttached`, and in `CommanderUnavailable` inside a settlement with no
  `PlayerEncounter` (#511).
```

**Verify**:
- `git grep -n -E "redirect ONLY while|Redirect fires ONLY|while EnlistedAttached\. Seeded|it is gated on EnlistedAttached, and step 8|Known gap: the mask still disarms|for any non-attached state|but the redirect is gated on" -- Main TAOM.Tests docs/features docs/reference`
  prints nothing.
- `git grep -n -c "CommanderUnavailable" -- docs/features/enlistment.md` prints
  `docs/features/enlistment.md:17` (it prints `:15` at 3164713da; the planned text removes two
  matching lines and adds four: one in 4e, three in 4f). The plan writer applied 4d, 4e and 4f to a
  scratch copy of the base file and counted 17.
- `git diff 3164713da -U0 -- Main TAOM.Tests docs | grep '^+' | grep -c -F -e "$(printf '\xe2\x80\x94')" -e "$(printf '\xe2\x80\x93')"`
  prints `0` (it counts added lines holding an em or en dash; grep exits 1 on a zero count, which is
  the pass).
- `python tools/lint_docs.py --fail-on-drift` exits 0.

### Step 5: full verification

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EnlistmentMenuServiceTests"`
4. `python tools/lint_docs.py --fail-on-drift`
5. `git status --porcelain`
6. `git diff 3164713da -- Main/Features/Enlistment/EnlistmentMenuService.cs | grep -E '^[-+][^-+]'`

**Verify**: build exit 0; full suite `Failed: 3` (the same three localization tests), `Passed:
42935`, `Skipped: 9`, `Total: 42947` (42911 + 24 and 42923 + 24; if Step 2 showed a different
number of new results, use base + that number); filtered `Failed: 0, Passed: B + 24`; lint exit 0;
`git status --porcelain` lists only the eight in-scope files (plus any untracked `plans/` content
that was there before you started); the diff of `EnlistmentMenuService.cs` (item 6 drops the
`---`/`+++` header lines) removes only the old gate line
`if (_store.Record.State != EnlistmentState.EnlistedAttached)` (plus, depending on how the diff
aligns, the unchanged `return false;` beside it, re-added), and adds the `var state` line, the new
gate, and the helper with its comment; no other line of the method changes.

### Step 6: commit

Stage by name:

```
git add Main/Features/Enlistment/EnlistmentMenuService.cs TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs Main/Features/Enlistment/IEnlistmentMenuService.cs Main/Features/Enlistment/IEnlistmentConfigProvider.cs Main/Features/Enlistment/DischargeService.cs docs/features/enlistment.md docs/features/return-to-army.md docs/reference/harmony-patch-registry.md
```

Commit message (draft; re-read every claim against your diff, substitute the version, wrap at 72;
the trailers' continuation lines are indented by two spaces so git folds each into one trailer):

```
fix(enlistment): v2.0.40 - keep the redirect inside a town in grace

When a soldier's commander was captured or lost his party while the
soldier stood inside the commander's town or castle, the service
menu redirect switched off for the whole grace window. The soldier
is placed there without a settlement encounter, so the approach menu
the game pushes for that shape (town_outside, castle_outside) would
crash at menu init. The redirect now stays on in the grace window
while the soldier is inside a settlement with no encounter (#511).
A soldier in grace who walks into a town himself brings his own
encounter and keeps the normal town menus. Outside a settlement
nothing changes, and the battle and shore-leave exceptions still
come first.

Rejected: walking the soldier out of the settlement when the
  commander is lost. It changes where he waits out the grace window,
  does not repair saves already in that shape, and fires the
  settlement-left events.
Not-tested: the SetNextMenu and EnterMenuMode hooks in a live game;
  the in-game check is owed on #511.
```

**Verify**: `git log -1 --format=%s` prints the subject, at most 72 characters, with the version
from `Main/_Module/SubModule.xml`; `git log -1 --format='%(trailers:key=Rejected,only)'` prints one
`Rejected:` trailer and `git log -1 --format='%(trailers:key=Not-tested,only)'` prints one
`Not-tested:` trailer; `git show --stat HEAD` lists exactly the eight staged files;
`git status --porcelain` shows none of them.

## Test plan

- New tests, all in `TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs`, modelled on the
  existing `TryRedirectMenu_*` tests there:
  - Regression (RED at base): `TryRedirectMenu_CommanderUnavailableInsideSettlement_RedirectsToWaitMenu`,
    six rows: the three approach menus and the three settlement menus, inside with no encounter.
  - Self-entry pin: `TryRedirectMenu_CommanderUnavailableInsideSettlementWithEncounter_NeverRedirects`,
    the same six rows with `hasPlayerEncounter: true`. Fails against the naive widening (Step 3a) and
    against the settlement-only gate (Step 3b).
  - Outside-settlement pin: `TryRedirectMenu_CommanderUnavailableOutsideSettlement_NeverRedirects`
    (five rows), plus the existing `TryRedirect_NonAttachedServiceStates_NeverRedirect`
    `CommanderUnavailable` row. Both fail against the naive widening in Step 3a.
  - Exception ordering pins: `TryRedirectMenu_CommanderUnavailableInsideSettlementInMapEvent_LetsBattleMenusThrough`
    (battle exception), `TryRedirectMenu_CommanderUnavailableOnShoreLeaveInsideSettlement_SettlementMenusPassThrough`
    (shore-leave exception).
  - Skip-guard exhaustion over the other states inside a settlement:
    `TryRedirectMenu_OtherNonAttachedStatesInsideSettlement_NeverRedirect` (fails if the gate is ever
    widened to `IsEnlisted`).
  - Hot path: `TryRedirectMenu_EveryStateButCommanderUnavailable_DoesNotReadPresenceForANonBattleMenu`
    (fails if the presence read moves above the state check).
- Cannot be unit tested: the live Harmony prefix on `GameMenuManager.SetNextMenu` and postfix on
  `MapState.EnterMenuMode` routing a real `Campaign.Tick` push or a vanilla
  `StartSettlementEncounter` through the service. Those lines only, in the commit's `Not-tested:`
  trailer.

## Done criteria

ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] The full suite reports the baseline's three localization failures and no other, with Passed =
      42911 + the new results (24 expected).
- [ ] The filtered `EnlistmentMenuServiceTests` run reports `Failed: 0` and `Passed: B + 24`.
- [ ] Step 2's RED run (6 failures, all `..._RedirectsToWaitMenu`), Step 3a's mutation run (12
      failures: the outside-settlement pins and the self-entry pin) and Step 3b's mutation run (6
      failures, all `..._InsideSettlementWithEncounter_NeverRedirects`) are quoted in the report.
- [ ] `git grep -n -E "redirect ONLY while|Redirect fires ONLY|while EnlistedAttached\. Seeded|it is gated on EnlistedAttached, and step 8|Known gap: the mask still disarms|for any non-attached state|but the redirect is gated on" -- Main TAOM.Tests docs/features docs/reference` prints nothing.
- [ ] `git grep -n -c "CommanderUnavailable" -- docs/features/enlistment.md` prints `docs/features/enlistment.md:17`.
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] `git status --porcelain` lists nothing from this plan after the commit; `git show --stat HEAD`
      lists exactly the eight in-scope files.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes.

## STOP conditions

Stop and report (do not improvise) if:

- The drift check prints a changed file and the "Current state" excerpts no longer match.
- `TryRedirectMenu` no longer opens with the `EnlistedAttached` gate at lines 112 to 113, or the
  battle and shore-leave exceptions are not below it: someone may have fixed #511 another way.
- `IMobilePartyAttachmentAdapter.GetPresenceFlags()`, `PlayerPresenceFlags.IsInSettlement` or
  `PlayerPresenceFlags.HasPlayerEncounter` does not exist with the shape quoted above.
- Step 2's RED run fails for any reason other than the six `..._RedirectsToWaitMenu` assertions (for
  example a compile error you cannot fix in one attempt, or NSubstitute refusing the struct return).
- Step 3a does not fail the outside-settlement pins, or Step 3b does not fail exactly the six
  self-entry rows: the pins do not guard what this plan says.
- The fix seems to need `ServiceMaintenanceService.EnsureServiceMenu`, the reconciler, an adapter,
  the redirect id list, `EnlistmentIoC.cs`, `Main/IoC.cs`, `Main/SubModule.cs` or the csproj.
- Any existing test outside the three localization failures goes red, in particular anything in
  `ServiceMaintenanceServiceTests`, `EnlistmentPumpAuthorityTests`, `Patch66EnlistmentBindingTests`,
  `ServiceSignatureGateTests` or `BannedConstructsGateTests`.
- The assumption "every vanilla menu push in this shape passes through `GameMenuManager.SetNextMenu`
  or `MapState.EnterMenuMode`" turns out false in anything you read; report the route.

## Orchestrator steps (not the executor's)

- Issue: #511 exists. After merge, close it with the label `triage-needs-ingame` (the in-game check
  below is owed).
- `/deep-review` tier 1 (campaign menus) on the executor's commit before merge. Probe points are in
  Maintenance notes.
- `/localize`: none; no player-facing text.
- Feature doc: `docs/features/enlistment.md` is updated by the executor; the
  `docs/reference/feature-map.md` Enlistment row does not describe the redirect gate and needs no
  change.
- In-game check (no manifest: no existing driver reaches it; `tools/run_scenario.py` cannot enlist
  the player or remove a commander's party). Manual steps for the maintainer:
  1. Follow-path shape: enlist with a lord, follow him into a town or castle (the service wait menu
     shows inside); get his party captured or destroyed while you are inside, by whatever means is
     available (a battle he loses; a v1.5.7 console command for this is UNVERIFIED); confirm the log
     line that starts `[Enlistment] commander <id> lost their party` and names the grace day, the
     commander-loss modal, and that the wait menu stays up with the "lost the column" status; let 1
     to 2 campaign days pass at speed with no crash and no `town_outside` or `castle_outside` menu;
     save and reload in that state and confirm the wait menu (not an approach menu) comes back; then
     take the release option and confirm the discharge hands you a usable settlement menu or the map.
  2. Self-entry shape: with the commander lost while the column is on the open map (you are visible
     and free in grace), walk into a friendly town yourself. Expect the vanilla town menus, not the
     service wait menu; leave the town normally and confirm you are back on the map and can move.

## After merge: the maintainer's actions

None beyond the in-game check above. No hooks, settings or tracked-file removals.

## Maintenance notes

- `TryRedirectMenu` now reads presence for one more state. Anyone widening the gate again must keep
  the state check first (the hot-path test pins it), must keep the encounter term (the self-entry
  test pins it) and must not widen to `IsEnlisted` (the other-states test pins it).
- The review should probe: (1) a grace-window player inside a fortification whose settlement is
  then besieged or attacked: `GetGenericStateMenu` returns `menu_siege_strategies` (not redirected,
  same as `EnlistedAttached`) or `encounter` / `join_encounter` with the player in no map event
  (redirected when he has no encounter, same as `EnlistedAttached`); (2) the recovery path
  `CommanderUnavailable -> EnlistedAttached` inside a settlement, where `ReconcileGrace` calls
  `EnsureParked` without leaving the settlement and the next hourly `Assess` exits it; (3) the
  discharge path, which transitions to `Discharging` (a legal edge from `CommanderUnavailable`)
  before any menu work, so the widened mask is already off; (4) a grace player who entered a town
  himself: his visit carries a settlement encounter, so nothing is redirected; if something finishes
  that encounter while he stays inside, the next push is the encounter-less crash shape and the mask
  takes it, which is the intended fallback; (5) a grace player inside a settlement holding a
  non-settlement `PlayerEncounter` is not masked (as before this plan); whether that shape is
  reachable is UNVERIFIED (the grace sweep finishes TAOM's stranded encounters).
- Deferred, proposed not applied: the village shape (no generic state menu, immobile, no menu) if
  the wait menu ever closes inside a village in the grace window; and the root fix, a settlement
  encounter on the follow path, which needs `EncounterOwnershipPolicy` R3 taught about it first
  (issue #511 body, candidate 1). Both belong in a follow-up issue if the in-game check or a report
  shows the wait menu closing in that state.
- The unknown-id diagnostic at `EnlistmentMenuService.cs` line 155 still says "pushed while
  attached"; in the grace window inside a settlement it may print for a non-attached state. Harmless;
  left alone to keep the edit scope to the fix.
