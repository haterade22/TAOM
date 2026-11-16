using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #1346: the payload that carries a TAOM fief election to co-op clients. Coop sends the shared
/// decision fields itself; this carries the settlement, the excluded clan and the proposer the
/// decision's constructor needs.
/// </summary>
[TestClass]
public class FiefClaimantWireTests
{
    [TestMethod]
    public void RoundTrip_KeepsEveryField()
    {
        var data = new FiefClaimantWireData("town_a", "clan_proposer", "clan_excluded");

        Assert.IsTrue(FiefClaimantWire.TryDecode(FiefClaimantWire.Encode(data), out var back));

        Assert.AreEqual("town_a", back.SettlementId);
        Assert.AreEqual("clan_proposer", back.ProposerClanId);
        Assert.AreEqual("clan_excluded", back.ClanToExcludeId);
    }

    [TestMethod]
    public void RoundTrip_NoClanToExclude_StaysNull()
    {
        var data = new FiefClaimantWireData("town_a", "clan_proposer", null);

        Assert.IsTrue(FiefClaimantWire.TryDecode(FiefClaimantWire.Encode(data), out var back));

        Assert.IsNull(back.ClanToExcludeId);
    }

    [TestMethod]
    public void TryDecode_RefusesNullEmptyAndGarbage()
    {
        Assert.IsFalse(FiefClaimantWire.TryDecode(null, out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(new byte[0], out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(new byte[] { 1, 200, 3 }, out _));
    }

    [TestMethod]
    public void TryDecode_RefusesAnotherVersion()
    {
        var bytes = FiefClaimantWire.Encode(new FiefClaimantWireData("town_a", "clan_p", null));
        bytes[0] = 99;

        Assert.IsFalse(FiefClaimantWire.TryDecode(bytes, out _));
    }

    [TestMethod]
    public void TryDecode_RefusesAMissingSettlementProposerOrExcludedClan()
    {
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, "", "clan_p", null), out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, "town_a", "", null), out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, "town_a", "clan_p", ""), out _));
    }

    [TestMethod]
    public void TryDecode_AcceptsAnIdAtTheLimit_RefusesOneOverIt()
    {
        var atLimit = new string('x', FiefClaimantWire.MaxIdLength);
        var overLimit = new string('x', FiefClaimantWire.MaxIdLength + 1);

        Assert.IsTrue(FiefClaimantWire.TryDecode(Raw(1, atLimit, "clan_p", atLimit), out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, overLimit, "clan_p", null), out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, "town_a", overLimit, null), out _));
        Assert.IsFalse(FiefClaimantWire.TryDecode(Raw(1, "town_a", "clan_p", overLimit), out _));
    }

    [TestMethod]
    public void TryDecode_RefusesTrailingBytes()
    {
        var bytes = FiefClaimantWire.Encode(new FiefClaimantWireData("town_a", "clan_p", null));
        var longer = new byte[bytes.Length + 1];
        bytes.CopyTo(longer, 0);

        Assert.IsFalse(FiefClaimantWire.TryDecode(longer, out _));
    }

    private static byte[] Raw(byte version, string settlement, string proposer, string excluded)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(version);
            writer.Write(settlement);
            writer.Write(proposer);
            writer.Write(excluded != null);
            if (excluded != null) writer.Write(excluded);
        }

        return stream.ToArray();
    }
}
