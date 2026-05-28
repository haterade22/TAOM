You are an adversarial code reviewer for TAOM, a Bannerlord v1.4.5 total-conversion mod (LOTR). Review the new "terrain-based cultural movement-speed feats" feature. Confirm or dispute each Known Suspect, then find anything else. Be concrete: cite file:line, paste the offending code, and for engine-behavior claims paste the vanilla decompile you relied on. Prefer DISPUTED-with-evidence over vague speculation. Output findings grouped by severity (HIGH / MEDIUM / LOW), each with: claim, evidence, file:line, suggested fix.

FEATURE SUMMARY
Each LOTR culture gets a flat party movement-speed bonus on its "home" terrain, plus a night bonus for Mordor. 18 new FeatObject feats added to the existing CulturalFeats system (total 59 -> 77). Applied via the existing GameModel override TaomPartySpeedModel -> ICulturalFeatsService.ApplyTerrainSpeedFeats. The old scaled ApplyForestSpeedFeats was removed; Mirkwood/Lothlorien forest feats reworked from scaled penalty-reduction to a flat +10%, and Rivendell gained a matching forest feat.

Bonus matrix: Forest -> mirkwood/lothlorien/rivendell (+10%); Snow -> erebor/gundabad (+10%); Steppe -> battania(Khand)/khuzait(Rhun) (+10%); Desert -> umbar/aserai(Harad)/shaghana/abanissa (+10%); Plain -> gondor/vlandia(Rohan)/sturgia(Dale)/empire(Dunland)/isengard (+10%); Swamp -> isengard (+10%); Mordor -> plain/swamp +5% and night +10%.

TAOM ID CHEATSHEET
Culture IDs (custom, in taom_spcultures.xml): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa
Culture IDs (XSLT/vanilla, in spcultures.xslt): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings/Rhun, sturgia=Dale, battania=Khand
NOTE: "rohan", "dunland", "harad", "rhun", "dale", "khand" are NOT valid culture StringIds -- those cultures use the vanilla engine IDs above.

READ FIRST
- docs/features/cultural-feats.md (the feature doc, has the full matrix + architecture)
- Main/Features/CulturalFeats/TerrainKind.cs (TAOM-owned enum)
- Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs (the GameModel override + MapTerrain boundary)
- Main/Features/CulturalFeats/CulturalFeatsService.cs (ApplyTerrainSpeedFeats)
- Main/Features/CulturalFeats/TaomCulturalFeats.cs (feat register/init/accessors)
- Main/Features/CulturalFeats/ICulturalFeatsService.cs (interface)
- Main/_Module/ModuleData/taom_spcultures.xml (custom-culture <cultural_feats> blocks)
- Main/_Module/ModuleData/spcultures.xslt (vanilla-wrapped culture feat overrides + append templates)
- TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs
- TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs

KNOWN SUSPECTS (confirm or dispute each, with evidence)

SUSPECT 1 (HIGH PRIORITY -- likely real): The Snow bonus may never fire. TaomPartySpeedModel.MapTerrain keys TerrainKind.Snow off TerrainType.Snow returned by Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace). But vanilla DefaultPartySpeedCalculatingModel.CalculateFinalSpeed does NOT get Snow from the face terrain -- it derives snow from WEATHER: it calls Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(party.Position.ToVec2()) and only when that returns Snowy or Blizzard does it set faceTerrainType = TerrainType.Snow and apply -0.1. The actual map faces returned by GetFaceTerrainType are Plain/Steppe/Mountain/Forest/etc. -- so TerrainType.Snow may essentially never be returned by GetFaceTerrainType. QUESTION FOR CODEX: Does GetFaceTerrainType ever return TerrainType.Snow on the TAOM campaign map, or is Snow purely a weather overlay? If the latter, the erebor/gundabad snow feats (taom_erebor_snow_speed, taom_gundabad_snow_speed) are dead in practice. Recommend whether the model should additionally (or instead) detect snowy WEATHER like vanilla does, and map that to TerrainKind.Snow. Paste the vanilla snow handling you find.

Vanilla CalculateFinalSpeed snow + night handling (from installed v1.4.5 DefaultPartySpeedCalculatingModel, pasted for reference):
```
MapWeatherModel.WeatherEvent weatherEventInPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mobileParty.Position.ToVec2());
if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy || weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
{
    faceTerrainType = TerrainType.Snow;
    finalSpeed.AddFactor(-0.1f, _snow);
}
if (!mobileParty.IsCurrentlyAtSea)
{
    if (Campaign.Current.IsNight)
    {
        finalSpeed.AddFactor(-0.25f, _night);
        ...
    }
}
```

SUSPECT 2: TerrainType.Snow enum membership. Confirm via the installed v1.4.5 TaleWorlds.Core.dll whether TerrainType has a Snow member (we believe Snow = 3 and the code compiles, but verify independently and report the value).

