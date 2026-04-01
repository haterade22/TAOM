using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Encyclopedia.Models;

namespace TAOM.Tests.Features.Encyclopedia;

[TestClass]
public class TaomInformationRestrictionModelTests
{
    [TestMethod]
    public void DoesPlayerKnowDetailsOf_WhenShowAllEnabled_ReturnsTrueWithoutCallingBase()
    {
        var model = new TaomInformationRestrictionModel(() => true);

        // Passes null hero — safe because we short-circuit before base is invoked
        Assert.IsTrue(model.DoesPlayerKnowDetailsOf((Hero)null!));
    }

    [TestMethod]
    public void DoesPlayerKnowDetailsOf_WhenShowAllDisabled_DelegatesToBase()
    {
        var model = new TaomInformationRestrictionModel(() => false);

        // Base requires campaign state — NullReferenceException proves base was called
        // (our code did not short-circuit)
        Assert.ThrowsException<NullReferenceException>(
            () => model.DoesPlayerKnowDetailsOf((Hero)null!));
    }
}
