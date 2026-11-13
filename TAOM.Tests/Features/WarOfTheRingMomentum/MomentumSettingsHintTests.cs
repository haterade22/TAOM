using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// The Momentum hint once said reaching the threshold ENDS the war, while victory is off by default and the war is endless.
/// Pins the hint to the default of "Enable Victory (End the War)".
/// </summary>
[TestClass]
public class MomentumSettingsHintTests
{
    private static string Hint(string propertyName)
    {
        var property = typeof(TaomSettings).GetProperty(propertyName)!;
        var attribute = property.GetCustomAttributes().First(a => a.GetType().Name == "SettingPropertyBoolAttribute");
        return (string)attribute.GetType().GetProperty("HintText")!.GetValue(attribute)!;
    }

    [TestMethod]
    public void VictoryIsOffByDefault_AndTheTrackingHintSaysTheWarIsEndless()
    {
        Assert.IsFalse(new TaomSettings().MomentumVictoryEnabled, "if the default flips, reword the tracking hint too");

        var hint = Hint(nameof(TaomSettings.MomentumEnabled));
        StringAssert.Contains(hint, "Enable Victory (End the War)");
        StringAssert.Contains(hint, "endless");
        Assert.IsFalse(hint.Contains("ENDS the war"), "the war does not end by default");
    }

    [TestMethod]
    public void MomentumHints_HaveNoEmOrEnDash()
    {
        foreach (var name in new[] { nameof(TaomSettings.MomentumEnabled), nameof(TaomSettings.MomentumVictoryEnabled) })
        {
            var hint = Hint(name);
            Assert.IsFalse(hint.Contains('—') || hint.Contains('–'), name + " hint has an em or en dash");
        }
    }
}
