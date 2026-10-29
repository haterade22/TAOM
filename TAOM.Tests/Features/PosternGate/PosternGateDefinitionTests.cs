using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.PosternGate;
using TAOM.Features.TaomBuildings;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.PosternGate;

[TestClass]
public class PosternGateDefinitionTests
{
    private static TaomBuildingDefinition Definition(bool cutEnabled = true)
    {
        var settings = Substitute.For<IPosternGateSettingsProvider>();
        settings.CutEnabled.Returns(cutEnabled);
        return PosternGateDefinition.Create(new[] { 400, 700, 1000 }, new[] { 0.50f, 0.67f, 0.75f }, settings);
    }

    [TestMethod]
    public void EveryCultureId_ExistsInCultureData()
    {
        var known = CultureDataFixture.MainCultureIds();
        var unknown = Definition().Variants.SelectMany(v => v.CultureIds).Where(id => !known.Contains(id)).ToList();

        Assert.AreEqual(0, unknown.Count, "unknown culture ids: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void NoCultureInTwoVariants()
    {
        var ids = Definition().Variants.SelectMany(v => v.CultureIds).ToList();

        Assert.AreEqual(ids.Count, ids.Distinct().Count());
    }

    [TestMethod]
    public void UnlistedCulture_GetsPosternGate()
    {
        var catalog = new TaomBuildingCatalog(new[] { Definition() });

        Assert.AreEqual(PosternGateDefinition.DefaultVariantId, catalog.VariantFor(PosternGateDefinition.FamilyId, "aserai"));
        Assert.AreEqual(PosternGateDefinition.DefaultVariantId, catalog.VariantFor(PosternGateDefinition.FamilyId, null));
    }

    [TestMethod]
    public void EffectLine_SwitchOn_ShowsPercent()
    {
        var definition = Definition(cutEnabled: true);

        Assert.AreEqual("50", definition.LevelText(1)!.Value);
        Assert.AreEqual("67", definition.LevelText(2)!.Value);
        Assert.AreEqual("75", definition.LevelText(3)!.Value);
        Assert.IsNull(definition.LevelText(0));
        Assert.IsNull(definition.LevelText(4));
    }

    [TestMethod]
    public void EffectLine_SwitchOff_IsEmpty()
    {
        var definition = Definition(cutEnabled: false);

        Assert.IsNull(definition.LevelText(1));
        Assert.IsNull(definition.LevelText(3));
    }

    [TestMethod]
    public void Variants_AreSixDistinctLowercaseIds_AndCostsAreCopied()
    {
        var definition = Definition();

        Assert.AreEqual(6, definition.Variants.Count);
        Assert.IsTrue(definition.Variants.All(v => v.Id == v.Id.ToLowerInvariant()));
        CollectionAssert.AreEqual(new[] { 400, 700, 1000 }, definition.Costs.ToArray());
        Assert.IsTrue(definition.IsMilitary);
    }

    [TestMethod]
    public void Variants_AreTheSixSavedIds()
    {
        // Saved in player saves: add new ids, never rename or remove (Town.AfterLoad drops the building and clears the
        // town's build queue).
        CollectionAssert.AreEqual(
            new[]
            {
                "building_taom_postern_gate", "building_taom_postern_gate_dwarf", "building_taom_postern_gate_rohan",
                "building_taom_postern_gate_gondor", "building_taom_postern_gate_elf", "building_taom_postern_gate_orc",
            },
            Definition().Variants.Select(v => v.Id).ToArray());
    }

    [TestMethod]
    public void Culvert_ServesRohanAndItsCutSouthRhovanion()
    {
        // South Rhovanion is cut from Rohan (#1062): its own culture id must reach Rohan's variant.
        CollectionAssert.AreEqual(
            new[] { "vlandia", "southrhovanion" },
            Definition().Variants.Single(v => v.Id.EndsWith("_rohan")).CultureIds.ToArray());
    }

    [TestMethod]
    public void Text_UsesThePosternKeyPrefix_AndTheSiegeSentence()
    {
        // taom_bld_ belongs to Battle Load Diagnostics.
        var definition = Definition();
        var texts = definition.Variants.SelectMany(v => new[] { v.Name, v.Explanation })
            .Concat(new[] { definition.LevelText(1)!.Template }).ToList();

        Assert.IsTrue(texts.All(t => Regex.IsMatch(t, @"^\{=taom_postern_\w+\}")), "every key starts taom_postern_");
        Assert.IsTrue(definition.Variants.All(v =>
            v.Explanation.EndsWith("In a siege, parties slip into or out of this fief with fewer losses.")));
        StringAssert.Contains(definition.Variants.Single(v => v.Id.EndsWith("_elf")).Explanation,
            "A path through roots and rock that only the Elves can follow.");
    }
}
