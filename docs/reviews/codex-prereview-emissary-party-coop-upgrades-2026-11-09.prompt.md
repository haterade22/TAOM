You are reviewing an uncommitted change in the TAOM repository (E:\repos\TAOM, a Lord of the Rings total conversion for Mount & Blade II Bannerlord v1.5.6, .NET Framework 4.7.2). HEAD is fc7cd73c4. Review only; do not edit any file. Read AGENTS.md and .ai/review-reference.md first.

The change has two parts:
1. #1108 follow-up, party limit: with the MCM option "Elite Troops Cost Gold Only" on, the Elite Emissary sells elite troops for gold. A resource balance used to keep the quantity small; gold does not. The change caps each offer, and each purchase, at the room the main party has for that troop: the free slots (PartySizeLimit - NumberOfAllMembers) divided by the slots one troop fills (its Troop Weight while that option is on, else 1), rounded down. A purchase above the room returns a new status PartyFull before anything moves. With the gold option off nothing changes (the emissary still sells past the limit, as before).
2. #967 C2, co-op upgrade charge: under BannerlordCoop, the client's party screen Done is replaced by Coop, which resets the screen (so TAOM's queued upgrade_cost spend is cancelled) and sends the screen to the server. The server applies it in GameInterface.Services.Party.Handlers.PartyDoneLogicHandler.ApplyUpgradedTroopHistory(Hero mainHero, List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory). New Patch144 (CoopUpgradeChargePatch) is a Harmony postfix on that private static method, bound by reflection. It converts each (from, to, count) line to (target troop id, count), asks the adapter for the hero as an EarnerRef by StringId, and calls SpecialResourceEarningService.ChargeCoopUpgrades, which picks the payer with RecruitChargePolicy.ChoosePayer (only the authority charges), seeds an untracked player, and calls the existing SpecialResourceService.SpendForUpgrade per line. SubModule.RegisterSpecialResourcesAndCareers resolves the Transient earning service once per campaign start, gives it to SpecialResourcesBehavior and to the patch through CoopUpgradeChargePatch.Initialize.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- docs/features/elite-emissary.md (section "Gold-only option", bullet "Party limit under gold")
- docs/features/special-resources.md (section "Co-op upgrade charge (#967 C2)")
- docs/reference/harmony-patch-registry.md (section "Patch144_CoopUpgradeCharge")
- docs/features/troop-weight.md if present, and Main/Features/TroopWeight/TroopWeightService.cs (ApplyPartySizeWeightPenalty, ComputeSizePenalty, SubtractResultFramePenalty)
- Main/_Module/ModuleData/TroopWeights/troop_weights.xml (header comment: the weight rule)
- The BannerlordCoop source is at E:\repos\BannerlordCoop\source (GameInterface/Services/Party/Handlers/PartyDoneLogicHandler.cs). The installed module is E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Coop.
- docs/reviews/rca-gold-only-elite-troops-2026-11-09.md (the earlier review of the gold option)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code you quote):
S1. Payer on the authority. RecruitChargePolicy assumes no human plays on the authority process (see its sessionActive doc). Is that true for every way Coop runs a server? If a person plays on the server process, does that person's own party screen reach ApplyUpgradedTroopHistory (charged by Patch144) and also run TAOM's own Patch26 queue and CommitSession (a double charge), or neither (never charged)?
S2. Binding. The postfix binds by position (__0 Hero, __1 List<Tuple<CharacterObject, CharacterObject, int>>). Check the exact signature in the Coop source and in the installed GameInterface.dll. Is GameInterface.dll loaded when TryPatchCategory("Patch144_CoopUpgradeCharge") runs in SubModule.OnGameInitializationFinished? If not loaded then, does Prepare return false forever for this process?
S3. Tuple order and meaning. Confirm that Item2 is the upgrade target and Item3 the count, from vanilla PartyScreenLogic (UpgradeTroop, AddUpgradeToHistory) in the installed v1.5.6 TaleWorlds.CampaignSystem.dll and from how Coop builds or transports the list. Can one screen hold an upgrade that the server later rejects, after this method ran?
S4. The charge itself. SpendForUpgrade (Main/Features/SpecialResources/SpecialResourceService.cs) looks up the resource for (heroId, kingdomId, cultureId), applies the career discount through GetEffectiveUpgradeCost and adds a negative amount to storage. On the server for a remote hero: can any read throw, can the balance go negative or below what the client's Patch26 gate assumed, and is ChargeCoopUpgrades' "before - after" return value right when storage floors at zero?
S5. Lifetime. The patch holds static references to the earning service and the event adapter, replaced at each campaign start. Can the postfix run with a previous campaign's instance, or before the first Initialize, in a way that charges the wrong storage?
S6. Party room. Free slots = PartySizeLimit - NumberOfAllMembers on Hero.MainHero.PartyBelongedTo.Party; under Troop Weight the limit is reduced by ceil(weighted) - raw, clamped so the limit stays at least 1. Is floor(free / weight) the exact number of troops of that weight the party can take without passing the weighted limit? Check wounded troops, the clamp, the PartySizeLimit cache (keyed on the roster version), and the Enlistment feature (can the player reach the emissary while enlisted, and which party do PartyBelongedTo and GrantTroop then name?).
S7. Presenter. In EliteEmissaryInquiryPresenter, can the quantity picker offer and enable a quantity above the room? Does every status, PartyFull included, show the player a message?

