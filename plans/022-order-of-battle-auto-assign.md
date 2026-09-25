# Plan 022: Make the Order of Battle "Assign Heroes" button place heroes as captains through HeroAutoAssigner

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. When done, update the status row for this plan
> in `plans/README.md` (add a row for 022 if none exists), unless a reviewer
> dispatched you and told you they maintain the index.
>
> **Drift check (run first, from `E:/repos/TAOM`, BEFORE creating your worktree)**:
> `git -C E:/repos/TAOM diff --stat a39a9c86..bannerlord-1.5.x -- Main/Features/CompanionTactics/ TAOM.Tests/Features/CompanionTactics/ Main/Adapters/HeroCombatAdapter.cs Main/Adapters/IHeroCombatAdapter.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/ Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml docs/features/companion-tactics.md docs/reference/feature-map.md tools/harvest_literal_loc_keys.py tools/translate_with_claude.py TAOM.Tests/Infrastructure/Localization/ AGENTS.md`
> This compares the planned commit with the branch tip (running it inside a
> worktree made at `a39a9c86` would always print nothing). Empty output: go on.
> Any output: compare the "Current state" excerpts against
> `git show bannerlord-1.5.x:<path>` for each listed file; on a mismatch, treat
> it as a STOP condition. `CHANGELOG.md` and `plans/README.md` churn every
> session and are deliberately left out.

## Status

