# Adversarial Review: AlignmentRecruitment (TAOM, Bannerlord v1.4.6)

You are an adversarial code reviewer. Assume bugs exist. Your job is to CONFIRM or DISPUTE each Known Suspect with evidence read from the actual files, and to find anything else wrong. Do not rubber-stamp. Cite file:line for every claim.

## Feature in one line

A recruiter (player or AI lord) cannot recruit volunteer troops at a settlement controlled by an enemy-aligned kingdom (Free vs Evil). Implemented as a single override of `VolunteerModel.MaximumIndexHeroCanRecruitFromHero` in TAOM's existing `TaomVolunteerModel`, returning -1 (the engine's own "recruit nothing from this notable" signal) when the recruiter's kingdom alignment opposes the recruitment settlement's controlling-kingdom alignment.

## TAOM ID CHEATSHEET (do not confuse these)

Kingdom StringIds: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa, bluecraig and lindon and goblin and mistymountainorcs also valid kingdom StringIds.
NOTE: "rohan" is NOT a valid id (Rohan = vlandia). "gondor"/"mordor" are CULTURE ids, NOT kingdom ids. alignment.json is keyed by KINGDOM StringId.

alignment.json (the source of truth, keyed by kingdom StringId): empire_w=free, empire=evil, vlandia=free, erebor=free, sturgia=free, rivendell=free, lothlorien=free, mirkwood=free, empire_s=evil, isengard=evil, gundabad=evil, dolguldur=evil, khuzait=evil, battania=evil, aserai=evil, shaghana=neutral, abanissa=neutral, umbar=neutral, goblin=evil, mistymountainorcs=evil, bluecraig=evil, lindon=free.

## READ FIRST

- docs/features/alignment-recruitment.md (the feature design + edge cases)
- Main/_Module/ModuleData/recruitment_alignment/recruitment_alignment_config.json
- Main/_Module/ModuleData/execution/alignment.json
- Main/Features/Execution/AlignmentService.cs + IAlignmentService.cs (the reused lookup; note AreEnemyAlignments treats Neutral as enemy-of-everyone -- the new service deliberately does NOT use it)

## Files under review

Service + config (pure / TaleWorlds-free):
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs + IRecruitmentAlignmentService.cs
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentConfig.cs
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentConfigProvider.cs + IRecruitmentAlignmentConfigProvider.cs
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentSettingsProvider.cs + IRecruitmentAlignmentSettingsProvider.cs
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentIoC.cs

Integration (boundary):
- Main/Features/TroopProgression/Models/TaomVolunteerModel.cs (the MaximumIndexHeroCanRecruitFromHero override + new ctor dep)
- Main/SubModule.cs (around line 360-363, threads IRecruitmentAlignmentService into the model)
- Main/IoC.cs (RecruitmentAlignmentIoC registration)
- Main/Features/TaomSettings.cs (group "World/Recruitment Alignment", 3 bools)

Tests:
- TAOM.Tests/Features/AlignmentRecruitment/RecruitmentAlignmentServiceTests.cs
- TAOM.Tests/Features/AlignmentRecruitment/RecruitmentAlignmentConfigProviderTests.cs

## VANILLA CODE (v1.4.6, decompiled from the installed DLL -- authoritative)

DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero (the method TAOM overrides):

```csharp
public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)
{
    int num = MaximumIndexCanPartyRecruitFromHeroInternal(buyerHero, sellerHero);
    int num2 = ((useValueAsRelation < -100) ? buyerHero.GetRelation(sellerHero) : useValueAsRelation);
    int num3 = ((num2 >= 100) ? 7 : ((num2 >= 80) ? 6 : ((num2 >= 60) ? 5 : ((num2 >= 40) ? 4 : ((num2 >= 20) ? 3 : ((num2 >= 10) ? 2 : ((num2 >= 5) ? 1 : ((num2 < 0) ? (-1) : 0))))))));
    int num4 = ((sellerHero.CurrentSettlement != null && buyerHero.MapFaction == sellerHero.CurrentSettlement.MapFaction) ? 1 : 0);
    int num5 = ((buyerHero != Hero.MainHero) ? 1 : 0);
    int num6 = ((sellerHero.CurrentSettlement != null && buyerHero.MapFaction.IsAtWarWith(sellerHero.CurrentSettlement.MapFaction)) ? (-(1 + num5)) : 0);
    if (buyerHero.IsMinorFactionHero && sellerHero.CurrentSettlement != null && sellerHero.CurrentSettlement.IsVillage) { num6 = 0; }
    // + perk bonuses num7 ...
    return MathF.Min(6, num + num3 + num4 + num5 + num6 + num7);
}
public override int MaximumIndexGarrisonCanRecruitFromHero(Settlement settlement, Hero sellerHero)
{
    return MaximumIndexCanPartyRecruitFromHeroInternal(settlement.Owner, sellerHero);
}
```

