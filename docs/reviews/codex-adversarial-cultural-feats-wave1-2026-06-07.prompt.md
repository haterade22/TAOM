# Codex Adversarial Review — Cultural Feats Wave 1 (24 new feats)

## Feature in one paragraph

TAOM Wave 1 cultural-feats expansion (commits bf9226f + ce07ebe, issue #273) adds 24 new cultural feats across 11 cultures, raising the feat total 105 -> 129. Every feat is "Q-class": it plugs into an EXISTING `CulturalFeatsService.Apply*` method via an added `culture.HasFeat(...)` check. No new GameModels, no new service methods, no Harmony patches, no conditional logic. The faction-map CC page (`factions.json`) gained 26 bonus lines surfacing the feats (Goblin's 2 feats appear on both Goblin Town and Blue Craig factions since both play the `goblin` culture).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, goblin=Goblin (Goblin Town + Blue Craig), mistymountainorcs=Misty Mountain Orcs (Moria)

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, goblin, mistymountainorcs
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Rhun/Easterlings, sturgia=Dale

NOTE: "rohan" is NOT a valid culture id (use vlandia). "dale"/"khand"/"harad"/"rhun" are NOT valid culture ids — those map to sturgia/battania/aserai/khuzait. The XSLT-wrapped cultures use the vanilla engine id; the feat string-id (e.g. taom_dale_tariff_income) uses the lore name but the `<cultural_feats>` block it lives in is keyed by the vanilla id (sturgia).

## READ FIRST
- `docs/features/cultural-feats.md` (feature doc, updated to 129)
- `Main/Features/CulturalFeats/TaomCulturalFeats.cs` (24 new field/accessor/Register/Initialize/GetAllFeats)
- `Main/Features/CulturalFeats/CulturalFeatsService.cs` (24 new HasFeat checks)
- `Main/_Module/ModuleData/taom_spcultures.xml` (custom culture <cultural_feats>)
- `Main/_Module/ModuleData/spcultures.xslt` (XSLT culture <cultural_feats> templates)
- `Main/_Module/ModuleData/factionmap/factions.json` (26 new bonus lines)
- `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` + `TaomCulturalFeatsDefinitionTests.cs`

## The 24 feats (key | culture | service method | effectBonus | additionType | isPositive)
1. taom_mordor_smithing | mordor | ApplySmithingFeats | -0.15 | AddFactor | true
2. taom_erebor_tariff_income | erebor | ApplyTariffIncomeFeats | 0.05 | AddFactor | true
3. taom_umbar_raid_damage | umbar | ApplyRaidDamageFeats | 0.20 | AddFactor | true
4. taom_umbar_food_consumption | umbar | ApplyFoodConsumptionFeats | -0.10 | AddFactor | true
5. taom_lothlorien_volunteer_rate | lothlorien | ApplyVolunteerRespawnFeats | -0.15 | AddFactor | false
6. taom_mirkwood_army_influence_cost | mirkwood | ApplyArmyInfluenceCost | 0.15 | AddFactor | false
7. taom_goblin_smithing | goblin | ApplySmithingFeats | -0.10 | AddFactor | true
8. taom_goblin_raid_damage | goblin | ApplyRaidDamageFeats | 0.10 | AddFactor | true
9. taom_mistymountainorcs_smithing | mistymountainorcs | ApplySmithingFeats | -0.15 | AddFactor | true
10. taom_mistymountainorcs_raid_damage | mistymountainorcs | ApplyRaidDamageFeats | 0.15 | AddFactor | true
11. taom_mistymountainorcs_construction_speed | mistymountainorcs | ApplyConstructionSpeedFeats | -0.10 | AddFactor | false
12. taom_dale_tariff_income | sturgia | ApplyTariffIncomeFeats | 0.10 | AddFactor | true
13. taom_dale_renown | sturgia | ApplyRenownFeats | 0.10 | AddFactor | true
14. taom_dale_loyalty | sturgia | ApplyLoyaltyFeats | -0.5 | Add | false
15. taom_khand_renown | battania | ApplyRenownFeats | 0.08 | AddFactor | true
16. taom_khand_tariff_income | battania | ApplyTariffIncomeFeats | -0.10 | AddFactor | false
17. taom_khand_food_consumption | battania | ApplyFoodConsumptionFeats | -0.10 | AddFactor | true
18. taom_khand_party_size | battania | ApplyPartySizeFeats | 0.05 | AddFactor | true
19. taom_harad_morale | aserai | ApplyMoraleFeats | 5 | Add | true
20. taom_harad_food_consumption | aserai | ApplyFoodConsumptionFeats | -0.15 | AddFactor | true
21. taom_harad_raid_damage | aserai | ApplyRaidDamageFeats | 0.15 | AddFactor | true
22. taom_harad_army_influence_cost | aserai | ApplyArmyInfluenceCost | 0.15 | AddFactor | false
23. taom_rhun_loyalty | khuzait | ApplyLoyaltyFeats | -0.5 | Add | false
24. taom_rhun_raid_damage | khuzait | ApplyRaidDamageFeats | 0.15 | AddFactor | true

## Known Suspects (CONFIRM or DISPUTE each)

1. **Sign/flag correctness for cost-reduction-as-positive feats.** The smithing/food feats use a NEGATIVE effectBonus with isPositive:true (cost reduction is good). Verify this matches the EXISTING convention in `ApplySmithingFeats`/`ApplyFoodConsumptionFeats` (e.g. existing Erebor smithing -0.3 isPositive:true). Confirm the new feats don't accidentally invert. CONFIRM or DISPUTE.

2. **Army influence cost penalty direction.** mirkwood_army_influence_cost and harad_army_influence_cost are +0.15 isPositive:false (a penalty — recruitment costs MORE). `ApplyArmyInfluenceCost` accumulates into a `multiplier` and returns `(int)(baseCost * (1 + multiplier))`. Existing discount feats (Gundabad -0.40, DolGuldur -0.50, Mordor -0.60) use NEGATIVE effectBonus. Verify +0.15 yields a 15% INCREASE (penalty) not a discount. CONFIRM or DISPUTE.

3. **Negative Add loyalty feats.** dale_loyalty and rhun_loyalty are -0.5 Add isPositive:false (loyalty DROPS 0.5/day). `ApplyLoyaltyFeats` uses `result.Add(EffectBonus, CultureText)`. Existing loyalty feats are all POSITIVE Add (Gondor +1, Erebor +1, etc.). Verify a negative Add correctly reduces daily loyalty and doesn't break the ExplainedNumber. Also verify a -0.5/day loyalty drain on a whole CULTURE (every Dale/Rhun settlement) isn't catastrophically destabilizing (revolt spiral) — flag if the magnitude could cause runaway settlement loss. CONFIRM or DISPUTE both correctness and balance.

4. **XSLT culture feat attachment.** The 13 XSLT-culture feats (dale/khand/harad/rhun) live in `Culture[@id='sturgia'|'battania'|'aserai'|'khuzait']/cultural_feats` templates in spcultures.xslt. Verify each template APPENDS via `<xsl:apply-templates select="@*|node()"/>` BEFORE the new `<feat/>` lines (preserving vanilla + prior TAOM terrain feats), and that the parent `Culture[@id='X']` template does NOT exclude `cultural_feats` from its passthrough (which would prevent the sub-template firing). A dropped passthrough = silently deleted vanilla feats. CONFIRM or DISPUTE.

5. **Register-id ↔ XML-id exact match.** Every `Register("taom_X")` string must EXACTLY equal a `<feat id="taom_X"/>`. A typo = dead feat. Cross-check all 24. CONFIRM or DISPUTE.

6. **factions.json U+2212 regression.** The #260 Phase 3 fix replaced all Unicode minus (U+2212) with ASCII hyphen-minus because the in-game font renders U+2212 as a low underscore. Verify the 26 new bonus lines use ASCII "-" not U+2212. CONFIRM or DISPUTE.

7. **Axis collision.** Confirm no new feat duplicates an axis a culture already has (e.g. Goblin already has party_size "Goblin Swarm +40%" — a 2nd party-size feat would silently stack). The author claims they dropped a proposed Goblin party-size feat for exactly this reason. Verify no (culture, axis) pair is now double-covered in a way that stacks unintentionally. CONFIRM or DISPUTE.

## REQUIRED SECTIONS

### 1. VANILLA CODE
Decompile and paste (use ilspycmd against the INSTALLED v1.4.5 DLLs at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`, NOT the E:\Decompiled_Bannerlord dump):
- `TaleWorlds.CampaignSystem.CharacterDevelopment.FeatObject.Initialize` (confirm the 5-arg signature the new feats call)
- `TaleWorlds.Library.ExplainedNumber.Add` and `.AddFactor` (confirm the overloads used: `.Add(float, TextObject)` and `.AddFactor(float)` / `.AddFactor(float, TextObject)`)
- The vanilla `DefaultSettlementLoyaltyModel.CalculateLoyaltyChange` (or whichever method TaomSettlementLoyaltyModel overrides) to confirm a negative culture loyalty add is summed into the daily loyalty change and won't underflow/assert.

### 2. Service routing audit
For each of the 24 feats, confirm the HasFeat check is in the CORRECT Apply* method (matching its axis), and that each Apply* method used is actually invoked by a live `Taom*Model` GameModel (ApplySmithingFeats->TaomSmithingModel, ApplyTariffIncomeFeats->TaomClanFinanceModel, ApplyRaidDamageFeats->TaomRaidModel, ApplyFoodConsumptionFeats->TaomFoodConsumptionModel, ApplyVolunteerRespawnFeats->TaomVolunteerModel, ApplyArmyInfluenceCost->TaomArmyManagementModel, ApplyConstructionSpeedFeats->TaomBuildingConstructionModel, ApplyRenownFeats->TaomBattleRewardModel, ApplyLoyaltyFeats->TaomSettlementLoyaltyModel, ApplyMoraleFeats->TaomPartyMoraleModel, ApplyPartySizeFeats->TaomPartySizeModel). If ANY method is never called by a model, every feat routed to it is DEAD — flag HIGH.

### 3. Balance pass
The new feats add to existing per-culture feat stacks. Flag any culture whose TOTAL stack now feels over/under-tuned (e.g. Harad now has +5 morale + -15% food + +15% raid + the inherited Aserai feats + terrain + party size — is that too strong?). Particularly scrutinize the two -0.5/day loyalty drains (Dale, Rhun) for revolt-spiral risk and the cost-reduction stacking.

### 4. CONFIG CROSS-REFERENCE
- Every feat string-id in TaomCulturalFeats.cs Register(...) appears in exactly one culture's <cultural_feats> block (correct file + correct culture).
- Every factions.json bonus line is keyed {=taom_faction_...} and the key exists in taom_module_strings.xml.
- The XSLT feats are under the correct vanilla culture id (sturgia not dale, etc.).

### 5. FINDINGS OR OBSERVATIONS
Per Known Suspect: CONFIRMED / DISPUTED + one-paragraph reason + file:line if a bug. Then any ADDITIONAL findings. If a section has no findings, write "no findings" — do not skip.

## QUALITY GATES
- Paste vanilla code as inline code blocks.
- Explicit CONFIRMED/DISPUTED verdict per Known Suspect.
- Severity per finding: CRITICAL / HIGH / MEDIUM / LOW / INFO.
- Name file + approximate line for each actionable finding.

## Prior review lessons
SUCCESSES: Codex #45 (prior cultural-feats change) caught a missing per-(culture, occupation) dispatch test — this Wave applied that lesson (24/24 dispatch tests present, verify). Config-id cross-ref caught rohan/dol_guldur typos historically. Vanilla decompile caught missing gates.
FAILURES: Codex once assumed empire=Rohan (it is Dunland) — use the cheatsheet. Codex sometimes flags vanilla-matching code as a bug — decompile first. Codex has skipped hard sections — do not skip; write "no findings" if clean. A prior Codex tooling finding (CRLF) was a false positive — verify I/O claims empirically.

## Output location
Write your review to `docs/reviews/codex-adversarial-cultural-feats-wave1-2026-06-07.md`.