- **Priority**: P3
- **Effort**: M
- **Risk**: MED (the final mutation of the live Order of Battle view model cannot be unit tested; it is proven only in game)
- **Depends on**: none
- **Category**: tech-debt (wires a registered but never-resolved service to a stub button)
- **Planned at**: commit `a39a9c86`, 2026-09-24 (branch `bannerlord-1.5.x`)
- **Issue**: create before implementation lands (orchestrator)
- **Maintainer decision**: decision D21 of the 2026-09-24 audit sprint (the maintainer's numbered answers to that audit): "Wire Auto-Assign (feature, own plan)". The other unreachable scaffolding in the same audit finding (`IEditorSceneAdapter`, `EditorCacheRebuild/Caching`, reserved config fields) is deleted by a separate plan (025), not here; you do not need it.

## Why this matters

The Order of Battle (OOB) screen shows a TAOM button labelled "Assign Heroes". Since commit `55950378` (2026-05-07) pressing it prints a literal, unlocalized message, `Auto-Assign is a Phase-1 stub — feature pending. See follow-up GitHub issue.` (the shipped string even contains an em dash), and does nothing. Meanwhile a tested role-to-formation scorer, `HeroAutoAssigner`, is registered in IoC and never resolved by anything. After this plan the button places the player's companions as captains of the open formations that suit their equipment (archers lead ranged formations, riders lead cavalry, and so on), through the same vanilla handler a manual drag uses, reports the result in a localized message, and leaves every captain the player already chose alone.

## Current state

All excerpts below were read at `a39a9c86`.

### Files and roles

- `Main/Features/CompanionTactics/FormationPresets/HeroAutoAssigner.cs` (51 lines): pure scoring service. Registered, never resolved. You add the planning method here.
- `Main/Features/CompanionTactics/FormationPresets/IHeroAutoAssigner.cs` (17 lines): its interface. Its doc comment is wrong about the class numbering (see "Engine facts").
- `TAOM.Tests/Features/CompanionTactics/FormationPresets/HeroAutoAssignerTests.cs` (93 lines, 7 tests): existing scorer tests; you add planner tests here.
- `Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` (220 lines): the overlay's view model. `ExecuteAssignCharacters` (lines 73 to 89) is the stub.
- `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` (134 lines): attaches the overlay layer and constructs `OOBButtonsVM` with `new` at line 113.
- `Main/Features/CompanionTactics/FormationPresets/IOrderOfBattleVMTracker.cs` / `OrderOfBattleVMTracker.cs`: hold the live vanilla `OrderOfBattleVM` (`Current`), set by a ctor postfix (`Hooks/Patch35_OrderOfBattleVM_Ctor.cs`) and cleared by an `OnFinalize` prefix.
- `Main/Features/CompanionTactics/CompanionTacticsIoC.cs` (36 lines): the feature's DryIoc registrations. Not a single-owner file.
- `Main/Features/CompanionTactics/Roles/RoleTooltipDecorator.cs`: the existing boundary class that turns an OOB hero item's `Agent` into a campaign `Hero` and a `HeroCombatAdapter` (lines 76 to 79 and 135 to 142). Your boundary class copies that resolution.
- `Main/Adapters/HeroCombatAdapter.cs` / `IHeroCombatAdapter.cs`: the ADR-007 adapter over sealed `Hero`: `new HeroCombatAdapter(hero)`.
- `Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml`: the prefab. Line 24 binds the button: `Command.Click="ExecuteAssignCharacters"`; its label is the literal `Text="Assign Heroes"` (line 32). You do NOT edit this file.

### The stub (`OOBButtonsVM.cs:55-89`)

```csharp
    public OOBButtonsVM(
        IFormationPresetService presetService,
        IOrderOfBattleVMTracker vmTracker,
        IModLogger logger)
    {
        _presetService = presetService;
        _vmTracker = vmTracker;
        _logger = logger;
        UpdatePresetsButtonText();
        IsVisible = true;
    }
    ...
    public void ExecuteAssignCharacters()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage("No Order of Battle screen detected.", Colors.Red);
            return;
        }
        // Phase-1 stub. Codex review #36 (2026-05-06) flagged this as P2 — the button
        // surfaces the "Auto-Assign" intent but does NOT invoke HeroAutoAssigner against
        // the live OrderOfBattleVM. Full implementation requires reflection on
        // OrderOfBattleVM._allHeroes (private List<OrderOfBattleHeroItemVM>) and the per-
        // formation Heroes collection on OrderOfBattleVM.Formations[N], plus a corresponding
        // mutation path. Tracked as follow-up; until then, users see this message and can
        // still drag heroes manually.
        DisplayMessage("Auto-Assign is a Phase-1 stub — feature pending. See follow-up GitHub issue.", Colors.Yellow);
    }
```

`DisplayMessage` is `private static void DisplayMessage(string text, Color color)` at lines 216 to 219 (it calls `InformationManager.DisplayMessage(new InformationMessage(text, color))`). The usings at lines 1 to 9 are `System`, `System.Collections.Generic`, `System.Linq`, `TaleWorlds.Core`, `TaleWorlds.Library`, `TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle`, `TAOM.Core.Logging`, `TAOM.Features.CompanionTactics.FormationPresets.Models`, and a `SaveResult` alias. The comment's claim that reflection is required is wrong: every member this plan uses is public (see "Engine facts").

### The scorer (`HeroAutoAssigner.cs:14-50`)

```csharp
public sealed class HeroAutoAssigner : IHeroAutoAssigner
{
    private readonly ICompanionRoleService _roles;

    public HeroAutoAssigner(ICompanionRoleService roles)
    {
        _roles = roles;
    }

    public int ScoreHeroForFormation(IHeroCombatAdapter hero, int formationClass)
    {
        if (hero == null) return 0;
        var role = _roles.GetPrimaryRole(hero);
        return ScoreRoleForFormation(role, formationClass);
    }

    public int ScoreRoleForFormation(CombatRole role, int formationClass)
    {
        var melee = role == CombatRole.ShieldInfantry || role == CombatRole.TwoHanded
                 || role == CombatRole.Polearm || role == CombatRole.OneHanded;
        var ranged = role == CombatRole.Archer || role == CombatRole.Crossbow
                  || role == CombatRole.Skirmisher || role == CombatRole.Slinger;
        var cavalry = role == CombatRole.Cavalry;
        var horseArcher = role == CombatRole.HorseArcher;

        return formationClass switch
        {
            1 => melee ? 100 : 0,
            2 => ranged ? 100 : 0,
            3 => cavalry ? 100 : 0,
            4 => horseArcher ? 100 : 0,
            5 => melee ? 100 : (ranged ? 50 : 0),    // HeavyInfantry — prefers melee, accepts ranged
            6 => cavalry ? 100 : (horseArcher ? 50 : 0),
            0 => 50,                                  // Default class
            _ => 50,
        };
    }
}
```

`IHeroAutoAssigner.cs:6-17`:

```csharp
/// <summary>
/// Role-based scoring used by the OOB auto-assign button. Returns a 0..130 affinity score
/// for putting a hero into a formation of class <paramref name="formationClass"/>.
///
/// formationClass values follow vanilla DeploymentFormationClass:
///   0=Default, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=HeavyInfantry, 6=LightCavalry
/// </summary>
public interface IHeroAutoAssigner
{
    int ScoreHeroForFormation(IHeroCombatAdapter hero, int formationClass);
    int ScoreRoleForFormation(CombatRole role, int formationClass);
}
```

`ICompanionRoleService.GetPrimaryRole(IHeroCombatAdapter hero)` returns a `CombatRole` (`Unknown=0, Archer=1, Crossbow=2, ShieldInfantry=3, TwoHanded=4, Polearm=5, Cavalry=6, HorseArcher=7, Skirmisher=8, OneHanded=9, Slinger=10`). A mounted hero is always `Cavalry` or `HorseArcher`. `Unknown` scores 0 against classes 1 to 6.

### The overlay service (`OOBOverlayService.cs:41-51, 113`)

```csharp
    public OOBOverlayService(
        IModLogger logger,
        IFormationPresetService presetService,
        IOrderOfBattleVMTracker vmTracker,
        ICompanionTacticsSettingsProvider settings)
    {
        _logger = logger;
        _presetService = presetService;
        _vmTracker = vmTracker;
        _settings = settings;
    }
...
            _vm = new OOBButtonsVM(_presetService, _vmTracker, _logger);
```

The overlay attaches only when `_settings.EnableFormationPresets` is true (line 70). That MCM setting defaults to **false** (`docs/features/companion-tactics.md:109`, "WIP"). So the button is only visible to players who opted into Formation Presets. Do NOT change that default.

### IoC (`CompanionTacticsIoC.cs:25-29`)

```csharp
        // FormationPresets
        container.Register<IFormationPresetService, FormationPresetService>(Reuse.Singleton);
        container.Register<IHeroAutoAssigner, HeroAutoAssigner>(Reuse.Singleton);
        container.Register<IOrderOfBattleVMTracker, OrderOfBattleVMTracker>(Reuse.Singleton);
        container.Register<IOOBOverlayService, OOBOverlayService>(Reuse.Singleton);
```

`Main/IoC.cs:158` already calls `CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);` and `Main/SubModule.cs:1719` already applies `_harmony.PatchCategory("Patch35_CompanionTactics");`. Neither single-owner file needs an edit.

### Hero resolution precedent (`RoleTooltipDecorator.cs:135-142`)

```csharp
    private static Hero ResolveAgentHero(Agent agent)
    {
        if (agent == null) return null;
        if (agent.Character is not CharacterObject co || !co.IsHero) return null;
        var hero = co.HeroObject;
        if (hero == null || hero == Hero.MainHero) return null;
        return hero;
    }
```

(`Hero`, `CharacterObject` are in `TaleWorlds.CampaignSystem`; `Agent` in `TaleWorlds.MountAndBlade`.) The OOB role badges come from `_roles.GetPrimaryRole(new HeroCombatAdapter(hero))` on this hero, so Auto-Assign will agree with the `[BOW]`/`[CAV]` badge the player sees.

### Engine facts (installed v1.5.3 DLLs; do not re-derive, do not guess beyond them)

Decompiled with `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.<Type>`, `... path TaleWorlds.Core.DeploymentFormationClass` and `... path TaleWorlds.Localization.TextObject` (cache `C:\Users\mikew\.taom-src\v1.5.3\`).

1. **`DeploymentFormationClass` (namespace `TaleWorlds.Core`) is**
   `Unset=0, Infantry=1, Ranged=2, Cavalry=3, HorseArcher=4, InfantryAndRanged=5, CavalryAndHorseArcher=6`
   (the decompiled enum declares these seven members in this order with no explicit values).
   The TAOM comments naming 5 "HeavyInfantry" and 6 "LightCavalry" are wrong; the numbers still map sensibly (5 prefers melee and accepts ranged, 6 prefers cavalry and accepts horse archers). The max score is 100, not 130.
2. **`OrderOfBattleVM`** (public class):
   - `public OrderOfBattleVM()` calls `Game.Current.EventManager.RegisterEvent<...>(...)`, so it cannot be constructed in a unit test. Use `FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM))` in tests (precedent: `TAOM.Tests/Adapters/MissionAdapterFactoryTests.cs:25`, `(Agent)FormatterServices.GetUninitializedObject(typeof(Agent))`).
   - `public bool IsPlayerGeneral { get; }` (line 254). When false, every hero item except the player's is disabled and vanilla pre-assigns sergeants.
   - `public MBBindingList<OrderOfBattleFormationItemVM> FormationsFirstHalf` (line 203) and `FormationsSecondHalf` (line 491): together all formations, in formation-index order.
   - `public MBBindingList<OrderOfBattleHeroItemVM> UnassignedHeroes` (line 508).
   - `public void ExecuteClearHeroSelection()` (line 1322): clears the hero selection.
   - `private void OnFormationAcceptCaptain(OrderOfBattleFormationItemVM formationItem)` (lines 1327 to 1349), the handler a manual captain drop runs:
     ```csharp
     if (_selectedHeroes.Count != 1) { /* reset selection */ ClearHeroItemSelection(); return; }
     OrderOfBattleHeroItemVM orderOfBattleHeroItemVM = _selectedHeroes[0];
     ClearHeroAssignment(orderOfBattleHeroItemVM);
     AssignCaptain(orderOfBattleHeroItemVM.Agent, formationItem);
     ClearHeroItemSelection();
     orderOfBattleHeroItemVM.IsShown = true;
     if (!IsPlayerGeneral) { _mission.GetMissionBehavior<AssignPlayerRoleInTeamMissionController>().OnPlayerChoiceMade(formationItem.Formation.Index); }
     Game.Current?.EventManager.TriggerEvent(new OrderOfBattleHeroAssignedToFormationEvent(orderOfBattleHeroItemVM.Agent, formationItem.Formation));
     ```
   - `private void OnHeroSelection(OrderOfBattleHeroItemVM heroSlotItem)` (lines 1361 to 1378): when `IsPlayerGeneral`, a hero leading a formation is clear-and-selected, any other hero is toggled.
   - `private void InitializeFormationCallbacks()` (lines 569 to 592) wires both handlers into public static fields: `OrderOfBattleFormationItemVM.OnAcceptCaptain = OnFormationAcceptCaptain;` and `OrderOfBattleHeroItemVM.OnHeroSelection = OnHeroSelection;`. `public void Initialize(Mission mission, ...)` (line 702) calls `InitializeFormationCallbacks()` at line 717. Look for the assignments in `InitializeFormationCallbacks`, not in the `Initialize` body.
   - Vanilla has **no hero auto-assign**. Its "Reset Deployment"/Auto Deploy (`ExecuteAutoDeploy`, line 2011) calls `BeforeAutoDeploy`, which clears every hero assignment (`ClearAllHeroAssignments`). At initialization `SetInitialHeroFormations` only places heroes as hero-troops in a formation of their troop class; it never makes anyone captain.
3. **`OrderOfBattleFormationItemVM`** (public class):
   - `public static Action<OrderOfBattleFormationItemVM> OnAcceptCaptain;` (line 37) and `public void ExecuteAcceptCaptain() { OnAcceptCaptain?.Invoke(this); }` (line 1271).
   - `public bool HasFormation` (line 163; set at line 959 as `Classes.Any(c => c.Class != FormationClass.NumberOfAllFormations)`, so it means "the formation has a troop class set", NOT "the formation has troops"), `public bool HasCaptain`, `public OrderOfBattleHeroItemVM Captain { get; set; }` (line 505), `public MBBindingList<OrderOfBattleHeroItemVM> HeroTroops` (line 523), `public Formation Formation { get; private set; }` (line 143).
   - `public DeploymentFormationClass GetOrderOfBattleClass()` (line 964): derived from the formation's `Classes`; returns `Unset` for any combination it does not name.
4. **`OrderOfBattleHeroItemVM`** (public class):
   - `public static Action<OrderOfBattleHeroItemVM> OnHeroSelection;` (line 17), invoked by the private `ExecuteSelection()` (line 373) when the player clicks a portrait.
   - `public readonly Agent Agent;` (line 27); `public bool IsMainHero`, `IsLeadingAFormation`, `IsAssignedToAFormation`, `IsDisabled` (all public properties).
   - Captain identity: vanilla's `AssignCaptain` finds the item in `_allHeroes` by agent and sets `formationItem.Captain = thatItem`; the items in `UnassignedHeroes` and each `HeroTroops` list are those same instances, so `slot.Captain == hero` is a valid success check.
5. `TaleWorlds.Library.InformationManager.DisplayMessage(message)` is `DisplayMessageInternal?.Invoke(message)`, so it is a safe no-op in unit tests. `new TextObject("{=key}...").ToString()` works in unit tests (precedent: `CareerScreenVM`, constructed by `CareerScreenVMTests.cs:500`, which only calls `ToString()` on variable-free `TextObject`s). No existing test calls `SetTextVariable`; the decompile shows `SetTextVariable(string, int)` only writes into an `Attributes` dictionary, and `ToString()` wraps `MBTextManager.ProcessTextToString` in a `try`/`catch` that returns an error string instead of throwing. So the `Assigned` message path should not throw in the test host; if it does, that is a STOP condition (Step 4).

### What Auto-Assign does (the design this plan fixes)

- Only when the player is the general (`vm.IsPlayerGeneral`); otherwise it reports "Only the general of this battle can assign heroes." and changes nothing.
- **Open slots**: every formation with `HasFormation == true` (a troop class is set) and `HasCaptain == false`, in formation-index order (first half, then second half). Formations whose class is `Unset` (0) are never filled.
- **Candidates**: heroes in `UnassignedHeroes` plus heroes in any formation's `HeroTroops`, skipping: the player's own hero (`IsMainHero`; the player places themself), anyone already leading a formation (a manual captain choice is kept), disabled items, duplicates, and any agent that does not resolve to a campaign `Hero`.
- **Matching**: global greedy on `HeroAutoAssigner.ScoreHeroForFormation`. Build every (hero, slot) pair with score > 0, sort by score descending, then slot index ascending, then hero index ascending, and take a pair when neither its hero nor its slot is used yet. Deterministic; at most one captain per slot and one slot per hero. A hero whose role is `Unknown` is never placed.
- **Applying**: for each planned pair, exactly what a manual drag does: `vm.ExecuteClearHeroSelection()`, `OrderOfBattleHeroItemVM.OnHeroSelection?.Invoke(hero)`, `slot.ExecuteAcceptCaptain()`, then count it only if `slot.Captain == hero`. Finally `vm.ExecuteClearHeroSelection()` once more. No reflection, no private member access.
- **Message**: "Captains assigned: {COUNT}." or "No hero suits an open captain slot." (yellow), or the not-general message.
- **Co-op**: no gate. The action mutates only the local mission's OOB view model through the same public handlers a manual drag uses, and nothing campaign-side or save-backed. `docs/features/coop-interop.md` reserves `IsAuthority`/`ShouldDeferToHost`/`MayWriteSaveBackedState` for world-mutating campaign handlers and save-backed writes; this is neither. Whether BannerlordCoop shows an OOB screen on a client at all is UNVERIFIED and does not change the answer.
- **Threading**: a Gauntlet button click runs on the main thread; no `DeferredCallbackQueue.RunOrDefer` is needed.
- **Save compatibility**: no `[SaveableField]`, no `SyncData`, no MCM setting added.

### Conventions that bind this change

- **ADR-002 (thin entry points)**: entry points (patches, behaviors, views, VM commands) delegate immediately to a service; no business logic in them. The VM command here is three lines of delegation plus a message map. `OOBButtonsVM` is already 220 lines (over the 150 guideline); do not grow it beyond this plan's edit and do not refactor it.
- **ADR-007 (adapters)**: services never see sealed TaleWorlds types. The matching logic (`PlanCaptains`) takes `IHeroCombatAdapter` and plain `int` classes. The TaleWorlds-typed work lives in one boundary class (`OOBCaptainAutoAssigner`), like the existing `OOBOverlayService` and `IOrderOfBattleVMTracker`, whose interfaces legitimately name `OrderOfBattleVM` (see `IOrderOfBattleVMTracker.cs:5-10`: "Boundary class, exposes a sealed TaleWorlds VM type because the consumers ... live at the boundary layer").
- **ADR-008 (testability)**: services 100% unit covered; no static TaleWorlds calls in services. `PlanCaptains` gets full cell coverage below.
- **D19 interface rule** (maintainer decision 19 of the same audit sprint, 2026-09-24): "adapters always, a service only when faked or with a second implementation". `IOOBCaptainAutoAssigner` is faked by the VM delegation test, so it qualifies.
- `.claude/rules/csharp-architecture.md`: constructor injection only (no `IoC.Resolve` inside services or the new boundary class); convert sealed types at the boundary; `Reuse.Singleton` for services.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG`.
- Localization: every new player-facing string is `new TextObject("{=taom_key}Default")`, registered in `Main/_Module/ModuleData/taom_module_strings.xml` by `tools/harvest_literal_loc_keys.py`, and seeded into all 12 language files. The paid translator is NOT yours to run (AGENTS.md "Paid AI dispatch"; the maintainer authorizes it per run).
- Prose you write (docs, commit bodies): no em or en dash; use commas, colons, parentheses.

