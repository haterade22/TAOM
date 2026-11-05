using System.Collections;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #1019: the board is stored as a flat string, which the menu re-parses as text every frame. A stored string that
/// parsed differently from what was rendered (a leftover brace, a tag) would show on screen, so rendering the variable
/// must give back exactly the stored string, with every optional line present.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServiceStatusTextWriterRenderTests
{
    private const string Variable = "TAOM_ENLISTMENT_WAIT_TEXT";

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static IDictionary Variables()
    {
        var context = typeof(MBTextManager).GetField("TextContext", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        return (IDictionary)context.GetType().GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context);
    }

    private static string Stored() => ((TextObject)Variables()[Variable]).Value;

    private static ServiceStatusModel EveryLine() => new ServiceStatusModel(
        commanderName: "Aragorn", activity: CommanderActivity.InBattle, settlementName: "Minas Tirith",
        rank: ServiceRank.Veteran, daysServed: 24, trust: 9, deferredWages: 40, activeDutyId: "recon_sweep",
        serviceXp: 100, dailyWage: 12, nextRank: ServiceRank.Sergeant, nextRequirementKey: "xp",
        nextRequirementTarget: 200, activeDutyTitle: "Scout the ford");

    [TestMethod]
    public void Write_WhenNewlineWasNeverSet_StillStoresTheBoardWithLineBreaks()
    {
        // The engine sets the NEWLINE global lazily inside its UI builders, and a new campaign clears all globals.
        Variables().Remove("NEWLINE");
        var status = new ServiceStatusModel(
            commanderName: "Aragorn", activity: CommanderActivity.Marching, rank: ServiceRank.Veteran,
            daysServed: 24, trust: 9, serviceXp: 100, dailyWage: 12);

        new ServiceStatusTextWriter().Write(status);

        StringAssert.Contains(Stored(), "\n");
    }

    [TestMethod]
    public void Write_EveryOptionalLine_RendersTheStoredStringUnchanged()
    {
        new ServiceStatusTextWriter().Write(EveryLine());
        var rendered = new TextObject("{" + Variable + "}").ToString();

        Assert.AreEqual(Stored(), rendered);
        StringAssert.Contains(rendered, "Toward ");
        StringAssert.Contains(rendered, "Pay owed to you: 40");
        StringAssert.Contains(rendered, "You have orders: ");
        Assert.IsFalse(rendered.Contains("{") || rendered.Contains("}"), rendered);
    }

    [TestMethod]
    public void IsStale_IsTrueBeforeTheFirstWrite_AndFalseAfterIt()
    {
        var writer = new ServiceStatusTextWriter();
        Assert.IsTrue(writer.IsStale, "a writer that never rendered has no language to match");

        writer.Write(EveryLine());

        Assert.IsFalse(writer.IsStale, "the board was just rendered in the active language");
    }
}
