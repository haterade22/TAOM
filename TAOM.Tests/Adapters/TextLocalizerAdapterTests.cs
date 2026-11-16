using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The real TextObject, in English: it fills a template's variables by name, lowercase names
/// included, so the siege defense texts keep their {settlement}-style tokens (#1385).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TextLocalizerAdapterTests
{
    [TestMethod]
    public void Localize_LowercaseVariables_FillsEachToken()
    {
        var text = new TextLocalizerAdapter().Localize(
            "{=taom_siege_defense_default_title}{attacker} is besieging {settlement}!",
            new Dictionary<string, string> { ["attacker"] = "Mordor", ["settlement"] = "Minas Tirith" });

        Assert.AreEqual("Mordor is besieging Minas Tirith!", text);
    }

    [TestMethod]
    public void Localize_EmptyVariable_RendersNothingForIt()
    {
        var text = new TextLocalizerAdapter().Localize(
            "{=taom_siege_defense_default_title}[{attacker}]",
            new Dictionary<string, string> { ["attacker"] = "" });

        Assert.AreEqual("[]", text);
    }

    [TestMethod]
    public void Localize_EmptyRawWithVariables_ReturnsEmpty()
    {
        var text = new TextLocalizerAdapter().Localize("", new Dictionary<string, string> { ["attacker"] = "Mordor" });

        Assert.AreEqual("", text);
    }
}
