OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019e6ffa-28bf-7c92-97b1-3ab3b7971525
--------
user
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

ERROR: The process "65920" not found.
ERROR: The process "44600" not found.
codex
I’ll review this as an audit only: first I’m reading the feature surface and tests, then I’ll decompile the installed Bannerlord DLLs for the party-speed and terrain enum evidence before making any claims.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg --files Main/Features/CulturalFeats TAOM.Tests/Features/CulturalFeats docs/features Main/_Module/ModuleData | rg \"(cultural-feats\\.md|TerrainKind\\.cs|TaomPartySpeedModel\\.cs|CulturalFeatsService\\.cs|TaomCulturalFeats\\.cs|ICulturalFeatsService\\.cs|taom_spcultures\\.xml|spcultures\\.xslt|CulturalFeatsServiceTests\\.cs|TaomCulturalFeatsDefinitionTests\\.cs)"'$"' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "ApplyTerrainSpeedFeats|ApplyForestSpeedFeats|TerrainKind|CalculateFinalSpeed|CountMountedAndTotal|taom_.*_speed|cultural_feats|CreateAndRegister|GetAllFeats|Initialize|Register" docs/features/cultural-feats.md Main/Features/CulturalFeats Main/_Module/ModuleData/taom_spcultures.xml Main/_Module/ModuleData/spcultures.xslt TAOM.Tests/Features/CulturalFeats' in C:\Users\mikew\source\repos\TAOM
 succeeded in 541ms:
 M CHANGELOG.md
 M Main/Features/CulturalFeats/CulturalFeatsService.cs
 M Main/Features/CulturalFeats/ICulturalFeatsService.cs
 M Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs
 M Main/Features/CulturalFeats/TaomCulturalFeats.cs
 M Main/Features/TroopProgression/VolunteerRecruitmentService.cs
 M Main/_Module/ModuleData/spcultures.xslt
 M Main/_Module/ModuleData/taom_spcultures.xml
 M TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs
 M TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs
 M TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs
 M docs/features/cultural-feats.md
?? Main/Features/CulturalFeats/TerrainKind.cs
?? docs/reviews/codex-adversarial-cultural-feats-terrain-2026-05-28.md
?? docs/reviews/codex-adversarial-cultural-feats-terrain-2026-05-28.prompt.md

2026-05-28T19:05:45.152182Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/CulturalFeats TAOM.Tests/Features/CulturalFe …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2069ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/CulturalFeats TAOM.Tests/Features/CulturalFe …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-05-28T19:05:45.895646Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.8 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ApplyTerrainSpeedFeats|ApplyForestSpeedFeats|TerrainKind|Calcu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2812ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ApplyTerrainSpeedFeats|ApplyForestSpeedFeats|TerrainKind|Calcu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this shell, so I’m using PowerShell’s file search and line-numbered reads instead. The worktree has the expected feature files modified, plus unrelated TroopProgression changes that I’ll ignore unless they intersect this feature.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/CulturalFeatsService.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/TaomCulturalFeats.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/TerrainKind.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 549ms:
    1: using TaleWorlds.CampaignSystem;
    2: using TaleWorlds.CampaignSystem.GameComponents;
    3: using TaleWorlds.CampaignSystem.Party;
    4: using TaleWorlds.CampaignSystem.Roster;
    5: using TaleWorlds.Core;
    6: using TAOM.Features.CareerSystem;
    7: using TAOM.Features.CareerSystem.Domain;
    8: 
    9: namespace TAOM.Features.CulturalFeats.Models;
   10: 
   11: public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
   12: {
   13:     private readonly ICulturalFeatsService _feats;
   14:     private readonly ICareerPassiveService _careerPassives;
   15: 
   16:     public TaomPartySpeedModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
   17:     {
   18:         _feats = feats;
   19:         _careerPassives = careerPassives;
   20:     }
   21: 
   22:     public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
   23:     {
   24:         var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);
   25: 
   26:         // Boundary: convert sealed TaleWorlds types to primitives + adapter, then delegate.
   27:         // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
   28:         // scene transitions; `?.` short-circuit yields a null TerrainType which MapTerrain maps
   29:         // to TerrainKind.None so no terrain feat is applied.
   30:         var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
   31:         var terrain = MapTerrain(
   32:             Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
   33:         var isNight = Campaign.Current?.IsNight ?? false;
   34:         var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);
   35: 
   36:         _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
   37:         _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
   38:         _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
   39: 
   40:         return result;
   41:     }
   42: 
   43:     /// <summary>
   44:     /// Boundary helper — collapses a sealed <see cref="TroopRoster"/> down to the
   45:     /// two primitives <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/>
   46:     /// needs, keeping the service free of TaleWorlds types per ADR-007.
   47:     /// </summary>
   48:     /// <summary>
   49:     /// Boundary helper — maps the sealed TaleWorlds <see cref="TerrainType"/> (nullable
   50:     /// when the map scene is unavailable) to the TAOM-owned <see cref="TerrainKind"/> so
   51:     /// the service stays free of engine types (ADR-007). <see cref="TerrainType.Dune"/>
   52:     /// folds into <see cref="TerrainKind.Desert"/> to match vanilla's desert handling.
   53:     /// Any unmapped terrain (water, mountain, etc.) and a null input map to
   54:     /// <see cref="TerrainKind.None"/>.
   55:     /// </summary>
   56:     private static TerrainKind MapTerrain(TerrainType? terrain) => terrain switch
   57:     {
   58:         TerrainType.Plain => TerrainKind.Plain,
   59:         TerrainType.Forest => TerrainKind.Forest,
   60:         TerrainType.Swamp => TerrainKind.Swamp,
   61:         TerrainType.Steppe => TerrainKind.Steppe,
   62:         TerrainType.Desert => TerrainKind.Desert,
   63:         TerrainType.Dune => TerrainKind.Desert,
   64:         TerrainType.Snow => TerrainKind.Snow,
   65:         _ => TerrainKind.None,
   66:     };
   67: 
   68:     private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster)
   69:     {
   70:         int total = roster.TotalManCount;
   71:         int mounted = 0;
   72:         foreach (var element in roster.GetTroopRoster())
   73:         {
   74:             if (element.Character?.IsMounted == true)
   75:                 mounted += element.Number;
   76:         }
   77:         return (mounted, total);
   78:     }
   79: }

 succeeded in 557ms:
    1: using TaleWorlds.CampaignSystem;
    2: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    3: using TaleWorlds.Core;
    4: using TaleWorlds.Library;
    5: using TaleWorlds.Localization;
    6: 
    7: namespace TAOM.Features.CulturalFeats;
    8: 
    9: /// <summary>
   10: /// Concrete dispatch for <see cref="ICulturalFeatsService"/>. All methods are
   11: /// pure (no side effects beyond mutating the by-ref <see cref="ExplainedNumber"/>),
   12: /// no singleton lookups in the hot path, no per-call allocations beyond the
   13: /// shared <see cref="CultureText"/>. Each method's structure mirrors the
   14: /// original inline body of the corresponding <c>Taom*Model</c> override 1:1
   15: /// to keep the behavior-preserving refactor reviewable line-by-line.
   16: /// </summary>
   17: public sealed class CulturalFeatsService : ICulturalFeatsService
   18: {
   19:     // Phase 9b #144 — preserved verbatim from the original models so the
   20:     // ExplainedNumber description strings on the world-map tooltip stay
   21:     // identical post-refactor. The `try` guard is for unit tests where the
   22:     // TaleWorlds <c>GameTexts</c> static is uninitialised — the description
   23:     // is a pure UI string and `Add`/`AddFactor` accept null descriptions.
   24:     private static TextObject? _cultureText;
   25:     private static bool _cultureTextResolved;
   26:     private static TextObject? CultureText
   27:     {
   28:         get
   29:         {
   30:             if (_cultureTextResolved)
   31:                 return _cultureText;
   32:             try { _cultureText = GameTexts.FindText("str_culture"); }
   33:             catch { _cultureText = null; }
   34:             _cultureTextResolved = true;
   35:             return _cultureText;
   36:         }
   37:     }
   38: 
   39:     // ── ArmyManagement ──────────────────────────────────────────────────
   40: 
   41:     public float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward)
   42:     {
   43:         if (culture == null)
   44:             return baseAward;
   45: 
   46:         float result = baseAward;
   47:         if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
   48:             result += baseAward * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus;
   49:         if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
   50:             result += baseAward * TaomCulturalFeats.GondorArmyInfluenceFeat.EffectBonus;
   51:         return result;
   52:     }
   53: 
   54:     public int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost)
   55:     {
   56:         if (culture == null)
   57:             return baseCost;
   58: 
   59:         float multiplier = 0f;
   60:         if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
   61:             multiplier += TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus;
   62:         if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
   63:             multiplier += TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus;
   64:         if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
   65:             multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;
   66:         if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
   67:             multiplier += TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;
   68: 
   69:         return multiplier != 0f ? (int)(baseCost * (1f + multiplier)) : baseCost;
   70:     }
   71: 
   72:     // ── PartySpeed ──────────────────────────────────────────────────────
   73: 
   74:     public void ApplyTerrainSpeedFeats(
   75:         ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result)
   76:     {
   77:         if (culture == null)
   78:             return;
   79: 
   80:         switch (terrain)
   81:         {
   82:             case TerrainKind.Forest:
   83:                 ApplyIfHas(culture, TaomCulturalFeats.MirkwoodForestSpeedFeat, ref result);
   84:                 ApplyIfHas(culture, TaomCulturalFeats.LothlorienForestSpeedFeat, ref result);
   85:                 ApplyIfHas(culture, TaomCulturalFeats.RivendellForestSpeedFeat, ref result);
   86:                 break;
   87:             case TerrainKind.Snow:
   88:                 ApplyIfHas(culture, TaomCulturalFeats.EreborSnowSpeedFeat, ref result);
   89:                 ApplyIfHas(culture, TaomCulturalFeats.GundabadSnowSpeedFeat, ref result);
   90:                 break;
   91:             case TerrainKind.Steppe:
   92:                 ApplyIfHas(culture, TaomCulturalFeats.KhandSteppeSpeedFeat, ref result);
   93:                 ApplyIfHas(culture, TaomCulturalFeats.RhunSteppeSpeedFeat, ref result);
   94:                 break;
   95:             case TerrainKind.Desert:
   96:                 ApplyIfHas(culture, TaomCulturalFeats.UmbarDesertSpeedFeat, ref result);
   97:                 ApplyIfHas(culture, TaomCulturalFeats.HaradDesertSpeedFeat, ref result);
   98:                 ApplyIfHas(culture, TaomCulturalFeats.ShaghanaDesertSpeedFeat, ref result);
   99:                 ApplyIfHas(culture, TaomCulturalFeats.AbanissaDesertSpeedFeat, ref result);
  100:                 break;
  101:             case TerrainKind.Plain:
  102:                 ApplyIfHas(culture, TaomCulturalFeats.MordorPlainSpeedFeat, ref result);
  103:                 ApplyIfHas(culture, TaomCulturalFeats.GondorPlainSpeedFeat, ref result);
  104:                 ApplyIfHas(culture, TaomCulturalFeats.RohanPlainSpeedFeat, ref result);
  105:                 ApplyIfHas(culture, TaomCulturalFeats.DalePlainSpeedFeat, ref result);
  106:                 ApplyIfHas(culture, TaomCulturalFeats.DunlandPlainSpeedFeat, ref result);
  107:                 ApplyIfHas(culture, TaomCulturalFeats.IsengardPlainSpeedFeat, ref result);
  108:                 break;
  109:             case TerrainKind.Swamp:
  110:                 ApplyIfHas(culture, TaomCulturalFeats.MordorSwampSpeedFeat, ref result);
  111:                 ApplyIfHas(culture, TaomCulturalFeats.IsengardSwampSpeedFeat, ref result);
  112:                 break;
  113:         }
  114: 
  115:         if (isNight)
  116:             ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
  117:     }
  118: 
  119:     private static void ApplyIfHas(ICultureFeatAdapter culture, FeatObject feat, ref ExplainedNumber result)
  120:     {
  121:         if (culture.HasFeat(feat))
  122:             result.AddFactor(feat.EffectBonus, CultureText);
  123:     }
  124: 
  125:     public void ApplyRohanInfantryPenalty(
  126:         ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result)
  127:     {
  128:         if (culture == null || totalCount <= 0)
  129:             return;
  130:         if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
  131:             return;
  132:         if (mountedCount * 2 < totalCount)
  133:             result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
  134:     }
  135: 
  136:     // ── SettlementProsperity ───────────────────────────────────────────
  137: 
  138:     public void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  139:     {
  140:         if (culture == null)
  141:             return;
  142: 
  143:         // Match the original model's guard: only buff/nerf growth when result is still
  144:         // non-negative. Negative results are skipped wholesale per pre-refactor behavior.
  145:         if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
  146:             result.AddFactor(TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus, CultureText);
  147:         if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
  148:             result.AddFactor(TaomCulturalFeats.MirkwoodHearthGrowthFeat.EffectBonus, CultureText);
  149:         if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
  150:             result.AddFactor(TaomCulturalFeats.GondorHearthGrowthFeat.EffectBonus, CultureText);
  151:     }
  152: 
  153:     // ── SettlementMilitia ──────────────────────────────────────────────
  154: 
  155:     public void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  156:     {
  157:         if (culture == null)
  158:             return;
  159:         if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
  160:             result.Add(TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus);
  161:         if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
  162:             result.Add(TaomCulturalFeats.DolGuldurMilitiaProductionFeat.EffectBonus);
  163:     }
  164: 
  165:     // ── BuildingConstruction ───────────────────────────────────────────
  166: 
  167:     public void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  168:     {
  169:         if (culture == null)
  170:             return;
  171:         if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
  172:             result.AddFactor(TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus, CultureText);
  173:         if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
  174:             result.AddFactor(TaomCulturalFeats.LothlorienConstructionSpeedFeat.EffectBonus, CultureText);
  175:         if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
  176:             result.AddFactor(TaomCulturalFeats.DolGuldurConstructionSpeedFeat.EffectBonus, CultureText);
  177:         if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
  178:             result.AddFactor(TaomCulturalFeats.IsengardConstructionSpeedFeat.EffectBonus, CultureText);
  179:     }
  180: 
  181:     // ── VillageProduction ──────────────────────────────────────────────
  182: 
  183:     public void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result)
  184:     {
  185:         if (culture == null)
  186:             return;
  187:         if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
  188:             result.AddFactor(TaomCulturalFeats.EreborProductionFeat.EffectBonus, CultureText);
  189:         if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
  190:             result.AddFactor(TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus, CultureText);
  191:         if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
  192:             result.AddFactor(TaomCulturalFeats.MordorGrainProductionFeat.EffectBonus, CultureText);
  193:     }
  194: 
  195:     // ── Caravan ────────────────────────────────────────────────────────
  196: 
  197:     public int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost)
  198:     {
  199:         if (culture == null)
  200:             return baseCost;
  201:         if (culture.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat))
  202:             return MathF.Round(baseCost * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus));
  203:         return baseCost;
  204:     }
  205: 
  206:     // ── BattleReward ───────────────────────────────────────────────────
  207: 
  208:     public void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  209:     {
  210:         if (culture == null)
  211:             return;
  212:         if (culture.HasFeat(TaomCulturalFeats.UmbarRenownFeat))
  213:             result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);
  214:     }
  215: 
  216:     // ── PartyTroopUpgrade ──────────────────────────────────────────────
  217: 
  218:     public void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result)
  219:     {
  220:         if (culture == null || !isMounted)
  221:             return;
  222:         if (culture.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat))
  223:             result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);
  224:         if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
  225:             result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);
  226:     }
  227: 
  228:     // ── PartySize ──────────────────────────────────────────────────────
  229: 
  230:     public void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  231:     {
  232:         if (culture == null)
  233:             return;
  234:         if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
  235:             result.AddFactor(TaomCulturalFeats.MordorPartySizeFeat.EffectBonus, CultureText);
  236:         if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
  237:             result.AddFactor(TaomCulturalFeats.GundabadPartySizeFeat.EffectBonus, CultureText);
  238:         if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
  239:             result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);
  240:         if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
  241:             result.AddFactor(TaomCulturalFeats.IsengardPartySizeFeat.EffectBonus, CultureText);
  242:         if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
  243:             result.AddFactor(TaomCulturalFeats.GondorPartySizeFeat.EffectBonus, CultureText);
  244:     }
  245: 
  246:     // ── FoodConsumption ────────────────────────────────────────────────
  247: 
  248:     public void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  249:     {
  250:         if (culture == null)
  251:             return;
  252:         if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
  253:             result.AddFactor(TaomCulturalFeats.RivendellFoodConsumptionFeat.EffectBonus, CultureText);
  254:         if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
  255:             result.AddFactor(TaomCulturalFeats.MirkwoodFoodConsumptionFeat.EffectBonus, CultureText);
  256:         if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
  257:             result.AddFactor(TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus, CultureText);
  258:         if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
  259:             result.AddFactor(TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus, CultureText);
  260:     }
  261: 
  262:     // ── SettlementLoyalty ──────────────────────────────────────────────
  263: 
  264:     public void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  265:     {
  266:         if (culture == null)
  267:             return;
  268:         if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
  269:             result.Add(TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus, CultureText);
  270:         if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
  271:             result.Add(TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus, CultureText);
  272:         if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
  273:             result.Add(TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus, CultureText);
  274:         if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
  275:             result.Add(TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus, CultureText);
  276:         if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
  277:             result.Add(TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus, CultureText);
  278:     }
  279: 
  280:     // ── PartyMorale ────────────────────────────────────────────────────
  281: 
  282:     public void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  283:     {
  284:         if (culture == null)
  285:             return;
  286:         if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
  287:             result.Add(TaomCulturalFeats.GondorMoraleFeat.EffectBonus, CultureText);
  288:         if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
  289:             result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);
  290:         if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
  291:             result.Add(TaomCulturalFeats.EreborMoraleFeat.EffectBonus, CultureText);
  292:         if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
  293:             result.Add(TaomCulturalFeats.MirkwoodMoraleFeat.EffectBonus, CultureText);
  294:         if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
  295:             result.Add(TaomCulturalFeats.LothlorienMoraleFeat.EffectBonus, CultureText);
  296:     }
  297: 
  298:     // ── Smithing ───────────────────────────────────────────────────────
  299: 
  300:     public void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  301:     {
  302:         if (culture == null)
  303:             return;
  304:         if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
  305:             result.AddFactor(TaomCulturalFeats.EreborSmithingFeat.EffectBonus);
  306:         if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
  307:             result.AddFactor(TaomCulturalFeats.IsengardSmithingFeat.EffectBonus);
  308:     }
  309: 
  310:     // ── ClanFinance (tariffs) ──────────────────────────────────────────
  311: 
  312:     public void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  313:     {
  314:         if (culture == null)
  315:             return;
  316:         if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
  317:             result.AddFactor(TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus, CultureText);
  318:     }
  319: 
  320:     // ── Raid ───────────────────────────────────────────────────────────
  321: 
  322:     public void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
  323:     {
  324:         if (culture == null)
  325:             return;
  326:         if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
  327:             result.AddFactor(TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus, CultureText);
  328:         if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
  329:             result.AddFactor(TaomCulturalFeats.GundabadRaidDamageFeat.EffectBonus, CultureText);
  330:         if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
  331:             result.AddFactor(TaomCulturalFeats.IsengardRaidDamageFeat.EffectBonus, CultureText);
  332:     }
  333: }

 succeeded in 796ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    4: using TaleWorlds.Core;
    5: 
    6: namespace TAOM.Features.CulturalFeats;
    7: 
    8: public class TaomCulturalFeats
    9: {
   10:     private static TaomCulturalFeats _instance;
   11: 
   12:     private static TaomCulturalFeats Instance => _instance
   13:         ?? throw new InvalidOperationException(
   14:             "TaomCulturalFeats not initialized. Ensure Patch18_CulturalFeats is registered.");
   15: 
   16:     // Erebor
   17:     private FeatObject _ereborGarrisonWage;
   18:     private FeatObject _ereborProduction;
   19:     private FeatObject _ereborConstructionSpeed;
   20:     private FeatObject _ereborLoyalty;
   21:     private FeatObject _ereborMorale;
   22:     private FeatObject _ereborSmithing;
   23:     private FeatObject _ereborSnowSpeed;
   24: 
   25:     // Rivendell
   26:     private FeatObject _rivendellArmyInfluence;
   27:     private FeatObject _rivendellHearthGrowth;
   28:     private FeatObject _rivendellArmyInfluenceCost;
   29:     private FeatObject _rivendellFoodConsumption;
   30:     private FeatObject _rivendellLoyalty;
   31:     private FeatObject _rivendellForestSpeed;
   32: 
   33:     // Mirkwood
   34:     private FeatObject _mirkwoodForestSpeed;
   35:     private FeatObject _mirkwoodMilitiaProduction;
   36:     private FeatObject _mirkwoodHearthGrowth;
   37:     private FeatObject _mirkwoodFoodConsumption;
   38:     private FeatObject _mirkwoodMorale;
   39: 
   40:     // Lothlorien
   41:     private FeatObject _lothlorienForestSpeed;
   42:     private FeatObject _lothlorienGarrisonWage;
   43:     private FeatObject _lothlorienConstructionSpeed;
   44:     private FeatObject _lothlorienFoodConsumption;
   45:     private FeatObject _lothlorienLoyalty;
   46:     private FeatObject _lothlorienMorale;
   47: 
   48:     // Isengard
   49:     private FeatObject _isengardCheaperRecruits;
   50:     private FeatObject _isengardGarrisonWage;
   51:     private FeatObject _isengardDecisionPenalty;
   52:     private FeatObject _isengardPartySize;
   53:     private FeatObject _isengardConstructionSpeed;
   54:     private FeatObject _isengardSmithing;
   55:     private FeatObject _isengardRaidDamage;
   56:     private FeatObject _isengardPlainSpeed;
   57:     private FeatObject _isengardSwampSpeed;
   58: 
   59:     // Gundabad
   60:     private FeatObject _gundabadArmyInfluenceCost;
   61:     private FeatObject _gundabadGrainProduction;
   62:     private FeatObject _gundabadWage;
   63:     private FeatObject _gundabadPartySize;
   64:     private FeatObject _gundabadRaidDamage;
   65:     private FeatObject _gundabadSnowSpeed;
   66: 
   67:     // Umbar
   68:     private FeatObject _umbarCheaperCaravans;
   69:     private FeatObject _umbarRenown;
   70:     private FeatObject _umbarWage;
   71:     private FeatObject _umbarTariffIncome;
   72:     private FeatObject _umbarDesertSpeed;
   73: 
   74:     // Dol Guldur
   75:     private FeatObject _dolguldurArmyInfluenceCost;
   76:     private FeatObject _dolguldurMilitiaProduction;
   77:     private FeatObject _dolguldurConstructionSpeed;
   78:     private FeatObject _dolguldurPartySize;
   79:     private FeatObject _dolguldurFoodConsumption;
   80: 
   81:     // Gondor
   82:     private FeatObject _gondorGarrisonWage;
   83:     private FeatObject _gondorArmyInfluence;
   84:     private FeatObject _gondorHearthGrowth;
   85:     private FeatObject _gondorPartySize;
   86:     private FeatObject _gondorLoyalty;
   87:     private FeatObject _gondorMorale;
   88:     private FeatObject _gondorPlainSpeed;
   89: 
   90:     // Mordor
   91:     private FeatObject _mordorArmyInfluenceCost;
   92:     private FeatObject _mordorGrainProduction;
   93:     private FeatObject _mordorWage;
   94:     private FeatObject _mordorPartySize;
   95:     private FeatObject _mordorRaidDamage;
   96:     private FeatObject _mordorPlainSpeed;
   97:     private FeatObject _mordorSwampSpeed;
   98:     private FeatObject _mordorNightSpeed;
   99: 
  100:     // Rohan (XSLT culture — custom C# feats)
  101:     private FeatObject _rohanMountedCost;
  102:     private FeatObject _rohanMountedWage;
  103:     private FeatObject _rohanInfantrySpeed;
  104:     private FeatObject _rohanLoyalty;
  105:     private FeatObject _rohanMorale;
  106:     private FeatObject _rohanPlainSpeed;
  107: 
  108:     // Dale (XSLT culture — custom C# feats)
  109:     private FeatObject _dalePlainSpeed;
  110: 
  111:     // Khand (XSLT culture — custom C# feats)
  112:     private FeatObject _khandSteppeSpeed;
  113: 
  114:     // Rhun (XSLT culture — custom C# feats)
  115:     private FeatObject _rhunSteppeSpeed;
  116: 
  117:     // Harad (XSLT culture — custom C# feats)
  118:     private FeatObject _haradDesertSpeed;
  119: 
  120:     // Dunland (XSLT culture — custom C# feats)
  121:     private FeatObject _dunlandPlainSpeed;
  122: 
  123:     // Shaghana
  124:     private FeatObject _shaghanaDesertSpeed;
  125: 
  126:     // Abanissa
  127:     private FeatObject _abanissaDesertSpeed;
  128: 
  129:     // Erebor
  130:     public static FeatObject EreborGarrisonWageFeat => Instance._ereborGarrisonWage;
  131:     public static FeatObject EreborProductionFeat => Instance._ereborProduction;
  132:     public static FeatObject EreborConstructionSpeedFeat => Instance._ereborConstructionSpeed;
  133:     public static FeatObject EreborLoyaltyFeat => Instance._ereborLoyalty;
  134:     public static FeatObject EreborMoraleFeat => Instance._ereborMorale;
  135:     public static FeatObject EreborSmithingFeat => Instance._ereborSmithing;
  136:     public static FeatObject EreborSnowSpeedFeat => Instance._ereborSnowSpeed;
  137: 
  138:     // Rivendell
  139:     public static FeatObject RivendellArmyInfluenceFeat => Instance._rivendellArmyInfluence;
  140:     public static FeatObject RivendellHearthGrowthFeat => Instance._rivendellHearthGrowth;
  141:     public static FeatObject RivendellArmyInfluenceCostFeat => Instance._rivendellArmyInfluenceCost;
  142:     public static FeatObject RivendellFoodConsumptionFeat => Instance._rivendellFoodConsumption;
  143:     public static FeatObject RivendellLoyaltyFeat => Instance._rivendellLoyalty;
  144:     public static FeatObject RivendellForestSpeedFeat => Instance._rivendellForestSpeed;
  145: 
  146:     // Mirkwood
  147:     public static FeatObject MirkwoodForestSpeedFeat => Instance._mirkwoodForestSpeed;
  148:     public static FeatObject MirkwoodMilitiaProductionFeat => Instance._mirkwoodMilitiaProduction;
  149:     public static FeatObject MirkwoodHearthGrowthFeat => Instance._mirkwoodHearthGrowth;
  150:     public static FeatObject MirkwoodFoodConsumptionFeat => Instance._mirkwoodFoodConsumption;
  151:     public static FeatObject MirkwoodMoraleFeat => Instance._mirkwoodMorale;
  152: 
  153:     // Lothlorien
  154:     public static FeatObject LothlorienForestSpeedFeat => Instance._lothlorienForestSpeed;
  155:     public static FeatObject LothlorienGarrisonWageFeat => Instance._lothlorienGarrisonWage;
  156:     public static FeatObject LothlorienConstructionSpeedFeat => Instance._lothlorienConstructionSpeed;
  157:     public static FeatObject LothlorienFoodConsumptionFeat => Instance._lothlorienFoodConsumption;
  158:     public static FeatObject LothlorienLoyaltyFeat => Instance._lothlorienLoyalty;
  159:     public static FeatObject LothlorienMoraleFeat => Instance._lothlorienMorale;
  160: 
  161:     // Isengard
  162:     public static FeatObject IsengardCheaperRecruitsFeat => Instance._isengardCheaperRecruits;
  163:     public static FeatObject IsengardGarrisonWageFeat => Instance._isengardGarrisonWage;
  164:     public static FeatObject IsengardDecisionPenaltyFeat => Instance._isengardDecisionPenalty;
  165:     public static FeatObject IsengardPartySizeFeat => Instance._isengardPartySize;
  166:     public static FeatObject IsengardConstructionSpeedFeat => Instance._isengardConstructionSpeed;
  167:     public static FeatObject IsengardSmithingFeat => Instance._isengardSmithing;
  168:     public static FeatObject IsengardRaidDamageFeat => Instance._isengardRaidDamage;
  169:     public static FeatObject IsengardPlainSpeedFeat => Instance._isengardPlainSpeed;
  170:     public static FeatObject IsengardSwampSpeedFeat => Instance._isengardSwampSpeed;
  171: 
  172:     // Gundabad
  173:     public static FeatObject GundabadArmyInfluenceCostFeat => Instance._gundabadArmyInfluenceCost;
  174:     public static FeatObject GundabadGrainProductionFeat => Instance._gundabadGrainProduction;
  175:     public static FeatObject GundabadWageFeat => Instance._gundabadWage;
  176:     public static FeatObject GundabadPartySizeFeat => Instance._gundabadPartySize;
  177:     public static FeatObject GundabadRaidDamageFeat => Instance._gundabadRaidDamage;
  178:     public static FeatObject GundabadSnowSpeedFeat => Instance._gundabadSnowSpeed;
  179: 
  180:     // Umbar
  181:     public static FeatObject UmbarCheaperCaravansFeat => Instance._umbarCheaperCaravans;
  182:     public static FeatObject UmbarRenownFeat => Instance._umbarRenown;
  183:     public static FeatObject UmbarWageFeat => Instance._umbarWage;
  184:     public static FeatObject UmbarTariffIncomeFeat => Instance._umbarTariffIncome;
  185:     public static FeatObject UmbarDesertSpeedFeat => Instance._umbarDesertSpeed;
  186: 
  187:     // Dol Guldur
  188:     public static FeatObject DolGuldurArmyInfluenceCostFeat => Instance._dolguldurArmyInfluenceCost;
  189:     public static FeatObject DolGuldurMilitiaProductionFeat => Instance._dolguldurMilitiaProduction;
  190:     public static FeatObject DolGuldurConstructionSpeedFeat => Instance._dolguldurConstructionSpeed;
  191:     public static FeatObject DolGuldurPartySizeFeat => Instance._dolguldurPartySize;
  192:     public static FeatObject DolGuldurFoodConsumptionFeat => Instance._dolguldurFoodConsumption;
  193: 
  194:     // Gondor
  195:     public static FeatObject GondorGarrisonWageFeat => Instance._gondorGarrisonWage;
  196:     public static FeatObject GondorArmyInfluenceFeat => Instance._gondorArmyInfluence;
  197:     public static FeatObject GondorHearthGrowthFeat => Instance._gondorHearthGrowth;
  198:     public static FeatObject GondorPartySizeFeat => Instance._gondorPartySize;
  199:     public static FeatObject GondorLoyaltyFeat => Instance._gondorLoyalty;
  200:     public static FeatObject GondorMoraleFeat => Instance._gondorMorale;
  201:     public static FeatObject GondorPlainSpeedFeat => Instance._gondorPlainSpeed;
  202: 
  203:     // Mordor
  204:     public static FeatObject MordorArmyInfluenceCostFeat => Instance._mordorArmyInfluenceCost;
  205:     public static FeatObject MordorGrainProductionFeat => Instance._mordorGrainProduction;
  206:     public static FeatObject MordorWageFeat => Instance._mordorWage;
  207:     public static FeatObject MordorPartySizeFeat => Instance._mordorPartySize;
  208:     public static FeatObject MordorRaidDamageFeat => Instance._mordorRaidDamage;
  209:     public static FeatObject MordorPlainSpeedFeat => Instance._mordorPlainSpeed;
  210:     public static FeatObject MordorSwampSpeedFeat => Instance._mordorSwampSpeed;
  211:     public static FeatObject MordorNightSpeedFeat => Instance._mordorNightSpeed;
  212: 
  213:     // Rohan
  214:     public static FeatObject RohanMountedCostFeat => Instance._rohanMountedCost;
  215:     public static FeatObject RohanMountedWageFeat => Instance._rohanMountedWage;
  216:     public static FeatObject RohanInfantrySpeedFeat => Instance._rohanInfantrySpeed;
  217:     public static FeatObject RohanLoyaltyFeat => Instance._rohanLoyalty;
  218:     public static FeatObject RohanMoraleFeat => Instance._rohanMorale;
  219:     public static FeatObject RohanPlainSpeedFeat => Instance._rohanPlainSpeed;
  220: 
  221:     // Dale
  222:     public static FeatObject DalePlainSpeedFeat => Instance._dalePlainSpeed;
  223: 
  224:     // Khand
  225:     public static FeatObject KhandSteppeSpeedFeat => Instance._khandSteppeSpeed;
  226: 
  227:     // Rhun
  228:     public static FeatObject RhunSteppeSpeedFeat => Instance._rhunSteppeSpeed;
  229: 
  230:     // Harad
  231:     public static FeatObject HaradDesertSpeedFeat => Instance._haradDesertSpeed;
  232: 
  233:     // Dunland
  234:     public static FeatObject DunlandPlainSpeedFeat => Instance._dunlandPlainSpeed;
  235: 
  236:     // Shaghana
  237:     public static FeatObject ShaghanaDesertSpeedFeat => Instance._shaghanaDesertSpeed;
  238: 
  239:     // Abanissa
  240:     public static FeatObject AbanissaDesertSpeedFeat => Instance._abanissaDesertSpeed;
  241: 
  242:     public static void CreateAndRegister()
  243:     {
  244:         _instance = new TaomCulturalFeats();
  245:         _instance.RegisterAll();
  246:         _instance.InitializeAll();
  247:     }
  248: 
  249:     internal static void Reset() => _instance = null;
  250: 
  251:     private void RegisterAll()
  252:     {
  253:         _ereborGarrisonWage = Register("taom_erebor_garrison_wage");
  254:         _ereborProduction = Register("taom_erebor_production");
  255:         _ereborConstructionSpeed = Register("taom_erebor_construction_speed");
  256:         _ereborLoyalty = Register("taom_erebor_loyalty");
  257:         _ereborMorale = Register("taom_erebor_morale");
  258:         _ereborSmithing = Register("taom_erebor_smithing");
  259:         _ereborSnowSpeed = Register("taom_erebor_snow_speed");
  260: 
  261:         _rivendellArmyInfluence = Register("taom_rivendell_army_influence");
  262:         _rivendellHearthGrowth = Register("taom_rivendell_hearth_growth");
  263:         _rivendellArmyInfluenceCost = Register("taom_rivendell_army_influence_cost");
  264:         _rivendellFoodConsumption = Register("taom_rivendell_food_consumption");
  265:         _rivendellLoyalty = Register("taom_rivendell_loyalty");
  266:         _rivendellForestSpeed = Register("taom_rivendell_forest_speed");
  267: 
  268:         _mirkwoodForestSpeed = Register("taom_mirkwood_forest_speed");
  269:         _mirkwoodMilitiaProduction = Register("taom_mirkwood_militia_production");
  270:         _mirkwoodHearthGrowth = Register("taom_mirkwood_hearth_growth");
  271:         _mirkwoodFoodConsumption = Register("taom_mirkwood_food_consumption");
  272:         _mirkwoodMorale = Register("taom_mirkwood_morale");
  273: 
  274:         _lothlorienForestSpeed = Register("taom_lothlorien_forest_speed");
  275:         _lothlorienGarrisonWage = Register("taom_lothlorien_garrison_wage");
  276:         _lothlorienConstructionSpeed = Register("taom_lothlorien_construction_speed");
  277:         _lothlorienFoodConsumption = Register("taom_lothlorien_food_consumption");
  278:         _lothlorienLoyalty = Register("taom_lothlorien_loyalty");
  279:         _lothlorienMorale = Register("taom_lothlorien_morale");
  280: 
  281:         _isengardCheaperRecruits = Register("taom_isengard_cheaper_recruits");
  282:         _isengardGarrisonWage = Register("taom_isengard_garrison_wage");
  283:         _isengardDecisionPenalty = Register("taom_isengard_decision_penalty");
  284:         _isengardPartySize = Register("taom_isengard_party_size");
  285:         _isengardConstructionSpeed = Register("taom_isengard_construction_speed");
  286:         _isengardSmithing = Register("taom_isengard_smithing");
  287:         _isengardRaidDamage = Register("taom_isengard_raid_damage");
  288:         _isengardPlainSpeed = Register("taom_isengard_plain_speed");
  289:         _isengardSwampSpeed = Register("taom_isengard_swamp_speed");
  290: 
  291:         _gundabadArmyInfluenceCost = Register("taom_gundabad_army_influence_cost");
  292:         _gundabadGrainProduction = Register("taom_gundabad_grain_production");
  293:         _gundabadWage = Register("taom_gundabad_wage");
  294:         _gundabadPartySize = Register("taom_gundabad_party_size");
  295:         _gundabadRaidDamage = Register("taom_gundabad_raid_damage");
  296:         _gundabadSnowSpeed = Register("taom_gundabad_snow_speed");
  297: 
  298:         _umbarCheaperCaravans = Register("taom_umbar_cheaper_caravans");
  299:         _umbarRenown = Register("taom_umbar_renown");
  300:         _umbarWage = Register("taom_umbar_wage");
  301:         _umbarTariffIncome = Register("taom_umbar_tariff_income");
  302:         _umbarDesertSpeed = Register("taom_umbar_desert_speed");
  303: 
  304:         _dolguldurArmyInfluenceCost = Register("taom_dolguldur_army_influence_cost");
  305:         _dolguldurMilitiaProduction = Register("taom_dolguldur_militia_production");
  306:         _dolguldurConstructionSpeed = Register("taom_dolguldur_construction_speed");
  307:         _dolguldurPartySize = Register("taom_dolguldur_party_size");
  308:         _dolguldurFoodConsumption = Register("taom_dolguldur_food_consumption");
  309: 
  310:         _gondorGarrisonWage = Register("taom_gondor_garrison_wage");
  311:         _gondorArmyInfluence = Register("taom_gondor_army_influence");
  312:         _gondorHearthGrowth = Register("taom_gondor_hearth_growth");
  313:         _gondorPartySize = Register("taom_gondor_party_size");
  314:         _gondorLoyalty = Register("taom_gondor_loyalty");
  315:         _gondorMorale = Register("taom_gondor_morale");
  316:         _gondorPlainSpeed = Register("taom_gondor_plain_speed");
  317: 
  318:         _mordorArmyInfluenceCost = Register("taom_mordor_army_influence_cost");
  319:         _mordorGrainProduction = Register("taom_mordor_grain_production");
  320:         _mordorWage = Register("taom_mordor_wage");
  321:         _mordorPartySize = Register("taom_mordor_party_size");
  322:         _mordorRaidDamage = Register("taom_mordor_raid_damage");
  323:         _mordorPlainSpeed = Register("taom_mordor_plain_speed");
  324:         _mordorSwampSpeed = Register("taom_mordor_swamp_speed");
  325:         _mordorNightSpeed = Register("taom_mordor_night_speed");
  326: 
  327:         _rohanMountedCost = Register("taom_rohan_mounted_cost");
  328:         _rohanMountedWage = Register("taom_rohan_mounted_wage");
  329:         _rohanInfantrySpeed = Register("taom_rohan_infantry_speed");
  330:         _rohanLoyalty = Register("taom_rohan_loyalty");
  331:         _rohanMorale = Register("taom_rohan_morale");
  332:         _rohanPlainSpeed = Register("taom_rohan_plain_speed");
  333: 
  334:         _dalePlainSpeed = Register("taom_dale_plain_speed");
  335:         _khandSteppeSpeed = Register("taom_khand_steppe_speed");
  336:         _rhunSteppeSpeed = Register("taom_rhun_steppe_speed");
  337:         _haradDesertSpeed = Register("taom_harad_desert_speed");
  338:         _dunlandPlainSpeed = Register("taom_dunland_plain_speed");
  339:         _shaghanaDesertSpeed = Register("taom_shaghana_desert_speed");
  340:         _abanissaDesertSpeed = Register("taom_abanissa_desert_speed");
  341:     }
  342: 
  343:     private void InitializeAll()
  344:     {
  345:         // Erebor — Dwarves: cheap garrisons, strong production, slow construction
  346:         _ereborGarrisonWage.Initialize(
  347:             "{=taom_feat_erebor_gw}Dwarven Garrison",
  348:             "{=taom_feat_erebor_gw_desc}Garrison wages reduced by 25%.",
  349:             -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  350:         _ereborProduction.Initialize(
  351:             "{=taom_feat_erebor_p}Dwarven Industry",
  352:             "{=taom_feat_erebor_p_desc}All village production increased by 10%.",
  353:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  354:         _ereborConstructionSpeed.Initialize(
  355:             "{=taom_feat_erebor_cs}Dwarven Perfectionism",
  356:             "{=taom_feat_erebor_cs_desc}Construction speed reduced by 15%.",
  357:             -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  358:         _ereborLoyalty.Initialize(
  359:             "{=taom_feat_erebor_loy}Dwarven Honor",
  360:             "{=taom_feat_erebor_loy_desc}Settlement loyalty increased by 1 per day.",
  361:             1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  362:         _ereborMorale.Initialize(
  363:             "{=taom_feat_erebor_mor}Dwarven Stubbornness",
  364:             "{=taom_feat_erebor_mor_desc}Party morale increased by 5.",
  365:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  366:         _ereborSmithing.Initialize(
  367:             "{=taom_feat_erebor_sm}Master Smiths",
  368:             "{=taom_feat_erebor_sm_desc}Smithing energy cost reduced by 30%.",
  369:             -0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  370:         _ereborSnowSpeed.Initialize(
  371:             "{=taom_feat_erebor_ss}Mountain Folk",
  372:             "{=taom_feat_erebor_ss_desc}Party movement speed increased by 10% in snow.",
  373:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  374: 
  375:         // Rivendell — High Elves: strong armies, good growth, expensive to rally
  376:         _rivendellArmyInfluence.Initialize(
  377:             "{=taom_feat_riv_ai}Elven Wisdom",
  378:             "{=taom_feat_riv_ai_desc}Army influence award increased by 35%.",
  379:             0.35f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  380:         _rivendellHearthGrowth.Initialize(
  381:             "{=taom_feat_riv_hg}The Last Homely House",
  382:             "{=taom_feat_riv_hg_desc}Village hearth growth increased by 20%.",
  383:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  384:         _rivendellArmyInfluenceCost.Initialize(
  385:             "{=taom_feat_riv_aic}Elven Pride",
  386:             "{=taom_feat_riv_aic_desc}Army recruitment costs 25% more influence.",
  387:             0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  388:         _rivendellFoodConsumption.Initialize(
  389:             "{=taom_feat_riv_fc}Elven Frugality",
  390:             "{=taom_feat_riv_fc_desc}Party food consumption reduced by 15%.",
  391:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  392:         _rivendellLoyalty.Initialize(
  393:             "{=taom_feat_riv_loy}Elven Wisdom",
  394:             "{=taom_feat_riv_loy_desc}Settlement loyalty increased by 0.5 per day.",
  395:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  396:         _rivendellForestSpeed.Initialize(
  397:             "{=taom_feat_riv_fs}Woodland Grace",
  398:             "{=taom_feat_riv_fs_desc}Party movement speed increased by 10% in forests.",
  399:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  400: 
  401:         // Mirkwood — Wood Elves: forest masters, good militia, isolated
  402:         _mirkwoodForestSpeed.Initialize(
  403:             "{=taom_feat_mrk_fs}Woodland Realm",
  404:             "{=taom_feat_mrk_fs_desc}Party movement speed increased by 10% in forests.",
  405:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  406:         _mirkwoodMilitiaProduction.Initialize(
  407:             "{=taom_feat_mrk_mp}Silvan Wardens",
  408:             "{=taom_feat_mrk_mp_desc}25% increased chance of veteran militia.",
  409:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  410:         _mirkwoodHearthGrowth.Initialize(
  411:             "{=taom_feat_mrk_hg}Forest Isolation",
  412:             "{=taom_feat_mrk_hg_desc}Village hearth growth reduced by 20%.",
  413:             -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  414:         _mirkwoodFoodConsumption.Initialize(
  415:             "{=taom_feat_mrk_fc}Woodland Sustenance",
  416:             "{=taom_feat_mrk_fc_desc}Party food consumption reduced by 15%.",
  417:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  418:         _mirkwoodMorale.Initialize(
  419:             "{=taom_feat_mrk_mor}Woodland Bonds",
  420:             "{=taom_feat_mrk_mor_desc}Party morale increased by 3.",
  421:             3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  422: 
  423:         // Lothlorien — Golden Wood: forest speed, cheap garrisons, slow building
  424:         _lothlorienForestSpeed.Initialize(
  425:             "{=taom_feat_loth_fs}Golden Wood",
  426:             "{=taom_feat_loth_fs_desc}Party movement speed increased by 10% in forests.",
  427:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  428:         _lothlorienGarrisonWage.Initialize(
  429:             "{=taom_feat_loth_gw}Wardens of Lorien",
  430:             "{=taom_feat_loth_gw_desc}Garrison wages reduced by 20%.",
  431:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  432:         _lothlorienConstructionSpeed.Initialize(
  433:             "{=taom_feat_loth_cs}Timeless Craft",
  434:             "{=taom_feat_loth_cs_desc}Construction speed reduced by 10%.",
  435:             -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  436:         _lothlorienFoodConsumption.Initialize(
  437:             "{=taom_feat_loth_fc}Lembas Bread",
  438:             "{=taom_feat_loth_fc_desc}Party food consumption reduced by 15%.",
  439:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  440:         _lothlorienLoyalty.Initialize(
  441:             "{=taom_feat_loth_loy}Elven Grace",
  442:             "{=taom_feat_loth_loy_desc}Settlement loyalty increased by 0.5 per day.",
  443:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  444:         _lothlorienMorale.Initialize(
  445:             "{=taom_feat_loth_mor}Elven Harmony",
  446:             "{=taom_feat_loth_mor_desc}Party morale increased by 3.",
  447:             3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  448: 
  449:         // Isengard — Saruman: cheap mounted recruits, cheap garrisons, decision penalty
  450:         _isengardCheaperRecruits.Initialize(
  451:             "{=taom_feat_isen_cr}War Machine",
  452:             "{=taom_feat_isen_cr_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
  453:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  454:         _isengardGarrisonWage.Initialize(
  455:             "{=taom_feat_isen_gw}Orthanc Garrison",
  456:             "{=taom_feat_isen_gw_desc}Garrison wages reduced by 20%.",
  457:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  458:         _isengardDecisionPenalty.Initialize(
  459:             "{=taom_feat_isen_dp}Saruman's Grip",
  460:             "{=taom_feat_isen_dp_desc}Kingdom decision relationship penalties increased by 25%.",
  461:             0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  462:         _isengardPartySize.Initialize(
  463:             "{=taom_feat_isen_ps}Uruk-hai Legions",
  464:             "{=taom_feat_isen_ps_desc}Party size limit increased by 20%.",
  465:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  466:         _isengardConstructionSpeed.Initialize(
  467:             "{=taom_feat_isen_cs}Industrial Might",
  468:             "{=taom_feat_isen_cs_desc}Construction speed increased by 15%.",
  469:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  470:         _isengardSmithing.Initialize(
  471:             "{=taom_feat_isen_sm}Industrial Forges",
  472:             "{=taom_feat_isen_sm_desc}Smithing energy cost reduced by 20%.",
  473:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  474:         _isengardRaidDamage.Initialize(
  475:             "{=taom_feat_isen_rd}War Machine Raids",
  476:             "{=taom_feat_isen_rd_desc}Raid damage increased by 20%.",
  477:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  478:         _isengardPlainSpeed.Initialize(
  479:             "{=taom_feat_isen_ps2}Forced March",
  480:             "{=taom_feat_isen_ps2_desc}Party movement speed increased by 10% on plains.",
  481:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  482:         _isengardSwampSpeed.Initialize(
  483:             "{=taom_feat_isen_sws}Fenland Drillmasters",
  484:             "{=taom_feat_isen_sws_desc}Party movement speed increased by 10% in swamps.",
  485:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  486: 
  487:         // Gundabad — Orcs: cheap armies, grain production, expensive wages
  488:         _gundabadArmyInfluenceCost.Initialize(
  489:             "{=taom_feat_gun_aic}Orc Horde",
  490:             "{=taom_feat_gun_aic_desc}Army recruitment costs 40% less influence.",
  491:             -0.4f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  492:         _gundabadGrainProduction.Initialize(
  493:             "{=taom_feat_gun_gp}Plundered Stores",
  494:             "{=taom_feat_gun_gp_desc}Grain production increased by 15%.",
  495:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  496:         _gundabadWage.Initialize(
  497:             "{=taom_feat_gun_w}Plunder Demands",
  498:             "{=taom_feat_gun_w_desc}Party wages increased by 10%.",
  499:             0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  500:         _gundabadPartySize.Initialize(
  501:             "{=taom_feat_gun_ps}Mountain Swarm",
  502:             "{=taom_feat_gun_ps_desc}Party size limit increased by 30%.",
  503:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  504:         _gundabadRaidDamage.Initialize(
  505:             "{=taom_feat_gun_rd}Orc Pillagers",
  506:             "{=taom_feat_gun_rd_desc}Raid damage increased by 25%.",
  507:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  508:         _gundabadSnowSpeed.Initialize(
  509:             "{=taom_feat_gun_ss}Mountain Marauders",
  510:             "{=taom_feat_gun_ss_desc}Party movement speed increased by 10% in snow.",
  511:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  512: 
  513:         // Umbar — Corsairs: cheap caravans, battle renown, expensive wages
  514:         _umbarCheaperCaravans.Initialize(
  515:             "{=taom_feat_umb_cc}Corsair Trade",
  516:             "{=taom_feat_umb_cc_desc}Caravan formation cost reduced by 25%.",
  517:             -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  518:         _umbarRenown.Initialize(
  519:             "{=taom_feat_umb_r}Corsair Glory",
  520:             "{=taom_feat_umb_r_desc}Renown from battles increased by 8%.",
  521:             0.08f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  522:         _umbarWage.Initialize(
  523:             "{=taom_feat_umb_w}Corsair Greed",
  524:             "{=taom_feat_umb_w_desc}Party wages increased by 8%.",
  525:             0.08f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  526:         _umbarTariffIncome.Initialize(
  527:             "{=taom_feat_umb_ti}Corsair Trade Networks",
  528:             "{=taom_feat_umb_ti_desc}Tariff income increased by 15%.",
  529:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  530:         _umbarDesertSpeed.Initialize(
  531:             "{=taom_feat_umb_ds}Desert Corsairs",
  532:             "{=taom_feat_umb_ds_desc}Party movement speed increased by 10% in deserts.",
  533:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  534: 
  535:         // Dol Guldur — Shadow: cheap armies, veteran militia, slow construction
  536:         _dolguldurArmyInfluenceCost.Initialize(
  537:             "{=taom_feat_dg_aic}Shadow Command",
  538:             "{=taom_feat_dg_aic_desc}Army recruitment costs 50% less influence.",
  539:             -0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  540:         _dolguldurMilitiaProduction.Initialize(
  541:             "{=taom_feat_dg_mp}Dark Conscription",
  542:             "{=taom_feat_dg_mp_desc}20% increased chance of veteran militia.",
  543:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  544:         _dolguldurConstructionSpeed.Initialize(
  545:             "{=taom_feat_dg_cs}Ruinous Works",
  546:             "{=taom_feat_dg_cs_desc}Construction speed reduced by 20%.",
  547:             -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  548:         _dolguldurPartySize.Initialize(
  549:             "{=taom_feat_dg_ps}Dark Legions",
  550:             "{=taom_feat_dg_ps_desc}Party size limit increased by 25%.",
  551:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  552:         _dolguldurFoodConsumption.Initialize(
  553:             "{=taom_feat_dg_fc}Voracious Hordes",
  554:             "{=taom_feat_dg_fc_desc}Party food consumption increased by 10%.",
  555:             0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  556: 
  557:         // Gondor — Men of the West: cheap garrisons, army influence, depleted hearths
  558:         _gondorGarrisonWage.Initialize(
  559:             "{=taom_feat_gon_gw}Tower Guard",
  560:             "{=taom_feat_gon_gw_desc}Garrison wages reduced by 20%.",
  561:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  562:         _gondorArmyInfluence.Initialize(
  563:             "{=taom_feat_gon_ai}Gondorian Discipline",
  564:             "{=taom_feat_gon_ai_desc}Army influence award increased by 30%.",
  565:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  566:         _gondorHearthGrowth.Initialize(
  567:             "{=taom_feat_gon_hg}War-Depleted Lands",
  568:             "{=taom_feat_gon_hg_desc}Village hearth growth reduced by 15%.",
  569:             -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  570:         _gondorPartySize.Initialize(
  571:             "{=taom_feat_gon_ps}Standing Armies",
  572:             "{=taom_feat_gon_ps_desc}Party size limit increased by 10%.",
  573:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  574:         _gondorLoyalty.Initialize(
  575:             "{=taom_feat_gon_loy}Tower Guard Discipline",
  576:             "{=taom_feat_gon_loy_desc}Settlement loyalty increased by 1 per day.",
  577:             1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  578:         _gondorMorale.Initialize(
  579:             "{=taom_feat_gon_mor}Gondorian Resolve",
  580:             "{=taom_feat_gon_mor_desc}Party morale increased by 5.",
  581:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  582:         _gondorPlainSpeed.Initialize(
  583:             "{=taom_feat_gon_ps2}Men of the Fields",
  584:             "{=taom_feat_gon_ps2_desc}Party movement speed increased by 10% on plains.",
  585:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  586: 
  587:         // Mordor — Dark Lord: very cheap armies, grain production, very expensive wages
  588:         _mordorArmyInfluenceCost.Initialize(
  589:             "{=taom_feat_mor_aic}The Dark Lord's Will",
  590:             "{=taom_feat_mor_aic_desc}Army recruitment costs 60% less influence.",
  591:             -0.6f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  592:         _mordorGrainProduction.Initialize(
  593:             "{=taom_feat_mor_gp}Nurn Farmlands",
  594:             "{=taom_feat_mor_gp_desc}Grain production increased by 20%.",
  595:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  596:         _mordorWage.Initialize(
  597:             "{=taom_feat_mor_w}Dark Tribute",
  598:             "{=taom_feat_mor_w_desc}Party wages increased by 20%.",
  599:             0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  600:         _mordorPartySize.Initialize(
  601:             "{=taom_feat_mor_ps}Sauron's Hordes",
  602:             "{=taom_feat_mor_ps_desc}Party size limit increased by 30%.",
  603:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  604:         _mordorRaidDamage.Initialize(
  605:             "{=taom_feat_mor_rd}Sauron's Wrath",
  606:             "{=taom_feat_mor_rd_desc}Raid damage increased by 25%.",
  607:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  608:         _mordorPlainSpeed.Initialize(
  609:             "{=taom_feat_mor_pls}Shadow March",
  610:             "{=taom_feat_mor_pls_desc}Party movement speed increased by 5% on plains.",
  611:             0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  612:         _mordorSwampSpeed.Initialize(
  613:             "{=taom_feat_mor_sws}Dead Marshes",
  614:             "{=taom_feat_mor_sws_desc}Party movement speed increased by 5% in swamps.",
  615:             0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  616:         _mordorNightSpeed.Initialize(
  617:             "{=taom_feat_mor_ns}Creatures of the Dark",
  618:             "{=taom_feat_mor_ns_desc}Party movement speed increased by 10% at night.",
  619:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  620: 
  621:         // Rohan — Horse-lords: cheap mounted troops, slow without cavalry
  622:         _rohanMountedCost.Initialize(
  623:             "{=taom_feat_roh_mc}Horse-lord Heritage",
  624:             "{=taom_feat_roh_mc_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
  625:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  626:         _rohanMountedWage.Initialize(
  627:             "{=taom_feat_roh_mw}Riders of the Mark",
  628:             "{=taom_feat_roh_mw_desc}Mounted troop wages reduced by 15%.",
  629:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  630:         _rohanInfantrySpeed.Initialize(
  631:             "{=taom_feat_roh_is}Cavalry Dependent",
  632:             "{=taom_feat_roh_is_desc}Party speed reduced by 10% when majority infantry.",
  633:             -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  634:         _rohanLoyalty.Initialize(
  635:             "{=taom_feat_roh_loy}Horse-lord Fellowship",
  636:             "{=taom_feat_roh_loy_desc}Settlement loyalty increased by 0.5 per day.",
  637:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  638:         _rohanMorale.Initialize(
  639:             "{=taom_feat_roh_mor}Riders' Spirit",
  640:             "{=taom_feat_roh_mor_desc}Party morale increased by 5.",
  641:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  642:         _rohanPlainSpeed.Initialize(
  643:             "{=taom_feat_roh_pls}Riders of the Plains",
  644:             "{=taom_feat_roh_pls_desc}Party movement speed increased by 10% on plains.",
  645:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  646: 
  647:         // Dale — Men of Dale: traders of the vale
  648:         _dalePlainSpeed.Initialize(
  649:             "{=taom_feat_dale_pls}Vale Traders",
  650:             "{=taom_feat_dale_pls_desc}Party movement speed increased by 10% on plains.",
  651:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  652: 
  653:         // Khand — Variags: steppe charioteers
  654:         _khandSteppeSpeed.Initialize(
  655:             "{=taom_feat_khand_sts}Steppe Charioteers",
  656:             "{=taom_feat_khand_sts_desc}Party movement speed increased by 10% on steppes.",
  657:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  658: 
  659:         // Rhun — Easterlings: mounted outriders
  660:         _rhunSteppeSpeed.Initialize(
  661:             "{=taom_feat_rhun_sts}Easterling Outriders",
  662:             "{=taom_feat_rhun_sts_desc}Party movement speed increased by 10% on steppes.",
  663:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  664: 
  665:         // Harad — Haradrim: sons of the sun
  666:         _haradDesertSpeed.Initialize(
  667:             "{=taom_feat_har_ds}Sons of the Sun",
  668:             "{=taom_feat_har_ds_desc}Party movement speed increased by 10% in deserts.",
  669:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  670: 
  671:         // Dunland — Hill-men: hill marchers
  672:         _dunlandPlainSpeed.Initialize(
  673:             "{=taom_feat_dun_pls}Hill Marchers",
  674:             "{=taom_feat_dun_pls_desc}Party movement speed increased by 10% on plains.",
  675:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  676: 
  677:         // Shaghana — southern Haradrim: dune wanderers
  678:         _shaghanaDesertSpeed.Initialize(
  679:             "{=taom_feat_shg_ds}Dune Wanderers",
  680:             "{=taom_feat_shg_ds_desc}Party movement speed increased by 10% in deserts.",
  681:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  682: 
  683:         // Abanissa — deep-south Haradrim: deep desert nomads
  684:         _abanissaDesertSpeed.Initialize(
  685:             "{=taom_feat_aba_ds}Deep Desert Nomads",
  686:             "{=taom_feat_aba_ds_desc}Party movement speed increased by 10% in deserts.",
  687:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  688:     }
  689: 
  690:     private static FeatObject Register(string stringId)
  691:         => Game.Current.ObjectManager.RegisterPresumedObject(new FeatObject(stringId));
  692: 
  693:     internal static IEnumerable<FeatObject> GetAllFeats()
  694:     {
  695:         if (_instance == null)
  696:             yield break;
  697: 
  698:         yield return _instance._ereborGarrisonWage;
  699:         yield return _instance._ereborProduction;
  700:         yield return _instance._ereborConstructionSpeed;
  701:         yield return _instance._ereborLoyalty;
  702:         yield return _instance._ereborMorale;
  703:         yield return _instance._ereborSmithing;
  704:         yield return _instance._ereborSnowSpeed;
  705:         yield return _instance._rivendellArmyInfluence;
  706:         yield return _instance._rivendellHearthGrowth;
  707:         yield return _instance._rivendellArmyInfluenceCost;
  708:         yield return _instance._rivendellFoodConsumption;
  709:         yield return _instance._rivendellLoyalty;
  710:         yield return _instance._rivendellForestSpeed;
  711:         yield return _instance._mirkwoodForestSpeed;
  712:         yield return _instance._mirkwoodMilitiaProduction;
  713:         yield return _instance._mirkwoodHearthGrowth;
  714:         yield return _instance._mirkwoodFoodConsumption;
  715:         yield return _instance._mirkwoodMorale;
  716:         yield return _instance._lothlorienForestSpeed;
  717:         yield return _instance._lothlorienGarrisonWage;
  718:         yield return _instance._lothlorienConstructionSpeed;
  719:         yield return _instance._lothlorienFoodConsumption;
  720:         yield return _instance._lothlorienLoyalty;
  721:         yield return _instance._lothlorienMorale;
  722:         yield return _instance._isengardCheaperRecruits;
  723:         yield return _instance._isengardGarrisonWage;
  724:         yield return _instance._isengardDecisionPenalty;
  725:         yield return _instance._isengardPartySize;
  726:         yield return _instance._isengardConstructionSpeed;
  727:         yield return _instance._isengardSmithing;
  728:         yield return _instance._isengardRaidDamage;
  729:         yield return _instance._isengardPlainSpeed;
  730:         yield return _instance._isengardSwampSpeed;
  731:         yield return _instance._gundabadArmyInfluenceCost;
  732:         yield return _instance._gundabadGrainProduction;
  733:         yield return _instance._gundabadWage;
  734:         yield return _instance._gundabadPartySize;
  735:         yield return _instance._gundabadRaidDamage;
  736:         yield return _instance._gundabadSnowSpeed;
  737:         yield return _instance._umbarCheaperCaravans;
  738:         yield return _instance._umbarRenown;
  739:         yield return _instance._umbarWage;
  740:         yield return _instance._umbarTariffIncome;
  741:         yield return _instance._umbarDesertSpeed;
  742:         yield return _instance._dolguldurArmyInfluenceCost;
  743:         yield return _instance._dolguldurMilitiaProduction;
  744:         yield return _instance._dolguldurConstructionSpeed;
  745:         yield return _instance._dolguldurPartySize;
  746:         yield return _instance._dolguldurFoodConsumption;
  747:         yield return _instance._gondorGarrisonWage;
  748:         yield return _instance._gondorArmyInfluence;
  749:         yield return _instance._gondorHearthGrowth;
  750:         yield return _instance._gondorPartySize;
  751:         yield return _instance._gondorLoyalty;
  752:         yield return _instance._gondorMorale;
  753:         yield return _instance._gondorPlainSpeed;
  754:         yield return _instance._mordorArmyInfluenceCost;
  755:         yield return _instance._mordorGrainProduction;
  756:         yield return _instance._mordorWage;
  757:         yield return _instance._mordorPartySize;
  758:         yield return _instance._mordorRaidDamage;
  759:         yield return _instance._mordorPlainSpeed;
  760:         yield return _instance._mordorSwampSpeed;
  761:         yield return _instance._mordorNightSpeed;
  762:         yield return _instance._rohanMountedCost;
  763:         yield return _instance._rohanMountedWage;
  764:         yield return _instance._rohanInfantrySpeed;
  765:         yield return _instance._rohanLoyalty;
  766:         yield return _instance._rohanMorale;
  767:         yield return _instance._rohanPlainSpeed;
  768:         yield return _instance._dalePlainSpeed;
  769:         yield return _instance._khandSteppeSpeed;
  770:         yield return _instance._rhunSteppeSpeed;
  771:         yield return _instance._haradDesertSpeed;
  772:         yield return _instance._dunlandPlainSpeed;
  773:         yield return _instance._shaghanaDesertSpeed;
  774:         yield return _instance._abanissaDesertSpeed;
  775:     }
  776: }

 succeeded in 1040ms:
    1: namespace TAOM.Features.CulturalFeats;
    2: 
    3: /// <summary>
    4: /// TAOM-owned terrain classification used by the cultural terrain-speed feats.
    5: /// The model maps the sealed TaleWorlds <c>TerrainType</c> to this enum at the
    6: /// boundary so <see cref="ICulturalFeatsService"/> stays free of engine types
    7: /// (ADR-007). Only the terrains TAOM grants bonuses on are represented; every
    8: /// other <c>TerrainType</c> maps to <see cref="None"/>.
    9: /// </summary>
   10: public enum TerrainKind
   11: {
   12:     None = 0,
   13:     Plain,
   14:     Forest,
   15:     Swamp,
   16:     Steppe,
   17:     Desert,
   18:     Snow,
   19: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/TaomCulturalFeats.cs' | ForEach-Object { "'$i++; if ($i -ge 343 -and $i -le 688) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/ICulturalFeatsService.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/_Module/ModuleData/spcultures.xslt' | ForEach-Object { "'$i++; if ($_ -match "template match=|cultural_feats|taom_.*_speed|aserai|khuzait|sturgia|battania|vlandia|empire|apply-templates|copy") { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/_Module/ModuleData/taom_spcultures.xml' | ForEach-Object { "'$i++; if ($_ -match '"'taom_.*_speed|<Culture|</Culture>|<cultural_feats>|</cultural_feats>|<feat') { '{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 508ms:
    1: using TaleWorlds.CampaignSystem;
    2: using TaleWorlds.Core;
    3: 
    4: namespace TAOM.Features.CulturalFeats;
    5: 
    6: /// <summary>
    7: /// Centralises the per-feat dispatch logic for the 16 <c>Taom*Model</c>
    8: /// overrides in <see cref="Models"/>. Each method takes a boundary-converted
    9: /// <see cref="ICultureFeatAdapter"/> (or null when the source has no owning
   10: /// culture) and an in-flight <see cref="ExplainedNumber"/> by-ref, applies any
   11: /// matching cultural feats to that result, and returns.
   12: ///
   13: /// Career-passive integration is intentionally NOT part of this service — those
   14: /// remain owned by <c>ICareerPassiveService</c> and the model overrides still
   15: /// call them directly at the boundary. The single-responsibility line:
   16: /// <i>cultural feats only</i>.
   17: ///
   18: /// All methods are no-ops when <paramref name="culture"/> is null. Issues #144,
   19: /// #176.
   20: /// </summary>
   21: public interface ICulturalFeatsService
   22: {
   23:     // ── ArmyManagement ──────────────────────────────────────────────────
   24:     /// <summary>Applies Rivendell + Gondor army-influence-award factors (additive).</summary>
   25:     float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward);
   26: 
   27:     /// <summary>Applies Rivendell, Gundabad, Dol Guldur, Mordor army-influence-COST factors (additive).</summary>
   28:     int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost);
   29: 
   30:     // ── PartySpeed ──────────────────────────────────────────────────────
   31:     /// <summary>
   32:     /// Applies the per-culture terrain movement-speed feats whose terrain matches
   33:     /// <paramref name="terrain"/> (forest/snow/steppe/desert/plain/swamp), plus the
   34:     /// Mordor night-speed feat when <paramref name="isNight"/>. Flat <c>AddFactor</c>
   35:     /// of each matching feat's <c>EffectBonus</c>; no-op for <see cref="TerrainKind.None"/>.
   36:     /// </summary>
   37:     void ApplyTerrainSpeedFeats(ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result);
   38: 
   39:     /// <summary>Applies the Rohan infantry-speed penalty when &gt;50% of the party is infantry.</summary>
   40:     void ApplyRohanInfantryPenalty(ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result);
   41: 
   42:     // ── SettlementProsperity ───────────────────────────────────────────
   43:     /// <summary>Applies Rivendell/Mirkwood/Gondor hearth-growth factors. Skipped when current change is negative.</summary>
   44:     void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   45: 
   46:     // ── SettlementMilitia ──────────────────────────────────────────────
   47:     /// <summary>Applies Mirkwood/Dol Guldur veteran-militia spawn-chance bonuses (additive).</summary>
   48:     void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   49: 
   50:     // ── BuildingConstruction ───────────────────────────────────────────
   51:     /// <summary>Applies Erebor/Lothlorien/Dol Guldur/Isengard construction-speed factors.</summary>
   52:     void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   53: 
   54:     // ── VillageProduction ──────────────────────────────────────────────
   55:     /// <summary>Applies Erebor production + Gundabad/Mordor grain-production factors.</summary>
   56:     void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result);
   57: 
   58:     // ── Caravan ────────────────────────────────────────────────────────
   59:     /// <summary>Applies the Umbar cheaper-caravans factor to a base cost. Rounds to int via MathF.Round-equivalent banker rounding semantics matching the original model.</summary>
   60:     int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost);
   61: 
   62:     // ── BattleReward ───────────────────────────────────────────────────
   63:     /// <summary>Applies the Umbar renown factor.</summary>
   64:     void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   65: 
   66:     // ── PartyTroopUpgrade ──────────────────────────────────────────────
   67:     /// <summary>Applies the Isengard/Rohan mounted-upgrade-cost factors. No-op when troop is not mounted.</summary>
   68:     void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result);
   69: 
   70:     // ── PartySize ──────────────────────────────────────────────────────
   71:     /// <summary>Applies Mordor/Gundabad/Dol Guldur/Isengard/Gondor party-size factors.</summary>
   72:     void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   73: 
   74:     // ── FoodConsumption ────────────────────────────────────────────────
   75:     /// <summary>Applies Rivendell/Mirkwood/Lothlorien/Dol Guldur food-consumption factors.</summary>
   76:     void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   77: 
   78:     // ── SettlementLoyalty ──────────────────────────────────────────────
   79:     /// <summary>Applies Gondor/Erebor/Lothlorien/Rivendell/Rohan loyalty bonuses (Add — not AddFactor).</summary>
   80:     void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   81: 
   82:     // ── PartyMorale ────────────────────────────────────────────────────
   83:     /// <summary>Applies Gondor/Rohan/Erebor/Mirkwood/Lothlorien morale bonuses (Add).</summary>
   84:     void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   85: 
   86:     // ── Smithing ───────────────────────────────────────────────────────
   87:     /// <summary>Applies Erebor/Isengard smithing-energy-cost factors.</summary>
   88:     void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   89: 
   90:     // ── ClanFinance (tariffs) ──────────────────────────────────────────
   91:     /// <summary>Applies the Umbar tariff-income factor.</summary>
   92:     void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   93: 
   94:     // ── Raid ───────────────────────────────────────────────────────────
   95:     /// <summary>Applies Mordor/Gundabad/Isengard raid-damage factors.</summary>
   96:     void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
   97: }

 succeeded in 777ms:
  343:     private void InitializeAll()
  344:     {
  345:         // Erebor — Dwarves: cheap garrisons, strong production, slow construction
  346:         _ereborGarrisonWage.Initialize(
  347:             "{=taom_feat_erebor_gw}Dwarven Garrison",
  348:             "{=taom_feat_erebor_gw_desc}Garrison wages reduced by 25%.",
  349:             -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  350:         _ereborProduction.Initialize(
  351:             "{=taom_feat_erebor_p}Dwarven Industry",
  352:             "{=taom_feat_erebor_p_desc}All village production increased by 10%.",
  353:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  354:         _ereborConstructionSpeed.Initialize(
  355:             "{=taom_feat_erebor_cs}Dwarven Perfectionism",
  356:             "{=taom_feat_erebor_cs_desc}Construction speed reduced by 15%.",
  357:             -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  358:         _ereborLoyalty.Initialize(
  359:             "{=taom_feat_erebor_loy}Dwarven Honor",
  360:             "{=taom_feat_erebor_loy_desc}Settlement loyalty increased by 1 per day.",
  361:             1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  362:         _ereborMorale.Initialize(
  363:             "{=taom_feat_erebor_mor}Dwarven Stubbornness",
  364:             "{=taom_feat_erebor_mor_desc}Party morale increased by 5.",
  365:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  366:         _ereborSmithing.Initialize(
  367:             "{=taom_feat_erebor_sm}Master Smiths",
  368:             "{=taom_feat_erebor_sm_desc}Smithing energy cost reduced by 30%.",
  369:             -0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  370:         _ereborSnowSpeed.Initialize(
  371:             "{=taom_feat_erebor_ss}Mountain Folk",
  372:             "{=taom_feat_erebor_ss_desc}Party movement speed increased by 10% in snow.",
  373:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  374: 
  375:         // Rivendell — High Elves: strong armies, good growth, expensive to rally
  376:         _rivendellArmyInfluence.Initialize(
  377:             "{=taom_feat_riv_ai}Elven Wisdom",
  378:             "{=taom_feat_riv_ai_desc}Army influence award increased by 35%.",
  379:             0.35f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  380:         _rivendellHearthGrowth.Initialize(
  381:             "{=taom_feat_riv_hg}The Last Homely House",
  382:             "{=taom_feat_riv_hg_desc}Village hearth growth increased by 20%.",
  383:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  384:         _rivendellArmyInfluenceCost.Initialize(
  385:             "{=taom_feat_riv_aic}Elven Pride",
  386:             "{=taom_feat_riv_aic_desc}Army recruitment costs 25% more influence.",
  387:             0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  388:         _rivendellFoodConsumption.Initialize(
  389:             "{=taom_feat_riv_fc}Elven Frugality",
  390:             "{=taom_feat_riv_fc_desc}Party food consumption reduced by 15%.",
  391:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  392:         _rivendellLoyalty.Initialize(
  393:             "{=taom_feat_riv_loy}Elven Wisdom",
  394:             "{=taom_feat_riv_loy_desc}Settlement loyalty increased by 0.5 per day.",
  395:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  396:         _rivendellForestSpeed.Initialize(
  397:             "{=taom_feat_riv_fs}Woodland Grace",
  398:             "{=taom_feat_riv_fs_desc}Party movement speed increased by 10% in forests.",
  399:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  400: 
  401:         // Mirkwood — Wood Elves: forest masters, good militia, isolated
  402:         _mirkwoodForestSpeed.Initialize(
  403:             "{=taom_feat_mrk_fs}Woodland Realm",
  404:             "{=taom_feat_mrk_fs_desc}Party movement speed increased by 10% in forests.",
  405:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  406:         _mirkwoodMilitiaProduction.Initialize(
  407:             "{=taom_feat_mrk_mp}Silvan Wardens",
  408:             "{=taom_feat_mrk_mp_desc}25% increased chance of veteran militia.",
  409:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  410:         _mirkwoodHearthGrowth.Initialize(
  411:             "{=taom_feat_mrk_hg}Forest Isolation",
  412:             "{=taom_feat_mrk_hg_desc}Village hearth growth reduced by 20%.",
  413:             -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  414:         _mirkwoodFoodConsumption.Initialize(
  415:             "{=taom_feat_mrk_fc}Woodland Sustenance",
  416:             "{=taom_feat_mrk_fc_desc}Party food consumption reduced by 15%.",
  417:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  418:         _mirkwoodMorale.Initialize(
  419:             "{=taom_feat_mrk_mor}Woodland Bonds",
  420:             "{=taom_feat_mrk_mor_desc}Party morale increased by 3.",
  421:             3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  422: 
  423:         // Lothlorien — Golden Wood: forest speed, cheap garrisons, slow building
  424:         _lothlorienForestSpeed.Initialize(
  425:             "{=taom_feat_loth_fs}Golden Wood",
  426:             "{=taom_feat_loth_fs_desc}Party movement speed increased by 10% in forests.",
  427:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  428:         _lothlorienGarrisonWage.Initialize(
  429:             "{=taom_feat_loth_gw}Wardens of Lorien",
  430:             "{=taom_feat_loth_gw_desc}Garrison wages reduced by 20%.",
  431:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  432:         _lothlorienConstructionSpeed.Initialize(
  433:             "{=taom_feat_loth_cs}Timeless Craft",
  434:             "{=taom_feat_loth_cs_desc}Construction speed reduced by 10%.",
  435:             -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  436:         _lothlorienFoodConsumption.Initialize(
  437:             "{=taom_feat_loth_fc}Lembas Bread",
  438:             "{=taom_feat_loth_fc_desc}Party food consumption reduced by 15%.",
  439:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  440:         _lothlorienLoyalty.Initialize(
  441:             "{=taom_feat_loth_loy}Elven Grace",
  442:             "{=taom_feat_loth_loy_desc}Settlement loyalty increased by 0.5 per day.",
  443:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  444:         _lothlorienMorale.Initialize(
  445:             "{=taom_feat_loth_mor}Elven Harmony",
  446:             "{=taom_feat_loth_mor_desc}Party morale increased by 3.",
  447:             3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  448: 
  449:         // Isengard — Saruman: cheap mounted recruits, cheap garrisons, decision penalty
  450:         _isengardCheaperRecruits.Initialize(
  451:             "{=taom_feat_isen_cr}War Machine",
  452:             "{=taom_feat_isen_cr_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
  453:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  454:         _isengardGarrisonWage.Initialize(
  455:             "{=taom_feat_isen_gw}Orthanc Garrison",
  456:             "{=taom_feat_isen_gw_desc}Garrison wages reduced by 20%.",
  457:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  458:         _isengardDecisionPenalty.Initialize(
  459:             "{=taom_feat_isen_dp}Saruman's Grip",
  460:             "{=taom_feat_isen_dp_desc}Kingdom decision relationship penalties increased by 25%.",
  461:             0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  462:         _isengardPartySize.Initialize(
  463:             "{=taom_feat_isen_ps}Uruk-hai Legions",
  464:             "{=taom_feat_isen_ps_desc}Party size limit increased by 20%.",
  465:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  466:         _isengardConstructionSpeed.Initialize(
  467:             "{=taom_feat_isen_cs}Industrial Might",
  468:             "{=taom_feat_isen_cs_desc}Construction speed increased by 15%.",
  469:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  470:         _isengardSmithing.Initialize(
  471:             "{=taom_feat_isen_sm}Industrial Forges",
  472:             "{=taom_feat_isen_sm_desc}Smithing energy cost reduced by 20%.",
  473:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  474:         _isengardRaidDamage.Initialize(
  475:             "{=taom_feat_isen_rd}War Machine Raids",
  476:             "{=taom_feat_isen_rd_desc}Raid damage increased by 20%.",
  477:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  478:         _isengardPlainSpeed.Initialize(
  479:             "{=taom_feat_isen_ps2}Forced March",
  480:             "{=taom_feat_isen_ps2_desc}Party movement speed increased by 10% on plains.",
  481:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  482:         _isengardSwampSpeed.Initialize(
  483:             "{=taom_feat_isen_sws}Fenland Drillmasters",
  484:             "{=taom_feat_isen_sws_desc}Party movement speed increased by 10% in swamps.",
  485:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  486: 
  487:         // Gundabad — Orcs: cheap armies, grain production, expensive wages
  488:         _gundabadArmyInfluenceCost.Initialize(
  489:             "{=taom_feat_gun_aic}Orc Horde",
  490:             "{=taom_feat_gun_aic_desc}Army recruitment costs 40% less influence.",
  491:             -0.4f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  492:         _gundabadGrainProduction.Initialize(
  493:             "{=taom_feat_gun_gp}Plundered Stores",
  494:             "{=taom_feat_gun_gp_desc}Grain production increased by 15%.",
  495:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  496:         _gundabadWage.Initialize(
  497:             "{=taom_feat_gun_w}Plunder Demands",
  498:             "{=taom_feat_gun_w_desc}Party wages increased by 10%.",
  499:             0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  500:         _gundabadPartySize.Initialize(
  501:             "{=taom_feat_gun_ps}Mountain Swarm",
  502:             "{=taom_feat_gun_ps_desc}Party size limit increased by 30%.",
  503:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  504:         _gundabadRaidDamage.Initialize(
  505:             "{=taom_feat_gun_rd}Orc Pillagers",
  506:             "{=taom_feat_gun_rd_desc}Raid damage increased by 25%.",
  507:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  508:         _gundabadSnowSpeed.Initialize(
  509:             "{=taom_feat_gun_ss}Mountain Marauders",
  510:             "{=taom_feat_gun_ss_desc}Party movement speed increased by 10% in snow.",
  511:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  512: 
  513:         // Umbar — Corsairs: cheap caravans, battle renown, expensive wages
  514:         _umbarCheaperCaravans.Initialize(
  515:             "{=taom_feat_umb_cc}Corsair Trade",
  516:             "{=taom_feat_umb_cc_desc}Caravan formation cost reduced by 25%.",
  517:             -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  518:         _umbarRenown.Initialize(
  519:             "{=taom_feat_umb_r}Corsair Glory",
  520:             "{=taom_feat_umb_r_desc}Renown from battles increased by 8%.",
  521:             0.08f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  522:         _umbarWage.Initialize(
  523:             "{=taom_feat_umb_w}Corsair Greed",
  524:             "{=taom_feat_umb_w_desc}Party wages increased by 8%.",
  525:             0.08f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  526:         _umbarTariffIncome.Initialize(
  527:             "{=taom_feat_umb_ti}Corsair Trade Networks",
  528:             "{=taom_feat_umb_ti_desc}Tariff income increased by 15%.",
  529:             0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  530:         _umbarDesertSpeed.Initialize(
  531:             "{=taom_feat_umb_ds}Desert Corsairs",
  532:             "{=taom_feat_umb_ds_desc}Party movement speed increased by 10% in deserts.",
  533:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  534: 
  535:         // Dol Guldur — Shadow: cheap armies, veteran militia, slow construction
  536:         _dolguldurArmyInfluenceCost.Initialize(
  537:             "{=taom_feat_dg_aic}Shadow Command",
  538:             "{=taom_feat_dg_aic_desc}Army recruitment costs 50% less influence.",
  539:             -0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  540:         _dolguldurMilitiaProduction.Initialize(
  541:             "{=taom_feat_dg_mp}Dark Conscription",
  542:             "{=taom_feat_dg_mp_desc}20% increased chance of veteran militia.",
  543:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  544:         _dolguldurConstructionSpeed.Initialize(
  545:             "{=taom_feat_dg_cs}Ruinous Works",
  546:             "{=taom_feat_dg_cs_desc}Construction speed reduced by 20%.",
  547:             -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  548:         _dolguldurPartySize.Initialize(
  549:             "{=taom_feat_dg_ps}Dark Legions",
  550:             "{=taom_feat_dg_ps_desc}Party size limit increased by 25%.",
  551:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  552:         _dolguldurFoodConsumption.Initialize(
  553:             "{=taom_feat_dg_fc}Voracious Hordes",
  554:             "{=taom_feat_dg_fc_desc}Party food consumption increased by 10%.",
  555:             0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  556: 
  557:         // Gondor — Men of the West: cheap garrisons, army influence, depleted hearths
  558:         _gondorGarrisonWage.Initialize(
  559:             "{=taom_feat_gon_gw}Tower Guard",
  560:             "{=taom_feat_gon_gw_desc}Garrison wages reduced by 20%.",
  561:             -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  562:         _gondorArmyInfluence.Initialize(
  563:             "{=taom_feat_gon_ai}Gondorian Discipline",
  564:             "{=taom_feat_gon_ai_desc}Army influence award increased by 30%.",
  565:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  566:         _gondorHearthGrowth.Initialize(
  567:             "{=taom_feat_gon_hg}War-Depleted Lands",
  568:             "{=taom_feat_gon_hg_desc}Village hearth growth reduced by 15%.",
  569:             -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  570:         _gondorPartySize.Initialize(
  571:             "{=taom_feat_gon_ps}Standing Armies",
  572:             "{=taom_feat_gon_ps_desc}Party size limit increased by 10%.",
  573:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  574:         _gondorLoyalty.Initialize(
  575:             "{=taom_feat_gon_loy}Tower Guard Discipline",
  576:             "{=taom_feat_gon_loy_desc}Settlement loyalty increased by 1 per day.",
  577:             1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  578:         _gondorMorale.Initialize(
  579:             "{=taom_feat_gon_mor}Gondorian Resolve",
  580:             "{=taom_feat_gon_mor_desc}Party morale increased by 5.",
  581:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  582:         _gondorPlainSpeed.Initialize(
  583:             "{=taom_feat_gon_ps2}Men of the Fields",
  584:             "{=taom_feat_gon_ps2_desc}Party movement speed increased by 10% on plains.",
  585:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  586: 
  587:         // Mordor — Dark Lord: very cheap armies, grain production, very expensive wages
  588:         _mordorArmyInfluenceCost.Initialize(
  589:             "{=taom_feat_mor_aic}The Dark Lord's Will",
  590:             "{=taom_feat_mor_aic_desc}Army recruitment costs 60% less influence.",
  591:             -0.6f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  592:         _mordorGrainProduction.Initialize(
  593:             "{=taom_feat_mor_gp}Nurn Farmlands",
  594:             "{=taom_feat_mor_gp_desc}Grain production increased by 20%.",
  595:             0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  596:         _mordorWage.Initialize(
  597:             "{=taom_feat_mor_w}Dark Tribute",
  598:             "{=taom_feat_mor_w_desc}Party wages increased by 20%.",
  599:             0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  600:         _mordorPartySize.Initialize(
  601:             "{=taom_feat_mor_ps}Sauron's Hordes",
  602:             "{=taom_feat_mor_ps_desc}Party size limit increased by 30%.",
  603:             0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  604:         _mordorRaidDamage.Initialize(
  605:             "{=taom_feat_mor_rd}Sauron's Wrath",
  606:             "{=taom_feat_mor_rd_desc}Raid damage increased by 25%.",
  607:             0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  608:         _mordorPlainSpeed.Initialize(
  609:             "{=taom_feat_mor_pls}Shadow March",
  610:             "{=taom_feat_mor_pls_desc}Party movement speed increased by 5% on plains.",
  611:             0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  612:         _mordorSwampSpeed.Initialize(
  613:             "{=taom_feat_mor_sws}Dead Marshes",
  614:             "{=taom_feat_mor_sws_desc}Party movement speed increased by 5% in swamps.",
  615:             0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  616:         _mordorNightSpeed.Initialize(
  617:             "{=taom_feat_mor_ns}Creatures of the Dark",
  618:             "{=taom_feat_mor_ns_desc}Party movement speed increased by 10% at night.",
  619:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  620: 
  621:         // Rohan — Horse-lords: cheap mounted troops, slow without cavalry
  622:         _rohanMountedCost.Initialize(
  623:             "{=taom_feat_roh_mc}Horse-lord Heritage",
  624:             "{=taom_feat_roh_mc_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
  625:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  626:         _rohanMountedWage.Initialize(
  627:             "{=taom_feat_roh_mw}Riders of the Mark",
  628:             "{=taom_feat_roh_mw_desc}Mounted troop wages reduced by 15%.",
  629:             -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  630:         _rohanInfantrySpeed.Initialize(
  631:             "{=taom_feat_roh_is}Cavalry Dependent",
  632:             "{=taom_feat_roh_is_desc}Party speed reduced by 10% when majority infantry.",
  633:             -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
  634:         _rohanLoyalty.Initialize(
  635:             "{=taom_feat_roh_loy}Horse-lord Fellowship",
  636:             "{=taom_feat_roh_loy_desc}Settlement loyalty increased by 0.5 per day.",
  637:             0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  638:         _rohanMorale.Initialize(
  639:             "{=taom_feat_roh_mor}Riders' Spirit",
  640:             "{=taom_feat_roh_mor_desc}Party morale increased by 5.",
  641:             5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
  642:         _rohanPlainSpeed.Initialize(
  643:             "{=taom_feat_roh_pls}Riders of the Plains",
  644:             "{=taom_feat_roh_pls_desc}Party movement speed increased by 10% on plains.",
  645:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  646: 
  647:         // Dale — Men of Dale: traders of the vale
  648:         _dalePlainSpeed.Initialize(
  649:             "{=taom_feat_dale_pls}Vale Traders",
  650:             "{=taom_feat_dale_pls_desc}Party movement speed increased by 10% on plains.",
  651:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  652: 
  653:         // Khand — Variags: steppe charioteers
  654:         _khandSteppeSpeed.Initialize(
  655:             "{=taom_feat_khand_sts}Steppe Charioteers",
  656:             "{=taom_feat_khand_sts_desc}Party movement speed increased by 10% on steppes.",
  657:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  658: 
  659:         // Rhun — Easterlings: mounted outriders
  660:         _rhunSteppeSpeed.Initialize(
  661:             "{=taom_feat_rhun_sts}Easterling Outriders",
  662:             "{=taom_feat_rhun_sts_desc}Party movement speed increased by 10% on steppes.",
  663:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  664: 
  665:         // Harad — Haradrim: sons of the sun
  666:         _haradDesertSpeed.Initialize(
  667:             "{=taom_feat_har_ds}Sons of the Sun",
  668:             "{=taom_feat_har_ds_desc}Party movement speed increased by 10% in deserts.",
  669:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  670: 
  671:         // Dunland — Hill-men: hill marchers
  672:         _dunlandPlainSpeed.Initialize(
  673:             "{=taom_feat_dun_pls}Hill Marchers",
  674:             "{=taom_feat_dun_pls_desc}Party movement speed increased by 10% on plains.",
  675:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  676: 
  677:         // Shaghana — southern Haradrim: dune wanderers
  678:         _shaghanaDesertSpeed.Initialize(
  679:             "{=taom_feat_shg_ds}Dune Wanderers",
  680:             "{=taom_feat_shg_ds_desc}Party movement speed increased by 10% in deserts.",
  681:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  682: 
  683:         // Abanissa — deep-south Haradrim: deep desert nomads
  684:         _abanissaDesertSpeed.Initialize(
  685:             "{=taom_feat_aba_ds}Deep Desert Nomads",
  686:             "{=taom_feat_aba_ds_desc}Party movement speed increased by 10% in deserts.",
  687:             0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
  688:     }

 succeeded in 1017ms:
    6:   <Culture
  220:     <cultural_feats>
  221:       <feat id="taom_erebor_garrison_wage" />
  222:       <feat id="taom_erebor_production" />
  223:       <feat id="taom_erebor_construction_speed" />
  224:       <feat id="taom_erebor_loyalty" />
  225:       <feat id="taom_erebor_morale" />
  226:       <feat id="taom_erebor_smithing" />
  227:       <feat id="taom_erebor_snow_speed" />
  228:     </cultural_feats>
  365:   </Culture>
  370:   <Culture
  593:     <cultural_feats>
  594:       <feat id="taom_rivendell_army_influence" />
  595:       <feat id="taom_rivendell_hearth_growth" />
  596:       <feat id="taom_rivendell_army_influence_cost" />
  597:       <feat id="taom_rivendell_food_consumption" />
  598:       <feat id="taom_rivendell_loyalty" />
  599:       <feat id="taom_rivendell_forest_speed" />
  600:     </cultural_feats>
  735:   </Culture>
  740:   <Culture
  954:     <cultural_feats>
  955:       <feat id="taom_mirkwood_forest_speed" />
  956:       <feat id="taom_mirkwood_militia_production" />
  957:       <feat id="taom_mirkwood_hearth_growth" />
  958:       <feat id="taom_mirkwood_food_consumption" />
  959:       <feat id="taom_mirkwood_morale" />
  960:     </cultural_feats>
 1095:   </Culture>
 1100:   <Culture
 1314:     <cultural_feats>
 1315:       <feat id="taom_lothlorien_forest_speed" />
 1316:       <feat id="taom_lothlorien_garrison_wage" />
 1317:       <feat id="taom_lothlorien_construction_speed" />
 1318:       <feat id="taom_lothlorien_food_consumption" />
 1319:       <feat id="taom_lothlorien_loyalty" />
 1320:       <feat id="taom_lothlorien_morale" />
 1321:     </cultural_feats>
 1456:   </Culture>
 1461:   <Culture
 1674:     <cultural_feats>
 1675:       <feat id="taom_isengard_cheaper_recruits" />
 1676:       <feat id="taom_isengard_garrison_wage" />
 1677:       <feat id="taom_isengard_decision_penalty" />
 1678:       <feat id="taom_isengard_party_size" />
 1679:       <feat id="taom_isengard_construction_speed" />
 1680:       <feat id="taom_isengard_smithing" />
 1681:       <feat id="taom_isengard_raid_damage" />
 1682:       <feat id="taom_isengard_plain_speed" />
 1683:       <feat id="taom_isengard_swamp_speed" />
 1684:     </cultural_feats>
 1819:   </Culture>
 1824:   <Culture
 2038:     <cultural_feats>
 2039:       <feat id="taom_gundabad_army_influence_cost" />
 2040:       <feat id="taom_gundabad_grain_production" />
 2041:       <feat id="taom_gundabad_wage" />
 2042:       <feat id="taom_gundabad_party_size" />
 2043:       <feat id="taom_gundabad_raid_damage" />
 2044:       <feat id="taom_gundabad_snow_speed" />
 2045:     </cultural_feats>
 2180:   </Culture>
 2185:   <Culture
 2399:     <cultural_feats>
 2400:       <feat id="taom_umbar_cheaper_caravans" />
 2401:       <feat id="taom_umbar_renown" />
 2402:       <feat id="taom_umbar_wage" />
 2403:       <feat id="taom_umbar_tariff_income" />
 2404:       <feat id="taom_umbar_desert_speed" />
 2405:     </cultural_feats>
 2540:   </Culture>
 2545:   <Culture
 2759:     <cultural_feats>
 2760:       <feat id="taom_dolguldur_army_influence_cost" />
 2761:       <feat id="taom_dolguldur_militia_production" />
 2762:       <feat id="taom_dolguldur_construction_speed" />
 2763:       <feat id="taom_dolguldur_party_size" />
 2764:       <feat id="taom_dolguldur_food_consumption" />
 2765:     </cultural_feats>
 2900:   </Culture>
 2905:   <Culture
 3124:     <cultural_feats>
 3125:       <feat id="taom_gondor_garrison_wage" />
 3126:       <feat id="taom_gondor_army_influence" />
 3127:       <feat id="taom_gondor_hearth_growth" />
 3128:       <feat id="taom_gondor_party_size" />
 3129:       <feat id="taom_gondor_loyalty" />
 3130:       <feat id="taom_gondor_morale" />
 3131:       <feat id="taom_gondor_plain_speed" />
 3132:     </cultural_feats>
 3270:   </Culture>
 3275:   <Culture
 3494:     <cultural_feats>
 3495:       <feat id="taom_mordor_army_influence_cost" />
 3496:       <feat id="taom_mordor_grain_production" />
 3497:       <feat id="taom_mordor_wage" />
 3498:       <feat id="taom_mordor_party_size" />
 3499:       <feat id="taom_mordor_raid_damage" />
 3500:       <feat id="taom_mordor_plain_speed" />
 3501:       <feat id="taom_mordor_swamp_speed" />
 3502:       <feat id="taom_mordor_night_speed" />
 3503:     </cultural_feats>
 3643:   </Culture>
 3648:   <Culture
 3818:     <cultural_feats>
 3819:       <feat id="taom_shaghana_desert_speed" />
 3820:     </cultural_feats>
 3908:   </Culture>
 3913:   <Culture
 4082:     <cultural_feats>
 4083:       <feat id="taom_abanissa_desert_speed" />
 4084:     </cultural_feats>
 4170:   </Culture>
 4181:   <Culture
 4213:   </Culture>
 4216:   <Culture
 4248:   </Culture>
 4251:   <Culture
 4283:   </Culture>
 4286:   <Culture
 4318:   </Culture>
 4321:   <Culture
 4353:   </Culture>

 succeeded in 1263ms:
    6: 	<xsl:template match="@*|node()">
    7: 		<xsl:copy>
    8: 			<xsl:apply-templates select="@*|node()"/>
    9: 		</xsl:copy>
   13: 	<!-- Rename empire to Dunlending -->
   14: 	<xsl:template match="Culture[@id='empire']">
   15: 		<xsl:copy>
   16: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
   17: 			<xsl:apply-templates select="@*"/>
   20: 			<xsl:attribute name="name">{=aom_empire_name}Dunlendings</xsl:attribute>
   21: 			<xsl:attribute name="text">{=aom_empire_desc}The Dunlendings are a rugged and fierce people, dwelling in the wild hills and untamed lands west of Rohan. Long resentful of the Rohirrim, whom they see as invaders of their ancestral homes, the Dunlendings are united by their shared heritage and fierce independence. Skilled in guerrilla warfare and survival, they rely on cunning and ferocity to overcome their enemies. Though often dismissed as simple hill folk, the Dunlendings are a proud and enduring people, fiercely protective of their lands and ways of life. Driven by a deep-seated grudge against Rohan, they have allied themselves with the forces of darkness, seeking revenge and the restoration of their lost lands.</xsl:attribute>
   34: 			<xsl:attribute name="villager_party_template">PartyTemplate.villager_empire_template</xsl:attribute>
   36: 			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_empire</xsl:attribute>
   37: 			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_empire</xsl:attribute>
   39: 			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_empire_template</xsl:attribute>
   43: 			<xsl:attribute name="encounter_background_mesh">encounter_empire</xsl:attribute>
  107: 				<item id="Item.empire_sword_1_t2" />
  108: 				<item id="Item.empire_sword_2_t3" />
  109: 				<item id="Item.empire_sword_6_t5" />
  248: 			<!-- Notable templates (replaces vanilla empire notables with Dunland-specific) -->
  288: 			<!-- Override cultural feats: Dunland uses Battanian-style feats (hill tribe / forest guerrillas) -->
  289: 			<cultural_feats>
  290: 				<feat id="battanian_forest_speed" />
  291: 				<feat id="battanian_militia_production" />
  292: 				<feat id="battanian_slower_construction" />
  293: 				<feat id="taom_dunland_plain_speed" />
  294: 			</cultural_feats>
  297: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates or self::cultural_feats)]"/>
  298: 		</xsl:copy>
  301: 	<!-- Rename aserai to Haradrim -->
  302: 	<xsl:template match="Culture[@id='aserai']">
  303: 		<xsl:copy>
  304: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
  305: 			<xsl:apply-templates select="@*"/>
  308: 			<xsl:attribute name="name">{=aom_aserai_name}Haradrim</xsl:attribute>
  309: 			<xsl:attribute name="text">{=aom_aserai_desc}The Haradrim are a proud and fierce people hailing from the deserts and savannahs of the distant south. Renowned for their mastery of exotic beasts such as mûmakil, their warriors combine skill, resilience, and deadly precision in battle. Harad is a land of extremes, with its scorching sun, golden sands, and vibrant oases. Its people are united by ancient traditions, loyalty to their tribes, and the call of their chieftains and kings. Often allied with the forces of darkness, the Haradrim are driven by vengeance against Gondor and the promise of power and dominion offered by Sauron.</xsl:attribute>
  322: 			<xsl:attribute name="villager_party_template">PartyTemplate.villager_aserai_template</xsl:attribute>
  324: 			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_aserai</xsl:attribute>
  325: 			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_aserai</xsl:attribute>
  326: 			<xsl:attribute name="militia_party_template">PartyTemplate.militia_aserai_template</xsl:attribute>
  327: 			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_aserai_template</xsl:attribute>
  328: 			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_aserai</xsl:attribute>
  331: 			<xsl:attribute name="encounter_background_mesh">encounter_aserai</xsl:attribute>
  359: 			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_aserai</xsl:attribute>
  360: 			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_aserai</xsl:attribute>
  361: 			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_aserai</xsl:attribute>
  362: 			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_aserai</xsl:attribute>
  393: 				<item id="Item.aserai_sword_1_t2" />
  394: 				<item id="Item.aserai_sword_3_t3" />
  395: 				<item id="Item.aserai_sword_4_t4" />
  396: 				<item id="Item.aserai_sword_5_t4" />
  536: 			<!-- Notable templates (replaces vanilla aserai notables with Harad-specific) -->
  577: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
  578: 		</xsl:copy>
  581: 	<!-- Rename vlandia to Rohirrim -->
  582: 	<xsl:template match="Culture[@id='vlandia']">
  583: 		<xsl:copy>
  584: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
  585: 			<xsl:apply-templates select="@*"/>
  588: 			<xsl:attribute name="name">{=aom_vlandia_name}Rohirrim</xsl:attribute>
  589: 			<xsl:attribute name="text">{=aom_vlandia_desc}The Rohirrim, or Horse-lords of Rohan, are renowned for their unmatched cavalry and deep bond with their steeds. Proud and free-spirited, they dwell in the rolling plains of the Riddermark, living in villages and strongholds like Edoras. Guided by their King and loyal to their people, the Rohirrim prize honor, loyalty, and courage. Their warriors ride fearlessly into battle, forming the backbone of Middle-earth’s resistance against the shadow. Though steadfast in their traditions, the Rohirrim are ever ready to defend their land and allies, wielding spears, swords, and shields with precision.</xsl:attribute>
  602: 			<xsl:attribute name="villager_party_template">PartyTemplate.villager_vlandia_template</xsl:attribute>
  606: 			<xsl:attribute name="militia_party_template">PartyTemplate.militia_vlandia_template</xsl:attribute>
  607: 			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_vlandia_template</xsl:attribute>
  608: 			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_vlandia</xsl:attribute>
  611: 			<xsl:attribute name="encounter_background_mesh">encounter_vlandia</xsl:attribute>
  673: 				<item id="Item.vlandia_sword_1_t2" />
  674: 				<item id="Item.vlandia_sword_2_t3" />
  675: 				<item id="Item.vlandia_sword_3_t4" />
  676: 				<item id="Item.vlandia_sword_5_t5" />
  816: 			<!-- Notable templates (replaces vanilla vlandia notables with Rohan-specific) -->
  856: 			<cultural_feats>
  859: 				<feat id="taom_rohan_infantry_speed" />
  862: 				<feat id="taom_rohan_plain_speed" />
  863: 			</cultural_feats>
  866: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates or self::cultural_feats)]"/>
  867: 		</xsl:copy>
  870: 	<!-- Rename khuzait to Easterling -->
  871: 	<xsl:template match="Culture[@id='khuzait']">
  872: 		<xsl:copy>
  873: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
  874: 			<xsl:apply-templates select="@*"/>
  877: 			<xsl:attribute name="name">{=aom_khuzait_name}Easterlings</xsl:attribute>
  878: 			<xsl:attribute name="text">{=aom_khuzait_desc}The Easterlings are a diverse and fearsome people from the distant lands of Rhûn. Known for their loyalty to Sauron, they march to war with precision and discipline, wielding curved blades, spears, and massive war-drums that echo across the battlefield. Their golden armor and crimson banners reflect their fierce pride and martial prowess. Divided into clans and tribes, the Easterlings unite under powerful chieftains to bring the might of Rhûn against the Free Peoples of Middle-earth.</xsl:attribute>
  891: 			<xsl:attribute name="villager_party_template">PartyTemplate.villager_khuzait_template</xsl:attribute>
  895: 			<xsl:attribute name="militia_party_template">PartyTemplate.militia_khuzait_template</xsl:attribute>
  896: 			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_khuzait_template</xsl:attribute>
  897: 			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_khuzait</xsl:attribute>
  900: 			<xsl:attribute name="encounter_background_mesh">encounter_khuzait</xsl:attribute>
  928: 			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_khuzait</xsl:attribute>
  929: 			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_khuzait</xsl:attribute>
  930: 			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_khuzait</xsl:attribute>
  931: 			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_khuzait</xsl:attribute>
  962: 				<item id="Item.khuzait_sword_1_t2" />
  963: 				<item id="Item.khuzait_sword_2_t3" />
  964: 				<item id="Item.khuzait_sword_3_t3" />
  965: 				<item id="Item.khuzait_sword_4_t4" />
 1095: 			<!-- Notable templates (replaces vanilla khuzait notables with Rhun-specific) -->
 1136: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
 1137: 		</xsl:copy>
 1140: 	<!-- Rename sturgia to Barding -->
 1141: 	<xsl:template match="Culture[@id='sturgia']">
 1142: 		<xsl:copy>
 1143: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
 1144: 			<xsl:apply-templates select="@*"/>
 1147: 			<xsl:attribute name="name">{=TAOM_sturgia_culture}Barding</xsl:attribute>
 1148: 			<xsl:attribute name="text">{=TAOM_sturgia_desc}The Bardings of Dale, named for Bard the Bowman, are a proud and industrious people who rose to prominence after reclaiming their homeland from the shadow of Smaug. Nestled between the Lonely Mountain and the Long Lake, Dale thrives as a hub of trade and culture. Known for their resilience and craftsmanship, the Bardings excel in forging weapons and armor, rivaling even the Dwarves of Erebor. Their armies, composed of disciplined archers, stalwart swordsmen, and agile skirmishers, defend their lands with fierce determination. United under noble leaders, the Bardings are ever watchful, guarding against the encroaching darkness and preserving their rich heritage.</xsl:attribute>
 1215: 			<!-- Notable templates (replaces vanilla sturgia notables with Dale-specific) -->
 1246: 			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
 1247: 		</xsl:copy>
 1250: 	<!-- Rename battania to Variag -->
 1251: 	<xsl:template match="Culture[@id='battania']">
 1252: 		<xsl:copy>
 1253: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
 1254: 			<xsl:apply-templates select="@*"/>
 1257: 			<xsl:attribute name="name">{=TAOM_battania_culture}Variag</xsl:attribute>
 1258: 			<xsl:attribute name="text">{=TAOM_battania_desc}The Variags of Khand are a fierce and warlike people, hailing from the dry and rugged lands east of Mordor. Known for their mercenary prowess and loyalty to Sauron, the Variags fight with unmatched ferocity. They ride swift warhorses into battle, wielding curved blades and long spears with deadly precision. Their bronze and crimson armor, adorned with intricate designs, reflects their proud and martial heritage. Divided into tribes and clans, the Variags unite under powerful warlords, bringing fear and chaos to the enemies of the Dark Lord.</xsl:attribute>
 1308: 			<!-- Notable templates (replaces vanilla battania notables with Khand-specific) -->
 1339: 			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
 1340: 		</xsl:copy>
 1343: 	<!-- TAOM terrain movement-speed feats: append to each culture's vanilla cultural_feats,
 1345: 	<xsl:template match="Culture[@id='aserai']/cultural_feats">
 1346: 		<xsl:copy>
 1347: 			<xsl:apply-templates select="@*|node()"/>
 1348: 			<feat id="taom_harad_desert_speed" />
 1349: 		</xsl:copy>
 1352: 	<xsl:template match="Culture[@id='khuzait']/cultural_feats">
 1353: 		<xsl:copy>
 1354: 			<xsl:apply-templates select="@*|node()"/>
 1355: 			<feat id="taom_rhun_steppe_speed" />
 1356: 		</xsl:copy>
 1359: 	<xsl:template match="Culture[@id='sturgia']/cultural_feats">
 1360: 		<xsl:copy>
 1361: 			<xsl:apply-templates select="@*|node()"/>
 1362: 			<feat id="taom_dale_plain_speed" />
 1363: 		</xsl:copy>
 1366: 	<xsl:template match="Culture[@id='battania']/cultural_feats">
 1367: 		<xsl:copy>
 1368: 			<xsl:apply-templates select="@*|node()"/>
 1369: 			<feat id="taom_khand_steppe_speed" />
 1370: 		</xsl:copy>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs' | ForEach-Object { "'$i++; if ($_ -match '"'Terrain|Snow|Forest|Steppe|Desert|Plain|Swamp|Night|Mordor|Aserai|Harad|Khand|Dale|Rhun|Dunland|Rivendell|Mirkwood|Lothlorien|Erebor|Gundabad|Umbar|Isengard|Rohan|AddFactor|ApplyTerrain') { '{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/_Module/ModuleData/spcultures.xslt' | ForEach-Object { "'$i++; if (($i -ge 1 -and $i -le 20) -or ($i -ge 280 -and $i -le 300) -or ($i -ge 850 -and $i -le 870) -or ($i -ge 570 -and $i -le 582) -or ($i -ge 1130 -and $i -le 1140) -or ($i -ge 1240 -and $i -le 1252) -or ($i -ge 1334 -and $i -le 1373)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs' | ForEach-Object { "'$i++; if ($_ -match '"'Terrain|Speed|Snow|Forest|Steppe|Desert|Plain|Swamp|Night|GetAllFeats|77|taom_.*_speed|Mordor') { '{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'docs/features/cultural-feats.md' | ForEach-Object { "'$i++; if ($_ -match '"'terrain|Terrain|speed|Speed|Forest|Snow|Steppe|Desert|Plain|Swamp|Night|Mordor|Rohan|Dunland|Harad|Khand|Rhun|Dale|77|59') { '{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 511ms:
    1: <?xml version="1.0" encoding="utf-8"?>
    2: <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    3: 	<!-- Identity transformation - copies everything by default -->
    4: 	<xsl:output omit-xml-declaration="no" indent="yes"/>
    5: 
    6: 	<xsl:template match="@*|node()">
    7: 		<xsl:copy>
    8: 			<xsl:apply-templates select="@*|node()"/>
    9: 		</xsl:copy>
   10: 	</xsl:template>
   11: 
   12: 
   13: 	<!-- Rename empire to Dunlending -->
   14: 	<xsl:template match="Culture[@id='empire']">
   15: 		<xsl:copy>
   16: 			<!-- Copy all vanilla attributes first, then override the ones we change -->
   17: 			<xsl:apply-templates select="@*"/>
   18: 
   19: 			<!-- Name and description -->
   20: 			<xsl:attribute name="name">{=aom_empire_name}Dunlendings</xsl:attribute>
  280: 				<template name="NPCCharacter.spc_notable_dunland_13" />
  281: 				<template name="NPCCharacter.spc_notable_dunland_21" />
  282: 				<template name="NPCCharacter.spc_notable_dunland_22" />
  283: 				<template name="NPCCharacter.spc_dunland_headman_1" />
  284: 				<template name="NPCCharacter.spc_dunland_headman_2" />
  285: 				<template name="NPCCharacter.spc_dunland_headman_3" />
  286: 			</notable_templates>
  287: 
  288: 			<!-- Override cultural feats: Dunland uses Battanian-style feats (hill tribe / forest guerrillas) -->
  289: 			<cultural_feats>
  290: 				<feat id="battanian_forest_speed" />
  291: 				<feat id="battanian_militia_production" />
  292: 				<feat id="battanian_slower_construction" />
  293: 				<feat id="taom_dunland_plain_speed" />
  294: 			</cultural_feats>
  295: 
  296: 			<!-- Pass through vanilla child elements we don't override -->
  297: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates or self::cultural_feats)]"/>
  298: 		</xsl:copy>
  299: 	</xsl:template>
  300: 
  570: 				<template name="NPCCharacter.spc_notable_harad_22" />
  571: 				<template name="NPCCharacter.spc_harad_headman_1" />
  572: 				<template name="NPCCharacter.spc_harad_headman_2" />
  573: 				<template name="NPCCharacter.spc_harad_headman_3" />
  574: 			</notable_templates>
  575: 
  576: 			<!-- Pass through vanilla child elements we don't override -->
  577: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
  578: 		</xsl:copy>
  579: 	</xsl:template>
  580: 
  581: 	<!-- Rename vlandia to Rohirrim -->
  582: 	<xsl:template match="Culture[@id='vlandia']">
  850: 				<template name="NPCCharacter.spc_rohan_headman_1" />
  851: 				<template name="NPCCharacter.spc_rohan_headman_2" />
  852: 				<template name="NPCCharacter.spc_rohan_headman_3" />
  853: 			</notable_templates>
  854: 
  855: 			<!-- Override cultural feats: Rohan uses custom Horse-lord feats -->
  856: 			<cultural_feats>
  857: 				<feat id="taom_rohan_mounted_cost" />
  858: 				<feat id="taom_rohan_mounted_wage" />
  859: 				<feat id="taom_rohan_infantry_speed" />
  860: 				<feat id="taom_rohan_loyalty" />
  861: 				<feat id="taom_rohan_morale" />
  862: 				<feat id="taom_rohan_plain_speed" />
  863: 			</cultural_feats>
  864: 
  865: 			<!-- Pass through vanilla child elements we don't override -->
  866: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates or self::cultural_feats)]"/>
  867: 		</xsl:copy>
  868: 	</xsl:template>
  869: 
  870: 	<!-- Rename khuzait to Easterling -->
 1130: 				<template name="NPCCharacter.spc_rhun_headman_1" />
 1131: 				<template name="NPCCharacter.spc_rhun_headman_2" />
 1132: 				<template name="NPCCharacter.spc_rhun_headman_3" />
 1133: 			</notable_templates>
 1134: 
 1135: 			<!-- Pass through vanilla child elements we don't override -->
 1136: 			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
 1137: 		</xsl:copy>
 1138: 	</xsl:template>
 1139: 
 1140: 	<!-- Rename sturgia to Barding -->
 1240: 				<template name="NPCCharacter.spc_dale_headman_1" />
 1241: 				<template name="NPCCharacter.spc_dale_headman_2" />
 1242: 				<template name="NPCCharacter.spc_dale_headman_3" />
 1243: 			</notable_templates>
 1244: 
 1245: 			<!-- Pass through vanilla child elements we don't override -->
 1246: 			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
 1247: 		</xsl:copy>
 1248: 	</xsl:template>
 1249: 
 1250: 	<!-- Rename battania to Variag -->
 1251: 	<xsl:template match="Culture[@id='battania']">
 1252: 		<xsl:copy>
 1334: 				<template name="NPCCharacter.spc_khand_headman_2" />
 1335: 				<template name="NPCCharacter.spc_khand_headman_3" />
 1336: 			</notable_templates>
 1337: 
 1338: 			<!-- Pass through vanilla child elements we don't override -->
 1339: 			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
 1340: 		</xsl:copy>
 1341: 	</xsl:template>
 1342: 
 1343: 	<!-- TAOM terrain movement-speed feats: append to each culture's vanilla cultural_feats,
 1344: 	     preserving the existing vanilla feats. Harad/Easterling/Dale/Khand reuse vanilla IDs. -->
 1345: 	<xsl:template match="Culture[@id='aserai']/cultural_feats">
 1346: 		<xsl:copy>
 1347: 			<xsl:apply-templates select="@*|node()"/>
 1348: 			<feat id="taom_harad_desert_speed" />
 1349: 		</xsl:copy>
 1350: 	</xsl:template>
 1351: 
 1352: 	<xsl:template match="Culture[@id='khuzait']/cultural_feats">
 1353: 		<xsl:copy>
 1354: 			<xsl:apply-templates select="@*|node()"/>
 1355: 			<feat id="taom_rhun_steppe_speed" />
 1356: 		</xsl:copy>
 1357: 	</xsl:template>
 1358: 
 1359: 	<xsl:template match="Culture[@id='sturgia']/cultural_feats">
 1360: 		<xsl:copy>
 1361: 			<xsl:apply-templates select="@*|node()"/>
 1362: 			<feat id="taom_dale_plain_speed" />
 1363: 		</xsl:copy>
 1364: 	</xsl:template>
 1365: 
 1366: 	<xsl:template match="Culture[@id='battania']/cultural_feats">
 1367: 		<xsl:copy>
 1368: 			<xsl:apply-templates select="@*|node()"/>
 1369: 			<feat id="taom_khand_steppe_speed" />
 1370: 		</xsl:copy>
 1371: 	</xsl:template>
 1372: 
 1373: </xsl:stylesheet>

 succeeded in 509ms:
    5: Each of TAOM's 10 custom cultures now has 3 unique cultural feats (2 bonuses + 1 penalty) that provide gameplay differentiation. This replaces the placeholder Empire feats that all cultures previously shared. Additionally, Dunland (XSLT culture) was reassigned from Empire feats to Battanian feats for better lore fit.
    7: On top of the base feats, **terrain movement-speed feats** give 18 cultures a flat party-speed bonus on their "home" terrain (forest / snow / steppe / desert / plain / swamp) plus a night bonus for Mordor — see the [Terrain Movement-Speed Feats](#terrain-movement-speed-feats) section. The feat total is now **77** (59 base + 18 terrain).
   12: - **TAOM requirement:** All 10 custom cultures used identical Empire feats — no gameplay differentiation between Erebor Dwarves and Mordor Orcs
   49:   <feat id="taom_erebor_construction_speed" />
   53: ### Base Culture Feats (59)
   55: > The two forest feats below (`taom_mirkwood_forest_speed`, `taom_lothlorien_forest_speed`) were reworked into the terrain system — they now apply a **flat +10%** in forest instead of scaling the vanilla forest penalty. See [Terrain Movement-Speed Feats](#terrain-movement-speed-feats).
   62: | Erebor | `taom_erebor_construction_speed` | -15% construction speed | -0.15 | No |
   71: | Mirkwood | `taom_mirkwood_forest_speed` | +10% movement speed in forest (reworked from -60% penalty reduction) | 0.1 | Yes |
   76: | Lothlorien | `taom_lothlorien_forest_speed` | +10% movement speed in forest (reworked from -50% penalty reduction) | 0.1 | Yes |
   78: | Lothlorien | `taom_lothlorien_construction_speed` | -10% construction speed | -0.1 | No |
   86: | Isengard | `taom_isengard_construction_speed` | +15% construction speed | 0.15 | Yes |
  100: | Dol Guldur | `taom_dolguldur_construction_speed` | -20% construction speed | -0.2 | No |
  109: | Mordor | `taom_mordor_army_influence_cost` | -60% army influence cost | -0.6 | Yes |
  110: | Mordor | `taom_mordor_grain_production` | +20% grain production | 0.2 | Yes |
  111: | Mordor | `taom_mordor_wage` | +20% party wages | 0.2 | No |
  112: | Mordor | `taom_mordor_party_size` | +30% party size | 0.3 | Yes |
  113: | Mordor | `taom_mordor_raid_damage` | +25% raid damage | 0.25 | Yes |
  114: | Rohan | `taom_rohan_mounted_cost` | -15% mounted recruit/upgrade cost | -0.15 | Yes |
  115: | Rohan | `taom_rohan_mounted_wage` | -15% mounted troop wages | -0.15 | Yes |
  116: | Rohan | `taom_rohan_infantry_speed` | -10% speed when >50% infantry | -0.1 | No |
  117: | Rohan | `taom_rohan_loyalty` | +0.5 settlement loyalty/day | 0.5 | Yes |
  118: | Rohan | `taom_rohan_morale` | +5 party morale | 5.0 | Yes |
  120: ### Terrain Movement-Speed Feats
  122: 18 cultures gain a flat party movement-speed `AddFactor` bonus while on their "home" terrain. The bonus **stacks on top of** vanilla's terrain modifiers (e.g. vanilla forest is -30%, desert -10%, night -25%). Terrain is read each speed recalc in `TaomPartySpeedModel`, mapped from the sealed `TerrainType` to the TAOM-owned `TerrainKind` enum at the boundary (`TerrainType.Dune` folds into `Desert`); the Mordor night bonus keys off `Campaign.Current.IsNight` and is terrain-independent.
  124: | Terrain | Cultures (culture StringId) | Feat IDs | Bonus |
  126: | Forest | Mirkwood, Lothlorien, Rivendell | `taom_mirkwood_forest_speed`, `taom_lothlorien_forest_speed`, `taom_rivendell_forest_speed` | +10% |
  127: | Snow | Erebor (Dwarves), Gundabad | `taom_erebor_snow_speed`, `taom_gundabad_snow_speed` | +10% |
  128: | Steppe | Khand (battania), Rhûn (khuzait) | `taom_khand_steppe_speed`, `taom_rhun_steppe_speed` | +10% |
  129: | Desert | Umbar, Harad (aserai), Shaghâna, Âbanissa | `taom_umbar_desert_speed`, `taom_harad_desert_speed`, `taom_shaghana_desert_speed`, `taom_abanissa_desert_speed` | +10% |
  130: | Plain | Gondor, Rohan (vlandia), Dale (sturgia), Dunland (empire), Isengard | `taom_gondor_plain_speed`, `taom_rohan_plain_speed`, `taom_dale_plain_speed`, `taom_dunland_plain_speed`, `taom_isengard_plain_speed` | +10% |
  131: | Plain | Mordor | `taom_mordor_plain_speed` | **+5%** |
  132: | Swamp | Isengard | `taom_isengard_swamp_speed` | +10% |
  133: | Swamp | Mordor | `taom_mordor_swamp_speed` | **+5%** |
  134: | Night (any terrain) | Mordor | `taom_mordor_night_speed` | +10% |
  136: **Mordor** deliberately gets a smaller terrain buff (+5%) offset by its unique night bonus (+10%). The elven forest feats were unified to a flat +10% (previously Mirkwood ~+18% / Lothlorien ~+15% net via penalty reduction).
  138: **Note on `TerrainType.Snow`:** the snow bonus keys off the *terrain* type, not snowy *weather* (vanilla's snow slowdown is weather-based). If a northern region isn't authored as `Snow`-typed faces on the campaign map, the dwarf/Gundabad bonus will rarely trigger — verify in-game.
  142: The six vanilla-wrapped cultures get their terrain feat appended to their `<cultural_feats>` in `spcultures.xslt`. Dunland/Rohan already had a TAOM override block (feat added inline); Harad/Rhûn/Dale/Khand pass vanilla feats through, so a dedicated `Culture[@id='X']/cultural_feats` template copies the vanilla feats and appends the TAOM feat (preserving vanilla bonuses).
  144: | Culture | Feats Used | Terrain feat appended |
  146: | Dunland (empire) | Battanian feats (inline override) | `taom_dunland_plain_speed` |
  147: | Rohan (vlandia) | Custom C# feats (inline override) | `taom_rohan_plain_speed` |
  148: | Harad (aserai) | Aserai feats (unchanged) | `taom_harad_desert_speed` |
  149: | Rhûn (khuzait) | Khuzait feats (unchanged) | `taom_rhun_steppe_speed` |
  150: | Dale (sturgia) | Sturgian feats (unchanged) | `taom_dale_plain_speed` |
  151: | Khand (battania) | Battanian feats (unchanged) | `taom_khand_steppe_speed` |
  157: | `Main/Features/CulturalFeats/TaomCulturalFeats.cs` | Feat registration + static accessors (77 feats) |
  158: | `Main/Features/CulturalFeats/TerrainKind.cs` | TAOM-owned terrain enum (boundary type for the speed model → service, ADR-007) |
  159: | `Main/Features/CulturalFeats/ICulturalFeatsService.cs` / `CulturalFeatsService.cs` | Per-feat dispatch incl. `ApplyTerrainSpeedFeats` |
  162: | `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs` | Terrain movement-speed feats + night + Rohan infantry speed |
  167: | `Main/Features/CulturalFeats/Models/TaomBuildingConstructionModel.cs` | Construction speed feats |
  180: | `Main/_Module/ModuleData/spcultures.xslt` | Vanilla-wrapped culture feat overrides + terrain-feat append templates |
  191: | `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` | Feat property count (77), uniqueness, culture distribution, field structure |
  192: | `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` | Per-feat dispatch incl. terrain-speed (per-terrain match, Mordor 5% vs 10%, night, null/wrong-terrain no-ops) |
  194: GameModel overrides are thin entry points (delegate to `base` + apply feat modifier via the service) and are verified via in-game testing. The `TaomPartySpeedModel.MapTerrain` boundary mapping is verified in-game (it consumes the sealed `TerrainType`).
  205: 6. Update the feat count in `AllFeatProperties_ReturnFeatObject_CountIs77`, `RegisterAll_UsesCorrectStringIds`, and `GetAllFeats_YieldsZeroOrFullSet`; add a `[DataRow]` to `FeatProperty_Exists_IsPublicStatic`; bump the culture entry in `EachCulture_HasExpectedFeatCount`. If the feat sets an `EffectBonus` read in a service test, add it to the reflection table in `CulturalFeatsServiceTests.EnsureFeatsInitialised`.
  207: ### Add a new terrain movement-speed feat
  210: 2. Add an `ApplyIfHas(...)` line to the matching `case` in `CulturalFeatsService.ApplyTerrainSpeedFeats`

 succeeded in 539ms:
   42:     public void ApplyArmyInfluenceAward_RivendellOnly_AddsRivendellBonusToBase()
   44:         var culture = AdapterWith(TaomCulturalFeats.RivendellArmyInfluenceFeat);
   49:             100f + 100f * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus,
   73:     public void ApplyArmyInfluenceCost_GundabadOnly_AppliesGundabadFactor()
   75:         var culture = AdapterWith(TaomCulturalFeats.GundabadArmyInfluenceCostFeat);
   79:         var expected = (int)(100 * (1f + TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus));
   87:             TaomCulturalFeats.RivendellArmyInfluenceCostFeat,
   88:             TaomCulturalFeats.MordorArmyInfluenceCostFeat);
   92:         var multiplier = TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus
   93:                        + TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;
  112:     public void ApplyTerrainSpeedFeats_NullCulture_DoesNothing()
  114:         var en = new ExplainedNumber(1f);
  115:         _sut.ApplyTerrainSpeedFeats(null, TerrainKind.Forest, isNight: false, ref en);
  120:     public void ApplyTerrainSpeedFeats_NoneTerrain_DoesNothing()
  122:         var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
  123:         var en = new ExplainedNumber(1f);
  125:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.None, isNight: false, ref en);
  131:     public void ApplyTerrainSpeedFeats_WrongTerrain_DoesNothing()
  133:         // Mirkwood's forest feat must NOT apply when the party is on a plain.
  134:         var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
  135:         var en = new ExplainedNumber(1f);
  137:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);
  143:     public void ApplyTerrainSpeedFeats_Forest_ElvenFeat_AppliesFlatTenPercent()
  145:         var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
  146:         var en = new ExplainedNumber(1f);
  148:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Forest, isNight: false, ref en);
  150:         // Flat AddFactor of the feat's EffectBonus (0.10), NOT the old scaled value.
  155:     public void ApplyTerrainSpeedFeats_Snow_DwarfFeat_AppliesTenPercent()
  157:         var culture = AdapterWith(TaomCulturalFeats.EreborSnowSpeedFeat);
  158:         var en = new ExplainedNumber(1f);
  160:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Snow, isNight: false, ref en);
  166:     public void ApplyTerrainSpeedFeats_Steppe_KhandFeat_AppliesTenPercent()
  168:         var culture = AdapterWith(TaomCulturalFeats.KhandSteppeSpeedFeat);
  169:         var en = new ExplainedNumber(1f);
  171:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Steppe, isNight: false, ref en);
  177:     public void ApplyTerrainSpeedFeats_Desert_HaradFeat_AppliesTenPercent()
  179:         var culture = AdapterWith(TaomCulturalFeats.HaradDesertSpeedFeat);
  180:         var en = new ExplainedNumber(1f);
  182:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Desert, isNight: false, ref en);
  188:     public void ApplyTerrainSpeedFeats_Plain_MordorFeat_AppliesFivePercent()
  190:         // Mordor's terrain buff is deliberately smaller (5%) than the 10% others get.
  191:         var culture = AdapterWith(TaomCulturalFeats.MordorPlainSpeedFeat);
  192:         var en = new ExplainedNumber(1f);
  194:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);
  200:     public void ApplyTerrainSpeedFeats_Swamp_MordorFeat_AppliesFivePercent()
  202:         var culture = AdapterWith(TaomCulturalFeats.MordorSwampSpeedFeat);
  203:         var en = new ExplainedNumber(1f);
  205:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Swamp, isNight: false, ref en);
  211:     public void ApplyTerrainSpeedFeats_Plain_GondorFeat_AppliesTenPercent()
  213:         var culture = AdapterWith(TaomCulturalFeats.GondorPlainSpeedFeat);
  214:         var en = new ExplainedNumber(1f);
  216:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);
  222:     public void ApplyTerrainSpeedFeats_Night_MordorFeat_AppliesTenPercent()
  224:         var culture = AdapterWith(TaomCulturalFeats.MordorNightSpeedFeat);
  225:         var en = new ExplainedNumber(1f);
  227:         // Night bonus is terrain-independent — passing None still applies it.
  228:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.None, isNight: true, ref en);
  234:     public void ApplyTerrainSpeedFeats_Day_MordorNightFeat_DoesNotApply()
  236:         var culture = AdapterWith(TaomCulturalFeats.MordorNightSpeedFeat);
  237:         var en = new ExplainedNumber(1f);
  239:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);
  245:     public void ApplyTerrainSpeedFeats_MordorPlainAtNight_StacksTerrainAndNight()
  248:             TaomCulturalFeats.MordorPlainSpeedFeat,
  249:             TaomCulturalFeats.MordorNightSpeedFeat);
  250:         var en = new ExplainedNumber(1f);
  252:         _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: true, ref en);
  254:         // 5% plain + 10% night, additive ExplainedNumber factors.
  259:     public void ApplyRohanInfantryPenalty_NoRohanFeat_DoesNothing()
  263:         var en = new ExplainedNumber(1f);
  265:         _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 0, totalCount: 10, ref en);
  271:     public void ApplyRohanInfantryPenalty_MajorityInfantry_AppliesPenalty()
  273:         var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
  274:         var en = new ExplainedNumber(1f);
  277:         _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 4, totalCount: 10, ref en);
  280:             1f * (1f + TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus),
  286:     public void ApplyRohanInfantryPenalty_MostlyMounted_NoPenalty()
  288:         var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
  289:         var en = new ExplainedNumber(1f);
  292:         _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 6, totalCount: 10, ref en);
  298:     public void ApplyRohanInfantryPenalty_ZeroTotal_NoPenalty()
  300:         var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
  301:         var en = new ExplainedNumber(1f);
  303:         _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 0, totalCount: 0, ref en);
  314:             TaomCulturalFeats.RivendellHearthGrowthFeat,
  315:             TaomCulturalFeats.MirkwoodHearthGrowthFeat,
  317:         var en = new ExplainedNumber(-5f);
  326:     public void ApplyHearthGrowthFeats_PositiveResult_RivendellApplies()
  328:         var culture = AdapterWith(TaomCulturalFeats.RivendellHearthGrowthFeat);
  329:         var en = new ExplainedNumber(10f);
  334:             10f * (1f + TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus),
  342:         var en = new ExplainedNumber(10f);
  350:     public void ApplyVeteranMilitiaFeats_MirkwoodOnly_AddsBonus()
  352:         var culture = AdapterWith(TaomCulturalFeats.MirkwoodMilitiaProductionFeat);
  353:         var en = new ExplainedNumber(0.1f);
  358:             0.1f + TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus,
  366:         var en = new ExplainedNumber(0.1f);
  374:     public void ApplyConstructionSpeedFeats_EreborOnly_AppliesFactor()
  376:         var culture = AdapterWith(TaomCulturalFeats.EreborConstructionSpeedFeat);
  377:         var en = new ExplainedNumber(20f);
  382:             20f * (1f + TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus),
  390:         var en = new ExplainedNumber(20f);
  398:     public void ApplyVillageProductionFeats_EreborOnly_GeneralProductionApplies()
  400:         var culture = AdapterWith(TaomCulturalFeats.EreborProductionFeat);
  401:         var en = new ExplainedNumber(50f);
  406:             50f * (1f + TaomCulturalFeats.EreborProductionFeat.EffectBonus),
  412:     public void ApplyVillageProductionFeats_GundabadGrain_OnlyAppliesWhenGrain()
  414:         var culture = AdapterWith(TaomCulturalFeats.GundabadGrainProductionFeat);
  415:         var en = new ExplainedNumber(50f);
  423:     public void ApplyVillageProductionFeats_GundabadGrain_AppliesWhenGrain()
  425:         var culture = AdapterWith(TaomCulturalFeats.GundabadGrainProductionFeat);
  426:         var en = new ExplainedNumber(50f);
  431:             50f * (1f + TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus),
  439:         var en = new ExplainedNumber(50f);
  461:     public void ApplyCaravanCost_UmbarFeat_AppliesFactor()
  463:         var culture = AdapterWith(TaomCulturalFeats.UmbarCheaperCaravansFeat);
  468:             1000 * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus),
  476:     public void ApplyRenownFeats_UmbarOnly_AddsFactor()
  478:         var culture = AdapterWith(TaomCulturalFeats.UmbarRenownFeat);
  479:         var en = new ExplainedNumber(50f);
  484:             50f * (1f + TaomCulturalFeats.UmbarRenownFeat.EffectBonus),
  492:         var en = new ExplainedNumber(50f);
  503:             TaomCulturalFeats.IsengardCheaperRecruitsFeat,
  504:             TaomCulturalFeats.RohanMountedCostFeat);
  505:         var en = new ExplainedNumber(100f);
  513:     public void ApplyTroopUpgradeFeats_MountedIsengard_AppliesFactor()
  515:         var culture = AdapterWith(TaomCulturalFeats.IsengardCheaperRecruitsFeat);
  516:         var en = new ExplainedNumber(100f);
  521:             100f * (1f + TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus),
  529:         var en = new ExplainedNumber(100f);
  537:     public void ApplyPartySizeFeats_MordorOnly_AppliesFactor()
  539:         var culture = AdapterWith(TaomCulturalFeats.MordorPartySizeFeat);
  540:         var en = new ExplainedNumber(100f);
  545:             100f * (1f + TaomCulturalFeats.MordorPartySizeFeat.EffectBonus),
  553:         var en = new ExplainedNumber(100f);
  561:     public void ApplyFoodConsumptionFeats_LothlorienOnly_AppliesFactor()
  563:         var culture = AdapterWith(TaomCulturalFeats.LothlorienFoodConsumptionFeat);
  564:         var en = new ExplainedNumber(10f);
  569:             10f * (1f + TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus),
  578:         var en = new ExplainedNumber(10f);
  591:         var en = new ExplainedNumber(10f);
  602:         var en = new ExplainedNumber(2f);
  617:             TaomCulturalFeats.EreborLoyaltyFeat,
  618:             TaomCulturalFeats.LothlorienLoyaltyFeat,
  619:             TaomCulturalFeats.RivendellLoyaltyFeat,
  620:             TaomCulturalFeats.RohanLoyaltyFeat);
  621:         var en = new ExplainedNumber(0f);
  627:             TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus +
  628:             TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus +
  629:             TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus +
  630:             TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus;
  637:         var en = new ExplainedNumber(2f);
  648:         var en = new ExplainedNumber(50f);
  661:         var en = new ExplainedNumber(50f);
  669:     public void ApplySmithingFeats_EreborOnly_AppliesFactor()
  671:         var culture = AdapterWith(TaomCulturalFeats.EreborSmithingFeat);
  672:         var en = new ExplainedNumber(100f);
  677:             100f * (1f + TaomCulturalFeats.EreborSmithingFeat.EffectBonus),
  685:         var en = new ExplainedNumber(100f);
  693:     public void ApplyTariffIncomeFeats_UmbarOnly_AppliesFactor()
  695:         var culture = AdapterWith(TaomCulturalFeats.UmbarTariffIncomeFeat);
  696:         var en = new ExplainedNumber(500f);
  701:             500f * (1f + TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus),
  709:         var en = new ExplainedNumber(500f);
  717:     public void ApplyRaidDamageFeats_MordorOnly_AppliesFactor()
  719:         var culture = AdapterWith(TaomCulturalFeats.MordorRaidDamageFeat);
  720:         var en = new ExplainedNumber(100f);
  725:             100f * (1f + TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus),
  733:         var en = new ExplainedNumber(100f);
  786:                 ("_ereborGarrisonWage", "taom_erebor_garrison_wage", -0.25f),
  787:                 ("_ereborProduction", "taom_erebor_production", 0.1f),
  788:                 ("_ereborConstructionSpeed", "taom_erebor_construction_speed", -0.15f),
  789:                 ("_ereborLoyalty", "taom_erebor_loyalty", 1f),
  790:                 ("_ereborMorale", "taom_erebor_morale", 5f),
  791:                 ("_ereborSmithing", "taom_erebor_smithing", -0.3f),
  793:                 ("_rivendellArmyInfluence", "taom_rivendell_army_influence", 0.35f),
  794:                 ("_rivendellHearthGrowth", "taom_rivendell_hearth_growth", 0.2f),
  795:                 ("_rivendellArmyInfluenceCost", "taom_rivendell_army_influence_cost", 0.25f),
  796:                 ("_rivendellFoodConsumption", "taom_rivendell_food_consumption", -0.15f),
  797:                 ("_rivendellLoyalty", "taom_rivendell_loyalty", 0.5f),
  799:                 ("_mirkwoodForestSpeed", "taom_mirkwood_forest_speed", 0.1f),
  800:                 ("_mirkwoodMilitiaProduction", "taom_mirkwood_militia_production", 0.25f),
  801:                 ("_mirkwoodHearthGrowth", "taom_mirkwood_hearth_growth", -0.2f),
  802:                 ("_mirkwoodFoodConsumption", "taom_mirkwood_food_consumption", -0.15f),
  803:                 ("_mirkwoodMorale", "taom_mirkwood_morale", 3f),
  805:                 ("_lothlorienForestSpeed", "taom_lothlorien_forest_speed", 0.1f),
  806:                 ("_lothlorienGarrisonWage", "taom_lothlorien_garrison_wage", -0.2f),
  807:                 ("_lothlorienConstructionSpeed", "taom_lothlorien_construction_speed", -0.1f),
  808:                 ("_lothlorienFoodConsumption", "taom_lothlorien_food_consumption", -0.15f),
  809:                 ("_lothlorienLoyalty", "taom_lothlorien_loyalty", 0.5f),
  810:                 ("_lothlorienMorale", "taom_lothlorien_morale", 3f),
  812:                 ("_isengardCheaperRecruits", "taom_isengard_cheaper_recruits", -0.15f),
  813:                 ("_isengardGarrisonWage", "taom_isengard_garrison_wage", -0.2f),
  814:                 ("_isengardDecisionPenalty", "taom_isengard_decision_penalty", 0.25f),
  815:                 ("_isengardPartySize", "taom_isengard_party_size", 0.2f),
  816:                 ("_isengardConstructionSpeed", "taom_isengard_construction_speed", 0.15f),
  817:                 ("_isengardSmithing", "taom_isengard_smithing", -0.2f),
  818:                 ("_isengardRaidDamage", "taom_isengard_raid_damage", 0.2f),
  820:                 ("_gundabadArmyInfluenceCost", "taom_gundabad_army_influence_cost", -0.4f),
  821:                 ("_gundabadGrainProduction", "taom_gundabad_grain_production", 0.15f),
  822:                 ("_gundabadWage", "taom_gundabad_wage", 0.1f),
  823:                 ("_gundabadPartySize", "taom_gundabad_party_size", 0.3f),
  824:                 ("_gundabadRaidDamage", "taom_gundabad_raid_damage", 0.25f),
  826:                 ("_umbarCheaperCaravans", "taom_umbar_cheaper_caravans", -0.25f),
  827:                 ("_umbarRenown", "taom_umbar_renown", 0.08f),
  828:                 ("_umbarWage", "taom_umbar_wage", 0.08f),
  829:                 ("_umbarTariffIncome", "taom_umbar_tariff_income", 0.15f),
  844:                 ("_mordorArmyInfluenceCost", "taom_mordor_army_influence_cost", -0.6f),
  845:                 ("_mordorGrainProduction", "taom_mordor_grain_production", 0.2f),
  846:                 ("_mordorWage", "taom_mordor_wage", 0.2f),
  847:                 ("_mordorPartySize", "taom_mordor_party_size", 0.3f),
  848:                 ("_mordorRaidDamage", "taom_mordor_raid_damage", 0.25f),
  850:                 ("_rohanMountedCost", "taom_rohan_mounted_cost", -0.15f),
  851:                 ("_rohanMountedWage", "taom_rohan_mounted_wage", -0.15f),
  852:                 ("_rohanInfantrySpeed", "taom_rohan_infantry_speed", -0.1f),
  853:                 ("_rohanLoyalty", "taom_rohan_loyalty", 0.5f),
  854:                 ("_rohanMorale", "taom_rohan_morale", 5f),
  856:                 // Terrain movement-speed feats (issue: cultural terrain bonuses)
  857:                 ("_ereborSnowSpeed", "taom_erebor_snow_speed", 0.1f),
  858:                 ("_rivendellForestSpeed", "taom_rivendell_forest_speed", 0.1f),
  859:                 ("_isengardPlainSpeed", "taom_isengard_plain_speed", 0.1f),
  860:                 ("_isengardSwampSpeed", "taom_isengard_swamp_speed", 0.1f),
  861:                 ("_gundabadSnowSpeed", "taom_gundabad_snow_speed", 0.1f),
  862:                 ("_umbarDesertSpeed", "taom_umbar_desert_speed", 0.1f),
  863:                 ("_gondorPlainSpeed", "taom_gondor_plain_speed", 0.1f),
  864:                 ("_mordorPlainSpeed", "taom_mordor_plain_speed", 0.05f),
  865:                 ("_mordorSwampSpeed", "taom_mordor_swamp_speed", 0.05f),
  866:                 ("_mordorNightSpeed", "taom_mordor_night_speed", 0.1f),
  867:                 ("_rohanPlainSpeed", "taom_rohan_plain_speed", 0.1f),
  868:                 ("_dalePlainSpeed", "taom_dale_plain_speed", 0.1f),
  869:                 ("_khandSteppeSpeed", "taom_khand_steppe_speed", 0.1f),
  870:                 ("_rhunSteppeSpeed", "taom_rhun_steppe_speed", 0.1f),
  871:                 ("_haradDesertSpeed", "taom_harad_desert_speed", 0.1f),
  872:                 ("_dunlandPlainSpeed", "taom_dunland_plain_speed", 0.1f),
  873:                 ("_shaghanaDesertSpeed", "taom_shaghana_desert_speed", 0.1f),
  874:                 ("_abanissaDesertSpeed", "taom_abanissa_desert_speed", 0.1f),

 succeeded in 764ms:
   19:     public void AllFeatProperties_ReturnFeatObject_CountIs77()
   26:         Assert.AreEqual(77, properties.Count,
   27:             "Expected 77 culture feat properties (59 base + 18 terrain movement-speed feats across 18 cultures)");
   44:     public void GetAllFeats_YieldsZeroOrFullSet()
   46:         // GetAllFeats returns empty when the static `_instance` is null (no game
   47:         // framework loaded), OR the full 77-feat enumeration when a sibling test
   50:         var feats = TaomCulturalFeats.GetAllFeats().ToList();
   51:         Assert.IsTrue(feats.Count == 0 || feats.Count == 77,
   52:             $"GetAllFeats expected 0 (uninitialised) or 77 (full set), got {feats.Count}");
   58:     [DataRow("EreborConstructionSpeedFeat")]
   67:     [DataRow("MirkwoodForestSpeedFeat")]
   72:     [DataRow("LothlorienForestSpeedFeat")]
   74:     [DataRow("LothlorienConstructionSpeedFeat")]
   82:     [DataRow("IsengardConstructionSpeedFeat")]
   96:     [DataRow("DolGuldurConstructionSpeedFeat")]
  105:     [DataRow("MordorArmyInfluenceCostFeat")]
  106:     [DataRow("MordorGrainProductionFeat")]
  107:     [DataRow("MordorWageFeat")]
  108:     [DataRow("MordorPartySizeFeat")]
  109:     [DataRow("MordorRaidDamageFeat")]
  112:     [DataRow("RohanInfantrySpeedFeat")]
  115:     [DataRow("EreborSnowSpeedFeat")]
  116:     [DataRow("RivendellForestSpeedFeat")]
  117:     [DataRow("IsengardPlainSpeedFeat")]
  118:     [DataRow("IsengardSwampSpeedFeat")]
  119:     [DataRow("GundabadSnowSpeedFeat")]
  120:     [DataRow("UmbarDesertSpeedFeat")]
  121:     [DataRow("GondorPlainSpeedFeat")]
  122:     [DataRow("MordorPlainSpeedFeat")]
  123:     [DataRow("MordorSwampSpeedFeat")]
  124:     [DataRow("MordorNightSpeedFeat")]
  125:     [DataRow("RohanPlainSpeedFeat")]
  126:     [DataRow("DalePlainSpeedFeat")]
  127:     [DataRow("KhandSteppeSpeedFeat")]
  128:     [DataRow("RhunSteppeSpeedFeat")]
  129:     [DataRow("HaradDesertSpeedFeat")]
  130:     [DataRow("DunlandPlainSpeedFeat")]
  131:     [DataRow("ShaghanaDesertSpeedFeat")]
  132:     [DataRow("AbanissaDesertSpeedFeat")]
  163:             { "Mordor", 8 },
  191:         Assert.AreEqual(77, fields.Count,
  192:             "Expected 77 private FeatObject fields (59 base + 18 terrain movement-speed feats)");

codex
The first code pass shows the terrain feature is confined to the expected model, service, feat registry, XML/XSLT, docs, and tests. I’m now switching to installed-DLL decompilation for the claims that depend on engine behavior, especially snow and `ExplainedNumber` factor stacking.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd -ErrorAction SilentlyContinue | Format-List *' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'DefaultPartySpeedCalculatingModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter '*.dll' | Where-Object { "'$_.Name -in @('"'SandBox.dll','TaleWorlds.CampaignSystem.dll','TaleWorlds.Core.dll') } | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'TerrainType.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 547ms:

[32;1mHelpUri            : [0m
[32;1mFileVersionInfo    : [0mFile:             C:\Users\mikew\.dotnet\tools\ilspycmd.exe
                     InternalName:     ilspycmd.dll
                     OriginalFilename: ilspycmd.dll
                     FileVersion:      10.0.1.8346
                     FileDescription:  ilspycmd
                     Product:          ilspycmd
                     ProductVersion:   10.0.1.8346+aad16c66e96eb887eb05887d6b5a9e0522637906
                     Debug:            False
                     Patched:          False
                     PreRelease:       False
                     PrivateBuild:     False
                     SpecialBuild:     False
                     Language:         Language Neutral
                     
[32;1mPath               : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mExtension          : [0m.exe
[32;1mDefinition         : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mSource             : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mVersion            : [0m10.0.1.8346
[32;1mVisibility         : [0mPublic
[32;1mOutputType         : [0m{System.String}
[32;1mName               : [0milspycmd.exe
[32;1mCommandType        : [0mApplication
[32;1mModuleName         : [0m
[32;1mModule             : [0m
[32;1mRemotingCapability : [0mPowerShell
[32;1mParameters         : [0m
[32;1mParameterSets      : [0m


 succeeded in 767ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeedCalculatingModel.cs

 succeeded in 1015ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\TerrainType.cs

 succeeded in 1254ms:

[32;1mFullName                                                                                                     [0m[32;1m  Length[0m[32;1m L[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m a[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m s[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m t[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m W[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m r[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m i[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m t[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m e[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m T[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m i[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m m[0m
[32;1m                                                                                                             [0m[32;1m        [0m[32;1m e[0m
[32;1m--------                                                                                                     [0m [32;1m ------[0m [32;1m-[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll 5461952 5
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Core.dll            495040 5


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.Library.dll' | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'ExplainedNumber.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.Core.TerrainType 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 552ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs

 succeeded in 1124ms:
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents;

public class DefaultPartySpeedCalculatingModel : PartySpeedModel
{
	private static readonly TextObject _textCargo = new TextObject("{=fSGY71wd}Cargo within capacity");

	private static readonly TextObject _textOverburdened = new TextObject("{=xgO3cCgR}Overburdened");

	private static readonly TextObject _textOverPartySize = new TextObject("{=bO5gL3FI}Men within party size");

	private static readonly TextObject _textOverPrisonerSize = new TextObject("{=Ix8YjLPD}Men within prisoner size");

	private static readonly TextObject _textCavalry = new TextObject("{=YVGtcLHF}Cavalry");

	private static readonly TextObject _textCavalryWeatherPenalty = new TextObject("{=Cb0k9KM8}Cavalry weather penalty");

	private static readonly TextObject _textKhuzaitCavalryBonus = new TextObject("{=yi07dBks}Khuzait cavalry bonus");

	private static readonly TextObject _textMountedFootmen = new TextObject("{=5bSWSaPl}Footmen on horses");

	private static readonly TextObject _textMountedFootmenWeatherPenalty = new TextObject("{=JAKoFNgt}Footmen on horses weather penalty");

	private static readonly TextObject _textWounded = new TextObject("{=aLsVKIRy}Wounded members");

	private static readonly TextObject _textPrisoners = new TextObject("{=N6QTvjMf}Prisoners");

	private static readonly TextObject _textHerd = new TextObject("{=NhAMSaWU}Herding");

	private static readonly TextObject _textHighMorale = new TextObject("{=aDQcIGfH}High morale");

	private static readonly TextObject _textLowMorale = new TextObject("{=ydspCDIy}Low morale");

	private static readonly TextObject _textCaravan = new TextObject("{=vvabqi2w}Caravan");

	private static readonly TextObject _textDisorganized = new TextObject("{=JuwBb2Yg}Disorganized");

	private static readonly TextObject _movingInForest = new TextObject("{=rTFaZCdY}Forest");

	private static readonly TextObject _fordEffect = new TextObject("{=NT5fwUuJ}Fording");

	private static readonly TextObject _night = new TextObject("{=fAxjyMt5}Night");

	private static readonly TextObject _snow = new TextObject("{=vLjgcdgB}Snow");

	private static readonly TextObject _desert = new TextObject("{=ecUwABe2}Desert");

	private static readonly TextObject _sturgiaSnowBonus = new TextObject("{=0VfEGekD}Sturgia snow bonus");

	private readonly TextObject _culture = GameTexts.FindText("str_culture");

	private const float MovingAtForestEffect = -0.3f;

	private const float MovingAtWaterEffect = -0.3f;

	private const float MovingAtNightEffect = -0.25f;

	private const float MovingOnSnowEffect = -0.1f;

	private const float MovingInDesertEffect = -0.1f;

	private const float CavalryEffect = 0.3f;

	private const float MountedFootMenEffect = 0.15f;

	private const float HerdEffect = -0.4f;

	private const float WoundedEffect = -0.05f;

	private const float CargoEffect = -0.02f;

	private const float OverburdenedEffect = -0.4f;

	private const float HighMoraleThreshold = 70f;

	private const float LowMoraleThreshold = 30f;

	private const float HighMoraleEffect = 0.05f;

	private const float LowMoraleEffect = -0.1f;

	private const float DisorganizedEffect = -0.4f;

	public override float BaseSpeed => 4f;

	public override float MinimumSpeed => 1f;

	private ExplainedNumber CalculateLandBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
	{
		PartyBase party = mobileParty.Party;
		int numberOfAvailableMounts = 0;
		float totalWeightCarried = 0f;
		int herdSize = 0;
		int num = mobileParty.MemberRoster.TotalManCount + additionalTroopOnFootCount + additionalTroopOnHorseCount;
		AddCargoStats(mobileParty, ref numberOfAvailableMounts, ref totalWeightCarried, ref herdSize);
		float num2 = mobileParty.TotalWeightCarried;
		int num3 = (int)Campaign.Current.Models.InventoryCapacityModel.CalculateInventoryCapacity(mobileParty, mobileParty.IsCurrentlyAtSea, includeDescriptions: false, additionalTroopOnFootCount, additionalTroopOnHorseCount).ResultNumber;
		int num4 = party.NumberOfMenWithHorse + additionalTroopOnHorseCount;
		int num5 = party.NumberOfMenWithoutHorse + additionalTroopOnFootCount;
		int num6 = party.MemberRoster.TotalWounded;
		int num7 = party.PrisonRoster.TotalManCount;
		int num8 = party.PartySizeLimit;
		float morale = mobileParty.Morale;
		if (mobileParty.AttachedParties.Count != 0)
		{
			foreach (MobileParty attachedParty in mobileParty.AttachedParties)
			{
				AddCargoStats(attachedParty, ref numberOfAvailableMounts, ref totalWeightCarried, ref herdSize);
				num += attachedParty.MemberRoster.TotalManCount;
				num2 += attachedParty.TotalWeightCarried;
				num3 += attachedParty.InventoryCapacity;
				num4 += attachedParty.Party.NumberOfMenWithHorse;
				num5 += attachedParty.Party.NumberOfMenWithoutHorse;
				num6 += attachedParty.MemberRoster.TotalWounded;
				num7 += attachedParty.PrisonRoster.TotalManCount;
				num8 += attachedParty.Party.PartySizeLimit;
			}
		}
		float baseNumber = CalculateBaseSpeedForParty(num);
		ExplainedNumber result = new ExplainedNumber(baseNumber, includeDescriptions);
		bool num9 = Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(mobileParty.Position.ToVec2()) == MapWeatherModel.WeatherEventEffectOnTerrain.Wet;
		GetFootmenPerkBonus(mobileParty, num, num5, ref result);
		float cavalryRatioModifier = GetCavalryRatioModifier(num, num4);
		int num10 = MathF.Min(num5, numberOfAvailableMounts);
		float mountedFootmenRatioModifier = GetMountedFootmenRatioModifier(num, num10);
		result.AddFactor(cavalryRatioModifier, _textCavalry);
		result.AddFactor(mountedFootmenRatioModifier, _textMountedFootmen);
		if (num9)
		{
			float num11 = cavalryRatioModifier * 0.3f;
			float num12 = mountedFootmenRatioModifier * 0.3f;
			result.AddFactor(0f - num11, _textCavalryWeatherPenalty);
			result.AddFactor(0f - num12, _textMountedFootmenWeatherPenalty);
		}
		if (mountedFootmenRatioModifier > 0f && mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Riding.NomadicTraditions))
		{
			result.AddFactor(mountedFootmenRatioModifier * DefaultPerks.Riding.NomadicTraditions.PrimaryBonus, DefaultPerks.Riding.NomadicTraditions.Name);
		}
		float num13 = MathF.Min(num2, (float)num3);
		if (num13 > 0f)
		{
			float cargoEffect = GetCargoEffect(num13, num3);
			result.AddFactor(cargoEffect, _textCargo);
		}
		if (totalWeightCarried > (float)num3)
		{
			ExplainedNumber overburdenedEffect = GetOverburdenedEffect(mobileParty, totalWeightCarried - (float)num3, num3, includeDescriptions);
			result.AddFromExplainedNumber(overburdenedEffect, _textOverburdened);
		}
		if (mobileParty.HasPerk(DefaultPerks.Riding.SweepingWind, checkSecondaryRole: true))
		{
			result.AddFactor(DefaultPerks.Riding.SweepingWind.SecondaryBonus, DefaultPerks.Riding.SweepingWind.Name);
		}
		if (num > num8)
		{
			float overPartySizeEffect = GetOverPartySizeEffect(num, num8);
			if (mobileParty.ActualClan?.StringId == "deserters")
			{
				result.AddFactor(overPartySizeEffect * 0.5f, _textOverPartySize);
			}
			else
			{
				result.AddFactor(overPartySizeEffect, _textOverPartySize);
			}
		}
		herdSize += MathF.Max(0, numberOfAvailableMounts - num10);
		if (!mobileParty.IsVillager)
		{
			float herdingModifier = GetHerdingModifier(num, herdSize);
			result.AddFactor(herdingModifier, _textHerd);
			if (mobileParty.HasPerk(DefaultPerks.Riding.Shepherd))
			{
				result.AddFactor(herdingModifier * DefaultPerks.Riding.Shepherd.PrimaryBonus, DefaultPerks.Riding.Shepherd.Name);
			}
		}
		float woundedModifier = GetWoundedModifier(num, num6, mobileParty);
		result.AddFactor(woundedModifier, _textWounded);
		if (!mobileParty.IsCaravan)
		{
			if (mobileParty.Party.NumberOfPrisoners > mobileParty.Party.PrisonerSizeLimit)
			{
				float overPrisonerSizeEffect = GetOverPrisonerSizeEffect(mobileParty);
				result.AddFactor(overPrisonerSizeEffect, _textOverPrisonerSize);
			}
			float sizeModifierPrisoner = GetSizeModifierPrisoner(num, num7);
			result.AddFactor(1f / sizeModifierPrisoner - 1f, _textPrisoners);
		}
		if (morale > 70f)
		{
			result.AddFactor(0.05f * ((morale - 70f) / 30f), _textHighMorale);
		}
		if (morale < 30f)
		{
			result.AddFactor(-0.1f * (1f - mobileParty.Morale / 30f), _textLowMorale);
		}
		if (mobileParty == MobileParty.MainParty)
		{
			float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
			if (playerMapMovementSpeedBonusMultiplier > 0f)
			{
				result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty"));
			}
		}
		if (mobileParty.IsCaravan)
		{
			result.AddFactor(0.1f, _textCaravan);
		}
		if (mobileParty.IsDisorganized)
		{
			result.AddFactor(-0.4f, _textDisorganized);
		}
		result.LimitMin(MinimumSpeed);
		return result;
	}

	public override ExplainedNumber CalculateBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
	{
		return CalculateLandBaseSpeed(mobileParty, includeDescriptions, additionalTroopOnFootCount, additionalTroopOnHorseCount);
	}

	private void AddCargoStats(MobileParty mobileParty, ref int numberOfAvailableMounts, ref float totalWeightCarried, ref int herdSize)
	{
		ItemRoster itemRoster = mobileParty.ItemRoster;
		int numberOfPackAnimals = itemRoster.NumberOfPackAnimals;
		int numberOfLivestockAnimals = itemRoster.NumberOfLivestockAnimals;
		herdSize += numberOfPackAnimals + numberOfLivestockAnimals;
		numberOfAvailableMounts += itemRoster.NumberOfMounts;
		totalWeightCarried += mobileParty.TotalWeightCarried;
	}

	private float CalculateBaseSpeedForParty(int menCount)
	{
		return BaseSpeed * MathF.Pow(200f / (200f + (float)menCount), 0.4f);
	}

	private ExplainedNumber GetOverburdenedEffect(MobileParty party, float totalWeightCarried, int partyCapacity, bool includeDescriptions)
	{
		ExplainedNumber stat = new ExplainedNumber(-0.4f * (totalWeightCarried / (float)partyCapacity), includeDescriptions);
		if (!party.IsCurrentlyAtSea)
		{
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Athletics.Energetic, party, isPrimaryBonus: true, ref stat);
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.Unburdened, party, isPrimaryBonus: true, ref stat);
		}
		return stat;
	}

	public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
	{
		if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed.ApproximatelyEqualsTo(0f))
		{
			finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed);
		}
		TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
		Hero effectiveScout = mobileParty.EffectiveScout;
		if (faceTerrainType == TerrainType.Forest)
		{
			float num = 0f;
			if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForestKin))
			{
				for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
				{
					if (!mobileParty.MemberRoster.GetCharacterAtIndex(i).IsMounted)
					{
						num += (float)mobileParty.MemberRoster.GetElementNumber(i);
					}
				}
			}
			float value = ((num / (float)mobileParty.MemberRoster.TotalManCount >= 0.75f) ? (-0.3f * (0f - DefaultPerks.Scouting.ForestKin.PrimaryBonus)) : (-0.3f));
			finalSpeed.AddFactor(value, _movingInForest);
			if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
			{
				float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
				finalSpeed.AddFactor(value2, _culture);
			}
		}
		else if (!mobileParty.IsCurrentlyAtSea && (faceTerrainType == TerrainType.Water || faceTerrainType == TerrainType.River || faceTerrainType == TerrainType.UnderBridge || faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.Fording))
		{
			finalSpeed.AddFactor(-0.3f, _fordEffect);
		}
		else
		{
			switch (faceTerrainType)
			{
			case TerrainType.Desert:
			case TerrainType.Dune:
				if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
				{
					finalSpeed.AddFactor(-0.1f, _desert);
				}
				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.DesertBorn.PrimaryBonus, DefaultPerks.Scouting.DesertBorn.Name);
				}
				break;
			case TerrainType.Plain:
			case TerrainType.Steppe:
				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Pathfinder))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.Pathfinder.PrimaryBonus, DefaultPerks.Scouting.Pathfinder.Name);
				}
				break;
			}
		}
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
				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.NightRunner))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.NightRunner.PrimaryBonus, DefaultPerks.Scouting.NightRunner.Name);
				}
			}
			else if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DayTraveler))
			{
				finalSpeed.AddFactor(DefaultPerks.Scouting.DayTraveler.PrimaryBonus, DefaultPerks.Scouting.DayTraveler.Name);
			}
		}
		if (effectiveScout != null)
		{
			if (!mobileParty.IsCurrentlyAtSea)
			{
				PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Scouting.UncannyInsight, effectiveScout.CharacterObject, DefaultSkills.Scouting, applyPrimaryBonus: true, ref finalSpeed, Campaign.Current.Models.CharacterDevelopmentModel.MinSkillRequiredForEpicPerkBonus);
				if (effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForcedMarch) && mobileParty.Morale > 75f)
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.ForcedMarch.PrimaryBonus, DefaultPerks.Scouting.ForcedMarch.Name);
				}
			}
			if (mobileParty.DefaultBehavior == AiBehavior.EngageParty)
			{
				MobileParty targetParty = mobileParty.TargetParty;
				if (targetParty != null && !targetParty.IsCurrentlyAtSea && targetParty.MapFaction.IsAtWarWith(mobileParty.MapFaction) && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Tracker))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.Tracker.SecondaryBonus, DefaultPerks.Scouting.Tracker.Name);
				}
			}
		}
		if (mobileParty.Army?.LeaderParty != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.AttachedTo != mobileParty.Army.LeaderParty && !mobileParty.IsCurrentlyAtSea && mobileParty.Army.LeaderParty.HasPerk(DefaultPerks.Tactics.CallToArms))
		{
			finalSpeed.AddFactor(DefaultPerks.Tactics.CallToArms.PrimaryBonus, DefaultPerks.Tactics.CallToArms.Name);
		}
		finalSpeed.LimitMin(MinimumSpeed);
		return finalSpeed;
	}

	private float GetCargoEffect(float weightCarried, int partyCapacity)
	{
		return -0.02f * weightCarried / (float)partyCapacity;
	}

	private float GetOverPartySizeEffect(int totalMenCount, int partySize)
	{
		return 1f / ((float)totalMenCount / (float)partySize) - 1f;
	}

	private float GetOverPrisonerSizeEffect(MobileParty mobileParty)
	{
		int prisonerSizeLimit = mobileParty.Party.PrisonerSizeLimit;
		int numberOfPrisoners = mobileParty.Party.NumberOfPrisoners;
		return 1f / ((float)numberOfPrisoners / (float)prisonerSizeLimit) - 1f;
	}

	private float GetHerdingModifier(int totalMenCount, int herdSize)
	{
		herdSize -= totalMenCount;
		if (herdSize <= 0)
		{
			return 0f;
		}
		if (totalMenCount == 0)
		{
			return -0.8f;
		}
		return MathF.Max(-0.8f, -0.3f * ((float)herdSize / (float)totalMenCount));
	}

	private float GetWoundedModifier(int totalMenCount, int numWounded, MobileParty party)
	{
		if (numWounded <= totalMenCount / 4)
		{
			return 0f;
		}
		if (totalMenCount == 0)
		{
			return -0.5f;
		}
		float baseNumber = MathF.Max(-0.8f, -0.05f * (float)numWounded / (float)totalMenCount);
		ExplainedNumber stat = new ExplainedNumber(baseNumber);
		if (!party.IsCurrentlyAtSea)
		{
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Medicine.Sledges, party, isPrimaryBonus: true, ref stat);
		}
		return stat.ResultNumber;
	}

	private float GetCavalryRatioModifier(int totalMenCount, int totalCavalryCount)
	{
		if (totalMenCount == 0 || totalCavalryCount == 0)
		{
			return 0f;
		}
		return 0.3f * (float)totalCavalryCount / (float)totalMenCount;
	}

	private float GetMountedFootmenRatioModifier(int totalMenCount, int totalMountedFootmenCount)
	{
		if (totalMenCount == 0 || totalMountedFootmenCount == 0)
		{
			return 0f;
		}
		return 0.15f * (float)totalMountedFootmenCount / (float)totalMenCount;
	}

	private void GetFootmenPerkBonus(MobileParty party, int totalMenCount, int totalFootmenCount, ref ExplainedNumber result)
	{
		if (totalMenCount != 0)
		{
			float num = (float)totalFootmenCount / (float)totalMenCount;
			if (party.HasPerk(DefaultPerks.Athletics.Strong, checkSecondaryRole: true) && !num.ApproximatelyEqualsTo(0f))
			{
				result.AddFactor(num * DefaultPerks.Athletics.Strong.SecondaryBonus, DefaultPerks.Athletics.Strong.Name);
			}
		}
	}

	private float GetSizeModifierWounded(int totalMenCount, int totalWoundedMenCount)
	{
		return MathF.Pow((10f + (float)totalMenCount) / (10f + (float)totalMenCount - (float)totalWoundedMenCount), 0.33f);
	}

	private float GetSizeModifierPrisoner(int totalMenCount, int totalPrisonerCount)
	{
		return MathF.Pow((10f + (float)totalMenCount + (float)totalPrisonerCount) / (10f + (float)totalMenCount), 0.33f);
	}
}

 succeeded in 1509ms:
namespace TaleWorlds.Core;

public enum TerrainType
{
	Plain = 1,
	Desert = 2,
	Snow = 3,
	Forest = 4,
	Steppe = 5,
	Fording = 6,
	Mountain = 7,
	Lake = 8,
	Water = 10,
	River = 11,
	Canyon = 13,
	RuralArea = 14,
	Swamp = 15,
	Dune = 16,
	Bridge = 17,
	CoastalSea = 18,
	OpenSea = 19,
	Beach = 20,
	Cliff = 21,
	NonNavigableRiver = 22,
	LandRestriction = 23,
	SeaRestriction = 24,
	UnderBridge = 25
}

 succeeded in 1741ms:

[32;1mFullName                                                                                              [0m[32;1m Length[0m[32;1m LastWrite[0m
[32;1m                                                                                                      [0m[32;1m       [0m[32;1m Time[0m
[32;1m--------                                                                                              [0m [32;1m------[0m [32;1m---------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Library.dll 263616 5/22/202…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.ExplainedNumber 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\**\\*.cs' -Pattern 'GetFaceTerrainType' -ErrorAction SilentlyContinue | Select-Object -First 30 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*MapSceneWrapper*.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\ExplainedNumber.cs' | Select-Object -First 220" in C:\Users\mikew\source\repos\TAOM
 succeeded in 573ms:
 succeeded in 832ms:
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem;

public struct ExplainedNumber
{
	private class StatExplainer
	{
		public enum OperationType
		{
			Base,
			Add,
			Multiply,
			LimitMin,
			LimitMax
		}

		public readonly struct ExplanationLine(string name, float number, OperationType operationType)
		{
			public readonly float Number = number;

			public readonly string Name = name;

			public readonly OperationType OperationType = operationType;
		}

		public List<ExplanationLine> Lines { get; private set; } = new List<ExplanationLine>();

		public ExplanationLine? BaseLine { get; private set; }

		public ExplanationLine? LimitMinLine { get; private set; }

		public ExplanationLine? LimitMaxLine { get; private set; }

		public List<(string name, float number)> GetLines(float baseNumber, float unclampedResultNumber, TextObject overrideBaseLineText = null, TextObject overrideMaximumLineText = null, TextObject overrideMinimumLineText = null)
		{
			List<(string, float)> list = new List<(string, float)>();
			if (BaseLine.HasValue)
			{
				list.Add(((overrideBaseLineText != null) ? overrideBaseLineText.ToString() : BaseLine.Value.Name, BaseLine.Value.Number));
			}
			foreach (ExplanationLine line in Lines)
			{
				float num = line.Number;
				if (line.OperationType == OperationType.Multiply)
				{
					num = baseNumber * num * 0.01f;
				}
				list.Add((line.Name, num));
			}
			if (LimitMinLine.HasValue && LimitMinLine.Value.Number > unclampedResultNumber)
			{
				list.Add(((overrideMinimumLineText != null) ? overrideMinimumLineText.ToString() : LimitMinLine.Value.Name, LimitMinLine.Value.Number - unclampedResultNumber));
			}
			if (LimitMaxLine.HasValue && LimitMaxLine.Value.Number < unclampedResultNumber)
			{
				list.Add(((overrideMaximumLineText != null) ? overrideMaximumLineText.ToString() : LimitMaxLine.Value.Name, LimitMaxLine.Value.Number - unclampedResultNumber));
			}
			return list;
		}

		public void AddLine(string name, float number, OperationType opType)
		{
			ExplanationLine explanationLine = new ExplanationLine(name, number, opType);
			switch (opType)
			{
			case OperationType.Add:
			case OperationType.Multiply:
			{
				int num = -1;
				for (int i = 0; i < Lines.Count; i++)
				{
					if (Lines[i].Name.Equals(name) && Lines[i].OperationType == opType)
					{
						num = i;
						break;
					}
				}
				if (num < 0)
				{
					Lines.Add(explanationLine);
					break;
				}
				explanationLine = new ExplanationLine(name, number + Lines[num].Number, opType);
				Lines[num] = explanationLine;
				break;
			}
			case OperationType.Base:
				BaseLine = explanationLine;
				break;
			case OperationType.LimitMin:
				LimitMinLine = explanationLine;
				break;
			case OperationType.LimitMax:
				LimitMaxLine = explanationLine;
				break;
			}
		}
	}

	private static readonly TextObject LimitMinText = new TextObject("{=GNalaRaN}Minimum");

	private static readonly TextObject LimitMaxText = new TextObject("{=cfjTtxWv}Maximum");

	private static readonly TextObject BaseText = new TextObject("{=basevalue}Base");

	private float? _limitMinValue;

	private float? _limitMaxValue;

	private StatExplainer _explainer;

	public float ResultNumber => MathF.Clamp(_unclampedResultNumber, LimitMinValue, LimitMaxValue);

	public int RoundedResultNumber => MathF.Round(ResultNumber);

	public float BaseNumber { get; private set; }

	public bool IncludeDescriptions => _explainer != null;

	public float LimitMinValue
	{
		get
		{
			if (!_limitMinValue.HasValue)
			{
				return float.MinValue;
			}
			return _limitMinValue.Value;
		}
	}

	public float LimitMaxValue
	{
		get
		{
			if (!_limitMaxValue.HasValue)
			{
				return float.MaxValue;
			}
			return _limitMaxValue.Value;
		}
	}

	public float SumOfFactors { get; private set; }

	private float _unclampedResultNumber => BaseNumber + BaseNumber * SumOfFactors;

	public ExplainedNumber(float baseNumber = 0f, bool includeDescriptions = false, TextObject baseText = null)
	{
		BaseNumber = baseNumber;
		_explainer = (includeDescriptions ? new StatExplainer() : null);
		SumOfFactors = 0f;
		_limitMinValue = float.MinValue;
		_limitMaxValue = float.MaxValue;
		if (_explainer != null && !BaseNumber.ApproximatelyEqualsTo(0f))
		{
			_explainer.AddLine((baseText ?? BaseText).ToString(), BaseNumber, StatExplainer.OperationType.Base);
		}
	}

	public string GetExplanations()
	{
		if (_explainer == null)
		{
			return "";
		}
		MBStringBuilder mBStringBuilder = default(MBStringBuilder);
		mBStringBuilder.Initialize(16, "GetExplanations");
		foreach (var line in _explainer.GetLines(BaseNumber, _unclampedResultNumber))
		{
			string value = string.Format("{0} : {1}{2:0.##}\n", line.name, (line.number > 0.001f) ? "+" : "", line.number);
			mBStringBuilder.Append(value);
		}
		return mBStringBuilder.ToStringAndRelease();
	}

	public List<(string name, float number)> GetLines()
	{
		if (_explainer == null)
		{
			return new List<(string, float)>();
		}
		return _explainer.GetLines(BaseNumber, _unclampedResultNumber);
	}

	public void AddFromExplainedNumber(ExplainedNumber explainedNumber, TextObject baseText)
	{
		if (explainedNumber._explainer != null && _explainer != null)
		{
			TextObject textObject = new TextObject("{=HKoLNyIm}{BASE} Maximum");
			TextObject textObject2 = new TextObject("{=0Fliz2vk}{BASE} Minimum");
			textObject.SetTextVariable("BASE", baseText);
			textObject2.SetTextVariable("BASE", baseText);
			foreach (var line in explainedNumber._explainer.GetLines(explainedNumber.BaseNumber, explainedNumber._unclampedResultNumber, baseText, textObject, textObject2))
			{
				_explainer.AddLine(line.name, line.number, StatExplainer.OperationType.Add);
			}
		}
		BaseNumber += explainedNumber.ResultNumber;
	}

	public void SubtractFromExplainedNumber(ExplainedNumber explainedNumber, TextObject baseText)
	{
		if (explainedNumber._explainer != null && _explainer != null)
		{
			TextObject textObject = new TextObject("{=HKoLNyIm}{BASE} Maximum");
			TextObject textObject2 = new TextObject("{=0Fliz2vk}{BASE} Minimum");
			textObject.SetTextVariable("BASE", baseText);
			textObject2.SetTextVariable("BASE", baseText);
			foreach (var line in explainedNumber._explainer.GetLines(explainedNumber.BaseNumber, explainedNumber._unclampedResultNumber, baseText, textObject, textObject2))
			{
				_explainer.AddLine(line.name, 0f - line.number, StatExplainer.OperationType.Add);
			}
		}
		BaseNumber -= explainedNumber.ResultNumber;
	}


 succeeded in 1058ms:
 succeeded in 1092ms:
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem;

public struct ExplainedNumber
{
	private class StatExplainer
	{
		public enum OperationType
		{
			Base,
			Add,
			Multiply,
			LimitMin,
			LimitMax
		}

		public readonly struct ExplanationLine(string name, float number, OperationType operationType)
		{
			public readonly float Number = number;

			public readonly string Name = name;

			public readonly OperationType OperationType = operationType;
		}

		public List<ExplanationLine> Lines { get; private set; } = new List<ExplanationLine>();

		public ExplanationLine? BaseLine { get; private set; }

		public ExplanationLine? LimitMinLine { get; private set; }

		public ExplanationLine? LimitMaxLine { get; private set; }

		public List<(string name, float number)> GetLines(float baseNumber, float unclampedResultNumber, TextObject overrideBaseLineText = null, TextObject overrideMaximumLineText = null, TextObject overrideMinimumLineText = null)
		{
			List<(string, float)> list = new List<(string, float)>();
			if (BaseLine.HasValue)
			{
				list.Add(((overrideBaseLineText != null) ? overrideBaseLineText.ToString() : BaseLine.Value.Name, BaseLine.Value.Number));
			}
			foreach (ExplanationLine line in Lines)
			{
				float num = line.Number;
				if (line.OperationType == OperationType.Multiply)
				{
					num = baseNumber * num * 0.01f;
				}
				list.Add((line.Name, num));
			}
			if (LimitMinLine.HasValue && LimitMinLine.Value.Number > unclampedResultNumber)
			{
				list.Add(((overrideMinimumLineText != null) ? overrideMinimumLineText.ToString() : LimitMinLine.Value.Name, LimitMinLine.Value.Number - unclampedResultNumber));
			}
			if (LimitMaxLine.HasValue && LimitMaxLine.Value.Number < unclampedResultNumber)
			{
				list.Add(((overrideMaximumLineText != null) ? overrideMaximumLineText.ToString() : LimitMaxLine.Value.Name, LimitMaxLine.Value.Number - unclampedResultNumber));
			}
			return list;
		}

		public void AddLine(string name, float number, OperationType opType)
		{
			ExplanationLine explanationLine = new ExplanationLine(name, number, opType);
			switch (opType)
			{
			case OperationType.Add:
			case OperationType.Multiply:
			{
				int num = -1;
				for (int i = 0; i < Lines.Count; i++)
				{
					if (Lines[i].Name.Equals(name) && Lines[i].OperationType == opType)
					{
						num = i;
						break;
					}
				}
				if (num < 0)
				{
					Lines.Add(explanationLine);
					break;
				}
				explanationLine = new ExplanationLine(name, number + Lines[num].Number, opType);
				Lines[num] = explanationLine;
				break;
			}
			case OperationType.Base:
				BaseLine = explanationLine;
				break;
			case OperationType.LimitMin:
				LimitMinLine = explanationLine;
				break;
			case OperationType.LimitMax:
				LimitMaxLine = explanationLine;
				break;
			}
		}
	}

	private static readonly TextObject LimitMinText = new TextObject("{=GNalaRaN}Minimum");

	private static readonly TextObject LimitMaxText = new TextObject("{=cfjTtxWv}Maximum");

	private static readonly TextObject BaseText = new TextObject("{=basevalue}Base");

	private float? _limitMinValue;

	private float? _limitMaxValue;

	private StatExplainer _explainer;

	public float ResultNumber => MathF.Clamp(_unclampedResultNumber, LimitMinValue, LimitMaxValue);

	public int RoundedResultNumber => MathF.Round(ResultNumber);

	public float BaseNumber { get; private set; }

	public bool IncludeDescriptions => _explainer != null;

	public float LimitMinValue
	{
		get
		{
			if (!_limitMinValue.HasValue)
			{
				return float.MinValue;
			}
			return _limitMinValue.Value;
		}
	}

	public float LimitMaxValue
	{
		get
		{
			if (!_limitMaxValue.HasValue)
			{
				return float.MaxValue;
			}
			return _limitMaxValue.Value;
		}
	}

	public float SumOfFactors { get; private set; }

	private float _unclampedResultNumber => BaseNumber + BaseNumber * SumOfFactors;

	public ExplainedNumber(float baseNumber = 0f, bool includeDescriptions = false, TextObject baseText = null)
	{
		BaseNumber = baseNumber;
		_explainer = (includeDescriptions ? new StatExplainer() : null);
		SumOfFactors = 0f;
		_limitMinValue = float.MinValue;
		_limitMaxValue = float.MaxValue;
		if (_explainer != null && !BaseNumber.ApproximatelyEqualsTo(0f))
		{
			_explainer.AddLine((baseText ?? BaseText).ToString(), BaseNumber, StatExplainer.OperationType.Base);
		}
	}

	public string GetExplanations()
	{
		if (_explainer == null)
		{
			return "";
		}
		MBStringBuilder mBStringBuilder = default(MBStringBuilder);
		mBStringBuilder.Initialize(16, "GetExplanations");
		foreach (var line in _explainer.GetLines(BaseNumber, _unclampedResultNumber))
		{
			string value = string.Format("{0} : {1}{2:0.##}\n", line.name, (line.number > 0.001f) ? "+" : "", line.number);
			mBStringBuilder.Append(value);
		}
		return mBStringBuilder.ToStringAndRelease();
	}

	public List<(string name, float number)> GetLines()
	{
		if (_explainer == null)
		{
			return new List<(string, float)>();
		}
		return _explainer.GetLines(BaseNumber, _unclampedResultNumber);
	}

	public void AddFromExplainedNumber(ExplainedNumber explainedNumber, TextObject baseText)
	{
		if (explainedNumber._explainer != null && _explainer != null)
		{
			TextObject textObject = new TextObject("{=HKoLNyIm}{BASE} Maximum");
			TextObject textObject2 = new TextObject("{=0Fliz2vk}{BASE} Minimum");
			textObject.SetTextVariable("BASE", baseText);
			textObject2.SetTextVariable("BASE", baseText);
			foreach (var line in explainedNumber._explainer.GetLines(explainedNumber.BaseNumber, explainedNumber._unclampedResultNumber, baseText, textObject, textObject2))
			{
				_explainer.AddLine(line.name, line.number, StatExplainer.OperationType.Add);
			}
		}
		BaseNumber += explainedNumber.ResultNumber;
	}

	public void SubtractFromExplainedNumber(ExplainedNumber explainedNumber, TextObject baseText)
	{
		if (explainedNumber._explainer != null && _explainer != null)
		{
			TextObject textObject = new TextObject("{=HKoLNyIm}{BASE} Maximum");
			TextObject textObject2 = new TextObject("{=0Fliz2vk}{BASE} Minimum");
			textObject.SetTextVariable("BASE", baseText);
			textObject2.SetTextVariable("BASE", baseText);
			foreach (var line in explainedNumber._explainer.GetLines(explainedNumber.BaseNumber, explainedNumber._unclampedResultNumber, baseText, textObject, textObject2))
			{
				_explainer.AddLine(line.name, 0f - line.number, StatExplainer.OperationType.Add);
			}
		}
		BaseNumber -= explainedNumber.ResultNumber;
	}

	public void Add(float value, TextObject description = null, TextObject variable = null)
	{
		if (value.ApproximatelyEqualsTo(0f))
		{
			return;
		}
		BaseNumber += value;
		if (_explainer != null && description != null && !value.ApproximatelyEqualsTo(0f))
		{
			if (variable != null)
			{
				description.SetTextVariable("A0", variable);
			}
			_explainer.AddLine(description.ToString(), value, StatExplainer.OperationType.Add);
		}
	}

	public void AddFactor(float value, TextObject description = null)
	{
		if (!value.ApproximatelyEqualsTo(0f))
		{
			SumOfFactors += value;
			if (description != null && _explainer != null && !value.ApproximatelyEqualsTo(0f))
			{
				_explainer.AddLine(description.ToString(), MathF.Round(value, 3) * 100f, StatExplainer.OperationType.Multiply);
			}
		}
	}

	public void LimitMin(float minValue)
	{
		_limitMinValue = minValue;
		if (_explainer != null)
		{
			_explainer.AddLine(LimitMinText.ToString(), minValue, StatExplainer.OperationType.LimitMin);
		}
	}

	public void LimitMax(float maxValue, TextObject description = null)
	{
		_limitMaxValue = maxValue;
		if (_explainer != null)
		{
			_explainer.AddLine((description ?? LimitMaxText).ToString(), maxValue, StatExplainer.OperationType.LimitMax);
		}
	}

	public void Clamp(float minValue, float maxValue)
	{
		LimitMin(minValue);
		LimitMax(maxValue);
	}
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -Include '*.xml','*.xscene','*.json','*.txt','*.xslt' -ErrorAction SilentlyContinue | Select-String -Pattern 'TerrainType|terrain_type|Snow|snow|terrain' | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Include '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'GetFaceTerrainType' | Select-Object -First 50 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/_Module' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'map|scene|terrain|face' } | Select-Object -First 100 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'MapSceneWrapper|IMapScene'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 548ms:

[32;1mFullName                                                                                                          [0m[32;1m Leng[0m
[32;1m                                                                                                                  [0m[32;1m   th[0m
[32;1m--------                                                                                                          [0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\Assets\main_map_textures\taom_gui_map_circle_atlas_tex.tpac           593
C:\Users\mikew\source\repos\TAOM\Main\_Module\Assets\main_map_textures\taom_map_circle_decal_mtl.tpac               449
C:\Users\mikew\source\repos\TAOM\Main\_Module\AssetSources\main_map_textures\taom_gui_map_circle_atlas.psd         …653
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Brushes\FaceGen.xml                                              …946
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Brushes\FactionMap.xml                                           7600
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Brushes\MapBar.xml                                               …335
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Prefabs\MomentumView\MomentumMapIndicator.xml                    3252
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_map_boundary.png                    …861
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_map_boundary_full.png       …451
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\ui_factionmap\ui_factionmap.xml                       1116
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_banner_choose_update.png       7387
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_canvas.png       6233
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_daynight.png     …175
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_frame.png        …607
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_glow_overlay.png 1792
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame_silhouette.png    1466
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame.png               …293
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon1.png                      2499
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon2.png                      4414
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon3.png                      4569
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon4.png                      5446
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon5.png                      4100
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon6.png                      4274
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon7.png                      4797
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_canvas.png                 569
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame_silhouette.png      1544
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame.png                 …000
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame_silhouette.png     2031
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame.png                …433
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                  3402
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                      9494
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleSounds\LOTR\Dwarf\dwarf_facedirection.mp3                      …048
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleSounds\LOTR\Dwarf\dwarf_facenemy.mp3                           …407


 succeeded in 773ms:
 succeeded in 822ms:

[32;1mPath                                                                                                             [0m[32;1m LineN[0m
[32;1m                                                                                                                 [0m[32;1m umber[0m
[32;1m----                                                                                                             [0m [32;1m-----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml                     2490
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\adulthood_menu.json                     1140
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                          679
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                          991
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\heroes.xml                                      501
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\heroes.xml                                      549
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\heroes.xml                                      593
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\factionmap\factions.json                                   364
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\factionmap\factions.json                                   786
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\factionmap\factions.json                                  1100
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml             55
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1145
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1147
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1379
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1411
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1615
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1729
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           1887
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_career_strings_fre-FR.xml           2073
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                651
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                663
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                711
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                751
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                755
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                867
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                955
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml                995
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1011
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1031
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1035
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1155
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1291
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1379
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1503
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_cc_strings_fre-FR.xml               1547
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_module_strings_fre-FR.xml           1189
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_named_companion_strings_fre-FR.xml    63
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_named_companion_strings_fre-FR.xml   147
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_named_companion_strings_fre-FR.xml   183
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_wanderer_strings_fre-FR.xml          103
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_wanderer_strings_fre-FR.xml          105
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_wanderer_strings_fre-FR.xml          797
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_wanderer_strings_fre-FR.xml          805
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\FR\std_taom_xslt_strings_fre-FR.xml             1203
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_career_strings_pol-PL.xml           1411
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_cc_strings_pol-PL.xml                651
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_cc_strings_pol-PL.xml                755
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_cc_strings_pol-PL.xml               1503
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_module_strings_pol-PL.xml           1189
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_named_companion_strings_pol-PL.xml   183
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\Languages\PL\std_taom_xslt_strings_pol-PL.xml             1203
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\named_companions\named_companion_strings.xml               171
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                     7
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    12
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    17
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    22
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    27
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    32
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    37
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    42
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    47
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    52
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    58
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    63
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    68
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    73
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    78
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    83
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    88
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    94
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                    99
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml                                   104
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          409
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          441
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          442
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          443
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          444
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml                                          445
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml                                           6733
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml                                           6783
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml                                           6984
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml                                           7023
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                         7
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                         8
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        12
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        13
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        16
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        17
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        19
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        22
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        23
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        25
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        26
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        28
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        31
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        32
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        34
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        35
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        37
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        40
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        41
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        43
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        44
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        46
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        49
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        50
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        52
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        53
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        55
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        58
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        59
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        62
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        63
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        65
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        68
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        69
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        72
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        73
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        75
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml                                        78


 succeeded in 27383ms:

[32;1mPath[0m
[32;1m----                                                                                                                   [0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs                                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs                                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\NavigationHelper.cs                                
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\NavigationHelper.cs                                
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\NavigationHelper.cs                                
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\NavigationHelper.cs                                
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\NavigationHelper.cs                                
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignVec2.cs                  
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\FindingItemOnM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\MobilePartyTra…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCombatSimu…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanc…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanc…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanc…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanc…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapTrackMo…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapVisibil…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapVisibil…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobilePart…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeed…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map\IMapScene.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs             
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs             
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs             
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobilePartyAi.cs           
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobilePartyAi.cs           
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobilePartyAi.cs           
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs                                                           
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs                                                           
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs                                                           
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs                                                           
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs                                                    
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs                                                    
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs                                                    
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\StoryModeMissions.cs                                     


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Map\\IMapScene.cs' | ForEach-Object { "'$i=0; Get-Content $_.FullName | ForEach-Object { $i++; '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Modules\\SandBox\\Sandbox\\MapScene.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'GetFaceTerrainType|TerrainType|GetNavigationMeshFace' -or ("'$i -ge 1 -and $i -le 40)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter 'SandBox.dll' -ErrorAction SilentlyContinue | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Extension -in @('"'.xscene','.xml','.json','.txt') } | Select-String -Pattern 'terrain_type|TerrainType|Snow|snow' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
 succeeded in 533ms:
    1: using System.Collections.Generic;
    2: using TaleWorlds.CampaignSystem.Settlements;
    3: using TaleWorlds.Core;
    4: using TaleWorlds.Library;
    5: 
    6: namespace TaleWorlds.CampaignSystem.Map;
    7: 
    8: public interface IMapScene
    9: {
   10: 	void Load();
   11: 
   12: 	void AfterLoad();
   13: 
   14: 	void Destroy();
   15: 
   16: 	PathFaceRecord GetFaceIndex(in CampaignVec2 vec2);
   17: 
   18: 	TerrainType GetTerrainTypeAtPosition(in CampaignVec2 vec2);
   19: 
   20: 	List<TerrainType> GetEnvironmentTerrainTypes(in CampaignVec2 vec2);
   21: 
   22: 	List<TerrainType> GetEnvironmentTerrainTypesCount(in CampaignVec2 vec2, out TerrainType currentPositionTerrainType);
   23: 
   24: 	MapPatchData GetMapPatchAtPosition(in CampaignVec2 position);
   25: 
   26: 	TerrainType GetFaceTerrainType(PathFaceRecord faceIndex);
   27: 
   28: 	CampaignVec2 GetNearestFaceCenterForPosition(in CampaignVec2 vec2, int[] excludedFaceIds);
   29: 
   30: 	CampaignVec2 GetNearestFaceCenterForPositionWithPath(PathFaceRecord pathFaceRecord, bool targetIsLand, float maxDist, int[] excludedFaceIds);
   31: 
   32: 	CampaignVec2 GetAccessiblePointNearPosition(in CampaignVec2 vec2, float radius);
   33: 
   34: 	bool GetPathBetweenAIFaces(PathFaceRecord startingFace, PathFaceRecord endingFace, Vec2 startingPosition, Vec2 endingPosition, float agentRadius, NavigationPath path, int[] excludedFaceIds, float extraCostMultiplier, int regionSwitchCostFromLandToSea, int regionSwitchCostFromSeaToLand);
   35: 
   36: 	bool GetPathDistanceBetweenAIFaces(PathFaceRecord startingAiFace, PathFaceRecord endingAiFace, Vec2 startingPosition, Vec2 endingPosition, float agentRadius, float distanceLimit, out float distance, int[] excludedFaceIds, int regionSwitchCostFromLandToSea, int regionSwitchCostFromSeaToLand);
   37: 
   38: 	bool IsLineToPointClear(PathFaceRecord startingFace, Vec2 position, Vec2 destination, float agentRadius);
   39: 
   40: 	Vec2 GetLastPointOnNavigationMeshFromPositionToDestination(PathFaceRecord startingFace, Vec2 position, Vec2 destination, int[] excludedFaceIds = null);
   41: 
   42: 	Vec2 GetLastPositionOnNavMeshFaceForPointAndDirection(PathFaceRecord startingFace, Vec2 position, Vec2 destination);
   43: 
   44: 	Vec2 GetNavigationMeshCenterPosition(PathFaceRecord face);
   45: 
   46: 	Vec2 GetNavigationMeshCenterPosition(int faceIndex);
   47: 
   48: 	PathFaceRecord GetFaceAtIndex(int faceIndex);
   49: 
   50: 	int GetNumberOfNavigationMeshFaces();
   51: 
   52: 	bool GetHeightAtPoint(in CampaignVec2 point, ref float height);
   53: 
   54: 	float GetWinterTimeFactor();
   55: 
   56: 	void GetTerrainHeightAndNormal(Vec2 position, out float height, out Vec3 normal);
   57: 
   58: 	float GetFaceVertexZ(PathFaceRecord navMeshFace);
   59: 
   60: 	Vec3 GetGroundNormal(Vec2 position);
   61: 
   62: 	void GetSiegeCampFrames(Settlement settlement, out List<MatrixFrame> siegeCamp1GlobalFrames, out List<MatrixFrame> siegeCamp2GlobalFrames);
   63: 
   64: 	string GetTerrainTypeName(TerrainType type);
   65: 
   66: 	Vec2 GetTerrainSize();
   67: 
   68: 	uint GetSceneLevel(string name);
   69: 
   70: 	void SetSceneLevels(List<string> levels);
   71: 
   72: 	List<AtmosphereState> GetAtmosphereStates();
   73: 
   74: 	void SetAtmosphereColorgrade(TerrainType terrainType);
   75: 
   76: 	void AddNewEntityToMapScene(string entityId, in CampaignVec2 position);
   77: 
   78: 	void GetMapBorders(out Vec2 minimumPosition, out Vec2 maximumPosition, out float maximumHeight);
   79: 
   80: 	uint GetSceneXmlCrc();
   81: 
   82: 	uint GetSceneNavigationMeshCrc();
   83: 
   84: 	float GetSnowAmountAtPosition(Vec2 position);
   85: 
   86: 	float GetRainAmountAtPosition(Vec2 position);
   87: }

 succeeded in 572ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.IO;
    4: using System.Threading;
    5: using TaleWorlds.CampaignSystem;
    6: using TaleWorlds.CampaignSystem.Map;
    7: using TaleWorlds.CampaignSystem.Party;
    8: using TaleWorlds.CampaignSystem.Settlements;
    9: using TaleWorlds.Core;
   10: using TaleWorlds.DotNet;
   11: using TaleWorlds.Engine;
   12: using TaleWorlds.Library;
   13: using TaleWorlds.ModuleManager;
   14: using TaleWorlds.MountAndBlade;
   15: 
   16: namespace SandBox;
   17: 
   18: public class MapScene : IMapScene
   19: {
   20: 	private int _snowAndRainDataTextureWidth;
   21: 
   22: 	private int _snowAndRainDataTextureHeight;
   23: 
   24: 	public const int FlowMapTextureDimension = 512;
   25: 
   26: 	private const string MapCampArea1Tag = "map_camp_area_1";
   27: 
   28: 	private const string MapCampArea2Tag = "map_camp_area_2";
   29: 
   30: 	private Scene _scene;
   31: 
   32: 	private MBAgentRendererSceneController _agentRendererSceneController;
   33: 
   34: 	private byte[] _snowAndRainData;
   35: 
   36: 	private float[] _windFlowMapData;
   37: 
   38: 	private Vec2 _minimumPositionCache;
   39: 
   40: 	private Vec2 _maximumPositionCache;
  140: 	public void SetAtmosphereColorgrade(TerrainType terrainType)
  291: 		int[] invalidTerrainTypesForNavigationType = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType((NavigationType)3);
  292: 		foreach (int num in invalidTerrainTypesForNavigationType)
  317: 	public TerrainType GetTerrainTypeAtPosition(in CampaignVec2 position)
  327: 		return GetFaceTerrainType(face);
  330: 	public TerrainType GetFaceTerrainType(PathFaceRecord navMeshFace)
  335: 			Debug.FailedAssert("Null nav mesh face tried to get terrain type.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\SandBox\\MapScene.cs", "GetFaceTerrainType", 338);
  336: 			return (TerrainType)1;
  338: 		return (TerrainType)navMeshFace.FaceGroupIndex;
  361: 	public List<TerrainType> GetEnvironmentTerrainTypes(in CampaignVec2 originPosition)
  375: 		List<TerrainType> list = new List<TerrainType>();
  379: 		list.Add(GetTerrainTypeAtPosition(in position));
  386: 				TerrainType terrainTypeAtPosition = GetTerrainTypeAtPosition(in position);
  387: 				if (!list.Contains(terrainTypeAtPosition))
  389: 					list.Add(terrainTypeAtPosition);
  396: 	public List<TerrainType> GetEnvironmentTerrainTypesCount(in CampaignVec2 originPosition, out TerrainType currentPositionTerrainType)
  413: 		List<TerrainType> list = new List<TerrainType>();
  417: 		currentPositionTerrainType = (TerrainType)(int)GetTerrainTypeAtPosition(in position);
  418: 		list.Add(currentPositionTerrainType);
  428: 					TerrainType faceTerrainType = GetFaceTerrainType(((CampaignVec2)(ref position)).Face);
  429: 					list.Add(faceTerrainType);
  632: 	public string GetTerrainTypeName(TerrainType type)
  744: 	TerrainType IMapScene.GetTerrainTypeAtPosition(in CampaignVec2 vec2)
  747: 		return GetTerrainTypeAtPosition(in vec2);
  750: 	List<TerrainType> IMapScene.GetEnvironmentTerrainTypes(in CampaignVec2 vec2)
  752: 		return GetEnvironmentTerrainTypes(in vec2);
  755: 	List<TerrainType> IMapScene.GetEnvironmentTerrainTypesCount(in CampaignVec2 vec2, out TerrainType currentPositionTerrainType)
  757: 		return GetEnvironmentTerrainTypesCount(in vec2, out currentPositionTerrainType);

 succeeded in 777ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:409: <module_sound name="LOTR/Monsters/Warg/footsteps_snow" sound_category="mission_horse_gallop" min_pitch_multiplier="0.8" max_pitch_multiplier="0.9">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:441: <module_sound name="LOTR/Monsters/Warg/run_snows" sound_category="mission_horse_gallop" min_pitch_multiplier="0.9" max_pitch_multiplier="1.1">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:442: <variation path="LOTR/Monsters/Warg/run_snow_1.wav" weight="1" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:443: <variation path="LOTR/Monsters/Warg/run_snow_2.wav" weight="1" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:444: <variation path="LOTR/Monsters/Warg/run_snow_3.wav" weight="1" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\module_sounds.xml:445: <variation path="LOTR/Monsters/Warg/run_snow_4.wav" weight="1" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:6733: <Settlement id="castle_village_K4_2" name="{=Settlements.Settlement.name.castle_village_K4_2}Kuruluk" posX="1458.36" posY="567.591" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_3}Kuruluk sits alongside the Zakhan, a tributary of the Karakaz river. Though this valley is snowbound for much of the year, steppe ponies thrive on the spring grasses and in the winter dig through the snow for food, their thick coats protecting them from the icy blasts of wind.">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:6783: <Settlement id="castle_village_K5_1" name="{=Settlements.Settlement.name.castle_village_K5_1}Khimli" posX="1444.797" posY="420.991" culture="Culture.khuzait" text="{=Settlements.Settlement.text.castle_village_K5_1}Khimli sits in a basin the Devseg plateau. Though this land is dry most of the year, it experiences winter snowfalls and muddy springs. Fine clay can be collected from the pools left by the snowmelt and the rains.">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:6984: <Settlement id="village_K1_1" name="{=Settlements.Settlement.name.village_K1_1}Fisnar" posX="1283.389" posY="426.472" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_1}Fisnar sits alongside the Zakhan, a tributary of the Karakaz river. Snow-bound for much of the year, the spring grasses nonetheless sustain sizeable herds of sheep, the thick wool of which protects them from the bitter winter cold.">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlements.xml:7023: <Settlement id="village_K1_4" name="{=Settlements.Settlement.name.village_K1_4}Asalig" posX="1424.063" posY="447.955" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_4}Asalig sits at the foot of Bars Dag, one of the peaks that marks the edge of the Devseg plateau. Villagers raise horses in the lower slopes, keeping watch for the snow leopards for which the peak is named.">
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:16: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:17: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:19: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:25: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:26: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:28: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:34: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:35: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:37: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:43: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:44: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:46: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:52: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:53: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:55: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:62: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:63: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:65: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:72: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:73: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:75: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:82: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:83: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:85: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:92: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:93: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:95: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:106: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:107: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:109: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:125: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:126: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:128: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:130: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:137: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:138: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:140: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:147: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:148: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:150: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:157: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:158: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:160: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:162: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:168: <TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:169: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:171: <TerrainType
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml:173: </TerrainTypes>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:227: <feat id="taom_erebor_snow_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml:2044: <feat id="taom_gundabad_snow_speed" />

 succeeded in 893ms:

[32;1mFullName                                                                                                    [0m[32;1m  Length[0m[32;1m La[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m st[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m Wr[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m it[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m eT[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m im[0m
[32;1m                                                                                                            [0m[32;1m        [0m[32;1m e[0m
[32;1m--------                                                                                                    [0m [32;1m ------[0m [32;1m--[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\bin\Win64_Shipping_Client\SandBox.dll  1324992 5…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\bin\Win64_Shipping_wEditor\SandBox.dll 1404352 5…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Modules\\SandBox\\Sandbox\\MapScene.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 317 -and $i -le 338) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t SandBox.MapScene 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\bin\\Win64_Shipping_Client\\SandBox.dll' | Select-String -Pattern 'GetTerrainTypeAtPosition|GetFaceTerrainType|FaceGroupIndex|SnowAmount' -Context 3,5" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'main_map|map' -or "'$_.Extension -in @('"'.xscene') } | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module\\SceneObj' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 100 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 545ms:
  317: 	public TerrainType GetTerrainTypeAtPosition(in CampaignVec2 position)
  318: 	{
  319: 		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
  320: 		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
  321: 		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
  322: 		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
  323: 		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
  324: 		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
  325: 		CampaignVec2 val = position;
  326: 		PathFaceRecord face = ((CampaignVec2)(ref val)).Face;
  327: 		return GetFaceTerrainType(face);
  328: 	}
  329: 
  330: 	public TerrainType GetFaceTerrainType(PathFaceRecord navMeshFace)
  331: 	{
  332: 		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
  333: 		if (!((PathFaceRecord)(ref navMeshFace)).IsValid())
  334: 		{
  335: 			Debug.FailedAssert("Null nav mesh face tried to get terrain type.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\SandBox\\MapScene.cs", "GetFaceTerrainType", 338);
  336: 			return (TerrainType)1;
  337: 		}
  338: 		return (TerrainType)navMeshFace.FaceGroupIndex;

 succeeded in 581ms:

[32;1mFullName                                                                                                          [0m[32;1m Leng[0m
[32;1m                                                                                                                  [0m[32;1m   th[0m
[32;1m--------                                                                                                          [0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\Assets\main_map_textures\taom_gui_map_circle_atlas_tex.tpac           593
C:\Users\mikew\source\repos\TAOM\Main\_Module\Assets\main_map_textures\taom_map_circle_decal_mtl.tpac               449
C:\Users\mikew\source\repos\TAOM\Main\_Module\AssetSources\main_map_textures\taom_gui_map_circle_atlas.psd         …653
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Brushes\FactionMap.xml                                           7600
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Brushes\MapBar.xml                                               …335
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\Prefabs\MomentumView\MomentumMapIndicator.xml                    3252
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_map_boundary.png                    …861
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_map_boundary_full.png       …451
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\ui_factionmap\ui_factionmap.xml                       1116
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_banner_choose_update.png       7387
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_canvas.png       6233
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_daynight.png     …175
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_frame.png        …607
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle_glow_overlay.png 1792
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame_silhouette.png    1466
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame.png               …293
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon1.png                      2499
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon2.png                      4414
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon3.png                      4569
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon4.png                      5446
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon5.png                      4100
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon6.png                      4274
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon7.png                      4797
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_canvas.png                 569
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame_silhouette.png      1544
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame.png                 …000
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame_silhouette.png     2031
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame.png                …433


 succeeded in 622ms:
 succeeded in 1237ms:

  		MBMapScene.LoadAtmosphereData(mapScene);
  	}
  
> 	public TerrainType [7mGetTerrainTypeAtPosition[0m(in CampaignVec2 position)[0m
[7m[0m  	{[0m
[7m[0m  		//IL_0001: Unknown result type (might be due to invalid IL or missing references)[0m
[7m[0m  		//IL_0006: Unknown result type (might be due to invalid IL or missing references)[0m
[7m[0m  		//IL_0009: Unknown result type (might be due to invalid IL or missing references)[0m
[7m[0m  		//IL_000e: Unknown result type (might be due to invalid IL or missing references)[0m
  		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
  		CampaignVec2 val = position;
  		PathFaceRecord face = ((CampaignVec2)(ref val)).Face;
> 		return [7mGetFaceTerrainType[0m(face);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public TerrainType [7mGetFaceTerrainType[0m(PathFaceRecord navMeshFace)[0m
[7m[0m  	{[0m
[7m[0m  		//IL_0024: Unknown result type (might be due to invalid IL or missing references)[0m
[7m[0m  		if (!((PathFaceRecord)(ref navMeshFace)).IsValid())[0m
[7m[0m  		{[0m
> 			Debug.FailedAssert("Null nav mesh face tried to get terrain type.", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\SandBox\\MapScene.cs", "[7mGetFaceTerrainType[0m", [0m[7m[0m338);[0m
[7m[0m  			return (TerrainType)1;[0m
[7m[0m  		}[0m
> 		return (TerrainType)navMeshFace.[7mFaceGroupIndex[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public CampaignVec2 GetNearestFaceCenterForPosition(in CampaignVec2 position, int[] excludedFaceIds)[0m
[7m[0m  	{[0m
[7m[0m  		//IL_0007: Unknown result type (might be due to invalid IL or missing references)[0m
  		Vec2 val = default(Vec2);
  		((Vec2)(ref val))..ctor(1f, 0f);
  		CampaignVec2 position = originPosition;
> 		list.Add([7mGetTerrainTypeAtPosition[0m(in position));[0m
[7m[0m  		for (int i = 0; i < 8; i++)[0m
[7m[0m  		{[0m
[7m[0m  			((Vec2)(ref val)).RotateCCW(MathF.PI / 4f * (float)i);[0m
[7m[0m  			for (int j = 1; j < 7; j++)[0m
[7m[0m  			{[0m
  				position += (float)j * val;
> 				TerrainType terrainTypeAtPosition = [7mGetTerrainTypeAtPosition[0m(in position);[0m
[7m[0m  				if (!list.Contains(terrainTypeAtPosition))[0m
[7m[0m  				{[0m
[7m[0m  					list.Add(terrainTypeAtPosition);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  		Vec2 val = default(Vec2);
  		((Vec2)(ref val))..ctor(1f, 0f);
  		CampaignVec2 position = originPosition;
> 		currentPositionTerrainType = (TerrainType)(int)[7mGetTerrainTypeAtPosition[0m(in position);[0m
[7m[0m  		list.Add(currentPositionTerrainType);[0m
[7m[0m  		for (int i = 0; i < 8; i++)[0m
[7m[0m  		{[0m
[7m[0m  			((Vec2)(ref val)).RotateCCW(MathF.PI / 4f * (float)i);[0m
[7m[0m  			for (int j = 1; j < 7; j++)[0m
  				PathFaceRecord face = ((CampaignVec2)(ref position)).Face;
  				if (((PathFaceRecord)(ref face)).IsValid())
  				{
> 					TerrainType faceTerrainType = [7mGetFaceTerrainType[0m(((CampaignVec2)(ref position)).Face);[0m
[7m[0m  					list.Add(faceTerrainType);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return list;[0m
  		return new Vec2(_windFlowMapData[textureDataIndexForPosition * 2], _windFlowMapData[textureDataIndexForPosition * 
2 + 1]);
  	}
  
> 	public float Get[7mSnowAmount[0mAtPosition(Vec2 position)[0m
[7m[0m  	{[0m
[7m[0m  		//IL_0001: Unknown result type (might be due to invalid IL or missing references)[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mint [0m[7m[0mtextureDataIndexForPosition [0m[7m[0m= [0m[7m[0mGetTextureDataIndexForPosition(position, [0m[7m[0m_snowAndRainDataTextureWidth, [0m
[7m[0m_snowAndRainDataTextureHeight);[0m
[7m[0m  		return (int)_snowAndRainData[textureDataIndexForPosition * 2];[0m
[7m[0m  	}[0m
  		return GetFaceIndex(in vec2);
  	}
  
> 	TerrainType IMapScene.[7mGetTerrainTypeAtPosition[0m(in CampaignVec2 vec2)[0m
[7m[0m  	{[0m
[7m[0m  		//IL_0002: Unknown result type (might be due to invalid IL or missing references)[0m
> 		return [7mGetTerrainTypeAtPosition[0m(in vec2);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	List<TerrainType> IMapScene.GetEnvironmentTerrainTypes(in CampaignVec2 vec2)[0m
[7m[0m  	{[0m
[7m[0m  		return GetEnvironmentTerrainTypes(in vec2);[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module' -Force | Select-Object Name,Mode,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'main_map|map_scene|SceneObj' -or "'$_.Extension -eq '"'.xscene' } | Select-Object -First 200 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'C:\\Users\\mikew\\source\\repos\\TAOM' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Extension -in @('"'.xscene','.scene','.navmesh','.bin','.xml') -and ("'$_.FullName -match '"'main_map|Main_map|map_scene|MapScene|scene') } | Select-Object -First 200 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module' -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'Scene|scene|map|Map' } | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 507ms:

[32;1mName            [0m[32;1m Mode [0m[32;1m Length[0m
[32;1m----            [0m [32;1m---- [0m [32;1m------[0m
Assets           d---- 
AssetSources     d---- 
bin              d---- 
GUI              d---- 
ModuleData       d---- 
ModuleSounds     d---- 
RuntimeDataCache d---- 
SubModule.xml    -a--- 29374


 succeeded in 544ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\Assets\main_map_textures
C:\Users\mikew\source\repos\TAOM\Main\_Module\AssetSources\main_map_textures
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\ui_factionmap
C:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom\MapBar
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\factionmap


 succeeded in 675ms:

[32;1mFullName                                                                         [0m[32;1m Length[0m
[32;1m--------                                                                         [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml   3402
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\sp_battle_scenes.xml       9494


 succeeded in 899ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\evil_scene\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\main_menu_gondor_a\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\main_menu_gondor_b\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\main_menu_gondor_lords\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\main_menu_irondwarf_a\s…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\Backups\main_menu_rohan_a\scene…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\evil_scene\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_a\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_b\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_gondor_a\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_gondor_b\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_gondor_lords\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_irondwarf_a\scene.xsc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\main_menu_rohan_a\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SceneObj\temp_scene_save_folder_evil_sce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\EmAssetPackages\main_map_cliff\main_map_cliff.tpac
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\EmAssetPackages\main_map_cube_maps\main_map_cube_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\EmAssetPackages\main_map_outer_mesh\main_map_oute…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\EmAssetPackages\main_map_textures\main_map_textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\EmAssetPackages\terrain_textures\rock\main_map_mo…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\benchmark_battle_11\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\braidentest\scene.xscene                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\character_developer_scene\scene.xscene   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\character_menu_a\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\character_menu_c\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\character_menu_new\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\flora_editor_scene\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\ibl_shop\scene.xscene                    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\inventory_character_scene\scene.xscene   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\main_menu_a\scene.xscene                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_battle_map_001\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_battle_map_002\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_battle_map_003\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_compact\scene.xscene                  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_duel_001\scene.xscene                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_duel_002\scene.xscene                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_duel_mode_map_004\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_duel_mode_map_004_night\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_duel_mode_map_004_w\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_001\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_005\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_007\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_008\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_009\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_010\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_011\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_011s\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_011_rw\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_011_rw_winter\scene.xsce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_012\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_013\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_018\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_018_w\scene.xscene       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_sergeant_map_vlandia_01\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_003\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_004\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_005\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_005_winter\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_006\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_006_vlandia\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_007_battania\scene.xscene   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_007_battania_w\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_007_winter\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_009\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_010\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_011_b\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_012\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_siege_map_012_w\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_002f\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_002_winter\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_003_skinc\scene.xscene   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_004\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_005\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_006\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_006_nowater\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_007\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_007_winter\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_008\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_008_skin\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_009\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_010\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_010_winter\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_013\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_014\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_020\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_battania_02\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_map_battania_03\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_skirmish_spawn_test\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_001\scene.xscene              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_001_spring\scene.xscene       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_003\scene.xscene              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_004\scene.xscene              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_004a\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_004a_w\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\mp_tdm_map_004a_winter\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\rain\scene.xscene                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\scn_character_creation_scene\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\scn_conversation_tableau\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\scn_item_tableau\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SceneObj\scn_soldier\scene.xscene                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\act_1_alley_scene\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\arena_nord_a\scene.xscene              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\aserai_shipyard\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battania_shipyard\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_01\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_011\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_013\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_02\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_021\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_022\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_03\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_04\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_07\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_caspian_sea_02\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_coastal_south_europe_02…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_opensea_northern\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\battle_terrain_opensea_southern\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\coastal_naval_battle_scene\scene.xscene
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\coastal_terrain_north_of_the_north_sea…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\coastal_terrain_north_of_the_north_sea…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\coastal_terrain_south_of_the_north_sea…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\conversation_scene_sea\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\conversation_scene_sea_multi_agent\sce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\cutscene_saving_sister\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\empire_shipyard\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\khuzait_shipyard\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\Main_map\scene.xscene                  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_1_phase_03\scene.x…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_2_phase_01\scene.x…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_2_tutorial\scene.x…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_3_quest_1\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_3_quest_2\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_3_quest_3\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_3_quest_4\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\naval_storyline_act_3_quest_5\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_a\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_b\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_c\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_d\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_e\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_f\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_g\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_h\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_i\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_j\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_k\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_l\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_n\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_o\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_p\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_battle_terrain_r\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_castle_a\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_castle_b\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_castle_c\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_dungeon_stealth\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_keep_interior_lv1\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_keep_interior_lv2\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_keep_interior_lv3\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_shipyard\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_tavern\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_town_a\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_town_b\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_town_c\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_town_d\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_a\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_b\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_c\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_d\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_e\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_e_navalraid\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_f\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\nord_village_f_raid\scene.xscene       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\prototype_port_scene\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\prototype_port_scene_wide\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_aserai_01_4x4km\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_aserai_02_4x4km\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_empireEast_01_4x4km\scene.xsc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_empirewest_01_4x4km\scene.xsc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_nord_01_4x4km\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_nord_02_4x4km\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_vlandia_01_4x4km\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\river_bt_vlandia_02_4x4km\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\scn_cutscene_main_hero_naval_battle_de…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\scn_port\scene.xscene                  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\scn_ship_piece_tableau\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\scn_ship_tableau\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\storyline_shipyard_alley\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\sturgia_shipyard\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\SceneObj\vlandia_shipyard\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arabian_house_new_a_interior_a_house\sc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_aserai_a\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_battania_a\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_empire_a\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_khuzait_a\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_sturgia_a\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\arena_vlandia_a\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\aserai_castle_keep_a_l1_interior\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\aserai_castle_keep_a_l2_interior\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\aserai_castle_keep_a_l3_interior\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\SceneObj\aserai_dungeon_a\scene.xscene           


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Directory | Where-Object { "'$_.Name -match '"'TAOM|Age|Men|LOTR|LOME' } | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Directory | Select-Object Name" in C:\Users\mikew\source\repos\TAOM
 succeeded in 484ms:

[32;1mName[0m
[32;1m----[0m
ADOD_Beasts
Alliance.Wargs
Bannerlord.ButterLib
Bannerlord.Harmony
Bannerlord.MBOptionScreen
Bannerlord.UIExtenderEx
BattleLinkMPClient
BirthAndDeath
CustomBattle
FastMode
LOTRLOME_Armory
Multiplayer
Native
NavalDLC
Palantir.Debugger
SandBox
SandBoxCore
SandBoxCoreMP
StoryMode
TAOM
TAOM_Map
TAOM_Online
TAOM.Dependencies


 succeeded in 495ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Online
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM.Dependencies


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'nav|navigation|scene' } | Select-Object FullName,Length | Sort-Object FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map' -Recurse -File -ErrorAction SilentlyContinue | Select-Object FullName,Length | Sort-Object FullName | Select-Object -First 200" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { "'$_.Name -match '"'map|scene|terrain|snow' -or "'$_.Extension -eq '"'.xscene' } | Select-Object FullName,Length | Sort-Object FullName | Select-Object -First 200" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map' -Recurse -File -Include '*.xml','*.xscene','*.json','*.txt' -ErrorAction SilentlyContinue | Select-String -Pattern 'Snow|snow|terrain|Terrain|FaceGroup|face_group|navigation|navmesh' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() } | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
 succeeded in 591ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\Assets\main_map_textures\taom_gui_map_circle_atlas_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\Assets\main_map_textures\taom_map_circle_decal_mtl.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\AssetSources\main_map_textures\taom_gui_map_circle_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\Brushes\FactionMap.xml                          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\Brushes\MapBar.xml                              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\Prefabs\MomentumView\MomentumMapIndicator.xml   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteData\FactionMap\fullres\region_map_bounda…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteData\FactionMap\region_map_boundary.png   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteData\ui_factionmap\ui_factionmap.xml      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_banner_choose…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_circle…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_center_frame.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon1.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon2.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon3.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon4.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon5.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon6.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_icon7.png     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_canvas.p…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame_si…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_left_frame.png
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame_s…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\SpriteParts\ui_taom\MapBar\mapbar_right_frame.p…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleData\custom_battle_scenes.xml                 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleData\sp_battle_scenes.xml                     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleSounds\LOTR\Monsters\Warg\footstep_snow_1.wav 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleSounds\LOTR\Monsters\Warg\run_snow_1.wav      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleSounds\LOTR\Monsters\Warg\run_snow_2.wav      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleSounds\LOTR\Monsters\Warg\run_snow_3.wav      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\ModuleSounds\LOTR\Monsters\Warg\run_snow_4.wav      


 succeeded in 619ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Battle Map\worldmap_battle_scene_g…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\lotraom_sceners_general.xml             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Barad_Faen_Castle\navmesh.bin  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Barad_Faen_Castle\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\czr_mordor_02_forceatmo\navmes…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\czr_mordor_02_forceatmo\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Helms_Deep_Town_forceatmo\navm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Helms_Deep_Town_forceatmo\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotraom_w_osgiliath_i_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotraom_w_osgiliath_i_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_e_osgiliath_i_forceat…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_e_osgiliath_i_forceat…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_hat_gondor_town_calem…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_hat_gondor_town_calem…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_black_gates_force…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_black_gates_force…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_dead_marshes_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_dead_marshes_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_fords_of_isen_for…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_kys_fords_of_isen_for…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_minas_tirith_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_minas_tirith_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_minas_tirith\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_rohan_plains_westfold…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\lotrtaom_rohan_plains_westfold…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Main_lemmy\navmesh.bin         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Main_lemmy\scene.xscene        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Main_map\navmesh.bin           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\Main_map\scene.xscene          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\scn_czr_forceatmo\navmesh.bin  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\scn_czr_forceatmo\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\taom_haterade_erebor_town_z\sc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\taom_minas_morgul_002\scene.xs…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Backups\taom_minas_tirith_l1\scene.xsc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Barad_Faen_Castle\navmesh.bin          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Barad_Faen_Castle\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Bergraw_Castle\navmesh.bin             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Bergraw_Castle\scene.xscene            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Camloth_Village\navmesh.bin            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Camloth_Village\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\czr_mordor_02_forceatmo\navmesh.bin    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\czr_mordor_02_forceatmo\scene.xscene   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Dinadab_Village_forceatmo\navmesh.bin  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Dinadab_Village_forceatmo\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Goblin_Town\navmesh.bin                
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Goblin_Town\scene.xscene               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\HART_ISENGARD\navmesh.bin              
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\HART_ISENGARD\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Helms_Deep_Town_forceatmo\navmesh.bin  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Helms_Deep_Town_forceatmo\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\HelmsDeep_forceatmo\navmesh.bin        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\HelmsDeep_forceatmo\scene.xscene       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lossarnach_Town\navmesh.bin            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lossarnach_Town\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_1_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_1_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_1\navm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_1\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_2_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_2_forc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_2\navm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Lotraom_comprehend_Gondor_Arena_2\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotraom_gondor_generic_battle_01\navme…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotraom_gondor_generic_battle_01\scene…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotraom_w_osgiliath_i_forceatmo\navmes…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotraom_w_osgiliath_i_forceatmo\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_e_osgiliath_i_forceatmo\navme…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_e_osgiliath_i_forceatmo\scene…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_dunland_fb_d\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_dunland_fb_mountains_a\sc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_dunland_fb_y\navmesh.bin  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_dunland_fb_y\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_dunland_fb_z\scene.xscene 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_gondor_town_calembel\navm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_gondor_town_calembel\scen…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_hat_mt\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_iron_hills_01_forceatmo\navme…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_iron_hills_01_forceatmo\scene…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_black_gates_forceatmo\nav…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_black_gates_forceatmo\sce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_dead_marshes_forceatmo\na…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_dead_marshes_forceatmo\sc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_fords_of_isen_forceatmo\n…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_fords_of_isen_forceatmo\s…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_gondor_village_a_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_kys_gondor_village_a_forceatm…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_minas_tirith_civil\navmesh.bin
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_minas_tirith_civil\scene.xsce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_minas_tirith_forceatmo\navmes…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_minas_tirith_forceatmo\scene.…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_mordor_o_generic_forceatmo\na…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_mordor_o_generic_forceatmo\sc…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_rohan_plains_westfold\navmesh…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\lotrtaom_rohan_plains_westfold\scene.x…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Main_map\navmesh.bin                   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Main_map\scene.xscene                  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Melgobas_Castle\navmesh.bin            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Melgobas_Castle\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Mereham_Village\navmesh.bin            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Mereham_Village\scene.xscene           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\scn_czr_forceatmo\navmesh.bin          
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\scn_czr_forceatmo\scene.xscene         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\scn_erebor_kitbash_01\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\scn_gondor_01\scene.xscene             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\shogun_gondor_village_01\navmesh.bin   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\shogun_gondor_village_01\scene.xscene  
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_haterade_erebor_town_z\scene.xsce…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_minas_morgul_002\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_minas_tirith_l1\scene.xscene      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_minas_tirith_l1v3\scene.xscene    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_recruit_scene_01\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_recruit_scene_02\scene.xscene     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_zed_mordor_battle_001_forceatmo\n…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\taom_zed_mordor_battle_001_forceatmo\s…


 succeeded in 670ms:

[32;1mFullName[0m
[32;1m--------                                                                                                               [0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetPackages\pack0.tpac                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetPackages\pack1.tpac                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetPackages\pack2.tpac                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetPackages\pack3.tpac                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetPackages\pack4.tpac                        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\008_d_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\008_h_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\008_n_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\021_2_d_tex.tpac             
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\021_d_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\021_h_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\021_n_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\054_d_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\054_h_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\054_n_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\055_d_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\055_h_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\055_n_tex.tpac               
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\al_nat_river_mtl.tpac        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Beach_Cliff_3_d_tex.tpac     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Beach_Cliff_h_tex.tpac       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Beach_Cliff_n_tex.tpac       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\ground_grass_b_d_4K_tex.tpac 
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\height_AI_Far_h_tex.tpac     
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava01_d_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava01_h_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava01_n_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava04_d_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava04_h_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lava04_n_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lemmy_grass_mtl.tpac         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lemmy_rock_far_mtl.tpac      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lemmy_rock_mordor_mtl.tpac   
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lemmy_volcan_mtl.tpac        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\lemmy_water_mtl.tpac         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\map_mountain_base_02_mtl.tpac
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_cliff_b_d_tex.tpac      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_cliff_b4K_d_tex.tpac    
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_f_4k_tex.tpac           
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_far_03_h_tex.tpac       
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_far_new_h_tex.tpac      
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\rock_far_new_tex.tpac        
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Steppe_02_h_tex.tpac         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Steppe_d_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Steppe_h_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Steppe_n_tex.tpac            
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\4K_Textures\Steppe_WORK_tex.tpac         
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\dale icons_g…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dale\textures\dal…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\dol_gu…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dol Guldur\textur…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\textures\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\textures\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\textures\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\textures\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\textures\…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Dunland\Wildlings…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\erebor map…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\d…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\d…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\d…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\d…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\e…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\k…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\k…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\k…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\k…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\l…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\l…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\l…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\l…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\r…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\r…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\r…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Erebor\Textures\r…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_1…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_1…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_1…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_1…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_1…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_2…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_2…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_2…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_2…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_2…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\argonath_g…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor tow…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor vil…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor_pel…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor_pel…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor_pel…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor_pel…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\gondor_pel…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\minas_tiri…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\osgiliath_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gondor\Pelargir_g…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\gundabad…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Gundabad\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\isengard…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\orthanc_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\orthanc_…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets\Campaign Map\Map Icons\Isengard\textures…


 succeeded in 947ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotraom_w_osgiliath_i_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_e_osgiliath_i_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_kys_black_gates_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_kys_dead_marshes_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_kys_fords_of_isen_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_minas_tirith_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\lotrtaom_mordor_o_generic_forceatmo.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Atmospheres\Pelargir.xml:11: <value name="snow_density" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\DistanceCaches\last_rebuild_report.json:8: "NavigationType": "Default",
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\DistanceCaches\settlements_snapshot.json:3: "NavMeshCrc": 1737466250,
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\DistanceCaches\settlements_snapshot.json:4: "NavigationType": "Default",
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\FR\loc_settlements.xml:293: <string id="Settlements.Settlement.text.village_ES5_2" text="Lartusys se trouve près de l'extrémité sud du lac Tanaesis. Le village se dresse près d'un gisement de sel, et des sources salées remontent parfois à la surface, alimentant la légende selon laquelle ce fut jadis une porte d'entrée vers une mer souterraine hantée." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\FR\loc_settlements.xml:693: <string id="Settlements.Settlement.text.castle_village_A7_2" text="Bunqaz se trouve à l'embouchure du grand oued qui sépare l'escarpement de Jarjara des collines du Jabal Tamar. Les villageois creusent des puits ici pour exploiter les eaux souterraines de l'oued, les utilisant pour irriguer leurs oliveraies." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\FR\loc_settlements.xml:819: <string id="Settlements.Settlement.text.village_A5_1" text="Mahloul se trouve dans une large vallée entre l'escarpement de Jarjara et les collines du Jabal Ashab dans la steppe désertique au sud-est du Bahr al-Yeshm. Les eaux souterraines fournissent juste assez d'eau pour cultiver de quoi subsister, tandis que le village gagne son argent en élevant des chevaux sur l'herbe riche qui pousse dans le Jabal Ashabi après les pluies de printemps." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\FR\loc_settlements.xml:867: <string id="Settlements.Settlement.text.village_A8_1" text="Ezbet Nahul se trouve sur une plate-forme rocheuse surplombant les eaux que les Aserai appellent le Bahr al-Yeshm, la Mer de Jade. L'eau souterraine piégée par les roches est accessible par des puits et utilisée pour irriguer les champs de blé du village." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\FR\loc_settlements.xml:1101: <string id="0cZmKO76" text="La ville connue aujourd'hui sous le nom de Charas est traditionnellement considérée comme la première colonie calradienne sur les rives de leur nouveau continent. À l'apogée de l'Empire, lorsque la capitale fut déplacée au nord vers Paravenos, Charasea demeura un centre clé de navigation et de commerce, tandis que les aristocrates impériaux construisaient leurs résidences d'été sur les rives chaudes du golfe voisin et naviguaient sur leurs bateaux de plaisance autour de ses îles. Lorsque les Vlandiens arrivèrent, Charas tomba entre les mains de l'impitoyable Maison de dey Cortain, et sa richesse finance leurs ambitions." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\PL\loc_settlements.xml:1157: <string id="Settlements.Settlement.text.village_K1_3" text="Kuruluk sits alongside the Zakhan, a tributary of the Karakaz river. Though this valley is snowbound for much of the year, steppe ponies thrive on the spring grasses and in the winter dig through the snow for food, their thick coats protecting them from the icy blasts of wind." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\PL\loc_settlements.xml:1165: <string id="Settlements.Settlement.text.castle_village_K5_1" text="Khimli sits in a basin the Devseg plateau. Though this land is dry most of the year, it experiences winter snowfalls and muddy springs. Fine clay can be collected from the pools left by the snowmelt and the rains." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\PL\loc_settlements.xml:1203: <string id="Settlements.Settlement.text.village_K1_1" text="Fisnar sits alongside the Zakhan, a tributary of the Karakaz river. Snow-bound for much of the year, the spring grasses nonetheless sustain sizeable herds of sheep, the thick wool of which protects them from the bitter winter cold." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\Languages\PL\loc_settlements.xml:1213: <string id="Settlements.Settlement.text.village_K1_4" text="Asalig sits at the foot of Bars Dag, one of the peaks that marks the edge of the Devseg plateau. Villagers raise horses in the lower slopes, keeping watch for the snow leopards for which the peak is named." />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:6733: <Settlement id="castle_village_K4_2" name="{=Settlements.Settlement.name.castle_village_K4_2}Kuruluk" posX="1448.792" posY="556.884" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_3}Kuruluk sits alongside the Zakhan, a tributary of the Karakaz river. Though this valley is snowbound for much of the year, steppe ponies thrive on the spring grasses and in the winter dig through the snow for food, their thick coats protecting them from the icy blasts of wind.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:6783: <Settlement id="castle_village_K5_1" name="{=Settlements.Settlement.name.castle_village_K5_1}Khimli" posX="1445.801" posY="420.051" culture="Culture.khuzait" text="{=Settlements.Settlement.text.castle_village_K5_1}Khimli sits in a basin the Devseg plateau. Though this land is dry most of the year, it experiences winter snowfalls and muddy springs. Fine clay can be collected from the pools left by the snowmelt and the rains.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:6984: <Settlement id="village_K1_1" name="{=Settlements.Settlement.name.village_K1_1}Fisnar" posX="1283.389" posY="426.472" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_1}Fisnar sits alongside the Zakhan, a tributary of the Karakaz river. Snow-bound for much of the year, the spring grasses nonetheless sustain sizeable herds of sheep, the thick wool of which protects them from the bitter winter cold.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml:7023: <Settlement id="village_K1_4" name="{=Settlements.Settlement.name.village_K1_4}Asalig" posX="1426.566" posY="446.175" culture="Culture.khuzait" text="{=Settlements.Settlement.text.village_K1_4}Asalig sits at the foot of Bars Dag, one of the peaks that marks the edge of the Devseg plateau. Villagers raise horses in the lower slopes, keeping watch for the snow leopards for which the peak is named.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:32: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:57: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:82: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:107: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:132: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:157: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:182: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:207: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:232: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:257: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:282: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:307: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:332: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:534: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:577: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:620: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:663: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:706: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:749: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:792: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:835: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:878: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:921: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:964: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1007: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1154: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1352: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1388: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1418: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\ARENA_FUNCTION_ONE.xml:1441: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:37: <decal_component material="blood_terrain_decal_9"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:43: <decal_component material="blood_terrain_decal_6"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:72: <decal_component material="blood_decal_terrain_material" _index_="1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:878: <decal_component material="blood_terrain_decal_11"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:884: <decal_component material="blood_terrain_decal_0"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:890: <decal_component material="blood_terrain_decal_1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTPrefab6.xml:896: <decal_component material="blood_terrain_decal_10"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:17: <variable name="_properGroundOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:18: <variable name="_properGroundInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:19: <variable name="_underDebrisOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:20: <variable name="_underDebrisInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:21: <variable name="_overDebrisOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:22: <variable name="_overDebrisInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:23: <variable name="_underDebrisGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:24: <variable name="_overDebrisGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:25: <variable name="_onSolidWallGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:27: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4157: <game_entity name="terrain_rock_a2" old_prefab_name="">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4159: <physics shape="bo_terrain_rock_a"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4161: <meta_mesh_component name="terrain_rock_a2">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4162: <mesh name="terrain_rock_a2" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4163: <mesh name="terrain_rock_a2.lod3" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4164: <mesh name="terrain_rock_a2.lod4" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4165: <mesh name="terrain_rock_a2.lod5" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4169: <game_entity name="terrain_rock_b2" old_prefab_name="">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4171: <physics shape="bo_terrain_rock_b"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4173: <meta_mesh_component name="terrain_rock_b2">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4174: <mesh name="terrain_rock_b2" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4175: <mesh name="terrain_rock_b2.lod3" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4176: <mesh name="terrain_rock_b2.lod4" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4177: <mesh name="terrain_rock_b2.lod5" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4463: <variable name="_properGroundOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4464: <variable name="_properGroundInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4465: <variable name="_underDebrisOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4466: <variable name="_underDebrisInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4467: <variable name="_overDebrisOutsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4468: <variable name="_overDebrisInsideNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4469: <variable name="_underDebrisGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4470: <variable name="_overDebrisGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4471: <variable name="_onSolidWallGenericNavmeshID" value="-1"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:4473: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9150: <game_entity name="terrain_rock_a2" old_prefab_name="">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9152: <physics shape="bo_terrain_rock_a"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9154: <meta_mesh_component name="terrain_rock_a2">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9155: <mesh name="terrain_rock_a2" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9156: <mesh name="terrain_rock_a2.lod3" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9157: <mesh name="terrain_rock_a2.lod4" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9158: <mesh name="terrain_rock_a2.lod5" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9162: <game_entity name="terrain_rock_a" old_prefab_name="">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9164: <physics shape="bo_terrain_rock_a"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9166: <meta_mesh_component name="terrain_rock_a">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9167: <mesh name="terrain_rock_a" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9168: <mesh name="terrain_rock_a.lod3" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9169: <mesh name="terrain_rock_a.lod4" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9170: <mesh name="terrain_rock_a.lod5" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9174: <game_entity name="terrain_rock_b2" old_prefab_name="">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9176: <physics shape="bo_terrain_rock_b"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9178: <meta_mesh_component name="terrain_rock_b2">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9179: <mesh name="terrain_rock_b2" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9180: <mesh name="terrain_rock_b2.lod3" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9181: <mesh name="terrain_rock_b2.lod4" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\GTWALL_FULL.xml:9182: <mesh name="terrain_rock_b2.lod5" material="vtstt_rock_cliff" factor="4281742902"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\hart_pop_common.xml:25: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\hart_pop_common.xml:78: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\hart_pop_common.xml:131: <variable name="NavMeshPrefabName" value=""/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs\hart_pop_common.xml:184: <variable name="NavMeshPrefabName" value=""/>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SceneObj\\Main_map\\scene.xscene'
"'$i=0
Get-Content -Path $path -ErrorAction SilentlyContinue | ForEach-Object { $i++; if ($_ -match '"'navmesh|Navigation|terrain|Terrain|face|Face|layer|Layer|Snow|snow|group|Group') { '{0,5}: {1}' -f "'$i, $_ } } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SceneObj\\Main_map' -Force | Select-Object Name,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Format-Hex -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SceneObj\\Main_map\\navmesh.bin' -Count 256" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Item 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\SceneObj\\Main_map\\navmesh.bin' | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
 succeeded in 557ms:

[32;1mName          [0m[32;1m Length  [0m[32;1m LastWriteTime[0m
[32;1m----          [0m [32;1m------  [0m [32;1m-------------[0m
ShaderCache             5/28/2026 11:56:46 AM
atmosphere.xml 4941     5/28/2026 11:56:46 AM
flora.bin      39768759 5/28/2026 11:56:46 AM
flowmap.dds    1048704  5/28/2026 11:56:45 AM
navmesh.bin    780013   5/28/2026 11:56:46 AM
references.txt 14515    5/28/2026 11:56:46 AM
scene.xscene   11242169 5/28/2026 11:56:46 AM
terrain.bin    56084947 5/28/2026 11:56:43 AM


 succeeded in 560ms:

   Label: E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Main_map\navmesh.bin

[32;1m          Offset[0m [32;1;3mBytes                                          [0m[32;1m Ascii[0m
[32;1m                [0m [32;1;3m00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F[0m[32;1m[0m
[32;1m          ------[0m [32;1m-----------------------------------------------[0m [32;1m-----[0m
0000000000000000 52 4E 4D 31 E5 E6 0B 00 03 00 00 00 00 00 00 00 RNM1åæ� �       
0000000000000010 9F 12 1D 00 00 00 00 00 E5 E6 0B 00 00 00 00 00 ���     åæ�     
0000000000000020 00 00 00 00 00 00 00 00 17 1C B0 2A 97 80 00 00         ��°*��  
0000000000000030 F1 0C 4E 4D 47 39 B8 3A 00 00 1C 7F DA 43 E0 66 ñ�NMG9¸:  ��ÚCàf
0000000000000040 C9 42 33 33 34 42 53 A3 DB 43 D8 B3 C0 0C 00 F1 ÉB334BS£ÛCØ³À� ñ
0000000000000050 10 3E 2B E0 43 98 6B CB 42 FA 8B 34 42 24 30 E0 �>+àC�kËBú�4B$0à
0000000000000060 43 D0 11 C4 42 C1 13 35 42 DA 7E E3 43 78 52 C8 CÐ�ÄBÁ�5BÚ~ãCxRÈ
0000000000000070 24 00 71 2E 84 E3 43 08 97 C1 0C 00 71 B2 A4 E5 $ q.�ãC��Á� q²¤å
0000000000000080 43 18 D2 C7 0C 00 71 00 82 E5 43 88 D8 BF 0C 00 C�ÒÇ� q �åC�Ø¿� 
0000000000000090 F2 04 9C 9A E8 43 A8 3A C2 42 B3 9E 34 42 FA 3E ò���èC¨:ÂB³�4Bú>
00000000000000A0 E8 43 C0 F6 BB 18 00 F1 03 C2 EB 43 A8 8E BD 42 èCÀö»� ñ�ÂëC¨�½B
00000000000000B0 53 A2 34 42 5F 67 EB 43 D8 F4 B6 18 00 F1 04 49 S¢4B_gëCØô¶� ñ�I
00000000000000C0 51 EF 43 D0 53 BB 42 6F C4 34 42 69 E9 EE 43 B8 QïCÐS»BoÄ4BiéîC¸
00000000000000D0 F7 B3 18 00 F1 04 C6 B3 F2 43 18 53 B5 42 52 6C ÷³� ñ�Æ³òC�SµBRl
00000000000000E0 35 42 5C A6 F1 43 88 F0 AD 18 00 71 F4 60 F5 43 5B\¦ñC�ð­� qô`õC
00000000000000F0 08 EF A9 0C 00 71 61 66 F4 43 10 60 A5 0C 00 F1 �ï©� qafôC�`¥� ñ


 succeeded in 542ms:

[32;1mFullName                                                                                            [0m[32;1m Length[0m[32;1m LastWriteTi[0m
[32;1m                                                                                                    [0m[32;1m       [0m[32;1m me[0m
[32;1m--------                                                                                            [0m [32;1m------[0m [32;1m-----------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SceneObj\Main_map\navmesh.bin 780013 5/28/2026 …


 succeeded in 616ms:
  115: 												<variable name="NavMeshPrefabName" value=""/>
  145: 												<variable name="NavMeshPrefabName" value=""/>
  193: 						<flag name="align_to_terrain" value="true"/>
  205: 								<flag name="align_to_terrain" value="true"/>
  229: 												<variable name="NavMeshPrefabName" value=""/>
  238: 								<flag name="align_to_terrain" value="true"/>
  262: 												<variable name="NavMeshPrefabName" value=""/>
  271: 								<flag name="align_to_terrain" value="true"/>
  295: 												<variable name="NavMeshPrefabName" value=""/>
  304: 								<flag name="align_to_terrain" value="true"/>
  325: 								<flag name="align_to_terrain" value="true"/>
  346: 								<flag name="align_to_terrain" value="true"/>
  367: 								<flag name="align_to_terrain" value="true"/>
  388: 								<flag name="align_to_terrain" value="true"/>
  409: 								<flag name="align_to_terrain" value="true"/>
  430: 								<flag name="align_to_terrain" value="true"/>
  568: 						<flag name="align_to_terrain" value="true"/>
  585: 								<flag name="align_to_terrain" value="true"/>
  586: 								<flag name="align_rotation_to_terrain" value="true"/>
  598: 								<flag name="align_to_terrain" value="true"/>
  622: 												<variable name="NavMeshPrefabName" value=""/>
  631: 								<flag name="align_to_terrain" value="true"/>
  632: 								<flag name="align_rotation_to_terrain" value="true"/>
  641: 								<flag name="align_to_terrain" value="true"/>
  665: 												<variable name="NavMeshPrefabName" value=""/>
  674: 								<flag name="align_to_terrain" value="true"/>
  698: 												<variable name="NavMeshPrefabName" value=""/>
  707: 								<flag name="align_to_terrain" value="true"/>
  731: 												<variable name="NavMeshPrefabName" value=""/>
  740: 								<flag name="align_to_terrain" value="true"/>
  741: 								<flag name="align_rotation_to_terrain" value="true"/>
  750: 								<flag name="align_to_terrain" value="true"/>
  751: 								<flag name="align_rotation_to_terrain" value="true"/>
  760: 								<flag name="align_to_terrain" value="true"/>
  761: 								<flag name="align_rotation_to_terrain" value="true"/>
  770: 								<flag name="align_to_terrain" value="true"/>
  771: 								<flag name="align_rotation_to_terrain" value="true"/>
  783: 								<flag name="align_to_terrain" value="true"/>
  784: 								<flag name="align_rotation_to_terrain" value="true"/>
  796: 								<flag name="align_to_terrain" value="true"/>
  797: 								<flag name="align_rotation_to_terrain" value="true"/>
  809: 								<flag name="align_to_terrain" value="true"/>
  810: 								<flag name="align_rotation_to_terrain" value="true"/>
  822: 								<flag name="align_to_terrain" value="true"/>
  823: 								<flag name="align_rotation_to_terrain" value="true"/>
  907: 								<flag name="align_to_terrain" value="true"/>
  928: 								<flag name="align_to_terrain" value="true"/>
  949: 								<flag name="align_to_terrain" value="true"/>
  970: 								<flag name="align_to_terrain" value="true"/>
  991: 								<flag name="align_to_terrain" value="true"/>
 1012: 								<flag name="align_to_terrain" value="true"/>
 1033: 								<flag name="align_to_terrain" value="true"/>
 1090: 												<variable name="NavMeshPrefabName" value=""/>
 1120: 												<variable name="NavMeshPrefabName" value=""/>
 1185: 						<flag name="align_to_terrain" value="true"/>
 1278: 												<variable name="NavMeshPrefabName" value=""/>
 1308: 												<variable name="NavMeshPrefabName" value=""/>
 1338: 												<variable name="NavMeshPrefabName" value=""/>
 2104: 								<flag name="align_to_terrain" value="true"/>
 2125: 								<flag name="align_to_terrain" value="true"/>
 2140: 								<flag name="align_to_terrain" value="true"/>
 2149: 								<flag name="align_to_terrain" value="true"/>
 2263: 								<flag name="align_to_terrain" value="true"/>
 2272: 								<flag name="align_to_terrain" value="true"/>
 2312: 								<flag name="align_to_terrain" value="true"/>
 2448: 								<flag name="align_to_terrain" value="true"/>
 2548: 								<flag name="align_to_terrain" value="true"/>
 2563: 								<flag name="align_to_terrain" value="true"/>
 2578: 								<flag name="align_to_terrain" value="true"/>
 2587: 								<flag name="align_to_terrain" value="true"/>
 2620: 								<flag name="align_to_terrain" value="true"/>
 2647: 								<flag name="align_to_terrain" value="true"/>
 2656: 								<flag name="align_to_terrain" value="true"/>
 2665: 								<flag name="align_to_terrain" value="true"/>
 2692: 								<flag name="align_to_terrain" value="true"/>
 2737: 								<flag name="align_to_terrain" value="true"/>
 2746: 								<flag name="align_to_terrain" value="true"/>
 2755: 								<flag name="align_to_terrain" value="true"/>
 2764: 								<flag name="align_to_terrain" value="true"/>
 2773: 								<flag name="align_to_terrain" value="true"/>
 2782: 								<flag name="align_to_terrain" value="true"/>
 2791: 								<flag name="align_to_terrain" value="true"/>
 2800: 								<flag name="align_to_terrain" value="true"/>
 2815: 								<flag name="align_to_terrain" value="true"/>
 2824: 								<flag name="align_to_terrain" value="true"/>
 2833: 								<flag name="align_to_terrain" value="true"/>
 2842: 								<flag name="align_to_terrain" value="true"/>
 2851: 								<flag name="align_to_terrain" value="true"/>
 2889: 								<flag name="align_to_terrain" value="true"/>
 2943: 								<flag name="align_to_terrain" value="true"/>
 2952: 								<flag name="align_to_terrain" value="true"/>
 2979: 								<flag name="align_to_terrain" value="true"/>
 3024: 								<flag name="align_to_terrain" value="true"/>
 3302: 								<flag name="align_to_terrain" value="true"/>
 3303: 								<flag name="align_rotation_to_terrain" value="true"/>
 3315: 								<flag name="align_to_terrain" value="true"/>
 3316: 								<flag name="align_rotation_to_terrain" value="true"/>
 3325: 								<flag name="align_to_terrain" value="true"/>
 3326: 								<flag name="align_rotation_to_terrain" value="true"/>
 3338: 								<flag name="align_to_terrain" value="true"/>
 3339: 								<flag name="align_rotation_to_terrain" value="true"/>
 3351: 								<flag name="align_to_terrain" value="true"/>
 3375: 												<variable name="NavMeshPrefabName" value=""/>
 3384: 								<flag name="align_to_terrain" value="true"/>
 3408: 												<variable name="NavMeshPrefabName" value=""/>
 3417: 								<flag name="align_to_terrain" value="true"/>
 3418: 								<flag name="align_rotation_to_terrain" value="true"/>
 3427: 								<flag name="align_to_terrain" value="true"/>
 3428: 								<flag name="align_rotation_to_terrain" value="true"/>
 3440: 								<flag name="align_to_terrain" value="true"/>
 3441: 								<flag name="align_rotation_to_terrain" value="true"/>
 3453: 								<flag name="align_to_terrain" value="true"/>
 3454: 								<flag name="align_rotation_to_terrain" value="true"/>
 3466: 								<flag name="align_to_terrain" value="true"/>
 3467: 								<flag name="align_rotation_to_terrain" value="true"/>
 3479: 								<flag name="align_to_terrain" value="true"/>
 3480: 								<flag name="align_rotation_to_terrain" value="true"/>
 3492: 								<flag name="align_to_terrain" value="true"/>
 3493: 								<flag name="align_rotation_to_terrain" value="true"/>
 3502: 								<flag name="align_to_terrain" value="true"/>
 3526: 												<variable name="NavMeshPrefabName" value=""/>
 3535: 								<flag name="align_to_terrain" value="true"/>
 3559: 												<variable name="NavMeshPrefabName" value=""/>
 3568: 								<flag name="align_to_terrain" value="true"/>
 3569: 								<flag name="align_rotation_to_terrain" value="true"/>
 3581: 								<flag name="align_to_terrain" value="true"/>
 3582: 								<flag name="align_rotation_to_terrain" value="true"/>
 3591: 								<flag name="align_to_terrain" value="true"/>
 3612: 								<flag name="align_to_terrain" value="true"/>
 3633: 								<flag name="align_to_terrain" value="true"/>
 3654: 								<flag name="align_to_terrain" value="true"/>
 3675: 								<flag name="align_to_terrain" value="true"/>
 3696: 								<flag name="align_to_terrain" value="true"/>
 3717: 								<flag name="align_to_terrain" value="true"/>
 4003: 						<flag name="align_to_terrain" value="true"/>
 4020: 								<flag name="align_to_terrain" value="true"/>
 4044: 												<variable name="NavMeshPrefabName" value=""/>
 4053: 								<flag name="align_to_terrain" value="true"/>
 4077: 												<variable name="NavMeshPrefabName" value=""/>
 4086: 								<flag name="align_to_terrain" value="true"/>
 4110: 												<variable name="NavMeshPrefabName" value=""/>
 4119: 								<flag name="align_to_terrain" value="true"/>
 4143: 												<variable name="NavMeshPrefabName" value=""/>
 4224: 								<flag name="align_to_terrain" value="true"/>
 4245: 								<flag name="align_to_terrain" value="true"/>
 4266: 								<flag name="align_to_terrain" value="true"/>
 4287: 								<flag name="align_to_terrain" value="true"/>
 4308: 								<flag name="align_to_terrain" value="true"/>
 4329: 								<flag name="align_to_terrain" value="true"/>
 4350: 								<flag name="align_to_terrain" value="true"/>
 4407: 												<variable name="NavMeshPrefabName" value=""/>
 4437: 												<variable name="NavMeshPrefabName" value=""/>
 4514: 						<flag name="align_to_terrain" value="true"/>
 4534: 									<flag name="use_color_from_terrain" value="true"/>
 4601: 						<variable name="Align Road to Terrain" value="false"/>
 4602: 						<variable name="Road Elevation Offset From Terrain" value="0.000000"/>
 4603: 						<variable name="Align Terrain to Road" value="false"/>
 4632: 									<flag name="use_color_from_terrain" value="true"/>
 4682: 						<variable name="Align Road to Terrain" value="false"/>
 4683: 						<variable name="Road Elevation Offset From Terrain" value="0.000000"/>
 4684: 						<variable name="Align Terrain to Road" value="false"/>
 4910: 												<variable name="NavMeshPrefabName" value=""/>
 4940: 												<variable name="NavMeshPrefabName" value=""/>
 4990: 						<flag name="align_to_terrain" value="true"/>
 5002: 								<flag name="align_to_terrain" value="true"/>
 5026: 												<variable name="NavMeshPrefabName" value=""/>
 5035: 								<flag name="align_to_terrain" value="true"/>
 5059: 												<variable name="NavMeshPrefabName" value=""/>
 5078: 												<variable name="NavMeshPrefabName" value=""/>
 5087: 								<flag name="align_to_terrain" value="true"/>
 5111: 												<variable name="NavMeshPrefabName" value=""/>
 5120: 								<flag name="align_to_terrain" value="true"/>
 5141: 								<flag name="align_to_terrain" value="true"/>
 5162: 								<flag name="align_to_terrain" value="true"/>
 5183: 								<flag name="align_to_terrain" value="true"/>
 5204: 								<flag name="align_to_terrain" value="true"/>
 5225: 								<flag name="align_to_terrain" value="true"/>
 5246: 								<flag name="align_to_terrain" value="true"/>
 5368: 		<game_entity name="MapSnowAndRainDefiner" old_prefab_name="MapSnowAndRainDefiner" mobility="1">
 5371: 				<script name="SnowAndRainTextureDefiner">
 5373: 						<variable name="SnowAndRainTexture" value="main_map_snow_flowmap"/>
 5596: 												<variable name="NavMeshPrefabName" value=""/>
 5626: 												<variable name="NavMeshPrefabName" value=""/>
 5669: 						<flag name="align_to_terrain" value="true"/>
 5681: 								<flag name="align_to_terrain" value="true"/>
 5705: 												<variable name="NavMeshPrefabName" value=""/>
 5714: 								<flag name="align_to_terrain" value="true"/>
 5738: 												<variable name="NavMeshPrefabName" value=""/>
 5747: 								<flag name="align_to_terrain" value="true"/>
 5771: 												<variable name="NavMeshPrefabName" value=""/>
 5780: 								<flag name="align_to_terrain" value="true"/>
 5801: 								<flag name="align_to_terrain" value="true"/>
 5822: 								<flag name="align_to_terrain" value="true"/>
 5843: 								<flag name="align_to_terrain" value="true"/>
 5864: 								<flag name="align_to_terrain" value="true"/>
 5885: 								<flag name="align_to_terrain" value="true"/>
 5906: 								<flag name="align_to_terrain" value="true"/>
 6367: 						<flag name="align_to_terrain" value="true"/>
 6368: 						<flag name="align_rotation_to_terrain" value="true"/>
 6391: 						<variable name="Align Road to Terrain" value="false"/>

codex
The installed engine code confirms the important split: vanilla party speed reads terrain from the navmesh face, then separately turns snowy/blizzard weather into `TerrainType.Snow` for the snow penalty. I’m checking the feature data now so any declared/applied mismatches are based on an actual cross-reference, not a skim.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\_Module\\ModuleData' -Filter 'spcultures.xml' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData' -Filter 'spcultures.xml' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/ModuleData/taom_spcultures.xml','Main/_Module/ModuleData/spcultures.xslt' -Pattern 'id=\"(rohan|dunland|harad|rhun|dale|khand)\"|Culture\\.(rohan|dunland|harad|rhun|dale|khand)|taom_(rohan|dunland|harad|rhun|dale|khand)_' -AllMatches | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files = @('"'Main/Features/CulturalFeats/TaomCulturalFeats.cs','Main/Features/CulturalFeats/CulturalFeatsService.cs','Main/_Module/ModuleData/taom_spcultures.xml','Main/_Module/ModuleData/spcultures.xslt')
foreach ("'$file in $files) {
  Select-String -Path $file -Pattern '"'taom_[a-z0-9_]+_speed' -AllMatches | ForEach-Object {
    foreach ("'$m in $_.Matches) { '"'{0}:{1}: {2}' -f "'$file, $_.LineNumber, $m.Value }
  }
}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 561ms:

[32;1mFullName                                                                                           [0m[32;1m Length[0m[32;1m LastWriteTim[0m
[32;1m                                                                                                   [0m[32;1m       [0m[32;1m e[0m
[32;1m--------                                                                                           [0m [32;1m------[0m [32;1m------------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml 152632 2/27/2026 1…


 succeeded in 795ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:293: <feat id="taom_dunland_plain_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:857: <feat id="taom_rohan_mounted_cost" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:858: <feat id="taom_rohan_mounted_wage" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:859: <feat id="taom_rohan_infantry_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:860: <feat id="taom_rohan_loyalty" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:861: <feat id="taom_rohan_morale" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:862: <feat id="taom_rohan_plain_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:1348: <feat id="taom_harad_desert_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:1355: <feat id="taom_rhun_steppe_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:1362: <feat id="taom_dale_plain_speed" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\spcultures.xslt:1369: <feat id="taom_khand_steppe_speed" />

 succeeded in 1050ms:
 succeeded in 1284ms:
Main/Features/CulturalFeats/TaomCulturalFeats.cs:255: taom_erebor_construction_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:259: taom_erebor_snow_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:266: taom_rivendell_forest_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:268: taom_mirkwood_forest_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:274: taom_lothlorien_forest_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:276: taom_lothlorien_construction_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:285: taom_isengard_construction_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:288: taom_isengard_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:289: taom_isengard_swamp_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:296: taom_gundabad_snow_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:302: taom_umbar_desert_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:306: taom_dolguldur_construction_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:316: taom_gondor_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:323: taom_mordor_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:324: taom_mordor_swamp_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:325: taom_mordor_night_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:329: taom_rohan_infantry_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:332: taom_rohan_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:334: taom_dale_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:335: taom_khand_steppe_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:336: taom_rhun_steppe_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:337: taom_harad_desert_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:338: taom_dunland_plain_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:339: taom_shaghana_desert_speed
Main/Features/CulturalFeats/TaomCulturalFeats.cs:340: taom_abanissa_desert_speed
Main/_Module/ModuleData/taom_spcultures.xml:223: taom_erebor_construction_speed
Main/_Module/ModuleData/taom_spcultures.xml:227: taom_erebor_snow_speed
Main/_Module/ModuleData/taom_spcultures.xml:599: taom_rivendell_forest_speed
Main/_Module/ModuleData/taom_spcultures.xml:955: taom_mirkwood_forest_speed
Main/_Module/ModuleData/taom_spcultures.xml:1315: taom_lothlorien_forest_speed
Main/_Module/ModuleData/taom_spcultures.xml:1317: taom_lothlorien_construction_speed
Main/_Module/ModuleData/taom_spcultures.xml:1679: taom_isengard_construction_speed
Main/_Module/ModuleData/taom_spcultures.xml:1682: taom_isengard_plain_speed
Main/_Module/ModuleData/taom_spcultures.xml:1683: taom_isengard_swamp_speed
Main/_Module/ModuleData/taom_spcultures.xml:2044: taom_gundabad_snow_speed
Main/_Module/ModuleData/taom_spcultures.xml:2404: taom_umbar_desert_speed
Main/_Module/ModuleData/taom_spcultures.xml:2762: taom_dolguldur_construction_speed
Main/_Module/ModuleData/taom_spcultures.xml:3131: taom_gondor_plain_speed
Main/_Module/ModuleData/taom_spcultures.xml:3500: taom_mordor_plain_speed
Main/_Module/ModuleData/taom_spcultures.xml:3501: taom_mordor_swamp_speed
Main/_Module/ModuleData/taom_spcultures.xml:3502: taom_mordor_night_speed
Main/_Module/ModuleData/taom_spcultures.xml:3819: taom_shaghana_desert_speed
Main/_Module/ModuleData/taom_spcultures.xml:4083: taom_abanissa_desert_speed
Main/_Module/ModuleData/spcultures.xslt:293: taom_dunland_plain_speed
Main/_Module/ModuleData/spcultures.xslt:859: taom_rohan_infantry_speed
Main/_Module/ModuleData/spcultures.xslt:862: taom_rohan_plain_speed
Main/_Module/ModuleData/spcultures.xslt:1348: taom_harad_desert_speed
Main/_Module/ModuleData/spcultures.xslt:1355: taom_rhun_steppe_speed
Main/_Module/ModuleData/spcultures.xslt:1362: taom_dale_plain_speed
Main/_Module/ModuleData/spcultures.xslt:1369: taom_khand_steppe_speed

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$source = '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\spcultures.xml'
"'$xsltPath = '"'Main\\_Module\\ModuleData\\spcultures.xslt'
"'$xslt = [System.Xml.Xsl.XslCompiledTransform]::new()
$xslt.Load((Resolve-Path $xsltPath))
$sw = [System.IO.StringWriter]::new()
$xw = [System.Xml.XmlWriter]::Create($sw, $xslt.OutputSettings)
$xslt.Transform($source, $xw)
$xw.Close()
[xml]$doc = $sw.ToString()
$ids = @('"'empire','vlandia','aserai','khuzait','sturgia','battania')
foreach ("'$id in $ids) {
  $node = $doc.SelectSingleNode("//Culture[@id='"'"'$id'"']\")
  "'$featBlocks = $node.SelectNodes('"'cultural_feats')
  "'$feats = @($featBlocks | ForEach-Object { $_.SelectNodes('"'feat') | ForEach-Object { "'$_.id } })
  '"'{0}: cultural_feats={1}; feats=[{2}]' -f "'$id, $featBlocks.Count, ($feats -join '"', ')
}" in C:\Users\mikew\source\repos\TAOM
 succeeded in 724ms:
empire: cultural_feats=1; feats=[battanian_forest_speed, battanian_militia_production, battanian_slower_construction, taom_dunland_plain_speed]
vlandia: cultural_feats=1; feats=[taom_rohan_mounted_cost, taom_rohan_mounted_wage, taom_rohan_infantry_speed, taom_rohan_loyalty, taom_rohan_morale, taom_rohan_plain_speed]
aserai: cultural_feats=1; feats=[aserai_cheaper_caravans, aserai_desert_speed, aserai_increased_wages, taom_harad_desert_speed]
khuzait: cultural_feats=1; feats=[khuzait_cheaper_recruits_mounted, khuzait_increased_animal_production, khuzait_decreased_town_tax, taom_rhun_steppe_speed]
sturgia: cultural_feats=1; feats=[sturgian_increased_grain_production, sturgian_decreased_army_influence_cost, sturgian_increased_decision_penalty, taom_dale_plain_speed]
battania: cultural_feats=1; feats=[battanian_forest_speed, battanian_militia_production, battanian_slower_construction, taom_khand_steppe_speed]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$terrainExpected = [ordered]@{
  '"'taom_mirkwood_forest_speed'='Forest'
  'taom_lothlorien_forest_speed'='Forest'
  'taom_rivendell_forest_speed'='Forest'
  'taom_erebor_snow_speed'='Snow'
  'taom_gundabad_snow_speed'='Snow'
  'taom_khand_steppe_speed'='Steppe'
  'taom_rhun_steppe_speed'='Steppe'
  'taom_umbar_desert_speed'='Desert'
  'taom_harad_desert_speed'='Desert'
  'taom_shaghana_desert_speed'='Desert'
  'taom_abanissa_desert_speed'='Desert'
  'taom_mordor_plain_speed'='Plain'
  'taom_gondor_plain_speed'='Plain'
  'taom_rohan_plain_speed'='Plain'
  'taom_dale_plain_speed'='Plain'
  'taom_dunland_plain_speed'='Plain'
  'taom_isengard_plain_speed'='Plain'
  'taom_mordor_swamp_speed'='Swamp'
  'taom_isengard_swamp_speed'='Swamp'
  'taom_mordor_night_speed'='Night'
}
"'$regText = Get-Content '"'Main\\Features\\CulturalFeats\\TaomCulturalFeats.cs' -Raw
"'$svcText = Get-Content '"'Main\\Features\\CulturalFeats\\CulturalFeatsService.cs' -Raw
"'$xmlText = Get-Content '"'Main\\_Module\\ModuleData\\taom_spcultures.xml' -Raw
"'$xsltText = Get-Content '"'Main\\_Module\\ModuleData\\spcultures.xslt' -Raw
foreach ("'$kv in $terrainExpected.GetEnumerator()) {
  $id = $kv.Key
  $fieldMatch = [regex]::Match($regText, '"'([A-Za-z0-9_]+)\\s*=\\s*Register\\(\"' + [regex]::Escape("'$id) + '"'\"\\)')
  "'$field = if ($fieldMatch.Success) { $fieldMatch.Groups[1].Value } else { '"'<missing-register>' }
  "'$init = if ($field -ne '"'<missing-register>' -and "'$regText -match [regex]::Escape($field) + '"'\\.Initialize\\(') { 'yes' } else { 'no' }
  "'$yield = if ($field -ne '"'<missing-register>' -and "'$regText -match '"'yield return _instance\\.' + [regex]::Escape("'$field.TrimStart('"'_')) + ';') { 'yes' } else { 'no' }
  "'$decl = @()
  if ($xmlText -match [regex]::Escape($id)) { $decl += '"'taom_spcultures' }
  if ("'$xsltText -match [regex]::Escape($id)) { $decl += '"'spcultures.xslt' }
  "'$propName = ($field.TrimStart('"'_') -replace '("'^|_)([a-z])'"', { param("'$m) $m.Groups[2].Value.ToUpper() }) + '"'Feat'
  "'$apply = if ($svcText -match [regex]::Escape($propName)) { '"'yes' } else { 'no' }
  '{0,-34} expected={1,-6} decl={2,-30} regField={3,-28} init={4} yield={5} applyRef={6}' -f "'$id, $kv.Value, ($decl -join '"'+'), "'$field, $init, $yield, $apply
}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 671ms:
taom_mirkwood_forest_speed         expected=Forest decl=taom_spcultures                regField=_mirkwoodForestSpeed         init=yes yield=no applyRef=yes
taom_lothlorien_forest_speed       expected=Forest decl=taom_spcultures                regField=_lothlorienForestSpeed       init=yes yield=no applyRef=yes
taom_rivendell_forest_speed        expected=Forest decl=taom_spcultures                regField=_rivendellForestSpeed        init=yes yield=no applyRef=yes
taom_erebor_snow_speed             expected=Snow   decl=taom_spcultures                regField=_ereborSnowSpeed             init=yes yield=no applyRef=yes
taom_gundabad_snow_speed           expected=Snow   decl=taom_spcultures                regField=_gundabadSnowSpeed           init=yes yield=no applyRef=yes
taom_khand_steppe_speed            expected=Steppe decl=spcultures.xslt                regField=_khandSteppeSpeed            init=yes yield=no applyRef=yes
taom_rhun_steppe_speed             expected=Steppe decl=spcultures.xslt                regField=_rhunSteppeSpeed             init=yes yield=no applyRef=yes
taom_umbar_desert_speed            expected=Desert decl=taom_spcultures                regField=_umbarDesertSpeed            init=yes yield=no applyRef=yes
taom_harad_desert_speed            expected=Desert decl=spcultures.xslt                regField=_haradDesertSpeed            init=yes yield=no applyRef=yes
taom_shaghana_desert_speed         expected=Desert decl=taom_spcultures                regField=_shaghanaDesertSpeed         init=yes yield=no applyRef=yes
taom_abanissa_desert_speed         expected=Desert decl=taom_spcultures                regField=_abanissaDesertSpeed         init=yes yield=no applyRef=yes
taom_mordor_plain_speed            expected=Plain  decl=taom_spcultures                regField=_mordorPlainSpeed            init=yes yield=no applyRef=yes
taom_gondor_plain_speed            expected=Plain  decl=taom_spcultures                regField=_gondorPlainSpeed            init=yes yield=no applyRef=yes
taom_rohan_plain_speed             expected=Plain  decl=spcultures.xslt                regField=_rohanPlainSpeed             init=yes yield=no applyRef=yes
taom_dale_plain_speed              expected=Plain  decl=spcultures.xslt                regField=_dalePlainSpeed              init=yes yield=no applyRef=yes
taom_dunland_plain_speed           expected=Plain  decl=spcultures.xslt                regField=_dunlandPlainSpeed           init=yes yield=no applyRef=yes
taom_isengard_plain_speed          expected=Plain  decl=taom_spcultures                regField=_isengardPlainSpeed          init=yes yield=no applyRef=yes
taom_mordor_swamp_speed            expected=Swamp  decl=taom_spcultures                regField=_mordorSwampSpeed            init=yes yield=no applyRef=yes
taom_isengard_swamp_speed          expected=Swamp  decl=taom_spcultures                regField=_isengardSwampSpeed          init=yes yield=no applyRef=yes
taom_mordor_night_speed            expected=Night  decl=taom_spcultures                regField=_mordorNightSpeed            init=yes yield=no applyRef=yes
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m
[31;1mInvalidOperation: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  37 | [0m … lace '(^|_)([a-z])', { param($m) [36;1m$m.Groups[2].Value.ToUpper()[0m }) + 'F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot index into a null array.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$terrainExpected = [ordered]@{
  '"'taom_mirkwood_forest_speed'='Forest'
  'taom_lothlorien_forest_speed'='Forest'
  'taom_rivendell_forest_speed'='Forest'
  'taom_erebor_snow_speed'='Snow'
  'taom_gundabad_snow_speed'='Snow'
  'taom_khand_steppe_speed'='Steppe'
  'taom_rhun_steppe_speed'='Steppe'
  'taom_umbar_desert_speed'='Desert'
  'taom_harad_desert_speed'='Desert'
  'taom_shaghana_desert_speed'='Desert'
  'taom_abanissa_desert_speed'='Desert'
  'taom_mordor_plain_speed'='Plain'
  'taom_gondor_plain_speed'='Plain'
  'taom_rohan_plain_speed'='Plain'
  'taom_dale_plain_speed'='Plain'
  'taom_dunland_plain_speed'='Plain'
  'taom_isengard_plain_speed'='Plain'
  'taom_mordor_swamp_speed'='Swamp'
  'taom_isengard_swamp_speed'='Swamp'
  'taom_mordor_night_speed'='Night'
}
"'$regText = Get-Content '"'Main\\Features\\CulturalFeats\\TaomCulturalFeats.cs' -Raw
"'$svcText = Get-Content '"'Main\\Features\\CulturalFeats\\CulturalFeatsService.cs' -Raw
"'$xmlText = Get-Content '"'Main\\_Module\\ModuleData\\taom_spcultures.xml' -Raw
"'$xsltText = Get-Content '"'Main\\_Module\\ModuleData\\spcultures.xslt' -Raw
foreach ("'$kv in $terrainExpected.GetEnumerator()) {
  $id = $kv.Key
  $fieldMatch = [regex]::Match($regText, '"'([A-Za-z0-9_]+)\\s*=\\s*Register\\(\"' + [regex]::Escape("'$id) + '"'\"\\)')
  "'$field = if ($fieldMatch.Success) { $fieldMatch.Groups[1].Value } else { '"'<missing-register>' }
  "'$propertyMatch = if ($field -ne '"'<missing-register>') { [regex]::Match("'$regText, '"'public static FeatObject\\s+([A-Za-z0-9]+)\\s*=>\\s*Instance\\.' + [regex]::Escape("'$field)) } else { [regex]::Match('"'', 'x') }
  "'$property = if ($propertyMatch.Success) { $propertyMatch.Groups[1].Value } else { '"'<missing-property>' }
  "'$init = if ($field -ne '"'<missing-register>' -and "'$regText -match [regex]::Escape($field) + '"'\\.Initialize\\(') { 'yes' } else { 'no' }
  "'$yield = if ($field -ne '"'<missing-register>' -and "'$regText -match '"'yield return _instance\\.' + [regex]::Escape("'$field) + '"';') { 'yes' } else { 'no' }
  "'$decl = @()
  if ($xmlText -match [regex]::Escape($id)) { $decl += '"'taom_spcultures' }
  if ("'$xsltText -match [regex]::Escape($id)) { $decl += '"'spcultures.xslt' }
  "'$apply = if ($property -ne '"'<missing-property>' -and "'$svcText -match '"'TaomCulturalFeats\\.' + [regex]::Escape("'$property)) { '"'yes' } else { 'no' }
  '{0,-34} expected={1,-6} decl={2,-30} field={3,-28} prop={4,-28} init={5} yield={6} applyRef={7}' -f "'$id, $kv.Value, ($decl -join '"'+'), "'$field, $property, $init, $yield, $apply
}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 533ms:
taom_mirkwood_forest_speed         expected=Forest decl=taom_spcultures                field=_mirkwoodForestSpeed         prop=MirkwoodForestSpeedFeat      init=yes yield=yes applyRef=yes
taom_lothlorien_forest_speed       expected=Forest decl=taom_spcultures                field=_lothlorienForestSpeed       prop=LothlorienForestSpeedFeat    init=yes yield=yes applyRef=yes
taom_rivendell_forest_speed        expected=Forest decl=taom_spcultures                field=_rivendellForestSpeed        prop=RivendellForestSpeedFeat     init=yes yield=yes applyRef=yes
taom_erebor_snow_speed             expected=Snow   decl=taom_spcultures                field=_ereborSnowSpeed             prop=EreborSnowSpeedFeat          init=yes yield=yes applyRef=yes
taom_gundabad_snow_speed           expected=Snow   decl=taom_spcultures                field=_gundabadSnowSpeed           prop=GundabadSnowSpeedFeat        init=yes yield=yes applyRef=yes
taom_khand_steppe_speed            expected=Steppe decl=spcultures.xslt                field=_khandSteppeSpeed            prop=KhandSteppeSpeedFeat         init=yes yield=yes applyRef=yes
taom_rhun_steppe_speed             expected=Steppe decl=spcultures.xslt                field=_rhunSteppeSpeed             prop=RhunSteppeSpeedFeat          init=yes yield=yes applyRef=yes
taom_umbar_desert_speed            expected=Desert decl=taom_spcultures                field=_umbarDesertSpeed            prop=UmbarDesertSpeedFeat         init=yes yield=yes applyRef=yes
taom_harad_desert_speed            expected=Desert decl=spcultures.xslt                field=_haradDesertSpeed            prop=HaradDesertSpeedFeat         init=yes yield=yes applyRef=yes
taom_shaghana_desert_speed         expected=Desert decl=taom_spcultures                field=_shaghanaDesertSpeed         prop=ShaghanaDesertSpeedFeat      init=yes yield=yes applyRef=yes
taom_abanissa_desert_speed         expected=Desert decl=taom_spcultures                field=_abanissaDesertSpeed         prop=AbanissaDesertSpeedFeat      init=yes yield=yes applyRef=yes
taom_mordor_plain_speed            expected=Plain  decl=taom_spcultures                field=_mordorPlainSpeed            prop=MordorPlainSpeedFeat         init=yes yield=yes applyRef=yes
taom_gondor_plain_speed            expected=Plain  decl=taom_spcultures                field=_gondorPlainSpeed            prop=GondorPlainSpeedFeat         init=yes yield=yes applyRef=yes
taom_rohan_plain_speed             expected=Plain  decl=spcultures.xslt                field=_rohanPlainSpeed             prop=RohanPlainSpeedFeat          init=yes yield=yes applyRef=yes
taom_dale_plain_speed              expected=Plain  decl=spcultures.xslt                field=_dalePlainSpeed              prop=DalePlainSpeedFeat           init=yes yield=yes applyRef=yes
taom_dunland_plain_speed           expected=Plain  decl=spcultures.xslt                field=_dunlandPlainSpeed           prop=DunlandPlainSpeedFeat        init=yes yield=yes applyRef=yes
taom_isengard_plain_speed          expected=Plain  decl=taom_spcultures                field=_isengardPlainSpeed          prop=IsengardPlainSpeedFeat       init=yes yield=yes applyRef=yes
taom_mordor_swamp_speed            expected=Swamp  decl=taom_spcultures                field=_mordorSwampSpeed            prop=MordorSwampSpeedFeat         init=yes yield=yes applyRef=yes
taom_isengard_swamp_speed          expected=Swamp  decl=taom_spcultures                field=_isengardSwampSpeed          prop=IsengardSwampSpeedFeat       init=yes yield=yes applyRef=yes
taom_mordor_night_speed            expected=Night  decl=taom_spcultures                field=_mordorNightSpeed            prop=MordorNightSpeedFeat         init=yes yield=yes applyRef=yes

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -Include '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'CreateAndRegister|InitializeDefaultCampaignObjects|TaomCulturalFeats.GetAllFeats|GetAllFeats\\(|DefaultCulturalFeats|FeatObject' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Include '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'InitializeDefaultCampaignObjects|LoadFromXml|cultural_feats|FeatObject|DefaultCulturalFeats' | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Culture|DefaultCulturalFeats|CampaignObjectManager|DefaultCampaign' | Select-Object -First 80" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/CulturalFeats' -Recurse -File | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 703ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:7: [HarmonyPatch(typeof(Campaign), "InitializeDefaultCampaignObjects")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:9: public static class Campaign_InitializeDefaultCampaignObjects_Patch
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:14: TaomCulturalFeats.CreateAndRegister();
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:119: private static void ApplyIfHas(ICultureFeatAdapter culture, FeatObject feat, ref ExplainedNumber result)
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CultureFeatAdapter.cs:24: public bool HasFeat(FeatObject feat)
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\ICultureFeatAdapter.cs:18: /// feat registry hands out nulls before <c>CreateAndRegister</c> runs).
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\ICultureFeatAdapter.cs:20: bool HasFeat(FeatObject feat);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:17: private FeatObject _ereborGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:18: private FeatObject _ereborProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:19: private FeatObject _ereborConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:20: private FeatObject _ereborLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:21: private FeatObject _ereborMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:22: private FeatObject _ereborSmithing;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:23: private FeatObject _ereborSnowSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:26: private FeatObject _rivendellArmyInfluence;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:27: private FeatObject _rivendellHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:28: private FeatObject _rivendellArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:29: private FeatObject _rivendellFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:30: private FeatObject _rivendellLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:31: private FeatObject _rivendellForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:34: private FeatObject _mirkwoodForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:35: private FeatObject _mirkwoodMilitiaProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:36: private FeatObject _mirkwoodHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:37: private FeatObject _mirkwoodFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:38: private FeatObject _mirkwoodMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:41: private FeatObject _lothlorienForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:42: private FeatObject _lothlorienGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:43: private FeatObject _lothlorienConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:44: private FeatObject _lothlorienFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:45: private FeatObject _lothlorienLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:46: private FeatObject _lothlorienMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:49: private FeatObject _isengardCheaperRecruits;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:50: private FeatObject _isengardGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:51: private FeatObject _isengardDecisionPenalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:52: private FeatObject _isengardPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:53: private FeatObject _isengardConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:54: private FeatObject _isengardSmithing;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:55: private FeatObject _isengardRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:56: private FeatObject _isengardPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:57: private FeatObject _isengardSwampSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:60: private FeatObject _gundabadArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:61: private FeatObject _gundabadGrainProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:62: private FeatObject _gundabadWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:63: private FeatObject _gundabadPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:64: private FeatObject _gundabadRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:65: private FeatObject _gundabadSnowSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:68: private FeatObject _umbarCheaperCaravans;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:69: private FeatObject _umbarRenown;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:70: private FeatObject _umbarWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:71: private FeatObject _umbarTariffIncome;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:72: private FeatObject _umbarDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:75: private FeatObject _dolguldurArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:76: private FeatObject _dolguldurMilitiaProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:77: private FeatObject _dolguldurConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:78: private FeatObject _dolguldurPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:79: private FeatObject _dolguldurFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:82: private FeatObject _gondorGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:83: private FeatObject _gondorArmyInfluence;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:84: private FeatObject _gondorHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:85: private FeatObject _gondorPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:86: private FeatObject _gondorLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:87: private FeatObject _gondorMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:88: private FeatObject _gondorPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:91: private FeatObject _mordorArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:92: private FeatObject _mordorGrainProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:93: private FeatObject _mordorWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:94: private FeatObject _mordorPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:95: private FeatObject _mordorRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:96: private FeatObject _mordorPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:97: private FeatObject _mordorSwampSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:98: private FeatObject _mordorNightSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:101: private FeatObject _rohanMountedCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:102: private FeatObject _rohanMountedWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:103: private FeatObject _rohanInfantrySpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:104: private FeatObject _rohanLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:105: private FeatObject _rohanMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:106: private FeatObject _rohanPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:109: private FeatObject _dalePlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:112: private FeatObject _khandSteppeSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:115: private FeatObject _rhunSteppeSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:118: private FeatObject _haradDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:121: private FeatObject _dunlandPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:124: private FeatObject _shaghanaDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:127: private FeatObject _abanissaDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:130: public static FeatObject EreborGarrisonWageFeat => Instance._ereborGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:131: public static FeatObject EreborProductionFeat => Instance._ereborProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:132: public static FeatObject EreborConstructionSpeedFeat => Instance._ereborConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:133: public static FeatObject EreborLoyaltyFeat => Instance._ereborLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:134: public static FeatObject EreborMoraleFeat => Instance._ereborMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:135: public static FeatObject EreborSmithingFeat => Instance._ereborSmithing;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:136: public static FeatObject EreborSnowSpeedFeat => Instance._ereborSnowSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:139: public static FeatObject RivendellArmyInfluenceFeat => Instance._rivendellArmyInfluence;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:140: public static FeatObject RivendellHearthGrowthFeat => Instance._rivendellHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:141: public static FeatObject RivendellArmyInfluenceCostFeat => Instance._rivendellArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:142: public static FeatObject RivendellFoodConsumptionFeat => Instance._rivendellFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:143: public static FeatObject RivendellLoyaltyFeat => Instance._rivendellLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:144: public static FeatObject RivendellForestSpeedFeat => Instance._rivendellForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:147: public static FeatObject MirkwoodForestSpeedFeat => Instance._mirkwoodForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:148: public static FeatObject MirkwoodMilitiaProductionFeat => Instance._mirkwoodMilitiaProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:149: public static FeatObject MirkwoodHearthGrowthFeat => Instance._mirkwoodHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:150: public static FeatObject MirkwoodFoodConsumptionFeat => Instance._mirkwoodFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:151: public static FeatObject MirkwoodMoraleFeat => Instance._mirkwoodMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:154: public static FeatObject LothlorienForestSpeedFeat => Instance._lothlorienForestSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:155: public static FeatObject LothlorienGarrisonWageFeat => Instance._lothlorienGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:156: public static FeatObject LothlorienConstructionSpeedFeat => Instance._lothlorienConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:157: public static FeatObject LothlorienFoodConsumptionFeat => Instance._lothlorienFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:158: public static FeatObject LothlorienLoyaltyFeat => Instance._lothlorienLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:159: public static FeatObject LothlorienMoraleFeat => Instance._lothlorienMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:162: public static FeatObject IsengardCheaperRecruitsFeat => Instance._isengardCheaperRecruits;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:163: public static FeatObject IsengardGarrisonWageFeat => Instance._isengardGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:164: public static FeatObject IsengardDecisionPenaltyFeat => Instance._isengardDecisionPenalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:165: public static FeatObject IsengardPartySizeFeat => Instance._isengardPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:166: public static FeatObject IsengardConstructionSpeedFeat => Instance._isengardConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:167: public static FeatObject IsengardSmithingFeat => Instance._isengardSmithing;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:168: public static FeatObject IsengardRaidDamageFeat => Instance._isengardRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:169: public static FeatObject IsengardPlainSpeedFeat => Instance._isengardPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:170: public static FeatObject IsengardSwampSpeedFeat => Instance._isengardSwampSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:173: public static FeatObject GundabadArmyInfluenceCostFeat => Instance._gundabadArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:174: public static FeatObject GundabadGrainProductionFeat => Instance._gundabadGrainProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:175: public static FeatObject GundabadWageFeat => Instance._gundabadWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:176: public static FeatObject GundabadPartySizeFeat => Instance._gundabadPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:177: public static FeatObject GundabadRaidDamageFeat => Instance._gundabadRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:178: public static FeatObject GundabadSnowSpeedFeat => Instance._gundabadSnowSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:181: public static FeatObject UmbarCheaperCaravansFeat => Instance._umbarCheaperCaravans;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:182: public static FeatObject UmbarRenownFeat => Instance._umbarRenown;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:183: public static FeatObject UmbarWageFeat => Instance._umbarWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:184: public static FeatObject UmbarTariffIncomeFeat => Instance._umbarTariffIncome;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:185: public static FeatObject UmbarDesertSpeedFeat => Instance._umbarDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:188: public static FeatObject DolGuldurArmyInfluenceCostFeat => Instance._dolguldurArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:189: public static FeatObject DolGuldurMilitiaProductionFeat => Instance._dolguldurMilitiaProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:190: public static FeatObject DolGuldurConstructionSpeedFeat => Instance._dolguldurConstructionSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:191: public static FeatObject DolGuldurPartySizeFeat => Instance._dolguldurPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:192: public static FeatObject DolGuldurFoodConsumptionFeat => Instance._dolguldurFoodConsumption;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:195: public static FeatObject GondorGarrisonWageFeat => Instance._gondorGarrisonWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:196: public static FeatObject GondorArmyInfluenceFeat => Instance._gondorArmyInfluence;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:197: public static FeatObject GondorHearthGrowthFeat => Instance._gondorHearthGrowth;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:198: public static FeatObject GondorPartySizeFeat => Instance._gondorPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:199: public static FeatObject GondorLoyaltyFeat => Instance._gondorLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:200: public static FeatObject GondorMoraleFeat => Instance._gondorMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:201: public static FeatObject GondorPlainSpeedFeat => Instance._gondorPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:204: public static FeatObject MordorArmyInfluenceCostFeat => Instance._mordorArmyInfluenceCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:205: public static FeatObject MordorGrainProductionFeat => Instance._mordorGrainProduction;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:206: public static FeatObject MordorWageFeat => Instance._mordorWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:207: public static FeatObject MordorPartySizeFeat => Instance._mordorPartySize;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:208: public static FeatObject MordorRaidDamageFeat => Instance._mordorRaidDamage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:209: public static FeatObject MordorPlainSpeedFeat => Instance._mordorPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:210: public static FeatObject MordorSwampSpeedFeat => Instance._mordorSwampSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:211: public static FeatObject MordorNightSpeedFeat => Instance._mordorNightSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:214: public static FeatObject RohanMountedCostFeat => Instance._rohanMountedCost;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:215: public static FeatObject RohanMountedWageFeat => Instance._rohanMountedWage;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:216: public static FeatObject RohanInfantrySpeedFeat => Instance._rohanInfantrySpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:217: public static FeatObject RohanLoyaltyFeat => Instance._rohanLoyalty;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:218: public static FeatObject RohanMoraleFeat => Instance._rohanMorale;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:219: public static FeatObject RohanPlainSpeedFeat => Instance._rohanPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:222: public static FeatObject DalePlainSpeedFeat => Instance._dalePlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:225: public static FeatObject KhandSteppeSpeedFeat => Instance._khandSteppeSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:228: public static FeatObject RhunSteppeSpeedFeat => Instance._rhunSteppeSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:231: public static FeatObject HaradDesertSpeedFeat => Instance._haradDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:234: public static FeatObject DunlandPlainSpeedFeat => Instance._dunlandPlainSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:237: public static FeatObject ShaghanaDesertSpeedFeat => Instance._shaghanaDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:240: public static FeatObject AbanissaDesertSpeedFeat => Instance._abanissaDesertSpeed;
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:242: public static void CreateAndRegister()
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:349: -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:353: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:357: -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:361: 1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:365: 5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:369: -0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:373: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:379: 0.35f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:383: 0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:387: 0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:391: -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:395: 0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:399: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:405: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:409: 0.25f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:413: -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:417: -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:421: 3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:427: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:431: -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:435: -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:439: -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:443: 0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:447: 3f, isPositiveEffect: true, FeatObject.AdditionType.Add);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:453: -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:457: -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:461: 0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:465: 0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:469: 0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:473: -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:477: 0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:481: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:485: 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:491: -0.4f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:495: 0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:499: 0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:503: 0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs:507: 0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

 succeeded in 731ms:

[32;1mPath[0m
[32;1m----                                                                                                                   [0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyBaseHelper.cs                                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs                      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs                 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ManagedParameters.cs             
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ManagedParameters.cs             
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\DefaultCult…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\FeatObject.…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\FeatObject.…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\FeatObject.…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\ConversationAnimati…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\ConversationAnimati…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\KingdomElection.cs      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\KingdomElection.cs      
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManage…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBattleRewa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBattleRewa…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingCo…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingCo…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCaravanMod…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCaravanMod…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapVisibil…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMinorFacti…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeed…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeed…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeed…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyTroop…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyTroop…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageM…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlement…


 succeeded in 934ms:

[32;1mFullName                                                                                                             [0m[32;1m L[0m
[32;1m                                                                                                                     [0m[32;1m e[0m
[32;1m                                                                                                                     [0m[32;1m n[0m
[32;1m                                                                                                                     [0m[32;1m g[0m
[32;1m                                                                                                                     [0m[32;1m t[0m
[32;1m                                                                                                                     [0m[32;1m h[0m
[32;1m--------                                                                                                             [0m [32;1m-[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsIoC.cs                                      7
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs                                  8
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CultureFeatAdapter.cs                                    7
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\ICulturalFeatsService.cs                                 6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\ICultureFeatAdapter.cs                                   0
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TaomCulturalFeats.cs                                     3
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\TerrainKind.cs                                           4
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs 1
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs                        8
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs                          5
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBuildingConstructionModel.cs                  3
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomCaravanModel.cs                               6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomClanFinanceModel.cs                           4
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs                       1
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs                           6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs                             3
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs                            6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs                     6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomRaidModel.cs                                  0
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs                     1
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementMilitiaModel.cs                     6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementProsperityModel.cs                  6
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSmithingModel.cs                              7
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomVillageProductionModel.cs                     7


 succeeded in 1458ms:

Class TaleWorlds.CampaignSystem.[7mCampaignObjectManager[0m
Class TaleWorlds.CampaignSystem.[7mCulture[0mTrait
Class TaleWorlds.CampaignSystem.[7mCulture[0mObject
Class TaleWorlds.CampaignSystem.GameComponents.[7mDefaultCampaign[0mShipDamageModel
Class TaleWorlds.CampaignSystem.GameComponents.[7mDefaultCampaign[0mShipParametersModel
Class TaleWorlds.CampaignSystem.GameComponents.[7mDefaultCampaign[0mTimeModel
Class TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreation[7mCulture[0mStage
Class TaleWorlds.CampaignSystem.CharacterDevelopment.[7mDefaultCulturalFeats[0m
Class TaleWorlds.CampaignSystem.[7mCampaignObjectManager[0m.CampaignObjectType
Class TaleWorlds.CampaignSystem.[7mCulture[0mObject.<GetCulturalFeats>d__9
Class TaleWorlds.CampaignSystem.[7mCulture[0mObject.<>c
Class TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel.<GetAvailablePlayerKingdom[7mCulture[0ms>d__10
Class TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent.<Get[7mCulture[0ms>d__46
Class TaleWorlds.CampaignSystem.[7mCampaignObjectManager[0m.CampaignObjectType.<>c


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/Hooks/Campaign_InitializeDefaultCampaignObjects_Patch.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CultureObject 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Deserialize|LoadFromXml|cultural_feats|FeatObject|GetCulturalFeats|Initialize' -Context 3,8 | Select-Object -First 160" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Campaign 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'InitializeDefaultCampaignObjects|InitializeCampaignObjects|Load' -Context 3,10 | Select-Object -First 120" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultCulturalFeats 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Initialize|RegisterAll|FeatObject|RegisterPresumedObject|Instance' -Context 2,5 | Select-Object -First 160" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1055ms:

  public class DefaultCulturalFeats
  {
> 	private [7mFeatObject[0m _aseraiTraderFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _aseraiDesertSpeedFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _aseraiWageFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _battaniaForestSpeedFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _battaniaMilitiaFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _battaniaConstructionFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _empireGarrisonWageFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _empireArmyInfluenceFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _empireVillageHearthFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _khuzaitCheaperRecruitsFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _khuzaitAnimalProductionFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _khuzaitDecreasedTaxFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _sturgianGrainProductionFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _sturgianArmyInfluenceCostFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _sturgianDecisionPenaltyFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _vlandianRenownIncomeFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _vlandianVillageProductionFeat;[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m _vlandianArmyInfluenceCostFeat;[0m
[7m[0m  [0m
> 	private static DefaultCulturalFeats [7mInstance[0m => Campaign.Current.DefaultFeats;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m AseraiTraderFeat => Instance._aseraiTraderFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m AseraiDesertFeat => Instance._aseraiDesertSpeedFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m AseraiIncreasedWageFeat => Instance._aseraiWageFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m BattanianForestSpeedFeat => Instance._battaniaForestSpeedFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m BattanianMilitiaFeat => Instance._battaniaMilitiaFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m BattanianConstructionFeat => Instance._battaniaConstructionFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m EmpireGarrisonWageFeat => Instance._empireGarrisonWageFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m EmpireArmyInfluenceFeat => Instance._empireArmyInfluenceFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m EmpireVillageHearthFeat => Instance._empireVillageHearthFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m KhuzaitRecruitUpgradeFeat => Instance._khuzaitCheaperRecruitsFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m KhuzaitAnimalProductionFeat => Instance._khuzaitAnimalProductionFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m KhuzaitDecreasedTaxFeat => Instance._khuzaitDecreasedTaxFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m SturgianGrainProductionFeat => Instance._sturgianGrainProductionFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m SturgianArmyInfluenceCostFeat => Instance._sturgianArmyInfluenceCostFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m SturgianDecisionPenaltyFeat => Instance._sturgianDecisionPenaltyFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m VlandianRenownMercenaryFeat => Instance._vlandianRenownIncomeFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m VlandianCastleVillageProductionFeat => Instance._vlandianVillageProductionFeat;[0m
[7m[0m  [0m
> 	public static [7mFeatObject[0m VlandianArmyInfluenceFeat => Instance._vlandianArmyInfluenceCostFeat;[0m
[7m[0m  [0m
[7m[0m  	public DefaultCulturalFeats()[0m
[7m[0m  	{[0m
> 		[7mRegisterAll[0m();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void [7mRegisterAll[0m()[0m
[7m[0m  	{[0m
[7m[0m  		_aseraiTraderFeat = Create("aserai_cheaper_caravans");[0m
[7m[0m  		_aseraiDesertSpeedFeat = Create("aserai_desert_speed");[0m
[7m[0m  		_aseraiWageFeat = Create("aserai_increased_wages");[0m
[7m[0m  		_battaniaForestSpeedFeat = Create("battanian_forest_speed");[0m
  		_vlandianVillageProductionFeat = Create("vlandian_villages_production_bonus");
  		_vlandianArmyInfluenceCostFeat = Create("vlandian_increased_army_influence_cost");
> 		[7mInitialize[0mAll();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private [7mFeatObject[0m Create(string stringId)[0m
[7m[0m  	{[0m
> 		return Game.Current.ObjectManager.[7mRegisterPresumedObject[0m(new FeatObject(stringId));[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void [7mInitialize[0mAll()[0m
[7m[0m  	{[0m
> 		_aseraiTraderFeat.[7mInitialize[0m("{=!}aserai_cheaper_caravans", [0m[7m[0m"{=7kGGgkro}Caravans [0m[7m[0mare [0m[7m[0m30% [0m[7m[0mcheaper [0m[7m[0mto [0m[7m[0mbuild. [0m[7m[0m10% [0m
[7m[0mless [0m[7m[0mtrade [0m[7m[0mpenalty.", [0m[7m[0m0.7f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_aseraiDesertSpeedFeat.[7mInitialize[0m("{=!}aserai_desert_speed", [0m[7m[0m"{=6aFTN1Nb}No [0m[7m[0mspeed [0m[7m[0mpenalty [0m[7m[0mon [0m[7m[0mdesert.", [0m[7m[0m1f, [0m
[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_aseraiWageFeat.[7mInitialize[0m("{=!}aserai_increased_wages", [0m[7m[0m"{=GacrZ1Jl}Daily [0m[7m[0mwages [0m[7m[0mof [0m[7m[0mtroops [0m[7m[0min [0m[7m[0mthe [0m[7m[0mparty [0m[7m[0mare [0m
[7m[0mincreased [0m[7m[0mby [0m[7m[0m5%.", [0m[7m[0m0.05f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_battaniaForestSpeedFeat.[7mInitialize[0m("{=!}battanian_forest_speed", [0m[7m[0m"{=38W2WloI}50% [0m[7m[0mless [0m[7m[0mspeed [0m[7m[0mpenalty [0m[7m[0mand [0m[7m[0m15% [0m[7m[0msight [0m
[7m[0mrange [0m[7m[0mbonus [0m[7m[0min [0m[7m[0mforests.", [0m[7m[0m0.5f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_battaniaMilitiaFeat.[7mInitialize[0m("{=!}battanian_militia_production", [0m[7m[0m"{=HLI5zAMV}Towns [0m[7m[0mowned [0m[7m[0mby [0m[7m[0mBattanian [0m[7m[0mrulers [0m
[7m[0mwill [0m[7m[0mhave [0m[7m[0m+20% [0m[7m[0mchance [0m[7m[0mof [0m[7m[0mmilitias [0m[7m[0mto [0m[7m[0mspawn [0m[7m[0mas [0m[7m[0mveteran [0m[7m[0mmilitias.", [0m[7m[0m0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m
[7m[0mFeatObject.AdditionType.Add);[0m
> 		_battaniaConstructionFeat.[7mInitialize[0m("{=!}battanian_slower_construction", [0m[7m[0m"{=ruP9jbSq}10% [0m[7m[0mslower [0m[7m[0mbuild [0m[7m[0mrate [0m[7m[0mfor [0m
[7m[0mtown [0m[7m[0mprojects [0m[7m[0min [0m[7m[0msettlements.", [0m[7m[0m-0.1f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_empireGarrisonWageFeat.[7mInitialize[0m("{=!}empire_decreased_garrison_wage", [0m[7m[0m"{=a2eM0QUb}20% [0m[7m[0mless [0m[7m[0mgarrison [0m[7m[0mtroop [0m
[7m[0mwage.", [0m[7m[0m-0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_empireArmyInfluenceFeat.[7mInitialize[0m("{=!}empire_army_influence", [0m[7m[0m"{=xgPNGOa8}Being [0m[7m[0min [0m[7m[0marmy [0m[7m[0mbrings [0m[7m[0m25% [0m[7m[0mmore [0m
[7m[0minfluence.", [0m[7m[0m0.25f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_empireVillageHearthFeat.[7mInitialize[0m("{=!}empire_slower_hearth_production", [0m[7m[0m"{=UWiqIFUb}Village [0m[7m[0mhearths [0m[7m[0mincrease [0m
[7m[0m20% [0m[7m[0mless.", [0m[7m[0m-0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_khuzaitCheaperRecruitsFeat.[7mInitialize[0m("{=!}khuzait_cheaper_recruits_mounted", [0m[7m[0m"{=JUpZuals}Recruiting [0m[7m[0mand [0m
[7m[0mupgrading [0m[7m[0mmounted [0m[7m[0mtroops [0m[7m[0mare [0m[7m[0m10% [0m[7m[0mcheaper.", [0m[7m[0m-0.1f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_khuzaitAnimalProductionFeat.[7mInitialize[0m("{=!}khuzait_increased_animal_production", [0m[7m[0m"{=Xaw2CoCG}25% [0m[7m[0mproduction [0m
[7m[0mbonus [0m[7m[0mto [0m[7m[0mhorse, [0m[7m[0mmule, [0m[7m[0mcow [0m[7m[0mand [0m[7m[0msheep [0m[7m[0min [0m[7m[0mvillages [0m[7m[0mowned [0m[7m[0mby [0m[7m[0mKhuzait [0m[7m[0mrulers.", [0m[7m[0m0.25f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m
[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_khuzaitDecreasedTaxFeat.[7mInitialize[0m("{=!}khuzait_decreased_town_tax", [0m[7m[0m"{=8PsaGhI8}20% [0m[7m[0mless [0m[7m[0mtax [0m[7m[0mincome [0m[7m[0mfrom [0m
[7m[0mtowns.", [0m[7m[0m-0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_sturgianGrainProductionFeat.[7mInitialize[0m("{=!}sturgian_increased_grain_production", [0m[7m[0m"{=5BabRyaa}Villages [0m[7m[0mgrain [0m
[7m[0mproduction [0m[7m[0mis [0m[7m[0mincreased [0m[7m[0mby [0m[7m[0m10%.", [0m[7m[0m0.1f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_sturgianArmyInfluenceCostFeat.[7mInitialize[0m("{=!}sturgian_decreased_army_influence_cost", [0m[7m[0m"{=Lmjm5Q9D}Armies [0m[7m[0mare [0m
[7m[0mgathered [0m[7m[0mwith [0m[7m[0m50% [0m[7m[0mless [0m[7m[0minfluence.", [0m[7m[0m-0.5f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_sturgianDecisionPenaltyFeat.[7mInitialize[0m("{=!}sturgian_increased_decision_penalty", [0m[7m[0m"{=fB7kS9Cx}20% [0m[7m[0mmore [0m
[7m[0mrelationship [0m[7m[0mpenalty [0m[7m[0mfrom [0m[7m[0mkingdom [0m[7m[0mdecisions.", [0m[7m[0m0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_vlandianRenownIncomeFeat.[7mInitialize[0m("{=!}vlandian_renown_mercenary_income", [0m[7m[0m"{=ppdrgOL8}5% [0m[7m[0mmore [0m[7m[0mrenown [0m[7m[0mfrom [0m[7m[0mthe [0m
[7m[0mbattles, [0m[7m[0m15% [0m[7m[0mmore [0m[7m[0mincome [0m[7m[0mwhile [0m[7m[0mserving [0m[7m[0mas [0m[7m[0ma [0m[7m[0mmercenary.", [0m[7m[0m0.05f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m
[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_vlandianVillageProductionFeat.[7mInitialize[0m("{=!}vlandian_villages_production_bonus", [0m[7m[0m"{=3GsZXXOi}10% [0m[7m[0mproduction [0m
[7m[0mbonus [0m[7m[0mto [0m[7m[0mvillages [0m[7m[0mthat [0m[7m[0mare [0m[7m[0mbound [0m[7m[0mto [0m[7m[0mcastles.", [0m[7m[0m0.1f, [0m[7m[0misPositiveEffect: [0m[7m[0mtrue, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
> 		_vlandianArmyInfluenceCostFeat.[7mInitialize[0m("{=!}vlandian_increased_army_influence_cost", [0m[7m[0m"{=O1XCNeZr}Recruiting [0m
[7m[0mlords [0m[7m[0mto [0m[7m[0marmies [0m[7m[0mcosts [0m[7m[0m20% [0m[7m[0mmore [0m[7m[0minfluence.", [0m[7m[0m0.2f, [0m[7m[0misPositiveEffect: [0m[7m[0mfalse, [0m[7m[0mFeatObject.AdditionType.AddFactor);[0m
[7m[0m  	}[0m
[7m[0m  }[0m


 succeeded in 2207ms:

  
  	private MBList<TextObject> _clanNameList;
  
> 	private MBList<[7mFeatObject[0m> _cultureFeats;[0m
[7m[0m  [0m
[7m[0m  	private MBList<PolicyObject> _defaultPolicyList;[0m
[7m[0m  [0m
[7m[0m  	public CultureTrait[] Traits { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public CharacterObject BasicTroop { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public CharacterObject EliteBasicTroop { get; private set; }[0m
  
  	public MBReadOnlyList<TextObject> ClanNameList => _clanNameList;
  
> 	public MBReadOnlyList<[7mFeatObject[0m> CultureFeats => _cultureFeats;[0m
[7m[0m  [0m
[7m[0m  	public MBReadOnlyList<PolicyObject> DefaultPolicyList => _defaultPolicyList;[0m
[7m[0m  [0m
[7m[0m  	public MBReadOnlyList<int> PossibleClanBannerIconsIDs { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public MBReadOnlyList<CharacterObject> NotableTemplates { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public MBReadOnlyList<CharacterObject> RebelliousHeroTemplates { get; private set; }[0m
  		return Enumerable.Contains(Traits, trait);
  	}
  
> 	public bool HasFeat([7mFeatObject[0m feat)[0m
[7m[0m  	{[0m
[7m[0m  		return _cultureFeats.Contains(feat);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public IEnumerable<[7mFeatObject[0m> GetCulturalFeats(Func<FeatObject, bool> predicate = null)[0m
[7m[0m  	{[0m
> 		foreach ([7mFeatObject[0m cultureFeat in _cultureFeats)[0m
[7m[0m  		{[0m
[7m[0m  			if (predicate == null || predicate(cultureFeat))[0m
[7m[0m  			{[0m
[7m[0m  				yield return cultureFeat;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
  		return base.Name.ToString();
  	}
  
> 	public override void [7mDeserialize[0m(MBObjectManager objectManager, XmlNode node)[0m
[7m[0m  	{[0m
> 		base.[7mDeserialize[0m(objectManager, node);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mMilitiaBonus [0m[7m[0m= [0m[7m[0m((node.Attributes["militia_bonus"] [0m[7m[0m!= [0m[7m[0mnull) [0m[7m[0m? [0m
[7m[0mConvert.ToInt32(node.Attributes["militia_bonus"].Value) [0m[7m[0m: [0m[7m[0m0);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mProsperityBonus [0m[7m[0m= [0m[7m[0m((node.Attributes["prosperity_bonus"] [0m[7m[0m!= [0m[7m[0mnull) [0m[7m[0m? [0m
[7m[0mConvert.ToInt32(node.Attributes["prosperity_bonus"].Value) [0m[7m[0m: [0m[7m[0m0);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mNavalFactor [0m[7m[0m= [0m[7m[0m((node.Attributes["naval_factor"] [0m[7m[0m== [0m[7m[0mnull) [0m[7m[0m? [0m[7m[0m0f [0m[7m[0m: [0m
[7m[0mConvert.ToSingle(node.Attributes["naval_factor"].Value));[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mDefaultPartyTemplate [0m[7m[0m= [0m[7m[0mobjectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("default_party_template", [0m
[7m[0mnode);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mVillagerPartyTemplate [0m[7m[0m= [0m[7m[0mobjectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("villager_party_template", [0m
[7m[0mnode);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mFishingPartyTemplate [0m[7m[0m= [0m[7m[0mobjectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("fishing_party_template", [0m
[7m[0mnode);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mMilitiaPartyTemplate [0m[7m[0m= [0m[7m[0mobjectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("militia_party_template", [0m
[7m[0mnode);[0m
[7m[0m  		RebelsPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("rebels_party_template", node);[0m
  		_maleNameList = new MBList<TextObject>();
  		_femaleNameList = new MBList<TextObject>();
  		_clanNameList = new MBList<TextObject>();
> 		_cultureFeats = new MBList<[7mFeatObject[0m>();[0m
[7m[0m  		MBList<int> mBList = new MBList<int>();[0m
[7m[0m  		MBList<CharacterObject> mBList2 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<CharacterObject> mBList3 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<CharacterObject> mBList4 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<CharacterObject> mBList5 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<CharacterObject> mBList6 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<CharacterObject> mBList7 = new MBList<CharacterObject>();[0m
[7m[0m  		MBList<ItemObject> mBList8 = new MBList<ItemObject>();[0m
  					_clanNameList.Add(new TextObject(childNode4.Attributes["name"].Value));
  				}
  			}
> 			else if (item5.Name == "[7mcultural_feats[0m")[0m
[7m[0m  			{[0m
[7m[0m  				foreach (XmlNode childNode5 in item5.ChildNodes)[0m
[7m[0m  				{[0m
[7m[0m  					string value = childNode5.Attributes["id"].Value;[0m
> 					[7mFeatObject[0m featObject = (FeatObject)MBObjectManager.Instance.CreateObjectFromXmlNode(childNode5);[0m
[7m[0m  					Debug.Print("Reading feat with id " + value + ",  in Culture  " + base.StringId);[0m
> 					if ([7mfeatObject[0m != null)[0m
[7m[0m  					{[0m
> 						_cultureFeats.Add([7mfeatObject[0m);[0m
[7m[0m  					}[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  			else if (item5.Name == "possible_clan_banner_icon_ids")[0m
[7m[0m  			{[0m
[7m[0m  				foreach (XmlNode childNode6 in item5.ChildNodes)[0m
[7m[0m  				{[0m
[7m[0m  					int.TryParse(childNode6.Attributes["id"].Value, out var result2);[0m


 succeeded in 3639ms:

  using TaleWorlds.ModuleManager;
  using TaleWorlds.ObjectSystem;
  using TaleWorlds.SaveSystem;
> using TaleWorlds.SaveSystem.[7mLoad[0m;[0m
[7m[0m  [0m
[7m[0m  namespace TaleWorlds.CampaignSystem;[0m
[7m[0m  [0m
[7m[0m  public class Campaign : GameType[0m
[7m[0m  {[0m
[7m[0m  	[Flags][0m
[7m[0m  	public enum PartyRestFlags : uint[0m
[7m[0m  	{[0m
[7m[0m  		None = 0u,[0m
[7m[0m  		SafeMode = 1u[0m
  	}
  
> 	public enum Game[7mLoad[0mingType[0m
[7m[0m  	{[0m
[7m[0m  		Tutorial,[0m
[7m[0m  		NewCampaign,[0m
[7m[0m  		SavedCampaign,[0m
[7m[0m  		Editor[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public const float ConfigTimeMultiplier = 0.25f;[0m
[7m[0m  [0m
[7m[0m  	private EntitySystem<CampaignEntityComponent> _campaignEntitySystem;[0m
  
  	public bool GameStarted;
  
> 	private Game[7mLoad[0mingType _gameLoadingType;[0m
[7m[0m  [0m
[7m[0m  	public ConversationContext CurrentConversationContext;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	private float _dt;[0m
[7m[0m  [0m
[7m[0m  	private CampaignTimeControlMode _timeControlMode;[0m
[7m[0m  [0m
[7m[0m  	public int CurrentTickCount;[0m
[7m[0m  [0m
  
  	public SandBoxManager SandBoxManager { get; private set; }
  
> 	public Game[7mLoad[0mingType CampaignGameLoadingType => _gameLoadingType;[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(40)][0m
[7m[0m  	public SiegeEventManager SiegeEventManager { get; internal set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(41)][0m
[7m[0m  	public MapEventManager MapEventManager { get; internal set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(43)][0m
[7m[0m  	public MapMarkerManager MapMarkerManager { get; internal set; }[0m
[7m[0m  [0m
  		MainParty.ItemRoster.AddToCounts(DefaultItems.Grain, 1);
  	}
  
> 	[[7mLoad[0mInitializationCallback][0m
> 	private void On[7mLoad[0m(MetaData metaData, ObjectLoadData objectLoadData)[0m
[7m[0m  	{[0m
[7m[0m  		_campaignEntitySystem = new EntitySystem<CampaignEntityComponent>();[0m
[7m[0m  		PlayerFormationPreferences = _playerFormationPreferences.GetReadOnlyDictionary();[0m
[7m[0m  		SpeedUpMultiplier = 4f;[0m
> 		if (UniqueGameId == null && MBSave[7mLoad[0m.IsUpdatingGameVersion [0m[7m[0m&& [0m[7m[0mMBSaveLoad.LastLoadedGameVersion [0m[7m[0m< [0m
[7m[0mApplicationVersion.FromString("v1.2.2"))[0m
[7m[0m  		{[0m
[7m[0m  			UniqueGameId = "oldSave";[0m
[7m[0m  		}[0m
> 		if (MBSave[7mLoad[0m.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0"))[0m
[7m[0m  		{[0m
[7m[0m  			if (_previouslyUsedModules == null)[0m
[7m[0m  			{[0m
[7m[0m  				_previouslyUsedModules = new MBList<string>();[0m
[7m[0m  			}[0m
[7m[0m  			MBList<string> source = new MBList<string>(_previouslyUsedModules);[0m
[7m[0m  			_previouslyUsedModules.Clear();[0m
[7m[0m  			if (source.Any())[0m
[7m[0m  			{[0m
> 				_previouslyUsedModules.Add(string.Join(MBSave[7mLoad[0m.ModuleCodeSeperator.ToString(), [0m[7m[0msource.Select((string [0m[7m[0mx) [0m[7m[0m=> [0m[7m[0mx [0m
[7m[0m+ [0m[7m[0mMBSaveLoad.ModuleVersionSeperator [0m[7m[0m+ [0m[7m[0mApplicationVersion.Empty.ToString())));[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
> 		if (MBSave[7mLoad[0m.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0"))[0m
[7m[0m  		{[0m
[7m[0m  			UnlockedFigureheadsByMainHero = new List<Figurehead>();[0m
[7m[0m  			_customManagers = new List<ICustomSystemManager>();[0m
[7m[0m  			MapMarkerManager = new MapMarkerManager();[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void InitializeForSavedGame()[0m
[7m[0m  	{[0m
[7m[0m  		foreach (Settlement item in Settlement.All)[0m
  		{
> 			item.Party.OnFinish[7mLoad[0mState();[0m
[7m[0m  		}[0m
[7m[0m  		foreach (MobileParty item2 in MobileParties.ToList())[0m
[7m[0m  		{[0m
> 			item2.Party.OnFinish[7mLoad[0mState();[0m
[7m[0m  		}[0m
[7m[0m  		foreach (Settlement item3 in Settlement.All)[0m
[7m[0m  		{[0m
> 			item3.OnFinish[7mLoad[0mState();[0m
[7m[0m  		}[0m
[7m[0m  		GameMenuCallbackManager = new GameMenuCallbackManager();[0m
> 		GameMenuCallbackManager.OnGame[7mLoad[0m();[0m
[7m[0m  		IssueManager.InitializeForSavedGame();[0m
[7m[0m  		MinSettlementX = float.MaxValue;[0m
[7m[0m  		MinSettlementY = float.MaxValue;[0m
[7m[0m  		MaxSettlementX = float.MinValue;[0m
[7m[0m  		MaxSettlementY = float.MinValue;[0m
[7m[0m  		foreach (Settlement item4 in Settlement.All)[0m
[7m[0m  		{[0m
[7m[0m  			if (item4.Position.X < MinSettlementX)[0m
[7m[0m  			{[0m
[7m[0m  				MinSettlementX = item4.Position.X;[0m
  		}
  	}
  
> 	private void OnGame[7mLoad[0med(CampaignGameStarter starter)[0m
[7m[0m  	{[0m
> 		TroopRoster.CalculateCachedStatsOn[7mLoad[0m();[0m
[7m[0m  		_tickData = new CampaignTickCacheDataStore();[0m
> 		base.ObjectManager.PreAfter[7mLoad[0m();
> 		CampaignObjectManager.PreAfter[7mLoad[0m();
> 		IssueManager.PreAfter[7mLoad[0m();
> 		QuestManager.PreAfter[7mLoad[0m();
> 		base.ObjectManager.After[7mLoad[0m();
> 		CampaignObjectManager.After[7mLoad[0m();
> 		CharacterRelationManager.After[7mLoad[0m();
> 		FactionManager.After[7mLoad[0m();
> 		CampaignEventDispatcher.Instance.OnGameEarly[7mLoad[0med(starter);
> 		CampaignEventDispatcher.Instance.OnGame[7mLoad[0med(starter);[0m
[7m[0m  		InitializeForSavedGame();[0m
[7m[0m  		_tickData.InitializeDataCache();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void OnData[7mLoad[0mFinished(CampaignGameStarter starter)[0m
[7m[0m  	{[0m
[7m[0m  		_towns = new MBList<Town>();[0m
[7m[0m  		_castles = new MBList<Town>();[0m
[7m[0m  		_villages = new MBList<Village>();[0m
[7m[0m  		_hideouts = new MBList<Hideout>();[0m
[7m[0m  		for (int i = 0; i < Settlement.All.Count; i++)[0m
[7m[0m  		{[0m
[7m[0m  			Settlement settlement = Settlement.All[i];[0m
[7m[0m  			if (settlement.IsTown)[0m
[7m[0m  			{[0m
  		IsCraftingEnabled = true;
  		IsBannerEditorEnabled = true;
  		IsFaceGenEnabled = true;
> 		MapEventManager.OnAfter[7mLoad[0m();[0m
> 		SiegeEventManager.OnAfter[7mLoad[0m();[0m
[7m[0m  		KingdomManager.RegisterEvents();[0m
[7m[0m  		KingdomManager.OnSessionStart();[0m
[7m[0m  		CampaignInformationManager.RegisterEvents();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void DailyTickSettlement(Settlement settlement)[0m
[7m[0m  	{[0m
[7m[0m  		if (settlement.IsVillage)[0m
[7m[0m  		{[0m
[7m[0m  			settlement.Village.DailyTick();[0m
  		return lastPartyIndex;
  	}
  
> 	private void [7mLoad[0mMapScene()[0m
[7m[0m  	{[0m
[7m[0m  		_mapSceneWrapper = MapSceneCreator.CreateMapScene();[0m
[7m[0m  		_mapSceneWrapper.SetSceneLevels(new List<string> { "level_1", "level_2", "level_3", "siege", "raid", "burned" });[0m
> 		_mapSceneWrapper.[7mLoad[0m();[0m
[7m[0m  		_mapSceneWrapper.GetMapBorders(out var minimumPosition, out var maximumPosition, out var maximumHeight);[0m
[7m[0m  		MapMinimumPosition = minimumPosition;[0m
[7m[0m  		MapMaximumPosition = maximumPosition;[0m
[7m[0m  		MapMaximumHeight = maximumHeight;[0m
[7m[0m  		MapDiagonal = MapMinimumPosition.Distance(MapMaximumPosition);[0m
[7m[0m  		MapDiagonalSquared = MapDiagonal * MapDiagonal;[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mPlayerRegionSwitchCostFromLandToSea [0m[7m[0m= [0m[7m[0m(int)(MapDiagonal [0m[7m[0m* [0m
[7m[0m(float)Models.MapDistanceModel.RegionSwitchCostFromLandToSea [0m[7m[0m* [0m[7m[0m0.2f);[0m
[7m[0m  		PathFindingMaxCostLimit = Math.Max(PlayerRegionSwitchCostFromLandToSea * 100, (int)(MapDiagonal * 500f));[0m
> 		_mapSceneWrapper.After[7mLoad[0m();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void InitializeCachedLists()[0m
[7m[0m  	{[0m
[7m[0m  		MBObjectManager objectManager = Game.Current.ObjectManager;[0m
[7m[0m  		_characters = objectManager.GetObjectTypeList<CharacterObject>();[0m
[7m[0m  		_workshops = objectManager.GetObjectTypeList<WorkshopType>();[0m
[7m[0m  		_itemModifiers = objectManager.GetObjectTypeList<ItemModifier>();[0m
[7m[0m  		_itemModifierGroups = objectManager.GetObjectTypeList<ItemModifierGroup>();[0m
[7m[0m  		_concepts = objectManager.GetObjectTypeList<Concept>();[0m
  		GameSceneDataManager.Destroy();
  		CampaignInformationManager.DeRegisterEvents();
  		_campaignBehaviorManager?.ClearBehaviors();
> 		MBSave[7mLoad[0m.OnGameDestroy();[0m
[7m[0m  		Current = null;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void InitializeSinglePlayerReferences()[0m
[7m[0m  	{[0m
[7m[0m  		IsSinglePlayerReferencesInitialized = true;[0m
[7m[0m  		InitializeGamePlayReferences();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void CreateLists()[0m
  			string path3 = text + "meeting_scenes.xml";
  			if (File.Exists(path))
  			{
> 				GameSceneDataManager.Instance.[7mLoad[0mSPBattleScenes(path);[0m
[7m[0m  			}[0m
[7m[0m  			if (File.Exists(path2))[0m
[7m[0m  			{[0m
> 				GameSceneDataManager.Instance.[7mLoad[0mConversationScenes(path2);[0m
[7m[0m  			}[0m
[7m[0m  			if (File.Exists(path3))[0m
[7m[0m  			{[0m
> 				GameSceneDataManager.Instance.[7mLoad[0mMeetingScenes(path3);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void Set[7mLoad[0mingParameters(GameLoadingType gameLoadingType)[0m
[7m[0m  	{[0m
[7m[0m  		Current = this;[0m
> 		_game[7mLoad[0mingType = gameLoadingType;
> 		if (game[7mLoad[0mingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
[7m[0m  			Current.GameStarted = true;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void AddCampaignEventReceiver(CampaignEventReceiver receiver)[0m
[7m[0m  	{[0m
[7m[0m  		CampaignEventDispatcher.AddCampaignEventReceiver(receiver);[0m
[7m[0m  	}[0m
[7m[0m  [0m
  		_castles = new MBList<Town>();
  		_villages = new MBList<Village>();
  		_hideouts = new MBList<Hideout>();
> 		if (_game[7mLoad[0mingType != GameLoadingType.Editor)[0m
[7m[0m  		{[0m
[7m[0m  			CreateManagers();[0m
[7m[0m  		}[0m
[7m[0m  		CampaignGameStarter campaignGameStarter = new CampaignGameStarter(GameMenuManager, ConversationManager);[0m
[7m[0m  		SandBoxManager.Initialize(campaignGameStarter);[0m
[7m[0m  		base.GameManager.InitializeGameStarter(base.CurrentGame, campaignGameStarter);[0m
[7m[0m  		GameSceneDataManager.Initialize();[0m
> 		if (_game[7mLoad[0mingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
[7m[0m  			InitializeScenes();[0m
[7m[0m  		}[0m
[7m[0m  		base.GameManager.OnGameStart(base.CurrentGame, campaignGameStarter);[0m
[7m[0m  		base.CurrentGame.SetBasicModels(campaignGameStarter.Models);[0m
[7m[0m  		_gameModels = base.CurrentGame.AddGameModelsManager<GameModels>(campaignGameStarter.Models);[0m
[7m[0m  		CampaignTime.Initialize();[0m
[7m[0m  		base.CurrentGame.CreateGameManager();[0m
> 		if (_game[7mLoad[0mingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
> 			[7mInitializeDefaultCampaignObjects[0m();[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
[7m[0m  			MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime);[0m
[7m[0m  		}[0m
[7m[0m  		base.GameManager.BeginGameStart(base.CurrentGame);[0m
> 		if (_game[7mLoad[0mingType != GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
[7m[0m  			OnNewCampaignStart();[0m
[7m[0m  		}[0m
[7m[0m  		CreateLists();[0m
[7m[0m  		InitializeBasicObjectXmls();[0m
> 		if (_game[7mLoad[0mingType != GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
[7m[0m  			base.GameManager.OnNewCampaignStart(base.CurrentGame, campaignGameStarter);[0m
[7m[0m  		}[0m
> 		SandBoxManager.OnCampaignStart(campaignGameStarter, base.GameManager, _game[7mLoad[0mingType [0m[7m[0m== [0m
[7m[0mGameLoadingType.SavedCampaign);[0m
> 		if (_game[7mLoad[0mingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
> 			DetermineSavedStats(_game[7mLoad[0mingType);[0m
[7m[0m  		}[0m
> 		if (_game[7mLoad[0mingType != GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
[7m[0m  			AddCampaignBehaviorManager(new CampaignBehaviorManager(campaignGameStarter.CampaignBehaviors));[0m
[7m[0m  			base.GameManager.OnAfterCampaignStart(base.CurrentGame);[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
> 			base.GameManager.OnGame[7mLoad[0med(base.CurrentGame, campaignGameStarter);[0m
[7m[0m  			_campaignBehaviorManager.InitializeCampaignBehaviors(campaignGameStarter.CampaignBehaviors);[0m
> 			_campaignBehaviorManager.[7mLoad[0mBehaviorData();[0m
[7m[0m  			_campaignBehaviorManager.RegisterEvents();[0m
[7m[0m  		}[0m
[7m[0m  		foreach (INonReadyObjectHandler campaignBehavior in GetCampaignBehaviors<INonReadyObjectHandler>())[0m
[7m[0m  		{[0m
[7m[0m  			campaignBehavior.OnBeforeNonReadyObjectsDeleted();[0m
[7m[0m  		}[0m
> 		if (_game[7mLoad[0mingType != GameLoadingType.Tutorial)[0m
[7m[0m  		{[0m
[7m[0m  			campaignGameStarter.UnregisterNonReadyObjects();[0m
[7m[0m  		}[0m
> 		if (_game[7mLoad[0mingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  		{[0m
> 			[7mInitializeCampaignObjects[0mOnAfterLoad();[0m
[7m[0m  		}[0m
> 		else if (_game[7mLoad[0mingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.Tutorial)[0m
[7m[0m  		{[0m
[7m[0m  			CampaignObjectManager.InitializeOnNewGame();[0m
[7m[0m  		}[0m
[7m[0m  		InitializeCachedLists();[0m
[7m[0m  		InitializeDefaultEquipments();[0m
[7m[0m  		NameGenerator.Initialize();[0m
[7m[0m  		base.CurrentGame.OnGameStart();[0m
[7m[0m  		base.GameManager.OnGameInitializationFinished(base.CurrentGame);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void CalculateCachedStatsOn[7mLoad[0m()[0m
[7m[0m  	{[0m
> 		ItemRoster.CalculateCachedStatsOn[7mLoad[0m();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void InitializeBasicObjectXmls()[0m
[7m[0m  	{[0m
> 		base.ObjectManager.[7mLoad[0mXML("SPCultures");
> 		base.ObjectManager.[7mLoad[0mXML("Concepts");[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void [7mInitializeDefaultCampaignObjects[0m()[0m
[7m[0m  	{[0m
[7m[0m  		base.CurrentGame.InitializeDefaultGameObjects();[0m
[7m[0m  		DefaultItems = new DefaultItems();[0m
> 		base.CurrentGame.[7mLoad[0mBasicFiles();
> 		base.ObjectManager.[7mLoad[0mXML("Items");
> 		base.ObjectManager.[7mLoad[0mXML("EquipmentRosters");
> 		base.ObjectManager.[7mLoad[0mXML("partyTemplates");[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mWeaponDescription [0m[7m[0mweaponDescription [0m[7m[0m= [0m
[7m[0mMBObjectManager.Instance.GetObject<WeaponDescription>("OneHandedBastardSwordAlternative");[0m
[7m[0m  		if (weaponDescription != null)[0m
[7m[0m  		{[0m
[7m[0m  			weaponDescription.IsHiddenFromUI = true;[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mWeaponDescription [0m[7m[0mweaponDescription2 [0m[7m[0m= [0m
[7m[0mMBObjectManager.Instance.GetObject<WeaponDescription>("OneHandedBastardAxeAlternative");[0m
[7m[0m  		if (weaponDescription2 != null)[0m
[7m[0m  		{[0m
[7m[0m  			weaponDescription2.IsHiddenFromUI = true;[0m
[7m[0m  		}[0m
  		TournamentManager = new TournamentManager();
  	}
  
> 	private void [7mInitializeCampaignObjects[0mOnAfterLoad()[0m
[7m[0m  	{[0m
> 		CampaignObjectManager.InitializeOn[7mLoad[0m();
> 		FactionManager.PreAfter[7mLoad[0m();[0m
[7m[0m  		List<PerkObject> collection = AllPerks.Where((PerkObject x) => !x.IsTrash).ToList();[0m
[7m[0m  		AllPerks = new MBReadOnlyList<PerkObject>(collection);[0m
> 		LogEntryHistory.OnAfter[7mLoad[0m();[0m
[7m[0m  		foreach (Kingdom kingdom in Kingdoms)[0m
[7m[0m  		{[0m
[7m[0m  			foreach (Army army in kingdom.Armies)[0m
[7m[0m  			{[0m
> 				army.OnAfter[7mLoad[0m();[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void OnNewCampaignStart()[0m
[7m[0m  	{[0m
[7m[0m  		Game.Current.PlayerTroop = null;[0m
[7m[0m  		MapStateData = new MapStateData();[0m
> 		[7mInitializeDefaultCampaignObjects[0m();[0m
[7m[0m  		MainParty = MBObjectManager.Instance.CreateObject<MobileParty>("player_party");[0m
[7m[0m  		InitializeManagers();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	protected override void BeforeRegisterTypes(MBObjectManager objectManager)[0m
[7m[0m  	{[0m
[7m[0m  		objectManager.RegisterType<FeatObject>("feat", "Feats", 0u);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	protected override void OnRegisterTypes(MBObjectManager objectManager)[0m
  	private void OnNewGameCreatedInternal()
  	{
  		UniqueGameId = MiscHelper.GenerateCampaignId(12);
> 		_newGameVersion = MBSave[7mLoad[0m.CurrentVersion.ToString();[0m
[7m[0m  		PlatformID = ApplicationPlatform.CurrentPlatform.ToString();[0m
[7m[0m  		PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();[0m
[7m[0m  		TraitLevelingHelper.UpdateTraitXPAccordingToTraitLevels();[0m
[7m[0m  		TimeControlMode = CampaignTimeControlMode.Stop;[0m
[7m[0m  		_campaignEntitySystem = new EntitySystem<CampaignEntityComponent>();[0m
[7m[0m  		SiegeEventManager = new SiegeEventManager();[0m
[7m[0m  		MapEventManager = new MapEventManager();[0m
[7m[0m  		MapMarkerManager = new MapMarkerManager();[0m
[7m[0m  		MinSettlementX = float.MaxValue;[0m
[7m[0m  		MinSettlementY = float.MaxValue;[0m
  		PlayerFormationPreferences = _playerFormationPreferences.GetReadOnlyDictionary();
  	}
  
> 	protected override void Do[7mLoad[0mingForGameType(GameTypeLoadingStates [0m[7m[0mgameTypeLoadingState, [0m[7m[0mout [0m[7m[0mGameTypeLoadingStates [0m
[7m[0mnextState)[0m
[7m[0m  	{[0m
> 		nextState = GameType[7mLoad[0mingStates.None;
> 		switch (gameType[7mLoad[0mingState)[0m
[7m[0m  		{[0m
> 		case GameType[7mLoad[0mingStates.InitializeFirstStep:[0m
[7m[0m  			base.CurrentGame.Initialize();[0m
> 			nextState = GameType[7mLoad[0mingStates.WaitSecondStep;[0m
[7m[0m  			break;[0m
> 		case GameType[7mLoad[0mingStates.WaitSecondStep:
> 			nextState = GameType[7mLoad[0mingStates.LoadVisualsThirdState;[0m
[7m[0m  			break;[0m
> 		case GameType[7mLoad[0mingStates.LoadVisualsThirdState:[0m
[7m[0m  			if (GameMode == CampaignGameMode.Campaign)[0m
[7m[0m  			{[0m
> 				[7mLoad[0mMapScene();[0m
[7m[0m  			}[0m
> 			nextState = GameType[7mLoad[0mingStates.PostInitializeFourthState;[0m
[7m[0m  			break;[0m
> 		case GameType[7mLoad[0mingStates.PostInitializeFourthState:[0m
[7m[0m  		{[0m
[7m[0m  			CampaignGameStarter gameStarter = SandBoxManager.GameStarter;[0m
> 			if (_game[7mLoad[0mingType == GameLoadingType.SavedCampaign)[0m
[7m[0m  			{[0m
[7m[0m  				CheckMapUpdate();[0m
> 				OnData[7mLoad[0mFinished(gameStarter);[0m
[7m[0m  				CalculateCachedValues();[0m
> 				CalculateCachedStatsOn[7mLoad[0m();
> 				base.GameManager.OnAfterGame[7mLoad[0med(base.CurrentGame);
> 				OnGame[7mLoad[0med(gameStarter);[0m
[7m[0m  				OnSessionStart(gameStarter);[0m
[7m[0m  				foreach (Hero allAliveHero in Hero.AllAliveHeroes)[0m
[7m[0m  				{[0m
[7m[0m  					allAliveHero.CheckInvalidEquipmentsAndReplaceIfNeeded();[0m
[7m[0m  				}[0m
[7m[0m  				foreach (Hero deadOrDisabledHero in Hero.DeadOrDisabledHeroes)[0m
[7m[0m  				{[0m
[7m[0m  					deadOrDisabledHero.CheckInvalidEquipmentsAndReplaceIfNeeded();[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			else if (_game[7mLoad[0mingType == GameLoadingType.NewCampaign)[0m
[7m[0m  			{[0m
[7m[0m  				_campaignMapSceneXmlCrc = MapSceneWrapper.GetSceneXmlCrc();[0m
[7m[0m  				_campaignMapSceneNavigationMeshCrc = MapSceneWrapper.GetSceneNavigationMeshCrc();[0m
> 				OnData[7mLoad[0mFinished(gameStarter);[0m
[7m[0m  				CalculateCachedValues();[0m
> 				MBSave[7mLoad[0m.OnNewGame();[0m
[7m[0m  				InitializeMainParty();[0m
[7m[0m  				foreach (Settlement item in Settlement.All)[0m
[7m[0m  				{[0m
[7m[0m  					item.OnGameCreated();[0m
[7m[0m  				}[0m
[7m[0m  				MBObjectManager.Instance.RemoveTemporaryTypes();[0m
[7m[0m  				OnNewGameCreated(gameStarter);[0m
[7m[0m  				OnSessionStart(gameStarter);[0m
[7m[0m  				Debug.Print("Finished starting a new game.");[0m
[7m[0m  			}[0m
  		}
  	}
  
> 	private void DetermineSavedStats(Game[7mLoad[0mingType gameLoadingType)[0m
[7m[0m  	{[0m
[7m[0m  		if (_previouslyUsedModules == null)[0m
[7m[0m  		{[0m
[7m[0m  			_previouslyUsedModules = new MBList<string>();[0m
[7m[0m  		}[0m
[7m[0m  		if (_usedGameVersions == null)[0m
[7m[0m  		{[0m
[7m[0m  			_usedGameVersions = new MBList<string>();[0m
[7m[0m  		}[0m
> 		string text = MBSave[7mLoad[0m.CurrentVersion.ToString();
> 		string text2 = string.Join(MBSave[7mLoad[0m.ModuleCodeSeperator.ToString(), from x in ModuleHelper.GetActiveModules()
> 			select x.Id + MBSave[7mLoad[0m.ModuleVersionSeperator.ToString() + x.Version);[0m
[7m[0m  		if (_usedGameVersions.Count <= 0 || _usedGameVersions.Last() != text)[0m
[7m[0m  		{[0m
[7m[0m  			_usedGameVersions.Add(text);[0m
[7m[0m  		}[0m
[7m[0m  		if (_previouslyUsedModules.LastOrDefault() != text2)[0m
[7m[0m  		{[0m
[7m[0m  			_previouslyUsedModules.Add(text2);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m


 succeeded in 3869ms:
    1: using HarmonyLib;
    2: using TAOM.Core.Logging;
    3: using TaleWorlds.CampaignSystem;
    4: 
    5: namespace TAOM.Features.CulturalFeats.Hooks;
    6: 
    7: [HarmonyPatch(typeof(Campaign), "InitializeDefaultCampaignObjects")]
    8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
    9: public static class Campaign_InitializeDefaultCampaignObjects_Patch
   10: {
   11:     [HarmonyPostfix]
   12:     public static void Postfix()
   13:     {
   14:         TaomCulturalFeats.CreateAndRegister();
   15:         IoC.Resolve<IModLogger>().LogInfo("CulturalFeats: all TAOM cultural feats registered");
   16:     }
   17: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.ObjectSystem.MBObjectManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll' | Select-String -Pattern 'CreateObjectFromXmlNode|ReadObjectReferenceFromXml|RegisterPresumedObject|CreateObject' -Context 3,12 | Select-Object -First 220" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.ObjectSystem.dll' | Select-Object FullName,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem' -Filter 'Campaign.cs' | ForEach-Object { "'$i=0; Get-Content $_.FullName | ForEach-Object { $i++; if ($_ -match '"'InitializeDefaultCampaignObjects|InitializeBasicObjectXmls|OnNewCampaignStart|LoadXML\\(\"SPCultures\"|GameLoadingType.SavedCampaign|CreateLists\\(\\)') { '{0,5}: {1}' -f "'$i, $_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'MBObjectManager.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:

[32;1mFullName                                                                                                   [0m[32;1m Length[0m[32;1m Last[0m
[32;1m                                                                                                           [0m[32;1m       [0m[32;1m Writ[0m
[32;1m                                                                                                           [0m[32;1m       [0m[32;1m eTim[0m
[32;1m                                                                                                           [0m[32;1m       [0m[32;1m e[0m
[32;1m--------                                                                                                   [0m [32;1m------[0m [32;1m----[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.ObjectSystem.dll  53184 5/2…


 succeeded in 551ms:
 1141: 	private void CreateLists()
 1355: 		if (gameLoadingType == GameLoadingType.SavedCampaign)
 1387: 		if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)
 1396: 		if (_gameLoadingType == GameLoadingType.SavedCampaign)
 1398: 			InitializeDefaultCampaignObjects();
 1405: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1407: 			OnNewCampaignStart();
 1409: 		CreateLists();
 1410: 		InitializeBasicObjectXmls();
 1411: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1413: 			base.GameManager.OnNewCampaignStart(base.CurrentGame, campaignGameStarter);
 1415: 		SandBoxManager.OnCampaignStart(campaignGameStarter, base.GameManager, _gameLoadingType == GameLoadingType.SavedCampaign);
 1416: 		if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)
 1420: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1440: 		if (_gameLoadingType == GameLoadingType.SavedCampaign)
 1460: 	private void InitializeBasicObjectXmls()
 1462: 		base.ObjectManager.LoadXML("SPCultures");
 1466: 	private void InitializeDefaultCampaignObjects()
 1520: 	private void OnNewCampaignStart()
 1524: 		InitializeDefaultCampaignObjects();
 1663: 			if (_gameLoadingType == GameLoadingType.SavedCampaign)

 succeeded in 556ms:
E:\Decompiled_Bannerlord\Core\TaleWorlds.ObjectSystem\TaleWorlds.ObjectSystem\MBObjectManager.cs

 succeeded in 1466ms:

  		return registeredObject as T;
  	}
  
> 	public T [7mRegisterPresumedObject[0m<T>(T obj) where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		RegisterObjectInternalWithoutTypeId(obj, presumed: true, out var registeredObject);[0m
[7m[0m  		return registeredObject as T;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	internal void TryRegisterObjectWithoutInitialization(MBObjectBase obj)[0m
[7m[0m  	{[0m
[7m[0m  		Type type = obj.GetType();[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ObjectClass == type)[0m
[7m[0m  			{[0m
  		return mBList;
  	}
  
> 	public IList<MBObjectBase> [7mCreateObject[0mTypeList(Type objectClassType)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (!(objectTypeRecord.ObjectClass == objectClassType))[0m
[7m[0m  			{[0m
[7m[0m  				continue;[0m
[7m[0m  			}[0m
[7m[0m  			List<MBObjectBase> list = new List<MBObjectBase>();[0m
[7m[0m  			foreach (object? item2 in objectTypeRecord)[0m
[7m[0m  			{[0m
[7m[0m  				MBObjectBase item = item2 as MBObjectBase;[0m
[7m[0m  				list.Add(item);[0m
  		}
  	}
  
> 	public MBObjectBase [7mCreateObjectFromXmlNode[0m(XmlNode node)[0m
[7m[0m  	{[0m
> 		return [7mCreateObjectFromXmlNode[0m(node, node.Name);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public MBObjectBase [7mCreateObjectFromXmlNode[0m(XmlNode node, string typeName)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ElementName == typeName)[0m
[7m[0m  			{[0m
[7m[0m  				string value = node.Attributes["id"].Value;[0m
[7m[0m  				MBObjectBase presumedObject = GetPresumedObject(objectTypeRecord.ElementName, value);[0m
[7m[0m  				presumedObject.Deserialize(this, node);[0m
[7m[0m  				presumedObject.AfterInitialized();[0m
[7m[0m  				return presumedObject;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  		return null;
  	}
  
> 	public MBObjectBase [7mCreateObject[0mWithoutDeserialize(XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		string name = node.Name;[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ElementName == name)[0m
[7m[0m  			{[0m
[7m[0m  				string value = node.Attributes["id"].Value;[0m
[7m[0m  				MBObjectBase presumedObject = GetPresumedObject(objectTypeRecord.ElementName, value);[0m
[7m[0m  				presumedObject.Initialize();[0m
[7m[0m  				presumedObject.AfterInitialized();[0m
[7m[0m  				return presumedObject;[0m
[7m[0m  			}[0m
  		}
  	}
  
> 	public T [7mReadObjectReferenceFromXml[0m<T>(string attributeName, XmlNode node) where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		if (node.Attributes[attributeName] == null)[0m
[7m[0m  		{[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
[7m[0m  		string value = node.Attributes[attributeName].Value;[0m
[7m[0m  		string text = value.Split(".".ToCharArray())[0];[0m
[7m[0m  		if (text == value)[0m
[7m[0m  		{[0m
[7m[0m  			throw new MBInvalidReferenceException(value);[0m
[7m[0m  		}[0m
[7m[0m  		string text2 = value.Split(".".ToCharArray())[1];[0m
  		return GetPresumedObject(text, text2) as T;
  	}
  
> 	public MBObjectBase [7mReadObjectReferenceFromXml[0m(string attributeName, Type objectType, XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		if (node.Attributes[attributeName] == null)[0m
[7m[0m  		{[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
[7m[0m  		string value = node.Attributes[attributeName].Value;[0m
[7m[0m  		string text = value.Split(".".ToCharArray())[0];[0m
[7m[0m  		if (text == value)[0m
[7m[0m  		{[0m
[7m[0m  			throw new MBInvalidReferenceException(value);[0m
[7m[0m  		}[0m
[7m[0m  		string text2 = value.Split(".".ToCharArray())[1];[0m
  		return GetPresumedObject(text, text2);
  	}
  
> 	public T [7mCreateObject[0m<T>(string stringId) where T : MBObjectBase, new()[0m
[7m[0m  	{[0m
[7m[0m  		T val = new T[0m
[7m[0m  		{[0m
[7m[0m  			StringId = stringId[0m
[7m[0m  		};[0m
[7m[0m  		RegisterObject(val);[0m
[7m[0m  		if (_handlers != null)[0m
[7m[0m  		{[0m
[7m[0m  			foreach (IObjectManagerHandler handler in _handlers)[0m
[7m[0m  			{[0m
> 				handler.After[7mCreateObject[0m(val);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return val;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public T [7mCreateObject[0m<T>() where T : MBObjectBase, new()[0m
[7m[0m  	{[0m
> 		return [7mCreateObject[0m<T>(typeof(T).Name.ToString() + "_1");[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void DebugPrint(PrintOutputDelegate printOutput)[0m
[7m[0m  	{[0m
[7m[0m  		printOutput("-Printing MBObjectManager Debug-");[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			printOutput(objectTypeRecord.DebugBasicDump());[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void AddHandler(IObjectManagerHandler handler)[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.ObjectSystem.MBObjectManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll' | Select-String -Pattern 'GetPresumedObject|RegisterObjectInternalWithoutTypeId|MBObjectBase presumedObject' -Context 2,18 | Select-Object -First 220" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.ObjectSystem\\TaleWorlds.ObjectSystem\\MBObjectManager.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'GetPresumedObject|CreateObjectFromXmlNode|RegisterPresumedObject|RegisterObjectInternalWithoutTypeId' -or ("'$i -ge 200 -and $i -le 270)) { '"'{0,5}: {1}' -f "'$i, $_ } } | Select-Object -First 240' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CultureObject.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 140 -and $i -le 260) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Campaign.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if (($i -ge 1390 -and $i -le 1412) -or ($i -ge 1460 -and $i -le 1475) -or ($i -ge 1520 -and $i -le 1528)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 561ms:
  200: 
  201: 		void IObjectTypeRecord.RegisterMBObject(MBObjectBase obj, bool presumed, out MBObjectBase registeredObject)
  202: 		{
  203: 			if (obj is T)
  204: 			{
  205: 				RegisterObject(obj as T, presumed, out registeredObject);
  206: 			}
  207: 			else
  208: 			{
  209: 				registeredObject = null;
  210: 			}
  211: 		}
  212: 
  213: 		internal void RegisterObject(T obj, bool presumed, out MBObjectBase registeredObject)
  214: 		{
  215: 			if (_registeredObjects.TryGetValue(obj.StringId, out var value))
  216: 			{
  217: 				if (value == obj || presumed)
  218: 				{
  219: 					registeredObject = value;
  220: 					return;
  221: 				}
  222: 				string text;
  223: 				long num;
  224: 				(text, num) = GetIdParts(obj.StringId);
  225: 				if (_registeredObjects.ContainsKey(obj.StringId))
  226: 				{
  227: 					num = _objCount;
  228: 					obj.StringId = text + num;
  229: 					while (_registeredObjects.ContainsKey(obj.StringId))
  230: 					{
  231: 						num++;
  232: 						obj.StringId = text + num;
  233: 					}
  234: 				}
  235: 			}
  236: 			_registeredObjects.Add(obj.StringId, obj);
  237: 			obj.Id = GetNewId();
  238: 			_registeredObjectsWithGuid.Add(obj.Id, obj);
  239: 			RegisteredObjectsList.Add(obj);
  240: 			obj.IsReady = !presumed;
  241: 			obj.OnRegistered();
  242: 			registeredObject = obj;
  243: 		}
  244: 
  245: 		private (string str, long number) GetIdParts(string stringId)
  246: 		{
  247: 			int num = stringId.Length - 1;
  248: 			while (num > 0 && char.IsDigit(stringId[num]))
  249: 			{
  250: 				num--;
  251: 			}
  252: 			string item = stringId.Substring(0, num + 1);
  253: 			long result = 0L;
  254: 			if (num < stringId.Length - 1)
  255: 			{
  256: 				long.TryParse(stringId.Substring(num + 1, stringId.Length - num - 1), out result);
  257: 			}
  258: 			return (str: item, number: result);
  259: 		}
  260: 
  261: 		void IObjectTypeRecord.UnregisterMBObject(MBObjectBase obj)
  262: 		{
  263: 			if (obj is T)
  264: 			{
  265: 				UnregisterObject((T)obj);
  266: 				return;
  267: 			}
  268: 			throw new MBIllegalRegisterException();
  269: 		}
  270: 
  448: 		RegisterObjectInternalWithoutTypeId(obj, presumed: false, out var registeredObject);
  452: 	public T RegisterPresumedObject<T>(T obj) where T : MBObjectBase
  454: 		RegisterObjectInternalWithoutTypeId(obj, presumed: true, out var registeredObject);
  472: 	private void RegisterObjectInternalWithoutTypeId<T>(T obj, bool presumed, out MBObjectBase registeredObject) where T : MBObjectBase
  485: 		Debug.FailedAssert(typeof(T).Name + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "RegisterObjectInternalWithoutTypeId", 178);
  713: 	private MBObjectBase GetPresumedObject(string typeName, string objectName, bool isInitialize = false)
  733: 		Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetPresumedObject", 466);
 1392: 				MBObjectBase presumedObject = GetPresumedObject(typeName, value, isInitialize: true);
 1399: 	public MBObjectBase CreateObjectFromXmlNode(XmlNode node)
 1401: 		return CreateObjectFromXmlNode(node, node.Name);
 1404: 	public MBObjectBase CreateObjectFromXmlNode(XmlNode node, string typeName)
 1411: 				MBObjectBase presumedObject = GetPresumedObject(objectTypeRecord.ElementName, value);
 1428: 				MBObjectBase presumedObject = GetPresumedObject(objectTypeRecord.ElementName, value);
 1514: 		return GetPresumedObject(text, text2) as T;
 1534: 		return GetPresumedObject(text, text2);

 succeeded in 1769ms:

  	public T RegisterObject<T>(T obj) where T : MBObjectBase
  	{
> 		[7mRegisterObjectInternalWithoutTypeId[0m(obj, presumed: false, out var registeredObject);[0m
[7m[0m  		return registeredObject as T;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public T RegisterPresumedObject<T>(T obj) where T : MBObjectBase[0m
[7m[0m  	{[0m
> 		[7mRegisterObjectInternalWithoutTypeId[0m(obj, presumed: true, out var registeredObject);[0m
[7m[0m  		return registeredObject as T;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	internal void TryRegisterObjectWithoutInitialization(MBObjectBase obj)[0m
[7m[0m  	{[0m
[7m[0m  		Type type = obj.GetType();[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ObjectClass == type)[0m
[7m[0m  			{[0m
[7m[0m  				objectTypeRecord.RegisterMBObjectWithoutInitialization(obj);[0m
[7m[0m  				return;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mDebug.FailedAssert(obj.GetType().Name [0m[7m[0m+ [0m[7m[0m" [0m[7m[0mcould [0m[7m[0mnot [0m[7m[0mbe [0m[7m[0mfound [0m[7m[0min [0m[7m[0mMBObjectManager [0m[7m[0mobjectTypeRecords!", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", [0m
[7m[0m"TryRegisterObjectWithoutInitialization", [0m[7m[0m161);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private void [7mRegisterObjectInternalWithoutTypeId[0m<T>(T [0m[7m[0mobj, [0m[7m[0mbool [0m[7m[0mpresumed, [0m[7m[0mout [0m[7m[0mMBObjectBase [0m[7m[0mregisteredObject) [0m[7m[0mwhere [0m
[7m[0mT [0m[7m[0m: [0m[7m[0mMBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type type = obj.GetType();[0m
[7m[0m  		type = typeof(T);[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ObjectClass == type)[0m
[7m[0m  			{[0m
[7m[0m  				objectTypeRecord.RegisterMBObject(obj, presumed, out registeredObject);[0m
[7m[0m  				return;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		registeredObject = null;[0m
> 		Debug.FailedAssert(typeof(T).Name + " could not be found in MBObjectManager objectTypeRecords!", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", 
"[7mRegisterObjectInternalWithoutTypeId[0m", [0m[7m[0m178);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void UnregisterObject(MBObjectBase obj)[0m
[7m[0m  	{[0m
[7m[0m  		if (obj == null)[0m
[7m[0m  		{[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
[7m[0m  		Type type = obj.GetType();[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (type == objectTypeRecord.ObjectClass)[0m
[7m[0m  			{[0m
[7m[0m  				objectTypeRecord.UnregisterMBObject(obj);[0m
[7m[0m  				AfterUnregisterObject(obj);[0m
[7m[0m  				return;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  	}
  
> 	private MBObjectBase [7mGetPresumedObject[0m(string typeName, string objectName, bool isInitialize = false)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ElementName == typeName)[0m
[7m[0m  			{[0m
[7m[0m  				MBObjectBase mBObject = objectTypeRecord.GetMBObject(objectName);[0m
[7m[0m  				if (mBObject != null)[0m
[7m[0m  				{[0m
[7m[0m  					return mBObject;[0m
[7m[0m  				}[0m
[7m[0m  				if (objectTypeRecord.AutoCreate)[0m
[7m[0m  				{[0m
[7m[0m  					mBObject = objectTypeRecord.CreatePresumedMBObject(objectName);[0m
[7m[0m  					objectTypeRecord.RegisterMBObject(mBObject, presumed: true, out var registeredObject);[0m
[7m[0m  					return registeredObject;[0m
[7m[0m  				}[0m
[7m[0m  				throw new MBCanNotCreatePresumedObjectException();[0m
[7m[0m  			}[0m
  		}
> 		Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "[7mGetPresumedObject[0m", [0m
[7m[0m466);[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MBReadOnlyList<T> GetObjectTypeList<T>() where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type typeFromHandle = typeof(T);[0m
[7m[0m  		if (typeFromHandle.IsSealed)[0m
[7m[0m  		{[0m
[7m[0m  			foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  			{[0m
[7m[0m  				if (objectTypeRecord.ObjectClass == typeFromHandle)[0m
[7m[0m  				{[0m
[7m[0m  					return ((ObjectTypeRecord<T>)objectTypeRecord).GetObjectsList();[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mDebug.FailedAssert(typeof(T).Name [0m[7m[0m+ [0m[7m[0m" [0m[7m[0mcould [0m[7m[0mnot [0m[7m[0mbe [0m[7m[0mfound [0m[7m[0min [0m[7m[0mMBObjectManager [0m[7m[0mobjectTypeRecords!", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", [0m[7m[0m"GetObjectTypeList", [0m
[7m[0m503);[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
  			{
  				string value = xmlNode.Attributes["id"].Value;
> 				[7mMBObjectBase presumedObject[0m = GetPresumedObject(typeName, value, isInitialize: true);[0m
[7m[0m  				presumedObject.Deserialize(this, xmlNode);[0m
[7m[0m  				presumedObject.AfterInitialized();[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MBObjectBase CreateObjectFromXmlNode(XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		return CreateObjectFromXmlNode(node, node.Name);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MBObjectBase CreateObjectFromXmlNode(XmlNode node, string typeName)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ElementName == typeName)[0m
[7m[0m  			{[0m
[7m[0m  				string value = node.Attributes["id"].Value;[0m
> 				[7mMBObjectBase presumedObject[0m = GetPresumedObject(objectTypeRecord.ElementName, value);[0m
[7m[0m  				presumedObject.Deserialize(this, node);[0m
[7m[0m  				presumedObject.AfterInitialized();[0m
[7m[0m  				return presumedObject;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MBObjectBase CreateObjectWithoutDeserialize(XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		string name = node.Name;[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			if (objectTypeRecord.ElementName == name)[0m
[7m[0m  			{[0m
[7m[0m  				string value = node.Attributes["id"].Value;[0m
> 				[7mMBObjectBase presumedObject[0m = GetPresumedObject(objectTypeRecord.ElementName, value);[0m
[7m[0m  				presumedObject.Initialize();[0m
[7m[0m  				presumedObject.AfterInitialized();[0m
[7m[0m  				return presumedObject;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void UnregisterNonReadyObjects()[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  		{[0m
[7m[0m  			List<MBObjectBase> list = new List<MBObjectBase>();[0m
[7m[0m  			foreach (MBObjectBase item in objectTypeRecord)[0m
[7m[0m  			{[0m
[7m[0m  				if (!item.IsReady)[0m
[7m[0m  				{[0m
[7m[0m  					list.Add(item);[0m
  			throw new MBInvalidReferenceException(value);
  		}
> 		return [7mGetPresumedObject[0m(text, text2) as T;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MBObjectBase ReadObjectReferenceFromXml(string attributeName, Type objectType, XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		if (node.Attributes[attributeName] == null)[0m
[7m[0m  		{[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
[7m[0m  		string value = node.Attributes[attributeName].Value;[0m
[7m[0m  		string text = value.Split(".".ToCharArray())[0];[0m
[7m[0m  		if (text == value)[0m
[7m[0m  		{[0m
[7m[0m  			throw new MBInvalidReferenceException(value);[0m
[7m[0m  		}[0m
[7m[0m  		string text2 = value.Split(".".ToCharArray())[1];[0m
[7m[0m  		if (text == string.Empty || text2 == string.Empty)[0m
[7m[0m  		{[0m
[7m[0m  			throw new MBInvalidReferenceException(value);[0m
  		}
> 		return [7mGetPresumedObject[0m(text, text2);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public T CreateObject<T>(string stringId) where T : MBObjectBase, new()[0m
[7m[0m  	{[0m
[7m[0m  		T val = new T[0m
[7m[0m  		{[0m
[7m[0m  			StringId = stringId[0m
[7m[0m  		};[0m
[7m[0m  		RegisterObject(val);[0m
[7m[0m  		if (_handlers != null)[0m
[7m[0m  		{[0m
[7m[0m  			foreach (IObjectManagerHandler handler in _handlers)[0m
[7m[0m  			{[0m
[7m[0m  				handler.AfterCreateObject(val);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return val;[0m
[7m[0m  	}[0m


 succeeded in 2012ms:
  140: 	public MBEquipmentRoster DefaultCivilianEquipmentRoster { get; private set; }
  141: 
  142: 	public MBEquipmentRoster DefaultStealthEquipmentRoster { get; private set; }
  143: 
  144: 	public MBEquipmentRoster DuelPresetEquipmentRoster { get; private set; }
  145: 
  146: 	public MBEquipmentRoster MarriageBrideEquipmentRoster { get; private set; }
  147: 
  148: 	public CharacterObject BanditChief { get; private set; }
  149: 
  150: 	public CharacterObject BanditRaider { get; private set; }
  151: 
  152: 	public CharacterObject BanditBandit { get; private set; }
  153: 
  154: 	public CharacterObject BanditBoss { get; private set; }
  155: 
  156: 	public MBBodyProperty DefaultCharacterCreationBodyProperty { get; private set; }
  157: 
  158: 	public TextObject EncyclopediaText { get; private set; }
  159: 
  160: 	public CampaignVec2 StartingPoint { get; private set; }
  161: 
  162: 	public PartyTemplateObject DefaultPartyTemplate { get; private set; }
  163: 
  164: 	public PartyTemplateObject VillagerPartyTemplate { get; private set; }
  165: 
  166: 	public PartyTemplateObject FishingPartyTemplate { get; private set; }
  167: 
  168: 	public PartyTemplateObject MilitiaPartyTemplate { get; private set; }
  169: 
  170: 	public PartyTemplateObject RebelsPartyTemplate { get; private set; }
  171: 
  172: 	public MBList<PartyTemplateObject> CaravanPartyTemplates { get; private set; }
  173: 
  174: 	public MBList<PartyTemplateObject> EliteCaravanPartyTemplates { get; private set; }
  175: 
  176: 	public MBList<ShipHull> AvailableShipHulls { get; private set; }
  177: 
  178: 	public PartyTemplateObject BanditBossPartyTemplate { get; private set; }
  179: 
  180: 	public PartyTemplateObject VassalRewardTroopsPartyTemplate { get; private set; }
  181: 
  182: 	public PartyTemplateObject SettlementPatrolPartyTemplateWeak { get; private set; }
  183: 
  184: 	public PartyTemplateObject SettlementPatrolPartyTemplateModerate { get; private set; }
  185: 
  186: 	public PartyTemplateObject SettlementPatrolPartyTemplateStrong { get; private set; }
  187: 
  188: 	public PartyTemplateObject SettlementPatrolPartyTemplateNaval { get; private set; }
  189: 
  190: 	public MBReadOnlyList<ItemObject> VassalRewardItems { get; private set; }
  191: 
  192: 	public MBReadOnlyList<ItemObject> BannerBearerReplacementWeapons { get; private set; }
  193: 
  194: 	public MBReadOnlyList<TextObject> MaleNameList => _maleNameList;
  195: 
  196: 	public MBReadOnlyList<TextObject> FemaleNameList => _femaleNameList;
  197: 
  198: 	public MBReadOnlyList<TextObject> ClanNameList => _clanNameList;
  199: 
  200: 	public MBReadOnlyList<FeatObject> CultureFeats => _cultureFeats;
  201: 
  202: 	public MBReadOnlyList<PolicyObject> DefaultPolicyList => _defaultPolicyList;
  203: 
  204: 	public MBReadOnlyList<int> PossibleClanBannerIconsIDs { get; private set; }
  205: 
  206: 	public MBReadOnlyList<CharacterObject> NotableTemplates { get; private set; }
  207: 
  208: 	public MBReadOnlyList<CharacterObject> RebelliousHeroTemplates { get; private set; }
  209: 
  210: 	public MBReadOnlyList<CharacterObject> LordTemplates { get; private set; }
  211: 
  212: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForOneParticipant { get; private set; }
  213: 
  214: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForTwoParticipant { get; private set; }
  215: 
  216: 	public MBReadOnlyList<CharacterObject> TournamentTeamTemplatesForFourParticipant { get; private set; }
  217: 
  218: 	public MBReadOnlyList<CharacterObject> BasicMercenaryTroops { get; private set; }
  219: 
  220: 	public int MilitiaBonus { get; set; }
  221: 
  222: 	public int ProsperityBonus { get; set; }
  223: 
  224: 	public BoardGameType BoardGame { get; private set; }
  225: 
  226: 	public float NavalFactor { get; private set; }
  227: 
  228: 	internal static void AutoGeneratedStaticCollectObjectsCultureObject(object o, List<object> collectedObjects)
  229: 	{
  230: 		((CultureObject)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  231: 	}
  232: 
  233: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  234: 	{
  235: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  236: 	}
  237: 
  238: 	public bool HasTrait(CultureTrait trait)
  239: 	{
  240: 		return Enumerable.Contains(Traits, trait);
  241: 	}
  242: 
  243: 	public bool HasFeat(FeatObject feat)
  244: 	{
  245: 		return _cultureFeats.Contains(feat);
  246: 	}
  247: 
  248: 	public IEnumerable<FeatObject> GetCulturalFeats(Func<FeatObject, bool> predicate = null)
  249: 	{
  250: 		foreach (FeatObject cultureFeat in _cultureFeats)
  251: 		{
  252: 			if (predicate == null || predicate(cultureFeat))
  253: 			{
  254: 				yield return cultureFeat;
  255: 			}
  256: 		}
  257: 	}
  258: 
  259: 	public override string ToString()
  260: 	{

 succeeded in 2253ms:
 1390: 		}
 1391: 		base.GameManager.OnGameStart(base.CurrentGame, campaignGameStarter);
 1392: 		base.CurrentGame.SetBasicModels(campaignGameStarter.Models);
 1393: 		_gameModels = base.CurrentGame.AddGameModelsManager<GameModels>(campaignGameStarter.Models);
 1394: 		CampaignTime.Initialize();
 1395: 		base.CurrentGame.CreateGameManager();
 1396: 		if (_gameLoadingType == GameLoadingType.SavedCampaign)
 1397: 		{
 1398: 			InitializeDefaultCampaignObjects();
 1399: 		}
 1400: 		else
 1401: 		{
 1402: 			MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime);
 1403: 		}
 1404: 		base.GameManager.BeginGameStart(base.CurrentGame);
 1405: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1406: 		{
 1407: 			OnNewCampaignStart();
 1408: 		}
 1409: 		CreateLists();
 1410: 		InitializeBasicObjectXmls();
 1411: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1412: 		{
 1460: 	private void InitializeBasicObjectXmls()
 1461: 	{
 1462: 		base.ObjectManager.LoadXML("SPCultures");
 1463: 		base.ObjectManager.LoadXML("Concepts");
 1464: 	}
 1465: 
 1466: 	private void InitializeDefaultCampaignObjects()
 1467: 	{
 1468: 		base.CurrentGame.InitializeDefaultGameObjects();
 1469: 		DefaultItems = new DefaultItems();
 1470: 		base.CurrentGame.LoadBasicFiles();
 1471: 		base.ObjectManager.LoadXML("Items");
 1472: 		base.ObjectManager.LoadXML("EquipmentRosters");
 1473: 		base.ObjectManager.LoadXML("partyTemplates");
 1474: 		WeaponDescription weaponDescription = MBObjectManager.Instance.GetObject<WeaponDescription>("OneHandedBastardSwordAlternative");
 1475: 		if (weaponDescription != null)
 1520: 	private void OnNewCampaignStart()
 1521: 	{
 1522: 		Game.Current.PlayerTroop = null;
 1523: 		MapStateData = new MapStateData();
 1524: 		InitializeDefaultCampaignObjects();
 1525: 		MainParty = MBObjectManager.Instance.CreateObject<MobileParty>("player_party");
 1526: 		InitializeManagers();
 1527: 	}
 1528: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.ObjectSystem\\TaleWorlds.ObjectSystem\\MBObjectManager.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if (($i -ge 713 -and $i -le 735) -or ($i -ge 1399 -and $i -le 1418)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CultureObject.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 377 -and $i -le 391) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Campaign.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 1520 -and $i -le 1526) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Campaign.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 1396 -and $i -le 1410) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 568ms:
 1396: 		if (_gameLoadingType == GameLoadingType.SavedCampaign)
 1397: 		{
 1398: 			InitializeDefaultCampaignObjects();
 1399: 		}
 1400: 		else
 1401: 		{
 1402: 			MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime);
 1403: 		}
 1404: 		base.GameManager.BeginGameStart(base.CurrentGame);
 1405: 		if (_gameLoadingType != GameLoadingType.SavedCampaign)
 1406: 		{
 1407: 			OnNewCampaignStart();
 1408: 		}
 1409: 		CreateLists();
 1410: 		InitializeBasicObjectXmls();

 succeeded in 820ms:
  713: 	private MBObjectBase GetPresumedObject(string typeName, string objectName, bool isInitialize = false)
  714: 	{
  715: 		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)
  716: 		{
  717: 			if (objectTypeRecord.ElementName == typeName)
  718: 			{
  719: 				MBObjectBase mBObject = objectTypeRecord.GetMBObject(objectName);
  720: 				if (mBObject != null)
  721: 				{
  722: 					return mBObject;
  723: 				}
  724: 				if (objectTypeRecord.AutoCreate)
  725: 				{
  726: 					mBObject = objectTypeRecord.CreatePresumedMBObject(objectName);
  727: 					objectTypeRecord.RegisterMBObject(mBObject, presumed: true, out var registeredObject);
  728: 					return registeredObject;
  729: 				}
  730: 				throw new MBCanNotCreatePresumedObjectException();
  731: 			}
  732: 		}
  733: 		Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetPresumedObject", 466);
  734: 		return null;
  735: 	}
 1399: 	public MBObjectBase CreateObjectFromXmlNode(XmlNode node)
 1400: 	{
 1401: 		return CreateObjectFromXmlNode(node, node.Name);
 1402: 	}
 1403: 
 1404: 	public MBObjectBase CreateObjectFromXmlNode(XmlNode node, string typeName)
 1405: 	{
 1406: 		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)
 1407: 		{
 1408: 			if (objectTypeRecord.ElementName == typeName)
 1409: 			{
 1410: 				string value = node.Attributes["id"].Value;
 1411: 				MBObjectBase presumedObject = GetPresumedObject(objectTypeRecord.ElementName, value);
 1412: 				presumedObject.Deserialize(this, node);
 1413: 				presumedObject.AfterInitialized();
 1414: 				return presumedObject;
 1415: 			}
 1416: 		}
 1417: 		return null;
 1418: 	}

 succeeded in 1061ms:
  377: 			else if (item5.Name == "male_names")
  378: 			{
  379: 				foreach (XmlNode childNode2 in item5.ChildNodes)
  380: 				{
  381: 					_maleNameList.Add(new TextObject(childNode2.Attributes["name"].Value));
  382: 				}
  383: 			}
  384: 			else if (item5.Name == "female_names")
  385: 			{
  386: 				foreach (XmlNode childNode3 in item5.ChildNodes)
  387: 				{
  388: 					_femaleNameList.Add(new TextObject(childNode3.Attributes["name"].Value));
  389: 				}
  390: 			}
  391: 			else if (item5.Name == "clan_names")

 succeeded in 1304ms:
 1520: 	private void OnNewCampaignStart()
 1521: 	{
 1522: 		Game.Current.PlayerTroop = null;
 1523: 		MapStateData = new MapStateData();
 1524: 		InitializeDefaultCampaignObjects();
 1525: 		MainParty = MBObjectManager.Instance.CreateObject<MobileParty>("player_party");
 1526: 		InitializeManagers();

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CultureObject.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'cultural_feats' -or ("'$i -ge 405 -and $i -le 420)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 464ms:
  398: 			else if (item5.Name == "cultural_feats")
  405: 					if (featObject != null)
  406: 					{
  407: 						_cultureFeats.Add(featObject);
  408: 					}
  409: 				}
  410: 			}
  411: 			else if (item5.Name == "possible_clan_banner_icon_ids")
  412: 			{
  413: 				foreach (XmlNode childNode6 in item5.ChildNodes)
  414: 				{
  415: 					int.TryParse(childNode6.Attributes["id"].Value, out var result2);
  416: 					mBList.Add(result2);
  417: 				}
  418: 			}
  419: 			else if (item5.Name == "notable_templates")
  420: 			{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CultureObject.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 396 -and $i -le 409) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 459ms:
  396: 				}
  397: 			}
  398: 			else if (item5.Name == "cultural_feats")
  399: 			{
  400: 				foreach (XmlNode childNode5 in item5.ChildNodes)
  401: 				{
  402: 					string value = childNode5.Attributes["id"].Value;
  403: 					FeatObject featObject = (FeatObject)MBObjectManager.Instance.CreateObjectFromXmlNode(childNode5);
  404: 					Debug.Print("Reading feat with id " + value + ",  in Culture  " + base.StringId);
  405: 					if (featObject != null)
  406: 					{
  407: 						_cultureFeats.Add(featObject);
  408: 					}
  409: 				}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.ObjectSystem\\TaleWorlds.ObjectSystem\\MBObjectManager.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'RegisterType' -or ("'$i -ge 580 -and $i -le 630)) { '"'{0,5}: {1}' -f "'$i, $_ } } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.ObjectSystem.MBObjectManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll' | Select-String -Pattern 'RegisterType<' -Context 2,8 | Select-Object -First 120" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Campaign.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($i -ge 1530 -and $i -le 1540) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Campaign 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'RegisterType<FeatObject>' -Context 2,3" in C:\Users\mikew\source\repos\TAOM
 succeeded in 525ms:
  376: 	public void RegisterType<T>(string classPrefix, string classListPrefix, uint typeId, bool autoCreateInstance = true, bool isTemporary = false) where T : MBObjectBase
  380: 			Debug.FailedAssert(new MBTooManyRegisteredTypesException().ToString(), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "RegisterType", 64);
  580: 				if (objectTypeRecord.ObjectClass == typeFromHandle)
  581: 				{
  582: 					return ((ObjectTypeRecord<T>)objectTypeRecord).GetObject(objectName);
  583: 				}
  584: 			}
  585: 		}
  586: 		else
  587: 		{
  588: 			foreach (IObjectTypeRecord objectTypeRecord2 in ObjectTypeRecords)
  589: 			{
  590: 				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass) && objectTypeRecord2.GetMBObject(objectName) is T result)
  591: 				{
  592: 					return result;
  593: 				}
  594: 			}
  595: 		}
  596: 		return null;
  597: 	}
  598: 
  599: 	public T GetFirstObject<T>() where T : MBObjectBase
  600: 	{
  601: 		Type typeFromHandle = typeof(T);
  602: 		if (typeFromHandle.IsSealed)
  603: 		{
  604: 			foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)
  605: 			{
  606: 				if (objectTypeRecord.ObjectClass == typeFromHandle)
  607: 				{
  608: 					return ((ObjectTypeRecord<T>)objectTypeRecord).GetFirstObject();
  609: 				}
  610: 			}
  611: 		}
  612: 		else
  613: 		{
  614: 			foreach (IObjectTypeRecord objectTypeRecord2 in ObjectTypeRecords)
  615: 			{
  616: 				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass) && objectTypeRecord2.GetFirstMBObject() is T result)
  617: 				{
  618: 					return result;
  619: 				}
  620: 			}
  621: 		}
  622: 		return null;
  623: 	}
  624: 
  625: 	public bool ContainsObject<T>(string objectName) where T : MBObjectBase
  626: 	{
  627: 		Type typeFromHandle = typeof(T);
  628: 		if (typeFromHandle.IsSealed)
  629: 		{
  630: 			foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)

 succeeded in 519ms:
 1530: 	{
 1531: 		objectManager.RegisterType<FeatObject>("feat", "Feats", 0u);
 1532: 	}
 1533: 
 1534: 	protected override void OnRegisterTypes(MBObjectManager objectManager)
 1535: 	{
 1536: 		objectManager.RegisterType<MobileParty>("MobileParty", "MobileParties", 14u, autoCreateInstance: true, isTemporary: true);
 1537: 		objectManager.RegisterType<CharacterObject>("NPCCharacter", "NPCCharacters", 16u);
 1538: 		if (GameMode == CampaignGameMode.Tutorial)
 1539: 		{
 1540: 			objectManager.RegisterType<BasicCharacterObject>("NPCCharacter", "MPCharacters", 43u);

 succeeded in 1451ms:

  	}
  
> 	public void [7mRegisterType<[0mT>(string [0m[7m[0mclassPrefix, [0m[7m[0mstring [0m[7m[0mclassListPrefix, [0m[7m[0muint [0m[7m[0mtypeId, [0m[7m[0mbool [0m[7m[0mautoCreateInstance [0m[7m[0m= [0m
[7m[0mtrue, [0m[7m[0mbool [0m[7m[0misTemporary [0m[7m[0m= [0m[7m[0mfalse) [0m[7m[0mwhere [0m[7m[0mT [0m[7m[0m: [0m[7m[0mMBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		if (NumRegisteredTypes > MaxRegisteredTypes)[0m
[7m[0m  		{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mDebug.FailedAssert(new [0m[7m[0mMBTooManyRegisteredTypesException().ToString(), [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", [0m[7m[0m"RegisterType", [0m[7m[0m64);[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mObjectTypeRecords.Add(new [0m[7m[0mObjectTypeRecord<T>(typeId, [0m[7m[0mclassPrefix, [0m[7m[0mclassListPrefix, [0m[7m[0mautoCreateInstance, [0m
[7m[0misTemporary));[0m
[7m[0m  	}[0m
[7m[0m  [0m


 succeeded in 1647ms:

  	protected override void BeforeRegisterTypes(MBObjectManager objectManager)
  	{
> 		objectManager.[7mRegisterType<FeatObject>[0m("feat", "Feats", 0u);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	protected override void OnRegisterTypes(MBObjectManager objectManager)[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/CultureFeatAdapter.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0
Get-Content -Path '"'Main/Features/CulturalFeats/ICultureFeatAdapter.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/CulturalFeats/Models' -Filter 'Taom*Model.cs' | ForEach-Object { "'$file=$_.FullName; Select-String -Path $file -Pattern '"'CultureFeatAdapter.FromOrNull|IoC.Resolve|new |GetTroopRoster\\(|ToList\\(|Select\\(|Where\\(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Roster.TroopRoster 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'GetTroopRoster|GetCharacterAtIndex|GetElementNumber|Count|TroopRosterElement' -Context 2,8 | Select-Object -First 160" in C:\Users\mikew\source\repos\TAOM
 succeeded in 481ms:
    1: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    2: 
    3: namespace TAOM.Features.CulturalFeats;
    4: 
    5: /// <summary>
    6: /// Boundary adapter around <see cref="TaleWorlds.Core.BasicCultureObject"/>
    7: /// for feat-presence queries. Lets <see cref="ICulturalFeatsService"/>
    8: /// stay free of sealed TaleWorlds culture types so it can be unit-tested
    9: /// with NSubstitute (ADR-007). <c>null</c> adapter inputs to the service
   10: /// represent "no owning culture" (e.g. a wandering caravan) and must be
   11: /// treated as "has no feats".
   12: /// </summary>
   13: public interface ICultureFeatAdapter
   14: {
   15:     /// <summary>
   16:     /// True when the wrapped culture declares <paramref name="feat"/> as one of
   17:     /// its cultural feats. Returns false for a null-feat input (defensive — the
   18:     /// feat registry hands out nulls before <c>CreateAndRegister</c> runs).
   19:     /// </summary>
   20:     bool HasFeat(FeatObject feat);
   21: }

 succeeded in 1246ms:

  using System.Collections.Generic;
  using System.Diagnostics;
> using TaleWorlds.CampaignSystem.En[7mcount[0mers;[0m
[7m[0m  using TaleWorlds.CampaignSystem.Party;[0m
[7m[0m  using TaleWorlds.Core;[0m
[7m[0m  using TaleWorlds.Library;[0m
[7m[0m  using TaleWorlds.SaveSystem;[0m
[7m[0m  [0m
[7m[0m  namespace TaleWorlds.CampaignSystem.Roster;[0m
[7m[0m  [0m
[7m[0m  public class TroopRoster : ISerializableObject[0m
  
  	[SaveableField(0)]
> 	internal [7mTroopRosterElement[0m[] data;[0m
[7m[0m  [0m
[7m[0m  	[SaveableField(1)][0m
> 	internal int _[7mcount[0m;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	private int _totalRegulars;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	private int _totalWoundedRegulars;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
  
  	[CachedData]
> 	private MBList<[7mTroopRosterElement[0m> _troopRosterElements;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
> 	private int _[7mtroopRosterElement[0msVersion;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	private bool _isInitialized;[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(2)][0m
[7m[0m  	internal PartyBase OwnerParty { get; private set; }[0m
[7m[0m  [0m
> 	public int [7mCount[0m => _count;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	public int VersionNo { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public int TotalRegulars => _totalRegulars;[0m
[7m[0m  [0m
[7m[0m  	public int TotalWoundedRegulars => _totalWoundedRegulars;[0m
[7m[0m  [0m
  	public int TotalWounded => _totalWoundedRegulars + _totalWoundedHeroes;
  
> 	public int TotalMan[7mCount[0m => _totalRegulars + _totalHeroes;[0m
[7m[0m  [0m
> 	public int TotalHealthy[7mCount[0m => _totalRegulars + _totalHeroes - (_totalWoundedRegulars + _totalWoundedHeroes);[0m
[7m[0m  [0m
[7m[0m  	internal static void AutoGeneratedStaticCollectObjectsTroopRoster(object o, List<object> collectedObjects)[0m
[7m[0m  	{[0m
[7m[0m  		((TroopRoster)o).AutoGeneratedInstanceCollectObjects(collectedObjects);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)[0m
[7m[0m  	{[0m
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_[7mcount[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((TroopRoster)o)._[7mcount[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public TroopRoster(PartyBase ownerParty)[0m
[7m[0m  		: this()[0m
[7m[0m  	{[0m
[7m[0m  		OwnerParty = ownerParty;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	private TroopRoster()
  	{
> 		data = new [7mTroopRosterElement[0m[4];[0m
> 		_[7mcount[0m = 0;
> 		_[7mtroopRosterElement[0ms = new MBList<TroopRosterElement>();[0m
[7m[0m  		InitializeCachedData();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public static TroopRoster CreateDummyTroopRoster()[0m
[7m[0m  	{[0m
[7m[0m  		return new TroopRoster();[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	private void OnLoad(MetaData metaData)
  	{
> 		_[7mtroopRosterElement[0msVersion = -1;[0m
> 		_[7mtroopRosterElement[0ms = new MBList<TroopRosterElement>();[0m
[7m[0m  		InstanceListForLoadGame.Add(this);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void EnsureLength(int length)[0m
[7m[0m  	{[0m
[7m[0m  		if (length > 0 && (data == null || length > data.Length))[0m
[7m[0m  		{[0m
[7m[0m  			int num = 4;[0m
  				num = data.Length * 2;
  			}
> 			[7mTroopRosterElement[0m[] array = new TroopRosterElement[num];[0m
> 			for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  			{[0m
[7m[0m  				array[i] = data[i];[0m
[7m[0m  			}[0m
[7m[0m  			data = array;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public static void CalculateCachedStatsOnLoad()[0m
  		int num3 = 0;
  		int num4 = 0;
> 		for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
> 			[7mTroopRosterElement[0m troopRosterElement = data[i];
> 			if ([7mtroopRosterElement[0m.Character.IsHero)[0m
[7m[0m  			{[0m
[7m[0m  				num++;[0m
> 				if ([7mtroopRosterElement[0m.Character.HeroObject.IsWounded)[0m
[7m[0m  				{[0m
[7m[0m  					num2++;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  			else[0m
[7m[0m  			{[0m
[7m[0m  				num3 += data[i].Number;[0m
[7m[0m  				num4 += data[i].WoundedNumber;[0m
  	public FlattenedTroopRoster ToFlattenedRoster()
  	{
> 		return new FlattenedTroopRoster(TotalMan[7mCount[0m) { GetTroopRoster() };[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void Add(TroopRoster troopRoster)[0m
[7m[0m  	{[0m
> 		foreach ([7mTroopRosterElement[0m item in troopRoster.GetTroopRoster())[0m
[7m[0m  		{[0m
[7m[0m  			Add(item);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void Add([7mTroopRosterElement[0m troopRosterElement)[0m
[7m[0m  	{[0m
> 		AddTo[7mCount[0ms(troopRosterElement.Character, [0m[7m[0mtroopRosterElement.Number, [0m[7m[0minsertAtFront: [0m[7m[0mfalse, [0m
[7m[0mtroopRosterElement.WoundedNumber, [0m[7m[0mtroopRosterElement.Xp);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public ICollection<[7mTroopRosterElement[0m> RemoveIf(Predicate<TroopRosterElement> match)[0m
[7m[0m  	{[0m
> 		List<[7mTroopRosterElement[0m> list = new List<TroopRosterElement>();
> 		for (int num = _[7mcount[0m - 1; num >= 0; num--)[0m
[7m[0m  		{[0m
[7m[0m  			if (match(data[num]))[0m
[7m[0m  			{[0m
> 				[7mTroopRosterElement[0m elementCopyAtIndex = GetElementCopyAtIndex(num);[0m
[7m[0m  				list.Add(elementCopyAtIndex);[0m
> 				AddTo[7mCount[0msAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return list;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public int FindIndexOfTroop(CharacterObject character)[0m
[7m[0m  	{[0m
> 		for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			if (data[i].Character == character)[0m
[7m[0m  			{[0m
[7m[0m  				return i;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return -1;[0m
[7m[0m  	}[0m
  
> 	private int FlattenedRosterIndexToTroopRosterIndexWithFilter(int indexOfTroop, bool includeHeroes = false, bool 
[7mcount[0mOnlyHealthyOnes [0m[7m[0m= [0m[7m[0mtrue)[0m
[7m[0m  	{[0m
> 		for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			if (includeHeroes || !data[i].Character.IsHero)[0m
[7m[0m  			{[0m
> 				indexOfTroop -= ([7mcount[0mOnlyHealthyOnes ? (data[i].Number - data[i].WoundedNumber) : data[i].Number);[0m
[7m[0m  				if (indexOfTroop < 0)[0m
[7m[0m  				{[0m
[7m[0m  					return i;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return -1;[0m
[7m[0m  	}[0m
  			if (num >= 0)
  			{
> 				troopRoster.AddTo[7mCount[0ms(GetCharacterAtIndex(num), 1);[0m
> 				AddTo[7mCount[0msAtIndex(num, -1);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return troopRoster;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void WoundNumberOfNonHeroTroopsRandomly(int numberOfMen)[0m
[7m[0m  	{[0m
[7m[0m  		for (int i = 0; i < numberOfMen; i++)[0m
  			if (num >= 0)
  			{
> 				AddTo[7mCount[0msAtIndex(num, 0, 1);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SwapTroopsAtIndices(int firstIndex, int secondIndex)[0m
[7m[0m  	{[0m
> 		if (firstIndex < 0 || firstIndex >= [7mCount[0m || secondIndex < 0 || secondIndex >= Count)[0m
[7m[0m  		{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mTaleWorlds.Library.Debug.FailedAssert("Troop [0m[7m[0mroster [0m[7m[0mswap [0m[7m[0mindices [0m[7m[0mare [0m[7m[0mout [0m[7m[0mof [0m[7m[0mbounds.", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", [0m
[7m[0m"SwapTroopsAtIndices", [0m[7m[0m358);[0m
[7m[0m  		}[0m
[7m[0m  		else if (firstIndex != secondIndex)[0m
[7m[0m  		{[0m
> 			[7mTroopRosterElement[0m troopRosterElement = data[firstIndex];[0m
[7m[0m  			data[firstIndex] = data[secondIndex];[0m
> 			data[secondIndex] = [7mtroopRosterElement[0m;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void ShiftTroopToIndex(int troopIndex, int targetIndex)[0m
[7m[0m  	{[0m
> 		if (troopIndex < 0 || troopIndex >= [7mCount[0m || targetIndex < 0 || targetIndex >= Count)[0m
[7m[0m  		{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mTaleWorlds.Library.Debug.FailedAssert("Troop [0m[7m[0mroster [0m[7m[0mswap [0m[7m[0mindices [0m[7m[0mare [0m[7m[0mout [0m[7m[0mof [0m[7m[0mbounds.", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", [0m
[7m[0m"ShiftTroopToIndex", [0m[7m[0m376);[0m
[7m[0m  		}[0m
[7m[0m  		else if (troopIndex != targetIndex)[0m
[7m[0m  		{[0m
[7m[0m  			int num = ((troopIndex < targetIndex) ? 1 : (-1));[0m
> 			[7mTroopRosterElement[0m troopRosterElement = data[troopIndex];[0m
[7m[0m  			for (int i = troopIndex; i != targetIndex; i += num)[0m
[7m[0m  			{[0m
[7m[0m  				data[i] = data[i + num];[0m
[7m[0m  			}[0m
> 			data[targetIndex] = [7mtroopRosterElement[0m;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int AddTo[7mCount[0msAtIndex(int [0m[7m[0mindex, [0m[7m[0mint [0m[7m[0mcountChange, [0m[7m[0mint [0m[7m[0mwoundedCountChange [0m[7m[0m= [0m[7m[0m0, [0m[7m[0mint [0m[7m[0mxpChange [0m[7m[0m= [0m[7m[0m0, [0m[7m[0mbool [0m
[7m[0mremoveDepleted [0m[7m[0m= [0m[7m[0mtrue)[0m
[7m[0m  	{[0m
[7m[0m  		bool flag = false;[0m
[7m[0m  		CharacterObject character = data[index].Character;[0m
[7m[0m  		bool isHero = character.IsHero;[0m
> 		data[index].Number += [7mcount[0mChange;
> 		int num = data[index].WoundedNumber + wounded[7mCount[0mChange;[0m
[7m[0m  		if (num > data[index].Number)[0m
[7m[0m  		{[0m
> 			wounded[7mCount[0mChange += data[index].Number - num;[0m
[7m[0m  		}[0m
[7m[0m  		else if (num < 0)[0m
[7m[0m  		{[0m
> 			TaleWorlds.Library.Debug.FailedAssert("false", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", 
"AddTo[7mCount[0msAtIndex", [0m[7m[0m415);[0m
[7m[0m  		}[0m
> 		data[index].WoundedNumber += wounded[7mCount[0mChange;[0m
[7m[0m  		if (xpChange != 0)[0m
[7m[0m  		{[0m
[7m[0m  			SetElementXp(index, data[index].Xp + xpChange);[0m
[7m[0m  		}[0m
[7m[0m  		if (isHero)[0m
[7m[0m  		{[0m
> 			_totalHeroes += [7mcount[0mChange;[0m
[7m[0m  			if (character.HeroObject.IsWounded)[0m
[7m[0m  			{[0m
> 				_totalWoundedHeroes += [7mcount[0mChange;[0m
[7m[0m  			}[0m
> 			if ([7mcount[0mChange != 0)[0m
[7m[0m  			{[0m
[7m[0m  				flag = true;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
> 			_totalWoundedRegulars += wounded[7mCount[0mChange;
> 			_totalRegulars += [7mcount[0mChange;[0m
[7m[0m  		}[0m
[7m[0m  		if (removeDepleted && data[index].Number == 0)[0m
[7m[0m  		{[0m
[7m[0m  			RemoveRange(index, index + 1);[0m
[7m[0m  			index = -1;[0m
[7m[0m  		}[0m
[7m[0m  		if (OwnerParty != null && isHero)[0m
[7m[0m  		{[0m
> 			if ([7mcount[0mChange > 0)[0m
[7m[0m  			{[0m
[7m[0m  				OwnerParty.OnHeroAdded(character.HeroObject, this);[0m
[7m[0m  			}[0m
> 			else if ([7mcount[0mChange < 0)[0m
[7m[0m  			{[0m
[7m[0m  				OwnerParty.OnHeroRemoved(character.HeroObject, this);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
> 		if ([7mcount[0mChange != 0 || flag)[0m
[7m[0m  		{[0m
[7m[0m  			OwnerParty?.OnRosterSizeChanged(this);[0m
[7m[0m  		}[0m
> 		if ([7mcount[0mChange != 0 || woundedCountChange != 0)[0m
[7m[0m  		{[0m
[7m[0m  			UpdateVersion();[0m
[7m[0m  		}[0m
[7m[0m  		return index;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void RemoveRange(int p, int p2)[0m
[7m[0m  	{[0m
  		int num = p2 - p;
> 		for (int i = p2; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			data[i - num] = data[i];[0m
[7m[0m  		}[0m
> 		for (int j = _[7mcount[0m - num; j < _count; j++)[0m
[7m[0m  		{[0m
[7m[0m  			data[j].Clear();[0m
[7m[0m  		}[0m
> 		_[7mcount[0m -= num;[0m
[7m[0m  		if (num != 0)[0m
[7m[0m  		{[0m
[7m[0m  			UpdateVersion();[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private int AddNewElement(CharacterObject character, int insertionIndex)[0m
[7m[0m  	{[0m
> 		int num = _[7mcount[0m + 1;[0m
[7m[0m  		EnsureLength(num);[0m
> 		int num2 = ((insertionIndex == -1) ? _[7mcount[0m : insertionIndex);
> 		for (int num3 = _[7mcount[0m; num3 > num2; num3--)[0m
[7m[0m  		{[0m
[7m[0m  			data[num3] = data[num3 - 1];[0m
[7m[0m  		}[0m
> 		data[num2] = new [7mTroopRosterElement[0m(character);
> 		_[7mcount[0m = num;[0m
[7m[0m  		return num2;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	[Conditional("DEBUG_MORE")][0m
[7m[0m  	public void CheckValidity()[0m
[7m[0m  	{[0m
[7m[0m  		if (data == null)[0m
[7m[0m  		{[0m
  		for (int i = 0; i < data.Length; i++)
  		{
> 			[7mTroopRosterElement[0m troopRosterElement = data[i];[0m
> 			if ([7mtroopRosterElement[0m.Character != null)[0m
[7m[0m  			{[0m
> 				_ = [7mtroopRosterElement[0m.Number;
> 				_ = [7mtroopRosterElement[0m.WoundedNumber;[0m
[7m[0m  				num++;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int AddTo[7mCount[0ms(CharacterObject [0m[7m[0mcharacter, [0m[7m[0mint [0m[7m[0mcount, [0m[7m[0mbool [0m[7m[0minsertAtFront [0m[7m[0m= [0m[7m[0mfalse, [0m[7m[0mint [0m[7m[0mwoundedCount [0m[7m[0m= [0m[7m[0m0, [0m[7m[0mint [0m
[7m[0mxpChange [0m[7m[0m= [0m[7m[0m0, [0m[7m[0mbool [0m[7m[0mremoveDepleted [0m[7m[0m= [0m[7m[0mtrue, [0m[7m[0mint [0m[7m[0mindex [0m[7m[0m= [0m[7m[0m-1)[0m
[7m[0m  	{[0m
> 		if ([7mcount[0m != 0 || woundedCount != 0)[0m
[7m[0m  		{[0m
[7m[0m  			UpdateVersion();[0m
[7m[0m  		}[0m
[7m[0m  		if (insertAtFront)[0m
[7m[0m  		{[0m
[7m[0m  			index = 0;[0m
[7m[0m  		}[0m
[7m[0m  		int num = FindIndexOfTroop(character);[0m
  		if (num < 0)
  		{
> 			if ([7mcount[0m + woundedCount <= 0)[0m
[7m[0m  			{[0m
> 				TaleWorlds.Library.Debug.FailedAssert("false", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddTo[7mCount[0ms", [0m
[7m[0m572);[0m
[7m[0m  				return -1;[0m
[7m[0m  			}[0m
[7m[0m  			num = AddNewElement(character, index);[0m
[7m[0m  		}[0m
[7m[0m  		else if (index != -1 && index != num)[0m
[7m[0m  		{[0m
[7m[0m  			ShiftTroopToIndex(num, index);[0m
[7m[0m  			num = index;[0m
  		}
> 		AddTo[7mCount[0msAtIndex(num, count, woundedCount, xpChange, removeDepleted);[0m
[7m[0m  		return num;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int GetTroop[7mCount[0m(CharacterObject troop)[0m
[7m[0m  	{[0m
[7m[0m  		int num = FindIndexOfTroop(troop);[0m
[7m[0m  		if (num >= 0)[0m
[7m[0m  		{[0m
[7m[0m  			return data[num].Number;[0m
[7m[0m  		}[0m
[7m[0m  		return 0;[0m
[7m[0m  	}[0m
  
> 	public void RemoveZero[7mCount[0ms()[0m
[7m[0m  	{[0m
[7m[0m  		int num = 0;[0m
> 		for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			if (data[i].Number > 0)[0m
[7m[0m  			{[0m
[7m[0m  				if (num != i)[0m
[7m[0m  				{[0m
[7m[0m  					data[num] = data[i];[0m
[7m[0m  				}[0m
[7m[0m  				num++;[0m
  			}
  		}
> 		for (int j = num; j < _[7mcount[0m; j++)[0m
[7m[0m  		{[0m
[7m[0m  			data[j].Clear();[0m
[7m[0m  		}[0m
> 		_[7mcount[0m = num;[0m
[7m[0m  		UpdateVersion();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public [7mTroopRosterElement[0m GetElementCopyAtIndex(int index)[0m
[7m[0m  	{[0m
[7m[0m  		return data[index];[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetElementNumber(int index, int number)[0m
[7m[0m  	{[0m
> 		if (index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			data[index].Number = number;[0m
[7m[0m  			UpdateVersion();[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
[7m[0m  		throw new IndexOutOfRangeException();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int [7mGetElementNumber[0m(int index)[0m
[7m[0m  	{[0m
> 		if (index >= 0 && index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			return data[index].Number;[0m
[7m[0m  		}[0m
[7m[0m  		return 0;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int [7mGetElementNumber[0m(CharacterObject character)[0m
[7m[0m  	{[0m
> 		return [7mGetElementNumber[0m(FindIndexOfTroop(character));[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetElementWoundedNumber(int index, int number)[0m
[7m[0m  	{[0m
> 		if (index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			data[index].WoundedNumber = number;[0m
[7m[0m  			UpdateVersion();[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
[7m[0m  		throw new IndexOutOfRangeException();[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public int GetElementWoundedNumber(int index)
  	{
> 		if (index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			return data[index].WoundedNumber;[0m
[7m[0m  		}[0m
[7m[0m  		throw new IndexOutOfRangeException();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetElementXp(int index, int number)[0m
[7m[0m  	{[0m
> 		if (index >= 0 && index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			data[index].Xp = number;[0m
[7m[0m  			if (OwnerParty != null)[0m
[7m[0m  			{[0m
[7m[0m  				OwnerParty.OnXpChanged(this, ref data[index]);[0m
[7m[0m  			}[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
  	public int GetElementXp(int index)
  	{
> 		if (index < _[7mcount[0m && index >= 0)[0m
[7m[0m  		{[0m
[7m[0m  			return data[index].Xp;[0m
[7m[0m  		}[0m
[7m[0m  		return 0;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public int GetElementXp(CharacterObject character)[0m
[7m[0m  	{[0m
  	}
  
> 	public CharacterObject [7mGetCharacterAtIndex[0m(int index)[0m
[7m[0m  	{[0m
> 		if (index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			return data[index].Character;[0m
[7m[0m  		}[0m
[7m[0m  		throw new IndexOutOfRangeException();[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public override bool Equals(object obj)[0m
[7m[0m  	{[0m
  			return false;
  		}
> 		if (a.[7mCount[0m != b.Count)[0m
[7m[0m  		{[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  		if (a.OwnerParty != b.OwnerParty)[0m
[7m[0m  		{[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  		if (a.VersionNo != b.VersionNo)[0m
  			return false;
  		}
> 		for (int i = 0; i < a.[7mCount[0m; i++)[0m
[7m[0m  		{[0m
> 			[7mTroopRosterElement[0m elementCopyAtIndex = a.GetElementCopyAtIndex(i);[0m
[7m[0m  			int num = b.FindIndexOfTroop(elementCopyAtIndex.Character);[0m
[7m[0m  			if (num == -1)[0m
[7m[0m  			{[0m
[7m[0m  				return false;[0m
[7m[0m  			}[0m
> 			[7mTroopRosterElement[0m elementCopyAtIndex2 = b.GetElementCopyAtIndex(num);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(elementCopyAtIndex.Character [0m[7m[0m!= [0m[7m[0melementCopyAtIndex2.Character [0m[7m[0m|| [0m[7m[0melementCopyAtIndex.Number [0m[7m[0m!= [0m
[7m[0melementCopyAtIndex2.Number)[0m
[7m[0m  			{[0m
[7m[0m  				return false;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return true;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public bool Contains(CharacterObject character)
  	{
> 		for (int i = 0; i < [7mCount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			if (character == GetElementCopyAtIndex(i).Character)[0m
[7m[0m  			{[0m
[7m[0m  				return true;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return false;[0m
[7m[0m  	}[0m
  	public void ValidateTroopListCache()
  	{
> 		if (_[7mtroopRosterElement[0msVersion == VersionNo)[0m
[7m[0m  		{[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
[7m[0m  		lock (this)[0m
[7m[0m  		{[0m
> 			if (_[7mtroopRosterElement[0msVersion != VersionNo)[0m
[7m[0m  			{[0m
> 				_[7mtroopRosterElement[0ms.Clear();
> 				_[7mtroopRosterElement[0ms.Capacity = Math.Max(_troopRosterElements.Capacity, Count);
> 				for (int i = 0; i < [7mCount[0m; i++)[0m
[7m[0m  				{[0m
> 					_[7mtroopRosterElement[0ms.Add(GetElementCopyAtIndex(i));[0m
[7m[0m  				}[0m
> 				_[7mtroopRosterElement[0msVersion = VersionNo;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public MBList<[7mTroopRosterElement[0m> GetTroopRoster()[0m
[7m[0m  	{[0m
[7m[0m  		ValidateTroopListCache();[0m
> 		return _[7mtroopRosterElement[0ms;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void Clear()[0m
[7m[0m  	{[0m
> 		for (int num = _[7mcount[0m - 1; num >= 0; num--)[0m
[7m[0m  		{[0m
> 			AddTo[7mCount[0msAtIndex(num, -data[num].Number, -data[num].WoundedNumber);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mvoid [0m[7m[0mRemoveTroop(CharacterObject [0m[7m[0mtroop, [0m[7m[0mint [0m[7m[0mnumberToRemove [0m[7m[0m= [0m[7m[0m1, [0m[7m[0mUniqueTroopDescriptor [0m[7m[0mtroopSeed [0m[7m[0m= [0m
[7m[0mdefault(UniqueTroopDescriptor), [0m[7m[0mint [0m[7m[0mxp [0m[7m[0m= [0m[7m[0m0)[0m
[7m[0m  	{[0m
[7m[0m  		int index = FindIndexOfTroop(troop);[0m
[7m[0m  		bool removeDepleted = true;[0m
> 		if (PlayerEn[7mcount[0mer.CurrentBattleSimulation != null && !troop.IsHero)[0m
[7m[0m  		{[0m
[7m[0m  			removeDepleted = false;[0m
[7m[0m  		}[0m
> 		AddTo[7mCount[0msAtIndex(index, -numberToRemove, 0, (!troop.IsHero) ? (-xp) : 0, removeDepleted);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mvoid [0m[7m[0mWoundTroop(CharacterObject [0m[7m[0mtroop, [0m[7m[0mint [0m[7m[0mnumberToWound [0m[7m[0m= [0m[7m[0m1, [0m[7m[0mUniqueTroopDescriptor [0m[7m[0mtroopSeed [0m[7m[0m= [0m
[7m[0mdefault(UniqueTroopDescriptor))[0m
[7m[0m  	{[0m
[7m[0m  		int index = FindIndexOfTroop(troop);[0m
> 		AddTo[7mCount[0msAtIndex(index, 0, numberToWound);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	void ISerializableObject.SerializeTo(IWriter writer)[0m
[7m[0m  	{[0m
> 		writer.WriteInt([7mCount[0m);[0m
[7m[0m  		writer.WriteInt(VersionNo);[0m
[7m[0m  		if (data != null)[0m
[7m[0m  		{[0m
[7m[0m  			writer.WriteInt(data.Length);[0m
> 			[7mTroopRosterElement[0m[] array = data;
> 			foreach ([7mTroopRosterElement[0m troopRosterElement in array)[0m
[7m[0m  			{[0m
> 				writer.WriteSerializableObject([7mtroopRosterElement[0m);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
[7m[0m  			writer.WriteInt(0);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	void ISerializableObject.DeserializeFrom(IReader reader)
  	{
> 		_[7mcount[0m = reader.ReadInt();[0m
[7m[0m  		VersionNo = reader.ReadInt();[0m
[7m[0m  		int num = reader.ReadInt();[0m
> 		data = new [7mTroopRosterElement[0m[num];[0m
[7m[0m  		for (int i = 0; i < num; i++)[0m
[7m[0m  		{[0m
> 			data[i] = ([7mTroopRosterElement[0m)(object)reader.ReadSerializableObject();[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public int Sum(Func<[7mTroopRosterElement[0m, int> selector)[0m
[7m[0m  	{[0m
[7m[0m  		int num = 0;[0m
> 		for (int i = 0; i < _[7mcount[0m; i++)[0m
[7m[0m  		{[0m
[7m[0m  			num += selector(data[i]);[0m
[7m[0m  		}[0m
[7m[0m  		return num;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void OnHeroHealthStatusChanged(Hero hero)[0m
[7m[0m  	{[0m
  			for (int i = 0; i < data.Length; i++)
  			{
> 				[7mTroopRosterElement[0m troopRosterElement = data[i];[0m
> 				if ([7mtroopRosterElement[0m.Character != null)[0m
[7m[0m  				{[0m
> 					troopRoster.AddTo[7mCount[0ms(data[i].Character, [0m[7m[0mtroopRosterElement.Number, [0m[7m[0minsertAtFront: [0m[7m[0mfalse, [0m
[7m[0mtroopRosterElement.WoundedNumber);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return troopRoster;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void AddXpToTroop(CharacterObject troop, int xpAmount)[0m
[7m[0m  	{[0m
  	public void AddXpToTroopAtIndex(int index, int xpAmount)
  	{
> 		if (index >= 0 && index < _[7mcount[0m)[0m
[7m[0m  		{[0m
[7m[0m  			xpAmount += GetElementXp(data[index].Character);[0m
[7m[0m  			SetElementXp(index, xpAmount);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  }[0m


 succeeded in 1511ms:
    1: using TaleWorlds.CampaignSystem;
    2: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    3: 
    4: namespace TAOM.Features.CulturalFeats;
    5: 
    6: /// <summary>
    7: /// Production wrapper around a sealed <see cref="CultureObject"/>. Constructed
    8: /// at the boundary in each <c>Taom*Model</c> override; the underlying culture
    9: /// reference is never exposed to the service. <c>HasFeat</c> lives on
   10: /// <see cref="CultureObject"/> (Campaign system), not its
   11: /// <c>BasicCultureObject</c> base in TaleWorlds.Core — keeping the type as
   12: /// <c>CultureObject</c> matches how every model already pulls culture
   13: /// (via <c>party.Owner.Culture</c>, <c>town.OwnerClan.Culture</c>, etc.).
   14: /// </summary>
   15: public sealed class CultureFeatAdapter : ICultureFeatAdapter
   16: {
   17:     private readonly CultureObject _culture;
   18: 
   19:     public CultureFeatAdapter(CultureObject culture)
   20:     {
   21:         _culture = culture;
   22:     }
   23: 
   24:     public bool HasFeat(FeatObject feat)
   25:     {
   26:         if (_culture == null || feat == null)
   27:             return false;
   28:         return _culture.HasFeat(feat);
   29:     }
   30: 
   31:     /// <summary>
   32:     /// Convenience boundary helper: returns null when <paramref name="culture"/>
   33:     /// is null so the model overrides can keep their "no culture → skip"
   34:     /// short-circuit at a single point.
   35:     /// </summary>
   36:     public static ICultureFeatAdapter? FromOrNull(CultureObject? culture)
   37:         => culture == null ? null : new CultureFeatAdapter(culture);
   38: }

 succeeded in 1734ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:23: CultureFeatAdapter.FromOrNull(armyMemberParty.Party?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:28: CultureFeatAdapter.FromOrNull(armyLeaderParty.Party?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:34: _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty.Owner?.Culture ?? winnerParty.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBuildingConstructionModel.cs:21: _feats.ApplyConstructionSpeedFeats(CultureFeatAdapter.FromOrNull(town.OwnerClan?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:17: CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomClanFinanceModel.cs:21: _feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:21: _feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party?.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:25: _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party?.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25: _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:30: var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:72: foreach (var element in roster.GetTroopRoster())
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:26: CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomRaidModel.cs:26: CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs:37: _feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementMilitiaModel.cs:19: _feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementProsperityModel.cs:20: _feats.ApplyHearthGrowthFeats(CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:36: var result = new ExplainedNumber(baseCost, false);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:37: _feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomVillageProductionModel.cs:21: CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture),

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t Helpers.PartyBaseHelper 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'HasFeat|Culture|Owner' -Context 3,8 | Select-Object -First 160" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\PartyBaseHelper.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'HasFeat|Culture|Owner' -or ("'$i -ge 1 -and $i -le 60)) { '"'{0,5}: {1}' -f "'$i, $_ } } | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs Main/Features/CulturalFeats/CulturalFeatsService.cs | Select-Object -First 260' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Party.PartyBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Owner|Culture|MapFaction' -Context 2,6 | Select-Object -First 160" in C:\Users\mikew\source\repos\TAOM
 succeeded in 547ms:
diff --git a/Main/Features/CulturalFeats/CulturalFeatsService.cs b/Main/Features/CulturalFeats/CulturalFeatsService.cs
index 29cff0c..eeb8cb7 100644
--- a/Main/Features/CulturalFeats/CulturalFeatsService.cs
+++ b/Main/Features/CulturalFeats/CulturalFeatsService.cs
@@ -71,22 +71,55 @@ public sealed class CulturalFeatsService : ICulturalFeatsService
 
     // ── PartySpeed ──────────────────────────────────────────────────────
 
-    public void ApplyForestSpeedFeats(
-        ICultureFeatAdapter? culture, bool isForest, float forestPenaltyMagnitude, ref ExplainedNumber result)
+    public void ApplyTerrainSpeedFeats(
+        ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result)
     {
-        if (culture == null || !isForest)
+        if (culture == null)
             return;
 
-        if (culture.HasFeat(TaomCulturalFeats.MirkwoodForestSpeedFeat))
-        {
-            float bonus = TaomCulturalFeats.MirkwoodForestSpeedFeat.EffectBonus * forestPenaltyMagnitude;
-            result.AddFactor(bonus, CultureText);
-        }
-        if (culture.HasFeat(TaomCulturalFeats.LothlorienForestSpeedFeat))
+        switch (terrain)
         {
-            float bonus = TaomCulturalFeats.LothlorienForestSpeedFeat.EffectBonus * forestPenaltyMagnitude;
-            result.AddFactor(bonus, CultureText);
+            case TerrainKind.Forest:
+                ApplyIfHas(culture, TaomCulturalFeats.MirkwoodForestSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.LothlorienForestSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.RivendellForestSpeedFeat, ref result);
+                break;
+            case TerrainKind.Snow:
+                ApplyIfHas(culture, TaomCulturalFeats.EreborSnowSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.GundabadSnowSpeedFeat, ref result);
+                break;
+            case TerrainKind.Steppe:
+                ApplyIfHas(culture, TaomCulturalFeats.KhandSteppeSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.RhunSteppeSpeedFeat, ref result);
+                break;
+            case TerrainKind.Desert:
+                ApplyIfHas(culture, TaomCulturalFeats.UmbarDesertSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.HaradDesertSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.ShaghanaDesertSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.AbanissaDesertSpeedFeat, ref result);
+                break;
+            case TerrainKind.Plain:
+                ApplyIfHas(culture, TaomCulturalFeats.MordorPlainSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.GondorPlainSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.RohanPlainSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.DalePlainSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.DunlandPlainSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.IsengardPlainSpeedFeat, ref result);
+                break;
+            case TerrainKind.Swamp:
+                ApplyIfHas(culture, TaomCulturalFeats.MordorSwampSpeedFeat, ref result);
+                ApplyIfHas(culture, TaomCulturalFeats.IsengardSwampSpeedFeat, ref result);
+                break;
         }
+
+        if (isNight)
+            ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
+    }
+
+    private static void ApplyIfHas(ICultureFeatAdapter culture, FeatObject feat, ref ExplainedNumber result)
+    {
+        if (culture.HasFeat(feat))
+            result.AddFactor(feat.EffectBonus, CultureText);
     }
 
     public void ApplyRohanInfantryPenalty(
diff --git a/Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs b/Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs
index 95f8ab0..2c944ef 100644
--- a/Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs
+++ b/Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs
@@ -10,12 +10,6 @@ namespace TAOM.Features.CulturalFeats.Models;
 
 public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
 {
-    /// <summary>
-    /// Vanilla forest movement penalty magnitude
-    /// (<see cref="DefaultPartySpeedCalculatingModel.MovingAtForestEffect"/>).
-    /// </summary>
-    private const float ForestPenaltyMagnitude = 0.3f;
-
     private readonly ICulturalFeatsService _feats;
     private readonly ICareerPassiveService _careerPassives;
 
@@ -31,13 +25,15 @@ public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
 
         // Boundary: convert sealed TaleWorlds types to primitives + adapter, then delegate.
         // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
-        // scene transitions; `?.` short-circuit returns Plain so the forest branch is skipped.
+        // scene transitions; `?.` short-circuit yields a null TerrainType which MapTerrain maps
+        // to TerrainKind.None so no terrain feat is applied.
         var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
-        var terrain = Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace)
-                      ?? TerrainType.Plain;
+        var terrain = MapTerrain(
+            Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
+        var isNight = Campaign.Current?.IsNight ?? false;
         var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);
 
-        _feats.ApplyForestSpeedFeats(culture, terrain == TerrainType.Forest, ForestPenaltyMagnitude, ref result);
+        _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
         _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
         _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
 
@@ -49,6 +45,26 @@ public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
     /// two primitives <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/>
     /// needs, keeping the service free of TaleWorlds types per ADR-007.
     /// </summary>
+    /// <summary>
+    /// Boundary helper — maps the sealed TaleWorlds <see cref="TerrainType"/> (nullable
+    /// when the map scene is unavailable) to the TAOM-owned <see cref="TerrainKind"/> so
+    /// the service stays free of engine types (ADR-007). <see cref="TerrainType.Dune"/>
+    /// folds into <see cref="TerrainKind.Desert"/> to match vanilla's desert handling.
+    /// Any unmapped terrain (water, mountain, etc.) and a null input map to
+    /// <see cref="TerrainKind.None"/>.
+    /// </summary>
+    private static TerrainKind MapTerrain(TerrainType? terrain) => terrain switch
+    {
+        TerrainType.Plain => TerrainKind.Plain,
+        TerrainType.Forest => TerrainKind.Forest,
+        TerrainType.Swamp => TerrainKind.Swamp,
+        TerrainType.Steppe => TerrainKind.Steppe,
+        TerrainType.Desert => TerrainKind.Desert,
+        TerrainType.Dune => TerrainKind.Desert,
+        TerrainType.Snow => TerrainKind.Snow,
+        _ => TerrainKind.None,
+    };
+
     private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster)
     {
         int total = roster.TotalManCount;

 succeeded in 563ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.Linq;
    4: using TaleWorlds.CampaignSystem;
    5: using TaleWorlds.CampaignSystem.CharacterDevelopment;
    6: using TaleWorlds.CampaignSystem.Party;
    7: using TaleWorlds.CampaignSystem.Roster;
    8: using TaleWorlds.Core;
    9: using TaleWorlds.Library;
   10: using TaleWorlds.Localization;
   11: 
   12: namespace Helpers;
   13: 
   14: public static class PartyBaseHelper
   15: {
   16: 	public static void SortRoster(MobileParty mobileParty)
   17: 	{
   18: 		CharacterObject characterObject = null;
   19: 		foreach (TroopRosterElement item in mobileParty.MemberRoster.GetTroopRoster())
   20: 		{
   21: 			if (characterObject == null || characterObject.Tier < item.Character.Tier)
   22: 			{
   23: 				characterObject = item.Character;
   24: 				if (characterObject.Tier == Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier)
   25: 				{
   26: 					break;
   27: 				}
   28: 			}
   29: 		}
   30: 		if (characterObject != null)
   31: 		{
   32: 			mobileParty.MemberRoster.SwapTroopsAtIndices(mobileParty.MemberRoster.FindIndexOfTroop(characterObject), 0);
   33: 		}
   34: 	}
   35: 
   36: 	public static TextObject GetPartySizeText(PartyBase party)
   37: 	{
   38: 		if (party.NumberOfHealthyMembers == party.NumberOfAllMembers)
   39: 		{
   40: 			return new TextObject(party.NumberOfHealthyMembers.ToString());
   41: 		}
   42: 		MBTextManager.SetTextVariable("HEALTHY_NUM", party.NumberOfHealthyMembers);
   43: 		MBTextManager.SetTextVariable("WOUNDED_NUM", party.NumberOfAllMembers - party.NumberOfHealthyMembers);
   44: 		return GameTexts.FindText("str_party_health");
   45: 	}
   46: 
   47: 	public static TextObject GetPartySizeText(int healtyNumber, int woundedNumber, bool isInspected)
   48: 	{
   49: 		string seed = "";
   50: 		if (isInspected)
   51: 		{
   52: 			if (woundedNumber == 0)
   53: 			{
   54: 				return new TextObject(healtyNumber);
   55: 			}
   56: 			TextObject textObject = GameTexts.FindText("str_party_health");
   57: 			textObject.SetTextVariable("HEALTHY_NUM", healtyNumber);
   58: 			textObject.SetTextVariable("WOUNDED_NUM", woundedNumber);
   59: 			return textObject;
   60: 		}
   76: 		int num = Math.Min((mobileParty.LeaderHero != null && mobileParty.Party.Owner?.Clan != null && mobileParty.LeaderHero != mobileParty.Party.Owner.Clan.Leader) ? mobileParty.LeaderHero.CharacterObject.TroopWage : 0, mobileParty.TotalWage);
  373: 	public static bool HasFeat(PartyBase party, FeatObject feat)
  381: 			return party.LeaderHero.Culture.HasFeat(feat);
  383: 		if (party.Culture != null)
  385: 			return party.Culture.HasFeat(feat);
  387: 		if (party.Owner != null)
  389: 			return party.Owner.Culture.HasFeat(feat);
  393: 			return party.Settlement.Culture.HasFeat(feat);

 succeeded in 1214ms:

  
  	public static float FindPartySizeNormalLimit(MobileParty mobileParty)
  	{
> 		int num = Math.Min((mobileParty.LeaderHero != null && mobileParty.Party.[7mOwner[0m?.Clan [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m
[7m[0mmobileParty.LeaderHero [0m[7m[0m!= [0m[7m[0mmobileParty.Party.Owner.Clan.Leader) [0m[7m[0m? [0m[7m[0mmobileParty.LeaderHero.CharacterObject.TroopWage [0m[7m[0m: [0m[7m[0m0, [0m
[7m[0mmobileParty.TotalWage);[0m
[7m[0m  		int a = (int)((float)(mobileParty.PaymentLimit - num) / Campaign.Current.AverageWage) + 1;[0m
[7m[0m  		int num2 = TaleWorlds.Library.MathF.Max(1, TaleWorlds.Library.MathF.Min(a, mobileParty.Party.PartySizeLimit));[0m
[7m[0m  		return TaleWorlds.Library.MathF.Max(0.1f, (float)num2 / (float)mobileParty.Party.PartySizeLimit);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public static Hero GetCaptainOfTroop(PartyBase affectorParty, CharacterObject affectorCharacter)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (TroopRosterElement item in affectorParty.MemberRoster.GetTroopRoster())[0m
  		return num;
  	}
  
> 	public static bool [7mHasFeat[0m(PartyBase party, FeatObject feat)[0m
[7m[0m  	{[0m
[7m[0m  		if (party == null)[0m
[7m[0m  		{[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  		if (party.LeaderHero != null)[0m
[7m[0m  		{[0m
> 			return party.LeaderHero.[7mCulture[0m.HasFeat(feat);[0m
[7m[0m  		}[0m
> 		if (party.[7mCulture[0m != null)[0m
[7m[0m  		{[0m
> 			return party.[7mCulture[0m.HasFeat(feat);[0m
[7m[0m  		}[0m
> 		if (party.[7mOwner[0m != null)[0m
[7m[0m  		{[0m
> 			return party.[7mOwner[0m.Culture.HasFeat(feat);[0m
[7m[0m  		}[0m
[7m[0m  		if (party.Settlement != null)[0m
[7m[0m  		{[0m
> 			return party.Settlement.[7mCulture[0m.HasFeat(feat);[0m
[7m[0m  		}[0m
[7m[0m  		return false;[0m
[7m[0m  	}[0m
[7m[0m  }[0m


 succeeded in 1398ms:

  namespace TaleWorlds.CampaignSystem.Party;
  
> public sealed class PartyBase : IBattleCombatant, IRandom[7mOwner[0m, IInteractablePoint[0m
[7m[0m  {[0m
[7m[0m  	[SaveableField(15)][0m
[7m[0m  	private int _remainingFoodPercentage;[0m
[7m[0m  [0m
[7m[0m  	[SaveableField(182)][0m
[7m[0m  	private CampaignTime _lastEatingTime = CampaignTime.Now;[0m
  
  	[SaveableField(8)]
> 	private Hero _custom[7mOwner[0m;[0m
[7m[0m  [0m
[7m[0m  	[SaveableField(9)][0m
[7m[0m  	private int _index;[0m
[7m[0m  [0m
[7m[0m  	[SaveableField(200)][0m
[7m[0m  	private MapEventSide _mapEventSide;[0m
  	public ExplainedNumber HealingRateForMemberHeroesExplained => 
Campaign.Current.Models.PartyHealingModel.GetDailyHealingHpForHeroes(this, isPrisoners: false, includeDescriptions: 
true);
  
> 	public Hero [7mOwner[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
> 			Hero hero = _custom[7mOwner[0m;[0m
[7m[0m  			if (hero == null)[0m
[7m[0m  			{[0m
[7m[0m  				if (!IsMobile)[0m
[7m[0m  				{[0m
> 					return Settlement.[7mOwner[0m;[0m
[7m[0m  				}[0m
> 				hero = MobileParty.[7mOwner[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return hero;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero LeaderHero => MobileParty?.LeaderHero;[0m
  	public bool IsValid => Index >= 0;
  
> 	public IFaction [7mMapFaction[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (MobileParty != null)[0m
[7m[0m  			{[0m
> 				return MobileParty.[7mMapFaction[0m;[0m
[7m[0m  			}[0m
[7m[0m  			if (Settlement != null)[0m
[7m[0m  			{[0m
> 				return Settlement.[7mMapFaction[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(210)][0m
  	public int RandomValue { get; private set; } = MBRandom.RandomInt(1, int.MaxValue);
  
> 	public [7mCulture[0mObject Culture => MapFaction.Culture;[0m
[7m[0m  [0m
[7m[0m  	public Tuple<uint, uint> PrimaryColorPair[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
> 			if ([7mMapFaction[0m == null)[0m
[7m[0m  			{[0m
[7m[0m  				return new Tuple<uint, uint>(4291609515u, 4291609515u);[0m
[7m[0m  			}[0m
> 			return new Tuple<uint, uint>([7mMapFaction[0m.Color, MapFaction.Color2);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(216)][0m
[7m[0m  	public TextObject CustomName { get; private set; }[0m
[7m[0m  [0m
  	public Ship FlagShip => Ships.MaxBy((Ship x) => x.FlagshipScore);
  
> 	public Basic[7mCulture[0mObject BasicCulture => Culture;[0m
[7m[0m  [0m
[7m[0m  	public BasicCharacterObject General[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (MobileParty?.Army != null)[0m
  	{
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_lastEatingTime, collectedObjects);
> 		collectedObjects.Add(_custom[7mOwner[0m);[0m
[7m[0m  		collectedObjects.Add(_mapEventSide);[0m
[7m[0m  		collectedObjects.Add(_ships);[0m
[7m[0m  		collectedObjects.Add(Settlement);[0m
[7m[0m  		collectedObjects.Add(MobileParty);[0m
[7m[0m  		collectedObjects.Add(MemberRoster);[0m
[7m[0m  		collectedObjects.Add(PrisonRoster);[0m
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_custom[7mOwner[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((PartyBase)o)._custom[7mOwner[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	internal static object AutoGeneratedGetMemberValue_index(object o)[0m
[7m[0m  	{[0m
[7m[0m  		return ((PartyBase)o)._index;[0m
[7m[0m  	}[0m
  	}
  
> 	public void SetCustom[7mOwner[0m(Hero customOwner)[0m
[7m[0m  	{[0m
> 		_custom[7mOwner[0m = customOwner;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public static bool IsPartyUnderPlayerCommand(PartyBase party)[0m
[7m[0m  	{[0m
[7m[0m  		return Campaign.Current.Models.EncounterModel.IsPartyUnderPlayerCommand(party);[0m
[7m[0m  	}[0m
  				}
  			}
> 			if (IsMobile && MobileParty.IsCaravan && !MobileParty.IsCurrentlyUsedByAQuest && _custom[7mOwner [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m
[7m[0mMobileParty.Owner [0m[7m[0m!= [0m[7m[0mOwner)[0m
[7m[0m  			{[0m
> 				SetCustom[7mOwner[0m(null);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.0"))[0m
[7m[0m  		{[0m
[7m[0m  			MemberRoster.RemoveZeroCounts();[0m
[7m[0m  		}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs'
"'$i=0
Get-Content -Path $path | ForEach-Object { $i++; if ($_ -match '"'public Hero Owner|public Hero LeaderHero|public CultureObject Culture|public IFaction MapFaction|PartyComponent' -or ("'$i -ge 300 -and $i -le 360)) { '"'{0,5}: {1}' -f "'$i, $_ } } | Select-Object -First 200' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -Include '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'new MobileParty|CreateParty|MobileParty.CreateParty|SetCustomOwner|Owner =|LeaderHero' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() } | Select-Object -First 160' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Party.MobileParty 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public Hero Owner|LeaderHero|Culture|MapFaction|PartyComponent' -Context 2,8 | Select-Object -First 180" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -Include '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'PartyBaseHelper.HasFeat|\\.HasFeat\\(TaomCulturalFeats|CultureFeatAdapter.FromOrNull\\(.*Owner\\?\\.Culture' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 566ms:
   13: using TaleWorlds.CampaignSystem.Party.PartyComponents;
  253: 	private PartyComponent _partyComponent;
  275: 	public static MBReadOnlyList<MobileParty> AllPartiesWithoutPartyComponent => Campaign.Current.PartiesWithoutPartyComponent;
  289: 			if (_partyComponent != null)
  291: 				return _partyComponent.Name;
  300: 
  301: 	[SaveableProperty(1004)]
  302: 	public Vec2 Bearing { get; internal set; }
  303: 
  304: 	public MBReadOnlyList<MobileParty> AttachedParties => _attachedParties;
  305: 
  306: 	[SaveableProperty(1099)]
  307: 	public bool HasLandNavigationCapability { get; private set; } = true;
  308: 
  309: 	public MBReadOnlyList<Ship> Ships => Party.Ships;
  310: 
  311: 	public bool HasNavalNavigationCapability => Campaign.Current.Models.PartyNavigationModel.HasNavalNavigationCapability(this);
  312: 
  313: 	[SaveableProperty(1009)]
  314: 	public float Aggressiveness { get; set; }
  315: 
  316: 	public int PaymentLimit => _partyComponent?.WagePaymentLimit ?? Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;
  317: 
  318: 	public Banner Banner
  319: 	{
  320: 		get
  321: 		{
  322: 			if (Party.CustomBanner != null)
  323: 			{
  324: 				return Party.CustomBanner;
  325: 			}
  326: 			if (PartyComponent != null && PartyComponent.GetDefaultComponentBanner() != null)
  327: 			{
  328: 				return PartyComponent.GetDefaultComponentBanner();
  329: 			}
  330: 			if (MapFaction != null)
  331: 			{
  332: 				return MapFaction.Banner;
  333: 			}
  334: 			return null;
  335: 		}
  336: 	}
  337: 
  338: 	[SaveableProperty(1005)]
  339: 	public Vec2 ArmyPositionAdder { get; private set; }
  340: 
  341: 	public CampaignVec2 AiBehaviorTarget => Ai.BehaviorTarget;
  342: 
  343: 	[SaveableProperty(1090)]
  344: 	public PartyObjective Objective { get; private set; }
  345: 
  346: 	[CachedData]
  347: 	MobileParty ILocatable<MobileParty>.NextLocatable { get; set; }
  348: 
  349: 	[SaveableProperty(1019)]
  350: 	public MobilePartyAi Ai { get; private set; }
  351: 
  352: 	[SaveableProperty(1020)]
  353: 	public PartyBase Party { get; private set; }
  354: 
  355: 	[SaveableProperty(1023)]
  356: 	public bool IsActive { get; set; }
  357: 
  358: 	public bool IsInRaftState
  359: 	{
  360: 		get
  633: 				PartyComponent partyComponent = _partyComponent;
  634: 				if (partyComponent == null)
  638: 				settlement = partyComponent.HomeSettlement;
  775: 	public Hero LeaderHero => PartyComponent?.Leader;
  795: 	public Hero Owner => _partyComponent?.PartyOwner;
  952: 				if (_actualClan != null && value != null && PartyComponent is WarPartyComponent warPartyComponent)
  954: 					warPartyComponent.OnClanChange(_actualClan, value);
 1079: 	public IFaction MapFaction
 1217: 	public VillagerPartyComponent VillagerPartyComponent => _partyComponent as VillagerPartyComponent;
 1219: 	public CaravanPartyComponent CaravanPartyComponent => _partyComponent as CaravanPartyComponent;
 1221: 	public WarPartyComponent WarPartyComponent => _partyComponent as WarPartyComponent;
 1223: 	public BanditPartyComponent BanditPartyComponent => _partyComponent as BanditPartyComponent;
 1225: 	public PatrolPartyComponent PatrolPartyComponent => _partyComponent as PatrolPartyComponent;
 1227: 	public LordPartyComponent LordPartyComponent => _partyComponent as LordPartyComponent;
 1229: 	public GarrisonPartyComponent GarrisonPartyComponent => _partyComponent as GarrisonPartyComponent;
 1231: 	public PartyComponent PartyComponent => _partyComponent;
 1263: 				return BanditPartyComponent.IsBossParty;
 1273: 			if (_partyComponent != null)
 1275: 				return _partyComponent.AvoidHostileActions;
 1306: 		collectedObjects.Add(_partyComponent);
 1612: 	internal static object AutoGeneratedGetMemberValue_partyComponent(object o)
 1614: 		return ((MobileParty)o)._partyComponent;
 1648: 		PartyComponent?.SetWagePaymentLimit(newLimit);
 2004: 			UpdatePartyComponentFlags();
 2053: 		PartyComponent.ChangePartyLeader(newLeader);
 2092: 		PartyComponent.ChangePartyLeader(null);
 2243: 		UpdatePartyComponentFlags();
 2244: 		PartyComponent?.Initialize(this);
 3022: 		PartyComponent?.Finish();
 4124: 	public static MobileParty CreateParty(string stringId, PartyComponent component)
 4129: 		mobileParty._partyComponent = component;
 4130: 		mobileParty.UpdatePartyComponentFlags();
 4131: 		mobileParty._partyComponent?.Create(mobileParty);
 4132: 		mobileParty._partyComponent?.Initialize(mobileParty);
 4139: 	public void SetPartyComponent(PartyComponent partyComponent, bool firstTimePartyComponentCreation = true)
 4141: 		if (_partyComponent == partyComponent)
 4145: 		if (_partyComponent != null)
 4147: 			_partyComponent.Finish();
 4149: 		Campaign.Current.CampaignObjectManager.BeforePartyComponentChanged(this);
 4150: 		_partyComponent = partyComponent;
 4151: 		UpdatePartyComponentFlags();
 4152: 		Campaign.Current.CampaignObjectManager.AfterPartyComponentChanged(this);
 4153: 		if (_partyComponent != null)
 4155: 			if (firstTimePartyComponentCreation)
 4157: 				_partyComponent.Create(this);
 4159: 			_partyComponent.Initialize(this);
 4164: 	public void UpdatePartyComponentFlags()
 4166: 		IsLordParty = _partyComponent is LordPartyComponent;
 4167: 		IsVillager = _partyComponent is VillagerPartyComponent;
 4168: 		IsMilitia = _partyComponent is MilitiaPartyComponent;
 4169: 		IsCaravan = _partyComponent is CaravanPartyComponent;
 4170: 		IsPatrolParty = _partyComponent is PatrolPartyComponent;
 4171: 		IsGarrison = _partyComponent is GarrisonPartyComponent;
 4172: 		IsCustomParty = _partyComponent is CustomPartyComponent;
 4173: 		IsBandit = _partyComponent is BanditPartyComponent;

 succeeded in 785ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:23: CultureFeatAdapter.FromOrNull(armyMemberParty.Party?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:28: CultureFeatAdapter.FromOrNull(armyLeaderParty.Party?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:34: _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty.Owner?.Culture ?? winnerParty.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:21: _feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party?.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:25: _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party?.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25: _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:30: var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:26: CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomRaidModel.cs:26: CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty?.Owner?.Culture),
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs:37: _feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:47: if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:49: if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:60: if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:62: if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:64: if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:66: if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:130: if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:145: if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:147: if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:149: if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:159: if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:161: if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:171: if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:173: if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:175: if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:177: if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:187: if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:189: if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:191: if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:201: if (culture.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:212: if (culture.HasFeat(TaomCulturalFeats.UmbarRenownFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:222: if (culture.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:224: if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:234: if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:236: if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:238: if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:240: if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:242: if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:252: if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:254: if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:256: if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:258: if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:268: if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:270: if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:272: if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:274: if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:276: if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:286: if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:288: if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:290: if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:292: if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:294: if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:304: if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:306: if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:316: if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:326: if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:328: if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsService.cs:330: if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\Models\TaomDiplomacyModel.cs:34: && supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)

 succeeded in 833ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\MapConversationTableau_SpawnOpponentBodyguard_Patch.cs:11: /// Vanilla uses party.LeaderHero.MapFaction colors. This postfix applies clan-specific colors.
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:28: TaleWorlds.CampaignSystem.Hero? leaderHero = null;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:31: leaderHero = partyOrigin.Party?.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:33: leaderHero = partyGroupOrigin.Party?.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:35: leaderHero = simpleOrigin.Party?.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:37: if (leaderHero == null) return null;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:39: var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:38: var leaderHero = ownerParty?.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:39: if (leaderHero == null) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:41: var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:50: var hero = party.Owner ?? party.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomInventoryCapacityModel.cs:26: _careerPassives.ApplyFactor(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.InventoryCapacity);
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomMapVisibilityModel.cs:20: _careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySpottingRange);
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Collectors\CampaignStateCollector.cs:47: var hero = SafeRead(() => campaign.MainParty?.LeaderHero) ?? SafeRead(() => Hero.MainHero);
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Collectors\HarmonyCorrelationCollector.cs:82: var owner = p.owner ?? "(unknown)";
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:35: _careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:26: _careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:26: _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.PartySize);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:38: _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:29: _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.TroopUpgradeCost);
C:\Users\mikew\source\repos\TAOM\Main\Features\MissionDiagnostic\MissionDiagnosticService.cs:61: var hero = Campaign.Current.MainParty?.LeaderHero;
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:217: var isPlayerVictor = (mapEvent.AttackerSide.LeaderParty?.LeaderHero == hero && mapEvent.BattleState == BattleState.AttackerVictory)
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesBehavior.cs:218: || (mapEvent.DefenderSide.LeaderParty?.LeaderHero == hero && mapEvent.BattleState == BattleState.DefenderVictory);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:57: _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.TroopWages);

 succeeded in 2321ms:

  using TaleWorlds.CampaignSystem.MapEvents;
  using TaleWorlds.CampaignSystem.Naval;
> using TaleWorlds.CampaignSystem.Party.[7mPartyComponent[0ms;[0m
[7m[0m  using TaleWorlds.CampaignSystem.Roster;[0m
[7m[0m  using TaleWorlds.CampaignSystem.Settlements;[0m
[7m[0m  using TaleWorlds.CampaignSystem.Siege;[0m
[7m[0m  using TaleWorlds.Core;[0m
[7m[0m  using TaleWorlds.Library;[0m
[7m[0m  using TaleWorlds.Localization;[0m
[7m[0m  using TaleWorlds.SaveSystem;[0m
[7m[0m  using TaleWorlds.SaveSystem.Load;[0m
  
  	[SaveableField(210)]
> 	private [7mPartyComponent[0m _partyComponent;[0m
[7m[0m  [0m
[7m[0m  	public static MobileParty MainParty => Campaign.Current.MainParty;[0m
[7m[0m  [0m
[7m[0m  	public static MBReadOnlyList<MobileParty> All => Campaign.Current.MobileParties;[0m
[7m[0m  [0m
[7m[0m  	public static MBReadOnlyList<MobileParty> AllCaravanParties => Campaign.Current.CaravanParties;[0m
[7m[0m  [0m
[7m[0m  	public static MBReadOnlyList<MobileParty> AllPatrolParties => Campaign.Current.PatrolParties;[0m
  	public static MBReadOnlyList<MobileParty> AllCustomParties => Campaign.Current.CustomParties;
  
> 	public static MBReadOnlyList<MobileParty> AllPartiesWithout[7mPartyComponent [0m[7m[0m=> [0m
[7m[0mCampaign.Current.PartiesWithoutPartyComponent;[0m
[7m[0m  [0m
[7m[0m  	public static int Count => Campaign.Current.MobileParties.Count;[0m
[7m[0m  [0m
[7m[0m  	public static MobileParty ConversationParty => Campaign.Current.ConversationManager.ConversationParty;[0m
[7m[0m  [0m
[7m[0m  	public TextObject Name[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  				return Party.CustomName;
  			}
> 			if (_[7mpartyComponent[0m != null)[0m
[7m[0m  			{[0m
> 				return _[7mpartyComponent[0m.Name;[0m
[7m[0m  			}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mDebug.FailedAssert("UnnamedMobileParty", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\MobileParty.cs", [0m[7m[0m"Name", [0m[7m[0m122);[0m
[7m[0m  			return new TextObject("{=!}unnamedMobileParty");[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(1002)][0m
[7m[0m  	public Settlement LastVisitedSettlement { get; private set; }[0m
  	public float Aggressiveness { get; set; }
  
> 	public int PaymentLimit => _[7mpartyComponent[0m?.WagePaymentLimit [0m[7m[0m?? [0m
[7m[0mCampaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;[0m
[7m[0m  [0m
[7m[0m  	public Banner Banner[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (Party.CustomBanner != null)[0m
[7m[0m  			{[0m
[7m[0m  				return Party.CustomBanner;[0m
  			}
> 			if ([7mPartyComponent[0m != null && PartyComponent.GetDefaultComponentBanner() != null)[0m
[7m[0m  			{[0m
> 				return [7mPartyComponent[0m.GetDefaultComponentBanner();[0m
[7m[0m  			}[0m
> 			if ([7mMapFaction[0m != null)[0m
[7m[0m  			{[0m
> 				return [7mMapFaction[0m.Banner;[0m
[7m[0m  			}[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(1005)][0m
[7m[0m  	public Vec2 ArmyPositionAdder { get; private set; }[0m
[7m[0m  [0m
  		get
  		{
> 			if (IsLordParty && [7mLeaderHero[0m != null)[0m
[7m[0m  			{[0m
> 				return [7mLeaderHero[0m.Gold;[0m
[7m[0m  			}[0m
[7m[0m  			return _partyTradeGold;[0m
[7m[0m  		}[0m
[7m[0m  		set[0m
[7m[0m  		{[0m
> 			if (IsLordParty && [7mLeaderHero[0m != null)[0m
[7m[0m  			{[0m
> 				[7mLeaderHero[0m.Gold = TaleWorlds.Library.MathF.Max(value, 0);[0m
[7m[0m  			}[0m
[7m[0m  			else[0m
[7m[0m  			{[0m
[7m[0m  				_partyTradeGold = TaleWorlds.Library.MathF.Max(value, 0);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
  			if (settlement == null)
  			{
> 				[7mPartyComponent[0m partyComponent = _partyComponent;[0m
> 				if ([7mpartyComponent[0m == null)[0m
[7m[0m  				{[0m
[7m[0m  					return null;[0m
[7m[0m  				}[0m
> 				settlement = [7mpartyComponent[0m.HomeSettlement;[0m
[7m[0m  			}[0m
[7m[0m  			return settlement;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public MobileParty AttachedTo[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  	}
  
> 	public Hero [7mLeaderHero[0m => PartyComponent?.Leader;[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(1070)][0m
[7m[0m  	private Hero Scout { get; set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(1072)][0m
[7m[0m  	private Hero Engineer { get; set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(1071)][0m
  	private Hero Navigator { get; set; }
  
> 	[7mpublic Hero Owner[0m => _partyComponent?.PartyOwner;[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveScout[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (Scout == null || Scout.PartyBelongedTo != this)[0m
[7m[0m  			{[0m
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return Scout;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveQuartermaster[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (Quartermaster == null || Quartermaster.PartyBelongedTo != this)
  			{
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return Quartermaster;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveEngineer[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (Engineer == null || Engineer.PartyBelongedTo != this)
  			{
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return Engineer;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveSurgeon[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (Surgeon == null || Surgeon.PartyBelongedTo != this)
  			{
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return Surgeon;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveFirstMate[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (FirstMate == null || FirstMate.PartyBelongedTo != this)
  			{
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return FirstMate;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public Hero EffectiveNavigator[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (Navigator == null || Navigator.PartyBelongedTo != this)
  			{
> 				return [7mLeaderHero[0m;[0m
[7m[0m  			}[0m
[7m[0m  			return Navigator;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public float RecentEventsMorale[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  			if (_actualClan != value)
  			{
> 				if (_actualClan != null && value != null && [7mPartyComponent[0m is WarPartyComponent warPartyComponent)[0m
[7m[0m  				{[0m
> 					war[7mPartyComponent[0m.OnClanChange(_actualClan, value);[0m
[7m[0m  				}[0m
[7m[0m  				_actualClan = value;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public ExplainedNumber FoodChangeExplained[0m
[7m[0m  	{[0m
  	public bool IsMainParty => this == MainParty;
  
> 	public IFaction [7mMapFaction[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (ActualClan != null)[0m
[7m[0m  			{[0m
> 				return ActualClan.[7mMapFaction[0m;[0m
[7m[0m  			}[0m
[7m[0m  			if (Party.Owner != null)[0m
[7m[0m  			{[0m
[7m[0m  				if (Party.Owner == Hero.MainHero)[0m
[7m[0m  				{[0m
> 					return Party.Owner.[7mMapFaction[0m;[0m
[7m[0m  				}[0m
[7m[0m  				if (Party.Owner.IsNotable)[0m
[7m[0m  				{[0m
> 					return Party.Owner.HomeSettlement.[7mMapFaction[0m;[0m
[7m[0m  				}[0m
[7m[0m  				if ((IsMilitia || IsGarrison || IsVillager || IsPatrolParty) && HomeSettlement?.OwnerClan != null)[0m
[7m[0m  				{[0m
> 					return HomeSettlement.OwnerClan.[7mMapFaction[0m;[0m
[7m[0m  				}[0m
[7m[0m  				if (IsCaravan || IsBanditBossParty)[0m
[7m[0m  				{[0m
> 					return Party.Owner.[7mMapFaction[0m;[0m
[7m[0m  				}[0m
[7m[0m  				if (_isCurrentlyUsedByAQuest && Party.Owner != null)[0m
[7m[0m  				{[0m
> 					return Party.Owner.[7mMapFaction[0m;[0m
[7m[0m  				}[0m
> 				return ([7mLeaderHero[0m != null) ? LeaderHero.MapFaction : null;[0m
[7m[0m  			}[0m
[7m[0m  			if (HomeSettlement != null)[0m
[7m[0m  			{[0m
> 				return HomeSettlement.OwnerClan.[7mMapFaction[0m;[0m
[7m[0m  			}[0m
> 			return ([7mLeaderHero[0m != null) ? LeaderHero.MapFaction : null;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public TextObject ArmyName[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
[7m[0m  		{[0m
[7m[0m  			if (Army == null || Army.LeaderParty != this)[0m
  	}
  
> 	public Villager[7mPartyComponent[0m VillagerPartyComponent => _partyComponent as VillagerPartyComponent;[0m
[7m[0m  [0m
> 	public Caravan[7mPartyComponent[0m CaravanPartyComponent => _partyComponent as CaravanPartyComponent;[0m
[7m[0m  [0m
> 	public War[7mPartyComponent[0m WarPartyComponent => _partyComponent as WarPartyComponent;[0m
[7m[0m  [0m
> 	public Bandit[7mPartyComponent[0m BanditPartyComponent => _partyComponent as BanditPartyComponent;[0m
[7m[0m  [0m
> 	public Patrol[7mPartyComponent[0m PatrolPartyComponent => _partyComponent as PatrolPartyComponent;[0m
[7m[0m  [0m
> 	public Lord[7mPartyComponent[0m LordPartyComponent => _partyComponent as LordPartyComponent;[0m
[7m[0m  [0m
> 	public Garrison[7mPartyComponent[0m GarrisonPartyComponent => _partyComponent as GarrisonPartyComponent;[0m
[7m[0m  [0m
> 	public [7mPartyComponent[0m PartyComponent => _partyComponent;[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	public bool IsMilitia { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
[7m[0m  	public bool IsLordParty { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	[CachedData][0m
  			if (IsBandit)
  			{
> 				return Bandit[7mPartyComponent[0m.IsBossParty;[0m
[7m[0m  			}[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public bool AvoidHostileActions[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  		{
> 			if (_[7mpartyComponent[0m != null)[0m
[7m[0m  			{[0m
> 				return _[7mpartyComponent[0m.AvoidHostileActions;[0m
[7m[0m  			}[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	internal static void AutoGeneratedStaticCollectObjectsMobileParty(object o, List<object> collectedObjects)[0m
[7m[0m  	{[0m
[7m[0m  		((MobileParty)o).AutoGeneratedInstanceCollectObjects(collectedObjects);[0m
  		collectedObjects.Add(_actualClan);
  		CampaignVec2.AutoGeneratedStaticCollectObjectsCampaignVec2(_position, collectedObjects);
> 		collectedObjects.Add(_[7mpartyComponent[0m);[0m
[7m[0m  		collectedObjects.Add(LastVisitedSettlement);[0m
[7m[0m  		collectedObjects.Add(Ai);[0m
[7m[0m  		collectedObjects.Add(Party);[0m
[7m[0m  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(StationaryStartTime, collectedObjects);[0m
[7m[0m  		collectedObjects.Add(Anchor);[0m
[7m[0m  		CampaignVec2.AutoGeneratedStaticCollectObjectsCampaignVec2(EndPositionForNavigationTransition, collectedObjects);[0m
[7m[0m  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(NavigationTransitionDuration, collectedObjects);[0m
[7m[0m  		collectedObjects.Add(Scout);[0m
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_[7mpartyComponent[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((MobileParty)o)._[7mpartyComponent[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetLandNavigationAccess(bool access)[0m
[7m[0m  	{[0m
[7m[0m  		HasLandNavigationCapability = access;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public override TextObject GetName()[0m
  	public void SetWagePaymentLimit(int newLimit)
  	{
> 		[7mPartyComponent[0m?.SetWagePaymentLimit(newLimit);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetNavalVisualAsDirty()[0m
[7m[0m  	{[0m
[7m[0m  		IsNavalVisualDirty = true;[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void OnNavalVisualsUpdated()[0m
  				Ai.InitializeForOldSaves((float)memberValueBySaveId, (float)memberValueBySaveId2, 
(CampaignTime)memberValueBySaveId3, (int)memberValueBySaveId4, (AiBehavior)memberValueBySaveId5, 
(Vec2)memberValueBySaveId6, (bool)memberValueBySaveId7, (bool)memberValueBySaveId8, (memberValueBySaveId9 != null) ? 
((MoveModeType)memberValueBySaveId9) : MoveModeType.Hold, (MobileParty)memberValueBySaveId10, 
(Vec2)memberValueBySaveId11, (Vec2)memberValueBySaveId12, (Vec2)fieldValueBySaveId, (Vec2)memberValueBySaveId13, 
oldAiBehaviorMapEntity, ((CampaignTime?)memberValueBySaveId14) ?? CampaignTime.Never);
  			}
> 			Update[7mPartyComponent[0mFlags();[0m
[7m[0m  			if (IsGarrison || IsLordParty)[0m
[7m[0m  			{[0m
[7m[0m  				object memberValueBySaveId15 = objectLoadData.GetMemberValueBySaveId(1010);[0m
[7m[0m  				if (memberValueBySaveId15 != null)[0m
[7m[0m  				{[0m
[7m[0m  					SetWagePaymentLimit((int)memberValueBySaveId15);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  	public void ChangePartyLeader(Hero newLeader)
  	{
> 		[7mPartyComponent[0m.ChangePartyLeader(newLeader);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void OnPartyInteraction(MobileParty engagingParty)[0m
[7m[0m  	{[0m
[7m[0m  		MobileParty mobileParty = this;[0m
[7m[0m  		if (mobileParty.AttachedTo != null && engagingParty != mobileParty.AttachedTo)[0m
[7m[0m  		{[0m
[7m[0m  			mobileParty = mobileParty.AttachedTo;[0m
  			if (mobileParty.MapEvent != null)
  			{
> 				flag = mobileParty.MapEvent.MapEventSettlement == mobileParty.CurrentSettlement && 
(mobileParty.MapEvent.AttackerSide.LeaderParty.[7mMapFaction [0m[7m[0m== [0m[7m[0mengagingParty.MapFaction [0m[7m[0m|| [0m
[7m[0mmobileParty.MapEvent.DefenderSide.LeaderParty.MapFaction [0m[7m[0m== [0m[7m[0mengagingParty.MapFaction);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
[7m[0m  			flag = engagingParty != MainParty || !mobileParty.IsEngaging || mobileParty.ShortTermTargetParty != MainParty;[0m
[7m[0m  		}[0m
[7m[0m  		if (flag)[0m
[7m[0m  		{[0m
  	public void RemovePartyLeader()
  	{
> 		[7mPartyComponent[0m.ChangePartyLeader(null);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void CheckPositionsForMapChangeAndUpdateIfNeeded()[0m
[7m[0m  	{[0m
[7m[0m  		if (!Position.IsValid() || IsCurrentlyAtSea == Position.IsOnLand)[0m
[7m[0m  		{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mCampaignVec2 [0m[7m[0mclosestNavMeshFaceCenterPositionForPosition [0m[7m[0m= [0m
[7m[0mNavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(Position, [0m
[7m[0mCampaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType((!IsCurrentlyAtSea) [0m[7m[0m? [0m
[7m[0mNavigationType.Default [0m[7m[0m: [0m[7m[0mNavigationType.Naval));[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mPosition [0m[7m[0m= [0m[7m[0mNavigationHelper.FindPointAroundPosition(closestNavMeshFaceCenterPositionForPosition, [0m
[7m[0mNavigationCapability, [0m[7m[0m8f, [0m[7m[0m1f);[0m
  	protected override void PreAfterLoad()
  	{
> 		Update[7mPartyComponent[0mFlags();[0m
> 		[7mPartyComponent[0m?.Initialize(this);[0m
[7m[0m  		ComputePathAfterLoad();[0m
[7m[0m  		Anchor?.InitializeOnLoad(this);[0m
[7m[0m  		Ai.PreAfterLoad();[0m
[7m[0m  		if (_disorganizedUntilTime.IsFuture)[0m
[7m[0m  		{[0m
[7m[0m  			_isDisorganized = true;[0m
[7m[0m  		}[0m
[7m[0m  		if (!MBSaveLoad.IsUpdatingGameVersion)[0m
  		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.2"))
  		{
> 			if (([7mLeaderHero [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0mthis [0m[7m[0m!= [0m[7m[0mMainParty [0m[7m[0m&& [0m[7m[0mLeaderHero.PartyBelongedTo [0m[7m[0m!= [0m[7m[0mthis) [0m[7m[0m|| [0m[7m[0m(MapEvent [0m[7m[0m== [0m[7m[0mnull [0m[7m[0m&& [0m
[7m[0mbase.StringId.Contains("troops_of_")))[0m
[7m[0m  			{[0m
[7m[0m  				DestroyPartyAction.Apply(null, this);[0m
[7m[0m  			}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(MapEvent [0m[7m[0m== [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0m(base.StringId.Contains("troops_of_CharacterObject") [0m[7m[0m|| [0m
[7m[0mbase.StringId.Contains("troops_of_TaleWorlds.CampaignSystem.CharacterObject")))[0m
[7m[0m  			{[0m
[7m[0m  				if (!IsActive)[0m
[7m[0m  				{[0m
[7m[0m  					IsActive = true;[0m
  			}
  		}
> 		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && IsActive && [7mMapFaction[0m == null)[0m
[7m[0m  		{[0m
[7m[0m  			if (MapEvent != null)[0m
[7m[0m  			{[0m
[7m[0m  				MapEventSide = null;[0m
[7m[0m  			}[0m
[7m[0m  			RemoveParty();[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.15.113119")) [0m[7m[0m&& [0m[7m[0m(IsGarrison [0m
[7m[0m|| [0m[7m[0mIsMilitia) [0m[7m[0m&& [0m[7m[0mCurrentSettlement [0m[7m[0m== [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0m(MapEvent [0m[7m[0m== [0m[7m[0mnull [0m[7m[0m|| [0m[7m[0m(!MapEvent.IsSallyOut [0m[7m[0m&& [0m
[7m[0m!MapEvent.IsBlockadeSallyOut)))[0m
  		if (TargetParty != null)
  		{
> 			IFaction [7mmapFaction[0m = TargetParty.MapFaction;[0m
> 			if ([7mmapFaction[0m == null || !mapFaction.IsAtWarWith(MapFaction))[0m
[7m[0m  			{[0m
[7m[0m  				goto IL_024d;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		if (TargetSettlement != null)[0m
[7m[0m  		{[0m
> 			IFaction [7mmapFaction[0m2 = TargetSettlement.MapFaction;
> 			if ([7mmapFaction[0m2 == null || !mapFaction2.IsAtWarWith(MapFaction))[0m
[7m[0m  			{[0m
[7m[0m  				goto IL_024d;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		if (ShortTermTargetParty != null)[0m
[7m[0m  		{[0m
[7m[0m  			MobileParty shortTermTargetParty = ShortTermTargetParty;[0m
> 			if (shortTermTargetParty != null && shortTermTargetParty.[7mMapFaction[0m?.IsAtWarWith(MapFaction) == true)[0m
[7m[0m  			{[0m
[7m[0m  				return;[0m
[7m[0m  			}[0m
[7m[0m  			goto IL_024d;[0m
[7m[0m  		}[0m
[7m[0m  		return;[0m
[7m[0m  		IL_024d:[0m
[7m[0m  		SetMoveModeHold();[0m
  		if (IsActive)
  		{
> 			if ([7mLeaderHero[0m != null && CurrentSettlement != null && CurrentSettlement == LeaderHero.HomeSettlement)[0m
[7m[0m  			{[0m
> 				[7mLeaderHero[0m.PassedTimeAtHomeSettlement++;[0m
[7m[0m  			}[0m
[7m[0m  			Anchor.HourlyTick();[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void MovePartyToTheClosestLand()[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mint[] [0m[7m[0minvalidTerrainTypesForNavigationType [0m[7m[0m= [0m
[7m[0mCampaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(NavigationType.All);[0m
  	{
  		RecentEventsMorale -= RecentEventsMorale * 0.1f;
> 		if ([7mLeaderHero[0m != null)[0m
[7m[0m  		{[0m
> 			[7mLeaderHero[0m.PassedTimeAtHomeSettlement *= 0.9f;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public TextObject GetBehaviorText()[0m
[7m[0m  	{[0m
[7m[0m  		TextObject textObject = TextObject.GetEmpty();[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(Army [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0m(AttachedTo [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m|| [0m[7m[0mArmy.LeaderParty [0m[7m[0m== [0m[7m[0mthis) [0m[7m[0m&& [0m[7m[0m!Army.LeaderParty.IsEngaging [0m[7m[0m&& [0m
[7m[0m!Army.LeaderParty.IsFleeing())[0m
[7m[0m  		{[0m
  						textObject = new TextObject("{=LAt87KjS}Waiting for ally parties to defend {TARGET_SETTLEMENT}.");
  					}
> 					else if (ShortTermTargetParty != null && ShortTermTargetParty.[7mMapFaction[0m == MapFaction)[0m
[7m[0m  					{[0m
[7m[0m  						textObject = new TextObject("{=yD7rL5Nc}Helping ally party to defend {TARGET_SETTLEMENT}.");[0m
[7m[0m  					}[0m
[7m[0m  				}[0m
[7m[0m  				textObject.SetTextVariable("TARGET_SETTLEMENT", TargetSettlement.Name);[0m
[7m[0m  			}[0m
[7m[0m  			else if (DefaultBehavior == AiBehavior.RaidSettlement)[0m
[7m[0m  			{[0m
  				{
  					bool flag = IsLordParty && !AiBehaviorTarget.IsOnLand;
> 					textObject = ((!(Campaign.Current.Models.MapDistanceModel.GetDistance(this, TargetSettlement, IsTargetingPort, 
NavigationCapability, out estimatedLandRatio) > 
Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(NavigationType.All) * 0.5f)) ? ((!flag) ? 
new TextObject("{=yUVv3z5V}Patrolling around {TARGET_SETTLEMENT}.") : 
(TargetSettlement.[7mMapFaction[0m.IsAtWarWith(MapFaction) [0m[7m[0m? [0m[7m[0mnew [0m[7m[0mTextObject("{=VQY0e6hF}Threatening [0m[7m[0mthe [0m[7m[0mcoastal [0m[7m[0mwaters [0m[7m[0moff [0m
[7m[0m{TARGET_SETTLEMENT}.") [0m[7m[0m: [0m[7m[0mnew [0m[7m[0mTextObject("{=8qvUbTvW}Guarding [0m[7m[0mthe [0m[7m[0mcoastal [0m[7m[0mwaters [0m[7m[0moff [0m[7m[0m{TARGET_SETTLEMENT}."))) [0m[7m[0m: [0m
[7m[0m((!flag) [0m[7m[0m? [0m[7m[0mnew [0m[7m[0mTextObject("{=MNoogAgk}Heading [0m[7m[0mto [0m[7m[0mpatrol [0m[7m[0maround [0m[7m[0m{TARGET_SETTLEMENT}.") [0m[7m[0m: [0m
[7m[0m(TargetSettlement.MapFaction.IsAtWarWith(MapFaction) [0m[7m[0m? [0m[7m[0mnew [0m[7m[0mTextObject("{=YIKb1kob}Heading [0m[7m[0mto [0m[7m[0mthe [0m[7m[0mcoastal [0m[7m[0mwaters [0m[7m[0moff [0m
[7m[0m{TARGET_SETTLEMENT}.") [0m[7m[0m: [0m[7m[0mnew [0m[7m[0mTextObject("{=avhlH79s}Heading [0m[7m[0mto [0m[7m[0mpatrol [0m[7m[0mthe [0m[7m[0mcoastal [0m[7m[0mwaters [0m[7m[0moff [0m[7m[0m{TARGET_SETTLEMENT}."))));[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mtextObject.SetTextVariable("TARGET_SETTLEMENT", [0m[7m[0m(TargetSettlement [0m[7m[0m!= [0m[7m[0mnull) [0m[7m[0m? [0m[7m[0mTargetSettlement.Name [0m[7m[0m: [0m
[7m[0mHomeSettlement.Name);[0m
[7m[0m  				}[0m
[7m[0m  				else[0m
[7m[0m  				{[0m
[7m[0m  					textObject = new TextObject("{=TaK6ydAx}Travelling.");[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  			else if (ShortTermBehavior == AiBehavior.GoToSettlement || DefaultBehavior == AiBehavior.GoToSettlement)[0m
  					else
  					{
> 						textObject = ((ShortTermTargetParty != null && ShortTermTargetParty.[7mMapFaction[0m.IsAtWarWith(MapFaction)) [0m[7m[0m? [0m[7m[0mnew [0m
[7m[0mTextObject("{=NRpbagbZ}Running [0m[7m[0mto [0m[7m[0m{TARGET_PARTY}.") [0m[7m[0m: [0m[7m[0mnew [0m[7m[0mTextObject("{=EQHq3bHM}Travelling [0m[7m[0mto [0m[7m[0m{TARGET_PARTY}"));[0m
[7m[0m  						textObject.SetTextVariable("TARGET_PARTY", ShortTermTargetSettlement.Name);[0m
[7m[0m  					}[0m
[7m[0m  				}[0m
[7m[0m  				else if (DefaultBehavior == AiBehavior.GoToSettlement && TargetSettlement != null)[0m
[7m[0m  				{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mtextObject [0m[7m[0m= [0m[7m[0m((CurrentSettlement [0m[7m[0m!= [0m[7m[0mTargetSettlement) [0m[7m[0m? [0m[7m[0mnew [0m[7m[0mTextObject("{=EQHq3bHM}Travelling [0m[7m[0mto [0m
[7m[0m{TARGET_PARTY}") [0m[7m[0m: [0m[7m[0mnew [0m[7m[0mTextObject("{=Y65gdbrx}Waiting [0m[7m[0min [0m[7m[0m{TARGET_PARTY}."));[0m
[7m[0m  					textObject.SetTextVariable("TARGET_PARTY", TargetSettlement.Name);[0m
[7m[0m  				}[0m
  			list.Add(HomeSettlement);
  		}
> 		[7mPartyComponent[0m?.Finish();[0m
[7m[0m  		ActualClan = null;[0m
[7m[0m  		Anchor = null;[0m
[7m[0m  		Campaign.Current.CampaignObjectManager.RemoveMobileParty(this);[0m
[7m[0m  		foreach (Settlement item in list)[0m
[7m[0m  		{[0m
[7m[0m  			item.SettlementComponent.OnRelatedPartyRemoved(this);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  	private bool IsLastSpeedCacheInvalid()
  	{
> 		Hero [7mleaderHero[0m = LeaderHero;[0m
> 		bool flag = !IsActive || [7mleaderHero[0m == null || leaderHero.PartyBelongedToAsPrisoner != null;[0m
[7m[0m  		bool isNight = Campaign.Current.IsNight;[0m
[7m[0m  		Vec2 vec = _lastWind;[0m
[7m[0m  		if (IsCurrentlyAtSea)[0m
[7m[0m  		{[0m
[7m[0m  			vec = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(_lastNavigationFace.FaceIndex [0m[7m[0m== [0m[7m[0mCurrentNavigationFace.FaceIndex [0m[7m[0m&& [0m[7m[0m_partyLastCheckIsPrisoner [0m[7m[0m== [0m[7m[0mflag [0m[7m[0m&& [0m
[7m[0m_partyLastCheckAtNight [0m[7m[0m== [0m[7m[0misNight [0m[7m[0m&& [0m[7m[0m!(Math.Abs(_lastWind.RotationInRadians [0m[7m[0m- [0m[7m[0mvec.RotationInRadians) [0m[7m[0m> [0m[7m[0m0.06f))[0m
[7m[0m  		{[0m
  		else if (IsLastSpeedCacheInvalid())
  		{
> 			Hero [7mleaderHero[0m = LeaderHero;[0m
> 			bool partyLastCheckIsPrisoner = !IsActive || [7mleaderHero[0m == null || leaderHero.PartyBelongedToAsPrisoner != null;[0m
[7m[0m  			bool isNight = Campaign.Current.IsNight;[0m
[7m[0m  			if (IsCurrentlyAtSea)[0m
[7m[0m  			{[0m
[7m[0m  				_lastWind = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);[0m
[7m[0m  			}[0m
[7m[0m  			_lastNavigationFace = CurrentNavigationFace;[0m
[7m[0m  			_partyLastCheckIsPrisoner = partyLastCheckIsPrisoner;[0m
[7m[0m  			_partyLastCheckAtNight = isNight;[0m
  			return EffectiveNavigator?.GetPerkValue(perk) ?? false;
  		case PartyRole.PartyLeader:
> 			return [7mLeaderHero[0m?.GetPerkValue(perk) ?? false;[0m
[7m[0m  		case PartyRole.ArmyCommander:[0m
> 			return Army?.LeaderParty?.[7mLeaderHero[0m?.GetPerkValue(perk) ?? false;[0m
[7m[0m  		case PartyRole.PartyMember:[0m
[7m[0m  			foreach (TroopRosterElement item in MemberRoster.GetTroopRoster())[0m
[7m[0m  			{[0m
[7m[0m  				if (item.Character.IsHero && item.Character.HeroObject.GetPerkValue(perk))[0m
[7m[0m  				{[0m
[7m[0m  					return true;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  		case PartyRole.Personal:
  			Debug.FailedAssert("personal perk is called in party", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\MobileParty.cs", "HasPerk", 3195);
> 			return [7mLeaderHero[0m?.GetPerkValue(perk) ?? false;[0m
[7m[0m  		case PartyRole.ClanLeader:[0m
> 			if ([7mLeaderHero[0m != null)[0m
[7m[0m  			{[0m
> 				return [7mLeaderHero[0m.Clan?.Leader?.GetPerkValue(perk) ?? false;[0m
[7m[0m  			}[0m
[7m[0m  			return false;[0m
[7m[0m  		default:[0m
[7m[0m  			return false;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void SetHeroPartyRole(Hero hero, PartyRole partyRole)[0m
  		return partyRole switch
  		{
> 			PartyRole.PartyLeader => [7mLeaderHero[0m, [0m
[7m[0m  			PartyRole.Surgeon => Surgeon, [0m
[7m[0m  			PartyRole.Engineer => Engineer, [0m
[7m[0m  			PartyRole.Quartermaster => Quartermaster, [0m
[7m[0m  			PartyRole.Scout => Scout, [0m
[7m[0m  			PartyRole.FirstMate => FirstMate, [0m
[7m[0m  			PartyRole.Navigator => Navigator, [0m
[7m[0m  			_ => null, [0m
[7m[0m  		};[0m
  		return partyRole switch
  		{
> 			PartyRole.PartyLeader => [7mLeaderHero[0m, [0m
[7m[0m  			PartyRole.Surgeon => EffectiveSurgeon, [0m
[7m[0m  			PartyRole.Engineer => EffectiveEngineer, [0m
[7m[0m  			PartyRole.Quartermaster => EffectiveQuartermaster, [0m
[7m[0m  			PartyRole.Scout => EffectiveScout, [0m
[7m[0m  			PartyRole.FirstMate => EffectiveFirstMate, [0m
[7m[0m  			PartyRole.Navigator => EffectiveNavigator, [0m
[7m[0m  			_ => null, [0m
[7m[0m  		};[0m
  			for (MobileParty mobileParty = FindNextLocatable(ref data); mobileParty != null; mobileParty = 
FindNextLocatable(ref data))
  			{
> 				if (mobileParty.[7mMapFaction[0m.IsAtWarWith(MapFaction))[0m
[7m[0m  				{[0m
[7m[0m  					flag = false;[0m
[7m[0m  					break;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  			if (!flag)[0m
[7m[0m  			{[0m
[7m[0m  				continue;[0m
  	}
  
> 	public static MobileParty CreateParty(string stringId, [7mPartyComponent[0m component)[0m
[7m[0m  	{[0m
[7m[0m  		stringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>(stringId);[0m
[7m[0m  		MobileParty mobileParty = new MobileParty();[0m
[7m[0m  		mobileParty.StringId = stringId;[0m
> 		mobileParty._[7mpartyComponent[0m = component;
> 		mobileParty.Update[7mPartyComponent[0mFlags();
> 		mobileParty._[7mpartyComponent[0m?.Create(mobileParty);
> 		mobileParty._[7mpartyComponent[0m?.Initialize(mobileParty);[0m
[7m[0m  		Campaign.Current.CampaignObjectManager.AddMobileParty(mobileParty);[0m
[7m[0m  		CampaignEventDispatcher.Instance.OnMobilePartyCreated(mobileParty);[0m
[7m[0m  		CampaignEventDispatcher.Instance.OnMapInteractableCreated(mobileParty.Party);[0m
[7m[0m  		return mobileParty;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void Set[7mPartyComponent[0m(PartyComponent partyComponent, bool firstTimePartyComponentCreation = true)[0m
[7m[0m  	{[0m
> 		if (_[7mpartyComponent[0m == partyComponent)[0m
[7m[0m  		{[0m
[7m[0m  			return;[0m
[7m[0m  		}[0m
> 		if (_[7mpartyComponent[0m != null)[0m
[7m[0m  		{[0m
> 			_[7mpartyComponent[0m.Finish();[0m
[7m[0m  		}[0m
> 		Campaign.Current.CampaignObjectManager.Before[7mPartyComponent[0mChanged(this);
> 		_[7mpartyComponent[0m = partyComponent;
> 		Update[7mPartyComponent[0mFlags();
> 		Campaign.Current.CampaignObjectManager.After[7mPartyComponent[0mChanged(this);
> 		if (_[7mpartyComponent[0m != null)[0m
[7m[0m  		{[0m
> 			if (firstTime[7mPartyComponent[0mCreation)[0m
[7m[0m  			{[0m
> 				_[7mpartyComponent[0m.Create(this);[0m
[7m[0m  			}[0m
> 			_[7mpartyComponent[0m.Initialize(this);[0m
[7m[0m  		}[0m
[7m[0m  		Party.SetVisualAsDirty();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void Update[7mPartyComponent[0mFlags()[0m
[7m[0m  	{[0m
> 		IsLordParty = _[7mpartyComponent[0m is LordPartyComponent;
> 		IsVillager = _[7mpartyComponent[0m is VillagerPartyComponent;
> 		IsMilitia = _[7mpartyComponent[0m is MilitiaPartyComponent;
> 		IsCaravan = _[7mpartyComponent[0m is CaravanPartyComponent;
> 		IsPatrolParty = _[7mpartyComponent[0m is PatrolPartyComponent;
> 		IsGarrison = _[7mpartyComponent[0m is GarrisonPartyComponent;
> 		IsCustomParty = _[7mpartyComponent[0m is CustomPartyComponent;
> 		IsBandit = _[7mpartyComponent[0m is BanditPartyComponent;[0m
[7m[0m  	}[0m
[7m[0m  }[0m


context compacted
codex
| # | Severity | Title | File | Confirmed/Disputed |
|---|---|---|---|---|
| 1 | HIGH | Snow feats miss vanilla snow weather | `TaomPartySpeedModel.cs:31` | CONFIRMED S1 |
| 2 | MEDIUM | Mordor night bonus applies at sea where vanilla has no night penalty | `TaomPartySpeedModel.cs:33` | CONFIRMED |
| 3 | MEDIUM | Culture lookup is narrower than vanilla feat lookup | `TaomPartySpeedModel.cs:30` | CONFIRMED |
| 4 | MEDIUM | Hot path allocates a culture adapter every speed recalc | `CultureFeatAdapter.cs:36` | CONFIRMED |
| 5 | LOW | Duplicate XML-doc summary on terrain mapping helper | `TaomPartySpeedModel.cs:43` | CONFIRMED |
| S2 | None | `TerrainType.Snow` exists and is value `3` | installed `TaleWorlds.Core.dll` | DISPUTED as bug |
| S3 | None | XSLT append templates preserve vanilla feats and do not double emit | `spcultures.xslt` | DISPUTED as bug |
| S4 | None | Night feat stacks additively and offsets vanilla night penalty | `CulturalFeatsService.cs:115` | CONFIRMED no bug |
| S5 | None | Mordor magnitudes are correct | `TaomCulturalFeats.cs` | CONFIRMED no bug |
| S6 | None | Harad/Aserai double desert is intentional; Battania terrain differs | `spcultures.xslt` | DISPUTED as bug |
| S7 | None | Feat registration ordering is safe | `Campaign_InitializeDefaultCampaignObjects_Patch.cs:12` | DISPUTED as bug |

**HIGH**

1. Claim: Erebor/Gundabad snow feats do not apply to vanilla snowy/blizzard movement, because TAOM only maps face terrain.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:31
var terrain = MapTerrain(
    Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
```

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:64
TerrainType.Snow => TerrainKind.Snow,
```

Evidence, installed v1.4.5 vanilla `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed`:

```csharp
TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
...
MapWeatherModel.WeatherEvent weatherEventInPosition =
    Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mobileParty.Position.ToVec2());
if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy ||
    weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
{
    faceTerrainType = TerrainType.Snow;
    finalSpeed.AddFactor(-0.1f, _snow);
}
```

I cannot prove from source that the TAOM map has zero navmesh faces with `FaceGroupIndex == 3`; vanilla `MapScene.GetFaceTerrainType` simply returns `(TerrainType)navMeshFace.FaceGroupIndex`, so authored Snow faces can exist. But vanilla’s actual snow penalty is weather-driven, and TAOM does not read weather. That means the new snow feats will not offset the vanilla snow penalty unless the same party is also standing on a Snow-authored face.

Suggested fix: keep face-terrain Snow support, but also map `Snowy`/`Blizzard` weather to `TerrainKind.Snow`, matching vanilla’s condition.

**MEDIUM**

2. Claim: Mordor night speed applies at sea, where vanilla does not apply the night penalty it is meant to offset.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:33
var isNight = Campaign.Current?.IsNight ?? false;
```

```csharp
// Main/Features/CulturalFeats/CulturalFeatsService.cs:115
if (isNight)
    ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
```

Evidence, vanilla:

```csharp
if (!mobileParty.IsCurrentlyAtSea)
{
    if (Campaign.Current.IsNight)
    {
        finalSpeed.AddFactor(-0.25f, _night);
        ...
    }
}
```

Suggested fix: pass `isNight && !mobileParty.IsCurrentlyAtSea` unless Mordor is intentionally supposed to receive a naval night bonus. If intentional, document it and add a test.

3. Claim: TAOM uses only `Party.Owner.Culture`, while vanilla feat checks use broader party culture resolution.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:30
var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
```

Evidence, vanilla `PartyBaseHelper.HasFeat`:

```csharp
if (party.LeaderHero != null)
    return party.LeaderHero.Culture.HasFeat(feat);
if (party.Culture != null)
    return party.Culture.HasFeat(feat);
if (party.Owner != null)
    return party.Owner.Culture.HasFeat(feat);
if (party.Settlement != null)
    return party.Settlement.Culture.HasFeat(feat);
```

This can miss terrain feats for parties whose effective culture comes from leader, party/map faction, or settlement rather than owner. Suggested fix: resolve culture at the GameModel boundary using vanilla-equivalent precedence, then pass the adapted culture into the service.

4. Claim: The speed hot path allocates a new adapter per party speed calculation.

Evidence:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:30
var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
```

```csharp
// Main/Features/CulturalFeats/CultureFeatAdapter.cs:36
public static ICultureFeatAdapter? FromOrNull(CultureObject? culture)
    => culture == null ? null : new CultureFeatAdapter(culture);
```

No LINQ or `IoC.Resolve` was found in `CalculateFinalSpeed`, `MapTerrain`, `CountMountedAndTotal`, or `ApplyTerrainSpeedFeats`. `TroopRoster.GetTroopRoster()` uses a cached list after validation. The avoidable allocation is the adapter.

Suggested fix: use a non-allocating static helper at the boundary or cache adapters per `CultureObject`.

**LOW**

5. Claim: `TaomPartySpeedModel` has a duplicated/dangling XML-doc summary.

Evidence:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:43
/// <summary>
/// Counts mounted and total troops using vanilla CharacterObject.IsMounted semantics.
/// </summary>
/// <summary>
/// Maps TaleWorlds terrain enum to TAOM-owned primitive enum.
```

Suggested fix: remove the first summary or move it above `CountMountedAndTotal`.

**Known Suspects**

S2: `TerrainType.Snow` exists. Installed `TaleWorlds.Core.dll` decompiles to:

```csharp
public enum TerrainType
{
    Plain = 1,
    Desert = 2,
    Snow = 3,
    Forest = 4,
    Steppe = 5,
    ...
}
```

S3: XSLT is correct. I transformed installed `SandBoxCore/ModuleData/spcultures.xml` through `spcultures.xslt`; each checked culture had exactly one `<cultural_feats>`. Examples: `aserai` preserved `[aserai_cheaper_caravans, aserai_desert_speed, aserai_increased_wages]` and appended `taom_harad_desert_speed`; `battania` preserved vanilla forest/militia/construction feats and appended `taom_khand_steppe_speed`.

S4: Confirmed. `ExplainedNumber.AddFactor` stacks additively:

```csharp
public void AddFactor(float value, TextObject description = null)
{
    SumOfFactors += value;
}
```

So Mordor plain at land night is vanilla `-25%` plus TAOM `+5%` plus `+10%`, net `-10%` relative to base.

S5: Confirmed. Mordor plain/swamp are `0.05f`; Mordor night is `0.1f`; all other new/reworked terrain speed feats checked are `0.1f`.

S6: Disputed as bug. `aserai_desert_speed` remains and `taom_harad_desert_speed` is appended; docs say TAOM terrain bonuses stack on top of vanilla modifiers. Battania keeps forest speed and gets Khand steppe, a different terrain.

S7: Disputed as bug. TAOM registers feats in a postfix on `Campaign.InitializeDefaultCampaignObjects`, and vanilla loads `SPCultures` afterward via `InitializeBasicObjectXmls()`. Also, `FeatObject` is registered with auto-create behavior, so an unresolved feat id would be silently presumed, not throw. Cross-reference found no terrain-speed feat declared-but-not-registered, registered-but-not-yielded, or applied in the wrong terrain case.

Summary: `CRITICAL: 0 | HIGH: 1 | MEDIUM: 3 | LOW: 1`  
Verdict: `ISSUES FOUND`
SUCCESS: The process with PID 60112 (child process of PID 40632) has been terminated.
SUCCESS: The process with PID 40632 (child process of PID 63800) has been terminated.
SUCCESS: The process with PID 63800 (child process of PID 57104) has been terminated.
SUCCESS: The process with PID 57104 (child process of PID 72732) has been terminated.
tokens used
267,918
| # | Severity | Title | File | Confirmed/Disputed |
|---|---|---|---|---|
| 1 | HIGH | Snow feats miss vanilla snow weather | `TaomPartySpeedModel.cs:31` | CONFIRMED S1 |
| 2 | MEDIUM | Mordor night bonus applies at sea where vanilla has no night penalty | `TaomPartySpeedModel.cs:33` | CONFIRMED |
| 3 | MEDIUM | Culture lookup is narrower than vanilla feat lookup | `TaomPartySpeedModel.cs:30` | CONFIRMED |
| 4 | MEDIUM | Hot path allocates a culture adapter every speed recalc | `CultureFeatAdapter.cs:36` | CONFIRMED |
| 5 | LOW | Duplicate XML-doc summary on terrain mapping helper | `TaomPartySpeedModel.cs:43` | CONFIRMED |
| S2 | None | `TerrainType.Snow` exists and is value `3` | installed `TaleWorlds.Core.dll` | DISPUTED as bug |
| S3 | None | XSLT append templates preserve vanilla feats and do not double emit | `spcultures.xslt` | DISPUTED as bug |
| S4 | None | Night feat stacks additively and offsets vanilla night penalty | `CulturalFeatsService.cs:115` | CONFIRMED no bug |
| S5 | None | Mordor magnitudes are correct | `TaomCulturalFeats.cs` | CONFIRMED no bug |
| S6 | None | Harad/Aserai double desert is intentional; Battania terrain differs | `spcultures.xslt` | DISPUTED as bug |
| S7 | None | Feat registration ordering is safe | `Campaign_InitializeDefaultCampaignObjects_Patch.cs:12` | DISPUTED as bug |

**HIGH**

1. Claim: Erebor/Gundabad snow feats do not apply to vanilla snowy/blizzard movement, because TAOM only maps face terrain.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:31
var terrain = MapTerrain(
    Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
```

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:64
TerrainType.Snow => TerrainKind.Snow,
```

Evidence, installed v1.4.5 vanilla `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed`:

```csharp
TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
...
MapWeatherModel.WeatherEvent weatherEventInPosition =
    Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mobileParty.Position.ToVec2());
if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy ||
    weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
{
    faceTerrainType = TerrainType.Snow;
    finalSpeed.AddFactor(-0.1f, _snow);
}
```

I cannot prove from source that the TAOM map has zero navmesh faces with `FaceGroupIndex == 3`; vanilla `MapScene.GetFaceTerrainType` simply returns `(TerrainType)navMeshFace.FaceGroupIndex`, so authored Snow faces can exist. But vanilla’s actual snow penalty is weather-driven, and TAOM does not read weather. That means the new snow feats will not offset the vanilla snow penalty unless the same party is also standing on a Snow-authored face.

Suggested fix: keep face-terrain Snow support, but also map `Snowy`/`Blizzard` weather to `TerrainKind.Snow`, matching vanilla’s condition.

**MEDIUM**

2. Claim: Mordor night speed applies at sea, where vanilla does not apply the night penalty it is meant to offset.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:33
var isNight = Campaign.Current?.IsNight ?? false;
```

```csharp
// Main/Features/CulturalFeats/CulturalFeatsService.cs:115
if (isNight)
    ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
```

Evidence, vanilla:

```csharp
if (!mobileParty.IsCurrentlyAtSea)
{
    if (Campaign.Current.IsNight)
    {
        finalSpeed.AddFactor(-0.25f, _night);
        ...
    }
}
```

Suggested fix: pass `isNight && !mobileParty.IsCurrentlyAtSea` unless Mordor is intentionally supposed to receive a naval night bonus. If intentional, document it and add a test.

3. Claim: TAOM uses only `Party.Owner.Culture`, while vanilla feat checks use broader party culture resolution.

Evidence, TAOM:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:30
var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
```

Evidence, vanilla `PartyBaseHelper.HasFeat`:

```csharp
if (party.LeaderHero != null)
    return party.LeaderHero.Culture.HasFeat(feat);
if (party.Culture != null)
    return party.Culture.HasFeat(feat);
if (party.Owner != null)
    return party.Owner.Culture.HasFeat(feat);
if (party.Settlement != null)
    return party.Settlement.Culture.HasFeat(feat);
```

This can miss terrain feats for parties whose effective culture comes from leader, party/map faction, or settlement rather than owner. Suggested fix: resolve culture at the GameModel boundary using vanilla-equivalent precedence, then pass the adapted culture into the service.

4. Claim: The speed hot path allocates a new adapter per party speed calculation.

Evidence:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:30
var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
```

```csharp
// Main/Features/CulturalFeats/CultureFeatAdapter.cs:36
public static ICultureFeatAdapter? FromOrNull(CultureObject? culture)
    => culture == null ? null : new CultureFeatAdapter(culture);
```

No LINQ or `IoC.Resolve` was found in `CalculateFinalSpeed`, `MapTerrain`, `CountMountedAndTotal`, or `ApplyTerrainSpeedFeats`. `TroopRoster.GetTroopRoster()` uses a cached list after validation. The avoidable allocation is the adapter.

Suggested fix: use a non-allocating static helper at the boundary or cache adapters per `CultureObject`.

**LOW**

5. Claim: `TaomPartySpeedModel` has a duplicated/dangling XML-doc summary.

Evidence:

```csharp
// Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:43
/// <summary>
/// Counts mounted and total troops using vanilla CharacterObject.IsMounted semantics.
/// </summary>
/// <summary>
/// Maps TaleWorlds terrain enum to TAOM-owned primitive enum.
```

Suggested fix: remove the first summary or move it above `CountMountedAndTotal`.

**Known Suspects**

S2: `TerrainType.Snow` exists. Installed `TaleWorlds.Core.dll` decompiles to:

```csharp
public enum TerrainType
{
    Plain = 1,
    Desert = 2,
    Snow = 3,
    Forest = 4,
    Steppe = 5,
    ...
}
```

S3: XSLT is correct. I transformed installed `SandBoxCore/ModuleData/spcultures.xml` through `spcultures.xslt`; each checked culture had exactly one `<cultural_feats>`. Examples: `aserai` preserved `[aserai_cheaper_caravans, aserai_desert_speed, aserai_increased_wages]` and appended `taom_harad_desert_speed`; `battania` preserved vanilla forest/militia/construction feats and appended `taom_khand_steppe_speed`.

S4: Confirmed. `ExplainedNumber.AddFactor` stacks additively:

```csharp
public void AddFactor(float value, TextObject description = null)
{
    SumOfFactors += value;
}
```

So Mordor plain at land night is vanilla `-25%` plus TAOM `+5%` plus `+10%`, net `-10%` relative to base.

S5: Confirmed. Mordor plain/swamp are `0.05f`; Mordor night is `0.1f`; all other new/reworked terrain speed feats checked are `0.1f`.

S6: Disputed as bug. `aserai_desert_speed` remains and `taom_harad_desert_speed` is appended; docs say TAOM terrain bonuses stack on top of vanilla modifiers. Battania keeps forest speed and gets Khand steppe, a different terrain.

S7: Disputed as bug. TAOM registers feats in a postfix on `Campaign.InitializeDefaultCampaignObjects`, and vanilla loads `SPCultures` afterward via `InitializeBasicObjectXmls()`. Also, `FeatObject` is registered with auto-create behavior, so an unresolved feat id would be silently presumed, not throw. Cross-reference found no terrain-speed feat declared-but-not-registered, registered-but-not-yielded, or applied in the wrong terrain case.

Summary: `CRITICAL: 0 | HIGH: 1 | MEDIUM: 3 | LOW: 1`  
Verdict: `ISSUES FOUND`