## Commands you will need

Run every command from your worktree root.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests (full) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | exit 0; baseline at `a39a9c86` is 10,313 or more passed, 2 skipped, 0 failed (record the exact passed count as BASELINE in Step 0); after this plan, BASELINE + 12 passed |
| Tests (filtered) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | as stated per step |
| Localization tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Infrastructure.Localization"` | exit 0, 0 failed |
| Data | `python tools/validate_moduledata.py` | exit 0, 0 ERRORs |
| Docs | `python tools/lint_docs.py --quick --summary --fail-on-dead` | prints a `---` block whose `dead_links:` count equals the DEAD_BASELINE you record in Step 0; exit 0 when that count is 0 |

`python tools/lint_docs.py` with no flags always exits 0, so it proves nothing; `--fail-on-dead` makes dead links fail it, `--quick` limits it to the dead-link check, and `--summary` prints the counts. Never run `./build.ps1` (it deploys into the game install). Never run `tools/translate_with_claude.py` with `--apply` (paid API). Any test failure in the full suite is yours to explain before you continue: the baseline has 0 failures.

## Scope

**In scope** (the only files you may create or modify):

- `Main/Features/CompanionTactics/FormationPresets/IHeroAutoAssigner.cs` (add `PlanCaptains`, correct the doc comment)
- `Main/Features/CompanionTactics/FormationPresets/HeroAutoAssigner.cs` (implement `PlanCaptains`, correct the two class comments)
- `Main/Features/CompanionTactics/FormationPresets/Models/CaptainAssignment.cs` (new)
- `Main/Features/CompanionTactics/FormationPresets/Models/AutoAssignStatus.cs` (new)
- `Main/Features/CompanionTactics/FormationPresets/Models/AutoAssignResult.cs` (new)
- `Main/Features/CompanionTactics/FormationPresets/IOOBCaptainAutoAssigner.cs` (new)
- `Main/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssigner.cs` (new)
- `Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` (constructor parameter, `ExecuteAssignCharacters`, one message helper, one using)
- `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` (constructor parameter, line 113)
- `Main/Features/CompanionTactics/CompanionTacticsIoC.cs` (one registration line)
- `TAOM.Tests/Features/CompanionTactics/FormationPresets/HeroAutoAssignerTests.cs` (new tests; the line 31 comment)
- `TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBButtonsVMTests.cs` (new)
- `Main/_Module/ModuleData/taom_module_strings.xml` (3 rows, written by the harvest tool)
- `Main/_Module/ModuleData/Languages/<LANG>/std_taom_module_strings_<locale>.xml` for the 12 languages (3 English-seeded rows each, written by the seeding script in Step 6)
- `docs/features/companion-tactics.md`
- `docs/reference/feature-map.md` (one new row)
- `CHANGELOG.md` (only under the condition in Step 7)
- `plans/README.md` (the 022 status row)

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: single-owner, and no edit is needed (the feature IoC file is already called at `IoC.cs:158`; SDK-style globbing picks up new `.cs` files). If you believe one needs an edit, STOP and report the exact line.
- `Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml`: the "Assign Heroes" label stays as is.
- The other two stubs in `OOBButtonsVM.cs` (preset Load at lines 143 to 146, name-only Save at lines 184 to 189) and every other unlocalized overlay string ("Presets", inquiry texts, "No Order of Battle screen detected."): separate work.
- `TaomSettings.cs` and the `EnableFormationPresets` default (stays `false`).
- `RoleTooltipDecorator.cs` (you copy its 7-line hero resolution; do not refactor it into a shared helper here).
- Plan 025's deletions (`IEditorSceneAdapter`, `EditorCacheRebuild/Caching`, reserved config fields).
- Any `SaveableField`, `SyncData` or MCM change.
- `tools/translate_with_claude.py --apply` (paid) and every other language row beyond the 3 new keys.

