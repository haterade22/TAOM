using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>split</c> block of a fortress row (Mike, 2026-11-12): the shares of the opening split of the defenders. Absent, the fortress plays as before; a bad block costs one warning
/// and the fortress plays with no split.
/// </summary>
[TestClass]
public class LongDefeatSplitReaderTests
{
    private IModLogger _logger = null!;

    private const string Garrison = "{'troop':'d_inf','class':'Infantry','count':100},{'troop':'d_arc','class':'Ranged','count':50}";
    private const string Mix = "{'troop':'a_inf','class':'Infantry','weight':3},{'troop':'a_arc','class':'Ranged','weight':1}";
    private const string Waves = "{'rules':{},'bounties':{'a_inf':4,'a_arc':6},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[{'count':110,'mix':[" + Mix + "]}]}}}";
    private const string Shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'militia','name':'Militia','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]}}}";
    private const string Affixes = "{'names':{'isengard':{'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'}}}";

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static string Fortress(string split) =>
        "{'fortresses':[{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[" + Garrison + "],'keep':null" + (split.Length == 0 ? "" : ",'split':" + split) + "}]}";

    private LongDefeatConfigCatalog Load(string split) =>
        LongDefeatConfigCatalog.FromTexts(Fortress(split), Waves, Shop, _logger, Affixes, "{'boons':[]}", "{'classes':[]}");

    [TestMethod]
    public void Split_ABlock_IsRead()
    {
        var split = Load("{'wallFoot':0.7,'lowArchers':0.6}").Fortresses.Single().Split;

        Assert.AreEqual(new SplitShares(0.7f, 0.6f), split);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    public void Split_AbsentOrNull_IsNoSplitWithNoWarning(string split)
    {
        Assert.IsNull(Load(split).Fortresses.Single().Split);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("{'wallFoot':0.7}")]
    [DataRow("{'lowArchers':0.7}")]
    [DataRow("{'wallFoot':1,'lowArchers':0.7}")]
    [DataRow("{'wallFoot':0.7,'lowArchers':0}")]
    [DataRow("{'wallFoot':NaN,'lowArchers':0.7}")]
    [DataRow("{'wallFoot':'a lot','lowArchers':0.7}")]
    [DataRow("7")]
    public void Split_ABadBlock_IsNoSplitWithOneWarning_AndTheFortressStays(string split)
    {
        var fortress = Load(split).Fortresses.Single();

        Assert.AreEqual("helms", fortress.Id);
        Assert.IsNull(fortress.Split);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("split")));
    }

    [TestMethod]
    public void Split_ReachesTheRunPlan()
    {
        var catalog = Load("{'wallFoot':0.7,'lowArchers':0.7}");
        var fortress = catalog.Fortresses.Single();

        Assert.AreEqual(fortress.Split, catalog.BuildPlan(fortress, RunOptions.Default).Split);
    }
}
