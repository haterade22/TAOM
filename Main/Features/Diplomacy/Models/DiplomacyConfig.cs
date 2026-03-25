using System.Collections.Generic;

namespace TAOM.Features.Diplomacy.Models;

public class DiplomacyConfig
{
    public List<KingdomRelationship> Relationships { get; set; } = new List<KingdomRelationship>();
}
