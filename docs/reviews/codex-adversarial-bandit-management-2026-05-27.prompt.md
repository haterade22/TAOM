# Codex Adversarial Review — Bandit Management (2026-05-27)

You are reviewing a new TAOM feature `BanditManagement` that adds LOTR bandit culture replacement + PlayerProgress-scaled hideout density + party size scaling. The Claude `/deep-review` already passed 4 of 5 axes + found 1 data-flow gap (now fixed). Your job is to find what Claude missed.

## Project context

- Target: Bannerlord 1.4.5 (signatures verified via `pwsh tools/taom-src.ps1 path <Type>` which `ilspycmd`s the installed DLLs)
- .NET Framework 4.7.2 (`System.MathF` doesn't exist; use `TaleWorlds.Library.MathF`)
- TAOM has a pre-existing `TaomPartySizeModel : DefaultPartySizeLimitModel` (CulturalFeats feature) which overrides `GetPartyMemberSizeLimit` only — coexistence is intentional.

## What changed (full file list)

C# (new):
- `Main/Features/BanditManagement/BanditScalingConfig.cs` — POCO, 6 fields
- `Main/Features/BanditManagement/IBanditScalingConfigProvider.cs`
- `Main/Features/BanditManagement/BanditScalingConfigProvider.cs` — JSON loader with `FiniteFloatValidator` + range checks
- `Main/Features/BanditManagement/IBanditScalingSettingsProvider.cs`
- `Main/Features/BanditManagement/BanditScalingSettingsProvider.cs` — reads `TaomSettings.Instance` with NaN-safe clamp, falls back to JSON defaults
- `Main/Features/BanditManagement/IBanditScalingService.cs`
- `Main/Features/BanditManagement/BanditScalingService.cs` — pure math, no TaleWorlds deps
- `Main/Features/BanditManagement/BanditManagementIoC.cs` — 3 Reuse.Singleton registrations
- `Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs` — overrides 5 properties on `DefaultBanditDensityModel`
- `Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs` — `HarmonyPostfix` on `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)`

C# (modified):
- `Main/Features/TaomSettings.cs` — added 6 MCM properties under `GroupOrder = 35` (World/Bandit Scaling): `EnableBanditScaling`, `BanditDensityCurve`, `BanditPartySizeCurve`, `BanditBossFightCurve`, `BanditMaxHideoutsPerFaction`, `BanditMaxPartiesPerHideout`
- `Main/IoC.cs` — added `BanditManagementIoC.RegisterBanditManagementFeature(container)` call
- `Main/SubModule.cs` — added `campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()))`

XML data:
- `Main/_Module/ModuleData/taom_spcultures.xml` — 5 new `is_bandit="true"` cultures appended (dunland_raiders, rhun_raiders, harad_raiders, gundabad_raiders, umbar_corsairs)
- `Main/_Module/ModuleData/taom_partyTemplates.xml` — 10 new templates (5 raider + 5 boss)
- `Main/_Module/ModuleData/taom_module_strings.xml` — ~80 new loc keys
- `Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json` — defaults

External (TAOM_Map module, modified via `tools/migrate_hideouts_to_lotr.py --apply --backup`):
- `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` — 99 hideouts had `culture=` swapped to LOTR cultures + `name=` rewritten
- 12 `<game>/Modules/TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml` — 99 strings updated each
- `.bak` backups saved next to each

Tests (new):
- `TAOM.Tests/Features/BanditManagement/BanditScalingServiceTests.cs` — 16 tests
- `TAOM.Tests/Features/BanditManagement/BanditScalingConfigProviderTests.cs` — 15 tests

Total tests pass: 2551/2553 (2 preexisting skips, zero regressions).

## Known Suspects — verify each

1. **Patch39 parameter binding.** Harmony binds postfix params by NAME. Vanilla v1.4.5's signature is `FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)`. Our postfix declares `Postfix(ref TroopRoster __result, MobileParty party, PartyTemplateObject partyTemplate)`. Verify the names match exactly. If they don't, the postfix silently no-ops.

2. **Patch39 race condition.** `private static IBanditScalingService _service` with lazy `??=` resolve. If two threads enter `GetService()` simultaneously, both could race. This is in a Harmony callback which can be invoked from any thread. Is it actually safe?

3. **Patch39 TroopRoster mutation safety.** Vanilla just built the TroopRoster and is about to return it. We mutate it via `AddToCounts` AFTER vanilla finished building. Is there any vanilla code path that reads from the roster between the original `return` and our mutation? Any chance a vanilla `Debug.FailedAssert` fires because we exceed an assumed cap?

4. **PartyTemplateStack uniqueness.** Our postfix loops `partyTemplate.Stacks` and calls `GetTroopCount(stack.Character)` then `AddToCounts(stack.Character, delta)`. What happens if two stacks share the same `Character`? `GetTroopCount` returns the merged count; `AddToCounts` adds delta. So we'd over-scale: stack-1's contribution gets read AND scaled, then stack-2's contribution gets read (which now includes stack-1's scaling) AND scaled. Bug? Or is `partyTemplate.Stacks` guaranteed distinct by Character?

5. **TaomBanditDensityModel.GetPlayerProgress race with campaign teardown.** `Campaign.Current?.PlayerProgress ?? 0f` — what if `Campaign.Current` goes null between the null check and the property read? (Unlikely, but `Campaign.Current` is mutated on session start/end.)

