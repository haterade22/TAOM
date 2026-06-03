# Adversarial Review: CultureConversion feature (TAOM, Bannerlord v1.4.5)

You are an adversarial code reviewer. Assume bugs exist; prove or disprove each hypothesis by reading the actual TAOM source and decompiling the named vanilla targets. Report only findings you can substantiate with code. Disagreement with the author's reasoning is valuable signal.

## Feature (1-line)

When a town/castle is conquered by a clan of a DIFFERENT culture, after a configurable hold period the settlement (and its bound villages) gradually converts: Settlement.Culture flips to the new owner, recruits/militia/identity follow, and the "foreign occupier" loyalty penalty drops. Reconquest back to the original culture reverts it. Settlement.Culture is a public mutable field that is NOT engine-saved, so completed conversions persist in a TAOM store and re-apply on load.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/features/culture-conversion.md -- the feature design + the "Cross-feature interactions" section.
- docs/reviews/rca-culture-conversion-2026-06-02.md -- the deep-review RCA (6 findings already fixed; do NOT re-report those as new -- instead, VERIFY the fixes are correct and look for what that review missed).
- Main/_Module/ModuleData/culture_conversion/culture_conversion_config.json -- config defaults.

## Known Suspects (CONFIRM or DISPUTE each, with code evidence)

1. EVENT TIMING -- OnSettlementOwnerChangedEvent. The behavior reads the new owner's culture via the adapter (Settlement.OwnerClan.Culture) AT the event. Decompile TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction (ApplyInternal). CONFIRM that settlement.OwnerClan is already set to the NEW owner BEFORE OnSettlementOwnerChanged fires (line that dispatches the event vs the line that sets the owner). If the event fires BEFORE the owner is updated, GetOwnerCultureId would read the OLD owner and the feature would queue the wrong target (or no-op). This is the load-bearing timing assumption.

