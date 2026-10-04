# Plan 031: Read MCM settings once instead of per blow, per frame and per agent

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- Main/Features/CombatMechanics Main/Features/BlowDiagnostics Main/Features/MixedFormations Main/Features/CompanionTactics Main/Features/DreadAura Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs Main/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProvider.cs Main/Features/LocalizationOverride TAOM.Tests/Features/HotPathSettingsProvidersTests.cs TAOM.Tests/Features/CombatMechanics TAOM.Tests/Features/BlowDiagnostics TAOM.Tests/Features/MixedFormations TAOM.Tests/Features/CompanionTactics TAOM.Tests/Features/DreadAura TAOM.Tests/Features/Elephant/HowdahDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/SmartCavalryAI TAOM.Tests/Features/CultureDoctrine TAOM.Tests/Features/SiegePropDiagnostics TAOM.Tests/Features/LocalizationOverride TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs docs/features/combat-mechanics.md docs/features/companion-tactics.md docs/features/mixed-formations.md docs/features/localization-override.md docs/modding/file-catalogue.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Sibling plans planned at the same commit edit files
> this command lists, and their edits are expected drift, not a mismatch, as long as every excerpt and
> every quoted doc line THIS plan relies on still matches: plan 030 deletes
> `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` and a table row of
> `docs/features/companion-tactics.md`; plan 032 edits `Main/Features/MixedFormations/FormationLayoutService.cs`,
> the `Patch30_*` hooks, `TAOM.Tests/Features/MixedFormations` and `docs/features/mixed-formations.md`.
> That is why every doc edit below is anchored by its quoted text, never by its line number.

## Status

- **Priority**: P2
- **Effort**: L (mechanical, but nine providers, three behaviour changes and their tests)
- **Risk**: MED (behaviour-preserving by design; the breadth is the risk: a mis-wired getter changes gameplay)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` (net472),
  failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the other
  languages; the paid translator run waits on the maintainer), skipped:
  `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`. Python suite:
  not recorded and not needed (this plan touches no `tools/` file).
- **Issue**: filed by the orchestrator before execution

## Why this matters

Nine TAOM settings providers resolve `TaomSettings.Instance` (or `BlowDiagnosticsSettings.Instance`) on
every property read. Each resolve is two `ConcurrentDictionary` lookups plus a walk of MCM's settings
containers, and these providers are read per melee blow (about eight reads per ordinary hit), per frame,
per seat per frame, and per agent stat update in large battles. Commit `7feca96b` (refined by `02157b18`)
removed exactly this cost from `BattleBalanceSettingsProvider` by caching the reference lazily and reading
through it, so live MCM edits still apply; this plan applies the same pattern to the remaining hot
providers, moves a few pure per-hit and per-order checks ahead of the settings read, stops the Mixed
Formations behaviour from calling `Enum.TryParse` every frame, and stops the English-override prefix from
allocating a substring for every `{=ID}` text it sees. Nothing a player can see or tune changes: every
getter returns what it returns today, before and after an MCM edit.

## Current state

### The pattern to copy (read both files before Step 3)

`Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs` at `dffdf879` (after `02157b18`, which
replaced `7feca96b`'s constructor read with a lazy accessor):

```csharp
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public BattleBalanceSettingsProvider() { }
    internal BattleBalanceSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableCustomTroopPower      => Settings?.EnableCustomTroopPower      ?? true;
```

Its tests, `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`, hold the three test
shapes this plan reuses: the no-MCM default pins (`new BattleBalanceSettingsProvider().X`, because
`TaomSettings.Instance` is null in tests: MCM is never initialised there), the live-edit read-through test
`Getters_ReadThroughTheSettings_SoLiveMcmEditsApply` (builds the provider on `new TaomSettings()` through the
internal constructor, reads, edits the settings object, reads again), the DryIoc resolve test
`Provider_ResolvesFromARealContainer` (a second, internal constructor must not break DryIoc's single
public constructor selection), and the IL rule `Getters_NeverReadTaomSettingsInstance_TheLazyAccessorDoes`,
which uses `TAOM.Tests/Migration/IlCallScanner.cs` (`ExtractCalledMethods(MethodBase, byte[])`) and this
predicate:

```csharp
        bool IsInstanceGetter(MethodBase m) =>
            m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));