6. **TaomBanditDensityModel registered too early.** `SubModule.cs` adds the model via `campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()))`. If `IoC.Resolve<IBanditScalingService>()` throws (DryIoc not configured yet, dep missing), the whole AddModel sequence dies. Should this be guarded?

7. **MinPartiesToInfest invariant at runtime.** `BanditScalingSettingsProvider.MinPartiesToInfest` clamps to `[1, MaxPartiesPerHideoutCap]`. But `MaxPartiesPerHideoutCap` is read live from MCM. What if MCM user lowers `BanditMaxPartiesPerHideout` to 1? Then `MinPartiesToInfest` clamps to 1. What if user lowers it to 0? MCM range is `[1, 20]` so that shouldn't happen. Verify edge case at 1.

8. **JSON config field rename risk.** `bandit_scaling_config.json` uses PascalCase field names (`DensityCurve`, `MaxHideoutsPerFactionCap`). Newtonsoft's default casing is case-insensitive — should work. But `BanditScalingConfigProviderTests.GetConfig_DensityCurveOutOfRange_RevertsToDefault` writes `{ "DensityCurve": 99.0 }` and expects the warning text to contain `densityCurve=99` (camelCase). Does the warning log actually emit camelCase? If our LogWarning string says "densityCurve=" but `BanditScalingConfig.DensityCurve` is PascalCase, the test passes because we just hardcoded "densityCurve" in the string template — but it's inconsistent. Cosmetic, not a bug.

9. **5 new bandit cultures — clan creation race.** New `is_bandit="true"` cultures are loaded from `taom_spcultures.xml`. Vanilla SandBoxCore code creates bandit *clans* from these cultures during campaign init. Verify: does v1.4.5 SandBoxCore iterate cultures with `is_bandit="true"` and auto-create one bandit clan per culture? Or is there a hardcoded list somewhere that needs to be patched? If the clan never spawns, hideouts will have a non-existent `MapFaction` and crash.

10. **Hideout migration script regex precision.** `tools/migrate_hideouts_to_lotr.py` uses regex `HIDEOUT_LINE_RE` that requires `type="Hideout"` between the `id` and `culture=`. Verify the regex doesn't accidentally match: (a) settlements with `id="hideout_X"` that aren't actually Hideouts; (b) hideouts with attributes in unusual order; (c) lines split across multiple physical lines.

11. **TAOM_Map hideout IDs preserved across migration.** Memory says hideout IDs were intentionally NOT renamed for save-compat. Verify: did the script rename any IDs? Or only `culture=` + `name=`?

12. **Patch39 vs. TaomPartySizeModel collision.** TAOM already has `TaomPartySizeModel : DefaultPartySizeLimitModel` overriding `GetPartyMemberSizeLimit`. Our Patch39 postfixes `FindAppropriateInitialRosterForMobileParty` on the base class. If vanilla virtual dispatch lands on `TaomPartySizeModel.FindAppropriateInitialRosterForMobileParty` (which doesn't override it, so falls through to base), Harmony patches the base method — does the patch fire? Verify the virtual dispatch chain.

## Vanilla decompile reference (for context)

```csharp
// TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel (v1.4.5, line 427)
public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
{
    TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
    float initialPartySizeRatioForMobileParty = GetInitialPartySizeRatioForMobileParty(party, partyTemplate);  // private, returns 0.4..1.2 for bandit
    for (int i = 0; i < partyTemplate.Stacks.Count; i++)
    {
        int minValue = partyTemplate.Stacks[i].MinValue;
        int maxValue = partyTemplate.Stacks[i].MaxValue;
        int num = minValue;
        if (initialPartySizeRatioForMobileParty <= 0f) num = minValue;
        else if (initialPartySizeRatioForMobileParty <= 1f) num = MBRandom.RoundRandomized(minValue + (maxValue - minValue) * initialPartySizeRatioForMobileParty);
        else { Debug.FailedAssert(...); num = maxValue; }
        // ... villager-bonus snippet ...
        if (num > 0) { /* AddToCounts(stack.Character, num, ...) */ }
    }
    return troopRoster;
}
```

```csharp
// TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel (v1.4.5, line ~14-30)
public class DefaultBanditDensityModel : BanditDensityModel
{
    public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt => 2;
    public override int NumberOfMaximumBanditPartiesInEachHideout => 3;
    public override int NumberOfMaximumHideoutsAtEachBanditFaction => 9;
    public override int NumberOfInitialHideoutsAtEachBanditFaction => 7;
    public override int NumberOfMaximumTroopCountForFirstFightInHideout => MathF.Floor(11f * (2f + Campaign.Current.PlayerProgress));
    public override int NumberOfMaximumTroopCountForBossFightInHideout => MathF.Floor(1f + 5f * (1f + Campaign.Current.PlayerProgress));
    // ... GetMaximumTroopCountForHideoutMission() etc.
}
```

## What to report

For each Known Suspect: CONFIRMED / DISPUTED with code citation. Use exact file:line where possible.

Additionally, walk the changed files cold and report any defect you find that's NOT in the Known Suspects list. Prioritise: race conditions, save-compat breaks, vanilla-engine assumption violations, missing null guards on game-state pointers, dead code paths.

End with a punch list of CONFIRMED findings ranked HIGH/MED/LOW.
