namespace TAOM.Features.Realms;

/// <summary>
/// The "World/Realms" MCM group, clamped. Read when a price is quoted, on the weekly and daily ticks and when a lord's
/// party or conversation name is built, never per frame. Prices and payments are percentages of realms_titles.xml.
/// </summary>
public interface IRealmsSettingsProvider
{
    /// <summary>Claim prices, 10 to 500 percent of the file's.</summary>
    int ClaimPricePercent { get; }

    /// <summary>Influence to usurp, form, restore and revoke, 10 to 500 percent of the file's.</summary>
    int InfluenceCostPercent { get; }

    /// <summary>AI lords buy and press claims, form vacant titles and restore thrones on the weekly tick.</summary>
    bool AiClaims { get; }

    /// <summary>Weekly tribute, 0 to 300 percent of the file's.</summary>
    int TributePercent { get; }

    /// <summary>Weekly influence of offices, 0 to 300 percent of the file's.</summary>
    int OfficeInfluencePercent { get; }

    /// <summary>The weekly relation and loyalty loss over a town or castle held without its title.</summary>
    bool Resentment { get; }

    /// <summary>A vassal's weekly relation gain with their liege.</summary>
    bool LiegePull { get; }

    /// <summary>A liege calls vassals to an army for less influence.</summary>
    bool LiegeArmyDiscount { get; }

    /// <summary>AI lords hand lesser titles to their grown children.</summary>
    bool FamilyGrants { get; }

    /// <summary>Lord party and conversation names carry the lord's style and title.</summary>
    bool TitleStyles { get; }
}
