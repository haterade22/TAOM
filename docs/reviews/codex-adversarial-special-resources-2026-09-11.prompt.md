You are performing an adversarial code review of two commits in the TAOM Bannerlord mod repo at E:\repos\TAOM, branch bannerlord-1.4.5: 9e78afd2 (code, strings, docs) and e80949b7 (data). Target Bannerlord v1.4.8. Be skeptical. Your job is to find defects that a green test suite and a five-agent internal review did not. The working tree also holds OTHER sessions' uncommitted edits (ShaderPrecompilation, TaomSettings, a feat(shaders) CHANGELOG entry); those are out of scope, review the two commits with `git show` and the files as committed.

WHAT THE CHANGE IS

Special Resources is TAOM's per-kingdom secondary currency (War Spoils, Gems, Castar, Marks, Elven Wine, Lake Fish, War Drums, Tribal Relics, Dunlending Ale, Plunder, War Banners). Elite troops cost it to upgrade into, some cost it to recruit, and troops with a daily_upkeep row drain it every daily tick; at zero balance 10 percent of each upkeep troop type deserts per day.

Players reported two things: nobody could see what upkeep was costing them, and balances were "wiped after every battle". Both were transparency failures plus one real defect:
- The map-bar tooltip had passed an EMPTY troop list into the upkeep calculation since the feature's first commit, so it never rendered an upkeep line and Net was always just income. Its income also skipped the career SpecialResourceGain passive that the tick applied.
- No outflow produced a player-facing message. Daily upkeep, the party-screen upgrade commit, the recruit charge and the floor at zero only wrote log lines; earnings got a green toast.
- Desertion keyed on "the troop has ANY cost row". The Elite Emissary feature had added 50 merchant-only rows (merchant_cost, no daily_upkeep) for ordinary tree troops such as erebor_noble_royal_warden, so an Erebor player at 0 Gems lost Royal Wardens daily to "your Gems are depleted" though they cost nothing to keep.
- The 13 Black Numenorean rows carried daily_upkeep 1.0 to 3.0 against 0.05 to 0.3 for every other Mordor elite, charged in whatever resource the player holds; forty of them drained 60 a day against a top battle payout of 28.