## Git workflow

- Never work in `E:\repos\TAOM`'s own working tree: it holds another session's uncommitted edits. Create your own worktree on E: from the planned commit:
  `git -C E:/repos/TAOM worktree add E:/repos/wt-022 -b improve/022-order-of-battle-auto-assign a39a9c86`
  and run everything from `E:/repos/wt-022`. If the command fails because the path or the branch already exists, STOP and report: do not delete, reuse or rename either.
- This plan file is untracked in the main tree, so it is not inside your worktree; read it from `E:/repos/TAOM/plans/022-order-of-battle-auto-assign.md`. `plans/README.md` at `a39a9c86` has no 022 row; you add one in Step 7.
- Commit subject format: `<type>(<scope>): v<version> - <description>`, at most 72 characters, where `<version>` is the `<Version value=...>` in `Main/_Module/SubModule.xml` (`v2.0.30` at `a39a9c86`; re-read it). Body wrapped at 72, human prose, no em or en dash. **No AI attribution trailer** (no `Co-Authored-By`), whatever a harness suggests.
- Stage explicit paths only (`git add <path> <path> ...`); never `git add -A`, `git add .` or `git commit -a`.
- Never push, never open a PR, never `--no-verify`. The orchestrator runs `/deep-review` on your branch.
- Planned commits (Step 8): `feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner` (code, tests, string rows) and `docs(tactics): v2.0.30 - document OOB Auto-Assign` (docs, feature map, CHANGELOG, plans index). Useful trailers: `Not-tested:` (see Test plan), `Research:` (the engine facts above).

## Steps

### Step 0: Worktree, drift check, baseline

1. Run the drift check at the top of this file from `E:/repos/TAOM` (before the worktree exists), and handle its output as described there.
2. Create the worktree as in "Git workflow". Every later command runs from `E:/repos/wt-022`.
3. Confirm the stub text: `grep -n "Phase-1 stub" Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` shows exactly five lines: 81 and 88 (the Auto-Assign stub comment and message, which you replace), and 143, 146 and 184 (the preset Load comment, the Load message `Preset "{preset.Name}" ... Load is a Phase-1 stub`, and the Save comment, which you leave alone).
4. Record the baselines. Full suite: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`; write down the exact passed count as BASELINE (expected 10,313 or more, 2 skipped, 0 failed). Docs: `python tools/lint_docs.py --quick --summary --fail-on-dead`; write down the `dead_links:` count as DEAD_BASELINE and the exit code (exit 1 at baseline is allowed only when DEAD_BASELINE is above 0; those links are pre-existing and not yours).

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroAutoAssignerTests"` → exit 0, 7 passed, 0 failed. The full run in item 4 shows 0 failed; any failure there is yours to explain before Step 1 (STOP if it is environmental).

### Step 1 (RED): planner models, interface member, stub, and failing tests

1. Create `Main/Features/CompanionTactics/FormationPresets/Models/CaptainAssignment.cs`:
   ```csharp
   namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

   /// <summary>
   /// One planned Auto-Assign captaincy: the hero at <see cref="HeroIndex"/> in the candidate
   /// list leads the open formation at <see cref="SlotIndex"/> in the slot list.
   /// </summary>
   public sealed record CaptainAssignment(int HeroIndex, int SlotIndex);
   ```
   (Records already compile in this project: `Main/Features/CoopInterop/SaveDefinerRecord.cs:9`.)
2. In `IHeroAutoAssigner.cs`, add `using System.Collections.Generic;` and this member after `ScoreRoleForFormation`:
   ```csharp
   /// <summary>
   /// Plans which candidate leads which open formation. Global greedy on
   /// <see cref="ScoreHeroForFormation"/>: pairs with score above 0, highest score first, ties
   /// broken by lower slot index then lower hero index; each hero and each slot used at most
   /// once. Slots whose class is 0 (Unset) are never filled. Null lists or null heroes are
   /// skipped. Returned in the order the pairs were taken.
   /// </summary>
   IReadOnlyList<CaptainAssignment> PlanCaptains(
       IReadOnlyList<IHeroCombatAdapter> heroes, IReadOnlyList<int> formationClasses);
   ```
   and add `using TAOM.Features.CompanionTactics.FormationPresets.Models;`.
3. In `HeroAutoAssigner.cs`, add the same usings and a stub:
   ```csharp
   public IReadOnlyList<CaptainAssignment> PlanCaptains(
       IReadOnlyList<IHeroCombatAdapter> heroes, IReadOnlyList<int> formationClasses)
       => throw new System.NotImplementedException();
   ```
4. In `HeroAutoAssignerTests.cs`, add `using System.Collections.Generic;`, `using System.Linq;` and `using TAOM.Features.CompanionTactics.FormationPresets.Models;`, a helper, and these ten tests (roles come from the substituted `_roles`, as the existing `ScoreHeroForFormation_DelegatesToRoleService` test does):
   ```csharp
   private IHeroCombatAdapter Hero(string id, CombatRole role)
   {
       var hero = MakeHero(id);
       _roles.GetPrimaryRole(hero).Returns(role);
       return hero;
   }

   private static List<CaptainAssignment> Plan(HeroAutoAssigner sut,
       IReadOnlyList<IHeroCombatAdapter> heroes, params int[] classes)
       => sut.PlanCaptains(heroes, classes).ToList();
   ```
   | Test name | Heroes (index: role) | Classes (slot index: value) | Expected list, in order |
   |---|---|---|---|
   | `PlanCaptains_NoHeroes_ReturnsEmpty` | none | 0:1 | empty |
   | `PlanCaptains_NoSlots_ReturnsEmpty` | 0:ShieldInfantry | none | empty |
   | `PlanCaptains_NullLists_ReturnEmpty` | call `_sut.PlanCaptains(null, new[] { 1 })` and `_sut.PlanCaptains(new[] { Hero("a", CombatRole.Archer) }, null)` | | both empty |
   | `PlanCaptains_ArcherAndShieldInfantry_EachLeadsTheMatchingFormation` | 0:Archer, 1:ShieldInfantry | 0:1, 1:2 | `(1,0)`, `(0,1)` |
   | `PlanCaptains_TwoCavalryOneCavalrySlot_FirstCandidateLeads` | 0:Cavalry, 1:Cavalry | 0:3 | `(0,0)` |
   | `PlanCaptains_UnknownRole_IsNeverPlaced` | 0:Unknown | 0:1, 1:2, 2:3, 3:4, 4:5, 5:6 | empty |
   | `PlanCaptains_UnsetFormationClass_IsNeverFilled` | 0:ShieldInfantry | 0:0 | empty |
   | `PlanCaptains_ArcherWithMixedAndRangedSlots_TakesTheRangedSlot` | 0:Archer | 0:5, 1:2 | `(0,1)` |
   | `PlanCaptains_ThreeMeleeHeroesTwoSlots_EachHeroAndSlotUsedOnce` | 0:TwoHanded, 1:OneHanded, 2:Polearm | 0:1, 1:0, 2:5 | `(0,0)`, `(1,2)` |
   | `PlanCaptains_NullHeroEntry_IsSkipped` | 0:null, 1:Cavalry | 0:3 | `(1,0)` |

   Assert with `CollectionAssert.AreEqual(new List<CaptainAssignment> { new(1, 0), new(0, 1) }, result)` (records compare by value), or `Assert.AreEqual(0, result.Count)` for the empty cases. Give each hero a distinct id ("a", "b", "c") because `CompanionRoleService` caches by `StringId` (the substitute does not, but keep ids distinct anyway). Pass heroes as `new List<IHeroCombatAdapter> { ... }` so a `null` entry is allowed.

   Why the mixed-slot test matters: a per-formation greedy would give the lone archer slot 0 (score 50) and leave the ranged formation without a captain; the global order gives it slot 1 (score 100).
