using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Hooks;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// <c>SubModule</c> resolves <see cref="CombatMechanicsHooks"/> for the campaign damage model and Custom Battle's twin at every game start (#737, #788),
/// so a registration the container cannot satisfy fails only there, in game. DryIoc's Validate walks the graph without
/// constructing anything, so this needs no campaign; the three substitutes are the dependencies other modules register.
/// </summary>
[TestClass]
public class CombatMechanicsContainerWiringTests
{
    [TestMethod]
    public void Hooks_ResolveFromTheFeatureRegistration()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        CombatMechanicsIoC.RegisterCombatMechanicsFeature(container);

        // The registration is pinned on its own, so the check does not lean on how Validate treats an unregistered type.
        Assert.IsTrue(container.IsRegistered<CombatMechanicsHooks>(), "RegisterCombatMechanicsFeature does not register CombatMechanicsHooks");
        var errors = container.Validate(typeof(CombatMechanicsHooks));

        Assert.AreEqual(0, errors.Length,
            "CombatMechanicsHooks is not resolvable: " + string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
