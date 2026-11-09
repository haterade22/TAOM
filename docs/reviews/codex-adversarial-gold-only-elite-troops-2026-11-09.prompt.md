CODEX ADVERSARIAL REVIEW: #1108 gold-only elite troops

Project: TAOM, a Lord of the Rings total conversion for Bannerlord v1.5.6 (.NET Framework 4.7.2). The maintainer asked for this pass on 2026-11-09.

ROLE: review only. Do not edit, create, stage or delete any file. Do not run a git command that changes a ref, the index or the working tree. Do not run ./build.ps1, dotnet build or dotnet test: other sessions share this working tree. Read-only tools are fine: read files, git diff, git log, `pwsh tools/taom-src.ps1 path <Type>`, your ilspy server, and the python validators under tools/ that take no --apply.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-gold-only-elite-troops-2026-11-09.md. Do NOT write that path yourself.

STATUS OF THIS PASS: this reviews uncommitted files, so it informs the maintainer and cannot approve a commit. Do your own pass first: record your findings before you weigh the Known Suspects below, and do not open docs/reviews/rca-gold-only-elite-troops-2026-11-09.md until you have. Then read that RCA, do not re-report its fixed items, and confirm each fix in the source.

FEATURE: a new MCM option, EliteTroopsCostGoldOnly, off by default. With it on, SpecialResourceConfigProvider.GetTroopCost returns each troop's row with upgrade_cost, recruit_cost and daily_upkeep at 0 and merchant_cost kept. The Elite Emissary then sells for gold at merchant_cost times EmissaryGoldPerResource, and only to a buyer whose resolved special resource equals the settlement owner's.

REVIEW TARGET: the working tree on branch bannerlord-1.5.x, on top of commit d0d95bf27. Use `git diff HEAD -- <path>` for a modified file and read an untracked file directly. The FILES list below is the whole change. Other sessions have unrelated uncommitted files in this tree (MissionPerf, GraphicsOptions adapters, tools/perf_runs.py, tools/retopo_*, tools/petik, docs/audits/armory-ref-audit.md, tools/README.md, docs/reference/provenance-register.md): ignore them.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
AGENTS.md, .ai/roles/reviewer.md, .ai/report-format.md, .ai/review-reference.md
docs/features/special-resources.md, section "Gold-only option (#1108)"
docs/features/elite-emissary.md, bullet "Selling for gold" and section "MCM: group Special Resources (#1108)"
docs/features/coop-interop.md, section "What is NOT done"
Main/_Module/ModuleData/special_resources/troop_resource_costs.xml
Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml

DELIBERATE CHOICES (the maintainer approved these; report a defect in how they are built, do not re-argue them):
With the option off, nothing changes.
The seam is GetTroopCost returning a merchant-only row.
The emissary price is merchant_cost times EmissaryGoldPerResource (default 200, range 50 to 1000).
The emissary serves only a buyer whose resolved resource equals the settlement owner's.
Earning, the amount on the map bar and the armoury do not change.
While it is on, the two career perks for upkeep and upgrade cost do nothing, and the elephant and Mumakil upkeep drops to 0.
The option is per peer in co-op, like other TaomSettings toggles.

KNOWN SUSPECTS (hypotheses from the builder; CONFIRM or DISPUTE each with file:line evidence):
S1 Co-op mismatch. With the authority's value off and a client's value on, or the reverse, Patch26 or Patch51 lets a peer act on one value while the authority charges by the other. Does this drive a balance negative, charge twice, skip a charge the peer saw, or produce a desync? Trace RecruitChargePolicy.ChoosePayer, SpecialResourceService.ChargeRecruitCost, QueueUpgradeSpend, and SpecialResourceEventAdapter.SnapshotDaily.
S2 Hero identity. BuyerHoldsResource reads Hero.MainHero through IPlayerPartyAdapter.GetMainHeroFaction, while Purchase receives a heroId, and the purse reads Hero.MainHero. Can these differ (Player Switcher, enlisted service, co-op host)?
S3 Live flip. The option is read per call with no restart. Does a flip leave a label and a charge in disagreement between BuildOfferList and Purchase, between a party screen's queued upgrade spend and its commit, or across a daily tick?
S4 Non-null rows. Some readers treat a non-null row as "this troop costs a resource". Does any reader behave wrongly for a row with upgrade, recruit and upkeep at 0? Include SpecialResourceTroopBadge.Rows, EncyclopediaUnitBadgeMixin.BuildTooltip, PartyUpkeepReader.Collect, and any AI or economy logic.
S5 Gold path in EliteEmissaryService.Purchase. Can the float balance in BuildOfferList, the max-affordable arithmetic, int multiplication at the rate limits, or a refused ChargeGold after a grant produce free troops, a wrong result status or an unaffordable purchase?
S6 Cached settings object. SpecialResourceSettingsProvider caches TaomSettings.Instance in a private lazy accessor. Can MCM replace that object after the first read (preset change, reset, reload, a new campaign), freezing the option at an old value?

