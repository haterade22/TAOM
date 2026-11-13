using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class PersistenceTests
{
    [TestMethod]
    public void Round_trips_titles_holders_claims_laws_and_pending()
    {
        var service = new RealmsService();
        var config = RealmsConfig.Defaults();
        config.KingdomLaws["south"] = new TitleLaws(SuccessionLaw.Seniority, GenderLaw.Absolute);
        config.DefaultLaws = new TitleLaws(SuccessionLaw.Ultimogeniture, GenderLaw.Agnatic);
        service.Initialize(TestWorld.Build(), config);
        service.OnFiefOwnerChanged("town_b", "raider", "lord_b", TransferKind.Conquest, 12.5);
        service.Ledger.AddPending(new PendingSuccession("c_town_c", PendingKind.ClanLeader, "clan_c", null));

        var read = RealmsSaveFormat.Read(RealmsSaveFormat.Write(service.Catalog, service.Ledger));

        Assert.IsTrue(read.Succeeded, read.Error);
        Assert.AreEqual(0, read.Warnings.Count, string.Join("; ", read.Warnings));
        Assert.AreEqual(service.Catalog.Count, read.Catalog!.Count);
        Assert.AreEqual("c_town_b", read.Catalog.Get("b_castle_a")!.ParentId);
        Assert.AreEqual("lord_b", read.Ledger!.HolderOf("c_town_b"));
        var claim = read.Ledger.ClaimsOn("c_town_b").Single();
        Assert.AreEqual("raider", claim.HeroId);
        Assert.AreEqual(12.5, claim.CampaignDay);
        Assert.AreEqual(SuccessionLaw.Seniority, read.Ledger.LawsOn("k_south")!.Succession);
        Assert.AreEqual(config.DefaultLaws, read.Ledger.DefaultLaws);
        Assert.IsNull(read.Ledger.Pending.Single().PreviousHolderId);
    }

    [TestMethod]
    public void Names_with_tabs_newlines_and_backslashes_survive()
    {
        const string name = "{=k}Tûr\tMorva\nof the \\Dunlendings\\0";
        var catalog = new TitleCatalog(new[] { new TitleDefinition("c", TitleRank.County, name, null, "s") });

        var read = RealmsSaveFormat.Read(RealmsSaveFormat.Write(catalog, new TitleLedger()));

        Assert.AreEqual(name, read.Catalog!.Get("c")!.Name);
        Assert.IsNull(read.Catalog.Get("c")!.ParentId);
    }

    [TestMethod]
    public void Refuses_a_newer_version_or_foreign_payload()
    {
        Assert.IsFalse(RealmsSaveFormat.Read("TAOM.Realms\t999").Succeeded);
        Assert.IsFalse(RealmsSaveFormat.Read("something else").Succeeded);
        Assert.IsFalse(RealmsSaveFormat.Read(string.Empty).Succeeded);
    }

    [TestMethod]
    public void Skips_unknown_records_with_a_warning()
    {
        var read = RealmsSaveFormat.Read("TAOM.Realms\t1\nX\tfuture\nT\tc\tCounty\tName\t\\0\t\\0\t\\0\t\\0");

        Assert.IsTrue(read.Succeeded);
        Assert.AreEqual(1, read.Warnings.Count);
        Assert.AreEqual(1, read.Catalog!.Count);
    }

    [TestMethod]
    public void Chunks_join_back_exactly_and_never_split_a_surrogate_pair()
    {
        var payload = new string('a', 9) + "\U0001F451" + new string('b', 15);

        var chunks = SaveChunks.Split(payload, 10);

        Assert.IsTrue(chunks.All(c => c.Length <= 10));
        Assert.IsFalse(chunks.Any(c => char.IsHighSurrogate(c[c.Length - 1])));
        Assert.AreEqual(payload, SaveChunks.Join(chunks));
    }
}
