using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.SpecialResources;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #768: SpecialResourceService takes ICareerPassiveService as an OPTIONAL constructor parameter, so a
/// dependency that stops resolving (CareerPassiveService gained IPartySizeCacheInvalidator, which
/// production registers in IoC.RegisterCoreServices) would silently leave the service career-blind with
/// no error. This pins the graph across the two features, in the Arena/CompanionTactics wiring shape.
/// </summary>
[TestClass]
public class SpecialResourcesWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        // Production registers this in IoC.RegisterCoreServices, outside both features.
        container.RegisterInstance(Substitute.For<IPartySizeCacheInvalidator>());
        CareerSystemIoC.RegisterCareerSystemFeature(container);
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        return container;
    }

    [TestMethod]
    public void RegisterSpecialResources_WithCareerSystem_GraphResolvesWithoutError()
    {
        var container = BuildContainer();

        var errors = container.Validate(typeof(ISpecialResourceService), typeof(ICareerPassiveService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ResolveSpecialResourceService_WithCareerSystem_HasACareerPassiveService()
    {
        var container = BuildContainer();

        var service = container.Resolve<ISpecialResourceService>();

        var field = typeof(SpecialResourceService).GetField("_passiveService", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "SpecialResourceService._passiveService was renamed: update this wiring test.");
        Assert.IsNotNull(field.GetValue(service),
            "The optional ICareerPassiveService did not resolve, so SpecialResources runs career-blind.");
    }

    [TestMethod]
    public void MainIoC_RegistersThePartySizeCacheInvalidator_InRegisterCoreServices()
    {
        // Both CareerSystem and AiPartySize take this dependency; the tests above fake it, so only the
        // source can show the production registration still exists (#768).
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        int start = src.IndexOf("void RegisterCoreServices(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "IoC.RegisterCoreServices was renamed: update this pin.");
        int open = src.IndexOf('{', start);
        int depth = 0, end = -1;
        for (int i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) { end = i; break; }
        }
        Assert.AreNotEqual(-1, end, "RegisterCoreServices body not found.");

        StringAssert.Contains(src.Substring(open, end - open),
            "container.Register<IPartySizeCacheInvalidator, PartySizeCacheInvalidator>(Reuse.Singleton);");
    }
}