2. CASTLE VOLUNTEER REFILL after ResetVolunteers. On conversion, CultureConversionService.ApplyConversion calls adapter.ResetVolunteers (nulls every notable's 6 VolunteerTypes slots) so they repopulate from the new culture's pool. Vanilla RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement (decompile it -- see pasted snippet below) EARLY-RETURNS for castles (IsTown=false AND IsVillage=false), so vanilla never refills castle notables. TAOM's separate CastleRecruitment feature (Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs) is what fills castle volunteers. HYPOTHESIS to test: if a CASTLE is converted while CastleRecruitment is DISABLED (its MCM toggle off), the reset slots are never refilled -> the castle has an empty recruit pool. Is that a regression vs not-converting? (Consider: vanilla castles have no recruitment at all; CastleRecruitment is what adds it. So clearing already-unused slots may be harmless.) Determine whether ResetVolunteers on a castle is safe in all four combinations of {town, castle} x {CastleRecruitment on, off}.

3. CONVERTED-BRANCH REGRESSION. Main/Features/TroopProgression/VolunteerRecruitmentService.cs GetVolunteerTroopId was refactored: the old 6-step cascade was extracted into ResolveStandardCascade and a new converted-settlement branch was added in front. CONFIRM ResolveStandardCascade is byte-for-byte behaviorally identical to the pre-feature cascade (same order, same null-coalescing), so non-converted settlements are unaffected. Then CONFIRM the converted branch resolves CultureMap[SettlementCultureId] and falls through to the standard cascade only when that pool is null.

4. HasCulturePool GATE COMPLETENESS. CultureConversionService.OnSettlementConquered only queues a conversion if _recruitment.HasCulturePool(ownerCulture) is true. HasCulturePool returns CultureMap.ContainsKey(cultureId). Enumerate every culture id that has a CultureMap entry in VolunteerRecruitmentService.cs and cross-check against the 16 playable kingdom cultures in the cheatsheet. Is there any PLAYABLE culture that can own a fief but has NO CultureMap entry (so its conquests would never convert)? Is there any bandit/minor culture that DOES have a CultureMap entry (so a minor-faction-owned fief WOULD wrongly convert)?

5. ORIGINAL-CULTURE CAPTURE (R6). CultureConversionService.OnSettlementConquered captures OriginalCultureId = adapter.GetCurrentCultureId(settlementId) ONLY when the record does not yet exist. GetCurrentCultureId reads the LIVE Settlement.Culture. CONFIRM there is no path where this capture runs AFTER an override was already applied in-memory (which would record the converted culture as the "original" and break reconquest-to-original restoration). Consider the store-clear paths (OnNewGameCreated, OnSessionLaunched new-starter, Deserialize on load) and whether any leaves Settlement.Culture mutated in-memory while the store is empty. (The deep-review refuted one such scenario as architecturally impossible -- verify that refutation independently.)

6. SAVE/LOAD + same-process new-campaign. CultureConversionBehavior mirrors MessengerCampaignBehavior's _justLoadedFromSave guard. Trace: SyncData(IsLoading) sets the flag; OnSessionLaunched clears it unconditionally at the end; OnNewGameCreated clears the store only when !_justLoadedFromSave. CONFIRM a same-process sequence (load save A -> start new campaign B, or load A -> load B) cannot (a) wipe a freshly-loaded store, or (b) carry campaign A's conversions into campaign B. Compare against Main/Features/Messengers/MessengerCampaignBehavior.cs which it claims to mirror.

7. SyncData FORMAT ROUND-TRIP. SettlementConversionRecord.Serialize/TryParse uses a 4-field pipe-delimited composite with R-format doubles and NaN/Infinity rejection. CONFIRM: culture StringIds can never contain '|' (would corrupt the split); a malformed pending-timer drops ONLY the pending portion while preserving a completed override (so a real conversion is never lost to a corrupt timer); structural failure (empty original / wrong field count) drops the whole record.

## VANILLA CODE (decompile these from the INSTALLED v1.4.5 DLLs to verify; key snippets pre-extracted below)

DefaultSettlementLoyaltyModel (TaleWorlds.CampaignSystem.GameComponents), line ~194 -- the owner-culture loyalty gate this feature implicitly relies on:
```
if (town.Settlement.OwnerClan.Culture != town.Settlement.Culture)
    explainedNumber.Add(SettlementOwnerDifferentCultureLoyaltyEffect, CultureText);
```

RecruitmentCampaignBehavior (TaleWorlds.CampaignSystem.CampaignBehaviors), UpdateVolunteersOfNotablesInSettlement ~line 215 -- fills ONLY null slots, upgrades populated slots within their own tree, and early-returns for non-town/non-village (i.e. CASTLES):
```
private void UpdateVolunteersOfNotablesInSettlement(Settlement settlement)
{
    if ((!settlement.IsTown || settlement.Town.InRebelliousState) && (!settlement.IsVillage || settlement.Village.Bound.Town.InRebelliousState))
        return;   // castles: IsTown=false AND IsVillage=false -> always returns
    foreach (Hero notable in settlement.Notables) {
        CharacterObject basicVolunteer = Campaign.Current.Models.VolunteerModel.GetBasicVolunteer(notable);
        for (int i = 0; i < 6; i++) {
            CharacterObject characterObject = notable.VolunteerTypes[i];
            if (characterObject == null) notable.VolunteerTypes[i] = basicVolunteer;       // ONLY fills empty
            else if (characterObject.UpgradeTargets.Length != 0 && characterObject.Tier < MaxVolunteerTier)
                notable.VolunteerTypes[i] = characterObject.UpgradeTargets[...];            // upgrades in-tree
        }
    }
}
```
Decompile the FULL body to confirm the early-return condition and that no other path re-rolls a populated base troop. Also decompile ChangeOwnerOfSettlementAction.ApplyInternal for Suspect 1.

## Feature files

Service + domain + store + config + behavior:
- Main/Features/CultureConversion/CultureConversionService.cs
- Main/Features/CultureConversion/Domain/SettlementConversionRecord.cs
- Main/Features/CultureConversion/CultureConversionStore.cs + ICultureConversionStore.cs
- Main/Features/CultureConversion/CultureConversionConfig.cs + ICultureConversionConfigProvider.cs + CultureConversionConfigProvider.cs
- Main/Features/CultureConversion/ICultureConversionSettingsProvider.cs + CultureConversionSettingsProvider.cs
- Main/Features/CultureConversion/ICultureConversionService.cs
- Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs
- Main/Features/CultureConversion/CultureConversionIoC.cs

Adapter (boundary):
- Main/Adapters/ICultureConversionAdapter.cs + CultureConversionAdapter.cs

Recruitment integration (modified):
- Main/Adapters/VolunteerContextAdapter.cs
- Main/Features/TroopProgression/VolunteerContext.cs
- Main/Features/TroopProgression/IVolunteerRecruitmentService.cs
- Main/Features/TroopProgression/VolunteerRecruitmentService.cs

Wiring (modified):
- Main/IoC.cs (registration)
- Main/SubModule.cs (AddBehavior)
- Main/Features/TaomSettings.cs (MCM group "Culture Conversion")

Tests:
- TAOM.Tests/Features/CultureConversion/*.cs
- TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentConversionTests.cs

Cross-feature (read for coupling analysis, do not re-review their internals):
- Main/Features/CulturalFeats/Models/TaomSettlementLoyaltyModel.cs (reads the loyalty gate above)
- Main/Adapters/TownRosterAdapter.cs (CultureMarketplace reads OwnerClan.Culture here)
- Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs (castle volunteer refill -- Suspect 2)

## REQUIRED SECTIONS in your output

1. VANILLA CODE -- paste the decompiled bodies you verified (ChangeOwnerOfSettlementAction event ordering; RecruitmentCampaignBehavior refill).
2. KNOWN SUSPECTS -- CONFIRMED / DISPUTED verdict for each of the 7, with code evidence.
3. DEEP ANALYSIS -- concrete scenarios: (a) Gondor town captured by a Mordor clan, hold period, conversion, recruit a troop -- trace the exact troop id produced; (b) same for a CASTLE with CastleRecruitment off; (c) reconquest back to Gondor; (d) save mid-hold-period, reload, continue.
4. CONFIG CROSS-REFERENCE -- every culture_conversion_config.json field validated + consumed; every TaomSettings "Culture Conversion" property read + gating behavior.
5. CROSS-FEATURE -- RevoltTuning loyalty coupling + CultureMarketplace owner-culture divergence + CastleRecruitment castle-refill: are the docs (culture-conversion.md "Cross-feature interactions") accurate and complete? Any additional sibling feature that reads Settlement.Culture or OwnerClan.Culture and would be affected by conversion?
6. FINDINGS OR OBSERVATIONS -- numbered, each with file:line, severity (CRITICAL/HIGH/MEDIUM/LOW), and the minimal fix. If the feature is clean, say so explicitly per section -- do not manufacture findings.

## QUALITY GATES

- Verify "missing X" claims by grepping -- do not trust "I didn't find it."
- Decompile vanilla from the installed DLLs; the repo has no vanilla source. AGENTS.md explains the lookup.
- Do NOT flag vanilla-matching code as a bug.
- Do NOT re-report the 6 findings already fixed in the RCA -- verify those fixes instead.

## Prior review lessons

SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; tracing XML value -> calculator -> guard caught a dead feature.
FAILURES: Codex once assumed empire=Rohan (it is Dunland); flagged vanilla-matching code as bugs; skipped hard decompile sections. Avoid these.

## Output

Write your full review to docs/reviews/codex-adversarial-culture-conversion-2026-06-02.md (this file is the stdout target -- just produce the review as your response).
