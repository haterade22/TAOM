using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// When a kingdom loses its naming title, Realms gives it its own name back only while it still has the name Realms
/// set (#1297). A name the player chose in the kingdom screen meanwhile stays.
/// </summary>
[TestClass]
public class KingdomNamesRestoreTests
{
    [TestMethod]
    public void StillNamedByRealms_TheRealmsName_IsRestored() =>
        Assert.IsTrue(KingdomNames.StillNamedByRealms("Durin's Folk", "Durin's Folk"));

    [TestMethod]
    public void StillNamedByRealms_ThePlayerRenamedIt_IsKept() =>
        Assert.IsFalse(KingdomNames.StillNamedByRealms("The Iron Hills Pact", "Durin's Folk"));

    [TestMethod]
    public void StillNamedByRealms_NoNameSetThisSession_IsRestored() =>
        Assert.IsTrue(KingdomNames.StillNamedByRealms("Durin's Folk", null));
}
