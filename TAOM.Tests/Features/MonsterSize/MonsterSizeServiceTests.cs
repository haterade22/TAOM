using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MonsterSize;

namespace TAOM.Tests.Features.MonsterSize;

/// <summary>
/// A mount's size lives on its Monster (Mike, 2026-09-23, #646): <c>taom_body_length</c> on the Monster is copied
/// into every Horse item that names it, because the engine sizes a mount only from the ridden item's body_length
/// (<c>Mission.BuildAgent</c>). The value is user-edited XML, so every non-whole or out-of-range value is refused
/// with a warning and its mounts keep their items' own body_length.
/// </summary>
[TestClass]
public class MonsterSizeServiceTests
{
    private IMonsterSizeCatalogAdapter _catalog = null!;
    private IModLogger _logger = null!;
    private MonsterSizeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _catalog = Substitute.For<IMonsterSizeCatalogAdapter>();
        _logger = Substitute.For<IModLogger>();
        _catalog.SetBodyLength(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _sut = new MonsterSizeService(_catalog, _logger);
    }

    private void GivenSizes(params (string Monster, string Raw)[] sizes)
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var (monster, raw) in sizes)
            list.Add(new KeyValuePair<string, string>(monster, raw));
        _catalog.ReadDeclaredSizes().Returns(list);
    }

    private void GivenItems(params HorseItemRecord[] items) => _catalog.ReadHorseItems().Returns(items);

    [TestMethod]
    public void ApplyMonsterSizes_SizedMonster_WritesItsSizeIntoEachOfItsHorseItems()
    {
        GivenSizes(("taom_animalia_moose", "150"));
        GivenItems(new HorseItemRecord("taom_animalia_moose_a", "taom_animalia_moose", 0),
                   new HorseItemRecord("taom_animalia_moose_b", "taom_animalia_moose", 0));

        int written = _sut.ApplyMonsterSizes();

        Assert.AreEqual(2, written);
        _catalog.Received(1).SetBodyLength("taom_animalia_moose_a", 150);
        _catalog.Received(1).SetBodyLength("taom_animalia_moose_b", 150);
    }

    [TestMethod]
    public void ApplyMonsterSizes_ItemAlreadyAtTheSize_IsNotWritten()
    {
        GivenSizes(("taom_elk", "110"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 110));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyMonsterSizes_ItemOfAnUnsizedMonster_IsNotWritten()
    {
        GivenSizes(("taom_elk", "110"));
        GivenItems(new HorseItemRecord("charger", "horse", 0), new HorseItemRecord("taom_war_ram_a", "taom_war_ram", 100));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyMonsterSizes_ItemWithNoMonster_IsNotWritten()
    {
        GivenSizes(("taom_elk", "110"));
        GivenItems(new HorseItemRecord("odd_horse", null, 0));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyMonsterSizes_MonsterIdOfADifferentCase_DoesNotMatch()
    {
        // The engine resolves object ids case-sensitively, so "Taom_Elk" is another Monster, and the typo is warned.
        GivenSizes(("Taom_Elk", "110"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 0));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Taom_Elk") && m.Contains("no Horse item")));
    }

    [TestMethod]
    public void ApplyMonsterSizes_SizedMonsterNamedByNoHorseItem_IsWarned()
    {
        // A size nothing rides is dead config (a typo'd id, a renamed Monster): the summary cannot show it, because an
        // item already at its size is not listed, so it gets its own warning.
        GivenSizes(("taom_elk", "110"), ("taom_ghost", "150"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 110));

        _sut.ApplyMonsterSizes();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("taom_ghost") && m.Contains("no Horse item")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("taom_elk")));
    }

    [TestMethod]
    public void ApplyMonsterSizes_ItemHoldingThePlaceholder_MonsterWins_SummaryShowsTheOldValue()
    {
        // Items.xsd requires body_length on <Horse>, so a sized Monster's items keep the placeholder; overriding it is
        // the normal path, not a warning.
        GivenSizes(("taom_animalia_moose", "150"));
        GivenItems(new HorseItemRecord("taom_animalia_moose_a", "taom_animalia_moose", MonsterSizeConfig.ItemPlaceholderBodyLength));

        Assert.AreEqual(1, _sut.ApplyMonsterSizes());
        _catalog.Received(1).SetBodyLength("taom_animalia_moose_a", 150);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("taom_animalia_moose_a=150 (was 100)")));
    }

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("1.5")]
    [DataRow("150.0")]
    [DataRow("1e3")]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("-5")]
    [DataRow("0")]
    [DataRow("9")]
    [DataRow("1001")]
    [DataRow("99999999999")]
    public void ApplyMonsterSizes_ValueNotAWholeNumberInRange_IsSkippedAndWarned(string raw)
    {
        GivenSizes(("taom_elk", raw));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 0));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("taom_elk") && m.Contains(MonsterSizeConfig.AttributeName)));
    }

    [DataTestMethod]
    [DataRow("10", 10)]
    [DataRow("1000", 1000)]
    [DataRow(" 150 ", 150)]
    public void ApplyMonsterSizes_WholeNumberInRange_IsAccepted(string raw, int expected)
    {
        GivenSizes(("taom_elk", raw));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 0));

        Assert.AreEqual(1, _sut.ApplyMonsterSizes());
        _catalog.Received(1).SetBodyLength("taom_elk_a", expected);
    }

    [TestMethod]
    public void ApplyMonsterSizes_NoMonsterDeclaresASize_ReadsNoItems()
    {
        GivenSizes();

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().ReadHorseItems();
    }

    [TestMethod]
    public void ApplyMonsterSizes_SetterFails_LogsAnErrorAndCountsNothing()
    {
        GivenSizes(("taom_elk", "110"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 0));
        _catalog.SetBodyLength("taom_elk_a", 110).Returns(false);

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("taom_elk_a")));
    }

    [TestMethod]
    public void ApplyMonsterSizes_CatalogThrows_LogsAnErrorAndDoesNotThrow()
    {
        _catalog.ReadDeclaredSizes().Throws(new InvalidOperationException("merged XML unavailable"));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("merged XML unavailable")));
    }

    [TestMethod]
    public void ApplyMonsterSizes_ValueRefused_LogsASummaryWarning()
    {
        // csharp-architecture.md "Config Providers MUST Validate" item 6: a reversion ends in a summary warning, even
        // when every declared size was refused and nothing else is logged.
        GivenSizes(("taom_elk", "abc"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 100));

        _sut.ApplyMonsterSizes();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("1 ") && m.Contains("value(s) refused")));
    }

    [TestMethod]
    public void ApplyMonsterSizes_EmptyMonsterId_IsSkippedWithoutAWarning()
    {
        GivenSizes(("", "150"));
        GivenItems(new HorseItemRecord("odd_horse", "", 100));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyMonsterSizes_NullValue_IsRefusedAndWarned()
    {
        _catalog.ReadDeclaredSizes().Returns(new List<KeyValuePair<string, string>> { new("taom_elk", null!) });
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 100));

        Assert.AreEqual(0, _sut.ApplyMonsterSizes());
        _catalog.DidNotReceive().SetBodyLength(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("taom_elk") && m.Contains(MonsterSizeConfig.AttributeName)));
    }

    [TestMethod]
    public void ApplyMonsterSizes_FailureAfterAWrite_ReportsTheItemsAlreadyWritten()
    {
        GivenSizes(("taom_elk", "110"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 100), new HorseItemRecord("taom_elk_b", "taom_elk", 100));
        _catalog.SetBodyLength("taom_elk_b", 110).Throws(new InvalidOperationException("setter gone"));

        Assert.AreEqual(1, _sut.ApplyMonsterSizes(), "the one item already written counts");
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("setter gone") && m.Contains("taom_elk_a=110")));
    }

    // TryParseBodyLength is the one rule the service and the Armory data tests share.
    [DataTestMethod]
    [DataRow("150", 150)]
    [DataRow(" 110 ", 110)]
    public void TryParseBodyLength_DigitsInRange_ParsesTrimmed(string raw, int expected)
    {
        Assert.IsTrue(MonsterSizeService.TryParseBodyLength(raw, out int value));
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("+110")]
    [DataRow("1,000")]
    [DataRow("9")]
    [DataRow(null)]
    public void TryParseBodyLength_SignSeparatorBelowMinOrNull_IsRefused(string? raw)
    {
        Assert.IsFalse(MonsterSizeService.TryParseBodyLength(raw, out _));
    }

    [TestMethod]
    public void ApplyMonsterSizes_Written_LogsOneSummaryNamingEachItem()
    {
        GivenSizes(("taom_elk", "110"), ("taom_animalia_moose", "150"));
        GivenItems(new HorseItemRecord("taom_elk_a", "taom_elk", 0), new HorseItemRecord("taom_animalia_moose_a", "taom_animalia_moose", 0));

        _sut.ApplyMonsterSizes();

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("taom_elk_a=110") && m.Contains("taom_animalia_moose_a=150")));
    }
}
