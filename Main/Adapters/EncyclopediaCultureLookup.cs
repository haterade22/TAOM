using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// Resolves an encyclopedia link to the culture of the object it points at.
///
/// The link is <c>"&lt;ElementName&gt;-&lt;StringId&gt;"</c>, split on the FIRST dash. The element
/// name is the object-manager registration name, which is not always the type name: <c>Clan</c> is
/// registered as <c>"Faction"</c>, so clan links read <c>"Faction-clan_xyz"</c>.
///
/// Settlements resolve through <c>MBObjectManager</c>. Heroes, kingdoms and clans do not: 1.5.7
/// registers them as temporary types (<c>Campaign.OnRegisterTypes</c>, <c>isTemporary: true</c>) and
/// <c>MBObjectManager.RemoveTemporaryTypes</c> drops their records once a new campaign is built, so
/// <c>GetObject("Hero", id)</c> fails its assert and returns null. They resolve through
/// <c>CampaignObjectManager.Find&lt;T&gt;</c>, as vanilla's hero and faction encyclopedia pages do.
/// </summary>
public sealed class EncyclopediaCultureLookup : IEncyclopediaCultureLookup
{
    public string? GetCultureId(string? encyclopediaLink)
    {
        if (!TryParse(encyclopediaLink, out var kind, out var stringId)) return null;

        try
        {
            // Culture is a computed property on some of these types, so use ?. throughout, per
            // .claude/rules/adapters.md, since a TaleWorlds getter can throw before a null check.
            var campaignObjects = Campaign.Current?.CampaignObjectManager;
            switch (kind)
            {
                case EncyclopediaLinkKind.Settlement:
                    return (MBObjectManager.Instance?.GetObject("Settlement", stringId) as Settlement)?.Culture?.StringId;
                case EncyclopediaLinkKind.Hero:
                    return campaignObjects?.Find<Hero>(stringId)?.Culture?.StringId;
                case EncyclopediaLinkKind.Kingdom:
                    return campaignObjects?.Find<Kingdom>(stringId)?.Culture?.StringId;
                case EncyclopediaLinkKind.Clan:
                    return campaignObjects?.Find<Clan>(stringId)?.Culture?.StringId;
                default:
                    return null;
            }
        }
        catch
        {
            // Object managers not ready in this session.
            return null;
        }
    }

    /// <summary>
    /// Splits a link on its first dash and maps the element name to a link kind. False for a
    /// malformed link or an element name that carries no culture colour.
    /// </summary>
    internal static bool TryParse(string? encyclopediaLink, out EncyclopediaLinkKind kind, out string stringId)
    {
        kind = default;
        stringId = string.Empty;
        if (string.IsNullOrEmpty(encyclopediaLink)) return false;

        var dash = encyclopediaLink!.IndexOf('-');
        if (dash <= 0 || dash == encyclopediaLink.Length - 1) return false;

        switch (encyclopediaLink.Substring(0, dash))
        {
            case "Settlement": kind = EncyclopediaLinkKind.Settlement; break;
            case "Hero": kind = EncyclopediaLinkKind.Hero; break;
            case "Kingdom": kind = EncyclopediaLinkKind.Kingdom; break;
            case "Faction": kind = EncyclopediaLinkKind.Clan; break;
            default: return false;
        }

        stringId = encyclopediaLink.Substring(dash + 1);
        return true;
    }
}
