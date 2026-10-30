using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The mission result each staged end maps to: a custom battle never gets the campaign's pushed-back result.</summary>
[TestClass]
public class StagedSiegeResultTests
{
    [DataTestMethod]
    [DataRow(StagedEnd.DefenderPushedBack, StagedResultKind.DefenderPushedBack)]
    [DataRow(StagedEnd.AttackerWon, StagedResultKind.Successful)]
    [DataRow(StagedEnd.PlayerDefeated, StagedResultKind.Defeated)]
    public void For_MapsEachEnd(StagedEnd end, StagedResultKind expected) =>
        Assert.AreEqual(expected, StagedResult.For(end));

    [TestMethod]
    public void For_None_IsNull() => Assert.IsNull(StagedResult.For(StagedEnd.None));
}
