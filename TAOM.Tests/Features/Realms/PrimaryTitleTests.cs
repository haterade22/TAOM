using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Succession;

namespace TAOM.Tests.Features.Realms;

/// <summary>The primary title a ruler chooses from those of their highest rank, and the title banners that follow it.</summary>
[TestClass]
public class PrimaryTitleTests
{
    private static RealmsService KingOfBoth(RealmsConfig? config = null)
    {
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config ?? RealmsConfig.Defaults());
        service.Ledger.SetHolder("k_south", "king_n");
        return service;
    }

    [TestMethod]
    public void A_chosen_title_of_the_highest_rank_names_the_holder_but_not_the_laws()
    {
        var service = KingOfBoth();
        Assert.AreEqual("k_north", service.KnownByTitleOf("king_n", null)?.Id, "first by id until a choice is made");

        Assert.IsTrue(service.ChoosePrimary("king_n", "k_south"));

        Assert.AreEqual("k_south", service.ChosenPrimaryOf("king_n")?.Id);
        Assert.AreEqual("k_south", service.KnownByTitleOf("king_n", null)?.Id);
        Assert.AreEqual("k_north", service.PrimaryTitleOf("king_n")?.Id, "laws keep following the highest title");
    }

    [TestMethod]
    public void Only_a_held_title_of_the_highest_rank_can_be_chosen()
    {
        var service = KingOfBoth();

        Assert.IsFalse(service.ChoosePrimary("king_n", "c_town_a"), "a county under a king");
        Assert.IsFalse(service.ChoosePrimary("lord_b", "k_north"), "held by someone else");
        Assert.IsFalse(service.ChoosePrimary("king_n", "k_nowhere"), "no such title");
        Assert.IsNull(service.Ledger.ChosenPrimaryOf("king_n"));
        Assert.IsTrue(service.CanChoosePrimary("king_n", "k_north"));
    }

    [TestMethod]
    public void A_choice_lapses_when_the_title_is_lost_and_a_death_forgets_it()
    {
        var service = KingOfBoth();
        service.ChoosePrimary("king_n", "k_south");

        service.Ledger.SetHolder("k_south", "king_s");

        Assert.IsNull(service.ChosenPrimaryOf("king_n"));
        Assert.AreEqual("k_north", service.KnownByTitleOf("king_n", null)?.Id);

        service.ChoosePrimary("king_n", "k_north");
        service.OnHeroDied("king_n", new FamilySnapshot(new[] { new HeroInfo("king_n", false, false, 60, null) }));
        Assert.IsNull(service.Ledger.ChosenPrimaryOf("king_n"));
    }

    [TestMethod]
    public void A_kingdom_flies_the_banner_of_its_rulers_primary_title_its_own_first_on_a_tie()
    {
        var config = RealmsConfig.Defaults();
        config.TitleBanners["k_north"] = "banner_north";
        config.TitleBanners["k_south"] = "banner_south";
        var service = KingOfBoth(config);

        Assert.AreEqual("banner_north", service.KingdomBannerOf("north", "king_n"));
        Assert.AreEqual("banner_south", service.KingdomBannerOf("south", "king_n"), "the realm's own title wins the tie");

        service.ChoosePrimary("king_n", "k_south");

        Assert.AreEqual("banner_south", service.KingdomBannerOf("north", "king_n"), "the chosen title wins");
        Assert.IsNull(service.KingdomBannerOf("north", null));
    }

    [TestMethod]
    public void A_primary_title_without_a_banner_leaves_the_kingdom_its_own()
    {
        var config = RealmsConfig.Defaults();
        config.TitleBanners["k_south"] = "banner_south";
        var service = KingOfBoth(config);

        Assert.IsNull(service.KingdomBannerOf("north", "king_n"));
        Assert.IsNull(service.KingdomBannerOf("north", "lord_b"), "a ruler with no realm title");
    }

    [TestMethod]
    public void The_choice_survives_a_save()
    {
        var service = KingOfBoth();
        service.ChoosePrimary("king_n", "k_south");

        var read = RealmsSaveFormat.Read(RealmsSaveFormat.Write(service.Catalog, service.Ledger));

        Assert.IsTrue(read.Succeeded, read.Error);
        Assert.AreEqual(0, read.Warnings.Count, string.Join("; ", read.Warnings));
        Assert.AreEqual("k_south", read.Ledger!.ChosenPrimaryOf("king_n"));
    }

    [TestMethod]
    public void Banner_attributes_are_read_from_every_realm_title()
    {
        var result = RealmsConfigXml.Parse(XDocument.Parse(@"<Realms>
  <Kingdoms><Kingdom id='gondor' banner='11.1.1' /><Kingdom id='rohan' /></Kingdoms>
  <HighTitles><HighTitle id='arnor' name='Arnor' banner=' 11.2.2 ' /></HighTitles>
  <Unions><Union id='reunited' name='Reunited' banner='11.3.3'><Member title='k_gondor' /></Union></Unions>
  <TitularRealms><Realm id='khazad_dum' name='Khazad-dum' lands='k_moria' banner='11.4.4' /></TitularRealms>
</Realms>"));

        var banners = result.Config.TitleBanners;
        Assert.AreEqual(4, banners.Count);
        Assert.AreEqual("11.1.1", banners["k_gondor"]);
        Assert.AreEqual("11.2.2", banners["e_arnor"]);
        Assert.AreEqual("11.3.3", banners["u_reunited"]);
        Assert.AreEqual("11.4.4", banners["k_khazad_dum"]);
    }
}
