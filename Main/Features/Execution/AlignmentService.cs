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

    public FactionSide ResolveSide(string kingdomId, string cultureId)
    {
        var side = GetKingdomSide(kingdomId);
        if (side != FactionSide.Neutral)
            return side;

        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : GetCultureSide(cultureId);
    }

    private FactionSide GetSide(string id)
    {
        if (string.IsNullOrEmpty(id))
            return FactionSide.Neutral;

        return _kingdomSides.TryGetValue(id, out var side) ? side : FactionSide.Neutral;
    }

    public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB)
        => AreEnemyAlignments(GetKingdomSide(kingdomIdA), GetKingdomSide(kingdomIdB));

    public bool AreSameAlignment(string kingdomIdA, string kingdomIdB)
        => AreSameAlignment(GetKingdomSide(kingdomIdA), GetKingdomSide(kingdomIdB));

    // Neutral is nobody's ally and everybody's enemy, including another Neutral. Both predicates
    // are therefore stricter than plain (in)equality, and neither is the negation of the other.
    public bool AreEnemyAlignments(FactionSide sideA, FactionSide sideB)
        => sideA == FactionSide.Neutral || sideB == FactionSide.Neutral || sideA != sideB;

    public bool AreSameAlignment(FactionSide sideA, FactionSide sideB)
        => sideA != FactionSide.Neutral && sideB != FactionSide.Neutral && sideA == sideB;
}