5. Correct the comment `// formationClass: 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=HeavyInfantry, 6=LightCavalry` (line 31 at `a39a9c86`, line 34 after the three usings above; find it by its text) to: `// formationClass (DeploymentFormationClass): 0=Unset, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher`. Do not rename the existing test `ScoreRoleForFormation_HeavyInfantryClass_PrefersMelee`.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` → exit 0. Then `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroAutoAssignerTests"` → exit non-zero, 17 total: 7 passed, 10 failed, every failure a `NotImplementedException`.

### Step 2 (GREEN): implement PlanCaptains and correct the class comments

Replace the stub in `HeroAutoAssigner.cs` with:

```csharp
    private const int UnsetFormationClass = 0;

    public IReadOnlyList<CaptainAssignment> PlanCaptains(
        IReadOnlyList<IHeroCombatAdapter> heroes, IReadOnlyList<int> formationClasses)
    {
        var plan = new List<CaptainAssignment>();
        if (heroes == null || formationClasses == null) return plan;

        var pairs = new List<(int Score, int Slot, int Hero)>();
        for (var slot = 0; slot < formationClasses.Count; slot++)
        {
            if (formationClasses[slot] == UnsetFormationClass) continue;
            for (var hero = 0; hero < heroes.Count; hero++)
            {
                var score = ScoreHeroForFormation(heroes[hero], formationClasses[slot]);
                if (score > 0) pairs.Add((score, slot, hero));
            }
        }

        pairs.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score)
            : a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot)
            : a.Hero.CompareTo(b.Hero));

        var usedHeroes = new HashSet<int>();
        var usedSlots = new HashSet<int>();
        foreach (var pair in pairs)
        {
            if (usedHeroes.Contains(pair.Hero) || usedSlots.Contains(pair.Slot)) continue;
            usedHeroes.Add(pair.Hero);
            usedSlots.Add(pair.Slot);
            plan.Add(new CaptainAssignment(pair.Hero, pair.Slot));
        }
        return plan;
    }
```

Correct the comments (numbers and behaviour unchanged):

- In the `HeroAutoAssigner.cs` class summary, the two lines `/// formationClass values: 0=Default, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher,` and `/// 5=HeavyInfantry, 6=LightCavalry. Source values from <c>DeploymentFormationClass</c>.` (lines 11 and 12 at `a39a9c86`, shifted by the usings from Step 1; find them by their text) become: `formationClass values are vanilla TaleWorlds.Core.DeploymentFormationClass: 0=Unset, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher.`
- The trailing comment on the `5 =>` arm becomes `// InfantryAndRanged: prefers melee, accepts ranged`; the `0 =>` arm comment becomes `// Unset`.
- `IHeroAutoAssigner.cs` summary: "Returns a 0..100 affinity score" and the same class list as above.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroAutoAssignerTests"` → exit 0, 17 passed, 0 failed.

### Step 3 (RED): the boundary seam, the VM constructor, and the VM delegation test

1. Create `Models/AutoAssignStatus.cs`:
   ```csharp
   namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

   public enum AutoAssignStatus
   {
       NoneAssigned,
       Assigned,
       NotGeneral,
   }
   ```
2. Create `Models/AutoAssignResult.cs`:
   ```csharp
   namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

   /// <summary>Outcome of one Auto-Assign press, for the overlay's message.</summary>
   public sealed record AutoAssignResult(AutoAssignStatus Status, int AssignedCount);
   ```
3. Create `IOOBCaptainAutoAssigner.cs`:
   ```csharp
   using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
   using TAOM.Features.CompanionTactics.FormationPresets.Models;

   namespace TAOM.Features.CompanionTactics.FormationPresets;

   /// <summary>
   /// Applies Auto-Assign to the live Order of Battle view model. Boundary class: exposes the
   /// sealed TaleWorlds VM type because its only callers (the overlay VM) live at the boundary,
   /// as <see cref="IOrderOfBattleVMTracker"/> does. The matching itself is
   /// <see cref="IHeroAutoAssigner.PlanCaptains"/>.
   /// </summary>
   public interface IOOBCaptainAutoAssigner
   {
       AutoAssignResult AssignCaptains(OrderOfBattleVM vm);
   }
   ```
4. In `OOBButtonsVM.cs`: add a field `private readonly IOOBCaptainAutoAssigner _captainAutoAssigner;`, and change the constructor to `OOBButtonsVM(IFormationPresetService presetService, IOrderOfBattleVMTracker vmTracker, IOOBCaptainAutoAssigner captainAutoAssigner, IModLogger logger)`, assigning the new field. Do not change `ExecuteAssignCharacters` yet.
5. In `OOBOverlayService.cs`: add a field `private readonly IOOBCaptainAutoAssigner _captainAutoAssigner;`, a constructor parameter `IOOBCaptainAutoAssigner captainAutoAssigner` after `ICompanionTacticsSettingsProvider settings`, and change the line `_vm = new OOBButtonsVM(_presetService, _vmTracker, _logger);` (line 113 at `a39a9c86`; it shifts once you add the field, so find it by its text) to `_vm = new OOBButtonsVM(_presetService, _vmTracker, _captainAutoAssigner, _logger);`.
6. Create `TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBButtonsVMTests.cs` with two tests:
   ```csharp
   using System.Collections.Generic;
   using System.Runtime.Serialization;
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using NSubstitute;
   using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
   using TAOM.Core.Logging;
   using TAOM.Features.CompanionTactics.FormationPresets;
   using TAOM.Features.CompanionTactics.FormationPresets.Models;
   using TAOM.Features.CompanionTactics.FormationPresets.UI;

   namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

   /// <summary>
   /// Pins that the overlay's Assign Heroes command delegates to the boundary assigner (ADR-002).
   /// The OOB VM is a bare uninitialized object: its real constructor needs Game.Current, and
   /// the command only passes the reference through.
   /// </summary>
   [TestClass]
   public class OOBButtonsVMTests
   {
       private IFormationPresetService _presets = null!;
       private IOrderOfBattleVMTracker _tracker = null!;
       private IOOBCaptainAutoAssigner _assigner = null!;
       private OOBButtonsVM _sut = null!;

       [TestInitialize]
       public void Setup()
       {
           _presets = Substitute.For<IFormationPresetService>();
           _presets.Presets.Returns(new List<HoNFormationPreset>());
           _tracker = Substitute.For<IOrderOfBattleVMTracker>();
           _assigner = Substitute.For<IOOBCaptainAutoAssigner>();
           _sut = new OOBButtonsVM(_presets, _tracker, _assigner, Substitute.For<IModLogger>());
       }

       [TestMethod]
       public void ExecuteAssignCharacters_ScreenOpen_DelegatesToCaptainAutoAssigner()
       {
           var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));
           _tracker.Current.Returns(oob);
           _assigner.AssignCaptains(oob).Returns(new AutoAssignResult(AutoAssignStatus.Assigned, 2));

           _sut.ExecuteAssignCharacters();

           _assigner.Received(1).AssignCaptains(oob);
       }

       [TestMethod]
       public void ExecuteAssignCharacters_NoScreen_DoesNotCallCaptainAutoAssigner()
       {
           _tracker.Current.Returns((OrderOfBattleVM)null!);

           _sut.ExecuteAssignCharacters();

           _assigner.DidNotReceiveWithAnyArgs().AssignCaptains(default!);
       }
   }
   ```

**Verify**: build exits 0. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~OOBButtonsVMTests"` → exit non-zero, 2 total: `ExecuteAssignCharacters_ScreenOpen_DelegatesToCaptainAutoAssigner` FAILS (received 0 calls), `ExecuteAssignCharacters_NoScreen_DoesNotCallCaptainAutoAssigner` passes. If the failing test instead fails with an exception from `GetUninitializedObject` or from the `OOBButtonsVM` constructor, that is a STOP condition.

### Step 4 (GREEN): delegate the command and localize its messages