```

Why lazy and not in the constructor (from `02157b18`'s body, re-checked): a constructor read caches null
for the whole session if the provider is ever resolved before MCM sets `BaseSettingsProvider.Instance`.
The lazy `??=` keeps resolving until it gets a non-null instance, so this plan does not depend on when
each provider is first resolved. Until MCM is up, the getters fall back exactly as today.

### MCM facts (the MCM v5 decompile in the local decompile dump, `_modules_build/TAOM.Dependencies__MCMv5.cs`, read for this plan)

- `GlobalSettings<T>.Instance` (line 6321): `if (!GlobalSettings.Cache.ContainsKey(typeof(T))) ...; return
  BaseSettingsProvider.Instance?.GetSettings(GlobalSettings.Cache[typeof(T)]) as T;`
- `DefaultSettingsProvider.GetSettings(string id)` (line 2214) loops over every settings container and every
  external provider, and logs `GetSettings <id> returned null` when none has it.
- A container keeps one instance per id: `LoadedSettings.Add(settings.Id, settings)` (line 1964),
  `GetSettings` returns `LoadedSettings[id]` (line 1971). `OverrideSettings` (line 1995) and
  `ResetSettings` (line 2005) copy values INTO `LoadedSettings[settings.Id]`
  (`SettingsUtils.OverrideValues(current, new)`, line 4449); they never swap the instance. So a cached
  reference sees every MCM edit, reset and preset. This is the contract `BattleBalanceSettingsProvider`,
  `NameplateRelationSettingsProvider` and `NameplateFadeSettingsProvider` already ship on.

### The providers this plan changes (excerpts at `dffdf879`)

All nine are registered `Reuse.Singleton` in their feature's `*IoC.cs` (one instance per process); none is
constructed with `new` in `Main/` (`git grep` checked).

1. `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` (per blow). Lines 14-38:

   ```csharp
       public CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider)
       {
           _defaults = configProvider.GetConfig();
       }

       private bool MasterEnabled => TaomSettings.Instance?.EnableCombatMechanics ?? _defaults.Enabled;

       public bool SkillCrushThroughEnabled => MasterEnabled && (TaomSettings.Instance?.EnableSkillCrushThrough ?? _defaults.CrushThrough.SkillBasedEnabled);
       ...
       public bool CultureChargeDamageEnabled => MasterEnabled && (TaomSettings.Instance?.EnableCultureChargeDamage ?? _defaults.ChargeDamage.Enabled);
   ```

   Every `*Enabled` getter reads `Instance` twice (master plus its own). Lines 40-70 hold the five float
   getters (`CrushThroughMaxChance`, `ChargeAutoKnockdownWeightRatio` (which also reads
   `ChargeNeutralWeightRatio`), `ChargeNeutralWeightRatio`, `ChargeHorsePenetration`,
   `ChargeMinPenetrationFactor`), each `SettingClamp.Clamp(TaomSettings.Instance?.X, <json>, min, max)`.
   Read per hit by `CreatureCombatService.cs:67, :82, :90`, `RaceCombatModifiersResolver.cs:35`,
   `CrushThroughService.cs:88, :100, :104, :139`, `ChargeKnockdownService.cs:40, :66, :78-79`,
   `ShieldPenetrationService.cs:51, :71`, and per mount stat update by `ChargeDamageService.cs:25`.
2. `Main/Features/BlowDiagnostics/BlowDiagnosticsSettingsProvider.cs` (per blow, even while OFF), whole class:

   ```csharp
   public sealed class BlowDiagnosticsSettingsProvider : IBlowDiagnosticsSettingsProvider
   {
       public bool IsEnabled =>
           BlowDiagnosticsSettings.Instance?.EnableBlowDiagnostics ?? false;
   }
   ```

   `BlowDiagnosticsSettings` (`BlowDiagnosticsSettings.cs:11`) is its own MCM class:
   `public sealed class BlowDiagnosticsSettings : AttributeGlobalSettings<BlowDiagnosticsSettings>`, default
   `EnableBlowDiagnostics = false`. Read from `Agent_HandleBlowAux_BlowDiag_Patch.cs:32-33`
   (`if (svc == null || !svc.IsEnabled) return;`, through `BlowDiagnosticService.IsEnabled =>
   _settings.IsEnabled`, `BlowDiagnosticService.cs:26`).
3. `Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs` (per frame, and per unit per frame
   through `FormationLayoutService.cs:68` `if (!_settings.IsEnabled) return null;`), lines 7-14:

   ```csharp
       public bool IsEnabled => TaomSettings.Instance?.EnableMixedFormations ?? true;
       public FormationLayoutType DefaultLayout =>
           ResolveLayout(TaomSettings.Instance?.MixedFormationsDefaultLayout ?? 0);
       public string CycleHotkey => TaomSettings.Instance?.MixedFormationsCycleHotkey ?? "L";
       public bool IsDebugMode => TaomSettings.Instance?.MixedFormationsDebug ?? false;
   ```

   `ResolveLayout`: 0 InfantryFrontRangedBack, 1 RangedFrontInfantryBack, 2 RangedWingsInfantryCenter,
   3 Checkerboard, anything else InfantryFrontRangedBack (`FormationLayoutType` is in
   `TAOM.Features.MixedFormations.Models`).
4. `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs` (per frame from
   `BattleActionBarMissionView.cs:76` and `OOBOverlayService.cs:73`; per formation order from
   `Patch35_Formation_SetMovementOrder.cs:37`). Ten getters, lines 10-21, each
   `TaomSettings.Instance?.<SameName> ?? <default>`, and the getter names equal the `TaomSettings` property
   names: `EnableCompanionRoleTooltips` (true), `EnableOOBRoleDisplay` (true), `CompanionRolesDebug`
   (false), `EnableFormationPresets` (false), `MaxFormationPresets` (int, 10), `FormationPresetsDebug`
   (false), `EnableBattleActionBar` (true), `CancelStanceOnMove` (true), `EnableVolleyFire` (true),
   `BattleActionBarDebug` (false).
5. `Main/Features/DreadAura/DreadAuraSettingsProvider.cs` (per frame: `DreadAuraMissionLogic.cs:73`
   `if (!(_timeSinceStart >= WarmupSeconds) || !_service.IsEnabled)`, and `DreadAuraService.cs:33`
   `public bool IsEnabled => _settings.IsEnabled;`). Lines 28-46:

   ```csharp
       public DreadAuraSettingsProvider(IDreadAuraConfigProvider configProvider)
       {
           _defaults = configProvider.GetConfig();
       }

       public bool IsEnabled => TaomSettings.Instance?.EnableDreadAura ?? _defaults.Enabled;

       public float Radius
           => SettingClamp.Clamp(TaomSettings.Instance?.DreadAuraRadius, DefaultRadius, MinRadius, MaxRadius);
       ...
       public float MoralePerSecond
           => SettingClamp.Clamp(
               TaomSettings.Instance?.DreadAuraMoralePerSecond, DefaultMoralePerSecond, 0f, MaxMoralePerSecond);

       public bool AffectsPlayerTroops => TaomSettings.Instance?.DreadAuraAffectsPlayerTroops ?? true;
   ```

   `MinRadius = 4f`, `MaxRadius = 30f`, `MaxMoralePerSecond = 20f`; `InnerRadius` is JSON only.
6. `Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs` (per seat per frame:
   `TaomHowdahStandingPoint.cs:219` `if (_diagnostics?.IsEnabled == true)`), line 10:
   `public bool IsEnabled => TaomSettings.Instance?.EnableHowdahDiagnostics ?? true;`
7. `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs` (per frame:
   `SmartCavalryAIMissionBehavior.cs:66` and, per cavalry formation, `:93`), lines 8-24: `IsEnabled`
   (`EnableSmartCavalryAI ?? false`), `AvoidFriendlies` (`SmartCavalryAvoidFriendlies ?? true`),
   `ChargeFormationStrictness` (`SettingClamp.Clamp(..SmartCavalryChargeStrictness, 0.7f, 0.0f, 1.0f)`),
   `ReformDistanceAfterCharge` (`..SmartCavalryReformDistance, 25f, 10f, 80f`), `ChargeLineSpacing`
   (`..SmartCavalryLineSpacing, 1.2f, 0.8f, 3.0f`), `MaxLineUpSeconds` (`..SmartCavalryMaxLineUpSeconds,
   4f, 1f, 15f`), `IsDebugMode` (`SmartCavalryDebug ?? false`).
8. `Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs` (per agent stat update:
   `TaomAgentStatCalculateModel.UpdateAgentStats`, `TaomAgentStatCalculateModel.cs:113-114` calls
   `_aggression.Profile(...)`, which reads `IsAggressionEnabled`, `CultureAggressionService.cs:17`), lines
   7-19:

   ```csharp
       public CultureDoctrineSettingsProvider(ICultureDoctrineConfigProvider config)
       {
           _config = config;
       }

       public bool IsEnabled =>
           (TaomSettings.Instance?.EnableCultureDoctrine ?? false) && _config.GetCatalog().Enabled;
       public bool IsDebug => TaomSettings.Instance?.CultureDoctrineDebug ?? false;
       public bool IsMoraleEnabled => IsEnabled && (TaomSettings.Instance?.CultureDoctrineMorale ?? false);
       public bool IsAggressionEnabled => IsEnabled && (TaomSettings.Instance?.CultureDoctrineAggression ?? false);
   ```

   Note: the no-MCM fallbacks of `CultureDoctrineMorale` and `CultureDoctrineAggression` are `false` while
   the compiled MCM defaults are `true` (`TaomSettings.cs:835, :840`). That mismatch is pre-existing and
   OUT OF SCOPE (no default changes); the pins below pin today's `false`.
9. `Main/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProvider.cs` (per frame:
   `SiegePropDiagnosticsMissionBehavior.cs:50` `if (!_settings.IsEnabled) return;`), lines 10-12:
   `IsEnabled => TaomSettings.Instance?.EnableSiegePropDiagnostics ?? false;`
   `IsVerbose => TaomSettings.Instance?.SiegePropDiagnosticsVerbose ?? false;`

Compiled MCM defaults these tests rely on (`Main/Features/TaomSettings.cs`): `EnableSiegePropDiagnostics`
false (:492), `SiegePropDiagnosticsVerbose` false (:497), `EnableHowdahDiagnostics` true (:532),
`EnableMixedFormations` true (:663), `MixedFormationsDefaultLayout` 0 (:668), `MixedFormationsCycleHotkey`
"L" (:673), `MixedFormationsDebug` false (:678), `EnableSmartCavalryAI` false (:787),
`SmartCavalryAvoidFriendlies` true, `SmartCavalryChargeStrictness` 0.7, `SmartCavalryReformDistance` 25,
`SmartCavalryLineSpacing` 1.2, `SmartCavalryMaxLineUpSeconds` 4, `SmartCavalryDebug` false (:792-817),
`EnableCultureDoctrine` false (:825), `CultureDoctrineDebug` false (:830), `CultureDoctrineMorale` true
(:835), `CultureDoctrineAggression` true (:840), the ten CompanionTactics values listed above (:905-954),
`EnableCombatMechanics` and the eight other combat toggles true except `EnableShieldPenetration` false
(:1128-1168, :1198), `CrushThroughMaxChance` 0.5f (:1173), `ChargeAutoKnockdownWeightRatio` int 6
(:1178), `ChargeNeutralWeightRatio` 6f (:1183), `ChargeHorsePenetration` 0.4f (:1188),
`ChargeMinPenetrationFactor` 1f (:1193), `EnableDreadAura` true, `DreadAuraRadius` 12f,
`DreadAuraMoralePerSecond` 5f, `DreadAuraAffectsPlayerTroops` true (:1215-1230). JSON side
(`CombatMechanicsConfig.cs`): `ChargeKnockdown.MaxPenetrationFactor` 2.5f (:120). `SettingClamp`
(`Main/Core/Validation/SettingClamp.cs`): `Clamp(float? value, float default, float min, float max)` and
`Clamp(int? value, int default, int min, int max)`; null becomes the default, then the value is clamped.

### The per-hit gates to reorder (all operands are side-effect-free; the order cannot change a result)

- `Main/Features/CombatMechanics/CreatureCombatService.cs:67`:
  `if (!_settings.CreatureCleaveEnabled || !isColliderAgent || float.IsNaN(originalMomentum))`
- `CreatureCombatService.cs:82`:
  `if (!_settings.CreatureCleaveEnabled || !isColliderAgent || !(momentumRemaining > 0f) || inflictedDamage <= 0)`
- `CreatureCombatService.cs:90`: `if (!_settings.CreatureUnstoppableEnabled || victimMonsterId == null)`
- `Main/Features/CombatMechanics/CrushThroughService.cs:88-91`:

  ```csharp
          if (_settings.MonsterCrushThroughEnabled
              && context.AttackerMonsterId != null
              && _monsterCrushIds.Contains(context.AttackerMonsterId)
              && !context.DefendItemIsShield)
  ```
- `CrushThroughService.cs:100-102`:

  ```csharp
          bool orcQualified = _settings.OrcShieldCrushEnabled
              && context.IsAiControlled
              && IsOrcShieldCrushRace(context.AttackerRaceId);
  ```
- `Main/Features/CombatMechanics/ChargeKnockdownService.cs:40-43`:

  ```csharp
          if (!_settings.ChargeKnockdownEnabled)
              return null;
          if (!context.IsHorseCharge)
              return null;
  ```
- `Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs:35-42`:

  ```csharp
          if (!_settings.RaceCombatModifiersEnabled)
              return RaceCombatModifiers.Neutral;

          // Validate BEFORE any name lookup ...
          if (!raceId.HasValue || !_raceManager.IsValidRaceId(raceId.Value))
              return RaceCombatModifiers.Neutral;
  ```

Left as is, on purpose: `ShieldPenetrationService.cs:51` (`!_settings.ShieldPenetrationEnabled ||
!IsGranted(...)`: the toggle defaults off and `IsGranted` is two hash lookups, so the cached toggle is
already the cheaper first check) and `ChargeDamageService.cs:25` (already checks the string first).

### The three behaviour changes

**A. The cycle hotkey parse.** `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs`
(150 lines at `dffdf879`), called every frame from `OnMissionTick` (`:61` `HandleCycleHotkey();`):

```csharp
    private bool TryResolveCycleKey(out InputKey key)
    {
        var raw = _settings.CycleHotkey?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            key = default;
            return false;
        }
        return Enum.TryParse(raw, ignoreCase: true, out key);
    }
```

(lines 124-133). On .NET Framework `Enum.TryParse` splits the string on commas (an array allocation) and
scans the enum's names on every call. The new code must return exactly what this returns for every input,
including `Enum.TryParse`'s acceptance of numeric strings and comma lists (do NOT switch to
`Main/Core/Validation/EnumNames.cs`: that would change which strings are accepted).

**B. `Patch35_Formation_SetMovementOrder` order.**
`Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs:34-52`:

```csharp
        try
        {
            _settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();
            if (_settings == null || !_settings.CancelStanceOnMove) return;
            if (__instance == null) return;

            // Phase 9b #149 — team filter. ...
            if (__instance.Team != Mission.Current?.PlayerTeam) return;

            _stances ??= IoC.Resolve<ITroopStanceManager>();
            _stances?.ClearStance((int)__instance.FormationIndex);
        }
```

The postfix runs on every `Formation.SetMovementOrder` of every team, mostly from the async AI tick.
Engine facts (`pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Formation` and `...Mission`, v1.5.3):
`Formation.Team` is a field, `public readonly Team Team;` (Formation.cs:90); `Mission.PlayerTeam` is
`get { return Teams.Player; }` (Mission.cs:1304); `Formation.SetMovementOrder(MovementOrder input)` is
`public void` (Formation.cs:707). Both filter operands are side-effect-free reads, so the team filter can
move ahead of the settings read without changing any outcome.

**C. The English-override probe.**
`Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs:23-61` (62 lines; line 1
is `using System.Collections.Generic;`, there is no `using System;`):

```csharp
    private static readonly Dictionary<string, string> _overrides = new();
    ...
    [HarmonyPrefix]
    public static bool Prefix(string text, ref string __result)
    {
        if (text == null || text.Length <= 2 || text[0] != '{' || text[1] != '=')
            return true;

        int end = text.IndexOf('}', 2);
        if (end < 0)
            return true;

        int idLength = end - 2;
        if (idLength == 1 && (text[2] == '!' || text[2] == '*'))
            return true;

        string id = text.Substring(2, idLength);

        if (_overrides.TryGetValue(id, out string overrideText))
        {
            __result = overrideText;
            return false;
        }

        return true;
    }

    public static void RegisterOverride(string id, string text)
    {
        _overrides[id] = text;
    }

    public static void ClearOverrides()
    {
        _overrides.Clear();
    }
