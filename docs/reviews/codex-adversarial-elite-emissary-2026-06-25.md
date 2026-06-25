OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019eff62-9f19-7512-a009-9d5003f70b0f
--------
user
You are doing an adversarial code review of a NEW Bannerlord 1.4.6 total-conversion mod feature for TAOM (Tales From the Age of Men). The feature is "Elite Emissary": at a faction's key settlement (its capital) the player opens a town/castle/village menu option "Speak with the faction emissary", has a short conversation with a settlement notable, and buys that faction's elite troops for that faction's special resource (Castar, War Spoils, Gems, etc.). It reuses the existing SpecialResources economy and adds a new merchant_cost price field separate from recruit_cost.

This feature ALREADY passed a thorough in-house adversarial review (7 dimension reviewers + 20 confirm/refute verifiers). Your job is NOT to re-find what we already found -- it is to find what we MISSED, and to adversarially check that our fixes and our "conscious call" decisions actually hold. Be skeptical of our own conclusions. Read the actual source before asserting anything; cite file:line. A finding is a hypothesis -- verify it against the code, do not assert from plausibility.

== TAOM ID CHEATSHEET ==
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings/Rhun, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, goblin, mistymountainorcs
Culture IDs (XSLT/vanilla engine ids): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings/Rhun, sturgia=Dale, battania=Khand
NOTE: "rohan" is NOT a valid id -- Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". "harad"/"rhun"/"dale"/"khand" are NOT valid culture ids -- use aserai/khuzait/sturgia/battania.

== READ FIRST ==
- docs/features/elite-emissary.md -- the feature doc, INCLUDING the "Design Decisions & Known Edge Cases" section which records the conscious calls we made. Challenge those decisions if they are wrong.
- Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml -- key settlements + per-culture offer lists.
- Main/_Module/ModuleData/special_resources/troop_resource_costs.xml -- merchant_cost rows + the pre-existing recruit_cost/upgrade_cost rows.
- Main/_Module/ModuleData/special_resources/special_resources_config.xml -- which kingdoms/cultures map to which resource (this is what ResolveResource reads).

== WHAT THE IN-HOUSE REVIEW ALREADY FOUND (do not re-report; instead verify our fix/decision) ==
1. FIXED: a greeting-flag leak. EliteEmissaryBehavior._pendingEmissaryHeroId gated the emissary greeting (custom "start"-token dialog line) but was cleared only in GreetConsequence. We added a clear on CampaignEvents.ConversationEnded. VERIFY this fix is actually robust -- does ConversationEnded fire for a CampaignMapConversation.OpenConversation conversation in v1.4.6? Is there ANY remaining path where the flag leaks and the emissary greeting hijacks a normal notable chat?
2. DOCUMENTED (conscious call): offers are keyed by owner CULTURE but the charged resource is resolved kingdom-first via ResolveResource(kingdomId, cultureId). We argued this is consistent with the earning side and non-triggering in shipping config. VERIFY: is there any shipping or near-shipping state (defection, rebellion, minor faction, mercenary, conquest) where a key settlement's owner culture and kingdom map to DIFFERENT resources, making the player pay the wrong currency for culture-priced troops? Is it actually unreachable, or did we hand-wave it?
3. DOCUMENTED (conscious call): no war/relation gate -- we argued the menu requires entering the settlement and hostile settlements are not enterable. VERIFY: can the player reach the "town"/"castle"/"village" game menu (and thus the emissary option) for a settlement whose owner faction they are AT WAR with, or whose resource they should not be able to spend? Consider: a settlement the player owns that was just conquered from another culture; a neutral/at-peace-but-not-allied faction; an army passing through.
4. DOCUMENTED (conscious call): purchases are NOT party-size capped -- GrantTroop adds troops regardless of PartySizeLimit. VERIFY this can't corrupt anything.
5. REJECTED (we said not-a-bug): selling taom_spider_creature / harad_elephant_rider via the emissary -- we argued both are already player-recruitable as volunteers so no new command path. VERIFY: is granting a creature/mount troop directly to the MAIN PARTY roster via MemberRoster.AddToCounts genuinely identical to the volunteer-recruit path, or is there a difference (e.g. recruit goes through a different action that sets up the mount, vs a raw roster add)?

== KNOWN SUSPECTS (CONFIRM or DISPUTE each, with file:line evidence) ==
S1. Transaction atomicity. EliteEmissaryService.Purchase orders afford-check -> grant (IPlayerPartyAdapter.GrantTroop) -> charge (ChargeMerchantPurchase). Claim: a failed grant never charges, and there is no path that charges without granting or grants without charging. DISPUTE if you can find one (e.g. GrantTroop returns true but partially adds; an exception between grant and charge; the inquiry callback firing twice).
S2. Inquiry round-trip trust. The 2-step ShowMultiSelectionInquiry passes an EmissaryTroopOffer / int quantity as the Identifier. Purchase re-validates the troop is in the owner culture's offer list (IsOfferedBy) and re-derives cost from config. Claim: the player cannot manipulate quantity/troop to underpay or buy an un-offered troop. DISPUTE.
S3. Resource/charge consistency. BuildOfferList (display balance + afford), CanAffordMerchantPurchase (gate), and ChargeMerchantPurchase (deduct) must all resolve the SAME resource for the SAME (heroId, ownerKingdomId, ownerCultureId). Claim: they do. DISPUTE -- check that the behavior/presenter passes the SAME owner kingdom/culture to all three (owner is re-resolved per call from Settlement.CurrentSettlement; could CurrentSettlement change between the menu open and the inquiry callback, e.g. the conversation moving the player?).
S4. Config validation completeness. EliteEmissaryConfigProvider validates: unknown culture id (against a hardcoded KnownCultureIds set), troop without merchant_cost, duplicate culture, empty-after-validation. Key-settlement ids are validated at runtime in EliteEmissaryBehavior.ValidateKeySettlements. Claim: a malformed/typo config degrades safely (drop+warn) and never crashes or silently mis-sells. DISPUTE -- is there a config value the consumer branches on that is NOT validated (the M1 parsed-but-unresolvable trap)?
S5. merchant_cost / recruit_cost separation. ChargeMerchantPurchase reads cost.MerchantCost; ChargeRecruitCost reads cost.RecruitCost. Claim: a troop carrying BOTH (harad_elephant_rider: recruit_cost=50, merchant_cost=70; taom_spider_creature: recruit_cost=5, merchant_cost=18) is never charged the wrong field, and the config provider's troopId-keyed dict does not lose the upgrade_cost/recruit_cost when a row also has merchant_cost. DISPUTE -- check troop_resource_costs.xml for duplicate <Troop id> rows that would overwrite in the dict.
S6. Offer-vs-price-vs-real-troop integrity. Every <Troop id> in elite_emissary_config.xml must (a) have a merchant_cost row in troop_resource_costs.xml and (b) resolve to a real CharacterObject defined in troops/troops_*.xml (or characters/*.xml for the spider). Every key settlement id must exist in the live TAOM_Map settlements. Every <Culture id> with offers must map to a special resource in special_resources_config.xml (else the offer is dead -- goblin/mistymountainorcs map to NO resource; confirm they are correctly omitted). DISPUTE any broken ref.

== FILES ==
Service + domain (pure logic, should hold all decisions):
  Main/Features/EliteEmissary/IEliteEmissaryService.cs
  Main/Features/EliteEmissary/EliteEmissaryService.cs
  Main/Features/EliteEmissary/Domain/EmissaryTroopOffer.cs
  Main/Features/EliteEmissary/Domain/EmissaryOfferList.cs
  Main/Features/EliteEmissary/Domain/EmissaryPurchaseResult.cs
  Main/Features/EliteEmissary/Domain/EliteEmissaryConfig.cs
Config + settings providers (must validate):
  Main/Features/EliteEmissary/IEliteEmissaryConfigProvider.cs
  Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs
  Main/Features/EliteEmissary/IEliteEmissarySettingsProvider.cs
  Main/Features/EliteEmissary/EliteEmissarySettingsProvider.cs
Boundary (engine-coupled -- behavior + presenter + adapters):
  Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs
  Main/Features/EliteEmissary/Hooks/EliteEmissaryInquiryPresenter.cs
  Main/Adapters/ISettlementOwnerAdapter.cs
  Main/Adapters/SettlementOwnerAdapter.cs
  Main/Adapters/IPlayerPartyAdapter.cs
  Main/Adapters/PlayerPartyAdapter.cs
SpecialResources extension:
  Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs
  Main/Features/SpecialResources/SpecialResourceConfigProvider.cs
  Main/Features/SpecialResources/ISpecialResourceService.cs
  Main/Features/SpecialResources/SpecialResourceService.cs
Registration + MCM:
  Main/Features/EliteEmissary/EliteEmissaryIoC.cs
  Main/IoC.cs
  Main/SubModule.cs
  Main/Features/TaomSettings.cs
Data:
  Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml
  Main/_Module/ModuleData/special_resources/troop_resource_costs.xml
  Main/_Module/ModuleData/taom_emissary_strings.xml
Tests:
  TAOM.Tests/Features/EliteEmissary/EliteEmissaryServiceTests.cs
  TAOM.Tests/Features/EliteEmissary/EliteEmissaryConfigProviderTests.cs
  TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs

== ENGINE TOUCHPOINTS (verify signatures/semantics against the installed v1.4.6 DLLs) ==
No Harmony patches, no GameModel overrides. Engine calls to verify:
- CampaignMapConversation.OpenConversation(ConversationCharacterData, ConversationCharacterData) -- does opening this from inside a settlement game-menu cleanly transition, and does CampaignEvents.ConversationEnded fire for it?
- CampaignGameStarter.AddGameMenuOption on "town"/"castle"/"village" + MenuHelper.SetOptionProperties(args, condition, isDisabled, tooltip) semantics for the disabled-but-shown branch.
- MBInformationManager.ShowMultiSelectionInquiry / MultiSelectionInquiryData / InquiryElement -- can a disabled element be selected? Can the affirmative callback fire with a disabled identifier?
- Settlement.OwnerClan (computed getter -- village->bound-town hop), Settlement.Culture (field), Hero.OneToOneConversationHero, MobileParty.MainParty.MemberRoster.AddToCounts(CharacterObject, int).

== REQUIRED OUTPUT SECTIONS ==
1. KNOWN SUSPECTS -- one CONFIRMED/DISPUTED verdict per S1..S6 with file:line evidence.
2. VERIFY OUR FIXES/DECISIONS -- one verdict per item 1..5 in "WHAT THE IN-HOUSE REVIEW ALREADY FOUND".
3. CONFIG CROSS-REFERENCE -- the offer-vs-price-vs-real-troop + culture-maps-to-resource + key-settlement-exists checks, with any broken ref.
4. NEW FINDINGS -- anything the in-house review missed, severity HIGH/MED/LOW, file:line, concrete fix. Include things like: NRE risk on a computed getter, save/load, a config branch not validated, a vanilla interaction (does the emissary menu option survive other mods adding to the same menu?), localization keys used in code but missing from taom_emissary_strings.xml, an off-by-one or rounding in the quantity picker, the FindEmissaryNotable selection picking a notable that breaks the conversation, the village menu path being dead config.
5. FINDINGS OR OBSERVATIONS -- if you find nothing in a section, say so explicitly. Do NOT invent issues to fill space.

== QUALITY GATES ==
- Read every file you cite. Quote the exact line. No findings from plausibility alone.
- Decompile/verify any v1.4.6 API claim against the installed DLLs (do not assume older-Bannerlord behavior).
- Cross-reference every config id against the cheatsheet + the source-of-truth files before calling it a mismatch.
- If you cannot confirm a suspected bug is reachable, say "unconfirmed -- reachability unclear" rather than asserting it.

== PRIOR REVIEW LESSONS ==
SUCCESSES: config-id cross-ref catches rohan/dol_guldur mismatches; vanilla decompilation catches missing gates; lifecycle tracing catches stale state/flags.
FAILURES TO AVOID: do NOT assume empire=Rohan (empire is Dunland; Rohan is vlandia). Do NOT flag vanilla-matching code as a bug. Do NOT skip the hard sections (the resource-resolution and conversation-state questions are the hard ones -- answer them). Do NOT re-report the 5 already-found items as new -- verify them instead.

Write your review to stdout as markdown.

ERROR: The process "20904" not found.
codex
I’ll review this as read-only and keep the output in the requested reviewer format. I’m starting by mapping the Elite Emissary code, docs, config, and relevant vanilla decompiled touchpoints so any claim is tied to source lines.
  → Read feature docs, source, config, and tests
  • Verify engine touchpoints in decompiled v1.4.6 sources
  • Cross-reference XML ids for offers, costs, resources, settlements, troops, localization
  • Evaluate S1-S6 and prior review decisions
  • Write final markdown review with calibrated findings
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "EliteEmissary|emissary|merchant_cost|MerchantCost|ChargeMerchant|ResolveResource|ConversationEnded|OpenConversation|ShowMultiSelectionInquiry|AddGameMenuOption|SetOptionProperties" Main TAOM.Tests docs/features -S' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path . -Force | Select-Object Name,Mode,Length' in C:\Users\mikew\source\repos\TAOM
 succeeded in 671ms:
 M CHANGELOG.md
 M Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs
 M Main/Features/SpecialResources/ISpecialResourceService.cs
 M Main/Features/SpecialResources/SpecialResourceConfigProvider.cs
 M Main/Features/SpecialResources/SpecialResourceService.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/Languages/BR/language_data.xml
 M Main/_Module/ModuleData/Languages/CNs/language_data.xml
 M Main/_Module/ModuleData/Languages/CNt/language_data.xml
 M Main/_Module/ModuleData/Languages/DE/language_data.xml
 M Main/_Module/ModuleData/Languages/FR/language_data.xml
 M Main/_Module/ModuleData/Languages/IT/language_data.xml
 M Main/_Module/ModuleData/Languages/JP/language_data.xml
 M Main/_Module/ModuleData/Languages/KO/language_data.xml
 M Main/_Module/ModuleData/Languages/PL/language_data.xml
 M Main/_Module/ModuleData/Languages/RU/language_data.xml
 M Main/_Module/ModuleData/Languages/SP/language_data.xml
 M Main/_Module/ModuleData/Languages/TR/language_data.xml
 M Main/_Module/ModuleData/special_resources/troop_resource_costs.xml
 M Main/_Module/SubModule.xml
 M TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs
 M TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs
 M docs/reviews/LESSONS-LEARNED.md
?? Main/Adapters/IPlayerPartyAdapter.cs
?? Main/Adapters/ISettlementOwnerAdapter.cs
?? Main/Adapters/PlayerPartyAdapter.cs
?? Main/Adapters/SettlementOwnerAdapter.cs
?? Main/Features/EliteEmissary/
?? Main/_Module/ModuleData/Languages/BR/std_taom_emissary_strings_por-BR.xml
?? Main/_Module/ModuleData/Languages/CNs/std_taom_emissary_strings_zho-CN.xml
?? Main/_Module/ModuleData/Languages/CNt/std_taom_emissary_strings_zho-HK.xml
?? Main/_Module/ModuleData/Languages/DE/std_taom_emissary_strings_deu-DE.xml
?? Main/_Module/ModuleData/Languages/FR/std_taom_emissary_strings_fre-FR.xml
?? Main/_Module/ModuleData/Languages/IT/std_taom_emissary_strings_ita-IT.xml
?? Main/_Module/ModuleData/Languages/JP/std_taom_emissary_strings_jpn-JP.xml
?? Main/_Module/ModuleData/Languages/KO/std_taom_emissary_strings_kor-KO.xml
?? Main/_Module/ModuleData/Languages/PL/std_taom_emissary_strings_pol-PL.xml
?? Main/_Module/ModuleData/Languages/RU/std_taom_emissary_strings_rus-RU.xml
?? Main/_Module/ModuleData/Languages/SP/std_taom_emissary_strings_spa-LA.xml
?? Main/_Module/ModuleData/Languages/TR/std_taom_emissary_strings_tur-TR.xml
?? Main/_Module/ModuleData/elite_emissary/
?? Main/_Module/ModuleData/taom_emissary_strings.xml
?? TAOM.Tests/Features/EliteEmissary/
?? docs/features/elite-emissary.md
?? docs/reviews/codex-adversarial-elite-emissary-2026-06-25.md
?? docs/reviews/codex-adversarial-elite-emissary-2026-06-25.prompt.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 772ms:

[32;1mName                    [0m[32;1m Mode [0m[32;1m Length[0m
[32;1m----                    [0m [32;1m---- [0m [32;1m------[0m
.claude                  d---- 
.codex                   d---- 
.codex_tmp               d---- 
.codex_tmp2              d---- 
.git                     d--h- 
.github                  d---- 
.pytest_cache            d---- 
.serena                  d---- 
.taom-src                d---- 
.vs                      d--h- 
.vscode                  d---- 
BehaviorTreeWrapper      d---- 
Dependencies             d---- 
docs                     d---- 
Main                     d---- 
out                      d---- 
plans                    d---- 
scripts                  d---- 
Stubs                    d---- 
TAOM.Tests               d---- 
TestResults              d---- 
tools                    d---- 
.editorconfig            -a--- 342
.gitignore               -a--- 5326
.mcp.json                -a--- 1388
AGENTS.md                -a--- 115665
build.ps1                -a--- 1438
CHANGELOG.md             -a--- 1297787
CLAUDE.md                -a--- 138769
Directory.Build.props    -a--- 1394
LICENSE                  -a--- 1074
LICENSE-CONTENT.md       -a--- 1627
mordor-lords.html        -a--- 13276
README.md                -a--- 9322
report.json              -a--- 2
setup-dev-env.ps1        -a--- 1484
SPOrderOfBattleVM.tmp.cs -a--- 1145
TAOM.sln                 -a--- 2735


2026-06-25T15:25:33.166519Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.3 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "EliteEmissary|emissary|merchant_cost|MerchantCost|ChargeMercha …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2325ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "EliteEmissary|emissary|merchant_cost|MerchantCost|ChargeMercha …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
The working tree is dirty and the Elite Emissary feature is uncommitted, so I’m treating all inspected changes as the review target and not modifying anything. `rg` is not installed in this shell, so I’m using PowerShell search/read commands instead.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\EliteEmissary -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData\\elite_emissary -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests\\Features\\EliteEmissary -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 3994ms:

[32;1mFullName                                                                                           [0m[32;1m Length[0m
[32;1m--------                                                                                           [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Domain                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Hooks                                  
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissaryConfigProvider.cs         6769
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissaryIoC.cs                    701
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissaryService.cs                7012
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissarySettingsProvider.cs       842
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\IEliteEmissaryConfigProvider.cs        475
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\IEliteEmissaryService.cs               1921
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\IEliteEmissarySettingsProvider.cs      646
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Domain\EliteEmissaryConfig.cs          1409
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Domain\EmissaryOfferList.cs            2017
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Domain\EmissaryPurchaseResult.cs       1732
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Domain\EmissaryTroopOffer.cs           1068
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Hooks\EliteEmissaryBehavior.cs         9018
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\Hooks\EliteEmissaryInquiryPresenter.cs 10546


 succeeded in 4018ms:

[32;1mFullName                                                                                              [0m[32;1m Length[0m
[32;1m--------                                                                                              [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EliteEmissary\EliteEmissaryConfigProviderTests.cs   5583
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EliteEmissary\EliteEmissaryServiceTests.cs         11262


 succeeded in 4070ms:

[32;1mFullName                                                                                         [0m[32;1m Length[0m
[32;1m--------                                                                                         [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml   4666


codex
I’ve found the feature surface: a pure service/config layer, engine-bound behavior/presenter/adapters, XML data, and focused tests. Next I’m reading those with line numbers, then I’ll cross-check the engine behavior instead of relying on assumptions.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path docs'"\\features\\elite-emissary.md | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\EliteEmissaryService.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Hooks\\EliteEmissaryBehavior.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 618ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using TAOM.Adapters;
    4: using TAOM.Core.Logging;
    5: using TAOM.Features.EliteEmissary.Domain;
    6: using TAOM.Features.SpecialResources;
    7: 
    8: namespace TAOM.Features.EliteEmissary;
    9: 
   10: /// <summary>
   11: /// Pure decision + transaction logic for the Settlement Elite Emissary (ADR-002/007 — no sealed
   12: /// TaleWorlds types). Reuses the SpecialResources economy for resolution/balance/charge; the
   13: /// emissary's own price lives in <c>merchant_cost</c>, kept separate from the volunteer
   14: /// <c>recruit_cost</c> so the two economies never collide. See docs/features/elite-emissary.md.
   15: /// </summary>
   16: public sealed class EliteEmissaryService : IEliteEmissaryService
   17: {
   18:     private readonly IEliteEmissaryConfigProvider _config;
   19:     private readonly IEliteEmissarySettingsProvider _settings;
   20:     private readonly ISpecialResourceService _resourceService;
   21:     private readonly ISpecialResourceConfigProvider _resourceConfig;
   22:     private readonly IPlayerPartyAdapter _party;
   23:     private readonly IModLogger _logger;
   24: 
   25:     public EliteEmissaryService(
   26:         IEliteEmissaryConfigProvider config,
   27:         IEliteEmissarySettingsProvider settings,
   28:         ISpecialResourceService resourceService,
   29:         ISpecialResourceConfigProvider resourceConfig,
   30:         IPlayerPartyAdapter party,
   31:         IModLogger logger)
   32:     {
   33:         _config = config;
   34:         _settings = settings;
   35:         _resourceService = resourceService;
   36:         _resourceConfig = resourceConfig;
   37:         _party = party;
   38:         _logger = logger;
   39:     }
   40: 
   41:     public bool IsEnabled => _settings.IsEnabled;
   42: 
   43:     public bool IsKeySettlement(string settlementId)
   44:     {
   45:         if (string.IsNullOrEmpty(settlementId)) return false;
   46:         return _config.GetConfig().KeySettlementIds.Contains(settlementId);
   47:     }
   48: 
   49:     public bool HasPurchasableOffers(string ownerKingdomId, string ownerCultureId)
   50:     {
   51:         if (_resourceService.ResolveResource(ownerKingdomId, ownerCultureId) == null)
   52:             return false;
   53: 
   54:         foreach (var troopId in GetCultureOffers(ownerCultureId))
   55:         {
   56:             var cost = _resourceConfig.GetTroopCost(troopId);
   57:             if (cost != null && cost.MerchantCost > 0)
   58:                 return true;
   59:         }
   60:         return false;
   61:     }
   62: 
   63:     public EmissaryOfferList BuildOfferList(string heroId, string ownerKingdomId, string ownerCultureId)
   64:     {
   65:         var resource = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
   66:         if (resource == null)
   67:         {
   68:             _logger.LogDebug($"[EliteEmissary] BuildOfferList: no resource for kingdom='{ownerKingdomId}' culture='{ownerCultureId}'");
   69:             return EmissaryOfferList.NoResourceAvailable;
   70:         }
   71: 
   72:         var balance = _resourceService.GetCurrentAmount(heroId, ownerKingdomId, ownerCultureId);
   73:         var offers = new List<EmissaryTroopOffer>();
   74: 
   75:         foreach (var troopId in GetCultureOffers(ownerCultureId))
   76:         {
   77:             var cost = _resourceConfig.GetTroopCost(troopId);
   78:             var merchantCost = cost?.MerchantCost ?? 0;
   79:             if (merchantCost <= 0)
   80:             {
   81:                 _logger.LogWarning($"[EliteEmissary] Offer '{troopId}' (culture {ownerCultureId}) has no merchant_cost — skipped");
   82:                 continue;
   83:             }
   84: 
   85:             var maxAffordable = (int)(balance / merchantCost);
   86:             offers.Add(new EmissaryTroopOffer(troopId, merchantCost, canAfford: balance >= merchantCost, maxAffordableQuantity: maxAffordable));
   87:         }
   88: 
   89:         _logger.LogInfo($"[EliteEmissary] Offer list for culture '{ownerCultureId}': {offers.Count} troop(s), resource={resource.DisplayName}, balance={balance:F0}");
   90:         return EmissaryOfferList.ForResource(resource.Id, resource.DisplayName, resource.IconSpriteName, balance, offers);
   91:     }
   92: 
   93:     public EmissaryPurchaseResult Purchase(string heroId, string ownerKingdomId, string ownerCultureId, string troopId, int quantity)
   94:     {
   95:         if (string.IsNullOrEmpty(troopId) || quantity <= 0)
   96:         {
   97:             _logger.LogWarning($"[EliteEmissary] Purchase rejected (Invalid): troop='{troopId}' qty={quantity}");
   98:             return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Invalid, troopId, quantity);
   99:         }
  100: 
  101:         var resource = _resourceService.ResolveResource(ownerKingdomId, ownerCultureId);
  102:         if (resource == null)
  103:         {
  104:             _logger.LogWarning($"[EliteEmissary] Purchase rejected (NoResource): kingdom='{ownerKingdomId}' culture='{ownerCultureId}'");
  105:             return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NoResource, troopId, quantity);
  106:         }
  107: 
  108:         var cost = _resourceConfig.GetTroopCost(troopId);
  109:         var merchantCost = cost?.MerchantCost ?? 0;
  110:         if (merchantCost <= 0 || !IsOfferedBy(ownerCultureId, troopId))
  111:         {
  112:             _logger.LogWarning($"[EliteEmissary] Purchase rejected (NotOffered): '{troopId}' not a merchant offer for culture '{ownerCultureId}'");
  113:             return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.NotOffered, troopId, quantity);
  114:         }
  115: 
  116:         var totalCost = merchantCost * quantity;
  117: 
  118:         if (!_resourceService.CanAffordMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity))
  119:         {
  120:             _logger.LogInfo($"[EliteEmissary] Purchase blocked (Unaffordable): {troopId} x{quantity} costs {totalCost} {resource.DisplayName}");
  121:             return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Unaffordable, troopId, quantity, totalCost, resource.DisplayName);
  122:         }
  123: 
  124:         // Grant BEFORE charge: a failed grant (no party / unknown troop id) then never charges.
  125:         if (!_party.GrantTroop(troopId, quantity))
  126:         {
  127:             _logger.LogError($"[EliteEmissary] Purchase failed (grant): could not add {troopId} x{quantity} to party — no charge applied");
  128:             return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Failed, troopId, quantity, totalCost, resource.DisplayName);
  129:         }
  130: 
  131:         _resourceService.ChargeMerchantPurchase(heroId, ownerKingdomId, ownerCultureId, troopId, quantity);
  132:         _logger.LogInfo($"[EliteEmissary] PURCHASE: {troopId} x{quantity} for {totalCost} {resource.DisplayName} (settlement faction kingdom='{ownerKingdomId}' culture='{ownerCultureId}')");
  133:         return EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, troopId, quantity, totalCost, resource.DisplayName);
  134:     }
  135: 
  136:     private IReadOnlyList<string> GetCultureOffers(string cultureId)
  137:     {
  138:         if (cultureId != null && _config.GetConfig().CultureOffers.TryGetValue(cultureId, out var list) && list != null)
  139:             return list;
  140:         return Array.Empty<string>();
  141:     }
  142: 
  143:     private bool IsOfferedBy(string cultureId, string troopId)
  144:     {
  145:         foreach (var id in GetCultureOffers(cultureId))
  146:             if (id == troopId)
  147:                 return true;
  148:         return false;
  149:     }
  150: }

 succeeded in 621ms:
    1: # Elite Emissary (Settlement Special-Resource Troop Merchant)
    2: 
    3: ## Overview
    4: 
    5: At a faction's key settlement (its capital), the player can "Speak with the faction emissary" from the
    6: town/castle/village menu, have a short conversation, and buy that faction's **elite troops** for that
    7: faction's **special resource** (Castar, War Spoils, Gems, Elven Wine, Marks, War Banners, War Drums).
    8: A two-step popup picks the troop then the quantity; the resource is deducted and the troops join the
    9: player's party.
   10: 
   11: Inspired by ROT's (`E:\ROT6.2`) `ROTTownTradersBehavior`, which sells troops at specific settlements for
   12: **gold**. TAOM rebuilds the same experience on the existing [SpecialResources](special-resources.md)
   13: economy instead of gold.
   14: 
   15: ## Why This Exists
   16: 
   17: TAOM has a rich special-resource economy (11 resources earned by battle/raid/siege/etc.) but, before
   18: this feature, resources were only spent on troop **upgrades** (Patch26) and a couple of volunteer
   19: **recruits** (elephant/spider). There was no way to directly buy a faction's elite units. The Elite
   20: Emissary gives the player a resource sink and a reason to hold a capital: it turns "war currency" into
   21: "the faction's best troops."
   22: 
   23: Self-gating falls out of the design: the resource charged is the **settlement owner faction's** resource,
   24: and the player only earns their own faction's resource — so in practice you buy your own faction's elites
   25: at your own capitals. Conquering an enemy capital flips its offerings to the new owner.
   26: 
   27: ## Architecture
   28: 
   29: ### Design Challenge
   30: 
   31: - Reuse the SpecialResources economy (resolution / balance / charge) without coupling the new feature's
   32:   price to the volunteer-recruit charging (an elite that is also a volunteer must not be double-charged).
   33: - Offer the **current owner faction's** troops + resource, re-resolved each menu open so conquest flips
   34:   the offerings with no extra wiring.
   35: - Open a conversation from a settlement menu robustly, without the fragile `PlayerEncounter` machinery.
   36: 
   37: ### Solution Approach
   38: 
   39: - **Separate `merchant_cost` field** on the existing `troop_resource_costs.xml` rows (parsed into
   40:   `TroopResourceCostEntry.MerchantCost`). `recruit_cost` stays the volunteer-gate price; `merchant_cost`
   41:   is the emissary price. Two new thin methods on `ISpecialResourceService`
   42:   (`CanAffordMerchantPurchase` / `ChargeMerchantPurchase`) keep all resource-storage mutation inside
   43:   SpecialResources; the volunteer gate is never touched.
   44: - **Dynamic owner resolution** via `ISettlementOwnerAdapter` — `Settlement.OwnerClan?.Culture/Kingdom`
   45:   (the engine's `OwnerClan` already hops village→bound-town). The owner culture selects the offer list;
   46:   the owner kingdom+culture resolves the resource that is charged and whose balance is read.
   47: - **Conversation** via `CampaignMapConversation.OpenConversation(player, notable)` with a custom dialog
   48:   token, flag-gated (`_pendingEmissaryHeroId`) so the emissary greeting only fires for the conversation
   49:   the menu launched — never hijacks a normal notable chat. If a settlement has no living notable, the
   50:   purchase list opens directly (logged).
   51: - **Transaction order = afford → grant → charge.** Granting before charging means a failed grant (no
   52:   party, unknown troop id) never charges — no refund path needed (a single integer roster add can't
   53:   partially apply).
   54: 
   55: ### Component Diagram
   56: 
   57: ```
   58: [town/castle/village menu]  "Speak with the faction emissary"  (EliteEmissaryBehavior)
   59:         │ condition: IsKeySettlement + HasPurchasableOffers (owner faction)
   60:         ▼
   61:   CampaignMapConversation.OpenConversation(player, settlement notable)
   62:         │ dialog: greet → "I wish to purchase elite units."
   63:         ▼
   64:   EliteEmissaryInquiryPresenter.OpenTroopList(settlement)         ← boundary (engine UI)
   65:         │  ISettlementOwnerAdapter.GetOwnerInfo → (kingdom, culture)
   66:         │  IEliteEmissaryService.BuildOfferList(hero, kingdom, culture)
   67:         ▼  ShowMultiSelectionInquiry (troop → quantity, afford-gray)
   68:   IEliteEmissaryService.Purchase(hero, kingdom, culture, troop, qty)   ← pure service
   69:         │  afford → IPlayerPartyAdapter.GrantTroop → ISpecialResourceService.ChargeMerchantPurchase
   70:         ▼
   71:   EmissaryPurchaseResult → player message
   72: ```
   73: 
   74: ## Design Decisions & Known Edge Cases
   75: 
   76: These were reviewed in the 2026-06-25 deep review and are intentional, not bugs:
   77: 
   78: - **Resource resolution is kingdom-first; offers are culture-keyed.** `BuildOfferList`/`Purchase` select
   79:   offers by the owner **culture** but resolve the charged resource via the standard
   80:   `ResolveResource(kingdom, culture)` (kingdom-first). This is consistent with the *earning* side — a
   81:   settlement's owner faction earns the resource resolved from its kingdom. The only divergence is the rare
   82:   dynamic state where a clan's culture and kingdom map to *different* resources (e.g. a Gondor-culture clan
   83:   that defected into the Mordor kingdom and holds a key settlement): it would offer Gondor elites priced in
   84:   the kingdom's resource (War Spoils). This never triggers in the shipping config (all capitals start
   85:   culture==kingdom-aligned), is internally consistent (list, picker, and charge all use the resolved
   86:   resource), and is not a money exploit — only a thematic mismatch in an edge state. Left as-is.
   87: - **No war/relation gate.** The option lives on the `town`/`castle`/`village` game menus, which the player
   88:   can only open by **entering** a settlement — and hostile settlements aren't enterable (clicking an enemy
   89:   fief yields an encounter/siege, not the menu). So there is no "buy elites from a faction you're besieging"
   90:   path. Affordability self-gates via the resource (you only hold a faction's resource if you fight for it).
   91: - **Purchases are not party-size-capped.** `GrantTroop` adds the bought troops regardless of
   92:   `PartySizeLimit` (like quest/ransom troop rewards). Buying over the cap is allowed; the player chose to.
   93: - **Creature troops** (`taom_spider_creature`, `harad_elephant_rider`) were already player-recruitable as
   94:   volunteers before this feature, so selling them via the emissary reaches no new command path — their
   95:   native-crash mitigations already cover player-party instances. (Smoke-test that a bought spider mounts
   96:   correctly, since the emissary is a new ownership entry point.)
   97: - **Greeting-flag lifecycle is hardened.** `_pendingEmissaryHeroId` (which gates the emissary greeting so it
   98:   only fires for the menu-launched conversation, never a normal notable chat) is cleared in both
   99:   `GreetConsequence` AND on `CampaignEvents.ConversationEnded`, so it can never leak into a later
  100:   conversation even if a higher-priority vanilla `start` line wins the emissary conversation.
  101: 
  102: ## Configuration
  103: 
  104: ### Config File: `Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml`
  105: 
  106: - `<KeySettlements>` — settlement StringIds where the emissary appears (verified against
  107:   `TAOM_Map/settlements.xml`).
  108: - `<CultureOffers>` — keyed by the settlement's **owner culture** StringId; each lists ordered troop ids.
  109:   An offer troop with no `merchant_cost` row, or a culture id not in the known set, is dropped + warned at
  110:   load. Key-settlement ids are validated against live settlements at session launch (warning per missing id).
  111: 
  112: ### Config File: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`
  113: 
  114: - `merchant_cost="N"` per `<Troop>` — the one-time emissary price (in the owner faction's resource).
  115:   Cost band: L36 ≈ 10–14, L41 ≈ 18, L46 ≈ 28, L51 ≈ 45, creatures ≈ premium.
  116: 
  117: ### Current Values
  118: 
  119: 11 cultures with offers (owner culture → capital → resource):
  120: 
  121: | Culture | Capital (StringId) | Resource | Example elites |
  122: |---|---|---|---|
  123: | gondor | Minas Tirith (`town_EW1`) | Castar | ithilien_ranger, fountain_guard, swan_knight |
  124: | mordor | Barad Dûr (`town_ES1`) | War Spoils | uruk_captain, baraddurguard, spider |
  125: | erebor | Erebor (`town_E1`) | Gems | royal_warden, royal_legionary, ironbreaker |
  126: | dolguldur | Dol Guldur (`town_DG1`) | War Spoils | khamul_shadow_knight/reaper/bowman |
  127: | isengard | Orthanc (`town_isengard`) | War Spoils | orthanc_bodyguard, nazg_hai, warden |
  128: | gundabad | Mount Gundabad (`town_G1`) | War Spoils | dread_rider, bolgs_ironfang, berserker |
  129: | mirkwood | Felegoth (`town_M1`) | Elven Wine | palaceguard, thingolheir, beleglas |
  130: | rivendell | Rivendell (`town_R1`) | Elven Wine | high_captain, knight_golden_flower, glorfindel_guard |
  131: | vlandia (Rohan) | Edoras (`town_V1`) | Marks | golden_hall_supreme_rider, kings_own_* |
  132: | khuzait (Rhun) | Mistrand (`town_RU1`) | War Banners | dragon_wrath_obsidian_*, warlord_chariot |
  133: | aserai (Harad) | Korb Taskral (`town_A1`) | War Drums | elephant_rider |
  134: 
  135: Omitted (no L36+ elites): Dale (sturgia), Dunland (empire), Umbar, Khand (battania), Lothlorien.
  136: Omitted (no special-resource mapping): goblin, mistymountainorcs.
  137: 
  138: ### MCM — group "Elite Emissary"
  139: 
  140: - `EnableEliteEmissary` (master, default on).
  141: - `HideEmissaryWhenNoResource` (default on) — hide the option at settlements whose owner faction has no
  142:   special resource; off = show it disabled with a hint.
  143: 
  144: ## Key Files
  145: 
  146: | File | Purpose |
  147: |---|---|
  148: | `Main/Features/EliteEmissary/EliteEmissaryService.cs` | Pure logic: offer-list build, afford, transaction |
  149: | `Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs` | Loads + validates the config XML |
  150: | `Main/Features/EliteEmissary/EliteEmissarySettingsProvider.cs` | MCM-over-config-default |
  151: | `Main/Features/EliteEmissary/Domain/*` | Offer/result records, `EliteEmissaryConfig` |
  152: | `Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs` | Menu options + dialog wiring + key-settlement validation |
  153: | `Main/Features/EliteEmissary/Hooks/EliteEmissaryInquiryPresenter.cs` | The two-step purchase inquiry (boundary) |
  154: | `Main/Adapters/SettlementOwnerAdapter.cs` | Settlement → owner kingdom/culture |
  155: | `Main/Adapters/PlayerPartyAdapter.cs` | Grant troops to the main party roster |
  156: | `Main/Features/SpecialResources/...` | `MerchantCost` field + `*MerchantPurchase` methods |
  157: | `Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml` | Key settlements + culture offers |
  158: | `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | `merchant_cost` prices |
  159: | `Main/_Module/ModuleData/taom_emissary_strings.xml` | Player-facing strings (12-lang registered) |
  160: 
  161: ## Dependencies
  162: 
  163: - [SpecialResources](special-resources.md) — resolution, balance, storage, the price table.
  164: - MCM (`TaomSettings`), `IPathService`, `IModLogger`.
  165: - No Harmony patch, no GameModel override, no SyncData.
  166: 
  167: ## Tests
  168: 
  169: - `TAOM.Tests/Features/EliteEmissary/EliteEmissaryServiceTests.cs` — offer build, afford-gray, owner
  170:   resolution, the full Purchase decision tree (Invalid / NoResource / NotOffered / Unaffordable / Success /
  171:   grant-fail), grant-before-charge ordering.
  172: - `TAOM.Tests/Features/EliteEmissary/EliteEmissaryConfigProviderTests.cs` — valid/missing/malformed XML,
  173:   unknown-culture drop, unpriced-troop drop, enabled flag.
  174: - `TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs` — the two `*MerchantPurchase`
  175:   methods (afford boundary, charge amount, no-merchant-cost / no-resource / zero-count no-ops).
  176: 
  177: ## How to Add a Faction or Troop
  178: 
  179: 1. Add the troop id with a `merchant_cost` to `troop_resource_costs.xml` (and confirm the id is a real
  180:    `CharacterObject`).
  181: 2. Add it under the owner culture's `<Culture>` block in `elite_emissary_config.xml` (create the block if
  182:    the culture is new; the culture must map to a special resource in `special_resources_config.xml`).
  183: 3. To add a new key settlement, add its StringId to `<KeySettlements>` (verify against
  184:    `TAOM_Map/settlements.xml`).
  185: 4. New player-facing strings → `{=KEY}` in `taom_emissary_strings.xml`, then run `/localize`.
  186: 
  187: ## Performance
  188: 
  189: The menu condition runs per menu open (not per frame): a HashSet membership check + one owner resolution +
  190: an offer scan. The config is `Reuse.Singleton`, loaded once per process (edits need an app restart).
  191: 
  192: ## Changelog
  193: 
  194: - 2026-06-25 — Feature created. 11 cultures authored, L36+ elites, verified capitals.
  195: 
  196: ## GitHub Issue
  197: 
  198: TBD (create on ship).

 succeeded in 565ms:
    1: using System.Collections.Generic;
    2: using Helpers;
    3: using TaleWorlds.CampaignSystem;
    4: using TaleWorlds.CampaignSystem.Conversation;
    5: using TaleWorlds.CampaignSystem.GameMenus;
    6: using TaleWorlds.CampaignSystem.Settlements;
    7: using TaleWorlds.Localization;
    8: using TAOM.Adapters;
    9: using TAOM.Core.Logging;
   10: 
   11: namespace TAOM.Features.EliteEmissary.Hooks;
   12: 
   13: /// <summary>
   14: /// Thin boundary (ADR-002) for the Settlement Elite Emissary: registers a "Speak with the faction
   15: /// emissary" option on the town/castle/village menus at key settlements, opens a short conversation
   16: /// with a settlement notable, and on the "purchase elite units" line hands off to the
   17: /// <see cref="EliteEmissaryInquiryPresenter"/>. All decisions live in <see cref="IEliteEmissaryService"/>.
   18: /// Stateless across saves — no SyncData (balances persist via SpecialResources; troops via the engine roster).
   19: /// </summary>
   20: public sealed class EliteEmissaryBehavior : CampaignBehaviorBase
   21: {
   22:     private const string MenuOptionId = "taom_emissary_speak";
   23: 
   24:     private readonly IEliteEmissaryService _service;
   25:     private readonly IEliteEmissarySettingsProvider _settings;
   26:     private readonly IEliteEmissaryConfigProvider _config;
   27:     private readonly ISettlementOwnerAdapter _ownerAdapter;
   28:     private readonly IModLogger _logger;
   29:     private readonly EliteEmissaryInquiryPresenter _presenter;
   30: 
   31:     // Set in the menu consequence right before OpenConversation so the emissary greeting only fires
   32:     // for the conversation WE launched (never hijacks a normal notable chat). Cleared once greeted.
   33:     private string _pendingEmissaryHeroId;
   34:     private bool _keySettlementsValidated;
   35: 
   36:     public EliteEmissaryBehavior(
   37:         IEliteEmissaryService service,
   38:         IEliteEmissarySettingsProvider settings,
   39:         IEliteEmissaryConfigProvider config,
   40:         ISettlementOwnerAdapter ownerAdapter,
   41:         IModLogger logger)
   42:     {
   43:         _service = service;
   44:         _settings = settings;
   45:         _config = config;
   46:         _ownerAdapter = ownerAdapter;
   47:         _logger = logger;
   48:         _presenter = new EliteEmissaryInquiryPresenter(service, ownerAdapter, logger);
   49:     }
   50: 
   51:     public override void RegisterEvents()
   52:     {
   53:         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
   54:         // Belt to GreetConsequence's clear (deep-review 2026-06-25): guarantees the greeting flag never
   55:         // survives a conversation, so a stale flag can't make the emissary greeting appear in a later
   56:         // normal chat with the same notable (the rare case where a higher-priority vanilla "start" line —
   57:         // e.g. an active counter-offer issue with that notable — wins and our greet consequence never fires).
   58:         CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
   59:     }
   60: 
   61:     public override void SyncData(IDataStore dataStore)
   62:     {
   63:         // Stateless: resource balances persist via SpecialResources' store; granted troops via the
   64:         // engine roster; config is immutable; owner faction is re-resolved every menu open.
   65:     }
   66: 
   67:     private void OnSessionLaunched(CampaignGameStarter starter)
   68:     {
   69:         // Registered unconditionally so the MCM master toggle takes effect at runtime (the condition
   70:         // re-checks IsEnabled each open). Key settlements can be any type → all three menus.
   71:         var menuText = new TextObject("{=taom_emissary_menu}Speak with the faction emissary");
   72:         starter.AddGameMenuOption("town", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 5);
   73:         starter.AddGameMenuOption("castle", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 5);
   74:         starter.AddGameMenuOption("village", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 4);
   75: 
   76:         RegisterDialog(starter);
   77:         ValidateKeySettlements();
   78:     }
   79: 
   80:     // --- Menu ---
   81: 
   82:     private bool MenuCondition(MenuCallbackArgs args)
   83:     {
   84:         if (!_service.IsEnabled) return false;
   85:         var settlement = Settlement.CurrentSettlement;
   86:         if (settlement == null || !_service.IsKeySettlement(settlement.StringId)) return false;
   87: 
   88:         var owner = _ownerAdapter.GetOwnerInfo(settlement);
   89:         bool hasOffers = _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
   90:         if (!hasOffers && _settings.HideWhenNoResource)
   91:             return false;
   92: 
   93:         args.optionLeaveType = GameMenuOption.LeaveType.Trade;
   94:         return MenuHelper.SetOptionProperties(args, hasOffers, !hasOffers,
   95:             new TextObject("{=taom_emissary_no_trade}There is no elite trade for this faction here."));
   96:     }
   97: 
   98:     private void MenuConsequence(MenuCallbackArgs args)
   99:     {
  100:         var settlement = Settlement.CurrentSettlement;
  101:         if (settlement == null) return;
  102: 
  103:         var notable = FindEmissaryNotable(settlement);
  104:         if (notable != null)
  105:         {
  106:             _pendingEmissaryHeroId = notable.StringId;
  107:             _logger.LogInfo($"[EliteEmissary] Opening emissary conversation with {notable.Name} at {settlement.StringId}");
  108:             CampaignMapConversation.OpenConversation(
  109:                 new ConversationCharacterData(CharacterObject.PlayerCharacter),
  110:                 new ConversationCharacterData(notable.CharacterObject));
  111:         }
  112:         else
  113:         {
  114:             // No notable to embody the emissary (rare): open the purchase list directly.
  115:             _logger.LogWarning($"[EliteEmissary] No living notable at {settlement.StringId} — opening purchase list directly");
  116:             _presenter.OpenTroopList(settlement);
  117:         }
  118:     }
  119: 
  120:     private static Hero FindEmissaryNotable(Settlement settlement)
  121:     {
  122:         foreach (var notable in settlement.Notables)
  123:         {
  124:             if (notable != null && notable.IsAlive)
  125:                 return notable;
  126:         }
  127:         return null;
  128:     }
  129: 
  130:     // --- Dialog ---
  131: 
  132:     private void RegisterDialog(CampaignGameStarter starter)
  133:     {
  134:         starter.AddDialogLine("taom_emissary_greet", "start", "taom_emissary_hub",
  135:             "{=taom_emissary_greet}You seek the finest warriors our people can offer? Name them, and they are yours — for the right price.",
  136:             GreetCondition, GreetConsequence, 200);
  137: 
  138:         starter.AddPlayerLine("taom_emissary_buy", "taom_emissary_hub", "close_window",
  139:             "{=taom_emissary_buy}I wish to purchase elite units.",
  140:             BuyCondition, BuyConsequence, 200);
  141: 
  142:         starter.AddPlayerLine("taom_emissary_leave", "taom_emissary_hub", "close_window",
  143:             "{=taom_emissary_leave}Not today.", null, null, 100);
  144:     }
  145: 
  146:     private bool GreetCondition()
  147:     {
  148:         return _pendingEmissaryHeroId != null
  149:             && Hero.OneToOneConversationHero?.StringId == _pendingEmissaryHeroId;
  150:     }
  151: 
  152:     private void GreetConsequence()
  153:     {
  154:         // Clear so a later NORMAL conversation with the same notable doesn't show the emissary greeting.
  155:         _pendingEmissaryHeroId = null;
  156:     }
  157: 
  158:     private void OnConversationEnded(IEnumerable<CharacterObject> characters)
  159:     {
  160:         // Bulletproof clear: after ANY conversation ends, the greeting flag is reset. Covers the case
  161:         // where our greet line never fired (a higher-priority vanilla start line won), which would
  162:         // otherwise leave _pendingEmissaryHeroId set and leak the emissary greeting into the next
  163:         // normal conversation with that notable.
  164:         _pendingEmissaryHeroId = null;
  165:     }
  166: 
  167:     private bool BuyCondition()
  168:     {
  169:         var settlement = Settlement.CurrentSettlement;
  170:         if (settlement == null) return false;
  171:         var owner = _ownerAdapter.GetOwnerInfo(settlement);
  172:         return _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
  173:     }
  174: 
  175:     private void BuyConsequence()
  176:     {
  177:         var settlement = Settlement.CurrentSettlement;
  178:         if (settlement != null)
  179:             _presenter.OpenTroopList(settlement);
  180:     }
  181: 
  182:     // --- Key-settlement id validation (settlements exist by session launch; MBObjectManager populated) ---
  183: 
  184:     private void ValidateKeySettlements()
  185:     {
  186:         if (_keySettlementsValidated) return;
  187:         _keySettlementsValidated = true;
  188: 
  189:         int ok = 0, missing = 0;
  190:         foreach (var id in _config.GetConfig().KeySettlementIds)
  191:         {
  192:             if (Settlement.Find(id) == null)
  193:             {
  194:                 _logger.LogWarning($"[EliteEmissary] key settlement '{id}' not found in this campaign — emissary will never appear there (check elite_emissary_config.xml against settlements.xml)");
  195:                 missing++;
  196:             }
  197:             else
  198:             {
  199:                 ok++;
  200:             }
  201:         }
  202:         _logger.LogInfo($"[EliteEmissary] key-settlement validation: {ok} resolved, {missing} missing");
  203:     }
  204: }

codex
The first pass already shows the important owner/resource decision split: offers come from owner culture, while resource/balance/charge use `ResolveResource(ownerKingdomId, ownerCultureId)`. I’m now checking whether every boundary path passes the same tuple and whether the UI can bypass the service’s revalidation.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Hooks\\EliteEmissaryInquiryPresenter.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\EliteEmissaryConfigProvider.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\IEliteEmissaryService.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 583ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.IO;
    4: using System.Xml.Linq;
    5: using TAOM.Core.Infrastructure;
    6: using TAOM.Core.Logging;
    7: using TAOM.Features.EliteEmissary.Domain;
    8: using TAOM.Features.SpecialResources;
    9: 
   10: namespace TAOM.Features.EliteEmissary;
   11: 
   12: /// <summary>
   13: /// Loads + validates <c>elite_emissary/elite_emissary_config.xml</c> (Config-Providers-MUST-Validate,
   14: /// csharp-architecture.md). Missing/malformed → <see cref="EliteEmissaryConfig.Empty"/>. Validation:
   15: /// a <c>&lt;Culture&gt;</c> id not in the known culture set is dropped+warned (a typo would otherwise
   16: /// silently produce "this faction has no elites" — the M1 trap); a <c>&lt;Troop&gt;</c> with no
   17: /// <c>merchant_cost</c> row in troop_resource_costs.xml is dropped+warned (it would be unsellable).
   18: /// Key-settlement ids can't be validated here (MBObjectManager isn't populated at load) — the behavior
   19: /// validates them against live settlements at session launch.
   20: /// </summary>
   21: public sealed class EliteEmissaryConfigProvider : IEliteEmissaryConfigProvider
   22: {
   23:     // Owner-culture StringIds an emissary offer block may be keyed by. Source: CLAUDE.md / xml-data.md
   24:     // culture table (custom LOTR cultures + XSLT engine-id cultures + the two orc-host cultures).
   25:     private static readonly HashSet<string> KnownCultureIds = new(StringComparer.Ordinal)
   26:     {
   27:         "gondor", "mordor", "erebor", "rivendell", "lothlorien", "mirkwood",
   28:         "isengard", "gundabad", "dolguldur", "umbar", "goblin", "mistymountainorcs",
   29:         "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania",
   30:     };
   31: 
   32:     private readonly IPathService _pathService;
   33:     private readonly IModLogger _logger;
   34:     private readonly ISpecialResourceConfigProvider _resourceConfig;
   35:     private readonly Lazy<EliteEmissaryConfig> _config;
   36: 
   37:     public EliteEmissaryConfigProvider(IPathService pathService, IModLogger logger, ISpecialResourceConfigProvider resourceConfig)
   38:     {
   39:         _pathService = pathService;
   40:         _logger = logger;
   41:         _resourceConfig = resourceConfig;
   42:         _config = new Lazy<EliteEmissaryConfig>(LoadConfig);
   43:     }
   44: 
   45:     public EliteEmissaryConfig GetConfig() => _config.Value;
   46: 
   47:     private EliteEmissaryConfig LoadConfig()
   48:     {
   49:         var path = Path.Combine(_pathService.ModuleDataPath, "elite_emissary", "elite_emissary_config.xml");
   50: 
   51:         if (!File.Exists(path))
   52:         {
   53:             _logger.LogWarning($"EliteEmissaryConfigProvider: config not found at {path} — Elite Emissary inert");
   54:             return EliteEmissaryConfig.Empty;
   55:         }
   56: 
   57:         XDocument doc;
   58:         try
   59:         {
   60:             doc = XDocument.Load(path);
   61:         }
   62:         catch (Exception ex)
   63:         {
   64:             _logger.LogError($"EliteEmissaryConfigProvider: failed to parse elite_emissary_config.xml: {ex.Message}");
   65:             return EliteEmissaryConfig.Empty;
   66:         }
   67: 
   68:         try
   69:         {
   70:             var root = doc.Root;
   71:             var enabled = ParseBool(root?.Attribute("enabled")?.Value, true);
   72: 
   73:             var keySettlements = new HashSet<string>(StringComparer.Ordinal);
   74:             var keyEl = root?.Element("KeySettlements");
   75:             if (keyEl != null)
   76:             {
   77:                 foreach (var s in keyEl.Elements("Settlement"))
   78:                 {
   79:                     var id = s.Attribute("id")?.Value;
   80:                     if (!string.IsNullOrWhiteSpace(id))
   81:                         keySettlements.Add(id.Trim());
   82:                 }
   83:             }
   84: 
   85:             var cultureOffers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
   86:             var offersEl = root?.Element("CultureOffers");
   87:             int droppedTroops = 0, droppedCultures = 0;
   88:             if (offersEl != null)
   89:             {
   90:                 foreach (var c in offersEl.Elements("Culture"))
   91:                 {
   92:                     var cultureId = c.Attribute("id")?.Value?.Trim();
   93:                     if (string.IsNullOrEmpty(cultureId))
   94:                     {
   95:                         _logger.LogWarning("EliteEmissaryConfigProvider: <Culture> with empty id — skipped");
   96:                         continue;
   97:                     }
   98:                     if (!KnownCultureIds.Contains(cultureId))
   99:                     {
  100:                         _logger.LogWarning($"EliteEmissaryConfigProvider: unknown culture id '{cultureId}' — dropped (offers for it would never be reachable)");
  101:                         droppedCultures++;
  102:                         continue;
  103:                     }
  104:                     if (cultureOffers.ContainsKey(cultureId))
  105:                     {
  106:                         _logger.LogWarning($"EliteEmissaryConfigProvider: duplicate <Culture id='{cultureId}'> — keeping the first block, ignoring the rest");
  107:                         continue;
  108:                     }
  109: 
  110:                     var troops = new List<string>();
  111:                     foreach (var t in c.Elements("Troop"))
  112:                     {
  113:                         var troopId = t.Attribute("id")?.Value?.Trim();
  114:                         if (string.IsNullOrEmpty(troopId))
  115:                             continue;
  116: 
  117:                         var cost = _resourceConfig.GetTroopCost(troopId);
  118:                         if (cost == null || cost.MerchantCost <= 0)
  119:                         {
  120:                             _logger.LogWarning($"EliteEmissaryConfigProvider: offer '{troopId}' (culture {cultureId}) has no merchant_cost in troop_resource_costs.xml — dropped (unsellable)");
  121:                             droppedTroops++;
  122:                             continue;
  123:                         }
  124:                         troops.Add(troopId);
  125:                     }
  126: 
  127:                     if (troops.Count > 0)
  128:                         cultureOffers[cultureId] = troops;
  129:                     else
  130:                         _logger.LogWarning($"EliteEmissaryConfigProvider: culture '{cultureId}' has no sellable offers after validation — not recorded");
  131:                 }
  132:             }
  133: 
  134:             _logger.LogInfo($"EliteEmissaryConfigProvider: loaded {keySettlements.Count} key settlement(s), {cultureOffers.Count} culture offer list(s) (enabled={enabled}; dropped {droppedCultures} culture(s), {droppedTroops} troop(s))");
  135:             return new EliteEmissaryConfig(enabled, keySettlements, cultureOffers);
  136:         }
  137:         catch (Exception ex)
  138:         {
  139:             _logger.LogError($"EliteEmissaryConfigProvider: failed to parse elite_emissary_config.xml: {ex.Message}");
  140:             return EliteEmissaryConfig.Empty;
  141:         }
  142:     }
  143: 
  144:     private static bool ParseBool(string value, bool defaultValue) =>
  145:         bool.TryParse(value, out var b) ? b : defaultValue;
  146: }

 succeeded in 525ms:
    1: using TAOM.Features.EliteEmissary.Domain;
    2: 
    3: namespace TAOM.Features.EliteEmissary;
    4: 
    5: /// <summary>
    6: /// Pure logic for the Settlement Elite Emissary: deciding which settlements host an emissary, what a
    7: /// faction offers, whether the player can afford it, and executing the resource-for-troops purchase.
    8: /// Speaks only strings + Domain records — sealed TaleWorlds types stay in the behavior/adapters
    9: /// (ADR-002/007). The resource charged is the SETTLEMENT OWNER's, resolved from the kingdom/culture
   10: /// the behavior passes in (so conquest flips both the list and the currency).
   11: /// </summary>
   12: public interface IEliteEmissaryService
   13: {
   14:     /// <summary>MCM master toggle — re-checked on every menu open.</summary>
   15:     bool IsEnabled { get; }
   16: 
   17:     /// <summary>True if <paramref name="settlementId"/> is one of the curated key settlements.</summary>
   18:     bool IsKeySettlement(string settlementId);
   19: 
   20:     /// <summary>True if the owner culture has at least one sellable (merchant_cost &gt; 0) offer AND
   21:     /// the faction maps to a special resource — drives the "purchase elite units" dialog visibility.</summary>
   22:     bool HasPurchasableOffers(string ownerKingdomId, string ownerCultureId);
   23: 
   24:     /// <summary>Builds the offer list for the settlement OWNER faction, with the player's per-troop
   25:     /// affordability pre-computed. Returns <see cref="EmissaryOfferList.NoResourceAvailable"/> when
   26:     /// the faction maps to no resource.</summary>
   27:     EmissaryOfferList BuildOfferList(string heroId, string ownerKingdomId, string ownerCultureId);
   28: 
   29:     /// <summary>Executes a purchase: afford-check → grant troops → charge the owner resource (grant
   30:     /// before charge, so a failed grant never charges). Never throws — returns a typed result.</summary>
   31:     EmissaryPurchaseResult Purchase(string heroId, string ownerKingdomId, string ownerCultureId, string troopId, int quantity);
   32: }

 succeeded in 588ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using TaleWorlds.CampaignSystem;
    4: using TaleWorlds.CampaignSystem.Settlements;
    5: using TaleWorlds.Core;
    6: using TaleWorlds.Library;
    7: using TaleWorlds.Localization;
    8: using TaleWorlds.ObjectSystem;
    9: using TAOM.Adapters;
   10: using TAOM.Core.Logging;
   11: using TAOM.Features.EliteEmissary.Domain;
   12: 
   13: namespace TAOM.Features.EliteEmissary.Hooks;
   14: 
   15: /// <summary>
   16: /// Boundary presentation for the emissary purchase flow (engine-coupled — kept out of the pure
   17: /// service per ADR-002): builds the two-step troop-then-quantity <c>ShowMultiSelectionInquiry</c>
   18: /// (afford-gray per element), then delegates the decision + transaction to
   19: /// <see cref="IEliteEmissaryService.Purchase"/> and surfaces the typed result as a player message.
   20: /// </summary>
   21: public sealed class EliteEmissaryInquiryPresenter
   22: {
   23:     private readonly IEliteEmissaryService _service;
   24:     private readonly ISettlementOwnerAdapter _ownerAdapter;
   25:     private readonly IModLogger _logger;
   26: 
   27:     public EliteEmissaryInquiryPresenter(IEliteEmissaryService service, ISettlementOwnerAdapter ownerAdapter, IModLogger logger)
   28:     {
   29:         _service = service;
   30:         _ownerAdapter = ownerAdapter;
   31:         _logger = logger;
   32:     }
   33: 
   34:     public void OpenTroopList(Settlement settlement)
   35:     {
   36:         try
   37:         {
   38:             if (settlement == null) return;
   39:             var heroId = Hero.MainHero?.StringId;
   40:             if (heroId == null)
   41:             {
   42:                 _logger.LogWarning("[EliteEmissary] OpenTroopList: no main hero");
   43:                 return;
   44:             }
   45: 
   46:             var owner = _ownerAdapter.GetOwnerInfo(settlement);
   47:             _logger.LogInfo($"[EliteEmissary] OpenTroopList at {owner.SettlementId}: owner kingdom='{owner.OwnerKingdomId}' culture='{owner.OwnerCultureId}'");
   48: 
   49:             var offerList = _service.BuildOfferList(heroId, owner.OwnerKingdomId, owner.OwnerCultureId);
   50:             if (offerList.NoResource)
   51:             {
   52:                 Notify("{=taom_emissary_no_resource}There is no emissary trade in this settlement.");
   53:                 return;
   54:             }
   55:             if (!offerList.HasOffers)
   56:             {
   57:                 Notify("{=taom_emissary_no_offers}The emissary has no elite units to offer right now.");
   58:                 return;
   59:             }
   60: 
   61:             var elements = new List<InquiryElement>();
   62:             foreach (var offer in offerList.Offers)
   63:             {
   64:                 var character = MBObjectManager.Instance?.GetObject<CharacterObject>(offer.TroopId);
   65:                 if (character == null)
   66:                 {
   67:                     _logger.LogWarning($"[EliteEmissary] offer '{offer.TroopId}' did not resolve to a CharacterObject — skipped from list");
   68:                     continue;
   69:                 }
   70: 
   71:                 var title = new TextObject("{=taom_emissary_offer_line}{NAME} — {COST} {RESOURCE}")
   72:                     .SetTextVariable("NAME", character.Name)
   73:                     .SetTextVariable("COST", offer.MerchantCost)
   74:                     .SetTextVariable("RESOURCE", offerList.ResourceDisplayName)
   75:                     .ToString();
   76: 
   77:                 var hint = (offer.CanAfford
   78:                     ? new TextObject("{=taom_emissary_offer_afford}You can afford up to {MAX}.").SetTextVariable("MAX", offer.MaxAffordableQuantity)
   79:                     : new TextObject("{=taom_emissary_offer_cant}Not enough {RESOURCE}.").SetTextVariable("RESOURCE", offerList.ResourceDisplayName))
   80:                     .ToString();
   81: 
   82:                 // Per-element portraits are skipped — v1.4.6's InquiryElement image type is the abstract
   83:                 // TaleWorlds.Core.ImageIdentifiers.ImageIdentifier; name + cost in the title is the safe
   84:                 // text-only baseline (QuickActions does the same). Portraits can be added later.
   85:                 elements.Add(new InquiryElement(offer, title, null, offer.CanAfford, hint));
   86:             }
   87: 
   88:             if (elements.Count == 0)
   89:             {
   90:                 Notify("{=taom_emissary_no_offers}The emissary has no elite units to offer right now.");
   91:                 return;
   92:             }
   93: 
   94:             var desc = new TextObject("{=taom_emissary_list_desc}Balance: {AMOUNT} {RESOURCE}")
   95:                 .SetTextVariable("AMOUNT", (int)offerList.PlayerBalance)
   96:                 .SetTextVariable("RESOURCE", offerList.ResourceDisplayName)
   97:                 .ToString();
   98: 
   99:             var data = new MultiSelectionInquiryData(
  100:                 titleText: new TextObject("{=taom_emissary_list_title}Elite Units").ToString(),
  101:                 descriptionText: desc,
  102:                 inquiryElements: elements,
  103:                 isExitShown: true,
  104:                 minSelectableOptionCount: 1,
  105:                 maxSelectableOptionCount: 1,
  106:                 affirmativeText: new TextObject("{=taom_emissary_select}Select").ToString(),
  107:                 negativeText: new TextObject("{=taom_emissary_back}Back").ToString(),
  108:                 affirmativeAction: chosen =>
  109:                 {
  110:                     if (chosen == null || chosen.Count == 0) return;
  111:                     if (chosen[0].Identifier is EmissaryTroopOffer offer)
  112:                         OpenQuantityPicker(settlement, owner, heroId, offer, offerList.ResourceDisplayName, offerList.PlayerBalance);
  113:                 },
  114:                 negativeAction: _ => { });
  115: 
  116:             MBInformationManager.ShowMultiSelectionInquiry(data);
  117:         }
  118:         catch (Exception ex)
  119:         {
  120:             _logger.LogError($"[EliteEmissary] OpenTroopList failed: {ex.Message}");
  121:         }
  122:     }
  123: 
  124:     private void OpenQuantityPicker(Settlement settlement, SettlementOwnerInfo owner, string heroId, EmissaryTroopOffer offer, string resourceName, float balance)
  125:     {
  126:         try
  127:         {
  128:             var character = MBObjectManager.Instance?.GetObject<CharacterObject>(offer.TroopId);
  129:             var troopName = character?.Name?.ToString() ?? offer.TroopId;
  130: 
  131:             var quantities = new List<int>();
  132:             foreach (var q in new[] { 1, 5, 10, offer.MaxAffordableQuantity })
  133:                 if (q >= 1 && !quantities.Contains(q))
  134:                     quantities.Add(q);
  135:             quantities.Sort();
  136: 
  137:             var elements = new List<InquiryElement>();
  138:             foreach (var q in quantities)
  139:             {
  140:                 var enabled = q <= offer.MaxAffordableQuantity;
  141:                 var label = new TextObject("{=taom_emissary_qty_line}{QTY} (×{COST} = {TOTAL} {RESOURCE})")
  142:                     .SetTextVariable("QTY", q)
  143:                     .SetTextVariable("COST", offer.MerchantCost)
  144:                     .SetTextVariable("TOTAL", q * offer.MerchantCost)
  145:                     .SetTextVariable("RESOURCE", resourceName)
  146:                     .ToString();
  147:                 var hint = enabled ? string.Empty
  148:                     : new TextObject("{=taom_emissary_offer_cant}Not enough {RESOURCE}.").SetTextVariable("RESOURCE", resourceName).ToString();
  149:                 elements.Add(new InquiryElement(q, label, null, enabled, hint));
  150:             }
  151: 
  152:             var title = new TextObject("{=taom_emissary_qty_title}How many {TROOP}?").SetTextVariable("TROOP", troopName).ToString();
  153:             var desc = new TextObject("{=taom_emissary_qty_desc}Each costs {COST} {RESOURCE}. You have {BALANCE}.")
  154:                 .SetTextVariable("COST", offer.MerchantCost)
  155:                 .SetTextVariable("RESOURCE", resourceName)
  156:                 .SetTextVariable("BALANCE", (int)balance)
  157:                 .ToString();
  158: 
  159:             var data = new MultiSelectionInquiryData(
  160:                 titleText: title,
  161:                 descriptionText: desc,
  162:                 inquiryElements: elements,
  163:                 isExitShown: true,
  164:                 minSelectableOptionCount: 1,
  165:                 maxSelectableOptionCount: 1,
  166:                 affirmativeText: new TextObject("{=taom_emissary_confirm}Recruit").ToString(),
  167:                 negativeText: new TextObject("{=taom_emissary_back}Back").ToString(),
  168:                 affirmativeAction: chosen =>
  169:                 {
  170:                     if (chosen == null || chosen.Count == 0) return;
  171:                     if (chosen[0].Identifier is int qty)
  172:                         ExecutePurchase(owner, heroId, offer.TroopId, qty);
  173:                 },
  174:                 negativeAction: _ => { });
  175: 
  176:             MBInformationManager.ShowMultiSelectionInquiry(data);
  177:         }
  178:         catch (Exception ex)
  179:         {
  180:             _logger.LogError($"[EliteEmissary] OpenQuantityPicker failed: {ex.Message}");
  181:         }
  182:     }
  183: 
  184:     private void ExecutePurchase(SettlementOwnerInfo owner, string heroId, string troopId, int qty)
  185:     {
  186:         var result = _service.Purchase(heroId, owner.OwnerKingdomId, owner.OwnerCultureId, troopId, qty);
  187:         var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
  188:         var troopName = character?.Name?.ToString() ?? troopId;
  189: 
  190:         switch (result.Status)
  191:         {
  192:             case EmissaryPurchaseStatus.Success:
  193:                 Notify(new TextObject("{=taom_emissary_bought}Recruited {QTY} {TROOP} for {COST} {RESOURCE}.")
  194:                     .SetTextVariable("QTY", result.Quantity)
  195:                     .SetTextVariable("TROOP", troopName)
  196:                     .SetTextVariable("COST", result.TotalCost)
  197:                     .SetTextVariable("RESOURCE", result.ResourceDisplayName).ToString(), Colors.Green);
  198:                 break;
  199:             case EmissaryPurchaseStatus.Unaffordable:
  200:                 Notify(new TextObject("{=taom_emissary_cant_afford}Not enough {RESOURCE} — need {COST}.")
  201:                     .SetTextVariable("RESOURCE", result.ResourceDisplayName)
  202:                     .SetTextVariable("COST", result.TotalCost).ToString(), Colors.Red);
  203:                 break;
  204:             case EmissaryPurchaseStatus.NoResource:
  205:                 Notify("{=taom_emissary_no_resource}There is no emissary trade in this settlement.");
  206:                 break;
  207:             default:
  208:                 Notify("{=taom_emissary_failed}The emissary could not complete the deal.");
  209:                 break;
  210:         }
  211:     }
  212: 
  213:     private static void Notify(string textKeyOrLiteral) => Notify(textKeyOrLiteral, Colors.White);
  214: 
  215:     private static void Notify(string text, Color color) =>
  216:         InformationManager.DisplayMessage(new InformationMessage(
  217:             text.StartsWith("{=") ? new TextObject(text).ToString() : text, color));
  218: }

codex
The presenter captures the owner tuple when the troop list is opened and reuses it for the final purchase callback. That narrows one race, but I still need to verify if stale UI callbacks can produce grants after ownership/menu context changes, and whether service-side transaction code is exception-safe.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Domain\\EliteEmissaryConfig.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Domain\\EmissaryOfferList.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Domain\\EmissaryTroopOffer.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\Domain\\EmissaryPurchaseResult.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 530ms:
    1: using System;
    2: using System.Collections.Generic;
    3: 
    4: namespace TAOM.Features.EliteEmissary.Domain;
    5: 
    6: /// <summary>Loaded + validated <c>elite_emissary_config.xml</c>: the key-settlement set and the
    7: /// per-culture ordered offer lists. Immutable; built once by the config provider.</summary>
    8: public sealed class EliteEmissaryConfig
    9: {
   10:     public bool Enabled { get; }
   11: 
   12:     /// <summary>Settlement StringIds where the emissary appears (case-sensitive membership).</summary>
   13:     public ISet<string> KeySettlementIds { get; }
   14: 
   15:     /// <summary>cultureId → ordered troop ids offered for that owner culture.</summary>
   16:     public IReadOnlyDictionary<string, IReadOnlyList<string>> CultureOffers { get; }
   17: 
   18:     public EliteEmissaryConfig(bool enabled, ISet<string> keySettlementIds,
   19:         IReadOnlyDictionary<string, IReadOnlyList<string>> cultureOffers)
   20:     {
   21:         Enabled = enabled;
   22:         KeySettlementIds = keySettlementIds ?? new HashSet<string>();
   23:         CultureOffers = cultureOffers ?? new Dictionary<string, IReadOnlyList<string>>();
   24:     }
   25: 
   26:     /// <summary>Empty/inert config — feature does nothing. Used on missing/malformed file.</summary>
   27:     public static readonly EliteEmissaryConfig Empty = new(
   28:         enabled: false,
   29:         keySettlementIds: new HashSet<string>(),
   30:         cultureOffers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
   31: }

 succeeded in 531ms:
    1: using System.Collections.Generic;
    2: 
    3: namespace TAOM.Features.EliteEmissary.Domain;
    4: 
    5: /// <summary>The full set of elite troops a faction's emissary offers at a settlement, plus the
    6: /// resource context (name/icon/balance) needed to render the purchase list. Built fresh per menu
    7: /// open against the SETTLEMENT OWNER's faction so conquest flips the offerings automatically.</summary>
    8: public sealed class EmissaryOfferList
    9: {
   10:     /// <summary>True when the owner faction maps to no special resource — nothing can be sold/priced.</summary>
   11:     public bool NoResource { get; }
   12: 
   13:     public string ResourceId { get; }
   14:     public string ResourceDisplayName { get; }
   15:     public string IconSpriteName { get; }
   16: 
   17:     /// <summary>The player's current balance of the owner faction's resource (0 when NoResource).</summary>
   18:     public float PlayerBalance { get; }
   19: 
   20:     public IReadOnlyList<EmissaryTroopOffer> Offers { get; }
   21: 
   22:     public bool HasOffers => Offers != null && Offers.Count > 0;
   23: 
   24:     private EmissaryOfferList(bool noResource, string resourceId, string resourceDisplayName,
   25:         string iconSpriteName, float playerBalance, IReadOnlyList<EmissaryTroopOffer> offers)
   26:     {
   27:         NoResource = noResource;
   28:         ResourceId = resourceId;
   29:         ResourceDisplayName = resourceDisplayName;
   30:         IconSpriteName = iconSpriteName;
   31:         PlayerBalance = playerBalance;
   32:         Offers = offers ?? new List<EmissaryTroopOffer>();
   33:     }
   34: 
   35:     public static EmissaryOfferList ForResource(string resourceId, string resourceDisplayName,
   36:         string iconSpriteName, float playerBalance, IReadOnlyList<EmissaryTroopOffer> offers) =>
   37:         new(false, resourceId, resourceDisplayName, iconSpriteName, playerBalance, offers);
   38: 
   39:     /// <summary>Owner faction has no special resource — the menu option hides (or shows disabled).</summary>
   40:     public static readonly EmissaryOfferList NoResourceAvailable =
   41:         new(true, null, null, null, 0f, new List<EmissaryTroopOffer>());
   42: }

 succeeded in 559ms:
    1: namespace TAOM.Features.EliteEmissary.Domain;
    2: 
    3: /// <summary>One purchasable elite troop the faction emissary offers, with the player's affordability
    4: /// pre-computed by the service so the boundary just renders it (afford-gray + max-quantity picker).</summary>
    5: public sealed class EmissaryTroopOffer
    6: {
    7:     public string TroopId { get; }
    8: 
    9:     /// <summary>One-time <c>merchant_cost</c> per unit, in the settlement faction's special resource.</summary>
   10:     public int MerchantCost { get; }
   11: 
   12:     /// <summary>True if the player can afford at least ONE of this troop right now.</summary>
   13:     public bool CanAfford { get; }
   14: 
   15:     /// <summary>How many the player can afford at the current balance (floor(balance / cost)).</summary>
   16:     public int MaxAffordableQuantity { get; }
   17: 
   18:     public EmissaryTroopOffer(string troopId, int merchantCost, bool canAfford, int maxAffordableQuantity)
   19:     {
   20:         TroopId = troopId;
   21:         MerchantCost = merchantCost;
   22:         CanAfford = canAfford;
   23:         MaxAffordableQuantity = maxAffordableQuantity;
   24:     }
   25: }

 succeeded in 577ms:
    1: namespace TAOM.Features.EliteEmissary.Domain;
    2: 
    3: public enum EmissaryPurchaseStatus
    4: {
    5:     Success,
    6:     /// <summary>Player lacks enough of the owner faction's resource for the requested quantity.</summary>
    7:     Unaffordable,
    8:     /// <summary>Owner faction maps to no special resource — nothing to charge.</summary>
    9:     NoResource,
   10:     /// <summary>Troop isn't in the owner culture's offer list or has no merchant_cost.</summary>
   11:     NotOffered,
   12:     /// <summary>Bad request (null/empty troop id, quantity ≤ 0).</summary>
   13:     Invalid,
   14:     /// <summary>Charge/afford passed but the roster grant failed (no party, unknown troop id).</summary>
   15:     Failed,
   16: }
   17: 
   18: /// <summary>Typed outcome of <see cref="IEliteEmissaryService.Purchase"/> — never throws; the boundary
   19: /// maps the status to a player message.</summary>
   20: public sealed class EmissaryPurchaseResult
   21: {
   22:     public EmissaryPurchaseStatus Status { get; }
   23:     public string TroopId { get; }
   24:     public int Quantity { get; }
   25:     public int TotalCost { get; }
   26:     public string ResourceDisplayName { get; }
   27: 
   28:     public bool IsSuccess => Status == EmissaryPurchaseStatus.Success;
   29: 
   30:     public EmissaryPurchaseResult(EmissaryPurchaseStatus status, string troopId, int quantity, int totalCost, string resourceDisplayName)
   31:     {
   32:         Status = status;
   33:         TroopId = troopId;
   34:         Quantity = quantity;
   35:         TotalCost = totalCost;
   36:         ResourceDisplayName = resourceDisplayName;
   37:     }
   38: 
   39:     public static EmissaryPurchaseResult Of(EmissaryPurchaseStatus status, string troopId = null, int quantity = 0, int totalCost = 0, string resourceDisplayName = null) =>
   40:         new(status, troopId, quantity, totalCost, resourceDisplayName);
   41: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\EliteEmissarySettingsProvider.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\IEliteEmissarySettingsProvider.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\IEliteEmissaryConfigProvider.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\EliteEmissary\\EliteEmissaryIoC.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 579ms:
    1: namespace TAOM.Features.EliteEmissary;
    2: 
    3: /// <summary>MCM-over-config-default reads for the Elite Emissary feature. Every read is null-safe
    4: /// against <c>TaomSettings.Instance</c> (null pre-MCM / if MCM fails to load).</summary>
    5: public interface IEliteEmissarySettingsProvider
    6: {
    7:     /// <summary>Master toggle. When false the menu option never appears.</summary>
    8:     bool IsEnabled { get; }
    9: 
   10:     /// <summary>When true, the menu option is hidden at settlements whose owner faction maps to no
   11:     /// special resource. When false the option may show disabled with an explanatory hint.</summary>
   12:     bool HideWhenNoResource { get; }
   13: }

 succeeded in 582ms:
    1: using TAOM.Features;
    2: 
    3: namespace TAOM.Features.EliteEmissary;
    4: 
    5: /// <summary>Merges MCM live values (<c>TaomSettings.Instance</c>) over the config default. Mirrors
    6: /// <c>CastleRecruitmentSettingsProvider</c> — <c>TaomSettings.Instance</c> can be null early in
    7: /// startup / if MCM fails to load, so every read falls back safely.</summary>
    8: public sealed class EliteEmissarySettingsProvider : IEliteEmissarySettingsProvider
    9: {
   10:     private readonly bool _defaultEnabled;
   11: 
   12:     public EliteEmissarySettingsProvider(IEliteEmissaryConfigProvider configProvider)
   13:     {
   14:         _defaultEnabled = configProvider.GetConfig().Enabled;
   15:     }
   16: 
   17:     public bool IsEnabled => TaomSettings.Instance?.EnableEliteEmissary ?? _defaultEnabled;
   18: 
   19:     public bool HideWhenNoResource => TaomSettings.Instance?.HideEmissaryWhenNoResource ?? true;
   20: }

 succeeded in 612ms:
    1: using TAOM.Features.EliteEmissary.Domain;
    2: 
    3: namespace TAOM.Features.EliteEmissary;
    4: 
    5: /// <summary>Loads + validates <c>elite_emissary/elite_emissary_config.xml</c>. Missing/malformed →
    6: /// <see cref="EliteEmissaryConfig.Empty"/>; unknown culture ids and troops lacking a
    7: /// <c>merchant_cost</c> row are dropped with a warning (parse success ≠ validation success).</summary>
    8: public interface IEliteEmissaryConfigProvider
    9: {
   10:     EliteEmissaryConfig GetConfig();
   11: }

 succeeded in 573ms:
    1: using DryIoc;
    2: using TAOM.Adapters;
    3: 
    4: namespace TAOM.Features.EliteEmissary;
    5: 
    6: public static class EliteEmissaryIoC
    7: {
    8:     public static void RegisterEliteEmissaryFeature(IContainer container)
    9:     {
   10:         container.Register<IEliteEmissaryConfigProvider, EliteEmissaryConfigProvider>(Reuse.Singleton);
   11:         container.Register<IEliteEmissarySettingsProvider, EliteEmissarySettingsProvider>(Reuse.Singleton);
   12:         container.Register<IEliteEmissaryService, EliteEmissaryService>(Reuse.Singleton);
   13:         container.Register<ISettlementOwnerAdapter, SettlementOwnerAdapter>(Reuse.Singleton);
   14:         container.Register<IPlayerPartyAdapter, PlayerPartyAdapter>(Reuse.Singleton);
   15:     }
   16: }

codex
The config provider validates culture IDs and prices, but it does not validate troop IDs against `CharacterObject`; the presenter skips unresolved troops at display time. I’m going to cross-reference the shipping XML rather than turn that into a finding unless it can silently dead-end an entire valid-looking offer list.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Adapters\\ISettlementOwnerAdapter.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Adapters\\SettlementOwnerAdapter.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Adapters\\IPlayerPartyAdapter.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Adapters\\PlayerPartyAdapter.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 549ms:
    1: using TaleWorlds.CampaignSystem.Settlements;
    2: 
    3: namespace TAOM.Adapters;
    4: 
    5: /// <summary>TAOM-owned snapshot of a settlement's current owner faction (ADR-007 — keeps sealed
    6: /// <see cref="Settlement"/>/<c>Clan</c>/<c>Kingdom</c> out of services).</summary>
    7: public sealed class SettlementOwnerInfo
    8: {
    9:     public string SettlementId { get; }
   10: 
   11:     /// <summary>Owner kingdom StringId, or null for rebel/minor-faction/unowned settlements.</summary>
   12:     public string OwnerKingdomId { get; }
   13: 
   14:     /// <summary>Owner culture StringId (owner clan's culture, falling back to the settlement's
   15:     /// intrinsic culture for unowned/transient state), or null if unresolvable.</summary>
   16:     public string OwnerCultureId { get; }
   17: 
   18:     public SettlementOwnerInfo(string settlementId, string ownerKingdomId, string ownerCultureId)
   19:     {
   20:         SettlementId = settlementId;
   21:         OwnerKingdomId = ownerKingdomId;
   22:         OwnerCultureId = ownerCultureId;
   23:     }
   24: }
   25: 
   26: /// <summary>Resolves a settlement's current owner faction. <see cref="Settlement.OwnerClan"/> already
   27: /// hops village→bound-town, so a village key-settlement is priced by its bound town's owner.</summary>
   28: public interface ISettlementOwnerAdapter
   29: {
   30:     SettlementOwnerInfo GetOwnerInfo(Settlement settlement);
   31: }

 succeeded in 564ms:
    1: namespace TAOM.Adapters;
    2: 
    3: /// <summary>Boundary over the main party's roster for adding purchased troops (ADR-007 — keeps
    4: /// <c>MobileParty</c>/<c>CharacterObject</c> out of services).</summary>
    5: public interface IPlayerPartyAdapter
    6: {
    7:     /// <summary>The main hero's StringId, or null when there is no main hero.</summary>
    8:     string GetMainHeroId();
    9: 
   10:     /// <summary>Adds <paramref name="count"/> of <paramref name="troopId"/> to the main party roster.
   11:     /// Returns false (no charge should follow) when there is no main party, the troop id doesn't
   12:     /// resolve to a <c>CharacterObject</c>, or the add throws.</summary>
   13:     bool GrantTroop(string troopId, int count);
   14: }

 succeeded in 566ms:
    1: using System;
    2: using TaleWorlds.CampaignSystem;
    3: using TaleWorlds.ObjectSystem;
    4: using TAOM.Core.Logging;
    5: 
    6: namespace TAOM.Adapters;
    7: 
    8: /// <summary>Adds purchased emissary troops to the main party roster (ADR-007). Returns false on any
    9: /// reason a charge must NOT follow — no main party, unresolvable troop id, or an add exception.</summary>
   10: public sealed class PlayerPartyAdapter : IPlayerPartyAdapter
   11: {
   12:     private readonly IModLogger _logger;
   13: 
   14:     public PlayerPartyAdapter(IModLogger logger)
   15:     {
   16:         _logger = logger;
   17:     }
   18: 
   19:     public string GetMainHeroId() => Hero.MainHero?.StringId;
   20: 
   21:     public bool GrantTroop(string troopId, int count)
   22:     {
   23:         if (string.IsNullOrEmpty(troopId) || count <= 0)
   24:             return false;
   25: 
   26:         try
   27:         {
   28:             var roster = Hero.MainHero?.PartyBelongedTo?.MemberRoster;
   29:             if (roster == null)
   30:             {
   31:                 _logger.LogWarning("[EliteEmissary] GrantTroop: no main-party roster (dead hero / no party)");
   32:                 return false;
   33:             }
   34: 
   35:             var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
   36:             if (character == null)
   37:             {
   38:                 _logger.LogError($"[EliteEmissary] GrantTroop: troop id '{troopId}' did not resolve to a CharacterObject");
   39:                 return false;
   40:             }
   41: 
   42:             roster.AddToCounts(character, count);
   43:             _logger.LogInfo($"[EliteEmissary] GrantTroop: added {count}x {troopId} to the main party");
   44:             return true;
   45:         }
   46:         catch (Exception ex)
   47:         {
   48:             _logger.LogError($"[EliteEmissary] GrantTroop('{troopId}' x{count}) failed: {ex.Message}");
   49:             return false;
   50:         }
   51:     }
   52: }

 succeeded in 585ms:
    1: using System;
    2: using TaleWorlds.CampaignSystem.Settlements;
    3: using TAOM.Core.Logging;
    4: 
    5: namespace TAOM.Adapters;
    6: 
    7: /// <summary>
    8: /// Resolves a settlement's current owner faction for the Elite Emissary. <see cref="Settlement.OwnerClan"/>
    9: /// already hops village→bound-town, so village key-settlements price by their bound town's owner.
   10: /// Owner culture prefers the owner clan's culture, falling back to the settlement's intrinsic culture
   11: /// for unowned/just-conquered transient state. All access is null-conditional (adapters.md — computed
   12: /// getters can throw before a plain null check).
   13: /// </summary>
   14: public sealed class SettlementOwnerAdapter : ISettlementOwnerAdapter
   15: {
   16:     private readonly IModLogger _logger;
   17: 
   18:     public SettlementOwnerAdapter(IModLogger logger)
   19:     {
   20:         _logger = logger;
   21:     }
   22: 
   23:     public SettlementOwnerInfo GetOwnerInfo(Settlement settlement)
   24:     {
   25:         if (settlement == null)
   26:             return new SettlementOwnerInfo(null, null, null);
   27: 
   28:         try
   29:         {
   30:             var settlementId = settlement.StringId;
   31:             var ownerClan = settlement.OwnerClan;
   32:             var kingdomId = ownerClan?.Kingdom?.StringId;
   33:             var cultureId = ownerClan?.Culture?.StringId ?? settlement.Culture?.StringId;
   34:             return new SettlementOwnerInfo(settlementId, kingdomId, cultureId);
   35:         }
   36:         catch (Exception ex)
   37:         {
   38:             _logger.LogError($"[EliteEmissary] SettlementOwnerAdapter.GetOwnerInfo('{settlement.StringId}') failed: {ex.Message}");
   39:             return new SettlementOwnerInfo(settlement.StringId, null, null);
   40:         }
   41:     }
   42: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\SpecialResources\\ISpecialResourceService.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\SpecialResources\\Domain\\TroopResourceCostEntry.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\SpecialResources\\SpecialResourceConfigProvider.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\Features\\SpecialResources\\SpecialResourceService.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 564ms:
    1: using System.Collections.Generic;
    2: using TAOM.Features.SpecialResources.Domain;
    3: 
    4: namespace TAOM.Features.SpecialResources;
    5: 
    6: public interface ISpecialResourceService
    7: {
    8:     SpecialResource ResolveResource(string kingdomId, string cultureId);
    9:     float GetCurrentAmount(string heroId, string kingdomId, string cultureId);
   10:     void EarnFromBattle(string heroId, string kingdomId, string cultureId, float enemySizeRatio);
   11:     void EarnFromRaid(string heroId, string kingdomId, string cultureId);
   12:     void EarnFromSiege(string heroId, string kingdomId, string cultureId);
   13:     void EarnFromPrisoners(string heroId, string kingdomId, string cultureId, int prisonerCount);
   14:     void EarnFromTournament(string heroId, string kingdomId, string cultureId);
   15:     void EarnFromHideout(string heroId, string kingdomId, string cultureId);
   16:     void ApplyDailyTick(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
   17:     bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count);
   18:     void SpendForUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count);
   19: 
   20:     /// <summary>
   21:     /// Deducts the one-time recruit cost (<c>recruit_cost</c>) for <paramref name="troopId"/> ×
   22:     /// <paramref name="count"/> from the player's resolved resource. No-op when the troop has no
   23:     /// recruit cost or the hero's kingdom/culture maps to no resource. Charged from
   24:     /// <c>OnUnitRecruitedEvent</c> (player-only) for the elephant/spider volunteers; the
   25:     /// RecruitmentVM gate guarantees affordability before this fires.
   26:     /// </summary>
   27:     void ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count);
   28: 
   29:     /// <summary>
   30:     /// Decides whether the recruit-volunteers cart can be confirmed: sums <c>recruit_cost × count</c>
   31:     /// for cart entries that carry a recruit cost and compares to the player's current resolved-resource
   32:     /// balance. Returns <see cref="RecruitGateResult.Allowed"/> when nothing in the cart costs a resource
   33:     /// or the hero maps to no resource. Used by the RecruitmentVM Done-button gate.
   34:     /// </summary>
   35:     RecruitGateResult CanAffordRecruit(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart);
   36: 
   37:     /// <summary>
   38:     /// True if the hero can afford <c>merchant_cost × count</c> of <paramref name="troopId"/> from the
   39:     /// resource resolved via <paramref name="kingdomId"/>/<paramref name="cultureId"/> — pass the
   40:     /// SETTLEMENT OWNER's faction so the emissary charges the settlement faction's resource (Elite
   41:     /// Emissary feature). Returns true (afford-allow) when count ≤ 0, the troop has no
   42:     /// <c>merchant_cost</c>, or the faction maps to no resource (those cases are gated elsewhere).
   43:     /// Distinct from <see cref="CanAffordRecruit"/>, which reads <c>recruit_cost</c> for the volunteer gate.
   44:     /// </summary>
   45:     bool CanAffordMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count);
   46: 
   47:     /// <summary>
   48:     /// Deducts <c>merchant_cost × count</c> of <paramref name="troopId"/> from the resource resolved via
   49:     /// <paramref name="kingdomId"/>/<paramref name="cultureId"/> (pass the SETTLEMENT OWNER's faction).
   50:     /// No-op when count ≤ 0, the troop has no <c>merchant_cost</c>, or the faction maps to no resource.
   51:     /// Reads <c>merchant_cost</c>, never <c>recruit_cost</c> — the two economies never cross.
   52:     /// </summary>
   53:     void ChargeMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count);
   54: 
   55:     void BeginPartyScreenSession();
   56:     void QueueUpgradeSpend(string heroId, string troopId, int count);
   57:     float GetAvailableAfterPending(string heroId, string kingdomId, string cultureId);
   58:     int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount);
   59:     void CommitSession(string heroId, string kingdomId, string cultureId);
   60:     void CancelSession();
   61:     void InitializeHero(string heroId, string kingdomId, string cultureId);
   62:     float GetDailyEarning(string kingdomId, string cultureId, int ownedTownCount);
   63:     float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep, string heroId = null);
   64: 
   65:     /// <summary>
   66:     /// Projected net resource change (earning − upkeep) for one daily tick, including career
   67:     /// passive modifiers — same math as <see cref="ApplyDailyTick"/>. Returns 0 when the hero's
   68:     /// kingdom/culture maps to no resource. Used to warn the player one tick before a deficit
   69:     /// (and the troop desertion it triggers).
   70:     /// </summary>
   71:     float GetProjectedDailyNet(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
   72:     IReadOnlyList<TroopDesertionEntry> CalculateDesertion(string heroId, string kingdomId, string cultureId, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
   73: 
   74:     /// <summary>
   75:     /// Returns the highest <see cref="ResourceTier"/> the hero has reached,
   76:     /// or <c>null</c> if the resource has no tiers or the hero is below all thresholds.
   77:     /// </summary>
   78:     ResourceTier GetCurrentTier(string heroId, string kingdomId, string cultureId);
   79: 
   80:     /// <summary>
   81:     /// Returns the current tier level (1-N), or 0 if below all thresholds or no tiers defined.
   82:     /// </summary>
   83:     int GetCurrentTierLevel(string heroId, string kingdomId, string cultureId);
   84: 
   85:     /// <summary>
   86:     /// Clears singleton-scope session state that must not leak across new campaigns started in the
   87:     /// same process: pending PartyScreen spend, in-session flag, and the dedupe set for resolve
   88:     /// debug logging. Wired to <c>OnNewGameCreatedEvent</c> by the behavior. Phase 9b deferred #133 P2 R1.
   89:     /// </summary>
   90:     void ResetSessionState();
   91: }
   92: 
   93: public sealed class TroopUpkeepInfo
   94: {
   95:     public string TroopId { get; }
   96:     public int Count { get; }
   97: 
   98:     public TroopUpkeepInfo(string troopId, int count)
   99:     {
  100:         TroopId = troopId;
  101:         Count = count;
  102:     }
  103: }
  104: 
  105: public sealed class TroopDesertionEntry
  106: {
  107:     public string TroopId { get; }
  108:     public int DesertCount { get; }
  109: 
  110:     public TroopDesertionEntry(string troopId, int desertCount)
  111:     {
  112:         TroopId = troopId;
  113:         DesertCount = desertCount;
  114:     }
  115: }
  116: 
  117: /// <summary>One recruit-screen cart line: a troop id and how many copies of it are in the cart.</summary>
  118: public sealed class RecruitCartEntry
  119: {
  120:     public string TroopId { get; }
  121:     public int Count { get; }
  122: 
  123:     public RecruitCartEntry(string troopId, int count)
  124:     {
  125:         TroopId = troopId;
  126:         Count = count;
  127:     }
  128: }
  129: 
  130: /// <summary>Outcome of <see cref="ISpecialResourceService.CanAffordRecruit"/> — whether to block the
  131: /// Done button and, if so, the resource amount/name to surface in the hint.</summary>
  132: public sealed class RecruitGateResult
  133: {
  134:     public bool Blocked { get; }
  135:     public int Required { get; }
  136:     public string ResourceDisplayName { get; }
  137: 
  138:     public static readonly RecruitGateResult Allowed = new(false, 0, null);
  139: 
  140:     public RecruitGateResult(bool blocked, int required, string resourceDisplayName)
  141:     {
  142:         Blocked = blocked;
  143:         Required = required;
  144:         ResourceDisplayName = resourceDisplayName;
  145:     }
  146: }

 succeeded in 577ms:
    1: namespace TAOM.Features.SpecialResources.Domain;
    2: 
    3: public sealed class TroopResourceCostEntry
    4: {
    5:     public string TroopId { get; }
    6:     public string ResourceId { get; }
    7:     public int UpgradeCost { get; }
    8:     public float DailyUpkeep { get; }
    9: 
   10:     // One-time cost charged when this troop is RECRUITED as a volunteer (not upgraded into).
   11:     // Distinct from UpgradeCost so a troop that is both an upgrade target and a recruitable
   12:     // volunteer can't be double-charged. Consumed by the RecruitmentVM gate + OnUnitRecruited
   13:     // deduction; 0 means "no recruit cost". The recruited resource is the player's resolved
   14:     // resource, not ResourceId (which is documentation only).
   15:     public int RecruitCost { get; }
   16: 
   17:     // One-time cost charged when the Elite Emissary feature SELLS this troop from a faction's key
   18:     // settlement. Deliberately distinct from RecruitCost so an elite that is also a recruitable
   19:     // volunteer is never double-charged across the two economies — the volunteer gate reads
   20:     // RecruitCost, the emissary reads MerchantCost. 0 means "not offered by the emissary". The
   21:     // charged resource is the SETTLEMENT OWNER's resolved resource (passed by the caller), not
   22:     // ResourceId (documentation only). See docs/features/elite-emissary.md.
   23:     public int MerchantCost { get; }
   24: 
   25:     public TroopResourceCostEntry(string troopId, string resourceId, int upgradeCost, float dailyUpkeep, int recruitCost = 0, int merchantCost = 0)
   26:     {
   27:         TroopId = troopId;
   28:         ResourceId = resourceId;
   29:         UpgradeCost = upgradeCost;
   30:         DailyUpkeep = dailyUpkeep;
   31:         RecruitCost = recruitCost;
   32:         MerchantCost = merchantCost;
   33:     }
   34: }

 succeeded in 601ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using TAOM.Core.Logging;
    4: using TAOM.Features.CareerSystem;
    5: using TAOM.Features.CareerSystem.Domain;
    6: using TAOM.Features.SpecialResources.Domain;
    7: 
    8: namespace TAOM.Features.SpecialResources;
    9: 
   10: public class SpecialResourceService : ISpecialResourceService
   11: {
   12:     private readonly ISpecialResourceConfigProvider _config;
   13:     private readonly ISpecialResourceStorageService _storage;
   14:     private readonly IModLogger _logger;
   15:     private readonly ICareerPassiveService _passiveService;
   16:     private readonly HashSet<string> _loggedResolveKeys = new();
   17:     private float _pendingSpend;
   18:     private bool _inSession;
   19: 
   20:     public SpecialResourceService(ISpecialResourceConfigProvider config, ISpecialResourceStorageService storage, IModLogger logger, ICareerPassiveService passiveService = null)
   21:     {
   22:         _config = config;
   23:         _storage = storage;
   24:         _logger = logger;
   25:         _passiveService = passiveService;
   26:     }
   27: 
   28:     public SpecialResource ResolveResource(string kingdomId, string cultureId)
   29:     {
   30:         // Resolve is hot-path (called from MapInfoVM.OnRefresh tooltip rebuild several times per tick).
   31:         // Dedupe DEBUG logging by (kingdomId, cultureId) so we keep diagnostics on transitions
   32:         // without flooding the log with thousands of identical lines per session.
   33:         var key = (kingdomId ?? "") + "|" + (cultureId ?? "");
   34:         var firstSeen = _loggedResolveKeys.Add(key);
   35: 
   36:         if (kingdomId != null)
   37:         {
   38:             var byKingdom = _config.GetByKingdomId(kingdomId);
   39:             if (byKingdom != null)
   40:             {
   41:                 if (firstSeen)
   42:                     _logger.LogDebug($"[SpecRes] Resolved resource '{byKingdom.Id}' via kingdom '{kingdomId}'");
   43:                 return byKingdom;
   44:             }
   45:         }
   46:         if (cultureId != null)
   47:         {
   48:             var byCulture = _config.GetByCultureId(cultureId);
   49:             if (byCulture != null)
   50:             {
   51:                 if (firstSeen)
   52:                     _logger.LogDebug($"[SpecRes] Resolved resource '{byCulture.Id}' via culture '{cultureId}' (kingdom '{kingdomId}' had no match)");
   53:                 return byCulture;
   54:             }
   55:         }
   56:         if (firstSeen)
   57:             _logger.LogDebug($"[SpecRes] No resource resolved for kingdom='{kingdomId}', culture='{cultureId}'");
   58:         return null;
   59:     }
   60: 
   61:     public float GetCurrentAmount(string heroId, string kingdomId, string cultureId)
   62:     {
   63:         var resource = ResolveResource(kingdomId, cultureId);
   64:         if (resource == null) return 0f;
   65:         return _storage.Get(heroId, resource.Id);
   66:     }
   67: 
   68:     public void EarnFromBattle(string heroId, string kingdomId, string cultureId, float enemySizeRatio)
   69:     {
   70:         var resource = ResolveResource(kingdomId, cultureId);
   71:         if (resource == null) return;
   72: 
   73:         var clampedRatio = Math.Max(0.5f, Math.Min(2f, enemySizeRatio));
   74:         var amount = resource.PerBattleVictoryBase * clampedRatio;
   75:         var before = _storage.Get(heroId, resource.Id);
   76:         AddCapped(heroId, resource, amount);
   77:         var after = _storage.Get(heroId, resource.Id);
   78:         _logger.LogInfo($"[SpecRes] BATTLE: +{amount:F1} {resource.DisplayName} (ratio {enemySizeRatio:F2}→{clampedRatio:F2}) | {before:F0}→{after:F0}");
   79:     }
   80: 
   81:     public void EarnFromRaid(string heroId, string kingdomId, string cultureId)
   82:     {
   83:         var resource = ResolveResource(kingdomId, cultureId);
   84:         if (resource == null) return;
   85: 
   86:         var before = _storage.Get(heroId, resource.Id);
   87:         AddCapped(heroId, resource, resource.PerRaid);
   88:         var after = _storage.Get(heroId, resource.Id);
   89:         _logger.LogInfo($"[SpecRes] RAID: +{resource.PerRaid:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
   90:     }
   91: 
   92:     public void EarnFromSiege(string heroId, string kingdomId, string cultureId)
   93:     {
   94:         var resource = ResolveResource(kingdomId, cultureId);
   95:         if (resource == null) return;
   96: 
   97:         var before = _storage.Get(heroId, resource.Id);
   98:         AddCapped(heroId, resource, resource.PerSiegeVictory);
   99:         var after = _storage.Get(heroId, resource.Id);
  100:         _logger.LogInfo($"[SpecRes] SIEGE: +{resource.PerSiegeVictory:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
  101:     }
  102: 
  103:     public void EarnFromPrisoners(string heroId, string kingdomId, string cultureId, int prisonerCount)
  104:     {
  105:         var resource = ResolveResource(kingdomId, cultureId);
  106:         if (resource == null) return;
  107: 
  108:         var earned = resource.PerPrisoner * prisonerCount;
  109:         var before = _storage.Get(heroId, resource.Id);
  110:         AddCapped(heroId, resource, earned);
  111:         var after = _storage.Get(heroId, resource.Id);
  112:         _logger.LogInfo($"[SpecRes] PRISONERS: +{earned:F0} {resource.DisplayName} ({prisonerCount} captured) | {before:F0}→{after:F0}");
  113:     }
  114: 
  115:     public void EarnFromTournament(string heroId, string kingdomId, string cultureId)
  116:     {
  117:         var resource = ResolveResource(kingdomId, cultureId);
  118:         if (resource == null) return;
  119: 
  120:         var before = _storage.Get(heroId, resource.Id);
  121:         AddCapped(heroId, resource, resource.PerTournamentWin);
  122:         var after = _storage.Get(heroId, resource.Id);
  123:         _logger.LogInfo($"[SpecRes] TOURNAMENT: +{resource.PerTournamentWin:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
  124:     }
  125: 
  126:     public void EarnFromHideout(string heroId, string kingdomId, string cultureId)
  127:     {
  128:         var resource = ResolveResource(kingdomId, cultureId);
  129:         if (resource == null) return;
  130: 
  131:         var before = _storage.Get(heroId, resource.Id);
  132:         AddCapped(heroId, resource, resource.PerHideoutClear);
  133:         var after = _storage.Get(heroId, resource.Id);
  134:         _logger.LogInfo($"[SpecRes] HIDEOUT: +{resource.PerHideoutClear:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
  135:     }
  136: 
  137:     public void ApplyDailyTick(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
  138:     {
  139:         var resource = ResolveResource(kingdomId, cultureId);
  140:         if (resource == null) return;
  141: 
  142:         var net = ComputeDailyNet(heroId, resource, ownedTownCount, troopsWithUpkeep);
  143:         var before = _storage.Get(heroId, resource.Id);
  144: 
  145:         if (net >= 0)
  146:             AddCapped(heroId, resource, net);
  147:         else
  148:             _storage.Add(heroId, resource.Id, net);
  149: 
  150:         var after = _storage.Get(heroId, resource.Id);
  151:         _logger.LogDebug($"[SpecRes] DAILY: net={net:+0.0;-0.0} ({ownedTownCount} towns) | {before:F0}→{after:F0}");
  152:     }
  153: 
  154:     public float GetProjectedDailyNet(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
  155:     {
  156:         var resource = ResolveResource(kingdomId, cultureId);
  157:         if (resource == null) return 0f;
  158:         return ComputeDailyNet(heroId, resource, ownedTownCount, troopsWithUpkeep);
  159:     }
  160: 
  161:     // Single source of truth for the daily earning(+CustomResourceGain) − upkeep(+CustomResourceUpkeepModifier)
  162:     // math, shared by ApplyDailyTick (which applies it) and GetProjectedDailyNet (which projects the next tick).
  163:     private float ComputeDailyNet(string heroId, SpecialResource resource, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
  164:     {
  165:         var earning = resource.DailyPerTown * ownedTownCount;
  166:         var gainModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceGain);
  167:         if (gainModifier != 0f)
  168:             earning *= (1f + gainModifier);
  169: 
  170:         var upkeep = GetDailyUpkeep(troopsWithUpkeep, heroId);
  171:         return earning - upkeep;
  172:     }
  173: 
  174:     public bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
  175:     {
  176:         var resource = ResolveResource(kingdomId, cultureId);
  177:         if (resource == null) return true;
  178: 
  179:         var cost = _config.GetTroopCost(troopId);
  180:         if (cost == null) return true;
  181: 
  182:         var totalCost = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
  183:         var available = _storage.Get(heroId, resource.Id);
  184:         var canAfford = available >= totalCost;
  185:         _logger.LogDebug($"[SpecRes] CanAfford: {troopId} x{count} cost={totalCost} available={available:F0} → {canAfford}");
  186:         return canAfford;
  187:     }
  188: 
  189:     public void SpendForUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
  190:     {
  191:         var resource = ResolveResource(kingdomId, cultureId);
  192:         if (resource == null) return;
  193: 
  194:         var cost = _config.GetTroopCost(troopId);
  195:         if (cost == null) return;
  196: 
  197:         var totalCost = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
  198:         _storage.Add(heroId, resource.Id, -totalCost);
  199:         _logger.LogInfo($"[SpecRes] SPEND: -{totalCost} {resource.DisplayName} for {troopId} x{count}");
  200:     }
  201: 
  202:     public void ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count)
  203:     {
  204:         if (count <= 0) return;
  205: 
  206:         var resource = ResolveResource(kingdomId, cultureId);
  207:         if (resource == null) return;
  208: 
  209:         var cost = _config.GetTroopCost(troopId);
  210:         if (cost == null || cost.RecruitCost <= 0) return;
  211: 
  212:         var totalCost = cost.RecruitCost * count;
  213:         _storage.Add(heroId, resource.Id, -totalCost);
  214:         _logger.LogInfo($"[SpecRes] RECRUIT: -{totalCost} {resource.DisplayName} for {troopId} x{count}");
  215:     }
  216: 
  217:     public RecruitGateResult CanAffordRecruit(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart)
  218:     {
  219:         if (cart == null || cart.Count == 0) return RecruitGateResult.Allowed;
  220: 
  221:         var resource = ResolveResource(kingdomId, cultureId);
  222:         if (resource == null) return RecruitGateResult.Allowed;
  223: 
  224:         var required = 0;
  225:         foreach (var entry in cart)
  226:         {
  227:             if (entry == null || entry.Count <= 0) continue;
  228:             var cost = _config.GetTroopCost(entry.TroopId);
  229:             if (cost == null || cost.RecruitCost <= 0) continue;
  230:             required += cost.RecruitCost * entry.Count;
  231:         }
  232: 
  233:         if (required <= 0) return RecruitGateResult.Allowed;
  234: 
  235:         var available = _storage.Get(heroId, resource.Id);
  236:         var blocked = available < required;
  237:         if (blocked)
  238:             _logger.LogDebug($"[SpecRes] RECRUIT GATE: blocked (need {required} {resource.DisplayName}, have {available:F0})");
  239: 
  240:         return new RecruitGateResult(blocked, required, resource.DisplayName);
  241:     }
  242: 
  243:     public bool CanAffordMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
  244:     {
  245:         if (count <= 0) return true;
  246: 
  247:         var resource = ResolveResource(kingdomId, cultureId);
  248:         if (resource == null) return true;
  249: 
  250:         var cost = _config.GetTroopCost(troopId);
  251:         if (cost == null || cost.MerchantCost <= 0) return true;
  252: 
  253:         var totalCost = cost.MerchantCost * count;
  254:         var available = _storage.Get(heroId, resource.Id);
  255:         var canAfford = available >= totalCost;
  256:         _logger.LogDebug($"[SpecRes] CanAffordMerchant: {troopId} x{count} cost={totalCost} {resource.DisplayName} available={available:F0} → {canAfford}");
  257:         return canAfford;
  258:     }
  259: 
  260:     public void ChargeMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
  261:     {
  262:         if (count <= 0) return;
  263: 
  264:         var resource = ResolveResource(kingdomId, cultureId);
  265:         if (resource == null)
  266:         {
  267:             _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} x{count} — kingdom='{kingdomId}' culture='{cultureId}' maps to no resource");
  268:             return;
  269:         }
  270: 
  271:         var cost = _config.GetTroopCost(troopId);
  272:         if (cost == null || cost.MerchantCost <= 0)
  273:         {
  274:             _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} has no merchant_cost");
  275:             return;
  276:         }
  277: 
  278:         var totalCost = cost.MerchantCost * count;
  279:         _storage.Add(heroId, resource.Id, -totalCost);
  280:         _logger.LogInfo($"[SpecRes] MERCHANT: -{totalCost} {resource.DisplayName} for {troopId} x{count} (balance now {_storage.Get(heroId, resource.Id):F0})");
  281:     }
  282: 
  283:     public void BeginPartyScreenSession()
  284:     {
  285:         _pendingSpend = 0f;
  286:         _inSession = true;
  287:         _logger.LogDebug("[SpecRes] PartyScreen session BEGUN");
  288:     }
  289: 
  290:     public void QueueUpgradeSpend(string heroId, string troopId, int count)
  291:     {
  292:         var cost = _config.GetTroopCost(troopId);
  293:         if (cost == null) return;
  294: 
  295:         // Phase 9b #174: apply the career-passive CustomResourceUpgradeCostModifier here too.
  296:         // Pre-fix this queued base cost while ClampUpgradeCount + SpendForUpgrade used the
  297:         // discounted cost — so a player with a -30% career discount got the cheaper *count* but
  298:         // was debited the full price at CommitSession, silently overpaying by the discount %.
  299:         var added = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
  300:         _pendingSpend += added;
  301:         _logger.LogDebug($"[SpecRes] QUEUED: {troopId} x{count} = {added} pending (total pending={_pendingSpend:F0})");
  302:     }
  303: 
  304:     public float GetAvailableAfterPending(string heroId, string kingdomId, string cultureId)
  305:     {
  306:         var resource = ResolveResource(kingdomId, cultureId);
  307:         if (resource == null) return 0f;
  308:         return _storage.Get(heroId, resource.Id) - _pendingSpend;
  309:     }
  310: 
  311:     public int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount)
  312:     {
  313:         var cost = _config.GetTroopCost(troopId);
  314:         if (cost == null || cost.UpgradeCost <= 0) return requestedCount;
  315: 
  316:         var effectivePerUnit = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, 1);
  317:         if (effectivePerUnit <= 0) return requestedCount;
  318: 
  319:         var available = GetAvailableAfterPending(heroId, kingdomId, cultureId);
  320:         var maxAffordable = (int)(available / effectivePerUnit);
  321:         var clamped = Math.Max(0, Math.Min(requestedCount, maxAffordable));
  322: 
  323:         if (clamped < requestedCount)
  324:             _logger.LogDebug($"[SpecRes] CLAMP: {troopId} requested={requestedCount} clamped={clamped} (available={available:F0}, cost/unit={cost.UpgradeCost})");
  325: 
  326:         return clamped;
  327:     }
  328: 
  329:     public void CommitSession(string heroId, string kingdomId, string cultureId)
  330:     {
  331:         if (!_inSession) return;
  332: 
  333:         if (_pendingSpend > 0f)
  334:         {
  335:             var resource = ResolveResource(kingdomId, cultureId);
  336:             if (resource != null)
  337:             {
  338:                 _storage.Add(heroId, resource.Id, -_pendingSpend);
  339:                 _logger.LogInfo($"[SpecRes] PartyScreen COMMITTED: -{_pendingSpend:F0} {resource.DisplayName}");
  340:             }
  341:         }
  342:         else
  343:         {
  344:             _logger.LogDebug("[SpecRes] PartyScreen COMMITTED: no pending spend");
  345:         }
  346: 
  347:         _pendingSpend = 0f;
  348:         _inSession = false;
  349:     }
  350: 
  351:     public void CancelSession()
  352:     {
  353:         var wasPending = _pendingSpend;
  354:         _pendingSpend = 0f;
  355:         _inSession = false;
  356:         _logger.LogDebug($"[SpecRes] PartyScreen CANCELLED: discarded {wasPending:F0} pending spend");
  357:     }
  358: 
  359:     public void ResetSessionState()
  360:     {
  361:         // Phase 9b #133 P2 R1 — clear singleton-scope state on new-campaign boundary so the
  362:         // second campaign in the same process doesn't inherit:
  363:         //   - _inSession=true from a prior session (would let a stale CommitSession debit
  364:         //     the new hero's balance against the old pending amount)
  365:         //   - _pendingSpend>0 (would be applied at the next legitimate CommitSession)
  366:         //   - _loggedResolveKeys (so the first resolve of every new (kingdom,culture)
  367:         //     pair in the new campaign still surfaces a single diagnostic line)
  368:         var hadPending = _pendingSpend;
  369:         var wasInSession = _inSession;
  370:         _pendingSpend = 0f;
  371:         _inSession = false;
  372:         _loggedResolveKeys.Clear();
  373:         _logger.LogInfo($"[SpecRes] ResetSessionState: cleared (pending was {hadPending:F0}, inSession was {wasInSession})");
  374:     }
  375: 
  376:     public void InitializeHero(string heroId, string kingdomId, string cultureId)
  377:     {
  378:         var resource = ResolveResource(kingdomId, cultureId);
  379:         if (resource == null)
  380:         {
  381:             _logger.LogWarning($"[SpecRes] InitializeHero: no resource for kingdom='{kingdomId}', culture='{cultureId}'");
  382:             return;
  383:         }
  384: 
  385:         _storage.Set(heroId, resource.Id, resource.StartingAmount);
  386:         _logger.LogInfo($"[SpecRes] InitializeHero: {heroId} → {resource.DisplayName} = {resource.StartingAmount}");
  387:     }
  388: 
  389:     public float GetDailyEarning(string kingdomId, string cultureId, int ownedTownCount)
  390:     {
  391:         var resource = ResolveResource(kingdomId, cultureId);
  392:         if (resource == null) return 0f;
  393: 
  394:         return resource.DailyPerTown * ownedTownCount;
  395:     }
  396: 
  397:     public float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep, string heroId = null)
  398:     {
  399:         var total = 0f;
  400:         if (troopsWithUpkeep == null) return total;
  401: 
  402:         foreach (var troop in troopsWithUpkeep)
  403:         {
  404:             var cost = _config.GetTroopCost(troop.TroopId);
  405:             if (cost != null)
  406:                 total += cost.DailyUpkeep * troop.Count;
  407:         }
  408: 
  409:         var upkeepModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceUpkeepModifier);
  410:         if (upkeepModifier != 0f)
  411:             total *= (1f + upkeepModifier);
  412: 
  413:         return Math.Max(0f, total);
  414:     }
  415: 
  416:     public IReadOnlyList<TroopDesertionEntry> CalculateDesertion(string heroId, string kingdomId, string cultureId, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
  417:     {
  418:         var result = new List<TroopDesertionEntry>();
  419: 
  420:         var resource = ResolveResource(kingdomId, cultureId);
  421:         if (resource == null || troopsWithUpkeep == null || troopsWithUpkeep.Count == 0)
  422:             return result;
  423: 
  424:         var balance = _storage.Get(heroId, resource.Id);
  425:         if (balance > 0f)
  426:             return result;
  427: 
  428:         // At 0 resources: 10% of each upkeep troop type deserts per day (min 1)
  429:         foreach (var troop in troopsWithUpkeep)
  430:         {
  431:             var desertCount = Math.Max(1, (int)(troop.Count * 0.1f));
  432:             desertCount = Math.Min(desertCount, troop.Count);
  433:             result.Add(new TroopDesertionEntry(troop.TroopId, desertCount));
  434:         }
  435: 
  436:         if (result.Count > 0)
  437:         {
  438:             var totalDeserted = 0;
  439:             foreach (var entry in result)
  440:                 totalDeserted += entry.DesertCount;
  441:             _logger.LogInfo($"[SpecRes] DESERTION: {totalDeserted} elite troops deserting (balance={balance:F0}, {result.Count} troop types affected)");
  442:         }
  443: 
  444:         return result;
  445:     }
  446: 
  447:     public ResourceTier GetCurrentTier(string heroId, string kingdomId, string cultureId)
  448:     {
  449:         var resource = ResolveResource(kingdomId, cultureId);
  450:         if (resource == null || resource.TierThresholds.Count == 0)
  451:             return null;
  452: 
  453:         var amount = _storage.Get(heroId, resource.Id);
  454: 
  455:         // Walk from highest tier to lowest; return first one whose threshold is met
  456:         for (var i = resource.TierThresholds.Count - 1; i >= 0; i--)
  457:         {
  458:             if (amount >= resource.TierThresholds[i].Threshold)
  459:                 return resource.TierThresholds[i];
  460:         }
  461: 
  462:         return null;
  463:     }
  464: 
  465:     public int GetCurrentTierLevel(string heroId, string kingdomId, string cultureId)
  466:     {
  467:         var tier = GetCurrentTier(heroId, kingdomId, cultureId);
  468:         return tier?.Level ?? 0;
  469:     }
  470: 
  471:     private void AddCapped(string heroId, SpecialResource resource, float amount)
  472:     {
  473:         var current = _storage.Get(heroId, resource.Id);
  474:         var newAmount = Math.Min(current + amount, resource.Cap);
  475:         _storage.Set(heroId, resource.Id, newAmount);
  476:     }
  477: 
  478:     private float GetEffectiveUpgradeCost(string heroId, float baseCostPerUnit, int count)
  479:     {
  480:         var totalCost = baseCostPerUnit * count;
  481:         var costModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceUpgradeCostModifier);
  482:         if (costModifier != 0f)
  483:             totalCost *= (1f + costModifier);
  484:         return Math.Max(0f, totalCost);
  485:     }
  486: 
  487:     private float GetPassiveMagnitude(string heroId, PassiveEffectType type)
  488:     {
  489:         if (_passiveService == null || heroId == null) return 0f;
  490:         return _passiveService.GetPassiveMagnitude(heroId, type);
  491:     }
  492: }

 succeeded in 611ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.Globalization;
    4: using System.IO;
    5: using System.Linq;
    6: using System.Xml.Linq;
    7: using TAOM.Core.Infrastructure;
    8: using TAOM.Core.Logging;
    9: using TAOM.Features.SpecialResources.Domain;
   10: 
   11: namespace TAOM.Features.SpecialResources;
   12: 
   13: public class SpecialResourceConfigProvider : ISpecialResourceConfigProvider
   14: {
   15:     private readonly IPathService _pathService;
   16:     private readonly IModLogger _logger;
   17:     private List<SpecialResource> _resources;
   18:     private Dictionary<string, SpecialResource> _byKingdom;
   19:     private Dictionary<string, SpecialResource> _byCulture;
   20:     private Dictionary<string, TroopResourceCostEntry> _troopCosts;
   21: 
   22:     public SpecialResourceConfigProvider(IPathService pathService, IModLogger logger)
   23:     {
   24:         _pathService = pathService;
   25:         _logger = logger;
   26:     }
   27: 
   28:     public IReadOnlyList<SpecialResource> GetAllResources()
   29:     {
   30:         EnsureLoaded();
   31:         return _resources;
   32:     }
   33: 
   34:     public SpecialResource GetByKingdomId(string kingdomId)
   35:     {
   36:         EnsureLoaded();
   37:         return kingdomId != null && _byKingdom.TryGetValue(kingdomId, out var resource) ? resource : null;
   38:     }
   39: 
   40:     public SpecialResource GetByCultureId(string cultureId)
   41:     {
   42:         EnsureLoaded();
   43:         return cultureId != null && _byCulture.TryGetValue(cultureId, out var resource) ? resource : null;
   44:     }
   45: 
   46:     public TroopResourceCostEntry GetTroopCost(string troopId)
   47:     {
   48:         EnsureLoaded();
   49:         return _troopCosts.TryGetValue(troopId, out var entry) ? entry : null;
   50:     }
   51: 
   52:     private void EnsureLoaded()
   53:     {
   54:         if (_resources != null)
   55:             return;
   56: 
   57:         _resources = new List<SpecialResource>();
   58:         _byKingdom = new Dictionary<string, SpecialResource>();
   59:         _byCulture = new Dictionary<string, SpecialResource>();
   60:         _troopCosts = new Dictionary<string, TroopResourceCostEntry>();
   61: 
   62:         LoadResources();
   63:         LoadTroopCosts();
   64:     }
   65: 
   66:     private void LoadResources()
   67:     {
   68:         var path = Path.Combine(_pathService.ModuleDataPath, "special_resources", "special_resources_config.xml");
   69: 
   70:         if (!File.Exists(path))
   71:         {
   72:             _logger.LogWarning($"SpecialResourceConfigProvider: Config not found: {path}");
   73:             return;
   74:         }
   75: 
   76:         try
   77:         {
   78:             var doc = XDocument.Load(path);
   79:             foreach (var el in doc.Root.Elements("Resource"))
   80:             {
   81:                 var kingdomIds = new List<string>();
   82:                 foreach (var k in el.Elements("Kingdom"))
   83:                 {
   84:                     var kid = k.Attribute("id")?.Value;
   85:                     if (!string.IsNullOrEmpty(kid))
   86:                         kingdomIds.Add(kid);
   87:                 }
   88: 
   89:                 var cultureIds = new List<string>();
   90:                 foreach (var c in el.Elements("Culture"))
   91:                 {
   92:                     var cid = c.Attribute("id")?.Value;
   93:                     if (!string.IsNullOrEmpty(cid))
   94:                         cultureIds.Add(cid);
   95:                 }
   96: 
   97:                 var tiers = ParseTiers(el);
   98: 
   99:                 var resource = new SpecialResource(
  100:                     id: el.Attribute("id")?.Value ?? "",
  101:                     kingdomIds: kingdomIds,
  102:                     cultureIds: cultureIds,
  103:                     displayName: el.Attribute("display_name")?.Value ?? "",
  104:                     iconSpriteName: el.Attribute("icon_sprite")?.Value ?? "",
  105:                     cap: ParseFloat(el, "cap", 100f),
  106:                     startingAmount: ParseFloat(el, "starting_amount", 0f),
  107:                     dailyPerTown: ParseFloat(el, "daily_per_town", 0f),
  108:                     perBattleVictoryBase: ParseFloat(el, "per_battle_victory_base", 0f),
  109:                     perRaid: ParseFloat(el, "per_raid", 0f),
  110:                     perSiegeVictory: ParseFloat(el, "per_siege_victory", 0f),
  111:                     perPrisoner: ParseFloat(el, "per_prisoner", 0f),
  112:                     perTournamentWin: ParseFloat(el, "per_tournament_win", 0f),
  113:                     perHideoutClear: ParseFloat(el, "per_hideout_clear", 0f),
  114:                     tierThresholds: tiers);
  115: 
  116:                 _resources.Add(resource);
  117: 
  118:                 foreach (var kid in kingdomIds)
  119:                     _byKingdom[kid] = resource;
  120:                 foreach (var cid in cultureIds)
  121:                     _byCulture[cid] = resource;
  122:             }
  123: 
  124:             _logger.LogInfo($"SpecialResourceConfigProvider: Loaded {_resources.Count} resource definitions");
  125:         }
  126:         catch (Exception ex)
  127:         {
  128:             _logger.LogError($"SpecialResourceConfigProvider: Failed to parse resources: {ex.Message}");
  129:         }
  130:     }
  131: 
  132:     private void LoadTroopCosts()
  133:     {
  134:         var path = Path.Combine(_pathService.ModuleDataPath, "special_resources", "troop_resource_costs.xml");
  135: 
  136:         if (!File.Exists(path))
  137:         {
  138:             _logger.LogWarning($"SpecialResourceConfigProvider: Troop costs not found: {path}");
  139:             return;
  140:         }
  141: 
  142:         try
  143:         {
  144:             var doc = XDocument.Load(path);
  145:             foreach (var el in doc.Root.Elements("Troop"))
  146:             {
  147:                 var entry = new TroopResourceCostEntry(
  148:                     troopId: el.Attribute("id")?.Value ?? "",
  149:                     resourceId: el.Attribute("resource_id")?.Value ?? "",
  150:                     upgradeCost: (int)ParseFloat(el, "upgrade_cost", 0f),
  151:                     dailyUpkeep: ParseFloat(el, "daily_upkeep", 0f),
  152:                     recruitCost: (int)ParseFloat(el, "recruit_cost", 0f),
  153:                     merchantCost: (int)ParseFloat(el, "merchant_cost", 0f));
  154: 
  155:                 if (!string.IsNullOrEmpty(entry.TroopId))
  156:                     _troopCosts[entry.TroopId] = entry;
  157:             }
  158: 
  159:             _logger.LogInfo($"SpecialResourceConfigProvider: Loaded {_troopCosts.Count} troop cost entries");
  160:         }
  161:         catch (Exception ex)
  162:         {
  163:             _logger.LogError($"SpecialResourceConfigProvider: Failed to parse troop costs: {ex.Message}");
  164:         }
  165:     }
  166: 
  167:     private static List<ResourceTier> ParseTiers(XElement resourceEl)
  168:     {
  169:         var tiersEl = resourceEl.Element("Tiers");
  170:         if (tiersEl == null)
  171:             return new List<ResourceTier>();
  172: 
  173:         var tiers = new List<ResourceTier>();
  174:         foreach (var tierEl in tiersEl.Elements("Tier"))
  175:         {
  176:             var levelStr = tierEl.Attribute("level")?.Value;
  177:             var name = tierEl.Attribute("name")?.Value ?? "";
  178:             var thresholdStr = tierEl.Attribute("threshold")?.Value;
  179:             var description = tierEl.Attribute("description")?.Value ?? "";
  180: 
  181:             if (levelStr == null || thresholdStr == null)
  182:                 continue;
  183: 
  184:             if (!int.TryParse(levelStr, out var level))
  185:                 continue;
  186: 
  187:             if (!float.TryParse(thresholdStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
  188:                 continue;
  189: 
  190:             tiers.Add(new ResourceTier(level, name, threshold, description));
  191:         }
  192: 
  193:         tiers.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));
  194:         return tiers;
  195:     }
  196: 
  197:     private static float ParseFloat(XElement el, string attr, float defaultValue)
  198:     {
  199:         var val = el.Attribute(attr)?.Value;
  200:         if (val == null) return defaultValue;
  201:         // Phase 9b #133 P1 — was `float.Parse` (throws on malformed value, bubbles to outer catch
  202:         // → silently zeroes ALL resources for the file). Use TryParse + NaN/Infinity guard.
  203:         if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
  204:             return defaultValue;
  205:         if (float.IsNaN(result) || float.IsInfinity(result))
  206:             return defaultValue;
  207:         return result;
  208:     }
  209: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\_Module\\ModuleData\\elite_emissary\\elite_emissary_config.xml | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\_Module\\ModuleData\\special_resources\\troop_resource_costs.xml | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\_Module\\ModuleData\\special_resources\\special_resources_config.xml | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 567ms:
    1: <?xml version="1.0" encoding="utf-8"?>
    2: <TroopResourceCosts>
    3:   <!-- Mordor T6+ elite troops require Scraps to upgrade and maintain -->
    4:   <!-- Melee line -->
    5:   <!-- Ranged line -->
    6:   <!-- Shield line -->
    7:   <Troop id="mordor_uruk_vanguard" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="0.1" />
    8:   <Troop id="mordor_uruk_shieldguard" resource_id="war_spoils" upgrade_cost="3" daily_upkeep="0.15" />
    9:   <!-- Ranged line (#212 KEYforce revamp Black Uruk crossbow/heavy variants) -->
   10:   <Troop id="mordor_uruk_crossbow" resource_id="war_spoils" upgrade_cost="1" daily_upkeep="0.05" />
   11:   <Troop id="mordor_uruk_heavy_crossbow" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="0.1" />
   12:   <Troop id="mordor_uruk_heavy_archer" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="0.1" />
   13:   <!-- Cavalry (Nurn Warg Riders top-tier) -->
   14:   <Troop id="mordor_warg_beastmaster" resource_id="war_spoils" upgrade_cost="1" daily_upkeep="0.05" />
   15:   <!-- Command line -->
   16:   <Troop id="mordor_uruk_captain" resource_id="war_spoils" upgrade_cost="4" daily_upkeep="0.2" merchant_cost="14" />
   17:   <Troop id="mordor_uruk_baraddurguard" resource_id="war_spoils" upgrade_cost="5" daily_upkeep="0.3" merchant_cost="12" />
   18:   <!-- Special: elite creature mounts gated behind their culture's special resource.
   19:        recruit_cost is a ONE-TIME charge at volunteer recruitment (these are not upgrade targets,
   20:        so upgrade_cost would never fire). The charged resource is the PLAYER's resolved resource —
   21:        resource_id here is documentation only. Harad/aserai players pay War Drums; Dol Guldur
   22:        players pay War Spoils. -->
   23:   <Troop id="harad_elephant_rider" resource_id="war_drums" recruit_cost="50" daily_upkeep="10" merchant_cost="70" />
   24:   <Troop id="taom_spider_creature" resource_id="war_spoils" recruit_cost="5" daily_upkeep="1" merchant_cost="18" />
   25: 
   26:   <!-- ============================================================================
   27:        Elite Emissary merchant offers (merchant_cost = one-time price the faction
   28:        emissary charges at a key settlement, in the SETTLEMENT OWNER's special
   29:        resource — resource_id below is documentation only). Separate from recruit_cost
   30:        so an elite that is also a volunteer is never double-charged across the two
   31:        economies. Cost band by tier: L36≈10-14, L41≈18, L46≈28, L51≈45, creatures≈premium.
   32:        Surfaced by elite_emissary/elite_emissary_config.xml. See docs/features/elite-emissary.md.
   33:        (mordor_uruk_captain/baraddurguard, harad_elephant_rider, taom_spider_creature
   34:        carry their merchant_cost on the rows above so their upgrade/recruit costs survive.)
   35:        ============================================================================ -->
   36: 
   37:   <!-- Gondor (Castar) -->
   38:   <Troop id="gondor_ithilien_ranger" resource_id="caster" merchant_cost="45" />
   39:   <Troop id="gondor_mt_fountain_guard" resource_id="caster" merchant_cost="28" />
   40:   <Troop id="gondor_ith_moon_guard" resource_id="caster" merchant_cost="28" />
   41:   <Troop id="gondor_da_swan_knight" resource_id="caster" merchant_cost="28" />
   42:   <Troop id="gondor_mt_captain" resource_id="caster" merchant_cost="18" />
   43:   <Troop id="gondor_da_vet_knight" resource_id="caster" merchant_cost="18" />
   44:   <Troop id="gondor_cal_vale_knight" resource_id="caster" merchant_cost="18" />
   45:   <Troop id="gondor_ith_sharpshooter" resource_id="caster" merchant_cost="18" />
   46: 
   47:   <!-- Erebor + Iron Hills (Gems) -->
   48:   <Troop id="erebor_noble_royal_warden" resource_id="gems" merchant_cost="28" />
   49:   <Troop id="erebor_oathsworn_royal_legionary" resource_id="gems" merchant_cost="28" />
   50:   <Troop id="iron_hills_noble_royal_warden" resource_id="gems" merchant_cost="28" />
   51:   <Troop id="erebor_noble_shield_breaker" resource_id="gems" merchant_cost="18" />
   52:   <Troop id="erebor_noble_gate_warden" resource_id="gems" merchant_cost="18" />
   53:   <Troop id="iron_hills_noble_ironbreaker" resource_id="gems" merchant_cost="18" />
   54: 
   55:   <!-- Dol Guldur (War Spoils) -->
   56:   <Troop id="dg_khamul_shadow_knight" resource_id="war_spoils" merchant_cost="28" />
   57:   <Troop id="dg_khamul_shadow_reaper" resource_id="war_spoils" merchant_cost="28" />
   58:   <Troop id="dg_khamul_shadow_bowman" resource_id="war_spoils" merchant_cost="28" />
   59:   <Troop id="dg_khamul_veiled_knight" resource_id="war_spoils" merchant_cost="18" />
   60:   <Troop id="dg_fell_warg_rider" resource_id="war_spoils" merchant_cost="12" />
   61: 
   62:   <!-- Isengard (War Spoils) -->
   63:   <Troop id="orthanc_bodyguard" resource_id="war_spoils" merchant_cost="18" />
   64:   <Troop id="urukhai_nazg_hai" resource_id="war_spoils" merchant_cost="14" />
   65:   <Troop id="orthanc_warden" resource_id="war_spoils" merchant_cost="12" />
   66: 
   67:   <!-- Gundabad (War Spoils) -->
   68:   <Troop id="gundabad_dread_rider_of_the_tower" resource_id="war_spoils" merchant_cost="18" />
   69:   <Troop id="gundabad_bolgs_ironfang" resource_id="war_spoils" merchant_cost="14" />
   70:   <Troop id="gundabad_veteran_berserker" resource_id="war_spoils" merchant_cost="12" />
   71:   <Troop id="gundabad_guardian_of_the_tower" resource_id="war_spoils" merchant_cost="12" />
   72: 
   73:   <!-- Mirkwood (Elven Wine) -->
   74:   <Troop id="mirkwood_palaceguard" resource_id="elven_wine" merchant_cost="45" />
   75:   <Troop id="mirkwood_thingolheir" resource_id="elven_wine" merchant_cost="45" />
   76:   <Troop id="mirkwood_beleglas" resource_id="elven_wine" merchant_cost="28" />
   77:   <Troop id="mirkwood_borderwardens" resource_id="elven_wine" merchant_cost="28" />
   78:   <Troop id="mirkwood_guardians" resource_id="elven_wine" merchant_cost="28" />
   79:   <Troop id="mirkwood_wardens" resource_id="elven_wine" merchant_cost="18" />
   80: 
   81:   <!-- Rivendell (Elven Wine) -->
   82:   <Troop id="rivendell_high_captain" resource_id="elven_wine" merchant_cost="45" />
   83:   <Troop id="rivendell_knight_golden_flower" resource_id="elven_wine" merchant_cost="45" />
   84:   <Troop id="rivendell_warden_gondolin" resource_id="elven_wine" merchant_cost="45" />
   85:   <Troop id="rivendell_glorfindel_guard" resource_id="elven_wine" merchant_cost="45" />
   86:   <Troop id="rivendell_royal_knight" resource_id="elven_wine" merchant_cost="28" />
   87:   <Troop id="rider_of_himring" resource_id="elven_wine" merchant_cost="28" />
   88:   <Troop id="imladris_blademaster" resource_id="elven_wine" merchant_cost="18" />
   89: 
   90:   <!-- Rohan (Marks) -->
   91:   <Troop id="rohan_edoras_golden_hall_supreme_rider" resource_id="marks" merchant_cost="18" />
   92:   <Troop id="rohan_edoras_golden_hall_kings_own_rider" resource_id="marks" merchant_cost="12" />
   93:   <Troop id="rohan_westemnet_kings_own_rider" resource_id="marks" merchant_cost="12" />
   94:   <Troop id="rohan_eastemnet_kings_own_lancer" resource_id="marks" merchant_cost="12" />
   95:   <Troop id="rohan_wold_kings_own_horse_archer" resource_id="marks" merchant_cost="12" />
   96: 
   97:   <!-- Rhun (War Banners) -->
   98:   <Troop id="dragon_wrath_obsidian_knight" resource_id="war_banners" merchant_cost="28" />
   99:   <Troop id="dragon_wrath_obsidian_warbow" resource_id="war_banners" merchant_cost="28" />
  100:   <Troop id="dragon_wrath_obsidian_shieldmaster" resource_id="war_banners" merchant_cost="28" />
  101:   <Troop id="wainrider_warlord_chariot" resource_id="war_banners" merchant_cost="28" />
  102:   <Troop id="dragon_wrath_ash_knight" resource_id="war_banners" merchant_cost="18" />
  103:   <Troop id="loke_rim_gilded_champion" resource_id="war_banners" merchant_cost="12" />
  104: </TroopResourceCosts>

 succeeded in 587ms:
    1: <?xml version="1.0" encoding="utf-8"?>
    2: <!-- Settlement Elite Emissary — buy a faction's elite troops for its special resource.
    3:      KeySettlements: where the emissary appears (verified against TAOM_Map/settlements.xml).
    4:      CultureOffers: keyed by the settlement's CURRENT OWNER culture (so conquest flips the offerings).
    5:      Prices live in special_resources/troop_resource_costs.xml (merchant_cost); a troop listed here
    6:      without a merchant_cost row is dropped + warned at load. Cultures that map to NO special resource
    7:      (goblin, mistymountainorcs) or have no L36+ elites (dale/sturgia, dunland/empire, umbar, khand/
    8:      battania, lothlorien) are intentionally omitted — the option won't appear there.
    9:      See docs/features/elite-emissary.md. -->
   10: <EliteEmissary enabled="true">
   11:   <KeySettlements>
   12:     <Settlement id="town_EW1" />       <!-- Minas Tirith (Gondor) -->
   13:     <Settlement id="town_ES1" />       <!-- Barad Dûr (Mordor) -->
   14:     <Settlement id="town_E1" />        <!-- Erebor -->
   15:     <Settlement id="town_DG1" />       <!-- Dol Guldur -->
   16:     <Settlement id="town_isengard" />  <!-- Orthanc (Isengard) -->
   17:     <Settlement id="town_G1" />        <!-- Mount Gundabad -->
   18:     <Settlement id="town_M1" />        <!-- Felegoth (Mirkwood) -->
   19:     <Settlement id="town_R1" />        <!-- Rivendell -->
   20:     <Settlement id="town_V1" />        <!-- Edoras (Rohan) -->
   21:     <Settlement id="town_RU1" />       <!-- Mistrand (Rhun) -->
   22:     <Settlement id="town_A1" />        <!-- Korb Taskral (Harad) -->
   23:   </KeySettlements>
   24: 
   25:   <CultureOffers>
   26:     <Culture id="gondor">
   27:       <Troop id="gondor_ithilien_ranger" />
   28:       <Troop id="gondor_mt_fountain_guard" />
   29:       <Troop id="gondor_ith_moon_guard" />
   30:       <Troop id="gondor_da_swan_knight" />
   31:       <Troop id="gondor_mt_captain" />
   32:       <Troop id="gondor_da_vet_knight" />
   33:       <Troop id="gondor_cal_vale_knight" />
   34:       <Troop id="gondor_ith_sharpshooter" />
   35:     </Culture>
   36: 
   37:     <Culture id="mordor">
   38:       <Troop id="mordor_uruk_baraddurguard" />
   39:       <Troop id="mordor_uruk_captain" />
   40:       <Troop id="taom_spider_creature" />
   41:     </Culture>
   42: 
   43:     <Culture id="erebor">
   44:       <Troop id="erebor_noble_royal_warden" />
   45:       <Troop id="erebor_oathsworn_royal_legionary" />
   46:       <Troop id="iron_hills_noble_royal_warden" />
   47:       <Troop id="erebor_noble_shield_breaker" />
   48:       <Troop id="erebor_noble_gate_warden" />
   49:       <Troop id="iron_hills_noble_ironbreaker" />
   50:     </Culture>
   51: 
   52:     <Culture id="dolguldur">
   53:       <Troop id="dg_khamul_shadow_knight" />
   54:       <Troop id="dg_khamul_shadow_reaper" />
   55:       <Troop id="dg_khamul_shadow_bowman" />
   56:       <Troop id="dg_khamul_veiled_knight" />
   57:       <Troop id="dg_fell_warg_rider" />
   58:     </Culture>
   59: 
   60:     <Culture id="isengard">
   61:       <Troop id="orthanc_bodyguard" />
   62:       <Troop id="urukhai_nazg_hai" />
   63:       <Troop id="orthanc_warden" />
   64:     </Culture>
   65: 
   66:     <Culture id="gundabad">
   67:       <Troop id="gundabad_dread_rider_of_the_tower" />
   68:       <Troop id="gundabad_bolgs_ironfang" />
   69:       <Troop id="gundabad_veteran_berserker" />
   70:       <Troop id="gundabad_guardian_of_the_tower" />
   71:     </Culture>
   72: 
   73:     <Culture id="mirkwood">
   74:       <Troop id="mirkwood_palaceguard" />
   75:       <Troop id="mirkwood_thingolheir" />
   76:       <Troop id="mirkwood_beleglas" />
   77:       <Troop id="mirkwood_borderwardens" />
   78:       <Troop id="mirkwood_guardians" />
   79:       <Troop id="mirkwood_wardens" />
   80:     </Culture>
   81: 
   82:     <Culture id="rivendell">
   83:       <Troop id="rivendell_high_captain" />
   84:       <Troop id="rivendell_knight_golden_flower" />
   85:       <Troop id="rivendell_warden_gondolin" />
   86:       <Troop id="rivendell_glorfindel_guard" />
   87:       <Troop id="rivendell_royal_knight" />
   88:       <Troop id="rider_of_himring" />
   89:       <Troop id="imladris_blademaster" />
   90:     </Culture>
   91: 
   92:     <Culture id="vlandia">
   93:       <Troop id="rohan_edoras_golden_hall_supreme_rider" />
   94:       <Troop id="rohan_edoras_golden_hall_kings_own_rider" />
   95:       <Troop id="rohan_westemnet_kings_own_rider" />
   96:       <Troop id="rohan_eastemnet_kings_own_lancer" />
   97:       <Troop id="rohan_wold_kings_own_horse_archer" />
   98:     </Culture>
   99: 
  100:     <Culture id="khuzait">
  101:       <Troop id="dragon_wrath_obsidian_knight" />
  102:       <Troop id="dragon_wrath_obsidian_warbow" />
  103:       <Troop id="dragon_wrath_obsidian_shieldmaster" />
  104:       <Troop id="wainrider_warlord_chariot" />
  105:       <Troop id="dragon_wrath_ash_knight" />
  106:       <Troop id="loke_rim_gilded_champion" />
  107:     </Culture>
  108: 
  109:     <Culture id="aserai">
  110:       <Troop id="harad_elephant_rider" />
  111:     </Culture>
  112:   </CultureOffers>
  113: </EliteEmissary>

 succeeded in 528ms:
    1: <?xml version="1.0" encoding="utf-8"?>
    2: <SpecialResources>
    3: 
    4:   <!-- War Spoils (Mordor/Isengard/Gundabad/Dol Guldur) — aggressive, war-driven factions -->
    5:   <!-- High battle/raid/siege earning, low daily (depend on conquest, not economy) -->
    6:   <Resource id="war_spoils" display_name="War Spoils" icon_sprite="SpecialResources\taom_war_spoils_icon"
    7:     cap="500" starting_amount="30" daily_per_town="0.2"
    8:     per_battle_victory_base="14" per_raid="12" per_siege_victory="20"
    9:     per_prisoner="2" per_tournament_win="3" per_hideout_clear="8">
   10:     <Kingdom id="empire_s" />
   11:     <Kingdom id="isengard" />
   12:     <Kingdom id="gundabad" />
   13:     <Kingdom id="dolguldur" />
   14:     <Culture id="mordor" />
   15:     <Culture id="isengard" />
   16:     <Culture id="gundabad" />
   17:     <Culture id="dolguldur" />
   18:   </Resource>
   19: 
   20:   <!-- Gems (Erebor) — mining economy, wealth through industry -->
   21:   <!-- High daily (mines), low battle earning (dwarves fight for defense, not plunder) -->
   22:   <Resource id="gems" display_name="Gems" icon_sprite="SpecialResources\taom_gems_icon"
   23:     cap="600" starting_amount="40" daily_per_town="1.0"
   24:     per_battle_victory_base="5" per_raid="3" per_siege_victory="10"
   25:     per_prisoner="0" per_tournament_win="8" per_hideout_clear="4">
   26:     <Kingdom id="erebor" />
   27:     <Culture id="erebor" />
   28:     <Tiers>
   29:       <Tier level="1" name="Apprentice Miner" threshold="100"
   30:             description="Dwarven mining efficiency improves. -5% troop upgrade cost." />
   31:       <Tier level="2" name="Journeyman Smith" threshold="250"
   32:             description="Erebor's forges burn bright. -8% enchantment cost, +5% troop morale." />
   33:       <Tier level="3" name="Master of the Treasury" threshold="400"
   34:             description="The wealth of Erebor flows. -12% troop wages, +3 party size." />
   35:     </Tiers>
   36:   </Resource>
   37: 
   38:   <!-- Castar (Gondor) — ancient Numenorean lore, scholarly and defensive -->
   39:   <!-- Moderate daily (Minas Tirith libraries), moderate battle, high siege (defense) -->
   40:   <Resource id="caster" display_name="Castar" icon_sprite="SpecialResources\taom_caster_icon"
   41:     cap="500" starting_amount="25" daily_per_town="0.6"
   42:     per_battle_victory_base="8" per_raid="4" per_siege_victory="18"
   43:     per_prisoner="1" per_tournament_win="6" per_hideout_clear="5">
   44:     <Kingdom id="empire_w" />
   45:     <Culture id="gondor" />
   46:   </Resource>
   47: 
   48:   <!-- Marks (Rohan) — honor-driven horse lords -->
   49:   <!-- High battle and tournament (martial culture), no raid (dishonorable) -->
   50:   <Resource id="marks" display_name="Marks" icon_sprite="SpecialResources\taom_marks_icon"
   51:     cap="500" starting_amount="25" daily_per_town="0.4"
   52:     per_battle_victory_base="12" per_raid="0" per_siege_victory="12"
   53:     per_prisoner="1" per_tournament_win="10" per_hideout_clear="6">
   54:     <Kingdom id="vlandia" />
   55:     <Culture id="vlandia" />
   56:   </Resource>
   57: 
   58:   <!-- Elven Wine (Rivendell/Lothlorien/Mirkwood) — peaceful, patient, ageless -->
   59:   <!-- High daily (elven craft), low battle (elves fight reluctantly), no raid -->
   60:   <Resource id="elven_wine" display_name="Elven Wine" icon_sprite="SpecialResources\taom_elven_wine_icon"
   61:     cap="400" starting_amount="35" daily_per_town="0.8"
   62:     per_battle_victory_base="6" per_raid="0" per_siege_victory="10"
   63:     per_prisoner="0" per_tournament_win="8" per_hideout_clear="4">
   64:     <Kingdom id="rivendell" />
   65:     <Kingdom id="lothlorien" />
   66:     <Kingdom id="mirkwood" />
   67:     <Culture id="rivendell" />
   68:     <Culture id="lothlorien" />
   69:     <Culture id="mirkwood" />
   70:   </Resource>
   71: 
   72:   <!-- Lake Fish (Dale/Sturgia) — trade-based economy, Lake-town heritage -->
   73:   <!-- High daily (trade), moderate battle, high tournament (festive culture) -->
   74:   <Resource id="lake_fish" display_name="Lake Fish" icon_sprite="SpecialResources\taom_lake_fish_icon"
   75:     cap="500" starting_amount="30" daily_per_town="0.7"
   76:     per_battle_victory_base="7" per_raid="5" per_siege_victory="10"
   77:     per_prisoner="1" per_tournament_win="8" per_hideout_clear="5">
   78:     <Kingdom id="sturgia" />
   79:     <Culture id="sturgia" />
   80:   </Resource>
   81: 
   82:   <!-- War Drums (Harad/Shaghana/Abanissa) — martial escalation, momentum -->
   83:   <!-- Very high raid/battle (warlike culture), moderate daily, low tournament -->
   84:   <Resource id="war_drums" display_name="War Drums" icon_sprite="SpecialResources\taom_war_drums_icon"
   85:     cap="500" starting_amount="25" daily_per_town="0.3"
   86:     per_battle_victory_base="14" per_raid="14" per_siege_victory="16"
   87:     per_prisoner="2" per_tournament_win="4" per_hideout_clear="8">
   88:     <Kingdom id="aserai" />
   89:     <Kingdom id="shaghana" />
   90:     <Kingdom id="abanissa" />
   91:     <Culture id="aserai" />
   92:     <Culture id="shaghana" />
   93:     <Culture id="abanissa" />
   94:   </Resource>
   95: 
   96:   <!-- Tribal Relics (Khand/Battania) — ancestral totems, earned through deeds -->
   97:   <!-- Balanced: moderate everything, slightly higher hideout/raid (tribal warfare) -->
   98:   <Resource id="tribal_relics" display_name="Tribal Relics" icon_sprite="SpecialResources\taom_tribal_relics_icon"
   99:     cap="450" starting_amount="20" daily_per_town="0.4"
  100:     per_battle_victory_base="10" per_raid="10" per_siege_victory="12"
  101:     per_prisoner="1" per_tournament_win="6" per_hideout_clear="8">
  102:     <Kingdom id="battania" />
  103:     <Culture id="battania" />
  104:   </Resource>
  105: 
  106:   <!-- Dunlending Ale (Dunland/Empire) — communal, celebration-driven -->
  107:   <!-- High raid (raiding Rohan is cultural tradition), high hideout, low daily -->
  108:   <Resource id="dunlending_ale" display_name="Dunlending Ale" icon_sprite="SpecialResources\taom_dunlending_ale_icon"
  109:     cap="400" starting_amount="20" daily_per_town="0.3"
  110:     per_battle_victory_base="10" per_raid="12" per_siege_victory="10"
  111:     per_prisoner="1" per_tournament_win="5" per_hideout_clear="10">
  112:     <Kingdom id="empire" />
  113:     <Culture id="empire" />
  114:   </Resource>
  115: 
  116:   <!-- Plunder (Umbar) — piracy, raiding, and maritime conquest -->
  117:   <!-- Very high raid/prisoner (corsair slavers), moderate battle, low daily -->
  118:   <Resource id="plunder" display_name="Plunder" icon_sprite="SpecialResources\taom_plunder_icon"
  119:     cap="500" starting_amount="30" daily_per_town="0.3"
  120:     per_battle_victory_base="10" per_raid="16" per_siege_victory="14"
  121:     per_prisoner="3" per_tournament_win="4" per_hideout_clear="10">
  122:     <Kingdom id="umbar" />
  123:     <Culture id="umbar" />
  124:   </Resource>
  125: 
  126:   <!-- War Banners (Rhun/Khuzait) — disciplined military hierarchy -->
  127:   <!-- High battle/siege (organized armies), moderate daily (tribute), low raid -->
  128:   <Resource id="war_banners" display_name="War Banners" icon_sprite="SpecialResources\taom_war_banners_icon"
  129:     cap="500" starting_amount="25" daily_per_town="0.5"
  130:     per_battle_victory_base="12" per_raid="6" per_siege_victory="16"
  131:     per_prisoner="1" per_tournament_win="6" per_hideout_clear="5">
  132:     <Kingdom id="khuzait" />
  133:     <Culture id="khuzait" />
  134:   </Resource>
  135: 
  136: </SpecialResources>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path TAOM.Tests'"\\Features\\EliteEmissary\\EliteEmissaryConfigProviderTests.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path TAOM.Tests'"\\Features\\EliteEmissary\\EliteEmissaryServiceTests.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path TAOM.Tests'"\\Features\\SpecialResources\\SpecialResourceServiceTests.cs | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 583ms:
    1: using System.IO;
    2: using Microsoft.VisualStudio.TestTools.UnitTesting;
    3: using NSubstitute;
    4: using TAOM.Core.Infrastructure;
    5: using TAOM.Core.Logging;
    6: using TAOM.Features.EliteEmissary;
    7: using TAOM.Features.SpecialResources;
    8: using TAOM.Features.SpecialResources.Domain;
    9: 
   10: namespace TAOM.Tests.Features.EliteEmissary;
   11: 
   12: [TestClass]
   13: public class EliteEmissaryConfigProviderTests
   14: {
   15:     private string _tempDir = null!;
   16:     private string _featureDir = null!;
   17:     private IPathService _pathService = null!;
   18:     private IModLogger _logger = null!;
   19:     private ISpecialResourceConfigProvider _resourceConfig = null!;
   20:     private EliteEmissaryConfigProvider _sut = null!;
   21: 
   22:     [TestInitialize]
   23:     public void Setup()
   24:     {
   25:         _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_EliteEmissary_" + Path.GetRandomFileName());
   26:         _featureDir = Path.Combine(_tempDir, "elite_emissary");
   27:         Directory.CreateDirectory(_featureDir);
   28: 
   29:         _pathService = Substitute.For<IPathService>();
   30:         _pathService.ModuleDataPath.Returns(_tempDir);
   31:         _logger = Substitute.For<IModLogger>();
   32:         _resourceConfig = Substitute.For<ISpecialResourceConfigProvider>();
   33: 
   34:         // Priced troops (merchant_cost > 0)
   35:         _resourceConfig.GetTroopCost("gondor_a").Returns(Priced("gondor_a", 30));
   36:         _resourceConfig.GetTroopCost("gondor_b").Returns(Priced("gondor_b", 50));
   37:         // Unpriced troop (no merchant_cost row)
   38:         _resourceConfig.GetTroopCost("gondor_unpriced").Returns((TroopResourceCostEntry)null);
   39: 
   40:         _sut = new EliteEmissaryConfigProvider(_pathService, _logger, _resourceConfig);
   41:     }
   42: 
   43:     [TestCleanup]
   44:     public void Cleanup()
   45:     {
   46:         if (Directory.Exists(_tempDir))
   47:             Directory.Delete(_tempDir, true);
   48:     }
   49: 
   50:     private static TroopResourceCostEntry Priced(string id, int merchant) =>
   51:         new(id, "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: merchant);
   52: 
   53:     private void WriteConfig(string xml) =>
   54:         File.WriteAllText(Path.Combine(_featureDir, "elite_emissary_config.xml"), xml);
   55: 
   56:     [TestMethod]
   57:     public void GetConfig_ValidXml_LoadsKeySettlementsAndCultureOffers()
   58:     {
   59:         WriteConfig(@"<EliteEmissary enabled=""true"">
   60:   <KeySettlements>
   61:     <Settlement id=""town_EW1"" />
   62:     <Settlement id=""town_ES1"" />
   63:   </KeySettlements>
   64:   <CultureOffers>
   65:     <Culture id=""gondor"">
   66:       <Troop id=""gondor_a"" />
   67:       <Troop id=""gondor_b"" />
   68:     </Culture>
   69:   </CultureOffers>
   70: </EliteEmissary>");
   71: 
   72:         var config = _sut.GetConfig();
   73: 
   74:         Assert.IsTrue(config.Enabled);
   75:         Assert.IsTrue(config.KeySettlementIds.Contains("town_EW1"));
   76:         Assert.IsTrue(config.KeySettlementIds.Contains("town_ES1"));
   77:         Assert.AreEqual(2, config.KeySettlementIds.Count);
   78:         Assert.IsTrue(config.CultureOffers.ContainsKey("gondor"));
   79:         Assert.AreEqual(2, config.CultureOffers["gondor"].Count);
   80:         Assert.AreEqual("gondor_a", config.CultureOffers["gondor"][0]);
   81:     }
   82: 
   83:     [TestMethod]
   84:     public void GetConfig_MissingFile_ReturnsEmptyAndLogsWarning()
   85:     {
   86:         var config = _sut.GetConfig();
   87: 
   88:         Assert.IsFalse(config.Enabled);
   89:         Assert.AreEqual(0, config.KeySettlementIds.Count);
   90:         Assert.AreEqual(0, config.CultureOffers.Count);
   91:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
   92:     }
   93: 
   94:     [TestMethod]
   95:     public void GetConfig_MalformedXml_ReturnsEmptyAndLogsError()
   96:     {
   97:         WriteConfig("<EliteEmissary><not closed");
   98: 
   99:         var config = _sut.GetConfig();
  100: 
  101:         Assert.AreEqual(0, config.CultureOffers.Count);
  102:         _logger.Received().LogError(Arg.Is<string>(s => s.Contains("parse")));
  103:     }
  104: 
  105:     [TestMethod]
  106:     public void GetConfig_UnknownCultureId_DroppedWithWarning()
  107:     {
  108:         WriteConfig(@"<EliteEmissary enabled=""true"">
  109:   <CultureOffers>
  110:     <Culture id=""gondorr"">
  111:       <Troop id=""gondor_a"" />
  112:     </Culture>
  113:   </CultureOffers>
  114: </EliteEmissary>");
  115: 
  116:         var config = _sut.GetConfig();
  117: 
  118:         Assert.IsFalse(config.CultureOffers.ContainsKey("gondorr"));
  119:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gondorr")));
  120:     }
  121: 
  122:     [TestMethod]
  123:     public void GetConfig_TroopWithoutMerchantCost_DroppedWithWarning()
  124:     {
  125:         WriteConfig(@"<EliteEmissary enabled=""true"">
  126:   <CultureOffers>
  127:     <Culture id=""gondor"">
  128:       <Troop id=""gondor_a"" />
  129:       <Troop id=""gondor_unpriced"" />
  130:     </Culture>
  131:   </CultureOffers>
  132: </EliteEmissary>");
  133: 
  134:         var config = _sut.GetConfig();
  135: 
  136:         Assert.AreEqual(1, config.CultureOffers["gondor"].Count);
  137:         Assert.AreEqual("gondor_a", config.CultureOffers["gondor"][0]);
  138:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gondor_unpriced")));
  139:     }
  140: 
  141:     [TestMethod]
  142:     public void GetConfig_CultureWithNoValidTroops_NotRecorded()
  143:     {
  144:         WriteConfig(@"<EliteEmissary enabled=""true"">
  145:   <CultureOffers>
  146:     <Culture id=""gondor"">
  147:       <Troop id=""gondor_unpriced"" />
  148:     </Culture>
  149:   </CultureOffers>
  150: </EliteEmissary>");
  151: 
  152:         var config = _sut.GetConfig();
  153: 
  154:         Assert.IsFalse(config.CultureOffers.ContainsKey("gondor"));
  155:     }
  156: 
  157:     [TestMethod]
  158:     public void GetConfig_EnabledFalse_Honored()
  159:     {
  160:         WriteConfig(@"<EliteEmissary enabled=""false""><KeySettlements /><CultureOffers /></EliteEmissary>");
  161: 
  162:         var config = _sut.GetConfig();
  163: 
  164:         Assert.IsFalse(config.Enabled);
  165:     }
  166: }

 succeeded in 594ms:
    1: using System.Collections.Generic;
    2: using Microsoft.VisualStudio.TestTools.UnitTesting;
    3: using NSubstitute;
    4: using TAOM.Adapters;
    5: using TAOM.Core.Logging;
    6: using TAOM.Features.EliteEmissary;
    7: using TAOM.Features.EliteEmissary.Domain;
    8: using TAOM.Features.SpecialResources;
    9: using TAOM.Features.SpecialResources.Domain;
   10: 
   11: namespace TAOM.Tests.Features.EliteEmissary;
   12: 
   13: [TestClass]
   14: public class EliteEmissaryServiceTests
   15: {
   16:     private IEliteEmissaryConfigProvider _config;
   17:     private IEliteEmissarySettingsProvider _settings;
   18:     private ISpecialResourceService _resourceService;
   19:     private ISpecialResourceConfigProvider _resourceConfig;
   20:     private IPlayerPartyAdapter _party;
   21:     private IModLogger _logger;
   22:     private EliteEmissaryService _service;
   23: 
   24:     private static readonly SpecialResource Caster = new(
   25:         id: "caster",
   26:         kingdomIds: new[] { "empire_w" },
   27:         cultureIds: new[] { "gondor" },
   28:         displayName: "Castar",
   29:         iconSpriteName: "SpecialResources\\taom_caster_icon",
   30:         cap: 500f, startingAmount: 25f, dailyPerTown: 0.6f,
   31:         perBattleVictoryBase: 8f, perRaid: 4f, perSiegeVictory: 18f, perPrisoner: 1f);
   32: 
   33:     private const string Hero = "hero1";
   34:     private const string Kingdom = "empire_w";
   35:     private const string Culture = "gondor";
   36: 
   37:     [TestInitialize]
   38:     public void Setup()
   39:     {
   40:         _config = Substitute.For<IEliteEmissaryConfigProvider>();
   41:         _settings = Substitute.For<IEliteEmissarySettingsProvider>();
   42:         _resourceService = Substitute.For<ISpecialResourceService>();
   43:         _resourceConfig = Substitute.For<ISpecialResourceConfigProvider>();
   44:         _party = Substitute.For<IPlayerPartyAdapter>();
   45:         _logger = Substitute.For<IModLogger>();
   46: 
   47:         _settings.IsEnabled.Returns(true);
   48:         _config.GetConfig().Returns(MakeConfig(
   49:             keySettlements: new[] { "town_EW1" },
   50:             offers: ("gondor", new[] { "gondor_a", "gondor_b" })));
   51: 
   52:         _resourceService.ResolveResource(Kingdom, Culture).Returns(Caster);
   53:         _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 30));
   54:         _resourceConfig.GetTroopCost("gondor_b").Returns(Cost("gondor_b", 50));
   55: 
   56:         _service = new EliteEmissaryService(_config, _settings, _resourceService, _resourceConfig, _party, _logger);
   57:     }
   58: 
   59:     private static TroopResourceCostEntry Cost(string id, int merchant) =>
   60:         new(id, "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: merchant);
   61: 
   62:     private static EliteEmissaryConfig MakeConfig(string[] keySettlements, params (string culture, string[] troops)[] offers)
   63:     {
   64:         var dict = new Dictionary<string, IReadOnlyList<string>>();
   65:         foreach (var (culture, troops) in offers)
   66:             dict[culture] = new List<string>(troops);
   67:         return new EliteEmissaryConfig(true, new HashSet<string>(keySettlements), dict);
   68:     }
   69: 
   70:     // ── IsEnabled / IsKeySettlement ──
   71: 
   72:     [TestMethod]
   73:     public void IsEnabled_ReflectsSettings()
   74:     {
   75:         _settings.IsEnabled.Returns(false);
   76:         Assert.IsFalse(_service.IsEnabled);
   77:     }
   78: 
   79:     [TestMethod]
   80:     public void IsKeySettlement_ConfiguredId_ReturnsTrue() =>
   81:         Assert.IsTrue(_service.IsKeySettlement("town_EW1"));
   82: 
   83:     [TestMethod]
   84:     public void IsKeySettlement_UnknownId_ReturnsFalse() =>
   85:         Assert.IsFalse(_service.IsKeySettlement("town_ZZ9"));
   86: 
   87:     [TestMethod]
   88:     public void IsKeySettlement_Null_ReturnsFalse() =>
   89:         Assert.IsFalse(_service.IsKeySettlement(null));
   90: 
   91:     // ── HasPurchasableOffers ──
   92: 
   93:     [TestMethod]
   94:     public void HasPurchasableOffers_CultureWithPricedTroops_ReturnsTrue() =>
   95:         Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
   96: 
   97:     [TestMethod]
   98:     public void HasPurchasableOffers_NoResolvedResource_ReturnsFalse()
   99:     {
  100:         _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
  101:         Assert.IsFalse(_service.HasPurchasableOffers("rebel_x", "umbar"));
  102:     }
  103: 
  104:     [TestMethod]
  105:     public void HasPurchasableOffers_CultureNotInConfig_ReturnsFalse()
  106:     {
  107:         _resourceService.ResolveResource(Kingdom, "mordor").Returns(Caster);
  108:         Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, "mordor"));
  109:     }
  110: 
  111:     [TestMethod]
  112:     public void HasPurchasableOffers_AllOffersLackMerchantCost_ReturnsFalse()
  113:     {
  114:         _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 0));
  115:         _resourceConfig.GetTroopCost("gondor_b").Returns((TroopResourceCostEntry)null);
  116:         Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
  117:     }
  118: 
  119:     // ── BuildOfferList ──
  120: 
  121:     [TestMethod]
  122:     public void BuildOfferList_NoResource_ReturnsNoResourceMarker()
  123:     {
  124:         _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
  125:         var list = _service.BuildOfferList(Hero, "rebel_x", "umbar");
  126:         Assert.IsTrue(list.NoResource);
  127:         Assert.IsFalse(list.HasOffers);
  128:     }
  129: 
  130:     [TestMethod]
  131:     public void BuildOfferList_ReturnsPricedTroops_InConfigOrder()
  132:     {
  133:         _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);
  134:         var list = _service.BuildOfferList(Hero, Kingdom, Culture);
  135: 
  136:         Assert.IsFalse(list.NoResource);
  137:         Assert.AreEqual("caster", list.ResourceId);
  138:         Assert.AreEqual("Castar", list.ResourceDisplayName);
  139:         Assert.AreEqual(2, list.Offers.Count);
  140:         Assert.AreEqual("gondor_a", list.Offers[0].TroopId);
  141:         Assert.AreEqual(30, list.Offers[0].MerchantCost);
  142:         Assert.AreEqual("gondor_b", list.Offers[1].TroopId);
  143:     }
  144: 
  145:     [TestMethod]
  146:     public void BuildOfferList_SkipsTroopsWithoutMerchantCost()
  147:     {
  148:         _resourceConfig.GetTroopCost("gondor_a").Returns((TroopResourceCostEntry)null);
  149:         _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);
  150: 
  151:         var list = _service.BuildOfferList(Hero, Kingdom, Culture);
  152: 
  153:         Assert.AreEqual(1, list.Offers.Count);
  154:         Assert.AreEqual("gondor_b", list.Offers[0].TroopId);
  155:     }
  156: 
  157:     [TestMethod]
  158:     public void BuildOfferList_AffordFlag_FlipsAtBalanceEqualsCost()
  159:     {
  160:         _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(30f); // exactly one gondor_a
  161:         var list = _service.BuildOfferList(Hero, Kingdom, Culture);
  162: 
  163:         var a = list.Offers[0]; // cost 30
  164:         var b = list.Offers[1]; // cost 50
  165:         Assert.IsTrue(a.CanAfford);
  166:         Assert.AreEqual(1, a.MaxAffordableQuantity);
  167:         Assert.IsFalse(b.CanAfford);
  168:         Assert.AreEqual(0, b.MaxAffordableQuantity);
  169:     }
  170: 
  171:     [TestMethod]
  172:     public void BuildOfferList_MaxAffordable_IsFloorOfBalanceOverCost()
  173:     {
  174:         _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(95f);
  175:         var list = _service.BuildOfferList(Hero, Kingdom, Culture);
  176:         Assert.AreEqual(3, list.Offers[0].MaxAffordableQuantity); // 95 / 30 = 3
  177:         Assert.AreEqual(1, list.Offers[1].MaxAffordableQuantity); // 95 / 50 = 1
  178:     }
  179: 
  180:     [TestMethod]
  181:     public void BuildOfferList_CultureNotInConfig_EmptyOffers()
  182:     {
  183:         _resourceService.ResolveResource(Kingdom, "mordor").Returns(Caster);
  184:         var list = _service.BuildOfferList(Hero, Kingdom, "mordor");
  185:         Assert.IsFalse(list.NoResource);
  186:         Assert.IsFalse(list.HasOffers);
  187:     }
  188: 
  189:     // ── Purchase ──
  190: 
  191:     [TestMethod]
  192:     public void Purchase_NullTroop_Invalid()
  193:     {
  194:         var r = _service.Purchase(Hero, Kingdom, Culture, null, 1);
  195:         Assert.AreEqual(EmissaryPurchaseStatus.Invalid, r.Status);
  196:         _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
  197:     }
  198: 
  199:     [TestMethod]
  200:     public void Purchase_ZeroQuantity_Invalid()
  201:     {
  202:         var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 0);
  203:         Assert.AreEqual(EmissaryPurchaseStatus.Invalid, r.Status);
  204:     }
  205: 
  206:     [TestMethod]
  207:     public void Purchase_NoResource_NoResourceStatus()
  208:     {
  209:         _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
  210:         var r = _service.Purchase(Hero, "rebel_x", "umbar", "gondor_a", 1);
  211:         Assert.AreEqual(EmissaryPurchaseStatus.NoResource, r.Status);
  212:         _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
  213:     }
  214: 
  215:     [TestMethod]
  216:     public void Purchase_TroopWithoutMerchantCost_NotOffered()
  217:     {
  218:         _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 0));
  219:         var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);
  220:         Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
  221:     }
  222: 
  223:     [TestMethod]
  224:     public void Purchase_TroopNotInOwnerCultureList_NotOffered()
  225:     {
  226:         // Has a merchant cost, but isn't an offer for gondor — don't trust the inquiry round-trip.
  227:         _resourceConfig.GetTroopCost("mordor_uruk_captain").Returns(Cost("mordor_uruk_captain", 40));
  228:         var r = _service.Purchase(Hero, Kingdom, Culture, "mordor_uruk_captain", 1);
  229:         Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
  230:         _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
  231:     }
  232: 
  233:     [TestMethod]
  234:     public void Purchase_Unaffordable_DoesNotGrantOrCharge()
  235:     {
  236:         _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 3).Returns(false);
  237:         var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 3);
  238: 
  239:         Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
  240:         Assert.AreEqual(90, r.TotalCost); // 30 × 3
  241:         Assert.AreEqual("Castar", r.ResourceDisplayName);
  242:         _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
  243:         _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
  244:     }
  245: 
  246:     [TestMethod]
  247:     public void Purchase_Success_GrantsThenCharges()
  248:     {
  249:         _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 2).Returns(true);
  250:         _party.GrantTroop("gondor_a", 2).Returns(true);
  251: 
  252:         var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);
  253: 
  254:         Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
  255:         Assert.AreEqual(60, r.TotalCost);
  256:         Assert.AreEqual(2, r.Quantity);
  257:         Assert.AreEqual("gondor_a", r.TroopId);
  258:         _party.Received(1).GrantTroop("gondor_a", 2);
  259:         _resourceService.Received(1).ChargeMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 2);
  260:     }
  261: 
  262:     [TestMethod]
  263:     public void Purchase_GrantFails_FailedAndDoesNotCharge()
  264:     {
  265:         _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 1).Returns(true);
  266:         _party.GrantTroop("gondor_a", 1).Returns(false);
  267: 
  268:         var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);
  269: 
  270:         Assert.AreEqual(EmissaryPurchaseStatus.Failed, r.Status);
  271:         _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
  272:     }
  273: }

 succeeded in 608ms:
    1: using System.Collections.Generic;
    2: using Microsoft.VisualStudio.TestTools.UnitTesting;
    3: using NSubstitute;
    4: using TAOM.Core.Logging;
    5: using TAOM.Features.CareerSystem;
    6: using TAOM.Features.CareerSystem.Domain;
    7: using TAOM.Features.SpecialResources;
    8: using TAOM.Features.SpecialResources.Domain;
    9: 
   10: namespace TAOM.Tests.Features.SpecialResources;
   11: 
   12: [TestClass]
   13: public class SpecialResourceServiceTests
   14: {
   15:     private ISpecialResourceConfigProvider _config;
   16:     private ISpecialResourceStorageService _storage;
   17:     private IModLogger _logger;
   18:     private ICareerPassiveService _passiveService;
   19:     private SpecialResourceService _service;
   20: 
   21:     private static readonly SpecialResource MordorResource = new(
   22:         id: "war_spoils",
   23:         kingdomIds: new[] { "empire_s", "isengard" },
   24:         cultureIds: new[] { "mordor", "isengard" },
   25:         displayName: "War Spoils",
   26:         iconSpriteName: "taom_war_spoils_icon",
   27:         cap: 500f,
   28:         startingAmount: 30f,
   29:         dailyPerTown: 0.5f,
   30:         perBattleVictoryBase: 10f,
   31:         perRaid: 8f,
   32:         perSiegeVictory: 15f,
   33:         perPrisoner: 1f);
   34: 
   35:     [TestInitialize]
   36:     public void Setup()
   37:     {
   38:         _config = Substitute.For<ISpecialResourceConfigProvider>();
   39:         _storage = Substitute.For<ISpecialResourceStorageService>();
   40:         _logger = Substitute.For<IModLogger>();
   41:         _passiveService = Substitute.For<ICareerPassiveService>();
   42:         _service = new SpecialResourceService(_config, _storage, _logger, _passiveService);
   43: 
   44:         _config.GetByKingdomId("empire_s").Returns(MordorResource);
   45:         _config.GetByKingdomId("isengard").Returns(MordorResource);
   46:         _config.GetByCultureId("mordor").Returns(MordorResource);
   47:         _config.GetByCultureId("isengard").Returns(MordorResource);
   48:     }
   49: 
   50:     // ── Resolution ──
   51: 
   52:     [TestMethod]
   53:     public void ResolveResource_PrefersKingdom_OverCulture()
   54:     {
   55:         var result = _service.ResolveResource("empire_s", "mordor");
   56:         Assert.AreSame(MordorResource, result);
   57:     }
   58: 
   59:     [TestMethod]
   60:     public void ResolveResource_FallsToCulture_WhenKingdomIsNull()
   61:     {
   62:         var result = _service.ResolveResource(null, "mordor");
   63:         Assert.AreSame(MordorResource, result);
   64:     }
   65: 
   66:     [TestMethod]
   67:     public void ResolveResource_ReturnsNull_WhenBothNull()
   68:     {
   69:         var result = _service.ResolveResource(null, null);
   70:         Assert.IsNull(result);
   71:     }
   72: 
   73:     [TestMethod]
   74:     public void ResolveResource_SharedResource_BothKingdomsResolveSameInstance()
   75:     {
   76:         var fromMordor = _service.ResolveResource("empire_s", null);
   77:         var fromIsengard = _service.ResolveResource("isengard", null);
   78:         Assert.AreSame(fromMordor, fromIsengard);
   79:         Assert.AreEqual("war_spoils", fromMordor.Id);
   80:     }
   81: 
   82:     [TestMethod]
   83:     public void ResolveResource_FallsToCulture_WhenKingdomNotConfigured()
   84:     {
   85:         _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
   86:         var result = _service.ResolveResource("empire_w", "mordor");
   87:         Assert.AreSame(MordorResource, result);
   88:     }
   89: 
   90:     // ── Resolution Logging Dedupe ──
   91: 
   92:     [TestMethod]
   93:     public void ResolveResource_KingdomHit_LogsDebugOnce_ForFirstCall()
   94:     {
   95:         _service.ResolveResource("empire_s", null);
   96:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
   97:     }
   98: 
   99:     [TestMethod]
  100:     public void ResolveResource_KingdomHit_DoesNotLogDebug_OnSecondIdenticalCall()
  101:     {
  102:         _service.ResolveResource("empire_s", null);
  103:         _logger.ClearReceivedCalls();
  104:         _service.ResolveResource("empire_s", null);
  105:         _logger.DidNotReceive().LogDebug(Arg.Any<string>());
  106:     }
  107: 
  108:     [TestMethod]
  109:     public void ResolveResource_CultureFallback_DoesNotLogDebug_OnSecondIdenticalCall()
  110:     {
  111:         _service.ResolveResource(null, "mordor");
  112:         _logger.ClearReceivedCalls();
  113:         _service.ResolveResource(null, "mordor");
  114:         _logger.DidNotReceive().LogDebug(Arg.Any<string>());
  115:     }
  116: 
  117:     [TestMethod]
  118:     public void ResolveResource_NoMatch_DoesNotLogDebug_OnSecondIdenticalCall()
  119:     {
  120:         _config.GetByKingdomId("unknown_kingdom").Returns((SpecialResource)null);
  121:         _config.GetByCultureId("unknown_culture").Returns((SpecialResource)null);
  122: 
  123:         _service.ResolveResource("unknown_kingdom", "unknown_culture");
  124:         _logger.ClearReceivedCalls();
  125:         _service.ResolveResource("unknown_kingdom", "unknown_culture");
  126: 
  127:         _logger.DidNotReceive().LogDebug(Arg.Any<string>());
  128:     }
  129: 
  130:     [TestMethod]
  131:     public void ResolveResource_DifferentKeys_LogIndependently()
  132:     {
  133:         _service.ResolveResource("empire_s", null);
  134:         _service.ResolveResource("isengard", null);
  135: 
  136:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
  137:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'isengard'")));
  138:     }
  139: 
  140:     [TestMethod]
  141:     public void ResolveResource_SameKingdomDifferentCulture_LogsAgain()
  142:     {
  143:         // (kingdomId, cultureId) is the dedupe key; switching either side counts as a new context.
  144:         _service.ResolveResource(null, "mordor");
  145:         _service.ResolveResource(null, "isengard");
  146: 
  147:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via culture 'mordor'")));
  148:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via culture 'isengard'")));
  149:     }
  150: 
  151:     // ── Earning ──
  152: 
  153:     [TestMethod]
  154:     public void EarnFromBattle_AddsScaledAmount_BasedOnEnemySizeRatio()
  155:     {
  156:         _storage.Get("hero1", "war_spoils").Returns(100f);
  157:         _service.EarnFromBattle("hero1", "empire_s", null, 1.5f);
  158:         _storage.Received(1).Set("hero1", "war_spoils", 115f);
  159:     }
  160: 
  161:     [TestMethod]
  162:     public void EarnFromBattle_WorksViaCultureFallback()
  163:     {
  164:         _storage.Get("hero1", "war_spoils").Returns(100f);
  165:         _service.EarnFromBattle("hero1", null, "mordor", 1.0f);
  166:         _storage.Received(1).Set("hero1", "war_spoils", 110f);
  167:     }
  168: 
  169:     [TestMethod]
  170:     public void EarnFromBattle_ClampsRatio_ToMinHalf()
  171:     {
  172:         _storage.Get("hero1", "war_spoils").Returns(100f);
  173:         _service.EarnFromBattle("hero1", "empire_s", null, 0.1f);
  174:         _storage.Received(1).Set("hero1", "war_spoils", 105f);
  175:     }
  176: 
  177:     [TestMethod]
  178:     public void EarnFromBattle_ClampsRatio_ToMaxTwo()
  179:     {
  180:         _storage.Get("hero1", "war_spoils").Returns(100f);
  181:         _service.EarnFromBattle("hero1", "empire_s", null, 5.0f);
  182:         _storage.Received(1).Set("hero1", "war_spoils", 120f);
  183:     }
  184: 
  185:     [TestMethod]
  186:     public void EarnFromBattle_CapsAtResourceMax()
  187:     {
  188:         _storage.Get("hero1", "war_spoils").Returns(498f);
  189:         _service.EarnFromBattle("hero1", "empire_s", null, 1.0f);
  190:         _storage.Received(1).Set("hero1", "war_spoils", 500f);
  191:     }
  192: 
  193:     [TestMethod]
  194:     public void EarnFromBattle_NoOp_WhenNoResourceResolved()
  195:     {
  196:         _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
  197:         _config.GetByCultureId("gondor").Returns((SpecialResource)null);
  198:         _service.EarnFromBattle("hero1", "empire_w", "gondor", 1.0f);
  199:         _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  200:     }
  201: 
  202:     [TestMethod]
  203:     public void EarnFromRaid_AddsConfiguredAmount()
  204:     {
  205:         _storage.Get("hero1", "war_spoils").Returns(50f);
  206:         _service.EarnFromRaid("hero1", "empire_s", null);
  207:         _storage.Received(1).Set("hero1", "war_spoils", 58f);
  208:     }
  209: 
  210:     [TestMethod]
  211:     public void EarnFromSiege_AddsConfiguredAmount()
  212:     {
  213:         _storage.Get("hero1", "war_spoils").Returns(50f);
  214:         _service.EarnFromSiege("hero1", "empire_s", null);
  215:         _storage.Received(1).Set("hero1", "war_spoils", 65f);
  216:     }
  217: 
  218:     [TestMethod]
  219:     public void EarnFromPrisoners_AddsPerPrisonerTimesCount()
  220:     {
  221:         _storage.Get("hero1", "war_spoils").Returns(50f);
  222:         _service.EarnFromPrisoners("hero1", "empire_s", null, 7);
  223:         _storage.Received(1).Set("hero1", "war_spoils", 57f);
  224:     }
  225: 
  226:     // ── Upgrade Affordability ──
  227: 
  228:     [TestMethod]
  229:     public void CanAffordUpgrade_ReturnsFalse_WhenInsufficientResources()
  230:     {
  231:         var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  232:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
  233:         _storage.Get("hero1", "war_spoils").Returns(8f);
  234: 
  235:         Assert.IsFalse(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
  236:     }
  237: 
  238:     [TestMethod]
  239:     public void CanAffordUpgrade_ReturnsTrue_WhenSufficientResources()
  240:     {
  241:         var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  242:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
  243:         _storage.Get("hero1", "war_spoils").Returns(15f);
  244: 
  245:         Assert.IsTrue(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
  246:     }
  247: 
  248:     [TestMethod]
  249:     public void SpendForUpgrade_DeductsCorrectAmount()
  250:     {
  251:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  252:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  253: 
  254:         _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 3);
  255:         _storage.Received(1).Add("hero1", "war_spoils", -12f);
  256:     }
  257: 
  258:     // ── Recruit Cost (elephant/spider volunteer gate) ──
  259: 
  260:     [TestMethod]
  261:     public void ChargeRecruitCost_DeductsRecruitCostTimesCount()
  262:     {
  263:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  264:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  265: 
  266:         _service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 2);
  267: 
  268:         _storage.Received(1).Add("hero1", "war_spoils", -100f);
  269:     }
  270: 
  271:     [TestMethod]
  272:     public void ChargeRecruitCost_NoCostEntry_NoOp()
  273:     {
  274:         _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
  275: 
  276:         _service.ChargeRecruitCost("hero1", "empire_s", null, "plain_troop", 1);
  277: 
  278:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  279:     }
  280: 
  281:     [TestMethod]
  282:     public void ChargeRecruitCost_ZeroRecruitCost_NoOp()
  283:     {
  284:         // An upkeep-only entry (recruit_cost omitted) must not deduct on recruit.
  285:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f, recruitCost: 0);
  286:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  287: 
  288:         _service.ChargeRecruitCost("hero1", "empire_s", null, "mordor_uruk_captain", 3);
  289: 
  290:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  291:     }
  292: 
  293:     [TestMethod]
  294:     public void ChargeRecruitCost_NoResolvedResource_NoOp()
  295:     {
  296:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  297:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  298: 
  299:         _service.ChargeRecruitCost("hero1", "unmapped_kingdom", null, "harad_elephant_rider", 1);
  300: 
  301:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  302:     }
  303: 
  304:     [TestMethod]
  305:     public void CanAffordRecruit_BalanceEqualsCost_Allowed()
  306:     {
  307:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  308:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  309:         _storage.Get("hero1", "war_spoils").Returns(50f);
  310: 
  311:         var result = _service.CanAffordRecruit("hero1", "empire_s", null,
  312:             new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });
  313: 
  314:         Assert.IsFalse(result.Blocked);
  315:     }
  316: 
  317:     [TestMethod]
  318:     public void CanAffordRecruit_BalanceBelowCost_Blocked()
  319:     {
  320:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  321:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  322:         _storage.Get("hero1", "war_spoils").Returns(49f);
  323: 
  324:         var result = _service.CanAffordRecruit("hero1", "empire_s", null,
  325:             new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });
  326: 
  327:         Assert.IsTrue(result.Blocked);
  328:         Assert.AreEqual(50, result.Required);
  329:         Assert.AreEqual("War Spoils", result.ResourceDisplayName);
  330:     }
  331: 
  332:     [TestMethod]
  333:     public void CanAffordRecruit_SumsMultipleCartEntries()
  334:     {
  335:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  336:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  337:         _storage.Get("hero1", "war_spoils").Returns(120f);
  338: 
  339:         var result = _service.CanAffordRecruit("hero1", "empire_s", null,
  340:             new List<RecruitCartEntry> { new("harad_elephant_rider", 3) }); // 150 > 120
  341: 
  342:         Assert.IsTrue(result.Blocked);
  343:         Assert.AreEqual(150, result.Required);
  344:     }
  345: 
  346:     [TestMethod]
  347:     public void CanAffordRecruit_CartTroopHasNoRecruitCost_Allowed()
  348:     {
  349:         _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
  350:         _storage.Get("hero1", "war_spoils").Returns(0f);
  351: 
  352:         var result = _service.CanAffordRecruit("hero1", "empire_s", null,
  353:             new List<RecruitCartEntry> { new("plain_troop", 5) });
  354: 
  355:         Assert.IsFalse(result.Blocked);
  356:     }
  357: 
  358:     [TestMethod]
  359:     public void CanAffordRecruit_NoResolvedResource_Allowed()
  360:     {
  361:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
  362:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  363: 
  364:         var result = _service.CanAffordRecruit("hero1", "unmapped_kingdom", null,
  365:             new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });
  366: 
  367:         Assert.IsFalse(result.Blocked);
  368:     }
  369: 
  370:     [TestMethod]
  371:     public void CanAffordRecruit_EmptyCart_Allowed()
  372:     {
  373:         var result = _service.CanAffordRecruit("hero1", "empire_s", null, new List<RecruitCartEntry>());
  374:         Assert.IsFalse(result.Blocked);
  375:     }
  376: 
  377:     // ── Merchant Purchase (Elite Emissary) ──
  378:     // merchant_cost is a SEPARATE field from recruit_cost so the emissary never collides with the
  379:     // volunteer gate. The charged resource is resolved from the SETTLEMENT OWNER's faction (the
  380:     // kingdom/culture args), not the player's clan.
  381: 
  382:     [TestMethod]
  383:     public void CanAffordMerchantPurchase_BalanceAboveCost_ReturnsTrue()
  384:     {
  385:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  386:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  387:         _storage.Get("hero1", "war_spoils").Returns(100f);
  388: 
  389:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 ≤ 100
  390:     }
  391: 
  392:     [TestMethod]
  393:     public void CanAffordMerchantPurchase_BalanceBelowCost_ReturnsFalse()
  394:     {
  395:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  396:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  397:         _storage.Get("hero1", "war_spoils").Returns(80f);
  398: 
  399:         Assert.IsFalse(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 > 80
  400:     }
  401: 
  402:     [TestMethod]
  403:     public void CanAffordMerchantPurchase_BalanceEqualsCost_ReturnsTrue()
  404:     {
  405:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  406:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  407:         _storage.Get("hero1", "war_spoils").Returns(90f);
  408: 
  409:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 == 90
  410:     }
  411: 
  412:     [TestMethod]
  413:     public void CanAffordMerchantPurchase_NoMerchantCost_AllowsByDefault()
  414:     {
  415:         // An upgrade/upkeep-only entry (merchant_cost omitted) is not an emissary offer; afford-allow
  416:         // so the gate decision lives in the offer-list builder, not here.
  417:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f);
  418:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  419:         _storage.Get("hero1", "war_spoils").Returns(0f);
  420: 
  421:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 5));
  422:     }
  423: 
  424:     [TestMethod]
  425:     public void CanAffordMerchantPurchase_ZeroCount_AllowsByDefault()
  426:     {
  427:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  428:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  429:         _storage.Get("hero1", "war_spoils").Returns(0f);
  430: 
  431:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0));
  432:     }
  433: 
  434:     [TestMethod]
  435:     public void ChargeMerchantPurchase_DeductsMerchantCostTimesCount_FromOwnerResource()
  436:     {
  437:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  438:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  439: 
  440:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 2);
  441: 
  442:         _storage.Received(1).Add("hero1", "war_spoils", -60f);
  443:     }
  444: 
  445:     [TestMethod]
  446:     public void ChargeMerchantPurchase_NoMerchantCost_NoOp()
  447:     {
  448:         // recruit_cost set but merchant_cost 0 — must NOT deduct (proves the two economies don't cross).
  449:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 0);
  450:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  451: 
  452:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 2);
  453: 
  454:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  455:     }
  456: 
  457:     [TestMethod]
  458:     public void ChargeMerchantPurchase_NoCostEntry_NoOp()
  459:     {
  460:         _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
  461: 
  462:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "plain_troop", 1);
  463: 
  464:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  465:     }
  466: 
  467:     [TestMethod]
  468:     public void ChargeMerchantPurchase_NoResolvedResource_NoOp()
  469:     {
  470:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  471:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  472: 
  473:         _service.ChargeMerchantPurchase("hero1", "unmapped_kingdom", null, "mordor_uruk_captain", 1);
  474: 
  475:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  476:     }
  477: 
  478:     [TestMethod]
  479:     public void ChargeMerchantPurchase_ZeroCount_NoOp()
  480:     {
  481:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  482:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  483: 
  484:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0);
  485: 
  486:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  487:     }
  488: 
  489:     [TestMethod]
  490:     public void RecruitVsMerchant_SameTroopWithBothCosts_ChargeIndependentFields()
  491:     {
  492:         // The headline "never double-charged" invariant, load-bearing only for a troop that is BOTH a
  493:         // recruitable volunteer AND an emissary offer (harad_elephant_rider / taom_spider_creature):
  494:         // the volunteer path charges recruit_cost, the emissary path charges merchant_cost — never the
  495:         // other's field. Deep-review 2026-06-25 (completeness critic gap #5).
  496:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 70);
  497:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  498: 
  499:         _service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 1);
  500:         _storage.Received(1).Add("hero1", "war_spoils", -50f);   // volunteer path → recruit_cost
  501: 
  502:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 1);
  503:         _storage.Received(1).Add("hero1", "war_spoils", -70f);   // emissary path → merchant_cost
  504:     }
  505: 
  506:     // ── Daily Tick ──
  507: 
  508:     [TestMethod]
  509:     public void ApplyDailyTick_EarningExceedsUpkeep_AddsCapped()
  510:     {
  511:         _storage.Get("hero1", "war_spoils").Returns(100f);
  512:         _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
  513:         _storage.Received(1).Set("hero1", "war_spoils", 102f);
  514:     }
  515: 
  516:     [TestMethod]
  517:     public void ApplyDailyTick_UpkeepExceedsEarning_SubtractsFromStorage()
  518:     {
  519:         _storage.Get("hero1", "war_spoils").Returns(100f);
  520:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  521:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  522:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 20) };
  523: 
  524:         _service.ApplyDailyTick("hero1", "empire_s", null, 0, troops);
  525:         _storage.Received(1).Add("hero1", "war_spoils", -6f);
  526:     }
  527: 
  528:     [TestMethod]
  529:     public void GetDailyEarning_ReturnsPerTownTimesCount()
  530:     {
  531:         Assert.AreEqual(1.5f, _service.GetDailyEarning("empire_s", null, 3));
  532:     }
  533: 
  534:     // ── Projected Daily Net (deficit warning) ──
  535: 
  536:     [TestMethod]
  537:     public void GetProjectedDailyNet_EarningExceedsUpkeep_ReturnsPositive()
  538:     {
  539:         // 4 towns * 0.5 = 2.0 earning, no upkeep troops → net +2.0
  540:         var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
  541:         Assert.AreEqual(2.0f, net, 0.001f);
  542:     }
  543: 
  544:     [TestMethod]
  545:     public void GetProjectedDailyNet_UpkeepExceedsEarning_ReturnsNegative()
  546:     {
  547:         // 0 towns → 0 earning; 20 troops * 0.3 = 6.0 upkeep → net -6.0
  548:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  549:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  550:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 20) };
  551: 
  552:         var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 0, troops);
  553: 
  554:         Assert.AreEqual(-6.0f, net, 0.001f);
  555:     }
  556: 
  557:     [TestMethod]
  558:     public void GetProjectedDailyNet_NoResource_ReturnsZero()
  559:     {
  560:         var net = _service.GetProjectedDailyNet("hero1", "nonexistent_kingdom", null, 4, new List<TroopUpkeepInfo>());
  561:         Assert.AreEqual(0f, net, 0.001f);
  562:     }
  563: 
  564:     [TestMethod]
  565:     public void GetProjectedDailyNet_AppliesCareerGainAndUpkeepModifiers()
  566:     {
  567:         // Mirrors ApplyDailyTick math: earning gets +CustomResourceGain, upkeep gets CustomResourceUpkeepModifier.
  568:         // Earning: 4 * 0.5 = 2.0, +20% = 2.4. Upkeep: 10 * 0.3 = 3.0, -50% = 1.5. Net = 2.4 - 1.5 = 0.9.
  569:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceGain).Returns(0.2f);
  570:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpkeepModifier).Returns(-0.5f);
  571:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  572:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  573:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };
  574: 
  575:         var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 4, troops);
  576: 
  577:         Assert.AreEqual(0.9f, net, 0.001f);
  578:     }
  579: 
  580:     // ── Pending Transaction ──
  581: 
  582:     [TestMethod]
  583:     public void QueueUpgradeSpend_DoesNotMutateStorage()
  584:     {
  585:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  586:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  587: 
  588:         _service.BeginPartyScreenSession();
  589:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  590: 
  591:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  592:         _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  593:     }
  594: 
  595:     [TestMethod]
  596:     public void GetAvailableAfterPending_SubtractsPendingFromStorage()
  597:     {
  598:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  599:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  600:         _storage.Get("hero1", "war_spoils").Returns(100f);
  601: 
  602:         _service.BeginPartyScreenSession();
  603:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  604: 
  605:         Assert.AreEqual(88f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
  606:     }
  607: 
  608:     [TestMethod]
  609:     public void CommitSession_AppliesPendingToStorage()
  610:     {
  611:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  612:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  613: 
  614:         _service.BeginPartyScreenSession();
  615:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  616:         _service.CommitSession("hero1", "empire_s", null);
  617: 
  618:         _storage.Received(1).Add("hero1", "war_spoils", -12f);
  619:     }
  620: 
  621:     [TestMethod]
  622:     public void CancelSession_DiscardsAndNeverMutatesStorage()
  623:     {
  624:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  625:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  626: 
  627:         _service.BeginPartyScreenSession();
  628:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  629:         _service.CancelSession();
  630: 
  631:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  632:     }
  633: 
  634:     [TestMethod]
  635:     public void ClampUpgradeCount_LimitsByAvailableMinusPending()
  636:     {
  637:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  638:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  639:         _storage.Get("hero1", "war_spoils").Returns(10f);
  640: 
  641:         _service.BeginPartyScreenSession();
  642:         Assert.AreEqual(2, _service.ClampUpgradeCount("hero1", "empire_s", null, "mordor_uruk_captain", 5));
  643:     }
  644: 
  645:     [TestMethod]
  646:     public void QueueUpgradeSpend_WithPassiveDiscount_DebitsEffectiveCost()
  647:     {
  648:         // Regression test for #174 / #194. Pre-fix, ClampUpgradeCount + CanAffordUpgrade + SpendForUpgrade
  649:         // all called GetEffectiveUpgradeCost (discounted), but QueueUpgradeSpend used the bare base cost.
  650:         // A career with -30% CustomResourceUpgradeCostModifier would let the player queue upgrades at
  651:         // the discounted gate, then get debited the full base price at CommitSession — silently
  652:         // overpaying by the discount percentage.
  653:         //
  654:         // The fix threads heroId through QueueUpgradeSpend so the queued + committed amount matches
  655:         // the gate's effective per-unit cost. Setup:
  656:         //   - Base 10 per unit, -30% career discount -> effective 7 per unit
  657:         //   - Queue 1 unit -> pending = 7 (pre-fix this was 10)
  658:         //   - CommitSession -> storage.Add(..., -7) (pre-fix: -10)
  659:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 10, dailyUpkeep: 0.2f);
  660:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  661:         _passiveService
  662:             .GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpgradeCostModifier)
  663:             .Returns(-0.30f);
  664: 
  665:         _service.BeginPartyScreenSession();
  666:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 1);
  667:         _service.CommitSession("hero1", "empire_s", null);
  668: 
  669:         _storage.Received(1).Add("hero1", "war_spoils", -7f);
  670:     }
  671: 
  672:     [TestMethod]
  673:     public void QueueUpgradeSpend_NoCareerDiscount_DebitsBaseCost()
  674:     {
  675:         // Negative-case partner to QueueUpgradeSpend_WithPassiveDiscount_DebitsEffectiveCost: confirms
  676:         // the fix doesn't accidentally change behavior when no discount is active (passive = 0).
  677:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 10, dailyUpkeep: 0.2f);
  678:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  679:         // No _passiveService.Returns(...) configured — Substitute.For default-returns 0f.
  680: 
  681:         _service.BeginPartyScreenSession();
  682:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  683:         _service.CommitSession("hero1", "empire_s", null);
  684: 
  685:         _storage.Received(1).Add("hero1", "war_spoils", -30f);
  686:     }
  687: 
  688:     [TestMethod]
  689:     public void CommitSession_NoOp_WhenNotInSession()
  690:     {
  691:         _service.CommitSession("hero1", "empire_s", null);
  692:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  693:     }
  694: 
  695:     // ── Edge Cases ──
  696: 
  697:     [TestMethod]
  698:     public void EarnFromRaid_NoOp_WhenBothIdsNull()
  699:     {
  700:         _service.EarnFromRaid("hero1", null, null);
  701:         _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  702:     }
  703: 
  704:     [TestMethod]
  705:     public void InitializeHero_SetsStartingAmount()
  706:     {
  707:         _service.InitializeHero("hero1", "empire_s", null);
  708:         _storage.Received(1).Set("hero1", "war_spoils", 30f);
  709:     }
  710: 
  711:     [TestMethod]
  712:     public void InitializeHero_WorksViaCulture()
  713:     {
  714:         _service.InitializeHero("hero1", null, "mordor");
  715:         _storage.Received(1).Set("hero1", "war_spoils", 30f);
  716:     }
  717: 
  718:     [TestMethod]
  719:     public void GetCurrentAmount_ReturnsZero_WhenNoResourceResolved()
  720:     {
  721:         _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
  722:         _config.GetByCultureId("gondor").Returns((SpecialResource)null);
  723:         Assert.AreEqual(0f, _service.GetCurrentAmount("hero1", "empire_w", "gondor"));
  724:     }
  725: 
  726:     // ── Desertion ──
  727: 
  728:     [TestMethod]
  729:     public void CalculateDesertion_BalanceAboveZero_ReturnsEmpty()
  730:     {
  731:         _storage.Get("hero1", "war_spoils").Returns(10f);
  732:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 20) };
  733: 
  734:         var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);
  735: 
  736:         Assert.AreEqual(0, result.Count);
  737:     }
  738: 
  739:     [TestMethod]
  740:     public void CalculateDesertion_BalanceZero_Deserts10Percent()
  741:     {
  742:         _storage.Get("hero1", "war_spoils").Returns(0f);
  743:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 20) };
  744: 
  745:         var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);
  746: 
  747:         Assert.AreEqual(1, result.Count);
  748:         Assert.AreEqual("mordor_uruk_darkblade", result[0].TroopId);
  749:         Assert.AreEqual(2, result[0].DesertCount); // 10% of 20 = 2
  750:     }
  751: 
  752:     [TestMethod]
  753:     public void CalculateDesertion_BalanceZero_MinimumOnePerType()
  754:     {
  755:         _storage.Get("hero1", "war_spoils").Returns(0f);
  756:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 3) };
  757: 
  758:         var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);
  759: 
  760:         Assert.AreEqual(1, result.Count);
  761:         Assert.AreEqual(1, result[0].DesertCount); // 10% of 3 = 0.3 → min 1
  762:     }
  763: 
  764:     [TestMethod]
  765:     public void CalculateDesertion_NoTroops_ReturnsEmpty()
  766:     {
  767:         _storage.Get("hero1", "war_spoils").Returns(0f);
  768:         var troops = new List<TroopUpkeepInfo>();
  769: 
  770:         var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);
  771: 
  772:         Assert.AreEqual(0, result.Count);
  773:     }
  774: 
  775:     [TestMethod]
  776:     public void CalculateDesertion_NoResource_ReturnsEmpty()
  777:     {
  778:         _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
  779:         _config.GetByCultureId("gondor").Returns((SpecialResource)null);
  780:         var troops = new List<TroopUpkeepInfo> { new("gondor_knight", 10) };
  781: 
  782:         var result = _service.CalculateDesertion("hero1", "empire_w", "gondor", troops);
  783: 
  784:         Assert.AreEqual(0, result.Count);
  785:     }
  786: 
  787:     [TestMethod]
  788:     public void CalculateDesertion_MultipleTroopTypes_DesertsEach()
  789:     {
  790:         _storage.Get("hero1", "war_spoils").Returns(0f);
  791:         var troops = new List<TroopUpkeepInfo>
  792:         {
  793:             new("mordor_uruk_darkblade", 10),
  794:             new("mordor_uruk_deathwarden", 5)
  795:         };
  796: 
  797:         var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);
  798: 
  799:         Assert.AreEqual(2, result.Count);
  800:         Assert.AreEqual(1, result[0].DesertCount); // 10% of 10 = 1
  801:         Assert.AreEqual(1, result[1].DesertCount); // 10% of 5 = 0.5 → min 1
  802:     }
  803: 
  804:     // ── Career Passive Integration ──
  805: 
  806:     [TestMethod]
  807:     public void ApplyDailyTick_CustomResourceGain_ScalesEarning()
  808:     {
  809:         // 0.2 = 20% bonus to resource gain
  810:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceGain).Returns(0.2f);
  811:         _storage.Get("hero1", "war_spoils").Returns(100f);
  812: 
  813:         _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
  814: 
  815:         // Base earning = 0.5 * 4 = 2.0, with 20% bonus = 2.4
  816:         _storage.Received(1).Set("hero1", "war_spoils", 102.4f);
  817:     }
  818: 
  819:     [TestMethod]
  820:     public void ApplyDailyTick_NoCareerPassive_EarningUnchanged()
  821:     {
  822:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceGain).Returns(0f);
  823:         _storage.Get("hero1", "war_spoils").Returns(100f);
  824: 
  825:         _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
  826: 
  827:         // Base earning = 0.5 * 4 = 2.0, no bonus
  828:         _storage.Received(1).Set("hero1", "war_spoils", 102f);
  829:     }
  830: 
  831:     [TestMethod]
  832:     public void GetDailyUpkeep_CustomResourceUpkeepModifier_ReducesUpkeep()
  833:     {
  834:         // -0.25 = 25% upkeep reduction
  835:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpkeepModifier).Returns(-0.25f);
  836:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  837:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  838:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };
  839: 
  840:         var result = _service.GetDailyUpkeep(troops, "hero1");
  841: 
  842:         // Base upkeep = 0.3 * 10 = 3.0, with -25% modifier = 2.25
  843:         Assert.AreEqual(2.25f, result, 0.001f);
  844:     }
  845: 
  846:     [TestMethod]
  847:     public void GetDailyUpkeep_NoCareerPassive_UpkeepUnchanged()
  848:     {
  849:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpkeepModifier).Returns(0f);
  850:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  851:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  852:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };
  853: 
  854:         var result = _service.GetDailyUpkeep(troops, "hero1");
  855: 
  856:         Assert.AreEqual(3.0f, result, 0.001f);
  857:     }
  858: 
  859:     [TestMethod]
  860:     public void GetDailyUpkeep_NullHeroId_UpkeepUnmodified()
  861:     {
  862:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  863:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  864:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };
  865: 
  866:         var result = _service.GetDailyUpkeep(troops, null);
  867: 
  868:         Assert.AreEqual(3.0f, result, 0.001f);
  869:     }
  870: 
  871:     [TestMethod]
  872:     public void SpendForUpgrade_CustomResourceUpgradeCostModifier_ReducesCost()
  873:     {
  874:         // -0.3 = 30% cost reduction
  875:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpgradeCostModifier).Returns(-0.3f);
  876:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 10, 0.2f);
  877:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  878: 
  879:         _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 2);
  880: 
  881:         // Base cost = 10 * 2 = 20, with -30% modifier = 14
  882:         _storage.Received(1).Add("hero1", "war_spoils", -14f);
  883:     }
  884: 
  885:     [TestMethod]
  886:     public void ClampUpgradeCount_CustomResourceUpgradeCostModifier_AllowsMore()
  887:     {
  888:         // -0.5 = 50% cost reduction
  889:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpgradeCostModifier).Returns(-0.5f);
  890:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  891:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  892:         _storage.Get("hero1", "war_spoils").Returns(10f);
  893: 
  894:         _service.BeginPartyScreenSession();
  895:         // At cost 4 with -50% = effective cost 2, available 10 → can afford 5
  896:         Assert.AreEqual(5, _service.ClampUpgradeCount("hero1", "empire_s", null, "mordor_uruk_captain", 5));
  897:     }
  898: 
  899:     [TestMethod]
  900:     public void SpendForUpgrade_NoCareerPassive_CostUnchanged()
  901:     {
  902:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpgradeCostModifier).Returns(0f);
  903:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  904:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  905: 
  906:         _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 3);
  907: 
  908:         _storage.Received(1).Add("hero1", "war_spoils", -12f);
  909:     }
  910: 
  911:     [TestMethod]
  912:     public void ApplyDailyTick_UpkeepModifier_AffectsNetCalculation()
  913:     {
  914:         // Earning: 4 towns * 0.5 = 2.0 (no gain modifier)
  915:         // Upkeep: 10 troops * 0.3 = 3.0, with -50% modifier = 1.5
  916:         // Net: 2.0 - 1.5 = 0.5 (positive, should AddCapped)
  917:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceGain).Returns(0f);
  918:         _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.CustomResourceUpkeepModifier).Returns(-0.5f);
  919:         _storage.Get("hero1", "war_spoils").Returns(100f);
  920:         var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
  921:         _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
  922:         var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };
  923: 
  924:         _service.ApplyDailyTick("hero1", "empire_s", null, 4, troops);
  925: 
  926:         // Net = 2.0 - 1.5 = 0.5 → 100 + 0.5 = 100.5
  927:         _storage.Received(1).Set("hero1", "war_spoils", 100.5f);
  928:     }
  929: 
  930:     // ── ResetSessionState (Phase 9b deferred #133 P2 R1) ──
  931:     //
  932:     // Service is registered as Reuse.Singleton — its private state (_loggedResolveKeys,
  933:     // _pendingSpend, _inSession) survives across new-campaign-in-same-process boundaries.
  934:     // ResetSessionState wipes that state so a second campaign doesn't inherit a stale
  935:     // _inSession=true (which would let an orphaned CommitSession deduct from the new hero)
  936:     // or a stale _pendingSpend (which would be applied at the next CommitSession).
  937: 
  938:     [TestMethod]
  939:     public void ResetSessionState_ClearsPendingSpend()
  940:     {
  941:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  942:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  943:         _storage.Get("hero1", "war_spoils").Returns(100f);
  944: 
  945:         _service.BeginPartyScreenSession();
  946:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  947:         // Pre-reset: pending = 12 → GetAvailableAfterPending = 100 - 12 = 88.
  948:         Assert.AreEqual(88f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
  949: 
  950:         _service.ResetSessionState();
  951: 
  952:         // After reset: pending cleared → GetAvailableAfterPending == raw storage (100).
  953:         Assert.AreEqual(100f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
  954:     }
  955: 
  956:     [TestMethod]
  957:     public void ResetSessionState_ClearsInSession_CommitSessionBecomesNoOp()
  958:     {
  959:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
  960:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  961: 
  962:         _service.BeginPartyScreenSession();
  963:         _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
  964: 
  965:         _service.ResetSessionState();
  966: 
  967:         // After reset _inSession==false → CommitSession early-returns, no Add.
  968:         _service.CommitSession("hero1", "empire_s", null);
  969:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  970:     }
  971: 
  972:     [TestMethod]
  973:     public void ResetSessionState_ClearsLoggedResolveKeys_ReResolutionLogsAgain()
  974:     {
  975:         // First resolve logs once, second is deduped silent.
  976:         _service.ResolveResource("empire_s", null);
  977:         _logger.ClearReceivedCalls();
  978:         _service.ResolveResource("empire_s", null);
  979:         _logger.DidNotReceive().LogDebug(Arg.Any<string>());
  980: 
  981:         // After reset, the dedupe key set is cleared — same call logs again.
  982:         _service.ResetSessionState();
  983:         _logger.ClearReceivedCalls();
  984:         _service.ResolveResource("empire_s", null);
  985:         _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
  986:     }
  987: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines = Get-Content -Path TAOM.Tests'"\\Features\\SpecialResources\\SpecialResourceServiceTests.cs; for ("'$i=377; $i -le 510; $i++) { if ($i -le $lines.Count) { '"'{0,5}: {1}' -f "'$i,$lines[$i-1] } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 528ms:
  377:     // ── Merchant Purchase (Elite Emissary) ──
  378:     // merchant_cost is a SEPARATE field from recruit_cost so the emissary never collides with the
  379:     // volunteer gate. The charged resource is resolved from the SETTLEMENT OWNER's faction (the
  380:     // kingdom/culture args), not the player's clan.
  381: 
  382:     [TestMethod]
  383:     public void CanAffordMerchantPurchase_BalanceAboveCost_ReturnsTrue()
  384:     {
  385:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  386:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  387:         _storage.Get("hero1", "war_spoils").Returns(100f);
  388: 
  389:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 ≤ 100
  390:     }
  391: 
  392:     [TestMethod]
  393:     public void CanAffordMerchantPurchase_BalanceBelowCost_ReturnsFalse()
  394:     {
  395:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  396:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  397:         _storage.Get("hero1", "war_spoils").Returns(80f);
  398: 
  399:         Assert.IsFalse(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 > 80
  400:     }
  401: 
  402:     [TestMethod]
  403:     public void CanAffordMerchantPurchase_BalanceEqualsCost_ReturnsTrue()
  404:     {
  405:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  406:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  407:         _storage.Get("hero1", "war_spoils").Returns(90f);
  408: 
  409:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 == 90
  410:     }
  411: 
  412:     [TestMethod]
  413:     public void CanAffordMerchantPurchase_NoMerchantCost_AllowsByDefault()
  414:     {
  415:         // An upgrade/upkeep-only entry (merchant_cost omitted) is not an emissary offer; afford-allow
  416:         // so the gate decision lives in the offer-list builder, not here.
  417:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f);
  418:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  419:         _storage.Get("hero1", "war_spoils").Returns(0f);
  420: 
  421:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 5));
  422:     }
  423: 
  424:     [TestMethod]
  425:     public void CanAffordMerchantPurchase_ZeroCount_AllowsByDefault()
  426:     {
  427:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  428:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  429:         _storage.Get("hero1", "war_spoils").Returns(0f);
  430: 
  431:         Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0));
  432:     }
  433: 
  434:     [TestMethod]
  435:     public void ChargeMerchantPurchase_DeductsMerchantCostTimesCount_FromOwnerResource()
  436:     {
  437:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  438:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  439: 
  440:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 2);
  441: 
  442:         _storage.Received(1).Add("hero1", "war_spoils", -60f);
  443:     }
  444: 
  445:     [TestMethod]
  446:     public void ChargeMerchantPurchase_NoMerchantCost_NoOp()
  447:     {
  448:         // recruit_cost set but merchant_cost 0 — must NOT deduct (proves the two economies don't cross).
  449:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 0);
  450:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  451: 
  452:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 2);
  453: 
  454:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  455:     }
  456: 
  457:     [TestMethod]
  458:     public void ChargeMerchantPurchase_NoCostEntry_NoOp()
  459:     {
  460:         _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
  461: 
  462:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "plain_troop", 1);
  463: 
  464:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  465:     }
  466: 
  467:     [TestMethod]
  468:     public void ChargeMerchantPurchase_NoResolvedResource_NoOp()
  469:     {
  470:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  471:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  472: 
  473:         _service.ChargeMerchantPurchase("hero1", "unmapped_kingdom", null, "mordor_uruk_captain", 1);
  474: 
  475:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  476:     }
  477: 
  478:     [TestMethod]
  479:     public void ChargeMerchantPurchase_ZeroCount_NoOp()
  480:     {
  481:         var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
  482:         _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
  483: 
  484:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0);
  485: 
  486:         _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
  487:     }
  488: 
  489:     [TestMethod]
  490:     public void RecruitVsMerchant_SameTroopWithBothCosts_ChargeIndependentFields()
  491:     {
  492:         // The headline "never double-charged" invariant, load-bearing only for a troop that is BOTH a
  493:         // recruitable volunteer AND an emissary offer (harad_elephant_rider / taom_spider_creature):
  494:         // the volunteer path charges recruit_cost, the emissary path charges merchant_cost — never the
  495:         // other's field. Deep-review 2026-06-25 (completeness critic gap #5).
  496:         var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 70);
  497:         _config.GetTroopCost("harad_elephant_rider").Returns(cost);
  498: 
  499:         _service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 1);
  500:         _storage.Received(1).Add("hero1", "war_spoils", -50f);   // volunteer path → recruit_cost
  501: 
  502:         _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 1);
  503:         _storage.Received(1).Add("hero1", "war_spoils", -70f);   // emissary path → merchant_cost
  504:     }
  505: 
  506:     // ── Daily Tick ──
  507: 
  508:     [TestMethod]
  509:     public void ApplyDailyTick_EarningExceedsUpkeep_AddsCapped()
  510:     {

codex
The SpecialResources methods keep `recruit_cost` and `merchant_cost` separate in code and tests. One thing to watch is integer multiplication before charge; I’m checking whether any user-controlled quantity can exceed the picker bounds or invoke the service with very large counts.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\IoC.cs,Main\\SubModule.cs,Main\\Features\\TaomSettings.cs,Main\\_Module\\SubModule.xml -Pattern \"EliteEmissary|EnableEliteEmissary|HideEmissary|taom_emissary\" -CaseSensitive | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData\\Languages -Recurse -Filter \"*emissary*\" | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main'"\\_Module\\ModuleData\\taom_emissary_strings.xml | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 530ms:
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:41: using TAOM.Features.EliteEmissary;
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:108:         EliteEmissaryIoC.RegisterEliteEmissaryFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:556:             // EliteEmissary — buy a faction's elite troops for its special resource at key settlements.
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:558:             campaignStarter.AddBehavior(new Features.EliteEmissary.Hooks.EliteEmissaryBehavior(
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:559:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissaryService>(),
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:560:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissarySettingsProvider>(),
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:561:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissaryConfigProvider>(),
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:60:     public bool EnableEliteEmissary { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:65:     public bool HideEmissaryWhenNoResource { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:836:     <!-- Elite Emissary player-facing text (EliteEmissary feature) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\SubModule.xml:838:       <XmlName id="GameText" path="taom_emissary_strings"/>

 succeeded in 513ms:
    1: <?xml version="1.0" encoding="utf-8"?>
    2: <strings>
    3: 
    4: 	<!-- Menu + dialog -->
    5: 	<string id="taom_emissary_menu" text="{=taom_emissary_menu}Speak with the faction emissary" />
    6: 	<string id="taom_emissary_no_trade" text="{=taom_emissary_no_trade}There is no elite trade for this faction here." />
    7: 	<string id="taom_emissary_greet" text="{=taom_emissary_greet}You seek the finest warriors our people can offer? Name them, and they are yours — for the right price." />
    8: 	<string id="taom_emissary_buy" text="{=taom_emissary_buy}I wish to purchase elite units." />
    9: 	<string id="taom_emissary_leave" text="{=taom_emissary_leave}Not today." />
   10: 
   11: 	<!-- Troop list -->
   12: 	<string id="taom_emissary_list_title" text="{=taom_emissary_list_title}Elite Units" />
   13: 	<string id="taom_emissary_list_desc" text="{=taom_emissary_list_desc}Balance: {AMOUNT} {RESOURCE}" />
   14: 	<string id="taom_emissary_offer_line" text="{=taom_emissary_offer_line}{NAME} — {COST} {RESOURCE}" />
   15: 	<string id="taom_emissary_offer_afford" text="{=taom_emissary_offer_afford}You can afford up to {MAX}." />
   16: 	<string id="taom_emissary_offer_cant" text="{=taom_emissary_offer_cant}Not enough {RESOURCE}." />
   17: 	<string id="taom_emissary_select" text="{=taom_emissary_select}Select" />
   18: 	<string id="taom_emissary_back" text="{=taom_emissary_back}Back" />
   19: 
   20: 	<!-- Quantity picker -->
   21: 	<string id="taom_emissary_qty_title" text="{=taom_emissary_qty_title}How many {TROOP}?" />
   22: 	<string id="taom_emissary_qty_desc" text="{=taom_emissary_qty_desc}Each costs {COST} {RESOURCE}. You have {BALANCE}." />
   23: 	<string id="taom_emissary_qty_line" text="{=taom_emissary_qty_line}{QTY} (×{COST} = {TOTAL} {RESOURCE})" />
   24: 	<string id="taom_emissary_confirm" text="{=taom_emissary_confirm}Recruit" />
   25: 
   26: 	<!-- Outcomes -->
   27: 	<string id="taom_emissary_bought" text="{=taom_emissary_bought}Recruited {QTY} {TROOP} for {COST} {RESOURCE}." />
   28: 	<string id="taom_emissary_cant_afford" text="{=taom_emissary_cant_afford}Not enough {RESOURCE} — need {COST}." />
   29: 	<string id="taom_emissary_no_resource" text="{=taom_emissary_no_resource}There is no emissary trade in this settlement." />
   30: 	<string id="taom_emissary_no_offers" text="{=taom_emissary_no_offers}The emissary has no elite units to offer right now." />
   31: 	<string id="taom_emissary_failed" text="{=taom_emissary_failed}The emissary could not complete the deal." />
   32: 
   33: </strings>

 succeeded in 530ms:

[32;1mFullName                                                                                                   [0m[32;1m Length[0m
[32;1m--------                                                                                                   [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\BR\std_taom_emissary_strings_por-BR.xml     279
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\CNs\std_taom_emissary_strings_zho-CN.xml    275
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\CNt\std_taom_emissary_strings_zho-HK.xml    276
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\DE\std_taom_emissary_strings_deu-DE.xml     263
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_emissary_strings_fre-FR.xml     263
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\IT\std_taom_emissary_strings_ita-IT.xml     264
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\JP\std_taom_emissary_strings_jpn-JP.xml     265
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\KO\std_taom_emissary_strings_kor-KO.xml     263
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_emissary_strings_pol-PL.xml     263
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\RU\std_taom_emissary_strings_rus-RU.xml     264
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\SP\std_taom_emissary_strings_spa-LA.xml     269
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\TR\std_taom_emissary_strings_tur-TR.xml     264


codex
Localization keys used by the code are present in the base `taom_emissary_strings.xml`; I’ll also verify language registration and the exact SubModule XML wiring, since menu/dialog text is easy to leave unregistered.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines=Get-Content Main'"\\IoC.cs; for("'$i=95;$i -le 112;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines=Get-Content Main'"\\SubModule.cs; for("'$i=545;$i -le 566;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines=Get-Content Main'"\\Features\\TaomSettings.cs; for("'$i=50;$i -le 70;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines=Get-Content Main'"\\_Module\\SubModule.xml; for("'$i=830;$i -le 842;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 568ms:
  545:                 IoC.Resolve<ITownRosterAdapter>(),
  546:                 IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
  547:                 IoC.Resolve<IModLogger>()));
  548: 
  549:             // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
  550:             // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
  551:             // Registered unconditionally so the MCM master toggle takes effect at runtime.
  552:             campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
  553:                 IoC.Resolve<ICastleRecruitmentService>(),
  554:                 IoC.Resolve<IModLogger>()));
  555: 
  556:             // EliteEmissary — buy a faction's elite troops for its special resource at key settlements.
  557:             // Registered unconditionally so the MCM master toggle takes effect at runtime; stateless (no SyncData).
  558:             campaignStarter.AddBehavior(new Features.EliteEmissary.Hooks.EliteEmissaryBehavior(
  559:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissaryService>(),
  560:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissarySettingsProvider>(),
  561:                 IoC.Resolve<Features.EliteEmissary.IEliteEmissaryConfigProvider>(),
  562:                 IoC.Resolve<ISettlementOwnerAdapter>(),
  563:                 IoC.Resolve<IModLogger>()));
  564: 
  565:             // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
  566:             // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion

 succeeded in 567ms:
   95:         MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
   96:         ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
   97:         SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
   98:         ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
   99:         TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
  100:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
  101:         CareerSystemIoC.RegisterCareerSystemFeature(container);
  102:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
  103:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
  104:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
  105:         SettlementFoodIoC.RegisterSettlementFoodFeature(container);
  106:         BanditManagementIoC.RegisterBanditManagementFeature(container);
  107:         CastleRecruitmentIoC.RegisterCastleRecruitmentFeature(container);
  108:         EliteEmissaryIoC.RegisterEliteEmissaryFeature(container);
  109:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
  110:         MixedFormationsIoC.RegisterMixedFormationsFeature(container);
  111:         SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
  112:         QuickActionsIoC.RegisterQuickActionsFeature(container);

 succeeded in 556ms:
  830:       <XmlName id="GameText" path="taom_lotr_issue_strings"/>
  831:       <IncludedGameTypes>
  832:         <GameType value="Campaign"/>
  833:         <GameType value="CampaignStoryMode"/>
  834:       </IncludedGameTypes>
  835:     </XmlNode>
  836:     <!-- Elite Emissary player-facing text (EliteEmissary feature) -->
  837:     <XmlNode>
  838:       <XmlName id="GameText" path="taom_emissary_strings"/>
  839:       <IncludedGameTypes>
  840:         <GameType value="Campaign"/>
  841:         <GameType value="CampaignStoryMode"/>
  842:       </IncludedGameTypes>

 succeeded in 590ms:
   50:     [SettingPropertyGroup("Castle Recruitment")]
   51:     [SettingPropertyInteger("Notables Per Castle", 1, 5, Order = 2,
   52:         HintText = "How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
   53:     public int CastleNotablesPerCastle { get; set; } = 3;
   54: 
   55:     // --- Elite Emissary ---
   56: 
   57:     [SettingPropertyGroup("Elite Emissary")]
   58:     [SettingPropertyBool("Enable Elite Emissary", Order = 0,
   59:         HintText = "At a faction's key settlements (capitals), speak with the faction emissary to buy that faction's elite troops for its special resource (Castar, War Spoils, Gems...). Conquering a settlement flips its offerings to the new owner.")]
   60:     public bool EnableEliteEmissary { get; set; } = true;
   61: 
   62:     [SettingPropertyGroup("Elite Emissary")]
   63:     [SettingPropertyBool("Hide Emissary Without Resource", Order = 1,
   64:         HintText = "When on, the emissary option is hidden at settlements whose owner faction has no special resource. When off, the option still appears but is disabled with an explanatory hint.")]
   65:     public bool HideEmissaryWhenNoResource { get; set; } = true;
   66: 
   67:     // --- Culture Conversion ---
   68: 
   69:     [SettingPropertyGroup("Culture Conversion")]
   70:     [SettingPropertyBool("Enable Culture Conversion", Order = 0,

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$lines=Get-Content Main'"\\_Module\\SubModule.xml; for("'$i=837;$i -le 846;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 523ms:
  837:     <XmlNode>
  838:       <XmlName id="GameText" path="taom_emissary_strings"/>
  839:       <IncludedGameTypes>
  840:         <GameType value="Campaign"/>
  841:         <GameType value="CampaignStoryMode"/>
  842:       </IncludedGameTypes>
  843:     </XmlNode>
  844:     <!-- Loc keys harvested from XSLT transforms (kingdom/culture/clan/lord/hero descriptions) -->
  845:     <XmlNode>
  846:       <XmlName id="GameText" path="taom_xslt_strings"/>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter settlements.xml | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Include \"taom_spcultures.xml\",\"TAOM_spkingdoms.xml\",\"*troops*.xml\",\"*.xml\" | Where-Object { "'$_.Name -match '"'culture|kingdom|troop|settlement|character' } | Select-Object FullName,Length | Sort-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 609ms:
 succeeded in 625ms:

[32;1mFullName                                                                                                   [0m[32;1m Length[0m
[32;1m--------                                                                                                   [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\culture_marketplace\culture_marketplace_config.xml   2454
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml                              51752
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml       7117
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\troop_resource_costs.xml           7386
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_education_character_templates.xml              46977
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml                                290818
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml                                 52215
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_dale.xml                              79049
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_dolguldur.xml                        167570
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_dunland.xml                          116941
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_erebor.xml                           258874
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_goblin.xml                            85888
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_gondor.xml                           313558
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_gundabad.xml                         127505
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_harad.xml                             74757
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_isengard.xml                         121185
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_mirkwood.xml                          48858
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_mistymountainorcs.xml                 86862
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_mordor.xml                           132470
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_rhun_new.xml                         241782
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_rivendell.xml                        430071
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_rohan.xml                            211568
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_umbar.xml                             74853
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml                       7057


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\*.xml,Main\\_Module\\ModuleData\\**\\*.xml -Pattern 'town_EW1|town_ES1|town_E1|town_DG1|town_isengard|town_G1|town_M1|town_R1|town_V1|town_RU1|town_A1' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 633ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:6: <Settlement id="town_isengard" name="{=Settlements.Settlement.name.town_isengard}Isengard" owner="Faction.clan_isengard_1" posX="0" posY="0" culture="Culture.isengard" gate_posX="0" gate_posY="0" text="{=Settlements.Settlement.text.town_isengard}The fortress of Isengard, domain of Saruman the White.">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:26: <Location id="center" scene_name="town_isengard" scene_name_1="town_isengard" scene_name_2="town_isengard" scene_name_3="town_isengard" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:44: <Village id="village_comp_isengard_a" village_type="VillageType.wheat_farm" hearth="300" max_prosperity="3000" gate_rotation="0.008" bound="Settlement.town_isengard" background_crop_position="0.0" background_mesh="gui_bg_village_empire" wait_mesh="wait_empire_village" castle_background_mesh="gui_bg_castle_empire" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:7: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:94: initial_home_settlement="Settlement.town_R1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:180: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:354: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:445: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:627: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml:813: initial_home_settlement="Settlement.town_A14"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:12: initial_home_settlement="Settlement.town_EW1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:26: initial_home_settlement="Settlement.town_EW1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:40: initial_home_settlement="Settlement.town_EW1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:54: initial_home_settlement="Settlement.town_EW1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:68: initial_home_settlement="Settlement.town_EW1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:86: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:99: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:112: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:125: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:138: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:151: initial_home_settlement="Settlement.town_ES1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:170: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:183: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:196: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:209: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:222: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:235: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:248: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:261: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:274: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:287: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:300: initial_home_settlement="Settlement.town_V1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:459: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:474: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:489: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:504: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:519: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:534: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:549: initial_home_settlement="Settlement.town_E1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:569: initial_home_settlement="Settlement.town_R1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:583: initial_home_settlement="Settlement.town_R1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:598: initial_home_settlement="Settlement.town_R1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:618: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:633: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:648: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:663: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:678: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:693: initial_home_settlement="Settlement.town_M1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:732: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:747: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:762: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:777: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:792: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:807: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:822: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:837: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:852: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:867: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:882: initial_home_settlement="Settlement.town_isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:902: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:917: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:932: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:947: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:962: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:977: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:992: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1007: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1022: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1037: initial_home_settlement="Settlement.town_G1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1181: initial_home_settlement="Settlement.town_A10"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1194: initial_home_settlement="Settlement.town_A11"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1266: initial_home_settlement="Settlement.town_A14"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1279: initial_home_settlement="Settlement.town_A12"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1292: initial_home_settlement="Settlement.town_A13"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1377: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1392: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1407: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1422: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1437: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1452: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1467: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1482: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1497: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1512: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1527: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1542: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1557: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1572: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\clans.xml:1587: initial_home_settlement="Settlement.town_DG1"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:12: <Settlement id="town_EW1" />       <!-- Minas Tirith (Gondor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:13: <Settlement id="town_ES1" />       <!-- Barad Dûr (Mordor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:14: <Settlement id="town_E1" />        <!-- Erebor -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:15: <Settlement id="town_DG1" />       <!-- Dol Guldur -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:16: <Settlement id="town_isengard" />  <!-- Orthanc (Isengard) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:17: <Settlement id="town_G1" />        <!-- Mount Gundabad -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:18: <Settlement id="town_M1" />        <!-- Felegoth (Mirkwood) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:19: <Settlement id="town_R1" />        <!-- Rivendell -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:20: <Settlement id="town_V1" />        <!-- Edoras (Rohan) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:21: <Settlement id="town_RU1" />       <!-- Mistrand (Rhun) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:22: <Settlement id="town_A1" />        <!-- Korb Taskral (Harad) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:6: <!-- Minas Tirith (town_EW1) — Citadel Guards & Fountain Guards -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Directory | Where-Object { "'$_.FullName -match '"'Map|TAOM|settlement|world|map' } | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 578ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\bandit_management
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\castle_recruitment
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\clan_heraldry
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\configs
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\culture_conversion
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\culture_marketplace
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\diplomacy
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\execution
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\factionmap
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\messengers
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\named_companions
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\naval_travel
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\raceage
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\recruitment_alignment
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\recruitment_pools
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\shader_precompilation
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\siege
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\startup_resources
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\BR
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\CNs
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\CNt
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\DE
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\IT
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\JP
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\KO
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\RU
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\SP
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\TR


codex
For key-settlement existence, the local `ModuleData` only contains custom Isengard directly; the other capital IDs appear as initial home references, not settlement definitions. I’m expanding the search to the repo/cache so I don’t falsely flag valid TAOM map IDs as missing.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path . -Recurse -File -Include *.xml -ErrorAction SilentlyContinue | Select-String -Pattern '<Settlement id=\"town_EW1\"|<Settlement id=\"town_ES1\"|<Settlement id=\"town_E1\"|<Settlement id=\"town_DG1\"|<Settlement id=\"town_G1\"|<Settlement id=\"town_M1\"|<Settlement id=\"town_R1\"|<Settlement id=\"town_V1\"|<Settlement id=\"town_RU1\"|<Settlement id=\"town_A1\"|<Town id=\"town_EW1\"|<Town id=\"town_ES1\"|<Town id=\"town_E1\"|<Town id=\"town_DG1\"|<Town id=\"town_G1\"|<Town id=\"town_M1\"|<Town id=\"town_R1\"|<Town id=\"town_V1\"|<Town id=\"town_RU1\"|<Town id=\"town_A1\"' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
2026-06-25T15:28:17.728214Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 10 seconds
Output:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:12: <Settlement id="town_EW1" />       <!-- Minas Tirith (Gondor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:13: <Settlement id="town_ES1" />       <!-- Barad Dûr (Mordor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:14: <Settlement id="town_E1" />        <!-- Erebor -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:15: <Settlement id="town_DG1" />       <!-- Dol Guldur -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:17: <Settlement id="town_G1" />        <!-- Mount Gundabad -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:18: <Settlement id="town_M1" />        <!-- Felegoth (Mirkwood) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:19: <Settlement id="town_R1" />        <!-- Rivendell -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:20: <Settlement id="town_V1" />        <!-- Edoras (Rohan) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:21: <Settlement id="town_RU1" />       <!-- Mistrand (Rhun) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:22: <Settlement id="town_A1" />        <!-- Korb Taskral (Harad) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">

 exited 1 in 10044ms:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:12: <Settlement id="town_EW1" />       <!-- Minas Tirith (Gondor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:13: <Settlement id="town_ES1" />       <!-- Barad Dûr (Mordor) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:14: <Settlement id="town_E1" />        <!-- Erebor -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:15: <Settlement id="town_DG1" />       <!-- Dol Guldur -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:17: <Settlement id="town_G1" />        <!-- Mount Gundabad -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:18: <Settlement id="town_M1" />        <!-- Felegoth (Mirkwood) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:19: <Settlement id="town_R1" />        <!-- Rivendell -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:20: <Settlement id="town_V1" />        <!-- Edoras (Rohan) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:21: <Settlement id="town_RU1" />       <!-- Mistrand (Rhun) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:22: <Settlement id="town_A1" />        <!-- Korb Taskral (Harad) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_guards\settlement_guards_config.xml:7: <Settlement id="town_EW1">

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path C:\\Users\\mikew\\source\\repos\\TAOM -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -like '"'*TAOM*Map*' -or "'$_.FullName -like '"'*TAOM_Map*' -or "'$_.Name -eq '"'TAOM_Map' } | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
2026-06-25T15:28:24.310658Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:

 exited 1 in 902ms:
codex
The repo does not appear to include `TAOM_Map/settlements.xml`, so static settlement validation may be limited to IDs referenced in TAOM data plus the runtime `Settlement.Find` guard. I’ll keep looking in likely install/decompile locations before calling that out as unavailable evidence.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\ -Recurse -Filter settlements.xml -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path C:\\Users\\mikew -Recurse -Filter settlements.xml -ErrorAction SilentlyContinue | Select-Object -First 30 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\ -Directory -ErrorAction SilentlyContinue | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
2026-06-25T15:28:32.835841Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:

 exited 1 in 596ms:
 succeeded in 599ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\.blender_ext
E:\Achaea stuff
E:\alpha-nvidia-extension-main
E:\Bannerlord Online
E:\Bannerlord_Art
E:\Bannerlord_Launcher_Test
E:\BannerlordBackup
E:\Battle.net
E:\BC_HOA
E:\beards
E:\BL Example
E:\Blackmagic Design
E:\Blender
E:\Call of Duty
E:\Call of Duty Modern Warfare
E:\Cascadeur
E:\College
E:\ComfyUI_windows_portable_nvidia (1)
E:\Comission Completed
E:\DDS Viewer
E:\Decompiled_Bannerlord
E:\Decompiled_Bannerlord_pre_hotfix_20260529
E:\Decompiled_Bannerlord_v1.3.15
E:\Decompiled_Bannerlord_v1.4_OLD
E:\Decompiled_TOR
E:\DnSPY
E:\Documents
E:\Dwarf
E:\Dwarf_Voices
E:\EA
E:\Epic Games
E:\Harmony-2006-v2-3-6-220-1744894717
E:\JetBrains Rider 2024.3
E:\JS HELD
E:\Launcher NonSense
E:\LOTR BL CODE
E:\LOTRAOM Private
E:\LOTRAOM_Development
E:\LOTRAOM_GIT
E:\LOTRAOM_Releases
E:\LOTRAOMAssets
E:\mcp
E:\muddle-shadow-1.1.0
E:\Native
E:\New folder
E:\node.js
E:\OBS
E:\Path Of Titans
E:\PDFPW
E:\Podcasts_Video
E:\Recordings
E:\Release_v0.5.1
E:\repos
E:\reposDOTS
E:\ROT6.2
E:\Safee
E:\spider_anim_backup_2026-04-23_1715
E:\Steam
E:\STOP_Assets
E:\TAOM - UPDATE.tscproj
E:\TpacTool-Custom
E:\TTotal War
E:\UE_5.7
E:\VAMED
E:\Visual Studio
E:\World Creator
E:\World Machine


 succeeded in 5275ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Bannerlord_Art\CGHero\Mirkwood Lieutenant\Mirkwood Lieutenant\ModuleData\settlements.xml                            
E:\Bannerlord_Art\Rohan\Rohan_Royal_Guard\Rohan_Royal_Guard\LOTR Haterade_Editor_version\LOTR Haterade\ModuleData\sett…
E:\LOTRAOMAssets\1.3.0\SandBox\ModuleData\settlements.xml                                                              
E:\LOTRAOMAssets\A Dance of Dragons - Map\A Dance of Dragons - Map\ModuleData\settlements.xml                          
E:\LOTRAOMAssets\ADOD asset package\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml                        
E:\LOTRAOMAssets\adod half1\adod half1\A Dance of Dragons\A Dance of Dragons\ModuleData\settlements.xml                
E:\LOTRAOMAssets\adod half1\adod half1\A Dance of Dragons\ModuleData\settlements.xml                                   
E:\LOTRAOMAssets\AOM Umbar Expanded\AOM Umbar Expanded\ModuleData\settlements.xml                                      
E:\LOTRAOMAssets\Banners\_Oct\LOTRAOM\ModuleData\settlements.xml                                                       
E:\LOTRAOMAssets\Gondor\Sorush\horse_armour_pack\ModuleData\settlements.xml                                            
E:\LOTRAOMAssets\HelmsDeepAssetsv1\HelmsDeepAssetsv1\ModuleData\settlements.xml                                        
E:\LOTRAOMAssets\Highmoors Expansion-20251016T130652Z-1-001\Highmoors Expansion\settlements.xml                        
E:\LOTRAOMAssets\Horse Armor\Lord_sauron_horse_armour_pack\ModuleData\settlements.xml                                  
E:\LOTRAOMAssets\LOTRAOM\ModuleData\settlements.xml                                                                    
E:\LOTRAOMAssets\LOTR_Lemmy_Worldmap\LOTR_Lemmy_Worldmap\ModuleData\settlements.xml                                    
E:\LOTRAOMAssets\LOTR_Map\ModuleData\settlements.xml                                                                   
E:\LOTRAOMAssets\LOTR_Map (2)_comprehend_jan_26\LOTR_Map\ModuleData\settlements.xml                                    
E:\LOTRAOMAssets\Map\LOTR_Map_22\LOTR_Map\ModuleData\settlements.xml                                                   
E:\LOTRAOMAssets\Map\LOTR_Map_comprehend\LOTR_Map\ModuleData\settlements.xml                                           
E:\LOTRAOMAssets\ROT 8.0 Patron Version 2\ROT 8.0 Patron Version 2\ROT-Map\ModuleData\settlements.xml                  


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\ -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -eq '"'TAOM_Map' -or "'$_.FullName -like '"'*TAOM_Map*' } | Select-Object -First 20 FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1925ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\LOTRAOMAssets\TAOM_Map_170526
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Ma…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\LOTRAOMAssets\\TAOM_Development\\Modules -Directory -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -like '"'TAOM_Map*' } | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 557ms:
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\LOTRAOMAssets\\TAOM_Development\\Modules\\TAOM_Map\\ModuleData -Filter settlements.xml -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 549ms:

[32;1mFullName                                                                     [0m[32;1m  Length[0m
[32;1m--------                                                                     [0m [32;1m ------[0m
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml 1037348


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\LOTRAOMAssets\\TAOM_Development\\Modules\\TAOM_Map\\ModuleData\\settlements.xml -Pattern '<Settlement id=\"town_EW1\"|<Settlement id=\"town_ES1\"|<Settlement id=\"town_E1\"|<Settlement id=\"town_DG1\"|<Settlement id=\"town_isengard\"|<Settlement id=\"town_G1\"|<Settlement id=\"town_M1\"|<Settlement id=\"town_R1\"|<Settlement id=\"town_V1\"|<Settlement id=\"town_RU1\"|<Settlement id=\"town_A1\"' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 568ms:
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:1025: <Settlement id="town_ES1" name="{=Settlements.Settlement.name.town_ES1}Barad Dûr" owner="Faction.clan_empire_south_3" posX="1011.922" posY="724.509" culture="Culture.mordor" gate_posX="1010.242" gate_posY="728.3376" text="{=xkkI7ejQ}The frontier city of Danustica dominates the network of lakes and lagoons known as the Calsea. It has long held the line against incursions by the Aserai and various Darshi shahs and padishahs further to the east. Many an imperial legionary stationed here has scribbled graffiti on its walls cursing the city's sweltering summers, brackish water and the mosquitos that rise from the Calsea, but the inhabitants are proud of their role as border defenders and mock the soldiers wilting in the heat around them.">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:2382: <Settlement id="town_EW1" name="{=Settlements.Settlement.name.town_EW1}Minas Tirith" owner="Faction.clan_empire_west_1" posX="902.954" posY="681.31" culture="Culture.gondor" gate_posX="904.0729" gate_posY="679.5563" text="{==Settlements.Settlement.text.town_EW1}The Tower of Guard, Gondor's capital and greatest fortress-city, built in seven concentric levels on the Hill of Guard at the eastern end of the White Mountains with Mount Mindolluin behind it. The White City houses the Citadel with the Tower of Ecthelion, the Court of the Fountain with the White Tree, and serves as seat of the Stewards of Gondor (and later King Elessar). Surrounded by the fertile Pelennor Fields enclosed by the great defensive wall called the Rammas Echor.">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:4032: <Settlement id="town_A1" name="{=Settlements.Settlement.name.town_A1}Korb Taskral" owner="Faction.clan_aserai_1" posX="876.924" posY="437.36" culture="Culture.aserai" gate_posX="879.6564" gate_posY="438.8513" text="{=1ImuW3My}Quyaz was once a mercantile republic that dominated the trade between the western ocean and the Perassic Sea, speaking the now all-but-extinct Kannic tongue, a relative of Nahasawi. But imperial hegemony slowly snuffed out its trade, and the rulers was taken over by mercenaries from the Nahasa. It came intermittently under imperial subjugation but today is a prized holding of the Banu Hulyan, a leading clan of the Aserai sultanate.">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:6005: <Settlement id="town_V1" name="{=Settlements.Settlement.name.town_V1}Edoras" owner="Faction.clan_vlandia_1" posX="759.01" posY="744.8" culture="Culture.vlandia" gate_posX="759.4503" gate_posY="744.2111" text="{=aOn6pa0D}Sargot is a common name for hills, promontories, islands and other defensible positions in western Calradia. It refers to a fortified encampment of the Massa, a tribe allied to the the Vlandians who joined their invasion.  Though the Massa moved on, their stay was often so traumatic for a district that the name remained long after their departure. This Sargot was actually founded on the ruins of an imperial town, resettled by Vlandians, and which has supplanted the memory of the community that came before it.">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:8281: <Settlement id="town_DG1" name="{=Settlements.Settlement.name.town_DG1}Dol Guldur" owner="Faction.clan_dolguldur_1" posX="831.501" posY="974.3" culture="Culture.dolguldur" gate_posX="832.8876" gate_posY="980.6616">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:8874: <Settlement id="town_E1" name="{=Settlements.Settlement.name.town_E1}Erebor" owner="Faction.clan_erebor_1" posX="944.305" posY="1197.995" culture="Culture.erebor" gate_posX="944.689" gate_posY="1198.695">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:9505: <Settlement id="town_G1" name="{=Settlements.Settlement.name.town_G1}Mount Gundabad" owner="Faction.clan_gundabad_1" posX="724.044" posY="1241.576" culture="Culture.gundabad" gate_posX="728.6927" gate_posY="1241.575">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:9847: <Settlement id="town_isengard" name="{=Settlements.Settlement.name.town_isengard}Orthanc" owner="Faction.clan_isengard_1" posX="633.419" posY="831.216" culture="Culture.isengard" gate_posX="634.1232" gate_posY="831.858" text="{=Settlements.Settlement.text.town_isengard}The fortress of Isengard, domain of Saruman the White.">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:10455: <Settlement id="town_M1" name="{=Settlements.Settlement.name.town_M1}Felegoth" owner="Faction.clan_mirkwood_1" posX="898.174" posY="1195.003" culture="Culture.mirkwood" gate_posX="896.9339" gate_posY="1194.555">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:10873: <Settlement id="town_R1" name="{=Settlements.Settlement.name.town_R1}Rivendell" owner="Faction.clan_rivendell_1" posX="696.021" posY="1116.835" culture="Culture.rivendell" gate_posX="690.8975" gate_posY="1121.794">
E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml:11720: <Settlement id="town_RU1" name="{=Settlements.Settlement.name.town_RU1}Mistrand" owner="Faction.clan_khuzait_1" posX="1280.185" posY="888.889" culture="Culture.khuzait" gate_posX="1284.882" gate_posY="889.3673">

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$offerFile='"'Main\\_Module\\ModuleData\\elite_emissary\\elite_emissary_config.xml'; [xml]"'$offers=Get-Content $offerFile; $troopIds=@($offers.EliteEmissary.CultureOffers.Culture.Troop | ForEach-Object {$_.id}); $costFile='"'Main\\_Module\\ModuleData\\special_resources\\troop_resource_costs.xml'; [xml]"'$costs=Get-Content $costFile; $costIds=@($costs.TroopResourceCosts.Troop | ForEach-Object {$_.id}); $dup=$costIds | Group-Object | Where-Object Count -gt 1; '"'Offer troop count: '+"'$troopIds.Count; '"'Cost row count: '+"'$costIds.Count; '"'Duplicate cost ids: '+(("'$dup | ForEach-Object {$_.Name+'"':' + "'$_.Count}) -join '"', '); 'Offers missing merchant cost row: '+(("'$troopIds | Where-Object {$_ -notin $costIds}) -join '"', '); "'$cultureIds=@($offers.EliteEmissary.CultureOffers.Culture | ForEach-Object {$_.id}); [xml]$res=Get-Content '"'Main\\_Module\\ModuleData\\special_resources\\special_resources_config.xml'; "'$resCultures=@($res.SpecialResources.Resource.Culture | ForEach-Object {$_.id}); '"'Offer cultures: '+("'$cultureIds -join '"', '); 'Offer cultures without resource culture mapping: '+(("'$cultureIds | Where-Object {$_ -notin $resCultures}) -join '"', '); "'$settleIds=@($offers.EliteEmissary.KeySettlements.Settlement | ForEach-Object {$_.id}); $map='"'E:\\LOTRAOMAssets\\TAOM_Development\\Modules\\TAOM_Map\\ModuleData\\settlements.xml'; [xml]"'$settles=Get-Content $map; $mapIds=@($settles.Settlements.Settlement | ForEach-Object {$_.id}); '"'Key settlements missing in TAOM_Map: '+(("'$settleIds | Where-Object {$_ -notin $mapIds}) -join '"', '); "'$troopFiles=Get-ChildItem Main'"\\_Module\\ModuleData -Recurse -File -Include *.xml | Where-Object { "'$_.FullName -match '"'\\\\troops\\\\|\\\\characters\\\\|custom|creature|spider' }; "'$missing=@(); foreach($id in $troopIds){ $pat='"'id=\"'+[regex]::Escape("'$id)+'"'\"'; "'$found=Select-String -Path ($troopFiles.FullName) -Pattern $pat -SimpleMatch -ErrorAction SilentlyContinue | Select-Object -First 1; if(-not $found){ $missing += $id } }; '"'Offer troops missing in repo troop/character XML: '+("'$missing -join '"', ')" in C:\Users\mikew\source\repos\TAOM
 succeeded in 2564ms:
Offer troop count: 54
Cost row count: 60
Duplicate cost ids: 
Offers missing merchant cost row: 
Offer cultures: gondor, mordor, erebor, dolguldur, isengard, gundabad, mirkwood, rivendell, vlandia, khuzait, aserai
Offer cultures without resource culture mapping: 
Key settlements missing in TAOM_Map: 
Offer troops missing in repo troop/character XML: 

codex
The static XML cross-reference is clean: 54 offered troops, 54 matching merchant-cost rows, no duplicate cost IDs, all offer cultures map to resources, and all 11 key settlements resolve in `E:\LOTRAOMAssets\TAOM_Development\Modules\TAOM_Map\ModuleData\settlements.xml`. I’m moving to vanilla API semantics now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter CampaignMapConversation.cs -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MBInformationManager.cs -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter CampaignEvents.cs -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MultiSelectionInquiryData.cs -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 570ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\CampaignMapConversa…


 succeeded in 564ms:

[32;1mFullName                                                                             [0m[32;1m Length[0m
[32;1m--------                                                                             [0m [32;1m------[0m
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBInformationManager.cs   2966


 succeeded in 590ms:

[32;1mFullName                                                                                               [0m[32;1m Length[0m
[32;1m--------                                                                                               [0m [32;1m------[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs 123318


 succeeded in 609ms:

[32;1mFullName                                                                                  [0m[32;1m Length[0m
[32;1m--------                                                                                  [0m [32;1m------[0m
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MultiSelectionInquiryData.cs   2789


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MenuHelper.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter CampaignMapConversation.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter InquiryElement.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter CampaignGameStarter.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 581ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignGameStarter.cs

 succeeded in 644ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\CampaignMapConversation.cs

 succeeded in 644ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs

 succeeded in 641ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\InquiryElement.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\CampaignMapConversation.cs -Pattern \"OpenConversation|OpenMapConversation|EndConversation|ConversationEnded|StartConversation\" -Context 4,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs -Pattern \"ConversationEnded|OnConversationEnded|ConversationEndedEvent\" -Context 3,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\MenuHelper.cs -Pattern \"SetOptionProperties\" -Context 4,12 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 553ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\CampaignMapConversation.cs:5: 	public static void OpenConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  namespace TaleWorlds.CampaignSystem.Conversation;
  
  public static class CampaignMapConversation
  {
  	{
  		Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
  	}
  }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\CampaignMapConversation.cs:7: 		Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
  public static class CampaignMapConversation
  {
  	public static void OpenConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  	{
  	}
  }

 succeeded in 565ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:213: 	private readonly MbEvent<IEnumerable<CharacterObject>> _onConversationEnded = new MbEvent<IEnumerable<CharacterObject>>();
  
  	private readonly MbEvent<IAgent> _onAgentJoinedConversationEvent = new MbEvent<IAgent>();
  
  
  	private readonly MbEvent<MapEvent> _mapEventEnded = new MbEvent<MapEvent>();
  
  	private readonly MbEvent<MapEvent, PartyBase, PartyBase> _mapEventStarted = new MbEvent<MapEvent, PartyBase, PartyBase>();
  
  	private readonly MbEvent<Settlement, FlattenedTroopRoster, Hero, bool> _prisonersChangeInSettlement = new MbEvent<Settlement, FlattenedTroopRoster, Hero, bool>();
  
  	private readonly MbEvent<Hero, BoardGameHelper.BoardGameState> _onPlayerBoardGameOver = new MbEvent<Hero, BoardGameHelper.BoardGameState>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:771: 	public static IMbEvent<IEnumerable<CharacterObject>> ConversationEnded => Instance._onConversationEnded;
  
  	public static IMbEvent<IAgent> OnAgentJoinedConversationEvent => Instance._onAgentJoinedConversationEvent;
  
  
  	public static IMbEvent<MapEvent> MapEventEnded => Instance._mapEventEnded;
  
  	public static IMbEvent<MapEvent, PartyBase, PartyBase> MapEventStarted => Instance._mapEventStarted;
  
  	public static IMbEvent<Settlement, FlattenedTroopRoster, Hero, bool> PrisonersChangeInSettlement => Instance._prisonersChangeInSettlement;
  
  	public static IMbEvent<Hero, BoardGameHelper.BoardGameState> OnPlayerBoardGameOverEvent => Instance._onPlayerBoardGameOver;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1267: 		_onConversationEnded.ClearListeners(obj);
  		_gameMenuOptionSelectedEvent.ClearListeners(obj);
  		_playerStartRecruitmentEvent.ClearListeners(obj);
  		_onAgentJoinedConversationEvent.ClearListeners(obj);
  		_beforeHeroesMarried.ClearListeners(obj);
  		_onTroopsDesertedEvent.ClearListeners(obj);
  		_onBeforePlayerCharacterChangedEvent.ClearListeners(obj);
  		_onPlayerCharacterChangedEvent.ClearListeners(obj);
  		_onClanLeaderChangedEvent.ClearListeners(obj);
  		_onSiegeEventStartedEvent.ClearListeners(obj);
  		_onPlayerSiegeStartedEvent.ClearListeners(obj);
  		_onSiegeEventEndedEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1873: 	public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
  		Instance._onAgentJoinedConversationEvent.Invoke(agent);
  	}
  
  	{
  		Instance._onConversationEnded.Invoke(characters);
  	}
  
  	public override void OnMapEventEnded(MapEvent mapEvent)
  	{
  		Instance._mapEventEnded.Invoke(mapEvent);
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1875: 		Instance._onConversationEnded.Invoke(characters);
  
  	public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
  	{
  	}
  
  	public override void OnMapEventEnded(MapEvent mapEvent)
  	{
  		Instance._mapEventEnded.Invoke(mapEvent);
  	}
  
  	public override void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)

 succeeded in 543ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:26: 	public static bool SetOptionProperties(MenuCallbackArgs args, bool canPlayerDo, bool shouldBeDisabled, TextObject disabledText)
  namespace Helpers;
  
  public static class MenuHelper
  {
  	{
  		if (canPlayerDo)
  		{
  			return true;
  		}
  		if (!shouldBeDisabled)
  		{
  			return false;
  		}
  		args.IsEnabled = false;
  		args.Tooltip = disabledText;
  		return true;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\MenuHelper.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\CampaignMapConversation.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core\\TaleWorlds.Core\\MBInformationManager.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core\\TaleWorlds.Core\\InquiryElement.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 576ms:
    1: namespace TaleWorlds.CampaignSystem.Conversation;
    2: 
    3: public static class CampaignMapConversation
    4: {
    5: 	public static void OpenConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
    6: 	{
    7: 		Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
    8: 	}
    9: }

 succeeded in 596ms:
    1: using System;
    2: using TaleWorlds.Library;
    3: using TaleWorlds.Localization;
    4: 
    5: namespace TaleWorlds.Core;
    6: 
    7: public static class MBInformationManager
    8: {
    9: 	public enum NotificationPriority
   10: 	{
   11: 		Lowest,
   12: 		Low,
   13: 		Medium,
   14: 		High,
   15: 		Highest
   16: 	}
   17: 
   18: 	public enum NotificationStatus
   19: 	{
   20: 		Inactive,
   21: 		CurrentlyActive,
   22: 		InQueue
   23: 	}
   24: 
   25: 	public class DialogNotificationHandle
   26: 	{
   27: 	}
   28: 
   29: 	public static event Action<string, int, BasicCharacterObject, Equipment, string> FiringQuickInformation;
   30: 
   31: 	public static event Action ClearingQuickInformations;
   32: 
   33: 	public static event Action<MultiSelectionInquiryData, bool, bool> OnShowMultiSelectionInquiry;
   34: 
   35: 	public static event Action<InformationData> OnAddMapNotice;
   36: 
   37: 	public static event Action<InformationData> OnRemoveMapNotice;
   38: 
   39: 	public static event Action<SceneNotificationData> OnShowSceneNotification;
   40: 
   41: 	public static event Action OnHideSceneNotification;
   42: 
   43: 	public static event Func<bool> IsAnySceneNotificationActive;
   44: 
   45: 	public static void AddQuickInformation(TextObject message, int extraTimeInMs = 0, BasicCharacterObject announcerCharacter = null, Equipment equipment = null, string soundEventPath = "")
   46: 	{
   47: 		MBInformationManager.FiringQuickInformation?.Invoke(message.ToString(), extraTimeInMs, announcerCharacter, equipment, soundEventPath);
   48: 		Debug.Print(message.ToString(), 0, Debug.DebugColor.White, 1125899906842624uL);
   49: 	}
   50: 
   51: 	public static void ClearQuickInformations()
   52: 	{
   53: 		MBInformationManager.ClearingQuickInformations?.Invoke();
   54: 	}
   55: 
   56: 	public static void ShowMultiSelectionInquiry(MultiSelectionInquiryData data, bool pauseGameActiveState = false, bool prioritize = false)
   57: 	{
   58: 		MBInformationManager.OnShowMultiSelectionInquiry?.Invoke(data, pauseGameActiveState, prioritize);
   59: 	}
   60: 
   61: 	public static void AddNotice(InformationData data)
   62: 	{
   63: 		MBInformationManager.OnAddMapNotice?.Invoke(data);
   64: 	}
   65: 
   66: 	public static void MapNoticeRemoved(InformationData data)
   67: 	{
   68: 		MBInformationManager.OnRemoveMapNotice?.Invoke(data);
   69: 	}
   70: 
   71: 	public static void ShowHint(string hint)
   72: 	{
   73: 		InformationManager.ShowTooltip(typeof(string), hint);
   74: 	}
   75: 
   76: 	public static void HideInformations()
   77: 	{
   78: 		InformationManager.HideTooltip();
   79: 	}
   80: 
   81: 	public static void ShowSceneNotification(SceneNotificationData data)
   82: 	{
   83: 		MBInformationManager.OnShowSceneNotification?.Invoke(data);
   84: 	}
   85: 
   86: 	public static void HideSceneNotification()
   87: 	{
   88: 		MBInformationManager.OnHideSceneNotification?.Invoke();
   89: 	}
   90: 
   91: 	public static bool? GetIsAnySceneNotificationActive()
   92: 	{
   93: 		return MBInformationManager.IsAnySceneNotificationActive?.Invoke();
   94: 	}
   95: 
   96: 	public static void Clear()
   97: 	{
   98: 		MBInformationManager.FiringQuickInformation = null;
   99: 		MBInformationManager.OnShowMultiSelectionInquiry = null;
  100: 		MBInformationManager.OnAddMapNotice = null;
  101: 		MBInformationManager.OnRemoveMapNotice = null;
  102: 		MBInformationManager.OnShowSceneNotification = null;
  103: 		MBInformationManager.OnHideSceneNotification = null;
  104: 	}
  105: }

 succeeded in 608ms:
    1: using TaleWorlds.Core.ImageIdentifiers;
    2: 
    3: namespace TaleWorlds.Core;
    4: 
    5: public class InquiryElement
    6: {
    7: 	public readonly string Title;
    8: 
    9: 	public readonly ImageIdentifier ImageIdentifier;
   10: 
   11: 	public readonly object Identifier;
   12: 
   13: 	public readonly bool IsEnabled;
   14: 
   15: 	public readonly string Hint;
   16: 
   17: 	public InquiryElement(object identifier, string title, ImageIdentifier imageIdentifier)
   18: 	{
   19: 		Identifier = identifier;
   20: 		Title = title;
   21: 		ImageIdentifier = imageIdentifier;
   22: 		IsEnabled = true;
   23: 		Hint = null;
   24: 	}
   25: 
   26: 	public InquiryElement(object identifier, string title, ImageIdentifier imageIdentifier, bool isEnabled, string hint)
   27: 	{
   28: 		Identifier = identifier;
   29: 		Title = title;
   30: 		ImageIdentifier = imageIdentifier;
   31: 		IsEnabled = isEnabled;
   32: 		Hint = hint;
   33: 	}
   34: 
   35: 	public bool HasSameContentWith(object other)
   36: 	{
   37: 		if (other is InquiryElement inquiryElement)
   38: 		{
   39: 			if (Title == inquiryElement.Title)
   40: 			{
   41: 				if (ImageIdentifier != null || inquiryElement.ImageIdentifier != null)
   42: 				{
   43: 					ImageIdentifier imageIdentifier = ImageIdentifier;
   44: 					if (imageIdentifier == null || !imageIdentifier.Equals(inquiryElement.ImageIdentifier))
   45: 					{
   46: 						goto IL_0075;
   47: 					}
   48: 				}
   49: 				if (Identifier == inquiryElement.Identifier && IsEnabled == inquiryElement.IsEnabled)
   50: 				{
   51: 					return Hint == inquiryElement.Hint;
   52: 				}
   53: 			}
   54: 			goto IL_0075;
   55: 		}
   56: 		return false;
   57: 		IL_0075:
   58: 		return false;
   59: 	}
   60: }

 succeeded in 647ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.Linq;
    4: using TaleWorlds.CampaignSystem;
    5: using TaleWorlds.CampaignSystem.Actions;
    6: using TaleWorlds.CampaignSystem.Encounters;
    7: using TaleWorlds.CampaignSystem.GameMenus;
    8: using TaleWorlds.CampaignSystem.GameState;
    9: using TaleWorlds.CampaignSystem.Map;
   10: using TaleWorlds.CampaignSystem.MapEvents;
   11: using TaleWorlds.CampaignSystem.Naval;
   12: using TaleWorlds.CampaignSystem.Party;
   13: using TaleWorlds.CampaignSystem.Roster;
   14: using TaleWorlds.CampaignSystem.Settlements;
   15: using TaleWorlds.CampaignSystem.Settlements.Locations;
   16: using TaleWorlds.CampaignSystem.Siege;
   17: using TaleWorlds.Core;
   18: using TaleWorlds.Library;
   19: using TaleWorlds.LinQuick;
   20: using TaleWorlds.Localization;
   21: 
   22: namespace Helpers;
   23: 
   24: public static class MenuHelper
   25: {
   26: 	public static bool SetOptionProperties(MenuCallbackArgs args, bool canPlayerDo, bool shouldBeDisabled, TextObject disabledText)
   27: 	{
   28: 		if (canPlayerDo)
   29: 		{
   30: 			return true;
   31: 		}
   32: 		if (!shouldBeDisabled)
   33: 		{
   34: 			return false;
   35: 		}
   36: 		args.IsEnabled = false;
   37: 		args.Tooltip = disabledText;
   38: 		return true;
   39: 	}
   40: 
   41: 	public static void SetIssueAndQuestDataForHero(MenuCallbackArgs args, Hero hero)
   42: 	{
   43: 		if (hero.Issue != null && hero.Issue.IssueQuest == null)
   44: 		{
   45: 			args.OptionQuestData |= GameMenuOption.IssueQuestFlags.AvailableIssue;
   46: 		}
   47: 		Campaign.Current.QuestManager.TrackedObjects.TryGetValue(hero, out var value);
   48: 		if (value != null)
   49: 		{
   50: 			for (int i = 0; i < value.Count; i++)
   51: 			{
   52: 				if (!value[i].IsTrackEnabled)
   53: 				{
   54: 					continue;
   55: 				}
   56: 				if (value[i].IsSpecialQuest)
   57: 				{
   58: 					if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedStoryQuest) == 0 && value[i].QuestGiver != hero)
   59: 					{
   60: 						args.OptionQuestData |= GameMenuOption.IssueQuestFlags.TrackedStoryQuest;
   61: 					}
   62: 					else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveStoryQuest) == 0 && value[i].QuestGiver == hero)
   63: 					{
   64: 						args.OptionQuestData |= GameMenuOption.IssueQuestFlags.ActiveStoryQuest;
   65: 					}
   66: 				}
   67: 				else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedIssue) == 0 && value[i].QuestGiver != hero)
   68: 				{
   69: 					args.OptionQuestData |= GameMenuOption.IssueQuestFlags.TrackedIssue;
   70: 				}
   71: 				else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveIssue) == 0 && value[i].QuestGiver == hero)
   72: 				{
   73: 					args.OptionQuestData |= GameMenuOption.IssueQuestFlags.ActiveIssue;
   74: 				}
   75: 			}
   76: 		}
   77: 		if (hero.PartyBelongedTo != null && ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveStoryQuest) == 0 || (args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveIssue) == 0 || (args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedIssue) == 0 || (args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedStoryQuest) == 0))
   78: 		{
   79: 			Campaign.Current.QuestManager.TrackedObjects.TryGetValue(hero.PartyBelongedTo, out var value2);
   80: 			if (value2 != null)
   81: 			{
   82: 				for (int j = 0; j < value2.Count; j++)
   83: 				{
   84: 					if (!value2[j].IsTrackEnabled)
   85: 					{
   86: 						continue;
   87: 					}
   88: 					if (value2[j].IsSpecialQuest)
   89: 					{
   90: 						if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedStoryQuest) == 0 && value2[j].QuestGiver != hero)
   91: 						{
   92: 							args.OptionQuestData |= GameMenuOption.IssueQuestFlags.TrackedStoryQuest;
   93: 						}
   94: 						else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveStoryQuest) == 0 && value2[j].QuestGiver == hero)
   95: 						{
   96: 							args.OptionQuestData |= GameMenuOption.IssueQuestFlags.ActiveStoryQuest;
   97: 						}
   98: 					}
   99: 					else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.TrackedIssue) == 0 && value2[j].QuestGiver != hero)
  100: 					{
  101: 						args.OptionQuestData |= GameMenuOption.IssueQuestFlags.TrackedIssue;
  102: 					}
  103: 					else if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveIssue) == 0 && value2[j].QuestGiver == hero)
  104: 					{
  105: 						args.OptionQuestData |= GameMenuOption.IssueQuestFlags.ActiveIssue;
  106: 					}
  107: 				}
  108: 			}
  109: 		}
  110: 		if ((args.OptionQuestData & GameMenuOption.IssueQuestFlags.ActiveIssue) == 0 && hero.Issue?.IssueQuest != null && hero.Issue.IssueQuest.IsTrackEnabled)
  111: 		{
  112: 			args.OptionQuestData |= GameMenuOption.IssueQuestFlags.ActiveIssue;
  113: 		}
  114: 	}
  115: 
  116: 	public static void SetIssueAndQuestDataForLocations(MenuCallbackArgs args, List<Location> locations)
  117: 	{
  118: 		GameMenuOption.IssueQuestFlags issueQuestFlags = Campaign.Current.IssueManager.CheckIssueForMenuLocations(locations, getIssuesWithoutAQuest: true);
  119: 		args.OptionQuestData |= issueQuestFlags;
  120: 		args.OptionQuestData |= Campaign.Current.QuestManager.CheckQuestForMenuLocations(locations);
  121: 	}
  122: 
  123: 	public static bool CheckAndOpenNextLocation(MenuCallbackArgs args)
  124: 	{
  125: 		if (Campaign.Current.GameMenuManager.NextLocation != null && GameStateManager.Current.ActiveState is MapState)
  126: 		{
  127: 			PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(Campaign.Current.GameMenuManager.NextLocation, Campaign.Current.GameMenuManager.PreviousLocation);
  128: 			switch (Campaign.Current.GameMenuManager.NextLocation.StringId)
  129: 			{
  130: 			case "center":
  131: 				if (Settlement.CurrentSettlement.IsCastle)
  132: 				{
  133: 					Campaign.Current.GameMenuManager.SetNextMenu("castle");
  134: 				}
  135: 				else if (Settlement.CurrentSettlement.IsTown)
  136: 				{
  137: 					Campaign.Current.GameMenuManager.SetNextMenu("town");
  138: 				}
  139: 				else if (Settlement.CurrentSettlement.IsVillage)
  140: 				{
  141: 					Campaign.Current.GameMenuManager.SetNextMenu("village");
  142: 				}
  143: 				else
  144: 				{
  145: 					Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Helpers.cs", "CheckAndOpenNextLocation", 192);
  146: 				}
  147: 				break;
  148: 			case "tavern":
  149: 				Campaign.Current.GameMenuManager.SetNextMenu("town_backstreet");
  150: 				break;
  151: 			case "arena":
  152: 				Campaign.Current.GameMenuManager.SetNextMenu("town_arena");
  153: 				break;
  154: 			case "lordshall":
  155: 			case "prison":
  156: 				Campaign.Current.GameMenuManager.SetNextMenu("town_keep");
  157: 				break;
  158: 			case "port":
  159: 				Campaign.Current.GameMenuManager.SetNextMenu("port_menu");
  160: 				break;
  161: 			}
  162: 			Campaign.Current.GameMenuManager.NextLocation = null;
  163: 			Campaign.Current.GameMenuManager.PreviousLocation = null;
  164: 			return true;
  165: 		}
  166: 		return false;
  167: 	}
  168: 
  169: 	public static void DecideMenuState()
  170: 	{
  171: 		string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
  172: 		if (!string.IsNullOrEmpty(genericStateMenu))
  173: 		{
  174: 			GameMenu.SwitchToMenu(genericStateMenu);
  175: 		}
  176: 		else
  177: 		{
  178: 			GameMenu.ExitToLast();
  179: 		}
  180: 	}
  181: 
  182: 	public static bool EncounterAttackCondition(MenuCallbackArgs args)
  183: 	{
  184: 		args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
  185: 		if (MapEvent.PlayerMapEvent == null)
  186: 		{
  187: 			return false;
  188: 		}
  189: 		MapEvent playerMapEvent = MapEvent.PlayerMapEvent;
  190: 		Settlement mapEventSettlement = playerMapEvent.MapEventSettlement;
  191: 		if (mapEventSettlement != null && mapEventSettlement.IsFortification && playerMapEvent.IsSiegeAssault && PlayerSiege.PlayerSiegeEvent != null && !PlayerSiege.PlayerSiegeEvent.BesiegerCamp.IsPreparationComplete)
  192: 		{
  193: 			return false;
  194: 		}
  195: 		bool flag = MapEvent.PlayerMapEvent.PartiesOnSide(PartyBase.MainParty.OpponentSide).Any((MapEventParty party) => party.Party.NumberOfHealthyMembers > 0);
  196: 		if (Hero.MainHero.IsWounded)
  197: 		{
  198: 			args.Tooltip = new TextObject("{=UL8za0AO}You are wounded.");
  199: 			args.IsEnabled = false;
  200: 		}
  201: 		bool flag2 = (playerMapEvent.HasTroopsOnBothSides() || playerMapEvent.IsSiegeAssault) && MapEvent.PlayerMapEvent.GetLeaderParty(PartyBase.MainParty.OpponentSide) != null;
  202: 		if (!MobileParty.MainParty.IsInRaftState)
  203: 		{
  204: 			MobileParty mobileParty = playerMapEvent.PartiesOnSide(PlayerEncounter.Current.OpponentSide)[0].Party.MobileParty;
  205: 			if (mobileParty == null || !mobileParty.IsInRaftState)
  206: 			{
  207: 				goto IL_0125;
  208: 			}
  209: 		}
  210: 		args.Tooltip = new TextObject("{=x9ePfpw5}You are on a raft, in desperate circumstances, and cannot fight");
  211: 		args.IsEnabled = false;
  212: 		goto IL_0125;
  213: 		IL_0125:
  214: 		if (flag && !flag2 && !Hero.MainHero.IsWounded)
  215: 		{
  216: 			Debug.FailedAssert("This encounter case should be investigated", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Helpers.cs", "EncounterAttackCondition", 275);
  217: 			return false;
  218: 		}
  219: 		if (flag && Game.Current.IsDevelopmentMode && (mapEventSettlement == null || playerMapEvent.IsBlockadeSallyOut || playerMapEvent.IsSallyOut || playerMapEvent.IsSiegeOutside || playerMapEvent.IsBlockade))
  220: 		{
  221: 			bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
  222: 			MapPatchData mapPatchAtPosition = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position);
  223: 			string battleSceneForMapPatch = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
  224: 			args.Tooltip = new TextObject("{=!}[DEV] Scene: (" + battleSceneForMapPatch + ")");
  225: 		}
  226: 		if (MobileParty.MainParty.IsCurrentlyAtSea)
  227: 		{
  228: 			MapEvent encounteredBattle = PlayerEncounter.EncounteredBattle;
  229: 			if (encounteredBattle != null && encounteredBattle.MapEventSettlement?.IsVillage == true)
  230: 			{
  231: 				MapEvent encounteredBattle2 = PlayerEncounter.EncounteredBattle;
  232: 				if (encounteredBattle2 != null && encounteredBattle2.IsRaid)
  233: 				{
  234: 					int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
  235: 					if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene)
  236: 					{
  237: 						args.IsEnabled = false;
  238: 						args.Tooltip = new TextObject("{=*}You should at least have {NUMBER} healthy men in your party to take a hostile action.");
  239: 						args.Tooltip.SetTextVariable("NUMBER", minimumNumberOfMenForAttackingVillageViaScene);
  240: 					}
  241: 					else if (!ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty().AnyQ())
  242: 					{
  243: 						args.IsEnabled = false;
  244: 						args.Tooltip = new TextObject("{=*}You don't have any shallow draft ship.");
  245: 					}
  246: 				}
  247: 			}
  248: 		}
  249: 		return flag;
  250: 	}
  251: 
  252: 	public static bool EncounterCaptureEnemyCondition(MenuCallbackArgs args)
  253: 	{
  254: 		args.optionLeaveType = GameMenuOption.LeaveType.Surrender;
  255: 		MapEvent battle = PlayerEncounter.Battle;
  256: 		return battle?.PartiesOnSide(battle.GetOtherSide(battle.PlayerSide)).All((MapEventParty party) => !party.Party.IsSettlement && (party.Party.NumberOfHealthyMembers == 0 || party.Party.MobileParty.IsInRaftState)) ?? false;
  257: 	}
  258: 
  259: 	public static void EncounterAttackConsequence(MenuCallbackArgs args)
  260: 	{
  261: 		MapEvent battle = PlayerEncounter.Battle;
  262: 		PartyBase leaderParty = battle.GetLeaderParty(PartyBase.MainParty.OpponentSide);
  263: 		BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, leaderParty);
  264: 		if (PlayerEncounter.Current == null)
  265: 		{
  266: 			return;
  267: 		}
  268: 		Settlement mapEventSettlement = MobileParty.MainParty.MapEvent.MapEventSettlement;
  269: 		if (mapEventSettlement != null && !battle.IsBlockadeSallyOut && !battle.IsSallyOut && !battle.IsSiegeOutside && !battle.IsBlockade)
  270: 		{
  271: 			if (mapEventSettlement.IsFortification)
  272: 			{
  273: 				if (battle.IsSiegeAmbush)
  274: 				{
  275: 					PlayerEncounter.StartSiegeAmbushMission();
  276: 				}
  277: 				else if (battle.IsSiegeAssault)
  278: 				{
  279: 					if (PlayerSiege.PlayerSiegeEvent == null && PartyBase.MainParty.Side == BattleSideEnum.Attacker)
  280: 					{
  281: 						PlayerSiege.StartPlayerSiege(MobileParty.MainParty.Party.Side, isSimulation: false, mapEventSettlement);
  282: 					}
  283: 					else
  284: 					{
  285: 						if (PlayerSiege.PlayerSiegeEvent != null && !PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(PlayerSiege.PlayerSide.GetOppositeSide()).GetInvolvedPartiesForEventType().Any((PartyBase party) => party.NumberOfHealthyMembers > 0))
  286: 						{
  287: 							PlayerEncounter.Update();
  288: 							return;
  289: 						}
  290: 						if (PlayerSiege.BesiegedSettlement != null && PlayerSiege.BesiegedSettlement.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall)
  291: 						{
  292: 							FlattenedTroopRoster priorityListForLordsHallFightMission = Campaign.Current.Models.SiegeLordsHallFightModel.GetPriorityListForLordsHallFightMission(MapEvent.PlayerMapEvent, BattleSideEnum.Defender, Campaign.Current.Models.SiegeLordsHallFightModel.MaxDefenderSideTroopCount);
  293: 							int num = TaleWorlds.Library.MathF.Max(1, TaleWorlds.Library.MathF.Min(Campaign.Current.Models.SiegeLordsHallFightModel.MaxAttackerSideTroopCount, TaleWorlds.Library.MathF.Round((float)priorityListForLordsHallFightMission.Troops.Count() * Campaign.Current.Models.SiegeLordsHallFightModel.AttackerDefenderTroopCountRatio)));
  294: 							TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
  295: 							MobileParty mobileParty = ((MobileParty.MainParty.Army != null) ? MobileParty.MainParty.Army.LeaderParty : MobileParty.MainParty);
  296: 							troopRoster.Add(mobileParty.MemberRoster);
  297: 							foreach (MobileParty attachedParty in mobileParty.AttachedParties)
  298: 							{
  299: 								troopRoster.Add(attachedParty.MemberRoster);
  300: 							}
  301: 							TroopRoster troopRoster2 = TroopRoster.CreateDummyTroopRoster();
  302: 							FlattenedTroopRoster flattenedTroopRoster = troopRoster.ToFlattenedRoster();
  303: 							flattenedTroopRoster.RemoveIf((FlattenedTroopRosterElement x) => x.IsWounded);
  304: 							troopRoster2.Add(MobilePartyHelper.GetStrongestAndPriorTroops(flattenedTroopRoster, num, includePlayer: true));
  305: 							int minSelectableTroopCount = 1;
  306: 							args.MenuContext.OpenTroopSelection(troopRoster, troopRoster2, null, (CharacterObject character) => !character.IsPlayerCharacter, LordsHallTroopRosterManageDone, num, minSelectableTroopCount);
  307: 						}
  308: 						else
  309: 						{
  310: 							PlayerSiege.StartSiegeMission(mapEventSettlement);
  311: 						}
  312: 					}
  313: 				}
  314: 			}
  315: 			else if (mapEventSettlement.IsVillage)
  316: 			{
  317: 				MapEventHelper.GetRaidContext(battle, out var raiderSide, out var raiderHasSeaPresence, out var raiderHasLandPresence, out var villageFactionSideHasSeaPresence, out var villageFactionSideHasLandPresence, out var wasEverInLootingPhase);
  318: 				BattleSideEnum otherSide = battle.GetOtherSide(raiderSide);
  319: 				if (!wasEverInLootingPhase)
  320: 				{
  321: 					if (raiderHasSeaPresence)
  322: 					{
  323: 						StartSeaRaidMission(battle, raiderSide, args);
  324: 					}
  325: 					else
  326: 					{
  327: 						PlayerEncounter.StartVillageBattleMission();
  328: 					}
  329: 				}
  330: 				else if (villageFactionSideHasSeaPresence)
  331: 				{
  332: 					if (raiderHasSeaPresence && !raiderHasLandPresence && !villageFactionSideHasLandPresence)
  333: 					{
  334: 						MapPatchData mapPatchAtPosition = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position);
  335: 						string battleSceneForMapPatch = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter: true);
  336: 						MissionInitializerRecord rec = new MissionInitializerRecord(battleSceneForMapPatch);
  337: 						TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
  338: 						rec.TerrainType = (int)faceTerrainType;
  339: 						rec.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  340: 						rec.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  341: 						rec.NeedsRandomTerrain = false;
  342: 						rec.PlayingInCampaignMode = true;
  343: 						rec.RandomTerrainSeed = MBRandom.RandomInt(10000);
  344: 						rec.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position);
  345: 						rec.SceneHasMapPatch = true;
  346: 						rec.DecalAtlasGroup = 2;
  347: 						rec.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
  348: 						rec.PatchEncounterDir = (battle.AttackerSide.LeaderParty.Position.ToVec2() - battle.DefenderSide.LeaderParty.Position.ToVec2()).Normalized();
  349: 						CampaignMission.OpenNavalBattleMission(rec);
  350: 					}
  351: 					else
  352: 					{
  353: 						StartSeaRaidMission(battle, otherSide, args);
  354: 					}
  355: 				}
  356: 				else
  357: 				{
  358: 					PlayerEncounter.StartVillageBattleMission();
  359: 				}
  360: 			}
  361: 			else if (mapEventSettlement.IsHideout)
  362: 			{
  363: 				CampaignMission.OpenHideoutBattleMission("sea_bandit_a", null, isTutorial: false);
  364: 			}
  365: 		}
  366: 		else
  367: 		{
  368: 			bool flag = PlayerEncounter.IsNavalEncounter();
  369: 			MapPatchData mapPatchAtPosition2 = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position);
  370: 			string battleSceneForMapPatch2 = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition2, flag);
  371: 			MissionInitializerRecord rec2 = new MissionInitializerRecord(battleSceneForMapPatch2);
  372: 			TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
  373: 			rec2.TerrainType = (int)faceTerrainType2;
  374: 			rec2.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  375: 			rec2.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  376: 			rec2.NeedsRandomTerrain = false;
  377: 			rec2.PlayingInCampaignMode = true;
  378: 			rec2.RandomTerrainSeed = MBRandom.RandomInt(10000);
  379: 			rec2.AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position);
  380: 			rec2.SceneHasMapPatch = true;
  381: 			rec2.DecalAtlasGroup = 2;
  382: 			rec2.PatchCoordinates = mapPatchAtPosition2.normalizedCoordinates;
  383: 			rec2.PatchEncounterDir = (battle.AttackerSide.LeaderParty.Position.ToVec2() - battle.DefenderSide.LeaderParty.Position.ToVec2()).Normalized();
  384: 			bool flag2 = MapEvent.PlayerMapEvent.PartiesOnSide(BattleSideEnum.Defender).Any((MapEventParty involvedParty) => involvedParty.Party.IsMobile && (involvedParty.Party.MobileParty.IsCaravan || (involvedParty.Party.Owner != null && involvedParty.Party.Owner.IsMerchant)));
  385: 			bool flag3 = MapEvent.PlayerMapEvent.MapEventSettlement == null && MapEvent.PlayerMapEvent.PartiesOnSide(BattleSideEnum.Defender).Any((MapEventParty involvedParty) => involvedParty.Party.IsMobile && involvedParty.Party.MobileParty.IsVillager);
  386: 			if (flag)
  387: 			{
  388: 				CampaignMission.OpenNavalBattleMission(rec2);
  389: 			}
  390: 			else if (flag2 || flag3)
  391: 			{
  392: 				CampaignMission.OpenCaravanBattleMission(rec2, flag2);
  393: 			}
  394: 			else
  395: 			{
  396: 				CampaignMission.OpenBattleMission(rec2);
  397: 			}
  398: 		}
  399: 		PlayerEncounter.StartAttackMission();
  400: 		MapEvent.PlayerMapEvent.BeginWait();
  401: 	}
  402: 
  403: 	private static void StartSeaRaidMission(MapEvent mapEvent, BattleSideEnum navalSide, MenuCallbackArgs args)
  404: 	{
  405: 		bool num = mapEvent.PlayerSide == navalSide;
  406: 		List<MapEventParty> navalParties = mapEvent.PartiesOnSide(navalSide).ToList();
  407: 		List<Ship> selectedShips = (from x in navalParties.SelectMany((MapEventParty x) => x.Ships)
  408: 			where x.ShipHull.CanNavigateShallowWater
  409: 			orderby x.ShipHull.MainDeckCrewCapacity descending
  410: 			select x).Take(3).ToList();
  411: 		int maxSelectableTroopCount = selectedShips.Sum((Ship x) => x.ShipHull.MainDeckCrewCapacity);
  412: 		TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
  413: 		if (num)
  414: 		{
  415: 			TroopRoster strongestAndPriorTroops = MobilePartyHelper.GetStrongestAndPriorTroops(MobileParty.MainParty, Math.Min(maxSelectableTroopCount, MobileParty.MainParty.MemberRoster.TotalHealthyCount), includePlayer: true);
  416: 			args.MenuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, strongestAndPriorTroops, selectedShips, (CharacterObject character) => !character.IsPlayerCharacter, delegate(TroopRoster troops)
  417: 			{
  418: 				int count = maxSelectableTroopCount - troops.TotalHealthyCount;
  419: 				navalParties.RemoveAll((MapEventParty x) => x.Party == PartyBase.MainParty);
  420: 				foreach (FlattenedTroopRosterElement item in (from x in navalParties.SelectMany((MapEventParty x) => x.Troops)
  421: 					orderby x.Troop.GetBattlePower() descending
  422: 					select x).Take(count))
  423: 				{
  424: 					troops.AddToCounts(item.Troop, 1);
  425: 				}
  426: 				CampaignMission.OpenNavalRaidMission(troops, navalSide, selectedShips);
  427: 			}, maxSelectableTroopCount, Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene, isNavalRaid: true);
  428: 			return;
  429: 		}
  430: 		foreach (FlattenedTroopRosterElement item2 in (from x in navalParties.SelectMany((MapEventParty x) => x.Troops)
  431: 			orderby x.Troop.GetBattlePower() descending
  432: 			select x).Take(maxSelectableTroopCount))
  433: 		{
  434: 			troopRoster.AddToCounts(item2.Troop, 1);
  435: 		}
  436: 		CampaignMission.OpenNavalRaidMission(troopRoster, navalSide, selectedShips);
  437: 	}
  438: 
  439: 	private static void LordsHallTroopRosterManageDone(TroopRoster selectedTroops)
  440: 	{
  441: 		MapEvent.PlayerMapEvent.ResetBattleState();
  442: 		int wallLevel = PlayerSiege.BesiegedSettlement.Town.GetWallLevel();
  443: 		CampaignMission.OpenSiegeLordsHallFightMission(PlayerSiege.BesiegedSettlement.LocationComplex.GetLocationWithId("lordshall").GetSceneName(wallLevel), selectedTroops.ToFlattenedRoster());
  444: 	}
  445: 
  446: 	public static void CheckEnemyAttackableHonorably(MenuCallbackArgs args)
  447: 	{
  448: 		if ((MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty) && !PlayerEncounter.PlayerIsDefender)
  449: 		{
  450: 			IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
  451: 			if (mapFaction != null && mapFaction.NotAttackableByPlayerUntilTime.IsFuture)
  452: 			{
  453: 				args.IsEnabled = false;
  454: 				args.Tooltip = GameTexts.FindText("str_enemy_not_attackable_tooltip");
  455: 			}
  456: 		}
  457: 	}
  458: 
  459: 	public static bool EncounterOrderAttackCondition(MenuCallbackArgs args)
  460: 	{
  461: 		MapEvent playerMapEvent = MapEvent.PlayerMapEvent;
  462: 		if (playerMapEvent != null)
  463: 		{
  464: 			args.optionLeaveType = ((!playerMapEvent.IsNavalMapEvent) ? GameMenuOption.LeaveType.OrderTroopsToAttack : GameMenuOption.LeaveType.OrderShipsToAttack);
  465: 			MobileParty mobileParty = MapEvent.PlayerMapEvent.PartiesOnSide(PlayerEncounter.Current.OpponentSide)[0].Party.MobileParty;
  466: 			if (mobileParty != null && mobileParty.IsInRaftState)
  467: 			{
  468: 				return false;
  469: 			}
  470: 			CheckEnemyAttackableHonorably(args);
  471: 			int num = 0;
  472: 			foreach (MapEventParty party in MobileParty.MainParty.MapEventSide.Parties)
  473: 			{
  474: 				if (!party.Party.IsMobile || !party.Party.MobileParty.IsInRaftState)
  475: 				{
  476: 					num += party.Party.MemberRoster.Sum((TroopRosterElement x) => x.Character.IsHero ? ((x.Character != CharacterObject.PlayerCharacter && !x.Character.HeroObject.IsWounded) ? 1 : 0) : (x.Number - x.WoundedNumber));
  477: 				}
  478: 			}
  479: 			if (playerMapEvent.HasTroopsOnBothSides() && playerMapEvent.GetLeaderParty(PartyBase.MainParty.OpponentSide) != null && num > 0)
  480: 			{
  481: 				int num2 = 0;
  482: 				if (!MobileParty.MainParty.IsInRaftState)
  483: 				{
  484: 					num2 = MobileParty.MainParty.MemberRoster.Sum((TroopRosterElement x) => x.Character.IsHero ? ((x.Character != CharacterObject.PlayerCharacter && !x.Character.HeroObject.IsWounded) ? 1 : 0) : (x.Number - x.WoundedNumber));
  485: 				}
  486: 				if (num2 > 0)
  487: 				{
  488: 					if (MobileParty.MainParty.MapEvent.IsNavalMapEvent)
  489: 					{
  490: 						MBTextManager.SetTextVariable("SEND_TROOPS_TEXT", "{=NFnS5YqQ}Send ships.");
  491: 					}
  492: 					else
  493: 					{
  494: 						MBTextManager.SetTextVariable("SEND_TROOPS_TEXT", "{=QfMeoKOm}Send troops.");
  495: 					}
  496: 				}
  497: 				else
  498: 				{
  499: 					MBTextManager.SetTextVariable("SEND_TROOPS_TEXT", "{=jo3UHKMD}Leave it to the others.");
  500: 				}
  501: 				if (playerMapEvent.IsInvulnerable)
  502: 				{
  503: 					playerMapEvent.IsInvulnerable = false;
  504: 				}
  505: 				if (!MobilePartyHelper.CanPartyAttackWithCurrentMorale(MobileParty.MainParty))
  506: 				{
  507: 					args.Tooltip = new TextObject("{=xnRtINwH}Your men lack the courage to continue the battle without you. (Low Morale)");
  508: 					args.IsEnabled = false;
  509: 				}
  510: 				else
  511: 				{
  512: 					IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
  513: 					if (mapFaction == null || mapFaction.NotAttackableByPlayerUntilTime.IsPast)
  514: 					{
  515: 						args.Tooltip = TooltipHelper.GetSendTroopsPowerContextTooltipForMapEvent();
  516: 					}
  517: 				}
  518: 				if (MobileParty.MainParty.IsCurrentlyAtSea)
  519: 				{
  520: 					MapEvent encounteredBattle = PlayerEncounter.EncounteredBattle;
  521: 					if (encounteredBattle != null && encounteredBattle.MapEventSettlement?.IsVillage == true)
  522: 					{
  523: 						MapEvent encounteredBattle2 = PlayerEncounter.EncounteredBattle;
  524: 						if (encounteredBattle2 != null && encounteredBattle2.IsRaid)
  525: 						{
  526: 							int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
  527: 							if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene)
  528: 							{
  529: 								args.IsEnabled = false;
  530: 								args.Tooltip = new TextObject("{=*}You should at least have {NUMBER} healthy men in your party to take a hostile action.");
  531: 								args.Tooltip.SetTextVariable("NUMBER", minimumNumberOfMenForAttackingVillageViaScene);
  532: 							}
  533: 							else if (!ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty().AnyQ())
  534: 							{
  535: 								args.IsEnabled = false;
  536: 								args.Tooltip = new TextObject("{=*}You don't have any shallow draft ship.");
  537: 							}
  538: 						}
  539: 					}
  540: 				}
  541: 				return true;
  542: 			}
  543: 		}
  544: 		return false;
  545: 	}
  546: 
  547: 	private static void EncounterOrderAttack(TroopRoster selectedTroopsForPlayerSide)
  548: 	{
  549: 		MapEvent battle = PlayerEncounter.Battle;
  550: 		if (PlayerSiege.PlayerSiegeEvent != null)
  551: 		{
  552: 			ISiegeEventSide siegeEventSide = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(PlayerSiege.PlayerSide.GetOppositeSide());
  553: 			if (siegeEventSide != null && !siegeEventSide.GetInvolvedPartiesForEventType().Any((PartyBase party) => party.NumberOfHealthyMembers > 0) && (battle == null || !battle.GetMapEventSide(battle.GetOtherSide(battle.PlayerSide)).Parties.Any((MapEventParty party) => party.Party.NumberOfHealthyMembers > 0)))
  554: 			{
  555: 				PlayerEncounter.Update();
  556: 				return;
  557: 			}
  558: 		}
  559: 		PartyBase leaderParty = battle.GetLeaderParty(PartyBase.MainParty.OpponentSide);
  560: 		MobileParty mobileParty = MobileParty.MainParty.AttachedTo ?? MobileParty.MainParty;
  561: 		if (leaderParty.SiegeEvent?.BesiegerCamp != null && !leaderParty.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(leaderParty) && mobileParty.BesiegerCamp == null)
  562: 		{
  563: 			mobileParty.BesiegerCamp = leaderParty.SiegeEvent.BesiegerCamp;
  564: 		}
  565: 		BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, leaderParty);
  566: 		if (PlayerEncounter.Current != null)
  567: 		{
  568: 			GameMenu.ExitToLast();
  569: 			PlayerEncounter.InitSimulation(null, null);
  570: 			if (PlayerEncounter.Current != null && PlayerEncounter.Current.BattleSimulation != null)
  571: 			{
  572: 				((MapState)Game.Current.GameStateManager.ActiveState).StartBattleSimulation();
  573: 			}
  574: 		}
  575: 	}
  576: 
  577: 	public static void EncounterOrderAttackConsequence(MenuCallbackArgs args)
  578: 	{
  579: 		EncounterOrderAttack(null);
  580: 	}
  581: 
  582: 	public static void EncounterCaptureTheEnemyOnConsequence(MenuCallbackArgs args)
  583: 	{
  584: 		MapEvent.PlayerMapEvent.SetOverrideWinner(MapEvent.PlayerMapEvent.PlayerSide);
  585: 		PlayerEncounter.Update();
  586: 	}
  587: 
  588: 	public static void EncounterLeaveConsequence()
  589: 	{
  590: 		Settlement currentSettlement = MobileParty.MainParty.CurrentSettlement;
  591: 		MapEvent mapEvent = ((PlayerEncounter.Battle != null) ? PlayerEncounter.Battle : PlayerEncounter.EncounteredBattle);
  592: 		int numberOfInvolvedMen = mapEvent.GetNumberOfInvolvedMen(PartyBase.MainParty.Side);
  593: 		PlayerEncounter.Finish(currentSettlement == null && (MobileParty.MainParty.CurrentSettlement?.SiegeEvent == null || MobileParty.MainParty.CurrentSettlement?.MapFaction != MobileParty.MainParty.MapFaction));
  594: 		if (MobileParty.MainParty.BesiegerCamp != null)
  595: 		{
  596: 			MobileParty.MainParty.BesiegerCamp = null;
  597: 		}
  598: 		if (mapEvent != null && !mapEvent.IsFinalized && !mapEvent.IsRaid && numberOfInvolvedMen == PartyBase.MainParty.NumberOfHealthyMembers)
  599: 		{
  600: 			mapEvent.SimulateBattleSetup(PlayerEncounter.Current?.BattleSimulation?.SelectedTroops);
  601: 			mapEvent.SimulateBattleRound((PartyBase.MainParty.Side == BattleSideEnum.Attacker) ? 1 : 0, (PartyBase.MainParty.Side != BattleSideEnum.Attacker) ? 1 : 0);
  602: 		}
  603: 		if (currentSettlement != null)
  604: 		{
  605: 			EncounterManager.StartSettlementEncounter(MobileParty.MainParty, currentSettlement);
  606: 		}
  607: 	}
  608: 
  609: 	public static string GetEncounterCultureBackgroundMesh(CultureObject encounterCulture)
  610: 	{
  611: 		if (string.IsNullOrEmpty(encounterCulture?.EncounterBackgroundMesh))
  612: 		{
  613: 			Debug.FailedAssert("Background mesh is invalid for current encounter", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Helpers.cs", "GetEncounterCultureBackgroundMesh", 814);
  614: 			return string.Empty;
  615: 		}
  616: 		string text = encounterCulture.EncounterBackgroundMesh;
  617: 		MapEvent obj = PlayerEncounter.Battle ?? PlayerEncounter.EncounteredBattle;
  618: 		if (obj != null && obj.IsNavalMapEvent)
  619: 		{
  620: 			text += "_naval";
  621: 		}
  622: 		return text;
  623: 	}
  624: }

codex
Vanilla `MenuHelper.SetOptionProperties` does exactly what the feature expects: return true while disabling and setting a tooltip when `shouldBeDisabled` is true. The inquiry element itself carries `IsEnabled`; I’m now checking the Gauntlet/VM layer to see whether disabled elements can still be selected or passed to callbacks.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter '*MultiSelection*.*' -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core\\TaleWorlds.Core\\MultiSelectionInquiryData.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter '*Inquiry*VM.cs' -ErrorAction SilentlyContinue | Select-Object -First 50 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 539ms:
    1: using System;
    2: using System.Collections.Generic;
    3: 
    4: namespace TaleWorlds.Core;
    5: 
    6: public class MultiSelectionInquiryData
    7: {
    8: 	public readonly string TitleText;
    9: 
   10: 	public readonly string DescriptionText;
   11: 
   12: 	public readonly List<InquiryElement> InquiryElements;
   13: 
   14: 	public readonly bool IsExitShown;
   15: 
   16: 	public readonly int MaxSelectableOptionCount;
   17: 
   18: 	public readonly int MinSelectableOptionCount;
   19: 
   20: 	public readonly string SoundEventPath;
   21: 
   22: 	public readonly string AffirmativeText;
   23: 
   24: 	public readonly string NegativeText;
   25: 
   26: 	public readonly Action<List<InquiryElement>> AffirmativeAction;
   27: 
   28: 	public readonly Action<List<InquiryElement>> NegativeAction;
   29: 
   30: 	public readonly bool IsSeachAvailable;
   31: 
   32: 	public MultiSelectionInquiryData(string titleText, string descriptionText, List<InquiryElement> inquiryElements, bool isExitShown, int minSelectableOptionCount, int maxSelectableOptionCount, string affirmativeText, string negativeText, Action<List<InquiryElement>> affirmativeAction, Action<List<InquiryElement>> negativeAction, string soundEventPath = "", bool isSeachAvailable = false)
   33: 	{
   34: 		TitleText = titleText;
   35: 		DescriptionText = descriptionText;
   36: 		InquiryElements = inquiryElements;
   37: 		IsExitShown = isExitShown;
   38: 		AffirmativeText = affirmativeText;
   39: 		NegativeText = negativeText;
   40: 		AffirmativeAction = affirmativeAction;
   41: 		NegativeAction = negativeAction;
   42: 		MinSelectableOptionCount = minSelectableOptionCount;
   43: 		MaxSelectableOptionCount = maxSelectableOptionCount;
   44: 		SoundEventPath = soundEventPath;
   45: 		IsSeachAvailable = isSeachAvailable;
   46: 	}
   47: 
   48: 	public bool HasSameContentWith(object other)
   49: 	{
   50: 		if (other is MultiSelectionInquiryData multiSelectionInquiryData)
   51: 		{
   52: 			bool flag = true;
   53: 			if (InquiryElements.Count == multiSelectionInquiryData.InquiryElements.Count)
   54: 			{
   55: 				for (int i = 0; i < InquiryElements.Count; i++)
   56: 				{
   57: 					if (!InquiryElements[i].HasSameContentWith(multiSelectionInquiryData.InquiryElements[i]))
   58: 					{
   59: 						flag = false;
   60: 					}
   61: 				}
   62: 			}
   63: 			else
   64: 			{
   65: 				flag = false;
   66: 			}
   67: 			if (TitleText == multiSelectionInquiryData.TitleText && DescriptionText == multiSelectionInquiryData.DescriptionText && flag && IsExitShown == multiSelectionInquiryData.IsExitShown && AffirmativeText == multiSelectionInquiryData.AffirmativeText && NegativeText == multiSelectionInquiryData.NegativeText && AffirmativeAction == multiSelectionInquiryData.AffirmativeAction && NegativeAction == multiSelectionInquiryData.NegativeAction && MinSelectableOptionCount == multiSelectionInquiryData.MinSelectableOptionCount && MaxSelectableOptionCount == multiSelectionInquiryData.MaxSelectableOptionCount)
   68: 			{
   69: 				return SoundEventPath == multiSelectionInquiryData.SoundEventPath;
   70: 			}
   71: 			return false;
   72: 		}
   73: 		return false;
   74: 	}
   75: }

 succeeded in 571ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MultiSelectionInquiryData.cs
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Information\MultiSelectionElementsWidget.cs
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.ViewModelCollection\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\MultiSelectionQueryPopUpVM.cs

 succeeded in 518ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core.ViewModelCollection\TaleWorlds.Core.ViewModelCollection.Information\InquiryElementVM.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade.ViewModelCollection\\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\\MultiSelectionQueryPopUpVM.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -le 260){'"'{0,5}: {1}' -f "'$i,$_} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Core.ViewModelCollection\\TaleWorlds.Core.ViewModelCollection.Information\\InquiryElementVM.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade.GauntletUI.Widgets\\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Information\\MultiSelectionElementsWidget.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 489ms:
    1: using System.Collections.Generic;
    2: using TaleWorlds.GauntletUI;
    3: using TaleWorlds.GauntletUI.BaseTypes;
    4: 
    5: namespace TaleWorlds.MountAndBlade.GauntletUI.Widgets.Information;
    6: 
    7: public class MultiSelectionElementsWidget(UIContext context) : Widget(context)
    8: {
    9: 	private bool _updateRequired;
   10: 
   11: 	private List<ButtonWidget> _elementsList = new List<ButtonWidget>();
   12: 
   13: 	private ButtonWidget _doneButtonWidget;
   14: 
   15: 	private ListPanel _elementContainer;
   16: 
   17: 	[Editor(false)]
   18: 	public ButtonWidget DoneButtonWidget
   19: 	{
   20: 		get
   21: 		{
   22: 			return _doneButtonWidget;
   23: 		}
   24: 		set
   25: 		{
   26: 			if (_doneButtonWidget != value)
   27: 			{
   28: 				_doneButtonWidget = value;
   29: 				OnPropertyChanged(value, "DoneButtonWidget");
   30: 			}
   31: 		}
   32: 	}
   33: 
   34: 	protected override void OnLateUpdate(float dt)
   35: 	{
   36: 		base.OnLateUpdate(dt);
   37: 		if (_updateRequired)
   38: 		{
   39: 			UpdateElementsList();
   40: 			_updateRequired = false;
   41: 		}
   42: 	}
   43: 
   44: 	protected override void OnChildAdded(Widget child)
   45: 	{
   46: 		base.OnChildAdded(child);
   47: 		if (child is ListPanel)
   48: 		{
   49: 			_elementContainer = child as ListPanel;
   50: 			_elementContainer.ItemAddEventHandlers.Add(OnElementAdded);
   51: 		}
   52: 	}
   53: 
   54: 	private void OnElementAdded(Widget parentWidget, Widget addedWidget)
   55: 	{
   56: 		_updateRequired = true;
   57: 	}
   58: 
   59: 	private void UpdateElementsList()
   60: 	{
   61: 		_elementsList.Clear();
   62: 		for (int i = 0; i < _elementContainer.ChildCount; i++)
   63: 		{
   64: 			ButtonWidget item = _elementContainer.GetChild(i).GetChild(0) as ButtonWidget;
   65: 			_elementsList.Add(item);
   66: 		}
   67: 	}
   68: }

 succeeded in 558ms:
    1: using System;
    2: using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
    3: using TaleWorlds.Library;
    4: using TaleWorlds.Localization;
    5: 
    6: namespace TaleWorlds.Core.ViewModelCollection.Information;
    7: 
    8: public class InquiryElementVM : ViewModel
    9: {
   10: 	public readonly InquiryElement InquiryElement;
   11: 
   12: 	private readonly Action<InquiryElementVM, bool> _onSelectedStateChanged;
   13: 
   14: 	private bool _isFilteredOut;
   15: 
   16: 	private bool _isSelected;
   17: 
   18: 	private bool _isEnabled;
   19: 
   20: 	private string _text;
   21: 
   22: 	private bool _hasVisuals;
   23: 
   24: 	private ImageIdentifierVM _imageIdentifier;
   25: 
   26: 	private HintViewModel _hint;
   27: 
   28: 	[DataSourceProperty]
   29: 	public bool IsFilteredOut
   30: 	{
   31: 		get
   32: 		{
   33: 			return _isFilteredOut;
   34: 		}
   35: 		set
   36: 		{
   37: 			if (_isFilteredOut != value)
   38: 			{
   39: 				_isFilteredOut = value;
   40: 				OnPropertyChangedWithValue(value, "IsFilteredOut");
   41: 			}
   42: 		}
   43: 	}
   44: 
   45: 	[DataSourceProperty]
   46: 	public bool IsSelected
   47: 	{
   48: 		get
   49: 		{
   50: 			return _isSelected;
   51: 		}
   52: 		set
   53: 		{
   54: 			if (_isSelected != value)
   55: 			{
   56: 				_isSelected = value;
   57: 				OnPropertyChangedWithValue(value, "IsSelected");
   58: 				_onSelectedStateChanged?.Invoke(this, value);
   59: 			}
   60: 		}
   61: 	}
   62: 
   63: 	[DataSourceProperty]
   64: 	public bool HasVisuals
   65: 	{
   66: 		get
   67: 		{
   68: 			return _hasVisuals;
   69: 		}
   70: 		set
   71: 		{
   72: 			if (_hasVisuals != value)
   73: 			{
   74: 				_hasVisuals = value;
   75: 				OnPropertyChangedWithValue(value, "HasVisuals");
   76: 			}
   77: 		}
   78: 	}
   79: 
   80: 	[DataSourceProperty]
   81: 	public bool IsEnabled
   82: 	{
   83: 		get
   84: 		{
   85: 			return _isEnabled;
   86: 		}
   87: 		set
   88: 		{
   89: 			if (_isEnabled != value)
   90: 			{
   91: 				_isEnabled = value;
   92: 				OnPropertyChangedWithValue(value, "IsEnabled");
   93: 			}
   94: 		}
   95: 	}
   96: 
   97: 	[DataSourceProperty]
   98: 	public string Text
   99: 	{
  100: 		get
  101: 		{
  102: 			return _text;
  103: 		}
  104: 		set
  105: 		{
  106: 			if (_text != value)
  107: 			{
  108: 				_text = value;
  109: 				OnPropertyChangedWithValue(value, "Text");
  110: 			}
  111: 		}
  112: 	}
  113: 
  114: 	[DataSourceProperty]
  115: 	public ImageIdentifierVM ImageIdentifier
  116: 	{
  117: 		get
  118: 		{
  119: 			return _imageIdentifier;
  120: 		}
  121: 		set
  122: 		{
  123: 			if (_imageIdentifier != value)
  124: 			{
  125: 				_imageIdentifier = value;
  126: 				OnPropertyChangedWithValue(value, "ImageIdentifier");
  127: 			}
  128: 		}
  129: 	}
  130: 
  131: 	[DataSourceProperty]
  132: 	public HintViewModel Hint
  133: 	{
  134: 		get
  135: 		{
  136: 			return _hint;
  137: 		}
  138: 		set
  139: 		{
  140: 			if (_hint != value)
  141: 			{
  142: 				_hint = value;
  143: 				OnPropertyChangedWithValue(value, "Hint");
  144: 			}
  145: 		}
  146: 	}
  147: 
  148: 	public InquiryElementVM(InquiryElement elementData, TextObject hint, Action<InquiryElementVM, bool> onSelectedStateChanged = null)
  149: 	{
  150: 		Text = elementData.Title;
  151: 		ImageIdentifier = new GenericImageIdentifierVM(elementData.ImageIdentifier);
  152: 		InquiryElement = elementData;
  153: 		IsEnabled = elementData.IsEnabled;
  154: 		HasVisuals = elementData.ImageIdentifier != null;
  155: 		Hint = new HintViewModel(hint);
  156: 		_onSelectedStateChanged = onSelectedStateChanged;
  157: 	}
  158: }

 succeeded in 593ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using TaleWorlds.Core;
    4: using TaleWorlds.Core.ViewModelCollection.Information;
    5: using TaleWorlds.Library;
    6: using TaleWorlds.Localization;
    7: 
    8: namespace TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;
    9: 
   10: public class MultiSelectionQueryPopUpVM : PopUpBaseVM
   11: {
   12: 	private MultiSelectionInquiryData _data;
   13: 
   14: 	private int _selectedOptionCount;
   15: 
   16: 	private MBBindingList<InquiryElementVM> _inquiryElements;
   17: 
   18: 	private int _maxSelectableOptionCount;
   19: 
   20: 	private int _minSelectableOptionCount;
   21: 
   22: 	private bool _isSearchAvailable;
   23: 
   24: 	private string _searchText;
   25: 
   26: 	private string _searchPlaceholderText;
   27: 
   28: 	[DataSourceProperty]
   29: 	public MBBindingList<InquiryElementVM> InquiryElements
   30: 	{
   31: 		get
   32: 		{
   33: 			return _inquiryElements;
   34: 		}
   35: 		set
   36: 		{
   37: 			if (value != _inquiryElements)
   38: 			{
   39: 				_inquiryElements = value;
   40: 				OnPropertyChangedWithValue(value, "InquiryElements");
   41: 			}
   42: 		}
   43: 	}
   44: 
   45: 	[DataSourceProperty]
   46: 	public int MaxSelectableOptionCount
   47: 	{
   48: 		get
   49: 		{
   50: 			return _maxSelectableOptionCount;
   51: 		}
   52: 		set
   53: 		{
   54: 			if (value != _maxSelectableOptionCount)
   55: 			{
   56: 				_maxSelectableOptionCount = value;
   57: 				OnPropertyChangedWithValue(value, "MaxSelectableOptionCount");
   58: 			}
   59: 		}
   60: 	}
   61: 
   62: 	[DataSourceProperty]
   63: 	public int MinSelectableOptionCount
   64: 	{
   65: 		get
   66: 		{
   67: 			return _minSelectableOptionCount;
   68: 		}
   69: 		set
   70: 		{
   71: 			if (value != _minSelectableOptionCount)
   72: 			{
   73: 				_minSelectableOptionCount = value;
   74: 				OnPropertyChangedWithValue(value, "MinSelectableOptionCount");
   75: 			}
   76: 		}
   77: 	}
   78: 
   79: 	[DataSourceProperty]
   80: 	public bool IsSearchAvailable
   81: 	{
   82: 		get
   83: 		{
   84: 			return _isSearchAvailable;
   85: 		}
   86: 		set
   87: 		{
   88: 			if (value != _isSearchAvailable)
   89: 			{
   90: 				_isSearchAvailable = value;
   91: 				OnPropertyChangedWithValue(value, "IsSearchAvailable");
   92: 			}
   93: 		}
   94: 	}
   95: 
   96: 	[DataSourceProperty]
   97: 	public string SearchText
   98: 	{
   99: 		get
  100: 		{
  101: 			return _searchText;
  102: 		}
  103: 		set
  104: 		{
  105: 			if (value != _searchText)
  106: 			{
  107: 				bool isAppending = value.IndexOf(_searchText ?? "") >= 0;
  108: 				_searchText = value;
  109: 				OnPropertyChangedWithValue(value, "SearchText");
  110: 				UpdateInquiryFilter(_searchText, isAppending);
  111: 			}
  112: 		}
  113: 	}
  114: 
  115: 	[DataSourceProperty]
  116: 	public string SearchPlaceholderText
  117: 	{
  118: 		get
  119: 		{
  120: 			return _searchPlaceholderText;
  121: 		}
  122: 		set
  123: 		{
  124: 			if (value != _searchPlaceholderText)
  125: 			{
  126: 				_searchPlaceholderText = value;
  127: 				OnPropertyChangedWithValue(value, "SearchPlaceholderText");
  128: 			}
  129: 		}
  130: 	}
  131: 
  132: 	public MultiSelectionQueryPopUpVM(Action closeQuery)
  133: 		: base(closeQuery)
  134: 	{
  135: 		InquiryElements = new MBBindingList<InquiryElementVM>();
  136: 		MaxSelectableOptionCount = 0;
  137: 		MinSelectableOptionCount = 0;
  138: 		_selectedOptionCount = 0;
  139: 	}
  140: 
  141: 	public void SetData(MultiSelectionInquiryData data)
  142: 	{
  143: 		_data = data;
  144: 		InquiryElements.Clear();
  145: 		foreach (InquiryElement inquiryElement in _data.InquiryElements)
  146: 		{
  147: 			TextObject hint = (string.IsNullOrEmpty(inquiryElement.Hint) ? TextObject.GetEmpty() : new TextObject("{=!}" + inquiryElement.Hint));
  148: 			InquiryElementVM item = new InquiryElementVM(inquiryElement, hint, OnInquiryElementSelected);
  149: 			InquiryElements.Add(item);
  150: 		}
  151: 		base.TitleText = _data.TitleText;
  152: 		base.PopUpLabel = _data.DescriptionText;
  153: 		MaxSelectableOptionCount = _data.MaxSelectableOptionCount;
  154: 		MinSelectableOptionCount = _data.MinSelectableOptionCount;
  155: 		base.ButtonOkLabel = _data.AffirmativeText;
  156: 		base.ButtonCancelLabel = _data.NegativeText;
  157: 		base.IsButtonOkShown = true;
  158: 		base.IsButtonCancelShown = _data.IsExitShown;
  159: 		IsSearchAvailable = _data.IsSeachAvailable;
  160: 		SearchPlaceholderText = new TextObject("{=tQOPRBFg}Search...").ToString();
  161: 		RefreshIsButtonOkEnabled();
  162: 	}
  163: 
  164: 	private void OnInquiryElementSelected(InquiryElementVM elementVM, bool isSelected)
  165: 	{
  166: 		if (isSelected)
  167: 		{
  168: 			_selectedOptionCount++;
  169: 			if (MaxSelectableOptionCount == 1)
  170: 			{
  171: 				foreach (InquiryElementVM inquiryElement in InquiryElements)
  172: 				{
  173: 					if (inquiryElement != elementVM)
  174: 					{
  175: 						inquiryElement.IsSelected = false;
  176: 					}
  177: 				}
  178: 			}
  179: 		}
  180: 		else
  181: 		{
  182: 			_selectedOptionCount--;
  183: 		}
  184: 		RefreshIsButtonOkEnabled();
  185: 	}
  186: 
  187: 	public override void ExecuteAffirmativeAction()
  188: 	{
  189: 		if (_data.AffirmativeAction != null)
  190: 		{
  191: 			List<InquiryElement> list = new List<InquiryElement>();
  192: 			foreach (InquiryElementVM inquiryElement in InquiryElements)
  193: 			{
  194: 				if (inquiryElement.IsSelected)
  195: 				{
  196: 					list.Add(inquiryElement.InquiryElement);
  197: 				}
  198: 			}
  199: 			_data.AffirmativeAction(list);
  200: 		}
  201: 		CloseQuery();
  202: 	}
  203: 
  204: 	public override void ExecuteNegativeAction()
  205: 	{
  206: 		_data.NegativeAction?.Invoke(new List<InquiryElement>());
  207: 		CloseQuery();
  208: 	}
  209: 
  210: 	public override void OnClearData()
  211: 	{
  212: 		base.OnClearData();
  213: 		_data = null;
  214: 		MaxSelectableOptionCount = 0;
  215: 		MinSelectableOptionCount = 0;
  216: 		_selectedOptionCount = 0;
  217: 	}
  218: 
  219: 	private void RefreshIsButtonOkEnabled()
  220: 	{
  221: 		base.IsButtonOkEnabled = (MaxSelectableOptionCount <= 0 || _selectedOptionCount <= MaxSelectableOptionCount) && _selectedOptionCount >= MinSelectableOptionCount;
  222: 	}
  223: 
  224: 	private void UpdateInquiryFilter(string searchText, bool isAppending)
  225: 	{
  226: 		string value = searchText.ToLower();
  227: 		for (int i = 0; i < InquiryElements.Count; i++)
  228: 		{
  229: 			InquiryElementVM inquiryElementVM = InquiryElements[i];
  230: 			if (!isAppending || !inquiryElementVM.IsFilteredOut)
  231: 			{
  232: 				inquiryElementVM.IsFilteredOut = !inquiryElementVM.Text.ToLower().Contains(value);
  233: 			}
  234: 		}
  235: 	}
  236: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'InquiryElementVM|IsSelected|IsEnabled' -SimpleMatch | Select-Object -First 50 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1703ms:
codex
The inquiry VM does not revalidate `IsEnabled` in `ExecuteAffirmativeAction`; it trusts the UI selection state. That does not create an underpay path here because the service rechecks affordability and offer membership before grant/charge, but I’m noting the trust boundary when I write S2.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter '*ConversationManager*.cs' -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'OpenMapConversation' | Select-Object -First 20 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'OnConversationEnded\\(' | Select-Object -First 50 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 544ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\ConversationManager.cs

 succeeded in 1373ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:837: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:842: eventReceivers[i].OnConversationEnded(characters);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:373: public virtual void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1873: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\ConversationManager.cs:788: CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:16281: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:16286: eventReceivers[i].OnConversationEnded(characters);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:18360: public virtual void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:20970: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:84974: CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:16196: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:16201: eventReceivers[i].OnConversationEnded(characters);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:18275: public virtual void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:20885: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:84009: CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:16153: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:16158: eventReceivers[i].OnConversationEnded(characters);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:18232: public virtual void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:20842: public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:83966: CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);

 succeeded in 1375ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\CampaignMapConversation.cs:7: Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\ConversationManager.cs:1094: public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:83755: Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:85284: public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:85286: TaleWorlds.Library.Debug.Assert(GameStateManager.Current?.ActiveState is MapState, "GameStateManager needs to be initialized and active state needs to be MapState for map conversation to open", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Conversation\\ConversationManager.cs", "OpenMapConversation", 1493);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:82792: Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:84315: public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:82749: Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:84272: public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\ConversationManager.cs'; "'$lines=Get-Content $p; for($i=760;$i -le 805;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\ConversationManager.cs'; "'$lines=Get-Content $p; for($i=1086;$i -le 1125;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEventDispatcher.cs'; "'$lines=Get-Content $p; for($i=829;$i -le 846;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs'; "'$lines=Get-Content $p; for($i=1868;$i -le 1878;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 581ms:
  760: 	}
  761: 
  762: 	public void EndConversation()
  763: 	{
  764: 		Debug.Print("--------------- Conversation End --------------- ", 0, Debug.DebugColor.White, 4503599627370496uL);
  765: 		if (CampaignMission.Current != null)
  766: 		{
  767: 			foreach (IAgent conversationAgent in ConversationAgents)
  768: 			{
  769: 				CampaignMission.Current.OnConversationEnd(conversationAgent);
  770: 			}
  771: 		}
  772: 		_conversationParty = null;
  773: 		if (this.ConversationEndOneShot != null)
  774: 		{
  775: 			this.ConversationEndOneShot();
  776: 			this.ConversationEndOneShot = null;
  777: 		}
  778: 		if (this.ConversationEnd != null)
  779: 		{
  780: 			this.ConversationEnd();
  781: 		}
  782: 		IsConversationInProgress = false;
  783: 		foreach (IAgent conversationAgent2 in ConversationAgents)
  784: 		{
  785: 			conversationAgent2.SetAsConversationAgent(set: false);
  786: 		}
  787: 		Campaign.Current.CurrentConversationContext = ConversationContext.Default;
  788: 		CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);
  789: 		if (GetPersuasionIsActive())
  790: 		{
  791: 			EndPersuasion();
  792: 		}
  793: 		_conversationAgents.Clear();
  794: 		_speakerAgent = null;
  795: 		_listenerAgent = null;
  796: 		_mainAgent = null;
  797: 		if (IsConversationFlowActive)
  798: 		{
  799: 			OnConversationDeactivate();
  800: 		}
  801: 		Handler?.OnConversationUninstall();
  802: 	}
  803: 
  804: 	public void DoOption(int optionIndex)
  805: 	{

 succeeded in 608ms:
 1086: 		if (_tags.TryGetValue(tagId, out var value))
 1087: 		{
 1088: 			return value.IsApplicableTo(character);
 1089: 		}
 1090: 		Debug.FailedAssert("Asking for a nonexistent tag: " + tagId, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Conversation\\ConversationManager.cs", "IsTagApplicable", 1486);
 1091: 		return false;
 1092: 	}
 1093: 
 1094: 	public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
 1095: 	{
 1096: 		(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(playerCharacterData, conversationPartnerData);
 1097: 		SetupAndStartMapConversation(conversationPartnerData.Party?.MobileParty, new MapConversationAgent(conversationPartnerData.Character), new MapConversationAgent(CharacterObject.PlayerCharacter));
 1098: 	}
 1099: 
 1100: 	public static void StartPersuasion(float goalValue, float successValue, float failValue, float criticalSuccessValue, float criticalFailValue, float initialProgress = -1f, PersuasionDifficulty difficulty = PersuasionDifficulty.Medium)
 1101: 	{
 1102: 		_persuasion = new TaleWorlds.CampaignSystem.Conversation.Persuasion.Persuasion(goalValue, successValue, failValue, criticalSuccessValue, criticalFailValue, initialProgress, difficulty);
 1103: 	}
 1104: 
 1105: 	public static void EndPersuasion()
 1106: 	{
 1107: 		_persuasion = null;
 1108: 	}
 1109: 
 1110: 	public static void PersuasionCommitProgress(PersuasionOptionArgs persuasionOptionArgs)
 1111: 	{
 1112: 		_persuasion.CommitProgress(persuasionOptionArgs);
 1113: 	}
 1114: 
 1115: 	public static void Clear()
 1116: 	{
 1117: 		_persuasion = null;
 1118: 	}
 1119: 
 1120: 	public void GetPersuasionChanceValues(out float successValue, out float critSuccessValue, out float critFailValue)
 1121: 	{
 1122: 		successValue = _persuasion.SuccessValue;
 1123: 		critSuccessValue = _persuasion.CriticalSuccessValue;
 1124: 		critFailValue = _persuasion.CriticalFailValue;
 1125: 	}

 succeeded in 621ms:
  829: 	{
  830: 		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  831: 		for (int i = 0; i < eventReceivers.Length; i++)
  832: 		{
  833: 			eventReceivers[i].OnVillageLooted(village);
  834: 		}
  835: 	}
  836: 
  837: 	public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
  838: 	{
  839: 		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  840: 		for (int i = 0; i < eventReceivers.Length; i++)
  841: 		{
  842: 			eventReceivers[i].OnConversationEnded(characters);
  843: 		}
  844: 	}
  845: 
  846: 	public override void OnAgentJoinedConversation(IAgent agent)

 succeeded in 619ms:
 1868: 	public override void OnAgentJoinedConversation(IAgent agent)
 1869: 	{
 1870: 		Instance._onAgentJoinedConversationEvent.Invoke(agent);
 1871: 	}
 1872: 
 1873: 	public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
 1874: 	{
 1875: 		Instance._onConversationEnded.Invoke(characters);
 1876: 	}
 1877: 
 1878: 	public override void OnMapEventEnded(MapEvent mapEvent)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MapState.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 518ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs

codex
Vanilla confirms `ConversationManager.EndConversation` dispatches `OnConversationEnded` before clearing conversation characters, and `CampaignEvents.ConversationEnded` is the public event wrapper. That supports the in-house greeting-flag fix for `CampaignMapConversation.OpenConversation`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameState\\MapState.cs -Pattern \"OnMapConversationStarts|StartMapConversation|Conversation\" -Context 3,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 529ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:2: using TaleWorlds.CampaignSystem.Conversation;
  using Helpers;
  using TaleWorlds.CampaignSystem.Encounters;
  using TaleWorlds.CampaignSystem.Incidents;
  using TaleWorlds.CampaignSystem.Party;
  using TaleWorlds.Core;
  using TaleWorlds.Library;
  using TaleWorlds.ObjectSystem;
  
  namespace TaleWorlds.CampaignSystem.GameState;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:18: 	private bool _mapConversationActive;
  
  	private MenuContext _menuContext;
  
  
  	private bool _closeScreenNextFrame;
  
  	private IMapStateHandler _handler;
  
  	private BattleSimulation _battleSimulation;
  
  	public Incident NextIncident
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:64: 	public bool MapConversationActive => _mapConversationActive;
  
  	public bool AtMenu => MenuContext != null;
  
  
  	public IMapStateHandler Handler
  	{
  		get
  		{
  			return _handler;
  		}
  		set
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:209: 	public void OnMapConversationStarts(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  		_handler?.OnGameLoadFinished();
  	}
  
  	{
  		_mapConversationActive = true;
  		_handler?.OnMapConversationStarts(playerCharacterData, conversationPartnerData);
  	}
  
  	public void OnMapConversationOver()
  	{
  		_handler?.OnMapConversationOver();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:211: 		_mapConversationActive = true;
  
  	public void OnMapConversationStarts(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  	{
  		_handler?.OnMapConversationStarts(playerCharacterData, conversationPartnerData);
  	}
  
  	public void OnMapConversationOver()
  	{
  		_handler?.OnMapConversationOver();
  		_mapConversationActive = false;
  		if (Game.Current.GameStateManager.ActiveState is MapState)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:212: 		_handler?.OnMapConversationStarts(playerCharacterData, conversationPartnerData);
  	public void OnMapConversationStarts(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  	{
  		_mapConversationActive = true;
  	}
  
  	public void OnMapConversationOver()
  	{
  		_handler?.OnMapConversationOver();
  		_mapConversationActive = false;
  		if (Game.Current.GameStateManager.ActiveState is MapState)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:215: 	public void OnMapConversationOver()
  		_handler?.OnMapConversationStarts(playerCharacterData, conversationPartnerData);
  	}
  
  	{
  		_handler?.OnMapConversationOver();
  		_mapConversationActive = false;
  		if (Game.Current.GameStateManager.ActiveState is MapState)
  		{
  			MenuContext?.Refresh();
  		}
  		RefreshHandler();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:217: 		_handler?.OnMapConversationOver();
  
  	public void OnMapConversationOver()
  	{
  		_mapConversationActive = false;
  		if (Game.Current.GameStateManager.ActiveState is MapState)
  		{
  			MenuContext?.Refresh();
  		}
  		RefreshHandler();
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:218: 		_mapConversationActive = false;
  	public void OnMapConversationOver()
  	{
  		_handler?.OnMapConversationOver();
  		if (Game.Current.GameStateManager.ActiveState is MapState)
  		{
  			MenuContext?.Refresh();
  		}
  		RefreshHandler();
  	}
  
  	internal void OnSignalPeriodicEvents()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\MapState.cs:240: 		if (!Campaign.Current.ConversationManager.IsConversationFlowActive)
  	protected override void OnActivate()
  	{
  		base.OnActivate();
  		{
  			MenuContext?.Refresh();
  		}
  		RefreshHandler();
  	}
  
  	public void EnterMenuMode()
  	{

codex
`OpenMapConversation` assumes the active state is a `MapState`; settlement game menus run in that state, and `MapState.OnMapConversationStarts` toggles map-conversation state and notifies the handler. I’ll cite that for the clean-transition question rather than overstate more than the decompile proves.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameState\\MapState.cs'; "'$lines=Get-Content $p; for($i=204;$i -le 222;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 530ms:
  204: 		}
  205: 		Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
  206: 		_handler?.OnGameLoadFinished();
  207: 	}
  208: 
  209: 	public void OnMapConversationStarts(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
  210: 	{
  211: 		_mapConversationActive = true;
  212: 		_handler?.OnMapConversationStarts(playerCharacterData, conversationPartnerData);
  213: 	}
  214: 
  215: 	public void OnMapConversationOver()
  216: 	{
  217: 		_handler?.OnMapConversationOver();
  218: 		_mapConversationActive = false;
  219: 		if (Game.Current.GameStateManager.ActiveState is MapState)
  220: 		{
  221: 			MenuContext?.Refresh();
  222: 		}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignGameStarter.cs -Pattern \"AddGameMenuOption\" -Context 3,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter GameMenu.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter GameMenuOption.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 616ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs

 succeeded in 637ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignGameStarter.cs:93: 	public void AddGameMenuOption(string menuId, string optionId, string optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, bool isLeave = false, int index = -1, bool isRepeatable = false, object relatedObject = null)
  		GetPresumedGameMenu(idString).Initialize(new TextObject(text), initDelegate, condition, consequence, tick, type, overlay, targetWaitHours, flags, relatedObject);
  	}
  
  	{
  		GetPresumedGameMenu(menuId).AddOption(optionId, new TextObject(optionText), condition, consequence, index, isLeave, isRepeatable, relatedObject);
  	}
  
  	public GameMenu GetPresumedGameMenu(string stringId)
  	{
  		GameMenu gameMenu = _gameMenuManager.GetGameMenu(stringId);
  		if (gameMenu == null)

 succeeded in 583ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenuOption.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignGameStarter.cs'; "'$lines=Get-Content $p; for($i=88;$i -le 103;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameMenus\\GameMenu.cs -Pattern \"AddOption|Insert|index|Options\" -Context 2,8 | Select-Object -First 40 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameMenus\\GameMenuOption.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -le 120){'"'{0,5}: {1}' -f "'$i,$_} }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 532ms:
   88: 	public void AddWaitGameMenu(string idString, string text, OnInitDelegate initDelegate, OnConditionDelegate condition, OnConsequenceDelegate consequence, OnTickDelegate tick, GameMenu.MenuAndOptionType type, GameMenu.MenuOverlayType overlay = GameMenu.MenuOverlayType.None, float targetWaitHours = 0f, GameMenu.MenuFlags flags = GameMenu.MenuFlags.None, object relatedObject = null)
   89: 	{
   90: 		GetPresumedGameMenu(idString).Initialize(new TextObject(text), initDelegate, condition, consequence, tick, type, overlay, targetWaitHours, flags, relatedObject);
   91: 	}
   92: 
   93: 	public void AddGameMenuOption(string menuId, string optionId, string optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, bool isLeave = false, int index = -1, bool isRepeatable = false, object relatedObject = null)
   94: 	{
   95: 		GetPresumedGameMenu(menuId).AddOption(optionId, new TextObject(optionText), condition, consequence, index, isLeave, isRepeatable, relatedObject);
   96: 	}
   97: 
   98: 	public GameMenu GetPresumedGameMenu(string stringId)
   99: 	{
  100: 		GameMenu gameMenu = _gameMenuManager.GetGameMenu(stringId);
  101: 		if (gameMenu == null)
  102: 		{
  103: 			gameMenu = new GameMenu(stringId);

 succeeded in 575ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:65: 			if (MenuRepeatObjects.Count <= CurrentRepeatableIndex)
  		get
  		{
  			{
  				return null;
  			}
  			return MenuRepeatObjects[CurrentRepeatableIndex];
  		}
  	}
  
  	public bool IsWaitMenu { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:69: 			return MenuRepeatObjects[CurrentRepeatableIndex];
  				return null;
  			}
  		}
  	}
  
  	public bool IsWaitMenu { get; private set; }
  
  	public bool IsWaitActive { get; private set; }
  
  	public bool IsEmpty
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:99: 	public int CurrentRepeatableIndex { get; set; }
  	public OnConsequenceDelegate OnConsequence { get; private set; }
  
  
  	public IEnumerable<GameMenuOption> MenuOptions => _menuItems;
  
  	public bool AutoSelectFirst { get; private set; }
  
  	internal GameMenu(string idString)
  	{
  		StringId = idString;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:101: 	public IEnumerable<GameMenuOption> MenuOptions => _menuItems;
  	public int CurrentRepeatableIndex { get; set; }
  
  
  	public bool AutoSelectFirst { get; private set; }
  
  	internal GameMenu(string idString)
  	{
  		StringId = idString;
  		_menuItems = new List<GameMenuOption>();
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:113: 		CurrentRepeatableIndex = 0;
  	internal void Initialize(TextObject text, OnInitDelegate initDelegate, MenuOverlayType overlay, MenuFlags flags = MenuFlags.None, object relatedObject = null)
  	{
  		LastSelectedMenuObject = null;
  		_defaultText = text;
  		OnInit = initDelegate;
  		OverlayType = overlay;
  		AutoSelectFirst = (flags & MenuFlags.AutoSelectFirst) != 0;
  		RelatedObject = relatedObject;
  		IsReady = true;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:125: 		CurrentRepeatableIndex = 0;
  	internal void Initialize(TextObject text, OnInitDelegate initDelegate, OnConditionDelegate condition, OnConsequenceDelegate consequence, OnTickDelegate tick, MenuAndOptionType type, MenuOverlayType overlay, float targetWaitHours = 0f, MenuFlags flags = MenuFlags.None, object relatedObject = null)
  	{
  		LastSelectedMenuObject = null;
  		_defaultText = text;
  		OnInit = initDelegate;
  		OverlayType = overlay;
  		AutoSelectFirst = (flags & MenuFlags.AutoSelectFirst) != 0;
  		RelatedObject = relatedObject;
  		OnConsequence = consequence;
  		OnCondition = condition;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:146: 	private void AddOption(GameMenuOption newOption, int index = -1)
  	}
  
  	{
  		if (index >= 0 && _menuItems.Count >= index)
  		{
  			_menuItems.Insert(index, newOption);
  		}
  		else
  		{
  			_menuItems.Add(newOption);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:148: 		if (index >= 0 && _menuItems.Count >= index)
  	private void AddOption(GameMenuOption newOption, int index = -1)
  	{
  		{
  			_menuItems.Insert(index, newOption);
  		}
  		else
  		{
  			_menuItems.Add(newOption);
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:150: 			_menuItems.Insert(index, newOption);
  		if (index >= 0 && _menuItems.Count >= index)
  		{
  		}
  		else
  		{
  			_menuItems.Add(newOption);
  		}
  	}
  
  	public bool GetMenuOptionConditionsHold(Game game, MenuContext menuContext, int menuItemNumber)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:281: 			CampaignEventDispatcher.Instance.OnGameMenuOptionSelected(this, gameMenuOption);
  		if (Campaign.Current != null)
  		{
  		}
  	}
  
  	public void StartWait()
  	{
  		_previousTickTime = CampaignTime.Now;
  		IsWaitActive = true;
  		Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:301: 		CurrentRepeatableIndex = 0;
  	{
  		Progress = 0f;
  		MenuRepeatObjects.Clear();
  	}
  
  	public void RunOnInit(Game game, MenuContext menuContext)
  	{
  		ResetVariablesOnInit();
  		MenuCallbackArgs menuCallbackArgs = new MenuCallbackArgs(menuContext, MenuTitle);
  		if (OnInit != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:382: 	internal void AddOption(string optionId, TextObject optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, int index = -1, bool isLeave = false, bool isRepeatable = false, object relatedObject = null)
  	}
  
  	{
  		AddOption(new GameMenuOption(MenuAndOptionType.RegularMenuOption, optionId, optionText, optionText, condition, consequence, isLeave, isRepeatable, relatedObject), index);
  	}
  
  	internal void RemoveMenuOption(GameMenuOption option)
  	{
  		_menuItems.Remove(option);
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\GameMenu.cs:384: 		AddOption(new GameMenuOption(MenuAndOptionType.RegularMenuOption, optionId, optionText, optionText, condition, consequence, isLeave, isRepeatable, relatedObject), index);
  	internal void AddOption(string optionId, TextObject optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, int index = -1, bool isLeave = false, bool isRepeatable = false, object relatedObject = null)
  	{
  	}
  
  	internal void RemoveMenuOption(GameMenuOption option)
  	{
  		_menuItems.Remove(option);
  	}
  }

 succeeded in 602ms:
    1: using System;
    2: using TaleWorlds.CampaignSystem.GameState;
    3: using TaleWorlds.Core;
    4: using TaleWorlds.Localization;
    5: 
    6: namespace TaleWorlds.CampaignSystem.GameMenus;
    7: 
    8: public class GameMenuOption
    9: {
   10: 	public delegate bool OnConditionDelegate(MenuCallbackArgs args);
   11: 
   12: 	public delegate void OnConsequenceDelegate(MenuCallbackArgs args);
   13: 
   14: 	public enum LeaveType
   15: 	{
   16: 		Default,
   17: 		Mission,
   18: 		Submenu,
   19: 		BribeAndEscape,
   20: 		Escape,
   21: 		Craft,
   22: 		ForceToGiveGoods,
   23: 		ForceToGiveTroops,
   24: 		Bribe,
   25: 		LeaveTroopsAndFlee,
   26: 		OrderTroopsToAttack,
   27: 		Raid,
   28: 		HostileAction,
   29: 		Recruit,
   30: 		Trade,
   31: 		Wait,
   32: 		Leave,
   33: 		Continue,
   34: 		Manage,
   35: 		TroopSelection,
   36: 		WaitQuest,
   37: 		Surrender,
   38: 		Conversation,
   39: 		DefendAction,
   40: 		Devastate,
   41: 		Pillage,
   42: 		ShowMercy,
   43: 		Leaderboard,
   44: 		OpenStash,
   45: 		ManageGarrison,
   46: 		StagePrisonBreak,
   47: 		ManagePrisoners,
   48: 		Ransom,
   49: 		PracticeFight,
   50: 		BesiegeTown,
   51: 		SneakIn,
   52: 		LeadAssault,
   53: 		DonateTroops,
   54: 		DonatePrisoners,
   55: 		SiegeAmbush,
   56: 		Warehouse,
   57: 		VisitPort,
   58: 		VisitTown,
   59: 		SetSail,
   60: 		ManageFleet,
   61: 		CallFleet,
   62: 		OrderShipsToAttack,
   63: 		RepairShips
   64: 	}
   65: 
   66: 	[Flags]
   67: 	public enum IssueQuestFlags
   68: 	{
   69: 		None = 0,
   70: 		AvailableIssue = 1,
   71: 		ActiveIssue = 2,
   72: 		ActiveStoryQuest = 4,
   73: 		TrackedIssue = 8,
   74: 		TrackedStoryQuest = 0x10
   75: 	}
   76: 
   77: 	public static IssueQuestFlags[] IssueQuestFlagsValues = (IssueQuestFlags[])Enum.GetValues(typeof(IssueQuestFlags));
   78: 
   79: 	public OnConditionDelegate OnCondition;
   80: 
   81: 	public OnConsequenceDelegate OnConsequence;
   82: 
   83: 	public GameMenu.MenuAndOptionType Type { get; private set; }
   84: 
   85: 	public LeaveType OptionLeaveType { get; set; }
   86: 
   87: 	public IssueQuestFlags OptionQuestData { get; set; }
   88: 
   89: 	public string IdString { get; private set; }
   90: 
   91: 	public TextObject Text { get; private set; }
   92: 
   93: 	public TextObject Text2 { get; private set; }
   94: 
   95: 	public TextObject Tooltip { get; private set; }
   96: 
   97: 	public bool IsLeave { get; private set; }
   98: 
   99: 	public bool IsRepeatable { get; private set; }
  100: 
  101: 	public bool IsEnabled { get; private set; }
  102: 
  103: 	public object RelatedObject { get; private set; }
  104: 
  105: 	internal GameMenuOption()
  106: 	{
  107: 		Text = null;
  108: 		Tooltip = null;
  109: 		IsEnabled = true;
  110: 	}
  111: 
  112: 	public GameMenuOption(GameMenu.MenuAndOptionType type, string idString, TextObject text, TextObject text2, OnConditionDelegate condition, OnConsequenceDelegate consequence, bool isLeave = false, bool isRepeatable = false, object relatedObject = null)
  113: 	{
  114: 		Type = type;
  115: 		IdString = idString;
  116: 		Text = text;
  117: 		Text2 = text2;
  118: 		OnCondition = condition;
  119: 		OnConsequence = consequence;
  120: 		Tooltip = null;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameMenus\\GameMenu.cs'; "'$lines=Get-Content $p; for($i=140;$i -le 175;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=260;$i -le 286;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=378;$i -le 386;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 512ms:
  140: 
  141: 	public void SetMenuRepeatObjects(IEnumerable<object> list)
  142: 	{
  143: 		MenuRepeatObjects = list.ToList();
  144: 	}
  145: 
  146: 	private void AddOption(GameMenuOption newOption, int index = -1)
  147: 	{
  148: 		if (index >= 0 && _menuItems.Count >= index)
  149: 		{
  150: 			_menuItems.Insert(index, newOption);
  151: 		}
  152: 		else
  153: 		{
  154: 			_menuItems.Add(newOption);
  155: 		}
  156: 	}
  157: 
  158: 	public bool GetMenuOptionConditionsHold(Game game, MenuContext menuContext, int menuItemNumber)
  159: 	{
  160: 		if (IsWaitMenu)
  161: 		{
  162: 			if (_menuItems[menuItemNumber].GetConditionsHold(game, menuContext))
  163: 			{
  164: 				return RunWaitMenuCondition(menuContext);
  165: 			}
  166: 			return false;
  167: 		}
  168: 		return _menuItems[menuItemNumber].GetConditionsHold(game, menuContext);
  169: 	}
  170: 
  171: 	public TextObject GetMenuOptionText(int menuItemNumber)
  172: 	{
  173: 		return _menuItems[menuItemNumber].Text;
  174: 	}
  175: 
  260: 		{
  261: 			MenuCallbackArgs args = new MenuCallbackArgs(menuContext, MenuTitle);
  262: 			OnConsequence(args);
  263: 		}
  264: 	}
  265: 
  266: 	public void RunMenuOptionConsequence(MenuContext menuContext, int menuItemNumber)
  267: 	{
  268: 		if (menuItemNumber >= _menuItems.Count || menuItemNumber < 0)
  269: 		{
  270: 			Debug.FailedAssert("menuItemNumber out of bounds", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenu.cs", "RunMenuOptionConsequence", 263);
  271: 			menuItemNumber = _menuItems.Count - 1;
  272: 		}
  273: 		GameMenuOption gameMenuOption = _menuItems[menuItemNumber];
  274: 		if (gameMenuOption.IsLeave && IsWaitMenu)
  275: 		{
  276: 			EndWait();
  277: 		}
  278: 		gameMenuOption.RunConsequence(menuContext);
  279: 		if (Campaign.Current != null)
  280: 		{
  281: 			CampaignEventDispatcher.Instance.OnGameMenuOptionSelected(this, gameMenuOption);
  282: 		}
  283: 	}
  284: 
  285: 	public void StartWait()
  286: 	{
  378: 		Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
  379: 		Campaign.Current.GameMenuManager.ExitToLast();
  380: 	}
  381: 
  382: 	internal void AddOption(string optionId, TextObject optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, int index = -1, bool isLeave = false, bool isRepeatable = false, object relatedObject = null)
  383: 	{
  384: 		AddOption(new GameMenuOption(MenuAndOptionType.RegularMenuOption, optionId, optionText, optionText, condition, consequence, isLeave, isRepeatable, relatedObject), index);
  385: 	}
  386: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameMenus\\GameMenuOption.cs'; "'$lines=Get-Content $p; for($i=120;$i -le 190;$i++){ if($i -le $lines.Count){'"'{0,5}: {1}' -f "'$i,$lines[$i-1]}}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 535ms:
  120: 		Tooltip = null;
  121: 		IsRepeatable = isRepeatable;
  122: 		IsEnabled = true;
  123: 		IsLeave = isLeave;
  124: 		RelatedObject = relatedObject;
  125: 	}
  126: 
  127: 	public bool GetConditionsHold(Game game, MenuContext menuContext)
  128: 	{
  129: 		if (OnCondition != null)
  130: 		{
  131: 			MenuCallbackArgs menuCallbackArgs = new MenuCallbackArgs(menuContext, Text);
  132: 			bool result = OnCondition(menuCallbackArgs);
  133: 			IsEnabled = menuCallbackArgs.IsEnabled;
  134: 			Tooltip = menuCallbackArgs.Tooltip;
  135: 			OptionQuestData = menuCallbackArgs.OptionQuestData;
  136: 			OptionLeaveType = menuCallbackArgs.optionLeaveType;
  137: 			return result;
  138: 		}
  139: 		return true;
  140: 	}
  141: 
  142: 	public void RunConsequence(MenuContext menuContext)
  143: 	{
  144: 		if (OnConsequence != null)
  145: 		{
  146: 			MenuCallbackArgs args = new MenuCallbackArgs(menuContext, Text);
  147: 			OnConsequence(args);
  148: 		}
  149: 		menuContext.OnConsequence(this);
  150: 	}
  151: 
  152: 	public void SetEnable(bool isEnable)
  153: 	{
  154: 		IsEnabled = isEnable;
  155: 	}
  156: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter Hero.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter Settlement.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter TroopRoster.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter MobileParty.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 573ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs

 succeeded in 598ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs

 succeeded in 603ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs

 succeeded in 600ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Settlement.cs -Pattern \"OwnerClan|Culture|CurrentSettlement|Bound\" -Context 2,8 | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs -Pattern \"OneToOneConversationHero|MainHero|PartyBelongedTo\" -Context 2,8 | Select-Object -First 60 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern \"AddToCounts\\(|Add\\(\" -Context 2,10 | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 567ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:70: 	public CultureObject Culture;
  	private CampaignVec2 _position;
  
  
  	private TextObject _name;
  
  	[SaveableField(129)]
  	private MBList<Village> _boundVillages;
  
  	[SaveableField(131)]
  	private MobileParty _lastAttackerParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:75: 	private MBList<Village> _boundVillages;
  
  	[SaveableField(129)]
  
  	[SaveableField(131)]
  	private MobileParty _lastAttackerParty;
  
  	[SaveableField(148)]
  	private MBList<SiegeEvent.SiegeEngineMissile> _siegeEngineMissiles;
  
  	public Town Town;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:107: 	public Hero Owner => OwnerClan.Leader;
  	public bool IsActive { get; set; }
  
  
  	public Banner Banner
  	{
  		get
  		{
  			if (Party.CustomBanner != null)
  			{
  				return Party.CustomBanner;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:121: 			if (OwnerClan != null)
  				return SettlementComponent.GetDefaultComponentBanner();
  			}
  			{
  				return OwnerClan.Banner;
  			}
  			return null;
  		}
  	}
  
  	public bool IsVisible
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:123: 				return OwnerClan.Banner;
  			if (OwnerClan != null)
  			{
  			}
  			return null;
  		}
  	}
  
  	public bool IsVisible
  	{
  		get
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:317: 	public MBReadOnlyList<Village> BoundVillages => _boundVillages;
  	public ItemRoster ItemRoster => Party.ItemRoster;
  
  
  	public MobileParty LastAttackerParty
  	{
  		get
  		{
  			return _lastAttackerParty;
  		}
  		set
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:462: 	public static Settlement CurrentSettlement
  	public LocationComplex LocationComplex { get; private set; }
  
  	{
  		get
  		{
  			if (PlayerCaptivity.CaptorParty != null && PlayerCaptivity.CaptorParty.IsSettlement)
  			{
  				return PlayerCaptivity.CaptorParty.Settlement;
  			}
  			if (PlayerEncounter.EncounterSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:474: 			if (MobileParty.MainParty.CurrentSettlement != null)
  				return PlayerEncounter.EncounterSettlement;
  			}
  			{
  				return MobileParty.MainParty.CurrentSettlement;
  			}
  			return null;
  		}
  	}
  
  	public static MBReadOnlyList<Settlement> All => Campaign.Current.Settlements;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:476: 				return MobileParty.MainParty.CurrentSettlement;
  			if (MobileParty.MainParty.CurrentSettlement != null)
  			{
  			}
  			return null;
  		}
  	}
  
  	public static MBReadOnlyList<Settlement> All => Campaign.Current.Settlements;
  
  	public static Settlement GetFirst => All.FirstOrDefault();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:489: 	public Clan OwnerClan
  	public SiegeState CurrentSiegeState { get; private set; }
  
  	{
  		get
  		{
  			if (Village != null)
  			{
  				return Village.Bound.OwnerClan;
  			}
  			if (Town != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:495: 				return Village.Bound.OwnerClan;
  			if (Village != null)
  			{
  			}
  			if (Town != null)
  			{
  				return Town.OwnerClan;
  			}
  			if (IsHideout)
  			{
  				return Hideout.MapFaction as Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:499: 				return Town.OwnerClan;
  			if (Town != null)
  			{
  			}
  			if (IsHideout)
  			{
  				return Hideout.MapFaction as Clan;
  			}
  			return null;
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:520: 		collectedObjects.Add(_boundVillages);
  		collectedObjects.Add(_nextLocatable);
  		collectedObjects.Add(_settlementWallSectionHitPointsRatioList);
  		collectedObjects.Add(_lastAttackerParty);
  		collectedObjects.Add(_siegeEngineMissiles);
  		collectedObjects.Add(Party);
  		collectedObjects.Add(SiegeEvent);
  		collectedObjects.Add(SettlementComponent);
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(LastThreatTime, collectedObjects);
  		collectedObjects.Add(SiegeEngines);
  		collectedObjects.Add(SiegeStrategy);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:658: 	internal static object AutoGeneratedGetMemberValue_boundVillages(object o)
  	}
  
  	{
  		return ((Settlement)o)._boundVillages;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_lastAttackerParty(object o)
  	{
  		return ((Settlement)o)._lastAttackerParty;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:660: 		return ((Settlement)o)._boundVillages;
  	internal static object AutoGeneratedGetMemberValue_boundVillages(object o)
  	{
  	}
  
  	internal static object AutoGeneratedGetMemberValue_lastAttackerParty(object o)
  	{
  		return ((Settlement)o)._lastAttackerParty;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_siegeEngineMissiles(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:722: 	internal void AddBoundVillageInternal(Village village)
  	}
  
  	{
  		_boundVillages.Add(village);
  	}
  
  	internal void RemoveBoundVillageInternal(Village village)
  	{
  		_boundVillages.Remove(village);
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:724: 		_boundVillages.Add(village);
  	internal void AddBoundVillageInternal(Village village)
  	{
  	}
  
  	internal void RemoveBoundVillageInternal(Village village)
  	{
  		_boundVillages.Remove(village);
  	}
  
  	private void InitSettlement()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:727: 	internal void RemoveBoundVillageInternal(Village village)
  	}
  
  	{
  		_boundVillages.Remove(village);
  	}
  
  	private void InitSettlement()
  	{
  		_partiesCache = new MBList<MobileParty>();
  		_heroesWithoutPartyCache = new MBList<Hero>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:729: 		_boundVillages.Remove(village);
  	internal void RemoveBoundVillageInternal(Village village)
  	{
  	}
  
  	private void InitSettlement()
  	{
  		_partiesCache = new MBList<MobileParty>();
  		_heroesWithoutPartyCache = new MBList<Hero>();
  		_notablesCache = new MBList<Hero>();
  		_boundVillages = new MBList<Village>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:737: 		_boundVillages = new MBList<Village>();
  		_heroesWithoutPartyCache = new MBList<Hero>();
  		_notablesCache = new MBList<Hero>();
  		SettlementHitPoints = 1f;
  		CurrentSiegeState = SiegeState.OnTheWalls;
  		float currentTime = Campaign.CurrentTime;
  		LastVisitTimeOfOwner = currentTime;
  	}
  
  	public bool IsUnderRebellionAttack()
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:799: 	public float GetValue(Hero hero = null, bool countAlsoBoundedSettlements = true)
  	}
  
  	{
  		float num = 0f;
  		if (IsVillage)
  		{
  			num = 100000f + Village.Hearth * 250f;
  			num *= ((Village.VillageState == Village.VillageStates.Looted) ? 0.8f : ((Village.VillageState == Village.VillageStates.BeingRaided) ? 0.85f : (0.8f + (0.667f + 0.333f * Village.Settlement.SettlementHitPoints) * 0.2f)));
  		}
  		else if (IsCastle)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:823: 		if (countAlsoBoundedSettlements)
  			num2 *= (100f - value) / 100f;
  		}
  		{
  			foreach (Village boundVillage in BoundVillages)
  			{
  				num += boundVillage.Settlement.GetValue(hero, countAlsoBoundedSettlements: false);
  			}
  		}
  		return num * num2;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:825: 			foreach (Village boundVillage in BoundVillages)
  		if (countAlsoBoundedSettlements)
  		{
  			{
  				num += boundVillage.Settlement.GetValue(hero, countAlsoBoundedSettlements: false);
  			}
  		}
  		return num * num2;
  	}
  
  	public override TextObject GetName()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:827: 				num += boundVillage.Settlement.GetValue(hero, countAlsoBoundedSettlements: false);
  			foreach (Village boundVillage in BoundVillages)
  			{
  			}
  		}
  		return num * num2;
  	}
  
  	public override TextObject GetName()
  	{
  		return Name;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:961: 		Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>("culture", node);
  			SetPortPosition(portPosition);
  		}
  		EncyclopediaText = ((node.Attributes["text"] != null) ? new TextObject(node.Attributes["text"].Value) : TextObject.GetEmpty());
  		foreach (XmlNode childNode in node.ChildNodes)
  		{
  			if (childNode.Name == "Components")
  			{
  				foreach (XmlNode childNode2 in childNode.ChildNodes)
  				{
  					SettlementComponent settlementComponent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:1041: 				Town.OwnerClan = clan;
  			if (clan != null && Town != null)
  			{
  			}
  		}
  		_name.SetSettlementProperties(this);
  	}
  
  	public void OnFinishLoadState()
  	{
  		if (IsFortification)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:1091: 			if (item.CurrentSettlement != this)
  		foreach (MobileParty item in MobileParty.All)
  		{
  			{
  				continue;
  			}
  			if (IsFortification)
  			{
  				CampaignVec2 positionAfterMapChange = (item.IsCurrentlyAtSea ? PortPosition : GatePosition);
  				item.SetPositionAfterMapChange(positionAfterMapChange);
  				continue;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:1210: 		GarrisonPartyComponent.CreateGarrisonParty("garrison_party_" + base.StringId + "_" + OwnerClan.StringId + "_1", this);
  	public void AddGarrisonParty()
  	{
  	}
  
  	protected override void AfterLoad()
  	{
  		if (SiegeEvent != null && SiegeEvent.BesiegerCamp.LeaderParty == null)
  		{
  			if (SiegeEvent.BesiegedSettlement.Party.MapEvent != null)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:1267: 		AddTroopToMilitiaParty(militiaParty, Culture.MeleeMilitiaTroop, Culture.MeleeEliteMilitiaTroop, meleeTroopRate, militiaNumberToAdd);
  	{
  		Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaSpawnRate(this, out var meleeTroopRate, out var rangedTroopRate);
  		AddTroopToMilitiaParty(militiaParty, Culture.RangedMilitiaTroop, Culture.RangedEliteMilitiaTroop, rangedTroopRate, militiaNumberToAdd);
  	}
  
  	private void AddTroopToMilitiaParty(MobileParty militiaParty, CharacterObject militiaTroop, CharacterObject veteranMilitiaTroop, float troopRatio, int militiaNumberToAdd)
  	{
  		int num = MBRandom.RoundRandomized(troopRatio * (float)militiaNumberToAdd);
  		float resultNumber = Campaign.Current.Models.SettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance(this).ResultNumber;
  		for (int i = 0; i < num; i++)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:1268: 		AddTroopToMilitiaParty(militiaParty, Culture.RangedMilitiaTroop, Culture.RangedEliteMilitiaTroop, rangedTroopRate, militiaNumberToAdd);
  		Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaSpawnRate(this, out var meleeTroopRate, out var rangedTroopRate);
  		AddTroopToMilitiaParty(militiaParty, Culture.MeleeMilitiaTroop, Culture.MeleeEliteMilitiaTroop, meleeTroopRate, militiaNumberToAdd);
  	}
  
  	private void AddTroopToMilitiaParty(MobileParty militiaParty, CharacterObject militiaTroop, CharacterObject veteranMilitiaTroop, float troopRatio, int militiaNumberToAdd)
  	{
  		int num = MBRandom.RoundRandomized(troopRatio * (float)militiaNumberToAdd);
  		float resultNumber = Campaign.Current.Models.SettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance(this).ResultNumber;
  		for (int i = 0; i < num; i++)
  		{

 succeeded in 590ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:121: 	private MobileParty _partyBelongedTo;
  	[XmlIgnore]
  	[SaveableField(560)]
  
  	[SaveableField(580)]
  	private Settlement _stayingInSettlement;
  
  	[SaveableField(590)]
  	public MBList<ItemObject> SpecialItems;
  
  	[SaveableField(412)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:281: 			if (PartyBelongedTo == null || Clan == null)
  		get
  		{
  			{
  				yield break;
  			}
  			foreach (Hero companion in Clan.Companions)
  			{
  				if (companion.PartyBelongedTo == PartyBelongedTo)
  				{
  					yield return companion;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:287: 				if (companion.PartyBelongedTo == PartyBelongedTo)
  			foreach (Hero companion in Clan.Companions)
  			{
  				{
  					yield return companion;
  				}
  			}
  		}
  	}
  
  	[SaveableProperty(780)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:379: 			if (PartyBelongedTo != null)
  		get
  		{
  			{
  				return PartyBelongedTo.LeaderHero == this;
  			}
  			return false;
  		}
  	}
  
  	public bool IsNotable
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:381: 				return PartyBelongedTo.LeaderHero == this;
  			if (PartyBelongedTo != null)
  			{
  			}
  			return false;
  		}
  	}
  
  	public bool IsNotable
  	{
  		get
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:427: 				if (PartyBelongedTo != null)
  			if (health <= WoundedHealthLimit != _health <= WoundedHealthLimit)
  			{
  				{
  					PartyBelongedTo.MemberRoster.OnHeroHealthStatusChanged(this);
  				}
  				if (PartyBelongedToAsPrisoner != null)
  				{
  					PartyBelongedToAsPrisoner.PrisonRoster.OnHeroHealthStatusChanged(this);
  				}
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:429: 					PartyBelongedTo.MemberRoster.OnHeroHealthStatusChanged(this);
  				if (PartyBelongedTo != null)
  				{
  				}
  				if (PartyBelongedToAsPrisoner != null)
  				{
  					PartyBelongedToAsPrisoner.PrisonRoster.OnHeroHealthStatusChanged(this);
  				}
  			}
  			if (health > WoundedHealthLimit && IsWounded)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:431: 				if (PartyBelongedToAsPrisoner != null)
  					PartyBelongedTo.MemberRoster.OnHeroHealthStatusChanged(this);
  				}
  				{
  					PartyBelongedToAsPrisoner.PrisonRoster.OnHeroHealthStatusChanged(this);
  				}
  			}
  			if (health > WoundedHealthLimit && IsWounded)
  			{
  				CampaignEventDispatcher.Instance.OnHeroWounded(this);
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:433: 					PartyBelongedToAsPrisoner.PrisonRoster.OnHeroHealthStatusChanged(this);
  				if (PartyBelongedToAsPrisoner != null)
  				{
  				}
  			}
  			if (health > WoundedHealthLimit && IsWounded)
  			{
  				CampaignEventDispatcher.Instance.OnHeroWounded(this);
  			}
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:583: 			if (PartyBelongedTo != null)
  				return HomeSettlement.MapFaction;
  			}
  			{
  				return PartyBelongedTo.MapFaction;
  			}
  			return null;
  		}
  	}
  
  	public List<Alley> OwnedAlleys { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:585: 				return PartyBelongedTo.MapFaction;
  			if (PartyBelongedTo != null)
  			{
  			}
  			return null;
  		}
  	}
  
  	public List<Alley> OwnedAlleys { get; private set; }
  
  	public bool IsFactionLeader
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:631: 	public MobileParty PartyBelongedTo
  	public List<CaravanPartyComponent> OwnedCaravans { get; private set; }
  
  	{
  		get
  		{
  			return _partyBelongedTo;
  		}
  		private set
  		{
  			SetPartyBelongedTo(value);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:635: 			return _partyBelongedTo;
  		get
  		{
  		}
  		private set
  		{
  			SetPartyBelongedTo(value);
  		}
  	}
  
  	[SaveableProperty(570)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:639: 			SetPartyBelongedTo(value);
  		private set
  		{
  		}
  	}
  
  	[SaveableProperty(570)]
  	public PartyBase PartyBelongedToAsPrisoner { get; private set; }
  
  	public Settlement StayingInSettlement
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:644: 	public PartyBase PartyBelongedToAsPrisoner { get; private set; }
  
  	[SaveableProperty(570)]
  
  	public Settlement StayingInSettlement
  	{
  		get
  		{
  			return _stayingInSettlement;
  		}
  		set
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:667: 	public bool IsHumanPlayerCharacter => this == MainHero;
  	}
  
  
  	public bool IsKnownToPlayer
  	{
  		get
  		{
  			return _isKnownToPlayer;
  		}
  		set
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:746: 			if (PartyBelongedTo != null)
  		{
  			Settlement result = null;
  			{
  				result = PartyBelongedTo.CurrentSettlement;
  			}
  			else if (PartyBelongedToAsPrisoner != null)
  			{
  				result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement : (PartyBelongedToAsPrisoner.IsMobile ? PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement : null));
  			}
  			else if (StayingInSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:748: 				result = PartyBelongedTo.CurrentSettlement;
  			if (PartyBelongedTo != null)
  			{
  			}
  			else if (PartyBelongedToAsPrisoner != null)
  			{
  				result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement : (PartyBelongedToAsPrisoner.IsMobile ? PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement : null));
  			}
  			else if (StayingInSettlement != null)
  			{
  				result = StayingInSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:750: 			else if (PartyBelongedToAsPrisoner != null)
  				result = PartyBelongedTo.CurrentSettlement;
  			}
  			{
  				result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement : (PartyBelongedToAsPrisoner.IsMobile ? PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement : null));
  			}
  			else if (StayingInSettlement != null)
  			{
  				result = StayingInSettlement;
  			}
  			return result;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:752: 				result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement : (PartyBelongedToAsPrisoner.IsMobile ? PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement : null));
  			else if (PartyBelongedToAsPrisoner != null)
  			{
  			}
  			else if (StayingInSettlement != null)
  			{
  				result = StayingInSettlement;
  			}
  			return result;
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:891: 	public static Hero MainHero => CharacterObject.PlayerCharacter.HeroObject;
  	public static MBReadOnlyList<Hero> DeadOrDisabledHeroes => Campaign.Current.DeadOrDisabledHeroes;
  
  
  	public static Hero OneToOneConversationHero => Campaign.Current.ConversationManager.OneToOneConversationHero;
  
  	public static bool IsMainHeroIll => Campaign.Current.MainHeroIllDays != -1;
  
  	internal static void AutoGeneratedStaticCollectObjectsHero(object o, List<object> collectedObjects)
  	{
  		((Hero)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:893: 	public static Hero OneToOneConversationHero => Campaign.Current.ConversationManager.OneToOneConversationHero;
  	public static Hero MainHero => CharacterObject.PlayerCharacter.HeroObject;
  
  
  	public static bool IsMainHeroIll => Campaign.Current.MainHeroIllDays != -1;
  
  	internal static void AutoGeneratedStaticCollectObjectsHero(object o, List<object> collectedObjects)
  	{
  		((Hero)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:895: 	public static bool IsMainHeroIll => Campaign.Current.MainHeroIllDays != -1;
  	public static Hero OneToOneConversationHero => Campaign.Current.ConversationManager.OneToOneConversationHero;
  
  
  	internal static void AutoGeneratedStaticCollectObjectsHero(object o, List<object> collectedObjects)
  	{
  		((Hero)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:923: 		collectedObjects.Add(_partyBelongedTo);
  		collectedObjects.Add(_governorOf);
  		collectedObjects.Add(_ownedWorkshops);
  		collectedObjects.Add(_stayingInSettlement);
  		collectedObjects.Add(_bornSettlement);
  		collectedObjects.Add(_father);
  		collectedObjects.Add(_mother);
  		collectedObjects.Add(_exSpouses);
  		collectedObjects.Add(_spouse);
  		collectedObjects.Add(_children);
  		collectedObjects.Add(_heroDeveloper);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:940: 		collectedObjects.Add(PartyBelongedToAsPrisoner);
  		collectedObjects.Add(DeathMarkKillerHero);
  		collectedObjects.Add(LastKnownClosestSettlement);
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(LastMeetingTimeWithPlayer, collectedObjects);
  	}
  
  	internal static object AutoGeneratedGetMemberValueStaticBodyProperties(object o)
  	{
  		return ((Hero)o).StaticBodyProperties;
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1024: 	internal static object AutoGeneratedGetMemberValuePartyBelongedToAsPrisoner(object o)
  	}
  
  	{
  		return ((Hero)o).PartyBelongedToAsPrisoner;
  	}
  
  	internal static object AutoGeneratedGetMemberValueLastMeetingTimeWithPlayer(object o)
  	{
  		return ((Hero)o).LastMeetingTimeWithPlayer;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1026: 		return ((Hero)o).PartyBelongedToAsPrisoner;
  	internal static object AutoGeneratedGetMemberValuePartyBelongedToAsPrisoner(object o)
  	{
  	}
  
  	internal static object AutoGeneratedGetMemberValueLastMeetingTimeWithPlayer(object o)
  	{
  		return ((Hero)o).LastMeetingTimeWithPlayer;
  	}
  
  	internal static object AutoGeneratedGetMemberValueRandomValue(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1174: 	internal static object AutoGeneratedGetMemberValue_partyBelongedTo(object o)
  	}
  
  	{
  		return ((Hero)o)._partyBelongedTo;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_stayingInSettlement(object o)
  	{
  		return ((Hero)o)._stayingInSettlement;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1176: 		return ((Hero)o)._partyBelongedTo;
  	internal static object AutoGeneratedGetMemberValue_partyBelongedTo(object o)
  	{
  	}
  
  	internal static object AutoGeneratedGetMemberValue_stayingInSettlement(object o)
  	{
  		return ((Hero)o)._stayingInSettlement;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_isKnownToPlayer(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1250: 		if (PartyBelongedTo != null && PartyBelongedTo.LeaderHero == this)
  		_name = fullName;
  		_firstName = firstName;
  		{
  			PartyBelongedTo.PartyComponent.ClearCachedName();
  		}
  	}
  
  	public void OnIssueCreatedForHero(IssueBase issue)
  	{
  		Issue = issue;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1252: 			PartyBelongedTo.PartyComponent.ClearCachedName();
  		if (PartyBelongedTo != null && PartyBelongedTo.LeaderHero == this)
  		{
  		}
  	}
  
  	public void OnIssueCreatedForHero(IssueBase issue)
  	{
  		Issue = issue;
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1548: 			if (CurrentSettlement != null && PartyBelongedTo == null && PartyBelongedToAsPrisoner == null)
  				_clan.OnLordAdded(this);
  			}
  			{
  				CurrentSettlement.AddHeroWithoutParty(this);
  			}
  		}
  		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.0") && FirstName != null && Name != null && this != MainHero)
  		{
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FIRSTNAME"))
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1553: 		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.0") && FirstName != null && Name != null && this != MainHero)
  			}
  		}
  		{
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FIRSTNAME"))
  			{
  				Name.SetTextVariable("FIRSTNAME", FirstName.CopyTextObject());
  			}
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FEMALE"))
  			{
  				Name.SetTextVariable("FEMALE", IsFemale ? 1 : 0);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1568: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) && !CharacterObject.IsTemplate && !CharacterObject.HiddenInEncyclopedia && PartyBelongedTo != null && PartyBelongedTo.LeaderHero != this && (CharacterObject.Occupation == Occupation.Soldier || CharacterObject.Occupation == Occupation.Mercenary || CharacterObject.Occupation == Occupation.Bandit || CharacterObject.Occupation == Occupation.Gangster || CharacterObject.Occupation == Occupation.CaravanGuard || (CharacterObject.Occupation == Occupation.Villager && CharacterObject.UpgradeTargets.Length != 0)))
  			Name.Attributes["FIRSTNAME"] = new TextObject(Name.Value);
  		}
  		{
  			PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.14")) && PartyBelongedToAsPrisoner != null && StayingInSettlement != null)
  		{
  			StayingInSettlement = null;
  			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1570: 			PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) && !CharacterObject.IsTemplate && !CharacterObject.HiddenInEncyclopedia && PartyBelongedTo != null && PartyBelongedTo.LeaderHero != this && (CharacterObject.Occupation == Occupation.Soldier || CharacterObject.Occupation == Occupation.Mercenary || CharacterObject.Occupation == Occupation.Bandit || CharacterObject.Occupation == Occupation.Gangster || CharacterObject.Occupation == Occupation.CaravanGuard || (CharacterObject.Occupation == Occupation.Villager && CharacterObject.UpgradeTargets.Length != 0)))
  		{
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.14")) && PartyBelongedToAsPrisoner != null && StayingInSettlement != null)
  		{
  			StayingInSettlement = null;
  			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  			{
  				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1572: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.14")) && PartyBelongedToAsPrisoner != null && StayingInSettlement != null)
  			PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  		}
  		{
  			StayingInSettlement = null;
  			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  			{
  				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			}
  			PartyBelongedToAsPrisoner = null;
  			if (!IsDead && !IsDisabled)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1575: 			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  		{
  			StayingInSettlement = null;
  			{
  				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			}
  			PartyBelongedToAsPrisoner = null;
  			if (!IsDead && !IsDisabled)
  			{
  				ChangeState(CharacterStates.Fugitive);
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1577: 				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  			{
  			}
  			PartyBelongedToAsPrisoner = null;
  			if (!IsDead && !IsDisabled)
  			{
  				ChangeState(CharacterStates.Fugitive);
  			}
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.4.0") && OriginClan == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1579: 			PartyBelongedToAsPrisoner = null;
  				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			}
  			if (!IsDead && !IsDisabled)
  			{
  				ChangeState(CharacterStates.Fugitive);
  			}
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.4.0") && OriginClan == null)
  		{
  			if (Father != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1617: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.9.35637")) && GovernorOf != null && (PartyBelongedTo != null || PartyBelongedToAsPrisoner != null))
  		}
  		HeroDeveloper?.AfterLoad();
  		{
  			ChangeGovernorAction.RemoveGovernorOf(this);
  		}
  		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")))
  		{
  			if (this != MainHero && IsPrisoner && this != MainHero && IsPrisoner && PartyBelongedToAsPrisoner == null && CurrentSettlement != null)
  			{
  				PartyBelongedToAsPrisoner = CurrentSettlement.Party;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1623: 			if (this != MainHero && IsPrisoner && this != MainHero && IsPrisoner && PartyBelongedToAsPrisoner == null && CurrentSettlement != null)
  		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")))
  		{
  			{
  				PartyBelongedToAsPrisoner = CurrentSettlement.Party;
  			}
  			MobileParty mainParty = MobileParty.MainParty;
  			if (this != MainHero && PartyBelongedTo == mainParty && !mainParty.MemberRoster.Contains(CharacterObject))
  			{
  				MakeHeroFugitiveAction.Apply(this);
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1625: 				PartyBelongedToAsPrisoner = CurrentSettlement.Party;
  			if (this != MainHero && IsPrisoner && this != MainHero && IsPrisoner && PartyBelongedToAsPrisoner == null && CurrentSettlement != null)
  			{
  			}
  			MobileParty mainParty = MobileParty.MainParty;
  			if (this != MainHero && PartyBelongedTo == mainParty && !mainParty.MemberRoster.Contains(CharacterObject))
  			{
  				MakeHeroFugitiveAction.Apply(this);
  			}
  			if (mainParty.MemberRoster.Contains(CharacterObject) && PartyBelongedTo != mainParty)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1628: 			if (this != MainHero && PartyBelongedTo == mainParty && !mainParty.MemberRoster.Contains(CharacterObject))
  			}
  			MobileParty mainParty = MobileParty.MainParty;
  			{
  				MakeHeroFugitiveAction.Apply(this);
  			}
  			if (mainParty.MemberRoster.Contains(CharacterObject) && PartyBelongedTo != mainParty)
  			{
  				mainParty.MemberRoster.RemoveTroop(CharacterObject, mainParty.MemberRoster.GetElementNumber(CharacterObject));
  				if (!Campaign.Current.IssueManager.IssueSolvingCompanionList.Contains(this))
  				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1632: 			if (mainParty.MemberRoster.Contains(CharacterObject) && PartyBelongedTo != mainParty)
  				MakeHeroFugitiveAction.Apply(this);
  			}
  			{
  				mainParty.MemberRoster.RemoveTroop(CharacterObject, mainParty.MemberRoster.GetElementNumber(CharacterObject));
  				if (!Campaign.Current.IssueManager.IssueSolvingCompanionList.Contains(this))
  				{
  					MobileParty partyBelongedTo = PartyBelongedTo;
  					if (partyBelongedTo != null && !partyBelongedTo.IsCaravan)
  					{
  						MakeHeroFugitiveAction.Apply(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1637: 					MobileParty partyBelongedTo = PartyBelongedTo;
  				if (!Campaign.Current.IssueManager.IssueSolvingCompanionList.Contains(this))
  				{
  					if (partyBelongedTo != null && !partyBelongedTo.IsCaravan)
  					{
  						MakeHeroFugitiveAction.Apply(this);
  					}
  				}
  			}
  			if (Spouse != null && (Spouse.Clan != Clan || Clan == null || Age < (float)Campaign.Current.Models.AgeModel.HeroComesOfAge))
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1638: 					if (partyBelongedTo != null && !partyBelongedTo.IsCaravan)
  				{
  					MobileParty partyBelongedTo = PartyBelongedTo;
  					{
  						MakeHeroFugitiveAction.Apply(this);
  					}
  				}
  			}
  			if (Spouse != null && (Spouse.Clan != Clan || Clan == null || Age < (float)Campaign.Current.Models.AgeModel.HeroComesOfAge))
  			{
  				Hero spouse = Spouse;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1699: 			if (PartyBelongedTo != null)
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")) && !CharacterObject.IsTemplate && !CharacterObject.HiddenInEncyclopedia && (CharacterObject.Occupation == Occupation.Soldier || CharacterObject.Occupation == Occupation.Mercenary || CharacterObject.Occupation == Occupation.Bandit || CharacterObject.Occupation == Occupation.Gangster || CharacterObject.Occupation == Occupation.CaravanGuard || (CharacterObject.Occupation == Occupation.Villager && CharacterObject.UpgradeTargets.Length != 0)))
  		{
  			{
  				if (PartyBelongedTo.LeaderHero == this)
  				{
  					DestroyPartyAction.Apply(null, PartyBelongedTo);
  				}
  				else
  				{
  					PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1701: 				if (PartyBelongedTo.LeaderHero == this)
  			if (PartyBelongedTo != null)
  			{
  				{
  					DestroyPartyAction.Apply(null, PartyBelongedTo);
  				}
  				else
  				{
  					PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  				}
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1703: 					DestroyPartyAction.Apply(null, PartyBelongedTo);
  				if (PartyBelongedTo.LeaderHero == this)
  				{
  				}
  				else
  				{
  					PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  				}
  			}
  			if (PartyBelongedToAsPrisoner != null)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1707: 					PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  				else
  				{
  				}
  			}
  			if (PartyBelongedToAsPrisoner != null)
  			{
  				EndCaptivityAction.ApplyByDeath(this);
  			}
  			if (IsAlive)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1710: 			if (PartyBelongedToAsPrisoner != null)
  				}
  			}
  			{
  				EndCaptivityAction.ApplyByDeath(this);
  			}
  			if (IsAlive)
  			{
  				KillCharacterAction.ApplyByRemove(this);
  			}
  			Campaign.Current.CampaignObjectManager.UnregisterDeadHero(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1721: 		if (!MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.4.1.113285")) || this == MainHero || !IsAlive || IsSpecial || !(Age > (float)Campaign.Current.Models.AgeModel.HeroComesOfAge))
  			Campaign.Current.ObjectManager.UnregisterObject(CharacterObject);
  		}
  		{
  			return;
  		}
  		IEnumerable<Equipment> battleEquipments = MBEquipmentRosterExtensions.All.Find((MBEquipmentRoster x) => x.StringId == "generic_bat_dummy").GetBattleEquipments();
  		IEnumerable<Equipment> civilianEquipments = MBEquipmentRosterExtensions.All.Find((MBEquipmentRoster x) => x.StringId == "generic_civ_dummy").GetCivilianEquipments();
  		Equipment other = battleEquipments.First();
  		Equipment equipment = civilianEquipments.First();
  		bool flag = false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1874: 			if (o.Clan == MainHero.Clan)
  		if (o.Clan.IsMinorFaction || o.Clan.IsRebelClan)
  		{
  			{
  				MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=REWGj2ge}a rising new clan");
  			}
  			else if (o.Clan.IsSect)
  			{
  				MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=IlRC9Drl}a religious sect");
  			}
  			else if (o.Clan.IsClanTypeMercenary)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1898: 			if (o == MainHero)
  				MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=KZxKVby0}an organization");
  			}
  			{
  				if (o.GetTraitLevel(DefaultTraits.Mercy) == 0 && o.GetTraitLevel(DefaultTraits.Honor) == 0 && o.GetTraitLevel(DefaultTraits.Generosity) == 0 && o.GetTraitLevel(DefaultTraits.Valor) == 0 && o.GetTraitLevel(DefaultTraits.Calculating) == 0)
  				{
  					return new TextObject("{=V097rA1v}{LORD.FIRSTNAME} is a head of the {CLAN_NAME}, a rising new clan. {?LORD.GENDER}She{?}He{\\?} is still making {?LORD.GENDER}her{?}his{\\?} reputation.");
  				}
  				return new TextObject("{=hRfXSdlP}{LORD.FIRSTNAME} is a head of the {CLAN_NAME}, {CLAN_DESCRIPTION} from the lands of the {FACTION_NAME}. {?LORD.GENDER}She{?}He{\\?} has the reputation of being {REPUTATION}.");
  			}
  			return new TextObject("{=9Obe3S6L}{LORD.FIRSTNAME} is a member of the {CLAN_NAME}, {CLAN_DESCRIPTION} from the lands of the {FACTION_NAME}. {?LORD.GENDER}She{?}He{\\?} has the reputation of being {REPUTATION}.");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1968: 	private void SetPartyBelongedTo(MobileParty party)
  	}
  
  	{
  		if (_partyBelongedTo != party && _partyBelongedTo != null)
  		{
  			if (_partyBelongedTo.LeaderHero == this)
  			{
  				_partyBelongedTo.PartyComponent.ChangePartyLeader(null);
  			}
  			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1970: 		if (_partyBelongedTo != party && _partyBelongedTo != null)
  	private void SetPartyBelongedTo(MobileParty party)
  	{
  		{
  			if (_partyBelongedTo.LeaderHero == this)
  			{
  				_partyBelongedTo.PartyComponent.ChangePartyLeader(null);
  			}
  			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
  		}
  		_partyBelongedTo = party;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1972: 			if (_partyBelongedTo.LeaderHero == this)
  		if (_partyBelongedTo != party && _partyBelongedTo != null)
  		{
  			{
  				_partyBelongedTo.PartyComponent.ChangePartyLeader(null);
  			}
  			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
  		}
  		_partyBelongedTo = party;
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1974: 				_partyBelongedTo.PartyComponent.ChangePartyLeader(null);
  			if (_partyBelongedTo.LeaderHero == this)
  			{
  			}
  			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
  		}
  		_partyBelongedTo = party;
  	}
  
  	public bool CanBeGovernorOrHavePartyRole()
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1976: 			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
  				_partyBelongedTo.PartyComponent.ChangePartyLeader(null);
  			}
  		}
  		_partyBelongedTo = party;
  	}
  
  	public bool CanBeGovernorOrHavePartyRole()
  	{
  		if (IsPrisoner)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1978: 		_partyBelongedTo = party;
  			_partyBelongedTo.RemoveAllPartyRolesOfHero(this);
  		}
  	}
  
  	public bool CanBeGovernorOrHavePartyRole()
  	{
  		if (IsPrisoner)
  		{
  			return false;
  		}

 succeeded in 555ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:72: 		collectedObjects.Add(data);
  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		collectedObjects.Add(OwnerParty);
  	}
  
  	internal static object AutoGeneratedGetMemberValueOwnerParty(object o)
  	{
  		return ((TroopRoster)o).OwnerParty;
  	}
  
  	internal static object AutoGeneratedGetMemberValuedata(object o)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:73: 		collectedObjects.Add(OwnerParty);
  	{
  		collectedObjects.Add(data);
  	}
  
  	internal static object AutoGeneratedGetMemberValueOwnerParty(object o)
  	{
  		return ((TroopRoster)o).OwnerParty;
  	}
  
  	internal static object AutoGeneratedGetMemberValuedata(object o)
  	{
  		return ((TroopRoster)o).data;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:120: 		InstanceListForLoadGame.Add(this);
  		_troopRosterElementsVersion = -1;
  		_troopRosterElements = new MBList<TroopRosterElement>();
  	}
  
  	private void EnsureLength(int length)
  	{
  		if (length > 0 && (data == null || length > data.Length))
  		{
  			int num = 4;
  			if (data != null)
  			{
  				num = data.Length * 2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:190: 	public void Add(TroopRoster troopRoster)
  	}
  
  	{
  		foreach (TroopRosterElement item in troopRoster.GetTroopRoster())
  		{
  			Add(item);
  		}
  	}
  
  	public void Add(TroopRosterElement troopRosterElement)
  	{
  		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:194: 			Add(item);
  		foreach (TroopRosterElement item in troopRoster.GetTroopRoster())
  		{
  		}
  	}
  
  	public void Add(TroopRosterElement troopRosterElement)
  	{
  		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
  	}
  
  	public ICollection<TroopRosterElement> RemoveIf(Predicate<TroopRosterElement> match)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:198: 	public void Add(TroopRosterElement troopRosterElement)
  	}
  
  	{
  		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
  	}
  
  	public ICollection<TroopRosterElement> RemoveIf(Predicate<TroopRosterElement> match)
  	{
  		List<TroopRosterElement> list = new List<TroopRosterElement>();
  		for (int num = _count - 1; num >= 0; num--)
  		{
  			if (match(data[num]))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:200: 		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
  	public void Add(TroopRosterElement troopRosterElement)
  	{
  	}
  
  	public ICollection<TroopRosterElement> RemoveIf(Predicate<TroopRosterElement> match)
  	{
  		List<TroopRosterElement> list = new List<TroopRosterElement>();
  		for (int num = _count - 1; num >= 0; num--)
  		{
  			if (match(data[num]))
  			{
  				TroopRosterElement elementCopyAtIndex = GetElementCopyAtIndex(num);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:211: 				list.Add(elementCopyAtIndex);
  			{
  				TroopRosterElement elementCopyAtIndex = GetElementCopyAtIndex(num);
  				AddToCountsAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);
  			}
  		}
  		return list;
  	}
  
  	public int FindIndexOfTroop(CharacterObject character)
  	{
  		for (int i = 0; i < _count; i++)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:260: 				troopRoster.AddToCounts(GetCharacterAtIndex(num), 1);
  			if (num >= 0)
  			{
  				AddToCountsAtIndex(num, -1);
  			}
  		}
  		return troopRoster;
  	}
  
  	public void WoundNumberOfNonHeroTroopsRandomly(int numberOfMen)
  	{
  		for (int i = 0; i < numberOfMen; i++)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:428: 	public int AddToCounts(CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)
  	}
  
  	{
  		if (count != 0 || woundedCount != 0)
  		{
  			UpdateVersion();
  		}
  		if (insertAtFront)
  		{
  			index = 0;
  		}
  		int num = FindIndexOfTroop(character);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:646: 					_troopRosterElements.Add(GetElementCopyAtIndex(i));
  				for (int i = 0; i < Count; i++)
  				{
  				}
  				_troopRosterElementsVersion = VersionNo;
  			}
  		}
  	}
  
  	public MBList<TroopRosterElement> GetTroopRoster()
  	{
  		ValidateTroopListCache();
  		return _troopRosterElements;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:747: 					troopRoster.AddToCounts(data[i].Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber);
  				if (troopRosterElement.Character != null)
  				{
  				}
  			}
  		}
  		return troopRoster;
  	}
  
  	public void AddXpToTroop(CharacterObject troop, int xpAmount)
  	{
  		int index = FindIndexOfTroop(troop);
  		AddXpToTroopAtIndex(index, xpAmount);

codex
The adapter’s village handling matches vanilla: `Settlement.OwnerClan` returns `Village.Bound.OwnerClan`, then town owner, then hideout faction. The adapter also wraps the computed getter in `try/catch`, so I don’t see a crash path there.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Settlement.cs'; "'$lines=Get-Content $p; for($i=66;$i -le 72;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=458;$i -le 478;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=487;$i -le 503;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content $p; for($i=428;$i -le 490;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs'; "'$lines=Get-Content $p; for($i=887;$i -le 895;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=627;$i -le 637;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 556ms:
   66: 	private MBList<Hero> _notablesCache;
   67: 
   68: 	private CampaignVec2 _position;
   69: 
   70: 	public CultureObject Culture;
   71: 
   72: 	private TextObject _name;
  458: 
  459: 	[SaveableProperty(138)]
  460: 	public LocationComplex LocationComplex { get; private set; }
  461: 
  462: 	public static Settlement CurrentSettlement
  463: 	{
  464: 		get
  465: 		{
  466: 			if (PlayerCaptivity.CaptorParty != null && PlayerCaptivity.CaptorParty.IsSettlement)
  467: 			{
  468: 				return PlayerCaptivity.CaptorParty.Settlement;
  469: 			}
  470: 			if (PlayerEncounter.EncounterSettlement != null)
  471: 			{
  472: 				return PlayerEncounter.EncounterSettlement;
  473: 			}
  474: 			if (MobileParty.MainParty.CurrentSettlement != null)
  475: 			{
  476: 				return MobileParty.MainParty.CurrentSettlement;
  477: 			}
  478: 			return null;
  487: 	public SiegeState CurrentSiegeState { get; private set; }
  488: 
  489: 	public Clan OwnerClan
  490: 	{
  491: 		get
  492: 		{
  493: 			if (Village != null)
  494: 			{
  495: 				return Village.Bound.OwnerClan;
  496: 			}
  497: 			if (Town != null)
  498: 			{
  499: 				return Town.OwnerClan;
  500: 			}
  501: 			if (IsHideout)
  502: 			{
  503: 				return Hideout.MapFaction as Clan;

 succeeded in 523ms:
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
  476: 					data[num] = data[i];
  477: 				}
  478: 				num++;
  479: 			}
  480: 		}
  481: 		for (int j = num; j < _count; j++)
  482: 		{
  483: 			data[j].Clear();
  484: 		}
  485: 		_count = num;
  486: 		UpdateVersion();
  487: 	}
  488: 
  489: 	public TroopRosterElement GetElementCopyAtIndex(int index)
  490: 	{

 succeeded in 507ms:
  887: 	public static MBReadOnlyList<Hero> AllAliveHeroes => Campaign.Current.AliveHeroes;
  888: 
  889: 	public static MBReadOnlyList<Hero> DeadOrDisabledHeroes => Campaign.Current.DeadOrDisabledHeroes;
  890: 
  891: 	public static Hero MainHero => CharacterObject.PlayerCharacter.HeroObject;
  892: 
  893: 	public static Hero OneToOneConversationHero => Campaign.Current.ConversationManager.OneToOneConversationHero;
  894: 
  895: 	public static bool IsMainHeroIll => Campaign.Current.MainHeroIllDays != -1;
  627: 	}
  628: 
  629: 	public List<CaravanPartyComponent> OwnedCaravans { get; private set; }
  630: 
  631: 	public MobileParty PartyBelongedTo
  632: 	{
  633: 		get
  634: 		{
  635: 			return _partyBelongedTo;
  636: 		}
  637: 		private set

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern \"AddToCountsAtIndex\" -Context 2,35 | Select-Object -First 3 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 539ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:212: 				AddToCountsAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);
  				TroopRosterElement elementCopyAtIndex = GetElementCopyAtIndex(num);
  				list.Add(elementCopyAtIndex);
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
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:261: 				AddToCountsAtIndex(num, -1);
  			{
  				troopRoster.AddToCounts(GetCharacterAtIndex(num), 1);
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
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:275: 				AddToCountsAtIndex(num, 0, 1);
  			if (num >= 0)
  			{
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

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs -Pattern \"private.*AddToCountsAtIndex|public.*AddToCountsAtIndex|void AddToCountsAtIndex\" | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 518ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:312: public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content $p; for($i=312;$i -le 380;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 525ms:
  312: 	public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
  313: 	{
  314: 		bool flag = false;
  315: 		CharacterObject character = data[index].Character;
  316: 		bool isHero = character.IsHero;
  317: 		data[index].Number += countChange;
  318: 		int num = data[index].WoundedNumber + woundedCountChange;
  319: 		if (num > data[index].Number)
  320: 		{
  321: 			woundedCountChange += data[index].Number - num;
  322: 		}
  323: 		else if (num < 0)
  324: 		{
  325: 			TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCountsAtIndex", 415);
  326: 		}
  327: 		data[index].WoundedNumber += woundedCountChange;
  328: 		if (xpChange != 0)
  329: 		{
  330: 			SetElementXp(index, data[index].Xp + xpChange);
  331: 		}
  332: 		if (isHero)
  333: 		{
  334: 			_totalHeroes += countChange;
  335: 			if (character.HeroObject.IsWounded)
  336: 			{
  337: 				_totalWoundedHeroes += countChange;
  338: 			}
  339: 			if (countChange != 0)
  340: 			{
  341: 				flag = true;
  342: 			}
  343: 		}
  344: 		else
  345: 		{
  346: 			_totalWoundedRegulars += woundedCountChange;
  347: 			_totalRegulars += countChange;
  348: 		}
  349: 		if (removeDepleted && data[index].Number == 0)
  350: 		{
  351: 			RemoveRange(index, index + 1);
  352: 			index = -1;
  353: 		}
  354: 		if (OwnerParty != null && isHero)
  355: 		{
  356: 			if (countChange > 0)
  357: 			{
  358: 				OwnerParty.OnHeroAdded(character.HeroObject, this);
  359: 			}
  360: 			else if (countChange < 0)
  361: 			{
  362: 				OwnerParty.OnHeroRemoved(character.HeroObject, this);
  363: 			}
  364: 		}
  365: 		if (countChange != 0 || flag)
  366: 		{
  367: 			OwnerParty?.OnRosterSizeChanged(this);
  368: 		}
  369: 		if (countChange != 0 || woundedCountChange != 0)
  370: 		{
  371: 			UpdateVersion();
  372: 		}
  373: 		return index;
  374: 	}
  375: 
  376: 	private void RemoveRange(int p, int p2)
  377: 	{
  378: 		int num = p2 - p;
  379: 		for (int i = p2; i < _count; i++)
  380: 		{

codex
`TroopRoster.AddToCounts` has no party-size limit check; it updates roster counts and fires roster-size events. That supports the “over party cap is allowed” decision, with the usual caveat that the UI path only offers small quantities bounded by the player’s resource balance.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'AddGameMenu\\(\"town\"|AddGameMenu\\(\"castle\"|AddGameMenu\\(\"village\"|town_wait|village_wait' | Select-Object -First 100 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'StartSettlementEncounter|EncounterSettlement|CurrentSettlement|AtMenu' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter EncounterGameMenuModel.cs -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 486ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\EncounterGameMenuModel.cs

 succeeded in 603ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:17: if (mobileParty.CurrentSettlement != null && mobileParty.CurrentSettlement == settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:49: isFromPort = mobileParty.CurrentSettlement != null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:58: if (mobileParty.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:60: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty.CurrentSettlement, settlement, isFromPort: false, isTargetingPort, MobileParty.NavigationType.All, out landRatio);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:68: if (mobileParty.CurrentSettlement.HasPort)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:70: float distance2 = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty.CurrentSettlement, settlement, isFromPort: true, isTargetingPort, MobileParty.NavigationType.All, out landRatio);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:242: bool flag = Settlement.CurrentSettlement != null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:470: if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.Town != null && Settlement.CurrentSettlement.Town.Prosperity < (float)(200 * ((!Settlement.CurrentSettlement.IsTown) ? 1 : 5)) && deterministicHashCode % 2 == 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CraftingHelper.cs:30: Settlement currentSettlement = Settlement.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CraftingHelper.cs:33: Crafting crafting = new Crafting(craftingTemplate, (currentSettlement != null) ? currentSettlement.Culture : new CultureObject(), textObject);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:57: Settlement currentSettlement = mobileParty.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:60: bool flag2 = num && currentSettlement.HasPort && currentSettlement != toSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:61: bool flag3 = num && toSettlement.HasPort && currentSettlement != toSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:65: num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: false, navCapabilities, out landRatio);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:71: bool flag4 = flag2 && mobileParty.Anchor.IsAtSettlement(currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:77: float num3 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:93: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: true, navCapabilities, out landRatio3);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:105: float num4 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: true, navCapabilities, out landRatio4);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:141: if (fromMobileParty.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:206: Settlement currentSettlement = from.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:207: Settlement currentSettlement2 = to.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:208: if (currentSettlement2 != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:210: return FindClosestDistanceFromMobilePartyToSettlement(from, currentSettlement2, navigationType, out landRatio);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:212: if (currentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:274: Settlement currentSettlement = mobileParty.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:277: bool flag = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval && currentSettlement.HasPort;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:281: num = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, in point, isFromPort: false, navCapabilities);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:283: bool flag2 = flag && mobileParty.Anchor.IsAtSettlement(currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:286: float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, in point, isFromPort: true, navCapabilities);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs:311: if (fromMobileParty.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:307: if (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.TargetSettlement == noble.PartyBelongedTo.MapEvent.MapEventSettlement && mobileParty.CurrentSettlement == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:332: if (mobileParty2.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty2.TargetSettlement == partyBelongedTo.BesiegedSettlement && mobileParty2.CurrentSettlement == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:726: if (hero.CurrentSettlement != null && (hero.CurrentSettlement.IsUnderSiege || hero.CurrentSettlement.IsUnderRaid))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:783: if (hero.CurrentSettlement != null && (hero.CurrentSettlement.IsUnderSiege || hero.CurrentSettlement.IsUnderRaid))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:826: if (hero.CurrentSettlement != null && (hero.CurrentSettlement.IsUnderSiege || hero.CurrentSettlement.IsUnderRaid))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:31: textObject.SetTextVariable("IS_IN_SETTLEMENT", (hero.LastKnownClosestSettlement == hero.CurrentSettlement) ? 1 : 0);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:39: if (hero.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:41: settlement = hero.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:80: if (PlayerEncounter.Current.EncounterSettlementAux != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:82: return PlayerEncounter.Current.EncounterSettlementAux;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:203: Settlement settlement = MobileParty.MainParty.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:328: public static void ActivateTradeWithCurrentSettlement()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:330: OpenScreenAsTrade(Settlement.CurrentSettlement.ItemRoster, Settlement.CurrentSettlement.SettlementComponent);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:62: if (MobileParty.MainParty.CurrentSettlement.Town.GarrisonParty != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:64: return MobileParty.MainParty.CurrentSettlement.Town.GarrisonParty.MapEvent.DefenderSide.LeaderParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:66: if (MobileParty.MainParty.CurrentSettlement.Party?.MapEvent != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:68: return MobileParty.MainParty.CurrentSettlement.Party.MapEvent.DefenderSide.LeaderParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:70: return MobileParty.MainParty.CurrentSettlement.SiegeEvent.BesiegerCamp.LeaderParty.Party;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:79: return MobileParty.MainParty.CurrentSettlement == null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:131: if (Settlement.CurrentSettlement.IsCastle)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:135: else if (Settlement.CurrentSettlement.IsTown)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:139: else if (Settlement.CurrentSettlement.IsVillage)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:590: Settlement currentSettlement = MobileParty.MainParty.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:593: PlayerEncounter.Finish(currentSettlement == null && (MobileParty.MainParty.CurrentSettlement?.SiegeEvent == null || MobileParty.MainParty.CurrentSettlement?.MapFaction != MobileParty.MainParty.MapFaction));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:603: if (currentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:605: EncounterManager.StartSettlementEncounter(MobileParty.MainParty, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:34: if (hero.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:36: Settlement currentSettlement = hero.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:41: return SpawnLordParty(hero, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:266: public static Settlement GetCurrentSettlementOfMobilePartyForAICalculation(MobileParty mobileParty)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:268: Settlement settlement = mobileParty.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:291: public static void OpenScreenAsDonateGarrisonWithCurrentSettlement()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:297: if (Hero.MainHero.CurrentSettlement.Town.GarrisonParty == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:299: Hero.MainHero.CurrentSettlement.AddGarrisonParty();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:301: MobileParty garrisonParty = Hero.MainHero.CurrentSettlement.Town.GarrisonParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:315: if (Hero.MainHero.CurrentSettlement.Town.GarrisonParty == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:317: Hero.MainHero.CurrentSettlement.AddGarrisonParty();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:319: TroopRoster prisonRoster = Hero.MainHero.CurrentSettlement.Party.PrisonRoster;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:320: int num = Math.Max(Hero.MainHero.CurrentSettlement.Party.PrisonerSizeLimit - prisonRoster.Count, 0);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:322: textObject.SetTextVariable("SETTLEMENT_NAME", Hero.MainHero.CurrentSettlement.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:369: TroopRoster prisonRoster = Hero.MainHero.CurrentSettlement.Party.PrisonRoster;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:371: textObject.SetTextVariable("SETTLEMENT_NAME", Hero.MainHero.CurrentSettlement.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:372: PartyScreenLogicInitializationData initializationData = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(TroopRoster.CreateDummyTroopRoster(), prisonRoster, PartyScreenLogic.TransferState.NotTransferable, PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable, TroopTransferableDelegate, partyState.PartyScreenMode, null, textObject, leftPartyPrisonersSizeLimit: Hero.MainHero.CurrentSettlement.Party.PrisonerSizeLimit, partyPresentationDoneButtonDelegate: ManageGarrisonDoneHandler, header: new TextObject("{=aadTnAEg}Manage Prisoners"));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:381: bool flag = (hero != null && hero.Clan == Clan.PlayerClan) || (leftOwnerParty != null && leftOwnerParty.IsMobile && leftOwnerParty.MobileParty.IsCaravan && leftOwnerParty.Owner == Hero.MainHero) || (leftOwnerParty != null && leftOwnerParty.IsMobile && leftOwnerParty.MobileParty.IsGarrison && leftOwnerParty.MobileParty.CurrentSettlement?.OwnerClan == Clan.PlayerClan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:622: Settlement currentSettlement = Hero.MainHero.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:623: MobileParty garrisonParty = currentSettlement.Town.GarrisonParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:626: currentSettlement.AddGarrisonParty();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:627: garrisonParty = currentSettlement.Town.GarrisonParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:635: EnterSettlementAction.ApplyForCharacterOnly(elementCopyAtIndex.Character.HeroObject, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:645: Settlement currentSettlement = Hero.MainHero.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:650: EnterSettlementAction.ApplyForPrisoner(troop.HeroObject, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:653: CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(rightParty.MobileParty, rightSideTransferredPrisonerRoster, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:660: Settlement currentSettlement = Hero.MainHero.CurrentSettlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:666: EnterSettlementAction.ApplyForCharacterOnly(elementCopyAtIndex.Character.HeroObject, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:674: EnterSettlementAction.ApplyForPrisoner(elementCopyAtIndex2.Character.HeroObject, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs:189: if (governor != null && governor.GetPerkValue(perk) && governor.CurrentSettlement != null && governor.CurrentSettlement == town.Settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs:215: if (governor != null && governor.GetPerkValue(perk) && governor.CurrentSettlement != null && governor.CurrentSettlement == town.Settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:247: Town town = ((mobileParty.CurrentSettlement != null) ? FindNearestTownToSettlement(mobileParty.CurrentSettlement, navCapabilities, condition) : FindNearestTownToMobileParty(mobileParty, navCapabilities, condition));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:248: Settlement settlement = ((mobileParty.CurrentSettlement != null) ? FindNearestCastleToSettlement(mobileParty.CurrentSettlement, navCapabilities, condition) : FindNearestCastleToMobileParty(mobileParty, navCapabilities, condition));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:400: if ((item.IsVillager || item.IsCaravan) && item.TargetSettlement == settlementWhichChangedFaction && item.CurrentSettlement != settlementWhichChangedFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:412: if (allVillagerParty.HomeSettlement == settlementWhichChangedFaction && allVillagerParty.CurrentSettlement != settlementWhichChangedFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:414: if (allVillagerParty.CurrentSettlement != null && allVillagerParty.MapEvent == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:313: float num8 = ((LeaderParty.CurrentSettlement == null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty, LeaderParty.TargetSettlement, LeaderParty.IsTargetingPort, LeaderParty.NavigationCapability, out estimatedLandRatio) : Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty.CurrentSettlement, LeaderParty.TargetSettlement, LeaderParty.IsCurrentlyAtSea, LeaderParty.IsTargetingPort, LeaderParty.NavigationCapability));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:339: bool flag = LeaderParty.CurrentSettlement != null && LeaderParty.CurrentSettlement.SiegeEvent != null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:373: if (AiBehaviorObject is Settlement toSettlement && LeaderParty.DefaultBehavior == AiBehavior.GoToPoint && ((LeaderParty.CurrentSettlement != null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty.CurrentSettlement, toSettlement, isFromPort: false, isTargetingPort: false, LeaderParty.DesiredAiNavigationType) : Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty, toSettlement, isTargetingPort: false, LeaderParty.DesiredAiNavigationType, out var _)) < GatheringPositionMaxDistanceToTheSettlement * 2f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:536: if (MobileParty.MainParty.TargetSettlement != null && MobileParty.MainParty.CurrentSettlement != MobileParty.MainParty.TargetSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:541: else if (MobileParty.MainParty.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:544: textObject.SetTextVariable("SETTLEMENT_NAME", MobileParty.MainParty.CurrentSettlement.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:573: TextObject textObject = ((LeaderParty.CurrentSettlement == null) ? GameTexts.FindText("str_army_going_to_settlement") : GameTexts.FindText("str_army_waiting_in_settlement"));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:666: num2 = ((LeaderParty.CurrentSettlement != null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty.CurrentSettlement, settlement2, isFromPort: false, isTargetingPort: false, LeaderParty.NavigationCapability, out var landRatio) : Campaign.Current.Models.MapDistanceModel.GetDistance(LeaderParty, settlement2, isTargetingPort: false, LeaderParty.NavigationCapability, out landRatio));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:781: if (num3 && mobileParty.CurrentSettlement == null && mobileParty.IsActive && (!LeaderParty.IsCurrentlyAtSea || mobileParty.HasNavalNavigationCapability))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:496: obj42.GetFieldDefinitionWithId(new MemberTypeId(4, 1001)).InitializeForAutoGeneration(MobileParty.AutoGeneratedGetMemberValue_currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:584: obj47.GetPropertyDefinitionWithId(new MemberTypeId(2, 28)).InitializeForAutoGeneration(PlayerEncounter.AutoGeneratedGetMemberValueEncounterSettlementAux);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:975: if (Game.Current.GameStateManager.ActiveState is MapState { AtMenu: not false } mapState && !mapState.MenuContext.GameMenu.IsWaitActive)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:992: if (Game.Current.GameStateManager.ActiveState is MapState { AtMenu: false })
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:1753: if (Hero.MainHero.CurrentSettlement != null && !Hero.MainHero.IsPrisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2699: if (obj.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2829: foreach (MobileParty item in MobileParty.All.Where((MobileParty x) => x.IsMilitia && x.CurrentSettlement == settlement))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1665: public override void OnItemSold(PartyBase receiverParty, PartyBase payerParty, ItemRosterElement itemRosterElement, int number, Settlement currentSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1670: eventReceivers[i].OnItemSold(receiverParty, payerParty, itemRosterElement, number, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:757: public virtual void OnItemSold(PartyBase receiverParty, PartyBase payerParty, ItemRosterElement itemRosterElement, int number, Settlement currentSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2341: public override void OnItemSold(PartyBase receiverParty, PartyBase payerParty, ItemRosterElement itemRosterElement, int number, Settlement currentSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2343: Instance._onItemSoldEvent.Invoke(receiverParty, payerParty, itemRosterElement, number, currentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:38: if (!mobileParty.IsActive || mobileParty.AttachedTo != null || mobileParty.MapEventSide != null || (mobileParty.CurrentSettlement != null && !mobileParty.IsGarrison) || (mobileParty.BesiegedSettlement != null && mobileParty.ShortTermBehavior != AiBehavior.AssaultSettlement) || (!mobileParty.IsCurrentlyEngagingParty && !mobileParty.IsCurrentlyEngagingSettlement && (mobileParty.Ai.AiBehaviorInteractable == null || mobileParty.ShortTermBehavior != AiBehavior.GoToPoint || mobileParty.Ai.AiBehaviorInteractable is PartyBase { IsSettlement: not false } || mobileParty.Ai.AiBehaviorInteractable is PartyBase { IsMobile: not false } || (mobileParty.Party == PartyBase.MainParty && PlayerEncounter.Current != null))))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:50: if ((!mobileParty.IsCurrentlyEngagingSettlement || mobileParty.ShortTermTargetSettlement == null || mobileParty.ShortTermTargetSettlement != mobileParty.CurrentSettlement) && (!mobileParty.IsCurrentlyEngagingParty || (mobileParty.ShortTermTargetParty.IsActive && (mobileParty.ShortTermTargetParty.CurrentSettlement == null || (mobileParty.ShortTermTargetParty.MapEvent != null && (mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Attacker).MapFaction == mobileParty.MapFaction || mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Defender).MapFaction == mobileParty.MapFaction))))) && mobileParty.Ai.AiBehaviorInteractable.CanPartyInteract(mobileParty, dt))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:70: else if (((attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty) && !PlayerEncounter.IsActive) || (PlayerEncounter.EncounterSettlement != null && Settlement.CurrentSettlement != null && PlayerEncounter.EncounterSettlement == Settlement.CurrentSettlement))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:94: if (attackerParty != PartyBase.MainParty && attackerParty.MapEvent != null && attackerParty.MapEvent.IsSallyOut && attackerParty.MapEvent.MapEventSettlement == MobileParty.MainParty.CurrentSettlement && MobileParty.MainParty.Army == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:100: public static void StartSettlementEncounter(MobileParty attackerParty, Settlement settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:211: bool flag = MobileParty.MainParty.CurrentSettlement == settlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:252: if (attackerParty.MapEvent.DefenderSide.TroopCount == 0 && (PlayerSiege.PlayerSiegeEvent == null || PlayerSiege.PlayerSide != BattleSideEnum.Defender || MobileParty.MainParty.CurrentSettlement != settlement))

 succeeded in 2881ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:196: else if (Campaign.Current.CurrentMenuContext != null && (Campaign.Current.CurrentMenuContext.GameMenu.StringId == "town_wait_menus" || Campaign.Current.CurrentMenuContext.GameMenu.StringId == "village_wait_menus") && num < Campaign.Current.Models.IncidentModel.GetIncidentTriggerProbabilityDuringWait())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:40: campaignGameSystemStarter.AddGameMenu("town", "{=!}{SETTLEMENT_INFO}", game_menu_town_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:59: campaignGameSystemStarter.AddGameMenuOption("town", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:61: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:119: campaignGameSystemStarter.AddWaitGameMenu("town_wait_menus", "{=ydbVysqv}You are waiting in {CURRENT_SETTLEMENT}.", game_menu_settlement_wait_on_init, game_menu_town_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:123: campaignGameSystemStarter.AddGameMenuOption("town_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, delegate(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:128: campaignGameSystemStarter.AddGameMenu("castle", "{=!}{SETTLEMENT_INFO}", game_menu_castle_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:138: campaignGameSystemStarter.AddGameMenuOption("castle", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:140: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:153: campaignGameSystemStarter.AddGameMenu("village", "{=!}{SETTLEMENT_INFO}", game_menu_village_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:157: campaignGameSystemStarter.AddGameMenuOption("village", "village_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, game_menu_wait_village_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:165: campaignGameSystemStarter.AddWaitGameMenu("village_wait_menus", "{=lsBuV9W7}You are waiting in the village.", game_menu_settlement_wait_on_init, game_menu_village_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:169: campaignGameSystemStarter.AddGameMenuOption("village_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, game_menu_stop_waiting_at_village_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:354: GameMenu.SwitchToMenu("village_wait_menus");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:1181: private static bool game_menu_village_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:1188: private static bool game_menu_town_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:1421: [GameMenuInitializationHandler("town_wait_menus")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:1422: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs:1438: [GameMenuInitializationHandler("village_wait_menus")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEncounterGameMenuModel.cs:297: return "town_wait_menus";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEncounterGameMenuModel.cs:325: return "village_wait_menus";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers\DefaultEncounter.cs:386: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers\PlayerTownVisit.cs:16: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\RivalGangMovingInIssueBehavior.cs:996: Campaign.Current.GameMenuManager.SetNextMenu("town_wait_menus");
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\RivalGangMovingInIssueBehavior.cs:1377: if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement == _questSettlement && Campaign.Current.CurrentMenuContext != null && Campaign.Current.CurrentMenuContext.GameMenu.StringId == "town_wait_menus")
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\RivalGangMovingInIssueBehavior.cs:1709: Campaign.Current.CurrentMenuContext.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents.CampaignBehaviors\TutorialPhaseCampaignBehavior.cs:95: campaignGameStarter.AddGameMenuOption("storymode_tutorial_village_game_menu", "storymode_tutorial_village_wait", "{=zEoHYEUS}Wait here for some time", new OnConditionDelegate(wait_village_menu_option_condition), (OnConsequenceDelegate)null, false, -1, false, (object)null);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:52791: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:52849: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:60043: return "town_wait_menus";
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:60071: return "village_wait_menus";
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:192219: else if (Campaign.Current.CurrentMenuContext != null && (Campaign.Current.CurrentMenuContext.GameMenu.StringId == "town_wait_menus" || Campaign.Current.CurrentMenuContext.GameMenu.StringId == "village_wait_menus") && num < Campaign.Current.Models.IncidentModel.GetIncidentTriggerProbabilityDuringWait())
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207726: campaignGameSystemStarter.AddGameMenu("town", "{=!}{SETTLEMENT_INFO}", game_menu_town_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207745: campaignGameSystemStarter.AddGameMenuOption("town", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207747: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207805: campaignGameSystemStarter.AddWaitGameMenu("town_wait_menus", "{=ydbVysqv}You are waiting in {CURRENT_SETTLEMENT}.", game_menu_settlement_wait_on_init, game_menu_town_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207809: campaignGameSystemStarter.AddGameMenuOption("town_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, delegate(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207814: campaignGameSystemStarter.AddGameMenu("castle", "{=!}{SETTLEMENT_INFO}", game_menu_castle_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207824: campaignGameSystemStarter.AddGameMenuOption("castle", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207826: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207839: campaignGameSystemStarter.AddGameMenu("village", "{=!}{SETTLEMENT_INFO}", game_menu_village_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207843: campaignGameSystemStarter.AddGameMenuOption("village", "village_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, game_menu_wait_village_on_consequence);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207851: campaignGameSystemStarter.AddWaitGameMenu("village_wait_menus", "{=lsBuV9W7}You are waiting in the village.", game_menu_settlement_wait_on_init, game_menu_village_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:207855: campaignGameSystemStarter.AddGameMenuOption("village_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, game_menu_stop_waiting_at_village_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:208040: GameMenu.SwitchToMenu("village_wait_menus");
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:208881: private static bool game_menu_village_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:208888: private static bool game_menu_town_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:209121: [GameMenuInitializationHandler("town_wait_menus")]
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:209122: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:209138: [GameMenuInitializationHandler("village_wait_menus")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:52224: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:52282: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:59420: return "town_wait_menus";
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:59448: return "village_wait_menus";
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:190302: else if (Campaign.Current.CurrentMenuContext != null && (Campaign.Current.CurrentMenuContext.GameMenu.StringId == "town_wait_menus" || Campaign.Current.CurrentMenuContext.GameMenu.StringId == "village_wait_menus") && num < Campaign.Current.Models.IncidentModel.GetIncidentTriggerProbabilityDuringWait())
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205720: campaignGameSystemStarter.AddGameMenu("town", "{=!}{SETTLEMENT_INFO}", game_menu_town_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205739: campaignGameSystemStarter.AddGameMenuOption("town", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205741: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205799: campaignGameSystemStarter.AddWaitGameMenu("town_wait_menus", "{=ydbVysqv}You are waiting in {CURRENT_SETTLEMENT}.", game_menu_settlement_wait_on_init, game_menu_town_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205803: campaignGameSystemStarter.AddGameMenuOption("town_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, delegate(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205808: campaignGameSystemStarter.AddGameMenu("castle", "{=!}{SETTLEMENT_INFO}", game_menu_castle_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205818: campaignGameSystemStarter.AddGameMenuOption("castle", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205820: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205833: campaignGameSystemStarter.AddGameMenu("village", "{=!}{SETTLEMENT_INFO}", game_menu_village_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205837: campaignGameSystemStarter.AddGameMenuOption("village", "village_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, game_menu_wait_village_on_consequence);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205845: campaignGameSystemStarter.AddWaitGameMenu("village_wait_menus", "{=lsBuV9W7}You are waiting in the village.", game_menu_settlement_wait_on_init, game_menu_village_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:205849: campaignGameSystemStarter.AddGameMenuOption("village_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, game_menu_stop_waiting_at_village_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:206034: GameMenu.SwitchToMenu("village_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:206861: private static bool game_menu_village_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:206868: private static bool game_menu_town_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:207101: [GameMenuInitializationHandler("town_wait_menus")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:207102: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:207118: [GameMenuInitializationHandler("village_wait_menus")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:52181: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:52239: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:59377: return "town_wait_menus";
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:59405: return "village_wait_menus";
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:190287: else if (Campaign.Current.CurrentMenuContext != null && (Campaign.Current.CurrentMenuContext.GameMenu.StringId == "town_wait_menus" || Campaign.Current.CurrentMenuContext.GameMenu.StringId == "village_wait_menus") && num < Campaign.Current.Models.IncidentModel.GetIncidentTriggerProbabilityDuringWait())
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205705: campaignGameSystemStarter.AddGameMenu("town", "{=!}{SETTLEMENT_INFO}", game_menu_town_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205724: campaignGameSystemStarter.AddGameMenuOption("town", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205726: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205784: campaignGameSystemStarter.AddWaitGameMenu("town_wait_menus", "{=ydbVysqv}You are waiting in {CURRENT_SETTLEMENT}.", game_menu_settlement_wait_on_init, game_menu_town_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205788: campaignGameSystemStarter.AddGameMenuOption("town_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, delegate(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205793: campaignGameSystemStarter.AddGameMenu("castle", "{=!}{SETTLEMENT_INFO}", game_menu_castle_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205803: campaignGameSystemStarter.AddGameMenuOption("castle", "town_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, delegate
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205805: GameMenu.SwitchToMenu("town_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205818: campaignGameSystemStarter.AddGameMenu("village", "{=!}{SETTLEMENT_INFO}", game_menu_village_on_init, GameMenu.MenuOverlayType.SettlementWithBoth);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205822: campaignGameSystemStarter.AddGameMenuOption("village", "village_wait", "{=zEoHYEUS}Wait here for some time", game_menu_wait_here_on_condition, game_menu_wait_village_on_consequence);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205830: campaignGameSystemStarter.AddWaitGameMenu("village_wait_menus", "{=lsBuV9W7}You are waiting in the village.", game_menu_settlement_wait_on_init, game_menu_village_wait_on_condition, null, delegate(MenuCallbackArgs args, CampaignTime dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:205834: campaignGameSystemStarter.AddGameMenuOption("village_wait_menus", "wait_leave", "{=UqDNAZqM}Stop waiting", back_on_condition, game_menu_stop_waiting_at_village_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:206019: GameMenu.SwitchToMenu("village_wait_menus");
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:206846: private static bool game_menu_village_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:206853: private static bool game_menu_town_wait_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:207086: [GameMenuInitializationHandler("town_wait_menus")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:207087: [GameMenuInitializationHandler("town_wait")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:207103: [GameMenuInitializationHandler("village_wait_menus")]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultEncounterGameMenuModel.cs'; "'$lines=Get-Content $p; for($i=250;$i -le 335;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 509ms:
  250: 		if (mainParty.CurrentSettlement != null)
  251: 		{
  252: 			Settlement currentSettlement = mainParty.CurrentSettlement;
  253: 			if (currentSettlement.IsFortification)
  254: 			{
  255: 				if (currentSettlement.Party.SiegeEvent != null && ((currentSettlement.Party.MapEvent == null && (currentSettlement.Town.GarrisonParty == null || currentSettlement.Town.GarrisonParty.MapEvent == null)) || MobileParty.MainParty.MapFaction == currentSettlement.MapFaction))
  256: 				{
  257: 					if (currentSettlement.Party.SiegeEvent.BesiegerCamp.LeaderParty == MobileParty.MainParty)
  258: 					{
  259: 						return "continue_siege_after_attack";
  260: 					}
  261: 					if (MobileParty.MainParty.BesiegedSettlement == null && MobileParty.MainParty.CurrentSettlement == null)
  262: 					{
  263: 						if (MobileParty.MainParty.IsCurrentlyAtSea)
  264: 						{
  265: 							if (!currentSettlement.SiegeEvent.IsBlockadeActive || currentSettlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEvent == null)
  266: 							{
  267: 								return "naval_town_outside";
  268: 							}
  269: 							return "join_encounter";
  270: 						}
  271: 						return "join_siege_event";
  272: 					}
  273: 					if (mainParty.CurrentSettlement.Party.MapEvent != null && mainParty.CurrentSettlement.Party.MapEvent.InvolvedParties.Contains(PartyBase.MainParty))
  274: 					{
  275: 						return "encounter";
  276: 					}
  277: 					if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  278: 					{
  279: 						return "encounter_interrupted_siege_preparations";
  280: 					}
  281: 					return "menu_siege_strategies";
  282: 				}
  283: 				if (currentSettlement.Party.MapEvent != null)
  284: 				{
  285: 					if (MobileParty.MainParty.MapFaction == currentSettlement.MapFaction)
  286: 					{
  287: 						return "encounter";
  288: 					}
  289: 					return "join_encounter";
  290: 				}
  291: 				if (currentSettlement.MapFaction == MobileParty.MainParty.MapFaction && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty)
  292: 				{
  293: 					return "army_wait_at_settlement";
  294: 				}
  295: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting && currentSettlement.IsFortification)
  296: 				{
  297: 					return "town_wait_menus";
  298: 				}
  299: 				if (currentSettlement.IsCastle)
  300: 				{
  301: 					return "castle_outside";
  302: 				}
  303: 				if (MobileParty.MainParty.IsCurrentlyAtSea)
  304: 				{
  305: 					return "naval_town_outside";
  306: 				}
  307: 				return "town_outside";
  308: 			}
  309: 			if (currentSettlement.IsHideout)
  310: 			{
  311: 				return "hideout_place";
  312: 			}
  313: 		}
  314: 		else if (Settlement.CurrentSettlement != null)
  315: 		{
  316: 			Settlement currentSettlement2 = Settlement.CurrentSettlement;
  317: 			if (currentSettlement2.IsVillage)
  318: 			{
  319: 				if (currentSettlement2.IsUnderRaid)
  320: 				{
  321: 					return "encounter_interrupted_raid_started";
  322: 				}
  323: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  324: 				{
  325: 					return "village_wait_menus";
  326: 				}
  327: 			}
  328: 		}
  329: 		return null;
  330: 	}
  331: 
  332: 	public override bool IsPlunderMenu(string gameMenuId)
  333: 	{
  334: 		return gameMenuId == "raiding_village";
  335: 	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'town_outside|castle_outside|village_hostile_action|village|enter' | Where-Object { "'$_.Path -like '"'*CampaignBehaviors*' -or "'$_.Path -like '"'*GameMenus*' -or "'$_.Path -like '"'*SandBox*' } | Select-Object -First 150 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 683ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:78: gameStarter.AddBehavior(new VillageHostileActionCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:81: gameStarter.AddBehavior(new VillageTradeBoundCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:82: gameStarter.AddBehavior(new VillageGoodProductionCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:85: gameStarter.AddBehavior(new VillagerCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:86: gameStarter.AddBehavior(new VillageHealCampaignBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:104: gameStarter.AddBehavior(new CommentOnChangeVillageStateBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:174: gameStarter.AddBehavior(new HeadmanVillageNeedsDraughtAnimalsIssueBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:178: gameStarter.AddBehavior(new LandlordNeedsAccessToVillageCommonsIssueBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:183: gameStarter.AddBehavior(new VillageNeedsCraftingMaterialsIssueBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:193: gameStarter.AddBehavior(new VillageNeedsToolsIssueBehavior());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:241: gameStarter.AddModel(new DefaultVillageProductionCalculatorModel());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SandBoxManager.cs:328: gameStarter.AddModel(new DefaultVillageTradeModel());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs:580: Settlement settlement = SettlementHelper.FindNearestSettlementToMobileParty(encounteredParty.MobileParty, MobileParty.NavigationType.All, (Settlement x) => x.IsTown || x.IsVillage);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs:581: SettlementComponent settlementComponent = ((!settlement.IsTown) ? ((SettlementComponent)settlement.Village) : ((SettlementComponent)settlement.Town));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:47: CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:149: public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:329: EnterSettlementAction.ApplyForParty(mobileParty, hideoutComponent.Owner.Settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:513: if (item.IsTown || item.IsVillage)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignWarManagerBehavior.cs:28: if (raidEvent.MapEventSettlement != null && raidEvent.BattleState == BattleState.AttackerVictory && raidEvent.MapEventSettlement.IsVillage && raidEvent.MapEventSettlement.Village.VillageState == Village.VillageStates.Looted)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:281: CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:501: MobileParty caravanParty = CaravanPartyComponent.CreateCaravanParty(spawnSettlement: (settlement == null) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.IsTown ? settlement : ((!settlement.IsVillage) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.Village.TradeBound ?? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement))), caravanOwner: hero, templateObject: randomElementWithPredicate, isInitialSpawn: initialSpawn, caravanLeader: null, caravanItems: null, isElite: flag);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:685: public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:847: Town town = (settlement.IsTown ? settlement.Town : (settlement.IsVillage ? settlement.Village.Bound.Town : null));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:327: NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("empire_hunter_option", new TextObject("{=7eWmU2mF}Foresters"), new TextObject("{=yRFSzSDZ}Your family lived in a village, but did not own their own land. Instead, your father supplemented paid jobs with long trips in the woods, hunting and trapping, always keeping a wary eye for the lord's game wardens."), GetEmpireHunterNarrativeOptionArgs, EmpireHunterNarrativeOptionOnCondition, EmpireHunterNarrativeOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:532: NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("vlandia_retainer_option", new TextObject("{=2TptWc4m}A baron's retainers"), new TextObject("{=0Suu1Q9q}Your father was a bailiff for a local feudal magnate. He looked after his liege's estates, resolved disputes in the village, and helped train the village levy. He rode with the lord's cavalry, fighting as an armored knight."), GetVlandiaRetainerNarrativeOptionArgs, VlandiaRetainerNarrativeOptionOnCondition, VlandiaRetainerNarrativeOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:540: NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("vlandia_hunter_option", new TextObject("{=YcnK0Thk}Hunters"), new TextObject("{=yRFSzSDZ}Your family lived in a village, but did not own their own land. Instead, your father supplemented paid jobs with long trips in the woods, hunting and trapping, always keeping a wary eye for the lord's game wardens."), GetVlandiaHunterNarrativeOptionArgs, VlandiaHunterNarrativeOptionOnCondition, VlandiaHunterNarrativeOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:728: NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("sturgia_companion_option", new TextObject("{=mc78FEbA}A boyar's companions"), new TextObject("{=hob3WVkU}Your father was a member of a boyar's druzhina, the 'companions' that make up his retinue. He sat at his lord's table in the great hall, oversaw the boyar's estates, and stood by his side in the center of the shield wall in battle."), GetSturgiaCompanionNarrativeOptionArgs, SturgiaCompanionNarrativeOptionOnCondition, SturgiaCompanionNarrativeOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1316: NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("khuzait_retainer_option", new TextObject("{=FVaRDe2a}A noyan's kinsfolk"), new TextObject("{=jAs3kDXh}Your family were the trusted kinsfolk of a Khuzait noyan, and shared his meals in the chieftain's yurt. Your father assisted his chief in running the affairs of the clan and fought in the core of armored lancers in the center of the Khuzait battle line."), GetKhuzaitRetainerNarrativeOptionArgs, KhuzaitRetainerNarrativeOptionOnCondition, KhuzaitRetainerNarrativeOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1534: NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("childhood_brawn_option", new TextObject("{=5HXS8HEY}your brawn."), new TextObject("{=YKzuGc54}You were big, and other children looked to have you around in any scrap with children from a neighboring village. You pushed a plough and threw an axe like an adult."), GetChildhoodBrawnOptionArgs, ChildhoodBrawnOptionOnCondition, ChildhoodBrawnOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1542: NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("childhood_horse_option", new TextObject("{=MEgLE2kj}your skill with horses."), new TextObject("{=ngazFofr}You were always drawn to animals, and spent as much time as possible hanging out in the village stables. You could calm horses, and were sometimes called upon to break in new colts. You learned the basics of veterinary arts, much of which is applicable to humans as well."), GetChildhoodHorseOptionArgs, ChildhoodHorseOptionOnCondition, ChildhoodHorseOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1735: NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_education_menu", "narrative_childhood_menu", "narrative_youth_menu", new TextObject("{=rcoueCmk}Adolescence"), new TextObject("{=WYvnWcXQ}Like all village children you helped out in the fields. You also..."), list, GetEducationMenuNarrativeMenuCharacterArgs);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1742: NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("education_herder_option", new TextObject("{=RKVNvimC}herded the sheep."), new TextObject("{=KfaqPpbK}You went with other fleet-footed youths to take the villages' sheep, goats or cattle to graze in pastures near the village. You were in charge of chasing down stray beasts, and always kept a big stone on hand to be hurled at lurking predators if necessary."), GetEducationHerderOptionArgs, EducationHerderOptionOnCondition, EducationHerderOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1744: NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("education_smith_option", new TextObject("{=bTKiN0hr}worked in the village smithy."), new TextObject("{=y6j1bJTH}You were apprenticed to the local smith. You learned how to heat and forge metal, hammering for hours at a time until your muscles ached."), GetEducationSmithOptionArgs, EducationSmithOptionOnCondition, EducationSmithOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:1748: NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("education_doctor_option", new TextObject("{=TRwgSLD2}gathered herbs in the wild."), new TextObject("{=9ks4u5cH}You were sent by the village healer up into the hills to look for useful medicinal plants. You learned which herbs healed wounds or brought down a fever, and how to find them."), GetEducationDoctorOptionArgs, EducationDoctorOptionOnCondition, EducationDoctorOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2215: NarrativeMenuOption narrativeMenuOption17 = new NarrativeMenuOption("youth_camp_option", new TextObject("{=GFUggps8}marched with the camp followers."), new TextObject("{=64rWqBLN}You avoided service with one of the main forces of your realm's armies, but followed instead in the train - the troops' wives, lovers and servants, and those who make their living by caring for, entertaining, or cheating the soldiery."), GetYouthCampOptionArgs, YouthCampOptionOnCondition, YouthCampOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2846: NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("adulthood_saved_village_option", new TextObject("{=x1HTX5hq}you saved your village from a flood."), new TextObject("{=bWlmGDf3}When a sudden storm caused the local stream to rise suddenly, your neighbors needed quick-thinking leadership. You provided it, directing them to build levees to save their homes."), GetAdulthoodSavedVillageOptionArgs, AdulthoodSavedVillageOptionOnCondition, AdulthoodSavedVillageOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2850: NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("adulthood_workshop_option", new TextObject("{=xORjDTal}you invested some money in a workshop."), new TextObject("{=PyVqDLBu}Your parents didn't give you much money, but they did leave just enough for you to secure a loan against a larger amount to build a small workshop. You paid back what you borrowed, and sold your enterprise for a profit."), GetAdulthoodWorkshopOptionArgs, AdulthoodWorkshopOptionOnCondition, AdulthoodWorkshopOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2852: NarrativeMenuOption narrativeMenuOption7 = new NarrativeMenuOption("adulthood_investor_option", new TextObject("{=xKXcqRJI}you invested some money in land."), new TextObject("{=cbF9jdQo}Your parents didn't give you much money, but they did leave just enough for you to purchase a plot of unused land at the edge of the village. You cleared away rocks and dug an irrigation ditch, raised a few seasons of crops, than sold it for a considerable profit."), GetAdulthoodInvestorOptionArgs, AdulthoodInvestorOptionOnCondition, AdulthoodInvestorOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2854: NarrativeMenuOption narrativeMenuOption8 = new NarrativeMenuOption("adulthood_hunter_option", new TextObject("{=TbNRtUjb}you hunted a dangerous animal."), new TextObject("{=I3PcdaaL}Wolves, bears are a constant menace to the flocks of northern Calradia, while hyenas and leopards trouble the south. You went with a group of your fellow villagers and fired the missile that brought down the beast."), GetAdulthoodHunterOptionArgs, AdulthoodHunterOptionOnCondition, AdulthoodHunterOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2981: private void GetAdulthoodSavedVillageOptionArgs(NarrativeMenuOptionArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2998: private bool AdulthoodSavedVillageOptionOnCondition(CharacterCreationManager characterCreationManager)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:3011: private void AdulthoodSavedVillageOptionOnSelect(CharacterCreationManager characterCreationManager)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:27: private const int RaidDefenseRelationGainWithVillageNotable = 5;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:153: if (party.MobileParty.IsVillager)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:423: if (!item2.IsVillage || !(item2.Village.Bound.Town.Loyalty >= settlementLoyaltyModel.ThresholdForNotableRelationBonus))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:471: if (leaderParty == null || leaderParty.MapFaction == mapEvent.MapEventSettlement.MapFaction || winnerSide != BattleSideEnum.Attacker || hero == null || leaderParty2 == null || !leaderParty2.IsSettlement || !leaderParty2.Settlement.IsVillage || leaderParty2.Settlement.OwnerClan == Clan.PlayerClan)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:123: num += (float)fief.Settlement.BoundVillages.Count;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:465: float num = FactionHelper.FindIdealGarrisonStrengthPerWalledCenter(clan.MapFaction as Kingdom, clan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:120: VillageRaided,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:139: CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageRaided);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:176: campaignGameStarter.AddDialogLineWithVariation("companion_continue_grievance_raid_softspoken", "grievance_noticed", "grievance_listened", companion_grievance_raid_condition, null).Variation("{=zNvjSFaC}Pillaging villages is not what I signed up for.", "DefaultTag", 1).Variation("{=bpXgcBCp}What we did to that village... I don't like it. Those farmers, they're a lot like my people. I want to know it won't happen again.", "PersonaSoftspokenTag", 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:177: .Variation("{=4bkLDxIU}What we did back there, to that village... I don't do that. I want no part of it.", "PersonaCurtTag", 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:178: .Variation("{=VldAzBo5}I need to say something. What we did to that village - it was wrong. They're innocent farmers and they shouldn't have their homes and fields ransacked and burned like that. I won't do that again.", "PersonaEarnestTag", 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:179: .Variation("{=pDa7kOja}I know war is cruel, but I don't want to make it crueler than necessary. I'd rather not have the blood of innocents on my conscience, if you don't mind. Let's not raid villages like that.", "PersonaIronicTag", 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:194: return _currentGrievance.TypeOfGrievance == GrievanceType.VillageRaided;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:254: else if (_currentGrievance.TypeOfGrievance == GrievanceType.VillageRaided)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:256: MBTextManager.SetTextVariable("GRIEVANCE_SHORT_DESCRIPTION", "{=pQmUIjOQ}What I said about raiding villagers...");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:377: if ((type == GrievanceType.DesertedBattle && companionHero.GetTraitLevel(DefaultTraits.Valor) > 0) || (type == GrievanceType.Starvation && companionHero.GetTraitLevel(DefaultTraits.Generosity) > 0) || (type == GrievanceType.NoWage && companionHero.GetTraitLevel(DefaultTraits.Generosity) > 0) || (type == GrievanceType.VillageRaided && companionHero.GetTraitLevel(DefaultTraits.Mercy) > 0))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:389: private void OnVillageRaided(Village village)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:391: MapEvent mapEvent = village.Settlement.Party?.MapEvent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:400: DecideCompanionGrievances(GrievanceType.VillageRaided);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:409: EnterSettlementAction.ApplyForCharacterOnly(item, settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionsCampaignBehavior.cs:268: EnterSettlementAction.ApplyForCharacterOnly(hero, town2.Settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionsCampaignBehavior.cs:274: EnterSettlementAction.ApplyForCharacterOnly(hero, town.Settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionsCampaignBehavior.cs:392: EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:178: PlayerEncounter.EnterSettlement();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:204: GameMenu.SwitchToMenu("castle_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:208: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:236: GameMenu.SwitchToMenu("castle_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:240: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:263: GameMenu.SwitchToMenu("castle_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:267: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:300: GameMenu.SwitchToMenu("castle_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:304: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:32: private float DesertersSpawnRadiusAroundVillages => 0.2f * Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:187: List<Settlement> list = FindSettlementsAroundPoint(mapEvent.Position, (Settlement x) => x.IsVillage, MobileParty.NavigationType.Default, GetMaxVillageDistance());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:195: list.Add(SettlementHelper.FindNearestSettlementToPoint(mapEvent.Position, (Settlement x) => x.IsVillage));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:218: private float GetMaxVillageDistance()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:225: CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(settlement.GatePosition, MobileParty.NavigationType.Default, DesertersSpawnRadiusAroundVillages);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:231: CampaignVec2 campaignVec2 = NavigationHelper.FindReachablePointAroundPosition(campaignVec, MobileParty.NavigationType.Default, DesertersSpawnRadiusAroundVillages);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandPartyCampaignBehavior.cs:303: else if (relatedSettlement.IsVillage && relatedSettlement.Village.VillageState == Village.VillageStates.Normal)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandPartyCampaignBehavior.cs:305: MergeDisbandPartyToVillage(disbandParty, relatedSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandPartyCampaignBehavior.cs:364: private void MergeDisbandPartyToVillage(MobileParty disbandParty, Settlement settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EducationCampaignBehavior.cs:1160: TextObject textObject5 = new TextObject("{=9sdTLp49}The master artisan supervises the work of any smiths, carpenters or masons that you hire. {?PLAYER_CHILD}Your{?}The{\\?} child will join in the hard work and learn both stamina and craftsmanship.");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EducationCampaignBehavior.cs:1339: educationPage7.AddOption(new EducationOption(DefaultSkills.Throwing.Name, new TextObject("{=1oPRugXl}Trick knife throws and swift evasions can entertain a marketplace crowd, but are useful on the battlefield as well. "), null, null, null, null, new SkillObject[1] { DefaultSkills.Throwing }, childProperties41, specialCharacterProperties41));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EducationCampaignBehavior.cs:1641: educationPage.AddOption(new EducationOption(new TextObject("{=UpgqdJC0}a trip to your realm's court."), new TextObject("{=bTj16PqR}Every well-born youth wants to see the center of it all, where the lords and ladies gather in splendor to converse and connive. You invited the child to see the spectacle first hand, and provided the elegant clothes {?CHILD.GENDER}she{?}he{\\?}'d need to be part of it. "), null, null, null, new CharacterAttribute[1] { DefaultCharacterAttributes.Social }, new SkillObject[1] { DefaultSkills.Charm }, childProperties6));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:101: if (currentSettlement != null && (currentSettlement.IsFortification || currentSettlement.IsVillage))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:103: Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(Settlement.CurrentSettlement, out _accessDetails);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:130: gameSystemInitializer.AddGameMenu("naval_town_outside", "{=!}{PORT_OUTSIDE_TEXT}", naval_town_outside_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:131: gameSystemInitializer.AddGameMenuOption("naval_town_outside", "attack_the_blockade", "{=90OXjYk8}Attack the blockade to help the defenders", attack_blockade_besieger_side_on_condition, attack_blockade_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:132: gameSystemInitializer.AddGameMenuOption("naval_town_outside", "join_siege_defender", "{=X8KWb3PK}Break in through the blockade", attack_blockade_besieger_side_break_in_on_condition, game_menu_join_siege_event_on_defender_side_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:133: gameSystemInitializer.AddGameMenuOption("naval_town_outside", "join_encounter_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_town_naval_outside_leave_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:170: gameSystemInitializer.AddGameMenuOption("encounter", "village_raid_action", "{=lvttCRi8}Plunder the village, then raze it.", game_menu_village_hostile_action_on_condition, game_menu_village_raid_no_resist_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:171: gameSystemInitializer.AddGameMenuOption("encounter", "village_force_volunteer_action", "{=9YHjPkb8}Force notables to give you recruits.", game_menu_village_hostile_action_on_condition, game_menu_village_force_volunteers_no_resist_loot_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:172: gameSystemInitializer.AddGameMenuOption("encounter", "village_force_supplies_action", "{=JMzyh6Gl}Force people to give you supplies.", game_menu_village_hostile_action_on_condition, game_menu_village_force_supplies_no_resist_loot_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:203: gameSystemInitializer.AddGameMenu("town_outside", "{=!}{TOWN_TEXT}", game_menu_town_outside_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:204: gameSystemInitializer.AddGameMenuOption("town_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_town_outside_approach_gates_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:205: gameSystemInitializer.AddGameMenuOption("town_outside", "town_disguise_yourself", "{=VCREeAF1}Disguise yourself and sneak through the gate.", game_menu_town_disguise_yourself_on_condition, game_menu_town_initial_disguise_yourself_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:206: gameSystemInitializer.AddGameMenuOption("town_outside", "town_besiege", "{=WdIGdHuL}Besiege the town.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:207: gameSystemInitializer.AddGameMenuOption("town_outside", "town_enter_cheat", "{=!}Enter town (Cheat).", game_menu_town_outside_cheat_enter_on_condition, game_menu_town_outside_enter_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:208: gameSystemInitializer.AddGameMenuOption("town_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:212: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:218: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:232: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:247: gameSystemInitializer.AddGameMenu("castle_outside", "{=!}{TOWN_TEXT}", game_menu_castle_outside_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:248: gameSystemInitializer.AddGameMenuOption("castle_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_castle_outside_approach_gates_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:249: gameSystemInitializer.AddGameMenuOption("castle_outside", "town_besiege", "{=UzMYZgoE}Besiege the castle.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:250: gameSystemInitializer.AddGameMenuOption("castle_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:259: gameSystemInitializer.AddGameMenu("castle_enter_bribe", "{=yyz111nn}The guards say that they can't just let anyone in.", null, GameMenu.MenuOverlayType.SettlementWithCharacters);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:260: gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_pay", "{=3lxq5fvI}Pay a {AMOUNT}{GOLD_ICON} bribe to enter the castle.", game_menu_castle_enter_bribe_pay_bribe_on_condition, game_menu_castle_enter_bribe_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:261: gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_back", "{=E1OwmQFb}Back", game_menu_leave_on_condition, delegate
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:277: gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:278: gameSystemInitializer.AddGameMenu("village_loot_complete", "{=qt5bkw8l}On your orders your troops sack the village, pillaging everything of any value, and then put the buildings to the torch. From the coins and valuables that are found, you get your share.", game_menu_village_loot_complete_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:279: gameSystemInitializer.AddGameMenuOption("village_loot_complete", "continue", "{=veWOovVv}Continue...", game_menu_village_loot_complete_continue_on_condition, game_menu_village_loot_complete_continue_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:328: GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:371: string sceneName = LocationComplex.Current.GetLocationWithId("center").GetSceneName(wallLevel);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:698: if (encounteredBattle3 != null && encounteredBattle3.MapEventSettlement?.IsVillage == true)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:700: int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:701: if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:705: args.Tooltip.SetTextVariable("NUMBER", minimumNumberOfMenForAttackingVillageViaScene);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:778: GameMenu.SwitchToMenu("raiding_village");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:787: GameMenu.SwitchToMenu("raiding_village");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:797: GameMenu.SwitchToMenu("raiding_village");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:843: if (encounteredBattle2 != null && encounteredBattle2.MapEventSettlement?.IsVillage == true)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:848: int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:849: if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:853: args.Tooltip.SetTextVariable("NUMBER", minimumNumberOfMenForAttackingVillageViaScene);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:935: private void naval_town_outside_on_init(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:940: Debug.FailedAssert("naval_town_outside_on_init", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\EncounterGameMenuBehavior.cs", "naval_town_outside_on_init", 1125);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:948: textObject = new TextObject("{=n5A1tp2j}The settlement is under siege, and is also hostile to you. You may not enter.");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:956: game_menu_naval_town_outside_enter_on_consequence();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1168: PlayerEncounter.EnterSettlement();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1441: UpdateVillageHostileActionEncounter(args);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1458: private void UpdateVillageHostileActionEncounter(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1461: if (!(Game.Current.GameStateManager.ActiveState is MapState { MapConversationActive: false }) || battle?.MapEventSettlement == null || !battle.MapEventSettlement.IsVillage || !battle.DefenderSide.LeaderParty.IsSettlement || battle.AttackerSide != battle.GetMapEventSide(battle.PlayerSide))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1466: bool flag2 = ConsiderVillageSurrenderPossibility();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1485: game_menu_village_raid_no_resist_on_consequence(args);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1489: game_menu_village_force_supplies_no_resist_loot_on_consequence(args);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1493: game_menu_village_force_volunteers_no_resist_loot_on_consequence(args);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1498: Debug.FailedAssert("This case should not be happening anymore, check this case and make sure this is intended", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\EncounterGameMenuBehavior.cs", "UpdateVillageHostileActionEncounter", 1754);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1507: private bool ConsiderVillageSurrenderPossibility()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1511: if ((battle.IsRaid || battle.IsForcingSupplies || battle.IsForcingVolunteers) && battle.MapEventSettlement.IsVillage)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1518: num += party.Party.GetCustomStrength(BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Village);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1529: num2 += party2.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Village);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1539: num2 += attachedParty.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Village);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1562: if (encounteredBattle2 != null && encounteredBattle2.MapEventSettlement?.IsVillage == true)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1574: if (encounteredBattle4 != null && encounteredBattle4.MapEventSettlement?.IsVillage == true)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1600: int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1601: if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene || !ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty().AnyQ())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1605: if (MobileParty.MainParty.MemberRoster.TotalHealthyCount >= minimumNumberOfMenForAttackingVillageViaScene && ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty().AnyQ())

codex
For the no-war-gate decision, vanilla’s generic menu model sends hostile fortification encounters to `town_outside`/`castle_outside` rather than the inside `town`/`castle` menus. I’m checking the access model and “approach gates” path, because bribe/sneak-in can matter here.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Filter '*SettlementAccess*Model*.cs' -ErrorAction SilentlyContinue | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $p; for($i=95;$i -le 115;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=203;$i -le 208;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=1140;$i -le 1180;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs -Pattern \"approach_gates|CanMainHeroEnterSettlement|AccessDetails|Bribe|town_outside_enter|EnterSettlement\" -Context 2,8 | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\SettlementAccessModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs

 succeeded in 506ms:
   95: 		AddGameMenus(campaignGameStarter);
   96: 	}
   97: 
   98: 	private void InitializeAccessDetails()
   99: 	{
  100: 		Settlement currentSettlement = Settlement.CurrentSettlement;
  101: 		if (currentSettlement != null && (currentSettlement.IsFortification || currentSettlement.IsVillage))
  102: 		{
  103: 			Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(Settlement.CurrentSettlement, out _accessDetails);
  104: 		}
  105: 	}
  106: 
  107: 	private void AddGameMenus(CampaignGameStarter gameSystemInitializer)
  108: 	{
  109: 		gameSystemInitializer.AddGameMenu("taken_prisoner", "{=ezClQMBj}Your enemies take you as a prisoner.", game_menu_taken_prisoner_on_init);
  110: 		gameSystemInitializer.AddGameMenuOption("taken_prisoner", "taken_prisoner_continue", "{=WVkc4UgX}Continue.", game_menu_taken_prisoner_continue_on_condition, game_menu_taken_prisoner_continue_on_consequence);
  111: 		gameSystemInitializer.AddGameMenu("defeated_and_taken_prisoner", "{=ezClQMBj}Your enemies take you as a prisoner.", game_menu_taken_prisoner_on_init);
  112: 		gameSystemInitializer.AddGameMenuOption("defeated_and_taken_prisoner", "taken_prisoner_continue", "{=WVkc4UgX}Continue.", game_menu_taken_prisoner_continue_on_condition, game_menu_taken_prisoner_continue_on_consequence);
  113: 		gameSystemInitializer.AddGameMenu("encounter_meeting", "{=!}.", game_menu_encounter_meeting_on_init);
  114: 		gameSystemInitializer.AddGameMenu("join_encounter", "{=jKWJpIES}{JOIN_ENCOUNTER_TEXT}. You decide to...", game_menu_join_encounter_on_init);
  115: 		gameSystemInitializer.AddGameMenuOption("join_encounter", "join_encounter_help_attackers", "{=h3yEHb4U}Help {ATTACKER}.", game_menu_join_encounter_help_attackers_on_condition, game_menu_join_encounter_help_attackers_on_consequence);
  203: 		gameSystemInitializer.AddGameMenu("town_outside", "{=!}{TOWN_TEXT}", game_menu_town_outside_on_init);
  204: 		gameSystemInitializer.AddGameMenuOption("town_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_town_outside_approach_gates_on_consequence);
  205: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_disguise_yourself", "{=VCREeAF1}Disguise yourself and sneak through the gate.", game_menu_town_disguise_yourself_on_condition, game_menu_town_initial_disguise_yourself_on_consequence);
  206: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_besiege", "{=WdIGdHuL}Besiege the town.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
  207: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_enter_cheat", "{=!}Enter town (Cheat).", game_menu_town_outside_cheat_enter_on_condition, game_menu_town_outside_enter_on_consequence);
  208: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
 1140: 			return !mobileParty.IsMainParty;
 1141: 		}
 1142: 		return true;
 1143: 	}
 1144: 
 1145: 	private bool game_menu_siege_attacker_left_return_to_settlement_on_condition(MenuCallbackArgs args)
 1146: 	{
 1147: 		args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
 1148: 		GameTexts.SetVariable("SETTLEMENT", MobileParty.MainParty.LastVisitedSettlement.Name);
 1149: 		return true;
 1150: 	}
 1151: 
 1152: 	private void game_menu_siege_attacker_left_return_to_settlement_on_consequence(MenuCallbackArgs args)
 1153: 	{
 1154: 		if (PlayerEncounter.Current != null)
 1155: 		{
 1156: 			PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
 1157: 		}
 1158: 		if (MobileParty.MainParty.AttachedTo == null)
 1159: 		{
 1160: 			EncounterManager.StartSettlementEncounter(MobileParty.MainParty, MobileParty.MainParty.LastVisitedSettlement);
 1161: 		}
 1162: 		else
 1163: 		{
 1164: 			EncounterManager.StartSettlementEncounter(MobileParty.MainParty.AttachedTo, MobileParty.MainParty.LastVisitedSettlement);
 1165: 		}
 1166: 		if (PlayerEncounter.Current != null && PlayerEncounter.LocationEncounter == null)
 1167: 		{
 1168: 			PlayerEncounter.EnterSettlement();
 1169: 		}
 1170: 		string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
 1171: 		if (string.IsNullOrEmpty(genericStateMenu))
 1172: 		{
 1173: 			GameMenu.ExitToLast();
 1174: 		}
 1175: 		else
 1176: 		{
 1177: 			GameMenu.SwitchToMenu(genericStateMenu);
 1178: 		}
 1179: 	}
 1180: 

 succeeded in 570ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:33: 	private SettlementAccessModel.AccessDetails _accessDetails;
  	private int _breakInOutArmyCasualties;
  
  
  	private bool _playerIsAlreadyInCastle;
  
  	private const float SmugglingCrimeRate = 10f;
  
  	private bool _isBreakingOutFromPort;
  
  	private const float RatioOfItemsToRemoveOnTryToGetAway = 0.15f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:94: 		InitializeAccessDetails();
  	private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
  	{
  		AddGameMenus(campaignGameStarter);
  	}
  
  	private void InitializeAccessDetails()
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		if (currentSettlement != null && (currentSettlement.IsFortification || currentSettlement.IsVillage))
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:98: 	private void InitializeAccessDetails()
  	}
  
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		if (currentSettlement != null && (currentSettlement.IsFortification || currentSettlement.IsVillage))
  		{
  			Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(Settlement.CurrentSettlement, out _accessDetails);
  		}
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:103: 			Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(Settlement.CurrentSettlement, out _accessDetails);
  		if (currentSettlement != null && (currentSettlement.IsFortification || currentSettlement.IsVillage))
  		{
  		}
  	}
  
  	private void AddGameMenus(CampaignGameStarter gameSystemInitializer)
  	{
  		gameSystemInitializer.AddGameMenu("taken_prisoner", "{=ezClQMBj}Your enemies take you as a prisoner.", game_menu_taken_prisoner_on_init);
  		gameSystemInitializer.AddGameMenuOption("taken_prisoner", "taken_prisoner_continue", "{=WVkc4UgX}Continue.", game_menu_taken_prisoner_continue_on_condition, game_menu_taken_prisoner_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("defeated_and_taken_prisoner", "{=ezClQMBj}Your enemies take you as a prisoner.", game_menu_taken_prisoner_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:204: 		gameSystemInitializer.AddGameMenuOption("town_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_town_outside_approach_gates_on_consequence);
  		gameSystemInitializer.AddGameMenu("assault_town_order_attack", "", game_menu_town_assault_order_attack_on_init);
  		gameSystemInitializer.AddGameMenu("town_outside", "{=!}{TOWN_TEXT}", game_menu_town_outside_on_init);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_disguise_yourself", "{=VCREeAF1}Disguise yourself and sneak through the gate.", game_menu_town_disguise_yourself_on_condition, game_menu_town_initial_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_besiege", "{=WdIGdHuL}Besiege the town.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_enter_cheat", "{=!}Enter town (Cheat).", game_menu_town_outside_cheat_enter_on_condition, game_menu_town_outside_enter_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("disguise_blocked_night_time", "{=KZ27sSXS}With increased security at night guards check the identity of every entry. You can't sneak in during the night.", null);
  		gameSystemInitializer.AddGameMenuOption("disguise_blocked_night_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:207: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_enter_cheat", "{=!}Enter town (Cheat).", game_menu_town_outside_cheat_enter_on_condition, game_menu_town_outside_enter_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_disguise_yourself", "{=VCREeAF1}Disguise yourself and sneak through the gate.", game_menu_town_disguise_yourself_on_condition, game_menu_town_initial_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_besiege", "{=WdIGdHuL}Besiege the town.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("disguise_blocked_night_time", "{=KZ27sSXS}With increased security at night guards check the identity of every entry. You can't sneak in during the night.", null);
  		gameSystemInitializer.AddGameMenuOption("disguise_blocked_night_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("disguise_first_time", "{=6q7UsTtn}You have no contact in this town, you need to set one up.", first_time_disguise_on_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "continue", "{=WjwHVQzx}Set up contact", launch_mission_on_condition, launch_disguise_mission);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:248: 		gameSystemInitializer.AddGameMenuOption("castle_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_castle_outside_approach_gates_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("army_left_settlement_due_to_war_declaration", "army_left_settlement_due_to_war_declaration_continue", "{=WVkc4UgX}Continue.", game_menu_army_left_settlement_due_to_war_on_condition, game_menu_army_left_settlement_due_to_war_on_consequence);
  		gameSystemInitializer.AddGameMenu("castle_outside", "{=!}{TOWN_TEXT}", game_menu_castle_outside_on_init);
  		gameSystemInitializer.AddGameMenuOption("castle_outside", "town_besiege", "{=UzMYZgoE}Besiege the castle.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("castle_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("town_guard", "{=SxkaQbSa}You approach the gate. The men on the walls watch you closely.", null);
  		gameSystemInitializer.AddGameMenuOption("town_guard", "request_meeting_commander", "{=RSQbOjub}Request a meeting with someone.", game_menu_request_meeting_someone_on_condition, game_menu_request_meeting_someone_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_guard", "guard_discuss_criminal_surrender", "{=ACvQdkG8}Discuss the terms of your surrender", outside_menu_criminal_on_condition, outside_menu_criminal_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("town_guard", "guard_back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, game_menu_town_guard_back_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("castle_guard", "{=SxkaQbSa}You approach the gate. The men on the walls watch you closely.", null);
  		gameSystemInitializer.AddGameMenuOption("castle_guard", "request_shelter", "{=mG9jW8Fp}Request entry to the castle.", game_menu_town_guard_request_shelter_on_condition, game_menu_request_entry_to_castle_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:259: 		gameSystemInitializer.AddGameMenu("castle_enter_bribe", "{=yyz111nn}The guards say that they can't just let anyone in.", null, GameMenu.MenuOverlayType.SettlementWithCharacters);
  		gameSystemInitializer.AddGameMenuOption("castle_guard", "request_meeting_commander", "{=RSQbOjub}Request a meeting with someone.", game_menu_request_meeting_someone_on_condition, game_menu_request_meeting_someone_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("castle_guard", "guard_back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, game_menu_town_guard_back_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_pay", "{=3lxq5fvI}Pay a {AMOUNT}{GOLD_ICON} bribe to enter the castle.", game_menu_castle_enter_bribe_pay_bribe_on_condition, game_menu_castle_enter_bribe_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_back", "{=E1OwmQFb}Back", game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("castle_guard");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_granted", "{=Mg1PotzO}After a brief wait, the guards open the gates for you and allow your party inside.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_castle_entry_granted", "str_continue", "{=bLNocKd1}Continue..", game_request_entry_to_castle_approved_continue_on_condition, game_request_entry_to_castle_approved_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_denied", "{=QpQQJjD6}The lord of this castle has forbidden you from coming inside these walls, and the guard sergeant informs you that his men will fire if you attempt to come any closer.", menu_castle_entry_denied_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:260: 		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_pay", "{=3lxq5fvI}Pay a {AMOUNT}{GOLD_ICON} bribe to enter the castle.", game_menu_castle_enter_bribe_pay_bribe_on_condition, game_menu_castle_enter_bribe_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("castle_guard", "guard_back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, game_menu_town_guard_back_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("castle_enter_bribe", "{=yyz111nn}The guards say that they can't just let anyone in.", null, GameMenu.MenuOverlayType.SettlementWithCharacters);
  		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_back", "{=E1OwmQFb}Back", game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("castle_guard");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_granted", "{=Mg1PotzO}After a brief wait, the guards open the gates for you and allow your party inside.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_castle_entry_granted", "str_continue", "{=bLNocKd1}Continue..", game_request_entry_to_castle_approved_continue_on_condition, game_request_entry_to_castle_approved_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_denied", "{=QpQQJjD6}The lord of this castle has forbidden you from coming inside these walls, and the guard sergeant informs you that his men will fire if you attempt to come any closer.", menu_castle_entry_denied_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_castle_entry_denied", "str_continue", "{=veWOovVv}Continue...", null, game_request_entry_to_castle_rejected_continue_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:261: 		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_back", "{=E1OwmQFb}Back", game_menu_leave_on_condition, delegate
  		gameSystemInitializer.AddGameMenu("castle_enter_bribe", "{=yyz111nn}The guards say that they can't just let anyone in.", null, GameMenu.MenuOverlayType.SettlementWithCharacters);
  		gameSystemInitializer.AddGameMenuOption("castle_enter_bribe", "castle_bribe_pay", "{=3lxq5fvI}Pay a {AMOUNT}{GOLD_ICON} bribe to enter the castle.", game_menu_castle_enter_bribe_pay_bribe_on_condition, game_menu_castle_enter_bribe_on_consequence);
  		{
  			GameMenu.SwitchToMenu("castle_guard");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_granted", "{=Mg1PotzO}After a brief wait, the guards open the gates for you and allow your party inside.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_castle_entry_granted", "str_continue", "{=bLNocKd1}Continue..", game_request_entry_to_castle_approved_continue_on_condition, game_request_entry_to_castle_approved_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_castle_entry_denied", "{=QpQQJjD6}The lord of this castle has forbidden you from coming inside these walls, and the guard sergeant informs you that his men will fire if you attempt to come any closer.", menu_castle_entry_denied_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_castle_entry_denied", "str_continue", "{=veWOovVv}Continue...", null, game_request_entry_to_castle_rejected_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("request_meeting", "{=pBAx7jTM}With whom do you want to meet?", game_menu_town_menu_request_meeting_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:937: 		InitializeAccessDetails();
  	private void naval_town_outside_on_init(MenuCallbackArgs args)
  	{
  		if (PlayerEncounter.EncounterSettlement.IsUnderSiege && PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement.Party.SiegeEvent == null)
  		{
  			Debug.FailedAssert("naval_town_outside_on_init", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\EncounterGameMenuBehavior.cs", "naval_town_outside_on_init", 1125);
  			PlayerEncounter.Finish();
  		}
  		TextObject textObject = null;
  		if (PlayerEncounter.EncounterSettlement.IsUnderSiege)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:956: 				game_menu_naval_town_outside_enter_on_consequence();
  			else
  			{
  			}
  		}
  		else if (PlayerEncounter.EncounterSettlement.MapFaction.IsAtWarWith(Clan.PlayerClan.MapFaction))
  		{
  			textObject = new TextObject("{=eGizNNNC}The settlement is hostile to you, and you will not be allowed to dock at the port.");
  		}
  		else if (game_menu_town_disguise_yourself_on_condition(args))
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1168: 			PlayerEncounter.EnterSettlement();
  		if (PlayerEncounter.Current != null && PlayerEncounter.LocationEncounter == null)
  		{
  		}
  		string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
  		if (string.IsNullOrEmpty(genericStateMenu))
  		{
  			GameMenu.ExitToLast();
  		}
  		else
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2298: 		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
  		args.MenuTitle = encounterSettlement.Name;
  		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
  		int num = (int)accessLevel;
  		TextObject textObject;
  		if (num != 0)
  		{
  			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  			{
  				goto IL_0107;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2299: 		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
  		args.MenuTitle = encounterSettlement.Name;
  		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		int num = (int)accessLevel;
  		TextObject textObject;
  		if (num != 0)
  		{
  			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  			{
  				goto IL_0107;
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2304: 			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  		if (num != 0)
  		{
  			{
  				goto IL_0107;
  			}
  			textObject = GameTexts.FindText("str_gate_down_criminal_text");
  			textObject.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  		}
  		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.HostileFaction)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2311: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.HostileFaction)
  			textObject.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  		}
  		{
  			if (encounterSettlement.InRebelliousState)
  			{
  				textObject = GameTexts.FindText("str_gate_down_enemy_text_castle_low_loyalty");
  				textObject.SetTextVariable("FACTION_INFORMAL_NAME", encounterSettlement.MapFaction.InformalName);
  			}
  			else
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2325: 			if (_accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  		else
  		{
  			{
  				goto IL_0107;
  			}
  			textObject = GameTexts.FindText("str_gate_down_criminal_text");
  			textObject.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  		}
  		goto IL_0140;
  		IL_0140:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2337: 		if (_accessDetails.PreliminaryActionObligation == SettlementAccessModel.PreliminaryActionObligation.Optional && _accessDetails.PreliminaryActionType == SettlementAccessModel.PreliminaryActionType.FaceCharges)
  		textObject.SetTextVariable("FACTION_TERM", encounterSettlement.MapFaction.EncyclopediaLinkWithName);
  		MBTextManager.SetTextVariable("TOWN_TEXT", textObject);
  		{
  			GameMenu.SwitchToMenu("town_inside_criminal");
  		}
  		else if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess && _accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.Direct)
  		{
  			GameMenu.SwitchToMenu("town");
  		}
  		return;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2341: 		else if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess && _accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.Direct)
  			GameMenu.SwitchToMenu("town_inside_criminal");
  		}
  		{
  			GameMenu.SwitchToMenu("town");
  		}
  		return;
  		IL_0107:
  		if (encounterSettlement.InRebelliousState)
  		{
  			textObject = GameTexts.FindText("str_settlement_not_allowed_text_low_loyalty");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2391: 		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
  		args.MenuTitle = encounterSettlement.Name;
  		TextObject empty = TextObject.GetEmpty();
  		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
  		int num = (int)accessLevel;
  		if (num != 0)
  		{
  			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  			{
  				empty = ((encounterSettlement.OwnerClan != Hero.MainHero.Clan) ? GameTexts.FindText("str_castle_text_1") : GameTexts.FindText("str_castle_text_yours"));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2393: 		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
  		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		TextObject empty = TextObject.GetEmpty();
  		int num = (int)accessLevel;
  		if (num != 0)
  		{
  			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  			{
  				empty = ((encounterSettlement.OwnerClan != Hero.MainHero.Clan) ? GameTexts.FindText("str_castle_text_1") : GameTexts.FindText("str_castle_text_yours"));
  			}
  			else
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2397: 			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
  		if (num != 0)
  		{
  			{
  				empty = ((encounterSettlement.OwnerClan != Hero.MainHero.Clan) ? GameTexts.FindText("str_castle_text_1") : GameTexts.FindText("str_castle_text_yours"));
  			}
  			else
  			{
  				empty.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  				empty = GameTexts.FindText("str_gate_down_criminal_text");
  			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2407: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.HostileFaction)
  			}
  		}
  		{
  			empty = GameTexts.FindText("str_gate_down_enemy_text_castle");
  		}
  		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating)
  		{
  			empty.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  			empty = GameTexts.FindText("str_gate_down_criminal_text");
  		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2411: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating)
  			empty = GameTexts.FindText("str_gate_down_enemy_text_castle");
  		}
  		{
  			empty.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
  			empty = GameTexts.FindText("str_gate_down_criminal_text");
  		}
  		else
  		{
  			empty = GameTexts.FindText("str_settlement_not_allowed_text");
  		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2424: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess && (_accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.Direct || (_playerIsAlreadyInCastle && _accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.ByRequest)))
  		empty.SetTextVariable("SETTLEMENT_NAME", encounterSettlement.EncyclopediaLinkWithName);
  		MBTextManager.SetTextVariable("TOWN_TEXT", empty);
  		{
  			GameMenu.SwitchToMenu("castle");
  		}
  		else
  		{
  			_playerIsAlreadyInCastle = false;
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2439: 	private void game_menu_town_outside_approach_gates_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		GameMenu.SwitchToMenu("town_guard");
  	}
  
  	private bool game_menu_castle_outside_approach_gates_on_condition(MenuCallbackArgs args)
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
  		return true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2444: 	private bool game_menu_castle_outside_approach_gates_on_condition(MenuCallbackArgs args)
  	}
  
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
  		return true;
  	}
  
  	private void game_menu_castle_outside_approach_gates_on_consequence(MenuCallbackArgs args)
  	{
  		GameMenu.SwitchToMenu("castle_guard");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2450: 	private void game_menu_castle_outside_approach_gates_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		GameMenu.SwitchToMenu("castle_guard");
  	}
  
  	private void game_menu_fortification_high_crime_rating_continue_on_consequence(MenuCallbackArgs args)
  	{
  		PlayerEncounter.Finish();
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2463: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
  		{
  			return _accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating;
  		}
  		return false;
  	}
  
  	private void outside_menu_criminal_on_consequence(MenuCallbackArgs args)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2465: 			return _accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating;
  		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
  		{
  		}
  		return false;
  	}
  
  	private void outside_menu_criminal_on_consequence(MenuCallbackArgs args)
  	{
  		GameMenu.SwitchToMenu("town_discuss_criminal_surrender");
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2496: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
  		args.optionLeaveType = GameMenuOption.LeaveType.SneakIn;
  		MBTextManager.SetTextVariable("SNEAK_CHANCE", MathF.Round(Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) * 100f));
  		{
  			return _accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Disguise;
  		}
  		return false;
  	}
  
  	private void game_menu_town_initial_disguise_yourself_on_consequence(MenuCallbackArgs args)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2498: 			return _accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Disguise;
  		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
  		{
  		}
  		return false;
  	}
  
  	private void game_menu_town_initial_disguise_yourself_on_consequence(MenuCallbackArgs args)
  	{
  		if (CampaignTime.Now.IsNightTime)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2613: 	private void game_menu_town_outside_enter_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		GameMenu.SwitchToMenu("town");
  		PlayerEncounter.LocationEncounter.IsInsideOfASettlement = true;
  	}
  
  	private void game_menu_naval_town_outside_enter_on_consequence()
  	{
  		if (PlayerEncounter.Current != null && PlayerEncounter.LocationEncounter == null && !PlayerEncounter.EncounterSettlement.IsUnderSiege)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2619: 	private void game_menu_naval_town_outside_enter_on_consequence()
  	}
  
  	{
  		if (PlayerEncounter.Current != null && PlayerEncounter.LocationEncounter == null && !PlayerEncounter.EncounterSettlement.IsUnderSiege)
  		{
  			PlayerEncounter.EnterSettlement();
  		}
  		if (Settlement.CurrentSettlement.SiegeEvent != null)
  		{
  			GameMenu.SwitchToMenu("join_siege_event");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2623: 			PlayerEncounter.EnterSettlement();
  		if (PlayerEncounter.Current != null && PlayerEncounter.LocationEncounter == null && !PlayerEncounter.EncounterSettlement.IsUnderSiege)
  		{
  		}
  		if (Settlement.CurrentSettlement.SiegeEvent != null)
  		{
  			GameMenu.SwitchToMenu("join_siege_event");
  		}
  		else
  		{
  			GameMenu.SwitchToMenu("port_menu");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2687: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.NoAccess && _accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating)
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
  		{
  			args.Tooltip = new TextObject("{=03DZpTYi}You are a wanted criminal.");
  			args.IsEnabled = false;
  		}
  		List<Location> locations = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall" || x == "prison").ToList();
  		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
  		return true;
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2699: 		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterLordsHall(Settlement.CurrentSettlement, out var accessDetails);
  	private void game_menu_request_entry_to_castle_on_consequence(MenuCallbackArgs args)
  	{
  		if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess)
  		{
  			_playerIsAlreadyInCastle = true;
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
  		}
  		else if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess && accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Bribe)
  		{
  			if (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement) > 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2700: 		if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess)
  	{
  		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterLordsHall(Settlement.CurrentSettlement, out var accessDetails);
  		{
  			_playerIsAlreadyInCastle = true;
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
  		}
  		else if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess && accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Bribe)
  		{
  			if (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement) > 0)
  			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2705: 		else if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess && accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Bribe)
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
  		}
  		{
  			if (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement) > 0)
  			{
  				GameMenu.SwitchToMenu("castle_enter_bribe");
  				return;
  			}
  			_playerIsAlreadyInCastle = true;
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2707: 			if (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement) > 0)
  		else if (accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess && accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Bribe)
  		{
  			{
  				GameMenu.SwitchToMenu("castle_enter_bribe");
  				return;
  			}
  			_playerIsAlreadyInCastle = true;
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
  		}
  		else
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2709: 				GameMenu.SwitchToMenu("castle_enter_bribe");
  			if (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement) > 0)
  			{
  				return;
  			}
  			_playerIsAlreadyInCastle = true;
  			GameMenu.SwitchToMenu("menu_castle_entry_granted");
  		}
  		else
  		{
  			GameMenu.SwitchToMenu("menu_castle_entry_denied");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2751: 	private static bool game_menu_castle_enter_bribe_pay_bribe_on_condition(MenuCallbackArgs args)
  	}
  
  	{
  		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
  		List<Location> locations = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall").ToList();
  		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
  		args.optionLeaveType = GameMenuOption.LeaveType.Mission;
  		if (Hero.MainHero.Gold < bribeToEnterLordsHall)
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2753: 		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  	private static bool game_menu_castle_enter_bribe_pay_bribe_on_condition(MenuCallbackArgs args)
  	{
  		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
  		List<Location> locations = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall").ToList();
  		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
  		args.optionLeaveType = GameMenuOption.LeaveType.Mission;
  		if (Hero.MainHero.Gold < bribeToEnterLordsHall)
  		{
  			args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.");
  			args.IsEnabled = false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2754: 		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
  	{
  		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  		List<Location> locations = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall").ToList();
  		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
  		args.optionLeaveType = GameMenuOption.LeaveType.Mission;
  		if (Hero.MainHero.Gold < bribeToEnterLordsHall)
  		{
  			args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.");
  			args.IsEnabled = false;
  		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2758: 		if (Hero.MainHero.Gold < bribeToEnterLordsHall)
  		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
  		args.optionLeaveType = GameMenuOption.LeaveType.Mission;
  		{
  			args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.");
  			args.IsEnabled = false;
  		}
  		return bribeToEnterLordsHall > 0;
  	}
  
  	private void game_menu_castle_enter_bribe_on_consequence(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2763: 		return bribeToEnterLordsHall > 0;
  			args.IsEnabled = false;
  		}
  	}
  
  	private void game_menu_castle_enter_bribe_on_consequence(MenuCallbackArgs args)
  	{
  		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  		BribeGuardsAction.Apply(Settlement.CurrentSettlement, bribeToEnterLordsHall);
  		_playerIsAlreadyInCastle = true;
  		GameMenu.SwitchToMenu("menu_castle_entry_granted");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2766: 	private void game_menu_castle_enter_bribe_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  		BribeGuardsAction.Apply(Settlement.CurrentSettlement, bribeToEnterLordsHall);
  		_playerIsAlreadyInCastle = true;
  		GameMenu.SwitchToMenu("menu_castle_entry_granted");
  	}
  
  	private void game_menu_town_menu_request_meeting_on_init(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2768: 		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  	private void game_menu_castle_enter_bribe_on_consequence(MenuCallbackArgs args)
  	{
  		BribeGuardsAction.Apply(Settlement.CurrentSettlement, bribeToEnterLordsHall);
  		_playerIsAlreadyInCastle = true;
  		GameMenu.SwitchToMenu("menu_castle_entry_granted");
  	}
  
  	private void game_menu_town_menu_request_meeting_on_init(MenuCallbackArgs args)
  	{
  		List<Hero> heroesToMeetInTown = TownHelpers.GetHeroesToMeetInTown(Settlement.CurrentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2769: 		BribeGuardsAction.Apply(Settlement.CurrentSettlement, bribeToEnterLordsHall);
  	{
  		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
  		_playerIsAlreadyInCastle = true;
  		GameMenu.SwitchToMenu("menu_castle_entry_granted");
  	}
  
  	private void game_menu_town_menu_request_meeting_on_init(MenuCallbackArgs args)
  	{
  		List<Hero> heroesToMeetInTown = TownHelpers.GetHeroesToMeetInTown(Settlement.CurrentSettlement);
  		args.MenuContext.SetRepeatObjectList(heroesToMeetInTown);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3173: 			PlayerEncounter.EnterSettlement();
  		if (Hero.MainHero.CurrentSettlement == null)
  		{
  		}
  		if (PlayerSiege.PlayerSiegeEvent == null)
  		{
  			PlayerSiege.StartPlayerSiege(BattleSideEnum.Defender);
  		}
  		if (Hero.MainHero.CurrentSettlement.Party.MapEvent != null)
  		{
  			GameMenu.SwitchToMenu("join_encounter");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3263: 	[GameMenuInitializationHandler("castle_enter_bribe")]
  	}
  
  	public static void game_menu_castle_menu_sound_on_init(MenuCallbackArgs args)
  	{
  		args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
  		args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/keep");
  	}
  
  	[GameMenuInitializationHandler("naval_encounter_disengaged")]
  	public static void game_menu_encounter_naval_disengaged_init(MenuCallbackArgs args)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementAccessModel.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -le 220){'"'{0,5}: {1}' -f "'$i,$_} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $p; for($i=2290;$i -le 2348;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $p; for($i=2388;$i -le 2430;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $p; for($i=2490;$i -le 2528;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=2611;$i -le 2617;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=2751;$i -le 2771;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 754ms:
 2290: 	{
 2291: 		GameMenu.SwitchToMenu("village");
 2292: 	}
 2293: 
 2294: 	private void game_menu_town_outside_on_init(MenuCallbackArgs args)
 2295: 	{
 2296: 		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
 2297: 		args.MenuTitle = encounterSettlement.Name;
 2298: 		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
 2299: 		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
 2300: 		int num = (int)accessLevel;
 2301: 		TextObject textObject;
 2302: 		if (num != 0)
 2303: 		{
 2304: 			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
 2305: 			{
 2306: 				goto IL_0107;
 2307: 			}
 2308: 			textObject = GameTexts.FindText("str_gate_down_criminal_text");
 2309: 			textObject.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
 2310: 		}
 2311: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.HostileFaction)
 2312: 		{
 2313: 			if (encounterSettlement.InRebelliousState)
 2314: 			{
 2315: 				textObject = GameTexts.FindText("str_gate_down_enemy_text_castle_low_loyalty");
 2316: 				textObject.SetTextVariable("FACTION_INFORMAL_NAME", encounterSettlement.MapFaction.InformalName);
 2317: 			}
 2318: 			else
 2319: 			{
 2320: 				textObject = GameTexts.FindText("str_gate_down_enemy_text_castle");
 2321: 			}
 2322: 		}
 2323: 		else
 2324: 		{
 2325: 			if (_accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
 2326: 			{
 2327: 				goto IL_0107;
 2328: 			}
 2329: 			textObject = GameTexts.FindText("str_gate_down_criminal_text");
 2330: 			textObject.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
 2331: 		}
 2332: 		goto IL_0140;
 2333: 		IL_0140:
 2334: 		textObject.SetTextVariable("SETTLEMENT_NAME", encounterSettlement.EncyclopediaLinkWithName);
 2335: 		textObject.SetTextVariable("FACTION_TERM", encounterSettlement.MapFaction.EncyclopediaLinkWithName);
 2336: 		MBTextManager.SetTextVariable("TOWN_TEXT", textObject);
 2337: 		if (_accessDetails.PreliminaryActionObligation == SettlementAccessModel.PreliminaryActionObligation.Optional && _accessDetails.PreliminaryActionType == SettlementAccessModel.PreliminaryActionType.FaceCharges)
 2338: 		{
 2339: 			GameMenu.SwitchToMenu("town_inside_criminal");
 2340: 		}
 2341: 		else if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess && _accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.Direct)
 2342: 		{
 2343: 			GameMenu.SwitchToMenu("town");
 2344: 		}
 2345: 		return;
 2346: 		IL_0107:
 2347: 		if (encounterSettlement.InRebelliousState)
 2348: 		{

 succeeded in 774ms:
    1: using System.Linq;
    2: using Helpers;
    3: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    4: using TaleWorlds.CampaignSystem.ComponentInterfaces;
    5: using TaleWorlds.CampaignSystem.Party;
    6: using TaleWorlds.CampaignSystem.Settlements;
    7: using TaleWorlds.CampaignSystem.Settlements.Locations;
    8: using TaleWorlds.Core;
    9: using TaleWorlds.Library;
   10: using TaleWorlds.Localization;
   11: 
   12: namespace TaleWorlds.CampaignSystem.GameComponents;
   13: 
   14: public class DefaultSettlementAccessModel : SettlementAccessModel
   15: {
   16: 	public override void CanMainHeroEnterSettlement(Settlement settlement, out AccessDetails accessDetails)
   17: 	{
   18: 		if (settlement.IsFortification && Hero.MainHero.MapFaction == settlement.MapFaction && (settlement.Town.GarrisonParty == null || settlement.Town.GarrisonParty.Party.NumberOfAllMembers == 0))
   19: 		{
   20: 			accessDetails = new AccessDetails
   21: 			{
   22: 				AccessLevel = AccessLevel.FullAccess,
   23: 				AccessMethod = AccessMethod.Direct
   24: 			};
   25: 		}
   26: 		else if (settlement.IsTown)
   27: 		{
   28: 			CanMainHeroEnterTown(settlement, out accessDetails);
   29: 		}
   30: 		else if (settlement.IsCastle)
   31: 		{
   32: 			CanMainHeroEnterCastle(settlement, out accessDetails);
   33: 		}
   34: 		else if (settlement.IsVillage)
   35: 		{
   36: 			CanMainHeroEnterVillage(settlement, out accessDetails);
   37: 		}
   38: 		else
   39: 		{
   40: 			Debug.FailedAssert("Invalid type of settlement", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultSettlementAccessModel.cs", "CanMainHeroEnterSettlement", 42);
   41: 			accessDetails = new AccessDetails
   42: 			{
   43: 				AccessLevel = AccessLevel.FullAccess,
   44: 				AccessMethod = AccessMethod.Direct
   45: 			};
   46: 		}
   47: 	}
   48: 
   49: 	public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails)
   50: 	{
   51: 		accessDetails = default(AccessDetails);
   52: 		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
   53: 	}
   54: 
   55: 	public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
   56: 	{
   57: 		accessDetails = default(AccessDetails);
   58: 		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
   59: 	}
   60: 
   61: 	private void CanMainHeroEnterKeepInternal(Settlement settlement, out AccessDetails accessDetails)
   62: 	{
   63: 		accessDetails = default(AccessDetails);
   64: 		Hero mainHero = Hero.MainHero;
   65: 		if (settlement.OwnerClan == mainHero.Clan)
   66: 		{
   67: 			accessDetails = new AccessDetails
   68: 			{
   69: 				AccessLevel = AccessLevel.FullAccess,
   70: 				AccessMethod = AccessMethod.Direct
   71: 			};
   72: 		}
   73: 		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
   74: 		{
   75: 			accessDetails = new AccessDetails
   76: 			{
   77: 				AccessLevel = AccessLevel.FullAccess,
   78: 				AccessMethod = AccessMethod.Direct
   79: 			};
   80: 		}
   81: 		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
   82: 		{
   83: 			if (Campaign.Current.IsMainHeroDisguised)
   84: 			{
   85: 				accessDetails = new AccessDetails
   86: 				{
   87: 					AccessLevel = AccessLevel.LimitedAccess,
   88: 					LimitedAccessSolution = LimitedAccessSolution.Disguise,
   89: 					AccessLimitationReason = AccessLimitationReason.Disguised
   90: 				};
   91: 			}
   92: 			else if (Campaign.Current.Models.CrimeModel.DoesPlayerHaveAnyCrimeRating(settlement.MapFaction))
   93: 			{
   94: 				accessDetails = new AccessDetails
   95: 				{
   96: 					AccessLevel = AccessLevel.LimitedAccess,
   97: 					LimitedAccessSolution = LimitedAccessSolution.Bribe,
   98: 					AccessLimitationReason = AccessLimitationReason.CrimeRating
   99: 				};
  100: 			}
  101: 			else if (mainHero.Clan.Tier < 3)
  102: 			{
  103: 				accessDetails = new AccessDetails
  104: 				{
  105: 					AccessLevel = AccessLevel.LimitedAccess,
  106: 					LimitedAccessSolution = LimitedAccessSolution.Bribe,
  107: 					AccessLimitationReason = AccessLimitationReason.ClanTier
  108: 				};
  109: 			}
  110: 			else
  111: 			{
  112: 				accessDetails = new AccessDetails
  113: 				{
  114: 					AccessLevel = AccessLevel.FullAccess,
  115: 					AccessMethod = AccessMethod.Direct
  116: 				};
  117: 			}
  118: 		}
  119: 		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  120: 		{
  121: 			accessDetails = new AccessDetails
  122: 			{
  123: 				AccessLevel = AccessLevel.LimitedAccess,
  124: 				LimitedAccessSolution = LimitedAccessSolution.Disguise,
  125: 				AccessLimitationReason = AccessLimitationReason.Disguised
  126: 			};
  127: 		}
  128: 		if (accessDetails.AccessLevel == AccessLevel.LimitedAccess && (accessDetails.LimitedAccessSolution == LimitedAccessSolution.Bribe || accessDetails.LimitedAccessSolution == LimitedAccessSolution.Disguise) && settlement.LocationComplex.GetListOfCharactersInLocation("lordshall").IsEmpty() && settlement.LocationComplex.GetListOfCharactersInLocation("prison").IsEmpty())
  129: 		{
  130: 			accessDetails.AccessLevel = AccessLevel.NoAccess;
  131: 			accessDetails.AccessLimitationReason = AccessLimitationReason.LocationEmpty;
  132: 		}
  133: 	}
  134: 
  135: 	public override bool CanMainHeroAccessLocation(Settlement settlement, string locationId, out bool disableOption, out TextObject disabledText)
  136: 	{
  137: 		disabledText = null;
  138: 		disableOption = false;
  139: 		bool result = true;
  140: 		switch (locationId)
  141: 		{
  142: 		case "center":
  143: 			result = CanMainHeroWalkAroundTownCenter(settlement, out disableOption, out disabledText);
  144: 			break;
  145: 		case "arena":
  146: 			result = CanMainHeroGoToArena(settlement, out disableOption, out disabledText);
  147: 			break;
  148: 		case "tavern":
  149: 			result = CanMainHeroGoToTavern(settlement, out disableOption, out disabledText);
  150: 			break;
  151: 		case "lordshall":
  152: 		{
  153: 			CanMainHeroEnterLordsHall(settlement, out var accessDetails);
  154: 			result = ((accessDetails.AccessLevel != AccessLevel.LimitedAccess || accessDetails.LimitedAccessSolution != LimitedAccessSolution.Bribe) ? (accessDetails.AccessLevel == AccessLevel.FullAccess) : (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(settlement) == 0));
  155: 			break;
  156: 		}
  157: 		case "prison":
  158: 		{
  159: 			CanMainHeroEnterDungeon(settlement, out var accessDetails2);
  160: 			result = ((accessDetails2.AccessLevel != AccessLevel.LimitedAccess || accessDetails2.LimitedAccessSolution != LimitedAccessSolution.Bribe) ? (accessDetails2.AccessLevel == AccessLevel.FullAccess) : (Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterDungeon(settlement) == 0));
  161: 			break;
  162: 		}
  163: 		case "house_1":
  164: 		case "house_2":
  165: 		case "house_3":
  166: 		{
  167: 			Location locationWithId = settlement.LocationComplex.GetLocationWithId(locationId);
  168: 			result = locationWithId.IsReserved && (locationWithId.SpecialItems.Count > 0 || locationWithId.GetCharacterList().Any());
  169: 			break;
  170: 		}
  171: 		case "port":
  172: 			disableOption = true;
  173: 			disabledText = new TextObject("{=ILnr9eCQ}Door is locked!");
  174: 			result = false;
  175: 			break;
  176: 		default:
  177: 			Debug.FailedAssert("invalid location which is not supported by DefaultSettlementAccessModel", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultSettlementAccessModel.cs", "CanMainHeroAccessLocation", 207);
  178: 			break;
  179: 		}
  180: 		return result;
  181: 	}
  182: 
  183: 	public override bool IsRequestMeetingOptionAvailable(Settlement settlement, out bool disableOption, out TextObject disabledText)
  184: 	{
  185: 		bool result = true;
  186: 		disableOption = false;
  187: 		disabledText = null;
  188: 		CanMainHeroEnterSettlement(settlement, out var accessDetails);
  189: 		if (settlement.OwnerClan == Clan.PlayerClan)
  190: 		{
  191: 			result = false;
  192: 		}
  193: 		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(settlement.MapFaction, Clan.PlayerClan.MapFaction) && accessDetails.AccessLevel == AccessLevel.NoAccess)
  194: 		{
  195: 			result = TownHelpers.IsThereAnyoneToMeetInTown(settlement);
  196: 		}
  197: 		else if (settlement.IsTown && FactionManager.IsNeutralWithFaction(Hero.MainHero.MapFaction, settlement.MapFaction) && Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingMild(settlement.MapFaction))
  198: 		{
  199: 			result = false;
  200: 		}
  201: 		else if (Clan.PlayerClan.Tier < 3)
  202: 		{
  203: 			disableOption = true;
  204: 			disabledText = new TextObject("{=bdzZUVxf}Your clan tier is not high enough to request a meeting.");
  205: 			result = true;
  206: 		}
  207: 		else if (TownHelpers.IsThereAnyoneToMeetInTown(settlement))
  208: 		{
  209: 			result = true;
  210: 		}
  211: 		else
  212: 		{
  213: 			disableOption = true;
  214: 			disabledText = new TextObject("{=196tGVIm}There are no nobles to meet.");
  215: 		}
  216: 		return result;
  217: 	}
  218: 
  219: 	public override bool CanMainHeroDoSettlementAction(Settlement settlement, SettlementAction settlementAction, out bool disableOption, out TextObject disabledText)
  220: 	{

 succeeded in 760ms:
 2388: 	{
 2389: 		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
 2390: 		args.MenuTitle = encounterSettlement.Name;
 2391: 		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
 2392: 		TextObject empty = TextObject.GetEmpty();
 2393: 		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
 2394: 		int num = (int)accessLevel;
 2395: 		if (num != 0)
 2396: 		{
 2397: 			if (num != 1 || _accessDetails.AccessLimitationReason != SettlementAccessModel.AccessLimitationReason.CrimeRating)
 2398: 			{
 2399: 				empty = ((encounterSettlement.OwnerClan != Hero.MainHero.Clan) ? GameTexts.FindText("str_castle_text_1") : GameTexts.FindText("str_castle_text_yours"));
 2400: 			}
 2401: 			else
 2402: 			{
 2403: 				empty.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
 2404: 				empty = GameTexts.FindText("str_gate_down_criminal_text");
 2405: 			}
 2406: 		}
 2407: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.HostileFaction)
 2408: 		{
 2409: 			empty = GameTexts.FindText("str_gate_down_enemy_text_castle");
 2410: 		}
 2411: 		else if (_accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating)
 2412: 		{
 2413: 			empty.SetTextVariable("FACTION", Settlement.CurrentSettlement.MapFaction.Name);
 2414: 			empty = GameTexts.FindText("str_gate_down_criminal_text");
 2415: 		}
 2416: 		else
 2417: 		{
 2418: 			empty = GameTexts.FindText("str_settlement_not_allowed_text");
 2419: 		}
 2420: 		encounterSettlement.OwnerClan.Leader.SetPropertiesToTextObject(empty, "LORD");
 2421: 		empty.SetTextVariable("FACTION_TERM", encounterSettlement.MapFaction.EncyclopediaLinkWithName);
 2422: 		empty.SetTextVariable("SETTLEMENT_NAME", encounterSettlement.EncyclopediaLinkWithName);
 2423: 		MBTextManager.SetTextVariable("TOWN_TEXT", empty);
 2424: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess && (_accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.Direct || (_playerIsAlreadyInCastle && _accessDetails.AccessMethod == SettlementAccessModel.AccessMethod.ByRequest)))
 2425: 		{
 2426: 			GameMenu.SwitchToMenu("castle");
 2427: 		}
 2428: 		else
 2429: 		{
 2430: 			_playerIsAlreadyInCastle = false;

 succeeded in 757ms:
 2490: 	}
 2491: 
 2492: 	private bool game_menu_town_disguise_yourself_on_condition(MenuCallbackArgs args)
 2493: 	{
 2494: 		args.optionLeaveType = GameMenuOption.LeaveType.SneakIn;
 2495: 		MBTextManager.SetTextVariable("SNEAK_CHANCE", MathF.Round(Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) * 100f));
 2496: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
 2497: 		{
 2498: 			return _accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Disguise;
 2499: 		}
 2500: 		return false;
 2501: 	}
 2502: 
 2503: 	private void game_menu_town_initial_disguise_yourself_on_consequence(MenuCallbackArgs args)
 2504: 	{
 2505: 		if (CampaignTime.Now.IsNightTime)
 2506: 		{
 2507: 			GameMenu.SwitchToMenu("disguise_blocked_night_time");
 2508: 		}
 2509: 		else
 2510: 		{
 2511: 			GameMenu.SwitchToMenu(_alreadySneakedSettlements.Contains(Settlement.CurrentSettlement) ? "disguise_not_first_time" : "disguise_first_time");
 2512: 		}
 2513: 	}
 2514: 
 2515: 	private void game_menu_town_disguise_yourself_on_consequence(MenuCallbackArgs args)
 2516: 	{
 2517: 		bool num = Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) > MBRandom.RandomFloat;
 2518: 		SkillLevelingManager.OnMainHeroDisguised(num);
 2519: 		Campaign.Current.IsMainHeroDisguised = true;
 2520: 		if (num)
 2521: 		{
 2522: 			GameMenu.SwitchToMenu("menu_sneak_into_town_succeeded");
 2523: 		}
 2524: 		else
 2525: 		{
 2526: 			GameMenu.SwitchToMenu("menu_sneak_into_town_caught");
 2527: 		}
 2528: 	}
 2611: 	}
 2612: 
 2613: 	private void game_menu_town_outside_enter_on_consequence(MenuCallbackArgs args)
 2614: 	{
 2615: 		GameMenu.SwitchToMenu("town");
 2616: 		PlayerEncounter.LocationEncounter.IsInsideOfASettlement = true;
 2617: 	}
 2751: 	private static bool game_menu_castle_enter_bribe_pay_bribe_on_condition(MenuCallbackArgs args)
 2752: 	{
 2753: 		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
 2754: 		MBTextManager.SetTextVariable("AMOUNT", bribeToEnterLordsHall);
 2755: 		List<Location> locations = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall").ToList();
 2756: 		MenuHelper.SetIssueAndQuestDataForLocations(args, locations);
 2757: 		args.optionLeaveType = GameMenuOption.LeaveType.Mission;
 2758: 		if (Hero.MainHero.Gold < bribeToEnterLordsHall)
 2759: 		{
 2760: 			args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.");
 2761: 			args.IsEnabled = false;
 2762: 		}
 2763: 		return bribeToEnterLordsHall > 0;
 2764: 	}
 2765: 
 2766: 	private void game_menu_castle_enter_bribe_on_consequence(MenuCallbackArgs args)
 2767: 	{
 2768: 		int bribeToEnterLordsHall = Campaign.Current.Models.BribeCalculationModel.GetBribeToEnterLordsHall(Settlement.CurrentSettlement);
 2769: 		BribeGuardsAction.Apply(Settlement.CurrentSettlement, bribeToEnterLordsHall);
 2770: 		_playerIsAlreadyInCastle = true;
 2771: 		GameMenu.SwitchToMenu("menu_castle_entry_granted");

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs -Pattern \"menu_sneak_into_town_succeeded|sneak_into_town_succeeded|disguise_not_first_time|disguise_first_time\" -Context 2,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 709ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:214: 		gameSystemInitializer.AddGameMenu("disguise_first_time", "{=6q7UsTtn}You have no contact in this town, you need to set one up.", first_time_disguise_on_init);
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "continue", "{=WjwHVQzx}Set up contact", launch_mission_on_condition, launch_disguise_mission);
  		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_set", "{=S5OEsjwg}You slip into unconsciousness. After a little while some of the friendlier locals manage to bring you around. A little confused but without any serious injuries, you resolve to be more careful next time.", null);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_set", "continue", "{=veWOovVv}Continue...", continue_on_condition, delegate
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:215: 		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "continue", "{=WjwHVQzx}Set up contact", launch_mission_on_condition, launch_disguise_mission);
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("disguise_first_time", "{=6q7UsTtn}You have no contact in this town, you need to set one up.", first_time_disguise_on_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_set", "{=S5OEsjwg}You slip into unconsciousness. After a little while some of the friendlier locals manage to bring you around. A little confused but without any serious injuries, you resolve to be more careful next time.", null);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_set", "continue", "{=veWOovVv}Continue...", continue_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("disguise_not_first_time");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:216: 		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		gameSystemInitializer.AddGameMenu("disguise_first_time", "{=6q7UsTtn}You have no contact in this town, you need to set one up.", first_time_disguise_on_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_first_time", "continue", "{=WjwHVQzx}Set up contact", launch_mission_on_condition, launch_disguise_mission);
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_set", "{=S5OEsjwg}You slip into unconsciousness. After a little while some of the friendlier locals manage to bring you around. A little confused but without any serious injuries, you resolve to be more careful next time.", null);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_set", "continue", "{=veWOovVv}Continue...", continue_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("disguise_not_first_time");
  		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:223: 			GameMenu.SwitchToMenu("disguise_not_first_time");
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_set", "continue", "{=veWOovVv}Continue...", continue_on_condition, delegate
  		{
  		});
  		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_not_set", "{=KqrkAOY9}You slip into unconsciousness guards find you and throw you in jail.", null);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_not_set", "continue", "{=3nT5wWzb}Surrender", mno_sneak_caught_surrender_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenu("disguise_not_first_time", "{=jqb0q3Gp}You have a contact in this town, you can go about your business disguised.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:227: 		gameSystemInitializer.AddGameMenu("disguise_not_first_time", "{=jqb0q3Gp}You have a contact in this town, you can go about your business disguised.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_not_set", "{=KqrkAOY9}You slip into unconsciousness guards find you and throw you in jail.", null);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_not_set", "continue", "{=3nT5wWzb}Surrender", mno_sneak_caught_surrender_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:228: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_not_set", "continue", "{=3nT5wWzb}Surrender", mno_sneak_caught_surrender_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenu("disguise_not_first_time", "{=jqb0q3Gp}You have a contact in this town, you can go about your business disguised.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:229: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  		gameSystemInitializer.AddGameMenu("disguise_not_first_time", "{=jqb0q3Gp}You have a contact in this town, you can go about your business disguised.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:230: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  		{
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_caught", "{=u7yLV7Vr}As you try to sneak in, one of the guards recognizes you and raises the alarm! Another quickly slams the gate shut behind you, and you have no choice but to give up.", game_menu_sneak_into_town_caught_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:234: 		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  			GameMenu.SwitchToMenu("town_outside");
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_caught", "{=u7yLV7Vr}As you try to sneak in, one of the guards recognizes you and raises the alarm! Another quickly slams the gate shut behind you, and you have no choice but to give up.", game_menu_sneak_into_town_caught_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_caught", "mno_sneak_caught_surrender", "{=3nT5wWzb}Surrender.", mno_sneak_caught_surrender_on_condition, mno_sneak_caught_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_captivity_castle_taken_prisoner", "{=AFJ3BvTH}You are quickly surrounded by guards who take away your weapons. With curses and insults, they throw you into the dungeon where you must while away the miserable days of your captivity.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "mno_sneak_caught_surrender", "{=veWOovVv}Continue...", game_menu_captivity_castle_taken_prisoner_cont_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "cheat_continue", "{=!}Cheat : Leave.", game_menu_captivity_taken_prisoner_cheat_on_condition, game_menu_captivity_taken_prisoner_cheat_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:235: 		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		}, isLeave: true);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_caught", "{=u7yLV7Vr}As you try to sneak in, one of the guards recognizes you and raises the alarm! Another quickly slams the gate shut behind you, and you have no choice but to give up.", game_menu_sneak_into_town_caught_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_caught", "mno_sneak_caught_surrender", "{=3nT5wWzb}Surrender.", mno_sneak_caught_surrender_on_condition, mno_sneak_caught_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_captivity_castle_taken_prisoner", "{=AFJ3BvTH}You are quickly surrounded by guards who take away your weapons. With curses and insults, they throw you into the dungeon where you must while away the miserable days of your captivity.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "mno_sneak_caught_surrender", "{=veWOovVv}Continue...", game_menu_captivity_castle_taken_prisoner_cont_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "cheat_continue", "{=!}Cheat : Leave.", game_menu_captivity_taken_prisoner_cheat_on_condition, game_menu_captivity_taken_prisoner_cheat_on_consequence);
  		gameSystemInitializer.AddGameMenu("fortification_crime_rating", "{=!}{FORTIFICATION_CRIME_RATING_TEXT}", game_menu_fortification_high_crime_rating_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:236: 		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_caught", "{=u7yLV7Vr}As you try to sneak in, one of the guards recognizes you and raises the alarm! Another quickly slams the gate shut behind you, and you have no choice but to give up.", game_menu_sneak_into_town_caught_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_caught", "mno_sneak_caught_surrender", "{=3nT5wWzb}Surrender.", mno_sneak_caught_surrender_on_condition, mno_sneak_caught_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_captivity_castle_taken_prisoner", "{=AFJ3BvTH}You are quickly surrounded by guards who take away your weapons. With curses and insults, they throw you into the dungeon where you must while away the miserable days of your captivity.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "mno_sneak_caught_surrender", "{=veWOovVv}Continue...", game_menu_captivity_castle_taken_prisoner_cont_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "cheat_continue", "{=!}Cheat : Leave.", game_menu_captivity_taken_prisoner_cheat_on_condition, game_menu_captivity_taken_prisoner_cheat_on_consequence);
  		gameSystemInitializer.AddGameMenu("fortification_crime_rating", "{=!}{FORTIFICATION_CRIME_RATING_TEXT}", game_menu_fortification_high_crime_rating_on_init);
  		gameSystemInitializer.AddGameMenuOption("fortification_crime_rating", "fortification_crime_rating_continue", "{=WVkc4UgX}Continue.", game_menu_fortification_high_crime_rating_continue_on_condition, game_menu_fortification_high_crime_rating_continue_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:237: 		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_caught", "{=u7yLV7Vr}As you try to sneak in, one of the guards recognizes you and raises the alarm! Another quickly slams the gate shut behind you, and you have no choice but to give up.", game_menu_sneak_into_town_caught_on_init);
  		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_caught", "mno_sneak_caught_surrender", "{=3nT5wWzb}Surrender.", mno_sneak_caught_surrender_on_condition, mno_sneak_caught_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenu("menu_captivity_castle_taken_prisoner", "{=AFJ3BvTH}You are quickly surrounded by guards who take away your weapons. With curses and insults, they throw you into the dungeon where you must while away the miserable days of your captivity.", null);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "mno_sneak_caught_surrender", "{=veWOovVv}Continue...", game_menu_captivity_castle_taken_prisoner_cont_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("menu_captivity_castle_taken_prisoner", "cheat_continue", "{=!}Cheat : Leave.", game_menu_captivity_taken_prisoner_cheat_on_condition, game_menu_captivity_taken_prisoner_cheat_on_consequence);
  		gameSystemInitializer.AddGameMenu("fortification_crime_rating", "{=!}{FORTIFICATION_CRIME_RATING_TEXT}", game_menu_fortification_high_crime_rating_on_init);
  		gameSystemInitializer.AddGameMenuOption("fortification_crime_rating", "fortification_crime_rating_continue", "{=WVkc4UgX}Continue.", game_menu_fortification_high_crime_rating_continue_on_condition, game_menu_fortification_high_crime_rating_continue_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("army_left_settlement_due_to_war_declaration", "{=!}{ARMY_LEFT_SETTLEMENT_DUE_TO_WAR_TEXT}", game_menu_army_left_settlement_due_to_war_on_init);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:331: 	private void disguise_not_first_time_init(MenuCallbackArgs args)
  	}
  
  	{
  		if (Campaign.Current.GameMenuManager.NextLocation != null)
  		{
  			PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(Campaign.Current.GameMenuManager.NextLocation, Campaign.Current.GameMenuManager.PreviousLocation);
  			Campaign.Current.GameMenuManager.NextLocation = null;
  			Campaign.Current.GameMenuManager.PreviousLocation = null;
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:357: 			GameMenu.SwitchToMenu("disguise_not_first_time");
  		if (_alreadySneakedSettlements.Contains(Settlement.CurrentSettlement))
  		{
  		}
  		else if (Campaign.Current.GameMenuManager.NextLocation != null)
  		{
  			PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(Campaign.Current.GameMenuManager.NextLocation, Campaign.Current.GameMenuManager.PreviousLocation);
  			Campaign.Current.GameMenuManager.NextLocation = null;
  			Campaign.Current.GameMenuManager.PreviousLocation = null;
  		}
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1344: 	public static void menu_sneak_into_town_succeeded_continue_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		GameMenu.SwitchToMenu("town");
  	}
  
  	public static bool menu_sneak_into_town_succeeded_continue_on_condition(MenuCallbackArgs args)
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Continue;
  		return true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1349: 	public static bool menu_sneak_into_town_succeeded_continue_on_condition(MenuCallbackArgs args)
  	}
  
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Continue;
  		return true;
  	}
  
  	public static void game_menu_sneak_into_town_caught_on_init(MenuCallbackArgs args)
  	{
  		ChangeCrimeRatingAction.Apply(Settlement.CurrentSettlement.MapFaction, 10f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2511: 			GameMenu.SwitchToMenu(_alreadySneakedSettlements.Contains(Settlement.CurrentSettlement) ? "disguise_not_first_time" : "disguise_first_time");
  		else
  		{
  		}
  	}
  
  	private void game_menu_town_disguise_yourself_on_consequence(MenuCallbackArgs args)
  	{
  		bool num = Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) > MBRandom.RandomFloat;
  		SkillLevelingManager.OnMainHeroDisguised(num);
  		Campaign.Current.IsMainHeroDisguised = true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2522: 			GameMenu.SwitchToMenu("menu_sneak_into_town_succeeded");
  		if (num)
  		{
  		}
  		else
  		{
  			GameMenu.SwitchToMenu("menu_sneak_into_town_caught");
  		}
  	}
  
  	private bool game_menu_town_town_besiege_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3221: 	[GameMenuInitializationHandler("menu_sneak_into_town_succeeded")]
  	[GameMenuInitializationHandler("fortification_crime_rating")]
  	[GameMenuInitializationHandler("village_outside")]
  	[GameMenuInitializationHandler("disguise_first_time")]
  	[GameMenuInitializationHandler("disguise_not_first_time")]
  	private static void encounter_menu_ui_castle_on_init(MenuCallbackArgs args)
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		args.MenuContext.SetBackgroundMeshName(currentSettlement.SettlementComponent.WaitMeshName);
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3222: 	[GameMenuInitializationHandler("disguise_first_time")]
  	[GameMenuInitializationHandler("village_outside")]
  	[GameMenuInitializationHandler("menu_sneak_into_town_succeeded")]
  	[GameMenuInitializationHandler("disguise_not_first_time")]
  	private static void encounter_menu_ui_castle_on_init(MenuCallbackArgs args)
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		args.MenuContext.SetBackgroundMeshName(currentSettlement.SettlementComponent.WaitMeshName);
  	}
  
  	[GameMenuInitializationHandler("menu_castle_taken")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3223: 	[GameMenuInitializationHandler("disguise_not_first_time")]
  	[GameMenuInitializationHandler("menu_sneak_into_town_succeeded")]
  	[GameMenuInitializationHandler("disguise_first_time")]
  	private static void encounter_menu_ui_castle_on_init(MenuCallbackArgs args)
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		args.MenuContext.SetBackgroundMeshName(currentSettlement.SettlementComponent.WaitMeshName);
  	}
  
  	[GameMenuInitializationHandler("menu_castle_taken")]
  	[GameMenuInitializationHandler("menu_settlement_taken")]

codex
I found one concrete challenge to a conscious call: v1.4.6 does have a hostile-town entry route through disguise. A successful sneak-in reaches the `town` menu, where Elite Emissary currently has no war/relation check, so this is likely a real finding rather than a theoretical concern.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementAccessModel.cs -Pattern \"CanMainHeroEnterTown|CanMainHeroEnterCastle|CanMainHeroEnterVillage\" -Context 2,60 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 544ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:28: 			CanMainHeroEnterTown(settlement, out accessDetails);
  		else if (settlement.IsTown)
  		{
  		}
  		else if (settlement.IsCastle)
  		{
  			CanMainHeroEnterCastle(settlement, out accessDetails);
  		}
  		else if (settlement.IsVillage)
  		{
  			CanMainHeroEnterVillage(settlement, out accessDetails);
  		}
  		else
  		{
  			Debug.FailedAssert("Invalid type of settlement", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultSettlementAccessModel.cs", "CanMainHeroEnterSettlement", 42);
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  	}
  
  	public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	private void CanMainHeroEnterKeepInternal(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		Hero mainHero = Hero.MainHero;
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			if (Campaign.Current.IsMainHeroDisguised)
  			{
  				accessDetails = new AccessDetails
  				{
  					AccessLevel = AccessLevel.LimitedAccess,
  					LimitedAccessSolution = LimitedAccessSolution.Disguise,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:32: 			CanMainHeroEnterCastle(settlement, out accessDetails);
  		else if (settlement.IsCastle)
  		{
  		}
  		else if (settlement.IsVillage)
  		{
  			CanMainHeroEnterVillage(settlement, out accessDetails);
  		}
  		else
  		{
  			Debug.FailedAssert("Invalid type of settlement", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultSettlementAccessModel.cs", "CanMainHeroEnterSettlement", 42);
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  	}
  
  	public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	private void CanMainHeroEnterKeepInternal(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		Hero mainHero = Hero.MainHero;
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			if (Campaign.Current.IsMainHeroDisguised)
  			{
  				accessDetails = new AccessDetails
  				{
  					AccessLevel = AccessLevel.LimitedAccess,
  					LimitedAccessSolution = LimitedAccessSolution.Disguise,
  					AccessLimitationReason = AccessLimitationReason.Disguised
  				};
  			}
  			else if (Campaign.Current.Models.CrimeModel.DoesPlayerHaveAnyCrimeRating(settlement.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:36: 			CanMainHeroEnterVillage(settlement, out accessDetails);
  		else if (settlement.IsVillage)
  		{
  		}
  		else
  		{
  			Debug.FailedAssert("Invalid type of settlement", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultSettlementAccessModel.cs", "CanMainHeroEnterSettlement", 42);
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  	}
  
  	public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		CanMainHeroEnterKeepInternal(settlement, out accessDetails);
  	}
  
  	private void CanMainHeroEnterKeepInternal(Settlement settlement, out AccessDetails accessDetails)
  	{
  		accessDetails = default(AccessDetails);
  		Hero mainHero = Hero.MainHero;
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			if (Campaign.Current.IsMainHeroDisguised)
  			{
  				accessDetails = new AccessDetails
  				{
  					AccessLevel = AccessLevel.LimitedAccess,
  					LimitedAccessSolution = LimitedAccessSolution.Disguise,
  					AccessLimitationReason = AccessLimitationReason.Disguised
  				};
  			}
  			else if (Campaign.Current.Models.CrimeModel.DoesPlayerHaveAnyCrimeRating(settlement.MapFaction))
  			{
  				accessDetails = new AccessDetails
  				{
  					AccessLevel = AccessLevel.LimitedAccess,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:280: 	private void CanMainHeroEnterVillage(Settlement settlement, out AccessDetails accessDetails)
  	}
  
  	{
  		Hero mainHero = Hero.MainHero;
  		accessDetails = new AccessDetails
  		{
  			AccessLevel = AccessLevel.NoAccess,
  			AccessLimitationReason = AccessLimitationReason.None,
  			PreliminaryActionObligation = PreliminaryActionObligation.None,
  			PreliminaryActionType = PreliminaryActionType.None
  		};
  		MobileParty partyBelongedTo = mainHero.PartyBelongedTo;
  		if (partyBelongedTo != null && (partyBelongedTo.Army == null || partyBelongedTo.Army.LeaderParty == partyBelongedTo))
  		{
  			accessDetails.AccessLevel = AccessLevel.FullAccess;
  			accessDetails.AccessMethod = AccessMethod.Direct;
  		}
  		if (settlement.Village.VillageState == Village.VillageStates.Looted)
  		{
  			accessDetails.AccessLevel = AccessLevel.NoAccess;
  			accessDetails.AccessLimitationReason = AccessLimitationReason.VillageIsLooted;
  		}
  	}
  
  	private bool CanMainHeroManageTown(Settlement settlement, out bool disableOption, out TextObject disabledText)
  	{
  		disabledText = null;
  		disableOption = false;
  		if (settlement.IsTown)
  		{
  			return settlement.OwnerClan.Leader == Hero.MainHero;
  		}
  		return false;
  	}
  
  	private void CanMainHeroEnterCastle(Settlement settlement, out AccessDetails accessDetails)
  	{
  		Hero mainHero = Hero.MainHero;
  		accessDetails = default(AccessDetails);
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.ByRequest
  			};
  			if (!settlement.Town.IsOwnerUnassigned && settlement.OwnerClan.Leader.GetRelationWithPlayer() < -4f && Hero.MainHero.MapFaction.Leader != Hero.MainHero)
  			{
  				accessDetails.AccessLevel = AccessLevel.NoAccess;
  				accessDetails.AccessLimitationReason = AccessLimitationReason.RelationshipWithOwner;
  			}
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:314: 	private void CanMainHeroEnterCastle(Settlement settlement, out AccessDetails accessDetails)
  	}
  
  	{
  		Hero mainHero = Hero.MainHero;
  		accessDetails = default(AccessDetails);
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.ByRequest
  			};
  			if (!settlement.Town.IsOwnerUnassigned && settlement.OwnerClan.Leader.GetRelationWithPlayer() < -4f && Hero.MainHero.MapFaction.Leader != Hero.MainHero)
  			{
  				accessDetails.AccessLevel = AccessLevel.NoAccess;
  				accessDetails.AccessLimitationReason = AccessLimitationReason.RelationshipWithOwner;
  			}
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.ByRequest
  			};
  			if (Campaign.Current.Models.CrimeModel.DoesPlayerHaveAnyCrimeRating(settlement.MapFaction))
  			{
  				accessDetails.AccessLevel = AccessLevel.NoAccess;
  				accessDetails.AccessLimitationReason = AccessLimitationReason.CrimeRating;
  			}
  			else if (settlement.OwnerClan.Leader.GetRelationWithPlayer() < 0f)
  			{
  				accessDetails.AccessLevel = AccessLevel.NoAccess;
  				accessDetails.AccessLimitationReason = AccessLimitationReason.RelationshipWithOwner;
  			}
  		}
  		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.NoAccess,
  				AccessMethod = AccessMethod.ByRequest,
  				AccessLimitationReason = AccessLimitationReason.HostileFaction
  			};
  		}
  	}
  
  	private void CanMainHeroEnterTown(Settlement settlement, out AccessDetails accessDetails)
  	{
  		Hero mainHero = Hero.MainHero;
  		accessDetails = default(AccessDetails);
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementAccessModel.cs:368: 	private void CanMainHeroEnterTown(Settlement settlement, out AccessDetails accessDetails)
  	}
  
  	{
  		Hero mainHero = Hero.MainHero;
  		accessDetails = default(AccessDetails);
  		if (settlement.OwnerClan == mainHero.Clan)
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  		}
  		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  			{
  				accessDetails.PreliminaryActionType = PreliminaryActionType.FaceCharges;
  				accessDetails.PreliminaryActionObligation = PreliminaryActionObligation.Optional;
  			}
  		}
  		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.FullAccess,
  				AccessMethod = AccessMethod.Direct
  			};
  			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  			{
  				accessDetails.AccessLevel = AccessLevel.LimitedAccess;
  				accessDetails.AccessMethod = AccessMethod.None;
  				accessDetails.LimitedAccessSolution = LimitedAccessSolution.Disguise;
  				accessDetails.AccessLimitationReason = AccessLimitationReason.CrimeRating;
  			}
  		}
  		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  		{
  			accessDetails = new AccessDetails
  			{
  				AccessLevel = AccessLevel.LimitedAccess,
  				LimitedAccessSolution = LimitedAccessSolution.Disguise,
  				AccessLimitationReason = AccessLimitationReason.HostileFaction
  			};
  		}
  	}
  
  	private bool CanMainHeroWalkAroundTownCenter(Settlement settlement, out bool disableOption, out TextObject disabledText)
  	{
  		disabledText = null;
  		disableOption = false;
  		if (!settlement.IsTown)
  		{
  			return settlement.IsCastle;
  		}
  		return true;
  	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementAccessModel.cs'; "'$lines=Get-Content $p; for($i=314;$i -le 363;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=368;$i -le 408;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 622ms:
  314: 	private void CanMainHeroEnterCastle(Settlement settlement, out AccessDetails accessDetails)
  315: 	{
  316: 		Hero mainHero = Hero.MainHero;
  317: 		accessDetails = default(AccessDetails);
  318: 		if (settlement.OwnerClan == mainHero.Clan)
  319: 		{
  320: 			accessDetails = new AccessDetails
  321: 			{
  322: 				AccessLevel = AccessLevel.FullAccess,
  323: 				AccessMethod = AccessMethod.Direct
  324: 			};
  325: 		}
  326: 		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  327: 		{
  328: 			accessDetails = new AccessDetails
  329: 			{
  330: 				AccessLevel = AccessLevel.FullAccess,
  331: 				AccessMethod = AccessMethod.ByRequest
  332: 			};
  333: 			if (!settlement.Town.IsOwnerUnassigned && settlement.OwnerClan.Leader.GetRelationWithPlayer() < -4f && Hero.MainHero.MapFaction.Leader != Hero.MainHero)
  334: 			{
  335: 				accessDetails.AccessLevel = AccessLevel.NoAccess;
  336: 				accessDetails.AccessLimitationReason = AccessLimitationReason.RelationshipWithOwner;
  337: 			}
  338: 		}
  339: 		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  340: 		{
  341: 			accessDetails = new AccessDetails
  342: 			{
  343: 				AccessLevel = AccessLevel.FullAccess,
  344: 				AccessMethod = AccessMethod.ByRequest
  345: 			};
  346: 			if (Campaign.Current.Models.CrimeModel.DoesPlayerHaveAnyCrimeRating(settlement.MapFaction))
  347: 			{
  348: 				accessDetails.AccessLevel = AccessLevel.NoAccess;
  349: 				accessDetails.AccessLimitationReason = AccessLimitationReason.CrimeRating;
  350: 			}
  351: 			else if (settlement.OwnerClan.Leader.GetRelationWithPlayer() < 0f)
  352: 			{
  353: 				accessDetails.AccessLevel = AccessLevel.NoAccess;
  354: 				accessDetails.AccessLimitationReason = AccessLimitationReason.RelationshipWithOwner;
  355: 			}
  356: 		}
  357: 		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  358: 		{
  359: 			accessDetails = new AccessDetails
  360: 			{
  361: 				AccessLevel = AccessLevel.NoAccess,
  362: 				AccessMethod = AccessMethod.ByRequest,
  363: 				AccessLimitationReason = AccessLimitationReason.HostileFaction
  368: 	private void CanMainHeroEnterTown(Settlement settlement, out AccessDetails accessDetails)
  369: 	{
  370: 		Hero mainHero = Hero.MainHero;
  371: 		accessDetails = default(AccessDetails);
  372: 		if (settlement.OwnerClan == mainHero.Clan)
  373: 		{
  374: 			accessDetails = new AccessDetails
  375: 			{
  376: 				AccessLevel = AccessLevel.FullAccess,
  377: 				AccessMethod = AccessMethod.Direct
  378: 			};
  379: 		}
  380: 		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  381: 		{
  382: 			accessDetails = new AccessDetails
  383: 			{
  384: 				AccessLevel = AccessLevel.FullAccess,
  385: 				AccessMethod = AccessMethod.Direct
  386: 			};
  387: 			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  388: 			{
  389: 				accessDetails.PreliminaryActionType = PreliminaryActionType.FaceCharges;
  390: 				accessDetails.PreliminaryActionObligation = PreliminaryActionObligation.Optional;
  391: 			}
  392: 		}
  393: 		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  394: 		{
  395: 			accessDetails = new AccessDetails
  396: 			{
  397: 				AccessLevel = AccessLevel.FullAccess,
  398: 				AccessMethod = AccessMethod.Direct
  399: 			};
  400: 			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  401: 			{
  402: 				accessDetails.AccessLevel = AccessLevel.LimitedAccess;
  403: 				accessDetails.AccessMethod = AccessMethod.None;
  404: 				accessDetails.LimitedAccessSolution = LimitedAccessSolution.Disguise;
  405: 				accessDetails.AccessLimitationReason = AccessLimitationReason.CrimeRating;
  406: 			}
  407: 		}
  408: 		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementAccessModel.cs'; "'$lines=Get-Content $p; for($i=408;$i -le 417;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 567ms:
  408: 		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  409: 		{
  410: 			accessDetails = new AccessDetails
  411: 			{
  412: 				AccessLevel = AccessLevel.LimitedAccess,
  413: 				LimitedAccessSolution = LimitedAccessSolution.Disguise,
  414: 				AccessLimitationReason = AccessLimitationReason.HostileFaction
  415: 			};
  416: 		}
  417: 	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $p; for($i=223;$i -le 237;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=1338;$i -le 1352;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 543ms:
  223: 			GameMenu.SwitchToMenu("disguise_not_first_time");
  224: 		});
  225: 		gameSystemInitializer.AddGameMenu("settlement_player_unconscious_when_disguise_contact_not_set", "{=KqrkAOY9}You slip into unconsciousness guards find you and throw you in jail.", null);
  226: 		gameSystemInitializer.AddGameMenuOption("settlement_player_unconscious_when_disguise_contact_not_set", "continue", "{=3nT5wWzb}Surrender", mno_sneak_caught_surrender_on_condition, game_menu_captivity_castle_taken_prisoner_cont_on_consequence);
  227: 		gameSystemInitializer.AddGameMenu("disguise_not_first_time", "{=jqb0q3Gp}You have a contact in this town, you can go about your business disguised.", disguise_not_first_time_init);
  228: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "quick_sneak", "{=hPmawJUs}Sneak in as quickly as you can ({SNEAK_CHANCE}%)", game_menu_town_disguise_yourself_on_condition, game_menu_town_disguise_yourself_on_consequence);
  229: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "take_a_walk", "{=iHLBzWSI}Take a walk around the town disguised", launch_mission_on_condition, launch_disguise_mission);
  230: 		gameSystemInitializer.AddGameMenuOption("disguise_not_first_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate
  231: 		{
  232: 			GameMenu.SwitchToMenu("town_outside");
  233: 		}, isLeave: true);
  234: 		gameSystemInitializer.AddGameMenu("settlement_player_run_away_when_disguise", "{=WJyTrMf4}You manage to escape the town before getting caught somehow.", disguise_not_first_time_init);
  235: 		gameSystemInitializer.AddGameMenuOption("settlement_player_run_away_when_disguise", "continue_back", "{=veWOovVv}Continue...", menu_sneak_into_town_succeeded_continue_on_condition, escape_continue_on_consequence);
  236: 		gameSystemInitializer.AddGameMenu("menu_sneak_into_town_succeeded", "{=pSSDfAjR}Disguised in the garments of a poor pilgrim, you fool the guards and make your way into the town.", null);
  237: 		gameSystemInitializer.AddGameMenuOption("menu_sneak_into_town_succeeded", "str_continue", "{=DM6luo3c}Continue", menu_sneak_into_town_succeeded_continue_on_condition, menu_sneak_into_town_succeeded_continue_on_consequence);
 1338: 
 1339: 	private void game_menu_encounter_interrupted_leave_on_consequence(MenuCallbackArgs args)
 1340: 	{
 1341: 		PlayerEncounter.Finish();
 1342: 	}
 1343: 
 1344: 	public static void menu_sneak_into_town_succeeded_continue_on_consequence(MenuCallbackArgs args)
 1345: 	{
 1346: 		GameMenu.SwitchToMenu("town");
 1347: 	}
 1348: 
 1349: 	public static bool menu_sneak_into_town_succeeded_continue_on_condition(MenuCallbackArgs args)
 1350: 	{
 1351: 		args.optionLeaveType = GameMenuOption.LeaveType.Continue;
 1352: 		return true;

codex
There is another wrinkle: vanilla `CanMainHeroEnterVillage` has no faction/war check at all, but the encounter menu may route hostile villages through raid/encounter states first. I’m tracing that separately so I don’t overstate village reachability.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs -Pattern \"VillageOutsideOnInit|village_outside|SwitchToMenu\\(\\\"village\\\"|EnterSettlement\\(\\).*village|village_on\" -Context 2,12 | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
2026-06-25T15:34:18.724424Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path E:\Decompiled_Bannerlord\Campaign\TaleWorlds.Camp[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'village\|EnterSettlement\(\).*village|village_on'.[0m

 exited 1 in 615ms:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path E:\Decompiled_Bannerlord\Campaign\TaleWorlds.Camp[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'village\|EnterSettlement\(\).*village|village_on'.[0m

codex
The search command hit PowerShell quoting on the regex, not a code issue. I’m rerunning it with a simpler pattern set.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs -Pattern 'VillageOutsideOnInit','village_outside','SwitchToMenu(\"village\"','game_menu_village' -Context 2,10 | Select-Object -First 100 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
2026-06-25T15:34:25.777728Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path E:\Decompiled_Bannerlord\Campaign\TaleWorlds.Camp[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe string SwitchToMenu("village" is not a valid regular expression: Invalid pattern 'SwitchToMenu("village"' at offset 22. Not enough )'s.[0m

 exited 1 in 606ms:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path E:\Decompiled_Bannerlord\Campaign\TaleWorlds.Camp[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe string SwitchToMenu("village" is not a valid regular expression: Invalid pattern 'SwitchToMenu("village"' at offset 22. Not enough )'s.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs -Pattern 'VillageOutsideOnInit','village_outside','SwitchToMenu(\"village\")','game_menu_village' -SimpleMatch -Context 2,10 | Select-Object -First 100 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PreContext | ForEach-Object { '"'  '+"'$_ }; $_.Context.PostContext | ForEach-Object { '"'  '+"'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 560ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:170: 		gameSystemInitializer.AddGameMenuOption("encounter", "village_raid_action", "{=lvttCRi8}Plunder the village, then raze it.", game_menu_village_hostile_action_on_condition, game_menu_village_raid_no_resist_on_consequence);
  		gameSystemInitializer.AddGameMenu("encounter", "{=!}{ENCOUNTER_TEXT}", game_menu_encounter_on_init, GameMenu.MenuOverlayType.Encounter);
  		gameSystemInitializer.AddGameMenuOption("encounter", "continue_preparations", "{=FOoMM4AU}Continue siege preparations.", game_menu_town_besiege_continue_siege_on_condition, game_menu_town_besiege_continue_siege_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_volunteer_action", "{=9YHjPkb8}Force notables to give you recruits.", game_menu_village_hostile_action_on_condition, game_menu_village_force_volunteers_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_supplies_action", "{=JMzyh6Gl}Force people to give you supplies.", game_menu_village_hostile_action_on_condition, game_menu_village_force_supplies_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "attack", "{=o1pZHZOF}{ATTACK_TEXT}!", game_menu_encounter_attack_on_condition, game_menu_encounter_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "capture_the_enemy", "{=27yneDGL}Capture the enemy.", game_menu_encounter_capture_the_enemy_on_condition, game_menu_capture_the_enemy_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "str_order_attack", "{=!}{SEND_TROOPS_TEXT}", game_menu_encounter_order_attack_on_condition, game_menu_encounter_order_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "leave_soldiers_behind", "{=qNgGoqmI}Try to get away.", game_menu_encounter_leave_your_soldiers_behind_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("try_to_get_away");
  		});
  		gameSystemInitializer.AddGameMenuOption("encounter", "surrender", "{=3nT5wWzb}Surrender.", game_menu_encounter_surrender_on_condition, game_menu_encounter_surrender_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:171: 		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_volunteer_action", "{=9YHjPkb8}Force notables to give you recruits.", game_menu_village_hostile_action_on_condition, game_menu_village_force_volunteers_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "continue_preparations", "{=FOoMM4AU}Continue siege preparations.", game_menu_town_besiege_continue_siege_on_condition, game_menu_town_besiege_continue_siege_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_raid_action", "{=lvttCRi8}Plunder the village, then raze it.", game_menu_village_hostile_action_on_condition, game_menu_village_raid_no_resist_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_supplies_action", "{=JMzyh6Gl}Force people to give you supplies.", game_menu_village_hostile_action_on_condition, game_menu_village_force_supplies_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "attack", "{=o1pZHZOF}{ATTACK_TEXT}!", game_menu_encounter_attack_on_condition, game_menu_encounter_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "capture_the_enemy", "{=27yneDGL}Capture the enemy.", game_menu_encounter_capture_the_enemy_on_condition, game_menu_capture_the_enemy_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "str_order_attack", "{=!}{SEND_TROOPS_TEXT}", game_menu_encounter_order_attack_on_condition, game_menu_encounter_order_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "leave_soldiers_behind", "{=qNgGoqmI}Try to get away.", game_menu_encounter_leave_your_soldiers_behind_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("try_to_get_away");
  		});
  		gameSystemInitializer.AddGameMenuOption("encounter", "surrender", "{=3nT5wWzb}Surrender.", game_menu_encounter_surrender_on_condition, game_menu_encounter_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "leave", "{=2YYRyrOO}Leave...", game_menu_encounter_leave_on_condition, game_menu_encounter_leave_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:172: 		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_supplies_action", "{=JMzyh6Gl}Force people to give you supplies.", game_menu_village_hostile_action_on_condition, game_menu_village_force_supplies_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_raid_action", "{=lvttCRi8}Plunder the village, then raze it.", game_menu_village_hostile_action_on_condition, game_menu_village_raid_no_resist_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "village_force_volunteer_action", "{=9YHjPkb8}Force notables to give you recruits.", game_menu_village_hostile_action_on_condition, game_menu_village_force_volunteers_no_resist_loot_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "attack", "{=o1pZHZOF}{ATTACK_TEXT}!", game_menu_encounter_attack_on_condition, game_menu_encounter_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "capture_the_enemy", "{=27yneDGL}Capture the enemy.", game_menu_encounter_capture_the_enemy_on_condition, game_menu_capture_the_enemy_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "str_order_attack", "{=!}{SEND_TROOPS_TEXT}", game_menu_encounter_order_attack_on_condition, game_menu_encounter_order_attack_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "leave_soldiers_behind", "{=qNgGoqmI}Try to get away.", game_menu_encounter_leave_your_soldiers_behind_on_condition, delegate
  		{
  			GameMenu.SwitchToMenu("try_to_get_away");
  		});
  		gameSystemInitializer.AddGameMenuOption("encounter", "surrender", "{=3nT5wWzb}Surrender.", game_menu_encounter_surrender_on_condition, game_menu_encounter_surrender_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter", "leave", "{=2YYRyrOO}Leave...", game_menu_encounter_leave_on_condition, game_menu_encounter_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenuOption("encounter", "abandon_army", "{=Nr49hlfC}Abandon army.", game_menu_encounter_abandon_army_on_condition, game_menu_encounter_abandon_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:277: 		gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
  		gameSystemInitializer.AddGameMenuOption("request_meeting_with_besiegers", "request_meeting_town_leave", "{=3nbuRBJK}Forget it.", game_meeting_town_leave_on_condition, game_menu_request_meeting_town_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenuOption("request_meeting_with_besiegers", "request_meeting_castle_leave", "{=3nbuRBJK}Forget it.", game_meeting_castle_leave_on_condition, game_menu_request_meeting_castle_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("village_loot_complete", "{=qt5bkw8l}On your orders your troops sack the village, pillaging everything of any value, and then put the buildings to the torch. From the coins and valuables that are found, you get your share.", game_menu_village_loot_complete_on_init);
  		gameSystemInitializer.AddGameMenuOption("village_loot_complete", "continue", "{=veWOovVv}Continue...", game_menu_village_loot_complete_continue_on_condition, game_menu_village_loot_complete_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("raid_interrupted", "{=KW7amS8c}While your troops are pillaging the countryside, you receive news that the enemy is approaching. You quickly gather up your soldiers and prepare for battle.", null);
  		gameSystemInitializer.AddGameMenuOption("raid_interrupted", "continue", "{=veWOovVv}Continue...", game_menu_raid_interrupted_continue_on_condition, game_menu_raid_interrupted_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted", "{=lKWflUid}While you are waiting in {DEFENDER}, {ATTACKER} started an attack on it.", game_menu_encounter_interrupted_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_attackers", "{=h3yEHb4U}Help {ATTACKER}.", game_menu_join_encounter_help_attackers_on_condition, game_menu_join_encounter_help_attackers_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_defenders", "{=FwIgakj8}Help {DEFENDER}.", game_menu_join_encounter_help_defenders_on_condition, game_menu_join_encounter_help_defenders_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "leave", "{=UgfmaQgx}Leave {DEFENDER}", game_menu_encounter_interrupted_leave_on_condition, game_menu_encounter_interrupted_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted_siege_preparations", "{=ABeCWcLi}While you are resting, you hear news that a force led by {ATTACKER} has arrived outside the walls of {DEFENDER} and is beginning preparations for a siege.", game_menu_encounter_interrupted_siege_preparations_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_join_defend", "{=Lxx97yNh}Join the defense of {SETTLEMENT}", game_menu_encounter_interrupted_siege_preparations_join_defend_on_condition, game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:278: 		gameSystemInitializer.AddGameMenu("village_loot_complete", "{=qt5bkw8l}On your orders your troops sack the village, pillaging everything of any value, and then put the buildings to the torch. From the coins and valuables that are found, you get your share.", game_menu_village_loot_complete_on_init);
  		gameSystemInitializer.AddGameMenuOption("request_meeting_with_besiegers", "request_meeting_castle_leave", "{=3nbuRBJK}Forget it.", game_meeting_castle_leave_on_condition, game_menu_request_meeting_castle_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
  		gameSystemInitializer.AddGameMenuOption("village_loot_complete", "continue", "{=veWOovVv}Continue...", game_menu_village_loot_complete_continue_on_condition, game_menu_village_loot_complete_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("raid_interrupted", "{=KW7amS8c}While your troops are pillaging the countryside, you receive news that the enemy is approaching. You quickly gather up your soldiers and prepare for battle.", null);
  		gameSystemInitializer.AddGameMenuOption("raid_interrupted", "continue", "{=veWOovVv}Continue...", game_menu_raid_interrupted_continue_on_condition, game_menu_raid_interrupted_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted", "{=lKWflUid}While you are waiting in {DEFENDER}, {ATTACKER} started an attack on it.", game_menu_encounter_interrupted_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_attackers", "{=h3yEHb4U}Help {ATTACKER}.", game_menu_join_encounter_help_attackers_on_condition, game_menu_join_encounter_help_attackers_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_defenders", "{=FwIgakj8}Help {DEFENDER}.", game_menu_join_encounter_help_defenders_on_condition, game_menu_join_encounter_help_defenders_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "leave", "{=UgfmaQgx}Leave {DEFENDER}", game_menu_encounter_interrupted_leave_on_condition, game_menu_encounter_interrupted_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted_siege_preparations", "{=ABeCWcLi}While you are resting, you hear news that a force led by {ATTACKER} has arrived outside the walls of {DEFENDER} and is beginning preparations for a siege.", game_menu_encounter_interrupted_siege_preparations_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_join_defend", "{=Lxx97yNh}Join the defense of {SETTLEMENT}", game_menu_encounter_interrupted_siege_preparations_join_defend_on_condition, game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_break_out_of_town", "{=ybzBF59f}Break out of {SETTLEMENT}.", game_menu_encounter_interrupted_siege_preparations_break_out_of_town_on_condition, game_menu_encounter_interrupted_break_out_of_town_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:279: 		gameSystemInitializer.AddGameMenuOption("village_loot_complete", "continue", "{=veWOovVv}Continue...", game_menu_village_loot_complete_continue_on_condition, game_menu_village_loot_complete_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
  		gameSystemInitializer.AddGameMenu("village_loot_complete", "{=qt5bkw8l}On your orders your troops sack the village, pillaging everything of any value, and then put the buildings to the torch. From the coins and valuables that are found, you get your share.", game_menu_village_loot_complete_on_init);
  		gameSystemInitializer.AddGameMenu("raid_interrupted", "{=KW7amS8c}While your troops are pillaging the countryside, you receive news that the enemy is approaching. You quickly gather up your soldiers and prepare for battle.", null);
  		gameSystemInitializer.AddGameMenuOption("raid_interrupted", "continue", "{=veWOovVv}Continue...", game_menu_raid_interrupted_continue_on_condition, game_menu_raid_interrupted_continue_on_consequence);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted", "{=lKWflUid}While you are waiting in {DEFENDER}, {ATTACKER} started an attack on it.", game_menu_encounter_interrupted_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_attackers", "{=h3yEHb4U}Help {ATTACKER}.", game_menu_join_encounter_help_attackers_on_condition, game_menu_join_encounter_help_attackers_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "encounter_interrupted_help_defenders", "{=FwIgakj8}Help {DEFENDER}.", game_menu_join_encounter_help_defenders_on_condition, game_menu_join_encounter_help_defenders_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted", "leave", "{=UgfmaQgx}Leave {DEFENDER}", game_menu_encounter_interrupted_leave_on_condition, game_menu_encounter_interrupted_leave_on_consequence, isLeave: true);
  		gameSystemInitializer.AddGameMenu("encounter_interrupted_siege_preparations", "{=ABeCWcLi}While you are resting, you hear news that a force led by {ATTACKER} has arrived outside the walls of {DEFENDER} and is beginning preparations for a siege.", game_menu_encounter_interrupted_siege_preparations_on_init);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_join_defend", "{=Lxx97yNh}Join the defense of {SETTLEMENT}", game_menu_encounter_interrupted_siege_preparations_join_defend_on_condition, game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_break_out_of_town", "{=ybzBF59f}Break out of {SETTLEMENT}.", game_menu_encounter_interrupted_siege_preparations_break_out_of_town_on_condition, game_menu_encounter_interrupted_break_out_of_town_on_consequence);
  		gameSystemInitializer.AddGameMenuOption("encounter_interrupted_siege_preparations", "encounter_interrupted_siege_preparations_leave_town", "{=FILG5eZD}Leave {SETTLEMENT}.", game_menu_encounter_interrupted_siege_preparations_leave_town_on_condition, game_menu_encounter_interrupted_leave_on_consequence, isLeave: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1485: 				game_menu_village_raid_no_resist_on_consequence(args);
  			if (battle.IsRaid)
  			{
  			}
  			else if (battle.IsForcingSupplies)
  			{
  				game_menu_village_force_supplies_no_resist_loot_on_consequence(args);
  			}
  			else if (battle.IsForcingVolunteers)
  			{
  				game_menu_village_force_volunteers_no_resist_loot_on_consequence(args);
  			}
  		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1489: 				game_menu_village_force_supplies_no_resist_loot_on_consequence(args);
  			else if (battle.IsForcingSupplies)
  			{
  			}
  			else if (battle.IsForcingVolunteers)
  			{
  				game_menu_village_force_volunteers_no_resist_loot_on_consequence(args);
  			}
  		}
  		else if (!battle.AttackerSide.MapFaction.IsAtWarWith(battle.DefenderSide.MapFaction))
  		{
  			Debug.FailedAssert("This case should not be happening anymore, check this case and make sure this is intended", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\EncounterGameMenuBehavior.cs", "UpdateVillageHostileActionEncounter", 1754);
  		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1493: 				game_menu_village_force_volunteers_no_resist_loot_on_consequence(args);
  			else if (battle.IsForcingVolunteers)
  			{
  			}
  		}
  		else if (!battle.AttackerSide.MapFaction.IsAtWarWith(battle.DefenderSide.MapFaction))
  		{
  			Debug.FailedAssert("This case should not be happening anymore, check this case and make sure this is intended", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\EncounterGameMenuBehavior.cs", "UpdateVillageHostileActionEncounter", 1754);
  		}
  	}
  
  	public static bool game_menu_captivity_taken_prisoner_cheat_on_condition(MenuCallbackArgs args)
  	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2204: 	private bool game_menu_village_hostile_action_on_condition(MenuCallbackArgs args)
  	}
  
  	{
  		if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsVillage)
  		{
  			args.optionLeaveType = GameMenuOption.LeaveType.Raid;
  			MapEvent battle = PlayerEncounter.Battle;
  			if (PartyBase.MainParty.Side == BattleSideEnum.Attacker)
  			{
  				return !battle.PartiesOnSide(BattleSideEnum.Defender).Any((MapEventParty party) => party.Party.NumberOfHealthyMembers > 0);
  			}
  			return false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2219: 	private void game_menu_village_raid_no_resist_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, Settlement.CurrentSettlement.Party);
  		Settlement.CurrentSettlement.Militia = 0f;
  		if (PlayerEncounter.Current != null)
  		{
  			if (PlayerEncounter.InsideSettlement)
  			{
  				PlayerEncounter.LeaveSettlement();
  			}
  			GameMenu.ActivateGameMenu("raiding_village");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2233: 	private void game_menu_village_force_supplies_no_resist_loot_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		BeHostileAction.ApplyMinorCoercionHostileAction(PartyBase.MainParty, Settlement.CurrentSettlement.Party);
  		GameMenu.ActivateGameMenu("force_supplies_village");
  	}
  
  	private void game_menu_village_force_volunteers_no_resist_loot_on_consequence(MenuCallbackArgs args)
  	{
  		BeHostileAction.ApplyMajorCoercionHostileAction(PartyBase.MainParty, Settlement.CurrentSettlement.Party);
  		GameMenu.ActivateGameMenu("force_volunteers_village");
  	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2239: 	private void game_menu_village_force_volunteers_no_resist_loot_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		BeHostileAction.ApplyMajorCoercionHostileAction(PartyBase.MainParty, Settlement.CurrentSettlement.Party);
  		GameMenu.ActivateGameMenu("force_volunteers_village");
  	}
  
  	private void game_menu_taken_prisoner_on_init(MenuCallbackArgs args)
  	{
  	}
  
  	private bool game_menu_taken_prisoner_continue_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2289: 	private void VillageOutsideOnInit(MenuCallbackArgs args)
  	}
  
  	{
  		GameMenu.SwitchToMenu("village");
  	}
  
  	private void game_menu_town_outside_on_init(MenuCallbackArgs args)
  	{
  		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
  		args.MenuTitle = encounterSettlement.Name;
  		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2291: 		GameMenu.SwitchToMenu("village");
  	private void VillageOutsideOnInit(MenuCallbackArgs args)
  	{
  	}
  
  	private void game_menu_town_outside_on_init(MenuCallbackArgs args)
  	{
  		Settlement encounterSettlement = PlayerEncounter.EncounterSettlement;
  		args.MenuTitle = encounterSettlement.Name;
  		Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(encounterSettlement, out _accessDetails);
  		SettlementAccessModel.AccessLevel accessLevel = _accessDetails.AccessLevel;
  		int num = (int)accessLevel;
  		TextObject textObject;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2935: 	private void game_menu_village_loot_complete_on_init(MenuCallbackArgs args)
  	}
  
  	{
  		PlayerEncounter.Update();
  	}
  
  	private void game_menu_village_loot_complete_continue_on_consequence(MenuCallbackArgs args)
  	{
  		PlayerEncounter.Finish();
  	}
  
  	private bool game_menu_village_loot_complete_continue_on_condition(MenuCallbackArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2940: 	private void game_menu_village_loot_complete_continue_on_consequence(MenuCallbackArgs args)
  	}
  
  	{
  		PlayerEncounter.Finish();
  	}
  
  	private bool game_menu_village_loot_complete_continue_on_condition(MenuCallbackArgs args)
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Continue;
  		return true;
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:2945: 	private bool game_menu_village_loot_complete_continue_on_condition(MenuCallbackArgs args)
  	}
  
  	{
  		args.optionLeaveType = GameMenuOption.LeaveType.Continue;
  		return true;
  	}
  
  	private void game_menu_raid_interrupted_continue_on_consequence(MenuCallbackArgs args)
  	{
  		GameMenu.SwitchToMenu("encounter");
  	}
  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3220: 	[GameMenuInitializationHandler("village_outside")]
  	[GameMenuInitializationHandler("town_outside")]
  	[GameMenuInitializationHandler("fortification_crime_rating")]
  	[GameMenuInitializationHandler("menu_sneak_into_town_succeeded")]
  	[GameMenuInitializationHandler("disguise_first_time")]
  	[GameMenuInitializationHandler("disguise_not_first_time")]
  	private static void encounter_menu_ui_castle_on_init(MenuCallbackArgs args)
  	{
  		Settlement currentSettlement = Settlement.CurrentSettlement;
  		args.MenuContext.SetBackgroundMeshName(currentSettlement.SettlementComponent.WaitMeshName);
  	}
  
  	[GameMenuInitializationHandler("menu_castle_taken")]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultEncounterGameMenuModel.cs'; "'$lines=Get-Content $p; for($i=215;$i -le 249;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 529ms:
  215: 		{
  216: 			return null;
  217: 		}
  218: 		if (Hero.MainHero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
  219: 		{
  220: 			return null;
  221: 		}
  222: 		if (mainParty.MapEvent != null)
  223: 		{
  224: 			return "encounter";
  225: 		}
  226: 		if (mainParty.BesiegerCamp != null)
  227: 		{
  228: 			return "menu_siege_strategies";
  229: 		}
  230: 		if (mainParty.AttachedTo != null)
  231: 		{
  232: 			if (mainParty.Army.LeaderParty != mainParty && !mainParty.Army.LeaderParty.IsCurrentlyAtSea && mainParty.Army.LeaderParty.IsTransitionInProgress && !mainParty.HasNavalNavigationCapability)
  233: 			{
  234: 				return "menu_player_kicked_out_from_army_navigation_incapability";
  235: 			}
  236: 			if ((mainParty.AttachedTo.CurrentSettlement != null && !mainParty.AttachedTo.CurrentSettlement.IsUnderSiege) || (mainParty.AttachedTo.LastVisitedSettlement != null && mainParty.AttachedTo.LastVisitedSettlement.IsVillage && mainParty.AttachedTo.LastVisitedSettlement.Position.DistanceSquared(mainParty.AttachedTo.Position) < 1f))
  237: 			{
  238: 				return "army_wait_at_settlement";
  239: 			}
  240: 			if (mainParty.AttachedTo.CurrentSettlement != null && mainParty.AttachedTo.CurrentSettlement.IsUnderSiege)
  241: 			{
  242: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  243: 				{
  244: 					return "encounter_interrupted_siege_preparations";
  245: 				}
  246: 				return "menu_siege_strategies";
  247: 			}
  248: 			return "army_wait";
  249: 		}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultEncounterGameMenuModel.cs'; "'$lines=Get-Content $p; for($i=249;$i -le 330;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 544ms:
  249: 		}
  250: 		if (mainParty.CurrentSettlement != null)
  251: 		{
  252: 			Settlement currentSettlement = mainParty.CurrentSettlement;
  253: 			if (currentSettlement.IsFortification)
  254: 			{
  255: 				if (currentSettlement.Party.SiegeEvent != null && ((currentSettlement.Party.MapEvent == null && (currentSettlement.Town.GarrisonParty == null || currentSettlement.Town.GarrisonParty.MapEvent == null)) || MobileParty.MainParty.MapFaction == currentSettlement.MapFaction))
  256: 				{
  257: 					if (currentSettlement.Party.SiegeEvent.BesiegerCamp.LeaderParty == MobileParty.MainParty)
  258: 					{
  259: 						return "continue_siege_after_attack";
  260: 					}
  261: 					if (MobileParty.MainParty.BesiegedSettlement == null && MobileParty.MainParty.CurrentSettlement == null)
  262: 					{
  263: 						if (MobileParty.MainParty.IsCurrentlyAtSea)
  264: 						{
  265: 							if (!currentSettlement.SiegeEvent.IsBlockadeActive || currentSettlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEvent == null)
  266: 							{
  267: 								return "naval_town_outside";
  268: 							}
  269: 							return "join_encounter";
  270: 						}
  271: 						return "join_siege_event";
  272: 					}
  273: 					if (mainParty.CurrentSettlement.Party.MapEvent != null && mainParty.CurrentSettlement.Party.MapEvent.InvolvedParties.Contains(PartyBase.MainParty))
  274: 					{
  275: 						return "encounter";
  276: 					}
  277: 					if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  278: 					{
  279: 						return "encounter_interrupted_siege_preparations";
  280: 					}
  281: 					return "menu_siege_strategies";
  282: 				}
  283: 				if (currentSettlement.Party.MapEvent != null)
  284: 				{
  285: 					if (MobileParty.MainParty.MapFaction == currentSettlement.MapFaction)
  286: 					{
  287: 						return "encounter";
  288: 					}
  289: 					return "join_encounter";
  290: 				}
  291: 				if (currentSettlement.MapFaction == MobileParty.MainParty.MapFaction && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty)
  292: 				{
  293: 					return "army_wait_at_settlement";
  294: 				}
  295: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting && currentSettlement.IsFortification)
  296: 				{
  297: 					return "town_wait_menus";
  298: 				}
  299: 				if (currentSettlement.IsCastle)
  300: 				{
  301: 					return "castle_outside";
  302: 				}
  303: 				if (MobileParty.MainParty.IsCurrentlyAtSea)
  304: 				{
  305: 					return "naval_town_outside";
  306: 				}
  307: 				return "town_outside";
  308: 			}
  309: 			if (currentSettlement.IsHideout)
  310: 			{
  311: 				return "hideout_place";
  312: 			}
  313: 		}
  314: 		else if (Settlement.CurrentSettlement != null)
  315: 		{
  316: 			Settlement currentSettlement2 = Settlement.CurrentSettlement;
  317: 			if (currentSettlement2.IsVillage)
  318: 			{
  319: 				if (currentSettlement2.IsUnderRaid)
  320: 				{
  321: 					return "encounter_interrupted_raid_started";
  322: 				}
  323: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  324: 				{
  325: 					return "village_wait_menus";
  326: 				}
  327: 			}
  328: 		}
  329: 		return null;
  330: 	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\EncounterManager.cs'; "'$lines=Get-Content $p; for($i=96;$i -le 150;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=200;$i -le 235;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 713ms:
   96: 			GameMenu.SwitchToMenu("join_sally_out");
   97: 		}
   98: 	}
   99: 
  100: 	public static void StartSettlementEncounter(MobileParty attackerParty, Settlement settlement)
  101: 	{
  102: 		if (attackerParty.DefaultBehavior == AiBehavior.BesiegeSettlement && attackerParty.TargetSettlement == settlement && attackerParty.ShortTermBehavior != AiBehavior.AssaultSettlement)
  103: 		{
  104: 			if (attackerParty.BesiegedSettlement == null)
  105: 			{
  106: 				if (settlement.SiegeEvent == null)
  107: 				{
  108: 					Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, attackerParty);
  109: 				}
  110: 				else
  111: 				{
  112: 					MapEventSide mapEventSide = settlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEventSide;
  113: 					attackerParty.BesiegerCamp = settlement.SiegeEvent.BesiegerCamp;
  114: 					if (mapEventSide != null)
  115: 					{
  116: 						attackerParty.MapEventSide = mapEventSide;
  117: 					}
  118: 				}
  119: 			}
  120: 			if (settlement.Party.MapEvent == null)
  121: 			{
  122: 				return;
  123: 			}
  124: 		}
  125: 		if (attackerParty.DefaultBehavior == AiBehavior.DefendSettlement && attackerParty.IsCurrentlyAtSea && attackerParty.IsTargetingPort && settlement.SiegeEvent != null)
  126: 		{
  127: 			if (settlement.SiegeEvent.IsBlockadeActive)
  128: 			{
  129: 				if (settlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEventSide == null)
  130: 				{
  131: 					BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attackerParty.Party, settlement.SiegeEvent.BesiegerCamp.LeaderParty.Party, isSallyOut: false);
  132: 				}
  133: 				else
  134: 				{
  135: 					attackerParty.Party.MapEventSide = settlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEventSide.OtherSide;
  136: 				}
  137: 				return;
  138: 			}
  139: 			if (settlement.Party.MapEvent != null)
  140: 			{
  141: 				EnterSettlementAction.ApplyForParty(attackerParty, settlement);
  142: 				attackerParty.Party.MapEventSide = settlement.Party.MapEventSide;
  143: 				return;
  144: 			}
  145: 		}
  146: 		if (!attackerParty.IsVillager && attackerParty != MobileParty.MainParty && settlement.IsVillage && settlement.Village.VillageState == Village.VillageStates.Looted)
  147: 		{
  148: 			attackerParty.SetMoveModeHold();
  149: 			return;
  150: 		}
  200: 				{
  201: 					if (PlayerEncounter.IsActive)
  202: 					{
  203: 						PlayerEncounter.Finish();
  204: 					}
  205: 					RestartPlayerEncounter(attackerParty.Party, PartyBase.MainParty);
  206: 				}
  207: 			}
  208: 		}
  209: 		else
  210: 		{
  211: 			bool flag = MobileParty.MainParty.CurrentSettlement == settlement;
  212: 			MapEvent mapEvent2 = settlement.Party.MapEvent;
  213: 			if (mapEvent2 != null && !mapEvent2.IsFinalized && (mapEvent2.AttackerSide.MapFaction == attackerParty.MapFaction || mapEvent2.DefenderSide.MapFaction == attackerParty.MapFaction))
  214: 			{
  215: 				if (flag && attackerParty.AttachedTo == null)
  216: 				{
  217: 					PlayerEncounter.Finish();
  218: 				}
  219: 				settlement.Party.MapEventSide = ((mapEvent2.AttackerSide.MapFaction == attackerParty.MapFaction) ? mapEvent2.DefenderSide : mapEvent2.AttackerSide);
  220: 			}
  221: 			else if (settlement.Party.MapEvent == null && attackerParty != MobileParty.MainParty && attackerParty.ShortTermBehavior == AiBehavior.RaidSettlement && attackerParty.ShortTermTargetSettlement == settlement && FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, settlement.MapFaction))
  222: 			{
  223: 				if (flag)
  224: 				{
  225: 					PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
  226: 				}
  227: 				if (settlement.SettlementHitPoints > 0.001f)
  228: 				{
  229: 					StartBattleAction.ApplyStartRaid(attackerParty, settlement);
  230: 				}
  231: 				if (flag)
  232: 				{
  233: 					if (MobileParty.MainParty.MapFaction == settlement.MapFaction)
  234: 					{
  235: 						PlayerEncounter.Start();

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\EncounterManager.cs'; "'$lines=Get-Content $p; for($i=150;$i -le 199;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; for($i=235;$i -le 275;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 694ms:
  150: 		}
  151: 		if (attackerParty == MobileParty.MainParty)
  152: 		{
  153: 			PlayerEncounter.Start();
  154: 			MapEvent mapEvent = settlement.Party.MapEvent;
  155: 			if (mapEvent != null && mapEvent.IsRaid && attackerParty.MapFaction.IsAtWarWith(settlement.MapFaction))
  156: 			{
  157: 				MBList<MapEventParty> mBList = mapEvent.DefenderSide.Parties.WhereQ((MapEventParty x) => x.Party.IsMobile && x.Party.MobileParty.IsLordParty && x.Party.MemberRoster.TotalHealthyCount > 0).ToMBList();
  158: 				if (mBList.AnyQ())
  159: 				{
  160: 					PartyBase party = mBList[0].Party;
  161: 					PartyBase leaderParty = mapEvent.GetLeaderParty(BattleSideEnum.Attacker);
  162: 					MBReadOnlyList<MapEventParty> mBReadOnlyList = mapEvent.PartiesOnSide(BattleSideEnum.Attacker);
  163: 					StartBattleAction.Apply(party, leaderParty);
  164: 					foreach (MapEventParty item in mBList)
  165: 					{
  166: 						if (item.Party != party)
  167: 						{
  168: 							item.Party.MapEventSide = party.MapEventSide;
  169: 						}
  170: 					}
  171: 					foreach (MapEventParty item2 in mBReadOnlyList)
  172: 					{
  173: 						if (item2.Party != leaderParty)
  174: 						{
  175: 							item2.Party.MapEventSide = leaderParty.MapEventSide;
  176: 						}
  177: 					}
  178: 					PlayerEncounter.Current.Init(leaderParty, party);
  179: 				}
  180: 				else
  181: 				{
  182: 					PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);
  183: 				}
  184: 			}
  185: 			else
  186: 			{
  187: 				PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);
  188: 			}
  189: 			return;
  190: 		}
  191: 		if (attackerParty.Aggressiveness > 0.01f && PartyBase.MainParty.MapEvent != null && PartyBase.MainParty.MapEvent.MapEventSettlement == settlement)
  192: 		{
  193: 			if (PlayerEncounter.IsActive)
  194: 			{
  195: 				if (attackerParty.MapFaction == MobileParty.MainParty.MapFaction || (PartyBase.MainParty.MapEvent.AttackerSide.LeaderParty != PartyBase.MainParty && PartyBase.MainParty.MapEvent.DefenderSide.LeaderParty != PartyBase.MainParty))
  196: 				{
  197: 					PlayerEncounter.Current.OnPartyJoinEncounter(attackerParty);
  198: 				}
  199: 				else
  235: 						PlayerEncounter.Start();
  236: 						PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);
  237: 					}
  238: 					else
  239: 					{
  240: 						LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
  241: 					}
  242: 				}
  243: 			}
  244: 			else if (attackerParty != MobileParty.MainParty && attackerParty.ShortTermBehavior == AiBehavior.AssaultSettlement && attackerParty.ShortTermTargetSettlement == settlement && FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, settlement.MapFaction))
  245: 			{
  246: 				if (flag)
  247: 				{
  248: 					PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
  249: 				}
  250: 				bool flag2 = settlement.Party.MapEvent == null;
  251: 				StartBattleAction.ApplyStartAssaultAgainstWalls(attackerParty, settlement);
  252: 				if (attackerParty.MapEvent.DefenderSide.TroopCount == 0 && (PlayerSiege.PlayerSiegeEvent == null || PlayerSiege.PlayerSide != BattleSideEnum.Defender || MobileParty.MainParty.CurrentSettlement != settlement))
  253: 				{
  254: 					bool num = MobileParty.MainParty.BesiegedSettlement == settlement;
  255: 					if (num && PlayerEncounter.Current == null)
  256: 					{
  257: 						StartSettlementEncounter((MobileParty.MainParty.Army != null) ? MobileParty.MainParty.Army.LeaderParty : MobileParty.MainParty, settlement);
  258: 						MobileParty.MainParty.MapEventSide = ((PlayerSiege.PlayerSide != BattleSideEnum.Defender) ? attackerParty.MapEventSide : attackerParty.MapEventSide.OtherSide);
  259: 					}
  260: 					attackerParty.MapEvent.SetOverrideWinner(BattleSideEnum.Attacker);
  261: 					attackerParty.MapEvent.FinalizeEvent();
  262: 					if (num)
  263: 					{
  264: 						GameMenu.SwitchToMenu("menu_settlement_taken");
  265: 					}
  266: 					return;
  267: 				}
  268: 				if (attackerParty.ShortTermBehavior == AiBehavior.AssaultSettlement && flag2 && attackerParty != MobileParty.MainParty && PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == settlement && MobileParty.MainParty.CurrentSettlement == null)
  269: 				{
  270: 					PlayerEncounter.Finish();
  271: 				}
  272: 				if (MobileParty.MainParty.BesiegedSettlement == settlement && (MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty))
  273: 				{
  274: 					StartSettlementEncounter(MobileParty.MainParty, settlement);
  275: 				}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern 'SwitchToMenu(\"village_outside\")','ActivateGameMenu(\"village_outside\")','village_outside' -SimpleMatch | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 2625ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:277: gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3220: [GameMenuInitializationHandler("village_outside")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEncounterGameMenuModel.cs:55: return "village_outside";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\RevenueFarmingIssueBehavior.cs:572: GameMenu.SwitchToMenu("village_outside");
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents.CampaignBehaviors\TutorialPhaseCampaignBehavior.cs:473: GameMenu.SwitchToMenu("village_outside");
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:59801: return "village_outside";
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:151437: GameMenu.SwitchToMenu("village_outside");
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:185353: gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:188327: [GameMenuInitializationHandler("village_outside")]
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:59178: return "village_outside";
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:149873: GameMenu.SwitchToMenu("village_outside");
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:183532: gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:186441: [GameMenuInitializationHandler("village_outside")]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:59135: return "village_outside";
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:149825: GameMenu.SwitchToMenu("village_outside");
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:183484: gameSystemInitializer.AddGameMenu("village_outside", "{=!}.", VillageOutsideOnInit);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:186427: [GameMenuInitializationHandler("village_outside")]

context compacted
codex
I’ve finished the source pass and am closing out the engine touchpoints now: hostile access paths, conversation lifecycle, roster mutation, and the volunteer-recruit comparison. The main confirmed issue so far is not in the pure purchase service; it’s the settlement access assumption around hostile towns.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\SandBox\\TaleWorlds.CampaignSystem.SandBox\\TaleWorlds.CampaignSystem.SandBox.GameComponents\\DefaultEncounterGameMenuModel.cs'; "'$lines=Get-Content $path; for($i=45;$i -le 70;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs'; Select-String -Path "'$path -Pattern '"'OnRosterSizeChanged|MemberRoster' | Select-Object -First 20 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs,*.xml | Select-String -Pattern 'harad_elephant_rider|taom_spider_creature|ChargeRecruitCost|OnUnitRecruited|AddToCounts' | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter *.cs | Select-String -Pattern 'RecruitVolunteerTroopAction|ApplyByRecruiting|AddToCounts\\(' | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 647ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:129: public TroopRoster MemberRoster { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:347: int versionNo = MemberRoster.VersionNo;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:375: public int NumberOfHealthyMembers => MemberRoster.TotalManCount - MemberRoster.TotalWounded;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:377: public int NumberOfRegularMembers => MemberRoster.TotalRegulars;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:379: public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:381: public int NumberOfAllMembers => MemberRoster.TotalManCount;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:411: if (_lastNumberOfMenWithHorseVersionNo != MemberRoster.VersionNo)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:414: _lastNumberOfMenWithHorseVersionNo = MemberRoster.VersionNo;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:469: collectedObjects.Add(MemberRoster);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:486: internal static object AutoGeneratedGetMemberValueMemberRoster(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:488: return ((PartyBase)o).MemberRoster;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:772: MemberRoster.RemoveZeroCounts();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:809: else if (_lastNumberOfMenPerTierVersionNo != MemberRoster.VersionNo)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:819: for (int j = 0; j < MemberRoster.Count; j++)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:821: CharacterObject characterAtIndex = MemberRoster.GetCharacterAtIndex(j);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:827: int num2 = MemberRoster.GetElementNumber(j) - MemberRoster.GetElementWoundedNumber(j);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:832: _lastNumberOfMenPerTierVersionNo = MemberRoster.VersionNo;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:839: int num = MemberRoster.VersionNo;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:915: MemberRoster = new TroopRoster(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:943: for (int i = 0; i < MemberRoster.Count; i++)

2026-06-25T15:37:49.094112Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … s\DefaultEncounterGameMenuModel.cs'; $lines=[36;1mGet-Content $path[0m; for($i …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                               ~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\SandBox\TaleWorlds.CampaignSystem.SandBox\TaleWorlds.CampaignSystem.SandBox.GameComponents\DefaultEncounterGameMenuModel.cs' because it does not exist.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

 exited 1 in 740ms:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … s\DefaultEncounterGameMenuModel.cs'; $lines=[36;1mGet-Content $path[0m; for($i …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                               ~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\SandBox\TaleWorlds.CampaignSystem.SandBox\TaleWorlds.CampaignSystem.SandBox.GameComponents\DefaultEncounterGameMenuModel.cs' because it does not exist.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … t $path; for($i=45;$i -le 70;$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

 succeeded in 773ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:186: itemRoster.AddToCounts(item, 10);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:424: troops.AddToCounts(item.Troop, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:434: troopRoster.AddToCounts(item2.Troop, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:98: troopRoster.AddToCounts(CharacterObject.PlayerCharacter, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:106: troopRoster.AddToCounts(list2[num2], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:112: troopRoster.AddToCounts(list[num3], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:231: party.ItemRoster.AddToCounts(item, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:239: party.ItemRoster.AddToCounts(item, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:309: mobileParty.MemberRoster.AddToCounts(partyTemplateStack.Character, num5);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:329: mobileParty.MemberRoster.AddToCounts(character, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:347: mobileParty.MemberRoster.AddToCounts(character2, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:180: troopRoster.AddToCounts(character, _countToAddForEachTroopCheatMode);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:558: troopRoster.AddToCounts(hero.CharacterObject, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:561: troopRoster2.AddToCounts(hero.CharacterObject, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:599: rightOwnerParty.MemberRoster.AddToCounts(item.Character, -item.Number, insertAtFront: false, -item.WoundedNumber, -item.Xp);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:605: rightOwnerParty.PrisonRoster.AddToCounts(item2.Character, -item2.Number, insertAtFront: false, -item2.WoundedNumber, -item2.Xp);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:603: MainParty.ItemRoster.AddToCounts(DefaultItems.Grain, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:1788: MainParty.MemberRoster.AddToCounts(Hero.MainHero.CharacterObject, 1, insertAtFront: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:630: obj.PartyBelongedTo?.ItemRoster.AddToCounts(obj.BattleEquipment[equipmentIndex], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:636: obj.PartyBelongedTo?.ItemRoster.AddToCounts(obj.CivilianEquipment[equipmentIndex], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1168: PartyBase.MainParty.ItemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)i), 100);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1297: PartyBase.MainParty.ItemRoster.AddToCounts(itemObject, result);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1310: MobileParty.MainParty.ItemRoster.AddToCounts(rosterElement, result);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1314: MobileParty.MainParty.ItemRoster.AddToCounts(rosterElement, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1324: MobileParty.MainParty.ItemRoster.AddToCounts(itemObject, result);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs:209: target.PartyBelongedTo.ItemRoster.AddToCounts(target.BattleEquipment[i], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs:213: target.PartyBelongedTo.ItemRoster.AddToCounts(target.CivilianEquipment[i], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs:217: target.PartyBelongedTo.ItemRoster.AddToCounts(target.StealthEquipment[i], 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1570: PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1707: PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:2316: PartyBelongedTo?.ItemRoster.AddToCounts(DefaultItems.Trash, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:2341: PartyBelongedTo?.ItemRoster.AddToCounts(DefaultItems.Trash, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\AddHeroToPartyAction.cs:12: hero.PartyBelongedTo?.MemberRoster.AddToCounts(hero.CharacterObject, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BreakInOutBesiegedSettlementAction.cs:42: casualties.AddToCounts(characterAtIndex, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BreakInOutBesiegedSettlementAction.cs:98: casualties.AddToCounts(characterAtIndex2, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\GiveItemAction.cs:13: giverParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -itemRosterElement.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\GiveItemAction.cs:14: receiverParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\GiveItemAction.cs:19: giver.PartyBelongedTo.Party.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -itemRosterElement.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\GiveItemAction.cs:20: receiver.PartyBelongedTo.Party.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -itemRosterElement.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\RemoveCompanionAction.cs:22: partyBase.MemberRoster.AddToCounts(companion.CharacterObject, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\SellGoodsForTradeAction.cs:58: settlement.ItemRoster.AddToCounts(equipmentElement, num5);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\SellGoodsForTradeAction.cs:59: mobileParty.ItemRoster.AddToCounts(equipmentElement, -num5);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\SellItemsAction.cs:51: sellerParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\SellItemsAction.cs:52: buyerParty?.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\SellPrisonersAction.cs:23: sellerParty.PrisonRoster.AddToCounts(character, -item.Number, insertAtFront: false, -item.WoundedNumber);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\TransferPrisonerAction.cs:14: prisonerOwnerParty.PrisonRoster.AddToCounts(prisonerTroop, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.AgentOrigins\PartyAgentOrigin.cs:178: Party.MemberRoster.AddToCounts(_troop, 0, insertAtFront: false, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.AgentOrigins\PartyAgentOrigin.cs:197: Party?.MemberRoster.AddToCounts(_troop, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem.Barterables\ItemBarterable.cs:63: base.OriginalParty.ItemRoster.AddToCounts(_itemElement.EquipmentElement, -base.CurrentAmount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem.Barterables\ItemBarterable.cs:64: _otherParty.ItemRoster.AddToCounts(_itemElement.EquipmentElement, base.CurrentAmount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs:384: troopsTakenAsMember.AddToCounts(party.MemberRoster.GetCharacterAtIndex(i), party.MemberRoster.GetElementNumber(i));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs:388: troopsTakenAsPrisoner.AddToCounts(party.MemberRoster.GetCharacterAtIndex(i), party.MemberRoster.GetElementNumber(i));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditInteractionsCampaignBehavior.cs:397: troopsTakenAsMember.AddToCounts(party.PrisonRoster.GetCharacterAtIndex(num), party.PrisonRoster.GetElementNumber(num));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:194: mobileParty2.MemberRoster.AddToCounts(culture.BanditBoss, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:233: allBanditParty.ItemRoster.AddToCounts(item, num2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:531: banditParty.ItemRoster.AddToCounts(item, num2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:118: gainedLoots.AddToCounts(itemObject, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:143: gainedLoots.AddToCounts(hero.BannerItem.Item, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BattleCampaignBehavior.cs:34: gainedLoots.AddToCounts(item.EquipmentElement, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignBattleRecoveryBehavior.cs:34: party.ItemRoster.AddToCounts(elementCopyAtIndex.EquipmentElement, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignBattleRecoveryBehavior.cs:102: party.ItemRoster.AddToCounts(equipmentElement.Item, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:458: mobileParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -num3);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:490: mobileParty.ItemRoster.AddToCounts(itemRosterElement2.EquipmentElement, -num6);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:762: convoy.MemberRoster.AddToCounts(character, num4);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:777: convoy.MemberRoster.AddToCounts(character2, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:2148: troopRoster.AddToCounts(item.Character, item.Number);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:2204: itemRoster.AddToCounts(elementCopyAtIndex.EquipmentElement.Item, elementCopyAtIndex.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:2209: itemRoster.AddToCounts(elementCopyAtIndex.EquipmentElement.Item, elementCopyAtIndex.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:2223: itemRoster.AddToCounts(randomElement.EquipmentElement.Item, randomElement.Amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:269: MobileParty.MainParty.MemberRoster.AddToCounts(oneToOneConversationHero.CharacterObject, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:275: mobileParty.MemberRoster.AddToCounts(clan.Culture.BasicTroop, MBRandom.RandomInt(12, 15));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:276: mobileParty.MemberRoster.AddToCounts(clan.Culture.EliteBasicTroop, MBRandom.RandomInt(10, 15));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:847: rightOwnerParty.MemberRoster.AddToCounts(item.Character, -item.Number, insertAtFront: false, -item.WoundedNumber, -item.Xp);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:853: rightOwnerParty.PrisonRoster.AddToCounts(item2.Character, -item2.Number, insertAtFront: false, -item2.WoundedNumber, -item2.Xp);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:821: itemRoster.AddToCounts(craftingMaterialItem, -refineFormula.Input1Count);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:826: itemRoster.AddToCounts(craftingMaterialItem2, -refineFormula.Input2Count);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:831: itemRoster.AddToCounts(craftingMaterialItem3, refineFormula.OutputCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:836: itemRoster.AddToCounts(craftingMaterialItem4, refineFormula.Output2Count);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:853: itemRoster.AddToCounts(Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem((CraftingMaterials)num), smeltingOutputForItem[num]);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:856: itemRoster.AddToCounts(equipmentElement, -1);

 succeeded in 1792ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\spider_creature.xml:22: <NPCCharacter id="taom_spider_creature"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\spider_creature.xml:30: name="{=taom_spider_creature_name}Spider Rider">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:40: <Troop id="taom_spider_creature" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\elite_emissary\elite_emissary_config.xml:110: <Troop id="harad_elephant_rider" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\troop_resource_costs.xml:23: <Troop id="harad_elephant_rider" resource_id="war_drums" recruit_cost="50" daily_upkeep="10" merchant_cost="70" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\troop_resource_costs.xml:24: <Troop id="taom_spider_creature" resource_id="war_spoils" recruit_cost="5" daily_upkeep="1" merchant_cost="18" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\troop_resource_costs.xml:33: (mordor_uruk_captain/baraddurguard, harad_elephant_rider, taom_spider_creature
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_harad.xml:146: id="harad_elephant_rider"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\troops\troops_harad.xml:149: name="{=harad_elephant_rider_name}[Aharad] Elephant Rider"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:85: <TroopWeight id="taom_spider_creature" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:118: <TroopWeight id="harad_elephant_rider" weight="7.0" />
C:\Users\mikew\source\repos\TAOM\Main\Adapters\InventoryVMAdapter.cs:128: // response. Without this, direct mutation of Hero.BattleEquipment + ItemRoster.AddToCounts
C:\Users\mikew\source\repos\TAOM\Main\Adapters\ITownRosterAdapter.cs:18: /// removed (positive count, the item existed). Vanilla `AddToCounts` handles the
C:\Users\mikew\source\repos\TAOM\Main\Adapters\LotrIssueRewardAdapter.cs:12: /// renown via <c>GainRenownAction.Apply(hero, float)</c>, item via <c>ItemRoster.AddToCounts</c>.
C:\Users\mikew\source\repos\TAOM\Main\Adapters\LotrIssueRewardAdapter.cs:43: roster?.AddToCounts(item, count);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:8: /// Uses the <c>AddToCounts(EquipmentElement, int)</c> overload so the inventory entry preserves
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:10: /// <c>AddToCounts(ItemObject, int)</c> overload would drop the modifier.
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:21: roster.AddToCounts(concrete.Mount, 1);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:23: roster.AddToCounts(concrete.Harness, 1);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:33: roster.AddToCounts(concrete.Mount, -1);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs:35: roster.AddToCounts(concrete.Harness, -1);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PlayerEquipmentAdapter.cs:42: roster.AddToCounts(element, 1);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\PlayerPartyAdapter.cs:42: roster.AddToCounts(character, count);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\QuestHeroAdapter.cs:14: /// <c>ItemRoster.AddToCounts(ItemObject, int)</c>, reads via <c>Hero.Gold</c> / <c>Clan.Renown</c> /
C:\Users\mikew\source\repos\TAOM\Main\Adapters\QuestHeroAdapter.cs:49: roster?.AddToCounts(item, count);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\TownRosterAdapter.cs:49: settlement.ItemRoster.AddToCounts(new EquipmentElement(itemObject), count);
C:\Users\mikew\source\repos\TAOM\Main\Adapters\TownRosterAdapter.cs:100: // applied it to `new EquipmentElement(itemObject)` (null modifier), which AddToCounts
C:\Users\mikew\source\repos\TAOM\Main\Adapters\TownRosterAdapter.cs:106: // first — AddToCounts mutates/reindexes the roster as stacks empty.
C:\Users\mikew\source\repos\TAOM\Main\Adapters\TownRosterAdapter.cs:122: // Vanilla AddToCounts accepts negative counts; this triggers OnInventoryUpdated
C:\Users\mikew\source\repos\TAOM\Main\Adapters\TownRosterAdapter.cs:124: roster.AddToCounts(element, -take);
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:65: __result.AddToCounts(character, delta);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverPersonnelLotrIssue.cs:352: roster.AddToCounts(el.Character, -take);
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\Domain\TroopResourceCostEntry.cs:12: // volunteer can't be double-charged. Consumed by the RecruitmentVM gate + OnUnitRecruited
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\Hooks\RecruitmentVM_RecruitGate_Patch.cs:14: // OnUnitRecruitedEvent (SpecialResourcesBehavior); this is the block half only.
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\ISpecialResourceService.cs:24: /// <c>OnUnitRecruitedEvent</c> (player-only) for the elephant/spider volunteers; the
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\ISpecialResourceService.cs:27: void ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count);
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:54: // Player-only recruit charge: OnUnitRecruited fires only from player-facing recruit flows
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:59: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:321: private void OnUnitRecruited(CharacterObject character, int count)
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:329: _service.ChargeRecruitCost(hero.StringId, kingdomId, cultureId, character.StringId, count);
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:376: party.MemberRoster.AddToCounts(character, -toRemove);
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourceService.cs:202: public void ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count)
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderConfig.cs:6: /// `taom_spider_creature` (characters/spider_creature.xml) with `Item.spider_mount_a` in its Horse slot;
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderMissionBehavior.cs:18: /// (`taom_spider_creature`), not the spider. The rider's cavalry AI drives movement; the BT layers the
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\VolunteerRecruitmentService.cs:98: // and ADDS harad_elephant_rider at a low weight (1 of 11 ~= 9% of Ayerikkä's volunteer rolls). No other
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\VolunteerRecruitmentService.cs:106: ("harad_elephant_rider", 1));
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\VolunteerRecruitmentService.cs:590: // taom_spider_creature: the giant spider, recruitable at Dol Guldur fiefs only. It rides in the
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\VolunteerRecruitmentService.cs:616: ("taom_spider_creature",     40),  // TEMP-SPIDER-TEST: weight 1 -> 40 for campaign battle testing; REVERT before commit.
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\VolunteerRecruitmentService.cs:655: new VolunteerChance("taom_spider_creature", 40)  // TEMP-SPIDER-TEST: weight 1 -> 40 for campaign battle testing; REVERT before commit.
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroopsHook.cs:70: roster.AddToCounts(ch, -instr.Count);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopShedPlanning.cs:31: /// and applies it via <c>MemberRoster.AddToCounts(character, -Count)</c>.

codex
The volunteer path is matching the emissary grant shape so far: TAOM charges volunteer resources via `OnUnitRecruited`, while the actual roster insertion is still the vanilla `MemberRoster.AddToCounts(CharacterObject, count)` route. I’m checking the specific vanilla recruit action rather than relying on the event name.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'DefaultEncounterGameMenuModel.cs' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter *.cs | Select-String -Pattern 'RecruitVolunteerTroopAction|OnUnitRecruited|ApplyByRecruiting' | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem' -Recurse -Filter *.cs | Select-String -Pattern 'OnRosterSizeChanged|RosterSizeChanged|OnMemberRoster' | Select-Object -First 60 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main\\Features\\SpecialResources\\SpecialResourcesBehavior.cs'; "'$lines=Get-Content $path; foreach($range in @(@(50,65),@(315,333),@(365,380))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 827ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEncounterGameMenuModel.cs

 succeeded in 799ms:
   50:         CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
   51:         CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
   52:         CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
   53:         CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutCompleted);
   54:         // Player-only recruit charge: OnUnitRecruited fires only from player-facing recruit flows
   55:         // (recruit-volunteers screen, mercenary, prisoners), NOT the AI/generic ApplyInternal path
   56:         // (that fires OnTroopRecruited). Charges the one-time recruit_cost for any troop that carries
   57:         // one (elephant/spider). The RecruitmentVM gate (Patch51) blocks confirming an unaffordable
   58:         // cart, so this deduction never drives the balance negative for those troops.
   59:         CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
   60:         // Phase 9b #133 P1 — ScreenManager is static/global and outlives any campaign. New campaign
   61:         // in same process: a second behavior instance registers another listener; first instance's
   62:         // listener stays alive, calling _service.BeginPartyScreenSession() on the shared singleton
   63:         // service → resets _pendingSpend/_inSession for new campaign sessions, potentially
   64:         // cancelling legitimate spends. OnGameOverEvent is the only public lifecycle hook in
   65:         // v1.3.15 that fires when a campaign ends; CampaignBehaviorBase has no OnFinalize/OnGameEnd

  315:         NotifyEarning(hero.StringId, kingdomId, cultureId, "hideout");
  316:     }
  317: 
  318:     // Charges the one-time recruit_cost for the recruited troop (no-op unless it carries one). Player-only
  319:     // event, so we always charge Hero.MainHero's resolved resource. Data-driven: any troop with a
  320:     // recruit_cost in troop_resource_costs.xml is charged here and gated by Patch51 — no hardcoded ids.
  321:     private void OnUnitRecruited(CharacterObject character, int count)
  322:     {
  323:         if (character == null || count <= 0) return;
  324: 
  325:         var hero = Hero.MainHero;
  326:         if (hero == null) return;
  327: 
  328:         GetHeroIds(hero, out var kingdomId, out var cultureId);
  329:         _service.ChargeRecruitCost(hero.StringId, kingdomId, cultureId, character.StringId, count);
  330:     }
  331: 
  332:     private void NotifyEarning(string heroId, string kingdomId, string cultureId, string source)
  333:     {

  365:         {
  366:             var character = CharacterObject.Find(entry.TroopId);
  367:             if (character == null) continue;
  368: 
  369:             var index = party.MemberRoster.FindIndexOfTroop(character);
  370:             if (index < 0) continue;
  371: 
  372:             var currentCount = party.MemberRoster.GetElementNumber(index);
  373:             var toRemove = System.Math.Min(entry.DesertCount, currentCount);
  374:             if (toRemove <= 0) continue;
  375: 
  376:             party.MemberRoster.AddToCounts(character, -toRemove);
  377:             totalDeserted += toRemove;
  378:             _logger.LogInfo($"[SpecRes] Deserted: {entry.TroopId} x{toRemove}");
  379:         }
  380: 


 succeeded in 1092ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1197: internal void OnRosterSizeChanged(TroopRoster roster)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Roster\TroopRoster.cs:367: OwnerParty?.OnRosterSizeChanged(this);

 succeeded in 3652ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1179: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1184: eventReceivers[i].OnUnitRecruited(character, amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:533: public virtual void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:325: private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:881: public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1348: _onUnitRecruitedEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2143: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2145: Instance._onUnitRecruitedEvent.Invoke(character, amount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:141: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:191: private void OnUnitRecruited(CharacterObject troop, int count)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:795: CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitPrisonersCampaignBehavior.cs:130: CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment\RecruitmentVM.cs:895: CampaignEventDispatcher.Instance.OnUnitRecruited(item.Character, 1);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\StatisticsCampaignBehavior.cs:166: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener((object)this, (Action<CharacterObject, int>)OnUnitRecruited);
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\StatisticsCampaignBehavior.cs:207: private void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.QuestTasks\RecruitTroopTutorialQuestTask.cs:42: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener((object)this, (Action<CharacterObject, int>)OnUnitRecruited);
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.QuestTasks\RecruitTroopTutorialQuestTask.cs:45: private void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:16623: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:16628: eventReceivers[i].OnUnitRecruited(character, amount);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:18520: public virtual void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:19422: private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:19978: public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:20445: _onUnitRecruitedEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:21240: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:21242: Instance._onUnitRecruitedEvent.Invoke(character, amount);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:211042: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:211092: private void OnUnitRecruited(CharacterObject troop, int count)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:211705: CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:211880: CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.ViewModelCollection.cs:51409: CampaignEventDispatcher.Instance.OnUnitRecruited(item.Character, 1);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:16538: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:16543: eventReceivers[i].OnUnitRecruited(character, amount);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:18435: public virtual void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:19337: private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:19893: public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:20360: _onUnitRecruitedEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:21155: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:21157: Instance._onUnitRecruitedEvent.Invoke(character, amount);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:209020: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:209070: private void OnUnitRecruited(CharacterObject troop, int count)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:209674: CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:209845: CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.ViewModelCollection.cs:51340: CampaignEventDispatcher.Instance.OnUnitRecruited(item.Character, 1);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:16495: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:16500: eventReceivers[i].OnUnitRecruited(character, amount);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:18392: public virtual void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:19294: private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:19850: public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:20317: _onUnitRecruitedEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:21112: public override void OnUnitRecruited(CharacterObject character, int amount)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:21114: Instance._onUnitRecruitedEvent.Invoke(character, amount);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:209005: CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:209055: private void OnUnitRecruited(CharacterObject troop, int count)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:209659: CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:209830: CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.ViewModelCollection.cs:51340: CampaignEventDispatcher.Instance.OnUnitRecruited(item.Character, 1);

codex
The hostile-town path is now verified against v1.4.6 decompile: vanilla explicitly supports entering a hostile town by disguise and then switches into the normal `town` menu. That makes the current “hostile settlements are not enterable” decision unsafe for towns.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultEncounterGameMenuModel.cs'; "'$lines=Get-Content $path; foreach($range in @(@(45,70),@(270,305),@(350,420))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs'; "'$lines=Get-Content $path; for($i=1190;$i -le 1215;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem.ViewModelCollection\\TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment\\RecruitmentVM.cs'; "'$lines=Get-Content $path; foreach($range in @(@(860,905),@(905,930))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\RecruitmentCampaignBehavior.cs'; "'$lines=Get-Content $path; foreach($range in @(@(760,805),@(185,205))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 747ms:
 1190: 		}
 1191: 		else
 1192: 		{
 1193: 			element.Xp = TaleWorlds.Library.MathF.Max(element.Xp, 0);
 1194: 		}
 1195: 	}
 1196: 
 1197: 	internal void OnRosterSizeChanged(TroopRoster roster)
 1198: 	{
 1199: 		if (object.Equals(roster, MemberRoster))
 1200: 		{
 1201: 			CampaignEventDispatcher.Instance.OnPartySizeChanged(this);
 1202: 		}
 1203: 	}
 1204: }
 1205: 
 1206: 
 1207: 
 1208: 
 1209: 
 1210: 
 1211: 
 1212: 
 1213: 
 1214: 
 1215: 

 succeeded in 761ms:
   45: 					{
   46: 						joinBattle = true;
   47: 						return "encounter";
   48: 					}
   49: 					return "join_encounter";
   50: 				}
   51: 				if (settlement.MapFaction == MobileParty.MainParty.MapFaction && MobileParty.MainParty.Army != null && attackerParty == MobileParty.MainParty.Army.LeaderParty.Party && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty)
   52: 				{
   53: 					return "army_wait_at_settlement";
   54: 				}
   55: 				return "village_outside";
   56: 			}
   57: 			if (settlement.IsFortification)
   58: 			{
   59: 				if (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Defender && settlement.Party.MapEvent == null)
   60: 				{
   61: 					return "menu_siege_strategies";
   62: 				}
   63: 				if (settlement.Party.SiegeEvent != null && ((settlement.Party.MapEvent == null && (settlement.Town.GarrisonParty == null || settlement.Town.GarrisonParty.MapEvent == null || settlement.Town.GarrisonParty.MapEvent.IsSallyOut)) || MobileParty.MainParty.MapFaction == settlement.MapFaction))
   64: 				{
   65: 					if (settlement.Party.SiegeEvent.BesiegerCamp.LeaderParty == MobileParty.MainParty)
   66: 					{
   67: 						return "continue_siege_after_attack";
   68: 					}
   69: 					if (MobileParty.MainParty.BesiegedSettlement == null && MobileParty.MainParty.CurrentSettlement == null)
   70: 					{

  270: 						}
  271: 						return "join_siege_event";
  272: 					}
  273: 					if (mainParty.CurrentSettlement.Party.MapEvent != null && mainParty.CurrentSettlement.Party.MapEvent.InvolvedParties.Contains(PartyBase.MainParty))
  274: 					{
  275: 						return "encounter";
  276: 					}
  277: 					if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting)
  278: 					{
  279: 						return "encounter_interrupted_siege_preparations";
  280: 					}
  281: 					return "menu_siege_strategies";
  282: 				}
  283: 				if (currentSettlement.Party.MapEvent != null)
  284: 				{
  285: 					if (MobileParty.MainParty.MapFaction == currentSettlement.MapFaction)
  286: 					{
  287: 						return "encounter";
  288: 					}
  289: 					return "join_encounter";
  290: 				}
  291: 				if (currentSettlement.MapFaction == MobileParty.MainParty.MapFaction && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty)
  292: 				{
  293: 					return "army_wait_at_settlement";
  294: 				}
  295: 				if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting && currentSettlement.IsFortification)
  296: 				{
  297: 					return "town_wait_menus";
  298: 				}
  299: 				if (currentSettlement.IsCastle)
  300: 				{
  301: 					return "castle_outside";
  302: 				}
  303: 				if (MobileParty.MainParty.IsCurrentlyAtSea)
  304: 				{
  305: 					return "naval_town_outside";

  350: 
  351: 
  352: 
  353: 
  354: 
  355: 
  356: 
  357: 
  358: 
  359: 
  360: 
  361: 
  362: 
  363: 
  364: 
  365: 
  366: 
  367: 
  368: 
  369: 
  370: 
  371: 
  372: 
  373: 
  374: 
  375: 
  376: 
  377: 
  378: 
  379: 
  380: 
  381: 
  382: 
  383: 
  384: 
  385: 
  386: 
  387: 
  388: 
  389: 
  390: 
  391: 
  392: 
  393: 
  394: 
  395: 
  396: 
  397: 
  398: 
  399: 
  400: 
  401: 
  402: 
  403: 
  404: 
  405: 
  406: 
  407: 
  408: 
  409: 
  410: 
  411: 
  412: 
  413: 
  414: 
  415: 
  416: 
  417: 
  418: 
  419: 
  420: 


 succeeded in 713ms:
  860: 		int num5 = TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
  861: 		TotalCostText = num5.ToString();
  862: 		bool doesPlayerHasEnoughMoney = (IsDoneEnabled = num5 <= Hero.MainHero.Gold);
  863: 		DoneHint.HintText = new TextObject("{=!}" + GetDoneHint(doesPlayerHasEnoughMoney));
  864: 		UpdateRecruitAllProperties();
  865: 	}
  866: 
  867: 	public void ExecuteDone()
  868: 	{
  869: 		if (CurrentPartySize <= PartyCapacity)
  870: 		{
  871: 			OnDone();
  872: 			return;
  873: 		}
  874: 		GameTexts.SetVariable("newline", "\n");
  875: 		string text = GameTexts.FindText("str_party_over_limit_troops").ToString();
  876: 		InformationManager.ShowInquiry(new InquiryData(new TextObject("{=uJro3Bua}Over Limit").ToString(), text, isAffirmativeOptionShown: true, isNegativeOptionShown: true, GameTexts.FindText("str_yes").ToString(), GameTexts.FindText("str_no").ToString(), delegate
  877: 		{
  878: 			OnDone();
  879: 		}, null));
  880: 	}
  881: 
  882: 	private void OnDone()
  883: 	{
  884: 		RefreshPartyProperties();
  885: 		int num = TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
  886: 		if (num > Hero.MainHero.Gold)
  887: 		{
  888: 			Debug.FailedAssert("Execution shouldn't come here. The checks should happen before", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\GameMenu\\Recruitment\\RecruitmentVM.cs", "OnDone", 229);
  889: 			return;
  890: 		}
  891: 		foreach (RecruitVolunteerTroopVM item in TroopsInCart)
  892: 		{
  893: 			item.Owner.OwnerHero.VolunteerTypes[item.Index] = null;
  894: 			MobileParty.MainParty.MemberRoster.AddToCounts(item.Character, 1);
  895: 			CampaignEventDispatcher.Instance.OnUnitRecruited(item.Character, 1);
  896: 		}
  897: 		GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, num, disableNotification: true);
  898: 		if (num > 0)
  899: 		{
  900: 			MBTextManager.SetTextVariable("GOLD_AMOUNT", TaleWorlds.Library.MathF.Abs(num));
  901: 			InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_gold_removed_with_icon").ToString(), "event:/ui/notification/coins_negative"));
  902: 		}
  903: 		Deactivate();
  904: 	}
  905: 

  905: 
  906: 	public void ExecuteForceQuit()
  907: 	{
  908: 		if (IsQuitting)
  909: 		{
  910: 			return;
  911: 		}
  912: 		IsQuitting = true;
  913: 		if (TroopsInCart.Count > 0)
  914: 		{
  915: 			InformationManager.ShowInquiry(new InquiryData(GameTexts.FindText("str_quit").ToString(), GameTexts.FindText("str_quit_question").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, GameTexts.FindText("str_yes").ToString(), GameTexts.FindText("str_no").ToString(), delegate
  916: 			{
  917: 				ExecuteReset();
  918: 				ExecuteDone();
  919: 				IsQuitting = false;
  920: 			}, delegate
  921: 			{
  922: 				IsQuitting = false;
  923: 			}), pauseGameActiveState: true);
  924: 		}
  925: 		else
  926: 		{
  927: 			Deactivate();
  928: 		}
  929: 	}
  930: 


 succeeded in 715ms:
  760: 				int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
  761: 				MBTextManager.SetTextVariable("GOLD_AMOUNT", mercenaryData.Number * roundedResultNumber);
  762: 			}
  763: 		}
  764: 		else
  765: 		{
  766: 			num = 0;
  767: 		}
  768: 		return (byte)num != 0;
  769: 	}
  770: 
  771: 	private bool conversation_mercenary_recruit_accept_on_condition()
  772: 	{
  773: 		TownMercenaryData mercenaryData = GetMercenaryData(PlayerEncounter.EncounterSettlement.Town);
  774: 		int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
  775: 		MBTextManager.SetTextVariable("PLURAL", (mercenaryData.Number > 1) ? 1 : 0);
  776: 		return Hero.MainHero.Gold >= mercenaryData.Number * roundedResultNumber;
  777: 	}
  778: 
  779: 	private bool conversation_mercenary_recruited_on_condition()
  780: 	{
  781: 		if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Mercenary || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.CaravanGuard || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Gangster)
  782: 		{
  783: 			return PlayerEncounter.EncounterSettlement != null;
  784: 		}
  785: 		return false;
  786: 	}
  787: 
  788: 	private void BuyMercenaries()
  789: 	{
  790: 		GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).ChangeMercenaryCount(-_selectedMercenaryCount);
  791: 		int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).TroopType, Hero.MainHero).RoundedResultNumber;
  792: 		MobileParty.MainParty.AddElementToMemberRoster(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
  793: 		int amount = _selectedMercenaryCount * roundedResultNumber;
  794: 		GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount);
  795: 		CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, _selectedMercenaryCount);
  796: 	}
  797: 
  798: 	private void conversation_mercenary_recruit_accept_on_consequence()
  799: 	{
  800: 		_selectedMercenaryCount = GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).Number;
  801: 		BuyMercenaries();
  802: 	}
  803: 
  804: 	private bool conversation_mercenary_recruit_accept_some_on_condition()
  805: 	{

  185: 		if (recruiter != null && recruiter.PartyBelongedTo != null && troop.Occupation == Occupation.Bandit)
  186: 		{
  187: 			SkillLevelingManager.OnBanditsRecruited(recruiter.PartyBelongedTo, troop, count);
  188: 		}
  189: 	}
  190: 
  191: 	private void OnUnitRecruited(CharacterObject troop, int count)
  192: 	{
  193: 		if (Hero.MainHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
  194: 		{
  195: 			MobileParty.MainParty.MemberRoster.AddXpToTroop(troop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * count);
  196: 		}
  197: 		SkillLevelingManager.OnTroopRecruited(Hero.MainHero, count, troop.Tier);
  198: 		if (troop.Occupation == Occupation.Bandit)
  199: 		{
  200: 			SkillLevelingManager.OnBanditsRecruited(MobileParty.MainParty, troop, count);
  201: 		}
  202: 	}
  203: 
  204: 	private void DailyTickTown(Town town)
  205: 	{


codex
The creature/mount concern is clean on the grant mechanism: vanilla volunteer recruitment adds the selected `CharacterObject` to `MobileParty.MainParty.MemberRoster` and then fires `OnUnitRecruited`; emissary uses the same roster API but charges `merchant_cost` instead of relying on the recruit event.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementAccessModel.cs'; "'$lines=Get-Content $path; foreach($range in @(@(280,305),@(368,417))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Roster\\TroopRoster.cs'; "'$lines=Get-Content $path; foreach($range in @(@(312,373),@(428,455))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Modules\\SandBox\\SandBox.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $path; foreach($range in @(@(200,210),@(1340,1350),@(2488,2528))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main\\Features\\EliteEmissary\\Hooks\\EliteEmissaryBehavior.cs'; "'$lines=Get-Content $path; foreach($range in @(@(80,96),@(167,180))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 953ms:
  280: 	private void CanMainHeroEnterVillage(Settlement settlement, out AccessDetails accessDetails)
  281: 	{
  282: 		Hero mainHero = Hero.MainHero;
  283: 		accessDetails = new AccessDetails
  284: 		{
  285: 			AccessLevel = AccessLevel.NoAccess,
  286: 			AccessLimitationReason = AccessLimitationReason.None,
  287: 			PreliminaryActionObligation = PreliminaryActionObligation.None,
  288: 			PreliminaryActionType = PreliminaryActionType.None
  289: 		};
  290: 		MobileParty partyBelongedTo = mainHero.PartyBelongedTo;
  291: 		if (partyBelongedTo != null && (partyBelongedTo.Army == null || partyBelongedTo.Army.LeaderParty == partyBelongedTo))
  292: 		{
  293: 			accessDetails.AccessLevel = AccessLevel.FullAccess;
  294: 			accessDetails.AccessMethod = AccessMethod.Direct;
  295: 		}
  296: 		if (settlement.Village.VillageState == Village.VillageStates.Looted)
  297: 		{
  298: 			accessDetails.AccessLevel = AccessLevel.NoAccess;
  299: 			accessDetails.AccessLimitationReason = AccessLimitationReason.VillageIsLooted;
  300: 		}
  301: 	}
  302: 
  303: 	private bool CanMainHeroManageTown(Settlement settlement, out bool disableOption, out TextObject disabledText)
  304: 	{
  305: 		disabledText = null;

  368: 	private void CanMainHeroEnterTown(Settlement settlement, out AccessDetails accessDetails)
  369: 	{
  370: 		Hero mainHero = Hero.MainHero;
  371: 		accessDetails = default(AccessDetails);
  372: 		if (settlement.OwnerClan == mainHero.Clan)
  373: 		{
  374: 			accessDetails = new AccessDetails
  375: 			{
  376: 				AccessLevel = AccessLevel.FullAccess,
  377: 				AccessMethod = AccessMethod.Direct
  378: 			};
  379: 		}
  380: 		else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mainHero.MapFaction, settlement.MapFaction))
  381: 		{
  382: 			accessDetails = new AccessDetails
  383: 			{
  384: 				AccessLevel = AccessLevel.FullAccess,
  385: 				AccessMethod = AccessMethod.Direct
  386: 			};
  387: 			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  388: 			{
  389: 				accessDetails.PreliminaryActionType = PreliminaryActionType.FaceCharges;
  390: 				accessDetails.PreliminaryActionObligation = PreliminaryActionObligation.Optional;
  391: 			}
  392: 		}
  393: 		else if (FactionManager.IsNeutralWithFaction(mainHero.MapFaction, settlement.MapFaction))
  394: 		{
  395: 			accessDetails = new AccessDetails
  396: 			{
  397: 				AccessLevel = AccessLevel.FullAccess,
  398: 				AccessMethod = AccessMethod.Direct
  399: 			};
  400: 			if (Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(settlement.MapFaction) || Campaign.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(settlement.MapFaction))
  401: 			{
  402: 				accessDetails.AccessLevel = AccessLevel.LimitedAccess;
  403: 				accessDetails.AccessMethod = AccessMethod.None;
  404: 				accessDetails.LimitedAccessSolution = LimitedAccessSolution.Disguise;
  405: 				accessDetails.AccessLimitationReason = AccessLimitationReason.CrimeRating;
  406: 			}
  407: 		}
  408: 		else if (FactionManager.IsAtWarAgainstFaction(mainHero.MapFaction, settlement.MapFaction))
  409: 		{
  410: 			accessDetails = new AccessDetails
  411: 			{
  412: 				AccessLevel = AccessLevel.LimitedAccess,
  413: 				LimitedAccessSolution = LimitedAccessSolution.Disguise,
  414: 				AccessLimitationReason = AccessLimitationReason.HostileFaction
  415: 			};
  416: 		}
  417: 	}


 succeeded in 1001ms:
  312: 	public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
  313: 	{
  314: 		bool flag = false;
  315: 		CharacterObject character = data[index].Character;
  316: 		bool isHero = character.IsHero;
  317: 		data[index].Number += countChange;
  318: 		int num = data[index].WoundedNumber + woundedCountChange;
  319: 		if (num > data[index].Number)
  320: 		{
  321: 			woundedCountChange += data[index].Number - num;
  322: 		}
  323: 		else if (num < 0)
  324: 		{
  325: 			TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCountsAtIndex", 415);
  326: 		}
  327: 		data[index].WoundedNumber += woundedCountChange;
  328: 		if (xpChange != 0)
  329: 		{
  330: 			SetElementXp(index, data[index].Xp + xpChange);
  331: 		}
  332: 		if (isHero)
  333: 		{
  334: 			_totalHeroes += countChange;
  335: 			if (character.HeroObject.IsWounded)
  336: 			{
  337: 				_totalWoundedHeroes += countChange;
  338: 			}
  339: 			if (countChange != 0)
  340: 			{
  341: 				flag = true;
  342: 			}
  343: 		}
  344: 		else
  345: 		{
  346: 			_totalWoundedRegulars += woundedCountChange;
  347: 			_totalRegulars += countChange;
  348: 		}
  349: 		if (removeDepleted && data[index].Number == 0)
  350: 		{
  351: 			RemoveRange(index, index + 1);
  352: 			index = -1;
  353: 		}
  354: 		if (OwnerParty != null && isHero)
  355: 		{
  356: 			if (countChange > 0)
  357: 			{
  358: 				OwnerParty.OnHeroAdded(character.HeroObject, this);
  359: 			}
  360: 			else if (countChange < 0)
  361: 			{
  362: 				OwnerParty.OnHeroRemoved(character.HeroObject, this);
  363: 			}
  364: 		}
  365: 		if (countChange != 0 || flag)
  366: 		{
  367: 			OwnerParty?.OnRosterSizeChanged(this);
  368: 		}
  369: 		if (countChange != 0 || woundedCountChange != 0)
  370: 		{
  371: 			UpdateVersion();
  372: 		}
  373: 		return index;

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


 succeeded in 990ms:
   80:     // --- Menu ---
   81: 
   82:     private bool MenuCondition(MenuCallbackArgs args)
   83:     {
   84:         if (!_service.IsEnabled) return false;
   85:         var settlement = Settlement.CurrentSettlement;
   86:         if (settlement == null || !_service.IsKeySettlement(settlement.StringId)) return false;
   87: 
   88:         var owner = _ownerAdapter.GetOwnerInfo(settlement);
   89:         bool hasOffers = _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
   90:         if (!hasOffers && _settings.HideWhenNoResource)
   91:             return false;
   92: 
   93:         args.optionLeaveType = GameMenuOption.LeaveType.Trade;
   94:         return MenuHelper.SetOptionProperties(args, hasOffers, !hasOffers,
   95:             new TextObject("{=taom_emissary_no_trade}There is no elite trade for this faction here."));
   96:     }

  167:     private bool BuyCondition()
  168:     {
  169:         var settlement = Settlement.CurrentSettlement;
  170:         if (settlement == null) return false;
  171:         var owner = _ownerAdapter.GetOwnerInfo(settlement);
  172:         return _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
  173:     }
  174: 
  175:     private void BuyConsequence()
  176:     {
  177:         var settlement = Settlement.CurrentSettlement;
  178:         if (settlement != null)
  179:             _presenter.OpenTroopList(settlement);
  180:     }


 succeeded in 1235ms:



[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … viors\EncounterGameMenuBehavior.cs'; $lines=[36;1mGet-Content $path[0m; foreac …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                               ~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\EncounterGameMenuBehavior.cs' because it does not exist.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'EncounterGameMenuBehavior.cs' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 817ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs

codex
I’m not finding evidence of roster corruption from over-limit grants. The vanilla roster add updates counts and party-size events without consulting party capacity; the standard volunteer UI itself offers an over-limit confirmation and then still adds troops.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\EncounterGameMenuBehavior.cs'; "'$lines=Get-Content $path; foreach($range in @(@(200,210),@(1340,1350),@(2488,2528))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main\\Features\\SpecialResources\\SpecialResourceService.cs'; "'$lines=Get-Content $path; foreach($range in @(@(28,59),@(243,257),@(260,280))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main\\Features\\EliteEmissary\\EliteEmissaryConfigProvider.cs'; "'$lines=Get-Content $path; foreach($range in @(@(23,30),@(90,124),@(127,135))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main\\Features\\EliteEmissary\\EliteEmissarySettingsProvider.cs'; "'$lines=Get-Content $path; for($i=14;$i -le 19;$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 712ms:
  200: 		gameSystemInitializer.AddGameMenuOption("try_to_get_away_debrief", "try_to_get_away_continue", "{=veWOovVv}Continue...", game_menu_try_to_get_away_continue_on_condition, game_menu_try_to_get_away_end);
  201: 		gameSystemInitializer.AddGameMenu("assault_town", "", game_menu_town_assault_on_init);
  202: 		gameSystemInitializer.AddGameMenu("assault_town_order_attack", "", game_menu_town_assault_order_attack_on_init);
  203: 		gameSystemInitializer.AddGameMenu("town_outside", "{=!}{TOWN_TEXT}", game_menu_town_outside_on_init);
  204: 		gameSystemInitializer.AddGameMenuOption("town_outside", "approach_gates", "{=XlbDnuJx}Approach the gates and hail the guard.", game_menu_castle_outside_approach_gates_on_condition, game_menu_town_outside_approach_gates_on_consequence);
  205: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_disguise_yourself", "{=VCREeAF1}Disguise yourself and sneak through the gate.", game_menu_town_disguise_yourself_on_condition, game_menu_town_initial_disguise_yourself_on_consequence);
  206: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_besiege", "{=WdIGdHuL}Besiege the town.", game_menu_town_town_besiege_on_condition, game_menu_town_town_besiege_on_consequence);
  207: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_enter_cheat", "{=!}Enter town (Cheat).", game_menu_town_outside_cheat_enter_on_condition, game_menu_town_outside_enter_on_consequence);
  208: 		gameSystemInitializer.AddGameMenuOption("town_outside", "town_outside_leave", "{=2YYRyrOO}Leave...", game_menu_leave_on_condition, game_menu_castle_outside_leave_on_consequence, isLeave: true);
  209: 		gameSystemInitializer.AddGameMenu("disguise_blocked_night_time", "{=KZ27sSXS}With increased security at night guards check the identity of every entry. You can't sneak in during the night.", null);
  210: 		gameSystemInitializer.AddGameMenuOption("disguise_blocked_night_time", "back", GameTexts.FindText("str_back").ToString(), game_menu_leave_on_condition, delegate

 1340: 	{
 1341: 		PlayerEncounter.Finish();
 1342: 	}
 1343: 
 1344: 	public static void menu_sneak_into_town_succeeded_continue_on_consequence(MenuCallbackArgs args)
 1345: 	{
 1346: 		GameMenu.SwitchToMenu("town");
 1347: 	}
 1348: 
 1349: 	public static bool menu_sneak_into_town_succeeded_continue_on_condition(MenuCallbackArgs args)
 1350: 	{

 2488: 	{
 2489: 		GameMenu.SwitchToMenu("taken_prisoner");
 2490: 	}
 2491: 
 2492: 	private bool game_menu_town_disguise_yourself_on_condition(MenuCallbackArgs args)
 2493: 	{
 2494: 		args.optionLeaveType = GameMenuOption.LeaveType.SneakIn;
 2495: 		MBTextManager.SetTextVariable("SNEAK_CHANCE", MathF.Round(Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) * 100f));
 2496: 		if (_accessDetails.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess)
 2497: 		{
 2498: 			return _accessDetails.LimitedAccessSolution == SettlementAccessModel.LimitedAccessSolution.Disguise;
 2499: 		}
 2500: 		return false;
 2501: 	}
 2502: 
 2503: 	private void game_menu_town_initial_disguise_yourself_on_consequence(MenuCallbackArgs args)
 2504: 	{
 2505: 		if (CampaignTime.Now.IsNightTime)
 2506: 		{
 2507: 			GameMenu.SwitchToMenu("disguise_blocked_night_time");
 2508: 		}
 2509: 		else
 2510: 		{
 2511: 			GameMenu.SwitchToMenu(_alreadySneakedSettlements.Contains(Settlement.CurrentSettlement) ? "disguise_not_first_time" : "disguise_first_time");
 2512: 		}
 2513: 	}
 2514: 
 2515: 	private void game_menu_town_disguise_yourself_on_consequence(MenuCallbackArgs args)
 2516: 	{
 2517: 		bool num = Campaign.Current.Models.DisguiseDetectionModel.CalculateDisguiseDetectionProbability(Settlement.CurrentSettlement) > MBRandom.RandomFloat;
 2518: 		SkillLevelingManager.OnMainHeroDisguised(num);
 2519: 		Campaign.Current.IsMainHeroDisguised = true;
 2520: 		if (num)
 2521: 		{
 2522: 			GameMenu.SwitchToMenu("menu_sneak_into_town_succeeded");
 2523: 		}
 2524: 		else
 2525: 		{
 2526: 			GameMenu.SwitchToMenu("menu_sneak_into_town_caught");
 2527: 		}
 2528: 	}


 succeeded in 716ms:
   23:     // Owner-culture StringIds an emissary offer block may be keyed by. Source: CLAUDE.md / xml-data.md
   24:     // culture table (custom LOTR cultures + XSLT engine-id cultures + the two orc-host cultures).
   25:     private static readonly HashSet<string> KnownCultureIds = new(StringComparer.Ordinal)
   26:     {
   27:         "gondor", "mordor", "erebor", "rivendell", "lothlorien", "mirkwood",
   28:         "isengard", "gundabad", "dolguldur", "umbar", "goblin", "mistymountainorcs",
   29:         "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania",
   30:     };

   90:                 foreach (var c in offersEl.Elements("Culture"))
   91:                 {
   92:                     var cultureId = c.Attribute("id")?.Value?.Trim();
   93:                     if (string.IsNullOrEmpty(cultureId))
   94:                     {
   95:                         _logger.LogWarning("EliteEmissaryConfigProvider: <Culture> with empty id — skipped");
   96:                         continue;
   97:                     }
   98:                     if (!KnownCultureIds.Contains(cultureId))
   99:                     {
  100:                         _logger.LogWarning($"EliteEmissaryConfigProvider: unknown culture id '{cultureId}' — dropped (offers for it would never be reachable)");
  101:                         droppedCultures++;
  102:                         continue;
  103:                     }
  104:                     if (cultureOffers.ContainsKey(cultureId))
  105:                     {
  106:                         _logger.LogWarning($"EliteEmissaryConfigProvider: duplicate <Culture id='{cultureId}'> — keeping the first block, ignoring the rest");
  107:                         continue;
  108:                     }
  109: 
  110:                     var troops = new List<string>();
  111:                     foreach (var t in c.Elements("Troop"))
  112:                     {
  113:                         var troopId = t.Attribute("id")?.Value?.Trim();
  114:                         if (string.IsNullOrEmpty(troopId))
  115:                             continue;
  116: 
  117:                         var cost = _resourceConfig.GetTroopCost(troopId);
  118:                         if (cost == null || cost.MerchantCost <= 0)
  119:                         {
  120:                             _logger.LogWarning($"EliteEmissaryConfigProvider: offer '{troopId}' (culture {cultureId}) has no merchant_cost in troop_resource_costs.xml — dropped (unsellable)");
  121:                             droppedTroops++;
  122:                             continue;
  123:                         }
  124:                         troops.Add(troopId);

  127:                     if (troops.Count > 0)
  128:                         cultureOffers[cultureId] = troops;
  129:                     else
  130:                         _logger.LogWarning($"EliteEmissaryConfigProvider: culture '{cultureId}' has no sellable offers after validation — not recorded");
  131:                 }
  132:             }
  133: 
  134:             _logger.LogInfo($"EliteEmissaryConfigProvider: loaded {keySettlements.Count} key settlement(s), {cultureOffers.Count} culture offer list(s) (enabled={enabled}; dropped {droppedCultures} culture(s), {droppedTroops} troop(s))");
  135:             return new EliteEmissaryConfig(enabled, keySettlements, cultureOffers);


 succeeded in 717ms:
   28:     public SpecialResource ResolveResource(string kingdomId, string cultureId)
   29:     {
   30:         // Resolve is hot-path (called from MapInfoVM.OnRefresh tooltip rebuild several times per tick).
   31:         // Dedupe DEBUG logging by (kingdomId, cultureId) so we keep diagnostics on transitions
   32:         // without flooding the log with thousands of identical lines per session.
   33:         var key = (kingdomId ?? "") + "|" + (cultureId ?? "");
   34:         var firstSeen = _loggedResolveKeys.Add(key);
   35: 
   36:         if (kingdomId != null)
   37:         {
   38:             var byKingdom = _config.GetByKingdomId(kingdomId);
   39:             if (byKingdom != null)
   40:             {
   41:                 if (firstSeen)
   42:                     _logger.LogDebug($"[SpecRes] Resolved resource '{byKingdom.Id}' via kingdom '{kingdomId}'");
   43:                 return byKingdom;
   44:             }
   45:         }
   46:         if (cultureId != null)
   47:         {
   48:             var byCulture = _config.GetByCultureId(cultureId);
   49:             if (byCulture != null)
   50:             {
   51:                 if (firstSeen)
   52:                     _logger.LogDebug($"[SpecRes] Resolved resource '{byCulture.Id}' via culture '{cultureId}' (kingdom '{kingdomId}' had no match)");
   53:                 return byCulture;
   54:             }
   55:         }
   56:         if (firstSeen)
   57:             _logger.LogDebug($"[SpecRes] No resource resolved for kingdom='{kingdomId}', culture='{cultureId}'");
   58:         return null;
   59:     }

  243:     public bool CanAffordMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
  244:     {
  245:         if (count <= 0) return true;
  246: 
  247:         var resource = ResolveResource(kingdomId, cultureId);
  248:         if (resource == null) return true;
  249: 
  250:         var cost = _config.GetTroopCost(troopId);
  251:         if (cost == null || cost.MerchantCost <= 0) return true;
  252: 
  253:         var totalCost = cost.MerchantCost * count;
  254:         var available = _storage.Get(heroId, resource.Id);
  255:         var canAfford = available >= totalCost;
  256:         _logger.LogDebug($"[SpecRes] CanAffordMerchant: {troopId} x{count} cost={totalCost} {resource.DisplayName} available={available:F0} → {canAfford}");
  257:         return canAfford;

  260:     public void ChargeMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
  261:     {
  262:         if (count <= 0) return;
  263: 
  264:         var resource = ResolveResource(kingdomId, cultureId);
  265:         if (resource == null)
  266:         {
  267:             _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} x{count} — kingdom='{kingdomId}' culture='{cultureId}' maps to no resource");
  268:             return;
  269:         }
  270: 
  271:         var cost = _config.GetTroopCost(troopId);
  272:         if (cost == null || cost.MerchantCost <= 0)
  273:         {
  274:             _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} has no merchant_cost");
  275:             return;
  276:         }
  277: 
  278:         var totalCost = cost.MerchantCost * count;
  279:         _storage.Add(heroId, resource.Id, -totalCost);
  280:         _logger.LogInfo($"[SpecRes] MERCHANT: -{totalCost} {resource.DisplayName} for {troopId} x{count} (balance now {_storage.Get(heroId, resource.Id):F0})");


 succeeded in 759ms:
   14:         _defaultEnabled = configProvider.GetConfig().Enabled;
   15:     }
   16: 
   17:     public bool IsEnabled => TaomSettings.Instance?.EnableEliteEmissary ?? _defaultEnabled;
   18: 
   19:     public bool HideWhenNoResource => TaomSettings.Instance?.HideEmissaryWhenNoResource ?? true;

codex
The greeting-flag fix checks out against the map-conversation lifecycle: `OpenConversation` enters the map conversation manager, and `EndConversation` dispatches `OnConversationEnded` before clearing conversation agents. The added listener clears the pending hero id on that event.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p1='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\CampaignMapConversation.cs'; "'$l=Get-Content $p1; for($i=1;$i -le 12;$i++){ '"'{0,5}: {1}' -f "'$i,$l[$i-1] }; '"'' ; "'$p2='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Conversation\\ConversationManager.cs'; "'$l=Get-Content $p2; foreach($range in @(@(760,803),@(1092,1100))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$l[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignEvents.cs'; "'$l=Get-Content $p; foreach($range in @(@(768,772),@(1871,1876))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$l[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\EliteEmissary\\Hooks\\EliteEmissaryInquiryPresenter.cs'; "'$l=Get-Content $p; foreach($range in @(@(46,52),@(99,116),@(124,135),@(168,186))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$l[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\Features\\EliteEmissary\\Hooks\\EliteEmissaryBehavior.cs'; "'$l=Get-Content $p; foreach($range in @(@(51,59),@(146,165))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$l[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 729ms:
   46:             var owner = _ownerAdapter.GetOwnerInfo(settlement);
   47:             _logger.LogInfo($"[EliteEmissary] OpenTroopList at {owner.SettlementId}: owner kingdom='{owner.OwnerKingdomId}' culture='{owner.OwnerCultureId}'");
   48: 
   49:             var offerList = _service.BuildOfferList(heroId, owner.OwnerKingdomId, owner.OwnerCultureId);
   50:             if (offerList.NoResource)
   51:             {
   52:                 Notify("{=taom_emissary_no_resource}There is no emissary trade in this settlement.");

   99:             var data = new MultiSelectionInquiryData(
  100:                 titleText: new TextObject("{=taom_emissary_list_title}Elite Units").ToString(),
  101:                 descriptionText: desc,
  102:                 inquiryElements: elements,
  103:                 isExitShown: true,
  104:                 minSelectableOptionCount: 1,
  105:                 maxSelectableOptionCount: 1,
  106:                 affirmativeText: new TextObject("{=taom_emissary_select}Select").ToString(),
  107:                 negativeText: new TextObject("{=taom_emissary_back}Back").ToString(),
  108:                 affirmativeAction: chosen =>
  109:                 {
  110:                     if (chosen == null || chosen.Count == 0) return;
  111:                     if (chosen[0].Identifier is EmissaryTroopOffer offer)
  112:                         OpenQuantityPicker(settlement, owner, heroId, offer, offerList.ResourceDisplayName, offerList.PlayerBalance);
  113:                 },
  114:                 negativeAction: _ => { });
  115: 
  116:             MBInformationManager.ShowMultiSelectionInquiry(data);

  124:     private void OpenQuantityPicker(Settlement settlement, SettlementOwnerInfo owner, string heroId, EmissaryTroopOffer offer, string resourceName, float balance)
  125:     {
  126:         try
  127:         {
  128:             var character = MBObjectManager.Instance?.GetObject<CharacterObject>(offer.TroopId);
  129:             var troopName = character?.Name?.ToString() ?? offer.TroopId;
  130: 
  131:             var quantities = new List<int>();
  132:             foreach (var q in new[] { 1, 5, 10, offer.MaxAffordableQuantity })
  133:                 if (q >= 1 && !quantities.Contains(q))
  134:                     quantities.Add(q);
  135:             quantities.Sort();

  168:                 affirmativeAction: chosen =>
  169:                 {
  170:                     if (chosen == null || chosen.Count == 0) return;
  171:                     if (chosen[0].Identifier is int qty)
  172:                         ExecutePurchase(owner, heroId, offer.TroopId, qty);
  173:                 },
  174:                 negativeAction: _ => { });
  175: 
  176:             MBInformationManager.ShowMultiSelectionInquiry(data);
  177:         }
  178:         catch (Exception ex)
  179:         {
  180:             _logger.LogError($"[EliteEmissary] OpenQuantityPicker failed: {ex.Message}");
  181:         }
  182:     }
  183: 
  184:     private void ExecutePurchase(SettlementOwnerInfo owner, string heroId, string troopId, int qty)
  185:     {
  186:         var result = _service.Purchase(heroId, owner.OwnerKingdomId, owner.OwnerCultureId, troopId, qty);


 succeeded in 791ms:
    1: namespace TaleWorlds.CampaignSystem.Conversation;
    2: 
    3: public static class CampaignMapConversation
    4: {
    5: 	public static void OpenConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
    6: 	{
    7: 		Campaign.Current.ConversationManager.OpenMapConversation(playerCharacterData, conversationPartnerData);
    8: 	}
    9: }
   10: 
   11: 
   12: 

  760: 	}
  761: 
  762: 	public void EndConversation()
  763: 	{
  764: 		Debug.Print("--------------- Conversation End --------------- ", 0, Debug.DebugColor.White, 4503599627370496uL);
  765: 		if (CampaignMission.Current != null)
  766: 		{
  767: 			foreach (IAgent conversationAgent in ConversationAgents)
  768: 			{
  769: 				CampaignMission.Current.OnConversationEnd(conversationAgent);
  770: 			}
  771: 		}
  772: 		_conversationParty = null;
  773: 		if (this.ConversationEndOneShot != null)
  774: 		{
  775: 			this.ConversationEndOneShot();
  776: 			this.ConversationEndOneShot = null;
  777: 		}
  778: 		if (this.ConversationEnd != null)
  779: 		{
  780: 			this.ConversationEnd();
  781: 		}
  782: 		IsConversationInProgress = false;
  783: 		foreach (IAgent conversationAgent2 in ConversationAgents)
  784: 		{
  785: 			conversationAgent2.SetAsConversationAgent(set: false);
  786: 		}
  787: 		Campaign.Current.CurrentConversationContext = ConversationContext.Default;
  788: 		CampaignEventDispatcher.Instance.OnConversationEnded(ConversationCharacters);
  789: 		if (GetPersuasionIsActive())
  790: 		{
  791: 			EndPersuasion();
  792: 		}
  793: 		_conversationAgents.Clear();
  794: 		_speakerAgent = null;
  795: 		_listenerAgent = null;
  796: 		_mainAgent = null;
  797: 		if (IsConversationFlowActive)
  798: 		{
  799: 			OnConversationDeactivate();
  800: 		}
  801: 		Handler?.OnConversationUninstall();
  802: 	}
  803: 

 1092: 	}
 1093: 
 1094: 	public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
 1095: 	{
 1096: 		(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(playerCharacterData, conversationPartnerData);
 1097: 		SetupAndStartMapConversation(conversationPartnerData.Party?.MobileParty, new MapConversationAgent(conversationPartnerData.Character), new MapConversationAgent(CharacterObject.PlayerCharacter));
 1098: 	}
 1099: 
 1100: 	public static void StartPersuasion(float goalValue, float successValue, float failValue, float criticalSuccessValue, float criticalFailValue, float initialProgress = -1f, PersuasionDifficulty difficulty = PersuasionDifficulty.Medium)


 succeeded in 774ms:
   51:     public override void RegisterEvents()
   52:     {
   53:         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
   54:         // Belt to GreetConsequence's clear (deep-review 2026-06-25): guarantees the greeting flag never
   55:         // survives a conversation, so a stale flag can't make the emissary greeting appear in a later
   56:         // normal chat with the same notable (the rare case where a higher-priority vanilla "start" line —
   57:         // e.g. an active counter-offer issue with that notable — wins and our greet consequence never fires).
   58:         CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
   59:     }

  146:     private bool GreetCondition()
  147:     {
  148:         return _pendingEmissaryHeroId != null
  149:             && Hero.OneToOneConversationHero?.StringId == _pendingEmissaryHeroId;
  150:     }
  151: 
  152:     private void GreetConsequence()
  153:     {
  154:         // Clear so a later NORMAL conversation with the same notable doesn't show the emissary greeting.
  155:         _pendingEmissaryHeroId = null;
  156:     }
  157: 
  158:     private void OnConversationEnded(IEnumerable<CharacterObject> characters)
  159:     {
  160:         // Bulletproof clear: after ANY conversation ends, the greeting flag is reset. Covers the case
  161:         // where our greet line never fired (a higher-priority vanilla start line won), which would
  162:         // otherwise leave _pendingEmissaryHeroId set and leak the emissary greeting into the next
  163:         // normal conversation with that notable.
  164:         _pendingEmissaryHeroId = null;
  165:     }


 succeeded in 907ms:


[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … leWorlds.CampaignSystem.CampaignEvents.cs'; $l=[36;1mGet-Content $p[0m; foreac …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                  ~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignEvents.cs' because it does not exist.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … $range[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$l[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                     ~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\TaleWorlds.MountAndBlade.GauntletUI\\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Multiplayer\\MultiplayerOptions\\MultiSelectionQueryPopUpVM.cs'; if("'!(Test-Path $path)){ Get-ChildItem -Path '"'E:\\Decompiled_Bannerlord' -Recurse -Filter 'MultiSelectionQueryPopUpVM.cs' | ForEach-Object { "'$_.FullName } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 674ms:
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.ViewModelCollection\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\MultiSelectionQueryPopUpVM.cs

codex
I’m checking one last edge on the inquiry path: whether the UI itself gives atomic, one-shot callbacks. Regardless of UI behavior, the purchase service revalidates troop, quantity, cost, resource, and affordability before granting.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade.ViewModelCollection\\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\\MultiSelectionQueryPopUpVM.cs'; "'$lines=Get-Content $path; foreach($range in @(@(141,162),@(164,203))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 671ms:
  141: 	public void SetData(MultiSelectionInquiryData data)
  142: 	{
  143: 		_data = data;
  144: 		InquiryElements.Clear();
  145: 		foreach (InquiryElement inquiryElement in _data.InquiryElements)
  146: 		{
  147: 			TextObject hint = (string.IsNullOrEmpty(inquiryElement.Hint) ? TextObject.GetEmpty() : new TextObject("{=!}" + inquiryElement.Hint));
  148: 			InquiryElementVM item = new InquiryElementVM(inquiryElement, hint, OnInquiryElementSelected);
  149: 			InquiryElements.Add(item);
  150: 		}
  151: 		base.TitleText = _data.TitleText;
  152: 		base.PopUpLabel = _data.DescriptionText;
  153: 		MaxSelectableOptionCount = _data.MaxSelectableOptionCount;
  154: 		MinSelectableOptionCount = _data.MinSelectableOptionCount;
  155: 		base.ButtonOkLabel = _data.AffirmativeText;
  156: 		base.ButtonCancelLabel = _data.NegativeText;
  157: 		base.IsButtonOkShown = true;
  158: 		base.IsButtonCancelShown = _data.IsExitShown;
  159: 		IsSearchAvailable = _data.IsSeachAvailable;
  160: 		SearchPlaceholderText = new TextObject("{=tQOPRBFg}Search...").ToString();
  161: 		RefreshIsButtonOkEnabled();
  162: 	}

  164: 	private void OnInquiryElementSelected(InquiryElementVM elementVM, bool isSelected)
  165: 	{
  166: 		if (isSelected)
  167: 		{
  168: 			_selectedOptionCount++;
  169: 			if (MaxSelectableOptionCount == 1)
  170: 			{
  171: 				foreach (InquiryElementVM inquiryElement in InquiryElements)
  172: 				{
  173: 					if (inquiryElement != elementVM)
  174: 					{
  175: 						inquiryElement.IsSelected = false;
  176: 					}
  177: 				}
  178: 			}
  179: 		}
  180: 		else
  181: 		{
  182: 			_selectedOptionCount--;
  183: 		}
  184: 		RefreshIsButtonOkEnabled();
  185: 	}
  186: 
  187: 	public override void ExecuteAffirmativeAction()
  188: 	{
  189: 		if (_data.AffirmativeAction != null)
  190: 		{
  191: 			List<InquiryElement> list = new List<InquiryElement>();
  192: 			foreach (InquiryElementVM inquiryElement in InquiryElements)
  193: 			{
  194: 				if (inquiryElement.IsSelected)
  195: 				{
  196: 					list.Add(inquiryElement.InquiryElement);
  197: 				}
  198: 			}
  199: 			_data.AffirmativeAction(list);
  200: 		}
  201: 		CloseQuery();
  202: 	}
  203: 


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade.ViewModelCollection\\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\\InquiryElementVM.cs'; "'$lines=Get-Content $path; foreach($range in @(@(1,40),@(110,170))){ for($i=$range[0];$i -le $range[1];$i++){ '"'{0,5}: {1}' -f "'$i,$lines[$i-1] }; '"'' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 876ms:


[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … tion.Inquiries\InquiryElementVM.cs'; $lines=[36;1mGet-Content $path[0m; foreac …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                               ~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.ViewModelCollection\TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries\InquiryElementVM.cs' because it does not exist.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … ge[0];$i -le $range[1];$i++){ [36;1m'{0,5}: {1}' -f $i,$lines[$i-1][0m }; '' }[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

codex
**1. Known Suspects**

- S1: CONFIRMED for the normal UI path. `Purchase` validates, checks affordance, grants, then charges in that order: `Main/Features/EliteEmissary/EliteEmissaryService.cs:95-131`. `PlayerPartyAdapter.GrantTroop` only returns `true` after `MemberRoster.AddToCounts`: `Main/Adapters/PlayerPartyAdapter.cs:21-44`. No confirmed charge-without-grant path.
- S2: CONFIRMED. Inquiry identifiers are UI transport only; final purchase revalidates quantity, troop offer membership, cost, resource, and affordability: `EliteEmissaryInquiryPresenter.cs:108-112,168-172`; `EliteEmissaryService.cs:95-122`.
- S3: CONFIRMED. Presenter captures owner once and passes the same owner tuple through display, quantity picker, and purchase: `EliteEmissaryInquiryPresenter.cs:46-49,108-112,168-186`.
- S4: DISPUTED. Culture id and merchant cost are validated, but culture-to-resource resolvability is not. `goblin` / `mistymountainorcs` are accepted known culture ids at `EliteEmissaryConfigProvider.cs:27-29,98-128`, but no resource mapping would make the offer silently dead behind `HideWhenNoResource`.
- S5: CONFIRMED. Merchant purchase uses `MerchantCost`: `SpecialResourceService.cs:271-279`; recruit charge uses `RecruitCost`: `SpecialResourceService.cs:202-215`. No duplicate `<Troop id>` rows found; the dual-cost spider/elephant rows are single rows at `troop_resource_costs.xml:23-24`.
- S6: CONFIRMED. Parsed all 54 offer troop ids: all have `merchant_cost`, all resolve to real `CharacterObject`s, all offer cultures map to resources, and all 11 key settlements exist in TAOM_Map.

**2. Verify Fixes/Decisions**

1. Greeting flag fix: CLEAN. `ConversationEnded` is registered and clears `_pendingEmissaryHeroId`: `EliteEmissaryBehavior.cs:53-58,158-164`. Decompiled v1.4.6 `CampaignMapConversation.OpenConversation` enters `ConversationManager.OpenMapConversation`, and `ConversationManager.EndConversation` dispatches `OnConversationEnded` before clearing agents.
2. Culture offers + kingdom-first resource: CONSCIOUS CALL HOLDS, but it is reachable, not impossible. `SettlementOwnerAdapter` uses owner kingdom and owner culture: `SettlementOwnerAdapter.cs:31-34`; `ResolveResource` is kingdom-first: `SpecialResourceService.cs:36-54`. A defecting/conquered owner can pay kingdom currency for owner-culture offers.
3. No war/relation gate: DISPUTED. See HIGH finding below. Decompiled v1.4.6 allows hostile town entry by disguise, then switches to the normal `town` menu.
4. No party-size cap: CLEAN. Vanilla `RecruitmentVM` itself allows over-limit confirmation and still calls `MobileParty.MainParty.MemberRoster.AddToCounts`: decompiled `RecruitmentVM.cs:867-895`. `TroopRoster.AddToCounts` has no party-cap check.
5. Spider/elephant direct grant: CLEAN. Vanilla volunteer recruitment grants by `MemberRoster.AddToCounts(item.Character, 1)` then fires `OnUnitRecruited`: decompiled `RecruitmentVM.cs:891-895`. Emissary uses the same roster grant API: `PlayerPartyAdapter.cs:42`.

**3. Config Cross-Reference**

- Offers: all offer troops in `elite_emissary_config.xml:26-111` have merchant prices in `troop_resource_costs.xml:23-24,37-103`.
- Real troop refs: `taom_spider_creature` exists at `characters/spider_creature.xml:22`; `harad_elephant_rider` exists at `troops/troops_harad.xml:146`; the remaining 52 offer ids also resolved.
- Cultures: offer cultures `gondor,mordor,erebor,dolguldur,isengard,gundabad,mirkwood,rivendell,vlandia,khuzait,aserai` all map to resources in `special_resources_config.xml`. `goblin` and `mistymountainorcs` correctly have no offers.
- Key settlements: all ids from `elite_emissary_config.xml:11-23` exist in TAOM_Map: `town_ES1`, `town_EW1`, `town_A1`, `town_V1`, `town_DG1`, `town_E1`, `town_G1`, `town_isengard`, `town_M1`, `town_R1`, `town_RU1`.
- Localization: all code-used `taom_emissary_*` keys are present in `taom_emissary_strings.xml:5-31`.

**4. New Findings**

[HIGH] Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs:82 — War/relation gate — Hostile key towns can still reach the normal `town` menu via vanilla disguise entry, and the emissary menu/buy conditions only check key settlement + offers, not war/disguise/trade eligibility. Decompiled v1.4.6 evidence: hostile towns get `LimitedAccessSolution.Disguise`; the disguise option exists on `town_outside`; successful sneak flow switches to `town`. Fix: add an explicit settlement access gate in `MenuCondition`, `BuyCondition`, and before `ExecutePurchase`, rejecting at-war/disguised hostile access with a disabled tooltip or hiding the option.

[MEDIUM] Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs:98 — Config validation completeness — A known-but-unresourced culture block can be accepted if its troops have `merchant_cost`, then silently become unreachable because `ResolveResource` returns null and `HideWhenNoResource` defaults true. Fix: validate each offer culture resolves to a special resource at load time, or warn/drop no-resource culture blocks; add a regression test for `goblin` / `mistymountainorcs`.

**5. Findings Or Observations**

No CRITICAL findings. No broken config references. No confirmed inquiry tampering, wrong-cost-field, duplicate-cost-row, or creature-grant defect.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 51276 (child process of PID 54496) has been terminated.
SUCCESS: The process with PID 54496 (child process of PID 54116) has been terminated.
SUCCESS: The process with PID 54116 (child process of PID 83408) has been terminated.
SUCCESS: The process with PID 83408 (child process of PID 81800) has been terminated.
SUCCESS: The process with PID 55120 (child process of PID 32656) has been terminated.
SUCCESS: The process with PID 32656 (child process of PID 53268) has been terminated.
SUCCESS: The process with PID 53268 (child process of PID 40412) has been terminated.
SUCCESS: The process with PID 40412 (child process of PID 48860) has been terminated.
SUCCESS: The process with PID 48860 (child process of PID 81800) has been terminated.
tokens used
376,307
**1. Known Suspects**

- S1: CONFIRMED for the normal UI path. `Purchase` validates, checks affordance, grants, then charges in that order: `Main/Features/EliteEmissary/EliteEmissaryService.cs:95-131`. `PlayerPartyAdapter.GrantTroop` only returns `true` after `MemberRoster.AddToCounts`: `Main/Adapters/PlayerPartyAdapter.cs:21-44`. No confirmed charge-without-grant path.
- S2: CONFIRMED. Inquiry identifiers are UI transport only; final purchase revalidates quantity, troop offer membership, cost, resource, and affordability: `EliteEmissaryInquiryPresenter.cs:108-112,168-172`; `EliteEmissaryService.cs:95-122`.
- S3: CONFIRMED. Presenter captures owner once and passes the same owner tuple through display, quantity picker, and purchase: `EliteEmissaryInquiryPresenter.cs:46-49,108-112,168-186`.
- S4: DISPUTED. Culture id and merchant cost are validated, but culture-to-resource resolvability is not. `goblin` / `mistymountainorcs` are accepted known culture ids at `EliteEmissaryConfigProvider.cs:27-29,98-128`, but no resource mapping would make the offer silently dead behind `HideWhenNoResource`.
- S5: CONFIRMED. Merchant purchase uses `MerchantCost`: `SpecialResourceService.cs:271-279`; recruit charge uses `RecruitCost`: `SpecialResourceService.cs:202-215`. No duplicate `<Troop id>` rows found; the dual-cost spider/elephant rows are single rows at `troop_resource_costs.xml:23-24`.
- S6: CONFIRMED. Parsed all 54 offer troop ids: all have `merchant_cost`, all resolve to real `CharacterObject`s, all offer cultures map to resources, and all 11 key settlements exist in TAOM_Map.

**2. Verify Fixes/Decisions**

1. Greeting flag fix: CLEAN. `ConversationEnded` is registered and clears `_pendingEmissaryHeroId`: `EliteEmissaryBehavior.cs:53-58,158-164`. Decompiled v1.4.6 `CampaignMapConversation.OpenConversation` enters `ConversationManager.OpenMapConversation`, and `ConversationManager.EndConversation` dispatches `OnConversationEnded` before clearing agents.
2. Culture offers + kingdom-first resource: CONSCIOUS CALL HOLDS, but it is reachable, not impossible. `SettlementOwnerAdapter` uses owner kingdom and owner culture: `SettlementOwnerAdapter.cs:31-34`; `ResolveResource` is kingdom-first: `SpecialResourceService.cs:36-54`. A defecting/conquered owner can pay kingdom currency for owner-culture offers.
3. No war/relation gate: DISPUTED. See HIGH finding below. Decompiled v1.4.6 allows hostile town entry by disguise, then switches to the normal `town` menu.
4. No party-size cap: CLEAN. Vanilla `RecruitmentVM` itself allows over-limit confirmation and still calls `MobileParty.MainParty.MemberRoster.AddToCounts`: decompiled `RecruitmentVM.cs:867-895`. `TroopRoster.AddToCounts` has no party-cap check.
5. Spider/elephant direct grant: CLEAN. Vanilla volunteer recruitment grants by `MemberRoster.AddToCounts(item.Character, 1)` then fires `OnUnitRecruited`: decompiled `RecruitmentVM.cs:891-895`. Emissary uses the same roster grant API: `PlayerPartyAdapter.cs:42`.

**3. Config Cross-Reference**

- Offers: all offer troops in `elite_emissary_config.xml:26-111` have merchant prices in `troop_resource_costs.xml:23-24,37-103`.
- Real troop refs: `taom_spider_creature` exists at `characters/spider_creature.xml:22`; `harad_elephant_rider` exists at `troops/troops_harad.xml:146`; the remaining 52 offer ids also resolved.
- Cultures: offer cultures `gondor,mordor,erebor,dolguldur,isengard,gundabad,mirkwood,rivendell,vlandia,khuzait,aserai` all map to resources in `special_resources_config.xml`. `goblin` and `mistymountainorcs` correctly have no offers.
- Key settlements: all ids from `elite_emissary_config.xml:11-23` exist in TAOM_Map: `town_ES1`, `town_EW1`, `town_A1`, `town_V1`, `town_DG1`, `town_E1`, `town_G1`, `town_isengard`, `town_M1`, `town_R1`, `town_RU1`.
- Localization: all code-used `taom_emissary_*` keys are present in `taom_emissary_strings.xml:5-31`.

**4. New Findings**

[HIGH] Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs:82 — War/relation gate — Hostile key towns can still reach the normal `town` menu via vanilla disguise entry, and the emissary menu/buy conditions only check key settlement + offers, not war/disguise/trade eligibility. Decompiled v1.4.6 evidence: hostile towns get `LimitedAccessSolution.Disguise`; the disguise option exists on `town_outside`; successful sneak flow switches to `town`. Fix: add an explicit settlement access gate in `MenuCondition`, `BuyCondition`, and before `ExecutePurchase`, rejecting at-war/disguised hostile access with a disabled tooltip or hiding the option.

[MEDIUM] Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs:98 — Config validation completeness — A known-but-unresourced culture block can be accepted if its troops have `merchant_cost`, then silently become unreachable because `ResolveResource` returns null and `HideWhenNoResource` defaults true. Fix: validate each offer culture resolves to a special resource at load time, or warn/drop no-resource culture blocks; add a regression test for `goblin` / `mistymountainorcs`.

**5. Findings Or Observations**

No CRITICAL findings. No broken config references. No confirmed inquiry tampering, wrong-cost-field, duplicate-cost-row, or creature-grant defect.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0  
VERDICT: ISSUES FOUND
