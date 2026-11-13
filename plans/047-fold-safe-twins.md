# Plan 047: Fold three behaviour-identical twins into one copy each

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 7087fa633..HEAD -- Main/Features/LotrIssues Main/Features/FactionMap/Hooks Main/Features/SupplyLines/UI TAOM.Tests/Features/LotrIssues TAOM.Tests/Features/FactionMap TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml docs/features/lotr-issues.md Main/_Module/ModuleData/taom_module_strings.xml Main/Features/SupplyLines/ISupplySourceService.cs TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs TAOM.Tests/Infrastructure/ServerLoadGateTests.cs TAOM.Tests/Migration/HarmonyPatchBindingTests.cs TAOM.Tests/Composition/ClientFrontEndCompositionTests.cs`.
> The last six paths are files this plan relies on without editing. If any listed file changed since
> this plan was written, compare the "Current state" excerpts with the live code; a mismatch is a STOP
> condition.

## Status

- **Priority**: P3
- **Effort**: S to M (three independent parts, one commit each)
- **Risk**: LOW
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: dotnet `Passed! - Failed: 0, Passed: 39250, Skipped: 9, Total: 39259`
  (game-backed desktop run), failing: none. Build: exit 0, `11 Warning(s)`, `0 Error(s)`. Python suite:
  `Ran 8578 tests`, `FAILED (failures=7, errors=2, skipped=13)`; the nine failing names are heraldry and
  kingdom-generator data checks that fail on the trunk (this plan touches no `tools/` file, so it does
  not run that suite). Hook suite: 1267 passed, 0 failed (not run by this plan). The RefAsm no-game
  unit step and the binding gate were not measured at planning time: Step 1 records them.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Three clusters of production C# exist as copies that must be edited in step. The three LOTR issue
templates each carry the same frequency map (a fourth copy sits in `LotrIssuesCampaignBehavior`) and
the same relation-floor rule for taking a quest, and no test covers either. The three CultureStageView
Harmony patches each carry the same engine type probe, so the next engine rename of that view needs
three edits, and a missed one silently disables one patch through its `Prepare()`. The supply order
screen has two 79-line row view models that differ only in their label literal, so a fix to one row's
clamp or callback is easy to miss in the other. After this plan each rule lives once, the issue rules
gain their first unit tests, and nothing a player sees or saves changes.

## Current state

All excerpts were read at `7087fa633`. Line numbers are that commit's.

### Part A: the LOTR issue templates

Files:

- `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs`, `DeliverGoodsLotrIssue.cs`,
  `DeliverPersonnelLotrIssue.cs`: the three `IssueBase` templates (each file also holds its paired
  `QuestBase` class, untouched here).
- `Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs`: the behavior that offers issues; holds the
  fourth frequency map.
- `Main/Features/LotrIssues/Domain/LotrIssueDefinition.cs`: the immutable definition. `Frequency` is
  `IssueFrequencyTier` (`:49`); `RelationMin` is a plain `int` (`:86`), never null.
- `Main/Features/LotrIssues/Domain/IssueFrequencyTier.cs:8-13`: `public enum IssueFrequencyTier { Rare, Common, VeryCommon }`.
- `Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:30-37`: registers the six template and quest
  classes by local id. Not touched.

`GetFrequency`, token-identical at `CombatLotrIssue.cs:83-93`, `DeliverGoodsLotrIssue.cs:100-110`,
`DeliverPersonnelLotrIssue.cs:83-93` (Combat shown):

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
```

`CanPlayerTakeQuestConditions`, token-identical at `CombatLotrIssue.cs:125-143`,
`DeliverGoodsLotrIssue.cs:143-161`, `DeliverPersonnelLotrIssue.cs:125-143` (Combat shown):

```csharp
    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        return flag == PreconditionFlags.None;
    }
```

`EnsureDef`, identical in all three (`CombatLotrIssue.cs:44-47`, `DeliverGoodsLotrIssue.cs:46-49`,
`DeliverPersonnelLotrIssue.cs:44-47`). It reads and writes the instance fields `_def` and `_defId`, so
it stays in each template:

```csharp
    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }
```

The fourth frequency map, `LotrIssuesCampaignBehavior.cs:144-152`, used once at `:111-112`:

```csharp
            Campaign.Current.IssueManager.AddPotentialIssueData(hero,
                new PotentialIssueData(OnSelected, type, MapFrequency(def.Frequency), def));
```

```csharp
    private static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)
    {
        switch (tier)
        {
            case IssueFrequencyTier.VeryCommon: return IssueBase.IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueBase.IssueFrequency.Rare;
            default: return IssueBase.IssueFrequency.Common;
        }
    }
```

**Engine facts** (`pwsh tools/taom-src.ps1 path IssueBase`, v1.5.7, `TaleWorlds.CampaignSystem.Issues.IssueBase.cs`):

- `:41` `protected enum PreconditionFlags : uint` (`None = 0u`, `Relation = 1u`, `AtWar = 0x40u`, others).
  **It is protected.** A class outside the `IssueBase` hierarchy cannot name it (compiler error CS0122),
  so a static helper cannot take or return `PreconditionFlags`. This is why Part A shares only the
  relation-floor test and the frequency map; the flag assembly, the out-parameter defaults and the at-war
  test stay in each template's override.
- `:76-81` `public enum IssueFrequency { VeryCommon, Common, Rare }`: public, usable from a helper and a test.
- `:527` `public abstract IssueFrequency GetFrequency();`
- `:529` `protected abstract bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold);`

**Save compatibility** (why a shared base class is forbidden): the save system keys each saved field by
the class level of the type that declares it. `TaleWorlds.SaveSystem.Definition.TypeDefinition.cs:176-177`:
`byte classLevel = TypeDefinitionBase.GetClassLevel(fieldInfo.DeclaringType); MemberTypeId memberTypeId = new MemberTypeId(classLevel, saveableFieldAttribute.LocalSaveId);`
and `TypeDefinitionBase.cs:20-33` counts the inheritance depth. A class inserted between `IssueBase` and
a template would change the id of each template's `[SaveableField(1)] private string _defId;`
(`CombatLotrIssue.cs:30`, `DeliverGoodsLotrIssue.cs:30`, `DeliverPersonnelLotrIssue.cs:28`) and break
existing saves. An `internal static` helper adds no class to any hierarchy and moves no field.

**Tests today**: no test exercises `GetFrequency`, `CanPlayerTakeQuestConditions` or `MapFrequency`.
`TAOM.Tests/Features/LotrIssues/LotrIssueTemplateInvariantsTests.cs` checks only
`IssueQuestCanBeDuplicated` (reflection, no test category, so it already runs without the game).
`TAOM.Tests/Features/Enlistment/AttributionAudit/EnlistedSiegeCreditTests.cs:309` reads
`CombatLotrIssue.cs` as text for a line in the quest class, which this plan does not touch.
`TAOM.Tests/Features/LotrIssues/LotrIssueServiceTests.cs:37-40` builds a `LotrIssueDefinition` with the
16-argument constructor; model the new test's builder on it.

### Part B: the CultureStageView patches

Files: `Main/Features/FactionMap/Hooks/CultureStageView_Constructor_Patch.cs`,
`CultureStageView_Finalize_Patch.cs`, `CultureStageView_Tick_Patch.cs`. Each holds the same probe at
lines 16 to 30 and the same `Prepare()` at `:32` (Constructor shown, lines 1 to 40):

```csharp
using System;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch7_FactionMap")]
[ClientOnly]
public static class CultureStageView_Constructor_Patch
{
    private static IOnCultureStageViewCreated? _hook;

    public static void Initialize(IOnCultureStageViewCreated hook) => _hook = hook;

    private static readonly string[] PossibleTypeNames =
    {
        "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
        "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
    };

    private static Type? FindCultureStageViewType()
    {
        foreach (var typeName in PossibleTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type != null) return type;
        }
        return null;
    }

    static bool Prepare() => FindCultureStageViewType() != null;

    static MethodBase? TargetMethod()
    {
        var type = FindCultureStageViewType();
        if (type == null) return null;
        var ctors = type.GetConstructors();
        return ctors.Length > 0 ? ctors[0] : null;
    }
```

