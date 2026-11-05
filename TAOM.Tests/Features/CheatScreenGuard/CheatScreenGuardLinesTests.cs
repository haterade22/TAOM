using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CheatScreenGuard;
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Tests.Features.CheatScreenGuard;

[TestClass]
public class CheatScreenGuardLinesTests
{
    [TestMethod]
    public void InstallLine_StartsWithTheOnTag()
        => StringAssert.StartsWith(CheatScreenGuardLines.BuildInstallLine(), "[CheatScreenGuard] ON:");

    [TestMethod]
    public void PartialLine_NamesBothGates_ForEachCount()
    {
        var line = CheatScreenGuardLines.BuildPartialInstallLine(1, 0);
        StringAssert.Contains(line, "inventory gate on");
        StringAssert.Contains(line, "party screen gate off (the call site was not found exactly once)");

        line = CheatScreenGuardLines.BuildPartialInstallLine(-1, 1);
        StringAssert.Contains(line, "inventory gate off (the patch did not apply)");
        StringAssert.Contains(line, "party screen gate on");
    }

    [TestMethod]
    public void HiddenLine_NamesTheScreenAndTheSetting()
    {
        var inventory = CheatScreenGuardLines.BuildHiddenLine(CheatScreen.Inventory);
        StringAssert.Contains(inventory, "inventory");
        StringAssert.Contains(inventory, "Cheat Mode Lists All Items And Troops");

        var party = CheatScreenGuardLines.BuildHiddenLine(CheatScreen.PartyScreen);
        StringAssert.Contains(party, "party screen");
        StringAssert.Contains(party, "Cheat Mode Lists All Items And Troops");
    }
}
