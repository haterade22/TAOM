namespace TAOM.Features.Realms.Domain.Titles;

/// <summary>
/// The fixed identity of a title: what it is and where it sits in the de jure hierarchy.
/// Who holds it, who claims it and which laws apply live in <see cref="Ledger.TitleLedger"/>.
/// </summary>
public sealed class TitleDefinition
{
    public TitleDefinition(
        string id, TitleRank rank, string name, string? parentId = null,
        string? settlementId = null, string? kingdomId = null, string? cultureId = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("A title needs an id.", nameof(id));
        Id = id;
        Rank = rank;
        Name = name ?? string.Empty;
        ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
        SettlementId = string.IsNullOrEmpty(settlementId) ? null : settlementId;
        KingdomId = string.IsNullOrEmpty(kingdomId) ? null : kingdomId;
        CultureId = string.IsNullOrEmpty(cultureId) ? null : cultureId;
    }

    public string Id { get; }
    public TitleRank Rank { get; }

    /// <summary>The domain's name without the rank, for example "Edoras". May carry a {=key} localization prefix.</summary>
    public string Name { get; }

    public string? ParentId { get; }

    /// <summary>The settlement this title is tied to, for Lordship, Barony and County titles.</summary>
    public string? SettlementId { get; }

    /// <summary>The kingdom the title belonged to when it was created; for Kingdom titles the kingdom itself.</summary>
    public string? KingdomId { get; }

    /// <summary>Culture used to pick the rank name, for example "erebor" for "Hold of ...".</summary>
    public string? CultureId { get; }

    public TitleDefinition WithParent(string? parentId) =>
        new(Id, Rank, Name, parentId, SettlementId, KingdomId, CultureId);
}