Constructor lines 41 to 48, not shown above, are a blank line, a three-line comment block, a blank
line, then `[HarmonyPostfix]`, `static void Postfix(object __instance) => _hook?.OnCreated(__instance);`
and the closing brace.

The other two files match the Constructor at lines 1 to 9, 11, 13 and 15 to 36. They differ at line 10
(the class name), line 12 (the `_hook` type), line 14 (the `Initialize` parameter type), in the
`TargetMethod` body from line 37, and in the comment block and hook method after it (each of the two
is 45 lines). Finalize's `TargetMethod` (`:34-38`) returns
`type != null ? AccessTools.Method(type, "OnFinalize") : null` and its hook is a `[HarmonyPrefix] static void Prefix() => _hook?.OnFinalize();`;
Tick's (`:34-38`) returns `type != null ? AccessTools.Method(type, "Tick", new[] { typeof(float) }) : null`
with `[HarmonyPostfix] static void Postfix(object __instance, float dt) => _hook?.OnTick(__instance, dt);`.
Their `_hook` fields are `IOnCultureStageViewFinalize?` and `IOnCultureStageViewTick?`.

Readers of these classes that must keep working:

- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (`[TestCategory("BindingVerification")]`, `:43`)
  discovers patch types by a `[HarmonyPatch]` attribute or a static parameterless `TargetMethod`
  (`:116-125`) and invokes `TargetMethod` **by name** (`:153-160`). `Prepare` and `TargetMethod` must
  stay on each patch class, static and parameterless. The new helper must have neither a
  `[HarmonyPatch]` attribute nor a member named `TargetMethod` or `TargetMethods`, or the gate treats it
  as a patch.
- `TAOM.Tests/Composition/ClientFrontEndCompositionTests.cs:29-36`, `:64-66` read each patch's private
  static `_hook` field by reflection (`GetField("_hook", NonPublic | Static)`).
- `TAOM.Tests/Infrastructure/ServerLoadGateTests.cs:91` `PatchClassesReferencingClientAssemblies_AreClientOnly`
  (class is `[TestCategory("RequiresGame")]`, `:19`) reports `MISPLACED ... [ClientOnly] on a class that is not a Harmony patch class`
  (`:121-122`). **So the new helper must NOT carry `[ClientOnly]`.** The three patch classes keep theirs.
- `docs/reference/taleworlds-api-snapshot/patch-targets.md:170-172` lists the three resolved targets
  (generated); unchanged because the targets do not change.

### Part C: the supply row view models

`Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs` (79 lines) in full:

```csharp
using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// One purchasable good at the selected source. Quantity is clamped to [0, Available]; every
/// change calls back into the parent VM's Recompute so the price lines and the one-source lock
/// stay current.
/// </summary>
public sealed class SupplyGoodRowVM : ViewModel
{
    private readonly Action _onChanged;
    private readonly string _label;
    private int _qty;

    public SupplyGoodRowVM(SupplyLineItem item, Action onChanged)
    {
        _onChanged = onChanged;
        ItemId = item?.Id ?? string.Empty;
        Available = Math.Max(0, item?.Available ?? 0);
        UnitPrice = item?.UnitPrice ?? 0;

        var label = new TextObject("{=taom_sl_good_row}{NAME} - {STOCK} in stock, {PRICE} denars");
        label.SetTextVariable("NAME", item?.Name ?? ItemId);
        label.SetTextVariable("STOCK", Available);
        label.SetTextVariable("PRICE", UnitPrice);
        _label = label.ToString();
    }

    public string ItemId { get; }

    public int Available { get; }

    public int UnitPrice { get; }

    public int Qty
    {
        get => _qty;
        private set
        {
            if (_qty != value)
            {
                _qty = value;
                OnPropertyChanged(nameof(QtyText));
                _onChanged?.Invoke();
            }
        }
    }

    [DataSourceProperty]
    public string QtyText => _qty.ToString();

    [DataSourceProperty]
    public string Label => _label;

    public void ExecutePlus()
    {
        if (_qty < Available)
            Qty = _qty + 1;
    }

    public void ExecuteMinus()
    {
        if (_qty > 0)
            Qty = _qty - 1;
    }

    /// <summary>Silent reset: no per-row callback, the caller runs one Recompute for the batch.</summary>
    public void ResetQty()
    {
        if (_qty != 0)
        {
            _qty = 0;
            OnPropertyChanged(nameof(QtyText));
        }
    }
}
```

`SupplyTroopRowVM.cs` is the same file except: the summary (`:7-11`, "One recruitable troop ... kept as
its own class because the row label wording differs and the prefab binds the two lists through separate
item templates"), the class and constructor names (`:12`, `:18`), and the label lines:

```csharp
        var label = new TextObject("{=taom_sl_troop_row}{NAME} - {COUNT} available, {PRICE} denars");
        label.SetTextVariable("NAME", item?.Name ?? ItemId);
        label.SetTextVariable("COUNT", Available);
```

`SupplyLineItem` is `public sealed class` in namespace `TAOM.Features.SupplyLines`
(`Main/Features/SupplyLines/ISupplySourceService.cs:4`, `:29`).

Users in `Main/Features/SupplyLines/UI/SupplyOrderScreenVM.cs`:

- `:31` `private readonly MBBindingList<SupplyGoodRowVM> _goods = new MBBindingList<SupplyGoodRowVM>();`
- `:32` `private readonly MBBindingList<SupplyTroopRowVM> _troops = new MBBindingList<SupplyTroopRowVM>();`
- `:256` `public MBBindingList<SupplyGoodRowVM> Goods => _goods;`
- `:259` `public MBBindingList<SupplyTroopRowVM> Troops => _troops;`
- `:660` `_goods.Add(new SupplyGoodRowVM(preferred, Recompute));`
- `:664` `_goods.Add(new SupplyGoodRowVM(line, Recompute));`
- `:753` `_troops.Add(new SupplyTroopRowVM(line, Recompute));`

Test users:

- `TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs` (`[TestCategory("RequiresGame")]`,
  `:21`): the map at `:36-42`
  (`["Goods"] = typeof(SupplyGoodRowVM),` at `:39`, `["Troops"] = typeof(SupplyTroopRowVM),` at `:40`)
  and the type list at `:183-190` (`typeof(SupplyGoodRowVM),` at `:187`, `typeof(SupplyTroopRowVM),` at `:188`).
- `TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs:160`, a comment:
  `// SupplyGoodRowVM shows the id when the name is empty; the search matches the same text.`
- `TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs` (`RequiresGame`, `:17`) reaches the rows
  through `vm.Goods[i]` and `vm.Troops[i]` only; it names neither class.

The prefab `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml` binds the same names in both
item templates: `DataSource="{Goods}"` (`:225`) and `DataSource="{Troops}"` (`:309`), each with
`Text="@Label"` (`:234`, `:318`), `Command.Click="ExecuteMinus"` (`:243`, `:327`), `Text="@QtyText"`
(`:254`, `:338`), `Command.Click="ExecutePlus"` (`:258`, `:342`). Not touched.

**Engine facts** (v1.5.7, `pwsh tools/taom-src.ps1 path ViewModel` and `path GauntletView`): Gauntlet never
names a row VM type. `TaleWorlds.Library.ViewModel.cs:239-252`: the constructor caches members per
runtime `GetType()`; `:616-634` collects instance properties and methods of that type by name;
`:451-460` `GetPropertyValue(string name)` resolves a property by name; `:485-497` `ExecuteCommand`
resolves a command by name on the runtime type. `TaleWorlds.GauntletUI.Data.GauntletView.cs:166-168`
binds a list through the non-generic `IMBBindingList`. So `MBBindingList<SupplyRowVM>` binds exactly as
the two typed lists do, and no in-game check is needed for the fold.

**Localization**: the two literals are registered in `Main/_Module/ModuleData/taom_module_strings.xml:2812`
(`taom_sl_good_row`) and `:2830` (`taom_sl_troop_row`), translated in the 12 language files under
`Main/_Module/ModuleData/Languages/`, and cached in `tools/translation_cache/<lang>.json`. Every
translation uses `{STOCK}` for the good row and `{COUNT}` for the troop row. The literals must stay
byte-identical, and each must stay a whole C# string literal (`tools/check_orphan_loc_rows.py` and
`tools/harvest_literal_loc_keys.py` find a key by a `"{=taom_key}Default text"` literal under `Main/`).