In `OOBButtonsVM.cs`, add `using TaleWorlds.Localization;`, and replace the whole `public void ExecuteAssignCharacters()` method (lines 73 to 89 at `a39a9c86`, shifted by Step 3's field and the new using; find it by its name), including the stub comment and the em-dash message, with:

```csharp
    public void ExecuteAssignCharacters()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage("No Order of Battle screen detected.", Colors.Red);
            return;
        }
        DisplayMessage(AutoAssignMessage(_captainAutoAssigner.AssignCaptains(vm)), Colors.Yellow);
    }

    private static string AutoAssignMessage(AutoAssignResult result) => result?.Status switch
    {
        AutoAssignStatus.Assigned => new TextObject("{=taom_oob_autoassign_done}Captains assigned: {COUNT}.")
            .SetTextVariable("COUNT", result.AssignedCount).ToString(),
        AutoAssignStatus.NotGeneral => new TextObject(
            "{=taom_oob_autoassign_not_general}Only the general of this battle can assign heroes.").ToString(),
        _ => new TextObject("{=taom_oob_autoassign_none}No hero suits an open captain slot.").ToString(),
    };
```

Keep each `{=taom_...}Default` literal on one source line, exactly as above: the harvest tool reads the default out of the literal.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~OOBButtonsVMTests"` → exit 0, 2 passed. If `ExecuteAssignCharacters_ScreenOpen_DelegatesToCaptainAutoAssigner` fails with an exception thrown from `TextObject`, `SetTextVariable`, `MBTextManager` or `ToString()`, that is a STOP condition: report the exception; do not change the message code or the test. `grep -c "Auto-Assign is a Phase-1 stub" Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` → `0`, and `grep -c "Phase-1 stub" Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` → `3` (the preset Load comment, the Load message and the Save comment remain).

### Step 5: the boundary assigner and its registration

Create `Main/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssigner.cs`:

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Boundary class for the OOB Auto-Assign button. Reads the live vanilla view model, adapts each
/// candidate hero for <see cref="IHeroAutoAssigner.PlanCaptains"/>, and applies the plan through
/// vanilla's own manual-drag path (select the hero, then the formation's accept-captain
/// command), so vanilla keeps every side effect: agent formation, Formation.Captain, banner,
/// unassigned list and the tutorial event. Uses public members only; no reflection.
/// Keeps every captain already placed and never places the player's own hero.
/// </summary>
public sealed class OOBCaptainAutoAssigner : IOOBCaptainAutoAssigner
{
    private readonly IHeroAutoAssigner _planner;
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    public OOBCaptainAutoAssigner(
        IHeroAutoAssigner planner,
        ICompanionTacticsSettingsProvider settings,
        IModLogger logger)
    {
        _planner = planner;
        _settings = settings;
        _logger = logger;
    }

    public AutoAssignResult AssignCaptains(OrderOfBattleVM vm)
    {
        if (vm == null) return new AutoAssignResult(AutoAssignStatus.NoneAssigned, 0);
        if (!vm.IsPlayerGeneral) return new AutoAssignResult(AutoAssignStatus.NotGeneral, 0);

        var formations = new List<OrderOfBattleFormationItemVM>();
        AddAll(formations, vm.FormationsFirstHalf);
        AddAll(formations, vm.FormationsSecondHalf);

        var slots = new List<OrderOfBattleFormationItemVM>();
        var slotClasses = new List<int>();
        foreach (var formation in formations)
        {
            if (!formation.HasFormation || formation.HasCaptain) continue;
            slots.Add(formation);
            slotClasses.Add((int)formation.GetOrderOfBattleClass());
        }

        var heroItems = new List<OrderOfBattleHeroItemVM>();
        var heroes = new List<IHeroCombatAdapter>();
        if (vm.UnassignedHeroes != null)
            foreach (var item in vm.UnassignedHeroes) AddCandidate(item, heroItems, heroes);
        foreach (var formation in formations)
        {
            if (formation.HeroTroops == null) continue;
            foreach (var item in formation.HeroTroops) AddCandidate(item, heroItems, heroes);
        }

        var assigned = 0;
        foreach (var pick in _planner.PlanCaptains(heroes, slotClasses))
        {
            var hero = heroItems[pick.HeroIndex];
            var slot = slots[pick.SlotIndex];
            vm.ExecuteClearHeroSelection();
            OrderOfBattleHeroItemVM.OnHeroSelection?.Invoke(hero);
            slot.ExecuteAcceptCaptain();
            if (slot.Captain == hero) assigned++;
            else _logger.LogWarning($"[FormationPresets] Auto-Assign: vanilla did not accept {hero.Agent?.Name} as captain of formation {slot.Formation?.Index}");
        }
        vm.ExecuteClearHeroSelection();

        if (_settings.FormationPresetsDebug)
            _logger.LogDebug($"[FormationPresets] Auto-Assign: {heroes.Count} candidates, {slots.Count} open slots, {assigned} captains placed");
        return assigned > 0
            ? new AutoAssignResult(AutoAssignStatus.Assigned, assigned)
            : new AutoAssignResult(AutoAssignStatus.NoneAssigned, 0);
    }

    private static void AddAll(List<OrderOfBattleFormationItemVM> into, IEnumerable<OrderOfBattleFormationItemVM> from)
    {
        if (from == null) return;
        foreach (var formation in from)
            if (formation != null) into.Add(formation);
    }

    private static void AddCandidate(OrderOfBattleHeroItemVM item,
        List<OrderOfBattleHeroItemVM> items, List<IHeroCombatAdapter> heroes)
    {
        if (item == null || item.IsMainHero || item.IsLeadingAFormation || item.IsDisabled) return;
        if (items.Contains(item)) return;
        var hero = ResolveAgentHero(item.Agent);
        if (hero == null) return;
        items.Add(item);
        heroes.Add(new HeroCombatAdapter(hero));
    }

    // Same resolution as RoleTooltipDecorator.ResolveAgentHero, so the role Auto-Assign uses is
    // the role the OOB tooltip badge shows.
    private static Hero ResolveAgentHero(Agent agent)
    {
        if (agent == null) return null;
        if (agent.Character is not CharacterObject co || !co.IsHero) return null;
        var hero = co.HeroObject;
        if (hero == null || hero == Hero.MainHero) return null;
        return hero;
    }
}
```

Why the candidate list is snapshotted before applying: `ExecuteAcceptCaptain` removes the hero from `UnassignedHeroes` or from a `HeroTroops` list; iterating those binding lists while applying would throw "collection was modified".

In `CompanionTacticsIoC.cs`, add after the line `container.Register<IOOBOverlayService, OOBOverlayService>(Reuse.Singleton);` (line 29):

```csharp
        container.Register<IOOBCaptainAutoAssigner, OOBCaptainAutoAssigner>(Reuse.Singleton);
```

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` → exit 0, 0 errors. `grep -rn "IOOBCaptainAutoAssigner" Main/Features/CompanionTactics/CompanionTacticsIoC.cs` → 1 match. `grep -rn "AccessTools\|GetField\|GetMethod" Main/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssigner.cs` → no matches.

### Step 6: register and seed the three strings (no paid translation)

1. `python tools/harvest_literal_loc_keys.py --dry-run` → reports `unregistered: 3` and `taom_module_strings.xml  +3` (the three `taom_oob_autoassign_*` keys). Any other number is a STOP condition (the baseline at `a39a9c86` has zero unregistered keys).
2. `python tools/harvest_literal_loc_keys.py --apply` → `wrote 3 rows -> taom_module_strings.xml`. Check: `grep -n "taom_oob_autoassign" Main/_Module/ModuleData/taom_module_strings.xml` → 3 rows, each `<string id="taom_oob_autoassign_..." text="{=taom_oob_autoassign_...}<default>" />` with the default exactly as in the C# literal.
3. Seed the English text into all 12 language files so `LanguageFileCoverageTests` stays green and the maintainer's translator run has rows to fill. Write this script with the Write tool to your session scratchpad (not a Bash heredoc), then run it with `python <path>` from the worktree root:
   ```python
   import sys
   sys.path.insert(0, "tools")
   import translate_with_claude as t  # stdlib-only import; no API call happens here

   src = t.REPO_ROOT / "Main" / "_Module" / "ModuleData" / "taom_module_strings.xml"
   total = 0
   for lang, (locale, _name) in t.LANGUAGES.items():
       target = t.TAOM_LANG_DIR / lang / f"std_taom_module_strings_{locale}.xml"
       added = t.sync_missing_ids(src, target)
       print(lang, added)
       total += len(added)
   print("seeded", total)
   ```
   Expected: each of the 12 languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) prints exactly the three `taom_oob_autoassign_*` ids, and the last line is `seeded 36`. `t.REPO_ROOT` resolves from the imported file's location, so run it from `E:/repos/wt-022` and confirm with `git status --short Main/_Module/ModuleData/Languages` that only the 12 `std_taom_module_strings_*.xml` files changed.
