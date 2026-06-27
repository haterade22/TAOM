using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.Execution;

public class AlignmentService : IAlignmentService
{
    private readonly Dictionary<string, FactionSide> _kingdomSides;

    public AlignmentService(IAlignmentConfigProvider configProvider, IModLogger logger)
    {
        _kingdomSides = new Dictionary<string, FactionSide>(StringComparer.OrdinalIgnoreCase);

        var alignments = configProvider.LoadAlignments();
        foreach (var kvp in alignments)
        {
            if (Enum.TryParse<FactionSide>(kvp.Value, ignoreCase: true, out var side))
            {
                _kingdomSides[kvp.Key] = side;
            }
            else
            {
                logger.LogWarning($"AlignmentService: Unknown side '{kvp.Value}' for kingdom '{kvp.Key}', defaulting to Neutral");
                _kingdomSides[kvp.Key] = FactionSide.Neutral;
            }
        }

        logger.LogInfo($"AlignmentService: Loaded {_kingdomSides.Count} kingdom alignments");
    }

    public FactionSide GetKingdomSide(string kingdomId) => GetSide(kingdomId);

    public FactionSide GetCultureSide(string cultureId) => GetSide(cultureId);

    private FactionSide GetSide(string id)
    {
        if (string.IsNullOrEmpty(id))
            return FactionSide.Neutral;

        return _kingdomSides.TryGetValue(id, out var side) ? side : FactionSide.Neutral;
    }

    public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB)
    {
        var sideA = GetKingdomSide(kingdomIdA);
        var sideB = GetKingdomSide(kingdomIdB);

        if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
            return true;

        return sideA != sideB;
    }

    public bool AreSameAlignment(string kingdomIdA, string kingdomIdB)
    {
        var sideA = GetKingdomSide(kingdomIdA);
        var sideB = GetKingdomSide(kingdomIdB);

        if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
            return false;

        return sideA == sideB;
    }
}