```

The prefix runs on every localized text resolve (`MBTextManager.GetLocalizedText`, signature from
`pwsh tools/taom-src.ps1 path TaleWorlds.Localization.MBTextManager`:
`internal static string GetLocalizedText(string text)`, MBTextManager.cs:232) and allocates the id
substring every time. Callers: `Main/SubModule.cs:283` (`RegisterOverride` once per entry at load) and the
tests in `TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs`. The
dictionary is written only at module load and in tests.

**A source scan constrains where a nested type may go in this file.**
`TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` scans every `Main/**/*.cs`, blanks the
comments, finds each `static bool Prefix(` (`BoolPrefixPattern`, :328-330) and names its owner after the
LAST `class` declaration above it in the file (`ClassDeclPattern`, :334-336:
`^\s*(?:(?:public|private|internal|protected|static|sealed|partial|abstract)\s+)*class\s+(\w+)`; owner
lookup `classes.LastOrDefault(c => c.Index < prefix.Index)`, :395). Its `Registry` (:57) holds the key
`MBTextManager_GetLocalizedText_Patch` (:200). A nested `class` declared above `Prefix` therefore becomes
the owner, and two tests fail: `EveryBoolPrefix_HasACoopDisposition` (:404, the nested class is
unclassified) and `Registry_HasNoStaleEntries` (:422, the real key goes stale). A `struct` does not match
the pattern; a nested class placed after `Prefix` is harmless. Step 18 puts both new nested types at the
end of the class for this reason. The `Registry` is a gate: never add an entry to make it pass.

### Doc lines this plan updates (quoted at `dffdf879`; find each by its text, not its line number)

- `docs/features/combat-mechanics.md` (line 91): contains `` (`CombatMechanicsSettingsProvider:33`) ``.
- `docs/modding/file-catalogue.md`, the table row beginning `` | `combat_mechanics/combat_mechanics_config.json` ``
  (line 268): contains `` (`CombatMechanicsSettingsProvider.cs:19-35`) ``; the row beginning
  `` | `dread_aura/dread_aura_config.json` `` (line 280): contains `` (`DreadAuraSettingsProvider.cs:33-36`) ``.
  Both ranges stop holding once Steps 5 and 11 add the comment, field, accessor and constructor.
- `docs/features/companion-tactics.md`: the table row
  `` | `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs` | Reads `TaomSettings.Instance` directly (no reflection) | `` (line 139);
  the bullet beginning `` - `ICompanionTacticsSettingsProvider` `` and ending
  `` typed read from `TaomSettings.Instance` (testable seam). `` (line 162, with an em dash after the
  interface name); the bullet beginning `` - `SharedMovementOrderPostfixTests.cs` `` that says `5 tests` (line
  181; the class has 5 `[TestMethod]`s today).
- `docs/features/mixed-formations.md` (line 149): ends with
  ``every frame polls `Input.IsKeyDown` for the cycle hotkey. No allocations in the hot path.``
- `docs/features/localization-override.md`: step 3 of the flow (line 36) contains
  `looks up the ID in the static override dictionary`; the test bullet (line 98) says `**11 tests**` (the
  class has 11 `[TestMethod]`s today).

### Conventions that bind this change

- ADR-002 (`docs/adrs/002-thin-entry-points.md`): entry points (Harmony patches, MissionBehaviors) stay
  under 150 lines and delegate. `MixedFormationsMissionBehavior.cs` is at 150 now; this plan shrinks it.
- ADR-007 (`docs/adrs/007-adapter-pattern.md`): no sealed TaleWorlds type inside a service. The new
  `CachedEnumParse<TEnum>` is generic; `InputKey` appears only in the behaviour that uses it.
- ADR-008 (`docs/adrs/008-testability-requirements.md`): services and pure helpers 100% covered; Harmony
  and MissionBehavior bodies are game-tested.
- ADR-003 no `#region`; ADR-005 no `#if DEBUG`.
- `.claude/rules/csharp-architecture.md` "IoC Lifetimes" (providers are `Reuse.Singleton`; do not change
  a lifetime) and "Config Providers MUST Validate" (keep every `SettingClamp` exactly as it is).
- `.claude/rules/harmony-patches.md`: before editing a patch, read
  `docs/reviews/lessons/harmony-il.md` and the patch's section in
  `docs/reference/harmony-patch-registry.md` (Patch35 CompanionTactics and Patch25 LocalizationOverride).
  "Which thread runs your target": `Formation.SetMovementOrder` runs on the async AI tick; the `??=`
  lazy-static pattern is tolerable there only for an idempotent resolve. The new lazy `Settings` accessors
  are idempotent (every thread that races resolves the same object), so the providers need no lock.
- `.claude/rules/tests.md`: MSTest plus NSubstitute, names `Method_State_Expected`; a test that executes
  engine code carries `[TestCategory("RequiresGame")]`. Nothing in this plan's tests executes engine code
  (`TaomSettings` and `BlowDiagnosticsSettings` are MCM types, which the BattleBalance tests already
  construct untagged); the hotkey tests use `System.DayOfWeek`, never `InputKey`.
- `.claude/rules/simplicity-criterion.md`: two audit leads were rejected under it (see Maintenance notes);
  do not add them back.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, graph refreshed at `dffdf879`)

- `CombatMechanicsSettingsProvider`: `CombatMechanicsSettingsProviderTests` only (DI-resolved; the graph
  does not follow DryIoc registrations).
- `CreatureCombatService`, `CrushThroughService`, `ChargeKnockdownService`, `RaceCombatModifiersResolver`:
  their own test classes only (`[TestMethod]` counts at `dffdf879`: `CreatureCombatServiceTests` 26,
  `CrushThroughServiceTests` 22, `ChargeKnockdownServiceTests` 21, `RaceCombatModifiersResolverTests` 11).
- `MBTextManager_GetLocalizedText_Patch`: `Main/SubModule.cs:283` (`OnSubModuleLoad`) and the 11
  `MBTextManager_GetLocalizedText_PatchTests` methods.
- `BlowDiagnosticsSettingsProvider`, `MixedFormationsSettingsProvider`, `MixedFormationsMissionBehavior`,
  `CompanionTacticsSettingsProvider`, `DreadAuraSettingsProvider`, `HowdahDiagnosticsSettingsProvider`,
  `SmartCavalryAISettingsProvider`, `CultureDoctrineSettingsProvider`,
  `SiegePropDiagnosticsSettingsProvider`, `Patch35_Formation_SetMovementOrder`: "No affected nodes found"
  (reached only through DryIoc or Harmony). The interfaces and every consumer listed above stay unchanged.

## Commands you will need

Prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"` your dispatch rules give.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 1` (only `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, `Passed` = 12345 plus the new test results |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Data | `python tools/validate_moduledata.py` | not required: this plan changes no ModuleData |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Both MSBuild flags go on build AND test. Never `./build.ps1`: it deploys into the game install.
`dotnet test` builds `Main` and `TAOM.Tests` itself, so a filtered run is also the RED build check.

## Scope

**In scope** (the only files you modify or create):
- `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs`
- `Main/Features/CombatMechanics/CreatureCombatService.cs` (gate order only)
- `Main/Features/CombatMechanics/CrushThroughService.cs` (gate order only)
- `Main/Features/CombatMechanics/ChargeKnockdownService.cs` (gate order only)
- `Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs` (gate order only)
- `Main/Features/BlowDiagnostics/BlowDiagnosticsSettingsProvider.cs`
- `Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs`
- `Main/Features/MixedFormations/CachedEnumParse.cs` (new)
- `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs` (the hotkey resolve only)
- `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs`
- `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` (order only)
- `Main/Features/DreadAura/DreadAuraSettingsProvider.cs`
- `Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs`
- `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs`
- `Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs`
- `Main/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProvider.cs`
- `Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs`
- Tests: `TAOM.Tests/Features/HotPathSettingsProvidersTests.cs` (new),
  `TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs`,
  `TAOM.Tests/Features/CombatMechanics/CreatureCombatServiceTests.cs`,
  `TAOM.Tests/Features/CombatMechanics/CrushThroughServiceTests.cs`,
  `TAOM.Tests/Features/CombatMechanics/ChargeKnockdownServiceTests.cs`,
  `TAOM.Tests/Features/CombatMechanics/RaceCombatModifiersResolverTests.cs`,
  `TAOM.Tests/Features/BlowDiagnostics/BlowDiagnosticsSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/MixedFormations/MixedFormationsSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/MixedFormations/CachedEnumParseTests.cs` (new),
  `TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs`,
  `TAOM.Tests/Features/DreadAura/DreadAuraSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/Elephant/HowdahDiagnosticsSettingsProviderTests.cs`,
  `TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/CultureDoctrine/CultureDoctrineSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProviderTests.cs` (new),
  `TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs`
- Docs (only the lines quoted in "Doc lines this plan updates"): `docs/features/combat-mechanics.md`,
  `docs/modding/file-catalogue.md`, `docs/features/companion-tactics.md`, `docs/features/mixed-formations.md`,
  `docs/features/localization-override.md`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner) and every `*IoC.cs`: no
  registration or lifetime changes. If one seems needed, STOP and report the exact line.
- `Main/Features/MixedFormations/Hooks/Patch30_*`, `Main/Features/MixedFormations/FormationLayoutService.cs`
  (and its lock) and the Patch93 wield getters: plan 032 owns them. `FormationLayoutService.cs:68` gets
  cheaper through the provider alone.
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs`: plan 030 deletes it.
- `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs`: its `FieldInfo`s are already
  cached (`EnsureInitialized`, lines 56-67) and its settings read (line 73) gets cheaper through the
  provider; no edit.
- `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs`: its per-frame read
  (line 76) gets cheaper through the provider; the adapter-reuse lead is rejected (Maintenance notes).
- `Main/Features/BlowDiagnostics/Hooks/Agent_HandleBlowAux_BlowDiag_Patch.cs`,
  `Main/Features/Elephant/TaomHowdahStandingPoint.cs`, `DreadAuraMissionLogic.cs`,
  `SmartCavalryAIMissionBehavior.cs`, `SiegePropDiagnosticsMissionBehavior.cs`,
  `TaomAgentStatCalculateModel.cs`, `ShieldPenetrationService.cs`, `ChargeDamageService.cs`: consumers,
  unchanged.
- `Main/Features/TaomSettings.cs` and `BlowDiagnosticsSettings.cs`: any MCM default change, any fallback
  change (including the CultureDoctrine mismatch above).
- Any save-format change. `CHANGELOG.md`. `plans/README.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an entry to a validator allowlist. STOP and report instead. This
  includes `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` and its `Registry`: if it
  fails after Step 18, the nested types are in the wrong place (see "A source scan constrains where a
  nested type may go"); move them, never add a registry entry.

## Git workflow

- Commit on the branch you were given; never push or open a PR. Three commits, one per stage (Steps 9,
  12 and 19). Stage explicit paths only.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `dffdf879`; a hook refuses any
  other). Write the message to a file and run `git commit -F "<file>"`; never `--no-verify`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. No AI attribution trailer. Trailers you will use: `Not-tested:` (named per stage below).