**Docs**: `git grep` of `docs/features` for `SupplyGoodRowVM`, `SupplyTroopRowVM`, `PossibleTypeNames`,
`FindCultureStageViewType`, `MapFrequency` and `CanPlayerTakeQuest` at `7087fa633` found no hit.
`docs/features/supply-lines.md:254` says "+ row VMs", still true after the fold (two other row VMs remain).
The only other mention, `docs/reviews/codex-adversarial-supply-search-2026-09-13.prompt.md:69`, is a
dated review prompt kept as a record; leave it.

### Blast radius

`python tools/graphify_taom.py affected` could not run at planning time: the worktree had no graph, and
a `refresh` into a scratch folder outside the repo failed at its clustering step (`cluster-only failed (exit 143)`).
The callers below come from `git grep -n -E '<names>'` over all tracked files at `7087fa633`
(`plans/` and `docs/archive/` excluded):

- `CombatLotrIssue`, `DeliverGoodsLotrIssue`, `DeliverPersonnelLotrIssue`: constructed and typed in
  `LotrIssuesCampaignBehavior.cs:126-139`; registered in `LotrIssueSaveableTypeDefiner.cs:30-36`; tests
  `LotrIssueTemplateInvariantsTests.cs:24-26`, `LotrIssueSuppressionTests.cs:170`,
  `EnlistedSiegeCreditTests.cs:309` (source text of the quest class). Docs name the types only.
  No caller sees the change: the overrides keep their signatures and the engine calls them virtually.
- `LotrIssuesCampaignBehavior`: constructed in `Main/SubModule.cs:1538` (not touched) and in
  `TAOM.Tests/Features/CoopInterop/ClientWorldWriteGateTests.cs:89-113` (reflection on `OnGameLoaded`,
  a subclass overriding `SweepLingeringVanillaIssues`); neither reads `MapFrequency`.
- The three CultureStageView patches: initialized in `Main/Features/FactionMap/FactionMapIoC.cs:35-41`
  (`Initialize(hook)`, unchanged), and the three tests named in Part B.
- `SupplyGoodRowVM`, `SupplyTroopRowVM`: the `SupplyOrderScreenVM.cs` lines and the two test files above.

At execution, run `python tools/graphify_taom.py status --brief`. If it exits 0 (fresh graph), run
`python tools/graphify_taom.py affected "<Type>" --depth 2` for each type above and STOP if it names a
TAOM caller missing from these lists. Otherwise say in your report that you relied on this list.

### Conventions that bind this change

- **ADR-002** thin entry points: an entry point (a `CampaignBehaviorBase`, a Harmony patch class) stays
  under 150 lines and delegates; gate `EntryPointLineCountGateTests`. None of the in-scope classes is in
  `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`, and every change here shrinks them.
- **ADR-007** adapters: services take adapters, never sealed TaleWorlds types; gate
  `ServiceSignatureGateTests` scans types named `*Service` and `I*Adapter`. The new helpers are neither,
  and they take only TAOM types, strings and engine enums.
- **ADR-008** testability: every decision path gets a test; the issue rules get their first tests here.
- **ADR-003** no `#region`, **ADR-004** no `[Obsolete]`: delete the old code outright; gate `BannedConstructsGateTests`.
- **ADR-013** server-safe core: core code naming a client-only assembly fails `ServerJitGateTests`; the
  helpers name only `HarmonyLib`, `TaleWorlds.Library`, `TaleWorlds.Localization` and
  `TaleWorlds.CampaignSystem`, none of which is in `TAOM.Tests/Infrastructure/ClientAssemblies.cs:23-40`.
- `.claude/rules/csharp-architecture.md`: the rules table and "File Layout" (it puts Harmony patches
  in the feature's `Hooks/` folder; it names no place for a patch-only helper, so this plan puts
  `CultureStageViewType.cs` beside the three patches it serves). `.claude/rules/harmony-patches.md`: read the patch's registry section before
  editing it (`docs/reference/harmony-patch-registry.md:53-57`, "Patch7_FactionMap ... Faction map";
  nothing there constrains this move). `.claude/rules/gui-ui.md:161-164`: every `@Name` in a prefab
  matches a `[DataSourceProperty]` exactly, and every `[DataSourceProperty]` is bound.
  `.claude/rules/tests.md`: RED, GREEN, REFACTOR; names `MethodName_StateUnderTest_ExpectedBehavior`;
  MSTest plus NSubstitute.
- Nullable is enabled repo-wide (`Directory.Build.props:6`) but its warnings are off for these folders
  (root `.editorconfig:39-46`; none of the three folders has its own `.editorconfig`). `?` annotations
  are allowed and used in the patches already.
- The in-scope files have CRLF line endings: change them with the Edit tool only, never `sed -i`.
- Tests that run engine code (a `ViewModel` constructor, `TextObject.ToString`) need
  `[TestCategory("RequiresGame")]`: the RefAsm reference assemblies have no method bodies. A test that
  only names an engine enum may run without the game; Step 5 proves which.

## Commands you will need

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` (your dispatch rules give `<tmp>`; create the
folder first) and run it under `timeout 900`. Run every command from your worktree root.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)`, warnings not above the base's count |
| Tests (game-backed) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Passed!`, `Failed: 0`; Passed grows by exactly the new test results |
| One test class | the same plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run (a filter matching nothing proves nothing) |
| Binding gate (game-backed, `docs/ai-includes/agent-operating-manual.md:44`) | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | same totals and failure set as the base |
| RefAsm build (`.ai/verification.md`) | `dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0 |
| RefAsm unit step | `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the base's failure set, no new names |
| RefAsm binding gate | `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"` | the base's failure set, no new names |
| API snapshot (Part B) | `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` (after a game-backed `dotnet test`, which builds the Debug test DLL) | exit 0, no `DRIFT:` |
| Loc rows (Part C) | `python -B tools/check_orphan_loc_rows.py` | exit 0 |
| Data | `python -B tools/validate_moduledata.py` | exit 0, `0 error(s)` |
| Docs | `python -B tools/lint_docs.py --fail-on-drift` | exit 0 |
| Blast radius | `python tools/graphify_taom.py tests "<Type>"` | prints the `dotnet test --filter` line for the tests a change reaches |

Never `./build.ps1`: it deploys into the game install. Run the three RefAsm commands in order, all in
the Debug configuration; the next game-backed `dotnet test` rebuilds against the game.

## Scope

**In scope** (the only files you create, modify or delete):

Part A:
- create `Main/Features/LotrIssues/LotrIssueTemplateRules.cs`
- create `TAOM.Tests/Features/LotrIssues/LotrIssueTemplateRulesTests.cs`
- `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs`, `DeliverGoodsLotrIssue.cs`, `DeliverPersonnelLotrIssue.cs`
  (only `GetFrequency` and one line of `CanPlayerTakeQuestConditions` each)
