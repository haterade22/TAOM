using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.BattleActionBar;

namespace TAOM.Tests.Features.CompanionTactics;

[TestClass]
public class CompanionTacticsWiringTests
{
    [TestMethod]
    public void RegisterCompanionTacticsFeature_BattleActionBarService_Resolves()
    {
        // IOutcomeLog is registered in Main/IoC.cs, beside IModLogger; the feature module does not register it.
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);

        var errors = container.Validate(typeof(IBattleActionBarService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
