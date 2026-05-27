OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019e6b66-eaeb-77d1-ad3a-444e4c4d7527
--------
user
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

ERROR: The process "68736" not found.
ERROR: The process "73468" not found.
codex
I’ll review this as a verifier, not a fixer. I’m going to anchor each suspect against the local TAOM code and the v1.4.5 decompile where engine behavior matters.
  → Map changed files and diffs
  • Verify known suspects against TAOM and vanilla code
  • Cold-review BanditManagement files and data changes
  • Report confirmed/disputed findings with severity
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --stat' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg --files Main/Features/BanditManagement TAOM.Tests/Features/BanditManagement Main/_Module/ModuleData/bandit_management tools' in C:\Users\mikew\source\repos\TAOM
2026-05-27T21:46:59.684796Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.4 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/BanditManagement TAOM.Tests/Features/BanditM …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2372ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/BanditManagement TAOM.Tests/Features/BanditM …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 succeeded in 2686ms:
 CHANGELOG.md                                    |  42 ++++++
 Main/Features/TaomSettings.cs                   |  34 +++++
 Main/IoC.cs                                     |   2 +
 Main/SubModule.cs                               |   3 +
 Main/_Module/ModuleData/taom_module_strings.xml |  97 +++++++++++++
 Main/_Module/ModuleData/taom_partyTemplates.xml | 103 +++++++++++++
 Main/_Module/ModuleData/taom_spcultures.xml     | 183 ++++++++++++++++++++++++
 7 files changed, 464 insertions(+)
warning: in the working copy of 'CHANGELOG.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/_Module/ModuleData/taom_spcultures.xml', LF will be replaced by CRLF the next time Git touches it

 succeeded in 2954ms:
 M CHANGELOG.md
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/taom_module_strings.xml
 M Main/_Module/ModuleData/taom_partyTemplates.xml
 M Main/_Module/ModuleData/taom_spcultures.xml
?? Main/Features/BanditManagement/
?? Main/_Module/ModuleData/bandit_management/
?? TAOM.Tests/Features/BanditManagement/
?? docs/features/bandit-management.md
?? docs/reviews/codex-adversarial-bandit-management-2026-05-27.md
?? docs/reviews/codex-adversarial-bandit-management-2026-05-27.prompt.md
?? docs/reviews/rca-bandit-management-2026-05-27.md
?? tools/migrate_hideouts_to_lotr.py

codex
`rg` is not installed in this environment, so I’m switching to PowerShell-native file listing and `Select-String`. The worktree contains the BanditManagement feature plus docs and the migration script as untracked files.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main\\Features\\BanditManagement,TAOM.Tests\\Features\\BanditManagement,Main\\_Module\\ModuleData\\bandit_management,tools | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/TaomSettings.cs Main/IoC.cs Main/SubModule.cs Main/_Module/ModuleData/taom_spcultures.xml Main/_Module/ModuleData/taom_partyTemplates.xml' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File E:\\Decompiled_Bannerlord | Where-Object { "'$_.Name -in @('"'DefaultPartySizeLimitModel.cs','DefaultBanditDensityModel.cs','BanditFactionCampaignBehavior.cs','BanditManager.cs') } | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 786ms:
diff --git a/Main/Features/TaomSettings.cs b/Main/Features/TaomSettings.cs
index 8a6d0f3..34efbc7 100644
--- a/Main/Features/TaomSettings.cs
+++ b/Main/Features/TaomSettings.cs
@@ -430,6 +430,40 @@ public class TaomSettings : AttributeGlobalSettings<TaomSettings>
 
     // --- Map UI / Settlement Nameplates ---
 
+    // --- World / Bandit Scaling ---
+
+    [SettingPropertyGroup("World/Bandit Scaling", GroupOrder = 35)]
+    [SettingPropertyBool("Enable Bandit Scaling", Order = 0,
+        HintText = "Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below.")]
+    public bool EnableBanditScaling { get; set; } = true;
+
+    [SettingPropertyGroup("World/Bandit Scaling")]
+    [SettingPropertyFloatingInteger("Density Curve", 0.0f, 5.0f, "#0.0", Order = 1,
+        HintText = "Multiplier on hideout count + parties-per-hideout at PlayerProgress=1.0. Curve: 1 + curve * progress. 0 = vanilla density throughout. 1.5 (default) = up to 2.5x density in endgame.")]
+    public float BanditDensityCurve { get; set; } = 1.5f;
+
+    [SettingPropertyGroup("World/Bandit Scaling")]
+    [SettingPropertyFloatingInteger("Party Size Curve", 0.0f, 5.0f, "#0.0", Order = 2,
+        HintText = "Multiplier on bandit party troop counts at PlayerProgress=1.0. Vanilla already scales 0.4 -> 1.2; this is a final multiplier on top. 1.5 (default) = up to 2.5x bandit party sizes in endgame.")]
+    public float BanditPartySizeCurve { get; set; } = 1.5f;
+
+    [SettingPropertyGroup("World/Bandit Scaling")]
+    [SettingPropertyFloatingInteger("Boss Fight Curve", 0.0f, 5.0f, "#0.0", Order = 3,
+        HintText = "Multiplier on first-fight + boss-fight troop counts inside hideouts at PlayerProgress=1.0. 1.5 (default) = up to 2.5x bandits per hideout assault in endgame.")]
+    public float BanditBossFightCurve { get; set; } = 1.5f;
+
+    [SettingPropertyGroup("World/Bandit Scaling")]
+    [SettingPropertyInteger("Max Hideouts Per Faction Cap", 1, 100, Order = 4,
+        HintText = "Hard cap on hideouts per bandit faction regardless of scaling curve. Vanilla = 9. Default: 15.")]
+    public int BanditMaxHideoutsPerFaction { get; set; } = 15;
+
+    [SettingPropertyGroup("World/Bandit Scaling")]
+    [SettingPropertyInteger("Max Parties Per Hideout Cap", 1, 20, Order = 5,
+        HintText = "Hard cap on bandit parties per hideout regardless of scaling curve. Vanilla = 3. Default: 5.")]
+    public int BanditMaxPartiesPerHideout { get; set; } = 5;
+
+    // --- Map UI / Settlement Nameplates ---
+
     [SettingPropertyGroup("Map UI/Settlement Nameplates", GroupOrder = 40)]
     [SettingPropertyBool("Enable Settlement Nameplate Fade", Order = 0,
         HintText = "Fade settlement nameplates with camera distance. When off, all nameplates display at full visibility regardless of distance (vanilla).")]
diff --git a/Main/IoC.cs b/Main/IoC.cs
index 8d4e845..4e4ccb2 100644
--- a/Main/IoC.cs
+++ b/Main/IoC.cs
@@ -34,6 +34,7 @@ using TAOM.Features.CareerSystem;
 using TAOM.Features.SettlementGuards;
 using TAOM.Features.NamedCompanions;
 using TAOM.Features.RevoltTuning;
+using TAOM.Features.BanditManagement;
 using TAOM.Features.SiegeDismount;
 using TAOM.Features.MixedFormations;
 using TAOM.Features.SmartCavalryAI;
@@ -93,6 +94,7 @@ public static class IoC
         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
+        BanditManagementIoC.RegisterBanditManagementFeature(container);
         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
         MixedFormationsIoC.RegisterMixedFormationsFeature(container);
         SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
diff --git a/Main/SubModule.cs b/Main/SubModule.cs
index 8435df6..e517b28 100644
--- a/Main/SubModule.cs
+++ b/Main/SubModule.cs
@@ -62,6 +62,8 @@ using TAOM.Features.CareerSystem.Models;
 using TAOM.Features.SettlementGuards;
 using TAOM.Features.SettlementGuards.Hooks;
 using TAOM.Features.RevoltTuning;
+using TAOM.Features.BanditManagement;
+using TAOM.Features.BanditManagement.Models;
 using TAOM.Features.SiegeDismount.Hooks;
 using TAOM.Features.MixedFormations.Hooks;
 using TAOM.Features.SmartCavalryAI.Hooks;
@@ -353,6 +355,7 @@ public class SubModule : MBSubModuleBase
             campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
             campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
             campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
+            campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
             campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
             campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
             campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
diff --git a/Main/_Module/ModuleData/taom_partyTemplates.xml b/Main/_Module/ModuleData/taom_partyTemplates.xml
index 71af896..ec3cddb 100644
--- a/Main/_Module/ModuleData/taom_partyTemplates.xml
+++ b/Main/_Module/ModuleData/taom_partyTemplates.xml
@@ -1474,4 +1474,107 @@
 		</stacks>
 	</MBPartyTemplate>
 
+	<!-- ============================================== -->
+	<!-- LOTR Bandit Cultures - Raider + Boss Templates -->
+	<!-- ============================================== -->
+	<!-- Replaces vanilla forest/mountain/desert/steppe/sea bandit cultures with lore-appropriate
+	     LOTR factions. Each culture has two templates:
+	       {culture}_raider_party_template  -- regular bandit warbands roaming the map
+	       {culture}_boss_party_template    -- hideout boss + defenders
+	     Troop IDs pulled from each culture's existing troop XML (no new troop defs needed).
+	     Tiered T1-T4 to keep raider feel; bandit count scales further via TaomBanditDensityModel
+	     + Patch39_BanditPartySize at runtime.
+	-->
+
+	<!-- Dunland Raiders (replaces forest_bandits) -->
+	<MBPartyTemplate id="dunland_raiders_raider_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="8" max_value="18" troop="NPCCharacter.dunland_peasant" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.dunland_raider" />
+			<PartyTemplateStack min_value="3" max_value="8" troop="NPCCharacter.dunland_hunter" />
+			<PartyTemplateStack min_value="1" max_value="4" troop="NPCCharacter.dunland_clan_warrior" />
+		</stacks>
+	</MBPartyTemplate>
+	<MBPartyTemplate id="dunland_raiders_boss_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.dunland_wolf_raider" />
+			<PartyTemplateStack min_value="6" max_value="12" troop="NPCCharacter.dunland_clan_warrior" />
+			<PartyTemplateStack min_value="8" max_value="16" troop="NPCCharacter.dunland_raider" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.dunland_hunter" />
+		</stacks>
+	</MBPartyTemplate>
+
+	<!-- Rhûn Raiders (replaces steppe_bandits) -->
+	<MBPartyTemplate id="rhun_raiders_raider_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="6" max_value="14" troop="NPCCharacter.balcoth_volunteer" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.balcoth_footman" />
+			<PartyTemplateStack min_value="3" max_value="8" troop="NPCCharacter.kharaghul_rider" />
+			<PartyTemplateStack min_value="1" max_value="4" troop="NPCCharacter.balcoth_archer" />
+		</stacks>
+	</MBPartyTemplate>
+	<MBPartyTemplate id="rhun_raiders_boss_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.kharaghul_raider" />
+			<PartyTemplateStack min_value="5" max_value="10" troop="NPCCharacter.balcoth_archer" />
+			<PartyTemplateStack min_value="6" max_value="14" troop="NPCCharacter.balcoth_footman" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.kharaghul_rider" />
+		</stacks>
+	</MBPartyTemplate>
+
+	<!-- Haradrim Raiders (replaces desert_bandits) -->
+	<MBPartyTemplate id="harad_raiders_raider_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="8" max_value="18" troop="NPCCharacter.harad_levy" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.harad_skirmisher" />
+			<PartyTemplateStack min_value="3" max_value="8" troop="NPCCharacter.harad_archer" />
+			<PartyTemplateStack min_value="1" max_value="4" troop="NPCCharacter.harad_footman" />
+		</stacks>
+	</MBPartyTemplate>
+	<MBPartyTemplate id="harad_raiders_boss_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.harad_camelrider" />
+			<PartyTemplateStack min_value="5" max_value="10" troop="NPCCharacter.harad_footman" />
+			<PartyTemplateStack min_value="6" max_value="14" troop="NPCCharacter.harad_archer" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.harad_camelscout" />
+		</stacks>
+	</MBPartyTemplate>
+
+	<!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
+	<MBPartyTemplate id="gundabad_raiders_raider_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="8" max_value="18" troop="NPCCharacter.gundabad_snaga" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.gundabad_hunter" />
+			<PartyTemplateStack min_value="3" max_value="8" troop="NPCCharacter.gundabad_grunt" />
+			<PartyTemplateStack min_value="2" max_value="6" troop="NPCCharacter.gundabad_lurker" />
+		</stacks>
+	</MBPartyTemplate>
+	<MBPartyTemplate id="gundabad_raiders_boss_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.gundabad_despoiler_of_the_vale" />
+			<PartyTemplateStack min_value="5" max_value="10" troop="NPCCharacter.gundabad_scout" />
+			<PartyTemplateStack min_value="6" max_value="14" troop="NPCCharacter.gundabad_grunt" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.gundabad_lurker" />
+			<PartyTemplateStack min_value="3" max_value="8" troop="NPCCharacter.gundabad_hunter" />
+		</stacks>
+	</MBPartyTemplate>
+
+	<!-- Umbar Corsairs (replaces sea_raiders) -->
+	<MBPartyTemplate id="umbar_corsairs_raider_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="8" max_value="18" troop="NPCCharacter.aux_basic" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.umbar_elite" />
+			<PartyTemplateStack min_value="2" max_value="6" troop="NPCCharacter.umbar_elite_root1" />
+			<PartyTemplateStack min_value="1" max_value="4" troop="NPCCharacter.umbar_elite_root0" />
+		</stacks>
+	</MBPartyTemplate>
+	<MBPartyTemplate id="umbar_corsairs_boss_party_template">
+		<stacks>
+			<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.umbar_elite_root00" />
+			<PartyTemplateStack min_value="5" max_value="10" troop="NPCCharacter.umbar_elite_root0" />
+			<PartyTemplateStack min_value="6" max_value="14" troop="NPCCharacter.umbar_elite" />
+			<PartyTemplateStack min_value="4" max_value="10" troop="NPCCharacter.umbar_elite_root1" />
+		</stacks>
+	</MBPartyTemplate>
+
 </partyTemplates>
diff --git a/Main/_Module/ModuleData/taom_spcultures.xml b/Main/_Module/ModuleData/taom_spcultures.xml
index 4194aac..19a5399 100644
--- a/Main/_Module/ModuleData/taom_spcultures.xml
+++ b/Main/_Module/ModuleData/taom_spcultures.xml
@@ -4157,4 +4157,187 @@
     </basic_mercenary_troops>
   </Culture>
 
+  <!-- ============================================== -->
+  <!-- LOTR Bandit Cultures                             -->
+  <!-- ============================================== -->
+  <!-- Replace vanilla forest/mountain/desert/steppe/sea bandit cultures with lore-appropriate
+       LOTR factions. Hideouts in TAOM_Map/settlements.xml reference these cultures' StringIds.
+       Bandit clans inherit IsBanditFaction=true via is_bandit, so engine spawns them automatically.
+  -->
+
+  <!-- Dunlending Raiders (replaces forest_bandits) -->
+  <Culture
+    id="dunland_raiders"
+    name="{=taom_bandit_dunland_name}Dunlending Raiders"
+    bandit_chief="NPCCharacter.dunland_clan_warrior"
+    bandit_raider="NPCCharacter.dunland_raider"
+    bandit_bandit="NPCCharacter.dunland_peasant"
+    bandit_boss="NPCCharacter.dunland_wolf_raider"
+    elite_basic_troop="NPCCharacter.dunland_raider"
+    basic_troop="NPCCharacter.dunland_peasant"
+    is_bandit="true"
+    can_have_settlement="true"
+    encounter_background_mesh="encounter_forest_bandit"
+    bandit_boss_party_template="PartyTemplate.dunland_raiders_boss_party_template">
+    <male_names>
+      <name name="{=taom_bandit_dunland_m1}Caradoc" />
+      <name name="{=taom_bandit_dunland_m2}Gwidion" />
+      <name name="{=taom_bandit_dunland_m3}Brennor" />
+      <name name="{=taom_bandit_dunland_m4}Targal" />
+      <name name="{=taom_bandit_dunland_m5}Maedrun" />
+      <name name="{=taom_bandit_dunland_m6}Drûghu" />
+      <name name="{=taom_bandit_dunland_m7}Olfan" />
+      <name name="{=taom_bandit_dunland_m8}Cethrin" />
+      <name name="{=taom_bandit_dunland_m9}Hwalbor" />
+      <name name="{=taom_bandit_dunland_m10}Idris" />
+    </male_names>
+    <female_names>
+      <name name="{=taom_bandit_dunland_f1}Branwen" />
+      <name name="{=taom_bandit_dunland_f2}Aelis" />
+      <name name="{=taom_bandit_dunland_f3}Maerwen" />
+      <name name="{=taom_bandit_dunland_f4}Gwyneth" />
+      <name name="{=taom_bandit_dunland_f5}Olwen" />
+    </female_names>
+  </Culture>
+
+  <!-- Rhûn Raiders (replaces steppe_bandits) -->
+  <Culture
+    id="rhun_raiders"
+    name="{=taom_bandit_rhun_name}Rhûn Raiders"
+    bandit_chief="NPCCharacter.balcoth_archer"
+    bandit_raider="NPCCharacter.balcoth_footman"
+    bandit_bandit="NPCCharacter.balcoth_volunteer"
+    bandit_boss="NPCCharacter.kharaghul_raider"
+    elite_basic_troop="NPCCharacter.balcoth_footman"
+    basic_troop="NPCCharacter.balcoth_volunteer"
+    is_bandit="true"
+    can_have_settlement="true"
+    encounter_background_mesh="encounter_steppe_bandit"
+    bandit_boss_party_template="PartyTemplate.rhun_raiders_boss_party_template">
+    <male_names>
+      <name name="{=taom_bandit_rhun_m1}Khamûl" />
+      <name name="{=taom_bandit_rhun_m2}Targon" />
+      <name name="{=taom_bandit_rhun_m3}Bûrzakh" />
+      <name name="{=taom_bandit_rhun_m4}Sangar" />
+      <name name="{=taom_bandit_rhun_m5}Hazgrim" />
+      <name name="{=taom_bandit_rhun_m6}Vorzul" />
+      <name name="{=taom_bandit_rhun_m7}Daghir" />
+      <name name="{=taom_bandit_rhun_m8}Olmec" />
+      <name name="{=taom_bandit_rhun_m9}Tarvon" />
+      <name name="{=taom_bandit_rhun_m10}Kharzul" />
+    </male_names>
+    <female_names>
+      <name name="{=taom_bandit_rhun_f1}Sarila" />
+      <name name="{=taom_bandit_rhun_f2}Khazna" />
+      <name name="{=taom_bandit_rhun_f3}Vorina" />
+      <name name="{=taom_bandit_rhun_f4}Tahjia" />
+      <name name="{=taom_bandit_rhun_f5}Sangela" />
+    </female_names>
+  </Culture>
+
+  <!-- Haradrim Raiders (replaces desert_bandits) -->
+  <Culture
+    id="harad_raiders"
+    name="{=taom_bandit_harad_name}Haradrim Raiders"
+    bandit_chief="NPCCharacter.harad_footman"
+    bandit_raider="NPCCharacter.harad_archer"
+    bandit_bandit="NPCCharacter.harad_levy"
+    bandit_boss="NPCCharacter.harad_camelrider"
+    elite_basic_troop="NPCCharacter.harad_archer"
+    basic_troop="NPCCharacter.harad_levy"
+    is_bandit="true"
+    can_have_settlement="true"
+    encounter_background_mesh="encounter_desert_bandit"
+    bandit_boss_party_template="PartyTemplate.harad_raiders_boss_party_template">
+    <male_names>
+      <name name="{=taom_bandit_harad_m1}Suladân" />
+      <name name="{=taom_bandit_harad_m2}Azaghal" />
+      <name name="{=taom_bandit_harad_m3}Khaled" />
+      <name name="{=taom_bandit_harad_m4}Variag" />
+      <name name="{=taom_bandit_harad_m5}Dath" />
+      <name name="{=taom_bandit_harad_m6}Tarkil" />
+      <name name="{=taom_bandit_harad_m7}Mûzan" />
+      <name name="{=taom_bandit_harad_m8}Akhôr" />
+      <name name="{=taom_bandit_harad_m9}Sahir" />
+      <name name="{=taom_bandit_harad_m10}Zûran" />
+    </male_names>
+    <female_names>
+      <name name="{=taom_bandit_harad_f1}Nilhuna" />
+      <name name="{=taom_bandit_harad_f2}Saharia" />
+      <name name="{=taom_bandit_harad_f3}Aklira" />
+      <name name="{=taom_bandit_harad_f4}Bezara" />
+      <name name="{=taom_bandit_harad_f5}Mûzana" />
+    </female_names>
+  </Culture>
+
+  <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
+  <Culture
+    id="gundabad_raiders"
+    name="{=taom_bandit_gundabad_name}Gundabad Orc Raiders"
+    bandit_chief="NPCCharacter.gundabad_scout"
+    bandit_raider="NPCCharacter.gundabad_grunt"
+    bandit_bandit="NPCCharacter.gundabad_snaga"
+    bandit_boss="NPCCharacter.gundabad_despoiler_of_the_vale"
+    elite_basic_troop="NPCCharacter.gundabad_grunt"
+    basic_troop="NPCCharacter.gundabad_snaga"
+    is_bandit="true"
+    can_have_settlement="true"
+    encounter_background_mesh="encounter_mountain_bandit"
+    bandit_boss_party_template="PartyTemplate.gundabad_raiders_boss_party_template">
+    <male_names>
+      <name name="{=taom_bandit_gundabad_m1}Bolg" />
+      <name name="{=taom_bandit_gundabad_m2}Yazneg" />
+      <name name="{=taom_bandit_gundabad_m3}Fimbul" />
+      <name name="{=taom_bandit_gundabad_m4}Narzug" />
+      <name name="{=taom_bandit_gundabad_m5}Gorbag" />
+      <name name="{=taom_bandit_gundabad_m6}Mauhûr" />
+      <name name="{=taom_bandit_gundabad_m7}Snaga" />
+      <name name="{=taom_bandit_gundabad_m8}Ufthak" />
+      <name name="{=taom_bandit_gundabad_m9}Radbug" />
+      <name name="{=taom_bandit_gundabad_m10}Lugdush" />
+    </male_names>
+    <female_names>
+      <name name="{=taom_bandit_gundabad_f1}Skarra" />
+      <name name="{=taom_bandit_gundabad_f2}Vorga" />
+      <name name="{=taom_bandit_gundabad_f3}Mazra" />
+      <name name="{=taom_bandit_gundabad_f4}Grishta" />
+      <name name="{=taom_bandit_gundabad_f5}Narzga" />
+    </female_names>
+  </Culture>
+
+  <!-- Umbar Corsairs (replaces sea_raiders) -->
+  <Culture
+    id="umbar_corsairs"
+    name="{=taom_bandit_umbar_name}Corsairs of Umbar"
+    bandit_chief="NPCCharacter.umbar_elite_root0"
+    bandit_raider="NPCCharacter.umbar_elite"
+    bandit_bandit="NPCCharacter.aux_basic"
+    bandit_boss="NPCCharacter.umbar_elite_root00"
+    elite_basic_troop="NPCCharacter.umbar_elite"
+    basic_troop="NPCCharacter.aux_basic"
+    is_bandit="true"
+    can_have_settlement="true"
+    encounter_background_mesh="encounter_sea_raider"
+    bandit_boss_party_template="PartyTemplate.umbar_corsairs_boss_party_template">
+    <male_names>
+      <name name="{=taom_bandit_umbar_m1}Castamir" />
+      <name name="{=taom_bandit_umbar_m2}Sangahyandion" />
+      <name name="{=taom_bandit_umbar_m3}Angamaitë" />
+      <name name="{=taom_bandit_umbar_m4}Herumor" />
+      <name name="{=taom_bandit_umbar_m5}Fuinur" />
+      <name name="{=taom_bandit_umbar_m6}Adûnaphel" />
+      <name name="{=taom_bandit_umbar_m7}Aragôl" />
+      <name name="{=taom_bandit_umbar_m8}Calemir" />
+      <name name="{=taom_bandit_umbar_m9}Vorondil" />
+      <name name="{=taom_bandit_umbar_m10}Karazîr" />
+    </male_names>
+    <female_names>
+      <name name="{=taom_bandit_umbar_f1}Berúthiel" />
+      <name name="{=taom_bandit_umbar_f2}Nimreth" />
+      <name name="{=taom_bandit_umbar_f3}Adûniel" />
+      <name name="{=taom_bandit_umbar_f4}Tarmiriel" />
+      <name name="{=taom_bandit_umbar_f5}Inzilbêth" />
+    </female_names>
+  </Culture>
+
 </SPCultures>
warning: in the working copy of 'Main/_Module/ModuleData/taom_spcultures.xml', LF will be replaced by CRLF the next time Git touches it

 succeeded in 1975ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBanditDensityModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs

 succeeded in 2430ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditManagementIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingConfig.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\IBanditScalingConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\IBanditScalingService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\IBanditScalingSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Models\TaomBanditDensityModel.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BanditManagement\BanditScalingConfigProviderTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BanditManagement\BanditScalingServiceTests.cs
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\bandit_management\bandit_scaling_config.json
C:\Users\mikew\source\repos\TAOM\tools\.env.example
C:\Users\mikew\source\repos\TAOM\tools\add_race_attribute.py
C:\Users\mikew\source\repos\TAOM\tools\Add-RosterVariants.ps1
C:\Users\mikew\source\repos\TAOM\tools\analyze_reviews.py
C:\Users\mikew\source\repos\TAOM\tools\apply_culture_skills_traits.py
C:\Users\mikew\source\repos\TAOM\tools\apply_dolguldur_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\apply_erebor_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\apply_gondor_lord_review.py
C:\Users\mikew\source\repos\TAOM\tools\apply_gondor_polish_224.py
C:\Users\mikew\source\repos\TAOM\tools\apply_gondor_skills_traits.py
C:\Users\mikew\source\repos\TAOM\tools\apply_gondor_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\apply_gundabad_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\apply_hero_bios.py
C:\Users\mikew\source\repos\TAOM\tools\apply_isengard_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\apply_mordor_troop_revamp.py
C:\Users\mikew\source\repos\TAOM\tools\Apply-MapVillageNames.py
C:\Users\mikew\source\repos\TAOM\tools\Apply-SettlementNames.ps1
C:\Users\mikew\source\repos\TAOM\tools\armor_rebalance.csv
C:\Users\mikew\source\repos\TAOM\tools\assemble_faction_map.py
C:\Users\mikew\source\repos\TAOM\tools\assign_lord_equipment.py
C:\Users\mikew\source\repos\TAOM\tools\assign_xslt_lord_equipment.py
C:\Users\mikew\source\repos\TAOM\tools\audit_equipment_roster_coverage.py
C:\Users\mikew\source\repos\TAOM\tools\audit_item_refs.py
C:\Users\mikew\source\repos\TAOM\tools\Audit-CultureCoverage.ps1
C:\Users\mikew\source\repos\TAOM\tools\Audit-MeshRefs.ps1
C:\Users\mikew\source\repos\TAOM\tools\audit-review-counter.sh
C:\Users\mikew\source\repos\TAOM\tools\blender_bone_retargeter.py
C:\Users\mikew\source\repos\TAOM\tools\border_match.py
C:\Users\mikew\source\repos\TAOM\tools\build_backlinks.py
C:\Users\mikew\source\repos\TAOM\tools\build_erebor_kitbash.py
C:\Users\mikew\source\repos\TAOM\tools\build_test_erebor_house.py
C:\Users\mikew\source\repos\TAOM\tools\build_test_erebor_tower.py
C:\Users\mikew\source\repos\TAOM\tools\build_weapon_xml.py
C:\Users\mikew\source\repos\TAOM\tools\check_fbx_ik.js
C:\Users\mikew\source\repos\TAOM\tools\cleanup_deleted_gondor_armor.py
C:\Users\mikew\source\repos\TAOM\tools\cleanup_deleted_gondor_items.py
C:\Users\mikew\source\repos\TAOM\tools\cleanup_deleted_troops_212.py
C:\Users\mikew\source\repos\TAOM\tools\compile_research.py
C:\Users\mikew\source\repos\TAOM\tools\complete_lords_xslt.py
C:\Users\mikew\source\repos\TAOM\tools\dale_armor_meshes.txt
C:\Users\mikew\source\repos\TAOM\tools\decompile_to_folder.ps1
C:\Users\mikew\source\repos\TAOM\tools\expand_party_templates_212.py
C:\Users\mikew\source\repos\TAOM\tools\extract_fbx_bones.js
C:\Users\mikew\source\repos\TAOM\tools\extract_wanderers.py
C:\Users\mikew\source\repos\TAOM\tools\fix_lord_cultures_and_mounts.py
C:\Users\mikew\source\repos\TAOM\tools\fix_v1_4_5_item_ids.py
C:\Users\mikew\source\repos\TAOM\tools\generate_batch2_wanderers.py
C:\Users\mikew\source\repos\TAOM\tools\generate_career_starter_rosters.py
C:\Users\mikew\source\repos\TAOM\tools\generate_char_creation_equipment.py
C:\Users\mikew\source\repos\TAOM\tools\generate_culture_issue_drafts.py
C:\Users\mikew\source\repos\TAOM\tools\generate_dale_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_dale_troops.py
C:\Users\mikew\source\repos\TAOM\tools\generate_dolguldur_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_erebor_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_gondor_armor_phase2.py
C:\Users\mikew\source\repos\TAOM\tools\generate_gondor_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_gondor_troops.py
C:\Users\mikew\source\repos\TAOM\tools\generate_isengard_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_lord_template_equipment.py
C:\Users\mikew\source\repos\TAOM\tools\generate_mordor_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_rhun_armor.py
C:\Users\mikew\source\repos\TAOM\tools\generate_rhun_troops.py
C:\Users\mikew\source\repos\TAOM\tools\generate_translation_template.py
C:\Users\mikew\source\repos\TAOM\tools\generate_xslt.py
C:\Users\mikew\source\repos\TAOM\tools\Generate-ActionSets.ps1
C:\Users\mikew\source\repos\TAOM\tools\Generate-SceneEntitiesDoc.ps1
C:\Users\mikew\source\repos\TAOM\tools\Generate-Settlements.ps1
C:\Users\mikew\source\repos\TAOM\tools\Get-RosterSuggestions.ps1
C:\Users\mikew\source\repos\TAOM\tools\lint_docs.py
C:\Users\mikew\source\repos\TAOM\tools\list_fbx_objects_all.py
C:\Users\mikew\source\repos\TAOM\tools\lords_inventory.csv
C:\Users\mikew\source\repos\TAOM\tools\merge_settlements.py
C:\Users\mikew\source\repos\TAOM\tools\migrate_equipment_type_1_4_3.py
C:\Users\mikew\source\repos\TAOM\tools\migrate_hideouts_to_lotr.py
C:\Users\mikew\source\repos\TAOM\tools\process_faction_map.py
C:\Users\mikew\source\repos\TAOM\tools\README.md
C:\Users\mikew\source\repos\TAOM\tools\rebalance_armor.py
C:\Users\mikew\source\repos\TAOM\tools\rebalance_lords.py
C:\Users\mikew\source\repos\TAOM\tools\rebalance_troops.py
C:\Users\mikew\source\repos\TAOM\tools\rebalance_weapons.py
C:\Users\mikew\source\repos\TAOM\tools\rebuild_translation_files.py
C:\Users\mikew\source\repos\TAOM\tools\rewrite_ability_tooltips.py
C:\Users\mikew\source\repos\TAOM\tools\rollback_erebor_iron_misfile.py
C:\Users\mikew\source\repos\TAOM\tools\Settlement-Breakdown.ps1
C:\Users\mikew\source\repos\TAOM\tools\stamp_erebor_runes.py
C:\Users\mikew\source\repos\TAOM\tools\taom-src.ps1
C:\Users\mikew\source\repos\TAOM\tools\tpac_skeleton_dump.py
C:\Users\mikew\source\repos\TAOM\tools\tpac_skeleton_scan.py
C:\Users\mikew\source\repos\TAOM\tools\tpac_skeleton_transplant.py
C:\Users\mikew\source\repos\TAOM\tools\translate_all_remaining.sh
C:\Users\mikew\source\repos\TAOM\tools\translate_with_claude.py
C:\Users\mikew\source\repos\TAOM\tools\translation_status.sh
C:\Users\mikew\source\repos\TAOM\tools\validate_all_troop_refs.py
C:\Users\mikew\source\repos\TAOM\tools\validate_equipment_flags_1_4_3.py
C:\Users\mikew\source\repos\TAOM\tools\validate_gondor_refs.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_rebalance.csv
C:\Users\mikew\source\repos\TAOM\tools\comfyui\chatgpt_career_prompts.md
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\erebor_check.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\factions.json
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\lothlorien_check.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\polygon_widgets.xml
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\regions.json
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\verification_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\verification.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\verification2.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\verification3.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_anduin_vale.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_aru_thani_of_bellakar.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_browlands.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_chajaphan_of_abanissa.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_clans_of_andrast.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_clans_of_druwaith_iaur.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_clans_of_dunland.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_clans_of_forochel.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_dominion_of_isengard.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_dominion_of_mordor.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_eregion.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_fangorn_forest.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_goblins_of_blue_craig.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_goblins_of_goblin_town.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_golden_realm_of_rhun.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_havens_of_umbar.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_high_kingdom_of_lindon.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_khudorom_of_khand.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_angaladh.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_arthedain.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_cardolan.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_dale.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_dorwinion.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_erebor.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_ered_duin.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_ered_mithrin.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_imladris.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_lasgalen.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_lothlorien.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_moria.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_neldoreth.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_rhudaur.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_rohan.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_south_rhovanion.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_kingdom_of_zigalnara.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_map_boundary.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_nurunkhizdin.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_overlordship_of_dol_guldur.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_overlordship_of_gundabad.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_remnants_of_angmar.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_stewardship_of_gondor.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_stronghold_of_ered_gwaer.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_stronghold_of_narager.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_taskralan_of_harwan.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_taskralan_of_shaghana.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\region_wildmen_of_enedwaith.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_anduin_vale_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_aru_thani_of_bellakar_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_browlands_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_chajaphan_of_abanissa_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_clans_of_andrast_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_clans_of_druwaith_iaur_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_clans_of_dunland_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_clans_of_forochel_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_dominion_of_isengard_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_dominion_of_mordor_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_eregion_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_fangorn_forest_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_goblins_of_blue_craig_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_goblins_of_goblin_town_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_golden_realm_of_rhun_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_havens_of_umbar_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_high_kingdom_of_lindon_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_khudorom_of_khand_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_angaladh_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_arthedain_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_cardolan_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_dale_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_dorwinion_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_erebor_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_ered_duin_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_ered_mithrin_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_imladris_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_lasgalen_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_lothlorien_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_moria_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_neldoreth_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_rhudaur_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_rohan_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_south_rhovanion_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_kingdom_of_zigalnara_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_map_boundary_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_nurunkhizdin_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_overlordship_of_dol_guldur_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_overlordship_of_gundabad_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_remnants_of_angmar_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_stewardship_of_gondor_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_stronghold_of_ered_gwaer_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_stronghold_of_narager_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_taskralan_of_harwan_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_taskralan_of_shaghana_full.png
C:\Users\mikew\source\repos\TAOM\tools\factionmap_output\deploy\fullres\region_wildmen_of_enedwaith_full.png
C:\Users\mikew\source\repos\TAOM\tools\node_modules\.package-lock.json
C:\Users\mikew\source\repos\TAOM\tools\node_modules\@picode\binary-reader\LICENSE
C:\Users\mikew\source\repos\TAOM\tools\node_modules\@picode\binary-reader\package.json
C:\Users\mikew\source\repos\TAOM\tools\node_modules\@picode\binary-reader\README.md
C:\Users\mikew\source\repos\TAOM\tools\node_modules\@picode\binary-reader\lib\index.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\@picode\binary-reader\lib\index.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\LICENSE
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\package.json
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\README.md
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\ascii.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\ascii.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\binary.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\binary.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\FBXReader.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\FBXReader.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\index.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\index.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\shared.d.ts
C:\Users\mikew\source\repos\TAOM\tools\node_modules\fbx-parser\lib\shared.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\index.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\LICENSE
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\package.json
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\README.md
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_deflate.es5.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_deflate.es5.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_deflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_deflate.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_inflate.es5.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_inflate.es5.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_inflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako_inflate.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako.es5.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako.es5.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako.esm.mjs
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\dist\pako.min.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\deflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\inflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\utils\common.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\utils\strings.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\adler32.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\constants.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\crc32.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\deflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\gzheader.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\inffast.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\inflate.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\inftrees.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\messages.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\README
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\trees.js
C:\Users\mikew\source\repos\TAOM\tools\node_modules\pako\lib\zlib\zstream.js
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\REPORT.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\unused-items.tsv
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\_troops-by-culture.json
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\aserai.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\dolguldur.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\empire.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\erebor.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\gondor.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\gundabad.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\INDEX.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\isengard.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\khuzait.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\mirkwood.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\mordor.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\rivendell.md
C:\Users\mikew\source\repos\TAOM\tools\reports\culture-coverage\suggestions\vlandia.md
C:\Users\mikew\source\repos\TAOM\tools\reports\mesh-audit\LOTRLOME_Armory\meshes-present.txt
C:\Users\mikew\source\repos\TAOM\tools\reports\mesh-audit\LOTRLOME_Armory\meshes-referenced-detailed.tsv
C:\Users\mikew\source\repos\TAOM\tools\reports\mesh-audit\LOTRLOME_Armory\meshes-referenced.txt
C:\Users\mikew\source\repos\TAOM\tools\reports\mesh-audit\LOTRLOME_Armory\meshes-truly-unused.txt
C:\Users\mikew\source\repos\TAOM\tools\reports\mesh-audit\LOTRLOME_Armory\REPORT.md
C:\Users\mikew\source\repos\TAOM\tools\runes\ai_prompt.txt
C:\Users\mikew\source\repos\TAOM\tools\runes\clean_ai_mask.py
C:\Users\mikew\source\repos\TAOM\tools\runes\manifest.json
C:\Users\mikew\source\repos\TAOM\tools\runes\masks\filler\dwarven_decal_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\masks\filler\trim_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_bronze_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_bronze_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_bronze_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_carved_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_carved_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_carved_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_gold_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_gold_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_gold_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_mithril_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_mithril_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_mithril_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_silver_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_silver_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_trim_silver_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_bronze_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_bronze_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_bronze_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_carved_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_carved_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_carved_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_gold_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_gold_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_gold_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_mithril_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_mithril_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_mithril_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_silver_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_silver_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\preview_wall_silver_s.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\trim_bronze.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\trim_carved.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\trim_gold.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\trim_mithril.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\trim_silver.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\wall_bronze.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\wall_carved.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\wall_gold.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\wall_mithril.png
C:\Users\mikew\source\repos\TAOM\tools\runes\preview\thumbs\wall_silver.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_beast_eagle_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_beast_raven_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_beast_raven_002.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_beast_raven_003.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_beast_salmon_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_decal_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_decal_002.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\dwarven_icon_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_002.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_003.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_004.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_005.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\floor_motif_006.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\trim_001.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\trim_002.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\trim_003.png
C:\Users\mikew\source\repos\TAOM\tools\runes\raw_ai\trim_004.png
C:\Users\mikew\source\repos\TAOM\tools\runes\reference\mirkwood_stone_engraved_d.png
C:\Users\mikew\source\repos\TAOM\tools\runes\reference\mirkwood_stone_engraved_h.png
C:\Users\mikew\source\repos\TAOM\tools\runes\reference\mirkwood_stone_engraved_n.png
C:\Users\mikew\source\repos\TAOM\tools\runes\reference\mirkwood_stone_engraved_s.png
C:\Users\mikew\source\repos\TAOM\tools\tests\__init__.py
C:\Users\mikew\source\repos\TAOM\tools\tests\test_build_weapon_xml.py
C:\Users\mikew\source\repos\TAOM\tools\tests\fixtures\existing_swan_knight_a.xml
C:\Users\mikew\source\repos\TAOM\tools\tests\fixtures\new_gondor_test_sword.xml
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\br.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\cns.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\cnt.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\de.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\fr.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\it.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\jp.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\ko.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\ru.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\sp.json
C:\Users\mikew\source\repos\TAOM\tools\translation_cache\tr.json
C:\Users\mikew\source\repos\TAOM\tools\translation_overrides\ru.json
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\__init__.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\classify.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\config.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\extract.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\manifest.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\pipeline.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\render_items.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\render_pieces.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\render_xslt.py
C:\Users\mikew\source\repos\TAOM\tools\weapon_xml\verify.py
C:\Users\mikew\source\repos\TAOM\tools\__pycache__\build_backlinks.cpython-314.pyc
C:\Users\mikew\source\repos\TAOM\tools\__pycache__\lint_docs.cpython-314.pyc
C:\Users\mikew\source\repos\TAOM\tools\__pycache__\migrate_equipment_type_1_4_3.cpython-314.pyc
C:\Users\mikew\source\repos\TAOM\tools\__pycache__\tpac_skeleton_dump.cpython-313.pyc
C:\Users\mikew\source\repos\TAOM\tools\__pycache__\translate_with_claude.cpython-314.pyc

codex
The tracked diff shows `SubModule` registers the density model during campaign model setup, and the bandit data appends five culture rows plus ten templates. Next I’m reading the new C# files and then I’ll verify the engine paths that create bandit clans and call the patched roster method.
mcp: filesystem/read_multiple_files started
mcp: filesystem/read_multiple_files (completed)
mcp: filesystem/read_multiple_files started
mcp: filesystem/read_multiple_files (completed)
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySizeLimitModel.cs -Pattern \"FindAppropriateInitialRosterForMobileParty|GetInitialPartySizeRatioForMobileParty|FailedAssert|AddToCounts\" -Context 3,8" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBanditDensityModel.cs -Pattern \"NumberOfMinimumBanditParties|NumberOfMaximum|PlayerProgress|GetMaximumTroopCount\" -Context 2,4" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter *.cs | Select-String -Pattern \"IsBanditFaction|is_bandit|BanditBossPartyTemplate|InitializeBandit|bandit_cultures|BanditClan\" | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
2026-05-27T21:48:07.789387Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 20.3 seconds
Output:
command timed out after 20254 milliseconds

 exited 124 in 20254ms:
2026-05-27T21:48:07.819452Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 20.3 seconds
Output:
command timed out after 20284 milliseconds

 exited 124 in 20284ms:
 succeeded in 99566ms:

[32;1mPath                                                                                                             [0m[32;1m LineN[0m
[32;1m                                                                                                                 [0m[32;1m umber[0m
[32;1m----                                                                                                             [0m [32;1m-----[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs                                582
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs   194
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs   999
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       300
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       430
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       444
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       592
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       594
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       749
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       881
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs              178
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs              275
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\FactionManager.cs             183
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\FactionManager.cs             214
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1674
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1682
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\IFaction.cs                    54
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs                    173
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs                    179
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\KillCharacterActio…   133
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   215
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   499
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   590
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…    57
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…    67
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   121
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   134
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   141
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   145
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   201
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   202
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   204
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   242
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   248
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   280
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   319
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   326
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   440
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   463
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   472
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   560
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   562
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   569
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Campaign…    17
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Campaign…    43
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Characte…    56
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…   212
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…   391
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\FactionD…   121
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpaw…    22
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpaw…   144
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\InitialC…    26
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCa…    92
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCa…   149
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\KingdomD…    69
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyHea…   109
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RansomOf…    66
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBe…    43
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   182
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   126
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   152
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   253
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   180
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   184
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   189
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   190
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   195
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   196
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   201
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   202
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   266
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1075
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1127
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1171
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMarr…    42
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSett…    59
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSett…   164
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDeserter…   962
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWi…   653
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\ChangeSettlemen…   115
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…    38
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…   149
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…   151
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…   206
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…   355
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\Band…    28
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\Band…    74
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\Band…   161
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs         40
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs        127
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…  1458
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…  1515
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   584
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   628
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   534
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   240
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   251
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   255
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   240
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   251
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   251
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewMo…   454
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BasicCultureObject.cs                                  59
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\Give5TroopsToPlayerCheat.cs                                         24
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\HideoutConversationsCampaignBehavior.cs           51
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\SnareTheWealthyIssueBehavior.cs                             914
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\MissionCrimeHandler.cs                       14
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Hideout\HideoutMissionController.cs         132
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Hideout\HideoutMissionController.cs         248
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.FirstPhase\ArzagosBannerPieceQuest.cs                   284
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.FirstPhase\IstianasBannerPieceQuest.cs                  284
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.PlayerClanQuests\RescueFamilyQuestBehavior.cs           339
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.PlayerClanQuests\RescueFamilyQuestBehavior.cs           809
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\ConspiracyBaseOfOperati…   576
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\DisruptSupplyLinesConsp…   390
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.TutorialPhase\FindHideoutTutorialQuest.cs               374


codex
The broad vanilla search found the bandit behavior files; I’ll narrow to those files now. The first specific decompile reads timed out at a short limit, so I’m rerunning them with direct file reads and a longer timeout.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySizeLimitModel.cs'; "'$lines=Get-Content -Path $p; for($i=400;$i -le 490;$i++){ if($i -le $lines.Length){ '"'{0}: {1}' -f "'$i,$lines[$i-1] } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter Bandit*.cs | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBanditDensityModel.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 867ms:
400: 			float num2 = 0.4f + 0.8f * playerProgress;
401: 			float num3 = MBRandom.RandomFloatRanged(0.2f, 0.8f);
402: 			return num2 * num3;
403: 		}
404: 		if (party.IsCaravan && party.Owner == Hero.MainHero)
405: 		{
406: 			return 1f;
407: 		}
408: 		if (party.IsPatrolParty)
409: 		{
410: 			return 1f;
411: 		}
412: 		return party.RandomFloat();
413: 	}
414: 
415: 	public override int GetIdealVillagerPartySize(Village village)
416: 	{
417: 		float num = 0f;
418: 		foreach (var production in village.VillageType.Productions)
419: 		{
420: 			float resultNumber = Campaign.Current.Models.VillageProductionCalculatorModel.CalculateDailyProductionAmount(village, production.Item1).ResultNumber;
421: 			num += resultNumber;
422: 		}
423: 		float num2 = ((num > 10f) ? (40f * (1f - (TaleWorlds.Library.MathF.Min(40f, num) - 10f) / 60f)) : 40f);
424: 		return MinimumNumberOfVillagersAtVillagerParty + (int)(village.Hearth / num2);
425: 	}
426: 
427: 	public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
428: 	{
429: 		TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
430: 		float initialPartySizeRatioForMobileParty = GetInitialPartySizeRatioForMobileParty(party, partyTemplate);
431: 		for (int i = 0; i < partyTemplate.Stacks.Count; i++)
432: 		{
433: 			int minValue = partyTemplate.Stacks[i].MinValue;
434: 			int maxValue = partyTemplate.Stacks[i].MaxValue;
435: 			int num = minValue;
436: 			if (initialPartySizeRatioForMobileParty <= 0f)
437: 			{
438: 				num = minValue;
439: 			}
440: 			else if (initialPartySizeRatioForMobileParty <= 1f)
441: 			{
442: 				num = MBRandom.RoundRandomized((float)minValue + (float)(maxValue - minValue) * initialPartySizeRatioForMobileParty);
443: 			}
444: 			else
445: 			{
446: 				Debug.FailedAssert("initialPartySizeRatio should not be above 1", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultPartySizeLimitModel.cs", "FindAppropriateInitialRosterForMobileParty", 538);
447: 				num = maxValue;
448: 			}
449: 			if (party.IsVillager)
450: 			{
451: 				Village village = party.VillagerPartyComponent.Village;
452: 				if (village.Bound?.Town?.Governor != null && village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Scouting.VillageNetwork))
453: 				{
454: 					num = TaleWorlds.Library.MathF.Round((float)num * (1f + DefaultPerks.Scouting.VillageNetwork.SecondaryBonus));
455: 				}
456: 			}
457: 			if (num > 0)
458: 			{
459: 				CharacterObject character = partyTemplate.Stacks[i].Character;
460: 				troopRoster.AddToCounts(character, num);
461: 			}
462: 		}
463: 		return troopRoster;
464: 	}
465: 
466: 	public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
467: 	{
468: 		List<Ship> list = new List<Ship>();
469: 		float initialPartySizeRatioForMobileParty = GetInitialPartySizeRatioForMobileParty(party, partyTemplate);
470: 		if (partyTemplate.ShipHulls != null && partyTemplate.ShipHulls.Count > 0)
471: 		{
472: 			foreach (ShipTemplateStack shipHull in partyTemplate.ShipHulls)
473: 			{
474: 				int minValue = shipHull.MinValue;
475: 				int maxValue = shipHull.MaxValue;
476: 				int num = minValue;
477: 				num = ((initialPartySizeRatioForMobileParty <= 0f) ? MBRandom.RoundRandomized(Math.Max(0f, (float)minValue + (float)minValue * initialPartySizeRatioForMobileParty)) : ((!(initialPartySizeRatioForMobileParty <= 1f)) ? MBRandom.RoundRandomized((float)maxValue * initialPartySizeRatioForMobileParty) : MBRandom.RoundRandomized((float)minValue + (float)(maxValue - minValue) * initialPartySizeRatioForMobileParty)));
478: 				for (int i = 0; i < num; i++)
479: 				{
480: 					list.Add(new Ship(shipHull.ShipHull));
481: 				}
482: 			}
483: 		}
484: 		return list;
485: 	}
486: }

 succeeded in 853ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteract…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCam…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\BanditDensit…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPart…


 succeeded in 1208ms:
1: using TaleWorlds.CampaignSystem.CharacterDevelopment;
2: using TaleWorlds.CampaignSystem.ComponentInterfaces;
3: using TaleWorlds.CampaignSystem.Party;
4: using TaleWorlds.Library;
5: 
6: namespace TaleWorlds.CampaignSystem.GameComponents;
7: 
8: public class DefaultBanditDensityModel : BanditDensityModel
9: {
10: 	private const int MinimumTroopCountForHideoutMission = 25;
11: 
12: 	private Clan _deserterClan;
13: 
14: 	public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt => 2;
15: 
16: 	public override int NumberOfMaximumBanditPartiesInEachHideout => 3;
17: 
18: 	public override int NumberOfMaximumBanditPartiesAroundEachHideout => 3;
19: 
20: 	public override int NumberOfMaximumHideoutsAtEachBanditFaction => 9;
21: 
22: 	public override int NumberOfInitialHideoutsAtEachBanditFaction => 7;
23: 
24: 	public override int NumberOfMinimumBanditTroopsInHideoutMission => 10;
25: 
26: 	public override int NumberOfMaximumTroopCountForFirstFightInHideout => MathF.Floor(11f * (2f + Campaign.Current.PlayerProgress));
27: 
28: 	public override int NumberOfMaximumTroopCountForBossFightInHideout => MathF.Floor(1f + 5f * (1f + Campaign.Current.PlayerProgress));
29: 
30: 	public override float SpawnPercentageForFirstFightInHideoutMission => 0.8f;
31: 
32: 	private Clan DeserterClan
33: 	{
34: 		get
35: 		{
36: 			if (_deserterClan == null)
37: 			{
38: 				_deserterClan = Clan.FindFirst((Clan x) => x.StringId == "deserters");
39: 			}
40: 			return _deserterClan;
41: 		}
42: 	}
43: 
44: 	public override int GetMinimumTroopCountForHideoutMission(MobileParty party, bool isAssault)
45: 	{
46: 		if (!isAssault)
47: 		{
48: 			return 25;
49: 		}
50: 		return 8;
51: 	}
52: 
53: 	public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
54: 	{
55: 		if (clan == DeserterClan)
56: 		{
57: 			return 50;
58: 		}
59: 		if (clan.StringId == "looters" && DeserterClan != null)
60: 		{
61: 			return 270 - DeserterClan.WarPartyComponents.Count;
62: 		}
63: 		return 270;
64: 	}
65: 
66: 	public override int GetMaximumTroopCountForHideoutMission(MobileParty party, bool isAssault)
67: 	{
68: 		int num = (isAssault ? 15 : 40);
69: 		if (party.HasPerk(DefaultPerks.Tactics.SmallUnitTactics))
70: 		{
71: 			num += (int)DefaultPerks.Tactics.SmallUnitTactics.PrimaryBonus;
72: 		}
73: 		return num;
74: 	}
75: 
76: 	public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
77: 	{
78: 		return false;
79: 	}
80: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter Bandit*.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 849ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\BanditDensityModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\BanditSpawnCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=220;$i -le 360;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\BanditSpawnCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 220;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\BanditSpawnCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=360;$i -le 590;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 893ms:
220: 			allBanditParty.PartyTradeGold = (int)((double)allBanditParty.PartyTradeGold * 0.95 + (double)(50f * (float)allBanditParty.Party.MemberRoster.TotalManCount * 0.05f));
221: 			if (!(MBRandom.RandomFloat < 0.03f) || allBanditParty.MapEvent == null)
222: 			{
223: 				continue;
224: 			}
225: 			foreach (ItemObject item in Items.All)
226: 			{
227: 				if (item.IsFood)
228: 				{
229: 					int num = (IsLooterFaction(allBanditParty.MapFaction) ? 8 : 16);
230: 					int num2 = MBRandom.RoundRandomized((float)allBanditParty.MemberRoster.TotalManCount * (1f / (float)item.Value) * (float)num * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
231: 					if (num2 > 0)
232: 					{
233: 						allBanditParty.ItemRoster.AddToCounts(item, num2);
234: 					}
235: 				}
236: 			}
237: 		}
238: 	}
239: 
240: 	private void HourlyTickClan(Clan clan)
241: 	{
242: 		if (Campaign.Current.IsNight && clan.IsBanditFaction)
243: 		{
244: 			if (IsLooterFaction(clan))
245: 			{
246: 				SpawnLooters(clan, 0.07f, uniformDistribution: false);
247: 			}
248: 			else if (IsBanditFaction(clan))
249: 			{
250: 				SpawnBanditsAroundHideout(clan, 0.1f);
251: 			}
252: 		}
253: 	}
254: 
255: 	private void SpawnBanditsAroundHideout(Clan clan, float ratio)
256: 	{
257: 		int count = clan.WarPartyComponents.Count;
258: 		int num = MBRandom.RoundRandomized((float)(GetInfestedHideoutCount(clan) * _numberOfMaxBanditCountPerClanHideout - count) * ratio);
259: 		for (int i = 0; i < num; i++)
260: 		{
261: 			SpawnBanditParty(clan);
262: 		}
263: 	}
264: 
265: 	private void SpawnLooters(Clan clan, float ratio, bool uniformDistribution)
266: 	{
267: 		int count = clan.WarPartyComponents.Count;
268: 		int num = MBRandom.RoundRandomized((float)(GetCurrentLimitForLooters(clan) - count) * ratio);
269: 		for (int i = 0; i < num; i++)
270: 		{
271: 			SpawnLooterParty(clan, uniformDistribution);
272: 		}
273: 	}
274: 
275: 	private void AddNewHideouts()
276: 	{
277: 		List<((Clan, int), float)> list = new List<((Clan, int), float)>();
278: 		foreach (Clan banditFaction in Clan.BanditFactions)
279: 		{
280: 			if (IsBanditFaction(banditFaction))
281: 			{
282: 				int infestedHideoutCount = GetInfestedHideoutCount(banditFaction);
283: 				if (infestedHideoutCount < _numberOfMaxHideoutsAtEachBanditFaction)
284: 				{
285: 					list.Add(((banditFaction, infestedHideoutCount), 1f - (float)infestedHideoutCount / (float)_numberOfMaxHideoutsAtEachBanditFaction));
286: 				}
287: 			}
288: 		}
289: 		var (clan, num) = MBRandom.ChooseWeighted(list, out var _);
290: 		if (clan != null)
291: 		{
292: 			float num2 = (((float)num < (float)_numberOfMaxHideoutsAtEachBanditFaction * 0.5f) ? (0.2f + (float)(_numberOfMaxHideoutsAtEachBanditFaction - num) * 0.1f) : (0.1f + 0.5f * TaleWorlds.Library.MathF.Pow(1f - 0.25f * ((float)num - (float)_numberOfMaxHideoutsAtEachBanditFaction * 0.5f), 3f)));
293: 			if (MBRandom.RandomFloat < num2)
294: 			{
295: 				FillANewHideoutWithBandits(clan);
296: 			}
297: 		}
298: 	}
299: 
300: 	private void FillANewHideoutWithBandits(Clan faction)
301: 	{
302: 		Hideout hideout = SelectANonInfestedHideoutOfSameCultureByWeight(faction);
303: 		if (hideout != null)
304: 		{
305: 			for (int i = 0; (float)i < _numberOfMinimumBanditPartiesInAHideoutToInfestIt; i++)
306: 			{
307: 				AddBanditToHideout(hideout);
308: 			}
309: 		}
310: 	}
311: 
312: 	public MobileParty AddBanditToHideout(Hideout hideoutComponent, PartyTemplateObject overridenPartyTemplate = null, bool isBanditBossParty = false)
313: 	{
314: 		if (hideoutComponent.Owner.Settlement.Culture.IsBandit)
315: 		{
316: 			Clan clan = null;
317: 			foreach (Clan banditFaction in Clan.BanditFactions)
318: 			{
319: 				if (hideoutComponent.Owner.Settlement.Culture == banditFaction.Culture && (IsBanditFaction(banditFaction) || IsLooterFaction(banditFaction)))
320: 				{
321: 					clan = banditFaction;
322: 				}
323: 			}
324: 			PartyTemplateObject pt = overridenPartyTemplate ?? clan.DefaultPartyTemplate;
325: 			MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(clan.StringId + "_1", clan, hideoutComponent, isBanditBossParty, pt, hideoutComponent.Owner.Settlement.GatePosition);
326: 			InitializeBanditParty(mobileParty, clan);
327: 			mobileParty.SetMoveGoToSettlement(hideoutComponent.Owner.Settlement, mobileParty.NavigationCapability, isTargetingThePort: false);
328: 			mobileParty.RecalculateShortTermBehavior();
329: 			EnterSettlementAction.ApplyForParty(mobileParty, hideoutComponent.Owner.Settlement);
330: 			return mobileParty;
331: 		}
332: 		return null;
333: 	}
334: 
335: 	private Hideout SelectBanditHideout(Clan faction)
336: 	{
337: 		MBList<(Hideout, float)> mBList = new MBList<(Hideout, float)>();
338: 		foreach (Hideout item in Hideout.All)
339: 		{
340: 			if (item.Settlement.Culture == faction.Culture && item.IsInfested)
341: 			{
342: 				mBList.Add((item, GetSpawnChanceInSettlement(item.Settlement)));
343: 			}
344: 		}
345: 		if (mBList.Count != 0)
346: 		{
347: 			return MBRandom.ChooseWeighted(mBList);
348: 		}
349: 		return SelectAHideoutByCheckingCultureAndInfestedState(faction);
350: 	}
351: 
352: 	private float GetSpawnChanceInSettlement(Settlement settlement)
353: 	{
354: 		if (_banditCountsPerHideout.ContainsKey(settlement) && _banditCountsPerHideout[settlement] != 0)
355: 		{
356: 			return 1f / TaleWorlds.Library.MathF.Pow(_banditCountsPerHideout[settlement], 2f);
357: 		}
358: 		return 1f;
359: 	}
360: 

 succeeded in 893ms:
1: using System;
2: using System.Collections.Generic;
3: using System.Linq;
4: using Helpers;
5: using TaleWorlds.CampaignSystem.Actions;
6: using TaleWorlds.CampaignSystem.Extensions;
7: using TaleWorlds.CampaignSystem.Party;
8: using TaleWorlds.CampaignSystem.Party.PartyComponents;
9: using TaleWorlds.CampaignSystem.Settlements;
10: using TaleWorlds.Core;
11: using TaleWorlds.Library;
12: 
13: namespace TaleWorlds.CampaignSystem.CampaignBehaviors;
14: 
15: public class BanditSpawnCampaignBehavior : CampaignBehaviorBase
16: {
17: 	private const float BanditStartGoldPerBandit = 10f;
18: 
19: 	private const float BanditLongTermGoldPerBandit = 50f;
20: 
21: 	private const float HideoutInfestCooldownAfterFightInDays = 1.5f;
22: 
23: 	private Dictionary<CultureObject, List<Hideout>> _hideouts = new Dictionary<CultureObject, List<Hideout>>();
24: 
25: 	private Dictionary<Settlement, int> _banditCountsPerHideout = new Dictionary<Settlement, int>();
26: 
27: 	private float BanditSpawnRadiusAsDays => 0.5f * Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay;
28: 
29: 	private float _radiusAroundPlayerPartySquared => MobileParty.MainParty.SeeingRange * MobileParty.MainParty.SeeingRange;
30: 
31: 	private float _numberOfMinimumBanditPartiesInAHideoutToInfestIt => Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
32: 
33: 	private int _numberOfMaxBanditPartiesAroundEachHideout => Campaign.Current.Models.BanditDensityModel.NumberOfMaximumBanditPartiesAroundEachHideout;
34: 
35: 	private int _numberOfMaxHideoutsAtEachBanditFaction => Campaign.Current.Models.BanditDensityModel.NumberOfMaximumHideoutsAtEachBanditFaction;
36: 
37: 	private int _numberOfInitialHideoutsAtEachBanditFaction => Campaign.Current.Models.BanditDensityModel.NumberOfInitialHideoutsAtEachBanditFaction;
38: 
39: 	private int _numberOfMaximumBanditPartiesInEachHideout => Campaign.Current.Models.BanditDensityModel.NumberOfMaximumBanditPartiesInEachHideout;
40: 
41: 	private int _numberOfMaxBanditCountPerClanHideout => _numberOfMaxBanditPartiesAroundEachHideout + _numberOfMaximumBanditPartiesInEachHideout;
42: 
43: 	public override void RegisterEvents()
44: 	{
45: 		CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, MobilePartyCreated);
46: 		CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, MobilePartyDestroyed);
47: 		CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
48: 		CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);
49: 		CampaignEvents.HourlyTickClanEvent.AddNonSerializedListener(this, HourlyTickClan);
50: 		CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
51: 		CampaignEvents.OnHomeHideoutChangedEvent.AddNonSerializedListener(this, OnHomeHideoutChanged);
52: 		CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
53: 	}
54: 
55: 	private void MobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
56: 	{
57: 		if (party.IsBandit && party.ActualClan != null && (IsBanditFaction(party.ActualClan) || IsLooterFaction(party.ActualClan)))
58: 		{
59: 			int value = 0;
60: 			_banditCountsPerHideout.TryGetValue(party.HomeSettlement, out value);
61: 			_banditCountsPerHideout[party.HomeSettlement] = value - 1;
62: 		}
63: 	}
64: 
65: 	private void MobilePartyCreated(MobileParty party)
66: 	{
67: 		if (party.IsBandit && party.ActualClan != null && (IsBanditFaction(party.ActualClan) || IsLooterFaction(party.ActualClan)))
68: 		{
69: 			int value = 0;
70: 			_banditCountsPerHideout.TryGetValue(party.HomeSettlement, out value);
71: 			_banditCountsPerHideout[party.HomeSettlement] = value + 1;
72: 		}
73: 	}
74: 
75: 	private void OnGameLoaded(CampaignGameStarter starter)
76: 	{
77: 		CacheHideouts();
78: 		CacheBanditCounts();
79: 	}
80: 
81: 	public override void SyncData(IDataStore dataStore)
82: 	{
83: 	}
84: 
85: 	private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
86: 	{
87: 		switch (i)
88: 		{
89: 		case 10:
90: 			CacheHideouts();
91: 			if (_numberOfInitialHideoutsAtEachBanditFaction > 0)
92: 			{
93: 				InitializeInitialHideouts();
94: 			}
95: 			break;
96: 		case 11:
97: 			SpawnBanditsAroundHideoutAtNewGame();
98: 			SpawnLootersAtNewGame();
99: 			CacheBanditCounts();
100: 			break;
101: 		}
102: 	}
103: 
104: 	private void CacheHideouts()
105: 	{
106: 		foreach (Hideout item in Hideout.All)
107: 		{
108: 			if (!_hideouts.TryGetValue(item.Settlement.Culture, out var _))
109: 			{
110: 				_hideouts[item.Settlement.Culture] = new List<Hideout>();
111: 			}
112: 			_hideouts[item.Settlement.Culture].Add(item);
113: 		}
114: 	}
115: 
116: 	private void CacheBanditCounts()
117: 	{
118: 		_banditCountsPerHideout = new Dictionary<Settlement, int>();
119: 		foreach (MobileParty allBanditParty in MobileParty.AllBanditParties)
120: 		{
121: 			if (IsBanditFaction(allBanditParty.ActualClan) || IsLooterFaction(allBanditParty.ActualClan))
122: 			{
123: 				int value = 0;
124: 				_banditCountsPerHideout.TryGetValue(allBanditParty.HomeSettlement, out value);
125: 				_banditCountsPerHideout[allBanditParty.HomeSettlement] = value + 1;
126: 			}
127: 		}
128: 	}
129: 
130: 	public void InitializeInitialHideouts()
131: 	{
132: 		foreach (Clan banditFaction in Clan.BanditFactions)
133: 		{
134: 			if (IsBanditFaction(banditFaction))
135: 			{
136: 				SpawnHideoutsAndBanditsPartiallyOnNewGame(banditFaction);
137: 			}
138: 		}
139: 	}
140: 
141: 	private void SpawnHideoutsAndBanditsPartiallyOnNewGame(Clan banditClan)
142: 	{
143: 		for (int i = 0; i < _numberOfInitialHideoutsAtEachBanditFaction; i++)
144: 		{
145: 			FillANewHideoutWithBandits(banditClan);
146: 		}
147: 	}
148: 
149: 	public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
150: 	{
151: 		CheckForSpawningBanditBoss(settlement, mobileParty);
152: 		if (!Campaign.Current.GameStarted || mobileParty == null || !mobileParty.IsBandit || !settlement.IsHideout)
153: 		{
154: 			return;
155: 		}
156: 		if (!settlement.Hideout.IsSpotted && settlement.Hideout.IsInfested && mobileParty.IsVisible)
157: 		{
158: 			settlement.Hideout.IsSpotted = true;
159: 			settlement.Party.UpdateVisibilityAndInspected(MobileParty.MainParty.Position);
160: 			CampaignEventDispatcher.Instance.OnHideoutSpotted(MobileParty.MainParty.Party, settlement.Party);
161: 		}
162: 		int num = 0;
163: 		foreach (ItemRosterElement item in mobileParty.ItemRoster)
164: 		{
165: 			int num2 = (item.EquipmentElement.Item.IsFood ? MBRandom.RoundRandomized((float)mobileParty.MemberRoster.TotalManCount * ((3f + 6f * MBRandom.RandomFloat) / (float)item.EquipmentElement.Item.Value)) : 0);
166: 			if (item.Amount > num2)
167: 			{
168: 				int num3 = item.Amount - num2;
169: 				num += num3 * item.EquipmentElement.Item.Value;
170: 			}
171: 		}
172: 		if (num > 0)
173: 		{
174: 			if (mobileParty.IsPartyTradeActive)
175: 			{
176: 				mobileParty.PartyTradeGold += (int)(0.25f * (float)num);
177: 			}
178: 			settlement.SettlementComponent.ChangeGold((int)(0.25f * (float)num));
179: 		}
180: 	}
181: 
182: 	private void CheckForSpawningBanditBoss(Settlement settlement, MobileParty mobileParty)
183: 	{
184: 		if (settlement.IsHideout && settlement.Hideout.IsSpotted && settlement.Parties.Any((MobileParty x) => x.IsBandit || x.IsBanditBossParty))
185: 		{
186: 			CultureObject culture = settlement.Culture;
187: 			MobileParty mobileParty2 = settlement.Parties.FirstOrDefault((MobileParty x) => x.IsBanditBossParty);
188: 			if (mobileParty2 == null)
189: 			{
190: 				AddBossParty(settlement, culture);
191: 			}
192: 			else if (!mobileParty2.MemberRoster.Contains(culture.BanditBoss))
193: 			{
194: 				mobileParty2.MemberRoster.AddToCounts(culture.BanditBoss, 1);
195: 			}
196: 		}
197: 	}
198: 
199: 	private void AddBossParty(Settlement settlement, CultureObject culture)
200: 	{
201: 		PartyTemplateObject banditBossPartyTemplate = culture.BanditBossPartyTemplate;
202: 		if (banditBossPartyTemplate != null)
203: 		{
204: 			AddBanditToHideout(settlement.Hideout, banditBossPartyTemplate, isBanditBossParty: true).Ai.DisableAi();
205: 		}
206: 	}
207: 
208: 	public void DailyTick()
209: 	{
210: 		if (_numberOfMaxHideoutsAtEachBanditFaction > 0)
211: 		{
212: 			AddNewHideouts();
213: 		}
214: 		foreach (MobileParty allBanditParty in MobileParty.AllBanditParties)
215: 		{
216: 			if (!allBanditParty.IsPartyTradeActive)
217: 			{
218: 				continue;
219: 			}
220: 			allBanditParty.PartyTradeGold = (int)((double)allBanditParty.PartyTradeGold * 0.95 + (double)(50f * (float)allBanditParty.Party.MemberRoster.TotalManCount * 0.05f));

 succeeded in 861ms:
360: 
361: 	private void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
362: 	{
363: 		int value = 0;
364: 		_banditCountsPerHideout.TryGetValue(oldHomeHideout.Settlement, out value);
365: 		_banditCountsPerHideout[oldHomeHideout.Settlement] = value - 1;
366: 		value = 0;
367: 		_banditCountsPerHideout.TryGetValue(banditPartyComponent.HomeSettlement, out value);
368: 		_banditCountsPerHideout[banditPartyComponent.HomeSettlement] = value + 1;
369: 	}
370: 
371: 	private Hideout SelectAHideoutByCheckingCultureAndInfestedState(Clan faction)
372: 	{
373: 		List<Hideout> list = new List<Hideout>();
374: 		bool flag = false;
375: 		bool flag2 = false;
376: 		foreach (Hideout item in Hideout.All)
377: 		{
378: 			bool flag3 = item.Settlement.Culture == faction.Culture;
379: 			bool isInfested = item.IsInfested;
380: 			if (!flag2 && flag3)
381: 			{
382: 				flag2 = true;
383: 				list.Clear();
384: 			}
385: 			if (flag2 && !flag && isInfested)
386: 			{
387: 				flag = true;
388: 				list.Clear();
389: 			}
390: 			if ((!flag2 || flag3) && (!flag || isInfested))
391: 			{
392: 				list.Add(item);
393: 			}
394: 		}
395: 		return list.GetRandomElement();
396: 	}
397: 
398: 	private Hideout SelectANonInfestedHideoutOfSameCultureByWeight(Clan faction)
399: 	{
400: 		float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
401: 		float num = averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.33f * averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.33f;
402: 		List<(Hideout, float)> list = new List<(Hideout, float)>();
403: 		foreach (Hideout item in Hideout.All)
404: 		{
405: 			if (item.IsInfested || item.Settlement.Culture != faction.Culture)
406: 			{
407: 				continue;
408: 			}
409: 			int num2 = 1;
410: 			if (item.Settlement.LastThreatTime.ElapsedDaysUntilNow > 1.5f)
411: 			{
412: 				float num3 = Campaign.MapDiagonalSquared;
413: 				float num4 = Campaign.MapDiagonalSquared;
414: 				foreach (Hideout item2 in Hideout.All)
415: 				{
416: 					if (item != item2 && item2.IsInfested)
417: 					{
418: 						float num5 = item.Settlement.Position.DistanceSquared(item2.Settlement.Position);
419: 						if (item.Settlement.Culture == item2.Settlement.Culture && num5 < num3)
420: 						{
421: 							num3 = num5;
422: 						}
423: 						if (num5 < num4)
424: 						{
425: 							num4 = num5;
426: 						}
427: 					}
428: 					num2 = (int)TaleWorlds.Library.MathF.Max(averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.015f, num3 / num + averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.076f * (num4 / num));
429: 				}
430: 			}
431: 			list.Add((item, num2));
432: 		}
433: 		return MBRandom.ChooseWeighted(list);
434: 	}
435: 
436: 	public void SpawnBanditsAroundHideoutAtNewGame()
437: 	{
438: 		foreach (Clan banditFaction in Clan.BanditFactions)
439: 		{
440: 			if (IsBanditFaction(banditFaction))
441: 			{
442: 				SpawnBanditsAroundHideout(banditFaction, MBRandom.RandomFloatRanged(0.5f, 0.75f));
443: 			}
444: 		}
445: 	}
446: 
447: 	public void SpawnLootersAtNewGame()
448: 	{
449: 		foreach (Clan banditFaction in Clan.BanditFactions)
450: 		{
451: 			if (IsLooterFaction(banditFaction))
452: 			{
453: 				SpawnLooters(banditFaction, MBRandom.RandomFloatRanged(0.5f, 0.75f), uniformDistribution: true);
454: 			}
455: 		}
456: 	}
457: 
458: 	private void SpawnLooterParty(Clan selectedFaction, bool uniformDistribution)
459: 	{
460: 		Settlement settlement = SelectARandomSettlementForLooterParty(uniformDistribution);
461: 		CampaignVec2 spawnPositionAroundSettlement = GetSpawnPositionAroundSettlement(selectedFaction, settlement);
462: 		MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(selectedFaction.StringId + "_1", selectedFaction, settlement, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
463: 		InitializeBanditParty(mobileParty, selectedFaction);
464: 		mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, MobileParty.NavigationType.Default);
465: 	}
466: 
467: 	private void SpawnBanditParty(Clan selectedFaction)
468: 	{
469: 		Hideout hideout = SelectBanditHideout(selectedFaction);
470: 		CampaignVec2 spawnPositionAroundSettlement = GetSpawnPositionAroundSettlement(selectedFaction, hideout.Settlement);
471: 		MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(selectedFaction.StringId + "_1", selectedFaction, hideout, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
472: 		InitializeBanditParty(mobileParty, selectedFaction);
473: 		mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, mobileParty.NavigationCapability);
474: 	}
475: 
476: 	private static bool IsLooterFaction(IFaction faction)
477: 	{
478: 		if (!faction.Culture.CanHaveSettlement && !faction.HasNavalNavigationCapability)
479: 		{
480: 			return faction.StringId != "deserters";
481: 		}
482: 		return false;
483: 	}
484: 
485: 	private float GetSpawnRadiusForClan(Clan selectedFaction)
486: 	{
487: 		return BanditSpawnRadiusAsDays * (IsLooterFaction(selectedFaction) ? 1.5f : 1f);
488: 	}
489: 
490: 	private int GetInfestedHideoutCount(Clan banditFaction)
491: 	{
492: 		int num = 0;
493: 		foreach (Hideout item in _hideouts[banditFaction.Culture])
494: 		{
495: 			if (item.IsInfested && item.MapFaction == banditFaction)
496: 			{
497: 				num++;
498: 			}
499: 		}
500: 		return num;
501: 	}
502: 
503: 	private int GetCurrentLimitForLooters(Clan clan)
504: 	{
505: 		return Math.Min(Hideout.All.Count((Hideout x) => x.IsInfested) * 7, Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(clan));
506: 	}
507: 
508: 	private Settlement SelectARandomSettlementForLooterParty(bool uniformDistribution)
509: 	{
510: 		MBList<(Settlement, float)> mBList = new MBList<(Settlement, float)>();
511: 		foreach (Settlement item in Settlement.All)
512: 		{
513: 			if (item.IsTown || item.IsVillage)
514: 			{
515: 				mBList.Add((item, GetSpawnChanceInSettlement(item)));
516: 			}
517: 		}
518: 		return MBRandom.ChooseWeighted(mBList);
519: 	}
520: 
521: 	private void GiveFoodToBanditParty(MobileParty banditParty)
522: 	{
523: 		int num = (IsLooterFaction(banditParty.MapFaction) ? 8 : 16);
524: 		foreach (ItemObject item in Items.All)
525: 		{
526: 			if (item.IsFood)
527: 			{
528: 				int num2 = MBRandom.RoundRandomized((float)banditParty.MemberRoster.TotalManCount * (1f / (float)item.Value) * (float)num * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
529: 				if (num2 > 0)
530: 				{
531: 					banditParty.ItemRoster.AddToCounts(item, num2);
532: 				}
533: 			}
534: 		}
535: 	}
536: 
537: 	private CampaignVec2 GetSpawnPositionAroundSettlement(Clan clan, Settlement settlement)
538: 	{
539: 		CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(settlement.GatePosition, MobileParty.NavigationType.Default, GetSpawnRadiusForClan(clan));
540: 		if (campaignVec.DistanceSquared(MobileParty.MainParty.Position) < _radiusAroundPlayerPartySquared)
541: 		{
542: 			for (int i = 0; i < 15; i++)
543: 			{
544: 				CampaignVec2 campaignVec2 = NavigationHelper.FindReachablePointAroundPosition(campaignVec, MobileParty.NavigationType.Default, GetSpawnRadiusForClan(clan));
545: 				if (NavigationHelper.IsPositionValidForNavigationType(campaignVec2, MobileParty.NavigationType.Default))
546: 				{
547: 					float landRatio;
548: 					float num = DistanceHelper.FindClosestDistanceFromMobilePartyToPoint(MobileParty.MainParty, campaignVec2, MobileParty.NavigationType.Default, out landRatio);
549: 					if (num * num > _radiusAroundPlayerPartySquared)
550: 					{
551: 						campaignVec = campaignVec2;
552: 						break;
553: 					}
554: 				}
555: 			}
556: 		}
557: 		return campaignVec;
558: 	}
559: 
560: 	private bool IsBanditFaction(Clan clan)
561: 	{
562: 		if (!clan.HasNavalNavigationCapability && clan.IsBanditFaction)
563: 		{
564: 			return clan.Culture.CanHaveSettlement;
565: 		}
566: 		return false;
567: 	}
568: 
569: 	private void InitializeBanditParty(MobileParty banditParty, Clan faction)
570: 	{
571: 		banditParty.Party.SetVisualAsDirty();
572: 		banditParty.ActualClan = faction;
573: 		banditParty.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
574: 		CreatePartyTrade(banditParty);
575: 		GiveFoodToBanditParty(banditParty);
576: 	}
577: 
578: 	private static void CreatePartyTrade(MobileParty banditParty)
579: 	{
580: 		int initialGold = (int)(10f * (float)banditParty.Party.MemberRoster.TotalManCount * (0.5f + 1f * MBRandom.RandomFloat));
581: 		banditParty.InitializePartyTrade(initialGold);
582: 	}
583: }
584: 
585: 
586: 
587: 
588: 
589: 
590: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CultureObject.cs'; "'$lines=Get-Content -Path $p; for($i=130;$i -le 310;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem,E:\\Decompiled_Bannerlord\\Modules\\SandBox -Recurse -Filter *.cs | Select-String -Pattern \"BanditFactions|IsBanditFaction|new Clan|InitializeClan|CreateClan|MBObjectManager.Instance.GetObjectTypeList<CultureObject>|CultureObject.All|IsBandit\" | Select-Object Path,LineNumber,Line -First 200" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Clan.cs'; "'$lines=Get-Content -Path $p; for($i=260;$i -le 330;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=730;$i -le 770;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 970ms:
130: 	public CharacterObject Shipwright { get; private set; }
131: 
132: 	public CharacterObject ShipyardWorker { get; private set; }
133: 
134: 	public CharacterObject MilitiaVeteranArcher { get; private set; }
135: 
136: 	public CharacterObject GearDummy { get; private set; }
137: 
138: 	public MBEquipmentRoster DefaultBattleEquipmentRoster { get; private set; }
139: 
140: 	public MBEquipmentRoster DefaultCivilianEquipmentRoster { get; private set; }
141: 
142: 	public MBEquipmentRoster DefaultStealthEquipmentRoster { get; private set; }
143: 
144: 	public MBEquipmentRoster DuelPresetEquipmentRoster { get; private set; }
145: 
146: 	public MBEquipmentRoster MarriageBrideEquipmentRoster { get; private set; }
147: 
148: 	public CharacterObject BanditChief { get; private set; }
149: 
150: 	public CharacterObject BanditRaider { get; private set; }
151: 
152: 	public CharacterObject BanditBandit { get; private set; }
153: 
154: 	public CharacterObject BanditBoss { get; private set; }
155: 
156: 	public MBBodyProperty DefaultCharacterCreationBodyProperty { get; private set; }
157: 
158: 	public TextObject EncyclopediaText { get; private set; }
159: 
160: 	public CampaignVec2 StartingPoint { get; private set; }
161: 
162: 	public PartyTemplateObject DefaultPartyTemplate { get; private set; }
163: 
164: 	public PartyTemplateObject VillagerPartyTemplate { get; private set; }
165: 
166: 	public PartyTemplateObject FishingPartyTemplate { get; private set; }
167: 
168: 	public PartyTemplateObject MilitiaPartyTemplate { get; private set; }
169: 
170: 	public PartyTemplateObject RebelsPartyTemplate { get; private set; }
171: 
172: 	public MBList<PartyTemplateObject> CaravanPartyTemplates { get; private set; }
173: 
174: 	public MBList<PartyTemplateObject> EliteCaravanPartyTemplates { get; private set; }
175: 
176: 	public MBList<ShipHull> AvailableShipHulls { get; private set; }
177: 
178: 	public PartyTemplateObject BanditBossPartyTemplate { get; private set; }
179: 
180: 	public PartyTemplateObject VassalRewardTroopsPartyTemplate { get; private set; }
181: 
182: 	public PartyTemplateObject SettlementPatrolPartyTemplateWeak { get; private set; }
183: 
184: 	public PartyTemplateObject SettlementPatrolPartyTemplateModerate { get; private set; }
185: 
186: 	public PartyTemplateObject SettlementPatrolPartyTemplateStrong { get; private set; }
187: 
188: 	public PartyTemplateObject SettlementPatrolPartyTemplateNaval { get; private set; }
189: 
190: 	public MBReadOnlyList<ItemObject> VassalRewardItems { get; private set; }
191: 
192: 	public MBReadOnlyList<ItemObject> BannerBearerReplacementWeapons { get; private set; }
193: 
194: 	public MBReadOnlyList<TextObject> MaleNameList => _maleNameList;
195: 
196: 	public MBReadOnlyList<TextObject> FemaleNameList => _femaleNameList;
197: 
198: 	public MBReadOnlyList<TextObject> ClanNameList => _clanNameList;
199: 
200: 	public MBReadOnlyList<FeatObject> CultureFeats => _cultureFeats;
201: 
202: 	public MBReadOnlyList<PolicyObject> DefaultPolicyList => _defaultPolicyList;
203: 
204: 	public MBReadOnlyList<int> PossibleClanBannerIconsIDs { get; private set; }
205: 
206: 	public MBReadOnlyList<CharacterObject> NotableTemplates { get; private set; }
207: 
208: 	public MBReadOnlyList<CharacterObject> RebelliousHeroTemplates { get; private set; }
209: 
210: 	public MBReadOnlyList<CharacterObject> LordTemplates { get; private set; }
211: 
212: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForOneParticipant { get; private set; }
213: 
214: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForTwoParticipant { get; private set; }
215: 
216: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForFourParticipant { get; private set; }
217: 
218: 	public MBReadOnlyList<CharacterObject> BasicMercenaryTroops { get; private set; }
219: 
220: 	public int MilitiaBonus { get; set; }
221: 
222: 	public int ProsperityBonus { get; set; }
223: 
224: 	public BoardGameType BoardGame { get; private set; }
225: 
226: 	public float NavalFactor { get; private set; }
227: 
228: 	internal static void AutoGeneratedStaticCollectObjectsCultureObject(object o, List<object> collectedObjects)
229: 	{
230: 		((CultureObject)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
231: 	}
232: 
233: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
234: 	{
235: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
236: 	}
237: 
238: 	public bool HasTrait(CultureTrait trait)
239: 	{
240: 		return Enumerable.Contains(Traits, trait);
241: 	}
242: 
243: 	public bool HasFeat(FeatObject feat)
244: 	{
245: 		return _cultureFeats.Contains(feat);
246: 	}
247: 
248: 	public IEnumerable<FeatObject> GetCulturalFeats(Func<FeatObject, bool> predicate = null)
249: 	{
250: 		foreach (FeatObject cultureFeat in _cultureFeats)
251: 		{
252: 			if (predicate == null || predicate(cultureFeat))
253: 			{
254: 				yield return cultureFeat;
255: 			}
256: 		}
257: 	}
258: 
259: 	public override string ToString()
260: 	{
261: 		return base.Name.ToString();
262: 	}
263: 
264: 	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
265: 	{
266: 		base.Deserialize(objectManager, node);
267: 		MilitiaBonus = ((node.Attributes["militia_bonus"] != null) ? Convert.ToInt32(node.Attributes["militia_bonus"].Value) : 0);
268: 		ProsperityBonus = ((node.Attributes["prosperity_bonus"] != null) ? Convert.ToInt32(node.Attributes["prosperity_bonus"].Value) : 0);
269: 		NavalFactor = ((node.Attributes["naval_factor"] == null) ? 0f : Convert.ToSingle(node.Attributes["naval_factor"].Value));
270: 		DefaultPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("default_party_template", node);
271: 		VillagerPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("villager_party_template", node);
272: 		FishingPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("fishing_party_template", node);
273: 		MilitiaPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("militia_party_template", node);
274: 		RebelsPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("rebels_party_template", node);
275: 		BanditBossPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("bandit_boss_party_template", node);
276: 		VassalRewardTroopsPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("vassal_reward_party_template", node);
277: 		SettlementPatrolPartyTemplateWeak = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_1", node);
278: 		SettlementPatrolPartyTemplateModerate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_2", node);
279: 		SettlementPatrolPartyTemplateStrong = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_3", node);
280: 		SettlementPatrolPartyTemplateNaval = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_coastal", node);
281: 		EliteBasicTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("elite_basic_troop", node);
282: 		MeleeEliteMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("melee_elite_militia_troop", node);
283: 		RangedEliteMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_elite_militia_troop", node);
284: 		MeleeMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("melee_militia_troop", node);
285: 		RangedMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_militia_troop", node);
286: 		BasicTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_troop", node);
287: 		DefaultBattleEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("default_battle_equipment_roster", node);
288: 		DefaultCivilianEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("default_civilian_equipment_roster", node);
289: 		DefaultStealthEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("default_stealth_equipment_roster", node);
290: 		DuelPresetEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("duel_preset_equipment_roster", node);
291: 		MarriageBrideEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("marriage_bride_equipment_roster", node);
292: 		TournamentMaster = objectManager.ReadObjectReferenceFromXml<CharacterObject>("tournament_master", node);
293: 		Villager = objectManager.ReadObjectReferenceFromXml<CharacterObject>("villager", node);
294: 		CaravanMaster = objectManager.ReadObjectReferenceFromXml<CharacterObject>("caravan_master", node);
295: 		CaravanGuard = objectManager.ReadObjectReferenceFromXml<CharacterObject>("caravan_guard", node);
296: 		PrisonGuard = objectManager.ReadObjectReferenceFromXml<CharacterObject>("prison_guard", node);
297: 		Guard = objectManager.ReadObjectReferenceFromXml<CharacterObject>("guard", node);
298: 		Blacksmith = objectManager.ReadObjectReferenceFromXml<CharacterObject>("blacksmith", node);
299: 		Weaponsmith = objectManager.ReadObjectReferenceFromXml<CharacterObject>("weaponsmith", node);
300: 		Townswoman = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townswoman", node);
301: 		TownswomanInfant = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townswoman_infant", node);
302: 		TownswomanChild = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townswoman_child", node);
303: 		TownswomanTeenager = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townswoman_teenager", node);
304: 		Townsman = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townsman", node);
305: 		TownsmanInfant = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townsman_infant", node);
306: 		TownsmanChild = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townsman_child", node);
307: 		TownsmanTeenager = objectManager.ReadObjectReferenceFromXml<CharacterObject>("townsman_teenager", node);
308: 		VillageWoman = objectManager.ReadObjectReferenceFromXml<CharacterObject>("village_woman", node);
309: 		VillagerMaleChild = objectManager.ReadObjectReferenceFromXml<CharacterObject>("villager_male_child", node);
310: 		VillagerMaleTeenager = objectManager.ReadObjectReferenceFromXml<CharacterObject>("villager_male_teenager", node);

 succeeded in 1332ms:
260: 
261: 	public Settlement FactionMidSettlement => _midSettlement;
262: 
263: 	public CharacterObject BasicTroop
264: 	{
265: 		get
266: 		{
267: 			return _basicTroop ?? Culture.BasicTroop;
268: 		}
269: 		set
270: 		{
271: 			_basicTroop = value;
272: 		}
273: 	}
274: 
275: 	public static Clan PlayerClan => Campaign.Current.PlayerDefaultFaction;
276: 
277: 	public Hero Leader => _leader;
278: 
279: 	public int Gold => Leader?.Gold ?? 0;
280: 
281: 	public Banner Banner
282: 	{
283: 		get
284: 		{
285: 			if (Kingdom == null || Kingdom.RulingClan != this)
286: 			{
287: 				return _banner;
288: 			}
289: 			return Kingdom.Banner;
290: 		}
291: 		set
292: 		{
293: 			_banner = value;
294: 		}
295: 	}
296: 
297: 	public Banner ClanOriginalBanner => _banner;
298: 
299: 	[SaveableProperty(85)]
300: 	public bool IsBanditFaction { get; private set; }
301: 
302: 	bool IFaction.IsKingdomFaction => false;
303: 
304: 	public bool IsClan => true;
305: 
306: 	[SaveableProperty(88)]
307: 	public float Renown { get; set; }
308: 
309: 	[SaveableProperty(89)]
310: 	public float MainHeroCrimeRating { get; set; }
311: 
312: 	public float DailyCrimeRatingChange => Campaign.Current.Models.CrimeModel.GetDailyCrimeRatingChange(this).ResultNumber;
313: 
314: 	public ExplainedNumber DailyCrimeRatingChangeExplained => Campaign.Current.Models.CrimeModel.GetDailyCrimeRatingChange(this, includeDescriptions: true);
315: 
316: 	public int Tier
317: 	{
318: 		get
319: 		{
320: 			return _tier;
321: 		}
322: 		private set
323: 		{
324: 			int minClanTier = Campaign.Current.Models.ClanTierModel.MinClanTier;
325: 			int maxClanTier = Campaign.Current.Models.ClanTierModel.MaxClanTier;
326: 			if (value > maxClanTier)
327: 			{
328: 				value = maxClanTier;
329: 			}
330: 			else if (value < minClanTier)
730: 
731: 	public Clan()
732: 	{
733: 		InitMembers();
734: 		_isEliminated = false;
735: 		NotAttackableByPlayerUntilTime = CampaignTime.Zero;
736: 	}
737: 
738: 	public static Clan CreateClan(string stringID)
739: 	{
740: 		stringID = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Clan>(stringID);
741: 		Clan clan = new Clan();
742: 		clan.StringId = stringID;
743: 		Campaign.Current.CampaignObjectManager.AddClan(clan);
744: 		return clan;
745: 	}
746: 
747: 	protected override void PreAfterLoad()
748: 	{
749: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && IsBanditFaction && IsEliminated)
750: 		{
751: 			_isEliminated = false;
752: 		}
753: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.2") && base.StringId == "neutral")
754: 		{
755: 			foreach (Hero aliveHero in Campaign.Current.AliveHeroes)
756: 			{
757: 				if (aliveHero.Clan == this)
758: 				{
759: 					aliveHero.ResetClanForOldSave();
760: 					if (_aliveLordsCache.Contains(aliveHero))
761: 					{
762: 						_aliveLordsCache.Remove(aliveHero);
763: 					}
764: 					if (_heroesCache.Contains(aliveHero))
765: 					{
766: 						_heroesCache.Remove(aliveHero);
767: 					}
768: 				}
769: 			}
770: 			foreach (Hero deadOrDisabledHero in Campaign.Current.DeadOrDisabledHeroes)

 succeeded in 3513ms:

[32;1mPath                                                                                                             [0m[32;1m LineN[0m
[32;1m                                                                                                                 [0m[32;1m umber[0m
[32;1m----                                                                                                             [0m [32;1m-----[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs                                     157
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs                                582
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs                                   434
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs                                   439
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs                            552
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs                            570
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs                            573
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs                            609
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs   194
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs   999
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                  1212
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs            2011
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs            2197
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs      551
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs      591
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       300
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       424
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       430
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       438
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       444
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       592
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       594
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       738
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       741
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       749
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                       881
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                      1311
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs                      1341
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\FactionManager.cs             183
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\FactionManager.cs             214
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1674
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1682
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1876
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs                      1902
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\IFaction.cs                    54
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs                    173
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs                    179
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs             138
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Track.cs                      236
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeOwnerOfSettl…    77
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\KillCharacterActio…   133
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem.Barterables\S…    57
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem.Barterables\S…    68
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   215
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   499
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   590
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   620
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   636
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditIn…   650
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…    57
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…    67
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   121
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   132
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   134
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   152
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   184
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   187
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   204
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   242
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   248
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   278
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   280
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   312
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   314
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   317
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   319
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   325
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   438
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   440
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   449
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   560
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSp…   562
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Campaign…    17
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Campaign…    43
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Characte…    56
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…    47
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…   194
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…   212
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVari…   391
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Companio…   822
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCam…    30
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCam…    77
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandP…   203
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\FactionD…   121
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpaw…    48
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpaw…    57
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpaw…    63
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   469
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   508
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   575
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   610
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   615
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutC…   721
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Incident…  3088
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Incident…  3398
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Incident…  3400
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\InitialC…    26
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\InitialC…    60
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCa…    92
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCa…   101
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCa…   149
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\KingdomD…    69
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Notables…   233
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyHea…   109
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyUpg…   119
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PatrolPa…   362
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Politica…    92
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RansomOf…    66
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Rebellio…   455
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\Teleport…   122
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\TownSecu…    38
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\TownSecu…    72
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…    20
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…    33
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…   489
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…    38
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…    74
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…   134
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehavi…   531
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBe…    42
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBe…    43
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.CommentB…    21
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.CommentB…    18
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.CommentB…    14
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.CommentB…    19
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   239
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…    46
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   296
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   297
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   298
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   301
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   302
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaiman…   303
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   162
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   182
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   195
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   126
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   152
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   195
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   253
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   157
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   133
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\Default…   148
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   269
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   272
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   276
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   283
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   286
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAlle…   290
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBatt…   207
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBatt…   268
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultComb…    64
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultComb…   301
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultComb…   108
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1075
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1127
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDipl…  1171
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEnco…   317
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEnco…   364
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEnco…   452
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapT…    35
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMarr…    42
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…    90
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   146
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   151
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   199
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   368
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   463
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   543
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…   549
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…    37
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…    44
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobi…    91
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPart…   393
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPart…    91
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPart…   129
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPart…    78
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPris…    79
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSett…    59
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSett…   164
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\IncidentEffect.cs   693
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueB…   553
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCarav…   788
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCarav…  1262
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDeserter…   946
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDeserter…   958
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDeserter…   962
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccess…   334
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccess…   681
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccess…   688
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccess…   692
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccess…   995
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantArmyOfPoach…   812
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWi…   579
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWi…   596
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWi…   653
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\SmugglersIssueBehav…   777
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\ChangeSettlemen…   115
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…    38
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEnd…   149


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignObjectManager.cs'; "'$lines=Get-Content -Path $p; for($i=520;$i -le 620;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Clan.cs'; "'$lines=Get-Content -Path $p; for($i=400;$i -le 460;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=1300;$i -le 1350;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\ClanVariablesCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 230;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 858ms:
400: 
401: 	public MBReadOnlyList<IFaction> FactionsAtWarWith => _factionsAtWarWith;
402: 
403: 	public int RenownRequirementForNextTier => Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(Tier + 1);
404: 
405: 	public int CompanionLimit => Campaign.Current.Models.ClanTierModel.GetCompanionLimit(this);
406: 
407: 	public float DistanceToClosestNonAllyFortification
408: 	{
409: 		get
410: 		{
411: 			if (_distanceToClosestNonAllyFortificationCacheDirty)
412: 			{
413: 				_distanceToClosestNonAllyFortificationCache = FactionHelper.GetDistanceToClosestNonAllyFortificationOfFaction(this);
414: 				_distanceToClosestNonAllyFortificationCacheDirty = false;
415: 			}
416: 			return _distanceToClosestNonAllyFortificationCache;
417: 		}
418: 	}
419: 
420: 	public int WarPartyLimit => Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(this, Tier);
421: 
422: 	public static MBReadOnlyList<Clan> All => Campaign.Current.Clans;
423: 
424: 	public static IEnumerable<Clan> NonBanditFactions
425: 	{
426: 		get
427: 		{
428: 			foreach (Clan clan in Campaign.Current.Clans)
429: 			{
430: 				if (!clan.IsBanditFaction)
431: 				{
432: 					yield return clan;
433: 				}
434: 			}
435: 		}
436: 	}
437: 
438: 	public static IEnumerable<Clan> BanditFactions
439: 	{
440: 		get
441: 		{
442: 			foreach (Clan clan in Campaign.Current.Clans)
443: 			{
444: 				if (clan.IsBanditFaction)
445: 				{
446: 					yield return clan;
447: 				}
448: 			}
449: 		}
450: 	}
451: 
452: 	internal static void AutoGeneratedStaticCollectObjectsClan(object o, List<object> collectedObjects)
453: 	{
454: 		((Clan)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
455: 	}
456: 
457: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
458: 	{
459: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
460: 		collectedObjects.Add(_defaultPartyTemplate);
1300: 			int relationValueOfSupportingClan = diplomacyModel.GetRelationValueOfSupportingClan();
1301: 			ChangeClanInfluenceAction.Apply(supporterClan, -influenceCostOfSupportingClan);
1302: 			ChangeClanInfluenceAction.Apply(this, influenceValueOfSupportingClan);
1303: 			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(supporterClan.Leader, Leader, relationValueOfSupportingClan);
1304: 		}
1305: 	}
1306: 
1307: 	public static Clan CreateSettlementRebelClan(Settlement settlement, Hero owner, int iconMeshId = -1)
1308: 	{
1309: 		TextObject textObject = new TextObject("{=2LIV2cy7}{SETTLEMENT}{.o} rebels");
1310: 		textObject.SetTextVariable("SETTLEMENT", settlement.Name);
1311: 		Clan clan = CreateClan(settlement.StringId + "_rebel_clan");
1312: 		clan.ChangeClanName(textObject, textObject);
1313: 		clan.Culture = settlement.Culture;
1314: 		clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), iconMeshId);
1315: 		clan.SetInitialHomeSettlement(settlement);
1316: 		clan.SetLeader(owner);
1317: 		clan.Color = settlement.MapFaction.Color2;
1318: 		clan.Color2 = settlement.MapFaction.Color;
1319: 		clan.Tier = Campaign.Current.Models.ClanTierModel.RebelClanStartingTier;
1320: 		clan.BannerBackgroundColorPrimary = settlement.MapFaction.Banner.GetFirstIconColor();
1321: 		clan.BannerBackgroundColorSecondary = settlement.MapFaction.Banner.GetFirstIconColor();
1322: 		clan.BannerIconColor = settlement.MapFaction.Banner.GetPrimaryColor();
1323: 		clan._distanceToClosestNonAllyFortificationCacheDirty = true;
1324: 		clan.HomeSettlement = settlement;
1325: 		clan.IsRebelClan = true;
1326: 		clan.CalculateMidSettlement();
1327: 		CampaignEventDispatcher.Instance.OnClanCreated(clan, isCompanion: false);
1328: 		return clan;
1329: 	}
1330: 
1331: 	public void CalculateMidSettlement()
1332: 	{
1333: 		if (Campaign.Current.MapSceneWrapper != null)
1334: 		{
1335: 			_midSettlement = FactionHelper.GetMidSettlementOfFaction(this);
1336: 		}
1337: 	}
1338: 
1339: 	public static Clan CreateCompanionToLordClan(Hero hero, Settlement settlement, TextObject clanName, int newClanIconId)
1340: 	{
1341: 		Clan clan = CreateClan(Hero.MainHero.MapFaction.StringId + "_companion_clan");
1342: 		clan.ChangeClanName(clanName, clanName);
1343: 		clan.Culture = settlement.Culture;
1344: 		clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), newClanIconId);
1345: 		clan.Kingdom = Hero.MainHero.Clan.Kingdom;
1346: 		clan.Tier = Campaign.Current.Models.ClanTierModel.CompanionToLordClanStartingTier;
1347: 		clan.SetInitialHomeSettlement(settlement);
1348: 		hero.Clan = clan;
1349: 		clan.SetLeader(hero);
1350: 		clan.IsNoble = true;

 succeeded in 864ms:
520: 	internal void AddClan(Clan clan)
521: 	{
522: 		clan.Id = new MBGUID(18u, Campaign.Current.CampaignObjectManager.GetNextUniqueObjectIdOfType<Clan>());
523: 		_clans.Add(clan);
524: 		OnItemAdded(CampaignObjects.Clans, clan);
525: 		_factions.Add(clan);
526: 	}
527: 
528: 	internal void RemoveClan(Clan clan)
529: 	{
530: 		if (_clans.Contains(clan))
531: 		{
532: 			_clans.Remove(clan);
533: 			OnItemRemoved(CampaignObjects.Clans, clan);
534: 		}
535: 		if (_factions.Contains(clan))
536: 		{
537: 			_factions.Remove(clan);
538: 		}
539: 	}
540: 
541: 	internal void AddKingdom(Kingdom kingdom)
542: 	{
543: 		kingdom.Id = new MBGUID(20u, Campaign.Current.CampaignObjectManager.GetNextUniqueObjectIdOfType<Kingdom>());
544: 		_kingdoms.Add(kingdom);
545: 		OnItemAdded(CampaignObjects.Kingdoms, kingdom);
546: 		_factions.Add(kingdom);
547: 	}
548: 
549: 	private void AddPartyToAppropriateList(MobileParty party)
550: 	{
551: 		if (party.IsBandit)
552: 		{
553: 			_banditParties.Add(party);
554: 		}
555: 		else if (party.IsCaravan)
556: 		{
557: 			_caravanParties.Add(party);
558: 		}
559: 		else if (party.IsPatrolParty)
560: 		{
561: 			_patrolParties.Add(party);
562: 		}
563: 		else if (party.IsLordParty)
564: 		{
565: 			_lordParties.Add(party);
566: 		}
567: 		else if (party.IsMilitia)
568: 		{
569: 			_militiaParties.Add(party);
570: 		}
571: 		else if (party.IsVillager)
572: 		{
573: 			_villagerParties.Add(party);
574: 		}
575: 		else if (party.IsCustomParty)
576: 		{
577: 			_customParties.Add(party);
578: 		}
579: 		else if (party.IsGarrison)
580: 		{
581: 			_garrisonParties.Add(party);
582: 		}
583: 		else
584: 		{
585: 			_partiesWithoutPartyComponent.Add(party);
586: 		}
587: 	}
588: 
589: 	private void RemovePartyFromAppropriateList(MobileParty party)
590: 	{
591: 		if (party.IsBandit)
592: 		{
593: 			_banditParties.Remove(party);
594: 		}
595: 		else if (party.IsCaravan)
596: 		{
597: 			_caravanParties.Remove(party);
598: 		}
599: 		else if (party.IsPatrolParty)
600: 		{
601: 			_patrolParties.Remove(party);
602: 		}
603: 		else if (party.IsLordParty)
604: 		{
605: 			_lordParties.Remove(party);
606: 		}
607: 		else if (party.IsMilitia)
608: 		{
609: 			_militiaParties.Remove(party);
610: 		}
611: 		else if (party.IsVillager)
612: 		{
613: 			_villagerParties.Remove(party);
614: 		}
615: 		else if (party.IsCustomParty)
616: 		{
617: 			_customParties.Remove(party);
618: 		}
619: 		else if (party.IsGarrison)
620: 		{

 succeeded in 1251ms:
1: using System;
2: using System.Collections.Generic;
3: using System.Linq;
4: using Helpers;
5: using TaleWorlds.CampaignSystem.Actions;
6: using TaleWorlds.CampaignSystem.Election;
7: using TaleWorlds.CampaignSystem.Party;
8: using TaleWorlds.CampaignSystem.Party.PartyComponents;
9: using TaleWorlds.CampaignSystem.Settlements;
10: using TaleWorlds.Core;
11: using TaleWorlds.Library;
12: using TaleWorlds.LinQuick;
13: using TaleWorlds.Localization;
14: 
15: namespace TaleWorlds.CampaignSystem.CampaignBehaviors;
16: 
17: public class ClanVariablesCampaignBehavior : CampaignBehaviorBase
18: {
19: 	public override void RegisterEvents()
20: 	{
21: 		CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, DailyTickClan);
22: 		CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, DailyTickHero);
23: 		CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
24: 		CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
25: 		CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClan);
26: 		CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
27: 		CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedEnd);
28: 		CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
29: 		CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
30: 		CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
31: 		CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, WeeklyTickClan);
32: 	}
33: 
34: 	private void OnNewGameCreatedEnd(CampaignGameStarter starter)
35: 	{
36: 		foreach (Clan item in Clan.All)
37: 		{
38: 			if (item != Clan.PlayerClan)
39: 			{
40: 				UpdateClanSettlementsPaymentLimit(item);
41: 			}
42: 		}
43: 	}
44: 
45: 	private void WeeklyTickClan()
46: 	{
47: 		foreach (Clan nonBanditFaction in Clan.NonBanditFactions)
48: 		{
49: 			nonBanditFaction.ConsiderAndUpdateHomeSettlement();
50: 		}
51: 	}
52: 
53: 	public override void SyncData(IDataStore dataStore)
54: 	{
55: 	}
56: 
57: 	public void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
58: 	{
59: 		if (!settlement.IsFortification)
60: 		{
61: 			return;
62: 		}
63: 		newOwner.Clan.ConsiderAndUpdateHomeSettlement();
64: 		foreach (Hero hero in newOwner.Clan.Heroes)
65: 		{
66: 			hero.UpdateHomeSettlement();
67: 		}
68: 		oldOwner.Clan.ConsiderAndUpdateHomeSettlement();
69: 		foreach (Hero hero2 in oldOwner.Clan.Heroes)
70: 		{
71: 			hero2.UpdateHomeSettlement();
72: 		}
73: 		settlement.SetGarrisonWagePaymentLimit(Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
74: 		if (!oldOwner.Clan.MapFaction.IsKingdomFaction)
75: 		{
76: 			return;
77: 		}
78: 		foreach (Clan clan in oldOwner.Clan.Kingdom.Clans)
79: 		{
80: 			if (clan == oldOwner.Clan || clan == newOwner.Clan || clan.HomeSettlement != settlement)
81: 			{
82: 				continue;
83: 			}
84: 			clan.ConsiderAndUpdateHomeSettlement();
85: 			foreach (Hero hero3 in clan.Heroes)
86: 			{
87: 				hero3.UpdateHomeSettlement();
88: 			}
89: 		}
90: 	}
91: 
92: 	private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
93: 	{
94: 		clan.ConsiderAndUpdateHomeSettlement();
95: 		foreach (Settlement settlement in clan.Settlements)
96: 		{
97: 			foreach (Clan item in Clan.All)
98: 			{
99: 				if (clan != item && item.HomeSettlement == settlement)
100: 				{
101: 					item.ConsiderAndUpdateHomeSettlement();
102: 				}
103: 			}
104: 		}
105: 	}
106: 
107: 	private void OnHeroChangedClan(Hero hero, Clan oldClan)
108: 	{
109: 		if (oldClan != null && oldClan.Leader == hero && hero.Clan != oldClan)
110: 		{
111: 			ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(oldClan);
112: 		}
113: 	}
114: 
115: 	private void UpdateGovernorsOfClan(Clan clan)
116: 	{
117: 		List<Tuple<Town, float>> list = new List<Tuple<Town, float>>();
118: 		foreach (Town fief in clan.Fiefs)
119: 		{
120: 			float num = 0f;
121: 			num += (float)((!fief.IsTown) ? 1 : 3);
122: 			num += TaleWorlds.Library.MathF.Sqrt(fief.Prosperity / 1000f);
123: 			num += (float)fief.Settlement.BoundVillages.Count;
124: 			num *= ((clan.Culture == fief.Settlement.Culture) ? 1f : 0.5f);
125: 			float num2 = (clan.Leader.MapFaction.IsKingdomFaction ? Campaign.Current.Models.MapDistanceModel.GetDistance(fief.Settlement, clan.Leader.MapFaction.FactionMidSettlement, isFromPort: false, isTargetingPort: false, MobileParty.NavigationType.All) : 100f);
126: 			num *= 1f - TaleWorlds.Library.MathF.Sqrt(num2 / Campaign.Current.Models.MapDistanceModel.GetMaximumDistanceBetweenTwoConnectedSettlements(MobileParty.NavigationType.Default));
127: 			list.Add(new Tuple<Town, float>(fief, num));
128: 		}
129: 		List<Hero> list2 = new List<Hero>();
130: 		for (int i = 0; i < clan.Fiefs.Count; i++)
131: 		{
132: 			Tuple<Town, float> tuple = null;
133: 			float num3 = 0f;
134: 			foreach (Tuple<Town, float> item in list)
135: 			{
136: 				if (item.Item2 > num3)
137: 				{
138: 					num3 = item.Item2;
139: 					tuple = item;
140: 				}
141: 			}
142: 			if (!(num3 > 0.01f))
143: 			{
144: 				continue;
145: 			}
146: 			list.Remove(tuple);
147: 			float num4 = 0f;
148: 			Hero hero = null;
149: 			foreach (Hero aliveLord in clan.AliveLords)
150: 			{
151: 				if (Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(aliveLord) && aliveLord.PartyBelongedTo == null && aliveLord.Clan != Clan.PlayerClan && !list2.Contains(aliveLord))
152: 				{
153: 					float num5 = ((tuple.Item1.Governor == aliveLord) ? 1f : 0.75f) * Campaign.Current.Models.DiplomacyModel.GetHeroGoverningStrengthForClan(aliveLord);
154: 					if (num5 > num4)
155: 					{
156: 						num4 = num5;
157: 						hero = aliveLord;
158: 					}
159: 				}
160: 			}
161: 			if (hero == null)
162: 			{
163: 				continue;
164: 			}
165: 			if (tuple.Item1.Governor != hero)
166: 			{
167: 				if (hero.GovernorOf != null)
168: 				{
169: 					ChangeGovernorAction.RemoveGovernorOf(hero);
170: 				}
171: 				ChangeGovernorAction.Apply(tuple.Item1, hero);
172: 			}
173: 			list2.Add(hero);
174: 		}
175: 	}
176: 
177: 	public void OnNewGameCreated(CampaignGameStarter starter)
178: 	{
179: 		foreach (Kingdom item in Kingdom.All)
180: 		{
181: 			item.CalculateMidSettlement();
182: 		}
183: 		foreach (Clan item2 in Clan.All)
184: 		{
185: 			item2.ConsiderAndUpdateHomeSettlement();
186: 			if (item2 != Clan.PlayerClan && item2.Leader != null && item2.Leader.MapFaction != null && item2.Leader.MapFaction.IsKingdomFaction && item2.Renown > 0f)
187: 			{
188: 				ChangeClanInfluenceAction.Apply(item2, Campaign.Current.Models.ClanTierModel.CalculateInitialInfluence(item2));
189: 			}
190: 			item2.LastFactionChangeTime = CampaignTime.Now;
191: 			item2.CalculateMidSettlement();
192: 		}
193: 		DetermineBasicTroopsForMinorFactions();
194: 		foreach (Clan nonBanditFaction in Clan.NonBanditFactions)
195: 		{
196: 			UpdateGovernorsOfClan(nonBanditFaction);
197: 			if (nonBanditFaction.Kingdom != null && nonBanditFaction.Leader == nonBanditFaction.Kingdom.Leader)
198: 			{
199: 				nonBanditFaction.Kingdom.KingdomBudgetWallet = 2000000;
200: 			}
201: 		}
202: 	}
203: 
204: 	private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
205: 	{
206: 		if (!MBSaveLoad.IsUpdatingGameVersion || !(MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("e1.8.0")))
207: 		{
208: 			return;
209: 		}
210: 		foreach (Clan item in Clan.All)
211: 		{
212: 			if (item == Clan.PlayerClan || item.IsBanditFaction || item.Leader.IsAlive)
213: 			{
214: 				continue;
215: 			}
216: 			if (!item.IsEliminated)
217: 			{
218: 				ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(item);
219: 				if (!item.Leader.IsAlive)
220: 				{
221: 					DestroyClanAction.Apply(item);
222: 				}
223: 			}
224: 			else
225: 			{
226: 				if (item.Settlements.Count <= 0)
227: 				{
228: 					continue;
229: 				}
230: 				Clan clan = FactionHelper.ChooseHeirClanForFiefs(item);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem,E:\\Decompiled_Bannerlord\\Modules\\SandBox -Recurse -Filter *.cs | Select-String -Pattern \"CreateClan\\(|SetAsBandit|IsBanditFaction =|InitializeClan\\(|DefaultPartyTemplate\" | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1976ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:198: obj14.GetFieldDefinitionWithId(new MemberTypeId(3, 54)).InitializeForAutoGeneration(Clan.AutoGeneratedGetMemberValue_defaultPartyTemplate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:1218: foreach (PartyTemplateStack stack in objectType.DefaultPartyTemplate.Stacks)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2011: Clan clan = Clan.CreateClan("test_clan_" + Clan.All.Count);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:24: private PartyTemplateObject _defaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:112: public PartyTemplateObject DefaultPartyTemplate
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:116: if (_defaultPartyTemplate != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:118: return _defaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:120: return Culture.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:124: public bool HasNavalNavigationCapability => DefaultPartyTemplate.ShipHulls.Count > 0;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:460: collectedObjects.Add(_defaultPartyTemplate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:612: internal static object AutoGeneratedGetMemberValue_defaultPartyTemplate(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:614: return ((Clan)o)._defaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:738: public static Clan CreateClan(string stringID)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:881: IsBanditFaction = node.Attributes["is_bandit"] != null && Convert.ToBoolean(node.Attributes["is_bandit"].Value);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:888: _defaultPartyTemplate = (PartyTemplateObject)objectManager.ReadObjectReferenceFromXml("default_party_template", typeof(PartyTemplateObject), node);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1311: Clan clan = CreateClan(settlement.StringId + "_rebel_clan");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1341: Clan clan = CreateClan(Hero.MainHero.MapFaction.StringId + "_companion_clan");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs:162: public PartyTemplateObject DefaultPartyTemplate { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs:270: DefaultPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("default_party_template", node);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:125: public bool HasNavalNavigationCapability => Culture.DefaultPartyTemplate.ShipHulls.Any();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:324: PartyTemplateObject pt = overridenPartyTemplate ?? clan.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:462: MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(selectedFaction.StringId + "_1", selectedFaction, settlement, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:471: MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(selectedFaction.StringId + "_1", selectedFaction, hideout, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:498: PartyTemplateObject defaultPartyTemplate = item.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:500: foreach (PartyTemplateStack stack in defaultPartyTemplate.Stacks)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:213: PartyTemplateObject defaultPartyTemplate = fortification.Culture.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:220: for (int j = 0; j < defaultPartyTemplate.Stacks.Count; j++)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:222: num3 += (defaultPartyTemplate.Stacks[j].Character.IsRanged ? 6f : ((!defaultPartyTemplate.Stacks[j].Character.IsMounted) ? 2f : 1f)) * ((float)(defaultPartyTemplate.Stacks[j].MaxValue + defaultPartyTemplate.Stacks[j].MinValue) / 2f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:225: for (int k = 0; k < defaultPartyTemplate.Stacks.Count; k++)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:227: num4 -= (defaultPartyTemplate.Stacks[k].Character.IsRanged ? 6f : ((!defaultPartyTemplate.Stacks[k].Character.IsMounted) ? 2f : 1f)) * ((float)(defaultPartyTemplate.Stacks[k].MaxValue + defaultPartyTemplate.Stacks[k].MinValue) / 2f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:234: CharacterObject character = defaultPartyTemplate.Stacks[index].Character;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:265: PartyTemplateObject defaultPartyTemplate = mobileParty.LordPartyComponent.Owner.Clan.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:267: foreach (PartyTemplateStack stack in defaultPartyTemplate.Stacks)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:554: PartyTemplateObject partyTemplateObject = Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>("kingdom_hero_party_caravan_ambushers") ?? clan.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1263: PartyTemplateObject partyTemplateObject = Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>("kingdom_hero_party_caravan_ambushers") ?? clan.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:968: PartyTemplateObject defaultPartyTemplate = QuestSettlement.Culture.DefaultPartyTemplate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:969: MobilePartyHelper.FillPartyManuallyAfterCreation(_deserterMobileParty, defaultPartyTemplate, DeserterPartyMenCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:206: _defeatedSidePartyIsBanditFaction = leaderParty2.MapFaction.IsBanditFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\LordPartyComponent.cs:38: PartyTemplateObject pt = (owner.Clan.IsRebelClan ? owner.Clan.Culture.RebelsPartyTemplate : owner.Clan.DefaultPartyTemplate);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Clan.cs'; "'$lines=Get-Content -Path $p; for($i=100;$i -le 130;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=850;$i -le 900;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core\\TaleWorlds.Core\\BasicCultureObject.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 110;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern 'is_bandit=\"true\"|dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs|forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 300' in C:\Users\mikew\source\repos\TAOM
 succeeded in 878ms:
100: 	[SaveableProperty(51)]
101: 	public TextObject Name { get; private set; }
102: 
103: 	[SaveableProperty(52)]
104: 	public TextObject InformalName { get; private set; }
105: 
106: 	[SaveableProperty(53)]
107: 	public CultureObject Culture { get; set; }
108: 
109: 	[SaveableProperty(55)]
110: 	public CampaignTime LastFactionChangeTime { get; set; }
111: 
112: 	public PartyTemplateObject DefaultPartyTemplate
113: 	{
114: 		get
115: 		{
116: 			if (_defaultPartyTemplate != null)
117: 			{
118: 				return _defaultPartyTemplate;
119: 			}
120: 			return Culture.DefaultPartyTemplate;
121: 		}
122: 	}
123: 
124: 	public bool HasNavalNavigationCapability => DefaultPartyTemplate.ShipHulls.Count > 0;
125: 
126: 	[SaveableProperty(58)]
127: 	public int AutoRecruitmentExpenses { get; set; }
128: 
129: 	[SaveableProperty(56)]
130: 	public TextObject EncyclopediaText { get; private set; }
850: 				else
851: 				{
852: 					DestroyKingdomAction.Apply(kingdom);
853: 				}
854: 			}
855: 		}
856: 		CalculateMidSettlement();
857: 	}
858: 
859: 	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
860: 	{
861: 		base.Deserialize(objectManager, node);
862: 		SetLeader(objectManager.ReadObjectReferenceFromXml("owner", typeof(Hero), node) as Hero);
863: 		Kingdom = (Kingdom)objectManager.ReadObjectReferenceFromXml("super_faction", typeof(Kingdom), node);
864: 		Tier = ((node.Attributes["tier"] == null) ? 1 : Convert.ToInt32(node.Attributes["tier"].Value));
865: 		Renown = Campaign.Current.Models.ClanTierModel.CalculateInitialRenown(this);
866: 		if (node.Attributes["initial_home_settlement"] != null)
867: 		{
868: 			Settlement initialHomeSettlement = (Settlement)objectManager.ReadObjectReferenceFromXml("initial_home_settlement", typeof(Settlement), node);
869: 			SetInitialHomeSettlement(initialHomeSettlement);
870: 		}
871: 		ChangeClanName(new TextObject(node.Attributes["name"].Value), (node.Attributes["short_name"] != null) ? new TextObject(node.Attributes["short_name"].Value) : new TextObject(node.Attributes["name"].Value));
872: 		Culture = (CultureObject)objectManager.ReadObjectReferenceFromXml("culture", typeof(CultureObject), node);
873: 		Banner = null;
874: 		XmlNode xmlNode = node.Attributes["is_noble"];
875: 		if (xmlNode != null)
876: 		{
877: 			IsNoble = Convert.ToBoolean(xmlNode.InnerText);
878: 		}
879: 		Color = ((node.Attributes["color"] == null) ? 4291609515u : Convert.ToUInt32(node.Attributes["color"].Value, 16));
880: 		Color2 = ((node.Attributes["color2"] == null) ? 4291609515u : Convert.ToUInt32(node.Attributes["color2"].Value, 16));
881: 		IsBanditFaction = node.Attributes["is_bandit"] != null && Convert.ToBoolean(node.Attributes["is_bandit"].Value);
882: 		IsMinorFaction = node.Attributes["is_minor_faction"] != null && Convert.ToBoolean(node.Attributes["is_minor_faction"].Value);
883: 		IsOutlaw = node.Attributes["is_outlaw"] != null && Convert.ToBoolean(node.Attributes["is_outlaw"].Value);
884: 		IsSect = node.Attributes["is_sect"] != null && Convert.ToBoolean(node.Attributes["is_sect"].Value);
885: 		IsMafia = node.Attributes["is_mafia"] != null && Convert.ToBoolean(node.Attributes["is_mafia"].Value);
886: 		IsClanTypeMercenary = node.Attributes["is_clan_type_mercenary"] != null && Convert.ToBoolean(node.Attributes["is_clan_type_mercenary"].Value);
887: 		IsNomad = node.Attributes["is_nomad"] != null && Convert.ToBoolean(node.Attributes["is_nomad"].Value);
888: 		_defaultPartyTemplate = (PartyTemplateObject)objectManager.ReadObjectReferenceFromXml("default_party_template", typeof(PartyTemplateObject), node);
889: 		EncyclopediaText = ((node.Attributes["text"] != null) ? new TextObject(node.Attributes["text"].Value) : TextObject.GetEmpty());
890: 		if (node.Attributes["banner_key"] != null)
891: 		{
892: 			_banner = new Banner();
893: 			_banner.Deserialize(node.Attributes["banner_key"].Value);
894: 		}
895: 		else
896: 		{
897: 			_banner = Banner.CreateRandomClanBanner(base.StringId.GetDeterministicHashCode());
898: 		}
899: 		BannerBackgroundColorPrimary = _banner.GetPrimaryColor();
900: 		BannerBackgroundColorSecondary = _banner.GetSecondaryColor();

 succeeded in 920ms:
1: using System;
2: using System.Xml;
3: using TaleWorlds.Localization;
4: using TaleWorlds.ObjectSystem;
5: 
6: namespace TaleWorlds.Core;
7: 
8: public class BasicCultureObject : MBObjectBase
9: {
10: 	public TextObject Name { get; private set; }
11: 
12: 	public bool IsMainCulture { get; private set; }
13: 
14: 	public bool IsBandit { get; private set; }
15: 
16: 	public bool CanHaveSettlement { get; private set; }
17: 
18: 	public uint Color { get; private set; }
19: 
20: 	public uint Color2 { get; private set; }
21: 
22: 	public uint ClothAlternativeColor { get; private set; }
23: 
24: 	public uint ClothAlternativeColor2 { get; private set; }
25: 
26: 	public uint BackgroundColor1 { get; private set; }
27: 
28: 	public uint ForegroundColor1 { get; private set; }
29: 
30: 	public uint BackgroundColor2 { get; private set; }
31: 
32: 	public uint ForegroundColor2 { get; private set; }
33: 
34: 	public string EncounterBackgroundMesh { get; set; }
35: 
36: 	public Banner Banner { get; private set; }
37: 
38: 	public override string ToString()
39: 	{
40: 		return Name.ToString();
41: 	}
42: 
43: 	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
44: 	{
45: 		base.Deserialize(objectManager, node);
46: 		Name = new TextObject(node.Attributes["name"].Value);
47: 		Color = ((node.Attributes["color"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["color"].Value, 16));
48: 		Color2 = ((node.Attributes["color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["color2"].Value, 16));
49: 		ClothAlternativeColor = ((node.Attributes["cloth_alternative_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["cloth_alternative_color1"].Value, 16));
50: 		ClothAlternativeColor2 = ((node.Attributes["cloth_alternative_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["cloth_alternative_color2"].Value, 16));
51: 		BackgroundColor1 = ((node.Attributes["banner_background_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_background_color1"].Value, 16));
52: 		ForegroundColor1 = ((node.Attributes["banner_foreground_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_foreground_color1"].Value, 16));
53: 		BackgroundColor2 = ((node.Attributes["banner_background_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_background_color2"].Value, 16));
54: 		ForegroundColor2 = ((node.Attributes["banner_foreground_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_foreground_color2"].Value, 16));
55: 		IsMainCulture = node.Attributes["is_main_culture"] != null && Convert.ToBoolean(node.Attributes["is_main_culture"].Value);
56: 		EncounterBackgroundMesh = ((node.Attributes["encounter_background_mesh"] == null) ? null : node.Attributes["encounter_background_mesh"].Value);
57: 		Banner = ((node.Attributes["faction_banner_key"] == null) ? new Banner() : new Banner(node.Attributes["faction_banner_key"].Value));
58: 		IsBandit = false;
59: 		IsBandit = node.Attributes["is_bandit"] != null && Convert.ToBoolean(node.Attributes["is_bandit"].Value);
60: 		CanHaveSettlement = false;
61: 		CanHaveSettlement = node.Attributes["can_have_settlement"] != null && Convert.ToBoolean(node.Attributes["can_have_settlement"].Value);
62: 	}
63: }
64: 
65: 
66: 
67: 
68: 
69: 
70: 
71: 
72: 
73: 
74: 
75: 
76: 
77: 
78: 
79: 
80: 
81: 
82: 
83: 
84: 
85: 
86: 
87: 
88: 
89: 
90: 
91: 
92: 
93: 
94: 
95: 
96: 
97: 
98: 
99: 
100: 
101: 
102: 
103: 
104: 
105: 
106: 
107: 
108: 
109: 
110: 

 succeeded in 2771ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13084: <Settlement id="hideout_forest_1" name="{=Settlements.Settlement.name.hideout_forest_1}Hideout" type="Hideout" posX="770.748" posY="880.545" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13092: <Settlement id="hideout_forest_2" name="{=Settlements.Settlement.name.hideout_forest_2}Hideout" type="Hideout" posX="747.613" posY="856.493" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13100: <Settlement id="hideout_forest_3" name="{=Settlements.Settlement.name.hideout_forest_3}Hideout" type="Hideout" posX="776.185" posY="831.114" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13108: <Settlement id="hideout_forest_4" name="{=Settlements.Settlement.name.hideout_forest_4}Hideout" type="Hideout" posX="773.978" posY="806.852" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13116: <Settlement id="hideout_forest_5" name="{=Settlements.Settlement.name.hideout_forest_5}Hideout" type="Hideout" posX="773.043" posY="776.114" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13124: <Settlement id="hideout_forest_6" name="{=Settlements.Settlement.name.hideout_forest_6}Hideout" type="Hideout" posX="800.069" posY="722.118" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13132: <Settlement id="hideout_forest_7" name="{=Settlements.Settlement.name.hideout_forest_7}Hideout" type="Hideout" posX="724.227" posY="794.886" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13140: <Settlement id="hideout_forest_8" name="{=Settlements.Settlement.name.hideout_forest_8}Hideout" type="Hideout" posX="678.835" posY="816.911" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13148: <Settlement id="hideout_forest_9" name="{=Settlements.Settlement.name.hideout_forest_9}Hideout" type="Hideout" posX="738.98" posY="816.841" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13156: <Settlement id="hideout_forest_10" name="{=Settlements.Settlement.name.hideout_forest_10}Hideout" type="Hideout" posX="705.923" posY="767.518" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13164: <Settlement id="hideout_forest_11" name="{=Settlements.Settlement.name.hideout_forest_11}Hideout" type="Hideout" posX="617.601" posY="799.329" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13172: <Settlement id="hideout_forest_12" name="{=Settlements.Settlement.name.hideout_forest_12}Hideout" type="Hideout" posX="656.126" posY="815.868" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13180: <Settlement id="hideout_forest_13" name="{=Settlements.Settlement.name.hideout_forest_13}Hideout" type="Hideout" posX="573.135" posY="786.294" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13188: <Settlement id="hideout_forest_14" name="{=Settlements.Settlement.name.hideout_forest_14}Hideout" type="Hideout" posX="572.798" posY="818.379" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13196: <Settlement id="hideout_forest_15" name="{=Settlements.Settlement.name.hideout_forest_15}Hideout" type="Hideout" posX="547.914" posY="818.905" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13204: <Settlement id="hideout_forest_16" name="{=Settlements.Settlement.name.hideout_forest_16}Hideout" type="Hideout" posX="582.673" posY="842.145" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13212: <Settlement id="hideout_forest_17" name="{=Settlements.Settlement.name.hideout_forest_17}Hideout" type="Hideout" posX="591.523" posY="883.228" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13220: <Settlement id="hideout_forest_18" name="{=Settlements.Settlement.name.hideout_forest_18}Hideout" type="Hideout" posX="615.811" posY="923.319" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13228: <Settlement id="hideout_forest_19" name="{=Settlements.Settlement.name.hideout_forest_19}Hideout" type="Hideout" posX="628.332" posY="948.494" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13236: <Settlement id="hideout_seaside_1" name="{=Settlements.Settlement.name.hideout_seaside_1}Hideout" type="Hideout" posX="711.521" posY="555.527" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13244: <Settlement id="hideout_seaside_2" name="{=Settlements.Settlement.name.hideout_seaside_2}Hideout" type="Hideout" posX="766.938" posY="949.116" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13252: <Settlement id="hideout_seaside_3" name="{=Settlements.Settlement.name.hideout_seaside_3}Hideout" type="Hideout" posX="799.676" posY="1004.181" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13260: <Settlement id="hideout_seaside_4" name="{=Settlements.Settlement.name.hideout_seaside_4}Hideout" type="Hideout" posX="799.3" posY="1114.713" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13268: <Settlement id="hideout_seaside_5" name="{=Settlements.Settlement.name.hideout_seaside_5}Hideout" type="Hideout" posX="791.061" posY="1132.36" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13276: <Settlement id="hideout_seaside_6" name="{=Settlements.Settlement.name.hideout_seaside_6}Hideout" type="Hideout" posX="780.086" posY="1172.637" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13284: <Settlement id="hideout_seaside_7" name="{=Settlements.Settlement.name.hideout_seaside_7}Hideout" type="Hideout" posX="763.182" posY="1227.824" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13292: <Settlement id="hideout_seaside_8" name="{=Settlements.Settlement.name.hideout_seaside_8}Hideout" type="Hideout" posX="827.085" posY="1212.69" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13300: <Settlement id="hideout_seaside_9" name="{=Settlements.Settlement.name.hideout_seaside_9}Hideout" type="Hideout" posX="851.905" posY="1198.957" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13308: <Settlement id="hideout_seaside_10" name="{=Settlements.Settlement.name.hideout_seaside_10}Hideout" type="Hideout" posX="919.256" posY="1189.674" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13316: <Settlement id="hideout_seaside_11" name="{=Settlements.Settlement.name.hideout_seaside_11}Hideout" type="Hideout" posX="926.778" posY="1167.689" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13324: <Settlement id="hideout_seaside_12" name="{=Settlements.Settlement.name.hideout_seaside_12}Hideout" type="Hideout" posX="987.868" posY="1085.954" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13332: <Settlement id="hideout_seaside_13" name="{=Settlements.Settlement.name.hideout_seaside_13}Hideout" type="Hideout" posX="775.268" posY="965.898" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13340: <Settlement id="hideout_seaside_14" name="{=Settlements.Settlement.name.hideout_seaside_14}Seaside Hideout" type="Hideout" posX="547.506" posY="791.563" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13348: <Settlement id="hideout_seaside_15" name="{=Settlements.Settlement.name.hideout_seaside_15}Hideout" type="Hideout" posX="620.337" posY="788.961" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13356: <Settlement id="hideout_seaside_16" name="{=Settlements.Settlement.name.hideout_seaside_16}Hideout" type="Hideout" posX="1000.451" posY="826.282" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13364: <Settlement id="hideout_seaside_17" name="{=Settlements.Settlement.name.hideout_seaside_17}Hideout" type="Hideout" posX="1430.793" posY="816.84" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13372: <Settlement id="hideout_seaside_18" name="{=Settlements.Settlement.name.hideout_seaside_18}Hideout" type="Hideout" posX="1362.993" posY="473.479" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13380: <Settlement id="hideout_seaside_19" name="{=Settlements.Settlement.name.hideout_seaside_19}Hideout" type="Hideout" posX="1300.156" posY="247.452" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13388: <Settlement id="hideout_seaside_20" name="{=Settlements.Settlement.name.hideout_seaside_20}Hideout" type="Hideout" posX="777.705" posY="293.832" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13396: <Settlement id="hideout_mountain_1" name="{=Settlements.Settlement.name.hideout_mountain_1}Hideout" type="Hideout" posX="559.887" posY="640.117" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13404: <Settlement id="hideout_mountain_2" name="{=Settlements.Settlement.name.hideout_mountain_2}Hideout" type="Hideout" posX="621.992" posY="668.275" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13412: <Settlement id="hideout_mountain_3" name="{=Settlements.Settlement.name.hideout_mountain_3}Hideout" type="Hideout" posX="543.251" posY="669.003" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13420: <Settlement id="hideout_mountain_4" name="{=Settlements.Settlement.name.hideout_mountain_4}Hideout" type="Hideout" posX="500.38" posY="722.996" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13428: <Settlement id="hideout_mountain_5" name="{=Settlements.Settlement.name.hideout_mountain_5}Hideout" type="Hideout" posX="647.332" posY="686.817" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13436: <Settlement id="hideout_mountain_6" name="{=Settlements.Settlement.name.hideout_mountain_6}Hideout" type="Hideout" posX="714.48" posY="650.525" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13444: <Settlement id="hideout_mountain_7" name="{=Settlements.Settlement.name.hideout_mountain_7}Hideout" type="Hideout" posX="712.381" posY="624.834" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13452: <Settlement id="hideout_mountain_8" name="{=Settlements.Settlement.name.hideout_mountain_8}Hideout" type="Hideout" posX="763.507" posY="660.622" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13460: <Settlement id="hideout_mountain_9" name="{=Settlements.Settlement.name.hideout_mountain_9}Hideout" type="Hideout" posX="795.568" posY="638.99" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13468: <Settlement id="hideout_mountain_10" name="{=Settlements.Settlement.name.hideout_mountain_10}Hideout" type="Hideout" posX="781.159" posY="582.898" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13476: <Settlement id="hideout_mountain_11" name="{=Settlements.Settlement.name.hideout_mountain_11}Hideout" type="Hideout" posX="842.516" posY="591.794" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13484: <Settlement id="hideout_mountain_12" name="{=Settlements.Settlement.name.hideout_mountain_12}Hideout" type="Hideout" posX="866.494" posY="623.87" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13492: <Settlement id="hideout_mountain_13" name="{=Settlements.Settlement.name.hideout_mountain_13}Hideout" type="Hideout" posX="878.705" posY="607.363" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13500: <Settlement id="hideout_mountain_14" name="{=Settlements.Settlement.name.hideout_mountain_14}Hideout" type="Hideout" posX="843.333" posY="554.311" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13508: <Settlement id="hideout_mountain_15" name="{=Settlements.Settlement.name.hideout_mountain_15}Hideout" type="Hideout" posX="905.379" posY="538.943" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13516: <Settlement id="hideout_mountain_16" name="{=Settlements.Settlement.name.hideout_mountain_16}Hideout" type="Hideout" posX="857.443" posY="518.807" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13524: <Settlement id="hideout_mountain_17" name="{=Settlements.Settlement.name.hideout_mountain_17}Hideout" type="Hideout" posX="918.299" posY="653.893" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13532: <Settlement id="hideout_mountain_18" name="{=Settlements.Settlement.name.hideout_mountain_18}Hideout" type="Hideout" posX="893.563" posY="661.251" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13540: <Settlement id="hideout_mountain_19" name="{=Settlements.Settlement.name.hideout_mountain_19}Hideout" type="Hideout" posX="889.995" posY="702.1" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13548: <Settlement id="hideout_mountain_20" name="{=Settlements.Settlement.name.hideout_mountain_20}Hideout" type="Hideout" posX="853.9" posY="706.69" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13556: <Settlement id="hideout_desert_1" name="{=Settlements.Settlement.name.hideout_desert_1}Hideout" type="Hideout" posX="821.092" posY="394.346" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13564: <Settlement id="hideout_desert_2" name="{=Settlements.Settlement.name.hideout_desert_2}Hideout" type="Hideout" posX="860.578" posY="409.573" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13572: <Settlement id="hideout_desert_3" name="{=Settlements.Settlement.name.hideout_desert_3}Hideout" type="Hideout" posX="919.318" posY="386.194" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13580: <Settlement id="hideout_desert_4" name="{=Settlements.Settlement.name.hideout_desert_4}Hideout" type="Hideout" posX="999.831" posY="413.811" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13588: <Settlement id="hideout_desert_5" name="{=Settlements.Settlement.name.hideout_desert_5}Hideout" type="Hideout" posX="1062.571" posY="447.727" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13596: <Settlement id="hideout_desert_6" name="{=Settlements.Settlement.name.hideout_desert_6}Hideout" type="Hideout" posX="1135.509" posY="405.314" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13604: <Settlement id="hideout_desert_7" name="{=Settlements.Settlement.name.hideout_desert_7}Hideout" type="Hideout" posX="1071.244" posY="359.852" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13612: <Settlement id="hideout_desert_8" name="{=Settlements.Settlement.name.hideout_desert_8}Hideout" type="Hideout" posX="1195.96" posY="459.733" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13620: <Settlement id="hideout_desert_9" name="{=Settlements.Settlement.name.hideout_desert_9}Hideout" type="Hideout" posX="1228.664" posY="385.537" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13628: <Settlement id="hideout_desert_10" name="{=Settlements.Settlement.name.hideout_desert_10}Hideout" type="Hideout" posX="1155.894" posY="326.538" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13636: <Settlement id="hideout_desert_11" name="{=Settlements.Settlement.name.hideout_desert_11}Hideout" type="Hideout" posX="1047.286" posY="337.813" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13644: <Settlement id="hideout_desert_12" name="{=Settlements.Settlement.name.hideout_desert_12}Hideout" type="Hideout" posX="904.196" posY="315.645" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13652: <Settlement id="hideout_desert_13" name="{=Settlements.Settlement.name.hideout_desert_13}Hideout" type="Hideout" posX="1039.582" posY="239.546" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13660: <Settlement id="hideout_desert_14" name="{=Settlements.Settlement.name.hideout_desert_14}Hideout" type="Hideout" posX="1166.285" posY="219.163" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13668: <Settlement id="hideout_desert_15" name="{=Settlements.Settlement.name.hideout_desert_15}Hideout" type="Hideout" posX="869.585" posY="238.385" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13676: <Settlement id="hideout_desert_16" name="{=Settlements.Settlement.name.hideout_desert_16}Hideout" type="Hideout" posX="734.311" posY="154.314" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13684: <Settlement id="hideout_desert_17" name="{=Settlements.Settlement.name.hideout_desert_17}Hideout" type="Hideout" posX="707.65" posY="282.982" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13692: <Settlement id="hideout_desert_18" name="{=Settlements.Settlement.name.hideout_desert_18}Hideout" type="Hideout" posX="714.943" posY="348.153" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13700: <Settlement id="hideout_desert_19" name="{=Settlements.Settlement.name.hideout_desert_19}Hideout" type="Hideout" posX="556.015" posY="174.506" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13708: <Settlement id="hideout_desert_20" name="{=Settlements.Settlement.name.hideout_desert_20}Hideout" type="Hideout" posX="657.581" posY="200.323" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13716: <Settlement id="hideout_steppe_1" name="{=Settlements.Settlement.name.hideout_steppe_1}Hideout" type="Hideout" posX="1072.809" posY="824.275" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13724: <Settlement id="hideout_steppe_2" name="{=Settlements.Settlement.name.hideout_steppe_2}Hideout" type="Hideout" posX="1139.294" posY="825.568" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13732: <Settlement id="hideout_steppe_3" name="{=Settlements.Settlement.name.hideout_steppe_3}Hideout" type="Hideout" posX="1217.296" posY="833.625" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13740: <Settlement id="hideout_steppe_4" name="{=Settlements.Settlement.name.hideout_steppe_4}Hideout" type="Hideout" posX="1285.515" posY="856.581" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13748: <Settlement id="hideout_steppe_5" name="{=Settlements.Settlement.name.hideout_steppe_5}Hideout" type="Hideout" posX="1412.969" posY="783.693" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13756: <Settlement id="hideout_steppe_6" name="{=Settlements.Settlement.name.hideout_steppe_6}Hideout" type="Hideout" posX="1450.518" posY="736.316" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13764: <Settlement id="hideout_steppe_7" name="{=Settlements.Settlement.name.hideout_steppe_7}Hideout" type="Hideout" posX="1334.13" posY="940.679" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13772: <Settlement id="hideout_steppe_8" name="{=Settlements.Settlement.name.hideout_steppe_8}Hideout" type="Hideout" posX="1456.362" posY="1045.531" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13780: <Settlement id="hideout_steppe_9" name="{=Settlements.Settlement.name.hideout_steppe_9}Hideout" type="Hideout" posX="-137.747" posY="2406.58" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13788: <Settlement id="hideout_steppe_10" name="{=Settlements.Settlement.name.hideout_steppe_10}Hideout" type="Hideout" posX="1344.699" posY="1019.325" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13796: <Settlement id="hideout_steppe_11" name="{=Settlements.Settlement.name.hideout_steppe_11}Hideout" type="Hideout" posX="1274.175" posY="1054.239" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13804: <Settlement id="hideout_steppe_12" name="{=Settlements.Settlement.name.hideout_steppe_12}Hideout" type="Hideout" posX="1236.097" posY="1018.568" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13812: <Settlement id="hideout_steppe_13" name="{=Settlements.Settlement.name.hideout_steppe_13}Hideout" type="Hideout" posX="1243.265" posY="897.417" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13820: <Settlement id="hideout_steppe_14" name="{=Settlements.Settlement.name.hideout_steppe_14}Hideout" type="Hideout" posX="1041.594" posY="1057.945" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13828: <Settlement id="hideout_steppe_15" name="{=Settlements.Settlement.name.hideout_steppe_15}Hideout" type="Hideout" posX="970.725" posY="1079.438" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13836: <Settlement id="hideout_steppe_16" name="{=Settlements.Settlement.name.hideout_steppe_16}Hideout" type="Hideout" posX="1018.725" posY="1116.443" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13844: <Settlement id="hideout_steppe_17" name="{=Settlements.Settlement.name.hideout_steppe_17}Hideout" type="Hideout" posX="968.345" posY="1172.937" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13852: <Settlement id="hideout_steppe_18" name="{=Settlements.Settlement.name.hideout_steppe_18}Hideout" type="Hideout" posX="1063.691" posY="1148.414" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13860: <Settlement id="hideout_steppe_19" name="{=Settlements.Settlement.name.hideout_steppe_19}Hideout" type="Hideout" posX="1187.749" posY="1170.121" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13868: <Settlement id="hideout_steppe_20" name="{=Settlements.Settlement.name.hideout_steppe_20}Hideout" type="Hideout" posX="1183.59" posY="1224.941" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:808: <!-- Dunlending Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:826: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:844: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:862: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:880: <!-- Corsairs of Umbar (replaces sea_raiders) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1489: <!-- Dunland Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1490: <MBPartyTemplate id="dunland_raiders_raider_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1498: <MBPartyTemplate id="dunland_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1507: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1508: <MBPartyTemplate id="rhun_raiders_raider_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1516: <MBPartyTemplate id="rhun_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1525: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1526: <MBPartyTemplate id="harad_raiders_raider_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1534: <MBPartyTemplate id="harad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1543: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1544: <MBPartyTemplate id="gundabad_raiders_raider_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1552: <MBPartyTemplate id="gundabad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1562: <!-- Umbar Corsairs (replaces sea_raiders) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1563: <MBPartyTemplate id="umbar_corsairs_raider_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1571: <MBPartyTemplate id="umbar_corsairs_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4168: <!-- Dunlending Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4170: id="dunland_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4178: is_bandit="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4181: bandit_boss_party_template="PartyTemplate.dunland_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4203: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4205: id="rhun_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4213: is_bandit="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4216: bandit_boss_party_template="PartyTemplate.rhun_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4238: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4240: id="harad_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4248: is_bandit="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4251: bandit_boss_party_template="PartyTemplate.harad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4273: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4275: id="gundabad_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4283: is_bandit="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4286: bandit_boss_party_template="PartyTemplate.gundabad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4308: <!-- Umbar Corsairs (replaces sea_raiders) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4310: id="umbar_corsairs"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4318: is_bandit="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4321: bandit_boss_party_template="PartyTemplate.umbar_corsairs_boss_party_template">

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern 'culture=\"Culture\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)\"|default_party_template=\"PartyTemplate\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)|id=\"(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)\"' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1185ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4170: id="dunland_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4205: id="rhun_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4240: id="harad_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4275: id="gundabad_raiders"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4310: id="umbar_corsairs"

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter '*clan*.xml' | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter spcultures.xml | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter spclans.xml | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 839ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml

 succeeded in 2543ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\spcultures.xml

 succeeded in 3296ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\spclans.xml

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData\\spclans.xml' -Pattern 'is_bandit=\"true\"|forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders|default_party_template' -Context 0,3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\spclans.xml' -Pattern 'is_bandit=\"true\"|dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs|forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders|default_party_template' -Context 0,3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\characters\\clans.xml -Pattern 'is_bandit=\"true\"|dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs|forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders|default_party_template' -Context 0,3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
 succeeded in 835ms:
 succeeded in 906ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:24: default_party_template="PartyTemplate.looters_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:26: is_bandit="true"
    is_outlaw="true"
    name="{=1sbJQR4H}Looters"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:37: default_party_template="PartyTemplate.looters_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:39: is_bandit="true"
    is_outlaw="true"
    name="{=9yaFxhGa}Deserters"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:43: <!--Faction id="deserters" owner="Hero.lord_deserters_1" label_color="FF8B7C73" color="FF8B7C73" color2="FF8B7C73" alternative_color="FF8B7C73" alternative_color2="FF8B7C73" culture="Culture.deserters" default_party_template="PartyTemplate.deserters_template" settlement_banner_mesh="none" is_bandit="true" is_outlaw="true" name="Deserters">
    </Faction-->
    <Faction
    id="sea_raiders"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:46: id="sea_raiders"
    initial_home_settlement="Settlement.hideout_seaside_6"
    banner_key="17.234.116.1536.1536.768.768.1.0.0"
    color="FF8B7C73"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:51: culture="Culture.sea_raiders"
    default_party_template="PartyTemplate.sea_raiders_template"
    settlement_banner_mesh="none"
    is_bandit="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:52: default_party_template="PartyTemplate.sea_raiders_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:54: is_bandit="true"
    is_outlaw="true"
    name="{=pBUc2pL8}Sea Raiders"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:59: id="mountain_bandits"
    initial_home_settlement="Settlement.hideout_mountain_15"
    banner_key="16.59.116.1536.1536.768.768.1.0.0"
    color="FF8B7C73"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:64: culture="Culture.mountain_bandits"
    default_party_template="PartyTemplate.mountain_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:65: default_party_template="PartyTemplate.mountain_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:67: is_bandit="true"
    is_outlaw="true"
    name="{=mOa9CX13}Mountain Bandits"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:72: id="forest_bandits"
    initial_home_settlement="Settlement.hideout_forest_19"
    banner_key="17.181.116.1536.1536.768.768.1.0.0"
    color="FF8B7C73"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:77: culture="Culture.forest_bandits"
    default_party_template="PartyTemplate.forest_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:78: default_party_template="PartyTemplate.forest_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:80: is_bandit="true"
    is_outlaw="true"
    name="{=sAbjtZbB}Forest Bandits"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:85: id="desert_bandits"
    initial_home_settlement="Settlement.hideout_desert_12"
    banner_key="16.191.116.1536.1536.768.768.1.0.0"
    color="FF8B7C73"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:90: culture="Culture.desert_bandits"
    default_party_template="PartyTemplate.desert_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:91: default_party_template="PartyTemplate.desert_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:93: is_bandit="true"
    is_outlaw="true"
    name="{=kkUQObZn}Desert Bandits"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:98: id="steppe_bandits"
    initial_home_settlement="Settlement.hideout_steppe_5"
    banner_key="17.192.116.1536.1536.768.768.1.0.0"
    color="FF8B7C73"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:103: culture="Culture.steppe_bandits"
    default_party_template="PartyTemplate.steppe_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:104: default_party_template="PartyTemplate.steppe_bandits_template"
    settlement_banner_mesh="none"
    is_bandit="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:106: is_bandit="true"
    is_outlaw="true"
    name="{=e6csp4ha}Steppe Bandits"
    tier="1"></Faction>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:118: default_party_template="PartyTemplate.kingdom_hero_party_mercenary_aserai_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_clan_type_mercenary="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:144: default_party_template="PartyTemplate.kingdom_hero_party_mercenary_empire_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_clan_type_mercenary="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:171: default_party_template="PartyTemplate.kingdom_hero_party_mercenary_sturgia_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_clan_type_mercenary="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:197: default_party_template="PartyTemplate.kingdom_hero_party_mercenary_vlandia_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_clan_type_mercenary="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:224: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_aserai_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:250: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_battania_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:276: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_vlandia_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:297: <!--<Faction id="guardians" initial_posX="294.0" initial_posY="378.0" label_color="FFFFFFCC" color="FFFFFFCC" color2="FFFFFFCC" alternative_color="FFFFFFCC" alternative_color2="FFFFFFCC" culture="Culture.empire" default_party_template="PartyTemplate.kingdom_hero_party_outlaw_empire_s_template" settlement_banner_mesh="encounter_flag_f" is_minor_faction="true" is_outlaw="true" is_mafia="true" name="Guardians of the Hills" short_name="{=*}Guardians" text="{=*}The Watchers are frontiersmen in the northwest of the Empire. They are used to working with the local nobility, specializing in scouting and tracking. But as the Empire descended into civil war, they have begun asserting power in the areas in which they live, becoming a law unto themselves and taking what they want from surrounding villages. They still will cooperate with imperial marcher noble families, but on their terms, not the Empire's.">
    </Faction>-->
    <Faction
    id="hidden_hand"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:306: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_empire_w_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:333: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_sturgia_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:355: <!--<Faction id="chosen_of_the_sky" initial_posX="629.0" initial_posY="515.0" label_color="FFFFFFCC" color="FFFFFFCC" color2="FFFFFFCC" alternative_color="FFFFFFCC" alternative_color2="FFFFFFCC" culture="Culture.khuzait" default_party_template="PartyTemplate.kingdom_hero_party_karakhuzaits_template" settlement_banner_mesh="encounter_flag_f" is_minor_faction="true" is_outlaw="true" is_sect="true" name="Chosen of the Sky" short_name="{=*}Chosen" >
    </Faction>-->
    <Faction
    id="embers_of_flame"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:364: default_party_template="PartyTemplate.kingdom_hero_party_outlaw_empire_e_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:392: default_party_template="PartyTemplate.kingdom_hero_party_jawwal_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:418: default_party_template="PartyTemplate.kingdom_hero_party_karakhuzaits_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spclans.xml:444: default_party_template="PartyTemplate.kingdom_hero_party_forest_people_template"
    settlement_banner_mesh="encounter_flag_f"
    is_minor_faction="true"
    is_outlaw="true"

 succeeded in 1210ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Hideout.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 160;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Settlement.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 140;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter *.cs | Select-String -Pattern \"new Hideout|Hideout\\(\" | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
 succeeded in 831ms:
1: using System;
2: using System.Collections.Generic;
3: using System.Linq;
4: using System.Xml;
5: using TaleWorlds.CampaignSystem.Encounters;
6: using TaleWorlds.CampaignSystem.Extensions;
7: using TaleWorlds.CampaignSystem.GameState;
8: using TaleWorlds.CampaignSystem.Map;
9: using TaleWorlds.CampaignSystem.Map.DistanceCache;
10: using TaleWorlds.CampaignSystem.MapEvents;
11: using TaleWorlds.CampaignSystem.Party;
12: using TaleWorlds.CampaignSystem.Party.PartyComponents;
13: using TaleWorlds.CampaignSystem.Roster;
14: using TaleWorlds.CampaignSystem.Settlements.Buildings;
15: using TaleWorlds.CampaignSystem.Settlements.Locations;
16: using TaleWorlds.CampaignSystem.Siege;
17: using TaleWorlds.Core;
18: using TaleWorlds.Library;
19: using TaleWorlds.Localization;
20: using TaleWorlds.ObjectSystem;
21: using TaleWorlds.SaveSystem;
22: using TaleWorlds.SaveSystem.Load;
23: 
24: namespace TaleWorlds.CampaignSystem.Settlements;
25: 
26: public sealed class Settlement : MBObjectBase, ILocatable<Settlement>, IMapPoint, ITrackableCampaignObject, ITrackableBase, ISiegeEventSide, IRandomOwner, ISettlementDataHolder
27: {
28: 	public enum SiegeState
29: 	{
30: 		OnTheWalls,
31: 		InTheLordsHall,
32: 		Invalid
33: 	}
34: 
35: 	[SaveableField(107)]
36: 	public bool HasVisited;
37: 
38: 	[SaveableField(110)]
39: 	public float LastVisitTimeOfOwner;
40: 
41: 	[SaveableField(113)]
42: 	private bool _isVisible;
43: 
44: 	[CachedData]
45: 	private int _locatorNodeIndex;
46: 
47: 	[SaveableField(117)]
48: 	private Settlement _nextLocatable;
49: 
50: 	[CachedData]
51: 	private float _oldProsperityObsolete = -1f;
52: 
53: 	[SaveableField(119)]
54: 	private float _readyMilitia;
55: 
56: 	[SaveableField(120)]
57: 	private MBList<float> _settlementWallSectionHitPointsRatioList = new MBList<float>();
58: 
59: 	[CachedData]
60: 	private MBList<MobileParty> _partiesCache;
61: 
62: 	[CachedData]
63: 	private MBList<Hero> _heroesWithoutPartyCache;
64: 
65: 	[CachedData]
66: 	private MBList<Hero> _notablesCache;
67: 
68: 	private CampaignVec2 _position;
69: 
70: 	public CultureObject Culture;
71: 
72: 	private TextObject _name;
73: 
74: 	[SaveableField(129)]
75: 	private MBList<Village> _boundVillages;
76: 
77: 	[SaveableField(131)]
78: 	private MobileParty _lastAttackerParty;
79: 
80: 	[SaveableField(148)]
81: 	private MBList<SiegeEvent.SiegeEngineMissile> _siegeEngineMissiles;
82: 
83: 	public Town Town;
84: 
85: 	public Village Village;
86: 
87: 	public Hideout Hideout;
88: 
89: 	[CachedData]
90: 	public MilitiaPartyComponent MilitiaPartyComponent;
91: 
92: 	[SaveableField(145)]
93: 	public readonly ItemRoster Stash;
94: 
95: 	[SaveableProperty(101)]
96: 	public PartyBase Party { get; private set; }
97: 
98: 	[SaveableProperty(116)]
99: 	public int BribePaid { get; set; }
100: 
101: 	[SaveableProperty(111)]
102: 	public SiegeEvent SiegeEvent { get; set; }
103: 
104: 	[SaveableProperty(112)]
105: 	public bool IsActive { get; set; }
106: 
107: 	public Hero Owner => OwnerClan.Leader;
108: 
109: 	public Banner Banner
110: 	{
111: 		get
112: 		{
113: 			if (Party.CustomBanner != null)
114: 			{
115: 				return Party.CustomBanner;
116: 			}
117: 			if (SettlementComponent != null && SettlementComponent.GetDefaultComponentBanner() != null)
118: 			{
119: 				return SettlementComponent.GetDefaultComponentBanner();
120: 			}
121: 			if (OwnerClan != null)
122: 			{
123: 				return OwnerClan.Banner;
124: 			}
125: 			return null;
126: 		}
127: 	}
128: 
129: 	public bool IsVisible
130: 	{
131: 		get
132: 		{
133: 			return _isVisible;
134: 		}
135: 		set
136: 		{
137: 			if (_isVisible != value)
138: 			{
139: 				_isVisible = value;
140: 				Party.OnVisibilityChanged(value);

 succeeded in 1200ms:
1: using System.Collections.Generic;
2: using System.Linq;
3: using System.Xml;
4: using TaleWorlds.CampaignSystem.MapEvents;
5: using TaleWorlds.CampaignSystem.Party;
6: using TaleWorlds.Library;
7: using TaleWorlds.LinQuick;
8: using TaleWorlds.ObjectSystem;
9: using TaleWorlds.SaveSystem;
10: 
11: namespace TaleWorlds.CampaignSystem.Settlements;
12: 
13: public class Hideout : SettlementComponent, ISpottable
14: {
15: 	[SaveableField(200)]
16: 	private CampaignTime _nextPossibleAttackTime;
17: 
18: 	[SaveableField(201)]
19: 	private bool _isSpotted;
20: 
21: 	public CampaignTime NextPossibleAttackTime => _nextPossibleAttackTime;
22: 
23: 	public static MBReadOnlyList<Hideout> All => Campaign.Current.AllHideouts;
24: 
25: 	public bool IsInfested => base.Owner.Settlement.Parties.CountQ((MobileParty x) => x.IsBandit) >= Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
26: 
27: 	public override IFaction MapFaction
28: 	{
29: 		get
30: 		{
31: 			foreach (MobileParty party in base.Settlement.Parties)
32: 			{
33: 				if (party.IsBandit)
34: 				{
35: 					return party.ActualClan;
36: 				}
37: 			}
38: 			foreach (Clan item in Clan.All)
39: 			{
40: 				if (item.IsBanditFaction)
41: 				{
42: 					return item;
43: 				}
44: 			}
45: 			return null;
46: 		}
47: 	}
48: 
49: 	public bool IsSpotted
50: 	{
51: 		get
52: 		{
53: 			return _isSpotted;
54: 		}
55: 		set
56: 		{
57: 			_isSpotted = value;
58: 		}
59: 	}
60: 
61: 	internal static void AutoGeneratedStaticCollectObjectsHideout(object o, List<object> collectedObjects)
62: 	{
63: 		((Hideout)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
64: 	}
65: 
66: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
67: 	{
68: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
69: 		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_nextPossibleAttackTime, collectedObjects);
70: 	}
71: 
72: 	internal static object AutoGeneratedGetMemberValue_nextPossibleAttackTime(object o)
73: 	{
74: 		return ((Hideout)o)._nextPossibleAttackTime;
75: 	}
76: 
77: 	internal static object AutoGeneratedGetMemberValue_isSpotted(object o)
78: 	{
79: 		return ((Hideout)o)._isSpotted;
80: 	}
81: 
82: 	public void SetNextPossibleAttackTime(CampaignTime hiddenDurationFromNow)
83: 	{
84: 		_nextPossibleAttackTime = CampaignTime.Now + hiddenDurationFromNow;
85: 	}
86: 
87: 	public IEnumerable<PartyBase> GetDefenderParties(MapEvent.BattleTypes battleType)
88: 	{
89: 		yield return base.Settlement.Party;
90: 		foreach (MobileParty party in base.Settlement.Parties)
91: 		{
92: 			if (party.IsBandit || party.IsBanditBossParty)
93: 			{
94: 				yield return party.Party;
95: 			}
96: 		}
97: 	}
98: 
99: 	public PartyBase GetNextDefenderParty(ref int partyIndex, MapEvent.BattleTypes battleType)
100: 	{
101: 		partyIndex++;
102: 		if (partyIndex == 0)
103: 		{
104: 			return base.Settlement.Party;
105: 		}
106: 		for (int i = partyIndex - 1; i < base.Settlement.Parties.Count; i++)
107: 		{
108: 			MobileParty mobileParty = base.Settlement.Parties[i];
109: 			if (mobileParty.IsBandit || mobileParty.IsBanditBossParty)
110: 			{
111: 				partyIndex = i + 1;
112: 				return mobileParty.Party;
113: 			}
114: 		}
115: 		return null;
116: 	}
117: 
118: 	public Hideout()
119: 	{
120: 		IsSpotted = false;
121: 	}
122: 
123: 	public override void OnPartyEntered(MobileParty mobileParty)
124: 	{
125: 		base.OnPartyEntered(mobileParty);
126: 		UpdateOwnership();
127: 		if (mobileParty.MapFaction.IsBanditFaction && mobileParty.BanditPartyComponent != null)
128: 		{
129: 			mobileParty.BanditPartyComponent.SetHomeHideout(this);
130: 		}
131: 	}
132: 
133: 	public override void OnPartyLeft(MobileParty mobileParty)
134: 	{
135: 		UpdateOwnership();
136: 		if (base.Owner.Settlement.Parties.Count == 0)
137: 		{
138: 			OnHideoutIsEmpty();
139: 		}
140: 	}
141: 
142: 	public override void OnRelatedPartyRemoved(MobileParty mobileParty)
143: 	{
144: 		if (base.Owner.Settlement.Parties.Count == 0)
145: 		{
146: 			OnHideoutIsEmpty();
147: 		}
148: 	}
149: 
150: 	private void OnHideoutIsEmpty()
151: 	{
152: 		IsSpotted = false;
153: 		base.Owner.Settlement.IsVisible = false;
154: 		CampaignEventDispatcher.Instance.OnHideoutDeactivated(base.Settlement);
155: 	}
156: 
157: 	public override void OnInit()
158: 	{
159: 		base.Owner.Settlement.IsVisible = false;
160: 	}

 succeeded in 1859ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:347: public static Settlement FindRandomHideout(Func<Settlement, bool> condition = null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:49: gameStarter.AddBehavior(new HideoutCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:204: AddBanditToHideout(settlement.Hideout, banditBossPartyTemplate, isBanditBossParty: true).Ai.DisableAi();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:250: SpawnBanditsAroundHideout(clan, 0.1f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:255: private void SpawnBanditsAroundHideout(Clan clan, float ratio)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:307: AddBanditToHideout(hideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:312: public MobileParty AddBanditToHideout(Hideout hideoutComponent, PartyTemplateObject overridenPartyTemplate = null, bool isBanditBossParty = false)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:335: private Hideout SelectBanditHideout(Clan faction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:442: SpawnBanditsAroundHideout(banditFaction, MBRandom.RandomFloatRanged(0.5f, 0.75f));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:469: Hideout hideout = SelectBanditHideout(selectedFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:1786: IncidentEffect.InfestNearbyHideout(() => MobileParty.MainParty.LastVisitedSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PatrolPartiesCampaignBehavior.cs:229: private bool patrol_talk_on_condition_hideout()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encounters\PlayerEncounter.cs:1936: LocationEncounter = new HideoutEncounter(settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\IncidentEffect.cs:681: public static IncidentEffect InfestNearbyHideout(Func<Settlement> settlementGetter)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\IncidentEffect.cs:696: behavior.AddBanditToHideout(hideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:127: internal static object AutoGeneratedGetMemberValue_hideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:326: internal static object AutoGeneratedGetMemberValue_questHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:518: selectedHideout = FindSuitableHideout(issueGiver);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:529: private Settlement FindSuitableHideout(Hero issueGiver)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:168: internal static object AutoGeneratedGetMemberValue_issueHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:509: internal static object AutoGeneratedGetMemberValue_questHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:939: selectedHideout = ((issueGiver.CurrentSettlement != null && issueGiver.CurrentSettlement.IsTown) ? FindSuitableHideout(issueGiver.CurrentSettlement) : null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:952: private static Settlement FindSuitableHideout(Settlement settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWithOutlawsIssueQuestBehavior.cs:150: internal static object AutoGeneratedGetMemberValueRelatedHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWithOutlawsIssueQuestBehavior.cs:459: internal static object AutoGeneratedGetMemberValue_relatedHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\NearbyBanditBaseIssueBehavior.cs:190: internal static object AutoGeneratedGetMemberValue_targetHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\NearbyBanditBaseIssueBehavior.cs:445: internal static object AutoGeneratedGetMemberValue_targetHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\NearbyBanditBaseIssueBehavior.cs:629: private Settlement FindSuitableHideout(Hero issueOwner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\NearbyBanditBaseIssueBehavior.cs:657: Settlement settlement = FindSuitableHideout(hero);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\HideoutEventComponent.cs:48: HideoutEventComponent hideoutEventComponent = new HideoutEventComponent(mapEvent, isSendTroops);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\HideoutEventComponent.cs:56: return new HideoutEventComponent(mapEvent, isSendTroops);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs:91: internal static object AutoGeneratedGetMemberValueHideout(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs:141: public void SetHomeHideout(Hideout hideout)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs:61: internal static void AutoGeneratedStaticCollectObjectsHideout(object o, List<object> collectedObjects)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs:118: public Hideout()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs:129: mobileParty.BanditPartyComponent.SetHomeHideout(this);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter TroopRoster.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter PartyTemplateStack.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter PartyTemplateObject.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 895ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs

 succeeded in 1285ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyTemplateStack.cs

 succeeded in 1677ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyTemplateObject.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyTemplateStack.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 120;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyTemplateObject.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 180;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 260;$i++){ if($lines[$i-1] -match '"'CreateDummyTroopRoster|AddToCounts|GetTroopCount|FindIndexOfTroop|Debug|TotalManCount'){ '{0}: {1}' -f "'$i,$lines[$i-1] } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 796ms:
1: using System.Collections.Generic;
2: 
3: namespace TaleWorlds.CampaignSystem.Party;
4: 
5: public struct PartyTemplateStack
6: {
7: 	public CharacterObject Character;
8: 
9: 	public int MinValue;
10: 
11: 	public int MaxValue;
12: 
13: 	public static void AutoGeneratedStaticCollectObjectsPartyTemplateStack(object o, List<object> collectedObjects)
14: 	{
15: 		((PartyTemplateStack)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
16: 	}
17: 
18: 	private void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
19: 	{
20: 	}
21: 
22: 	public PartyTemplateStack(CharacterObject character, int minValue, int maxValue)
23: 	{
24: 		Character = character;
25: 		MinValue = minValue;
26: 		MaxValue = maxValue;
27: 	}
28: }
29: 
30: 
31: 
32: 
33: 
34: 
35: 
36: 
37: 
38: 
39: 
40: 
41: 
42: 
43: 
44: 
45: 
46: 
47: 
48: 
49: 
50: 
51: 
52: 
53: 
54: 
55: 
56: 
57: 
58: 
59: 
60: 
61: 
62: 
63: 
64: 
65: 
66: 
67: 
68: 
69: 
70: 
71: 
72: 
73: 
74: 
75: 
76: 
77: 
78: 
79: 
80: 
81: 
82: 
83: 
84: 
85: 
86: 
87: 
88: 
89: 
90: 
91: 
92: 
93: 
94: 
95: 
96: 
97: 
98: 
99: 
100: 
101: 
102: 
103: 
104: 
105: 
106: 
107: 
108: 
109: 
110: 
111: 
112: 
113: 
114: 
115: 
116: 
117: 
118: 
119: 
120: 

 succeeded in 1176ms:
1: using System;
2: using System.Collections.Generic;
3: using System.Xml;
4: using TaleWorlds.Core;
5: using TaleWorlds.Library;
6: using TaleWorlds.ObjectSystem;
7: 
8: namespace TaleWorlds.CampaignSystem.Party;
9: 
10: public sealed class PartyTemplateObject : MBObjectBase
11: {
12: 	public MBList<PartyTemplateStack> Stacks;
13: 
14: 	public MBList<ShipTemplateStack> ShipHulls;
15: 
16: 	internal static void AutoGeneratedStaticCollectObjectsPartyTemplateObject(object o, List<object> collectedObjects)
17: 	{
18: 		((PartyTemplateObject)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
19: 	}
20: 
21: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
22: 	{
23: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
24: 	}
25: 
26: 	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
27: 	{
28: 		Stacks = new MBList<PartyTemplateStack>();
29: 		ShipHulls = new MBList<ShipTemplateStack>();
30: 		base.Deserialize(objectManager, node);
31: 		foreach (XmlNode childNode in node.ChildNodes)
32: 		{
33: 			if (childNode.Name == "stacks")
34: 			{
35: 				foreach (XmlNode childNode2 in childNode.ChildNodes)
36: 				{
37: 					if (childNode2.Name == "PartyTemplateStack")
38: 					{
39: 						PartyTemplateStack item = new PartyTemplateStack((CharacterObject)objectManager.ReadObjectReferenceFromXml("troop", typeof(CharacterObject), childNode2), Convert.ToInt32(childNode2.Attributes["min_value"].Value), Convert.ToInt32(childNode2.Attributes["max_value"].Value));
40: 						Stacks.Add(item);
41: 					}
42: 				}
43: 			}
44: 			else
45: 			{
46: 				if (!(childNode.Name == "ship_hulls"))
47: 				{
48: 					continue;
49: 				}
50: 				foreach (XmlNode childNode3 in childNode.ChildNodes)
51: 				{
52: 					if (childNode3.Name == "ShipTemplateStack")
53: 					{
54: 						ShipTemplateStack item2 = new ShipTemplateStack((ShipHull)objectManager.ReadObjectReferenceFromXml("id", typeof(ShipHull), childNode3), Convert.ToInt32(childNode3.Attributes["min_value"].Value), Convert.ToInt32(childNode3.Attributes["max_value"].Value));
55: 						ShipHulls.Add(item2);
56: 					}
57: 				}
58: 			}
59: 		}
60: 	}
61: 
62: 	public int GetUpperTroopLimit()
63: 	{
64: 		int num = 0;
65: 		foreach (PartyTemplateStack stack in Stacks)
66: 		{
67: 			num += stack.MaxValue;
68: 		}
69: 		return num;
70: 	}
71: 
72: 	public int GetLowerTroopLimit()
73: 	{
74: 		int num = 0;
75: 		foreach (PartyTemplateStack stack in Stacks)
76: 		{
77: 			num += stack.MinValue;
78: 		}
79: 		return num;
80: 	}
81: }
82: 
83: 
84: 
85: 
86: 
87: 
88: 
89: 
90: 
91: 
92: 
93: 
94: 
95: 
96: 
97: 
98: 
99: 
100: 
101: 
102: 
103: 
104: 
105: 
106: 
107: 
108: 
109: 
110: 
111: 
112: 
113: 
114: 
115: 
116: 
117: 
118: 
119: 
120: 
121: 
122: 
123: 
124: 
125: 
126: 
127: 
128: 
129: 
130: 
131: 
132: 
133: 
134: 
135: 
136: 
137: 
138: 
139: 
140: 
141: 
142: 
143: 
144: 
145: 
146: 
147: 
148: 
149: 
150: 
151: 
152: 
153: 
154: 
155: 
156: 
157: 
158: 
159: 
160: 
161: 
162: 
163: 
164: 
165: 
166: 
167: 
168: 
169: 
170: 
171: 
172: 
173: 
174: 
175: 
176: 
177: 
178: 
179: 
180: 

 succeeded in 1496ms:
61: 	public int TotalManCount => _totalRegulars + _totalHeroes;
105: 	public static TroopRoster CreateDummyTroopRoster()
187: 		return new FlattenedTroopRoster(TotalManCount) { GetTroopRoster() };
200: 		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
212: 				AddToCountsAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);
218: 	public int FindIndexOfTroop(CharacterObject character)
248: 		TroopRoster troopRoster = CreateDummyTroopRoster();
251: 			TaleWorlds.Library.Debug.FailedAssert("numberOfMen > _totalRegulars - _totalWoundedRegulars", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "RemoveNumberOfNonHeroTroopsRandomly", 315);
260: 				troopRoster.AddToCounts(GetCharacterAtIndex(num), 1);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern 'public void AddToCounts|public int GetTroopCount|private void AddToCountsAtIndex' -Context 0,18 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content -Path $p; for($i=90;$i -le 140;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=214;$i -le 240;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=720;$i -le 820;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern 'GetTroopCount\\(|AddToCounts\\(' | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim() } | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
 succeeded in 885ms:
90: 
91: 	public TroopRoster(PartyBase ownerParty)
92: 		: this()
93: 	{
94: 		OwnerParty = ownerParty;
95: 	}
96: 
97: 	private TroopRoster()
98: 	{
99: 		data = new TroopRosterElement[4];
100: 		_count = 0;
101: 		_troopRosterElements = new MBList<TroopRosterElement>();
102: 		InitializeCachedData();
103: 	}
104: 
105: 	public static TroopRoster CreateDummyTroopRoster()
106: 	{
107: 		return new TroopRoster();
108: 	}
109: 
110: 	public override int GetHashCode()
111: 	{
112: 		return base.GetHashCode();
113: 	}
114: 
115: 	[LoadInitializationCallback]
116: 	private void OnLoad(MetaData metaData)
117: 	{
118: 		_troopRosterElementsVersion = -1;
119: 		_troopRosterElements = new MBList<TroopRosterElement>();
120: 		InstanceListForLoadGame.Add(this);
121: 	}
122: 
123: 	private void EnsureLength(int length)
124: 	{
125: 		if (length > 0 && (data == null || length > data.Length))
126: 		{
127: 			int num = 4;
128: 			if (data != null)
129: 			{
130: 				num = data.Length * 2;
131: 			}
132: 			TroopRosterElement[] array = new TroopRosterElement[num];
133: 			for (int i = 0; i < _count; i++)
134: 			{
135: 				array[i] = data[i];
136: 			}
137: 			data = array;
138: 		}
139: 	}
140: 
214: 		}
215: 		return list;
216: 	}
217: 
218: 	public int FindIndexOfTroop(CharacterObject character)
219: 	{
220: 		for (int i = 0; i < _count; i++)
221: 		{
222: 			if (data[i].Character == character)
223: 			{
224: 				return i;
225: 			}
226: 		}
227: 		return -1;
228: 	}
229: 
230: 	private int FlattenedRosterIndexToTroopRosterIndexWithFilter(int indexOfTroop, bool includeHeroes = false, bool countOnlyHealthyOnes = true)
231: 	{
232: 		for (int i = 0; i < _count; i++)
233: 		{
234: 			if (includeHeroes || !data[i].Character.IsHero)
235: 			{
236: 				indexOfTroop -= (countOnlyHealthyOnes ? (data[i].Number - data[i].WoundedNumber) : data[i].Number);
237: 				if (indexOfTroop < 0)
238: 				{
239: 					return i;
240: 				}
720: 			num += selector(data[i]);
721: 		}
722: 		return num;
723: 	}
724: 
725: 	public void OnHeroHealthStatusChanged(Hero hero)
726: 	{
727: 		UpdateVersion();
728: 		_totalWoundedHeroes += (hero.IsWounded ? 1 : (-1));
729: 	}
730: 
731: 	public void UpdateVersion()
732: 	{
733: 		OwnerParty?.MobileParty?.UpdateVersionNo();
734: 		VersionNo++;
735: 	}
736: 
737: 	public TroopRoster CloneRosterData()
738: 	{
739: 		TroopRoster troopRoster = new TroopRoster();
740: 		if (data != null)
741: 		{
742: 			for (int i = 0; i < data.Length; i++)
743: 			{
744: 				TroopRosterElement troopRosterElement = data[i];
745: 				if (troopRosterElement.Character != null)
746: 				{
747: 					troopRoster.AddToCounts(data[i].Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber);
748: 				}
749: 			}
750: 		}
751: 		return troopRoster;
752: 	}
753: 
754: 	public void AddXpToTroop(CharacterObject troop, int xpAmount)
755: 	{
756: 		int index = FindIndexOfTroop(troop);
757: 		AddXpToTroopAtIndex(index, xpAmount);
758: 	}
759: 
760: 	public void AddXpToTroopAtIndex(int index, int xpAmount)
761: 	{
762: 		if (index >= 0 && index < _count)
763: 		{
764: 			xpAmount += GetElementXp(data[index].Character);
765: 			SetElementXp(index, xpAmount);
766: 		}
767: 	}
768: }
769: 
770: 
771: 
772: 
773: 
774: 
775: 
776: 
777: 
778: 
779: 
780: 
781: 
782: 
783: 
784: 
785: 
786: 
787: 
788: 
789: 
790: 
791: 
792: 
793: 
794: 
795: 
796: 
797: 
798: 
799: 
800: 
801: 
802: 
803: 
804: 
805: 
806: 
807: 
808: 
809: 
810: 
811: 
812: 
813: 
814: 
815: 
816: 
817: 
818: 
819: 
820: 

 succeeded in 1274ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:457: public int GetTroopCount(CharacterObject troop)
    {
    int num = FindIndexOfTroop(troop);
    if (num >= 0)
    {
    return data[num].Number;
    }
    return 0;
    }
    
    public void RemoveZeroCounts()
    {
    int num = 0;
    for (int i = 0; i < _count; i++)
    {
    if (data[i].Number > 0)
    {
    if (num != i)
    {

 succeeded in 1635ms:
200: AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
260: troopRoster.AddToCounts(GetCharacterAtIndex(num), 1);
428: public int AddToCounts(CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)
457: public int GetTroopCount(CharacterObject troop)
747: troopRoster.AddToCounts(data[i].Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content -Path $p; for($i=410;$i -le 475;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 725ms:
410: 	{
411: 		if (data == null)
412: 		{
413: 			return;
414: 		}
415: 		int num = 0;
416: 		for (int i = 0; i < data.Length; i++)
417: 		{
418: 			TroopRosterElement troopRosterElement = data[i];
419: 			if (troopRosterElement.Character != null)
420: 			{
421: 				_ = troopRosterElement.Number;
422: 				_ = troopRosterElement.WoundedNumber;
423: 				num++;
424: 			}
425: 		}
426: 	}
427: 
428: 	public int AddToCounts(CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)
429: 	{
430: 		if (count != 0 || woundedCount != 0)
431: 		{
432: 			UpdateVersion();
433: 		}
434: 		if (insertAtFront)
435: 		{
436: 			index = 0;
437: 		}
438: 		int num = FindIndexOfTroop(character);
439: 		if (num < 0)
440: 		{
441: 			if (count + woundedCount <= 0)
442: 			{
443: 				TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCounts", 572);
444: 				return -1;
445: 			}
446: 			num = AddNewElement(character, index);
447: 		}
448: 		else if (index != -1 && index != num)
449: 		{
450: 			ShiftTroopToIndex(num, index);
451: 			num = index;
452: 		}
453: 		AddToCountsAtIndex(num, count, woundedCount, xpChange, removeDepleted);
454: 		return num;
455: 	}
456: 
457: 	public int GetTroopCount(CharacterObject troop)
458: 	{
459: 		int num = FindIndexOfTroop(troop);
460: 		if (num >= 0)
461: 		{
462: 			return data[num].Number;
463: 		}
464: 		return 0;
465: 	}
466: 
467: 	public void RemoveZeroCounts()
468: 	{
469: 		int num = 0;
470: 		for (int i = 0; i < _count; i++)
471: 		{
472: 			if (data[i].Number > 0)
473: 			{
474: 				if (num != i)
475: 				{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern 'AddToCountsAtIndex' -Context 0,35 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 796ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:212: AddToCountsAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);
    }
    }
    return list;
    }
    
    public int FindIndexOfTroop(CharacterObject character)
    {
    for (int i = 0; i < _count; i++)
    {
    if (data[i].Character == character)
    {
    return i;
    }
    }
    return -1;
    }
    
    private int FlattenedRosterIndexToTroopRosterIndexWithFilter(int indexOfTroop, bool includeHeroes = false, bool countOnlyHealthyOnes = true)
    {
    for (int i = 0; i < _count; i++)
    {
    if (includeHeroes || !data[i].Character.IsHero)
    {
    indexOfTroop -= (countOnlyHealthyOnes ? (data[i].Number - data[i].WoundedNumber) : data[i].Number);
    if (indexOfTroop < 0)
    {
    return i;
    }
    }
    }
    return -1;
    }
    
    public TroopRoster RemoveNumberOfNonHeroTroopsRandomly(int numberOfMen)
    {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:261: AddToCountsAtIndex(num, -1);
    }
    }
    return troopRoster;
    }
    
    public void WoundNumberOfNonHeroTroopsRandomly(int numberOfMen)
    {
    for (int i = 0; i < numberOfMen; i++)
    {
    int indexOfTroop = MBRandom.RandomInt(_totalRegulars - _totalWoundedRegulars);
    int num = FlattenedRosterIndexToTroopRosterIndexWithFilter(indexOfTroop);
    if (num >= 0)
    {
    AddToCountsAtIndex(num, 0, 1);
    }
    }
    }
    
    public void SwapTroopsAtIndices(int firstIndex, int secondIndex)
    {
    if (firstIndex < 0 || firstIndex >= Count || secondIndex < 0 || secondIndex >= Count)
    {
    TaleWorlds.Library.Debug.FailedAssert("Troop roster swap indices are out of bounds.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "SwapTroopsAtIndices", 358);
    }
    else if (firstIndex != secondIndex)
    {
    TroopRosterElement troopRosterElement = data[firstIndex];
    data[firstIndex] = data[secondIndex];
    data[secondIndex] = troopRosterElement;
    }
    }
    
    public void ShiftTroopToIndex(int troopIndex, int targetIndex)
    {
    if (troopIndex < 0 || troopIndex >= Count || targetIndex < 0 || targetIndex >= Count)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:275: AddToCountsAtIndex(num, 0, 1);
    }
    }
    }
    
    public void SwapTroopsAtIndices(int firstIndex, int secondIndex)
    {
    if (firstIndex < 0 || firstIndex >= Count || secondIndex < 0 || secondIndex >= Count)
    {
    TaleWorlds.Library.Debug.FailedAssert("Troop roster swap indices are out of bounds.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "SwapTroopsAtIndices", 358);
    }
    else if (firstIndex != secondIndex)
    {
    TroopRosterElement troopRosterElement = data[firstIndex];
    data[firstIndex] = data[secondIndex];
    data[secondIndex] = troopRosterElement;
    }
    }
    
    public void ShiftTroopToIndex(int troopIndex, int targetIndex)
    {
    if (troopIndex < 0 || troopIndex >= Count || targetIndex < 0 || targetIndex >= Count)
    {
    TaleWorlds.Library.Debug.FailedAssert("Troop roster swap indices are out of bounds.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "ShiftTroopToIndex", 376);
    }
    else if (troopIndex != targetIndex)
    {
    int num = ((troopIndex < targetIndex) ? 1 : (-1));
    TroopRosterElement troopRosterElement = data[troopIndex];
    for (int i = troopIndex; i != targetIndex; i += num)
    {
    data[i] = data[i + num];
    }
    data[targetIndex] = troopRosterElement;
    }
    }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:312: public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
    {
    bool flag = false;
    CharacterObject character = data[index].Character;
    bool isHero = character.IsHero;
    data[index].Number += countChange;
    int num = data[index].WoundedNumber + woundedCountChange;
    if (num > data[index].Number)
    {
    woundedCountChange += data[index].Number - num;
    }
    else if (num < 0)
    {
    TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCountsAtIndex", 415);
    }
    data[index].WoundedNumber += woundedCountChange;
    if (xpChange != 0)
    {
    SetElementXp(index, data[index].Xp + xpChange);
    }
    if (isHero)
    {
    _totalHeroes += countChange;
    if (character.HeroObject.IsWounded)
    {
    _totalWoundedHeroes += countChange;
    }
    if (countChange != 0)
    {
    flag = true;
    }
    }
    else
    {
    _totalWoundedRegulars += woundedCountChange;
    _totalRegulars += countChange;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:325: TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCountsAtIndex", 415);
    }
    data[index].WoundedNumber += woundedCountChange;
    if (xpChange != 0)
    {
    SetElementXp(index, data[index].Xp + xpChange);
    }
    if (isHero)
    {
    _totalHeroes += countChange;
    if (character.HeroObject.IsWounded)
    {
    _totalWoundedHeroes += countChange;
    }
    if (countChange != 0)
    {
    flag = true;
    }
    }
    else
    {
    _totalWoundedRegulars += woundedCountChange;
    _totalRegulars += countChange;
    }
    if (removeDepleted && data[index].Number == 0)
    {
    RemoveRange(index, index + 1);
    index = -1;
    }
    if (OwnerParty != null && isHero)
    {
    if (countChange > 0)
    {
    OwnerParty.OnHeroAdded(character.HeroObject, this);
    }
    else if (countChange < 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:453: AddToCountsAtIndex(num, count, woundedCount, xpChange, removeDepleted);
    return num;
    }
    
    public int GetTroopCount(CharacterObject troop)
    {
    int num = FindIndexOfTroop(troop);
    if (num >= 0)
    {
    return data[num].Number;
    }
    return 0;
    }
    
    public void RemoveZeroCounts()
    {
    int num = 0;
    for (int i = 0; i < _count; i++)
    {
    if (data[i].Number > 0)
    {
    if (num != i)
    {
    data[num] = data[i];
    }
    num++;
    }
    }
    for (int j = num; j < _count; j++)
    {
    data[j].Clear();
    }
    _count = num;
    UpdateVersion();
    }
    
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:663: AddToCountsAtIndex(num, -data[num].Number, -data[num].WoundedNumber);
    }
    }
    
    public void RemoveTroop(CharacterObject troop, int numberToRemove = 1, UniqueTroopDescriptor troopSeed = default(UniqueTroopDescriptor), int xp = 0)
    {
    int index = FindIndexOfTroop(troop);
    bool removeDepleted = true;
    if (PlayerEncounter.CurrentBattleSimulation != null && !troop.IsHero)
    {
    removeDepleted = false;
    }
    AddToCountsAtIndex(index, -numberToRemove, 0, (!troop.IsHero) ? (-xp) : 0, removeDepleted);
    }
    
    public void WoundTroop(CharacterObject troop, int numberToWound = 1, UniqueTroopDescriptor troopSeed = default(UniqueTroopDescriptor))
    {
    int index = FindIndexOfTroop(troop);
    AddToCountsAtIndex(index, 0, numberToWound);
    }
    
    void ISerializableObject.SerializeTo(IWriter writer)
    {
    writer.WriteInt(Count);
    writer.WriteInt(VersionNo);
    if (data != null)
    {
    writer.WriteInt(data.Length);
    TroopRosterElement[] array = data;
    foreach (TroopRosterElement troopRosterElement in array)
    {
    writer.WriteSerializableObject(troopRosterElement);
    }
    }
    else
    {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:675: AddToCountsAtIndex(index, -numberToRemove, 0, (!troop.IsHero) ? (-xp) : 0, removeDepleted);
    }
    
    public void WoundTroop(CharacterObject troop, int numberToWound = 1, UniqueTroopDescriptor troopSeed = default(UniqueTroopDescriptor))
    {
    int index = FindIndexOfTroop(troop);
    AddToCountsAtIndex(index, 0, numberToWound);
    }
    
    void ISerializableObject.SerializeTo(IWriter writer)
    {
    writer.WriteInt(Count);
    writer.WriteInt(VersionNo);
    if (data != null)
    {
    writer.WriteInt(data.Length);
    TroopRosterElement[] array = data;
    foreach (TroopRosterElement troopRosterElement in array)
    {
    writer.WriteSerializableObject(troopRosterElement);
    }
    }
    else
    {
    writer.WriteInt(0);
    }
    }
    
    void ISerializableObject.DeserializeFrom(IReader reader)
    {
    _count = reader.ReadInt();
    VersionNo = reader.ReadInt();
    int num = reader.ReadInt();
    data = new TroopRosterElement[num];
    for (int i = 0; i < num; i++)
    {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:681: AddToCountsAtIndex(index, 0, numberToWound);
    }
    
    void ISerializableObject.SerializeTo(IWriter writer)
    {
    writer.WriteInt(Count);
    writer.WriteInt(VersionNo);
    if (data != null)
    {
    writer.WriteInt(data.Length);
    TroopRosterElement[] array = data;
    foreach (TroopRosterElement troopRosterElement in array)
    {
    writer.WriteSerializableObject(troopRosterElement);
    }
    }
    else
    {
    writer.WriteInt(0);
    }
    }
    
    void ISerializableObject.DeserializeFrom(IReader reader)
    {
    _count = reader.ReadInt();
    VersionNo = reader.ReadInt();
    int num = reader.ReadInt();
    data = new TroopRosterElement[num];
    for (int i = 0; i < num; i++)
    {
    data[i] = (TroopRosterElement)(object)reader.ReadSerializableObject();
    }
    }
    
    public int Sum(Func<TroopRosterElement, int> selector)
    {

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '[xml]$xml = Get-Content -Path Main'"\\_Module\\ModuleData\\taom_partyTemplates.xml; foreach("'$pt in $xml.partyTemplates.MBPartyTemplate){ $troops=@(); foreach($s in $pt.stacks.PartyTemplateStack){ $troops += [string]$s.troop }; $dups=$troops | Group-Object | Where-Object Count -gt 1; if($dups){ '"'{0}: {1}' -f "'$pt.id, (($dups | ForEach-Object { $_.Name + '"' x' + "'$_.Count }) -join '"', ') } }" in C:\Users\mikew\source\repos\TAOM
2026-05-27T21:52:26.201539Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:
[31;1mMetadataError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1m[xml]$xml = Get-Content -Path Main\_Module\ModuleData\taom_partyTempl[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot convert value "System.Object[]" to type "System.Xml.XmlDocument". Error: "An XML comment cannot contain '--', and '-' cannot be the last character. Line 1482, position 42."[0m

 exited 1 in 945ms:
[31;1mMetadataError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1m[xml]$xml = Get-Content -Path Main\_Module\ModuleData\taom_partyTempl[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot convert value "System.Object[]" to type "System.Xml.XmlDocument". Error: "An XML comment cannot contain '--', and '-' cannot be the last character. Line 1482, position 42."[0m

codex
I found one cold-review issue already: the new party template XML does not parse because an XML comment contains a literal `--`. That is independent of the known suspects and would block data loading before the runtime scaling paths matter.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '[xml]$xml = Get-Content -Path Main'"\\_Module\\ModuleData\\taom_spcultures.xml; 'taom_spcultures.xml parsed OK'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\_Module\\ModuleData\\taom_partyTemplates.xml'; "'$lines=Get-Content -Path $p; for($i=1478;$i -le 1486;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern '<"'!--.*--.*-->'"' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '[xml]$xml = Get-Content -Path Main'"\\_Module\\ModuleData\\taom_module_strings.xml; 'taom_module_strings.xml parsed OK'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 883ms:
1478: 	<!-- LOTR Bandit Cultures - Raider + Boss Templates -->
1479: 	<!-- ============================================== -->
1480: 	<!-- Replaces vanilla forest/mountain/desert/steppe/sea bandit cultures with lore-appropriate
1481: 	     LOTR factions. Each culture has two templates:
1482: 	       {culture}_raider_party_template  -- regular bandit warbands roaming the map
1483: 	       {culture}_boss_party_template    -- hideout boss + defenders
1484: 	     Troop IDs pulled from each culture's existing troop XML (no new troop defs needed).
1485: 	     Tiered T1-T4 to keep raider feel; bandit count scales further via TaomBanditDensityModel
1486: 	     + Patch39_BanditPartySize at runtime.

 succeeded in 926ms:
taom_spcultures.xml parsed OK

 succeeded in 898ms:
taom_module_strings.xml parsed OK

 succeeded in 1210ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\SubModule.cs'; "'$lines=Get-Content -Path $p; for($i=345;$i -le 360;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\BanditManagement\\Hooks\\Patch39_BanditPartySize.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\BanditManagement\\BanditScalingSettingsProvider.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\BanditManagement\\Models\\TaomBanditDensityModel.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 873ms:
345:             campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
346:             campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
347:             campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
348:             campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
349:             campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
350:             campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
351:             campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
352:             campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
353:             campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
354:             campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
355:             campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
356:             campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
357:             campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
358:             campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
359:             campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
360:             campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));

 succeeded in 1229ms:
1: using TAOM.Features;
2: 
3: namespace TAOM.Features.BanditManagement;
4: 
5: public sealed class BanditScalingSettingsProvider : IBanditScalingSettingsProvider
6: {
7:     private readonly BanditScalingConfig _defaults;
8: 
9:     public BanditScalingSettingsProvider(IBanditScalingConfigProvider configProvider)
10:     {
11:         _defaults = configProvider.GetConfig();
12:     }
13: 
14:     public bool IsEnabled => TaomSettings.Instance?.EnableBanditScaling ?? true;
15: 
16:     public float DensityCurve =>
17:         SafeClamp(TaomSettings.Instance?.BanditDensityCurve, _defaults.DensityCurve, 0f, 5f);
18: 
19:     public float PartySizeCurve =>
20:         SafeClamp(TaomSettings.Instance?.BanditPartySizeCurve, _defaults.PartySizeCurve, 0f, 5f);
21: 
22:     public float BossFightCurve =>
23:         SafeClamp(TaomSettings.Instance?.BanditBossFightCurve, _defaults.BossFightCurve, 0f, 5f);
24: 
25:     public int MaxHideoutsPerFactionCap =>
26:         SafeClampInt(TaomSettings.Instance?.BanditMaxHideoutsPerFaction, _defaults.MaxHideoutsPerFactionCap, 1, 100);
27: 
28:     public int MaxPartiesPerHideoutCap =>
29:         SafeClampInt(TaomSettings.Instance?.BanditMaxPartiesPerHideout, _defaults.MaxPartiesPerHideoutCap, 1, 20);
30: 
31:     // No MCM knob for MinPartiesToInfest -- it's a JSON-only advanced tuning value with a strict
32:     // upper bound derived from the live MCM cap (not the JSON default), so the invariant
33:     // min <= max holds at runtime even if the user lowers BanditMaxPartiesPerHideout in MCM.
34:     public int MinPartiesToInfest
35:     {
36:         get
37:         {
38:             var cap = MaxPartiesPerHideoutCap;
39:             var v = _defaults.MinPartiesToInfest;
40:             if (v < 1) v = 1;
41:             if (v > cap) v = cap;
42:             return v;
43:         }
44:     }
45: 
46:     private static float SafeClamp(float? value, float defaultValue, float min, float max)
47:     {
48:         var v = value ?? defaultValue;
49:         if (float.IsNaN(v) || float.IsInfinity(v)) return defaultValue;
50:         return v < min ? min : v > max ? max : v;
51:     }
52: 
53:     private static int SafeClampInt(int? value, int defaultValue, int min, int max)
54:     {
55:         var v = value ?? defaultValue;
56:         return v < min ? min : v > max ? max : v;
57:     }
58: }

 succeeded in 1576ms:
1: using TaleWorlds.CampaignSystem;
2: using TaleWorlds.CampaignSystem.GameComponents;
3: using TaleWorlds.Library;
4: 
5: namespace TAOM.Features.BanditManagement.Models;
6: 
7: /// <summary>
8: /// Overrides vanilla <see cref="DefaultBanditDensityModel"/> to scale hideout density +
9: /// boss-fight troop counts by PlayerProgress * MCM curves. Vanilla is the floor — bandit
10: /// scaling never reduces difficulty below vanilla, only amplifies it as the campaign
11: /// progresses.
12: ///
13: /// Per gamemodels.md rule: this class is a thin entry — every property delegates to the
14: /// service or returns a single expression. No <c>if</c>/<c>foreach</c>/branching here.
15: /// </summary>
16: public class TaomBanditDensityModel : DefaultBanditDensityModel
17: {
18:     private readonly IBanditScalingService _scaling;
19: 
20:     public TaomBanditDensityModel(IBanditScalingService scaling)
21:     {
22:         _scaling = scaling;
23:     }
24: 
25:     public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt =>
26:         _scaling.IsEnabled
27:             ? _scaling.MinPartiesToInfest
28:             : base.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
29: 
30:     public override int NumberOfMaximumHideoutsAtEachBanditFaction =>
31:         _scaling.IsEnabled
32:             ? Cap(base.NumberOfMaximumHideoutsAtEachBanditFaction, _scaling.GetDensityMultiplier(GetPlayerProgress()), _scaling.MaxHideoutsPerFactionCap)
33:             : base.NumberOfMaximumHideoutsAtEachBanditFaction;
34: 
35:     public override int NumberOfMaximumBanditPartiesInEachHideout =>
36:         _scaling.IsEnabled
37:             ? Cap(base.NumberOfMaximumBanditPartiesInEachHideout, _scaling.GetDensityMultiplier(GetPlayerProgress()), _scaling.MaxPartiesPerHideoutCap)
38:             : base.NumberOfMaximumBanditPartiesInEachHideout;
39: 
40:     public override int NumberOfMaximumTroopCountForFirstFightInHideout =>
41:         _scaling.IsEnabled
42:             ? Scale(base.NumberOfMaximumTroopCountForFirstFightInHideout, _scaling.GetBossFightMultiplier(GetPlayerProgress()))
43:             : base.NumberOfMaximumTroopCountForFirstFightInHideout;
44: 
45:     public override int NumberOfMaximumTroopCountForBossFightInHideout =>
46:         _scaling.IsEnabled
47:             ? Scale(base.NumberOfMaximumTroopCountForBossFightInHideout, _scaling.GetBossFightMultiplier(GetPlayerProgress()))
48:             : base.NumberOfMaximumTroopCountForBossFightInHideout;
49: 
50:     // Helpers stay private + branch-free; per gamemodels.md, the property bodies above hold the
51:     // ternary which is allowed (it's a single conditional expression, not a multi-line block).
52:     private static int Cap(int baseValue, float multiplier, int hardCap)
53:     {
54:         var scaled = (int)MathF.Round(baseValue * multiplier);
55:         return scaled < baseValue ? baseValue : scaled > hardCap ? hardCap : scaled;
56:     }
57: 
58:     private static int Scale(int baseValue, float multiplier)
59:     {
60:         var scaled = (int)MathF.Round(baseValue * multiplier);
61:         return scaled < baseValue ? baseValue : scaled;
62:     }
63: 
64:     private static float GetPlayerProgress() => Campaign.Current?.PlayerProgress ?? 0f;
65: }

 succeeded in 1936ms:
1: using HarmonyLib;
2: using TaleWorlds.CampaignSystem;
3: using TaleWorlds.CampaignSystem.GameComponents;
4: using TaleWorlds.CampaignSystem.Party;
5: using TaleWorlds.CampaignSystem.Roster;
6: using TaleWorlds.Library;
7: 
8: namespace TAOM.Features.BanditManagement.Hooks;
9: 
10: /// <summary>
11: /// Patch39 — Postfix on <see cref="DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty"/>.
12: ///
13: /// Vanilla flow: <c>FindAppropriateInitialRoster</c> consults the (private) ratio function which
14: /// returns 0.4..1.2 for bandit parties, then for each <see cref="PartyTemplateStack"/> rolls
15: /// <c>num = MinValue + (MaxValue - MinValue) * ratio</c> as the troop count. Vanilla asserts
16: /// <c>ratio &lt;= 1.0</c>, so we cannot simply Postfix the ratio.
17: ///
18: /// Our scaling: after the roster is built, walk each stack, scale its troop count UP by
19: /// <see cref="IBanditScalingService.GetPartySizeMultiplier"/>, and cap at the stack's
20: /// <c>MaxValue</c>. That respects the upper bound vanilla party templates already encode
21: /// while letting endgame bandit parties hit full templated strength reliably instead of
22: /// the random vanilla draw.
23: ///
24: /// Non-bandit parties (player, lords, caravans, villagers, patrols) pass through untouched.
25: /// </summary>
26: [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty))]
27: public static class Patch39_BanditPartySize
28: {
29:     private static IBanditScalingService _service;
30: 
31:     private static IBanditScalingService GetService() =>
32:         _service ??= TAOM.IoC.Resolve<IBanditScalingService>();
33: 
34:     [HarmonyPostfix]
35:     public static void Postfix(ref TroopRoster __result, MobileParty party, PartyTemplateObject partyTemplate)
36:     {
37:         if (__result == null || party == null || partyTemplate == null) return;
38:         if (!party.IsBandit) return;
39: 
40:         var service = GetService();
41:         if (service == null || !service.IsEnabled) return;
42: 
43:         var playerProgress = Campaign.Current?.PlayerProgress ?? 0f;
44:         var multiplier = service.GetPartySizeMultiplier(playerProgress);
45:         if (multiplier <= 1f) return;
46: 
47:         var stacks = partyTemplate.Stacks;
48:         if (stacks == null) return;
49: 
50:         for (int i = 0; i < stacks.Count; i++)
51:         {
52:             var stack = stacks[i];
53:             var character = stack.Character;
54:             if (character == null) continue;
55: 
56:             var current = __result.GetTroopCount(character);
57:             if (current <= 0) continue;
58: 
59:             var scaled = MathF.Round(current * multiplier);
60:             if (scaled > stack.MaxValue) scaled = stack.MaxValue;
61: 
62:             var delta = scaled - current;
63:             if (delta > 0)
64:                 __result.AddToCounts(character, delta);
65:         }
66:     }
67: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign,E:\\Decompiled_Bannerlord\\Modules -Recurse -Filter *.cs | Select-String -Pattern 'FindAppropriateInitialRosterForMobileParty\\(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign,E:\\Decompiled_Bannerlord\\Modules -Recurse -Filter *.cs | Select-String -Pattern 'FillPartyManuallyAfterCreation|CreateBanditParty|CreateLooterParty' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign,E:\\Decompiled_Bannerlord\\Modules -Recurse -Filter *.cs | Select-String -Pattern 'TWParallel|Parallel\\.For|Thread|Task.Run|FindAppropriateInitialRosterForMobileParty' | ForEach-Object { if("'$_.Line -match '"'FindAppropriateInitialRosterForMobileParty|TWParallel|Parallel\\.For|Task.Run|Thread') { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
 succeeded in 2638ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PatrolPartiesCampaignBehavior.cs:113: TroopRoster troopRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplateForPatrolParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\PartySizeLimitModel.cs:28: public abstract TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs:427: public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:2638: TroopRoster memberRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(this, pt);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents\StoryModePartySizeLimitModel.cs:41: public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents\StoryModePartySizeLimitModel.cs:43: return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplate);

 succeeded in 2641ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:294: public static void FillPartyManuallyAfterCreation(MobileParty mobileParty, PartyTemplateObject partyTemplate, int desiredMenCount)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:325: MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(clan.StringId + "_1", clan, hideoutComponent, isBanditBossParty, pt, hideoutComponent.Owner.Settlement.GatePosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:462: MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(selectedFaction.StringId + "_1", selectedFaction, settlement, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:471: MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(selectedFaction.StringId + "_1", selectedFaction, hideout, isBossParty: false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:178: MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(DeserterClan.StringId + "_1", DeserterClan, settlement, isBossParty: false, null, deserterSpawnPosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:2902: MobileParty mobileParty = BanditPartyComponent.CreateBanditParty("incident_tiller_and_wanderer_bandit_revenge_" + i, clan, hideout, isBossParty: false, null, initialPosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:555: _banditParty = BanditPartyComponent.CreateBanditParty("caravan_ambush_quest_" + clan.Name, clan, hideout.Settlement.Hideout, isBossParty: false, partyTemplateObject, _targetSettlement.GatePosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:690: MobilePartyHelper.FillPartyManuallyAfterCreation(mobileParty, randomCaravanTemplate, CaravanPartyTroopCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1264: _questBanditMobileParty = BanditPartyComponent.CreateBanditParty("escort_caravan_quest_" + base.StringId, clan, closestHideout.Settlement.Hideout, isBossParty: false, partyTemplateObject, _questCaravanMobileParty.TargetSettlement.GatePosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:949: _deserterMobileParty = BanditPartyComponent.CreateLooterParty("ebdi_deserters_party_1_new", clan, QuestSettlement, isBossParty: false, null, initialPosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:964: _deserterMobileParty = BanditPartyComponent.CreateBanditParty("ebdi_deserters_party_1", clan2, hideout, isBossParty: false, null, initialPosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:969: MobilePartyHelper.FillPartyManuallyAfterCreation(_deserterMobileParty, defaultPartyTemplate, DeserterPartyMenCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:1036: MobilePartyHelper.FillPartyManuallyAfterCreation(_defenderMobileParty, QuestSettlement.Culture.MilitiaPartyTemplate, DefenderPartyMenCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandLordCompanyOfTroubleIssueBehavior.cs:690: _companyOfTroubleParty = BanditPartyComponent.CreateBanditParty("company_of_trouble_" + base.StringId, settlement.OwnerClan, settlement.Hideout, isBossParty: false, null, MobileParty.MainParty.Position);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccessToVillageCommonsIssueBehavior.cs:695: _rivalMobileParty = BanditPartyComponent.CreateLooterParty("villagers_of_landlord_needs_access_to_village_common_quest" + _questId, clan, _targetSettlement, isBossParty: false, null, _targetSettlement.GatePosition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\SmugglersIssueBehavior.cs:778: MobilePartyHelper.FillPartyManuallyAfterCreation(mobileParty, randomCaravanTemplate, desiredMenCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs:106: public static MobileParty CreateBanditParty(string stringId, Clan clan, Hideout hideout, bool isBossParty, PartyTemplateObject pt, CampaignVec2 initialPosition)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs:117: public static MobileParty CreateLooterParty(string stringId, Clan clan, Settlement relatedSettlement, bool isBossParty, PartyTemplateObject pt, CampaignVec2 initialPosition)
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\SnareTheWealthyIssueBehavior.cs:902: MobilePartyHelper.FillPartyManuallyAfterCreation(_caravanParty, randomCaravanTemplate, CaravanPartyTroopCount);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\SnareTheWealthyIssueBehavior.cs:916: MobilePartyHelper.FillPartyManuallyAfterCreation(_gangParty, val3, GangPartyTroopCount);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.FirstPhase\ArzagosBannerPieceQuest.cs:170: MobileParty obj = BanditPartyComponent.CreateBanditParty("arzagos_banner_piece_quest_raider_party_" + number, hideoutClan, _hideout.Hideout, false, (PartyTemplateObject)null, _hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.FirstPhase\IstianasBannerPieceQuest.cs:174: MobileParty obj = BanditPartyComponent.CreateBanditParty("istiana_banner_piece_quest_raider_party_" + number, hideoutClan, _hideout.Hideout, false, (PartyTemplateObject)null, _hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.PlayerClanQuests\RescueFamilyQuestBehavior.cs:341: MobileParty obj = BanditPartyComponent.CreateBanditParty("rescue_family_quest_raider_party_" + number, val, _hideout.Hideout, isBanditBossParty, (PartyTemplateObject)null, _hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\ConspiracyBaseOfOperationsDiscoveredConspiracyQuest.cs:267: MobileParty obj = BanditPartyComponent.CreateBanditParty("conspiracy_discovered_quest_raider_party_" + partyIndex, hideout.OwnerClan, hideout.Hideout, isBanditBossParty, (PartyTemplateObject)null, hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\DestroyRaidersConspiracyQuest.cs:432: MobileParty val2 = BanditPartyComponent.CreateBanditParty(string.Concat(obj), _banditFaction, hideout.Hideout, false, val, hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.TutorialPhase\FindHideoutTutorialQuest.cs:260: MobileParty obj = BanditPartyComponent.CreateBanditParty("radagos_raider_party_" + number, _hideout.OwnerClan, _hideout.Hideout, isBanditBossParty, (PartyTemplateObject)null, _hideout.GatePosition);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.TutorialPhase\LocateAndRescueTravellerTutorialQuest.cs:98: MobileParty val3 = BanditPartyComponent.CreateBanditParty("locate_and_rescue_traveller_quest_raider_party_" + _raiderPartyCount, settlement.OwnerClan, settlement.Hideout, false, (PartyTemplateObject)null, val2);

 succeeded in 2623ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:72: TWParallel.For(0, _currentFrameToTickListFlattened.Count, delegate(int startInclusive, int endExclusive)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:3: using System.Threading;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:74: private readonly TWParallel.ParallelForAuxPredicate _parallelInitializeCachedPartyVariablesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:76: private readonly TWParallel.ParallelForAuxPredicate _parallelCacheTargetPartyVariablesAtFrameStartPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:78: private readonly TWParallel.ParallelForAuxPredicate _parallelArrangePartyIndicesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:80: private readonly TWParallel.ParallelForAuxPredicate _parallelTickMovingArmiesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:82: private readonly TWParallel.ParallelForAuxPredicate _parallelTickTransitioningArmiesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:84: private readonly TWParallel.ParallelForAuxPredicate _parallelTickTransitioningPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:86: private readonly TWParallel.ParallelForAuxPredicate _parallelTickMovingPartiesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:88: private readonly TWParallel.ParallelForAuxPredicate _parallelTickStationaryPartiesPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:90: private readonly TWParallel.ParallelForAuxPredicate _parallelCheckExitingSettlementsPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:92: private readonly TWParallel.ParallelForAuxPredicate _parallelTickStationaryArmyLeaderPredicate;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:306: TWParallel.For(0, Campaign.Current.MobileParties.Count, _parallelCheckExitingSettlementsPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:320: TWParallel.For(0, count, _parallelInitializeCachedPartyVariablesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:321: TWParallel.For(0, count, _parallelCacheTargetPartyVariablesAtFrameStartPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:322: TWParallel.For(0, count, _parallelArrangePartyIndicesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:323: TWParallel.For(0, _currentFrameMovingArmyLeaderCount + 1, _parallelTickMovingArmiesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:324: TWParallel.For(0, _currentFrameTransitioningArmyLeaderCount + 1, _parallelTickTransitioningArmiesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:325: TWParallel.For(0, _currentFrameMovingPartyCount + 1, _parallelTickMovingPartiesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:326: TWParallel.For(0, _currentFrameTransitioningCount + 1, _parallelTickTransitioningPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:327: TWParallel.For(0, _currentFrameStationaryArmyLeaderCount + 1, _parallelTickStationaryArmyLeaderPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:328: TWParallel.For(0, _currentFrameStationaryPartyCount + 1, _parallelTickStationaryPartiesPredicate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PatrolPartiesCampaignBehavior.cs:113: TroopRoster troopRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplateForPatrolParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\PartySizeLimitModel.cs:28: public abstract TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs:427: public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs:446: Debug.FailedAssert("initialPartySizeRatio should not be above 1", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultPartySizeLimitModel.cs", "FindAppropriateInitialRosterForMobileParty", 538);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:4: using System.Threading;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:2638: TroopRoster memberRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(this, pt);
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs:4: using System.Threading;
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:57: val.Instance = new DelegateDefinition(UpdateThinkingAboutMoveOnSeparateThread);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:126: private void UpdateThinkingAboutMoveOnSeparateThread()
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:130: CalculatePreMovementStageOnSeparateThread();
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:134: CalculateMovementStageMoveOnSeparateThread();
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:161: private void CalculatePreMovementStageOnSeparateThread()
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:163: if (OnBeginSeparateThread())
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:166: OnExitSeparateThread(calculatedMove);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:170: private void CalculateMovementStageMoveOnSeparateThread()
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:172: if (OnBeginSeparateThread())
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:175: OnExitSeparateThread(calculatedMove);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:179: private bool OnBeginSeparateThread()
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI\BoardGameAIBase.cs:197: private void OnExitSeparateThread(Move calculatedMove)
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\ConversationAnimationToolCampaignBehavior.cs:279: Imgui.BeginMainThreadScope();
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\ConversationAnimationToolCampaignBehavior.cs:335: Imgui.EndMainThreadScope();
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents\StoryModePartySizeLimitModel.cs:41: public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents\StoryModePartySizeLimitModel.cs:43: return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplate);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\PatrolPartiesCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=100;$i -le 120;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs'; "'$lines=Get-Content -Path $p; for($i=2610;$i -le 2655;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party.PartyComponents\\BanditPartyComponent.cs'; "'$lines=Get-Content -Path $p; for($i=100;$i -le 130;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 834ms:
100: 
101: 	internal static object AutoGeneratedGetMemberValue_relatedSettlement(object o)
102: 	{
103: 		return ((BanditPartyComponent)o)._relatedSettlement;
104: 	}
105: 
106: 	public static MobileParty CreateBanditParty(string stringId, Clan clan, Hideout hideout, bool isBossParty, PartyTemplateObject pt, CampaignVec2 initialPosition)
107: 	{
108: 		InitializationArgs args = new InitializationArgs(clan, pt, initialPosition);
109: 		return MobileParty.CreateParty(stringId, new BanditPartyComponent(hideout, isBossParty, args));
110: 	}
111: 
112: 	public static void ConvertPartyToBanditParty(MobileParty mobileParty, Clan clan, Hideout hideout, bool isBossParty)
113: 	{
114: 		mobileParty.SetPartyComponent(new BanditPartyComponent(hideout, isBossParty, null));
115: 	}
116: 
117: 	public static MobileParty CreateLooterParty(string stringId, Clan clan, Settlement relatedSettlement, bool isBossParty, PartyTemplateObject pt, CampaignVec2 initialPosition)
118: 	{
119: 		InitializationArgs args = new InitializationArgs(clan, pt, initialPosition);
120: 		return MobileParty.CreateParty(stringId, new BanditPartyComponent(relatedSettlement, args));
121: 	}
122: 
123: 	public static void ConvertPartyToLooterParty(MobileParty mobileParty, Clan clan, Settlement relatedSettlement)
124: 	{
125: 		mobileParty.SetPartyComponent(new BanditPartyComponent(relatedSettlement, null));
126: 	}
127: 
128: 	protected BanditPartyComponent(Hideout hideout, bool isBossParty, InitializationArgs args)
129: 	{
130: 		Hideout = hideout;

 succeeded in 878ms:
2610: 	{
2611: 		IsCurrentlyAtSea = !position.IsOnLand;
2612: 		CreateFigure(position);
2613: 		SetMoveModeHold();
2614: 	}
2615: 
2616: 	public void InitializeMobilePartyAtPosition(TroopRoster memberRoster, TroopRoster prisonerRoster, CampaignVec2 position, bool isNaval = false)
2617: 	{
2618: 		InitializeMobilePartyWithRosterInternal(memberRoster, prisonerRoster, position);
2619: 	}
2620: 
2621: 	public void InitializeMobilePartyAroundPosition(TroopRoster memberRoster, TroopRoster prisonerRoster, CampaignVec2 position, float spawnRadius, float minSpawnRadius = 0f, bool isNaval = false)
2622: 	{
2623: 		if (spawnRadius > 0f)
2624: 		{
2625: 			NavigationType navigationCapability = ((!isNaval) ? NavigationType.Default : NavigationType.Naval);
2626: 			position = NavigationHelper.FindReachablePointAroundPosition(position, navigationCapability, spawnRadius, minSpawnRadius);
2627: 		}
2628: 		InitializeMobilePartyWithRosterInternal(memberRoster, prisonerRoster, position);
2629: 	}
2630: 
2631: 	public void InitializeMobilePartyAtPosition(PartyTemplateObject pt, CampaignVec2 position)
2632: 	{
2633: 		InitializeMobilePartyWithPartyTemplate(pt, position);
2634: 	}
2635: 
2636: 	private void InitializeMobilePartyWithPartyTemplate(PartyTemplateObject pt, CampaignVec2 position)
2637: 	{
2638: 		TroopRoster memberRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(this, pt);
2639: 		foreach (Ship item in Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialShipsForMobileParty(this, pt))
2640: 		{
2641: 			ChangeShipOwnerAction.ApplyByMobilePartyCreation(Party, item);
2642: 		}
2643: 		InitializeMobilePartyWithRosterInternal(memberRoster, null, position);
2644: 	}
2645: 
2646: 	private void InitializeMobilePartyWithRosterInternal(TroopRoster memberRoster, TroopRoster prisonerRoster, CampaignVec2 position)
2647: 	{
2648: 		MemberRoster.Add(memberRoster);
2649: 		if (prisonerRoster != null)
2650: 		{
2651: 			PrisonRoster.Add(prisonerRoster);
2652: 		}
2653: 		InitializeMobilePartyAtPosition(position);
2654: 	}
2655: 

 succeeded in 1233ms:
100: 			{
101: 				ReplenishParty(mobileParty);
102: 			}
103: 		}
104: 		if (mobileParty.CurrentSettlement == null && mobileParty.TargetSettlement == mobileParty.HomeSettlement && mobileParty.DefaultBehavior == AiBehavior.GoToSettlement && !mobileParty.TargetSettlement.IsUnderSiege)
105: 		{
106: 			mobileParty.Ai.SetInitiative(0.1f, 1f, 1f);
107: 		}
108: 	}
109: 
110: 	private void ReplenishParty(MobileParty party)
111: 	{
112: 		PartyTemplateObject partyTemplateForPatrolParty = Campaign.Current.Models.SettlementPatrolModel.GetPartyTemplateForPatrolParty(party.CurrentSettlement, party.PatrolPartyComponent.IsNaval);
113: 		TroopRoster troopRoster = Campaign.Current.Models.PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplateForPatrolParty);
114: 		party.MemberRoster.Clear();
115: 		party.MemberRoster.Add(troopRoster);
116: 		SortRoster(party);
117: 	}
118: 
119: 	private void SortRoster(MobileParty mobileParty)
120: 	{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\IoC.cs'; "'$lines=Get-Content -Path $p; for($i=70;$i -le 105;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\SubModule.cs -Pattern 'IoC\\.Initialize|BuildContainer|CreateGameModels|AddGameModels|OnGameStart|AddModel|Register' -Context 2,4 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\BanditManagement\\BanditManagementIoC.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 793ms:
70:         TroopProgressionIoC.RegisterTroopProgressionFeature(container);
71:         Features.Arena.ArenaIoC.RegisterArenaFeature(container);
72:         FactionMapIoC.RegisterFactionMapFeature(container);
73:         CharacterCreationIoC.RegisterCharacterCreationFeature(container);
74:         InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
75:         DiplomacyIoC.RegisterDiplomacyFeature(container);
76:         RaceAgeIoC.RegisterRaceAgeFeature(container);
77:         ExecutionIoC.RegisterExecutionFeature(container);
78:         EncyclopediaIoC.RegisterEncyclopediaFeature(container);
79:         StartupResourcesIoC.RegisterStartupResourcesFeature(container);
80:         TroopWeightIoC.RegisterTroopWeightFeature(container);
81:         AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
82:         WargIoC.RegisterWargFeature(container);
83:         // DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
84:         // SpiderIoC.RegisterSpiderFeature(container);
85:         CustomBattlesIoC.RegisterCustomBattlesFeature(container);
86:         BattleBalanceIoC.RegisterBattleBalanceFeature(container);
87:         MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
88:         ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
89:         SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
90:         ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
91:         TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
92:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
93:         CareerSystemIoC.RegisterCareerSystemFeature(container);
94:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
95:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
96:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
97:         BanditManagementIoC.RegisterBanditManagementFeature(container);
98:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
99:         MixedFormationsIoC.RegisterMixedFormationsFeature(container);
100:         SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
101:         QuickActionsIoC.RegisterQuickActionsFeature(container);
102:         MessengerIoC.RegisterMessengerFeature(container);
103:         EquipPresetsIoC.RegisterEquipPresetsFeature(container);
104:         CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
105:         FiefManagementIoC.RegisterFiefManagementFeature(container);

 succeeded in 1191ms:
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:121: _uiExtender.Register(typeof(SubModule).Assembly);
    _uiExtender.Enable();
    
    _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();
    
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:136: MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
    logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
    }
    catch (System.Exception ex)
    {
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:137: logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
    }
    catch (System.Exception ex)
    {
    logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:156: DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
    AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
    AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
    DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
    MakePeaceAction_ApplyInternal_Patch.Initialize(logger);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:163: ExecutionIoC.InitializeHooks(executionHook);
    
    TroopWeightIoC.InitializeHooks(
    IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
    IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:165: TroopWeightIoC.InitializeHooks(
    IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
    IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
    IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
    IoC.Resolve<IOnPartyVMPopulatePartyListLabel>());
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:171: CustomBattlesIoC.InitializeHooks(
    IoC.Resolve<IOnGetCustomBattleCommanders>(),
    IoC.Resolve<IOnGetCustomBattleFactions>(),
    IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
    IoC.Resolve<ISideCommanderFilter>(),
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:179: ShaderPrecompilationIoC.InitializeHooks(logger);
    
    _harmony.PatchCategory("Patch22_ArmyTargeting");
    _harmony.PatchCategory("Patch30_MixedFormations");
    // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:267: protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
    base.OnGameStart(game, gameStarterObject);
    
    // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:269: base.OnGameStart(game, gameStarterObject);
    
    // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
    // assembly versions / campaign context. Runs once per session and is
    // idempotent so OnGameStart on save-load doesn't spam.
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:273: // idempotent so OnGameStart on save-load doesn't spam.
    try
    {
    IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
    }
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:278: catch { /* diagnostic is best-effort, never break OnGameStart */ }
    
    if (gameStarterObject is CampaignGameStarter campaignStarter)
    {
    var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:309: campaignStarter.AddModel(new TaomCharacterStatsModel());
    campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
    campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter));
    
    var raceAgeService = IoC.Resolve<IRaceAgeService>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:310: campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
    campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter));
    
    var raceAgeService = IoC.Resolve<IRaceAgeService>();
    var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:311: campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter));
    
    var raceAgeService = IoC.Resolve<IRaceAgeService>();
    var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
    var raceAgeLogger = IoC.Resolve<IModLogger>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:317: campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
    campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
    campaignStarter.AddModel(new TaomHeroCreationModel());
    
    var diplomacyService = IoC.Resolve<IDiplomacyService>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:318: campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
    campaignStarter.AddModel(new TaomHeroCreationModel());
    
    var diplomacyService = IoC.Resolve<IDiplomacyService>();
    var wotrService = IoC.Resolve<IWarOfTheRingService>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:319: campaignStarter.AddModel(new TaomHeroCreationModel());
    
    var diplomacyService = IoC.Resolve<IDiplomacyService>();
    var wotrService = IoC.Resolve<IWarOfTheRingService>();
    var diplomacyLogger = IoC.Resolve<IModLogger>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:325: campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
    campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
    campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
    
    var wotrLogger = IoC.Resolve<IModLogger>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:326: campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
    campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
    
    var wotrLogger = IoC.Resolve<IModLogger>();
    campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:327: campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
    
    var wotrLogger = IoC.Resolve<IModLogger>();
    campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
    
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:335: campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
    
    var executionRelationService = IoC.Resolve<IExecutionRelationService>();
    var playerContext = IoC.Resolve<IPlayerContextAdapter>();
    campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:339: campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
    
    // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
    // ICulturalFeatsService. Each model is now a thin boundary that converts
    // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:345: campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
    campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
    campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
    campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:346: campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
    campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
    campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
    campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:347: campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
    campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
    campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
    campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
    campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:348: campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
    campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
    campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
    campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
    campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:349: campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
    campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
    campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
    campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:350: campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
    campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
    campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
    campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:351: campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
    campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
    campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:352: campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
    campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:353: campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
    campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
    campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
    campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));

 succeeded in 1494ms:
1: using DryIoc;
2: 
3: namespace TAOM.Features.BanditManagement;
4: 
5: public static class BanditManagementIoC
6: {
7:     public static void RegisterBanditManagementFeature(IContainer container)
8:     {
9:         container.Register<IBanditScalingConfigProvider, BanditScalingConfigProvider>(Reuse.Singleton);
10:         container.Register<IBanditScalingSettingsProvider, BanditScalingSettingsProvider>(Reuse.Singleton);
11:         container.Register<IBanditScalingService, BanditScalingService>(Reuse.Singleton);
12:     }
13: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\SubModule.cs'; "'$lines=Get-Content -Path $p; for($i=80;$i -le 110;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 732ms:
80: public class SubModule : MBSubModuleBase
81: {
82:     private Harmony _harmony;
83:     private UIExtender? _uiExtender;
84:     private ITimeAccelerationService? _timeAccelerationService;
85:     private static float _shaderTickAccumulator;
86:     private static int _lastShaderCount = -1;
87:     private static bool _missionTimePatchesApplied;
88: 
89:     protected override void OnSubModuleLoad()
90:     {
91:         base.OnSubModuleLoad();
92: 
93:         IoC.Configure();
94: 
95:         // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
96:         // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
97:         // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
98:         // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
99:         // is the IoC.Configure() call itself — if THAT throws, the entire feature is
100:         // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
101:         // a manual DI container; accept and document the residual.
102:         _harmony = new Harmony("com.taom.mod");
103:         if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
104:         {
105:             try
106:             {
107:                 _harmony.PatchCategory("Patch37_CrashReport");
108:                 IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
109:                 if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
110:                 {

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\settlements.xml' -Pattern 'hideout_.*Culture\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)|hideout_.*Culture\\.(forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders)' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 140' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\settlements.xml'; if(Test-Path "'$path){ Select-String -Path $path -Pattern '"'id=\"hideout_' | ForEach-Object { if("'$_.Line -notmatch '"'type=\"Hideout\"'){ '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } } | Select-Object -First 20 }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\settlements.xml'; if(Test-Path "'$path){ Select-String -Path $path -Pattern '"'id=\"hideout_' | ForEach-Object { if("'$_.Line -notmatch '"'<Settlement\\s+id=\"hideout_[a-z0-9_]+\"\\s+name='){ '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } } | Select-Object -First 20 }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\settlements.xml'; if(Test-Path "'$path){ $content=Get-Content -Path $path -Raw; $all=[regex]::Matches($content,'"'<Settlement\\b["'^>]*'"\\bid=\"hideout_[a-z0-9_]+\"["'^>]*>'"'); "'$hideouts=$all | Where-Object { $_.Value -match '"'type=\"Hideout\"' }; "'$new=$hideouts | Where-Object { $_.Value -match '"'culture=\"Culture\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)\"' }; "'$old=$hideouts | Where-Object { $_.Value -match '"'culture=\"Culture\\.(forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders)\"' }; 'hideout_id_tags={0} hideout_type_tags={1} new_culture={2} old_culture={3}' -f "'$all.Count,$hideouts.Count,$new.Count,$old.Count }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 917ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13084: <Settlement id="hideout_forest_1" name="{=Settlements.Settlement.name.hideout_forest_1}Dunlending Raider's Camp" type="Hideout" posX="773.894" posY="888.486" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13092: <Settlement id="hideout_forest_2" name="{=Settlements.Settlement.name.hideout_forest_2}Dunlending Raider's Camp" type="Hideout" posX="750.418" posY="865.628" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13100: <Settlement id="hideout_forest_3" name="{=Settlements.Settlement.name.hideout_forest_3}Dunlending Raider's Camp" type="Hideout" posX="782.647" posY="835.076" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13108: <Settlement id="hideout_forest_4" name="{=Settlements.Settlement.name.hideout_forest_4}Dunlending Raider's Camp" type="Hideout" posX="767.952" posY="808.494" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13116: <Settlement id="hideout_forest_5" name="{=Settlements.Settlement.name.hideout_forest_5}Dunlending Raider's Camp" type="Hideout" posX="774.284" posY="771.491" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13124: <Settlement id="hideout_forest_6" name="{=Settlements.Settlement.name.hideout_forest_6}Dunlending Raider's Camp" type="Hideout" posX="802.162" posY="721.944" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13132: <Settlement id="hideout_forest_7" name="{=Settlements.Settlement.name.hideout_forest_7}Dunlending Raider's Camp" type="Hideout" posX="722.8" posY="800.752" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13140: <Settlement id="hideout_forest_8" name="{=Settlements.Settlement.name.hideout_forest_8}Dunlending Raider's Camp" type="Hideout" posX="679.509" posY="799.785" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13148: <Settlement id="hideout_forest_9" name="{=Settlements.Settlement.name.hideout_forest_9}Dunlending Raider's Camp" type="Hideout" posX="737.61" posY="827.423" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13156: <Settlement id="hideout_forest_10" name="{=Settlements.Settlement.name.hideout_forest_10}Dunlending Raider's Camp" type="Hideout" posX="705.923" posY="767.518" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13164: <Settlement id="hideout_forest_11" name="{=Settlements.Settlement.name.hideout_forest_11}Dunlending Raider's Camp" type="Hideout" posX="617.395" posY="798.651" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13172: <Settlement id="hideout_forest_12" name="{=Settlements.Settlement.name.hideout_forest_12}Dunlending Raider's Camp" type="Hideout" posX="685.576" posY="828.636" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13180: <Settlement id="hideout_forest_13" name="{=Settlements.Settlement.name.hideout_forest_13}Dunlending Raider's Camp" type="Hideout" posX="571.069" posY="788.418" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13188: <Settlement id="hideout_forest_14" name="{=Settlements.Settlement.name.hideout_forest_14}Dunlending Raider's Camp" type="Hideout" posX="566.718" posY="824.423" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13196: <Settlement id="hideout_forest_15" name="{=Settlements.Settlement.name.hideout_forest_15}Dunlending Raider's Camp" type="Hideout" posX="534.867" posY="816.08" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13204: <Settlement id="hideout_forest_16" name="{=Settlements.Settlement.name.hideout_forest_16}Dunlending Raider's Camp" type="Hideout" posX="580.024" posY="845.865" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13212: <Settlement id="hideout_forest_17" name="{=Settlements.Settlement.name.hideout_forest_17}Dunlending Raider's Camp" type="Hideout" posX="586.851" posY="883.695" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13220: <Settlement id="hideout_forest_18" name="{=Settlements.Settlement.name.hideout_forest_18}Dunlending Raider's Camp" type="Hideout" posX="608.445" posY="927.513" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13228: <Settlement id="hideout_forest_19" name="{=Settlements.Settlement.name.hideout_forest_19}Dunlending Raider's Camp" type="Hideout" posX="607.871" posY="948.146" culture="Culture.dunland_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13236: <Settlement id="hideout_seaside_1" name="{=Settlements.Settlement.name.hideout_seaside_1}Corsair's Cove" type="Hideout" posX="712.379" posY="565.983" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13244: <Settlement id="hideout_seaside_2" name="{=Settlements.Settlement.name.hideout_seaside_2}Corsair's Cove" type="Hideout" posX="767.829" posY="949.151" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13252: <Settlement id="hideout_seaside_3" name="{=Settlements.Settlement.name.hideout_seaside_3}Corsair's Cove" type="Hideout" posX="801.899" posY="1009.042" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13260: <Settlement id="hideout_seaside_4" name="{=Settlements.Settlement.name.hideout_seaside_4}Corsair's Cove" type="Hideout" posX="799.3" posY="1114.713" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13268: <Settlement id="hideout_seaside_5" name="{=Settlements.Settlement.name.hideout_seaside_5}Corsair's Cove" type="Hideout" posX="795.44" posY="1127.314" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13276: <Settlement id="hideout_seaside_6" name="{=Settlements.Settlement.name.hideout_seaside_6}Corsair's Cove" type="Hideout" posX="780.506" posY="1171.037" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13284: <Settlement id="hideout_seaside_7" name="{=Settlements.Settlement.name.hideout_seaside_7}Corsair's Cove" type="Hideout" posX="755.158" posY="1228.213" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13292: <Settlement id="hideout_seaside_8" name="{=Settlements.Settlement.name.hideout_seaside_8}Corsair's Cove" type="Hideout" posX="822.572" posY="1215.833" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13300: <Settlement id="hideout_seaside_9" name="{=Settlements.Settlement.name.hideout_seaside_9}Corsair's Cove" type="Hideout" posX="851.485" posY="1202.49" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13308: <Settlement id="hideout_seaside_10" name="{=Settlements.Settlement.name.hideout_seaside_10}Corsair's Cove" type="Hideout" posX="920.66" posY="1188.131" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13316: <Settlement id="hideout_seaside_11" name="{=Settlements.Settlement.name.hideout_seaside_11}Corsair's Cove" type="Hideout" posX="930.322" posY="1169.05" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13324: <Settlement id="hideout_seaside_12" name="{=Settlements.Settlement.name.hideout_seaside_12}Corsair's Cove" type="Hideout" posX="982.544" posY="1091.008" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13332: <Settlement id="hideout_seaside_13" name="{=Settlements.Settlement.name.hideout_seaside_13}Corsair's Cove" type="Hideout" posX="777.576" posY="971.499" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13340: <Settlement id="hideout_seaside_14" name="{=Settlements.Settlement.name.hideout_seaside_14}Corsair's Cove" type="Hideout" posX="532.741" posY="794.553" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13348: <Settlement id="hideout_seaside_15" name="{=Settlements.Settlement.name.hideout_seaside_15}Corsair's Cove" type="Hideout" posX="601.944" posY="776.922" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13356: <Settlement id="hideout_seaside_16" name="{=Settlements.Settlement.name.hideout_seaside_16}Corsair's Cove" type="Hideout" posX="991.311" posY="828.229" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13364: <Settlement id="hideout_seaside_17" name="{=Settlements.Settlement.name.hideout_seaside_17}Corsair's Cove" type="Hideout" posX="1431.684" posY="827.873" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13372: <Settlement id="hideout_seaside_18" name="{=Settlements.Settlement.name.hideout_seaside_18}Corsair's Cove" type="Hideout" posX="1363.318" posY="469.053" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13380: <Settlement id="hideout_seaside_19" name="{=Settlements.Settlement.name.hideout_seaside_19}Corsair's Cove" type="Hideout" posX="1300.865" posY="245.494" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13388: <Settlement id="hideout_seaside_20" name="{=Settlements.Settlement.name.hideout_seaside_20}Corsair's Cove" type="Hideout" posX="780.419" posY="288.639" culture="Culture.umbar_corsairs">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13396: <Settlement id="hideout_mountain_1" name="{=Settlements.Settlement.name.hideout_mountain_1}Gundabad Orc Raider's Camp" type="Hideout" posX="550.138" posY="646.875" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13404: <Settlement id="hideout_mountain_2" name="{=Settlements.Settlement.name.hideout_mountain_2}Gundabad Orc Raider's Camp" type="Hideout" posX="631.406" posY="670.314" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13412: <Settlement id="hideout_mountain_3" name="{=Settlements.Settlement.name.hideout_mountain_3}Gundabad Orc Raider's Camp" type="Hideout" posX="563.298" posY="667.084" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13420: <Settlement id="hideout_mountain_4" name="{=Settlements.Settlement.name.hideout_mountain_4}Gundabad Orc Raider's Camp" type="Hideout" posX="532.143" posY="702.893" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13428: <Settlement id="hideout_mountain_5" name="{=Settlements.Settlement.name.hideout_mountain_5}Gundabad Orc Raider's Camp" type="Hideout" posX="654.958" posY="696.056" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13436: <Settlement id="hideout_mountain_6" name="{=Settlements.Settlement.name.hideout_mountain_6}Gundabad Orc Raider's Camp" type="Hideout" posX="721.448" posY="660.787" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13444: <Settlement id="hideout_mountain_7" name="{=Settlements.Settlement.name.hideout_mountain_7}Gundabad Orc Raider's Camp" type="Hideout" posX="710.724" posY="622.198" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13452: <Settlement id="hideout_mountain_8" name="{=Settlements.Settlement.name.hideout_mountain_8}Gundabad Orc Raider's Camp" type="Hideout" posX="768.941" posY="645.029" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13460: <Settlement id="hideout_mountain_9" name="{=Settlements.Settlement.name.hideout_mountain_9}Gundabad Orc Raider's Camp" type="Hideout" posX="795.151" posY="646.083" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13468: <Settlement id="hideout_mountain_10" name="{=Settlements.Settlement.name.hideout_mountain_10}Gundabad Orc Raider's Camp" type="Hideout" posX="781.159" posY="582.898" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13476: <Settlement id="hideout_mountain_11" name="{=Settlements.Settlement.name.hideout_mountain_11}Gundabad Orc Raider's Camp" type="Hideout" posX="842.516" posY="591.794" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13484: <Settlement id="hideout_mountain_12" name="{=Settlements.Settlement.name.hideout_mountain_12}Gundabad Orc Raider's Camp" type="Hideout" posX="868.269" posY="625.593" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13492: <Settlement id="hideout_mountain_13" name="{=Settlements.Settlement.name.hideout_mountain_13}Gundabad Orc Raider's Camp" type="Hideout" posX="878.705" posY="607.363" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13500: <Settlement id="hideout_mountain_14" name="{=Settlements.Settlement.name.hideout_mountain_14}Gundabad Orc Raider's Camp" type="Hideout" posX="843.333" posY="554.311" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13508: <Settlement id="hideout_mountain_15" name="{=Settlements.Settlement.name.hideout_mountain_15}Gundabad Orc Raider's Camp" type="Hideout" posX="905.379" posY="538.943" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13516: <Settlement id="hideout_mountain_16" name="{=Settlements.Settlement.name.hideout_mountain_16}Gundabad Orc Raider's Camp" type="Hideout" posX="858.309" posY="520.394" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13524: <Settlement id="hideout_mountain_17" name="{=Settlements.Settlement.name.hideout_mountain_17}Gundabad Orc Raider's Camp" type="Hideout" posX="918.299" posY="653.893" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13532: <Settlement id="hideout_mountain_18" name="{=Settlements.Settlement.name.hideout_mountain_18}Gundabad Orc Raider's Camp" type="Hideout" posX="896.598" posY="669.17" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13540: <Settlement id="hideout_mountain_19" name="{=Settlements.Settlement.name.hideout_mountain_19}Gundabad Orc Raider's Camp" type="Hideout" posX="894.7" posY="700.764" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13548: <Settlement id="hideout_mountain_20" name="{=Settlements.Settlement.name.hideout_mountain_20}Gundabad Orc Raider's Camp" type="Hideout" posX="850.541" posY="711.06" culture="Culture.gundabad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13556: <Settlement id="hideout_desert_1" name="{=Settlements.Settlement.name.hideout_desert_1}Haradrim Raider's Camp" type="Hideout" posX="825.168" posY="391.966" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13564: <Settlement id="hideout_desert_2" name="{=Settlements.Settlement.name.hideout_desert_2}Haradrim Raider's Camp" type="Hideout" posX="860.578" posY="409.573" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13572: <Settlement id="hideout_desert_3" name="{=Settlements.Settlement.name.hideout_desert_3}Haradrim Raider's Camp" type="Hideout" posX="919.318" posY="386.194" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13580: <Settlement id="hideout_desert_4" name="{=Settlements.Settlement.name.hideout_desert_4}Haradrim Raider's Camp" type="Hideout" posX="998.23" posY="409.833" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13588: <Settlement id="hideout_desert_5" name="{=Settlements.Settlement.name.hideout_desert_5}Haradrim Raider's Camp" type="Hideout" posX="1062.571" posY="447.727" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13596: <Settlement id="hideout_desert_6" name="{=Settlements.Settlement.name.hideout_desert_6}Haradrim Raider's Camp" type="Hideout" posX="1135.509" posY="405.314" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13604: <Settlement id="hideout_desert_7" name="{=Settlements.Settlement.name.hideout_desert_7}Haradrim Raider's Camp" type="Hideout" posX="1072.412" posY="374.593" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13612: <Settlement id="hideout_desert_8" name="{=Settlements.Settlement.name.hideout_desert_8}Haradrim Raider's Camp" type="Hideout" posX="1195.96" posY="459.733" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13620: <Settlement id="hideout_desert_9" name="{=Settlements.Settlement.name.hideout_desert_9}Haradrim Raider's Camp" type="Hideout" posX="1228.664" posY="385.537" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13628: <Settlement id="hideout_desert_10" name="{=Settlements.Settlement.name.hideout_desert_10}Haradrim Raider's Camp" type="Hideout" posX="1155.894" posY="326.538" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13636: <Settlement id="hideout_desert_11" name="{=Settlements.Settlement.name.hideout_desert_11}Haradrim Raider's Camp" type="Hideout" posX="1047.286" posY="337.813" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13644: <Settlement id="hideout_desert_12" name="{=Settlements.Settlement.name.hideout_desert_12}Haradrim Raider's Camp" type="Hideout" posX="904.196" posY="315.645" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13652: <Settlement id="hideout_desert_13" name="{=Settlements.Settlement.name.hideout_desert_13}Haradrim Raider's Camp" type="Hideout" posX="1037.77" posY="230.835" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13660: <Settlement id="hideout_desert_14" name="{=Settlements.Settlement.name.hideout_desert_14}Haradrim Raider's Camp" type="Hideout" posX="1162.524" posY="222.471" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13668: <Settlement id="hideout_desert_15" name="{=Settlements.Settlement.name.hideout_desert_15}Haradrim Raider's Camp" type="Hideout" posX="869.585" posY="238.385" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13676: <Settlement id="hideout_desert_16" name="{=Settlements.Settlement.name.hideout_desert_16}Haradrim Raider's Camp" type="Hideout" posX="732.441" posY="154.374" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13684: <Settlement id="hideout_desert_17" name="{=Settlements.Settlement.name.hideout_desert_17}Haradrim Raider's Camp" type="Hideout" posX="707.65" posY="282.982" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13692: <Settlement id="hideout_desert_18" name="{=Settlements.Settlement.name.hideout_desert_18}Haradrim Raider's Camp" type="Hideout" posX="711.022" posY="348.136" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13700: <Settlement id="hideout_desert_19" name="{=Settlements.Settlement.name.hideout_desert_19}Haradrim Raider's Camp" type="Hideout" posX="558.663" posY="175.213" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13708: <Settlement id="hideout_desert_20" name="{=Settlements.Settlement.name.hideout_desert_20}Haradrim Raider's Camp" type="Hideout" posX="657.581" posY="200.323" culture="Culture.harad_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13716: <Settlement id="hideout_steppe_1" name="{=Settlements.Settlement.name.hideout_steppe_1}Rhûn Raider's Camp" type="Hideout" posX="1072.809" posY="824.275" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13724: <Settlement id="hideout_steppe_2" name="{=Settlements.Settlement.name.hideout_steppe_2}Rhûn Raider's Camp" type="Hideout" posX="1139.294" posY="825.568" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13732: <Settlement id="hideout_steppe_3" name="{=Settlements.Settlement.name.hideout_steppe_3}Rhûn Raider's Camp" type="Hideout" posX="1216.1" posY="836.568" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13740: <Settlement id="hideout_steppe_4" name="{=Settlements.Settlement.name.hideout_steppe_4}Rhûn Raider's Camp" type="Hideout" posX="1284.231" posY="857.542" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13748: <Settlement id="hideout_steppe_5" name="{=Settlements.Settlement.name.hideout_steppe_5}Rhûn Raider's Camp" type="Hideout" posX="1385.431" posY="777.468" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13756: <Settlement id="hideout_steppe_6" name="{=Settlements.Settlement.name.hideout_steppe_6}Rhûn Raider's Camp" type="Hideout" posX="1462.967" posY="736.327" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13764: <Settlement id="hideout_steppe_7" name="{=Settlements.Settlement.name.hideout_steppe_7}Rhûn Raider's Camp" type="Hideout" posX="1320.726" posY="932.196" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13772: <Settlement id="hideout_steppe_8" name="{=Settlements.Settlement.name.hideout_steppe_8}Rhûn Raider's Camp" type="Hideout" posX="1465.94" posY="1034.912" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13780: <Settlement id="hideout_steppe_9" name="{=Settlements.Settlement.name.hideout_steppe_9}Rhûn Raider's Camp" type="Hideout" posX="1151.774" posY="137.374" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13788: <Settlement id="hideout_steppe_10" name="{=Settlements.Settlement.name.hideout_steppe_10}Rhûn Raider's Camp" type="Hideout" posX="1343.707" posY="1008.83" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13796: <Settlement id="hideout_steppe_11" name="{=Settlements.Settlement.name.hideout_steppe_11}Rhûn Raider's Camp" type="Hideout" posX="1274.175" posY="1054.239" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13804: <Settlement id="hideout_steppe_12" name="{=Settlements.Settlement.name.hideout_steppe_12}Rhûn Raider's Camp" type="Hideout" posX="1245.168" posY="1028.073" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13812: <Settlement id="hideout_steppe_13" name="{=Settlements.Settlement.name.hideout_steppe_13}Rhûn Raider's Camp" type="Hideout" posX="1243.265" posY="897.417" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13820: <Settlement id="hideout_steppe_14" name="{=Settlements.Settlement.name.hideout_steppe_14}Rhûn Raider's Camp" type="Hideout" posX="1043.027" posY="1062.519" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13828: <Settlement id="hideout_steppe_15" name="{=Settlements.Settlement.name.hideout_steppe_15}Rhûn Raider's Camp" type="Hideout" posX="987.542" posY="1058.258" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13836: <Settlement id="hideout_steppe_16" name="{=Settlements.Settlement.name.hideout_steppe_16}Rhûn Raider's Camp" type="Hideout" posX="1018.725" posY="1116.443" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13844: <Settlement id="hideout_steppe_17" name="{=Settlements.Settlement.name.hideout_steppe_17}Rhûn Raider's Camp" type="Hideout" posX="968.345" posY="1172.937" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13852: <Settlement id="hideout_steppe_18" name="{=Settlements.Settlement.name.hideout_steppe_18}Rhûn Raider's Camp" type="Hideout" posX="1063.691" posY="1148.414" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13860: <Settlement id="hideout_steppe_19" name="{=Settlements.Settlement.name.hideout_steppe_19}Rhûn Raider's Camp" type="Hideout" posX="1180.236" posY="1165.538" culture="Culture.rhun_raiders">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13868: <Settlement id="hideout_steppe_20" name="{=Settlements.Settlement.name.hideout_steppe_20}Rhûn Raider's Camp" type="Hideout" posX="1183.59" posY="1224.941" culture="Culture.rhun_raiders">

 succeeded in 904ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13086: <Hideout id="hideout_forest_1" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13089: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13094: <Hideout id="hideout_forest_2" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13097: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13102: <Hideout id="hideout_forest_3" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13105: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13110: <Hideout id="hideout_forest_4" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13113: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13118: <Hideout id="hideout_forest_5" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13121: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13126: <Hideout id="hideout_forest_6" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13129: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13134: <Hideout id="hideout_forest_7" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13137: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13142: <Hideout id="hideout_forest_8" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13145: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13150: <Hideout id="hideout_forest_9" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13153: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13158: <Hideout id="hideout_forest_10" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13161: <Location id="hideout_center" scene_name="bandit_forest_sv" />

 succeeded in 909ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13086: <Hideout id="hideout_forest_1" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13089: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13094: <Hideout id="hideout_forest_2" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13097: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13102: <Hideout id="hideout_forest_3" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13105: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13110: <Hideout id="hideout_forest_4" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13113: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13118: <Hideout id="hideout_forest_5" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13121: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13126: <Hideout id="hideout_forest_6" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13129: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13134: <Hideout id="hideout_forest_7" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13137: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13142: <Hideout id="hideout_forest_8" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13145: <Location id="hideout_center" scene_name="forest_hideout_004_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13150: <Hideout id="hideout_forest_9" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13153: <Location id="hideout_center" scene_name="bandit_forest_sv" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13158: <Hideout id="hideout_forest_10" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="0.0" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:13161: <Location id="hideout_center" scene_name="bandit_forest_sv" />

 succeeded in 964ms:
hideout_id_tags=99 hideout_type_tags=99 new_culture=99 old_culture=0

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path Main -Recurse -Filter TaomPartySizeModel.cs | Select-Object -ExpandProperty FullName' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\**\\*.cs -Pattern 'class TaomPartySizeModel|FindAppropriateInitialRosterForMobileParty|GetPartyMemberSizeLimit' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Modules\\StoryMode\\StoryMode.GameComponents\\StoryModePartySizeLimitModel.cs'; if(Test-Path "'$p){ $lines=Get-Content -Path $p; for($i=1;$i -le 55;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 797ms:
 succeeded in 1215ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs

 succeeded in 1505ms:
1: using System.Collections.Generic;
2: using System.Linq;
3: using StoryMode.Quests.SecondPhase.ConspiracyQuests;
4: using StoryMode.Quests.ThirdPhase;
5: using TaleWorlds.CampaignSystem;
6: using TaleWorlds.CampaignSystem.ComponentInterfaces;
7: using TaleWorlds.CampaignSystem.Naval;
8: using TaleWorlds.CampaignSystem.Party;
9: using TaleWorlds.CampaignSystem.Roster;
10: using TaleWorlds.CampaignSystem.Settlements;
11: using TaleWorlds.Core;
12: using TaleWorlds.Localization;
13: 
14: namespace StoryMode.GameComponents;
15: 
16: public class StoryModePartySizeLimitModel : PartySizeLimitModel
17: {
18: 	private DefeatTheConspiracyQuestBehavior _defeatTheConspiracyQuestBehavior;
19: 
20: 	public override int MinimumNumberOfVillagersAtVillagerParty => ((MBGameModel<PartySizeLimitModel>)this).BaseModel.MinimumNumberOfVillagersAtVillagerParty;
21: 
22: 	private DefeatTheConspiracyQuestBehavior DefeatTheConspiracyQuestBehavior
23: 	{
24: 		get
25: 		{
26: 			if (_defeatTheConspiracyQuestBehavior != null)
27: 			{
28: 				return _defeatTheConspiracyQuestBehavior;
29: 			}
30: 			_defeatTheConspiracyQuestBehavior = Campaign.Current.GetCampaignBehavior<DefeatTheConspiracyQuestBehavior>();
31: 			return _defeatTheConspiracyQuestBehavior;
32: 		}
33: 	}
34: 
35: 	public override ExplainedNumber CalculateGarrisonPartySizeLimit(Settlement settlement, bool includeDescriptions = false)
36: 	{
37: 		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
38: 		return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);
39: 	}
40: 
41: 	public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
42: 	{
43: 		return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.FindAppropriateInitialRosterForMobileParty(party, partyTemplate);
44: 	}
45: 
46: 	public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
47: 	{
48: 		return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.FindAppropriateInitialShipsForMobileParty(party, partyTemplate);
49: 	}
50: 
51: 	public override int GetAssumedPartySizeForLordParty(Hero leaderHero, IFaction partyMapFaction, Clan actualClan)
52: 	{
53: 		return ((MBGameModel<PartySizeLimitModel>)this).BaseModel.GetAssumedPartySizeForLordParty(leaderHero, partyMapFaction, actualClan);
54: 	}
55: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\CulturalFeats\\Models\\TaomPartySizeModel.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le $lines.Length;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 716ms:
1: using TaleWorlds.CampaignSystem;
2: using TaleWorlds.CampaignSystem.GameComponents;
3: using TaleWorlds.CampaignSystem.Party;
4: using TaleWorlds.Core;
5: using TAOM.Features.CareerSystem;
6: using TAOM.Features.CareerSystem.Domain;
7: 
8: namespace TAOM.Features.CulturalFeats.Models;
9: 
10: public class TaomPartySizeModel : DefaultPartySizeLimitModel
11: {
12:     private readonly ICulturalFeatsService _feats;
13:     private readonly ICareerPassiveService _careerPassives;
14: 
15:     public TaomPartySizeModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:     {
17:         _feats = feats;
18:         _careerPassives = careerPassives;
19:     }
20: 
21:     public override ExplainedNumber GetPartyMemberSizeLimit(
22:         PartyBase party, bool includeDescriptions = false)
23:     {
24:         var result = base.GetPartyMemberSizeLimit(party, includeDescriptions);
25:         _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture), ref result);
26:         _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.PartySize);
27:         return result;
28:     }
29: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\SubModule.cs -Pattern 'PatchCategory|PatchAll|Patch39|BanditPartySize|BanditManagement' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Filter *.cs | Select-String -Pattern 'HarmonyPatchCategory|Patch39_BanditPartySize|Patch38|Patch37|Patch36' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\SubModule.cs'; "'$lines=Get-Content -Path $p; for($i=145;$i -le 205;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 796ms:
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:65: using TAOM.Features.BanditManagement;
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:66: using TAOM.Features.BanditManagement.Models;
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:97: // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:107: _harmony.PatchCategory("Patch37_CrashReport");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:128: _harmony.PatchCategory("Patch25_LocalizationOverride");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:144: _harmony.PatchCategory("Patch18_CulturalFeats");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:145: _harmony.PatchCategory("Patch19_CustomBattles");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:147: // _harmony.PatchCategory("Patch0_BattleScenes");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:178: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:181: _harmony.PatchCategory("Patch22_ArmyTargeting");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:182: _harmony.PatchCategory("Patch30_MixedFormations");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:461: _harmony.PatchCategory("Patch1_FirstTimeInit");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:462: _harmony.PatchCategory("Patch2_RefreshTableau");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:463: _harmony.PatchCategory("Patch3_SetRace");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:464: _harmony.PatchCategory("Patch4_CharacterSpawner");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:465: _harmony.PatchCategory("Patch5_FaceGen");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:466: _harmony.PatchCategory("Late_Transpiler");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:467: _harmony.PatchCategory("Late_ActionSetOverride");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:468: _harmony.PatchCategory("Patch6_BannerEditor");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:469: _harmony.PatchCategory("Patch7_FactionMap");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:470: _harmony.PatchCategory("Patch9_RaceFilter");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:471: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:472: _harmony.PatchCategory("Patch8_SiegeCampGuard");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:473: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:474: _harmony.PatchCategory("Patch11_Diplomacy");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:475: _harmony.PatchCategory("Patch12_WarOfTheRing");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:477: _harmony.PatchCategory("Patch14_Execution");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:478: _harmony.PatchCategory("Patch15_BannerLayerLimit");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:479: _harmony.PatchCategory("Patch16_AtmospherePersistence");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:480: _harmony.PatchCategory("Patch17_TroopWeight");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:481: _harmony.PatchCategory("Patch23_BannerColorPersistence");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:482: _harmony.PatchCategory("Patch24_BannerDriftGuard");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:489: _harmony.PatchCategory("Patch26_SpecialResources");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:490: _harmony.PatchCategory("Patch27_CareerSystem");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:491: _harmony.PatchCategory("Patch29_CCBodyProperties");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:492: _harmony.PatchCategory("Patch33_EquipPresets");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:493: _harmony.PatchCategory("Patch34_QuickActions");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:494: _harmony.PatchCategory("Patch35_CompanionTactics");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:495: _harmony.PatchCategory("Patch36_FiefManagement");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:497: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:578: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:663: _harmony?.UnpatchAll("com.taom.mod");

 succeeded in 752ms:
145:         _harmony.PatchCategory("Patch19_CustomBattles");
146:         // Battle scenes disabled — custom map not yet ready, will re-enable when TAOM_Map is integrated
147:         // _harmony.PatchCategory("Patch0_BattleScenes");
148:         // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first
149: 
150:         var pathService = IoC.Resolve<IPathService>();
151:         var logger = IoC.Resolve<IModLogger>();
152:         FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);
153: 
154:         var allianceHook = IoC.Resolve<IOnAllianceAction>();
155:         var peaceHook = IoC.Resolve<IOnPeaceAction>();
156:         DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
157:         AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
158:         AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
159:         DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
160:         MakePeaceAction_ApplyInternal_Patch.Initialize(logger);
161: 
162:         var executionHook = IoC.Resolve<IOnExecutionAction>();
163:         ExecutionIoC.InitializeHooks(executionHook);
164: 
165:         TroopWeightIoC.InitializeHooks(
166:             IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
167:             IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
168:             IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
169:             IoC.Resolve<IOnPartyVMPopulatePartyListLabel>());
170: 
171:         CustomBattlesIoC.InitializeHooks(
172:             IoC.Resolve<IOnGetCustomBattleCommanders>(),
173:             IoC.Resolve<IOnGetCustomBattleFactions>(),
174:             IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
175:             IoC.Resolve<ISideCommanderFilter>(),
176:             logger);
177: 
178:         _harmony.PatchCategory("Patch21_ShaderPrecompilation");
179:         ShaderPrecompilationIoC.InitializeHooks(logger);
180: 
181:         _harmony.PatchCategory("Patch22_ArmyTargeting");
182:         _harmony.PatchCategory("Patch30_MixedFormations");
183:         // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
184:         // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
185:         // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
186:         // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
187: 
188:         var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
189:         var bannerColorService = IoC.Resolve<IBannerColorService>();
190:         var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();
191: 
192:         Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
193:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
194:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
195:         Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
196:         Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
197:         BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
198:         CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
199:         SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
200:         SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
201:         PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
202:         HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
203:         PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
204:         ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
205:         CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);

 succeeded in 1298ms:
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:95: // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:107: _harmony.PatchCategory("Patch37_CrashReport");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:428: // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:495: _harmony.PatchCategory("Patch36_FiefManagement");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:497: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
C:\Users\mikew\source\repos\TAOM\Main\Adapters\SettlementOwnershipAdapter.cs:30: // Fast path for Patch36_MapScreenF6.Postfix (polled every frame) and the FiefHubService
C:\Users\mikew\source\repos\TAOM\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
C:\Users\mikew\source\repos\TAOM\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: public static class Patch39_BanditPartySize
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:11: // alongside Patch37_CrashReport when CrashReportSettings.EnableCrashCapture is true.
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:11: // Patch37_CrashReport category — 9 Harmony Finalizers on TaleWorlds lifecycle methods,
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:28: // Marker class for the Patch37_CrashReport category. No [HarmonyPatch] attribute —
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:31: public static class Patch37_CrashReport
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:33: public const string Category = "Patch37_CrashReport";
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\FiefHubService.cs:39: // Patch36_MapScreenF6.Postfix polls this every frame for the empty-fief gate; Clamp / Next /
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\FiefHubCampaignBehavior.cs:76: // Patch36 prefix to substitute GauntletFiefManagementScreen for this state.
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:13: public static class Patch36_GameStateScreenManager
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:16: public static class Patch36_MapScreenF6
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:78: (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapScreenF6.Postfix threw: {ex}");
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
C:\Users\mikew\source\repos\TAOM\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
C:\Users\mikew\source\repos\TAOM\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
C:\Users\mikew\source\repos\TAOM\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
C:\Users\mikew\source\repos\TAOM\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids=@('"'dunland_peasant','dunland_raider','dunland_hunter','dunland_clan_warrior','dunland_wolf_raider','balcoth_volunteer','balcoth_footman','kharaghul_rider','balcoth_archer','kharaghul_raider','harad_levy','harad_skirmisher','harad_archer','harad_footman','harad_camelrider','harad_camelscout','gundabad_snaga','gundabad_hunter','gundabad_grunt','gundabad_lurker','gundabad_despoiler_of_the_vale','gundabad_scout','aux_basic','umbar_elite','umbar_elite_root1','umbar_elite_root0','umbar_elite_root00'); foreach("'$id in $ids){ $match=Get-ChildItem -Path Main'"\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern ('id=\"'+[regex]::Escape("'$id)+'"'\"') -List; if(-not "'$match){ '"'MISSING '+"'$id } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids=@('"'dunland_raiders_boss_party_template','rhun_raiders_boss_party_template','harad_raiders_boss_party_template','gundabad_raiders_boss_party_template','umbar_corsairs_boss_party_template','dunland_raiders_raider_party_template','rhun_raiders_raider_party_template','harad_raiders_raider_party_template','gundabad_raiders_raider_party_template','umbar_corsairs_raider_party_template'); foreach("'$id in $ids){ $match=Get-ChildItem -Path Main'"\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern ('id=\"'+[regex]::Escape("'$id)+'"'\"') -List; if(-not "'$match){ '"'MISSING '+"'$id } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern 'default_party_template=\"PartyTemplate\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)|bandit_boss_party_template=\"PartyTemplate\\.(dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs)' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 978ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4181: bandit_boss_party_template="PartyTemplate.dunland_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4216: bandit_boss_party_template="PartyTemplate.rhun_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4251: bandit_boss_party_template="PartyTemplate.harad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4286: bandit_boss_party_template="PartyTemplate.gundabad_raiders_boss_party_template">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4321: bandit_boss_party_template="PartyTemplate.umbar_corsairs_boss_party_template">

 succeeded in 2886ms:
 succeeded in 4987ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Campaign.cs'; "'$lines=Get-Content -Path $p; for($i=1195;$i -le 1230;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 701ms:
1195: 		EstimatedMaximumLordPartySpeedExceptPlayer = 10f;
1196: 		EstimatedAverageLordPartySpeed = 3.36f;
1197: 		EstimatedAverageCaravanPartySpeed = 4.2f;
1198: 		EstimatedAverageVillagerPartySpeed = 3.43f;
1199: 		EstimatedAverageBanditPartySpeed = 3.41f;
1200: 		EstimatedAverageLordPartyNavalSpeed = EstimatedAverageLordPartySpeed * 1.2f;
1201: 		EstimatedAverageCaravanPartyNavalSpeed = 3.53f;
1202: 		EstimatedAverageVillagerPartyNavalSpeed = 4.01f;
1203: 		EstimatedAverageBanditPartyNavalSpeed = 3.57f;
1204: 		CalculateAverageDistanceBetweenTowns();
1205: 		CalculateAverageWage();
1206: 	}
1207: 
1208: 	private void CalculateAverageWage()
1209: 	{
1210: 		float num = 0f;
1211: 		float num2 = 0f;
1212: 		foreach (CultureObject objectType in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
1213: 		{
1214: 			if (!objectType.IsMainCulture)
1215: 			{
1216: 				continue;
1217: 			}
1218: 			foreach (PartyTemplateStack stack in objectType.DefaultPartyTemplate.Stacks)
1219: 			{
1220: 				int troopWage = stack.Character.TroopWage;
1221: 				float num3 = (float)(stack.MaxValue + stack.MinValue) * 0.5f;
1222: 				num += (float)troopWage * num3;
1223: 				num2 += num3;
1224: 			}
1225: 		}
1226: 		if (num2 > 0f)
1227: 		{
1228: 			AverageWage = num / num2;
1229: 		}
1230: 	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party.PartyComponents\\BanditPartyComponent.cs'; "'$lines=Get-Content -Path $p; for($i=1;$i -le 90;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=128;$i -le 170;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 725ms:
1: using System.Collections.Generic;
2: using TaleWorlds.CampaignSystem.Roster;
3: using TaleWorlds.CampaignSystem.Settlements;
4: using TaleWorlds.Core;
5: using TaleWorlds.Library;
6: using TaleWorlds.Localization;
7: using TaleWorlds.SaveSystem;
8: 
9: namespace TaleWorlds.CampaignSystem.Party.PartyComponents;
10: 
11: public class BanditPartyComponent : WarPartyComponent
12: {
13: 	protected class InitializationArgs
14: 	{
15: 		public readonly Clan Clan;
16: 
17: 		public readonly CampaignVec2 InitialPosition;
18: 
19: 		public readonly PartyTemplateObject PartyTemplate;
20: 
21: 		public InitializationArgs(Clan clan, PartyTemplateObject pt, CampaignVec2 position)
22: 		{
23: 			Clan = clan;
24: 			InitialPosition = position;
25: 			PartyTemplate = pt;
26: 		}
27: 
28: 		public void InitializeBanditOnCreation(MobileParty mobileParty)
29: 		{
30: 			if (PartyTemplate != null)
31: 			{
32: 				mobileParty.InitializeMobilePartyAtPosition(PartyTemplate, InitialPosition);
33: 				return;
34: 			}
35: 			TroopRoster memberRoster = new TroopRoster(mobileParty.Party);
36: 			TroopRoster prisonerRoster = new TroopRoster(mobileParty.Party);
37: 			mobileParty.InitializeMobilePartyAtPosition(memberRoster, prisonerRoster, InitialPosition);
38: 		}
39: 	}
40: 
41: 	private InitializationArgs _initializationArgs;
42: 
43: 	[CachedData]
44: 	private TextObject _cachedName;
45: 
46: 	[SaveableField(3)]
47: 	private readonly Settlement _relatedSettlement;
48: 
49: 	[SaveableProperty(1)]
50: 	public Hideout Hideout { get; private set; }
51: 
52: 	[SaveableProperty(2)]
53: 	public bool IsBossParty { get; private set; }
54: 
55: 	public override Settlement HomeSettlement
56: 	{
57: 		get
58: 		{
59: 			if (Hideout == null)
60: 			{
61: 				return _relatedSettlement;
62: 			}
63: 			return Hideout.Settlement;
64: 		}
65: 	}
66: 
67: 	public override Hero PartyOwner => base.MobileParty.ActualClan?.Leader;
68: 
69: 	public override TextObject Name
70: 	{
71: 		get
72: 		{
73: 			TextObject obj = (Game.Current.IsDevelopmentMode ? new TextObject(base.MobileParty.StringId) : (_cachedName ?? (_cachedName = base.MobileParty.MapFaction.Name)));
74: 			obj.SetTextVariable("IS_BANDIT", 1);
75: 			return obj;
76: 		}
77: 	}
78: 
79: 	internal static void AutoGeneratedStaticCollectObjectsBanditPartyComponent(object o, List<object> collectedObjects)
80: 	{
81: 		((BanditPartyComponent)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
82: 	}
83: 
84: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
85: 	{
86: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
87: 		collectedObjects.Add(_relatedSettlement);
88: 		collectedObjects.Add(Hideout);
89: 	}
90: 
128: 	protected BanditPartyComponent(Hideout hideout, bool isBossParty, InitializationArgs args)
129: 	{
130: 		Hideout = hideout;
131: 		IsBossParty = isBossParty;
132: 		_initializationArgs = args;
133: 	}
134: 
135: 	protected BanditPartyComponent(Settlement relatedSettlement, InitializationArgs args)
136: 	{
137: 		_relatedSettlement = relatedSettlement;
138: 		_initializationArgs = args;
139: 	}
140: 
141: 	public void SetHomeHideout(Hideout hideout)
142: 	{
143: 		Hideout hideout2 = Hideout;
144: 		Hideout = hideout;
145: 		if (hideout2 != Hideout)
146: 		{
147: 			CampaignEventDispatcher.Instance.OnHomeHideoutChanged(this, hideout2);
148: 		}
149: 	}
150: 
151: 	public override void ClearCachedName()
152: 	{
153: 		_cachedName = null;
154: 	}
155: 
156: 	protected override void OnMobilePartySetOnCreation()
157: 	{
158: 		if (_initializationArgs != null)
159: 		{
160: 			base.MobileParty.ActualClan = _initializationArgs.Clan;
161: 			_initializationArgs.InitializeBanditOnCreation(base.MobileParty);
162: 			_initializationArgs = null;
163: 		}
164: 	}
165: }
166: 
167: 
168: 
169: 
170: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -File | Select-String -Pattern 'forest_bandits|mountain_bandits|desert_bandits|steppe_bandits|sea_raiders|spclans|looters_template|sea_raiders_template|mountain_bandits_template|forest_bandits_template|desert_bandits_template|steppe_bandits_template' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1475ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13084: <Settlement id="hideout_forest_1" name="{=Settlements.Settlement.name.hideout_forest_1}Hideout" type="Hideout" posX="770.748" posY="880.545" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13092: <Settlement id="hideout_forest_2" name="{=Settlements.Settlement.name.hideout_forest_2}Hideout" type="Hideout" posX="747.613" posY="856.493" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13100: <Settlement id="hideout_forest_3" name="{=Settlements.Settlement.name.hideout_forest_3}Hideout" type="Hideout" posX="776.185" posY="831.114" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13108: <Settlement id="hideout_forest_4" name="{=Settlements.Settlement.name.hideout_forest_4}Hideout" type="Hideout" posX="773.978" posY="806.852" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13116: <Settlement id="hideout_forest_5" name="{=Settlements.Settlement.name.hideout_forest_5}Hideout" type="Hideout" posX="773.043" posY="776.114" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13124: <Settlement id="hideout_forest_6" name="{=Settlements.Settlement.name.hideout_forest_6}Hideout" type="Hideout" posX="800.069" posY="722.118" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13132: <Settlement id="hideout_forest_7" name="{=Settlements.Settlement.name.hideout_forest_7}Hideout" type="Hideout" posX="724.227" posY="794.886" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13140: <Settlement id="hideout_forest_8" name="{=Settlements.Settlement.name.hideout_forest_8}Hideout" type="Hideout" posX="678.835" posY="816.911" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13148: <Settlement id="hideout_forest_9" name="{=Settlements.Settlement.name.hideout_forest_9}Hideout" type="Hideout" posX="738.98" posY="816.841" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13156: <Settlement id="hideout_forest_10" name="{=Settlements.Settlement.name.hideout_forest_10}Hideout" type="Hideout" posX="705.923" posY="767.518" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13164: <Settlement id="hideout_forest_11" name="{=Settlements.Settlement.name.hideout_forest_11}Hideout" type="Hideout" posX="617.601" posY="799.329" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13172: <Settlement id="hideout_forest_12" name="{=Settlements.Settlement.name.hideout_forest_12}Hideout" type="Hideout" posX="656.126" posY="815.868" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13180: <Settlement id="hideout_forest_13" name="{=Settlements.Settlement.name.hideout_forest_13}Hideout" type="Hideout" posX="573.135" posY="786.294" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13188: <Settlement id="hideout_forest_14" name="{=Settlements.Settlement.name.hideout_forest_14}Hideout" type="Hideout" posX="572.798" posY="818.379" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13196: <Settlement id="hideout_forest_15" name="{=Settlements.Settlement.name.hideout_forest_15}Hideout" type="Hideout" posX="547.914" posY="818.905" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13204: <Settlement id="hideout_forest_16" name="{=Settlements.Settlement.name.hideout_forest_16}Hideout" type="Hideout" posX="582.673" posY="842.145" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13212: <Settlement id="hideout_forest_17" name="{=Settlements.Settlement.name.hideout_forest_17}Hideout" type="Hideout" posX="591.523" posY="883.228" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13220: <Settlement id="hideout_forest_18" name="{=Settlements.Settlement.name.hideout_forest_18}Hideout" type="Hideout" posX="615.811" posY="923.319" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13228: <Settlement id="hideout_forest_19" name="{=Settlements.Settlement.name.hideout_forest_19}Hideout" type="Hideout" posX="628.332" posY="948.494" culture="Culture.forest_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13236: <Settlement id="hideout_seaside_1" name="{=Settlements.Settlement.name.hideout_seaside_1}Hideout" type="Hideout" posX="711.521" posY="555.527" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13244: <Settlement id="hideout_seaside_2" name="{=Settlements.Settlement.name.hideout_seaside_2}Hideout" type="Hideout" posX="766.938" posY="949.116" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13252: <Settlement id="hideout_seaside_3" name="{=Settlements.Settlement.name.hideout_seaside_3}Hideout" type="Hideout" posX="799.676" posY="1004.181" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13260: <Settlement id="hideout_seaside_4" name="{=Settlements.Settlement.name.hideout_seaside_4}Hideout" type="Hideout" posX="799.3" posY="1114.713" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13268: <Settlement id="hideout_seaside_5" name="{=Settlements.Settlement.name.hideout_seaside_5}Hideout" type="Hideout" posX="791.061" posY="1132.36" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13276: <Settlement id="hideout_seaside_6" name="{=Settlements.Settlement.name.hideout_seaside_6}Hideout" type="Hideout" posX="780.086" posY="1172.637" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13284: <Settlement id="hideout_seaside_7" name="{=Settlements.Settlement.name.hideout_seaside_7}Hideout" type="Hideout" posX="763.182" posY="1227.824" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13292: <Settlement id="hideout_seaside_8" name="{=Settlements.Settlement.name.hideout_seaside_8}Hideout" type="Hideout" posX="827.085" posY="1212.69" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13300: <Settlement id="hideout_seaside_9" name="{=Settlements.Settlement.name.hideout_seaside_9}Hideout" type="Hideout" posX="851.905" posY="1198.957" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13308: <Settlement id="hideout_seaside_10" name="{=Settlements.Settlement.name.hideout_seaside_10}Hideout" type="Hideout" posX="919.256" posY="1189.674" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13316: <Settlement id="hideout_seaside_11" name="{=Settlements.Settlement.name.hideout_seaside_11}Hideout" type="Hideout" posX="926.778" posY="1167.689" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13324: <Settlement id="hideout_seaside_12" name="{=Settlements.Settlement.name.hideout_seaside_12}Hideout" type="Hideout" posX="987.868" posY="1085.954" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13332: <Settlement id="hideout_seaside_13" name="{=Settlements.Settlement.name.hideout_seaside_13}Hideout" type="Hideout" posX="775.268" posY="965.898" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13340: <Settlement id="hideout_seaside_14" name="{=Settlements.Settlement.name.hideout_seaside_14}Seaside Hideout" type="Hideout" posX="547.506" posY="791.563" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13348: <Settlement id="hideout_seaside_15" name="{=Settlements.Settlement.name.hideout_seaside_15}Hideout" type="Hideout" posX="620.337" posY="788.961" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13356: <Settlement id="hideout_seaside_16" name="{=Settlements.Settlement.name.hideout_seaside_16}Hideout" type="Hideout" posX="1000.451" posY="826.282" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13364: <Settlement id="hideout_seaside_17" name="{=Settlements.Settlement.name.hideout_seaside_17}Hideout" type="Hideout" posX="1430.793" posY="816.84" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13372: <Settlement id="hideout_seaside_18" name="{=Settlements.Settlement.name.hideout_seaside_18}Hideout" type="Hideout" posX="1362.993" posY="473.479" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13380: <Settlement id="hideout_seaside_19" name="{=Settlements.Settlement.name.hideout_seaside_19}Hideout" type="Hideout" posX="1300.156" posY="247.452" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13388: <Settlement id="hideout_seaside_20" name="{=Settlements.Settlement.name.hideout_seaside_20}Hideout" type="Hideout" posX="777.705" posY="293.832" culture="Culture.sea_raiders">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13396: <Settlement id="hideout_mountain_1" name="{=Settlements.Settlement.name.hideout_mountain_1}Hideout" type="Hideout" posX="559.887" posY="640.117" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13404: <Settlement id="hideout_mountain_2" name="{=Settlements.Settlement.name.hideout_mountain_2}Hideout" type="Hideout" posX="621.992" posY="668.275" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13412: <Settlement id="hideout_mountain_3" name="{=Settlements.Settlement.name.hideout_mountain_3}Hideout" type="Hideout" posX="543.251" posY="669.003" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13420: <Settlement id="hideout_mountain_4" name="{=Settlements.Settlement.name.hideout_mountain_4}Hideout" type="Hideout" posX="500.38" posY="722.996" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13428: <Settlement id="hideout_mountain_5" name="{=Settlements.Settlement.name.hideout_mountain_5}Hideout" type="Hideout" posX="647.332" posY="686.817" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13436: <Settlement id="hideout_mountain_6" name="{=Settlements.Settlement.name.hideout_mountain_6}Hideout" type="Hideout" posX="714.48" posY="650.525" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13444: <Settlement id="hideout_mountain_7" name="{=Settlements.Settlement.name.hideout_mountain_7}Hideout" type="Hideout" posX="712.381" posY="624.834" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13452: <Settlement id="hideout_mountain_8" name="{=Settlements.Settlement.name.hideout_mountain_8}Hideout" type="Hideout" posX="763.507" posY="660.622" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13460: <Settlement id="hideout_mountain_9" name="{=Settlements.Settlement.name.hideout_mountain_9}Hideout" type="Hideout" posX="795.568" posY="638.99" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13468: <Settlement id="hideout_mountain_10" name="{=Settlements.Settlement.name.hideout_mountain_10}Hideout" type="Hideout" posX="781.159" posY="582.898" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13476: <Settlement id="hideout_mountain_11" name="{=Settlements.Settlement.name.hideout_mountain_11}Hideout" type="Hideout" posX="842.516" posY="591.794" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13484: <Settlement id="hideout_mountain_12" name="{=Settlements.Settlement.name.hideout_mountain_12}Hideout" type="Hideout" posX="866.494" posY="623.87" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13492: <Settlement id="hideout_mountain_13" name="{=Settlements.Settlement.name.hideout_mountain_13}Hideout" type="Hideout" posX="878.705" posY="607.363" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13500: <Settlement id="hideout_mountain_14" name="{=Settlements.Settlement.name.hideout_mountain_14}Hideout" type="Hideout" posX="843.333" posY="554.311" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13508: <Settlement id="hideout_mountain_15" name="{=Settlements.Settlement.name.hideout_mountain_15}Hideout" type="Hideout" posX="905.379" posY="538.943" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13516: <Settlement id="hideout_mountain_16" name="{=Settlements.Settlement.name.hideout_mountain_16}Hideout" type="Hideout" posX="857.443" posY="518.807" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13524: <Settlement id="hideout_mountain_17" name="{=Settlements.Settlement.name.hideout_mountain_17}Hideout" type="Hideout" posX="918.299" posY="653.893" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13532: <Settlement id="hideout_mountain_18" name="{=Settlements.Settlement.name.hideout_mountain_18}Hideout" type="Hideout" posX="893.563" posY="661.251" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13540: <Settlement id="hideout_mountain_19" name="{=Settlements.Settlement.name.hideout_mountain_19}Hideout" type="Hideout" posX="889.995" posY="702.1" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13548: <Settlement id="hideout_mountain_20" name="{=Settlements.Settlement.name.hideout_mountain_20}Hideout" type="Hideout" posX="853.9" posY="706.69" culture="Culture.mountain_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13556: <Settlement id="hideout_desert_1" name="{=Settlements.Settlement.name.hideout_desert_1}Hideout" type="Hideout" posX="821.092" posY="394.346" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13564: <Settlement id="hideout_desert_2" name="{=Settlements.Settlement.name.hideout_desert_2}Hideout" type="Hideout" posX="860.578" posY="409.573" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13572: <Settlement id="hideout_desert_3" name="{=Settlements.Settlement.name.hideout_desert_3}Hideout" type="Hideout" posX="919.318" posY="386.194" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13580: <Settlement id="hideout_desert_4" name="{=Settlements.Settlement.name.hideout_desert_4}Hideout" type="Hideout" posX="999.831" posY="413.811" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13588: <Settlement id="hideout_desert_5" name="{=Settlements.Settlement.name.hideout_desert_5}Hideout" type="Hideout" posX="1062.571" posY="447.727" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13596: <Settlement id="hideout_desert_6" name="{=Settlements.Settlement.name.hideout_desert_6}Hideout" type="Hideout" posX="1135.509" posY="405.314" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13604: <Settlement id="hideout_desert_7" name="{=Settlements.Settlement.name.hideout_desert_7}Hideout" type="Hideout" posX="1071.244" posY="359.852" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13612: <Settlement id="hideout_desert_8" name="{=Settlements.Settlement.name.hideout_desert_8}Hideout" type="Hideout" posX="1195.96" posY="459.733" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13620: <Settlement id="hideout_desert_9" name="{=Settlements.Settlement.name.hideout_desert_9}Hideout" type="Hideout" posX="1228.664" posY="385.537" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13628: <Settlement id="hideout_desert_10" name="{=Settlements.Settlement.name.hideout_desert_10}Hideout" type="Hideout" posX="1155.894" posY="326.538" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13636: <Settlement id="hideout_desert_11" name="{=Settlements.Settlement.name.hideout_desert_11}Hideout" type="Hideout" posX="1047.286" posY="337.813" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13644: <Settlement id="hideout_desert_12" name="{=Settlements.Settlement.name.hideout_desert_12}Hideout" type="Hideout" posX="904.196" posY="315.645" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13652: <Settlement id="hideout_desert_13" name="{=Settlements.Settlement.name.hideout_desert_13}Hideout" type="Hideout" posX="1039.582" posY="239.546" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13660: <Settlement id="hideout_desert_14" name="{=Settlements.Settlement.name.hideout_desert_14}Hideout" type="Hideout" posX="1166.285" posY="219.163" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13668: <Settlement id="hideout_desert_15" name="{=Settlements.Settlement.name.hideout_desert_15}Hideout" type="Hideout" posX="869.585" posY="238.385" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13676: <Settlement id="hideout_desert_16" name="{=Settlements.Settlement.name.hideout_desert_16}Hideout" type="Hideout" posX="734.311" posY="154.314" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13684: <Settlement id="hideout_desert_17" name="{=Settlements.Settlement.name.hideout_desert_17}Hideout" type="Hideout" posX="707.65" posY="282.982" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13692: <Settlement id="hideout_desert_18" name="{=Settlements.Settlement.name.hideout_desert_18}Hideout" type="Hideout" posX="714.943" posY="348.153" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13700: <Settlement id="hideout_desert_19" name="{=Settlements.Settlement.name.hideout_desert_19}Hideout" type="Hideout" posX="556.015" posY="174.506" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13708: <Settlement id="hideout_desert_20" name="{=Settlements.Settlement.name.hideout_desert_20}Hideout" type="Hideout" posX="657.581" posY="200.323" culture="Culture.desert_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13716: <Settlement id="hideout_steppe_1" name="{=Settlements.Settlement.name.hideout_steppe_1}Hideout" type="Hideout" posX="1072.809" posY="824.275" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13724: <Settlement id="hideout_steppe_2" name="{=Settlements.Settlement.name.hideout_steppe_2}Hideout" type="Hideout" posX="1139.294" posY="825.568" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13732: <Settlement id="hideout_steppe_3" name="{=Settlements.Settlement.name.hideout_steppe_3}Hideout" type="Hideout" posX="1217.296" posY="833.625" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13740: <Settlement id="hideout_steppe_4" name="{=Settlements.Settlement.name.hideout_steppe_4}Hideout" type="Hideout" posX="1285.515" posY="856.581" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13748: <Settlement id="hideout_steppe_5" name="{=Settlements.Settlement.name.hideout_steppe_5}Hideout" type="Hideout" posX="1412.969" posY="783.693" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13756: <Settlement id="hideout_steppe_6" name="{=Settlements.Settlement.name.hideout_steppe_6}Hideout" type="Hideout" posX="1450.518" posY="736.316" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13764: <Settlement id="hideout_steppe_7" name="{=Settlements.Settlement.name.hideout_steppe_7}Hideout" type="Hideout" posX="1334.13" posY="940.679" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13772: <Settlement id="hideout_steppe_8" name="{=Settlements.Settlement.name.hideout_steppe_8}Hideout" type="Hideout" posX="1456.362" posY="1045.531" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13780: <Settlement id="hideout_steppe_9" name="{=Settlements.Settlement.name.hideout_steppe_9}Hideout" type="Hideout" posX="-137.747" posY="2406.58" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13788: <Settlement id="hideout_steppe_10" name="{=Settlements.Settlement.name.hideout_steppe_10}Hideout" type="Hideout" posX="1344.699" posY="1019.325" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13796: <Settlement id="hideout_steppe_11" name="{=Settlements.Settlement.name.hideout_steppe_11}Hideout" type="Hideout" posX="1274.175" posY="1054.239" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13804: <Settlement id="hideout_steppe_12" name="{=Settlements.Settlement.name.hideout_steppe_12}Hideout" type="Hideout" posX="1236.097" posY="1018.568" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13812: <Settlement id="hideout_steppe_13" name="{=Settlements.Settlement.name.hideout_steppe_13}Hideout" type="Hideout" posX="1243.265" posY="897.417" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13820: <Settlement id="hideout_steppe_14" name="{=Settlements.Settlement.name.hideout_steppe_14}Hideout" type="Hideout" posX="1041.594" posY="1057.945" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13828: <Settlement id="hideout_steppe_15" name="{=Settlements.Settlement.name.hideout_steppe_15}Hideout" type="Hideout" posX="970.725" posY="1079.438" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13836: <Settlement id="hideout_steppe_16" name="{=Settlements.Settlement.name.hideout_steppe_16}Hideout" type="Hideout" posX="1018.725" posY="1116.443" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13844: <Settlement id="hideout_steppe_17" name="{=Settlements.Settlement.name.hideout_steppe_17}Hideout" type="Hideout" posX="968.345" posY="1172.937" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13852: <Settlement id="hideout_steppe_18" name="{=Settlements.Settlement.name.hideout_steppe_18}Hideout" type="Hideout" posX="1063.691" posY="1148.414" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13860: <Settlement id="hideout_steppe_19" name="{=Settlements.Settlement.name.hideout_steppe_19}Hideout" type="Hideout" posX="1187.749" posY="1170.121" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:13868: <Settlement id="hideout_steppe_20" name="{=Settlements.Settlement.name.hideout_steppe_20}Hideout" type="Hideout" posX="1183.59" posY="1224.941" culture="Culture.steppe_bandits">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:808: <!-- Dunlending Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:826: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:844: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:862: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:880: <!-- Corsairs of Umbar (replaces sea_raiders) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1489: <!-- Dunland Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1507: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1525: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1543: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_partyTemplates.xml:1562: <!-- Umbar Corsairs (replaces sea_raiders) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4168: <!-- Dunlending Raiders (replaces forest_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4203: <!-- Rhûn Raiders (replaces steppe_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4238: <!-- Haradrim Raiders (replaces desert_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4273: <!-- Gundabad Orc Raiders (replaces mountain_bandits) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:4308: <!-- Umbar Corsairs (replaces sea_raiders) -->

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MBRandom.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter *.cs | Select-String -Pattern 'ChooseWeighted' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
 succeeded in 789ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBRandom.cs

 succeeded in 2616ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\NameGenerator.cs:180: MBRandom.ChooseWeighted(list, out var chosenIndex);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:289: var (clan, num) = MBRandom.ChooseWeighted(list, out var _);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:347: return MBRandom.ChooseWeighted(mBList);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:433: return MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:518: return MBRandom.ChooseWeighted(mBList);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:380: town = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:775: int index = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1950: TradeActionLog tradeActionLog = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonTroopsCampaignBehavior.cs:537: return MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:273: CharacterObject element = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:408: settlement = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:432: settlement = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:441: settlement = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:625: TroopRosterElement troopRosterElement = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:651: TroopRosterElement troopRosterElement2 = MBRandom.ChooseWeighted(list2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:253: IssueData issueData = MBRandom.ChooseWeighted(list2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\SiegeAftermathCampaignBehavior.cs:263: return MBRandom.ChooseWeighted(new List<(SiegeAftermathAction.SiegeAftermath, float)>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\WorkshopsCampaignBehavior.cs:1064: itemObject = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultWorkshopModel.cs:82: return MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:568: PartyTemplateStack partyTemplateStack = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1276: PartyTemplateStack partyTemplateStack = MBRandom.ChooseWeighted(list);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\IssueBase.cs:762: int index = AlternativeSolutionSentTroops.FindIndexOfTroop(MBRandom.ChooseWeighted(list2).Character);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\RaidEventComponent.cs:297: ItemObject item2 = MBRandom.ChooseWeighted(Campaign.Current.Models.RaidModel.GetCommonLootItemScores());
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ItemModifierGroup.cs:68: return MBRandom.ChooseWeighted(useLootScores ? _lootDropItemModifierScores : _productionDropItemModifierScores);
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBRandom.cs:93: public static T ChooseWeighted<T>(IReadOnlyList<(T, float)> weightList)
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBRandom.cs:96: return ChooseWeighted(weightList, out chosenIndex);
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBRandom.cs:99: public static T ChooseWeighted<T>(IReadOnlyList<(T, float)> weightList, out int chosenIndex)
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\GuardsCampaignBehavior.cs:194: (CharacterObject, int) tuple = MBRandom.ChooseWeighted<(CharacterObject, int)>((IReadOnlyList<ValueTuple<(CharacterObject, int), float>>)list, ref index);
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade.Source.Missions.Handlers\LordsHallFightMissionController.cs:54: KeyValuePair<int, AreaData> keyValuePair = MBRandom.ChooseWeighted(list3);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core\\TaleWorlds.Core\\MBRandom.cs'; "'$lines=Get-Content -Path $p; for($i=85;$i -le 130;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 713ms:
85: 		float num2 = f - (float)num;
86: 		if (RandomFloat < num2)
87: 		{
88: 			num++;
89: 		}
90: 		return num;
91: 	}
92: 
93: 	public static T ChooseWeighted<T>(IReadOnlyList<(T, float)> weightList)
94: 	{
95: 		int chosenIndex;
96: 		return ChooseWeighted(weightList, out chosenIndex);
97: 	}
98: 
99: 	public static T ChooseWeighted<T>(IReadOnlyList<(T, float)> weightList, out int chosenIndex)
100: 	{
101: 		chosenIndex = -1;
102: 		float num = weightList.Sum(((T, float) x) => x.Item2);
103: 		float num2 = RandomFloat * num;
104: 		for (int num3 = 0; num3 < weightList.Count; num3++)
105: 		{
106: 			num2 -= weightList[num3].Item2;
107: 			if (num2 <= 0f)
108: 			{
109: 				chosenIndex = num3;
110: 				return weightList[num3].Item1;
111: 			}
112: 		}
113: 		if (weightList.Count > 0)
114: 		{
115: 			chosenIndex = 0;
116: 			return weightList[0].Item1;
117: 		}
118: 		chosenIndex = -1;
119: 		return default(T);
120: 	}
121: 
122: 	public static float RandomFloatGaussian(float center, float spread, float min, float max)
123: 	{
124: 		float a = 1f - RandomFloat;
125: 		float num = 1f - RandomFloat;
126: 		float num2 = TaleWorlds.Library.MathF.Sqrt(-2f * TaleWorlds.Library.MathF.Log(a)) * TaleWorlds.Library.MathF.Sin(System.MathF.PI * 2f * num);
127: 		return TaleWorlds.Library.MathF.Clamp(center + spread * num2, min, max);
128: 	}
129: 
130: 	public static void SetSeed(uint seed, uint seed2)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\SubModule.cs'; "'$lines=Get-Content -Path $p; for($i=485;$i -le 500;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 714ms:
485:         var specResLogger = IoC.Resolve<IModLogger>();
486:         PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
487:         PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
488:         PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
489:         _harmony.PatchCategory("Patch26_SpecialResources");
490:         _harmony.PatchCategory("Patch27_CareerSystem");
491:         _harmony.PatchCategory("Patch29_CCBodyProperties");
492:         _harmony.PatchCategory("Patch33_EquipPresets");
493:         _harmony.PatchCategory("Patch34_QuickActions");
494:         _harmony.PatchCategory("Patch35_CompanionTactics");
495:         _harmony.PatchCategory("Patch36_FiefManagement");
496:         SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
497:         _harmony.PatchCategory("Patch38_SettlementNameplateFade");
498: 
499:         // CompanionTactics — manual patch for the PRIVATE method
500:         // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs -Pattern 'public bool IsBandit|public bool IsBanditBossParty|BanditPartyComponent' -Context 0,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 755ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1223: public BanditPartyComponent BanditPartyComponent => _partyComponent as BanditPartyComponent;
    
    public PatrolPartyComponent PatrolPartyComponent => _partyComponent as PatrolPartyComponent;
    
    public LordPartyComponent LordPartyComponent => _partyComponent as LordPartyComponent;
    
    public GarrisonPartyComponent GarrisonPartyComponent => _partyComponent as GarrisonPartyComponent;
    
    public PartyComponent PartyComponent => _partyComponent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1255: public bool IsBandit { get; private set; }
    
    public bool IsBanditBossParty
    {
    get
    {
    if (IsBandit)
    {
    return BanditPartyComponent.IsBossParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1257: public bool IsBanditBossParty
    {
    get
    {
    if (IsBandit)
    {
    return BanditPartyComponent.IsBossParty;
    }
    return false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1263: return BanditPartyComponent.IsBossParty;
    }
    return false;
    }
    }
    
    public bool AvoidHostileActions
    {
    get
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:4173: IsBandit = _partyComponent is BanditPartyComponent;
    }
    }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs'; "'$lines=Get-Content -Path $p; for($i=1248;$i -le 1265;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }; for($i=4165;$i -le 4178;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 628ms:
1248: 	[CachedData]
1249: 	public bool IsGarrison { get; private set; }
1250: 
1251: 	[CachedData]
1252: 	public bool IsCustomParty { get; private set; }
1253: 
1254: 	[CachedData]
1255: 	public bool IsBandit { get; private set; }
1256: 
1257: 	public bool IsBanditBossParty
1258: 	{
1259: 		get
1260: 		{
1261: 			if (IsBandit)
1262: 			{
1263: 				return BanditPartyComponent.IsBossParty;
1264: 			}
1265: 			return false;
4165: 	{
4166: 		IsLordParty = _partyComponent is LordPartyComponent;
4167: 		IsVillager = _partyComponent is VillagerPartyComponent;
4168: 		IsMilitia = _partyComponent is MilitiaPartyComponent;
4169: 		IsCaravan = _partyComponent is CaravanPartyComponent;
4170: 		IsPatrolParty = _partyComponent is PatrolPartyComponent;
4171: 		IsGarrison = _partyComponent is GarrisonPartyComponent;
4172: 		IsCustomParty = _partyComponent is CustomPartyComponent;
4173: 		IsBandit = _partyComponent is BanditPartyComponent;
4174: 	}
4175: }
4176: 
4177: 
4178: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\HideoutCampaignBehavior.cs -Pattern 'NumberOfMaximumTroopCountForBossFightInHideout|NumberOfMaximumTroopCountForFirstFightInHideout|GetMaximumTroopCountForHideoutMission' -Context 3,5 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\HideoutCampaignBehavior.cs'; "'$lines=Get-Content -Path $p; for($i=580;$i -le 670;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 671ms:
580: 				int minimumTroopCountForHideoutMission = Campaign.Current.Models.BanditDensityModel.GetMinimumTroopCountForHideoutMission(MobileParty.MainParty, isAssault: false);
581: 				if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumTroopCountForHideoutMission)
582: 				{
583: 					args.IsEnabled = false;
584: 					args.Tooltip = new TextObject("{=yUbdUFSC}You should have more than {AMOUNT} healthy troops in your party to send your troops!");
585: 					args.Tooltip.SetTextVariable("AMOUNT", minimumTroopCountForHideoutMission);
586: 				}
587: 				args.optionLeaveType = GameMenuOption.LeaveType.OrderTroopsToAttack;
588: 				float sendTroopsSuccessChance = Campaign.Current.Models.HideoutModel.GetSendTroopsSuccessChance(hideout);
589: 				MBTextManager.SetTextVariable("SUCCESS_CHANCE", TaleWorlds.Library.MathF.Round(sendTroopsSuccessChance * 100f));
590: 			}
591: 		}
592: 		else
593: 		{
594: 			num = 0;
595: 		}
596: 		return (byte)num != 0;
597: 	}
598: 
599: 	private void game_menu_encounter_send_troops_on_consequence(MenuCallbackArgs args)
600: 	{
601: 		UpdateInitialHideoutPopulation();
602: 		PlayerEncounter.Current.ForceHideoutSendTroops = true;
603: 		GameMenu.SwitchToMenu("encounter");
604: 	}
605: 
606: 	private void ArrangeHideoutTroopCountsForMission()
607: 	{
608: 		int numberOfMinimumBanditTroopsInHideoutMission = Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditTroopsInHideoutMission;
609: 		int num = Campaign.Current.Models.BanditDensityModel.NumberOfMaximumTroopCountForFirstFightInHideout + Campaign.Current.Models.BanditDensityModel.NumberOfMaximumTroopCountForBossFightInHideout;
610: 		MBList<MobileParty> mBList = Settlement.CurrentSettlement.Parties.Where((MobileParty x) => x.IsBandit || x.IsBanditBossParty).ToMBList();
611: 		int num2 = mBList.Sum((MobileParty x) => x.MemberRoster.TotalHealthyCount);
612: 		if (num2 > num)
613: 		{
614: 			int num3 = num2 - num;
615: 			mBList.RemoveAll((MobileParty x) => x.IsBanditBossParty || x.MemberRoster.TotalHealthyCount == 1);
616: 			while (num3 > 0 && mBList.Count > 0)
617: 			{
618: 				MobileParty randomElement = mBList.GetRandomElement();
619: 				MBList<TroopRosterElement> troopRoster = randomElement.MemberRoster.GetTroopRoster();
620: 				List<(TroopRosterElement, float)> list = new List<(TroopRosterElement, float)>();
621: 				foreach (TroopRosterElement item in troopRoster)
622: 				{
623: 					list.Add((item, item.Number - item.WoundedNumber));
624: 				}
625: 				TroopRosterElement troopRosterElement = MBRandom.ChooseWeighted(list);
626: 				randomElement.MemberRoster.AddToCounts(troopRosterElement.Character, -1);
627: 				num3--;
628: 				if (randomElement.MemberRoster.TotalHealthyCount == 1)
629: 				{
630: 					mBList.Remove(randomElement);
631: 				}
632: 			}
633: 		}
634: 		else
635: 		{
636: 			if (num2 >= numberOfMinimumBanditTroopsInHideoutMission)
637: 			{
638: 				return;
639: 			}
640: 			int num4 = numberOfMinimumBanditTroopsInHideoutMission - num2;
641: 			mBList.RemoveAll((MobileParty x) => x.MemberRoster.GetTroopRoster().All((TroopRosterElement y) => y.Number == 0 || y.Character.Culture.BanditBoss == y.Character || y.Character.IsHero));
642: 			while (num4 > 0 && mBList.Count > 0)
643: 			{
644: 				MobileParty randomElement2 = mBList.GetRandomElement();
645: 				MBList<TroopRosterElement> troopRoster2 = randomElement2.MemberRoster.GetTroopRoster();
646: 				List<(TroopRosterElement, float)> list2 = new List<(TroopRosterElement, float)>();
647: 				foreach (TroopRosterElement item2 in troopRoster2)
648: 				{
649: 					list2.Add((item2, item2.Number * ((item2.Character.Culture.BanditBoss != item2.Character && !item2.Character.IsHero) ? 1 : 0)));
650: 				}
651: 				TroopRosterElement troopRosterElement2 = MBRandom.ChooseWeighted(list2);
652: 				randomElement2.MemberRoster.AddToCounts(troopRosterElement2.Character, 1);
653: 				num4--;
654: 			}
655: 		}
656: 	}
657: 
658: 	private void OnTroopRosterManageDone(TroopRoster hideoutTroops, bool isDirectAssault)
659: 	{
660: 		ArrangeHideoutTroopCountsForMission();
661: 		GameMenu.SwitchToMenu("hideout_place");
662: 		Settlement.CurrentSettlement.Hideout.SetNextPossibleAttackTime(Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
663: 		if (PlayerEncounter.IsActive)
664: 		{
665: 			PlayerEncounter.LeaveEncounter = false;
666: 		}
667: 		else
668: 		{
669: 			PlayerEncounter.Start();
670: 			PlayerEncounter.Current.SetupFields(PartyBase.MainParty, Settlement.CurrentSettlement.Party);

 succeeded in 700ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:562: int maximumTroopCountForHideoutMission = banditDensityModel.GetMaximumTroopCountForHideoutMission(MobileParty.MainParty, isDirectAssault);
    TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
    TroopRoster strongestAndPriorTroops = MobilePartyHelper.GetStrongestAndPriorTroops(MobileParty.MainParty, maximumTroopCountForHideoutMission, includePlayer: true);
    troopRoster.Add(strongestAndPriorTroops);
    int maximumTroopCountForHideoutMission2 = banditDensityModel.GetMaximumTroopCountForHideoutMission(MobileParty.MainParty, isDirectAssault);
    args.MenuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, troopRoster, null, CanChangeStatusOfTroop, onDone, maximumTroopCountForHideoutMission2, banditDensityModel.GetMinimumTroopCountForHideoutMission(MobileParty.MainParty, isDirectAssault));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:566: int maximumTroopCountForHideoutMission2 = banditDensityModel.GetMaximumTroopCountForHideoutMission(MobileParty.MainParty, isDirectAssault);
    args.MenuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, troopRoster, null, CanChangeStatusOfTroop, onDone, maximumTroopCountForHideoutMission2, banditDensityModel.GetMinimumTroopCountForHideoutMission(MobileParty.MainParty, isDirectAssault));
    }
    
    private bool game_menu_send_troops_hideout_on_condition(MenuCallbackArgs args)
    {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:609: int num = Campaign.Current.Models.BanditDensityModel.NumberOfMaximumTroopCountForFirstFightInHideout + Campaign.Current.Models.BanditDensityModel.NumberOfMaximumTroopCountForBossFightInHideout;
    MBList<MobileParty> mBList = Settlement.CurrentSettlement.Parties.Where((MobileParty x) => x.IsBandit || x.IsBanditBossParty).ToMBList();
    int num2 = mBList.Sum((MobileParty x) => x.MemberRoster.TotalHealthyCount);
    if (num2 > num)
    {
    int num3 = num2 - num;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\SubModule.cs'; "'$lines=Get-Content -Path $p; for($i=645;$i -le 675;$i++){ '"'{0}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\IoC.cs -Pattern 'Dispose|Container|Configure|Resolve' -Context 0,3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features -Recurse -Filter *.cs | Select-String -Pattern 'private static I.*Service _service|ResetForUnload|OnSubModuleUnloaded|IoC.Resolve<.*Service>\\(\\)' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
 succeeded in 758ms:
645:             }
646:         }
647:     }
648: 
649:     protected override void OnSubModuleUnloaded()
650:     {
651:         base.OnSubModuleUnloaded();
652:         // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
653:         // the hook doesn't hold a stale reference to a disposed CrashReportService
654:         // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
655:         try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
656:         catch { /* IoC may already be torn down — best-effort */ }
657: 
658:         // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
659:         // doesn't leave dangling MinHook trampolines. Best-effort — swallows.
660:         try { NativeSkinFixesInstaller.Uninstall(); }
661:         catch { /* shutdown — never block */ }
662: 
663:         _harmony?.UnpatchAll("com.taom.mod");
664:         IoC.Dispose();
665: 
666:         // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
667:         // the patch helper so the next module load resolves a fresh service graph from
668:         // the new IoC container. Without this, Finalizers fire against a disposed
669:         // FileLogger after reload and silently drop every log line.
670:         TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
671:     }
672: }
673: 
674: 
675: 

 succeeded in 1155ms:
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:56: private static IContainer _container;
    
    public static void Configure()
    {
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:58: public static void Configure()
    {
    var container = new Container();
    
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:60: var container = new Container();
    
    container.RegisterInstance<IContainer>(container);
    
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:62: container.RegisterInstance<IContainer>(container);
    
    RegisterCoreServices(container);
    RegisterLoggingServices(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:64: RegisterCoreServices(container);
    RegisterLoggingServices(container);
    
    HeroRaceIoC.RegisterHeroRaceFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:65: RegisterLoggingServices(container);
    
    HeroRaceIoC.RegisterHeroRaceFeature(container);
    BannerInjectionIoC.RegisterBannerInjectionFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:67: HeroRaceIoC.RegisterHeroRaceFeature(container);
    BannerInjectionIoC.RegisterBannerInjectionFeature(container);
    BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
    TroopProgressionIoC.RegisterTroopProgressionFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:68: BannerInjectionIoC.RegisterBannerInjectionFeature(container);
    BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
    TroopProgressionIoC.RegisterTroopProgressionFeature(container);
    Features.Arena.ArenaIoC.RegisterArenaFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:69: BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
    TroopProgressionIoC.RegisterTroopProgressionFeature(container);
    Features.Arena.ArenaIoC.RegisterArenaFeature(container);
    FactionMapIoC.RegisterFactionMapFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:70: TroopProgressionIoC.RegisterTroopProgressionFeature(container);
    Features.Arena.ArenaIoC.RegisterArenaFeature(container);
    FactionMapIoC.RegisterFactionMapFeature(container);
    CharacterCreationIoC.RegisterCharacterCreationFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:71: Features.Arena.ArenaIoC.RegisterArenaFeature(container);
    FactionMapIoC.RegisterFactionMapFeature(container);
    CharacterCreationIoC.RegisterCharacterCreationFeature(container);
    InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:72: FactionMapIoC.RegisterFactionMapFeature(container);
    CharacterCreationIoC.RegisterCharacterCreationFeature(container);
    InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
    DiplomacyIoC.RegisterDiplomacyFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:73: CharacterCreationIoC.RegisterCharacterCreationFeature(container);
    InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
    DiplomacyIoC.RegisterDiplomacyFeature(container);
    RaceAgeIoC.RegisterRaceAgeFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:74: InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
    DiplomacyIoC.RegisterDiplomacyFeature(container);
    RaceAgeIoC.RegisterRaceAgeFeature(container);
    ExecutionIoC.RegisterExecutionFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:75: DiplomacyIoC.RegisterDiplomacyFeature(container);
    RaceAgeIoC.RegisterRaceAgeFeature(container);
    ExecutionIoC.RegisterExecutionFeature(container);
    EncyclopediaIoC.RegisterEncyclopediaFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:76: RaceAgeIoC.RegisterRaceAgeFeature(container);
    ExecutionIoC.RegisterExecutionFeature(container);
    EncyclopediaIoC.RegisterEncyclopediaFeature(container);
    StartupResourcesIoC.RegisterStartupResourcesFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:77: ExecutionIoC.RegisterExecutionFeature(container);
    EncyclopediaIoC.RegisterEncyclopediaFeature(container);
    StartupResourcesIoC.RegisterStartupResourcesFeature(container);
    TroopWeightIoC.RegisterTroopWeightFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:78: EncyclopediaIoC.RegisterEncyclopediaFeature(container);
    StartupResourcesIoC.RegisterStartupResourcesFeature(container);
    TroopWeightIoC.RegisterTroopWeightFeature(container);
    AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:79: StartupResourcesIoC.RegisterStartupResourcesFeature(container);
    TroopWeightIoC.RegisterTroopWeightFeature(container);
    AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
    WargIoC.RegisterWargFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:80: TroopWeightIoC.RegisterTroopWeightFeature(container);
    AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
    WargIoC.RegisterWargFeature(container);
    // DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.

 succeeded in 1705ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:505: var service = TAOM.IoC.Resolve<TAOM.Features.EditorCacheRebuild.IRuntimeCacheRebuildService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AdvancedCombatBehavior.cs:16: _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AdvancedCombatBehavior.cs:17: _debugService = IoC.Resolve<ISpatialGridDebugService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:19: private static IArmyTargetingService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:47: _service  ??= IoC.Resolve<IArmyTargetingService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:29: private static IBanditScalingService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:32: _service ??= TAOM.IoC.Resolve<IBanditScalingService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:53: var passiveService = IoC.Resolve<ICareerPassiveService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs:21: _dataService = IoC.Resolve<ICareerDataService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs:32: _dataService = IoC.Resolve<ICareerDataService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs:34: _passiveService = IoC.Resolve<ICareerPassiveService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:36: var service = IoC.Resolve<INarrativeHorseGuardService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:75: var service = IoC.Resolve<INarrativeHorseGuardService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:114: var service = IoC.Resolve<INarrativeHorseGuardService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:27: var service = IoC.Resolve<ICCBodyPropertiesService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:35: var service = IoC.Resolve<ICCBodyPropertiesService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:33: try { _filterService = IoC.Resolve<ICultureRaceFilterService>(); } catch { /* IoC not ready */ }
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\BattleActionBar\Hooks\BattleActionBarMissionView.cs:37: _service = IoC.Resolve<IBattleActionBarService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:21: _overlay ??= IoC.Resolve<IOOBOverlayService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:22: _overlay ??= IoC.Resolve<IOOBOverlayService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:10: // Lifecycle: `ResetForUnload()` MUST be called from `SubModule.OnSubModuleUnloaded`
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:57: public static void ResetForUnload()
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:65: try { _service = TAOM.IoC.Resolve<ICrashReportService>(); }
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:55: IoC.Resolve<IEquipmentPresetService>(),
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:20: private static IFiefHubService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:52: var service = _service ??= IoC.Resolve<IFiefHubService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Configuration\RacePositionConfig.cs:43: var pathService = IoC.Resolve<IPathService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Configuration\RacePositionConfig.cs:72: var pathService = IoC.Resolve<IPathService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:22: var service = IoC.Resolve<ICharacterSpawnerService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\UI\MessengerEncyclopediaMixin.cs:33: _service = TAOM.IoC.Resolve<IMessengerService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\MixedFormations\Hooks\MixedFormationsMissionBehavior.cs:28: _service = IoC.Resolve<IFormationLayoutService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:24: var service = _service ??= IoC.Resolve<IFormationLayoutService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\NativeSkinFixesInstaller.cs:128: /// Reverses install — called from <see cref="SubModule.OnSubModuleUnloaded"/>
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:56: var service = IoC.Resolve<IQuickActionsService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementGuards\Hooks\GuardsCampaignBehavior_GetSuitableSpear_Patch.cs:12: private static ISettlementGuardService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementGuards\Hooks\GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:17: private static ISettlementGuardService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:27: private static INameplateFadeService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Hooks\SiegeDismountMissionBehavior.cs:23: _service = IoC.Resolve<ISiegeDismountService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:40: _service ??= IoC.Resolve<ICavalryChargeService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\Hooks\SmartCavalryAIMissionBehavior.cs:40: _service = IoC.Resolve<ICavalryChargeService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\UI\SpecialResourceMapBarMixin.cs:26: _service = IoC.Resolve<ISpecialResourceService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderMissionBehavior.cs:41: _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderMissionBehavior.cs:42: _spawnerService = IoC.Resolve<ISpiderSpawnerService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\BehaviorTreeElements\SpiderAttackTask.cs:25: var attackService = IoC.Resolve<ISpiderAttackService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\WargMissionBehavior.cs:41: _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\WargAttackTask.cs:31: IoC.Resolve<IWargAttackService>().WargAttack(wargAdapter);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\spcultures.xml','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData\\spcultures.xml' -Pattern 'id=\"forest_bandits\"|id=\"mountain_bandits\"|id=\"desert_bandits\"|id=\"steppe_bandits\"|id=\"sea_raiders\"|default_party_template|bandit_boss_party_template|is_bandit=\"true\"' -Context 0,3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } } | Select-Object -First 140' in C:\Users\mikew\source\repos\TAOM
 succeeded in 775ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:17: default_party_template="PartyTemplate.kingdom_hero_party_empire_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_empire_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_empire_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_empire_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:863: default_party_template="PartyTemplate.kingdom_hero_party_aserai_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_aserai_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_aserai_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_aserai_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:1406: default_party_template="PartyTemplate.kingdom_hero_party_sturgia_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_sturgia_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_sturgia_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_sturgia_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:2017: default_party_template="PartyTemplate.kingdom_hero_party_vlandia_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_vlandia_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_vlandia_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_vlandia_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:2607: default_party_template="PartyTemplate.kingdom_hero_party_battania_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_battania_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_battania_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_battania_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:3068: default_party_template="PartyTemplate.kingdom_hero_party_khuzait_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_khuzait_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_khuzait_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_khuzait_template_level_3"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:3744: is_bandit="true"
    can_have_settlement="false"
    encounter_background_mesh="encounter_looter">
    <banner_bearer_replacement_weapons>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4172: <!---Culture id="deserters" name="Deserters" elite_basic_troop="NPCCharacter.deserter" basic_troop="NPCCharacter.deserter" is_bandit="true"/-->
    <Culture
    id="sea_raiders"
    name="{=pBUc2pL8}Sea Raiders"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4174: id="sea_raiders"
    name="{=pBUc2pL8}Sea Raiders"
    bandit_chief="NPCCharacter.sea_raiders_chief"
    bandit_raider="NPCCharacter.sea_raiders_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4182: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_shore_bandit"
    bandit_boss_party_template="PartyTemplate.sea_raiders_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4185: bandit_boss_party_template="PartyTemplate.sea_raiders_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.sturgia_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4464: id="mountain_bandits"
    name="{=mOa9CX13}Mountain Bandits"
    bandit_chief="NPCCharacter.mountain_bandits_chief"
    bandit_raider="NPCCharacter.mountain_bandits_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4472: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_mountain_bandit"
    bandit_boss_party_template="PartyTemplate.mountain_bandits_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4475: bandit_boss_party_template="PartyTemplate.mountain_bandits_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.vlandia_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4652: id="forest_bandits"
    name="{=sAbjtZbB}Forest Bandits"
    bandit_chief="NPCCharacter.forest_bandits_chief"
    bandit_raider="NPCCharacter.forest_bandits_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4660: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_forest_bandit"
    bandit_boss_party_template="PartyTemplate.forest_bandits_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:4663: bandit_boss_party_template="PartyTemplate.forest_bandits_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.battania_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5000: id="desert_bandits"
    name="{=kkUQObZn}Desert Bandits"
    bandit_chief="NPCCharacter.desert_bandits_chief"
    bandit_raider="NPCCharacter.desert_bandits_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5008: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_desert_bandit"
    bandit_boss_party_template="PartyTemplate.desert_bandits_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5011: bandit_boss_party_template="PartyTemplate.desert_bandits_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.aserai_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5228: id="steppe_bandits"
    name="{=e6csp4ha}Steppe Bandits"
    bandit_chief="NPCCharacter.steppe_bandits_chief"
    bandit_raider="NPCCharacter.steppe_bandits_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5236: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_desert_bandit"
    bandit_boss_party_template="PartyTemplate.steppe_bandits_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml:5239: bandit_boss_party_template="PartyTemplate.steppe_bandits_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.khuzait_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:18: default_party_template="PartyTemplate.kingdom_hero_party_empire_template"
    caravan_party_template="PartyTemplate.caravan_template_empire"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_empire"
    militia_party_template="PartyTemplate.militia_empire_template"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:881: default_party_template="PartyTemplate.kingdom_hero_party_aserai_template"
    encounter_background_mesh="encounter_aserai"
    faction_banner_key="11.0.0.4345.4345.764.764.1.0.0.463.1.1.466.466.764.764.1.0.0"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_aserai"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:1439: default_party_template="PartyTemplate.kingdom_hero_party_sturgia_template"
    encounter_background_mesh="encounter_sturgia"
    faction_banner_key="11.12.12.4345.4345.764.764.1.0.0.462.13.13.512.512.764.764.1.0.0"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_sturgia"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:2063: default_party_template="PartyTemplate.kingdom_hero_party_vlandia_template"
    encounter_background_mesh="encounter_vlandia"
    faction_banner_key="11.14.14.1536.1536.764.764.1.0.0.160.15.15.497.497.764.764.1.0.0"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_vlandia"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:2672: default_party_template="PartyTemplate.kingdom_hero_party_battania_template"
    encounter_background_mesh="encounter_battania"
    faction_banner_key="11.2.2.6296.6296.764.764.1.0.0.460.3.3.457.457.764.764.1.0.0"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_battania"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3146: default_party_template="PartyTemplate.kingdom_hero_party_khuzait_template"
    encounter_background_mesh="encounter_khuzait"
    faction_banner_key="11.10.10.1536.1536.764.764.1.0.0.461.11.11.497.497.764.764.1.0.0"
    elite_caravan_party_template="PartyTemplate.elite_caravan_template_khuzait"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3812: is_bandit="true"
    can_have_settlement="false"
    encounter_background_mesh="encounter_looter">
    <banner_bearer_replacement_weapons>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3826: <!---Culture id="deserters" name="Deserters" elite_basic_troop="NPCCharacter.deserter" basic_troop="NPCCharacter.deserter" is_bandit="true"/-->
    <Culture
    id="sea_raiders"
    name="{=pBUc2pL8}Sea Raiders"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3828: id="sea_raiders"
    name="{=pBUc2pL8}Sea Raiders"
    bandit_chief="NPCCharacter.sea_raiders_chief"
    bandit_raider="NPCCharacter.sea_raiders_raider"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3836: is_bandit="true"
    can_have_settlement="true"
    encounter_background_mesh="encounter_shore_bandit"
    bandit_boss_party_template="PartyTemplate.sea_raiders_boss_party_template">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3839: bandit_boss_party_template="PartyTemplate.sea_raiders_boss_party_template">
    <banner_bearer_replacement_weapons>
    <item
    id="Item.sturgia_sword_1_t2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spcultures.xml:3852: id="mountain_bandits"
    name="{=mOa9CX13}Mountain Bandits"
    bandit_chief="NPCCharacter.mountain_bandits_chief"
    bandit_raider="NPCCharacter.mountain_bandits_raider"

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern 'id=\"dunland_wolf_raider\"|id=\"kharaghul_raider\"|id=\"harad_camelrider\"|id=\"gundabad_despoiler_of_the_vale\"|id=\"umbar_elite_root00\"' -Context 0,12 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml | Select-String -Pattern 'id=\"dunland_peasant\"|id=\"balcoth_volunteer\"|id=\"harad_levy\"|id=\"gundabad_snaga\"|id=\"aux_basic\"' -Context 0,6 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 983ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_dunland.xml:751: id="dunland_wolf_raider"
    default_group="Infantry"
    level="21"
    name="{=aom_dunland_wolf_raider_name}[Dunland] Blaidd-lûth Raider"
    occupation="Soldier"
    culture="Culture.empire">
    <face>
    <face_key_template
    value="BodyProperty.fighter_battania" />
    </face>
    <skills>
    <skill
    id="Athletics"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_gundabad.xml:1483: id="gundabad_despoiler_of_the_vale"
    race="pale_uruk"
    default_group="HorseArcher"
    level="26"
    name="{=aom_gundabad_despoiler_of_the_vale_name}[Gundabad] Despoiler of the Vale"
    occupation="Soldier"
    culture="Culture.gundabad">
    <face>
    <face_key_template
    value="BodyProperty.fighter_empire" />
    </face>
    <skills>
    <skill
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_harad.xml:758: id="harad_camelrider"
    default_group="Cavalry"
    level="21"
    name="{=aom_harad_camelrider_name}[Harad] Camel Rider"
    occupation="Soldier"
    culture="Culture.aserai">
    <face>
    <face_key_template
    value="BodyProperty.fighter_aserai" />
    </face>
    <skills>
    <skill
    id="Athletics"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_rhun_new.xml:806: id="kharaghul_raider"
    default_group="Cavalry"
    level="21"
    name="{=aom_kharaghul_raider_name}[Rhûn] Kharaghûl Raider"
    occupation="Soldier"
    culture="Culture.khuzait">
    <face>
    <face_key_template value="BodyProperty.fighter_khuzait" />
    </face>
    <skills>
    <skill id="Athletics" value="105" />
    <skill id="Riding" value="138" />
    <skill id="OneHanded" value="115" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_umbar.xml:707: id="umbar_elite_root00"
    default_group="Infantry"
    level="21"
    name="{=aom_umbar_elite_root00_name}[Umbar] Rozadan Footmen"
    occupation="Soldier"
    culture="Culture.umbar">
    <face>
    <face_key_template
    value="BodyProperty.fighter_aserai" />
    </face>
    <skills>
    <skill
    id="Athletics"

 succeeded in 1534ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_dunland.xml:8: id="dunland_peasant"
    default_group="Infantry"
    level="6"
    name="{=aom_dunland_peasant_name}[Dunland] Peasant"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.empire">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_gundabad.xml:4: id="gundabad_snaga"
    race="pale_uruk"
    default_group="Infantry"
    level="11"
    name="{=aom_gundabad_snaga_name}[Gundabad] Pale Uruk Warrior"
    occupation="Soldier"
    is_basic_troop="true"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_harad.xml:6: id="harad_levy"
    default_group="Infantry"
    level="6"
    name="{=aom_harad_levy_name}[Harad] Levy"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.aserai">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_rhun_new.xml:5: id="balcoth_volunteer"
    default_group="Infantry"
    level="11"
    name="{=aom_balcoth_volunteer_name}[Rhûn] Balcoth Volunteer"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.khuzait">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_umbar.xml:6: id="aux_basic"
    default_group="Infantry"
    level="6"
    name="{=aom_aux_basic_name}[Umbar] Auxiliary Recruit"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.umbar">

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\spnpccharacters.xml','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData\\spnpccharacters.xml' -Pattern 'id=\"forest_bandits_boss\"|id=\"sea_raiders_boss\"|id=\"mountain_bandits_boss\"|id=\"desert_bandits_boss\"|id=\"steppe_bandits_boss\"' -Context 0,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
2026-05-27T21:57:36.640695Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path 'E:\Steam\steamapps\common\Mount & Blade II Banne[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spnpccharacters.xml' because it does not exist.[0m

 exited 1 in 843ms:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path 'E:\Steam\steamapps\common\Mount & Blade II Banne[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\spnpccharacters.xml' because it does not exist.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter '*.xml' | Select-String -Pattern 'id=\"forest_bandits_boss\"' -List | Select-Object -ExpandProperty Path" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter '*npccharacters*.xml' | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1374ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spnpccharacters.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\std_spnpccharacters_xml.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\BR\std_spnpccharacters_xml_por-BR.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\CNs\std_spnpccharacters_xml-zho-CN.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\CNt\std_spnpccharacters_xml-zho-HK.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\DE\std_spnpccharacters_xml_ger-DE.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\FR\std_spnpccharacters_xml_fre-FR.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\IT\std_spnpccharacters_xml_ita-IT.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\JP\std_spnpccharacters_xml_jpn.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\KO\std_spnpccharacters_xml_kor.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\PL\std_spnpccharacters_xml_pol.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\RU\std_spnpccharacters_xml_rus.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\SP\std_spnpccharacters_xml_spa-M9.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\Languages\TR\std_spnpccharacters_xml_tur.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCoreMP\ModuleData\battlelink_npccharacters.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\spnpccharacters.xml

 succeeded in 6806ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCoreMP\ModuleData\battlelink_npccharacters.xml

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData\\bandits.xml' -Pattern 'id=\"forest_bandits_boss\"|id=\"sea_raiders_boss\"|id=\"mountain_bandits_boss\"|id=\"desert_bandits_boss\"|id=\"steppe_bandits_boss\"' -Context 0,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 747ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml:3536: id="sea_raiders_boss"
    default_group="Infantry"
    name="{=C1Fkzdvu}Sea Raider Boss"
    level="26"
    occupation="Bandit"
    culture="Culture.sea_raiders">
    <face>
    <face_key_template
    value="BodyProperty.fighter_lakepike" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml:3621: id="mountain_bandits_boss"
    default_group="Infantry"
    name="{=ziOBJSzR}Mountain Bandit Boss"
    level="26"
    occupation="Bandit"
    culture="Culture.mountain_bandits">
    <face>
    <face_key_template
    value="BodyProperty.fighter_mountain_bandits" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml:3703: id="forest_bandits_boss"
    default_group="Ranged"
    name="{=dc2qxanZ}Forest Bandit Boss"
    level="26"
    occupation="Bandit"
    culture="Culture.forest_bandits">
    <face>
    <face_key_template
    value="BodyProperty.fighter_mountain_bandits" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml:3785: id="desert_bandits_boss"
    default_group="Cavalry"
    name="{=Q2V4uQrr}Desert Bandit Boss"
    level="26"
    upgrade_requires="ItemCategory.war_horse"
    occupation="Bandit"
    culture="Culture.desert_bandits">
    <face>
    <face_key_template
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\bandits.xml:3871: id="steppe_bandits_boss"
    default_group="HorseArcher"
    name="{=Bg63YFnh}Steppe Bandit Boss"
    upgrade_requires="ItemCategory.war_horse"
    level="26"
    occupation="Bandit"
    culture="Culture.steppe_bandits">
    <face>
    <face_key_template

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module -Filter SubModule.xml -Recurse | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\SubModule.xml -Pattern 'settlements|taom_spcultures|taom_partyTemplates|XmlNode|ModuleData|DependedModule|TAOM_Map' -Context 0,2 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 776ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml

 succeeded in 1221ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:10: <DependedModules>
    <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:11: <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:12: <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:13: <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
    </DependedModules>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:14: <DependedModule Id="CustomBattle" />
    </DependedModules>
    <DependedModuleMetadatas>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:15: </DependedModules>
    <DependedModuleMetadatas>
    <DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:16: <DependedModuleMetadatas>
    <DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
    <DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:17: <DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
    <DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
    <DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:18: <DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
    <DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
    <DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:19: <DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
    <DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
    </DependedModuleMetadatas>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:20: <DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
    </DependedModuleMetadatas>
    <SubModules>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:21: </DependedModuleMetadatas>
    <SubModules>
    <SubModule>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:35: <XmlNode>
    <XmlName id="Kingdoms" path="spkingdoms"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:41: </XmlNode>
    <!-- XSLT transformations to rename vanilla cultures to TAOM names -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:43: <XmlNode>
    <XmlName id="SPCultures" path="spcultures"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:51: </XmlNode>
    <!-- XSLT transformations to rename vanilla clans to TAOM names -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:53: <XmlNode>
    <XmlName id="Factions" path="spclans"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:59: </XmlNode>
    <!-- XSLT transformations to rename vanilla lords to TAOM names and add BodyProperties -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:61: <XmlNode>
    <XmlName id="NPCCharacters" path="lords"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:69: </XmlNode>
    <!-- XSLT transformations to add TAOM biographical text to heroes -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:71: <XmlNode>
    <XmlName id="Heroes" path="heroes"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:77: </XmlNode>
    
    <!-- ============================================== -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:84: <XmlNode>
    <XmlName id="SPCultures" path="taom_spcultures"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:85: <XmlName id="SPCultures" path="taom_spcultures"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:92: </XmlNode>
    
    <!-- TAOM custom kingdoms -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:95: <XmlNode>
    <XmlName id="Kingdoms" path="taom_spkingdoms"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:101: </XmlNode>
    
    <!-- TAOM additional clans (extended vanilla + custom cultures) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:104: <XmlNode>
    <XmlName id="Factions" path="characters/clans"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:110: </XmlNode>
    
    <!-- TAOM additional heroes -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:113: <XmlNode>
    <XmlName id="Heroes" path="characters/heroes"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:119: </XmlNode>
    
    <!-- TAOM additional lords (NPCCharacters) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:122: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/lords"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:130: </XmlNode>
    
    <!-- TAOM body properties (character appearance templates) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:133: <XmlNode>
    <XmlName id="BodyProperties" path="TAOM_bodyproperties"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:140: </XmlNode>
    
    <!-- ============================================== -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:146: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_gondor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:154: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rohan"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:155: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rohan"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:163: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_erebor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:164: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_erebor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:172: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dale"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:173: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dale"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:181: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rivendell"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:182: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rivendell"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:190: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_mirkwood"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:191: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_mirkwood"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:199: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_mordor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:200: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_mordor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:208: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_isengard"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:209: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_isengard"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:217: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dolguldur"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:218: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dolguldur"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:226: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dolguldur"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:227: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dolguldur"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:235: </XmlNode>
    <!-- Spider creature anchor (used by SpiderSpawnerService for AgentBuildData) -->
    <!-- DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:238: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/spider_creature"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:246: </XmlNode>
    -->
    
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:249: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_dolguldur"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:257: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="taom_education_character_templates"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:258: <XmlNode>
    <XmlName id="NPCCharacters" path="taom_education_character_templates"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:264: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_education_equipment_templates"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:265: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_education_equipment_templates"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:271: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_child_equipment_templates"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:272: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_child_equipment_templates"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:278: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_gundabad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:279: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_gundabad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:287: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_gundabad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:288: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_gundabad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:296: </XmlNode>
    <XmlNode>
    <XmlName id="partyTemplates" path="taom_partyTemplates"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:297: <XmlNode>
    <XmlName id="partyTemplates" path="taom_partyTemplates"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:298: <XmlName id="partyTemplates" path="taom_partyTemplates"/>
    <IncludedGameTypes>
    <GameType value ="Campaign"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:305: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_gundabad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:306: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_gundabad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:314: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_mirkwood"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:315: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_mirkwood"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:323: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_mirkwood"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:324: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_mirkwood"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:332: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_erebor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:333: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_erebor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:341: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_erebor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:342: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_erebor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:350: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rivendell"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:351: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rivendell"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:359: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rivendell"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:360: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rivendell"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:368: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_gondor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:369: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_gondor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:377: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_gondor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:378: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_gondor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:386: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_mordor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:387: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_mordor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:395: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_mordor"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:396: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_mordor"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:404: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_harad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:405: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_harad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:413: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rhun_new"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:414: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_rhun_new"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:422: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dunland"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:423: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_dunland"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:431: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_umbar"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:432: <XmlNode>
    <XmlName id="NPCCharacters" path="troops/troops_umbar"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:440: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rohan"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:441: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rohan"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:449: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rohan"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:450: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rohan"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:458: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dunland"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:459: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dunland"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:467: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_dunland"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:468: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_dunland"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:476: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_harad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:477: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_harad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:485: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_harad"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:486: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_harad"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:494: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rhun"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:495: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_rhun"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:503: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rhun"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:504: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_rhun"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:512: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_dale"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:513: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_dale"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:521: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_lothlorien"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:522: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_lothlorien"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:530: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_umbar"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:531: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_umbar"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:539: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_isengard"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:540: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_isengard"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:548: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_lothlorien"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:549: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_lothlorien"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:557: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_shaghana"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:558: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_shaghana"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:566: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_abanissa"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:567: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_abanissa"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:575: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_umbar"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:576: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_umbar"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:584: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dale"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:585: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_dale"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:593: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_khand"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:594: <XmlNode>
    <XmlName id="NPCCharacters" path="characters/npcs_khand"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:602: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_isengard"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:603: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_isengard"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:611: </XmlNode>
    <XmlNode>
    <XmlName id="NPCCharacters" path="taom_wanderers"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:612: <XmlNode>
    <XmlName id="NPCCharacters" path="taom_wanderers"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:620: </XmlNode>
    <!-- TAOM lord skill templates — referenced from NPCCharacter[@skill_template]
    on every adult lord across lords.xml + lords.xslt. Generated by
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:624: <XmlNode>
    <XmlName id="SkillSets" path="taom_lord_skill_sets"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:632: </XmlNode>
    
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:634: <XmlNode>
    <XmlName id="SkillSets" path="taom_wanderer_skill_sets"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:642: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_wanderer_equipment"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:643: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_wanderer_equipment"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:651: </XmlNode>
    <!-- Named companion characters (lore heroes as recruitable wanderers) -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:653: <XmlNode>
    <XmlName id="NPCCharacters" path="named_companions/named_companions"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:659: </XmlNode>
    <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_named_companions"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:660: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_named_companions"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:666: </XmlNode>
    <!-- Character creation equipment rosters for 10 custom cultures -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:668: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_char_creation_equipment"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:674: </XmlNode>
    <!-- Career-archetype starting equipment overrides applied after culture-default
    roster at end of character creation (CareerStartingEquipmentService). -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:677: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_career_starting_equipment"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:683: </XmlNode>
    <!-- v1.4.3 mandatory equipment rosters per culture (IsLordTemplate variants).
    Generated by tools/generate_lord_template_equipment.py. Satisfies the
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:690: <XmlNode>
    <XmlName id="EquipmentRosters" path="equipmentsets/taom_lord_template_equipment"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:698: </XmlNode>
    <!-- XSLT to remove vanilla faction strings and replace with LOTR-themed -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:700: <XmlNode>
    <XmlName id="GameText" path="module_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:706: </XmlNode>
    <!-- XSLT to remove "the" before faction names in action/comment strings -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:708: <XmlNode>
    <XmlName id="GameText" path="action_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:714: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="comment_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:715: <XmlNode>
    <XmlName id="GameText" path="comment_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:721: </XmlNode>
    <!-- TAOM faction/culture strings for all cultures -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:723: <XmlNode>
    <XmlName id="GameText" path="taom_module_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:729: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="taom_wanderer_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:730: <XmlNode>
    <XmlName id="GameText" path="taom_wanderer_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:738: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="named_companions/named_companion_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:739: <XmlNode>
    <XmlName id="GameText" path="named_companions/named_companion_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:745: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="taom_cc_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:746: <XmlNode>
    <XmlName id="GameText" path="taom_cc_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:754: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="taom_career_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:755: <XmlNode>
    <XmlName id="GameText" path="taom_career_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:763: </XmlNode>
    <XmlNode>
    <XmlName id="GameText" path="taom_messenger_strings"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:764: <XmlNode>
    <XmlName id="GameText" path="taom_messenger_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:772: </XmlNode>
    <!-- Loc keys harvested from XSLT transforms (kingdom/culture/clan/lord/hero descriptions) -->
    <XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:774: <XmlNode>
    <XmlName id="GameText" path="taom_xslt_strings"/>
    <IncludedGameTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:782: </XmlNode>
    <XmlNode>
    <XmlName id="BannerIcons" path="banner_icons"/>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:783: <XmlNode>
    <XmlName id="BannerIcons" path="banner_icons"/>
    </XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:785: </XmlNode>
    <XmlNode>
    <XmlName id="CustomBattleScenes" path="custom_battle_scenes" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:786: <XmlNode>
    <XmlName id="CustomBattleScenes" path="custom_battle_scenes" />
    </XmlNode>
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:788: </XmlNode>
    </Xmls>
    </Module>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SubModule.xml' -TotalCount 120" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SubModule.xml' -Pattern 'DependedModule|DependedModuleMetadata|TAOM|TAOM_Map|SandBoxCore|XmlName|settlements|spcultures' -Context 0,2 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim(); foreach($c in $_.Context.PostContext){ '"'    '+"'$c.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData' -File | Select-Object -ExpandProperty Name" in C:\Users\mikew\source\repos\TAOM
 succeeded in 783ms:
<Module>
	<Name value="TAOM_Map"/>
	<Id value="TAOM_Map"/>
	<Version value="v2.0.0"/>
	<DefaultModule value="false"/>
	<ModuleCategory value="Singleplayer"/>
	<Official value="false"/>
	<DependedModules>
		<DependedModule Id="Native"/>
		<DependedModule Id="SandBoxCore"/>
		<DependedModule Id="Sandbox"/>
		<DependedModule Id="CustomBattle"/>
		<DependedModule Id="StoryMode"/>
	</DependedModules>
	<SubModules/>
	<Xmls>
		<XmlNode>
			<XmlName id="Items" path="items"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="SPCultures" path="spcultures"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="NPCCharacters" path="spnpccharacters"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="partyTemplates" path="partyTemplates"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Kingdoms" path="spkingdoms"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Factions" path="spclans"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="WorkshopTypes" path="spworkshops"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Settlements" path="settlements"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
	</Xmls>
</Module>


 succeeded in 806ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:2: <Name value="TAOM_Map"/>
    <Id value="TAOM_Map"/>
    <Version value="v2.0.0"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:3: <Id value="TAOM_Map"/>
    <Version value="v2.0.0"/>
    <DefaultModule value="false"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:8: <DependedModules>
    <DependedModule Id="Native"/>
    <DependedModule Id="SandBoxCore"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:9: <DependedModule Id="Native"/>
    <DependedModule Id="SandBoxCore"/>
    <DependedModule Id="Sandbox"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:10: <DependedModule Id="SandBoxCore"/>
    <DependedModule Id="Sandbox"/>
    <DependedModule Id="CustomBattle"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:11: <DependedModule Id="Sandbox"/>
    <DependedModule Id="CustomBattle"/>
    <DependedModule Id="StoryMode"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:12: <DependedModule Id="CustomBattle"/>
    <DependedModule Id="StoryMode"/>
    </DependedModules>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:13: <DependedModule Id="StoryMode"/>
    </DependedModules>
    <SubModules/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:14: </DependedModules>
    <SubModules/>
    <Xmls>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:18: <XmlName id="Items" path="items"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:25: <XmlName id="SPCultures" path="spcultures"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:32: <XmlName id="NPCCharacters" path="spnpccharacters"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:39: <XmlName id="partyTemplates" path="partyTemplates"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:46: <XmlName id="Kingdoms" path="spkingdoms"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:53: <XmlName id="Factions" path="spclans"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:60: <XmlName id="WorkshopTypes" path="spworkshops"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml:67: <XmlName id="Settlements" path="settlements"/>
    <IncludedGameTypes>
    <GameType value="Campaign"/>

 succeeded in 1110ms:
action_sets.xml
action_types.xml
collision_infos.xml
combat_parameters.xml
face_animations.xml
item_holsters.xml
items.xml
native_parameters.xml
partyTemplates.xml
physics_materials.xml
project.mbproj
settlement_track_instruments.xml
settlement_tracks.xml
settlements.xml
settlements.xml.bak
settlements.xslt
skins.xml
spclans.xml
spcultures.xml
spkingdoms.xml
spnpccharacters.xml
spworkshops.xml

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\spcultures.xml' -Pattern 'dunland_raiders|rhun_raiders|harad_raiders|gundabad_raiders|umbar_corsairs|id=\"dunland\"|id=\"gundabad\"|id=\"umbar\"' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
 succeeded in 680ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '[xml]$xml = Get-Content -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\settlements.xml'; 'TAOM_Map settlements.xml parsed OK'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\Languages' -Recurse -Filter loc_settlements.xml | ForEach-Object { try { [xml]"'$x=Get-Content -Path $_.FullName; } catch { '"'{0}: {1}' -f "'$_.FullName,$_.Exception.Message } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\ModuleData\\Languages' -Recurse -Filter loc_settlements.xml | ForEach-Object { "'$n=(Select-String -Path $_.FullName -Pattern '"'Dunlending Raider''s Camp|Gundabad Orc Raider''s Camp|Haradrim Raider''s Camp|Rhûn Raider''s Camp|Corsair''s Cove').Count; '{0}: {1}' -f "'$_.Directory.Name,$n }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 890ms:
TAOM_Map settlements.xml parsed OK

 succeeded in 864ms:
BR: 99
CNs: 99
CNt: 99
DE: 99
FR: 99
IT: 99
JP: 99
KO: 99
PL: 99
RU: 99
SP: 99
TR: 99

 succeeded in 999ms:
  ✓ Map changed files and diffs
  ✓ Verify known suspects against TAOM and vanilla code
  → Cold-review BanditManagement files and data changes
  • Report confirmed/disputed findings with severity
  ✓ Map changed files and diffs
  ✓ Verify known suspects against TAOM and vanilla code
  ✓ Cold-review BanditManagement files and data changes
  → Report confirmed/disputed findings with severity
codex
**Known Suspects**

1. DISPUTED: Patch39 parameter names match vanilla. TAOM uses `party, partyTemplate` at `Patch39_BanditPartySize.cs:35`; vanilla is `FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)` at `DefaultPartySizeLimitModel.cs:427`.

2. DISPUTED: The lazy `_service ??=` race is not a confirmed defect here. Vanilla call sites are campaign creation/replenish paths (`MobileParty.cs:2638`, `PatrolPartiesCampaignBehavior.cs:113`), and a double DryIoc singleton resolve would be redundant, not corrupting. Separate issue: the patch is not registered, below.

3. DISPUTED: No vanilla read occurs between return and postfix mutation. Vanilla returns the built roster at `DefaultPartySizeLimitModel.cs:457-463`; caller consumes it after the patched call at `MobileParty.cs:2638-2648`.

4. DISPUTED for current data: `PartyTemplateObject.Deserialize` does not enforce unique stack characters (`PartyTemplateObject.cs:37-40`), but the 10 new TAOM templates have no duplicate troop IDs. This is latent only if future data adds duplicates.

5. DISPUTED: `Campaign.Current?.PlayerProgress` is not the meaningful race. C# null-conditional reads the receiver once; vanilla density properties already dereference `Campaign.Current.PlayerProgress` directly at `DefaultBanditDensityModel.cs:26-28`.

6. DISPUTED: Not too early. `IoC.Configure()` runs in `SubModule.cs:93`, BanditManagement registers at `IoC.cs:97`, and the model resolves during `OnGameStart` at `SubModule.cs:358`, matching existing model patterns.

7. DISPUTED: Runtime cap edge is safe. `BanditScalingSettingsProvider.cs:38-42` clamps `MinPartiesToInfest` down to the live `MaxPartiesPerHideoutCap`, so cap `1` returns min `1`.

8. DISPUTED: Cosmetic only. Warning labels are hardcoded camelCase in `BanditScalingConfigProvider.cs:65` etc.; behavior is unaffected.

9. CONFIRMED: See HIGH finding. Vanilla does not create bandit clans from `Culture.is_bandit`; clan bandit status is loaded from clan XML.

10. DISPUTED for current file: the regex is narrow and would miss reordered/split settlement tags, but current TAOM_Map has 99 one-line hideout settlement tags, all migrated to new cultures and zero old culture refs.

11. DISPUTED: IDs preserved. Current external TAOM_Map still has IDs like `hideout_forest_1` while only name/culture changed.

12. DISPUTED with caveat: inherited virtual dispatch would reach the patched base implementation because `TaomPartySizeModel` does not override `FindAppropriateInitialRosterForMobileParty` (`TaomPartySizeModel.cs:10,21-28`). But Patch39 currently never registers.

**Findings**

[CRITICAL] Main/_Module/ModuleData/taom_partyTemplates.xml:1482 — XML validity — The new block puts `--` inside an XML comment (`{culture}_raider_party_template  -- regular...`). XML comments cannot contain double hyphen; PowerShell `[xml]` parse fails at this line. Fix by removing/rewording the double hyphens.

[HIGH] Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs:26 — Harmony registration — Patch39 has `[HarmonyPatch]` but no `[HarmonyPatchCategory]`, and `SubModule.cs:489-497` patches categories through Patch38 only. Result: bandit party size scaling is dead code. Fix by adding a category and patching it from `SubModule`.

[HIGH] Main/_Module/ModuleData/taom_spcultures.xml:4170 — Bandit clan contract — New bandit cultures are only `Culture` rows. Vanilla `Clan.IsBanditFaction` comes from clan XML (`Clan.cs:881`), and `Clan.BanditFactions` iterates existing clans (`Clan.cs:438-445`). Migrated hideouts use `Culture.dunland_raiders` etc., but no matching bandit `Faction` rows exist, so vanilla old bandit clans remain tied to old cultures and spawn logic can stall/crash on missing hideout culture buckets (`BanditSpawnCampaignBehavior.cs:490-493`). Fix by adding/repointing bandit clan rows with `is_bandit=true`, culture, home settlement, and default party template.

[HIGH] E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/SubModule.xml:8 — Cross-module data contract — TAOM_Map settlements now reference cultures defined in TAOM Main, but TAOM_Map neither defines `dunland_raiders` etc. nor depends on TAOM; Main also does not depend on TAOM_Map. This makes load order/launcher profile correctness accidental. Fix by moving the new culture/clan data into TAOM_Map or adding explicit dependency metadata in the consuming module.

CRITICAL: 1 | HIGH: 3 | MEDIUM: 0 | LOW: 0  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 59600 (child process of PID 39248) has been terminated.
SUCCESS: The process with PID 39248 (child process of PID 58324) has been terminated.
SUCCESS: The process with PID 58324 (child process of PID 72884) has been terminated.
SUCCESS: The process with PID 72884 (child process of PID 71712) has been terminated.
tokens used
259,490
**Known Suspects**

1. DISPUTED: Patch39 parameter names match vanilla. TAOM uses `party, partyTemplate` at `Patch39_BanditPartySize.cs:35`; vanilla is `FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)` at `DefaultPartySizeLimitModel.cs:427`.

2. DISPUTED: The lazy `_service ??=` race is not a confirmed defect here. Vanilla call sites are campaign creation/replenish paths (`MobileParty.cs:2638`, `PatrolPartiesCampaignBehavior.cs:113`), and a double DryIoc singleton resolve would be redundant, not corrupting. Separate issue: the patch is not registered, below.

3. DISPUTED: No vanilla read occurs between return and postfix mutation. Vanilla returns the built roster at `DefaultPartySizeLimitModel.cs:457-463`; caller consumes it after the patched call at `MobileParty.cs:2638-2648`.

4. DISPUTED for current data: `PartyTemplateObject.Deserialize` does not enforce unique stack characters (`PartyTemplateObject.cs:37-40`), but the 10 new TAOM templates have no duplicate troop IDs. This is latent only if future data adds duplicates.

5. DISPUTED: `Campaign.Current?.PlayerProgress` is not the meaningful race. C# null-conditional reads the receiver once; vanilla density properties already dereference `Campaign.Current.PlayerProgress` directly at `DefaultBanditDensityModel.cs:26-28`.

6. DISPUTED: Not too early. `IoC.Configure()` runs in `SubModule.cs:93`, BanditManagement registers at `IoC.cs:97`, and the model resolves during `OnGameStart` at `SubModule.cs:358`, matching existing model patterns.

7. DISPUTED: Runtime cap edge is safe. `BanditScalingSettingsProvider.cs:38-42` clamps `MinPartiesToInfest` down to the live `MaxPartiesPerHideoutCap`, so cap `1` returns min `1`.

8. DISPUTED: Cosmetic only. Warning labels are hardcoded camelCase in `BanditScalingConfigProvider.cs:65` etc.; behavior is unaffected.

9. CONFIRMED: See HIGH finding. Vanilla does not create bandit clans from `Culture.is_bandit`; clan bandit status is loaded from clan XML.

10. DISPUTED for current file: the regex is narrow and would miss reordered/split settlement tags, but current TAOM_Map has 99 one-line hideout settlement tags, all migrated to new cultures and zero old culture refs.

11. DISPUTED: IDs preserved. Current external TAOM_Map still has IDs like `hideout_forest_1` while only name/culture changed.

12. DISPUTED with caveat: inherited virtual dispatch would reach the patched base implementation because `TaomPartySizeModel` does not override `FindAppropriateInitialRosterForMobileParty` (`TaomPartySizeModel.cs:10,21-28`). But Patch39 currently never registers.

**Findings**

[CRITICAL] Main/_Module/ModuleData/taom_partyTemplates.xml:1482 — XML validity — The new block puts `--` inside an XML comment (`{culture}_raider_party_template  -- regular...`). XML comments cannot contain double hyphen; PowerShell `[xml]` parse fails at this line. Fix by removing/rewording the double hyphens.

[HIGH] Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs:26 — Harmony registration — Patch39 has `[HarmonyPatch]` but no `[HarmonyPatchCategory]`, and `SubModule.cs:489-497` patches categories through Patch38 only. Result: bandit party size scaling is dead code. Fix by adding a category and patching it from `SubModule`.

[HIGH] Main/_Module/ModuleData/taom_spcultures.xml:4170 — Bandit clan contract — New bandit cultures are only `Culture` rows. Vanilla `Clan.IsBanditFaction` comes from clan XML (`Clan.cs:881`), and `Clan.BanditFactions` iterates existing clans (`Clan.cs:438-445`). Migrated hideouts use `Culture.dunland_raiders` etc., but no matching bandit `Faction` rows exist, so vanilla old bandit clans remain tied to old cultures and spawn logic can stall/crash on missing hideout culture buckets (`BanditSpawnCampaignBehavior.cs:490-493`). Fix by adding/repointing bandit clan rows with `is_bandit=true`, culture, home settlement, and default party template.

[HIGH] E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/SubModule.xml:8 — Cross-module data contract — TAOM_Map settlements now reference cultures defined in TAOM Main, but TAOM_Map neither defines `dunland_raiders` etc. nor depends on TAOM; Main also does not depend on TAOM_Map. This makes load order/launcher profile correctness accidental. Fix by moving the new culture/clan data into TAOM_Map or adding explicit dependency metadata in the consuming module.

CRITICAL: 1 | HIGH: 3 | MEDIUM: 0 | LOW: 0  
VERDICT: ISSUES FOUND