- `Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs` (only `:112` and `:143-152`)
- `docs/features/lotr-issues.md` (in "Key Files (as built)": one new row, and the test-count row)

Part B:
- create `Main/Features/FactionMap/Hooks/CultureStageViewType.cs`
- create `TAOM.Tests/Features/FactionMap/CultureStageViewTypeTests.cs`
- `Main/Features/FactionMap/Hooks/CultureStageView_Constructor_Patch.cs`, `CultureStageView_Finalize_Patch.cs`,
  `CultureStageView_Tick_Patch.cs` (only the `using System;` line, lines 16 to 30 and the probe calls)

Part C:
- create `Main/Features/SupplyLines/UI/SupplyRowVM.cs`
- create `TAOM.Tests/Features/SupplyLines/SupplyRowVMTests.cs`
- delete `Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs` and `Main/Features/SupplyLines/UI/SupplyTroopRowVM.cs`
- `Main/Features/SupplyLines/UI/SupplyOrderScreenVM.cs` (only the seven lines listed above)
- `TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs` (only `:39-40` and `:187-188`)
- `TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs` (only the comment at `:160`)

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: single-owner. Nothing here needs them (the
  csproj is SDK-style and globs new `.cs` files; no registration changes). If you think one does, STOP and
  report the exact line.
- `Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs`, any `[SaveableField]` line, and the class
  declaration line of any template or quest class. No base class, no new class in any `IssueBase` or
  `QuestBase` hierarchy.
- `Main/Features/LotrIssues/LotrIssueService.cs:39` (`if (giver.RelationWithPlayer < def.RelationMin) continue;`):
  the offer-side relation gate, a different rule site in a TaleWorlds-free service. Leave it.
- The quest classes in the three template files, `EnsureDef`, `Tx`, `IssueStayAliveConditions`,
  `GetIssueEffectAmountInternal`: they differ between templates or touch instance state.
- `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml`, `Main/_Module/ModuleData/**`
  (strings and the 12 language files), `tools/translation_cache/**`: no localization change.
- `docs/features/lotr-issues.html`: a stale one-off export with no generator
  (`docs/reviews/rca-lotr-issues-deliver-personnel-2026-07-30.md:22`). Do not sync it with the `.md`.
- `docs/features/faction-map.md:22` says the Finalize patch is a "Postfix on `Finalize`"; the code is a
  Prefix on `OnFinalize`. A pre-existing doc error outside this plan: report it, do not fix it.
- `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`:
  protected; this plan needs no edit to them (no Step 0).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist or baseline entry. The only test edits allowed are the
  type names in `SupplyOrderPrefabBindingTests.cs` and the comment in `SupplyGoodsSearchTests.cs`.

## Git workflow

- Commit on the branch you were given; never push or open a PR. One commit per part, in the order A, B, C.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (it was `v2.0.38` at planning; a
  hook refuses any other). Suggested subjects (count them; each is under 72 with `v2.0.38`):
  - A: `refactor(lotr-issues): <version> - Share the templates' frequency rules`
  - B: `refactor(faction-map): <version> - One CultureStageView type probe`
  - C: `refactor(supply-lines): <version> - One row VM for goods and troops`
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72, no em or en dash. Never edit `CHANGELOG.md`. No AI attribution trailer. Write the message to a
  file in your scratch folder and run `git commit -F "<file>"`. Never `--no-verify`.
- Stage explicit paths only (`git add <path> <path>`; `git rm <path>` for the Part C deletions); never
  `-A`, `-u`, `--all` or `.`.
- Use trailers where they apply: `Not-tested:` (named per part below), `Save-compat:` for Part A.
  Each suggested trailer is one line under 72 characters; keep it that way.

## Steps

### Step 1: record the base

1. Run the drift check at the top. Read `Main/_Module/SubModule.xml` and note the version.
2. Open each file named in "Current state" at the cited lines and confirm the excerpts match. A
   mismatch is a STOP.
3. Run, in this order, and write each totals line and every failing test name into your report:
   the Build; the game-backed Tests; the game-backed Binding gate; the three RefAsm commands (build,
   unit step, binding gate); `python -B tools/check_orphan_loc_rows.py`; `python -B tools/lint_docs.py --fail-on-drift`.
4. Run `python tools/graphify_taom.py status --brief` and act as "Blast radius" says.

**Verify**: the game-backed Tests line reads `Passed!` with `Failed: 0`, and its totals match
"Baseline at that commit" (`Passed: 39250, Skipped: 9, Total: 39259`) or the difference is explained by
commits the drift check listed. The RefAsm and binding-gate failure sets are recorded by name (they are
the comparison for every later run; failures there are not yours unless their names are new).

### Step 2 (Part A, RED): write the rules tests against a throwing helper

Create `TAOM.Tests/Features/LotrIssues/LotrIssueTemplateRulesTests.cs` with this content (a draft:
re-check every expected value against the old code quoted in "Current state" before you rely on it):

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

/// <summary>
/// The rules the three issue templates share: the frequency map (LotrIssuesCampaignBehavior uses it too
/// when it offers an issue) and the relation floor a player must reach to take the quest. Expected values
/// are the templates' code before the fold.
/// </summary>
[TestClass]
public class LotrIssueTemplateRulesTests
{
    private static LotrIssueDefinition Def(IssueFrequencyTier frequency = IssueFrequencyTier.Common, int relationMin = -10)
        => new LotrIssueDefinition("lotr_x", LotrIssueTemplate.DeliverGoods, IssueGiverOccupation.Headman, frequency,
            new List<string>(), 1, 0f, "", "", 0, 0f, 0, "", "", relationMin,
            new LotrIssueText("", "", "", "", "", "", "", "", ""));

    [DataTestMethod]
    [DataRow(IssueFrequencyTier.VeryCommon, IssueBase.IssueFrequency.VeryCommon)]
    [DataRow(IssueFrequencyTier.Common, IssueBase.IssueFrequency.Common)]
    [DataRow(IssueFrequencyTier.Rare, IssueBase.IssueFrequency.Rare)]
    public void MapFrequency_EachTier_MapsToTheEngineFrequency(IssueFrequencyTier tier, IssueBase.IssueFrequency expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.MapFrequency(tier));

    [TestMethod]
    public void MapFrequency_UndefinedTierValue_FallsBackToCommon()
        => Assert.AreEqual(IssueBase.IssueFrequency.Common, LotrIssueTemplateRules.MapFrequency((IssueFrequencyTier)99));

    [TestMethod]
    public void FrequencyOf_NoDefinition_IsCommon()
        => Assert.AreEqual(IssueBase.IssueFrequency.Common, LotrIssueTemplateRules.FrequencyOf(null));

    [TestMethod]
    public void FrequencyOf_Definition_MapsItsTier()
        => Assert.AreEqual(IssueBase.IssueFrequency.Rare, LotrIssueTemplateRules.FrequencyOf(Def(IssueFrequencyTier.Rare)));

    [DataTestMethod]
    [DataRow(4, true)]
    [DataRow(5, false)]
    [DataRow(6, false)]
    public void IsBelowRelationMin_DefinitionFloorOfFive_IsStrictlyBelow(int relation, bool expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.IsBelowRelationMin(relation, Def(relationMin: 5)));

    [DataTestMethod]
    [DataRow(-11, true)]
    [DataRow(-10, false)]
    public void IsBelowRelationMin_NoDefinition_UsesMinusTen(int relation, bool expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.IsBelowRelationMin(relation, null));
}
```

Create `Main/Features/LotrIssues/LotrIssueTemplateRules.cs` as a skeleton whose three methods each
`throw new System.NotImplementedException();` and whose signatures are exactly those in Step 3, plus the
`DefaultRelationMin` constant.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~LotrIssueTemplateRulesTests"`
builds and reports `Failed!` with every result of `LotrIssueTemplateRulesTests` failing on
`System.NotImplementedException` (11 results: 3 + 1 + 1 + 1 + 3 + 2 data rows and methods). Quote the
totals line. A build error is not this RED: fix the test or the skeleton until it compiles.