## Steps

### Step 1: drift check and the base

Run the drift check from the top of this file. Then run the full suite once before any edit:
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.

**Verify**: the totals line reads `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`, the one
failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`. Write the line into your report. A different
failure set is a STOP condition.

### Step 2: read the conventions

Read `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`,
`TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`, `TAOM.Tests/Migration/IlCallScanner.cs`
(only `ExtractCalledMethods`), `docs/reviews/lessons/harmony-il.md`, and the Patch35 (CompanionTactics) and
Patch25 (LocalizationOverride) sections of `docs/reference/harmony-patch-registry.md`.

**Verify**: `git grep -n "_settings ??= TaomSettings.Instance" -- Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`
prints one line (the exemplar is what this plan says it is). If it prints nothing, STOP.

### Step 3: RED, the IL rule for the per-blow providers

Create `TAOM.Tests/Features/HotPathSettingsProvidersTests.cs`. Draft (re-check it compiles and that the
names it reflects on exist):

```csharp
using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CombatMechanics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features;

/// <summary>
/// Settings providers read on a hot path (per blow, per frame, per agent update) take the MCM settings
/// reference once, in a private lazy <c>Settings</c> accessor, and read through it; no other member may
/// resolve MCM's <c>Instance</c>, which walks MCM's settings containers on every call. The accessor caches
/// only a non-null instance, so a resolve before MCM is up cannot pin the defaults. Pattern:
/// BattleBalanceSettingsProvider (02157b18).
/// </summary>
[TestClass]
public class HotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type the
    // settings class derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null
        && (m.DeclaringType.IsAssignableFrom(typeof(TaomSettings))
            || m.DeclaringType.IsAssignableFrom(typeof(BlowDiagnosticsSettings)));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CombatMechanicsSettingsProvider))]
    [DataRow(typeof(BlowDiagnosticsSettingsProvider))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // A second (internal, test-only) constructor must not break DryIoc's single-public-constructor
    // selection at registration or resolve.
    [DataTestMethod]
    [DataRow(typeof(ICombatMechanicsSettingsProvider), typeof(CombatMechanicsSettingsProvider))]
    [DataRow(typeof(IBlowDiagnosticsSettingsProvider), typeof(BlowDiagnosticsSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }
}
```

If DryIoc's non-generic `RegisterInstance(Type, object)` or `Register(Type, Type, IReuse)` overloads differ
in this DryIoc version, adapt the call shape (it is test code) and say so in your report; do not drop the
test.

Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HotPathSettingsProvidersTests"`.

**Verify**: the build succeeds; both `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance` rows FAIL on the
`Assert.IsNotNull` ("the private lazy Settings accessor"); both `Provider_ResolvesFromARealContainer` rows
PASS (they guard Step 5, they are not RED). Quote the two failure messages.

### Step 4: RED, read-through and default pins for the per-blow providers

**4a.** In `TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs` (keep every
existing test) add:

- `Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply`: for each row below, build a fresh
  `var mcm = new TaomSettings();`, a config provider substitute returning `new CombatMechanicsConfig()`, and
  `var sut = new CombatMechanicsSettingsProvider(configProvider, mcm);` (the internal constructor Step 5
  adds); read EVERY public property of `ICombatMechanicsSettingsProvider` once (warms any cache); apply
  the row's edit to `mcm`; assert the row's getter returns the expected value (message: the row index).
  Rows (edit, getter, expected after the edit):

  | Edit on `mcm` | Getter | Expected |
  |---|---|---|
  | `EnableSkillCrushThrough = false` | `SkillCrushThroughEnabled` | `false` |
  | `EnableMonsterCrushThrough = false` | `MonsterCrushThroughEnabled` | `false` |
  | `EnableOrcShieldCrush = false` | `OrcShieldCrushEnabled` | `false` |
  | `EnableCreatureCleave = false` | `CreatureCleaveEnabled` | `false` |
  | `EnableCreatureUnstoppable = false` | `CreatureUnstoppableEnabled` | `false` |
  | `EnableChargeKnockdown = false` | `ChargeKnockdownEnabled` | `false` |
  | `EnableShieldPenetration = true` | `ShieldPenetrationEnabled` | `true` |
  | `EnableRaceCombatModifiers = false` | `RaceCombatModifiersEnabled` | `false` |
  | `EnableCultureChargeDamage = false` | `CultureChargeDamageEnabled` | `false` |
  | `CrushThroughMaxChance = 0.25f` | `CrushThroughMaxChance` | `0.25f` |
  | `ChargeAutoKnockdownWeightRatio = 10` | `ChargeAutoKnockdownWeightRatio` | `10f` |
  | `ChargeNeutralWeightRatio = 4f` | `ChargeNeutralWeightRatio` | `4f` |
  | `ChargeHorsePenetration = 0.6f` | `ChargeHorsePenetration` | `0.6f` |
  | `ChargeMinPenetrationFactor = 0.5f` | `ChargeMinPenetrationFactor` | `0.5f` |

  (Every expected value differs from the compiled MCM default, so a getter that snapshots, or reads JSON
  instead of the settings, fails its row. `10` passes the auto ratio's clamp because the neutral ratio
  stays at its default 6, so the floor is 6.)
- `MasterToggleOff_EveryEnabledGetterReportsDisabled`: same construction, warm, set
  `mcm.EnableCombatMechanics = false`, then assert all nine `*Enabled` getters return `false`.
- `NoMcm_TogglesAndMaxChance_FallBackToJson` (a pin; green before and after): with the existing `_sut`
  (public constructor, `_config` from `Setup`), set `_config.Enabled = true`,
  `_config.ShieldPenetration.Enabled = true`, `_config.CrushThrough.SkillBasedEnabled = false`,
  `_config.Creatures.CleaveEnabled = false`, `_config.CrushThrough.MaxSkillChance = 0.3f`; assert
  `ShieldPenetrationEnabled` true, `SkillCrushThroughEnabled` false, `CreatureCleaveEnabled` false,
  `MonsterCrushThroughEnabled` true, `RaceCombatModifiersEnabled` true, `CrushThroughMaxChance` 0.3f
  (delta 0.0001f). Then set `_config.Enabled = false` and assert all nine `*Enabled` getters are false.

**4b.** Create `TAOM.Tests/Features/BlowDiagnostics/BlowDiagnosticsSettingsProviderTests.cs` (namespace
`TAOM.Tests.Features.BlowDiagnostics`) with:

- `IsEnabled_NoMcm_DefaultsOff`: `Assert.IsFalse(new BlowDiagnosticsSettingsProvider().IsEnabled);`
- `CompiledDefault_MatchesTheProviderFallback`: `Assert.IsFalse(new BlowDiagnosticsSettings().EnableBlowDiagnostics);`
- `IsEnabled_ReadsThroughTheCachedSettings_SoLiveMcmEditsApply`:
  `var mcm = new BlowDiagnosticsSettings(); var sut = new BlowDiagnosticsSettingsProvider(mcm);`
  assert false, set `mcm.EnableBlowDiagnostics = true`, assert true, set it back to false, assert false.

Run the CombatMechanicsSettingsProviderTests and BlowDiagnosticsSettingsProviderTests filters.

**Verify**: the test project FAILS TO BUILD; the errors are on the two-argument
`CombatMechanicsSettingsProvider` construction and the one-argument `BlowDiagnosticsSettingsProvider`
construction (a constructor-arity diagnostic such as CS1729), and nothing else. Any other compile error is
yours to fix before moving on.

### Step 5: GREEN, cache the two per-blow providers

`CombatMechanicsSettingsProvider.cs`: add

```csharp
    // HOT PATH: read per melee blow (crush-through, cleave, stagger, knockdown, shield penetration) and
    // per mount stat update. Resolving TaomSettings.Instance walks MCM's settings containers, so the
    // reference is cached on its first non-null read and read THROUGH, never snapshotted: MCM edits its
    // one registered instance in place (reset and presets copy values into it), so live MCM edits still
    // apply. Lazy, not in the constructor, so a resolve before MCM is up cannot pin the JSON fallbacks.
    // Same contract as BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;
```

keep the public constructor exactly as it is, add

```csharp
    internal CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;
```

and replace every `TaomSettings.Instance` in the file's getters (including `MasterEnabled` and the local
`var mcm = TaomSettings.Instance?.ChargeAutoKnockdownWeightRatio;`) with `Settings`. Nothing else in the
getters changes: same fallbacks, same `SettingClamp` calls, same bounds.

`BlowDiagnosticsSettingsProvider.cs`: keep the class comment, then

```csharp
    // Read per damaging blow even while OFF (Agent_HandleBlowAux_BlowDiag_Patch), so the MCM reference
    // is cached on its first non-null read and read through (BattleBalanceSettingsProvider pattern).
    private BlowDiagnosticsSettings? _settings;
    private BlowDiagnosticsSettings? Settings => _settings ??= BlowDiagnosticsSettings.Instance;

    public BlowDiagnosticsSettingsProvider() { }
    internal BlowDiagnosticsSettingsProvider(BlowDiagnosticsSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableBlowDiagnostics ?? false;
```

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0; the filters
`HotPathSettingsProvidersTests`, `CombatMechanicsSettingsProviderTests`,
`BlowDiagnosticsSettingsProviderTests` all pass with 0 failed, and the totals show the new tests ran (not 0).

### Step 6: RED, the per-hit gates read the toggle last

Add these tests (each: arrange as the class's `SetUp` does, call `_settings.ClearReceivedCalls();` right
before the act, then assert with `_ = _settings.DidNotReceive().<Toggle>;`, and also assert the result is
unchanged from today):

| File | Test | Act | Not read | Result |
|---|---|---|---|---|
| `CreatureCombatServiceTests.cs` | `CalculateCleaveMomentum_NotColliderAgent_NeverReadsTheToggle` | `_sut.CalculateCleaveMomentum("cave_troll", 10f, isColliderAgent: false)` | `CreatureCleaveEnabled` | `null` |
| same | `ShouldForceSliceThrough_NotColliderAgent_NeverReadsTheToggle` | `_sut.ShouldForceSliceThrough("cave_troll", 5f, isColliderAgent: false, inflictedDamage: 10)` | `CreatureCleaveEnabled` | `false` |
| same | `IsUnstoppable_NullMonsterId_NeverReadsTheToggle` | `_sut.IsUnstoppable(null, 5)` | `CreatureUnstoppableEnabled` | `false` |
| `CrushThroughServiceTests.cs` | `DecideCrushThrough_NoMonsterId_NeverReadsTheMonsterToggle` | `_sut.DecideCrushThrough(Ctx(attackerMonsterId: null))` | `MonsterCrushThroughEnabled` | whatever the same call returns today (assert it equals a second call made before `ClearReceivedCalls`) |
| same | `DecideCrushThrough_PlayerControlled_NeverReadsTheOrcToggle` | `_sut.DecideCrushThrough(Ctx(isAiControlled: false))` | `OrcShieldCrushEnabled` | same rule as the row above |
| `ChargeKnockdownServiceTests.cs` | `DecideChargeKnockdown_NotHorseCharge_NeverReadsTheToggle` | `_sut.DecideChargeKnockdown(Context(isHorseCharge: false))` | `ChargeKnockdownEnabled` | `null` |
| `RaceCombatModifiersResolverTests.cs` | `Resolve_NullRaceId_NeverReadsTheToggle` | `_sut.Resolve(null)` | `RaceCombatModifiersEnabled` | `RaceCombatModifiers.Neutral` |

Check the helper signatures (`Ctx(...)` in `CrushThroughServiceTests`, `Context(...)` in
`ChargeKnockdownServiceTests`, the `in` parameters of `DecideCrushThrough`/`DecideChargeKnockdown`) in the
files and adapt the call shape; the table's intent is binding, its call text is a draft.

Run the four class filters.

**Verify**: the build succeeds and exactly these seven new tests FAIL, each on its `DidNotReceive`
assertion (NSubstitute's "Expected to receive no calls matching ... get_<Toggle>"). Every pre-existing test
in the four classes passes.

### Step 7: GREEN, reorder the gates

Change only the order of the operands; keep every comment (move a comment with its operand where needed):

- `CreatureCombatService.cs:67`: `if (!isColliderAgent || float.IsNaN(originalMomentum) || !_settings.CreatureCleaveEnabled)`
- `CreatureCombatService.cs:82`: `if (!isColliderAgent || !(momentumRemaining > 0f) || inflictedDamage <= 0 || !_settings.CreatureCleaveEnabled)`
  (the comment above it says "Scalar guards run before any string work"; it stays true).
- `CreatureCombatService.cs:90`: `if (victimMonsterId == null || !_settings.CreatureUnstoppableEnabled)`
- `CrushThroughService.cs:88-91`:

  ```csharp
          if (context.AttackerMonsterId != null
              && !context.DefendItemIsShield
              && _settings.MonsterCrushThroughEnabled
              && _monsterCrushIds.Contains(context.AttackerMonsterId))
  ```
- `CrushThroughService.cs:100-102`:

  ```csharp
          bool orcQualified = context.IsAiControlled
              && _settings.OrcShieldCrushEnabled
              && IsOrcShieldCrushRace(context.AttackerRaceId);
  ```
- `ChargeKnockdownService.cs:40-43`: the `!context.IsHorseCharge` check first, then
  `!_settings.ChargeKnockdownEnabled`.
- `RaceCombatModifiersResolver.cs:35-42`:

  ```csharp
          if (!raceId.HasValue)
              return RaceCombatModifiers.Neutral;

          if (!_settings.RaceCombatModifiersEnabled)
              return RaceCombatModifiers.Neutral;

          // Validate BEFORE any name lookup ... (the existing comment, unchanged)
          if (!_raceManager.IsValidRaceId(raceId.Value))
              return RaceCombatModifiers.Neutral;
  ```

**Verify**: the four class filters (`CreatureCombatServiceTests`, `CrushThroughServiceTests`,
`ChargeKnockdownServiceTests`, `RaceCombatModifiersResolverTests`) pass with 0 failed, and their counts are
the pre-existing counts (26, 22, 21, 11) plus the new ones (3, 2, 1, 1).

### Step 8: stage 1 full suite and docs

Doc edits (find each by the text quoted in "Doc lines this plan updates"):

- `docs/features/combat-mechanics.md`: replace `` (`CombatMechanicsSettingsProvider:33`) `` with
  `` (`CombatMechanicsSettingsProvider.ShieldPenetrationEnabled`) `` (the line number no longer holds).
- `docs/modding/file-catalogue.md`, the `combat_mechanics/combat_mechanics_config.json` row: replace
  `` (`CombatMechanicsSettingsProvider.cs:19-35`) `` with
  `` (the `*Enabled` getters of `CombatMechanicsSettingsProvider.cs`) ``. Change nothing else in the file.

Run the full suite and `python tools/lint_docs.py --fail-on-drift`.

**Verify**: `Failed: 1` (only `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, `Passed`
above 12345 by the number of new test results; lint exits 0;
`git grep -n -F "CombatMechanicsSettingsProvider.cs:19-35" -- docs` prints nothing.

### Step 9: commit stage 1

Stage exactly these fifteen paths, each named on the `git add` line (no directory, no wildcard):

```
Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs
Main/Features/BlowDiagnostics/BlowDiagnosticsSettingsProvider.cs
Main/Features/CombatMechanics/CreatureCombatService.cs
Main/Features/CombatMechanics/CrushThroughService.cs
Main/Features/CombatMechanics/ChargeKnockdownService.cs
Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs
TAOM.Tests/Features/HotPathSettingsProvidersTests.cs
TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs
TAOM.Tests/Features/BlowDiagnostics/BlowDiagnosticsSettingsProviderTests.cs
TAOM.Tests/Features/CombatMechanics/CreatureCombatServiceTests.cs
TAOM.Tests/Features/CombatMechanics/CrushThroughServiceTests.cs
TAOM.Tests/Features/CombatMechanics/ChargeKnockdownServiceTests.cs
TAOM.Tests/Features/CombatMechanics/RaceCombatModifiersResolverTests.cs
docs/features/combat-mechanics.md
docs/modding/file-catalogue.md
```

Before staging, `git status --porcelain` must list exactly these paths (the two new test files as `??`).
Any other path there is a STOP condition: you edited something outside stage 1.
Subject draft: `perf(combat): <version> - read combat settings once, not per blow`. Body draft (re-check
every claim against your diff):

```
The combat-mechanics and blow-diagnostics settings providers resolved
MCM's settings object on every property read, and those reads run on
every melee blow (about eight per ordinary hit, more on a blocked
swing), even with blow diagnostics switched off. Each provider now
caches the reference on its first non-null read and reads through it,
the pattern BattleBalanceSettingsProvider already uses, so changing a
combat setting in the MCM menu mid-battle still takes effect at once.

The cleave, stagger-immunity, crush-through, charge-knockdown and race
modifier checks now test the cheap facts of the hit (is it a horse
charge, is there a monster id, is the attacker AI) before reading a
setting. Every result is unchanged.

Not-tested: in game; toggle a Combat Mechanics setting mid-battle and
check the next blow follows it.
```

**Verify**: `git show --stat HEAD` lists exactly those fifteen paths; `git status --porcelain` is empty.

### Step 10: RED, the per-frame and per-agent providers

**10a.** In `HotPathSettingsProvidersTests.cs` add these rows to BOTH data-driven tests, and add the
`using`s `TAOM.Features.MixedFormations`, `TAOM.Features.CompanionTactics`, `TAOM.Features.DreadAura`,
`TAOM.Features.Elephant`, `TAOM.Features.SmartCavalryAI`, `TAOM.Features.CultureDoctrine` and
`TAOM.Features.SiegePropDiagnostics` (each provider and its interface share one namespace):

- `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance`: `MixedFormationsSettingsProvider`,
  `CompanionTacticsSettingsProvider`, `DreadAuraSettingsProvider`, `HowdahDiagnosticsSettingsProvider`,
  `SmartCavalryAISettingsProvider`, `CultureDoctrineSettingsProvider`, `SiegePropDiagnosticsSettingsProvider`.
- `Provider_ResolvesFromARealContainer`: the same seven, each with its interface
  (`IMixedFormationsSettingsProvider`, `ICompanionTacticsSettingsProvider`, `IDreadAuraSettingsProvider`,
  `IHowdahDiagnosticsSettingsProvider`, `ISmartCavalryAISettingsProvider`,
  `ICultureDoctrineSettingsProvider`, `ISiegePropDiagnosticsSettingsProvider`).

Run `dotnet test ... --filter "FullyQualifiedName~HotPathSettingsProvidersTests"` now, before 10b.
**Verify 10a**: the build succeeds; the seven new `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance` rows
FAIL on "the private lazy Settings accessor" (quote them); the two stage-1 rows and all nine
`Provider_ResolvesFromARealContainer` rows pass.

**10b.** Per provider, create or extend its test file with (1) no-MCM default pins through the public
constructor (green before and after) and (2) a read-through test through the internal constructor that
Step 11 adds: build on a fresh settings object, read every interface getter once, apply the edit, assert
the expected value; one fresh settings object and provider per row.

| Test file | Pins (public constructor, no MCM) | Read-through rows (edit -> getter == expected) |
|---|---|---|
| `MixedFormations/MixedFormationsSettingsProviderTests.cs` (new) | `IsEnabled` true, `DefaultLayout` `InfantryFrontRangedBack`, `CycleHotkey` `"L"`, `IsDebugMode` false | `EnableMixedFormations=false` -> `IsEnabled` false; `MixedFormationsDefaultLayout=3` -> `DefaultLayout` `Checkerboard`; `MixedFormationsCycleHotkey="K"` -> `CycleHotkey` `"K"`; `MixedFormationsDebug=true` -> `IsDebugMode` true |
| `CompanionTactics/CompanionTacticsSettingsProviderTests.cs` (new) | the ten defaults listed in Current state item 4; plus `EveryFallback_EqualsTheMcmCompiledDefault` modelled on BattleBalance's (getter names equal the `TaomSettings` property names) | modelled on `Getters_ReadThroughTheSettings_SoLiveMcmEditsApply` in the BattleBalance tests: for each interface property, flip the same-named `TaomSettings` bool, or add 1 to the int `MaxFormationPresets`, and assert every getter equals its settings property before and after |
| `DreadAura/DreadAuraSettingsProviderTests.cs` (new) | config `new DreadAuraConfig { Enabled = false }` with `Profile.Radius = 15f`, `Profile.MoralePerSecond = 6f`: `IsEnabled` false, `Radius` 15f, `MoralePerSecond` 6f, `AffectsPlayerTroops` true, `InnerRadius` equals `Profile.InnerRadius` | config `new DreadAuraConfig()`: `EnableDreadAura=false` -> `IsEnabled` false; `DreadAuraRadius=20f` -> `Radius` 20f; `DreadAuraMoralePerSecond=8f` -> `MoralePerSecond` 8f; `DreadAuraAffectsPlayerTroops=false` -> `AffectsPlayerTroops` false |
| `Elephant/HowdahDiagnosticsSettingsProviderTests.cs` (extend; keep both tests) | already pinned | `EnableHowdahDiagnostics=false` -> `IsEnabled` false |
| `SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs` (new) | `IsEnabled` false, `AvoidFriendlies` true, `ChargeFormationStrictness` 0.7f, `ReformDistanceAfterCharge` 25f, `ChargeLineSpacing` 1.2f, `MaxLineUpSeconds` 4f, `IsDebugMode` false (floats with delta 0.0001f) | `EnableSmartCavalryAI=true` -> `IsEnabled` true; `SmartCavalryAvoidFriendlies=false` -> false; `SmartCavalryChargeStrictness=0.3f` -> 0.3f; `SmartCavalryReformDistance=40f` -> 40f; `SmartCavalryLineSpacing=2f` -> 2f; `SmartCavalryMaxLineUpSeconds=8f` -> 8f; `SmartCavalryDebug=true` -> `IsDebugMode` true |
| `CultureDoctrine/CultureDoctrineSettingsProviderTests.cs` (new) | catalog `new DoctrineCatalog(enabled: true, DoctrineCatalog.VanillaDefault(), new Doctrine[0])` returned by an `ICultureDoctrineConfigProvider` substitute (shape: `CultureMoraleAndAggressionTests.cs:149-155`): `IsEnabled` false, `IsDebug` false, `IsMoraleEnabled` false, `IsAggressionEnabled` false (today's fallbacks, see the mismatch note) | same catalog: `EnableCultureDoctrine=true` -> `IsEnabled`, `IsMoraleEnabled`, `IsAggressionEnabled` all true; `EnableCultureDoctrine=true` and `CultureDoctrineMorale=false` -> `IsMoraleEnabled` false; `EnableCultureDoctrine=true` and `CultureDoctrineAggression=false` -> `IsAggressionEnabled` false; `CultureDoctrineDebug=true` -> `IsDebug` true; and with a catalog built `enabled: false`, `EnableCultureDoctrine=true` -> `IsEnabled` false |
| `SiegePropDiagnostics/SiegePropDiagnosticsSettingsProviderTests.cs` (new) | `IsEnabled` false, `IsVerbose` false | `EnableSiegePropDiagnostics=true` -> `IsEnabled` true; `SiegePropDiagnosticsVerbose=true` -> `IsVerbose` true |

Each new file uses the namespace `TAOM.Tests.Features.<Folder>` and needs `Microsoft.VisualStudio.TestTools.UnitTesting`,
`TAOM.Features` (for `TaomSettings`) and its provider's namespace. Also: MixedFormations needs
`TAOM.Features.MixedFormations.Models` (`FormationLayoutType`); DreadAura needs
`TAOM.Features.DreadAura.Domain` (`DreadAuraConfig`, whose `Profile` is a settable `DreadProfileConfig`)
and `NSubstitute`; CultureDoctrine needs `TAOM.Features.CultureDoctrine.Domain` (`DoctrineCatalog`,
`Doctrine`) and `NSubstitute`.

Run the seven class filters.

**Verify 10b**: the test project FAILS TO BUILD, and the errors include a constructor-arity diagnostic
(such as CS1729) on the new internal-constructor call of each of the seven providers. Any other compile
error (a missing `using`, a mistyped name) is yours to fix before moving on; once fixed, only the arity
errors remain.

### Step 11: GREEN, cache the seven providers

For each of the seven files apply the Step 5 shape:

- add `private TaomSettings? _settings;` and `private TaomSettings? Settings => _settings ??= TaomSettings.Instance;`
  with a one-line comment naming the hot reader (for example
  `// Read every frame by MixedFormationsMissionBehavior and per unit by Patch30: cached on the first non-null read, read through (BattleBalanceSettingsProvider pattern).`);
- parameterless providers (MixedFormations, CompanionTactics, Howdah, SmartCavalryAI, SiegeProp): add
  `public <Name>() { }` and `internal <Name>(TaomSettings settings) => _settings = settings;`;
- providers with a constructor parameter (DreadAura, CultureDoctrine): keep the public constructor and add
  `internal <Name>(<ConfigProviderInterface> config, TaomSettings settings) : this(config) => _settings = settings;`;
- replace every `TaomSettings.Instance` in the getters with `Settings`. Nothing else changes (fallbacks,
  clamps, `ResolveLayout`, the `_config.GetCatalog().Enabled` term, the existing class comments).

**Verify**: build exits 0; `HotPathSettingsProvidersTests` (18 data rows across the two tests) and the
seven provider test classes pass with 0 failed;
`git grep -n -F "Instance?." -- <the seven provider files> Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs Main/Features/BlowDiagnostics/BlowDiagnosticsSettingsProvider.cs`
prints nothing (every getter-side `Instance?.` read is gone; several of these files mention
`TaomSettings.Instance` in comments, so do not count plain `Instance` hits), and
`git grep -c -F "_settings ??= " -- <the same nine files>` prints `:1` for each file (the accessor).

### Step 12: stage 2 docs, full suite, commit

Doc edits (find each by the text quoted in "Doc lines this plan updates"):

- `docs/features/companion-tactics.md`, the `CompanionTacticsSettingsProvider.cs` table row:
  `Reads \`TaomSettings.Instance\` directly (no reflection)` becomes
  `Caches \`TaomSettings.Instance\` on its first non-null read and reads through it, so MCM edits apply live (no reflection)`;
- the same file, the `` `ICompanionTacticsSettingsProvider` `` bullet:
  `typed read from \`TaomSettings.Instance\` (testable seam)` becomes
  `typed read through a cached \`TaomSettings.Instance\` (testable seam)`; since you rewrite that line,
  also replace its em dash after the backticked interface name with a colon (house prose rule);
- `docs/modding/file-catalogue.md`, the `dread_aura/dread_aura_config.json` row: replace
  `` (`DreadAuraSettingsProvider.cs:33-36`) `` with
  `` (`DreadAuraSettingsProvider.cs`: `IsEnabled`, `Radius`, `MoralePerSecond`) ``.

Run the full suite and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`; lint exits 0;
`git grep -n -F "DreadAuraSettingsProvider.cs:33-36" -- docs` prints nothing. Before staging,
`git status --porcelain` lists exactly these seventeen paths (the six new test files as `??`); stage
each by name:

```
Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs
Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs
Main/Features/DreadAura/DreadAuraSettingsProvider.cs
Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs
Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs
Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs
Main/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProvider.cs
TAOM.Tests/Features/HotPathSettingsProvidersTests.cs
TAOM.Tests/Features/MixedFormations/MixedFormationsSettingsProviderTests.cs
TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs
TAOM.Tests/Features/DreadAura/DreadAuraSettingsProviderTests.cs
TAOM.Tests/Features/Elephant/HowdahDiagnosticsSettingsProviderTests.cs
TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs
TAOM.Tests/Features/CultureDoctrine/CultureDoctrineSettingsProviderTests.cs
TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProviderTests.cs
docs/features/companion-tactics.md
docs/modding/file-catalogue.md
```

If `git status` shows a path not in the list, or misses one, STOP. Subject draft:
`perf(battle): <version> - cache MCM settings in per-frame providers`. Body draft:

```
Seven settings providers that battles read every frame, per howdah
seat or per agent stat update (Mixed Formations, Companion Tactics,
Dread Aura, howdah diagnostics, Smart Cavalry AI, Culture Doctrine and
siege prop diagnostics) resolved MCM's settings object on every read.
They now cache it on the first non-null read and read through it, as
the combat providers do, so MCM changes still apply mid-battle and
every default is unchanged.

Not-tested: in game; flip Mixed Formations and Smart Cavalry AI in MCM
during a field battle and check each follows the toggle.
```

`git status --porcelain` is empty after the commit.

### Step 13: RED, the hotkey parses only when the setting changes

Create `TAOM.Tests/Features/MixedFormations/CachedEnumParseTests.cs` (namespace
`TAOM.Tests.Features.MixedFormations`) against the API Step 14 creates
(`TAOM.Features.MixedFormations.CachedEnumParse<TEnum>` with `bool TryGet(string? raw, out TEnum value)`, a
public parameterless constructor using `Enum.TryParse(trimmed, ignoreCase: true, out value)`, and an
internal constructor taking `CachedEnumParse<TEnum>.Parser`, a delegate `bool Parser(string text, out TEnum value)`).
Use `System.DayOfWeek` and a counting parser
(`(string s, out DayOfWeek d) => { calls++; return Enum.TryParse(s, true, out d); }`):

- `TryGet_SameString_ParsesOnce`: three calls with `"Monday"`: all true, `DayOfWeek.Monday`, `calls == 1`.
- `TryGet_EqualStringNewInstance_ParsesOnce`: `"Monday"` then `new string("Monday".ToCharArray())`: `calls == 1`.
- `TryGet_ChangedString_ParsesAgain`: `"Monday"` then `"Friday"`: second returns Friday, `calls == 2`.
- `TryGet_NullEmptyOrWhitespace_FalseWithoutParsing`: `null`, `""`, `"   "`: each false, `calls == 0`.
- `TryGet_UnknownName_FalseAndCachesTheFailure`: `"Blursday"` twice: false both times, `calls == 1`.
- `TryGet_PaddedMixedCase_MatchesTrimmedIgnoreCaseParse` (default constructor): `" friday "` -> true, Friday.
- `TryGet_NumericString_KeepsEnumTryParseSemantics` (default constructor): `"3"` -> true, `DayOfWeek.Wednesday`
  (exactly what the old `Enum.TryParse` call returns; this pins parity, not a preference).
- `TryGet_ResultEqualsTheOldTrimThenTryParse` (default constructor): for each of `null, "", " ", "L",
  " friday ", "FRIDAY", "3", "Monday, Friday", "x"`, compare with the old logic inlined in the test
  (`var t = raw?.Trim(); expected = !string.IsNullOrEmpty(t) && Enum.TryParse(t, true, out DayOfWeek v)`),
  both the bool and, when true, the value.

Run the `CachedEnumParseTests` filter.

**Verify**: the build FAILS with a type-not-found diagnostic for `CachedEnumParse` (CS0246 or CS0234), and
no other error.

### Step 14: GREEN, `CachedEnumParse<TEnum>` and the behaviour

Create `Main/Features/MixedFormations/CachedEnumParse.cs`. Target shape (a draft: keep the semantics, the
naming may follow house style):

```csharp
using System;

namespace TAOM.Features.MixedFormations;

/// <summary>
/// A setting string parsed into an enum once per distinct value instead of on every read. Returns what
/// trimming and calling <c>Enum.TryParse(ignoreCase: true)</c> returns: false for null, empty or
/// whitespace, and whatever Enum.TryParse decides otherwise (it also accepts numbers and comma lists).
/// MCM returns the same string until the player edits it, so the per-frame cost is one string compare.
/// </summary>
public sealed class CachedEnumParse<TEnum> where TEnum : struct, Enum
{
    public delegate bool Parser(string text, out TEnum value);

    private readonly Parser _parse;
    private bool _hasResult;
    private string? _lastRaw;
    private bool _lastOk;
    private TEnum _lastValue;

    public CachedEnumParse() : this(DefaultParse) { }
    internal CachedEnumParse(Parser parse) => _parse = parse;

    public bool TryGet(string? raw, out TEnum value)
    {
        if (!_hasResult || !string.Equals(raw, _lastRaw, StringComparison.Ordinal))
        {
            var trimmed = raw?.Trim();
            _lastOk = !string.IsNullOrEmpty(trimmed) && _parse(trimmed!, out _lastValue);
            if (!_lastOk) _lastValue = default;
            _lastRaw = raw;
            _hasResult = true;
        }

        value = _lastValue;
        return _lastOk;
    }

    private static bool DefaultParse(string text, out TEnum value) => Enum.TryParse(text, ignoreCase: true, out value);
}
```

In `MixedFormationsMissionBehavior.cs` add the field
`private readonly CachedEnumParse<InputKey> _cycleKey = new();` next to `_wasCycleKeyDown`, and replace the
body of `TryResolveCycleKey` (lines 124-133) with
`private bool TryResolveCycleKey(out InputKey key) => _cycleKey.TryGet(_settings.CycleHotkey, out key);`.
Change nothing else in the file (keep `using System;`: `Array.Empty` needs it). The behaviour is
constructed per mission, so the cache lives one mission.

**Verify**: build exits 0; `CachedEnumParseTests` passes (8 tests, 0 failed);
`git grep -n "Enum.TryParse" -- Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs` prints
nothing; `wc -l Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs` prints a number below
150.

### Step 15: RED, Patch35 checks the team before the setting

In `TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs` add (this class is the existing
source-content gate for this exact file; `Formation` cannot be constructed in a unit test):

```csharp
    // Formation.SetMovementOrder runs for every team's formations, mostly on the async AI tick. The team
    // filter is two field reads; checking it first skips the settings read for every enemy order.
    [TestMethod]
    public void Patch35_FiltersThePlayerTeam_BeforeReadingTheSetting()
    {
        var src = RepoPaths.ReadSource(
            "Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs",
            stripComments: true);
        int team = src.IndexOf("Mission.Current?.PlayerTeam", System.StringComparison.Ordinal);
        int setting = src.IndexOf(".CancelStanceOnMove", System.StringComparison.Ordinal);
        Assert.IsTrue(team >= 0 && setting >= 0, "both checks are still present");
        Assert.IsTrue(team < setting, "the player-team filter runs before the CancelStanceOnMove read");
    }
```

Run the `SharedMovementOrderPostfixTests` filter.

**Verify**: the new test FAILS on "the player-team filter runs before the CancelStanceOnMove read"; the
class's other tests pass.

### Step 16: GREEN, reorder Patch35

Inside the `try` of `Postfix`, the order becomes: `if (__instance == null) return;`, then the existing
team-filter comment block and `if (__instance.Team != Mission.Current?.PlayerTeam) return;`, then
`_settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();` and
`if (_settings == null || !_settings.CancelStanceOnMove) return;`, then the `_stances` lines unchanged. Add
one line to the team-filter comment: the filter now runs first because it is the cheaper check and every
operand is a side-effect-free read. Change nothing else (the catch block, the attributes, the fields).

**Verify**: `SharedMovementOrderPostfixTests` passes, 0 failed.

### Step 17: RED, the override probe allocates nothing

In `TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs` (keep every
existing test; `Setup` already clears the overrides; the file's only `using`s today are
`Microsoft.VisualStudio.TestTools.UnitTesting` and `TAOM.Features.LocalizationOverride.Hooks`, so add
`System` (for `ArgumentNullException`), `System.Linq` and `TAOM.Tests.Migration` (for `IlCallScanner`)) add:

- `Prefix_NeverCallsSubstring` (the RED): scan
  `typeof(MBTextManager_GetLocalizedText_Patch).GetMethod("Prefix")` with
  `IlCallScanner.ExtractCalledMethods(prefix, prefix.GetMethodBody().GetILAsByteArray())` and assert none
  is `m.DeclaringType == typeof(string) && m.Name == "Substring"`.
- Pins, green before and after:
  `Prefix_TextIdIsAPrefixOfARegisteredId_FallsThrough` (register `"abcd"`; `"{=abc}x"` returns true),
  `Prefix_RegisteredIdIsAPrefixOfTheTextId_FallsThrough` (register `"abc"`; `"{=abcd}x"` returns true),
  `Prefix_IdDiffersOnlyInCase_FallsThrough` (register `"AbC"`; `"{=abc}x"` returns true),
  `Prefix_ManyRegisteredIds_EachTextGetsItsOwnOverride` (register `"id0"` to `"id499"` with text
  `"text" + i`; each `"{=id" + i + "}tail"` returns false with its own text),
  `Prefix_EmptyIdRegistered_MatchesTheEmptyIdText` (register `""` as `"E"`; `"{=}tail"` returns false,
  result `"E"`),
  `RegisterOverride_NullId_ThrowsArgumentNullException` (`[ExpectedException(typeof(ArgumentNullException))]`
  or `Assert.ThrowsException`, whichever the file's MSTest version supports).

Run the `MBTextManager_GetLocalizedText_PatchTests` filter.

**Verify**: only `Prefix_NeverCallsSubstring` fails (it finds `System.String.Substring`); the six pins and
the 11 existing tests pass.

### Step 18: GREEN, probe by slice

In `MBTextManager_GetLocalizedText_Patch.cs` key the dictionary by a slice of the source string. Add
`using System;` above `using System.Collections.Generic;` (for `ArgumentNullException`). Keep the
namespace line, the `<summary>` block and both attributes exactly as they are. The class body becomes
this target shape (a draft: keep the semantics and the member ORDER; naming may follow house style):

```csharp
public static class MBTextManager_GetLocalizedText_Patch
{
    // Keyed by a (string, start, length) slice so the per-call probe reads the id in place instead of
    // allocating it: this prefix runs on every localized text resolve. Ordinal, like the string key it
    // replaces. Written only at module load (SubModule) and in tests.
    private static readonly Dictionary<IdSlice, string> _overrides = new(IdSliceComparer.Instance);

    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(MBTextManager), "GetLocalizedText");

    [HarmonyPrefix]
    public static bool Prefix(string text, ref string __result)
    {
        if (text == null || text.Length <= 2 || text[0] != '{' || text[1] != '=')
            return true;

        int end = text.IndexOf('}', 2);
        if (end < 0)
            return true;

        int idLength = end - 2;
        if (idLength == 1 && (text[2] == '!' || text[2] == '*'))
            return true;

        if (_overrides.TryGetValue(new IdSlice(text, 2, idLength), out string overrideText))
        {
            __result = overrideText;
            return false;
        }

        return true;
    }

    public static void RegisterOverride(string id, string text)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        _overrides[new IdSlice(id, 0, id.Length)] = text;
    }

    public static void ClearOverrides()
    {
        _overrides.Clear();
    }

    // These two types stay BELOW Prefix: CoopVetoClassificationTests names a bool Prefix's owner after
    // the last class declared above it in the file, so a nested class placed above Prefix takes it over.
    private readonly struct IdSlice
    {
        public readonly string Source;
        public readonly int Start;
        public readonly int Length;
        public IdSlice(string source, int start, int length) { Source = source; Start = start; Length = length; }
    }

    private sealed class IdSliceComparer : IEqualityComparer<IdSlice>
    {
        public static readonly IdSliceComparer Instance = new();

        public bool Equals(IdSlice a, IdSlice b) =>
            a.Length == b.Length && string.CompareOrdinal(a.Source, a.Start, b.Source, b.Start, a.Length) == 0;

        public int GetHashCode(IdSlice s)
        {
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = s.Start, end = s.Start + s.Length; i < end; i++)
                    hash = (hash ^ s.Source[i]) * 16777619;
                return hash;
            }
        }
    }
}
```

What changed against today: the `_overrides` declaration and its comment, the `Substring` line removed
and the lookup keyed by `new IdSlice(text, 2, idLength)`, the null check in `RegisterOverride` (it keeps
today's `ArgumentNullException`, which `Dictionary` threw for a null key), and the two nested types at the
END of the class. Do not move them above `Prefix`: the co-op veto scan (Current state, "A source scan
constrains where a nested type may go") would then name `IdSliceComparer` as the prefix's owner and fail
two tests. The whole file comes to about 94 lines, under the 150-line limit.

**Verify**:
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MBTextManager_GetLocalizedText_PatchTests"`
  passes, 0 failed, 18 tests run.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CoopVetoClassificationTests"`
  passes, 0 failed, a non-zero `Passed`. If `EveryBoolPrefix_HasACoopDisposition` names `IdSliceComparer`
  or `Registry_HasNoStaleEntries` names `MBTextManager_GetLocalizedText_Patch`, a nested class sits above
  `Prefix`: move it below `ClearOverrides`. Never edit the test's `Registry`.
