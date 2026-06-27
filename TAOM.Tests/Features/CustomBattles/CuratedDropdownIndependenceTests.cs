using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Pins the load-bearing assumption that the per-faction commander dropdown
/// (<see cref="SideCommanderFilter.ResolveCommandersForCulture"/>) is rebuilt purely from
/// <c>GetCommanderIdsForFaction</c> + <c>GetBasicCharacter</c> — independent of the master
/// <c>GetCommanderIds()</c> list. Curated ids (incl. 3-segment ids the master list filters out)
/// therefore surface in the dropdown without being in the master list. If a future engine change
/// makes the dropdown cross-check the master list, this test breaks and tells us why.
/// </summary>
[TestClass]
public class CuratedDropdownIndependenceTests
{
    [TestMethod]
    public void ResolveCommandersForCulture_CuratedIdNotInMasterList_StillResolves()
    {
        // Arrange
        var service = Substitute.For<ICustomBattleService>();
        var objectManager = Substitute.For<IObjectManagerAdapter>();
        var logger = Substitute.For<IModLogger>();

        // Master list deliberately does NOT contain the curated 3-segment id.
        service.GetCommanderIds().Returns(new List<string> { "lord_1_15" });
        service.GetCommanderIdsForFaction("mordor", SideCommanderFilter.MaxCommandersPerCulture)
            .Returns(new List<string> { "lord_1_48_1" }); // 3-segment, absent from master list

        var character = Substitute.For<BasicCharacterObject>();
        objectManager.GetBasicCharacter("lord_1_48_1").Returns(character);

        var sut = new SideCommanderFilter(service, objectManager, logger);

        // Act
        var result = sut.ResolveCommandersForCulture("mordor");

        // Assert — surfaces in the dropdown despite being absent from GetCommanderIds()
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, character);
    }
}