The fix:
- ISpecialResourceService.GetDailyBreakdown returns Earning (career gain applied), Upkeep (career upkeep modifier applied PER LINE so the lines sum to the total), Net, and one TroopUpkeepLine per troop type whose cost row has DailyUpkeep > 0. ApplyDailyTick applies its Net; the tooltip, the daily toast, the map-bar warning flag and taom.print_special_resources render the same object. GetDailyEarning and the list-taking GetDailyUpkeep were REMOVED from the interface. GetProjectedDailyNet remains as breakdown.Net.
- CalculateDesertion skips troops whose row has no daily upkeep. The behavior's desertion gate and both warnings key on breakdown.UpkeepLines.Count > 0.
- PartyUpkeepReader (internal static) reads the party roster and the owned-town count off engine objects for the behavior, the mixin and the console command. It is a dumb collector: every troop with a cost row; the service decides which carry upkeep.
- SpecialResourceMessages (internal static, pure) builds four TextObjects with slots: DailyUpkeep, UpkeepOverdraft, UpgradeSpend, RecruitCharge. CommitSession and ChargeRecruitCost now return the float debited so the behavior can toast it. The behavior reads the balance before and after ApplyDailyTick and toasts an overdraft when before + breakdown.Net < 0.
- Tooltip: title with balance/cap, tier or next tier, "Daily change" rundown with income (town count), elite upkeep (troop-type count), one row per troop type with onlyShowWhenExtended: true, net, "Depleted in N days" via DailyResourceBreakdown.DaysUntilDepleted, a deserting notice at zero, then the per-event rates. Every label is a {=taom_res_tt_*} key rendered through TextObject.ToString() because TooltipProperty is string-only. OnRefreshCore now computes the breakdown every map-bar refresh and sets HasWarning = UpkeepLines.Count > 0 && amount + Net <= 0f.
- SpecialResourceStorageService.Set refuses a non-finite value (previous balance stands) and RestoreData repairs non-finite entries to 0 without dropping the key.
- 20 taom_res_* keys registered in Main/_Module/ModuleData/taom_module_strings.xml and seeded as ENGLISH rows in all 12 Languages/*/std_taom_module_strings_*.xml files; the translator run is owed.
- Data commit e80949b7: the 13 mordor_num_* rows in troop_resource_costs.xml now carry daily_upkeep 0.05 / 0.1 / 0.15 / 0.2 / 0.3 by rung and merchant_cost 6 / 8 / 12 / 18 / 28.

A five-agent internal review already ran and its findings are FIXED or recorded; do not re-report these, verify the fixes instead:
1. The one-day-ahead deficit warning fired for any positive balance with negative net, without requiring upkeep troops, while the map-bar flag and desertion both require them. Fixed: the branch now requires hasUpkeepTroops.
2. Math.Max(0f, NaN) is NaN, so the storage floor stored NaN and the save round trip kept it. Fixed as described above, four tests.
3. No test covered an upkeep modifier below minus 100 percent. Added.
4. Per-refresh breakdown allocation in OnRefreshCore at the map bar's refresh cadence. DECLINED with reasoning: the agent rated the cost class equal to vanilla's own per-refresh CalculateClanGoldChange and a roster-version cache adds a field plus a stale-icon edge. Dispute the reasoning if you can show the cost is real.
5. An early claim that prisoner recruitment never reaches OnUnitRecruitedEvent was WRONG: RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited dispatches OnUnitRecruited(troop, 1) per unit. Corrected in the issue and memory.

Decisions recorded in the CHANGELOG and RCA, not bugs (dispute them on the merits if you like, but do not report them as unnoticed):
- The daily line shows on EVERY day the party holds upkeep troops (the user chose this over "only when net is negative").
- HasWarning uses <= 0 where vanilla gold uses < 0; it deliberately matches the desertion trigger (balance <= 0).
- GetProjectedDailyNet is kept as a thin convenience over the breakdown.
- The hook interface IOnPartyUpgradeResourceCheck.CommitSession stays void; nothing calls it.
- SpendForUpgrade on the service is pre-existing dead code and was left alone.
- The pre-existing "running out" warning string and the Patch26 "Only enough resources for N upgrade(s)." toast remain raw English (pre-existing, out of scope).
- The two Black Numenorean rungs under the documented merchant band (6 and 8) are an extrapolation the author flagged.
- No translation yet; English rows only.

TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur".

READ FIRST

- docs/features/special-resources.md
- docs/reviews/rca-special-resources-outflow-2026-09-11.md
- CHANGELOG.md, the two 2026-09-11 entries tagged (#558)
- .claude/rules/csharp-architecture.md, the "Engine-Float Decision Gates" section
- docs/reviews/lessons/gamemodels-services.md, the last lesson ("A cost row is not an upkeep row")

FILES CHANGED

Commit 9e78afd2, C# production:
Main/Features/SpecialResources/ISpecialResourceService.cs
Main/Features/SpecialResources/SpecialResourceService.cs
Main/Features/SpecialResources/SpecialResourceStorageService.cs
Main/Features/SpecialResources/SpecialResourcesBehavior.cs
Main/Features/SpecialResources/PartyUpkeepReader.cs (new)
Main/Features/SpecialResources/SpecialResourceMessages.cs (new)
Main/Features/SpecialResources/Domain/DailyResourceBreakdown.cs (new)
Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs
Main/Features/SpecialResources/Cheats/SpecialResourceCheats.cs

Tests:
TAOM.Tests/Features/SpecialResources/SpecialResourceBreakdownTests.cs (new)
TAOM.Tests/Features/SpecialResources/SpecialResourceMessagesTests.cs (new)
TAOM.Tests/Features/SpecialResources/SpecialResourceDumpFormatTests.cs
TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs
TAOM.Tests/Features/SpecialResources/SpecialResourceStorageServiceTests.cs

Data and docs:
Main/_Module/ModuleData/taom_module_strings.xml plus 12 Languages/*/std_taom_module_strings_*.xml
docs/features/special-resources.md, docs/reviews/lessons/gamemodels-services.md, docs/reviews/rca-special-resources-outflow-2026-09-11.md, CHANGELOG.md

Commit e80949b7:
Main/_Module/ModuleData/special_resources/troop_resource_costs.xml

Related but NOT changed, and relevant to correctness:
Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs, PartyScreenLogic_UpgradeTroop_Patch.cs, PartyCharacterVM_InitializeUpgrades_Patch.cs (Patch26)
Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs, RecruitmentResourceGateHook.cs (Patch51)
Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs
Main/Features/SpecialResources/SpecialResourceConfigProvider.cs, SpecialResourcesIoC.cs
Main/Features/EliteEmissary/EliteEmissaryService.cs and Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml
Main/Features/CareerSystem, the ICareerPassiveService implementation of GetPassiveMagnitude
Main/_Module/ModuleData/special_resources/special_resources_config.xml

KNOWN SUSPECTS -- confirm or dispute each, with evidence from the code

