using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LordPartyTemplates;
using TAOM.Features.LordPartyTemplates.Domain;

namespace TAOM.Tests.Features.LordPartyTemplates;

[TestClass]
public class LordPartyTemplateServiceTests
{
    private LordPartyTemplateService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var provider = Substitute.For<ILordPartyTemplateConfigProvider>();
        provider.GetConfig().Returns(new LordPartyTemplateConfig
        {
            Overrides = new Dictionary<string, string>
            {
                ["lord_1_34"] = "kingdom_hero_party_gondor_faramir_template",
            },
        });
        _sut = new LordPartyTemplateService(provider);
    }

    [TestMethod]
    public void TryGetTemplateId_MappedHero_ReturnsTemplate()
    {
        var found = _sut.TryGetTemplateId("lord_1_34", out var templateId);

        Assert.IsTrue(found);
        Assert.AreEqual("kingdom_hero_party_gondor_faramir_template", templateId);
    }

    [TestMethod]
    public void TryGetTemplateId_UnmappedHero_ReturnsFalse()
    {
        var found = _sut.TryGetTemplateId("lord_1_7", out var templateId);

        Assert.IsFalse(found);
        Assert.IsNull(templateId);
    }

    [TestMethod]
    public void TryGetTemplateId_NullHero_ReturnsFalse()
    {
        Assert.IsFalse(_sut.TryGetTemplateId(null!, out _));
    }

    [TestMethod]
    public void TryGetTemplateId_EmptyHero_ReturnsFalse()
    {
        Assert.IsFalse(_sut.TryGetTemplateId(string.Empty, out _));
    }

    [TestMethod]
    public void TryGetTemplateId_IsCaseSensitive()
    {
        // StringIds are exact in the engine; a case-insensitive match here would claim a hero
        // the engine would never resolve.
        Assert.IsFalse(_sut.TryGetTemplateId("LORD_1_34", out _));
    }
}
