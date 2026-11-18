using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EngineBugGuards.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EngineBugGuards;

/// <summary>
/// Patch167 (#1444, from Silas He's v1.5.6 fix package) keeps a dead hero's kits through <c>Hero.OnDeath</c> by the
/// private saveable properties <c>_battleEquipment</c> and <c>_civilianEquipment</c> (v1.5.7 Hero.cs :212-216). The
/// general binding gate resolves <c>OnDeath</c>; this resolves the four accessors, which a rename would turn into a
/// silent no-op.
/// </summary>
[TestClass]
public class Patch167DeadHeroOutfitTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void KitAccessors_BindAgainstTheInstalledEngine()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(Patch167_DeadHeroOutfit.GetBattle, "Hero._battleEquipment getter");
        Assert.IsNotNull(Patch167_DeadHeroOutfit.SetBattle, "Hero._battleEquipment setter");
        Assert.IsNotNull(Patch167_DeadHeroOutfit.GetCivilian, "Hero._civilianEquipment getter");
        Assert.IsNotNull(Patch167_DeadHeroOutfit.SetCivilian, "Hero._civilianEquipment setter");
    }
}