SUSPECT 1: Overdraft toast equivalence. SpecialResourcesBehavior.OnDailyTickHero computes breakdown = GetDailyBreakdown(...), reads before = GetCurrentAmount(...), calls ApplyDailyTick(...), then toasts UpkeepOverdraft when before + breakdown.Net < 0f. ApplyDailyTick recomputes the breakdown and applies Net through AddCapped (net >= 0) or _storage.Add (net < 0), and Set floors at 0. Prove or disprove that the toast fires exactly when the floor absorbed something. Consider: a hero whose kingdom resolves to no resource (breakdown is Empty), a null hero StringId, the cap path (net > 0 and before + net > Cap, which clamps silently), and whether the two calls can ever see different inputs.

SUSPECT 2: Per-refresh cost. SpecialResourceMapBarMixin.OnRefreshCore now calls PartyUpkeepReader.Collect (a roster walk with one GetTroopCost dictionary lookup per element) and GetDailyBreakdown (which calls ResolveResource once and GetPassiveMagnitude twice) on every MapInfoVM refresh. Read the ICareerPassiveService implementation of GetPassiveMagnitude and state what it costs per call (dictionary lookup, LINQ, allocation, lock, config parse). State the refresh cadence from the installed MapInfoVM / GauntletMapBarGlobalLayer code. Then rule on the declined cache: right call, or a real cost the author waved away?

SUSPECT 3: Are the per-troop rows reachable? The tooltip adds them with onlyShowWhenExtended: true. Decompile, from the INSTALLED v1.4.8 DLLs, the tooltip pipeline that renders a MapInfoItemVM hint (the Func<List<TooltipProperty>> path through TaleWorlds.Core.ViewModelCollection.Information: PropertyBasedTooltipVM or its equivalent, and whatever reads TooltipProperty.OnlyShowWhenExtended and the "extended" input). Confirm whether the map-bar hint supports the extended mode at all and which key toggles it. If the rows can never show, that is a HIGH finding: the whole point of the change was to show them.

SUSPECT 4: TextObject rendering. SpecialResourceMessages passes pre-formatted numeric strings ("2.4", "0.05") through SetTextVariable(string, string) and the recruit count through SetTextVariable(string, int); the tooltip passes CharacterObject.Name.ToString() (already rendered) as {TROOP}. Decompile TextObject.SetTextVariable overloads and MBTextManager's variable substitution in the installed DLL and confirm: a string variable is inserted verbatim (no re-parse of braces or of the {=id} form), an int renders without grouping separators, and a pre-rendered troop name cannot be double-processed. Also state what CultureInfo Bannerlord runs under, because FormatAmount uses InvariantCulture while the tooltip title uses {amount:F0} in the current culture; is the mix visible to a DE or FR player?

SUSPECT 5: Desertion application. PartyUpkeepReader.Collect reads element.Number, which counts wounded troops. ApplyDesertion removes toRemove = min(entry.DesertCount, currentCount) with MemberRoster.AddToCounts(character, -toRemove). Decompile TroopRoster.AddToCounts and its wounded handling in the installed DLL: if a troop type has more wounded than healthy, does removing N with woundedCount 0 throw, clamp, or leave a negative healthy count? This code predates the change but the change moved the gate that reaches it, so own it.

SUSPECT 6: Storage repair on load. SpecialResourcesBehavior.SyncData calls _storage.GetAllData(), passes the same dictionary by ref to dataStore.SyncData, then RestoreData(data). RestoreData now collects non-finite keys and overwrites them. Confirm no enumeration-modification hazard on either the save path (same dictionary instance) or the load path, and confirm a save written by an older build with a NaN balance loads to 0 and stays tracked (Contains true) so OnGameLoaded's legacy seed does not re-fire.

SUSPECT 7: The three gates. HasWarning: UpkeepLines.Count > 0 && amount + Net <= 0. Day-ahead toast: balance > 0 && hasUpkeepTroops && balance + Net <= 0. Desertion: balance <= 0 && hasUpkeepTroops && not the first tick after load. Enumerate the states (balance 0 or positive; upkeep troops present, merchant-only only, none; grace tick or not; income covering upkeep or not) and find any state where the icon is red and nothing will happen, or troops desert with no prior red icon and no prior toast. A troop recruited mid-day and a town lost mid-day are the interesting transitions.

