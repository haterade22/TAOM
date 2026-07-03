# Adversarial Review: CultureConversion Notable Replacement (#325)

You are performing an adversarial code review of a NEW addition to the TAOM Bannerlord mod (v1.4.6). The feature: when a conquered settlement completes culture conversion (Settlement.Culture flips after a hold period), the settlement's foreign-culture NOTABLES are now REPLACED with same-occupation notables from the new culture's templates. Property (workshops/alleys/caravans) transfers to replacements; relations reset; active issues cancel; the old notable is removed.

The changes are UNCOMMITTED in the working tree -- review the current file contents, not git HEAD.

Your job: find real bugs. Attack the design. Do not flatter. Every finding needs file:line evidence from the TAOM source AND, for engine-behavior claims, decompiled vanilla code as proof.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, goblin, mistymountainorcs, shaghana, abanissa
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/features/culture-conversion.md (especially the "Notable replacement (2026-07-03)" section and Known limitations)
- Main/_Module/ModuleData/culture_conversion/culture_conversion_config.json
- docs/reviews/rca-culture-conversion-notables-2026-07-03.md (the internal review already run -- find what IT missed)

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence

1. HEIR-SPAWN SUPPRESSION. CultureConversionAdapter.ReplaceNotable calls hero.AddPower(-hero.Power) then KillCharacterAction.ApplyByRemove(hero). Claim: this guarantees NotablesCampaignBehavior.OnHeroKilled takes the non-heir branch (victim.Power >= NotableDisappearPowerLimit is false), so no old-culture relative spawns. Attack this: is Hero.Power purely field-backed? Does any engine or TAOM model (NotablePowerModel override? Power clamps? event between AddPower and ApplyByRemove) restore power before OnHeroKilled fires? Is there any OTHER engine path that resurrects/replaces a removed notable with the old culture?
2. ISSUE CANCEL COMPLETENESS. hero.Issue?.CompleteIssueWithCancel() is claimed to deterministically leave hero.Issue == null so ApplyByRemove's "notable has quest" assert cannot fire and IssueManager.OnHeroKilled no-ops. Attack: trace IssueBase.CompleteIssueWithCancel -> IssueFinalized -> DeactivateIssue -> OnIssueDeactivatedForHero for EVERY issue state (no quest started, quest active, alternative-solution troops in flight, quest in a mission). Any state where Issue stays non-null or the call throws mid-DailyTick? What about a quest whose current stage holds a conversation/mission reference?
3. MID-TICK ENGINE MUTATION SAFETY. ReplaceNotable runs inside DailyTickEvent and calls HeroCreator.CreateNotable (which fires OnHeroCreated -> EnterSettlementAction.ApplyForCharacterOnly + NotablePowerManagementBehavior.AddPower + random SupporterOf) and KillCharacterAction.ApplyByRemove (fires OnHeroKilled to ALL listeners). Attack: any vanilla listener on OnHeroCreated/OnHeroKilled that enumerates Settlement.Notables or Hero collections in a way that conflicts with our loop? Any re-entrancy into CultureConversion itself?
4. WORKSHOP/ALLEY/CARAVAN TRANSFER EDGE CASES. ChangeOwnerOfWorkshopAction.ApplyByDeath(workshop, replacement) / Alley.SetOwner(replacement) / CaravanPartyComponent.TransferCaravanOwnership(caravan.MobileParty, replacement, settlement). Attack: replacement was created microseconds ago -- any engine assumption that the new owner has gold, is in the same settlement as the workshop, or has a party? What happens to an alley WAR (AreaState fighting) mid-transfer? A caravan currently in a map event?
5. TEMPLATE PRE-CHECK FIDELITY. The adapter pre-checks settlement.Culture.NotableTemplates.Any(t => t != null && t.Occupation == occupation) before HeroCreator.CreateNotable. Claim: this exactly prevents the null-template NRE (DefaultHeroCreationModel.GetRandomTemplateByOccupation returns null on empty filtered list). Attack: is there any divergence (e.g. the engine ALSO requires template.IsOriginalCharacter or a Frequency trait) where the pre-check passes but CreateNotable still fails? Does TAOM override HeroCreationModel in a way that changes GetRandomTemplateByOccupation? (Check Main/Features/RaceAge/TaomHeroCreationModel.)
6. SNAPSHOT LOOP INTEGRITY. CultureConversionService.ReplaceForeignNotables iterates adapter.GetNotables(settlementId) (a materialized DTO list) and calls ReplaceNotable per hero id. Attack: can a replacement notable spawned mid-loop have the SAME StringId pattern or otherwise be re-processed? Can hero resolution (CampaignObjectManager.Find<Hero>) return the WRONG hero after removals? Any case where the same notable is processed twice (town + village overlap)?

