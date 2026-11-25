using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// Pins how a menu link is routed. Hero, Kingdom and Clan are temporary object-manager types in
/// 1.5.7 (Campaign.OnRegisterTypes) and RemoveTemporaryTypes drops them once the campaign is built,
/// so those three must be told apart from Settlement and looked up through CampaignObjectManager.
/// </summary>
[TestClass]
public class EncyclopediaLinkParseTests
{
    [DataTestMethod]
    [DataRow("Settlement-town_ES1", EncyclopediaLinkKind.Settlement, "town_ES1")]
    [DataRow("Hero-lord_1_17", EncyclopediaLinkKind.Hero, "lord_1_17")]
    [DataRow("Kingdom-empire_s", EncyclopediaLinkKind.Kingdom, "empire_s")]
    [DataRow("Faction-clan_empire_south_1", EncyclopediaLinkKind.Clan, "clan_empire_south_1")]
    [DataRow("Hero-lord_M1-1", EncyclopediaLinkKind.Hero, "lord_M1-1")]
    public void TryParse_KnownElementName_ReturnsKindAndId(string link, EncyclopediaLinkKind kind, string id)
    {
        // Act
        var ok = EncyclopediaCultureLookup.TryParse(link, out var actualKind, out var actualId);

        // Assert
        Assert.IsTrue(ok);
        Assert.AreEqual(kind, actualKind);
        Assert.AreEqual(id, actualId);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Hero")]
    [DataRow("-lord_1")]
    [DataRow("Hero-")]
    [DataRow("Concept-str_taom_concept")]
    [DataRow("NPCCharacter-gondor_recruit")]
    public void TryParse_MalformedOrUnsupportedLink_ReturnsFalse(string? link)
    {
        // Act
        var ok = EncyclopediaCultureLookup.TryParse(link, out _, out _);

        // Assert
        Assert.IsFalse(ok);
    }
}