FILES:
Production C#:
Main/Features/SpecialResources/ISpecialResourceSettingsProvider.cs (new)
Main/Features/SpecialResources/SpecialResourceSettingsProvider.cs (new)
Main/Features/SpecialResources/SpecialResourceConfigProvider.cs
Main/Features/SpecialResources/SpecialResourcesIoC.cs
Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs
Main/Features/EliteEmissary/EliteEmissaryService.cs
Main/Features/EliteEmissary/IEliteEmissaryService.cs
Main/Features/EliteEmissary/Hooks/EliteEmissaryInquiryPresenter.cs
Main/Features/EliteEmissary/Domain/EmissaryOfferList.cs
Main/Features/EliteEmissary/Domain/EmissaryPurchaseResult.cs
Main/Features/EliteEmissary/Domain/EmissaryTroopOffer.cs
Main/Adapters/IPlayerPartyAdapter.cs (GetMainHeroFaction only)
Main/Adapters/PlayerPartyAdapter.cs (GetMainHeroFaction only)
Main/Features/TaomSettings.cs (only the "Special Resources (#1108)" block at the end of the class)
Tests:
TAOM.Tests/Features/SpecialResources/SpecialResourceGoldOnlyTests.cs (new)
TAOM.Tests/Features/SpecialResources/SpecialResourceSettingsProviderTests.cs (new)
TAOM.Tests/Features/EliteEmissary/EliteEmissaryServiceTests.cs
TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs (count pins)
TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs (count pin)
Data and strings:
Main/_Module/ModuleData/taom_emissary_strings.xml (key taom_emissary_denars)
Main/_Module/ModuleData/taom_module_strings.xml (four taom_specres_mcm_gold keys)
tools/ingame_checks/1108.toml (new)
Unchanged consumers you must read:
Main/Features/SpecialResources/SpecialResourceService.cs
Main/Features/SpecialResources/SpecialResourceEarningService.cs
Main/Features/SpecialResources/PartyUpkeepReader.cs
Main/Features/SpecialResources/RecruitChargePolicy.cs
Main/Features/SpecialResources/SpecialResourceTroopBadge.cs
Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs
Main/Features/SpecialResources/Hooks/RecruitmentResourceGateHook.cs
Main/Features/SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs
Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs
Main/Features/SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs
Main/Features/SpecialResources/Hooks/RecruitmentVM_ExecuteDone_Patch.cs
Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs
Main/Adapters/SpecialResourceEventAdapter.cs
Main/Adapters/CareerPurseAdapter.cs
Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs
Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs
Main/Features/ServerSettings/ServerSettingsReader.cs
Main/Features/ServerSettings/ServerSettingsSchema.cs
Main/Features/TroopWeight/Diagnostics/TroopCountDiagnosticsBehavior.cs
Client/Features/SpecialResources/UI/EncyclopediaUnitBadgeMixin.cs
Client/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs

REQUIRED SECTIONS:

1. VANILLA CODE. Read these from the installed DLLs through your ilspy server or tools/taom-src.ps1, not from E:\Decompiled_Bannerlord, and quote the lines your claims rest on: PartyCharacterVM.InitializeUpgrades, PartyScreenLogic.AddCommand, PartyScreenLogic.UpgradeTroop, RecruitmentVM.ExecuteDone, RecruitmentVM.RefreshPartyProperties, GiveGoldAction.ApplyBetweenCharacters, Hero.MainHero, Hero.Clan, Clan.Kingdom.

2. DEEP ANALYSIS. Walk each scenario and state the result:
A. Option on, 0 Castar, a Gondor player: upgrade into a level 41 Gondor elite, recruit gondor_mt_fountain_guard from a notable, recruit a prisoner, pass a daily tick, buy from the emissary at town_EW1.
B. Option on, then off, in one campaign, at a zero balance with upkeep troops in the party.
C. Buyer states: kingdom ruler, vassal, mercenary clan, independent clan with no kingdom, a buyer whose culture differs from the kingdom's, a shared War Spoils owner, a settlement that has been conquered.
D. Co-op: authority off and client on, then the reverse; a dedicated server reading taom-server-settings.json.
E. Player Switcher or enlisted service active during a purchase.
F. Price limits: rate 50 and 1000, merchant_cost 2800, quantity at the maximum, gold equal to the price and one less.

3. CONFIG CROSS-REFERENCE. Every troop under CultureOffers in elite_emissary_config.xml has a merchant_cost row. Every price at both rate limits is positive and fits an int. Check kingdom and culture ids against the cheatsheet.

4. FINDINGS OR OBSERVATIONS. Use .ai/report-format.md. Then give the Known Suspects verdicts: S1 to S6, each CONFIRMED or DISPUTED with evidence.

QUALITY GATES:
Every finding has a file and line, the quoted code, the trigger, the impact and a proposed remedy.
Every engine claim quotes the decompiled line from the installed DLLs.
Mark anything you could not check as UNVERIFIED.
Do not manufacture a minimum number of findings. Where you find nothing, state what you covered.
Complete every section; do not skip the hard ones.
Do not flag code that matches vanilla as a bug without showing the harm.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