Consumer call sites (verified):
- Player UI: Helpers.HeroHelper.HeroCanRecruitFromHero(buyerHero, sellerHero, index) returns `index <= VolunteerModel.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero)`. RecruitVolunteerTroopVM sets CanBeRecruited only when this is true. Volunteer slot indices are 0..5, so a return of -1 makes every slot non-recruitable.
- AI: RecruitmentCampaignBehavior.RecruitVolunteersFromNotable computes `num3 = MaximumIndexHeroCanRecruitFromHero(mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero, notable)` and does `if (num > num3) continue;` where num >= 0 is the slot index. So num3 == -1 skips the notable entirely.

## TAOM OVERRIDE under review

```csharp
public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)
{
    var recruiterKingdomId = buyerHero?.Clan?.Kingdom?.StringId;
    var sourceKingdomId = sellerHero?.CurrentSettlement?.MapFaction?.StringId;
    var isPlayer = buyerHero == Hero.MainHero;
    return _recruitmentAlignment.IsRecruitmentBlocked(recruiterKingdomId, sourceKingdomId, isPlayer)
        ? -1
        : base.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation);
}
```

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each, with file:line evidence

1. RECRUITER-BASIS ASYMMETRY (highest priority). The override resolves the recruiter via `buyerHero.Clan?.Kingdom?.StringId`, but vanilla's OWN same-faction (num4) and at-war (num6) checks in this exact method use `buyerHero.MapFaction`, and the override's SOURCE side uses `sellerHero.CurrentSettlement.MapFaction.StringId`. Question: should the recruiter side also use `buyerHero.MapFaction?.StringId` for consistency? Consider mercenary clans and minor-faction heroes serving a kingdom -- does `Clan.Kingdom` diverge from `MapFaction` for them (e.g., a mercenary contracted to Mordor)? Determine, by decompiling Hero.MapFaction and Clan.Kingdom, whether the two can disagree in a way that makes a "serving an evil kingdom" recruiter wrongly resolve to Neutral (no block) under the Clan.Kingdom approach. State which basis is correct for the intent "the kingdom/faction the recruiter currently serves" and whether this is a real defect or a benign equivalence.

2. GARRISON PATH. The override covers MaximumIndexHeroCanRecruitFromHero but NOT MaximumIndexGarrisonCanRecruitFromHero (vanilla above, passes settlement.Owner as buyer). Determine whether garrison auto-recruit actually invokes the Garrison method in v1.4.6 (grep callers) or whether garrison recruitment is routed through MaximumIndexHeroCanRecruitFromHero with `mobileParty.Party.Owner` as buyer (per the AI consumer above). If the Garrison method is live and unoverridden, is there a scenario where a free owner's garrison auto-recruits evil troops? Reason about whether owner-of-settlement == settlement-MapFaction always holds (so the alignment would never oppose anyway).

3. VILLAGE.MapFaction NULL SAFETY. `sellerHero.CurrentSettlement?.MapFaction?.StringId` -- decompile Settlement.MapFaction / Village.MapFaction. Village.MapFaction returns Bound.MapFaction with no null guard on Bound. Can the override throw for a village notable with a null Bound? Note vanilla's own line 18/20 dereferences `sellerHero.CurrentSettlement.MapFaction` WITHOUT a null-conditional -- so is the TAOM override strictly more defensive than vanilla, or does it introduce a NEW throw path vanilla doesn't have? Verdict: regression or parity?

