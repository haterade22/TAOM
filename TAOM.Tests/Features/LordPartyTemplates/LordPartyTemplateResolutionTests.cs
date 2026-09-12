using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LordPartyTemplates;

namespace TAOM.Tests.Features.LordPartyTemplates;

/// <summary>
/// The pure decision behind the Patch88 postfix on <c>Clan.DefaultPartyTemplate</c>. The postfix
/// itself reads a thread-static ambient owner and cannot be unit tested; every branch that decides
/// WHETHER a read is overridden lives here so it can be.
/// </summary>
[TestClass]
public class LordPartyTemplateResolutionTests
{
    private sealed class FakeService : ILordPartyTemplateService
    {
        private readonly string? _faramirTemplate;

        public FakeService(string? faramirTemplate = "kingdom_hero_party_gondor_faramir_template") =>
            _faramirTemplate = faramirTemplate;

        public bool TryGetTemplateId(string heroId, out string? templateId)
        {
            templateId = heroId == "lord_1_34" ? _faramirTemplate : null;
            return templateId != null;
        }
    }

    private static readonly ILordPartyTemplateService Service = new FakeService();

    [TestMethod]
    public void Resolve_NoAmbientOwner_ReturnsNull()
    {
        // Every clan template read outside a lord spawn (naval capability, clan variables, bandit
        // spawns) arrives here with no ambient owner and must stay vanilla.
        Assert.IsNull(LordPartyTemplateResolution.Resolve(null, null, "clan_empire_west_1", Service));
    }

    [TestMethod]
    public void Resolve_ReadIsForAnotherClan_ReturnsNull()
    {
        // Inside the spawn scope of Faramir, a read of some OTHER clan's template is not his.
        Assert.IsNull(LordPartyTemplateResolution.Resolve("lord_1_34", "clan_empire_west_1", "clan_empire_west_2", Service));
    }

    [TestMethod]
    public void Resolve_OwnerHasNoClan_ReturnsNull()
    {
        Assert.IsNull(LordPartyTemplateResolution.Resolve("lord_1_34", null, "clan_empire_west_1", Service));
    }

    [TestMethod]
    public void Resolve_ReadClanUnknown_ReturnsNull()
    {
        Assert.IsNull(LordPartyTemplateResolution.Resolve("lord_1_34", "clan_empire_west_1", null, Service));
    }

    [TestMethod]
    public void Resolve_OwnerNotMapped_ReturnsNull()
    {
        // Denethor shares the clan with Faramir. His own spawn scope must resolve to nothing so
        // the clan binding stands for him.
        Assert.IsNull(LordPartyTemplateResolution.Resolve("lord_1_7", "clan_empire_west_1", "clan_empire_west_1", Service));
    }

    [TestMethod]
    public void Resolve_MappedOwnerReadingOwnClan_ReturnsTemplate()
    {
        Assert.AreEqual(
            "kingdom_hero_party_gondor_faramir_template",
            LordPartyTemplateResolution.Resolve("lord_1_34", "clan_empire_west_1", "clan_empire_west_1", Service));
    }

    [TestMethod]
    public void Resolve_ServiceReturnsBlank_ReturnsNull()
    {
        // A blank id would reach MBObjectManager and resolve to nothing; refuse it here so the
        // postfix never even tries.
        Assert.IsNull(LordPartyTemplateResolution.Resolve("lord_1_34", "clan_empire_west_1", "clan_empire_west_1", new FakeService("  ")));
    }
}