SUSPECT 3: XSLT template correctness. For aserai/khuzait/sturgia/battania, spcultures.xslt adds a dedicated template `<xsl:template match="Culture[@id='X']/cultural_feats">` that copies vanilla feats via `<xsl:apply-templates select="@*|node()"/>` then appends one TAOM feat. Confirm: (a) these templates actually fire (template priority vs the identity template `@*|node()`), (b) vanilla feats are preserved, (c) the inline-override cultures empire/vlandia (whose main template EXCLUDES cultural_feats from passthrough and emits an inline <cultural_feats> block) do NOT also get the append template applied -- i.e., no double cultural_feats element. The result of transforming SandBoxCore/ModuleData/spcultures.xml through this XSLT was observed as: aserai -> [aserai_cheaper_caravans, aserai_desert_speed, aserai_increased_wages, taom_harad_desert_speed]; battania -> [battanian_forest_speed, battanian_militia_production, battanian_slower_construction, taom_khand_steppe_speed]. Confirm this is correct and that no culture ends up with two <cultural_feats> elements.

SUSPECT 4: Night feat semantics. taom_mordor_night_speed (+10%) is applied OUTSIDE the terrain switch, gated only by isNight = Campaign.Current?.IsNight ?? false. Confirm it (a) applies on any terrain at night, (b) is additive with the terrain feat (Mordor on plain at night = +5% +10% via two AddFactor calls), and (c) correctly partially offsets vanilla's -25% night penalty (net result, not a true +10%). Is additive stacking of ExplainedNumber.AddFactor the intended/observed behavior here?

SUSPECT 5: Mordor magnitude. Confirm taom_mordor_plain_speed and taom_mordor_swamp_speed are 0.05 (5%) while taom_mordor_night_speed is 0.10 and all 15 other new feats + the 2 reworked elf feats are 0.10. Flag any wrong magnitude.

SUSPECT 6: Aserai double desert bonus. Vanilla aserai already has aserai_desert_speed (negates vanilla's -10% desert penalty for aserai). taom_harad_desert_speed ADDS another +10% on top. Confirm this is intended (Harad gets net positive desert speed) and not an accidental double-count. Same question for battania which keeps battanian_forest_speed AND now gets taom_khand_steppe_speed (different terrains, so fine -- but confirm).

SUSPECT 7: Feat registration vs culture XML deserialization ordering. The 18 new feats are Register()ed in a Harmony postfix on Campaign.InitializeDefaultCampaignObjects (existing TaomCulturalFeats.CreateAndRegister). Culture XML <feat id="..."/> references resolve these. Confirm the ordering is safe (the existing 59 feats use the same path) and that an unresolved feat id would not throw at culture deserialization.

REQUIRED ANALYSIS
- VANILLA CODE: decompile DefaultPartySpeedCalculatingModel.CalculateFinalSpeed and TerrainType from the installed DLLs at "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/" (TaleWorlds.CampaignSystem.dll, TaleWorlds.Core.dll). Use the installed DLLs as authoritative -- the dump at E:/Decompiled_Bannerlord is the same version but installed DLLs are canonical for signatures.
- CONFIG CROSS-REFERENCE: verify every <feat id="taom_*_speed"/> in taom_spcultures.xml and spcultures.xslt maps to a Register()ed + Initialize()d + GetAllFeats()-yielded feat in TaomCulturalFeats.cs, and to an ApplyIfHas call in the correct terrain case of ApplyTerrainSpeedFeats. Report any feat declared-but-not-applied, applied-but-not-declared, or placed in the wrong terrain case.
- HOT PATH: CalculateFinalSpeed runs per party per recalc. Flag any allocation / LINQ / IoC.Resolve in TaomPartySpeedModel.CalculateFinalSpeed, MapTerrain, CountMountedAndTotal, or ApplyTerrainSpeedFeats.

QUALITY GATES
- Do not flag code that merely matches vanilla behavior as a bug.
- For every "missing" claim, grep before asserting -- the codebase may already have it.
- "rohan"/"harad"/etc. are NOT culture IDs -- do not flag the use of vlandia/aserai/etc. as wrong.
- ExplainedNumber.AddFactor stacks additively by design -- only flag if a specific stack is wrong.

PRIOR REVIEW LESSONS
SUCCESSES: config ID cross-ref catches wrong culture IDs; vanilla decompilation catches missing gates / wrong assumptions; data-flow tracing catches declared-but-unused config.
FAILURES TO AVOID: do not assume empire=Rohan (empire=Dunland, vlandia=Rohan); do not flag vanilla-matching code; do not skip the hard vanilla-decompile section; a deep-review agent on this feature already produced a self-contradictory false positive claiming TerrainType.Snow does not exist (it does, Snow=3) -- verify enums against the actual DLL, do not trust confident assertions.

Write your review as structured markdown. Lead with a findings summary table (# | Severity | Title | File | Confirmed/Disputed), then per-finding detail.
