using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.CoopCompat.Handshake;
using TAOM.CoopCompat.Transport;

namespace TAOM.CoopCompat.Tests.Handshake;

[TestClass]
public class BuildIdentityTests
{
    [TestMethod]
    public void FromLoadedAssemblies_CoversBothModuleDlls()
    {
        var identity = BuildIdentity.FromLoadedAssemblies();
        var scanned = typeof(CoopCompatSubModule).Assembly.ManifestModule.ModuleVersionId.ToString();
        var coopFacing = typeof(CoopTransport).Assembly.ManifestModule.ModuleVersionId.ToString();

        StringAssert.Contains(identity.CoopCompatMvid, scanned);
        StringAssert.Contains(identity.CoopCompatMvid, coopFacing, "A different TAOM.CoopCompat.Coop.dll must count as a different build.");
    }
}