FILES (this change; other uncommitted files in the tree belong to other sessions and are out of scope):
C#: Main/Adapters/IPlayerPartyAdapter.cs, Main/Adapters/PlayerPartyAdapter.cs, Main/Adapters/ISpecialResourceEventAdapter.cs, Main/Adapters/SpecialResourceEventAdapter.cs, Main/Features/EliteEmissary/EliteEmissaryService.cs, Main/Features/EliteEmissary/Domain/EmissaryTroopOffer.cs, Main/Features/EliteEmissary/Domain/EmissaryPurchaseResult.cs, Main/Features/EliteEmissary/Hooks/EliteEmissaryInquiryPresenter.cs, Main/Features/SpecialResources/SpecialResourceEarningModels.cs, Main/Features/SpecialResources/SpecialResourceEarningService.cs, Main/Features/SpecialResources/SpecialResourceService.cs (one guard in SpendForUpgrade), Main/Features/CoopInterop/Hooks/CoopUpgradeChargePatch.cs (new), Main/SubModule.cs (the TryPatchCategory("Patch144_CoopUpgradeCharge") line and RegisterSpecialResourcesAndCareers only).
Tests: TAOM.Tests/Features/EliteEmissary/EliteEmissaryServiceTests.cs, TAOM.Tests/Features/CoopInterop/CoopUpgradeChargePatchTests.cs (new), TAOM.Tests/Features/SpecialResources/SpecialResourceEarningServiceCoopTests.cs, SpecialResourceEarningServiceWiringTests.cs, SpecialResourceBreakdownTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs, DischargeConsequenceServiceTests.cs, TAOM.Tests/Migration/HarmonyPatchBindingTests.cs, TAOM.Tests/Features/CoopInterop/CoopPresenceReadGateTests.cs.
XML: Main/_Module/ModuleData/taom_emissary_strings.xml and its 12 language files under Main/_Module/ModuleData/Languages (two new keys, English seeded).
Manifests: tools/ingame_checks/967.toml (new), tools/ingame_checks/1108.toml.
Use git diff HEAD on these paths, and git status for the new files.

REQUIRED SECTIONS:
1. VANILLA AND COOP CODE: quote the target method, its only caller and the vanilla upgrade-history code, from the installed DLLs (your ilspy server) and the Coop source.
2. KNOWN SUSPECTS: CONFIRMED or DISPUTED for S1 to S7, each with the quoted code.
3. DEEP ANALYSIS: walk one co-op upgrade from the client's Done to the server's balance, and one gold purchase of a weight-3 elite and of a weight-20 Mumakil rider into a party with 10 free slots.
4. CONFIG CROSS-REFERENCE: the new keys in the 13 strings files; the manifests' check id against the Outcomes call in the patch.
5. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, the failure scenario, and the evidence.

QUALITY GATES: quote code for every claim. Say UNVERIFIED when you could not read the source. Do not flag code that matches vanilla or Coop behaviour as a bug without showing the harm.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-prereview-emissary-party-coop-upgrades-2026-11-09.md; do NOT write that path yourself.
