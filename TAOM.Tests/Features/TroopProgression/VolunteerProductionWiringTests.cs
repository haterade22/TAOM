using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CulturalFeats;
using TAOM.Features.TroopProgression;
using TAOM.Features.WarChronicle;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// The volunteer production service SubModule resolves at every campaign start takes its dependencies
/// from three registration sites: TroopProgressionIoC (the service), CulturalFeatsIoC (the feats) and
/// the War Chronicle module (the effect registry); the castle fill consumes it too. DryIoc's Validate
/// walks the graph without constructing anything, so this runs with no campaign and no game, with the
/// real registrations rather than substitutes for the cross-feature dependencies.
/// </summary>
[TestClass]
public class VolunteerProductionWiringTests
{
    [TestMethod]
    public void VolunteerProduction_AllThreeFeaturesRegistered_ResolvesWithoutErrors()
    {
        using var container = new Container();
        // Core services IoC.cs owns.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        TroopProgressionIoC.RegisterTroopProgressionFeature(container);
        CulturalFeatsIoC.RegisterCulturalFeatsFeature(container);
        CastleRecruitmentIoC.RegisterCastleRecruitmentFeature(container);
        new WarChronicleModule().RegisterServices(container);

        var errors = container.Validate(typeof(VolunteerProductionService), typeof(ICastleRecruitmentService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