## FILES

TAOM source (working tree):
- Main/Features/CultureConversion/CultureConversionService.cs (ApplyConversion + ReplaceForeignNotables)
- Main/Adapters/CultureConversionAdapter.cs (GetNotables + ReplaceNotable -- the engine sequence under review)
- Main/Adapters/ICultureConversionAdapter.cs
- Main/Features/CultureConversion/Domain/ConvertibleNotable.cs
- Main/Features/CultureConversion/CultureConversionConfig.cs + CultureConversionConfigProvider.cs
- Main/Features/CultureConversion/ICultureConversionSettingsProvider.cs + CultureConversionSettingsProvider.cs
- Main/Features/TaomSettings.cs (CultureConversionReplaceNotables, Culture Conversion group)
- Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs (driver -- unchanged, context)
- TAOM.Tests/Features/CultureConversion/CultureConversionServiceTests.cs
- TAOM.Tests/Features/CultureConversion/CultureConversionConfigProviderTests.cs

## REQUIRED SECTIONS in your output

### VANILLA CODE
Decompile and paste (as code blocks) the load-bearing engine methods from the installed v1.4.6 (use ilspycmd against E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll, or the dump at E:\Decompiled_Bannerlord\Campaign\ which is v1.4.6):
- KillCharacterAction.ApplyInternal (assert + OnHeroKilled dispatch path)
- NotablesCampaignBehavior.OnHeroKilled + CheckAndMakeNotableDisappear
- IssueBase.CompleteIssueWithCancel + IssueFinalized; IssueManager.DeactivateIssue + OnHeroKilled
- HeroCreator.CreateNotable + DefaultHeroCreationModel.GetRandomTemplateByOccupation
- ChangeOwnerOfWorkshopAction.ApplyByDeath; Alley.SetOwner; CaravanPartyComponent.TransferCaravanOwnership

### DEEP ANALYSIS -- concrete scenarios
For each scenario, state the exact code path and outcome:
1. Mordor converts a Gondor town with 5 notables: 2 merchants (one owns 2 workshops), 2 gang leaders (one owns an alley + has an active player quest), 1 artisan with power 250. Walk ALL 5 replacements end to end.
2. The bound village headman has volunteers in slots and a caravan (possible?). Village conversion path.
3. Player is INSIDE the town menu when the daily tick completes the conversion. What does the player see / can anything NRE (LocationComplex, conversation, menu refresh)?
4. Save made mid-loop is impossible (single-threaded tick), but a CRASH mid-loop leaves half-replaced notables with culture flipped and record marked converted -- is the resulting state self-healing or corrupt?
5. Reconquest: Gondor retakes the converted town on day X, re-converts on day X+45 -- confirm orc notables are replaced back and no record/property leaks.
6. Toggle interactions: master EnableCultureConversion on + ReplaceNotables off, then user flips ReplaceNotables on AFTER a conversion completed -- notables stay old culture (documented). Any path where flipping toggles mid-pending-timer misbehaves?

### CONFIG CROSS-REFERENCE
- culture_conversion_config.json field names vs CultureConversionConfig property names (Newtonsoft camelCase binding)
- MCM property TaomSettings.CultureConversionReplaceNotables wiring through CultureConversionSettingsProvider
- Defaults consistency: JSON true, POCO true, MCM true, doc claims

### FINDINGS OR OBSERVATIONS
Numbered findings with severity (HIGH/MED/LOW), file:line, evidence, and a concrete fix. If a Known Suspect is DISPUTED, show the disproving code. If you find nothing in a category, say so explicitly -- do not pad.

## QUALITY GATES
- Every engine-behavior claim must cite decompiled code (paste the lines).
- Every TAOM claim must cite file:line from the working tree.
- Do NOT flag code as a bug when it matches vanilla behavior -- check vanilla first.
- Do NOT assume empire=Rohan (it is Dunland). Use the cheatsheet.
- If you cannot verify something, mark it UNVERIFIED -- do not guess.

## PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates (IssueQuestCanBeDuplicated, MobileParty capability propagation). Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections when context ran long -- do not skip the VANILLA CODE section.

Write your review to stdout (it is being captured to docs/reviews/codex-adversarial-culture-conversion-notables-2026-07-03.md).
