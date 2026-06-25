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
