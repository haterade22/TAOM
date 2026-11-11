using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// Co-op (docs/features/coop-interop.md): only the host changes titles. A client keeps the ledger it loaded with the
/// host's save and never creates one of its own, so it cannot hand out starting grants the host never made.
/// </summary>
[TestClass]
public class RealmsCoopGateTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void A_coop_client_creates_no_titles()
    {
        var previous = LiegeArmyDiscount.Behavior;
        try
        {
            var realms = new RealmsCampaign(new NullRealmsPopupAdapter(), () => false);

            Assert.IsFalse(realms.EnsureInitialized());
            Assert.IsFalse(realms.Service.IsInitialized);
            Assert.IsFalse(realms.IsDisabled, "A client's ledger is waiting for the host's save, not switched off.");
        }
        finally
        {
            LiegeArmyDiscount.Behavior = previous;
        }
    }
}
