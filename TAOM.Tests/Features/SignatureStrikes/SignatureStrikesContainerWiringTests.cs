using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// <c>SubModule</c> resolves <see cref="ISignatureStrikeService"/> and <see cref="ISignatureAgentRoster"/> for the campaign
/// damage model and, since #788, for Custom Battle's twin in <c>RegisterCustomBattleModels</c>, unguarded at every game
/// start: a registration the container cannot satisfy would stop a Custom Battle from starting. DryIoc's Validate walks the
/// graph without constructing anything, so this needs no game; the substitutes are the dependencies other registrations
/// supply.
/// </summary>
[TestClass]
public class SignatureStrikesContainerWiringTests
{
    [TestMethod]
    public void ServiceAndRoster_ResolveFromTheFeatureRegistration()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<INazgulRegistry>());
        SignatureStrikesIoC.RegisterSignatureStrikesFeature(container);

        Assert.IsTrue(container.IsRegistered<ISignatureStrikeService>(), "RegisterSignatureStrikesFeature does not register ISignatureStrikeService");
        Assert.IsTrue(container.IsRegistered<ISignatureAgentRoster>(), "RegisterSignatureStrikesFeature does not register ISignatureAgentRoster");
        var errors = container.Validate(typeof(ISignatureStrikeService), typeof(ISignatureAgentRoster));

        Assert.AreEqual(0, errors.Length,
            "the Signature Strikes services are not resolvable: " + string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