### Step 3 (Part A, GREEN): implement the helper

Replace the skeleton with:

```csharp
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Rules the three issue templates (Combat, DeliverGoods, DeliverPersonnel) share, and the frequency map
/// <see cref="LotrIssuesCampaignBehavior"/> uses when it offers an issue. A static helper, never a base
/// class: the save system keys each saved field by the class level of its declaring type, so a class
/// between IssueBase and a template would change the id of the template's saved _defId. The engine's
/// IssueBase.PreconditionFlags is protected, so each template still builds its own flags.
/// </summary>
internal static class LotrIssueTemplateRules
{
    /// <summary>The relation floor when a template has no definition.</summary>
    internal const int DefaultRelationMin = -10;

    /// <summary>VeryCommon and Rare map to themselves; every other value maps to Common.</summary>
    internal static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)
    {
        switch (tier)
        {
            case IssueFrequencyTier.VeryCommon: return IssueBase.IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueBase.IssueFrequency.Rare;
            default: return IssueBase.IssueFrequency.Common;
        }
    }

    /// <summary>A template's frequency: Common when its definition did not resolve.</summary>
    internal static IssueBase.IssueFrequency FrequencyOf(LotrIssueDefinition? def)
        => def == null ? IssueBase.IssueFrequency.Common : MapFrequency(def.Frequency);

    /// <summary>True when the relation is strictly below the definition's floor, or below -10 with no definition.</summary>
    internal static bool IsBelowRelationMin(int relationWithPlayer, LotrIssueDefinition? def)
        => relationWithPlayer < (def?.RelationMin ?? DefaultRelationMin);
}
```

**Verify**: the Step 2 filter reports `Passed!` with the same 11 results, `Failed: 0`.

### Step 4 (Part A): route the four copies through the helper

1. In each of the three templates, replace the body of `GetFrequency` so the method reads:

   ```csharp
       public override IssueFrequency GetFrequency()
       {
           EnsureDef();
           return LotrIssueTemplateRules.FrequencyOf(_def);
       }
   ```

2. In each of the three templates, change only the relation line of `CanPlayerTakeQuestConditions`
   (`CombatLotrIssue.cs:132`, `DeliverGoodsLotrIssue.cs:150`, `DeliverPersonnelLotrIssue.cs:132`) from
   `if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))` to
   `if (LotrIssueTemplateRules.IsBelowRelationMin(issueGiver.GetRelationWithPlayer(), _def))`.
   Every other line of the override stays as quoted: the four out-parameter defaults, `EnsureDef()` before
   the relation line, `relationHero = issueGiver` only inside the relation branch, the at-war test with its
   `issueGiver.CurrentSettlement != null &&` short-circuit (so `Hero.MainHero.MapFaction` is still read only
   when the giver has a settlement), and `return flag == PreconditionFlags.None;`. `GetRelationWithPlayer()`
   is still evaluated exactly once.
3. In `LotrIssuesCampaignBehavior.cs`, change `:112` to
   `new PotentialIssueData(OnSelected, type, LotrIssueTemplateRules.MapFrequency(def.Frequency), def));`
   and delete the private `MapFrequency` method (`:144-152`) and the blank line before it, so the class
   ends with `TemplateType` and its closing brace.
4. No `using` is needed: the templates' namespace `TAOM.Features.LotrIssues.Templates` sits inside
   `TAOM.Features.LotrIssues`, and the behavior is in `TAOM.Features.LotrIssues`.

**Verify**:
- The Build exits 0 with no new warning naming an in-scope file.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Features.LotrIssues"`
  reports `Passed!`, `Failed: 0`.
- `git grep --untracked -n -E "IssueFrequencyTier\.(VeryCommon|Rare)|\?\? -10" -- Main Client` lists only
  `Main/Features/LotrIssues/LotrIssueTemplateRules.cs` (two lines, the two `case` labels).
  `--untracked` is required: the helper is not staged yet, and plain `git grep` searches tracked files
  only, so without it the command prints nothing even on a correct change.
- This command prints nothing (exit 1):
  `git diff 7087fa633 -- Main/Features/LotrIssues | grep -E '^[-+]' | grep -v -E '^(\+\+\+|---) ' | grep -E 'SaveableField|class |LotrIssueSaveableTypeDefiner'`.
  It checks only added and removed lines. Do not read the raw `git diff` for `class `: each hunk header
  repeats the enclosing class declaration (for example `@@ -129,7 +123,7 @@ public class CombatLotrIssue : IssueBase`),
  which is not a change. The untracked helper does not appear in `git diff` at this point.

### Step 5 (Part A): RefAsm check, doc row, full suite, commit

1. Run the three RefAsm commands (build, unit step, binding gate). Then prove the new tests run off the
   game: run the RefAsm unit step again with the class added to its filter,
   `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification&FullyQualifiedName~LotrIssueTemplateRulesTests"`.
   It must report `Passed!` with `Failed: 0, Passed: 11` and `Total: 11`. (A plain run of the unit step
   prints only failures and the totals line, so it cannot show that these 11 ran; this filtered run can.)
   If they fail there with a symptom `.ai/verification.md` lists for an untagged game-bound test (a
   `TypeInitializationException`, a `FileNotFoundException` for a module assembly, a
   `NullReferenceException` from a TaleWorlds frame), add `[TestCategory("RequiresGame")]` to the class,
   rebuild, rerun, and say so in your report: the filtered RefAsm run then matches no test, which is
   expected only in this case, and the game-backed Step 2 filter must still report `Passed: 11`. Any
   other failure is a STOP.
2. In `docs/features/lotr-issues.md`, "Key Files (as built)" table (`:387` onward):
   - add this row directly after the `Templates/{DeliverGoods,DeliverPersonnel,Combat}LotrIssue.cs` row:
     `| Main/Features/LotrIssues/LotrIssueTemplateRules.cs | rules the 3 templates share: the frequency map (the behavior's too) and the relation floor for taking a quest (-10 with no definition); static, so no class enters the IssueBase hierarchy (save ids) |`
     with the path in backticks like its neighbours;
   - in the last row (`:401`, `TAOM.Tests/Features/LotrIssues/*`), the count is stale after this part.
     Replace its Purpose cell `config-provider + service + suppression tests (84 [TestMethod] methods, plus two data-driven ones)`
     (with backticks around `[TestMethod]` as now) by
     `config-provider, service, suppression, template-invariant and template-rules tests (87 [TestMethod] methods, plus five data-driven ones)`,
     keeping the backticks. Prove the two numbers first:
     `git grep --untracked -c "\[TestMethod\]" -- TAOM.Tests/Features/LotrIssues` must give
     26, 38, 20 and 3 (sum 87; the invariants file has none), and
     `git grep --untracked -c "\[DataTestMethod\]" -- TAOM.Tests/Features/LotrIssues` must give 1, 1 and 3
     (sum 5). If the sums differ, write the sums you get.
3. Run the full game-backed Tests and `python -B tools/lint_docs.py --fail-on-drift`.
4. Commit Part A. Stage exactly these seven paths:
   `git add Main/Features/LotrIssues/LotrIssueTemplateRules.cs TAOM.Tests/Features/LotrIssues/LotrIssueTemplateRulesTests.cs Main/Features/LotrIssues/Templates/CombatLotrIssue.cs Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs docs/features/lotr-issues.md`.
   Body draft (re-check each claim before using it):
   "The Combat, DeliverGoods and DeliverPersonnel issue templates each carried the same frequency map
   and the same relation floor for taking a quest, and the behavior that offers issues carried a fourth
   copy of the map. They now call one static helper with its first unit tests. Nothing a player sees
   changes: VeryCommon and Rare map as before, everything else is Common, and the relation floor stays
   -10 with no definition and strictly below." Trailers:
   `Save-compat: no class enters the IssueBase hierarchy; no field moves`,
   `Not-tested: the template overrides in game (they need a live Hero)`.

