using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// Skeleton Buffer and the Anim Memory probe both register the shared engine signature cache. The main container keeps
/// DryIoc's default AppendNotKeyed rule, so two plain registrations would make the cache unresolvable. Both modules
/// together must still resolve one cache.
/// </summary>
[TestClass]
public class EngineSignatureCacheRegistrationTests
{
    [TestMethod]
    public void BothModulesRegistered_ResolveOneSharedCache()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());

        new SkeletonBufferModule().RegisterServices(container);
        new AnimMemoryProbeModule().RegisterServices(container);

        var first = container.Resolve<IEngineSignatureCache>();
        var second = container.Resolve<IEngineSignatureCache>();

        Assert.IsNotNull(first);
        Assert.AreSame(first, second);
        Assert.IsNotNull(container.Resolve<IEngineSignatureCacheAdapter>());
    }
}
