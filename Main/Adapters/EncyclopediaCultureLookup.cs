using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// Resolves an encyclopedia link to the culture of the object it points at.
///
/// Mirrors vanilla's own round-trip in <c>EncyclopediaManager.GoToLink</c>: split on the FIRST
/// dash, then <c>MBObjectManager.GetObject(elementName, stringId)</c>. The element name is the
/// object-manager registration name, which is not always the type name — <c>Clan</c> is registered
/// as <c>"Faction"</c> (<c>Campaign.RegisterType&lt;Clan&gt;("Faction", ...)</c>), so clan links read
/// <c>"Faction-clan_xyz"</c>.
/// </summary>
public sealed class EncyclopediaCultureLookup : IEncyclopediaCultureLookup
{
    public string? GetCultureId(string? encyclopediaLink)
    {
        if (string.IsNullOrEmpty(encyclopediaLink)) return null;

        var dash = encyclopediaLink!.IndexOf('-');
        if (dash <= 0 || dash == encyclopediaLink.Length - 1) return null;

        var elementName = encyclopediaLink.Substring(0, dash);
        var stringId = encyclopediaLink.Substring(dash + 1);

        try
        {
            // GetObject logs a failed assert and returns null for an unknown element name or id.
            var obj = MBObjectManager.Instance?.GetObject(elementName, stringId);

            // Culture is a computed property on some of these types — use ?. throughout, per
            // .claude/rules/adapters.md, since a TaleWorlds getter can throw before a null check.
            switch (obj)
            {
                case Settlement settlement: return settlement.Culture?.StringId;
                case Hero hero: return hero.Culture?.StringId;
                case Kingdom kingdom: return kingdom.Culture?.StringId;
                case Clan clan: return clan.Culture?.StringId;
                default: return null;
            }
        }
        catch
        {
            // Object manager not ready, or the element name is not registered in this session.
            return null;
        }
    }
}
