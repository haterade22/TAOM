# Adversarial Review: Career Passive Party-Size Fix (TAOM)

You are an adversarial code reviewer. Find real bugs. Confirm or DISPUTE each Known Suspect with evidence from the actual code. Be concrete -- cite file:line. Do NOT flag vanilla-matching code as bugs. Use the installed v1.4.5 DLLs as the API source of truth.

## SCOPE -- review ONLY these committed changes

Review git commit 991ef7e (diff 4c68256..991ef7e) on branch bannerlord-1.4.5. This is a career-passive bugfix. The working tree ALSO contains unrelated uncommitted "community/superpowers" changes (CLAUDE.md, .claude/rules/*, .claude/skills/*, docs/ai-includes/agent-teams.md, extra CHANGELOG entries) -- IGNORE those entirely; they are a separate author's WIP and NOT under review. Run `git show 991ef7e --stat` to see exactly the files in scope.

## Feature

TAOM career system: each hero career grants passive bonuses defined in taom_career_choices.xml, parsed by CareerConfigProvider into PassiveEffect objects, cached by CareerPassiveService, and applied by GameModels (e.g. TaomPartySizeModel) via ICareerPassiveService.ApplyFactor (multiplicative) or ApplyFlat (additive).

## Two bugs fixed

Issue A (#249): TaomPartySizeModel applied the PartySize passive via ApplyFactor. PartySize magnitudes are whole counts (2-6; description "+2 party size"). AddFactor(2) sets SumOfFactors += 2 so result = BaseNumber + BaseNumber*2 = x3 the base (~75 -> ~225). Fix: use ApplyFlat (result.Add) instead.

Issue B (#250): CareerConfigProvider.ParseChoice read only a direct <PassiveEffect> child and only the magnitude= attribute. 310 choices authored as <PassiveEffects><PassiveEffect value="0.10" /></PassiveEffects> (plural wrapper + value= attr) parsed to a null passive -- dead across all 16 cultures. Fix: fall back to el.Element("PassiveEffects")?.Element("PassiveEffect") and accept value= as an alias for magnitude= (magnitude= wins when both present). Also reconciled the 20 wrapped PartySize entries (value 0.10/0.12/0.14) to flat counts 4/5/6 + matching "+N party size" descriptions, propagated to 12 language files + 11 caches.

## VANILLA (verified against installed v1.4.5 TaleWorlds.CampaignSystem.dll)

ExplainedNumber.Add(float value, TextObject d=null, TextObject v=null): BaseNumber += value  (FLAT)
ExplainedNumber.AddFactor(float value, TextObject d=null): SumOfFactors += value
ExplainedNumber result: ResultNumber = MathF.Clamp(BaseNumber + BaseNumber * SumOfFactors, min, max)
So Add(2) on base 75 -> 77; AddFactor(2) on base 75 -> 225. The fix is correct for flat-count magnitudes.
DefaultPartySizeLimitModel.GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions=false) -- signature matches the TAOM override.

## READ FIRST
- docs/reviews/rca-career-partysize-2026-05-29.md (the RCA for this fix)
- docs/features/career-system.md (the "Two PassiveEffect schemas" + "Magnitude scale" sections)

## Known Suspects -- CONFIRM or DISPUTE each with file:line evidence

1. FLAT-VS-FACTOR ISOLATION. TaomPartySizeModel.cs now calls ApplyFlat for PassiveEffectType.PartySize. Verify NO other consumer of PassiveEffectType.PartySize still uses AddFactor, and that the 9 other campaign passive types (TroopWages, PartyMovementSpeed, TroopMorale, BattleRenownGain, TroopDamage, TroopUpgradeCost, EnchantmentCostReduction, PartySpottingRange, InventoryCapacity) STILL use ApplyFactor (their magnitudes are fractional, so they must stay factor). Grep all `_careerPassives.Apply*` call sites.

2. x-N REGRESSION FROM ACTIVATION. Activating 310 previously-dead wrapped passives: is there ANY effect type whose wrapped entries carry a WHOLE-COUNT magnitude AND whose consumer applies it via AddFactor (which would create a new "+N -> +N00%" bug like #249)? Check each PassiveEffectType used in wrapped entries against its consumer's apply method. Health (value 25/30/75) -> TaomAgentStatCalculateModel flat add; combat types (value 0.0x) -> CareerAgentStatService fractional; PartySize -> reconciled to flat magnitude 4/5/6 + ApplyFlat. Confirm no remaining `type="PartySize" value=` in taom_career_choices.xml.

3. PARSER FALLBACK EDGE CASES (CareerConfigProvider.ParseChoice). The fallback is `el.Element("PassiveEffect") ?? el.Element("PassiveEffects")?.Element("PassiveEffect")`. Magnitude is `ParseFloat(el,"magnitude", ParseFloat(el,"value",0f))`. Consider: (a) a choice with BOTH a direct child and a wrapper -- direct wins; is that the right precedence? (b) both magnitude= and value= present -- magnitude wins; correct? (c) value= malformed/NaN -- ParseFloat rejects NaN/Infinity and returns the default. (d) is the wrapped form ever expected to hold 2+ <PassiveEffect> children, and if so does the parser silently drop the 2nd+? Verify against the real file whether any wrapper has >1 child.

4. SINGLE-CHILD WRAPPER ASSUMPTION. The parser reads only the FIRST <PassiveEffect> in a wrapper. CareerChoicesIntegrationTests asserts every type="Passive" choice yields a non-null passive, but does NOT assert single-child. Is a multi-child wrapper a latent silent-drop risk for future authoring? Should there be a guard/test? (Currently 0/310 wrappers have 2+ children -- verify.)

5. TOOLING (tools/fix_wrapped_partysize_translations.py). Byte-faithful BOM I/O? Idempotent? Key-scoped replacement that cannot corrupt unrelated strings? Dry-run gating? It replaces "+12%"/"+%12" -> "+5" and "+14%"/"+%14" -> "+6" within only the 13 keyed lines.

6. DESCRIPTION/MAGNITUDE CONSISTENCY. For the 20 wrapped PartySize entries, do the "+N party size" descriptions in taom_career_choices.xml + taom_career_strings.xml match their reconciled magnitudes (4/5/6)? Any stale "+12%"/"+14% party size" remaining in any of the 12 language files for the 13 changed keys?

## Files in scope (from commit 991ef7e)
- Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs
- Main/Features/CareerSystem/CareerConfigProvider.cs
- Main/_Module/ModuleData/career_system/taom_career_choices.xml
- Main/_Module/ModuleData/taom_career_strings.xml
- Main/_Module/ModuleData/Languages/*/std_taom_career_strings_*.xml (12)
- tools/translation_cache/*.json (11)
- tools/fix_wrapped_partysize_translations.py
- TAOM.Tests/Features/CareerSystem/CareerConfigProviderTests.cs
- TAOM.Tests/Features/CareerSystem/CareerPassiveServiceTests.cs
- TAOM.Tests/Features/CareerSystem/CareerChoicesIntegrationTests.cs

## Known/accepted (do NOT re-flag as new bugs)
- 5 PassiveEffectType values (Ammo, HorseChargeDamage, HorseHealth, TroopResistance, StealthBonus) have no consumer -- pre-existing, documented as a known limitation.
- The passive cache discards Operation/IsPercentage (flat-vs-factor is a per-consumer decision) -- a data-driven IsPercentage cache is a documented deferred refactor.
- Non-PL languages are AI first-draft translations.

## REQUIRED OUTPUT
1. Per Known Suspect: CONFIRMED / DISPUTED + file:line evidence.
2. FINDINGS: any real bug, with severity (HIGH/MED/LOW), file:line, and the minimal fix.
3. ANYTHING THE FIX MISSED: edge cases, regressions, test gaps.
4. If you find NO real bugs, say so explicitly -- do not invent findings.

Write your review as structured markdown.
