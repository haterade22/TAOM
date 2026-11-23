using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Localization;
using TAOM.Features.Realms;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// Which name a kingdom keeps. A name the player gives in the kingdom screen beats every title name, now and after a
/// load (#1316). When a kingdom loses its naming title, Realms gives it its own name back only while it still has the
/// name Realms set (#1297), and never a saved own name that has lost its text.
/// </summary>
[TestClass]
public class KingdomNamesRestoreTests
{
    [TestMethod]
    public void StillNamedByRealms_TheRealmsName_IsRestored() =>
        Assert.IsTrue(KingdomNames.StillNamedByRealms("Durin's Folk", "Durin's Folk"));

    [TestMethod]
    public void StillNamedByRealms_AnotherSourceRenamedIt_IsKept() =>
        Assert.IsFalse(KingdomNames.StillNamedByRealms("The Iron Hills Pact", "Durin's Folk"));

    [TestMethod]
    public void StillNamedByRealms_NoNameSetThisSession_IsRestored() =>
        Assert.IsTrue(KingdomNames.StillNamedByRealms("Durin's Folk", null));

    // The kingdom screen's rename (KingdomManagementVM.OnChangeKingdomNameDone) sets str_generic_kingdom_name with the
    // typed name as its variable; the save keeps both, so the player's name is known after a load too.
    [TestMethod]
    public void RenamedByPlayer_TheKingdomScreenTextWithItsName_IsThePlayers() =>
        Assert.IsTrue(KingdomNames.RenamedByPlayer("{=generickingdomname}{KINGDOM_NAME}", hasVariables: true));

    [TestMethod]
    public void RenamedByPlayer_AnXmlOrATitleName_IsNot()
    {
        Assert.IsFalse(KingdomNames.RenamedByPlayer("{=taom_kingdom_erebor}Erebor", hasVariables: false));
        Assert.IsFalse(KingdomNames.RenamedByPlayer("Durin's Folk", hasVariables: false));
        Assert.IsFalse(KingdomNames.RenamedByPlayer(null, hasVariables: false));
    }

    // Builds before #1316 could save the kingdom screen's text as an own name without its variable, then restore it.
    // That text shows an error, so it is no player's name, and a title may replace it.
    [TestMethod]
    public void RenamedByPlayer_TheKingdomScreenTextWithoutItsName_IsNot() =>
        Assert.IsFalse(KingdomNames.RenamedByPlayer("{=generickingdomname}{KINGDOM_NAME}", hasVariables: false));

    [TestMethod]
    public void Restorable_AnOwnNameSavedWithoutItsVariable_IsNot() =>
        Assert.IsFalse(KingdomNames.Restorable("{=generickingdomname}{KINGDOM_NAME}"));

    [TestMethod]
    public void Restorable_AnXmlOrAResolvedName_Is()
    {
        Assert.IsTrue(KingdomNames.Restorable("{=taom_kingdom_erebor}Erebor"));
        Assert.IsTrue(KingdomNames.Restorable("The Iron Hills Pact"));
    }

    // A name with a variable, such as a rebel kingdom named after its clan, is kept as the text the player reads; the
    // bare template would come back without its variable. A plain name keeps its {=key}, so it follows a language change.
    // #1391: the world snapshot reads kingdom names through this overload, so a kingdom title keeps the {=key} of the
    // kingdom's name.
    [TestCategory("RequiresGame")]   // constructs TextObject
    [TestMethod]
    public void OwnText_ATextObjectWithAKey_KeepsTheKey()
    {
        Assert.AreEqual("{=taom_town_westoft}Westoft",
            KingdomNames.OwnText(new TextObject("{=taom_town_westoft}Westoft"), "westoft"));
        Assert.AreEqual("westoft", KingdomNames.OwnText(null, "westoft"));
    }

    [TestCategory("RequiresGame")]   // constructs TextObject
    [TestMethod]
    public void OwnText_ATextObjectWithAVariable_KeepsItsResolvedText()
    {
        var name = new TextObject("{=generickingdomname}{KINGDOM_NAME}");
        name.SetTextVariable("KINGDOM_NAME", "Iron Hills Pact");

        Assert.AreEqual("Iron Hills Pact", KingdomNames.OwnText(name, "pact"));
    }

    // #1391: the engine sets IS_SETTLEMENT, IS_CASTLE, IS_TOWN and IS_HIDEOUT on every settlement name
    // (Settlement.Deserialize, SetSettlementProperties), so the kingdom rule would resolve it. No settlement name's
    // text uses them, so a settlement title keeps the raw {=key} text.
    [TestCategory("RequiresGame")]   // constructs TextObject
    [TestMethod]
    public void SettlementNameText_AFlaggedSettlementName_KeepsTheKey()
    {
        var name = new TextObject("{=taom_town_westoft}Westoft");
        name.SetTextVariable("IS_SETTLEMENT", 1);
        name.SetTextVariable("IS_TOWN", 1);

        Assert.AreEqual("{=taom_town_westoft}Westoft", WorldSnapshotReader.SettlementNameText(name, "westoft"));
        Assert.AreEqual("westoft", WorldSnapshotReader.SettlementNameText(null, "westoft"));
    }

    [TestMethod]
    public void OwnText_ANameWithVariables_KeepsItsResolvedText() =>
        Assert.AreEqual("Dunland Rebels",
            KingdomNames.OwnText("{=rebel_kingdom}{CLAN_NAME} Rebels", hasVariables: true, () => "Dunland Rebels", "rebels_1"));

    [TestMethod]
    public void OwnText_APlainName_KeepsItsRawText() =>
        Assert.AreEqual("{=taom_kingdom_erebor}Erebor",
            KingdomNames.OwnText("{=taom_kingdom_erebor}Erebor", hasVariables: false, () => "Erebor", "erebor"));

    [TestMethod]
    public void OwnText_NoName_IsTheFallback() =>
        Assert.AreEqual("erebor", KingdomNames.OwnText(null, hasVariables: false, () => "x", "erebor"));
}