**Verify**: the full Tests line reads `Passed!`, `Failed: 0`, Passed = base + 11 (or the RefAsm-tag
change explained); RefAsm sets have no new failing name; lint exit 0; `git log -1 --stat` lists exactly
the seven Part A paths above; `git status --porcelain` prints nothing.

### Step 6 (Part B, RED): pin the probe names

Create `TAOM.Tests/Features/FactionMap/CultureStageViewTypeTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// The three CultureStageView patches find the engine view by these names, first match wins, so a rename
/// or a reorder changes which view they patch. The binding gate (HarmonyPatchBindingTests) proves that the
/// names resolve against the installed engine.
/// </summary>
[TestClass]
public class CultureStageViewTypeTests
{
    [TestMethod]
    public void PossibleTypeNames_AreTheTwoEngineNamesInProbeOrder()
    {
        CollectionAssert.AreEqual(new[]
        {
            "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
            "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
        }, CultureStageViewType.PossibleTypeNames);
    }
}
```

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and its errors name
`CultureStageViewType` in `CultureStageViewTypeTests.cs` (the type does not exist yet). Quote the error
lines. Any other error is yours to fix first.

### Step 7 (Part B, GREEN): one probe, three callers, gates, commit

1. Create `Main/Features/FactionMap/Hooks/CultureStageViewType.cs`:

   ```csharp
   using System;
   using HarmonyLib;

   namespace TAOM.Features.FactionMap.Hooks;

   /// <summary>
   /// Finds the engine's character-creation culture stage view for the three CultureStageView patches. It
   /// lives in SandBox.GauntletUI or SandBox.View, which TAOM cannot reference at compile time, so each name
   /// is tried in order and the first that resolves wins. Not cached: every Prepare and TargetMethod call
   /// probes again. It carries no client-only marker: ServerLoadGateTests allows that attribute only on
   /// Harmony patch classes.
   /// </summary>
   internal static class CultureStageViewType
   {
       internal static readonly string[] PossibleTypeNames =
       {
           "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
           "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
       };

       internal static Type? Find()
       {
           foreach (var typeName in PossibleTypeNames)
           {
               var type = AccessTools.TypeByName(typeName);
               if (type != null) return type;
           }
           return null;
       }
   }
   ```

2. In each of the three patch files: delete lines 16 to 30 (the `PossibleTypeNames` array and
   `FindCultureStageViewType`, with the blank line after them), replace each call
   `FindCultureStageViewType()` with `CultureStageViewType.Find()` (two per file: in `Prepare` and in
   `TargetMethod`), and delete `using System;` (nothing else in these files uses it after the move). Keep
   everything else byte-identical: `[HarmonyPatch]`, `[HarmonyPatchCategory("Patch7_FactionMap")]`,
   `[ClientOnly]`, `public static class`, the `_hook` field, `Initialize`, `static bool Prepare()`,
   `static MethodBase? TargetMethod()` with its per-patch body, the comment block and the Prefix or Postfix.

**Verify**:
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CultureStageViewTypeTests"`
  reports `Passed!` with `Passed: 1` and `Total: 1`.
- The Build exits 0 with no new warning naming an in-scope file.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ClientFrontEndCompositionTests|FullyQualifiedName~ServerLoadGateTests|FullyQualifiedName~TAOM.Tests.Features.FactionMap"`
  reports `Passed!`, `Failed: 0` (with no line naming `CultureStageViewType` as `MISPLACED`).
- The game-backed Binding gate matches the base's totals and failure set, and its output has no failure
  naming a `CultureStageView_` patch.
- `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` exits 0 with no `DRIFT:`.
- `git grep --untracked -n -E "PossibleTypeNames|FindCultureStageViewType" -- Main Client TAOM.Tests` lists only
  `Main/Features/FactionMap/Hooks/CultureStageViewType.cs` and
  `TAOM.Tests/Features/FactionMap/CultureStageViewTypeTests.cs`. `--untracked` is required: both files
  are not staged yet, and plain `git grep` would print nothing.

Then run the full game-backed Tests and commit Part B. Stage exactly these five paths:
`git add Main/Features/FactionMap/Hooks/CultureStageViewType.cs TAOM.Tests/Features/FactionMap/CultureStageViewTypeTests.cs Main/Features/FactionMap/Hooks/CultureStageView_Constructor_Patch.cs Main/Features/FactionMap/Hooks/CultureStageView_Finalize_Patch.cs Main/Features/FactionMap/Hooks/CultureStageView_Tick_Patch.cs`.
Body draft: "The three
Harmony patches on the character-creation culture stage view each carried the same list of engine type
names and the same lookup. They now share one helper, so the next engine rename of the view is one
edit and cannot leave one patch silently disabled. The patches, their targets and their category are
unchanged; the binding gate resolves each target against the installed engine." Trailer:
`Not-tested: Harmony applying the three patches in game`.

**Verify**: full Tests `Passed!`, `Failed: 0`, Passed = Step 5's count + 1; `git log -1 --stat` lists
exactly the five Part B paths above; `git status --porcelain` prints nothing.

### Step 8 (Part C): characterize the two labels on the old classes

Create `TAOM.Tests/Features/SupplyLines/SupplyRowVMTests.cs`, written against the OLD classes so the
expected strings are proven on today's code before anything moves:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.UI;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The goods and troops lists share one row VM; only the label differs. Each form keeps its own
/// registered key and count variable ({=taom_sl_good_row} with STOCK, {=taom_sl_troop_row} with COUNT),
/// or the translated rows would show an unfilled count. A ViewModel and TextObject.ToString run engine
/// code, so the class needs the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SupplyRowVMTests
{
    private static SupplyLineItem Line(string id, string name, int available, int unitPrice)
        => new SupplyLineItem { Id = id, Name = name, Available = available, UnitPrice = unitPrice };

    [TestMethod]
    public void ForGood_Label_FillsNameStockAndPrice()
        => Assert.AreEqual("Grain - 5 in stock, 10 denars", new SupplyGoodRowVM(Line("grain", "Grain", 5, 10), () => { }).Label);

    [TestMethod]
    public void ForTroop_Label_FillsNameCountAndPrice()
        => Assert.AreEqual("Militia - 4 available, 50 denars", new SupplyTroopRowVM(Line("militia", "Militia", 4, 50), () => { }).Label);

    [TestMethod]
    public void ForGood_NegativeStockAndNoName_ClampsAndFallsBackToTheId()
    {
        var row = new SupplyGoodRowVM(Line("grain", null!, -3, 10), () => { });
        Assert.AreEqual(0, row.Available);
        Assert.AreEqual("grain - 0 in stock, 10 denars", row.Label);
    }

    [TestMethod]
    public void ExecutePlus_RaisesQtyTextBeforeTheCallback()
    {
        var events = new List<string>();
        var row = new SupplyTroopRowVM(Line("militia", "Militia", 4, 50), () => events.Add("callback"));
        row.PropertyChanged += (_, e) => events.Add(e.PropertyName!);
        row.ExecutePlus();
        CollectionAssert.AreEqual(new[] { "QtyText", "callback" }, events);
        Assert.AreEqual("1", row.QtyText);
    }
}
```

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyRowVMTests"`
reports `Passed!` with 4 results. If an exact label assertion fails on this OLD code, the oracle is
wrong, not the code: replace the expected string with the old code's actual `Label` only if that output
holds the name (or id), the count and the price with no `{` left in it; otherwise STOP (the game-backed
run does not render TextObjects the way this plan assumes).

### Step 9 (Part C, RED): point the tests at the folded type

