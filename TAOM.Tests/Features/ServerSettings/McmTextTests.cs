using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>
/// The co-op fingerprint and the server defaults file name settings groups in English, without the keys MCM uses to
/// translate each part of a group path (#1385 Phase 4).
/// </summary>
[TestClass]
public class McmTextTests
{
    [DataTestMethod]
    [DataRow("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_mixed_formations}Mixed Formations", "Battle Tactics/Mixed Formations")]
    [DataRow("{=taom_mcm_group_realm_borders}Realm Borders", "Realm Borders")]
    [DataRow("Realm Borders/Smart Cavalry", "Realm Borders/Smart Cavalry")]
    [DataRow("", "")]
    public void Plain_DropsTheKeyOfEveryPart(string raw, string expected) =>
        Assert.AreEqual(expected, McmText.Plain(raw));

    [TestMethod]
    public void Plain_Null_IsEmpty() => Assert.AreEqual("", McmText.Plain(null));
}
