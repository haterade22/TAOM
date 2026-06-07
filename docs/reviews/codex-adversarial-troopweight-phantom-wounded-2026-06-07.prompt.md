# Adversarial Review: TAOM TroopWeight phantom-wounded display fix

You are an adversarial code reviewer. Assume the code has bugs. Prove them with evidence from the TAOM source and the vanilla Bannerlord v1.4.5 decompile. Confirm or DISPUTE each Known Suspect. Output findings with file:line and a concrete fix.

## Feature in one paragraph

TAOM's TroopWeight feature makes heavy troops cost more party-size budget by Postfix-patching `PartyBase.NumberOfAllMembers` (and `NumberOfRegularMembers`) to return a WEIGHTED member count (each troop counts as its `weight`, default 1.0, e.g. cave_troll=4, blademaster=2). It deliberately does NOT weight `PartyBase.NumberOfHealthyMembers` because that getter feeds gameplay (battle troop supply, casualty tracking, sacrifice limits, desertion, inventory capacity). Four vanilla DISPLAY surfaces compute `wounded = NumberOfAllMembers - NumberOfHealthyMembers`; since only the first term is weighted, the weight surplus rendered as PHANTOM wounded troops (e.g. 23 weight-2 troops = 46 weighted all - 23 real healthy = 23 phantom wounded, with no battle fought). This change adds display-only Postfix patches on those four surfaces to rewrite the shown numbers using a weighted (healthy, wounded) split.

## TAOM ID CHEATSHEET (not central to this feature but for reference)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
NOTE: "rohan" and "dol_guldur" are NOT valid IDs.

## READ FIRST

- Main/Features/TroopWeight/TroopWeightService.cs (the new ComputeWeightedHealthyAndWounded + GetWeightedHealthAndWounded + WeightedContribution + the ConditionalWeakTable cache)
- Main/Features/TroopWeight/ITroopWeightService.cs
- Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs (the 4 hook methods + RewriteHealthTooltip strip logic)
- Main/Features/TroopWeight/Hooks/CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs
- Main/Features/TroopWeight/Hooks/CampaignUIHelper_GetPartyHealthTooltip_Patch.cs
- Main/Features/TroopWeight/Hooks/GameMenuPartyItemVM_RefreshCounts_Patch.cs
- Main/Features/TroopWeight/Hooks/PartyBaseHelper_GetPartySizeText_Patch.cs
- Main/Features/TroopWeight/TroopWeightIoC.cs
- Existing hooks for context: Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs, PartyVMPopulatePartyListLabelHook.cs
- Tests: TAOM.Tests/Features/TroopWeight/TroopWeightServiceTests.cs, TroopWeightHooksTests.cs

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. VersionNo staleness. GetWeightedHealthAndWounded caches (healthy, wounded) keyed by party in a ConditionalWeakTable, invalidated when `roster.VersionNo` changes. CRITICAL: does `TroopRoster.VersionNo` increment on EVERY mutation that changes the wounded count -- specifically when a troop is WOUNDED or HEALED (not just added/removed)? Decompile TroopRoster.AddToCountsAtIndex / WoundTroop / the wounded mutators and verify they bump VersionNo (or _versionNo). If healing/wounding a troop does NOT bump VersionNo, the cached wounded count goes stale and the tooltip shows the wrong number after a battle. This is the highest-risk suspect.

2. Healing-block strip over/under-reach. In RewriteHealthTooltip, when weighted wounded == 0, the code removes list entries from just after the "Wounded Troops" entry up to the next "Prisoners" or "Land Troop Capacity" entry (else end of list), but ONLY if a "Healing Rate" entry exists in that range. In GetPartyHealthTooltip (the per-party overload) NOTHING follows the explanation, so boundary == list.Count and the whole tail is removed. Confirm: (a) the strip cannot remove a legitimate non-healing entry; (b) RemoveRange cannot throw; (c) when weighted wounded > 0 but the labels are not found (woundedIdx < 0) it leaves vanilla values intact rather than corrupting.

3. Separate-ceiling rounding. ComputeWeightedHealthyAndWounded ceilings weightedHealthy and weightedWounded INDEPENDENTLY. For fractional weights with mixed wound states, Ceiling(healthy)+Ceiling(wounded) can exceed Ceiling(total) by 1, so the tooltip's "Battle Ready + Wounded" could be 1 higher than the "62/23" panel header (which uses Ceiling(total) via NumberOfAllMembers). Confirm: (a) TAOM ships only INTEGER weights (check Main/_Module/ModuleData for the troop weight XML) so this never manifests in practice; (b) it matches the existing PartyVMPopulatePartyListLabelHook separate-ceiling choice (so the party-list label and the tooltip agree). Is this acceptable as documented, or a real bug?