SUSPECT 8: Black Numenorean data. Parse Main/_Module/ModuleData/special_resources/troop_resource_costs.xml. Confirm 77 rows, the 13 mordor_num_* values, and that every row's attributes are consumed somewhere (upgrade_cost by Patch26 via PartyUpgradeResourceCheckHook, recruit_cost by Patch51 and OnUnitRecruited, daily_upkeep by the breakdown, merchant_cost by EliteEmissaryService). Then look for OTHER rows that are out of family the same way the Black Numenoreans were (harad_mumakil_rider daily_upkeep 500, harad_elephant_rider 10, taom_spider_creature 1, the ironpass rams 0.1 to 0.25) and say whether each is a deliberate creature premium or the next "wiped after every battle" report. Check elite_emissary_config.xml still lists all 13 mordor_num_* troops and that nothing (test, validator, EliteEmissary code) enforces a minimum merchant_cost that 6 and 8 violate.

SUSPECT 9: Test sensitivity. For every NEW test in SpecialResourceBreakdownTests, SpecialResourceMessagesTests, SpecialResourceStorageServiceTests (the four non-finite tests) and SpecialResourceDumpFormatTests (the two breakdown tests), state what the test would have done against the PRE-change code (git show 2652fe2f:<path> for the service and storage). A test that passes either way is coverage in name only; list them.

SUSPECT 10: Mixin lifetime. SpecialResourceMapBarMixin keeps _lastAmount, _lastWarning, _lastResource, _lastKingdomId, _lastCultureId and _itemAdded per instance. Confirm from the installed MapInfoVM / UIExtenderEx behaviour whether a new MapInfoVM (and so a new mixin) is constructed per campaign, and whether returning to the main menu and loading another save in the same process can leave a stale _itemAdded or _lastResource that skips the SecondaryInfoItems add or shows the previous campaign's resource for a frame.

REQUIRED SECTIONS

1. VANILLA CODE. Decompile and paste as code blocks, from the INSTALLED v1.4.8 DLLs (use `pwsh tools/taom-src.ps1 path <Full.Type.Name>` from the repo root, or ilspycmd against E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/*.dll), never from the E:\Decompiled_Bannerlord dump:
   - MapInfoVM.UpdatePlayerInfo (the gold HasWarning line) and whatever calls MapInfoVM.Refresh, with the cadence.
   - TooltipProperty constructors and the VM or widget that honours OnlyShowWhenExtended (SUSPECT 3).
   - TextObject.SetTextVariable(string, string) and (string, int), and the substitution path (SUSPECT 4).
   - TroopRoster.AddToCounts (SUSPECT 5).
   - RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited and the RecruitmentCampaignBehavior volunteer loop that dispatches OnUnitRecruited, to confirm per-unit dispatch and therefore one recruit-charge toast per unit.

2. DATA ANALYSIS of troop_resource_costs.xml and special_resources_config.xml as described in SUSPECT 8, plus: every troop id in troop_resource_costs.xml must resolve to an NPCCharacter in Main/_Module/ModuleData/troops/*.xml or in the live E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData tree; list any that do not.

3. CONFIG CROSS-REFERENCE. Every {=taom_res_*} key used in the C# files must have a row in taom_module_strings.xml and in all 12 language files; every {SLOT} in a default text must have a matching SetTextVariable and no SetTextVariable may name a slot absent from the text. The 20 seeded language rows must be well-formed XML and must not duplicate an existing id. Confirm the taom_res_tt_troop_line template "{TROOP} x{COUNT}" survives MBTextManager (the literal x followed by a brace).

4. LOCALIZATION. The inline C# default for each key must equal the registered English row byte for byte (the harvester copied them; confirm). Note which keys carry no digits in the default text: the test AssertNoBakedNumber enforces that, so the overdraft template was reworded to "none is left". Judge whether the four toast wordings read correctly for a player who does not know the feature.

5. FINDINGS OR OBSERVATIONS. If you find nothing, say so plainly rather than padding. Rank by severity. For each finding give file, line, the concrete failure path, and the minimal fix. Quote code only from lines you actually read; a prior review quoted a comment line as code.

QUALITY GATES

- Verify every "missing" claim by grepping before asserting it.
- A passing test suite is not evidence of correctness. 252 tests in the affected suites and 8449 of 8450 in the full suite pass on this changeset already; the one failure (ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded) predates it and touches no file here.
- Do not report style preferences.
- State file and line for every finding, and the commit or working-tree version you read.

PRIOR REVIEW LESSONS

SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. The test-sensitivity table caught tests that passed either way. Looking at code NOT in the diff caught a discharge bug.
FAILURES: Codex has assumed empire=Rohan (it is Dunland). Codex has flagged vanilla-matching code as bugs. Codex has skipped hard sections. Codex has quoted a comment line as if it were code (SettlementEncounter review, 2026-08-24). Codex has reported decisions the changeset recorded (see the list above) as if they were unnoticed defects; disputing a decision on its merits is welcome, reporting it as a bug is not.

Output to: docs/reviews/raw/codex-adversarial-special-resources-2026-09-11.md
