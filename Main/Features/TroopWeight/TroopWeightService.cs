using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight;

public class TroopWeightService : ITroopWeightService
{
    private readonly IModLogger _logger;
    private readonly ITroopWeightXmlLoader _xmlLoader;
    private Dictionary<string, float> _weights;

    public TroopWeightService(IModLogger logger, ITroopWeightXmlLoader xmlLoader)
    {
        _logger = logger;
        _xmlLoader = xmlLoader;
        _weights = xmlLoader.GetTroopWeights();
    }

    public float GetTroopWeight(string troopStringId)
    {
        if (string.IsNullOrEmpty(troopStringId))
            return 1.0f;

        return _weights.TryGetValue(troopStringId, out var weight) ? weight : 1.0f;
    }

    public float GetTroopWeight(CharacterObject character)
    {
        return GetTroopWeight(character?.StringId);
    }

    public float CalculateWeightedMemberCount(PartyBase party)
    {
        if (party?.MemberRoster == null)
            return 0f;

        return CalculateWeightedRosterCount(party.MemberRoster);
    }

    public float CalculateWeightedRosterCount(TroopRoster roster)
    {
        if (roster == null)
            return 0f;

        try
        {
            float totalWeight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                totalWeight += CalculateWeightedElementCount(element);
            }
            return totalWeight;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating weighted roster count: {ex.Message}");
            return roster.TotalManCount;
        }
    }

    public float CalculateWeightedElementCount(TroopRosterElement element)
    {
        if (element.Character == null)
            return element.Number;

        var weight = GetTroopWeight(element.Character);
        return element.Number * weight;
    }

    public void ClearCache()
    {
        _weights = _xmlLoader.GetTroopWeights();
    }
}
