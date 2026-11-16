using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The two career-ability toasts (#1385). The key name and the seconds ride as text variables, so a
/// translation keeps them. Assertions read <c>Value</c> and <c>Attributes</c>, never <c>ToString()</c>,
/// which needs MBTextManager and a running Module.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CareerAbilityMessagesTests
{
    [TestMethod]
    public void Ready_BoundKey_NamesThatKey()
    {
        var message = CareerAbilityMessages.Ready("G");

        StringAssert.StartsWith(message.Value, "{=taom_career_ability_ready}");
        StringAssert.Contains(message.Value, "{KEY}");
        Assert.AreEqual("G", message.Attributes["KEY"].ToString());
    }

    [TestMethod]
    public void Ready_UnboundKey_SaysToBindOneInsteadOfNamingABlank()
    {
        var message = CareerAbilityMessages.Ready("");

        StringAssert.StartsWith(message.Value, "{=taom_career_ability_ready_unbound}");
        Assert.IsTrue(message.Attributes == null || !message.Attributes.ContainsKey("KEY"));
    }

    [TestMethod]
    public void Charging_BindsTheSeconds()
    {
        var message = CareerAbilityMessages.Charging(4);

        StringAssert.StartsWith(message.Value, "{=taom_career_ability_charging}");
        Assert.AreEqual("4", message.Attributes["SECONDS"].ToString());
    }
}
