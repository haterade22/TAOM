using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.XmlMerge;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Wiring regression guard for the XmlMerge fast path (plan 042): the module is listed, declares Patch99 at process
/// load (a game's merges run before the late batch), and a fresh DryIoc container can build the service. The last
/// catches a non-public constructor, which the tests would reach through InternalsVisibleTo but the game's container
/// could not.
/// </summary>
[TestClass]
public class XmlMergeWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheXmlMergeModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<XmlMergeModule>().Count(),
            "XmlMergeModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresTheCategoryAtProcessLoad()
    {
        var decls = new XmlMergeModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch99_XmlMergeFastPath", decls[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decls[0].Phase);
    }

    [TestMethod]
    public void Module_RegistersAServiceTheContainerCanBuild()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new XmlMergeModule().RegisterServices(container);
        // The real adapter's constructor reflects on the engine; this test is about the container.
        container.RegisterInstance<IXmlMergeEngineAdapter>(new RecordingXmlMergeEngine(), IfAlreadyRegistered.Replace);

        var service = container.Resolve<XmlMergeService>();

        Assert.IsNotNull(service);
        Assert.AreSame(service, container.Resolve<XmlMergeService>(), "the once-only latches only work as a singleton");
    }
}
