# Plan 061: Party-screen prisoner recruits pay their recruit_cost, clamped to the balance

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat db5680c8a..HEAD -- Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs Main/Features/SpecialResources/ISpecialResourceService.cs Main/Features/SpecialResources/SpecialResourceService.cs Main/Features/SpecialResources/SpecialResourceMessages.cs Main/Features/SpecialResources/SpecialResourcesBehavior.cs TAOM.Tests/Features/SpecialResources/SpecialResourcePrisonerRecruitGateTests.cs TAOM.Tests/Features/SpecialResources/SpecialResourceGoldOnlyTests.cs TAOM.Tests/Features/SpecialResources/SpecialResourceMessagesTests.cs TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/special-resources.md docs/features/dire-wolf.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md Main/_Module/ModuleData/special_resources/troop_resource_costs.xml TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorLineCountTests.cs Main/Features/SpecialResources/SpecialResourceStorageService.cs Main/Features/SpecialResources/SpecialResourceConfigProvider.cs`
> (the 18 Scope paths, one of them new, plus three files "Current state" quotes but this plan does not edit).
> If one of them changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (a Harmony prefix on a party-screen command that can now return `false`)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `db5680c8a`, 2026-11-19
- **Baseline at that commit**: build exit 0; dotnet 43028 passed, 3 failed, 9 skipped. Failing: the
  three language-file tests `EveryKey_IsSeededInAllTwelveLanguageFiles`,
  `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems` (the
  translation XMLs are the project lead's and lag the English source). Python suite: 10549 ran,
  0 failed, 13 skipped. `python tools/lint_docs.py --fail-on-drift` exit 0 (the writer ran it).
- **Issue**: #563 (open). The maintainer approved option A on 2026-11-19.

## Why this matters

A captured troop that carries a `recruit_cost` (spider 5, Harad elephant rider 50, Mumakil rider
2000, cave and hill trolls 50, Ironpass rams 10 to 18) can be recruited from the party screen's
prisoner list at any special-resource balance, including zero. The charge fires later, at Done, and
the storage floors at zero, so the player keeps the troop and pays whatever was left, possibly
nothing. The volunteer screen already blocks an unaffordable cart (Patch51), so the prisoner list is
the one free route. After this plan the party screen clamps a prisoner recruit to the number the
balance covers (after the upgrades queued and the prisoners already recruited in the same screen),
tells the player with a yellow line, and refuses it outright when not even one is affordable.

## Current state

All excerpts are from the writer's own reads at `db5680c8a`.

### The files

- `Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` (78 lines): Patch26
  prefix on `PartyScreenLogic.AddCommand`. Clamps `UpgradeTroop` only; the bug is line 31.
- `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs`: the hook interface the
  three Patch26 classes use. One implementer only: `PartyUpgradeResourceCheckHook`.
- `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs`: the implementer; delegates
  to `ISpecialResourceService` and `ISpecialResourceConfigProvider`. Already registered in
  `SpecialResourcesIoC.cs:21` and already handed to the patch by `Main/SubModule.cs:1936`
  (`PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger)`), so no wiring changes.
- `Main/Features/SpecialResources/ISpecialResourceService.cs` and `SpecialResourceService.cs`: the
  decisions. One implementer (`SpecialResourceService`); every test fake is an NSubstitute substitute.
- `Main/Features/SpecialResources/SpecialResourceMessages.cs`: pure `TextObject` builders for the
  player-facing resource lines.
- `Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs`: Patch51, owner of the
  `{=taom_recruit_needs_resource}` text this plan reuses.
- `Main/Features/SpecialResources/SpecialResourcesBehavior.cs`: charges `recruit_cost` on
  `OnUnitRecruited`. Only a comment changes. **It is exactly 276 lines, the ceiling that
  `SpecialResourcesBehaviorLineCountTests` pins (`private const int Ceiling = 276;`).**

### The bug

`PartyScreenLogic_AddCommand_Patch.cs:27-46`:

```csharp
    [HarmonyPrefix]
    public static bool Prefix(PartyScreenLogic __instance, ref PartyScreenLogic.PartyCommand command)
    {
        if (_hook == null) return true;
        if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return true;

        _behavior?.AttachToPartyScreen(__instance);

        var heroId = Hero.MainHero?.StringId;
        var kingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        var cultureId = Hero.MainHero?.Culture?.StringId;
        if (heroId == null) return true;

        var targetId = command.Character?.UpgradeTargets?[command.UpgradeTarget]?.StringId;
        if (targetId == null) return true;

        var costPerUnit = _hook.GetUpgradeCost(targetId);
        if (costPerUnit <= 0) return true;

        var clamped = _hook.ClampUpgradeCount(heroId, kingdomId, cultureId, targetId, command.TotalNumber);
```

and `:48-77`, the upgrade clamp and the reflection write the recruit gate reuses:

```csharp
        if (clamped <= 0)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: BLOCKED {targetId} x{command.TotalNumber} (insufficient resources)");
            return false;
        }

        if (clamped < command.TotalNumber)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: CLAMPED {targetId} {command.TotalNumber}→{clamped}");
            SetTotalNumber(ref command, clamped);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Only enough resources for {clamped} upgrade(s).", Colors.Yellow));
        }
        else
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: ALLOWED {targetId} x{command.TotalNumber}");
        }

        return true;
    }

    private static void SetTotalNumber(ref PartyScreenLogic.PartyCommand command, int value)
    {
        _totalNumberField ??= typeof(PartyScreenLogic.PartyCommand)
            .GetField("<TotalNumber>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(PartyScreenLogic.PartyCommand)
            .GetField("TotalNumber", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        _totalNumberField?.SetValue(command, value);
    }
```

**Correction to the brief:** the upgrade toast has no localization key. It is the raw interpolated
string `$"Only enough resources for {clamped} upgrade(s)."` (line 59), noted as pre-existing raw
English in `docs/reviews/codex-adversarial-special-resources-2026-09-11.prompt.md:36`. So the recruit
toast needs one new key, `taom_res_recruit_clamped`. This plan leaves the upgrade toast as it is.

### The engine (quoted from `pwsh tools/taom-src.ps1 path PartyScreenLogic`, v1.5.7 decompile)

`TaleWorlds.CampaignSystem.Party.PartyScreenLogic.cs`:

```csharp
// :77-91
public class PartyCommand : ISerializableObject
{
    public PartyCommandCode Code { get; private set; }
    public PartyRosterSide RosterSide { get; private set; }
    public CharacterObject Character { get; private set; }
    public int TotalNumber { get; private set; }
    ...
// :150-158
public void FillForRecruitTroop(PartyRosterSide side, TroopType type, CharacterObject character, int number, int index)
{
    Code = PartyCommandCode.RecruitTroop;
    RosterSide = side;
    Character = character;
    Type = type;
    TotalNumber = number;
    Index = index;
}
// :415
public PartyScreenData CurrentData { get; private set; }
// :577-579 (inside ProcessCommand)
case PartyCommandCode.RecruitTroop:
    RecruitPrisoner(command);
    break;
// :592-595
public void AddCommand(PartyCommand command)
{
    ProcessCommand(command);
}
// :942-970
protected void RecruitPrisoner(PartyCommand command)
{
    bool flag = false;
    if (ValidateCommand(command))
    {
        CharacterObject character = command.Character;
        TroopRoster troopRoster = PrisonerRosters[(uint)command.RosterSide];
        int num = TaleWorlds.Library.MathF.Min(CurrentData.RightRecruitableData[character], command.TotalNumber);
        if (num > 0)
        {
            ...
            AddRecruitToHistory(character, num);
            flag = true;
        }
        else
        {
            flag = false;
        }
    }
    if (flag)
    {
        UpdateDelegate?.Invoke(command);
        this.Update?.Invoke(command);
    }
}
```

`TaleWorlds.CampaignSystem.Party.PartyScreenData.cs:38`:
`public List<Tuple<CharacterObject, int>> RecruitedPrisonersHistory;`

Where the charge comes from: `PartyScreenLogic.DoneLogic` (:1228) calls `FireCampaignRelatedEvents()`
at :1264, which folds `CurrentData.RecruitedPrisonersHistory` into a `FlattenedTroopRoster` and raises
`OnMainPartyPrisonerRecruited` (:1295-1310); then :1271 empties the history.
`RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited` (:126-133) raises
`CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1)` once per unit, and
`SpecialResourcesBehavior.OnUnitRecruited` (:206-217) charges `ChargeRecruitCost` through the earning
service. `DoneLogic` ends at :1276. The `PartyScreenClosedEvent` is raised by a separate public method,
`PartyScreenLogic.OnPartyScreenClosed(bool fromCancel)` (:1278-1285, the event at :1284), and that
event is what makes `SpecialResourcesBehavior.OnPartyScreenClosed` (:235, `CommitSession` at :255)
commit the queued upgrade spend. The order comes from the caller,
`Helpers.PartyScreenHelper.ClosePartyPresentation` (`Helpers.PartyScreenHelper.cs:91-112`):

```csharp
// :101-107
if (!fromCancel)
{
    flag = partyScreenLogic?.DoneLogic(isForced) ?? false;
}
if (flag)
{
    partyScreenLogic?.OnPartyScreenClosed(fromCancel);
```

So prisoners are charged first, inside `DoneLogic`, and queued upgrades second, when the screen
closes after Done.

Who issues the command: `PartyVM.ExecuteRecruit` (`TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM.cs:2925-2945`)
checks `IsPrisonerRecruitable` first, sets `number` to 1, 5 or `NumOfRecruitablePrisoners`, calls
`FillForRecruitTroop` and `PartyScreenLogic.AddCommand`, then `CurrentCharacter.UpdateRecruitable()`.
It is the only caller of `FillForRecruitTroop` in the decompile.

So a prefix that returns `false` for `RecruitTroop` skips only `ProcessCommand` and with it
`RecruitPrisoner`, which would itself change nothing when `num` is 0: no roster write, no history
line, no `Update`. Nothing vanilla needs is lost (`docs/reviews/lessons/harmony-il.md`, "When a
Prefix returns false, decompile the FULL call chain").

### The service today

`SpecialResourceService.cs:260-284`, the volunteer cart verdict:

```csharp
    public RecruitGateResult CanAffordRecruit(string heroId, string? kingdomId, string? cultureId, IReadOnlyList<RecruitCartEntry> cart)
    {
        if (cart == null || cart.Count == 0) return RecruitGateResult.Allowed;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return RecruitGateResult.Allowed;

        var required = 0;
        foreach (var entry in cart)
        {
            if (entry == null || entry.Count <= 0) continue;
            var cost = _config.GetTroopCost(entry.TroopId);
            if (cost == null || cost.RecruitCost <= 0) continue;
            required += cost.RecruitCost * entry.Count;
        }

        if (required <= 0) return RecruitGateResult.Allowed;

        var available = _storage.Get(heroId, resource.Id);
        var blocked = available < required;
        if (blocked)
            _logger.LogDebug($"[SpecRes] RECRUIT GATE: blocked (need {required} {resource.DisplayName}, have {available:F0})");

        return new RecruitGateResult(blocked, required, resource.DisplayName);
    }
```

`CanAffordRecruit` cannot be reused as it stands for the party screen: it answers yes or no for a
whole cart, while the command needs a count, and it reads the raw balance without the upgrade spend
queued in the same screen. The plan adds `ClampRecruitCount` beside it and shares the cart sum.

`SpecialResourceService.cs:347-370`, the pattern to mirror:

```csharp
    public float GetAvailableAfterPending(string heroId, string? kingdomId, string? cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return 0f;
        return _storage.Get(heroId, resource.Id) - _pendingSpend;
    }

    public int ClampUpgradeCount(string heroId, string? kingdomId, string? cultureId, string troopId, int requestedCount)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.UpgradeCost <= 0) return requestedCount;
        ...
        var available = GetAvailableAfterPending(heroId, kingdomId, cultureId);
        var maxAffordable = (int)(available / effectivePerUnit);
        var clamped = Math.Max(0, Math.Min(requestedCount, maxAffordable));
```

`SpecialResourceService.cs:237-258`, `ChargeRecruitCost`: charges `cost.RecruitCost * count` with no
career modifier and returns the measured debit; its comment at :248-251 says the party screen's
prisoner recruit is not gated (#563), which this plan makes stale.

Storage (`SpecialResourceStorageService.cs:11-25`): `Get` returns 0 for an unknown key; `Set` refuses
a non-finite value and floors at 0. Gold-only mode (#1108) works through the config row:
`SpecialResourceConfigProvider.cs:57` returns the merchant-only row whose `RecruitCost` is 0, so any
gate that reads `GetTroopCost(...).RecruitCost` honours it with no extra code
(`SpecialResourceGoldOnlyTests.GetTroopCost_GoldOn_ZeroesEveryTroopCost_AndKeepsTheMerchantPrice`).

### The texts

`SpecialResourceMessages.cs:38-43` (the sibling to copy):

```csharp
    public static TextObject RecruitCharge(string resourceName, float amount, int count, string troopName) =>
        new TextObject("{=taom_res_recruit_charge}-{AMOUNT} {RESOURCE} for {COUNT} {TROOP}")
            .SetTextVariable("RESOURCE", resourceName)
            .SetTextVariable("AMOUNT", FormatAmount(amount))
            .SetTextVariable("COUNT", count)
            .SetTextVariable("TROOP", troopName);
```

`RecruitmentVM_RecruitGate_Patch.cs:69-75`:

```csharp
    internal static TextObject RequiresText(RecruitGateResult result)
    {
        var text = new TextObject("{=taom_recruit_needs_resource}Requires {AMOUNT} {RESOURCE}");
        text.SetTextVariable("AMOUNT", result.Required);
        text.SetTextVariable("RESOURCE", result.ResourceDisplayName);
        return text;
    }
```

`taom_recruit_needs_resource` is registered (English in `Main/_Module/ModuleData/taom_module_strings.xml`,
rows in all twelve language files). `UnregisteredLocalizationKeyBaselineTests.NoNewUnregisteredLocalizationKeys`
is a hard gate (its baseline file, `TAOM.Tests/Infrastructure/Localization/unregistered-loc-keys-baseline.txt`,
holds 15 comment lines and no entries): a new `{=taom_*}` literal in C# fails it until the
English row is registered. Registration is `/localize`, which the orchestrator runs after you (see
"Orchestrator steps"), so this test is red on your branch for exactly the key `taom_res_recruit_clamped`.

### Gates that read this code

- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:106` pins the reflection site by line:
  `[DataRow("TaleWorlds.CampaignSystem.Party.PartyScreenLogic+PartyCommand", "PartyCommand", "TotalNumber", "Member", "PartyScreenLogic_AddCommand_Patch.cs:72")]`
  and `docs/reference/taleworlds-api-snapshot/reflection-sites.md:93` repeats the label:
  ``| `…Party.PartyScreenLogic+PartyCommand` | `TotalNumber` | member | `PartyScreenLogic_AddCommand_Patch.cs:72` | SpecialResources transactional spend |``.
  `EveryLineLabel_PointsAtALineThatNamesItsMember` fails when line N of the file no longer contains
  `TotalNumber` outside comments, and `EveryDataRow_HasAMatchingRowInTheCatalogue` fails when the two
  labels differ. Line 72 today is `.GetField("<TotalNumber>k__BackingField", ...`. Your edit moves it.
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs:286-289` classifies this prefix:

```csharp
        ["PartyScreenLogic_AddCommand_Patch"] = new(CoopVeto.ReviewedSafe,
            "Blocks an upgrade the local player cannot afford. Reads _taom_specialResources, which " +
            "is TAOM campaign state — but it gates the acting player's OWN party screen, and each " +
            "peer owns its own resources."),
```

  The verdict stays `ReviewedSafe`; only the reason text gains the prisoner recruit.
- `SpecialResourcesBehaviorLineCountTests`: ceiling 276, the file is 276.
- `EntryPointLineCountGateTests` (ADR-002): the patch file must stay under 150 lines.

### Co-op

`docs/features/coop-interop.md:336-339` and `docs/features/special-resources.md:446`: under
BannerlordCoop, Coop's prisoner and mercenary recruit paths never raise `OnUnitRecruited`, so no
`recruit_cost` is charged in a session; and a guest's gates read the guest's own, possibly stale,
client balance until #967 makes balances server-owned. Neither co-op doc (nor
`docs/features/bannerlord-together-compat.md`) says anything about party-screen commands under
BannerlordTogether. The upgrade clamp in this same prefix already runs on every peer against that
peer's own balance and is classified `ReviewedSafe` for that reason. This plan mirrors it: no new
co-op branch. Under co-op the prisoner clamp is therefore a local check against a local balance with
no charge behind it. That is a new restriction a co-op player will see: today a prisoner recruit in a
session costs nothing at any balance; after this plan it is cut or refused whenever the acting peer's
own balance (on a guest, possibly stale or zero) is short. The commit body and the feature doc say so
plainly. Whether Coop's guest party screen reaches this prefix at all is UNVERIFIED in a live session.

### Claims this plan makes stale

Searched at `db5680c8a` for every `#563` mention outside the history files (`CHANGELOG.md`,
`docs/changelog-archive/`, `docs/reviews/`, `plans/`), then by distinctive words with the Step 8
stale-claim `git grep`, which prints exactly these nine lines today:

- `Main/Features/SpecialResources/SpecialResourceService.cs:249` (comment; Step 7.1)
- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml:15`, `:35`, `:81` (XML comments; Step 8)
- `docs/features/dire-wolf.md:108` (Step 8)
- `docs/features/special-resources.md:3`, `:49`, `:150`, `:232` (Step 8)

Still true after the plan, so left alone: `SpecialResourceService.cs:382-384` (a prisoner recruited
in the same screen is charged before Done commits the upgrade queue; the upgrade clamp still ignores
it), and the "recruit_cost charges only a captured rider recruited from prisoners" lines in
`troop_resource_costs.xml:27-29`, `docs/features/elephant.md:760`, `docs/features/elite-emissary.md:192`
and `docs/features/special-resources.md:131`. No test reads the comments of `troop_resource_costs.xml`
(`git grep -n -i -E "comment|XComment|ungated|#563"` over `TroopResourceCostDataTests.cs` and
`DireWolfTroopDataTests.cs` prints nothing); both classes parse the file, so a broken comment fails them.

### Blast radius

`python tools/graphify_taom.py status --brief` at the planned-at commit printed
`graphify code graph: MISSING`, so the caller lists below come from `git grep -n -w "<Type>" -- Main TAOM.Tests Client`:

- `PartyScreenLogic_AddCommand_Patch`: `Main/SubModule.cs:1296` (`SetBehavior`), `Main/SubModule.cs:1936`
  (`Initialize`), `CoopVetoClassificationTests.cs:286,291`, `ReflectionSiteBindingTests.cs:105-106`.
- `IOnPartyUpgradeResourceCheck`: `PartyCharacterVM_InitializeUpgrades_Patch.cs:14,17`,
  `PartyScreenLogic_AddCommand_Patch.cs:14,19`, `PartyScreenLogic_UpgradeTroop_Patch.cs:12,15`,
  `PartyUpgradeResourceCheckHook.cs:3`, `SpecialResourcesIoC.cs:21`, `Main/SubModule.cs:1932`. No test fake.
- `PartyUpgradeResourceCheckHook`: `SpecialResourcesIoC.cs:21`, `SpecialResourceGoldOnlyTests.cs:222`.
- `ISpecialResourceService`: one implementer (`SpecialResourceService`); consumers in CheatMenu,
  GuestTrade, EliteEmissary, PlayerPossession and SpecialResources only call members, so a new member
  breaks nothing.
- `SpecialResourceMessages`: callers in `SpecialResourceToasts.cs`, `SpecialResourceEarningService.cs`,
  `SpecialResourceCheats.cs`, `SpecialResourceTroopBadge.cs`, `Client/.../SpecialResourceMapBarMixin.cs`;
  this plan only adds methods.
- `RecruitmentVM_RecruitGate_Patch.RequiresText`: `RecruitmentVM_RecruitGate_Patch.cs:43`,
  `RecruitmentVM_ExecuteDone_Patch.cs:27`; behaviour unchanged.

### Conventions that bind this change

- **ADR-002 (thin entry points):** the patch stays under 150 lines and decides nothing itself; the
  count decision lives in `SpecialResourceService.ClampRecruitCount`. Gate: `EntryPointLineCountGateTests`.
- **ADR-007 (adapters for sealed types):** services take no TaleWorlds type. The patch converts
  `CharacterObject` and the history tuples to `string` ids and `RecruitCartEntry` at the boundary.
  Gate: `ServiceSignatureGateTests`.
- **ADR-008 (service testability):** every decision path of the new service method has a test.
- **ADR-013 (server-safe core):** the patch is core (`Main/`) and names only core TaleWorlds types
  (`PartyScreenLogic`, `PartyScreenData`, `InformationManager`, `TextObject`). Gate: `ServerJitGateTests`.
- **`csharp-architecture.md`, "Engine-Float Decision Gates":** the balance is a float at a decision
  gate, so NaN must fail it (`if (!FiniteFloatValidator.IsFinite(available) || !(available >= cost)) return 0;`),
  the float-to-int cast happens only below `requestedCount`, and the gate gets one NaN test.
- **`harmony-patches.md`:** prefix parameters bind by name, so keep `__instance` and `command`; read
  the Patch26 registry entry (`docs/reference/harmony-patch-registry.md:219-223`) before editing.
- **`tests.md`:** MSTest plus NSubstitute, names `MethodName_StateUnderTest_ExpectedBehavior`, RED
  before GREEN.
- **Localization:** player-facing text is `{=KEY}default` with every number in a slot, never baked
  into the default (`SpecialResourceMessagesTests.AssertNoBakedNumber`). Never edit the files under
  `Main/_Module/ModuleData/Languages/`.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests (Step 9 gives the numbers) |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Tests a change reaches | `python tools/graphify_taom.py tests "<Type>"` prints a filter (only if the graph exists; it is missing at the planned-at commit, so use the class filters below) | |
| Bindings (patch plans) | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | exit 0, 0 failed |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not needed: the only ModuleData change is three XML comments in `troop_resource_costs.xml`; Step 8 runs the two test classes that parse that file instead |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | not needed: this plan changes no tool |

Both flags go on build AND test, and prefix every dotnet command with the TEMP and TMP your dispatch
rules give. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify; 18 paths, one of them new):

1. `Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs`
2. `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs`
3. `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs`
4. `Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs` (`RequiresText` body only)
5. `Main/Features/SpecialResources/ISpecialResourceService.cs`
6. `Main/Features/SpecialResources/SpecialResourceService.cs`
7. `Main/Features/SpecialResources/SpecialResourceMessages.cs`
8. `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` (two comment lines, no net line change)
9. `TAOM.Tests/Features/SpecialResources/SpecialResourcePrisonerRecruitGateTests.cs` (new)
10. `TAOM.Tests/Features/SpecialResources/SpecialResourceGoldOnlyTests.cs`
11. `TAOM.Tests/Features/SpecialResources/SpecialResourceMessagesTests.cs`
12. `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` (one reason string)
13. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (one line label)
14. `docs/features/special-resources.md`
15. `docs/reference/harmony-patch-registry.md`
16. `docs/reference/taleworlds-api-snapshot/reflection-sites.md` (one line label)
17. `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` (the text of three XML
    comments, lines 15, 35 and 81, only; no element or attribute changes)
18. `docs/features/dire-wolf.md` (line 108 only)

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: single-owner, and no edit is needed (the hook
  is registered and initialized already; SDK projects pick up the new test file by glob). If you find
  you need one, STOP and report the exact line.
- `Main/_Module/ModuleData/taom_module_strings.xml` and everything under `Main/_Module/ModuleData/Languages/`:
  registration is the orchestrator's `/localize`; the language files are the project lead's.
- Every other ModuleData file, and every `<Troop>` element and attribute in
  `troop_resource_costs.xml`: only the three comments of Scope item 17 change.
- The upgrade toast `$"Only enough resources for {clamped} upgrade(s)."`: pre-existing raw English,
  not part of #563.
- `ClampUpgradeCount` and the upgrade path: the gap where an upgrade queued after a prisoner recruit
  ignores that recruit is a follow-up (see Maintenance notes), not this plan.
- `docs/features/coop-interop.md`, `docs/reviews/lessons/*.md`, `CHANGELOG.md`, `plans/README.md`.
- Any save-format change (a new `SyncData` field). This plan adds none.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, raising `SpecialResourcesBehaviorLineCountTests.Ceiling`, changing the
  `CoopVeto` verdict, or adding a line to `unregistered-loc-keys-baseline.txt`. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, where `<version>` is
  the `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.40` at the
  planned-at commit; a hook refuses any other). Suggested:
  `fix(resources): v2.0.40 - clamp prisoner recruits to the balance` (64 characters).
- The body is the changelog entry, for a reader of the release note, wrapped at 72, no em or en
  dash, no AI attribution trailer. Say what the player sees: a prisoner with a recruit cost can only
  be recruited up to what the balance covers; a partial recruit says "Only enough resources for N
  recruit(s)."; a refused one shows "Requires N <Resource>". Say the co-op effect plainly, in a
  sentence such as: "In co-op, where a prisoner recruit charges nothing, the same check now reads the
  recruiting player's own balance, so a recruit that was free in a session can be cut or refused." Add
  `Not-tested: the Harmony prefix in game (PartyScreenLogic.AddCommand with RecruitTroop), the toasts, and co-op; owed in game per #563`.
- Stage the 18 paths of Scope by name (never `-A`, `-u` or `.`), write the message to a file and run
  `git commit -F "<file>"`. Never `--no-verify`.

## Steps

### Step 1: record the base

Run the build, the full test suite, the binding gate (the "Bindings" row of "Commands you will
need") and the doc lint before any edit. Write the totals and the failing test names of each into
your report. The binding gate runs with `TAOM_RUN_BENCHMARKS=1` and maps an inconclusive result to a
failure (`TAOM.Tests/binding-gate.runsettings`), so a failure that comes from this machine's state
shows here, on the base, and Step 9 compares against it.

**Verify**: build exit 0; dotnet `Failed: 3, Passed: 43028, Skipped: 9` with exactly the three
language-file tests above failing; the binding gate's totals line and exit code recorded (expected
exit 0, 0 failed; the writer did not run it at the planned-at commit); `python tools/lint_docs.py --fail-on-drift`
exit 0. Any other difference: explain it in your report before you go on (another session's commit is
not a reason to continue; STOP if you cannot explain it). If the binding gate fails on the base, record
the failing test names: Step 9 then requires the same set and no new name.

### Step 2: RED for the clamp (stubs plus failing tests)

1. In `Main/Features/SpecialResources/ISpecialResourceService.cs`, after the `CanAffordRecruit`
   declaration (line 42), add:

```csharp

    /// <summary>
    /// How many of <paramref name="requestedCount"/> prisoners of <paramref name="troopId"/> the hero can
    /// recruit in the open party screen (#563): the balance less the upgrade spend queued in this screen
    /// and the <c>recruit_cost</c> of <paramref name="recruitedThisSession"/>, the prisoners already
    /// recruited here, which the engine charges at Done. Returns <paramref name="requestedCount"/> when the
    /// troop has no recruit cost or the hero maps to no resource, and 0 when not even one is affordable.
    /// Never writes the balance.
    /// </summary>
    int ClampRecruitCount(string heroId, string? kingdomId, string? cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession);
```

2. In `SpecialResourceService.cs`, after `CanAffordRecruit` (ends line 284), add the stub:

```csharp

    public int ClampRecruitCount(string heroId, string? kingdomId, string? cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession)
        => requestedCount;
```

3. In `IOnPartyUpgradeResourceCheck.cs` add `using System.Collections.Generic;` at the top and these
   two members after `GetUpgradeCost`:

```csharp
    int ClampRecruitCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession);
    int GetRecruitCost(string troopId);
```

   (`RecruitCartEntry` lives in the enclosing namespace `TAOM.Features.SpecialResources`, so no other
   using is needed.)

4. In `PartyUpgradeResourceCheckHook.cs` add `using System.Collections.Generic;` and, after
   `GetUpgradeCost`:

```csharp

    public int ClampRecruitCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession)
        => _service.ClampRecruitCount(heroId, kingdomId, cultureId, troopId, requestedCount, recruitedThisSession);

    public int GetRecruitCost(string troopId) => 0;
```

5. Create `TAOM.Tests/Features/SpecialResources/SpecialResourcePrisonerRecruitGateTests.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #563: a prisoner recruited in the party screen pays its recruit_cost at Done, once per unit
/// (OnUnitRecruited), so Patch26 clamps the RecruitTroop command to what the balance covers after the
/// upgrades queued and the prisoners already recruited in the same screen. Real storage, so the
/// balance read back is the saved one.
/// </summary>
[TestClass]
public class SpecialResourcePrisonerRecruitGateTests
{
    private const string HeroId = "hero_player";
    private const string Kingdom = "empire";
    private const string Culture = "gondor";
    private const string Troll = "cave_troll";             // recruit_cost 50
    private const string Spider = "taom_spider_creature";  // recruit_cost 5
    private const string Captain = "mordor_uruk_captain";  // upgrade_cost 60, no recruit_cost
    private const string Plain = "plain_troop";            // no cost row

    private ISpecialResourceConfigProvider _config = null!;
    private SpecialResourceStorageService _storage = null!;
    private SpecialResourceService _service = null!;

    private static readonly SpecialResource Castar = new(
        id: "castar",
        kingdomIds: new[] { "empire" },
        cultureIds: new[] { "gondor" },
        displayName: "Castar",
        iconSpriteName: "taom_castar_icon",
        cap: 10000f,
        startingAmount: 0f,
        dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f,
        perRaid: 8f,
        perSiegeVictory: 15f,
        perPrisoner: 1f);

    private static IReadOnlyList<RecruitCartEntry> None => new List<RecruitCartEntry>();

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _config.GetByKingdomId(Kingdom).Returns(Castar);
        _config.GetByCultureId(Culture).Returns(Castar);
        _config.GetTroopCost(Troll).Returns(new TroopResourceCostEntry(Troll, "castar", upgradeCost: 0, dailyUpkeep: 5f, recruitCost: 50));
        _config.GetTroopCost(Spider).Returns(new TroopResourceCostEntry(Spider, "castar", upgradeCost: 0, dailyUpkeep: 0.5f, recruitCost: 5));
        _config.GetTroopCost(Captain).Returns(new TroopResourceCostEntry(Captain, "castar", upgradeCost: 60, dailyUpkeep: 0.2f));
        _config.GetTroopCost(Plain).Returns((TroopResourceCostEntry)null);
        _storage = new SpecialResourceStorageService();
        _service = new SpecialResourceService(_config, _storage, Substitute.For<IModLogger>());
    }

    private void SetBalance(float amount) => _storage.Set(HeroId, "castar", amount);

    private float Balance => _storage.Get(HeroId, "castar");

    private int Clamp(string troopId, int requested, IReadOnlyList<RecruitCartEntry> earlier) =>
        _service.ClampRecruitCount(HeroId, Kingdom, Culture, troopId, requested, earlier);

    [TestMethod]
    public void ClampRecruitCount_BalanceCoversTheWholeRequest_ReturnsTheRequest()
    {
        SetBalance(150f);
        Assert.AreEqual(3, Clamp(Troll, 3, None));
    }

    [TestMethod]
    public void ClampRecruitCount_BalanceCoversPartOfTheRequest_ClampsToWhatItCovers()
    {
        SetBalance(120f);
        Assert.AreEqual(2, Clamp(Troll, 3, None));
    }

    [TestMethod]
    public void ClampRecruitCount_BalanceBelowOneRecruit_ReturnsZero()
    {
        SetBalance(49f);
        Assert.AreEqual(0, Clamp(Troll, 1, None));
    }

    [TestMethod]
    public void ClampRecruitCount_ZeroBalance_ReturnsZero() =>
        Assert.AreEqual(0, Clamp(Troll, 1, None));

    [TestMethod]
    public void ClampRecruitCount_PrisonersAlreadyRecruitedThisScreen_CountAgainstTheBalance()
    {
        SetBalance(120f);
        var earlier = new List<RecruitCartEntry> { new(Troll, 2) }; // 100 owed at Done

        Assert.AreEqual(0, Clamp(Troll, 1, earlier));
    }

    [TestMethod]
    public void ClampRecruitCount_EarlierRecruitsOfOtherTroops_CountOnlyTheirRecruitCost()
    {
        SetBalance(60f);
        var earlier = new List<RecruitCartEntry> { new(Troll, 1), new(Plain, 4), new(Captain, 2) }; // 50 owed

        Assert.AreEqual(2, Clamp(Spider, 3, earlier)); // 10 left, 5 each
    }

    [TestMethod]
    public void ClampRecruitCount_UpgradeQueuedThisScreen_CountsAgainstTheBalance()
    {
        SetBalance(100f);
        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend(HeroId, Captain, 1); // 60 pending

        Assert.AreEqual(0, Clamp(Troll, 1, None));
    }

    [TestMethod]
    public void ClampRecruitCount_TroopWithoutARecruitCost_ReturnsTheRequestAtZeroBalance()
    {
        Assert.AreEqual(4, Clamp(Plain, 4, None));
        Assert.AreEqual(4, Clamp(Captain, 4, None));
    }

    [TestMethod]
    public void ClampRecruitCount_NoResourceForTheKingdomOrCulture_ReturnsTheRequest() =>
        Assert.AreEqual(2, _service.ClampRecruitCount(HeroId, "unknown_kingdom", "unknown_culture", Troll, 2, None));

    [TestMethod]
    public void ClampRecruitCount_AnyRequest_NeverWritesTheBalance()
    {
        SetBalance(120f);
        Clamp(Troll, 3, None);
        Assert.AreEqual(120f, Balance);
    }

    [TestMethod]
    public void ClampRecruitCount_NonFiniteBalance_ReturnsZero()
    {
        var storage = Substitute.For<ISpecialResourceStorageService>();
        storage.Get(HeroId, "castar").Returns(float.NaN);
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>());

        Assert.AreEqual(0, service.ClampRecruitCount(HeroId, Kingdom, Culture, Troll, 1, None));
    }

    [TestMethod]
    public void ChargeRecruitCost_AfterAClampedRequest_ChargesInFullWithoutFlooring()
    {
        SetBalance(120f);

        var clamped = Clamp(Troll, 3, None);
        var charged = _service.ChargeRecruitCost(HeroId, Kingdom, Culture, Troll, clamped);

        Assert.AreEqual(2, clamped);
        Assert.AreEqual(100f, charged);
        Assert.AreEqual(20f, Balance);
    }

    [TestMethod]
    public void ChargeRecruitCost_AfterAnAffordableRequest_ChargesInFull()
    {
        SetBalance(150f);

        var clamped = Clamp(Troll, 3, None);
        var charged = _service.ChargeRecruitCost(HeroId, Kingdom, Culture, Troll, clamped);

        Assert.AreEqual(3, clamped);
        Assert.AreEqual(150f, charged);
        Assert.AreEqual(0f, Balance);
    }

    [TestMethod]
    public void GetRecruitCost_RowsWithAndWithoutARecruitCost_ReturnTheCostOrZero()
    {
        var hook = new PartyUpgradeResourceCheckHook(_service, _config);

        Assert.AreEqual(50, hook.GetRecruitCost(Troll));
        Assert.AreEqual(0, hook.GetRecruitCost(Captain));
        Assert.AreEqual(0, hook.GetRecruitCost(Plain));
    }
}
```

6. In `SpecialResourceGoldOnlyTests.cs`, under the `// ── Recruit ──` heading, after
   `ChargeRecruitCost_GoldOn_ChargesNothing` (it starts at line 178) and before the
   `// ── Upgrade ──` heading (line 187), add:

```csharp

    [TestMethod]
    public void ClampRecruitCount_GoldOff_ZeroBalance_ClampsToZero() =>
        Assert.AreEqual(0, _service.ClampRecruitCount(Hero, Kingdom, Culture, Elite, 4, new RecruitCartEntry[0]));

    [TestMethod]
    public void ClampRecruitCount_GoldOn_ZeroBalance_ReturnsTheRequest()
    {
        GoldOn();
        Assert.AreEqual(4, _service.ClampRecruitCount(Hero, Kingdom, Culture, Elite, 4, new RecruitCartEntry[0]));
    }
```

**Verify**:
- the build command exits 0 (Main), and
  `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors, so every
  failure below is an assertion and not a compile error;
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SpecialResourcePrisonerRecruitGateTests"`
  reports Total 14, Failed 9, Passed 5. The nine failures are assertion failures (expected/actual),
  not compile errors: `BalanceCoversPartOfTheRequest`, `BalanceBelowOneRecruit`, `ZeroBalance`,
  `PrisonersAlreadyRecruitedThisScreen`, `EarlierRecruitsOfOtherTroops`, `UpgradeQueuedThisScreen`,
  `NonFiniteBalance`, `ChargeRecruitCost_AfterAClampedRequest`, `GetRecruitCost_RowsWithAndWithout`.
- `--filter "FullyQualifiedName~SpecialResourceGoldOnlyTests.ClampRecruitCount"` reports Total 2,
  Failed 1 (`ClampRecruitCount_GoldOff_ZeroBalance_ClampsToZero`), Passed 1.
- Quote both totals lines in your report.

### Step 3: GREEN for the clamp

1. In `SpecialResourceService.cs`, extract the cart sum of `CanAffordRecruit` into a private helper
   and use it there (same behaviour; the existing `CanAffordRecruit_*` tests guard it):

```csharp
    // The recruit_cost owed for these entries: troops without one, and empty or null entries, add nothing.
    private int RecruitCostOf(IReadOnlyList<RecruitCartEntry>? entries)
    {
        var total = 0;
        if (entries == null) return total;
        foreach (var entry in entries)
        {
            if (entry == null || entry.Count <= 0) continue;
            var cost = _config.GetTroopCost(entry.TroopId);
            if (cost == null || cost.RecruitCost <= 0) continue;
            total += cost.RecruitCost * entry.Count;
        }
        return total;
    }
```

   In `CanAffordRecruit`, replace the `var required = 0;` line and the `foreach` loop after it with
   `var required = RecruitCostOf(cart);`. Leave the rest of the method as it is.

2. Replace the `ClampRecruitCount` stub with:

```csharp
    public int ClampRecruitCount(string heroId, string? kingdomId, string? cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession)
    {
        if (requestedCount <= 0) return requestedCount;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return requestedCount;

        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.RecruitCost <= 0) return requestedCount;

        // The prisoners recruited earlier in this screen are charged at Done (OnUnitRecruited, once per
        // unit) and so still sit in the balance; GetAvailableAfterPending takes off the queued upgrades.
        var available = GetAvailableAfterPending(heroId, kingdomId, cultureId) - RecruitCostOf(recruitedThisSession);
        if (!FiniteFloatValidator.IsFinite(available) || !(available >= cost.RecruitCost))
        {
            _logger.LogDebug($"[SpecRes] RECRUIT CLAMP: {troopId} requested={requestedCount} clamped=0 (available={available:F0}, cost/unit={cost.RecruitCost})");
            return 0;
        }

        // Cast only below requestedCount, so the float never overflows the int.
        var affordable = available / cost.RecruitCost;
        if (affordable >= requestedCount) return requestedCount;

        var clamped = (int)affordable;
        _logger.LogDebug($"[SpecRes] RECRUIT CLAMP: {troopId} requested={requestedCount} clamped={clamped} (available={available:F0}, cost/unit={cost.RecruitCost})");
        return clamped;
    }
```

3. In `PartyUpgradeResourceCheckHook.cs` replace the `GetRecruitCost` stub with
   `public int GetRecruitCost(string troopId) => _config.GetTroopCost(troopId)?.RecruitCost ?? 0;`

**Verify**:
- build exit 0;
- `--filter "FullyQualifiedName~SpecialResourcePrisonerRecruitGateTests"`: Total 14, Passed 14;
- `--filter "FullyQualifiedName~SpecialResourceGoldOnlyTests"`: 0 failed (26 tests: 24 before plus 2);
- `--filter "FullyQualifiedName~SpecialResourceServiceTests"`: 0 failed (the `CanAffordRecruit_*`
  tests prove the extraction kept behaviour).

### Step 4: RED for the two texts

1. In `SpecialResourceMessages.cs`, after `RecruitCharge` (ends line 43), add stubs:

```csharp

    public static TextObject RecruitClamped(int count) => new TextObject("{=!}");

    public static TextObject RecruitNeedsResource(int amount, string resourceName) => new TextObject("{=!}");
```

2. In `SpecialResourceMessagesTests.cs`, after `RecruitCharge_BindsAmountCountAndTroopName`, add:

```csharp

    [TestMethod]
    public void RecruitClamped_AnyCount_BindsTheCountAsASlot()
    {
        var message = SpecialResourceMessages.RecruitClamped(2);

        Assert.AreEqual("{=taom_res_recruit_clamped}Only enough resources for {COUNT} recruit(s).", message.Value);
        AssertNoBakedNumber(message);
        Assert.AreEqual(2, message.Attributes["COUNT"]);
    }

    [TestMethod]
    public void RecruitNeedsResource_AmountAndName_ReusesTheVolunteerGateKey()
    {
        var message = SpecialResourceMessages.RecruitNeedsResource(50, "War Spoils");

        Assert.AreEqual("{=taom_recruit_needs_resource}Requires {AMOUNT} {RESOURCE}", message.Value);
        Assert.AreEqual(50, message.Attributes["AMOUNT"]);
        Assert.AreEqual("War Spoils", message.Attributes["RESOURCE"]);
    }
```

   The class carries `[TestCategory("RequiresGame")]`; on this machine the game is installed and the
   class runs.

**Verify**:
- `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors;
- `--filter "FullyQualifiedName~SpecialResourceMessagesTests"` reports Total 16, Failed 2, Passed 14.
  The class has 9 `[TestMethod]`s and one `[DataTestMethod]` with 5 `[DataRow]`s at the planned-at
  commit (14 tests), plus the 2 new ones. The two failures are the new tests, assertion failures on
  `message.Value`. If the run reports 0 tests or skips the class, STOP: the RequiresGame setup is not
  what this plan assumes.

### Step 5: GREEN for the texts, and one source for the reused key

1. Replace the two stubs in `SpecialResourceMessages.cs` with:

```csharp

    /// <summary>The yellow line when a party-screen prisoner recruit is cut to what the balance covers (#563).</summary>
    public static TextObject RecruitClamped(int count) =>
        new TextObject("{=taom_res_recruit_clamped}Only enough resources for {COUNT} recruit(s).")
            .SetTextVariable("COUNT", count);

    /// <summary>The "Requires" line of the volunteer gate (Patch51), also shown when a prisoner recruit is refused (#563).</summary>
    public static TextObject RecruitNeedsResource(int amount, string resourceName) =>
        new TextObject("{=taom_recruit_needs_resource}Requires {AMOUNT} {RESOURCE}")
            .SetTextVariable("AMOUNT", amount)
            .SetTextVariable("RESOURCE", resourceName);
```

2. In `RecruitmentVM_RecruitGate_Patch.cs:69-75`, replace the body of `RequiresText` so the key has one
   literal in the code:

```csharp
    internal static TextObject RequiresText(RecruitGateResult result)
        => SpecialResourceMessages.RecruitNeedsResource(result.Required, result.ResourceDisplayName);
```

   Keep `using TaleWorlds.Localization;` (the return type still needs it).

**Verify**:
- build exit 0;
- `--filter "FullyQualifiedName~SpecialResourceMessagesTests"`: Total 16, Passed 16;
- `--filter "FullyQualifiedName~RecruitGatePatchTests"`: 0 failed;
- `--filter "FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests"`: exactly 1 failed,
  `NoNewUnregisteredLocalizationKeys`, and its message names `taom_res_recruit_clamped` and no other
  key. This is the expected red that the orchestrator's `/localize` clears; do not register the key
  yourself and do not touch the baseline file.
- `--filter "FullyQualifiedName~LocalizationKeyConsistencyTests"`: 0 failed (the reused key keeps its
  registered English, "Requires {AMOUNT} {RESOURCE}").

### Step 6: gate RecruitTroop in the prefix

No unit test reaches this method: it is a Harmony prefix that reads `Hero.MainHero` and calls
`InformationManager`, so it goes in the commit's `Not-tested:` trailer. Its decision is
`ClampRecruitCount`, tested in Steps 2 and 3.

1. In `PartyScreenLogic_AddCommand_Patch.cs`, add `using System.Collections.Generic;` to the usings.
2. Replace line 31, `if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return true;`,
   with the two lines:

```csharp
        if (command.Code == PartyScreenLogic.PartyCommandCode.RecruitTroop) return GateRecruit(__instance, ref command);
        if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return true;
```

3. Insert these two methods between the end of `Prefix` and `SetTotalNumber`:

```csharp

    // #563: a prisoner recruited here is charged its recruit_cost at Done (OnUnitRecruited, once per unit),
    // so the command is clamped to what the balance covers after the queued upgrades and the prisoners
    // already recruited in this screen. Returning false skips only RecruitPrisoner, which then changes nothing.
    private static bool GateRecruit(PartyScreenLogic logic, ref PartyScreenLogic.PartyCommand command)
    {
        var hero = Hero.MainHero;
        var troopId = command.Character?.StringId;
        if (hero?.StringId == null || troopId == null || command.TotalNumber <= 0) return true;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;
        var clamped = _hook.ClampRecruitCount(hero.StringId, kingdomId, cultureId, troopId, command.TotalNumber, RecruitedThisScreen(logic));
        if (clamped >= command.TotalNumber) return true;

        if (clamped <= 0)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: BLOCKED recruit {troopId} x{command.TotalNumber} (insufficient resources)");
            InformationManager.DisplayMessage(new InformationMessage(
                SpecialResourceMessages.RecruitNeedsResource(_hook.GetRecruitCost(troopId), _hook.GetResourceDisplayName(kingdomId, cultureId)).ToString(),
                Colors.Yellow));
            return false;
        }

        _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: CLAMPED recruit {troopId} {command.TotalNumber}→{clamped}");
        SetTotalNumber(ref command, clamped);
        InformationManager.DisplayMessage(new InformationMessage(
            SpecialResourceMessages.RecruitClamped(clamped).ToString(), Colors.Yellow));
        return true;
    }

    // The prisoners recruited earlier in this screen; the engine empties the list at Done and on Reset.
    private static List<RecruitCartEntry> RecruitedThisScreen(PartyScreenLogic logic)
    {
        var history = logic?.CurrentData?.RecruitedPrisonersHistory;
        var entries = new List<RecruitCartEntry>(history?.Count ?? 0);
        if (history == null) return entries;
        foreach (var item in history)
        {
            if (item?.Item1 != null) entries.Add(new RecruitCartEntry(item.Item1.StringId, item.Item2));
        }
        return entries;
    }
```

   Do not call `_behavior?.AttachToPartyScreen` for a recruit: a recruit queues nothing, and the
   upgrade path attaches when an upgrade comes.

4. Repoint the reflection label. Run
   `grep -n "k__BackingField" Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs`
   and note the line number N of `.GetField("<TotalNumber>k__BackingField", ...`. Replace
   `PartyScreenLogic_AddCommand_Patch.cs:72` with `PartyScreenLogic_AddCommand_Patch.cs:N` in both
   `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:106` and
   `docs/reference/taleworlds-api-snapshot/reflection-sites.md:93`. Change nothing else on those lines.

**Verify**:
- build exit 0;
- `wc -l Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` prints fewer than 150;
- `--filter "FullyQualifiedName~ReflectionSiteBindingTests"`: 0 failed, and the run includes
  `EveryLineLabel_PointsAtALineThatNamesItsMember` and `EveryDataRow_HasAMatchingRowInTheCatalogue`;
- `--filter "FullyQualifiedName~EntryPointLineCountGateTests"`: 0 failed;
- `--filter "FullyQualifiedName~ServerJitGateTests"`: 0 failed.

### Step 7: comments and the co-op classification reason

Each text below is a draft: before you write it, re-read the code it describes and correct it if it
says anything the code does not do.

1. `SpecialResourceService.cs:248-251` (the comment inside `ChargeRecruitCost`) becomes:

```csharp
        // Return what LEFT the wallet, not the nominal cost. Both recruit gates check the balance first
        // (Patch51 for volunteers, Patch26 for party-screen prisoners since #563), but the storage floors
        // at zero and a co-op guest's gate reads a stale client balance (#967), so the charge can still land
        // on a smaller balance. A toast built from the nominal cost would announce currency that was
        // never there (Codex, review 95, F1).
```

2. `ISpecialResourceService.cs:31-32`, the end of the `ChargeRecruitCost` summary, from
   "Charged from `OnUnitRecruitedEvent`" on, becomes:
   `/// the caller can tell the player. Charged from <c>OnUnitRecruitedEvent</c> (player-only) for volunteers`
   `/// and party-screen prisoners; the RecruitmentVM gate and the Patch26 prisoner clamp (#563) check affordability first.`
   Keep the first lines of the summary as they are.

3. `SpecialResourcesBehavior.cs:203-204`, replace exactly these two lines with exactly two lines
   (the file must stay at 276 lines):

```csharp
    // Charges the one-time recruit_cost (no-op unless the troop carries one in troop_resource_costs.xml; Patch51
    // gates volunteers and Patch26 party-screen prisoners, #563; a guest's gate reads a stale balance, #967). The service picks
```

   Line 205 (`// the payer: Hero.MainHero in single-player, ...`) stays as it is and must still follow.

4. `CoopVetoClassificationTests.cs:287-289`, the reason string for `PartyScreenLogic_AddCommand_Patch`,
   becomes (verdict unchanged, `CoopVeto.ReviewedSafe`):

```csharp
            "Blocks or clamps an upgrade or a party-screen prisoner recruit (#563) the local player " +
            "cannot afford. Reads _taom_specialResources, which is TAOM campaign state, but it gates " +
            "the acting player's OWN party screen, and each peer owns its own resources."),
```

**Verify**:
- `wc -l Main/Features/SpecialResources/SpecialResourcesBehavior.cs` prints 276;
- build exit 0;
- `--filter "FullyQualifiedName~SpecialResourcesBehaviorLineCountTests"`: 0 failed;
- `--filter "FullyQualifiedName~CoopVetoClassificationTests"`: 0 failed.

### Step 8: record the decision in the docs and the data comments

Drafts again: check every sentence against the code you wrote and the engine excerpts above. The
line numbers below are from the planned-at commit; an edit earlier in a file shifts the later ones,
so find each anchor by its text.

`docs/features/special-resources.md`:

1. Line 3 (Status): replace the last sentence, `#563 tracks the ungated prisoner recruit path.`, with
   `Since 2026-11-19 (#563) the party screen's prisoner recruit is clamped to the recruit_cost the balance covers; its in-game check is owed.`
2. Line 49 (Notifications): replace
   `the storage floors at zero and the party screen's prisoner recruit is not gated by \`recruit_cost\` (#563), so the nominal cost can exceed the balance.`
   with
   `the storage floors at zero, and an upgrade queued after a prisoner recruit in the same screen, or a co-op guest's stale balance, can still let the nominal cost exceed the balance.`
   On the same line, replace the anchor (it occurs once in the file)
   `once per recruited prisoner or volunteer). The spend and charge lines report`
   with
   `once per recruited prisoner or volunteer); and, for a party-screen prisoner recruit the balance cannot fully cover (#563), a yellow \`Only enough resources for 2 recruit(s).\` (\`{=taom_res_recruit_clamped}\`) when it is cut to fewer, or the volunteer gate's yellow \`Requires 50 War Spoils\` (\`{=taom_recruit_needs_resource}\`) when not one is affordable. The spend and charge lines report`
3. The cost table at lines 140-144: the `recruit_cost` row becomes
   `| \`recruit_cost\` | Recruited as a volunteer or from prisoners (one-time) | Patch51 gate (volunteers), Patch26 clamp (party-screen prisoners, #563) + \`OnUnitRecruitedEvent\` charge |`
4. Line 150: replace `the ungated prisoner path` with
   `the prisoner path, which the party screen clamps to the balance since #563`.
5. After the Patch51 paragraph that ends at line 163 ("Both patches fold the cart through the pure
   `RecruitCartGrouping`."), add a blank line and this paragraph:

   `**Prisoner recruit gate (Patch26, #563):** the party screen's prisoner recruit pays the same \`recruit_cost\` as the volunteer screen (the maintainer's decision of 2026-11-19, option A of #563; the alternative, a free recruit as a capture reward, was declined). \`PartyScreenLogic_AddCommand_Patch\` gates \`PartyCommandCode.RecruitTroop\` in the prefix that already clamps \`UpgradeTroop\`. \`ISpecialResourceService.ClampRecruitCount\` returns how many of the requested prisoners the balance covers after the upgrade spend queued in this screen and the \`recruit_cost\` of the prisoners already recruited in it (\`PartyScreenLogic.CurrentData.RecruitedPrisonersHistory\`, which the engine charges at Done through \`OnMainPartyPrisonerRecruited\` and then \`OnUnitRecruited\` once per unit). A partial count is clamped through the command's \`TotalNumber\` with a yellow \`{=taom_res_recruit_clamped}\` line; a count of zero returns false, which skips \`RecruitPrisoner\` (it would change nothing at zero either), with the volunteer gate's \`{=taom_recruit_needs_resource}\` line in yellow. A troop without a \`recruit_cost\`, a hero whose kingdom and culture map to no resource, and Elite Troops Cost Gold Only (every \`recruit_cost\` reads 0) pass unchanged. Known gap: the upgrade clamp does not count prisoners recruited earlier in the same screen, so an upgrade queued after a prisoner recruit can still exceed the balance and its commit floors at zero. Co-op: the clamp reads the acting peer's own balance, as the upgrade clamp does, but Coop's prisoner path charges nothing (see "Co-op recruit charge"). So this adds a restriction a co-op player did not have: a prisoner recruit that costs nothing in a session is now cut or refused whenever that peer's own balance (on a guest, possibly stale or zero) is short. Not checked in a session.`

6. Line 232: replace `(ungated, #563)` with `(clamped to the balance in the party screen since #563)`.
7. Key Files table, line 294: the row for `PartyScreenLogic_AddCommand_Patch.cs` becomes
   `| \`Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs\` | Prefix: clamp an upgrade or a party-screen prisoner recruit (#563) to what the balance covers |`
8. Tests section (starts line 322): add the bullet
   `- \`SpecialResourcePrisonerRecruitGateTests.cs\`: the party-screen prisoner clamp (#563) against real storage: whole, partial and refused counts, earlier recruits and queued upgrades in the same screen, troops without a cost, no resource, NaN, and the charge that follows a clamp never flooring`
   (no count: nothing computes it). In the same section, line 328 carries a count that is already
   wrong (the class has 14 tests, 16 after Step 4): replace
   `` `SpecialResourceMessagesTests.cs`: 5 tests. Each outflow message keeps every number in a slot (no digit baked into the default text) and binds the expected attributes; ``
   with
   `` `SpecialResourceMessagesTests.cs`: each outflow message keeps every number in a slot (no digit baked into the default text) and binds the expected attributes, including the two party-screen prisoner recruit lines (#563); ``
   (the rest of the line, `` `FormatAmount` ``, stays).
9. Changelog (line 527 heading): add as the first entry
   `- 2026-11-19 (#563): a prisoner recruited in the party screen is clamped to the \`recruit_cost\` the balance covers, net of the upgrades queued and the prisoners already recruited in that screen; a refused recruit shows the volunteer gate's "Requires" line. Before, a captured spider, troll or Harad rider was recruited at any balance and the charge floored at zero. Under co-op the clamp reads the acting player's own balance although Coop's prisoner path charges nothing, so a co-op player can now be refused a prisoner recruit that was free in a session. Tests in \`SpecialResourcePrisonerRecruitGateTests\`. Owed in game: capture a spider at zero balance and try to recruit it.`
10. Line 446 ("Co-op recruit charge" section): replace
   `a guest's affordability gate (Patch51) reads a stale client balance`
   with
   `a guest's affordability gates (Patch51, and the Patch26 prisoner clamp since #563 where it runs on a guest) read a stale client balance`.

`docs/features/dire-wolf.md` line 108: replace
`A prisoner recruit is charged afterward with no gate, so it succeeds at a zero balance (#563, open, the same for every elite creature).`
with
`A prisoner recruit in the party screen is clamped to what the balance covers and charged at Done (#563, the same for every troop with a \`recruit_cost\`).`
The file has CRLF line ends; edit with the Edit tool, never `sed -i`.

`Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` (CRLF; Edit tool only; change
only the comment text below, inside the existing `<!-- ... -->`, and never write `--` inside a comment):

- Line 15: replace `recruit_cost charges the notable route, gated, and a prisoner recruit, ungated (#563).`
  with `recruit_cost charges the notable route and a prisoner recruit, both gated (Patch51, and the Patch26 party-screen clamp since #563).`
- Line 35: replace `troll, a path Patch51 does not gate (the balance floors at 0, #563); the upkeep`
  with `troll, a path the party screen clamps to the balance since #563 (Patch26); the upkeep`
- Line 81: replace `(the engine raises OnUnitRecruited there too, ungated, #563), so`
  with `(the engine raises OnUnitRecruited there too; the party screen clamps it to the balance, #563), so`

`docs/reference/harmony-patch-registry.md`, section `## Patch26_SpecialResources` (line 219): after
the line `Per-kingdom resource gating + transactional spending`, add a blank line and:
`Since #563 (2026-11-19) the \`AddCommand\` prefix also clamps \`PartyCommandCode.RecruitTroop\`, a party-screen prisoner recruit, to the \`recruit_cost\` the balance covers, through \`IOnPartyUpgradeResourceCheck.ClampRecruitCount\`. A count of zero returns false, which skips \`ProcessCommand\` and so \`RecruitPrisoner\` and nothing else (\`AddCommand\` only calls \`ProcessCommand\`). See \`docs/features/special-resources.md\`.`

(The backslash-escaped backticks above are only for this plan's formatting: write plain backticks.)

**Verify**:
- `python tools/lint_docs.py --fail-on-drift` exits 0;
- the stale-claim grep, over the whole repo, tests included, minus the history files:
  `git grep -n -i -E "ungated prisoner|prisoner recruit[^.]{0,40}(ungated|no gate|not gated)|(ungated|no gate|not gated|does not gate)[^.]{0,40}#563|is not \(#563\)|succeeds at a zero balance" -- . ':!CHANGELOG.md' ':!docs/changelog-archive' ':!docs/reviews' ':!plans'`
  prints nothing. At the planned-at commit it printed exactly the nine lines of "Claims this plan makes
  stale" (`SpecialResourceService.cs:249`, `troop_resource_costs.xml:15,35,81`, `dire-wolf.md:108`,
  `special-resources.md:3,49,150,232`). `CHANGELOG.md`, `docs/changelog-archive/`, `docs/reviews/` and
  `plans/` keep their history and are excluded. If it prints a line this plan does not name, STOP;
- `git diff --numstat -- Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` prints
  `3	3`, and `git diff -U0 -- Main/_Module/ModuleData/special_resources/troop_resource_costs.xml | grep '^[+-]' | grep -c "<Troop"`
  prints 0 (only comment lines changed; the first `grep` keeps the added and removed lines only);
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TroopResourceCostDataTests|FullyQualifiedName~DireWolfTroopDataTests"`
  reports Total 13 (8 and 5 at the planned-at commit), Failed 0: both classes parse
  `troop_resource_costs.xml`;
- `grep -c "PartyScreenLogic_AddCommand_Patch.cs:72" docs/reference/taleworlds-api-snapshot/reflection-sites.md TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`
  prints 0 for both files unless N is still 72.

### Step 9: full verification

Run, in order: the build; the full test suite; the binding gate.

**Verify**:
- build exit 0;
- the full suite: `Failed: 4, Passed: 43045, Skipped: 9` (43058 total: the base's 43040 plus 18 new
  tests: 14 in the new class, 2 gold-only, 2 messages). The four failures are the base's three
  language-file tests plus `NoNewUnregisteredLocalizationKeys` for `taom_res_recruit_clamped`
  (Step 5). If the base totals differed in Step 1, apply the same difference and explain it;
- the binding gate: exit 0, 0 failed (or exactly the failing set Step 1 recorded on the base, and no
  new name);
- `git status --porcelain` lists only the 18 Scope paths, plus untracked files under `plans/` (the run
  worktree holds untracked plan files and the `plans/_audit/` folder; leave them alone).

### Step 10: commit

Stage the 18 Scope paths by name and commit with the message file (see Git workflow).

**Verify**: `git log -1 --stat` shows exactly those 18 files; the subject is at most 72 characters and
names the `<Version>` from `Main/_Module/SubModule.xml`; `git status --porcelain` shows no tracked
change left.

## Test plan

- New `TAOM.Tests/Features/SpecialResources/SpecialResourcePrisonerRecruitGateTests.cs` (real
  `SpecialResourceStorageService`, substituted config): whole request, partial clamp, below one unit,
  zero balance, earlier recruits of the same troop, earlier recruits of other troops (only their
  recruit cost counts), a queued upgrade, a troop without a recruit cost, no resource, read-only,
  NaN (substituted storage, since real storage refuses NaN), the clamp followed by `ChargeRecruitCost`
  both clamped and affordable (the regression of #563: the charge equals the nominal cost and nothing
  floors), and `PartyUpgradeResourceCheckHook.GetRecruitCost` for a row with and without a cost.
- `SpecialResourceGoldOnlyTests`: the clamp clamps with the option off and passes with it on, through
  the real config provider over real XML.
- `SpecialResourceMessagesTests`: the new key with its count in a slot; the reused key with its
  registered English.
- Pattern: `TAOM.Tests/Features/SpecialResources/SpecialResourceSpenderTests.cs` (real storage) and
  `SpecialResourceGoldOnlyTests.cs`.
- Not unit-testable, for the `Not-tested:` trailer: the Harmony prefix's `RecruitTroop` branch
  (`GateRecruit`, `RecruitedThisScreen`), the two toasts on screen, and co-op. In-game check owed on
  #563: capture a spider at zero balance and try to recruit it; with enough for two of three trolls,
  recruit all three and expect two plus the yellow line.
- No RefAsm replay is needed: the new tests touch no TaleWorlds type except the messages tests, which
  sit in a `RequiresGame` class already.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full suite reports `Failed: 4, Passed: 43045, Skipped: 9` (or the Step 1 base adjusted the
      same way), the fourth failure being `NoNewUnregisteredLocalizationKeys` naming only
      `taom_res_recruit_clamped`
- [ ] `--filter "FullyQualifiedName~SpecialResourcePrisonerRecruitGateTests"` reports 14 passed, and
      the Step 2 log shows 9 of them failed against the stub
- [ ] The binding gate exits 0 (or fails exactly the set Step 1 recorded on the base)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] The Step 8 whole-repo stale-claim `git grep` prints nothing
- [ ] `troop_resource_costs.xml` changed in comment text only (`git diff --numstat` gives `3	3`, no
      `<Troop` line among the changed lines), and `TroopResourceCostDataTests` plus
      `DireWolfTroopDataTests` report Total 13, Failed 0
- [ ] `wc -l` gives 276 for `SpecialResourcesBehavior.cs` and fewer than 150 for the patch file
- [ ] `git status --porcelain` lists only the 18 Scope paths (plus untracked files under `plans/`)
      before the commit, and nothing tracked after it
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts (the drift check names the files).
- `ClampRecruitCount` or `GetRecruitCost` already exists, or `PartyScreenLogic_AddCommand_Patch`
  already handles `RecruitTroop`: the fix landed elsewhere.
- A second implementer of `IOnPartyUpgradeResourceCheck` or `ISpecialResourceService` (a hand-written
  fake class) breaks the build.
- `PartyScreenLogic.CurrentData`, `PartyScreenData.RecruitedPrisonersHistory`, `PartyCommand.TotalNumber`
  or `PartyCommandCode.RecruitTroop` does not compile as quoted: report the mismatch, do not decompile
  and improvise.
- Step 2 does not give 9 assertion failures and 5 passes, or Step 4 does not report Total 16,
  Failed 2 for `SpecialResourceMessagesTests`.
- Any test outside the expected four fails at Step 9, in particular any `Infrastructure.Localization`
  test other than `NoNewUnregisteredLocalizationKeys`, or that test names a key other than
  `taom_res_recruit_clamped`.
- `SpecialResourcesBehavior.cs` cannot keep 276 lines, or a gate seems to need loosening.
- The fix seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, a language file, or a
  ModuleData XML change other than the three comment edits of Scope item 17 (any element or
  attribute change, or any other ModuleData file).
- The Step 8 stale-claim grep prints a line this plan does not name.
- A verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: #563 exists. After merge, close it with the label `triage-needs-ingame` (the in-game check
  in Test plan is owed).
- `/localize` for the new key `taom_res_recruit_clamped` (Case A, step 2:
  `python tools/harvest_literal_loc_keys.py --apply` registers the English row in
  `Main/_Module/ModuleData/taom_module_strings.xml`), which turns
  `NoNewUnregisteredLocalizationKeys` green. `EveryLanguage_DeclaresARowForEveryEnglishKey` then also
  reports this key until the project lead's language pass; it is already red in the baseline. The
  reused key `taom_recruit_needs_resource` needs nothing.
- `docs/features/special-resources.md` is updated by the executor; its `feature-map.md` row needs no
  change.
- File the follow-up named in Maintenance notes (upgrade clamp ignores prisoners recruited earlier in
  the same screen) if the maintainer wants it tracked.
- Review tier: `/deep-review` tier 1 (a Harmony prefix on a party-screen command that can return false).

## After merge: the maintainer's actions

None beyond the in-game check owed on #563.

## Maintenance notes

- The clamp reads `PartyScreenLogic.CurrentData.RecruitedPrisonersHistory`; an engine bump that
  renames it or changes when `DoneLogic` empties it breaks the in-screen accounting silently (the
  gate would then see only the balance). The binding gate does not cover a direct field read that
  compiles; a compile error is what catches a rename.
- `SetTotalNumber` writes the command through reflection; if `_totalNumberField` resolves to null the
  toast says N while vanilla recruits the full count. `ReflectionSiteBindingTests` pins the field.
- What review should probe: the `return false` path (nothing after `ProcessCommand` in `AddCommand`);
  the order of charges at Done (prisoners inside `DoneLogic`, at :1264; queued upgrades at
  `PartyScreenClosedEvent`, which `OnPartyScreenClosed` raises after `ClosePartyPresentation` sees
  `DoneLogic` return true); the NaN gate and the cast; that the toast never fires for a troop
  without a recruit cost.
- Deferred, by scope: `ClampUpgradeCount` does not subtract the prisoners recruited earlier in the
  same screen, so recruit-then-upgrade can still overdraw the upgrade commit (it floors at zero).
  The fix is to pass the same history into the upgrade clamp; it changes the upgrade path, which #563
  did not ask for.
- Deferred, by scope: the upgrade toast at `PartyScreenLogic_AddCommand_Patch.cs` (`"Only enough
  resources for {clamped} upgrade(s)."`) is still raw English with the number baked in.
- Co-op: under BannerlordCoop the prisoner path charges nothing, so the clamp on a guest is a local
  check with no charge behind it; when #967 makes balances server-owned, revisit both clamps together.
