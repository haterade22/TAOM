using System;
using TAOM.Core.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.Arena;

public class TournamentService : ITournamentService
{
    // Tunable constants moved out of the model body.
    internal const float TournamentStartChance1Lord = 0.45f;
    internal const float TournamentStartChance2Lords = 0.75f;
    internal const float TournamentStartChance3Lords = 0.90f;
    internal const float TournamentEndChanceGraceDays = 20f;
    internal const float TournamentEndChanceRamp = 0.033f;

    // Race name (lower-case) that must never be mounted in tournaments — custom skeleton clips
    // inside the mount. One-line extension point if more custom-skeleton races are added later.
    private const string DwarfRaceName = "dwarf";

    private readonly IRaceManager _raceManager;

    public TournamentService(IRaceManager raceManager)
    {
        _raceManager = raceManager;
    }

    public float CalculateStartChance(int lordCount)
    {
        return lordCount switch
        {
            <= 0 => 0f,
            1 => TournamentStartChance1Lord,
            2 => TournamentStartChance2Lords,
            3 => TournamentStartChance3Lords,
            _ => 1.00f
        };
    }

    public float CalculateEndChance(float elapsedDays)
    {
        return MathF.Max(0f, (elapsedDays - TournamentEndChanceGraceDays) * TournamentEndChanceRamp);
    }

    public MBList<ItemObject> BuildPrizePool(string cultureId, float minTier, float maxTier)
    {
        if (string.IsNullOrEmpty(cultureId))
            return new MBList<ItemObject>();
        var result = new MBList<ItemObject>();
        foreach (var item in Items.All)
        {
            if (item.Culture?.StringId != cultureId) continue;
            if (item.NotMerchandise) continue;
            if (item.Tierf < minTier || item.Tierf >= maxTier) continue;
            if (!item.HasWeaponComponent && !item.HasArmorComponent) continue;
            if (item.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
            result.Add(item);
        }
        return result;
    }

    public string ResolveDummyId(string participantCultureId, string settlementCultureId)
    {
        if (!string.IsNullOrEmpty(participantCultureId))
            return $"gear_practice_dummy_{participantCultureId}";
        if (!string.IsNullOrEmpty(settlementCultureId))
            return $"gear_practice_dummy_{settlementCultureId}";
        return "gear_practice_dummy_empire";
    }

    public bool ShouldDismountInTournament(int raceId)
    {
        // Validate-before-lookup: GetRaceNameFromId returns "human" as a fallback for unknown ids
        // (see .claude/rules/csharp-architecture.md "Validate Before Lookup"). Treat an invalid id
        // as "not dwarf" so we never strip a mount based on a coerced fallback name.
        if (!_raceManager.IsValidRaceId(raceId))
            return false;
        var name = _raceManager.GetRaceNameFromId(raceId);
        return string.Equals(name, DwarfRaceName, StringComparison.OrdinalIgnoreCase);
    }
}
