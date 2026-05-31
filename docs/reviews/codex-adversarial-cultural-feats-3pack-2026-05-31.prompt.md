You are an adversarial code reviewer for TAOM, a Bannerlord v1.4.5 total-conversion mod (LOTR). Review the just-merged "cultural-feats 3-pack" delivery (issue #255): party-size retune + 3 new feats, village volunteer respawn-rate (new mechanism), per-settlement notable count (new mechanism). Confirm or dispute each Known Suspect, then find anything else. Be concrete: cite file:line, paste offending code, paste relevant vanilla decompile. Prefer DISPUTED-with-evidence over vague speculation. Output findings grouped by severity (CRITICAL / HIGH / MEDIUM / LOW), each with: claim, evidence, file:line, suggested fix.

FEATURE SUMMARY

15 new FeatObjects + 4 retunes; feat total 77 -> 92. Three new dimensions:

1. **Party size retune + 3 new feats.** Mordor 0.30 -> 0.10, Gundabad 0.30 -> 0.20, Dol Guldur 0.25 -> 0.20, Gondor 0.10 -> 0.025. Add Dunland (empire)/Rhun (khuzait)/Harad (aserai) +5% each via XSLT append. Also: TaomPartySizeModel was using `party.Owner?.Culture ?? party.Culture`, narrower than vanilla `PartyBaseHelper.HasFeat` precedence. Extracted ResolvePartyCulture to `CultureFeatAdapter.FromOrNull(PartyBase?)` shared by speed + size models (mirrors vanilla precedence: leader -> party -> owner -> settlement).

2. **Village volunteer respawn rate (new).** TaomVolunteerModel now overrides `GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)`. Wraps base `float` in `ExplainedNumber`, applies `ICulturalFeatsService.ApplyVolunteerRespawnFeats(culture, ref result)` keyed on `settlement?.OwnerClan?.Culture`, clamps `[0,1]`. New feats: Dunland +10%, Gundabad / Dol Guldur / Mordor +20%. No vanilla culture has a volunteer-rate feat; this is a brand-new hook site (matches vanilla's own ExplainedNumber+AddFactor pattern used for Cantons policy and CavalryTactics perk in the same vanilla method).

3. **Per-settlement notable count (new).** New `TaomNotableSpawnModel : DefaultNotableSpawnModel` overrides `GetTargetNotableCountForSettlement(Settlement settlement, Occupation occupation)`. Keyed on `settlement.Culture` (settlement IDENTITY, NOT OwnerClan; documented decision). Returns `(int)Math.Ceiling(base * (1 + bonus))` so any positive bonus on any non-zero base advances by >=1 (Mordor +5% on Artisan base 1 -> 2 nudges by 1 reliably). 8 new feats: Isengard/Dol Guldur town +50% village +10%, Mordor town/village +5%, Gundabad town/village +10%.

**Supporting XML work + fix already applied:** added 4 new RuralNotable templates (`spc_notable_{isengard,mordor,dolguldur,gundabad}_23`) to `characters/npcs_*.xml` AND registered each via `<template name="NPCCharacter.spc_notable_{culture}_23" />` in the culture's `<notable_templates>` block in `taom_spcultures.xml`. Without the registration the new NPC is unreachable (engine pools from the culture file, not the NPC file). Deep-review Agent 5 caught the missing registration; we fixed it.

TAOM ID CHEATSHEET

Custom cultures (in taom_spcultures.xml): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa
XSLT-wrapped cultures (in spcultures.xslt): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings/Rhun, sturgia=Dale, battania=Khand
NOTE: `rohan` / `dunland` / `harad` / `rhun` / `dale` / `khand` are NOT valid culture StringIds.

READ FIRST

- docs/features/cultural-feats.md
- docs/reviews/rca-cultural-feats-3pack-2026-05-31.md (the RCA written BEFORE this Codex pass; deep-review findings + their fix already applied)
- Main/Features/CulturalFeats/CultureFeatAdapter.cs (new FromOrNull(PartyBase?) overload)
- Main/Features/CulturalFeats/Models/TaomNotableSpawnModel.cs (NEW)
- Main/Features/TroopProgression/Models/TaomVolunteerModel.cs (extended)
- Main/Features/CulturalFeats/{TaomCulturalFeats.cs, ICulturalFeatsService.cs, CulturalFeatsService.cs}
- Main/Features/CulturalFeats/Models/{TaomPartySpeedModel.cs, TaomPartySizeModel.cs}
- Main/SubModule.cs (hoisted culturalFeats resolve)
- Main/_Module/ModuleData/{taom_spcultures.xml, spcultures.xslt, characters/npcs_{isengard,mordor,dolguldur,gundabad}.xml}
- TAOM.Tests/Features/CulturalFeats/{CulturalFeatsServiceTests.cs, TaomCulturalFeatsDefinitionTests.cs}

KNOWN SUSPECTS (confirm or dispute each, with evidence)

SUSPECT 1 (verify the deep-review fix is COMPLETE): the new `_23` RuralNotable templates were missing from `<notable_templates>` and we just added them. Independently verify by transforming SandBoxCore/ModuleData/spcultures.xml through TAOM's spcultures.xslt and reading the resulting `<notable_templates>` blocks for isengard, gundabad, dolguldur, mordor cultures. Confirm each block contains a `<template name="NPCCharacter.spc_notable_{culture}_23" />` line. Then independently verify the `_23` NPC definition exists in `characters/npcs_{culture}.xml` with `is_template="true" occupation="RuralNotable"`. Flag any culture where either layer is still missing. ALSO: check Mordor's Artisan slot — vanilla town target is 1 Artisan; `ceil(1 * 1.05) = 2`. Mordor has 2 Artisan templates (`spc_notable_mordor_8/_9`). Are BOTH listed in Mordor's `<notable_templates>` block? If only one is listed, we have the same unregistered-template bug for Artisans that we just fixed for RuralNotables.

SUSPECT 2 (HIGH PRIORITY -- vanilla NRE risk): vanilla `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` reads `hero.CurrentSettlement.MapFaction.Fiefs` without a null guard. Our TAOM override `TaomVolunteerModel.GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)` calls `base.GetDailyVolunteerProductionProbability(hero, index, settlement)` first, then wraps the result in `ExplainedNumber`, applies feats keyed on `settlement?.OwnerClan?.Culture`, clamps `[0,1]`. QUESTION: does our override ever get called with `hero.CurrentSettlement == null` in v1.4.5? The sole vanilla caller `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement(Settlement)` iterates `settlement.Notables`. For each notable, what guarantees `notable.CurrentSettlement != null`? Decompile that behavior + `Hero.CurrentSettlement` and report whether the invariant truly holds in all call paths (e.g. notable in jail, notable currently traveling, notable dead). If there's any code path that calls the model with a hero whose CurrentSettlement is null, vanilla NREs, and we inherit the NRE through `base`. Fix would be to guard inside our override before calling base.

SUSPECT 3 (volunteer culture resolution semantics): we key feats on `settlement?.OwnerClan?.Culture`. For a Mordor village that was just conquered by Gondor: the bonus should disappear on the next daily tick (intended — political bonus). Verify settlement.OwnerClan is correctly populated immediately after conquest and that the daily tick fires after the OwnerClan update. ALSO verify: villager parties traveling on the map use the village notable's daily probability when at the village; what does `settlement` refer to in `UpdateVolunteersOfNotablesInSettlement` for villages owned by a clan that the player has rebelled against (clan absent / kingdom decay)? Should we guard `settlement.OwnerClan?.Culture` returning null and short-circuit, or is `OwnerClan` always non-null for active villages?

SUSPECT 4 (notable count rounding edge cases): we use `(int)Math.Ceiling((double)baseCount * (1.0 + multiplier))`. Verify:
(a) Vanilla `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement` actually returns 1 for Artisan in towns in v1.4.5 (not 0 or 2). Decompile the full method body and quote it. If vanilla returns 0 for some (settlement, occupation) pair our `baseCount <= 0` early-return correctly skips. If vanilla returns 1 for Artisan, our +5% Mordor ceil(1.05) = 2 doubles Artisan count — confirm Mordor has 2 distinct Artisan templates and both are referenced in the notable_templates block (per Suspect 1).
(b) Floating-point precision: `(int)Math.Ceiling((double)2 * 1.05)` = `(int)Math.Ceiling(2.1)` = `3` (target). `(int)Math.Ceiling((double)1 * 1.05)` = `(int)Math.Ceiling(1.05)` = `2`. Any base+multiplier combination where double imprecision could produce `1.9999999` -> ceil -> 2 (intended 2) or `2.00000001` -> ceil -> 3 (unintended 3)? Walk a few cases.
(c) Negative or zero `multiplier`: defended by `if (multiplier <= 0f) return baseCount;` — but the multiplier itself is `float`, not `double`. Could the float -> double promotion in `(1.0 + multiplier)` cause issues? Probably not but verify.

SUSPECT 5 (CultureFeatAdapter.FromOrNull(PartyBase?) precedence parity): we mirror vanilla `PartyBaseHelper.HasFeat`. Decompile that helper from installed v1.4.5 TaleWorlds.CampaignSystem.dll and paste its body. Compare line-by-line to our `CultureFeatAdapter.FromOrNull(PartyBase?)` implementation. Confirm:
(a) The order is LeaderHero -> party.Culture -> Owner -> Settlement.
(b) The conditions are `party.LeaderHero != null` (NOT `LeaderHero.Culture != null`) — i.e., we key on the entity being non-null, return its Culture even if Culture happens to be null. Mirroring vanilla exactly.
(c) Any vanilla call site uses an alternative resolver (PartyBase.Culture directly without the precedence walk)? If so, are we sure our shared helper is the right replacement for both speed + size models?

SUSPECT 6 (SubModule.cs hoisting side-effects): we moved `var culturalFeats = IoC.Resolve<…>()` from line ~353 up to before line ~320 so TaomVolunteerModel can take it. Verify (a) no later model registration that previously reset/shadowed `culturalFeats` is now broken, (b) no IoC resolve-order dependency (does ICulturalFeatsService need a later-registered dependency that wasn't yet available at the hoist point?), (c) the original `var culturalFeats = IoC.Resolve<…>()` at the OLD location is removed (no duplicate `var` declaration compile error).

SUSPECT 7 (test reflection table hardcoded values): `CulturalFeatsServiceTests.EnsureFeatsInitialised` has a hardcoded `(_field, stringId, effectBonus)` table. We updated 4 retuned entries (Mordor 0.1, Gundabad 0.2, DolGuldur 0.2, Gondor 0.025) and added 15 new entries. Verify each retuned value matches `TaomCulturalFeats.cs` `InitializeAll` AND the description string in `InitializeAll` matches the new percentage (e.g. Gondor desc says "2.5%" not "10%"). Flag any drift.

SUSPECT 8 (per-culture EachCulture dict): `TaomCulturalFeatsDefinitionTests.EachCulture_HasExpectedFeatCount` updates 7 cultures + holds at unchanged for others. New sum must = 92. Walk: Erebor 7 + Rivendell 6 + Mirkwood 5 + Lothlorien 6 + Isengard 11 + Gundabad 9 + Umbar 5 + DolGuldur 8 + Gondor 7 + Mordor 11 + Rohan 6 + Dale 1 + Khand 1 + Rhun 2 + Harad 2 + Dunland 3 + Shaghana 1 + Abanissa 1. Confirm sum.

SUSPECT 9 (XSLT correctness, re-verify): aserai + khuzait + empire (Dunland) cultures should now each get MULTIPLE TAOM feat lines through their XSLT path:
- aserai: taom_harad_desert_speed + taom_harad_party_size (append template)
- khuzait: taom_rhun_steppe_speed + taom_rhun_party_size (append template)
- empire (Dunland): taom_dunland_plain_speed + taom_dunland_party_size + taom_dunland_volunteer_rate (inline override block)
Transform installed SandBoxCore/ModuleData/spcultures.xml through TAOM's spcultures.xslt; for each of the 6 transformed cultures (aserai, khuzait, sturgia, battania, empire, vlandia) report the FULL `<cultural_feats>` `<feat id=…/>` list and confirm vanilla feats are preserved (no accidental replacement) AND TAOM feats are present without duplicates AND each culture has exactly ONE `<cultural_feats>` element after transform (not zero, not two).

SUSPECT 10 (party-size balance change ripple): Mordor party size dropped 0.30 -> 0.10. AI army composition and `TaomMilitaryPowerModel` (configurable T7-T10 troop power) interplay -- any silent assumption in AI code that Mordor parties hold more troops? Grep for `MordorPartySizeFeat` or `Culture.mordor` party-size references outside CulturalFeatsService. Probably fine but verify. Same for Gondor 0.10 -> 0.025 (sharp reduction).

REQUIRED ANALYSIS

- VANILLA CODE: decompile from installed v1.4.5 DLLs (`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`) the FULL bodies of: `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement`, `DefaultVolunteerModel.GetDailyVolunteerProductionProbability`, `PartyBaseHelper.HasFeat`, `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`, and `SettlementHelper.SpawnNotablesIfNeeded`. Paste each. Identify any unguarded null deref or invariant we depend on.

- XML/XSLT TRANSFORM SANITY: transform installed SandBoxCore/ModuleData/spcultures.xml through TAOM's spcultures.xslt. For each of the 4 affected cultures + 6 XSLT-wrapped cultures, dump the post-transform `<cultural_feats>` AND `<notable_templates>` blocks. Report any culture where either block is missing/empty/duplicated.

- CROSS-REFERENCE: every `<feat id="taom_*_party_size"/>`, `*_volunteer_rate`, `*_notable_count_town`, `*_notable_count_village` in XML/XSLT must map to a Register()ed + Initialize()d + GetAllFeats()-yielded feat in TaomCulturalFeats.cs AND to an ApplyIfHas call in the correct service method/branch.

- TEST COVERAGE: confirm the EnsureFeatsInitialised reflection table + EachCulture dict numbers + DataRows all match the production code.

QUALITY GATES

- Do not flag vanilla-matching code as a bug.
- For every "missing" claim, grep before asserting -- the codebase may already have it.
- `rohan`/`harad`/etc. are NOT culture IDs; do not flag the use of vlandia/aserai/etc. as wrong.
- ExplainedNumber.AddFactor stacks additively by design.
- The new Math.Ceiling rounding rule is INTENTIONAL (user-confirmed). +5% on 1 = 2 is correct behavior. Do not flag as wrong; only flag if the rounding produces ARITHMETICALLY wrong values (e.g. ceil(0.99 * X) somehow producing X-1).
- The notable_templates two-layer registration was a HIGH bug we FIXED before this review. Only flag it if the fix is incomplete on any of the 4 cultures.

PRIOR REVIEW LESSONS

SUCCESSES (do these well): vanilla decompile catches missing gates; data-flow tracing catches declared-but-unused config; config ID cross-ref catches wrong culture IDs; calibrated hedging instead of over-asserting on findings you cannot prove from source.
FAILURES TO AVOID: do not assume empire=Rohan (empire=Dunland, vlandia=Rohan); do not flag vanilla-matching code; do not skip the hard vanilla-decompile section; do not produce self-contradictory findings (a 2026-05-28 deep-review agent claimed TerrainType.Snow does not exist while quoting vanilla code that uses `TerrainType.Snow`; verify enums against actual DLL); do not flag the notable-template registration as a new bug if the fix is in place on all 4 cultures.

OUTPUT

Lead with a findings summary table (# | Severity | Title | File | Confirmed/Disputed), then per-finding detail. End with overall Verdict: APPROVED / NEEDS FIXES, and the per-suspect verdict (S1..S10: confirmed / no-bug / disputed).