4. MCM-SHADOWS-JSON. RecruitmentAlignmentSettingsProvider reads `TaomSettings.Instance?.X ?? _defaults.Y`. In-game TaomSettings.Instance is non-null, so the MCM value ALWAYS wins. The MCM default for AlignmentRecruitmentGoodRejectsEvilOnly is hardcoded `false`; the JSON `mode` field feeds `_defaults.GoodRejectsEvilOnly`. Therefore a user who sets `"mode": "GoodRejectsEvil"` in the JSON but never touches MCM gets Symmetric behavior in-game (MCM default false shadows the JSON). Is this a bug, or the intended MCM-over-JSON precedence used by every other TAOM settings provider (e.g., CastleRecruitmentSettingsProvider)? Should the feature doc warn that JSON mode is the test/compiled-default only and MCM is authoritative in-game?

5. isPlayer CLASSIFICATION. `isPlayer = buyerHero == Hero.MainHero`. For a player-clan party led by a COMPANION (not MainHero), isPlayer is false, so the recruiter is treated as "AI" and gated only when ApplyToAi is true. Is that the intended semantics ("the player" = MainHero only, companion-led clan parties follow the AI toggle), or should player-clan parties always be gated like the player? Low severity -- judge intent.

6. useValueAsRelation. When blocked, the override returns -1 and ignores useValueAsRelation; when not blocked it passes it to base. Is there any caller that passes a specific useValueAsRelation expecting a HYPOTHETICAL index (e.g., a preview/"if relation were X" probe) where a hard -1 would be wrong? Grep callers of MaximumIndexHeroCanRecruitFromHero with a non-default third arg.

## REQUIRED ANALYSIS

- BLOCK PREDICATE CORRECTNESS: read RecruitmentAlignmentService.IsRecruitmentBlocked. Verify: disabled -> false; AI recruiter when ApplyToAi false -> false; either side Neutral -> false; GoodRejectsEvil -> only (Free recruiter, Evil source); Symmetric -> both non-Neutral and different. Confirm there is NO path where a Neutral side blocks. Confirm the Neutral early-return is BEFORE both mode branches.
- CONFIG VALIDATION: read RecruitmentAlignmentConfigProvider. Unknown `mode` reverts to Symmetric with a warning; canonicalizes casing; missing file + malformed JSON fall back to defaults. Is the validation complete? Are bools (enabled, applyToAi) un-validatable (any parseable bool is valid)?
- KINGDOM ID COVERAGE: cross-reference -- are there any runtime kingdom StringIds that are NOT keys in alignment.json (would silently resolve Neutral = no block)? Use the cheatsheet. Flag any gap with the specific kingdom.
- TEST ADEQUACY: read both test files. Is the full (recruiterSide x sourceSide) matrix covered under both modes? Are disabled / applyToAi-false(player) / applyToAi-false(AI) / applyToAi-true(AI) all covered? Is RecruitmentAlignmentSettingsProvider untested acceptable (it is a thin MCM-over-JSON wrapper, mirrors the untested CastleRecruitmentSettingsProvider)?
- ADR COMPLIANCE: the service + providers must not reference TaleWorlds types (ADR-007); the model override must be a thin boundary (ADR-002, gamemodels.md rule 4 -- only id-extraction + a direct delegate/ternary, no inline if/foreach/switch). Confirm.

## FINDINGS OR OBSERVATIONS

For each Known Suspect: CONFIRMED (bug) / DISPUTED (not a bug) / DESIGN-QUESTION, with file:line and a one-line fix if confirmed. Then any ADDITIONAL findings with severity HIGH/MEDIUM/LOW. If you find nothing beyond the suspects, say so explicitly -- do not pad.

## QUALITY GATES

- Cite file:line for every claim; "I didn't find X" must be backed by a grep you actually ran.
- For any vanilla-API claim, decompile the installed v1.4.6 type, do not guess.
- Distinguish "diverges from vanilla" (may be intended) from "bug."
- Do NOT flag vanilla-matching code as a TAOM bug.

## PRIOR REVIEW LESSONS

SUCCESSES: config ID cross-ref catches rohan/dol_guldur mismatches; vanilla decompilation catches missing gates; lifecycle tracing catches stale caches.
FAILURES to avoid: Codex has previously assumed empire=Rohan (it is Dunland); flagged vanilla-matching code as bugs; skipped the hard decompile sections. Do not repeat these.

Write your full review to stdout (it is being captured to docs/reviews/codex-adversarial-AlignmentRecruitment-2026-06-17.md).