4. GameMenuPartyItemVM vanilla setter bug interaction. The vanilla PartyWoundedSize setter guards `if (value != _partySize)` (note: compares against _partySize, NOT _partyWoundedSize -- a vanilla copy-paste bug). Our Postfix sets PartyWoundedSize then PartySize then PartySizeLbl. Trace whether both writes actually take effect given the buggy guard, for the realistic case where vanilla just set _partySize=46/_partyWoundedSize=16 and we want 62/0. Decompile GameMenuPartyItemVM to confirm the setter bodies.

5. EnableTroopWeight toggle OFF mid-session. All 8 Patch17 postfixes gate on `TaomSettings.Instance?.EnableTroopWeight ?? true`. With the toggle OFF the postfixes are no-ops, so displays revert to vanilla arithmetic. But the ConditionalWeakTable cache in the service persists. Confirm there is NO path where a stale cache value is shown when the toggle is off (the hooks are not called at all when gated, so the cache is simply never read -- verify).

6. Surface completeness. Confirm there is no FIFTH vanilla display surface computing `NumberOfAllMembers - NumberOfHealthyMembers` that we missed. Grep the decompile for the pattern across CampaignSystem + ViewModelCollection. (We found exactly 4: CampaignUIHelper.GetMainPartyHealthTooltip, CampaignUIHelper.GetPartyHealthTooltip, GameMenuPartyItemVM.RefreshCounts, PartyBaseHelper.GetPartySizeText. EncounterMenuOverlayVM and TooltipRefresherCollection use NumberOfHealthyMembers as a sum/sort, NOT the subtraction -- not phantom surfaces.)

## VANILLA CODE (v1.4.5, for reference -- verify against installed DLLs at E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/)

PartyBase.cs:
  public int NumberOfHealthyMembers => MemberRoster.TotalManCount - MemberRoster.TotalWounded;
  public int NumberOfAllMembers => MemberRoster.TotalManCount;
  public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;

CampaignUIHelper.GetMainPartyHealthTooltip() (line ~1059): adds "Battle Ready Troops" = party.NumberOfHealthyMembers; then num = NumberOfAllMembers - NumberOfHealthyMembers; adds "Wounded Troops" = num; if (num > 0) adds a DoubleSeperator + "Healing Rate" + explanation; if prisoners>0 adds Prisoners; EmptyLine; "Land Troop Capacity" totalManCount/partySizeLimit; ships block.

CampaignUIHelper.GetPartyHealthTooltip(PartyBase party) (line ~928): "Battle Ready Troops" = NumberOfHealthyMembers (Title); num = AllMembers - HealthyMembers; "Wounded Troops" = num (Title); if (num>0) DoubleSeperator + "Healing Rate" (Title) + Seperator + explanation; returns. Nothing after.

GameMenuPartyItemVM.RefreshCounts() (line ~778): if (PartySize != NumberOfHealthyMembers || PartyWoundedSize != AllMembers - HealthyMembers) { PartyWoundedSize = AllMembers - HealthyMembers; PartySize = NumberOfHealthyMembers; PartySizeLbl = IsInfoHidden ? "?" : NumberOfHealthyMembers.ToString(); } ShipCount = ...

Helpers.PartyBaseHelper.GetPartySizeText(PartyBase party) (line ~36): if (NumberOfHealthyMembers == NumberOfAllMembers) return new TextObject(NumberOfHealthyMembers.ToString()); SetTextVariable("HEALTHY_NUM", NumberOfHealthyMembers); SetTextVariable("WOUNDED_NUM", AllMembers - HealthyMembers); return GameTexts.FindText("str_party_health").

## REQUIRED OUTPUT SECTIONS

1. VANILLA VERIFICATION: paste the actual decompiled bodies of TroopRoster.VersionNo's mutators (for Suspect 1) and GameMenuPartyItemVM's PartySize/PartyWoundedSize setters (for Suspect 4) as code blocks; confirm or correct the vanilla code above.
2. KNOWN SUSPECTS: CONFIRMED / DISPUTED for each of the 6, with evidence.
3. ADDITIONAL FINDINGS: anything else -- thread-safety of the ConditionalWeakTable on the UI thread, exception-path correctness, label-match locale stability, IoC wiring, the fail-safe (0,0) guards.
4. CONFIG CROSS-REFERENCE: confirm the troop-weight XML ships only integer weights (relevant to Suspect 3).
5. QUALITY GATE: is this ready to ship? List blocking issues only.

## QUALITY GATES

- Do NOT flag vanilla-matching behavior as a bug.
- Do NOT assume API signatures -- verify against the installed v1.4.5 DLLs.
- A finding without file:line + evidence is an observation, not a finding -- label it as such.
- Separate CONFIRMED bugs from style preferences.

## PRIOR REVIEW LESSONS

SUCCESSES: vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; config ID cross-ref caught mismatches.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard decompile sections. Do the TroopRoster.VersionNo decompile (Suspect 1) -- it is the hardest and highest-value part.

Output the review to stdout (it is being redirected to docs/reviews/codex-adversarial-troopweight-phantom-wounded-2026-06-07.md).
