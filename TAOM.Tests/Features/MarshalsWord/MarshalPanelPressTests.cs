using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>A click on a panel entry stores the same press as a key, for the companies the click named (build-spec 10): the panel closes after the click and may clear its selection first.</summary>
[TestClass]
public class MarshalPanelPressTests
{
    private const int Clicked = 3;

    private FakeMarshalOrders _orders = null!;
    private MarshalFireDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalFireTable.Begin();
        _orders = new FakeMarshalOrders();
        _driver = new MarshalFireDriver(_orders, new FakeMarshalFire(), new RecordingOutcomeLog(), new ListLogger(), _ => { }, "7");
    }

    [TestCleanup]
    public void Cleanup() => MarshalFireTable.Clear();

    private void Pass(double now)
    {
        _orders.Now = now;
        _driver.Pass();
    }

    [TestMethod]
    public void PressFire_ForTheCompaniesOfAClick_ChangesThem_EvenWhenTheSelectionIsGoneAtTheTick()
    {
        _driver.PressFire(new List<int> { Clicked });

        Pass(0.0);

        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(Clicked));
    }

    [TestMethod]
    public void PressFire_ForTheCompaniesOfAClick_LeavesTheSelectedCompaniesAlone()
    {
        _orders.Selected.Add(5);

        _driver.PressFire(new List<int> { Clicked });
        Pass(0.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(5));
    }

    [TestMethod]
    public void PressFire_AKeyPressAfterAClick_UsesTheSelectionAtTheTick_SoTheClicksCompaniesAreUsedOnce()
    {
        _orders.Selected.Add(5);
        _driver.PressFire(new List<int> { Clicked });
        Pass(0.0);

        _driver.PressFire();
        Pass(0.1);

        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(5));
        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(Clicked), "the second press did not touch it again");
    }

    [TestMethod]
    public void PressWord_ForTheCompaniesOfAClick_GivesThemTheWord()
    {
        MarshalFireTable.SetMode(Clicked, FireMode.OnWord);

        _driver.PressWord(new List<int> { Clicked });
        Pass(2.0);

        Assert.AreEqual(2.0, MarshalFireTable.WordTimeOf(Clicked));
    }

    [TestMethod]
    public void PressHold_ForTheCompaniesOfAClick_TurnsTheirHoldOn()
    {
        _driver.PressHold(new List<int> { Clicked });

        Pass(0.0);

        Assert.IsTrue(MarshalFireTable.HoldOf(Clicked));
    }

    [TestMethod]
    public void PressHold_ForACompanyUnderTheAi_IsSkipped_AsTheKeyIs()
    {
        _orders.Lost.Add(Clicked);

        _driver.PressHold(new List<int> { Clicked });
        Pass(0.0);

        Assert.IsFalse(MarshalFireTable.HoldOf(Clicked));
    }
}