In `SupplyRowVMTests.cs`, replace `new SupplyGoodRowVM(` with `SupplyRowVM.ForGood(` and
`new SupplyTroopRowVM(` with `SupplyRowVM.ForTroop(` (four places; the arguments stay). Change nothing else.

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and its errors name
`SupplyRowVM` in `SupplyRowVMTests.cs`. Quote the error lines.

### Step 10 (Part C, GREEN): one row VM, its users, gates, commit

1. Create `Main/Features/SupplyLines/UI/SupplyRowVM.cs`. Copy `SupplyGoodRowVM.cs` from "Current state"
   and change only the summary, the class name, the constructor and two new factories, so the top of the
   class reads:

   ```csharp
   /// <summary>
   /// One purchasable good or recruitable troop at the selected source; the goods and troops lists differ
   /// only in the label. Quantity is clamped to [0, Available]; every change calls back into the parent VM's
   /// Recompute so the price lines and the one-source lock stay current. Gauntlet binds an item template by
   /// property and command name on the runtime type, so both lists share this one type.
   /// </summary>
   public sealed class SupplyRowVM : ViewModel
   {
       private readonly Action _onChanged;
       private readonly string _label;
       private int _qty;

       internal static SupplyRowVM ForGood(SupplyLineItem item, Action onChanged)
           => new SupplyRowVM(item, onChanged, "{=taom_sl_good_row}{NAME} - {STOCK} in stock, {PRICE} denars", "STOCK");

       internal static SupplyRowVM ForTroop(SupplyLineItem item, Action onChanged)
           => new SupplyRowVM(item, onChanged, "{=taom_sl_troop_row}{NAME} - {COUNT} available, {PRICE} denars", "COUNT");

       private SupplyRowVM(SupplyLineItem item, Action onChanged, string labelText, string countVariable)
       {
           _onChanged = onChanged;
           ItemId = item?.Id ?? string.Empty;
           Available = Math.Max(0, item?.Available ?? 0);
           UnitPrice = item?.UnitPrice ?? 0;

           var label = new TextObject(labelText);
           label.SetTextVariable("NAME", item?.Name ?? ItemId);
           label.SetTextVariable(countVariable, Available);
           label.SetTextVariable("PRICE", UnitPrice);
           _label = label.ToString();
       }
   ```

   Everything from `public string ItemId { get; }` to the end of the file is copied unchanged from
   `SupplyGoodRowVM.cs:32-79`: the same public members, the `Qty` setter calling
   `OnPropertyChanged(nameof(QtyText))` before `_onChanged?.Invoke()`, and `[DataSourceProperty]` on
   `QtyText` and `Label` only. The factories are `internal static` (Gauntlet collects instance members
   only, `ViewModel.cs:621`, `:626`; `SupplyOrderScreenVM` and the tests see internals).
   Compare each literal with `Main/_Module/ModuleData/taom_module_strings.xml:2812` and `:2830`: the text
   after `{=key}` must match the registered English byte for byte.
2. In `SupplyOrderScreenVM.cs`: `:31` and `:32` become `MBBindingList<SupplyRowVM>` on both sides; `:256`
   and `:259` return `MBBindingList<SupplyRowVM>`; `:660` and `:664` become
   `_goods.Add(SupplyRowVM.ForGood(preferred, Recompute));` and `_goods.Add(SupplyRowVM.ForGood(line, Recompute));`;
   `:753` becomes `_troops.Add(SupplyRowVM.ForTroop(line, Recompute));`. Keep `[DataSourceProperty]` and
   the property names `Goods` and `Troops`.
3. Delete both old files: `git rm Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs Main/Features/SupplyLines/UI/SupplyTroopRowVM.cs`.
4. In `SupplyOrderPrefabBindingTests.cs`: `:39` becomes `["Goods"] = typeof(SupplyRowVM),`, `:40` becomes
   `["Troops"] = typeof(SupplyRowVM),`, and the two lines `:187-188` become the one line `typeof(SupplyRowVM),`.
5. In `SupplyGoodsSearchTests.cs:160`, change only `SupplyGoodRowVM` to `SupplyRowVM` in the comment.

**Verify**:
- The Build exits 0 with no new warning naming an in-scope file.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Features.SupplyLines"`
  reports `Passed!`, `Failed: 0`.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyRowVMTests|FullyQualifiedName~SupplyOrderPrefabBindingTests|FullyQualifiedName~SupplyOrderScreenVMTests"`
  reports `Passed!` with `Failed: 0, Passed: 68` and `Total: 68`: 4 new `SupplyRowVMTests`, 4
  `SupplyOrderPrefabBindingTests` and 60 `SupplyOrderScreenVMTests`. (Default output prints only
  failures and the totals line, so the exact total is what proves all three classes ran.) The 4 and 60
  are `git grep -c "\[TestMethod\]"` of those two files at `7087fa633`, which have no data rows; if the
  drift check listed either file, recount with that command.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~LocalizationKeyConsistencyTests"`
  reports `Passed!`, `Failed: 0`. Its `EveryCSharpDefault_OfARegisteredTaomKey_MatchesTheRegisteredEnglish`
  (`TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs:258`) fails when a whole
  `"{=taom_*}..."` C# literal, the factory arguments included, differs from its registered English: the
  machine check behind the manual comparison in sub-step 1.
- `git grep --untracked -n -E "SupplyGoodRowVM|SupplyTroopRowVM" -- . ':!plans' ':!docs/reviews/codex-adversarial-supply-search-2026-09-13.prompt.md'` returns nothing.
- `git grep --untracked -n -F "{=taom_sl_good_row}" -- Main Client` and
  `git grep --untracked -n -F "{=taom_sl_troop_row}" -- Main Client`
  each list `Main/Features/SupplyLines/UI/SupplyRowVM.cs` plus the registered row in
  `Main/_Module/ModuleData/taom_module_strings.xml` (`:2812`, `:2830`), nothing else. `--untracked` is
  required: `SupplyRowVM.cs` is not staged yet, and plain `git grep` would list only the strings row.
- `python -B tools/check_orphan_loc_rows.py` exits 0.

Then run the full game-backed Tests and commit Part C. The two deletions are already staged by
`git rm`; stage the other five paths with
`git add Main/Features/SupplyLines/UI/SupplyRowVM.cs TAOM.Tests/Features/SupplyLines/SupplyRowVMTests.cs Main/Features/SupplyLines/UI/SupplyOrderScreenVM.cs TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs`.
Body draft: "The supply order screen's goods and troops lists used two row view models that were copies
except for the label text. They now share one, so a fix to the quantity clamp or the price callback
reaches both lists. Gauntlet binds a list's rows by property and command name on the runtime type, so
the screen binds as before, and both labels keep their translated keys and text." Trailer:
`Not-tested: the supply order screen in game`.

**Verify**: full Tests `Passed!`, `Failed: 0`, Passed = Step 7's count + 4; `git log -1 --stat` lists
exactly the seven Part C paths (the five above and the two deletions); `git status --porcelain` prints
nothing.

### Step 11: final verification

Run, in this order: the Build; the full game-backed Tests; the game-backed Binding gate; the three RefAsm
commands; `python -B tools/validate_moduledata.py`; `python -B tools/lint_docs.py --fail-on-drift`;
`git status --porcelain`; `git log --oneline 7087fa633..HEAD`.

After the RefAsm unit step, run it once more with `&FullyQualifiedName~CultureStageViewTypeTests` added
to its filter (the form Step 5.1 shows). It must report `Passed!` with `Passed: 1` and `Total: 1`: Part
B's new test is untagged, and this is the first RefAsm run that reaches it. If it fails with one of the
untagged game-bound symptoms Step 5.1 names, apply Step 5.1's rule (tag the class, rebuild, rerun, report
it, commit the tag as a separate Part B fix-up commit); any other failure is a STOP.

