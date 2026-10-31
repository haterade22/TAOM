using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.InputSystem;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>
/// The command panel's key table (moved out of the view to keep it under the ADR-002 cap) against the same two-lane fixture match
/// as <see cref="FordsCommandVMTests"/>: gold 1000, offers inf 100 first, heroes hero_a 700 and hero_b 400.
/// </summary>
[TestClass]
public class FordsCommandHotkeysTests
{
    private FordsSession _session = null!;
    private FordsCommandVM _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        var battlefield = Substitute.For<IFordsBattlefieldAdapter>();
        battlefield.CharacterName(Arg.Any<string>()).Returns(call => "Name_" + call.Arg<string>());
        _session = new FordsSession();
        _session.Fill(new object(), Create(), WestSlot, battlefield, new Vec3f(0f, 25f, 45f), new Vec3f(1000f, 25f, 0f), new List<Vec3f>());
        _vm = new FordsCommandVM(_session, () => { });
        _vm.Refresh();
    }

    private static IInputContext Pressed(params InputKey[] keys)
    {
        var input = Substitute.For<IInputContext>();
        foreach (var key in keys)
            input.IsKeyPressed(key).Returns(true);
        return input;
    }

    [TestMethod]
    public void Read_SecondLaneKey_SelectsTheSecondLane()
    {
        FordsCommandHotkeys.Read(Pressed(InputKey.D2), _vm);

        Assert.AreEqual(1, _vm.SelectedLane);
    }

    [TestMethod]
    public void Read_FirstBuyKey_BuysTheFirstPack()
    {
        FordsCommandHotkeys.Read(Pressed(InputKey.F1), _vm);

        Assert.AreEqual(900, _session.GetView()!.Gold);
    }

    [TestMethod]
    public void Read_HeroKey_FieldsTheFirstAffordableHero()
    {
        FordsCommandHotkeys.Read(Pressed(InputKey.H), _vm);

        Assert.AreEqual(300, _session.GetView()!.Gold);
    }

    [TestMethod]
    public void Read_HoldKey_SetsHoldOnTheSelectedLane()
    {
        FordsCommandHotkeys.Read(Pressed(InputKey.X), _vm);

        Assert.AreEqual(LaneOrder.Hold, _session.GetView()!.Lanes[0].Order);
    }

    [TestMethod]
    public void Read_WithControlHeld_IgnoresEveryKey()
    {
        var input = Pressed(InputKey.F1, InputKey.X);
        input.IsControlDown().Returns(true);

        FordsCommandHotkeys.Read(input, _vm);

        Assert.AreEqual(1000, _session.GetView()!.Gold);
        Assert.AreEqual(LaneOrder.Push, _session.GetView()!.Lanes[0].Order);
    }

    [TestMethod]
    public void Read_WithAltHeld_IgnoresEveryKey()
    {
        var input = Pressed(InputKey.F1);
        input.IsAltDown().Returns(true);

        FordsCommandHotkeys.Read(input, _vm);

        Assert.AreEqual(1000, _session.GetView()!.Gold);
    }
}