4. Do NOT run `tools/translate_with_claude.py --apply`. The machine translation is the maintainer's paid run (AGENTS.md "Paid AI dispatch"); say so in your report.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Infrastructure.Localization"` → exit 0, 0 failed (this includes `UnregisteredLocalizationKeyBaselineTests` and `LanguageFileCoverageTests`). `python tools/validate_moduledata.py` → exit 0, 0 ERRORs.

### Step 7: documentation

Rule for this step: every line you edit is a new `+` line in the diff, so it must carry no em or en dash, even if the original did. Where an edited line had a dash separator, the replacement below uses a colon. Lines you do not edit keep their dashes; do not rewrite them.

1. `docs/features/companion-tactics.md`:
   - Line 8 (starts `2. **FormationPresets**`, and contains an em dash): replace the whole line with
     `2. **FormationPresets**: saveable named OOB hero-to-formation assignments; injects an Assign Heroes (Auto-Assign) button and a Presets button into the Order of Battle screen.`
   - In the Solution Approach tree under `FormationPresets/` (after the `IOOBOverlayService` line 52), add a line: `│   ├── IOOBCaptainAutoAssigner → OOBCaptainAutoAssigner  (boundary: applies PlanCaptains through vanilla's accept-captain path)`, and change the `IHeroAutoAssigner` line to `(role scoring + PlanCaptains; consumes ICompanionRoleService)`.
   - Add a subsection `### Auto-Assign (Assign Heroes button)` at the end of the "Architecture" section (before `## Configuration`) stating, in plain prose without em or en dashes: general only (otherwise a message and no change); fills formations that have a troop class set and no captain, never `Unset` formations; candidates are unassigned heroes and hero-troops, never the player's own hero, never a hero already leading; global greedy by `HeroAutoAssigner` score, ties to the lower formation index then the earlier hero; a hero with no recognised role is not placed; applied through vanilla's own selection and `ExecuteAcceptCaptain`, so the result equals a manual drag; captains already placed are kept; only visible while `EnableFormationPresets` is on (default off); no co-op gate, because it mutates only the local mission's OOB view model through vanilla's public handlers; no save data; the `DeploymentFormationClass` numbering (0=Unset to 6=CavalryAndHorseArcher).
   - Tests section (`## Tests`, lines 150 to 161 at `a39a9c86`; your earlier edits in this step shift every line number below, so find each line by its text). The headline says 84 tests, but the baseline sum is 85, because `TroopStanceManagerTests.cs` has 9 tests and its bullet says 8. Fix that too, so the bullets add up to the headline. Make exactly these four line edits:
     - Line 152 (starts `84 tests across 8 files in`): change only `84 tests across 8 files` to `97 tests across 9 files`; the rest of the line stays.
     - Line 157 (the `TroopStanceManagerTests.cs` bullet) becomes
       ``- `BattleActionBar/TroopStanceManagerTests.cs`: 9 tests; per-formationIndex isolation; ClearAllStances; SetStance toggle behavior.``
     - Line 159 (the `HeroAutoAssignerTests.cs` bullet) becomes
       ``- `FormationPresets/HeroAutoAssignerTests.cs`: 17 tests; role scoring per class plus the `PlanCaptains` cells (empty and null inputs, matching class, tie-break, unknown role, Unset class, global-over-local choice, one use per hero and slot, null hero).``
     - Insert after line 159:
       ``- `FormationPresets/OOBButtonsVMTests.cs`: 2 tests; the Assign Heroes command delegates to `IOOBCaptainAutoAssigner` and does nothing without a screen.``

     Confirm 97 with `grep -rc "\[TestMethod\]" TAOM.Tests/Features/CompanionTactics` (sum the per-file numbers: 85 at baseline plus your 12). If the sum is not 97, write the measured number and report the difference.
   - Known limitations: add a bullet "Auto-Assign places captains only. It does not move hero-troops between formations, does not use presets, and needs an in-game check on each engine bump because it drives vanilla's OOB handlers (`OrderOfBattleHeroItemVM.OnHeroSelection`, `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`)."
2. `docs/reference/feature-map.md`: CompanionTactics has no row. Insert this row directly after the row that begins `| CombatMechanics |` (line 19 at `a39a9c86`):
   `| CompanionTactics | \`Main/Features/CompanionTactics/\` (Patch35): equipment-based role badges on party and Order of Battle tooltips; Order of Battle overlay with Assign Heroes (role-matched captains through \`HeroAutoAssigner.PlanCaptains\` and vanilla's accept-captain path) and name-only presets (MCM \`EnableFormationPresets\`, off by default); display-only battle action bar. See [companion-tactics.md](../features/companion-tactics.md) |`
3. `CHANGELOG.md`: run `grep -n "CHANGELOG.md is updated every session" AGENTS.md`. If it matches (it does at `a39a9c86`, AGENTS.md line 80), add an entry. The file has no "unreleased" section: after the `> **Archive:**` note it has dated `## YYYY-MM-DD` headings, newest first, each holding `### <commit subject>` entries followed by a prose body. If the topmost heading is today's date, add your entry directly under it, above the entries already there; otherwise insert a new `## <today, YYYY-MM-DD>` heading above the current topmost date heading and put your entry under it. The entry is:
   ```markdown
   ### feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner

   Order of Battle: the Assign Heroes button now places your companions as captains of the
   formations that suit their equipment (it was a placeholder message). Visible with Formation
   Presets enabled. Three new strings are registered with English rows in all 12 languages; the
   translator run is owed. Nothing smoked in game.
   ```
   Use the version you re-read from `Main/_Module/SubModule.xml` in the heading. If the grep does not match (a pending plan, 020, may move CHANGELOG writing to release time), skip this file and put the entry's body in the feat commit body instead.
4. `plans/README.md`: its table columns are `| Plan | Title | Priority | Effort | Category | Depends on | Status |`. Add or update the 022 row: `| 022 | Make the Order of Battle "Assign Heroes" button place heroes as captains | P3 | M | tech-debt | none | DONE on branch improve/022-order-of-battle-auto-assign (<feat commit short sha>) |`.

**Verify**: `python tools/lint_docs.py --quick --summary --fail-on-dead` → `dead_links:` equals DEAD_BASELINE from Step 0 (the new feature-map link must resolve). `git diff -U0 -- docs/features/companion-tactics.md docs/reference/feature-map.md CHANGELOG.md plans/README.md | python -c "import sys; b=sys.stdin.buffer.read().decode('utf-8'); print([l for l in b.splitlines() if l.startswith('+') and ('—' in l or '–' in l)])"` prints `[]`. A non-empty list means an edited or added line still carries a dash: replace that dash with a colon or comma and re-run.

### Step 8: full verification and commits

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` → exit 0, 0 errors.
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` → exit 0, 0 failed, 2 skipped, passed = BASELINE (recorded in Step 0) + 12 (10 planner tests + 2 VM tests). Quote the exact summary line in your report.
3. `python tools/validate_moduledata.py` → exit 0, 0 ERRORs. `python tools/lint_docs.py --quick --summary --fail-on-dead` → `dead_links:` equals DEAD_BASELINE.
4. `git status --short` lists only in-scope paths.
5. Commit 1 (stage explicitly): the 7 `Main/Features/CompanionTactics/...` code files (2 edited service files, 3 new model files, 2 new boundary files), `OOBButtonsVM.cs`, `OOBOverlayService.cs`, `CompanionTacticsIoC.cs`, the 2 test files, `taom_module_strings.xml` and the 12 language files. Subject: `feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner` (re-read the version first). Body: what the button now does, that it goes through vanilla's accept-captain path, that 3 strings are registered with English seeded rows and await the maintainer's translator run; trailers `Not-tested: OOBCaptainAutoAssigner against a live OrderOfBattleVM (needs a mission); in-game check owed` and `Research: v1.5.3 OrderOfBattleVM, OrderOfBattleFormationItemVM, OrderOfBattleHeroItemVM, DeploymentFormationClass`.
6. Commit 2: the docs, feature map, CHANGELOG (if touched) and `plans/README.md`. Subject: `docs(tactics): v2.0.30 - document OOB Auto-Assign`.