- `git grep -n "Substring" -- Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs`
  prints nothing.

### Step 19: stage 3 docs, full suite, commit

Doc edits (find each by the text quoted in "Doc lines this plan updates"):

- `docs/features/mixed-formations.md`: replace `every frame polls \`Input.IsKeyDown\` for the cycle
  hotkey. No allocations in the hot path.` with `every frame polls \`Input.IsKeyDown\` for the cycle hotkey,
  whose setting string is parsed only when it changes (\`CachedEnumParse\`). No allocations in the hot path.`
- `docs/features/localization-override.md`: after "looks up the ID in the static override dictionary",
  add ` (keyed by a slice of the input, so the lookup allocates nothing)`; in the
  `MBTextManager_GetLocalizedText_PatchTests.cs` test bullet, `**11 tests**` becomes `**18 tests**`
  (11 plus Step 17's seven).
- `docs/features/companion-tactics.md`, the `` - `SharedMovementOrderPostfixTests.cs` `` bullet: `5 tests`
  becomes `6 tests` (Step 15 added one). Change only the number.

Before writing each new count, confirm it: `grep -c "\[TestMethod\]"` on the two test files prints 18 and
6. A different number means a test was added or lost; find out which before you write the doc.

Run the full suite and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`; lint exits 0. Before staging,
`git status --porcelain` lists exactly these ten paths (`CachedEnumParse.cs` and its test file as `??`);
stage each by name:

```
Main/Features/MixedFormations/CachedEnumParse.cs
Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs
Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs
Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs
TAOM.Tests/Features/MixedFormations/CachedEnumParseTests.cs
TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs
TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs
docs/features/mixed-formations.md
docs/features/localization-override.md
docs/features/companion-tactics.md
```

If `git status` shows a path not in the list, or misses one, STOP. Subject draft:
`perf(battle): <version> - parse hotkey on change, probe loc ids in place`. Body draft:

```
Mixed Formations parsed its cycle hotkey setting with Enum.TryParse
every frame of a field battle; it now parses only when the setting
string changes, with the same result for every input. The English
string override checked every localized text by cutting its {=ID} out
into a new string; it now looks the id up in place, so resolving text
no longer allocates. The stance-cancel patch on formation orders now
checks the player's team before reading its setting, which skips the
setting read for every enemy order. Behaviour is unchanged.

Not-tested: in game; rebind the Mixed Formations cycle key in MCM
mid-battle and press it; open a screen with overridden English text
(an alliance notification) and check the override still shows.
```

`git status --porcelain` is empty after the commit.

### Step 20: final checks

Run every Done criterion below and paste each output into your report.

## Test plan

- New IL rule (`HotPathSettingsProvidersTests.OnlyTheLazySettingsAccessor_ReadsTheMcmInstance`, nine data
  rows): no method or constructor of a hot provider resolves MCM's `Instance` except the private lazy
  `Settings` accessor, which does. RED against every old provider (no accessor).
- New DryIoc resolve rows (nine): the internal test constructor leaves one public constructor.
- Per provider: no-MCM default pins (characterization, green before and after) and a live-edit read-through
  test on a cached settings object (RED by compile, then GREEN).
- Seven gate-order tests (NSubstitute `DidNotReceive` on the toggle), RED against the old order.
- `CachedEnumParseTests` (8): parse once per distinct string, re-parse on change, parity with the old
  trim-then-`Enum.TryParse` for null, empty, whitespace, padded, case, numeric and comma inputs.
- `Patch35_FiltersThePlayerTeam_BeforeReadingTheSetting`: source order, RED against the old order.
- Localization: `Prefix_NeverCallsSubstring` (RED) plus six pins (prefix ids, case, 500 ids, empty id,
  null id). The existing source gate `CoopVetoClassificationTests` is re-run after Step 18 to prove the
  nested types did not take over the prefix's co-op owner.
- Patterns to model: `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs` (providers),
  `TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs:105` (`_ = sub.DidNotReceive().Prop;`),
  `TAOM.Tests/Features/LotrIssues/LotrIssueTemplateInvariantsTests.cs:23-27` (`[DataTestMethod]` with
  `[DataRow(typeof(...))]`).
- Not testable in a unit test (name them in the `Not-tested:` trailers): the live MCM menu round trip, the
  `MixedFormationsMissionBehavior` wiring line, the Patch35 Harmony invocation, the Harmony-applied
  localization prefix in game.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows `Failed: 1` (only
      `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, and `Passed` equal to 12345 plus the
      number of new test results, all of which pass
- [ ] With `P` = `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs Main/Features/BlowDiagnostics/BlowDiagnosticsSettingsProvider.cs Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs Main/Features/DreadAura/DreadAuraSettingsProvider.cs Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs Main/Features/SiegePropDiagnostics/SiegePropDiagnosticsSettingsProvider.cs`:
      `git grep -n -F "Instance?." -- $P` prints nothing, and `git grep -c -F "_settings ??= " -- $P`
      prints `:1` for each of the nine files (the IL rule in `HotPathSettingsProvidersTests` is the
      authoritative check; these greps are the quick floor)
- [ ] `git grep -n "Substring" -- Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs` prints nothing
- [ ] `git grep -n "Enum.TryParse" -- Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs` prints nothing
- [ ] `git grep -n -e "CombatMechanicsSettingsProvider:33" -e "CombatMechanicsSettingsProvider.cs:19-35" -e "DreadAuraSettingsProvider.cs:33-36" -- docs`
      prints nothing
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CoopVetoClassificationTests"`
      passes, 0 failed (the localization prefix keeps its co-op owner), and `git diff --stat dffdf879..HEAD --
      TAOM.Tests/Features/CoopInterop` prints nothing
- [ ] `git diff --stat dffdf879..HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj Main/Features/TaomSettings.cs Main/Features/BlowDiagnostics/BlowDiagnosticsSettings.cs ':(glob)Main/**/*IoC.cs'` prints nothing
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git log --oneline <base>..HEAD`, where `<base>` is the commit your dispatch names as your starting
      point (your branch starts after `dffdf879`, at the commit that added the plans, so do not use
      `dffdf879` here), shows exactly three commits, each subject at most 72 characters with the
      SubModule.xml version; `git status --porcelain` lists nothing
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (say so in the report, naming any you changed)

## STOP conditions

Stop and report (do not improvise) if:

- The code at a "Current state" location does not match its excerpt (other than plan 030's deletion of
  `Patch35_Mission_OnTick.cs`).
- A step's verification fails twice after a reasonable fix.
- Any pre-existing test fails after a change. In particular a combat-service test failing after Step 7, or
  a provider pin failing after Step 5 or 11, means a result changed: that is forbidden here.
- A change would alter a combat result, a default, a fallback or a clamp, or would need an MCM default
  change (including the CultureDoctrine fallback mismatch).
- The fix seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any `*IoC.cs` lifetime or
  registration change, or a protected file.
- A provider turns out to be registered with a lifetime other than `Reuse.Singleton`, or is constructed per
  call somewhere (`git grep -n "new <Provider>("` in `Main/`): the cache would then buy nothing and the
  plan's premise is wrong for that provider.
- You find code that REPLACES MCM's registered settings instance (rather than copying values into it), for
  example a TAOM call that registers a new `TaomSettings`: the read-through contract would then serve a
  stale object. Report the location.
- `CoopVetoClassificationTests` still fails after Step 18 with both nested types below `ClearOverrides`:
  the scan behaves differently from "Current state"; report its message, do not touch its `Registry`.
- A TaleWorlds signature differs from "Current state" (`Formation.Team`, `Mission.PlayerTeam`,
  `MBTextManager.GetLocalizedText`); report the mismatch, do not decompile and improvise.
- The assumption "every reordered operand is free of side effects" is false for a getter you touch (for
  example a provider getter that logs or counts): leave that gate as it is and report it.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (the brief names none) and give the executor its number for the report.
- `/localize`: none; this plan adds no player-facing text.
- Feature docs: the doc edits in Steps 8, 12 and 19 are in scope; no new feature doc or feature-map row.
- Review: `/deep-review` on the three commits before any merge, with the probes below.

## After merge: the maintainer's actions

Pull. In game, once: change a Combat Mechanics toggle, the Mixed Formations cycle key and Smart Cavalry AI
in MCM during a field battle and confirm each takes effect without a restart (the `Not-tested:` lines).

## Maintenance notes

- A new settings provider read per frame or per blow should start in this shape and be added as a row to
  both `HotPathSettingsProvidersTests` data tests.
- The read-through contract rests on MCM editing its registered instance in place. An MCM upgrade that
  swaps instances on reset or preset load would freeze every cached provider (twelve with this plan);
  the live-edit tests cannot catch that because they build their own settings objects. Re-check
  `SettingsUtils.OverrideValues` and the containers' `LoadedSettings` after an MCM bump.
- While MCM is not up (or never loads), the lazy accessor resolves on every read exactly as today, so
  there is no regression and no gain in that state.
- Review probes: the `??=` accessors on worker threads (idempotent, no lock needed; confirm nothing else
  in a provider is mutable); that `ChargeAutoKnockdownWeightRatio` still derives its floor from the live
  neutral value; the `IdSlice` comparer (equal slices hash equally; length checked before
  `CompareOrdinal`) and its placement below `Prefix` (the co-op veto scan attributes a bool `Prefix` to
  the last `class` declared above it); that `CachedEnumParse` returns `default` on every false result.
- Considered and rejected under `simplicity-criterion.md`:
  - **Reuse one `FormationAdapter` per formation in `BattleActionBarMissionView.RefreshFromSelectedFormation`
    (lines 123-125).** The refresh runs at most every 0.5 s (`RefreshIntervalSec`), and the adapter's
    composition TTL is 500 ms (`FormationAdapter.cs:83`), so a reused adapter would rescan on every refresh
    anyway; within one refresh a fresh adapter already scans once and serves the second count from its
    cache (`FormationCompositionAnalyzer.Analyze` reads `PolearmUnitCount` then `ShieldUnitCount`). The win
    is one small allocation per 0.5 s, and the TTL compares `Environment.TickCount` as a float, whose
    rounding at large uptimes could let a reused adapter serve a composition one refresh stale. Rejected.
  - **Replace `OOBOverlayService`'s per-frame `FieldInfo.GetValue` (line 82) with a field-ref delegate.**
    The `FieldInfo`s are already cached; what remains is one reflective read and one box per frame while
    the order-of-battle handler ticks. Tiny win; rejected.
- Deferred, not this plan: the CultureDoctrine fallback mismatch (`?? false` against compiled `true` for
  Morale and Aggression), which is a default question for the maintainer.