**Verify**: every result in "Done criteria" holds. Write the totals lines into your report beside the
Step 1 lines.

## Test plan

- New: `TAOM.Tests/Features/LotrIssues/LotrIssueTemplateRulesTests.cs`: the frequency map over every
  `IssueFrequencyTier` value and an undefined value; the null-definition frequency; a definition's
  frequency; the relation floor below, at and above a floor of 5; the null-definition floor at -11 and -10.
  Model: `TAOM.Tests/Features/LotrIssues/LotrIssueServiceTests.cs` (definition builder, MSTest).
- New: `TAOM.Tests/Features/FactionMap/CultureStageViewTypeTests.cs`: the probe names and their order.
  Model: `TAOM.Tests/Features/FactionMap/CultureStageViewFinalizeHookTests.cs`.
- New: `TAOM.Tests/Features/SupplyLines/SupplyRowVMTests.cs` (`RequiresGame`): each label form renders
  its own wording and count, the clamp and id fallback, and the property-change-then-callback order.
  Model: `TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs`.
- Existing proof per part: Part A, the LotrIssues test folder; Part B, `HarmonyPatchBindingTests` in the
  binding gate, `ClientFrontEndCompositionTests`, `ServerLoadGateTests`, the API snapshot check; Part C,
  `SupplyOrderPrefabBindingTests` and `SupplyOrderScreenVMTests` (both `RequiresGame`, so only the
  game-backed run proves them; the RefAsm unit step skips them).
- Not testable here, named in each commit's `Not-tested:` trailer: the template overrides called by the
  engine with a live `Hero`; Harmony applying the three patches in game; the order screen in game.
- Expected totals: Passed grows by 11 (Part A), 1 (Part B) and 4 (Part C) results over the Step 1 base;
  Failed stays 0; Skipped unchanged.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The Build exits 0 with `0 Error(s)` and no warning naming an in-scope file that the base did not have
- [ ] The game-backed Tests line reads `Passed!`, `Failed: 0`, with Passed = the Step 1 base + 16 and
      Skipped unchanged (or each difference explained: a part skipped, a drift commit)
- [ ] The game-backed and RefAsm binding gates and the RefAsm unit step show the Step 1 failure sets and no new name
- [ ] `git grep -n -E "IssueFrequencyTier\.(VeryCommon|Rare)|\?\? -10" -- Main Client` lists only `Main/Features/LotrIssues/LotrIssueTemplateRules.cs`
- [ ] `git grep -n -E "PossibleTypeNames|FindCultureStageViewType" -- Main Client TAOM.Tests` lists only `CultureStageViewType.cs` and `CultureStageViewTypeTests.cs`
- [ ] `git grep -n -E "SupplyGoodRowVM|SupplyTroopRowVM" -- . ':!plans' ':!docs/reviews/codex-adversarial-supply-search-2026-09-13.prompt.md'` returns nothing
- [ ] `git diff 7087fa633 -- Main/Features/LotrIssues ':!Main/Features/LotrIssues/LotrIssueTemplateRules.cs' | grep -E '^[-+]' | grep -v -E '^(\+\+\+|---) ' | grep -E 'SaveableField|class |LotrIssueSaveableTypeDefiner'`
      prints nothing (exit 1; the new helper is excluded because its own `class` line is an addition),
      and `git diff --stat 7087fa633 -- Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs` prints nothing
- [ ] `git grep -n -E "^\s*\[(ClientOnly|HarmonyPatch)|TargetMethods?\s*\(" -- Main/Features/FactionMap/Hooks/CultureStageViewType.cs`
      returns nothing (no patch attribute, no `TargetMethod` member)
- [ ] `python -B tools/check_orphan_loc_rows.py`, `python -B tools/validate_moduledata.py` and
      `python -B tools/lint_docs.py --fail-on-drift` exit 0
- [ ] `git status --porcelain` is empty, and `git log --oneline 7087fa633..HEAD` shows the three commits
      (fewer only for a part you skipped and reported; a fourth only for the Step 11 RefAsm tag fix-up)
- [ ] Every comment, doc line, commit body and test oracle this plan supplied was re-checked against the code it describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- Part A seems to need a base class, a new class in the `IssueBase` hierarchy, a change to a
  `[SaveableField]` or to `LotrIssueSaveableTypeDefiner.cs`, or a helper that names `PreconditionFlags`.
- The relation line cannot be swapped without moving the at-war test, the out-parameter defaults or the
  `relationHero` assignment.
- Step 5's RefAsm run fails the new LotrIssues tests with anything other than the documented
  untagged-game-test symptoms.
- The binding gate fails for a `CultureStageView_` patch, `ServerLoadGateTests` reports `MISPLACED`,
  `MISSING` or `STALE` for a FactionMap class, or `HarmonyPatchBindingTests` lists `CultureStageViewType`
  as a patch type.
- Step 8's label oracle cannot be fixed under the rule given there, or `SupplyOrderPrefabBindingTests`
  fails after Step 10.
- `check_orphan_loc_rows.py` or `LocalizationKeyConsistencyTests` reports `taom_sl_good_row` or `taom_sl_troop_row`, or the fold would need a
  change to a strings XML, a language file or the prefab.
- `EntryPointLineCountGateTests`, `ServiceSignatureGateTests`, `BannedConstructsGateTests` or
  `ServerJitGateTests` fails with a name from this plan.
- The change seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` or a protected file.
- A TaleWorlds signature or behaviour differs from "Current state"; report the mismatch.
- A step's verification fails twice after a reasonable fix.

A part whose conditions cannot be met is skipped and reported; the other parts still land. To skip a
part after you edited its files, put each edited file back to its `7087fa633` content (read it with
`git show 7087fa633:<path>` and restore it with the Edit or Write tool; never `git restore`, `git checkout`
or `git stash`), delete the files that part created, confirm `git status --porcelain -- <that part's paths>`
prints nothing, and continue with the next part.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (none exists for this work).
- `/localize`: not needed. No new player-facing text: the two row literals stay byte-identical and registered.
- Feature docs: no new feature. Part A adds one Key Files row to `docs/features/lotr-issues.md`; no
  `docs/reference/feature-map.md` row names any changed file (checked with `git grep` at `7087fa633`).
- Run `/deep-review` on the branch before merge (C# change).
- Follow-up candidate (not this plan): `docs/features/faction-map.md:22` calls the Finalize patch a
  Postfix on `Finalize`; it is a `[HarmonyPrefix]` on `OnFinalize` (`CultureStageView_Finalize_Patch.cs:37`, `:43-44`).

## After merge: the maintainer's actions

None. No hook, setting, save format or game data changes.

## Maintenance notes

- `IssueBase.PreconditionFlags` is protected, so the three `CanPlayerTakeQuestConditions` overrides stay
  17-line near-copies around one shared call. A new precondition (a skill, gold) still needs three edits;
  if that recurs, a TAOM-owned result type the templates map to flags is the next step, never a base class.
- `LotrIssueService.cs:39` applies the relation floor on the offer side (`giver.RelationWithPlayer < def.RelationMin`,
  no -10 default because the definition is never null there). A change to the floor's meaning must touch
  both sites.
- `CultureStageViewType.PossibleTypeNames` is the place to edit after an engine rename of the culture stage
  view; `CultureStageViewTypeTests` pins its order and the binding gate proves it resolves.
- A third supply list with its own wording would be a third factory on `SupplyRowVM`, plus a row in the
  prefab binding test's map.
- The review should probe: the at-war short-circuit and `relationHero` handling in all three overrides;
  that `CultureStageViewType` carries no `[ClientOnly]`, `[HarmonyPatch]` or `TargetMethod`; that the two
  row literals are byte-identical to `taom_module_strings.xml:2812` and `:2830`; that `OnPropertyChanged`
  still precedes `_onChanged` in `SupplyRowVM.Qty`.
