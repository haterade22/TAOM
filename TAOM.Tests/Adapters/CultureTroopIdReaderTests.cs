using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

[TestClass]
public class CultureTroopIdReaderTests
{
    private static XmlDocument Doc(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }

    [TestMethod]
    public void Read_CultureWithAllTroopAttributes_ReturnsIdsWithoutTheTypePrefix()
    {
        // Arrange: the shape of a merged SPCultures document (taom_spcultures.xml writes NPCCharacter.<id>).
        var doc = Doc(
            "<SPCultures><Culture id=\"erebor\" basic_troop=\"NPCCharacter.erebor_reg_miner\"" +
            " melee_militia_troop=\"NPCCharacter.erebor_militia_spearman\"" +
            " ranged_militia_troop=\"NPCCharacter.erebor_militia_archer\"" +
            " elite_basic_troop=\"NPCCharacter.erebor_noble\"" +
            " ranged_elite_militia_troop=\"NPCCharacter.erebor_militia_veteran_archer\" /></SPCultures>");

        // Act
        var result = CultureTroopIdReader.Read(doc);

        // Assert
        var erebor = result["erebor"];
        Assert.AreEqual("erebor_reg_miner", erebor.BasicTroopId);
        Assert.AreEqual("erebor_militia_spearman", erebor.MeleeMilitiaTroopId);
        Assert.AreEqual("erebor_militia_archer", erebor.RangedMilitiaTroopId);
        Assert.AreEqual("erebor_noble", erebor.EliteBasicTroopId);
        Assert.AreEqual("erebor_militia_veteran_archer", erebor.RangedEliteMilitiaTroopId);
    }

    [TestMethod]
    public void Read_MissingOrEmptyAttribute_IsNull()
    {
        // Arrange
        var doc = Doc("<SPCultures><Culture id=\"nord\" basic_troop=\"NPCCharacter.sturgian_recruit\" melee_militia_troop=\"\" /></SPCultures>");

        // Act
        var nord = CultureTroopIdReader.Read(doc)["nord"];

        // Assert
        Assert.AreEqual("sturgian_recruit", nord.BasicTroopId);
        Assert.IsNull(nord.MeleeMilitiaTroopId);
        Assert.IsNull(nord.RangedMilitiaTroopId);
    }

    [TestMethod]
    public void Read_CultureWithoutId_IsSkipped()
    {
        // Arrange
        var doc = Doc("<SPCultures><Culture basic_troop=\"NPCCharacter.x\" /><Culture id=\"gondor\" /></SPCultures>");

        // Act
        var result = CultureTroopIdReader.Read(doc);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.ContainsKey("gondor"));
    }

    [TestMethod]
    public void Read_MixedCaseId_IsFoundByLowercaseLookup()
    {
        // Arrange: CustomBattleService lowercases culture ids before it looks them up.
        var doc = Doc("<SPCultures><Culture id=\"Gondor\" basic_troop=\"NPCCharacter.gondor_recruit\" /></SPCultures>");

        // Act
        var result = CultureTroopIdReader.Read(doc);

        // Assert
        Assert.AreEqual("gondor_recruit", result["gondor"].BasicTroopId);
    }

    [TestMethod]
    public void Read_NullDocument_ReturnsEmpty()
    {
        // Act
        var result = CultureTroopIdReader.Read(null);

        // Assert
        Assert.AreEqual(0, result.Count);
    }
}