**Verify**: `git log --oneline a39a9c86..HEAD` shows exactly your 2 commits; `git show --stat HEAD~1 HEAD` lists only in-scope paths; `git log -2 --format=%B | grep -ci "co-authored-by"` → `0`.

## Test plan

- **New tests, `HeroAutoAssignerTests.cs`** (10): the table in Step 1. Cells covered for `PlanCaptains`: null heroes list, null classes list, empty heroes, empty slots, one-to-one matching of two roles to two classes, two heroes competing for one slot (hero-order tie-break), `Unknown` role against all six real classes, `Unset` class, a hero whose best slot is not the first slot (global greedy), three heroes for two slots with an `Unset` slot between them (each used once, slot-order tie-break), a null hero entry.
- **New tests, `OOBButtonsVMTests.cs`** (2): screen open delegates exactly once with the tracked VM; no screen never calls the assigner.
- **Pattern**: model the planner tests on the existing `HeroAutoAssignerTests` (`Substitute.For<ICompanionRoleService>()`, `MakeHero`); model the bare VM on `TAOM.Tests/Adapters/MissionAdapterFactoryTests.cs:25`.
- **Structurally untestable** (for the `Not-tested:` trailer): `OOBCaptainAutoAssigner.AssignCaptains` against a real `OrderOfBattleVM` (needs `Mission`, `Game.Current` and `Initialize`), and the on-screen result. This is the owed in-game check.
- **In-game check owed** (the orchestrator files it on the issue with the `triage-needs-ingame` label; the executor does not run the game): enable MCM "Battle Tactics/Formation Presets"; in a campaign with at least one bow companion and one mounted companion in the party, start a field battle as the general; on the Order of Battle screen, press Assign Heroes. Expected: the bow companion leads the ranged formation, the mounted one the cavalry formation, the message reads "Captains assigned: 2.", the player's own portrait is not moved, a captain placed by hand before pressing stays, pressing again reports "No hero suits an open captain slot." when nothing is left, the battle starts and each captain fights with its formation. Then join a battle led by an AI lord: pressing reports "Only the general of this battle can assign heroes." and nothing moves. Check `taom_debug_*.log` for any `[FormationPresets] Auto-Assign: vanilla did not accept` warning.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` → all pass, 12 new tests included.

## Done criteria

ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 failed, 2 skipped, and passed = BASELINE from Step 0 (10,313 or more) + 12
- [ ] `dotnet test ... --filter "FullyQualifiedName~HeroAutoAssignerTests"` → 17 passed; `--filter "FullyQualifiedName~OOBButtonsVMTests"` → 2 passed
- [ ] `grep -rn "Auto-Assign is a Phase-1 stub" Main/` returns no matches
- [ ] `grep -rn "IOOBCaptainAutoAssigner, OOBCaptainAutoAssigner" Main/Features/CompanionTactics/CompanionTacticsIoC.cs` returns 1 match
- [ ] `grep -c "taom_oob_autoassign_" Main/_Module/ModuleData/taom_module_strings.xml` → 3; `grep -l "taom_oob_autoassign_done" Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml | wc -l` → 12
- [ ] `grep -rn "HeavyInfantry\|LightCavalry" Main/Features/CompanionTactics/FormationPresets/` returns no matches
- [ ] `grep -c "Phase-1 stub" Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs` → 3 (the untouched preset Load and Save stubs)
- [ ] `python tools/validate_moduledata.py` exits 0 with 0 ERRORs; `python tools/lint_docs.py --quick --summary --fail-on-dead` shows `dead_links:` equal to DEAD_BASELINE
- [ ] The Step 7 dash check on `git diff -U0 a39a9c86..HEAD -- docs/features/companion-tactics.md docs/reference/feature-map.md CHANGELOG.md plans/README.md` prints `[]`
- [ ] `git diff --name-only a39a9c86..HEAD` lists only in-scope paths; `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`, `OOBButtonsOverlay.xml` are absent
- [ ] `docs/reference/feature-map.md` contains a `| CompanionTactics |` row
- [ ] `plans/README.md` row for 022 updated

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check (run against `bannerlord-1.5.x` before the worktree exists) shows any listed file changed since `a39a9c86`, and the "Current state" excerpts no longer match `git show bannerlord-1.5.x:<path>`.
- `git worktree add` fails because `E:/repos/wt-022` or the branch `improve/022-order-of-battle-auto-assign` already exists.
- Step 0's grep for "Phase-1 stub" shows anything other than lines 81, 88, 143, 146 and 184.
- Any engine member this plan relies on is missing or non-public in the build: `OrderOfBattleVM.IsPlayerGeneral`, `FormationsFirstHalf`, `FormationsSecondHalf`, `UnassignedHeroes`, `ExecuteClearHeroSelection`; `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`, `GetOrderOfBattleClass`, `Captain`, `HasFormation`, `HasCaptain`, `HeroTroops`, `Formation`; `OrderOfBattleHeroItemVM.OnHeroSelection`, `Agent`, `IsMainHero`, `IsLeadingAFormation`, `IsDisabled`. A compile error naming one of these means the engine drifted: report the error; do not reach for reflection.
- `FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM))` or the `OOBButtonsVM` constructor throws inside the test host (Step 3), or the Step 4 GREEN test throws from `TextObject`, `SetTextVariable`, `MBTextManager` or `ToString()`. Report the exception; do not remove or weaken the test.
- The harvest dry run reports anything other than exactly 3 unregistered keys, or the seeding script seeds anything other than exactly 36 rows (3 per language).
- Any step would need `tools/translate_with_claude.py --apply`, an edit to a single-owner file, the prefab, `TaomSettings.cs`, or a save or MCM change.
- A commit hook blocks a commit (for example a review-gate or version-subject hook): report its message; never `--no-verify`.
- The full suite shows any failure that also fails at `a39a9c86` in a clean worktree (environment), or any new failure you cannot explain after one fix attempt.
- A step's verification fails twice after a reasonable fix attempt.
- You discover the assumption "calling `OrderOfBattleHeroItemVM.OnHeroSelection` then `ExecuteAcceptCaptain` is exactly vanilla's manual captain drag" is false in the decompiled source (for example `OnHeroSelection` or `OnAcceptCaptain` is no longer assigned in `OrderOfBattleVM.InitializeFormationCallbacks`, or `Initialize` no longer calls that method).

## Maintenance notes

- **Engine bumps**: `OOBCaptainAutoAssigner` drives vanilla's static delegates `OrderOfBattleHeroItemVM.OnHeroSelection` and `OrderOfBattleFormationItemVM.OnAcceptCaptain`, which `OrderOfBattleVM.InitializeFormationCallbacks` (called from `Initialize`) points at the most recently initialized OOB VM. If TaleWorlds renames them or changes `OnFormationAcceptCaptain`'s single-selection rule, Auto-Assign silently places nobody (each miss logs a `[FormationPresets] Auto-Assign: vanilla did not accept` warning). Add these members to the `/verify-bindings` snapshot review on the next engine bump.
- **Reviewer focus** (`/deep-review`): (1) `OOBCaptainAutoAssigner` snapshotting before mutation and the `slot.Captain == hero` success check; (2) that `ExecuteAcceptCaptain` on a formation with no captain cannot evict anyone; (3) the main-hero exclusion (a product choice: the player may want the planner to place them too; changing it is one condition); (4) the `DeploymentFormationClass` cast via `GetOrderOfBattleClass()` (returns `Unset` for class combinations it does not name, so such formations are skipped); (5) that `OOBButtonsVM` grew only by the delegation and the message map.
- **Deferred**: the preset Load and name-only Save stubs in the same VM (lines 143 to 146 and 184 to 189 at `a39a9c86`); localizing the rest of the overlay ("Assign Heroes", "Presets", inquiry texts); a shared `ResolveAgentHero` helper for `RoleTooltipDecorator` and `OOBCaptainAutoAssigner` (two copies of 7 lines, below the simplicity bar for a new abstraction today); flipping `EnableFormationPresets` on by default (a maintainer decision once Load works); machine translation of the 3 new keys (maintainer's paid run: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply` per language, or a run scoped to the three keys).
- **Custom Battle**: candidates must resolve to a campaign `Hero` through `CharacterObject.HeroObject`. Whether Custom Battle agents do is UNVERIFIED; if they do not, Auto-Assign reports "No hero suits an open captain slot." there, which is harmless.
