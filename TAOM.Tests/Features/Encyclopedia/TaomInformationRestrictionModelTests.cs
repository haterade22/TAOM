using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Models;

namespace TAOM.Tests.Features.Encyclopedia;

[TestClass]
public class TaomInformationRestrictionModelTests
{
    [TestMethod]
    public void DoesPlayerKnowDetailsOf_WhenShowAllEnabled_ReturnsTrueWithoutCallingBase()
    {
        var settings = Substitute.For<IEncyclopediaSettingsProvider>();
        settings.ShowAllEncyclopediaCharacters.Returns(true);
        var model = new TaomInformationRestrictionModel(settings);

        // Passes null hero — safe because we short-circuit before base is invoked
        Assert.IsTrue(model.DoesPlayerKnowDetailsOf((Hero)null!));
    }

    [TestMethod]
    public void DoesPlayerKnowDetailsOf_WhenShowAllDisabled_DelegatesToBase()
    {
        var settings = Substitute.For<IEncyclopediaSettingsProvider>();
        settings.ShowAllEncyclopediaCharacters.Returns(false);
        var model = new TaomInformationRestrictionModel(settings);

        // Base requires campaign state — NullReferenceException proves base was called
        // (our code did not short-circuit)
        Assert.ThrowsException<NullReferenceException>(
            () => model.DoesPlayerKnowDetailsOf((Hero)null!));
    }
}
