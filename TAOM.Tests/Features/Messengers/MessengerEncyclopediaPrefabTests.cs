using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Messengers.UI;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// The Send Messenger button on the hero page. Gauntlet never hovers a disabled widget, so the hint for the disabled
/// state must sit outside the ButtonWidget whose IsEnabled is bound (vanilla's governor-button shape, #944).
/// </summary>
[TestClass]
public class MessengerEncyclopediaPrefabTests
{
    private static XmlElement Root()
    {
        var doc = new MessengerEncyclopediaPrefabExtension().GetDocument();
        return doc.DocumentElement!;
    }

    [TestMethod]
    public void TheButton_SitsInAFixedWrapper_ThatStaysHoverable()
    {
        var wrapper = (XmlElement?)Root().SelectSingleNode("Children/Widget");
        Assert.IsNotNull(wrapper, "a wrapper Widget must hold the button");
        foreach (var attr in new[] { "IsEnabled", "IsDisabled", "DoNotAcceptEvents", "DoNotPassEventsToChildren" })
            Assert.IsFalse(wrapper.HasAttribute(attr), "the wrapper must stay hoverable and pass events on, so it carries no " + attr);
        Assert.IsNotNull(wrapper.SelectSingleNode("Children/ButtonWidget"), "the button is a direct child of the wrapper");
        Assert.AreEqual("Fixed", wrapper.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("227", wrapper.GetAttribute("SuggestedWidth"));
        Assert.AreEqual("40", wrapper.GetAttribute("SuggestedHeight"));
    }

    [TestMethod]
    public void TheDisabledStateHint_IsOutsideTheButtonWidget()
    {
        var hint = (XmlElement?)Root().SelectSingleNode("Children/Widget/Children/HintWidget");
        Assert.IsNotNull(hint, "the wrapper holds its own HintWidget");
        Assert.IsNull(hint.SelectSingleNode("ancestor::ButtonWidget"), "the hint must not sit inside the disabled button");
        Assert.AreEqual("{SendMessengerHint}", hint.GetAttribute("DataSource"));
    }

    [TestMethod]
    public void TheButton_KeepsItsEnabledBindingAndClickCommand()
    {
        var button = (XmlElement?)Root().SelectSingleNode("Children/Widget/Children/ButtonWidget");
        Assert.IsNotNull(button);
        Assert.AreEqual("@IsMessengerAvailable", button.GetAttribute("IsEnabled"));
        Assert.AreEqual("ExecuteSendMessenger", button.GetAttribute("Command.Click"));
        // The mixin binds an empty hint whenever the button is enabled (MessengerEncyclopediaMixin.UpdateAvailability),
        // so a hint inside the button could never show anything: the one hint sits on the wrapper.
        Assert.IsNull(button.SelectSingleNode("descendant::HintWidget"), "no hint inside the button: it would never show text");
    }
}
